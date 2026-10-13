# D221 (#221) coverage delta — baseline → after (rv=1, corrective n=2/3)

Focused-selector coverage from the frozen `coverage-driver.sh`, plus (corrective) a separated run of the
added selectors only (`coverage-driver-added.sh`) so the added-test contribution is separable from the
comparable existing-selector coverage. These focused numbers are **not** a full-suite threshold claim.

| Phase (selectors) | Line | Branch | DataContextBuilder | ServiceCollectionExtensions |
|---|---|---|---|---|
| baseline (existing: U/Q/I-existing) | 9.4% (5496/58393) | 6.5% (2267/34477) | 63.8% (95/149) | 80.0% (80/100) |
| after — existing only | 9.5% | 6.6% (2300/34507) | 90.2% (240/266) | 92.0% (92/100) |
| after — added selectors only (`Category=T221`) | 9.5% | 6.6% (2308/34507) | 83.1% (221/266) | 84.0% (84/100) |
| after — combined (frozen driver `after`) | 11.4% (6669/58450) | 7.8% | 91.7% (244/266) | 100% (100/100) |

- Comparable existing selectors do **not** regress: every existing-selector suite is green
  (`core-u1.log`, `core-u2.log`, `sqlite-q.log`, `integration-contract.log`) and the class coverage for
  both changed types increased.
- The two changed types are fully accounted for: `ServiceCollectionExtensions` reaches 100% line coverage;
  `DataContextBuilder` reaches 91.7%.

## Changed-branch / changed-line disposition

All lines changed by D221 are covered by the combined run:

| Changed region (`src/nextorm.core/DI/`) | Covered by |
|---|---|
| `DataContextBuilder` internal provider ctor + `AddInterceptor(Type)`/`<T>()` + `AddEntry` + `IsValidInterceptorType` + `ResolveInterceptors` + `ResolveType` + `CreateDataContext` | core `InterceptorBuilderTests`/`InterceptorDiRegistrationTests` (U/UN), sqlite `InterceptorDiRegistrationTests` (QN), integration `CommonTestSuite.InterceptorDiTests` (I) |
| `ServiceCollectionExtensions.RegisterContextFactory` (both `new DataContextBuilder(sp)`) | U/UN (core DI tests) + I (provider DI tests) |

No changed line is uncovered. The only uncovered `DataContextBuilder` lines in the combined run are
pre-existing, unrelated members: `UseQueryCache` (244-247), `UseEagerLoadMode` Auto/Default branches
(273-274, 284), `UseCacheSlidingExpiration` (296-299). `ServiceCollectionExtensions` has 0 uncovered lines.

- Project thresholds (85% line / 75% branch) remain configured as a hard gate on `main` and warn-only on
  branch `1.0.9`.

Evidence:
- `TestResults/D221/rv1/coverage-baseline.cobertura.xml` + `coverage-baseline/Summary.txt`
- `TestResults/D221/rv1/coverage-existing-after.cobertura.xml` + `coverage-existing-after/Summary.txt`
- `TestResults/D221/rv1/coverage-added.cobertura.xml` + `coverage-added/Summary.txt`
- `TestResults/D221/rv1/coverage-after.cobertura.xml` + `coverage-after/Summary.txt`
- `artifacts/pdca/D221/rv1/coverage-*-collect.log`, `coverage-driver.sh`, `coverage-driver-added.sh`
