# SQLite-specific SQL

> SQLite contributes the core scalar functions (`printf`/`format`, `hex`/`unhex`, `random`/`randomblob`,
> `quote`, `typeof`, `glob`, `unicode`/`char`, `soundex`, `octet_length`, `if`/`ifnull`), the JSON1
> functions/operators/aggregates and the `json_each`/`json_tree` table-valued functions, the date
> helpers (`timediff`, `unixepoch`, `julianday`), the math-extension functions
> (`acos`/`asin`/`atan`/`atan2`, the hyperbolics, `log2`/`log10`, `mod`) and the FTS3/FTS4/FTS5
> full-text query surface.

**Prerequisites:** [Querying and projections](../../querying/index.md) · [SQLite provider](../../providers/sqlite.md)

All of the functions on this page are exposed through
[`SqlFunctions.Sqlite`](xref:NextORM.Core.SqliteFunctions) and gated by
[`ISqlDialect.SqliteFunctions`](xref:NextORM.Core.ISqlDialect.SqliteFunctions). A provider that does not
opt in throws `NotSupportedException` instead of emitting SQL it cannot execute.

## Core scalar functions

| C# | SQLite | Notes |
|---|---|---|
| `printf(format, ...)` / `format(format, ...)` | `printf(...)` / `format(...)` | C-style `%` formatting; `format` needs 3.38+ |
| `hex(value)` | `hex(...)` | upper-case hex of the value treated as a BLOB |
| `unhex(value)` / `unhex(value, ignored)` | `unhex(...)` | decode hex to a BLOB; needs 3.41+ |
| `random()` | `random()` | pseudo-random 64-bit integer |
| `randomblob(count)` | `randomblob(...)` | a `count`-byte random BLOB |
| `quote(value)` | `quote(...)` | the SQL literal of the value |
| `@typeof(value)` | `typeof(...)` | `null`/`integer`/`real`/`text`/`blob` |
| `glob(pattern, value)` | `glob(...)` | GLOB match (the pattern is the first argument) |
| `unicode(value)` | `unicode(...)` | code point of the first character |
| `@char(c1, ...)` | `char(...)` | string from Unicode code points |
| `octet_length(value)` | `octet_length(...)` | byte length of the encoded value |
| `soundex(value)` | `soundex(...)` | requires the `SQLITE_SOUNDEX` build option |
| `ifnull(value, other)` | `ifnull(...)` | two-argument `coalesce` |
| `@if(condition, whenTrue, whenFalse)` | `if(...)` | `if()` alias needs 3.48+ |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Label = SqlFunctions.Sqlite.printf("%d-%s", c.Id, c.String),
        Kind = SqlFunctions.Sqlite.@typeof(c.String),
        Ascii = SqlFunctions.Sqlite.unicode(c.String)
    })
    .ToList();
```

```sql
select printf('%d-%s', id, somestring) as 'Label',
       typeof(somestring) as 'Kind',
       unicode(somestring) as 'Ascii'
from complex_entity
```

## JSON1

Scalar access, construction and mutation:

| C# | SQLite |
|---|---|
| `json_extract<T>(json, path)` | `json_extract(json, path)` |
| `json_get(json, path)` | `json -> path` |
| `json_get_text(json, path)` | `json ->> path` |
| `json(value)` / `jsonb(value)` | `json(...)` / `jsonb(...)` (`jsonb` needs 3.45+) |
| `json_array(v1, ...)` | `json_array(...)` |
| `json_array_insert(json, path, value, ...)` | `json_array_insert(...)` |
| `json_insert` / `json_replace` / `json_set` | same names |
| `json_object(label, value, ...)` | `json_object(...)` |
| `json_patch(target, patch)` | `json_patch(...)` |
| `json_pretty(json)` | `json_pretty(...)` (needs 3.46+) |
| `json_quote(value)` | `json_quote(...)` |
| `json_remove(json, path, ...)` | `json_remove(...)` |
| `json_type(json)` / `json_type(json, path)` | `json_type(...)` |
| `json_valid(json)` / `json_valid(json, flags)` | `json_valid(...)` |

The JSON1 functions are built into SQLite since 3.38 (opt-in `SQLITE_ENABLE_JSON1` before that).

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Name = SqlFunctions.Sqlite.json_extract<string>(c.String, "$.name"),
        Raw = SqlFunctions.Sqlite.json_get(c.String, "$.name"),
        Updated = SqlFunctions.Sqlite.json_set(c.String, "$.name", "nextorm")
    })
    .ToList();
```

```sql
select json_extract(somestring, '$.name') as 'Name',
       (somestring -> '$.name') as 'Raw',
       json_set(somestring, '$.name', 'nextorm') as 'Updated'
from complex_entity
```

Aggregates turn a group into a JSON array/object:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Array = SqlFunctions.Sqlite.json_group_array(c.String),
        Object = SqlFunctions.Sqlite.json_group_object(c.Int, c.String)
    })
    .ToList();
```

## `json_each` / `json_tree` table functions

`json_each` walks the immediate children of a JSON value and `json_tree` walks it recursively. Use them
through [`FromTableFunction`](../11-table-valued-functions.md) and project the row shape returned by the
call; the columns are `key`, `value`, `type`, `fullkey` and `path` (plus `id`/`parent` for
`json_tree`).

```csharp
var values = dataContext
    .FromTableFunction(() => SqlFunctions.Sqlite.json_each("[\"a\",\"b\",\"c\"]"))
    .Select(r => r.Value)
    .ToList();
```

```sql
select value from json_each('["a","b","c"]')
```

## Date helpers

`timediff(a, b)` returns a signed SQLite interval string (3.43+), `unixepoch(value)` the seconds since
1970-01-01 (3.38+), and `julianday(value)` the Julian day number.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Seconds = SqlFunctions.Sqlite.unixepoch(c.Datetime),
        Day = SqlFunctions.Sqlite.julianday(c.Datetime)
    })
    .ToList();
```

## Math functions

The math functions map to SQLite's math extension (`SQLITE_ENABLE_MATH_FUNCTIONS`), available in the
SQLite build shipped with `nextorm.sqlite`. The surface exposes `acos`, `acosh`, `asin`, `asinh`,
`atan`, `atan2`, `atanh`, `cosh`, `log10`, `log2`, `mod`, `sinh` and
`tanh`; the portable [`Math.*`](../../scalar-functions/index.md) mappings (`Math.Sqrt`, `Math.Log10`,
`Math.Sign`, …) also render on SQLite. `degrees`/`radians`/`pi` are portable
([`SqlFunctions.Sql`](../../scalar-functions/02-math-functions.md)) rather than SQLite-only.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Pi = SqlFunctions.Sql.pi(),
        Degrees = SqlFunctions.Sql.degrees(SqlFunctions.Sql.pi()),
        Log2 = SqlFunctions.Sqlite.log2(8.0)
    })
    .ToList();
```

```sql
select pi() as 'Pi', degrees(pi()) as 'Degrees', log2(8) as 'Log2' from complex_entity
```

## Full-text search (FTS3/FTS4/FTS5)

SQLite's full-text search lives in the `fts3`, `fts4` and `fts5` virtual-table modules. nextorm
exposes only the **query** side through [`SqlFunctions.Sqlite`](xref:NextORM.Core.SqliteFunctions); it
does not create or maintain the virtual table, so create it with raw SQL (or your own migration):

```sql
create virtual table article_fts using fts5(title, body);
```

Support exists only when the SQLite build includes the module — the bundled provider ships all three.
These members are independent of the cross-provider `SqlFunctions.Sql.contains`/`freetext`
predicates, which stay gated by [`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText)
(`false` on SQLite) and therefore throw `NotSupportedException`. Every FTS member is SQL-only: the
in-memory provider throws `NotSupportedException`.

### FTS5

| C# | SQLite |
|---|---|
| `Match(tableOrColumn, query)` | `tableOrColumn MATCH query` |
| `MatchTable<TEntity>(table, query)` | `table(query)` as a `FROM` source |
| `FTS5bm25(table)` / `FTS5bm25(table, weights…)` | `bm25(table[, weights…])` |
| `Highlight(table, columnIndex, startMatch, endMatch)` | `highlight(...)` |
| `Snippet(table, columnIndex, startMatch, endMatch, ellipses, tokens)` | `snippet(...)` |
| `Rank(table)` | `table.rank` (hidden column) |

`Match` takes either a trusted constant table/alias token or a mapped column expression as its first
argument; `query` is the FTS5 match expression (`hel*`, `"hello world"`, `hello OR goodbye`, …).

```csharp
var query = "hello";

var rows = dataContext.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", query))
    .OrderBy(x => x.RowId)
    .Select(x => new
    {
        x.RowId,
        x.Title,
        Score = SqlFunctions.Sqlite.FTS5bm25("article_fts"),
        Preview = SqlFunctions.Sqlite.Snippet("article_fts", 1, "[", "]", "...", 8),
        Marked = SqlFunctions.Sqlite.Highlight("article_fts", 0, "<b>", "</b>")
    })
    .ToList();
```

```sql
select rowid, title, bm25("article_fts") as 'Score',
       snippet("article_fts", 1, '[', ']', '...', 8) as 'Preview',
       highlight("article_fts", 0, '<b>', '</b>') as 'Marked'
from article_fts
where "article_fts" match $query
order by rowid
```

`Rank(table)` reads FTS5's hidden `rank` column (equal to the default `bm25` score; smaller is a
better match). Pass weights to `FTS5bm25` to score a specific column:

```csharp
var weighted = dataContext.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", query))
    .Select(x => new { x.RowId, Score = SqlFunctions.Sqlite.FTS5bm25("article_fts", 1.0, 2.0) })
    .ToList();
```

The table-valued form filters in `FROM`, so it composes with the rest of the builder:

```csharp
var rows = dataContext
    .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<Article>("article_fts", query))
    .Where(x => x.RowId > 0)
    .OrderBy(x => x.RowId)
    .Select(x => new { x.RowId, x.Title })
    .ToList();
```

```sql
select rowid, title from "article_fts"($query) where rowid > 0 order by rowid
```

### FTS3 / FTS4

| C# | SQLite |
|---|---|
| `Match(tableOrColumn, query)` | `tableOrColumn MATCH query` |
| `RowId(table)` | `table.rowid` (hidden column) |
| `FTS3Offsets(table)` | `offsets(table)` |
| `FTS3MatchInfo(table)` / `FTS3MatchInfo(table, format)` | `matchinfo(table[, format])` |
| `FTS3Snippet(table)` / `FTS3Snippet(table, …)` | `snippet(table[, …])` |
| `Rank(matchInfo)` | `rank(matchInfo)` |

FTS3/4 has **no built-in `rank`**. `Rank` renders `rank(matchinfo(...))` and expects a SQL UDF named
`rank` registered on the connection (the provider does not register one); without it the query fails
with `SQLite Error 1: no such function: rank`. `FTS3Snippet` has overloads for the start/end markers,
the ellipsis, the column index (`-1` for every column) and the token count.

```csharp
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

// Register the ranking UDF once, on the connection the context will use.
using var connection = new SqliteConnection("Data Source=app.db");
connection.CreateFunction<byte[]?, long>("rank", static matchInfo =>
{
    // Score the matchinfo() blob (see SQLite's matchinfo documentation); this sample sums its bytes.
    long score = 0;
    if (matchInfo is not null)
        foreach (var b in matchInfo)
            score += b;
    return score;
});

using var dataContext = new DataContextBuilder().UseSqlite(connection).CreateDataContext();

var rows = dataContext.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", "hello"))
    .Select(x => new
    {
        x.RowId,
        HiddenRowId = SqlFunctions.Sqlite.RowId("article_fts"),
        Offsets = SqlFunctions.Sqlite.FTS3Offsets("article_fts"),
        MatchInfo = SqlFunctions.Sqlite.FTS3MatchInfo("article_fts", "pcx"),
        Snippet = SqlFunctions.Sqlite.FTS3Snippet("article_fts", "[", "]", "...", 0, 8),
        Score = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("article_fts"))
    })
    .ToList();
```

The first argument of the auxiliary functions (and the table token of `Match`) is a trusted constant
emitted as a quoted identifier — never build it from user input.

Creating, populating and tuning the index stays outside the query surface: the FTS5
maintenance/control functions (`AutoMerge`, `CrisisMerge`, `Merge`, `Optimize`, `Rebuild`,
`IntegrityCheck`) are deferred to follow-ups
[#195](https://github.com/AlexeyShirshov/nextorm/issues/195) and
[#196](https://github.com/AlexeyShirshov/nextorm/issues/196). Run those statements through raw SQL in
the meantime.

See [SQLite provider](../../providers/sqlite.md) for the provider-level summary.
