# Bulk insert

`CreateBulkInsertBuilder<TEntity>()` writes a whole set in one explicit call, without change tracking. Where the
provider has a native bulk API it can be used — PostgreSQL binary `COPY`, SQL Server `SqlBulkCopy`,
MySQL/MariaDB `MySqlBulkCopy` and ClickHouse `InsertBinaryAsync` — otherwise the set is written as a
parameterised `INSERT ... VALUES`, optionally chunked. Which path runs is chosen by the
[execution mode](#execution-modes), not by the provider alone.

Bulk insert works over a normal mapping; see [Data modification (INSERT)](17-insert-statement.md) for
declaring keys, identity and computed columns.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

[SqlTable("orders")]
public interface IOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    int Id { get; set; }
    [Column("customer_id")]
    int CustomerId { get; set; }
    [Column("total")]
    decimal Total { get; set; }
}

public sealed class Order : IOrder
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
}
```

## Writing a set

Identity and computed columns are excluded automatically, so you only supply the writable ones.

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

var orders = new[]
{
    new Order { CustomerId = 1, Total = 10m },
    new Order { CustomerId = 2, Total = 20m },
    new Order { CustomerId = 3, Total = 30m },
};

// Auto on PostgreSQL takes native COPY; on the other providers the path follows the supplied options
var written = ctx.CreateBulkInsertBuilder<IOrder>().Values(orders).BulkInsert();
// written == 3
```

An `IAsyncEnumerable<TEntity>` source is also accepted and streamed — see
[Streaming sources](#streaming-sources). An empty source writes nothing and returns `0`.

Global query filters are not injected into the bulk write; the rows are validated against the target's
active filters before they are written (`QueryFilterException` on a violation). A synchronous source is
validated in full before the first batch; an async source is validated as it streams, so a later
violation can leave earlier rows written. See
[INSERT and MERGE (validation)](../advanced/query-filters.md#insert-and-merge-validation).

## Execution modes

[`BulkInsertMode`](xref:NextORM.Core.BulkInsertMode) selects the path. [`Mode`](xref:NextORM.Core.BulkInsertOptions.Mode)
defaults to `Auto` on every provider:

| Mode | Behaviour |
|---|---|
| `Auto` (default) | Prefers the provider's native bulk path when it can honour every supplied option and every preflight prerequisite; otherwise it takes the portable `INSERT ... VALUES` path, and only when that path can honour the request too. |
| `Native` | Uses the provider's native bulk path and never falls back: a missing prerequisite, an option the native path cannot express or an unsupported destination throws `NotSupportedException` naming the reason, before the source is read or a row is written. |
| `Portable` | Explicitly selects the parameterised `INSERT ... VALUES` path. A native-only option is refused by name instead of being ignored, and the write never becomes native implicitly. |

```csharp
// keep the SQL-batch path even where a native load is available
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.Mode(BulkInsertMode.Portable))
    .Values(orders)
    .BulkInsert();
```

`Returning*` always writes through the portable path, because the native bulk APIs cannot return rows; a
`Mode.Native` request combined with a returning terminal is refused instead of being silently downgraded.

> **Compatibility:** Auto may switch existing MySQL/MariaDB/ClickHouse calls to native; Portable restores SQL-batch selection. Previously ignored unsupported options now validate strictly.

## Streaming sources

`Values` accepts two source shapes, and only one of them streams:

| Source | Read | Terminal | Buffered before the first write |
|---|---|---|---|
| `IEnumerable<TEntity>` | once, synchronously | `BulkInsert()` or `BulkInsertAsync()` | yes — the whole set is materialised and validated up front |
| `IAsyncEnumerable<TEntity>` | once, asynchronously | `BulkInsertAsync()` only | no — rows are pulled as they are written |

A source streams only when you pass an `IAsyncEnumerable<TEntity>` **and** finish with
`BulkInsertAsync()`:

```csharp
// any IAsyncEnumerable<IOrder> — a Channel reader, an async paging loop, EF's AsAsyncEnumerable(), ...
IAsyncEnumerable<IOrder> ordersStream = ...;

await ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

The stream is always a sequence of mapped entities — there is no overload for a `DbDataReader`, a byte
`Stream` or raw value arrays, so project whatever you read into `TEntity` and yield it. A synchronous
source (an array, a `List<T>`, a LINQ query) is *buffered*: `BulkInsert()`/`BulkInsertAsync()`
materialise the whole set, and run [global-filter validation](../advanced/query-filters.md#insert-and-merge-validation)
over every row, before the first statement is sent, so it must fit in memory. Passing the async source
to the synchronous `BulkInsert()` — or to `ToSql()` — throws `InvalidOperationException`; use
`BulkInsertAsync()`.

How far the stream reaches the database depends on the path:

| Path | Streams row by row | Buffer |
|---|---|---|
| PostgreSQL native `COPY` | yes — each row is written as it is read, the set is never buffered | none; a chunk limit starts a new importer per bounded chunk |
| SQL Server `SqlBulkCopy` | no — the set is buffered in a `DataTable` first, because `SqlBulkCopy` needs column type metadata | the whole set; `MaxBatchSize` sets the driver's `BatchSize` but does not split the buffer |
| MySQL/MariaDB native `MySqlBulkCopy` | per chunk — the source is read in bounded chunks and each chunk is loaded once | one chunk (`MaxBatchSize`, default 10 000 rows) |
| ClickHouse native `InsertBinaryAsync` | per chunk — the adapter pulls a chunk, awaits all of its driver calls, then pulls the next one | one chunk (`MaxBatchSize`, default 10 000 rows) |
| Portable `INSERT ... VALUES` | per batch — rows fill a buffer up to a chunk limit or the end, then the batch is written and the buffer reused | sized by `MaxBatchSize`/`MaxParameters`/`MaxSqlLength` |

Only the portable path sizes its buffer — see
[Bounding the batch and reporting progress](#bounding-the-batch-and-reporting-progress) for the three
limits, their defaults and the progress callback.

`Returning*` accepts either source: read it with `ToList()` (sync source) or `ToListAsync()` (async
source). On the streaming path an entity is validated as it is projected, so a later row that violates a
global query filter can leave the earlier batches already committed — a synchronous source is validated
in full first. nextorm reads the source exactly once and never disposes it. See
[INSERT and MERGE (validation)](../advanced/query-filters.md#insert-and-merge-validation).

## Retargeting the destination table

`Table(name)` / `Table(schema, name)` overrides the entity's mapped table for one write, so a mapped
shape can be copied into a differently named table without a second `[SqlTable]` mapping:

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.Table("archive", "orders_2024"))
    .Values(orders)
    .BulkInsert();
```

The override has priority over the `[SqlTable]`/`Table(...)` mapping, the naming convention is not
applied to it, and the optional schema is quoted separately from the table. It works on both the native
and the portable `INSERT ... VALUES` paths.

### Destination parts

The table, schema and database can also be addressed independently, without restating the table:
`Schema(name)`, `Database(name)` and `Server(name)` set `SchemaName`, `DatabaseName` and `ServerName`.

```csharp
// keep the mapped table, override only its schema
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.Schema("archive"))
    .Values(orders)
    .BulkInsert();
```

- A **schema-only** request keeps the mapped (or `Table(...)`-overridden) table and changes only the schema part.
- `TableSchema` and `SchemaName` are two aliases of one part: equal values collapse into that part, and
  disagreeing values are an **argument error** raised before any row is read.
- Every part is quoted separately, so a qualified name never becomes a single identifier containing dots.
- **`DatabaseName` is dialect-scoped.** PostgreSQL addresses only the connection's current database — naming
  it is a no-op, naming another one is refused by name. MySQL/MariaDB and ClickHouse address another database
  as a qualification, and SQL Server renders it as part of the multipart name. A dialect that cannot express
  another database refuses the request instead of dropping the part.
- **`ServerName` is not available and is refused**: a bulk insert never reroutes to another server. Use a
  connection that targets the required server. No rerouting is implemented and none is promised.

## Local temporary targets

`TargetKind(BulkInsertTargetKind.LocalTemporary)` writes to a temporary table that already exists on the
context's own connection and, where the provider has one, the same session. nextorm never creates or drops
the table and opens no implicit transaction.

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .TargetKind(BulkInsertTargetKind.LocalTemporary)
        .Table("#orders_stage"))
    .Values(orders)
    .BulkInsert();
```

The temporary table lives in the current session's temporary store, so a request that contradicts that is
refused by name: `DatabaseName` with a local temporary target, a schema qualifier on SQL Server (`#temp`
takes none), a sessionless ClickHouse insert (`UseSession=false`) and a ClickHouse parallelism above one.
Creating, altering or dropping a temporary table is out of scope.

## Reading the generated keys

The native bulk APIs cannot return rows, so `Returning*` switches to the portable path and writes the
set as a batch of returning `INSERT ... VALUES ... RETURNING`/`OUTPUT` statements. Use
`ReturningKey<TKey>()` for the key column or `Returning(projection)` for any mapped projection; both
terminals are `ToList()` / `ToListAsync()`.

```csharp
// keys only — ReturningKey<TKey>() reads the key column declared in metadata
IReadOnlyList<int> ids = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .ReturningKey<int>()
    .ToList();
// ids = [1, 2, 3]

// a projection per written row
var rows = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .Returning(o => new { o.Id, o.CustomerId })
    .ToList();
// rows = [ { Id = 1, CustomerId = 1 }, { Id = 2, CustomerId = 2 }, ... ]

// a single scalar projection
IReadOnlyList<int> customerIds = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .Returning(o => o.CustomerId)
    .ToList();

// async
IReadOnlyList<int> keys = await ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(ordersStream)
    .ReturningKey<int>()
    .ToListAsync(cancellationToken);
```

> **The result order is not guaranteed** to match the order of the source (neither `RETURNING` nor
> `OUTPUT` promises it). Correlate rows by a business key, not by position:

```csharp
var idByCustomer = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .Returning(o => new { o.CustomerId, o.Id })
    .ToList()
    .ToDictionary(o => o.CustomerId, o => o.Id);

var orderIdForCustomer2 = idByCustomer[2];
```

`Returning*` requires a provider with `RETURNING`/`OUTPUT`: PostgreSQL and SQLite 3.35+ (via
`RETURNING`) and SQL Server (via `OUTPUT`). MySQL, MariaDB and ClickHouse reject it on the bulk path with a
clear `NotSupportedException` — read the keys with a plain `CreateInsertBuilder` there. MariaDB's
version-gated `RETURNING` (explicitly configured 13.0+) applies to a single-table `UPDATE`, not to the bulk
`INSERT`; a bulk `Returning*` stays refused there.

## Bounding the batch and reporting progress

The portable path sends the whole set as one statement by default. Chunk limits and progress are write
options, passed to `CreateBulkInsertBuilder<TEntity>` — either as a `BulkInsertOptions` record or through the
fluent `BulkInsertOptionsBuilder` callback:

```csharp
var written = 0;

await ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .MaxBatchSize(1_000)                                 // rows per statement
        .MaxParameters(20_000)                               // bound parameters per statement
        .NotifyAfter(10_000, (total, _) => written = total)) // called with the cumulative count
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

Chunks are written as separate `INSERT` statements; nextorm does **not** wrap them in a transaction — if
you need atomicity, start a transaction yourself. Every write is awaited before the next chunk is read, and
nextorm disposes only the resources it created, never the source, the connection or a caller-owned object.

The `NotifyAfter` callback runs inline and only **after a batch has completed** (a written batch on the
portable path, a completed load chunk on MySQL/MariaDB/ClickHouse, a progress tick from
`SqlBulkCopy.NotifyAfter` on SQL Server, a completed chunk/importer on PostgreSQL). It is never called
per row and never ahead of the write. Keep it fast and non-throwing: a callback that throws propagates the
exception and leaves the already-written rows in place, and a callback that blocks stalls the call until it
returns. nextorm makes no rollback, throughput or per-row-commit promise.

Its second argument is a `CancellationToken`. Supply the source with `ProgressCancellationTokenSource` to
let a cooperative callback stop early — the writer throws `OperationCanceledException` once the in-flight
callback returns:

```csharp
using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .ProgressCancellationTokenSource(budget)
        .NotifyAfter(10_000, (total, token) => Report(total, token)))
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

> The token cannot pre-empt a callback that ignores it (the callback runs inline on the writing thread);
> it only lets a callback that honours it stop early. nextorm never cancels or disposes the source.

## Timeout

`Timeout(TimeSpan)` sets **one** budget for the whole invocation, measured from execution entry. It spans
connection acquisition, setup, source enumeration, every batch, the driver work and the progress callbacks;
it is never reset per batch, so a slow source and a slow driver share one deadline.

```csharp
var written = await ctx.CreateBulkInsertBuilder<IOrder>(o => o.Timeout(TimeSpan.FromMinutes(2)))
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

- A budget of zero or less is an argument error.
- An expired budget is reported as `TimeoutException`.
- The caller's `CancellationToken` always wins: a user cancellation surfaces as `OperationCanceledException`,
  never as a timeout, even when both race.
- `Timeout` is the only timeout surface: the former `TimeoutSeconds` option and the `Timeout(int)` builder
  method are **removed without shims**, so migrate by passing a `TimeSpan`
  (`Timeout(TimeSpan.FromSeconds(30))`). The budget now applies to every path, not only to the SQL Server
  native one, and the old PostgreSQL "timeout is not supported" limitation no longer exists.

## Explicit identity values and skipping duplicates

`KeepIdentity` writes the identity values carried by the entities instead of letting the server generate
them. `IgnoreDuplicates` skips rows that would violate a unique constraint instead of failing the whole
write.

```csharp
// explicit ids
var pinned = new[]
{
    new Order { Id = 9001, CustomerId = 1, Total = 5m },
    new Order { Id = 9002, CustomerId = 2, Total = 6m },
};
ctx.CreateBulkInsertBuilder<IOrder>(o => o.KeepIdentity()).Values(pinned).BulkInsert();

// insert a batch that repeats 9001: the duplicate is skipped, the other rows are written
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.KeepIdentity().IgnoreDuplicates())
    .Values([
        new Order { Id = 9001, CustomerId = 1, Total = 5m },   // conflicts -> skipped
        new Order { Id = 9003, CustomerId = 3, Total = 7m },   // written
    ])
    .BulkInsert();
// written == 1
```

`KeepIdentity` is honoured per provider, on the path that can express it:

| Provider | `KeepIdentity=true` |
|---|---|
| SQL Server | native `SqlBulkCopyOptions.KeepIdentity`; the portable statement is wrapped with `SET IDENTITY_INSERT ... ON/OFF` |
| PostgreSQL | native `COPY` with the explicit identity column in its column list; the portable statement adds `OVERRIDING SYSTEM VALUE` |
| MySQL / MariaDB | the explicit identity column on both paths, under a scoped `NO_AUTO_VALUE_ON_ZERO` guard, so an explicit `0` is written instead of being replaced by a generated value |
| ClickHouse | the explicit value in the native insert; only ordinary and `DEFAULT` columns are writable — a mapped ALIAS or MATERIALIZED column is refused by name |
| SQLite | the explicit identity column in the portable statement |

`IgnoreDuplicates` uses whichever form the resolved path has:

| Provider | `IgnoreDuplicates=true` |
|---|---|
| SQL Server | refused by name (no ignore form on either path) |
| PostgreSQL | `ON CONFLICT DO NOTHING` (portable) |
| SQLite | `INSERT OR IGNORE` (portable) |
| MySQL / MariaDB | `INSERT IGNORE` (portable) or `ConflictOption.Ignore` on the native load |
| ClickHouse | refused by name: there is no uniqueness to skip, and a plain insert would write every row |

> `KeepIdentity()` has **no effect** when the entity's mapping declares no identity column: explicit values
> are still written for non-identity keys, but the provider's identity-insert form (`SET IDENTITY_INSERT`,
> `OVERRIDING SYSTEM VALUE`, the MySQL zero-mode guard) is not emitted, so a non-identity table never fails
> with SQL Server error 8106, and the path decision is unchanged. The check uses the donor entity's metadata,
> not the actual target table — when `Table(...)` retargets a write, the caller owns the target's identity
> shape.

## SQL Server bulk-copy options

The native `SqlBulkCopy` path exposes four flags that the PostgreSQL `COPY` path and the portable
`INSERT ... VALUES` path cannot express. They are off by default, matching `SqlBulkCopyOptions.Default`:

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .TableLock()          // SqlBulkCopyOptions.TableLock
        .CheckConstraints()   // SqlBulkCopyOptions.CheckConstraints
        .KeepNulls()          // SqlBulkCopyOptions.KeepNulls
        .FireTriggers())      // SqlBulkCopyOptions.FireTriggers
    .Values(orders)
    .BulkInsert();
```

| Option | Builder method | Effect |
|---|---|---|
| `CheckConstraints` | `CheckConstraints()` | check CHECK / FOREIGN KEY constraints during the copy (SQL Server ignores them by default) |
| `TableLock` | `TableLock()` | take a table-level bulk-update lock for the duration of the copy |
| `KeepNulls` | `KeepNulls()` | write explicit `NULL`s instead of the destination column's `DEFAULT` |
| `FireTriggers` | `FireTriggers()` | fire `INSERT` triggers for the copied rows |

`null` and `false` are equivalent: the flag is not set and the write keeps its current behavior. Requesting
a flag on a path that cannot express it throws a `NotSupportedException` naming every offending flag instead
of silently ignoring them — PostgreSQL `COPY`, MySQL/MariaDB, ClickHouse and the portable
`INSERT ... VALUES` path all refuse `TableLock=true` and `FireTriggers=true`, and `CheckConstraints=false` is
accepted only by SQL Server (native), MariaDB and SQLite (scoped session controls).

`KeepNulls=false` means "let the destination DEFAULT apply instead of an explicit NULL". It is accepted only
for a **default-equivalent shape** — every written column is nullable and its SQL default is
NULL or absent — and is refused by name otherwise, on every path, so it is never accepted and silently
ignored.

### `SqlServer(SqlServerBulkInsertOptions)`

SQL Server-specific native options live in a typed group, so they cannot be mistaken for portable ones:

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.SqlServer(new SqlServerBulkInsertOptions
    {
        UseInternalTransaction = true,
    }))
    .Values(orders)
    .BulkInsert();
```

| Field | Effect |
|---|---|
| `UseInternalTransaction` | `SqlBulkCopy` runs its own transaction around the **driver batch** (`SqlBulkCopyOptions.UseInternalTransaction`). An unset/`false` value requests no flag. It is not a whole-load guarantee: with several driver batches only the current batch is covered. Combining it with an external context transaction is refused by name before the source is read. The portable path refuses `true`. |

An empty group (every value `null`) creates no request. A supplied field of the *other* provider's group is
rejected when the target provider is known — including an explicit `false`. A default-equivalent value on its
own provider is accepted by its **result**, not by the presence of a driver flag.

### `ClickHouse(ClickHouseBulkInsertOptions)`

ClickHouse native options are a second typed group, applied to `InsertBinaryAsync`:

```csharp
var written = await ctx.CreateBulkInsertBuilder<IOrder>(o => o.ClickHouse(new ClickHouseBulkInsertOptions
    {
        MaxDegreeOfParallelism = 4,
    }))
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

| Field | Effect |
|---|---|
| `MaxDegreeOfParallelism` | The parallelism the native insert requests (`null` preserves the configured default). A session and a parallelism above one are contradictory and refused by name. The portable statement writes a single stream, so it accepts `null`/`1` as the same result and refuses a value above one by name. A non-positive value is an argument error on every path. |
| `UseSession` | `null` preserves the configured session, `true` requires an actual session id, and `false` explicitly requests a sessionless non-temporary insert. On the portable path the request is honoured only when the context connection already carries that session, and refused by name otherwise. |

An empty group creates no request. The group is refused when it is supplied to another provider.

`ClickHouse.Driver` 1.4.0's `ClickHouseBulkCopy` class is **not** obsolete — only its `InitAsync` method is —
so the class is not the reason nextorm uses `InsertBinaryAsync`: the binary insert API is what exposes the
per-insert settings copy and the session/parallelism contract above, and it runs through the context's own
client.

## Inspecting the SQL

`ToSql()` renders the parameterised SQL the portable path would execute for the first batch, without
opening a connection:

```csharp
var sql = ctx.CreateBulkInsertBuilder<IOrder>().Values(orders).ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), (@p2, @p3), (@p4, @p5)

var returningSql = ctx.CreateBulkInsertBuilder<IOrder>().Values(orders).ReturningKey<int>().ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), ... returning id
```

## Options

Write options are passed to `CreateBulkInsertBuilder<TEntity>` — as a `BulkInsertOptions` record or, more
concisely, through the `BulkInsertOptionsBuilder` callback:

```csharp
ctx.CreateBulkInsertBuilder<IOrder>(new BulkInsertOptions { MaxBatchSize = 1_000, IgnoreDuplicates = true });
ctx.CreateBulkInsertBuilder<IOrder>(o => o.MaxBatchSize(1_000).IgnoreDuplicates());
```

| Option | Builder method | Effect |
|---|---|---|
| `Mode` | `Mode(BulkInsertMode)` | execution path: `Auto` (default), `Native`, `Portable` |
| `TargetKind` | `TargetKind(BulkInsertTargetKind)` | `Regular` (default) or `LocalTemporary` |
| `MaxBatchSize` | `MaxBatchSize(rows)` | rows per statement/batch on the portable path; a chunk size on the native MySQL/MariaDB/ClickHouse paths; `SqlBulkCopy.BatchSize` on SQL Server; a new importer per chunk on PostgreSQL |
| `MaxParameters` | `MaxParameters(count)` | bound parameters per statement (portable path) |
| `MaxSqlLength` | `MaxSqlLength(chars)` | approximate SQL length per statement (portable path) |
| `TableName` / `TableSchema` | `Table(name)` / `Table(schema, name)` | override the mapped destination table, optionally schema-qualified |
| `SchemaName` | `Schema(schema)` | override only the schema part, keeping the mapped/explicit table |
| `DatabaseName` | `Database(database)` | the database part where the dialect can address another database |
| `ServerName` | `Server(server)` | **refused**: a bulk insert never reroutes to another server |
| `KeepIdentity` | `KeepIdentity()` | write explicit identity values on the path that can express them; no effect when the entity has no identity column |
| `IgnoreDuplicates` | `IgnoreDuplicates()` | skip rows that violate a unique constraint where an ignore form exists |
| `CheckConstraints` | `CheckConstraints()` | check CHECK/FOREIGN KEY constraints; `false` only where a scoped control exists |
| `TableLock` | `TableLock()` | take a table-level bulk-update lock; SQL Server native only |
| `KeepNulls` | `KeepNulls()` | `true`: write explicit `NULL`s; `false`: default-equivalent shapes only |
| `FireTriggers` | `FireTriggers()` | fire `INSERT` triggers; SQL Server native only |
| `Timeout` | `Timeout(TimeSpan)` | one whole-invocation budget for every path |
| `SqlServer` | `SqlServer(SqlServerBulkInsertOptions)` | SQL Server native group (`UseInternalTransaction`) |
| `ClickHouse` | `ClickHouse(ClickHouseBulkInsertOptions)` | ClickHouse native group (`MaxDegreeOfParallelism`, `UseSession`) |
| `Progress` / `NotifyEvery` | `NotifyAfter(rows, onRows)` | progress with the cumulative written-row count, after completed batches |
| `ProgressCancellationTokenSource` | `ProgressCancellationTokenSource(source)` | token passed to the progress callback |

An option that the resolved provider/path cannot honour is refused with a `NotSupportedException` that names
it, before the source is read — no option is accepted and silently dropped.

On the returned builder:

| Method | Effect |
|---|---|
| `Values(IEnumerable<TEntity>)` / `Values(IAsyncEnumerable<TEntity>)` | the set, read once; an empty set writes nothing and returns `0` |
| `ReturningKey<TKey>()` / `Returning(projection)` | materialise the keys/projection of the written rows via `RETURNING`/`OUTPUT` |
| `BulkInsert()` / `BulkInsertAsync(ct)` | write the set and return the number of rows written |
| `ToSql()` | render the first batch's SQL without executing |

## Provider support

| Provider | `Auto`/`Native` path | Target qualification | `Returning` | `IgnoreDuplicates` | `KeepIdentity` | Bulk-copy flags |
|---|---|---|---|---|---|---|
| PostgreSQL | binary `COPY` | `schema.table`; `DatabaseName` = the current database only | `RETURNING` (portable) | `ON CONFLICT DO NOTHING` | `OVERRIDING SYSTEM VALUE` (portable) / explicit `COPY` column list | — (rejected) |
| SQL Server | `SqlBulkCopy` | `database.schema.table` | `OUTPUT` (portable) | — (refused) | `SqlBulkCopyOptions.KeepIdentity` / `SET IDENTITY_INSERT` | `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` |
| MySQL | `MySqlBulkCopy` (`LOAD DATA LOCAL INFILE`) | `db.table` | — (refused) | `ConflictOption.Ignore` / `INSERT IGNORE` | explicit column + zero-mode guard | — (rejected) |
| MariaDB | `MySqlBulkCopy` (`LOAD DATA LOCAL INFILE`) | `db.table` | — (refused) | `ConflictOption.Ignore` / `INSERT IGNORE` | explicit column + zero-mode guard | — (rejected) |
| SQLite | — (portable only; `Native` refused) | `schema.table` | `RETURNING` (portable) | `INSERT OR IGNORE` | explicit column | — (rejected) |
| ClickHouse | `InsertBinaryAsync` | `db.table` | — (refused) | — (refused) | explicit value, writable columns only | — (rejected) |
| In-memory | — | — | — | — | `NotSupportedException` (read-only) | — (read-only) |

## Driver prerequisites

`Auto` takes the native path only when every prerequisite below is met; `Native` refuses by name; the
portable path needs none of them. A missing prerequisite is never repaired behind your back — no driver
setting, server variable or connection string is changed by nextorm.

| Provider | Required for the native path |
|---|---|
| SQL Server | a `Microsoft.Data.SqlClient` `SqlConnection`; an internal transaction is opt-in through `SqlServer(new SqlServerBulkInsertOptions { UseInternalTransaction = true })` and cannot be combined with an external context transaction |
| PostgreSQL | a `Npgsql` connection; the binary `COPY` importer streams on that connection, and a finite `MaxBatchSize` starts one importer per bounded chunk (completed on success, disposed on error or cancellation) |
| MySQL / MariaDB | a MySqlConnector connection with `AllowLoadLocalInfile=true` **and** the server variable `@@GLOBAL.local_infile=1`. The client option is read from the connection string and the server variable is read with a read-only preflight (`select @@global.local_infile`); nextorm never enables either. Without them the native path is unavailable, so `Auto` falls back only when the portable path can honour the whole request and `Native` refuses by name |
| ClickHouse | a `ClickHouse.Driver` client (`ClickHouseClient`/`ClickHouseDataSource` — the `UseClickHouse` overloads), because `InsertBinaryAsync` needs one; client settings are copied for the insert, and a session request resolves against the actual session id. A parallelism above one cannot be combined with a session (one concurrent query per session) |
| SQLite | none: there is no native bulk API, so every write uses the portable path on the context's own connection and a `Mode.Native` request is refused. The certified configuration is a file database (the live suite uses one, not a cross-process server) |

## See also

- [Data modification (INSERT)](17-insert-statement.md)
- [Data merging (MERGE / upsert)](21-merge-statement.md)
- [Global query filters](../advanced/query-filters.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../connecting-providers/overview.md)
