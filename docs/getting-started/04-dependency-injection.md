# Dependency injection

> Register NextORM contexts with [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})), configure the provider and logging on a scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder), and resolve a single context instance per scope.

**Prerequisites:** [Installation](01-installation.md) · [Quickstart](02-quickstart.md).

## Overview

DI registration lives in [`NextORM.Core`](xref:NextORM.Core) ([`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions)) and works with any
`Microsoft.Extensions.DependencyInjection` container. There are two registration paths:

* **Type path** - [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder})) registers the concrete context type as scoped and
  forwards [`IDataContext`](xref:NextORM.Core.IDataContext) to it. The container constructs `TContext`, so its constructor must be
  resolvable from the container (for example the parameterless [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)).
* **Options path** - `AddNextOrmContext(Action<DataContextBuilder>)` (or the
  `Action<IServiceProvider, DataContextBuilder>` overload) registers a **scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)** that
  the options delegate configures, plus an [`IDataContext`](xref:NextORM.Core.IDataContext) factory that calls
  `` This is the path used with the provider `Use…` methods.

The guarantees come from the XML docs on [`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions):

* a concrete context type is registered **once per scope**, and resolving the concrete type and
  [`IDataContext`](xref:NextORM.Core.IDataContext) yields the **same instance**;
* the options delegate is **mandatory** for factory-based registration - passing `null` throws
  `ArgumentNullException` at registration time, not at resolution time;
* the [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) itself is scoped, so every scope gets a fresh builder.

Keyed variants ([`AddKeyedNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddKeyedNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder},System.Object))) register the builder and [`IDataContext`](xref:NextORM.Core.IDataContext) under a service key,
so several differently-configured contexts can coexist.

## Registering a context

The generic registration is the shortest form:

```csharp
var services = new ServiceCollection();

services.AddNextOrmContext<InMemoryDataContext>();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var viaInterface = scope.ServiceProvider.GetRequiredService<IDataContext>();
var viaConcrete = scope.ServiceProvider.GetRequiredService<InMemoryDataContext>();
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
services.AddKeyedNextOrmContext<InMemoryDataContext>("audit"); // generic keyed type path
```

```csharp
using var scope = provider.CreateScope();
var reporting = scope.ServiceProvider.GetRequiredKeyedService<IDataContext>("reporting");
```

> **Note:** the options path registers only [`IDataContext`](xref:NextORM.Core.IDataContext) (and the scoped [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)); it does
> not register the concrete [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext)/[`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext)/[`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext) type. Resolve
> [`IDataContext`](xref:NextORM.Core.IDataContext) from the options path. The type path is the one that makes the concrete type and the
> interface resolve to the same instance.

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
* [Documentation index](../index.md)

---

Source: `tests/nextorm.core.tests/DependencyInjectionTests.cs:13`,
`tests/nextorm.core.tests/DependencyInjectionTests.cs:43`,
`tests/nextorm.core.tests/DependencyInjectionTests.cs:58`,
`tests/nextorm.core.tests/DependencyInjectionTests.cs:71`;
`src/nextorm.core/DI/ServiceCollectionExtensions.cs:73`;
`src/nextorm.core/DI/DataContextBuilder.cs:20`;
`src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs:8`;
`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs:8`.
