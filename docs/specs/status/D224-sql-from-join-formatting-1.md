---
task_id: T224
issue: 224
title: "SQL: форматирование — `from`/`join` с новой строки"
selected_variant: pdca-dotnet
collection: docs/specs/status/collection-1.0.9-todo.md
group: G1
cycle: 1
plan_revision: 1
plan_state: ready
evidence_contract: rv1
status: planned
phase: PLAN
created: 2026-10-13
---

# D224 — PLAN r=1 (frozen)

## Goal
Generated SELECT SQL exposes source/join boundaries consistently and uses platform-independent line separators, preserving query semantics, parameters, dialect capabilities and cache behaviour.

## Formatting contract (decided)
- Unconditional DEFAULT formatting (spec Q1): opt-in flag is a non-goal.
- In SELECT text mode (`!ParamMode`, `SqlBuilder.cs:62`) emit `\n` before `FROM` and before each `JOIN`, retaining one leading separator space before those keywords; preserve keyword casing and trailing spacing.
- Apply recursively to generated nested SELECTs, including derived-table FROM, CTE and insert-select; no nesting indentation layer and no added blank lines (Q3).
- Global newline contract (Q2/Q5): replace library-generated `AppendLine()`/provider platform newlines with explicit LF (`'\n'`) throughout SQL emission, including existing clauses (`SqlBuilder.cs:224,267,307,360,391,448,820,1317`), `SqlServerDialect.cs:644`, `ClickHouseExtremeRowRenderer.cs:86-118`, and batch separators; retain clause spacing and batch-option meaning. Do not parse/reflow opaque raw SQL or alter literal/parameter contents.

## Acceptance criteria (each with a negative case)
- R1: 0/1/many-join SELECTs and nested forms match new raw goldens. Negative: inline FROM/JOIN, duplicate separators, accidental indentation or an opt-in dependency fails.
- R2: generated contract `CommandText` contains no `\r`, and the emitter audit proves no platform-dependent separators remain. Negative: Linux-only success while a Windows emitter still uses CRLF fails.
- R3: row results, parameters, null/default semantics, dialect keywords and shared-command cache state remain unchanged. Negative: changed predicates, bindings, JOIN capabilities, sticky cache flags or altered opaque inputs fails.
- R4: regenerated exact-string assertions and all eight signed D160 pairs agree byte-for-byte. Negative: weakened comparisons or a stale hash fails.
- R5: public EN/RU examples and the layout note agree with the contract. Negative: stale example, deleted-page resurrection or unrelated spec edit fails.
- R6: build has zero warnings/errors; coverage line >=85% and branch >=75%. Negative: either threshold missed fails.
- R7: every applicable variant/provider obligation executes. Negative: skipped required provider or zero selected tests fails.

## Variant matrix (closed)
- SELECT shape — 0/1/many joins, derived-table FROM, CTE, correlated/nested subquery: test.
- Text vs parameter-only mode: test + guard.
- Null/default inputs, value/reference projections: test existing semantics + guard no new branching.
- Dialects SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse: test each.
- ClickHouse + SQL Server JOIN/APPLY overrides: test + guard unchanged keyword contract.
- External (publicextensibility) dialect seam: test existing seam.
- Batch and INSERT/CTAS/MERGE containing SELECT: test recursive layout + LF.
- Standalone DELETE-join / UPDATE-join: test existing layout + LF, guard against imposing SELECT clause placement; changing their clause layout is deferred (trigger: separately approved DML-layout requirement).

## Unit execution mode
One sequential unit in the existing tree with a shared writer/newline/test contract; no worktrees/branches/commits/merges for execution. All D-tasks are fix now when implementation is authorised.

## Task list (D-tasks; executed only after DO authorisation)
- D1: inventory SQL newline emitters via Roslyn + permitted literal/comment searches; resolve existing test owners/selectors; capture pre-change outputs/parameters; inspect baseline Git attributes; coordinate overlapping collection edits. Produce scoped runner, `scope.json`, assertion map, emitter inventory.
- D2: change SELECT source/join emission at `SqlBuilder.cs:151,186`; replace SQL-emitter platform newlines incl. `SqlBuilder.cs:224,267,307,360,391,448,820,1317`, `SqlServerDialect.cs:644`, `ClickHouseExtremeRowRenderer.cs:86-118`; preserve parameter-only and dialect keyword paths.
- D3: add raw-layout/no-CR golden cases to existing SQL-generation suites; regenerate affected literal assertions and D160 pairs; verify unchanged standalone-DML/keyword expectations and semantic/cache guards. Never replace exact assertions with whitespace collapsing.
- D4: update current public EN/RU SQL examples and add the unconditional-LF/no-indent layout note to `docs/advanced/limitations.md` + RU counterpart; clarify in both `23-sql-batch.md` pages that multiline batching controls separation, not clause-layout opt-in.
- D5: validate scope, perform the single boundary evidence sweep, write/validate the manifest and hand off to CHECK; do not declare completion from an implementation report.

## Write footprint (blast radius)
- Product: `src/nextorm.core/DataContext/SqlBuilder.cs`; SQL-newline emission sites identified by D1 across `src/nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse,entityframeworkcore}/**`; the supplied SQL Server/ClickHouse sites; audit `SqlSourceRenderer.cs:233` and dialect JOIN overrides. Do not change keyword APIs. Add narrowly scoped `.gitattributes` LF rules only if required for D160 files.
- Tests (paths relative to `tests/nextorm.<project>.tests/`; regenerate SELECT expectations / add statement-level guards — not indiscriminate rewriting):
  - alias: `RootAliasTests.cs`
  - core: `JoinIntoOneToOneTests.cs`, `RootProjectionTests.cs`, `TypedCteTests.cs`, `JoinAliasMixingSeamTests.cs`
  - entityframeworkcore: `AddNextOrmFromDbContextTests.cs`, `CreateNextOrmContextTests.cs`, `NextOrmModelMapperTests.cs`, `UseNextOrmTests.cs`
  - integration: `DynamicColumnsMariaDbContainerTests.cs` + Roslyn-resolved common/provider SQL-semantic owners
  - clickhouse: `CreateTableAsSqlGenerationTests.cs`, `InsertSqlGenerationTests.cs`, `JoinIntoSqlGenerationTests.cs`, `SourceOverrideSqlGenerationTests.cs`, `SqlGenerationTests.cs`
  - mariadb: `CreateTableAsSqlGenerationTests.cs`, `DeleteSqlGenerationTests.cs`, `ExtremeRowPublicConstructionTests.cs`, `SourceOverrideSqlGenerationTests.cs`, `SqlGenerationTests.cs`, `UpdateJoinSqlGenerationTests.cs`
  - mysql: `CreateTableAsSqlGenerationTests.cs`, `DeleteSqlGenerationTests.cs`, `InsertSqlGenerationTests.cs`, `SourceOverrideSqlGenerationTests.cs`, `SqlGenerationTests.cs`, `UpdateJoinSqlGenerationTests.cs`
  - postgres: `BatchSqlGenerationTests.cs`, `ColumnCollationSqlGenerationTests.cs`, `CreateTableAsSqlGenerationTests.cs`, `DeleteSqlGenerationTests.cs`, `ExtremeRowNativeSqlGenerationTests.cs`, `InsertSqlGenerationTests.cs`, `MergeSqlGenerationTests.cs`, `SourceOverrideSqlGenerationTests.cs`, `SqlGenerationTests.cs`, `StringSemanticsSqlGenerationTests.cs`, `UpdateJoinSqlGenerationTests.cs`, `UpdateSqlGenerationTests.cs`
  - sqlite: `BatchSqlGenerationTests.cs`, `CorrelatedQueryTests.cs`, `CreateTableAsSqlGenerationTests.cs`, `CsvStreamTests.cs`, `DeleteSqlGenerationTests.cs`, `DictionaryLookupSqlGenerationTests.cs`, `DynamicColumnsTests.cs`, `InsertSqlGenerationTests.cs`, `JoinIntoOneToOneSqlGenerationTests.cs`, `JoinIntoSqlGenerationTests.cs`, `LobSqlGenerationTests.cs`, `QueryFilterSqlGenerationTests.cs`, `SourceOverrideSqlGenerationTests.cs`, `SqlGenerationTests.cs`, `ToDataReaderSqliteTests.cs`, `UpdateJoinSqlGenerationTests.cs`, `SqliteFts5MaintenanceSqlGenerationTests.cs`, `PositionalChainSqlInvariantTests.cs`
  - sqlserver: `BatchSqlGenerationTests.cs`, `CreateTableAsSqlGenerationTests.cs`, `DeleteSqlGenerationTests.cs`, `InsertSqlGenerationTests.cs`, `MergeSqlGenerationTests.cs`, `SourceOverrideSqlGenerationTests.cs`, `SqlGenerationTests.cs`, `UpdateJoinSqlGenerationTests.cs`, `UpdateSqlGenerationTests.cs`
  - Additional guards: provider `*DialectTests.cs` keyword assertions stay unchanged unless an audited assertion covers a real newline emitter; retain whitespace-collapsing JoinInto suites; re-verify `publicextensibility/ExternalDialectExtensibilityTests.cs`, relevant `clickhouse.extensions` owners, existing core InMemoryTests.
- Baselines: `tests/nextorm.sqlite.tests/Baselines/D160/positional/sqlite/` — 8 pairs `01-inner,02-left,03-right,04-full,05-cross,06-three-join-chain,07-where-between-joins,08-positional-prefix-as` (`.sql` + `.sha256`); regenerate UTF-8 raw bytes + uppercase SHA256 sidecar `<HEX>  <name>.sql\n`; generator/compare `tests/nextorm.sqlite.tests/PositionalChainSqlInvariantTests.cs:64-73,79-96`; do not change D160 test identity.
- Docs: all current handwritten public SQL examples under `docs/**` and `docs/ru/**` incl. guides, scalar-functions, advanced, getting-started and current restructured public dirs; add layout note to `docs/advanced/limitations.md` + RU. Include untracked public pages without staging/overwriting sibling work; do not restore deleted pages; do not touch generated API/site output or `docs/specs/**` except this status file. Source/docs stay CRLF; byte-exact SQL/hash artifacts intentionally LF.

## Test strategy
- `test scope`: `{projects:[tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse,clickhouse.extensions,entityframeworkcore,publicextensibility,alias,integration}.tests], selectors:[nonempty exact FQN filters resolved by Roslyn from the listed files and relevant shared/provider/InMemory owners], files:[the above test files plus D1-resolved owners], rebuild:"affected", boundary:"once after D2-D4, immediately before DO->CHECK", rationale:"writer/LF blast radius, dialect parity, raw baselines and unchanged execution"}`. D1 materialises concrete selectors before execution; no whole-project/solution selectors. Validate with `python3 scripts/validate_inner_loop.py brief artifacts/pdca/D224/rv1/scope.json`.
- SQL-generation/unit goldens establish bytes; existing in-memory and result-based integration establish semantics. Required integration: SQLite, PostgreSQL, SQL Server, MySQL, ClickHouse and the affected MariaDB container case; load `.opencode/skills/running-integration-tests/SKILL.md`, use `DOCKER_HOST`, recover Podman when necessary, require nonzero executed cases / no required skips.
- Coverage: `coverage.settings.xml` (modules `nextorm.(core|sqlite|postgres|sqlserver)`), both thresholds.

## Perf decision (with argument)
Acceptance benchmark NOT required for this frozen scope: only SQL-emitter spacing/newline operations change, not planning/cache decisions, executor/materialisation or in-memory algorithms; formatting runs at preparation/plan-cache miss (`QueryPlanner.cs:663-665`), not on cache-hit execution (`:739+`) or per row. No production reflow/normalisation pass. Trigger DO->PLAN if the audit requires changing those algorithms or the execution frequency changes.

## Reconnaissance decision
No design spike: spec Q1-Q5 plus the existing clause convention settle the design; D1 is a finite inventory/baseline-verification step.

## Design checklist outcome
SOLID/DRY: preserve writer/dialect responsibilities, reuse existing casing. Types/nullability: no API/type changes. Perf: append-only preparation work. Risks: snapshot volume (~697 exact-string asserts), embedded SELECTs, CRLF checkout, concurrent docs edits. Assumption (explicit): literal/raw SQL content is excluded from reflow — this is a semantics-preserving assumption, not licence to retain platform-generated CRLF.

## Evidence contract (frozen rv=1)
`required_rows=[E01,E02,E03,E04,E05,E06,E07]`; manifest path `artifacts/pdca/D224/rv1/manifest.json` (written at DO->CHECK; validated with `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D224/rv1/manifest.json`). Let `A=artifacts/pdca/D224/rv1`; runner `A/run-evidence.sh` resolves scoped filters and runs `dotnet test <project> -c Debug --no-build --filter "<resolved filter>"`; each row writes `A/<row>.log` and `A/<row>.json` (commands, exits, counts, outcomes, source/artifact paths). Required exit 0, stated predicates true, logs retained.
- E01 -> R6; owner D5: `dotnet build nextorm.slnx -c Debug`; zero warnings/errors.
- E02 -> R1,R2,R3,R7; owner D3: `bash artifacts/pdca/D224/rv1/run-evidence.sh sql`; full variant/dialect matrix; no generated CR.
- E03 -> R4; owner D3: `bash artifacts/pdca/D224/rv1/run-evidence.sh baselines`; eight raw byte comparisons + recomputed SHA256.
- E04 -> R3,R7; owner D3: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock bash artifacts/pdca/D224/rv1/run-evidence.sh integration`; listed providers executed, no required skip.
- E05 -> R6; owner D5: `dotnet-coverage collect -s coverage.settings.xml -f cobertura -o artifacts/pdca/D224/rv1/coverage.cobertura.xml "bash artifacts/pdca/D224/rv1/run-evidence.sh coverage"`; line >=85%, branch >=75%.
- E06 -> R5; owner D4: `bash artifacts/pdca/D224/rv1/run-evidence.sh docs`; no stale affected example, unintended deletion/restoration or unrelated spec edit.
- E07 -> R2,R3; owner D2: `bash artifacts/pdca/D224/rv1/run-evidence.sh audit`; Roslyn-backed emitter inventory + reviewed diff proving LF emission, unchanged opaque contents/parameter-only path/keyword ownership/cache flags.
- CHECK opens the manifest first; re-gather budget two targeted rounds (owner CHECK, executor coder/scout); absence of evidence is not product FAIL.

## Priorities / sibling & collection requirements
- E02/E04/E07 are P1 (default-layout/LF invariants; nextorm sibling-form / per-dialect / each-provider / applicable nullable rows). E03 P1 by byte-exact specification.
- Preserve SQL-writer semantics and shared-command cache flags; prohibit global whitespace-collapsing that weakens unrelated assertions; serialize overlapping writer/test/docs edits with sibling tasks in the single tree.
- Collection ALL-barrier: persist this ready plan; DO does not start at this PLAN-only boundary. Next allowed step after ALL-barrier re-pass: re-cluster, then resume at T215 DO.

## Progress log
- 2026-10-13: PLAN r=1 frozen by planner; plan_state=ready; evidence contract rv=1 frozen (first freeze, no supersession). Status file created by coder at PLAN->DO boundary. DO not started (implementation not authorised).
