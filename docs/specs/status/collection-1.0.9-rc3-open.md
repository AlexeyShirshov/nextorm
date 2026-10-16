# Collection 1.0.9-rc3-open — status

- collection-id: `1.0.9-rc3-open`
- slug: `collection-1.0.9-rc3-open`
- input: **open issues of milestone `1.0.9-rc3`**, re-read 2026-10-16 via `gh` (milestone #4, 6 open / 26 closed).
  In scope (in-repo, user decision 2026-10-16): **#207, #234, #237, #240**.
  OUT OF WORKSPACE at collection start — **not run by this collection**: **#235** (canonical = global `pdca-dotnet` skill dir `~/.config/opencode/skills/pdca-dotnet/**` + repo-copy sync) and **#238** (canonical = external repo `~/sources/opencode-config`) — both issues' own approved scope said implementation there requires separate explicit workspace/access/instruction. **UPDATE 2026-10-16:** both **#235 and #238 are CLOSED (COMPLETED)** by the owner (closed 2026-10-16T14:29Z), outside this collection — they are no longer open gaps.
- base: branch `1.0.9-rc3` @ `c7ff89ca` (`git status --short`: only 4 untracked approved design specs — `docs/specs/design/issue-235-*`, `issue-237-*`, `issue-238-*`, `issue-240-*`).
- mode: **autonomous + auto-commit (explicit user request 2026-10-16)**. Collection is single-group unless clustering finds otherwise → if single group: current worktree/branch `1.0.9-rc3`, no worktree/group-branch/task-branch, no `merge --no-ff`; commits (authorized) land in the current branch. Push: owner-authorized; branch pushed `c7ff89ca..531c0698` (owner then asked to push; `GIT_ALLOW_PUSH=1` used to pass the project pre-push hook).
- phase: **A — collection COMPLETED** (C = PASS on integrated tree `044d03e2`; push done).

## Tasks

| id | issue | title | variant | status | plan_state | cycle N | r | rv | status file | baseline ref |
|----|-------|-------|---------|--------|-----------|---|--|----|-------------|--------------|
| T207 | #207 | Uniform cancellation handling during projection build | pdca-dotnet | done | closed | 1 | 1 | 1 | docs/specs/status/D207-projection-cancellation-1.md | c7ff89ca |
| T237 | #237 | Identifier safety: SQL Server bracket escaping and raw UNPIVOT/Column<T> names | pdca-dotnet | done | closed | 1 | 1 | 1 | docs/specs/status/D237-identifier-safety-1.md | c7ff89ca |
| T234 | #234 | Evidence pipeline hardening: atomic evidence generator + stale-hash lint | pdca-coder | done | closed | 1 | 2 | 2 | docs/specs/status/D234-evidence-pipeline-hardening-1.md | c7ff89ca |
| T240 | #240 | Perf: fluent command-construction allocation cost | pdca-dotnet | incomplete | closed | 1 | 1 | 1 | docs/specs/status/D240-construction-allocation-1.md | c7ff89ca |

> `plan_state` (`pending`|`running`|`ready`) is plan readiness, **not** execution status; `ready` ≠ `go`/`done`.

## Groups (allocated only after ALL-barrier)

| group | tasks (topological order) | worktree | branch/ref | status |
|-------|---------------------------|----------|------------|--------|
| G1 | **T207 → T237 → T234 → T240** | none (single group) | current branch `1.0.9-rc3` (tip `044d03e2`) | **incomplete** — T240 terminal `incomplete` (perf allocation goal; no commit, patch saved) |

## Phase decisions

- **P.1 Bootstrap (2026-10-16):** scope fixed by owner decision (4 in-repo tasks; #235/#238 gaps). Variant per task by language rule: #207/#237/#240 → `pdca-dotnet`; #234 (Python/shell evidence tooling, "no product change") → `pdca-coder`.
- **P.2 Own PLAN per task — DONE (2026-10-16).** All four PLANs persisted `plan_state=ready` at N=1/r=1/rv=1 by their `pdca-orchestrator` PLAN streams. Plan handoffs (footprint / requirements / prerequisites):
  - **T207** — footprint: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/DataContext/EntitySelectListBuilder.cs`, tests (`tests/nextorm.sqlite.tests/EntityItemProjectionTests.cs`, new `tests/nextorm.core.tests/EntitySelectListBuilderCancellationTests.cs`), `docs/specs/design/code-smells-review.md`. No predecessors. Feasible in-workspace (core+SQLite need no `DOCKER_HOST`). Red state already reproduced at `c7ff89ca`.
  - **T237** — footprint: `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`, `src/nextorm.core/Visitors/{BaseExpressionVisitor,NormSqlTranslator}.cs` (`PivotProjectionRewriter.cs` only if proven necessary), no-DB tests across 7 provider projects + `integration.tests/{CommonTestSuite.*,T232SqlServerTests,T232PivotFixture}`, 6 EN + 6 RU doc pages + XML docs. No predecessors. Conforms to approved spec `docs/specs/design/issue-237-safe-identifier-rendering.md`. Container prerequisites (`DOCKER_HOST`) for integration.
  - **T234** — footprint: `scripts/pdca_evidence.py`, `scripts/tests/**`, `scripts/README.pdca-evidence.md`, `.github/workflows/dotnet.yml`, `.opencode/skills/nextorm-pdca/SKILL.md`, generated output under `artifacts/pdca/T234/rv1/**`. No predecessors. **Out-of-workspace slice R234-03G** (canonical admission into the global `pdca-dotnet` dir `~/.config/opencode/**`) is NOT writable here → stays a documented gap; the in-repo generator+lint+CI+overlay wiring is implementable/testable here.
  - **T240** — footprint: `src/nextorm.core/Query/{QueryCommand.cs,QueryCommand.Clone.cs,QueryCommand.TResult.cs}`, `src/nextorm.core/Builders/EntityBuilder.cs`, tests, new `eng/perf/t240_evidence.py`, perf docs. Protected (not touched): `QueryCommand.QueryPreparer.cs`, `QueryCommand.Plan.cs`, `QueryPlanEqualityComparer.cs`, `ExpressionPlanEqualityComparer.cs`, `QueryPlanner.cs`, `QueryPlanStore.cs`. No predecessors. **Two open items:** (a) owner decision on whether `eng/perf/t240_evidence.py` (reproducibility adapter) is within spec §10; (b) feasibility "PLAUSIBLE, NOT DEMONSTRATED" — RecursiveCte needs ~36% non-recipe cut; if unreachable → owner escalation with measured residual, never a lowered threshold.
- **P.3 ALL-barrier — PASSED (2026-10-16).** All four own PLANs are `ready` (no task left a `pending`-placeholder; T234's out-of-workspace slice is an explicit named gap, not a placeholder). Early DO before this point: none.
- **P.4 Clustering (2026-10-16) — ONE group G1.** Rationale: (i) T207/T237/T240 all modify the shared `nextorm.core` query/dialect/visitor infrastructure and the collection CHECK runs on the integrated tree; (ii) T240's perf acceptance (BDN `SqliteBenchmarkFeaturesFairCached`) requires an **exclusive** machine — a concurrent lane would make its numbers non-comparable (contract: non-comparable runs are not regression evidence) — so the lane must be serialized; (iii) no cross-task file overlap exists, so a single sequential lane is conflict-free. `>=2`-group machinery (worktree, group/task branches, `merge --no-ff`) is therefore NOT created. Order **T207 → T237 → T234 → T240** (perf last / most escalation-prone last). Revision vector for the schedule: T207(r=1) · T237(r=1) · T234(r=1) · T240(r=1).
- **D merge strategy:** n/a — one group ⇒ current worktree/branch `1.0.9-rc3`, commits (auto-commit authorized by the explicit user request) land in the current branch; no `merge --no-ff`, no push. **Notice:** no `todowrite` tool exists in this harness (Code Mode catalog) → the sidebar todo mirror is unavailable; this status file is the single source of truth (rebuild-sensitive).

## Lane log

- 2026-10-16 | T207 | **done** | commit `ec42c960` (6 files, 492+/19-, no push) | CHECK PASS (8/8 contract rows met, validator exit 0); build 0W/0E; core 2571/2571, sqlite 8/8, boundary sweep 11 projects exit 0; coverage 85.7% line / 78.2% branch; perf cached/prepared 1.72 vs baseline 1.87 (≤2.244). Residual slice (sorting/grouping) split to new issue **#241** (milestone 1.0.9-rc3). Status `docs/specs/status/D207-projection-cancellation-1.md`.
- 2026-10-16 | T237 | **done** | commits `11268ba7` + `4557bd14` (39 files, no push) | CHECK PASS (11/11 criteria met); Debug+Release 0W/0E; 7/7 unit suites exit 0; integration exit 0 with all 5 provider routes executed (0 T237 skips); coverage 88.97% line / 80.39% branch; mutation 6/6 killed; benchmark 1.63 within band; DocFX exit 0; EN↔RU 5/5; no public-API change. Status `docs/specs/status/D237-identifier-safety-1.md`.
- 2026-10-16 | T234 | **done (in-repo slice)** | commits `7acad56d` + `be0cbadb` | r=2/rv=2 CHECK PASS (E234-01…07 + R234-01/02/03L/04/05 met); 56/56 unittest, 16/16 negative cases rc2, coverage 95.06%, 5/5 mutants killed, byte-identical regeneration, frozen tree recompute equal. Residual (named, invariant 8): **R234-03G / E234-08** — global canonical evidence-gate admission (unauthorized global `pdca-dotnet` dir) → follow-up issue **#242** (same milestone); **#234 left OPEN**.
- 2026-10-16 | T240 | **incomplete** (terminal) | **no commit** — CHECK FAIL → STOP; perf goal ≤1.20× unreachable within approved §6 scope: Cte 1.1852 MET, Distinct 0.9640 MET, Except 1.2426, Intersect 1.2511, RecursiveCte 1.5669; time MET all five (0.44–0.62); acceptance 7 cases / 46.13 s; 2 pre-existing immutability defects found+fixed red→green. Changes saved as patch `artifacts/pdca/D240/rv1/T240-uncommitted.patch` (84977 B, md5 `6c59e5f0e24078a44f84db1afabfc140`) + `/tmp/nextorm/T240-uncommitted.patch`, then rolled back to tip `044d03e2`. **Owner decision pending** (bounded extra budget OR accept the RecursiveCte/Except/Intersect residual in 1.0.9-rc3). Status `docs/specs/status/D240-construction-allocation-1.md`.
- 2026-10-16 | **owner-side commit `044d03e2`** on `1.0.9-rc3`: `#235 T235: синхронизировать scripts/validate_inner_loop.py с canonical` (only `scripts/validate_inner_loop.py`, +770/-51). NOT part of this collection (the owner excluded #235 as out-of-workspace at start). Left in place (not reverted). **Resolved:** the owner closed **#235 and #238** as COMPLETED on 2026-10-16; this commit is consistent with that and touches no product code.

## General verification & recovery

- state: **`pass`** (general C on the integrated tree).
- last C result / evidence: **PASS** — `artifacts/pdca/collection-1.0.9-rc3-open/C/gather.md` (raw gates) + verdict from `check` (medium). Integrated tree `1.0.9-rc3` @ `044d03e2` (T207 `ec42c960`, T237 `11268ba7`+`4557bd14`, T234 `7acad56d`+`be0cbadb`; T240 rolled back to patch): build Debug/Release 0W/0E; 7 unit suites all exit 0 (core 2578/0, sqlite 1759/1 env-gate, postgres 981/0, sqlserver 972/8 explicit, mysql 459/0, mariadb 341/0, clickhouse 738/0); integration 5 providers exit 0 (3985 total, 0 failed, 198 capability skips, 0 provider skips); coverage line **87.5%** / branch **79.3%** (≥85/75); DocFX exit 0 (2 pre-existing warnings, 0 errors); suppression/slop 0 added; tree identity `044d03e2`, 0 tracked modifications.
- defect id / history: none — no product defect found by C.
- correction-task status file: none (no corrective task needed).
- next allowed step: **A (completion)** — issue disposition + report. T240 owner decision still pending (task-level, not a C blocker).

## Done / Verified / Incomplete

- **Done:** T207 (#207, `ec42c960`; split slice → #241), T237 (#237, `11268ba7`/`4557bd14`), T234 (#234 in-repo slice, `7acad56d`/`be0cbadb`; residual R234-03G → #242).
- **Verified:** collection C = **PASS** on the integrated tree `1.0.9-rc3` @ `044d03e2`.
- **Incomplete:** T240 (#240) terminal `incomplete` — perf allocation goal unmet (RecursiveCte 1.5669, Except 1.2426, Intersect 1.2511 > 1.20×); changes preserved as patch, rolled back; owner decision pending. Group G1 = `incomplete` (single group: commits stay on the branch, no merge).
- **Gaps:** none open. **#235** and **#238** were never executed by this collection (out-of-workspace canonical) and are now **CLOSED (COMPLETED) by the owner** (2026-10-16).
- **Owner-side change:** commit `044d03e2` (#235 repo-copy sync of `scripts/validate_inner_loop.py`) sits on the branch; not part of this collection; consistent with the owner's now-CLOSED #235. Left in place.
- **Issue disposition:** #237 → CLOSED (fully delivered). #207, #234, #240 → left OPEN (split/residual/incomplete). Follow-ups created: #241 (from T207), #242 (from T234).
