# JSON and JSONB (PostgreSQL)

PostgreSQL is the only supported provider with `json`/`jsonb` types. When an argument represents a JSON
document, the SQL expression normally needs one of those types: for example, a mapped column, another
JSON function's result, or a parameter whose runtime value is `JsonDocument`, `JsonElement` or
`JsonNode` (Npgsql binds these as `jsonb`). A plain `string` parameter is bound as `text`; cast it
explicitly with `SqlFunctions.Postgres.json_cast(value)` (`cast(value as jsonb)`) when the function expects
a JSON-typed document.

The accessors `json_get`, `json_get_text`, `json_get_path` and `json_get_path_text` are the exception.
Their string-key and integer-index overloads apply **contextual SQL typing** to a raw client-side document
value — a literal, captured value or explicit parameter — and cast it to `jsonb` in the generated SQL.
When the operand is already a native `json`/`jsonb` column or server-side JSON expression, the accessor
leaves it unchanged, preserving its operand and result types.

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), "name") == "Alice")
    .Select(e => new { e.Id })
    .ToList(document);
```

```sql
select id
 from complex_entity where ((@norm_p0 ->> 'name') = 'Alice')
```

The aggregates collapse a result set into a single JSON document:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(e.String))
    .First();

var person = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String))
    .First();
```

```sql
select jsonb_agg(somestring)
 from complex_entity
select jsonb_build_object('id', id, 'name', somestring)
 from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_agg(x)` / `jsonb_agg(x)` | `json_agg(x)` / `jsonb_agg(x)` |
| `SqlFunctions.Postgres.json_object_agg(k, v)` / `jsonb_object_agg(k, v)` | `json_object_agg(k, v)` / `jsonb_object_agg(k, v)` |
| `SqlFunctions.Postgres.json_build_object("a", x, ...)` | `json_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.json_build_array(x, y)` / `jsonb_build_array(x, y)` | `json_build_array(x, y)` / `jsonb_build_array(x, y)` |
| `SqlFunctions.Postgres.json_array(x, y)` / `jsonb_array(x, y)` | `json_array(x, y)` / `json_array(x, y returning jsonb)` |
| `SqlFunctions.Postgres.to_json(x)` / `to_jsonb(x)` | `to_json(x)` / `to_jsonb(x)` |
| `SqlFunctions.Postgres.json_cast(x)` | `cast(x as jsonb)` |
| `SqlFunctions.Postgres.json_get(json, "key")` / `json_get(json, 0)` | `json -> key` / `json -> 0` |
| `SqlFunctions.Postgres.json_get_text(json, "key")` / `json_get_text(json, 0)` | `json ->> key` / `json ->> 0` |
| `SqlFunctions.Postgres.json_get_path(json, path)` / `json_get_path_text(json, path)` | `json #> path` / `json #>> path` |
| `SqlFunctions.Postgres.json_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.json_exists(json, "key")` | `json ? 'key'` |
| `SqlFunctions.Postgres.json_exists_any(json, keys)` / `json_exists_all(json, keys)` | `json ?\| keys` / `json ?& keys` |
| `SqlFunctions.Postgres.json_array_length(json)` / `jsonb_array_length(json)` | `json_array_length(json)` / `jsonb_array_length(json)` |
| `SqlFunctions.Postgres.json_typeof(json)` / `jsonb_typeof(json)` | `json_typeof(json)` / `jsonb_typeof(json)` |
| `SqlFunctions.Postgres.jsonb_set(json, path, value[, create])` | `jsonb_set(...)` |
| `SqlFunctions.Postgres.jsonb_insert(json, path, value[, after])` | `jsonb_insert(...)` |
| `SqlFunctions.Postgres.jsonb_strip_nulls(json)` | `jsonb_strip_nulls(json)` |
| `SqlFunctions.Postgres.jsonb_pretty(json)` | `jsonb_pretty(json)` |
| `SqlFunctions.Postgres.jsonb_delete(json, "key")` / `jsonb_delete(json, 0)` | `json - 'key'` / `json - 0` |
| `SqlFunctions.Postgres.json_concat(a, b)` | `a \|\| b` |
| `SqlFunctions.Postgres.row_to_json(row)` | `row_to_json(row)` |
| `SqlFunctions.Postgres.array_to_json(array)` | `array_to_json(array)` |
| `SqlFunctions.Postgres.jsonb_path_exists(json, path)` | `jsonb_path_exists(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_match(json, path)` | `jsonb_path_match(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_query_first(json, path)` | `jsonb_path_query_first(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_query_array(json, path)` | `jsonb_path_query_array(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.json_value(json, path)` / `json_query(json, path)` | `json_value(json, cast(path as jsonpath))` / `json_query(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.json_exists(json, path, fromJsonPath)` | `json_exists(json, cast(path as jsonpath))` |

The access operators type the document and the key/index operand. For example, a raw client document is
cast to `jsonb`, while a native JSON expression is left as-is; the key, index or path is cast according to
the overload:

| Operand | SQL type |
|---|---|
| string key | `text` |
| integer index | `integer` |
| path or key list | `text[]` |

Thus `json_get("{\"a\":1}", "a")` renders `cast('{"a":1}' as jsonb) -> cast('a' as text)`, while a
native column renders as `doc -> cast('a' as text)`. Parameter binding itself does not change: an ordinary
`string` parameter is still bound as `text`.

Choose the path argument shape to match the API. The `json_get_path` accessors take `string[]`, and
`json_exists_any`/`json_exists_all` take a `string[]` of keys; each array is bound as one PostgreSQL
array parameter (see [Arrays](06-arrays.md#arrays-postgresql)). For example,
`SqlFunctions.Postgres.json_get_path(json, new[] { "a", "b" })` renders `json #> cast(@p0 as text[])`.
The JSONPath functions take a path string and cast it to `jsonpath`. The SQL/JSON forms `json_value`,
`json_query` and three-argument `json_exists` also take a JSONPath; they are distinct from
`json_exists(json, "key")`, which tests for a key. The contextual cast described above applies only to
the four accessors; SQL/JSON `JSON_EXISTS` and the other JSON functions keep their own typing rules.

## SQL/JSON constructors, paths and readers (PostgreSQL 17)

PostgreSQL 17 adds the SQL/JSON constructs in this section; the section also covers related path and
row-reader functions. These forms are PostgreSQL-only: scalar and path forms are gated by
[`SupportsPostgresJsonSql`](xref:NextORM.Core.ISqlDialect.SupportsPostgresJsonSql) and the readers by
`SupportsTableFunction`, so every other provider — including the in-memory provider — throws
`NotSupportedException` at translation. The aggregate members of the family are listed on the
[aggregates](05-aggregates.md) page; this page documents the scalar and row-returning members.

### SQL/JSON constructors

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json(x)` | `json(cast(x as text) format json)` |
| `SqlFunctions.Postgres.json_scalar(x)` | `json_scalar(x)` |
| `SqlFunctions.Postgres.json_serialize(x)` | `json_serialize(cast(x as text) format json returning text)` |

`json` and `json_serialize` consume JSON **text**. Their argument is rendered as `cast(x as text)`, and
`FORMAT JSON` tells PostgreSQL to parse it. Invalid text raises `22P02` (measured); a plain text parameter
needs no additional `json_cast`. `json` returns the `json` type. `json_serialize` explicitly returns
`text`, so its result reads back as `text`. By contrast, `json_scalar` constructs a JSON scalar: a string
becomes a JSON string (`"abc"`), a number a JSON number (`5`), and SQL NULL remains SQL NULL. These are
PostgreSQL 17 parser-level constructs, not catalog functions, so they have no `pg_proc` row.

### Path extraction

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_extract_path(doc, path)` | `json_extract_path(cast(doc as json), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.json_extract_path_text(doc, path)` | `json_extract_path_text(cast(doc as json), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.jsonb_extract_path(doc, path)` | `jsonb_extract_path(cast(doc as jsonb), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.jsonb_extract_path_text(doc, path)` | `jsonb_extract_path_text(cast(doc as jsonb), variadic cast(path as text[]))` |

These functions consume JSON text, cast the document to `json` or `jsonb`, and bind the `string[]` path
as **one** `text[]` parameter. They pass that array through `variadic`, as PostgreSQL requires when an
array arrives as a single expression; the bare `text[]` form does not resolve to a function (measured).
An absent path, a JSON `null` element or a SQL NULL path yields SQL NULL; the `_text` forms return SQL
NULL for a JSON `null` element. An empty path returns the document itself (measured).
`json_extract_path` returns `json`, the `_text` forms return `text`, and `jsonb_extract_path` returns
`jsonb`.

### `jsonb_set_lax`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_set_lax(doc, path, newValue, createIfMissing, nullValueTreatment)` | `jsonb_set_lax(cast(doc as jsonb), cast(path as text[]), cast(newValue as jsonb), createIfMissing, nullValueTreatment)` |

All five arguments are explicit; nextorm renders both PostgreSQL defaults instead of omitting them. The
document and new value are JSON text cast to `jsonb`; the path is one `text[]` parameter, not a
`variadic` argument. The default for `createIfMissing` is `true`.

Choose `nullValueTreatment` only for a SQL NULL `newValue`: `use_json_null` (the default) writes JSON
`null`, `delete_key` removes the target key, `return_target` leaves the document unchanged, and
`raise_exception` raises an error. The server validates unknown treatments and reports the argument as
`null_value_treatment`. A SQL-NULL document remains NULL.

### Time-zone-aware JSONPath scalars

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_path_exists_tz(doc, path, vars, silent)` | `jsonb_path_exists_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_match_tz(doc, path, vars, silent)` | `jsonb_path_match_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_query_array_tz(doc, path, vars, silent)` | `jsonb_path_query_array_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_query_first_tz(doc, path, vars, silent)` | `jsonb_path_query_first_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |

The document and `vars` are JSON text cast to `jsonb`. The path is cast to `jsonpath`; PostgreSQL does
not implicitly coerce a `text` parameter to that type. `silent` is an ordinary boolean argument.

Use the `_tz` forms when a path needs a time-zone-dependent (`timestamptz`) conversion, for example the
JSONPath `.datetime()` method. The plain sibling functions are immutable (their results must not depend
on session state) and raise a server error for such a path (measured message: "… without time zone
usage"); the `_tz` functions support it. An empty match set returns `[]` from `query_array` and SQL NULL
from `query_first`. A SQL-NULL document returns SQL NULL (measured for `exists`).

### Reader table functions

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_array_elements(doc)` | `json_array_elements(cast(doc as json))` |
| `SqlFunctions.Postgres.json_array_elements_text(doc)` | `json_array_elements_text(cast(doc as json))` |
| `SqlFunctions.Postgres.json_each(doc)` | `json_each(cast(doc as json))` |
| `SqlFunctions.Postgres.json_each_text(doc)` | `json_each_text(cast(doc as json))` |
| `SqlFunctions.Postgres.jsonb_path_query_tz(doc, path, vars, silent)` | `(select jsonb_path_query_tz from jsonb_path_query_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent))` |
| `SqlFunctions.Postgres.json_to_record<TRow>(doc)` | `json_to_record(cast(doc as json)) as x(col type, ...)` |
| `SqlFunctions.Postgres.json_to_recordset<TRow>(doc)` | `json_to_recordset(cast(doc as json)) as x(col type, ...)` |

These are table functions: use them as a `FROM` source when you need rows, rather than as scalar
expressions. The non-`jsonb` readers take JSON **text**, so the document is cast to `json` in the call
(a text parameter does not resolve the function). Their result rows use local interfaces in
`SqlFunctions`:

- `json_array_elements`/`json_array_elements_text` produce
  [`IJsonArrayElementsRow`](xref:NextORM.Core.SqlFunctions.IJsonArrayElementsRow): one
  `[Column("value")] string? Value` per element — a `json` document rendered as text, or plain text
  from the `_text` form, where a JSON `null` element is SQL NULL.
- `json_each`/`json_each_text` produce [`IJsonbEachRow`](xref:NextORM.Core.SqlFunctions.IJsonbEachRow):
  `[Column("key")] string? Key` and `[Column("value")] string? Value` — a `json` document rendered as
  text, or plain text from the `_text` form.
- `jsonb_path_query_tz` produces
  [`IJsonPathQueryTzRow`](xref:NextORM.Core.SqlFunctions.IJsonPathQueryTzRow): the single
  `[Column("jsonb_path_query_tz")] string? Value` holds one match as a `jsonb` document rendered as
  text; the one-column subquery restores that column name after the FROM alias.
- `json_to_record<TRow>`/`json_to_recordset<TRow>` derive the alias column-definition list from
  `TRow`'s mapped `[Column]` names and types, so the caller declares the row shape;
  `json_to_record` reads one object into one row and `json_to_recordset` one row per array element.

For these row-returning forms, an empty array, an empty object or a SQL-NULL document produces no rows.
A path with no match also selects no rows (measured). If a JSON property is absent,
`json_to_record`/`json_to_recordset` leave the caller-declared column SQL NULL.

### Refusals

| Family | Refused member | Tested reason |
|---|---|---|
| `json_populate_record(set)` | `SqlFunctions.Postgres.json_populate_record<TRow>(baseRecord, json, useJsonAsText)` | The first argument is an `anyelement` base row: neither a named composite type nor the pseudo-type `record` can be named from a CLR type, so no faithful call can be emitted. |
| `json_populate_record(set)` | `SqlFunctions.Postgres.json_populate_recordset<TRow>(baseRecord, json, useJsonAsText)` | The same `anyelement` base row as `json_populate_record`. |
| `jsonb_populate_record(set)` | `SqlFunctions.Postgres.jsonb_populate_record<TRow>(baseRecord, json)` | The result row type is the base row's composite type, which no CLR type can name. |
| `jsonb_populate_record(set)` | `SqlFunctions.Postgres.jsonb_populate_recordset<TRow>(baseRecord, json)` | The same `anyelement` base row as `jsonb_populate_record`. |
| `jsonb_populate_record_valid` | `SqlFunctions.Postgres.jsonb_populate_record_valid(baseRecord, json)` | Needs the same `anyelement` base row; without a base row the server raises `0A000` (could not determine row type). |
| `json_table` | `SqlFunctions.Postgres.json_table<TRow>(json, path)` | Needs a per-call `COLUMNS (...)` clause inside the call parentheses, while the expression model only renders a caller-declared schema as an alias column-definition list after the alias. |

These refusals are limits of the expression model, not a claim that PostgreSQL lacks the operations.
The `json_populate_record*` forms need a PostgreSQL composite row type that the CLR mapping cannot name;
`json_table` needs its `COLUMNS (...)` declaration inside the call, while nextorm can declare a row shape
only after a source alias. Each member therefore throws `NotSupportedException` when the source or
expression is composed, before SQL is emitted. `json_table` itself works on PostgreSQL 17.

## JSON as text (SQL Server, MySQL/MariaDB)

SQL Server stores JSON in an ordinary `nvarchar` column — or, on 2025+ with a context declaring major
version 17+ or an Azure/Fabric SQL database engine edition, in its opt-in native `json` type ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson);
object/array roots only, see [JSON support](../guide/16-json.md#native-json-on-sql-server-2025)) — and
offers a text-oriented function subset
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)); MySQL/MariaDB expose the same
surface over the `JSON_EXTRACT`/`JSON_SET` family. The path is a JSONPath string (`'$.name'`), and `json_value` returns
a scalar while `json_query` returns an object/array fragment:

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
select json_value(somestring, '$.id') as [Id], json_query(somestring, '$.name') as [Name], json_modify(somestring, '$.id', '1') as [Updated]
 from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.json_value(json, path)` | `json_value(json, path)` |
| `SqlFunctions.SqlServer.json_query(json, path)` | `json_query(json, path)` |
| `SqlFunctions.SqlServer.json_modify(json, path, value)` | `json_modify(json, path, value)` |
| `SqlFunctions.SqlServer.isjson(value)` | `isjson(value)` |

On MySQL/MariaDB the same calls render as `json_unquote(json_extract(...))`, `json_extract(...)`,
`json_set(...)` and `json_valid(...)`.

`SqlFunctions.SqlServer.isjson` returns a boolean: in a predicate it renders `(isjson(x)) = 1` (T-SQL `ISJSON`
returns an `int`) and as a projected value it is cast to `bit`. `isjson` is also used directly in a
`WHERE` (`Where(e => SqlFunctions.SqlServer.isjson(e.String))`).

MariaDB JSON and dynamic-column support is provider-specific; consult the
[MariaDB catalogue](08-reference.md#mariadb-catalogue) for supported forms and refusals.

## XML data-type methods (SQL Server)

SQL Server's `xml` type exposes postfix methods
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)). They are
called through `SqlFunctions.SqlServer` and render `xmlcol.method(...)`; the XQuery and the SQL type
must be string literals (both are emitted verbatim, with embedded single quotes escaped):

```csharp
var rows = dataContext.From<IXmlEntity>()
    .Select(x => new
    {
        Value = SqlFunctions.SqlServer.xml_value<string>(x.Payload, "(/root/item)[1]", "nvarchar(100)"),
        Fragment = SqlFunctions.SqlServer.xml_query(x.Payload, "/root/item[1]"),
        Exists = SqlFunctions.SqlServer.xml_exist(x.Payload, "/root/item[2]")
    })
    .ToList();
```

```sql
select payload.value('(/root/item)[1]', 'nvarchar(100)') as [Value], payload.query('/root/item[1]') as [Fragment], payload.exist('/root/item[2]') as [Exists]
 from xml_entity
```

Grammar-required literal arguments — JSON keys and documents, XQuery and format patterns — are rendered with
the provider's own string-literal form (PostgreSQL `E'…'`, SQL Server `N'…'`, MySQL/MariaDB `_utf8mb4 X'…'`,
SQLite and ClickHouse quoted with apostrophes doubled), while a PostgreSQL argument containing `U+0000` is
rejected with `NotSupportedException`.

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.xml_value<T>(xml, xpath, sqlType)` | `xml.value('xpath', 'sqlType')` |
| `SqlFunctions.SqlServer.xml_query(xml, xpath)` | `xml.query('xpath')` |
| `SqlFunctions.SqlServer.xml_exist(xml, xpath)` | `xml.exist('xpath')` |
| `SqlFunctions.SqlServer.xml_nodes(xml, xpath)` | `xml.nodes('xpath') as [alias]([value])` (APPLY source) |

`xml_exist` returns `bit`: in a predicate it renders `(xml.exist('xpath')) = 1`, as a projected value it
stays a bit.

The rowset method `.nodes` is a correlated source, not a scalar: use it as the source of
`CrossApply`/`OuterApply` and project the unfolded `IXmlNodesRow.Value` with the scalar methods above.
It unfolds the XML value into one row per node selected by the XQuery and renders
`<xml>.nodes('xpath') as [alias]([value])`:

```csharp
var rows = dataContext.From<IXmlEntity>()
    .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.Payload, "/root/item"))
    .Select(p => new
    {
        Id = SqlFunctions.SqlServer.xml_value<int>(p.Item2.Value, "(.)[1]/@id", "int"),
        Text = SqlFunctions.SqlServer.xml_value<string>(p.Item2.Value, "(.)[1]", "nvarchar(100)")
    })
    .ToList();
```

```sql
select t2.value.value('(.)[1]/@id', 'int') as [Id], t2.value.value('(.)[1]', 'nvarchar(100)') as [Text]
 from xml_entity as [t1]
 cross apply t1.payload.nodes('/root/item') as [t2](value)
```

The operand must be a column of the outer row and the XQuery a string literal; every other provider
rejects `xml_nodes` with a `NotSupportedException`, as does the in-memory provider.

## JSON functions (ClickHouse, #286 T286b)

ClickHouse's JSON surface is dispatched by `JsonExtractSqlTranslator` and gated by
`ISqlDialect.SupportsJsonExtract`. The members below complete the catalogue; result shapes are explicit
(`long?`, `ulong?`, `bool?`, `double?`, `string`, `Tuple<string, string>[]`, `string[]`,
`Dictionary<string, string>`) and normalised with `toNullable`/`toInt64` where the vendor infers a
narrower type.

| Function | SQL |
|---|---|
| `SqlFunctions.ClickHouse.json_merge_patch(a, b)` | `JSONMergePatch(a, b)` |
| `SqlFunctions.ClickHouse.json_array_length(j)` | `JSONArrayLength(j)` |
| `SqlFunctions.ClickHouse.json_dynamic_paths(j)` / `json_dynamic_paths_with_types(j)` | `JSONDynamicPaths` / `JSONDynamicPathsWithTypes` |
| `SqlFunctions.ClickHouse.json_shared_data_paths(j)` / `json_shared_data_paths_with_types(j)` | `JSONSharedDataPaths` / `JSONSharedDataPathsWithTypes` |
| `SqlFunctions.ClickHouse.json_key(j, path)` | `JSONKey(j, path)` |
| `SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(j)` | `JSONExtractKeysAndValuesRaw(j)` |
| `SqlFunctions.ClickHouse.json_extract_uint(j, path)` | `JSONExtractUInt(j, path)` |
| `SqlFunctions.ClickHouse.is_valid_json(s)` | `isValidJSON(s)` |
| `SqlFunctions.ClickHouse.simple_json_extract_raw` / `_int` / `_uint` / `_float` / `_bool` / `_string` | `simpleJSONExtractRaw` / `Int` / `UInt` / `Float` / `Bool` / `String` |

The string-JSON extractors (`JSONExtractString`/`Int`/`Float`/`Bool`/`Raw`/`JSONHas`/`JSONType`,
`json_length`, `visitParamExtract*`), the array forms (`json_extract_keys`/`json_extract_array_raw`/
`json_extract_keys_and_values<T>`) and the native-JSON forms (`json_all_paths`,
`json_all_paths_with_types`, `to_json_string`, the `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` scalars) are
documented on [ClickHouse-specific SQL](../guide/provider-specific-sql/clickhouse.md#json-dictionaries-and-array-functions).
`pretty_print_json` (introduced in 26.4) and `json_extract_uint_or_default` (no such function; the vendor
zero default already covers it) are refused with a precise reason.
