# D221 (#221) mutation disposition — rv=1

Tool: Stryker.NET 5.0.0. Config: `artifacts/pdca/D221/rv1/stryker.json` (mutate `DI/DataContextBuilder.cs`
and `DI/ServiceCollectionExtensions.cs`; test projects `nextorm.core.tests` + `nextorm.sqlite.tests`;
`test-runner: mtp`; reporters Progress/Html/Json/ClearText).

Four runs on the same source:

| Run | Log | Mutants tested | Killed | Survived | Score |
|---|---|---|---|---|---|
| 1 (before added assertions) | `mutation.log`, report archived as `mutation/mutation-report.rerun0.json` | 70 | 53 | 17 | 69.74% |
| 2 (after argument-guard assertions) | `mutation-rerun.log`, report archived as `mutation/mutation-report.rerun1.json` | 70 | 62 | 8 | 81.58% |
| 3 (after covering the previously NoCoverage members) | `mutation-rerun2.log`, report archived as `mutation/mutation-report.rerun2.json` | 76 | 68 | 8 | 89.47% |
| 4 (corrective n=2/3, after V05/V07/no-eager-resolve tests + doc reword) | `mutation-rerun3.log` | 76 | 68 | 8 | 89.47% |

The final run has **0 NoCoverage** mutants in the two target files. The remaining 8 run-4 entries are
dispositioned below. **17/17 run-1 survivors are accounted for**: 9 were killed by the assertions added
for this disposition, 1 is killed by an existing test but mislabeled by Stryker, and 7 are equivalent.

## Killed by the rerun (9 run-1 survivors)

Targeted assertions added to `tests/nextorm.core.tests` (`InterceptorBuilderTests.cs`,
`DependencyInjectionTests.cs`); after the additions U = 28/28 and UN = 34/34 (exit 0), then Stryker
re-run:

| Run-1 id | Location | Added test |
|---|---|---|
| 18456 | `DataContextBuilder.cs:48` internal ctor `ThrowIfNull(serviceProvider)` | `Constructor_WithNullServiceProvider_Throws` |
| 18459 | `DataContextBuilder.cs:131` `UseLoggerFactory` guard | `UseLoggerFactory_WithNull_Throws` |
| 18468 / 18471 | `DataContextBuilder.cs:230` `UseCommandTimeout` conditional/equality | `UseCommandTimeout_WithPositiveValue_IsApplied`, `UseCommandTimeout_WithNonPositiveValue_UsesProviderDefault` |
| 18474 / 18475 | `DataContextBuilder.cs:278,282` `UseEagerLoadMode` messages | `UseEagerLoadMode_WithDefaultSentinel_ThrowsWithMessage`, `UseEagerLoadMode_WithUndefinedValue_ThrowsWithMessage` |
| 18524 / 18528 / 18536 | `ServiceCollectionExtensions.cs:30,46,77` `ThrowIfNull(optionsBuilder)` | `AddNextOrmContext_ProviderOverload_NullArguments_ShouldFailFast`, `AddKeyedNextOrmContext_ProviderOverload_NullArguments_ShouldFailFast`, `AddKeyedNextOrmContext_ActionOverload_NullArguments_ShouldFailFast` |

For `ServiceCollectionExtensions.cs` the argument-guard coverage previously lived only in
`nextorm.integration.tests`, which is outside Stryker's `test-projects`, so the guards had no killer in
the mutation run; the equivalent core-project assertions now pin them.

## Remaining run-2 survivors (classified)

| id | Type | Location | Verdict | Evidence / reasoning |
|---|---|---|---|---|
| 18498 | Logical (`&&` → `||`) | `DI/DataContextBuilder.cs:411-412` (`IsValidInterceptorType`) | **Killed by test (Stryker mislabels)** | The mutation makes `IsValidInterceptorType` return `true` for `typeof(List<>)` (open generic: `!IsValueType` is `true`, so `A || (B && C)` is `true`), so `AddInterceptor(Type)` stops throwing. `InterceptorBuilderTests.AddInterceptor_WithOpenGenericType_Throws` asserts that throw. Empirical proof: temporarily applied the mutation → `dotnet build tests/nextorm.core.tests` = 0 errors → `dotnet run --project tests/nextorm.core.tests -c Debug --no-build -- -method "*AddInterceptor_WithOpenGenericType_Throws"` = **Total: 1, Failed: 1** (exit 1); reverted, rebuilt, U = 28/28 green. The Stryker JSON still labels it `Survived` (per-test coverage mapping gap for this test), but the test set does discriminate it. |
| 18503 | Logical (`&&` → `||`) | `DI/DataContextBuilder.cs:424` (`ResolveInterceptors` cache guard) | **Equivalent** | `_resolvedQueryInterceptors` and `_resolvedConnectionInterceptors` are only ever written together: both assigned at `DataContextBuilder.cs:438-439` and both reset to `null` at `:473-474` (`CreateDataContext`). No code path can leave exactly one non-null, so `_q is not null && _c is not null` and ``_q is not null || _c is not null`` are observationally identical. |
| 18523 | Statement (`;`) | `ServiceCollectionExtensions.cs:29` (`ThrowIfNull(services)`, provider overload) | **Equivalent** | Removing the local guard still fails with `ArgumentNullException("services")` from the downstream Microsoft DI call: `RegisterContextFactory` → `services.AddScoped(...)` (`ServiceCollectionExtensions.cs:122/129`), whose `ServiceCollectionServiceExtensions` validates `services`. Empirical proof: removed the `:29` guard, rebuilt, ran `AddNextOrmContext_ProviderOverload_NullArguments_ShouldFailFast` = **Total: 1, Failed: 0** (exit 0, still `ArgumentNullException`); reverted. |
| 18527 | Statement (`;`) | `ServiceCollectionExtensions.cs:45` (keyed provider overload) | **Equivalent** | Same mechanism via `RegisterContextFactory` → `services.AddKeyedScoped(...)` (`:133/140`), which throws `ArgumentNullException("services")` when `services` is null. |
| 18531 | Statement (`;`) | `ServiceCollectionExtensions.cs:60` (`Action<DataContextBuilder>` overload) | **Equivalent** | Same mechanism via `:63` → `RegisterContextFactory` → `services.AddScoped(...)` (`:122/129`). |
| 18535 | Statement (`;`) | `ServiceCollectionExtensions.cs:76` (keyed `Action<DataContextBuilder>` overload) | **Equivalent** | Same mechanism via `:79` → `services.AddKeyedScoped(...)` (`:133/140`). |
| 18539 | Statement (`;`) | `ServiceCollectionExtensions.cs:92` (`AddNextOrmContext<T>`) | **Equivalent** | Same mechanism via `:94` → `RegisterContextType<T>` → `services.AddScoped<T>()` (`:153`), which throws `ArgumentNullException("services")` when `services` is null. |
| 18542 | Statement (`;`) | `ServiceCollectionExtensions.cs:108` (`AddKeyedNextOrmContext<T>`) | **Equivalent** | Same mechanism via `:110` → `services.AddKeyedScoped<T>(serviceKey)` (`:158`). |

## No-coverage mutants (reported separately)

The first run reported 6 `NoCoverage` mutants on members outside the focused D221 selectors
(`DataContextBuilder.cs:150` `LogSensitiveData`, `:202` `UseUppercaseKeywords`, `:213` `UseMultilineBatchSql`,
`:460` the `_factory is null` throw in `CreateDataContext`). The run-3 additions
(`CreateDataContext_WithoutFactory_Throws`, `LogSensitiveData_TogglesTheFlag`,
`UseUppercaseKeywords_And_UseMultilineBatchSql_ToggleTheFlags`) cover them; the final run has **0
NoCoverage** mutants in the two target files.

## Result

Changed-P1 mutants: `DataContextBuilder` new internal ctor guard (killed), `IsValidInterceptorType`
(killed by test), `AddInterceptor`/`ResolveInterceptors` behavior (killed). The only surviving
changed-surface mutants are the two logical mutants proven equivalent (`18503`) and the six redundant
null-guards proven equivalent (`18523/18527/18531/18535/18539/18542`). No unexplained non-equivalent
survivor remains.

## Deferred / accepted findings (recorded, not implemented)

- **B4 (P2, deferred with trigger)** — `ResolveInterceptors` (`DataContextBuilder.cs:421-431`) allocates two
  `List<>` objects even when `_interceptorEntries` is empty, so a context with no interceptors pays two
  small allocations at construction. This is context-construction work, not per-row work; it is
  **deferred**, trigger: **measured context-construction pressure** (a construction/allocation profile
  showing this path as a cost). Recorded, not optimized.
- **B7 (not-a-defect)** — the compile-time `where T : class` constraint plus the runtime
  `IsValidInterceptorType` check are intentional; the runtime check rejects open-generic and
  non-assignable types immediately, which the XML docs now state (`AddInterceptor<T>()` remark).
- **B1 / B3 (accepted plan assumptions A1/A2)** — same-runtime-type collapse and distinct-alias
  double-invocation are the documented deduplication/alias semantics; behavior unchanged and covered by
  `InterceptorBuilderTests`/`InterceptorDiRegistrationTests` and the EN/RU interceptor docs.
