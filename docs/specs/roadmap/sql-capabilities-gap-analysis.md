# SQL query-building capabilities: nextorm vs EF Core and linq2db

This document compares the SQL-query-building surface of nextorm with EF Core and linq2db, lists what
nextorm supports today, and enumerates the gaps. It is the reference for the implementation workstreams
tracked in [Implementation plan](#5-implementation-plan).

The analysis is based on the current `1.0.3-alpha` tree: `SqlBuilder`, `QueryCommand`,
`BaseExpressionVisitor`, `CorrelatedQueryExpressionVisitor`, the join builders, the `ISqlDialect`
contract and the provider dialects, plus the integration tests under `tests/nextorm.integration.tests`.

> **Status (updated 2026-09-19).** Sections 1–4 below started as the original gap analysis and were
> refreshed in place; the implementation status is:
>
> | # | Workstream | Status |
> |---|---|---|
> | 1 | Join types: `LEFT`/`RIGHT`/`FULL`/`CROSS` (+ fluent API, in-memory, dialects) | **Done** |
> | 2 | Join arity > 3 (4..8 supported via `Projection<T1..T8>`/`JoinedEntityBuilder<T1..T8>`) | **Done** |
> | 3 | `CASE WHEN` / ternary / `switch` | **Done** |
> | 4 | String, math and date scalar functions + `LIKE` | **Done** |
> | 5 | `IN` over a list/array (`@in`, `Contains`) | **Done** (+ optimized) |
> | 6 | Logical `!` and unary operators | **Done** |
> | 7 | `SELECT DISTINCT` | **Done** |
> | 8 | `INTERSECT` / `EXCEPT` (+`ALL` on PostgreSQL, MariaDB and ClickHouse) | **Done** (+ optimized) |
> | 9 | CTEs (`WITH`, recursive) | **Done** |
> | 10 | Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`) | **Done** |
> | 11 | User-defined scalar-valued functions (`[SqlFunction]`) | **Done** |
> | 12 | Table-valued functions (`[SqlTableFunction]`) | **Done**; the built-in `SqlFunctions.Sql` TVFs are gated by `ISqlDialect.SupportsTableFunction` |
> | 13 | Navigation properties / relationships | **Out of scope** |
> | 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | **Out of scope** |
> | 15 | `APPLY` / `LATERAL` (`CrossApply`/`OuterApply`) | **Partial** — SQL Server `CROSS/OUTER APPLY`, PostgreSQL/MySQL/MariaDB `LATERAL`; the applied source cannot be correlated yet (no public outer-reference API for a `FROM` subquery) |
> | 16 | Statement-level query hints (`Hint(...)`) | **Done on SQL Server** (`OPTION (...)`); other dialects reject hints with `NotSupportedException` |
> | 17 | Full-text search (`contains`/`freetext`) | **Done** on SQL Server, PostgreSQL and MySQL/MariaDB via `MakeFullText` |
> | 18 | JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | **Done on SQL Server and MySQL/MariaDB** (`SupportsTextJson`); native JSON documents on PostgreSQL (`SupportsJson`) |
> | 19 | `FOR JSON` / `FOR XML` | **Done on SQL Server** (`ForJson`/`ForXml`) |
> | 20 | `GREATEST` / `LEAST` | **Done** on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; NULL semantics differ per provider |
> | 21 | `STRING_AGG` / `ARRAY_AGG` (incl. `WITHIN GROUP`, `FILTER`) | **Done** — `string_agg` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `array_agg` on PostgreSQL (`MakeStringAgg`/`MakeArrayAgg`) |
> | 22 | `ROLLUP` / `CUBE` / `GROUPING SETS` | **Done** — `ROLLUP` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `CUBE`/`GROUPING SETS` on SQL Server, PostgreSQL, SQLite and ClickHouse (`GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`) |
> | 23 | Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | **Done** across SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; the accepted fields differ per provider and are validated by `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
> | 24 | Table hints (`with (nolock)`, ...) | **Done on SQL Server** (`WithTableHint`); other dialects throw |
> | 25 | Extended scalar + session/info functions (`make_interval`, `justify_*`, `to_*`, `timezone`, `gen_random_uuid`/`uuidv7`, `pg_typeof`; `current_user`/`session_user`/`current_schema`/`current_database`/`version`) | Extended scalar + `pg_typeof`: **Done on PostgreSQL** (`SupportsExtendedScalarFunctions`). Session/info: **Done** cross-provider — all five on PostgreSQL/SQL Server/MySQL/MariaDB, `current_user`/`current_database`/`version` on ClickHouse and `version` on SQLite (`SupportsSessionInfoFunctions` + per-name `SupportsSessionInfoFunction`). UUID generators: **Done** cross-provider — `gen_random_uuid`/`uuidv7` on PostgreSQL/MariaDB/ClickHouse, `gen_random_uuid` (`newid()`) also on SQL Server, gated off on MySQL (v1 only) and SQLite (`SupportsUuidGenerators` + per-name `SupportsUuidGenerator`) |
> | 26 | Correlated scalar subqueries (and correlated `EXISTS`/`IN`/`ANY`/`ALL` in `SELECT`/`WHERE`/`ORDER BY`) | **Done on SQL providers, one level**; nesting depth > 1 and the in-memory provider throw `NotSupportedException` (see `plan-correlated-subqueries.md`) |
>
> Test coverage after the work is **83.6% line** (CI threshold 75%); the full integration suite is
> 803 tests / 0 failed / 23 capability-based skips. Benchmarks and the performance optimizations that
> followed are documented in
> [Iteration 6 of `benchmark-report.md`](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/specs/performance/benchmark-report.md):
> prepared nextorm wins every new feature against compiled EF Core/linq2db/Dapper, and the warm-path
> losses on IN-list (7–8×), `INTERSECT`/`EXCEPT` (~8×) and recursive CTE (~7×) were eliminated.

---

## 1. Scope of the comparison

EF Core (relational) and linq2db are both mature LINQ-to-SQL providers. For the purpose of this document
the relevant "query-building" surface is:

* projection (`SELECT`);
* predicates (`WHERE`) and boolean/arithmetic operators;
* joins (`INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS`, `APPLY`/`LATERAL`);
* subqueries (derived table, scalar, correlated);
* grouping and aggregation (`GROUP BY`, `HAVING`, aggregates);
* sorting and paging (`ORDER BY`, `LIMIT`/`OFFSET`/`TOP`);
* set operations (`UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT`);
* scalar-valued and table-valued functions, and user-defined function mapping;
* JSON and full-text search;
* CTEs, window functions;
* DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`);
* navigation properties / relationship metadata;
* raw SQL.

---

## 2. Capability matrix

Legend: **yes** = first-class support; **partial** = supported with limits; **no** = not supported.

The managed SQL surface is split by portability: cross-provider helpers live on `SqlFunctions.Sql`, while the
provider-only functions live on a provider-specific surface — `SqlFunctions.Postgres` (native arrays, native
JSON, the extended scalar library, the PG-only aggregates and the `generate_series`/`unnest` table
functions), `SqlFunctions.SqlServer` (JSON-as-text on SQL Server and MySQL/MariaDB, plus SQL Server `string_split`/`openjson`) and `SqlFunctions.ClickHouse`
(`arg_min`/`arg_max` and the `-If` combinator). A call on another provider fails with a clear
`NotSupportedException`, and its capability flags are the same ones documented below.

| SQL construct | EF Core | linq2db | nextorm | Evidence in nextorm |
|---|---|---|---|---|
| INNER JOIN | yes | yes | **yes** | `SqlBuilder.MakeJoin` |
| LEFT JOIN | yes (`GroupJoin`+`DefaultIfEmpty`) | yes (`LeftJoin`) | **yes** | `JoinType.Left` |
| RIGHT JOIN | no (workaround) | yes | **yes** | `JoinType.Right`, `ISqlDialect.SupportsRightFullJoin` |
| FULL JOIN | no (workaround) | yes | **yes** on SQL Server, PostgreSQL, SQLite and ClickHouse (not MySQL/MariaDB) | `JoinType.Full`, `ISqlDialect.SupportsFullJoin` |
| CROSS JOIN | yes (`SelectMany`) | yes | **yes** | `JoinType.Cross` |
| APPLY / LATERAL | partial | yes | **partial** — non-correlated only | `JoinType.CrossApply/OuterApply`, `ISqlDialect.SupportsApply`/`MakeApply`; correlation not expressible |
| Join strictness (`ANY`/`ALL`/`ASOF`) and `GLOBAL` | no | no | **yes** on ClickHouse | `JoinStrictness`, `EntityBuilder.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin`/`MakeJoinKeyword`; `SEMI`/`ANTI`/`PASTE` are not implemented |
| Query hints | yes | yes (provider specific) | **partial** — SQL Server only | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Table hints | yes | yes | **partial** — SQL Server only | `EntityBuilder.WithTableHint`, `SupportsTableHints`/`MakeTableHints` |
| JOIN to a derived table (subquery) | yes | yes | **partial** — the subquery may be the *joined* side (`primary.Join(QueryCommand<T>)`); a derived query as the *primary* `FROM` source (`ctx.From(derivedQuery)`) followed by `.Join(...)` throws `QueryPreparationException: Select must return new anonymous type` | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| More than two joined tables | unlimited | unlimited | **yes — up to 8** | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| Subquery in `FROM` | yes | yes | **yes** | `SqlBuilder.MakeFrom`, `FromExpression` |
| Scalar subquery in `SELECT`/`WHERE`/`ORDER BY` | yes | yes | **yes** — correlated and non-correlated | `CommonTestSuite.CorrelatedQuery.cs`, `CorrelatedQueryTests.cs` |
| Correlated subquery | yes | yes | **yes (one level)** — scalar subqueries and `EXISTS`/`IN`/`ANY`/`ALL` in `SELECT`/`WHERE`/`ORDER BY`; deeper nesting and the in-memory provider throw `NotSupportedException` | `CorrelatedQueryExpressionVisitor.cs` |
| `IN` (subquery) | yes | yes | **yes** — plus the ClickHouse distributed `GLOBAL IN` (`global_in`, `SupportsGlobalPredicates`) | `SqlFunctions.@in`, `SqlFunctions.ClickHouse.global_in`, `BaseExpressionVisitor` |
| `IN` (list/array/`Contains`) | yes | yes | **yes** | `SqlFunctions.@in`, `Contains` |
| `EXISTS` / `ANY` / `ALL` | yes | yes | **yes** | `SqlFunctions.exists/any/all` |
| `WHERE` (and/or/not, comparisons) | yes | yes | **yes** | `WhereExpressionVisitor`, `VisitUnary` |
| `GROUP BY` | yes | yes | **yes** | `EntityBuilder.GroupBy`, `SqlBuilder` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` | yes | yes | **yes** — `ROLLUP` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `CUBE`/`GROUPING SETS` on SQL Server, PostgreSQL, SQLite and ClickHouse; `WITH TOTALS` on ClickHouse | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | no | no | **ClickHouse only** — at most `n` rows per distinct key | `LimitBy`, `SupportsLimitBy`/`MakeLimitBy` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | no | no | **ClickHouse only** for `FINAL`/`PREWHERE`/`SETTINGS`; the cross-provider `TABLESAMPLE` analog is exposed separately on PostgreSQL and SQL Server (see the `DISTINCT ON`/`WITH TIES`/`TABLESAMPLE` row) | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| Generated-row table functions (`numbers`/`numbers_mt`, `zeros`/`zeros_mt`) | yes (`generate_series`/`unnest`) | yes (`string_split`/`openjson`) | **ClickHouse** — generated sources; `numbers`' `UInt64` is cast to `Int64`, `zeros`' `UInt8` materialises directly | `ClickHouseFunctions.numbers`/`zeros`, `INumbersRow`/`IZerosRow`, `WrapTableFunction` |
| `HAVING` | yes | yes | **yes** | `EntityBuilder.Having` |
| Aggregates (`count`/`min`/`max`/`avg`/`sum`/`stdev`/`var`, distinct) | yes | yes | **yes** — `FILTER (WHERE ...)` on PostgreSQL and SQLite; boolean aggregates on PostgreSQL; bit and statistical aggregates on PostgreSQL and ClickHouse; `regr_*` on PostgreSQL; the arbitrary-value `any_agg` on MySQL and ClickHouse (MariaDB gated off — no `ANY_VALUE` until 13.2, MDEV-10426); `arg_min`/`arg_max`, the `uniq*` distinct-count family (the exact `uniqExact` overlaps the portable `count_distinct`/`count_big_distinct`, while SQL Server 2019+ `APPROX_COUNT_DISTINCT` is the approximate-distinct analog), the parameterised `quantile*`/`median`, the row-picking `any_last` and the `-If` combinators on ClickHouse (where `count`/`count_if` are cast to `toInt32`/`toInt64` because the native result is `UInt64`); the window percentiles `percentile_cont`/`percentile_disc` on SQL Server and MariaDB | `AdvancedAggregateTranslator.cs`, `WindowFunctionTranslator.cs`, `Supports*Aggregates`, `SupportsAnyValueAggregate`, `WrapsCountResult`, `SupportsPercentileWindow` |
| `SELECT DISTINCT` | yes | yes | **yes** | `EntityBuilder.IsDistinct` |
| `ORDER BY` (expression/ordinal, asc/desc, multiple) | yes | yes | **yes** | `EntityBuilder`, `SqlBuilder` |
| `LIMIT`/`OFFSET`/`TOP` | yes | yes | **yes** | dialect `MakePage`/`MakeTop` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` / `FOR UPDATE`/`FOR SHARE` | no | no | `DISTINCT ON` — **PostgreSQL only**; `WITH TIES` — **PostgreSQL + SQL Server**; `TABLESAMPLE` — **PostgreSQL** (`SYSTEM`/`BERNOULLI`) **+ SQL Server** (`SYSTEM`); row locking — **PostgreSQL + MySQL/MariaDB** (`FOR UPDATE`/`LOCK IN SHARE MODE`) | `DistinctOn`/`WithTies`/`TableSample`/`ForUpdate`/`ForShare`, `SupportsDistinctOn`/`SupportsWithTies`/`SupportsTableSample`/`SupportsLocking` |
| Temporal tables (`FOR SYSTEM_TIME`) | no | no | **SQL Server + MariaDB** — `AS OF`/`BETWEEN ... AND ...`/`FROM ... TO ...`/`ALL`; `CONTAINED IN` — SQL Server only | `ForSystemTime`, `TemporalKind`/`TemporalClause`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| `UNION` / `UNION ALL` | yes | yes | **yes** | `QueryCommand`, `SqlBuilder` |
| `INTERSECT` / `EXCEPT` | yes | yes | **yes** (with `ALL` on PostgreSQL, MariaDB and ClickHouse) | `UnionType`, `SupportsIntersectExceptAll` |
| CTE (`WITH`), recursive CTE | yes | yes | **yes** | `QueryCommand.Cte`, `EntityBuilder` |
| Window functions (`OVER`, `ROW_NUMBER`, ...) | yes | yes | **yes** — includes `percent_rank`/`cume_dist` (`SupportsPercentRankCumeDist`, ClickHouse included) and `nth_value` (`SupportsNthValue`; PostgreSQL/MySQL/MariaDB/SQLite/ClickHouse, rejected on SQL Server) | `CommonFunctions.row_number/rank/lag/nth_value/...`, `WindowFunctionTranslator`, dialects |
| `CASE WHEN` / ternary `?:` / `switch` | yes | yes | **yes** | `BaseExpressionVisitor.VisitConditional` |
| `COALESCE` (`??`) | yes | yes | **yes** | `BaseExpressionVisitor`, `MakeCoalesce` |
| `CAST` (numeric) | yes | yes | **yes** | `BaseExpressionVisitor` |
| `LIKE` / string methods (`Contains`, `StartsWith`, `ToUpper`, `Substring`, `Trim`, `Remove`, `Insert`, `IndexOf`, `LastIndexOf`, `PadLeft`, `PadRight`, `new string(char, n)`, `Split`/`Join` on arrays) | yes | yes | **yes** (string `LastIndexOf` is not available on SQLite, which has no reversal; `Split`/`Join` require PostgreSQL arrays) | `BaseExpressionVisitor`, dialect string hooks |
| Math functions (`Math.*`) | yes | yes | **yes** | `BaseExpressionVisitor`, `MakeMathFunction` |
| Date/time functions (`DATEPART`, ...) | yes | yes | **yes** | `CommonFunctions`, `MakeDatePart`/`MakeDateAdd`/... |
| Full-text search | partial (`EF.Functions`) | yes (provider) | **yes** on SQL Server, PostgreSQL, MySQL/MariaDB (boolean predicates); plus the native PostgreSQL `tsvector`/`tsquery` surface (`to_tsvector`/`to_tsquery`/`ts_rank`/`ts_headline`/`@@`) | `contains`/`freetext`, `SupportsFullText`/`MakeFullText`; `PostgresFunctions.to_tsvector/...`, `SupportsTextSearchFunctions` |
| Native JSON | yes | yes | **yes on PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | yes | **yes on SQL Server and MySQL/MariaDB** | `SupportsTextJson`, `MakeTextJsonFunction`, `MakeIsJson` |
| String JSON (`JSONExtract*`, `JSONHas`, `JSONLength`, `JSONType`, `visitParamExtract*`, JSONPath `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`) | no | no | **yes on ClickHouse** — `json_extract_string` is the same scalar-string extractor as the portable `json_value` (no separate surface needed), the typed `JSONExtractInt`/`Float`/`Bool`/`Raw` are ClickHouse-only, and the JSONPath scalars share the same gate | `SupportsJsonExtract`, `MakeJsonExtract`, `JsonExtractSqlTranslator` |
| Dictionary functions (`dictGet`, `dictGetOrDefault`, `dictHas`) | no | no | **yes on ClickHouse** | `SupportsDictionaries`, `MakeDictionaryFunction`, `DictionarySqlTranslator` |
| Array functions (`cardinality`/`array_*`/`@>`/`&&`; ClickHouse `length`, `has`, `indexOf`, `hasAny`/`hasAll`, `arrayStringConcat`, `splitByChar`, `arraySort`/`arrayReverse`/`arrayDistinct`, `arrayJoin`) | yes (`PostgresFunctions`, parameter arrays) | no | **yes on PostgreSQL and ClickHouse** — ClickHouse operates on `Array(T)` columns, `arrayJoin` expands one row per element and the `[LEFT] ARRAY JOIN` clause (+ element binding via `ArrayJoinElement`/`ArrayJoinProjection<TEntity, TElement>.Element`) is supported; higher-order functions and the array row reader are not implemented | `SupportsArrayFunctions`/`SupportsArrayJoin`/`SupportsArrayJoinClause`, `MakeArrayFunction`/`MakeArrayJoin`, `ArraySqlTranslator`, `ArrayJoinKind`, `ClickHouseFunctions` |
| Conditional functions (`iif`, `choose`) | no | no | **yes** — portable `iif` (native `iif` on SQL Server/SQLite, `if` on MySQL/MariaDB/ClickHouse, `case when` on PostgreSQL); `choose` is SQL Server-only (C# `?:` renders the portable `case`) | `CommonFunctions.iif`, `SqlServerFunctions.choose`, `SupportsIif`/`MakeIif`, `SupportsChoose`, `BuiltinFunctionTranslator` |
| `FOR JSON` / `FOR XML` | partial | yes (provider) | **yes on SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| `GREATEST` / `LEAST` | yes | partial | **yes** on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse (NULL handling is provider-specific: PostgreSQL, SQL Server 2022+ and ClickHouse 24.12+ ignore NULL arguments, while MySQL/MariaDB and SQLite return NULL when any argument is NULL) | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | yes | **yes** — `string_agg` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `array_agg` on PostgreSQL | `string_agg`/`array_agg`, `SupportsStringAgg`/`SupportsArrayAgg` |
| User-defined scalar-valued functions | yes (`DbFunction`) | yes (`Sql.Ext`/custom) | **yes** (`[SqlFunction]`) | `UdfScalarTranslator` |
| Table-valued functions | yes (TVF mapping) | yes (`TableFunction`) | **yes** (`[SqlTableFunction]`); built-ins gated | `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Navigation properties (implicit joins) | yes | yes | **no** | explicit joins only; no relationship metadata |
| DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | yes | **no** | read-only provider |
| Raw SQL (whole query) | yes (`FromSql`) | yes | **yes** | `PrepareFromSql`/`WithSql` |
| Raw SQL as a composable source/subquery | yes | yes | **no** | `WithSql` replaces the whole query |

The matrix was originally written before the section-5 workstreams landed; it has been updated in place on
2026-09-19. For per-provider details see `docs/providers/*.md`.

---

## 3. What nextorm does well

These are fully implemented and covered by SQL-generation or integration tests:

* **Projection** — anonymous types, DTOs, records, tuples, member init, primitive/scalar projections.
* **Predicates** — equality/inequality (including `IS NULL` / `IS NOT NULL` special-casing),
  relational comparisons, `and`/`or`/`not`, arithmetic, bitwise and shift operators (with bracketing).
* **`COALESCE`** and numeric **`CAST`**.
* **`GROUP BY` + `HAVING` + aggregates**, including `count`, `count_big`, `count_distinct`, `min`, `max`,
  `avg`, `sum`, `stdev`, `stdevp`, `var`, `varp`, the `_distinct` variants, plus the provider-specific
  statistical/bitwise/regression/`argMin`-`argMax` sets and `FILTER`/`-If` combinators.
* **`ORDER BY`** by expression and by projected column ordinal, ascending/descending, chainable.
* **Paging** — dialect-specific `TOP`, `LIMIT`/`OFFSET` and `OFFSET ... FETCH`, including the SQL Server
  requirement to inject an `ORDER BY`.
* **Set operations** — `UNION`/`UNION ALL` and `INTERSECT`/`EXCEPT` (with `ALL` on PostgreSQL, MariaDB and ClickHouse).
* **Provider-specific scalar/aggregate functions** gated by capability flags — for example ClickHouse
  `dateTrunc`, `addDays`/.../`toLastDayOfMonth`, `arrayStringConcat(groupArray(...))`,
  `groupBitAnd`/`groupBitOr`/`groupBitXor`, `corr`/`covarPop`/`covarSamp`, `argMin`/`argMax`, the
  `uniq`/`uniqExact` distinct counts (the exact `uniqExact` overlaps the portable
  `count_distinct`/`count_big_distinct`, and SQL Server 2019+ `APPROX_COUNT_DISTINCT` is the
  approximate-distinct analog), the parameterised `quantile(level)(value)`/`median` family and the
  last-row `anyLast`, plus the cross-provider arbitrary-value `any_agg` (`ANY_VALUE` on MySQL, `any` on
  ClickHouse; MariaDB and SQL Server gated off) and the window percentiles
  `percentile_cont`/`percentile_disc` on SQL Server/MariaDB.
* **Subqueries** in `FROM`, scalar subqueries in `SELECT`/`WHERE`/`ORDER BY` (correlated and
  non-correlated), and correlated `EXISTS`/`IN`/`ANY`/`ALL`.
* **Derived-table joins** (`Join(QueryCommand<T>)`).
* **Full-text search** (`contains`/`freetext`) on SQL Server, PostgreSQL and MySQL/MariaDB, expressed
  through a single `MakeFullText` dialect hook.
* **JSON** — native JSON documents on PostgreSQL, the standard `json_value`/`json_query`/`json_modify`
  and   `isjson` on SQL Server and MySQL/MariaDB, `FOR JSON`/`FOR XML` on SQL Server, and the string-JSON
  `JSONExtract*`/`JSONHas`/`JSONLength`/`JSONType`/`visitParamExtract*` family and the dictionary functions
  on ClickHouse.
* **Date arithmetic** — `date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts` plus the
  `DateTime.Add*` methods, each provider emitting its native form. Arbitrary parts are exposed as
  `SqlFunctions.Sql.extract(part, value)` (`quarter`/`week` ISO/`dow`/`isodow`, validated by
  `SupportsDatePart`) and the numeric `SqlFunctions.Sql.date_part("epoch", value)`.
* **Built-in table-valued functions** — `generate_series`/`unnest` (PostgreSQL),
  `string_split`/`openjson` (SQL Server) — with a `SupportsTableFunction` gate so an unsupported provider
  throws instead of emitting invalid SQL.
* **Raw SQL** for a whole query (`PrepareFromSql` / `WithSql`) with named parameters.

---

## 4. Remaining gaps, in order of significance

1. **Correlated subqueries deeper than one level.** One level of correlation now works for scalar
   subqueries and `EXISTS`/`IN`/`ANY`/`ALL` in `SELECT`/`WHERE`/`ORDER BY`, but a subquery nested
   inside another correlated subquery (and any correlated subquery on the in-memory provider) throws
   `NotSupportedException`. Supporting it requires threading the referenced-query/outer-reference
   scope chain through rendering (see the `plan-correlated-subqueries.md` follow-up). Aggregate
   terminals (`Count()`/`Sum(...)`) as a subquery projection are likewise rejected in favour of the
   `SqlFunctions.Sql` aggregate.
2. **`APPLY`/`LATERAL` sources cannot be correlated.** SQL Server `CROSS/OUTER APPLY` and
   PostgreSQL/MySQL/MariaDB `LATERAL` are emitted for non-correlated sources only; the outer-reference
   primitives introduced for correlated subqueries are a step towards it.
3. **Raw SQL is not composable.** `WithSql` replaces the whole query, so raw SQL cannot be used as a
   `FROM` source, joined, or further filtered; EF Core (`FromSql`) and linq2db both allow this.
4. **The pre-declared table-function set is small.** `SqlFunctions.Sql` ships only four built-ins, each gated by
   `ISqlDialect.SupportsTableFunction`: `generate_series`/`unnest` (PostgreSQL) and
   `string_split`/`openjson` (SQL Server). MySQL/MariaDB, SQLite and ClickHouse expose none of them, so
   there a user must declare their own `[SqlTableFunction]` wrapper (user wrappers are never gated), while
   EF Core and linq2db surface many more provider TVFs out of the box. Not mapped: `CONTAINSTABLE`/
   `FREETEXTTABLE` with ranking, `OPENJSON ... WITH` typed schemas, and MySQL `JSON_TABLE`.
5. **Full-text search has no ranking/score.** `contains`/`freetext` render boolean predicates; there is no
   `ts_rank`/`CONTAINSTABLE` score projection.
6. **Column identifiers are emitted unquoted.** Outside projection aliases and inner-query columns, nextorm
   writes the mapped column name verbatim (`select id from simple_entity`, even on PostgreSQL, which would
   also accept `"id"`). A physical name that collides with a keyword must therefore be pre-quoted in its
   `[Column]` mapping — as `SqlFunctions.IOpenJsonRow.Key` does for the T-SQL reserved word `key`. EF Core and
   linq2db escape identifiers per provider instead. Fixing this globally would change every generated
   statement and is deliberately deferred.
7. **Provider field/feature differences remain.** The accepted `date_add`/`date_trunc`/`date_diff` fields
   differ per provider (e.g. SQLite folds `millisecond`/`quarter`, SQL Server rejects
   `decade`/`century`/`millennium` for `date_trunc`), `CUBE`/`GROUPING SETS` and `FULL JOIN` are missing
   on MySQL/MariaDB, `GREATEST`/`LEAST` has provider-specific NULL semantics, and several features
   (`FOR JSON`/`FOR XML`, table hints, query hints) exist on a subset of providers. These are documented
   in `docs/providers/*.md` rather than unified.
 8. **No `PIVOT`/`UNPIVOT`, temporal tables or XML-data-type methods** (`.value`/`.query`/`.nodes`/`.exist`).
 9. **No DML and no navigation properties / relationship metadata** — by design for a read-only,
    no-change-tracking mapper, but still a functional gap versus both references.
10. **A derived query as the primary `FROM` source cannot be joined.** `ctx.From(derivedQuery).Join(...)`
    fails during preparation with `QueryPreparationException: Select must return new anonymous type`
    (`QueryCommand.QueryPreparer.cs:373`): the wrapped source has no projection/entity metadata, so the
    joined columns cannot be resolved. The derived query does work as the source of
    `Where`/`OrderBy`/`GroupBy`/`Select`, and a subquery *as the joined side* works
    (`primary.Join(QueryCommand<T>)`). Workarounds: fold the join into the query that produces the
    derived table, or use the CTE API `With(name, query).From(name)`. Tracked from the demo-DB run
    (`docs/specs/demodb/postgres-aviasales.md`, `mssql-adventureworks.md`).

---

## 5. Implementation plan

This is the original per-workstream plan, kept as a status ledger. Workstreams that touch the same files
(`SqlBuilder.cs`, `BaseExpressionVisitor.cs`, `ISqlDialect`/`SqlDialectBase`, `EntityBuilder.cs`) must not be
developed in parallel on the same working tree.

| # | Workstream | Status | Primary files | Verification |
|---|---|---|---|---|
| 1 | Join types: `LEFT`/`RIGHT`/`FULL`/`CROSS` (+ fluent API, in-memory, dialects) | **Done** | `SqlBuilder.cs`, `JoinExpression.cs`, `EntityBuilder.cs`, `JoinCommandBuilder.cs`, `Projection.cs`, `InMemoryDataContext.cs`, dialects | `CommonTestSuite.Join.cs`, `SqlGenerationTests.cs` |
| 2 | Join arity > 3 (4..8) | **Done** | `Projection.cs`, `JoinCommandBuilder.cs`, `EntityBuilder.cs`, `SqlBuilder.cs` | join + SQL-generation tests |
| 3 | `CASE WHEN` / ternary / `switch` | **Done** | `BaseExpressionVisitor.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | `CommonTestSuite.Conditional.cs`, SQL-generation tests |
| 4 | String, math and date scalar functions + `LIKE` | **Done** | `BaseExpressionVisitor.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | `CommonTestSuite.Functions.cs`, SQL-generation tests |
| 5 | `IN` over a list/array | **Done** | `SqlFunctions.cs`, `BaseExpressionVisitor.cs`, dialects | `CommonTestSuite.In.cs`, SQL-generation tests |
| 6 | Logical `!` and unary operators | **Done** | `BaseExpressionVisitor.cs`, `WhereExpressionVisitor.cs` | `CommonTestSuite.Binary.cs`/`CommonTestSuite.Unary.cs`, SQL-generation tests |
| 7 | `SELECT DISTINCT` | **Done** | `QueryCommand.cs`, `SqlBuilder.cs`, `EntityBuilder.cs` | `CommonTestSuite.Distinct.cs` |
| 8 | `INTERSECT` / `EXCEPT` | **Done** | `QueryCommand.cs`, `UnionType.cs`, `SqlBuilder.cs` | `CommonTestSuite.SetOperations.cs` |
| 9 | CTEs (`WITH`, recursive) | **Done** | `QueryCommand.cs`, `SqlBuilder.cs`, builder API | `CommonTestSuite.Cte.cs` |
| 10 | Window functions (`OVER`, ranking, framed) | **Done** | `SqlFunctions.cs`, `BaseExpressionVisitor.cs`, dialects | `CommonTestSuite.Window.cs`, SQL-generation tests |
| 11 | User-defined scalar-valued functions | **Done** | `SqlFunctions.cs`, `ISqlDialect.cs`, `BaseExpressionVisitor.cs` | `CommonTestSuite.Udf.cs` |
| 12 | Table-valued functions | **Done** (+ gated built-ins) | builder + `ISqlDialect.cs`, `SqlBuilder.cs` | `CommonTestSuite.Tvf.cs`, SQL-generation tests |
| 13 | Navigation properties / relationships | **Out of scope** | metadata (`Meta/`), `EntityBuilder.cs`, `SqlBuilder.cs` | — |
| 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | **Out of scope** | new subsystem + provider `DbCommand` layer | — |
| 15 | `APPLY` / `LATERAL` | **Partial** — non-correlated only | `JoinExpression.cs`, `SqlBuilder.cs`, dialects | SQL-generation tests |
| 16 | Statement-level query hints | **Done on SQL Server** | `QueryCommand.TResult.cs`, `SqlBuilder.cs`, SQL Server dialect | SQL-generation tests |
| 17 | Full-text search (`contains`/`freetext`) | **Done** | `BuiltinFunctionTranslator.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | SQL-generation tests |
| 18 | JSON scalar functions and `isjson` | **Done on SQL Server** | `TextJsonSqlTranslator.cs`, `ISqlDialect.cs`, SQL Server dialect | SQL-generation tests |
| 19 | `FOR JSON` / `FOR XML` | **Done on SQL Server** | `ForJson.cs`, `ForXml.cs`, `QueryCommand.TResult.cs`, `SqlBuilder.cs`, SQL Server dialect | SQL-generation tests |
| 20 | `GREATEST` / `LEAST` | **Done** | `BuiltinFunctionTranslator.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | SQL-generation tests |
| 21 | `STRING_AGG` / `ARRAY_AGG` | **Done** | `SqlFunctions.cs`, `BuiltinFunctionTranslator.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | SQL-generation tests (+ ClickHouse integration) |
| 22 | `ROLLUP` / `CUBE` / `GROUPING SETS` | **Done** | `EntityBuilder.cs`, `QueryCommand.cs`, `SqlBuilder.cs`, dialects | SQL-generation tests |
| 23 | Date arithmetic parity (MySQL/MariaDB, SQLite) | **Done** | `BuiltinFunctionTranslator.cs`, dialects | SQL-generation tests + provider integration tests |
| 24 | Table hints | **Done on SQL Server** | `EntityBuilder.cs`, `SqlBuilder.cs`, SQL Server dialect | SQL-generation tests |
| 25 | PostgreSQL extended scalar functions | **Done** | `SqlFunctions.cs`, `ExtendedScalarFunctionTranslator.cs`, Postgres dialect | SQL-generation tests |

Workstream 17–25 extended provider parity and are tracked in detail in
[`todo_mssql.md`](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/specs/roadmap/todo_mssql.md).

Future workstreams (not scheduled): joins over a derived query as the primary `FROM` source
(`ctx.From(derivedQuery).Join(...)`), nested (depth > 1) correlation and correlated `APPLY`, composable
raw SQL, `CONTAINSTABLE`/`FREETEXTTABLE` with ranking, `OPENJSON ... WITH` typed schemas, `JSON_TABLE`,
`PIVOT`/`UNPIVOT`, temporal tables, XML-data-type methods, DML, navigation properties.

### Cross-cutting requirements

* Preserve the public API compatibility rules in the `api-design` skill: extend-only, no breaking changes
  to existing signatures.
* Every workstream adds tests before it is considered done. SQL-generation tests
  (`tests/nextorm.*.tests/SqlGenerationTests.cs`) do not require a database; integration tests run against
  SQLite locally and against PostgreSQL/SQL Server/MySQL/ClickHouse via Testcontainers.
* Keep the existing performance characteristics in mind: new SQL must be built with the same pooled
  `StringBuilder` pattern and must not allocate on the hot path more than necessary.
* Line endings are CRLF (`AGENTS.md`); normalize new and edited files with
  `perl -pi -e 's/\r?\n/\r\n/g' <file>`.

---

## 6. Verifying a workstream

```bash
# Build the solution.
dotnet build nextorm.sln

# Provider SQL-generation tests (no database required). Run with `dotnet test`
# (VSTest filter) or `dotnet run --project ... -- -class ...`:
dotnet test tests/nextorm.sqlite.tests -c Release
dotnet test tests/nextorm.sqlserver.tests -c Release
dotnet test tests/nextorm.postgres.tests -c Release
dotnet test tests/nextorm.mysql.tests -c Release
dotnet test tests/nextorm.mariadb.tests -c Release
dotnet test tests/nextorm.clickhouse.tests -c Release

# Integration tests (SQLite runs locally). Testcontainers is pointed at the
# Podman socket; Ryuk works as-is, no extra flags:
DOCKER_HOST="unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock" \
  dotnet run --project tests/nextorm.integration.tests -c Release
```

Without `DOCKER_HOST` every non-SQLite provider suite is skipped by `ProviderTestSuite`'s
`Assert.SkipUnless(Provider.IsAvailable, ...)`, so a "green" run proves nothing about the database
providers. External servers can be used instead via `NEXTORM_POSTGRES_CONNECTION`,
`NEXTORM_SQLSERVER_CONNECTION`, `NEXTORM_MYSQL_CONNECTION` and `NEXTORM_CLICKHOUSE_CONNECTION`.

---

## See also

- [nextorm vs linq2db: functionality comparison](linq2db-comparison.md) — a focused side-by-side of the
  two libraries, including the `APPLY`/`LATERAL` and query-hint status.
- [`todo_mssql.md`](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/specs/roadmap/todo_mssql.md) — the SQL Server
  feature-parity backlog and the provider-consistency audit, including the complex items deliberately
  deferred (`PIVOT`/`UNPIVOT`, temporal tables, correlated `APPLY`, composable raw SQL).
