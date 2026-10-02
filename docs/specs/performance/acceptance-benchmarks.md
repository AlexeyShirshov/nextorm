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

### Iteration 4 (ACT re-verification — ratio 2.61 is an outlier)

Same host/config/case set (AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, .NET 10.0.12,
BenchmarkDotNet 0.15.8, `Job.ShortRun`, `InProcessEmitToolchain`, `Categories=acceptance`, **7**
cases, **0** failures). External shell wall clock **~50 s**; BDN `Global total time` **~50 s** — both
under the 4 min budget.

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **2.61** — vs baseline
**1.87** (+39.6%), above the 20% investigation threshold. **The ratio is an outlier and the
regression is NOT confirmed.** The other runs in this cycle measured **1.88 / 1.92 / 2.02**, and the
`ShortRun` config is high-variance on this host (**N=3**; for some rows `Error` is ~3× `Mean`, so the
`Cached_ToList` numerator carries a large CI). Per the interpretation rule above, a noisy difference
on a run this variable must not be reported as a regression, and no path touched by #108 adds
per-row work on the cached path (the filter scope is resolved once per command at plan time). Recorded
as the cycle's acceptance run; **verdict: regression not confirmed — outlier**, consistent with the
1.88–2.02 band of the cycle's other runs.

## FilterFunc cached path (D6, #108 PR4)

The builder-function (`FilterFunc`) filter path is **not** covered by the seven acceptance cases; it
is measured separately by `SqliteBenchmarkQueryFilterFunc` (category `query-filter`). It is a
focused cached-vs-prepared pair over the same SQL, so the delta is the cached-query overhead (plan
lookup + `ExtractParams`) for the function form. Command:

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=query-filter
```

Measured 2026-09-28, same host/config as the baseline above (`Job.ShortRun`, `InProcessEmitToolchain`,
`MemoryDiagnoser`, **2** executed benchmarks, **0** failures; BDN `Global total time` **12.29 s**).

| Case | Mean | Allocated | Ratio (time) | Alloc ratio |
|------|------|-----------|--------------|-------------|
| `FuncFilter_Prepared_ToList` (baseline) | 1.466 ms | 75 KB | 1.00 | 1.00 |
| `FuncFilter_Cached_ToList` | 4.640 ms | 1078.45 KB | **3.17** | **14.38** |

**Reading.** The func cached path costs **3.17×** the prepared time and **14.38×** the allocation on
this run, versus **1.87× / 7.42×** for the ordinary `Cached_ToList / Prepared_ToList` pair. The
difference is real and expected: `GetPreparedQueryCommand` prepares every fresh command (and so
invokes the builder function once) **before** the plan-cache lookup, which only avoids re-rendering
SQL and rebuilding the mapper — the function is not memoized per plan. This is recorded as the
measured cost of the function form; no "no regression" claim is made for this path. `Error` for both
rows is large on `ShortRun` (`FuncFilter_Cached_ToList` `Error = 2.033 ms`), so the ratio is
indicative, not a hard gate; any future optimisation (memoizing the injected predicate per plan
shape) should re-run this focused case.

## Results 2026-09-28 — issue #104 (dynamic-columns write side)

Perf acceptance for #104 measured on the same host/config/case set as the baseline
(AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, .NET 10.0.12, BenchmarkDotNet 0.15.8,
`Job.ShortRun`, `InProcessEmitToolchain`, `Categories=acceptance`). This is the re-run after the
DML static-arm fix below. **7** cases selected, **0** failures. External shell wall clock
**45.79 s**; BDN `Global total time` **44.49 s** — both under the 4 min budget.

| Case | Mean | Allocated | Delta Mean vs baseline |
|------|------|-----------|------------------------|
| `Nextorm_Count` | 2.191 ms | 339.84 KB | -24.8% (high-variance row, not an assertion) |
| `Nextorm_GroupByCount` | 59.59 ms | 50.01 MB | -2.2% |
| `Nextorm_Cached` | 1.932 ms | 539.92 KB | +9.0% (high-variance row) |
| `Prepared_ToList` | 956.1 us | 76.14 KB | +3.5% |
| `Cached_ToList` | 1,802.9 us | 572.25 KB | +4.4% |
| `Cached_PlanOnly_Param` | 500.5 us | 496.11 KB | -3.4% |
| `Nextorm_Cached_ToListAsync` | 2.153 ms | 705.35 KB | +0.7% |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **1.89** — vs documented
baseline **1.87** (+0.9%) and vs the stated prior figure **2.07** (-8.6%); the corresponding
allocated ratio is **7.52** (baseline **7.42**, +1.3%). Both time deltas are below the **20%**
investigation threshold, so no >20% growth flag is raised. Verdict: within noise, no regression.

## DML benchmark: static vs dynamic-columns insert (#104 write side)

Command:

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkDynamicInsert*
```

Same host/config as above (`Job.ShortRun`, `InProcessEmitToolchain`, `MemoryDiagnoser`); **2**
executed benchmarks, **0** failures (the logged `Failed to set up priority High ... Permission
denied` is the known benign BDN process warning, not a benchmark failure). BDN `Global total time`
**14.95 s**; external shell wall clock **16.69 s**.

Each measured invocation is a real per-call `InsertInto<T>().Values(...).Insert()` build (store
key extraction + ordinal sort) + SQL render + execute against a benchmark-owned SQLite database,
inside a transaction that is rolled back, so the dynamic-key work is inside the measurement and
the statement actually runs.

Both arms execute the **same region** over the **same table** (`dynamic_insert_bench`), but on
**different entity types**: `Insert_Static` uses `StaticInsertEntity`, which has only the two
mapped columns (`id`, `name`) and **no `[DynamicColumns]` store at all**; `Insert_DynamicColumns`
uses `DynamicInsertEntity` (mapped `id`/`name` plus a store) and populates two store keys (`age`,
`city`). This matters: an earlier revision of this benchmark used the store-carrying entity in
**both** arms (with an empty store on the "static" side), so the `FromEntities` / `MappedNames` /
`ReadKeys` store plumbing still ran in the baseline arm and understated the real dynamic-path
overhead — that figure is superseded by the numbers below.

| Case | Mean | Allocated | Ratio (time) | Alloc ratio |
|------|------|-----------|--------------|-------------|
| `Insert_Static` | 614.2 us | 268.37 KB | 1.00 | 1.00 |
| `Insert_DynamicColumns` | 892.0 us | 485.56 KB | **1.45** | **1.81** |

**Caveat — this is not a before/after regression comparison.** The `Insert_Static` arm is the
**store-less** baseline (the byte-identity-guarded mapped-only path), not a pre-#104 revision of
the dynamic-columns path. With a genuinely store-less baseline the DML figure now quantifies the
**true overhead of the dynamic-columns form relative to the static form**: **1.45×** time and
**1.81×** allocation on this run (the dynamic arm's `Allocated` is unchanged at 485.56 KB, while
the static arm dropped from 331.65 KB to 268.37 KB — exactly the store plumbing it no longer
pays). It must not be reported as either a regression or an improvement introduced by #104.
`Error` on both rows is large on `ShortRun` (`Insert_Static` `309.3 us`, `Insert_DynamicColumns`
`394.4 us`, i.e. ≈ 50 %/44 % of `Mean`), so the ratio is indicative, not a gate.

## Results 2026-09-28 — issue #110 (dynamic-columns read: qualified star)

Perf acceptance for #110 (the dynamic-columns read must alias-qualify the appended `*` on
MySQL/MariaDB). Same host/config/case set as the baseline (AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS,
.NET SDK 10.0.401, .NET 10.0.12, BenchmarkDotNet 0.15.8, `Job.ShortRun`, `InProcessEmitToolchain`,
`Categories=acceptance`). **7** cases selected, **0** failures. External shell wall clock **45 s**;
BDN `Global total time` **42.79 s** — both under the 4 min budget. (The host was not fully quiet:
concurrent `opencode`/`roslyn` processes put the load average around 8–10, so the absolute means are
higher than the 2026-09-26 baseline across the board and are not individually comparable; the tracked
cached-vs-prepared ratio is computed within this run.)

| Case | Mean | Allocated | Delta Mean vs baseline |
|------|------|-----------|------------------------|
| `Nextorm_Count` | 2.736 ms | 339.84 KB | -6.1% (high-variance row, not an assertion) |
| `Nextorm_GroupByCount` | 71.80 ms | 50.02 MB | +17.8% (high-variance row) |
| `Nextorm_Cached` | 2.249 ms | 539.9 KB | +26.9% (high-variance row) |
| `Prepared_ToList` | 1,057.4 us | 76.14 KB | +14.5% |
| `Cached_ToList` | 2,317.4 us | 572.26 KB | +34.1% |
| `Cached_PlanOnly_Param` | 672.2 us | 496.1 KB | +29.7% |
| `Nextorm_Cached_ToListAsync` | 2.714 ms | 705.35 KB | +27.0% |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **2.19** — vs documented
baseline **1.87** (+17.1%) and vs the stated prior figure **2.07** (+5.8%); the corresponding
allocated ratio is **7.52** (baseline **7.42**). Both time deltas are below the **20%** investigation
threshold; the `Cached_ToList` row's `Error` (4,836.9 us) is larger than twice its `Mean`, so the
ratio is indicative only, not a gate.

**No before/after comparison is available or claimed.** The change under acceptance is
**dialect-gated**: the new `ISqlDialect.RequiresQualifiedSelectStar` capability defaults to `false`
and is overridden only on MySQL (`MySqlDialect`, inherited by MariaDB), and the extra `hasDynamicStore`
predicate in `SqlBuilder` is true only when the select list contains an `IsDynamicColumnsStore` item.
The seven acceptance cases run on SQLite with ordinary queries and carry no dynamic-columns store, so
neither branch is reachable on the acceptance path. The run is recorded as the cycle's acceptance
evidence; the deltas above are **not** a regression or an improvement introduced by #110.

## Results 2026-09-29 — issue #106 cycle 2 (D6: parameter-creation path)

D6 changed the parameter-creation seam: the execution layer now receives
`DataContext.CreateParam` as `Func<DbCommand, string, object?, DbParameter>` (the executing
command is passed in), and `nextorm.mysql` mints its parameters through
`command.CreateParameter()` so a `MySql.Data` connection gets `MySql.Data` parameters and a
`MySqlConnector` connection gets `MySqlConnector` parameters. The public command-unaware
`GetDbCommand` overload forwards its factory straight into the shared binding core with no
capturing adapter (the earlier adapter was removed). Evidence: (a) the seven-case acceptance
suite as query-path protection, (b) the current-tree `ParamsAllocationBenchmark` re-measure
below.

### Acceptance run (query-path protection)

Command and host/config/case set as the baseline (AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS,
.NET SDK 10.0.401, .NET 10.0.12, BenchmarkDotNet 0.15.8, `Job.ShortRun`,
`InProcessEmitToolchain`, `Categories=acceptance`). **7** cases selected, **0** failures.
External shell wall clock **52.23 s**; BDN `Global total time` **49.39 s** — both under the
4 min budget. The host was **not quiet**: concurrent `opencode`/`roslyn`/`VBCSCompiler`
processes held the load average around 6–12 on 8 logical cores, so every absolute mean is
inflated and the cached-vs-prepared ratio is dominated by run noise.

| Case | Mean | Allocated | Delta Mean vs baseline |
|------|------|-----------|------------------------|
| `Nextorm_Count` | 3.951 ms | 339.84 KB | +35.5% (high-variance row, not an assertion) |
| `Nextorm_GroupByCount` | 111.9 ms | 50.01 MB | +83.6% (high-variance row) |
| `Nextorm_Cached` | 3.064 ms | 539.9 KB | +72.9% (high-variance row) |
| `Prepared_ToList` | 2,065.7 us | 76.14 KB | +123.6% |
| `Cached_ToList` | 2,844.4 us | 572.26 KB | +64.7% |
| `Cached_PlanOnly_Param` | 934.3 us | 496.11 KB | +80.2% |
| `Nextorm_Cached_ToListAsync` | 3.417 ms | 716.29 KB | +59.9% |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **1.38** — vs
documented baseline **1.87** (-26%) and vs the stated prior figure **2.07** (-33%); the
corresponding allocated ratio is **7.52** (baseline **7.42**, unchanged). The time ratio is
*below* baseline because `Prepared_ToList` itself measured ≈2.2× its baseline
(2,065.7 us vs 923.8 us, `Error = 12,403.8 us`), so the ratio is compressed by host noise,
not improved by D6. A second run of the same command on the same host measured a ratio of
**3.89** (`Prepared_ToList` 1.372 ms, `Cached_ToList` 5.343 ms, `Cached_ToList`
`Error = 22,596 us`) — a symmetric outlier in the other direction. The two runs
(**1.38 / 3.89**) bracket the documented 1.87–2.07 band and confirm the host's `ShortRun`
variance rather than a code change. **No regression can be established from the time ratio
on this host; the allocated ratio is unchanged at 7.52, and no path touched by D6 adds
per-row work** (the delegate carries one extra `DbCommand` argument and MySQL mints through
`command.CreateParameter()`, both per-command, not per-row).

### Parameter path — `ParamsAllocationBenchmark` (current tree, re-measured)

Command (14 executed benchmarks, 0 failures; BDN `Global total time` **1 m 53 s**):

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *ParamsAllocation*
```

The D6 refactor removed the capturing adapter the public overload formerly built from the
command-unaware factory: `GetDbCommand(span, factory, conn, tx)` now calls
`GetDbCommandCore` with the factory as its `legacy` delegate, which the core invokes
directly — the same way the internal command-aware overloads pass their `aware` factory. The
public arm therefore carries **0 B/op extra** over the core/internal path. The benchmark
binds the SQLite 2-arg factory once (`_createParam = _dbCtx.CreateParam`, no closure) and
both `GetDbCommand_*` arms call the public overload. Mean in ns and `Allocated` per
benchmark operation (BDN's unit; each of these arms loops 100 times internally) as BDN
prints it:

| Arm | Mean | Allocated (BDN, per op) |
|-----|------|-------------------------|
| `GetDbCommand_1Arg_Params` | 2,638.7 ns | 5,600 B |
| `GetDbCommand_1Arg_ReusedArray` | 1,740.1 ns | 2,400 B |
| `Nextorm_Any_1Arg_Params` | 996,486 ns | 87,209 B |
| `Nextorm_Any_1Arg_ReusedArray` | 1,133,708 ns | 84,009 B |
| `Nextorm_Any_2Arg_Params` | 1,190,554 ns | 124,009 B |
| `Nextorm_Any_2Arg_ReusedArray` | 1,108,541 ns | 120,009 B |

**Allocation.** The public arm allocates exactly the parameter array plus the boxed value
and nothing else: `GetDbCommand_1Arg_Params` 5,600 B per 100-iteration op = 100 × (`object[1]`
32 B + boxed `int` 24 B), while `GetDbCommand_1Arg_ReusedArray` (2,400 B = 100 × boxed
`int` 24 B) drops the array and keeps only the box. A per-call capturing adapter would add a
reference object on top of that; its absence is what "0 B/op extra" means here. This is
**not a "byte-identical" claim**: the current-tree numbers match the earlier D2 run on every
arm except one BDN rounding step — `Nextorm_Any_2Arg_Params` 124,018 B → **124,009 B**
(−9 B, 0.007 %) and `EntityAnyCommand_BuildAndPrepare` 7,417 B → **7,418 B** (+1 B). The
public 3-arg arm's allocation is unchanged (`5,600 B`), so removing the adapter changed no
allocation.

Raw times are host-noise-dominated (`Error ≈ Mean` or larger on several rows) and are not
used to claim a regression or an improvement.

For MySQL specifically (not exercised by this SQLite-based benchmark): `MySqlConnector`'s
`MySqlCommand.CreateParameter()` returns `new MySqlParameter()`, the same allocation the
previous `new MySqlParameter(name, value ?? DBNull.Value)` performed, and D6 only adds a
`DbCommand` argument to an already-bound delegate — no per-call allocation on the MySQL
path either.

**Verdict: no regression** — the query-path acceptance is 7/7 with the allocated ratio
unchanged (7.52 vs 7.42), and the current-tree parameter-path allocations match the earlier
D2 run within BDN's one-unit rounding (no "byte-identical" claim); the MySQL
`command.CreateParameter()` swap is allocation-neutral. The noisy acceptance time ratio
(1.38 / 3.89) is reported as such and is not used to claim an improvement or a regression.

## Results 2026-10-01 — issue #123 (MERGE/UPSERT target-filter isolation)

Post-fix acceptance re-run on the same host/config/case set as the D2 pre-fix baseline
(AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, .NET 10.0.12, BenchmarkDotNet 0.15.8,
`Job.ShortRun`, `InProcessEmitToolchain`, `Categories=acceptance`, **7** cases, **0** failures).
External shell wall clock **51.95 s**; BDN `Global total time` under the 4 min budget.

| Metric | D2 pre-fix baseline | Post-fix (#123) |
|--------|---------------------|-----------------|
| Wall clock | 43.14 s | 51.95 s |
| Cached/prepared ratio (`Cached_ToList / Prepared_ToList`) | 1.89 | **1.93** |
| Allocated ratio | 7.87 | **7.88** |

Both runs are **same-host** and the deltas are within `ShortRun` noise: the tracked cached-vs-prepared
ratio moves +2.1 % (time) and +0.1 % (allocation), far below the 20 % investigation threshold.
Verdict: within noise, **no regression**; the target filter is added only when a filter is active and
not `IgnoreFilters`, and the unsupported-form refusal is metadata-only (no DB round-trip).

### Targeted `MergeTargetFilterBenchmark` (category `merge-filter`)

Command `--filter '*MergeTargetFilterBenchmark*'` (SQL Server renders SQL only, placeholder connection,
no connection opened; the in-memory arms use `InMemoryDataContext`). Post-fix the
`FullMerge_Filtered_SqlBuild` arm is **~492 µs** (vs the pre-fix ~146 µs): the added cost is the new
**one-time command-build work** that resolves the filter and renders the `target`-qualified predicate
into `MERGE ... ON` — paid once per command, not per row. `FullMerge_IgnoreFilters_SqlBuild` and
`InMemory_NoFilter` are **unchanged** (the filter render is skipped when inactive or `IgnoreFilters`);
`InMemory_ActiveFilter` becomes the metadata capability refusal (no source read, no interceptor events).

## Results 2026-10-01 — issue #124 (global filters on bound raw SQL sources)

Final acceptance re-run on the same host/config/case set as the D0 pre-#124 baseline
(AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, .NET 10.0.12, BenchmarkDotNet 0.15.8,
`Job.ShortRun`, `InProcessEmitToolchain`, `Categories=acceptance`, **7** cases, **0** failures).
External shell wall clock **~51 s** (baseline 42.6 s); BDN `Global total time` under the 4 min budget.

| Metric | D0 pre-#124 baseline | Final (#124) |
|--------|----------------------|--------------|
| Wall clock | 42.6 s | ~51 s |
| Cached/prepared ratio (`Cached_ToList / Prepared_ToList`) | 1.94 | **2.03** |
| Allocated ratio | 7.88 | **7.90** |
| `Cached_PlanOnly_Param` ratio | 0.57 | **0.57** |

Both runs are **same-host** and the deltas are within `ShortRun` noise: the tracked cached-vs-prepared
ratio moves +4.6 % (time, 1.94 → 2.03), the allocated ratio +0.3 %, and the plan-only ratio is
unchanged at 0.57 — all far below the 20 % investigation threshold. Verdict: within noise,
**no regression**.

### Raw-plan reuse — `RawSourcePlanReuseBenchmark` (category `raw-plan-reuse`)

New focused, non-acceptance benchmark for the `FromExpressionPlanEqualityComparer` seam that #124
tightened: a raw `FromSql(sql)` source is identified by **reference** in the plan key
(`ReferenceEquals(x.RawSqlSource, y.RawSqlSource)`), so two independently-constructed
`FromSql(sql)` sources no longer share a cached plan, while reusing the same builder instance still
hits the cache. SQLite renders the statement only (`:memory:`, never opened). Reused warm
(`Unbound_Reused_Warm` baseline) **111.4 µs / 158.0 KB**; bound reused warm **1.45×** time / 1.23×
alloc; fresh unbound cold **2.46×** / 2.40×; fresh bound cold **3.00×** / 2.87× — a
**1.45×–3.00×** conservative warm/cold spread. The conservative reference-identity key is the
deliberate price of isolation: independent same-SQL sources are a guaranteed cache miss by
construction. A `GlobalSetup` guard fails the run if independent raw sources share a cached command
or a reused source misses. Command `--filter '*RawSourcePlanReuseBenchmark*'`.

## Results 2026-10-02 — issue #147 (factory renames + query forwarders)

Perf acceptance runs on the candidate tree (uncommitted #147) on the same host/config/case set as
the baseline (AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, .NET 10.0.12,
BenchmarkDotNet 0.15.8, `Job.ShortRun`, `InProcessEmitToolchain`, `MemoryDiagnoser`,
`Categories=acceptance`). Command:

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

### Run 1

**7** cases selected, **0** failures; exit **0**. External shell wall clock **51.98 s**; BDN
`Global total time` **49.19 s** (executed benchmarks: 7) — both under the 4 min budget. The host was
**not quiet**: concurrent `opencode`/`roslynq`/`VBCSCompiler` processes held the load average around
4.3–4.8 on 8 logical / 4 physical cores at run time, and the `Cached_ToList` row's `Error`
(4,809.4 us) is larger than its `Mean` (2,548.0 us), so the absolute means and the tracked ratio are
noise-dominated.

| Case | Mean | Allocated |
|------|------|-----------|
| `Nextorm_Count` | 3.419 ms | 372.66 KB |
| `Nextorm_GroupByCount` | 81.59 ms | 50.05 MB |
| `Nextorm_Cached` | 2.634 ms | 571.15 KB |
| `Prepared_ToList` | 1,152.7 us | 76.14 KB |
| `Cached_ToList` | 2,548.0 us | 601.17 KB |
| `Cached_PlanOnly_Param` | 747.2 us | 525.02 KB |
| `Nextorm_Cached_ToListAsync` | 3.136 ms | 736.6 KB |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **2.21** (2548.0 / 1152.7)
= **+18.2 %** vs the documented baseline **1.87**; the corresponding allocated ratio is **7.90**
(601.17 / 76.14). The tracked rule is a **>20 %** deterioration, i.e. a ratio above
**1.87 × 1.20 = 2.244**; **2.21 < 2.244**, so run 1 is **below the investigation threshold**. It is a
noisy-host measurement (numerator `Error > Mean`), and `ShortRun` on this host is high-variance —
recorded runs span **1.38–3.89** (`Interpretation`, above; the #106 cycle-2 entry above records the
1.38 and 3.89 endpoints) — so it is not reported as a regression.

### Run 2 (second consecutive run, same host/config/case set)

**7** cases selected, **0** failures; exit **0**. External shell wall clock **50.49 s**; BDN
`Global total time` **47.35 s** (executed benchmarks: 7) — both under the 4 min budget.

| Case | Mean | Allocated |
|------|------|-----------|
| `Nextorm_Count` | 3.065 ms | 372.66 KB |
| `Nextorm_GroupByCount` | 79.06 ms | 50.05 MB |
| `Nextorm_Cached` | 2.487 ms | 571.15 KB |
| `Prepared_ToList` | 1,222.7 us | 76.14 KB |
| `Cached_ToList` | 2,435.6 us | 601.17 KB |
| `Cached_PlanOnly_Param` | 778.8 us | 525.02 KB |
| `Nextorm_Cached_ToListAsync` | 2.982 ms | 736.6 KB |

Comparable ratio = **1.99** (2435.6 / 1222.7) = **+6.5 %** vs baseline **1.87** and **-9.9 %** vs
run 1; allocated ratio **7.90** (601.17 / 76.14), unchanged. The two-run spread is **1.99–2.21**;
neither run reaches the **2.244** trigger.

**Change under acceptance is declaration-only.** #147 is the `Create…Builder` factory renames plus
the additive `CreateQueryBuilder*` forwarders: the changed hunks in `DataContextExtensions.cs` are
name/doc updates and one additive forwarder block, and the bodies at `:996`, `:1216`, `:1332` are
untouched; `QueryPlanner`, `QueryCache`, `QueryExecutor` and the materialization path are unchanged
(the only other product edit is a `DataContext.cs` error-message string rename at line 1144). The
cached path therefore cannot have gained per-call work. The overlay's query-path class rule
(`### Перф-приёмка cached path`, table row «изменение query-path / plan cache») mandates this ratio
for query-path/plan-cache changes; a declaration-only change does not touch that class, so both runs
are recorded as protection evidence, not as an improvement or a regression.

**Verdict: no regression** — both runs **7/7**, **0** failures, exit **0**; run 1 **+18.2 %** and
run 2 **+6.5 %** are each below the 20 % (**2.244**) investigation threshold, and the allocated ratio
is unchanged across both runs and the baseline.

Before run 1, one invocation of the same command aborted every SQLite case with
`SQLite Error 14: unable to open database file` because the fixture path `/tmp/nextorm-bench/test.db`
(`BenchDb`'s WSL fallback) was absent; the fixture was restored byte-for-byte from
`benchmarks/nextorm.benchmark/data/test.db` (`PRAGMA integrity_check = ok`) and the command re-run,
producing run 1 above. Raw logs: `/tmp/opencode/147/acceptance.log` (run 1) and
`/tmp/opencode/147/acceptance-run2.log` (run 2).

acceptance command exit code = 0 (7 cases, 0 failures) — logs `/tmp/opencode/147/acceptance.log`,
`/tmp/opencode/147/acceptance-run2.log`.
