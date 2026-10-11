# D211 REQ-10 — acceptance re-run on the final corrected tree (r1 n2/3)

CHECK flagged the D:07 `perf/summary.md:21-34` table: several comparable Means were 23–50 % above the
documented baseline without an investigation. This re-run resolves it.

- Command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
- Tree: working tree = `e98ae60e` + r1-n2 corrections + the two r1-n2 coverage tests.
- Result: exit 0, **7** executed benchmarks, **0** failures, BDN `Global total time` **49.38 s**
  (external shell wall clock **57.40 s**), ≤240 s.
- Config: `Job.ShortRun`, `InProcessEmitToolchain`, `IterationCount=3`, `WarmupCount=3`,
  `LaunchCount=1`, `Categories=acceptance`, `MemoryDiagnoser`.
- Log: `perf/acceptance-final.log`.

## Per-case comparison

`run` = this final run; `base` = latest documented baseline block
(`docs/specs/performance/acceptance-benchmarks.md`, 2026-10-10); `orig` = the 2026-09-26 table the
D:07 summary compared against; `prior` = `perf/summary.md` D:07 run.

| Case | run Mean | run Alloc | Δ vs 2026-10-10 | Δ vs 2026-09-26 | Δ vs prior D:07 |
|---|---|---|---|---|---|
| `Nextorm_Count` | 2.175 ms | 380.47 KB | −8.7% | −25.4% (high-variance row) | −27.9% |
| `Nextorm_GroupByCount` | 56.71 ms | 50.1 MB | −3.8% | −7.0% | −22.5% |
| `Nextorm_Cached` | 1.829 ms | 615.67 KB | −6.4% | +3.2% | −25.5% |
| `Prepared_ToList` | 892.2 us | 76.14 KB | 0.0% | −3.4% | −21.5% |
| `Cached_ToList` | 1,763.9 us | 589.44 KB | −4.5% | +2.1% | −29.0% |
| `Cached_PlanOnly_Param` | 516.6 us | 513.3 KB | −7.5% | −0.3% | −33.4% |
| `Nextorm_Cached_ToListAsync` | 1.949 ms | 613.94 KB | +1.0% | −8.8% | −27.5% |

Tracked ratio `Cached_ToList / Prepared_ToList` = **1.977** (allocated ratio 7.74):
vs 2026-10-10 baseline **2.07** = **−4.5 %**; vs 2026-09-26 baseline **1.87** = **+5.7 %**.
Both far below the **20 %** investigation threshold (`1.87 × 1.20 = 2.244`).

## Verdict

**Noise — not reproduced.** The 23–50 % rises in the D:07 table are a uniform, whole-run inflation
(the prepared denominator and the no-database `Cached_PlanOnly_Param` moved with everything else),
i.e. a noisy host during the D:07 run. The final run lands within ±9 % of the 2026-10-10 baseline for
every case except the declared high-variance `Nextorm_Count` row, and within ±9 % of the 2026-09-26
baseline as well; the tracked cached-vs-prepared ratio is well inside the 20 % band on both
comparisons. The change touches the eager-load child path, not the seven acceptance cases' query
shapes, so no per-row work is added to the measured paths. No comparable >20 % growth is reproduced.
