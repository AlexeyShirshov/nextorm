# D190 provenance ledger (rc2-190-join-whole-entity-1)

Read-only checker entry point. Every row is a real invocation with the recorded argv, the filter
value, exit code and selected test count, plus the raw log path. All cwd = `/home/alex/sources/nextorm`
unless stated. Timestamps are host-local 2026-10-08 (≈UTC+5). `$E = artifacts/pdca/rc2-190`.
Revision strings: `n1 = 0817ffef`, `n1-fix = b447b7be`, `n2 = cabc1d72`, `n3 = d67e3b3d` (current HEAD).
`exit 0` = success; `exit 1` = test failures; `exit 2` = runner reports failed tests OR the
`dotnet test` non-success code; `124` = shell timeout (killed).

## Builds

| # | argv (array) | phase | rev | exit | rev-source | log |
|---|--------------|-------|-----|------|-----------|-----|
| B1 | `["dotnet","build","nextorm.slnx","-c","Debug"]` | boundary | n3 | 0 (0W/0E) | d67e3b3d | `n3-build/run.log` |
| B2 | `["dotnet","build","nextorm.slnx","-c","Release"]` | boundary | n3 | 0 (0W/0E) | d67e3b3d | `n3-build-release/run.log` |
| B3 | `["dotnet","build","nextorm.slnx","-c","Debug"]` | — | n1-fix | 0 | b447b7be | `n3-branch-baseline-full/build.log` |
| B4 | 8× `["dotnet","build","tests/<project>","-c","Debug"]` (core, sqlite, postgres, sqlserver, mysql, mariadb, clickhouse, integration) | inner | n3 | 0 (0W/0E each) | d67e3b3d | `n3-*/` build output |

## Unit / SQL-shape inner-loop runs (n=3 final tree d67e3b3d)

| # | project | `--filter` value (no `|`) | exit | selected | failed | log |
|---|---------|---------------------------|------|----------|--------|-----|
| T1 | tests/nextorm.core.tests | `FullyQualifiedName~EntityItemProjectionInMemoryTests` | 0 | 7 | 0 | `n3-split-core-ei/run.log` |
| T2 | tests/nextorm.core.tests | `FullyQualifiedName~RowMaterializerBuilderTests` | 0 | 6 | 0 | `n3-split-core-rm/run.log` |
| T3 | tests/nextorm.core.tests | `FullyQualifiedName~SelectExpressionPlanEqualityComparerTests` | 0 | 12 | 0 | `n3-split-core-sc/run.log` |
| T4 | tests/nextorm.core.tests | `FullyQualifiedName~JoinReturningIdentityTests` | 0 | 24 | 0 | `n3-split-core-jr/run.log` |
| T5 | tests/nextorm.sqlite.tests | `FullyQualifiedName~EntityItemProjectionTests` | 0 | 15 | 0 | `n3-split-sqlite-ei/run.log` |
| T6 | tests/nextorm.sqlite.tests | `FullyQualifiedName~PlanKeyUniquenessTests` | 0 | 13 | 0 | `n3-split-sqlite-pk/run.log` |
| T7 | tests/nextorm.sqlite.tests | `FullyQualifiedName~JoinWholeEntitySqlGenerationTests` | 0 | 8 | 0 | `n3-split-sqlite-jw/run.log` |
| T8 | tests/nextorm.postgres.tests | `FullyQualifiedName~JoinWholeEntitySqlGenerationTests` | 0 | 8 | 0 | `n3-sql-postgres/run.log` |
| T9 | tests/nextorm.sqlserver.tests | `FullyQualifiedName~JoinWholeEntitySqlGenerationTests` | 0 | 8 | 0 | `n3-sql-sqlserver/run.log` |
| T10 | tests/nextorm.mysql.tests | `FullyQualifiedName~JoinWholeEntitySqlGenerationTests` | 0 | 8 | 0 | `n3-sql-mysql/run.log` |
| T11 | tests/nextorm.mariadb.tests | `FullyQualifiedName~JoinWholeEntitySqlGenerationTests` | 0 | 8 | 0 | `n3-sql-mariadb/run.log` |
| T12 | tests/nextorm.clickhouse.tests | `FullyQualifiedName~JoinWholeEntitySqlGenerationTests` | 0 | 8 | 0 | `n3-sql-clickhouse/run.log` |

argv template: `["dotnet","test","<project>","-c","Debug","--no-build","--filter","<value>"]`.

## Integration runs (n=3 tree; `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`)

| # | project | `--filter` value | exit | selected | skipped | log |
|---|---------|------------------|------|----------|---------|-----|
| I1 | tests/nextorm.integration.tests | `FullyQualifiedName~JoinWholeEntity_` | 0 | 20 | 0 | `n3-split-int-direct/run.log` |
| I2 | tests/nextorm.integration.tests | `FullyQualifiedName~MariaDbJoinWholeEntityIntegrationTests` | 0 | 2 | 0 | `n3-split-int-mariadb/run.log` |
| I3 | tests/nextorm.integration.tests | `FullyQualifiedName~ClickHouseJoinWholeEntityIntegrationTests` | 0 | 2 | 0 | `n3-split-int-clickhouse/run.log` |
| I4 | tests/nextorm.integration.tests | `FullyQualifiedName~Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData` | 0 | 4 | 0 | `n3-split-int-ctehet/run.log` |
| I5 | tests/nextorm.integration.tests | `FullyQualifiedName~Cte_Typed_SelfJoin_ShouldReturnBothSides` | 0 | 4 | 0 | `n3-split-int-cteself/run.log` |
| I6 | tests/nextorm.integration.tests | `FullyQualifiedName~JoinArities_ToSqlAndUpdateJoin_ShouldRender` | 0 | 1 | 0 | `n3-split-int-joinar/run.log` |

Combined integration run (I1–I6 in one argv, `--filter` joined with `|`): exit 0, selected 29, skipped 0,
log `n3-integration/run.log`.

## Boundary sweep / coverage / perf (n=3 tree)

| # | argv (array) | exit | selected | log |
|---|--------------|------|----------|-----|
| C1 | `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test --no-build --verbosity normal -c Debug --max-parallel-test-modules 1"]` | 0 | 9235 (0 failed, 198 capability skips) | `n3-coverage/run.log` |
| C2 | `["dotnet","tool","run","reportgenerator","-reports:tests/coverage/coverage.cobertura.xml","-targetdir:tests/coverage/report","-reporttypes:Html;TextSummary;Cobertura","-riskhotspotassemblyfilters:+nextorm.*"]` | 0 | — | `n3-coverage-report/run.log`; summary `tests/coverage/report/Summary.txt` |
| P1 | `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--no-build","--","--anyCategories=acceptance"]` | 0 | 7 cases, 0 failures, BDN 43.67 s, wall 44 s | `n3-perf/run.log` |

## Branch baseline (full-sweep scope, base `b447b7be` = `eb45e213~1`) — temporary worktree

| # | argv / action | cwd | exit | log |
|---|---------------|-----|------|-----|
| D1 | `git worktree add /tmp/opencode/d190-base-full eb45e213~1` | repo | 0 | — |
| D2 | `dotnet build nextorm.slnx -c Debug` | `/tmp/opencode/d190-base-full` | 0 | `n3-branch-baseline-full/build.log` |
| D3 | `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o <E>/n3-branch-baseline-full/base-full.cobertura.xml "dotnet test --no-build --verbosity normal -c Debug --max-parallel-test-modules 1"` (+DOCKER_HOST) | `/tmp/opencode/d190-base-full` | **2** (9184 total, **2 unrelated failures**: `ClickHouseExtremeRowNativeSpecificTests.FloatNativeParity_GroupedCompositeAndThreeComponent_MinAndMax_ShouldMatchPortable`, `TimedDictionaryEvictionTests.ExpireTimedEntriesForTesting_Should_Age_All_Five_ProcessWide_Caches`; coverage XML still produced) | `n3-branch-baseline-full/sweep.log` |
| D4 | `git worktree remove --force /tmp/opencode/d190-base-full` | repo | 0 | — |

## Mutation runs (all disclosure; none usable)

| # | argv (array) | config | exit | outcome | log |
|---|--------------|--------|------|---------|-----|
| M1 | `["dotnet-stryker","--config-file","artifacts/pdca/rc2-190/mutation/stryker-config.json"]` | mutate QueryPreparer+RowMaterializerBuilder; default vstest; coverage on | **124** | timed out; 1167 mutants pending | `mutation/run.log` |
| M2 | `["dotnet-stryker","--config-file","artifacts/pdca/rc2-190/mutation/stryker-config-rowmaterializer.json"]` | RowMaterializerBuilder; coverage on | **124** | timed out; 182 pending | `mutation/run-rowmaterializer.log` |
| M3 | `["dotnet-stryker","--config-file","artifacts/pdca/rc2-190/mutation/stryker-n2-rowmaterializer.json"]` | RowMaterializerBuilder; coverage-analysis off | **0** | 182 tested, **score 0.00%** (182 survived, 0 killed) | `mutation/run-n2-rowmaterializer.log` |
| M4 | `["dotnet-stryker","--config-file","artifacts/pdca/rc2-190/mutation/stryker-n2-rowmaterializer-sqlite.json"]` | +sqlite project | **0** | 182 tested, **score 0.00%** | `mutation/run-n2-rowmaterializer-sqlite.log` |
| M5 | `["dotnet-stryker","--config-file","artifacts/pdca/rc2-190/mutation/stryker-n3-recognizer.json"]` | QueryPreparer; coverage perTest; vstest | **124** | 1035 pending; 8151 compile errors | `mutation/run-n3-recognizer.log` |
| M6 | `TreatWarningsAsErrors=false dotnet-stryker --config-file artifacts/pdca/rc2-190/mutation/stryker-n3-mtp.json` | QueryPreparer; `test-runner: mtp`; coverage perTest; warnings relaxed | **124** | discovered **9204** tests (filter not honoured under MTP) → 1035 mutants pending; 8158 compile errors | `mutation/run-n3-mtp.log` |

Mutation tooling: `dotnet-stryker` 5.0.0 (global), `--test-runner` supports `vstest,mtp`; the MTP runner
accepted the option but ignored `test-case-filter`. `TreatWarningsAsErrors=false` in the environment did
not remove the Stryker Safe-Mode compile errors (CS0165/CS8081 in unrelated visitor files). Mutation is
**not usable as evidence**; residual unverified mutants: `RowMaterializerBuilder.cs` 182,
`QueryCommand.QueryPreparer.cs` 1035. Row intentionally open.

## Inner-loop validator

| # | argv | exit | output |
|---|------|------|--------|
| V1 | `["python3","/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py","brief","artifacts/pdca/rc2-190/test-scope.json"]` | 0 | `validate-inner-loop-brief.txt` |
| V2 | `["python3","/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py","report","artifacts/pdca/rc2-190/evidence.json"]` | 0 | `validate-inner-loop-report-n3.txt` |
