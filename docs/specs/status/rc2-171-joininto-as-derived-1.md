# RC2-171 - D171 / issue #171: JoinInto over As/derived source (PLAN, collection 1.0.9-rc2)

- task: D171 (GitHub issue #171, milestone 1.0.9-rc2)
- selected_variant: pdca-dotnet (nextorm-pdca overlay applies)
- base: branch `1.0.9-rc2` @ `18659e41`
- cycle: N=1; plan revision r = n/a (no plan issued)
- plan_state: **gap**
- phase: PLAN (COLLECTION TASK PLAN), stopped at the PLAN->DO boundary
- status: not executable until the unblock trigger; no DO performed

## Blocking reason
Placeholder "revisit on demand": no concrete consumer and no agreed API/semantics.
No derivable acceptance criteria (including a negative case) follow from the statement;
implementing derived-source support now would invent scope.

## Existing behavior / evidence (file:line)
- Guard `EnsureJoinIntoSource` rejects a derived source (`_query is not null`) or a
  projection-dimension `TEntity`: `src/nextorm.core/Builders/EntityBuilder.cs:779-784`
  (condition `:781`; throws `NotSupportedException` at `:782-783`); callers
  `EntityBuilder.cs:629`, `:707`, `:754`.
- Derived source produced by `.As<TResult>` `src/nextorm.core/Builders/EntityBuilder.cs:339`
  (`_query` field `:28`, ctor `:72`); reverse guard `EnsureNoJoinIntos` `:1016`, called from
  `As` at `:341`.
- Pinned by tests: `JoinIntoRejectionTests.cs:217` (`DerivedAsSource_ShouldThrow`),
  `:260` (`AsAfterJoinInto_ShouldThrowAtBuildTime`).
- Roadmap deferred/trigger: `docs/specs/roadmap/todo_navigation_properties.md:268`, `:325`,
  `:341`, and trigger definition `:333-336`.
- Public limitation: `docs/advanced/relationships.md:285` and
  `docs/ru/advanced/relationships.md:285`.
- Demand: none - issue open, 0 comments, no cross-reference/consumer.
- The issue's only stated current behavior ("reject now") is already satisfied by the guard
  plus the pinned tests; future support is deliberately not implemented (limitation, not a
  demonstrated defect).

## Unblock trigger
A concrete consumer plus agreed API/semantics -> a separate issue
(`docs/specs/roadmap/todo_navigation_properties.md:333-336`).

## Collection handling
Record gap; not executable until the trigger; not done; do not close issue #171
automatically; no artificial DO; do not defer work to a future milestone.

## Evidence contract
Not issued - no executable PLAN, DO not permitted. Tests were NOT run in this PLAN phase;
the test references above are static pins, not execution evidence.

## Progress log
- 2026-10-07 PLAN gather: scout evidence pack (file:line above).
- 2026-10-07 PLAN decide: planner classified `plan_state=gap`; r = n/a; N = 1.
- 2026-10-07 Stopped at the PLAN->DO boundary per COLLECTION TASK PLAN. No DO, no
  branches/worktrees, no code.
