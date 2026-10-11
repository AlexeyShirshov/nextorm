# D211 perf evidence

Owner: DO-perf (D:07). Baseline: `docs/specs/performance/acceptance-benchmarks.md`, measured 2026-09-26 on the
same host/config. Logs: `acceptance.log`, `eager-perparent.log`, `benchmark-build.log`, `docfx.log`,
native BDN reports under `native/`.

## Mandatory acceptance

Recorded for `artifacts/pdca/D211-eager-perparent-topn-1/rv1/manifest.json` EV-13.

- Command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
- Exit: 0. Wall clock (shell): **51 s** (BDN `Global total time: 00:00:46 (46.45 sec)`), ≤240 s.
- **7** completed cases, **0** failures.
- Config: `Job=ShortRun`, `InProcessEmitToolchain`, `IterationCount=3`, `WarmupCount=3`, `LaunchCount=1`,
  `Categories=acceptance`, `MemoryDiagnoser`.

| Case | Mean | Allocated | Baseline Mean | Baseline Allocated |
|---|---|---|---|---|
| `Nextorm_Count` | 3.018 ms | 380.47 KB | 2.915 ms | 335.17 KB |
| `Nextorm_GroupByCount` | 73.13 ms | 50.1 MB | 60.95 ms | 50 MB |
| `Nextorm_Cached` | 2.455 ms | 615.68 KB | 1.772 ms | 534.42 KB |
| `Prepared_ToList` | 1,136.3 us | 76.14 KB | 923.8 us | 76.14 KB |
| `Cached_ToList` | 2,485.0 us | 589.45 KB | 1,727.4 us | 565.22 KB |
| `Cached_PlanOnly_Param` | 775.1 us | 513.3 KB | 518.3 us | 489.08 KB |
| `Nextorm_Cached_ToListAsync` | 2.690 ms | 613.94 KB | 2.137 ms | 692.07 KB |

### Cached vs prepared ratio

- This run: `Cached_ToList / Prepared_ToList` = 2485.0 / 1136.3 = **2.19** (alloc ratio 589.45 / 76.14 = 7.74).
- Baseline: **1.87** (alloc ratio 7.42).
- Ratio growth = 2.19 / 1.87 − 1 = **+17 %** — **below the 20 % investigation threshold**; no regression
  investigation is triggered. `Cached_PlanOnly_Param` is a no-database unlike workload and is not used for
  this ratio (per the baseline page). Absolute means moved on both sides (noisy `ShortRun`, 3 iterations);
  recorded, not interpreted as a gain/regression.

## Dedicated eager per-parent benchmark

- Command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*EagerPerParentPagingBenchmark*" --job short`
- Exit: 0. Wall clock (shell): **69 s**. BDN `Global total time: 00:01:04 (64.13 sec)`,
  `executed benchmarks: 9`.
- Not assigned to the `acceptance` category; the seven-case acceptance count is unchanged.
- 9 completed cases = `InProcessEmitToolchain` job (the acceptance toolchain). The extra `--job short`
  `Default` (out-of-process) job could not generate its project: BDN found **two** matching
  `nextorm.benchmark.csproj` because of the pre-existing gitignored snapshot
  `artifacts/pdca/D208/rv1/base/benchmarks/nextorm.benchmark/nextorm.benchmark.csproj` (721 MB, another
  cycle's evidence, not modified). This is an environment collision, not a benchmark/code failure; the
  in-process job ran all 9 cases.
- Setup validates that both server-paged modes reproduce the independent
  unpaged-load-plus-reference-page result exactly at every size; the benchmark methods assert the command
  count of the measured invocation (split = 2, single = 1).

| Size | Case | Mean | Allocated |
|---|---|---|---|
| 1×8 | `Split_Paged` | 8.786 ms | 426.46 KB |
| 1×8 | `SingleQuery_Paged` | 4.103 ms | 346.81 KB |
| 1×8 | `Split_Unpaged_ReferencePage` | 5.545 ms | 399.39 KB |
| 100×20 | `Split_Paged` | 31.645 ms | 1472.31 KB |
| 100×20 | `SingleQuery_Paged` | 31.210 ms | 1616.93 KB |
| 100×20 | `Split_Unpaged_ReferencePage` | 23.415 ms | 3960.63 KB |
| 1000×20 | `Split_Paged` | 387.315 ms | 11656.19 KB |
| 1000×20 | `SingleQuery_Paged` | 275.948 ms | 12903.04 KB |
| 1000×20 | `Split_Unpaged_ReferencePage` | 323.823 ms | 38566.18 KB |

The reference case is a deliberately **unlike** workload (it transfers and materializes every child — at
1000×20 ~3.3× the allocation of the paged split path), so its numbers are a page-equivalence reference, not
a comparable regression baseline. Query count per measured invocation is captured by the interceptor and
asserted inside each case.

## Documentation build

- Command: `dotnet docfx docs/docfx.json`, exit 0. 2 warnings, both pre-existing and unrelated
  (`Duplicate source file .../AnalyzerReleases.*.md` in `nextorm.core.sourcegenerator`); 0 errors. Generated
  `docs/_site`/`docs/api` are gitignored and not committed.
