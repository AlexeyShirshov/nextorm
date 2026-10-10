# D175 variant -> symbol mapping (post-change, EC175-02)

Comparers resolved by Roslyn; `file:line` from the current tree.

| Variant | Symbol / site | Comparer level | Verdict |
|---|---|---|---|
| V175-H01 | `SelectExpressionPlanEqualityComparerTests.cs:34` site -> `Comparer_ShouldDistinguishPhysicalColumnNames` | `SelectExpressionPlanEqualityComparer` | test: Equals false; hash NotBe deleted |
| V175-H02 | `:60` -> `Comparer_ShouldDistinguishEntityItemFromScalarColumn` | `SelectExpressionPlanEqualityComparer` | test: Equals false; hash NotBe deleted |
| V175-H03 | `:72` -> `Comparer_ShouldDistinguishProjectionItemSlots` | `SelectExpressionPlanEqualityComparer` | test: Equals false; hash NotBe deleted |
| V175-H04 | `ImplicitNavigationR3CountBoundaryTests.cs:102` -> `Wide_count_flag_participates_in_the_plan_identity` | `SelectExpressionPlanEqualityComparer` (via command) | test: Equals false; hash NotBe deleted |
| V175-H05 | `RawSourceBindingFilterTests.cs:927` -> `SameSqlAndColumns_DifferentEntityType_NotEqual` | `FromExpressionPlanEqualityComparer` | test: Equals false; collision claim deleted, test renamed |
| V175-H06 | `RawSourceBindingFilterTests.cs:948` -> `SameTypeSameColumnCount_DifferentColumnNames_NotEqual` | `FromExpressionPlanEqualityComparer` | test: Equals false; collision claim deleted, test renamed |
| V175-H07 | `PlanKeyStructureTests.cs:238` -> `QueryPlan_ShouldCompareByCommandAndSql` | `QueryPlan` / `QueryPlanEqualityComparer` | test: Equals false; hash NotBe deleted |
| V175-H08 | `InMemoryTests.cs:331/334` -> `TestQueryPlanCache` | `QueryPlanEqualityComparer` | test: Equals false; hash NotBe deleted |
| V175-C01 | `JoinIntoPlanKeyTests.cs:63` `RepeatedDeclaration_ShouldHitTheSamePlan` | `QueryPlanEqualityComparer` | test: Equals true + hash Be (coherence) retained; read-only |
| V175-C02 | `JoinIntoPlanKeyTests.cs:91` | `QueryPlanEqualityComparer` | hash Be coherence retained |
| V175-C03 | `JoinIntoPlanKeyTests.cs:106` | `QueryPlanEqualityComparer` | hash Be coherence retained |
| V175-C04 | `JoinIntoPlanKeyTests.cs:135` | `QueryPlanEqualityComparer` | hash Be coherence retained |
| V175-C05 | `JoinIntoSqlGenerationTests.cs:136` `JoinInto_DistinctSpecs_…` | `QueryPlanEqualityComparer` | hash Be coherence retained |
| V175-X01 | `JoinIntoExecutionTests.cs:55` | execution count | retained |
| V175-X02 | `JoinIntoExecutionTests.cs:76` | execution count | retained |
| V175-X03 | `CommonTestSuite.JoinInto.cs:221-222` | integration interceptor counts | guard: integration file unchanged |
| V175-X04 | `JoinIntoSqlGenerationTests.cs:159-162` | `ReferenceEquals` reuse | retained |
| V175-X05 | `JoinIntoSqlGenerationTests.cs:161-162` | `ReferenceEquals` miss | retained |
| V175-A01/A02 | `EntityBuilderEagerLoading.cs:307` + 6 refs | visibility | guard: `internal`, no external consumer |

T01/T02/T03: no null/default/provider/flag variant introduced; operands/scenarios unchanged (guard).
