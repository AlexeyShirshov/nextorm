# E03 — variant matrix + core regressions (C3, refreshed DO r1 n2), issue #166

## Class filter — `Iteration14CteLookupTests`

Logged command (memory caps included):

```
$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~Iteration14CteLookupTests"
```

- exit code = **0**; selected = **12**, passed = **12**, failed = **0**, skipped = **0**
- log: `docs/specs/status/nested-read-cte-1-evidence/C3-class.log`

The 7 nested-read tests plus the 5 pre-existing class tests all pass. New in DO r1 n2 (closing the
CHECK-open variant rows):

| test | closes |
| --- | --- |
| `NestedReadCte_AcyclicSharedBody_ClassifiesAndReusesWarmPlan` | D4(a): acyclic shared/diamond body — same `CteDefinition` instance via two outer bodies; classification false, warm plan identity stable, warm getter 0 bytes |
| `NestedReadCte_PreparedClone_KeepsClassificationAndWarmAlloc` | D4(b): `Clone()` and `CloneForCache()` of a prepared nested-read keep `IsPrepared`/read-only classification, warm getter 0 bytes |
| `NestedMutation_PreparedClone_KeepsClassification` | D4(b): `Clone()`/`CloneForCache()` of a prepared nested mutation stay `true` |
| `NestedReadCte_PreparedWarmPolicy_AllocatesZeroAndSharesPlan` | D4(c) **guard**: renamed from the misleading `NestedReadCte_SyncAsyncPolicyMatches`; pins only the provider-free prepared policy/allocation/plan identity (see E04-behavior.md for the DO→PLAN candidate) |

Full matrix (read/mutation × flat/nested × cold/prepared × acyclic; cyclic from the pre-existing
cold tests; shared/non-shared; recursive/non-recursive): all rows asserted false/true as expected,
including "a nested mutation must not become reusable merely because its command is prepared".

## Full `tests/nextorm.core.tests` (no filter)

```
$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.core.tests -c Debug
```

- exit code = **0**; total = **1524**, failed = **0**, succeeded = **1524**, skipped = **0**
- log: `docs/specs/status/nested-read-cte-1-evidence/C3-full-core.log`

No wider core regression.

## Sync/async terminal row — closed as `guard` (DO→PLAN candidate)

The core test project cannot execute a nested-read CTE terminal without a DB/provider:

- `InMemoryDataContext` + seeded data, real `outer.ToList()` → `NullReferenceException` inside the
  in-memory materializer (no CTE execution path): `E04-terminal-probe.log` (exit 2, 1 failed).
- `DmlCteTestContext` has no DbDataReader, so a terminal can only reach the fake boundary.

Both terminal families converge on the single `HasDataModifyingCte` read at
`src/nextorm.core/DataContext/QueryPlanner.cs:559` (E01-paths.md); the convergence is a static
call-path fact, not observable at the terminal level in a provider-free core test. Returned to
planner as a DO→PLAN candidate instead of keeping a proxy named `SyncAsync` (method renamed).

## D6.2 (rv=2) — variant rows (a)/(b) already closed by the D4 tests

Verified read-only against the current tree; **no new tests added** (no genuine assertion gap):

| row | test | file:line | fail-capable assertion |
| --- | --- | --- | --- |
| (a) diamond / shared body | `NestedReadCte_AcyclicSharedBody_ClassifiesAndReusesWarmPlan` | `tests/nextorm.core.tests/Iteration14CteLookupTests.cs:275` | cold `HasDataModifyingCte` false; warm `GetPreparedQueryCommand` twice returns same reference (`:299`); prepared classification still false (`:300`); warm getter 0 bytes over 10 000 iters (`:308`) |
| (b) prepared clone | `NestedReadCte_PreparedClone_KeepsClassificationAndWarmAlloc` | `tests/nextorm.core.tests/Iteration14CteLookupTests.cs:313` | `Clone()` and `CloneForCache()` each keep `IsPrepared` true + read-only false + 0-byte warm getter via `AssertPreparedCloneReadOnlyAndAllocFree` (`:341-354`) |
| (b) prepared clone (mutation) | `NestedMutation_PreparedClone_KeepsClassification` | `tests/nextorm.core.tests/Iteration14CteLookupTests.cs:329` | `Clone()`/`CloneForCache()` of a prepared nested mutation must stay `HasDataModifyingCte == true` (`:337-338`) |

Each assertion is value/identity-based and fails on a reverted F1 or a clone that drops `IsPrepared`
/ the hoisted `_ctes`; none compares an object to itself. The planned duplicates
`NestedReadCte_DiamondSharedBody_PreservesState` / `NestedReadCte_PreparedClone_PreservesState` were
therefore **not** added.

## D6.1/SA-READ + D6.3/SA-MUTATION (rv=2) — executed terminals & mutation classification

Exact invocations (memory caps applied), sequential, current tree:

```
$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NestedReadCte_SyncTerminal_Executes"
$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NestedReadCte_AsyncTerminal_Executes"
$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NestedReadCte_SyncTerminal_Executes|FullyQualifiedName~NestedReadCte_AsyncTerminal_Executes"
$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~Iteration14CteLookupTests"
$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~DataModifyingCtePlanCacheTests"
```

| invocation | exit | selected | passed | skipped | log |
| --- | --- | --- | --- | --- | --- |
| sqlite sync `NestedReadCte_SyncTerminal_Executes` | 0 | 1 | 1 | 0 | `SA-READ-sync.log` |
| sqlite async `NestedReadCte_AsyncTerminal_Executes` | 0 | 1 | 1 | 0 | `SA-READ-async.log` |
| sqlite combined (sync|async) | 0 | 2 | 2 | 0 | `SA-READ-combined.log` |
| core `Iteration14CteLookupTests` | 0 | 12 | 12 | 0 | `E03-class.log` |
| postgres `DataModifyingCtePlanCacheTests` | 0 | 4 | 4 | 0 | `SA-MUTATION-classification.log` |

Both SA-READ tests build the nested read CTE (outer read CTE whose body declares the inner read CTE)
and execute the SQLite terminal (`ToList()` sync with a second warm `ToList()`; `ToListAsync()`
async), asserting the independently specified fixture rows `{1,2,3}` and `Total(Id==2)==20`. The
postgres nested-mutation test `DataModifyingCte_NestedInReadCteBody_ShouldStillBypassCache`
(`tests/nextorm.postgres.tests/DataModifyingCtePlanCacheTests.cs:62`) proves the hoisted DML CTE still
keeps `ReferenceEquals(first, second) == false`.

SQLite mutation is a deliberate **unsupported-provider guard, not executed**: `SupportsDataModifyingCtes`
defaults to `false` in `SqlDialectBase` (`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:973`)
and is overridden `true` only by `PostgresDialect` (`src/nextorm.postgres/PostgresDialect.cs:34`); no
SQLite mutation terminal was built or run.
