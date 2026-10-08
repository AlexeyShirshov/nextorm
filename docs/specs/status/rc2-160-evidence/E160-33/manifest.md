# E160-33 — manifest (F4: correlated lambda / QueryCommand compilation, outer slots, SQL, real results)

- row_id: **E160-33**
- requirement_ids: **R160-01, R160-03, R160-09, R160-11** (per `P:D160-r3`; R160-09 → E160-08/E160-33)
- contract_revision: **rv=4**
- plan_revision: **r=3**; attempt: **n=1/3**; cycle: **1**; tree: `40b1a159+dirty`
- owner: **`coder` creates execution evidence; `check` independently matches row/result/artifact.**
- defect_key: **F4** (correlated APPLY through the alias seam). Applied fixes of F4: **1** (`D160.3-4`). No new public core seam. No F1/C1 recurrence observed.

## Source paths
- `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` — `JoinSourceKind.Correlated`, `TryGetJoinedType(..., allowCorrelated)` + `IsCorrelatedSource`; emits both `Expression<Func<TEntity, EntityBuilder<TJoin>>>` and `Expression<Func<TEntity, QueryCommand<TJoin>>>` alias extensions forwarding to the existing `EntityBuilder.JoinAlias` correlated seams (`EntityBuilder.cs:3015-3021,3036-3042`); a step after a correlated APPLY is intentionally not emitted.
- `tests/nextorm.alias.tests/MixedJoinChainTests.cs`
- `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`
- `tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs`
- `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/JoinAliasSqlGenerationTests.cs`
- `tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs`, `MariaDbJoinAliasIntegrationTests.cs`, `ClickHouseIntegrationTests.cs`

## Exact commands (argument arrays) and results
| # | command (arg array) | phase | exit | selected | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|---|
| 1 | `["dotnet","build","nextorm.slnx","-c","Debug"]` | boundary | 0 | 1 | 0 warnings / 0 errors | — | — | `D160.3-4/build3.log` |
| 2 | `["dotnet","build","tests/nextorm.alias.tests","-c","Debug"]` | inner | 0 | 1 | — | — | — | `D160.3-4/alias-build3.log` |
| 3 | `["dotnet","build","tests/nextorm.integration.tests","-c","Debug"]` | inner | 0 | 1 | — | — | — | `D160.3-4/integration-build2.log` |
| 4 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~MixedJoinChainTests"]` | inner | 0 | 25 | 25 | 0 | 0 | `D160.3-4/mixed-filtered2.log` |
| 5 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasGeneratorDiagnosticTests"]` | inner | 0 | 30 | 30 | 0 | 0 | `D160.3-4/diagnostic-filtered.log` |
| 6 | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]` | inner | 0 | 19 | 19 | 0 | 0 | `D160.3-4/postgres-join-alias.log` |
| 7 | `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.mysql.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]` | inner | 0 | 19 / 19 / 19 | all | 0 | 0 | `D160.3-4/{sqlserver,mysql,mariadb}-join-alias.log` |
| 8 | `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]` | inner | 0 | 17 / 20 | all | 0 | 0 | `D160.3-4/{sqlite,clickhouse}-join-alias.log` |
| 9 | `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.mysql.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]`, `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasSqlGenerationTests"]` | inner | 0 | 20/22/22/22/22/23 | all | 0 | 0 | `D160.3-5/{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-join-alias.log` |
| 10 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build"]` | boundary | 0 | 125 | 125 | 0 | 0 | `D160.3-4/alias-boundary2.log` |
| 11 | six separate `--no-build`: `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--no-build"]`, `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build"]`, `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug","--no-build"]`, `["dotnet","test","tests/nextorm.mysql.tests","-c","Debug","--no-build"]`, `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build"]`, `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug","--no-build"]` | boundary | 0 | 1170/809/731/310/236/599 | all | 0 | 1 (sqlite) | `D160.3-4/{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-boundary.log` |
| 12 | `["DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock","dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--no-build","--","-noColor","-result-xml","…/D160.3-4/integration-results.xml"]` | boundary | 0 | 3369 | 3169 | 0 | 200 | `D160.3-4/integration.log` |
| 13 | `["DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock","dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--","-noColor","-result-xml","docs/specs/status/rc2-160-evidence/E160-08/integration-results.xml"]` (part 1, post-MariaDB-fix) | boundary | 0 | 3377 | 3177 | 0 | 200 | `E160-08/integration.log` |
| 14 | `["dotnet","build","nextorm.slnx","-c","Debug"]` / `["dotnet","build","nextorm.slnx","-c","Release","--no-incremental"]` (part 1) | boundary | 0 | 1 | 0 warnings / 0 errors | — | — | `E160-07/build.log`, `E160-07/release-build.log` |
| 15 | `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories=acceptance"]` (part 1) | boundary | 0 | 7 | 7 | 0 | 0 | `E160-09/acceptance.log` |
| 16 | `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test --no-build --verbosity normal"]` (part 1) | boundary | 0 | 9313 | 9112 | 0 | 201 | `E160-10/coverage-collect.log` |
| 17 | `["python3","/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py","…"]` (part 1) | boundary | 0 | 1 | — | — | — | `E160-12/validator-report.log`, `D160.3-4/validator-report.log` |

## Executed test identities (class.method)
Alias compilation / outer slots (A):
- `NextORM.AliasTests.MixedJoinChainTests.Correlated_builder_apply_alias_routes_through_the_alias_seam`
- `NextORM.AliasTests.MixedJoinChainTests.Correlated_query_apply_alias_routes_through_the_alias_seam`
- `NextORM.AliasTests.MixedJoinChainTests.Correlated_outer_apply_alias_routes_through_the_alias_seam`
- `NextORM.AliasTests.MixedJoinChainTests.Correlated_apply_after_a_preceding_alias_is_refused_fail_closed`
- `NextORM.AliasTests.MixedJoinChainTests.Positional_step_after_a_correlated_apply_binds_the_generated_receiver`
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Correlated_apply_alias_emits_builder_and_query_source_seam_overloads`
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Alias_step_after_a_correlated_apply_has_no_generated_extension`
- `NextORM.AliasTests.AliasGeneratedSurfaceTests.Generated_public_type_set_is_frozen` (10 new correlated alias types)

Provider SQL (S) — correlated APPLY alias is byte-identical to positional plus dialect spelling:
- `NextORM.Postgres.Tests.JoinAliasSqlGenerationTests.Correlated_builder_cross_apply_alias_matches_positional`
- `…Correlated_query_cross_apply_alias_matches_positional`
- `…Correlated_builder_outer_apply_alias_matches_positional`
- same three in `NextORM.SqlServer.Tests`, `NextORM.MySql.Tests`, `NextORM.MariaDb.Tests` (`…_matches_positional`)
- `NextORM.Sqlite.Tests.JoinAliasSqlGenerationTests.Correlated_{builder,query}_{cross,outer}_apply_alias_fails_closed_without_lateral`
- `NextORM.ClickHouse.Tests.JoinAliasSqlGenerationTests.Correlated_{builder,query}_{cross,outer}_apply_alias_fails_closed_without_lateral` (ClickHouse no LATERAL)

Real results (I):
- `NextORM.Integration.Tests.CommonTestSuite.Correlated_cross_apply_alias_builder_source_returns_matched_rows`
- `…Correlated_cross_apply_alias_query_source_returns_matched_rows`
- `…Correlated_outer_apply_alias_preserves_unmatched_outer_rows`
- `NextORM.Integration.Tests.MariaDbJoinAliasIntegrationTests.Root_alias_inner_join_returns_matched_rows`, `.Root_alias_then_mixed_positional_join_executes_and_keeps_slots`, `.Mixed_positional_then_alias_join_executes_and_keeps_slot_identity`, `.Alternating_alias_positional_alias_executes_and_keeps_slots`
- `NextORM.Integration.Tests.ClickHouseIntegrationTests.Root_alias_inner_join_returns_matched_rows`, `.Root_alias_then_mixed_positional_join_executes_and_keeps_slots`, `.Mixed_positional_then_alias_join_executes_and_keeps_slot_identity`, `.Alternating_alias_positional_alias_executes_and_keeps_slots`

## Real-execution result (D160.3-4 and part-1 E160-08)
- D160.3-4: Total **3369**, Errors 0, Failed 0, Skipped 200; **9/9** correlated-APPLY alias cases pass on PostgreSQL / SQL Server / MySQL; 3 capability-skipped on SQLite (`Provider.SupportsApply=false`); 0 provider-availability skips.
- part-1 E160-08: Total **3377**, Errors 0, Failed 0, Skipped 200, Not Run 0; six providers executed. D160 buckets: root alias **16 passed**, mixed positional/alias **10 passed**, correlated APPLY alias **9 passed + 3 capability-skipped**; **0 provider-availability skips**.

## Guarded limitation (recorded, not a DO→PLAN candidate)
Correlated APPLY requires a **non-projection single-entity receiver**: runtime guard `EntityBuilder.JoinAliasApply` (`EntityBuilder.cs:3096-3098`, mirrored by positional `JoinApply` `:3570`) throws `NotSupportedException("*single-entity*")` when `TEntity` is a join projection. Consequently (i) a correlated APPLY after an alias/join step is fail-closed by design, and (ii) an alias step **after** a correlated APPLY does not bind — the generator deliberately does not emit the follower's alias transition (pinned by `Alias_step_after_a_correlated_apply_has_no_generated_extension`). A positional step after a correlated APPLY does bind and route (green). Re-open trigger: if free-mixing scope later requires a correlated-APPLY follower, the fix is generator-only and needs no new public seam.

## Row → artifact mapping (cross-reference)
- New row E160-33 → `D160.3-4/` (primary) + `D160.3-5/` (fixtures) + part-1 `E160-08/`, `E160-09/`, `E160-10/`, `E160-12/`.
- Refreshed rows: E160-01 (mixing), E160-02 (root matrix), E160-08 (R160-09 six-provider integration), E160-20/E160-29 (binding/classification).

## Exit / result
All commands exit **0**; Debug/Release solution builds **0 warnings / 0 errors**; correlated alias form proven byte-identical to positional on supporting dialects and fail-closed on SQLite/ClickHouse; real execution green on PostgreSQL/SQL Server/MySQL, capability-skipped on SQLite only. No new public core seam. No renderer/planner defect. **No F1/C1 recurrence.**
