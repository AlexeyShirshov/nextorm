# SQLite provider

> Use `nextorm.sqlite` for file-based or in-memory SQLite databases; it renders `$name` parameters, `limit`/`offset` paging, `ifnull` coalescing and `strftime` date parts, and registers custom `stdev`/`var` aggregates.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

`SqliteDbContext` (`src/nextorm.sqlite/SqliteDbContext.cs`) wraps `Microsoft.Data.Sqlite`. It creates a
`SqliteConnection` from the connection string, registers the custom aggregate functions on every
connection it creates, and returns `SqliteDialect.Instance` from its `Dialect` property.

`SqliteDialect` (`src/nextorm.sqlite/SqliteDialect.cs`) is the dialect:

- parameter placeholder `$name`;
- string concatenation with `||`;
- `MakeCoalesce` renders `ifnull(a, b)`;
- `MakeNow` renders `datetime('now')` for both local and UTC (`SQLite has no now()`);
- date parts use `cast(strftime(...) as integer)` for `year`/`month`/`day`/`hour`/`minute`/`second`,
  falling back to ANSI `extract(part from value)` for anything else;
- `Math.Log` maps to `ln(...)` (SQLite's `log()` is base 10);
- paging is `limit n` / `limit n offset m`; offset without limit becomes `limit -1 offset m`.

## Registering the provider

Two overloads are available on `DbContextBuilder`
(`src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.sqlite;

// From a file path (the debug build checks that the file exists).
var byPath = new DbContextBuilder().UseSqlite("app.db");

// From an existing, caller-owned connection.
using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
var byConnection = new DbContextBuilder().UseSqlite(connection);

using var ctx = byPath.CreateDbContext();   // IDataContext
```

You can also construct the context directly (this is what the provider tests do):

```csharp
using nextorm.core;
using nextorm.sqlite;

using IDataContext ctx = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());
```

## Custom aggregate functions

Microsoft.Data.Sqlite has no attribute-based auto-registration, so the core library registers its custom
aggregates explicitly and once per connection
(`src/nextorm.sqlite/SQLiteFunctions.cs`, called from `SqliteDbContext.OnConnectionCreated`):

| Function | Meaning |
|---|---|
| `stdev` | sample standard deviation (n−1) |
| `stdevp` | population standard deviation (n) |
| `var` | sample variance (n−1) |
| `varp` | population variance (n) |

The four share one `VarianceAccumulator`; a flavour returns `null` when there are too few non-null rows
(fewer than 2 for the sample flavour, fewer than 1 for the population flavour). Because the functions are
already named `stdev`/`var`, SQLite performs no aggregate name remapping.

```csharp
var stddev = ctx.Create<IComplexEntity>()
    .Select(x => NORM.SQL.stdev((double)x.Id))
    .First();
```

```sql
select stdev(cast(id as double precision)) from complex_entity
```

## Date parts, coalesce and `LIKE`

```csharp
var query = ctx.Create<IComplexEntity>()
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

`NORM.SQL.like(column, pattern)` and `NORM.SQL.like(column, pattern, escapeChar)` render a `like`
predicate; the string methods (`Contains`, `StartsWith`, `EndsWith`) are translated to `like` with the
appropriate wildcards.

## Paging

```csharp
ctx.Create<IComplexEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.Create<IComplexEntity>().Offset(10).Select(x => x.Id);    // limit -1 offset 10
```

```sql
select id from complex_entity limit 5 offset 10
select id from complex_entity limit -1 offset 10
```

SQLite has no `OFFSET` without `LIMIT`, so an offset-only query emits the sentinel `limit -1`.

## Limitations

SQLite has no `ANY`/`ALL` subquery support. `NORM.SQL.any(...)` / `NORM.SQL.all(...)` are translated to
SQL, and the database rejects them at execution time with a `SqliteException` — the failure is not raised
during translation.

```csharp
// Throws Microsoft.Data.Sqlite.SqliteException when executed.
await ctx.Create<ISimpleEntity>()
    .Where(it => it.Id == NORM.SQL.any(ctx.Create<IComplexEntity>().Select(c => c.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

`IntersectAll` and `ExceptAll` are rejected by the dialect with a `NotSupportedException` because SQLite
has no `intersect all` / `except all`.

## Provider differences

| Aspect | SQLite |
|---|---|
| Parameter placeholder | `$name` |
| Concat | `||` |
| Coalesce | `ifnull` |
| Boolean literal | `1` / `0` |
| Identifier quoting | single quotes (`as 't1'`) |
| Derived table alias | not required |
| TVF alias | not required |
| `*ALL` | not supported |
| `ANY`/`ALL` subqueries | rejected by the database at execution |

## See also

- [Provider overview](overview.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `test/nextorm.sqlite.tests/SqliteDialectTests.cs:23,29,42,50`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:120,133,142,152,161,173,204,217,892`,
`test/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`src/nextorm.sqlite/SqliteDialect.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs`,
`src/nextorm.sqlite/SqliteDbContext.cs`, `src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs`.
