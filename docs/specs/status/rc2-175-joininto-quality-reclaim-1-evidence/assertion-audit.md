# D175 post-change assertion audit (EC175-02 / A2)

Diff: `do.patch` (5 core test files, +4 / −16). No `src/**`, config, public docs, coverage settings or
integration file changed. Tree HEAD `3ebaa4ee` + worktree edits.

## H01–H08 -> inequality proven by `Equals == false`; hash `NotBe` removed

| ID | Post-change method (Roslyn) / site | Final inequality oracle | Hash `NotBe` |
|---|---|---|---|
| H01 | `SelectExpressionPlanEqualityComparerTests.Comparer_ShouldDistinguishPhysicalColumnNames` :34 | `SelectExpressionPlanEqualityComparer.Equals(first, second) == false` | removed |
| H02 | `…Comparer_ShouldDistinguishEntityItemFromScalarColumn` :58-59 | `…Equals(withItem, withoutItem) == false` | removed |
| H03 | `…Comparer_ShouldDistinguishProjectionItemSlots` :84-85 | `…Equals(first, second) == false` | removed |
| H04 | `ImplicitNavigationR3CountBoundaryTests.Wide_count_flag_participates_in_the_plan_identity` :100 | `GetSelectExpressionPlanEqualityComparer().Equals(narrowed, ordinary) == false` | removed |
| H05 | `RawSourceBindingFilterTests.SameSqlAndColumns_DifferentEntityType_NotEqual` :912 (was `…_NotEqualWithDistinctHash`) | `FromExpressionPlanEqualityComparer.Equals(tenant.SourceFrom, opaque.SourceFrom) == false` | removed |
| H06 | `RawSourceBindingFilterTests.SameTypeSameColumnCount_DifferentColumnNames_NotEqual` :930 (was `…_NotEqualWithDistinctHash`) | `…Equals(declared.SourceFrom, swapped.SourceFrom) == false` | removed |
| H07 | `PlanKeyStructureTests.QueryPlan_ShouldCompareByCommandAndSql` :237 | `QueryPlan.Equals(renderedB) == false` | removed |
| H08 | `InMemoryTests.TestQueryPlanCache` :382 | `QueryPlanEqualityComparer.Equals(q1, q2) == false` | removed |

Intent text corrected: the `SelectExpressionPlanEqualityComparerTests` XML doc no longer claims "hash
apart", and H05/H06 lost the `WithDistinctHash` naming; no remaining in-scope assertion claims hash
distinctness.

## Residual hash `NotBe` (out of the R175 H-matrix) — complete census

The predecessor census used the regex `GetHashCode(...).Should().NotBe`, which only matches the
*inline* form and **misses the local-variable form** (`var h = GetHashCode(x); h.Should().NotBe(h2)`).
The corrected census scans every `NotBe` operand in the five files and classifies it:

| # | File:line | Enclosing test method | Live assertion | In H01–H08? |
|---|---|---|---|---|
| 1 | `SelectExpressionPlanEqualityComparerTests.cs:101` | `Comparer_ShouldDistinguishProjectionItemMembers` | `comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));` | no (#173/D190) |
| 2 | `SelectExpressionPlanEqualityComparerTests.cs:130` | `Comparer_ShouldDistinguishNullFromNonNullProjectionItemMember` | `comparer.GetHashCode(withMember).Should().NotBe(comparer.GetHashCode(withoutMember));` | no (#173/D190) |
| 3 | `SelectExpressionPlanEqualityComparerTests.cs:145` | `Comparer_ShouldNotConflateSameMemberNameOnDifferentDeclaringTypes` | `comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));` | no (#173/D190) |
| 4 | `SelectExpressionPlanEqualityComparerTests.cs:157` | `Comparer_ShouldDistinguishProjectionItemEntityTypes` | `comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));` | no (#173/D190) |
| 5 | `SelectExpressionPlanEqualityComparerTests.cs:189` | `Comparer_ShouldDistinguishDirectRootItemSourceExpressions` | `comparer.GetHashCode(fromFirst).Should().NotBe(comparer.GetHashCode(fromSecond));` | no (D190) |
| 6 | `SelectExpressionPlanEqualityComparerTests.cs:205` | `Comparer_ShouldApplyMemberIdentityToValueTypeEntityItems` | `comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));` | no (#173) |
| 7 | `RawSourceBindingFilterTests.cs:974` | `SameTypeSameColumns_DifferentEffectiveMetadataInstance_NotEqualByHash` | `secondHash.Should().NotBe(firstHash, …)` where `firstHash`/`secondHash` are locals (`:970`,`:972`) — **local-variable form the old regex missed** | no (outside H01–H08) |

Total live residual hash-inequality assertions: **7** across the five files — 6 in
`SelectExpressionPlanEqualityComparerTests.cs` (added by `#173`/`D190`, not the H01–H03 plan sites) and
1 in `RawSourceBindingFilterTests.cs:974` (`SameTypeSameColumns_DifferentEffectiveMetadataInstance_NotEqualByHash`).

`RawSourceBindingFilterTests.cs:974` is **OUTSIDE the plan's H01–H08 scope** (H05/H06 are the
`FromExpressionPlanEqualityComparer` `.Equals == false` sites at `:925`/`:943`; this test deliberately
asserts hash-distinctness for two different effective-metadata instances). It is **not** an R175 target,
so its assertion **must NOT be removed**; it is retained unchanged and recorded here explicitly so the
census is complete. R175 does not delete any assertion (git diff is deletions of the 8 H-site hash
`NotBe` lines only).

Also present but **not live assertions**: two commented-out hash `NotBe` lines inside the disabled
`// public void TestQueryCache()` block — `InMemoryTests.cs:365` and `InMemoryTests.cs:378` — both
prefixed `//`, so they execute nothing.

No in-scope H-site retains a hash `NotBe`.

## Retained (unchanged)

- Coherence hash-equality (R175-COH): `SelectExpressionPlanEqualityComparerTests.cs:48`;
  `PlanKeyStructureTests.cs:231,256,274,281,326,327,382`; `RawSourceBindingFilterTests.cs:863-864,1006`;
  `InMemoryTests.cs:401,406`; `JoinIntoPlanKeyTests.cs:63,91,106,135`;
  `JoinIntoSqlGenerationTests.cs:136`.
- Execution/identity (R175-SEM): `JoinIntoExecutionTests.cs:55,76`;
  `JoinIntoSqlGenerationTests.cs:159-162`; `CommonTestSuite.JoinInto.cs:221-222`.
- Collision-is-legal counter-examples retained: `PlanKeyStructureTests.EqualHashes_ShouldNotImplyEquality_ForQueryableConstant`
  and `QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality`.

## Scope / variants

Operands, constructors and scenario bodies unchanged; only the weak second oracle was deleted and two
method names plus one doc comment corrected. `JoinIntoPlanKeyTests.cs`/`JoinIntoExecutionTests.cs`/
`JoinIntoSqlGenerationTests.cs` are unmodified. No new null/default/provider/flag branch was introduced or
removed. All five files remain CRLF.

## Post focused evidence

FC1 12/0/0; FC2 60/0/0; FC3 7/0/0; FC4 19/0/0; FC5 93/0/0; FS1 7/0/0; FS2 9/0/0 — all exit 0. Full core
1969/0/0; full sqlite 1311 (1310+1skip)/0fail; solution sweep 9818/6996/2822/0fail.
