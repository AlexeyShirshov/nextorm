# Nextorm - high performance zero-sql object-relational mapping (ORM) library

## Documentation

### Getting started

- [Installation](getting-started/01-installation.md)
- [Quickstart](getting-started/02-quickstart.md)
- [Entities and metadata](getting-started/03-entities-and-metadata.md)
- [Dependency injection](getting-started/04-dependency-injection.md)

### Guide

- [Querying and projections](guide/01-querying-and-projections.md)
- [Filtering (WHERE)](guide/02-filtering-where.md)
- [Joins](guide/03-joins.md)
- [Grouping and aggregates](guide/04-grouping-and-aggregates.md)
- [Sorting and paging](guide/05-sorting-and-paging.md)
- [Subqueries](guide/06-subqueries.md)
- [Set operations](guide/07-set-operations.md)
- [SELECT DISTINCT](guide/08-distinct.md)
- [Common table expressions (CTE)](guide/09-cte.md)
- [Window functions](guide/10-window-functions.md)
- [Scalar functions](guide/11-scalar-functions.md)
- [User-defined functions](guide/12-user-defined-functions.md)
- [Table-valued functions](guide/13-table-valued-functions.md)
- [Raw SQL](guide/14-raw-sql.md)
- [Query reuse: cache vs Prepare](guide/15-query-reuse.md)
- [Connections and logging](guide/16-connections-and-logging.md)

### Providers

- [Provider overview](providers/overview.md)
- [SQLite](providers/sqlite.md)
- [SQL Server](providers/sqlserver.md)
- [PostgreSQL](providers/postgres.md)
- [In-memory](providers/in-memory.md)

### Advanced

- [Limitations and out-of-scope features](advanced/limitations.md)
- [API reference](advanced/api-reference.md)
- [Prepared vs cached: reusing a query](prepared-vs-cached.md)
- [SQL capabilities gap analysis](sql-capabilities-gap-analysis.md)

### Русская документация

- [Обзор](ru/overview.md)
- [Постановка задачи](ru/motivation.md)
- [Быстрый старт (англ.)](getting-started/02-quickstart.md)

Полное руководство на английском: [Getting started](getting-started/01-installation.md) ·
[Guide](#guide) · [Providers](#providers). Русский перевод в работе.

## Overview

Nextorm perform two main functions:

- Generate SQL code
- Map relational data into language (or rather a framework) structures such as classes, primitive types, arrays, lists, etc.

Nextorm uses protocol-level libraries (for example, SqlClient for Microsoft SQL Server or SQLite for SQLite) and designed to create a high performance data access layer independent of SQL and specific RDBMS.

## Status

The current status (1.0.1-alpha) is a prof of concept.

## Roadmap

- [1.0.2-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.2)
- [1.0.3-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.3)
- [1.0.4-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.4)
- [1.0.5-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.5)
- [1.0-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-b.1)
- [1.0-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-rc.1)
- [1.0](https://github.com/AlexeyShirshov/nextorm/milestones/1.0)
- [1.1.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.1-a.1)
- [1.1-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.1-b.1)
- [1.1-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.1-rc.1)
- [1.1](https://github.com/AlexeyShirshov/nextorm/milestones/1.1)
- [1.2.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.2-a.1)
- [1.2-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.2-b.1)
- [1.2-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.2-rc.1)
- [1.2](https://github.com/AlexeyShirshov/nextorm/milestones/1.2)
- [1.3.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.3-a.1)
- [1.3-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.3-b.1)
- [1.3-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.3-rc.1)
- [1.3](https://github.com/AlexeyShirshov/nextorm/milestones/1.3)
- [1.4.1-alpha](https://github.com/AlexeyShirshov/nextorm/milestones/1.4-a.1)
- [1.4-beta](https://github.com/AlexeyShirshov/nextorm/milestones/1.4-b.1)
- [1.4-rc](https://github.com/AlexeyShirshov/nextorm/milestones/1.4-rc.1)
- [1.4](https://github.com/AlexeyShirshov/nextorm/milestones/1.4)

## Installation

- from cli `dotnet add package nextorm`
- from package manager `Install-Package nextorm`

Don't forget to add `--prerelease` flag since all current versions is not stable.
To add specific database provider use the following:

- `dotnet add package nextorm.sqlserver`
- `dotnet add package nextorm.sqlite`
- `dotnet add package nextorm.postgres`

In-memory provider is built-in in core library.

## Query reuse

There are two independent ways to avoid re-building a query plan on every execution: the implicit plan
cache (used automatically by `Entity`/`QueryCommand` terminals) and explicit `Prepare()` returning an
`IPreparedQueryCommand<TResult>`.

They differ in cost, lifetime and thread-safety rules. Which one to use, what each one costs per call and
its limitations: **[Prepared vs Cached](prepared-vs-cached.md)**.

## Releases

### 1.0.1-alpha

- [Where clause](https://github.com/AlexeyShirshov/nextorm/issues/1)
- [Joins](https://github.com/AlexeyShirshov/nextorm/issues/2)
- [Paging](https://github.com/AlexeyShirshov/nextorm/issues/7)
- [Subqueries](https://github.com/AlexeyShirshov/nextorm/issues/10)
- [Sorting](https://github.com/AlexeyShirshov/nextorm/issues/11)
- [Microsoft SQL Server support](https://github.com/AlexeyShirshov/nextorm/issues/23)
- [In-memory support](https://github.com/AlexeyShirshov/nextorm/issues/24)
