# Examples verification report

> **Last verified: 2026-11-01** (the examples are now **comparison projects**: native comparison queries have
> nextorm and linq2db arms, and both are executed against the same dataset; BenchmarkDotNet runs
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

Each `Program` runs both native arms for each comparable query to completion: it prints the rows of
each arm, `[ OK ]`/`[FAIL]`/`[DIFF]` per query and an `n/m queries matched in both arms` summary. Only
documented gaps (`NotSupportedException`, `QueryPreparationException`, `LinqToDBException`) are recorded
as `FAIL`; any other exception aborts the run. Comparison arms must be native on both sides; whole-query raw SQL (`FromSql`) is not allowed, while partial `Sql.Expr(...)` fragments inside an otherwise native LINQ query are allowed.

## Results

| Example | Matched in both arms | Failing query (documented gap) |
|---|---|---|
| `nextorm.examples.postgres.aviasales` | **10/10** | — |
| `nextorm.examples.mssql.adventureworks` | **11/11** | — |
| `nextorm.examples.clickhouse.analytics` | **9/9** | — |

`nextorm.examples.oc-report` is not a two-arm comparison: it is the NativeAOT reference app. Verified
2026-11-08 (13 GB `opencode.db`, 3-day window, warm page cache, median of three runs): JIT 0.42 s →
NativeAOT 0.12 s, 8.2 MB binary, 30 MB RSS; byte-identical output against the app's Python twin and
against the EF Core / Dapper.AOT arms. See its [README](nextorm.examples.oc-report/README.md).

Query-by-query counts on 2026-10-30 (warm volumes, Podman):

* postgres: aircraft delay chains 30, occupancy matrix 8, no-show 0, rolling revenue 91, ABC/XYZ 614,
  top routes 186, airport OTP 63, percentiles 8, frequent flyers 10, passenger growth 2, cancellations 15.
  All queries return matching row counts; full-value equality is verified separately where noted below.
  `AircraftDelayChains` is the query the named-alias conversion rewrote and the one #350 broke; it now
  returns 30 rows again.
* mssql: 146, 364, 0, 266, 4, 12, 40, 20, 14, 364, 40 — identical in both arms.
* clickhouse: array analytics 10, funnel 3, sessions 100, daily traffic 7, top landing pages 10, device
  split 2, top referrers 10, session depth 5, rolling activity 7; `Retention` (nextorm-only) is excluded from comparison; `Incremental` was removed with the whole-query `FromSql` cleanup.

## Failures and their tracked cause

| Query | Why it fails | Tracked in |
|---|---|---|
| CH `Retention` | rc4 wraps every array result in `cast(… as Array(...))`; for a tuple element the type renderer falls back to the CLR name including its arity, so the SQL says ``Array(Nullable(Tuple`2))`` and ClickHouse rejects it (`Code: 62. Back quoted string is not closed`). The alias conversion is not involved — the positional form emits the same cast. Not present in rc3, which emitted the bare `groupArray(...)`. The query is nextorm-only since the 2026-11-01 comparison withdrawal. | [#359](https://github.com/AlexeyShirshov/nextorm/issues/359) |

CH `Incremental` is no longer part of the example: `uniqMerge` (the `-Merge` combinator over an
`AggregateFunction(uniq, …)` state column) has no nextorm LINQ surface in the released packages — the
state cannot be projected as a scalar — and its only comparison arm was a whole-query linq2db `FromSql`.
Tracked in [`todo_clickhouse_aggregate_function_state.md`](../docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md).

MSSQL `QuarterlyPivot` is no longer a failure: the native `PIVOT` now accepts a derived query, so the
reference `WITH OrderMargins AS (<5-table join>) ... PIVOT (...)` is expressed directly.

## Actuality changes made on this verification

 * **T271 CH Retention source binding and projection measurement (2026-10-31).** Comparison withdrawn on 2026-11-01 (#271, whole-query `FromSql` rule). NextORM-only historical figures: workspace-source baselines 87.46 KB / 186.1 ms and 87.81 KB / 187.1 ms; final projection 197.47 KB / 190.5 ms and 87.37 KB / 190.7 ms. The `SelectGroup` projection was subsequently reverted; source references and tuple-rendering test remain. SQL and row snapshots were byte-identical before/after. Evidence: `.pdca/rc5/items/T271/rv1/`.

  * **T271 correction round (2026-10-31).** The `RetentionGroupRow` projection was reverted; the runnable anonymous projection is restored to the same source state as the workspace baseline (revert diff rc 0), with the workspace project references and tuple-rendering unit test retained. On the reverted baseline, a synchronous single-run probe (3 warmups + 9 samples, `GC.GetAllocatedBytesForCurrentThread`) measured median total `68,176 B/call` (43,392 B query/CTE construction + 24,784 B planning/execute/materialise); two later samples were `67,344 B` (43,392 + 23,952 B). Every sample returned 1 row and a one-element matrix. AC1 remains unmet and no Tier B performance credit is claimed; comparative data is historical only. Gates in correction round: solution Debug 0W/0E, core tests 3946/3946, ClickHouse tests 1456/1456, ClickHouse integration 151/151 with no skips, example Release 0W/0E. Correction evidence: `.pdca/rc5/items/T271/rv1/attempts/2/`.

* **T269 PG AirportOnTimePerformance measurement (2026-10-31).** The final result type is now an
  `internal readonly record struct` (same six columns/order; see
  [`AviasalesQueries.cs`](nextorm.examples.postgres.aviasales/AviasalesQueries.cs)). Four baseline
  paired runs measured NextORM allocations `51.95, 51.95, 51.61, 51.80 KB/op` and linq2db
  `43.33, 43.55, 43.64, 42.90 KB/op` (ratios `1.198×, 1.193×, 1.183×, 1.207×`). Two candidate
  paired runs measured NextORM `50,365.33 B/op / 61.382 ms` and `50,110 B/op / 62.274 ms`, versus
  linq2db `44,472 B/op / 61.422 ms` and `43,824 B/op / 61.564 ms`; ratios `1.1325×` and `1.1434×`.
  Both arms returned 63 rows. Full ordered JSON comparison found two `AvgDelayMinutes` differences
  (Toronto `15.1` vs `15`; Helsinki `13.5` vs `13.4`), so equal row counts are established but full
  value equivalence is not. Evidence, including frozen-source linkage and diff, is under
   `.pdca/rc5/items/T269/rv1/evidence-round1/`.

 * **T270 CH Funnel projection measurement (2026-11-01).** The final Funnel projection in
   [`ClickHouseQueries.cs`](nextorm.examples.clickhouse.analytics/ClickHouseQueries.cs) now uses a
    `readonly record struct` with the same `int Level` / `int ConversionCount` members and types. Two baseline
   paired `*Funnel*` ShortRuns measured Nextorm `48.63 KB / 158.049 ms` vs linq2db `53.33 KB /
   145.187 ms` (ratio `0.912×`), and `48.81 KB / 156.764 ms` vs `53.36 KB / 153.478 ms` (`0.915×`).
    Initial candidate runs with a mismatched `long ConversionCount` measured `46.99 KB / 152.2 ms`
    vs `52.89 KB / 150.9 ms` (`0.8885×`) and `47.12 KB / 149.1 ms` vs `53.05 KB / 149.2 ms`
    (`0.8882×`); that candidate changed the generated cast and was corrected. Final candidate runs
    with `int ConversionCount` measured Nextorm `46.52 KB / 157.0 ms` vs linq2db `53.60 KB / 155.4 ms`
    (`0.8679×`), and `46.62 KB / 163.3 ms` vs `54.15 KB / 159.8 ms` (`0.8609×`). Baseline and candidate numbers
   differ substantially from the historical 2026-10-17 baseline (`161.07 / 52.93 KB`), so comparisons
   here are the same-session current-checkout pairs. The post-change full Funnel comparison returned
    `(Level, ConversionCount)`: `(0,35709)`, `(1,744)`, `(2,9)` in both arms, and pre/post `cmp`
    succeeded for SQL and full row dumps (both rc=0). The rejected `long` candidate's SQL differed
    only by the added `cast(toInt32(count(*)) as Int64)`; the final `int` candidate restores byte identity.
   Candidate run logs and row output are under `.pdca/rc5/items/T270/rv1/attempts/1/`; baseline logs
   are under `.pdca/rc5/items/T270/rv1/baseline/`.

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

* **Every example is now a comparison project.** Each dataset's comparable queries are implemented twice:
  `*Queries.cs` (nextorm) and `Linq2DbQueries.cs` (linq2db 6.5.0), with parallel entity mappings
  (`Entities.cs` interfaces for nextorm, `Linq2DbEntities.cs` POCOs for linq2db). `Program` runs both
  arms per query, prints the first rows of each, reports `[ OK ]`/`[FAIL]`/`[DIFF]` and checks that the
  row counts match. Queries that cannot be compared natively (`DelayPercentilesByModel`, `Retention`)
  keep only the nextorm arm. Known-gap exceptions on either side are `[FAIL]`; anything else aborts.
* **Benchmarks.** Each project has a BenchmarkDotNet class (`Benchmarks.cs`) with a `Nextorm_*`
  baseline and a `Linq2Db_*` arm per query; `--benchmark` provisions the DB once and runs BDN in-process
  (`Job.ShortRun` + `InProcessEmitToolchain`, `[MemoryDiagnoser]`). Filter with
  `--benchmark --filter '*Name*'`; set `NEXTORM_EXAMPLES_BENCH_FULL=1` for the full job. The measured
  code is only build + execute + materialise (no printing/JSON).
* Partial `Sql.Expr(...)` fragments are allowed inside native linq2db queries; whole-query `FromSql` comparison arms are not. `Funnel` uses `FromSql` only for an inner subquery and remains a native LINQ query overall. The former PG `DelayPercentilesByModel` (#267) and CH `Retention` (#271) comparisons were withdrawn on 2026-11-01 under this rule; both queries are nextorm-only now, and CH `Incremental` was removed with them. MSSQL `PIVOT` is conditional aggregation on both sides.
  Everything else is LINQ (`AsCte`, `Sql.Window.*`, `Sql.StringAggregate`, `Sql.Expr`).
  Value parity was spot-checked across all retained queries. One intentional representation difference
  remains on ClickHouse: `uniq` is approximate, so the linq2db arm uses exact `COUNT(DISTINCT)` (e.g.
  `DeviceSplit` desktop users 86 944 vs 86 317). PostgreSQL `date_diff("minute")` is truncated
  integer minutes in nextorm, so the linq2db arm emits the same `CAST(trunc(EXTRACT(EPOCH …)/60) AS integer)`
  for `AirportOnTimePerformance` (values then match exactly).
* **Packages.** The examples add `linq2db` 6.5.0 plus the PostgreSQL/SQL Server/ClickHouse providers
  (`Linq2DbVersion` in [`Directory.Packages.props`](Directory.Packages.props)). The nextorm package
  model is unchanged: postgres/clickhouse use the published `1.0.9-rc4` packages, mssql stays on
  `ProjectReference` for the typed `Pivot` surface.
* **Full benchmark run (2026-10-17, Release, `ShortRun` + `InProcessEmitToolchain`, warm volumes).**
  PostgreSQL 22/22 cases (28:53), MSSQL 22/22 (02:43), ClickHouse 19/21 (06:29; historical benchmark set).
  Notable ratios (nextorm / linq2db, mean): PG
  `BusinessOccupancyMatrix` 4.71 s / 19.86 s, `PassengerNoShowAnalysis` 4.90 s / 16.49 s,
  MSSQL `RollingKpi`
  19.2 / 32.6 ms, `ProductAbcXyz` 84.1 / 154.6 ms, `QuarterlyPivot` 181 / 109 ms, `TerritoryGrowthMoM`
  13.3 / 32.8 ms; ClickHouse `Sessions` 1.27 s median / 1.05 s, `SessionDepth` 167 / 116 ms,
  `TopLandingPages` 414 / 370 ms. Database execution dominates every case, so the ratios reflect the SQL
  plans the two ORMs generate, not only materialisation overhead; `ShortRun` values are noisy and are
  regression indicators, not published benchmark claims. Allocation regressions above 1.2× are tracked
  in [#267](https://github.com/AlexeyShirshov/nextorm/issues/267) (PG `DelayPercentilesByModel`, comparison withdrawn 2026-11-01),
  [#268](https://github.com/AlexeyShirshov/nextorm/issues/268) (PG `AircraftDelayChains` 1.42×),
  [#269](https://github.com/AlexeyShirshov/nextorm/issues/269) (PG `AirportOnTimePerformance` 1.23×),
  [#270](https://github.com/AlexeyShirshov/nextorm/issues/270) (CH `Funnel` 3.04×),
  [#271](https://github.com/AlexeyShirshov/nextorm/issues/271) (CH `Retention`, comparison withdrawn 2026-11-01),
  [#272](https://github.com/AlexeyShirshov/nextorm/issues/272) (CH `Sessions` 2.82×),
  [#273](https://github.com/AlexeyShirshov/nextorm/issues/273) (CH `SessionDepth` 1.62×) and
  [#274](https://github.com/AlexeyShirshov/nextorm/issues/274) (CH `RollingActivity` 1.22×).

## T267 projection allocation follow-up (2026-11-01)

Comparison withdrawn on 2026-11-01 (#267, whole-query `FromSql` rule); the following NextORM-only measurements remain historical facts.

The two-stage `DelayPercentilesByModel` projections now use readonly record structs. The historical
pre-change baseline (two `ShortRun`/`InProcessEmitToolchain` runs) and same-round post-change runs:

| State / run | Nextorm Mean | Nextorm Allocated |
|---|---:|---:|
| Before 1 (standing baseline) | 45.93 ms | 29.70 KB |
| Before 2 (standing baseline) | 44.45 ms | 29.76 KB |
| After 1 | 45.30 ms | 28.25 KB |
| After 2 | 45.04 ms | 28.23 KB |

Same-round NextORM projection probe measured 30,896 B/call anonymous vs 29,192 B/call struct (~213 B/row); build-only was 18,440
B/call, so the dominant residual is per-call pipeline construction, not per-row materialisation.
This step does not claim that residual is closable here. SQL capture before/after was byte-identical,
and the nextorm arm returned the same 8 rows and values before and after.

## T267 #267 Lever 1 publication (2026-11-01)

Comparison withdrawn on 2026-11-01 (#267, whole-query `FromSql` rule). Historical NextORM-only measurements: before Lever 1, 50.76/45.17 ms and 28.90/28.17 KB; after Lever 1, 45.03/45.23 ms and 16.17/16.18 KB. Lever 2 was timeboxed out; retained changes stand. Tier A target and residual gap remain historical conclusions only. Evidence: `.pdca/T267/rv2/`.

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
* **T272 CH Sessions projection measurement (2026-11-01).** The `Sessions` result projection uses `readonly record struct SessionResultRow(long UserID, long SessionId, int Hits)` with unchanged member types. Each paired ShortRun executed both arms against the warm `hits_v1` volume. Allocation is KB/op; times below are the per-run **medians** (ms/op), not means.

  | State | Run | Nextorm KB | Nextorm median ms | linq2db KB | linq2db median ms | Ratio |
  |---|---:|---:|---:|---:|---:|---:|
  | Baseline | 1 | 129.52 | 973.510 | 167.16 | 985.279 | 0.775× |
  | Baseline | 2 | 128.98 | 973.917 | 489.84 | 957.986 | 0.263× |
  | Candidate | 1 | 174.12 | 999.166 | 169.35 | 959.816 | 1.028× |
  | Candidate | 2 | 127.73 | 964.912 | 168.02 | 958.511 | 0.760× |
  | Candidate | 3 | 125.22 | 971.084 | 168.41 | 940.072 | 0.744× |

  All candidate pairs are within the 1.2× allocation target and below the 1,270.66 ms latency limit; every arm returns 100 rows. The baseline linq2db allocation and candidate Nextorm allocation varied substantially, so these results support measured target attainment, not a reliable causal savings claim; uniform ≤1.0× stretch attainment is not established. The comparison query orders descending by Hits and has no secondary key: raw positional changes are only swaps inside equal-Hits ties, with 100-row multisets and descending order preserved. For SQL identity, a temporary `ILoggerFactory` Debug setup captured the two complete non-benchmark NextORM statements; pre/post statements compare byte-identically (rc=0), including `limit 1` and `limit 100`. Extracted full statements are in `.pdca/rc5/items/T272/rv1/attempts/1/sql-{pre,post}-full.txt`. No separate control arm is exposed. Evidence: `.pdca/rc5/items/T272/rv1/attempts/1/`.

* **T273 CH SessionDepth projection measurement (2026-11-01).** The final grouped result uses
  `readonly record struct SessionDepthRow(string? Bucket, int Sessions)` with the original CLR member
  types. The two current-checkout before/after pairs use Release `ShortRun` + `InProcessEmitToolchain`;
  the table reports BDN allocation and the actual three-sample median time (and arithmetic mean for the
  frozen AC1 mean-time bound). Ratios are `Nextorm / linq2db` allocations.

  | State / run | Nextorm KB | Nextorm mean ms | Nextorm median ms | linq2db KB | linq2db mean ms | linq2db median ms | Ratio |
  |---|---:|---:|---:|---:|---:|---:|---:|
  | Before 1 | 79.60 | 99.540 | 99.924 | 53.97 | 100.781 | 100.219 | 1.4749× |
  | Before 2 | 78.71 | 108.724 | 105.977 | 53.90 | 100.894 | 101.024 | 1.4603× |
  | After 1 | 79.24 | 104.228 | 102.566 | 54.50 | 97.422 | 97.779 | 1.4539× |
  | After 2 | 78.59 | 112.722 | 115.204 | 55.46 | 106.331 | 108.318 | 1.4171× |

  The post-change pairs remain above the user target `≤1.2×` (and do not establish the `≤1.0×`
  stretch); the performance criterion is not demonstrated by these measurements. Both Nextorm means
  remain below 166.9 ms. Each benchmark run executed both arms (2/2). The exact five ordered rows are
  unchanged in each arm (`cmp` rc=0); nextorm SQL is byte-identical before/after (`cmp` rc=0), and each
  arm's result multiset is preserved. The historical issue measurement was 166.90/115.63 ms and
  88.20/54.52 KB (1.62×); these current-checkout pairs are the comparable before/after evidence. A
  post-projection `SelectGroup(...).OrderBy(Bucket)` factoring was rejected by query preparation
  (`SelectGroup ORDER BY references a row expression not covered by inferred keys`) and reverted.
  Full logs and captures: `.pdca/rc5/items/T273/rv1/`.

* **T274 CH RollingActivity projection measurement (2026-11-01).** Both grouped/returned projections
  now use `readonly record struct` rows with the original CLR member types: `DateTime EventDate`,
  `int Hits`, `long Users`, and `long` for both rolling sums. The standing baseline is the two paired
  current-lane-head runs (collection base `1e31331d` is an ancestor of HEAD; the same-round linq2db
  denominator is unaffected):

  | State / run | Nextorm KB | Nextorm mean ms | Nextorm median ms | linq2db KB | linq2db mean ms | linq2db median ms | Ratio |
  |---|---:|---:|---:|---:|---:|---:|---:|
  | Before 1 | 56.84 | 57.148 | 56.764 | 46.99 | 52.610 | 52.787 | 1.210× |
  | Before 2 | 57.58 | 56.973 | 56.403 | 47.21 | 52.485 | 52.703 | 1.220× |
  | After 1 | 55.97 | 60.705 | 59.804 | 47.07 | 51.825 | 52.373 | 1.189× |
  | After 2 | 55.97 | 59.325 | 58.629 | 46.87 | 51.200 | 51.305 | 1.194× |

  Both after-runs meet the `≤1.2×` allocation target and `mean <66.7 ms`; the stretch `≤1.0×` is
  not met. Each paired run executed both benchmarks (2/2). Before/after SQL `cmp` rc=0; full ordered
  row `cmp` rc=0 for each arm and between the two arms. All 7 row values match exactly; there is no
  tie permutation, and each arm's result multiset is preserved. Gates: solution Debug 0W/0E, example
  Release 0W/0E, core tests 3946/3946, ClickHouse tests 1456/1456, ClickHouse integration 151/151
  with no skips. Logs and identity artifacts: `.pdca/rc5/items/T274/rv1/`; issue #274 comment posted
  with this table.

## T268 reverted (2026-11-01)

The `AircraftDelayChains` call-site expression hoists measured in cycles 2–3 were **reverted**:
they removed caller-side work that the linq2db arm also performs on every call, so the
comparison stopped being engine-vs-engine. Raw measurements remain as evidence under
`.pdca/T268/rv2/` and `.pdca/T268/rv3/`; #268 is re-scoped to the engine.

## Example-side levers reverted (2026-11-02)

The example-side levers behind #267, #269, #270, #272 and #274 are **reverted**:

- #267 — hoisted `DelayPercentilesByModel` expression trees (`9cc092a5`) and `readonly record struct`
  row types (`84e98272`);
- #269 — `readonly record struct AirportOnTimePerformanceRow` (`d1a7fa62`);
- #270 — `readonly record struct FunnelResultRow` (`04dc4b2f`);
- #272 — `readonly record struct` Sessions projection (`23ac24f0`);
- #274 — `readonly record struct` RollingActivity projections (`55499588`).

Each removed caller-side work or changed the example's own data shape, so the comparison stopped being
engine-vs-engine; a performance issue about an example is closed by a fix in `src/**`, never by editing
the example's call site or projection type. The measured numbers stay in the historical sections above
as evidence only. Both examples build Release 0W/0E afterwards.

The core direction is closed with numbers in `docs/specs/performance/decisions-and-lessons.md` (§R15)
and in the closing comments of #319 and #363.
