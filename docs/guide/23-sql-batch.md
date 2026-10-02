# Executing statements in one batch

> A batch sends several statements to the database as **one round trip on one server session**. It is what makes a session-scoped temporary table usable under a connection-level pooler, keeps a mutation and the query that reads it on the same backend, and removes a round trip when a materialisation is immediately read back. Build one with [`BatchExtensions.CreateBatchBuilder`](xref:NextORM.Core.BatchExtensions).

**Prerequisites:** [Materializing a query into a table](18-create-table-as.md) · [Transactions](21-transactions.md) · [Provider overview](../providers/overview.md)

## Why a batch

Every statement sent as its own command is a separate round trip, and under a connection-level pooler (PgBouncer in `transaction` mode, or any similar load balancer) the next query can land on a different backend. When statements are related — one prepares data the next reads, or a mutation must be visible to the following query — that relationship has to stay within one session, or the result is unavailable.

One open client connection does not guarantee it. An explicit transaction works around it (PgBouncer pins a server for the whole `BEGIN … COMMIT`), and is the right tool when several queries read the table across the transaction. When the goal is simply "run a few statements in sequence and read the result", a **batch** gives the same single session in one round trip, without opening a transaction: the statements travel as one packet, and an intermediate result is visible to the next statement.

Materialising and immediately reading back is a common case but not the only one. `ToTempTable("recent_orders")` creates the table, so it can be read back with `From("recent_orders")`:

```csharp
ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable("recent_orders");

var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

Those are **two commands**, which a connection-level pooler can route to different backends, making the read fail with `relation "…" does not exist`. The materialisation and the reading query go into one batch instead:

```csharp
var orders = ctx.CreateBatchBuilder()
    .CreateTempTable("recent_orders", ctx.From<IOrder>()
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

## Lazy temporary tables

A lazy temporary table from [`AsTempTable`](18-create-table-as.md#lazy-temporary-tables-astemptable) is the declarative form of this batch: nothing runs when the source is built, and reading it through `From(...)` compiles to a `CreateTempTable` step followed by the read, in one batch. It is useful when the same materialisation is read more than once or across terminal calls: every read re-materialises, so a pooler can never hand the read a backend that lacks the table.

```csharp
var source = ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .AsTempTable();   // nothing executed

var orders = ctx.From(source)   // DROP + CREATE TEMP + read, one batch
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

Each read prepends a `DROP TABLE IF EXISTS` so the batch is self-contained and repeatable:

```sql
-- PostgreSQL
drop table if exists __nextorm_temp_xxxxxxxx;
create temporary table __nextorm_temp_xxxxxxxx as select id, total from orders
 where (total > @b0_minTotal);
select id, total from __nextorm_temp_xxxxxxxx
```

`ToBatchSql()` renders that batch without executing it, and every terminal (`ToList`, `First`, `Single`, `Count`, `Any`, `ToAsyncEnumerable`, …) runs it. A lazy source may itself read another lazy source; the tables are then materialised in dependency order before the read. The read command is re-rendered on every execution, so it is never stored in the plan cache (like every batch plan) and its captured variables become parameters as usual.

`AsTempTable` is available exactly where `CreateTempTable` is — PostgreSQL, SQLite, MySQL and MariaDB; SQL Server (use `CreateTable` with a `#`-prefixed name), ClickHouse and the in-memory context reject it when the query renders.

## Building a batch directly

`ctx.CreateBatchBuilder()` returns a `BatchBuilder` for more than one statement, a side-effecting DML or raw SQL step, or an explicit order.

Materialisation + read:

```csharp
var rows = ctx.CreateBatchBuilder()
    .CreateTempTable("recent_orders", ctx.From<IOrder>().Where(x => x.Total > minTotal).Select(x => new { x.Id, x.Total }))
    .CreateTempTable("recent_ids", ctx.From("recent_orders").Select(t => new { Id = t.GetInt32("id") }))
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

Replacing a persistent table — `CreateTable` with `DropExisting` drops it first, so the step can run again instead of failing on the second run:

```csharp
var rows = ctx.CreateBatchBuilder()
    .CreateTable("order_archive", ctx.From<IOrder>().Select(x => new { x.Id, x.Total }),
        o => o.DropExisting())
    .Query(ctx.From("order_archive").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

```sql
-- PostgreSQL
drop table if exists order_archive;
create table order_archive as select id, total from orders;
select id from order_archive
```

`DropExisting` is valid only for a persistent `CreateTable` (a `CreateTempTable` is session-scoped and rejects it) and cannot be combined with `IfNotExists`.

Mutation + read — the query observes the update on the same session:

```csharp
var updated = ctx.CreateBatchBuilder()
    .Update(ctx.CreateUpdateBuilder<IOrder>().Set(x => x.Status, "shipped").Where(x => x.Id == id))
    .Query(ctx.From<IOrder>().Where(x => x.Id == id).Select(x => new { x.Id, x.Status }))
    .ToList();
```

```sql
-- PostgreSQL
update orders set status = @p0 where (id = @b0_id);
select id, status from orders
 where (id = @b1_id)
```

* `CreateTempTable(name, source, options?)` and `CreateTable(name, source, options?)` add materialisations, in order, any number. Options can be passed as `CreateTableOptions` or as a `CreateTableOptionsBuilder` callback (`o => o.DropExisting()`); a persistent `CreateTable` with `DropExisting` prepends a `DROP TABLE IF EXISTS` so the step replaces an existing table.
* `Insert(insert)`, `Update(update)`, `Delete(delete)` and `Truncate(truncate)` add a side-effecting DML statement, in order, any number. They take the same builders as `ctx.CreateInsertBuilder<T>()`, `ctx.CreateUpdateBuilder<T>()`, `ctx.CreateDeleteBuilder<T>()` and `ctx.CreateTruncateBuilder<T>()`; the builder's terminal (`Insert()`, `Update()`, …) is never called — the batch runs it.
* `Raw(sql)` adds a verbatim, side-effecting SQL step, in order, any number: the text is emitted unchanged and no parameters are bound. It follows the same ordering rule as the other side-effecting steps — it must precede `Query`/`AddQuery` (see [Raw steps](#raw-steps)).
* `Query<TResult>(query)` adds the single result-bearing query and returns the terminal; it must be the last statement. A second `Query`, or any statement after `Query`, throws `InvalidOperationException`. To carry several result sets use `AddQuery<TResult>` and `Execute`/`ExecuteAsync` instead (see [Multiple result sets](#multiple-result-sets)).
* Every statement must be built from the batch's own context; a statement bound to a different `IDataContext` is rejected with `ArgumentException`.

Parameters are numbered across the whole batch, and a captured variable used by more than one statement gets a per-statement prefix (`b0_`, `b1_`, …), so placeholder names never collide — including in the `;`-joined form. A captured variable referenced twice within the same statement keeps a single parameter entry.

The SQL blocks above are the PostgreSQL rendering. By default `ToSql()` prints the statements on one line separated by `; `; enable `DataContextBuilder.UseMultilineBatchSql()` to render each statement on its own line, as shown here. SQL Server renders the same pair differently — there is no `CREATE TEMPORARY TABLE ... AS SELECT` and `CreateTempTable` throws `NotSupportedException`, so a temporary target is created with `CreateTable` and a `#`-prefixed name:

```sql
-- SQL Server
select id, total into #recent_orders from orders
 where (total > @b0_minTotal);
select id, total from #recent_orders
```

## Raw steps

`Raw(sql)` adds a **verbatim, side-effecting** step: the string is emitted exactly as given, placeholders are **not** rewritten, and no parameters are bound — it is the escape hatch for DDL or provider-specific statements the typed builders do not model. It is added before the result-bearing `Query`/`AddQuery`, like any other side-effecting step: adding a `Raw` step after `Query` or after the first `AddQuery` throws `InvalidOperationException`, and the batch must still end with at least one result-bearing query — a batch of `Raw` steps alone is rejected. The text is executed verbatim and binds no parameters, so pass only trusted SQL — never concatenate untrusted user input into a `Raw` string.

`Raw` participates in `ToSql()`, and in the multi-result `AddQuery`/`Execute` form it is one of the side-effecting steps that precede the queries. SQL Server can read a stored-procedure result through a temp table in one batch, without a transaction:

```csharp
var rows = ctx.CreateBatchBuilder()
    .Raw("create table #r (id int, total decimal(18,2))")
    .Raw("insert into #r (id, total) exec dbo.MyProc @p = 42")
    .Query(ctx.From("#r").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

`INSERT … EXEC` cannot be nested (a procedure that itself runs `INSERT … EXEC` fails), and the procedure must return a **single** result set whose columns match `#r` — the text is passed through unchanged, so NextORM cannot validate either constraint. `Raw` binds no parameters: `@p = 42` above is a trusted literal in the T-SQL text, not a bound parameter, and any value that must vary has to be inlined by the caller.

## Multiple result sets

A batch may carry **more than one** result-bearing query. Add each one with `AddQuery<TResult>` instead of the single-result `Query<TResult>` terminal, then run the whole batch once with `Execute()`/`ExecuteAsync(...)` and read the sets in the order they were added:

```csharp
record OrderTotal(int Id, decimal Total);
record OrderLine(int Id, string Status);

var result = ctx.CreateBatchBuilder()
    .AddQuery(ctx.From<IOrder>().Where(x => x.Status == "open")
        .Select(x => new OrderTotal(x.Id, x.Total)))
    .AddQuery(ctx.From<IOrder>().Where(x => x.Status == "shipped")
        .Select(x => new OrderLine(x.Id, x.Status)))
    .Execute();

var open = result.Read<OrderTotal>();       // first added query
var shipped = result.Read<OrderLine>();     // second added query
```

The two queries go to the database as **one** batch — one round trip, one server session:

```sql
-- PostgreSQL
select id, total from orders
 where (status = @b0_status);
select id, status from orders
 where (status = @b1_status)
```

`AddQuery` returns the builder, so several calls chain. Every result query must follow the side-effecting statements: a materialisation or DML step added after the first `AddQuery` throws `InvalidOperationException`. `AddQuery` and `Query` are mutually exclusive — `Query` ends the batch, so a later `AddQuery` throws, and `Query` after `AddQuery` throws too. `Execute()`/`ExecuteAsync()` with no result query added throws `InvalidOperationException`.

They return a [`BatchResult`](xref:NextORM.Core.BatchResult):

| Member | Effect |
|---|---|
| `ResultSetCount` | The number of result sets the batch produced. |
| `Read<TResult>()` | Returns the next set, in the order the queries were added; each set is read once. |

`Read<TResult>()` is typed: the requested type must match the projected type of the next query, otherwise it throws `InvalidOperationException`; reading past the last set throws `InvalidOperationException` as well. `BatchResult` is **eager** and owns no reader or connection — every set is buffered before `Execute()` returns, so it is not disposable and does not stream.

For a batch with a single result the `Query<TResult>` terminal still renders without executing through `BatchQuery<TResult>.ToSql()`, and the multi-result `AddQuery`/`Execute` form renders the whole batch without executing through `BatchBuilder.ToSql()` — both render the same text that execution runs, and `BatchBuilder.ToSql()` requires at least one result step (it throws `InvalidOperationException` otherwise).

## Provider support

| Provider | Mechanism |
|---|---|
| PostgreSQL | `NpgsqlBatch`; its implicit transaction pins one backend, which is exactly what a transaction-mode pooler needs. |
| MySQL / MariaDB | `MySqlBatch`. |
| SQLite | One `;`-joined command (no `DbBatch`). |
| SQL Server | One `;`-joined command. `SqlBatch` is deliberately not used: it runs each command in its own scope, so a `#temp` created by one command would not be visible to the next. Name a session-scoped target with the `#` prefix and `CreateTable` (`CreateTempTable` has no `CREATE TEMPORARY TABLE ... AS SELECT` on SQL Server). |
| ClickHouse | rejected — `NotSupportedException`. |
| In-memory | rejected — `NotSupportedException`. |

The capability is [`ISqlDialect.SupportsBatch`](xref:NextORM.Core.ISqlDialect.SupportsBatch); a provider without it (and the in-memory context) fails fast instead of silently degrading to separate statements.

## Limitations

* A batch ends with one or more result-bearing queries — one added with the `Query<TResult>` terminal, several with `AddQuery<TResult>` and `Execute`/`ExecuteAsync` — and they are the last statements. The statements before them are side-effecting — materialisations, DML (`INSERT`/`UPDATE`/`DELETE`/`TRUNCATE`) and raw SQL steps — which return no columns.
* Multi-result execution is **sequential and eager**: `BatchResult.Read<TResult>()` returns the sets in the order the queries were added, each set may be read once, and every set is fully buffered in memory before `Execute()`/`ExecuteAsync()` returns — there is no streaming and no random access. Only the single-result `BatchQuery<TResult>` terminal streams, through `ToAsyncEnumerable`.
* A mutation added to a batch may not request returned rows: `Returning()`/`ReturningIdentity()` terminals produce a different builder type and are not accepted. Multi-table `CreateUpdateJoinBuilder`/`DeleteJoin` and `Merge` are not batch steps. Raw SQL/DDL is expressible as a verbatim step through `Raw(sql)` — see [Raw steps](#raw-steps).
* `ToSql()` shows the `;`-joined text without executing: on the single-result `BatchQuery<TResult>` terminal and on `BatchBuilder` for the multi-result `AddQuery`/`Execute` form (`BatchBuilder.ToSql()` renders the whole batch and requires at least one result step). On a `DbBatch` provider the statements are still sent as separate commands of one batch when the batch executes.
* Batch execution is **not** routed through the query interceptors: their events carry a `DbCommand`, while a `DbBatch` command is a `DbBatchCommand`.
* `SqlFunctions.Parameter` runtime placeholders cannot be used in a batched query; capture the value in a local variable instead (as in any query rendered as a source). Captured variables become parameters automatically.
* Batch plans are not cached: each terminal call re-renders the batch.

## See also

- [Materializing a query into a table](18-create-table-as.md)
- [Transactions](21-transactions.md)
- [Provider overview](../providers/overview.md)
