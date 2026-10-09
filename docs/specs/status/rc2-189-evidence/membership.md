# D189 — E09.M02 membership reconciliation (r=3 / rv=3 / n=1)

## Frozen membership: 7 acceptance cases

Reconciled from two sources:
- D134 documented historical baseline (`docs/specs/status/rc1-tail-134-sqlite-datareader-1.md`):
  `--anyCategories=acceptance` → exit 0, **7/7** cases, global **48.08 s**
  (`/tmp/D134-evidence/baseline-acceptance.log`).
- rv=3 discovery (`discovery.log`, `git f7a6d728`,
  `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --list flat --anyCategories=acceptance`,
  exit 0) → exactly 7 cases.

| # | Case | Category | Source |
|---|---|---|---|
| 1 | `NextORM.Benchmark.InMemoryBenchmarkAggregates.Nextorm_Count` | acceptance | discovery.log |
| 2 | `NextORM.Benchmark.InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | acceptance | discovery.log |
| 3 | `NextORM.Benchmark.SqliteBenchmarkAny.Nextorm_Cached` | acceptance | discovery.log |
| 4 | `NextORM.Benchmark.SqliteBenchmarkCachedPlan.Prepared_ToList` | acceptance | discovery.log |
| 5 | `NextORM.Benchmark.SqliteBenchmarkCachedPlan.Cached_ToList` | acceptance | discovery.log |
| 6 | `NextORM.Benchmark.SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | acceptance | discovery.log |
| 7 | `NextORM.Benchmark.SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | acceptance | discovery.log |

Reconciliation: **7/7 names match**, 0 added, 0 dropped, 0 substituted, 0 invented. Frozen set = discovery set
= D134 historical set size.

## Measurement config (frozen)

- Benchmark harness: `NextormConfig` with `Job.ShortRun` + `InProcessEmitToolchain` (from the D134
  acceptance run; `--anyCategories=acceptance` selects the same 7 cases).
- `G_current` = the BDN summary line
  `Global total time: 00:00:MM (SS.ss sec), executed benchmarks: 7`.
- Historical baseline constant: `G_baseline = 48.08 s` (D134 baseline acceptance log).
- Cached/prepared ratio = `Mean(Prepared_ToList) / Mean(Cached_ToList)`
  (case 4 over case 5; both from `SqliteBenchmarkCachedPlan`).

## Closure predicate (no invented noise allowance)

`G_current ≤ 48.08 s` AND `ratio = G_current / 48.08 ≤ 1.0000` AND 7/7 cases executed.
