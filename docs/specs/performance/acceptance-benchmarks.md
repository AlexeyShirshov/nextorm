# Acceptance benchmarks: cached query path

Internal performance acceptance suite for changes that touch query planning / the plan
cache, the executor / materialization path, or in-memory query execution. It is a small
subset of the full benchmark corpus (7 cases) chosen so a full run fits in the `ShortRun`
budget on a developer host.

It is not a CI performance gate and not a correctness test; the mandate lives in the
PDCA overlay (skill `.opencode/skills/nextorm-pdca/SKILL.md`, `### Перф-приёмка cached path`,
загружается вместе с глобальным скиллом `pdca-dotnet`).

> **Статус (2026-10-17).** Этот файл — только **контракт приёмки**: 7 кейсов, baseline, правила
> интерпретации. Датированные прогоны и пер-issue замеры вынесены в `benchmark-report.md` →
> «Acceptance runs and per-issue measurements». Baseline зафиксирован 2026-09-26; текущие измерения
> ниже него — улучшение, а не дрейф (на 2026-10-15: 1.71 по времени / 5.98 по аллокациям против
> 1.87 / 7.42). Новые прогоны добавлять в реестр, не сюда.

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

## Measured results: #299 literal escaping (2026-10-21)

Task T299 / issue #299 (#258 commit `27d17429`, inline string literals escaped through the
shared `SqlLiteral.ToSqlStringLiteral` helper). Same host, config and case set as the baseline:
AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401 / runtime 10.0.12, BenchmarkDotNet
0.15.8, `Job.ShortRun` (`IterationCount=3`, `WarmupCount=3`, `LaunchCount=1`),
`InProcessEmitToolchain`, `Categories=acceptance`, `DOTNET_GCHeapHardLimit=0x80000000`,
`DOTNET_gcServer=0`, 7 executed benchmarks, 0 failures, BDN `Global total time` **46.68 s**.
The baseline column is the pre-fix run of the same tree with the old literal renderer
(`.pdca/T299/rv1/red/benchmark.log`); the candidate column is the fixed tree
(`.pdca/T299/rv1/green/benchmark.log`), run with identical host/env/config/case set.

| Case | Baseline Mean | Candidate Mean | Candidate / baseline | Allocated (base → cand) |
|------|---------------|----------------|----------------------|--------------------------|
| `Nextorm_Count` | 2.996 ms | 2.467 ms | 0.823 | 320.31 KB → 334.38 KB |
| `Nextorm_GroupByCount` | 129.5 ms | 63.74 ms | 0.492 | 50.04 MB → 50.04 MB |
| `Nextorm_Cached` | 3.092 ms | 1.999 ms | 0.647 | 533.65 KB → 533.67 KB |
| `Prepared_ToList` | 1,135.8 μs | 925.9 μs | 0.815 | 76.14 KB → 76.14 KB |
| `Cached_ToList` | 2,115.3 μs | 1,665.0 μs | 0.787 | 433.97 KB → 433.96 KB |
| `Cached_PlanOnly_Param` | 500.7 μs | 439.6 μs | 0.878 | 357.83 KB → 357.82 KB |
| `Nextorm_Cached_ToListAsync` | 2.598 ms | 2.170 ms | 0.835 | 553.78 KB → 553.78 KB |

Cached-vs-prepared ratios of this pair of runs: `Cached_ToList / Prepared_ToList`
**1.86 → 1.80**, allocated ratio **5.70 → 5.70**; `Cached_PlanOnly_Param / Prepared_ToList`
**0.44 → 0.47** (no-database micro-benchmark, not comparable, see above).

Verdict: **no comparable ratio growth >20 %**. The highest mean ratio is 0.878
(`Cached_PlanOnly_Param`) and the highest allocation ratio 1.044 (`Nextorm_Count`,
320.31 → 334.38 KB, the documented high-variance case); every other allocation delta is ≤0.02 KB.
`Nextorm_GroupByCount` and `Nextorm_Count` do not touch the literal renderer at all yet move as
much as the touched cases (−49 % / −18 %), which identifies the deltas as session/`ShortRun`
drift rather than an effect of this change — **no improvement is claimed**. No threshold in this
contract changed and no public guarantee is added. Backslash and NUL literal semantics are
tracked separately in #327 and are out of scope here.

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

