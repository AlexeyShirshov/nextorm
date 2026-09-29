# Transactions

> A nextorm context can run its queries inside a database transaction: either one it starts itself, or one owned by EF Core, Dapper or raw ADO.NET that it enlists in. The role is separate from the query surface, so a context without a connection (the in-memory one) does not implement it, and ClickHouse — which speaks HTTP and has no transaction — rejects it. There is no change tracking and no `SaveChanges`: writes stay explicit commands.

**Prerequisites:** [Connections](../infrastructure/02-connections.md) · [Data modification (INSERT)](15-insert-statement.md) · [Provider overview](../providers/overview.md)

## Overview

[`ITransactionManager`](xref:NextORM.Core.ITransactionManager) is the transaction axis of [`DataContext`](xref:NextORM.Core.DataContext), symmetric with [`IConnectionManager`](xref:NextORM.Core.IConnectionManager). Cast the context to the role to start a transaction or to enlist in an existing one; every command the context runs on the connection is then bound to the active transaction.

```csharp
public interface ITransactionManager
{
    DbTransaction? CurrentTransaction { get; }
    DbTransaction BeginTransaction();
    DbTransaction BeginTransaction(IsolationLevel isolationLevel);
    Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    void UseTransaction(DbTransaction? transaction);
}
```

## Starting a transaction

Cast the context to the role and use the ADO.NET-style `BeginTransaction*` methods; commit or roll back with the returned [`DbTransaction`](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction):

```csharp
using var ctx = new PostgresDataContext(connectionString, new DataContextBuilder());
var transactions = (ITransactionManager)ctx;

await using var tx = await transactions.BeginTransactionAsync();
ctx.InsertInto<IOrder>().Values(new Order { Id = 42, Total = 10m }).Insert();
ctx.From<IOrder>().Where(x => x.Id == 42).ToList(); // sees the uncommitted row
await tx.CommitAsync();
```

## Enlisting an existing transaction

To run nextorm queries inside a transaction owned by EF Core, Dapper or raw ADO.NET, enlist it with `UseTransaction`. The context only binds it and never commits, rolls back or disposes it:

```csharp
using var db = new MyDbContext(options);                       // EF Core
await using var efTx = await db.Database.BeginTransactionAsync();
db.Orders.Add(new Order { Id = 42, Total = 10m });
await db.SaveChangesAsync();                                   // EF writes (uncommitted)

using var next = new PostgresDataContext(db.Database.GetDbConnection(), new DataContextBuilder());
((ITransactionManager)next).UseTransaction(efTx.GetDbTransaction());

var row = next.From<IOrder>().Where(x => x.Id == 42).FirstOrDefault(); // sees EF's uncommitted row
await efTx.RollbackAsync();                                    // nextorm drops the completed transaction
```

The transaction must belong to the context's connection (the context validates it).

## Reusing an existing transaction

A reusable method should not assume it owns the transaction. [`TryBeginTransaction`](xref:NextORM.Core.ITransactionManager.TryBeginTransaction(System.Data.Common.DbTransaction@)) starts one only when none is active and never throws when one already is:

```csharp
var transactions = (ITransactionManager)ctx;

var started = transactions.TryBeginTransaction(out var tx);   // tx is the active one when started == false
try
{
    // ... write with ctx ...
    if (started) tx!.Commit();
}
catch
{
    if (started) tx!.Rollback();
    throw;
}
```

`TryBeginTransaction` returns `false` — without throwing — when a transaction is already active (including one enlisted with `UseTransaction`), handing the active transaction back through `out`; and it returns `false` with `null` when the provider has no transactions at all (ClickHouse). Only the caller that started it (`started == true`) commits or rolls it back; an inner caller just runs on the existing transaction. The asynchronous twin [`TryBeginTransactionAsync`](xref:NextORM.Core.ITransactionManager.TryBeginTransactionAsync(System.Threading.CancellationToken)) reports the same outcome as a `(bool Started, DbTransaction? Transaction)` tuple.

## Ownership and lifetime

* Only one transaction is active at a time: a second `BeginTransaction`, or enlisting while one is active, throws `InvalidOperationException`. See [Nested transactions and savepoints](#nested-transactions-and-savepoints).
* A transaction started with `BeginTransaction*` is owned by the context: if it is still open when the context is disposed, the context rolls it back. `UseTransaction(null)` does not detach it — commit, roll back or dispose it instead.
* A transaction passed to `UseTransaction` is owned by the caller: the context never commits, rolls back or disposes it. Pass `null` to detach it (only an enlisted transaction can be detached).
* A transaction that has already been committed, rolled back or disposed is dropped lazily, so later queries on the context run outside it.
* Like the connection itself, the role is not thread-safe: do not begin, detach or complete a transaction concurrently with query execution on the same context.

## Nested transactions and savepoints

The transaction model is flat: a context has **one** transaction at a time, and the role is not
re-entrant. A second `BeginTransaction*` while one is active — or enlisting another transaction with
`UseTransaction(tx)` — throws `InvalidOperationException`:

```csharp
await using var tx = await transactions.BeginTransactionAsync();

await transactions.BeginTransactionAsync();   // InvalidOperationException
// A transaction is already in progress on this context. Nested transactions are not supported;
// use the active transaction instead.
```

There is no ambient or scope-based transaction either: nextorm never uses `TransactionScope` and never
propagates a transaction across contexts. Transactional work is composed by passing the **same** context
(or the same `DbTransaction`) down the call chain, so the inner calls run on the already-open transaction
and never commit or roll it back themselves:

```csharp
// both calls run in one transaction; only the outermost owner commits
await repository.ReserveAsync(order, cancellationToken);
await payment.ChargeAsync(order, cancellationToken);
```

`UseTransaction(externalTx)` is the hook for sharing an *externally owned* transaction (EF Core, Dapper,
raw ADO.NET) — it binds the context to that transaction, it is not a way to nest.

**Savepoints.** A savepoint would let an inner step roll back without discarding the outer work, but the
API does not model them. If you need one, issue the provider's statements yourself through
[raw commands](12-raw-sql.md) — they run on the same connection and are bound to the active transaction:

```csharp
void Raw(string sql) { using (ctx.ExecuteRaw(sql)) { } }

Raw("savepoint before_batch");
try
{
    // ... the step that may need a partial rollback ...
    Raw("release savepoint before_batch");
}
catch
{
    Raw("rollback to savepoint before_batch");
    throw;
}
```

PostgreSQL, SQLite, MySQL and MariaDB spell it `SAVEPOINT` / `RELEASE SAVEPOINT` /
`ROLLBACK TO SAVEPOINT`; SQL Server uses `SAVE TRANSACTION <name>` / `ROLLBACK TRANSACTION <name>`.
nextorm neither creates nor tracks savepoints — name uniqueness and nesting are the caller's
responsibility.

## Provider support

`ITransactionManager` is implemented for SQLite, PostgreSQL, SQL Server and MySQL/MariaDB, and the context binds `DbCommand.Transaction` on every execution path (buffered, streaming and mutations). ClickHouse speaks HTTP and has no ADO.NET transaction: its dialect reports [`SupportsTransactions`](xref:NextORM.Core.ISqlDialect.SupportsTransactions) as `false` and `BeginTransaction*`/`UseTransaction` throw `NotSupportedException`. The in-memory provider does not implement the role.

## See also

- [Connections](../infrastructure/02-connections.md)
- [Data modification (INSERT)](15-insert-statement.md)
- [Provider overview](../providers/overview.md)
- [API reference](../advanced/api-reference.md)
