# D175 intent audit (EC175-01 / A1) — plan-key inequality vs hash/execution/identity

Cycle N=1, plan_revision r=1, evidence contract rv=2.
Branch `1.0.9-rc2`. Plan baseline `cf34f910` (confirmed ancestor of HEAD). Current tree HEAD `3ebaa4ee`
(plan baseline drifted forward by other rc2-reclaim cycles; the five test files are unchanged from baseline).
Roslyn (`roslyn` tool) used for symbol/reference/method resolution; `file:line` from the current tree

## Site mapping H01–H08 (plan IDs -> current logical sites)

The plan matrix cites `SelectExpressionPlanEqualityComparerTests.cs:34/:60/:72`; the current tree has the
same logical assertions at `:34/:60/:87` (the `.Should()` line) with the paired hash `NotBe` at
`:35/:61/:88`. The test methods are identical and uniquely named (Roslyn `members`), so the mapping is
by symbol + assertion text, not by drifted line number. No genuine divergence -> no PLAN revision.

| ID | Current file | Method (Roslyn-resolved) | `Equals == false` line | Hash `NotBe` line | Comparer (Roslyn-resolved) |
|---|---|---|---|---|---|
| H01 | SelectExpressionPlanEqualityComparerTests.cs | `Comparer_ShouldDistinguishPhysicalColumnNames` (:15) | :34 | :35 | `NextORM.Core.SelectExpressionPlanEqualityComparer` |
| H02 | SelectExpressionPlanEqualityComparerTests.cs | `Comparer_ShouldDistinguishEntityItemFromScalarColumn` (:53) | :59-60 | :61 | `NextORM.Core.SelectExpressionPlanEqualityComparer` |
| H03 | SelectExpressionPlanEqualityComparerTests.cs | `Comparer_ShouldDistinguishProjectionItemSlots` (:80) | :86-87 | :88 | `NextORM.Core.SelectExpressionPlanEqualityComparer` |
| H04 | ImplicitNavigationR3CountBoundaryTests.cs | `Wide_count_flag_participates_in_the_plan_identity` (:91) | :100-101 | :102 | `command.GetSelectExpressionPlanEqualityComparer()` => `SelectExpressionPlanEqualityComparer` |
| H05 | RawSourceBindingFilterTests.cs | `SameSqlAndColumns_DifferentEntityType_NotEqualWithDistinctHash` (:912) | :925-926 | :927-929 | `FromExpressionPlanEqualityComparer` (`NewFromComparer`, :908) |
| H06 | RawSourceBindingFilterTests.cs | `SameTypeSameColumnCount_DifferentColumnNames_NotEqualWithDistinctHash` (:933) | :946-947 | :948-950 | `FromExpressionPlanEqualityComparer` |
| H07 | PlanKeyStructureTests.cs | `QueryPlan_ShouldCompareByCommandAndSql` (:214) | :237 | :238 | `QueryPlan` structural equality (uses `QueryPlanEqualityComparer`) |
| H08 | InMemoryTests.cs | `TestQueryPlanCache` (:368) | :383 | :382 | `QueryPlanEqualityComparer(q1)` (:380) |

All eight already prove inequality with `comparer.Equals(...) == false`; the `NotBe(GetHashCode...)`
line is a weak/collision-based second oracle. The production comparers
(`SelectExpressionPlanEqualityComparer`, `FromExpressionPlanEqualityComparer`, `QueryPlanEqualityComparer`)
declare no "unequal inputs must hash apart" invariant; `PlanKeyStructureTests.EqualHashes_ShouldNotImplyEquality_ForQueryableConstant:415-423`
and `QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality:292` prove collisions are legal
and resolved by structural equality. H05/H06 method names assert the false hash-distinctness contract and
the SelectExpression XML doc says "hash apart" -> those intent texts are corrected in R2.

## Retained hash-equality coherence (R175-COH) — unchanged

`SelectExpressionPlanEqualityComparerTests.cs:48`; `PlanKeyStructureTests.cs:231,256,274,281,326,327,382`;
`RawSourceBindingFilterTests.cs:863-864,1006`; `InMemoryTests.cs:353(commented),401,406`;
`JoinIntoPlanKeyTests.cs:63,91,106,135`; `JoinIntoSqlGenerationTests.cs:136`.

## Retained execution / identity (R175-SEM) — unchanged

`JoinIntoExecutionTests.cs:55,76` (execution count); `JoinIntoSqlGenerationTests.cs:159-162`
(`ReferenceEquals` reuse/miss); `CommonTestSuite.JoinInto.cs:221-222` (interceptor counts; integration
file unchanged -> guard A4).

## Assign visibility (EC175-03 / A3)

`NextORM.Core.EagerLoadSpec<TEntity,TChild,TKey>.Assign` is `internal` at
`src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:307:19` (Roslyn `members`). Roslyn `refs`
enumerated 6 references, all inside `src/nextorm.core`:
`EntityBuilderEagerLoading.cs:164,201,209`; `JoinIntoManyToManySpec.cs:331`; `JoinIntoSpec.cs:308,424`.
No test/benchmark/other-assembly consumer. Decision: keep `internal`; no widening, no narrowing.

## T-variants

Changed sites are direct comparer assertions on in-memory objects; no null/default/provider/flag branch
is introduced or removed (guard). Scenario operands and constructors unchanged.

## Baseline focused evidence (R1)

- FC1 `SelectExpressionPlanEqualityComparerTests` exit 0, total 12 / failed 0 / skipped 0
  (`focused/core-baseline/FC1-selectexpr.log`).
- FC2 `RawSourceBindingFilterTests` exit 0, total 60 / 0 / 0 (`focused/core-baseline/FC2-rawsource.log`).
- FC3 `ImplicitNavigationR3CountBoundaryTests` exit 0, total 7 / 0 / 0 (`focused/core-baseline/FC3-implicitnav.log`).
- FC4 `PlanKeyStructureTests` exit 0, total 19 / 0 / 0 (`focused/core-baseline/FC4-plankey.log`).
- FC5 `InMemoryTests` exit 0, total 93 / 0 / 0 (`focused/core-baseline/FC5-inmemory.log`).
- FS1 sqlite `JoinIntoExecutionTests` exit 0, total 7 / 0 / 0 (`focused/sqlite-baseline/FS1-joinexec.log`).
- FS2 sqlite `JoinIntoSqlGenerationTests` exit 0, total 9 / 0 / 0 (`focused/sqlite-baseline/FS2-joinsql.log`).
- Build baseline exit 0, 0 warnings / 0 errors (`build/baseline.log`).
