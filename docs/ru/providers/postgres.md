# Провайдер PostgreSQL

> Используйте `nextorm.postgres` для PostgreSQL; он отрисовывает параметры `@name`, разбиение на страницы `limit`/`offset`, `coalesce`, части даты `extract` и агрегаты `stddev`/`variance`, поддерживает `INTERSECT ALL`/`EXCEPT ALL` и требует псевдонимов в двойных кавычках.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

[`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext) (`src/nextorm.postgres/PostgresDataContext.cs`) оборачивает `Npgsql`. Он создаёт
`NpgsqlConnection` и передаёт значения параметров null как `DBNull` (Npgsql отклоняет значение параметра null).

[`PostgresDialect`](xref:NextORM.Postgres.PostgresDialect) (`src/nextorm.postgres/PostgresDialect.cs`) — это диалект:

- плейсхолдер параметра `@name`;
- конкатенация строк с помощью `||`;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect.MakeCoalesce(System.String,System.String)) отрисовывает `coalesce(a, b)`;
- логические литералы — `true`/`false`;
- идентификаторы используют двойные кавычки ([`Escape`](xref:NextORM.Core.ISqlDialect.Escape(System.String)) возвращает `"name"`), и [`MakeColumnReference`](xref:NextORM.Core.ISqlDialect.MakeColumnReference(System.String)) тоже квотирует имя,
  чтобы квотированный псевдоним сохранялся, когда на него ссылаются из внешнего запроса;
- производные таблицы и табличные функции должны иметь псевдонимы ([`RequireSubqueryAlias`](xref:NextORM.Core.ISqlDialect.RequireSubqueryAlias) равно `true`, и его
  следствием является то, что псевдоним выдаётся всегда);
- `INTERSECT ALL` / `EXCEPT ALL` поддерживаются ([`SupportsIntersectExceptAll`](xref:NextORM.Core.ISqlDialect.SupportsIntersectExceptAll) равно `true`);
- массивы поддерживаются ([`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays) равно `true`): параметры-массивы с квантификаторами
  `any`/`all` и функции для массивов;
- JSON/JSONB поддерживается ([`SupportsPostgresJsonSql`](xref:NextORM.Core.ISqlDialect.SupportsPostgresJsonSql) равно `true`, только PostgreSQL; нативное хранение `jsonb` —
  [`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson)): агрегаты `json_agg`/`jsonb_agg`, функции
  построения/доступа и операторы `->`/`->>`/`@>`/`?`, а параметры `JsonDocument`/`JsonElement`/`JsonNode`
  привязываются как `jsonb`;
- `greatest`/`least` и предложение `FILTER (WHERE ...)` у агрегатов включены ([`SupportsGreatestLeast`](xref:NextORM.Core.ISqlDialect.SupportsGreatestLeast) равно `true`, а
  [`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle) — `AnsiFilter`, когда настроенная версия сервера не задана или 9.4+, а на версии
  ниже 9.4 диалект отклоняет фильтрующие агрегаты через `NotSupportedException`);
- `date_trunc` включён ([`SupportsDateTrunc`](xref:NextORM.Core.ISqlDialect.SupportsDateTrunc) равно `true`);
- арифметика дат включена ([`SupportsDateArithmetic`](xref:NextORM.Core.ISqlDialect.SupportsDateArithmetic) равно `true`): `SqlFunctions.Sql.date_add`/`end_of_month`
  и методы `DateTime.Add*` отрисовывают интервальную арифметику PostgreSQL
  (`x + (n * interval '1 day')`, `date_trunc('month', x) + interval '1 month - 1 day'`);
- агрегаты `string_agg`/`array_agg` включены ([`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates) равно `true`);
- полнотекстовый поиск включён ([`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText) равно `true`): `SqlFunctions.Sql.contains` отрисовывает
  `to_tsvector(col) @@ plainto_tsquery(search)`, а `freetext` — `websearch_to_tsquery(search)`;
  ранжирование доступно через native `SqlFunctions.Postgres.ts_rank`/`ts_rank_cd`/`ts_headline` (гейтится
  [`SupportsTextSearchFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTextSearchFunctions));
- расширенная библиотека скалярных функций включена ([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions) равно `true`):
  дополнительные математические (`asin`, `cbrt`, `mod`, ...), строковые (`split_part`,
  `lpad`, `initcap`, ...), POSIX-регулярные выражения (`regexp_replace`, `regexp_like`, ...), дата/время
  (`make_interval`, `justify_days`, `justify_hours`, `to_char`, `to_date`, ...), `num_nulls`/`num_nonnulls`
  и помощник типа `pg_typeof`;
- session/info-функции включены ([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions) равно `true`):
  `SqlFunctions.Sql.current_user`/`session_user`/`current_schema` отрисовываются ключевыми словами, а
  `current_database`/`version` — как `current_database()`/`version()`;
- генераторы UUID включены ([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators) равно `true`):
  `SqlFunctions.Sql.gen_random_uuid()` отрисовывается как `gen_random_uuid()` (PostgreSQL 13+), а
  `uuidv7()` — как `uuidv7()` (PostgreSQL 18+);
- логические, битовые, статистические и упорядоченные агрегаты включены ([`SupportsBooleanAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBooleanAggregates),
  [`SupportsBitAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBitAggregates), [`SupportsStatisticalAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStatisticalAggregates) и [`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates) равны `true`):
  `bool_and`/`bool_or`/`every`, `bit_and`/`bit_or`/`bit_xor`, `corr`/`covar_*`/`regr_*` и
  `percentile_cont`/`percentile_disc`/`mode` с `WITHIN GROUP`;
- имена агрегатов переотображаются: `stdev`→`stddev`, `stdevp`→`stddev_pop`, `var`→`variance`,
  `varp`→`var_pop`;
- `Math.Log` отображается на `ln(...)` (в PostgreSQL `log()` — это логарифм по основанию 10);
- `Math.Round(x, digits)` приводит первый аргумент `double`/`float` к `numeric`
  (`round((x)::numeric, digits)`): в PostgreSQL нет `round(double precision, integer)`;
- `SqlFunctions.Postgres.setseed(seed)` рендерит `setseed(seed)` (гейт
  [`SupportsRandomSeed`](xref:NextORM.Core.ISqlDialect.SupportsRandomSeed)); `setseed` в PostgreSQL
  возвращает `void`, поэтому проецируемое значение всегда `null`;
- `DateTime.Now` отрисовывает `now()`, `DateTime.UtcNow` отрисовывает `now() at time zone 'utc'`;
- части даты отрисовываются как `extract(part from value)`; `SqlFunctions.Sql.extract(part, value)`
  покрывает `quarter`/`week` (ISO)/`dow`/`isodow`, а `SqlFunctions.Sql.date_part("epoch", value)`
  рендерит `cast(extract(epoch from value) as double precision)`;
- разбиение на страницы — `limit n` / `limit n offset m`; запрос только с offset выдаёт только `offset m`.

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.Postgres;

var byString = new DataContextBuilder().UsePostgres("Host=localhost;Database=app;Username=app;Password=secret");

using var connection = new Npgsql.NpgsqlConnection("Host=localhost;Database=app;...");
var byConnection = new DataContextBuilder().UsePostgres(connection);

using var ctx = byString.CreateDataContext();   // IDataContext
```

Напрямую:

```csharp
using NextORM.Core;
using NextORM.Postgres;

using IDataContext ctx = new PostgresDataContext("Host=localhost;Database=app;...", new DataContextBuilder());
```

## Версия сервера

Диалект можно привязать к версии сервера, которая гейтит зависящий от версии синтаксис. Настройка
**явная** — nextorm никогда не опрашивает живой сервер. Передайте версию через параметр `Version`
конструктора контекста; если его не задать, сохраняется историческое поведение и предполагается
PostgreSQL 9.4 или новее, поэтому предложение ANSI `FILTER (WHERE ...)` у агрегатов остаётся
включённым. Версия ниже 9.4 его отключает: фильтрующий агрегат бросает `NotSupportedException`.

```csharp
// Версия не задана: фильтрующие агрегаты включены (предполагается 9.4+).
using var current = new PostgresDataContext("Host=localhost;Database=app;...", new DataContextBuilder());

// Привязка к 9.3: предложение FILTER (WHERE ...) у агрегатов отклоняется.
using var legacy = new PostgresDataContext("Host=localhost;Database=app;...", new DataContextBuilder(), new Version(9, 3));
```

Версия неизменяема и фиксируется на **конкретный тип контекста** на всё время жизни процесса. Два
контекста одного конкретного типа, запрашивающие разные версии, бросают `InvalidOperationException`;
чтобы работать с несколькими версиями в одном процессе, объявите для каждой отдельный подкласс
контекста:

```csharp
public sealed class Postgres93DataContext : PostgresDataContext
{
    public Postgres93DataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder, new Version(9, 3)) { }
}
```

## Разбиение на страницы

```csharp
ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<ISimpleEntity>().Offset(10).Select(x => x.Id);    // offset 10
```

```sql
select id from simple_entity limit 5 offset 10
select id from simple_entity offset 10
```

`OFFSET` может появляться сам по себе, но `LIMIT` должен идти первым, когда присутствуют оба. Поскольку PostgreSQL
не требует внедрённой сортировки, `ORDER BY` не добавляется.

## Coalesce, части даты и агрегаты

```csharp
var query = ctx.From<IComplexEntity>()
    .Select(x => new
    {
        Fallback = x.String ?? "",
        Year = x.Datetime!.Value.Year,
    });
```

```sql
select coalesce(somestring, '') as "Fallback", extract(year from dt) as "Year"
from complex_entity
```

```csharp
var stdev = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.stdev((double)x.Id));  // stddev(...)
var varp  = ctx.From<IComplexEntity>().Select(x => SqlFunctions.Sql.varp((double)x.Id));   // var_pop(...)
```

`count` и `count_big` оба отрисовывают `count(*)`, потому что `count` в PostgreSQL уже возвращает 64-битное
целое.

## Массивы

PostgreSQL — единственный поддерживаемый провайдер с нативными массивами, и array-поверхность
находится в [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) ([`Sql`](xref:NextORM.Core.SqlFunctions.Sql) остаётся кросс-провайдерным). Массив передаётся одним
параметром, поэтому `column = any(@array)` работает и с runtime-параметром, и с захваченным массивом,
а SQL не зависит от количества элементов:

```csharp
var ids = new long[] { 1, 2, 3 };

ctx.From<IComplexEntity>().Where(e => SqlFunctions.Postgres.any(e.Id, ids));      // (id = any(@p0))
ctx.From<IComplexEntity>().Where(e => e.Id == SqlFunctions.Postgres.any(ids));    // id = any(@p0)
ctx.From<IComplexEntity>().Where(e => e.Id == SqlFunctions.Postgres.any(SqlFunctions.Parameter<long[]>(0))); // id = any(@norm_p0)
```

```sql
select id from complex_entity where (id = any(@p0))
```

Функции для массивов (`cardinality`, `array_length`, `array_position`, ...) и операторы `@>`/`&&`
описаны в разделе [Скалярные функции](../scalar-functions/06-arrays.md#массивы-postgresql). Остальные
провайдеры отклоняют их с `NotSupportedException`.

## Range-типы

PostgreSQL — единственный поддерживаемый провайдер с нативными range-типами (`int4range`, `int8range`,
`numrange`, `tsrange`, `tstzrange`, `daterange`). Для колонки или параметра используйте
provider-agnostic [`Range<T>`](xref:NextORM.Core.Range`1); провайдер биндит его через Npgsql и читает
обратно, сохраняя unbounded стороны и пустой диапазон. PostgreSQL-only range-поверхность
(`overlaps`, `range_contains`/`range_contained_by`, `range_union`, `lower`/`upper`/`isempty`,
конструкторы и `empty_range<T>()`) находится в
[`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) и гейтится
[`SupportsRanges`](xref:NextORM.Core.ISqlDialect.SupportsRanges):

```csharp
var window = new Range<int>(15, 25);   // [15,25)

ctx.From<IReservation>()
    .Where(e => SqlFunctions.Postgres.overlaps(e.During, window))
    .Select(e => e.Id)
    .ToList();                          // (during && @p0)
```

Multirange-типы (`int4multirange`…`datemultirange`) мапятся на `Range<T>[]` и используют те же имена
операторов, перегруженные для multirange, плюс `multirange`, `range_merge` и агрегаты
`range_agg`/`range_intersect_agg`. Полная поверхность —
[Специфичный для PostgreSQL SQL](../guide/provider-specific/postgresql.md#multirange).

In-memory провайдер вычисляет всю поверхность range/multirange с той же семантикой; все остальные
провайдеры отклоняют её с `NotSupportedException`. Полная таблица — в разделе
[Специфичный для PostgreSQL SQL](../guide/provider-specific/postgresql.md#range-типы).

## JSON и JSONB

PostgreSQL — единственный поддерживаемый провайдер с `json`/`jsonb`. Свойство `[JsonColumn]` отображает
CLR-объект на нативную колонку `jsonb` на чтение и запись (`Auto` storage; `Native` принудительно
включает `jsonb`, `Text` — текст), поэтому объект проходит цикл insert, update и `RETURNING`. Параметр
`JsonDocument`, `JsonElement` или `JsonNode` привязывается как `jsonb`, поэтому операторы доступа и
функции работают напрямую:

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

using var ctx = new PostgresDataContext(connectionString, new DataContextBuilder());
ctx.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), "name") == "Alice")
    .Select(e => e.Id)
    .ToList(document);

ctx.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(e.String));   // jsonb_agg(somestring)
```

Обычная строка с JSON привязывается как `text`; для разбора используйте `SqlFunctions.Postgres.json_cast(value)`.
«Голое» свойство `JsonNode`/`JsonNode?` (объявленное ровно как `JsonNode`, не `JsonObject`/`JsonArray`)
читает нативную колонку `json`/`jsonb`: текст читается и разбирается для корней-объектов, массивов и
скаляров, как в скалярной (`Select(x => x.Data)`), так и в составной проекции. SQL `NULL` и JSON-литерал
`null` оба материализуются в CLR `null`. Объявленные свойства `JsonObject`/`JsonArray` и «голая»
*скалярная* проекция `JsonDocument`/`JsonElement` в эту поддержку не входят (отображайте их через
`[JsonColumn]` или проецируйте `JsonDocument`/`JsonElement` в именованной форме). Отображение
`[JsonColumn]` и привязка JSON-параметров не меняются, DDL колонок nextorm не генерирует.
Полная поверхность (`json_agg`, `jsonb_build_object`, `->`, `->>`, `#>`, `@>`, `?`, `?|`, `?&`, ...)
описана в разделе [Скалярные функции](../scalar-functions/07-json-and-xml.md#json-и-jsonb-postgresql).
Остальные провайдеры отклоняют её с `NotSupportedException`.

## Дополнительная поверхность функций

PostgreSQL также включает `greatest`/`least`, `date_trunc`, агрегаты `string_agg`/`array_agg`, предложение
`FILTER (WHERE ...)` у агрегатов, переносимый `iif` (рендерится как `case when ... then ... else ... end`),
оконные функции `percent_rank`/`cume_dist`/`nth_value` и встроенные наборные табличные функции
`generate_series`, `unnest`, `regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements(_text)`,
`jsonb_each(_text)`, `jsonb_object_keys`, `jsonb_path_query`, `ts_stat` и record-функции
`jsonb_to_record`/`jsonb_to_recordset`
([`SqlFunctions.Postgres.jsonb_to_record<TRow>(json)`](xref:NextORM.Core.PostgresFunctions), схема
результата которых рендерится как список определений колонок в псевдониме из `TRow` вызывающего под
[`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema))):

```csharp
ctx.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Names = SqlFunctions.Sql.string_agg(e.String, ","),
        Big = SqlFunctions.Sql.count(() => e.Id > 10L)
    });   // string_agg(somestring, ',') ... count(*) filter (where (id > 10))
```

Они описаны в разделе
[Скалярные функции](../scalar-functions/05-aggregates.md#строковые-и-массивные-агрегаты). SQLite
также принимает предложение `FILTER`; остальные функции доступны только в PostgreSQL.

## Операции над множествами `*ALL` и порядок null

PostgreSQL — единственный поддерживаемый реляционный провайдер, реализующий `INTERSECT ALL` и `EXCEPT ALL`,
поэтому [`IntersectAll`](xref:NextORM.Core.QueryCommand`1.IntersectAll``1(NextORM.Core.QueryCommand{``0}))/[`ExceptAll`](xref:NextORM.Core.QueryCommand`1.ExceptAll``1(NextORM.Core.QueryCommand{``0})) отрисовывают свой SQL напрямую.

```csharp
var q = a.Select(x => x.Id).IntersectAll(b.Select(x => x.Id));   // ... intersect all ...
```

PostgreSQL рассматривает `NULL` как наибольшее значение, поэтому `ORDER BY … DESC` ставит группу `NULL` первой. Общий
набор тестов избегает зависимости от этого; тест провайдера фиксирует это явно.

```csharp
var r = ctx.From<IComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToList();
// r[0].Id == 1 (the row whose Int is NULL)
```

## Большие объекты (потоковое чтение LOB)

Одну колонку `byte[]` или `string` можно прочитать как `Stream`/`TextReader`, не загружая значение целиком в managed-память. Npgsql реализует `GetStream`/`GetTextReader` для `bytea` и `text`, поэтому PostgreSQL — один из трёх провайдеров, поддерживающих LOB-терминалы в этом выпуске (наряду с SQL Server и SQLite):

```csharp
await using var stream = ctx.From<BinaryEntity>()
    .Where(x => x.Id == 1)
    .Select(x => x.Payload)
    .ToStream();          // открывает reader с CommandBehavior.SequentialAccess

using var reader = ctx.From<Document>()
    .Where(x => x.Id == 1)
    .Select(x => x.Body)
    .ToTextReader();
```

Возвращённый поток владеет reader'ом и per-call командой до освобождения и не закрывает контекст; проекция обязана быть ровно одной колонкой `byte[]`/`string` (иначе `InvalidOperationException`). MySQL/MariaDB, ClickHouse и провайдер in-memory отклоняют терминалы через `NotSupportedException`. См. [Потоковое чтение больших объектов](../guide/26-large-objects.md).

PostgreSQL также поддерживает многоколоночный терминал `ToDataReader`/`ToDataReaderAsync`: он отдаёт ту же sequential-access команду как принадлежащий вызывающему `DbDataReader`, поэтому вызывающий может прочитать все колонки и строки (или несколько LOB-колонок по порядку), не материализуя результат. SQLite тоже его поддерживает, но через буферизованный reader без локатора (без `rowid`, без `SequentialAccess`, то есть без чанкового LOB); MySQL/MariaDB, ClickHouse и провайдер in-memory не имеют поддержки sequential access и отклоняют его.

## Псевдонимы

Производные таблицы и табличные функции должны иметь псевдонимы в двойных кавычках:

```sql
select t1.value, t2.somestring as "String"
from all_rows() as "t1"
join complex_entity as "t2" on t1.id = t2.id
```

## Различия провайдеров

| Аспект | PostgreSQL |
|---|---|
| Плейсхолдер параметра | `@name` |
| Разбиение на страницы | `limit n` / `limit n offset m` / `offset m` |
| Внедряемая сортировка при разбиении на страницы | нет |
| Concat | `||` |
| Coalesce | `coalesce` |
| Логический литерал | `true` / `false` |
| Квотирование идентификаторов | двойные кавычки (`as "t1"`) |
| Псевдоним производной таблицы / TVF | требуется |
| `*ALL` | поддерживается |
| Массивы | поддерживаются (`any(@array)`, `cardinality`, ...) |
| JSON/JSONB | поддерживается (`json_agg`, `->`, ...; нативные колонки `json`/`jsonb` через `[JsonColumn]`; параметры `JsonDocument`/`JsonElement`/`JsonNode` привязываются как `jsonb`) |
| Потоковое чтение LOB (`ToStream`/`ToTextReader`, `ToDataReader`) | поддерживается (`SequentialAccess`; одна колонка `byte[]`/`string` или многоколоночный reader, принадлежащий вызывающему) |
| `greatest` / `least` / `date_trunc` | поддерживаются (`greatest`/`least` игнорируют NULL-аргументы) |
| Условная функция | `iif(cond, a, b)` → `case when cond then a else b end` |
| Оконные функции | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` поддерживаются |
| `date_add` / `end_of_month` / `DateTime.Add*` | интервальная арифметика (`x + (n * interval '1 day')`) |
| `string_agg` / `array_agg` / `filter` у агрегатов | поддерживаются (`filter` у агрегатов требует 9.4+; незаданная версия — ≥9.4) |
| Версия сервера | настраивается явно через `Version` у контекста |
| Session/info-функции | `current_user`, `session_user`, `current_schema`, `current_database()`, `version()` |
| Табличные функции | `generate_series`, `unnest`, `regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`, `jsonb_object_keys`, `jsonb_path_query`, `ts_stat`, `jsonb_to_record`/`jsonb_to_recordset` (схема из `TRow`) |
| Рекурсивный CTE | `with recursive` (без опции max-recursion) |
| `stdev` / `stdevp` | `stddev` / `stddev_pop` |
| `var` / `varp` | `variance` / `var_pop` |
| `DateTime.Now` / `UtcNow` | `now()` / `now() at time zone 'utc'` |
| Размещение null при `ORDER BY … DESC` | null сортируются первыми |

## См. также

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.postgres.tests/PostgresDialectTests.cs:21,27,41,49`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:117,130,139,151,160,172,199,211,223,248,788,976`,
`tests/nextorm.integration.tests/PostgresSpecificTests.cs:15,24`,
`src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.postgres/PostgresDataContext.cs`,
`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs`.
