# Installation

> Add the `nextorm` core package plus one database-provider package to a `net10.0` project, then use the [`NextORM.Core`](xref:NextORM.Core) types from code.

**Prerequisites:** A project targeting `net10.0`.

## Overview

NextORM is split into a small, driver-free core package and one package per relational provider:

* `nextorm` (assembly `nextorm.core`) contains the query builder and compiler, entity metadata, the
  dependency-injection helpers, the plan cache **and the in-memory provider** ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext)). It
  references `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`,
  `Microsoft.Extensions.ObjectPool` and `OneOf`, but no database driver.
* `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb` and
  `nextorm.clickhouse` each add one concrete context
  ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext),
  [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext)) and a `Use…` registration extension.
  The provider packages depend on `nextorm` transitively.

All current releases are prereleases (`1.0.1-alpha` line), so every install command must opt in to
prerelease versions.

## Install the core package

```bash
dotnet add package nextorm --prerelease
```

Then add exactly one provider package for the database you target:

```bash
dotnet add package nextorm.sqlite --prerelease
dotnet add package nextorm.sqlserver --prerelease
dotnet add package nextorm.postgres --prerelease
dotnet add package nextorm.mysql --prerelease
dotnet add package nextorm.mariadb --prerelease
dotnet add package nextorm.clickhouse --prerelease
```

The Package Manager Console equivalent:

```powershell
Install-Package nextorm -Prerelease
Install-Package nextorm.sqlite -Prerelease
```

## Package reference

| Package | Package ID | Adds | Driver |
|---|---|---|---|
| Core | `nextorm` | Query engine, metadata, DI, in-memory provider | — |
| SQLite | `nextorm.sqlite` | [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext), [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions) | `Microsoft.Data.Sqlite` |
| SQL Server | `nextorm.sqlserver` | [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext), [`UseSqlServer`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions) | `Microsoft.Data.SqlClient` |
| PostgreSQL | `nextorm.postgres` | [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext), [`UsePostgres`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions) | `Npgsql` |
| MySQL | `nextorm.mysql` | [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), [`UseMySql`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions) | `MySqlConnector` |
| MariaDB | `nextorm.mariadb` | [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext), [`UseMariaDb`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions) | `MySqlConnector` |
| ClickHouse | `nextorm.clickhouse` | [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext), [`UseClickHouse`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions) | `ClickHouse.Driver` |

The in-memory provider lives in the core package, so it is available without installing a provider.

## Target framework

The packages target `net10.0` and enable implicit usings and nullable reference types. A
`PackageReference` looks like this (versions are illustrative; use the latest `1.0.1-alpha`
prerelease):

```xml
<ItemGroup>
  <PackageReference Include="nextorm" Version="1.0.1-alpha" />
  <PackageReference Include="nextorm.sqlite" Version="1.0.1-alpha" />
</ItemGroup>
```

## What to read next

* [Quickstart](02-quickstart.md) - a complete minimal program.
* [Entities and metadata](03-entities-and-metadata.md) - attributes, interface/class mapping and the
  entity-free [`TableAlias`](xref:NextORM.Core.TableAlias) mode.
* [Dependency injection](04-dependency-injection.md) - registering a context with [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions)
  and the provider `Use…` methods.

---

Source: `docs/index.md:91`; `src/nextorm.core/nextorm.core.csproj:4`;
`src/nextorm.sqlite/nextorm.sqlite.csproj:7`; `src/nextorm.sqlserver/nextorm.sqlserver.csproj:16`;
`src/nextorm.postgres/nextorm.postgres.csproj:8`;
in-memory availability: `tests/nextorm.core.tests/DependencyInjectionTests.cs:16`.
