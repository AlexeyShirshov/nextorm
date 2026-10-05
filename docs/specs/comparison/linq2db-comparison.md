# nextorm vs linq2db: functionality comparison

> A side-by-side functional comparison of nextorm and linq2db. It complements the
> [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) and the
> [capability matrix](capability-matrix.md) (which also cover EF Core). Based on the current tree, it shows
> nextorm matching or exceeding linq2db across the analytic query surface — join types, `APPLY`/`LATERAL`,
> named join aliases, full-text/JSON/arrays, cross-provider row values, native range types (and
> range-over-scalar-pairs), CLR `Regex` translation, `ROLLUP`/`CUBE`/`GROUPING SETS`, date arithmetic,
> temporal tables, row locking, statement/table/index hints, configurable keyword casing, TVFs,
> unmapped-column access, extreme-row selection, per-query source overrides and depth-one in-memory
> correlation — and adding a complete explicit write surface
> (`INSERT`/`UPDATE`/`DELETE`/full `MERGE`, PostgreSQL data-modifying CTEs), bulk insert, `CREATE TABLE AS
> SELECT`, `TRUNCATE`, a transactions role, value converters, JSON columns, a dynamic-columns store,
> declarative relationships with level-1 eager loading and declared-relationship implicit navigation,
> global query filters, command interceptors, a SQL batch builder, result-set streaming and export, stored
> procedures and table-valued parameters, and an EF Core integration package.

**Prerequisites:** [Provider overview](../../providers/overview.md) · [Limitations](../../advanced/limitations.md) · [Query hints](../../guide/13-query-hints.md)

## Positioning

* **nextorm** is a focused, no-change-tracking SQL builder and mapper purpose-built for reading and
  reporting. It covers the complete analytic query surface, adds a complete explicit write surface
  (`INSERT`/`UPDATE`/`DELETE`/full `MERGE`, bulk insert, `CREATE TABLE AS SELECT`, and data-modifying CTEs on
  PostgreSQL and `TRUNCATE`) plus a transactions role (`ITransactionManager`, own and enlisted), and
  generates provider-portable SQL for SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse. It is
  built around a small allocation footprint, parameterisation and query compilation (implicit plan cache /
  explicit `Prepare()`), with opt-in output controls (identifier quoting, naming conventions, keyword
  casing) and index hints — and benchmarks at or above Dapper, EF Core and linq2db on the shipped
  scenarios. The mapping layer covers value converters, JSON columns and a dynamic-columns store; the
  execution layer covers raw commands and stored procedures, table-valued parameters, a SQL batch builder
  and result-set streaming and export. It also models relationships declaratively
  (`[Relationship]`/`HasMany`/`HasOne` + `JoinInto`) with level-1 `LoadWith` eager loading and
  declared-relationship implicit navigation, ships command interceptors and global query filters, and
  integrates with EF Core (`nextorm.entityframeworkcore`).
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

| Area | nextorm | linq2db | nextorm evidence |
|---| --- |---|---|
| Projection (`SELECT`, DTO/anonymous/record/tuple/scalar; unmapped columns via `SqlFunctions.Column`) | yes | yes | `EntityBuilder.Select`, `SqlFunctions.Column` |
| Predicates (`WHERE`: comparison, `and`/`or`/`!`, arithmetic, bitwise/shift) | yes | yes | `Visitors/WhereExpressionVisitor.cs`, `BaseExpressionVisitor.cs` |
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes (`FULL JOIN` not on MySQL/MariaDB) | yes | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin`, `ISqlDialect.SupportsFullJoin` |
| `APPLY` / `LATERAL` | yes (gated off on SQLite/ClickHouse) | yes | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Join strictness (`ANY`/`ALL`/`ASOF`) and `GLOBAL` | **yes** on ClickHouse | yes (ClickHouse `ClickHouseHints.Join.*`, incl. `Global*`) | `JoinStrictness`, `JoinOptions.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin` |
| Join arity | yes — up to 8 | yes | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| JOIN to a derived table (subquery) | yes | yes | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| Named join aliases (`Alias.<Name>`) | **yes** — generated `AliasProjection_*`/`AliasJoin_*` types address each slot (`JoinSlotAttribute`); SQL-provider only | no | `Alias`, generated `NextORM.Generated.*`, `JoinSlotAttribute` |
| Subqueries (`FROM`, scalar, correlated `EXISTS/IN/ANY/ALL`) | yes (in-memory evaluates depth one only) | yes | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference`, `DataContext/InMemoryCorrelatedPlan.cs` |
| `IN` over a list/array | **yes** | partial — no ClickHouse distributed `GLOBAL IN` | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs`, `SqlFunctions.ClickHouse.global_in` |
| `GROUP BY` / `HAVING` / aggregates (`FILTER`/`-If`, statistical/quantile/ordered families) | yes (ANSI `FILTER` on PostgreSQL/SQLite, `<fn>If` on ClickHouse; provider statistical/quantile/ordered families) | yes (`Sql.Ext` provider aggregates) | `EntityBuilder.GroupBy/Having`, `AdvancedAggregateTranslator.cs`, `Supports*Aggregates`, `AggregateFilterStyle` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes (`WITH TOTALS` on ClickHouse) | yes | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | **yes** on ClickHouse | no | `LimitBy`, `ILimitByRenderer.Render` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | **yes** on ClickHouse | partial — `FINAL` (`ClickHouseHints.Table.Final`) and `SETTINGS` (`ClickHouseHints.Query.Settings`) only; no `SAMPLE`/`PREWHERE` | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, dialect `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL` where the engine has it) | yes (provider dependent; `ALL` on PostgreSQL, MariaDB and ClickHouse) | yes | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | **yes** per provider (`DISTINCT ON` PostgreSQL; `WITH TIES` PostgreSQL + SQL Server; `TABLESAMPLE` PostgreSQL + SQL Server) | no | `DistinctOn`/`WithTies`/`FromOptions.TableSample`, `ISqlDialect.SupportsWithTies` |
| Row locking (`FOR UPDATE`/`FOR SHARE`, `NOWAIT`/`SKIP LOCKED`) | yes — `FOR UPDATE`/`FOR SHARE` per provider (PostgreSQL/MySQL/MariaDB trailing `FOR UPDATE`/`LOCK IN SHARE MODE`; SQL Server via table hints) **plus the `NOWAIT`/`SKIP LOCKED` wait modes** (`LockWaitMode`; MySQL switches a shared lock to `FOR SHARE`, SQL Server uses `READPAST` as the `SKIP LOCKED` approximation) | yes (provider-specific `SubQueryTableHint`: PostgreSQL `FOR UPDATE`/`FOR NO KEY UPDATE`/`FOR SHARE`/`FOR KEY SHARE`, MySQL `FOR UPDATE`/`FOR SHARE`/`LOCK IN SHARE MODE`, SQL Server via `UPDLOCK`/`XLOCK` table hints; plus `NOWAIT`/`SKIP LOCKED`) | `ForUpdate`/`ForShare` + `LockWaitMode`, `ILockRenderer`/`ILockRenderer.UsesTableHints`/`Render` |
| Temporal tables (`FOR SYSTEM_TIME`) | yes on SQL Server + MariaDB (`CONTAINED IN` — SQL Server only) | yes | `ForSystemTime`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| CTEs (including recursive and typed `AsCte`/`AsRecursiveCte`) | **yes** — the typed surfaces carry the anchor projection, auto-`UNION ALL`/hoisting; PostgreSQL data-modifying CTEs | yes (no data-modifying CTEs) | `Cte<T>`/`QueryCommand<T>.AsCte`/`AsRecursiveCte`, `Builders/MutationCteQuery.cs`, `ISqlDialect.SupportsDataModifyingCtes` |
| Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`, percentiles) | **yes** (named windows, `GROUPS`/`EXCLUDE`, window percentiles) | partial — no named windows or frame `GROUPS`/`EXCLUDE` | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion`/`SupportsPercentileWindow` |
| `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| String / math / date scalar functions, `LIKE`, string concatenation, `NULLIF`, PostgreSQL settings/sequences | yes — portable CLR `string` methods and `+` concatenation on every provider, `SqlFunctions.Sql.nullif`, plus the native string/`regexp_*`/settings/sequence library on PostgreSQL (`SqlFunctions.Postgres`) | yes | `Visitors/ScalarFunctionTranslator.cs`, dialect `Make*` hooks, `SqlFunctions.Sql.nullif`, `SqlFunctions.Postgres` |
| CLR `Regex` (`IsMatch`/`Replace`, constant pattern) | **yes** — PostgreSQL, MySQL/MariaDB, ClickHouse, SQLite and SQL Server 2025+ (`REGEXP_LIKE`/`REGEXP_REPLACE`; 2019/2022 reject) | partial — open `linq2db#698` (no `Regex.IsMatch` translation) | `Visitors/RegexSqlTranslator.cs`, `ISqlDialect.SupportsRegex`/`MakeRegexMatch`/`MakeRegexReplace` |
| Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Full-text search | yes on SQL Server, PostgreSQL and MySQL/MariaDB | yes (provider) | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Native JSON documents | **yes on PostgreSQL** | partial — the `json`/`jsonb` type plus `JsonContains` (`@>`), `JsonExtractPathText` (`#>>`) and `Json.Value`; not the full `jsonb_*` library | `SupportsJson`, `JsonSqlTranslator` |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | yes on SQL Server and MySQL/MariaDB | yes | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| String JSON + native-JSON + dictionary functions (ClickHouse) | **yes on ClickHouse** (`JSONExtract*`/`JSONAllPaths`/`toJSONString`/`visitParam*`, plus dictionaries) | no dedicated API | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Arrays (`cardinality`/`array_*`/`@>`/`&&`, ClickHouse `Array(T)`, `ARRAY JOIN`) | **yes** on PostgreSQL and ClickHouse | partial — PostgreSQL array operators (`PostgreSQLExtensions`); no ClickHouse `Array(T)`/higher-order/`ARRAY JOIN` API | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `ArrayJoinClause`/`IArrayJoinRenderer.Render` |
| Row values / tuples (`ROW`/`(a, b)`, element access, row comparison) | yes on PostgreSQL and ClickHouse | yes (`Sql.Row`; emulated where the provider has no native row) | `ISqlDialect.Tuple`/`ITupleRenderer`, `Visitors/TupleSqlTranslator.cs` |
| Native range types + range-over-scalar-pairs (`Range<T>`, `Overlaps`, `range_contains`, bound inspection) | **yes** — native range/multirange types on PostgreSQL; a mapped **pair of scalar columns** (`[RangeColumns]`) on SQL Server/MySQL/MariaDB/SQLite/ClickHouse | partial — provider-native `NpgsqlRange<T>`/multirange mapping on PostgreSQL; no portable `Range<T>`/scalar-pair mapping | `Query/Range.cs`, `RangeColumnsAttribute`, `ISqlDialect.SupportsRanges`/`SupportsRangeColumns`, `SqlFunctions.Postgres` |
| Conditional functions (`iif`/`choose`/`multi_if`) | **yes** | no | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes on SQL Server | yes (provider) | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | partial — SQL Server only | yes (provider) | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | **yes** (NULL handling is provider-specific) | partial | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes (`array_agg` on PostgreSQL) | yes | `SupportsStringAgg`/`SupportsArrayAgg` |
| User-defined scalar functions | yes (`[SqlFunction]`) | yes (`DbFunction` / `Sql.Ext`) | `SqlFunctionAttribute.cs` |
| Table-valued functions | yes (`[SqlTableFunction]`) | yes (`TableFunction`; DB TVFs can be scaffolded) | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Native `PIVOT` / `UNPIVOT` source | **yes on SQL Server** | no (raw SQL) | `EntityBuilder.Pivot`/`Unpivot` |
| Raw SQL (whole query) | yes | yes | `WithSql` / `PrepareFromSql` |
| Raw SQL as a composable source/subquery | yes | yes | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Per-query source overrides (`WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/`WithTableExpression`) | **yes** — provider-gated per level, part of the plan key; SQL Server 4-part, MySQL/MariaDB/ClickHouse `db.table`, PostgreSQL/SQLite `schema.table` | partial (`Table(Name=...)`, `Sql.TableExpression`) | `EntityBuilder.WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/`WithTableExpression`, `ISqlDialect.SupportsCrossDatabase`/`SupportsLinkedServer` |
| Statement-level query hints | **yes** — SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB inline `/*+ ... */`; SQLite/ClickHouse reject | partial (provider-specific `QueryHint`: SQL Server `OPTION (...)`, MySQL/Oracle `/*+ ... */`, ClickHouse `SETTINGS`; no PostgreSQL hint API) | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Locking table hints (e.g. `WITH (NOLOCK)`) | yes — SQL Server only | yes — SQL Server only (`SqlServerHints.TableHint`; the MySQL `TableHint` is optimizer-only, PostgreSQL/ClickHouse have no table-hint syntax) | `FromOptions.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Index hints (`USE`/`FORCE`/`IGNORE INDEX`, `INDEXED BY`, `WITH (INDEX(...))`) | yes — MySQL/MariaDB, SQLite and SQL Server; PostgreSQL (without `pg_hint_plan`), ClickHouse and in-memory reject | yes (`IndexHint`/`TableHint`, provider-specific) | `FromOptions.WithIndex`/`WithoutIndex`, `ISqlDialect.IndexHints`/`IIndexHintRenderer` |
| Identifier quoting | yes (opt-in) | yes (per provider) | `ISqlDialect.QuoteIdentifier` |
| Naming conventions (e.g. snake_case) | **yes** (opt-in, built-in `SnakeCaseNamingConvention`) | partial — no built-in convention | `INamingConvention` / `SnakeCaseNamingConvention` |
| SQL keyword casing (upper/lower) | **yes** — opt-in `KeywordCase.Upper`; the default `KeywordCase.Lower` is byte-for-byte the historical output | no (keywords are emitted in the provider's canonical case) | `KeywordCase`, `DataContextBuilder.UseKeywordCase`/`EntityBuilder.WithKeywordCase` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | yes | `InsertBuilder<TEntity>`, `InsertReturningBuilder<TEntity,TResult>`, `MergeBuilder<TEntity>`, `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>`, `DeleteBuilder<TEntity>`, `UpdateBuilder<TEntity>`, `CreateDeleteJoinBuilder`/`DeleteJoinBuilder<TProjection>`, `CreateUpdateJoinBuilder`/`UpdateJoinBuilder<TProjection>`, `DataContextExtensions.Update`/`Delete`, `CreateTruncateBuilder`, `MutationCteQuery<TResult>`, `ISqlDialect.SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`/`SupportsIdentityFunction`/`SupportsDataModifyingCtes`/`SupportsOnConflict`/`SupportsOnDuplicateKey`/`SupportsMerge`/`SupportsDelete`; the in-memory provider only applies the key upsert |
| Bulk copy / merge / temporary tables | partial — key upsert (`CreateMergeBuilder`/`MergeBuilder<T>`), full `MERGE` with branches (`WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource`, arbitrary conditions, `RETURNING`/`OUTPUT`; SQL Server, PostgreSQL 15+), bulk insert (`CreateBulkInsertBuilder<T>`: native `COPY`/`SqlBulkCopy` + chunked `INSERT ... VALUES`, configured through the `BulkInsertOptions` record or the fluent `BulkInsertOptionsBuilder` — `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`, `IgnoreDuplicates`, `KeepIdentity`, `Timeout`, `NotifyAfter` progress with a `ProgressCancellationTokenSource`; `ReturningKey`/`Returning`) and materializing a query into a (temporary) table (`ToTable` on PostgreSQL/SQLite/MySQL/MariaDB/SQL Server/ClickHouse, the `ToTempTable` temporary form on PostgreSQL/SQLite/MySQL/MariaDB) are implemented | yes | `MergeBuilder<TEntity>`, `BulkInsertBuilder<TEntity>`, `BulkInsertOptions`, `TempTableExtensions` |
| Transactions (own + enlisted) | yes (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB; ClickHouse and in-memory reject) | yes | `DataContext/Roles/ITransactionManager.cs`, `DataContext/DbConnectionManager.cs`, `ISqlDialect.SupportsTransactions` |
| Navigation properties / associations / eager loading | partial — navigation metadata (O2M/M2O/O2O/M2M) + `JoinInto` (one-to-many collections, one-to-one references, many-to-many through an explicit junction), level-1 `LoadWith` (split / single-query), and declared-relationship implicit navigation (reference scalar chains/presence/whole-reference as `LEFT JOIN`; collection `Any`/`Count`/`LongCount`/`Count`); what remains is convention-over-FK inference (absent in linq2db too), composite keys, a composite junction selector and a many-to-many `JoinInto` under `AsSingleQuery` | yes (`[Association]`, `LoadWith`) | `Builders/EntityBuilder.cs`, `JoinIntoSpec.cs`/`JoinIntoStitcher.cs`, `Builders/EntityBuilderEagerLoading.cs`, `AsEntityBuilder<T>` |
| Change tracking / identity map | no (by design) | partial | — |
| Extensibility (interceptors, custom SQL, query filters) | yes — dialect + `[SqlFunction]`/`[SqlTableFunction]`, command/connection interceptors, global query filters and raw SQL | extensive | `SqlDialectBase`, `IQueryInterceptor`/`IConnectionInterceptor`, `QueryFilterAttribute`/`HasQueryFilter` |
| Providers | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, ClickHouse, DuckDB, Ydb | `src/nextorm.*` |
| EF Core integration | yes — `nextorm.entityframeworkcore`, shares the EF connection/transaction ([EF Core integration](../../advanced/integration-efcore.md)); the opt-in DML/`SaveChanges` bridge is out of scope | yes (`linq2db.EntityFrameworkCore`) | `src/nextorm.entityframeworkcore` |
| Raw command execution and stored procedures (`ExecuteRaw`/`ExecuteProcedure`, output/return parameters, forward-only cursors) | yes — stored procedures on SQL Server/PostgreSQL/MySQL/MariaDB | yes | `IRawCommandExecutor`, `ProcedureResult`/`ProcedureParameter`, `ISqlDialect.SupportsStoredProcedures` |
| Table-valued parameters | yes — native SQL Server, array/JSON/`Array(T)`+`arrayJoin` emulation elsewhere | yes (`TableParameterValue`) | `ProcedureParameter.Table<T>`, `ISqlDialect.SupportsTableValuedParameters` |
| SQL batch (`CreateBatchBuilder`/`BatchQuery<TResult>`/`BatchResult`, typed result sets in one round trip) | **yes** (PostgreSQL/SQL Server/MySQL/MariaDB/SQLite) | partial — batch only for a remote context (`BeginBatch`/`CommitBatch`); no local multi-statement builder | `BatchExtensions`/`BatchBuilder`/`BatchResult`, `ISqlDialect.SupportsBatch` |
| Result-set streaming consumption (`ToStream`/`ToTextReader`/`ToDataReader`) | **yes** | partial — raw `DbDataReader` (`ExecuteReader`/`DataReaderWrapper`) and materialising `IAsyncEnumerable`; no LOB streaming terminal | `QueryCommandExtensions.ToStream`/`ToTextReader`/`ToDataReader`, `ISqlDialect.LobLocatorColumn`/`SupportsSequentialAccess` |
| Result-set export to a stream (`WriteJson`/`WriteCsv`) | **yes** | partial (client serialization) | `QueryCommand.WriteJson`/`WriteCsv`, `JsonStreamOptions`/`CsvStreamOptions` |
| `TRUNCATE` | yes (SQLite/in-memory reject) | yes | `DataContextExtensions.CreateTruncateBuilder` |
| Query plan cache and `Prepare()` | yes — implicit structural plan cache plus explicit `Prepare()` | yes | `EntityBuilderExtensions.Prepare`, `IPreparedQueryCommand<TResult>`, `DataContextCache` |
| `SelectMany` / `GroupJoin` | partial — in-memory only; SQL providers reject | yes (SQL translation) | `EntityBuilder.SelectMany`/`GroupJoin`, `InMemoryLinqSource` |
| Context configuration, logging and DI (`DataContextBuilder`, `AddNextOrmContext`) | yes | yes | `DataContextBuilder`, `ServiceCollectionExtensions.AddNextOrmContext` |
| In-memory provider (query-only, registered CLR datasets) | **yes** | partial | `InMemoryDataContext`, `WithData`/`WithAsyncData` |
| Optimistic concurrency (token-guarded pattern; no concurrency-token metadata) | partial | yes | `Returning`, `DataContextExtensions.Update` |
| Connection lifecycle (`IConnectionManager`: supplied vs owned, reuse, disposal) | yes | yes | `IConnectionManager.GetConnection`/`EnsureConnectionOpen`, `DbConnectionManager` |
| Performance posture | **highest** | high | `docs/specs/performance/benchmark-report.md` |

## Where nextorm leads

Against the shared surface nextorm matches or exceeds linq2db; on top of that it adds:

* The complete analytic query surface: every join type including `APPLY`/`LATERAL` (including correlated
  sources), derived-table joins, ClickHouse join strictness/`GLOBAL`, set operations, `DISTINCT`
  (plus `DISTINCT ON`/`WITH TIES`), CTEs (recursive, plus PostgreSQL **data-modifying CTEs**), window functions (named windows, `GROUPS`,
  frame `EXCLUDE`, `nth_value`, `percent_rank`/`cume_dist`), `ROLLUP`/`CUBE`/`GROUPING SETS`/`WITH TOTALS`,
  `CASE`/`COALESCE`/`CAST`, string/math/date functions, `IN`-lists, UDF/TVF mapping, native `PIVOT`/`UNPIVOT`,
  temporal tables, row locking and raw SQL for a whole query.
* Unmapped columns, named join aliases and per-query source overrides: read a column with no mapped property
  (`SqlFunctions.Column`), address each join slot through generated `Alias.<Name>` types (`JoinSlotAttribute`),
  and override the table/schema/database/server or supply a raw table expression per query
  (`WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/`WithTableExpression`) — each folded into the plan key.
* Extreme-row selection as a dedicated operator: `SelectWhereMax`/`SelectWhereMin` with
  `ExtremeRowTies.One`/`All`, lowered portably but using the native strategy on PostgreSQL (`DISTINCT ON` /
  `ORDER BY ... LIMIT 1`) and ClickHouse (`argMin`/`argMax`). linq2db has no such operator (it composes
  window functions plus `OrderBy`/`Take`), and EF Core only reaches the single-row case (`OrderBy`+`First`,
  `MaxBy`/`MinBy`).
* A first-class JSON column (`[JsonColumn]` with Auto/Native/Text storage) — the mapping surface linq2db
  still lacks (open `linq2db#1661`).
* A complete, explicit write surface: `INSERT ... VALUES`/`INSERT ... SELECT` (single row, entity, batch), the
  generated key (`ReturningIdentity`/`ReturningKey`) and inserted rows (`Returning`, PostgreSQL/SQLite/SQL Server),
  **key upsert** (`CreateMergeBuilder`), **`DELETE`** (`CreateDeleteBuilder`/`Delete<T>`/`CreateTruncateBuilder<T>`), **`UPDATE`**
  (`CreateUpdateBuilder<T>`/`UpdateBuilder<T>`) and **full `MERGE`** with `WHEN MATCHED`/`WHEN NOT MATCHED`/
  `WHEN NOT MATCHED BY SOURCE` branches and `RETURNING`/`OUTPUT` (SQL Server, PostgreSQL 15+); bulk insert
  (`CreateBulkInsertBuilder<T>`, native `COPY`/`SqlBulkCopy` or chunked `VALUES`, with the `BulkInsertOptions`/
  `BulkInsertOptionsBuilder` surface) and materializing a query into a table (`ToTempTable`/`ToTable`); on
  PostgreSQL, **data-modifying CTEs** (`With(name, insert)`/`CteQuery.With(name, insert)` → a write CTE whose
  `RETURNING` rows are read typed with the full operator set, or fed into a further `INSERT ... SELECT`); and
  **transactions** (`ITransactionManager`, own or enlisted from EF Core/Dapper/ADO.NET) — all explicit
  commands, with no change tracking and no `SaveChanges`.
* Declarative relationships and eager loading: O2M/M2O via `[Relationship]`/`HasMany`/`HasOne` and
  `JoinInto` (LEFT/INNER, `Where`, parent paging, multiple collections, in-memory parity), plus level-1
  `LoadWith` (split-query by default, opt-in `AsSingleQuery`) with global query filters applied to children
  ([relationships](../../advanced/relationships.md), [eager loading](../../advanced/eager-loading.md)).
* An EF Core integration package (`nextorm.entityframeworkcore`): `UseNextOrm`/`GetNextOrmContext`/
  `AddNextOrmFromDbContext` run nextorm over the EF connection with the EF model mapping and share the EF
  transaction; `ToNextOrm` translates a bounded EF `IQueryable` subset
  ([EF Core integration](../../advanced/integration-efcore.md)).
* Global query filters (soft-delete / multi-tenancy): per-entity predicates auto-injected into queries,
  joins, subqueries and eagerly-loaded children, with keyed filters, selective `IgnoreFilters`,
  `UPDATE`/`DELETE` filters and `INSERT`/`MERGE` validation ([query filters](../../advanced/query-filters.md)).
* Command/connection interceptors and structured logging over the ADO pipeline
  ([interceptors](../../infrastructure/03-interceptors.md)).
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
* **Relationships**: nextorm models O2M/M2O/O2O/M2M relationships declaratively (`[Relationship]`/`HasMany`/
  `HasOne`/`HasOneToOne`/`HasManyThrough` + `JoinInto`) with level-1 `LoadWith` eager loading and
  declared-relationship implicit navigation ([relationships](../../advanced/relationships.md),
  [eager loading](../../advanced/eager-loading.md), [implicit navigation](../../guide/29-implicit-navigation.md));
  what linq2db still adds is eager-load ordering/strategy — convention-over-FK inference is absent in both,
  since associations are declared in each library.
* **Database-first tooling**: linq2db ships a CLI/T4 code-generation toolchain that scaffolds entity and
  table-function mappings from a live database; nextorm declares mappings in code. The dynamic-schema
  sources nextorm supports through a caller-declared `TRow` schema (ClickHouse `values()`, PostgreSQL
  `jsonb_to_record(set)`, [dynamic result schema](../../guide/11-table-valued-functions.md#dynamic-result-schema))
  are unsupported by linq2db as well.
* **Provider breadth**: the two overlap on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse;
  linq2db additionally ships Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB and
  Ydb, while nextorm adds an in-memory object context and separate MySQL/MariaDB providers (linq2db models
  MariaDB as a MySQL version).
* **DDL / schema management**: linq2db ships `CreateTable`/`DropTable` and a schema API; nextorm mutates the
  schema only through `CREATE TABLE AS SELECT` — ad-hoc DDL goes through `ExecuteRaw` (out of scope).
* **External / linked-server sources**: `WithServer`/`WithDatabase` only rename the `FROM` qualifier;
  `OPENROWSET`/`OPENQUERY` are out of scope (linq2db has no first-class surface either).
Correlation is uniform on the SQL providers (arbitrary nesting depth for scalar subqueries, aggregate
terminals, `EXISTS`/`IN`/`ANY`/`ALL` and correlated `APPLY`/`LATERAL` sources). The in-memory provider
now evaluates depth-one correlated scalar/aggregate/`EXISTS`/`IN` once per outer row, and only rejects
the deeper forms — correlation depth greater than one, an outer reference inside the inner projection or
`ORDER BY`, a correlated `GROUP BY`/`HAVING` and an async inner source (see
[`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4).

## Architecture differences

| Aspect | nextorm | linq2db |
|---|---|---|
| Model | Query builder and mapper without change tracking, with a full explicit write surface (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), bulk insert and transactions | Explicit CRUD ORM with associations; no automatic change tracking |
| Entity requirement | Entity class optional — mapped via attributes/fluent/conventions, or none at all with `From("table")` + `TableAlias` | A mapped class required (attributes, fluent or convention) |
| Reuse | Implicit plan cache and `Prepare()` | Compiled queries, query cache |
| Result consumption | First-class non-materialising terminals on the query: single-column LOB streaming (`ToStream`/`ToTextReader`), a caller-owned `DbDataReader` (`ToDataReader`) and JSON/CSV export (`WriteJson`/`WriteCsv`) that stream rows without constructing `TResult` | Always materialises the mapped type per row; raw access only through `DataConnection.ExecuteReader`/`DataReaderWrapper` (`DbDataReader`), with no LOB or JSON/CSV terminals |
| Identifier quoting | off by default; enabled with `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` | on by default (per provider) |
| Name mapping | attributes/fluent/auto-derived, plus opt-in naming conventions (`UseNamingConvention()`/`WithNamingConvention()`) | fluent/attributes (`MappingSchema`) |
| SQL keyword case | configurable, lower-case by default (`KeywordCase`) | fixed (provider-canonical, upper-case) |
| Extensibility | Dialect contract (`ISqlDialect`), `[SqlFunction]`/`[SqlTableFunction]` attributes, interceptors (`IQueryInterceptor`), global query filters and raw SQL | Interceptors, custom SQL, runtime `MappingSchema` and provider extension packages |

## Summary

For reading, reporting and explicit data modification over an existing schema, nextorm is the stronger
choice: it covers essentially the whole analytic query surface linq2db offers — the provider-only function
families, the ClickHouse-specific constructs, cross-provider row values, TVFs and more — with a smaller
allocation footprint, benchmark results at or above Dapper, EF Core and linq2db on the shipped scenarios,
and more configurable SQL output (identifier quoting, naming conventions and keyword casing are opt-in and
overridable per command, whereas linq2db quotes by default and fixes names through its mapping schema).
linq2db remains the better fit only when the same layer must also track changes, generate the data layer from
a live schema, or expose eager-load ordering/strategy — surface nextorm deliberately leaves out (O2M/M2O/O2O
relationships, many-to-many through a junction, declared-relationship implicit navigation and level-1 eager
loading are already covered).

## See also

- [Capability matrix: nextorm vs EF Core and linq2db](capability-matrix.md) — the exhaustive per-construct matrix.
- [linq2db backlog gap analysis](linq2db-backlog-gap-analysis.md) — what linq2db *plans to add* and which of it nextorm lacks.
- [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core and linq2db, per construct.
- [Limitations and out-of-scope features](../../advanced/limitations.md)
- [Joins](../../guide/02-joins.md) — `CrossApply`/`OuterApply`.
- [Range columns](../../guide/25-range-columns.md) — a `Range<T>` stored as a pair of scalar columns.
- [Query hints](../../guide/13-query-hints.md)
- [Provider overview](../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/comparison/capability-matrix.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/performance/benchmark-report.md`.
linq2db capabilities are described from its public documentation.
