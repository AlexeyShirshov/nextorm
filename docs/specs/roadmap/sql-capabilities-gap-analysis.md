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
> | 10 | Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`, named windows, `GROUPS`/`EXCLUDE`) | **Done** |
> | 11 | User-defined scalar-valued functions (`[SqlFunction]`) | **Done** |
> | 12 | Table-valued functions (`[SqlTableFunction]`) | **Done**; the built-in `SqlFunctions.Sql` TVFs are gated by `ISqlDialect.SupportsTableFunction` |
> | 13 | Navigation properties / relationships | **Out of scope** |
> | 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | **Out of scope** |
> | 15 | `APPLY` / `LATERAL` (`CrossApply`/`OuterApply`) | **Done** — SQL Server `CROSS/OUTER APPLY`, PostgreSQL/MySQL/MariaDB `LATERAL`, including a **correlated** applied source (a lambda over the left-hand row); gated by `ISqlDialect.SupportsApply` (SQLite/ClickHouse reject); the in-memory provider does not support it |
> | 16 | Statement-level query hints (`Hint(...)`) | **Done on SQL Server** (`OPTION (...)`); other dialects reject hints with `NotSupportedException` |
> | 17 | Full-text search (`contains`/`freetext`) | **Done** on SQL Server, PostgreSQL and MySQL/MariaDB via `MakeFullText` |
> | 18 | JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | **Done on SQL Server and MySQL/MariaDB** (`SupportsTextJson`); native JSON documents on PostgreSQL (`SupportsJson`) |
> | 19 | `FOR JSON` / `FOR XML` | **Done on SQL Server** (`ForJson`/`ForXml`) |
> | 20 | `GREATEST` / `LEAST` | **Done** on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; NULL semantics differ per provider |
> | 21 | `STRING_AGG` / `ARRAY_AGG` (incl. `WITHIN GROUP`, `FILTER`) | **Done** — `string_agg` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `array_agg` on PostgreSQL (`MakeStringAgg`/`MakeArrayAgg`) |
> | 22 | `ROLLUP` / `CUBE` / `GROUPING SETS` | **Done** — `ROLLUP` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `CUBE`/`GROUPING SETS` on SQL Server, PostgreSQL, SQLite and ClickHouse (`GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`) |
> | 23 | Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | **Done** across SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; the accepted fields differ per provider and are validated by `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField`. The ClickHouse-native conversion/part surface (`toDate`/`toDateTime`/`toDate32`, `toYear`/..., `toStartOf*`, `toMonday`, `toYYYYMM`/`toYYYYMMDD`, `toUnixTimestamp`) is exposed through `SqlFunctions.ClickHouse` under `DateConversion` |
> | 24 | Table hints (`with (nolock)`, ...) | **Done on SQL Server** (`WithTableHint`); other dialects throw |
> | 25 | Extended scalar + session/info functions (`make_interval`, `justify_*`, `to_*`, `timezone`, `gen_random_uuid`/`uuidv7`, `pg_typeof`; `current_user`/`session_user`/`current_schema`/`current_database`/`version`) | Extended scalar + `pg_typeof`: **Done on PostgreSQL** (`SupportsExtendedScalarFunctions`). Session/info: **Done** cross-provider — all five on PostgreSQL/SQL Server/MySQL/MariaDB, `current_user`/`current_database`/`version` on ClickHouse and `version` on SQLite (`SessionInfoFunctions` + per-name `SessionInfoFunctions`). UUID generators: **Done** cross-provider — `gen_random_uuid`/`uuidv7` on PostgreSQL/MariaDB/ClickHouse, `gen_random_uuid` (`newid()`) also on SQL Server, gated off on MySQL (v1 only) and SQLite (`UuidGenerators` + per-name `UuidGenerators`) |
> | 26 | Correlated scalar subqueries (and correlated `EXISTS`/`IN`/`ANY`/`ALL` in `SELECT`/`WHERE`/`ORDER BY`/`HAVING`) | **Done on SQL providers** (any nesting depth; join-projection outer references included); the in-memory provider still throws `NotSupportedException` (`todo_correlated_inmemory.md`) |
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

The full nextorm vs EF Core vs linq2db matrix has been extracted into
[`comparison/capability-matrix.md`](../comparison/capability-matrix.md).

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
  approximate-distinct analog), the parameterised `quantile(level)(value)`/`median` family, the
  sequence/funnel `windowFunnel`/`sequenceMatch`/`retention` and the
  last-row `anyLast`, the multi-branch `multiIf`, the frame-respecting `lagInFrame`/`leadInFrame`, plus the
  cross-provider arbitrary-value `any_agg` (`ANY_VALUE` on MySQL, `any` on
  ClickHouse; MariaDB and SQL Server gated off) and the window percentiles
  `percentile_cont`/`percentile_disc` on SQL Server/MariaDB.
* **Window model extensions** — named windows (`WINDOW w AS (...)` referenced by `OVER w`) on
  PostgreSQL, MySQL, MariaDB, ClickHouse and SQLite; the `GROUPS` frame unit on PostgreSQL, ClickHouse
  and SQLite; frame `EXCLUDE` on PostgreSQL and SQLite. Each piece is gated individually
  (`SupportsNamedWindows` / `SupportsWindowFrameGroups` / `SupportsWindowFrameExclusion`).
* **Subqueries** in `FROM`, scalar subqueries in `SELECT`/`WHERE`/`ORDER BY`/`HAVING` (correlated and
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
* **Crypto hashes** — `SqlFunctions.Postgres.md5`, `digest(data, type)` (requires the `pgcrypto`
  extension) and `sha256(bytes)`, gated by `SupportsCryptoFunctions` (PostgreSQL only; SQL
  Server/MySQL/ClickHouse expose native SHA-256 under different names/return types, so a
  cross-provider hash surface is a separate follow-up).
* **Built-in table-valued functions** — `generate_series`/`unnest` plus the PostgreSQL set-returning
  functions `regexp_matches`, `regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`,
  `jsonb_object_keys`, `jsonb_path_query` and `ts_stat`; `string_split`/`openjson` (SQL Server) — with a
  `SupportsTableFunction` gate so an unsupported provider throws instead of emitting invalid SQL.
* **Raw SQL** for a whole query (`PrepareFromSql` / `WithSql`) with named parameters.

---

## 4. Remaining gaps, in order of significance

Ordered by impact on real query authoring. Per-feature details and owners live in the per-feature
`docs/specs/roadmap/todo_*.md`; the full matrix is in
[`comparison/capability-matrix.md`](../comparison/capability-matrix.md).

1. **Correlated subqueries deeper than one level.** Correlation works at any nesting depth for scalar
   subqueries, aggregate terminals and `EXISTS`/`IN`/`ANY`/`ALL` in `SELECT`/`WHERE`/`ORDER BY`/`HAVING`,
   including references to a column of a join-projection item (`p.Item1.Id`); the outer-reference and
   referenced-query registries are flattened onto the root command so each marker resolves in the scope
   that declared it. The only remaining limit is the in-memory provider (no per-row outer-row binding).
   Todo: [`todo_correlated_inmemory.md`](todo_correlated_inmemory.md) (in-memory).
2. **Raw SQL is composable.** `WithSql` still replaces a whole query, and a raw fragment can now also be
   used as a `FROM` source and joined/filtered further: [`FromSql`](../../guide/14-raw-sql.md#compositing-raw-sql-as-a-from-source)
   renders it as a derived table (`FROM (&lt;sql&gt;) AS alias`) with named parameters, gated by
   [`SupportsRawSqlSource`](xref:NextORM.Core.ISqlDialect.SupportsRawSqlSource). This matches EF Core
   (`FromSql`) and linq2db.
   Shipped: [Raw SQL](../../guide/14-raw-sql.md).
3. **XML `.nodes` rowset method — shipped.** The XML data-type methods (`.value`/`.query`/`.exist` and
   the `.nodes` rowset) ship on SQL Server: `SqlFunctions.SqlServer.xml_value`/`xml_query`/`xml_exist`
   and the `xml_nodes` rowset, which is used as a correlated `CrossApply`/`OuterApply` source and
   renders `<xml>.nodes('xpath') as [alias]([value])` (the unfolded `IXmlNodesRow.Value` is projected
   with the scalar methods). Gated by `IXmlFunctions.Supports("nodes")` (SQL Server only). The native
   `PIVOT`/`UNPIVOT` source construct ships via `EntityBuilder.Pivot`/`Unpivot` over a plain
   table/entity, a table-valued function or a derived query (the pivot input can carry joins and
   computed aggregate/FOR columns).
   Shipped: [SQL Server-specific SQL](../../guide/provider-specific/sqlserver.md#xml-data-type-methods).
4. **ClickHouse array/Tuple row reader — shipped.** `Array(T)` (including nested `Array(Array(T))`)
   and `Tuple(...)` columns and expressions materialise as a CLR `T[]`/`System.Tuple<...>` (arity
   1–7), and the array-returning aggregates `groupArray`/`groupUniqArray` are exposed as
   [`ClickHouseFunctions.group_array`](xref:NextORM.Core.ClickHouseFunctions.group_array)/`group_uniq_array`,
   the multi-level quantile aggregate `quantiles` (as `double[]`) and the most-frequent
   `topK`/`topKWeighted` aggregates (as `T[]`) are exposed as
   [`ClickHouseFunctions.quantiles`](xref:NextORM.Core.ClickHouseFunctions.quantiles)/`top_k`/`top_k_weighted`.
   Native `UInt64` rows (`hits_v1.UserID`, any `UInt64` column and `ulong`/`ulong?` projection) now
   materialise through the row reader without a SQL cast; the aggregate/function results that declare a
   signed CLR return type (`count`, `uniq*`, `length`, `index_of`, `json_length`) keep their normalising
   cast.
   Still open on this item: the array-returning
   `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`, the `tuple`/`tupleElement`/`untuple`
   scalars and `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`.
   Shipped: [ClickHouse provider](../../providers/clickhouse.md),
   [Provider-specific SQL](../../guide/provider-specific/clickhouse.md#aggregates),
   [Grouping and aggregates](../../guide/04-grouping-and-aggregates.md),
   [Scalar functions](../../guide/11-scalar-functions.md#arrays-clickhouse).
   Todo: [`todo_clickhouse_arrays.md`](todo_clickhouse_arrays.md).
5. **ClickHouse higher-order array functions — shipped.** `arrayMap`/`arrayFilter`/`arrayExists`/
   `arrayAll`/`arrayCount`/`arrayFirst*`/`arrayLast*` translate an inline lambda argument
   (`ClickHouseFunctions.array_map`/`array_filter`/`array_exists`/`array_all`/`array_count`/
   `array_first`/`array_first_index`/`array_last`/`array_last_index`) under
   [`SupportsHigherOrderArrayFunctions`](xref:NextORM.Core.ISqlDialect.SupportsHigherOrderArrayFunctions);
   other providers throw `NotSupportedException`.
   Shipped: [Scalar functions](../../guide/11-scalar-functions.md#arrays-clickhouse),
   [Provider-specific SQL](../../guide/provider-specific/clickhouse.md).
6. **ClickHouse `-State`/`-Merge` combinators and `runningAccumulate` need an
   `AggregateFunction(...)` state type**, which nextorm does not model.
   Todo: [`todo_clickhouse_aggregate_function_state.md`](todo_clickhouse_aggregate_function_state.md).
7. **ClickHouse native `JSON` type — shipped.** The JSONPath scalars `JSON_VALUE`/`JSON_QUERY`/
   `JSON_EXISTS` and the native-JSON functions `JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString` are
   mapped on `ClickHouseFunctions` under `SupportsJsonExtract` (other providers throw
   `NotSupportedException`). A native `JSON` *column* is not mapped to a CLR type yet (the driver reads it
   as `System.Text.Json.Nodes.JsonObject`); see [Limitations](../../advanced/limitations.md).
   Shipped: [ClickHouse-specific SQL](../../guide/provider-specific/clickhouse.md#json-dictionaries-and-array-functions),
   [JSON support](../../guide/18-json.md).
8. **ClickHouse join `SEMI`/`ANTI`/`PASTE` is not implemented.** `SEMI`/`ANTI` change the result column
    set (left table only, incompatible with `Projection<T1,T2>`) and `PASTE JOIN` has no `ON`, so a
    result shape must be chosen first.
    Todo: [`todo_clickhouse_join_strictness.md`](todo_clickhouse_join_strictness.md).
9. **ClickHouse scalar-over-array predicates — shipped.** `startsWith`/`endsWith` over `Array(T)` and
    the contiguous-subsequence `hasSubstr` are exposed as
    [`ClickHouseFunctions.starts_with`](xref:NextORM.Core.ClickHouseFunctions.starts_with)/`ends_with`/`has_substr`
    under `SupportsArrayFunctions`; `length`/`position` over arrays map to `length`/`indexOf`
    ([`ClickHouseFunctions.length`](xref:NextORM.Core.ClickHouseFunctions.length)/`index_of`). Other
    providers throw `NotSupportedException`.
    Shipped: [Scalar functions](../../guide/11-scalar-functions.md#arrays-clickhouse),
    [Provider-specific SQL](../../guide/provider-specific/clickhouse.md).
10. **ClickHouse columns have no by-name access without an entity property.** `IHit` describes only 6
    `hits_v1` columns; the rest are reachable only through `WithSql`.
    Todo: [`todo_clickhouse_columns_by_name.md`](todo_clickhouse_columns_by_name.md).
11. **Dynamic-schema and server-scoped table sources.** The ClickHouse server/cluster table functions
    `url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas` are shipped on
    `SqlFunctions.ClickHouse.*` with a generic caller-declared `TRow` row interface; the dynamic-schema
    `format`/`merge`/`input` remain on this item. Still open: ClickHouse `values()` (dynamic schema) and
    PostgreSQL `jsonb_to_record(set)`.
    Todo: [`todo_dynamic_result_schema.md`](todo_dynamic_result_schema.md).
    Shipped: [Table-valued functions](../../guide/13-table-valued-functions.md#built-in-table-functions).
12. **Full-text ranking/score is implemented.** `contains`/`freetext` render the boolean predicates, and
    ranking is available: PostgreSQL `ts_rank`/`ts_rank_cd`/`ts_headline` and the SQL Server
    `containstable`/`freetexttable` table functions with `KEY`/`RANK`.
    Shipped: [Table-valued functions](../../guide/13-table-valued-functions.md#built-in-table-functions).
13. **The pre-declared table-function set is expanded.** `SqlFunctions.Sql` ships the built-ins, each gated
    by `ISqlDialect.SupportsTableFunction`: `generate_series`, `unnest`, `regexp_matches`,
    `regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`, `jsonb_object_keys`,
    `jsonb_path_query` and `ts_stat` (PostgreSQL), `string_split`/`openjson` and now
    `containstable`/`freetexttable` with `KEY`/`RANK` (SQL Server) and
    `numbers`/`numbers_mt`/`zeros`/`zeros_mt`/`generateRandom` and the server/cluster functions
`url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas` (ClickHouse). MySQL/MariaDB and SQLite
    still expose no built-ins, so there a user declares their own `[SqlTableFunction]` wrapper (user
    wrappers are never gated); MySQL `JSON_TABLE` is expressible through the new
    `SqlTableFunctionAttribute.CallClause` in-call schema, and `OPENJSON ... WITH` through `WithClause`.
    The remaining dynamic-schema `jsonb_to_record`/`json_populate_record` is tracked in item 11.
    Shipped: [Table-valued functions](../../guide/13-table-valued-functions.md).
14. **Column identifiers are emitted unquoted by default.** Outside projection aliases and inner-query
    columns, nextorm writes the mapped column name verbatim (`select id from simple_entity`). Identifier
    quoting is now available as an opt-in: `DataContextBuilder.UseQuotedIdentifiers()` sets a
    context-wide default and `WithQuotedIdentifiers()` overrides it per command
    (`docs/getting-started/03-entities-and-metadata.md`, "Quoted identifiers"). A physical name that
    collides with a keyword then needs no manual quoting. The default is unchanged (verbatim), so
    existing SQL is unaffected. Auto-derived names can likewise be translated to the database's
    spelling with an opt-in naming convention: `DataContextBuilder.UseNamingConvention(...)` sets a
    context-wide default and `WithNamingConvention()` overrides it per command; the built-in
    `SnakeCaseNamingConvention` maps `SimpleEntity` to `simple_entity` and `FirstName` to
    `first_name`, while names declared with `[SqlTable]`/`[Column]` or a fluent mapping are never
    translated (`docs/getting-started/03-entities-and-metadata.md`, "Naming conventions").
15. **Provider field/feature differences are decided and documented.** The accepted
    `date_add`/`date_trunc`/`date_diff` fields differ per provider (e.g. SQLite folds
    `millisecond`/`quarter`, SQL Server rejects `decade`/`century`/`millennium` for `date_trunc`),
    `CUBE`/`GROUPING SETS` and `FULL JOIN` are missing on MySQL/MariaDB, `GREATEST`/`LEAST` has
    provider-specific NULL semantics, and several features (`FOR JSON`/`FOR XML`) exist on a subset of
    providers. Each divergence now carries an explicit **unify / gate / document** decision in
    [Provider differences: unification decisions](../../providers/overview.md#provider-differences-unification-decisions):
    date fields and `FULL JOIN`/`CUBE`/`GROUPING SETS` are gated per field/feature (no polyfill: the
    rewrites change row shape/semantics and defeat the planner), `GREATEST`/`LEAST` NULL behaviour is a
    documented difference, and date/number formatting stays provider-specific `[SqlFunction]` UDFs
    because the `.NET` (`FORMAT`), PostgreSQL (`to_char`) and `%`
    (`DATE_FORMAT`/`strftime`/`formatDateTime`) template languages are incompatible — a single portable
    `template` argument cannot exist.
    Shipped: [Provider overview](../../providers/overview.md).
16. **Table hints only on SQL Server.** Statement-level hints (`Hint(...)`) are now wired on SQL Server
    (`OPTION (...)`), PostgreSQL and MySQL/MariaDB (inline `/*+ ... */`, read by the optional
    `pg_hint_plan` extension on PostgreSQL and as native optimizer hints on MySQL/MariaDB); SQLite and
    ClickHouse have no statement-hint syntax and stay gated. Table hints (`WithTableHint`) remain
    SQL Server-only, since the MySQL/MariaDB and SQLite index hints (`USE INDEX`/`INDEXED BY`) change
    the plan but not the locking semantics of `WITH (NOLOCK)`.
    Shipped: [Query hints](../../guide/17-query-hints.md).
17. **No DML and no navigation properties / relationship metadata** — by design for a read-only,
    no-change-tracking mapper, but still a functional gap versus both references.
    Todo: out of scope — [`limitations.md`](../../advanced/limitations.md); DML tracked separately in
    [`todo_insert.md`](todo_insert.md)/[`todo_update.md`](todo_update.md)/
    [`todo_delete.md`](todo_delete.md)/[`todo_merge.md`](todo_merge.md).
18. **Server/engine limits** (not fixable in nextorm): SQL Server has no `INTERSECT ALL`/`EXCEPT ALL`
    (the dialect correctly throws `NotSupportedException`); the ClickHouse `Memory` engine does not
    support `FINAL`/`PREWHERE`/`SAMPLE` (an integration-test limitation, not missing functionality).
    Todo: [`limitations.md`](../../advanced/limitations.md) (out of scope: engine/server cannot).
19. **Warm-path plan-build cost (performance) — closed by decision.** On the fast (tmpfs) full run the
    prepared path wins every measured class; the non-prepared (warm) path stays ~1.1–1.6× behind Dapper on
    `CTE`, recursive `CTE`, `Join4` and `IN`-list. Iterations 6 and 8 closed the `IN`-list refresh cost and
    parts of `INTERSECT`/`EXCEPT` and recursive `CTE`; a fresh iteration-9 measurement confirms the remaining
    gap is the inherent per-call cost of building and hashing a fresh expression tree (≈3–9 µs per query),
    and `QueryPlanEqualityComparer` already hashes by sub-hashes, so there is no safe local lever. **Why it
    is closed:** the fresh-fluent arm is compared against Dapper's constant SQL, and closing the gap requires
    the structural fluent/`Prepare()` parity rework (M12 #3) that touches cache-key semantics (M9). The
    sanctioned fast path is [`Prepare()`](../../guide/15-query-reuse.md) (faster than Dapper on every class);
    the implicit plan cache stays as the safe per-thread default. See `performance-findings.md` M12
    (Решение).
    Closed: [`performance-findings.md` M12](../performance/performance-findings.md).

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
| 10 | Window functions (`OVER`, ranking, framed, named windows/`GROUPS`/`EXCLUDE`) | **Done** | `SqlFunctions.cs`, `WindowDefinition.cs`, `EntityBuilder.cs`, `BaseExpressionVisitor.cs`, dialects | `CommonTestSuite.Window.cs`, SQL-generation tests |
| 11 | User-defined scalar-valued functions | **Done** | `SqlFunctions.cs`, `ISqlDialect.cs`, `BaseExpressionVisitor.cs` | `CommonTestSuite.Udf.cs` |
| 12 | Table-valued functions | **Done** (+ gated built-ins) | builder + `ISqlDialect.cs`, `SqlBuilder.cs` | `CommonTestSuite.Tvf.cs`, SQL-generation tests |
| 13 | Navigation properties / relationships | **Out of scope** | metadata (`Meta/`), `EntityBuilder.cs`, `SqlBuilder.cs` | — |
| 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | **Out of scope** | new subsystem + provider `DbCommand` layer | — |
| 15 | `APPLY` / `LATERAL` | **Done** (incl. correlated sources) | `JoinExpression.cs`, `SqlBuilder.cs`, dialects | SQL-generation tests |
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
| 26 | Warm-path plan-build (CTE / recursive CTE / `Join4` / `IN`-list) | **Closed (decision)** | `QueryCommand*.cs`, `SqlBuilder.cs`, `SqlSourceRenderer.cs`, `Visitors/`, `EntityBuilder.cs`, `JoinedEntityBuilder.cs` | `SqliteBenchmarkFeaturesFairCached`, SQL-generation tests |

Workstream 17–25 extended provider parity.

Future workstreams (not scheduled): dynamic-schema table sources (ClickHouse
`values()`, PostgreSQL `jsonb_to_record`), DML, navigation properties. The XML `.nodes` rowset (the
scalar `.value`/`.query`/`.exist`) and the native `PIVOT`/`UNPIVOT` source construct ship on SQL
Server.

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

- [nextorm vs linq2db: functionality comparison](../comparison/linq2db-comparison.md) — a focused side-by-side of the
  two libraries across the current query surface, including the provider-only function families.
