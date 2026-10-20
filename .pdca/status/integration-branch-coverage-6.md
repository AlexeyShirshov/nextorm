# integration-branch-coverage-6

Status: CLOSED
Current cycle: 6
Plan revision: r2
Contract revision: rv2
Attempt: n 1/3
Phase: ACT closed (EXIT)

## Goal

Raise counted integration-suite branch coverage by at least 250 counted branches (floor
+250; target 26793/38317, i.e. 69.2 % -> >= 69.93 %) with tests executing against REAL
databases only. The counted denominators stay pinned at 38317 branches / 59713 statements /
8191 methods; the thresholds stay untouched at MIN_LINE_COVERAGE=85, MIN_BRANCH_COVERAGE=75,
MIN_INTEGRATION_LINE_COVERAGE=65, MIN_INTEGRATION_BRANCH_COVERAGE=75. No denominator edit,
no exclusion-filter edit, no InMemory substitution, no product-code change. Proven-
unreachable arms are recorded with the exact observation and never asserted as passing.

## Baseline (pinned, post-cycle-5 T4/F4 counted model)

| metric | value |
|--------|-------|
| branch covered/total | 26543/38317 (69.2 %) |
| line covered/total | 46354/59713 (77.6 %) |
| methods covered/total | 5641/8191 |
| full integration suite | 4546 discovered / 4351 passed / 0 failed / 195 skipped / 37 skip reasons |
| working tree | HEAD c99cc512, 21 tree entries |
| protected concurrent files | 6/6 sha256 clean |

Source of truth: `tests/coverage/integration-report/Summary.txt` (cobertura ->
reportgenerator Clover -> Summary). Raw cobertura is targeting-only, never a counted claim.
Baseline fingerprint: `rv1/inputs/baseline.json` (cobertura sha256
48140c454c9a305320b5c133cf69c65d4e7be98f2c01d180340adc018316342b, summary sha256
48da87c3fb66b214d9197359fba747b219e37373796b35b59516f77091646c6a).

Observed drift at D0 (recorded, not used to edit the pin): the workspace HEAD is now
`c9aa8690` (two T247 commits after `c99cc512`: `DataContext.cs`, `DataContextCache.cs`,
`Json/JsonRowWriterCache.cs`, `Json/JsonRowWriterFactory.cs`; 4 src files, +396/-6), and
`git status --short` reported 18 entries before this cycle's status file was written (19 with it). None of the four changed src files is a target of
this revision and none is protected, but the CHECK delta must re-measure rather than reuse
the cycle-5 absolute numbers. See `rv1/results/baseline-verify.txt` and
`rv1/results/tree-d0.txt`.

## Final numbers (r2)

The ONE r2 acceptance sweep: F2 GREEN (exit 0, Total 4637 / Passed 4442 / Failed 0 /
Skipped 195 / Not Run 0, 87.263 s, `rv1/logs/F2.log`) followed by T2 (exit 0,
`rv1/logs/T2.log`, `rv1/results/final2-Summary.txt`).

| metric | baseline (pinned) | T2 final (literal) | T247-excluded | cycle delta |
|--------|-------------------|--------------------|---------------|-------------|
| branch covered / total | 26543 / 38317 (69.2 %) | 26924 / 38381 | **26877 / 38317 (70.1 %)** | **+334** |
| line covered / total | 46354 / 59713 (77.6 %) | 46714 / 59908 | **46560 / 59713 (78.0 %)** | **+206** |
| methods covered / total | 5641 / 8191 | 5670 / 8213 | denominator 8191 (numerator not re-credited) | +29 literal |
| full integration suite | 4546 / 4351 / 0 / 195 | 4637 / 4442 / 0 / 195 / 0 | — | +91 discovered, +91 passed |

- Denominator drift after the r2 sweep is branch +64, line +195, methods +22, all of it the
  concurrent T247 files (`JsonRowWriterCache.cs` +64 branches / +182 lines / +18 methods,
  `DataContextCache.cs` +3 lines / +2 methods, `DataContext.cs` +10 lines / +2 methods,
  `JsonRowWriterFactory.cs` no total change). Excluding those four files restores the pinned
  denominators **38317 / 59713 / 8191 exactly**.
- T247-excluded line covered = 46714 − 154 = **46560**; the 154 is the per-file
  `line_covered_delta` of the four T247 files in `rv1/inputs/contributions2.json`
  (+145 new file, +6 DataContext.cs, +3 DataContextCache.cs, 0 JsonRowWriterFactory.cs),
  cross-checked against both coberturas (same 145 / 6 / 3 / 0).
- Floor: target 26793, measured T247-excluded 26877 → **margin +84** branches. The literal
  counted total 26924 is +131 above target but is not claimed as cycle-6 achievement.
- Raw per-file branch partition: **+227** across 19 positive files, **0 negative files**;
  top: QueryPlanEqualityComparer +65, JsonRowWriterCache +45 (T247, not credited),
  QueryCommand.QueryPreparer +40, EntityBuilder +18, SqlBuilder +16, SqlSourceRenderer +11
  (protected file, unchanged, marked separately), QueryCommand +11.
- Docs baselines now state the measured T247-excluded values: `78.0 % line / 70.1 % branch`
  in `AGENTS.md` and `.github/workflows/dotnet.yml`. The gate is **still RED**:
  70.1 % < `MIN_INTEGRATION_BRANCH_COVERAGE=75`. Thresholds stay byte-identical
  (85 / 75 / 65 / 75; `rv1/results/threshold-diff.txt`).

## Acceptance criteria

| id | requirement | negative case |
|----|-------------|---------------|
| REQ-COV | counted covered branches >= 26793 on the unchanged 38317 denominator after exactly ONE green boundary F then T; numerator, denominator, losses and drift recorded separately | raw-cobertura gain, exclusion-filter edit, denominator edit, concurrent change credited, partial/red sweep presented as green |
| REQ-LIVE | every new fact executes against a real DBMS (file SQLite or a Testcontainers provider); no InMemoryDataContext; class-owned `pk6_*`/provider markers; FluentAssertions only; IntegrationSuiteScopingTests green; skip budget pin 195/37 | translation-only or vacuous facts, unexpected skips, a fact that needs no database |
| REQ-QUALITY | whole-solution `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors; CRLF; no NoWarn/suppression; no edit to the six protected src files | bypassing the warning gate or editing production code to make a fact pass |
| REQ-BOUNDARY | exactly ONE F with DOCKER_HOST set and NEXTORM_SKIP_PROVIDERS unset, all container providers fail-closed; T with `-classfilters:"-NextORM.Core.InMemory*"`; inner runs inside the frozen scope; ONE F+T only | provider skipped, a second sweep without a PLAN decision, broadened inner scope |
| REQ-DOCS | D7 writes only measured baseline numbers and status finalization; thresholds 85/75/65/75 byte-identical; no public docs link to specs | threshold change, docs/** or docs/ru/** edit beyond the measured-number statement |
| REQ-DEFECTS | each observed defect has a stable key, a live-DB minimal repro, a deduplicated ticket/search log, a verified issue URL and an applied-fix count; #303/#304/#307/#308/#309 stay blockers | silently dropping a failing fact or filing a duplicate |
| REQ-STATE | durable state (`cycle`, `plan_revision`, `attempt`, phase) and defect history survive restart; scope sealed before the first test edit; evidence contract complete (R01..R07, E01..E03, V01..V15) | state reconstructed from a dispatch, scope amended after edits |

## Tasks (round r1)

| id | owner | files (PLANNED) | targets | provider | forecast |
|----|-------|-----------------|---------|----------|----------|
| D0 | coder | status, rv1/{inputs,results,inner,logs,variants}, D1.brief.json, baseline copy, tree snapshot | state sealed before any test edit; protected 6 sha256 re-verified; baseline cobertura reproduces the pinned totals | none | 0 |
| D1 | coder | `tests/nextorm.integration.tests/Sqlite/PlanKeyBranchCoverage6Tests.cs` | QueryPlanEqualityComparer WindowsEqual :249, EqualsCore :67, CteDefinitionsEqual :337; ExpressionPlanEqualityComparer Compare2 :311, CompareLambda :389, CompareBinding :572, VisitIndex :807 | SQLite | allocation 80, forecast 90, fact cap 30. Fallback: KeyValueListsEqual :287, ExpressionListsEqual :302, CteBodiesEqual :59, StringListsEqual :235 |
| D2 | coder | `tests/nextorm.integration.tests/Sqlite/PreparerBranchCoverage6Tests.cs` | QueryCommand.QueryPreparer PrepareColumns :507, ScanUnkeyedTupleInFrom :3007, ScanUnkeyedScalarInFrom :3263, ScanUnkeyedTupleInValues :2853, RefreshInValuesShape :196 | SQLite | allocation 70, forecast 80, fact cap 28. Fallback: IsTransparentMember :2653, RawShape.Matches :3703, CaptureRawShape :3892 |
| D3 | coder | `tests/nextorm.integration.tests/Coverage6/{Sqlite,Postgres,SqlServer,ClickHouse}RenderingBranchCoverage6Tests.cs` | SqlBuilder MakeSelect :36, MakeExtremeRowOuter :1273, MakeNamedWindow :2115; SqlSourceRenderer MakeFrom :433, MakeJoin :187, MakeWithClause :23 | SQLite/Postgres/SQL Server/ClickHouse | allocation 55, forecast 65, fact cap 24 (6/provider). Fallback: MakeUpdateJoin :1696, MakeDeleteJoin :1582, TryGetIdentityAddress :2013, RenderResultSchema :873 |
| D4 | coder | `tests/nextorm.integration.tests/Sqlite/EagerBuilderBranchCoverage6Tests.cs` | EntityBuilder UnsupportedChildShapes :1208, CreatePairCommand :1528, get_HasCompositionState :2433 | SQLite | allocation 25, forecast 35, fact cap 16. Fallback: AliasRoot :3278, IsPlainScalarScope :1300, EagerLoading.Assign :787 |
| D5 | coder | `tests/nextorm.integration.tests/Sqlite/CorrelatedBranchCoverage6Tests.cs` | CorrelatedQueryExpressionVisitor VisitBinary :1684, VisitMethodCall :169, IsAggregateTerminal :414; AggregateTerminalRewriter AggregateMethodFor :56 | SQLite | allocation 20, forecast 30, fact cap 14. Fallback: UnwrapLambda :49 and other supported VisitMethodCall arms |
| D6 | coder | boundary evidence rv1/results, manifest | ONE acceptance F + T; skip-budget and scoping guards; counted delta attribution | all providers | 0 (one F + T, boundary only) |
| D7 | coder | status finalization, measured-number statement | docs + durable state, deferred register carry-forward, defect ledger | none | 0 |

## Fact budget

Total hard cap 112 facts, allocation 30 (D1) + 28 (D2) + 24 (D3) + 16 (D4) + 14 (D5).
Any extra fact requires a planner revision; no unit may exceed its allocation. Facts that
cannot reach their planned arm are recorded in `rv1/inputs/deferred.json` with the exact
observation and do not consume a passing-claim slot.

## Test strategy

- Inner loop uses the exact selector `-class NextORM.Integration.Tests.<Class>` only; no
  whole-project or whole-solution test runs inside DO.
- SQLite inner runs:
  `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -class NextORM.Integration.Tests.<Class>`.
- Provider inner runs (D3): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`
  with NEXTORM_SKIP_PROVIDERS unset, one class at a time.
- Exactly ONE boundary sweep: one `dotnet-coverage collect` F over the whole integration
  project followed by one reportgenerator T (`-classfilters:"-NextORM.Core.InMemory*"`).
- Variant matrix V01..V15 (see evidence contract): each row names an arm cluster, its
  provider, its primary or fallback status, and the artifact that records its verdict.

## Boundary policy

Exactly ONE acceptance F + T at DO->CHECK. A red F is preserved as evidence and never
retried. A repeated provider timeout stops that task's fact set and returns it to PLAN. No
second sweep, no retry without an explicit planner revision. A green F whose counted total
is below the 26793 floor returns to PLAN with measured numbers; thresholds, denominators and
exclusions are never adjusted.

## Evidence contract

| row | requirement | owner | verifier | artifact |
|-----|-------------|-------|----------|----------|
| R01 | REQ-COV: counted delta on 38317 denominator, losses/drift separated | coder | check | `rv1/results/coverage-delta.md`, `rv1/results/T.log` |
| R02 | REQ-LIVE: live DB per fact, skip ledger 195/37, scoping guard | coder | check | `rv1/results/skip-ledger.json`, inner logs |
| R03 | REQ-QUALITY: build 0/0, CRLF, no suppression, protected 6 sha256 | coder | check | `rv1/logs/*-build-*.log`, `rv1/results/tree-d0.txt` |
| R04 | REQ-BOUNDARY: ONE F + T, DOCKER_HOST set, no skip env, InMemory classfilter | coder | check | `rv1/results/F.log`, `rv1/results/T.log` |
| R05 | REQ-DOCS: measured numbers only, thresholds 85/75/65/75 unchanged | coder | check | `rv1/results/threshold-diff.txt` |
| R06 | REQ-DEFECTS: stable keys, live repro, dedup log, issue URL, fix count | coder | check | `rv1/inputs/defects.json` |
| R07 | REQ-STATE: durable state, sealed scope, complete contract | coder | check | `.pdca/status/integration-branch-coverage-6.md`, `rv1/inner/*`, manifest |
| E01 | build evidence per unit | coder | check | `rv1/logs/<unit>-build-<n>.log` |
| E02 | inner filtered run evidence per unit | coder | check | `rv1/logs/<unit>-run-<n>.log`, `rv1/inner/<unit>.report.json` |
| E03 | boundary F + T evidence | coder | check | `rv1/results/F.log`, `rv1/results/T.log`, `rv1/results/final*/` |
| V01 | D1 WindowsEqual frame cluster (type/start/end/offsets/exclusion), SQLite | coder | check | `rv1/results/v01.txt` |
| V02 | D1 WindowsEqual presence, partition/order counts, direction, name, SQLite | coder | check | `rv1/results/v02.txt` |
| V03 | D1 EqualsCore ordered equal-prefix one-field negatives, SQLite | coder | check | `rv1/results/v03.txt` |
| V04 | D1 CteDefinitionsEqual/CteBodiesEqual, SQLite | coder | check | `rv1/results/v04.txt` |
| V05 | D1 KeyValueListsEqual/StringListsEqual, SQLite | coder | check | `rv1/results/v05.txt` |
| V06 | D1 ExpressionListsEqual/GroupingSetsEqual where publicly reachable, SQLite | coder | check | `rv1/results/v06.txt` |
| V07 | D1 ExpressionComparer Compare2/CompareLambda, SQLite | coder | check | `rv1/results/v07.txt` |
| V08 | D1 ExpressionComparer CompareBinding/VisitIndex, SQLite | coder | check | `rv1/results/v08.txt` |
| V09 | D2 PrepareColumns shapes, SQLite | coder | check | `rv1/results/v09.txt` |
| V10 | D2 ScanUnkeyedTupleInFrom/ScanUnkeyedScalarInFrom, SQLite | coder | check | `rv1/results/v10.txt` |
| V11 | D2 ScanUnkeyedTupleInValues/RefreshInValuesShape, SQLite | coder | check | `rv1/results/v11.txt` |
| V12 | D2 fallbacks IsTransparentMember/RawShape.Matches/CaptureRawShape, SQLite | coder | check | `rv1/results/v12.txt` |
| V13 | D3 provider rendering SqlBuilder/SqlSourceRenderer x 4 providers | coder | check | `rv1/results/v13-*.txt` |
| V14 | D4 EntityBuilder + eager fallbacks, SQLite | coder | check | `rv1/results/v14.txt` |
| V15 | D5 correlated visitor + aggregate terminal rewrite, SQLite | coder | check | `rv1/results/v15.txt` |

## Docs plan

D7 writes only measured baseline numbers (no projection), keeps the four thresholds
byte-identical, and updates `.pdca/status/integration-branch-coverage-6.md` to its final
phase. No `docs/**` or `docs/ru/**` edit is planned; public pages never link to specs.

## Perf decision

Not needed: tests only. The cached preparation path is exercised, never changed; no
benchmark is run and no performance acceptance applies.

## Recon decision

Not needed: `rv1/inputs/targeting.md` already carries the counted and raw top-25 tables, the
per-method uncovered-branch inventory for every focus file, and the comparer-family
inventory from the cycle-5 sweep.

## Unit execution mode

One working tree, sequential units, no worktrees; the 21 pre-existing tree entries are
preserved as found (18 at D0, recorded). No commits, no push, no branch/merge operation.

## Deferred register (carried from cycle 5, 15 rows)

| id | disposition | trigger |
|----|-------------|---------|
| D1-CH-SAMPLE | deferred | a class-owned MergeTree-family table (with a sampling key) that the provider suite may create on ClickHouse |
| D1-SS-INDEX-IGNORE | deferred | a public SQL Server index-ignore form is added; the negative pair over a real index now uses two real indexes |
| D2-GROUPING-CUBE | deferred | a provider that renders CUBE, with live rows |
| D2-TUPLE-PROJECTION | blocked (#307) | the tuple-projection limitation is lifted |
| D2-ALIAS-SETTER | deferred | a public From/Join alias override that participates in the plan key |
| D3-DERIVED-ROOT-RAWSHAPE | deferred | the raw descriptor learns to capture a derived source |
| D3-WHERE-IN-SHAPE | deferred | the captured-value ineligibility is lifted for value-free shapes |
| D4-NONE | n/a | no reachable arm was dropped |
| D5-CHILD-FACTORY-JOININTO | deferred | a public single-query fold that consumes a child JoinInto declaration |
| D5-JOININTO-CHILD-WHERE | deferred | the JoinInto pair-command contract explicitly folds the child source condition |
| D1-08-SS-SUBQUERY-HINT | deferred | SQL Server gains a public sub-query hint spelling |
| D6-CH-SETTINGS-SCALAR | deferred | the scalar terminal stops appending LIMIT 1 after the SETTINGS clause |
| D2-FROM-CAPTURED-VALUE-NOT-A-SHAPE | deferred | an arm that changes the inner predicate structure is assertable in the same fact |
| D2-GROUPING-SETS-LIVE | deferred | a SQLite build with grouping-set support, or a provider-neutral live form |
| D5-CHILD-PAGING-GUARD | deferred | a nested paging form that the per-parent source refuses and the single-query fold rejects |

Cycle-6 additions (the full register is `rv1/inputs/deferred.json`, **17 rows**):

| id | disposition | trigger |
|----|-------------|---------|
| D1B-GROUPING-SETS-SQLITE | deferred | a SQLite build with grouping-set support, or a provider-neutral live form that SQLite renders without the ANSI extension (the pair executes on PostgreSQL) |
| D3-CH-LEGACY-WITHRECURSIVE | deferred | ClickHouse starts gating the legacy `WithRecursive` declaration, or the legacy form is removed in favour of the typed `AsRecursiveCte` surface |

#303 (correlated aggregate-terminal predicates), #304 (reference-hop shapes), #307 (tuple
projection), #308 and #309 remain blockers: no fact in this revision asserts behaviour
behind them.

## Risks

- Baseline drift: HEAD moved from `c99cc512` to `c9aa8690` (T247 JSON cache, 4 src files)
  after the cycle-5 sweep. D0 records it; CHECK must attribute the delta on the current
  counted model and must not credit concurrent change to this revision.
- WindowsEqual has 55 uncovered branches but may have no public frame API: the D1 probe
  decides, and a partially unreachable target is recorded, never invented.
- D3 provider classes need containers; a provider timeout stops that task for PLAN, it is
  never retried inside DO.
- Per-unit fact caps (30/28/24/16/14) are hard; overflow needs a planner revision.

## Defect history

| key | class | status | filed | duplicates | applied fixes | evidence | last occurrence |
|-----|-------|--------|-------|------------|---------------|----------|-----------------|
| CH-CLEANUP-TIMEOUT | environmental (pre-existing cycle-3 test `ClickHouseDialectBranchCoverage3Tests.PagedSelect_WithOrderBy_ShouldReturnTheRequestedPage`, HttpClient 120 s timeout in DropMarker) — not a product defect | did not repeat in r2 | 0 | 0 | 0 | `rv1/logs/F.log` (r1 red F), `rv1/logs/F2.log` (r2 green F) | r1/n1 |

No other defect key was observed in cycle 6. Every red unit run was a test-code/expectation
fix and is retained under `rv1/logs`; the r2 boundary F is green. `filed: 0`, `duplicates: 0`,
`applied_fixes: 0` (`rv1/inputs/defects.json`). Blockers **#303 / #304 / #307 / #308 / #309**
remain open; no cycle-6 fact asserts behaviour behind them.

## Durable state

Historical r1 state (superseded by the canonical r2 CHECK block below; kept for recovery):

```yaml
task: integration-branch-coverage-6
cycle: 6
plan_revision: r2
contract_revision: rv2
attempt: n 1/3
phase: DO
status: OPEN
baseline:
  branch_covered: 26543
  branch_total: 38317
  line_covered: 46354
  line_total: 59713
  methods_covered: 5641
  methods_total: 8191
  suite_discovered: 4546
  suite_passed: 4351
  suite_failed: 0
  suite_skipped: 195
  skip_reasons: 37
  head: c99cc512
  tree_entries: 21
floor_counted_branches: 250
target: 26793
thresholds: [85, 75, 65, 75]
defect_history: []
```

## Progress log

2026-10-18T09:05:00Z | PLAN | revision r1 | iteration n 1/3 | plan r1 accepted; scope sealed for D0 | .pdca/integration-branch-coverage-6/rv1/inputs/targeting.md
2026-10-18T09:11:17Z | DO | revision r1 | iteration n 1/3 | DO started; D0 seal in progress | .pdca/integration-branch-coverage-6/rv1/
2026-10-18T09:24:00Z | DO | revision r1 | iteration n 1/3 | D0 seal complete: dirs created; D1 brief validated rc 0; protected 6/6 sha256 match; archived cobertura reproduces 26543/38317 branches, 46354/59713 lines, 5641/8191 methods; HEAD drift c99cc512 -> c9aa8690 and tree 21 -> 19 entries recorded | rv1/results/tree-d0.txt, rv1/results/baseline-verify.txt
2026-10-18T09:40:00Z | DO | revision r1 | iteration n 1/3 | D1 window probe: public frame API present (WindowFrame.Rows/Range/Groups, Preceding/Following/CurrentRow/Unbounded*, WithExclusion; EntityBuilder.Window + WindowFunction.Over(name)); VisitIndex arm unreachable (String.get_Chars NotSupportedException) and not asserted | rv1/inputs/window-probe.md
2026-10-18T10:00:00Z | DO | revision r1 | iteration n 1/3 | D1 class green: selected 8 / passed 8 / failed 0 / skipped 0; arms: WindowsEqual frame unit/start kind/start offset/end kind/end offset/exclusion/presence/window name/window count/partition count+expression/order count+direction+expression; CteDefinitionsEqual+CteBodiesEqual (body/count/recursive/max-recursion); StringListsEqual via index hints (length arm unreachable: SQLite INDEXED BY takes exactly one name); CompareBinding member/expression/binding-count through member-init; 27 differential pairs in 8 facts | rv1/inner/D1.report.json, rv1/logs/D1-run-4.log
2026-10-18T10:01:00Z | DO | revision r1 | iteration n 1/3 | D3 brief validated rc 0 (4 selectors, 4 files); whole-solution build `dotnet build nextorm.slnx -c Debug` exit 0, 25 projects, 0 warnings / 0 errors | rv1/inner/D3.brief.json, rv1/logs/D3-build-10.log
2026-10-18T10:02:00Z | DO | revision r1 | iteration n 1/3 | D3 ClickHouse red run retained: 5/6, `NamedWindow_ShouldRankInsideEachPartition` expected a recursive-CTE rejection that ClickHouse does not apply to legacy `WithRecursive`; the non-arm assertion was removed, every named-window ranking assertion and exact row kept; arms document records the observation | rv1/logs/D3-clickhouse-run-4.log, rv1/inputs/rendering-arms.md
2026-10-18T10:03:00Z | DO | revision r1 | iteration n 1/3 | D3 classes green: SQLite 6 selected / 6 passed / 0 failed, Postgres 6/6/0, SQL Server 6/6/0, ClickHouse 6/6/0 (exit 0 each); 24 facts total = cap 24 (6/provider); guard IntegrationSuiteScopingTests 2/2 exit 0 | rv1/logs/D3-sqlite-run-3.log, D3-postgres-run-2.log, D3-sqlserver-run-4.log, D3-clickhouse-run-5.log, D3-guard-1.log
2026-10-18T10:04:00Z | DO | revision r1 | iteration n 1/3 | D3 unreachable arms recorded: ClickHouse legacy `WithRecursive` not gated (typed `AsRecursiveCte` owns the refusal), SQLite `MakeDeleteJoin` render body, SQL Server `MakeNamedWindow` body, PostgreSQL index-hint arms, SQLite PREWHERE branch; SQLite param-mode named-window note and the three fallbacks (MakeExtremeRowOuter, TryGetIdentityAddress, RenderResultSchema) recorded as not authored, not unreachable; D3 report validator rc 0 | rv1/inputs/rendering-arms.md, rv1/inner/D3.report.json
2026-10-18T10:15:00Z | DO | revision r1 | iteration n 1/3 | D4 brief validated rc 0 before edits (1 selector, 1 new file); class EagerBuilderBranchCoverage6Tests authored with 14 facts (cap 16): 5 UnsupportedChildShapes child-shape guards each with a live split companion, 4 CreatePairCommand facts (m:n multi-key, async two collections, derived filtered source, empty derived source), 5 BindEntity composition/state facts (flat scalar, reference nav, collection nav, inherited member, nullable composite) | rv1/inner/D4.brief.json, rv1/inputs/eager-arms.md
2026-10-18T10:16:00Z | DO | revision r1 | iteration n 1/3 | D4 red runs retained: D4-build-1 (5 compile errors: nullable collection selector + FA Equal overload), D4-run-1/D4-run-2 (plain-child JoinInto source does not fold its Where: 9613 still stitched, #309 observation never asserted, filtered/empty facts switched to a derived From(QueryCommand) child source) | rv1/logs/D4-build-1.log, rv1/logs/D4-run-1.log, rv1/logs/D4-run-2.log
2026-10-18T10:19:00Z | DO | revision r1 | iteration n 1/3 | D4 class green: 14 selected / 14 passed / 0 failed / 0 skipped (exit 0); whole-solution build `dotnet build nextorm.slnx -c Debug` exit 0, 25 projects, 0 warnings / 0 errors; guard IntegrationSuiteScopingTests 2/2 exit 0; 14/16 facts, unreachable HasCompositionState arms (_query/_joins/_joinIntos/_loadSpecs and internal provider-extension setters) recorded, no fallback fact needed | rv1/inner/D4.report.json (validator rc 0), rv1/logs/D4-run-3.log, D4-build-4.log, D4-guard-2.log
2026-10-18T10:26:00Z | DO | revision r1 | iteration n 1/3 | D5 brief validated rc 0 BEFORE edits (1 selector, 1 new file); class CorrelatedBranchCoverage6Tests authored against class-owned co6_parent/co6_child/co6_note with live SQLite execution | rv1/inner/D5.brief.json, rv1/logs/D5-brief-1.log
2026-10-18T10:27:00Z | DO | revision r1 | iteration n 1/3 | D5 red runs retained: D5-build-1 (8 compile errors: unconstrained TResult? on Stdev/Var is not Nullable<double>), D5-run-1 (captured null binds a parameter, so amount = @p is unknown even for the NULL row - expectation corrected), D5-run-3 (free-standing LongCount in a correlated projection renders a whole-entity sub-select and SQLite rejects it: 'sub-select returns 5 columns - expected 1'; probe removed, not asserted) | rv1/logs/D5-build-1.log, D5-run-1.log, D5-run-3.log
2026-10-18T10:28:00Z | DO | revision r1 | iteration n 1/3 | D5 class green: 9 selected / 9 passed / 0 failed / 0 skipped (exit 0); whole-solution build `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; guard IntegrationSuiteScopingTests 2/2 exit 0; 9 facts = hard cap 14, no fallback fact needed (UnwrapLambda 4 branches recorded unreachable) | rv1/inner/D5.report.json (validator rc 0), rv1/logs/D5-run-4.log, D5-build-7.log, D5-guard-2.log
2026-10-18T10:29:00Z | DO | revision r1 | iteration n 1/3 | D5 arms: AggregateMethodFor/IsAggregateTerminal Stdev+Stdevp+Var+Varp in projections (24 uncovered branches targeted); VisitMethodCall builder-terminal :299 correlated+non-correlated, QueryCommand scalar :327, grandparent marker :335, trace logger :316/:364; VisitBinary shapes probed live; unreachable recorded: VisitBinary :1691/:1696/:1701/:1712/:1719 all IsInMemory-only (31 branches, REQ-LIVE excludes InMemoryDataContext), VisitMethodCall :183/:193/:244-true (_forPrepare false, InMemoryRowMaterializer only), :348 false arm (no public captured-constant-free shape), UnwrapLambda :49 Lambda/default (Quote is the only public shape), All/Average absent from EntityBuilderExtensions, captured decimal unusable on SQLite (TEXT binding) | rv1/inputs/correlated-arms.md, rv1/results/v15.txt
2026-10-18T10:30:00Z | DO | revision r1 | iteration n 1/3 | all implementation units D1-D5 are closed for the DO->CHECK gate: D1 8 facts, D2 (green per its report), D3 24 facts (4 providers), D4 14 facts, D5 9 facts; only the boundary F+T (D6) and finalization (D7) remain | rv1/inner/D1..D5.report.json
2026-10-18T10:26:56Z | CHECK | revision r1 | iteration n 1/3 | boundary preflight: podman socket ping OK; NEXTORM_SKIP_PROVIDERS unset (printenv rc 1); 5 reusable containers listed; no src/tests .cs newer than the Debug test assembly (count 0); protected 6/6 sha256 match; HEAD c9aa8690 (2 concurrent #247 commits after baseline c99cc512), tree 26 entries | rv1/results/boundary-preflight.log
2026-10-18T10:27:30Z | CHECK | revision r1 | iteration n 1/3 | boundary B: `dotnet build nextorm.slnx -c Debug` exit 0, 25 projects, 0 warnings / 0 errors | rv1/logs/B-final.log
2026-10-18T10:27:45Z | CHECK | revision r1 | iteration n 1/3 | boundary G: IntegrationSuiteScopingTests 2 selected / 2 passed / 0 failed / 0 skipped, exit 0 | rv1/logs/G-final.log
2026-10-18T10:30:40Z | CHECK | revision r1 | iteration n 1/3 | boundary F (ONE, not retried): exit 1 RED - Total 4622, Errors 0, Failed 1, Skipped 195, Not Run 0, Time 156.381s; sole failure ClickHouseDialectBranchCoverage3Tests.PagedSelect_WithOrderBy_ShouldReturnTheRequestedPage (HttpClient 120s timeout in DropMarker), a pre-existing cycle-3 test, not a cycle-6 fact; cobertura produced and usable | rv1/logs/F.log
2026-10-18T10:30:50Z | CHECK | revision r1 | iteration n 1/3 | skip budget: rc 0, 195 skips match the pinned budget; the script writes a ledger only with --update, so no rv1/inputs/skip-ledger.json was generated | rv1/results/skip-budget.log
2026-10-18T10:31:10Z | CHECK | revision r1 | iteration n 1/3 | boundary T: rc 0, Summary 26830/38381 branch, 46696/59908 line, 5669/8213 methods (baseline 26543/38317, 46354/59713, 5641/8191) | rv1/logs/T.log, rv1/results/final-Summary.txt
2026-10-18T10:32:00Z | CHECK | revision r1 | iteration n 1/3 | delta: counted numerator +287; denominator drift +64/+195/+22 is entirely #247 (branch +64 = new JsonRowWriterCache.cs; line +182+3+10; methods +18+2+2), pinned denominators restored exactly after exclusion; raw cobertura per-file partition +190 across 18 positive files, 0 losses; protected SqlSourceRenderer +11 (sha unchanged, marked separately); T247 counted credit +47 (JsonRowWriterCache +45, DataContext +2) => cycle-attributable 26783, shortfall -10 vs 26793 (literal counted residual +37); no second sweep run | rv1/results/coverage-delta.md, rv1/inputs/contributions.json

## Plan r2 (replan after CHECK FAIL)

Rationale: the r1 boundary measurement is retained exactly as recorded and is **not re-credited**:
counted numerator 26783/38317 (+240 over the pinned 26543 baseline), shortfall 10 vs the 26793
floor; the r1 F run was RED on a pre-existing environmental ClickHouse cleanup timeout, which is
preserved and never retried. Exactly ONE new acceptance F+T is authorized for r2; no second sweep
of r1 runs and no re-run of the red F. r2 adds 15 facts (total 76 + 15 = 91 of the 112 hard cap):
D1b 8 (6 `EqualsCore` one-field negatives + 2 `GroupingSetsEqual`) and D2b 7 (3
`IsTransparentMember`, 2 `RawShape.Matches`, 2 `CaptureRawShape`). Every fact is an addition to an
existing class; no new file and no new skip.

Allocation (r2, hard caps unchanged):

| unit | class | target method | facts | budget |
|------|-------|---------------|-------|--------|
| D1b | `Sqlite/PlanKeyBranchCoverage6Tests.cs` | `QueryPlanEqualityComparer.EqualsCore:67` | 6 | within D1 30 |
| D1b | `Coverage6/PostgresRenderingBranchCoverage6Tests.cs` | `QueryPlanEqualityComparer.GroupingSetsEqual:316` | 2 | within D1 30 |
| D2b | `Sqlite/PreparerBranchCoverage6Tests.cs` | `QueryPreparer.IsTransparentMember:2653` | 3 | within D2 28 |
| D2b | `Sqlite/PreparerBranchCoverage6Tests.cs` | `QueryPreparer.RawShape.Matches:3703` | 2 | within D2 28 |
| D2b | `Sqlite/PreparerBranchCoverage6Tests.cs` | `QueryPreparer.CaptureRawShape:3892` | 2 | within D2 28 |

Allocation rationale: D1b keeps the SQLite-only rule for the six `EqualsCore` pairs (one context,
two fresh commands per pair, one component difference, equal control) over components not covered
by cycles 5/6 (EntityType, CTE declaration name, UnionQuery operand, EagerLoadShape,
PerParentPaging, ResolvedEagerLoadMode); a SQLite grouping-set execution is not assertable with
exact rows (the bundled engine rejects the rendered syntax), so the two `GroupingSetsEqual` facts
move to the existing PostgreSQL class with live ROLLUP/GROUPING SETS and exact row multisets; the
SQLite unreachability is recorded. D2b probes the bound-source filter dependency walk
(`DateTime` part, `Guid? .Value`, inherited member chain) and the raw-descriptor path on eligible
plain-table shapes (where+join+order control, one-component negative, UNION body, DISTINCT and
ORDER BY+LIMIT).

r2 boundary policy: exactly ONE F+T at DO->CHECK; r1's red F is preserved as evidence and never
retried; a repeated ClickHouse cleanup timeout escalates instead of retrying; a green F whose
counted total is still below 26793 stops with measured numbers and no third sweep. Inner runs stay
inside the frozen r2 scope (`rv1/inner/D1b.brief.json`, `D2b.brief.json`, validated rc 0 before
edits); no product/docs/protected/git edits.

Docs-plan correction: D7 must also record the measured numbers in `AGENTS.md` and
`.github/workflows/dotnet.yml` (measured numbers only; thresholds stay byte-identical at 85, 75,
65, 75; no denominator or exclusion edit). No public `docs/**` link to specs is added.

Durable state (r2, canonical):

```yaml
task: integration-branch-coverage-6
cycle: 6
plan_revision: r2
contract_revision: rv2
attempt: n 1/3
phase: CHECK
status: OPEN
facts_total: 91
facts_cap: 112
contract_rows: 25
contract_rows_met: 22
contract_rows_deferred: 3   # V05 KeyValueListsEqual, V06 ExpressionListsEqual, V08 VisitIndex (machine status open)
baseline:
  branch_covered: 26543
  branch_total: 38317
  line_covered: 46354
  line_total: 59713
  methods_covered: 5641
  methods_total: 8191
  suite_discovered: 4546
  suite_passed: 4351
  suite_failed: 0
  suite_skipped: 195
final_r2:
  branch_covered: 26924
  branch_total: 38381
  line_covered: 46714
  line_total: 59908
  methods_covered: 5670
  methods_total: 8213
  suite_discovered: 4637
  suite_passed: 4442
  suite_failed: 0
  suite_skipped: 195
  suite_not_run: 0
  time: 87.263s
final_t247_excluded:
  branch_covered: 26877
  branch_total: 38317
  branch_pct: 70.1
  line_covered: 46560
  line_total: 59713
  line_pct: 78.0
  methods_total: 8191
target: 26793
margin: 84
raw_partition: 227          # 19 positive files, 0 negative
gate_integration: RED       # 70.1 % < 75 %
thresholds: [85, 75, 65, 75]
evidence:
  boundary_f: rv1/logs/F2.log
  boundary_t: rv1/logs/T2.log
  summary: rv1/results/final2-Summary.txt
  manifest: rv1/manifest.json
  validators: {journal: 0, project: 0, report: 0, manifest: 0, evidence_lint: 0}
deferred_rows: 3
deferred_register: 17
defect_history:
  - key: CH-CLEANUP-TIMEOUT
    observed: r1/n1 boundary F (ClickHouseDialectBranchCoverage3Tests DropMarker HttpClient 120s)
    fixes_applied: 0
    filed: 0
    duplicates: 0
    status: did not repeat in r2/n1 GREEN boundary F (Total 4637, Failed 0); remains environmental, no retry
```

2026-10-18T16:05:00Z | PLAN | revision r2 | iteration n 1/3 | Replanned: CHECK FAIL (counted 26783/38317, +240, shortfall 10; F red on environmental ClickHouse timeout); exactly one new acceptance F+T authorized; 15 additional facts (budget 76+15=91/112) | .pdca/status/integration-branch-coverage-6.md
2026-10-18T10:54:41Z | DO | revision r2 | iteration n 1/3 | D1b brief validated rc 0 before edits (2 selectors, 2 files); PlanKeyBranchCoverage6Tests red 14 selected / 11 passed / 3 failed (union row expectations, params-overload assertion, SingleQuery child OrderBy guard), test-only fixes, affected rebuild 0/0, green 14/14 exit 0 | rv1/inner/D1b.brief.json, rv1/logs/D1b-run-2.log, D1b-build-3.log, D1b-run-3.log
2026-10-18T10:54:41Z | DO | revision r2 | iteration n 1/3 | D2b brief validated rc 0 before edits (1 selector, 1 file); PreparerBranchCoverage6Tests red 28 selected / 27 passed / 1 failed (raw-descriptor differential asserted through the prepared-form comparer), test-only fix, affected rebuild 0/0, green 28/28 exit 0 | rv1/inner/D2b.brief.json, rv1/logs/D2b-run-2.log, D2b-build-2.log, D2b-run-3.log
2026-10-18T10:54:41Z | DO | revision r2 | iteration n 1/3 | Postgres grouping-set placement: the 2 GroupingSetsEqual facts live in Coverage6/PostgresRenderingBranchCoverage6Tests.cs because SQLite rejects the rendered GROUPING SETS syntax; class green 8 selected / 8 passed / 0 failed exit 0 with DOCKER_HOST, SQLite unreachability recorded, no arm dropped | rv1/logs/D3b-postgres-run-2.log, rv1/inputs/rendering-arms.md, rv1/inputs/preparer-arms.md
2026-10-18T10:54:41Z | DO | revision r2 | iteration n 1/3 | total facts 91/112 (r1 76 + r2 15: D1b 8, D2b 7); whole-solution build 0 warnings / 0 errors exit 0; scoping guard IntegrationSuiteScopingTests 2/2 exit 0; D1b and D2b reports validator rc 0 | rv1/logs/D1b-build-2.log, D1b-guard-2.log, rv1/inner/D1b.report.json, rv1/inner/D2b.report.json
2026-10-18T11:00:10Z | CHECK | revision r2 | iteration n 1/3 | r2 boundary preflight: podman socket ping OK; NEXTORM_SKIP_PROVIDERS unset (printenv rc 1); 5 reusable containers listed; no src/tests .cs newer than the Debug test assembly (count 0); protected 6/6 sha256 match vs tree-before.txt; HEAD d1e271d5 (T247 src diff unchanged); tree 24 entries | rv1/results/boundary2-preflight.log
2026-10-18T11:00:20Z | CHECK | revision r2 | iteration n 1/3 | r2 boundary B2: `dotnet build nextorm.slnx -c Debug` exit 0, 25 projects, 0 warnings / 0 errors | rv1/logs/B2-final.log
2026-10-18T11:00:30Z | CHECK | revision r2 | iteration n 1/3 | r2 boundary G2: IntegrationSuiteScopingTests 2 selected / 2 passed / 0 failed / 0 skipped, exit 0 | rv1/logs/G2-final.log
2026-10-18T11:02:10Z | CHECK | revision r2 | iteration n 1/3 | r2 boundary F (ONE, r1 red F preserved and never retried): exit 0 GREEN - Total 4637, Errors 0, Failed 0, Skipped 195, Not Run 0, Time 87.263s (Passed 4442) | rv1/logs/F2.log
2026-10-18T11:02:20Z | CHECK | revision r2 | iteration n 1/3 | skip budget: rc 0, 195 skips / 37 reasons match the pinned budget; constructed ledger 195/37, matches_pinned true | rv1/results/skip-budget2.log, rv1/inputs/skip-ledger.json
2026-10-18T11:02:40Z | CHECK | revision r2 | iteration n 1/3 | r2 boundary T: rc 0, Summary 26924/38381 branch, 46714/59908 line, 5670/8213 methods (baseline 26543/38317, 46354/59713, 5641/8191) | rv1/logs/T2.log, rv1/results/final2-Summary.txt
2026-10-18T11:03:20Z | CHECK | revision r2 | iteration n 1/3 | r2 delta: counted numerator +381, denominator drift +64 branch all #247; T247-excluded covered 26877 (+334 vs 26543), margin +84 vs 26793 (literal +131); raw per-file partition +227 across 19 positive files, 0 losses; protected SqlSourceRenderer +11 marked separately | rv1/results/coverage-delta2.md, rv1/inputs/contributions2.json
2026-10-18T11:07:30Z | CHECK | revision r2 | iteration n 1/3 | D7 docs baseline applied: AGENTS.md and .github/workflows/dotnet.yml carry the measured T247-excluded 78.0 % line / 70.1 % branch (was 77.6 / 69.2); four threshold lines sha256-identical before/after and the threshold-restricted diff has no changed line; gate still RED (70.1 < 75) | rv1/results/docs-baseline.diff, rv1/results/threshold-diff.txt
2026-10-18T11:07:35Z | CHECK | revision r2 | iteration n 1/3 | D7 variant evidence V01..V15 written: each row names its class+facts, exact command, exit 0, log, observed counts/rows; V05 (KeyValueListsEqual), V06 (ExpressionListsEqual), V08 (VisitIndex) recorded deferred with the exact observation and trigger; defects.json has the single environmental CH-CLEANUP-TIMEOUT (filed 0, duplicates 0, fixes 0); deferred.json carries the 15 cycle-5 rows plus 2 new entries (17) | rv1/results/v01.txt..v15.txt, rv1/inputs/defects.json, rv1/inputs/deferred.json
2026-10-18T11:07:40Z | CHECK | revision r2 | iteration n 1/3 | D7 contract rv2: 25 rows R01..R07 + E01..E03 + V01..V15, owner coder / verifier check, evidence paths existing; 22 met, 3 deferred (machine status open with disposition=deferred and the trigger in reason); footprint tests/nextorm.integration.tests + AGENTS.md + .github/workflows/dotnet.yml, base_ref c99cc512 | rv1/inputs/contract.json
2026-10-18T11:07:45Z | CHECK | revision r2 | iteration n 1/3 | D7 evidence manifest generated by pdca_evidence.py generate (rc 0, contract_rv 2, tree f97d9e79, 25 rows) and both gates rc 0: validate_inner_loop.py manifest and pdca_evidence.py lint; no duplicate row id and no superseded generation | rv1/manifest.json, rv1/results/gate-manifest.txt, rv1/results/gate-lint.txt, rv1/generations/
2026-10-18T11:07:50Z | CHECK | revision r2 | iteration n 1/3 | D7 inner-loop validators built from the D1..D5/D1b/D2b reports: journal 41 executions / 82 events rc 0; project rc 0 (mechanical projection saved as rv1/inner/report.json); report + journal projection-completeness rc 0; no diagnostic was suppressed | rv1/inner/journal.json, rv1/inner/report.json, rv1/results/validator-journal.txt, rv1/results/validator-project.txt, rv1/results/validator-report.txt
2026-10-18T11:07:55Z | CHECK | revision r2 | iteration n 1/3 | D7 status finalization: final numbers table (baseline 26543/38317 -> T247-excluded 26877/38317, +334, margin +84; line 46560/59713 = 78.0 %), defect history (CH-CLEANUP-TIMEOUT, 0/0/0), durable state updated to phase CHECK / status OPEN / facts 91 of 112; CHECK only, no ACT | .pdca/status/integration-branch-coverage-6.md
2026-10-18T11:08:00Z | CHECK | revision r2 | iteration n 1/3 | D7 tree preserved: HEAD d1e271d570850b76fcef753c416fb96a6c55921a, 24 status entries, `git diff --name-only -- src/` empty, protected 6/6 sha256 `-c` OK (rc 0), 0 staged paths, no commit and no push | rv1/results/tree-preserved.txt

## ACT

CHECK returned **PASS** on plan revision **r2**, contract revision **rv2**, attempt **n 1/3**
(criteria matrix below: 7/7 met). Verdict pointer: the CHECK block of this file (the r2
progress entries `r2 boundary F`, `r2 boundary T`, `r2 delta`, `D7 ...`), with the frozen
tree under review `f97d9e7972ebaab98dde660b5dbe4c7c5d1e1caf` (generation `50673a46...5c93`)
and the pre-ACT snapshot `rv1/results/tree-preserved.txt`. ACT re-verified the tree, re-froze
the evidence over the changed status document (the status file is inside `.pdca/**`, which is
excluded from the footprint, so the synthetic tree itself is unchanged) and closed the cycle.

### CHECK criteria matrix (7/7 met)

| criterion | verdict |
|-----------|---------|
| REQ-COV | met - T247-excluded **26877/38317 (70.1 %)**, **+334** counted over the pinned 26543, **margin +84** vs the 26793 floor; denominators 38317 / 59713 / 8191 restored exactly after excluding the four #247 src files |
| REQ-LIVE | met - F2 Total **4637** / Passed **4442** / Failed **0** / Skipped **195** / 37 reasons; all five container providers execute under `DOCKER_HOST`; `IntegrationSuiteScopingTests` 2/2 |
| REQ-QUALITY | met - `dotnet build nextorm.slnx -c Debug` exit 0 (25 projects, 0 warnings / 0 errors); CRLF; no suppression; protected six sha256 **6/6 OK** |
| REQ-BOUNDARY | met - exactly ONE r2 acceptance sweep (F2 then T2 with `-classfilters:"-NextORM.Core.InMemory*"`); the r1 red F is preserved as evidence and was never retried |
| REQ-DOCS | met - measured T247-excluded 78.0 % line / 70.1 % branch in `AGENTS.md` and `.github/workflows/dotnet.yml`; thresholds 85/75/65/75 byte-identical (block sha256 576b2ef4) |
| REQ-DEFECTS | met - exactly one key (`CH-CLEANUP-TIMEOUT`, environmental); 0 filed, 0 duplicates, 0 applied fixes; #303/#304/#307/#308/#309 untouched |
| REQ-STATE | met - durable state survived the r1 -> r2 replan; scope sealed before the first test edit; 25-row contract rv2 (22 met / 3 deferred with triggers) |

### Final numbers (ACT-confirmed)

| metric | baseline (pinned) | final T247-excluded | cycle delta |
|--------|-------------------|---------------------|-------------|
| branch covered / total | 26543 / 38317 (69.2 %) | **26877 / 38317 (70.1 %)** | **+334** |
| line covered / total | 46354 / 59713 (77.6 %) | **46560 / 59713 (78.0 %)** | **+206** |
| methods | 5641 / 8191 | 5670 / 8213 literal; denominator **8191** (numerator not re-credited) | +29 literal |
| full integration suite | 4546 / 4351 / 0 / 195 | **4637 total / 4442 passed / 0 failed / 195 skipped / 37 reasons / 87.263 s** | +91 discovered, +91 passed |

- Literal counted T2 is 26924 / 38381; the denominator drift (+64 branches / +195 lines / +22
  methods) is entirely the four concurrent #247 files and is **not credited** to cycle 6.
  Excluding them restores 38317 / 59713 / 8191 **exactly** - the counted denominators are
  unchanged, and no exclusion filter was edited.
- Floor 250 met: 26543 + 250 = 26793; measured 26877 -> **margin +84**. The literal total
  26924 (+131 over target) is not claimed.
- Raw per-file partition **+227 across 19 positive files, 0 negative files**; the protected
  `SqlSourceRenderer` +11 is marked separately (sha256 unchanged).
- Gate status: **still RED** - 70.1 % < `MIN_INTEGRATION_BRANCH_COVERAGE=75`.

### Facts and product edits

- **91 facts of the 112 hard cap** across **8 cycle-authored test classes**:
  `Sqlite/PlanKeyBranchCoverage6Tests` **14** (D1 8 + D1b 6), `Sqlite/PreparerBranchCoverage6Tests`
  **28** (D2 21 + D2b 7), `Coverage6/PostgresRenderingBranchCoverage6Tests` **8** (D3 6 +
  the 2 `GroupingSetsEqual` facts), `Coverage6/SqliteRenderingBranchCoverage6Tests` **6**,
  `Coverage6/SqlServerRenderingBranchCoverage6Tests` **6**, `Coverage6/ClickHouseRenderingBranchCoverage6Tests`
  **6**, `Sqlite/EagerBuilderBranchCoverage6Tests` **14**, `Sqlite/CorrelatedBranchCoverage6Tests`
  **9** = 91. Per-unit attribution: D1 14, D2 28, D3 24 (+2 grouping-set facts charged to the
  D1 allocation), D4 14, D5 9; caps 30 / 28 / 24 / 16 / 14 respected, no fact added in ACT.
- **0 product edits**: cycle 6 touched no `src/` file; the six protected concurrent files verify
  **6/6 OK** against `rv1/inputs/tree-before.txt`; 0 staged paths; **no commit, no push**.

### Defects

`CH-CLEANUP-TIMEOUT` - environmental, pre-existing cycle-3 test
(`ClickHouseDialectBranchCoverage3Tests.PagedSelect_WithOrderBy_ShouldReturnTheRequestedPage`,
HttpClient 120 s timeout in `DropMarker`), observed in the r1 red F, did not repeat in r2;
**not filed** (0 filed / 0 duplicates / 0 applied fixes). #303 / #304 / #307 / #308 / #309
remain open blockers, untouched by this revision.

### Accepted deviations

(a) **r1 was a measured loop-back**, never hidden and never threshold-tuned: the r1 sweep was
red F on the environmental ClickHouse cleanup timeout and its counted total was +240 (shortfall
10 vs 26793). It was reported as such and returned to PLAN; no threshold, denominator or
exclusion was adjusted to make it pass.
(b) **r2 authorized exactly one new acceptance sweep** by an explicit planner revision. That
single sweep is the only one that counts; the r1 F/T pair is retained byte-identically as
historical evidence and was never re-run or re-credited.
(c) **3 contract rows are legitimately deferred with triggers** (open, never presented as
passing): V05 `KeyValueListsEqual` (SQLite-inapplicable: no SETTINGS key-value list; the
`StringListsEqual` half is met), V06 `ExpressionListsEqual` (needs ARRAY JOIN, impossible on
SQLite and not a plan-key differential on ClickHouse; the `GroupingSetsEqual` half is met on
PostgreSQL), V08 `VisitIndex` (publicly unreachable: indexers compile to `String.get_Chars`,
NotSupportedException). Each carries its activation trigger in `rv1/inputs/contract.json`.
(d) **HEAD and the worktree moved during the cycle** (baseline `c99cc512` -> `d1e271d5` at the
r2 boundary -> `6069fac7` and five uncommitted #265 src files during ACT), from concurrent
work. The movement is recorded in `rv1/results/tree-preserved-final.txt` with mtimes, is never
credited to this revision, and does not change the frozen synthetic tree (src/ is outside the
recorded footprint).

### Residual / next plan inputs

- The integration gate is **still RED** at **70.1 %** branch vs `MIN_INTEGRATION_BRANCH_COVERAGE=75`.
  On the unchanged denominator 38317, 75 % needs **28738** covered branches; residual from 26877
  is **1861** counted branches.
- Remaining levers on the same counted model: plan-key comparer batteries on further fields
  (the equal-prefix one-field differential family still pays), `QueryPreparer` prepare/shape
  arms not yet reached, `SqlBuilder` / `SqlSourceRenderer` provider-rendering arms, and
  `EntityBuilder` nested/alias/copy paths.
- Deferred register (`rv1/inputs/deferred.json`, 17 rows) keeps every proven-unreachable arm
  with its trigger: SQLite grouping sets / CUBE, ClickHouse SAMPLE (needs a MergeTree sampling
  key), SQL Server sub-query hints and index-ignore forms, tuple projection (needs #307),
  child-factory / JoinInto folding, ClickHouse legacy `WithRecursive` gating.

### Transferable lessons

- **Window frames are fully reachable through the public API** (`EntityBuilder.Window` +
  `WindowFrame.Rows/Range/Groups`, `Preceding/Following/CurrentRow/Unbounded*`, `WithExclusion`);
  a suspected "no public frame API" target turned into the cheapest cluster of the cycle.
- **The ordered equal-prefix one-field differential family still pays**: resolving two commands
  that differ in exactly one compared component and asserting `Equals` false while both execute
  live added +65 branches on `QueryPlanEqualityComparer` alone.
- **A provider comparer arm must live in a class whose engine can execute the shape**: SQLite
  rejects rendered GROUPING SETS, so the two `GroupingSetsEqual` facts belong to the PostgreSQL
  class; placing them by file convenience would have produced a translation-only assertion.
- **The ClickHouse cleanup timeout is environmental**: it appears only in the full sweep, never
  in isolation, and must be recorded with its exact observation rather than filed as a product
  defect.
- **A deferral is only legitimate with a trigger**: each of the three deferred rows names the
  concrete capability that would activate it, so the next plan can pick them up mechanically.

### Durable state (ACT, canonical)

```yaml
task: integration-branch-coverage-6
cycle: 6
plan_revision: r2
contract_revision: rv2
attempt: n 1/3
phase: ACT closed (EXIT)
status: CLOSED
facts: 91/112
facts_total: 91
facts_cap: 112
check_verdict: PASS
contract_rows: 25
contract_rows_met: 22
contract_rows_deferred: 3
baseline:
  branch_covered: 26543
  branch_total: 38317
  line_covered: 46354
  line_total: 59713
  methods_covered: 5641
  methods_total: 8191
final_t247_excluded:
  branch_covered: 26877
  branch_total: 38317
  branch_pct: 70.1
  line_covered: 46560
  line_total: 59713
  line_pct: 78.0
  methods_total: 8191
  cycle_delta_branches: 334
  cycle_delta_lines: 206
final_literal:
  branch_covered: 26924
  branch_total: 38381
  line_covered: 46714
  line_total: 59908
  methods_covered: 5670
  methods_total: 8213
suite:
  total: 4637
  passed: 4442
  failed: 0
  skipped: 195
  skip_reasons: 37
  time: 87.263s
target: 26793
margin: 84
raw_partition: 227
raw_positive_files: 19
raw_negative_files: 0
gate_integration: RED
residual_to_75pct: 1861
thresholds: [85, 75, 65, 75]
protected_files: 6/6 OK
product_edits: 0
test_files: 8
head_at_act: 6069fac7cda6d38266c9a4493443d2e6bcdf5280
commit: false
push: false
evidence:
  manifest: rv1/manifest.json
  tree_preserved_final: rv1/results/tree-preserved-final.txt
  act_generate: rv1/results/act-generate.txt
  act_lint: rv1/results/act-lint.txt
  act_manifest: rv1/results/act-manifest.txt
defect_history:
  - key: CH-CLEANUP-TIMEOUT
    class: environmental (pre-existing cycle-3 ClickHouse cleanup timeout, HttpClient 120s in DropMarker)
    observed: r1/n1 boundary F
    status: did not repeat in r2/n1 green boundary F
    filed: 0
    duplicates: 0
    fixes_applied: 0
blockers: ["#303", "#304", "#307", "#308", "#309"]
```

2026-10-18T11:17:17Z | ACT | revision r2 | iteration n 1/3 | ACT closed (EXIT): CHECK PASS r2/rv2/n 1/3 (7/7 criteria met) confirmed; final T247-excluded 26877/38317 (70.1 %, +334, margin +84; gate still RED, residual 1861 to 75 %); 91/112 facts across 8 classes; 0 product edits, 6/6 protected, 0 staged, no commit/push; evidence re-frozen over the changed status document (tree unchanged; superseded generation preserved as text per the cycle-4/5 precedent) and both gates re-run | rv1/results/act-generate.txt, rv1/results/act-lint.txt, rv1/results/act-manifest.txt, rv1/results/tree-preserved-final.txt
2026-10-18T11:17:57Z | ACT | revision r2 | iteration n 1/3 | evidence re-freeze: generate run 1 rc 0 -> generation 05813ff0 (tree unchanged f97d9e79), lint run 1 rc 2 with exactly 25 duplicate-row-id diagnostics naming the superseded generation 50673a46 -> its 25 receipts preserved AS TEXT under rv1/inputs/superseded-generation-50673a46/ (manifest.json replaced atomically before preservation - recorded as not recoverable, never invented) and removed -> generate run 2 rc 0 (same generation 05813ff0), lint run 2 rc 0, manifest gate rc 0 (25/25 rows: 22 met / 3 deferred); exactly one generation set remains | rv1/manifest.json, rv1/results/act-generate.txt, rv1/results/act-lint.txt, rv1/results/act-manifest.txt
