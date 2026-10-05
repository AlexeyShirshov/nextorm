# D181 — SQLite full-text (FTS3/FTS4/FTS5)

- task: D181
- issue: #181 (https://github.com/AlexeyShirshov/nextorm/issues/181)
- collection: 1.0.9-rc1
- group: G01
- branch: 1.0.9-rc1
- cycle: N=1, revision r=3, attempt n=2/3
- evidence contract: rv=D181.r3.ec1 (supersedes rv=D181.r2.ec1)
- autonomy: autonomous + auto-commit authorized
- push/merge: not authorized

## Goal
Add a SQLite full-text (FTS3/FTS4/FTS5) surface: query functions (Match, FTS5 bm25/highlight/snippet/rank, FTS3/4 helpers), the FROM-source/table-function form, an `ISqlDialect` capability gate; docs EN+RU + tests.

## Reconnaissance (persisted; not yet re-verified)
- Issue #181 "TODO: SQLite full-text (FTS3/FTS4/FTS5)"; Open; enhancement; milestone 1.0.9-rc1 (#19); no comments/branches/PRs. Body: SQLite is the one engine with no full-text surface; linq2db `SQLiteExtensions.cs` ~900 lines (FTS5 `Match`/`FTS5bm25`/`Highlight`/`Snippet`/`Rank`/`AutoMerge`/`CrisisMerge`/`Merge`/`Optimize`/`Rebuild`/`IntegrityCheck`; FTS3/4 `Match`/`MatchTable`/`Rank`/`RowId`/`FTS3Offsets`/`FTS3MatchInfo`/`FTS3Snippet`). Draft scope: FTS5 query surface, FTS3/4 helpers, FROM-source/table-function form, `ISqlDialect` capability gate, docs EN+RU + tests.
- **No FTS surface exists**: `src/nextorm.core/Query/SqlFunctions.Sqlite.cs` (274 lines; `SqlFunctions.Sqlite` `:12`, `SqliteFunctions` `:83-274`) has only core-scalars/JSON1/date/math; `src/nextorm.sqlite/SqliteFunctionRenderer.cs` `SupportedNames` `:14-25`, `Render` switch `:31-37` — no FTS.
- Capability/gates: `ISqlDialect.SupportsFullText` `ISqlDialect.cs:298`, default false `SqlDialectBase.cs:100`, SQLite does not override; cross-provider `contains`/`freetext` `BuiltinFunctionTranslator.cs:79-82,416-435`, `MakeFullText` `ISqlDialect.cs:744`. Provider-function dispatch: `SqliteFunctionTranslator.cs:31-59`; `ISqliteFunctions.Supports/Render` `DialectCapabilities.cs:641-652`; `ISqlDialect.SqliteFunctions` DIM `ISqlDialect.cs:273`; `SqlDialectBase.cs:90`; `SqliteDialect.cs:119`.
- FROM-source/table-function form: `[SqlTableFunction("json_each")]` placeholders `SqlFunctions.Sqlite.cs:257-273`; gate `SqliteDialect.cs:122`; `SqlTableFunctionAttribute.cs:39,95,107`. Built-in `SqliteFunctions` members are **attribute-less** (dispatched by `SqliteFunctionTranslator`); `[SqlFunction]` is the unrelated user-UDF path — do not conflate.
- Tests: `tests/nextorm.sqlite.tests/SqliteFunctionsSqlGenerationTests.cs:7` (analogs `JsonScalars:125`, `JsonTableFunctions:187`), `SqlGenerationTests.cs:2715 FullTextPredicates_ShouldThrowBecauseSqliteLacksThem`, `tests/nextorm.core.tests/InMemorySqliteFunctionsTests.cs:9`, `tests/nextorm.integration.tests/SqliteSpecificTests.cs:633-771` (no FTS).
- Docs EN+RU: `docs/providers/sqlite.md:113-130,132-151,155-171` + RU; `docs/guide/provider-specific/sqlite.md:1-161` + RU; `docs/providers/overview.md:84-85,110` + RU; `docs/advanced/limitations.md:78,80`; specs `comparison/provider-feature-comparison.md:54-55,172-178` (explicit #181), `evidence-01-provider-specific-features.md:93`, `capability-matrix.md:88`, `linq2db-comparison.md:82,192`; no `scalar-functions` SQLite FTS section.
- In-memory: `SqlFunctions.Sqlite` fully provider-specific; `InMemoryScalarFunctionRewriter.cs:43-64`; FTS has no CLR equivalent → SQL-only.
- No `todo_*fts*` file. Collection `docs/specs/status/collection-1.0.9-rc1.md:23` D181 in-progress.
- Open design questions for PLAN: exact scope (FTS5 query functions vs all linq2db ~900-line surface incl. merge/optimize/rebuild/integrity-check); how the FROM-source/table-function form is expressed; which capability gate to extend (`SupportsFullText` vs `ISqlDialect.SqliteFunctions`); whether index DDL/creation is in scope; in-memory disposition.

## Locked plan r=2
### Locked decision
Escalate option A: #181 closes with FTS3/4/5 **query + FROM-source** support; FTS5 maintenance/control (`AutoMerge`, `CrisisMerge`, `Merge`, `Optimize`, `Rebuild`, `IntegrityCheck`) split into separate mandatory 1.0.9-rc1 units M1/M2 (created before closing #181).

### Acceptance criteria R181-01..R181-10
- R181-01 FTS5 `Match`, `FTS5bm25`, `Highlight`, `Snippet`, `Rank` translate+execute vs real FTS5 table, results equal native SQL; no CLR eval; malformed syntax not silently empty.
- R181-02 FTS3 AND FTS4 `Match`, `MatchTable`, `Rank`, `RowId`, `FTS3Offsets`, `FTS3MatchInfo`, `FTS3Snippet` execute vs native; not represented as FTS5-only.
- R181-03 FTS5 table-valued `[SqlTableFunction]` search (params, alias, composition) + ordinary FTS3/4 `FROM … WHERE … MATCH`; no table identifier from untrusted text.
- R181-04 dispatch via `ISqlDialect.SqliteFunctions`; non-SQLite throws `NotSupportedException`; `SupportsFullText` stays false; cross-provider `contains`/`freetext` remain unsupported.
- R181-05 in-memory: every new SQL-only FTS expression throws explicit `NotSupportedException`; existing supported in-memory functions unaffected.
- R181-06 rename/reword `SqlGenerationTests.cs:2715 FullTextPredicates_ShouldThrowBecauseSqliteLacksThem` to distinguish cross-provider predicates from SQLite FTS; rejection behavior retained.
- R181-07 EN/RU docs + specs accurate; no blanket "SQLite lacks full text"; no public→specs links; no guide renumbering.
- R181-08 bundled SQLite creates FTS3/FTS4/FTS5 and executes scenarios; module absence is a blocker, not a skip.
- R181-09 M1/M2 exist, milestone 1.0.9-rc1, link #181, verified URLs before #181 closes.
- R181-10 Debug/Release builds, tests, coverage (line ≥85/branch ≥75), docfx (`--warningsAsErrors`).

### Locked ranking semantics
FTS5 `Rank(table)` → hidden `rank` column. FTS3/4 `Rank(matchInfo)` → `rank(matchinfo(...))` via a connection-registered SQL UDF `rank`; real-SQLite tests register a deterministic UDF and compare to native SQL; docs include the registration prerequisite. No client fallback.

### DO units
D181-P0 persist; D181-D1 query decl/render (`SqlFunctions.Sqlite.cs:83-274`, `SqliteFunctionRenderer.cs:14-25,31-37`, `SqliteFunctionTranslator.cs:31-59`); D181-D2 table-source/capability (`SqliteDialect.cs:119,122`, `SqlFunctions.Sqlite.cs:257-273`, `SqlTableFunctionAttribute.cs:39,95,107`, `ISqlDialect.cs:273,298`, `SqlDialectBase.cs:90,100`); D181-D3 tests (`SqliteFunctionsSqlGenerationTests.cs:125,187`, `SqlFunctionsRejectionTests.cs`, `InMemorySqliteFunctionsTests.cs`, `SqlGenerationTests.cs:2715`; tag `Issue=181`); D181-D4 real SQLite (`SqliteSpecificTests.cs` after `:771`); D181-D5 docs; D181-D6 follow-up registration M1/M2; D181-D7 boundary/handoff. Deferred (same milestone, triggers): M1 (command-surface, trigger = query support passes CHECK), M2 (tests+docs for M1, trigger = M1 contract fixed).

### Test strategy / matrix
Layers: SQL-gen, provider rejection, in-memory rejection, real SQLite (no container), boundary regression (containers for PG/MSSQL/MySQL/CH; skips ≠ pass), coverage 85/75. Variant matrix per the planner's closed table (FTS5 5 funcs; FTS3/FTS4 7 funcs; Rank forms; with/without UDF; table vs column MATCH; FROM/alias/projection; optional args; BM25 weights; nulls; Unicode/quoted/phrase/prefix/boolean; malformed; matchinfo formats; mapped entities; unmapped token rejection; other providers; in-memory; cross-provider predicates; maintenance deferred; module availability). Oracle = same parameterized native statement on the bundled SQLite connection.

### Priority
All R181-01..R181-10 and dispatch/capability/SQL-only/parameterization/table-token/module/build/coverage rows are P1; CHECK may not downgrade.

### Docs plan
`docs/providers/sqlite.md:113-130,132-151,155-171` + RU; `docs/guide/provider-specific/sqlite.md:1-161` + RU; `docs/providers/overview.md:84-85,110` + RU; `docs/advanced/limitations.md:78,80` + RU; specs `comparison/provider-feature-comparison.md:54-55,172-178`, `evidence-01-provider-specific-features.md:93`, `capability-matrix.md:88`, `linq2db-comparison.md:82,192`. Untouched: guide numbering, generated docs.

### Perf / recon decisions
Perf: not required (translation/query preparation only; native SQLite does search/scoring; no CLR rank). Recon: not required (binding decision + facts fix integration points; module availability is a hard test).

### Evidence contract `rv=D181.r2.ec1`
Command catalog: C-BD `dotnet build -c Debug`; C-BR `dotnet build -c Release`; C-US/C-UP/C-UM/C-IS `dotnet run --project tests/nextorm.<x>.tests -c Debug -- -trait "Issue=181" -noColor`; C-ALL `DOCKER_HOST=… dotnet test -c Debug`; C-COV `dotnet-coverage collect "dotnet test -c Debug" --settings coverage.settings.xml --output artifacts/d181/coverage.cobertura.xml --output-format cobertura`; C-REPORT `reportgenerator …`; C-DOC `dotnet docfx docs/docfx.json --warningsAsErrors`; C-DIFF `git diff --check`; C-PATCH `git diff -- src tests docs`.
Rows `E181-01..E181-12` (build; SQL-shape/overload/param; provider rejection + renamed regression; in-memory rejection; module availability; real execution/native equivalence/ranking; boundary regression; coverage; docs+docfx; design/invariant review+CRLF; verified M1/M2 tracking; final CHECK adjudication) with owner (DO or CHECK), applicability always, and artifact paths under `artifacts/d181/`.

### Follow-up creation
`gh issue create --repo "$REPO" --title "FTS5 maintenance/control command surface — follow-up to #181" --milestone "1.0.9-rc1" --body …` and the M2 equivalent; verify with `gh issue view`; comment on #181. One-shot creation; reconcile before retry.

### CHECK re-gather budget
2 targeted requests, owner CHECK.

### Assumptions / blockers
Bundled FTS availability (blocker if absent), FTS3/4 ranking UDF contract, GitHub permission/milestone, container/tool prerequisites, preser/report IDs, actual test symbols reported later.

## Progress log
- Recon persisted (run 3 start).
- Notice: host has no todowrite tool; this file is the progress log.
- PLAN r=2 locked (r+1); evidence rv=D181.r2.ec1; awaiting DO.
- FTS module availability: bundled SQLite 3.53.4; fts5/fts4/fts3 all OK — artifacts/d181/fts-module-availability.log, artifacts/d181/fts-module-probe-run.log; SQL-shape probe artifacts/d181/fts-sql-shape.log, artifacts/d181/fts-smoke-run2.log.
- D1/D2 code landed (working tree, uncommitted): `src/nextorm.core/Query/SqlFunctions.Sqlite.cs`, `src/nextorm.core/Visitors/SqliteFunctionTranslator.cs`, `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs`, `src/nextorm.sqlite/SqliteDialect.cs`, `src/nextorm.sqlite/SqliteFunctionRenderer.cs`.
- R181-05 (in-memory rejection) fixed: `InMemoryScalarFunctionRewriter.cs` now early-throws `NotSupportedException` for the SQLite FTS names (Match, MatchTable, FTS5bm25, Highlight, Snippet, Rank, RowId, FTS3Offsets, FTS3MatchInfo, FTS3Snippet). Throwaway probes removed (`D181FtsInMemoryProbeTests.cs`, `D181FtsSmokeProbeTests.cs`); `git status` shows no probe files.
- D181-D6 follow-ups created in milestone 1.0.9-rc1: M1 #195 "FTS5 maintenance/control command surface — follow-up to #181" https://github.com/AlexeyShirshov/nextorm/issues/195; M2 #196 "FTS5 maintenance real-SQLite tests and EN/RU docs — follow-up to #181" https://github.com/AlexeyShirshov/nextorm/issues/196 (depends on #195). Comment on #181: https://github.com/AlexeyShirshov/nextorm/issues/181#issuecomment-6003233665.
- Final build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s), 0 Error(s) — artifacts/d181/final-build.log, artifacts/d181/final-build-exit.txt.
2026-10-05T21:25Z | DO | revision r=2 | iteration n=1/3 | FTS module availability verified: SQLite 3.53.4, fts3/fts4/fts5 OK | artifacts/d181/fts-module-availability.log
2026-10-05T21:25Z | DO | revision r=2 | iteration n=1/3 | D1/D2 query+FROM+capability code landed (uncommitted) | git diff -- src
2026-10-05T21:25Z | DO | revision r=2 | iteration n=1/3 | R181-05 in-memory early rejection added; probe total 1 failed 0 passed 1 exit 0; probes deleted | artifacts/d181/fts-inmemory-run2.log
2026-10-05T21:25Z | DO | revision r=2 | iteration n=1/3 | D181-D6 M1 #195 + M2 #196 created (milestone 1.0.9-rc1) and #181 comment posted | artifacts/d181/m1-create.log, artifacts/d181/m2-create.log, artifacts/d181/181-comment.log
2026-10-05T21:25Z | DO | revision r=2 | iteration n=1/3 | final solution build exit 0 0W/0E | artifacts/d181/final-build.log

### D181-D4 continuation — real-SQLite FTS failures diagnosed and fixed (2026-10-06)
- Root cause (test-construction, not production). Execution-mode SQL captured with a temporary prepare-only dump in `QueryExecutor.RunReader` (`artifacts/d181/diag-commandtext.log`). One failing shape rendered:
  `select rowid from fts5_docs` / ` where fts5_docs MATCH $query` / ` order by ` (trailing empty ORDER BY) → `SQLite Error 1: 'incomplete input'`.
  The FTS `Match` table token was emitted verbatim (not parameterised) and the predicate parameter bound correctly (`PARAM query = [hello]`), so execution-mode FTS rendering matches SQL-gen mode.
- Actual defect in the D4 tests: `.Select(x => x.RowId).OrderBy(id => id)` orders by the projected scalar's identity lambda, which the query builder does not support and renders as `order by ` with no column. Confirmed general/pre-existing on a non-FTS entity (`artifacts/d181/diag-scalar-orderby.log`: `.Select(x => x.Int).OrderBy(id => id)` → `... order by `; `.OrderBy(x => x.Int).Select(x => x.Int)` → `... order by nullableint`). Unrelated to #181 and outside D181 scope; noted as a separate query-builder gap, not a D181 blocker.
- Fix (test wiring, `tests/nextorm.integration.tests/SqliteSpecificTests.cs`): all D4 queries that ordered by the projected scalar now order on the source member before projection (`.OrderBy(x => x.RowId).Select(x => x.RowId)`); `Fts5_Highlight`/`Fts5_Snippet` order by `rowid` and their native oracles were aligned to `order by rowid`; `Fts5_Bm25` no longer compares weighted BM25 to unweighted but to a native weighted `bm25(fts5_docs, 1.0, 2.0)` oracle. No production FTS source changed.
- D4 result: `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class NextORM.Integration.Tests.SqliteSpecificTests -noColor` → exit 0, total 60, passed 60, failed 0, skipped 0 — artifacts/d181/d4-sqlite-fts-rerun.log.
- R181-06 renamed-test result: `dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- -method "*FullText*" -noColor` → exit 0, total 1, passed 1, failed 0 (`CrossProviderFullTextPredicates_ContainsFreetext_ShouldThrowBecauseSqliteLacksThem`); `--filter` is rejected by the xunit v3/MTP runner, so the native `-method` selector was used — artifacts/d181/r181-06-fulltext-rerun.log.
- Final `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s), 0 Error(s) — artifacts/d181/d4-final-build.log. Throwaway diagnostics removed: `QueryExecutor.cs` unchanged (not in `git status`), probe test deleted.
2026-10-06T02:36Z | DO | revision r=2 | iteration n=1/3 | D4 real-SQLite FTS failures fixed (test wiring: empty ORDER BY from post-projection identity ordering); D4 60/60 exit 0; R181-06 FullText 1/1 exit 0; build 0W/0E | artifacts/d181/d4-sqlite-fts-rerun.log
2026-10-06T02:40Z | DO | revision r=2 | iteration n=1/3 | D5 docs updated EN+RU: provider sqlite FTS query surface + ranking UDF prerequisite + maintenance deferred #195/#196, guide provider-specific/sqlite full FTS5 and FTS3/4 sections, overview matrix SQLite FTS row, limitations (cross-provider contains/freetext unsupported, SQLite FTS available, maintenance deferred), specs provider-feature-comparison/evidence-01/capability-matrix/linq2db-comparison mark #181 query+FROM shipped with boundary; docfx --warningsAsErrors exit 255, 0 errors, 2 pre-existing duplicate-source MSBuild warnings, no xref/link warnings | artifacts/d181/docfx.log

## D7 boundary evidence (2026-10-06, revision r=2, iteration n=1/3)

### Builds
- C-BD `dotnet build nextorm.slnx -c Debug` -> exit 0, 0 Warning(s), 0 Error(s) — artifacts/d181/build-debug.log, artifacts/d181/build-debug-exit.txt
- C-BR `dotnet build nextorm.slnx -c Release` -> exit 0, 0 Warning(s), 0 Error(s) — artifacts/d181/build-release.log, artifacts/d181/build-release-exit.txt

### Provider unit projects (Debug, Microsoft.Testing.Platform runner, `dotnet test <proj> -c Debug --no-build`)
| project | exit | total | passed | failed | skipped | log |
| --- | --- | --- | --- | --- | --- | --- |
| nextorm.core.tests | 0 | 1547 | 1547 | 0 | 0 | artifacts/d181/unit-core.log |
| nextorm.sqlite.tests | 0 | 1045 | 1044 | 0 | 1 | artifacts/d181/unit-sqlite.log |
| nextorm.postgres.tests | 0 | 767 | 767 | 0 | 0 | artifacts/d181/unit-postgres.log |
| nextorm.sqlserver.tests | 0 | 560 | 560 | 0 | 0 | artifacts/d181/unit-sqlserver.log |
| nextorm.mysql.tests | 0 | 273 | 273 | 0 | 0 | artifacts/d181/unit-mysql.log |
| nextorm.mariadb.tests | 0 | 181 | 181 | 0 | 0 | artifacts/d181/unit-mariadb.log |
| nextorm.clickhouse.tests | 0 | 491 | 491 | 0 | 0 | artifacts/d181/unit-clickhouse.log |
Per-project exit files: artifacts/d181/unit-<proj>-exit.txt. A single `dotnet test` at the boundary would duplicate these; the brief's unit-only integration question is subsumed by C-ALL below.

### Container integration (mandatory; skipped != pass)
- Command: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml artifacts/d181/integration-results.xml`
- Result: exit 0, Total 3159, Passed 2966, Failed 0, Skipped 193, Not Run 0, 44.880s — artifacts/d181/integration.log, artifacts/d181/integration-exit.txt, artifacts/d181/integration-results.xml
- Podman machine `podman-machine-default` `Currently running`; socket ping `OK`.
| provider | passed | skipped | failed | ran? |
| --- | --- | --- | --- | --- |
| SQLite (no container) | 638 | 43 | 0 | YES |
| PostgreSQL | 751 | 25 | 0 | YES |
| SQL Server | 655 | 43 | 0 | YES |
| MySQL | 579 | 79 | 0 | YES |
| MariaDB | 50 | 0 | 0 | YES |
| ClickHouse | 173 | 0 | 0 | YES |
| cross-provider/EfCore/core-contract | 120 | 3 | 0 | YES |
- SQLite FTS cases executed: 16/16 Pass (all in `NextORM.Integration.Tests.SqliteSpecificTests`), 0 skipped: Fts5_TableMatch; Fts5_ColumnMatch; Fts5_MatchTableFromSource; Fts5_AdvancedQuerySyntax; Fts5_MalformedQuery_ShouldThrowTheSameNativeError; Fts5_RankHiddenColumn; Fts5_Bm25 (weighted vs native weighted oracle); Fts5_Highlight; Fts5_Snippet; Fts4_Match; Fts4_RankOverMatchInfo (UDF); Fts4_RankOverMatchInfo_ShouldFailWithoutUdf; Fts4_Helpers; Fts3_Match; Fts3_RankOverMatchInfo (UDF); Fts3_Helpers. SQLite FTS needs no container and ran in this suite.
- Skipped 193 = 43 SQLite + 25 PG + 43 MSSQL + 79 MySQL + 2 LobCapabilityProbe + 1 LobPerfHarness (provider capability skips, not failures).

### Coverage (CI recipe, no DOCKER_HOST)
- C-COV `dotnet tool run dotnet-coverage collect "dotnet test -c Debug" --settings coverage.settings.xml --output artifacts/d181/coverage.cobertura.xml --output-format cobertura` -> exit 0 — artifacts/d181/coverage-collect.log, artifacts/d181/coverage-collect-exit.txt; inner test run total 8211, failed 0, skipped 2501.
- C-REPORT `dotnet tool run reportgenerator "-reports:artifacts/d181/coverage.cobertura.xml" "-targetdir:artifacts/d181/coverage" "-reporttypes:Cobertura;TextSummary"` -> exit 0 — artifacts/d181/coverage-report.log, artifacts/d181/coverage-report-exit.txt, artifacts/d181/coverage/Summary.txt, artifacts/d181/coverage/Cobertura.xml
- Overall: Line 87.1% (45109/51748) vs gate 85 PASS; Branch 78.8% (23305/29548) vs gate 75 PASS. Assemblies 4, classes 554. coverage.settings.xml scopes to nextorm.{core,sqlite,postgres,sqlserver} (mysql/mariadb/clickhouse out of coverage by design).
| changed file | line | branch | note |
| --- | --- | --- | --- |
| src/nextorm.sqlite/SqliteFunctionRenderer.cs | 97.2% | 76.8% | FTS render switch covered |
| src/nextorm.core/Visitors/SqliteFunctionTranslator.cs | 97.3% | 97.4% | FTS dispatch covered |
| src/nextorm.sqlite/SqliteDialect.cs | 82.4% | 54.7% | whole file; composes Iif 100/100, Tuple 100/100, StringFormats 93.8/84.8, ScalarFunctions 95.7/72.8 |
| src/nextorm.core/DataContext/InMemoryScalarFunctionRewriter.cs | 97.2% | 85.1% | new early-throw covered |
| src/nextorm.core/Query/SqlFunctions.Sqlite.cs | 0.0% | 100.0% | declaration-only `SqlFunctions`/`SqliteFunctions`; same 0% pattern as Postgres/MySql/ClickHouse/SqlServer function declaration classes; usable behavior covered via translator/renderer above |

### Git hygiene
- `git diff --check` -> exit 0, no output (no whitespace errors).
- CRLF defect found and fixed at boundary: 13 tracked-modified docs/specs files and this status file were LF-only (unmodified repo docs are CRLF). Normalized with `perl -pi -e 's/\r?\n/\r\n/g'`; re-run `git diff --check` exit 0; content diff unchanged (24 files, +1743/-36).
- `git status --short`: 24 modified tracked files (docs 12, src 5, tests 5, specs 2) + 2 untracked (`artifacts/`, `docs/specs/status/rc1-181-sqlite-fts-1.md`). No commit/push performed.

### Blockers
- None. All builds 0W/0E; all unit + integration suites exit 0 with 0 failures; coverage above 85/75; container providers all executed (no provider skipped wholesale); SQLite FTS 16/16 executed.

2026-10-06T02:40Z | DO | revision r=2 | iteration n=1/3 | boundary builds Debug+Release exit 0 0W/0E | artifacts/d181/build-debug.log, artifacts/d181/build-release.log
2026-10-06T02:40Z | DO | revision r=2 | iteration n=1/3 | 7 provider unit projects exit 0 (core 1547, sqlite 1045/1skip, pg 767, mssql 560, mysql 273, mariadb 181, ch 491) | artifacts/d181/unit-*.log
2026-10-06T02:40Z | DO | revision r=2 | iteration n=1/3 | container integration exit 0 total 3159 passed 2966 failed 0 skipped 193; PG/MSSQL/MySQL/CH/Maria ran; SQLite FTS 16/16 Pass | artifacts/d181/integration.log, artifacts/d181/integration-results.xml
2026-10-06T02:40Z | DO | revision r=2 | iteration n=1/3 | coverage line 87.1%/branch 78.8% (gates 85/75 PASS); changed files 76.8-97.4% branch/line except declaration-only SqlFunctions.Sqlite | artifacts/d181/coverage/Summary.txt
2026-10-06T02:40Z | DO | revision r=2 | iteration n=1/3 | CRLF defect fixed: 13 modified docs/specs LF-only -> CRLF; git diff --check exit 0 | artifacts/d181/git-diff-check.log
2026-10-06T02:44Z | CHECK | revision r=2 | iteration n=1/3 | CHECK gather: slop scan 0 suppression/NoWarn/TODO, 3 added null-forgiving, suppression ratio 0.00%; consolidated packet; plain docfx exit 0 (2 pre-existing dup-source warnings, 0 xref/link) | artifacts/d181/check/slop-scan.txt, artifacts/d181/check/D181-CHECK-packet.md

## CHECK verdict (r=2, n=1) — FAIL
Required-test gaps + a PLAN discrepancy + an evidence gate; no product defect proven. Verdict returned for loop-back; not a PASS.

### Findings / routing
- **P: reconcile R181-02 FTS3/4 `MatchTable` semantics.** R181-02 names FTS3/4 `MatchTable`, but `MatchTable` is implemented FTS5-only (`SqlFunctions.Sqlite.cs:300`). Distinguish table-valued FROM from ordinary table MATCH; any replan must supersede the contract revision and preserve functional obligations — declaring N/A is insufficient. → route to `planner` (r=3).
- **D: close required-test gaps** (bind each to its contract row first, then implement):
  - native hidden-`rank` oracle (current FTS5 rank test is self-referential vs bm25, not native `rank`);
  - FTS5 FROM **alias** (required by R181-03, untested);
  - unmapped/default table-token rejection (`SqliteFunctionTranslator.cs:110-111`, 0% covered);
  - nulls (null table/query/match) — bind to a row;
  - FTS3 column MATCH (FTS4 has it);
  - FTS3 negative rank-without-UDF;
  - FTS3/4 malformed/Unicode/quoted/prefix/boolean (only FTS5 covered);
  - other-provider FTS rejection beyond PostgreSQL;
  - branch gaps: `SqliteFunctionTranslator.cs:107`, `SqliteFunctionRenderer.cs:55`, `SqliteDialect.cs:134/138`.
- **DocFX gate:** the plan's `dotnet docfx docs/docfx.json --warningsAsErrors` exits 255 from 2 **pre-existing** `nextorm.core.sourcegenerator/AnalyzerReleases.*` warnings. Either restore the strict command (fix/suppress the pre-existing warnings, out of D181's own diff) or obtain an authorized `P:` reconciliation scoping the baseline exception. `--warningsAsErrors` exit 255 is not passing.
- **Security candidate #1 (unresolved, proposed P1 if untrusted provenance demonstrated):** `SqliteFunctionTranslator.cs:114-124` accepts any non-empty `ConstantExpression` string as a verbatim table token; rendered raw (`SqliteFunctionRenderer.cs:44-47`, `SqliteDialect.cs:141-143`). Gather the explicit token trust contract + an adversarial test. ClickHouse `VerbatimArguments` precedent is not a waiver.
- **#2 (P2 robustness):** `SqliteFunctionTranslator.cs:91-99` — non-constant/non-column `Match` first arg renders `$p MATCH $q` instead of throwing; parameterized, not injection.
- **Not defects:** PG/IM `MatchTable` weak assertions (generic gate is valid rejection); #4/#8/#9 maintenance/perf candidates (track with triggers); parameterized search text / unchanged `SupportsFullText` / no shared-command mutation (positive).
- **Evidence re-gather (CHECK budget):** exact contract/variant/priority rows, DO ledger, current-tree log paths, DocFX warning baseline, applicable skip reasons.

### Defect history
- `D181-c1`: CHECK FAIL (r=2, n=1) — required-test gaps + R181-02 PLAN discrepancy; no product defect proven. Next DO attempt n=2/3 after any real replan (r=3) or direct test-gap closure.

### Next allowed step
- `planner` reconciles R181-02 (r=3); then `DO` closes the test gaps + DocFX decision; then re-CHECK.

## Locked plan r=3 (supersedes r=2)
- cycle N=1, revision r=3, attempt n=1/3; evidence contract `rv=D181.r3.ec1` explicitly supersedes `rv=D181.r2.ec1`.
- Reconciled decisions of planner r=3:
  1. R181-02 corrected: the FTS3/4 supported set is `Match`, `Rank(matchInfo)`, `RowId`, `FTS3Offsets`, `FTS3MatchInfo`, `FTS3Snippet` (NO `MatchTable`); `MatchTable`/table-valued FROM belongs to R181-03 (FTS5 FROM).
  2. Close the required-test gaps (variant IDs V181-34-*, V181-5-*, V181-TOKEN-*, V181-SHAPE-*, V181-PROVIDER-*, V181-BRANCH-*).
  3. DocFX: C-DOC revised to plain `dotnet docfx docs/docfx.json` exit 0, zero errors, no new warnings, with the 2 pre-existing `nextorm.core.sourcegenerator/AnalyzerReleases.*` duplicate-source warnings as a documented baseline.
  4. Token hardening: render constant table-name tokens as quoted SQLite identifiers with correct escaping (reuse the dialect identifier-quoting mechanism), reject null/empty/unmapped/default tokens; preserve valid mapped names incl. quoted/Unicode; adversarial test.
  5. Invalid `Match` first operand: reject at translation (`NotSupportedException`) instead of `$p MATCH $q`; preserve table-token and mapped-column operands.

### Evidence contract `rv=D181.r3.ec1` — new rows
- E181-13 FTS3/4 variants (per-version `Match`/`RowId`/`FTS3Offsets`/`FTS3MatchInfo`/`FTS3Snippet` incl. column MATCH, negative rank-without-UDF, malformed/Unicode/quoted/prefix/boolean).
- E181-14 FTS5 FROM alias (alias + composition + projection required by R181-03).
- E181-15 native rank oracle (FTS5 hidden `rank` compared to native `rank`, not self-referential vs bm25).
- E181-16 input guards (null/empty/default table/query/match tokens rejected at translation).
- E181-17 token safety (adversarial constant table token with `"`/`;`/comment text rendered as one escaped identifier; sentinel survives).
- E181-18 provider rejection matrix (SQLite FTS surface rejected by PostgreSQL/SQL Server/MySQL/ClickHouse/in-memory).
- E181-19 branch evidence (the CHECK-flagged branch gaps `SqliteFunctionTranslator.cs:107`, `SqliteFunctionRenderer.cs:55`, `SqliteDialect.cs:134/138`).
- Inherited rows E181-01..E181-12 and follow-ups M1 #195 / M2 #196 remain unchanged.

2026-10-06T02:54Z | DO | revision r=3 | iteration n=1/3 | plan r=3 persisted; evidence contract rv=D181.r3.ec1 supersedes rv=D181.r2.ec1; R181-02 reconciled (FTS3/4 no MatchTable), test-gap closure, DocFX baseline, token hardening, Match operand guard | docs/specs/status/rc1-181-sqlite-fts-1.md
2026-10-06T02:56Z | DO | revision r=3 | iteration n=1/3 | token hardening landed: constant FTS table tokens quoted via ISqlDialect.QuoteIdentifier in Match/auxiliary translators and SqliteDialect.WrapTableFunction; null/empty rejected; non-constant/non-column Match first operand rejected; raw-token test expectations updated to quoted form | src/nextorm.core/Visitors/SqliteFunctionTranslator.cs, src/nextorm.sqlite/SqliteDialect.cs
2026-10-06T02:56Z | DO | revision r=3 | iteration n=1/3 | item-4/5 throwaway probe 5/5 exit 0 (escaped sentinel token, null/empty rejection, computed Match rejection, empty FROM token rejection, adversarial FROM token); probe deleted | artifacts/d181/r3-probe-run2.log
2026-10-06T02:56Z | DO | revision r=3 | iteration n=1/3 | final build exit 0 0W/0E; FTS tests sqlite 12/12, core 11/11, postgres 11/11, integration 16/16 all exit 0 | artifacts/d181/r3-final-build.log, artifacts/d181/r3-final-*-fts.log

## r=3 DO results — required-test gap closure + token hardening (2026-10-06)

### Tests added
- `tests/nextorm.integration.tests/SqliteSpecificTests.cs` (+8): FTS3 column MATCH (table + column, native oracle); FTS3 rank-without-UDF negative; FTS3/FTS4 advanced syntax vs native oracle; FTS3/FTS4 malformed native-error equivalence; FTS5 FROM alias; FTS5 native hidden-`rank` oracle vs unconfigured bm25; adversarial-token sentinel.
- `tests/nextorm.sqlite.tests/SqliteFunctionsSqlGenerationTests.cs` (+8): operand/token guards; null-query predicate; mapped table/column operand preservation; adversarial escaped identifier; `WrapTableFunction` branch evidence; renderer fallback.
- new `tests/nextorm.sqlserver.tests/SqliteFunctionsRejectionTests.cs` (7), `tests/nextorm.mysql.tests/SqliteFunctionsRejectionTests.cs` (7), `tests/nextorm.mariadb.tests/SqliteFunctionsRejectionTests.cs` (7), `tests/nextorm.clickhouse.tests/SqliteFunctionsRejectionTests.cs` (7); E181-18 also exercises the extended `tests/nextorm.postgres.tests/SqliteFunctionsRejectionTests.cs` and `tests/nextorm.core.tests/InMemorySqliteFunctionsTests.cs` FTS members.

### Results (all exit 0)
| run | total | passed | failed | skipped | evidence |
| --- | --- | --- | --- | --- | --- |
| `dotnet build nextorm.slnx -c Debug` | — | — | 0W/0E | — | artifacts/d181/d5-final-build.log, artifacts/d181/d5-final-build-exit.txt |
| `SqliteSpecificTests` class | 68 | 68 | 0 | 0 | artifacts/d181/d5-final-integration-sqlite.log, artifacts/d181/d5-final-integration-sqlite-exit.txt |
| sqlite FTS filter | 19 | 19 | 0 | 0 | artifacts/d181/d5-final-sqlite-fts.log, artifacts/d181/d5-sqlite-fts-exit.txt |
| sqlite renderer fallback | 1 | 1 | 0 | 0 | artifacts/d181/d5-final-sqlite-renderer.log, artifacts/d181/d5-sqlite-renderer-exit.txt |
| core FTS | 11 | 11 | 0 | 0 | artifacts/d181/d5-final-core-fts.log, artifacts/d181/d5-core-fts-exit.txt |
| postgres FTS | 11 | 11 | 0 | 0 | artifacts/d181/d5-final-postgres-fts.log, artifacts/d181/d5-postgres-fts-exit.txt |
| provider rejection SQL Server | 7 | 7 | 0 | 0 | artifacts/d181/d5-final-sqlserver-rejection.log, artifacts/d181/d5-sqlserver-rejection-exit.txt |
| provider rejection MySQL | 7 | 7 | 0 | 0 | artifacts/d181/d5-final-mysql-rejection.log, artifacts/d181/d5-mysql-rejection-exit.txt |
| provider rejection MariaDB | 7 | 7 | 0 | 0 | artifacts/d181/d5-final-mariadb-rejection.log, artifacts/d181/d5-mariadb-rejection-exit.txt |
| provider rejection ClickHouse | 7 | 7 | 0 | 0 | artifacts/d181/d5-final-clickhouse-rejection.log, artifacts/d181/d5-clickhouse-rejection-exit.txt |
| `dotnet docfx docs/docfx.json` | — | — | 0 error(s) | — | artifacts/d181/d5-docfx.log, artifacts/d181/d5-docfx-exit.txt (2 pre-existing `nextorm.core.sourcegenerator/AnalyzerReleases.*` duplicate-source baseline warnings) |
| `git diff --check` | — | — | clean (0 bytes) | — | artifacts/d181/d5-git-diff-check.log |
New files CRLF; `git diff --check` clean.

### New test symbols → evidence rows E181-13..E181-19
- **E181-13 FTS3/4 variants** — `tests/nextorm.integration.tests/SqliteSpecificTests.cs`: `Fts3_Match_ShouldFilterAndMatchNative` **:1211** (table + column MATCH), `Fts3_RankOverMatchInfo_ShouldFailWithoutUdf` **:1304**, `Fts3_AdvancedQuerySyntax_ShouldMatchNative` **:1329**, `Fts4_AdvancedQuerySyntax_ShouldMatchNative` **:1348**, `Fts3_MalformedQuery_ShouldThrowTheSameNativeError` **:1367**, `Fts4_MalformedQuery_ShouldThrowTheSameNativeError` **:1390**.
- **E181-14 FTS5 FROM alias** — `SqliteSpecificTests.cs:Fts5_MatchTableFromSourceAliased_ShouldProjectAliasQualifiedAndMatchNative` **:1415**.
- **E181-15 native rank oracle** — `SqliteSpecificTests.cs:Fts5_RankHiddenColumn_ShouldMatchNativeConfiguredRankAndDifferFromBm25` **:1443**.
- **E181-16 input guards** — `tests/nextorm.sqlite.tests/SqliteFunctionsSqlGenerationTests.cs`: `Fts_Match_ShouldRejectNullEmptyComputedAndCapturedFirstOperand` **:409**, `Fts_AuxiliaryAndRank_ShouldRejectNullEmptyAndComputedTableToken` **:428**, `Fts5_MatchTable_ShouldRejectNullAndEmptyTableToken` **:447**, `Fts_Match_ShouldPreserveNullQueryPredicate` **:464**, `Fts_Match_ShouldKeepTableTokenAndMappedColumnOperandsValid` **:479**.
- **E181-17 token safety** — `SqliteFunctionsSqlGenerationTests.cs:Fts_Match_AdversarialToken_ShouldBeOneEscapedIdentifierNotRawSql` **:496**; `SqliteSpecificTests.cs:Fts_TokenAdversarial_ShouldStayEscapedAndNotDropSentinel` **:1480**.
- **E181-18 provider rejection matrix** — `tests/nextorm.sqlserver.tests/SqliteFunctionsRejectionTests.cs:18` (7), `tests/nextorm.mysql.tests/SqliteFunctionsRejectionTests.cs:18` (7), `tests/nextorm.mariadb.tests/SqliteFunctionsRejectionTests.cs:18` (7), `tests/nextorm.clickhouse.tests/SqliteFunctionsRejectionTests.cs:18` (7); `tests/nextorm.postgres.tests/SqliteFunctionsRejectionTests.cs:72` `Fts_Match_ShouldThrow` (+ `:83` Fts5Bm25, `:94` Fts5Highlight, `:105` Fts5Snippet, `:116` Fts5Rank, `:127` Fts3RankOverMatchInfo, `:138` Fts3RowId, `:149` Fts3Offsets, `:160` Fts3MatchInfo, `:171` Fts3Snippet, `:182` Fts5MatchTable); `tests/nextorm.core.tests/InMemorySqliteFunctionsTests.cs:79` `Fts_Match_ShouldThrow` (+10 FTS members `:91`–`:190`).
- **E181-19 branch evidence** — `SqliteFunctionsSqlGenerationTests.cs:WrapTableFunction_Fts5_ShouldQuoteTokenRejectMalformedAndPassThroughOthers` **:518** (covers `SqliteDialect.cs:134/138`), `SqliteFunctionsSqlGenerationTests.cs:SqliteFunctionRenderer_UnsupportedName_ShouldThrowInsteadOfEmittingRawSql` **:540** (renderer fallback); `SqliteFunctionTranslator.cs:107` operand-branch guard is exercised by **:409**/**:**428.

### D181-c1 closure
- `D181-c1` (CHECK FAIL r=2, n=1): all flagged required-test gaps closed at r=3, n=1; product code unchanged at r=3 except the already-logged token hardening (quote/reject). Re-CHECK pending.

2026-10-06T03:04Z | DO | revision r=3 | iteration n=1/3 | test gaps closed: integration +8 (E181-13/14/15/17), sqlite SQL-gen +8 (E181-16/17/19), 4 provider rejection files (E181-18); SqliteSpecificTests 68/68, sqlite FTS 19/19, sqlite renderer 1/1, core 11/11, postgres 11/11, rejection 7/7 each — all exit 0 | artifacts/d181/d5-final-integration-sqlite.log, artifacts/d181/d5-final-sqlite-fts.log, artifacts/d181/d5-final-*-rejection.log
2026-10-06T03:04Z | DO | revision r=3 | iteration n=1/3 | D181-c1 closed: every CHECK-flagged required-test gap bound to E181-13..E181-19 and passing; build 0W/0E; plain docfx exit 0 0 errors 2 pre-existing baseline warnings; git diff --check clean; new files CRLF; re-CHECK pending | artifacts/d181/d5-final-build.log, artifacts/d181/d5-docfx.log, artifacts/d181/d5-git-diff-check.log

## CHECK re-gather (r=3, n=1) — fresh evidence on the current tree
- Packet: `/tmp/d181-r3-packet.md` (rv=D181.r3.ec1; supersedes rv=D181.r2.ec1). All evidence re-bound to HEAD a9fd636e; combined changed src+tests sha256 99c074cacd0fb453b688b405c337036b.
- Integration (current r=3 tree): `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml artifacts/d181/r3-integration-results.xml` → exit 0, Total 3167, Passed 2974, Failed 0, Skipped 193 (43 SQLite + 25 PG + 43 MSSQL + 79 MySQL + 2 Lob + 1 LobPerf); PG/MSSQL/MySQL/MariaDB/ClickHouse all executed; SQLite FTS 24/24 Pass, 0 skipped — artifacts/d181/r3-integration.log, r3-integration-results.xml, r3-integration-parse.txt.
- Coverage: collect exit 0 (inner 8255/0 fail/2501 skip); reportgenerator exit 0; Line 87.1% (45135/51769) / Branch 78.9% (23325/29562) vs gates 85/75 PASS — artifacts/d181/r3-coverage/Summary.txt.
- Build exit 0 0W/0E; plain `dotnet docfx docs/docfx.json` exit 0, 2 pre-existing baseline warnings, 0 errors; slop 0/0; `git diff --check` clean; status 24 M + 6 ??, no bin/obj/docs-api/_site — artifacts/d181/r3-build.log, r3-docfx.log, r3-slop-scan.txt, r3-git-diff-check.log, r3-git-status.txt.
- D181-c1 resolved at r=3 (R181-02 correction + variants E181-13..E181-19); token/operand hardening preventive; no product regression. Fixes applied for D181-c1: 1.
2026-10-05T22:12Z | CHECK | revision r=3 | iteration n=1/3 | re-gather integration on current r=3 tree exit 0 total 3167 passed 2974 failed 0 skipped 193; SQLite FTS 24/24; providers all executed | artifacts/d181/r3-integration.log, artifacts/d181/r3-integration-results.xml
2026-10-05T22:12Z | CHECK | revision r=3 | iteration n=1/3 | fresh coverage r=3 line 87.1%/branch 78.9% gates 85/75 PASS | artifacts/d181/r3-coverage/Summary.txt
2026-10-05T22:12Z | CHECK | revision r=3 | iteration n=1/3 | build 0W/0E; plain docfx exit 0 (2 baseline warnings); slop 0/0; git diff --check clean; 24 M + 6 ?? | artifacts/d181/r3-build.log, artifacts/d181/r3-docfx.log, artifacts/d181/r3-slop-scan.txt
2026-10-05T22:12Z | CHECK | revision r=3 | iteration n=1/3 | contract packet rv=D181.r3.ec1 with E181-01..E181-19 + DO ledger persisted | /tmp/d181-r3-packet.md
2026-10-05T22:12Z | CHECK | revision r=3 | iteration n=1/3 | D181-c1 closed at r=3 (R181-02 corrected + E181-13..E181-19); no product regression; fixes applied 1 | docs/specs/status/rc1-181-sqlite-fts-1.md

## r=3 security-fix stream — FTS value parameterisation + residual FROM literal (2026-10-06)

### Fixes
- FTS scalar constant value arguments (search query, matchinfo format, highlight/snippet markers) are bound via `BaseExpressionVisitor.EmitStableStringParameter` (`src/nextorm.core/Visitors/SqliteFunctionTranslator.cs`), so they never render as raw/injectable SQL literals. Implemented in the tree before this stream; this stream updated the stale expectations and closed the residual FROM case.
- FTS5 FROM rewrite uses the structured 3-arg `ISqlDialect.WrapTableFunction(name, call, arguments)` (`src/nextorm.sqlite/SqliteDialect.cs`) that quotes `arguments[0]` through `QuoteIdentifier` and never reparses the serialised call text.
- **Residual FROM match-value literal (new, this stream):** `SqlSourceRenderer.MakeTableFunction` rendered a built-in constant table-function string argument through the generic constant path, which inlines without escaping, so `MatchTable("docs_fts", "O'Brien")` emitted `"docs_fts"('O'Brien')`. Fixed by rendering a built-in constant string value with `SqlLiteral.ToSqlStringLiteral` (doubles `'`), yielding `"docs_fts"('O''Brien')`. Deliberately kept inline (consistent with the FROM form) and scoped to built-in table functions (`CommonFunctions`); user-defined table functions stay verbatim.

### Tests updated (stale expectations -> new shapes)
- `tests/nextorm.sqlite.tests/SqliteFunctionsSqlGenerationTests.cs`: `Fts_Match_ShouldEmitTableAndColumnMatch`, `Fts_Match_ShouldKeepTableTokenAndMappedColumnOperandsValid` -> quoted table token + bound `$p0`/`$p1`; `Fts5_Highlight_ShouldEmitNativeForm` -> `highlight("docs_fts", 0, $p0, $p1)`; `Fts3_MatchInfo_ShouldEmitDefaultAndExplicitFormat` -> `matchinfo("docs_fts", $p0)`; `Fts3_Snippet_ShouldEmitDefaultAndFullOverloads` -> `snippet("docs_fts", $p0, $p1, $p2, 0, 8)`; `Fts_Match_AdversarialToken_ShouldBeOneEscapedIdentifierNotRawSql` -> escaped identifier + bound `$p0`; `WrapTableFunction_Fts5_ShouldQuoteTokenRejectMalformedAndPassThroughOthers` -> structured 3-arg overload (quote adversarial token, pass-through non-FTS, reject too-few/empty). `Fts5_Snippet_ShouldEmitNativeForm` was already on the parameterised shape and needed no change. Before the update: 19 run / 12 passed / 7 failed (artifacts/d181/pretest-sqlite-fts.log).

### Tests added
- `SqliteFunctionsSqlGenerationTests.cs:Fts_ApostropheConstants_ShouldBindNotInlineBrokenLiterals` — apostrophe query (`Match("docs_fts","O'Brien")` -> `$p0`), marker (`Highlight(...,"O'Brien", "]")` -> `$p0/$p1`) and matchinfo format (`"p'cx"` -> `$p0`) are bound, never inline.
- `SqliteFunctionsSqlGenerationTests.cs:Fts5_MatchTableFromSourceApostropheMatch_ShouldEscapeLiteralNotInject` — discriminating for the residual FROM fix: `'O''Brien'` and `'x''); drop table sentinel; --'` escaped (red before the fix: got `'O'Brien'`, exit 1).
- `tests/nextorm.integration.tests/SqliteSpecificTests.cs:Fts5_MatchTableFromSourceAdversarialToken_ShouldStayEscapedAndNotDropSentinel` — adversarial FROM table token stays one quoted identifier; sentinel survives.
- `tests/nextorm.integration.tests/SqliteSpecificTests.cs:Fts5_ApostropheScalarConstants_ShouldBindAndMatchNative` — real execution: apostrophe markers match native; apostrophe query reaches FTS5 as bound data (same FTS5 syntax error as a bound `@q`, not a SQL parse error).
- Throwaway probe `tests/nextorm.sqlite.tests/ZzProbeTests.cs` deleted; absent from `git status`.

### Results
| run | exit | total | passed | failed | log |
| --- | --- | --- | --- | --- | --- |
| sqlite FTS before expectation update (red) | 1 | 19 | 12 | 7 | artifacts/d181/pretest-sqlite-fts.log |
| FROM apostrophe test without the residual fix (red) | 1 | 1 | 0 | 1 | artifacts/d181/red-from-apostrophe.log |
| `dotnet build nextorm.slnx -c Debug` | 0 | — | — | 0 (0W/0E) | artifacts/d181/final-build.log |
| `nextorm.sqlite.tests -method "*Fts*"` | 0 | 21 | 21 | 0 | artifacts/d181/final-sqlite-fts.log |
| `nextorm.core.tests -method "*Fts*"` | 0 | 11 | 11 | 0 | artifacts/d181/final-core-fts.log |
| `nextorm.postgres.tests -method "*Fts*"` | 0 | 11 | 11 | 0 | artifacts/d181/final-postgres-fts.log |
| `nextorm.postgres.tests SqliteFunctionsRejectionTests` | 0 | 16 | 16 | 0 | artifacts/d181/final-postgres-rejection.log |
| integration `SqliteSpecificTests` (real SQLite) | 0 | 70 | 70 | 0 | artifacts/d181/final-integration-sqlite.log |
| `dotnet docfx docs/docfx.json` | 0 | — | — | 0 error(s), 2 baseline warnings | artifacts/d181/docfx.log |
| `git diff --check` | 0 | — | — | clean (0 bytes) | artifacts/d181/git-diff-check.log |

### Security disposition
- r=3 candidates closed: (a) constant FTS table tokens are quoted identifiers or rejected (`QuoteIdentifier`/`TryTableToken`); (b) FTS scalar constant value args are bound (`EmitStableStringParameter`); (c) the FTS5 FROM rewrite is structural (no reparse) and quotes the table token; (d) the residual FROM match-value literal is now escaped (`''`), proven by a discriminating red->green test.
- Residual disposition: the FROM match value remains an inline literal (escaped) rather than a bound parameter, consistent with the FROM table-function form; parameterising it was not required (escape is the documented minimum) and would change the native-oracle shape. The generic constant-inlining path (`BaseExpressionVisitor.TryEmitValue`) is outside D181 scope and unchanged.
- No new public API, no commit/push.

2026-10-06T03:23Z | DO | revision r=3 | iteration n=1/3 | security-fix stream: updated 7 stale sqlite FTS expectations to parameterised/quoted shapes; Fts5_Snippet already current | artifacts/d181/pretest-sqlite-fts.log
2026-10-06T03:23Z | DO | revision r=3 | iteration n=1/3 | residual FROM match-value literal fixed in SqlSourceRenderer.MakeTableFunction (escape built-in constant string args via SqlLiteral.ToSqlStringLiteral); red before fix, green after | artifacts/d181/red-from-apostrophe.log, artifacts/d181/final-sqlite-fts.log
2026-10-06T03:23Z | DO | revision r=3 | iteration n=1/3 | discriminating tests added: FROM apostrophe (sqlite SQL-gen), FROM adversarial token sentinel + apostrophe scalar query/marker execution (integration); probe ZzProbeTests.cs deleted | artifacts/d181/final-integration-sqlite.log
2026-10-06T03:23Z | DO | revision r=3 | iteration n=1/3 | verification: build 0W/0E; sqlite FTS 21/21, core FTS 11/11, postgres FTS 11/11, postgres rejection 16/16, integration SqliteSpecificTests 70/70 — all exit 0; docfx exit 0 0 errors (2 baseline); git diff --check clean; CRLF normalised | artifacts/d181/final-build.log, artifacts/d181/final-*.log, artifacts/d181/docfx.log, artifacts/d181/git-diff-check.log

## Final CHECK re-gather (r=3, n=1) — POST-security-fix, current tree (2026-10-06)

- Packet: `/tmp/d181-final-packet.md` (rv=D181.r3.ec1; HEAD a9fd636e; changed src+tests sha256 `82092dc6f69ebc220df2811263c0fdfa0a9c7c83b209267dc885bc6000b0dbbf`; run tree == current tree). **Verdict: FAIL.**
- Fresh container integration: `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml artifacts/d181/final-integration-results.xml` → exit 0, Total 3169, Passed 2976, Failed 0, Skipped 193 (37.4s); per-provider SQLite 574/0/43, PostgreSQL 592/0/25, SQL Server 574/0/43, MySQL 538/0/79, MariaDB 50/0/0, ClickHouse 103/0/0; SQLite FTS 26/26 Pass, 0 skipped — artifacts/d181/final-integration.log, final-integration-results.xml, final-integration-parse.txt, final-integration-exit.txt.
- Fresh coverage: collect exit **2** — 3 tests failed in nextorm.sqlite.tests (`CoreScalars_ShouldEmitNativeForms`, `NoArgumentCoreScalars_ShouldEmitNativeForms`, `JsonScalars_ShouldEmitNativeForms`); reportgenerator exit 0; Line 87.1% (45157/51794) / Branch 78.9% (23340/29580) vs gates 85/75 (percentages pass, tests FAIL) — artifacts/d181/final-coverage-collect.log, final-coverage-report.log, final-coverage/Summary.txt.
- Focused runs (all exit 0): sqlite `*Fts*` 21/21; core `*Fts*` 11/11; postgres `*Fts*` 11/11; postgres `*FullText*` 2/2; postgres rejection 16/16; SQL Server/MySQL/MariaDB/ClickHouse rejection 7/7 each; integration SqliteSpecificTests 70/70 — artifacts/d181/final-*-exit.txt, final-*.log. Direct `*Scalars*` (no coverage): 13 total / 3 failed / exit 1 — artifacts/d181/final-sqlite-scalars-direct.log.
- Build/DocFX/slop/git: build exit 0 0W/0E; plain `dotnet docfx docs/docfx.json` exit 0 (0 errors, 2 pre-existing baseline warnings); slop 0; `git diff --check` exit 0; 24 M + untracked (4 new rejection tests, artifacts/, this status file); CRLF clean — artifacts/d181/final-build.log, final-docfx.log, final-slop-scan.txt, final-git-diff-check.log.
- **D181-c2 (NEW, r=3,n=1, POST-security-fix, P1, regression)** — `SqliteFunctionTranslator.cs:97-104` binds a constant string via `BaseExpressionVisitor.EmitStableStringParameter` (`BaseExpressionVisitor.cs:585`) for **all** SQLite scalar functions, not only FTS; the security stream updated only the 7 FTS expectations, leaving 3 core-scalar/JSON expectations stale → R181-10 tests FAIL. Fixes applied: 0.
- **D181-c3 (NEW, r=3,n=1, P1, docs)** — `docs/specs/comparison/provider-feature-comparison.md:236` still asserts "SQLite full-text (FTS3/4/5), where nextorm has no surface", contradicting the shipped-FTS rows `:173`/`:178` → R181-07 accuracy FAIL. Fixes applied: 0.
- D181-c1 (r=2) remains resolved at r=3 (R181-02 corrected; E181-13..E181-19 pass). Next allowed step: route D181-c2/D181-c3 for a scoped fix (FTS-scope the parameter binding, refresh the stale docs row) then re-CHECK.
2026-10-05T22:30Z | CHECK | revision r=3 | iteration n=1/3 | final POST-security-fix re-gather: integration exit 0 total 3169 passed 2976 failed 0 skipped 193; SQLite FTS 26/26; focused runs all exit 0 | artifacts/d181/final-integration.log, final-integration-parse.txt, final-*-fts.log
2026-10-05T22:30Z | CHECK | revision r=3 | iteration n=1/3 | coverage collect exit 2 — 3 sqlite scalar/JSON tests failed (unscoped EmitStableStringParameter); coverage % 87.1/78.9 PASS but R181-10 tests FAIL | artifacts/d181/final-coverage-collect.log, final-sqlite-scalars-direct.log
2026-10-05T22:30Z | CHECK | revision r=3 | iteration n=1/3 | D181-c2 (regression) + D181-c3 (stale provider-feature-comparison.md:236) recorded; fixes applied 0; final packet persisted; verdict FAIL | /tmp/d181-final-packet.md, docs/specs/status/rc1-181-sqlite-fts-1.md

## r=3 DO fix stream — D181-c2 + D181-c3 closure (2026-10-05, UTC, attempt n=2/3)

### D181-c2 (P1 regression) — fix
- Scope: `src/nextorm.core/Visitors/SqliteFunctionTranslator.cs` — `EmitStableStringParameter` now applies only to the FTS query surface. New local `isFts = name == SqliteFunctions.Match || FtsVerbatimFirstArgument.Contains(name)`; the constant-string branch is `if (isFts && args[i] is ConstantExpression { Value: string })`. All non-FTS `SqliteFunctions` scalar/JSON/date/math/aggregate members again render constant strings as escaped SQL literals through `VisitToString`; FTS table tokens stay quoted identifiers, FTS value strings stay bound, apostrophe safety preserved. `BaseExpressionVisitor.EmitStableStringParameter` unchanged (now FTS-only callers).
- Changed lines: `SqliteFunctionTranslator.cs:71-75` (new `isFts`), `:120` (gated condition). CRLF preserved.

### D181-c2 — verification (current tree)
| run | exit | total | passed | failed | skipped | evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `dotnet build nextorm.slnx -c Debug` | 0 | — | — | 0 (0W/0E) | — | artifacts/d181/build.log |
| full `tests/nextorm.sqlite.tests` | 0 | 1055 | 1054 | 0 | 1 | artifacts/d181/sqlite-tests.log |
| 3 regressions focused (`CoreScalars`/`NoArgumentCoreScalars`/`JsonScalars`) | 0 | 3 | 3 | 0 | 0 | artifacts/d181/regression3.log |
| sqlite `*Fts*` | 0 | 21 | 21 | 0 | 0 | artifacts/d181/sqlite-fts.log |
| core `*Fts*` | 0 | 11 | 11 | 0 | 0 | artifacts/d181/core-fts.log |
| postgres `*Fts*`/`*FullText*` | 0 | 13 | 13 | 0 | 0 | artifacts/d181/postgres-fts.log |
| SQL Server rejection | 0 | 7 | 7 | 0 | 0 | artifacts/d181/sqlserver-rejection.log |
| MySQL rejection | 0 | 7 | 7 | 0 | 0 | artifacts/d181/mysql-rejection.log |
| MariaDB rejection | 0 | 7 | 7 | 0 | 0 | artifacts/d181/mariadb-rejection.log |
| ClickHouse rejection | 0 | 7 | 7 | 0 | 0 | artifacts/d181/clickhouse-rejection.log |
The 3 previously-failing suites are green; the FTS suite (21) stays green.

### D181-c2 — coverage / integration
- Coverage collect (CI recipe): exit 0; inner run total 8259, failed 0, skipped 2501 — artifacts/d181/coverage-collect.log. reportgenerator exit 0 — artifacts/d181/coverage-report.log. Line 87.1%, Branch 78.9% (gates 85/75 PASS) — tests/coverage/report/Summary.txt.
- Container integration: `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -result-xml artifacts/d181/c2-integration-results.xml` → exit 0, Total 3169, Passed 2976, Failed 0, Skipped 193 — artifacts/d181/c2-integration.log, c2-integration-results.xml, c2-integration-exit.txt. Per-provider: SQLite 574/0/43, PostgreSQL 592/0/25, SQL Server 574/0/43, MySQL 538/0/79, MariaDB 50/0/0, ClickHouse 103/0/0; `SqliteSpecificTests` 70/70; SQLite FTS 26/26 Pass, 0 skipped (Fts3_Match, Fts4_Match, Fts5_TableMatch, MatchTableFromSource(+Aliased), Bm25, Highlight, Snippet, rank oracles, helper/advanced/malformed, apostrophe/adversarial).

### D181-c3 (P1 stale docs) — fix
- `docs/specs/comparison/provider-feature-comparison.md:236` reworded: no longer "where nextorm has no surface"; it now states that only FTS3/4/5 **maintenance/control** remains with linq2db (AutoMerge/CrisisMerge/Merge/Optimize/Rebuild/IntegrityCheck), that nextorm shipped the **query + FROM** surface in #181, and that maintenance/control is deferred to #195/#196. Consistent with `:54-56`, `:173`, `:177-181`. An `rg -i` sweep of the file confirms no other stale "no surface"/FTS gap line.

### DocFX / git
- Plain `dotnet docfx docs/docfx.json` exit 0, 0 error(s), 2 pre-existing `nextorm.core.sourcegenerator/AnalyzerReleases.*` baseline warnings — artifacts/d181/docfx.log.
- `git diff --check` exit 0, clean (0 bytes) — artifacts/d181/git-diff-check.log. Both edited files CRLF.

### Defect closures
- `D181-c2` (P1 regression): CLOSED at r=3, n=2 — fixes applied 1 (scope the constant-string parameter binding to FTS). Regression suites green; no FTS behavior change.
- `D181-c3` (P1 docs): CLOSED at r=3, n=2 — fixes applied 1 (provider-feature-comparison.md:236). Awaiting re-CHECK.

2026-10-05T22:36Z | DO | revision r=3 | iteration n=2/3 | D181-c2 fixed: EmitStableStringParameter scoped to FTS via isFts (Match + FtsVerbatimFirstArgument); non-FTS scalars/JSON back to escaped literals | src/nextorm.core/Visitors/SqliteFunctionTranslator.cs, artifacts/d181/build.log
2026-10-05T22:36Z | DO | revision r=3 | iteration n=2/3 | D181-c2 verified: full sqlite 1055/1054/0/1 exit 0; 3 regressions 3/3; sqlite FTS 21/21; core FTS 11/11; postgres FTS 13/13; rejection 7/7 each | artifacts/d181/sqlite-tests.log, regression3.log, sqlite-fts.log, core-fts.log, postgres-fts.log, *-rejection.log
2026-10-05T22:36Z | DO | revision r=3 | iteration n=2/3 | coverage collect exit 0 (inner 8259/0 fail/2501 skip), reportgenerator exit 0, line 87.1%/branch 78.9% gates PASS | artifacts/d181/coverage-collect.log, coverage-report.log, tests/coverage/report/Summary.txt
2026-10-05T22:36Z | DO | revision r=3 | iteration n=2/3 | container integration exit 0 total 3169 passed 2976 failed 0 skipped 193; SqliteSpecificTests 70/70; SQLite FTS 26/26 | artifacts/d181/c2-integration.log, c2-integration-results.xml
2026-10-05T22:36Z | DO | revision r=3 | iteration n=2/3 | D181-c3 fixed: provider-feature-comparison.md:236 now maintenance-only + #181 query/FROM + #195/#196; no other stale FTS line | docs/specs/comparison/provider-feature-comparison.md
2026-10-05T22:36Z | DO | revision r=3 | iteration n=2/3 | docfx exit 0 0 errors 2 baseline warnings; git diff --check clean; defects D181-c2/D181-c3 CLOSED (fixes applied 1+1); re-CHECK pending | artifacts/d181/docfx.log, git-diff-check.log

## ACT (r=3, n=2/3, rv=D181.r3.ec1) — CHECK PASS, D181 done

- CHECK verdict: **PASS**. Frozen point: plan revision **r=3**, attempt **n=2/3**, evidence contract **rv=D181.r3.ec1**.
- Requirement/evidence contract R181-01..R181-10 and E181-01..E181-19 satisfied on the current tree after the
  D181-c2/D181-c3 fix stream: build exit 0 0W/0E; coverage line 87.1% / branch 78.9% (gates 85/75 PASS);
  container integration exit 0 total 3169 passed 2976 failed 0 skipped 193 with SQLite FTS 26/26 and
  PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse all executed; focused suites (sqlite FTS 21/21, core FTS 11/11,
  postgres FTS 13/13, provider rejections 7/7 each, integration SqliteSpecificTests 70/70) exit 0; plain
  `dotnet docfx docs/docfx.json` exit 0 (0 errors, 2 pre-existing baseline warnings); `git diff --check` clean.
- Defect closures: D181-c1 (r=2 required-test gaps + R181-02) resolved at r=3 (R181-02 corrected; variants
  E181-13..E181-19 added); D181-c2 (P1 regression, unscoped `EmitStableStringParameter`) and D181-c3 (P1 stale
  provider-feature-comparison row) each closed with 1 applied fix at r=3, n=2. No product regression remains.
- Commit plan: one D181 commit `#181 SQLite FTS3/4/5 query surface and FTS5 FROM source` staging only the D181
  change set (core dialect capabilities + translator + SqlSourceRenderer + SqlFunctions.Sqlite; SQLite dialect
  and renderer; core/provider SQL-gen and rejection tests; integration SqliteSpecificTests; EN/RU docs + specs;
  this status file). The collection-status update is a separate bookkeeping commit. Both remain **unpushed** —
  push never authorized; no merge.
- Follow-ups (deferred, NOT part of #181): **#195** FTS5 maintenance/control command surface (AutoMerge,
  CrisisMerge, Merge, Optimize, Rebuild, IntegrityCheck); **#196** FTS5 maintenance real-SQLite tests and EN/RU
  docs (depends on #195). Both OPEN, milestone 1.0.9-rc1, created and linked from #181 before close.
- Residual note: the generic non-FTS constant-inlining path (`BaseExpressionVisitor.TryEmitValue`) is outside
  #181 scope and unchanged; the FTS5 FROM match value remains an inline escaped literal (not a bound parameter)
  by design, consistent with the table-function FROM form; `SupportsFullText` stays false and cross-provider
  `contains`/`freetext` remain unsupported.
- Issue outcome: #181 closed with a summary naming the commit SHA, the CHECK PASS (r=3 / n=2/3 / rv=D181.r3.ec1),
  follow-ups #195/#196, and the commit's unpushed state.
- 2026-10-06T03:45Z | ACT | revision r=3 | iteration n=2/3 | CHECK PASS; freeze r=3 / n=2/3 / rv=D181.r3.ec1; D181 committed; #181 closed; collection status updated in separate bookkeeping commit | this file; docs/specs/status/collection-1.0.9-rc1.md
