# Capabilities: Nextorm vs Dapper, linq2db and EF Core

One construct-level matrix: what each library supports and where it does not. Legend: **yes** =
first-class; **partial** = supported with a named limitation, or implementable but not implemented; **no** =
not supported. A Nextorm cell names a database-engine restriction in parentheses; it does not lower the mark.
Competitor gaps with an open tracking issue link to it. As of **2026-10-04**.

Rows are ordered by area — querying, types and mapping, provider functions, writes, model and tooling. A
**Total** row closes the matrix: the yes / partial / no counts for that column (`n/a` is excluded, and the
provider list is not scored). The per-engine API breadth is on [Provider features](provider-features.md).

| Area | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Projections (entity, DTO, anonymous, record, tuple, scalar; unmapped columns via `SqlFunctions.Column`) | yes | yes | yes |
| Predicates (`WHERE`: comparison, `and`/`or`/`!`, arithmetic, bitwise/shift) | yes | yes | yes |
| Joins (`INNER`/`LEFT`/`RIGHT`/`FULL`/`CROSS`, derived-table joins, up to 8 sources) | yes (`FULL JOIN` not on MySQL/MariaDB) | yes | partial (`RIGHT`/`FULL` need a workaround) |
| Named join aliases (`Alias.<Name>`) | yes (SQL-provider only) | no | no |
| `APPLY` / `LATERAL` | yes (gated off where the engine has no lateral source) | yes | partial |
| ClickHouse join strictness (`ANY`/`ALL`/`ASOF`) and `GLOBAL` | yes | yes | no |
| Subqueries (`FROM`, scalar, correlated `EXISTS`/`IN`/`ANY`/`ALL`) | yes (in-memory evaluates depth one) | yes | yes |
| `IN` over a list/array | yes | partial (no ClickHouse `GLOBAL IN`) | yes |
| `GROUP BY` / `HAVING` / aggregates (`FILTER`, ClickHouse `-If`, statistical/quantile/ordered families) | yes | yes (`Sql.Ext`) | partial (no `FILTER`, fewer aggregate families) |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes | yes | partial |
| `LIMIT n BY expr` | yes on ClickHouse | no | no |
| ClickHouse query modifiers (`FINAL`, `SAMPLE`, `PREWHERE`, `SETTINGS`) | yes | partial (`FINAL` and `SETTINGS` only) | no |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | yes |
| Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`, percentiles) | yes (named windows, `GROUPS`/`EXCLUDE`) | partial (no named windows or frame `GROUPS`/`EXCLUDE`) | partial |
| Set operations (`UNION`/`UNION ALL`/`INTERSECT`/`EXCEPT`, `ALL` where the engine has it) | yes | yes | yes |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | yes (per provider) | no | no |
| CTEs (recursive, typed `AsCte`/`AsRecursiveCte`; PostgreSQL data-modifying CTEs) | yes | yes (no data-modifying CTEs) | partial (no data-modifying CTEs) |
| Extreme-row selection (`SelectWhereMax`/`SelectWhereMin`) | yes | partial (window functions + `OrderBy`/`Take`) | partial (`OrderBy`+`First`) |
| Per-query source overrides (table/schema/database/server) | yes | partial | partial (model-level) |
| Temporal tables (`FOR SYSTEM_TIME`) | yes (SQL Server, MariaDB) | no | partial (SQL Server only) |
| Row locking (`FOR UPDATE`/`FOR SHARE`, `NOWAIT`/`SKIP LOCKED`) | yes | yes | no (raw SQL only) |
| Query / table / index hints | yes | partial | no (raw SQL or an interceptor) |
| Raw SQL (whole query, composable source, raw commands with parameters/output parameters/multiple result sets) | yes | yes | yes |
| Batch / multiple result sets from one call | yes (PostgreSQL, SQL Server, MySQL, MariaDB, SQLite) | yes | no |
| Stored procedures (`ExecuteProcedure`, `CommandType.StoredProcedure`) | yes (SQL Server, PostgreSQL, MySQL/MariaDB) | yes | yes |
| Result-set streaming (LOB stream, `ToDataReader`) | yes (sequential LOB streaming on PostgreSQL/SQL Server; SQLite is buffered; MySQL/MariaDB/ClickHouse and in-memory reject) | yes | partial (raw reader) |
| Result-set export to a stream (JSON, CSV) | yes | partial (client serialization) | partial (client serialization) |
| `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST` | yes | yes | yes |
| String / math / date scalar functions, `LIKE`, concatenation, `NULLIF` | yes | yes | yes |
| CLR `Regex` (`IsMatch`/`Replace`, constant pattern) | yes (PostgreSQL, MySQL/MariaDB, ClickHouse, SQLite, SQL Server 2025+) | partial (open [linq2db#698](https://github.com/linq2db/linq2db/issues/698)) | no |
| `string.Format` / interpolation in SQL | yes (culture-invariant subset) | partial ([linq2db#5921](https://github.com/linq2db/linq2db/issues/5921)) | no |
| Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes | yes |
| Full-text search | yes (SQL Server, PostgreSQL, MySQL/MariaDB; SQLite FTS3/4/5 query + `FROM`, FTS5 maintenance builder) | yes | partial |
| Conditional functions (`iif`/`choose`/`multi_if`) | yes | no | no |
| User-defined scalar functions | yes (`[SqlFunction]`) | yes (`DbFunction` / `Sql.Ext`) | yes |
| Table-valued functions | yes (`[SqlTableFunction]`) | yes (`TableFunction`; DB TVFs can be scaffolded) | yes |
| Dynamic result schema for table functions | yes | no | no |
| Native `PIVOT` / `UNPIVOT` source | yes (SQL Server) | no | no |
| `FOR JSON` / `FOR XML` | yes (SQL Server) | yes | partial |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | partial (SQL Server only) | yes | no |
| `GREATEST` / `LEAST` | yes (NULL handling is provider-specific) | partial | partial |
| `STRING_AGG` / `ARRAY_AGG` | yes (`array_agg` on PostgreSQL) | yes | partial |
| Value converters | yes | yes | yes (`HasConversion`) |
| Dynamic columns (`[DynamicColumns]` store) | yes | yes | no (JSON/TPH workarounds) |
| JSON column ↔ object | yes | partial ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)) | yes (JSON columns) |
| Duration / interval columns | yes (PostgreSQL `interval` → `TimeSpan` only for representable zero-month values) | yes | partial (provider interval mapping) |
| Native JSON documents | yes on PostgreSQL | partial (the `json`/`jsonb` type plus `@>`, `#>>` and `Json.Value`; not the full `jsonb_*` library) | partial (JSON column mapping + Npgsql `EF.Functions.Json*`) |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | yes (SQL Server, MySQL/MariaDB) | yes | partial |
| String JSON + native-JSON + dictionary functions (ClickHouse) | yes on ClickHouse | no | no |
| Arrays and higher-order array functions (`cardinality`/`array_*`/`@>`/`&&`, `Array(T)`, `ARRAY JOIN`) | yes on PostgreSQL and ClickHouse | partial (PostgreSQL array operators only) | partial |
| Row values / tuples (`ROW`/`(a, b)`, element access, row comparison) | yes — PostgreSQL `ROW`/`(row).fN` and ClickHouse `tuple`/`tupleElement`; flat `(a, b)` on MySQL/MariaDB/SQLite | yes (`Sql.Row`; emulated where the provider has no native row) | partial |
| Range types and range-over-scalar-columns (`Range<T>`, `Overlaps`, `range_contains`, bound inspection) | yes — native range/multirange on PostgreSQL; a mapped pair of scalar columns (`[RangeColumns]`) elsewhere | partial (provider-native `NpgsqlRange<T>`/multirange mapping only; no portable `Range<T>`/pair) | partial |
| Collation and ordinal string semantics | yes | partial ([linq2db#5927](https://github.com/linq2db/linq2db/issues/5927)) | partial |
| Naming conventions (e.g. snake_case) | yes (opt-in, built-in `SnakeCaseNamingConvention`) | partial (no built-in convention) | partial |
| Identifier quoting | yes (opt-in) | yes (on by default) | yes (always) |
| SQL keyword casing (upper/lower) | yes (opt-in `KeywordCase.Upper`) | no (keywords are emitted in the provider's canonical case) | no |
| `INSERT` / `UPDATE` / `DELETE` / `MERGE` | yes | yes | partial (no `MERGE`) |
| Returning or output of affected rows | yes | yes | partial (`ExecuteUpdate`/`ExecuteDelete` return the affected count; row values via `SaveChanges`) |
| Bulk copy / insert / temporary tables | partial (native `COPY`/`SqlBulkCopy` or chunked `VALUES`, `ToTable`/`ToTempTable` — narrower than linq2db's bulk/temp surface) | yes | partial |
| Table-valued parameters | yes (native SQL Server; array/JSON/`Array(T)`+`arrayJoin` emulation on PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse) | yes (`TableParameterValue`) | no |
| `CREATE TABLE AS SELECT`, temporary tables | yes | yes | no |
| `TRUNCATE` | yes (SQLite/in-memory reject) | yes | no (raw SQL only) |
| Optimistic concurrency | partial (explicit token-guarded pattern; no concurrency-token metadata) | yes | yes |
| `OUTPUT ... INTO` | yes (SQL Server) | partial (`…WithOutputInto` writes to a table; combined returning+INTO open [#3832](https://github.com/linq2db/linq2db/issues/3832)) | no |
| Transactions (own and enlisted) | yes (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB; ClickHouse and in-memory reject) | yes | yes |
| Captured-collection lookup (`dict[column]`, `list[column]`) | yes | no (open [linq2db#5879](https://github.com/linq2db/linq2db/issues/5879); only `Contains` → `IN`) | no (indexer not translated; client evaluation blocked) |
| Entity class optional (query without a mapped type) | yes | partial | no |
| Navigation properties / associations / eager loading | partial (declarative O2M/M2O/O2O/M2M metadata, `JoinInto`, `LoadWith` eager loading — split or single-query, nested level 2+ with a graph-wide mode and per-level ordering/top-N — and declared-relationship implicit navigation; convention-over-FK inference is absent in linq2db too) | yes (`[Association]`, `LoadWith`) | yes |
| Context configuration, logging and DI | yes | yes | yes |
| Query plan cache | yes | yes (compiled queries, query cache) | yes |
| Explicit `Prepare()` | yes (`IPreparedQueryCommand<TResult>`) | yes | no |
| Change tracking / identity map | no (by design) | partial | yes |
| Migrations | no | partial (schema API; migrations via third-party) | yes |
| Database-first scaffolding | no (mappings declared in code) | yes | yes |
| EF Core integration | yes (read-only MVP, `nextorm.entityframeworkcore`) | yes (`linq2db.EntityFrameworkCore`) | n/a |
| Providers | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, ClickHouse, DuckDB, Ydb | SQL Server, PostgreSQL, MySQL, SQLite, Oracle, and more |
| `SelectMany` / `GroupJoin` | partial (in-memory only; SQL providers reject) | yes (SQL translation) | yes |
| In-memory provider (query-only, registered CLR datasets) | yes | partial | partial |
| Connection lifecycle (`IConnectionManager`: supplied vs owned, reuse, disposal) | yes | yes | yes |
| Extensibility (dialect, `[SqlFunction]`/`[SqlTableFunction]`, interceptors, global query filters, raw SQL) | yes | yes (extensive) | yes |
| **Total** | 71 / 5 / 3 | 48 / 21 / 10 | 27 / 27 / 24 |

## Where Nextorm leads

Against the shared surface Nextorm matches or exceeds linq2db; on top of that it adds:

- The complete analytic query surface: every join type including `APPLY`/`LATERAL` (with correlated
  sources), derived-table joins, ClickHouse join strictness/`GLOBAL`, set operations, `DISTINCT` (plus
  `DISTINCT ON`/`WITH TIES`), CTEs (recursive, plus PostgreSQL data-modifying CTEs), window functions
  (named windows, `GROUPS`, frame `EXCLUDE`, percentiles), `ROLLUP`/`CUBE`/`GROUPING SETS`/`WITH TOTALS`,
  temporal tables, row locking, hints and raw SQL for a whole query.
- Unmapped columns (`SqlFunctions.Column`), named join aliases (`Alias.<Name>`) and per-query source
  overrides — each folded into the plan key.
- `SelectWhereMax`/`SelectWhereMin` as a dedicated extreme-row operator, lowered to the native strategy on
  PostgreSQL and ClickHouse; linq2db composes window functions plus `OrderBy`/`Take`.
- `[JsonColumn]` with Auto/Native/Text storage — a mapping linq2db still lacks
  ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)).
- A complete, explicit write surface: `INSERT`/`UPDATE`/`DELETE`/full `MERGE`, generated keys and returned
  rows, key upsert, bulk insert, `CREATE TABLE AS SELECT`/temporary tables, PostgreSQL data-modifying CTEs and
  transactions — all explicit commands, with no change tracking and no `SaveChanges`.
- Declarative relationships and eager loading: O2M/M2O/O2O/M2M via `JoinInto`, plus `LoadWith` (split-query by
  default, opt-in single-query, nested level 2+, per-parent ordering/top-N) and declared-relationship implicit
  navigation, with global query filters applied to children at every level.
- An EF Core integration package (`nextorm.entityframeworkcore`) sharing the EF connection, model mapping and
  transaction.
- Cross-provider row values, portable range-over-scalar-pairs (`[RangeColumns]`), index hints, configurable
  keyword casing, full-text search including the SQLite FTS3/4/5 query surface and the FTS5
  maintenance/control builder, and CLR `Regex` translation.
- Two reuse paths (implicit plan cache and explicit `Prepare()`), query parametrisation and a low-allocation
  design benchmarked at or above Dapper, EF Core and linq2db on the shipped scenarios.

## Deliberate boundaries: what Nextorm leaves to linq2db

These are conscious scope decisions in Nextorm's focused, no-change-tracking model — not gaps on the query
surface, which Nextorm matches or exceeds:

- **Change tracking and identity map**: every write stays an explicit command; there is no `SaveChanges` and
  no automatic change tracking. linq2db flushes a tracked unit of work.
- **Database-first tooling**: linq2db ships a CLI/T4 code-generation toolchain that scaffolds entity and
  table-function mappings from a live database; Nextorm declares mappings in code.
- **Provider breadth**: beyond the providers both ship, linq2db additionally covers Oracle, Firebird, DB2,
  SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB and Ydb, while Nextorm adds an in-memory object context
  and separate MySQL/MariaDB providers.
- **DDL / schema management**: linq2db ships `CreateTable`/`DropTable` and a schema API; Nextorm mutates the
  schema only through `CREATE TABLE AS SELECT` — ad-hoc DDL goes through `ExecuteRaw` (out of scope).

Correlation is uniform on the SQL providers (arbitrary nesting depth). The in-memory provider evaluates
depth-one correlated scalar/aggregate/`EXISTS`/`IN` once per outer row and rejects only the deeper forms (an
outer reference inside the inner projection or `ORDER BY`, a correlated `GROUP BY`/`HAVING`, and an async
inner source).

## Architecture differences

| Aspect | Nextorm | linq2db |
|---|---|---|
| Model | Query builder and mapper without change tracking, with a full explicit write surface (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), bulk insert and transactions | Explicit CRUD ORM with associations; no automatic change tracking |
| Entity requirement | Entity class optional — mapped via attributes/fluent/conventions, or none at all with `From("table")` + `TableAlias` | A mapped class required (attributes, fluent or convention) |
| Reuse | Implicit plan cache and `Prepare()` | Compiled queries, query cache |
| Result consumption | First-class non-materialising terminals on the query: single-column LOB streaming (`ToStream`/`ToTextReader`), a caller-owned `DbDataReader` (`ToDataReader`) and JSON/CSV export (`WriteJson`/`WriteCsv`) that stream rows without constructing `TResult` | Always materialises the mapped type per row; raw access only through `DataConnection.ExecuteReader`/`DataReaderWrapper`, with no LOB or JSON/CSV terminals |

## Summary

For reading, reporting and explicit data modification over an existing schema, Nextorm is the stronger
choice: it covers essentially the whole analytic query surface linq2db offers — the provider-only function
families, the ClickHouse-specific constructs, cross-provider row values, table-valued functions and more —
with a smaller allocation footprint, benchmark results at or above Dapper, EF Core and linq2db on the shipped
scenarios, and more configurable SQL output (identifier quoting, naming conventions and keyword casing are
opt-in and overridable per command). linq2db remains the better fit only when the same layer must also track
changes or generate the data layer from a live schema — the deliberate boundaries listed above.

## See also

- [Provider features](provider-features.md) — per-engine API breadth against linq2db.
- [Benchmarks](benchmarks.md) — the shipped scenarios.
- [Limitations and out-of-scope features](../advanced/limitations.md).
- [Provider overview](../connecting-providers/overview.md).
- [Joins](../guide/04-joins.md) — `CrossApply`/`OuterApply`.
- [Range columns](../guide/27-range-columns.md) — a `Range<T>` stored as a pair of scalar columns.
- [Query hints](../guide/15-query-hints.md).
