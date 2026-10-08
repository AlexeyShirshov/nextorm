# D160.3-1 — DO→CHECK boundary sweeps (single pass)

Unit: D160.3-1 (F1, 2nd fix of D160-C1). Tree: `40b1a159+dirty`. All commands are full-project runs
(`dotnet test <project> -c Debug`); the host `validate_inner_loop.py` allows only one unfiltered
boundary test sweep in `evidence.json`, so the alias sweep + solution build are recorded there and the
remaining full-project sweeps are recorded here (and in the per-project logs).

| project | command | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| nextorm.alias.tests | `dotnet test tests/nextorm.alias.tests -c Debug` | 0 | 96 | 96 | 0 | 0 | `alias-boundary.log` |
| nextorm.core.tests | `dotnet test tests/nextorm.core.tests -c Debug` | 0 | 1774 | 1774 | 0 | 0 | `core-boundary.log` |
| nextorm.sqlite.tests | `dotnet test tests/nextorm.sqlite.tests -c Debug` | 0 | 1166 | 1165 | 0 | 1 | `sqlite-tests.log` |
| nextorm.postgres.tests | `dotnet test tests/nextorm.postgres.tests -c Debug` | 0 | 806 | 806 | 0 | 0 | `postgres-tests.log` |
| nextorm.sqlserver.tests | `dotnet test tests/nextorm.sqlserver.tests -c Debug` | 0 | 728 | 728 | 0 | 0 | `sqlserver-tests.log` |
| nextorm.mysql.tests | `dotnet test tests/nextorm.mysql.tests -c Debug` | 0 | 307 | 307 | 0 | 0 | `mysql-tests.log` |
| nextorm.mariadb.tests | `dotnet test tests/nextorm.mariadb.tests -c Debug` | 0 | 233 | 233 | 0 | 0 | `mariadb-tests.log` |
| nextorm.clickhouse.tests | `dotnet test tests/nextorm.clickhouse.tests -c Debug` | 0 | 595 | 595 | 0 | 0 | `clickhouse-tests.log` |
| solution build | `dotnet build nextorm.slnx -c Debug` | 0 | — | 0 warnings / 0 errors | — | — | `build.log` |

Regression note: the first core boundary sweep failed 14 tests (`QueryFilterTests`,
`RawSourceBindingFilterTests`, `JoinIntoInMemoryFilterTests`) because `QueryFilterFunc`'s
reflection-based purity snapshot compares the `_state` reference; a class-based carrier clone was
therefore seen as changed. Fixed inside `EntityBuilder.cs` only by making `EntityBuilderSharedState` a
value type (structural boxed equality); `QueryFilterFunc` and `QueryFilterTests` were not modified.
See `core-boundary.log` (pre-fix) and `queryfilter-check.log` / `core-boundary.log` (post-fix).
