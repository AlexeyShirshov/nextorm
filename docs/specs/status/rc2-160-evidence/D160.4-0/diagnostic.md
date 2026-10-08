# D160.4-0 — diagnostic (clean-build red reproduction + stale-generator check)

- Plan `r=4`, attempt `n=1/3`, contract `rv=5`, cycle 1. Unit `.4-0` (diagnostic only — **no product edits**).
- Tree: `40b1a159bdf60289ebf5a01b9fffc36786cdd94d` (branch `1.0.9-rc2`), `dotnet 10.0.401`.
- Run identity in `provenance.log` (HEAD, `git status --porcelain`, source diff/untracked hashes excluding `docs/specs/status/**`).
- Command logs in this directory. Raw output not pasted.

## Fresh clean boundary (B-clean)

| step | command | exit | result |
|---|---|---|---|
| clean Debug | `dotnet clean nextorm.slnx -c Debug` | 0 | Succeeded |
| clean Release | `dotnet clean nextorm.slnx -c Release` | 0 | Succeeded |
| build Debug | `dotnet build nextorm.slnx -c Debug --no-incremental` | 0 | **0 Warning(s) / 0 Error(s)** (47.33s) |
| build Release | `dotnet build nextorm.slnx -c Release --no-incremental` | 0 | **0 Warning(s) / 0 Error(s)** (28.63s) |

Logs: `clean-debug.log`, `clean-release.log`, `build-debug-clean.log`, `build-release-clean.log`.

## Diagnostic suites on the fresh clean build (exact frozen A/K/S commands)

| suite | command | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| A alias | `dotnet test tests/nextorm.alias.tests -c Debug` | 2 | 129 | 111 | 18 | 0 | `A-alias.log` |
| K core | `dotnet test tests/nextorm.core.tests -c Debug` | 2 | 1776 | 1775 | 1 | 0 | `K-core.log` |
| S sqlite | `dotnet test tests/nextorm.sqlite.tests -c Debug` | 2 | 1173 | 1169 | 3 | 1 | `S-sqlite.log` |
| S postgres | `dotnet test tests/nextorm.postgres.tests -c Debug` | 2 | 812 | 809 | 3 | 0 | `S-postgres.log` |
| S sqlserver | `dotnet test tests/nextorm.sqlserver.tests -c Debug` | 2 | 734 | 731 | 3 | 0 | `S-sqlserver.log` |
| S mysql | `dotnet test tests/nextorm.mysql.tests -c Debug` | 2 | 313 | 310 | 3 | 0 | `S-mysql.log` |
| S mariadb | `dotnet test tests/nextorm.mariadb.tests -c Debug` | 2 | 239 | 236 | 3 | 0 | `S-mariadb.log` |
| S clickhouse | `dotnet test tests/nextorm.clickhouse.tests -c Debug` | 2 | 602 | 599 | 3 | 0 | `S-clickhouse.log` |

The red reproduces **exactly** the CHECK r=3 failure set and count (alias 18, core 1, six providers 3 each) on a
clean `--no-incremental` build. This is expected diagnostic output for `.4-0`; nothing was fixed.

## Exact failing tests + assertion site

### A alias (18)
- `AliasGeneratedSurfaceTests.Generated_projection_builder_pairs_are_public_and_slot_bound` — `AliasGeneratedSurfaceTests.cs:114` (helper `AssertPair` `:239`)
- `AliasJoinWhereTests.Where_on_base_before_a_second_alias_join_keeps_the_base_slot` — `AliasJoinWhereTests.cs:56`
- `AliasJoinWhereTests.Where_after_second_alias_join_targets_the_second_joined_slot` — `AliasJoinWhereTests.cs:118`
- `AliasJoinWhereTests.Where_between_first_and_second_alias_join_targets_the_first_joined_slot` — `AliasJoinWhereTests.cs:93`
- `AliasPlanCacheTests.Alternating_buyer_and_approver_queries_keep_slots_and_the_plan_cache` — `AliasPlanCacheTests.cs:34`
- `AliasProjectionShapeTests.Alias_projection_retains_item_members_and_exposes_alias_members` — `AliasProjectionShapeTests.cs:34`
- `AliasProjectionShapeTests.Alias_members_carry_join_slot_attribute_and_resolve_to_the_right_slot` — `AliasProjectionShapeTests.cs:174`
- `AliasProjectionShapeTests.Generated_new_instance_transitions_win_over_inherited_and_keep_alias_extensions_applicable` — `AliasProjectionShapeTests.cs:158`
- `AliasSqliteEndToEndTests.Repeated_joined_type_resolves_to_distinct_slots_and_distinct_sql_aliases` — `AliasSqliteEndToEndTests.cs:30`
- `JoinAliasGeneratorDiagnosticTests.Root_alias_emits_a_dim1_projection_and_a_withalias_extension` — `JoinAliasGeneratorDiagnosticTests.cs:97`
- `JoinAliasGeneratorDiagnosticTests.Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot` — `JoinAliasGeneratorDiagnosticTests.cs:302`
- `JoinAliasGeneratorDiagnosticTests.Correlated_apply_alias_emits_builder_and_query_source_seam_overloads` — `JoinAliasGeneratorDiagnosticTests.cs:430`
- `MixedJoinChainTests.Digit_ending_alias_in_a_non_matching_slot_resolves_to_its_actual_slot` — `MixedJoinChainTests.cs:219`
- `MixedJoinChainTests.Correlated_builder_apply_alias_routes_through_the_alias_seam` — `MixedJoinChainTests.cs:483`
- `MixedJoinChainTests.Stored_alias_receiver_supports_alternating_alias_positional_alias_with_repeated_clr_types` — `MixedJoinChainTests.cs:347`
- `MixedJoinChainTests.Correlated_query_apply_alias_routes_through_the_alias_seam` — `MixedJoinChainTests.cs:518`
- `TypedCteAliasTests.Join_of_two_distinct_same_type_typed_ctes_binds_each_to_its_alias` — `TypedCteAliasTests.cs:138`
- `TypedCteAliasTests.Shared_two_step_chain_with_entity_and_cte_intermediate_compiles_and_binds_both_branches` — `TypedCteAliasTests.cs:272`

### K core (1)
- `EntityBuilderStateCopyTests.Positional_join_control_pins_the_baseline_modifier_behavior` — `EntityBuilderStateCopyTests.cs:191` (positional join over-preserves `_subQueryHint`/`_tag`/`_commandTimeout`).

### S providers (same 3 tests in each of six dialects)
- `JoinAliasSqlGenerationTests.Repeated_clr_type_resolves_to_distinct_slots` — sqlite `:304`, postgres `:105`, sqlserver `:105`, mysql `:103`, mariadb `:103`, clickhouse `:104`.
- `JoinAliasSqlGenerationTests.Alternating_alias_positional_alias_assigns_slots_in_chain_order` — sqlite `:326`, postgres `:385`, sqlserver `:382`, mysql `:381`, mariadb `:381`, clickhouse `:378`.
- `JoinAliasSqlGenerationTests.Mixed_positional_then_alias_join_matches_the_positional_chain` — sqlite `:105`, postgres `:363`, sqlserver `:360`, mysql `:359`, mariadb `:359`, clickhouse `:356`.

Observed slot-numbering mismatch (postgres representative): alternating mixed chain renders
`select t2.id …` while the equivalent fully positional chain renders `select t4.id …`;
repeated-CLR alias-first chain actual `… join complex_entity as "t3"` vs expected `select t2.id`.
Same ordered-slot shift in every dialect — Defect A (ordinal numbering), not a dialect defect.

## Stale-generator hypothesis — verdict

**DENIED.** Evidence:

1. No compiler-generated source files are persisted to disk (`EmitCompilerGeneratedFiles` is not set,
   `persisted 'generated' dirs found: NONE`); the generator emits in-memory, so "existing generated output
   files" do not exist as `.cs`. The persisted generator artifacts are the generator assemblies.
2. The fresh, clean, `--no-incremental` build recompiles the generator from source. Debug/Release generator
   DLL sha256 after `dotnet clean` + rebuild are **bit-identical** to the pre-clean artifacts:
   - Debug `nextorm.core.sourcegenerator.dll` = `656c7b526e57411b1d08a1fa7001f1d4847591ee2337b5c76135b5de95e1cda3` (pre == post)
   - Release `nextorm.core.sourcegenerator.dll` = `5293322c8e2436eea575a12086be94fef017ba56632d1df1fd5520f6234e2f23` (pre == post)
3. With the generator demonstrably rebuilt from current source, the identical slot-numbering red persists
   (18/1/3×6) on the clean build, with the same dialect-independent `t2` vs `t3`/`t4` shift.
4. A stale lowercase `obj/linux/release/` generator artifact (`5da7c779…`, an unused configuration folder not
   touched by `dotnet clean -c Release`) is present before and after; it is not referenced by the standard
   `-c Release`/`-c Debug` build and cannot explain the red.

Conclusion: the red is **source-caused** (Defect A ordinal numbering + Defect B positional over-preservation),
not caused by a stale generator artifact. Hypothesis status: `confirmed` stale-generator = **no / denied**.

## Artifacts
- `provenance.log`, `post-clean-artifacts.log`, `post-build-artifacts.log`
- `clean-debug.log`, `clean-release.log`, `build-debug-clean.log`, `build-release-clean.log`
- `A-alias.log`, `K-core.log`, `S-{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.log`
- `scope.json`, `validator-brief.log`
