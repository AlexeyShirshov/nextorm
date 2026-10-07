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

`SqlFunctions.Sqlite` exposes the SQLite-only surface: the core scalars, JSON1, the date helpers, the
math-extension functions and the FTS3/FTS4/FTS5 full-text query surface. `json_each`/`json_tree` and
the FTS5 table-valued search are available as table functions. Other providers reject every member of
the surface with a `NotSupportedException`.

```csharp
ctx.From<IComplexEntity>()
    .Select(x => new
    {
        Json = SqlFunctions.Sqlite.json_extract<string>(x.String, "$.name"),
        Kind = SqlFunctions.Sqlite.@typeof(x.String),
        Pi = SqlFunctions.Sql.pi()
    });
```

### Full-text search (FTS3/FTS4/FTS5)

The FTS members query an FTS virtual table, and the SQLite-only maintenance command surface (the
`IDataContext.CreateSqliteFts5CommandBuilder(tableName)` factory, returning `SqliteFts5CommandBuilder`)
tunes it; nextorm never creates the table. Query support exists only when the SQLite build includes
the module (the bundled provider ships `fts3`, `fts4` and `fts5`). The scalar/table-valued FTS query
API lives on `SqlFunctions.Sqlite`; the maintenance commands are a separate command-builder surface,
not members of `SqlFunctions.Sqlite`. Both are separate from the cross-provider
`SqlFunctions.Sql.contains`/`freetext` predicates: those stay gated by
[`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText), which is `false` on SQLite, so
they throw `NotSupportedException`. Every FTS member is SQL-only — the in-memory provider throws
`NotSupportedException`.

| C# | SQLite | Module |
|---|---|---|
| `Match(tableOrColumn, query)` | `tableOrColumn MATCH query` | FTS3/4/5 |
| `MatchTable<TEntity>(table, query)` | `table(query)` as a `FROM` source | FTS5 |
| `FTS5bm25(table)` / `FTS5bm25(table, weights…)` | `bm25(table[, weights…])` | FTS5 |
| `Highlight(table, columnIndex, startMatch, endMatch)` | `highlight(...)` | FTS5 |
| `Snippet(table, columnIndex, startMatch, endMatch, ellipses, tokens)` | `snippet(...)` | FTS5 |
| `Rank(table)` | `table.rank` (hidden column) | FTS5 |
| `Rank(matchInfo)` | `rank(matchInfo)` (needs the `rank` UDF) | FTS3/4 |
| `RowId(table)` | `table.rowid` (hidden column) | FTS3/4 |
| `FTS3Offsets(table)` | `offsets(table)` | FTS3/4 |
| `FTS3MatchInfo(table[, format])` | `matchinfo(table[, format])` | FTS3/4 |
| `FTS3Snippet(table, …)` | `snippet(table, …)` | FTS3/4 |

The first argument of the auxiliary functions (and the table token of `Match`) is a **trusted constant**
table name; it is emitted as a quoted identifier, so never build it from user input. The FTS `query` is
bound as a parameter when it is a run-time value and inlined as a literal when it is constant.

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

FTS5 also has a table-valued `FROM` form, `MatchTable`, which composes like any other source
(`FromTableFunction`):

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

FTS3/4 has no built-in `rank`: SQLite resolves `rank(matchinfo(...))` through a **connection-registered
SQL UDF** named `rank`, which the provider does not register. Register it on every connection or the
query fails at execution with `SQLite Error 1: no such function: rank`:

```csharp
using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=app.db");
connection.CreateFunction<byte[]?, long>("rank", static matchInfo =>
{
    // Score the matchinfo() blob (see SQLite's matchinfo documentation); this sample just sums bytes.
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

Creating and populating the index stays outside the query surface (create the virtual table with raw
SQL), but the FTS5 maintenance/control commands are available through the SQLite-only command builder
`IDataContext.CreateSqliteFts5CommandBuilder(string tableName)`:

| Operation | Rendered values | Notes |
|---|---|---|
| `AutoMerge(value)` | `('automerge', value)` | `value` is 0..16; other values throw |
| `CrisisMerge(value)` | `('crisismerge', value)` | `value` must be nonnegative; `0`/`1` pass through unchanged |
| `Merge(pages)` | `('merge', pages)` | any signed `int`, passed through unchanged |
| `Optimize()` | `('optimize')` | one-column form |
| `Rebuild()` | `('rebuild')` | one-column form; unavailable for contentless FTS5 tables |
| `IntegrityCheck(checkExternalContent = null)` | `('integrity-check')` / `('integrity-check', 0\|1)` | one-column when the flag is omitted; with `true` also verifies external content |

Each operation returns a new immutable builder; the terminals are `ToSql()` (renders without touching
the database), `Execute()` (`int` affected rows) and `ExecuteAsync(CancellationToken = default)`
(`Task<int>`). Calling a terminal before selecting an operation throws `InvalidOperationException`.
The two-column `"rank"` form is used by `automerge`/`crisismerge`/`merge`, the one-column form by
`optimize`/`rebuild` (and by `integrity-check` with the flag omitted). These are command-surface
statements — not scalar or table-valued functions and not part of `SqlFunctions.Sqlite`. The table name
is rejected if it is null, empty, whitespace or contains a NUL character.

```csharp
var builder = ctx.CreateSqliteFts5CommandBuilder("article_fts");
var sql = builder.AutoMerge(4).ToSql();
// INSERT INTO "article_fts" ("article_fts", "rank") VALUES ('automerge', 4)
var affected = builder.Optimize().Execute();
```

The builder is SQLite-only: on every other provider the surface throws
`NotSupportedException($"{dialect.GetType().Name} does not support SQLite FTS5 maintenance commands.")`
before any database access. On success, `Execute`/`ExecuteAsync` return the driver's affected-row
`int`, nonnegative for a successful maintenance command; nextorm promises no fixed count or value
(these are not page or repair counts). Native SQLite errors propagate. `Rebuild` is unavailable for
contentless FTS5 tables, and `IntegrityCheck(true)` also verifies external content.

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

The LOB streaming terminals (`ToStream`/`ToTextReader`) are supported for a single `byte[]`/`string` column (binary and text). SQLite has no row locator of its own, so `Microsoft.Data.Sqlite` returns a true streaming `SqliteBlob` only when the query also selects `rowid`; the dialect appends its trailing [`LobLocatorColumn`](xref:NextORM.Core.ISqlDialect.LobLocatorColumn) (`rowid`, with the payload staying at ordinal `0`). The source must therefore be a normal rowid table: on a `view` or a `WITHOUT ROWID` table the **single-column streaming** command fails closed with the raw `Microsoft.Data.Sqlite.SqliteException: no such column: rowid` — there is no buffered fallback **on that scalar/locator LOB path** (the multi-column `ToDataReader` below uses a separate, buffered, locator-free seam). See [Streaming large objects](../guide/26-large-objects.md).

The multi-column `ToDataReader`/`ToDataReaderAsync` terminal **is** supported on SQLite. It goes through a separate, locator-free seam (the per-call `storeInCache: false` buffered preparation, not the `SequentialAccess` LOB path): the `rowid` locator is never appended, so `FieldCount` equals the projection column count and the ordinals match `Select`. The reader is **buffered**, not chunked — a `byte[]`/`string` column inside a multi-column projection is read whole into managed memory — so for a single large LOB column prefer `ToStream`/`ToTextReader`. The returned reader is caller-owned: dispose it to release the provider reader and the per-call command; the context stays alive and usable.

SQLite's own FTS3/FTS4/FTS5 query surface is available (see [Full-text search](#full-text-search-fts3fts4fts5)), as is the SQLite-only FTS5 maintenance command builder (`CreateSqliteFts5CommandBuilder`); only the cross-provider `SqlFunctions.Sql.contains`/`freetext` predicates (gated by [`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText), `false` on SQLite) stay outside it. The maintenance surface is rejected on every non-SQLite provider with `NotSupportedException`, and `Rebuild` is unavailable for contentless FTS5 tables.

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
| Full-text search | FTS3/FTS4/FTS5 query surface via `SqlFunctions.Sqlite` (`Match`, FTS5 `FTS5bm25`/`Highlight`/`Snippet`/`Rank`, FTS3/4 helpers, FTS5 table-valued `MatchTable`; FTS3/4 `Rank` needs a connection-registered `rank` UDF); plus the SQLite-only FTS5 maintenance command builder (`CreateSqliteFts5CommandBuilder`: `AutoMerge`/`CrisisMerge`/`Merge`/`Optimize`/`Rebuild`/`IntegrityCheck`, sync/async terminals returning the affected-row `int`); cross-provider `contains`/`freetext` throw |
| LOB streaming (`ToStream`/`ToTextReader`) | supported (`blob` / `text`; the source must expose `rowid` — a `view`/`WITHOUT ROWID` source fails with `SqliteException: no such column: rowid`) |
| Multi-column reader (`ToDataReader`/`ToDataReaderAsync`) | supported (buffered, locator-free; no chunked LOB; non-LOB projections and a LOB column inside a multi-column select) |
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
