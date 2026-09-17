# Connections and logging

> See how NextORM acquires, reuses and disposes the database connection, and how to wire an `ILoggerFactory` to capture executed SQL, parameters and connection lifecycle messages.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md) · [Query reuse](15-query-reuse.md).

## Overview

The SQL providers (`SqliteDbContext`, `SqlServerDbContext`, `PostgresDbContext`) all derive from `DbContext`, which implements both `IDataContext` and `IConnectionManager`. A context is built either from a connection string (the context creates and owns the connection) or from an already-built `DbConnection` (the caller owns it). The in-memory provider implements only `IDataContext` - it has no connection at all.

Logging is configured per context through the `DbContextBuilder`: `UseLoggerFactory(ILoggerFactory)` enables it, and `LogSensitiveData(bool)` decides whether parameter values may appear in the output.

## Connection string vs supplied connection

Every SQL provider exposes two constructors, and the `Use…` builder extensions wrap them:

```csharp
// A: the context creates and owns the connection from the string.
using var owned = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());

// B: the caller owns the connection; the context reuses this exact instance.
using var supplied = new SqliteConnection("Data Source=app.db");
using var reused = new SqliteDbContext(supplied, new DbContextBuilder());

// Equivalent through the builder:
var builderA = new DbContextBuilder().UseSqlite("app.db");
var builderB = new DbContextBuilder().UseSqlite(supplied);
```

`ConnectionString` returns the string the context was built with, or the supplied connection's own `ConnectionString` when the context was built from a connection:

```csharp
using var ctx = new SqliteDbContext("Data Source=:memory:", new DbContextBuilder());
ctx.ConnectionString; // "Data Source=:memory:"

using var supplied = new SqliteConnection("Data Source=:memory:");
using var ctx2 = new SqliteDbContext(supplied, new DbContextBuilder());
ctx2.ConnectionString; // supplied.ConnectionString
```

## Connection reuse

`GetConnection()` creates the connection on first access and returns the **same** instance on every subsequent call. `EnsureConnectionOpen()` opens it if it is closed; `EnsureConnectionOpenAsync(CancellationToken)` is the cancellable asynchronous form.

```csharp
using var ctx = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());

ctx.EnsureConnectionOpen();

bool same = ReferenceEquals(ctx.GetConnection(), ctx.GetConnection()); // true
```

Because the connection is reused, a supplied connection is the way to keep a per-connection database alive. A SQLite `:memory:` database is scoped to its connection, so a table created through a supplied connection is visible to the context only because the context runs on that very connection:

```csharp
using var supplied = new SqliteConnection("Data Source=:memory:");
supplied.Open();

using (var setup = supplied.CreateCommand())
{
    setup.CommandText = "create table simple_entity (id integer primary key);" +
                        "insert into simple_entity (id) values (42);";
    setup.ExecuteNonQuery();
}

using var ctx = new SqliteDbContext(supplied, new DbContextBuilder());
var rows = ctx.Create<ISimpleEntity>().Select(x => x.Id).ToList(); // [42]
```

## Disposal rules

* An **owned** connection (created from a connection string) is disposed by `Dispose()` / `DisposeAsync()`. Cached prepared commands that were bound to it are reset so they rebind on the next use.
* A **supplied** connection is never disposed by the context - it is left in whatever state it was in. The caller owns it.

```csharp
var owned = new SqliteDbContext("Data Source=:memory:", new DbContextBuilder());
var ownedConn = owned.GetConnection();
owned.EnsureConnectionOpen();
owned.Dispose();
// ownedConn.State == ConnectionState.Closed

using var supplied = new SqliteConnection("Data Source=:memory:");
supplied.Open();
using (var ctx = new SqliteDbContext(supplied, new DbContextBuilder()))
{
    ctx.EnsureConnectionOpen();
}
// supplied.State == ConnectionState.Open
```

Because `IDataContext` is registered as scoped in DI and implements `IDisposable`/`IAsyncDisposable`, disposing the scope disposes an owned connection automatically. See [Dependency injection](../getting-started/04-dependency-injection.md).

## `IConnectionManager`

Connection lifecycle is a separate role, deliberately not part of `IDataContext`, so a provider without a connection (the in-memory context) is not forced to implement a no-op:

```csharp
public interface IConnectionManager
{
    DbConnection GetConnection();
    void EnsureConnectionOpen();
    Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default);
}
```

`DbContext` implements it, so casting a context yields the same connection as the public `GetConnection()`:

```csharp
using var ctx = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());
bool same = ReferenceEquals(((IConnectionManager)ctx).GetConnection(), ctx.GetConnection()); // true
```

## Logging

### Wiring an `ILoggerFactory`

Set a factory on the `DbContextBuilder`; the context creates its loggers in the constructor. The `Use…` provider extensions and the options-based DI registration both return the same builder, so logging configuration is provider-independent.

```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

var builder = new DbContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var ctx = builder.CreateDbContext();
```

Three loggers are created from the factory:

| Member | Category | Purpose |
|---|---|---|
| `Logger` | the context type (for example `nextorm.sqlite.SqliteDbContext`) | connection and command messages. Exposed on `IContextEnvironment`. |
| `CommandLogger` | the `QueryCommand` type | attached to the commands built by `Create<T>()` / `From(...)`. Exposed on `IContextEnvironment`. |
| `ResultSetEnumeratorLogger` | `nextorm.core.ResultSetEnumerator` | streaming lifecycle messages (`Trace` on `MoveNext`, `Debug` when opening the connection or disposing the reader). Internal. |

### `LogSensitiveData`

`LogSensitiveData(bool)` controls whether parameter values and the connection string are written. It defaults to `false`:

* the SQL is logged at `Debug` as `Executing query: {sql}`;
* with `LogSensitiveData(false)`, a parameterised query additionally logs `Use LogSensitiveData to see param values`;
* with `LogSensitiveData(true)`, every parameter name and value is logged, and connection creation logs `Creating connection with {connStr}` instead of just `Creating connection`.

Turn it on only when the log sink is trusted: parameter values may contain personal data.

### Command logging

Buffered terminals (`ToList`, `First`, `ExecuteScalar`, ...) log the command through `Logger` before executing it. Streaming terminals use `ResultSetEnumeratorLogger`, which also emits `Move next` at `Trace`. Both paths are at `Debug`, so the log level of the configured factory must allow `Debug` for anything to appear:

```csharp
var builder = new DbContextBuilder()
    .UseSqlServer(connectionString)
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(true); // includes parameter values in the output
```

## Provider differences

| Provider | Connection type | Notes |
|---|---|---|
| SQLite | `SqliteConnection` | `OnConnectionCreated` registers the custom aggregate functions (`stdev`, `stdevp`, `var`, `varp`) for both created and supplied connections. |
| SQL Server | `SqlConnection` | No connection callback; a null parameter value is sent as `DBNull`. |
| PostgreSQL | `NpgsqlConnection` | No connection callback; a null parameter value is sent as `DBNull`. |
| In-memory | none | `InMemoryContext` implements `IDataContext` only and does not expose `GetConnection`/`IConnectionManager`. |

Logging behaves the same across providers; only the connection type differs.

## See also

* [Query reuse: plan cache and `Prepare`](15-query-reuse.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Quickstart](../getting-started/02-quickstart.md)
* [Documentation index](../index.md)

---

Source: `test/nextorm.sqlite.tests/ConnectionManagementTests.cs:24` (created connection),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:32` (stable across calls),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:40` (supplied instance),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:49` (supplied stays open),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:63` (owned is disposed),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:76` / `:84` (`ConnectionString`),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:93` (`IConnectionManager`),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:101` (SQLite functions on a supplied connection),
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:116` (queries use the supplied connection);
`src/nextorm.core/DataContext/Roles/IConnectionManager.cs:10`,
`src/nextorm.core/DataContext/Roles/IContextEnvironment.cs:8`,
`src/nextorm.core/DataContext/DbContext.cs:106` (`GetConnection`),
`src/nextorm.core/DataContext/DbContext.cs:176` (`ConnectionString`),
`src/nextorm.core/DI/DataContextOptionsBuilder.cs:20` (`UseLoggerFactory`).
