# Collection 1.0.9-todo

- collection-id: `1.0.9-todo`
- input: milestone `1.0.9` (milestone #4) open issues — **6 tasks**: #213, #212, #211, #209, #201, #53
- base: branch `1.0.9` @ `58025147` (merge of #214 `1.0.9-rc2`)
- mode: autonomous; auto-commit authorized (task/collection status files and task paths only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- phase: **D — lane G1 executing** (ALL-barrier passed; **T209 done** → lane advancing to **T211**; single group → current worktree/branch `1.0.9`, no worktree/branches/merge)

## Input task list

Selected variant: `pdca-dotnet` for all tasks (C#/.NET repo). Each task runs its own real PLAN at the P phase, stops at the PLAN→DO boundary, and the lane resumes the **same** cycle from DO.

| id | issue | title | selected_variant | plan_state |
|---|---|---|---|---|
| T209 | #209 | eager-load strategy — explicit strategy + auto-dispatcher | pdca-dotnet | done (r=3) |
| T211 | #211 | eager-load per-parent Take/Skip (absorbs #210) | pdca-dotnet | ready (r=1) |
| T212 | #212 | eager-load nested — level 2+ LoadWith | pdca-dotnet | ready (r=1) |
| T213 | #213 | JoinInto — composite keys / M:N single-query | pdca-dotnet | ready (r=1) |
| T201 | #201 | Perf: M12 #3 — cached-path parity with LINQ to DB | pdca-dotnet | **gap → excluded-gap** |
| T53 | #53 | IF6: public API freeze (PublicApiAnalyzers + PublicAPI.*) | pdca-dotnet | ready (r=1) |

## Groups

| id | tasks (order) | worktree | branch/ref | status |
|---|---|---|---|---|
| G1 | T209 → T211 → T212 → T213 → T53 | no (single group) | current branch `1.0.9` | in-progress |

### G1 execution contract

- A task starts only after the previous task is `done` (including its CHECK). `plan_state=ready` ≠ `done`.
- `T201` is an `excluded-gap`: genuine gap (issue #201 approves only goal+tolerance; the mandatory separately-reviewed design `docs/specs/performance/m12-3-prepare-command-design.md` does not exist and is not satisfiable by the task's own autonomous cycle). Milestone **1.0.9** kept (metadata/body mismatch with `1.1-a.1` left to the issue owner).
- File-level serialization: `EntityBuilder.cs`, `EntityBuilderEagerLoading.cs`, `SqlBuilder.cs`, `ISqlDialect.cs` (read-only for T209), `Builders/Joins/*`, `CommonTestSuite.*.cs`, shared specs and EN/RU docs are written by one task at a time in the stated order; T53 last (its API baseline must include every accepted eager-loading public-API change).
- Fail-stop: a definitively `incomplete` task stops the whole group — remaining `pending` tasks → `incomplete` («группа остановлена»), never executed; already-made commits stay in the current branch (no auto-rollback); the failing task's uncommitted changes are saved as a patch.
- Integration: single group → no `merge --no-ff`. Result = the current branch `1.0.9`. Push never.
- Integration-tests precondition: `DOCKER_HOST`/Podman; a skipped required provider is unrun, not green.

## Tasks

| id | issue | group | order | status | plan_state | selected_variant | cycle_id | plan_revision | PLAN commit | status file |
|---|---|---|---|---|---|---|---|---|---|---|
| T209 | #209 | G1 | 1 | done | done | pdca-dotnet | N=1 | r=3 | 5e9f6756 | docs/specs/status/D209-eager-strategy-1.md |
| T211 | #211 | G1 | 2 | pending | ready | pdca-dotnet | N=1 | r=1 | e18dc143 | docs/specs/status/D211-eager-perparent-topn-1.md |
| T212 | #212 | G1 | 3 | pending | ready | pdca-dotnet | N=1 | r=1 | 034e5b45 | docs/specs/status/D212-eager-nested-1.md |
| T213 | #213 | G1 | 4 | pending | ready | pdca-dotnet | N=1 | r=1 | 9e7facff | docs/specs/status/D213-joininto-remainder-1.md |
| T201 | #201 | — | — | incomplete (excluded-gap) | gap | pdca-dotnet | N=1 | r=1 | 9bf13403 | docs/specs/status/D201-cached-parity-1.md |
| T53  | #53  | G1 | 5 | pending | ready | pdca-dotnet | N=1 | r=1 | (untracked, D53 file) | docs/specs/status/D53-public-api-freeze-1.md |

- T201 reason: design prerequisite unmet (external approval gate), not a resource/defect gap → excluded from the barrier and the lane.
- Evidence contract (per task, frozen at that task's DO→CHECK): `rv=1`; manifests `artifacts/pdca/<task>/rv1/manifest.json`.

## Phase decisions

- **Bootstrap:** input list of 6 tasks, stable ids, briefs, `selected_variant=pdca-dotnet`; no code, no DAG precomputation.
- **Own PLANs (P):** all 6 executed via `pdca-orchestrator` (COLLECTION TASK PLAN). After the medium-tier `planner` provider (`opencode/gpt-6.1-sol`) was funded again, T209/T211/T213 re-finalized:
  - ready: T209 (`b4051b2b`), T211 (`e18dc143`), T212 (`034e5b45`), T213 (`9e7facff`), T53 (untracked D53 file).
  - excluded-gap: T201 (`9bf13403`) — genuine design gap; not autonomously satisfiable.
- **ALL-barrier: PASSED** — every non-excluded task has a persisted `plan_state=ready`; T201 is a genuine `excluded-gap` (not a `ready` placeholder). Early DO was forbidden until here.
- **Clustering (`planner`):** no genuine parallelism — `EntityBuilder.cs`, `EntityBuilderEagerLoading.cs`, `ISqlDialect.cs`, `SqlBuilder.cs`, `Builders/Joins/*`, `CommonTestSuite.*.cs` and shared specs/docs are co-written, and T53 must follow the eager-loading public-API work. **One group G1** (degradation to a single lane): T209 → T211 → T212 → T213 → T53. T201 outside the group and the barrier.
- **PLAN revision vector (schedule binding):** T209 r=3 (done), T211 r=1, T212 r=1, T213 r=1, T53 r=1; evidence contracts: T209 rv=2 (done), others rv=1 each.

## Общая верификация и восстановление

- Verification state (C): not reached — lane G1 in progress.
- Last collection `C` result: —
- Blocker: —
- Defect id / history: —
- Corrective-task status file: —
- Next allowed step: drive lane **G1** (T209 done/verified; continue each remaining task's saved PLAN from DO, in order T211 → T212 → T213 → T53); then collection-wide `C` on the integrated current branch.

## Done / Verified / Incomplete

- Done: T209 (#209) — eager-load strategy (explicit strategy + auto-dispatcher); cycle N=1 r=3 n=2/3, CHECK PASS, contract rv=2; status file `docs/specs/status/D209-eager-strategy-1.md`.
- Verified: T209 (#209) — rv=2 evidence (build 0/0; six dialect suites 0 failures; integration all 5 providers executed; coverage line 88.5% / branch 80.5%; acceptance benchmark 7/7, no ratio >20%); manifest `artifacts/pdca/D209/rv2/manifest.json` E209-01..21 all `met`, validator exit 0; CHECK report `artifacts/pdca/D209/rv3/check-report.md`.
- Incomplete: —
