# In-memory provider: feature backlog vs EF Core LINQ

> Comparison of the EF Core LINQ operator catalog against what `InMemoryContext` supports today,
> with a feasibility breakdown. Source of truth for the in-memory support table is
> `docs/providers/in-memory.md`.

## Context

The EF Core LINQ catalog referenced here:
`Where`, `Select`, `SelectMany`, `Join`, `GroupJoin`, navigation queries, `GroupBy`/aggregates,
`OrderBy`/`ThenBy`, `Skip`/`Take`, `Distinct`, `Union`/`Concat`/`Intersect`/`Except`,
`Any`/`All`/`Contains`, `First`/`Single`/`Last` (+`OrDefault`), `Include`/`ThenInclude`,
`AsNoTracking`, async materializers, top-level client evaluation.

Note: Microsoft's own InMemory provider supports a **smaller** query set than SQLite because it is not
relational; it is discouraged for testing.

## Already supported

`Where`, `Select`/projections, `Join` (all types, chains up to 8), `OrderBy`/`ThenBy`/`Desc`,
`Skip`/`Take`, `Distinct` (value-equality projections), `Any`, `First`/`Single` (+`OrDefault`),
async `ToList`/`ToAsyncEnumerable`, subquery as `FROM`.

## Easy (isolated, no query-model change)

- `Last`/`LastOrDefault` — no API yet; thin wrapper over the enumerator.
- `ToArray`/`ToDictionary`/`ToHashSet` (+async) — thin wrappers over the `ToList` path.
- `Contains` in `Where` — **already implemented** (`Query/InValues.cs`); covered by
  `InMemoryTests.Contains_ShouldFilterData`.
- Ordering over `IAsyncEnumerable` — **done**: an async source is buffered and ordered via
  `OrderAsyncEnumerable` (`InMemoryDataContext.cs`); covered by
  `InMemoryTests.OrderByOverAsyncSource_ShouldSortData`.

## Medium (query model already exists, only in-memory execution missing)

- `Last`/`LastOrDefault` — **done**: `QueryCommand<TResult>.ForLast()` reverses every `ORDER BY` direction
  and reuses the `First` path, so SQL and in-memory share one implementation. Unordered queries throw.
- Aggregates `Count`/`Sum`/`Min`/`Max`/`Avg`/`Stdev`/`Var` — **done** for buffered sources:
  `InMemoryAggregates.Compute` folds the rows and the aggregate projection is detected in
  `InMemoryDataContext.TryGetAggregate`. Also fixed a latent bug: the `NORM_SQL` `MinMI`/`MaxMI`/
  `AvgMI`/`SumMI` reflection lookups were ambiguous (the `(value, filter)` overloads) and threw
  `TypeInitializationException`, so those four were unusable even on SQL providers. Async sources throw.
- `GroupBy`/`Having` — **done** for buffered sources: `CreateGroupedEnumerator` +
  `InMemoryGroupAggregateVisitor` fold aggregates per group and rewrite `HAVING`/projection.
  Grouped ordering by column index is supported; byte-identical to the SQL suite's grouping tests.
  Async sources, filtered aggregates and grouped ordering by expression throw.
- Set operations `UNION`/`UNION ALL`/`INTERSECT`/`INTERSECT ALL`/`EXCEPT`/`EXCEPT ALL` — **done**:
  `CreateSetOperationEnumerator` + `CombineSet` materialise both operands and combine with SQL value
  semantics (`InMemoryDistinct` comparer policy). Async operands and mixed result types throw.

## Resolved gap

- A subquery source (`ctx.From(cmd)`) executed through a synchronous terminal (`From(cmd).ToList()`) or
  aggregated (`From(cmd).Count()`) is now supported for buffered subqueries: `InMemoryEnumeratorAdapter`
  implements `IEnumerator<TResult>` too, and `CreateEnumeratorAdapter` folds an aggregate over the
  buffered subquery rows. Async subqueries still throw. The SQL suite's `From(cmd).Count()` pattern is
  now mirrored by `InMemoryTests.SetOperation_AsSubquery_Count_ShouldReturnRowCount`.

## Translation on top of an existing primitive

- `SelectMany`/`GroupJoin`/multi-`from` query syntax — **in-memory done, SQL deferred**. The engine
  already exposes APPLY/LATERAL on the SQL providers: `EntityBuilder.CrossApply`/`OuterApply`,
  `JoinType.CrossApply`/`OuterApply`, `ISqlDialect.SupportsApply`/`MakeApply`,
  `SqlBuilder.MakeApplyJoin` (SQL Server `CROSS/OUTER APPLY`, PostgreSQL/MySQL/MariaDB
  `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true`, rejected by SQLite/ClickHouse). The
  correlation machinery also exists — `OuterRefMarker<T>`, `IQueryProvider.OuterReferences`/
  `AddOuterReference`, `CorrelatedQueryExpressionVisitor` — and is used today for correlated
  `EXISTS/IN/ANY/ALL` subqueries.
  Implemented for the in-memory provider: `EntityBuilder.SelectMany` (two overloads) and
  `GroupJoin`, backed by the `LinqSourceExpression` node on `FromExpression`; the SQL providers
  throw `NotSupportedException` at call time. Still missing for SQL is the translation of the
  correlated LINQ lambda to the existing APPLY/LATERAL joins (item (b) below), so
  `CrossApply`/`OuterApply` take a prebuilt `QueryCommand`/`EntityBuilder` rather than a lambda over
  the outer row (`docs/advanced/limitations.md:21`).

## Out of scope by design

- `Include`/`ThenInclude`, navigation queries, relationship fixup — no relationship metadata
  (`docs/advanced/limitations.md:20`).
- `AsNoTracking` — there is no tracking.
- Change tracking, `SaveChanges`, transactions, raw SQL — nextorm is read-only.

## Suggested order

1. Easy items (this file's "Easy" section) — small, isolated, immediately useful.
2. Aggregates, then `GroupBy`/`Having` (largest payoff: covers three catalog rows).
3. Set operations.
4. `SelectMany`/`GroupJoin` — in-memory implementation **done**; SQL translation still open (only a
   correlated applied source needs new authoring API).

## Progress

- [x] ToArray/ToDictionary/ToHashSet (+async) — `QueryCommand.TResult.cs`, `EntityBuilder.cs`
- [x] Contains — already supported, test added
- [x] Ordering over IAsyncEnumerable — `InMemoryDataContext.cs`
- [x] Aggregates (buffered sources) — `InMemoryAggregates.cs` + `NORM_SQL` lookup fix
- [x] GroupBy/Having (buffered sources) — `InMemoryGroupBy.cs`
- [x] Set operations (buffered operands) — `InMemoryDataContext.CreateSetOperationEnumerator`
- [x] Last/LastOrDefault (ordered queries; reverse `ORDER BY` + `First`)
- [x] Sync/aggregate over a buffered subquery source (`From(cmd)`) — sync `InMemoryEnumeratorAdapter`
      + `From(sub).Count()` folds the subquery. Async subqueries throw.
- [x] SelectMany/GroupJoin (in-memory) — `EntityBuilder.SelectMany`/`GroupJoin`,
      `LinqSourceExpression` + `FromExpression.LinqSource`, execution in
      `InMemoryDataContext.BuildLinqSourceDelegate`/`ApplySelectMany`/`ApplyGroupJoin`; SQL providers
      throw `NotSupportedException`. Tests: `InMemorySelectManyTests`, plus the two SQL-rejection
      tests in `test/nextorm.sqlite.tests/SqlGenerationTests.cs`. SQL translation still open (the
      APPLY/LATERAL primitive exists; only a correlated applied source needs new public authoring API,
      `docs/advanced/limitations.md:21`).

## Benchmarks

Added `InMemoryBenchmarkAggregates`, `InMemoryBenchmarkGroupBy`, `InMemoryBenchmarkSetOperations` and
`InMemoryBenchmarkMaterializers` (category `InMemoryNew`; run with
`dotnet run -c Release -- --anyCategories InMemoryNew`). Results and analysis are in
`benchmark-report.md` ("In-memory: новый функционал"). Summary:

- Aggregates: 14–36× faster than EF Core InMemory, 2.6–5.2× slower than raw LINQ (per-call command
  preparation dominates; the fold itself is now unboxed — `Sum` allocations dropped 59 MB → 0.32 MB).
- GroupBy: 2.0× faster than EF InMemory, 4.6× slower than LINQ.
- Set operations: 4.6–16.6× faster than EF InMemory, 1.5–3× slower than LINQ.
- Materializers: `ToArray`/`ToDictionary` 2.5–32× faster than EF InMemory; `Last` is slow because
  in-memory ordering still uses an object (boxing) key — documented as a follow-up.

## Coverage

Full local run of all 8 test projects (Postgres + SQL Server via the Podman socket, see `AGENTS.md`):
**83.4% line, 76.7% branch** across the 4 report assemblies — above the enforced
`MIN_LINE_COVERAGE=75` (`.github/workflows/dotnet.yml`) and in line with the pre-existing 84%.
Assembly split: core 83.3%, postgres 81.5%, sqlite 88.6%, sqlserver 90.6%.
