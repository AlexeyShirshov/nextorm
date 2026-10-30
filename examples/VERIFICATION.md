# Examples verification report

> **Last verified: 2026-10-30** (the examples are now **comparison projects**: every query has a
> nextorm arm and a linq2db arm, and both are executed against the same dataset; BenchmarkDotNet runs
> through `--benchmark`). Re-run the examples whenever the engine gains features and update the tables
> below. This file is the source of truth for "what works / what does not and why"; the topics are owned
> by the roadmap backlog under [`docs/specs/roadmap/`](../docs/specs/roadmap) and
> [`docs/specs/`](../docs/specs). Run mechanics live in
> [`.opencode/skills/running-examples/SKILL.md`](../.opencode/skills/running-examples/SKILL.md).
>
> **The numbers below are from 2026-10-30**, observed after the four remaining anonymous chains were
> converted to named projections and aliased joins (#231) and `NextOrmVersion` was bumped to
> `1.0.9-rc4`, the release that ships the fix for the CTE-alias defect those chains exposed (#350).
> The mssql example still builds the `ProjectReference` to `src/` (#232); postgres and clickhouse use
> the published rc4 packages.

## Environment

Testcontainers + Podman (Windows Podman machine, reached from WSL through
`unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`). Datasets download once into
`~/.cache/nextorm/{aviasales,adventureworks,clickhouse}/` and are reused.

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/<name> -c Debug
```

Each `Program` runs both arms (nextorm and linq2db) for every query to completion: it prints the rows of
each arm, `[ OK ]`/`[FAIL]`/`[DIFF]` per query and an `n/m queries matched in both arms` summary. Only
documented gaps (`NotSupportedException`, `QueryPreparationException`, `LinqToDBException`) are recorded
as `FAIL`; any other exception aborts the run.

## Results

| Example | Matched in both arms | Failing query (documented gap) |
|---|---|---|
| `nextorm.examples.postgres.aviasales` | **11/11** | — |
| `nextorm.examples.mssql.adventureworks` | **11/11** | — |
| `nextorm.examples.clickhouse.analytics` | **9/11** (10 of 11 executed; `Retention` excluded) | `Incremental` (nextorm arm only) and `Retention` — the rc4 tuple-array cast ([#359](https://github.com/AlexeyShirshov/nextorm/issues/359)) |

Query-by-query counts on 2026-10-30 (warm volumes, Podman):

* postgres: aircraft delay chains 30, occupancy matrix 8, no-show 0, rolling revenue 91, ABC/XYZ 614,
  top routes 186, airport OTP 63, percentiles 8, frequent flyers 10, passenger growth 2, cancellations 15
  — identical in both arms. `AircraftDelayChains` is the query the named-alias conversion rewrote and
  the one #350 broke; it now returns 30 rows again.
* mssql: 146, 364, 0, 266, 4, 12, 40, 20, 14, 364, 40 — identical in both arms.
* clickhouse: array analytics 10, funnel 3, sessions 100, daily traffic 7, top landing pages 10, device
  split 2, top referrers 10, session depth 5, rolling activity 7; `Incremental` returns 7 rows on the
  linq2db arm and throws on the nextorm arm; `Retention` did not execute (#359).

## Failures and their tracked cause

| Query | Why it fails | Tracked in |
|---|---|---|
| CH `Retention` | rc4 wraps every array result in `cast(… as Array(...))`; for a tuple element the type renderer falls back to the CLR name including its arity, so the SQL says ``Array(Nullable(Tuple`2))`` and ClickHouse rejects it (`Code: 62. Back quoted string is not closed`). The alias conversion is not involved — the positional form emits the same cast. Not present in rc3, which emitted the bare `groupArray(...)`. | [#359](https://github.com/AlexeyShirshov/nextorm/issues/359) |
| CH `Incremental` | nextorm: `uniqMerge`, the `-Merge` combinator over an `AggregateFunction(uniq, …)` state column, has no LINQ surface in the released packages (the state cannot be projected as a scalar). The linq2db arm runs the query through `FromSql` and returns 7 rows, so the gap is visible rather than assumed. | [`todo_clickhouse_aggregate_function_state.md`](../docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md) |

MSSQL `QuarterlyPivot` is no longer a failure: the native `PIVOT` now accepts a derived query, so the
reference `WITH OrderMargins AS (<5-table join>) ... PIVOT (...)` is expressed directly.

## Actuality changes made on this verification

* **Re-verified against the `1.0.9-rc4` packages (2026-10-30).** `NextOrmVersion` bumped
  `1.0.9-rc3 → 1.0.9-rc4` (mssql keeps the `ProjectReference`). rc4 ships the CTE-alias fix (#350,
  T363) and the aliased **source-less** join form (#322), so the four chains whose joins still
  projected anonymously (#231) were converted: `AircraftDelayChains` and `BusinessOccupancyMatrix`
  (postgres), `SupplyChain` (mssql) and `Retention` (clickhouse) project their aliased CTEs into named
  row types (`DelayChainRow`/`FlightOccupancyRow`/`BusinessCapacityRow`, `SupplierDelayRow`/
  `ProductionImpactRow`, `RetentionWeekRow`/`CohortSizeRow`) and name every slot
  (`WithAlias(Alias.<Root>)` + `Alias.<Name>`), reading `p.<Slot>` instead of `p.ItemN`. Joined
  entities use the source-less form (`.Join<T>((l, r) => …, Alias.X)`); a joined CTE passes its
  descriptor (`.Join(cte, …, Alias.X)`). No anonymous root or anonymous aliased CTE is left in those
  chains. The `route_monthly_revenue` join chain of postgres query 5 is named too
  (`Alias.Timetable` + `Alias.Departure`/`Arrival`/`Segment`/`Ticket`): its root is a mapped table, so
  it needs no DTO, and only `p.ItemN` → `p.<Slot>` changed. **Every join in all three examples is
  named**: each chain roots at `WithAlias(Alias.<Root>)` and passes `Alias.<Name>` per join, so no
  `p.ItemN` is left in a query body. Every reference `GROUP BY` whose keys are exactly the projection's
  non-aggregate outputs is written as `SelectGroup`, so the keys are inferred instead of repeated in a
  `GroupBy` + `Select` pair and the rendered SQL is unchanged. Four sites keep the explicit pair:
  postgres `BusinessOccupancyMatrix` (its `DayOfWeek` projection would become a fourth key) and
  clickhouse `Retention.first_visits` (its key would be the `UserId` cast), both documented at the call
  site, plus two for generator reasons — mssql `VipChurn`'s `CustomerMetrics` and clickhouse
  `Retention.weekly`: the generator's chain recovery does not yet recognise `SelectGroup`, so a
  `SelectGroup` producer would drop the aliases of the chain that consumes it.
  Results: postgres **11/11** — `AircraftDelayChains` returns 30 rows in both arms now that #350 is
  fixed (it was 10/11 on the T361 candidate); mssql **11/11**; clickhouse **9/10 executed**, with
  `Incremental` as the only failing query and `Retention` excluded (below).
* **New rc4 defect found by the converted clickhouse chain: the tuple-array cast (#359).** `Retention`
  projects `group_array(Tuple.Create(...))`; rc4 wraps array results in `cast(… as Array(...))` and the
  element renderer emits the **CLR** name for a tuple, including its arity:
  ``cast(… as Array(Nullable(Tuple`2)))``. The backtick opens a quoted identifier, so the server rejects
  the statement with `Code: 62. Back quoted string is not closed`, and the run aborts (it is not one of
  the documented gap exception types). This is unrelated to the alias conversion — the aliased SQL is
  otherwise identical to the positional form — and it did not exist in rc3, which emitted the bare
  `groupArray(...)` without a cast. Tracked in
  [#359](https://github.com/AlexeyShirshov/nextorm/issues/359); `Retention` is excluded from the rc4
  run via `--only` and is expected back at `[ OK ]` on the package that carries the fix.

* **AdventureWorks consumes the current checkout instead of a published package (#232).**
  `nextorm.examples.mssql.adventureworks` replaces its `nextorm`/`nextorm.sqlserver`
  `PackageReference`s with `ProjectReference`s to `src/nextorm.core` and `src/nextorm.sqlserver`, so it
  can build the typed `Pivot`/`Unpivot` example against the working tree (an explicit exception to the
  postgres/clickhouse published-package model — see [`README.md`](README.md)). Run and build commands
  are unchanged.

* **Examples moved to the published packages.** The three projects now reference the `nextorm.*` NuGet
  packages (`NextOrmVersion` in [`Directory.Packages.props`](../Directory.Packages.props), `1.0.9-rc3`
  at the time of that switch) instead of `src/`, so they build against what ships on nuget.org. Re-run
  against Testcontainers after the switch: postgres `11/11`, mssql `11/11`, clickhouse `10/11`
  (`Incremental` only).
* **Databases are cached in named Docker volumes (the examples are read-only).** Each `DemoDatabase`
  mounts a named volume at the provider's data directory and only copies the dataset + loads when a
  row-count probe finds no data, so warm runs skip the copy and the multi-minute load and merely start
  a container against the existing volume. Measured on Podman: mssql cold `31 s` → warm `12 s`,
  postgres cold `2:34` → warm `1:31` (query time dominates), clickhouse cold `2:54` → warm `28 s`.
  `NEXTORM_EXAMPLES_RELOAD=true` deletes the volume first for a cold rebuild. Volumes:
  `nextorm-examples-{aviasales,adventureworks,clickhouse}-data`.
* **CH `ArrayAnalytics` and `Retention` are now `WORKING` (ClickHouse 8/11 → 10/11).** The stale
  "higher-order lambdas / `groupArray` have no LINQ surface" notes were wrong for the 1.0.4 surface:
  `ArrayAnalytics` uses `split_by_char` → `array_map` → `array_filter` grouped by the resulting array, and
  `Retention` uses `ctx.With(...)` CTEs + `group_array(Tuple.Create(...))`. `length(x)` inside the
  higher-order lambda is written as `x.ToLower().Length` because member access on the lambda parameter
  (`x.Length`) is rejected by the translator. A full `hits_v1` run against the 1.0.4 packages completed
  with **10/11** — `[ OK ]` for `ArrayAnalytics` and `Retention` (plus all six course exercises), the only
  `FAIL` being `Incremental`.
* **CH `Incremental` remains the only `FAIL`**: `uniqMerge` (the `-Merge`/`-State` combinator) has no LINQ
  surface in the released packages.
* **CH `Sessions`** now uses the native `SqlFunctions.ClickHouse.lag_in_frame(...)` (was emulated with
  `Sql.lag`). `runningAccumulate(...)` is still emulated by a framed `sum_over(...)`.
* **CH `SessionDepth`** now uses `SqlFunctions.ClickHouse.multi_if(when(...), …, otherwise(...))` in
  `GROUP BY`/`ORDER BY`/`SELECT` (was a nested ternary).
* **MSSQL `QuarterlyPivot`** was the documented derived-source blocker; it now fits the native `Pivot`
  over a derived query (see the next bullet). Its message/comment no longer reports a gap.
* Docs synced: `README.md` and the per-provider READMEs.
* **Re-verified after correlated subqueries were completed** (depth ≥ 2, aggregate terminals, `HAVING`,
  function over an outer column) **and correlated `CROSS`/`OUTER APPLY`/`LATERAL` landed**: results are
  unchanged (11/11, 10/11, 8/11). No example query overlaps those features — the remaining gaps are
  ClickHouse arrays / `AggregateFunction` states / `groupArray` and MSSQL `PIVOT` over a derived source,
  none of which correlation or `APPLY` addresses. Query comments and failure messages stay accurate.
* **Re-verified after a derived query could become the primary `FROM` source of a join** (`ctx.From(derivedQuery).Join(...)`, `ResolveJoinBase`): results were unchanged (11/11, 10/11, 8/11). No example query uses derived-primary joins; the `QuarterlyPivot` blocker stayed open then and is now resolved (next bullet). The stale `todo_mssql_derived_from_join.md` link was repointed to `todo_mssql_pivot_derived.md` after it was retired.
* **Re-verified after `PIVOT`/`UNPIVOT` accepted a derived source** (`ResolvePivotInner`, `ctx.From(derivedQuery).Pivot(...)`): MSSQL moved **10/11 → 11/11** — `QuarterlyPivot` now pivots a 5-table join through a derived query (numeric `FOR QuarterNum IN (1,2,3,4)`; the string form needs an explicit T-SQL `CAST`). The other two examples are unchanged. The work plan `docs/specs/roadmap/todo_mssql_pivot_derived.md` was folded into the docs and retired.

* **Re-verified against the `1.0.9-rc2` packages (2026-10-13).** `NextOrmVersion` bumped `1.0.8-b →
  1.0.9-rc2`; results unchanged: postgres `11/11`, mssql `11/11`, clickhouse `10/11` (`Incremental`
  only). One example fix was required: the MSSQL `AdventureWorksQueries.cs` needed
  `using NextORM.SqlServer;`, because the provider-specific fluent extensions were relocated out of core
  into the provider packages (`#191`, `1.0.9`), so `Pivot`/`Unpivot`/`DistinctOn` no longer come from
  `NextORM.Core`.

* **All demo CTEs are now typed (`AsCte`).** The remaining string-name CTE reads
  (`ctx.With(name, query)` / `ctx.From(name)` + `GetInt32("col")`) were converted to typed descriptors
  (`query.AsCte(name)` + `ctx.From(cte)` with member access) in postgres (queries 1–5), mssql
  (queries 2–4) and clickhouse (queries 4, 5); the final projections stay anonymous. `avg`/`stdev`, which
  return a **non-nullable** value type for a value-type argument, are projected as `(double?)`/`(decimal?)`
  so the one-row-per-group case stays distinguishable from zero — matching the previous `GetNullable*`
  reads. The only remaining by-name reads are the MSSQL `Pivot` result columns, which are dynamic. Re-run
  unchanged: postgres `11/11`, mssql `11/11`, clickhouse `10/11` (`Incremental` only).

* **Re-verified against the `1.0.9-rc3` packages (2026-10-20).** `NextOrmVersion` bumped
  `1.0.9-rc2 → 1.0.9-rc3` for postgres/clickhouse (mssql keeps the `ProjectReference` to the current
  checkout). Results and row counts are unchanged: postgres `11/11`, mssql `11/11`, clickhouse `10/11`
  (`Incremental` only). One example now exercises a fix shipped in this release: MSSQL `VipChurn` aliases
  the CTE-producing chain itself (`WithAlias(Alias.Order)` plus `Alias.Customer`/`Alias.Person`) and then
  consumes it from the aliased `CustomerMetrics` chain — the shape that used to drop every downstream
  alias (`Alias.Metrics`/`SalesPerson`/`Manager` → `CS0117`, [#230](https://github.com/AlexeyShirshov/nextorm/issues/230)).
  The metrics root still projects the named `CustomerMetricRow` DTO: an anonymous chain type is rejected
  with `NORMGEN009` ([#229](https://github.com/AlexeyShirshov/nextorm/issues/229)), so aliasing it is not
  possible by design. `VipChurn` still returns 146 rows in both arms. The same pass moved every eligible
  join in the postgres and mssql examples to the source-less (type-only) form
  (`.Join<T>((l, r) => …)` instead of `.Join(ctx.From<T>(), …)`) and left the descriptor form for CTE
  sides (`.Join(cte, …)`, which needs no `ctx.From(cte)` wrapper); SQL and row counts are unchanged.

## Comparison conversion (2026-10-17)

* **Every example is now a comparison project.** The eleven queries per dataset are implemented twice:
  `*Queries.cs` (nextorm) and `Linq2DbQueries.cs` (linq2db 6.5.0), with parallel entity mappings
  (`Entities.cs` interfaces for nextorm, `Linq2DbEntities.cs` POCOs for linq2db). `Program` runs both
  arms per query, prints the first rows of each, reports `[ OK ]`/`[FAIL]`/`[DIFF]` and checks that the
  row counts match. Known-gap exceptions on either side are `[FAIL]`; anything else aborts.
* **Benchmarks.** Each project has a BenchmarkDotNet class (`Benchmarks.cs`) with a `Nextorm_*`
  baseline and a `Linq2Db_*` arm per query; `--benchmark` provisions the DB once and runs BDN in-process
  (`Job.ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`). Filter with
  `--benchmark --filter '*Name*'`; set `NEXTORM_EXAMPLES_BENCH_FULL=1` for the full job. The measured
  code is only build + execute + materialise (no printing/JSON).
* **linq2db arms deliberately use raw terminals where the LINQ surface ends** — annotated per method:
  PostgreSQL `percentile_disc(...) WITHIN GROUP` (`FromSql`), MSSQL `PIVOT` (rewritten as conditional
  aggregation), ClickHouse `windowFunnel`/`groupArray`/`uniqMerge` (`FromSql`) and the aggregate-state
  threshold. Everything else is LINQ (`AsCte`, `Sql.Window.*`, `Sql.StringAggregate`, `Sql.Expr`).
  Value parity was spot-checked across all queries. Two intentional representation differences remain on
  ClickHouse: `uniq` is approximate, so the linq2db arm uses exact `COUNT(DISTINCT)` (e.g.
  `DeviceSplit` desktop users 86 944 vs 86 317), and the retention matrix is a `toString(...)` string on
  the linq2db arm vs a CLR tuple array on the nextorm arm. PostgreSQL `date_diff("minute")` is truncated
  integer minutes in nextorm, so the linq2db arm emits the same `CAST(trunc(EXTRACT(EPOCH …)/60) AS integer)`
  for `AirportOnTimePerformance`/`DelayPercentilesByModel` (values then match exactly).
* **Packages.** The examples add `linq2db` 6.5.0 plus the PostgreSQL/SQL Server/ClickHouse providers
  (`Linq2DbVersion` in [`Directory.Packages.props`](Directory.Packages.props)). The nextorm package
  model is unchanged: postgres/clickhouse use the published `1.0.9-rc4` packages, mssql stays on
  `ProjectReference` for the typed `Pivot` surface.
* **Full benchmark run (2026-10-17, Release, `ShortRun` + `InProcessEmitToolchain`, warm volumes).**
  PostgreSQL 22/22 cases (28:53), MSSQL 22/22 (02:43), ClickHouse 19/21 (06:29; `Nextorm_DailyTraffic`
  and `Linq2Db_Incremental` hit transient ClickHouse HTTP 120 s timeouts and were re-run successfully:
  78.72 / 83.82 ms and 24.84 ms). Notable ratios (nextorm / linq2db, mean): PG
  `BusinessOccupancyMatrix` 4.71 s / 19.86 s, `PassengerNoShowAnalysis` 4.90 s / 16.49 s,
  `DelayPercentilesByModel` 45.05 / 45.04 ms (5.5 KB vs 31.3 KB allocated); MSSQL `RollingKpi`
  19.2 / 32.6 ms, `ProductAbcXyz` 84.1 / 154.6 ms, `QuarterlyPivot` 181 / 109 ms, `TerritoryGrowthMoM`
  13.3 / 32.8 ms; ClickHouse `Sessions` 1.27 s median / 1.05 s, `SessionDepth` 167 / 116 ms,
  `TopLandingPages` 414 / 370 ms. Database execution dominates every case, so the ratios reflect the SQL
  plans the two ORMs generate, not only materialisation overhead; `ShortRun` values are noisy and are
  regression indicators, not published benchmark claims. Allocation regressions above 1.2× are tracked
  in [#267](https://github.com/AlexeyShirshov/nextorm/issues/267) (PG `DelayPercentilesByModel` 5.67×),
  [#268](https://github.com/AlexeyShirshov/nextorm/issues/268) (PG `AircraftDelayChains` 1.42×),
  [#269](https://github.com/AlexeyShirshov/nextorm/issues/269) (PG `AirportOnTimePerformance` 1.23×),
  [#270](https://github.com/AlexeyShirshov/nextorm/issues/270) (CH `Funnel` 3.04×),
  [#271](https://github.com/AlexeyShirshov/nextorm/issues/271) (CH `Retention` 3.02×),
  [#272](https://github.com/AlexeyShirshov/nextorm/issues/272) (CH `Sessions` 2.82×),
  [#273](https://github.com/AlexeyShirshov/nextorm/issues/273) (CH `SessionDepth` 1.62×) and
  [#274](https://github.com/AlexeyShirshov/nextorm/issues/274) (CH `RollingActivity` 1.22×).

## Known environment pitfalls (do not mistake for product failures)

* The Podman machine can stop after an OOM on the WSL host (`no such container`, connection refused).
  Start it again (`podman.exe machine start`) and prune leftovers.
* A WSL restart clears `/tmp` and drops the `/mnt/wsl/podman-sockets/...` mount, so the next example run
  fails with `DockerUnavailableException`. Recreate it with
  `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start` (idempotent) and wait for
  `podman-user.sock` to reappear before retrying.
* The first (cold) `hits_v1` load copies the ~841 MB archive and builds the columnar parts; free memory
  first (`dotnet build-server shutdown`, `POST /containers/prune`) or the process is OOM-killed. Warm
  runs reuse the named volume and skip it.
* This repository is sometimes edited by another agent in parallel; re-check that the example
  `md5sum`s you measured still match before trusting a run, and re-run if the tree moved.
