# Dependency injection

> Register NextORM contexts with [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext``1(Microsoft.Extensions.DependencyInjection.IServiceCollection)), configure the provider and logging on a scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder), and resolve a single context instance per scope.

**Prerequisites:** [Installation](01-installation.md) · [Quickstart](02-quickstart.md).

## Overview

Use the options path to configure a SQL provider with `Use…`. Use the type path when the container can
construct the context directly, as it can with
[`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext). Both paths are available through
[`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions) in
[`NextORM.Core`](xref:NextORM.Core), and work with any
`Microsoft.Extensions.DependencyInjection` container.

The options path registers a scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) and an
[`IDataContext`](xref:NextORM.Core.IDataContext) factory. The factory calls
[`CreateDataContext`](xref:NextORM.Core.DataContextBuilder.CreateDataContext). The type path registers
the concrete context as scoped and forwards `IDataContext` to that instance. The container constructs
`TContext`, so it must be able to resolve the context's constructor.

These registrations guarantee:

* a concrete context type is registered **once per scope**, and resolving the concrete type and
  [`IDataContext`](xref:NextORM.Core.IDataContext) yields the **same instance**;
* the options delegate is **mandatory** for factory-based registration - passing `null` throws
  `ArgumentNullException` at registration time, not at resolution time;
* the [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) itself is scoped, so every scope gets a fresh builder.

Keyed variants ([`AddKeyedNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddKeyedNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder},System.Object))) register the builder and [`IDataContext`](xref:NextORM.Core.IDataContext) under a service key,
so several differently-configured contexts can coexist.

## Registering a context

For a SQL provider, configure the builder in the options delegate:

```csharp
var services = new ServiceCollection();

services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));

```

Use the `IServiceProvider` overload when the provider configuration needs another service, such as an
`ILoggerFactory`:

```csharp
services.AddNextOrmContext((sp, builder) =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    builder
        .UseSqlServer(connectionString)
        .UseLoggerFactory(loggerFactory);
});
```

Use keyed registrations when an application needs multiple differently configured contexts:

```csharp
services.AddKeyedNextOrmContext(builder => builder.UsePostgres(reportingConnectionString), "reporting");
services.AddKeyedNextOrmContext<InMemoryDataContext>("audit"); // generic keyed type path
```

Build the provider after registering both contexts, then resolve a keyed context from a scope:

```csharp
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var reporting = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("reporting");
```

The options path registers only [`IDataContext`](xref:NextORM.Core.IDataContext) and the scoped
[`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder). It does not register a concrete provider
type such as [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext),
[`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), or
[`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext). Resolve `IDataContext` from this
path.

For a context the container can construct directly, use the type path. Resolving the concrete type and
`IDataContext` in the same scope returns the same instance:

```csharp
var typeServices = new ServiceCollection();
typeServices.AddNextOrmContext<InMemoryDataContext>();

using var typeProvider = typeServices.BuildServiceProvider();
using var typeScope = typeProvider.CreateScope();

var viaInterface = typeScope.ServiceProvider.GetRequiredService<IDataContext>();
var viaConcrete = typeScope.ServiceProvider.GetRequiredService<InMemoryDataContext>();
// viaInterface and viaConcrete are the same instance
```

## DI-registered interceptors

An interceptor can be registered by its service type instead of a pre-built instance; the builder resolves it from the scope that created the builder:

```csharp
services.AddScoped<TimingInterceptor>();

services.AddNextOrmContext(builder => builder
    .UseSqlite("app.db")
    .AddInterceptor<TimingInterceptor>()); // or .AddInterceptor(typeof(TimingInterceptor))
```

Resolution happens once per context creation, so scoped, singleton and transient lifetimes are honoured and the container keeps ownership of disposal. A type registration requires a DI-backed builder - it fails at `CreateDataContext()` on a standalone builder, and a service that is not registered fails with the container's own `GetRequiredService` error before the context factory runs. See [Interceptors](../infrastructure/02-interceptors.md) for ordering, deduplication and the error contract.

## Configuring the builder directly

[`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) can also be used without a container - this is exactly what the DI factory calls:

```csharp
var builder = new DataContextBuilder()
    .UseSqlite("app.db")
    .UseLoggerFactory(loggerFactory)
    .LogSensitiveData(false);

using var dataContext = builder.CreateDataContext();
// Without a Use… call (or an assigned Factory) CreateDataContext() throws
// InvalidOperationException("Context is not set").
```

| Member | Purpose |
|---|---|
| [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory) | `Func<DataContextBuilder, IDataContext>`; set by the `Use…` extensions, or assign your own to bypass providers. |
| `UseLoggerFactory(ILoggerFactory)` | Creates the context, command and enumerator loggers from the factory. |
| `LogSensitiveData(bool)` | Controls whether parameter values are written to the logs. |
| [`CreateDataContext`](xref:NextORM.Core.DataContextBuilder.CreateDataContext) | Invokes [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory); throws `InvalidOperationException("Context is not set")` when no provider was configured. |

The provider extensions ([`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection))/[`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions.UseSqlServer(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection))/[`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions.UsePostgres(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)), in the provider packages) each accept
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
| [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})) | `T` | scoped | once per scope |
| [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})) | [`IDataContext`](xref:NextORM.Core.IDataContext) | scoped | forwards to the same `T` instance |
| `AddNextOrmContext(Action<…>)` | [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) | scoped | built fresh per scope |
| `AddNextOrmContext(Action<…>)` | [`IDataContext`](xref:NextORM.Core.IDataContext) | scoped | `builder.CreateDataContext()` |
| `AddKeyedNextOrmContext(…, key)` | keyed [`IDataContext`](xref:NextORM.Core.IDataContext) / [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) | scoped | resolved with the key |

Because [`IDataContext`](xref:NextORM.Core.IDataContext) is scoped and implements `IDisposable`/`IAsyncDisposable`, disposing the scope
disposes the context (and, for contexts that own it, the connection). Do not resolve contexts from the
root provider - always create a scope.

## Provider differences

[`UseLoggerFactory`](xref:NextORM.Core.DataContextBuilder.UseLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory)) and [`LogSensitiveData`](xref:NextORM.Core.DataContextBuilder.LogSensitiveData(System.Boolean)) behave identically across providers; only the `Use…` extension
that selects the provider differs.

| Provider | Extension | Overloads |
|---|---|---|
| SQLite | [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string filepath`, `DbConnection` |
| SQL Server | [`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions.UseSqlServer(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| PostgreSQL | [`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions.UsePostgres(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| MySQL | [`UseMySql`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions.UseMySql(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| MariaDB | [`UseMariaDb`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions.UseMariaDb(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| ClickHouse | [`UseClickHouse`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions.UseClickHouse(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `string connectionString`, `DbConnection` |
| In-memory | none | register [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) directly or assign [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory) |

## See also

* [Installation](01-installation.md)
* [Quickstart](02-quickstart.md)
* [Entities and metadata](03-entities-and-metadata.md)
* [Logging](../infrastructure/04-logging.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:13`,
`tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:43`,
`tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:58`,
`tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:71`;
`src/nextorm.core/DI/ServiceCollectionExtensions.cs:73`;
`src/nextorm.core/DI/DataContextBuilder.cs:20`;
`src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs:8`.
