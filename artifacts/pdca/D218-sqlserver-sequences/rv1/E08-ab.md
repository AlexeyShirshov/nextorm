# E08 A/B — T218 (#218) preparation-path control, pre-D1 vs HEAD (escalate n=3)

> **INCOMPARABLE — superseded, kept for history only.** The A/B in this first section was measured
> with an **unmatched** BDN job: side A normalized **128 op** per iteration in pairs 1-2 (64 op in
> pair 3) while side B normalized **64 op** in every run, so the two sides did not iterate on the same
> counts and the `+5.14 KB` reading cannot be attributed. It is **not** evidence of a product delta.
> The decisive, op-count-matched re-measurement is the **“Matched-job re-measurement (escalate step
> 2)”** section at the end of this file; it also shows the op-count mismatch was *not* the cause of
> the gap — the driver was the benchmark binary.

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
> **Annotation (2026-10-14, ACT routing) — superseded:** this `discriminating oracle … and it fails` claim is superseded by `E08-ab.md:164-172` (matched-job isolation: mode 0.00 KB, mean −1.56 KB, 98.8 %) and by `E08-disposition.md:40-41` (the control oracle is **non-discriminating** at this granularity); annotation only — no number changed, nothing deleted.
D1 allocation on the translator query-preparation path, explicit contract supersession, preserve
R1–R7, no allocation tolerance) is handed to the planner (`r=2`). E08 stays **open**.

---

## Matched-job re-measurement (escalate step 2, r=1 / n=3)

Appended on the escalate-directed step 2. The first section above is **incomparable** (unmatched op
counts) and is kept only for history; the measurement below is decisive.

### Forced job — identical on both sides

The job is pinned on the command line so BDN cannot auto-pick different iteration counts for the two
sides (the pilot stage is skipped when `InvocationCount` is set), and the same filter selects only the
control case (the four sequence cases are excluded):

```
["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--",
 "--anyCategories=t218-preparation","--filter","*Prepare_Control_Patindex*",
 "--invocationCount","64","--unrollFactor","1","--launchCount","1","--warmupCount","3",
 "--iterationCount","5","--strategy","Throughput"]
```

**Every one of the 23 runs below reports `WorkloadResult … : 64 op` on every measured iteration** —
the normalized op counts are identical on both sides, so the `Allocated` column sits on the same
measurement grid. All 23 runs exit 0 and select exactly 1 benchmark. (Confirmed in the logs: with
`InvocationCount=64` there is no `WorkloadPilot` stage, unlike the incomparable run above.)

### (a) Re-run of the earlier (confounded) A/B on the matched job

A = throwaway worktree `/tmp/opencode/t218-ab` at `f8caa690` (pre-D1 src) with the **control-only**
projection; B = main worktree at HEAD (`0911f850`) with the **full five-case** file, filtered to the
control case.

| pair | side / benchmark binary | Mean | Gen0 | Allocated | op | log |
|---|---|---|---|---|---|---|
| 1 | A ctrl-only file | 2.761 ms | 93.7500 | 784.46 KB | 64 | `logs/e08m-Ac1.log` |
| 1 | B 5-case file | 2.959 ms | 93.7500 | 792.66 KB | 64 | `logs/e08m-B1.log` |
| 2 | A ctrl-only file | 2.890 ms | 93.7500 | 784.46 KB | 64 | `logs/e08m-Ac2.log` |
| 2 | B 5-case file | 2.623 ms | 93.7500 | 800.47 KB | 64 | `logs/e08m-B2.log` |
| 3 | A ctrl-only file | 2.762 ms | 93.7500 | 784.92 KB | 64 | `logs/e08m-Ac3.log` |
| 3 | B 5-case file | 2.961 ms | 93.7500 | 789.53 KB | 64 | `logs/e08m-B3.log` |

Means: A **784.61 KB** vs B **794.22 KB** (Δ **+9.61 KB**). Matching the op counts did **not** remove
the gap, so the earlier 128-vs-64 mismatch was **not** the cause of the `+5.14 KB`. The confound it
exposed instead is that the two sides run **different benchmark binaries** — A compiles a
one-property row type, B the five-property row type needed by the sequence cases.

### (b) Isolation — identical benchmark source on both sides, only the product src differs

The same control-only benchmark file is dropped into a second throwaway worktree at **HEAD**
(`/tmp/opencode/t218-b`, `0911f850`) as an untracked file, so both sides build the *identical*
benchmark source and differ **only** in `src/` (pre-D1 vs HEAD). Alternating `A B' A B' …`, 7 pairs:

| pair | A pre-D1 `f8caa690` | B' HEAD `0911f850` | Δ | op (A / B') | logs |
|---|---|---|---|---|---|
| 1 | 784.46 KB | 784.52 KB | +0.06 KB | 64 / 64 | `logs/e08m-A1.log` / `logs/e08m-Bp1.log` |
| 2 | 795.44 KB | 795.39 KB | -0.05 KB | 64 / 64 | `logs/e08m-A2.log` / `logs/e08m-Bp2.log` |
| 3 | 795.39 KB | 787.58 KB | -7.81 KB | 64 / 64 | `logs/e08m-A3.log` / `logs/e08m-Bp3.log` |
| 4 | 784.46 KB | 784.46 KB | 0.00 KB | 64 / 64 | `logs/e08m-A4.log` / `logs/e08m-Bp4.log` |
| 5 | 784.46 KB | 784.46 KB | 0.00 KB | 64 / 64 | `logs/e08m-A5.log` / `logs/e08m-Bp5.log` |
| 6 | 787.58 KB | 784.46 KB | -3.12 KB | 64 / 64 | `logs/e08m-A6.log` / `logs/e08m-Bp6.log` |
| 7 | 784.46 KB | 784.46 KB | 0.00 KB | 64 / 64 | `logs/e08m-A7.log` / `logs/e08m-Bp7.log` |

Means: A **788.04 KB** vs B' **786.48 KB** → Δ **-1.56 KB** (**mode 0.00 KB**, 4 of 7 pairs exactly
equal). Mean-time ratio B'/A = 2.819 / 2.854 ms = **98.8 % (≤105 %)**. Both sides sample the *same*
set of readings (784.46 / 787.58 / 795.4 KB): with the benchmark binary held constant, pre-D1 and HEAD
are indistinguishable.

### (c) Harness-swap control — same revision, same path, different benchmark file

Inside the single HEAD worktree `/tmp/opencode/t218-b` (revision `0911f850`, unchanged path) only the
benchmark **file** was swapped, rebuilding each time:

| benchmark file in the same HEAD build | Allocated (×3) | op |
|---|---|---|
| control-only (as in A) | 784.46 / 795.44 / 795.39 KB | 64 |
| full five-case file | 789.53 / 789.53 / 789.53 KB | 64 |

Swapping the benchmark binary at an **unchanged product revision** moves the reading by up to ~11 KB —
the same magnitude as the entire gap under investigation. Logs: `logs/e08m-Bp*.log` vs
`logs/e08m-Bfull{1,2,3}.log`; builds `logs/e08m-{A,B,Bp,Bfull}-build.log` (all exit 0, 0/0).

### Verdict — E08 **met** (closed; no product regression)

- The matched job removes the earlier 128-vs-64 incomparability, and the gap still does not close →
  the `+5.14 KB` was never an op-count artifact.
- The driver is the **benchmark binary**, not the product: with the benchmark source held constant the
  product revision moves the reading by **−1.56 KB mean / 0.00 KB mode** (mean time **98.8 %** of the
  pre-D1 side, ≤105 %); with the product revision held constant, swapping the benchmark file moves the
  reading by several KB.
- This agrees with the PLAN gather: the D1 diff adds **no per-call allocation mechanism** on the
  shared `patindex` path (new name-sets are `static readonly`, class-init-once; `SequenceFunctions`
  `Contains` and the `Supports` `is`-pattern allocate nothing).
- The **D0 single-reading baseline (`784.39 KB`, taken with the control-only harness) is
  incomparable** to the later five-case-harness readings (789.5–800.5 KB): the harness changed, not the
  product.
- Residual, stated honestly: the control's `Allocated` oracle has ~±8 KB run-to-run scatter
  (784.46–800.47 KB observed across all sides) and must not be used as a pass/fail oracle at the 5 KB
  level.

**Outcome: `met` — no attributable product delta. The earlier `+5.14 KB` is a benchmark-binary /
run-to-run artifact, not a D1 allocation; E08 is closed and the planner `r=2` routing is withdrawn.**
