# D175 r1 — integration guard

### git diff -- tests/nextorm.integration.tests
```
(empty)
exit=0
```
### git diff --cached -- tests/nextorm.integration.tests
```
(empty)
exit=0
```
### changed tracked files (all)
```
 M tests/nextorm.core.tests/ImplicitNavigationR3CountBoundaryTests.cs
 M tests/nextorm.core.tests/InMemoryTests.cs
 M tests/nextorm.core.tests/PlanKeyStructureTests.cs
 M tests/nextorm.core.tests/RawSourceBindingFilterTests.cs
 M tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs
```

## Disposition

The integration-guard predicate is **FALSE**: neither the unstaged nor the staged diff over
`tests/nextorm.integration.tests` contains any change (0 files). No integration-suite file changed.

Therefore **no integration provider (PostgreSQL / SQL Server / MySQL / ClickHouse / Testcontainers)
was run** — this is the plan-permitted condition "INTEGRATION only if an integration-suite file
changed", and running the container suite would have been an out-of-scope boundary sweep. The D175
delta is test-only in `tests/nextorm.core.tests/**`, so the core + SQLite boundary sweeps are the
complete boundary evidence.
