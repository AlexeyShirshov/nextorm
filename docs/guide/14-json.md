# JSON support across providers

> There is no single portable JSON API: PostgreSQL exposes a native `json`/`jsonb` surface, SQL Server returns a document with a trailing `FOR JSON` clause and offers JSON-as-text functions, MySQL/MariaDB offer the same JSON-as-text functions over the `JSON_EXTRACT`/`JSON_SET` family, and the remaining providers reject JSON constructs.

**Prerequisites:** [Querying and projections](../querying/index.md) · [Scalar functions](../scalar-functions/07-json-and-xml.md#json-and-jsonb-postgresql) · [Table-valued functions](11-table-valued-functions.md#built-in-table-functions) · [Provider overview](../providers/overview.md)

## Overview

nextorm deliberately has no cross-provider JSON function surface. "Working with JSON" means different things on
different providers, and the engine keeps those mechanisms separate instead of pretending they are one
feature:

* **SQL Server** has two independent surfaces. [`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) executes the query and returns the
  whole result set as one JSON document (`FOR JSON PATH`/`FOR JSON AUTO`), and [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) provides the JSON-as-text scalar functions
  (`json_value`, `json_query`, `json_modify`, `isjson`) plus the `openjson` table function.
* **PostgreSQL** has native `json`/`jsonb` types (the only provider with them) and a function/operator
  surface on `SqlFunctions.Postgres`: construction, aggregation, access, containment and JSONPath.
* **MySQL and MariaDB** store JSON in text columns and expose the same JSON-as-text surface as SQL
  Server ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) over the
  `JSON_EXTRACT`/`JSON_UNQUOTE`/`JSON_SET` family.
* **ClickHouse** maps its string-JSON extractors (`JSONExtractString`, `JSONExtractInt`,
  `JSONExtractFloat`, `JSONExtractBool`, `JSONExtractRaw`, `JSONHas`, `JSONLength`, `JSONType`) — plus
  the array-returning `JSONExtractKeys`/`JSONExtractArrayRaw` (projecting as `string[]`) and
  `JSONExtractKeysAndValues` (projecting as `Tuple<string, T>[]`) — and the
  flat-JSON fast path (`visitParamExtractString`/`Int`/`Float`/`Bool`/`Raw`) through
  [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract), its JSONPath scalars
  `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (written as `json_value`/`json_query`/`json_exists`) and its
  native-JSON functions `JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString` (written as
  `json_all_paths`/`json_all_paths_with_types`/`to_json_string`) through the same flag. These take a
  native `JSON` value (`CAST(col AS JSON)` for a `String` column); `JSONAllPaths` projects as `string[]`
  and `JSONAllPathsWithTypes` as `Map(String, String)` → `Dictionary<string, string>`. A native `JSON`
  *column* is still not mapped (the driver returns it as `System.Text.Json.Nodes.JsonObject`).
* **SQLite** does not expose any JSON construct. The database has JSON1, but nextorm does not map it
  yet, so building the SQL throws `NotSupportedException`.
* **Every SQL provider** shares one JSON *terminal*: [`WriteJson`](xref:NextORM.Core.QueryCommand`1.WriteJson(System.IO.Stream)) /
  [`WriteJsonAsync`](xref:NextORM.Core.QueryCommand`1.WriteJsonAsync(System.IO.Stream,System.Threading.CancellationToken)) stream the query's projection to a caller-owned `Stream` as a JSON array or
  NDJSON, serialized client-side with `System.Text.Json` (see [Streaming JSON to a Stream](#streaming-json-to-a-stream)).
  This is a serialization terminal, not a SQL JSON function: it does not change the per-provider JSON
  surfaces below.

Because the mechanisms are different, the same conceptual result is written in different ways. Read the
section for your provider; the [provider matrix](#provider-matrix) and
[choosing an approach](#choosing-an-approach) show the side-by-side equivalents.

## Provider matrix

| Provider | `ForJson` output | Native `json`/`jsonb` | JSON-as-text functions | `openjson` as `FROM` |
|---|---|---|---|---|
| SQL Server | Supported ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)) | No - JSON lives in `nvarchar` | Supported ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) | Supported |
| PostgreSQL | No | Supported ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson)) | No | No |
| SQLite | No | No | No | No |
| MySQL / MariaDB | No | Not exposed | Supported ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) | No |
| ClickHouse | No | Not exposed | String JSON + JSONPath scalars + native-JSON functions (`JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString`; [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)) | No |
| In-memory | Not applicable (no SQL) | Not applicable | Not applicable | Not applicable |

"No" means the command is rejected with `NotSupportedException` when its SQL is built, not that the
database lacks JSON support.

## Streaming JSON to a Stream

[`WriteJson`](xref:NextORM.Core.QueryCommand`1.WriteJson(System.IO.Stream)) / [`WriteJsonAsync`](xref:NextORM.Core.QueryCommand`1.WriteJsonAsync(System.IO.Stream,System.Threading.CancellationToken)) are the one JSON surface shared by every SQL provider. The terminal
executes the query, reads each row through typed `DbDataReader` accessors and writes it to a
caller-owned `Stream` with `System.Text.Json` — it never materializes a `TResult` per row, so live
memory stays O(buffer) regardless of the result-set size. That makes it the right tool for piping a
large or unbounded result set to an HTTP response, a file or a network stream. It is **not** available
on the in-memory provider: there is no `DbDataReader` and no managed fallback, so both methods throw
`NotSupportedException` before the destination is touched.

The four terminals are members of [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) and are
mirrored as extension methods on [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). The
public overloads take no explicit parameter list: pass values through captured variables in the query.
Streaming needs an explicit `Select` projection — an entity query with no projection has no JSON shape
and throws `InvalidOperationException`.

```csharp
public void WriteJson(Stream destination);
public void WriteJson(Stream destination, JsonStreamOptions options);
public Task WriteJsonAsync(Stream destination, CancellationToken cancellationToken = default);
public Task WriteJsonAsync(Stream destination, JsonStreamOptions options, CancellationToken cancellationToken = default);
```

```csharp
await using var file = File.Create("orders.ndjson");

await ctx.From<Order>()
    .Where(o => o.CreatedAt >= from)
    .Select(o => new { o.Id, o.CreatedAt, o.Total })
    .WriteJsonAsync(file, new JsonStreamOptions
    {
        Mode = JsonStreamMode.NdJson,
        IgnoreNull = true,
    }, cancellationToken);
```

### Options

[`JsonStreamMode`](xref:NextORM.Core.JsonStreamMode) selects the container shape:

| Value | Output |
|---|---|
| [`Array`](xref:NextORM.Core.JsonStreamMode.Array) (default) | one `[ ... ]` document; a multi-column row is an object, a single-column projection is written as a bare value |
| [`NdJson`](xref:NextORM.Core.JsonStreamMode.NdJson) | one standalone JSON value per row, separated by `\n` |

[`JsonStreamOptions`](xref:NextORM.Core.JsonStreamOptions) shapes the document:

| Option | Meaning |
|---|---|
| `Mode` | [`Array`](xref:NextORM.Core.JsonStreamMode.Array) (default) or [`NdJson`](xref:NextORM.Core.JsonStreamMode.NdJson). |
| `Root` | Array only: wraps the document as `{"<Root>":[...]}`. `null` (default) writes a bare array. |
| `IgnoreNull` | When `true`, object members whose value is SQL NULL are omitted instead of written as `null`. Only affects object (multi-column) rows; a scalar `null` is still written. |
| `WriteIndented` | Array only: pretty-prints the document. |
| `PropertyNamingPolicy` | A `System.Text.Json` naming policy applied to the projected member names. A single-column (scalar) projection has no member name, so the policy has no effect there. |

`NdJson` combined with `Root` or with `WriteIndented` is contradictory and throws
`NotSupportedException` while the shape is planned — before any output is produced. Invalid option
values throw the same way from the terminal's options overloads.

### Supported shapes (fail-fast)

The projection must be a **flat** row of whitelisted scalar columns. Supported CLR types are `byte`,
`short`, `int`, `long`, `float`, `double`, `decimal`, `bool`, `string`, `Guid`, `DateTime`, `byte[]`
and their `Nullable<>` forms. Values are written the way `System.Text.Json` defaults render them:
numbers as JSON numbers, `bool` as a JSON boolean, `Guid` as canonical text, `DateTime` as ISO-8601,
`byte[]` as base64.

Anything outside that list throws `NotSupportedException` when the shape is planned, before any output
is produced — `TimeSpan`, `DateTimeOffset`, `DateOnly`/`TimeOnly`, enums, `Range<T>`, value-converted
columns (including JSON-column members) and streaming LOB columns are all rejected on purpose rather
than silently degraded. Entity-typed projection items are not rejected as such: they are flattened
into their mapped scalar columns (the standard SQL-mapping expansion) and those columns are then
validated against this same whitelist. These shape/option validation failures are raised before any
output; conversely, a provider-runtime or I/O failure, or cancellation, can occur mid-document and
leave partial output (see [Ownership, flushing and errors](#ownership-flushing-and-errors)).

A multi-column projection must have named members (an
anonymous type or a named record); a column without a name throws. There is no nesting: unlike SQL
Server `FOR JSON`, each row is one flat object, so project the fields you need and reshape on the
consumer side.

### Ownership, flushing and errors

* The destination is **caller-owned**: the terminal never closes it and never calls `Stream.Flush`,
  so flush or dispose it yourself after the call (for example with `await using` on a file).
* Rows are flushed to the destination as they are written (per row, at the buffer threshold and at
  the end), so output appears progressively while the query is still running.
* If an error occurs mid-document the exception propagates and the already-written bytes are left in
  place (the JSON document is truncated, with no closing bracket), the destination is **not** closed,
  and the reader and command are released. An empty result writes `[]` in `Array` mode and zero bytes
  in `NdJson` mode.
* A query backed by a lazy temporary-table source throws `NotSupportedException`; materialize it
  first with `ToTable`/`ToTempTable`. Passing `null` for the destination or options throws
  `ArgumentNullException`.

### `WriteJson` vs SQL Server `FOR JSON`

| | `WriteJson` / `WriteJsonAsync` | [`ForJson`](#return-the-whole-result-set-as-one-json-document) |
|---|---|---|
| Providers | every SQL provider | SQL Server only |
| Where the JSON is built | client side, `System.Text.Json`, O(buffer) memory | server side, `FOR JSON` |
| Shape | one flat object per row (or a bare scalar) | `FOR JSON PATH`/`AUTO`, driven by the projection or the table/join structure |
| Result | written incrementally to your `Stream` | one `string` materialized in memory (`null` when the query is empty) |
| Use when | large or unbounded result sets, HTTP/file/network output, portable code | a small result set, SQL Server-side nesting, or you want the database to render the JSON |

Both are terminals on the same query, so pick one — do not chain them.

## SQL Server

### Return the whole result set as one JSON document

[`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) is a **terminal operator**: it executes the query and returns the whole
result set as one JSON document (the projection drives the document shape; the query element type is
irrelevant because the database returns a single document column):

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true);
```

```sql
select id, somestring from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) shapes the document from the projection aliases (the default) and
[`Auto`](xref:NextORM.Core.ForJsonMode.Auto) from the table structure:

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Auto);
```

```sql
select id, somestring from complex_entity for json auto
```

The optional `root` wraps the document in `ROOT('name')` and `includeNullValues` adds
`INCLUDE_NULL_VALUES`. `ForJson` returns `null` when the query produces no rows (SQL Server returns SQL
NULL for an empty `FOR JSON` result). The clause is placed after `ORDER BY` and before a trailing
`OPTION (...)`, so it composes with [`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) (apply the hint first, then the
terminal): a `for json path option (recompile)` query is valid. Use
[`WithForJson`](xref:NextORM.Core.QueryCommand`1.WithForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean)) to only attach the clause and keep the command composable. (Table hints,
[`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])), attach to the `FROM` table and are independent of the
JSON clause.) A dialect that does not support the clause rejects the command, and combining `ForJson`
with `ForXml` throws `NotSupportedException("FOR JSON and FOR XML cannot be combined.")`.

### Read JSON stored in a text column

Neither SQL Server nor MySQL/MariaDB expose a native JSON type here: JSON is stored in an ordinary text
column and manipulated with text-oriented functions
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)). The path is a JSONPath string
(`'$.name'`); `json_value` returns a scalar, `json_query` an object/array fragment and `json_modify` a
modified copy. On MySQL/MariaDB the same expressions render as `json_unquote(json_extract(...))`,
`json_extract(...)` and `json_set(...)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Id = SqlFunctions.SqlServer.json_value(e.String, "$.id"),
        Name = SqlFunctions.SqlServer.json_query(e.String, "$.name"),
        Updated = SqlFunctions.SqlServer.json_modify(e.String, "$.id", "1")
    })
    .ToList();
```

```sql
select json_value(somestring, '$.id') as [Id], json_query(somestring, '$.name') as [Name], json_modify(somestring, '$.id', '1') as [Updated] from complex_entity
```

`SqlFunctions.SqlServer.isjson` tests whether a text value is valid JSON. On SQL Server a predicate
renders `(isjson(x)) = 1` because T-SQL `ISJSON` returns an `int`, and as a projected value it is cast
to `bit`; on MySQL/MariaDB it renders `json_valid(x)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.SqlServer.isjson(e.String))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (isjson(somestring)) = 1
```

### Turn JSON into rows with `openjson`

`SqlFunctions.SqlServer.openjson(json)` is a table-valued function (SQL Server 2016+) used through
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})). Its default
schema yields the properties of a JSON object or the elements of a JSON array as
[`SqlFunctions.IOpenJsonRow`](xref:NextORM.Core.SqlFunctions.IOpenJsonRow) (`Key`/`Value`/`Type`):

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .FromTableFunction(() => SqlFunctions.SqlServer.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type from openjson(@json) as [t1]
```

For a typed projection declare your own `[SqlTableFunction("openjson")]` wrapper whose row shape
matches the `WITH (...)` clause; nextorm only emits the call, it does not create the function.
`SqlFunctions.SqlServer.string_split` follows the same pattern for a comma-separated string. Both are
gated by [`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)), so only SQL Server emits them.

## PostgreSQL

### Pass a JSON value as a parameter

A parameter whose runtime value is a `JsonDocument`, `JsonElement` or `JsonNode` is bound as `jsonb`, so
it can be used directly with the JSON operators:

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), "name") == "Alice")
    .Select(e => new { e.Id })
    .ToList(document);
```

```sql
select id from complex_entity where ((@norm_p0 ->> 'name') = 'Alice')
```

A plain JSON string is bound as `text`; parse it explicitly with `SqlFunctions.Postgres.json_cast(value)`
(`cast(value as jsonb)`).

### Build a JSON object or array in the projection

Construction functions build a `json`/`jsonb` value from ordinary SQL expressions. String arguments that
are constants become the keys:

```csharp
var document = dataContext.From<IComplexEntity>()
    .Select(e => new { V = SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String) })
    .First()
    .V;
```

```sql
select jsonb_build_object('id', id, 'name', somestring) as "V" from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.jsonb_build_array(x, y)` | `jsonb_build_array(x, y)` |
| `SqlFunctions.Postgres.json_array(x, y)` / `jsonb_array(x, y)` | `json_array(x, y)` / `json_array(x, y returning jsonb)` |
| `SqlFunctions.Postgres.to_jsonb(x)` | `to_jsonb(x)` |

### Collapse a result set into one document (the `FOR JSON` analogue)

PostgreSQL has no `FOR JSON` suffix; the equivalent is the `jsonb_agg` aggregate over a projection. Use
`jsonb_agg`/`json_agg` for an array of values or `jsonb_object_agg` for a key/value map:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(
        SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String)))
    .First();
```

```sql
select jsonb_agg(jsonb_build_object('id', id, 'name', somestring)) from complex_entity
```

Nesting is expressed with `GroupBy`: the outer aggregate makes the document, the inner one makes each
group's array:

```csharp
var groups = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Items = SqlFunctions.Postgres.jsonb_agg(
            SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String))
    })
    .ToList();
```

```sql
-- jsonb_agg(jsonb_build_object('id', id, 'name', somestring)) grouped by int
```

`json_object_agg(key, value)` / `jsonb_object_agg(key, value)` produce a JSON object instead of an array
(`jsonb_object_agg(id, somestring)`).

### Read and filter JSON

The access operators and predicates are exposed as `SqlFunctions.Postgres` methods:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_get(json, "key")` / `json_get(json, 0)` | `json -> key` / `json -> 0` |
| `SqlFunctions.Postgres.json_get_text(json, "key")` / `json_get_text(json, 0)` | `json ->> key` / `json ->> 0` |
| `SqlFunctions.Postgres.json_get_path(json, path)` / `json_get_path_text(json, path)` | `json #> path` / `json #>> path` |
| `SqlFunctions.Postgres.json_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.json_exists(json, "key")` | `json ? 'key'` |
| `SqlFunctions.Postgres.json_exists_any(json, keys)` / `json_exists_all(json, keys)` | `json ?\| keys` / `json ?& keys` |
| `SqlFunctions.Postgres.jsonb_path_exists(json, path)` / `jsonb_path_match(json, path)` | `jsonb_path_exists(json, cast(path as jsonpath))` / `jsonb_path_match(...)` |
| `SqlFunctions.Postgres.jsonb_path_query_first(json, path)` / `jsonb_path_query_array(json, path)` | `jsonb_path_query_first(...)` / `jsonb_path_query_array(...)` |

A `path`/`keys` operand is a `string[]` bound as a single array parameter, so
`json_get_path(json, new[] { "a", "b" })` renders `json #> @p0`. JSONPath functions take the path as a
plain string and cast it to `jsonpath`.

```csharp
var path = new[] { "a", "b" };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.json_contains(
        SqlFunctions.Parameter<JsonDocument>(0),
        SqlFunctions.Parameter<JsonDocument>(1)))
    .Where(e => SqlFunctions.Postgres.json_exists(SqlFunctions.Parameter<JsonDocument>(2), "key"))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (@norm_p0 @> @norm_p1) and (@norm_p2 ? 'key')
```

### SQL/JSON query functions

The SQL/JSON query functions take a `jsonpath` (cast from the `path` string) and select a value rather
than a key:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_value(json, path)` | `json_value(json, cast(path as jsonpath))` (returns `text`) |
| `SqlFunctions.Postgres.json_query(json, path)` | `json_query(json, cast(path as jsonpath))` (returns `jsonb`) |
| `SqlFunctions.Postgres.json_exists(json, path, fromJsonPath)` | `json_exists(json, cast(path as jsonpath))` (returns `boolean`) |

```csharp
var json = SqlFunctions.Postgres.jsonb_build_object("a", 1);

var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Value = SqlFunctions.Postgres.json_value(json, "$.a"),
        Exists = SqlFunctions.Postgres.json_exists(json, "$.a", true)
    })
    .ToList();
```

```sql
select json_value(jsonb_build_object('a', 1), cast('$.a' as jsonpath)) as "Value",
       json_exists(jsonb_build_object('a', 1), cast('$.a' as jsonpath)) as "Exists"
from complex_entity
```

`json_exists` has two forms: the two-argument `json_exists(json, "key")` is the top-level `?` operator,
while the three-argument `json_exists(json, path, fromJsonPath)` is the SQL/JSON path function (the
third argument is a discriminator and is not rendered into SQL).

The construction and aggregation functions return `string?`; deserialize with
`JsonSerializer.Deserialize<T>(...)` when you need a .NET object.

## Choosing an approach

| Goal | SQL Server | PostgreSQL |
|---|---|---|
| One JSON document for the whole result set | `.ForJson(...)` | `jsonb_agg(jsonb_build_object(...))` |
| One JSON object per row | `json_query`/text concatenation, or assemble on the client | `jsonb_build_object(...)` |
| Read a field from a JSON value | `json_value(col, '$.x')` | `col ->> 'x'` |
| Test that a value is JSON | `isjson(col)` | the value is typed `json`/`jsonb` by the column |
| Filter by JSON content | `json_value(col, '$.x') = ...` | `col @> ...`, `col ? 'x'`, `col #> ...` |
| Expand JSON into rows | `openjson(...)` as a `FROM` source | no built-in helper; declare a `[SqlTableFunction]` wrapper |

## Limitations

* There is no portable JSON abstraction. Code written for one provider's JSON surface throws
  `NotSupportedException` on another; use `ISqlDialect` capability flags if you must branch.
* `ForJson`/`ForXml` are SQL Server only ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson) /
  [`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)), and the two are mutually exclusive on one command.
* The PostgreSQL `json`/`jsonb` surface (`SqlFunctions.Postgres`) requires [`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson);
  the SQL Server and MySQL/MariaDB text functions require
  [`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson). Each throws on the other provider.
* ClickHouse's JSON surface (`JSONExtract*`/`visitParamExtract*`, the JSONPath scalars and the
  native-JSON `JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString`) requires
  [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract); it does not overlap with the
  `SupportsTextJson` set. The native-JSON functions take a native `JSON` value (cast a `String` column
  with `CAST(col AS JSON)`); `json_all_paths` projects as `string[]` and `json_all_paths_with_types` as
  `Dictionary<string, string>` (`mapKeys`/`mapValues` turn the map into a collection). A native `JSON`
  *column* is not mapped yet: the driver returns it as `System.Text.Json.Nodes.JsonObject`, so project
  JSON through a `String` column or cast it in SQL.
* SQLite has JSON features in the database, but nextorm does not expose them yet; SQLite's JSON1
  extension is likewise not mapped.
* The in-memory provider produces no SQL, so `ForJson`/`ForXml` throw `NotSupportedException` and the
  JSON function surfaces do not apply to it.
* PostgreSQL is the only provider whose columns are mapped as a native JSON type in the parameter path;
  SQL Server and MySQL/MariaDB JSON is always text.

## See also

- [Querying and projections](../querying/index.md) - `ForJson`/`ForXml` for SQL Server.
- [Scalar functions](../scalar-functions/07-json-and-xml.md#json-and-jsonb-postgresql) - the full PostgreSQL JSON/JSONB surface and the SQL Server / MySQL/MariaDB text-JSON functions.
- [Table-valued functions](11-table-valued-functions.md#built-in-table-functions) - `openjson` and `string_split`.
- [PostgreSQL provider](../providers/postgres.md) - JSON parameter binding and arrays.
- [SQL Server provider](../providers/sqlserver.md) - `FOR JSON`, `FOR XML` and text JSON.
- [MySQL provider](../providers/mysql.md) and [MariaDB provider](../providers/mariadb.md) - text JSON over `JSON_EXTRACT`/`JSON_SET`.
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/Visitors/JsonSqlTranslator.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`src/nextorm.core/Query/SqlFunctions.SqlServer.cs`, `src/nextorm.core/Expressions/ForJson.cs`,
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.sqlserver/SqlServerDialect.cs`,
`src/nextorm.postgres/PostgresDialect.cs`; tests `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:443,695,1313,1390`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:848,868,887,930,950,1831,1861`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1773`.
