# nextorm vs linq2db: functionality comparison

> A side-by-side functional comparison of nextorm and linq2db. It complements the
> [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) (which also covers EF Core) and is
> based on the current `1.0.3-alpha` tree, including the `APPLY`/`LATERAL` and query-hint additions.

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
| `INNER` / `LEFT` / `RIGHT` / `FULL` / `CROSS JOIN` | yes | yes | `SqlBuilder.MakeJoin`, `EntityBuilder.Join/LeftJoin/RightJoin/FullJoin/CrossJoin` |
| `APPLY` / `LATERAL` | yes | **yes** (including correlated sources) | `JoinType.CrossApply/OuterApply`, `SqlBuilder.MakeApplyJoin`, `ISqlDialect.MakeApply`; correlated via a lambda over the left-hand row |
| Join arity | unlimited | 2–8 (compile-time cap) | `Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>` |
| Subqueries (`FROM`, scalar, correlated `EXISTS/IN/ANY/ALL`) | yes | yes (one level; deeper nesting and the in-memory provider throw `NotSupportedException`) | `CorrelatedQueryExpressionVisitor.cs`, `SqlFunctions.Sql` |
| `IN` over a list/array | yes | yes | `Query/InValues.cs`, `Visitors/InValuesTranslator.cs` |
| `GROUP BY` / `HAVING` / aggregates | yes | yes | `EntityBuilder.GroupBy/Having`, `BaseExpressionVisitor.cs` |
| `ORDER BY` / paging (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | `SqlBuilder.MakeSelect`, dialect `MakePage`/`MakeTop` |
| `UNION` / `UNION ALL` | yes | yes | `QueryCommand<TResult>.Union/UnionAll` |
| `INTERSECT` / `EXCEPT` (+ `ALL` where the engine has it) | yes | yes (provider dependent) | `UnionType`, `ISqlDialect.SupportsIntersectExceptAll` |
| `SELECT DISTINCT` | yes | yes | `QueryCommand<TResult>.Distinct`, `QueryCommand.IsDistinct` |
| CTEs (including recursive) | yes | yes | `Builders/CteQuery.cs`, `DataContext.DataContextExtensions.With/WithRecursive` |
| Window functions (`OVER`, ranking, framed aggregates, `lag`/`lead`) | yes | yes | `Visitors/WindowFunctionTranslator.cs`, `SqlFunctions.Sql` |
| `CASE WHEN` / ternary / `switch`, `COALESCE`, numeric `CAST` | yes | yes | `BaseExpressionVisitor.cs` |
| String / math / date scalar functions, `LIKE` | yes | yes | `Visitors/ScalarFunctionTranslator.cs`, dialect `Make*` hooks |
| User-defined scalar functions | yes (`DbFunction` / `Sql.Ext`) | yes (`[SqlFunction]`) | `SqlFunctionAttribute.cs` |
| Table-valued functions | yes (`TableFunction`) | yes (`[SqlTableFunction]`) | `SqlTableFunctionAttribute.cs`, `DataContextExtensions.FromTableFunction` |
| Raw SQL (whole query) | yes | yes | `WithSql` / `PrepareFromSql` |
| Raw SQL as a composable source/subquery | yes | **no** | — |
| Query hints | yes (provider specific) | **partial** — SQL Server `OPTION (...)` only | `QueryCommand<TResult>.Hint`, `ISqlDialect.SupportsQueryHints`/`RenderQueryHints` |
| Table hints (e.g. `WITH (NOLOCK)`) | yes | **no** | — |
| **DML** (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) | yes | **no** (read-only by design) | — |
| Bulk copy / merge / temporary tables | yes | **no** | — |
| Navigation properties / associations / eager loading | yes (`[Association]`, `LoadWith`) | **no** | — |
| Change tracking / identity map | partial | **no** (by design) | — |
| Extensibility (interceptors, custom SQL, query filters) | extensive | minimal (dialect + `[SqlFunction]`/`[SqlTableFunction]`) | `SqlDialectBase` |
| Providers | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | `src/nextorm.*` |
| EF Core integration | yes (`linq2db.EntityFrameworkCore`) | no | — |
| Performance posture | high | benchmarked at/above Dapper/EF Core on the shipped scenarios | `docs/specs/performance/benchmark-report.md` |

## What nextorm does well

* The complete analytic query surface: every join type including `APPLY`/`LATERAL` (including correlated sources),
  set operations, `DISTINCT`, CTEs (recursive), window functions, `CASE`/`COALESCE`/`CAST`, string/math/
  date functions, `IN`-lists, UDF/TVF mapping and raw SQL for a whole query.
* Provider-portable rendering: the same C# renders `CROSS APPLY` on SQL Server and
  `CROSS JOIN LATERAL` on PostgreSQL/MySQL/MariaDB, driven by `ISqlDialect` capabilities.
* Two reuse paths (implicit plan cache and explicit `Prepare()`), query parametrisation and a
  benchmarked low-allocation design.
* SQL Server statement-level hints with plan-key participation (`Hint(...)`), coalescing cleanly with
  the CTE `option (maxrecursion n)` clause.

## Where linq2db is stronger

* **Data modification**: `INSERT`/`UPDATE`/`DELETE`/`MERGE`, bulk copy, temporary tables — entirely
  absent from nextorm by design.
* **Relationships**: `[Association]`, `LoadWith` eager loading and implicit join inference.
* **Correlation**: nextorm supports correlated scalar subqueries, correlated
  `EXISTS`/`IN`/`ANY`/`ALL` and correlated `APPLY`/`LATERAL` sources on the SQL providers (any
  nesting depth); the in-memory provider has no per-row outer-row binding (see
  [`sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) §4).
* **Query/table hints** across providers, plus query filters, interceptors and other extensibility.
* **Provider breadth**: Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE and more.
* **EF Core integration** and a larger ecosystem.

## Architecture differences

| Aspect | linq2db | nextorm |
|---|---|---|
| Model | Explicit CRUD ORM with associations; no automatic change tracking | Read-only query builder and mapper |
| Entity requirement | Mapping via attributes/fluent/inference | Entity class optional; `From("table")` with `TableAlias` |
| Reuse | Compiled queries, query cache | Implicit plan cache and `Prepare()` |
| Extensibility | Interceptors, custom SQL, provider extensions | Dialect contract and function attributes |

## Summary

If the requirement is *read and report over an existing schema* with a small, fast, provider-portable
mapper, nextorm now covers essentially the whole analytic query surface that linq2db offers. The
remaining functional delta is deliberate: DML, relationships, correlated lateral sources, table hints,
broader provider coverage and the larger extensibility/ecosystem surface. Conversely, linq2db is the
better fit when the same layer must also write data and model relationships.

## See also

- [SQL capabilities gap analysis](../roadmap/sql-capabilities-gap-analysis.md) — nextorm vs EF Core and linq2db, per construct.
- [Limitations and out-of-scope features](../../advanced/limitations.md)
- [Joins](../../guide/03-joins.md) — `CrossApply`/`OuterApply`.
- [Query hints](../../guide/17-query-hints.md)
- [Provider overview](../../providers/overview.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.*/**`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
`docs/specs/performance/benchmark-report.md`. linq2db capabilities are described from its public documentation.
