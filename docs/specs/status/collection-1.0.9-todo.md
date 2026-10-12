# Collection 1.0.9-todo

- collection-id: `1.0.9-todo`
- input: milestone `1.0.9` (milestone #4) open issues — **6 tasks**: #213, #212, #211, #209, #201, #53
- base: branch `1.0.9` @ `58025147` (merge of #214 `1.0.9-rc2`)
- mode: autonomous; auto-commit authorized (task/collection status files and task paths only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- phase: **D — lane G1 STOPPED** (T209 done/verified; **T211 incomplete** → fail-stop; remaining G1 tasks T212/T213/T201/T53 never executed; single group → current worktree/branch `1.0.9`, no worktree/branches/merge)

## Input task list

Selected variant: `pdca-dotnet` for all tasks (C#/.NET repo). Each task runs its own real PLAN at the P phase, stops at the PLAN→DO boundary, and the lane resumes the **same** cycle from DO.

| id | issue | title | selected_variant | plan_state |
|---|---|---|---|---|
| T209 | #209 | eager-load strategy — explicit strategy + auto-dispatcher | pdca-dotnet | done (r=3) |
| T211 | #211 | eager-load per-parent Take/Skip (absorbs #210) | pdca-dotnet | ready (r=1) |
| T212 | #212 | eager-load nested — level 2+ LoadWith | pdca-dotnet | ready (r=1) |
| T213 | #213 | JoinInto — composite keys / M:N single-query | pdca-dotnet | ready (r=1) |
| T201 | #201 | Perf: M12 #3 — cached-path parity with LINQ to DB | pdca-dotnet | ready (r=2) |
| T53 | #53 | IF6: public API freeze (PublicApiAnalyzers + PublicAPI.*) | pdca-dotnet | ready (r=1) |

## Groups

| id | tasks (order) | worktree | branch/ref | status |
|---|---|---|---|---|
| G1 | T209 → T211 → T212 → T213 → T201 → T53 | no (single group) | current branch `1.0.9` | incomplete (not merged — single group, no merge) |

### G1 execution contract

- A task starts only after the previous task is `done` (including its CHECK). `plan_state=ready` ≠ `done`.
- `T201` is now `ready (r=2)`: the mandatory separately-reviewed written design `docs/specs/performance/m12-3-prepare-command-design.md` now exists (revision 2) and was approved by the first ordered separate review; the detailed r=2 plan / sequential execution mode / rv1 evidence contract was approved by the second ordered separate review. Milestone **1.0.9** kept (metadata/body mismatch with `1.1-a.1` left to the issue owner).
- File-level serialization: `EntityBuilder.cs`, `EntityBuilderEagerLoading.cs`, `SqlBuilder.cs`, `ISqlDialect.cs` (read-only for T209), `Builders/Joins/*`, `CommonTestSuite.*.cs`, shared specs and EN/RU docs are written by one task at a time in the stated order; T53 last (its API baseline must include every accepted eager-loading public-API change).
- Fail-stop: a definitively `incomplete` task stops the whole group — remaining `pending` tasks → `incomplete` («группа остановлена»), never executed; already-made commits stay in the current branch (no auto-rollback); the failing task's uncommitted changes are saved as a patch.
- Integration: single group → no `merge --no-ff`. Result = the current branch `1.0.9`. Push never.
- Integration-tests precondition: `DOCKER_HOST`/Podman; a skipped required provider is unrun, not green.

## Tasks

| id | issue | group | order | status | plan_state | selected_variant | cycle_id | plan_revision | PLAN commit | status file |
|---|---|---|---|---|---|---|---|---|---|---|
| T209 | #209 | G1 | 1 | done | done | pdca-dotnet | N=1 | r=3 | 5e9f6756 | docs/specs/status/D209-eager-strategy-1.md |
| T211 | #211 | G1 | 2 | incomplete | ready | pdca-dotnet | N=1 | r=1 | e18dc143 | docs/specs/status/D211-eager-perparent-topn-1.md |
| T212 | #212 | G1 | 3 | incomplete | ready | pdca-dotnet | N=1 | r=1 | 034e5b45 | docs/specs/status/D212-eager-nested-1.md |
| T213 | #213 | G1 | 4 | incomplete | ready | pdca-dotnet | N=1 | r=1 | 9e7facff | docs/specs/status/D213-joininto-remainder-1.md |
| T201 | #201 | G1 | 5 | incomplete | ready | pdca-dotnet | N=1 | r=2 | bd4af157 | docs/specs/status/D201-cached-parity-1.md |
| T53  | #53  | G1 | 6 | incomplete | ready | pdca-dotnet | N=1 | r=1 | (untracked, D53 file) | docs/specs/status/D53-public-api-freeze-1.md |

- T201 reason: the design prerequisite is now satisfied — the separately-reviewed written design (revision 2) and the r=2 plan / sequential mode / rv1 contract are approved; `plan_state=ready (r=2)`. **Amended by user directive:** T201 belongs **in G1 before the freeze** (T53) because its outcome may change the public API. G1 order 5 retained; T201 precedes T53.
- Evidence contract (per task, frozen at that task's DO→CHECK): `rv=1`; manifests `artifacts/pdca/<task>/rv1/manifest.json`.
- T211 **incomplete**: r1 exhausted (3/3 CHECK fails); escalate re-gather (a): EV-14 met, EV-11 rejected (Stryker restart exit code inferred, not captured); no product/config defect demonstrated; STOP. Retained commits `e98ae60e`, `5bd3028f`, `15c6253f` (kept, none reverted). Patch `artifacts/pdca/D211-eager-perparent-topn-1/incomplete/D211-incomplete.patch`; status file `docs/specs/status/D211-eager-perparent-topn-1.md`.
- T212 (#212), T213 (#213), T201 (#201), T53 (#53) = `incomplete` — «группа остановлена», never executed (fail-stop after T211 incomplete).

## Phase decisions

- **Bootstrap:** input list of 6 tasks, stable ids, briefs, `selected_variant=pdca-dotnet`; no code, no DAG precomputation.
- **Own PLANs (P):** all 6 executed via `pdca-orchestrator` (COLLECTION TASK PLAN). After the medium-tier `planner` provider (`opencode/gpt-6.1-sol`) was funded again, T209/T211/T213 re-finalized:
  - ready: T209 (`b4051b2b`), T211 (`e18dc143`), T212 (`034e5b45`), T213 (`9e7facff`), T53 (untracked D53 file).
  - ready: T201 (`bd4af157`, P201-READY; was rejected `9bf13403`) — separately-reviewed written design revision 2, r=2 plan / sequential mode / rv1 approved.
- **ALL-barrier: PASSED** — every task now has a persisted `plan_state=ready`, including T201 (written design revision 2 and the r=2 plan / sequential mode / rv1 separately approved); no task remains a non-ready `excluded-gap`. Early DO was forbidden until here. Readiness does not lift T53's execution-evidence freeze hold.
- **Clustering (`planner`):** no genuine parallelism — `EntityBuilder.cs`, `EntityBuilderEagerLoading.cs`, `ISqlDialect.cs`, `SqlBuilder.cs`, `Builders/Joins/*`, `CommonTestSuite.*.cs` and shared specs/docs are co-written, and T53 must follow the eager-loading public-API work. **One group G1** (degradation to a single lane): T209 → T211 → T212 → T213 → T201 → T53 (amended: T201 before the freeze, so T53's API baseline includes any public-API change from #201).
- **PLAN revision vector (schedule binding):** T209 r=3 (done), T211 r=1, T212 r=1, T213 r=1, T201 r=2, T53 r=1; evidence contracts: T209 rv=2 (done), others rv=1 each.

## Schedule amendment (user directive, post-lane-start)

- Directive: **#201 must precede the freeze (#53)** — T201's outcome may change the public API, so T53's API baseline must include T201's result. Amended G1 order: `T209 → T211 → T212 → T213 → T201 → T53` (T53 remains last).
- Resolved: T201's design gate is satisfied — the separately-reviewed written design `docs/specs/performance/m12-3-prepare-command-design.md` (revision 2) now exists and is approved, and the r=2 plan / sequential mode / rv1 contract is approved; T201 is `ready (r=2)` at G1 order 5.
- Consequence: the freeze (T53) is **held** — it will not run before T201 is resolved. Post-lane-start scope/schedule change → routed to `planner`/`escalate` per the collection contract (no silent re-cluster).
- Status: T201 design gate unblocked (`plan_state=ready (r=2)`); the freeze hold on T53 remains until T201's accepted EXECUTION evidence, not merely PLAN readiness.

## Общая верификация и восстановление

- Verification state (C): not reached — lane G1 in progress.
- Last collection `C` result: —
- Blocker: T211 incomplete (r1 exhausted, 3/3 CHECK fails; EV-11 exit-code evidence gap) — lane G1 fail-stop.
- Defect id / history: —
- Corrective-task status file: —
- Next allowed step: **STOP** — no further G1 execution (T211 incomplete, remaining tasks «группа остановлена»); T211 saved as patch and reverted; branch `1.0.9` unchanged (commits `e98ae60e`, `5bd3028f`, `15c6253f` retained).

## Done / Verified / Incomplete

- Done: T209 (#209) — eager-load strategy (explicit strategy + auto-dispatcher); cycle N=1 r=3 n=2/3, CHECK PASS, contract rv=2; status file `docs/specs/status/D209-eager-strategy-1.md`.
- Verified: T209 (#209) — rv=2 evidence (build 0/0; six dialect suites 0 failures; integration all 5 providers executed; coverage line 88.5% / branch 80.5%; acceptance benchmark 7/7, no ratio >20%); manifest `artifacts/pdca/D209/rv2/manifest.json` E209-01..21 all `met`, validator exit 0; CHECK report `artifacts/pdca/D209/rv3/check-report.md`.
- Incomplete: T211 (#211) — r1 exhausted (3/3 CHECK fails); escalate re-gather EV-14 met / EV-11 rejected (Stryker restart exit code inferred, not captured); no product/config defect; STOP. Patch `artifacts/pdca/D211-eager-perparent-topn-1/incomplete/D211-incomplete.patch`; retained commits `e98ae60e`, `5bd3028f`, `15c6253f`; status file `docs/specs/status/D211-eager-perparent-topn-1.md`.
- Incomplete: T212 (#212), T213 (#213), T201 (#201), T53 (#53) — «группа остановлена», never executed.

## History (superseded current-state facts)

- T201 was previously recorded as `gap → excluded-gap` (PLAN commit `9bf13403`): a genuine design gap because issue #201 approves only goal+tolerance and the mandatory separately-reviewed design `docs/specs/performance/m12-3-prepare-command-design.md` did not exist. That claim is superseded by the approved design revision 2 and the approved r=2 plan; it is retained here, not as current state.
- The ALL-barrier was previously described as passing while T201 remained an `excluded-gap`. That exclusion is superseded: every task, including T201, now has `plan_state=ready`.
- G1 previously listed `T209 → T211 → T212 → T213 → T53`; superseded by the amended order `T209 → T211 → T212 → T213 → T201 → T53`.
- The T201 schedule amendment previously recorded `awaiting owner unblock of T201's design gate`; superseded by the P201-READY register (`plan_state=ready (r=2)`), with T53's execution-evidence freeze hold retained.
