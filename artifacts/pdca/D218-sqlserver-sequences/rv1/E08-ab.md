# E08 A/B — T218 (#218) preparation-path control, pre-D1 vs HEAD (escalate n=3)

Decisive comparable measurement requested by the escalate decision: the D0-vs-post comparison changed
both the benchmark **session** and the product revision, so the reproducible `+~5 KB` allocation shift
on the unchanged `patindex` control could not be attributed. This run isolates it with an A/B pair in
**one session, alternating**, on the same job and filter.

## Setup

- **A (pre-D1)** — detached throwaway worktree `/tmp/opencode/t218-ab` at `f8caa690` (`22b96233^`, the
  state before the whole T218 DO). The HEAD benchmark file is copied in as untracked in a
  **control-only projection** (`Prepare_Control_Patindex_Uncached` + `T218PreparationRow.Control`
  only): the four sequence cases are omitted because `next_value_for`/`nextval` do not exist pre-D1, so
  the same benchmark project compiles. The control method, its `[BenchmarkCategory("t218-preparation")]`,
  the 100-iteration loop and the query shape are identical to HEAD. Nothing is committed in the
  worktree; the main worktree's `src/` is untouched.
- **B (HEAD)** — `/home/alex/sources/nextorm` at the corrective revision.
- Both trees build `benchmarks/nextorm.benchmark -c Release` with identical `Directory.Packages.props`,
  `global.json` and `Directory.Build.props` (`dotnet build … -c Release` exit 0, 0 Warning(s)/0
  Error(s): `logs/e08-ab-A-build.log`, `logs/e08-ab-B-build.log`).
- Same job (BDN `ShortRun(Toolchain=InProcessEmitToolchain, IterationCount=3, LaunchCount=1,
  WarmupCount=3)`, `[MemoryDiagnoser]`) and same filter, alternating **A B A B A B**:

```
["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--",
 "--anyCategories=t218-preparation","--filter","*Prepare_Control_Patindex*"]
```

## Measurements — `Prepare_Control_Patindex_Uncached`

| pair | side | rev | Mean | Error | StdDev | Gen0 | Allocated | log |
|---|---|---|---|---|---|---|---|---|
| 1 | A pre-D1 | `f8caa690` | 969.9 us | 2,775.4 us | 152.1 us | 93.7500 | **784.39 KB** | `logs/e08-ab-A1.log` |
| 1 | B HEAD | corrective | 2.473 ms | 18.170 ms | 0.9959 ms | 93.7500 | **789.53 KB** | `logs/e08-ab-B1.log` |
| 2 | A pre-D1 | `f8caa690` | 789.3 us | 977.4 us | 53.57 us | 93.7500 | **784.39 KB** | `logs/e08-ab-A2.log` |
| 2 | B HEAD | corrective | 2.380 ms | 13.680 ms | 0.7498 ms | 93.7500 | **789.53 KB** | `logs/e08-ab-B2.log` |
| 3 | A pre-D1 | `f8caa690` | 2.395 ms | 17.806 ms | 0.9760 ms | 93.7500 | **784.46 KB** | `logs/e08-ab-A3.log` |
| 3 | B HEAD | corrective | 2.412 ms | 25.454 ms | 1.395 ms | 93.7500 | **789.53 KB** | `logs/e08-ab-B3.log` |

All six runs exit 0, 1 benchmark selected each. Every run logs the BDN `MinIterationTime` warning
(the workload is shorter than BDN's 100 ms floor), so the **Mean is noisy** and is not the oracle here;
the allocation column is bit-stable within each side.

## Interpretation (per the routed decision)

- **Allocations are not equal.** A (pre-D1) is stable at **784.39 / 784.39 / 784.46 KB**; B (HEAD) is
  bit-identical at **789.53 KB** in all three runs. Delta **≈ +5.14 KB (+0.66 %)** in every pair, with
  identical Gen0 (93.7500).
- The signal **tracks the product source state**, not the session: alternating A/B in one session
  always splits A≈784.4 / B≈789.5. Therefore the delta is **attributable to the D1 change** (the
  control `patindex` flows through the D1-modified `SqlServerScalarFunctionTranslator`), i.e. a real
  allocation on the query-preparation path.
- Confound closed: A uses a control-only projection whose `T218PreparationRow` has one property, yet
  its allocation is **bit-identical to the D0 baseline `784.39 KB`** taken with the full five-case
  file — the row-type/benchmark-file difference does not move the measured allocation, so the delta is
  not a benchmark-binary artifact.
- The earlier `789.47 KB` post-change readings (no `--filter`, 5-case run) and these `789.53 KB` reads
  are the same stable side (sub-0.1 KB display rounding); both are clearly separated from ≈784.4 KB.

**Outcome: `gap/incomplete, product delta ≈5 KB`.** The control allocation criterion is a
**discriminating oracle** and it fails: no `src` change is made here. The obligation (remove the ≈5 KB
D1 allocation on the translator query-preparation path, explicit contract supersession, preserve
R1–R7, no allocation tolerance) is handed to the planner (`r=2`). E08 stays **open**.
