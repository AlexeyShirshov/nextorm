# D181 CHECK packet — SQLite FTS3/FTS4/FTS5 (#181) — rv=D181.r2.ec1
branch 1.0.9-rc1 · cycle N=1 r=2 n=1/3 · gate DO→CHECK · no commit/push · CRLF

## Contract rows E181-01..E181-12 (owner DO unless noted; applicability always)
| row | scenario | exact invocation | achieved (exit/counts) | artifact | DO-ledger binding |
| E181-01 | Debug+Release solution build | `dotnet build nextorm.slnx -c Debug`; `-c Release` | 0/0; 0W/0E each | build-debug{,-exit}.txt, build-release{,-exit}.txt | SqliteFunctionRenderer.cs:38 |
| E181-02 | FTS SQL shape/overload/param | `dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- -method "*Fts*" -noColor` | 0; 12/12 pass 0 fail | artifacts/d181/d3-sqlite-fts-inner2.log | SqliteFunctionsSqlGenerationTests.Fts_Match_ShouldEmitTableAndColumnMatch (:209) |
| E181-03 | non-SQLite rejection + renamed regression | `… nextorm.postgres.tests … -method "*Fts*"`; `… nextorm.sqlite.tests … -method "*FullText*"` | 0; 11/11; 0; 1/1 | d3-postgres-fts-inner2.log; r181-06-fulltext-rerun.log | SqliteFunctionsRejectionTests.Fts_Match_ShouldThrow (:72); SqlGenerationTests.cs:2716 |
| E181-04 | in-memory rejection | `dotnet run --project tests/nextorm.core.tests -c Debug --no-build -- -method "*Fts*" -noColor` | 0; 11/11 | d3-core-fts-inner2.log | InMemorySqliteFunctionsTests.Fts_Match_ShouldThrow (:79) |
| E181-05 | FTS3/4/5 module availability | bundled-SQLite probe (`create virtual table … using fts5/fts4/fts3`) | OK; sqlite 3.53.4, fts5/fts4/fts3 OK; 1/1 | fts-module-availability.log; fts-module-probe-run.log | SqliteSpecificTests.ResetFtsTables (:823) |
| E181-06 | real execution/native equivalence/ranking | `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class NextORM.Integration.Tests.SqliteSpecificTests -noColor` | 0; 60/60 pass 0 skip | d4-sqlite-fts-rerun.log | SqliteSpecificTests.Fts5_TableMatch_ShouldFilterParameterisedAndMatchNative (:869) … Fts3_RankOverMatchInfo_ShouldMatchNativeWithUdf (:1273) |
| E181-07 | container boundary regression | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml artifacts/d181/integration-results.xml` | 0; 3159 total 2966 pass 0 fail 193 skip | integration.log; integration-results.xml | provider table below |
| E181-08 | coverage gate | `dotnet-coverage collect "dotnet test -c Debug" --settings coverage.settings.xml --output … --output-format cobertura`; `reportgenerator -reports:… -targetdir:… -reporttypes:Cobertura;TextSummary` | 0/0; line 87.1% branch 78.8% | coverage-collect.log; coverage/Summary.txt; coverage/Cobertura.xml | gates 85/75 |
| E181-09 | docs EN+RU + specs + docfx | `dotnet docfx docs/docfx.json --warningsAsErrors`; plain `dotnet docfx docs/docfx.json` | 255 (2 pre-existing warnings); plain 0 | docfx.log, docfx-exit.txt; check/docfx-plain{,-exit} | caveat below |
| E181-10 | design/invariant + CRLF hygiene | `git diff --check`; CRLF normalize + re-check | 0 no output; 24 mods CRLF | git-diff-check.log | status §Git hygiene |
| E181-11 | M1/M2 verified tracking | `gh issue view 195/196/181 --json number,title,milestone,state,url` | OPEN, milestone 1.0.9-rc1 (#19) | m1-create.log; m2-create.log; 181-comment.log | #195/#196 below |
| E181-12 | final CHECK adjudication | this packet (owner CHECK) | PASS pending orchestrator sign-off | artifacts/d181/check/D181-CHECK-packet.md | defect history below |

## Criterion→file:line (production + test)
- R181-01 FTS5: prod SqlFunctions.Sqlite.cs:290,304,307,310,313,316 + SqliteFunctionRenderer.cs:44-50 + SqliteFunctionTranslator.cs:34-44,63-65,85-96; test SqliteSpecificTests.cs:869,887,905,945,968,991,1034,1057; SqliteFunctionsSqlGenerationTests.cs:209,228,245,260,275,290
- R181-02 FTS3/4: prod SqlFunctions.Sqlite.cs:301,319,322,325,328,331,334-349 + SqliteFunctionRenderer.cs:46-47,51-53; test SqliteSpecificTests.cs:1082,1107,1160,1184,1211,1229,1273
- R181-03 FROM/table-valued, trusted token: prod SqlFunctions.Sqlite.cs:300,352 + SqliteDialect.cs:122,128-144 + SqliteFunctionTranslator.cs:105-124; test SqliteSpecificTests.cs:1057; SqliteFunctionsSqlGenerationTests.cs:290
- R181-04 dispatch/rejection/SupportsFullText=false: prod SqliteFunctionTranslator.cs:58-59, SqliteDialect.cs:119, DialectCapabilities.cs:643, ISqlDialect.cs:298, SqlDialectBase.cs:100; test SqliteFunctionsRejectionTests.cs:72-190, SqlGenerationTests.cs:2716
- R181-05 in-memory: prod InMemoryScalarFunctionRewriter.cs:123-135,166-171; test InMemorySqliteFunctionsTests.cs:77-198
- R181-06 renamed test: SqlGenerationTests.cs:2714-2728 (`CrossProviderFullTextPredicates_ContainsFreetext_ShouldThrowBecauseSqliteLacksThem`); r181-06-fulltext-rerun.log 1/1 exit 0
- R181-07 docs: docs/guide/provider-specific/sqlite.md:164-289; docs/providers/overview.md:86; docs/advanced/limitations.md:42,81; RU mirrors; 0 public→specs links; guide numbering 01-29 unchanged
- R181-08 bundled modules+execution: fts-module-availability.log:1-4; SqliteSpecificTests.cs:823-837; SQLite FTS 16/16
- R181-09 M1/M2: gh #195/#196 OPEN milestone 1.0.9-rc1 (#19); m1-create.log, m2-create.log
- R181-10 builds/tests/coverage/docfx: E181-01/07/08/09 above

## Changed files & counts
24 tracked modified: docs articles 8 (4 EN + 4 RU), specs 5, src 6, tests 5; +1744/-37; 2 untracked (artifacts/, docs/specs/status/rc1-181-sqlite-fts-1.md). No commit/push.
src: DialectCapabilities.cs, InMemoryScalarFunctionRewriter.cs, SqlFunctions.Sqlite.cs, SqliteFunctionTranslator.cs, SqliteDialect.cs, SqliteFunctionRenderer.cs.
tests: InMemorySqliteFunctionsTests.cs, SqliteSpecificTests.cs, SqliteFunctionsRejectionTests.cs, SqlGenerationTests.cs, SqliteFunctionsSqlGenerationTests.cs.
Follow-ups #195 https://github.com/AlexeyShirshov/nextorm/issues/195 (M1), #196 https://github.com/AlexeyShirshov/nextorm/issues/196 (M2, depends #195) — milestone 1.0.9-rc1 (#19). #181 comment https://github.com/AlexeyShirshov/nextorm/issues/181#issuecomment-6003233665.

## Builds / tests / coverage / git
Builds Debug exit 0 (0W/0E), Release exit 0 (0W/0E).
Unit: core 1547/1547; sqlite 1045 (1044 pass,1 skip); postgres 767; sqlserver 560; mysql 273; mariadb 181; clickhouse 491 — all exit 0.
Integration exit 0: 3159 total, 2966 pass, 0 fail, 193 skip, 44.880s; providers: SQLite 638/43, PG 751/25, MSSQL 655/43, MySQL 579/79, MariaDB 50/0, ClickHouse 173/0, cross 120/3; SQLite FTS 16/16 pass 0 skip.
Coverage collect exit 0 (inner 8211 total, 0 fail, 2501 skip), report exit 0; overall line 87.1% (45109/51748) ≥85, branch 78.8% (23305/29548) ≥75.
Changed-file coverage: SqliteFunctionRenderer.cs 97.2/76.8; SqliteFunctionTranslator.cs 97.3/97.4; SqliteDialect.cs 82.4/54.7; InMemoryScalarFunctionRewriter.cs 97.2/85.1; SqlFunctions.Sqlite.cs 0.0/100.0; DialectCapabilities.cs doc-only (ExtremeRowDescription 100/50). Test files out of coverage scope (nextorm.{core,sqlite,postgres,sqlserver} only).
git diff --check exit 0, no output; git status --short: 24 M + 2 ??.

## DocFX caveat
plan `--warningsAsErrors` exits 255 due only to 2 pre-existing `nextorm.core.sourcegenerator/AnalyzerReleases.{Shipped,Unshipped}.md` duplicate-source MSBuild warnings (unrelated to D181).
plain `dotnet docfx docs/docfx.json` exit 0, 2 warnings (same pre-existing), 0 errors, 0 XREF/link warnings introduced — artifacts/d181/check/docfx-plain.log.

## Coverage note — SqlFunctions.Sqlite
SqlFunctions.Sqlite.cs line 0.0% / branch 100%: declaration-only throwing marker bodies (`SqlFunctions`/`SqliteFunctions`); behavior is exercised via SqliteFunctionTranslator/SqliteFunctionRenderer and the rejection tests; same 0% pattern as Postgres/MySql/ClickHouse/SqlServer declaration classes.

## Defect history
none product. D4 "incomplete input" was test wiring (post-projection identity `.OrderBy(id => id)` emitted an empty ORDER BY; pre-existing query-builder gap outside #181) fixed in SqliteSpecificTests.cs; no product change.
