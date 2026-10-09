# Streaming data to a Stream

> Write a `Select` result to a caller-owned `Stream` as JSON or RFC 4180 CSV, row by row, without materialising the result set: JSON is serialized client-side with `System.Text.Json`, CSV is formatted incrementally from the provider reader.

**Prerequisites:** [Querying and projections](../querying/index.md) · [Projections](../querying/01-projections.md) · [Streaming large objects (BLOB/CLOB)](26-large-objects.md) · [JSON support across providers](14-json.md) · [Limitations](../advanced/limitations.md)

## Overview

nextorm has two streaming *save* terminals, one per output format. Both write a query's projection straight to a caller-owned `Stream`, pull rows from the database reader and format them one at a time, and never materialise the result set into a list:

* [`WriteJson`](xref:NextORM.Core.QueryCommand`1.WriteJson(System.IO.Stream)) / [`WriteJsonAsync`](xref:NextORM.Core.QueryCommand`1.WriteJsonAsync(System.IO.Stream,System.Threading.CancellationToken)) write each row as a JSON array or NDJSON value, serialized client-side with `System.Text.Json`; live memory stays O(buffer).
* `WriteCsv` / `WriteCsvAsync` write each row as RFC 4180 CSV, reading every supported column without boxing; live memory stays O(row), except a `byte[]` column, which is read whole-field.

Neither terminal closes or disposes the destination — the caller owns the stream's lifetime, which is what lets you hand it an HTTP response body or a `FileStream`. Neither is available on the in-memory provider (there is no `DbDataReader` and no managed fallback), so both throw `NotSupportedException` before the destination is touched.

Pick a format below. To stream a single large value rather than a whole result set, use the [LOB terminals](26-large-objects.md) instead.

## JSON

[`WriteJson`](xref:NextORM.Core.QueryCommand`1.WriteJson(System.IO.Stream)) / [`WriteJsonAsync`](xref:NextORM.Core.QueryCommand`1.WriteJsonAsync(System.IO.Stream,System.Threading.CancellationToken)) are the one JSON surface shared by every SQL provider. The terminal executes the query, reads each row through typed `DbDataReader` accessors and writes it to a caller-owned `Stream` with `System.Text.Json` — it never materializes a `TResult` per row, so live memory stays O(buffer) regardless of the result-set size. That makes it the right tool for piping a large or unbounded result set to an HTTP response, a file or a network stream. It is **not** available on the in-memory provider: there is no `DbDataReader` and no managed fallback, so both methods throw `NotSupportedException` before the destination is touched.

The four terminals are members of [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) and are mirrored as extension methods on [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). Besides the option-only overloads, each terminal has an overload that binds **positional SQL parameter values** through a trailing `params` list (see [Positional SQL parameters](#positional-sql-parameters)). Streaming needs an explicit `Select` projection — an entity query with no projection has no JSON shape and throws `NotSupportedException` with the `[projection]` token.

```csharp
public void WriteJson(Stream destination);
public void WriteJson(Stream destination, JsonStreamOptions options);
public void WriteJson(Stream destination, JsonStreamOptions options, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters);
public Task WriteJsonAsync(Stream destination, CancellationToken cancellationToken = default);
public Task WriteJsonAsync(Stream destination, JsonStreamOptions options, CancellationToken cancellationToken = default);
public Task WriteJsonAsync(Stream destination, JsonStreamOptions options, CancellationToken cancellationToken, params object?[] parameters);
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

### JSON options

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

### Positional SQL parameters

Each JSON terminal has an overload that takes a trailing `params` list of **positional SQL parameter
values**. Element `i` is bound to the query's placeholder `i` (`NormParam.GetName(i)`), exactly as the
ordinary buffered terminals bind their `params` list; it is not a list of column selectors. An empty
list binds nothing (there is no arity guard), and a `null` element binds `DBNull`.

`JsonStreamOptions options` and `CancellationToken cancellationToken` are **required** on this overload
(no defaults), so `WriteJson(stream, null)` cannot be ambiguous with an options-only call; the
option-only overloads above are unchanged and keep the token optional.

```csharp
await using var file = File.Create("orders.ndjson");

await ctx.From<Order>()
    .Where(o => o.CreatedAt >= from && o.Total > minTotal)
    .Select(o => new { o.Id, o.CreatedAt, o.Total })
    .WriteJsonAsync(file, new JsonStreamOptions
    {
        Mode = JsonStreamMode.NdJson,
        IgnoreNull = true,
    }, cancellationToken, from, minTotal);
```

Both surfaces ([`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) and
[`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1)) and both the sync and async overloads
take the same list.

### Validation exceptions and runtime errors

Preflight validation — options, projection shape, member names, unsupported columns, reader binding,
in-memory and unsupported execution forms — throws `NotSupportedException` and follows the stable
message convention `JSON streaming validation [<token>]: <context>.` The fixed tokens are:

| Token | Raised when |
|---|---|
| `mode-options` | an unknown `JsonStreamMode`, or `NdJson` combined with `Root`/`WriteIndented` |
| `projection` | the query has no `Select` projection |
| `names` | an unnamed member, a duplicate name in one object scope, or a naming policy returning `null` |
| `unsupported-column` | a projected type or shape outside the supported whitelist |
| `reader-binding` | the provider's reader field type is incompatible with the declared column |
| `in-memory` | the context is the in-memory provider |
| `unsupported-execution-form` | a query backed by a lazy temporary-table source |

An incompatible reader schema (`[reader-binding]`) is detected after the reader is opened but **before
any destination write**, including the array/root framing, so a destination preloaded with sentinel
bytes is left byte-for-byte unchanged. Lifecycle errors keep their own type —
`InvalidOperationException`/`ObjectDisposedException` — and a `null` destination or options still throws
`ArgumentNullException`. Destination I/O errors, provider runtime errors and cancellation are **not**
normalized into validation failures and can still occur mid-document; a value-dependent numeric
overflow during a row read stays a runtime error (see
[Ownership, flushing and errors](#ownership-flushing-and-errors)).

### Supported shapes (fail-fast)

The scalar (leaf) CLR types are `byte`, `short`, `int`, `long`, `float`, `double`, `decimal`,
`bool`, `string`, `Guid`, `DateTime`, `byte[]`, enums and their `Nullable<>` forms. Values are written the
way `System.Text.Json` defaults render them: numbers as JSON numbers, `bool` as a JSON boolean,
`Guid` as canonical text, `DateTime` as ISO-8601, `byte[]` as base64.

**Enums** are supported. By default an enum is written as a JSON number equal to its underlying
integral value (all eight underlying integral types, without narrowing `uint`/`ulong`) — the numeric
value, not the enum name. A property- or type-level `[JsonConverter]` attribute carrying the stock
`JsonStringEnumConverter` or `JsonStringEnumConverter<TEnum>` is honoured instead, writing the string
form exactly as the stock converter does in `System.Text.Json`: named values by name, `[Flags]`
combinations joined the same way, and a value with no name as its underlying number written as a
string (the converter's default `allowIntegerValues: true`). The property attribute wins over the
enum-type attribute. A storage converter (`ValueConverter`, for example `EnumToStringConverter<T>`) is
**not** accepted — a text-stored enum is not what the JSON string representation means — and any other
JSON converter on an enum, as well as any `[JsonConverter]` attribute on a non-enum scalar, is rejected
before output.

On top of that flat whitelist the projection may **nest**:

* **Nested objects.** `new { ... }` (anonymous) and `new Dto { ... }` (named construction /
  member-initializer) recurse: every nested construction becomes its own JSON object, at any depth up
  to the writer limit (64). An entity-typed member is expanded into its mapped scalar columns and
  becomes an object too.
* **`Projection<T1,T2>` slots.** A bare join command whose result is a `Projection<T1,T2>` emits the
  top-level members `Item1` and `Item2`, each an object or a bare scalar according to its item type.
  A scalar slot is **not** wrapped in an invented object: `Select(p => new { p.Item1, ChildName =
  p.Item2.Name })` writes `Item1` as an object and `ChildName` as a string. Entity and scalar slots
  projected inside an explicit `new { ... }` follow the same rule.
* **Native arrays.** A rank-one provider-native array member (`T[]`, for example PostgreSQL
  `array_agg` or ClickHouse `Array(String)`/`Array(Int32)`) is written as a JSON array, recursing
  through the supported scalar-element contract. Jagged arrays (arrays of arrays) become nested JSON
  arrays. `byte[]` keeps Base64 precedence, including as a jagged element (`byte[][]` is an array of
  base64 strings, never an array of numbers).
* **Conditional nested construction.** `predicate ? new Dto { ... } : null` writes the object on the
  construction arm and `null` on the other arm; a hidden presence column carries the arm decision.
  The predicate must be translatable to SQL and exactly one arm must be a `new`/member-init
  construction. Two construction arms, a construction against a non-null arm, or an untranslatable
  predicate is rejected before output.

Everything else is rejected with `NotSupportedException` while the shape is planned, before any
output: `TimeSpan`, `DateTimeOffset`, `DateOnly`/`TimeOnly`, `Range<T>`, value-converted
columns (including JSON-column members) and streaming LOB columns. Nested construction has **explicit
exclusions** that are deferred to their own issues rather than silently flattened:

* **arbitrary JSON converters and native/text enum storage** — issue [#178](https://github.com/AlexeyShirshov/nextorm/issues/178);
* **child-collection query projections** (`List<T>`, `IEnumerable<T>`, dictionaries and any other
  collection with no provider-native array source) — issue [#172](https://github.com/AlexeyShirshov/nextorm/issues/172);
* **new naming-policy / options behaviour** — issue [#177](https://github.com/AlexeyShirshov/nextorm/issues/177);
* **DB-side JSON generation** (SQL Server `FOR JSON`, PostgreSQL `json_agg`, …) stays a separate
  server-side path for *nested* shapes; `WriteJson` on SQL Server uses `FOR JSON` internally only for an
  eligible flat shape (see [SQL Server native fast-path](#sql-server-native-fast-path));
* **multidimensional arrays** and rank-one arrays of unsupported element types (a `List<T>` member, a
  dictionary element, a value-converted element) fail closed instead of using an arbitrary runtime
  serialization path.

These shape/option validation failures are raised before any output; conversely, a provider-runtime
or I/O failure, or cancellation, can occur mid-document and leave partial output (see
[Ownership, flushing and errors](#ownership-flushing-and-errors)).

A multi-column projection must have named members (an anonymous type or a named record); a column
without a name throws when the shape is planned. Nested object members are validated **per object
scope**.

### SQL Server native fast-path

On SQL Server an eligible request is served by the database itself: the query gets a trailing
`FOR JSON PATH` clause and the reported single document column is copied straight to your `Stream` in
bounded chunks, with no managed per-row serialization. This is an internal optimization — it changes
neither the public API/signatures nor the JSON contract; only *where* the JSON is built changes
(server side instead of `System.Text.Json`).

A request is eligible for the native path only when **all** of these hold:

* the projection is a **flat object** — one named member per projected column, with no nested object,
  no `Projection<T1,T2>`/recursive shape and no scalar (single-column) projection;
* every member name is a **simple identifier** (`[A-Za-z_][A-Za-z0-9_]*`); a dotted (`a.b`) or
  otherwise special alias would become a `FOR JSON PATH` path or change the emitted property name;
* every bound column is `string`, `bool`, `short`, `int` or `long`, or a nullable form of one of them —
  `byte`, the floating-point types, `decimal`, `Guid`, `DateTime`, `byte[]` and every enum stay on the
  managed path, as do provider value conversions and `*OrDefault` projections;
* the options are the defaults `FOR JSON PATH` can reproduce: `Mode = Array`, `Root = null`,
  `WriteIndented = false`, `PropertyNamingPolicy = null`. `IgnoreNull` is **not** a disqualifier:
  `IgnoreNull = true` maps to `FOR JSON`'s default (null members omitted) and `IgnoreNull = false` to
  `INCLUDE_NULL_VALUES`.

Everything else — recursive nested/projection shapes, enums (numeric or string), provider conversions
(`decimal`/`Guid`/`DateTime`/binary/…), special aliases and naming policies, a root or indentation,
`NdJson`, `*OrDefault` substitution, and every other provider — automatically uses the managed
`System.Text.Json` writer, decided **before execution**. There is no retry: a failure on the native path
is not replayed through the managed path.

The native document is **logically equivalent** to the managed output, not necessarily byte-for-byte
identical: it is valid UTF-8 and parses to the same JSON values, but SQL Server's escaping differs from
`System.Text.Json` — for example it leaves `<`, `>`, `&` and non-ASCII characters unescaped. A large
document is split by the database across several reader rows and concatenated transparently. There is
no public API or signature change. An empty result still writes `[]` in `Array` mode, and destination
ownership is unchanged: the library never calls `Stream.Flush` and never disposes your `Stream`; on
cancellation or error the operation stops and rethrows, leaving any already-written bytes in place (see
[Ownership, flushing and errors](#ownership-flushing-and-errors)).

The scalar [`ForJson`](14-json.md#return-the-whole-result-set-as-one-json-document) / `ForJsonAsync`
terminals are a separate, unchanged surface: they still return one materialized `string`, and `null` for
an empty result.

### Names, null semantics and arrays

* **Scoped names.** JSON member names must be unique **within one object scope**. The same effective
  name in different scopes is legal — a nested child may repeat its parent's property name, and the
  two slots of a `Projection<T1,T2>` may both carry `Id` (`Item1.Id` and `Item2.Id`) — while a
  duplicate inside *one* object throws before any output.
* **Explicit construction is always an object.** `new { ... }` / `new Dto { ... }` establishes object
  presence: an object whose properties are all SQL `NULL` is still written as an object (`null`
  members, or omitted members when `IgnoreNull = true`), never as JSON `null`.
* **A null conditional arm is `null`.** `predicate ? new Dto { ... } : null` writes JSON `null` for
  the rows where the predicate is false, regardless of the construction's property values. Presence
  comes from a hidden sentinel column, not from the visible leaves.
* **An absent joined entity is `null`.** A `Projection<T1,T2>` slot (or a whole-entity projection)
  read through an outer join is JSON `null` when every mapped column of that entity slot is SQL
  `NULL` — the same absence predicate the ordinary materializer uses, evaluated on the raw `DBNull`
  inputs before `DefaultOnNull` substitution.
* **Arrays.** A SQL `NULL` array column is JSON `null`, an empty array is `[]`, and a supported
  null/reference element is JSON `null`. `IgnoreNull` applies to object members only, so a scalar
  `null` and the `null` inside an array are still emitted.
* **Scalar leaves** keep Phase 1 behaviour: `DBNull` → `null`, with the ordinary `Nullable` and
  `DefaultOnNull` (`*OrDefault`) substitutions.

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

| | `WriteJson` / `WriteJsonAsync` | [`ForJson`](14-json.md#return-the-whole-result-set-as-one-json-document) |
|---|---|---|
| Providers | every SQL provider | SQL Server only |
| Where the JSON is built | client side, `System.Text.Json`, O(buffer) memory | server side, `FOR JSON` |
| Shape | a nested object tree per row (or a bare scalar), from the projection | `FOR JSON PATH`/`AUTO`, driven by the projection or the table/join structure |
| Result | written incrementally to your `Stream` | one `string` materialized in memory (`null` when the query is empty) |
| Use when | large or unbounded result sets, HTTP/file/network output, portable code | a small result set, SQL Server-side nesting, or you want the database to render the JSON |

Both are terminals on the same query, so pick one — do not chain them.

## CSV

`WriteCsv`/`WriteCsvAsync` turn a query into CSV and write it straight to a `Stream` you supply. Rows are pulled from the database reader and formatted one at a time: the result set is **not** materialised into a list, no `TResult` instance is constructed for a row, and the per-row path reads every supported column without boxing. That includes SQL Server numeric columns: the CSV terminal reads them through the provider's storage-typed getter (the `MapTypedColumnExpression` hook, which receives the reader's actual field type) and converts them with a typed `Convert.To<T>` — no `GetValue` and no `Convert.ChangeType(object)`. Since [#168](https://github.com/AlexeyShirshov/nextorm/issues/168) the same storage-typed dispatch also backs the shared buffered materialization path (`MapColumnExpression` resolves the reader's field type at runtime), so a normal buffered `ToList` no longer boxes SQL Server numerics either. A column whose storage type is unknown or cannot be converted is rejected with `NotSupportedException` before any output. Memory use is O(row), so a query over millions of rows can be served to a file or an HTTP response without buffering it; binary (`byte[]`) columns are the exception — each is read and Base64-encoded whole-field per row, so memory is bounded by the largest field/row rather than a fixed buffer (see [Limitations](#limitations)).

This is the streaming terminal for **tabular** output. The single-`byte[]`/`string` [LOB terminals](26-large-objects.md) stream one large value; the CSV terminals stream every column and row of the projection.

### Terminals

Sync overloads take the parameters as a `params ReadOnlySpan<object?>`; async overloads take a `params object?[]`:

```csharp
public static void WriteCsv<TResult>(this QueryCommand<TResult> command, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params ReadOnlySpan<object?> parameters);
public static Task WriteCsvAsync<TResult>(this QueryCommand<TResult> command, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params object?[] parameters);
```

The same two terminals exist directly on the query builder, so a whole-entity projection does not need an explicit `Select`:

```csharp
public static void WriteCsv<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params ReadOnlySpan<object?> parameters);
public static Task WriteCsvAsync<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params object?[] parameters);
```

The `QueryCommand<TResult>` form is the type `Select` returns; the `EntityBuilder<TEntity>` form forwards to it. Both prepare a **fresh per-call command with `storeInCache: false`**, so a CSV export neither reads nor writes the plan cache and never reuses the shared buffered command of the same query shape.

### Usage

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// QueryCommand<TResult> surface: project the columns you want in the file.
await using var file = File.Create("orders.csv");
ctx.From<Order>()
    .Where(x => x.Total > 100)
    .Select(x => new { x.Id, x.CustomerName, x.Total, x.CreatedAt })
    .WriteCsv(file);

// EntityBuilder<TEntity> surface: the whole entity, with a parameter and a token.
await using var report = File.Create("report.csv");
await ctx.From<Order>()
    .Where(x => x.CreatedAt >= from)
    .WriteCsvAsync(report, null, cancellationToken, from);
```

### CSV options

```csharp
public sealed class CsvStreamOptions
{
    public bool IncludeHeader { get; set; } = true;
    public char Delimiter { get; set; } = ',';
    public string NullMarker { get; set; } = "\\N";
    public bool ExcelMode { get; set; }
    public Func<object?, string?>? ValueTransform { get; set; }
}
```

Pass `null` (or omit the argument) for the defaults, or an instance to change the dialect. The options are read once, when the export starts:

```csharp
ctx.From<Order>()
    .Select(x => new { x.Id, x.CustomerName })
    .WriteCsv(file, new CsvStreamOptions
    {
        Delimiter = ';',
        IncludeHeader = false,
        NullMarker = "NULL",
        ExcelMode = true,
        ValueTransform = v => v is DateTime d ? d.ToString("yyyy-MM-dd") : v?.ToString(),
    });
```

| Option | Default | Meaning |
|---|---|---|
| `IncludeHeader` | `true` | emit the projection-derived header row before the data rows |
| `Delimiter` | `,` | field separator |
| `NullMarker` | `\N` (two characters: backslash, `N`) | literal text written for a SQL NULL |
| `ExcelMode` | `false` | enable the formula-injection guard |
| `ValueTransform` | `null` | per-value text transform; `null` keeps the box-free default formatting |

The rest of the dialect stays fixed (see the [dialect table](#dialect)): UTF-8 without a BOM, RFC 4180 escaping, CRLF terminators and invariant-culture value formats.

#### NULL, empty values and the marker

* **SQL NULL → `NullMarker` verbatim.** A SQL `NULL`/`DBNull` is written as `NullMarker` exactly, unquoted, and never passed through `ValueTransform`.
* **Empty string → empty field.** A non-`NULL` empty string (or an empty `byte[]`) is written as an empty field, so a reader can tell a missing value from an empty one.
* **Marker-equal text is force-quoted.** A non-`NULL` value whose formatted text equals `NullMarker` exactly is wrapped in `"` (`"\N"`) so it can never be read back as NULL.
* **The marker is validated before any output.** `NullMarker` must be non-empty and must not contain the `Delimiter`, a `"`, CR or LF; a violating value is rejected before the header (and any data row) is written.

#### Excel guard

With `ExcelMode = true`, a field whose formatted text begins with `=`, `+`, `-` or `@` is prefixed with an apostrophe (`'`) so spreadsheet applications treat it as text rather than a formula. The guard runs **after** `ValueTransform` and **before** RFC 4180 escaping, so the apostrophe becomes part of the escaped field (it ends up inside the quotes) and a transform cannot smuggle a formula past it. It applies to the formatted text of every column, including negative numeric values.

#### Value transform

`ValueTransform` receives every non-`NULL` value (boxed) and returns the text that replaces the default formatting, or `null` to write the NULL marker. It runs before the Excel guard and before CSV escaping; a SQL NULL never invokes it. The default path (no transform) stays box-free — boxing happens only when `ValueTransform` is set.

### Dialect

| Aspect | Behaviour |
|---|---|
| Encoding | UTF-8 **without** a byte-order mark |
| Header | one row before the data when `IncludeHeader` is `true` (the default); names come from the projection property names, falling back to `Column1`, `Column2`, … for unnamed members |
| Row terminator | CRLF (`\r\n`) |
| Field separator | `options.Delimiter`, default `,` |
| Quoting | RFC 4180: a field is wrapped in `"` when it contains the delimiter, a `"`, CR or LF; an embedded `"` is doubled (`""`) |
| `null` / `DBNull` | the literal `options.NullMarker` (default `\N`), written verbatim and unquoted |
| Empty string / empty `byte[]` | empty field (unquoted) |
| Marker-equal data | force-quoted (`"\N"`) so it cannot be read back as NULL |
| Excel guard | with `ExcelMode = true`, a value starting with `=`/`+`/`-`/`@` gets a leading `'` (after `ValueTransform`, before escaping) |
| `ValueTransform` | optional per-value transform for non-`NULL` values; running before the Excel guard and escaping |
| Culture | `CultureInfo.InvariantCulture` for every value |
| `DateTime` / `DateTimeOffset` | round-trip `"O"` format |
| `Guid` | `"D"` format |
| `bool` | `true` / `false` |
| `byte[]` | Base64 string |
| Numbers | invariant-culture default formatting |

### Providers

| Provider | `WriteCsv` / `WriteCsvAsync` |
|---|---|
| PostgreSQL | supported |
| SQL Server | supported |
| SQLite | supported |
| MySQL / MariaDB | supported |
| ClickHouse | supported |
| In-memory | `NotSupportedException` |

Unlike the LOB terminals, CSV export needs no sequential-access support and no `rowid` locator, so it works against every relational provider. The in-memory provider has no `DbDataReader` to stream from and rejects the terminal with `NotSupportedException`, exactly like `ToDataReader`; there is no buffered fallback in memory — materialise the query and write the CSV yourself if you need that.

### Writing to an HTTP response or a file

The terminal writes to `destination` and **never closes or disposes it**: the caller owns the stream's lifetime. That is what lets you hand it an HTTP response body or a `FileStream` and keep control.

```csharp
// Minimal API: stream a query straight into the response body.
app.MapGet("/orders.csv", async (HttpContext http, CancellationToken ct) =>
{
    using var ctx = new DataContextBuilder()
        .UsePostgres(connectionString)
        .CreateDataContext();

    http.Response.ContentType = "text/csv; charset=utf-8";
    http.Response.Headers.ContentDisposition = "attachment; filename=orders.csv";

    await ctx.From<Order>()
        .Select(x => new { x.Id, x.CustomerName, x.Total, x.CreatedAt })
        .WriteCsvAsync(http.Response.Body, cancellationToken: ct);
});
```

ASP.NET Core owns `Response.Body` and disposes it after the handler returns; the terminal does not. The same shape works for a file the caller opens and disposes:

```csharp
await using var file = File.Create("orders.csv");   // caller owns the file
await ctx.From<Order>().WriteCsvAsync(file);
```

### Ownership, cancellation and errors

* **Stream ownership.** The destination `Stream` is never closed by the terminal — on success, cancellation or error. Dispose it yourself when you opened it.
* **Reader/command ownership.** The terminal opens a per-call command and reader and releases both when it returns (or throws); the [`DataContext`](xref:NextORM.Core.DataContext) stays alive and usable.
* **Cancellation.** The token is honoured while reading rows and, on the async path, while writing to the destination; a cancelled export throws `OperationCanceledException` without closing the destination stream.
* **Write errors.** An exception from the destination stream (a broken socket, a full disk) propagates after the reader and command are released; the destination is still left open.

### Limitations

* **In-memory context — `NotSupportedException`.** There is no `DbDataReader`, so the terminal fails closed instead of buffering; see [Providers](#providers).
* **Lazy temp-table sources — `NotSupportedException` before any output.** A query that reads a source created with [`AsTempTable`](18-create-table-as.md#lazy-temporary-tables-astemptable) is not a single statement: it needs a `DROP` + `CREATE TEMPORARY TABLE ... AS SELECT` + read batch on one session, which the CSV terminal cannot stream. The terminal fails closed with `NotSupportedException` before the header (and any data row) is written and never runs the batch, so the destination is never left with a partial file; materialise the query first (for example `ToList`/`ToListAsync`) and write the rows yourself.
* **Unsupported projections — rejected before any row is written.** A projection column that has no single scalar CSV form, or whose storage type is unknown or cannot be converted, is rejected up front: a nested entity or collection, an array other than `byte[]`, a tuple, a dictionary, an arbitrary object, and a column whose mapping needs an object-based converter or a `GetValue` fallback. The failure happens before the header is written, so the destination is never left with a partial file.
* **Binary (`byte[]`) fields are read whole-field — memory is bounded by the largest field/row.** A `byte[]` column is materialised with a typed `GetFieldValue<byte[]>` and then Base64-encoded for the row (the encoder itself runs in 3-byte chunks), so peak memory is not a fixed buffer: exporting rows with very large BLOBs holds at least one full field in memory at a time. Chunked LOB streaming — writing a binary field chunk by chunk from a sequential-access reader — is a future slice (trigger: demand for exporting large binary columns). To stream a single large value with O(buffer) memory today, use the [single-`byte[]`/`string` LOB terminals](26-large-objects.md).
* **Spreadsheet formula injection (CWE-1236) is opt-in.** The guard is off by default (`ExcelMode = false`), so field values are written verbatim after RFC 4180 quoting unless you enable it; quoting alone does not neutralise a value whose first character is `=`, `+`, `-`, `@` or a leading tab or CR. If any exported value can come from untrusted input, set `ExcelMode = true` to prefix a single quote (or neutralise it yourself / disable formula evaluation in the consuming application).

## See also

* [JSON support across providers](14-json.md) — the per-provider SQL JSON surfaces (`ForJson`, `jsonb`, text JSON, `openjson`).
* [Projections](../querying/01-projections.md)
* [Streaming large objects (BLOB/CLOB)](26-large-objects.md)
* [Raw SQL](12-raw-sql.md)
* [API reference](../advanced/api-reference.md)
* [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs`, `src/nextorm.core/Query/QueryCommandExtensions.cs`, `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `src/nextorm.core/Query/Json/JsonStreamOptions.cs`, `src/nextorm.core/Query/CsvStreamOptions.cs`.
