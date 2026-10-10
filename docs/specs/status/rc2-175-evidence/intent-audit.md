# D175 intent audit (D:175.1) — plan-key inequality vs hash/execution/identity

Branch: 1.0.9-rc2 @ 04836505; base 18659e41 (ancestor exit 0). No tracked local modifications.
Roslyn used for symbol/reference resolution; file:line from current tree.

## Plan-inequality sites (H01–H08)

| ID | Site | Comparer (actual, Roslyn-resolved) | Operands / scenario | Paired `Equals == false` | Hash `NotBe` verdict |
|---|---|---|---|---|---|
| H01 | `SelectExpressionPlanEqualityComparerTests.cs:34` | `SelectExpressionPlanEqualityComparer` (direct, :16) | two `SelectExpression` differing only `PhysicalColumnName` | :33 | redundant -> DELETE |
| H02 | `SelectExpressionPlanEqualityComparerTests.cs:60` | `SelectExpressionPlanEqualityComparer` | entity item vs scalar column | :58-59 | redundant -> DELETE |
| H03 | `SelectExpressionPlanEqualityComparerTests.cs:72` | `SelectExpressionPlanEqualityComparer` | different projection slots | :70-71 | redundant -> DELETE |
| H04 | `ImplicitNavigationR3CountBoundaryTests.cs:102` | `command.GetSelectExpressionPlanEqualityComparer()` => `SelectExpressionPlanEqualityComparer` (:95) | wide-count vs ordinary int column | :100-101 | redundant -> DELETE |
| H05 | `RawSourceBindingFilterTests.cs:927` | `FromExpressionPlanEqualityComparer` (`NewFromComparer`, :908) | bound entity type differs | :925-926 | collision-only claim -> DELETE (rename test, drop "DistinctHash") |
| H06 | `RawSourceBindingFilterTests.cs:948` | `FromExpressionPlanEqualityComparer` | same column count, different names | :946-947 | collision-only claim -> DELETE (rename test, drop "DistinctHash") |
| H07 | `PlanKeyStructureTests.cs:238` | `QueryPlan` structural equality (uses `QueryPlanEqualityComparer`) | same command, different rendered SQL | :237 | redundant -> DELETE |
| H08 | `InMemoryTests.cs:334` | `QueryPlanEqualityComparer(q1)` (:332) | two different plans | :335 | redundant -> DELETE |

All eight already prove inequality with `comparer.Equals(...) == false`; the `NotBe(GetHashCode...)`
line is a weak/collision-based second oracle. Production hash comparers (`SelectExpression…`,
`FromExpression…`, `QueryPlanEqualityComparer`) declare no "unequal inputs must hash apart" invariant;
`PlanKeyStructureTests.EqualHashes_ShouldNotImplyEquality_ForQueryableConstant:415-423` and
`QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality:292` prove collisions are legal
and resolved by structural equality. No site has a documented standalone hash-inequality contract.

## Retained hash-equality (coherence, R175-COH) — unchanged

`SelectExpressionPlanEqualityComparerTests.cs:48`; `JoinIntoPlanKeyTests.cs:63,91,106,135`;
`JoinIntoSqlGenerationTests.cs:136`; `PlanKeyStructureTests.cs:231,256,274,281,326,327,382`;
`RawSourceBindingFilterTests.cs:863-864,1006`; `InMemoryTests.cs:353,358`.

## Retained execution / identity (R175-SEM) — unchanged

`JoinIntoExecutionTests.cs:55,76` (`CountingInterceptor.Executions == 1`);
`JoinIntoSqlGenerationTests.cs:159-162` (`ReferenceEquals` reuse/miss identity);
`CommonTestSuite.JoinInto.cs:221-222` (interceptor counts; integration file unchanged -> guard).

## Assign visibility (V175-A01/A02)

`NextORM.Core.EagerLoadSpec<TEntity,TChild,TKey>.Assign` is declared `internal` at
`src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:307`. Definition + 6 references, all inside
`nextorm.core`:
`EntityBuilderEagerLoading.cs:164,201,209`; `JoinIntoManyToManySpec.cs:331`; `JoinIntoSpec.cs:308,424`.
No test/benchmark/other-assembly consumer. Evidence pack "6" = reference count; enumeration included the
definition (7 total). Decision: keep `internal`; no widening, no `private`.

## T-variants

Changed sites are direct comparer assertions on in-memory objects; no null/default/provider/flag
branch is introduced or removed (guard). Scenario operands and constructors unchanged.

## Baseline (focuses)

- CORE-FOCUSED: `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "<sel1>"` -> exit 0,
  total 252, failed 0, skipped 0 (`core-focused-baseline.log`).
- SQLITE-FOCUSED: `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "<sel2>"` -> exit 0,
  total 16, failed 0, skipped 0 (`sqlite-focused-baseline.log`).
