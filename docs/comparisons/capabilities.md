# Capabilities: Nextorm vs linq2db and EF Core

A category-level summary of what each library supports. Legend: **yes** = first-class; **partial** =
supported with a named limitation, or implementable but not implemented; **no** = not supported. A Nextorm
cell names a database-engine restriction in parentheses; it does not lower the mark. Competitor gaps with an
open tracking issue link to it. As of **2026-09-25**.

This is the high-level view; it covers the categories that matter when choosing a library, not every
construct.

## Querying

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Projections (entity, DTO, record, tuple, scalar) | yes | yes | yes |
| Joins (`INNER`/`LEFT`/`RIGHT`/`FULL`/`CROSS`) | yes (`FULL JOIN` not on MySQL/MariaDB) | yes | partial (`RIGHT`/`FULL` need a workaround) |
| `APPLY` / `LATERAL` | yes (gated off where the engine has no lateral source) | yes | partial |
| Subqueries (scalar, correlated, `EXISTS`/`IN`/`ANY`/`ALL`) | yes | yes | yes |
| `GROUP BY` / `HAVING` / aggregates | yes | yes | partial (no `FILTER`, fewer aggregate families) |
| `ROLLUP` / `CUBE` / `GROUPING SETS` | yes | yes | partial |
| Window functions (`OVER`, ranking, frames) | yes | partial | partial |
| Set operations (`UNION`/`INTERSECT`/`EXCEPT`, plus `ALL`) | yes | yes | yes |
| CTEs, including recursive | yes (data-modifying CTEs on PostgreSQL) | yes | partial (no data-modifying CTEs) |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | yes (per provider) | no | no |
| Temporal tables (`FOR SYSTEM_TIME`) | yes (SQL Server, MariaDB) | no | no |
| Row locking (`FOR UPDATE`, `NOWAIT`/`SKIP LOCKED`) | yes | yes | no (raw SQL only) |
| Query / table / index hints | yes | partial | no (raw SQL or an interceptor) |
| Raw SQL (whole query, and as a composable source) | yes | yes | yes |

## Types and mapping

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Value converters | yes | yes | yes (`HasConversion`) |
| JSON column ↔ object | yes | partial ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)) | yes (JSON columns) |
| Duration / interval columns | yes | yes | partial (provider interval mapping) |
| Native JSON documents | yes on PostgreSQL | yes | yes |
| Arrays and higher-order array functions | yes on PostgreSQL and ClickHouse | no | partial |
| Row values / tuples | yes on PostgreSQL and ClickHouse | yes | partial |
| Range types and range-over-scalar-columns | yes | partial | partial |
| Collation and ordinal string semantics | yes | partial ([linq2db#5927](https://github.com/linq2db/linq2db/issues/5927)) | partial |
| Naming conventions (e.g. snake_case) | yes (opt-in, built-in) | partial | partial |
| Identifier quoting | yes (opt-in) | yes (on by default) | yes (always) |
| SQL keyword casing | yes (opt-in) | no | no |

## Provider functions

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Portable scalar library (string, math, date, conditional) | yes | yes | partial (provider extensions) |
| Full-text search | yes (SQL Server, PostgreSQL, MySQL/MariaDB) | yes | partial |
| Native string / regexp library | yes (provider-only) | yes | partial |
| CLR `Regex` translation | yes (including SQL Server 2025+) | partial ([linq2db#698](https://github.com/linq2db/linq2db/issues/698)) | no |
| `string.Format` / interpolation in SQL | yes (culture-invariant subset) | partial ([linq2db#5921](https://github.com/linq2db/linq2db/issues/5921)) | no |
| Table-valued functions | yes | yes | yes |
| Dynamic result schema for table functions | yes | no | no |
| Native `PIVOT` / `UNPIVOT` source | yes (SQL Server) | no | no |
| `FOR JSON` / `FOR XML` | yes (SQL Server) | yes | partial |

## Writing data

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| `INSERT` / `UPDATE` / `DELETE` / `MERGE` | yes | yes | partial (no `MERGE`) |
| Returning or output of affected rows | yes | yes | yes |
| Bulk insert | yes (native copy or chunked `VALUES`) | yes | partial |
| `CREATE TABLE AS SELECT`, temporary tables | yes | yes | no |
| `OUTPUT ... INTO` | yes (SQL Server) | no ([linq2db#3832](https://github.com/linq2db/linq2db/issues/3832)) | no |
| Transactions (own and enlisted) | yes | yes | yes |
| Captured-collection lookup (`dict[column]`, `list[column]`) | yes | no | no |

## Model and tooling

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Entity class optional (query without a mapped type) | yes | partial | no |
| Navigation properties / associations / eager loading | no | yes | yes |
| Change tracking / identity map | no (by design) | partial | yes |
| Migrations | no | partial (schema API; migrations via third-party) | yes |
| Database-first scaffolding | no (mappings declared in code) | yes | yes |
| EF Core integration | no | yes | n/a |
| Providers | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, SQLite, Oracle, and more |

## See also

- [Benchmarks](benchmarks.md) — the shipped scenarios.
- [Limitations and out-of-scope features](../advanced/limitations.md).
- [Provider overview](../providers/overview.md).
