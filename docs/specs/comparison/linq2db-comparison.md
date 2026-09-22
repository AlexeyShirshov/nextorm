# nextorm vs linq2db: functionality comparison

> A side-by-side functional comparison of nextorm and linq2db. It complements the
> [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) and the
> [capability matrix](capability-matrix.md) (which also cover EF Core), and is based on the current
> `1.0.3-alpha` tree, including the join-type, `APPLY`/`LATERAL`, function-parity (full-text, JSON, arrays,
> `ROLLUP`/`CUBE`/`GROUPING SETS`, date arithmetic), temporal-table, row-locking and query/table-hint
> additions.

**Prerequisites:** [Provider overview](../../providers/overview.md) · [Limitations](../../advanced/limitations.md) · [Query hints](../../guide/17-query-hints.md)

## Positioning

* **linq2db** is a mature, full-featured LINQ-to-SQL ORM: a wide provider matrix, DML (insert/update/
  delete/merge), associations/eager loading, bulk copy, temporary-table support, query and table hints,
  extensibility (interceptors, custom SQL mapping) and an EF Core integration package. It sits between a
  micro-ORM and a full ORM.
* **nextorm** is a read-only, no-change-tracking SQL builder and mapper. It deliberately omits DML,
  change tracking and relationship metadata, and focuses on a small allocation footprint, parameterisation,
  query compilation (plan cache / `Prepare()`) and provider-portable SQL generation.

The two overlap on the *query* surface; they diverge on *data modification* and *relationship modelling*.

## Capability matrix

Legend: **yes** = first-class; **partial** = supported with limits; **no** = not supported. Evidence for
nextorm points at the source that owns the behaviour.

| Area | linq2db | nextorm | nextorm evidence |
|---|---|---|---|
| Projection (`SELECT`, DTO/anonymous/record/tuple/scalar) | yes | yes | `EntityBuilder.Select` |
| Predicates (`WHERE`: comparison, `and`/`or`/`!`, arithmetic, bitwise/shift) | yes | yes | `Visitors/WhereExpressionVisitor.cs`, `BaseExpressionVisitor.cs` |
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes | **yes** (`FULL JOIN` not on MySQL/MariaDB) | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin`, `ISqlDialect.SupportsFullJoin` |
| `APPLY` / `LATERAL` | yes | **yes** (including correlated sources; gated off on SQLite/ClickHouse) | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.SupportsApply`/`MakeApply` |
| Join strictness (`ANY`/`ALL`/`ASOF`) and `GLOBAL` | no | **yes** on ClickHouse (`SEMI`/`ANTI`/`PASTE` via `SemiJoin`/`AntiJoin`/`PasteJoin`) | `JoinStrictness`, `EntityBuilder.WithStrictness`/`Global`, `ISqlDialect.SupportsJoinStrictness`/`SupportsGlobalJoin` |
| Join arity | unlimited | 2–8 (compile-time cap) | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| JOIN to a derived table (subquery) | yes | **yes** — either side: the joined side (`Join(QueryCommand<T>)`) or the primary `FROM` source | `EntityBuilder`, `SqlBuilder.MakeFrom`, `DataContextExtensions.From(QueryCommand)` |
| Subqueries (`FROM`, scalar, correlated `EXISTS/IN/ANY/ALL`) | yes | yes — correlated at any nesting depth on the SQL providers; the in-memory provider throws `NotSupportedException` | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference` |
| `IN` over a list/array | yes | yes (+ ClickHouse distributed `GLOBAL IN`) | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs`, `SqlFunctions.ClickHouse.global_in` |
| `GROUP BY` / `HAVING` / aggregates | yes | yes — plus `FILTER (WHERE ...)`, boolean/bit/statistical/regression aggregates and the `arg_min`/`arg_max`, `uniq*`, `quantile*`/`median` and `-If` families | `EntityBuilder.GroupBy/Having`, `AdvancedAggregateTranslator.cs`, `Supports*Aggregates` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes | yes per provider (`WITH TOTALS` on ClickHouse) | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`/`WithTotals`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`/`SupportsGroupByWithTotals` |
| `LIMIT n BY expr` | no | **ClickHouse only** | `LimitBy`, `ILimitByRenderer.Render` |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | no | **ClickHouse only** (the cross-provider `TABLESAMPLE` analog is separate, below) | `Final`/`Sample`/`PreWhere`/`Settings`, `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, dialect `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL` where the engine has it) | yes | yes (provider dependent; `ALL` on PostgreSQL, MariaDB and ClickHouse) | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | no | yes per provider (`DISTINCT ON` PostgreSQL; `WITH TIES` PostgreSQL + SQL Server; `TABLESAMPLE` PostgreSQL + SQL Server) | `DistinctOn`/`WithTies`/`TableSample`, `ISqlDialect.SupportsWithTies` |
| Row locking (`FOR UPDATE`/`FOR SHARE`) | partial — SQL Server via `UPDLOCK`/`XLOCK` table hints, provider-specific elsewhere | yes per provider (PostgreSQL/MySQL/MariaDB trailing `FOR UPDATE`/`LOCK IN SHARE MODE`; SQL Server via table hints) | `ForUpdate`/`ForShare`, `ILockRenderer`/`ILockRenderer.UsesTableHints` |
| Temporal tables (`FOR SYSTEM_TIME`) | yes (SQL Server) | **SQL Server + MariaDB** (`CONTAINED IN` — SQL Server only) | `ForSystemTime`, `SupportsTemporalTable`/`SupportsTemporalKind`/`MakeTemporalTable` |
| CTEs (including recursive) | yes | yes | `Builders/CteQuery.cs`, `DataContext.DataContextExtensions.With/WithRecursive` |
| Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`) | yes | yes — plus named windows, the `GROUPS` frame unit, frame `EXCLUDE`, `percent_rank`/`cume_dist`, `nth_value` and the ClickHouse `lagInFrame`/`leadInFrame` | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion` |
| `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| String / math / date scalar functions, `LIKE` | yes | yes | `Visitors/ScalarFunctionTranslator.cs`, dialect `Make*` hooks |
| Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes across all providers (accepted fields differ and are validated per provider) | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Full-text search | yes (provider) | **yes** on SQL Server, PostgreSQL and MySQL/MariaDB (boolean predicates; no ranking score) | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Native JSON documents | yes | **yes on PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | **yes on SQL Server and MySQL/MariaDB** | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| String JSON + dictionary functions (ClickHouse) | no | **yes on ClickHouse** | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Arrays (`cardinality`/`array_*`/`@>`/`&&`, ClickHouse `Array(T)`, `ARRAY JOIN`) | no | **yes on PostgreSQL and ClickHouse** (`ARRAY JOIN`; higher-order/lambda functions and the `Array(T)`/`Tuple` row reader are implemented) | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `ArrayJoinClause`/`IArrayJoinRenderer.Render` |
| Conditional functions (`iif`/`choose`/`multi_if`) | no | **yes** (portable `iif`; `choose` SQL Server only; `multi_if` ClickHouse) | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes (provider) | **yes on SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | yes (provider) | **partial** — SQL Server only | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | partial | **yes** (NULL handling is provider-specific) | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | **yes** — `string_agg` cross-provider; `array_agg` on PostgreSQL | `SupportsStringAgg`/`SupportsArrayAgg` |
| User-defined scalar functions | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Table-valued functions | yes (`TableFunction`) | yes (`[SqlTableFunction]`); built-ins gated, the pre-declared set (`generate_series`/`unnest`/…, `string_split`/`openjson`, `containstable`/`freetexttable`, ClickHouse `numbers`/`zeros`/`generateRandom`) is smaller | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Native `PIVOT` / `UNPIVOT` source | no (raw SQL) | **yes on SQL Server** | `EntityBuilder.Pivot`/`Unpivot` |
| Raw SQL (whole query) | yes | yes | `WithSql` / `PrepareFromSql` |
| Raw SQL as a composable source/subquery | yes | **yes** — `FromSql` renders the fragment as a derived table, joined/filtered further | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Query hints | yes (provider specific) | **yes** — SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB inline `/*+ ... */`; SQLite/ClickHouse reject | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Table hints (e.g. `WITH (NOLOCK)`) | yes | **partial** — SQL Server only | `EntityBuilder.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Identifier quoting | yes (per provider) | opt-in — `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`; default emits physical names verbatim | `ISqlDialect.QuoteIdentifier` |
| Naming conventions (e.g. snake_case) | via `MappingSchema`/attributes (no built-in convention) | opt-in — `UseNamingConvention()`/`WithNamingConvention()`; built-in `SnakeCaseNamingConvention`; explicit names stay verbatim | `INamingConvention` / `SnakeCaseNamingConvention` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **no** (read-only by design) | — |
| Bulk copy / merge / temporary tables | yes | **no** | — |
| Navigation properties / associations / eager loading | yes (`[Association]`, `LoadWith`) | **no** | — |
| Change tracking / identity map | partial | **no** (by design) | — |
| Extensibility (interceptors, custom SQL, query filters) | extensive | minimal (dialect + `[SqlFunction]`/`[SqlTableFunction]`) | `SqlDialectBase` |
| Providers | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | `src/nextorm.*` |
| EF Core integration | yes (`linq2db.EntityFrameworkCore`) | no | — |
| Performance posture | high | benchmarked at/above Dapper, EF Core and linq2db on the shipped scenarios | `docs/specs/performance/benchmark-report.md` |

## What nextorm does well

* The complete analytic query surface: every join type including `APPLY`/`LATERAL` (including correlated
  sources), derived-table joins, ClickHouse join strictness/`GLOBAL`, set operations, `DISTINCT`
  (plus `DISTINCT ON`/`WITH TIES`), CTEs (recursive), window functions (named windows, `GROUPS`,
  frame `EXCLUDE`, `nth_value`, `percent_rank`/`cume_dist`), `ROLLUP`/`CUBE`/`GROUPING SETS`/`WITH TOTALS`,
  `CASE`/`COALESCE`/`CAST`, string/math/date functions, `IN`-lists, UDF/TVF mapping, native `PIVOT`/`UNPIVOT`,
  temporal tables, row locking and raw SQL for a whole query.
* Provider-portable rendering: the same C# renders `CROSS APPLY` on SQL Server and
  `CROSS JOIN LATERAL` on PostgreSQL/MySQL/MariaDB, driven by `ISqlDialect` capabilities.
* Provider-only surfaces behind the same capability gate — PostgreSQL native JSON, arrays and the
  extended scalar library; SQL Server `FOR JSON`/`FOR XML`, JSON-as-text and `string_split`/`openjson`;
  ClickHouse `Array(T)`/`ARRAY JOIN`, `JSONExtract*`, dictionaries, the quantile/`uniq`/`argMin`-`argMax`
  families, `LIMIT BY`, `PREWHERE`/`FINAL`/`SETTINGS` and multi-branch `multiIf`.
* Full-text search (`contains`/`freetext`) on SQL Server, PostgreSQL and MySQL/MariaDB.
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
* Configurable mapping output: identifier quoting and naming conventions are opt-in and overridable per
  command (`UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`,
  `UseNamingConvention()`/`WithNamingConvention()`), so the generated SQL stays predictable by default
  while reserved-word/mixed-case schemas and snake_case catalogs need no hand-written names.

## Where linq2db is stronger

* **Data modification**: `INSERT`/`UPDATE`/`DELETE`/`MERGE`, bulk copy, temporary tables — entirely
  absent from nextorm by design.
* **Relationships**: `[Association]`, `LoadWith` eager loading and implicit join inference.
* **Hint breadth**: nextorm exposes statement-level query hints on SQL Server, PostgreSQL and
  MySQL/MariaDB, but table hints only on SQL Server; linq2db additionally covers cross-provider table
  hints,   plus query filters, interceptors and other extensibility.
* **Coverage beyond the query core**: a larger pre-declared TVF set (though nextorm now ships
  `CONTAINSTABLE`/`FREETEXTTABLE` with `KEY`/`RANK`, the PostgreSQL `ts_rank`/`ts_rank_cd` and the SQL
  Server XML `.nodes` rowset via `xml_nodes`), and dynamic-schema sources (ClickHouse `values()`/server
  table functions, MySQL `JSON_TABLE`, PostgreSQL `jsonb_to_record`).
* **Provider breadth**: Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE and more.
* **EF Core integration** and a larger ecosystem.

The in-memory provider remains the one place where correlation is limited: nextorm supports correlated
scalar subqueries, correlated `EXISTS`/`IN`/`ANY`/`ALL` and correlated `APPLY`/`LATERAL` sources at any
nesting depth on the SQL providers, while the in-memory provider has no per-row outer-row binding (see
[`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4).

## Architecture differences

| Aspect | linq2db | nextorm |
|---|---|---|
| Model | Explicit CRUD ORM with associations; no automatic change tracking | Read-only query builder and mapper |
| Entity requirement | Mapping via attributes/fluent/inference | Entity class optional; `From("table")` with `TableAlias` |
| Reuse | Compiled queries, query cache | Implicit plan cache and `Prepare()` |
| Identifier quoting | on by default (per provider) | off by default; enabled with `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` |
| Name mapping | fluent/attributes (`MappingSchema`) | attributes/fluent/auto-derived, plus opt-in naming conventions (`UseNamingConvention()`/`WithNamingConvention()`) |
| Extensibility | Interceptors, custom SQL, provider extensions | Dialect contract and function attributes |

## Summary

If the requirement is *read and report over an existing schema* with a small, fast, provider-portable
mapper, nextorm now covers essentially the whole analytic query surface that linq2db offers, including
the provider-only function families and the ClickHouse-specific constructs. The remaining functional delta
is deliberate: DML and change tracking, relationships, table hints outside SQL Server, composable raw SQL,
a larger pre-declared TVF set, broader provider coverage and the
larger extensibility/ecosystem surface. Mapping output, by contrast, is more configurable in nextorm:
identifier quoting and naming conventions are opt-in and can be overridden per command, whereas
linq2db quotes by default and fixes names through its mapping schema. Conversely, linq2db is the better
fit when the same layer must also
write data and model relationships.

## See also

- [Capability matrix: nextorm vs EF Core and linq2db](capability-matrix.md) — the exhaustive per-construct matrix.
- [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core and linq2db, per construct.
- [Limitations and out-of-scope features](../../advanced/limitations.md)
- [Joins](../../guide/03-joins.md) — `CrossApply`/`OuterApply`.
- [Query hints](../../guide/17-query-hints.md)
- [Provider overview](../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/comparison/capability-matrix.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/performance/benchmark-report.md`.
linq2db capabilities are described from its public documentation.
