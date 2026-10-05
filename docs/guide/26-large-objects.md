# Streaming large objects (BLOB/CLOB)

> Read a single large binary or text column as a `Stream`/`TextReader` without materialising the whole value in managed memory.

**Prerequisites:** [Querying and projections](../querying/index.md) · [Projections](../querying/01-projections.md) · [PostgreSQL](provider-specific/postgresql.md) · [SQL Server](provider-specific/sqlserver.md) · [Limitations](../advanced/limitations.md)

## Overview

By default a `byte[]`/`string` projection is materialised whole: the row reader calls `GetValue`/`GetString` and the entire value lands in a managed array or string. For a file, image or document column of a few megabytes that is an avoidable allocation and can exhaust memory. The LOB terminals open the provider's streaming accessors instead (`DbDataReader.GetStream`/`GetTextReader`) and hand back an object you read incrementally.

For a whole result set rather than a single LOB column, the JSON streaming terminal applies the same idea to rows: [`WriteJson`](28-streaming-data.md#json) / `WriteJsonAsync` write the query's `Select` projection to a caller-owned `Stream` with O(buffer) live memory and never close the destination. See [Streaming data to a Stream](28-streaming-data.md).

## Terminals

Sync overloads take the parameters as a `params ReadOnlySpan<object?>`:

```csharp
public static Stream ToStream(this QueryCommand<byte[]> command, params ReadOnlySpan<object?> parameters);
public static Stream ToStream(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters);

public static TextReader ToTextReader(this QueryCommand<string> command, params ReadOnlySpan<object?> parameters);
public static TextReader ToTextReader(this QueryCommand<string> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters);
```

Async overloads take the parameters as a `params object?[]` (opening is asynchronous; the `CancellationToken` overload also cancels the open):

```csharp
public static Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, params object?[] parameters);
public static Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params object?[] parameters);

public static Task<TextReader> ToTextReaderAsync(this QueryCommand<string> command, params object?[] parameters);
public static Task<TextReader> ToTextReaderAsync(this QueryCommand<string> command, CancellationToken cancellationToken, params object?[] parameters);
```

The terminals are extension methods on [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) — the type [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) returns — so the query must first project a single `byte[]` or `string` column. They are created with `storeInCache: false`: the LOB command is per-call and never reuses the shared, buffered command of the same query shape.

## Usage

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// BLOB
await using var stream = ctx.From<BinaryEntity>()
    .Where(x => x.Id == 1)
    .Select(x => x.Payload)
    .ToStream();                         // reader opened with CommandBehavior.SequentialAccess

var buffer = new byte[81920];
int read;
while ((read = await stream.ReadAsync(buffer)) > 0)
    await destination.WriteAsync(buffer.AsMemory(0, read));

// CLOB
using var reader = ctx.From<Document>()
    .Where(x => x.Id == 1)
    .Select(x => x.Body)
    .ToTextReader();

var chars = new char[8192];
int n;
while ((n = await reader.ReadAsync(chars, 0, chars.Length)) > 0)
    _ = chars[n];
```

The async form opens the command against the database; bring your own parameters through the trailing `params` list:

```csharp
Stream stream = await ctx.From<BinaryEntity>()
    .Where(x => x.Id == id)
    .Select(x => x.Payload)
    .ToStreamAsync(cancellationToken, id);
```

## Named columns and row streaming

The streaming accessors are also available on the named-column (`From("table")`) surface and can be
read per row of an `IAsyncEnumerable` projection. A `Stream`/`TextReader` member in the projection is
opened lazily from the sequential-access reader and is **valid only until the enumerator advances to
the next row** (`MoveNext`): the underlying reader is owned by the enumerator and its per-call
command, so the value must be fully consumed inside the loop body and never stored past the step.
Dispose each per-row stream/reader before the next iteration; this releases the provider's per-row
handle (required by SQLite, whose rowid locator keeps the statement pinned until the value is
released).

After `MoveNext` the handle **must not be used**: its behavior is undefined and provider-specific.
PostgreSQL may hand back the **next** row's bytes, SQL Server throws `ObjectDisposedException`, and
SQLite returns end-of-stream at the blob's end. The only guarantee is a safety property — a stale
handle never yields the watched row's data — so treat the value as invalid the moment the enumerator
advances and do not depend on any particular failure mode.

The named-column accessors are
[`TableAlias.GetStream`](xref:NextORM.Core.TableAlias.GetStream(System.String)) /
[`TableAlias.GetTextReader`](xref:NextORM.Core.TableAlias.GetTextReader(System.String)) and the
indexer members [`TableColumn.AsStream`](xref:NextORM.Core.TableColumn.AsStream) /
[`TableColumn.AsTextReader`](xref:NextORM.Core.TableColumn.AsTextReader). The streaming member must be
the **last** member of the projection, so the sequential-access reader reaches the LOB column after
every scalar column:

```csharp
await foreach (var row in ctx.From("documents")
    .Where(t => t.GetInt32("id") == id)
    .Select(t => new { Id = t.GetInt32("id"), Data = t.GetStream("data") })
    .ToAsyncEnumerable(cancellationToken))
{
    // The stream is valid only until the next MoveNext: read and release it before iterating.
    await using var data = row.Data;
    await data.CopyToAsync(destination, cancellationToken);
}
```

A row projection that contains a streaming member is prepared fresh with
`CommandBehavior.SequentialAccess` and is never written to the plan cache, so an ordinary buffered
projection of the same shape keeps its plan. On SQLite the `rowid` locator is appended to these rows
too, so the source must be a rowid-bearing table. A provider without sequential-access support
(MySQL/MariaDB, ClickHouse) throws `NotSupportedException` when the query is executed; use the
buffered `byte[]`/`string` projection there instead.

## Providers

| Provider | `ToStream` / `ToTextReader` |
|---|---|
| PostgreSQL | supported (`bytea` / `text`) |
| SQL Server | supported (`varbinary(max)` / `nvarchar(max)`; the streaming mode is `CommandBehavior.SequentialAccess`) |
| SQLite | supported (`blob` / `text`; the source must expose `rowid`) |
| MySQL / MariaDB | `NotSupportedException` |
| ClickHouse | `NotSupportedException` |
| In-memory | supported (`MemoryStream`/`StringReader` over the single materialized value; see [In-memory](#in-memory)) |

Streaming is implemented for **PostgreSQL, SQL Server and SQLite** in this release. The streaming terminal is opt-in: the ordinary buffered `byte[]`/`string` projection keeps working on every provider, and DML of a LOB is out of scope. MySQL/MariaDB and ClickHouse reject the terminal with a `NotSupportedException` whose message names the provider. The in-memory provider supports the scalar terminals as well, but with no `DbDataReader` to stream from it returns a plain BCL object over the materialized value — see [In-memory](#in-memory).

The multi-column [`ToDataReader`](#multiple-columns-todatareader) terminal is available on PostgreSQL and SQL Server. SQLite rejects it because its streaming projection always carries the `rowid` locator; MySQL/MariaDB and ClickHouse reject it because they have no sequential-access support; the in-memory provider rejects it because it has no `DbDataReader` at all:

| Provider | `ToDataReader` / `ToDataReaderAsync` |
|---|---|
| PostgreSQL | supported |
| SQL Server | supported |
| SQLite | `NotSupportedException` (rowid locator) |
| MySQL / MariaDB | `NotSupportedException` |
| ClickHouse | `NotSupportedException` |
| In-memory | `NotSupportedException` |

SQLite has no row locator of its own, so `Microsoft.Data.Sqlite` returns a true streaming `SqliteBlob` only when the query also selects `rowid`. The dialect appends its own trailing locator column — [`ISqlDialect.LobLocatorColumn`](xref:NextORM.Core.ISqlDialect.LobLocatorColumn), `"rowid"` on SQLite and `null` on PostgreSQL/SQL Server — so the payload stays at ordinal `0` and is not part of the user projection. The query source must therefore be a normal rowid table: on a `view` or a `WITHOUT ROWID` table the command fails closed with the raw `Microsoft.Data.Sqlite.SqliteException: no such column: rowid` — there is no buffered fallback.

### In-memory

[`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) has no `DbDataReader`, so the scalar LOB terminals do not open a provider reader. They query the in-memory source as usual and wrap the single projected value in a plain BCL object owned by the caller: `ToStream`/`ToStreamAsync` return a `MemoryStream` over the `byte[]`, and `ToTextReader`/`ToTextReaderAsync` return a `StringReader` over the `string`. The projection must still be exactly one `byte[]`/`string` column.

Only the first row is read. An empty result and a first value that is `NULL` both return `Stream.Null`/`TextReader.Null` — the in-memory scalar API does not distinguish "no row" from "row with a `NULL` LOB". Nothing on the context needs disposing and the context need not stay alive while the returned object is read: it is an ordinary `MemoryStream`/`StringReader` that owns no reader or command. Because the value is already materialized, there is no O(buffer) streaming benefit in memory — the whole value is in managed memory either way.

`ToDataReader`/`ToDataReaderAsync` remain unsupported on the in-memory provider (`NotSupportedException`): there is no `DbDataReader` to hand back.

### SQLite query shapes

The SQLite locator can only be appended to a **single-source, rowid-bearing** projection. Queries that join several sources, or that carry `DISTINCT`/`DISTINCT ON`, `UNION`, `GROUP BY` or an aggregate, are **not supported**; they may surface a SQLite driver error instead of a clean early failure. On a single-column projection a raw [`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) override is rejected with `NotSupportedException`, because a `rowid` locator cannot be added to caller-supplied SQL safely; a raw projection of two or more columns is rejected with the usual `InvalidOperationException`.

## Exactly one column

The terminal reads column ordinal `0` and requires **exactly one** column: zero or several columns throw `InvalidOperationException`. The single column must also be a valid `byte[]`/`string`; when it is of the wrong type the error surfaces from the provider's `GetStream`/`GetTextReader` (the exact exception type depends on the driver). To read several columns — or several rows — through a `DbDataReader`, use the [`ToDataReader`](#multiple-columns-todatareader) escape hatch below.

## Multiple columns: `ToDataReader`

When the projection has more than one column, or you need every row without materialising the result, use the `ToDataReader`/`ToDataReaderAsync` terminals. They return a caller-owned [`DbDataReader`](https://learn.microsoft.com/dotnet/api/system.data.common.dbdatareader) over the same sequential-access command:

```csharp
public static DbDataReader ToDataReader<TResult>(this QueryCommand<TResult> command, params ReadOnlySpan<object?> parameters);
public static DbDataReader ToDataReader<TResult>(this QueryCommand<TResult> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters);

public static Task<DbDataReader> ToDataReaderAsync<TResult>(this QueryCommand<TResult> command, params object?[] parameters);
public static Task<DbDataReader> ToDataReaderAsync<TResult>(this QueryCommand<TResult> command, CancellationToken cancellationToken, params object?[] parameters);
```

```csharp
using var reader = ctx.From<Document>()
    .Where(x => x.Id == id)
    .Select(x => new { x.Id, x.Body })
    .ToDataReader(id);

while (reader.Read())
{
    var id2 = reader.GetInt64(0);
    var body = reader.IsDBNull(1) ? null : reader.GetString(1);
}
```

The terminal does **not** apply the single-column guard: it hands the provider reader over as-is, so the [streaming projection contract](#ownership-and-disposal) still applies:

* **Forward-only, sequential access.** Read columns in ascending ordinal order and do not read a column twice; a LOB column must be read before any later column.
* **Ownership.** The returned reader owns the underlying `DbDataReader` and the per-call `DbCommand`; dispose it (`await using` on the async path) to release both. The context stays alive and usable.
* **Cancellation.** The token cancels opening the reader; it is also linked into `Read`/`ReadAsync` and `NextResult`/`NextResultAsync`.

`ToDataReader` is **not supported on SQLite** because the SQLite streaming projection always appends the `rowid` locator, so the terminal would expose a column the caller never projected; it fails closed with `NotSupportedException`. Use `ToStream`/`ToTextReader` for a single LOB column on SQLite. MySQL/MariaDB and ClickHouse reject the terminal because they have no sequential-access support, and the in-memory provider rejects it because it has no `DbDataReader`.

## `SequentialAccess` and the plan cache

The command is opened with [`CommandBehavior.SequentialAccess`](https://learn.microsoft.com/dotnet/api/system.data.commandbehavior), which tells the provider to hand the LOB bytes over as they are read rather than buffering the value. The streaming terminal prepares a **fresh per-call command** with `storeInCache: false`, so it **never touches the plan cache** — it neither reads nor writes an entry. A buffered query of the same SQL shape keeps its ordinary plan (without `SequentialAccess`) and does not reuse the LOB command. The observable guarantee is the same — the buffered and streaming forms never share a plan — but the mechanism is the per-call preparation, not a discriminator in the plan-cache key.

## Ownership and disposal

The returned `Stream`/`TextReader` **owns** the underlying `DbDataReader` and a per-call `DbCommand`. Disposing the stream (preferably with `await using` on the async path) releases both and returns the connection to the pool; the [`DataContext`](xref:NextORM.Core.DataContext) itself is **not** closed and can serve further queries. The context must stay alive while the stream is read: opening a terminal when the context is already disposed throws `ObjectDisposedException`.

On the in-memory provider none of this applies: the returned `MemoryStream`/`StringReader` owns no reader or command, so disposing it releases nothing on the context, and the context need not stay alive while the object is read (opening the terminal on an already-disposed context still throws `ObjectDisposedException`).

## Memory and async

Memory use is O(buffer): only the chunk you request is materialised, never the whole value. `ToStreamAsync`/`ToTextReaderAsync` open the reader and command asynchronously, but the provider getter itself (`GetStream`/`GetTextReader`) is synchronous — so the asynchronous part is the reading you drive through `Stream.ReadAsync`/`TextReader.ReadAsync` on the returned object. Cancellation is honoured while opening and surfaces as an exception without leaking the reader or the command.

On the in-memory provider the value is materialised before the terminal wraps it, so memory is O(value), not O(buffer), and `ReadAsync` on the returned `MemoryStream`/`StringReader` is the ordinary BCL async surface with no provider round-trip.

## See also

* [Projections](../querying/01-projections.md)
* [Streaming data to a Stream](28-streaming-data.md)
* [Raw SQL](12-raw-sql.md)
* [PostgreSQL provider](../providers/postgres.md) · [SQL Server provider](../providers/sqlserver.md)
* [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs`, `src/nextorm.core/DataContext/CommandReaderOwner.cs`,
`src/nextorm.core/DataContext/LobDataReader.cs`,
`tests/nextorm.integration.tests/CommonTestSuite.Lob.cs` and the SQL-generation/dialect tests for `SupportsSequentialAccess`.
