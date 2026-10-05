# E06 — acceptance benchmark (7-case suite), issue #166

Command (exact arg array):

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

Env: `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0`, `timeout 900`. `NEXTORM_BENCH_FULL` unset.
Raw transcript: `docs/specs/status/nested-read-cte-1-evidence/E06-acceptance.log`.

- exit code = **0**
- BDN `Global total time: 00:00:53 (53.6 sec), executed benchmarks: 7` → exactly 7 completed, 0 failed/missing
- external shell wall clock measured = **63 s** (includes `dotnet run`/build startup) → ≤ 240 s budget, PASS
- host: AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, runtime .NET 10.0.12,
  BenchmarkDotNet 0.15.8, `Job.ShortRun`, `InProcessEmitToolchain`, `MemoryDiagnoser`

## Per-case result (this run)

| # | Case | Mean | Error | Allocated | Baseline Mean | Baseline Allocated |
|---|------|------|-------|-----------|---------------|--------------------|
| 1 | `Nextorm_Count` | 2.574 ms | 2.828 ms | 334.38 KB | 2.915 ms | 335.17 KB |
| 2 | `Nextorm_GroupByCount` | 65.89 ms | 24.40 ms | 50.05 MB | 60.95 ms | 50 MB |
| 3 | `Nextorm_Cached` | 2.210 ms | 3.441 ms | 547.7 KB | 1.772 ms | 534.42 KB |
| 4 | `Prepared_ToList` | 980.8 us | 2,147.5 us | 76.14 KB | 923.8 us | 76.14 KB |
| 5 | `Cached_ToList` | 2,322.4 us | 5,740.6 us | 541.01 KB | 1,727.4 us | 565.22 KB |
| 6 | `Cached_PlanOnly_Param` | 593.4 us | 568.3 us | 464.86 KB | 518.3 us | 489.08 KB |
| 7 | `Nextorm_Cached_ToListAsync` | 2.031 ms | 1.304 ms | 565.5 KB | 2.137 ms | 692.07 KB |

Per-class raw BDN reports overwritten by the run under
`BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.{InMemoryBenchmarkAggregates,InMemoryBenchmarkGroupBy,SqliteBenchmarkAny,SqliteBenchmarkCachedPlan,SqliteBenchmarkWhere}-report-github.md`;
the artifacts dir was restored to its pre-run state after this capture (see below).

## Cached vs prepared (same run)

Tracked rule: `Cached_ToList / Prepared_ToList`.

| Metric | This run | Documented baseline | Iteration-14 after (2026-10-03) |
|--------|----------|---------------------|---------------------------------|
| Mean ratio (time) | **2.37** (2322.4 / 980.8 us) | 1.87 | 1.99 |
| Allocated ratio | **7.11** (541.01 / 76.14 KB) | 7.42 | 7.83 |

`2.37` is above the 20 % investigatory trigger (`1.87 x 1.20 = 2.244`). The `Cached_ToList` row is
noise-dominated: `Error` (5,740.6 us) is ~2.5x `Mean` (2,322.4 us), `Prepared_ToList` `Error`
(2,147.5 us) likewise, and the host `ShortRun` band recorded in
`docs/specs/performance/acceptance-benchmarks.md` spans 1.88-2.61. The change under acceptance (F1)
only short-circuits the recursive getter path for prepared commands and removes an allocation; it
adds **no per-row work**, and the allocated ratio moved down (7.11 vs 7.42 baseline). No improvement
is claimed and no confirmed regression is established on this noisy run; recorded as the cycle's
acceptance result for CHECK.

## Artifacts hygiene

`BenchmarkArtifacts.Resolve()` looks for `nextorm.sln`, which does not exist (repo has
`nextorm.slnx`), so BDN fell back to `Directory.GetCurrentDirectory()/BenchmarkDotNet.Artifacts`
(i.e. repo-root `BenchmarkDotNet.Artifacts`); pre-existing untracked files under
`benchmarks/BenchmarkDotNet.Artifacts` were not touched. Root artifacts were restored with
`git checkout -- BenchmarkDotNet.Artifacts` after this capture.
