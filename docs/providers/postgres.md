# PostgreSQL provider

> Use `nextorm.postgres` for PostgreSQL; it renders `@name` parameters, `limit`/`offset` paging, `coalesce`, `extract` date parts and `stddev`/`variance` aggregates, supports `INTERSECT ALL`/`EXCEPT ALL`, and requires double-quoted aliases.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

`PostgresDbContext` (`src/nextorm.postgres/PostgresDbContext.cs`) wraps `Npgsql`. It creates an
`NpgsqlConnection` and passes null parameter values as `DBNull` (Npgsql rejects a null parameter value).

`PostgresDialect` (`src/nextorm.postgres/PostgresDialect.cs`) is the dialect:

- parameter placeholder `@name`;
- string concatenation with `||`;
- `MakeCoalesce` renders `coalesce(a, b)`;
- boolean literals are `true`/`false`;
- identifiers use double quotes (`Escape` returns `"name"`), and `MakeColumnReference` quotes a name too
  so that a quoted alias survives when it is referenced from an outer query;
- derived tables and table-valued functions must be aliased (`RequireSubqueryAlias` is `true`, and its
  consequence is that an alias is always emitted);
- `INTERSECT ALL` / `EXCEPT ALL` are supported (`SupportsIntersectExceptAll` is `true`);
- arrays are supported (`SupportsArrays` is `true`): array parameters with the `any`/`all` quantifiers
  and the array functions;
- JSON/JSONB is supported (`SupportsJson` is `true`): the `json_agg`/`jsonb_agg` aggregates, the
  construction/access functions and the `->`/`->>`/`@>`/`?` operators, with `JsonDocument`/`JsonElement`/
  `JsonNode` parameters bound as `jsonb`;
- `greatest`/`least` and the aggregate `FILTER (WHERE ...)` clause are enabled (`SupportsGreatestLeast` and
  `SupportsFilter` are `true`);
- `date_trunc` is enabled (`SupportsDateTrunc` is `true`);
- date arithmetic is enabled (`SupportsDateArithmetic` is `true`): `NORM.SQL.date_add`/`end_of_month`
  and the `DateTime.Add*` methods render PostgreSQL interval arithmetic
  (`x + (n * interval '1 day')`, `date_trunc('month', x) + interval '1 month - 1 day'`);
- the `string_agg`/`array_agg` aggregates are enabled (`SupportsStringArrayAggregates` is `true`);
- the extended scalar function library is enabled (`SupportsExtendedScalarFunctions` is `true`):
  additional math (`asin`, `cbrt`, `degrees`, `pi`, `mod`, ...), string (`split_part`, `lpad`,
  `initcap`, ...), POSIX regular expression (`regexp_replace`, `regexp_like`, ...), date/time
  (`age`, `make_date`, `to_char`, `extract`, ...) and `num_nulls`/`num_nonnulls`;
- the boolean, bitwise, statistical and ordered-set aggregates are enabled
  (`SupportsBooleanAggregates`, `SupportsBitAggregates`, `SupportsStatisticalAggregates` and
  `SupportsOrderedAggregates` are `true`): `bool_and`/`bool_or`/`every`, `bit_and`/`bit_or`/`bit_xor`,
  `corr`/`covar_*`/`regr_*` and `percentile_cont`/`percentile_disc`/`mode` with `WITHIN GROUP`;
- aggregate names are remapped: `stdev`→`stddev`, `stdevp`→`stddev_pop`, `var`→`variance`,
  `varp`→`var_pop`;
- `Math.Log` maps to `ln(...)` (PostgreSQL's `log()` is base 10);
- `DateTime.Now` renders `now()`, `DateTime.UtcNow` renders `now() at time zone 'utc'`;
- date parts render as `extract(part from value)`;
- paging is `limit n` / `limit n offset m`; an offset-only query emits `offset m` alone.

## Registering the provider

Two overloads are available on `DbContextBuilder`
(`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.postgres;

var byString = new DbContextBuilder().UsePostgres("Host=localhost;Database=app;Username=app;Password=secret");

using var connection = new Npgsql.NpgsqlConnection("Host=localhost;Database=app;...");
var byConnection = new DbContextBuilder().UsePostgres(connection);

using var ctx = byString.CreateDbContext();   // IDataContext
```

Directly:

```csharp
using nextorm.core;
using nextorm.postgres;

using IDataContext ctx = new PostgresDbContext("Host=localhost;Database=app;...", new DbContextBuilder());
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
var stdev = ctx.From<IComplexEntity>().Select(x => NORM.SQL.stdev((double)x.Id));  // stddev(...)
var varp  = ctx.From<IComplexEntity>().Select(x => NORM.SQL.varp((double)x.Id));   // var_pop(...)
```

`count` and `count_big` both render `count(*)`, because PostgreSQL's `count` already returns a 64-bit
integer.

## Arrays

PostgreSQL is the only supported provider with native arrays. An array operand is passed as a single
parameter, so `column = any(@array)` works with a runtime parameter or a captured array, and the SQL
does not depend on the number of elements:

```csharp
var ids = new long[] { 1, 2, 3 };

ctx.From<IComplexEntity>().Where(e => NORM.SQL.any(e.Id, ids));      // (id = any(@p0))
ctx.From<IComplexEntity>().Where(e => e.Id == NORM.SQL.any(ids));    // id = any(@p0)
ctx.From<IComplexEntity>().Where(e => e.Id == NORM.SQL.any(NORM.Param<long[]>(0))); // id = any(@norm_p0)
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

using var ctx = new PostgresDbContext(connectionString, new DbContextBuilder());
ctx.From<IComplexEntity>()
    .Where(e => NORM.SQL.json_get_text(NORM.Param<JsonDocument>(0), "name") == "Alice")
    .Select(e => e.Id)
    .ToList(document);

ctx.From<IComplexEntity>()
    .Select(e => NORM.SQL.jsonb_agg(e.String));   // jsonb_agg(somestring)
```

A plain JSON string is bound as `text`; use `NORM.SQL.json_cast(value)` to parse it as `jsonb`. The full
surface (`json_agg`, `jsonb_build_object`, `->`, `->>`, `#>`, `@>`, `?`, `?|`, `?&`, ...) is documented
in [Scalar functions](../guide/11-scalar-functions.md#json-and-jsonb-postgresql). Other providers
reject it with `NotSupportedException`.

## Additional function surface

PostgreSQL also opts into `greatest`/`least`, `date_trunc`, the `string_agg`/`array_agg` aggregates, the
aggregate `FILTER (WHERE ...)` clause and the built-in `generate_series`/`unnest` table functions:

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

These are documented in
[Scalar functions](../guide/11-scalar-functions.md#string-and-array-aggregates-postgresql). SQLite also
accepts the `FILTER` clause; the other functions are PostgreSQL-only.

## `*ALL` set operations and null ordering

PostgreSQL is the only supported relational provider that implements `INTERSECT ALL` and `EXCEPT ALL`,
so `IntersectAll`/`ExceptAll` render their SQL directly.

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
| `greatest` / `least` / `date_trunc` | supported |
| `date_add` / `end_of_month` / `DateTime.Add*` | interval arithmetic (`x + (n * interval '1 day')`) |
| `string_agg` / `array_agg` / aggregate `filter` | supported |
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

Source: `test/nextorm.postgres.tests/PostgresDialectTests.cs:21,27,41,49`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:117,130,139,151,160,172,199,211,223,248,788,976`,
`test/nextorm.integration.tests/PostgresSpecificTests.cs:15,24`,
`src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.postgres/PostgresDbContext.cs`,
`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs`.
