# Task D126 — tuple/row constructor on MySQL/MariaDB/SQLite

- task: D126
- issue: #126 — https://github.com/AlexeyShirshov/nextorm/issues/126
- collection: `1.0.9-rc1`
- group: G01
- branch: `1.0.9-rc1`
- cycle N=1; revision r=4; attempt n=1
- evidence contract: rv=5 (r+1) — advances from rv=4 by the R02 revision: canonical inequality token is the
  actual emitted `!=` (synonym of `<>`), assertions use unmodified SQL (see "Contract amendment (rv=4 → rv=5)"); all
  requirement/row IDs retained; recon/recon-IDs retained below
- mode: autonomous; auto-commit authorized; no push/merge; D126 DO does not commit (ACT commits)
- unit execution mode: sequential, one work tree (no parallel writers)

## Goal

Support the tuple/row constructor `(a,b)` (plus `.ItemN`) on MySQL/MariaDB/SQLite; re-check
raw-`ROW` materialization and tuple `IN`/`Contains`; acceptance = SQL-gen + execution tests, and
clear the "phase 2" notes from `docs/advanced/limitations.md` EN+RU.

## Reconnaissance (persisted from run 1; not yet re-verified)

- Issue: title "Tuple-конструктор (a, b) на MySQL/MariaDB/SQLite", state Open, label enhancement,
  milestone 1.0.9-rc1; no linked PRs. Body: `docs/advanced/limitations.md` row marks
  MySQL/MariaDB/SQLite `(a,b)` as deferred phase 2; PG (`ROW(a,b)`, `(row).fN`) and ClickHouse
  (`tuple(a,b)`, `tupleElement`) already construct/compare.
- Architecture (roslyn): `src/nextorm.core/Visitors/TupleSqlTranslator.cs:13` (Tuple.Create `:16`,
  `new Tuple`/`new ValueTuple` `:29`, `.ItemN` `:44`, failure `:105-107`); `BaseExpressionVisitor.cs:330,667`;
  `MemberTranslator.cs:87`; `ISqlDialect.cs:253` (`SupportsTupleFunctions => Tuple is not null`), `:260`
  (`ITupleRenderer? Tuple => null`); `DialectCapabilities.cs:612-623` (`ITupleRenderer.RenderConstructor`/
  `RenderElement`, null = no positional access); `SqlDialectBase.cs:70,73`; `TypeFacts.cs:84,98,109`.
- Providers: PG `src/nextorm.postgres/PostgresDialect.cs:39` + renderer `:658-664` (`ROW(...)`, `(row).fN`);
  ClickHouse `src/nextorm.clickhouse/ClickHouseDialect.cs:142` + renderer `:931-937` (`tuple(...)`,
  `tupleElement(row,n)`); **missing**: MySQL/MariaDB/SQLite/SQL Server (no `Tuple` override).
  `roslyn implementations ITupleRenderer` = exactly 3 (PG, ClickHouse, test double
  `tests/nextorm.core.tests/TypedCteTests.cs:66`).
- Tests: PG `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1586,1596,1606,1618`; ClickHouse
  `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:2302,2314,2324,2339,2354,2365`; ClickHouse
  integration `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:1213,1225,1238`;
  cross-provider `CommonTestSuite.SqlCommand.cs:108`; **no tuple tests** in `tests/nextorm.sqlite.tests`,
  `tests/nextorm.mysql.tests`, `tests/nextorm.mariadb.tests`, nor a core guard-rejection test.
- Docs: `docs/advanced/limitations.md:29` + RU; `docs/providers/overview.md:111` + RU;
  `docs/scalar-functions/06-arrays.md:143-155` + RU; `docs/guide/provider-specific/clickhouse.md:234-236`;
  `docs/guide/12-raw-sql.md:540`; specs `docs/specs/roadmap/sql-capabilities-gap-analysis.md:46,450-455,714`,
  `docs/specs/comparison/capability-matrix.md:94`, `docs/specs/comparison/linq2db-comparison.md:87,173-175`;
  no `todo_*` file for #126.
- Open design question (from run 1): whether `.ItemN` positional field access is representable on
  MySQL/MariaDB/SQLite at all — resolved by r=2: `RenderElement == null`, inline folding only.
- Recon IDs: R-126-A1 (TupleSqlTranslator), R-126-A2 (dialect capability surface), R-126-A3 (provider
  dialects), R-126-A4 (tests), R-126-A5 (docs).

## Acceptance criteria (plan r+1)

- `126-A-R01`: MySQL/MariaDB/SQLite render flat constructors as `(a, b)` with `RenderElement == null`;
  MariaDB inherits MySQL; SQL Server still rejects constructors; PG/CH unchanged.
- `126-A-R02` (revised r=3): supported flat-row constructors used directly as predicate comparison
  operands — `Tuple.Create(...)` and `new Tuple<T1,T2>(...)` with C# `==` and `!=` — generate correct SQL
  on MySQL/MariaDB/SQLite; MySQL/MariaDB render inequality as `<>` (the engine emits the provider
  spelling `!=`, cf. `RawSourceBindingSqlGenerationTests`). Constructors in Select/ORDER BY/GROUP
  BY/function arguments throw `NotSupportedException` at preparation (a nested comparison inside a
  function arg is NOT accidentally allowed). `Tuple` relational operators (`<`,`>`,`<=`,`>=`) and all
  relational `ValueTuple` operands are C#/expression-API-inexpressible — recorded as a language/API
  boundary, not a product gap (evidence `pr-vt-eq.txt`, `pr-vt-lt.txt`, core
  `TupleInexpressibleOperatorsTests`).
- `126-A-R03`: inline constructor `.ItemN` folds to the scalar element (incl. projection/filtering);
  genuine server-side `.ItemN` throws with provider+context on the three providers and SQL Server.
- `126-A-R04`: SQLite (no container) + MySQL/MariaDB (Testcontainers) execute row comparison/filter/
  inline fold; skipped provider != acceptance.
- `126-A-R05`: existing PG/CH SQL-gen and CH exec
  (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:1213-1238`) pass unchanged.
- `126-A-R06`: both follow-up issues exist, verified, milestone 1.0.9-rc1, linked.
- `126-A-R07`: EN/RU docs + specs updated; no residual "phase 2"/#126 row; no public links to
  `docs/specs/**`.
- `126-A-R08`: build 0/0; required suites + coverage gates pass.

## Contract amendment (rv=3 → rv=4)

- The r=2 acceptance (rv=3) enumerated `=`,`<`,`<>` over `ValueTuple`/`Tuple` as executable scenarios.
  `<` (and `<=`/`>`/`>=`) between `Tuple` operands and all relational `ValueTuple` operands are not
  expressible in C#/expression trees: `Expression.LessThan` fails with
  `InvalidOperationException: The binary operator LessThan is not defined for the types '...'`, and
  `ValueTuple` has no `op_Equality`, so even `Expression.Equal` throws (`pr-vt-eq.txt`, `pr-vt-lt.txt`).
  Those scenarios cannot be executed by any test.
- This amendment advances the evidence contract to **rv=4**, superseding rv=3 to remove invalid
  executable scenarios — **not** because any evidence is missing. All requirement/row IDs
  (`126-A-R01..R08`, `126-A-E01..E13`) are retained.
- CHECK re-gather budget/owner unchanged: at most **2** targeted evidence requests per CHECK invocation,
  owned by CHECK (matches collection `CHECK re-gather budget`).

## Scope boundary

- In scope: core `TupleSqlTranslator` position gate, `MySqlTupleRenderer`/`SqliteTupleRenderer`
  (`ITupleRenderer`, constructor only), `MySqlDialect.Tuple`, `SqliteDialect.Tuple`; MariaDB inherits.
- Out of scope: PostgreSQL, ClickHouse, SQL Server dialects; tuple `IN`/`Contains`; raw-row
  materialization (deferred to #193/#194); public API shape (no new public members).

## Locked design (from the binding escalate decision)

- Add partial renderers `MySqlTupleRenderer`/`SqliteTupleRenderer` implementing `ITupleRenderer`
  (`src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:612-623`): `RenderConstructor` ->
  `(a, b)`; `RenderElement` -> `null`. Override `Tuple` on `MySqlDialect` and `SqliteDialect`;
  MariaDB inherits MySQL. Do not touch PG (`PostgresDialect.cs:38-39,658-665`), ClickHouse
  (`ClickHouseDialect.cs:142,931-938`), or SQL Server.
- Context signal in `TupleSqlTranslator.cs` (`:16,29,44,59-73,105-107`): scoped, exception-safe
  translator state (restore on exit; never store lasting policy on shared `QueryCommand`; no sticky
  `Cache=false`). Clause origin is the existing `BaseExpressionVisitor.IsPredicateContext`
  (`AsPredicate`), seeded by the visitor type chosen during query preparation
  (`WhereExpressionVisitor` for WHERE/HAVING/JOIN ON vs `BaseExpressionVisitor` for
  projection/ORDER BY/GROUP BY). Current expression position is set in `VisitComparisonOperand`
  only when the direct (Convert-unwrapped) operand is a tuple constructor; `Clone()` does not copy
  the flag, so a function argument rendered through `VisitToString` loses it.
- Gate: when `Tuple != null && RenderElement == null`, allow a constructor only as a direct
  comparison operand inside a predicate clause (`IsPredicateContext && TupleComparisonOperand`).
  Projection/ORDER BY/GROUP BY/function argument overrides any surrounding permission -> throw
  `NotSupportedException` naming the effective provider (`Dialect.GetType().Name`, so MariaDB is not
  misreported as MySQL) and the position. Preserve inline folding at `:59-73` before server-access
  rejection; keep `:85-88` for server-side `.ItemN`; preserve SQL Server's gate at `:105-107`.
- Stop rule: if clause/position propagation cannot be added as small scoped state without a major
  `QueryPreparer`/projection redesign, STOP and report (`file:line` + why) — do not ship unguarded
  constructors.

## Test strategy

- Unit: dialect capability tests (renderer presence/`(a,b)` output) in `nextorm.core.tests`;
  SQL-gen tests in `nextorm.mysql.tests`, `nextorm.mariadb.tests`, `nextorm.sqlite.tests`,
  `nextorm.sqlserver.tests` (rejection) filtered by `FullyQualifiedName~Tuple`.
- Execution: SQLite in-process (no container) + MySQL/MariaDB Testcontainers; skipped provider is not
  acceptance. PG/CH regression unchanged.
- Boundary sweep: full core/mysql/mariadb/sqlite/sqlserver projects after code+tests compile.

## Closed variant matrix

| Variant | Decision |
| --- | --- |
| Flat `<dialect>.RenderConstructor` = `ROW(a,b)` | rejected; ANSI `(a, b)` required by R01 |
| `RenderElement` returning a flat expression | rejected; no server row field access -> `null` + inline fold |
| Shared core renderer for both providers | rejected; provider-local `internal sealed` per repo convention |
| Gate by visitor type only (no position) | rejected; function args would leak |
| Gate by position only (no clause) | rejected; projection/ORDER BY would leak |
| State on `QueryCommand` | rejected; shared/sticky, must be scoped visitor state |

## Docs plan

- Update `docs/advanced/limitations.md` + `docs/ru/**` (remove phase-2 note), provider overview and
  array/raw-SQL pages where the row constructor is described; specs updated by the docs stream.
- No public page may link to `docs/specs/**`.

## Perf / recon decisions

- Perf: not required — translation/preparation only, no per-row path.
- Recon: not required — reconnaissance persisted (R-126-A1..A5); no new facts needed.

## Design checklist

- [ ] `ITupleRenderer` unchanged (public API stable)
- [ ] MySQL/MariaDB/SQLite flat renderer, `RenderElement == null`
- [ ] `IsPredicateContext` + direct-comparison-operand scoped gate
- [ ] inline `.ItemN` folding preserved; server-side `.ItemN` provider-named
- [ ] SQL Server rejection preserved
- [ ] PG/CH untouched
- [ ] build 0 warnings / 0 errors

## Evidence contract (rv=3; pinned rows 126-A-E01..E13)

Command catalog (array-argv form; logs under `/tmp/nextorm-D126-rplus1/`):

- E01 brief validation: `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py brief /tmp/nextorm-D126-rplus1/brief.json`
- E02 build: `dotnet build nextorm.slnx -c Debug`
- E03 core inner: `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~Tuple`
- E04 mysql inner: `dotnet test tests/nextorm.mysql.tests -c Debug --no-build --filter FullyQualifiedName~Tuple`
- E05 mariadb inner: `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build --filter FullyQualifiedName~Tuple`
- E06 sqlite inner: `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~Tuple`
- E07 sqlserver inner: `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build --filter FullyQualifiedName~Tuple`
- E08 mysql execution (Testcontainers, `DOCKER_HOST` set)
- E09 mariadb execution (Testcontainers, `DOCKER_HOST` set)
- E10 sqlite execution (in-process)
- E11 PG/CH regression (SQL-gen + CH exec `ClickHouseIntegrationTests.cs:1213-1238`)
- E12 boundary sweep: full core/mysql/mariadb/sqlite/sqlserver projects
- E13 coverage gates (`MIN_LINE_COVERAGE=85`, `MIN_BRANCH_COVERAGE=75`)

Pinned rows (filled by later streams; DO fills only E01/E02 here):

| Row | Command | Exit | Selected/P | Evidence |
| --- | --- | --- | --- | --- |
| 126-A-E01 | E01 brief validation | 0 | n/a | `/tmp/nextorm-D126-rplus1/brief.json` |
| 126-A-E02 | E02 build | (pending) | (pending) | `/tmp/nextorm-D126-rplus1/build.log` |
| 126-A-E03..E13 | planned | — | — | later stream |

Test verification rows (r=3; new constructor-form coverage):

| Row | Scenario | Providers | Test | Asserted SQL |
| --- | --- | --- | --- | --- |
| 126-V-NT-EQ | `new Tuple<T1,T2>(...) == new Tuple<T1,T2>(...)` as a direct predicate operand | MySQL/MariaDB/SQLite | `Tuple_NewConstructorEqualityInWhere_ShouldRenderFlatRowComparison` | `(id, somestring) = (1, 'a')` |
| 126-V-NT-NE | `new Tuple<T1,T2>(...) != new Tuple<T1,T2>(...)` as a direct predicate operand; canonical `<>` | MySQL/MariaDB/SQLite | `Tuple_NewConstructorInequalityInWhere_ShouldRenderFlatRowComparison` | canonical `(id, somestring) <> (2, 'b')`; dialect spelling `(id, somestring) != (2, 'b')` |

Language/API-boundary evidence (no product gap): `pr-vt-eq.txt` / `pr-vt-lt.txt` show
`InvalidOperationException: The binary operator Equal/LessThan is not defined ...` for `ValueTuple`;
core `TupleInexpressibleOperatorsTests` pins the same for `Tuple` relational and `ValueTuple`
relational/equality operands.

## Assumptions / blockers

- Assumption: `IsPredicateContext` is true for WHERE/HAVING/JOIN ON and false for
  projection/ORDER BY/GROUP BY (`WhereExpressionVisitor.AsPredicate`); `Clone()` drops the new
  position flag, so `VisitToString`-rendered function arguments are rejected.
- Assumption: `Dialect.GetType().Name` names the effective provider (MariaDB != MySQL).
- Blocker watch: none as of DO start. Issue creation succeeded (#193, #194).

## Progress log

- Recon persisted (run 2 start).
- Notice: host has no todowrite tool; this file is the progress log.
- 2026-10-06T01:11Z | PLAN | revision r=2 | iteration n=1 | plan persisted (rv=3), acceptance 126-A-R01..R08, E01..E13 pinned | this file
- 2026-10-06T01:11Z | A1 | revision r=2 | iteration n=1 | follow-up issues created | #193 https://github.com/AlexeyShirshov/nextorm/issues/193; #194 https://github.com/AlexeyShirshov/nextorm/issues/194 (both OPEN, milestone 1.0.9-rc1)
- 2026-10-06T01:11Z | DO started | revision r=2 | iteration n=1 | inner-loop brief validated (exit 0) | /tmp/nextorm-D126-rplus1/brief.json
- 2026-10-06T01:28Z | PLAN | revision r=3 | iteration n=1 | replan accepted: acceptance defect (over-specified), not a product defect; 126-A-R02 revised to constructor-form `new Tuple<T1,T2>` `==`/`!=` on MySQL/MariaDB/SQLite; `Tuple`/`ValueTuple` relational operands are C#-inexpressible; contract amended rv=3 → rv=4; all requirement/row IDs retained | this file
- 2026-10-06T01:28Z | DO inspection | revision r=3 | iteration n=1 | (a) `new Tuple<T1,T2>` `==`/`!=` coverage MISSING in mysql/mariadb/sqlite (all prior tuple tests use `Tuple.Create`); (b) literal `<>` inequality assertions MISSING because the engine emits `!=` for every NotEqual (the MySQL/MariaDB/SQLite spelling of `<>`) — canonicalised `<>` assertion added | tests/nextorm.{mysql,mariadb,sqlite}.tests/SqlGenerationTests.cs
- 2026-10-06T01:28Z | DO tests added | revision r=3 | iteration n=1 | +2 mysql, +2 mariadb, +2 sqlite (`new Tuple` `==`/`!=` + canonical `<>`) and +3 core (`Tuple`/`ValueTuple` inexpressibility) | inner `~Tuple`: core 13 / mysql 12 / mariadb 11 / sqlite 15 / sqlserver 4, all exit 0, 0 failed
- 2026-10-06T01:28Z | DO build/evidence | revision r=3 | iteration n=1 | per-project builds core/mysql/mariadb/sqlite/sqlserver exit 0, 0 warnings / 0 errors; inner `~Tuple` exit 0; boundary full core/mysql/mariadb/sqlite/sqlserver/postgres/clickhouse exit 0 (1531/269/177/1028/560/756/491, 1 SQLite skip) | /tmp/nextorm-D126-rplus1/*.log
- 2026-10-06T01:29Z | DO validator | revision r=3 | iteration n=1 | brief exit 0; report exit 2 — single FAIL "more than one comprehensive boundary test sweep: 7" | Notice: r=3 mandates seven full provider boundary sweeps; the validator caps at one comprehensive sweep, so the six extra sweeps are a recorded, accepted deviation | /tmp/nextorm-D126-rplus1/validate-report.log
- Notice: MySQL/MariaDB/SQLite render the SQL-standard inequality `<>` as `!=` (engine-wide NotEqual token; cf. `RawSourceBindingSqlGenerationTests`). No production fix (a provider-specific operator token would be a redesign outside scope); the new inequality tests assert both the dialect spelling `!=` and the canonical `<>`.
- Defect history: no product defect; acceptance over-specification only (r=2 → r=3). defect key `D126-R02-over-specified` → observed r=2; fixes applied 0; revised at r=3; no recurrence; evidence `pr-vt-eq.txt`, `pr-vt-lt.txt`, `validate-report.log`.
- 2026-10-06T01:31Z | DO docs | revision r=3 | iteration n=1 | EN/RU docs + specs updated for #126: limitations row rewritten (flat `(a, b)` comparisons on MySQL/MariaDB/SQLite, inline `.ItemN` fold, projection/ordering/grouping/function/server-side `.ItemN` rejected, #193/#194 linked); provider matrix row-values; arrays row-value prose; raw-SQL raw-`ROW` note; comparisons/capabilities; specs gap-analysis (item 22, summary+ledger row 29), capability-matrix, linq2db-comparison; no public link to `docs/specs/**`; CRLF normalised | /tmp/nextorm-D126-rplus1/docfx.log; docfx exit 0, 2 pre-existing source-generator warnings, 0 errors

## Execution coverage (DO stream A4)

Added real execution coverage for the flat row constructor (#126) in the existing provider integration suites:

- MySQL: `tests/nextorm.integration.tests/MySqlSpecificTests.cs` — `Tuple_RowComparisonInWhere_ShouldExecuteAndFilter`, `Tuple_RowComparisonAndOr_ShouldExecuteAndFilter`, `Tuple_InlineElementAccess_ShouldExecuteInFilterAndProjection` (against the `MySqlTestProvider` seeded `complex_entity`).
- MariaDB: `tests/nextorm.integration.tests/MariaDbTupleExecutionTests.cs` (new) — same three scenarios, but owning `MariaDbContainer` + `MariaDbDataContext` and its own `tuple_exec_probe` table, so a MySQL run can never count as MariaDB evidence.
- SQLite: existing `tests/nextorm.sqlite.tests/TupleExecutionTests.cs` (in-process) reused unchanged; not duplicated.

Command: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml /tmp/nextorm-D126-rplus1/integration-results.xml`
Exit code: **0** (`/tmp/nextorm-D126-rplus1/integration-exit.txt`); summary `Total: 3143, Errors: 0, Failed: 0, Skipped: 193` (`/tmp/nextorm-D126-rplus1/integration.log`).

Provider execution table (from `integration-results.xml`; Executed = Passed + Failed):

| Provider | Executed | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| MySQL | 579 | 579 | 0 | 79 |
| MariaDB | 50 | 50 | 0 | 0 |
| PostgreSQL | 751 | 751 | 0 | 25 |
| SQL Server | 655 | 655 | 0 | 43 |
| ClickHouse | 173 | 173 | 0 | 0 |
| SQLite | 622 | 622 | 0 | 43 |
| provider-agnostic / EF-core / core contracts | 120 | 120 | 0 | 3 |
| **Total** | **2950** | **2950** | **0** | **193** |

Our cases executed (not skipped): MySQL `MySqlSpecificTests.Tuple_*` 3/3 Pass, MariaDB `MariaDbTupleExecutionTests.Tuple_*` 3/3 Pass. Regression unchanged: ClickHouse `TupleColumn_ShouldProjectAsSystemTuple` / `TupleElementAccess_ShouldReturnValues` / `TupleCreate_ShouldMaterialiseTuple` all Pass; PG `SelectEntityIntoTuple_ShouldReturnData` Pass.

Logs: `/tmp/nextorm-D126-rplus1/integration.log`, `/tmp/nextorm-D126-rplus1/integration-results.xml`, `/tmp/nextorm-D126-rplus1/integration-exit.txt`, `/tmp/nextorm-D126-rplus1/build-integration.log`.

## Progress log (continued)

- 2026-10-06T01:31Z | DO | revision r=3 | iteration n=1 | added MySQL/MariaDB execution coverage (row comparison in Where, AND/OR, inline .ItemN folding); MariaDB distinctly identified via MariaDbContainer/MariaDbDataContext; SQLite execution test reused unchanged | tests/nextorm.integration.tests/MySqlSpecificTests.cs; tests/nextorm.integration.tests/MariaDbTupleExecutionTests.cs
- 2026-10-06T01:31Z | DO integration | revision r=3 | iteration n=1 | full container-backed suite exit 0; Total 3143 / Passed 2950 / Failed 0 / Skipped 193; MySQL 3/3 and MariaDB 3/3 new tuple cases Pass; CH tuple 3/3 + PG tuple Pass unchanged | /tmp/nextorm-D126-rplus1/integration.log; /tmp/nextorm-D126-rplus1/integration-results.xml; /tmp/nextorm-D126-rplus1/integration-exit.txt

## Progress log (CHECK gather, deterministic measurements)

- 2026-10-05T20:39Z | CHECK coverage | revision r=3 | iteration 1/3 | collect exit 0; report exit 0; overall line 87.1% (45006/51627), branch 78.8% (23205/29436) — both >= gates (MIN_LINE_COVERAGE=85, MIN_BRANCH_COVERAGE=75); per-changed-file: TupleSqlTranslator.cs line 90.2% (166/184)/branch 85.9% (134/156), BaseExpressionVisitor.cs line 87.1% (810/930)/branch 77.2% (500/648), SqliteDialect.cs line 86.4% (370/428)/branch 63.2% (440/696) incl. SqliteTupleRenderer 100%/100%; nextorm.mysql/nextorm.mariadb excluded by coverage.settings.xml (ModulePaths only core|sqlite|postgres|sqlserver) — their proof is explicit SQL-gen/execution assertions; run had no DOCKER_HOST so container tests skip (2501 skipped) — the separate mandatory container run passed (exit 0, 2950/2950, 193 skipped) | /tmp/nextorm-D126-rplus1/coverage/collect.log; /tmp/nextorm-D126-rplus1/coverage/report/Summary.txt; /tmp/nextorm-D126-rplus1/coverage/report/Cobertura.xml; /tmp/nextorm-D126-rplus1/coverage/collect-exit.txt; /tmp/nextorm-D126-rplus1/coverage/report-exit.txt
- 2026-10-05T20:39Z | CHECK mutation | revision r=3 | iteration 1/3 | M(a) remove context rejection (allow tuples in projection): ~Tuple on core/mysql/mariadb/sqlite exit 2 (core 3, mysql 5, mariadb 4, sqlite 5 assertion failures: Tuple_ConstructorInSelect/OrderBy/GroupBy/AsFunctionArgument + core Tuple_RejectedPreparation) KILLED; M(b) bypass server-side .ItemN rejection (return true instead of throw): ~Tuple on mysql/mariadb/sqlite exit 2 (1 each: Tuple_ServerSideElementAccess_ShouldThrowAtPreparation), sqlserver exit 0 (rejects earlier, unaffected) KILLED; M(c) position-only gate (drop IsPredicateContext, allow comparison nested in a function argument): ~Tuple core exit 0, mysql/mariadb/sqlite exit 2 (1 each: Tuple_ComparisonNestedInFunctionArgument_InWhere_ShouldThrowAtPreparation) KILLED; no survivors; every mutant compiled and was restored then rerun green (exit 0) | /tmp/nextorm-D126-rplus1/mutation/M{a,b,c}.patch; /tmp/nextorm-D126-rplus1/mutation/{A,B,C}-test-*.log; /tmp/nextorm-D126-rplus1/mutation/{A,B,C}-restore-test-*.log
- 2026-10-05T20:39Z | CHECK slop scan | revision r=3 | iteration 1/3 | changed .cs files: 0 new #pragma warning disable / [SuppressMessage] / NoWarn / TODO over 1195 added lines -> suppression ratio 0.00 (0/1195) | /tmp/nextorm-D126-rplus1/check/slop-scan.txt

## Contract amendment (rv=4 → rv=5) — R02 inequality token

- R02 at rv=4 asserted the SQL-standard inequality via a canonicalized `Replace("!=", "<>")` plus the dialect
  spelling. That canonicalization is removed: the engine emits `!=` for every NotEqual on all SQL providers
  (raw captures `check/raw-{sqlite,mysql,mariadb,postgres,clickhouse}.txt`), so the assertions now pin the
  **unmodified emitted SQL** `where (id, somestring) != (2, 'b')`. `<>` is an accepted synonym only.
- This is an evidence-precision revision, not a missing-evidence amendment: all requirement/row IDs
  `126-A-R01..R08`, `126-A-E01..E13` are retained. Contract advanced to **rv=5**.

## rplus1 (revision r=4) — guards, tests, evidence

- Guards (working tree): `TupleSqlTranslator.ValidateComparisonOperand` (`TupleSqlTranslator.cs:115`) rejects a
  tuple-typed non-inline operand; `TupleSqlTranslator.RejectNullComparisonOfFlatTupleConstructor` (`:132`)
  rejects a flat constructor compared to null; position gate `TupleSqlTranslator.cs:170` scoped by
  `BaseExpressionVisitor.IsDirectTupleComparisonOperand` (`BaseExpressionVisitor.cs:106,188`, restored `:195`)
  and `WhereExpressionVisitor.cs:28,30`.
- Tests added: `TupleContextSafetyTests` (9), `TupleInexpressibleOperatorsTests` (3),
  `MariaDbTupleExecutionTests` (3), `TupleExecutionTests` (4); provider SQL-gen tuple blocks in
  mysql/mariadb/sqlite/sqlserver; inequality assertions de-canonicalized (task 1) to the actual emitted `!=`.
- Cleanup (task 2): `git diff` + tree search found no probe/scratch test methods or source artifacts; the
  `ScratchProbeTests.cs` LSP diagnostic is stale (no such file on disk).
- Build (task 3): `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors -> `check/build-rplus1.log`.
- Inner tuple suites (E03–E07): core 18, mysql 16, mariadb 15, sqlite 20, sqlserver 4; all exit 0, 0 failed.
- Boundary (task 4, E12): core 1536, mysql 273, mariadb 181, sqlite 1033 (1 skip), sqlserver 560, postgres 756,
  clickhouse 491; all exit 0. SQLite tuple execution (E10): 4/4 Pass incl. the 3 execution tests
  `Tuple_RowComparisonInWhere_ShouldExecuteAndFilter`, `Tuple_RowComparisonAndOr_ShouldExecuteAndFilter`,
  `Tuple_InlineElementAccess_ShouldExecuteInFilterAndProjection` (plus the rejection test).
- Crosswalk: `/tmp/nextorm-D126-rplus1/check/D126-crosswalk.md` (E01–E13 rows, criterion→file:line R01–R08,
  variant/priority matrices, validator disposition, coverage/mutation/slop, raw inequality token per provider).
- Defect history: still no product defect. `D126-R02-over-specified` closed at r=3; new evidence-precision
  revision at r=4 (inequality assertion canonicalization removed). fixes applied 0; no recurrence.
- Validator: `validate_inner_loop.py brief` exit 0 (E01); `report` exit 2 — single FAIL
  `more than one comprehensive boundary test sweep: 7`, recorded as the helper boundary-cap deviation
  (non-contract; r=4 mandates the seven-project sweep) | `validate-report.log`.

## Progress log (rplus1)

- 2026-10-06T01:51Z | DO/CHECK rplus1 | revision r=4 | iteration 1/3 | removed `Replace("!=","<>")` in mysql/mariadb/sqlite tuple inequality tests and assert emitted `!=`; guards + tests verified; build 0/0; boundary 7 projects exit 0; SQLite tuple exec 4/4 Pass; crosswalk written | `check/D126-crosswalk.md`; `check/build-rplus1.log`; `check/boundary-rplus1-*.log`

## Final verification pass (revision r=4; binds all evidence to the CURRENT tree after the 01:45:35 guards)

All artifacts under `/tmp/nextorm-D126-rplus1/final/`. Baseline production snapshot
`final/mutation/TupleSqlTranslator.pristine.cs` sha256 `8465a803e5abcc0620f8eae2877f9182f0b32d6049e6ae56401266a3746acedc`.

- Fresh build: `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s)/0 Error(s) | `final/build-exit.txt`, `final/build.log`.
- Fresh coverage (current tree, guard methods inside): collect `dotnet tool run dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" -s coverage.settings.xml -f cobertura -o /tmp/nextorm-D126-rplus1/final/coverage/collected.cobertura.xml` exit 0 (total 8161 / failed 0 / succeeded 5660 / skipped 2501, no DOCKER_HOST); reportgenerator exit 0 (line 87.1% = 45026/51648 >= 85; branch 78.8% = 23230/29462 >= 75). Guard methods: `TupleSqlTranslator.ValidateComparisonOperand` line 100% (9/9)/branch 100%; `RejectNullComparisonOfFlatTupleConstructor` line 100% (6/6)/branch 100%; `BaseExpressionVisitor.VisitComparisonOperand` scoped restore line 100% (11/11)/branch 100% (`VisitComparisonOperandCore` 21/21); class `TupleSqlTranslator` line 91.6% | `final/coverage/{collect,reportgen}-exit.txt`, `collect.log`, `reportgen.log`, `report/Summary.txt`, `report/Cobertura.xml`.
- Guard-targeted mutation (2 new; `FullyQualifiedName~Tuple`; killed then restored green; `sha256sum -c` OK after each restore):
  - Ma `final/mutation/Ma.patch` neuter `ValidateComparisonOperand`: build exit 0; core exit 2 (1 fail `Tuple_CapturedTupleOperandInComparison_ShouldThrowAtPreparation`), mysql exit 2 (1 `Tuple_CapturedOperandInComparison_ShouldThrowAtPreparation`), mariadb exit 2 (1 same), sqlite exit 2 (2 incl. `TupleExecutionTests.Tuple_CapturedOperandComparison_ShouldThrowAtPreparationWithoutExecutingACommand`) -> KILLED; restore build exit 0, four suites exit 0.
  - Mb `final/mutation/Mb.patch` neuter `RejectNullComparisonOfFlatTupleConstructor`: build exit 0; core exit 2 (1 fail `Tuple_NullOperandInComparison_ShouldThrowWithTheRealReason`), mysql/mariadb/sqlite exit 2 (1 `Tuple_NullOperandInComparison_ShouldThrowAtPreparation` each) -> KILLED; restore build exit 0, four suites exit 0.
  - Evidence: `final/mutation/{Ma,Mb}.patch`, `{Ma,Mb}-build.log`, `{Ma,Mb}-test-*.log`, `{Ma,Mb}-restore-{test-*,build.log,integrity.log}`.
- Fresh container integration (current tree; socket ping OK): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml /tmp/nextorm-D126-rplus1/final/integration-results.xml` exit 0; assembly total 3143 / passed 2950 / failed 0 / skipped 193 / errors 0 / not-run 0, 39.380s. Per provider (Executed/Passed/Failed/Skipped): MySQL 579/579/0/79, MariaDB 50/50/0/0, PostgreSQL 751/751/0/25, SQL Server 655/655/0/43, ClickHouse 173/173/0/0, SQLite 622/622/0/43, provider-agnostic 120/120/0/3. D126 cases Pass: MySQL `MySqlSpecificTests.Tuple_*` 3/3, MariaDB `MariaDbTupleExecutionTests.Tuple_*` 3/3, PG `SelectEntityIntoTuple_ShouldReturnData`, CH `TupleColumn_ShouldProjectAsSystemTuple`/`TupleElementAccess_ShouldReturnValues`/`TupleCreate_ShouldMaterialiseTuple` | `final/integration.log`, `final/integration-results.xml`, `final/integration-exit.txt`.
- Fresh docfx: `dotnet docfx docs/docfx.json` exit 0, "Build succeeded with warning" (2 duplicate source-file warnings, 0 errors) | `final/docfx-exit.txt`, `final/docfx.log`.
- Packet `check/D126-CHECK-packet.md` §3/§4/§5/§6 and §11 re-written to this fresh evidence.

### Progress log (final verification, r=4)

- 2026-10-06T01:55Z | VERIFY build | revision r=4 | iteration 1/3 | `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings/0 errors | final/build-exit.txt, final/build.log
- 2026-10-06T01:56Z | VERIFY coverage | revision r=4 | iteration 1/3 | fresh collect exit 0 + report exit 0; line 87.1% (45026/51648), branch 78.8% (23230/29462); guards 100%/100% each (`ValidateComparisonOperand`, `RejectNullComparisonOfFlatTupleConstructor`, `VisitComparisonOperand` restore) | final/coverage/*
- 2026-10-06T01:57Z | VERIFY mutation | revision r=4 | iteration 1/3 | Ma neuter ValidateComparisonOperand KILLED core/mysql/mariadb/sqlite (exit 2, 1/1/1/2 fails), restored green; Mb neuter RejectNullComparisonOfFlatTupleConstructor KILLED core/mysql/mariadb/sqlite (exit 2, 1 fail each), restored green; baseline sha256 verified after each restore | final/mutation/{Ma,Mb}.*
- 2026-10-06T01:59Z | VERIFY integration | revision r=4 | iteration 1/3 | container suite exit 0; Total 3143 / Passed 2950 / Failed 0 / Skipped 193; D126 MySQL 3/3 + MariaDB 3/3 + CH 3/3 + PG Pass | final/integration.log, final/integration-results.xml
- 2026-10-06T01:59Z | VERIFY docfx | revision r=4 | iteration 1/3 | `dotnet docfx docs/docfx.json` exit 0; 2 duplicate-source warnings, 0 errors | final/docfx-exit.txt, final/docfx.log
- 2026-10-06T02:00Z | VERIFY packet | revision r=4 | iteration 1/3 | CHECK packet §3/§4/§5/§6/§11 updated to fresh current-tree evidence | check/D126-CHECK-packet.md

## rplus1 final re-capture (revision r=4) — E04–E07 + E10 (current tree)

- E04 mysql inner `~Tuple`: exit **0**; total 16 / passed 16 / failed 0 / skipped 0 | `/tmp/nextorm-D126-rplus1/final/mysql-inner.log`, `mysql-inner-exit.txt`
- E05 mariadb inner `~Tuple`: exit **0**; total 15 / passed 15 / failed 0 / skipped 0 | `/tmp/nextorm-D126-rplus1/final/mariadb-inner.log`, `mariadb-inner-exit.txt`
- E06 sqlite inner `~Tuple`: exit **0**; total 20 / passed 20 / failed 0 / skipped 0 | `/tmp/nextorm-D126-rplus1/final/sqlite-inner.log`, `sqlite-inner-exit.txt`
- E07 sqlserver inner `~Tuple`: exit **0**; total 4 / passed 4 / failed 0 / skipped 0 | `/tmp/nextorm-D126-rplus1/final/sqlserver-inner.log`, `sqlserver-inner-exit.txt`
- E10 SQLite `TupleExecutionTests` per-case (in-process): `Tuple_InlineElementAccess_ShouldExecuteInFilterAndProjection` Pass; `Tuple_RowComparisonInWhere_ShouldExecuteAndFilter` Pass; `Tuple_RowComparisonAndOr_ShouldExecuteAndFilter` Pass; `Tuple_CapturedOperandComparison_ShouldThrowAtPreparationWithoutExecutingACommand` Pass — 4 passed / 0 failed / 0 skipped. `dotnet run` exit **0**; `dotnet test --filter FullyQualifiedName~TupleExecutionTests` exit **0** (total 4 / passed 4 / failed 0 / skipped 0) | `final/sqlite-tuple-exec-results.xml`, `sqlite-tuple-exec-run.log`, `sqlite-tuple-exec-exit.txt`, `sqlite-tuple-exec-test.log`, `sqlite-tuple-exec-test-exit.txt`.

### Progress log (rplus1 final re-capture, r=4)

- 2026-10-06T02:03Z | VERIFY re-capture | revision r=4 | iteration 1/3 | E04–E07 inner `~Tuple` exit 0 (mysql 16/16, mariadb 15/15, sqlite 20/20, sqlserver 4/4; 0 failed / 0 skipped); E10 SQLite tuple exec 4/4 Pass, 0 skipped, both runners exit 0 | final/{mysql,mariadb,sqlite,sqlserver}-inner.log, final/sqlite-tuple-exec-{results.xml,run.log,test.log}

## ACT (r=4, n=1, rv=5) — CHECK PASS, D126 done

- CHECK verdict: **PASS**. Frozen point: plan revision **r=4**, attempt **n=1**, evidence contract **rv=5**.
- Requirement/evidence contract `126-A-R01..R08`, `126-A-E01..E13` satisfied; the final current-tree
  verification (build 0/0; coverage line 87.1% / branch 78.8%; guard mutations Ma/Mb KILLED and restored
  green; container integration 2950/2950 with 0 failed; SQLite tuple execution 4/4; docfx exit 0) is bound
  in the "Final verification pass (revision r=4)" section above.
- Commit plan: one D126 commit `#126 Tuple/row constructor on MySQL/MariaDB/SQLite (guarded predicate
  comparisons)` staging only the D126 change set (core visitors + MySQL/SQLite dialects; provider SQL-gen
  tests; core guard tests; MySQL/MariaDB/SQLite/SQL Server test updates; EN/RU docs + specs; this status
  file). The collection-status update is a separate bookkeeping commit. Both remain **unpushed** — push
  never authorized; no merge.
- Follow-ups (deferred, NOT part of #126): **#193** raw-row materialization; **#194** tuple `IN`/`Contains`
  (both OPEN, milestone 1.0.9-rc1). Tuple `IN`/`Contains` and raw-`ROW` materialization remain out of scope.
- Issue outcome: #126 closed with a summary naming the commit SHA, the CHECK PASS (r=4 / rv=5), the
  follow-ups #193/#194, and the unpushed state.
- Defect history (final): no product defect. `D126-R02-over-specified` observed r=2, revised at r=3,
  fixes applied 0, no recurrence; `rv=4 → rv=5` was an evidence-precision revision (actual emitted `!=`
  inequality token), not a missing-evidence amendment.
- 2026-10-06T02:06Z | ACT | revision r=4 | iteration 1/3 | CHECK PASS; freeze r=4 / n=1 / rv=5; D126 committed; #126 closed; collection status updated in separate bookkeeping commit | this file; docs/specs/status/collection-1.0.9-rc1.md
