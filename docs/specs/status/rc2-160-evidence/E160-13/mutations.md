# E160-13 — six reversible mutations (R160-01, R160-04, R160-06, R160-07, R160-11)

- task_id: D160 (issue #160), cycle 1, plan r=2, contract rv=3, attempt n=1/3, phase DO
- tree identity at execution: HEAD `9a4ad52155fba928a8072d120af5c51696a10057`, branch `1.0.9-rc2`
- local check only (not a mutation score): each mutation is applied, a targeted filtered test is run,
  the failure is recorded, the code is restored exactly (`git checkout -- <file>`), and the same
  filtered test is run green again. No new packages, no CPM change.
- `git diff -- src tests` after all restores: **empty** (no residual mutation). Only this evidence dir
  and the D160 status file differ.
- F1 kill (cycle r=2, D:160-03) executed on HEAD `9a4ad521` with the new discriminating tests uncommitted
  in `tests/nextorm.alias.tests/{MixedJoinChainTests,AliasGeneratedSurfaceTests,JoinAliasGeneratorDiagnosticTests}.cs`;
  after the kill the generator mutation was restored exactly (`git diff -- src` empty; `git diff -- src tests`
  shows only those three test files).

## Method / commands (argument arrays)

| id | selected filter command |
|---|---|
| M1 | `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~AliasProjectionShapeTests` |
| M2 | `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~Positional_join_after_alias_binds_the_generated_receiver` |
| M3 | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~Root_alias_fails_closed_on_the_in_memory_provider` |
| M4 | original: `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~Digit_ending_alias_is_a_name_and_does_not_shift_the_slot` (plus full `dotnet test tests/nextorm.alias.tests -c Debug`); F1 kill: `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~Digit_ending_alias` + restored full `dotnet test tests/nextorm.alias.tests -c Debug` |
| M5 | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~Repeated_root_projection_preparation_reuses_the_plan_without_sticky_cache_mutation` |
| M6 | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~Dim1_projection_reports_dimension_one_and_extend_yields_slot_two` |

## Results

| id | mutation | file:line edited | red (exit) | failing test + assertion | restored green (exit) |
|---|---|---|---|---|---|
| M1 | Alias slot K→K+1: generated `[JoinSlot(member.Slot)]` emitted as `member.Slot + 1` | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:936` | exit 2; total 3, failed 2, passed 1 (`m1-red.log`) | `Alias_members_carry_join_slot_attribute_and_resolve_to_the_right_slot` — *Expected projection.GetProperty("Buyer")!.GetCustomAttribute<JoinSlotAttribute>()!.Position to be 2, but found 3* (`AliasProjectionShapeTests.cs:76`); also `Alias_projection_retains_item_members_and_exposes_alias_members` (`:34`) | exit 0; total 3, failed 0, passed 3 (`m1-green.log`) |
| M2 | Positional-after-alias bypasses the `JoinAlias` seam: generated `new` instance positional transition returns a fresh builder instead of routing through `JoinAlias` | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:986-990` | exit 2; total 1, failed 1, passed 0 (`m2-red.log`) | `Positional_join_after_alias_binds_the_generated_receiver` — `BuildSqlCommandException: Table name is not registered for type …AliasProjection_P1_A2_Buyer_P3…` (`AliasProjectionShapeTests.cs:62`) | exit 0; total 1, failed 0, passed 1 (`m2-green.log`) |
| M3 | Remove the root-alias in-memory guard from `AliasRoot` | `src/nextorm.core/Builders/EntityBuilder.cs:3101-3103` | exit 2; total 1, failed 1, passed 0 (`m3-red.log`) | `Root_alias_fails_closed_on_the_in_memory_provider` — *Expected a System.NotSupportedException to be thrown, but no exception was thrown* (`RootProjectionTests.cs:186`) | exit 0; total 1, failed 0, passed 1 (`m3-green.log`) |
| M4 | Mis-parse `Buyer2` as slot number: `BuildSchema` derives the alias `AliasMember` slot from the alias name's trailing digit | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:763-765` | **detected after F1 kill** (D:160-03, cycle r=2, n=1) — exit 2; total 3, failed 2, passed 1 (`f1-m4-red.log`). *Original run (F1) survived*: exit 0, total 1, passed 1 (`m4-red.log`); full alias suite exit 0, total 71, passed 71 (`m4-red-aliassuite.log`) — kept as history | both new discriminating tests: `MixedJoinChainTests.Digit_ending_alias_in_a_non_matching_slot_resolves_to_its_actual_slot` — *Expected `chained.Select(p => p.Buyer2.Id).ToList()` to be equal to {20}, but {10} differs at index 0* (`MixedJoinChainTests.cs:197`); `JoinAliasGeneratorDiagnosticTests.Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot` — generated `[global::NextORM.Core.JoinSlot(2)] … public T2 Buyer2` instead of `[JoinSlot(3)] … public T3 Buyer2` (`JoinAliasGeneratorDiagnosticTests.cs:285`) | exit 0; total 73, failed 0, passed 73 (`f1-alias-suite-restored-green.log`) |
| M5 | Sticky cache flag on the shared command: unconditional `queryCommand.Cache = false;` at the top of `QueryPlanner.GetPreparedQueryCommand` | `src/nextorm.core/DataContext/QueryPlanner.cs:553` (inserted line) | exit 2; total 1, failed 1, passed 0 (`m5-red.log`) | `Repeated_root_projection_preparation_reuses_the_plan_without_sticky_cache_mutation` — *Expected second to refer to …DbPreparedQueryCommand…, but found …* (`RootProjectionTests.cs:211`) | exit 0; total 1, failed 0, passed 1 (`m5-green.log`) |
| M6 | Mis-extend dim-1: `Projection<T1>.Extend` returns the same dim-1 projection (no slot added) | `src/nextorm.core/Builders/Projection.cs:47-51` | exit 2; total 1, failed 1, passed 0 (`m6-red.log`) | `Dim1_projection_reports_dimension_one_and_extend_yields_slot_two` — *Expected type to be Projection<Order,String>, but found Projection<Order>* (`RootProjectionTests.cs:75`) | exit 0; total 1, failed 0, passed 1 (`m6-green.log`) |

Summary: **6/6 mutations detected** (M4 killed by the F1 discriminating tests added in cycle r=2,
D:160-03), all six edits fully reverted, all six restores green, no residual mutation in `src`/`tests`.
`git diff -- src` after the F1 restore is **empty**; `git diff -- src tests` shows only the three
alias-test files changed by the new tests (no production mutation residue).

## Finding F1 — M4 test gap: **KILLED** by the added discriminating tests (cycle r=2, D:160-03)

- Original observation (cycle r=1): M4 survived because `Digit_ending_alias_is_a_name_and_does_not_shift_the_slot`
  (`MixedJoinChainTests.cs`, `Buyer2` in slot 2) has the alias name's trailing digit (`2`) coinciding with its
  positional slot (`2`), so a trailing-digit slot parse produced the same `AliasMember` slot as the correct
  positional computation. The full alias suite (71/71) also stayed green.
- Fix (D:160-03, R160-01): two discriminating negative tests place `Buyer2` in a slot whose number **differs**
  from the trailing digit (`Buyer2` in slot 3, behind a slot-2 positional join `P1_P2_A3_Buyer2`):
  1. `MixedJoinChainTests.Digit_ending_alias_in_a_non_matching_slot_resolves_to_its_actual_slot`
     (`tests/nextorm.alias.tests/MixedJoinChainTests.cs:197`) — runtime: the slot-2 positional join resolves to
     Buyer (10) and the slot-3 alias join to Approver (20); `p.Buyer2` must be `{20}` and equal `p.Item3`, not
     `p.Item2`. Under M4 it binds to t2 and returns `{10}` — red.
  2. `JoinAliasGeneratorDiagnosticTests.Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot`
     (`tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs:285`) — generator harness: asserts the
     emitted `[JoinSlot(3)] … public T3 Buyer2`; under M4 the generated projection carries
     `[JoinSlot(2)] … public T2 Buyer2` — red.
  The frozen generated-surface baseline (`AliasGeneratedSurfaceTests`) was extended with the two new
  `AliasJoin/AliasProjection_P1_P2_A3_Buyer2`3` names.
- Kill proof: mutation re-applied (`f1-m4-mutation.diff`), filtered run `FullyQualifiedName~Digit_ending_alias`
  exit 2 / total 3 / failed 2 / passed 1 (`f1-m4-red.log`); the two new tests fail, the original slot-2
  `Digit_ending_alias_is_a_name_and_does_not_shift_the_slot` still passes. Restored exactly (`git checkout --`
  the generator) and full alias suite exit 0 / total 73 / failed 0 (`f1-alias-suite-restored-green.log`).
- No production defect was implied; the fix is purely a test-quality closure of F1 and it is now KILLED.

## Logs
- `m1-baseline.log`, `m1-mutation.diff`, `m1-red.log`, `m1-green.log`
- `m2-mutation.diff`, `m2-red.log`, `m2-green.log`
- `m3-mutation.diff`, `m3-red.log`, `m3-green.log`
- `m4-mutation.diff`, `m4-red.log`, `m4-red-aliassuite.log`, `m4-green.log` (original F1-surviving run)
- `m5-mutation.diff`, `m5-red.log`, `m5-green.log`
- `m6-mutation.diff`, `m6-red.log`, `m6-green.log`
- F1 kill: `f1-m4-mutation.diff`, `f1-m4-red.log`, `f1-new-tests-green.log`,
  `f1-alias-suite-green.log`, `f1-alias-suite-restored-green.log`
