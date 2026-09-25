# Interceptors

> Observe the command execution and connection lifecycle of a context without deriving from `DataContext`, and without a logger.

**Prerequisites:** [Connections and logging](16-connections-and-logging.md) · [Query reuse](15-query-reuse.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Overview

An interceptor is a small object registered on a context that receives callbacks around the work the context performs. nextorm ships two of them:

| Interface | Observes | Callbacks |
|---|---|---|
| [`IQueryInterceptor`](xref:NextORM.Core.IQueryInterceptor) | command execution | `CommandInitialized`, `CommandExecuting`, `CommandExecuted`, `CommandFailed` |
| [`IConnectionInterceptor`](xref:NextORM.Core.IConnectionInterceptor) | connection open | `ConnectionOpening`, `ConnectionOpened` |

A callback receives an event payload ([`CommandEventData`](xref:NextORM.Core.CommandEventData) / [`ConnectionEventData`](xref:NextORM.Core.ConnectionEventData)) plus the ADO.NET object involved (`DbCommand` / `DbConnection`). The events are raised by the **context's execution and connection axes**, not by the command: a `DbCommand` is a data structure and cannot log or profile itself.

Typical uses are timing and metrics, structured logging of the executed SQL, and provider-specific command options (a command timeout, for example).

## Registering an interceptor

Register on the builder to apply an interceptor to every context the builder creates:

```csharp
using NextORM.Core;

var builder = new DataContextBuilder()
    .UseSqlite("app.db")
    .AddInterceptor(new TimingInterceptor());

using var ctx = builder.CreateDataContext();
```

Interceptors run in registration order. Because the interfaces use **default interface methods**, an implementation only overrides the callbacks it cares about - the rest stay no-ops. The same registrations work through dependency injection:

```csharp
services.AddNextOrmContext(o => o
    .UseSqlite(connectionString)
    .AddInterceptor(new TimingInterceptor()));
```

A context created from the builder can add more interceptors for its own lifetime:

```csharp
using var ctx = builder.CreateDataContext();
ctx.AddInterceptor(new AuditInterceptor());
```

`AddInterceptor` is available on [`DataContext`](xref:NextORM.Core.DataContext), not on the [`IDataContext`](xref:NextORM.Core.IDataContext) facade, so cast the context when you hold the interface.

## A query interceptor

```csharp
using System.Data.Common;
using NextORM.Core;

public sealed class TimingInterceptor : IQueryInterceptor
{
    public void CommandExecuting(CommandEventData eventData, DbCommand command)
        => Console.WriteLine($"executing: {eventData.Sql}");

    public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
        => Console.WriteLine($"executed in {elapsed.TotalMilliseconds:F1} ms");

    public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
        => Console.WriteLine($"failed: {exception.GetType().Name}");
}
```

The lifecycle of a buffered terminal (`ToList`, `First`, `ExecuteScalar`, ...), a streaming terminal (`ToAsyncEnumerable`) and a DML statement (`Insert`, `Update`, `Delete`, `Merge`, `Truncate`) is:

```text
CommandInitialized -> CommandExecuting -> CommandExecuted
                                      \-> CommandFailed   (on a provider error)
```

* `CommandInitialized` fires when a command has been created and its parameters are bound. For a query this happens **once, when the plan is built** (a plan-cache hit does not fire it again); for a DML statement it fires on **every execution**, because mutation commands are not cached.
* `CommandExecuting` fires immediately before execution, with the fully bound command - an interceptor can read `command.Parameters` there.
* `CommandExecuted` carries the elapsed `TimeSpan`; `CommandFailed` carries the provider exception, which is then rethrown unchanged.

## A connection interceptor

```csharp
public sealed class AuditConnectionInterceptor : IConnectionInterceptor
{
    public void ConnectionOpening(ConnectionEventData eventData)
        => Console.WriteLine($"opening {eventData.Connection.GetType().Name}");

    public void ConnectionOpened(ConnectionEventData eventData)
        => Console.WriteLine($"opened {eventData.Connection.DataSource}");
}
```

The connection is opened lazily and reused, so `ConnectionOpening`/`ConnectionOpened` fire only for the transition from closed to open - not on every query. Both the synchronous [`EnsureConnectionOpen`](xref:NextORM.Core.DataContext.EnsureConnectionOpen) and the asynchronous [`EnsureConnectionOpenAsync`](xref:NextORM.Core.DataContext.EnsureConnectionOpenAsync(System.Threading.CancellationToken)) paths raise them.

## Guarantees

* **Registration order** is preserved; every interceptor of a type receives every event, in the order it was added, after the builder-configured ones.
* **Thread safety**: interceptors must be thread-safe, because a context may execute concurrently. The interceptor list itself is a copy-on-write snapshot, so adding an interceptor while queries run is safe.
* **Zero cost when unused**: with no interceptor registered, the execution path takes a branch and calls the provider directly - no event objects, timestamps, delegates or async state machines are allocated.

## Limitations

* **Do not change `CommandText`.** A query's command belongs to the cached plan and is reused across executions; rewriting its text would silently desynchronise the cache. `CommandInitialized` is intended for options that do not alter the SQL (a timeout, provider-specific flags).
* **Exception translation is out of scope.** There is no exception interceptor: wrap the explicit terminal call (or the repository method) in a `try`/`catch` to map a provider exception to a domain type. The one gap is deferred/streaming execution, where the failure surfaces during enumeration of an `IAsyncEnumerable` rather than at the call site.
* **No interceptors for the in-memory provider.** [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) has no command or connection, so `AddInterceptor` only affects database-backed contexts.
* **`CommandInitialized` and the shared plan cache.** The plan cache is shared per context type and thread, so a query whose plan was already built by another context raises `CommandInitialized` only on that first build.

## See also

* [Connections and logging](16-connections-and-logging.md)
* [Query reuse: cache vs `Prepare`](15-query-reuse.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [API reference](../advanced/api-reference.md)

---

Source: `src/nextorm.core/Interceptors/IQueryInterceptor.cs`,
`src/nextorm.core/Interceptors/IConnectionInterceptor.cs`,
`src/nextorm.core/DataContext/QueryExecutor.cs`,
`src/nextorm.core/DataContext/DbConnectionManager.cs`,
`tests/nextorm.sqlite.tests/InterceptorTests.cs`.
