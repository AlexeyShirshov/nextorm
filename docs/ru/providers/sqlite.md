# Провайдер SQLite

> Используйте `nextorm.sqlite` для файловых или in-memory баз данных SQLite; он отрисовывает параметры `$name`, разбиение на страницы `limit`/`offset`, coalesce `ifnull` и части даты `strftime`, а также регистрирует пользовательские агрегаты `stdev`/`var`.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

[`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext) (`src/nextorm.sqlite/SqliteDataContext.cs`) оборачивает `Microsoft.Data.Sqlite`. Он создаёт
`SqliteConnection` из строки подключения, регистрирует пользовательские агрегатные функции на каждом
соединении, которое создаёт, и возвращает [`Instance`](xref:NextORM.Sqlite.SqliteDialect.Instance) из своего свойства `Dialect`.

[`SqliteDialect`](xref:NextORM.Sqlite.SqliteDialect) (`src/nextorm.sqlite/SqliteDialect.cs`) — это диалект:

- плейсхолдер параметра `$name`;
- конкатенация строк с помощью `||`;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect.MakeCoalesce(System.String,System.String)) отрисовывает `ifnull(a, b)`;
- [`MakeNow`](xref:NextORM.Core.ISqlDialect.MakeNow(System.Boolean)) отрисовывает `datetime('now')` и для локального, и для UTC (`SQLite has no now()`);
- части даты используют `cast(strftime(...) as integer)` для `year`/`month`/`day`/`hour`/`minute`/`second`
  и `dayofyear` (`%j`), с откатом к ANSI `extract(part from value)` для всего остального;
- `Math.Log` отображается на `ln(...)` (в SQLite `log()` — это логарифм по основанию 10);
- разбиение на страницы — `limit n` / `limit n offset m`; offset без limit становится `limit -1 offset m`.

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.Sqlite;

// From a file path (the debug build checks that the file exists).
var byPath = new DataContextBuilder().UseSqlite("app.db");

// From an existing, caller-owned connection.
using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
var byConnection = new DataContextBuilder().UseSqlite(connection);

using var ctx = byPath.CreateDataContext();   // IDataContext
```

Вы также можете создать контекст напрямую (это делают тесты провайдера):

```csharp
using NextORM.Core;
using NextORM.Sqlite;

using IDataContext ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());
```

## Пользовательские агрегатные функции

В Microsoft.Data.Sqlite нет авторегистрации на основе атрибутов, поэтому основная библиотека регистрирует свои пользовательские
агрегаты явно и один раз на соединение
(`src/nextorm.sqlite/SQLiteFunctions.cs`, вызывается из [`OnConnectionCreated`](xref:NextORM.Sqlite.SqliteDataContext.OnConnectionCreated(System.Data.Common.DbConnection))):

| Функция | Значение |
|---|---|
| `stdev` | выборочное стандартное отклонение (n−1) |
| `stdevp` | генеральное стандартное отклонение (n) |
| `var` | выборочная дисперсия (n−1) |
| `varp` | генеральная дисперсия (n) |

Все четыре используют один `VarianceAccumulator`; вариант возвращает `null`, когда непустых строк слишком мало
(меньше 2 для выборочного варианта, меньше 1 для генерального). Поскольку функции
уже названы `stdev`/`var`, SQLite не выполняет переименование агрегатов.

```csharp
var stddev = ctx.From<IComplexEntity>()
    .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
    .First();
```

```sql
select stdev(cast(id as double precision)) from complex_entity
```

## Части даты, coalesce и `LIKE`

```csharp
var query = ctx.From<IComplexEntity>()
    .Select(x => new
    {
        Year = x.Datetime!.Value.Year,
        Fallback = x.String ?? "",
    });
```

```sql
select cast(strftime('%Y', dt) as integer) as 'Year', ifnull(somestring, '') as 'Fallback'
from complex_entity
```

`SqlFunctions.Sql.like(column, pattern)` и `SqlFunctions.Sql.like(column, pattern, escapeChar)` отрисовывают предикат
`like`; строковые методы (`Contains`, `StartsWith`, `EndsWith`) транслируются в `like` с
соответствующими подстановочными знаками.

## Разбиение на страницы

```csharp
ctx.From<IComplexEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<IComplexEntity>().Offset(10).Select(x => x.Id);    // limit -1 offset 10
```

```sql
select id from complex_entity limit 5 offset 10
select id from complex_entity limit -1 offset 10
```

В SQLite нет `OFFSET` без `LIMIT`, поэтому запрос только с offset выдаёт сигнальное значение `limit -1`.

## Функции, специфичные для SQLite

`SqlFunctions.Sqlite` даёт поверхность, специфичную для SQLite: функции ядра, JSON1, функции дат,
функции математического расширения и поверхность полнотекстового поиска FTS3/FTS4/FTS5.
`json_each`/`json_tree` и табличный поиск FTS5 доступны как табличные функции. Прочие провайдеры
отклоняют любой член поверхности с `NotSupportedException`.

```csharp
ctx.From<IComplexEntity>()
    .Select(x => new
    {
        Json = SqlFunctions.Sqlite.json_extract<string>(x.String, "$.name"),
        Kind = SqlFunctions.Sqlite.@typeof(x.String),
        Pi = SqlFunctions.Sql.pi()
    });
```

### Полнотекстовый поиск (FTS3/FTS4/FTS5)

Члены FTS выполняют запрос к виртуальной таблице FTS; nextorm её не создаёт и не обслуживает.
Поддержка запросов существует только когда сборка SQLite содержит соответствующий модуль
(поставляемая сборка включает `fts3`, `fts4` и `fts5`). Это отдельная поверхность от
кросс-провайдерных предикатов `SqlFunctions.Sql.contains`/`freetext`: они по-прежнему гейтятся
[`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText), который на SQLite равен
`false`, поэтому бросают `NotSupportedException`. Каждый член FTS — SQL-only: in-memory провайдер
бросает `NotSupportedException`.

| C# | SQLite | Модуль |
|---|---|---|
| `Match(tableOrColumn, query)` | `tableOrColumn MATCH query` | FTS3/4/5 |
| `MatchTable<TEntity>(table, query)` | `table(query)` как источник `FROM` | FTS5 |
| `FTS5bm25(table)` / `FTS5bm25(table, weights…)` | `bm25(table[, weights…])` | FTS5 |
| `Highlight(table, columnIndex, startMatch, endMatch)` | `highlight(...)` | FTS5 |
| `Snippet(table, columnIndex, startMatch, endMatch, ellipses, tokens)` | `snippet(...)` | FTS5 |
| `Rank(table)` | `table.rank` (скрытая колонка) | FTS5 |
| `Rank(matchInfo)` | `rank(matchInfo)` (нужна UDF `rank`) | FTS3/4 |
| `RowId(table)` | `table.rowid` (скрытая колонка) | FTS3/4 |
| `FTS3Offsets(table)` | `offsets(table)` | FTS3/4 |
| `FTS3MatchInfo(table[, format])` | `matchinfo(table[, format])` | FTS3/4 |
| `FTS3Snippet(table, …)` | `snippet(table, …)` | FTS3/4 |

Первый аргумент вспомогательных функций (и токен таблицы у `Match`) — **доверенное константное**
имя таблицы; оно подставляется как quoted-идентификатор, поэтому никогда не строите его из
пользовательского ввода. `query` FTS привязывается как параметр, когда это значение времени выполнения, и подставляется
литералом, когда оно константно.

```csharp
var query = "hello";

var rows = ctx.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", query))
    .OrderBy(x => x.RowId)
    .Select(x => new
    {
        x.RowId,
        x.Title,
        Score = SqlFunctions.Sqlite.FTS5bm25("article_fts"),
        Preview = SqlFunctions.Sqlite.Snippet("article_fts", 1, "[", "]", "...", 8)
    })
    .ToList();
```

```sql
select rowid, title, bm25("article_fts") as 'Score',
       snippet("article_fts", 1, '[', ']', '...', 8) as 'Preview'
from article_fts
where "article_fts" match $query
order by rowid
```

У FTS5 есть также табличная форма `FROM` — `MatchTable`, которая композируется как любой другой
источник (`FromTableFunction`):

```csharp
var rows = ctx
    .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<Article>("article_fts", query))
    .OrderBy(x => x.RowId)
    .Select(x => new { x.RowId, x.Title })
    .ToList();
```

```sql
select rowid, title from "article_fts"($query) order by rowid
```

У FTS3/4 нет встроенного `rank`: SQLite разрешает `rank(matchinfo(...))` через **зарегистрированную
на соединении SQL UDF** с именем `rank`, которую провайдер не регистрирует. Зарегистрируйте её на
каждом соединении, иначе запрос упадёт при выполнении с `SQLite Error 1: no such function: rank`:

```csharp
using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=app.db");
connection.CreateFunction<byte[]?, long>("rank", static matchInfo =>
{
    // Оцените blob matchinfo() (см. документацию SQLite по matchinfo); здесь просто сумма байт.
    long score = 0;
    if (matchInfo is not null)
        foreach (var b in matchInfo)
            score += b;
    return score;
});
```

```csharp
var scores = ctx.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", "hello"))
    .Select(x => new
    {
        x.RowId,
        Score = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("article_fts"))
    })
    .ToList();
```

Создание, наполнение и настройка индекса остаются вне поверхности запросов: функции
обслуживания/управления FTS5 (`AutoMerge`, `CrisisMerge`, `Merge`, `Optimize`, `Rebuild`,
`IntegrityCheck`) отложены в follow-up
[#195](https://github.com/AlexeyShirshov/nextorm/issues/195) и
[#196](https://github.com/AlexeyShirshov/nextorm/issues/196); создавайте виртуальную таблицу сырым
SQL.

Полный список, нативное написание в SQLite и требования к версии/опциям сборки — в разделе
[Специфичный для SQLite SQL](../guide/provider-specific/sqlite.md).

## Ограничения

В SQLite нет поддержки подзапросов `ANY`/`ALL`. `SqlFunctions.Sql.any(...)` / `SqlFunctions.Sql.all(...)` транслируются в
SQL, и база данных отклоняет их во время выполнения с `SqliteException` — сбой не возникает
во время трансляции.

```csharp
// Throws Microsoft.Data.Sqlite.SqliteException when executed.
await ctx.From<ISimpleEntity>()
    .Where(it => it.Id == SqlFunctions.Sql.any(ctx.From<IComplexEntity>().Select(c => c.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

[`IntersectAll`](xref:NextORM.Core.QueryCommand`1.IntersectAll``1(NextORM.Core.QueryCommand{``0})) и [`ExceptAll`](xref:NextORM.Core.QueryCommand`1.ExceptAll``1(NextORM.Core.QueryCommand{``0})) отклоняются диалектом с `NotSupportedException`, потому что в SQLite
нет `intersect all` / `except all`.

Терминалы потокового чтения LOB (`ToStream`/`ToTextReader`) поддерживаются для одной колонки `byte[]`/`string` (бинарной и текстовой). У SQLite нет собственного локатора строк, поэтому `Microsoft.Data.Sqlite` возвращает настоящий потоковый `SqliteBlob` только когда запрос выбирает ещё и `rowid`; диалект добавляет завершающий [`LobLocatorColumn`](xref:NextORM.Core.ISqlDialect.LobLocatorColumn) (`rowid`, payload остаётся на позиции `0`). Поэтому источник обязан быть обычной rowid-таблицей: на `view` или таблице `WITHOUT ROWID` команда падает fail-closed с сырым `Microsoft.Data.Sqlite.SqliteException: no such column: rowid` — буферизованного фолбэка нет. См. [Потоковое чтение больших объектов](../guide/26-large-objects.md).

Многоколоночный терминал `ToDataReader`/`ToDataReaderAsync` в SQLite **не** поддерживается: его потоковая проекция всегда несёт завершающий локатор `rowid`, поэтому терминал выставил бы колонку, которую вызывающий не проецировал. Он падает fail-closed с `NotSupportedException` до выполнения; для одной LOB-колонки используйте `ToStream`/`ToTextReader`.

Собственная поверхность полнотекстового поиска SQLite (`FTS3/FTS4/FTS5`) доступна (см. [Полнотекстовый поиск](#полнотекстовый-поиск-fts3fts4fts5)); за её пределами остаются только кросс-провайдерные предикаты `SqlFunctions.Sql.contains`/`freetext` (гейт [`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText), на SQLite `false`) и обслуживание FTS-индекса (отложено в [#195](https://github.com/AlexeyShirshov/nextorm/issues/195)/[#196](https://github.com/AlexeyShirshov/nextorm/issues/196)).

## Различия провайдеров

| Аспект | SQLite |
|---|---|
| Плейсхолдер параметра | `$name` |
| Concat | `||` |
| Coalesce | `ifnull` |
| `greatest` / `least` | `max` / `min` (скалярные, 2+ аргумента; возвращают NULL, если хотя бы один аргумент NULL) |
| Условная функция | `iif(cond, a, b)` (SQLite 3.32+) |
| Оконные функции | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` поддерживаются |
| Логический литерал | `1` / `0` |
| Квотирование идентификаторов | одинарные кавычки (`as 't1'`) |
| Псевдоним производной таблицы | не требуется |
| Псевдоним TVF | не требуется |
| `*ALL` | не поддерживается |
| Подзапросы `ANY`/`ALL` | отклоняются базой данных при выполнении |
| Полнотекстовый поиск | поверхность запросов FTS3/FTS4/FTS5 через `SqlFunctions.Sqlite` (`Match`, FTS5 `FTS5bm25`/`Highlight`/`Snippet`/`Rank`, помощники FTS3/4, табличный `MatchTable` FTS5; FTS3/4 `Rank` требует зарегистрированной на соединении UDF `rank`); кросс-провайдерные `contains`/`freetext` бросают |
| Потоковое чтение LOB (`ToStream`/`ToTextReader`) | поддерживается (`blob` / `text`; источник должен раскрывать `rowid` — на `view`/`WITHOUT ROWID` падает с `SqliteException: no such column: rowid`); `ToDataReader` бросает `NotSupportedException` (локатор `rowid`) |
| Session/info-функции | `version()` → `sqlite_version()` (нет информации о пользователе/схеме/БД) |

## См. также

- [Provider overview](overview.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.sqlite.tests/SqliteDialectTests.cs:23,29,42,50`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:120,133,142,152,161,173,204,217,892`,
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`src/nextorm.sqlite/SqliteDialect.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs`,
`src/nextorm.sqlite/SqliteDataContext.cs`, `src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs`.
