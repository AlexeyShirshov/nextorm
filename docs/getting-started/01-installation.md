# Installation

Install NextORM and the database provider packages your application needs, then prepare to follow the [quickstart](02-quickstart.md). The quickstart uses SQLite. This page covers package installation; it doesn't configure a live database or teach query syntax.

## Before you begin

- A project that targets `net10.0` or `net11.0`.
- A terminal opened in the directory that contains the project file.

## Install the core package

From that directory, add the core package:

```bash
dotnet add package nextorm
```

Add the provider package for the database you use. For the quickstart, use SQLite:

```bash
dotnet add package nextorm.sqlite
```

To follow the quickstart, the project file must reference `nextorm` and `nextorm.sqlite`. For an
application that uses other databases, add the corresponding provider package or packages. See the
[package reference](#package-reference) for the supported provider IDs.

If you use Visual Studio's Package Manager Console, select the application project and run:

```powershell
Install-Package nextorm
Install-Package nextorm.sqlite
```

## Optional packages

The core and provider packages are enough for the quickstart. Add `nextorm.extensions` only when you
need its query-component operators:

```bash
dotnet add package nextorm.extensions
```

This package adds `SelectWhereMin` / `SelectWhereMax` and `SelectGroup` / `SelectGroupHaving`. It
works with every provider; see [Extensions package](../advanced/extensions-package.md).

If your application already uses an Entity Framework Core `DbContext` and you want NextORM to use the
same connection and transaction, add the optional bridge:

```bash
dotnet add package nextorm.entityframeworkcore
```

See [EF Core integration](../advanced/integration-efcore.md) for details.

To generate mappings for an existing database, use the separate scaffolding tool, not a package
reference in your app. See [Generate models from an existing database](05-existing-database.md).

## Overview

NextORM separates its query engine from database drivers:

* `nextorm` (assembly `nextorm.core`) contains the query builder and compiler, entity metadata,
  dependency-injection helpers, the plan cache, and the in-memory provider
  ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)). It references
  `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, and
  `Microsoft.Extensions.ObjectPool`, but no database driver.
* Each provider package adds a context and a `Use…` registration extension. Provider packages depend
  on `nextorm` transitively.

Version `1.0.9` is the first stable release, so the stable package commands don't need
`--prerelease`. Preview builds (`1.0.10-a.x` and later) still do.

## Package reference

| Package | Package ID | Adds | Driver |
|---|---|---|---|
| Core | `nextorm` | Query engine, metadata, DI, in-memory provider | — |
| SQLite | `nextorm.sqlite` | [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `Microsoft.Data.Sqlite` |
| SQL Server | `nextorm.sqlserver` | [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions.UseSqlServer(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `Microsoft.Data.SqlClient` |
| PostgreSQL | `nextorm.postgres` | [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions.UsePostgres(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `Npgsql` |
| MySQL | `nextorm.mysql` | [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`UseMySql`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions.UseMySql(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `MySqlConnector` |
| MariaDB | `nextorm.mariadb` | [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext), [`UseMariaDb`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions.UseMariaDb(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `MySqlConnector` |
| ClickHouse | `nextorm.clickhouse` | [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext), [`UseClickHouse`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions.UseClickHouse(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) | `ClickHouse.Driver` |
| Extensions (optional) | `nextorm.extensions` | [`SelectWhereMin`](xref:NextORM.Extensions.ExtremeRowExtensions) / [`SelectWhereMax`](xref:NextORM.Extensions.ExtremeRowExtensions) and [`SelectGroup`](xref:NextORM.Extensions.SelectGroupExtensions) / [`SelectGroupHaving`](xref:NextORM.Extensions.SelectGroupExtensions), on top of `nextorm.core` | — |
| EF Core (optional) | `nextorm.entityframeworkcore` | [`CreateNextOrmContext`](xref:NextORM.EntityFrameworkCore.EntityFrameworkCoreExtensions.CreateNextOrmContext(Microsoft.EntityFrameworkCore.DbContext,System.Action{NextORM.Core.DataContextBuilder})) — nextorm queries over an existing `DbContext` connection and transaction | `Microsoft.EntityFrameworkCore.Relational` |

The in-memory provider lives in the core package, so it is available without installing a provider.

## Target framework

The packages support `net10.0` and `net11.0`; NuGet selects the assembly that matches your project's
target framework. The examples assume that implicit usings and nullable reference types are enabled.
If your project doesn't already set these properties, add this property group to its `.csproj` file:

```xml
<PropertyGroup>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

A `PackageReference` looks like this:

```xml
<ItemGroup>
  <PackageReference Include="nextorm" Version="1.0.9" />
  <PackageReference Include="nextorm.sqlite" Version="1.0.9" />
</ItemGroup>
```

For a preview build, float to the newest prerelease with `Version="*-*"`.

## What to read next

* [Quickstart](02-quickstart.md) - a complete minimal program.
* [Entities and metadata](03-entities-and-metadata.md) - attributes, interface/class mapping and the
  entity-free [`TableAlias`](xref:NextORM.Core.TableAlias) mode.
* [Dependency injection](04-dependency-injection.md) - registering a context with [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions.AddNextOrmContext(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{NextORM.Core.DataContextBuilder}))
  and the provider `Use…` methods.
* [Extensions package](../advanced/extensions-package.md) - the optional
  `nextorm.extensions` operators and when to use them instead of the built-in ones.

---

Source: `docs/index.md:91`; `src/nextorm.core/nextorm.core.csproj:4`;
`src/nextorm.sqlite/nextorm.sqlite.csproj:7`; `src/nextorm.sqlserver/nextorm.sqlserver.csproj:16`;
`src/nextorm.postgres/nextorm.postgres.csproj:8`;
in-memory availability: `tests/nextorm.core.tests/DependencyInjection/DependencyInjectionTests.cs:16`.
