# T249 acceptance benchmarks: cached query path (pre-T249 vs T249)

Internal performance record for task T249 / issue #249 (metadata-driven `LoadWith`, D6b). The contract it
applies — seven cases, `0` failures, the cached-vs-prepared ratio, the `>20 %` investigate rule — lives in
`acceptance-benchmarks.md`; this page records one paired measurement only. Internal page: never linked
from public docs.

## Method

- **Baseline tree:** a detached worktree of the *same* repository at `7203db77` (`#251 composite
  relationship declaration, ordered metadata and full-component SQL navigations`) — the pre-T249 tree with
  T251 already landed, i.e. exactly the comparable tree the plan requires. Created with
  `git worktree add --detach /tmp/opencode/t249-perf-baseline 7203db77`, never modified, removed with
  `git worktree remove --force` after the measurement.
- **Candidate tree:** `/home/alex/sources/nextorm-worktrees/rc4/g1`, branch
  `collection/rc4/task-1-18`, same base commit `7203db77` plus the uncommitted T249 change set.
- **Command (identical in both trees, verbatim):**
  `flock -w 3600 /tmp/opencode/nextorm-bdn-host.lock dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
  — the exclusive host lock serializes this lane against every other BenchmarkDotNet run on the machine,
  so no foreign benchmark competes for the 8 logical cores during either measurement.
- **Host/config:** AMD Ryzen 7 5800HS 3.19 GHz, Ubuntu 22.04.5 LTS (Jammy Jellyfish), .NET SDK
  10.0.401 / runtime 10.0.12, BenchmarkDotNet 0.15.8, `Job.ShortRun`
  (`IterationCount=3`, `WarmupCount=3`, `LaunchCount=1`) with `InProcessEmitToolchain`, `MemoryDiagnoser`
  on, `Categories=acceptance`. `NEXTORM_BENCH_FULL` unset (so the quick job, not `Job.Default`).
  Both runs used the shared `data/test.db` SQLite file (`/tmp/nextorm-bench/test.db`), so the candidate
  and baseline read the same rows; no containers and no external database were involved.
- **Case set:** `--anyCategories=acceptance` selected exactly **7** cases and **0** failures in both
  runs (BDN `Global total time … executed benchmarks: 7`): `InMemoryBenchmarkAggregates.Nextorm_Count`,
  `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount`, `SqliteBenchmarkAny.Nextorm_Cached`,
  `SqliteBenchmarkCachedPlan.{Prepared_ToList,Cached_ToList,Cached_PlanOnly_Param}`,
  `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`.
- BenchmarkDotNet's own generated reports for both runs were copied to
  `.pdca/T249/rv2/perf/{baseline,candidate}-artifacts/`; in the candidate worktree the tracked
  `benchmarks/BenchmarkDotNet.Artifacts` tree was restored to its `HEAD` state after the run, so the
  working tree carries no benchmark output.

## Wall clock

| run | BDN internal `Global total time` | external shell wall clock (`/usr/bin/time`) | budget |
|---|---|---|---|
| baseline (`7203db77`) | **47.11 s** | **74.72 s** | ≤ 240 s |
| candidate (T249) | **49.35 s** | **69.65 s** | ≤ 240 s |

The two figures are distinct by design: the BDN number is the measured benchmark execution, the external
number additionally contains `dotnet run` restore/build/startup. Both are far inside the four-minute
budget in both trees; the candidate's external wall clock is *lower* than the baseline's, i.e. the whole
run including the Release rebuild is not slower with T249.

## Per-case results

Means and allocations exactly as BDN printed them; `cand/base` is the candidate mean divided by the
baseline mean measured on the same host minutes apart.

| # | Case | Baseline Mean | Candidate Mean | cand/base (time) | Baseline Allocated | Candidate Allocated | cand/base (alloc) |
|---|---|---|---|---|---|---|---|
| 1 | `Nextorm_Count` | 2.351 ms | 2.572 ms | **1.094** | 320.31 KB | 320.31 KB | 1.000 |
| 2 | `Nextorm_GroupByCount` | 58.52 ms | 56.02 ms | **0.957** | 50.07 MB | 50.04 MB | 0.999 |
| 3 | `Nextorm_Cached` | 2.712 ms | 1.954 ms | **0.720** | 533.63 KB | 533.67 KB | 1.000 |
| 4 | `Prepared_ToList` | 953.4 µs | 874.7 µs | **0.917** | 76.14 KB | 76.14 KB | 1.000 |
| 5 | `Cached_ToList` | 1,567.3 µs | 1,482.6 µs | **0.946** | 444.9 KB | 433.96 KB | 0.975 |
| 6 | `Cached_PlanOnly_Param` | 424.0 µs | 389.3 µs | **0.918** | 368.76 KB | 357.82 KB | 0.970 |
| 7 | `Nextorm_Cached_ToListAsync` | 1.942 ms | 1.927 ms | **0.992** | 553.78 KB | 553.77 KB | 1.000 |

Raw error/standard deviation, for the noise discussion below (BDN hid `Error`/`StdDev` for cases 3 and 7
in its summary table; the detailed sections report them):

| Case | Baseline Error / StdDev | Candidate Error / StdDev |
|---|---|---|
| `Nextorm_Count` | 0.5044 ms / 0.0276 ms | **9.491 ms / 0.5202 ms** |
| `Nextorm_GroupByCount` | 21.11 ms / 1.157 ms | 5.367 ms / 0.294 ms |
| `Nextorm_Cached` | 2.646 ms / 0.1450 ms | 0.7252 ms / 0.0398 ms |
| `Prepared_ToList` | 393.4 µs / 21.56 µs | 51.07 µs / 2.80 µs |
| `Cached_ToList` | 186.5 µs / 10.22 µs | 958.86 µs / 52.56 µs |
| `Cached_PlanOnly_Param` | 188.1 µs / 10.31 µs | 113.60 µs / 6.23 µs |
| `Nextorm_Cached_ToListAsync` | — / — | — / — |

## Cached vs prepared (the tracked ratio)

Both sides run the identical SQLite execution, so the DB cost cancels out and the ratio is comparable
across the two runs.

| | Mean | Ratio vs `Prepared_ToList` |
|---|---|---|
| Baseline `Prepared_ToList` (1.00) | 953.4 µs | — |
| Baseline `Cached_ToList` | 1,567.3 µs | **1.644** (BDN `Ratio` column 1.64) |
| Candidate `Prepared_ToList` (1.00) | 874.7 µs | — |
| Candidate `Cached_ToList` | 1,482.6 µs | **1.695** (BDN `Ratio` column 1.69) |

- **Time ratio: 1.644 -> 1.695 (+3.1 %).**
- **Allocation ratio: 5.84 -> 5.70 (−2.4 %)** (`444.9 KB / 76.14 KB` -> `433.96 KB / 76.14 KB`).

Both are far below the published contract baseline (1.87 time / 7.42 allocation) and inside the
host's own run-to-run band; the candidate's absolute cached and prepared means are both *lower* than the
baseline's.

> Note on the number quoted in the D6b hand-off: the summary line there said `1.670 -> 1.695`. The
> baseline run's own `Ratio` column is **1.64** (`1,567.3 / 953.4 = 1.644`), and that is what this page
> uses; 1.670 does not correspond to any measured pair in `perf/baseline.log`.

## `>20 %` rule

**No comparable case grew by more than 20 %; no repeat run was therefore required, and none was made.**

- The largest single-case movement is `Nextorm_Count` at **+9.4 %**, and that case is the known
  high-variance row of this suite: its candidate `Error` (9.491 ms) exceeds its own `Mean` (2.572 ms), and
  `acceptance-benchmarks.md` records it as measurement-only, never a regression assertion.
- All other cases moved **down** (−0.8 % … −28.0 %); `Nextorm_Cached`'s −28 % is the mirror image of its
  baseline noise (`Error` 2.646 ms > `Mean` 2.712 ms in that run) and is not claimed as a T249
  improvement.
- The tracked cached/prepared ratio moved +3.1 % (time) and −2.4 % (allocation).

**Sub-~20 % deltas on this host are noise, not signal.** `Job.ShortRun` with three iterations on a
shared 8-logical-core developer machine has run-to-run spread of this order (the suite's own baseline
rows show 5–20 % `Error` on the in-memory cases, and earlier T-series records move individual cases by
tens of percent between runs of the same tree). Any comparison of the individual rows below ~20 % should
be read as "no measured regression", not as an improvement claim; only the tracked ratio and a >20 %
movement are treated as findings.

## Evidence

| artifact | path |
|---|---|
| baseline run (full BDN console output) | `.pdca/T249/rv2/perf/baseline.log` |
| candidate run (full BDN console output) | `.pdca/T249/rv2/perf/candidate.log` |
| external wall clock, both runs | `.pdca/T249/rv2/perf/{baseline,candidate}.wall-seconds` |
| BDN generated reports, both runs | `.pdca/T249/rv2/perf/{baseline,candidate}-artifacts/` |

Reproduce with the command in **Method**; a run that selects fewer than seven cases is incomplete and must
not be compared with these numbers (see `acceptance-benchmarks.md`).
