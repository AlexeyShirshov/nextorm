# Capability matrix: nextorm vs EF Core and linq2db

> Part of the comparison [`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md),
> extracted into its own document. For per-provider details see `docs/providers/*.md`. Per-function
> coverage is tracked in [`sql-function-coverage-gap.md`](../roadmap/sql-function-coverage-gap.md).

Legend: **yes** = first-class support; **partial** = the library's own gap — implementable but not
implemented yet (a tracked gap/TODO); **no** = not supported. nextorm is the baseline, so its cell carries
the plain mark — a restriction imposed by the database engine itself is noted briefly in parentheses and
does not lower the mark. Where a competitor supports the construct but lacks a capability nextorm has, it is
marked **partial** with the missing piece named.

The managed SQL surface is split by portability: cross-provider helpers live on `SqlFunctions.Sql`, while the
provider-only functions live on a provider-specific surface — `SqlFunctions.Postgres` (native arrays, native
JSON, the extended scalar library, the PG-only aggregates and the `generate_series`/`unnest` table
functions), `SqlFunctions.SqlServer` (JSON-as-text on SQL Server and MySQL/MariaDB, plus SQL Server `string_split`/`openjson`) and `SqlFunctions.ClickHouse`
(`arg_min`/`arg_max` and the `-If` combinator). A call on another provider fails with a clear
`NotSupportedException`, and its capability flags are the same ones documented below.

| SQL construct | nextorm | linq2db | EF Core | Evidence in nextorm |
|---|---|---|---|---|
| INNER JOIN | **yes** | yes | yes | `SqlBuilder.MakeJoin` |
| LEFT JOIN | **yes** | yes | yes | `JoinType.Left` |
| RIGHT JOIN | **yes** | yes | no (workaround) | `JoinType.Right`, `ISqlDialect.SupportsRightFullJoin` |
| FULL JOIN | **yes** — gated on MySQL/MariaDB, no polyfill: the `LEFT JOIN … UNION … RIGHT JOIN` rewrite changes row shape/deduplication and is never emitted implicitly | yes | no (workaround) | `JoinType.Full`, `ISqlDialect.SupportsFullJoin`, `SqlSourceRenderer.MakeJoin` |
| CROSS JOIN | **yes** | yes | yes | `JoinType.Cross` |
| APPLY / LATERAL | **yes** — SQLite/ClickHouse have no lateral source (gated, no rewrite of an APPLY to a plain join) | yes | partial | `JoinType.CrossApply/OuterApply`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Join strictness (`ANY`/`ALL`/`ASOF`) and `GLOBAL` | **yes** on ClickHouse | no | no | `JoinStrictness`, `EntityBuilder.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin`/`MakeJoinKeyword`; `SEMI`/`ANTI`/`PASTE` via `JoinType.Semi`/`Anti`/`Paste` + `EntityBuilder.SemiJoin`/`AntiJoin`/`PasteJoin` (`SupportsSemiAntiJoin`/`SupportsPasteJoin`) |
| Statement-level query hints (`OPTION (...)`, `/*+ ... */`) | **yes** (SQLite/ClickHouse reject) | partial — no PostgreSQL API | no (raw SQL / command interceptor only) | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Locking table hints (`WITH (NOLOCK/UPDLOCK/HOLDLOCK/ROWLOCK/TABLOCK...)`, primary table) | **yes** (SQL Server only) | yes (SQL Server only) | no (raw SQL only) | `EntityBuilder.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Row locking (`FOR UPDATE`/`FOR SHARE`/`LOCK IN SHARE MODE`, `NOWAIT`/`SKIP LOCKED`) | **yes** — `FOR UPDATE`/`FOR SHARE` and the `NOWAIT`/`SKIP LOCKED` wait modes on PostgreSQL/MySQL/MariaDB (trailing clause) and SQL Server (`UPDLOCK`/`HOLDLOCK` table hints with `NOWAIT`/`READPAST`); SQLite/ClickHouse/in-memory reject | yes | no (raw SQL only) | `EntityBuilder.ForUpdate`/`ForShare` + `LockWaitMode`, `Lock`/`ILockRenderer`/`ILockRenderer.UsesTableHints`/`Render` |
| Index hints (`USE`/`FORCE`/`IGNORE INDEX`, `INDEXED BY`, `WITH (INDEX(...))`) | **yes** (PostgreSQL, ClickHouse and in-memory reject: no index-hint syntax) | partial — explicit only for MySQL/Oracle/SQL Server; ClickHouse/PostgreSQL have no syntax, so a raw hint is emitted and ignored (or invalid) | no | `EntityBuilder.WithIndex`/`WithoutIndex`, `ISqlDialect.IndexHints`/`IIndexHintRenderer` |
| SQL keyword casing | **yes** — opt-in `KeywordCase.Upper`; default `KeywordCase.Lower` keeps the historical output | no (fixed, upper-case) | no (fixed, provider-canonical) | `KeywordCase`, `DataContextBuilder.UseKeywordCase`/`EntityBuilder.WithKeywordCase` |
| JOIN to a derived table (subquery) | **yes** (in-memory rejects a derived primary source) | yes | yes | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| More than two joined tables | **yes** — up to 8 | yes | yes | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| Subquery in `FROM` | **yes** | yes | yes | `SqlBuilder.MakeFrom`, `FromExpression` |
| Scalar subquery in `SELECT`/`WHERE`/`ORDER BY`/`HAVING` | **yes** | yes | yes | `CommonTestSuite.CorrelatedQuery.cs`, `CorrelatedQueryTests.cs` |
| Correlated subquery | **yes** (in-memory evaluates depth one only) | yes | yes | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference`, `InMemoryCorrelatedPlan.cs` |
| `IN` (subquery) | **yes** | partial — no ClickHouse distributed `GLOBAL IN` | partial — no ClickHouse distributed `GLOBAL IN` | `SqlFunctions.@in`, `SqlFunctions.ClickHouse.global_in`, `BaseExpressionVisitor` |
| `IN` (list/array/`Contains`) | **yes** | yes | yes | `SqlFunctions.@in`, `Contains` |
| `EXISTS` / `ANY` / `ALL` | **yes** | yes | yes | `SqlFunctions.exists/any/all` |
| `WHERE` (and/or/not, comparisons) | **yes** | yes | yes | `WhereExpressionVisitor`, `VisitUnary` |
| `GROUP BY` | **yes** | yes | yes | `EntityBuilder.GroupBy`, `SqlBuilder` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` | **yes** (`WITH TOTALS` on ClickHouse) | yes | partial — no first-class `ROLLUP`/`CUBE`/`GROUPING SETS` | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | **yes** on ClickHouse | no | no | `LimitBy`, `ILimitByRenderer.Render` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | **yes** on ClickHouse for `FINAL`/`PREWHERE`/`SETTINGS`; the cross-provider `TABLESAMPLE` analog is exposed separately on PostgreSQL and SQL Server (see the `DISTINCT ON`/`WITH TIES`/`TABLESAMPLE` row) | no | no | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| Generated-row table functions (`numbers`/`numbers_mt`, `zeros`/`zeros_mt`, `generateRandom`) | **yes** on ClickHouse | yes (`string_split`/`openjson`) | yes (`generate_series`/`unnest`) | `ClickHouseFunctions.numbers`/`zeros`/`generate_random`, `INumbersRow`/`IZerosRow`/`IGenerateRandomRow`, `WrapTableFunction` |
| `HAVING` | **yes** | yes | yes | `EntityBuilder.Having` |
| Aggregates (`count`/`min`/`max`/`avg`/`sum`/`stdev`/`var`, distinct) | **yes** | yes | partial — no `FILTER (WHERE ...)`, and no statistical/regression/quantile families | `AdvancedAggregateTranslator.cs`, `WindowFunctionTranslator.cs`, `Supports*Aggregates`, `SupportsAnyValueAggregate`, `WrapsCountResult`, `SupportsPercentileWindow` |
| `SELECT DISTINCT` | **yes** | yes | yes | `EntityBuilder.IsDistinct` |
| `ORDER BY` (expression/ordinal, asc/desc, multiple) | **yes** | yes | yes | `EntityBuilder`, `SqlBuilder` |
| `LIMIT`/`OFFSET`/`TOP` | **yes** | yes | yes | dialect `MakePage`/`MakeTop` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | `DISTINCT ON` — **yes** on PostgreSQL; `WITH TIES` — **yes** on PostgreSQL + SQL Server; `TABLESAMPLE` — **yes** on PostgreSQL + SQL Server | no | no | `DistinctOn`/`WithTies`/`FromOptions.TableSample`, `DistinctOn`/`SupportsWithTies`/`TableSample` |
| Temporal tables (`FOR SYSTEM_TIME`) | **yes** on SQL Server + MariaDB; `CONTAINED IN` — SQL Server only | no | no | `ForSystemTime`, `TemporalKind`/`TemporalClause`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| `UNION` / `UNION ALL` | **yes** | yes | yes | `QueryCommand`, `SqlBuilder` |
| `INTERSECT` / `EXCEPT` | **yes** (`ALL` on PostgreSQL, MariaDB and ClickHouse) | yes | yes | `UnionType`, `SupportsIntersectExceptAll` |
| CTE (`WITH`), recursive CTE | **yes** (PostgreSQL data-modifying CTEs) | yes | partial — no data-modifying CTEs | `QueryCommand.Cte`, `EntityBuilder`, `Builders/MutationCteQuery.cs`, `ISqlDialect.SupportsDataModifyingCtes` |
| Window functions (`OVER`, `ROW_NUMBER`, ...) | **yes** | yes | partial — no LINQ surface for `OVER (...)` (provider extensions/raw SQL only) | `CommonFunctions.row_number/rank/lag/nth_value/...`, `ClickHouseFunctions.lag_in_frame`/`lead_in_frame`, `WindowDefinition`/`EntityBuilder.Window`, `WindowFunctionTranslator`, dialects |
| `CASE WHEN` / ternary `?:` / `switch` | **yes** | yes | yes | `BaseExpressionVisitor.VisitConditional` |
| `COALESCE` (`??`) | **yes** | yes | yes | `BaseExpressionVisitor`, `MakeCoalesce` |
| `CAST` (numeric) | **yes** | yes | yes | `BaseExpressionVisitor` |
| `LIKE` / CLR `string` methods (`Contains`, `StartsWith`, `ToUpper`, `Substring`, `Trim`, `Remove`, `Insert`, `IndexOf`, `LastIndexOf`, `PadLeft`, `PadRight`, `new string(char, n)`, `Split`/`Join` on arrays) | **yes** (string `LastIndexOf` is not available on SQLite, which has no reversal; `Split` needs a scalar array while `Join` requires PostgreSQL arrays) | yes | yes | `BaseExpressionVisitor`, dialect string hooks |
| Native string / regexp functions (`split_part`, `strpos`, `left`/`right`, `lpad`/`rpad`, `repeat`, `reverse`, `initcap`, `translate`, `overlay`, `concat_ws`, `format`; PostgreSQL `regexp_like`/`regexp_replace`/`regexp_count`/`regexp_instr`/`regexp_split_to_array`) | **yes** on PostgreSQL (provider-only `SqlFunctions.Postgres`; no cross-provider string library — other providers use the portable CLR methods or a `[SqlFunction]`) | yes (provider `Sql.Ext`) | partial (`EF.Functions` provider extensions only) | `SqlFunctions.Postgres`, `ISqlDialect.SupportsExtendedScalarFunctions` |
| Math functions (`Math.*`) | **yes** | yes | yes | `BaseExpressionVisitor`, `MakeMathFunction` |
| Date/time functions (`DATEPART`, ...) | **yes** | yes | yes | `CommonFunctions`, `MakeDatePart`/`MakeDateAdd`/... |
| Full-text search | **yes** on SQL Server, PostgreSQL and MySQL/MariaDB (including native PostgreSQL `tsvector`/`tsquery` and SQL Server ranking) | yes (provider) | partial (`EF.Functions`) | `contains`/`freetext`, `SupportsFullText`/`MakeFullText`; `PostgresFunctions.to_tsvector/...`, `SupportsTextSearchFunctions`; `SqlServerFunctions.containstable/freetexttable` |
| Native JSON | **yes** on PostgreSQL | yes | yes | `SupportsJson`, `JsonSqlTranslator` |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | **yes** on SQL Server and MySQL/MariaDB | yes | yes | `SupportsTextJson`, `MakeTextJsonFunction`, `MakeIsJson` |
| String JSON (`JSONExtract*`, `JSONHas`, `JSONLength`, `JSONType`, `visitParamExtract*`, JSONPath `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`) | **yes** on ClickHouse | no | no | `SupportsJsonExtract`, `MakeJsonExtract`, `JsonExtractSqlTranslator` |
| Dictionary functions (`dictGet`, `dictGetOrDefault`, `dictHas`, `dictGetHierarchy`, `dictGetChildren`, `dictIsIn`) | **yes** on ClickHouse | no | no | `SupportsDictionaries`, `MakeDictionaryFunction`, `DictionarySqlTranslator` |
| Array functions (`cardinality`/`array_*`/`@>`/`&&` on PostgreSQL; `length`/`has`/`indexOf`/`hasAny`/`hasAll`/`arrayStringConcat`/`splitByChar`/`arraySort`/`range`/`arrayJoin` and the higher-order functions on ClickHouse) | **yes** on PostgreSQL and ClickHouse (including the `[LEFT] ARRAY JOIN` clause and the `Array(T)`/`Tuple(...)` row reader) | no | yes (`PostgresFunctions`, parameter arrays) | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`/`ArrayJoinClause`/`StringSplit`, `MakeArrayFunction`/`IArrayJoinRenderer.Render`/`IStringSplitRenderer.Render`, `ArraySqlTranslator`, `ArrayJoinKind`, `ClickHouseFunctions` |
| Row values / tuples (`ROW`/`(a, b)`, element access, row comparison) | **yes** on PostgreSQL and ClickHouse | yes (`Sql.Row`, emulated without native support) | partial | `ISqlDialect.Tuple`/`ITupleRenderer`, `TupleSqlTranslator.cs` |
| Conditional functions (`iif`, `choose`, `multi_if`) | **yes** | no | no | `CommonFunctions.iif`, `SqlServerFunctions.choose`, `ClickHouseFunctions.multi_if`, `Iif`/`IIifRenderer.Render`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render`, `BuiltinFunctionTranslator` |
| `FOR JSON` / `FOR XML` | **yes** on SQL Server | yes (provider) | partial | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | **partial** — SQL Server only | yes (provider) | no | `SqlServerFunctions.xml_value/xml_query/xml_exist/xml_nodes`, `XmlFunctions`/`IXmlFunctions.Render`, `IXmlNodesRow` |
| `GREATEST` / `LEAST` | **yes** (NULL handling is provider-specific) | partial | yes | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | **yes** (`array_agg` on PostgreSQL) | yes | yes | `string_agg`/`array_agg`, `SupportsStringAgg`/`SupportsArrayAgg` |
| User-defined scalar-valued functions | **yes** (`[SqlFunction]`) | yes (`Sql.Ext`/custom) | yes (`DbFunction`) | `UdfScalarTranslator` |
| Table-valued functions | **yes** (`[SqlTableFunction]`); built-ins gated | yes (`TableFunction`) | yes (TVF mapping) | `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Navigation properties (implicit joins) | **no** | yes | yes | explicit joins only; no relationship metadata |
| DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | **yes** | yes | partial — no `MERGE` | `InsertBuilder<TEntity>`, `InsertReturningBuilder<TEntity,TResult>`, `MergeBuilder<TEntity>`, `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>`, `DeleteBuilder<TEntity>`, `UpdateBuilder<TEntity>`, `MutationCteQuery<TResult>`, `ISqlDialect.SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`/`SupportsIdentityFunction`/`SupportsDataModifyingCtes`/`SupportsOnConflict`/`SupportsOnDuplicateKey`/`SupportsMerge`/`SupportsDelete` |
| Bulk insert / bulk copy | **yes** (native `COPY`/`SqlBulkCopy` or chunked `VALUES`, with the `BulkInsertOptions`/`BulkInsertOptionsBuilder` surface) | yes | partial — no native server-side bulk copy (third-party extensions only) | `Builders/BulkInsertBuilder.cs`, `Builders/BulkInsertOptions.cs`, `Builders/BulkInsertReturningBuilder.cs`, `Query/Mutations/BulkInsertCommand.cs`, `ISqlDialect.SupportsBulkCopy` |
| Materialize a query into a table (`ToTable`/`ToTempTable`, CTAS) | **yes** (temporary form on PostgreSQL/SQLite/MySQL/MariaDB only) | yes (provider) | no | `Builders/{CreateTableAsOptions,TempTableExtensions}.cs`, `Query/Mutations/CreateTableAsCommand.cs`, `ISqlDialect.SupportsCreateTableAsSelect`/`SupportsTemporaryCreateTableAsSelect`/`CreateTableAsSelectUsesSelectInto` |
| Transactions (own + enlisted) | **yes** (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB; ClickHouse and in-memory reject) | yes | yes | `DataContext/Roles/ITransactionManager.cs`, `DataContext/DbConnectionManager.cs`, `ISqlDialect.SupportsTransactions` |
| Raw SQL (whole query) | **yes** | yes | yes (`FromSql`) | `PrepareFromSql`/`WithSql` |
| Raw SQL as a composable source/subquery | **yes** | yes | yes | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |

The matrix was originally written before the section-5 workstreams landed; it has been updated in place on
2026-09-24 (complete DML, bulk insert with the options API, CTAS and transactions). For per-provider details
see `docs/providers/*.md`.
