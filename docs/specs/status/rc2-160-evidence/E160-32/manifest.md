# E160-32 — manifest (F3: valid positions + invalid `ItemN` diagnostics)

- row_id: **E160-32**
- requirement_ids: **R160-04, R160-11** (per `P:D160-r3`)
- contract_revision: **rv=4**
- plan_revision: **r=3**; attempt: **n=1/3**; cycle: **1**; tree: `40b1a159+dirty`
- owner: **`coder` creates execution evidence; `check` independently matches row/result/artifact.**
- defect_key: **F3** (`ItemN` parser reconciliation). Applied fixes of F3: **1** (`D160.3-3`). No new defect key, no new diagnostic id. No F1/C1 recurrence observed.

## Source paths
- `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` — `IsItemAlias` now rejects the whole structural shape `Item` + one-or-more digits regardless of arity; both call sites (root-alias `:657`, join-chain `ValidateChain` `:884`) drop the arity argument; every rejected name routes to the existing **NORMGEN002** (`AliasCollision`). `ProjectionAliasCache` intentionally unchanged.
- `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`
- `tests/nextorm.alias.tests/AliasProjectionShapeTests.cs`

## Exact commands (argument arrays) and results
| # | command (arg array) | phase | exit | selected | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|---|
| 1 | `["dotnet","build","tests/nextorm.alias.tests","-c","Debug"]` (F3 reverted) | inner | 0 | 1 | — | — | — | `D160.3-3/red-build.log` |
| 2 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasGeneratorDiagnosticTests"]` (F3 reverted) | inner | **2** | 28 | 23 | **5** | 0 | `D160.3-3/red-filtered.log` |
| 3 | `["dotnet","build","tests/nextorm.alias.tests","-c","Debug"]` (post-fix) | inner | 0 | 1 | 0 warnings / 0 errors | — | — | `D160.3-3/fix-build.log` |
| 4 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasGeneratorDiagnosticTests"]` | inner | 0 | 28 | 28 | 0 | 0 | `D160.3-3/fix-diagnostic-filtered.log` |
| 5 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~AliasProjectionShapeTests"]` | inner | 0 | 5 | 5 | 0 | 0 | `D160.3-3/fix-shape-filtered.log` |
| 6 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug"]` | boundary | 0 | 118 | 118 | 0 | 0 | `D160.3-3/alias-boundary.log` |
| 7 | `["dotnet","test","tests/nextorm.core.tests","-c","Debug"]` | boundary | 0 | 1774 | 1774 | 0 | 0 | `D160.3-3/core-boundary.log` |
| 8 | `["dotnet","build","nextorm.slnx","-c","Debug"]` | boundary | 0 | 1 | 0 warnings / 0 errors | — | — | `D160.3-3/solution-build.log` |
| 9 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~JoinAliasGeneratorDiagnosticTests"]` (r=3 closure) | inner | 0 | 31 | 31 | 0 | 0 | `D160.3-5/JoinAliasGeneratorDiagnosticTests.log` |
| 10 | `["dotnet","test","tests/nextorm.alias.tests","-c","Debug"]` (r=3 closure) | boundary | 0 | 129 | 129 | 0 | 0 | `D160.3-5/alias-boundary.log` |
| 11 | `["dotnet","build","nextorm.slnx","-c","Debug"]` / `["dotnet","build","nextorm.slnx","-c","Release","--no-incremental"]` (part 1) | boundary | 0 | 1 | 0 warnings / 0 errors | — | — | `E160-07/build.log`, `E160-07/release-build.log` |
| 12 | `["python3","/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py","…"]` (part 1) | boundary | 0 | 1 | — | — | — | `E160-12/validator-report.log` |

## Executed test identities (class.method)
Invalid `ItemN` (positive rejection → NORMGEN002, generated C# well-formed):
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Out_of_range_item_join_alias_reports_NORMGEN002(string name)` — `Item0`, `Item9`
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Item_alias_at_arity_plus_one_reports_NORMGEN002` — `Item3`
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Out_of_range_item_root_alias_reports_NORMGEN002(string name)` — `Item0`, `Item2` (root)
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Non_numeric_item_names_stay_valid_join_aliases(string name)` — `Item`, `Items`, `ItemX`, `Buyer2` boundary
Valid positions (must remain diagnostic-free and compile):
- `NextORM.AliasTests.JoinAliasGeneratorDiagnosticTests.Valid_in_range_positions_still_compile_without_diagnostics` (+ `AssertGeneratedSourceIsWellFormed` parse check)
- `NextORM.AliasTests.AliasProjectionShapeTests.Alias_members_carry_join_slot_attribute_and_resolve_to_the_right_slot`
- `NextORM.AliasTests.AliasProjectionShapeTests` full 5/5 (well-formed generated shapes)

## Row → artifact mapping (cross-reference)
- New row E160-32 → `D160.3-3/` (primary red→green) + `D160.3-5/` (r=3 closure) + part-1 `E160-07/`.
- Refreshed rows: E160-03 (dim-1/valid positions), E160-05 (naming/diagnostics), E160-22 (collision diagnostics), E160-29 (aliases/diagnostic classification).

## Exit / result
Red→green proven: with F3 reverted the new out-of-range cases fail (exit **2**, selected 28, failed **5** = Item0 ×2, Item2 root, Item3, Item9); with the fix the same filter is **28/28 exit 0** and the r=3 closure is **31/31 exit 0**. No malformed generated C# (zero syntax errors). NORMGEN001–008 registry unchanged (no new id). No F1/C1 recurrence.
