# E160-30 — manifest (F1: structural EntityBuilder state copy + non-default transfer + derived-root semantics)

- row_id: **E160-30**
- requirement_ids: **R160-02, R160-03, R160-07** (per `P:D160-r3`)
- contract_revision: **rv=4** (supersedes rv=3)
- plan_revision: **r=3**; attempt: **n=1/3**; cycle: **1**; tree: `40b1a159+dirty`
- owner: **`coder` creates execution evidence; `check` independently matches row/result/artifact.**
- defect_key: **F1** (2nd fix of `D160-C1`, `D160.3-1`). Applied fixes of F1: **1** (C1 additionally fixed once in n=2, never reverted). No new defect key. **No F1/C1 recurrence was observed at any boundary in this unit** — the STOP rule did not fire.

## Source paths
- `src/nextorm.core/Builders/EntityBuilder.cs` — `EntityBuilderSharedState` value-type carrier + `CopySharedStateTo<TOther>`; used by `AliasRoot` and `CreateAliasJoined`/`ApplyJoinStateTo`; derived-root `Query`→`SourceFrom` normalization and pre-alias slot rebasing. Guard `EntityBuilder.cs:3464-3466` kept unchanged.
- `tests/nextorm.core.tests/EntityBuilderStateCopyTests.cs`
- `tests/nextorm.core.tests/RootProjectionTests.cs`
- `tests/nextorm.alias.tests/RootAliasTests.cs`
- `tests/nextorm.alias.tests/HintAliasTestContext.cs`

## Exact commands (argument arrays) and results
| # | command (arg array) | phase | exit | selected | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|---|
| 1 | `["dotnet","build","tests/nextorm.core.tests","-c","Debug"]` | inner | 0 | 1 | — | 0 | 0 | `D160.3-1/core-build3.log` |
| 2 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--filter","FullyQualifiedName~EntityBuilderStateCopyTests"]` | inner | 0 | 4 | 4 | 0 | 0 | `D160.3-1/statecopy-filtered.log` |
| 3 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--filter","FullyQualifiedName~RootProjectionTests"]` | inner | 0 | 14 | 14 | 0 | 0 | `D160.3-1/rootprojection-filtered.log` |
| 4 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--filter","FullyQualifiedName~RootAliasTests"]` | inner | 0 | 32 | 32 | 0 | 0 | `D160.3-1/rootalias-filtered.log` |
| 5 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug"]` | boundary | 0 | 96 | 96 | 0 | 0 | `D160.3-1/alias-boundary.log` |
| 6 | `["dotnet","build","nextorm.slnx","-c","Debug"]` | boundary | 0 | 1 | 0 warnings / 0 errors | — | — | `D160.3-1/build.log` |
| 7 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` | boundary | 0 | 1774 | 1774 | 0 | 0 | `D160.3-1/core-boundary.log` |
| 8 | six separate: `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.mysql.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug"]` | boundary | 0 | 1166/806/728/307/233/595 | all | 0 | 1 (sqlite) | `D160.3-1/{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-tests.log` |
| 9 | `["dotnet","build","tests/nextorm.core.tests","-c","Debug"]` + `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--filter","FullyQualifiedName~RootProjectionTests"]` | inner (r=3 closure) | 0 | 15 | 15 | 0 | 0 | `D160.3-5/build.log`, `D160.3-5/root-projection-filtered.log` |
| 10 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--filter","FullyQualifiedName~RootAliasTests"]` | inner (r=3 closure) | 0 | 34 | 34 | 0 | 0 | `D160.3-5/root-alias-filtered.log` |
| 11 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug"]` | boundary (r=3 closure) | 0 | 129 | 129 | 0 | 0 | `D160.3-5/alias-boundary.log` |
| 12 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` | boundary (r=3 closure) | 0 | 1775 | 1775 | 0 | 0 | `D160.3-5/core-boundary.log` |
| 13 | `["dotnet","build","nextorm.slnx","-c","Debug"]` and `["dotnet","build","nextorm.slnx","-c","Release","--no-incremental"]` | boundary (part 1) | 0 | 1 | 0 warnings / 0 errors | — | — | `E160-07/build.log`, `E160-07/release-build.log` |
| 14 | `["DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock","dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--","-noColor","-result-xml","docs/specs/status/rc2-160-evidence/E160-08/integration-results.xml"]` | boundary (part 1) | 0 | 3377 | 3177 | 0 | 200 | `E160-08/integration.log` |
| 15 | `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories=acceptance"]` | boundary (part 1) | 0 | 7 | 7 | 0 | 0 | `E160-09/acceptance.log` |
| 16 | `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test --no-build --verbosity normal"]` | boundary (part 1) | 0 | 9313 | 9112 | 0 | 201 | `E160-10/coverage-collect.log` |
| 17 | `["dotnet","docfx","docs/docfx.json"]` / `["git","diff","--check"]` | boundary (part 1) | 0 | 1 / — | 0 errors / clean | — | — | `E160-11/docfx.log`, `E160-11/git-diff-check.log` |
| 18 | `["python3","/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py","brief","docs/specs/status/rc2-160-evidence/brief.json"]` and `… "report" "…/E160-12/evidence.json"` | boundary (part 1) | 0 | 1 | — | — | — | `E160-12/validator-report.log` |

## Executed test identities (class.method)
- `NextORM.Core.Tests.EntityBuilderStateCopyTests.Every_EntityBuilder_instance_field_is_copied_or_explicitly_excluded`
- `NextORM.Core.Tests.EntityBuilderStateCopyTests.Shared_state_carrier_clone_copies_every_field`
- `NextORM.Core.Tests.EntityBuilderStateCopyTests.Root_alias_preserves_a_subquery_hint_and_the_source_entity_type`
- `NextORM.Core.Tests.EntityBuilderStateCopyTests.Alias_join_transition_preserves_a_subquery_hint_and_the_source_entity_type`
- `NextORM.Core.Tests.RootProjectionTests.Pre_alias_where_is_rebased_onto_the_root_slot_when_a_join_follows` (plus the 13 dim-1/Extend/cache/refusal controls in the same filter)
- `NextORM.AliasTests.RootAliasTests.Generated_root_alias_on_a_fromsql_source_keeps_the_derived_source`
- `NextORM.AliasTests.RootAliasTests.Generated_root_alias_on_a_builder_source_keeps_the_source`
- `NextORM.AliasTests.RootAliasTests.Generated_root_alias_on_a_query_command_source_keeps_the_derived_query`
- `NextORM.AliasTests.RootAliasTests.Generated_WithAlias_and_alias_join_preserve_a_subquery_hint_across_both_transitions`
- `NextORM.AliasTests.RootAliasTests.Generated_root_alias_preserves_a_where_applied_before_it` / `…_an_order_by_…` / `…_a_group_by_…` / `…_paging_…` / `…_where_order_by_and_paging_across_a_join` (pre-`.WithAlias` state, C1 regression guard)

## Row → artifact mapping (cross-reference)
- New row E160-30 → `D160.3-1/` (primary) + `D160.3-5/` (r=3 closure) + part-1 `E160-07/` (Debug/Release build).
- Canonical part-1 rows: E160-08 (integration), E160-09 (acceptance), E160-10 (coverage), E160-11 (docs/links), E160-12 (validator).
- Existing rows refreshed: E160-02/E160-25/E160-26 (derived-root preservation), E160-03/E160-04/E160-06 (core projection/refusal/cache controls in the same filters), E160-27 (C1 fix regression guard).

## Exit / result
All commands exit **0**; Debug and Release solution builds **0 warnings / 0 errors**; no open F1/C1 defect; no residual mutation (`git diff --check` exit 0).
