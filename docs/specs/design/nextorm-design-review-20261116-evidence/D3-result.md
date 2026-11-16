# D3 implementation result

## Scope

- Consolidated the eight case-insensitive interval units in internal `ClickHouseIntervalUnits`, consumed by the core `ClickHouseNativeFunctionTranslator` and provider `ClickHouseScalarFunctions` (`ClickHouseScalars` is the test-field alias).
- Preserved literal-only translator validation, both existing error messages, direct-render null behavior, dialect argument-count short-circuit, and 3/4-argument rendering.
- Expanded unit tests across all units and lower/upper/mixed casing, both entry paths and render forms, invalid/sub-second/whitespace/null inputs, and captured-unit rejection. No public API or project configuration changed.

## Verification

All commands ran in DO; complete command output is in the listed logs.

| Command argument array | Exit | Result | Log |
|---|---:|---|---|
| `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug","--filter","FullyQualifiedName~T286bF04DateTimeFunctionSqlGenerationTests"]` (pre-extraction baseline) | 0 | 15 passed, 0 failed, 0 skipped | `D3-baseline-focused-tests.log` |
| `["dotnet","build","src/nextorm.core/nextorm.core.csproj","-c","Debug"]` | 0 | 0 warnings, 0 errors | `D3-core-build.log` |
| `["dotnet","build","src/nextorm.clickhouse/nextorm.clickhouse.csproj","-c","Debug"]` | 0 | 0 warnings, 0 errors | `D3-clickhouse-build.log` |
| `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug","--filter","FullyQualifiedName~T286bF04DateTimeFunctionSqlGenerationTests"]` | 0 | 15 passed, 0 failed, 0 skipped | `D3-focused-clickhouse-tests.log` |
| `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug"]` | 0 | 1538 passed, 0 failed, 0 skipped | `D3-clickhouse-tests.log` |
| `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` | 0 | 4328 passed, 0 failed, 0 skipped | `D3-core-tests.log` |
| `["dotnet","build","nextorm.slnx","-c","Debug"]` (first attempt) | unavailable | Canceled when the server restarted; no exit status was emitted. Partial output and cancellation record retained. | `D3-final-solution-build-canceled.log` |
| `["dotnet","build","nextorm.slnx","-c","Debug"]` (retry after cancellation) | 1 | 0 warnings, 202 errors. The benchmark's generated row-materializer source cannot resolve `NextORM.Core.IRowMaterializerProvider` / `RowMaterializerColumn`, matching the pre-existing D1 build diagnosis. Not changed or waived in D3. | `D3-final-solution-build.log` |
