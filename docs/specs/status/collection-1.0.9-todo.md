# Collection 1.0.9-todo

- collection-id: `1.0.9-todo`
- input: milestone `1.0.9` (milestone #4) open issues — **6 tasks**: #213, #212, #211, #209, #201, #53
- base: branch `1.0.9` @ `58025147` (merge of #214 `1.0.9-rc2`)
- mode: autonomous; auto-commit authorized (task/collection status files and task paths only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- phase: **P — own PLANs of all tasks (bootstrap)**

## Input task list

Selected variant: `pdca-dotnet` for all tasks (C#/.NET repo). Each task runs its own real PLAN at the P phase and stops at the PLAN→DO boundary.

| id | issue | title | selected_variant | state |
|---|---|---|---|---|
| T209 | #209 | eager-load strategy — explicit strategy + auto-dispatcher | pdca-dotnet | own PLAN pending |
| T211 | #211 | eager-load per-parent Take/Skip (absorbs #210) | pdca-dotnet | own PLAN pending |
| T212 | #212 | eager-load nested — level 2+ LoadWith | pdca-dotnet | own PLAN pending |
| T213 | #213 | JoinInto — composite keys / M:N single-query | pdca-dotnet | own PLAN pending |
| T201 | #201 | Perf: M12 #3 — cached-path parity with LINQ to DB | pdca-dotnet | own PLAN pending |
| T53 | #53 | IF6: public API freeze (PublicApiAnalyzers + PublicAPI.*) | pdca-dotnet | own PLAN pending |

## Groups

_not yet built (ALL-barrier not passed)_

## Tasks

| id | issue | group | order | branch | status | plan_state | selected_variant | cycle_id | plan_revision | status file | reason + patch |
|---|---|---|---|---|---|---|---|---|---|---|---|
| T209 | #209 | — | — | — | pending | pending | pdca-dotnet | N=1 | r=1 | docs/specs/status/D209-eager-strategy-1.md | own PLAN pending |
| T211 | #211 | — | — | — | pending | gap | pdca-dotnet | N=1 | r=1 | docs/specs/status/D211-eager-perparent-topn-1.md | own PLAN blocked: planner (medium) unavailable — upstream funds; resource blocker, no plan |
| T212 | #212 | — | — | — | pending | pending | pdca-dotnet | N=1 | r=1 | docs/specs/status/D212-eager-nested-1.md | own PLAN pending |
| T213 | #213 | — | — | — | pending | pending | pdca-dotnet | N=1 | r=1 | docs/specs/status/D213-joininto-remainder-1.md | own PLAN pending |
| T201 | #201 | — | — | — | pending | pending | pdca-dotnet | N=1 | r=1 | docs/specs/status/D201-cached-parity-1.md | own PLAN pending |
| T53  | #53  | — | — | — | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/D53-public-api-freeze-1.md | own PLAN ready (r1) — stopped at PLAN→DO boundary |

## Phase decisions

- **Bootstrap:** input list, ids, selected variants; no code, no branches, no worktrees.
- **Own PLANs (P):** in progress — each task runs its own real PLAN (scout gather → planner decide → coder persist), stopping at the PLAN→DO boundary (no DO).
- **T211 PLAN blocked (resource):** PLAN decide failed twice with upstream `Insufficient account funds`; `plan_state=gap`, ALL-barrier not passed for T211. Recovery: re-run T211 own PLAN after the medium-tier provider is funded (scout pack already gathered).
- **T53 PLAN ready:** own PLAN r1 persisted to docs/specs/status/D53-public-api-freeze-1.md; awaiting other tasks' PLANs for ALL-barrier.
- **DAG/groups:** not yet built.

## Общая верификация и восстановление

- Verification state: n/a (P phase).
- Next allowed step: complete own PLANs of all tasks (T211 blocked on planner funds), then ALL-barrier.

## Done / Verified / Incomplete

- Done: —
- Verified: —
- Incomplete: —

