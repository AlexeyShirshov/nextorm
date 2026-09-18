# Провайдер PostgreSQL

> Используйте `nextorm.postgres` для PostgreSQL; он отрисовывает параметры `@name`, разбиение на страницы `limit`/`offset`, `coalesce`, части даты `extract` и агрегаты `stddev`/`variance`, поддерживает `INTERSECT ALL`/`EXCEPT ALL` и требует псевдонимов в двойных кавычках.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

`PostgresDbContext` (`src/nextorm.postgres/PostgresDbContext.cs`) оборачивает `Npgsql`. Он создаёт
`NpgsqlConnection` и передаёт значения параметров null как `DBNull` (Npgsql отклоняет значение параметра null).

`PostgresDialect` (`src/nextorm.postgres/PostgresDialect.cs`) — это диалект:

- плейсхолдер параметра `@name`;
- конкатенация строк с помощью `||`;
- `MakeCoalesce` отрисовывает `coalesce(a, b)`;
- логические литералы — `true`/`false`;
- идентификаторы используют двойные кавычки (`Escape` возвращает `"name"`), и `MakeColumnReference` тоже квотирует имя,
  чтобы квотированный псевдоним сохранялся, когда на него ссылаются из внешнего запроса;
- производные таблицы и табличные функции должны иметь псевдонимы (`RequireSubqueryAlias` равно `true`, и его
  следствием является то, что псевдоним выдаётся всегда);
- `INTERSECT ALL` / `EXCEPT ALL` поддерживаются (`SupportsIntersectExceptAll` равно `true`);
- массивы поддерживаются (`SupportsArrays` равно `true`): параметры-массивы с квантификаторами
  `any`/`all` и функции для массивов;
- JSON/JSONB поддерживается (`SupportsJson` равно `true`): агрегаты `json_agg`/`jsonb_agg`, функции
  построения/доступа и операторы `->`/`->>`/`@>`/`?`, а параметры `JsonDocument`/`JsonElement`/`JsonNode`
  привязываются как `jsonb`;
- `greatest`/`least` и предложение `FILTER (WHERE ...)` у агрегатов включены (`SupportsGreatestLeast` и
  `SupportsFilter` равны `true`);
- `date_trunc` включён (`SupportsDateTrunc` равно `true`);
- арифметика дат включена (`SupportsDateArithmetic` равно `true`): `NORM.SQL.date_add`/`end_of_month`
  и методы `DateTime.Add*` отрисовывают интервальную арифметику PostgreSQL
  (`x + (n * interval '1 day')`, `date_trunc('month', x) + interval '1 month - 1 day'`);
- агрегаты `string_agg`/`array_agg` включены (`SupportsStringArrayAggregates` равно `true`);
- полнотекстовый поиск включён (`SupportsFullText` равно `true`): `NORM.SQL.contains` отрисовывает
  `to_tsvector(col) @@ plainto_tsquery(search)`, а `freetext` — `websearch_to_tsquery(search)`;
- расширенная библиотека скалярных функций включена (`SupportsExtendedScalarFunctions` равно `true`):
  дополнительные математические (`asin`, `cbrt`, `degrees`, `pi`, `mod`, ...), строковые (`split_part`,
  `lpad`, `initcap`, ...), POSIX-регулярные выражения (`regexp_replace`, `regexp_like`, ...), дата/время
  (`make_interval`, `justify_days`, `justify_hours`, `to_char`, `to_date`, ...) и
  `num_nulls`/`num_nonnulls`;
- логические, битовые, статистические и упорядоченные агрегаты включены (`SupportsBooleanAggregates`,
  `SupportsBitAggregates`, `SupportsStatisticalAggregates` и `SupportsOrderedAggregates` равны `true`):
  `bool_and`/`bool_or`/`every`, `bit_and`/`bit_or`/`bit_xor`, `corr`/`covar_*`/`regr_*` и
  `percentile_cont`/`percentile_disc`/`mode` с `WITHIN GROUP`;
- имена агрегатов переотображаются: `stdev`→`stddev`, `stdevp`→`stddev_pop`, `var`→`variance`,
  `varp`→`var_pop`;
- `Math.Log` отображается на `ln(...)` (в PostgreSQL `log()` — это логарифм по основанию 10);
- `DateTime.Now` отрисовывает `now()`, `DateTime.UtcNow` отрисовывает `now() at time zone 'utc'`;
- части даты отрисовываются как `extract(part from value)`;
- разбиение на страницы — `limit n` / `limit n offset m`; запрос только с offset выдаёт только `offset m`.

## Регистрация провайдера

На `DbContextBuilder` доступны две перегрузки
(`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.postgres;

var byString = new DbContextBuilder().UsePostgres("Host=localhost;Database=app;Username=app;Password=secret");

using var connection = new Npgsql.NpgsqlConnection("Host=localhost;Database=app;...");
var byConnection = new DbContextBuilder().UsePostgres(connection);

using var ctx = byString.CreateDbContext();   // IDataContext
```

Напрямую:

```csharp
using nextorm.core;
using nextorm.postgres;

using IDataContext ctx = new PostgresDbContext("Host=localhost;Database=app;...", new DbContextBuilder());
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
var stdev = ctx.From<IComplexEntity>().Select(x => NORM.SQL.stdev((double)x.Id));  // stddev(...)
var varp  = ctx.From<IComplexEntity>().Select(x => NORM.SQL.varp((double)x.Id));   // var_pop(...)
```

`count` и `count_big` оба отрисовывают `count(*)`, потому что `count` в PostgreSQL уже возвращает 64-битное
целое.

## Массивы

PostgreSQL — единственный поддерживаемый провайдер с нативными массивами, и array-поверхность
находится в `NORM.PG_SQL` (`NORM.SQL` остаётся кросс-провайдерным). Массив передаётся одним
параметром, поэтому `column = any(@array)` работает и с runtime-параметром, и с захваченным массивом,
а SQL не зависит от количества элементов:

```csharp
var ids = new long[] { 1, 2, 3 };

ctx.From<IComplexEntity>().Where(e => NORM.PG_SQL.any(e.Id, ids));      // (id = any(@p0))
ctx.From<IComplexEntity>().Where(e => e.Id == NORM.PG_SQL.any(ids));    // id = any(@p0)
ctx.From<IComplexEntity>().Where(e => e.Id == NORM.PG_SQL.any(NORM.Param<long[]>(0))); // id = any(@norm_p0)
```

```sql
select id from complex_entity where (id = any(@p0))
```

Функции для массивов (`cardinality`, `array_length`, `array_position`, ...) и операторы `@>`/`&&`
описаны в разделе [Скалярные функции](../guide/11-scalar-functions.md#массивы-postgresql). Остальные
провайдеры отклоняют их с `NotSupportedException`.

## JSON и JSONB

PostgreSQL — единственный поддерживаемый провайдер с `json`/`jsonb`. Параметр `JsonDocument`,
`JsonElement` или `JsonNode` привязывается как `jsonb`, поэтому операторы доступа и функции работают
напрямую:

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

using var ctx = new PostgresDbContext(connectionString, new DbContextBuilder());
ctx.From<IComplexEntity>()
    .Where(e => NORM.PG_SQL.json_get_text(NORM.Param<JsonDocument>(0), "name") == "Alice")
    .Select(e => e.Id)
    .ToList(document);

ctx.From<IComplexEntity>()
    .Select(e => NORM.PG_SQL.jsonb_agg(e.String));   // jsonb_agg(somestring)
```

Обычная строка с JSON привязывается как `text`; для разбора используйте `NORM.PG_SQL.json_cast(value)`.
Полная поверхность (`json_agg`, `jsonb_build_object`, `->`, `->>`, `#>`, `@>`, `?`, `?|`, `?&`, ...)
описана в разделе [Скалярные функции](../guide/11-scalar-functions.md#json-и-jsonb-postgresql).
Остальные провайдеры отклоняют её с `NotSupportedException`.

## Дополнительная поверхность функций

PostgreSQL также включает `greatest`/`least`, `date_trunc`, агрегаты `string_agg`/`array_agg`, предложение
`FILTER (WHERE ...)` у агрегатов и встроенные табличные функции `generate_series`/`unnest`:

```csharp
ctx.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Names = NORM.SQL.string_agg(e.String, ","),
        Big = NORM.SQL.count(() => e.Id > 10L)
    });   // string_agg(somestring, ',') ... count(*) filter (where (id > 10))
```

Они описаны в разделе
[Скалярные функции](../guide/11-scalar-functions.md#строковые-и-массивные-агрегаты-postgresql). SQLite
также принимает предложение `FILTER`; остальные функции доступны только в PostgreSQL.

## Операции над множествами `*ALL` и порядок null

PostgreSQL — единственный поддерживаемый реляционный провайдер, реализующий `INTERSECT ALL` и `EXCEPT ALL`,
поэтому `IntersectAll`/`ExceptAll` отрисовывают свой SQL напрямую.

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
| JSON/JSONB | поддерживается (`json_agg`, `->`, ...; параметры `JsonDocument` привязываются как `jsonb`) |
| `greatest` / `least` / `date_trunc` | поддерживаются |
| `date_add` / `end_of_month` / `DateTime.Add*` | интервальная арифметика (`x + (n * interval '1 day')`) |
| `string_agg` / `array_agg` / `filter` у агрегатов | поддерживаются |
| Табличные функции | `generate_series(...)`, `unnest(...)` |
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

Source: `test/nextorm.postgres.tests/PostgresDialectTests.cs:21,27,41,49`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:117,130,139,151,160,172,199,211,223,248,788,976`,
`test/nextorm.integration.tests/PostgresSpecificTests.cs:15,24`,
`src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.postgres/PostgresDbContext.cs`,
`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs`.
