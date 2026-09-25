# nextorm vs linq2db: functionality comparison

> A side-by-side functional comparison of nextorm and linq2db. It complements the
> [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) and the
> [capability matrix](capability-matrix.md) (which also cover EF Core). Based on the current tree, it shows
> nextorm matching or exceeding linq2db across the analytic query surface — join types, `APPLY`/`LATERAL`,
> full-text/JSON/arrays, cross-provider row values, native range types (and range-over-scalar-pairs),
> CLR `Regex` translation, `ROLLUP`/`CUBE`/`GROUPING SETS`, date arithmetic,
> temporal tables, row locking, statement/table/index hints, configurable keyword casing, TVFs and
> depth-one in-memory correlation — and adding a complete explicit write surface
> (`INSERT`/`UPDATE`/`DELETE`/full `MERGE`, PostgreSQL data-modifying CTEs), bulk insert, `CREATE TABLE AS
> SELECT` and a transactions role.

**Prerequisites:** [Provider overview](../../providers/overview.md) · [Limitations](../../advanced/limitations.md) · [Query hints](../../guide/17-query-hints.md)

## Positioning

* **nextorm** is a focused, no-change-tracking SQL builder and mapper purpose-built for reading and
  reporting. It covers the complete analytic query surface, adds a complete explicit write surface
  (`INSERT`/`UPDATE`/`DELETE`/full `MERGE`, bulk insert, `CREATE TABLE AS SELECT`, and data-modifying CTEs on
  PostgreSQL) plus a transactions role (`ITransactionManager`, own and enlisted) and generates
  provider-portable SQL for SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse. It is built around a small
  allocation footprint, parameterisation and query compilation (implicit plan cache / explicit
  `Prepare()`), with opt-in output controls (identifier quoting, naming conventions, keyword casing) and
  index hints — and benchmarks at or above Dapper, EF Core and linq2db on the shipped scenarios.
* **linq2db** is a broad, mature LINQ-to-SQL ORM: a wider provider matrix, full CRUD (`INSERT`/`UPDATE`/
  `DELETE`/`MERGE`), associations/eager loading, bulk copy, temporary tables, schema code-generation
  tooling, extensibility (interceptors, custom SQL mapping) and an EF Core integration package. That extra
  surface comes with change tracking and a heavier model.

The two overlap on the *query* surface and explicit data modification — where nextorm matches or exceeds
linq2db — and diverge on *relationship modelling*, *change tracking* and *tooling*, which nextorm leaves out
by design.

## Capability matrix

Legend: **yes** = first-class; **partial** = the library's own gap — implementable but not implemented yet;
**no** = not supported. nextorm is the baseline, so its cell carries the plain mark — a restriction imposed
by the database engine itself is named briefly in parentheses and does not lower the mark. Where linq2db
supports the construct but lacks a capability nextorm has, it is marked **partial** with the missing piece
named. Evidence for nextorm points at the source that owns the behaviour.

| Area | linq2db | nextorm | nextorm evidence |
|---|---|---|---|
| Projection (`SELECT`, DTO/anonymous/record/tuple/scalar) | yes | yes | `EntityBuilder.Select` |
| Predicates (`WHERE`: comparison, `and`/`or`/`!`, arithmetic, bitwise/shift) | yes | yes | `Visitors/WhereExpressionVisitor.cs`, `BaseExpressionVisitor.cs` |
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes | **yes** (`FULL JOIN` not on MySQL/MariaDB) | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin`, `ISqlDialect.SupportsFullJoin` |
| `APPLY` / `LATERAL` | yes | **yes** (gated off on SQLite/ClickHouse) | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Join strictness (`ANY`/`ALL`/`ASOF`) and `GLOBAL` | no | **yes** on ClickHouse | `JoinStrictness`, `EntityBuilder.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin` |
| Join arity | yes | **yes** — up to 8 | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| JOIN to a derived table (subquery) | yes | **yes** | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| Subqueries (`FROM`, scalar, correlated `EXISTS/IN/ANY/ALL`) | yes | **yes** (in-memory evaluates depth one only) | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference`, `DataContext/InMemoryCorrelatedPlan.cs` |
| `IN` over a list/array | partial — no ClickHouse distributed `GLOBAL IN` | **yes** | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs`, `SqlFunctions.ClickHouse.global_in` |
| `GROUP BY` / `HAVING` / aggregates | yes | **yes** | `EntityBuilder.GroupBy/Having`, `AdvancedAggregateTranslator.cs`, `Supports*Aggregates` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes | **yes** (`WITH TOTALS` on ClickHouse) | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | no | **yes** on ClickHouse | `LimitBy`, `ILimitByRenderer.Render` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | no | **yes** on ClickHouse | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, dialect `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL` where the engine has it) | yes | yes (provider dependent; `ALL` on PostgreSQL, MariaDB and ClickHouse) | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | no | **yes** per provider (`DISTINCT ON` PostgreSQL; `WITH TIES` PostgreSQL + SQL Server; `TABLESAMPLE` PostgreSQL + SQL Server) | `DistinctOn`/`WithTies`/`FromOptions.TableSample`, `ISqlDialect.SupportsWithTies` |
| Row locking (`FOR UPDATE`/`FOR SHARE`, `NOWAIT`/`SKIP LOCKED`) | yes (provider-specific `SubQueryTableHint`: PostgreSQL `FOR UPDATE`/`FOR NO KEY UPDATE`/`FOR SHARE`/`FOR KEY SHARE`, MySQL `FOR UPDATE`/`FOR SHARE`/`LOCK IN SHARE MODE`, SQL Server via `UPDLOCK`/`XLOCK` table hints; plus `NOWAIT`/`SKIP LOCKED`) | **yes** — `FOR UPDATE`/`FOR SHARE` per provider (PostgreSQL/MySQL/MariaDB trailing `FOR UPDATE`/`LOCK IN SHARE MODE`; SQL Server via table hints) **plus the `NOWAIT`/`SKIP LOCKED` wait modes** (`LockWaitMode`; MySQL switches a shared lock to `FOR SHARE`, SQL Server uses `READPAST` as the `SKIP LOCKED` approximation) | `ForUpdate`/`ForShare` + `LockWaitMode`, `ILockRenderer`/`ILockRenderer.UsesTableHints`/`Render` |
| Temporal tables (`FOR SYSTEM_TIME`) | yes | **yes** on SQL Server + MariaDB (`CONTAINED IN` — SQL Server only) | `ForSystemTime`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| CTEs (including recursive) | yes | **yes** (PostgreSQL data-modifying CTEs) | `Builders/CteQuery.cs`, `Builders/MutationCteQuery.cs`, `DataContext.DataContextExtensions.With/WithRecursive`, `ISqlDialect.SupportsDataModifyingCtes` |
| Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`) | partial — no named windows or frame `GROUPS`/`EXCLUDE` | **yes** | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion` |
| `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| String / math / date scalar functions, `LIKE` | yes | **yes** — portable CLR `string` methods on every provider, plus the native string/`regexp_*` library on PostgreSQL (`SqlFunctions.Postgres`) | `Visitors/ScalarFunctionTranslator.cs`, dialect `Make*` hooks, `SqlFunctions.Postgres` |
| CLR `Regex` (`IsMatch`/`Replace`, constant pattern) | partial — open `linq2db#698` (no `Regex.IsMatch` translation) | **yes** — PostgreSQL, MySQL/MariaDB, ClickHouse, SQLite and SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`; 2019/2022 reject) | `Visitors/RegexSqlTranslator.cs`, `ISqlDialect.SupportsRegex`/`MakeRegexMatch`/`MakeRegexReplace` |
| Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | **yes** | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Full-text search | yes (provider) | **yes** on SQL Server, PostgreSQL and MySQL/MariaDB | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Native JSON documents | yes | **yes on PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | **yes on SQL Server and MySQL/MariaDB** | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| String JSON + dictionary functions (ClickHouse) | no | **yes on ClickHouse** | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Arrays (`cardinality`/`array_*`/`@>`/`&&`, ClickHouse `Array(T)`, `ARRAY JOIN`) | no | **yes** on PostgreSQL and ClickHouse | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `ArrayJoinClause`/`IArrayJoinRenderer.Render` |
| Row values / tuples (`ROW`/`(a, b)`, element access, row comparison) | yes (`Sql.Row`; emulated where the provider has no native row) | **yes** on PostgreSQL and ClickHouse | `ISqlDialect.Tuple`/`ITupleRenderer`, `Visitors/TupleSqlTranslator.cs` |
| Native range types + range-over-scalar-pairs (`Range<T>`, `Overlaps`, `range_contains`, bound inspection) | partial — `Sql.Row.Overlaps` only (no first-class `Range<T>` mapping) | **yes** — native range/multirange types on PostgreSQL; a mapped **pair of scalar columns** (`[RangeColumns]`) on SQL Server/MySQL/MariaDB/SQLite/ClickHouse | `Query/Range.cs`, `RangeColumnsAttribute`, `ISqlDialect.SupportsRanges`/`SupportsRangeColumns`, `SqlFunctions.Postgres` |
| Conditional functions (`iif`/`choose`/`multi_if`) | no | **yes** | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes (provider) | **yes on SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | yes (provider) | **partial** — SQL Server only | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | partial | **yes** (NULL handling is provider-specific) | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | **yes** (`array_agg` on PostgreSQL) | `SupportsStringAgg`/`SupportsArrayAgg` |
| User-defined scalar functions | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Table-valued functions | yes (`TableFunction`; DB TVFs can be scaffolded) | **yes** (`[SqlTableFunction]`) | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Native `PIVOT` / `UNPIVOT` source | no (raw SQL) | **yes on SQL Server** | `EntityBuilder.Pivot`/`Unpivot` |
| Raw SQL (whole query) | yes | yes | `WithSql` / `PrepareFromSql` |
| Raw SQL as a composable source/subquery | yes | **yes** | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Statement-level query hints | partial (provider-specific `QueryHint`: SQL Server `OPTION (...)`, MySQL/Oracle `/*+ ... */`, ClickHouse `SETTINGS`; no PostgreSQL hint API) | **yes** — SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB inline `/*+ ... */`; SQLite/ClickHouse reject | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Locking table hints (e.g. `WITH (NOLOCK)`) | yes — SQL Server only (`SqlServerHints.TableHint`; the MySQL `TableHint` is optimizer-only, PostgreSQL/ClickHouse have no table-hint syntax) | **yes** — SQL Server only | `EntityBuilder.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Index hints (`USE`/`FORCE`/`IGNORE INDEX`, `INDEXED BY`, `WITH (INDEX(...))`) | yes (`IndexHint`/`TableHint`, provider-specific) | **yes** — MySQL/MariaDB, SQLite and SQL Server; PostgreSQL (without `pg_hint_plan`), ClickHouse and in-memory reject | `EntityBuilder.WithIndex`/`WithoutIndex`, `ISqlDialect.IndexHints`/`IIndexHintRenderer` |
| Identifier quoting | yes (per provider) | **yes** (opt-in) | `ISqlDialect.QuoteIdentifier` |
| Naming conventions (e.g. snake_case) | partial — no built-in convention | **yes** (opt-in, built-in `SnakeCaseNamingConvention`) | `INamingConvention` / `SnakeCaseNamingConvention` |
| SQL keyword casing (upper/lower) | no (keywords are emitted in the provider's canonical case) | **yes** — opt-in `KeywordCase.Upper`; the default `KeywordCase.Lower` is byte-for-byte the historical output | `KeywordCase`, `DataContextBuilder.UseKeywordCase`/`EntityBuilder.WithKeywordCase` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **yes** | `InsertBuilder<TEntity>`, `InsertReturningBuilder<TEntity,TResult>`, `MergeBuilder<TEntity>`, `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>`, `DeleteBuilder<TEntity>`, `UpdateBuilder<TEntity>`, `MutationCteQuery<TResult>`, `ISqlDialect.SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`/`SupportsIdentityFunction`/`SupportsDataModifyingCtes`/`SupportsOnConflict`/`SupportsOnDuplicateKey`/`SupportsMerge`/`SupportsDelete`; the in-memory provider only applies the key upsert |
| Bulk copy / merge / temporary tables | yes | **partial** — key upsert (`MergeInto`/`MergeBuilder<T>`), full `MERGE` with branches (`WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource`, arbitrary conditions, `RETURNING`/`OUTPUT`; SQL Server, PostgreSQL 15+), bulk insert (`BulkInsertInto<T>`: native `COPY`/`SqlBulkCopy` + chunked `INSERT ... VALUES`, configured through the `BulkInsertOptions` record or the fluent `BulkInsertOptionsBuilder` — `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`, `IgnoreDuplicates`, `KeepIdentity`, `Timeout`, `NotifyAfter` progress with a `ProgressCancellationTokenSource`; `ReturningKey`/`Returning`) and materializing a query into a (temporary) table (`ToTempTable`/`ToTable`, `CREATE [TEMPORARY] TABLE ... AS SELECT`, PostgreSQL/SQLite/MySQL/MariaDB) are implemented | `MergeBuilder<TEntity>`, `BulkInsertBuilder<TEntity>`, `BulkInsertOptions`, `TempTableExtensions` |
| Transactions (own + enlisted) | yes | **yes** (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB; ClickHouse and in-memory reject) | `DataContext/Roles/ITransactionManager.cs`, `DataContext/DbConnectionManager.cs`, `ISqlDialect.SupportsTransactions` |
| Navigation properties / associations / eager loading | yes (`[Association]`, `LoadWith`) | **no** | — |
| Change tracking / identity map | partial | **no** (by design) | — |
| Extensibility (interceptors, custom SQL, query filters) | extensive | minimal (dialect + `[SqlFunction]`/`[SqlTableFunction]`) | `SqlDialectBase` |
| Providers | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | `src/nextorm.*` |
| EF Core integration | yes (`linq2db.EntityFrameworkCore`) | **no** (planned: [EF Core integration](../roadmap/todo_efcore_integration.md)) | — |
| Performance posture | high | benchmarked at/above Dapper, EF Core and linq2db on the shipped scenarios | `docs/specs/performance/benchmark-report.md` |

## Where nextorm leads

Against the shared surface nextorm matches or exceeds linq2db; on top of that it adds:

* The complete analytic query surface: every join type including `APPLY`/`LATERAL` (including correlated
  sources), derived-table joins, ClickHouse join strictness/`GLOBAL`, set operations, `DISTINCT`
  (plus `DISTINCT ON`/`WITH TIES`), CTEs (recursive, plus PostgreSQL **data-modifying CTEs**), window functions (named windows, `GROUPS`,
  frame `EXCLUDE`, `nth_value`, `percent_rank`/`cume_dist`), `ROLLUP`/`CUBE`/`GROUPING SETS`/`WITH TOTALS`,
  `CASE`/`COALESCE`/`CAST`, string/math/date functions, `IN`-lists, UDF/TVF mapping, native `PIVOT`/`UNPIVOT`,
  temporal tables, row locking and raw SQL for a whole query.
* A complete, explicit write surface: `INSERT ... VALUES`/`INSERT ... SELECT` (single row, entity, batch), the
  generated key (`ReturningIdentity`/`ReturningKey`) and inserted rows (`Returning`, PostgreSQL/SQLite/SQL Server),
  **key upsert** (`MergeInto`), **`DELETE`** (`DeleteFrom`/`Delete<T>`/`Truncate<T>`), **`UPDATE`**
  (`Update<T>`/`UpdateBuilder<T>`) and **full `MERGE`** with `WHEN MATCHED`/`WHEN NOT MATCHED`/
  `WHEN NOT MATCHED BY SOURCE` branches and `RETURNING`/`OUTPUT` (SQL Server, PostgreSQL 15+); bulk insert
  (`BulkInsertInto<T>`, native `COPY`/`SqlBulkCopy` or chunked `VALUES`, with the `BulkInsertOptions`/
  `BulkInsertOptionsBuilder` surface) and materializing a query into a table (`ToTempTable`/`ToTable`); on
  PostgreSQL, **data-modifying CTEs** (`With(name, insert)`/`CteQuery.With(name, insert)` → a write CTE whose
  `RETURNING` rows are read typed with the full operator set, or fed into a further `INSERT ... SELECT`); and
  **transactions** (`ITransactionManager`, own or enlisted from EF Core/Dapper/ADO.NET) — all explicit
  commands, with no change tracking and no `SaveChanges`.
* Cross-provider row values: `System.Tuple`/`ValueTuple` constructors, element access and row comparison
  render as `ROW(a, b)`/`(row).fN` on PostgreSQL and `tuple(a, b)`/`tupleElement` on ClickHouse, driven by
  `ISqlDialect.Tuple`.
* Range types without a native range column: PostgreSQL maps `Range<T>` natively, while the other
  providers store it as a pair of scalar bounds (`[RangeColumns]`) and translate the whole predicate and
  inspection surface (`overlaps`, `range_contains`/`range_contained_by`, the positional and adjacency
  predicates, `lower`/`upper`/`isempty`) over the pair — a mapping linq2db lacks.
* Index hints (`WithIndex`/`WithoutIndex`) across MySQL/MariaDB, SQLite and SQL Server, and configurable
  SQL keyword casing (`KeywordCase.Upper`) — both opt-in and both part of the plan key.
* Correlated subqueries evaluate once per outer row on the in-memory provider too (depth-one scalar,
  aggregate, `EXISTS` and `IN`), in addition to the SQL providers' arbitrary nesting depth.
* Provider-portable rendering: the same C# renders `CROSS APPLY` on SQL Server and
  `CROSS JOIN LATERAL` on PostgreSQL/MySQL/MariaDB, driven by `ISqlDialect` capabilities.
* Provider-only surfaces behind the same capability gate — PostgreSQL native JSON, arrays, the
  range/multirange types and the extended scalar library; SQL Server `FOR JSON`/`FOR XML`, JSON-as-text
  and `string_split`/`openjson`;
  ClickHouse `Array(T)`/`ARRAY JOIN`, `JSONExtract*`, dictionaries, the quantile/`uniq`/`argMin`-`argMax`
  families, `LIMIT BY`, `PREWHERE`/`FINAL`/`SETTINGS` and multi-branch `multiIf`.
* Full-text search (`contains`/`freetext`) on SQL Server, PostgreSQL and MySQL/MariaDB.
* CLR `Regex` translation (`Regex.IsMatch`/`Regex.Replace`) with a constant pattern on PostgreSQL,
  MySQL/MariaDB, ClickHouse, SQLite and SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`) — an open
  feature request in linq2db (`linq2db#698`).
* Two reuse paths (implicit plan cache and explicit `Prepare()`), query parametrisation and a
  benchmarked low-allocation design.
* Benchmarked performance: on the fast (tmpfs) full run the prepared path wins every measured class
  against Dapper, EF Core and linq2db (`Any`, `First`, `Join`, `Single`, `Where`, `LargeIteration`
  `ToList`/stream and `Cache`) with a small allocation footprint. The remaining gap is the warm
  (non-prepared) path: `CTE` ~1.31×, recursive `CTE` ~1.52×, `Join4` ~1.15× and captured `IN`
  ~1.61–1.70× behind Dapper; iteration 8 closed the inline-`IN` refresh cost
  (`docs/specs/performance/benchmark-report.md`, iterations 3–8).
* SQL Server statement-level hints with plan-key participation (`Hint(...)`), coalescing cleanly with
  the CTE `option (maxrecursion n)` clause, plus SQL Server table hints (`WithTableHint`).
* Configurable SQL output: identifier quoting, naming conventions and keyword casing are opt-in and
  overridable per command (`UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`,
  `UseNamingConvention()`/`WithNamingConvention()`, `UseKeywordCase()`/`WithKeywordCase()`), so the
  generated SQL stays predictable by default while reserved-word/mixed-case schemas, snake_case catalogs
  and upper-case-keyword preferences need no hand-written names.

## Deliberate boundaries: what nextorm leaves to linq2db

These are conscious scope decisions in nextorm's focused, no-change-tracking model — not gaps on the query
surface, which nextorm matches or exceeds. linq2db covers them:

* **Change tracking and identity map**: nextorm ships the full explicit DML surface (`INSERT`/`UPDATE`/
  `DELETE`/full `MERGE`), bulk insert, `CREATE TABLE AS SELECT` and transactions, but every write stays an
  explicit command — there is no `SaveChanges` and no automatic change tracking. linq2db flushes a tracked
  unit of work.
* **Relationships**: nextorm has no relationship metadata — joins are always explicit; linq2db adds
  `[Association]`, `LoadWith` eager loading and implicit join inference.
* **Plug-in extensibility and broader table hints**: linq2db offers interceptors, query filters and
  custom-SQL mapping, plus table hints on more providers (for example Oracle); nextorm deliberately keeps a
  fixed dialect contract with statement hints, SQL Server locking hints and index hints on MySQL/MariaDB,
  SQLite and SQL Server.
* **Database-first tooling**: linq2db ships a CLI/T4 code-generation toolchain that scaffolds entity and
  table-function mappings from a live database; nextorm declares mappings in code. The dynamic-schema
  sources nextorm supports through a caller-declared `TRow` schema (ClickHouse `values()`, PostgreSQL
  `jsonb_to_record(set)`, [dynamic result schema](../../guide/13-table-valued-functions.md#dynamic-result-schema))
  are unsupported by linq2db as well.
* **Provider breadth**: linq2db adds Oracle, Firebird, DB2, SAP HANA, Informix, Sybase and SQL CE; nextorm
  focuses on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse.
* **EF Core integration** package and a larger ecosystem (nextorm's integration is
  [planned](../roadmap/todo_efcore_integration.md)).

Correlation is uniform on the SQL providers (arbitrary nesting depth for scalar subqueries, aggregate
terminals, `EXISTS`/`IN`/`ANY`/`ALL` and correlated `APPLY`/`LATERAL` sources). The in-memory provider
now evaluates depth-one correlated scalar/aggregate/`EXISTS`/`IN` once per outer row, and only rejects
the deeper forms — correlation depth greater than one, an outer reference inside the inner projection or
`ORDER BY`, a correlated `GROUP BY`/`HAVING` and an async inner source (see
[`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4).

## Architecture differences

| Aspect | linq2db | nextorm |
|---|---|---|
| Model | Explicit CRUD ORM with associations; no automatic change tracking | Query builder and mapper without change tracking, with a full explicit write surface (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), bulk insert and transactions |
| Entity requirement | A mapped class required (attributes, fluent or convention) | Entity class optional — mapped via attributes/fluent/conventions, or none at all with `From("table")` + `TableAlias` |
| Reuse | Compiled queries, query cache | Implicit plan cache and `Prepare()` |
| Identifier quoting | on by default (per provider) | off by default; enabled with `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` |
| Name mapping | fluent/attributes (`MappingSchema`) | attributes/fluent/auto-derived, plus opt-in naming conventions (`UseNamingConvention()`/`WithNamingConvention()`) |
| SQL keyword case | fixed (provider-canonical, upper-case) | configurable, lower-case by default (`KeywordCase`) |
| Extensibility | Interceptors, custom SQL, provider extensions | Dialect contract and function attributes |

## Summary

For reading, reporting and explicit data modification over an existing schema, nextorm is the stronger
choice: it covers essentially the whole analytic query surface linq2db offers — the provider-only function
families, the ClickHouse-specific constructs, cross-provider row values, TVFs and more — with a smaller
allocation footprint, benchmark results at or above Dapper, EF Core and linq2db on the shipped scenarios,
and more configurable SQL output (identifier quoting, naming conventions and keyword casing are opt-in and
overridable per command, whereas linq2db quotes by default and fixes names through its mapping schema).
linq2db remains the better fit only when the same layer must also model relationships, track
changes or generate the data layer from a live schema — surface nextorm deliberately
leaves out.

## See also

- [Capability matrix: nextorm vs EF Core and linq2db](capability-matrix.md) — the exhaustive per-construct matrix.
- [linq2db backlog gap analysis](linq2db-backlog-gap-analysis.md) — what linq2db *plans to add* and which of it nextorm lacks.
- [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core and linq2db, per construct.
- [Limitations and out-of-scope features](../../advanced/limitations.md)
- [Joins](../../guide/03-joins.md) — `CrossApply`/`OuterApply`.
- [Range columns](../../guide/31-range-columns.md) — a `Range<T>` stored as a pair of scalar columns.
- [Query hints](../../guide/17-query-hints.md)
- [Provider overview](../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/comparison/capability-matrix.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/performance/benchmark-report.md`.
linq2db capabilities are described from its public documentation.
