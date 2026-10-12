# T213 D02(r=2) direction-B focused probe RESULT: **FAILED (STOP → DO→PLAN candidate)**

- UTC: 2026-10-12T14:45:00Z (approx; see commit)
- Scope: escalation-approved direction B implemented minimally (composite junction branch in
  `JunctionLinkSourceFactory.BuildSource`, composite link item/accessors + `Create` in
  `JoinIntoManyToManySpec`, `BuildPairedKeyEquality` made reachable). Probe test:
  `JoinIntoCompositeJunctionProbeTests.CompositeJunction_ManyToMany_ShouldPrepareSql`
  (`tests/nextorm.sqlite.tests`, archive copy in this folder). Log:
  `../probe-composite-junction.log`.
- Source diff (before revert): `direction-b.patch`.

## Answer: NO — direction B does not flow. Exact failure

The derived `Projection<TJunction, long>` link source builds, but the composite join predicate
`link.Item1.<parent-FK>` is not translatable: the alias resolver cannot resolve the **nested
projection-item access chain**.

- Exit: 2. Failure:
  `System.InvalidOperationException : Operation is not valid due to the current state of the object.`
  at `NextORM.Core.MemberTranslator.ResolveProjectionItemAliasIndex`
  (`src/nextorm.core/Visitors/MemberTranslator.cs:827`)
  ← `MemberTranslator.VisitMember` (`:487`)
  ← `BaseExpressionVisitor.VisitMember` / `VisitComparisonOperand`
  ← `WhereExpressionVisitor.VisitBinary`
  ← `SqlSourceRenderer.MakeWhere` (`DataContext/SqlSourceRenderer.cs:999`)
  ← `SqlSourceRenderer.MakeJoin` (`:270`)
  ← `SqlBuilder.MakeSelect` (`:185`)
  ← `QueryPlanner.GetPreparedQueryCommand`.
- Meaning: `VisitMember` at `:483-494` treats `l.Item1.ParentId` as a projection-item access
  (`node.Expression` is `l.Item1`, `ProjectionAliasCache.GetMemberPosition` → position 1) and asks
  `ResolveProjectionItemAliasIndex(columnsProvider, itemType = <junction type>, occ, l, ...)`.
  `FindAlias(<junction type>, ...)` returns null because the derived link subquery is projected by
  the `MemberInit` branch (`QueryCommand.QueryPreparer.cs:685-728`), which emits one **entity-typed**
  `Item1` column instead of flattening the junction into slot-tagged mapped columns. The
  `IdentitySlotAliases` fallback (`:822-824`) is false for this ordinary pair command, so the miss
  fails closed at `:827` rather than emitting an unqualified column.

## Why this is the planned STOP, not a fixable direction-B detail

- Instruction step 2 required the composite ON to read `link.Item1.<fk>`; the probe shows the SQL
  path does **not** accept that `Item1.Prop` access chain (contradicting the plan's "Verify it
  accepts an `Item1.Prop` access chain").
- The fix would be to make the derived link source expose `Item1` as the junction's slot-tagged
  mapped columns (i.e. route the link subquery through the mixed-projection expander
  `TryBuildProjectionSelectList` instead of the `MemberInit` branch) and/or teach
  `MemberTranslator`/alias resolution nested projection-item access. Both are **nested-projection
  support**, which the planner placed outside the confined D03 scope and named a DO→PLAN candidate.
  It is not part of direction B's three steps.
- Scalar junction path is unaffected: filtered `FullyQualifiedName~JoinInto` in
  `nextorm.sqlite.tests` with the direction-B source in place = 58 succeeded / 1 failed (the probe
  only). The implementation is reverted so the composite case keeps its previous deterministic
  `NotSupportedException` (fail-closed) rather than a confusing alias error.

## Routing

STOP. No production source changed (direction-B diff archived, reverted). This decision routes back
to `planner`: nested `Projection<..>` link-item support (expander + alias resolution) is required
before direction B can be completed, or a different composite link carrier must be selected.
