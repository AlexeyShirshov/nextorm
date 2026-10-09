# Collection status: rc2-reclaim

- collection-id: `rc2-reclaim`
- repository: `/home/alex/sources/nextorm`
- branch: `1.0.9-rc2` @ baseline `cf34f910`
- mode: single lane (current worktree/branch); no worktrees, no group/task branches, no merge
- autocommit: AUTHORIZED (task files only, `git add <paths>`, message prefix `#<issue>`); push never
- evidence contract: `docs/specs/status/collection-1.0.9-rc2-C-evidence.md`
- skills: pdca-collection + pdca-dotnet + nextorm-pdca

## Input task list (P-phase)

| id | issue | title | selected_variant | prior state |
|---|---|---|---|---|
| R184 | #184 | LoadWith: fold `AsSingleQuery` into an `EagerLoadMode` parameter | pdca-dotnet | OPEN, prior terminal r=2, product not in tree |
| R175 | #175 | JoinInto test-quality debt: hash-inequality checks / `EagerLoadSpec.Assign` visibility | pdca-dotnet | OPEN, prior terminal r=1, product reverted |
| R177 | #177 | JSON streaming Phase 3: DB-side JSON fast-path (`FOR JSON`/`json_agg`/`JSONEachRow`) | pdca-dotnet | OPEN, prior terminal r=2/rv=2, product in tree (`2ac20818`), missing native-path tests |
| R190 | #190 | Fix whole-entity selection from JOIN projections | pdca-dotnet | CLOSED 2026-10-08, product in tree (re-verification task) |

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| — | — | — | (single lane: current worktree) | `1.0.9-rc2` | pending |

## Tasks

| id | group | branch | status | plan_state | selected_variant | cycle_id | status_file | plan_revision | baseline ref |
|---|---|---|---|---|---|---|---|---|---|
| R184 | — | `1.0.9-rc2` | pending | pending | pdca-dotnet | 184-1 | `docs/specs/status/rc2-184-<slug>-1.md` | r=1 (prior terminal r=2) | TBD |
| R175 | — | `1.0.9-rc2` | pending | pending | pdca-dotnet | 175-1 | `docs/specs/status/rc2-175-<slug>-1.md` | r=1 (prior terminal r=1) | TBD |
| R177 | — | `1.0.9-rc2` | pending | pending | pdca-dotnet | 177-1 | `docs/specs/status/rc2-177-<slug>-1.md` | r=1 (prior terminal r=2) | TBD |
| R190 | — | `1.0.9-rc2` | pending | pending | pdca-dotnet | 190-1 | `docs/specs/status/rc2-190-<slug>-1.md` | r=1 (re-verification) | TBD |

## Collection phase decisions

- P.PLAN: own PLAN per task (gather → planner → coder persists) — IN PROGRESS
- ALL-barrier: NOT PASSED (awaiting all 4 plans ready)
- Clustering: not run
- Integration: none (single lane)

## Overall verification and recovery

- last overall CHECK: none
- state: unverified
- next allowed step: complete per-task plans, then ALL-barrier

## PLAN revision vector (schedule binding)

- not bound yet

## Done / Verified / Incomplete

- Done: (bootstrap)
- Verified: —
- Incomplete: —
