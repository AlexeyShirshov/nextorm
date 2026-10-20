# Collection rc3-4 — 1.0.9-rc3 open issues (autonomous, auto-commit)

- collection-id: `rc3-4`
- entry: user requested the PDCA family entry; routing `pdca-collection` (a batch of independent tasks with separate goals; parallel mode unavailable — `cap = 1`, so single-group sequential)
- mode: **autonomous**; **auto-commit explicitly authorised by the user (2026-10-19)**; `git merge --no-ff` for `done` groups authorised by the same request; **push is never allowed**
- repo / branch: `/home/alex/sources/nextorm`, `1.0.9-rc3`
- base ref: `0d4b0cf6` (HEAD at collection start)
- variant per task: `pdca-dotnet` (all tasks are .NET/C#); project overlay `.opencode/skills/nextorm-pdca/SKILL.md` must be loaded before PLAN
- lane cap: **1** — every task's acceptance needs the benchmark host in an exclusive quiet window; parallel lanes would invalidate measurements. Lanes may still be isolated in worktrees but must take the host lock before any measurement.
- host resources: BDN lock `/tmp/opencode/nextorm-bdn-host.lock`; pinned DB `sha256 32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; containers via `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`
- worktree hygiene: the main worktree already contains 45 foreign dirty integration-test paths, benchmark artifacts, `.github/workflows/dotnet.yml`, `AGENTS.md`, `docs/superpowers/*` and untracked `.pdca/status/integration-*` files. **Never stage them**; task commits stage only that task's own paths (`git add <paths>`, never `git add -A`).

## Milestone scope (context for every task)

Open issues in `1.0.9-rc3`:

| id | issue | title |
|---|---|---|
| T245 | #245 | Perf: warm EXCEPT/INTERSECT allocations exceed linq2db (#240 follow-up) |
| T246 | #246 | Perf: ToDataReader raw reader is slower than linq2db |
| T247 | #247 | Perf: JSON streaming is slower than linq2db at 10k rows |
| T250 | #250 | Perf: CSV terminal recompiles its column plan on every export (terminal plan cache, CSV slice) |
| T311 | #311 | Perf: JSON streaming — 10k per-row throughput lever, same-session acceptance pairing, noise-robust ratio oracle (#247 follow-up) |

#317/#318/#319 are **rc5**, not part of this collection.

## Tasks

| id | issue | group | variant | plan_state | status | branch | cycle_id | status_path | plan_revision | baseline | notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| T245 | #245 | G1 | pdca-dotnet | ready | incomplete | — | N=2 | `.pdca/status/T245-warm-set-ops-2.md` | r=1 | `0d4b0cf6` | terminal incomplete: insufficient_evidence: unexplained_baseline_plus_1250B; no_proven_safe_sufficient_allocation_lever; final_clarification_budget_exhausted; evidence+status diff preserved in `.pdca/T245/rv2/incomplete.patch` |
| T246 | #246 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T246-raw-datareader-1.md` | r=4 | `0d4b0cf6` | group stopped |
| T247 | #247 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T247-json-streaming-1.md` | r=1 | `0d4b0cf6` | group stopped |
| T250 | #250 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T250-csv-terminal-plan-cache-1.md` | r=1 | `0d4b0cf6` | group stopped |
| T311 | #311 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T311-json-streaming-1.md` | r=1 | `0d4b0cf6` | group stopped |

## Task briefs

### T245 — #245 warm EXCEPT/INTERSECT allocations

- Issue: https://github.com/AlexeyShirshov/nextorm/issues/245
- Prior terminal state: `T245` ended `incomplete` (`insufficient`) at r=2; **no production change**. Attribution `.pdca/T245/rv1/attribution.json`; live status `.pdca/status/T245-warm-set-ops-1.md`; spec `docs/specs/status/D245-warm-set-ops-1.md`.
- Why the previous run stopped: the measured narrow levers did not reach the target, and the plan did not authorise the remaining work.
- New input the planner must use (owner authorisation, 2026-10-19): the **remainder is authorised** — D240-protected comparer files (stateless comparers/cached registry) and the frozen preparation/recipe, plus the `QueryDefinition` inline (CHK-1: 480 B/query elidable via `QueryShape`). It may run **only** under the global matrix/test gate with zero degradation.
- Current residual after the COW revert: EXCEPT 5,392.2 vs linq2db 5,176.2 B/query → **≈216 B/query to parity**; remaining pools: comparers (≤656 B) and preparation (1,264 B).
- Acceptance (issue, frozen): EXCEPT ≤5.05 KB/query, INTERSECT ≤5.01 KB/query on the same-round report (≤1.00× linq2db; ≤1.20× only a checkpoint); time no regression beyond the noise band; SQL/result order/duplicates/public API unchanged; prepared controls and `Warm_Reused = 0 B` unchanged; attribution + final per-case BDN numbers; no averaged set-op number.
- Constraints: keep the `a.Union(b).Except(c)` shared-tail protection; do not change cache-key semantics or the frozen D201-3 recipe; general path only, no benchmark-specific shortcuts.

### T245 PLAN handoff (N=2, r=1)

- `plan_state`: **ready** (PLAN→DO boundary; DO not started; no product change). Plan: `.pdca/status/T245-warm-set-ops-2.md` (`## Plan (r=1)`).
- Planned footprint: exactly the plan's §10 lists — production `src/nextorm.core/{Builders/EntityBuilder.cs, QueryCommand.cs, QueryCommand.QueryPreparer.cs, QueryCommand.Plan.cs, QueryPlanEqualityComparer.cs, ExpressionPlanEqualityComparer.cs, Expressions/JoinExpressionPlanEqualityComparer.cs, DataContext/QueryPlanner.cs, DataContext/Cache/QueryPlanStore.cs}`; tests `tests/nextorm.core.tests/WarmSetOperationTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite/CommonTestSuite.WarmSetOperations.cs`; benchmarks `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFairCached.cs`, `SqliteBenchmarkWarmDecompose.cs` (instrumentation only); gate/CI `.pdca/scripts/perf/{t245_evidence.py, iteration14_gate.py, iteration14-budgets.json, test_t245_gate.py}`, `.github/workflows/dotnet.yml`; docs `docs/specs/performance/{acceptance-benchmarks.md, benchmark-report.md}`; status/evidence `.pdca/status/T245-warm-set-ops-2.md`, `.pdca/T245/rv2/**`. Out of footprint: rv1, prior status files, tracked raw BDN reports, all other foreign dirty paths.
- Uncertainty: `medium` on allocation room (authorised pools 480 B `QueryDefinition` elision + ≤656 B comparers + preparation vs historical 1,356.9/1,383.9 B; savings not additive); `low` on combined timing until measured.
- Predecessor requirements: rv1 attribution totals 7,056/7,040 B with zero residual; preparation 2,248.12/2,248.10 B on the correct denominator; corrected comparer ownership (≤656 B, not 984 B); COW revert leaves the cited source interval net-identical; prior terminal record `.pdca/status/T245-warm-set-ops-1.md` (`incomplete`/`insufficient` at r=2) preserved verbatim.
- Assumptions / prerequisites: host lock `/tmp/opencode/nextorm-bdn-host.lock`; both fixture copies sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; clean task-relevant paths; 76 foreign dirty paths unchanged; fresh Release build provenance; Podman socket `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; recorded runtime case inventory.
- Refs: https://github.com/AlexeyShirshov/nextorm/issues/245; `.pdca/status/T245-warm-set-ops-1.md`; `.pdca/T245/rv1/{contract.json, attribution.json, lever-decision.md}`; `docs/specs/status/D240-construction-allocation-1.md`; `docs/specs/performance/acceptance-benchmarks.md`; `docs/specs/performance/benchmark-report.md`; `.opencode/skills/nextorm-pdca/SKILL.md`.

### T246 — #246 ToDataReader raw reader

- Issue: https://github.com/AlexeyShirshov/nextorm/issues/246
- Prior terminal state: rv3 terminal GAP classified as a **host/measurement failure**; owner reopened/authorised the task. Evidence `artifacts/pdca/T246/rv3/GAP.md`, `docs/specs/status/D246-raw-datareader-3.md`.
- Mandatory prerequisite: this task runs in an **exclusive quiet window with no parallel lane** (already reflected in `cap=1`).
- Acceptance (issue): 10,000 rows ≤3.942 ms and ≤943.00 KB (≤1.00× linq2db); empty-result arm ≤1.00× (or an owner-approved tolerance); no result/LOB semantics change; `CommandReaderOwner` dispose semantics preserved; add a benchmark/gate row; cross-provider streaming report if the seam changes.
- Constraints: keep LOB terminals (`ToStream`/`ToTextReader`); no caller-side `Prepare()` requirement; no SQLite-only production hardcode — the seam choice is dialect-driven.

### T247 — #247 JSON streaming 10k

- Issue: https://github.com/AlexeyShirshov/nextorm/issues/247
- Prior terminal state: T247 ended `incomplete` / measured GAP. Evidence `docs/specs/status/D247-json-streaming-2.md`; the terminal GAP record is cross-referenced from #311.
- The open follow-up #311 (T311) is a **direct continuation**: per-row throughput lever, same-session paired acceptance, noise-robust ratio oracle. Treat T247 and T311 as one dependency line; the planner must decide the order and may merge the acceptance oracle work into one plan.
- Acceptance (frozen from the issue): 10k `WriteJson_Array_Dto` ≤9.150 ms and ≤435.64 KB; 1k fixed-cost gap materially reduced (≤1.2×); 100k not worse than 0.95×; correct JSON/array/NDJSON/root/IgnoreNull/cancellation/partial-output; add 1k/10k/100k to the gate; explain the remaining per-row string allocation.
- Constraints: keep direct streaming (no DTO materialisation), no weakened validation/cancellation/error policy, general bounded cache keys.

### T250 — #250 CSV terminal plan cache

- Issue: https://github.com/AlexeyShirshov/nextorm/issues/250
- Prior state: new task; harness `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkCsvFixedCost.cs` is committed.
- Acceptance (issue): 3-column export 100 rows ≤230 µs, 1,000 rows ≤1.1 ms, 10,000 rows within ±3% of 10.9 ms; remove the per-column plan garbage (≥8 KB/column/call); byte-identical cache hit vs cold run; no aliasing across options/provider/storage types/sequential mode; `ValueTransform` always bypasses; `DataContextCache.Clear()` drops entries; bound respected; no public API change; JSON/CSV must not regress each other.
- Design constraint: one bounded per-context terminal-plan cache, **shared in design with the JSON shape/writer cache in #247** (one mechanism, not two); CSV key includes concrete context type, projection signature, reader storage types, `SequentialAccess`, CSV options; `ValueTransform` bypasses.
- Correctness: preserve RFC 4180/CRLF/null marker/Excel guard; unsupported projection rejected before header; `AsTempTable` guard; in-memory `NotSupportedException`.

### T311 — #311 JSON follow-up

- Issue: https://github.com/AlexeyShirshov/nextorm/issues/311
- Three items: (1) attribute and remove the remaining ~486–777 µs per-call per-row work; (2) capture a same-session paired acceptance baseline `<baseline>/round-{1,2}/acceptance/cases.json`; (3) introduce a **new spec** for a noise-robust ratio oracle (min-of-N or paired difference with a pre-registered noise band) — never edit T247 thresholds/tolerances/denominators.
- Dependency: follows T247; may share one group/one measurement harness with T247 and T250.
- Acceptance: the new oracle is pre-registered before measurement; the 10k lever is attributed and measured; T247's frozen thresholds stay untouched for audit.

## Groups (after ALL-barrier)

| group | tasks (order) | worktree | branch/ref | status |
|---|---|---|---|---|
| G1 | T245 → T246 → T250 → T311 → T247 | /home/alex/sources/nextorm | `1.0.9-rc3` @ `0d4b0cf6` | incomplete — group stopped after T245 terminal incomplete |

## Decisions log

- 2026-10-19: entry routing `pdca-collection`; all five open rc3 issues are in scope.
- 2026-10-19: auto-commit + `merge --no-ff` authorised by explicit user request; push never.
- 2026-10-19: lane cap = 1 because acceptance measurements require the host in an exclusive window.
- 2026-10-19: routing rationale corrected — collection is a batch of independent goals; parallelism is an execution mode, not an entry condition. The five tasks have separate goals/acceptance → `pdca-collection`, not `pdca-incremental`; `cap = 1` means single-group sequential execution.
- 2026-10-19: prior terminal reasons of T245/T246/T247 are mandatory PLAN inputs (collection skill §P.2); the planner must show the decisive evidence is now obtainable or redefine the oracle.
- 2026-10-19: T245 own PLAN complete (collection §P.2) — plan_state=ready, N=2, r=1, rv2; prior terminal incomplete/insufficient at r=2 is preserved and addressed by owner authorisation + fresh-HEAD baseline/probe obtainability (overlay invariants 11–12); no product code, no branches, no benchmark runs.
- 2026-10-19: **ALL-barrier passed** — T246 ready (N=1, r=4), T247 ready (N=1, r=1), T250 ready (N=1, r=1), T311 ready (N=1, r=1); all plans persisted and no DO started.
- 2026-10-19: **clustering decision (planner)** — one sequential group **G1**; order **T245 → T246 → T250 → T311 → T247**; the only functional dependency is **T311 → T247** (T311 first); the rest are resource/order constraints. One measurement host + `cap=1` → single-group degradation is normal, not a contract change. Single-group policy: current worktree/branch, **no worktrees, no task/group branches, no merge**; commits only task-owned paths; push never.
- 2026-10-19: **PLAN-revision vector bound to the schedule** — T245 N=2/r=1, T246 N=1/r=4, T250 N=1/r=1, T311 N=1/r=1, T247 N=1/r=1. A genuine PLAN revision requires rebinding the vector before further dispatch.
- 2026-10-19: first dispatch = **T245** (no predecessor; establishes core query/plan-cache changes before downstream terminal/streaming work).
- 2026-10-19: **T245 terminal `incomplete` (N=2, r=1)** — pdca-planner + pdca-escalate STOP decision **B** at escalation trigger 5 (plus the sizing question of trigger 2): required reduction ≈1,590 B/query vs ≈1,136 B of proven/risky pools (QueryDefinition 480 B proven-disjoint; comparers ≤656 B unsafe; `QueryShape` missing 12 fields); the whole-suite zero-degradation oracle costs ≈2 h 40 per round and was unobtainable (rc=124), so **no r=2**. Follow-up recorded, **not executed**: bounded single-case bisect of the +1,250 B baseline delta, then a new plan with acceptance unchanged.
- 2026-10-19: **group G1 stopped** — T245 `incomplete`; T246/T250/T311/T247 not executed (`group stopped`). No `git merge`, no branches/worktrees, no commit, no push; issue #245 left open. T245 commits: **none** (nothing to commit; evidence preserved uncommitted + `.pdca/T245/rv2/incomplete.patch`). Recommendation: fresh collection lane on the current HEAD for the remaining four tasks.
- 2026-10-19: **collection C performed** (pdca-check, independent) — verdict **`blocked`**, not a code defect. 0/1 groups `done`, 0/5 tasks `done`. The integrated tree is the untouched user HEAD (`bcb8e676`; zero collection product diff) and is healthy: Debug build 0W/0E, core 2648/2648, sqlite 1792/1792 (1 skip). The block is the task-level evidence/lever limitation: required 1,356.9–1,590.1 B/query (EXCEPT) / 1,381.8–1,493.9 B/query (INTERSECT) vs ≤1,136 B of authorised/proven pools; the whole-suite zero-degradation oracle (~2 h 40/round) hit the 5,400 s cap. No re-gather can unblock T245; no cleanup is allowed.

## General verification and recovery

- state: **`blocked`** (standard collection C; no `done` group to verify; zero product diff)
- last C: **`blocked`** — completion 0/1 groups, 0/5 tasks; reason: T245 task-level evidence/lever insufficiency + unobtainable whole-suite oracle, **not** a product code defect. Raw: Debug build 0W/0E, core 2648/2648, sqlite 1792/1792 (1 skipped); provenance: HEAD moved `0d4b0cf6 → bcb8e676` with `src/` diff empty and identical assembly/fixture hashes.
- defect id: — (none; no code regression introduced)
- correction task: — (none; re-gather cannot unblock T245)
- next allowed step: fresh collection lane on current HEAD `bcb8e676` for **T246 → T250 → T311 → T247** (their plans stay `ready`); T245 requires a bounded single-case bisect of the +1,250 B baseline delta and a new plan with acceptance unchanged.

## Done / Verified / Incomplete

- Done: 0/5 tasks; 0/1 groups.
- Verified: integrated tree builds and unit-tests green, but the collection C is **blocked** (no `done` group).
- Incomplete: T245 (#245 — insufficient evidence / no safe sufficient lever); T246, T250, T311, T247 (group stopped — not executed).
- 2026-10-20: **all five open rc3 perf issues moved to `1.0.9-rc5`** by owner instruction — #245, #246, #247, #250, #311. No further rc3 lane; rc3-5 stopped at T246 (terminal incomplete, patch preserved, tree reverted).
