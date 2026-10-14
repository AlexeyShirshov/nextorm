---
task: T218 (#218)
title: SQL Server sequences (NEXT VALUE FOR) — missing cross-provider support
issue: https://github.com/AlexeyShirshov/nextorm/issues/218
milestone: 1.0.9
collection: 1.0.9-todo
group: G1 (single group; no worktree/branch/merge)
cycle: N=1
plan_revision: r=1
iteration: n=1
plan_state: ready
evidence_contract_rv: 1
manifest: artifacts/pdca/D218-sqlserver-sequences/rv1/manifest.json
selected_variant: pdca-dotnet
base_commit: 58025147
---

# T218 PLAN r=1 — SQL Server sequences (NEXT VALUE FOR)

## Goal
Add a SQL Server sequence-expression surface through the existing provider-specific scalar-function
surface, without DDL and without a portable API. Consume issue requirement: `SqlFunctions.SqlServer`
gains `next_value_for` (+ `nextval` alias) rendering `NEXT VALUE FOR`.

## Acceptance criteria
| ID | Positive | Negative |
|---|---|---|
| R1 | `SqlServerFunctions.next_value_for(string)` and `nextval(string)` return CLR `long`; both translate to the same `NEXT VALUE FOR` | Markers are not evaluated client-side; unsupported result types not declared supported |
| R2 | SQL Server supports both names; existing PostgreSQL/MariaDB sequence API behaviour unchanged | SQL Server markers rejected by PostgreSQL, MariaDB, MySQL, SQLite, ClickHouse and in-memory before argument traversal |
| R3 | Name is a string constant, one or two nonempty components; each component quoted separately | `null`, empty components, 3 components, row-dependent and captured non-constant expressions rejected; name string never becomes a SQL parameter |
| R4 | Generation uses `NEXT VALUE FOR [name]` or `NEXT VALUE FOR [schema].[name]`; `]` escaped as `]]` | SQL-like name content cannot escape the quoted identifier |
| R5 | Real SQL Server BIGINT sequence: both aliases, consecutive values, same-sequence repeated in one row, `long` boundaries | Exhausted `NO CYCLE` sequence errors, not wraparound/default; same sequence in one row returns the same value |
| R6 | SELECT projection supported; no DDL, portable API, `OVER`, or extension of other sequence/session functions | Prohibited SQL Server positions not declared supported; server error asserted, no client fallback |
| R7 | Bilingual docs, registries, build/tests, coverage and performance evidence complete | PASS forbidden while a required evidence row is open or a required provider is skipped |

## Capability / design decision (overlay invariant 1)
Chosen: existing nullable `ISqlDialect.SqlServerFunctions` + per-name `Supports` — NO new
`ISqlDialect` flag and NO change to `SqlDialectBase`/`ISqlDialect`.
Refs: `ISqlDialect.cs:533`; `SqlServerScalarFunctionTranslator.cs:88,93-94,116`;
`SqlServerDialect.cs:966,971,993`; `SqlFunctions.SqlServer.cs:15,144`.
Rejected: new `ISqlDialect.SupportsSequences` (duplicate capability, one consumer).
Deferred: portable sequence abstraction (until a second agreed consumer).

## Identifier quoting (licensed)
Split the raw constant on `.` into 1–2 nonempty components; quote each with the SQL Server
identifier quoter (`SqlServerDialect.cs:171`, `[name]` with `]]` escaping), join with `.`.
Do NOT quote a full `schema.name` in one call. Compose schema/name separately, consistent with
`MakeQualifiedTableName:183` / `JoinTableQualifier:433`. Reject NUL; bound each component length.
Input is raw names, not a ready T-SQL parser. Constant capture via `Visitor/SqlLiteral.cs:27`
(`TryGetConstantString`).

## What the statement did not say (closed)
- Captured names: minimal boundary is a literal string; captured/default/non-constant expressions are guard-tested, not arbitrary expression evaluation.
- Other sequence numeric types: BIGINT only; no catalog introspection.
- SQL positions: SQL Server restrictions preserved; no new general position validator.
- Registries/tracking: D0 fixes the verified T218↔issue↔milestone link.

## D-units (fix now, order D0→D5, one execution unit)
- **D0 — freeze + baseline.** Write plan/contract/scope brief/source identity; run acceptance baseline; add an isolated preparation control benchmark on existing `patindex` (planned category `t218-preparation`) and take its baseline BEFORE D1.
- **D1 — API and translation.** `src/nextorm.core/Query/SqlFunctions.SqlServer.cs:15,144`; `src/nextorm.core/Visitors/SqlServerScalarFunctionTranslator.cs:88,93-94,116`; `src/nextorm.sqlserver/SqlServerDialect.cs:966,971,993`. Add both markers / name-set / Supports / render; identifier capture before normal parameter-mode argument traversal. Use the established constant-string mechanism (`Visitor/SqlLiteral.cs:27`).
- **D2 — SQL generation and guards.** Extend existing core/provider test suites. `src/nextorm.core/Visitors/InMemoryScalarFunctionRewriter.cs:155-156` — keep and verify the existing guard (no production edit needed). `NormSqlTranslator.cs:453` — dispatch already sufficient, DO NOT change.
- **D3 — DB execution.** Add SQL Server-only integration scenarios in the SQL Server-specific suite: unique sequence names, BIGINT fixture DDL test-only, cleanup in `finally`, positive/negative R5–R6 cases.
- **D4 — docs and registries (CORRECTED: update existing pages only).** Update:
  `docs/scalar-functions/10-sequences.md`, `docs/ru/scalar-functions/10-sequences.md`,
  `docs/guide/provider-specific-sql/sqlserver.md`, `docs/ru/guide/provider-specific-sql/sqlserver.md`
  (replace “SQL Server — Not supported yet”/RU equivalent with “Supported”, documenting functionality, limitations, examples; keep #218 as tracking reference only), and
  `docs/specs/roadmap/sql-function-coverage-gap.md`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
  `docs/specs/comparison/evidence-01-provider-specific-features.md`.
  DO NOT create `docs/articles/sqlserver-sequences.md`, RU counterpart, page 13, new TOC entries, or renumber. No public docs link to `docs/specs/**`.
- **D5 — evidence closure.** Boundary suites, coverage, acceptance + targeted preparation post-run; baseline comparison; brief/report/manifest validation and DO→CHECK freeze.

## Deferred (with trigger)
Other numeric types — agreed type-support request; `currval/lastval/setval/scope_identity` — separate semantic spec; Oracle — a consumer appears; DDL/portable API — standalone scope; unrestricted positions/`OVER`, captured names, and names with a literal dot inside one component — separate agreed scenario; production `CYCLE` sequences — separate lifecycle scenario (fixtures explicitly `NO CYCLE`). No deferred capability is claimed supported.

## Variant matrix
(tests: SQL-gen + DB)
- both aliases × bare/schema-qualified name → test
- `null`/`default(string)`, empty, empty component, >2 components, >128/component, NUL → guard + test
- `]`, spaces, SQL-like content inside a raw component → test (quoting/escaping, no injection)
- row-dependent / captured / non-constant expression → guard + test
- CLR `long`, BIGINT, MIN/MAX, exhaustion `NO CYCLE` → test (DB)
- other value numeric types; nullable result → deferred (separate type-support request)
- reference-type result → guard (public markers return only `long`)
- capability present + Supports true → test
- capability null / Supports false; other providers + in-memory → guard + test (early rejection)
- PostgreSQL/MariaDB existing sequence functions → test (regression boundary)
- one reference / two references in a row / multiple rows → test (DB semantics)
- prohibited SQL position → test (expected server rejection)
- `OVER`, unrestricted positions, DDL/portable API → deferred
- cache/default preparation control → test/benchmark (sticky query-command flags unchanged)
- branch delta + mutation plan: enumerate added/changed branches after D1, map each to test/guard; mutate (remove provider gate, corrupt `NEXT VALUE FOR` render, break alias dispatch, bypass name guarding) — each targeted suite must fail, then restore and rerun green.

## Test strategy
Unit/SQL-gen verify recognition, SQL and early guards; container-backed integration verifies values and server restrictions. Coverage gate 85% line / 75% branch (hard-fail on `main`, warning on this branch). Boundary sweep at DO→CHECK.

## Priority
E01–E07 P1 by R1–R7 and execution path; E00/E08–E10 P1 as mandatory process/performance/evidence gates. CHECK may not downgrade.

## Performance decision (argument)
Required: `SqlServerScalarFunctionTranslator.cs:88,116` is on the query-preparation path, not one-time init. Baseline/post: acceptance suite + targeted preparation control; new benchmark additionally measures both aliases and 1/2 components. Control (unchanged `patindex`): mean ≤105% baseline and allocations not above baseline; noise/incomparability → unresolved evidence, not silent PASS. New sequence cases get absolute time/allocation report (no invented pre-feature baseline). Acceptance run: exactly **7** original cases; the new benchmark does NOT get category `acceptance`. Neither `--list` nor `--job Dry` is gate evidence.

## Reconnaissance decision (argument)
Closed: supplied capability/dispatch/quoting/constant-capture + MS Learn semantics (`NEXT VALUE FOR` syntax/placement, per-row generation, same-sequence-same-row) are sufficient. No new spike for the design decision; runtime proof lives in D3.

## Unit execution mode
Sequential in one tree: API, translator, dialect, tests and evidence overlap by contract. No worktrees.

## Evidence contract rv=1 (pinned before DO)
`status_file: docs/specs/status/D218-sqlserver-sequences-1.md`
`manifest: artifacts/pdca/D218-sqlserver-sequences/rv1/manifest.json`

Manifest schema (frozen at DO→CHECK, bound to commit+rv, append-only during CHECK): keys `task`,
`tree`, `contract_rv`, `status_file`, unique `required_rows`, `rows[]` where each row = `id`,
`status: met|open|na`; `met`→`evidence[]{kind,path,command?,exit?,value?}`; `open`→`reason`;
`na`→observed `predicate`. No PASS while any `required_rows` entry is `open`.
Validate with `python3 scripts/validate_inner_loop.py manifest <path>`.

`required_rows = ["E00","E01","E02","E03","E04","E05","E06","E07","E08","E09","E10"]`
- E00 / R0 — Gate-1 freeze, footprint/tracking; status + scope brief (C0).
- E01 / R1 — markers/`long`, three registration points; source review + build (C1).
- E02 / R1,R4 — both aliases, bare/schema SQL; executed SQL-gen assertions (C2).
- E03 / R2 — capability guards of all target providers; PG/MariaDB regression (C3+C4).
- E04 / R3,R4 — name/default/row/captured/escaping matrix; executed assertions (C2+C3).
- E05 / R5 — BIGINT values, both names/aliases, per-row semantics, boundaries; DB evidence (C5).
- E06 / R6 — scope exclusions, prohibited position, exhaustion; review + expected-error tests (C2+C5).
- E07 / R7 — EN/RU docs, registries, no specs links, CRLF/invariants (C6).
- E08 / R7 — acceptance 7 cases baseline/post + preparation comparison (C7+C8 before/after D1).
- E09 / R7 — coverage of included assemblies + branch policy (C9a+C9b+C9c).
- E10 / R0,R7 — source/artifact revision binding, complete report/manifest (C10).

CHECK re-gather budget: at most **2** targeted rounds, owner **check**. Missing report → re-gather (no product FAIL / no new DO attempt). Exhausted + open → unresolved, list IDs, PASS forbidden. Completing evidence does not bump rv.

### Exact commands
- C0 `python3 scripts/validate_inner_loop.py brief artifacts/pdca/D218-sqlserver-sequences/rv1/brief.json`
- C1 `dotnet build -c Debug`
- C2 `dotnet test tests/nextorm.sqlserver.tests -c Debug --verbosity normal`
- C3 `dotnet test tests/nextorm.core.tests -c Debug --verbosity normal`
- C4 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test -c Debug --verbosity normal`
- C5 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`
- C6 `git diff --check` and `dotnet docfx docs/docfx.json`
- C7 `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
- C8 `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=t218-preparation` (category created in D0)
- C9a `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
- C9b `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`
- C9c `cp -a tests/coverage artifacts/pdca/D218-sqlserver-sequences/rv1/coverage`
- C10 `python3 scripts/validate_inner_loop.py report artifacts/pdca/D218-sqlserver-sequences/rv1/report.json` and `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D218-sqlserver-sequences/rv1/manifest.json`

## Risks
SQL Server position restrictions; benchmark noise; fixture cleanup; container availability; warning-only coverage off `main`. None weakens R1–R7 or E00–E10.

## Planned write-footprint
Production:
- `src/nextorm.core/Query/SqlFunctions.SqlServer.cs` — NEW public APIs (both aliases), `long`
- `src/nextorm.core/Visitors/SqlServerScalarFunctionTranslator.cs`
- `src/nextorm.sqlserver/SqlServerDialect.cs` (ISqlServerFunctions impl `:966`, quoting `:171`)
- `src/nextorm.core/Visitors/NormSqlTranslator.cs` — only if dispatch requires (recon says sufficient; expected NO change)
- `src/nextorm.core/Visitors/InMemoryScalarFunctionRewriter.cs` — verify existing guard; expected NO change
- NOT touched: `SqlDialectBase.cs`, `ISqlDialect.cs`, `Builders/**`, shared `QueryCommand` state, CPM, other providers' implementations.
Tests (extend existing suites + new SQL-gen tests): `tests/nextorm.core.tests`, `tests/nextorm.sqlserver.tests`, `tests/nextorm.postgres.tests`, `tests/nextorm.mariadb.tests`, `tests/nextorm.mysql.tests`, `tests/nextorm.sqlite.tests`, `tests/nextorm.clickhouse.tests`, `tests/nextorm.integration.tests` (SQL Server-specific).
Docs: the D4 list above. Plus this status file and (in DO) evidence artifacts.

**Public-API additions (flag for later T53 freeze):** `SqlFunctions.SqlServer.next_value_for(string)`,
`SqlFunctions.SqlServer.nextval(string)` (both → CLR `long`). No other public surface change.

## Requirements on sibling results (shared G1 files)
Exclusive writes to dialect contracts/base, `SqlFunctions.SqlServer.cs`, dispatch and SQL Server provider files during this lane. Other G1 tasks must preserve the per-name `Supports` semantics and both markers. T218 touches no `Builders/**`, shared `QueryCommand` state, CPM or other providers. Freeze sibling production writes during baseline/post measurements (E08). T53 consumes the recorded NEW public API list.

## DO→PLAN classification
None now (still initial PLAN). A newly proven prerequisite keeps the original D active, blocked on the dependency; missing evidence first goes to the bounded gather.

---

## Progress log

2026-10-14T03:20:00Z | DO | revision r=1 | iteration 1/3 | DO started — scope brief written and validated (fail-closed) before any edit; D0-D5 executed as one unit in the current worktree (no worktree/branch/merge) | artifacts/pdca/D218-sqlserver-sequences/rv1/brief.json (`validate_inner_loop.py brief` exit 0)

2026-10-14T03:22:00Z | DO | revision r=1 | iteration 1/3 | D0 freeze + baselines: source identity captured; `dotnet build nextorm.slnx -c Debug` = 0 Warning(s)/0 Error(s); acceptance PRE baseline (7 cases, exit 0); `t218-preparation` control benchmark added on the unchanged `patindex` path (NOT `acceptance`) with its own PRE baseline (879.5 us / 784.39 KB, exit 0) | artifacts/pdca/D218-sqlserver-sequences/rv1/{source-identity.txt,logs/d0-build.log,logs/d0-acceptance-baseline.log,logs/d0-t218-preparation-baseline.log}; benchmarks/nextorm.benchmark/T218PreparationControlBenchmark.cs

2026-10-14T03:30:00Z | DO | revision r=1 | iteration 1/3 | D1 API + translation: `SqlFunctions.SqlServer.next_value_for(string)`/`nextval(string)` both CLR `long` (`SqlFunctions.SqlServer.cs:294,301`); translator name-set (`SqlServerScalarFunctionTranslator.cs:60`), `SequenceFunctions` guard set (`:90-96`), sequence branch before argument traversal (`:109-111`), `EmitSequence` (`:169`) + `QuoteSequenceName` (`:192`) — constant capture via `SqlLiteral.TryGetConstantString`, 1-2 nonempty components, per-component `QuoteIdentifier`, NUL/empty/>128/>2 rejection, param-mode-safe (name never a parameter); dialect `Supports` (`SqlServerDialect.cs:1045`) + `Render` (`:1130`). `NormSqlTranslator.cs:453` dispatch and `InMemoryScalarFunctionRewriter.cs:155-156` guard verified sufficient — NO change (as the plan expected) | src/nextorm.core/Query/SqlFunctions.SqlServer.cs; src/nextorm.core/Visitors/SqlServerScalarFunctionTranslator.cs; src/nextorm.sqlserver/SqlServerDialect.cs; logs/inner-build-*.log (7 projects, each 0/0)

2026-10-14T03:38:00Z | DO | revision r=1 | iteration 1/3 | D2 SQL generation + guards: 7 new SQL-gen/guard test cases in `SqlServerFunctionsSqlGenerationTests` (both aliases, schema-qualified components, `]`→`]]` escaping, null/empty/empty-component/>2/129-char guards, 128-char boundary accepted, captured/row-dependent rejection, no `@p` parameter, `long` return type, `Supports`/`Render`); in-memory rejection added to `InMemoryScalarFunctionsTests`; rejection rows added to the postgres/mysql/mariadb/clickhouse rejection suites and the sqlite suite. Affected filtered runs (all exit 0, 0 failed, 0 skipped): core 19, sqlserver 33, sqlserver cross-provider 2, postgres 3, mysql 3, mariadb 3, clickhouse 3, sqlite 8 (+ inner-loop scope amendment for `InMemoryScalarFunctionsTests`, validated before the run) | tests/**; logs/inner-test-*.log

2026-10-14T03:52:00Z | DO | revision r=1 | iteration 1/3 | D3 DB execution (SQL Server only): 5 container-backed scenarios added to `SqlServerSpecificTests` — both aliases advancing one sequence (10/15/20, bare + `dbo.`-qualified), same sequence twice in one row (1/1), BIGINT MIN/MAX materialisation, `NO CYCLE` exhaustion raising server `SqlException` (no wraparound), and a `WHERE`-clause position rejected by the server (T-SQL restriction 15) with no client fallback; each test uses a unique sequence name, `drop sequence if exists` + `create sequence ... no cycle` (test-only DDL) and cleanup in `finally`; `First()`/TOP deliberately avoided (TOP is forbidden with NEXT VALUE FOR) | tests/nextorm.integration.tests/SqlServerSpecificTests.cs; logs/boundary-integration-sqlserverspecific.log (Total 96 / Failed 0 / Skipped 0, DOCKER_HOST)

2026-10-14T04:10:00Z | DO | revision r=1 | iteration 1/3 | D4 docs + registries (EN+RU, CRLF): `docs[/ru]/scalar-functions/10-sequences.md` SQL Server section rewritten from "Not supported yet"/"Пока не поддержано" to supported (both aliases → CLR `long`, `next value for [name]`/`[schema].[name]`, 1-2 nonempty components, `]`→`]]`, non-constant/captured rejected, name never a parameter; #218 kept as tracking only) and the overview table + intro updated; `docs[/ru]/guide/provider-specific-sql/sqlserver.md` gained a "Sequences"/"Последовательности" subsection; `sql-function-coverage-gap.md` (summary row + SQL Server section), `sql-capabilities-gap-analysis.md` (§5.38 + §5.40) and `evidence-01-provider-specific-features.md` (F2 row now `yes` with `file:line`; Not-found/Open-questions records resolved) updated. T53 freeze candidates registered (freeze HELD, tag `#218`) in `docs/specs/status/D53-public-api-freeze-1.md` §Candidate records. No public doc links to `docs/specs/**`; `dotnet docfx docs/docfx.json` exit 0 (only 2 pre-existing source-generator warnings, no invalid xrefs) | docs/**; docs/specs/**; logs/docs-check.log; logs/docfx.log

2026-10-14T04:30:00Z | DO | revision r=1 | iteration 1/3 | D5 boundary + coverage + perf: solution build 0/0; serialised per-project namespace-scoped SQL-gen sweep all exit 0 (core 2493, sqlserver 859, postgres 940, mariadb 300, mysql 418, sqlite 1703 with 1 pre-existing opt-in LOB-probe skip, clickhouse 697); coverage collect/report/copy exit 0 — line 87.3% (>=85), branch 79.9% (>=75) over the 4 included assemblies (`SqlServerScalarFunctionTranslator` 93.4%, `SqlServerSpecificFunctions` 100%); acceptance POST 7 cases exit 0 with cached/prepared ratio 2.13 -> 2.05 and no regression; `t218-preparation` POST = control 640.0 us (72.8% of the 879.5 us baseline, <=105%) + 4 absolute sequence cases (621.5/635.6/681.6/629.3 us); report.json (31 executions: 17 inner / 14 boundary) and manifest.json (required_rows E00..E10) both validate exit 0, frozen tree `b5dc7eb833db291490c872fc9e20cdf352ee8030` | artifacts/pdca/D218-sqlserver-sequences/rv1/{report.json,manifest.json,coverage/report/Summary.txt,logs/*.log}

2026-10-14T04:42:00Z | DO | revision r=1 | iteration 1/3 | **Notice: acceptance-category count reconciled.** `src` carries **8** `[BenchmarkCategory("acceptance")]` attributes, but the runtime category yields exactly **7** cases (verified twice: D0 baseline and D5 POST, both exit 0) because `acceptance/SqlServerBufferedNumericAcceptanceBenchmark.cs` is compiled only with `-p:AcceptanceBenchmark=true` (excluded from the normal benchmark build). The plan's perf note "exactly 7" therefore holds; the observed count is recorded in E08, and the binding constraint is only that the new sequence benchmark must NOT get category `acceptance` (the 5 `t218-preparation` cases were added to the separate `t218-preparation` category) | logs/d0-acceptance-baseline.log; logs/d5-acceptance-post.log

2026-10-14T04:44:00Z | DO | revision r=1 | iteration 1/3 | **Notice: guide page had no sequence text to replace.** `docs/guide/provider-specific-sql/sqlserver.md` (+RU) contained no `NEXT VALUE FOR`/sequence section, so the D4 change is an **ADD** of a "Sequences"/"Последовательности" subsection (EN+RU) rather than a replacement — the only page whose "Not supported yet"/"Пока не поддерживается" claim was replaced is `docs[/ru]/scalar-functions/10-sequences.md`. No new TOC page, no renumbering, no `docs/articles/sqlserver-sequences.md`, no `12-sequences.md` | docs/guide/provider-specific-sql/sqlserver.md; docs/ru/guide/provider-specific-sql/sqlserver.md

2026-10-14T04:44:30Z | DO | revision r=1 | iteration 1/3 | Observation (not a defect): the approved plan front-matter records `base_commit: 58025147`, while the current worktree HEAD is `f8caa690` (later sibling commits). Mechanical drift only; the frozen tree and evidence are bound to the current tree. DO→PLAN candidates: **none** — no anchor/premise divergence changed design, acceptance criteria or the evidence contract | artifacts/pdca/D218-sqlserver-sequences/rv1/source-identity.txt
