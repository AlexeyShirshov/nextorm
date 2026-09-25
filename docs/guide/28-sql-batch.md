# Executing statements in one batch

> A batch sends several statements to the database as **one round trip on one server session**. It is what makes a session-scoped temporary table usable under a connection-level pooler, keeps a mutation and the query that reads it on the same backend, and removes a round trip when a materialisation is immediately read back. Build one with [`BatchExtensions.Batch`](xref:NextORM.Core.BatchExtensions).

**Prerequisites:** [Materializing a query into a table](22-create-table-as.md) · [Transactions](25-transactions.md) · [Provider overview](../providers/overview.md)

## Why a batch

Every statement sent as its own command is a separate round trip, and under a connection-level pooler (PgBouncer in `transaction` mode, or any similar load balancer) the next query can land on a different backend. When statements are related — one prepares data the next reads, or a mutation must be visible to the following query — that relationship has to stay within one session, or the result is unavailable.

One open client connection does not guarantee it. An explicit transaction works around it (PgBouncer pins a server for the whole `BEGIN … COMMIT`), and is the right tool when several queries read the table across the transaction. When the goal is simply "run a few statements in sequence and read the result", a **batch** gives the same single session in one round trip, without opening a transaction: the statements travel as one packet, and an intermediate result is visible to the next statement.

Materialising and immediately reading back is a common case but not the only one. A name is optional: `ToTempTable()` generates one and returns it, so the table can be read back with `From(name)`:

```csharp
var name = ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable();   // e.g. "__nextorm_temp_1a2b3c4d"

var orders = ctx.From(name)
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

Those are **two commands**, which a connection-level pooler can route to different backends, making the read fail with `relation "…" does not exist`. The materialisation and the reading query go into one batch instead:

```csharp
var orders = ctx.Batch()
    .CreateTempTableAs("recent_orders", ctx.From<IOrder>()
        .Where(x => x.Total > minTotal)
        .Select(x => new { x.Id, x.Total }))
    .Query(ctx.From("recent_orders")
        .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") }))
    .ToList();
```

Renders as one batch:

```sql
-- PostgreSQL
create temporary table recent_orders as select id, total from orders
 where (total > @b0_minTotal);
select id, total from recent_orders
```

`Query<TResult>` returns the `BatchQuery<TResult>` terminal:

| Member | Effect |
|---|---|
| `ToList()` | Executes the batch and returns the result rows. |
| `ToListAsync(cancellationToken = default)` | Asynchronous twin of `ToList`. |
| `ToAsyncEnumerable(cancellationToken = default)` | Streams the result rows; the reader stays open for the whole batch, so the batch runs when enumeration starts and holds the connection until it completes. |
| `ToSql()` | Renders the batch (statements joined with `;`) without executing it. |

## Building a batch directly

`ctx.Batch()` returns a `BatchBuilder` for more than one statement, a side-effecting DML step, or an explicit order.

Materialisation + read:

```csharp
var rows = ctx.Batch()
    .CreateTempTableAs("recent_orders", ctx.From<IOrder>().Where(x => x.Total > minTotal).Select(x => new { x.Id, x.Total }))
    .CreateTempTableAs("recent_ids", ctx.From("recent_orders").Select(t => new { Id = t.GetInt32("id") }))
    .Query(ctx.From("recent_ids").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

```sql
-- PostgreSQL
create temporary table recent_orders as select id, total from orders
 where (total > @b0_minTotal);
create temporary table recent_ids as select id from recent_orders;
select id from recent_ids
```

Mutation + read — the query observes the update on the same session:

```csharp
var updated = ctx.Batch()
    .Update(ctx.Update<IOrder>().Set(x => x.Status, "shipped").Where(x => x.Id == id))
    .Query(ctx.From<IOrder>().Where(x => x.Id == id).Select(x => new { x.Id, x.Status }))
    .ToList();
```

```sql
-- PostgreSQL
update orders set status = @p0 where (id = @b0_id);
select id, status from orders
 where (id = @b1_id)
```

* `CreateTempTableAs(name, source, options?)` and `CreateTableAs(name, source, options?)` add materialisations, in order, any number.
* `Insert(insert)`, `Update(update)`, `Delete(delete)` and `Truncate(truncate)` add a side-effecting DML statement, in order, any number. They take the same builders as `ctx.InsertInto<T>()`, `ctx.Update<T>()`, `ctx.DeleteFrom<T>()` and `ctx.Truncate<T>()`; the builder's terminal (`Insert()`, `Update()`, …) is never called — the batch runs it.
* `Query<TResult>(query)` adds the single result-bearing query and returns the terminal; it must be the last statement. A second `Query`, or any statement after `Query`, throws `InvalidOperationException`.
* Every statement must be built from the batch's own context; a statement bound to a different `IDataContext` is rejected with `ArgumentException`.

Parameters are numbered across the whole batch, and a captured variable used by more than one statement gets a per-statement prefix (`b0_`, `b1_`, …), so placeholder names never collide — including in the `;`-joined form. A captured variable referenced twice within the same statement keeps a single parameter entry.

The SQL blocks above are the PostgreSQL rendering. By default `ToSql()` prints the statements on one line separated by `; `; enable `DataContextBuilder.UseMultilineBatchSql()` to render each statement on its own line, as shown here. SQL Server renders the same pair differently — there is no `CREATE TEMPORARY TABLE ... AS SELECT` and `CreateTempTableAs` throws `NotSupportedException`, so a temporary target is created with `CreateTableAs` and a `#`-prefixed name:

```sql
-- SQL Server
select id, total into #recent_orders from orders
 where (total > @b0_minTotal);
select id, total from #recent_orders
```

## Provider support

| Provider | Mechanism |
|---|---|
| PostgreSQL | `NpgsqlBatch`; its implicit transaction pins one backend, which is exactly what a transaction-mode pooler needs. |
| MySQL / MariaDB | `MySqlBatch`. |
| SQLite | One `;`-joined command (no `DbBatch`). |
| SQL Server | One `;`-joined command. `SqlBatch` is deliberately not used: it runs each command in its own scope, so a `#temp` created by one command would not be visible to the next. Name a session-scoped target with the `#` prefix and `CreateTableAs` (`CreateTempTableAs` has no `CREATE TEMPORARY TABLE ... AS SELECT` on SQL Server). |
| ClickHouse | rejected — `NotSupportedException`. |
| In-memory | rejected — `NotSupportedException`. |

The capability is [`ISqlDialect.SupportsBatch`](xref:NextORM.Core.ISqlDialect.SupportsBatch); a provider without it (and the in-memory context) fails fast instead of silently degrading to separate statements.

## Limitations

* A batch has exactly one result-bearing query, and it is the last statement. The statements before it are side-effecting — materialisations and DML (`INSERT`/`UPDATE`/`DELETE`/`TRUNCATE`) — which return no columns.
* A mutation added to a batch may not request returned rows: `Returning()`/`ReturningIdentity()` terminals produce a different builder type and are not accepted. Multi-table `UpdateJoin`/`DeleteJoin` and `Merge` are not batch steps. Raw SQL/DDL has no command model.
* `ToSql()` shows the `;`-joined text; on a `DbBatch` provider the statements are still sent as separate commands of one batch.
* Batch execution is **not** routed through the query interceptors: their events carry a `DbCommand`, while a `DbBatch` command is a `DbBatchCommand`.
* `SqlFunctions.Parameter` runtime placeholders cannot be used in a batched query; capture the value in a local variable instead (as in any query rendered as a source). Captured variables become parameters automatically.
* Batch plans are not cached: each terminal call re-renders the batch.

## See also

- [Materializing a query into a table](22-create-table-as.md)
- [Transactions](25-transactions.md)
- [Provider overview](../providers/overview.md)
