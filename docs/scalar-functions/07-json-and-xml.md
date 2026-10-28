# JSON and JSONB (PostgreSQL)

PostgreSQL is the only supported provider with `json`/`jsonb` types. A JSON operand is expected to be a
`json`/`jsonb` expression: a mapped column, another JSON function, or a parameter whose runtime value is
a `JsonDocument`, `JsonElement` or `JsonNode` (Npgsql binds those as `jsonb`). A plain JSON string parameter is
bound as `text` and can be parsed explicitly with `SqlFunctions.Postgres.json_cast(value)` (`cast(value as jsonb)`).
The six accessors `json_get`, `json_get_text`, `json_get_path` and `json_get_path_text` (string-key and
integer-index overloads) are the exception: they apply **contextual SQL typing** to a raw client document
value — a literal, a captured value or an explicit parameter — and parse it as `jsonb` in the generated SQL.
Native `json`/`jsonb` columns and server-side JSON expressions are not cast and keep their operand and result
types.

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

The four access operators type both operands. A raw client document value (literal, captured value or
explicit parameter) is parsed as `jsonb` — `json_get("{\"a\":1}", "a")` renders
`cast('{"a":1}' as jsonb) -> cast('a' as text)` — while a native `json`/`jsonb` column or a server-side JSON
expression is emitted unchanged (`doc -> cast('a' as text)`), so its operand and result types do not change.
The key/index/path operand is typed per overload: `cast(<key> as text)`, `cast(<index> as integer)`,
`cast(<path> as text[])`. Provider parameter binding is unchanged; an ordinary `string` parameter still binds
as `text`.

A `path`/`keys` operand is a `string[]` and is bound as a **single array parameter** (see
[Arrays](06-arrays.md#arrays-postgresql)), so `SqlFunctions.Postgres.json_get_path(json, new[] { "a", "b" })` renders
`json #> cast(@p0 as text[])`. The JSONPath functions take the path as a plain string and render it as
`cast(<path> as jsonpath)`; `JSON_EXISTS`, `jsonpath` and the sibling JSON functions are unchanged by this
typing rule.

## SQL/JSON constructors, paths and readers (PostgreSQL 17)

These members complete the PostgreSQL JSON surface. All of them are PostgreSQL-only: the scalar and path
forms are gated by
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

`json` and `json_serialize` take JSON **text**: the argument is rendered as `cast(x as text)` and
`FORMAT JSON` makes the server parse it, so invalid text raises `22P02` (measured) and a plain text
parameter needs no separate cast. `json` returns `json`, while `json_serialize` names its text result
with `returning text`, so it reads back as `text`. `json_scalar` is the value constructor — a string
becomes a JSON string (`"abc"`), a number a JSON number (`5`), and SQL NULL stays SQL NULL. All three
are PostgreSQL 17 parser-level constructs with no `pg_proc` row.

### Path extraction

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_extract_path(doc, path)` | `json_extract_path(cast(doc as json), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.json_extract_path_text(doc, path)` | `json_extract_path_text(cast(doc as json), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.jsonb_extract_path(doc, path)` | `jsonb_extract_path(cast(doc as jsonb), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.jsonb_extract_path_text(doc, path)` | `jsonb_extract_path_text(cast(doc as jsonb), variadic cast(path as text[]))` |

The document is JSON text cast to `json`/`jsonb`, and the `string[]` path is bound as **one** `text[]`
parameter passed through `variadic`, which PostgreSQL requires when the array arrives as a single
expression (measured: the bare `text[]` form resolves to no function). An absent path, a JSON `null`
element and a SQL NULL path all yield SQL NULL; the `_text` forms read a JSON `null` element as SQL
NULL, and an empty path returns the document itself (measured). `json_extract_path` returns `json`;
the `_text` forms return `text` and `jsonb_extract_path` returns `jsonb`.

### `jsonb_set_lax`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_set_lax(doc, path, newValue, createIfMissing, nullValueTreatment)` | `jsonb_set_lax(cast(doc as jsonb), cast(path as text[]), cast(newValue as jsonb), createIfMissing, nullValueTreatment)` |

All five arguments are explicit — both PostgreSQL defaults are rendered instead of omitted. The
document and the new value are JSON text cast to `jsonb`, the path is one `text[]` (not variadic), and
`createIfMissing` is `true` for the default. `nullValueTreatment` applies only when `newValue` is SQL
NULL and is one of `use_json_null` (the default, writes JSON `null`), `delete_key` (removes the target
key), `return_target` (returns the document unchanged) or `raise_exception` (raises). An unknown
treatment is validated by the server, whose message names `null_value_treatment`; a SQL-NULL document
stays NULL.

### Time-zone-aware JSONPath scalars

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_path_exists_tz(doc, path, vars, silent)` | `jsonb_path_exists_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_match_tz(doc, path, vars, silent)` | `jsonb_path_match_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_query_array_tz(doc, path, vars, silent)` | `jsonb_path_query_array_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_query_first_tz(doc, path, vars, silent)` | `jsonb_path_query_first_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |

The document and `vars` are JSON text cast to `jsonb`, the path is cast to `jsonpath` (a text
parameter is not implicitly coerced) and `silent` is an ordinary boolean argument. The `_tz` form is
required for a path that compares date/time values with a time-zone-dependent (`timestamptz`)
conversion, such as the `.datetime()` method: the plain siblings are immutable and raise a server error
for such a path (measured message: "… without time zone usage"), while these four are time-zone-aware.
An empty match set yields `[]` from `query_array` and SQL NULL from `query_first`; a SQL-NULL document
yields SQL NULL (measured for `exists`).

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

The non-`jsonb` readers take JSON **text**, so the document is cast to `json` in the call (a text
parameter does not resolve the function). Row shapes are local interfaces in `SqlFunctions`:

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

An empty array, an empty object and a SQL-NULL document produce no rows, a path with no match selects
no rows (measured), and an absent JSON property leaves the caller-declared column SQL NULL.

### Refusals

| Family | Refused member | Tested reason |
|---|---|---|
| `json_populate_record(set)` | `SqlFunctions.Postgres.json_populate_record<TRow>(baseRecord, json, useJsonAsText)` | The first argument is an `anyelement` base row: neither a named composite type nor the pseudo-type `record` can be named from a CLR type, so no faithful call can be emitted. |
| `json_populate_record(set)` | `SqlFunctions.Postgres.json_populate_recordset<TRow>(baseRecord, json, useJsonAsText)` | The same `anyelement` base row as `json_populate_record`. |
| `jsonb_populate_record(set)` | `SqlFunctions.Postgres.jsonb_populate_record<TRow>(baseRecord, json)` | The result row type is the base row's composite type, which no CLR type can name. |
| `jsonb_populate_record(set)` | `SqlFunctions.Postgres.jsonb_populate_recordset<TRow>(baseRecord, json)` | The same `anyelement` base row as `jsonb_populate_record`. |
| `jsonb_populate_record_valid` | `SqlFunctions.Postgres.jsonb_populate_record_valid(baseRecord, json)` | Needs the same `anyelement` base row; without a base row the server raises `0A000` (could not determine row type). |
| `json_table` | `SqlFunctions.Postgres.json_table<TRow>(json, path)` | Needs a per-call `COLUMNS (...)` clause inside the call parentheses, while the expression model only renders a caller-declared schema as an alias column-definition list after the alias. |

Each member is refused with `NotSupportedException` when the source or expression is composed, before
any SQL is emitted. The `json_table` construct itself works on PostgreSQL 17; only the rendering model
cannot express it.

## JSON as text (SQL Server, MySQL/MariaDB)

SQL Server stores JSON in an ordinary `nvarchar` column — or, on 2025+ with a context built from an
explicit `Version(17, 0)`, in its opt-in native `json` type ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson);
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
