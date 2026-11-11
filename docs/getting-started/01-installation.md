# Installation

> Add the `nextorm` core package plus one database-provider package to a `net10.0` or `net11.0` project, then use the [`NextORM.Core`](xref:NextORM.Core) types from code.

**Prerequisites:** A project targeting `net10.0` or `net11.0`.

## Overview

NextORM is split into a small, driver-free core package and one package per relational provider:

* `nextorm` (assembly `nextorm.core`) contains the query builder and compiler, entity metadata, the
  dependency-injection helpers, the plan cache **and the in-memory provider** ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)). It
  references `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` and
  `Microsoft.Extensions.ObjectPool`, but no database driver.
* `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb` and
  `nextorm.clickhouse` each add one concrete context
  ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext),
  [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext)) and a `Use…` registration extension.
  The provider packages depend on `nextorm` transitively.
* `nextorm.extensions` is an optional product package that adds the query-component operators
  `SelectWhereMin` / `SelectWhereMax` and `SelectGroup` / `SelectGroupHaving` on top of the public
  query-component layer. It references `nextorm.core` only, needs no provider package of its own and
  works with every provider; see [Extensions package](../advanced/extensions-package.md).
* `nextorm.entityframeworkcore` is an optional bridge package that runs nextorm queries and commands
  over an existing EF Core `DbContext` connection and transaction, reusing the EF model mapping
  (`db.CreateNextOrmContext()`); see [EF Core integration](../advanced/integration-efcore.md).

1.0.9 is the first stable release, so the install commands below no longer need `--prerelease`.
Preview builds (`1.0.10-a.x` and later) still do.

## Install the core package

```bash
dotnet add package nextorm
```

Then add exactly one provider package for the database you target:

```bash
dotnet add package nextorm.sqlite
dotnet add package nextorm.sqlserver
dotnet add package nextorm.postgres
dotnet add package nextorm.mysql
dotnet add package nextorm.mariadb
dotnet add package nextorm.clickhouse
```

The `nextorm.extensions` package is optional. Add it when you need the component-based
`SelectWhereMin` / `SelectWhereMax` or the inferred-grouping `SelectGroup` / `SelectGroupHaving`; it
works with any provider:

```bash
dotnet add package nextorm.extensions
```

The `nextorm.entityframeworkcore` package is optional as well — add it when an application already has
an EF Core `DbContext` and you want nextorm queries over the same connection:

```bash
dotnet add package nextorm.entityframeworkcore
```

The Package Manager Console equivalent:

```powershell
Install-Package nextorm
Install-Package nextorm.sqlite
```

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

The packages multi-target `net10.0` and `net11.0`, so NuGet resolves the asset that matches the
consuming project, and enable implicit usings and nullable reference types. A
`PackageReference` looks like this:

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
