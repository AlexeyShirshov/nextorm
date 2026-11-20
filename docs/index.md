# Nextorm - high performance zero-sql object-relational mapping (ORM) library

Use this documentation if you build .NET applications and want to query a database with typed C# expressions. The path below takes you from package installation to a working SQLite query, then shows how to filter rows, choose returned fields, and join tables. It focuses on NextORM's query API, not on database design or SQL fundamentals.

## Start here

1. [Install NextORM and a database provider](getting-started/01-installation.md).
2. [Run your first query](getting-started/02-quickstart.md).
3. [Filter rows](guide/03-filtering-where.md).
4. [Select the fields you need](guide/02-projections.md).
5. [Combine tables with a join](guide/04-joins.md).

Already have a working project? Use the documentation catalog below to open a topic directly.

## Documentation

### Getting started

- [Installation](getting-started/01-installation.md)
- [Quickstart](getting-started/02-quickstart.md)
- [Entities and metadata](getting-started/03-entities-and-metadata.md)
- [Dependency injection](getting-started/04-dependency-injection.md)

### Guide

- [Query sources](guide/01-query-sources.md)
- [Projections](guide/02-projections.md)
- [Filtering (WHERE)](guide/03-filtering-where.md)
- [Joins](guide/04-joins.md)
- [Grouping and aggregates](guide/05-grouping-and-aggregates.md)
- [Sorting and paging](guide/06-sorting-and-paging.md)
- [Subqueries](guide/07-subqueries.md)
- [Set operations](guide/08-set-operations.md)
- [SELECT DISTINCT](guide/09-distinct.md)
- [Common table expressions (CTE)](guide/10-cte.md)
- [Window functions](guide/11-window-functions.md)
- [User-defined functions](guide/12-user-defined-functions.md)
- [Table-valued functions](guide/13-table-valued-functions.md)
- [Raw SQL](guide/14-raw-sql.md)
- [Query hints](guide/15-query-hints.md)
- [JSON support across providers](guide/16-json.md)
- [Data modification (INSERT)](guide/17-insert-statement.md)
- [Data modification (DELETE)](guide/18-delete-statement.md)
- [Data modification (UPDATE)](guide/19-update-statement.md)
- [Materializing a query into a table](guide/20-create-table-as.md)
- [Data merging (MERGE / upsert)](guide/21-merge-statement.md)
- [Bulk insert](guide/22-bulk-insert.md)
- [Transactions](guide/23-transactions.md)
- [Duration (TimeSpan) columns](guide/24-duration-columns.md)
- [Executing statements in one batch](guide/25-sql-batch.md)
- [Optimistic concurrency and change tracking](guide/26-optimistic-concurrency.md)
- [Range columns (pair of scalar columns)](guide/27-range-columns.md)
- [Streaming large objects (BLOB/CLOB)](guide/28-large-objects.md)
- [Dynamic columns](guide/29-dynamic-columns.md)
- [Streaming data to a Stream](guide/30-streaming-data.md)
- [Provider-specific features](guide/31-provider-specific-features.md)

### Engine

- [How the engine works](engine/index.md)
- [Query pipeline: fluent to SQL](engine/01-query-pipeline.md)
- [Execution and rows](engine/02-execution-and-rows.md)
- [Plan cache and reuse](engine/03-plan-cache-and-reuse.md)

### Relationships

- [Relationships and single-query loading](relationships/relationships.md)
- [Implicit navigation queries](relationships/implicit-navigation.md)
- [Eager loading child collections](relationships/eager-loading.md)
- [Per-parent paging in eager loading](relationships/eager-loading-per-parent-paging.md)

### Infrastructure

- [Connections](infrastructure/01-connections.md)
- [Interceptors](infrastructure/02-interceptors.md)
- [Value converters and JSON columns](infrastructure/03-value-converters.md)
- [Logging](infrastructure/04-logging.md)

### Scalar functions

- [Overview](scalar-functions/index.md)
- [String functions](scalar-functions/01-string-functions.md)
- [Math functions](scalar-functions/02-math-functions.md)
- [Date and time](scalar-functions/03-date-and-time.md)
- [Conditional and conversion helpers](scalar-functions/04-conditionals-and-conversion.md)
- [Aggregates](scalar-functions/05-aggregates.md)
- [Arrays](scalar-functions/06-arrays.md)
- [JSON and XML](scalar-functions/07-json-and-xml.md)
- [Provider reference](scalar-functions/08-reference.md)
- [System functions](scalar-functions/09-system-functions.md)
- [Sequences and UUID generators](scalar-functions/10-sequences.md)

### Connecting providers

- [Provider overview](connecting-providers/overview.md)
- [SQLite](connecting-providers/sqlite.md)
- [SQL Server](connecting-providers/sqlserver.md)
- [PostgreSQL](connecting-providers/postgres.md)
- [In-memory](connecting-providers/in-memory.md)

### Comparisons

- [Comparisons](comparisons/index.md)

### Advanced

- [Global query filters](advanced/query-filters.md)
- [Entity Framework Core integration](advanced/integration-efcore.md)
- [Limitations and out-of-scope features](advanced/limitations.md)
- [Native extreme-row strategies](advanced/select-where-extrema-native.md)
- [Extensibility: custom SQL dialects and renderers](advanced/extensibility.md)
- [API reference](advanced/api-reference.md)

### Русская документация

- [Обзор](ru/overview.md)
- [Постановка задачи](ru/motivation.md)
- [Быстрый старт](ru/getting-started/02-quickstart.md)
- [Все разделы на русском](ru/index.md)

## Overview

Nextorm perform two main functions:

- Generate SQL code
- Map relational data into language (or rather a framework) structures such as classes, primitive types, arrays, lists, etc.

Nextorm uses protocol-level libraries (for example, SqlClient for Microsoft SQL Server or SQLite for SQLite) and designed to create a high performance data access layer independent of SQL and specific RDBMS.

## Status

Nextorm is stable and targets .NET 10 and .NET 11. Package validation checks public API compatibility
against a published baseline.

## Installation

- from cli `dotnet add package nextorm`
- from package manager `Install-Package nextorm`

Use `--prerelease` only if you want to install a preview.
To add specific database provider use the following:

- `dotnet add package nextorm.sqlserver`
- `dotnet add package nextorm.sqlite`
- `dotnet add package nextorm.postgres`
- `dotnet add package nextorm.mysql`
- `dotnet add package nextorm.mariadb`
- `dotnet add package nextorm.clickhouse`

Optional packages: `nextorm.extensions` (query components: `SelectGroup`/`SelectGroupHaving`) and
`nextorm.entityframeworkcore` (run nextorm queries over an existing EF Core `DbContext`).

In-memory provider is built-in in core library.

## Query reuse

There are two independent ways to avoid re-building a query plan on every execution: the implicit plan
cache (used automatically by [`EntityBuilder`](xref:NextORM.Core.EntityBuilder)/[`QueryCommand`](xref:NextORM.Core.QueryCommand) terminals) and explicit [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) returning an
[`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1).

They differ in cost, lifetime and thread-safety rules. Which one to use, what each one costs per call and
its limitations are covered in the [plan cache and reuse guide](engine/03-plan-cache-and-reuse.md).
