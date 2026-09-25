# SQLite-specific SQL

> SQLite contributes the core scalar functions (`printf`/`format`, `hex`/`unhex`, `random`/`randomblob`,
> `quote`, `typeof`, `glob`, `unicode`/`char`, `soundex`, `octet_length`, `if`/`ifnull`), the JSON1
> functions/operators/aggregates and the `json_each`/`json_tree` table-valued functions, the date
> helpers (`timediff`, `unixepoch`, `julianday`) and the math-extension functions
> (`acos` … `tanh`, `degrees`, `log2`/`log10`, `mod`, `pi`, `radians`).

**Prerequisites:** [Querying and projections](../01-querying-and-projections.md) · [SQLite provider](../../providers/sqlite.md)

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
through [`FromTableFunction`](../13-table-valued-functions.md) and project the row shape returned by the
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
`atan`, `atan2`, `atanh`, `cosh`, `degrees`, `log10`, `log2`, `mod`, `pi`, `radians`, `sinh` and
`tanh`; the portable [`Math.*`](../../guide/11-scalar-functions.md) mappings (`Math.Sqrt`, `Math.Log10`,
`Math.Sign`, …) also render on SQLite.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Pi = SqlFunctions.Sqlite.pi(),
        Degrees = SqlFunctions.Sqlite.degrees(SqlFunctions.Sqlite.pi()),
        Log2 = SqlFunctions.Sqlite.log2(8.0)
    })
    .ToList();
```

```sql
select pi() as 'Pi', degrees(pi()) as 'Degrees', log2(8) as 'Log2' from complex_entity
```
