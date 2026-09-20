# PostgreSQL provider

> Use `nextorm.postgres` for PostgreSQL; it renders `@name` parameters, `limit`/`offset` paging, `coalesce`, `extract` date parts and `stddev`/`variance` aggregates, supports `INTERSECT ALL`/`EXCEPT ALL`, and requires double-quoted aliases.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

[`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext) (`src/nextorm.postgres/PostgresDataContext.cs`) wraps `Npgsql`. It creates an
`NpgsqlConnection` and passes null parameter values as `DBNull` (Npgsql rejects a null parameter value).

[`PostgresDialect`](xref:NextORM.Postgres.PostgresDialect) (`src/nextorm.postgres/PostgresDialect.cs`) is the dialect:

- parameter placeholder `@name`;
- string concatenation with `||`;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect) renders `coalesce(a, b)`;
- boolean literals are `true`/`false`;
- identifiers use double quotes ([`Escape`](xref:NextORM.Core.ISqlDialect) returns `"name"`), and [`MakeColumnReference`](xref:NextORM.Core.ISqlDialect) quotes a name too
  so that a quoted alias survives when it is referenced from an outer query;
- derived tables and table-valued functions must be aliased ([`RequireSubqueryAlias`](xref:NextORM.Core.ISqlDialect.RequireSubqueryAlias) is `true`, and its
  consequence is that an alias is always emitted);
- `INTERSECT ALL` / `EXCEPT ALL` are supported ([`SupportsIntersectExceptAll`](xref:NextORM.Core.ISqlDialect.SupportsIntersectExceptAll) is `true`);
- arrays are supported ([`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays) is `true`): array parameters with the `any`/`all` quantifiers
  and the array functions;
- JSON/JSONB is supported ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson) is `true`): the `json_agg`/`jsonb_agg` aggregates, the
  construction/access functions and the `->`/`->>`/`@>`/`?` operators, with `JsonDocument`/`JsonElement`/
  `JsonNode` parameters bound as `jsonb`;
- `greatest`/`least` and the aggregate `FILTER (WHERE ...)` clause are enabled ([`SupportsGreatestLeast`](xref:NextORM.Core.ISqlDialect.SupportsGreatestLeast) and
  [`SupportsFilter`](xref:NextORM.Core.ISqlDialect.SupportsFilter) are `true`);
- `date_trunc` is enabled ([`SupportsDateTrunc`](xref:NextORM.Core.ISqlDialect.SupportsDateTrunc) is `true`);
- date arithmetic is enabled ([`SupportsDateArithmetic`](xref:NextORM.Core.ISqlDialect.SupportsDateArithmetic) is `true`): `SqlFunctions.Sql.date_add`/`end_of_month`
  and the `DateTime.Add*` methods render PostgreSQL interval arithmetic
  (`x + (n * interval '1 day')`, `date_trunc('month', x) + interval '1 month - 1 day'`);
- the `string_agg`/`array_agg` aggregates are enabled ([`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates) is `true`);
- full-text search is enabled ([`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText) is `true`): `SqlFunctions.Sql.contains` renders
  `to_tsvector(col) @@ plainto_tsquery(search)` and `freetext` `websearch_to_tsquery(search)`;
- the extended scalar function library is enabled ([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions) is `true`):
  additional math (`asin`, `cbrt`, `degrees`, `pi`, `mod`, ...), string (`split_part`, `lpad`,
  `initcap`, ...), POSIX regular expression (`regexp_replace`, `regexp_like`, ...), date/time
  (`make_interval`, `justify_days`, `justify_hours`, `to_char`, `to_date`, ...), `num_nulls`/`num_nonnulls`
  and the type helper `pg_typeof`;
- the session/information functions are enabled ([`SupportsSessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SupportsSessionInfoFunctions) is `true`):
  `SqlFunctions.Sql.current_user`/`session_user`/`current_schema` render the key words and
  `current_database`/`version` render `current_database()`/`version()`;
- the UUID generators are enabled ([`SupportsUuidGenerators`](xref:NextORM.Core.ISqlDialect.SupportsUuidGenerators) is `true`):
  `SqlFunctions.Sql.gen_random_uuid()` renders `gen_random_uuid()` (PostgreSQL 13+) and `uuidv7()`
  renders `uuidv7()` (PostgreSQL 18+);
- the boolean, bitwise, statistical and ordered-set aggregates are enabled
  ([`SupportsBooleanAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBooleanAggregates), [`SupportsBitAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBitAggregates), [`SupportsStatisticalAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStatisticalAggregates) and
  [`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates) are `true`): `bool_and`/`bool_or`/`every`, `bit_and`/`bit_or`/`bit_xor`,
  `corr`/`covar_*`/`regr_*` and `percentile_cont`/`percentile_disc`/`mode` with `WITHIN GROUP`;
- aggregate names are remapped: `stdev`→`stddev`, `stdevp`→`stddev_pop`, `var`→`variance`,
  `varp`→`var_pop`;
- `Math.Log` maps to `ln(...)` (PostgreSQL's `log()` is base 10);
- `DateTime.Now` renders `now()`, `DateTime.UtcNow` renders `now() at time zone 'utc'`;
- date parts render as `extract(part from value)`;
- paging is `limit n` / `limit n offset m`; an offset-only query emits `offset m` alone.

## Registering the provider

Two overloads are available on [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)
(`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.Postgres;

var byString = new DataContextBuilder().UsePostgres("Host=localhost;Database=app;Username=app;Password=secret");

using var connection = new Npgsql.NpgsqlConnection("Host=localhost;Database=app;...");
var byConnection = new DataContextBuilder().UsePostgres(connection);

using var ctx = byString.CreateDataContext();   // IDataContext
```

Directly:

```csharp
using NextORM.Core;
using NextORM.Postgres;

using IDataContext ctx = new PostgresDataContext("Host=localhost;Database=app;...", new DataContextBuilder());
```

## Paging

```csharp
ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<ISimpleEntity>().Offset(10).Select(x => x.Id);    // offset 10
```

```sql
select id from simple_entity limit 5 offset 10
select id from simple_entity offset 10
```

`OFFSET` may appear on its own, but `LIMIT` must come first when both are present. Because PostgreSQL
does not require an injected sort, no `ORDER BY` is added.

## Coalesce, date parts and aggregates

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

`count` and `count_big` both render `count(*)`, because PostgreSQL's `count` already returns a 64-bit
integer.

## Arrays

PostgreSQL is the only supported provider with native arrays, and the array surface lives on
[`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) ([`Sql`](xref:NextORM.Core.SqlFunctions.Sql) stays cross-provider). An array operand is passed as a single parameter, so
`column = any(@array)` works with a runtime parameter or a captured array, and the SQL does not depend
on the number of elements:

```csharp
var ids = new long[] { 1, 2, 3 };

ctx.From<IComplexEntity>().Where(e => SqlFunctions.Postgres.any(e.Id, ids));      // (id = any(@p0))
ctx.From<IComplexEntity>().Where(e => e.Id == SqlFunctions.Postgres.any(ids));    // id = any(@p0)
ctx.From<IComplexEntity>().Where(e => e.Id == SqlFunctions.Postgres.any(SqlFunctions.Parameter<long[]>(0))); // id = any(@norm_p0)
```

```sql
select id from complex_entity where (id = any(@p0))
```

The array functions (`cardinality`, `array_length`, `array_position`, ...) and the `@>`/`&&` operators
are documented in [Scalar functions](../guide/11-scalar-functions.md#arrays-postgresql). Other
providers reject them with `NotSupportedException`.

## JSON and JSONB

PostgreSQL is the only supported provider with `json`/`jsonb`. Passing a `JsonDocument`, `JsonElement`
or `JsonNode` parameter binds it as `jsonb`, so the access operators and functions work directly:

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

A plain JSON string is bound as `text`; use `SqlFunctions.Postgres.json_cast(value)` to parse it as `jsonb`. The full
surface (`json_agg`, `jsonb_build_object`, `->`, `->>`, `#>`, `@>`, `?`, `?|`, `?&`, ...) is documented
in [Scalar functions](../guide/11-scalar-functions.md#json-and-jsonb-postgresql). Other providers
reject it with `NotSupportedException`.

## Additional function surface

PostgreSQL also opts into `greatest`/`least`, `date_trunc`, the `string_agg`/`array_agg` aggregates, the
aggregate `FILTER (WHERE ...)` clause, the portable `iif` (rendered `case when ... then ... else ... end`),
the `percent_rank`/`cume_dist`/`nth_value` window functions and the built-in `generate_series`/`unnest`
table functions:

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

These are documented in
[Scalar functions](../guide/11-scalar-functions.md#string-and-array-aggregates-postgresql). SQLite also
accepts the `FILTER` clause; the other functions are PostgreSQL-only.

## `*ALL` set operations and null ordering

PostgreSQL is the only supported relational provider that implements `INTERSECT ALL` and `EXCEPT ALL`,
so [`IntersectAll`](xref:NextORM.Core.QueryCommand`1)/[`ExceptAll`](xref:NextORM.Core.QueryCommand`1) render their SQL directly.

```csharp
var q = a.Select(x => x.Id).IntersectAll(b.Select(x => x.Id));   // ... intersect all ...
```

PostgreSQL treats `NULL` as the largest value, so `ORDER BY … DESC` puts the `NULL` group first. The
shared test suite avoids depending on this; the provider test pins it explicitly.

```csharp
var r = ctx.From<IComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToList();
// r[0].Id == 1 (the row whose Int is NULL)
```

## Aliases

Derived tables and table-valued functions must be aliased with double quotes:

```sql
select t1.value, t2.somestring as "String"
from all_rows() as "t1"
join complex_entity as "t2" on t1.id = t2.id
```

## Provider differences

| Aspect | PostgreSQL |
|---|---|
| Parameter placeholder | `@name` |
| Paging | `limit n` / `limit n offset m` / `offset m` |
| Injected sort when paging | none |
| Concat | `||` |
| Coalesce | `coalesce` |
| Boolean literal | `true` / `false` |
| Identifier quoting | double quotes (`as "t1"`) |
| Derived table / TVF alias | required |
| `*ALL` | supported |
| Arrays | supported (`any(@array)`, `cardinality`, ...) |
| JSON/JSONB | supported (`json_agg`, `->`, ...; `JsonDocument` params bind as `jsonb`) |
| `greatest` / `least` / `date_trunc` | supported (`greatest`/`least` ignore NULL arguments) |
| Conditional function | `iif(cond, a, b)` → `case when cond then a else b end` |
| Window functions | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` supported |
| `date_add` / `end_of_month` / `DateTime.Add*` | interval arithmetic (`x + (n * interval '1 day')`) |
| `string_agg` / `array_agg` / aggregate `filter` | supported |
| Session/info functions | `current_user`, `session_user`, `current_schema`, `current_database()`, `version()` |
| Table functions | `generate_series(...)`, `unnest(...)` |
| Recursive CTE | `with recursive` (no max-recursion option) |
| `stdev` / `stdevp` | `stddev` / `stddev_pop` |
| `var` / `varp` | `variance` / `var_pop` |
| `DateTime.Now` / `UtcNow` | `now()` / `now() at time zone 'utc'` |
| `ORDER BY … DESC` null placement | nulls sort first |

## See also

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
