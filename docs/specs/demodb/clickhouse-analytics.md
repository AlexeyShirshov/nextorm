# ClickHouse / аналитика (`datasets.hits_v1`)

Модельные запросы: [`docs/specs/clickhouse-demodb/`](../clickhouse-demodb).

Источник модели данных — официальный сэмпл ClickHouse «Обезличенная веб-аналитика»
(<https://clickhouse.com/docs/ru/get-started/sample-datasets/anon-web-analytics-metrica>): две
таблицы `datasets.hits_v1` и `datasets.visits_v1` (обезличенные данные Яндекс.Метрики). Запросы
демо-папки используют `datasets.hits_v1` и материализованное представление
`datasets.daily_unique_users_mv` (AggregatingMergeTree) — **последнего нет в официальном сэмпле**,
это надстройка демо-стенда, которая создаётся фикстурой (см.
[verification-plan.md](verification-plan.md#33-clickhouse-hits_v1--daily_unique_users_mv)).

Из-за «продвинутых» конструкций ClickHouse покрытие здесь заметно ниже, чем у SQL-провайдеров, и
большинство запросов пока требует `WithSql`; это отражено в
[todo_clickhouse.md](../roadmap/todo_clickhouse.md).

## Сущности

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.DemoDb.ClickHouse;

[SqlTable("datasets.hits_v1")]
public interface IHit
{
    [Key, Column("WatchID")] ulong WatchId { get; set; }
    [Column("UserID")] ulong UserId { get; set; }
    [Column("EventDate")] DateTime EventDate { get; set; }
    [Column("EventTime")] DateTime EventTime { get; set; }
    [Column("URL")] string Url { get; set; }
    [Column("SearchPhrase")] string SearchPhrase { get; set; }
}

[SqlTable("datasets.daily_unique_users_mv")]
public interface IDailyUniqueUsers
{
    [Key, Column("EventDate")] DateTime EventDate { get; set; }
    // AggregateFunction(uniq, UInt64): значение-состояние нельзя проецировать как скаляр,
    // его читают только через uniqMerge(...) внутри запроса.
    [Column("users_state")] object? UsersState { get; set; }
}
```

Общие правила:

* **`UserID` — `UInt64`.** Если row reader не материализует `ulong` напрямую, колонку берут через
  `toInt64(UserID)`; это видно при запуске сверки (см. [verification-plan.md](verification-plan.md)).
* **Сырой SQL**: `WithSql`/`PrepareFromSql` заменяют текст запроса, но **проекцию** задаёт LINQ-запрос
  до замены. Алиасы в сыром SQL должны совпадать с именами членов DTO (`[Column]`).
* **Массивы/кортежи** (`groupArray`, `Array(...)`) проецировать как колонку нельзя — в сыром SQL для
  сверки их приводят к строке (`arrayStringConcat(...)`/`toString(...)`).

Шаблон сырого запроса (на примере воронки):

```csharp
public sealed class FunnelRow
{
    [Column("level")] public int Level { get; set; }
    [Column("conversion_count")] public long ConversionCount { get; set; }
}

private const string FunnelSql = /* текст из clickhouse_funnel.sql */;

var rows = await ctx.From<IHit>()
    // выражения задают только форму результата — SQL заменяется целиком
    .Select(h => new FunnelRow { Level = (int)h.WatchId, ConversionCount = (long)h.UserId })
    .WithSql(FunnelSql)
    .ToListAsync();
```

---

## 1. `clickhouse_array_analytics.sql` — массивы (`arrayMap`/`arrayFilter`)

**Покрытие: raw.** `splitByChar` есть в API, но higher-order `arrayMap`/`arrayFilter` ещё не
реализованы (todo_clickhouse, уровень 3), а группировка по массиву и вовсе не выражается —
результат-массив не материализуется.

```csharp
public sealed class WordFrequencyRow
{
    // в сыром SQL массив приводится к строке: arrayStringConcat(clean_words, ',')
    [Column("clean_words")] public string CleanWords { get; set; } = "";
    [Column("occurrence")] public int Occurrence { get; set; }
}

// исходный запрос + приведение массива к строке для сравнения
private const string ArrayAnalyticsSql =
    """
    select arrayStringConcat(clean_words, ',') as clean_words, occurrence
    from (
        select arrayFilter(x -> length(x) > 3,
                           arrayMap(x -> lower(x), splitByChar(' ', SearchPhrase))) as clean_words,
               count() as occurrence
        from datasets.hits_v1
        where SearchPhrase != '' and EventDate = '2014-03-20'
        group by clean_words
    )
    order by occurrence desc
    limit 10
    """;

var rows = await ctx.From<IHit>()
    .Select(h => new WordFrequencyRow { CleanWords = "", Occurrence = 0 })
    .WithSql(ArrayAnalyticsSql)
    .ToListAsync();
```

При появлении `arrayMap`/`arrayFilter` в API (`SqlFunctions.ClickHouse`, флаг
`SupportsArrayFunctions`) запрос можно переписать на LINQ; до тех пор это задача роадмапа.

## 2. `clickhouse_funnel.sql` — воронка (`windowFunnel`)

**Покрытие: raw.** `windowFunnel` — «продвинутый» агрегат (хронологическая воронка), не реализован.

```csharp
public sealed class FunnelRow
{
    [Column("level")] public int Level { get; set; }
    [Column("conversion_count")] public long ConversionCount { get; set; }
}

private const string FunnelSql = /* clickhouse_funnel.sql */;

var rows = await ctx.From<IHit>()
    .Select(h => new FunnelRow { Level = (int)h.WatchId, ConversionCount = (long)h.UserId })
    .WithSql(FunnelSql)
    .ToListAsync();
```

## 3. `clickhouse_incremental.sql` — инкрементальный расчёт (`uniqMerge`)

**Покрытие: raw.** Комбинатор `-Merge` (`uniqMerge(users_state)`) не поддержан. Точный `uniq` есть
(`SqlFunctions.ClickHouse.uniq_exact`), но он работает по сырым событиям, а не по состоянию
AggregatingMergeTree.

```csharp
public sealed class DailyUniqueRow
{
    [Column("Дата")] public DateTime EventDate { get; set; }
    [Column("Точное кол-во уникальных посетителей")] public long UniqueUsers { get; set; }
}

private const string IncrementalSql = /* clickhouse_incremental.sql */;

var rows = await ctx.From<IDailyUniqueUsers>()
    .Select(d => new DailyUniqueRow { EventDate = d.EventDate, UniqueUsers = 0 })
    .WithSql(IncrementalSql)
    .ToListAsync();

// Альтернатива без MV: прямой расчёт по hits_v1 (другой источник, но выражается штатно):
var direct = await ctx.From<IHit>()
    .Where(h => h.EventDate >= new DateTime(2014, 3, 1) && h.EventDate <= new DateTime(2014, 3, 31))
    .GroupBy(h => new { h.EventDate })
    .OrderByDescending(h => h.EventDate)
    .Select(h => new
    {
        h.EventDate,
        UniqueUsers = SqlFunctions.ClickHouse.uniq_exact(h.UserId)
    })
    .ToListAsync();
```

## 4. `clickhouse_retention.sql` — когортное удержание (`groupArray`)

**Покрытие: raw.** `groupArray((week_number, retention_rate))` возвращает массив кортежей; row reader
для массивов отсутствует, и «матрицу удержания» проецировать нельзя.

```csharp
public sealed class RetentionMatrixRow
{
    [Column("Неделя когорты")] public DateTime CohortWeek { get; set; }
    [Column("Размер когорты")] public long CohortSize { get; set; }
    // в сыром SQL матрица приводится к строке: arrayStringConcat(groupArray(...), ';')
    [Column("Матрица удержания")] public string Matrix { get; set; } = "";
}

private const string RetentionSql = /* clickhouse_retention.sql + arrayStringConcat(...) */;

var rows = await ctx.From<IHit>()
    .Select(h => new RetentionMatrixRow { CohortWeek = h.EventDate, CohortSize = 0, Matrix = "" })
    .WithSql(RetentionSql)
    .ToListAsync();
```

Если матричная форма не обязательна, тот же расчёт выражается штатно (строка на когорту и неделю):

```csharp
var firstVisits = ctx.From<IHit>()
    .GroupBy(h => new { h.UserId })
    .Select(h => new
    {
        h.UserId,
        CohortWeek = SqlFunctions.Sql.date_trunc("week", SqlFunctions.Sql.min(h.EventDate))
    });

var cohortSize = ctx.From(firstVisits)
    .GroupBy(f => new { f.CohortWeek })
    .Select(f => new { f.CohortWeek, Size = (long)SqlFunctions.Sql.count() });

var weeks = ctx.From(firstVisits)
    .Join(ctx.From<IHit>(), (f, h) => f.UserId == h.UserId)
    .Select(p => new
    {
        p.Item1.CohortWeek,
        WeekNumber = SqlFunctions.Sql.date_diff("day", p.Item1.CohortWeek, p.Item2.EventDate) / 7,
        p.Item2.UserId
    })
    .Where(p => p.WeekNumber <= 4);

var retention = ctx.From(weeks)
    .GroupBy(w => new { w.CohortWeek, w.WeekNumber })
    .Select(w => new
    {
        w.CohortWeek,
        w.WeekNumber,
        Users = (long)SqlFunctions.Sql.count_distinct(w.UserId)
    });

// строковая (не матричная) форма: когорта x неделя x retention_rate
var rates = await ctx.From(retention)
    .Join(cohortSize, (r, c) => r.CohortWeek == c.CohortWeek)
    .OrderByDescending(r => r.Item1.CohortWeek)
    .OrderBy(r => r.Item1.WeekNumber)
    .Select(r => new
    {
        r.Item1.CohortWeek,
        r.Item1.WeekNumber,
        CohortSize = r.Item2.Size,
        Users = r.Item1.Users,
        RetentionRate = Math.Round((double)r.Item1.Users / r.Item2.Size * 100, 2)
    })
    .ToListAsync();
```

## 5. `clickhouse_sessions.sql` — сессионные аномалии

**Покрытие: эквивалентная замена.** `lagInFrame` заменяется на `lag`, а
`runningAccumulate(if(time_diff > 1800, 1, 0))` — на кумулятивный `sum_over(case …)` с фреймом
`rows between unbounded preceding and current row`. `quantile(0.99)(x)` поддержан явно.

```csharp
// CTE sessions: time_diff через lag (вместо lagInFrame)
var sessions = ctx.From<IHit>()
    .Select(h => new
    {
        h.UserId,
        h.EventTime,
        TimeDiff = SqlFunctions.Sql.date_diff("second",
            SqlFunctions.Sql.lag(h.EventTime).Over(partitionBy: () => h.UserId, orderBy: () => h.EventTime),
            h.EventTime)
    });

// CTE session_flags: runningAccumulate(if(...)) -> кумулятивная сумма
var flags = ctx.From(sessions)
    .Select(s => new
    {
        s.UserId,
        s.EventTime,
        SessionId = SqlFunctions.Sql
            .sum_over(s.TimeDiff == null || s.TimeDiff > 1800 ? 1 : 0)
            .Over(
                partitionBy: () => s.UserId,
                orderBy: () => s.EventTime,
                frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)
    });

// CTE session_counts
var counts = ctx.From(flags)
    .GroupBy(f => new { f.UserId, f.SessionId })
    .Select(f => new
    {
        f.UserId,
        f.SessionId,
        HitsInSession = SqlFunctions.Sql.count()
    });

// Скалярный подзапрос: (select quantile(0.99)(hits_in_session) from session_counts)
var threshold = ctx.From(counts)
    .Select(c => SqlFunctions.ClickHouse.quantile(0.99, c.HitsInSession))
    .First();

var rows = await ctx.From(counts)
    .Where(c => c.HitsInSession > threshold)
    .OrderByDescending(c => c.HitsInSession)
    .Limit(100)
    .Select(c => new
    {
        c.UserId,
        SessionId = c.SessionId,
        Hits = c.HitsInSession
    })
    .ToListAsync();
```

Семантика `lag`/`lagInFrame` и `sum_over(if(...))`/`runningAccumulate` совпадает при полной
сортировке окна; расхождение возможно на строках с одинаковым `EventTime` (тай-брейк не задан и
там, и там). Это проверяется отдельным тестом сверки.
