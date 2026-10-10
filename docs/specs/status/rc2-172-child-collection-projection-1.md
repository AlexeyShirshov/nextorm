## Collection rc2-final — own PLAN adjudication (2026-10-09, HEAD 9a2a2871)

- task: T172 / #172; selected_variant: `pdca-dotnet`; cycle_id: N=1; plan_revision: r=1
- plan_state: **gap** (no `ready` placeholder created; barrier not passed)
- CLASSIFICATION: **GAP** — no meaningful in-milestone deliverable is derivable.

### Reason
No concrete consumer, no agreed API/semantics, and no new, testable acceptance criterion for arbitrary/anonymous child-collection projection. Adding a speculative API or another unsupported-feature guard would invent scope rather than close the gap.

### Evidence
- `docs/specs/roadmap/todo_navigation_properties.md:335-338` — explicitly deferred pending a concrete consumer + agreed API/semantics.
- `docs/advanced/relationships.md:67` — the historical `NORM.ChildCollection(...)` form is not provided; Roslyn finds no symbol/stub.
- `docs/advanced/relationships.md:65-73` — existing `JoinInto` requires a declared `ICollection<TChild>`; no anonymous-projection requirements.
- `docs/guide/28-streaming-data.md:169-170`; `tests/nextorm.core.tests/JsonShapeWriterTests.cs:327` — JSON child-collection queries already excluded/rejected; re-pinning that guard would not deliver #172.
- `docs/specs/status/rc2-176-json-nested-1.md:131,187` — #176 leaves collection projection deferred to #172.
- `docs/specs/status/rc2-172-child-collection-projection-1.md:7,12,15,18-19,27,44-48` — prior GAP, #172 open, neither done nor superseded.

### Unblock trigger
A concrete consuming scenario plus agreed API/semantics sufficient to define observable acceptance criteria and negative cases; then gather the footprint and return T172 to PLAN.

### Collection disposition
The ALL-barrier does not pass; collection `rc2-final` stops here. T172 remains unresolved/open; no `ready` placeholder, implementation plan, or DO authorization. This confirms the existing GAP; it is not a revised implementation plan and does not reset revision/attempt counters.

---

# PDCA PLAN — Issue #172 Child-collection projection into anonymous/derived form (NORM.ChildCollection)

- collection: `1.0.9-rc2`
- task: D172
- cycle: N=1, plan revision r=1, attempt n=0 (PLAN only; DO not started)
- selected_variant: `pdca-dotnet`
- phase: PLAN complete — `plan_state=gap`
- issue: https://github.com/AlexeyShirshov/nextorm/issues/172
- base: branch `1.0.9-rc2` @ `18659e41`

## Goal
Decide whether a bounded implementation plan for the historical `NORM.ChildCollection(...)` anonymous child-collection projection can be approved. Outcome: cannot — the issue's own precondition (concrete consumer + agreed API/semantics) is unmet, so no implementation plan is authorized. This file records the gap; implementation is deferred until the precondition is satisfied.

## Decision
`plan_state=gap` (planner, high confidence).

## Unmet precondition
- Issue #172 requires "конкретный потребитель + согласованные API/семантика → реализация"; neither is established in the evidence.
- Deferred entry and trigger: `docs/specs/roadmap/todo_navigation_properties.md:333-336`.
- Capability explicitly documented unavailable: `docs/advanced/relationships.md:67` (+ RU mirror).

## Why no bounded plan can be derived
- Acceptance criteria (incl. negative cases) would require inventing grouping/identity, child shape, empty/null behavior, join semantics and supported terminals.
- Variant matrix cannot be closed as test/guard/deferred without those choices.
- Existing seams do not authorize a new API: projection at `src/nextorm.core/Builders/EntityBuilder.cs:249`; `As<TResult>` guards over JoinInto/eager state at `:341-342`.
- JoinInto restrictions remain constraints (`todo_navigation_properties.md:340-342`), not evidence to extend/replace.
- No `ChildCollection` symbol/stub in `src/` (absent); #40 commit `a23dea65` records the deferral.

## Plan items (status)
- No implementation D-tasks authorized; DO not started.
- Test strategy, docs plan, perf decision, reconnaissance decision, unit mode, variant matrix, evidence contract: all underivable without the missing consumer/semantics — not fabricated.

## Footprint (potential, unconfirmed)
- `src/nextorm.core/Builders/EntityBuilder.cs`
- `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs`
- `src/nextorm.core/Builders/Joins/JoinIntoStitching.cs`
- `src/nextorm.core/DataContext/InMemoryProjectionFactory.cs`
- `src/nextorm.core/DataContext/RowMapperFactory.cs`
- docs EN/RU `docs/advanced/relationships.md`, `docs/ru/advanced/relationships.md`

Actual edits/tests undetermined.

## Predecessor-result requirements
- An approved consuming scenario with observable expected results, agreed API/semantics, and explicit unsupported cases. No technical predecessor suffices.
- Do not substitute assumptions for the missing consumer; do not invent a public API (nextorm invariants 1/2).

## Assumptions / prerequisites
- Prerequisite: issue author supplies the concrete consumer + agreed API/semantics; then re-enter PLAN.
- Escalation not warranted now (explicit unmet prerequisite, not technical uncertainty); escalate only if an authorized API-shape decision is requested.

## Risks
- Speculative public API without a consumer; expanding JoinInto's documented contract; choosing grouping/materialization semantics without a consumer.

## Routing
- Return to issue author for the concrete consuming scenario and API/semantic agreement; then re-enter PLAN.
- No DO, no worktrees/branches in this phase.

## Progress log
- 2026-10-07 PLAN | r1 | n0 | gather (scout x2) + decide (planner) | result: plan_state=gap | evidence: issue #172 body; todo_navigation_properties.md:333-336; relationships.md:67; EntityBuilder.cs:249,341-342; absent src ChildCollection. Defects: none.
