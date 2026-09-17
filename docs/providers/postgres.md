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
ctx.Create<ISimpleEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.Create<ISimpleEntity>().Offset(10).Select(x => x.Id);    // offset 10
```

```sql
select id from simple_entity limit 5 offset 10
select id from simple_entity offset 10
```

`OFFSET` may appear on its own, but `LIMIT` must come first when both are present. Because PostgreSQL
does not require an injected sort, no `ORDER BY` is added.

## Coalesce, date parts and aggregates

```csharp
var query = ctx.Create<IComplexEntity>()
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
var stdev = ctx.Create<IComplexEntity>().Select(x => NORM.SQL.stdev((double)x.Id));  // stddev(...)
var varp  = ctx.Create<IComplexEntity>().Select(x => NORM.SQL.varp((double)x.Id));   // var_pop(...)
```

`count` and `count_big` both render `count(*)`, because PostgreSQL's `count` already returns a 64-bit
integer.

## `*ALL` set operations and null ordering

PostgreSQL is the only supported relational provider that implements `INTERSECT ALL` and `EXCEPT ALL`,
so `IntersectAll`/`ExceptAll` render their SQL directly.

```csharp
var q = a.Select(x => x.Id).IntersectAll(b.Select(x => x.Id));   // ... intersect all ...
```

PostgreSQL treats `NULL` as the largest value, so `ORDER BY … DESC` puts the `NULL` group first. The
shared test suite avoids depending on this; the provider test pins it explicitly.

```csharp
var r = ctx.Create<IComplexEntity>()
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
