# R175 rv=2 evidence contract + row-bound ledger

Cycle N=1, plan_revision r=1, evidence contract rv=2. Baseline `cf34f910`, tree HEAD `3ebaa4ee`,
branch `1.0.9-rc2`. Artifact root `E = docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/`.
`state` is one of `planned/not-run`, `run-pass`, `run-fail`, `guard`, `blocked`.
Shared logs are allowed; every row below names its own invocation + artifact fragment.
Tree/artifact revision token for every row below: `3ebaa4ee+d175-post` (source == artifact).

## Contract

- Every applicable row needs an actual invocation, its exit/result, an existing artifact and provenance
  (source_revision = tree revision the command ran against; artifact_revision = built artifact revision).
- `planned/not-run/failed/missing/blocked` are recorded explicitly; historical rv=1 artifacts never close
  an rv=2 row.
- rv=2 row state is authoritative in this file; DO hands artifacts + row state to CHECK for the verdict.
- "Predicate / version binding" states the applicability condition and the revision the evidence binds to.

## EC rows (evidence-collection obligations)

| Row | rv | Scenario | Actual invocation / fragment | Exit/result | Artifact | Predicate / version binding | State |
|---|---|---|---|---|---|---|---|
| EC175-01 | 2 | intent audit / H-site mapping | `roslyn members` + `roslyn refs` + text search; H01-H08 -> current logical sites | 0 | intent-audit.md | applies; binds `3ebaa4ee+d175-post`, baseline `cf34f910` ancestor | closed |
| EC175-02 | 2 | post assertion audit / hash census | `git diff` + full `NotBe` sweep over the 5 files (7 live residual sites) | 0 | assertion-audit.md | applies; binds `3ebaa4ee+d175-post` | closed |
| EC175-03 | 2 | `EagerLoadSpec.Assign` refs | `roslyn members` + `roslyn refs NextORM.Core.EagerLoadSpec.Assign` | 0 | assign-refs.md, assign-roslyn-output.md | applies; `internal`, 6 refs all in `src/nextorm.core` | closed |
| EC175-04 | 2 | core focused baseline+post | FC1-FC6 post (12/60/7/19/93/8) + FC1-FC5 baseline | 0 | focused/core-post/, focused/core-baseline/ | applies; binds `3ebaa4ee+d175-post` | closed |
| EC175-05 | 2 | sqlite focused baseline+post | FS1-FS2 post (7/9) + FS1-FS2 baseline | 0 | focused/sqlite-post/, focused/sqlite-baseline/ | applies; binds `3ebaa4ee+d175-post` | closed |
| EC175-06 | 2 | full core post | `dotnet test tests/nextorm.core.tests -c Debug --no-build` -> 1969/0/0 | 0 | full/core.log | applies; binds `3ebaa4ee+d175-post` | closed |
| EC175-07 | 2 | full sqlite post | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` -> 1311 (1310+1skip)/0 | 0 | full/sqlite.log | applies; binds `3ebaa4ee+d175-post` | closed |
| EC175-08 | 2 | integration iff integration diff else guard | guard (no integration-source diff) **and** unconditional AC3 sweep 3555/0/0/197 | 0 | integration-guard.md, collection/integration.log | predicate: `git diff --name-only` has no `tests/nextorm.integration.tests/**` path -> guard | guard+closed |
| EC175-09 | 2 | build baseline+post | `dotnet build nextorm.slnx -c Debug` -> 0W/0E | 0 | build/baseline.log, build/post.log | applies; binds `3ebaa4ee+d175-post` | closed |
| EC175-10 | 2 | scope/CRLF/no-runtime-diff/coverage guard | `git diff --name-only` = 5 core test files; byte census CRLF; coverage settings unchanged | 0 | scope-guard.md | predicate: no production/public-doc/coverage-policy diff; CRLF-only except documented pre-existing EOF-no-newline in InMemoryTests.cs | closed |

## H rows (equality-comparer inequality; focused runs)

| Row | rv | Scenario / test method | Actual invocation / fragment | Exit/result | Artifact | Predicate / version binding | State |
|---|---|---|---|---|---|---|---|
| H01 | 2 | differing PhysicalColumnName (`Comparer_ShouldDistinguishPhysicalColumnNames`) | FC1 `--filter FullyQualifiedName~NextORM.Core.Tests.SelectExpressionPlanEqualityComparerTests` | 0; 12/0/0 | variants/H01.md, focused/core-post/FC1-selectexpr.log | applies; binds `3ebaa4ee+d175-post` | run-pass |
| H02 | 2 | entity item vs scalar column (`Comparer_ShouldDistinguishEntityItemFromScalarColumn`) | FC1 | 0; 12/0/0 | variants/H02.md | applies; binds `3ebaa4ee+d175-post` | run-pass |
| H03 | 2 | different projection slots (`Comparer_ShouldDistinguishProjectionItemSlots`) | FC1 | 0; 12/0/0 | variants/H03.md | applies; binds `3ebaa4ee+d175-post` | run-pass |
| H04 | 2 | wide-count flag vs ordinary int (`Wide_count_flag_participates_in_the_plan_identity`) | FC3 | 0; 7/0/0 | variants/H04.md, focused/core-post/FC3-implicitnav.log | applies; binds `3ebaa4ee+d175-post` | run-pass |
| H05 | 2 | raw binding different entity type (`SameSqlAndColumns_DifferentEntityType_NotEqual`) | FC2 | 0; 60/0/0 | variants/H05.md, focused/core-post/FC2-rawsource.log | applies; binds `3ebaa4ee+d175-post` | run-pass |
| H06 | 2 | raw binding different declared columns (`SameTypeSameColumnCount_DifferentColumnNames_NotEqual`) | FC2 | 0; 60/0/0 | variants/H06.md | applies; binds `3ebaa4ee+d175-post` | run-pass |
| H07 | 2 | same command different rendered SQL (`QueryPlan_ShouldCompareByCommandAndSql`) | FC4 | 0; 19/0/0 | variants/H07.md, focused/core-post/FC4-plankey.log | applies; binds `3ebaa4ee+d175-post` | run-pass |
| H08 | 2 | two different query plans (`TestQueryPlanCache`) | FC5 | 0; 93/0/0 | variants/H08.md, focused/core-post/FC5-inmemory.log | applies; binds `3ebaa4ee+d175-post` | run-pass |

## C rows (equal-plan coherence; source files NOT in the diff)

Each C row names the test method actually selected by the cited focused run, plus the full/coverage
sweeps that also exercise it (not "untouched" alone). Predicate: the file is unchanged by R175, so the
executed result is directly attributable at the same revision token.

| Row | rv | Site / test method | Actual invocation / fragment | Exit/result | Artifact | Predicate / version binding | State |
|---|---|---|---|---|---|---|---|
| C01 | 2 | `JoinIntoPlanKeyTests.cs:63` `RepeatedDeclaration_ShouldHitTheSamePlan` (:54) | FC6 `JoinIntoPlanKeyTests` 8/0/0; corroborated by full core 1969/0/0 and coverage sweep 9818/0 | 0 | variants/C01.md, focused/core-post/FC6-joinplan.log, full/core.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |
| C02 | 2 | `JoinIntoPlanKeyTests.cs:91` `DistinctOneToOneNavigations_ShouldHaveDistinctPlanKeys` (:67) | FC6 8/0/0; full core 1969/0/0; coverage 9818/0 | 0 | variants/C02.md, focused/core-post/FC6-joinplan.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |
| C03 | 2 | `JoinIntoPlanKeyTests.cs:106` `DistinctManyToManyJunctionTypes_ShouldHaveDistinctPlanKeys` (:95) | FC6 8/0/0; full core 1969/0/0; coverage 9818/0 | 0 | variants/C03.md, focused/core-post/FC6-joinplan.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |
| C04 | 2 | `JoinIntoPlanKeyTests.cs:135` `StructurallyIdenticalManyToManyDeclarations_ShouldReuseOnePlan` (:124) | FC6 8/0/0; full core 1969/0/0; coverage 9818/0 | 0 | variants/C04.md, focused/core-post/FC6-joinplan.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |
| C05 | 2 | `JoinIntoSqlGenerationTests.cs:136` `JoinInto_DistinctSpecs_ShouldHaveDistinctPlanKeys_AndRepeatsHit` (:111) | FS2 `JoinIntoSqlGenerationTests` 9/0/0 (baseline FS2 9/0/0 too); corroborated by full sqlite 1311/0 and coverage sweep 9818/0 | 0 | variants/C05.md, focused/sqlite-post/FS2-joinsql.log, full/sqlite.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |

## X rows (execution / identity / cache-miss; source files NOT in the diff)

| Row | rv | Site / test method | Actual invocation / fragment | Exit/result | Artifact | Predicate / version binding | State |
|---|---|---|---|---|---|---|---|
| X01 | 2 | `JoinIntoExecutionTests.cs:55` `ListTerminal_ShouldExecuteOneRoundTrip_AndGroupChildren` (:44) `Executions.Should().Be(1)` | FS1 `JoinIntoExecutionTests` 7/0/0 (baseline FS1 7/0/0 too); corroborated by full sqlite 1311/0 and coverage sweep 9818/0 | 0 | variants/X01.md, focused/sqlite-post/FS1-joinexec.log, full/sqlite.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |
| X02 | 2 | `JoinIntoExecutionTests.cs:76` `ListTerminalAsync_ShouldExecuteOneRoundTrip_AndGroupChildren` (:65) `Executions.Should().Be(1)` | FS1 7/0/0; full sqlite 1311/0; coverage 9818/0 | 0 | variants/X02.md, focused/sqlite-post/FS1-joinexec.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |
| X03 | 2 | `CommonTestSuite.JoinInto.cs:221-222` `JoinInto_ListTerminal_ShouldExecuteOneDenormalizedRoundTrip` (`Executing`/`Executed == 1`) | guard (source not in diff) + unconditional integration sweep 3555/0/0/197, availability grep=0 | 0 | integration-guard.md, collection/integration.log, collection/availability.log | predicate: no `tests/nextorm.integration.tests/**` path in `git diff --name-only` -> guard | guard+run-pass |
| X04 | 2 | `JoinIntoSqlGenerationTests.cs:159-160` `JoinInto_RepeatedDeclaration_ShouldReuseCachedPreparedPlan` (:140) `ReferenceEquals(first, repeat).Should().BeTrue()` | FS2 9/0/0; full sqlite 1311/0; coverage 9818/0 | 0 | variants/X04.md, focused/sqlite-post/FS2-joinsql.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |
| X05 | 2 | `JoinIntoSqlGenerationTests.cs:161-162` same test `ReferenceEquals(first, distinct).Should().BeFalse()` (cache miss) | FS2 9/0/0; full sqlite 1311/0; coverage 9818/0 | 0 | variants/X05.md, focused/sqlite-post/FS2-joinsql.log | applies; file not in diff; binds `3ebaa4ee+d175-post` | run-pass |

## AC rows (acceptance criteria)

| Row | rv | Scenario | Actual invocation / fragment | Exit/result | Artifact | Predicate / version binding | State |
|---|---|---|---|---|---|---|---|
| AC175-01 | 2 | build 0W/0E | `dotnet build nextorm.slnx -c Debug` (+ `--no-restore`) | 0; `0 Warning(s) 0 Error(s)` | build/post.log | applies; binds `3ebaa4ee+d175-post` | run-pass |
| AC175-02 | 2 | unit 11 projects 6262+1skip=6263/0fail | per-project `dotnet test <proj> -c Debug --no-build` x11 | 0; `AGGREGATE total=6263 succeeded=6262 skipped=1 failed=0 rc_all=0` | collection/unit.log | applies; drift +16 vs planned 6247 (R184 core 1953->1969), not an AC relaxation; binds `3ebaa4ee+d175-post` | run-pass |
| AC175-03 | 2 | integration 3555/0err/0fail/197skip + availability | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor`; `grep -c "is not available"`=0 (grep exit 1) | 0; `Total: 3555, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0` | collection/integration.log, collection/availability.log | applies; binds `3ebaa4ee+d175-post` | run-pass |
| AC175-04 | 2 | coverage >=85/75 | collect: `dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` (exit 0; 9818/0/198); report: `reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:+nextorm.*` (exit 0, `2026-10-09T18:23Z`) | 0 / 0; line 88.3% / branch 80.3% (27179 of 33817) | collection/coverage.log, collection/coverage-collect.log, collection/coverage-report.log (`exit_code=0`), collection/coverage-Summary.txt | applies; collect exit 0 recorded in evidence.json `executions[]`; report exit 0 captured in coverage-report.log | run-pass |
| AC175-05 | 2 | docfx 2w/0e | `dotnet docfx docs/docfx.json` | 0; 2 warning(s) / 0 error(s), both pre-existing duplicate AnalyzerReleases | collection/docfx.log | applies; binds `3ebaa4ee+d175-post` | run-pass |

## Guard rows

| Row | Applicability | State |
|---|---|---|
| null/default | No null/default branch introduced or removed | guard |
| value/reference | Comparer types and operand kinds preserved; inequality by equality not hash uniqueness | guard |
| collision synthesis | Deferred (trigger: comparer/hash change or explicit collision-robustness requirement) | deferred |
| X03/EC175-08 | Integration source diff: none (`git diff --name-only` = five core test files) -> guard; AC3 sweep still run unconditionally | guard+run-pass |
