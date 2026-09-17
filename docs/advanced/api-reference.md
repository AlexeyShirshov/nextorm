# API reference

> A curated index of nextorm's public types, grouped by namespace, with the source file that owns each one.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Provider overview](../providers/overview.md)

## Overview

This is a **curated** index; the reference generated from the XML doc comments on the source types is
published in the **API reference** section of this site (see the top navigation). Each entry below gives the
type, a one-line description and the source path to read for the full contract.

Types are listed under their defining namespace. All of the query API is in `nextorm.core`; each provider
package adds a context, a dialect and a `DbContextBuilder` extension class in its own namespace.

## Namespace `nextorm.core`

### Context and roles

| Type | Description | Source |
|---|---|---|
| `IDataContext` | Composite facade over the context roles; the entry point consumers normally depend on. | `src/nextorm.core/DataContext/IDataContext.cs` |
| `IQueryExecutor` | Executes a prepared command and materialises its result (terminals). | `src/nextorm.core/DataContext/Roles/IQueryExecutor.cs` |
| `IQueryMaterializer` | Narrowest contract for planning + row reading, without terminals. | `src/nextorm.core/DataContext/Roles/IQueryMaterializer.cs` |
| `IQueryPlanner` | Builds/resets execution plans and resolves `From` sources. | `src/nextorm.core/DataContext/Roles/IQueryPlanner.cs` |
| `IRowReaderFactory` | Creates row readers/enumerators over a prepared command. | `src/nextorm.core/DataContext/Roles/IRowReaderFactory.cs` |
| `IQueryCache` | Holds cached plans and the shared `Any` plan. | `src/nextorm.core/DataContext/Roles/IQueryCache.cs` |
| `IContextEnvironment` | Ambient state: loggers, mapping mode, property bag. | `src/nextorm.core/DataContext/Roles/IContextEnvironment.cs` |
| `IConnectionManager` | Owns the connection lifecycle (not part of `IDataContext`; the in-memory provider does not implement it). | `src/nextorm.core/DataContext/Roles/IConnectionManager.cs` |
| `InMemoryContext` | Built-in in-memory `IDataContext` over CLR collections. | `src/nextorm.core/DataContext/InMemoryDataContext.cs` |

### Query builders

| Type | Description | Source |
|---|---|---|
| `Entity<TEntity>` | Fluent, immutable query builder for a mapped entity type. | `src/nextorm.core/Builders/Entity.cs` |
| `Entity` | Fluent builder for an alias/table source that has no entity type (`TableAlias` mode). | `src/nextorm.core/Builders/Entity.cs` |
| `EntityP2<T1,T2>` … `EntityP8<T1..T8>` | Accumulated join builders; arity 2 through 8. | `src/nextorm.core/Builders/Joins/JoinCommandBuilder.cs` |
| `EntityBuilder<T>` | Fluent entity-metadata configuration used by `IDataContext.Create<T>(...)`. | `src/nextorm.core/DataContext/Meta/EntityBuilder.cs` |
| `Projection<T1,T2>` … `Projection<T1..T8>` | Result shape of a joined query; exposes `t1`…`t8`. | `src/nextorm.core/Builders/Projection.cs` |
| `IProjection` / `IExtendableProjection` | Markers for accumulated join projections (arity 8 is not extendable). | `src/nextorm.core/Builders/Projection.cs` |
| `CteQuery` | Fluent scope collecting `WITH` declarations. | `src/nextorm.core/Builders/CteQuery.cs` |
| `CteDefinition` | One CTE: name, defining query, recursive flag and optional max recursion. | `src/nextorm.core/Builders/CteQuery.cs` |
| `TableAlias` / `TableColumn` | Alias-mode column accessors and typed column wrapper (`AsInt`, `AsString`, …). | `src/nextorm.core/Builders/TableAlias.cs` |
| `Paging` | `Limit` / `Offset` value used by every query builder. | `src/nextorm.core/Builders/Paging.cs` |

### Commands, plans and functions

| Type | Description | Source |
|---|---|---|
| `QueryCommand` | Non-generic query command holding the plan/state shared by all results. | `src/nextorm.core/Query/QueryCommand.cs` |
| `QueryCommand<TResult>` | Typed query command with terminals (`ToList`, `First`, `Union`, `Distinct`, `Prepare`, …). | `src/nextorm.core/Query/QueryCommand.TResult.cs` |
| `IPreparedQueryCommand<TResult>` | Prepared command; its default members execute it against a supplied `IDataContext`. | `src/nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs` |
| `NORM` | Static entry point: `NORM.SQL` and `NORM.Param<T>(idx)`. | `src/nextorm.core/Query/NORM.cs` |
| `NORM_SQL` | SQL function surface: `exists`, `like`, `@in`, `any`/`all`, aggregates, window functions. | `src/nextorm.core/Query/NORM.cs` |
| `NORM.WindowFunction<T>` | Unfinished window call; complete it with `Over(...)`. | `src/nextorm.core/Query/NORM.cs` |
| `NORM.WindowOrder` | An ordered window key plus `OrderDirection`. | `src/nextorm.core/Query/NORM.cs` |
| `NORM.WindowFrame`, `WindowFrameBound`, `WindowFrameType`, `WindowFrameBoundKind` | `ROWS`/`RANGE` frame specification and its boundaries. | `src/nextorm.core/Query/NORM.cs` |
| `DataContextExtensions` | Provider-independent helpers: `Create<T>`, `From`, `FromTableFunction`, prepared-command terminals. | `src/nextorm.core/DataContext/DataContextExtensions.cs` |
| `IDataContextExtensions` | CTE entry points `With` / `WithRecursive`. | `src/nextorm.core/DataContext/IDataContextExtensions.cs` |

### Mapping attributes

| Type | Description | Source |
|---|---|---|
| `SqlTableAttribute` | Maps a class or interface to a table name (`[SqlTable("name")]`). | `src/nextorm.core/TableAttribute.cs` |
| `SqlFunctionAttribute` | Maps a CLR method (or its declaring type) to a scalar database function; optional `Name`/`Schema`. | `src/nextorm.core/SqlFunctionAttribute.cs` |
| `SqlTableFunctionAttribute` | Maps a static method (or its declaring type) to a table-valued function used as a `FROM` source. | `src/nextorm.core/SqlTableFunctionAttribute.cs` |

### Supporting expression types

| Type | Description | Source |
|---|---|---|
| `OrderDirection` | `Asc` / `Desc`. | `src/nextorm.core/Expressions/OrderDirection.cs` |
| `JoinType` | `Inner`, `Left`, `Right`, `Full`, `Cross`, `FullCross`. | `src/nextorm.core/Expressions/JoinExpression.cs` |
| `JoinExpression` | A single join: condition, type and joined source. | `src/nextorm.core/Expressions/JoinExpression.cs` |
| `FromExpression` / `SelectExpression` | FROM source and projected column metadata. | `src/nextorm.core/Expressions/FromExpression.cs`, `src/nextorm.core/Expressions/SelectExpression.cs` |
| `UnionType` | `None`, `Distinct`, `All`, `Intersect`, `IntersectAll`, `Except`, `ExceptAll`. | `src/nextorm.core/Expressions/UnionType.cs` |

### Dependency injection

| Type | Description | Source |
|---|---|---|
| `DbContextBuilder` | Provider options builder: `UseLoggerFactory`, `LogSensitiveData`, `Factory`, `CreateDbContext`. | `src/nextorm.core/DI/DataContextOptionsBuilder.cs` |
| `ServiceCollectionExtensions` | `AddNextOrmContext` / `AddKeyedNextOrmContext` (generic and options-driven). | `src/nextorm.core/DI/ServiceCollectionExtensions.cs` |

## Namespace `nextorm.sqlite`

| Type | Description | Source |
|---|---|---|
| `SqliteDbContext` | `DbContext` over `Microsoft.Data.Sqlite`; registers the custom aggregates. | `src/nextorm.sqlite/SqliteDbContext.cs` |
| `SqliteDialect` | SQLite `ISqlDialect` singleton (`SqliteDialect.Instance`). | `src/nextorm.sqlite/SqliteDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UseSqlite(string filepath)` and `UseSqlite(DbConnection)`. | `src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs` |

## Namespace `nextorm.postgres`

| Type | Description | Source |
|---|---|---|
| `PostgresDbContext` | `DbContext` over `Npgsql`. | `src/nextorm.postgres/PostgresDbContext.cs` |
| `PostgresDialect` | PostgreSQL `ISqlDialect` singleton (`PostgresDialect.Instance`). | `src/nextorm.postgres/PostgresDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UsePostgres(string connectionString)` and `UsePostgres(DbConnection)`. | `src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs` |

## Namespace `nextorm.sqlserver`

| Type | Description | Source |
|---|---|---|
| `SqlServerDbContext` | `DbContext` over `Microsoft.Data.SqlClient`, with numeric column conversion. | `src/nextorm.sqlserver/SqlServerDbContext.cs` |
| `SqlServerDialect` | SQL Server `ISqlDialect` singleton (`SqlServerDialect.Instance`). | `src/nextorm.sqlserver/SqlServerDialect.cs` |
| `DataContextOptionsBuilderExtensions` | `UseSqlServer(string connectionString)` and `UseSqlServer(DbConnection)`. | `src/nextorm.sqlserver/DI/DataContextOptionsBuilderExtensions.cs` |

## See also

- [Provider overview](../providers/overview.md)
- [SQLite](../providers/sqlite.md)
- [SQL Server](../providers/sqlserver.md)
- [PostgreSQL](../providers/postgres.md)
- [In-memory](../providers/in-memory.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.sqlite/**`, `src/nextorm.postgres/**`,
`src/nextorm.sqlserver/**` (XML doc comments are the authoritative API documentation).
