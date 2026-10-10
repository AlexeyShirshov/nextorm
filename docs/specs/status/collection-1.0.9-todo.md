# Collection 1.0.9-todo

- collection-id: `1.0.9-todo`
- input: milestone `1.0.9` (milestone #4) open issues — **6 tasks**: #213, #212, #211, #209, #201, #53
- base: branch `1.0.9` @ `58025147` (merge of #214 `1.0.9-rc2`)
- mode: autonomous; auto-commit authorized (task/collection status files and task paths only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- phase: **P BLOCKED — ALL-barrier NOT passed. Environment blocker: medium-tier provider `opencode/gpt-6.1-sol` returns "Insufficient account funds"; role `planner` (and by extension `check`) unavailable.**

## Input task list

Selected variant: `pdca-dotnet` for all tasks (C#/.NET repo). Each task runs its own real PLAN at the P phase and stops at the PLAN→DO boundary.

| id | issue | title | selected_variant | state |
|---|---|---|---|---|
| T209 | #209 | eager-load strategy — explicit strategy + auto-dispatcher | pdca-dotnet | own PLAN **gap** (resource) |
| T211 | #211 | eager-load per-parent Take/Skip (absorbs #210) | pdca-dotnet | own PLAN **gap** (resource) |
| T212 | #212 | eager-load nested — level 2+ LoadWith | pdca-dotnet | own PLAN **ready** |
| T213 | #213 | JoinInto — composite keys / M:N single-query | pdca-dotnet | own PLAN **gap** (resource) |
| T201 | #201 | Perf: M12 #3 — cached-path parity with LINQ to DB | pdca-dotnet | own PLAN **gap** (design prerequisite) |
| T53 | #53 | IF6: public API freeze (PublicApiAnalyzers + PublicAPI.*) | pdca-dotnet | own PLAN **ready** |

## Groups

_not built — ALL-barrier NOT passed (§P.3). Early DO forbidden._

## Tasks

| id | issue | group | order | branch | status | plan_state | selected_variant | cycle_id | plan_revision | status file | reason + patch |
|---|---|---|---|---|---|---|---|---|---|---|---|
| T209 | #209 | — | — | — | pending | gap (resource) | pdca-dotnet | N=1 | r=1 | docs/specs/status/D209-eager-strategy-1.md | gather+design/test lenses done; PLAN decide blocked — `planner` (medium) "Insufficient account funds" (3 dispatches) |
| T211 | #211 | — | — | — | pending | gap (resource) | pdca-dotnet | N=1 | r=1 | docs/specs/status/D211-eager-perparent-topn-1.md | 5 scout streams done; PLAN decide blocked — `planner` (medium) out of funds (2 dispatches) |
| T212 | #212 | — | — | — | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/D212-eager-nested-1.md | own PLAN r=1 persisted (commit `034e5b45`); deps: #209/#211/#213 results for affected shapes |
| T213 | #213 | — | — | — | pending | gap (resource) | pdca-dotnet | N=1 | r=1 | docs/specs/status/D213-joininto-remainder-1.md | reconnaissance done (artifact committed `43b63ab7`); PLAN finalize blocked — `planner` (medium) out of funds |
| T201 | #201 | — | — | — | pending | gap (design) | pdca-dotnet | N=1 | r=1 | docs/specs/status/D201-cached-parity-1.md | issue approves only goal+tolerance; mandatory separately-reviewed design (`m12-3-prepare-command-design.md`) does not exist → not plannable in-cycle; **excluded-gap candidate** |
| T53  | #53  | — | — | — | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/D53-public-api-freeze-1.md | own PLAN r=1/rv1 persisted (untracked, P-phase no commit); depends on #209/#211/#212/#213 public-API results before final baseline |

## Phase decisions

- **Bootstrap:** input list, ids, selected variants; no code, no branches, no worktrees.
- **Own PLANs (P):** executed for all 6 tasks via `pdca-orchestrator` (COLLECTION TASK PLAN). Result:
  - **ready (2):** T212 (commit `034e5b45`), T53 (status file `docs/specs/status/D53-public-api-freeze-1.md`, untracked).
  - **gap — resource (3):** T209 (`5ad1f161`), T211 (`abd35b2a`), T213 (`43b63ab7`). Reconnaissance complete in each, but the **PLAN decide role `planner` (medium tier = `opencode/gpt-6.1-sol`) returns "Insufficient account funds"** — no role substitution allowed, orchestrator must not author the plan.
  - **gap — design prerequisite (1):** T201 (`9bf13403`). Issue #201 approves only goal+tolerance; a separately reviewed written design is mandatory and absent. Not satisfiable by the task's own autonomous cycle; `ready` placeholder forbidden.
- **ALL-barrier: NOT PASSED.** Not all plannable tasks have `plan_state=ready`. Early DO is forbidden.
- **DAG/groups:** not built (barrier not passed).

## Общая верификация и восстановление

- Verification state: n/a (P phase; no integration tree).
- Blocker: **environment/resource** — medium-tier provider (`opencode` / `gpt-6.1-sol`, used by `planner`, `check`, `architect`, `security-auditor`) is out of funds. The PDCA cycle cannot decide PLAN or issue a CHECK verdict without it.
- Not a product defect; no code changed beyond status/plan artifacts.
- Next allowed step (after unblock): retry `planner` decide for T209/T211/T213 (gather already done; no re-gather needed) → ALL-barrier; decide T201 exclude-gap; then cluster.

## Done / Verified / Incomplete

- Done: —
- Verified: —
- Incomplete: —
