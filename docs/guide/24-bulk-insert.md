# Bulk insert

`BulkInsertInto<TEntity>()` writes a whole set in one explicit call, without change tracking. Where the
provider has a native bulk API it is used — PostgreSQL binary `COPY` and SQL Server `SqlBulkCopy` —
otherwise the set is written as a parameterised `INSERT ... VALUES`, optionally chunked.

Bulk insert works over a normal mapping; see [Data modification (INSERT)](19-insert-statement.md) for
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

// native COPY on PostgreSQL; portable INSERT ... VALUES on the other providers
var written = ctx.BulkInsertInto<IOrder>().Values(orders).BulkInsert();
// written == 3
```

An `IAsyncEnumerable<TEntity>` source is also accepted, and the whole thing has an async terminal:

```csharp
await ctx.BulkInsertInto<IOrder>().Values(ordersStream).BulkInsertAsync(cancellationToken);
```

An empty source writes nothing and returns `0`.

## Retargeting the destination table

`Table(name)` / `Table(schema, name)` overrides the entity's mapped table for one write, so a mapped
shape can be copied into a differently named table without a second `[SqlTable]` mapping:

```csharp
var written = ctx.BulkInsertInto<IOrder>(o => o.Table("archive", "orders_2024"))
    .Values(orders)
    .BulkInsert();
```

The override has priority over the `[SqlTable]`/`Table(...)` mapping, the naming convention is not
applied to it, and the optional schema is quoted separately from the table. It works on both the native
(`COPY`/`SqlBulkCopy`) and the portable `INSERT ... VALUES` paths, and combines with `Returning*` and
`KeepIdentity`.

## Reading the generated keys

The native bulk APIs cannot return rows, so `Returning*` switches to the portable path and writes the
set as a batch of returning `INSERT ... VALUES ... RETURNING`/`OUTPUT` statements. Use
`ReturningKey<TKey>()` for the key column or `Returning(projection)` for any mapped projection; both
terminals are `ToList()` / `ToListAsync()`.

```csharp
// keys only — ReturningKey<TKey>() reads the key column declared in metadata
IReadOnlyList<int> ids = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .ReturningKey<int>()
    .ToList();
// ids = [1, 2, 3]

// a projection per written row
var rows = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .Returning(o => new { o.Id, o.CustomerId })
    .ToList();
// rows = [ { Id = 1, CustomerId = 1 }, { Id = 2, CustomerId = 2 }, ... ]

// a single scalar projection
IReadOnlyList<int> customerIds = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .Returning(o => o.CustomerId)
    .ToList();

// async
IReadOnlyList<int> keys = await ctx.BulkInsertInto<IOrder>()
    .Values(ordersStream)
    .ReturningKey<int>()
    .ToListAsync(cancellationToken);
```

> **The result order is not guaranteed** to match the order of the source (neither `RETURNING` nor
> `OUTPUT` promises it). Correlate rows by a business key, not by position:

```csharp
var idByCustomer = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .Returning(o => new { o.CustomerId, o.Id })
    .ToList()
    .ToDictionary(o => o.CustomerId, o => o.Id);

var orderIdForCustomer2 = idByCustomer[2];
```

`Returning*` requires a provider with `RETURNING`/`OUTPUT`: PostgreSQL and SQLite 3.35+ (via
`RETURNING`) and SQL Server (via `OUTPUT`). MySQL, MariaDB and ClickHouse reject it with a clear
`NotSupportedException` — read the keys with a plain `InsertInto` there.

## Bounding the batch and reporting progress

The portable path sends the whole set as one statement by default. Chunk limits and progress are write
options, passed to `BulkInsertInto<TEntity>` — either as a `BulkInsertOptions` record or through the
fluent `BulkInsertOptionsBuilder` callback:

```csharp
var written = 0;

await ctx.BulkInsertInto<IOrder>(o => o
        .MaxBatchSize(1_000)                                 // rows per statement
        .MaxParameters(20_000)                               // bound parameters per statement
        .NotifyAfter(10_000, (total, _) => written = total)) // called with the cumulative count
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

Chunks are written as separate `INSERT` statements; nextorm does **not** wrap them in a transaction — if
you need atomicity, start a transaction yourself.

The `NotifyAfter` callback runs inline — after a committed chunk on the portable path, or on the
provider's native progress tick (`SqlBulkCopy.NotifyAfter` on SQL Server, the write loop on PostgreSQL).
Keep it fast and non-throwing: a callback that throws propagates the exception and leaves the
already-written rows committed (nextorm opens no transaction), and a callback that blocks stalls the
call until it returns.

Its second argument is a `CancellationToken`. Supply the source with `ProgressCancellationTokenSource` to
let a cooperative callback stop early — the writer throws `OperationCanceledException` once the in-flight
callback returns:

```csharp
using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await ctx.BulkInsertInto<IOrder>(o => o
        .ProgressCancellationTokenSource(budget)
        .NotifyAfter(10_000, (total, token) => Report(total, token)))
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

> The token cannot pre-empt a callback that ignores it (the callback runs inline on the writing thread);
> it only lets a callback that honours it stop early. nextorm never cancels or disposes the source.

## Explicit identity values and skipping duplicates

`KeepIdentity` writes the identity values carried by the entities (`OVERRIDING SYSTEM VALUE` on
PostgreSQL, `SET IDENTITY_INSERT ... ON/OFF` on SQL Server). `IgnoreDuplicates` skips rows that would
violate a unique constraint instead of failing the whole write.

```csharp
// explicit ids
var pinned = new[]
{
    new Order { Id = 9001, CustomerId = 1, Total = 5m },
    new Order { Id = 9002, CustomerId = 2, Total = 6m },
};
ctx.BulkInsertInto<IOrder>(o => o.KeepIdentity()).Values(pinned).BulkInsert();

// insert a batch that repeats 9001: the duplicate is skipped, the other rows are written
var written = ctx.BulkInsertInto<IOrder>(o => o.KeepIdentity().IgnoreDuplicates())
    .Values([
        new Order { Id = 9001, CustomerId = 1, Total = 5m },   // conflicts -> skipped
        new Order { Id = 9003, CustomerId = 3, Total = 7m },   // written
    ])
    .BulkInsert();
// written == 1
```

`IgnoreDuplicates` is available on PostgreSQL (`ON CONFLICT DO NOTHING`), SQLite (`INSERT OR IGNORE`)
and MySQL/MariaDB (`INSERT IGNORE`); it is rejected on SQL Server, and on ClickHouse it is a no-op
(there is no uniqueness, so every row is written).

> `KeepIdentity()` is ignored when the entity's mapping declares no identity column (matching linq2db):
> explicit values are still written for non-identity keys, but the provider's identity-insert form
> (`SET IDENTITY_INSERT`, `OVERRIDING SYSTEM VALUE`) is not emitted, so a non-identity table never
> fails with SQL Server error 8106. The check uses the donor entity's metadata, not the actual target
> table — when `Table(...)` retargets a write, the caller is responsible for the target's identity
> shape.

## SQL Server bulk-copy options

The native `SqlBulkCopy` path exposes four flags that the PostgreSQL `COPY` path and the portable
`INSERT ... VALUES` path cannot express. They are off by default, matching `SqlBulkCopyOptions.Default`:

```csharp
var written = ctx.BulkInsertInto<IOrder>(o => o
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
a flag on a path that cannot express it throws a `NotSupportedException` naming the flags instead of
silently ignoring them — this covers PostgreSQL `COPY`, the portable `INSERT ... VALUES` used by
MySQL/MariaDB/SQLite/ClickHouse, and SQL Server itself whenever `Returning*` or `KeepIdentity` switches the
write to the portable path (so `KeepIdentity` and the bulk-copy flags cannot be combined).

## Inspecting the SQL

`ToSql()` renders the parameterised SQL the portable path would execute for the first batch, without
opening a connection:

```csharp
var sql = ctx.BulkInsertInto<IOrder>().Values(orders).ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), (@p2, @p3), (@p4, @p5)

var returningSql = ctx.BulkInsertInto<IOrder>().Values(orders).ReturningKey<int>().ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), ... returning id
```

## Options

Write options are passed to `BulkInsertInto<TEntity>` — as a `BulkInsertOptions` record or, more
concisely, through the `BulkInsertOptionsBuilder` callback:

```csharp
ctx.BulkInsertInto<IOrder>(new BulkInsertOptions { MaxBatchSize = 1_000, IgnoreDuplicates = true });
ctx.BulkInsertInto<IOrder>(o => o.MaxBatchSize(1_000).IgnoreDuplicates());
```

| Option | Builder method | Effect |
|---|---|---|
| `MaxBatchSize` | `MaxBatchSize(rows)` | chunk the portable path (default: one statement) |
| `MaxParameters` | `MaxParameters(count)` | bound parameters per statement |
| `MaxSqlLength` | `MaxSqlLength(chars)` | approximate SQL length per statement |
| `TableName` / `TableSchema` | `Table(name)` / `Table(schema, name)` | override the mapped destination table, optionally schema-qualified |
| `KeepIdentity` | `KeepIdentity()` | write explicit identity values (`OVERRIDING SYSTEM VALUE` on PostgreSQL, `SET IDENTITY_INSERT ... ON/OFF` on SQL Server); ignored when the entity has no identity column |
| `IgnoreDuplicates` | `IgnoreDuplicates()` | skip rows that violate a unique constraint |
| `CheckConstraints` | `CheckConstraints()` | check CHECK/FOREIGN KEY constraints; SQL Server native only |
| `TableLock` | `TableLock()` | take a table-level bulk-update lock; SQL Server native only |
| `KeepNulls` | `KeepNulls()` | write explicit `NULL`s instead of the destination `DEFAULT`; SQL Server native only |
| `FireTriggers` | `FireTriggers()` | fire `INSERT` triggers; SQL Server native only |
| `TimeoutSeconds` | `Timeout(seconds)` | command timeout; SQL Server native only |
| `Progress` / `NotifyEvery` | `NotifyAfter(rows, onRows)` | progress with the cumulative written-row count |
| `ProgressCancellationTokenSource` | `ProgressCancellationTokenSource(source)` | token passed to the progress callback |

On the returned builder:

| Method | Effect |
|---|---|
| `Values(IEnumerable<TEntity>)` / `Values(IAsyncEnumerable<TEntity>)` | the set, read once; an empty set writes nothing and returns `0` |
| `ReturningKey<TKey>()` / `Returning(projection)` | materialise the keys/projection of the written rows via `RETURNING`/`OUTPUT` |
| `BulkInsert()` / `BulkInsertAsync(ct)` | write the set and return the number of rows written |
| `ToSql()` | render the first batch's SQL without executing |

## Provider support

| Provider | Native bulk | Target override | `Returning` | `IgnoreDuplicates` | `KeepIdentity` | Bulk-copy flags |
|---|---|---|---|---|---|---|
| PostgreSQL | binary `COPY` | `schema.table` | `RETURNING` (portable) | `ON CONFLICT DO NOTHING` | `OVERRIDING SYSTEM VALUE` | — (rejected) |
| SQL Server | `SqlBulkCopy` | `schema.table` | `OUTPUT` (portable) | — (rejected) | `SET IDENTITY_INSERT` | `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` |
| MySQL | — (portable) | `db.table` | — | `INSERT IGNORE` | explicit values | — (rejected) |
| MariaDB | — (portable) | `db.table` | — | `INSERT IGNORE` | explicit values | — (rejected) |
| SQLite | — (portable) | `schema.table` | `RETURNING` (portable) | `INSERT OR IGNORE` | explicit values | — (rejected) |
| ClickHouse | — (portable) | `db.table` | — | no-op (no uniqueness) | — | — (rejected) |
| In-memory | — | — | — | — | `NotSupportedException` (read-only) | — (read-only) |

## See also

- [Data modification (INSERT)](19-insert-statement.md)
- [Data merging (MERGE / upsert)](23-merge-statement.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)
- [Provider overview](../providers/overview.md)
