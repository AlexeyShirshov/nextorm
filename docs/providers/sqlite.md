# SQLite provider

> Use `nextorm.sqlite` for file-based or in-memory SQLite databases; it renders `$name` parameters, `limit`/`offset` paging, `ifnull` coalescing and `strftime` date parts, and registers custom `stdev`/`var` aggregates.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

[`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext) (`src/nextorm.sqlite/SqliteDataContext.cs`) wraps `Microsoft.Data.Sqlite`. It creates a
`SqliteConnection` from the connection string, registers the custom aggregate functions on every
connection it creates, and returns [`Instance`](xref:NextORM.Sqlite.SqliteDialect.Instance) from its `Dialect` property.

[`SqliteDialect`](xref:NextORM.Sqlite.SqliteDialect) (`src/nextorm.sqlite/SqliteDialect.cs`) is the dialect:

- parameter placeholder `$name`;
- string concatenation with `||`;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect.MakeCoalesce(System.String,System.String)) renders `ifnull(a, b)`;
- [`MakeNow`](xref:NextORM.Core.ISqlDialect.MakeNow(System.Boolean)) renders `datetime('now')` for both local and UTC (`SQLite has no now()`);
- date parts use `cast(strftime(...) as integer)` for `year`/`month`/`day`/`hour`/`minute`/`second` and
  `dayofyear` (`%j`), falling back to ANSI `extract(part from value)` for anything else;
- `Math.Log` maps to `ln(...)` (SQLite's `log()` is base 10);
- paging is `limit n` / `limit n offset m`; offset without limit becomes `limit -1 offset m`.

## Registering the provider

Two overloads are available on [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)
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

You can also construct the context directly (this is what the provider tests do):

```csharp
using NextORM.Core;
using NextORM.Sqlite;

using IDataContext ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());
```

## Custom aggregate functions

Microsoft.Data.Sqlite has no attribute-based auto-registration, so the core library registers its custom
aggregates explicitly and once per connection
(`src/nextorm.sqlite/SQLiteFunctions.cs`, called from [`OnConnectionCreated`](xref:NextORM.Sqlite.SqliteDataContext.OnConnectionCreated(System.Data.Common.DbConnection))):

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
var stddev = ctx.From<IComplexEntity>()
    .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
    .First();
```

```sql
select stdev(cast(id as double precision)) from complex_entity
```

## Date parts, coalesce and `LIKE`

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

`SqlFunctions.Sql.like(column, pattern)` and `SqlFunctions.Sql.like(column, pattern, escapeChar)` render a `like`
predicate; the string methods (`Contains`, `StartsWith`, `EndsWith`) are translated to `like` with the
appropriate wildcards.

## Paging

```csharp
ctx.From<IComplexEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<IComplexEntity>().Offset(10).Select(x => x.Id);    // limit -1 offset 10
```

```sql
select id from complex_entity limit 5 offset 10
select id from complex_entity limit -1 offset 10
```

SQLite has no `OFFSET` without `LIMIT`, so an offset-only query emits the sentinel `limit -1`.

## SQLite-only functions

`SqlFunctions.Sqlite` exposes the SQLite-only surface: the core scalars, JSON1, the date helpers and the
math-extension functions. `json_each`/`json_tree` are available as table functions. Other providers
reject every member of the surface with a `NotSupportedException`.

```csharp
ctx.From<IComplexEntity>()
    .Select(x => new
    {
        Json = SqlFunctions.Sqlite.json_extract<string>(x.String, "$.name"),
        Kind = SqlFunctions.Sqlite.@typeof(x.String),
        Pi = SqlFunctions.Sqlite.pi()
    });
```

See [SQLite-specific SQL](../guide/provider-specific/sqlite.md) for the full list, the native SQLite
spelling and the version/build-option requirements.

## Limitations

SQLite has no `ANY`/`ALL` subquery support. `SqlFunctions.Sql.any(...)` / `SqlFunctions.Sql.all(...)` are translated to
SQL, and the database rejects them at execution time with a `SqliteException` — the failure is not raised
during translation.

```csharp
// Throws Microsoft.Data.Sqlite.SqliteException when executed.
await ctx.From<ISimpleEntity>()
    .Where(it => it.Id == SqlFunctions.Sql.any(ctx.From<IComplexEntity>().Select(c => c.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

[`IntersectAll`](xref:NextORM.Core.QueryCommand`1.IntersectAll``1(NextORM.Core.QueryCommand{``0})) and [`ExceptAll`](xref:NextORM.Core.QueryCommand`1.ExceptAll``1(NextORM.Core.QueryCommand{``0})) are rejected by the dialect with a `NotSupportedException` because SQLite
has no `intersect all` / `except all`.

## Provider differences

| Aspect | SQLite |
|---|---|
| Parameter placeholder | `$name` |
| Concat | `||` |
| Coalesce | `ifnull` |
| `greatest` / `least` | `max` / `min` (scalar, 2+ arguments; returns NULL when any argument is NULL) |
| Conditional function | `iif(cond, a, b)` (SQLite 3.32+) |
| Window functions | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` supported |
| Boolean literal | `1` / `0` |
| Identifier quoting | single quotes (`as 't1'`) |
| Derived table alias | not required |
| TVF alias | not required |
| `*ALL` | not supported |
| `ANY`/`ALL` subqueries | rejected by the database at execution |
| Session/info functions | `version()` → `sqlite_version()` (no user/schema/database information) |

## See also

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
