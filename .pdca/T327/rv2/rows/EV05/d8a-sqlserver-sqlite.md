# EV05 - SQL Server Unicode/long/NUL and SQLite NUL native limits

- SQL Server (10/10): Cyrillic, CJK, emoji, NUL-bearing and the 4096-character value are exact through
  inline and captured projection and through INSERT/UPDATE into the task-owned `nvarchar(max)` probe
  table; NUL storage/readback exact.
- SQLite (10/10): NUL storage/readback is exact (`CAST(X'00' AS TEXT)`); native LIKE truncates at the
  first NUL - measured (pattern `"a"` matches a NUL-bearing `"a..b"` value) and asserted as the native
  limitation, not emulated. Unicode/emoji and the 4096-character value exact.

## D8b full fail-closed boundary (r=3, single run)

One canonical full-suite run, serialized under the collection container lease, with no `NEXTORM_SKIP_PROVIDERS`: Total 5352, Failed 7, Skipped 197, Not Run 0, exit 1; skip budget 197/197 (delta 0). Log `.pdca/T327/rv2/logs/D8b-full-boundary.log`, details in `.pdca/T327/rv2/rows/EV09/d8b-boundary.json`. The boundary is red: 2 product regressions (DEF-T327-03, DEF-T327-04), 4 stale expectations under the declared representation (DEF-T327-06), 1 pre-existing unrelated SQLite failure (DEF-T327-05).

Two SQL Server failures are stale expectations of the declared `N'...'` literal spelling: `CrossProviderScalarFunctions_NumericAndLength_ShouldCompute` (bit_length 24 -> 48, `CommonTestSuite/Functions/CommonTestSuite.CrossProviderScalarFunctions.cs:76`) and `SqlServerExtendedFunctionTests.CompressionAndTimeZoneFunctions_ShouldExecute` (`SqlServer/SqlServerExtendedFunctionTests.cs:58`, `compress(N'abc')` round-trips UTF-16 bytes). The D8a Unicode/NUL facts are unaffected.
