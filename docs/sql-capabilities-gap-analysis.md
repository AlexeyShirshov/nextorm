# SQL query-building capabilities: nextorm vs EF Core and linq2db

This document compares the SQL-query-building surface of nextorm with EF Core and linq2db, lists what
nextorm supports today, and enumerates the gaps. It is the reference for the implementation workstreams
tracked in [Implementation plan](#5-implementation-plan).

The analysis is based on the current `1.0.3-alpha` tree: `SqlBuilder`, `QueryCommand`,
`BaseExpressionVisitor`, `CorrelatedQueryExpressionVisitor`, the join builders, the `ISqlDialect`
contract and the provider dialects, plus the integration tests under `test/nextorm.integration.tests`.

> **Status (updated).** Sections 1–4 below are the original gap analysis and are kept as the baseline.
> The implementation status is now:
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
> | 8 | `INTERSECT` / `EXCEPT` (+ `ALL` where supported) | **Done** (+ optimized) |
> | 9 | CTEs (`WITH`, recursive) | **Done** |
> | 10 | Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`) | **Done** |
> | 11 | User-defined scalar-valued functions (`[SqlFunction]`) | **Done** |
> | 12 | Table-valued functions (`[SqlTableFunction]`) | **Done** |
> | 13 | Navigation properties / relationships | **Out of scope** |
> | 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | **Out of scope** |
> | 15 | `APPLY` / `LATERAL` (`CrossApply`/`OuterApply`) | **Partial** — SQL Server `CROSS/OUTER APPLY`, PostgreSQL/MySQL/MariaDB `LATERAL`; the applied source cannot be correlated yet (no public outer-reference API for a `FROM` subquery) |
> | 16 | Statement-level query hints (`Hint(...)`) | **Done on SQL Server** (`OPTION (...)`); other dialects reject hints with `NotSupportedException` |
>
> Test coverage after the work is 81.9% line (CI threshold 75%). Benchmarks and the performance
> optimizations that followed are documented in [Iteration 6 of `benchmark-report.md`](https://github.com/AlexeyShirshov/nextorm/blob/main/benchmark-report.md):
> prepared nextorm wins every new feature against compiled EF Core/linq2db/Dapper, and the warm-path
> losses on IN-list (7–8×), `INTERSECT`/`EXCEPT` (~8×) and recursive CTE (~7×) were eliminated.
>
> Consequently the matrix in section 2 and the gap list in section 4 describe the **pre-implementation**
> state — a few nextorm entries are now stale (notably join arity is 8, and `LEFT`/`RIGHT`/`FULL`/`CROSS`,
> `CASE WHEN`, string/math functions, `IN`-lists, `DISTINCT`, `INTERSECT`/`EXCEPT`, CTEs, window
> functions and UDF/TVF support all exist; `APPLY`/`LATERAL` exists for non-correlated sources; and
> SQL Server statement-level query hints exist).

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
* CTEs, window functions;
* DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`);
* navigation properties / relationship metadata;
* raw SQL.

---

## 2. Capability matrix

Legend: **yes** = first-class support; **partial** = supported with limits; **no** = not supported.

| SQL construct | EF Core | linq2db | nextorm | Evidence in nextorm |
|---|---|---|---|---|
| INNER JOIN | yes | yes | **yes** | `SqlBuilder.cs:253` |
| LEFT JOIN | yes (`GroupJoin`+`DefaultIfEmpty`) | yes (`LeftJoin`) | **no** | `SqlBuilder.cs:257` throws `NotImplementedException` |
| RIGHT JOIN | no (workaround) | yes | **no** | defined in `JoinType` (`JoinExpression.cs:5`) but never emitted |
| FULL JOIN | no (workaround) | yes | **no** | same as above |
| CROSS JOIN | yes (`SelectMany`) | yes | **no** | same as above |
| APPLY / LATERAL | partial | yes | **partial** — non-correlated only | `CrossApply`/`OuterApply`, `JoinType.CrossApply/OuterApply`, `ISqlDialect.SupportsApply`/`MakeApply`; correlation not expressible (no public outer-reference API for a `FROM` subquery) |
| Query hints | yes | yes (provider specific) | **partial** — SQL Server only | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| JOIN to a derived table (subquery) | yes | yes | **yes** | `EntityBuilder.cs:176`, `SqlBuilder.MakeFrom:403` |
| More than two joined tables | unlimited | unlimited | **partial — max 3** | `Projection.cs:24,40`; `EntityP3` has no `Join` |
| Subquery in `FROM` | yes | yes | **yes** | `SqlBuilder.cs:372`, `FromExpression.cs:10` |
| Scalar subquery in `SELECT`/`WHERE`/`ORDER BY` | yes | yes | **yes** (non-correlated) | `CommonTestSuite.SqlCommand.cs:612-660` |
| Correlated subquery | yes | yes | **partial** — `EXISTS`/`IN`/`ANY`/`ALL` only | `CorrelatedQueryExpressionVisitor.cs`; `CommonTestSuite.CorrelatedQuery.cs` |
| `IN` (subquery) | yes | yes | **yes** | `NORM.cs:34`, `BaseExpressionVisitor.cs:253` |
| `IN` (list/array/`Contains`) | yes | yes | **no** | `@in` only accepts a `QueryCommand` |
| `EXISTS` / `ANY` / `ALL` | yes | yes | **yes** | `NORM.cs:33-36`, `BaseExpressionVisitor.cs:172-251` |
| `WHERE` (and/or/not, comparisons) | yes | yes | **partial** — logical `!` not translated | `EntityBuilder.Where:88`; `VisitUnary` handles numeric casts only |
| `GROUP BY` | yes | yes | **yes** | `EntityBuilder.GroupBy:187`, `SqlBuilder.cs:88` |
| `HAVING` | yes | yes | **yes** | `EntityBuilder.Having:195`, `SqlBuilder.cs:117` |
| Aggregates (`count`/`min`/`max`/`avg`/`sum`/`stdev`/`var`, distinct) | yes | yes | **yes** | `NORM.cs:37-54`, `BaseExpressionVisitor.cs:299-391` |
| `SELECT DISTINCT` | yes | yes | **no** | no API; only `count_distinct` |
| `ORDER BY` (expression/ordinal, asc/desc, multiple) | yes | yes | **yes** | `EntityBuilder.cs:349-376`, `SqlBuilder.cs:141` |
| `LIMIT`/`OFFSET`/`TOP` | yes | yes | **yes** | dialect `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | **yes** | `QueryCommand.cs:1034-1047`, `SqlBuilder.cs:124` |
| `INTERSECT` / `EXCEPT` | yes | yes | **no** | absent |
| CTE (`WITH`), recursive CTE | yes | yes | **no** | absent |
| Window functions (`OVER`, `ROW_NUMBER`, ...) | yes | yes | **no** | absent |
| `CASE WHEN` / ternary `?:` / `switch` | yes | yes | **no** | `BaseExpressionVisitor.cs:915-918` throws |
| `COALESCE` (`??`) | yes | yes | **yes** | `BaseExpressionVisitor.cs:897-913` |
| `CAST` (numeric) | yes | yes | **yes** | `BaseExpressionVisitor.cs:63-77, 860-878` |
| `LIKE` / string methods (`Contains`, `StartsWith`, `ToUpper`, `Substring`, `Trim`) | yes | yes | **no** | `BaseExpressionVisitor.cs:399-429`: only `string.Concat`, otherwise throws |
| Math functions (`Math.*`) | yes | yes | **no** | no translation table |
| Date/time functions (`DATEPART`, ...) | yes | yes | **no** | no translation table |
| User-defined scalar-valued functions | yes (`DbFunction`) | yes (`Sql.Ext`/custom) | **no** | only hard-coded `NORM.SQL` |
| Table-valued functions | yes (TVF mapping) | yes (`TableFunction`) | **no** | only physical tables and subqueries |
| Navigation properties (implicit joins) | yes | yes | **no** | explicit joins only; no relationship metadata |
| DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | yes | **no** | read-only provider |
| Raw SQL (whole query) | yes (`FromSql`) | yes | **yes** | `PrepareFromSql`/`WithSql` in `EntityExtensions.cs` |
| Raw SQL as a composable source/subquery | yes | yes | **no** | `WithSql` replaces the whole query |

---

## 3. What nextorm does well

These are fully implemented and covered by SQL-generation or integration tests:

* **Projection** — anonymous types, DTOs, records, tuples, member init, primitive/scalar projections.
* **Predicates** — equality/inequality (including `IS NULL` / `IS NOT NULL` special-casing in
  `WhereExpressionVisitor.cs:16-48`), relational comparisons, `and`/`or`, arithmetic, bitwise and shift
  operators (with bracketing).
* **`COALESCE`** and numeric **`CAST`**.
* **`GROUP BY` + `HAVING` + aggregates**, including `count`, `count_big`, `count_distinct`, `min`, `max`,
  `avg`, `sum`, `stdev`, `stdevp`, `var`, `varp` and the `_distinct` variants.
* **`ORDER BY`** by expression and by projected column ordinal, ascending/descending, chainable.
* **Paging** — dialect-specific `TOP`, `LIMIT`/`OFFSET` and `OFFSET ... FETCH`, including the SQL Server
  requirement to inject an `ORDER BY`.
* **`UNION` / `UNION ALL`**.
* **Provider-specific scalar/aggregate functions** gated by capability flags — for example ClickHouse
  `dateTrunc`, `addDays`/.../`toLastDayOfMonth`, `arrayStringConcat(groupArray(...))`,
  `groupBitAnd`/`groupBitOr`/`groupBitXor`, `corr`/`covarPop`/`covarSamp`, `argMin`/`argMax` and the
  `-If` combinators (`count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`).
* **Subqueries** in `FROM`, scalar subqueries in `SELECT`/`WHERE`/`ORDER BY`, and correlated
  `EXISTS`/`IN`/`ANY`/`ALL`.
* **Derived-table joins** (`Join(QueryCommand<T>)`).
* **Raw SQL** for a whole query (`PrepareFromSql` / `WithSql`) with named parameters.

---

## 4. Gaps, in order of significance

1. **Only `INNER JOIN` is emitted.** `JoinType` already declares `Left`, `Right`, `Full`, `Cross` and
   `FullCross` (`JoinExpression.cs:5-13`), but `SqlBuilder.MakeJoin` throws `NotImplementedException` for
   everything except `Inner` (`SqlBuilder.cs:252-259`), and the fluent API always creates an inner join
   (`EntityBuilder.cs:165,176,585,590`). The in-memory provider has the same limitation
   (`InMemoryDataContext.cs:213/247`). This is the largest gap.
2. **No `APPLY`/`LATERAL`**, and no `RIGHT`/`FULL`/`CROSS` join surface — a direct consequence of (1).
3. **No `SELECT DISTINCT`, `INTERSECT`, `EXCEPT`, CTEs or window functions.**
4. **No `CASE WHEN`/ternary**, although `COALESCE` works. `Conditional` and `Switch` are explicitly
   rejected (`BaseExpressionVisitor.cs:915-918`).
5. **No string or math/date function translation** (`LIKE`, `Contains`, `StartsWith`, `Math.*`,
   `DATEPART`, ...). The string branch only knows `string.Concat` and otherwise throws
   (`BaseExpressionVisitor.cs:399-429`).
6. **No user-defined scalar-valued or table-valued function mapping** (EF `DbFunction`, linq2db
   `TableFunction`); only the built-in `NORM.SQL` set is available.
7. **No `IN` over a list/array.**
8. **Maximum of three tables in a join** (`Projection<T1,T2>` / `Projection<T1,T2,T3>`); no 4+ arity and no
   recursive extension.
9. **No DML and no navigation properties / relationship metadata** — by design for a read-only,
   no-change-tracking mapper, but still a functional gap versus both references.
10. **Correlated scalar subqueries** are only exercised through `EXISTS`/`IN`/`ANY`/`ALL`; the
    `OuterRefMarker` mechanism exists but a general correlated scalar projection is not covered.
11. **Logical negation (`!`)** is not translated: `VisitUnary` only handles numeric conversions
    (`BaseExpressionVisitor.cs:860-878`).

---

## 5. Implementation plan

Each workstream is independent enough to be implemented on its own branch/PR. Dependencies are noted; a
workstream must not be started before its dependencies land, and workstreams that touch the same files
(`SqlBuilder.cs`, `BaseExpressionVisitor.cs`, `ISqlDialect`/`SqlDialectBase`, `EntityBuilder.cs`) must not be
developed in parallel on the same working tree.

| # | Workstream | Primary files | Depends on | Verification |
|---|---|---|---|---|
| 1 | Join types: `LEFT`/`RIGHT`/`FULL`/`CROSS` (+ fluent API, in-memory, dialects) | `SqlBuilder.cs`, `JoinExpression.cs`, `EntityBuilder.cs`, `JoinCommandBuilder.cs`, `Projection.cs`, `InMemoryDataContext.cs`, dialects | — | `CommonTestSuite.Join.cs`, `SqlGenerationTests.cs` |
| 2 | Join arity > 3 (open-ended projection) | `Projection.cs`, `JoinCommandBuilder.cs`, `EntityBuilder.cs`, `SqlBuilder.cs` | 1 | join tests, SQL-generation tests |
| 3 | `CASE WHEN` / ternary / `switch` | `BaseExpressionVisitor.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | — | new SQL-generation tests |
| 4 | String, math and date scalar functions + `LIKE` | `BaseExpressionVisitor.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, dialects | 3 (shares the function dispatch) | new SQL-generation tests per provider |
| 5 | `IN` over a list/array | `NORM.cs`, `BaseExpressionVisitor.cs`, dialects | — | new integration tests |
| 6 | Logical `!` and unary operators | `BaseExpressionVisitor.cs`, `WhereExpressionVisitor.cs` | 3 | new SQL-generation tests |
| 7 | `SELECT DISTINCT` | `QueryCommand.cs`, `SqlBuilder.cs`, `EntityBuilder.cs` | — | integration tests |
| 8 | `INTERSECT` / `EXCEPT` | `QueryCommand.cs`, `UnionType.cs`, `SqlBuilder.cs` | 7 | integration tests |
| 9 | CTEs (`WITH`, recursive) | `QueryCommand.cs`, `SqlBuilder.cs`, new builder API | 8 | integration tests |
| 10 | Window functions (`OVER`, ranking, framed) | `NORM.cs`, `BaseExpressionVisitor.cs`, dialects | 3, 4 | new integration tests |
| 11 | User-defined scalar-valued functions | `NORM.cs`, `ISqlDialect.cs`, `BaseExpressionVisitor.cs` | 3, 4 | new tests |
| 12 | Table-valued functions | new builder + `ISqlDialect.cs`, `SqlBuilder.cs` | 1 | new tests |
| 13 | Navigation properties / relationships | metadata (`Meta/`), `EntityBuilder.cs`, `SqlBuilder.cs` | 1 | new tests |
| 14 | DML (`INSERT`/`UPDATE`/`DELETE`) | new subsystem + provider `DbCommand` layer | — | new integration tests |

### Cross-cutting requirements

* Preserve the public API compatibility rules in the `api-design` skill: extend-only, no breaking changes
  to existing signatures.
* Every workstream adds tests before it is considered done. SQL-generation tests
  (`test/nextorm.*.tests/SqlGenerationTests.cs`) do not require a database; integration tests for joins
  and grouping run against SQLite by default and against SQL Server/PostgreSQL via Testcontainers.
* Keep the existing performance characteristics in mind: new SQL must be built with the same pooled
  `StringBuilder` pattern and must not allocate on the hot path more than necessary.
* Line endings are CRLF (`AGENTS.md`); normalize new and edited files with
  `perl -pi -e 's/\r?\n/\r\n/g' <file>`.

---

## 6. Verifying a workstream

```bash
# Build the solution.
dotnet build nextorm.sln

# Provider SQL-generation tests (no database required).
dotnet test test/nextorm.sqlite.tests
dotnet test test/nextorm.sqlserver.tests
dotnet test test/nextorm.postgres.tests
dotnet test test/nextorm.mysql.tests
dotnet test test/nextorm.mariadb.tests
dotnet test test/nextorm.clickhouse.tests

# Integration tests (SQLite runs locally; SQL Server/PostgreSQL need Docker/Testcontainers).
dotnet test test/nextorm.integration.tests
```

---

## See also

- [nextorm vs linq2db: functionality comparison](linq2db-comparison.md) — a focused side-by-side of the
  two libraries, including the `APPLY`/`LATERAL` and query-hint status.
