# Capabilities: Nextorm vs linq2db and EF Core

A category-level summary of what each library supports. Legend: **yes** = first-class; **partial** =
supported with a named limitation, or implementable but not implemented; **no** = not supported. A Nextorm
cell names a database-engine restriction in parentheses; it does not lower the mark. Competitor gaps with an
open tracking issue link to it. As of **2026-10-04**.

This is the high-level view; it covers the categories that matter when choosing a library, not every
construct.

## Querying

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Projections (entity, DTO, record, tuple, scalar) | yes | yes | yes |
| Joins (`INNER`/`LEFT`/`RIGHT`/`FULL`/`CROSS`); named join aliases | yes (`FULL JOIN` not on MySQL/MariaDB; alias types SQL-provider only) | yes (no named aliases) | partial (`RIGHT`/`FULL` need a workaround) |
| `APPLY` / `LATERAL` | yes (gated off where the engine has no lateral source) | yes | partial |
| Subqueries (scalar, correlated, `EXISTS`/`IN`/`ANY`/`ALL`) | yes | yes | yes |
| `GROUP BY` / `HAVING` / aggregates | yes | yes | partial (no `FILTER`, fewer aggregate families) |
| `ROLLUP` / `CUBE` / `GROUPING SETS` | yes | yes | partial |
| Window functions (`OVER`, ranking, frames) | yes | partial | partial |
| Set operations (`UNION`/`INTERSECT`/`EXCEPT`, plus `ALL`) | yes | yes | yes |
| CTEs, including recursive | yes (data-modifying CTEs on PostgreSQL) | yes (no data-modifying CTEs) | partial (no data-modifying CTEs) |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | yes (per provider) | no | no |
| Extreme-row selection (`SelectWhereMax`/`SelectWhereMin`) | yes | partial (window functions + `OrderBy`/`Take`) | partial (`OrderBy`+`First`; `MaxBy`/`MinBy` in EF Core 11) |
| Per-query source overrides (table/schema/database/server) | yes | partial | partial (model-level) |
| Temporal tables (`FOR SYSTEM_TIME`) | yes (SQL Server, MariaDB) | no | no |
| Row locking (`FOR UPDATE`, `NOWAIT`/`SKIP LOCKED`) | yes | yes | no (raw SQL only) |
| Query / table / index hints | yes | partial | no (raw SQL or an interceptor) |
| Raw SQL (whole query, composable source, and raw commands with parameters/output parameters/multiple result sets) | yes | yes | yes |
| Multiple result sets from one batch (`AddQuery<TResult>` + `Execute`/`ExecuteAsync`) | yes (PostgreSQL, SQL Server, MySQL, MariaDB, SQLite) | yes | no |
| Stored procedures (`ExecuteProcedure`, `CommandType.StoredProcedure`) | yes (SQL Server, PostgreSQL, MySQL/MariaDB) | yes | yes |
| Result-set streaming (LOB stream, `ToDataReader`) | yes | yes | partial (raw reader) |
| Result-set export to a stream (JSON, CSV) | yes | partial (client serialization) | partial (client serialization) |

## Types and mapping

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Value converters | yes | yes | yes (`HasConversion`) |
| Dynamic columns (`[DynamicColumns]` store) | yes | yes | no (JSON/TPH workarounds) |
| JSON column ↔ object | yes | partial ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)) | yes (JSON columns) |
| Duration / interval columns | yes | yes | partial (provider interval mapping) |
| Native JSON documents | yes on PostgreSQL | partial (type + `@>`/`#>>`/`Json.Value`; not the full `jsonb_*` library) | partial (JSON column mapping + Npgsql `EF.Functions.Json*`) |
| Arrays and higher-order array functions | yes on PostgreSQL and ClickHouse | partial (PostgreSQL array operators) | partial |
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
| Returning or output of affected rows | yes | yes | partial (`ExecuteUpdate`/`ExecuteDelete` return the affected count; row values via `SaveChanges`) |
| Bulk insert | yes (native copy or chunked `VALUES`) | yes | partial |
| Table-valued parameters | yes (native SQL Server; array/JSON emulation on PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse) | yes | no |
| `CREATE TABLE AS SELECT`, temporary tables | yes | yes | no |
| `TRUNCATE` | yes (SQLite/in-memory reject) | yes | no (raw SQL only) |
| Optimistic concurrency | partial (explicit token-guarded pattern) | yes | yes |
| `OUTPUT ... INTO` | yes (SQL Server) | partial (`…WithOutputInto` writes to a table; combined returning+INTO open [#3832](https://github.com/linq2db/linq2db/issues/3832)) | no |
| Transactions (own and enlisted) | yes | yes | yes |
| Captured-collection lookup (`dict[column]`, `list[column]`) | yes | no (open [linq2db#5879](https://github.com/linq2db/linq2db/issues/5879); only `Contains` → `IN`) | no (indexer not translated; client evaluation blocked) |

## Model and tooling

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Entity class optional (query without a mapped type) | yes | partial | no |
| Navigation properties / associations / eager loading | partial (declarative O2M/M2O/O2O/M2M relationship metadata and the single-query `JoinInto` loader, plus level-1 `LoadWith` eager loading and declared-relationship implicit navigation; convention-over-FK inference is absent in linq2db too) | yes | yes |
| Context configuration, logging, DI and plan cache (`Prepare()`) | yes | yes | yes |
| Change tracking / identity map | no (by design) | partial | yes |
| Migrations | no | partial (schema API; migrations via third-party) | yes |
| Database-first scaffolding | no (mappings declared in code) | yes | yes |
| EF Core integration | yes (read-only MVP, `nextorm.entityframeworkcore`) | yes | n/a |
| Providers | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, ClickHouse, DuckDB, Ydb | SQL Server, PostgreSQL, MySQL, SQLite, Oracle, and more |

## See also

- [Benchmarks](benchmarks.md) — the shipped scenarios.
- [Limitations and out-of-scope features](../advanced/limitations.md).
- [Provider overview](../providers/overview.md).
