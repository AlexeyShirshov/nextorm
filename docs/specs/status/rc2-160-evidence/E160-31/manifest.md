# E160-31 — manifest (F2: stored / inline / parenthesized alias receivers × both directions)

- row_id: **E160-31**
- requirement_ids: **R160-01, R160-11** (per `P:D160-r3`)
- contract_revision: **rv=4**
- plan_revision: **r=3**; attempt: **n=1/3**; cycle: **1**; tree: `40b1a159+dirty`
- owner: **`coder` creates execution evidence; `check` independently matches row/result/artifact.**
- defect_key: **F2** (stored non-root alias receiver). Applied fixes of F2: **1** (`D160.3-2`). No new defect key. No F1/C1 recurrence observed.

## Source paths
- `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` — after the observed-transition loop, every alias-bearing schema (`schemas.Values.Where(HasAlias)`) gets its `${suffix}_P{n+1}` result schema and all seven operators' `PositionalTransition` entries seeded (deterministic `JoinOperatorOrder`), reusing `RenderPositionalMethods`; no core/`EntityBuilder.cs` change.
- `tests/nextorm.alias.tests/MixedJoinChainTests.cs`
- `tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs`
- `tests/nextorm.alias.tests/AliasProjectionShapeTests.cs`
- `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`

## Exact commands (argument arrays) and results
| # | command (arg array) | phase | exit | selected | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|---|
| 1 | `["dotnet","build","tests/nextorm.alias.tests","-c","Debug"]` (pre-fix, red) | inner | **1** | 1 | — | 5× CS1061 | — | `D160.3-2/red-build.log` |
| 2 | `["dotnet","build","tests/nextorm.alias.tests","-c","Debug"]` (post-fix) | inner | 0 | 1 | 0 warnings / 0 errors | — | — | `D160.3-2/fix-build.log` |
| 3 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~MixedJoinChainTests"]` | inner | 0 | 20 | 20 | 0 | 0 | `D160.3-2/MixedJoinChainTests.log` |
| 4 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~AliasProjectionShapeTests"]` | inner | 0 | 5 | 5 | 0 | 0 | `D160.3-2/AliasProjectionShapeTests.log` |
| 5 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~AliasGeneratedSurfaceTests"]` | inner | 0 | 5 | 5 | 0 | 0 | `D160.3-2/AliasGeneratedSurfaceTests.log` |
| 6 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasGeneratorDiagnosticTests"]` | inner | 0 | 18 | 18 | 0 | 0 | `D160.3-2/JoinAliasGeneratorDiagnosticTests.log` |
| 7 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug"]` | boundary | 0 | 108 | 108 | 0 | 0 | `D160.3-2/alias-boundary.log` |
| 8 | `["dotnet","build","nextorm.slnx","-c","Debug"]` | boundary | 0 | 1 | 0 warnings / 0 errors | — | — | `D160.3-2/solution-build.log` |
| 9 | six separate: `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.mysql.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug"]`, `["dotnet","test","tests/nextorm.clickhouse.tests","-c","Debug"]` | boundary | 0 | 1166/806/728/307/233/595 | all | 0 | 1 (sqlite) | `D160.3-2/{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-tests.log` |
| 10 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~MixedJoinChainTests"]` (r=3 closure) | inner | 0 | 26 | 26 | 0 | 0 | `D160.3-5/mixed-chain-filtered.log` |
| 11 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~AliasGeneratedSurfaceTests"]` (r=3 closure) | inner | 0 | 5 | 5 | 0 | 0 | `D160.3-5/AliasGeneratedSurfaceTests.log` |
| 12 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug"]` (r=3 closure) | boundary | 0 | 129 | 129 | 0 | 0 | `D160.3-5/alias-boundary.log` |
| 13 | `["dotnet","build","nextorm.slnx","-c","Debug"]` / `["dotnet","build","nextorm.slnx","-c","Release","--no-incremental"]` (part 1) | boundary | 0 | 1 | 0 warnings / 0 errors | — | — | `E160-07/build.log`, `E160-07/release-build.log` |
| 14 | `["DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock","dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--","-noColor","-result-xml","docs/specs/status/rc2-160-evidence/E160-08/integration-results.xml"]` (part 1) | boundary | 0 | 3377 | 3177 | 0 | 200 | `E160-08/integration.log` |
| 15 | `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test --no-build --verbosity normal"]` (part 1) | boundary | 0 | 9313 | 9112 | 0 | 201 | `E160-10/coverage-collect.log` |
| 16 | `["python3","/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py","…"]` (part 1) | boundary | 0 | 1 | — | — | — | `E160-12/validator-report.log` |

## Executed test identities (class.method)
Stored non-root receiver (F2 regression surface):
- `NextORM.AliasTests.MixedJoinChainTests.Stored_alias_receiver_binds_each_generated_positional_operator(string operation)` — `Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`
- `NextORM.AliasTests.MixedJoinChainTests.Stored_alias_receiver_apply_fails_closed_on_a_provider_without_lateral(string operation)` — `CrossApply`, `OuterApply`
- `NextORM.AliasTests.MixedJoinChainTests.Stored_alias_receiver_declares_the_full_generated_positional_operator_set`
- `NextORM.AliasTests.MixedJoinChainTests.Stored_alias_receiver_supports_alternating_alias_positional_alias_with_repeated_clr_types`
- `NextORM.AliasTests.MixedJoinChainTests.Stored_alias_receiver_then_positional_keeps_slot_and_matches_the_inline_route`
- `NextORM.AliasTests.MixedJoinChainTests.Stored_positional_receiver_then_alias_binds_the_alias_to_the_next_slot`
- `NextORM.AliasTests.MixedJoinChainTests.Inline_and_parenthesized_receivers_bind_positional_and_alias_transitions_in_both_directions`
Binding/structure controls:
- `NextORM.AliasTests.AliasProjectionShapeTests.Positional_join_after_alias_binds_the_generated_receiver`
- `NextORM.AliasTests.AliasProjectionShapeTests.Generated_new_instance_transitions_hide_the_inherited_positional_overloads`
- `NextORM.AliasTests.AliasProjectionShapeTests.Generated_new_instance_transitions_win_over_inherited_and_keep_alias_extensions_applicable`
- `NextORM.AliasTests.AliasGeneratedSurfaceTests.Generated_public_type_set_is_frozen`
- `NextORM.AliasTests.AliasGeneratedSurfaceTests.Generated_extension_methods_have_unique_signatures_and_keep_the_frozen_baseline`
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot`

## Public generated-surface delta (frozen)
8 new `${suffix}_P{n+1}` builder/projection pairs: `A1_Root_A2_Buyer_P3`, `A1_Root_P2_P3`, `P1_A2_Buyer2_P3`, `P1_A2_Buyer_A3_Approver_P4`, `P1_A2_Buyer_P3_A4_Approver_P5`, `P1_A2_Buyer_P3_P4`, `P1_P2_A3_Approver_P4`, `P1_P2_A3_Buyer2_P4`.

## Row → artifact mapping (cross-reference)
- New row E160-31 → `D160.3-2/` (primary) + `D160.3-5/` (r=3 closure) + part-1 `E160-07/`.
- Refreshed rows: E160-01, E160-15, E160-16, E160-17 (mixing directions), E160-18 (parity), E160-20/E160-21 (receiver binding/shadowing), E160-23 (F2 related).

## Exit / result
Red→green proven: pre-fix alias build exit **1** (5× CS1061, inherited positional route yields `Projection<…,Person>` without `Item3`); post-fix build exit **0** (0/0), all filtered and boundary runs exit **0** with 0 failed. No structural check (`every declared operator`) failure. No F1/C1 recurrence.
