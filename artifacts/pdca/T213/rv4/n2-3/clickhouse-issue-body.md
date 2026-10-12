# Flaky ClickHouse test: `ClickHouseExtremeRowNativeSpecificTests.FloatNativeParity_GroupedSingleFloat64_MinAndMax_AcrossDatasets`

## Summary

`NextORM.Integration.Tests.ClickHouseExtremeRowNativeSpecificTests.FloatNativeParity_GroupedSingleFloat64_MinAndMax_AcrossDatasets`
(issue #150 fixture, unchanged since base `20a67a1c`) is flaky. Three distinct symptoms were observed
against the same ClickHouse 25.8 container; none is attributable to a product change.

## Symptoms

1. **Isolated single-test run → `UNKNOWN_TABLE`**
   `dotnet test tests/nextorm.integration.tests --filter ...FloatNativeParity_GroupedSingleFloat64...`
   fails with:
   `DB::Exception: Unknown table expression identifier 'extreme_float_150' ... (UNKNOWN_TABLE)`.
   Root cause: `ProviderTestSuite` seeds lazily on the first `_sut` access
   (`ProviderTestSuite.CreateRepository` → `Provider.EnsureSeeded()`), but
   `AssertGroupedParity` builds the **portable oracle first** (`Portable()`, line ~630) and only then
   touches `_sut` (native, line ~634). With no earlier test in the process the fixture table was never
   created.
2. **Full-suite run → `QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`**
   `DB::Exception: Query with id = ... is already running. (QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING) (Code 216)`.
   Concurrent execution (shared ClickHouse server, same generated query id) is rejected by the server.
3. **Another isolated run → group-keys mismatch**
   `Expected nativeByGroup.Keys to be a collection with 0 item(s) because finite must expose the same groups, but {20, 10} contains 2 item(s)`
   — consistent with the shared fixture table being dropped/recreated/seeded while a reader is querying it.

## Evidence

- Whole class at HEAD passes: `ClickHouseExtremeRowNativeSpecificTests` 35/35, exit 0
  (`artifacts/pdca/T213/rv4/n2-3/ch-head-class.log`).
- Isolated method at HEAD fails `UNKNOWN_TABLE`, exit 2
  (`artifacts/pdca/T213/rv4/n2-3/ch-head-isolation.log`).
- Full integration run failed with Code 216 on this exact test
  (`artifacts/pdca/T213/rv4/check2/integration-full.log:331-347`, exit 1;
  the only failure of Total 3743, Errors 0, Failed 1).
- Earlier isolation attempts: `artifacts/pdca/T213/rv4/check2/ch-flaky-isolation.log`,
  `ch-flaky-isolation2.log`.

## Why this is not a product regression

- `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs`,
  `Providers/ClickHouseTestProvider.cs` and `ProviderTestSuite.cs` are byte-identical between base
  `20a67a1c` and HEAD (`git diff --stat 20a67a1c HEAD -- <those paths>` is empty).
- The failing statement is the portable/native extreme-row query; the failures are server-side
  isolation/concurrency errors (`UNKNOWN_TABLE`, Code 216, partial data), not query-shape or type-mapping
  errors.
- The whole class passes reproducibly at HEAD (35/35).

## Suggested fix

- Make `AssertGroupedParity`/`AssertGlobalParity` touch `_sut` (or `Provider.EnsureSeeded()`) before the
  portable oracle so the fixture exists regardless of test ordering.
- Serialise ClickHouse tests that mutate the shared fixture tables (or use per-test tables/databases) to
  remove the Code 216 / partial-data races; consider disabling shared query ids in the driver if supported.
