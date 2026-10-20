# E41 — adopt the prepared description by reference under COW (r=7)

## What changed

`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` — `ApplyPreparationFrom(QueryCommand clone)`
(body replaced; the signature/doc comment stay).

1. **Description adoption by reference**
   - `if (clone._description is { } description) { description.MarkShared(); _description = description; }`
   - `else { _description = null; }`
   - `_definitionMemo = null;` (the memo is node-owned; the old path cleared it inside `WriteDescription`).
2. **Non-description state copied element-wise, in `CopyTo` order, via the same accessors/properties**
   - preparation-backed: `_groupingList`, `ColumnsPlanHash`, `JoinPlanHash`, `WherePlanHash`,
     `SortingPlanHash`, `PreparedCondition`, `PreparedHaving`, `InValuesShapeHash`, `LookupPartitions`,
     `ShapeScanned`, `HasUnkeyedTupleInValues`, `HasUnkeyedScalarInValues`, `ResultPlanHash`,
     `GroupingPlanHash`, `FromPlanHash`, `UnionPlanHash`, `ReferencedQueriesPlanHash`, `CtesPlanHash`,
     `HintsPlanHash`, `WindowsPlanHash`, `ResolvedQuoteIdentifiers`, `ResolvedNamingConvention`,
     `ResolvedKeywordCase`, `ResolvedCommandTimeout`, `ResolvedEagerLoadMode`, `ResultType`,
     `DefaultOnEmpty`, `SingleScalar`, `_limitByColumns`, `_distinctOnColumns`, `_extremeRowColumns`,
     `_extremeRowGroupByColumns`, `_perParentPartitionColumns`, `_preparedArrayJoin`, `_whereBasePlanHash`,
     `PreparedPreWhere`, `PreWhereShapeHash`, `HasPreWhereInValues`, and the six comparers.
   - plain/protected/binding: `_selectList`, `_isPrepared`, `_srcType`, `_dontCache`, `Paging`,
     `_preWhere`, `_arrayJoins`, `_customData`, `_from`.
   - **Not** reassigned: `_referencedQueries`, `_union`, `_ctes` (the adopted description carries them).
   - **Not** adopted: the clone's `_preparation` object (post-adopt writes must not mutate the recipe).
   - **Not** copied: description-backed members (`_filterScope`, `_joins`, `_hints`, `_windows`,
     options/clauses/flags, `OneColumn`, `_outerRefs`, `OuterRegistry`, ...) — they already carry the
     clone's values through the adopted description.
3. **COW guarantee** — `MarkShared()` marks the recipe description and every block shared, so any later
   write on either side goes through `WriteDescription()`/`Write*` and splits (shell first, then block).
   The only direct `_description` writes in the method are the two field assignments (adopt / null).

## Files

- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`
- `tests/nextorm.core.tests/Caching/CachedPreparationRecipeTests.cs`
  - `ApplyPreparationFrom_AdoptedDescription_ParityWithCopyTo`
  - `ApplyPreparationFrom_AdoptedDescription_IsolatesRecipeFromPostAdoptWrites`
  - private helpers: old-path runner (reflection over protected `CopyTo` + the four legacy members),
    description/state snapshotters.

## Reflection parity (same command, new path vs old path)

`ApplyPreparationFrom_AdoptedDescription_ParityWithCopyTo` compares **all 18** declared `QueryCommand`
instance fields (`_dataContext`, `_customData`, `Paging`, `_selectList`, `_isPrepared`, `_srcType`,
`_exp`, `_condition`, `_groupExp`, `_having`, `_preWhere`, `_arrayJoins`, `_sorting`, `_from`,
`_definitionMemo`, `_description`, `_preparation`, `_binding`):
reference equality for the 14 shared/plain members, `_definitionMemo == null` on both, deep
description contents equal (root `ProjectionType`, `Flags`, all four blocks; only `Shared` ignored),
`_preparation` field-by-field equal (comparers by reference), `_binding` field-by-field equal.
**Result: PASS** (see `parity.txt`).

## COW isolation

`ApplyPreparationFrom_AdoptedDescription_IsolatesRecipeFromPostAdoptWrites`:
apply the recipe to a fresh command, then write `Tag`, `CommandTimeout`, `IsDistinct`, `LimitBy`,
`Paging` on the adopter; the recipe's description/preparation/binding snapshot is byte-identical before
and after. Reading `Definition` on the adopter (node-owned memo) also leaves the recipe untouched.
A second fresh command applying the same recipe renders SQL identical to ordinary preparation
(`storeInCache: false`, fresh context). **Result: PASS.**

Red arm (recorded, guard temporarily removed): `e41-red-isolation.log` — the isolation test **fails**
(exit 2, 1 failed / 1 passed; parity stays green because it deliberately ignores the `Shared` flags).
The green run after restoring the guard is `e41-core-focused.log`.

## Roslyn reference check (no direct description writer introduced)

`roslyn refs NextORM.Core.QueryCommand._description`:
- `QueryCommand.QueryPreparer.cs:4380` read `clone._description`
- `QueryCommand.QueryPreparer.cs:4383` `_description = description`
- `QueryCommand.QueryPreparer.cs:4387` `_description = null`

`roslyn refs NextORM.Core.QueryDescription.MarkShared`: `QueryCommand.QueryPreparer.cs:4382` plus the
pre-existing `Description.cs:275` (`CloneShellForWrite`) and `QueryCommand.cs:198` (live clone edge).
No `_description.<member> =` write exists in `QueryPreparer.cs`.

## Verification

| command | exit | total / passed / failed / skipped | log |
| --- | --- | --- | --- |
| `MSBUILDDISABLENODEREUSE=1 dotnet build nextorm.slnx -c Debug` | 0 | 0 warnings / 0 errors | `.pdca/T297/rv7/logs/e41-build.log` |
| `dotnet test tests/nextorm.core.tests --no-build --filter "FullyQualifiedName~CteDescriptionCowTests\|FullyQualifiedName~CachedPreparationRecipeTests"` | 0 | 35 / 35 / 0 / 0 | `e41-core-focused.log` |
| `dotnet test tests/nextorm.core.tests --no-build` | 0 | 2660 / 2660 / 0 / 0 | `e41-core-full.log` |
| `dotnet test tests/nextorm.publicextensibility.tests --no-build --filter "FullyQualifiedName~T297ProtectedFieldCompatibilityTests"` | 0 | 9 / 9 / 0 / 0 | `e41-e19-focused.log` |

Gate: `python3 .pdca/scripts/validate_inner_loop.py brief .pdca/T297/rv7/logs/test-scope.json` → rc 0
(`.pdca/T297/rv7/logs/test-scope-e41.log` — see commit payload; gate was run before edits).

Out of footprint untouched: `QueryCommand.Clone.cs`, `QueryPlan*.cs`, `DataContext/QueryPlanner.cs`,
E19, public API.
