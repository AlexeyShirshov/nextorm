# Logging

> Wire an `ILoggerFactory` to the [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) to capture the executed SQL, parameter values and connection lifecycle messages.

**Prerequisites:** [Connections](02-connections.md) · [Dependency injection](../getting-started/04-dependency-injection.md) · [Query reuse](01-query-reuse-and-caching.md).

## Overview

Logging is configured per context through the [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder): `UseLoggerFactory(ILoggerFactory)` enables it, and `LogSensitiveData(bool)` decides whether parameter values may appear in the output. The `Use…` provider extensions and the options-based DI registration both return the same builder, so logging configuration is provider-independent.

## Wiring an `ILoggerFactory`

Set a factory on the [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder); the context creates its loggers in the constructor.

```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

var builder = new DataContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var ctx = builder.CreateDataContext();
```

## The three loggers

Three loggers are created from the factory:

| Member | Category | Purpose |
|---|---|---|
| [`Logger`](xref:NextORM.Core.QueryCommand.Logger) | the context type (for example `nextorm.sqlite.SqliteDataContext`) | connection and command messages. Exposed on [`IContextEnvironment`](xref:NextORM.Core.IContextEnvironment). |
| `CommandLogger` | the [`QueryCommand`](xref:NextORM.Core.QueryCommand) type | attached to the commands built by [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) / `From(...)`. Exposed on [`IContextEnvironment`](xref:NextORM.Core.IContextEnvironment). |
| [`ResultSetEnumeratorLogger`](xref:NextORM.Core.InMemoryDataContext.ResultSetEnumeratorLogger) | `NextORM.Core.ResultSetEnumerator` | streaming lifecycle messages (`Trace` on `MoveNext`, `Debug` when opening the connection or disposing the reader). Internal. |

## [`LogSensitiveData`](xref:NextORM.Core.DataContextBuilder.LogSensitiveData(System.Boolean))

`LogSensitiveData(bool)` controls whether parameter values and the connection string are written. It defaults to `false`:

* the SQL is logged at `Debug` as `Executing query: {sql}`;
* with `LogSensitiveData(false)`, a parameterised query additionally logs `Use LogSensitiveData to see param values`;
* with `LogSensitiveData(true)`, every parameter name and value is logged, and connection creation logs `Creating connection with {connStr}` instead of just `Creating connection`.

Turn it on only when the log sink is trusted: parameter values may contain personal data.

## Command logging

Buffered terminals ([`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`ExecuteScalar`](xref:NextORM.Core.QueryCommand`1.ExecuteScalar(System.ReadOnlySpan{System.Object})), ...) log the command through [`Logger`](xref:NextORM.Core.QueryCommand.Logger) before executing it. Streaming terminals use [`ResultSetEnumeratorLogger`](xref:NextORM.Core.InMemoryDataContext.ResultSetEnumeratorLogger), which also emits `Move next` at `Trace`. Both paths are at `Debug`, so the log level of the configured factory must allow `Debug` for anything to appear:

```csharp
var builder = new DataContextBuilder()
    .UseSqlServer(connectionString)
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(true); // includes parameter values in the output
```

## Provider differences

Logging behaves the same across every provider; only the connection type differs. See [Connections: provider differences](02-connections.md#provider-differences).

## See also

* [Connections](02-connections.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Query reuse: plan cache and `Prepare`](01-query-reuse-and-caching.md)
* [Documentation index](../index.md)

---

Source: `src/nextorm.core/DI/DataContextBuilder.cs:20` ([`UseLoggerFactory`](xref:NextORM.Core.DataContextBuilder.UseLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory))).
