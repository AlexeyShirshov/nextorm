# Acceptance benchmarks: cached query path

Internal performance acceptance suite for changes that touch query planning / the plan
cache, the executor / materialization path, or in-memory query execution. It is a small
subset of the full benchmark corpus (7 cases) chosen so a full run fits in the `ShortRun`
budget on a developer host.

It is not a CI performance gate and not a correctness test; the mandate lives in the
PDCA overlay (skill `.opencode/skills/nextorm-pdca/SKILL.md`, `### Перф-приёмка cached path`,
загружается вместе с глобальным скиллом `pdca-dotnet`).

## Reproducible command

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

The seven cases are tagged with `[BenchmarkCategory("acceptance")]`, so
`--anyCategories=acceptance` selects exactly them. The benchmark project's `NextormConfig`
default job is `Job.ShortRun` with `InProcessEmitToolchain`; setting `NEXTORM_BENCH_FULL=1`
switches it to `Job.Default`. The suite uses no containers and no external database —
SQLite runs against the bundled `data/test.db`.

The acceptance contract is confirmed from the executed run itself: BenchmarkDotNet must
report **7** selected cases and **0** failures. The observed run ended with
`Global total time: 00:00:51 (51.88 sec), executed benchmarks: 7`; a run that selected
fewer cases is incomplete and must not be compared with the baseline. A cheaper list/count
smoke was not validated for this runner — do not assume a list CLI syntax; take the count
from the executed run output.

`--job Dry` is **not** the gate and **not** a cheap smoke: it adds a second job on top of
the config's `ShortRun`, so the same command reports 14 cases = 7 methods × 2 jobs. Do not
read its metrics and do not substitute it for the executed acceptance run.

## The seven cases

| # | Class | Method | What it covers |
|---|-------|--------|----------------|
| 1 | `InMemoryBenchmarkAggregates` | `Nextorm_Count` | in-memory `Count()` over 10 000 rows × 100 iterations (declared baseline) |
| 2 | `InMemoryBenchmarkGroupBy` | `Nextorm_GroupByCount` | in-memory `GroupBy` + `count()` over 10 000 rows × 100 iterations (declared baseline) |
| 3 | `SqliteBenchmarkAny` | `Nextorm_Cached` | cached `AnyAsync` |
| 4 | `SqliteBenchmarkCachedPlan` | `Prepared_ToList` | SQLite prepared command, no plan lookup (declared baseline) |
| 5 | `SqliteBenchmarkCachedPlan` | `Cached_ToList` | SQLite cached plan + `ExtractParams`, same SQL as #4 |
| 6 | `SqliteBenchmarkCachedPlan` | `Cached_PlanOnly_Param` | plan-cache lookup + parameter extraction, no database |
| 7 | `SqliteBenchmarkWhere` | `Nextorm_Cached_ToListAsync` | cached `Where(...).ToListAsync()` |

## Baseline table

Measured 2026-09-26, `Job.ShortRun` (`IterationCount=3`, `WarmupCount=3`, `LaunchCount=1`),
`InProcessEmitToolchain`, `MemoryDiagnoser` enabled (`Gen0` / `Allocated` reported for every
case). All 7 cases completed with 0 failures. A separate external wall-clock observation
(including `dotnet run` startup) recorded **53 s** — this figure is not emitted by BDN's
report. BenchmarkDotNet's own `Global total time` for the benchmark execution was
**51.88 s**. These are distinct figures — 53 s is the external shell duration, 51.88 s is
BDN's internal total; both are under the 4 min budget.
BenchmarkDotNet writes a generated report per run (untracked, not a stable repository
reference); the table below is the source of truth for comparison and records `Allocated`
exactly as BDN prints it in that report — BDN rounds, e.g. a raw ~50.02 MB measurement is
printed as `50 MB`.

| Case | Mean | Error | StdDev | Allocated | Gen0 |
|------|------|-------|--------|-----------|------|
| `Nextorm_Count` | 2.915 ms | 10.458 ms | 0.5733 ms | 335.17 KB | 39.06 |
| `Nextorm_GroupByCount` | 60.95 ms | 13.69 ms | 0.750 ms | 50 MB | 6222.22 |
| `Nextorm_Cached` | 1.772 ms | 0.2600 ms | 0.0143 ms | 534.42 KB | 64.45 |
| `Prepared_ToList` | 923.8 μs | 185.40 μs | 10.16 μs | 76.14 KB | 8.79 |
| `Cached_ToList` | 1,727.4 μs | 159.21 μs | 8.73 μs | 565.22 KB | 68.36 |
| `Cached_PlanOnly_Param` | 518.3 μs | 91.43 μs | 5.01 μs | 489.08 KB | 59.57 |
| `Nextorm_Cached_ToListAsync` | 2.137 ms | — | — | 692.07 KB | 82.03 |

`Nextorm_Count` is high-variance: `Error` (10.458 ms) exceeds `Mean` (2.915 ms), coefficient
of variation ≈ 20 %. Its baseline row is **measurement only** — do not use it for regression
assertions on this host/config; it remains a measured acceptance case. BDN hid `Error`/`StdDev`
for `Nextorm_Cached_ToListAsync`; the raw values were `Error = 0.6466 ms`,
`StdDev = 0.0354 ms`.

## Cached vs prepared (same run)

`SqliteBenchmarkCachedPlan` measures both sides against the same SQLite execution, so the
ratio is comparable within a run:

| Case | Mean | Ratio vs `Prepared_ToList` | Allocated | Alloc ratio |
|------|------|----------------------------|-----------|-------------|
| `Prepared_ToList` (baseline) | 923.8 μs | 1.00 | 76.14 KB | 1.00 |
| `Cached_ToList` | 1,727.4 μs | **1.87** | 565.22 KB | **7.42** |

The cached-vs-prepared ratio tracked by the acceptance rule is
`Cached_ToList / Prepared_ToList` = **1.87** on this run; the corresponding allocated-memory
ratio is **7.42**.

`Cached_PlanOnly_Param` (518.3 μs, 489.08 KB) is a **no-database** micro-benchmark: it
measures plan-cache lookup plus parameter extraction, without the SQLite execution that
`Prepared_ToList` performs. The raw report's ratio-to-`Prepared_ToList` value (0.56 time,
6.42 allocation) therefore divides two different workloads and is **not comparable** — it
must not be used for the cached-vs-prepared rule.

## Environment

- Host: AMD Ryzen 7 5800HS with Radeon Graphics 3.19 GHz, 1 CPU, 8 logical / 4 physical cores.
- OS: Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish).
- .NET SDK 10.0.401; runtime .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3.
- BenchmarkDotNet 0.15.8.

## Interpretation

- Measurements are noisy. `ShortRun` uses only 3 iterations; `Error`/`StdDev` above are large
  for several cases. Compare only on the same host with the same config and the same case set,
  ideally in the same session.
- A **comparable** cached-vs-prepared ratio deterioration of **>20 %** relative to the baseline
  is a prompt to investigate, not an automated hard fail. Record the outcome.
- Runs that are not comparable (different OS/CPU/BenchmarkDotNet config/case set, missing
  cases, `NEXTORM_BENCH_FULL=1`, out-of-process vs in-process toolchain) and noisy differences
  must not be reported as regressions.
- Cost target: **≤4 min** wall clock on the current host for the `ShortRun` acceptance command.
  Exceeding it is a gate failure — report the measured time; do not silently loosen the
  criterion.

## Results 2026-09-27

Two in-cycle re-runs of the acceptance command on the same host/config/case set as the baseline
(AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, .NET 10.0.12, BenchmarkDotNet 0.15.8,
`Job.ShortRun`, `InProcessEmitToolchain`, `Categories=acceptance`, **7** cases, **0** failures).
Machine verified quiet of other builds/tests before each run.

### Iteration 1 (earlier this cycle)

External shell wall clock **48 s**; BDN `Global total time` **45.67 s** — both under the 4 min
budget.

| Case | Mean | Allocated | Delta Mean vs baseline |
|------|------|-----------|------------------------|
| `Nextorm_Count` | 2.225 ms | 335.16 KB | -24% (high-variance row, not an assertion) |
| `Nextorm_GroupByCount` | 59.32 ms | 50 MB | -2.7% |
| `Nextorm_Cached` | 1.834 ms | 534.43 KB | +3.5% |
| `Prepared_ToList` | 901.9 us | 76.14 KB | -2.4% |
| `Cached_ToList` | 1,779.1 us | 565.22 KB | +3.0% |
| `Cached_PlanOnly_Param` | 514.8 us | 489.08 KB | -0.7% |
| `Nextorm_Cached_ToListAsync` | 2.089 ms | 692.07 KB | -2.2% |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **1.97** (baseline
**1.87**, +5.5%); the corresponding allocated ratio is **7.42**, unchanged.

### Iteration 2 (post-fix re-verification)

External shell wall clock **50.69 s**; BDN `Global total time` **49.21 s**, **7** executed
benchmarks, **0** failures — both under the 4 min budget. (BenchmarkDotNet logged its usual
`Failed to set up priority High ... Permission denied` process warning; it is not a benchmark
failure.)

| Case | Mean | Allocated | Delta Mean vs baseline | Delta Mean vs iter 1 |
|------|------|-----------|------------------------|----------------------|
| `Nextorm_Count` | 2.201 ms | 335.16 KB | -24% (high-variance row, not an assertion) | -1.1% |
| `Nextorm_GroupByCount` | 59.82 ms | 50.01 MB | -1.9% | +0.8% |
| `Nextorm_Cached` | 1.801 ms | 534.42 KB | +1.6% | -1.8% |
| `Prepared_ToList` | 904.8 us | 76.14 KB | -2.1% | +0.3% |
| `Cached_ToList` | 1,735.6 us | 565.22 KB | +0.5% | -2.4% |
| `Cached_PlanOnly_Param` | 498.4 us | 489.08 KB | -3.8% | -3.2% |
| `Nextorm_Cached_ToListAsync` | 1.987 ms | 692.06 KB | -7.0% | -4.9% |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **1.92** — vs baseline
**1.87** (+2.7%) and vs iteration 1 **1.97** (-2.5%); the corresponding allocated ratio is
**7.42**, unchanged. Both deltas are far below the 20% investigation threshold, so no re-run was
required.

Verdict (final, iteration 2): within noise, **no regression**; baseline numbers above left
unchanged.

### Iteration 3 (P1 plan/mapper fix re-verification)

BDN `Global total time` **44.55 s**, **7** executed benchmarks, **0** failures — under the 4 min
budget. (Same benign `Failed to set up priority High ... Permission denied` process warning.)

| Case | Mean | Allocated | Delta Mean vs baseline |
|------|------|-----------|------------------------|
| `Nextorm_Count` | 2.220 ms | 338.28 KB | -23.8% (high-variance row, not an assertion) |
| `Nextorm_GroupByCount` | 65.38 ms | 50.02 MB | +7.2% |
| `Nextorm_Cached` | 2.310 ms | 537.58 KB | +30.4% (high-variance, CI ±191%) |
| `Prepared_ToList` | 922.8 us | 76.14 KB | -0.1% |
| `Cached_ToList` | 1,797.4 us | 569.90 KB | +4.1% |
| `Cached_PlanOnly_Param` | 503.9 us | 493.76 KB | -2.8% |
| `Nextorm_Cached_ToListAsync` | 2.195 ms | 717.07 KB | +2.7% |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **1.95** — vs baseline
**1.87** (+4.1%), far below the 20% threshold; allocated ratio **7.49** (baseline 7.42), unchanged.
The Cached path adds two type comparisons per projected column during plan lookup (constant per
command, not per row). Verdict: within noise, **no regression**.
