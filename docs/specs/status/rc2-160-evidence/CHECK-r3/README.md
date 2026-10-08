# D160 — final CHECK r3 evidence (fresh deterministic boundary re-run)

- Tree: HEAD `40b1a159` + dirty D160 r=3 changes (branch `1.0.9-rc2`).
- Run started: 2026-10-08T04:30:48Z (CHECK r3, n1/3). Commands run sequentially, no `--filter`.
- All logs in this directory. Exit codes captured from the process; no log truncation.

## Command → exit code → counts

| # | command | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| 1 | `dotnet build nextorm.slnx -c Debug` | 0 | — | — | — | — | `build-debug.log` (Build succeeded, 0 Warning(s), 0 Error(s), 42.84s) |
| 2 | `dotnet build nextorm.slnx -c Release --no-incremental` | 0 | — | — | — | — | `build-release.log` (Build succeeded, 0 Warning(s), 0 Error(s), 46.10s) |
| 3 | `dotnet test tests/nextorm.alias.tests -c Debug` | **2** | 129 | 111 | **18** | 0 | `alias.log` |
| 4 | `dotnet test tests/nextorm.core.tests -c Debug` | **2** | 1776 | 1775 | **1** | 0 | `core.log` |
| 5 | `dotnet test tests/nextorm.sqlite.tests -c Debug` | **2** | 1173 | 1169 | **3** | 1 | `sqlite.log` |
| 6 | `dotnet test tests/nextorm.postgres.tests -c Debug` | **2** | 812 | 809 | **3** | 0 | `postgres.log` |
| 7 | `dotnet test tests/nextorm.sqlserver.tests -c Debug` | **2** | 734 | 731 | **3** | 0 | `sqlserver.log` |
| 8 | `dotnet test tests/nextorm.mysql.tests -c Debug` | **2** | 313 | 310 | **3** | 0 | `mysql.log` |
| 9 | `dotnet test tests/nextorm.mariadb.tests -c Debug` | **2** | 239 | 236 | **3** | 0 | `mariadb.log` |
| 10 | `dotnet test tests/nextorm.clickhouse.tests -c Debug` | **2** | 602 | 599 | **3** | 0 | `clickhouse.log` |
| 11 | `git diff --check` | 0 | — | — | — | — | `git-diff-check.log` (48 lines, only benign LF→CRLF warnings; no whitespace errors) |

**Verdict of this gather: NOT green.** Builds and `git diff --check` pass; every test sweep that was
run is red.

## Failure inventory (32 failing tests total)

### alias (18) — `alias.log`
- `AliasGeneratedSurfaceTests.Generated_projection_builder_pairs_are_public_and_slot_bound`
- `AliasJoinWhereTests.{Where_after_second_alias_join_targets_the_second_joined_slot, Where_between_first_and_second_alias_join_targets_the_first_joined_slot, Where_on_base_before_a_second_alias_join_keeps_the_base_slot}`
- `AliasPlanCacheTests.Alternating_buyer_and_approver_queries_keep_slots_and_the_plan_cache`
- `AliasProjectionShapeTests.{Alias_members_carry_join_slot_attribute_and_resolve_to_the_right_slot, Alias_projection_retains_item_members_and_exposes_alias_members, Generated_new_instance_transitions_win_over_inherited_and_keep_alias_extensions_applicable}`
- `AliasSqliteEndToEndTests.Repeated_joined_type_resolves_to_distinct_slots_and_distinct_sql_aliases`
- `JoinAliasGeneratorDiagnosticTests.{Correlated_apply_alias_emits_builder_and_query_source_seam_overloads, Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot, Root_alias_emits_a_dim1_projection_and_a_withalias_extension}`
- `MixedJoinChainTests.{Correlated_builder_apply_alias_routes_through_the_alias_seam, Correlated_query_apply_alias_routes_through_the_alias_seam, Digit_ending_alias_in_a_non_matching_slot_resolves_to_its_actual_slot, Stored_alias_receiver_supports_alternating_alias_positional_alias_with_repeated_clr_types}`
- `TypedCteAliasTests.{Join_of_two_distinct_same_type_typed_ctes_binds_each_to_its_alias, Shared_two_step_chain_with_entity_and_cte_intermediate_compiles_and_binds_both_branches}`

### core (1) — `core.log`
- `EntityBuilderStateCopyTests.Positional_join_control_pins_the_baseline_modifier_behavior`
  (`EntityBuilderStateCopyTests.cs:191`): asserts a baseline positional join drops
  `_subQueryHint`/`_tag`/`_commandTimeout`; observed SQL/`CommandTimeout` now **preserve** them
  (`MY_HINT(positional)` emitted, `positional-tag` present, `CommandTimeout=42`).

### providers (3 identical per provider × 6) — `{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.log`
- `JoinAliasSqlGenerationTests.Mixed_positional_then_alias_join_matches_the_positional_chain`
- `JoinAliasSqlGenerationTests.Alternating_alias_positional_alias_assigns_slots_in_chain_order`
- `JoinAliasSqlGenerationTests.Repeated_clr_type_resolves_to_distinct_slots`

Characterization (sqlite log): the mixed chain renders `select t2.id …` while the equivalent fully
positional chain renders `select t3.id …` / `select t4.id …` — an ordered-slot shift
(R160-01 mixing parity). Same failure across all six dialects.

## Delta vs the D160.3-5 claimed-green evidence

| suite | D160.3-5 (2026-10-07T22:23Z) | this CHECK r3 |
|---|---|---|
| alias boundary | total 129 / failed 0 | total 129 / failed 18 |
| core boundary | total 1775 / failed 0 | total 1776 / failed 1 |
| sqlite join-alias | 20 / failed 0 | full project 1173 / failed 3 |
| postgres join-alias | 22 / failed 0 | full project 812 / failed 3 |

The current tree is red on the same suites that the DO ledger reports green; note also the core
suite gained one test (1775 → 1776). No product source was edited by this gather — logs only.
