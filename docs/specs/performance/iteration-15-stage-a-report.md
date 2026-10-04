# Iteration 15 / #183 — Stage A report: fresh-fluent cached-path decomposition

- Task: #183 (cycle 2, Stage A). Base commit: `01f3e9202ed710f6a459012e80fd2bcfa4c19708`.
- Branch / worktree: `collection/1.0.9-b-5/group-1` / `nextorm-worktrees/1.0.9-b-5/group-1`.
- Design: `docs/specs/performance/iteration-15-cached-path-design.md` (approval `5137ec8`).
- Machine-readable evidence: `docs/specs/performance/iteration-15-stage-a-manifest.json`.
- **No `src/nextorm.core` edit in Stage A**: `git diff --exit-code 01f3e92 -- src/nextorm.core` exits `0`
  (empty diff). `CteHoister` predicate: `git diff --exit-code 01f3e92 -- src/nextorm.core/Builders/CteHoister.cs`
  exits `0` → **G1-CTE changed = false**, so #166 stays deferred with its trigger.
- `scripts/validate_inner_loop.py` is **absent** in this worktree (`ls` → *No such file or directory*);
  the cycle ran under manual inner-loop discipline. `scripts/iteration15_evidence.py` does not replace it.

Stage A changes only the benchmark project (`benchmarks/nextorm.benchmark`), the two
`docs/specs/performance/iteration-15-stage-a-*` files and `scripts/iteration15_evidence.py`.

## 1. Host, runtime, command

- Host: AMD Ryzen 7 5800HS, 8 logical / 4 physical cores, Ubuntu 22.04.5 LTS; load average ≈ 4–6.
- .NET SDK 10.0.401; runtime .NET 10.0.12; BenchmarkDotNet 0.15.8.
- `dotnet-trace` 10.0.745401; **`dotnet-counters` and `dotnet-gcdump` are absent** (recorded).
- Frozen fixture: `/tmp/nextorm-bench/test.db`, sha256 `32bf10a0…2295` (byte-identical to
  `benchmarks/nextorm.benchmark/data/test.db`).
- Canonical acceptance (exactly this command):
  `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
- Workload: `Job.ShortRun` (`IterationCount=3`, `WarmupCount=3`, `LaunchCount=1`),
  `InProcessEmitToolchain`, `MemoryDiagnoser`, `NEXTORM_BENCH_FULL` unset.

## 2. Canonical acceptance (7 cases, 0 failures)

Exit **0**; external wall clock **45.788 s** (≤ 4 min budget); BDN `Global total time`
`00:00:41 (41.87 sec)`; **7** executed benchmarks, **0** failures.
Raw log: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-stage-a/acceptance.log`
(sha256 `928f5537…0b1d`).

| Case | Mean | Allocated |
|------|------|-----------|
| `Nextorm_Count` | 2.618 ms | 354.69 KB |
| `Nextorm_GroupByCount` | 64.52 ms | 50.06 MB |
| `Nextorm_Cached` (`AnyAsync`) | 1.681 ms | 568.04 KB |
| `Cached_PlanOnly_Param` | 532.9 µs | 506.27 KB |
| `Prepared_ToList` | 889.3 µs | 76.14 KB |
| `Cached_ToList` | 1,866.9 µs | 582.42 KB |
| `Nextorm_Cached_ToListAsync` | 2.461 ms | 734.25 KB |

Tracked comparable ratio `Cached_ToList / Prepared_ToList` = **2.10** vs baseline **1.87**
(**+12.3 %**, below the `1.87 × 1.20 = 2.244` trigger); allocated ratio `582.42 / 76.14 = 7.65`
vs baseline `7.42` (+3.0 %). A second run with `--exporters json` measured `2.11` (+13.1 %) and the
same allocated ratio `7.65`. **No >20 % item → no repeat required** (investigate-and-record only).

## 3. Stage A decomposition (timed runs, no diagnostic counters inside)

New class `SqliteBenchmarkStageA` (category `stage-a`), 100 logical ops per invocation. Each arm builds
the command from scratch and stops at a different stage, so stage cost = difference of adjacent arms.
`Where_CachedHit_ToList` and `Where_Prepared_ToList` share the same SQL shape, parameter count and rows,
so the delta is attributable. The two are **normalized-equivalent, not byte-identical**: the cached
closure arm renders the captured argument as `$value`, while the prepared control uses the runtime
placeholder `$norm_p0` (`SqlFunctions.Parameter<int>(0)`). The Join arms use `LargeEntity` on both
sides (matched types, unlike the mixed anonymous/entity projections of the older ad-hoc benches).

| Arm | Mean | Alloc (invocation) | B/op (÷100) |
|-----|-----:|-------------------:|------------:|
| `Where_Construct` | 118.7 µs | 254.4 KB (254 405 B) | 2 544.05 |
| `Where_Prepare_NoHash` | 234.2 µs | 378.9 KB (388 009 B) | 3 880.09 |
| `Where_Prepare_Hash` | 315.3 µs | 425.0 KB (435 211 B) | 4 352.11 |
| `Where_CachedHit_PlanOnly` | 570.1 µs | 637.5 KB (652 817 B) | 6 528.17 |
| `Where_CachedHit_ToList` | 1 920.9 µs | 709.0 KB (725 984 B) | 7 259.84 |
| `Where_Prepared_ToList` | 944.9 µs | 76.1 KB (77 965 B) | 779.65 |
| `Join_Construct` | 523.0 µs | 586.0 KB (600 038 B) | 6 000.38 |
| `Join_Prepare_NoHash` | 917.8 µs | 831.3 KB (851 256 B) | 8 512.56 |
| `Join_Prepare_Hash` | 1 194.0 µs | 874.3 KB (895 263 B) | 8 952.63 |
| `Join_CachedHit_PlanOnly` | 1 827.5 µs | 1 190.0 KB (1 218 482 B) | 12 184.82 |
| `Join_CachedHit_ToList` | 4 370.2 µs | 1 263.6 KB (1 293 931 B) | 12 939.31 |
| `Join_Prepared_ToList` | 963.4 µs | 78.3 KB (80 225 B) | 802.25 |

`Where_Prepared_ToList` (77 965 B) is byte-identical to the acceptance `Prepared_ToList` (77 965 B) —
the matched control is consistent with the acceptance suite.

### Per-stage deltas (adjacent arms)

| Stage | Where time | Where bytes | Join time | Join bytes |
|-------|-----------:|------------:|----------:|-----------:|
| construction | 118.7 µs | 254 405 | 523.0 µs | 600 038 |
| prepare (visitors) | 115.5 µs | 133 604 | 394.8 µs | 251 218 |
| hashing / plan key | 81.1 µs | 47 202 | 276.2 µs | 44 007 |
| lookup + equality + `ExtractParams` | 254.8 µs | 217 606 | 633.5 µs | 323 219 |
| execution (SQLite) | 1 350.8 µs | 73 167 | 2 542.7 µs | 75 449 |

**Reading.** For the primary Join path the largest non-execution allocation is **construction**
(586 KB/invocation), followed by **lookup/equality/parameter refresh** (316 KB) and **prepare/visitors**
(245 KB); hashing is comparatively small (43 KB). Execution dominates *time* (~2.5 ms) but allocates
only ~74 KB. The cached hit overhead vs the prepared control is **+1 138 KB** for Join
(1 218 482 vs 80 225 B) and **+575 KB** for Where (652 817 vs 77 965 B). This is the measured, numeric
basis for ordering B1 (equality-scope allocations) and B2 (guarded parameter-refresh recipe); the
B ordering decision itself belongs to Stage C/ACT.

### Parameter-refresh split (public-surface diagnostic control)

The combined `lookup + equality + ExtractParams` row is split for the simple `Where` shape by a
diagnostic control added to `StageADiagnostics` (public `GetPreparedQueryCommand` hit loop, 2000 warm
iterations, `GC.GetAllocatedBytesForCurrentThread` + `Stopwatch`): a captured local needs a parameter
refresh on every hit (`ExtractParams` re-runs), while the runtime placeholder
`SqlFunctions.Parameter<int>(0)` has the same SQL shape/parameter count/rows and skips it
(`needsParamRefresh = false`). The allocation split is exact and reproducible across three runs:

| Where stage | Alloc (B/op) | ×100 ops |
|---|---:|---:|
| `lookup + equality + ExtractParams` (BDN delta) | 2 176.06 | 217 606 |
| `parameter refresh` (diagnostic delta) | 1 343.92 | 134 392 |
| residual `lookup + equality` (diagnostic-scale) | 832.14 | 83 214 |

The refresh arm's allocation (`6 528.10 B/op`) matches the BDN `Where_CachedHit_PlanOnly` arm and the
control (`5 184.19 B/op`) matches the acceptance `Cached_PlanOnly_Param` arm, so the split is
consistent with the exact BDN numbers. The two control predicates differ in one leaf (captured member
vs runtime method call), so the residual is approximate; the time delta (`≈15.4 µs/op`; the refresh
arm measured 48.3–57.6 µs/op across three runs on this loaded host) is indicative only. Exact
isolation via immutable refresh-recipe instrumentation is deferred to Stage B2 (trigger: the recipe
exists). The Join row stays combined because the diagnostic control covers only the single-table
`Where` shape.

Pooled and unattributed residue remains (JIT, SQLitePCL bind/step, allocator) and is not attributed to
a named stage above.

## 4. Profile findings (dotnet-trace, representative cached-path workload)

Workload (`--stage-a profile`): fresh captured `Join(...).ToList()` + fresh captured `Where(...)`
plan-only, ~240 000 logical ops in 8 s. Traces are under `/tmp/nextorm-bench/stage-a/` (paths + sha256
in the manifest; not committed).

CPU inclusive (`cpu-topN-inclusive.txt`, `Microsoft-DotNETCore-SampleProfiler`):

| Frame | Inclusive |
|-------|----------:|
| `StageADiagnostics.RunProfile` | 49.72 % |
| `JoinCachedToList` | 36.14 % |
| `DataContext.ToList` | 20.06 % |
| `SqliteCommand.ExecuteReader` | 18.95 % |
| `DataContext.GetPreparedQueryCommand` | 12.94 % |
| `QueryPreparer.Prepare` | 12.10 % |
| `QueryPreparer.PrepareColumns` | 7.11 % |
| `WhereCachedPlanOnly` | 6.63 % |

The other ~50 % is the trace host's `TimerQueue.TimerThread` / `WaitHandle.WaitOneNoCheck` (49.8 %),
a **profiler artifact, not application work**. Inclusive CPU is a nested call tree and **must not be
summed as independent stages**. The exclusive report (`cpu-topN-exclusive.txt`) puts `SqliteDataReader.NextResult`
9.1 %, `SqliteParameter.Bind` 4.14 %, `sqlite3_bind_parameter_index` 3.14 % at the top; the hot managed
frames are thin, which is consistent with inlining of the small planning helpers.

Allocation: `alloc.nettrace` (`Microsoft-Windows-DotNETRuntime:0x1:5`) and `gc.nettrace`
(`gc-verbose`) were captured. On this `dotnet-trace` build (10.0.745401) the Speedscope conversion
renders a single evented, sampled-CPU-style thread profile (314 frames) and **does not surface
`GCAllocationTick` type payloads**, so allocation attribution is taken from BenchmarkDotNet
`MemoryDiagnoser` exact `BytesAllocatedPerOperation` (Section 3). The trace type-payload gap is
recorded as **unattributed residue**, not hidden.

## 5. Hit / plan / parameter proofs (diagnostic batches)

`--stage-a diagnostics` (no timed operation contains counters), raw JSON in the manifest:

- `Where` captured closure: 1 miss / **99 hits**, `distinctSql = 1`, `needsParamRefresh = true`,
  100 distinct parameter values seen, **0 mismatches** → parameter refresh on hit proved.
- `Join` captured closure: 1 miss / **99 hits**, `distinctSql = 1`, `needsParamRefresh = true`,
  100 distinct values, **0 mismatches**.
- `Where` runtime placeholder (`SqlFunctions.Parameter<int>(0)`): 1 miss / 99 hits,
  `needsParamRefresh = false`, 1 parameter value → the no-`ExtractParams` control.
- `Where` constant (`id == 5`): 1 miss / 99 hits, `noParams = true` (no parameter) control.
- `paramRefreshAttribution`: refresh-needed (`ExtractParams`, captured local) vs no-refresh (runtime
  `SqlFunctions.Parameter<int>(0)`) cached-hit loops; allocation delta **1 343.92 B/op** (exact across
  three runs), time delta indicative (≈15.4 µs/op). See §3 for the split.

The hit proof uses the public `GetPreparedQueryCommand` result identity (a hit returns the cached
command instance) — the same mechanism the production plan-cache tests assert.

## 6. Correctness boundary

`--stage-a correctness` (`--scope boundary`): `where_sql_match`, `join_sql_match`,
`where_rows_match`, `join_rows_match`, `any`, `first`, `single`, `where_rows`,
`sticky_cache_false_absent` — **all true**, `all_pass = true`. No sticky
`queryCommand.Cache = false` / `_dontCache` is introduced (the benchmark never sets it).

## 7. Frozen budgets (exact, before any core edit)

Normalization: `B/op = Memory.BytesAllocatedPerOperation / 100` logical ops. Budget =
`ceil(observed × 1.20)` B/op (a `>20 %` allocation-growth budget aligned with the acceptance rule).
`Prepared_ToList` stays at the existing **no-growth 780 B/op** (`eng/perf/iteration14-budgets.json`).

| Arm | Observed B/op | Frozen budget B/op |
|-----|--------------:|-------------------:|
| `Where_Construct` | 2 544.05 | 3 053 |
| `Where_Prepare_NoHash` | 3 880.09 | 4 657 |
| `Where_Prepare_Hash` | 4 352.11 | 5 223 |
| `Where_CachedHit_PlanOnly` | 6 528.17 | 7 834 |
| `Where_CachedHit_ToList` | 7 259.84 | 8 712 |
| `Join_Construct` | 6 000.38 | 7 201 |
| `Join_Prepare_NoHash` | 8 512.56 | 10 216 |
| `Join_Prepare_Hash` | 8 952.63 | 10 744 |
| `Join_CachedHit_PlanOnly` | 12 184.82 | 14 622 |
| `Join_CachedHit_ToList` | 12 939.31 | 15 528 |
| `Prepared_ToList` | 779.65 | 780 (no growth) |
| `Join_Prepared_ToList` | 802.25 | 963 |

Time is **not** frozen: `ShortRun` on this host is noise-dominated (several rows have `Error > Mean`).
Stage B acceptance uses two paired/interleaved runs on the same host/job/config, per the design.

## 8. Limitations and handoff

- Profiler perturbation and inlining are present; managed hot frames are thin (see Section 4).
- Allocation stacks were not obtainable from `dotnet-trace` on this version (type payload gap);
  per-stage exact bytes come from `MemoryDiagnoser`.
- No production measurement seam was missing: every stage is observable with the public command /
  prepared-command surface, so **no DO→PLAN candidate for a core seam was raised** and no unplanned
  instrumentation was added.
- Placeholder rendering differs between the timed cached arms (captured argument → `$value`) and the
  prepared controls (runtime `SqlFunctions.Parameter<int>(0)` → `$norm_p0`). The arm comparison is
  therefore **normalized equivalence** (same predicate shape, parameter count and rows), not
  byte-identical SQL text; the acceptance correctness check `where_sql_match` compares like-for-like
  placeholder forms and remains true.
- `lookup/equality` and parameter-refresh are separated only approximately with public-only
  instrumentation: the no-refresh control differs from the captured arm in one predicate leaf, so the
  refresh delta carries that leaf's lookup/equality residual (`832.14 B/op` residual). Exact isolation
  (immutable refresh-recipe instrumentation) is **deferred to Stage B2**, trigger: the recipe exists.
- Raw SQL (`WithSql`/`PrepareFromSql`, `RawSqlOverride`) is a public query surface but not a refresh
  fastpath: the raw-SQL plan identity is the injected SQL text, the prepared `SqlStmt` is null, and a
  raw-SQL cache hit skips `ExtractParams`. Characterization test
  `CachedPathCharacterizationTests.RawSqlOverride_FormAndValueChange_ShouldKeyThePlanBySqlTextOnly`.
  Converter/rawSQL/nested/IN fallback preservation is a mandatory B2 obligation.
- Stage A is a measurement deliverable only; it claims no numeric improvement and does not start B.
  Handoff to B1/B2 with the ordering decision is Stage C/ACT.

## 9. Suppression/slop audit (Stage A changed files)

Full Stage A changed-file set reviewed: 6 `.cs` files (**1391** total lines) plus
`scripts/iteration15_evidence.py` (632 lines; scanned, 0 matches for every C#-specific pattern).
Command: `rg -c -- '<pattern>' <files>`. Ratios are `count / 1391`.

| pattern | count | ratio | locations |
|---|---:|---:|---|
| `#pragma warning` | 0 | 0.00000 | — |
| `SuppressMessage` | 0 | 0.00000 | — |
| `NoWarn` | 0 | 0.00000 | — |
| `TODO`/`FIXME`/`HACK`/`XXX` | 0 | 0.00000 | — |
| `Debug.Assert` | 0 | 0.00000 | — |
| `async void` | 0 | 0.00000 | — |
| empty catch (`catch { }`) | 0 | 0.00000 | — |
| `Thread.Sleep` | 0 | 0.00000 | — |
| `GC.Collect` | 0 | 0.00000 | — |
| `Console.Write` | 4 | 0.00288 | `StageADiagnostics.cs:89,90,91` (STAGE-A JSON emit channel), `Program.cs:36` (commented-out dev line) |

Files: `benchmarks/nextorm.benchmark/Program.cs` (36), `benchmarks/nextorm.benchmark/SqliteBenchmarkStageA.cs`
(198), `benchmarks/nextorm.benchmark/StageADiagnostics.cs` (429),
`tests/nextorm.core.tests/PlanKeyStructureTests.cs` (336),
`tests/nextorm.sqlite.tests/DataContextCacheClearTests.cs` (100),
`tests/nextorm.sqlite.tests/CachedPathCharacterizationTests.cs` (292). All suppression/slop markers are
**zero**; the four `Console.Write` hits are the intended benchmark/diagnostics stdout plus one commented
line. No register delta: `git diff --exit-code 01f3e92 -- docs/specs/design` is **empty** (exit 0); no
public API change and no claims added to `API-NAMING-REVIEW.md` / `code-smells-review.md`.
