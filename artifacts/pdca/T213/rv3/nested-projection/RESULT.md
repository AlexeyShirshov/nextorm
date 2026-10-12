# T213 D04(r=3) gated nested-projection normalization RESULT: **FAILED (STOP → planner/escalate C-vs-D)**

- UTC: 2026-10-12T20:15:00Z (approx; see commit)
- Scope: D04b/c implementation of the escalation-approved direction-B composite junction branch behind
  the D04 gate (`QueryCommand.QueryPreparer.cs` MemberInit gate, `QueryCommand.cs`/`QueryDefinition.cs`
  junction-link marker, `Visitors/MemberTranslator.cs` marker-gated alias fallback,
  `Builders/Joins/JunctionLinkSourceFactory.cs` composite `Projection<TJunction,long>` source +
  marker, `Builders/Joins/JoinIntoManyToManySpec.cs` composite branch, `Builders/EntityBuilder.cs`
  `BuildPairedKeyEquality` reachability). All edits are **reverted** to the green D03 commit
  `2983845e`; the diff is archived here as `d04-gate-attempt.patch`.
- Probe: `JoinIntoCompositeJunctionProbeTests.CompositeJunction_ManyToMany_ShouldPrepareSql`
  (`tests/nextorm.sqlite.tests`), archived copy `probe-test.cs`.

## Answer: NO — the D04 gate does not make the nested projection materialize

Two distinct failures were observed across the D04 attempts (order as the logs show):

1. **Nested-alias resolution failure** — `nested-alias-failure.log` (a.k.a. `probe.log`), exit 2:
   `System.InvalidOperationException : Operation is not valid due to the current state of the object.`
   at `NextORM.Core.MemberTranslator.ResolveProjectionItemAliasIndex`
   (`src/nextorm.core/Visitors/MemberTranslator.cs:847`) ← `VisitMember` (`:487`)
   ← `VisitComparisonOperand` ← `WhereExpressionVisitor.VisitBinary` ←
   `SqlSourceRenderer.MakeWhere` (`DataContext/SqlSourceRenderer.cs:999`) ←
   `SqlSourceRenderer.MakeJoin` (`:270`) ← `SqlBuilder.MakeSelect` (`:185`).
   Trigger: the composite join predicate reads the **nested** access chain
   `pair.Item2.Item1.ChildId` — the outer projection item's `Item2` projection item's `Item1`
   junction's `ChildId`. The marker-gated fallback resolves the first projection-item level but the
   nested second level (`Item2.Item1`) still misses among the subquery's slot-tagged columns and
   fails closed at `:847` instead of emitting the column.
2. **Materialization failure** — `materialization-failure.log`, exit 2:
   `System.ArgumentException : 'Id' is not a member of type
   'NextORM.Core.Projection\`3[ProbeCompositeParent,NextORM.Core.Projection\`2[ProbeCompositeLink,System.Int64],ProbeCompositeChild]'`
   (Parameter 'bindings[0]') at `NextORM.Core.RowMaterializerBuilder.BuildMemberInit`
   (`src/nextorm.core/DataContext/RowMaterializerBuilder.cs:468`) ← `BuildCore` (`:363`) ←
   `Build` (`:69`) ← `RowMapperFactory.Build` (`:348`/`:311`/`:223`) ←
   `QueryPlanner.GetMapCached` (`:865`) ← `GetPreparedQueryCommand` (`:669`/`:559`).
   The nested `Projection<Projection<..>,..>` carrier (outer parent, inner `Projection<junction,long>`,
   child) is accepted by SQL generation but the shared row materializer cannot build a `MemberInit`
   for it: it tries to bind member `Id` against the **outer** projection type, which has no such
   member.

## Decisive fact

The link **is** materialized in the in-memory/stitch path — `JoinIntoStitcher.Stitch` reads the link
per row at `JoinIntoStitcher.cs:214` (`linkProperties[i]!.GetValue(row)`) and hands it to
`AssignChildrenFromLinks` at `JoinIntoStitcher.cs:241`. So the gap is **not** stitch-side; it is the
nested-projection normalization that SQL generation + row materialization require (nested alias
resolution at `MemberTranslator.cs:847`, nested `MemberInit` at `RowMaterializerBuilder.cs:468`),
which the D04 gate did not and cannot provide within its confined scope.

## Why this is the planned STOP

- The D04 gate only normalizes a **single** projection-item level (`Projection<TEntity,T2>` whose
  first item is a mapped entity) on the marker; the composite junction link's nested carrier is
  `Projection<Projection<TJunction,long>, TChild>` two levels deep, and `Item2.Item1.ChildId`
  crosses both. Nested (multi-level) projection-item support was named a DO→PLAN candidate and is
  outside D04's confined scope.
- Both failures are nested-projection/materialization support, not fixable details of direction B's
  three steps. A different composite link carrier — or a planner decision between the remaining
  C-vs-D options — is required.
- The scalar junction path is unaffected: after reverting, filtered
  `FullyQualifiedName~JoinInto` is green in `nextorm.core.tests` (143/143) and
  `nextorm.sqlite.tests` (58/58), and the composite case keeps its previous deterministic
  `NotSupportedException` (fail-closed) rather than a confusing alias/materialization error.

## Routing

STOP. No production source changed (D04 diff archived here, reverted); probe test removed from the
tree. Routes back to `planner`/`escalate`: decide the C-vs-D composite link carrier, given that
nested `Projection<..>` materialization is required by direction B but falls outside the D04 gate.
