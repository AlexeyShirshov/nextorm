# SQL query-building capabilities: nextorm vs EF Core and linq2db

This document compares the SQL-query-building surface of nextorm with EF Core and linq2db, lists what
nextorm supports today, and enumerates the gaps. It is the reference for the implementation workstreams
tracked in [Implementation plan](#5-implementation-plan).

The analysis is based on the current `1.0.3-alpha` tree: `SqlBuilder`, `QueryCommand`,
`BaseExpressionVisitor`, `CorrelatedQueryExpressionVisitor`, the join builders, the `ISqlDialect`
contract and the provider dialects, plus the integration tests under `test/nextorm.integration.tests`.

> **Status (updated 2026-09-18).** Sections 1–4 below started as the original gap analysis and were
> refreshed in place; the implementation status is:
>
> | # | Workstream | Status |
> |---|---|---|
> | 1 | Join types: `LEFT`/`RIGHT`/`FULL`/`CROSS` (+ fluent API, in-memory, dialects) | **Done** |
> | 2 | Join arity > 3 (4..8 supported via `Projection<T1..T8>`/`EntityP2..P8`) | **Done** |
> | 3 | `CASE WHEN` / ternary / `switch` | **Done** |
> | 4 | String, math and date scalar functions + `LIKE` | **Done** |
> | 5 | `IN` over a list/array (`@in`, `Contains`) | **Done** (+ optimized) |
> | 6 | Logical `!` and unary operators | **Done** |
> | 7 | `SELECT DISTINCT` | **Done** |
> | 8 | `INTERSECT` / `EXCEPT` (+`ALL` on PostgreSQL, MariaDB and ClickHouse) | **Done** (+ optimized) |
> | 9 | CTEs (`WITH`, recursive) | **Done** |
> | 10 | Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`) | **Done** |
> | 11 | User-defined scalar-valued functions (`[SqlFunction]`) | **Done** |
> | 12 | Table-valued functions (`[SqlTableFunction]`) | **Done**; the built-in `NORM.SQL` TVFs are gated by `ISqlDialect.SupportsTableFunction` |
> | 13 | Navigation properties / relationships | **Out of scope** |
> | 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | **Out of scope** |
> | 15 | `APPLY` / `LATERAL` (`CrossApply`/`OuterApply`) | **Partial** — SQL Server `CROSS/OUTER APPLY`, PostgreSQL/MySQL/MariaDB `LATERAL`; the applied source cannot be correlated yet (no public outer-reference API for a `FROM` subquery) |
> | 16 | Statement-level query hints (`Hint(...)`) | **Done on SQL Server** (`OPTION (...)`); other dialects reject hints with `NotSupportedException` |
> | 17 | Full-text search (`contains`/`freetext`) | **Done** on SQL Server, PostgreSQL and MySQL/MariaDB via `MakeFullText` |
> | 18 | JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | **Done on SQL Server** (`SupportsTextJson`); native JSON documents on PostgreSQL (`SupportsJson`) |
> | 19 | `FOR JSON` / `FOR XML` | **Done on SQL Server** (`ForJson`/`ForXml`) |
> | 20 | `GREATEST` / `LEAST` | **Done** on SQL Server, PostgreSQL, MySQL/MariaDB and ClickHouse |
> | 21 | `STRING_AGG` / `ARRAY_AGG` (incl. `WITHIN GROUP`, `FILTER`) | **Done** — `string_agg` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `array_agg` on PostgreSQL (`MakeStringAgg`/`MakeArrayAgg`) |
> | 22 | `ROLLUP` / `CUBE` / `GROUPING SETS` | **Done** — `ROLLUP` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `CUBE`/`GROUPING SETS` on SQL Server, PostgreSQL, SQLite and ClickHouse (`GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`) |
> | 23 | Date arithmetic (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | **Done** across SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; the accepted fields differ per provider and are validated by `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField` |
> | 24 | Table hints (`with (nolock)`, ...) | **Done on SQL Server** (`WithTableHint`); other dialects throw |
> | 25 | PostgreSQL extended scalar functions (`make_interval`, `justify_*`, `to_*`, `timezone`, `current_*`) | **Done on PostgreSQL** (`SupportsExtendedScalarFunctions`) |
>
> Test coverage after the work is **83.6% line** (CI threshold 75%); the full integration suite is
> 803 tests / 0 failed / 23 capability-based skips. Benchmarks and the performance optimizations that
> followed are documented in
> [Iteration 6 of `benchmark-report.md`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmark-report.md):
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

The managed SQL surface is split by portability: cross-provider helpers live on `NORM.SQL`, while the
provider-only functions live on a provider-specific surface — `NORM.PG_SQL` (native arrays, native
JSON, the extended scalar library, the PG-only aggregates and the `generate_series`/`unnest` table
functions), `NORM.MS_SQL` (JSON-as-text and `string_split`/`openjson`) and `NORM.CLK_SQL`
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
| Query hints | yes | yes (provider specific) | **partial** — SQL Server only | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Table hints | yes | yes | **partial** — SQL Server only | `EntityBuilder.WithTableHint`, `SupportsTableHints`/`MakeTableHints` |
| JOIN to a derived table (subquery) | yes | yes | **yes** | `EntityBuilder`, `SqlBuilder.MakeFrom` |
| More than two joined tables | unlimited | unlimited | **yes — up to 8** | `Projection<T1..T8>`, `EntityP2..P8` |
| Subquery in `FROM` | yes | yes | **yes** | `SqlBuilder.MakeFrom`, `FromExpression` |
| Scalar subquery in `SELECT`/`WHERE`/`ORDER BY` | yes | yes | **yes** (non-correlated) | `CommonTestSuite.SqlCommand.cs` |
| Correlated subquery | yes | yes | **partial** — `EXISTS`/`IN`/`ANY`/`ALL` only | `CorrelatedQueryExpressionVisitor.cs` |
| `IN` (subquery) | yes | yes | **yes** | `NORM.@in`, `BaseExpressionVisitor` |
| `IN` (list/array/`Contains`) | yes | yes | **yes** | `NORM.@in`, `Contains` |
| `EXISTS` / `ANY` / `ALL` | yes | yes | **yes** | `NORM.exists/any/all` |
| `WHERE` (and/or/not, comparisons) | yes | yes | **yes** | `WhereExpressionVisitor`, `VisitUnary` |
| `GROUP BY` | yes | yes | **yes** | `EntityBuilder.GroupBy`, `SqlBuilder` |
| `ROLLUP` / `CUBE` / `GROUPING SETS` | yes | yes | **yes** — `ROLLUP` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `CUBE`/`GROUPING SETS` on SQL Server, PostgreSQL, SQLite and ClickHouse | `GroupByRollup`/`GroupByCube`/`GroupByGroupingSets`, `SupportsRollup`/`SupportsCube`/`SupportsGroupingSets` |
| `HAVING` | yes | yes | **yes** | `EntityBuilder.Having` |
| Aggregates (`count`/`min`/`max`/`avg`/`sum`/`stdev`/`var`, distinct) | yes | yes | **yes** — `FILTER (WHERE ...)` on PostgreSQL and SQLite; boolean aggregates on PostgreSQL; bit and statistical aggregates on PostgreSQL and ClickHouse; `regr_*` on PostgreSQL; `arg_min`/`arg_max` and `-If` combinators on ClickHouse | `AdvancedAggregateTranslator.cs`, `Supports*Aggregates` |
| `SELECT DISTINCT` | yes | yes | **yes** | `EntityBuilder.IsDistinct` |
| `ORDER BY` (expression/ordinal, asc/desc, multiple) | yes | yes | **yes** | `EntityBuilder`, `SqlBuilder` |
| `LIMIT`/`OFFSET`/`TOP` | yes | yes | **yes** | dialect `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | **yes** | `QueryCommand`, `SqlBuilder` |
| `INTERSECT` / `EXCEPT` | yes | yes | **yes** (with `ALL` on PostgreSQL, MariaDB and ClickHouse) | `UnionType`, `SupportsIntersectExceptAll` |
| CTE (`WITH`), recursive CTE | yes | yes | **yes** | `QueryCommand.Cte`, `EntityBuilder` |
| Window functions (`OVER`, `ROW_NUMBER`, ...) | yes | yes | **yes** | `NORM_SQL.row_number/rank/lag/...`, dialects |
| `CASE WHEN` / ternary `?:` / `switch` | yes | yes | **yes** | `BaseExpressionVisitor.VisitConditional` |
| `COALESCE` (`??`) | yes | yes | **yes** | `BaseExpressionVisitor`, `MakeCoalesce` |
| `CAST` (numeric) | yes | yes | **yes** | `BaseExpressionVisitor` |
| `LIKE` / string methods (`Contains`, `StartsWith`, `ToUpper`, `Substring`, `Trim`, `Remove`, `Insert`, `IndexOf`, `LastIndexOf`, `PadLeft`, `PadRight`, `new string(char, n)`, `Split`/`Join` on arrays) | yes | yes | **yes** (string `LastIndexOf` is not available on SQLite, which has no reversal; `Split`/`Join` require PostgreSQL arrays) | `BaseExpressionVisitor`, dialect string hooks |
| Math functions (`Math.*`) | yes | yes | **yes** | `BaseExpressionVisitor`, `MakeMathFunction` |
| Date/time functions (`DATEPART`, ...) | yes | yes | **yes** | `NORM_SQL`, `MakeDatePart`/`MakeDateAdd`/... |
| Full-text search | partial (`EF.Functions`) | yes (provider) | **yes** on SQL Server, PostgreSQL, MySQL/MariaDB | `contains`/`freetext`, `SupportsFullText`/`MakeFullText` |
| Native JSON | yes | yes | **yes on PostgreSQL** | `SupportsJson`, `JsonSqlTranslator` |
| JSON scalar functions (`json_value`/`json_query`/`json_modify`, `isjson`) | yes | yes | **yes on SQL Server** | `SupportsTextJson`, `MakeTextJsonFunction`, `MakeIsJson` |
| `FOR JSON` / `FOR XML` | partial | yes (provider) | **yes on SQL Server** | `QueryCommand.ForJson/ForXml`, `SupportsForJson`/`SupportsForXml` |
| `GREATEST` / `LEAST` | yes | partial | **yes** on SQL Server, PostgreSQL, MySQL/MariaDB, ClickHouse | `SupportsGreatestLeast`/`MakeGreatest`/`MakeLeast` |
| `STRING_AGG` / `ARRAY_AGG` | yes | yes | **yes** — `string_agg` on SQL Server, PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse; `array_agg` on PostgreSQL | `string_agg`/`array_agg`, `SupportsStringAgg`/`SupportsArrayAgg` |
| User-defined scalar-valued functions | yes (`DbFunction`) | yes (`Sql.Ext`/custom) | **yes** (`[SqlFunction]`) | `UdfScalarTranslator` |
| Table-valued functions | yes (TVF mapping) | yes (`TableFunction`) | **yes** (`[SqlTableFunction]`); built-ins gated | `SqlBuilder.MakeTableFunction`, `SupportsTableFunction` |
| Navigation properties (implicit joins) | yes | yes | **no** | explicit joins only; no relationship metadata |
| DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | yes | **no** | read-only provider |
| Raw SQL (whole query) | yes (`FromSql`) | yes | **yes** | `PrepareFromSql`/`WithSql` |
| Raw SQL as a composable source/subquery | yes | yes | **no** | `WithSql` replaces the whole query |

The matrix was originally written before the section-5 workstreams landed; it has been updated in place on
2026-09-18. For per-provider details see `docs/providers/*.md`.

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
  `groupBitAnd`/`groupBitOr`/`groupBitXor`, `corr`/`covarPop`/`covarSamp`, `argMin`/`argMax`.
* **Subqueries** in `FROM`, scalar subqueries in `SELECT`/`WHERE`/`ORDER BY`, and correlated
  `EXISTS`/`IN`/`ANY`/`ALL`.
* **Derived-table joins** (`Join(QueryCommand<T>)`).
* **Full-text search** (`contains`/`freetext`) on SQL Server, PostgreSQL and MySQL/MariaDB, expressed
  through a single `MakeFullText` dialect hook.
* **JSON** — native JSON documents on PostgreSQL, the standard `json_value`/`json_query`/`json_modify`
  and `isjson` on SQL Server, and `FOR JSON`/`FOR XML` on SQL Server.
* **Date arithmetic** — `date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts` plus the
  `DateTime.Add*` methods, each provider emitting its native form.
* **Built-in table-valued functions** — `generate_series`/`unnest` (PostgreSQL),
  `string_split`/`openjson` (SQL Server) — with a `SupportsTableFunction` gate so an unsupported provider
  throws instead of emitting invalid SQL.
* **Raw SQL** for a whole query (`PrepareFromSql` / `WithSql`) with named parameters.

---

## 4. Remaining gaps, in order of significance

1. **Correlated scalar subqueries** are only expressible through `EXISTS`/`IN`/`ANY`/`ALL`; a general
   correlated scalar projection in `SELECT`/`WHERE` is not covered, although the `OuterRefMarker`
   mechanism exists. The same missing outer-reference API is what limits `APPLY`/`LATERAL` sources.
2. **`APPLY`/`LATERAL` sources cannot be correlated.** SQL Server `CROSS/OUTER APPLY` and
   PostgreSQL/MySQL/MariaDB `LATERAL` are emitted for non-correlated sources only.
3. **Raw SQL is not composable.** `WithSql` replaces the whole query, so raw SQL cannot be used as a
   `FROM` source, joined, or further filtered; EF Core (`FromSql`) and linq2db both allow this.
4. **The pre-declared table-function set is small.** `NORM.SQL` ships only four built-ins, each gated by
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
   `[Column]` mapping — as `NORM.IOpenJsonRow.Key` does for the T-SQL reserved word `key`. EF Core and
   linq2db escape identifiers per provider instead. Fixing this globally would change every generated
   statement and is deliberately deferred.
7. **Provider field/feature differences remain.** The accepted `date_add`/`date_trunc`/`date_diff` fields
   differ per provider (e.g. SQLite folds `millisecond`/`quarter`, SQL Server rejects
   `decade`/`century`/`millennium` for `date_trunc`), `CUBE`/`GROUPING SETS` and `FULL JOIN` are missing
   on MySQL/MariaDB, `GREATEST`/`LEAST` is missing on SQLite, and several features
   (`FOR JSON`/`FOR XML`, table hints, query hints) exist on a subset of providers. These are documented
   in `docs/providers/*.md` rather than unified.
8. **No `PIVOT`/`UNPIVOT`, temporal tables or XML-data-type methods** (`.value`/`.query`/`.nodes`/`.exist`).
9. **No DML and no navigation properties / relationship metadata** — by design for a read-only,
   no-change-tracking mapper, but still a functional gap versus both references.

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
| 5 | `IN` over a list/array | **Done** | `NORM.cs`, `BaseExpressionVisitor.cs`, dialects | `CommonTestSuite.In.cs`, SQL-generation tests |
| 6 | Logical `!` and unary operators | **Done** | `BaseExpressionVisitor.cs`, `WhereExpressionVisitor.cs` | `CommonTestSuite.Binary.cs`/`CommonTestSuite.Unary.cs`, SQL-generation tests |
| 7 | `SELECT DISTINCT` | **Done** | `QueryCommand.cs`, `SqlBuilder.cs`, `EntityBuilder.cs` | `CommonTestSuite.Distinct.cs` |
| 8 | `INTERSECT` / `EXCEPT` | **Done** | `QueryCommand.cs`, `UnionType.cs`, `SqlBuilder.cs` | `CommonTestSuite.SetOperations.cs` |
| 9 | CTEs (`WITH`, recursive) | **Done** | `QueryCommand.cs`, `SqlBuilder.cs`, builder API | `CommonTestSuite.Cte.cs` |
| 10 | Window functions (`OVER`, ranking, framed) | **Done** | `NORM.cs`, `BaseExpressionVisitor.cs`, dialects | `CommonTestSuite.Window.cs`, SQL-generation tests |
| 11 | User-defined scalar-valued functions | **Done** | `NORM.cs`, `ISqlDialect.cs`, `BaseExpressionVisitor.cs` | `CommonTestSuite.Udf.cs` |
| 12 | Table-valued functions | **Done** (+ gated built-ins) | builder + `ISqlDialect.cs`, `SqlBuilder.cs` | `CommonTestSuite.Tvf.cs`, SQL-generation tests |
| 13 | Navigation properties / relationships | **Out of scope** | metadata (`Meta/`), `EntityBuilder.cs`, `SqlBuilder.cs` | — |
| 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | **Out of scope** | new subsystem + provider `DbCommand` layer | — |
| 15 | `APPLY` / `LATERAL` | **Partial** — non-correlated only | `JoinExpression.cs`, `SqlBuilder.cs`, dialects | SQL-generation tests |
| 16 | Statement-level query hints | **Done on SQL Server** | `QueryCommand.TResult.cs`, `SqlBuilder.cs`, SQL Server dialect | SQL-generation tests |
| 17 | Full-text search (`contains`/`freetext`) | **Done** | `BuiltinFunctionTranslator.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | SQL-generation tests |
| 18 | JSON scalar functions and `isjson` | **Done on SQL Server** | `TextJsonSqlTranslator.cs`, `ISqlDialect.cs`, SQL Server dialect | SQL-generation tests |
| 19 | `FOR JSON` / `FOR XML` | **Done on SQL Server** | `ForJson.cs`, `ForXml.cs`, `QueryCommand.TResult.cs`, `SqlBuilder.cs`, SQL Server dialect | SQL-generation tests |
| 20 | `GREATEST` / `LEAST` | **Done** | `BuiltinFunctionTranslator.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | SQL-generation tests |
| 21 | `STRING_AGG` / `ARRAY_AGG` | **Done** | `NORM.cs`, `BuiltinFunctionTranslator.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | SQL-generation tests (+ ClickHouse integration) |
| 22 | `ROLLUP` / `CUBE` / `GROUPING SETS` | **Done** | `EntityBuilder.cs`, `QueryCommand.cs`, `SqlBuilder.cs`, dialects | SQL-generation tests |
| 23 | Date arithmetic parity (MySQL/MariaDB, SQLite) | **Done** | `BuiltinFunctionTranslator.cs`, dialects | SQL-generation tests + provider integration tests |
| 24 | Table hints | **Done on SQL Server** | `EntityBuilder.cs`, `SqlBuilder.cs`, SQL Server dialect | SQL-generation tests |
| 25 | PostgreSQL extended scalar functions | **Done** | `NORM.cs`, `ExtendedScalarFunctionTranslator.cs`, Postgres dialect | SQL-generation tests |

Workstream 17–25 extended provider parity and are tracked in detail in
[`todo_mssql.md`](https://github.com/AlexeyShirshov/nextorm/blob/main/todo_mssql.md).

Future workstreams (not scheduled): general correlated scalar subqueries / correlated `APPLY`, composable
raw SQL, `CONTAINSTABLE`/`FREETEXTTABLE` with ranking, `OPENJSON ... WITH` typed schemas, `JSON_TABLE`,
`PIVOT`/`UNPIVOT`, temporal tables, XML-data-type methods, DML, navigation properties.

### Cross-cutting requirements

* Preserve the public API compatibility rules in the `api-design` skill: extend-only, no breaking changes
  to existing signatures.
* Every workstream adds tests before it is considered done. SQL-generation tests
  (`test/nextorm.*.tests/SqlGenerationTests.cs`) do not require a database; integration tests run against
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

# Provider SQL-generation tests (no database required). The solution uses the
# Microsoft.Testing.Platform runner, so plain `dotnet test` discovers nothing:
dotnet run --project test/nextorm.sqlite.tests -c Release
dotnet run --project test/nextorm.sqlserver.tests -c Release
dotnet run --project test/nextorm.postgres.tests -c Release
dotnet run --project test/nextorm.mysql.tests -c Release
dotnet run --project test/nextorm.mariadb.tests -c Release
dotnet run --project test/nextorm.clickhouse.tests -c Release

# Integration tests (SQLite runs locally). The PostgreSQL/SQL Server/MySQL/ClickHouse
# containers are started with Testcontainers; point it at the Podman socket and disable
# Ryuk (its bind-mount is not visible inside the Podman machine):
DOCKER_HOST="unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock" \
  TESTCONTAINERS_RYUK_DISABLED=true \
  dotnet run --project test/nextorm.integration.tests -c Release
```

Without `DOCKER_HOST` every non-SQLite provider suite is skipped by `ProviderTestSuite`'s
`Assert.SkipUnless(Provider.IsAvailable, ...)`, so a "green" run proves nothing about the database
providers. External servers can be used instead via `NEXTORM_POSTGRES_CONNECTION`,
`NEXTORM_SQLSERVER_CONNECTION`, `NEXTORM_MYSQL_CONNECTION` and `NEXTORM_CLICKHOUSE_CONNECTION`.

---

## See also

- [nextorm vs linq2db: functionality comparison](linq2db-comparison.md) — a focused side-by-side of the
  two libraries, including the `APPLY`/`LATERAL` and query-hint status.
- [`todo_mssql.md`](https://github.com/AlexeyShirshov/nextorm/blob/main/todo_mssql.md) — the SQL Server
  feature-parity backlog and the provider-consistency audit, including the complex items deliberately
  deferred (`PIVOT`/`UNPIVOT`, temporal tables, correlated `APPLY`, composable raw SQL).
