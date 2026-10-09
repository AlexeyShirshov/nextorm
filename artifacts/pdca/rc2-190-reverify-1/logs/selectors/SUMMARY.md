# R190 re-verification (rv=2) — 13-selector summary

- F (acceptance tip) = `ee42f183ffa1d769da76a0ccaa024ec53a7885c9` (current HEAD)
- B (reconciliation reference) = `cf34f91045781faccdeb3b7163be2cc5f1dce0f2`
- Build: `dotnet build nextorm.slnx -c Debug` → exit 0, 0W/0E (`../build.log`)
- Command form per run: `dotnet test <project> -c Debug --no-build --verbosity normal --filter "FullyQualifiedName~<selector>"`
  (integration runs add `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`)
- All runs executed fresh on F, after the build above. Every run selected >=1 test.

| # | selector | project | exit | selected | passed | failed | skipped | log |
|---|----------|---------|------|----------|--------|--------|---------|-----|
| 1 | EntityItemProjectionInMemoryTests | tests/nextorm.core.tests | 0 | 7 | 7 | 0 | 0 | core.EntityItemProjectionInMemoryTests.log |
| 2 | RowMaterializerBuilderTests | tests/nextorm.core.tests | 0 | 6 | 6 | 0 | 0 | core.RowMaterializerBuilderTests.log |
| 3 | SelectExpressionPlanEqualityComparerTests | tests/nextorm.core.tests | 0 | 12 | 12 | 0 | 0 | core.SelectExpressionPlanEqualityComparerTests.log |
| 4 | JoinReturningIdentityTests | tests/nextorm.core.tests | 0 | 24 | 24 | 0 | 0 | core.JoinReturningIdentityTests.log |
| 5 | PlanKeyUniquenessTests | tests/nextorm.sqlite.tests | 0 | 13 | 13 | 0 | 0 | sqlite.PlanKeyUniquenessTests.log |
| 6 | EntityItemProjectionTests | tests/nextorm.sqlite.tests | 0 | 15 | 15 | 0 | 0 | sqlite.EntityItemProjectionTests.log |
| 7 | JoinWholeEntitySqlGenerationTests | tests/nextorm.sqlite.tests | 0 | 8 | 8 | 0 | 0 | provider.sqlite.JoinWholeEntitySqlGenerationTests.log |
| 8 | JoinWholeEntitySqlGenerationTests | tests/nextorm.postgres.tests | 0 | 8 | 8 | 0 | 0 | provider.postgres.JoinWholeEntitySqlGenerationTests.log |
| 9 | JoinWholeEntitySqlGenerationTests | tests/nextorm.sqlserver.tests | 0 | 8 | 8 | 0 | 0 | provider.sqlserver.JoinWholeEntitySqlGenerationTests.log |
| 10 | JoinWholeEntitySqlGenerationTests | tests/nextorm.mysql.tests | 0 | 8 | 8 | 0 | 0 | provider.mysql.JoinWholeEntitySqlGenerationTests.log |
| 11 | JoinWholeEntitySqlGenerationTests | tests/nextorm.mariadb.tests | 0 | 8 | 8 | 0 | 0 | provider.mariadb.JoinWholeEntitySqlGenerationTests.log |
| 12 | JoinWholeEntitySqlGenerationTests | tests/nextorm.clickhouse.tests | 0 | 8 | 8 | 0 | 0 | provider.clickhouse.JoinWholeEntitySqlGenerationTests.log |
| 13 | JoinWholeEntity_ | tests/nextorm.integration.tests | 0 | 20 | 20 | 0 | 0 | int.JoinWholeEntity_.log |
| 14 | MariaDbJoinWholeEntityIntegrationTests | tests/nextorm.integration.tests | 0 | 2 | 2 | 0 | 0 | int.MariaDbJoinWholeEntityIntegrationTests.log |
| 15 | ClickHouseJoinWholeEntityIntegrationTests | tests/nextorm.integration.tests | 0 | 2 | 2 | 0 | 0 | int.ClickHouseJoinWholeEntityIntegrationTests.log |
| 16 | Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData | tests/nextorm.integration.tests | 0 | 4 | 4 | 0 | 0 | int.Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData.log |
| 17 | Cte_Typed_SelfJoin_ShouldReturnBothSides | tests/nextorm.integration.tests | 0 | 4 | 4 | 0 | 0 | int.Cte_Typed_SelfJoin_ShouldReturnBothSides.log |
| 18 | JoinArities_ToSqlAndUpdateJoin_ShouldRender | tests/nextorm.integration.tests | 0 | 1 | 1 | 0 | 0 | int.JoinArities_ToSqlAndUpdateJoin_ShouldRender.log |

Selector #7 (`JoinWholeEntitySqlGenerationTests`) is the provider-shape selector spanning the six
provider test projects (rows 7-12). All other selectors are single-project. No selector selected 0 tests
and no run exited nonzero.

## Historical RED subrecord (EV190-RED) — cited, NOT recreated

Read from predecessor evidence, not re-run on F:

- `artifacts/pdca/rc2-190/EV190-RED-CORE/run.log` — total 5, failed 3, succeeded 2, skipped 0, exit code 2 (`EV190-RED-CORE/exit-code.txt` = `RC=2`).
- `artifacts/pdca/rc2-190/EV190-RED-SQLITE/run.log` — total 7, failed 3, succeeded 4, skipped 0, exit code 2 (`EV190-RED-SQLITE/exit-code.txt` = `RC=2`).
- Predecessor status: `docs/specs/status/rc2-190-join-whole-entity-1.md`.
