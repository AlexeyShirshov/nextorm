# Streaming query results to CSV

> Write a `Select` result to a caller-owned `Stream` as RFC 4180 CSV, row by row, without materialising the result set.

**Prerequisites:** [Querying and projections](../querying/index.md) · [Projections](../querying/01-projections.md) · [Streaming large objects (BLOB/CLOB)](26-large-objects.md) · [Limitations](../advanced/limitations.md)

## Overview

`WriteCsv`/`WriteCsvAsync` turn a query into CSV and write it straight to a `Stream` you supply. Rows are pulled from the database reader and formatted one at a time: the result set is **not** materialised into a list, no `TResult` instance is constructed for a row, and the per-row path reads every supported column without boxing. That includes SQL Server numeric columns: the CSV terminal reads them through the provider's storage-typed getter (the `MapTypedColumnExpression` hook, which receives the reader's actual field type) and converts them with a typed `Convert.To<T>` — no `GetValue` and no `Convert.ChangeType(object)`. A column whose storage type is unknown or cannot be converted is rejected with `NotSupportedException` before any output. Memory use is O(row), so a query over millions of rows can be served to a file or an HTTP response without buffering it; binary (`byte[]`) columns are the exception — each is read and Base64-encoded whole-field per row, so memory is bounded by the largest field/row rather than a fixed buffer (see [Limitations](#limitations)).

This is the streaming terminal for **tabular** output. The single-`byte[]`/`string` [LOB terminals](26-large-objects.md) stream one large value; the CSV terminals stream every column and row of the projection.

## Terminals

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

## Usage

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

## Options

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

### NULL, empty values and the marker

* **SQL NULL → `NullMarker` verbatim.** A SQL `NULL`/`DBNull` is written as `NullMarker` exactly, unquoted, and never passed through `ValueTransform`.
* **Empty string → empty field.** A non-`NULL` empty string (or an empty `byte[]`) is written as an empty field, so a reader can tell a missing value from an empty one.
* **Marker-equal text is force-quoted.** A non-`NULL` value whose formatted text equals `NullMarker` exactly is wrapped in `"` (`"\N"`) so it can never be read back as NULL.
* **The marker is validated before any output.** `NullMarker` must be non-empty and must not contain the `Delimiter`, a `"`, CR or LF; a violating value is rejected before the header (and any data row) is written.

### Excel guard

With `ExcelMode = true`, a field whose formatted text begins with `=`, `+`, `-` or `@` is prefixed with an apostrophe (`'`) so spreadsheet applications treat it as text rather than a formula. The guard runs **after** `ValueTransform` and **before** RFC 4180 escaping, so the apostrophe becomes part of the escaped field (it ends up inside the quotes) and a transform cannot smuggle a formula past it. It applies to the formatted text of every column, including negative numeric values.

### Value transform

`ValueTransform` receives every non-`NULL` value (boxed) and returns the text that replaces the default formatting, or `null` to write the NULL marker. It runs before the Excel guard and before CSV escaping; a SQL NULL never invokes it. The default path (no transform) stays box-free — boxing happens only when `ValueTransform` is set.

## Dialect

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

## Providers

| Provider | `WriteCsv` / `WriteCsvAsync` |
|---|---|
| PostgreSQL | supported |
| SQL Server | supported |
| SQLite | supported |
| MySQL / MariaDB | supported |
| ClickHouse | supported |
| In-memory | `NotSupportedException` |

Unlike the LOB terminals, CSV export needs no sequential-access support and no `rowid` locator, so it works against every relational provider. The in-memory provider has no `DbDataReader` to stream from and rejects the terminal with `NotSupportedException`, exactly like `ToDataReader`; there is no buffered fallback in memory — materialise the query and write the CSV yourself if you need that.

## Writing to an HTTP response or a file

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

## Ownership, cancellation and errors

* **Stream ownership.** The destination `Stream` is never closed by the terminal — on success, cancellation or error. Dispose it yourself when you opened it.
* **Reader/command ownership.** The terminal opens a per-call command and reader and releases both when it returns (or throws); the [`DataContext`](xref:NextORM.Core.DataContext) stays alive and usable.
* **Cancellation.** The token is honoured while reading rows and, on the async path, while writing to the destination; a cancelled export throws `OperationCanceledException` without closing the destination stream.
* **Write errors.** An exception from the destination stream (a broken socket, a full disk) propagates after the reader and command are released; the destination is still left open.

## Limitations

* **In-memory context — `NotSupportedException`.** There is no `DbDataReader`, so the terminal fails closed instead of buffering; see [Providers](#providers).
* **Lazy temp-table sources — `NotSupportedException` before any output.** A query that reads a source created with [`AsTempTable`](18-create-table-as.md#lazy-temporary-tables-astemptable) is not a single statement: it needs a `DROP` + `CREATE TEMPORARY TABLE ... AS SELECT` + read batch on one session, which the CSV terminal cannot stream. The terminal fails closed with `NotSupportedException` before the header (and any data row) is written and never runs the batch, so the destination is never left with a partial file; materialise the query first (for example `ToList`/`ToListAsync`) and write the rows yourself.
* **Unsupported projections — rejected before any row is written.** A projection column that has no single scalar CSV form, or whose storage type is unknown or cannot be converted, is rejected up front: a nested entity or collection, an array other than `byte[]`, a tuple, a dictionary, an arbitrary object, and a column whose mapping needs an object-based converter or a `GetValue` fallback. The failure happens before the header is written, so the destination is never left with a partial file.
* **Binary (`byte[]`) fields are read whole-field — memory is bounded by the largest field/row.** A `byte[]` column is materialised with a typed `GetFieldValue<byte[]>` and then Base64-encoded for the row (the encoder itself runs in 3-byte chunks), so peak memory is not a fixed buffer: exporting rows with very large BLOBs holds at least one full field in memory at a time. Chunked LOB streaming — writing a binary field chunk by chunk from a sequential-access reader — is a future slice (trigger: demand for exporting large binary columns). To stream a single large value with O(buffer) memory today, use the [single-`byte[]`/`string` LOB terminals](26-large-objects.md).
* **Spreadsheet formula injection (CWE-1236) is opt-in.** The guard is off by default (`ExcelMode = false`), so field values are written verbatim after RFC 4180 quoting unless you enable it; quoting alone does not neutralise a value whose first character is `=`, `+`, `-`, `@` or a leading tab or CR. If any exported value can come from untrusted input, set `ExcelMode = true` to prefix a single quote (or neutralise it yourself / disable formula evaluation in the consuming application).

## See also

* [Projections](../querying/01-projections.md)
* [Streaming large objects (BLOB/CLOB)](26-large-objects.md)
* [Raw SQL](12-raw-sql.md)
* [API reference](../advanced/api-reference.md)
* [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs`, `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `src/nextorm.core/Query/CsvStreamOptions.cs`.
