# Installation

> Add the `nextorm` core package plus one database-provider package to a `net10.0` project, then use the `nextorm.core` types from code.

**Prerequisites:** A project targeting `net10.0`.

## Overview

NextORM is split into a small, driver-free core package and one package per relational provider:

* `nextorm` (assembly `nextorm.core`) contains the query builder and compiler, entity metadata, the
  dependency-injection helpers, the plan cache **and the in-memory provider** (`InMemoryContext`). It
  references `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`,
  `Microsoft.Extensions.ObjectPool` and `OneOf`, but no database driver.
* `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb` and
  `nextorm.clickhouse` each add one concrete context
  (`SqliteDbContext`, `SqlServerDbContext`, `PostgresDbContext`, `MySqlDbContext`, `MariaDbContext`,
  `ClickHouseDbContext`) and a `Use…` registration extension.
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
| SQLite | `nextorm.sqlite` | `SqliteDbContext`, `UseSqlite` | `Microsoft.Data.Sqlite` |
| SQL Server | `nextorm.sqlserver` | `SqlServerDbContext`, `UseSqlServer` | `Microsoft.Data.SqlClient` |
| PostgreSQL | `nextorm.postgres` | `PostgresDbContext`, `UsePostgres` | `Npgsql` |
| MySQL | `nextorm.mysql` | `MySqlDbContext`, `UseMySql` | `MySqlConnector` |
| MariaDB | `nextorm.mariadb` | `MariaDbContext`, `UseMariaDb` | `MySqlConnector` |
| ClickHouse | `nextorm.clickhouse` | `ClickHouseDbContext`, `UseClickHouse` | `ClickHouse.Driver` |

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
  entity-free `TableAlias` mode.
* [Dependency injection](04-dependency-injection.md) - registering a context with `AddNextOrmContext`
  and the provider `Use…` methods.

---

Source: `docs/index.md:91`; `src/nextorm.core/nextorm.core.csproj:4`;
`src/nextorm.sqlite/nextorm.sqlite.csproj:7`; `src/nextorm.sqlserver/nextorm.sqlserver.csproj:16`;
`src/nextorm.postgres/nextorm.postgres.csproj:8`;
in-memory availability: `test/nextorm.core.tests/DependencyInjectionTests.cs:16`.
