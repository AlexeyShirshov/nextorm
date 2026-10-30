# Cycle status: T245 / cycle 3

## Current state

- task: `T245` · form: `incremental` (campaign) · specialized skill: `pdca-dotnet` (`coder+.NET`)
- mode: `autonomous` (collection `rc5`, lane `collection/rc5/group-1`, auto-commit authorized for this collection)
- cycle: `N=3` · revision: `r=1` · attempt: `n=1/3` (step 1 is measurement-only and consumes **no** attempt — no candidate is produced)
- scope: step 1 of the campaign — the same-round baseline for the warm cached fluent `Except`/`Intersect` paths plus per-phase allocation attribution, **measurement only, no product change**. Every later step's scope is decided by the planner after this step's CHECK. · status path: `.pdca/status/T245-warm-set-ops-3.md`
- goal: reduce per-query managed allocations on the warm cached fluent `Except`/`Intersect` paths of `NextORM.Core` without SQL/behaviour/API regression, with per-phase attribution, native allocation gates and same-round paired BDN evidence — the §PLAN record, §Original task and `.pdca/rc5/items/T245/preparation.md@r1` are authoritative
- phase: `DO` · record: `open` · outcome: `pending`
- source: issue `#245` (`AlexeyShirshov/nextorm`, milestone `1.0.9-rc5`), dispatched through collection `rc5` · context refs: `.pdca/rc5/items/T245/preparation.md@r1`, `.pdca/rc5/entry-plan.md@r1`, `.pdca/status/collection-rc5.md` §Input → `### #245 …`, §Task queue row `T245`, §Decisions log 06:45:06Z (evidence-root supersede)
- criteria: `AC-245-01` … `AC-245-07` — exact verbatim text in §Original task below (source: `preparation.md@r1` §6, "verbatim user-supplied set"), not retyped in the summary

## Original task

The user's task is the issue `#245` brief, persisted verbatim (complete, unabridged, original language) at
`.pdca/status/collection-rc5.md` §Input → `### #245 — Perf: warm EXCEPT/INTERSECT allocations exceed linq2db (#240 follow-up)`,
running from its `## Context` through its `## Related` list. That artefact is the authoritative source and is not retold here.

Its verbatim user-supplied acceptance criteria, as carried by `.pdca/rc5/items/T245/preparation.md@r1` §6 (no addition, no
weakening, no replacement):

- **`AC-245-01` (Global target — stretch / milestone-level, measurable):**
  > **Global target (milestone-level):** EXCEPT/INTERSECT allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db on the same-round report. The original ≤ **5.05 KB/query** (EXCEPT) / ≤ **5.01 KB/query** (INTERSECT) parity numbers remain the stretch reference, not a per-task gate.
- **`AC-245-02` (Local acceptance — task closure gate, measurable):**
  > **Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
  > - allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
  > - time improves beyond the pre-registered noise band and allocations do not increase.
  >
  > A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.
- **`AC-245-03` (Measurement rules — comparability & protocol invariant):**
  > **Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.
- **`AC-245-04` (Correctness, API & time non-regression — invariant + measurable):**
  > Time does not regress beyond the ShortRun noise band; SQL, result order/duplicates and public API unchanged.
- **`AC-245-05` (Prepared controls, Warm_Reused & native allocation gate — invariant + verifiable):**
  > Prepared controls and `Warm_Reused = 0 B` unchanged; add both rows to the native allocation gate.
- **`AC-245-06` (Attribution & per-case reporting — evidence deliverable):**
  > Evidence includes attribution and final per-case BDN numbers; no averaged set-operation number.
- **`AC-245-07` (Tail-mutation, cache-key & general-path constraints — negative condition / invariant):**
  > - Preserve the #240 fix that prevents mutating a shared set-op tail (`a.Union(b).Except(c)`).
  > - Do not change the cache-key semantics or the frozen D201-3 recipe.
  > - General path only; no benchmark-specific SQL/type checks.

## PLAN record

### r1

`not recorded verbatim: the item PLAN (cycle N=3, r=1, n=1/3) returned to the rc5 collection driver for this campaign was not supplied to the DO dispatch, so it cannot be recorded as its source produced it and is not reconstructed from the preparation's summary of it.` Its identity, its source and the reservation of this section for its first verbatim persistence are stated by `.pdca/rc5/items/T245/preparation.md@r1` (§Preparation record header, line 7: "plan record ref … its first verbatim persistence is `.pdca/status/T245-warm-set-ops-3.md` §PLAN record"). Request the exact original from the driving role; writing it here needs no new plan approval.

## Approval record

`not applicable: autonomous mode` — the collection `rc5` lane runs with explicit user auto-commit authorization ("pdca по открытым задачам из майлстона rc5 автономно с автокомитом"); there is no interactive plan approval.

## DO result

Step 1 baseline, measurement only, **no product edit and no attempt consumed**. Completed actions and evidence (§Baseline record and `.pdca/rc5/items/T245/rv3/baseline/summary.md` carry the full table):

- Re-anchor: `HEAD = cb2246e9` on `collection/rc5/group-1`; `git status --porcelain -- src tests` **empty**; worktree in sync with the frozen identity the dispatch supplied.
  Notice (pointer drift, cosmetic, not semantic): `preparation.md@r1` §0 names the re-anchor commit `1e31331d`; the measured baseline is the dispatch's `cb2246e9`, the lane tip after the earlier `G1` items (`… → T319`). Same branch, later commit — recorded, not treated as a refuted premise.
- Container socket `/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` present; **no** `podman machine start` was needed (no container-backed run in this step; the `/mnt/c/Program Files/RedHat/podman/podman.exe` path was never invoked).
- `dotnet build-server shutdown` before the first run; `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`; one process at a time; every run capped with `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 1800 … 2>&1 | tee <log>`.
- Four runs, all exit `0`, none truncated: (A) `*SqliteBenchmarkFeaturesFairCached*` 55 arms, 380 s; (B) `*SqliteBenchmarkWarmDecompose*` 58 arms, 374 s; (C) the two `Prepare()` control arms only, 17 s; (D) the existing `--t245-attribution` phase probe. Logs and raw JSON/CSV under `.pdca/rc5/items/T245/rv3/baseline/`.
- Deviation recorded, not hidden: the dispatch expected run A's filter to yield `B_Nextorm_Prepared_*` and `*_Warm_Reused`; it yields neither (the `Prepare()` controls are `A_Nextorm_Prepared_*` in `SqliteBenchmarkFeaturesFair`, the `*_Prepare_*`/`*_Warm_Reused` rungs are `WarmDecompose`-only). Run C supplies the requested control rows (2 arms, no other arm added); run D is the phase probe that `preparation.md@r1` §7.1 requires and it already exists at `HEAD` — no harness was written or modified.
- Evidence root: `.pdca/rc5/items/T245/rv3/` per the dispatch and the collection's 2026-10-30T06:45:06Z supersede (`.pdca/<item>/rv<k>/` → `.pdca/rc5/items/<item>/rv<k>/`); `preparation.md@r1` still carries the pre-supersede form `.pdca/T245/rv3/` — pointer only, nothing was written there.
- Unfinished for this step: the CHECK of this measurement (driver's next action). No candidate, so no verdict, no `PASS`, no attempt used.

## Baseline record

- **Exact values (this round, 2026-10-30, BDN `ShortRun` + `InProcessEmitToolchain`, pinned DB sha256 `32bf10a0…72295`), B/query after ÷10 (FairCached/Fair) or ÷100 (ladder) of BDN `Allocated B/op`:**
  - `B_Nextorm_Cached_Except` `7 112.1 B/query` (71 121 B/op, Mean `187.887 µs ± 1.123 µs StdErr`) vs `B_Linq2Db_Except` `5 176.2 B/query` (`395.977 µs ± 5.814 µs`) ⇒ **1.374× allocated, time 0.475×, +1 935.9 B/query**.
  - `B_Nextorm_Cached_Intersect` `7 096.1 B/query` (70 961 B/op, `182.769 µs ± 0.713 µs`) vs `B_Linq2Db_Intersect` `5 128.2 B/query` (`382.876 µs ± 2.478 µs`) ⇒ **1.384× allocated, time 0.477×, +1 967.9 B/query**.
  - `Prepare()` controls: `A_Nextorm_Prepared_Except` `760.0 B/query` (`114.036 µs ± 0.214 µs`), `A_Nextorm_Prepared_Intersect` `744.0 B/query` (`104.909 µs ± 0.217 µs`).
  - Ladder (`Warm_Except` / `Warm_Intersect`, EXCEPT = INTERSECT on every rung): `*_Construct` `4 184.18`, `*_Prepare_NoHash` `5 816.26`, `*_Prepare_Hash` `6 608.31`, `*_Warm_PlanOnly` `6 432.31 / 6 432.28`, `*_Warm_Reused` **`0.00`** B/query (both operations). No `*_Execute` rung exists; `*_Warm_Reused`/`*_Prepare_*` do not exist in the FairCached class.
- **Composition (existing `--t245-attribution` probe, deterministic min = median = max, closure residual `0`), B/query EXCEPT / INTERSECT:** first operand `1 808 / 1 808`, second operand `1 904 / 1 904`, first-operand `Clone()` `872 / 872`, `ResetPreparation` `0 / 0`, `SetOperation` attach `0 / 0`, prepare/hash + plan lookup `2 248 / 2 248`, execute + materialize `808 / 792`; totals `7 640 / 7 624`. Probe level is `+528 B/query` above this round's BDN arm and its printed `V0 − BDN` delta uses the historical `rv1` reference — the probe is used for **relative phase shares only**; BDN stays the acceptance metric.
- **Command · exit · UTC** (all `rc=0`, logs under the evidence root): A `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached*' --exporters json` → 2026-10-30T12:43:07Z · B `… --filter '*SqliteBenchmarkWarmDecompose*' --exporters json` → 2026-10-30T12:49:34Z · C `… --filter '*A_Nextorm_Prepared_Except*' '*A_Nextorm_Prepared_Intersect*' --exporters json` → 2026-10-30T12:50:01Z · D `… --t245-attribution .pdca/rc5/items/T245/rv3/baseline/attribution` → 2026-10-30T12:50:08Z.
- **Evidence path:** `.pdca/rc5/items/T245/rv3/baseline/` — `summary.md` (full per-arm table), `bdn-faircached.log`, `bdn-warmdecompose.log`, `bdn-prepared-controls.log`, `t245-attribution.log`, `NextORM.Benchmark.SqliteBenchmark{FeaturesFairCached,WarmDecompose,FeaturesFair}-report.{csv,github.md,full-compressed.json}`, `attribution/probe-variants2.{json,csv}`.
- **Frozen identity:** `HEAD = cb2246e9` (branch `collection/rc5/group-1`), `src/` and `tests/` clean (`git status --porcelain -- src tests` empty) — the baseline this campaign measures against.
- **`preparation_ref@r1`:** `.pdca/rc5/items/T245/preparation.md@r1` (sha256 of the file is not claimed here; the file is the immutable charter).
- **Carried context, not re-measured:** `T317` residual per `.pdca/status/T317-rc5-1.md` — deterministic `+160 B/op` on the four `In*` arms and `+1 761 B` mean on `RecursiveCte`, both unmeasured in this step, mechanism unattributed; a per-instance memo seam cannot satisfy `AC-317-02` (fresh `QueryCommand` per terminal). `T318` closed terminal `infeasible` with no product delta — no empty-collection fast path is available as a lever; the clone shell remains `872 B/query`.

## CHECK record

`pending: CHECK not run yet` — this DO dispatch returned at the DO boundary; the independent CHECK of the Step 1 measurement is the driver's next action. No verdict, no `PASS`, no acceptance is claimed.

## ACT record

`pending`

## Revision history

`none`

## Defect history

`none`

## Changed files

- `.pdca/rc5/items/T245/rv3/baseline/summary.md` → created: baseline + attribution table (arm / Mean ± StdErr / Allocated B/op / B/query, divisor stated), run ledger, absent-rung list, T317 residual context.
- `.pdca/rc5/items/T245/rv3/baseline/bdn-faircached.log`, `bdn-warmdecompose.log`, `bdn-prepared-controls.log`, `t245-attribution.log` → created: full streamed logs of runs A–D (`tee`, rc appended).
- `.pdca/rc5/items/T245/rv3/baseline/NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report.{csv,github.md,full-compressed.json}`, `…WarmDecompose-report.{csv,github.md,full-compressed.json}`, `…FeaturesFair-report.{csv,github.md,full-compressed.json}` → copied byte-identically from `benchmarks/BenchmarkDotNet.Artifacts/results/` (raw BDN export, `--exporters json`).
- `.pdca/rc5/items/T245/rv3/baseline/attribution/probe-variants2.{json,csv}` → written by the existing probe entry point (`--t245-attribution`).
- `.pdca/status/T245-warm-set-ops-3.md` → created (this record).
- `.pdca/status/collection-rc5.md` → modified: §Decisions log entry + §Progress log row recording this Step 1 baseline; queue row `T245` unchanged (`status=ready`, `attempts=0`).

No file under `src/`, `tests/`, `benchmarks/` or `docs/` was created, edited or deleted — the product tree is byte-identical to `cb2246e9`.

## Git state

`status path: .pdca/status/T245-warm-set-ops-3.md — created this cycle · untracked` — durable on disk and read back; committing it is this dispatch's explicit instruction (auto-commit policy of collection `rc5`, `collection/**` branch). Baseline supplied this cycle: `cb2246e9`. Container status `.pdca/status/collection-rc5.md` was clean at `cb2246e9` before this modification, so its committed delta is this cycle's only. Evidence under `.pdca/rc5/items/T245/rv3/baseline/` stays uncommitted (local evidence, never a CI input). Per dispatch, the commit carries exactly `.pdca/status/T245-warm-set-ops-3.md` and `.pdca/status/collection-rc5.md`; no staging of anything else, no `-A`, no push.

## Progress log

2026-10-30T12:36:47Z | DO | revision r=1 | attempt n=1 | Step 1 (baseline, measurement only) opened: frozen identity `HEAD cb2246e9` on `collection/rc5/group-1`, `src`/`tests` clean, podman socket present (no machine start), `dotnet build-server shutdown`, `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db` | `.pdca/rc5/items/T245/rv3/baseline/`
2026-10-30T12:43:07Z | DO | revision r=1 | attempt n=1 | run A `*SqliteBenchmarkFeaturesFairCached*` exit 0, 55 arms, 380 s; `B_Nextorm_Cached_Except` 71 121 B/op = 7 112.1 B/query vs `B_Linq2Db_Except` 51 762 B/op = 5 176.2 (1.374×), `…_Intersect` 70 961 = 7 096.1 vs 51 282 = 5 128.2 (1.384×) | `bdn-faircached.log`, `NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached-report-full-compressed.json`
2026-10-30T12:49:34Z | DO | revision r=1 | attempt n=1 | run B `*SqliteBenchmarkWarmDecompose*` exit 0, 58 arms, 374 s; set-op ladder present (`Warm_Except`/`Warm_Intersect`): Construct 4 184.18, Prepare_NoHash 5 816.26, Prepare_Hash 6 608.31, Warm_PlanOnly 6 432.31/6 432.28, Warm_Reused 0.00 B/query; no `*_Execute` rung exists | `bdn-warmdecompose.log`, `NextORM.Benchmark.SqliteBenchmarkWarmDecompose-report-full-compressed.json`
2026-10-30T12:50:01Z | DO | revision r=1 | attempt n=1 | run C `Prepare()` controls exit 0: `A_Nextorm_Prepared_Except` 7 600 B/op = 760.0 B/query, `A_Nextorm_Prepared_Intersect` 7 440 = 744.0 (dispatch's `B_Nextorm_Prepared_*` names do not exist in the FairCached class; this 2-arm run supplies them) | `bdn-prepared-controls.log`, `NextORM.Benchmark.SqliteBenchmarkFeaturesFair-report-full-compressed.json`
2026-10-30T12:50:08Z | DO | revision r=1 | attempt n=1 | run D existing `--t245-attribution` probe exit 0, deterministic, closure residual 0: operands 3 712, clone 872, prepare/hash 2 248, execute 808/792, totals 7 640/7 624 B/query (probe level +528 vs BDN; used for relative shares only) | `t245-attribution.log`, `attribution/probe-variants2.json`
2026-10-30T12:52:00Z | DO | revision r=1 | attempt n=1 | baseline record written: `summary.md` + §Baseline record; no product change, no attempt consumed, no candidate ⇒ CHECK pending, no verdict | `.pdca/rc5/items/T245/rv3/baseline/summary.md`, `.pdca/status/T245-warm-set-ops-3.md`
