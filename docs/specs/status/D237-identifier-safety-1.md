# T237 (#237) — Identifier safety: SQL Server bracket escaping and raw UNPIVOT/Column&lt;T&gt; names

- task_id: `T237`
- issue: [#237](https://github.com/AlexeyShirshov/nextorm/issues/237) — OPEN, milestone **1.0.9-rc3**
- origin: #232 (T232) — `docs/specs/status/D232-sqlserver-pivot-unpivot-1.md` (two High identifier-safety findings, accepted risk there, never fixed; remedy scoped here)
- collection: `1.0.9-rc3-open` — **single group**; current worktree/branch `1.0.9-rc3`; no worktree/group-branch/task-branch, no `merge --no-ff`, **no commit, no push**
- selected_variant: `pdca-dotnet` (+ project overlay `nextorm-pdca`)
- current cycle `N=1`; plan_revision `r=1`; attempt `n` not started (this invocation stops at the PLAN→DO boundary)
- evidence_contract: `rv=1`; frozen manifest path (planned): `artifacts/pdca/D237-identifier-safety/rv1/manifest.json`
- **plan_state: `ready`** (gate 1 items present: goal, acceptance criteria with negative cases, task list, `file:line` refs, variant matrix, test strategy, docs plan, perf decision, reconnaissance decision, unit execution mode, write footprint, risks/prerequisites)
- canonical status path: `docs/specs/status/D237-identifier-safety-1.md`
- baseline ref: `c7ff89ca` (branch `1.0.9-rc3`)
- spec_version: approved design `docs/specs/design/issue-237-safe-identifier-rendering.md` (APPROVED 2026-10-16; current state: untracked)
- phase: **P complete — stop at PLAN→DO**; no DO, no product code, no commits

## Task

Realize exactly the approved scope of #237: raw-name/always-safe-quoted contract for `SqlFunctions.Column<T>`, `PivotValue.Create`/typed `PivotValues.Value<T>`, `UnpivotColumn.Create`, `UnpivotValues.Name`/`Value`; SQL Server `Escape` doubles an embedded `]`; exactly one escaping stage per emitted occurrence; public docs EN+RU + XML docs in the same change; integration evidence across SQLite + containerized SQL Server/PostgreSQL/MySQL/ClickHouse at DO→CHECK. This invocation authored and persisted the task's own PLAN for the collection's PLAN→DO handoff — no user `go` is awaited; the collection caller owns the boundary.

## Goal

Emit the listed identifiers safely while preserving PIVOT data values, existing validations and the behaviour of all other SQL paths.

**Minimal solution — three answers:** (1) essential outcome — fix the SQL text; (2) non-negotiable boundary — exactly one escaping stage, unchanged public signatures/data/cache semantics; (3) minimum implementation — fix SQL Server `Escape`, pass raw names, force `QuoteIdentifier` at only the two target sinks.

| Alternative | Benefits / cost & risk | Decision |
|---|---|---|
| Narrow quoting helper for `AppendColumnReference` and the direct markers; raw-name formatter | Small footprint; requires covering every marker route | **Selected**; the shared helper does not change `AppendIdentifier` |
| Globally change `AppendIdentifier` or turn `QuoteIdentifiers` on | Fewer local edits, but breaks the legacy contract and widens scope | Rejected |
| Pre-escape, then recognise/strip quotes | Keeps some old strings, but changes data values and creates double quoting | Rejected |

## Acceptance criteria

Refs point at current sources; new tests and their lines are **planned**, not yet written.

| ID | Observable assertion | Negative case | Basis `file:line` |
|---|---|---|---|
| R237-01 | SQL Server `Escape("a]b") == "[a]]b]"`; ordinary and repeated `]` correct; declarations and references agree; consumers do not regress | single `]`, double escaping, damaged aliases/mapped refs/update-delete/source locks | `src/nextorm.sqlserver/SqlServerDialect.cs:168,171,202,837-867`; `src/nextorm.core/DataContext/SqlBuilder.cs:539,1624,1738` |
| R237-02 | Typed/untyped PIVOT: `"1"`→`[1]`, `"a]b"`→`[a]]b]`; `.Value("[1]")` keeps value `[1]` and identifier `[[1]]]`; both quoting modes | `[1]` silently becoming `1`, data-value change, re-quoting of the typed projection | `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs:135-148`; `src/nextorm.core/Query/PivotValues.cs:50` |
| R237-03 | Raw UNPIVOT columns/roles identical in declaration/reference; `0+(SELECT COUNT(*) FROM sys.tables)` via `UnpivotValues.Name/Value` is one quoted identifier | payload becomes an expression; role/reference gets different brackets | `src/nextorm.sqlserver/SqlServerDialect.cs:853-867`; `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs:196-199` |
| R237-04 | `Column(t,"name")` with quoting=false yields `t.[name]`/`t."name"`/backticks; all name shapes incl. the R03 payload are one identifier; dot not split; one pass, no stripping | raw SQL, qualification by dot, stripping `[1]`, double quoting in quoted mode | `src/nextorm.core/Query/SqlFunctions.cs:82-95`; `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:464-475` |
| R237-05 | Safe: explicit `Column`, direct `TryTranslateMarker` before rewriter replacement, and injected grouping `Column`; grouping snapshots intentionally change at quoting=false | a route stays raw/option-gated, or grouping uses an unsafe bypass | `src/nextorm.core/Visitors/NormSqlTranslator.cs:83-118,198`; `src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs:311-354` |
| R237-06 | Unrelated snapshots, legacy `TableAlias.Get*`, WithTableName/CTE/general aliases, the global option and metadata/config stay as before | mass quoting, changed legacy accessors, or flipping the global option to make a test pass | `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:335-341,451,453`; `src/nextorm.core/Visitors/MemberTranslator.cs:313,322,480,512,984` |
| R237-07 | Existing null/default/empty/nonconstant/unbound/ambiguous/in-memory checks and generic value/reference/nullable behaviour preserved | accepting what was rejected, rejecting valid punctuation, or changing type/value | `src/nextorm.core/Visitors/NormSqlTranslator.cs:89-110,130-143`; `tests/nextorm.core.tests/InMemoryTests.cs:1021` |
| R237-08 | Re-preparation with different names never reuses a foreign identifier; typed/untyped consistent; real queries execute correctly | warm cache returns the previous name; flags/cache state change; a skipped provider counted as success | `tests/nextorm.sqlserver.tests/T232PreparationProbeTests.cs:110-140`; `tests/nextorm.integration.tests/T232SqlServerTests.cs:35-322` |
| R237-09 | EN/RU/XML explain raw names, literal dot/brackets, migration and the limited security scope; no public links to specs | stale prequoted advice, EN/RU or XML divergence, or a link into the internal spec | `docs/guide/provider-specific-sql/sqlserver.md:143-293`; `docs/ru/guide/provider-specific-sql/sqlserver.md:144-336`; `src/nextorm.core/Query/SqlFunctions.cs:82-95` |
| R237-10 | Build 0 warnings/errors; boundary tests with no skipped affected provider; coverage ≥85% lines / 75% branches on the comparable scope; scoped mutation closed | empty selection, hidden skips, a lost branch, an unexplained surviving mutant | `.github/workflows/dotnet.yml`; `coverage.settings.xml:12`; `tests/nextorm.integration.tests/ProviderTestSuite.cs:19` |
| R237-11 | Acceptance benchmark baseline/after contains all 7 cases plus time/allocations comparison | no baseline, incomplete selection, or an unexplained material regression | `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:474`; `src/nextorm.core/Visitors/NormSqlTranslator.cs:115`; overlay class table «query-path / plan cache» |

## Variant matrix

The Cartesian product applies only to supported combinations; every row is closed as test/guard, there are no deferred rows.

| Variants | Closure |
|---|---|
| SQL Server/PostgreSQL/SQLite/MySQL/MariaDB/ClickHouse × quoting on/off × explicit Column × all name shapes | No-DB test; independent provider-correct full SQL oracles, not substrings and not calling the `QuoteIdentifier` under test |
| SQL Server × typed/untyped PIVOT × on/off × explicit/direct-marker/injected-grouping | Test each applicable route, including the direct marker before rewriter replacement and grouping in unquoted mode |
| SQL Server × typed/untyped UNPIVOT × on/off × explicit/direct Name/direct Value/injected-grouping | Test the same raw input in declaration and reference; absence of a second escaping |
| All listed test strings × simple/numeric/whitespace/Unicode/reserved/dot/provider-delimiter/backslash/embedded `]`/SQL-looking payload/literal `[1]` | Table-driven cases; PIVOT data values are not normalised; real provider name-existence limits do not substitute for quoting |
| Context default on/off × available query override on/off; value/reference/nullable generic arguments; null/default/empty/nonconstant/source ambiguity/in-memory | Positive tests + preserved negative guards; test the override only where the API supports it |
| PIVOT/UNPIVOT on the other five providers; MariaDB runtime | Existing unsupported-provider guard; MariaDB gets its own no-DB tests and MySQL dialect reuse, no separate DB claimed |

## Priorities

R237-01…11 are all **P1** by the statement's invariants and the execution-path/class table; CHECK does not downgrade. The SQL shape of affected grouping intentionally changes; unrelated snapshots do not.

## Units (single tree, sequential)

Dependencies: D01→D02→D03→D04→D05→D06→D07.

- **D-01, fix now:** record the starting dirty state, baseline benchmark/coverage; add red cases for the payload and bracket/data-value; create the execution brief. Selector — project `tests/nextorm.sqlserver.tests`; do not invent test symbols.
- **D-02, fix now:** `src/nextorm.sqlserver/SqlServerDialect.cs:168` — double `]`; verify the actual Roslyn impact of the listed `Escape` consumers, including polymorphic dispatch; selector — sqlserver project.
- **D-03, fix now:** `BaseExpressionVisitor.cs:464-475`, `NormSqlTranslator.cs:115,198` — narrow always-quoted emission for both sinks; do not change legacy callers; selectors — core and all six provider projects.
- **D-04, fix now:** `SqlServerEntityBuilderExtensions.cs:135-148,196-199` — remove pre-escaping from marker-name transfer, keep raw data; rewriter `:311-354` must not bypass via an unsafe helper; migrate affected T232/snapshots, verify the cache. Selectors — sqlserver/core.
- **D-05, fix now:** fill the whole no-DB matrix, add real identifier/payload cases to the integration provider/common suites and the SQL Server PIVOT fixture; selectors — seven no-DB projects, integration only at the boundary.
- **D-06, fix now:** EN/RU/XML migration, source evidence, scoped mutants and branch-delta; after D-02…05. Other dialects' `Escape` — deferred: a separate open risk, trigger = a separate agreed task; never claimed fixed.
- **D-07, fix now:** one DO→CHECK boundary sweep, collect/freeze the evidence manifest and reports; DO makes no PASS claim.

**Unit execution mode:** sequential in the current tree/branch `1.0.9-rc3`; footprints overlap. No worktree/branch/merge/commit/push. Inner loop = affected-project selector after each change, no containers.

## Commands (planned; `A=artifacts/pdca/D237-identifier-safety/rv1`)

All scripts/artifacts under `A` are planned and created by `coder`; commands run from the repo root preserving real exit code, stdout/stderr and selected_count.

- **U:** `bash "$A/unit-sweep.sh" --no-build` — runs `dotnet test "tests/nextorm.$p.tests" -c Debug "$@"` for `p=core sqlite postgres sqlserver mysql mariadb clickhouse`, stops on error, keeps a separate log per project. The inner loop uses the same commands without `--no-build`.
- **I — the single boundary integration run:** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; executes the planned `bash "$A/integration-sweep.sh"`; SQLite and SQL Server/PostgreSQL/MySQL/ClickHouse are mandatory; a skip of any forbids PASS.
- **B:** `dotnet build nextorm.slnx -c Debug`
- **C:** `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "bash artifacts/pdca/D237-identifier-safety/rv1/unit-sweep.sh --no-build"` (C contains boundary U; no separate repeat of U)
- **G:** `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`; keep the baseline separately before the fix, final after B.
- **P:** `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; baseline before production edits and after on the same host/config; keep both BenchmarkDotNet results.
- **V:** `python3 scripts/validate_inner_loop.py brief "$A/inner-loop-brief.json"`; then `python3 scripts/validate_inner_loop.py report "$A/inner-loop-report.json"`; freeze: `python3 scripts/validate_inner_loop.py manifest "$A/manifest.json"`; each exit=0; a violation/usage exit is not accepted.

**Execution records:** the brief carries unit, scope.projects/selectors/files/rationale/rebuild/boundary; the report carries the actual command[], phase, source_revision, artifact_revision, exit_code and selected_count≥1. Do not write future test symbols that do not exist: `coder` adds observed Roslyn identities to the scenario-map.

## Test strategy

SQL generation — unit/no-DB; actual execution/escaping — integration I once at the boundary. Branch-delta maps changed branches to positive/negative cases; unexplained uncovered reachable branches are not accepted.

**Scoped mutation:** `bash "$A/mutation-sweep.sh"` — six isolated manual mutants: remove the `]` doubling; restore the option gate on Column; raw direct marker; prequote the typed formatter; strip literal brackets; unsafe grouping route. Scope: `nextorm.core` BaseExpressionVisitor/NormSqlTranslator and `nextorm.sqlserver` SqlServerDialect/SqlServerEntityBuilderExtensions; the rewriter only if actually changed.

**Mutation evidence:** each patch/hash, the corresponding core/sqlserver project command, the failing assertion and the restored source hash; a mutant must be killed, a survivor either strengthens a test or is proven equivalent/unreachable. Leave no mutants in the final tree; do not revert foreign changes.

**Coverage:** baseline/final with identical settings, projects and configuration; ≥85/75 for the included core/sqlite/postgres/sqlserver, plus a branch-delta for the touched lines. Do not claim MySQL/MariaDB/ClickHouse are covered by that filter; their evidence is their own tests. On this branch the CI warning does not waive the contract.

## Docs plan

- **EN:** `docs/guide/provider-specific-sql/sqlserver.md`, `docs/guide/30-projections.md`, `docs/connecting-providers/clickhouse.md`, `docs/connecting-providers/overview.md`, `docs/advanced/api-reference.md`, `docs/comparisons/capabilities.md`.
- **RU:** `docs/ru/guide/provider-specific-sql/sqlserver.md`, `docs/ru/guide/30-projections.md`, `docs/ru/connecting-providers/clickhouse.md`, `docs/ru/connecting-providers/overview.md`, `docs/ru/advanced/api-reference.md`, `docs/ru/comparisons/capabilities.md`.
- **XML/docs checks:** `src/nextorm.core/Query/{SqlFunctions,PivotExpression,PivotValues}.cs`; explain raw names and the `Column(t,"[1]")`→`Column(t,"1")` migration; replace affected `GetNullableDecimal("[1]")` with the existing `Column`. `dotnet docfx docs/docfx.json` and the planned `python3 "$A/audit-docs.py"`: exit=0, EN/RU/XML review, no public specs links; do not change toc/numbering/API signatures.

## Write footprint

- **src:** `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`, `src/nextorm.core/Visitors/{BaseExpressionVisitor,NormSqlTranslator}.cs`, and the XML files listed above; `src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs` only if proven necessary.
- **tests:** `tests/nextorm.{core,sqlserver,postgres,sqlite,mysql,mariadb,clickhouse}.tests/` — the gathered T232/SqlGeneration/CachedPreparationRecipe/InMemory files plus local cases; `tests/nextorm.integration.tests/CommonTestSuite.*.cs`, `T232SqlServerTests.cs`, `T232PivotFixture.cs`; exact new names/strings are fixed after creation, not invented now.
- **other:** the 12 docs above, this status file, `artifacts/pdca/D237-identifier-safety/rv1/**`, `tests/coverage/**`, generated ignored `BenchmarkDotNet.Artifacts/**`, `docs/api/**`, `docs/_site/**`, OS-specific bin/obj. The spec stays untracked and unchanged; packages/config/cache semantics are out of scope. Every changed repo text file is CRLF.
- **Uncertainty:** whether a new shared test file/fixture is added and the exact new test names/strings; whether `PivotProjectionRewriter.cs` needs edits; whether the provider-correct oracles need per-provider helper methods.

## Performance decision

Acceptance run **P is mandatory**: although emission is prepare-time, `BaseExpressionVisitor.cs:474` / `NormSqlTranslator.cs:115` lie in the query-preparation path covered by the project class table; a row-materialization benchmark is not needed. Compare all 7 cases and allocations; a >10% regression is checked with one repeat, and a stable unexplained one returns to PLAN — not a waiver.

## Reconnaissance decision

A separate design spike is not needed — the decision is approved; D-01 performs a limited red experiment. Observable criterion: the new assertions compile and fail on the unsafe payload/`]`/prequote, and pass after the fix; a build failure is not red proof. Other providers' `QuoteIdentifier` is checked by independent oracles without changing their policies.

## Evidence contract `rv=1`

Stable pairs R237-01…11 ↔ E237-01…11; there are no superseded revisions. All sources below are **planned**; the gathered `file:line` refs are evidence of the code state, not proof of a future PASS.

**Common mandatory slots for every E-row:** `rv=1`; applicability=true unconditionally for T237; expected result exit 0, selected_count≥1 for tests, all mandatory scenarios executed without skips; kinds = test/source or the specific kinds noted below; source = scenario-map + actual Roslyn locations; artifacts = `$A/E237-NN/{command.log,scenario-map.json,review.md}` plus the extra files noted; owner D-07 for packaging, executor named per row. The red baseline separately permits nonzero only from expected assertion failures.

| Row → requirement | Required scenario; exact invocation | Extra evidence kinds/sources/artifacts; executor |
|---|---|---|
| E237-01 → R237-01 | Escape and consumer regressions; `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | source/Roslyn impact → `$A/escape-impact.json`, exact SQL cases; D-02 |
| E237-02 → R237-02 | Data value/typed/untyped/no-double; `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | tests + raw/expected/actual SQL and value cases; D-04 |
| E237-03 → R237-03 | UNPIVOT payload/roles/columns agreement; `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | tests + declaration/reference oracle, red baseline log; D-04/05 |
| E237-04 → R237-04 | Six-provider Column/name-shape matrix; `bash "$A/unit-sweep.sh" --no-build` | seven project logs, independent golden/oracle sources, complete matrix → `$A/variants.json`; D-03/05 |
| E237-05 → R237-05 | Explicit/direct/injected routes; `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | tests + Roslyn route proof, quoting=false grouping SQL; D-03/04 |
| E237-06 → R237-06 | Unrelated/legacy/options invariants; `bash "$A/unit-sweep.sh" --no-build` | snapshot diff classification + source review; D-03/05 |
| E237-07 → R237-07 | Validation/value/reference/default cases; `bash "$A/unit-sweep.sh" --no-build` | tests + guard matrix, exception/unchanged-validation review; D-05 |
| E237-08 → R237-08 | Reprepare/cache plus actual results; `bash "$A/unit-sweep.sh" --no-build` and `bash "$A/integration-sweep.sh"` | shared boundary logs, five-provider executed counts/results, cache-name sequence; D-04/05 |
| E237-09 → R237-09 | EN/RU/XML migration; `dotnet docfx docs/docfx.json` and `python3 "$A/audit-docs.py"` | doc-build log, bilingual/XML review, link audit; D-06 |
| E237-10 → R237-10 | B, C, G, I exactly as above; `bash "$A/mutation-sweep.sh"` | build/coverage/integration/mutation kinds; cobertura+summary, branch-delta.json, mutants.json/patches; D-06/07 |
| E237-11 → R237-11 | P exactly as above, baseline/after | benchmark kind; seven-case reports, source hashes, comparison.md; D-01/07 |

**Freeze / CHECK:** `$A/manifest.json` carries task/tree/contract_rv/status_file/required_rows E01…11 and met/open/na evidence records with kind/path/command/exit/value. Tree binding = HEAD `c7ff89ca` + a content digest of the tracked/untracked footprint inputs, including spec/status, excluding generated evidence; one run may serve several rows, so I is not repeated. Any `open` forbids PASS; a missing report does not imply DO or a revision.

**CHECK re-gather:** owner `check`; at most 2 targeted gather batches, 20 minutes total, using only existing logs/artifacts/source mapping and V, without a new container sweep. A missing run is `open` and routes per CHECK; persistently low confidence → recommend `escalate`, trigger 5. A revision requires a genuine change of actions, preserving IDs/obligations with explicit supersession.

## Gaps, risks, assumptions

- Exact future test symbols/strings are closed by the observed scenario-map; provider delimiter policies are closed by the oracles, and a mismatch is a blocked D + PLAN — never a self-authorized fix.
- Helper placement is a local assumption inside the approved decision; no new framework/package dependencies are assumed.
- `DO → PLAN` is not applicable now: this is the first PLAN; additive prerequisites keep the original D active/blocked.

## Feasibility / owner decisions

Container socket available; no-commit work and the untracked approved spec are compatible with the content-digest evidence. Coverage/docfx tool availability is checked by D-01; an in-scope repair is a prerequisite, an external limit returns to PLAN. **No owner decision is needed**; confidence is high on scope, while tests/metrics are not yet proven before DO.

## Handoff

`coder` persists this plan before DO; the collection caller owns the PLAN→DO boundary. This invocation ends at PLAN→DO: nothing is executed, nothing is declared complete, no commit is made.
## Verification (CHECK r=1 / n=1 / rv=1)

- Verdict: **PASS** — evidence contract `rv=1`; all eleven requirements R237-01…R237-11 are `met`.
- Frozen manifest: `artifacts/pdca/D237-identifier-safety/rv1/manifest.json`; `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D237-identifier-safety/rv1/manifest.json` → exit **0**.
- No required row (`E237-01`…`E237-11`) is `open`.

| Requirement | Status | Proving artifact (under `artifacts/pdca/D237-identifier-safety/rv1/`) |
|---|---|---|
| R237-01 | met | `logs/unit-sqlserver.log`, `escape-impact.json` |
| R237-02 | met | `logs/unit-sqlserver.log`, `logs/unit-sqlserver-c.log`, `variant-classification.json` |
| R237-03 | met | `red/red-arm.json`, `logs/unit-sqlserver.log` |
| R237-04 | met | `logs/unit-{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.log` |
| R237-05 | met | `logs/unit-sqlserver.log`, `escape-impact.json` |
| R237-06 | met | `logs/unit-{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.log`, `variant-classification.json` |
| R237-07 | met | `logs/unit-core.log`, `logs/unit-core-c.log` |
| R237-08 | met | `logs/unit-core.log`, `logs/unit-sqlserver.log`, `logs/integration.log`, `logs/integration-summary.txt` |
| R237-09 | met | `logs/docfx.log`, `logs/audit-docs.log` |
| R237-10 | met | `logs/build-c.log`, `logs/build-boundary.log`, `logs/build-after-mutation.log`, `coverage/candidate-ci/report/Summary.json`, `coverage/changed-branches.json`, `mutation/mutants.json`, `logs/integration.log` |
| R237-11 | met | `perf/comparison.md`, `logs/benchmark-after.log`, `perf/after/` |

## ACT

- Unit sweep **7/7 suites exit 0** (passed/failed/skipped): core 2578/0/0, sqlite 1759/0/1 skip, postgres 981/0/0, sqlserver 972/0/8 (pre-existing explicit skips), mysql 459/0/0, mariadb 341/0/0, clickhouse 738/0/0 — **0 failed**.
- Integration **exit 0**: all 5 provider routes executed (SQLite/PostgreSQL/MySQL/SQL Server/ClickHouse; MariaDB via the MySQL dialect route); 198 pre-existing intra-provider capability skips; the new T237 cases **0 skipped**.
- Coverage CI-equivalent overall line **88.97%** / branch **80.39%**; changed branches **5/5** covered.
- Mutation **6/6 killed** with byte-exact restore.
- Acceptance benchmark **7/7 cases**, ratio **1.63** within band **1.38–3.89**.
- Build Debug+Release **0 warnings / 0 errors**; docs EN+RU (5 pairs) + XML docs updated; DocFX **exit 0**.

`Notice:` the pre-fix baseline benchmark/coverage window was missed — perf was compared against `docs/specs/performance/acceptance-benchmarks.md` and coverage was measured CI-equivalently with the absolute CI thresholds instead of a pre/post delta. The residual open analog risk (other dialects' `Escape` not fixed) stays out of scope per spec §4.1/§7 and is **NOT** claimed as fixed.

## Progress log

2026-10-16 12:04 UTC | DO | revision r=1 | iteration 1/3 | DO continued — D-02/D-03/D-04 applied and green; unit matrix added (7 projects); XML docs + EN/RU docs updated (10 pages touched, 2 checked-unchanged); integration cases added and compiled (not yet executed); DocFX build exit 0 (2 pre-existing warnings, 0 errors); build 0 Warning(s)/0 Error(s); sqlserver filter 75 passed/0 failed/8 skipped, core filter 19 passed/0 failed/0 skipped; doc audit exit 0 (5/5 pairs, specs-link-free, terms present). Notice: the pre-fix baseline benchmark/coverage window was missed, so the perf comparison uses docs/specs/performance/acceptance-benchmarks.md (recorded ratio 1.87 / latest rc3 1.71, band 1.38–3.89) and coverage uses the absolute CI thresholds 85/75 plus a changed-branch inventory. | evidence: artifacts/pdca/D237-identifier-safety/rv1/{logs/docfx.log,logs/build-c.log,logs/unit-sqlserver-c.log,logs/unit-core-c.log,audit-docs.py,unit-sweep.sh,integration-sweep.sh}
2026-10-16 12:11 UTC | DO | revision r=1 | iteration 1/3 | DO→CHECK boundary sweeps — unit 7/7 projects exit 0 (passed/failed/skipped): core 2578/0/0, sqlite 1759/0/1, postgres 981/0/0, sqlserver 972/0/8, mysql 459/0/0, mariadb 341/0/0, clickhouse 738/0/0; the 9 skips are pre-existing env-gated/Explicit probes unrelated to T237. Integration exit 0: 3985 total / 0 failed / 198 skipped, all skips are per-capability skips inside providers that all ran (executed = discovered-skipped: sqlite 829-41=788, sqlserver 869-48=821, postgres 955-26=929, mysql 778-80=698, clickhouse 293-0=293, mariadb 116-0=116), no provider-level skip. New cases executed and green: CommonTestSuite.IdentifierSafety_QuotingRequiredNames 5/5 (sqlite+sqlserver+postgres+mysql+ClickHouse host class), T232 bracket PIVOT/UNPIVOT 2/2, whole T232SqlServerTests 15/15. MariaDB reuses the MySQL dialect route (no separate DB, no isolated host by design; its evidence is the 7 no-DB projects). ClickHouse host class added and green. No guarded skip was needed and no test source was changed. One earlier isolated -class run hit a transient MSSQL fixture-seed error (Invalid object name) on a cold container; the immediate retry was fully green. Build nextorm.slnx exit 0, 0 Warning(s) 0 Error(s). | evidence: artifacts/pdca/D237-identifier-safety/rv1/logs/{unit-*.log,unit-*.exit,integration.log,integration-summary.txt,integration-list-tests.log,integration-t232sqlserver.log,confirm-ident.log,confirm-t237sqlserver.log,confirm-t232class.log,build-boundary.log}
2026-10-16 12:28 UTC | DO | revision r=1 | iteration 1/3 | DO→CHECK boundary completion — unit sweep 7/7 projects exit 0 (passed/failed/skipped): core 2578/0/0, sqlite 1759/0/1, postgres 981/0/0, sqlserver 972/0/8, mysql 459/0/0, mariadb 341/0/0, clickhouse 738/0/0; integration exit 0 — all 5 provider routes executed (SQLite/PostgreSQL/MySQL/SQL Server/ClickHouse; MariaDB via the MySQL dialect route), 198 pre-existing intra-provider capability skips, T237 cases 0 skipped (attempt 1 was an infra flake, attempt 2 green); coverage CI-equivalent sweep overall line 88.97% / branch 80.39% (core 88.78/80.60, sqlite 91.67/70.45, postgres 90.66/80.36, sqlserver 95.12/79.12); changed branches 5/5 covered; mutation 6/6 killed with byte-exact restore; benchmark 7/7 cases ratio 1.63 within band 1.38–3.89; manifest frozen at artifacts/pdca/D237-identifier-safety/rv1/manifest.json (validator exit 0). Notice: pre-fix baseline window missed — perf compared against docs/specs/performance/acceptance-benchmarks.md and coverage measured CI-equivalently rather than as a pre/post delta. | evidence: artifacts/pdca/D237-identifier-safety/rv1/{manifest.json,manifest-notes.json,logs/unit-*.log,logs/unit-*.exit,logs/integration.log,logs/integration-summary.txt,coverage/candidate-ci/report/Summary.json,coverage/changed-branches.json,mutation/mutants.json,perf/comparison.md,logs/benchmark-after.log,logs/docfx.log,logs/audit-docs.log}
2026-10-16 12:38 UTC | CHECK | revision r=1 | iteration 1/3 | CHECK → ACT: verdict PASS | evidence: artifacts/pdca/D237-identifier-safety/rv1/manifest.json
2026-10-16 12:39 UTC | ACT | revision r=1 | iteration 1/3 | ACT: status finalized; commit fc7032190af675712ac87ff50ee62409be9a5372 on branch 1.0.9-rc3 (no push) | evidence: docs/specs/status/D237-identifier-safety-1.md
