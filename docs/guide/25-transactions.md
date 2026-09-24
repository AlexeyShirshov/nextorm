# Transactions

> A nextorm context can run its queries inside a database transaction: either one it starts itself, or one owned by EF Core, Dapper or raw ADO.NET that it enlists in. The role is separate from the query surface, so a context without a connection (the in-memory one) does not implement it, and ClickHouse — which speaks HTTP and has no transaction — rejects it. There is no change tracking and no `SaveChanges`: writes stay explicit commands.

**Prerequisites:** [Connections and logging](16-connections-and-logging.md) · [Data modification (INSERT)](19-insert-statement.md) · [Provider overview](../providers/overview.md)

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

## Ownership and lifetime

* Only one transaction is active at a time: a second `BeginTransaction`, or enlisting while one is active, throws `InvalidOperationException`. Nested transactions and savepoints are not part of this surface.
* A transaction started with `BeginTransaction*` is owned by the context: if it is still open when the context is disposed, the context rolls it back. `UseTransaction(null)` does not detach it — commit, roll back or dispose it instead.
* A transaction passed to `UseTransaction` is owned by the caller: the context never commits, rolls back or disposes it. Pass `null` to detach it (only an enlisted transaction can be detached).
* A transaction that has already been committed, rolled back or disposed is dropped lazily, so later queries on the context run outside it.
* Like the connection itself, the role is not thread-safe: do not begin, detach or complete a transaction concurrently with query execution on the same context.

## Provider support

`ITransactionManager` is implemented for SQLite, PostgreSQL, SQL Server and MySQL/MariaDB, and the context binds `DbCommand.Transaction` on every execution path (buffered, streaming and mutations). ClickHouse speaks HTTP and has no ADO.NET transaction: its dialect reports [`SupportsTransactions`](xref:NextORM.Core.ISqlDialect.SupportsTransactions) as `false` and `BeginTransaction*`/`UseTransaction` throw `NotSupportedException`. The in-memory provider does not implement the role.

## See also

- [Connections and logging](16-connections-and-logging.md)
- [Data modification (INSERT)](19-insert-statement.md)
- [Provider overview](../providers/overview.md)
- [API reference](../advanced/api-reference.md)
