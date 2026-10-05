# E04 — SQL / parameters / plan-key / clone / shared-body behavior (refreshed DO r1 n2), issue #166

Product behavior (SQL, parameters, plan identity, shared-command state) was unchanged by F1; DO r1 n2
adds test coverage for the CHECK-open rows and fixes the parameter assertion. Product code changed only
in an XML-doc remark since the green run (no behavior change).

## D4(a) — acyclic shared/diamond body

`NestedReadCte_AcyclicSharedBody_ClassifiesAndReusesWarmPlan` builds two outer read CTEs whose bodies
both carry the **same** `CteDefinition` instance (the shared inner), with no cycle:

- `root.HasDataModifyingCte == false` (cold and after prepare).
- two `GetPreparedQueryCommand(root, ...)` calls return the same warm plan instance.
- 10,000 warm getter reads allocate **0 bytes**.

## D4(b) — prepared `Clone()` / `CloneForCache()`

Resolved API via roslyn (`QueryCommand.Clone` `QueryCommand.Clone.cs:211`, `CloneForCache` `:203`,
`CopyTo` copies `_isPrepared` at `:22`).

- `NestedReadCte_PreparedClone_KeepsClassificationAndWarmAlloc`: both clone APIs keep `IsPrepared`,
  return `HasDataModifyingCte == false`, and 10,000 warm getter reads allocate **0 bytes**.
- `NestedMutation_PreparedClone_KeepsClassification`: both clone APIs of a prepared nested mutation
  return `HasDataModifyingCte == true` (a nested mutation must not become reusable via a clone).

## D4(c) — real sync/async terminals: closed as `guard`, not `test`

- Probe: real `outer.ToList()` / `ToListAsync()` on a nested-read CTE over `InMemoryDataContext` fails
  in the in-memory materializer (`NullReferenceException`; no CTE execution path) —
  `E04-terminal-probe.log`, exit 2, 1 failed.
- The provider-free `DmlCteTestContext` has a throwing fake reader, so a terminal cannot observe result
  behavior. Both terminal families statically converge on the single `HasDataModifyingCte` read at
  `src/nextorm.core/DataContext/QueryPlanner.cs:559`, a guard closure over the call-local
  `storeInCache`; that convergence is a call-path fact (E01-paths.md), not a terminal-observable one.
- The proxy `NestedReadCte_SyncAsyncPolicyMatches` was **renamed**
  `NestedReadCte_PreparedWarmPolicy_AllocatesZeroAndSharesPlan` to state exactly what it proves; the
  sync/async terminal row is returned to planner as a **DO→PLAN candidate** (not faked).

## D5 — parameter assertion gap

`NestedReadCte_PreservesSqlParametersAndPlanKey` no longer compares the warm instance against itself.
It now prepares a structurally identical nested-read CTE through a separate instance (storeInCache:
false) as an **independent expected snapshot** and asserts parameter name/value/CLR-type/order against
it. Red→green proof that the snapshot can fail: perturbing the expected threshold by +1 fails with
`Expected parameters.Select(p => p.Value) to be equal to {8} ... but {7} differs at index 0`
(`E04-assertion-red.log`, exit 2); with the snapshot restored the class is green. The
`GetType() == typeof(FakeParameter)` provider coupling was dropped; the value-CLR-type comparison is
provider-independent.

- Declaration order (`read_inner` before `read_outer`), SQL containment and warm plan identity remain
  asserted (E03-class.log).

## Variant matrix

`CteReuse_VariantMatrix` closes read/mutation × flat/nested × cold/prepared: read=false (4 rows) and
mutation=true (4 rows). D4(a)/D4(b) add the shared-body and clone axes; D4(c) is the guard row.
