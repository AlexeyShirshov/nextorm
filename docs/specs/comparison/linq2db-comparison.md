# nextorm vs linq2db: functionality comparison

> A side-by-side functional comparison of nextorm and linq2db. It complements the
> [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) and the
> [capability matrix](capability-matrix.md) (which also cover EF Core). Based on the current tree, it shows
> nextorm matching or exceeding linq2db across the analytic query surface — join types, `APPLY`/`LATERAL`,
> full-text/JSON/arrays, cross-provider row values, `ROLLUP`/`CUBE`/`GROUPING SETS`, date arithmetic,
> temporal tables, row locking, statement/table/index hints, configurable keyword casing, TVFs and
> depth-one in-memory correlation — and adding an explicit `INSERT ... VALUES`/`INSERT ... SELECT`/returning
> surface, including PostgreSQL data-modifying CTEs.

**Prerequisites:** [Provider overview](../../providers/overview.md) · [Limitations](../../advanced/limitations.md) · [Query hints](../../guide/17-query-hints.md)

## Positioning

* **nextorm** is a focused, no-change-tracking SQL builder and mapper purpose-built for reading and
  reporting. It covers the complete analytic query surface, adds an explicit `INSERT ... VALUES`/`INSERT ... SELECT`/returning
  surface (single row, entity, batch, generated key, and data-modifying CTEs on PostgreSQL) and generates provider-portable
  SQL for SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse. It is built around a small
  allocation footprint, parameterisation and query compilation (implicit plan cache / explicit
  `Prepare()`), with opt-in output controls (identifier quoting, naming conventions, keyword casing) and
  index hints — and benchmarks at or above Dapper, EF Core and linq2db on the shipped scenarios.
* **linq2db** is a broad, mature LINQ-to-SQL ORM: a wider provider matrix, full CRUD (`INSERT`/`UPDATE`/
  `DELETE`/`MERGE`), associations/eager loading, bulk copy, temporary tables, schema code-generation
  tooling, extensibility (interceptors, custom SQL mapping) and an EF Core integration package. That extra
  surface comes with change tracking and a heavier model.

The two overlap on the *query* surface and basic inserts — where nextorm matches or exceeds linq2db — and
diverge on *full data modification*, *relationship modelling* and *tooling*, which nextorm leaves out by
design.

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
| Subqueries (`FROM`, scalar, correlated `EXISTS/IN/ANY/ALL`) | yes | yes — correlated at any nesting depth on the SQL providers and depth-one (scalar/aggregate/`EXISTS`/`IN`) on the in-memory provider; deeper in-memory forms throw `NotSupportedException` | `CorrelatedQueryExpressionVisitor.cs`, `MemberTranslator.TryTranslateProjectionOuterReference`, `DataContext/InMemoryCorrelatedPlan.cs` |
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
| CTEs (including recursive) | yes | yes — plus PostgreSQL **data-modifying CTEs** (`With(name, insert)`, typed read-back, `INSERT ... VALUES`/`INSERT ... SELECT` body, main `INSERT ... SELECT` reading a mutation CTE) | `Builders/CteQuery.cs`, `Builders/MutationCteQuery.cs`, `DataContext.DataContextExtensions.With/WithRecursive`, `ISqlDialect.SupportsDataModifyingCtes` |
| Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`) | yes | yes — plus named windows, the `GROUPS` frame unit, frame `EXCLUDE`, `percent_rank`/`cume_dist`, `nth_value` and the ClickHouse `lagInFrame`/`leadInFrame` | `Visitors/WindowFunctionTranslator.cs`, `WindowDefinition`, `SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion` |
| `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| String / math / date scalar functions, `LIKE` | yes | yes | `Visitors/ScalarFunctionTranslator.cs`, dialect `Make*` hooks |
| Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes across all providers (accepted fields differ and are validated per provider) | `CommonFunctions`, `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
| Full-text search | yes (provider) | **yes** on SQL Server, PostgreSQL and MySQL/MariaDB (boolean predicates; no ranking score) | `contains`/`freetext`, `ISqlDialect.SupportsFullText`/`MakeFullText` |
| Native JSON documents | yes | **yes on PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | **yes on SQL Server and MySQL/MariaDB** | `SupportsTextJson`, `MakeTextJsonFunction`/`MakeIsJson` |
| String JSON + dictionary functions (ClickHouse) | no | **yes on ClickHouse** | `SupportsJsonExtract`, `SupportsDictionaries`, `MakeJsonExtract`/`MakeDictionaryFunction` |
| Arrays (`cardinality`/`array_*`/`@>`/`&&`, ClickHouse `Array(T)`, `ARRAY JOIN`) | no | **yes on PostgreSQL and ClickHouse** (`ARRAY JOIN`; higher-order/lambda functions and the `Array(T)`/`Tuple` row reader are implemented) | `SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions`/`SupportsArrayJoin`, `ArraySqlTranslator`, `ArrayJoinClause`/`IArrayJoinRenderer.Render` |
| Row values / tuples (`ROW`/`(a, b)`, element access, row comparison) | yes (`Sql.Row`; emulated where the provider has no native row) | **yes on PostgreSQL and ClickHouse** (`ROW(a, b)`/`(row).fN` and `tuple(a, b)`/`tupleElement`); SQL Server, MySQL/MariaDB, SQLite and in-memory reject | `ISqlDialect.Tuple`/`ITupleRenderer`, `Visitors/TupleSqlTranslator.cs` |
| Conditional functions (`iif`/`choose`/`multi_if`) | no | **yes** (portable `iif`; `choose` SQL Server only; `multi_if` ClickHouse) | `CommonFunctions.iif`, `SupportsChoose`, `MultiIf`/`IMultiIfRenderer.Render` |
| `FOR JSON` / `FOR XML` | yes (provider) | **yes on SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | yes (provider) | **partial** — SQL Server only | `SqlServerFunctions.xml_value`/`xml_query`/`xml_exist`/`xml_nodes` |
| `GREATEST` / `LEAST` | partial | **yes** (NULL handling is provider-specific) | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | **yes** — `string_agg` cross-provider; `array_agg` on PostgreSQL | `SupportsStringAgg`/`SupportsArrayAgg` |
| User-defined scalar functions | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Table-valued functions | yes (`TableFunction`; DB TVFs can be scaffolded) | yes (`[SqlTableFunction]`); a small per-provider gated built-in set (`generate_series`/`unnest`, `string_split`/`openjson`/`containstable`/`freetexttable`, ClickHouse `numbers`/`zeros`/`generateRandom` + server/cluster functions); user-declared wrappers cover the rest | `SqlTableFunctionAttribute.cs`, `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Native `PIVOT` / `UNPIVOT` source | no (raw SQL) | **yes on SQL Server** | `EntityBuilder.Pivot`/`Unpivot` |
| Raw SQL (whole query) | yes | yes | `WithSql` / `PrepareFromSql` |
| Raw SQL as a composable source/subquery | yes | **yes** — `FromSql` renders the fragment as a derived table, joined/filtered further | `DataContextExtensions.FromSql`, `ISqlDialect.SupportsRawSqlSource` |
| Statement-level query hints | yes (provider specific) | **yes** — SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB inline `/*+ ... */`; SQLite/ClickHouse reject | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Locking table hints (e.g. `WITH (NOLOCK)`) | yes | **partial** — SQL Server only | `EntityBuilder.WithTableHint`, `ISqlDialect.SupportsTableHints`/`MakeTableHints` |
| Index hints (`USE`/`FORCE`/`IGNORE INDEX`, `INDEXED BY`, `WITH (INDEX(...))`) | yes (`IndexHint`/`TableHint`, provider-specific) | **yes** — MySQL/MariaDB, SQLite and SQL Server; PostgreSQL (without `pg_hint_plan`), ClickHouse and in-memory reject | `EntityBuilder.WithIndex`/`WithoutIndex`, `ISqlDialect.IndexHints`/`IIndexHintRenderer` |
| Identifier quoting | yes (per provider) | opt-in — `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`; default emits physical names verbatim | `ISqlDialect.QuoteIdentifier` |
| Naming conventions (e.g. snake_case) | via `MappingSchema`/attributes (no built-in convention) | opt-in — `UseNamingConvention()`/`WithNamingConvention()`; built-in `SnakeCaseNamingConvention`; explicit names stay verbatim | `INamingConvention` / `SnakeCaseNamingConvention` |
| SQL keyword casing (upper/lower) | no (keywords are emitted in the provider's canonical case) | **yes** — opt-in `KeywordCase.Upper`; the default `KeywordCase.Lower` is byte-for-byte the historical output | `KeywordCase`, `DataContextBuilder.UseKeywordCase`/`EntityBuilder.WithKeywordCase` |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **partial** — `INSERT ... VALUES` (single row, entity, batch), `INSERT ... SELECT` (`Values(source, mapping)` over an `EntityBuilder`), generated key (`ReturningIdentity`/`ReturningKey`) and returned rows (`Returning`, PostgreSQL/SQLite/SQL Server only) via `InsertInto`; **PostgreSQL data-modifying CTEs** (`With(name, insert)`/`CteQuery.With(name, insert)` → `MutationCteQuery<T>`, a write CTE whose `RETURNING` rows are read typed via `From`/`FromTable`, usable as a main `INSERT ... SELECT` source); **key upsert** (`MergeInto` → `MergeBuilder<T>`, native `ON CONFLICT ... DO UPDATE`/`ON DUPLICATE KEY UPDATE`/`MERGE ... USING (VALUES ...)`, ClickHouse/in-memory reject it); **`DELETE`** (`DeleteFrom` → `DeleteBuilder<T>` and `Delete<T>(entity)` by declared key, explicit `All()` for a full-table delete, native `DELETE FROM <table> [WHERE ...]`, `Returning()` for the removed rows, `Truncate<T>()` for `TRUNCATE TABLE`, ClickHouse `ALTER TABLE ... DELETE` mutation); **`UPDATE`** (`Update<T>(entity)`/`UpdateBuilder<T>`, predicate/key, `Returning`, multi-table join, ClickHouse mutation) and **full `MERGE`** with `WHEN MATCHED`/`WHEN NOT MATCHED`/`WHEN NOT MATCHED BY SOURCE` branches, conditions and `RETURNING`/`OUTPUT` (SQL Server, PostgreSQL 15+) implemented | `InsertBuilder<TEntity>`, `InsertReturningBuilder<TEntity,TResult>`, `MergeBuilder<TEntity>`, `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>`, `DeleteBuilder<TEntity>`, `UpdateBuilder<TEntity>`, `MutationCteQuery<TResult>`, `ISqlDialect.SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`/`SupportsIdentityFunction`/`SupportsDataModifyingCtes`/`SupportsOnConflict`/`SupportsOnDuplicateKey`/`SupportsMerge`/`SupportsDelete` |
| Bulk copy / merge / temporary tables | yes | **partial** — key upsert (`MergeInto`/`MergeBuilder<T>`), full `MERGE` with branches (`WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource`, arbitrary conditions, `RETURNING`/`OUTPUT`; SQL Server, PostgreSQL 15+) and materializing a query into a (temporary) table (`ToTempTable`/`ToTable`, `CREATE [TEMPORARY] TABLE ... AS SELECT`, PostgreSQL/SQLite/MySQL/MariaDB) are implemented; bulk insert is planned: [bulk insert](../roadmap/todo_bulk_insert.md) | `MergeBuilder<TEntity>`, `TempTableExtensions` |
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
* A small, explicit write surface: `INSERT ... VALUES` (single row, entity, batch, `DEFAULT`/all-defaults)
  and `INSERT ... SELECT`, the generated key
  (`ReturningIdentity`/`ReturningKey`), the inserted rows (`Returning`, on PostgreSQL/SQLite/SQL Server) and,
  on PostgreSQL, **data-modifying CTEs** (`With(name, insert)`/`CteQuery.With(name, insert)` → a write CTE
  whose `RETURNING` rows are read typed with the full operator set, or fed into a further `INSERT ... SELECT`) —
  explicit commands, with no change tracking and no `SaveChanges`.
* Cross-provider row values: `System.Tuple`/`ValueTuple` constructors, element access and row comparison
  render as `ROW(a, b)`/`(row).fN` on PostgreSQL and `tuple(a, b)`/`tupleElement` on ClickHouse, driven by
  `ISqlDialect.Tuple`.
* Index hints (`WithIndex`/`WithoutIndex`) across MySQL/MariaDB, SQLite and SQL Server, and configurable
  SQL keyword casing (`KeywordCase.Upper`) — both opt-in and both part of the plan key.
* Correlated subqueries evaluate once per outer row on the in-memory provider too (depth-one scalar,
  aggregate, `EXISTS` and `IN`), in addition to the SQL providers' arbitrary nesting depth.
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
* Configurable SQL output: identifier quoting, naming conventions and keyword casing are opt-in and
  overridable per command (`UseQuotedIdentifiers()`/`WithQuotedIdentifiers()`,
  `UseNamingConvention()`/`WithNamingConvention()`, `UseKeywordCase()`/`WithKeywordCase()`), so the
  generated SQL stays predictable by default while reserved-word/mixed-case schemas, snake_case catalogs
  and upper-case-keyword preferences need no hand-written names.

## Deliberate boundaries: what nextorm leaves to linq2db

These are conscious scope decisions in nextorm's focused, no-change-tracking model — not gaps on the query
surface, which nextorm matches or exceeds. linq2db covers them:

* **Full data modification**: nextorm ships an explicit write surface (`INSERT ... VALUES`/`INSERT ... SELECT`,
  returning, key upsert, `DELETE` and PostgreSQL data-modifying CTEs) only; full
  `UPDATE`, full `MERGE` with branches, bulk copy, temporary tables and change tracking are out of scope by design
  (each write is an explicit command, with no identity map). linq2db covers the full CRUD surface.
* **Relationships**: nextorm has no relationship metadata — joins are always explicit; linq2db adds
  `[Association]`, `LoadWith` eager loading and implicit join inference.
* **Plug-in extensibility and broader table hints**: linq2db offers interceptors, query filters and
  custom-SQL mapping, plus table hints on more providers (for example Oracle); nextorm deliberately keeps a
  fixed dialect contract with statement hints, SQL Server locking hints and index hints on MySQL/MariaDB,
  SQLite and SQL Server.
* **Database-first tooling**: linq2db ships a CLI/T4 code-generation toolchain that scaffolds entity and
  table-function mappings from a live database; nextorm declares mappings in code. The dynamic-schema
  sources nextorm still lacks (ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`) are unsupported by
  linq2db as well, so they are a shared gap tracked in
  [`todo_dynamic_result_schema.md`](../roadmap/todo_dynamic_result_schema.md).
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
| Model | Explicit CRUD ORM with associations; no automatic change tracking | Query builder and mapper without change tracking, with an explicit `INSERT` surface |
| Entity requirement | A mapped class required (attributes, fluent or convention) | Entity class optional — mapped via attributes/fluent/conventions, or none at all with `From("table")` + `TableAlias` |
| Reuse | Compiled queries, query cache | Implicit plan cache and `Prepare()` |
| Identifier quoting | on by default (per provider) | off by default; enabled with `UseQuotedIdentifiers()`/`WithQuotedIdentifiers()` |
| Name mapping | fluent/attributes (`MappingSchema`) | attributes/fluent/auto-derived, plus opt-in naming conventions (`UseNamingConvention()`/`WithNamingConvention()`) |
| SQL keyword case | fixed (provider-canonical, upper-case) | configurable, lower-case by default (`KeywordCase`) |
| Extensibility | Interceptors, custom SQL, provider extensions | Dialect contract and function attributes |

## Summary

For reading, reporting and the occasional explicit insert over an existing schema, nextorm is the stronger
choice: it covers essentially the whole analytic query surface linq2db offers — the provider-only function
families, the ClickHouse-specific constructs, cross-provider row values, TVFs and more — with a smaller
allocation footprint, benchmark results at or above Dapper, EF Core and linq2db on the shipped scenarios,
and more configurable SQL output (identifier quoting, naming conventions and keyword casing are opt-in and
overridable per command, whereas linq2db quotes by default and fixes names through its mapping schema).
linq2db remains the better fit only when the same layer must also perform full CRUD (`UPDATE` and
full `MERGE` with branches), model relationships or generate the data layer from a live schema — surface nextorm deliberately
leaves out.

## See also

- [Capability matrix: nextorm vs EF Core and linq2db](capability-matrix.md) — the exhaustive per-construct matrix.
- [linq2db backlog gap analysis](linq2db-backlog-gap-analysis.md) — what linq2db *plans to add* and which of it nextorm lacks.
- [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core and linq2db, per construct.
- [Limitations and out-of-scope features](../../advanced/limitations.md)
- [Joins](../../guide/03-joins.md) — `CrossApply`/`OuterApply`.
- [Query hints](../../guide/17-query-hints.md)
- [Provider overview](../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/comparison/capability-matrix.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/performance/benchmark-report.md`.
linq2db capabilities are described from its public documentation.
