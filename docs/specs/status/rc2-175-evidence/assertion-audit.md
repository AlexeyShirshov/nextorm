# D175 post-change assertion audit (EC175-02)

Diff: `/tmp/nextorm-D175/r1/d175.patch` (5 test files, +4/−16). No `src/**`, config, or integration
file changed.

## H01–H08 -> plan inequality is proven by `Equals == false`; hash `NotBe` removed

| ID | Post-change symbol / site | Final inequality oracle | Hash `NotBe` |
|---|---|---|---|
| H01 | `SelectExpressionPlanEqualityComparerTests.Comparer_ShouldDistinguishPhysicalColumnNames` :32-33 | `SelectExpressionPlanEqualityComparer.Equals(first, second) == false` | removed |
| H02 | `…Comparer_ShouldDistinguishEntityItemFromScalarColumn` :57-58 | `…Equals(withItem, withoutItem) == false` | removed |
| H03 | `…Comparer_ShouldDistinguishProjectionItemSlots` :69-70 | `…Equals(first, second) == false` | removed |
| H04 | `ImplicitNavigationR3CountBoundaryTests.Wide_count_flag_participates_in_the_plan_identity` :99-100 | `GetSelectExpressionPlanEqualityComparer().Equals(narrowed, ordinary) == false` | removed |
| H05 | `RawSourceBindingFilterTests.SameSqlAndColumns_DifferentEntityType_NotEqual` :912 (was `…_NotEqualWithDistinctHash`) | `FromExpressionPlanEqualityComparer.Equals(tenant.SourceFrom, opaque.SourceFrom) == false` | removed |
| H06 | `RawSourceBindingFilterTests.SameTypeSameColumnCount_DifferentColumnNames_NotEqual` :930 (was `…_NotEqualWithDistinctHash`) | `FromExpressionPlanEqualityComparer.Equals(declared.SourceFrom, swapped.SourceFrom) == false` | removed |
| H07 | `PlanKeyStructureTests.QueryPlan_ShouldCompareByCommandAndSql` :237 | `QueryPlan.Equals(renderedB) == false` | removed |
| H08 | `InMemoryTests.TestQueryPlanCache` :334 | `QueryPlanEqualityComparer.Equals(q1, q2) == false` | removed |

`rg` over the three test projects confirms no remaining live `GetHashCode(...).Should().NotBe(...)`
in scope (only a commented-out line in `CommandBuilderTests.cs:44`, untouched).

## Retained (unchanged)

- Coherence hash-equality (R175-COH): `SelectExpressionPlanEqualityComparerTests.cs:48`;
  `JoinIntoPlanKeyTests.cs:63,91,106,135`; `JoinIntoSqlGenerationTests.cs:136`;
  `PlanKeyStructureTests.cs:231,256,274,281,326,327,382`; `RawSourceBindingFilterTests.cs:863-864,1006`;
  `InMemoryTests.cs:353,358`.
- Execution/identity (R175-SEM): `JoinIntoExecutionTests.cs:55,76`; `JoinIntoSqlGenerationTests.cs:159-162`.
- Collision-is-legal counter-example retained: `PlanKeyStructureTests.EqualHashes_ShouldNotImplyEquality_ForQueryableConstant`
  and `QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality`.

## Scope / variants

Operands, constructors and scenario bodies are unchanged; only the weak second oracle was deleted and
two method names/doc-comment were corrected. `JoinIntoPlanKeyTests.cs` is unmodified. No new
null/default/provider/flag branch was introduced or removed.
