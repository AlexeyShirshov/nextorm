# Measured results: #309 `JoinInto` child `Where` fold (2026-10-25)

Task T309 / issue [#309](https://github.com/AlexeyShirshov/nextorm/issues/309). Internal
record; this file is not linked from public documentation and is excluded from the DocFX
build (`docs/docfx.json` excludes `specs/**`).

The change touches the query path — `EntityBuilder.cs` terminal `JoinInto` overloads now fold
the child source's own `Where` into the join predicate (`FoldJoinIntoChildPredicate`), and
`JoinIntoManyToManySpec.Create` normalizes the M2M predicate before the spec is captured — so
the acceptance benchmark is **required** (`docs/specs/performance/acceptance-benchmarks.md`,
7 cases, ≤240 s). Only the two production files of the T309 footprint changed; no benchmark,
job, config or case set was modified between the two runs.

## Runs

Identical invocation, host, environment, job and case set; only the tree differs.

```bash
cd /home/alex/sources/nextorm-worktrees/rc4/g18
flock -x /tmp/opencode/nextorm-bdn-host.lock \
  timeout 240s dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

| Half | Tree | Log / summary | Exit | Wall | BDN selection |
|---|---|---|---:|---:|---|
| Baseline | pre-fix (`15a4d0bc` state of the two files) | `.pdca/T309/rv1/perf/baseline/` | 0 | 58 s | 7 executed, 0 failures |
| Fixed | T309 fix applied | `.pdca/T309/rv1/perf/fixed/` | 0 | 70 s | 7 executed, 0 failures |

Both halves: BenchmarkDotNet 0.15.8, Linux Ubuntu 22.04.5 LTS, AMD Ryzen 7 5800HS
(1 CPU, 8 logical / 4 physical cores), .NET 10.0.12, SDK 10.0.401, `Job.ShortRun`
(`IterationCount=3`, `LaunchCount=1`, `WarmupCount=3`, `Toolchain=InProcessEmitToolchain`),
`Categories=acceptance`. Wall time is the external shell duration (includes `dotnet run`
build/startup), not BDN's `Global total time`.

## Baseline and fixed tables

| # | Method | Baseline Mean | Fixed Mean | Fixed / baseline | Baseline Allocated | Fixed Allocated |
|---|---|---:|---:|---:|---:|---:|
| 1 | `Nextorm_Count` | 3.238 ms | 2.483 ms | 0.767 | 320.31 KB | 320.31 KB |
| 2 | `Nextorm_GroupByCount` | 118.2 ms | 61.85 ms | 0.523 | 50.04 MB | 50.04 MB |
| 3 | `Nextorm_Cached` | 2.347 ms | 2.229 ms | 0.950 | 550.84 KB | 550.84 KB |
| 4 | `Prepared_ToList` | 1,131.2 us | 1,046.3 us | 0.925 | 76.14 KB | 76.14 KB |
| 5 | `Cached_ToList` | 3,628.6 us | 1,787.4 us | 0.493 | 441.78 KB | 441.78 KB |
| 6 | `Cached_PlanOnly_Param` | 693.2 us | 433.3 us | 0.625 | 365.64 KB | 365.63 KB |
| 7 | `Nextorm_Cached_ToListAsync` | 5.060 ms | 2.145 ms | 0.424 | 553.8 KB | 553.78 KB |

Per-case `Error` / `StdDev` for both halves are transcribed in the two
`perf/{baseline,fixed}/summary.md` files next to their logs.

Cached-vs-prepared ratios of the pair: `Cached_ToList / Prepared_ToList` **3.22 → 1.71**
(`Prepared_ToList` is the declared baseline of that shared table). `Cached_PlanOnly_Param /
Prepared_ToList` **0.61 → 0.41** divides two different workloads (no-database plan-cache
lookup vs SQLite execution) and stays **not comparable**, as the acceptance contract states.

## Verdict

**No comparable ratio growth >20 %.** The largest per-case ratio is **0.950**
(`SqliteBenchmarkAny.Nextorm_Cached`, −5.0 %); every other case is at or below the baseline and
allocated memory is unchanged within BDN's rounding (`365.64 → 365.63 KB` only). The
cached/prepared ratio improved (3.22 → 1.71).

No improvement is *claimed* for the fold: `JoinInto` is not exercised by any of the seven
acceptance cases, and `Nextorm_GroupByCount` (−47.7 %) and `Cached_ToList` (−50.7 %) move
exactly as much as the untouched in-memory/`Where` cases, which identifies these deltas as
session/`ShortRun` drift on this host rather than an effect of #309 — the same pattern the
#299 run recorded. The recorded noise is unchanged: `Nextorm_Count` has `Error` > `Mean` in
both halves (4.208 > 3.238 ms; 2.107 > 2.483 ms) and is measurement-only.

The interpretation rule is the published one — a comparable rise of **>20 %** is
investigate-and-record, never an automatic hard fail, and no threshold is invented here;
`ShortRun`'s 3 iterations leave several cases with large `Error`, so runs are comparable only
on this host with this config and case set.
