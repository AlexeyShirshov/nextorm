# Defect: `SqlFunctions.Parameter<T>` in a native-eligible JSON projection renders unaliased

- Unit: `R177-D177-R3` (cycle N=1, plan revision r=2, evidence contract rv=2, clarification; no r bump)
- Product baseline: `2ac20818bc5bdb429406f99758b1db50a470561e` (`#177 D177 native JSON fast-path`)
- Failure artifact (immutable): `docs/specs/status/rc2-177-reclaim-evidence/findings/native-parameter-projection-defect.R3-failing.log`
- Original log: `docs/specs/status/rc2-177-reclaim-evidence/logs/sqlserver-nativejsonstream.log`

## Description

A `SqlFunctions.Parameter<T>(idx)` used as a **projected column** (`Select(x => new { x.Id,
Bound = SqlFunctions.Parameter<string>(0) })`) is classified as a native direct pass-through by the
native eligibility guard, but the translator renders the placeholder (`@p0`) into the `SELECT` list
**without an alias**. SQL Server rejects an unaliased column under `FOR JSON PATH`
(`FOR JSON` requires every column/table to carry a name or alias), so the native route fails at
execution instead of falling back to the managed row writer.

The projection is admitted by native eligibility because:

- `JsonShapePlan.IsDirectProjection` (`src/nextorm.core/Query/Json/JsonShapePlan.cs:348-367`) treats a
  `MethodCallExpression` whose declaring type is neither `TableAlias` nor `TableColumn` as a direct
  pass-through — `SqlFunctions.Parameter<T>` is such a call.
- `JsonNativeStream` (`src/nextorm.core/Query/Json/JsonNativeStream.cs:65-94`) admits the string/int
  column as a simple alias + direct pass-through.
- `NormSqlTranslator` (`src/nextorm.core/Visitors/NormSqlTranslator.cs:71-80`) emits `Dialect.MakeParam`
  for the placeholder, so the select item is a bare `@pN`.
- `SqlBuilder` (`src/nextorm.core/DataContext/SqlBuilder.cs:505-518`) aliases a projected column only
  when `NeedAliasForColumn` is true; `SqlSourceRenderer.MakeColumn`
  (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:1105-1138`) does not set it for a bare parameter
  (no `ColumnName`, no property-vs-column mismatch), so no alias is emitted.
- `SqlFunctions.Parameter<T>` is declared at `src/nextorm.core/Query/SqlFunctions.cs:73-80`.
- Managed contrast: `QueryExecutor.WriteJson` (`src/nextorm.core/DataContext/QueryExecutor.cs:1162-1198`)
  serializes reader columns positionally and never needs the column alias, so the same projection works
  on the managed fallback.

## Observable failures (R3 log, 16 total / 3 failed)

1. `NextORM.Integration.Tests.SqlServerNativeJsonStreamTests.NativeParams_QueryCommandSurface_ShouldBindChangedValuesSyncAndAsync`
   - failing at `tests/nextorm.integration.tests/SqlServerNativeJsonStreamTests.cs:466`
     (`command.WriteJson(syncOrder, ...)`), projection `First/Second = SqlFunctions.Parameter<string>`
     at `:462`.
   - SQL Server error: `Column expressions and data sources without names or aliases cannot be
     formatted as JSON text using FOR JSON clause. Add alias to the unnamed column or table.`
   - The native-route assertion at `:463` (`IsNativeRoute(...).Should().BeTrue()`) passed before the
     SQL error: the failure stack starts at `WriteJsonNative` (`QueryExecutor.cs:1257`).

2. `NextORM.Integration.Tests.SqlServerNativeJsonStreamTests.Native_RepeatedCalls_ChangedParams_ShouldIsolateStateAndNotLeakCachePolicy`
   - failing at `tests/nextorm.integration.tests/SqlServerNativeJsonStreamTests.cs:381`
     (`command.WriteJson(first, ...)`), projection `Bound = SqlFunctions.Parameter<string>(0)` at `:375`.
   - Same SQL Server `FOR JSON` alias error; the native-route assertion at `:377-378` passed first.

3. `NextORM.Integration.Tests.SqlServerNativeJsonStreamTests.NativeParams_EntityBuilderSurface_ShouldBindChangedValuesSyncAndAsync`
   - failing the slash assertion at `tests/nextorm.integration.tests/SqlServerNativeJsonStreamTests.cs:516`:
     `Expected twoText "[{\"Id\":2,\"Name\":\"é中😀<b>\\/slash\",\"Flag\":false,\"Num\":20}]" to contain
     "é中😀<b>/slash".`
   - SQL Server `FOR JSON` escapes `/` as `\/`; the raw-text assertion expected the unescaped `/`. This
     test has no projected parameter (whole-entity projection) — it is a test-side assertion bug only.

## Invocation and result

```
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- \
  -class NextORM.Integration.Tests.SqlServerNativeJsonStreamTests
```

- exit_code=1
- `nextorm.integration.tests  Total: 16, Errors: 0, Failed: 3, Skipped: 0, Not Run: 0, Time: 13.293s`
- SQL Server actually ran (Testcontainers reuse of container `4ab2d4542432`, `Ready` at log line 19).

## R3 correction (tests only, no `src/**` change)

Per the R177 DO R3 clarification the defect is a **product** gap (projected `SqlFunctions.Parameter<T>`
renders unaliased); the tests are re-scoped to the supported native shape (parameters bound through
`WHERE` and the `WriteJson` params overloads), `TEMP_DumpNativeSql` is removed, and the slash assertion
is corrected. The product gap itself is out of scope for this dispatch.
