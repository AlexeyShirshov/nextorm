# D201 — Cached-path parity with LINQ to DB (#201)

- task: T201
- issue: #201 (repo AlexeyShirshov/nextorm) — https://github.com/AlexeyShirshov/nextorm/issues/201
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=1 (no revision issued)
- phase: P (own PLAN) — stopped at the PLAN→DO boundary
- plan_state: **gap** (ALL-barrier NOT passed; early DO forbidden)
- collection: `docs/specs/status/collection-1.0.9-todo.md`
- date: 2026-10-11

## Goal (approved: goal + tolerance only)

Perf M12 #3 — parity of NextORM's cached path with LINQ to DB in time and
allocations. Category B target scenario: a fresh-fluent **non-prepared** NextORM
query hitting the implicit plan-cache vs a **non-compiled** LINQ to DB query with
a warm implicit cache. Time: per-scenario `cached NextORM / cached LINQ to DB <= 1.05`
(5% approved); allocations: per-scenario `<= 1.00` (0→0); `Warm_Reused = 0 B`
preserved. Categories: Cte, RecursiveCte, Except, Intersect, Distinct.
`Prepare()` is a no-regression control only, not a goal; the public
compiled/prepared API is not a route.

## PLAN decision — `gap`

Reason — issue #201's own approval gate: only «изменение цели и допуски
(goal + tolerance)» are approved; «Черновые стадии, дизайн, спека, формальный план
и исполнение — не одобрены». The issue mandates a **separately reviewed written
design** before implementation, referencing `docs/specs/performance/m12-3-prepare-command-design.md`,
which **does not exist**. `docs/specs/performance/iteration-15-cached-path-results.md:41-47,53`
conditions that design on a trigger whose activation is unverified. This required
review/approval is **not satisfiable by the task's own autonomous cycle alone**:
producing a document or an agent review cannot replace the issue's explicit
approval gates. A `ready` placeholder is forbidden (collection ALL-barrier rule).

## Minimal unblock

The issue owner / design approver must:
1. confirm whether the M12 #3 trigger (`iteration-15-cached-path-results.md:47`) has fired;
2. authorize the conversational design-review step for #201's approved goal.

Then complete, in the issue's prescribed order: design discussion → written spec →
written-spec review. The detailed implementation-plan review still precedes any code change.

## Prerequisites / assumptions

- Require #201-specific approved design/spec; the approved #183 spec
  (`iteration-15-cached-path-design.md`) and M9's «сделано» label are **not** substitutes.
- M9 correctness and streaming obligations remain mandatory regression evidence.
- Milestone contradiction: issue metadata `1.0.9` vs body `1.1-a.1` — keep grouping
  under the current milestone **1.0.9** (invariant 8); reconcile with the issue owner.

## Planned write-footprint (uncertain)

- Unblocking design (not created): `docs/specs/performance/m12-3-prepare-command-design.md`.
- Tracking: `docs/specs/status/D201-cached-parity-1.md`; issue #201.
- Implementation / test / benchmark / doc footprint **undetermined until design approval**.
  Core anchors were gathered as impact candidates, not approved edit targets:
  `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:71-136`,
  `src/nextorm.core/Builders/CteHoister.cs:29`,
  `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs:20,31-35`,
  `src/nextorm.core/DataContext/QueryPlanner.cs:561`.

## Evidence contract

Not frozen. `artifacts/pdca/T201/rv1/manifest.json` is a planned future location only.

## Next allowed step

Return `plan_handoff` to the collection parent; the ALL-barrier is not passed.
No DO, no branches, no worktrees, no product code.

## Progress log

- 2026-10-11 — PLAN gather (`scout`, issue #201 + refs + code anchors + benchmark gate)
  → decide (`planner`) → `plan_state=gap` persisted; stopped at PLAN→DO boundary.
