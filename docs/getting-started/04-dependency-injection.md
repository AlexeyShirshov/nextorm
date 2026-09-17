# Dependency injection

> Register NextORM contexts with `AddNextOrmContext`, configure the provider and logging on a scoped `DbContextBuilder`, and resolve a single context instance per scope.

**Prerequisites:** [Installation](01-installation.md) · [Quickstart](02-quickstart.md).

## Overview

DI registration lives in `nextorm.core` (`ServiceCollectionExtensions`) and works with any
`Microsoft.Extensions.DependencyInjection` container. There are two registration paths:

* **Type path** - `AddNextOrmContext<TContext>()` registers the concrete context type as scoped and
  forwards `IDataContext` to it. The container constructs `TContext`, so its constructor must be
  resolvable from the container (for example the parameterless `InMemoryContext`).
* **Options path** - `AddNextOrmContext(Action<DbContextBuilder>)` (or the
  `Action<IServiceProvider, DbContextBuilder>` overload) registers a **scoped `DbContextBuilder`** that
  the options delegate configures, plus an `IDataContext` factory that calls
  `DbContextBuilder.CreateDbContext()`. This is the path used with the provider `Use…` methods.

The guarantees come from the XML docs on `ServiceCollectionExtensions`:

* a concrete context type is registered **once per scope**, and resolving the concrete type and
  `IDataContext` yields the **same instance**;
* the options delegate is **mandatory** for factory-based registration - passing `null` throws
  `ArgumentNullException` at registration time, not at resolution time;
* the `DbContextBuilder` itself is scoped, so every scope gets a fresh builder.

Keyed variants (`AddKeyedNextOrmContext`) register the builder and `IDataContext` under a service key,
so several differently-configured contexts can coexist.

## Registering a context

The generic registration is the shortest form:

```csharp
var services = new ServiceCollection();

services.AddNextOrmContext<InMemoryContext>();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var viaInterface = scope.ServiceProvider.GetRequiredService<IDataContext>();
var viaConcrete = scope.ServiceProvider.GetRequiredService<InMemoryContext>();
// viaInterface and viaConcrete are the same instance
```

To configure a database provider, use the options overload:

```csharp
services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

The `IServiceProvider` overload lets you pull dependencies such as `ILoggerFactory` out of the
container while configuring:

```csharp
services.AddNextOrmContext((sp, builder) =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    builder
        .UseSqlServer(connectionString)
        .UseLoggerFactory(loggerFactory);
});
```

A keyed registration keeps multiple contexts apart:

```csharp
services.AddKeyedNextOrmContext(builder => builder.UsePostgres(reportingConnectionString), "reporting");
services.AddKeyedNextOrmContext<InMemoryContext>("audit"); // generic keyed type path
```

```csharp
using var scope = provider.CreateScope();
var reporting = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("reporting");
```

> **Note:** the options path registers only `IDataContext` (and the scoped `DbContextBuilder`); it does
> not register the concrete `SqliteDbContext`/`SqlServerDbContext`/`PostgresDbContext` type. Resolve
> `IDataContext` from the options path. The type path is the one that makes the concrete type and the
> interface resolve to the same instance.

## Configuring the builder directly

`DbContextBuilder` can also be used without a container - this is exactly what the DI factory calls:

```csharp
var builder = new DbContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var dataContext = builder.CreateDbContext();
// Without a Use… call (or an assigned Factory) CreateDbContext() throws
// InvalidOperationException("Context is not set").
```

| Member | Purpose |
|---|---|
| `Factory` | `Func<DbContextBuilder, IDataContext>`; set by the `Use…` extensions, or assign your own to bypass providers. |
| `UseLoggerFactory(ILoggerFactory)` | Creates the context, command and enumerator loggers from the factory. |
| `LogSensitiveData(bool)` | Controls whether parameter values are written to the logs. |
| `CreateDbContext()` | Invokes `Factory`; throws `InvalidOperationException("Context is not set")` when no provider was configured. |

The provider extensions (`UseSqlite`/`UseSqlServer`/`UsePostgres`, in the provider packages) each accept
either a connection string or a `DbConnection`:

```csharp
builder.UseSqlite("app.db");              // context creates and owns the connection
builder.UseSqlite(connection);            // context reuses the supplied connection
builder.UseSqlServer("Server=localhost;Database=app;Trusted_Connection=True;");
builder.UsePostgres("Host=localhost;Database=app");
```

A context created from a supplied connection leaves that connection open when the context is disposed;
a context that created its own connection closes it. In `DEBUG` builds `UseSqlite(string)` also
verifies that the file exists.

## Lifetimes

| Registration | Service | Lifetime | Notes |
|---|---|---|---|
| `AddNextOrmContext<T>()` | `T` | scoped | once per scope |
| `AddNextOrmContext<T>()` | `IDataContext` | scoped | forwards to the same `T` instance |
| `AddNextOrmContext(Action<…>)` | `DbContextBuilder` | scoped | built fresh per scope |
| `AddNextOrmContext(Action<…>)` | `IDataContext` | scoped | `builder.CreateDbContext()` |
| `AddKeyedNextOrmContext(…, key)` | keyed `IDataContext` / `DbContextBuilder` | scoped | resolved with the key |

Because `IDataContext` is scoped and implements `IDisposable`/`IAsyncDisposable`, disposing the scope
disposes the context (and, for contexts that own it, the connection). Do not resolve contexts from the
root provider - always create a scope.

## Provider differences

`UseLoggerFactory` and `LogSensitiveData` behave identically across providers; only the `Use…` extension
that selects the provider differs.

| Provider | Extension | Overloads |
|---|---|---|
| SQLite | `UseSqlite` | `string filepath`, `DbConnection` |
| SQL Server | `UseSqlServer` | `string connectionString`, `DbConnection` |
| PostgreSQL | `UsePostgres` | `string connectionString`, `DbConnection` |
| In-memory | none | register `InMemoryContext` directly or assign `Factory` |

## See also

* [Installation](01-installation.md)
* [Quickstart](02-quickstart.md)
* [Entities and metadata](03-entities-and-metadata.md)
* [Documentation index](../index.md)

---

Source: `test/nextorm.core.tests/DependencyInjectionTests.cs:13`,
`test/nextorm.core.tests/DependencyInjectionTests.cs:43`,
`test/nextorm.core.tests/DependencyInjectionTests.cs:58`,
`test/nextorm.core.tests/DependencyInjectionTests.cs:71`;
`src/nextorm.core/DI/ServiceCollectionExtensions.cs:73`;
`src/nextorm.core/DI/DataContextOptionsBuilder.cs:20`;
`src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.sqlserver/DI/DataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs:8`.
