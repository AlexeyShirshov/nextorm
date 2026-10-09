# D189 — E09.M01 measurement + closure (r=3 / rv=3 / n=1)

## Run (single authorized invocation, exit 0)

Command:
```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --artifacts /tmp/nextorm-d189-e09-rv3-f7a6d728/BenchmarkDotNet.Artifacts
```
- `measurement.exit` = **0**
- Log: `measurement.log` (BDN wrote its results to the repo-root `BenchmarkDotNet.Artifacts`; the
  acceptance report set was copied to `$A/BenchmarkDotNet.Artifacts/` as evidence).
- Config: `Job=ShortRun Toolchain=InProcessEmitToolchain IterationCount=3 LaunchCount=1 WarmupCount=3`
  (identical to the D134 acceptance config).

## G_current (raw BDN global line)

```
Global total time: 00:00:55 (55.05 sec), executed benchmarks: 7
```

- `G_current = 55.05 s`
- `G_baseline = 48.08 s` (D134 baseline acceptance log, `/tmp/D134-evidence/baseline-acceptance.log`)
- `ratio = G_current / G_baseline = 55.05 / 48.08 = 1.1450`

## Per-case results (all 7 executed)

| # | Case | Mean | Allocated |
|---|---|---|---|
| 1 | `InMemoryBenchmarkAggregates.Nextorm_Count` | 2.186 ms | 375 KB |
| 2 | `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | 57.21 ms | 50.11 MB |
| 3 | `SqliteBenchmarkAny.Nextorm_Cached` | 1.946 ms | 610.2 KB |
| 4 | `SqliteBenchmarkCachedPlan.Prepared_ToList` | 885.1 μs | 76.14 KB |
| 5 | `SqliteBenchmarkCachedPlan.Cached_ToList` | 1,804.6 μs | 583.97 KB |
| 6 | `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | 545.3 μs | 507.83 KB |
| 7 | `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | 1.919 ms | 608.46 KB |

Executed: **7/7** (BDN reports `executed benchmarks: 7`; all five report classes present:
InMemoryBenchmarkAggregates, InMemoryBenchmarkGroupBy, SqliteBenchmarkAny, SqliteBenchmarkCachedPlan,
SqliteBenchmarkWhere). No failed/missing cases.

## Cached/prepared ratio (R07)

`Mean(Prepared_ToList) / Mean(Cached_ToList) = 885.1 μs / 1,804.6 μs = 0.4905`.

## Closure predicate (no invented noise allowance)

| Clause | Requirement | Observed | Result |
|---|---|---|---|
| `G_current ≤ 48.08 s` | ≤ 48.08 | **55.05 s** | **FALSE** |
| `ratio ≤ 1.0000` | ≤ 1.0000 | **1.1450** | **FALSE** |
| 7/7 executed | 7 | 7 | TRUE |

## Verdict: **FAIL** — E09 remains OPEN

`G_current` (55.05 s) exceeds the D134 baseline (48.08 s) by 6.97 s; the ratio is 1.1450 > 1.0000.
Per the closure predicate this is a **FAIL**, reported honestly with no noise allowance. E09 stays
open; this is a DO→CHECK / potential escalation item, not closed by this task. The cached/prepared
ratio (0.4905) is recorded for R07 context but does not override the global predicate.
