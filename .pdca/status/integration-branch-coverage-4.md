# integration-branch-coverage-4

Status: CLOSED
Current cycle: 4
Plan revision: r1
Contract revision: rv1
Attempt: n 1/3
Phase: ACT closed (EXIT)

## Goal

Raise the counted integration-suite branch coverage toward the hard gate `MIN_INTEGRATION_BRANCH_COVERAGE=75`
(thresholds unchanged) with tests that execute against REAL databases only, and with every fact anchored to a
RECON-verified public entry point. This cycle's floor: **+250 counted branches** (covered-branch delta between
the pinned baseline and the post-sweep `tests/coverage/integration-report/Summary.txt`). Baseline:
branches **25807/38281 = 67.4 %**, lines **45738/59676 = 76.6 %**, residual to 75 % = **2904** branch points;
both denominators (38281 branches / 59676 lines) must stay unchanged.

## Baseline (pinned, measured)

| metric | value | note |
| --- | --- | --- |
| branch covered/total | 25807/38281 (67.4 %) | residual to 75 % = 2904 |
| line covered/total | 45738/59676 (76.6 %) | denominator 59676 |
| method covered/total | 5590/8189 (68.2 %) | fully covered 4084/8189 (49.8 %) |
| full suite (green F3 of cycle 3) | 4400 passed / 0 failed / 195 skipped / 37 skip reasons | Errors 0 / Not Run 0 / Time 57.021s |
| source of the numbers | `tests/coverage/integration-report/Summary.txt` (pinned T model) | copied byte-identical to `rv1/inputs/baseline/Summary.txt` |
| raw cobetura copy | `rv1/inputs/baseline/integration.cobertura.xml` | **TARGETING-ONLY** - its own line/branch model is never a counted gain |

The raw cobetura model is used only to point at uncovered branches (targeting). Every counted claim of this
cycle comes from the reportgenerator model (`T`), never from the raw XML.

## Acceptance criteria

| id | requirement | negative case (must NOT satisfy it) |
| --- | --- | --- |
| REQ-COV | counted covered branches **>= 26057/38281** (baseline 25807 + floor 250) in `Summary.txt` after exactly ONE green boundary sweep F followed by T, with both denominators unchanged (38281 / 59676) | a gain produced by an exclusion filter, a denominator edit, or by the raw cobetura model; a partial/red sweep presented as green |
| REQ-LIVE | every new fact executes against a real database (in-process SQLite file DB or a Testcontainers provider); no `InMemoryDataContext` in the integration project; class-owned marker schema; FluentAssertions only; guard `IntegrationSuiteScopingTests` green; 195-skip / 37-reason pin unchanged | translation-only assertions, vacuous facts (always-empty result sets), unexpected skips, a fact that needs no database |
| REQ-QUALITY | whole-solution build `B` = 0 warnings / 0 errors (`TreatWarningsAsErrors`), every planned test file compiles, no new suppression/`NoWarn` added | bypassing the warning gate, adding `NoWarn`, or editing production code to make a fact pass |
| REQ-BOUNDARY | exactly ONE boundary sweep `F` (`DOCKER_HOST` set, `NEXTORM_SKIP_PROVIDERS` unset, fail-closed over all container providers) then `T` with `-classfilters:"-NextORM.Core.InMemory*"`; skip-budget gate rc 0 with the pinned 195/37 multiset; the inner loop stays inside the frozen scope (affected projects only, exact `-class` selectors) | a second sweep without a PLAN decision, any provider reported `skipped`, or an inner run widening the scope to the whole project/solution |
| REQ-DOCS | only `AGENTS.md:75` and `.github/workflows/dotnet.yml:29-32` synchronized to the newly measured numbers (numbers only) | any threshold change (85/75 whole solution, 65/75 integration), any `docs/**` or `docs/ru/**` edit, any public-page claim added |
| REQ-DEFECTS | every observed defect gets a stable defect key recorded in the defect history with the number of applied fixes, evidence pointers, last repetition and escalation outcome; a red F is a DO loop-back and no acceptance claim is made for it | silently dropping a failing fact, lowering an expectation, or retrying a red sweep without recording it |
| REQ-STATE | durable state (`Current cycle 4`, `Plan revision r1`, `Contract revision rv1`, `Attempt n 1/3`) and the defect history survive a session/`task_id` reset and are read from this status file, never reconstructed from a dispatch's memory | state resurrected from a dispatch, `r`/`n` reset by a new dispatch, or a defect history wiped on reset |

## Minimal solution

Tests only + two baseline numbers:

1. **11 new integration test classes** (all names below are **PLANNED**, nothing here exists yet) executing live
   public paths whose branches are uncovered in the pinned baseline (see `rv1/inputs/branch-ledger.md`).
2. **Two baseline descriptions** (`AGENTS.md:75`, `.github/workflows/dotnet.yml:29-32`) updated with the newly
   measured numbers only - thresholds untouched.

| alternative | why rejected |
| --- | --- |
| lower `MIN_INTEGRATION_BRANCH_COVERAGE` to make the gate green | forbidden by AGENTS.md; the gate stays red until the coverage is real |
| broaden an exclusion filter (`-classfilters`) to shrink the denominator | denominator must stay 38281/59676; excluded paths are unit-only by policy |
| production testability refactor (public setter for private-only seams) | out of scope for a coverage cycle; unreachable candidates are deferred with an activation trigger instead |
| unit-test the same arms in `tests/nextorm.core.tests` with the in-memory provider | the integration gate measures the integration suite only; unit tests would raise the wrong denominator |
| reflection/private-state manipulation to reach private arms | banned: only public paths, otherwise the candidate is deferred |

## Forecast

| cluster | content | forecast (counted branches) |
| --- | --- | --- |
| D1 | QueryPreparer `RawShape` comparison arms + CteShape pairing + WindowsEqual/`*Equal` edge arms + RefreshInValuesShape + IsUnrelatedCastOfProjectionItem + CollectBound/PendingCrossJoinFilters + ScanUnkeyedTuple/Scalar InFrom/InValues + AttachPrimarySourceLock + PreparePreWhere (ClickHouse) | +160..220 |
| D2 | `QueryPlanEqualityComparer` (StringListsEqual/WindowsEqual frame/KeyValueListsEqual/GroupingSetsEqual/ExpressionListsEqual/CteDefinitionsEqual/GetHashCodeCore) + `ExpressionPlanEqualityComparer` (constant/closure/member, null, char/string, convert/unary, arrays/lists, nested lambdas, parameter identity) | +75..115 |
| D3 | `SqlBuilder.MakeSelect` dispatch arms + `SqlSourceRenderer` MakeFrom/MakeJoin/MakeApplyJoin/MakeRawSqlSource/MakeTableExpression/MakeTableFunction | +40..65 |
| D4 | `EntityBuilderEagerLoading` single-query fold: `IEagerLoadSpec<TEntity>.PlanSingleQuery` (:154 declaration, :338-region implementation) + Execute/ExecuteLoadedChildren + per-parent guards | +30..50 |
| D5 | `CorrelatedQueryExpressionVisitor` + `AggregateTerminalRewriter` | +15..30 |
| **total** | | **+320..480**, midpoint **+400**, floor **+250** |

The forecast is a range over the ledger's target arms, not a measurement. Anything the sweep does not confirm
is reported as measured, never as forecast: only the delta in `Summary.txt` counts (REQ-COV, REQ-LIVE).

## Tasks (round r1)

Method for every cluster: a **battery** of independent facts over class-owned marker tables **plus** a
**differential** A->A->B->B->A on ONE context (same command shape, property changed between A and B), so both
the hit and the miss arm of every compared field are executed on the same context; every guard row is paired
with a live counterpart of the same shape.

| id | owner | planned files / classes | target methods / lines | provider routing | overlap control (named neighbour facts that must NOT be repeated) | forecast |
| --- | --- | --- | --- | --- | --- | --- |
| D0 | scout + coder (**done in this dispatch**) | `rv1/inputs/branch-ledger.md`, `rv1/results/tree-before*.txt`, `rv1/results/cycle3-untracked.sha256`, `rv1/inputs/scope.json`, `rv1/inputs/contract.json` | Roslyn public-path proof for `RawShape.LambdaArraysEqual` (:3791) and `QueryPreparer.PreparePreWhere` (:3353); uncovered-branch ledger from `rv1/inputs/baseline/` + `/tmp/nextorm/c3/uncovered-{files,classes}.txt` | none (no execution) | records the existing tests' facts so D1..D5 can name a different arm per row | 0 |
| D1 | coder | `tests/nextorm.integration.tests/Sqlite/PreparerBatteryBranchCoverage4Tests.cs`, `Sqlite/PreparationDifferentialBranchCoverage4Tests.cs`, `SqlServer/SqlServerPreparationDifferentialBranchCoverage4Tests.cs`, `Postgres/PostgresPreparationDifferentialBranchCoverage4Tests.cs`, `ClickHouse/ClickHousePreparationDifferentialBranchCoverage4Tests.cs` (**all planned**) | `QueryCommand.QueryPreparer.cs`: `Matches` :3707-3760, `LambdaArraysEqual` :3791, `CtesEqual` :3766, `WindowsEqual` :3803, `NamingConventionsEqual` :3836, `FilterScopeEqual` :3839, `StringListsEqual` :3842, `SettingsEqual` :3854, `GroupingSetsEqual` :3867, `RefreshInValuesShape` :195-280, `AttachPrimarySourceLock` :316, `IsLockablePhysicalSource` :357, `IsUnrelatedCastOfProjectionItem` :1792, `CollectBoundCrossJoinFilters` :2278, `AppendPendingCrossJoinFilters` :2305, `InjectBoundSourceFilters` :2168, `ScanUnkeyedTupleInValues` :2838/:2852, `ScanUnkeyedScalarInValues` :3083/:3097, `ScanUnkeyedTupleInFrom` :3006, `ScanUnkeyedScalarInFrom` :3262, `HasUnkeyedScalarInCondition` :3251, `PreparePreWhere` :3353, `PrepareLimitBy` :3395, `PrepareDistinctOn` :3413 | SQLite in-process (core arms, inner runs); SQL Server / Postgres / ClickHouse only in the single boundary sweep F | `Sqlite/RawShapeBranchCoverage2Tests.cs:59,190,215,262,297,322,358,386,426,454,492`; `Sqlite/PreparationBranchCoverage3Tests.cs:114,151,187,262,300,333,367,419,471,499,526,563`; `Sqlite/SqlitePlanCacheShapeCoverageTests.cs:435`; `ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs:322` (LimitBy happy path) - each ledger row names a DIFFERENT arm than these | +160..220 |
| D2 | coder | `tests/nextorm.integration.tests/Sqlite/PlanEqualityDifferentialBranchCoverage4Tests.cs` (**planned**) | `QueryPlanEqualityComparer.cs`: `StringListsEqual` :234, `WindowsEqual` :248 (frame arm :274, 36 uncovered points), `KeyValueListsEqual` :286, `ExpressionListsEqual` :301, `GroupingSetsEqual` :315, `CteDefinitionsEqual` :336, `GetHashCodeCore` :390; `ExpressionPlanEqualityComparer.cs`: `ExpressionComparer` :280 arms :171/:339/:354/:371/:381/:385/:403/:413/:440/:475/:481, `QueryCommandKey` :702 (:643-696), `Visitor` :742 (:983/:985) | SQLite in-process | `Sqlite/PlanEqualityBranchCoverage2Tests.cs:39,72,121,147,189,213,235,257,281,305,326,346,374,396,424,450,486,521`; `Sqlite/PlanKeyBranchCoverage3Tests.cs:56,102,157,227`; `Sqlite/RawShapeBranchCoverage2Tests.cs:190` - the ledger targets the plan-KEY comparers and the arms those files do not touch (the 36-point frame cluster is on the comparer side, not on the `RawShape` side already covered at `PreparationBranchCoverage3Tests.cs:419`) | +75..115 |
| D3 | coder | `tests/nextorm.integration.tests/Sqlite/SourceRenderingDifferentialBranchCoverage4Tests.cs`, `SqlServer/SqlServerSourceRenderingBranchCoverage4Tests.cs`, `Postgres/PostgresSourceRenderingBranchCoverage4Tests.cs` (**all planned**) | `DataContext/SqlBuilder.cs`: `MakeSelect` :35 (dispatch arms :136/:151/:153/:156/:167/:170/:176/:178/:184/:186), `MakePerParentPagingSelect` :663, `MakeExtremeRowSelect` :941/:981/:995/:1074/:1110/:1267; `DataContext/SqlSourceRenderer.cs`: `MakeFrom` :424/:432, `MakeJoin` :186, `MakeJoinParts` :293, `MakeApplyJoin` :397, `MakePlainTableApply` :416, `MakeRawSqlSource` :656, `MakeTableExpression` :711, `MakeTableFunction` :753, `MakeXmlNodes` :915, `MakePivot` :993 | SQLite in-process for the portable arms; SQL Server (`.Pivot`/`.Unpivot`, `SqlServerEntityBuilderExtensions.cs:31/60`) and Postgres (`DistinctOn`, `PostgresEntityBuilderExtensions.cs:23`) only in the boundary sweep | `Sqlite/SourceRenderingBranchCoverage2Tests.cs:78,104,128,157,174,194,210,232,252,270,298,322,350,372`; `Sqlite/ExtremaEagerBranchCoverage3Tests.cs:109,131,162,184,219,262,304,340`; c3 status notes on `SqlBuilder.MakeSelect` :35-660 (`PostgresDialectBranchCoverage3Tests`) - the ledger targets dispatch arms those files never execute (per-parent paging outer, extreme-row native/portable, table function/xml/pivot) | +40..65 |
| D4 | coder | `tests/nextorm.integration.tests/Sqlite/EagerGraphBranchCoverage4Tests.cs` (**planned**) | `Builders/EntityBuilderEagerLoading.cs`: `IEagerLoadSpec<TEntity>.PlanSingleQuery` :154 (implementation `EagerLoadSpec<TEntity,TChild,TKey>` :223, arms :373/:395), `Execute` :893, `ExecuteAsync` :934, `ExecuteLoadedChildren` :918/:952, `HasPerParentTopN` :974, `PerParentTopNNotSupported` :982, `ValidatePerParentPagingChild` :1000, `ValidateLoadWithForJoinInto` :1051, `IEagerLoadNodeSpec.SubtreeItemCount` :52, `ResolveChildNodeSource` :42 | SQLite in-process | `Sqlite/EagerModeBranchCoverage2Tests.cs:155,181,220,248,267,294,321,359,388,417,454,541`; `Sqlite/ExtremaEagerBranchCoverage3Tests.cs:219,262,304,340`; `CommonTestSuite.EagerLoading` - the ledger targets the single-query FOLD internals (stitch columns, per-parent rank source, guard arms) that those files only exercise through split/auto mode | +30..50 |
| D5 | coder | `tests/nextorm.integration.tests/Sqlite/CorrelatedAggregateBranchCoverage4Tests.cs` (**planned**) | `Visitors/CorrelatedQueryExpressionVisitor.cs`: `VisitMethodCall` :168, `RewriteOuterReference` :90, `VisitMember` :445, `TryLowerCollectionTerminalCall` :481, `LowerAdapterTerminal` :524, `LowerCollectionTerminal` :544, `LowerCollectionCount` :578, `BuildCollectionCorrelation` :703, `BuildReferencePrefixKey` :796, `BuildJunctionChildExistence` :842, `EnsureRegisteredSource` :669/:683, uncovered clusters :193/:529/:674-685/:736/:802-821/:875/:1000-1048/:1510/:1607-1622; `Visitors/AggregateTerminalRewriter.cs`: `Rewrite` :22 (partial :81), `UnwrapLambda` :49, `AggregateMethodFor` :56, `SelectMethodFor` :69 | SQLite in-process | `Sqlite/CorrelatedBranchCoverage2Tests.cs:16,111,132,154,170,186,202,219`; `Sqlite/CorrelatedSubquerySweepTests.cs:13`; `Sqlite/PreparerBatteryBranchCoverage3Tests.cs` set-operation battery - the ledger targets terminals/navigations those files never combine (wide vs narrow count, adapter vs collection terminal, junction child existence) | +15..30 |
| D6 | coder | `AGENTS.md` (:75), `.github/workflows/dotnet.yml` (:29-32) - numbers only | the two measured numbers 67.4 % branch / 76.6 % line (and only if the F/T measurement confirms them) | none (no execution) | thresholds 85/75 and 65/75 untouched; no `docs/**` edit | 0 |
| D7 | coder | boundary reporting: `B`, `G`, the single `F`, `T`, skip-budget gate, counted delta + per-file attribution, then `contract.json` rows `met`/`open`, `manifest.json` freeze, validators | `B` whole solution; `G` guard; ONE `F` sweep; `T` reportgenerator | all container providers in F only | no new facts authored here; no threshold/production change | 0 |

## Что не покрываем сейчас (deferred register of this cycle)

| candidate | uncovered weight in the pinned baseline | why deferred | activation trigger |
| --- | --- | --- | --- |
| `QueryPreparer.PreparePreWhere` InValues arms (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:3353`, arms :3363/:3367-3377) | 12 raw branch points | the core `EntityBuilder` has no public `PreWhere` setter (`PreWhereCondition` is `internal`, `Builders/EntityBuilder.cs:256`); only the ClickHouse extension sets it. D0's Roslyn check refines this: the public path exists **through the provider extension**, and the ledger carries it as a ClickHouse scenario row | proven public `JoinInto` rewrite path (core-side), plus the ClickHouse row already added by D0 |
| `RawShape.LambdaArraysEqual` (`...QueryPreparer.cs:3791`, arms :3794/:3795/:3797) | 8 raw branch points | previously deferred as private-only | **trigger fired**: D0 proved the public path `.ArrayJoin`/`.LeftArrayJoin` (`src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs:61/65`) -> `RawShape.ArrayJoins` -> `QueryPlanStore.TryGetRaw` (`src/nextorm.core/DataContext/Cache/QueryPlanStore.cs:133`) -> `Matches:3749` -> `LambdaArraysEqual:3791` -> D1 row `D1-19` |
| `Builders/EntityBuilder.cs` guard/validation arms | 365 raw points | the public guard/validation throw sites were already swept by cycle 3 (`BuilderGuardBranchCoverage3Tests`, 14 facts, `EntityBuilder.cs` :4142/:947/:1045/:965/:988/:1073/:1082/:3622/:3122/:3675/:3300/:3290/:3653/:3715) | a new public guard introduced by production work |
| provider-function batteries (ClickHouse/SQL Server/MySQL/MariaDB scalar functions) | e.g. `SqlServerSpecificFunctions` 252, `ClickHouseScalarFunctions` 287 raw | saturated lever: cycle 3 added +41 discovered provider items (4334 -> 4375) with **no** measurable counted gain; the counted attribution went to core (+369) vs sqlserver (+15) / clickhouse (+10) | a provider function change or a new public overload surface |
| private reflection / production hooks | - | out of scope by policy: only public paths may be exercised; no reflection into private state, no production testableness refactor | a public surface is added by production work |

## Test strategy

- **Inner loop = affected filtered subset only.** `G` (`dotnet test tests/nextorm.core.tests --filter
  FullyQualifiedName~IntegrationSuiteScopingTests`) runs before the first cluster edit; then, per cluster, one
  affected-project build and `I(C)` = an exact-class run:
  `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse dotnet run --project
  tests/nextorm.integration.tests -c Debug --no-build -- -noColor -class <full class name>`.
  Nothing else: never the whole project, never the whole solution, never a container provider inside DO.
- **Provider classes** (SqlServer/Postgres/ClickHouse preparation + SqlServer/Postgres source rendering + any
  container fact) run inside DO only as an exact-class run **with** `DOCKER_HOST` set and
  `NEXTORM_SKIP_PROVIDERS` unset; `class` stays exact so no run widens the selection.
- **Boundary = exactly ONE** `F` at DO->CHECK on a frozen tree, then `T`; then the skip-budget gate.
- Every row of the ledger maps to a named uncovered method/line; assertions are FluentAssertions only; all
  probes are scoped to class-owned marker rows (never global seed tables); every guard fact is paired with a
  live counterpart of the same shape.
- Unreachability is proven by public-path analysis (Roslyn) + a numeric list of unreachable baseline points,
  never by a reflection probe.

### Variant matrix (each row closes as test / guard / deferred-with-trigger)

| variant | meaning | close as |
| --- | --- | --- |
| V01 | SQLite in-process, plan-cache hit (A->A) reuse of an equivalent command | test |
| V02 | SQLite in-process, plan-cache miss (A->B) re-preparation + live read | test |
| V03 | SQLite in-process differential A->A->B->B->A on ONE context | test |
| V04 | live SQL Server differential A->A->B->B->A (boundary sweep) | test |
| V05 | live Postgres differential A->A->B->B->A (boundary sweep) | test |
| V06 | live ClickHouse differential A->A->B->B->A (boundary sweep) | test |
| V07 | null / empty-collection / empty-string edge paired with a live counterpart | test + guard |
| V08 | changed-collection edge (single vs multiple elements, length mismatch) | test |
| V09 | rejection arm: exact exception type + message fragment, paired with a live counterpart | guard |
| V10 | Roslyn-proven unreachable public path -> no fact | deferred-with-trigger |
| V11 | uncached preparation (`storeInCache: false` / `_dontCache`) path | test |
| V12 | reference identity / self / null short-circuit arm | test |

## Unit execution mode

Sequential, **one tree** (no worktrees): the clusters share `tests/nextorm.integration.tests` (one project,
one set of fixtures and marker helpers), the coverage outputs under `tests/coverage/**`, the two
baseline-description files, and a working tree that already carries 100 entries of user work (62 modified /
11 deleted / 27 untracked). Parallel worktrees would fork those shared files and the coverage artifacts, and
would risk the preserved entries - the argument is recorded here rather than implied.

## Design checklist

KISS; public behaviour only; no production change; measure-never-assert; FluentAssertions only; CRLF for
everything outside `.pdca/**`; CPM untouched; Roslyn for every C# symbol question and ignore-aware search for
text (never `grep -r`/`find`); `TreatWarningsAsErrors` build discipline; nothing leaves milestone `1.0.9-rc4`;
an unprovable criterion is a GAP, not `ready`.

## Docs plan

D6 only: `AGENTS.md:75` and `.github/workflows/dotnet.yml:29-32`, numbers only. No public API/behaviour
change; `docs/**` and `docs/ru/**` untouched; thresholds untouched.

## Perf decision

Not needed: the cycle changes tests only; production execution paths are exercised but unchanged, so no
benchmark and no perf claim. Coverage duration is operational evidence, not a performance claim.

## Recon decision

Bounded D0 ledger (this dispatch): the uncovered-branch ledger is derived from the pinned baseline artifacts
plus the cycle-3 line/class extracts, the public entry points are proven with Roslyn, and the existing tests'
facts are recorded so each row names a different arm. No production testability refactor, no new recon round.

## Pinned commands

| id | command |
| --- | --- |
| B | `dotnet build nextorm.slnx -c Debug` |
| G | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~IntegrationSuiteScopingTests` |
| I(C) SQLite-class | `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -class <full class>` |
| I(C) provider-class | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -class <full class>` (no `NEXTORM_SKIP_PROVIDERS`) |
| F | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o tests/coverage/integration.cobertura.xml "dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -notrait Category=Diagnostic"` |
| T | `dotnet tool run reportgenerator -reports:tests/coverage/integration.cobertura.xml -targetdir:tests/coverage/integration-report -reporttypes:"Html;TextSummary" -classfilters:"-NextORM.Core.InMemory*" -riskhotspotassemblyfilters:"+nextorm.*"` |

`B` runs at phase `boundary` (journalled as such); `I(C)` is kind `other`; `G` is kind `unit`.

## Evidence contract (rows)

| row | subject | owner | applicability | evidence |
| --- | --- | --- | --- | --- |
| R01 | REQ-COV: counted branches >= 26057/38281 after ONE green `F` + `T`, denominators unchanged | coder | always | `rv1/results/F.log` (exit 0), `rv1/results/T.log`, `tests/coverage/integration-report/Summary.txt` |
| R02 | REQ-COV: measured baseline + post-sweep totals + per-file attribution summing to the counted delta, 0 files/classes losing branches | coder | always | `rv1/inputs/baseline/baseline-summary.txt`, `rv1/inputs/contributions.json`, `rv1/results/coverage-delta.md` |
| R03 | REQ-LIVE: every new fact live (no `InMemoryDataContext`), FluentAssertions only, class-owned markers, guard green, skip budget unchanged | coder | always | per-cluster `rv1/results/d*-inner.log`, `rv1/results/F.log`, `rv1/inputs/branch-ledger.md`, `rv1/results/skip-budget.log` |
| R04 | REQ-QUALITY / REQ-BOUNDARY: `B` 0 warnings / 0 errors; `G` green; exactly one `F`; inner scope respected | coder | always | `rv1/results/b*.log`, `rv1/results/g*.log`, `rv1/results/F.log`, `rv1/journal.json` |
| R05 | REQ-DOCS: only `AGENTS.md:75` + `.github/workflows/dotnet.yml:29-32` synced; thresholds untouched; tree preserved (100 pre-existing entries); CRLF; no commit/push | coder | always | `rv1/results/agents-baseline.diff`, `rv1/results/workflow-baseline.diff`, `rv1/results/tree-before.txt`, `rv1/results/tree-preserved.txt` |
| R06 | evidence pipeline: brief/journal/manifest/generate/lint rc 0; `project`/`report` rc 2 only for the accepted reason `FAIL: multiple boundary solution builds: N` | coder | always | `rv1/results/validator-*.txt`, `rv1/results/generate.log`, `rv1/results/lint.log`, `rv1/manifest.json` |
| R07 | REQ-DEFECTS: defect keys with applied-fix counts, evidence pointers and last repetition; red `F` = DO loop-back | coder | if a defect is observed (else `na` with the predicate) | `rv1/inputs/defects.json`, `rv1/results/F-failures.txt`, `rv1/inputs/deferred.json` |
| V01 | D1 inner evidence: Preparer battery + differentials live, green | coder | always | `rv1/results/d1-inner.log` (+ `rv1/results/d1-provider-inner.log` if container classes run) |
| V02 | D2 inner evidence: plan-equality differential green | coder | always | `rv1/results/d2-inner.log` |
| V03 | D3 inner evidence: source-rendering differential green | coder | always | `rv1/results/d3-inner.log` |
| V04 | D4 inner evidence: eager single-query fold green | coder | always | `rv1/results/d4-inner.log` |
| V05 | D5 inner evidence: correlated aggregate terminal green | coder | always | `rv1/results/d5-inner.log` |
| E01 | scope sealed before any edit: `validate_inner_loop.py brief` rc 0 | coder | always | `rv1/inputs/scope.json`, `rv1/results/validator-brief.txt` |
| E02 | execution capture: journal + report valid; `project` rc 2 only for the accepted reason | coder | always | `rv1/journal.json`, `rv1/results/validator-journal.txt`, `rv1/results/validator-project.txt`, `rv1/results/validator-report.txt`, `rv1/inputs/report.json` |
| E03 | frozen evidence pack: `generate` + `lint` + `manifest` validators rc 0, every `met` row pointing at an existing file | coder | always | `rv1/manifest.json`, `rv1/results/generate.log`, `rv1/results/lint.log`, `rv1/results/validator-manifest.txt` |

Frozen manifest path: `.pdca/integration-branch-coverage-4/rv1/manifest.json` (written only at the DO->CHECK
boundary, never in D0). CHECK re-gather: owner `check`, budget **2 batches**, scoped read of the cycle's own
evidence artifacts only, **no new B/F/T** run and no tree change.

## Risks

- **Denominator drift**: any scope/filter edit could move 38281/59676 - the sweep's Summary is compared field
  by field against the pinned baseline copy before any delta is claimed.
- **Container preflight for `F`**: live Podman socket, `NEXTORM_SKIP_PROVIDERS` unset, five container
  providers fail-closed + in-process SQLite; a provider reported `skipped` is never a green run.
- **Baseline artifact overwrite**: `tests/coverage/integration.cobertura.xml` is overwritten by `F`; the
  pre-sweep copy and its sha256 are frozen in `rv1/inputs/baseline/` (STEP 2 of D0).
- **Preserved working tree**: 100 pre-existing entries (62 modified / 11 deleted / 27 untracked) must survive;
  no commit, no push, no revert; the cycle-3 classes stay untracked and byte-identical (sha256 recorded in
  `rv1/results/cycle3-untracked.sha256`).
- **Forecast is not a measurement**: if `F`'s counted delta lands below +250, that is a CHECK verdict on this
  revision (`r1`, `n 1/3`), not a reason to retune the threshold.

## Durable state

```yaml
cycle: 4
plan_revision: r1
contract_revision: rv1
attempt: n 1/3
unit: integration-branch-coverage-4
baseline:
  branches_covered: 25807
  branches_total: 38281
  branch_pct: 67.4
  lines_covered: 45738
  lines_total: 59676
  line_pct: 76.6
  methods_covered: 5590
  methods_total: 8189
  full_methods_covered: 4084
  residual_branch_points_to_75: 2904
  suite: 4400 passed / 0 failed / 195 skipped / 37 skip reasons
floor_counted_branches: 250
floor_target: "26057/38281"
forecast_total: "320..480"
forecast_midpoint: 400
green_boundary_budget: 1
green_boundary_runs: 1
commit: false
push: false
```

## Progress log

TIMESTAMP | EVENT | revision r1 | iteration n 1/3 | text | artifact
 | PLAN | revision r1 | iteration n 1/3 | plan r1 recorded (D0..D7; forecast D1 +160..220 / D2 +75..115 / D3 +40..65 / D4 +30..50 / D5 +15..30, total +320..480, midpoint +400, floor +250 counted branches >= 26057/38281; baseline 25807/38281 = 67.4 ranches, 45738/59676 = 76.6  0nes, residual 2904, 4400 passed / 0 failed / 195 skipped / 37 skip reasons); acceptance criteria REQ-COV/REQ-LIVE/REQ-QUALITY/REQ-BOUNDARY/REQ-DOCS/REQ-DEFECTS/REQ-STATE with their negative cases; variant matrix V01..V12; reconciled with the planner PROPOSAL - the proposal's D0..D7 naming is dropped in favour of D0 bootstrap/D1..D5 clusters/D6 docs/D7 boundary; reconciled with the reviewer DISSENT where it does not conflict (forecast kept as a range, no threshold change, no reflection, one tree) | .pdca/status/integration-branch-coverage-4.md
 | D0 | revision r1 | iteration n 1/3 | D0 bootstrap: evidence dirs .pdca/integration-branch-coverage-4/rv1/{inputs,results,generations}; pre-sweep baseline copied with cp -p (never moved) to rv1/inputs/baseline/{integration.cobertura.xml,Summary.txt} - cobertura sha256 a1db019e21514fd611a6379b0c0c7a560ae30ab86d0a001a4f50da1e9724ab5a, Summary sha256 d4d0f17816efdb4b5f844f4112e12d94397bf21f50678803b41c77bb83c1b258, both matching the live originals (rv1/inputs/baseline/sha256.txt); pinned measured baseline restated verbatim in rv1/inputs/baseline/baseline-summary.txt (branches 25807/38281, lines 45738/59676, methods 5590/8189, 4400 passed / 0 failed / 195 skipped / 37 reasons) with the raw-cobetura-is-targeting-only caveat; tree inventory rv1/results/tree-before.txt + tree-before-status.txt (HEAD 1b140d23cd12ecc19af4bfbad3563b6e8281aa63, 100 status entries = 62 modified / 11 deleted / 27 untracked) and rv1/results/cycle3-untracked.sha256 (all 22 untracked .cs under tests/nextorm.integration.tests, the 10 cycle-3 deliverables flagged cycle3=yes); nothing outside .pdca/** touched, no commit, no push | .pdca/integration-branch-coverage-4/rv1/results/tree-before.txt, rv1/inputs/baseline/sha256.txt
 | D0 | revision r1 | iteration n 1/3 | D0 branch ledger written (rv1/inputs/branch-ledger.md, 51 scenario rows: D1 27 / D2 8 / D3 9 / D4 3 / D5 4), each row naming provider + planned class + public API path (file:line) + target uncovered branch (file:line, from the cobetura copy parsed with python ElementTree) + the named neighbour fact that must NOT be repeated + a distinct input transition + variant id V01..V12; cluster/class map links D1..D5 to the eleven planned classes; deferred register restates the 5 not-covered-now candidates (PreparePreWhere, LambdaArraysEqual, EntityBuilder.cs 365 raw, provider-function saturation, private reflection) with activation triggers | .pdca/integration-branch-coverage-4/rv1/inputs/branch-ledger.md
 | D0 | revision r1 | iteration n 1/3 | D0 STEP 5 Roslyn verdicts (batched Promise.all in ONE execute; declaring types resolved): (a) RawShape.LambdaArraysEqual src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:3791 has exactly 1 call site (Matches:3749), Matches is reached from QueryPlanStore.TryGetRaw:133/SetRaw:171 <- QueryPlanner.cs:635, and RawShape.ArrayJoins is settable only by the PUBLIC ClickHouse extensions .ArrayJoin/.LeftArrayJoin (src/nextorm.clickhouse/Extensions/ClickHouseEntityBuilderExtensions.cs:61/:65) -> PUBLIC PATH FOUND -> scenario D1-19 added (V06); (b) QueryPreparer.PreparePreWhere :3353 has exactly 1 call site (Prepare:104, unconditional) and _preWhere is written only through EntityBuilder.PreWhereCondition (src/nextorm.core/Builders/EntityBuilder.cs:256, internal; read at :3422; assigned at QueryCommand.cs:504) whose only public writer is the ClickHouse extension .PreWhere (ClickHouseEntityBuilderExtensions.cs:42) -> PUBLIC PATH FOUND but provider-specific -> scenario D1-27 added (V06), while the CORE/SQLite side keeps NO public setter and the planner deferral stands there (trigger: proven public JoinInto rewrite path) | .pdca/integration-branch-coverage-4/rv1/inputs/branch-ledger.md
 | D0 | revision r1 | iteration n 1/3 | D0 scope sealed + contract seeded: rv1/inputs/scope.json (projects tests/nextorm.integration.tests + tests/nextorm.core.tests; 12 selectors = the 11 exact planned -class selectors emitted by the reviewer recipe + FullyQualifiedName~IntegrationSuiteScopingTests; 13 files = the 11 planned new test files + AGENTS.md + .github/workflows/dotnet.yml; rebuild affected; boundary = the single F; integration_projects [tests/nextorm.integration.tests]; integration_boundary_runs 1) - validate_inner_loop.py brief rc 0 FIRST TRY, stdout recorded in rv1/results/validator-brief.txt; rv1/inputs/contract.json seeded with 15 open rows R01..R07/V01..V05/E01..E03 (status open, reason "planned: evidence produced in DO"), footprint [tests/nextorm.integration.tests, AGENTS.md, .github/workflows/dotnet.yml], exclusions [.pdca/**], contract_rv 1, base_ref 1b140d23cd12ecc19af4bfbad3563b6e8281aa63; NO manifest generated (boundary step); recorded DEVIATION: the seed keeps contract.issue = null exactly as instructed, but pdca_evidence.py generate requires a non-empty string for issue (cycle 3 used "milestone:1.0.9-rc4"), so the boundary dispatch must set it before generate/lint - flagged in the contract as issue_note, never corrected silently | .pdca/integration-branch-coverage-4/rv1/inputs/scope.json, rv1/inputs/contract.json, rv1/results/validator-brief.txt
2026-10-18T02:34:30Z | D0 | revision r1 | iteration n 1/3 | D0 tree accounting correction (documented, not rounded): the raw snapshot rv1/results/tree-before.txt was captured after this dispatch created its own evidence dir, so it reads 101 entries (62 modified / 11 deleted / 28 untracked); excluding this cycle own artifacts (git status --short | grep -v integration-branch-coverage-4) the PRE-EXISTING state is exactly 100 (62/11/27) as the brief states, and the single delta is ?? .pdca/integration-branch-coverage-4/ (this dispatch own evidence dir; the new status file lives inside the already-untracked .pdca/status/ so it adds no separate entry); no pre-existing entry changed status code, none removed, HEAD 1b140d23cd12ecc19af4bfbad3563b6e8281aa63 unchanged, no commit and no push | .pdca/integration-branch-coverage-4/rv1/results/tree-before.txt, rv1/results/tree-before-status.txt
2026-10-18T03:04:45Z | D1 | revision r1 | iteration n 1/3 | D1 authoring closed: 5 classes / 24 facts authored on the frozen tree - PreparerBatteryBranchCoverage4Tests (11 facts), PreparationDifferentialBranchCoverage4Tests (5), SqlServerPreparationDifferentialBranchCoverage4Tests (3), PostgresPreparationDifferentialBranchCoverage4Tests (2), ClickHousePreparationDifferentialBranchCoverage4Tests (3); class-owned marker tables (pb4_*, pd4_*, ss4_marker, pg4_marker, ch4_marker), FluentAssertions only, no production edit | tests/nextorm.integration.tests/{Sqlite/PreparerBatteryBranchCoverage4Tests.cs,Sqlite/PreparationDifferentialBranchCoverage4Tests.cs,SqlServer/SqlServerPreparationDifferentialBranchCoverage4Tests.cs,Postgres/PostgresPreparationDifferentialBranchCoverage4Tests.cs,ClickHouse/ClickHousePreparationDifferentialBranchCoverage4Tests.cs}
2026-10-18T02:54:15Z | D1 | revision r1 | iteration n 1/3 | D1 inner build sequence: d1-build1 rc 1 (Build FAILED, 0 warnings / 2 errors - CS1503 :181 and CS1501 :451, both test-side call shapes) -> fix -> d1-build2..build5 and d1-build7 rc 0 (0 warnings / 0 errors each); whole-solution B d1-build6-solution rc 0 (Build succeeded, 0 warnings / 0 errors, 45.69s over nextorm.slnx) | rv1/results/d1-build1.log, d1-build2.log, d1-build3.log, d1-build4.log, d1-build5.log, d1-build6-solution.log, d1-build7.log
2026-10-18T03:04:38Z | D1 | revision r1 | iteration n 1/3 | D1 guard G green: IntegrationSuiteScopingTests total 2, failed 0, succeeded 2 (database evidence + FluentAssertions-only over the new classes) | rv1/results/d1-guard.log
2026-10-18T03:04:45Z | D1 | revision r1 | iteration n 1/3 | D1 per-class inner results: PreparerBattery 11 selected / 0 failed (run2 and frozen-tree class run); PreparationDifferential 4 selected / 0 failed (run2) then 5 selected / 0 failed after the late ForJson fact; SqlServer 3 / 0; Postgres 2 / 0; ClickHouse 3 / 0 (after the red run1) | rv1/results/d1-battery-run2.log, d1-PreparerBatteryBranchCoverage4Tests.log, d1-differential-run2.log, d1-PreparationDifferentialBranchCoverage4Tests.log, d1-sqlserver-run1.log, d1-postgres-run1.log, d1-clickhouse-run2.log
2026-10-18T02:57:32Z | D1 | revision r1 | iteration n 1/3 | D1 red first runs kept with rc 1 (never dropped, never relabelled): battery-run1 11 selected / 3 failed (cast expectation corrected; ExtremeRow equal direction dropped - twin descriptor did not match; cross-join shape corrected to null + live counts), differential-run1 4 / 2 failed (FilterScope equal direction dropped: QueryFilterScope has no Equals override so FilterScopeEqual:3839-3840 is reference equality; an unsupported Cast projection removed and replaced by a supported cast pair), clickhouse-run1 3 / 2 failed (ArrayJoin: 120 s HttpClient seed timeout; Settings: twin `.Settings(("join_use_nulls","1"))` descriptors did not match -> true arm removed + defect candidate) | rv1/results/d1-battery-run1.log, d1-differential-run1.log, d1-clickhouse-run1.log
2026-10-18T03:28:19Z | D1 | revision r1 | iteration n 1/3 | D1 journal recorded: 18 real commands = 36 start/finish events (9 scoped single-class `dotnet run` runs as kind `other` under the recorded POLICY RESOLUTION, 8 `dotnet build` as kind `build`, 1 guard `dotnet test --filter` as kind `unit`), argv arrays, env separated, every non-zero exit_code 1 kept (the red first build d1-build1 and the three red test runs), boundary_runs_executed 0; validator `journal` rc 0 with empty stdout (no inner `other`-as-integration flag) | rv1/journal.json, rv1/results/validator-journal.txt
2026-10-18T03:28:19Z | D1 | revision r1 | iteration n 1/3 | D1 ledger coverage: 27 D1 rows - closed 13 (D1-01/02/03/06/09/15/19/20/21/22/23/25/26), partial 6 (D1-05 settings true arm, D1-11 source-lock equal arm, D1-12 LimitBy true arm, D1-13 DistinctOn true arm, D1-14 ExtremeRow true arm, D1-18 six expression slots), deferred 8 (D1-04 Sample, D1-07 index hints, D1-08 sub-query hint on a derived source, D1-10 TableSample/Temporal, D1-16 EagerLoadShape, D1-17 context-level settings, D1-24 cross-join filter cluster, D1-27 PreWhere); every dropped/partial row carries an entry with raw cost and an activation trigger | rv1/inputs/deferred.json

## Evidence contract (rows) - D1 amendment (r1, 2026-10-18T03:28:19Z)

Only row V01's artifact set changed; the R/E rows are untouched and no row status is claimed met here.

| row | as planned | as produced by D1 (amendment) |
| --- | --- | --- |
| V01 | `rv1/results/d1-inner.log` (+ `rv1/results/d1-provider-inner.log` if container classes run) | the plan's two aggregate log names were never produced; the real evidence is per-class: `rv1/results/d1-PreparerBatteryBranchCoverage4Tests.log` (11/0), `rv1/results/d1-PreparationDifferentialBranchCoverage4Tests.log` (5/0), `rv1/results/d1-battery-run1.log` (red, 11/3), `rv1/results/d1-battery-run2.log` (11/0), `rv1/results/d1-differential-run1.log` (red, 4/2), `rv1/results/d1-differential-run2.log` (4/0), plus the provider inner logs `rv1/results/d1-sqlserver-run1.log` (3/0), `rv1/results/d1-postgres-run1.log` (2/0), `rv1/results/d1-clickhouse-run1.log` (red, 3/2) and `rv1/results/d1-clickhouse-run2.log` (3/0) |
| E02 | `rv1/journal.json`, `rv1/results/validator-journal.txt` | still open at CHECK: the raw journal and the `journal` validator output now exist (rc 0, empty stdout); `project`/`report` were **not** run in this bookkeeping dispatch |

Journal ids are `d1-build1..build7`, `d1-battery-run1/2`, `d1-battery-class`, `d1-differential-run1/2`, `d1-differential-class`, `d1-guard`, `d1-sqlserver-run1`, `d1-postgres-run1`, `d1-clickhouse-run1/2` - 18 start/finish pairs, all phase `inner`.
2026-10-18T03:59:10Z | DO | revision r1 | iteration n 1/3 | D2 authoring + inner loop: PlanEqualityDifferentialBranchCoverage4Tests authored (9 facts / 9 distinct plan- and expression-key arms: captured-object member, null-valued closure vs null constant, char-vs-string + unary/binary/Convert.ToInt64, plan-key index-hint string lists, plan-key grouping sets, plan-key window frame cluster, one/two-level correlated nesting, fresh ParameterExpression identity, hash/equals matrix incl. both null hash arms) over class-owned pd4_marker/pd4_line (own DDL, unique live indexes pd4_grp_idx/pd4_tag_idx); whole-solution B d2d4-build1 rc 1 (6 test-side compile errors) -> d2d4-build2 rc 0 (0 warnings / 0 errors); guard d2d4-guard rc 0 (2/0); d2-run1 rc 1 (9 selected / 2 failed: SQLite rejects tables-in-scope hints at preparation, and a NULL-valued closure is a NULL parameter under three-valued logic, not IS NULL) -> fix -> affected build d2d4-build3 rc 0 -> d2-run2 rc 0 (9/0) -> d2-run3-finaltree rc 0 (9/0, final tree) | rv1/results/d2d4-build1.log, d2d4-build2.log, d2d4-guard.log, d2-run1.log, d2d4-build3.log, d2-run2.log, d2-run3-finaltree.log
2026-10-18T03:59:10Z | DO | revision r1 | iteration n 1/3 | D4 authoring + inner loop: EagerGraphBranchCoverage4Tests authored (5 facts: filtered-child split-vs-single with 0/1/many children and no leakage, two-level graph with a childless middle level sync+async, per-parent top-N over UNIQUE descending keys plus the Page offset shape in both modes, SingleQuery+JoinInto stitched sync+async with the Join/Apply guard, and the flat+nested unsupported-shape chains GroupBy / TablesInScopeHint / OrderBy,Distinct) over class-owned g4_parent/g4_child/g4_grand/g4_note; d4-run1 rc 1 (5 selected / 2 failed - both message expectations corrected to the observed public guard text) -> affected build d4d2-build4 rc 0 -> d4-run2 rc 0 (5/0) | rv1/results/d4-run1.log, d4d2-build4.log, d4-run2.log
2026-10-18T03:59:10Z | DO | revision r1 | iteration n 1/3 | D2+D4 ledger accounting: D2 closed 3 (D2-01 StringListsEqual incl. null/length/element, D2-02 WindowsEqual frame cluster, D2-06 constant/closure/member/null/char/convert arms), partial 2 (D2-04 grouping sets closed but ArrayJoinExpressions unreachable on SQLite, D2-05 GetHashCodeCore exercised with CteDefinitionsEqual not authored), deferred 3 (D2-03 Settings - ClickHouse-only writer, D2-07 spill counters >4 parameters, D2-08 From/Join/Sorting plan comparers); D4 closed 2 (D4-01 level-one fold, D4-02 sync/async execute + guards), partial 1 (D4-03 SubtreeItemCount exercised, three-level repeated-type arm blocked by the cycle guard); every dropped/partial row carries its raw cost and an activation trigger | rv1/inputs/deferred.json (38 rows)
2026-10-18T03:59:10Z | DO | revision r1 | iteration n 1/3 | D2+D4 defects: no new confirmed defect candidate - the three red-run observations were test-side call shapes (compile errors), a dialect capability (SQLite rejects table-level hint lists) and documented semantics (a bound NULL parameter is not IS NULL); rv1/inputs/defects.json unchanged, no candidate filed | rv1/inputs/defects.json
2026-10-18T03:59:10Z | DO | revision r1 | iteration n 1/3 | D2+D4 journal recorded incrementally (56 events total: D2 build/guard/run pairs + D4 run/build/run pair + the D2 final-tree re-run; every non-zero exit kept with its real exit_code 1), argv arrays with the skip-provider env outside argv, source_revision HEAD 1b140d23; validator `journal` rc 0 with empty stdout | rv1/journal.json, rv1/results/validator-journal.txt
2026-10-18T04:10:00Z | DO | revision r1 | iteration n 1/3 | D3 authoring + inner loop: SourceRenderingDifferentialBranchCoverage4Tests (5 facts over class-owned sr4_marker/sr4_line, own DDL incl. a real ix_sr4_grp index: nested derived source two levels, parameterised JOINED raw source re-bound A->B->A, RIGHT/FULL/CROSS spellings with both unmatched sides + per-join table-hint rejections on a derived and on a physical join source each paired with the live join, INDEXED BY Use/Force, temporal dispatch gate with a live counterpart), SqlServerSourceRenderingBranchCoverage4Tests (2 facts over ss4r_marker/ss4r_line: table hint on the primary source of a JOINED statement re-prepared on one context; WithServer(".") linked-server qualifier) and PostgresSourceRenderingBranchCoverage4Tests (2 facts over pg4r_marker: correlated LATERAL CrossApply + the plain-table fallback, per-table-hint rejection on a derived join source with the live counterpart); d3d5-build1 rc 1 (14 test-side compile errors) -> d3d5-build2 rc 0 (0 warnings / 0 errors); guard d3d5-guard rc 0 (2/0); d3-sqlite-run1 rc 1 (5 selected / 1 failed: LEFT JOIN projected a non-nullable int from an unmatched row -> materializer threw at ordinal 1) -> build3 rc 0 -> d3-sqlite-run2 rc 0 (5/0) -> final-tree d3-sqlite-run3-finaltree rc 0 (5/0); d3-postgres-run1 rc 0 (2/0, DOCKER_HOST set, no skip env); d3-sqlserver-run1 rc 0 (2/0); final-tree guard d3d5-guard2-finaltree rc 0 (2/0) | rv1/results/d3d5-guard2-finaltree.log, rv1/results/d3d5-build1.log, d3d5-build2.log, d3-sqlite-run1.log, d3d5-build3.log, d3-sqlite-run2.log, d3-sqlite-run3-finaltree.log, d3d5-guard.log, d3-postgres-run1.log, d3-sqlserver-run1.log
2026-10-18T04:10:00Z | DO | revision r1 | iteration n 1/3 | D5 authoring + inner loop: CorrelatedAggregateBranchCoverage4Tests (3 facts over class-owned c4_parent/c4_child/c4_group/c4_root with the declared Children collection and the Group/Root reference chain: aggregate-terminal rewriter in a projection mixing Count() with Sum/Max over nullable results and a non-nullable Sum/Min, the childless parent asserted by VALUE (0 / NULL), the same shape re-prepared A->A->B->B->A on one context, typed null literal + captured value inside an exists correlation); d3d5-build4 rc 0; d5-run1 rc 1 (4 selected / 3 failed: (int?)null cast returned an empty set -> lifted == null form + defect candidate; builder aggregate terminal in a WHERE predicate threw an internal InvalidOperationException -> row deferred, never asserted; reference-hop form threw "variable navigation0 ... is not defined" -> deferred) -> build5 rc 0 -> d5-run2 rc 1 (4/1: FK property still int? while the DDL column is int) -> build6 rc 0 -> d5-run3 rc 1 (4/1: reference hop still fails inside NavigationExpansion) -> fact replaced by the reference-adapter guard -> d3d5-build6 rc 0 -> d5-run4 rc 1 (4/1: the documented adapter rejection does not fire, arm unreachable) -> fact dropped -> d3d5-build7 rc 0 -> d5-run5 rc 0 (3/0, final tree) | rv1/results/d5-run1.log, d5-run2.log, d5-run3.log, d5-run4.log, d5-run5.log, d3d5-build4.log, d3d5-build5.log, d3d5-build6.log, d3d5-build7.log
2026-10-18T04:10:00Z | DO | revision r1 | iteration n 1/3 | D3+D5 bookkeeping: journal extended with 18 real commands = 36 start/finish events (7 whole-solution builds kind build, 1 guard kind unit, 10 scoped single-class runs kind other; the guard and every SQLite run carry NEXTORM_SKIP_PROVIDERS outside argv, the two container runs carry DOCKER_HOST; every non-zero exit kept with its real exit_code 1); validator journal rc 0 with empty stdout; deferred.json grows 13 rows (D3-01 partial, D3-05/D3-07 closed, D3-02/03/06/08/09 deferred, D3-04 partial; D5-01 partial, D5-03 closed, D5-02/D5-04 deferred) and defects.json gains 4 candidates filed:false milestone 1.0.9-rc4 (c4-builder-aggregate-terminal-in-predicate, c4-correlated-reference-hop-navigation-scope, c4-typed-null-cast-in-correlated-predicate, c4-reference-adapter-not-rejected); no production edit, no commit, no push, boundary_runs_executed stays 0 | rv1/journal.json, rv1/results/validator-journal.txt, rv1/inputs/deferred.json, rv1/inputs/defects.json
2026-10-18T04:11:47Z | boundary | revision r1 | iteration n 1/3 | preflight: skill running-integration-tests loaded; DOCKER_HOST socket present, /_ping OK; NEXTORM_SKIP_PROVIDERS printenv rc 1 (unset); five stopped reusable containers listed (postgres ff3bb6dd042f, sqlserver 1b5207b30a62, mysql 3c255d28405f, mariadb 914f06a0ba5f, clickhouse 6bf39546e2b9); live baseline integration.cobertura.xml sha256 a1db019e21514fd611a6379b0c0c7a560ae30ab86d0a001a4f50da1e9724ab5a == pinned copy, pinned Summary.txt d4d0f17816efdb4b5f844f4112e12d94397bf21f50678803b41c77bb83c1b258 == live report copy, both to be overwritten by F/T; freshness: 0 src/tests .cs/.csproj/.props newer than the 09:08:12 test assembly, so --no-build is honest | rv1/results/boundary-preflight.log
2026-10-18T04:11:51Z | boundary | revision r1 | iteration n 1/3 | B whole-solution build rc 0: Build succeeded, 0 Warning(s), 0 Error(s), Time Elapsed 00:00:02.77 over nextorm.slnx | rv1/results/d6-build.log
2026-10-18T04:11:57Z | boundary | revision r1 | iteration n 1/3 | G guard rc 0: IntegrationSuiteScopingTests total 2, failed 0, succeeded 2, skipped 0 (duration 3s 180ms) | rv1/results/g-final.log
2026-10-18T04:14:09Z | boundary | revision r1 | iteration n 1/3 | F the single boundary sweep rc 0 (green, never retried; no F2): Total 4450, Errors 0, Failed 0, Skipped 195, Not Run 0, Time 107.746s; all container providers exercised fail-closed; per-provider skips MySql 80 / SqlServer 48 / Sqlite 41 / Postgres 26, ClickHouse 0 / MariaDb 0 | rv1/results/F.log
2026-10-18T04:14:13Z | boundary | revision r1 | iteration n 1/3 | skip budget gate rc 0 over the final F log: OK, 195 skips match the pinned budget; 37 reasons (measured), per-provider split identical to the pin (MySql 80 / SqlServer 48 / Sqlite 41 / Postgres 26; ClickHouse 0, MariaDb 0) | rv1/results/skip-budget.log, rv1/results/skip-ledger.json
2026-10-18T04:14:31Z | boundary | revision r1 | iteration n 1/3 | T reportgenerator rc 0 (7.4 s) with -classfilters:-NextORM.Core.InMemory* -riskhotspotassemblyfilters:+nextorm.*; counted final: branches 26199/38317 = 68.3 %, lines 46022/59713 = 77 %, methods 5607/8191 = 68.4 % | rv1/results/T.log, rv1/results/counted-final.txt
2026-10-18T04:15:33Z | boundary | revision r1 | iteration n 1/3 | counted delta MEASURED via two Clover models that each reproduce their Summary exactly: net +392 branches / +284 lines (>= +250 floor, margin +142, conservative test-attributable lower bound +356); BUT denominators moved 38281->38317 (+36) and 59676->59713 (+37): three files (MemberInfoExtensions +24, MemberTranslator +8, EntityMetadata +4) belong to a six-file product change with mtimes 2026-10-18 08:49:39 that postdates the pinned baseline build and adds the two methods 8189->8191 - recorded as a CHECK adjudication item, not hidden; exact partition +393 positive / -1 negative = +392 net, and the 'no file lost branches' claim is FALSE: IEqualityComparerExtensions.cs lost exactly 1 branch (total unchanged 48) | rv1/results/coverage-delta.md, rv1/results/contributions.json, rv1/results/delta-compute.log
2026-10-18T04:16:55Z | boundary | revision r1 | iteration n 1/3 | baseline numbers synced to the measured post-F model (AGENTS.md:75 and .github/workflows/dotnet.yml:29-32 only, numbers only): 76.6 % line / 67.4 % branch -> 77.0 % line / 68.3 % branch; every MIN_* threshold byte-identical (85/75/65/75), CRLF preserved (AGENTS.md 101/101, dotnet.yml 215/215); only measured-baseline text changed, no threshold line changed | rv1/results/agents-baseline.diff, rv1/results/workflow-baseline.diff, rv1/results/threshold-proof.txt
2026-10-18T04:17:20Z | boundary | revision r1 | iteration n 1/3 | defects: F was green on the first sweep, so no failure needed classification and no candidate was added; rv1/inputs/defects.json unchanged (4 candidates from D5 remain) | rv1/inputs/defects.json
2026-10-18T04:17:20Z | boundary | revision r1 | iteration n 1/3 | journal bookkeeping: 3 real boundary commands appended as 6 start/finish events (B kind build, G kind unit, F kind integration, boundary_runs_executed 1 vs planned 1; every exit_code 0 kept as measured, argv arrays with DOCKER_HOST in env never argv); validator journal rc 0 with empty stdout | rv1/journal.json, rv1/results/validator-journal.txt

2026-10-18T04:31:00Z | CHECK | revision r1 | iteration n 1/3 | defect triage `c4-clickhouse-twin-descriptor-mismatch`: exact four-command repro on a fresh ClickHouse context gives Settings A=[join_use_nulls=1] B=[join_use_nulls=1], Matches(B,A,bAgain)=True and the three DIFFERENCE comparisons False; the run1 false does not reproduce (that run also hit a 120 s ClickHouse container timeout) and no differing shape member exists -> `not-a-defect`, not filed | rv1/inputs/defects/c4-clickhouse-twin-descriptor-mismatch.md, rv1/results/defect-triage.log
2026-10-18T04:31:05Z | CHECK | revision r1 | iteration n 1/3 | defect `c4-builder-aggregate-terminal-in-predicate`: reproduced on SQLite (InvalidOperationException "binary operator GreaterThan is not defined for Func<IQueryRegistry,QueryCommand> and int?" at CorrelatedQueryExpressionVisitor.cs:1770; same aggregate in a projection is green) -> product defect FILED as #303 with milestone 1.0.9-rc4 | https://github.com/AlexeyShirshov/nextorm/issues/303, rv1/inputs/defects/c4-builder-aggregate-terminal-in-predicate.md
2026-10-18T04:31:10Z | CHECK | revision r1 | iteration n 1/3 | defect `c4-correlated-reference-hop-navigation-scope`: reproduced on SQLite ("variable 'navigation0' of type C4Group referenced from scope '', but it is not defined", compiled at CorrelatedQueryExpressionVisitor.cs:1479 after NavigationExpansion.cs:426/432) -> product defect FILED as #304 with milestone 1.0.9-rc4 | https://github.com/AlexeyShirshov/nextorm/issues/304, rv1/inputs/defects/c4-correlated-reference-hop-navigation-scope.md
2026-10-18T04:31:15Z | CHECK | revision r1 | iteration n 1/3 | defect `c4-typed-null-cast-in-correlated-predicate`: reproduced in a correlated exists (lifted -> [8002], typed -> [], SQL "t2.amount = null" instead of "is null"); duplicate of open #296 (same nullable value-type null-literal miss) -> COMMENT posted on #296 (comment 6245668235), no new issue | https://github.com/AlexeyShirshov/nextorm/issues/296#issuecomment-6245668235, rv1/inputs/defects/c4-typed-null-cast-in-correlated-predicate.md
2026-10-18T04:31:20Z | CHECK | revision r1 | iteration n 1/3 | defect `c4-reference-adapter-not-rejected`: no exception is thrown, but the reference adapter renders "left join c4_group ... t2.id is not null" and correctly excludes a dangling-FK parent, which matches the PUBLIC XML doc (reference adaptation supported); the internal rejection at LowerAdapterTerminal.cs:529-531 is dead for this shape -> `not-a-defect` (stale internal comment, no user-visible defect), not filed | rv1/inputs/defects/c4-reference-adapter-not-rejected.md, rv1/results/defect-triage.log
2026-10-18T04:31:25Z | CHECK | revision r1 | iteration n 1/3 | observation `c4-iequalitycomparerextensions-branch-loss`: IEqualityComparerExtensions lost exactly 1 covered branch in the counted post-F model (file total unchanged at 48); recorded as a measurement observation / CHECK adjudication item, NOT a product defect, not filed | rv1/results/coverage-delta.md, rv1/inputs/defects.json
2026-10-18T04:31:30Z | CHECK | revision r1 | iteration n 1/3 | duplicate search completed for every candidate before filing (several keyword variants of symptom+component+cause over --state open AND --state closed plus `gh search issues --author @me`); no match for the aggregate-in-predicate or reference-hop defects; #296 matched the typed-null candidate; #252/#298/#299/#300 confirmed unrelated; issue-search.log and issue-filing.log written | rv1/results/issue-search.log, rv1/results/issue-filing.log
2026-10-18T04:31:35Z | CHECK | revision r1 | iteration n 1/3 | defects.json updated: 5 triaged candidates (2 filed, 1 duplicate-comment, 2 not-a-defect, 0 candidate) + 1 observation; applied_fixes 0 for every key (product untouched); no commit, no push | rv1/inputs/defects.json

2026-10-18T04:33:16Z | DO | revision r1 | iteration n 1/3 | preservation audit (evidence freeze): pre-existing inventory 100 entries (62 modified / 11 deleted / 27 untracked) is preserved - 0 removals and 0 status-code changes (full comm of the raw snapshot lines against the current `git status --short`); this cycle added exactly the 11 untracked *BranchCoverage4Tests.cs classes, while 20 further new entries (6 src/** files, 2 benchmarks, 4 docs, docs/specs/comparison + D247 status, .pdca/T247/, 2 .pdca/scripts/perf files, 2 docs/guide/32 pages, tests/nextorm.sqlite.tests DerivedEntitySqlGenerationTests.cs) belong to the CONCURRENT dispatch | rv1/results/tree-preserved.txt, rv1/results/tree-preserved-status.txt
2026-10-18T04:33:16Z | DO | revision r1 | iteration n 1/3 | section-2 src/ audit: `git diff --name-only -- src/` returns 6 files, every one with mtime 2026-10-18 08:49:39.33-39.36 (+0500) and +94/-18 total (MemberInfoExtensions 59/5, MemberTranslator 16/8, EntityMetadata 15/1, SqlSourceRenderer 2/2, RangeColumnsSqlTranslator 1/1, SqliteFunctionTranslator 1/1); all six appear in git status only AFTER the D0 snapshot (02:27:05Z) and before F (09:08-09:14), so they are attributed to the CONCURRENT dispatch, not to cycle 4; cycle 4 authored ZERO src/** or tracked tests/** edits, no NoWarn/SuppressMessage added (0 occurrences in the src diff and in the 11 new classes) | rv1/results/tree-preserved.txt, rv1/results/tree-preserved-status.txt
2026-10-18T04:34:00Z | DO | revision r1 | iteration n 1/3 | evidence hygiene (no receipt edited): rv1/results/contributions.json and rv1/results/skip-ledger.json moved byte-identically to rv1/inputs/ because pdca_evidence.py lint enumerates every *.json under rv1/results/ as a receipt; the defects.json evidence pointer was repointed to rv1/inputs/contributions.json. sha256 of both bodies unchanged | rv1/inputs/contributions.json, rv1/inputs/skip-ledger.json, rv1/inputs/defects.json
2026-10-18T04:33:38Z | DO | revision r1 | iteration n 1/3 | reviewer evidence pack frozen: rv1/review/ holds the FULL bodies (not excerpts) of the 11 cycle-authored integration test classes, AGENTS.md, .github/workflows/dotnet.yml and rv1/results/coverage-delta.md, plus the skip ledger - 15 copies, each sha256-identical to its original (content_equal=YES) - and the blanket liveness evidence as one explicit extract, results/F-summary.txt (verbatim TEST EXECUTION SUMMARY block of F.log, marked EXTRACT); manifest.tsv = 16 rows listing original path, staged path, both sha256, sizes and equality; source_identity.txt pins HEAD 1b140d23cd12ecc19af4bfbad3563b6e8281aa63 and the exact reviewed working tree (132 status entries = 76 M / 11 D / 45 ??) with the cycle-4-authored file list | rv1/review/manifest.tsv, rv1/review/source_identity.txt, rv1/results/reviewer-pack.txt
2026-10-18T04:35:34Z | DO | revision r1 | iteration n 1/3 | scope re-validated at the freeze: validate_inner_loop.py brief rc 0 (0-byte stdout) on the unchanged sealed scope (projects tests/nextorm.integration.tests + tests/nextorm.core.tests, 12 exact selectors, integration_boundary_runs 1); the D0 record precedes every edit and is kept above the re-run block | rv1/results/validator-brief.txt, rv1/inputs/scope.json
2026-10-18T04:35:24Z | DO | revision r1 | iteration n 1/3 | frozen evidence pack generated: pdca_evidence.py generate rc 0 (0-byte stdout), lint rc 0 (0-byte stdout), validate_inner_loop.py manifest rc 0; frozen synthetic tree 5e6078a0f6e890b6061592df3a8fa00bd6cce6e7 over base_ref 1b140d23cd12ecc19af4bfbad3563b6e8281aa63 with footprint [tests/nextorm.integration.tests, AGENTS.md, .github/workflows/dotnet.yml] and exclusions [.pdca/**]; issue marker milestone:1.0.9-rc4 (milestone marker, not an issue number); manifest 15 rows = 12 met (R01-R05, R07, V01-V05, E01) + 3 open (R06, E02, E03) | rv1/results/generate.log, rv1/results/lint.log, rv1/results/validator-manifest.txt, rv1/manifest.json
2026-10-18T04:35:10Z | DO | revision r1 | iteration n 1/3 | REAL FAILURE recorded, NOT waived: journal validator rc 0 (0-byte stdout), but `project` rc 2 with ten 'FAIL: execution N: broad command not allowed in inner phase: [dotnet, build, nextorm.slnx, -c, Debug]' (executions 9,18,19,28,29,30,33,36,37,39) and `report` rc 1 'malformed JSON in inputs/report.json' (the refused projection wrote nothing). The expected accepted reason 'multiple boundary solution builds: N' did NOT occur: rv1/journal.json labels the ten whole-solution builds phase 'inner' whereas the plan pins B at phase 'boundary' (cycle 3's journal has 10 boundary builds and produced the accepted reason). Rows R06 and E02 therefore stay open; no journal edit was made in a freeze dispatch and no reason was suppressed | rv1/results/validator-journal.txt, rv1/results/validator-project.txt, rv1/results/validator-report.txt, rv1/inputs/report.json

### Notice - denominator drift (measured, not hidden)

| item | measured value |
| --- | --- |
| branch denominator | 38281 -> **38317** (+36) |
| line denominator | 59676 -> **59713** (+37) |
| method denominator | 8189 -> **8191** (+2) |
| raw counted delta | covered branches **+392** (25807 -> 26199), covered lines **+284** (45738 -> 46022) |
| conservative test-attributable bound | **+356** (= 392 - 36; using the measured covered delta of the changed files: 392 - 33 = +359) -> floor **+250** still met |
| exclusion / threshold change | **none** - `-classfilters:"-NextORM.Core.InMemory*"` unchanged, MIN_* = 85 / 75 / 65 / 75 byte-identical |

Cause (measured, not inferred): six `src/**` files were modified in the shared working tree at `2026-10-18 08:49:39.33-39.36 +0500`
by a CONCURRENT dispatch (after the pinned baseline build at 05:53:40, before sweep F at 09:08-09:14):

| file | git diff --stat | branch total | covered-branch delta |
| --- | --- | --- | --- |
| `src/nextorm.core/DataContext/Meta/Implementation/EntityMetadata.cs` | 15+/1- | 8 -> 12 (+4) | +3 |
| `src/nextorm.core/DataContext/SqlSourceRenderer.cs` | 2+/2- | 518 (unchanged) | +19 (real test gain) |
| `src/nextorm.core/MemberInfoExtensions.cs` | 59+/5- | 22 -> 46 (+24) | +17 |
| `src/nextorm.core/Visitors/MemberTranslator.cs` | 16+/8- | 556 -> 564 (+8) | +13 |
| `src/nextorm.core/Visitors/RangeColumnsSqlTranslator.cs` | 1+/1- | unchanged | - |
| `src/nextorm.core/Visitors/SqliteFunctionTranslator.cs` | 1+/1- | unchanged | - |

Only three files contribute the +36 branch points (MemberInfoExtensions +24, MemberTranslator +8, EntityMetadata +4); the same
change adds the two methods (8189 -> 8191). Cycle 4 did not edit `src/**` (see `rv1/results/tree-preserved.txt`) and the tree was
left exactly as found. Effect on the verdict: raw counted +392; subtracting the whole +36 leaves +356 of test-attributable gain,
i.e. the **+250 floor is met** either way. The literal REQ-COV clause "both denominators unchanged (38281 / 59676)" is NOT
satisfied and is left for CHECK to adjudicate - it is recorded here, in `rv1/results/coverage-delta.md` and in the R01/R02 row
notes, never hidden. No exclusion was broadened and no threshold was lowered to produce the measured state.

### Notice - journal phase labels block the accepted project/report reason

`validate_inner_loop.py project` exits 2 with ten `broad command not allowed in inner phase` failures, not with the accepted
`FAIL: multiple boundary solution builds: N`. The journal labels the ten `dotnet build nextorm.slnx` events phase `inner`, while
the plan pins `B` at phase `boundary` and cycle 3's journal carries exactly those builds as `boundary`. This is a capture defect
in `rv1/journal.json`, not a sweep and not a waiver: R06 and E02 are frozen **open** with the verbatim reason, the rc-0 half of
the pipeline (brief/journal/generate/lint/manifest) is recorded, and a journal repair is a DO loop-back decision for the
orchestrator - it was deliberately not performed inside this freeze dispatch.

### Defect history

| key | seen at | fix count (0, product untouched) | issue | status |
| --- | --- | --- | --- | --- |
| `c4-clickhouse-twin-descriptor-mismatch` | D1 2026-10-18T03:07:55Z (rv1, run1) | 0 | — | not-a-defect (not reproducible; probe `Matches` returns true) |
| `c4-builder-aggregate-terminal-in-predicate` | D5 2026-10-18T04:06:23Z (rv1, d5-run1) | 0 | #303 | filed (milestone 1.0.9-rc4) |
| `c4-correlated-reference-hop-navigation-scope` | D5 2026-10-18T04:07:27Z (rv1, d5-run3) | 0 | #304 | filed (milestone 1.0.9-rc4) |
| `c4-typed-null-cast-in-correlated-predicate` | D5 2026-10-18T04:06:23Z (rv1, d5-run1) | 0 | #296 (comment 6245668235) | duplicate-comment |
| `c4-reference-adapter-not-rejected` | D5 2026-10-18T04:07:54Z (rv1, d5-run4) | 0 | — | not-a-defect (correct presence semantics; stale internal rejection arm) |
| `c4-iequalitycomparerextensions-branch-loss` | boundary 2026-10-18T04:15:33Z (rv1, T) | 0 | — | observation (measurement, not a product defect) |

### Notice - journal phase reclassification (authorised)

Orchestrator decision, quoted verbatim: "AUTHORISED REPAIR (orchestrator decision, record it verbatim in the status file and in the journal note): the 10 whole-solution `dotnet build nextorm.slnx -c Debug` executions in `rv1/journal.json` are journalled with `phase: "inner"`, but the pinned plan classifies B as the **boundary** build — cycle 3’s accepted journal does exactly this (10 × `boundary`) and its status file states “B journalled phase boundary”. The validator therefore rejects them as broad inner commands."

Applied (only the `phase` field of those executions, plus the one appended note per affected event; argv, timestamps, exit codes, selected_count, `log` paths, revisions, kinds, event count and event order untouched - no execution added or removed):

| item | value |
| --- | --- |
| reclassified executions | 10 whole-solution build executions `inner` -> `boundary`: `d1-build6-solution`, `d2d4-build1`, `d2d4-build2`, `d3d5-build1`, `d3d5-build2`, `d3d5-build3`, `d3d5-build4`, `d3d5-build5`, `d3d5-build6`, `d3d5-build7` |
| touched event records | 20 (start+finish of those ids; event indices 18,19,36,37,38,39,56,57,58,59,60,65,67,70,72,74,76,78,81,84) |
| phase fields changed | 19 - the `d1-build6-solution` finish event carries no `phase` field, so only its note was appended |
| note on every touched event | "phase reclassified inner->boundary per plan r1 pinned B; command, time, exit code and log unchanged (cycle-3 precedent)" (appended to the pre-existing note, never replacing it) |
| deliberately NOT reclassified | `boundary-build` (already `boundary`); the project-scoped `dotnet build tests/nextorm.integration.tests` events (stay `inner`); every scoped single-class `dotnet run` (stays `other`/`inner`); every guard `dotnet test --filter` (stays `unit`); F (stays `integration`/`boundary`) |
| structural diff check | a recursive JSON diff before/after reports changes ONLY at `events[i].phase` and `events[i].note` - no other key added, removed or changed |

Resulting pipeline (rc and reason verbatim): `journal` rc 0; `project` rc 2 with EXACTLY `FAIL: multiple boundary solution builds: 11` (the real count: 10 repaired B builds + the already-`boundary` `boundary-build`); `report --journal` rc 2 with the SAME single reason. `inputs/report.json` is the script's own mechanical `project_journal` projection (50 executions, every journal start present) because the CLI prints nothing while the report carries the accepted violation - it was never hand-edited. Consequently **R06 met**, **E02 met**; **E03 stays open** with reason "CHECK verdict pending". Defect filing was verified with `gh issue view` (`rv1/results/issue-verify.log`): #303 and #304 OPEN with label `bug` and milestone `1.0.9-rc4`, #296 comment `6245668235` present; the two `not-a-defect` probes and the one observation are unchanged and carried by R07.

### Progress log - journal phase reclassification dispatch (continued)

2026-10-18T04:38:14Z | DO | revision r1 | iteration n 1/3 | authorised journal phase reclassification: 10 whole-solution B builds `inner` -> `boundary` + appended note, 20 event records / 19 phase fields, no other field touched, no execution added or removed | rv1/journal.json
2026-10-18T04:38:14Z | DO | revision r1 | iteration n 1/3 | validate_inner_loop.py journal rc 0, empty stdout | rv1/results/validator-journal.txt
2026-10-18T04:38:27Z | DO | revision r1 | iteration n 1/3 | project rc 2 with EXACTLY `FAIL: multiple boundary solution builds: 11` (the accepted reason only, nothing waived); inputs/report.json materialised from the module's own project_journal (50 executions, ids unique) because the CLI prints nothing on the accepted failure | rv1/results/validator-project.txt, rv1/inputs/report.json
2026-10-18T04:38:28Z | DO | revision r1 | iteration n 1/3 | report --journal rc 2 with EXACTLY the same single reason `FAIL: multiple boundary solution builds: 11` (43-byte stdout, empty stderr) | rv1/results/validator-report.txt
2026-10-18T04:38:46Z | CHECK | revision r1 | iteration n 1/3 | defect filing verified with gh (not assumed): #303 and #304 CREATED/OPEN, label bug, milestone 1.0.9-rc4; #296 comment 6245668235 present; 2 not-a-defect probes + 1 observation unchanged, applied_fixes 0 everywhere -> R06 met (pipeline + verified defect records), E02 met, E03 remains open (CHECK verdict pending) | rv1/results/issue-verify.log, rv1/inputs/defects.json, rv1/inputs/defects/*.md, rv1/inputs/contract.json
2026-10-18T04:39:08Z | DO | revision r1 | iteration n 1/3 | pdca_evidence.py generate rc 0; new generation 400006e1b95851aaeaf3e1ac9123685c97a3e626542e821a95c3bb97a27f910e; frozen tree stays 5e6078a0f6e890b6061592df3a8fa00bd6cce6e7 (footprint untouched) | rv1/results/generate.log, rv1/manifest.json
2026-10-18T04:39:17Z | DO | revision r1 | iteration n 1/3 | lint run 1 rc 2: duplicate row ids because the superseded generation 76be7081 was still enumerated (verbatim in the log); superseded manifest + 15 receipts copied aside AS TEXT to rv1/inputs/superseded-generation-76be7081/ (never hand-edited) and the old generation removed -> lint run 2 rc 0 | rv1/results/lint.log
2026-10-18T04:39:20Z | DO | revision r1 | iteration n 1/3 | validate_inner_loop.py manifest rc 0; 15 rows = 14 met (R01-R07, V01-V05, E01, E02) / 1 open (E03 "CHECK verdict pending") | rv1/results/validator-manifest.txt, rv1/manifest.json

## ACT (round r1, attempt n 1/3)

### CHECK verdict (pointer)

- **CHECK: PASS** on plan **r1**, attempt **n 1/3**, contract **rv1**: **14/15 contract rows binding-met**
  (R01..R07, V01..V05, E01, E02), **0 blocking findings**. The full verdict, the REQ matrix, the binding /
  audit classification and the five accepted non-blocking findings are written verbatim in
  `rv1/results/check-verdict.md` (source of this section).
- **E03 is the audit row classified `diagnostic_nonbinding`** (its subject is the CHECK verdict itself): it was
  returned `open` with the reason "CHECK verdict pending" and is closed by this ACT verdict, not counted among
  the 14 binding-met rows.
- CHECK re-gathered from the cycle's own artifacts only: no new `B`/`F`/`T` run, no tree change, no receipt edit;
  no loop-back, no replan, no new attempt.

### Final numbers

| metric | baseline (pinned) | final (post-`F`, `T` model) | delta |
| --- | --- | --- | --- |
| branch covered/total | **25807/38281** (67.4 %) | **26199/38317** (68.3 %) | **+392** |
| line covered/total | **45738/59676** (76.6 %) | **46022/59713** (77.0 %) | **+284** |
| floor | +250 counted branches (target 26057/38281) | +392 raw / **+356 conservative** | **met**: margin **+142 raw / +106 conservative** |
| denominators | 38281 branches / 59676 lines | 38317 / 59713 (+36 / +37) | explained by the concurrent 08:49:39 dispatch (see deviation (a)); exclusion filter and thresholds unchanged |

- Source of truth: `tests/coverage/integration-report/Summary.txt` (the pinned `T` invocation), reproduced by a
  Clover model on both sides (`rv1/results/coverage-delta.md`, `rv1/inputs/contributions.json`).
- Full suite (`F`, the single green boundary sweep): **4450 passed / 0 failed / 195 skipped / 0 errors /
  107.746 s**, six providers live (in-process SQLite + Testcontainers PostgreSQL, SQL Server, MySQL, MariaDB,
  ClickHouse), skip-budget gate rc 0 with the pinned **195 skips / 37 reasons** multiset.

### Criteria matrix (REQ-COV .. REQ-STATE) - all met

| requirement | status | closure |
| --- | --- | --- |
| REQ-COV | met | 26199 counted covered branches >= floor 26057 (+392 >= +250), one green `F` + `T`, no exclusion and no threshold touched; the denominator clause is adjudicated by accepted deviation (a) |
| REQ-LIVE | met | 4450/0/195 in `F`, six providers live, no `InMemoryDataContext`, FluentAssertions only, class-owned marker schemas, guards green, skip pin unchanged |
| REQ-QUALITY | met | every whole-solution `B` rc 0 / 0 warnings / 0 errors, no `NoWarn` or suppression added, 0 product edits by this cycle |
| REQ-BOUNDARY | met | exactly ONE boundary sweep `F` on the frozen tree (never retried), inner runs inside the frozen scope, `journal` rc 0, `project`/`report` rc 2 solely with the accepted reason |
| REQ-DOCS | met | only `AGENTS.md:75` + `.github/workflows/dotnet.yml:29-32` (numbers only); thresholds 85/75/65/75 unchanged; no `docs/**` or `docs/ru/**` edit by this cycle; CRLF; 100 pre-existing entries preserved; no commit/push |
| REQ-DEFECTS | met | 6 defect keys with applied-fix count 0, evidence pointers, last repetition and outcome; issue filing verified with `gh`; green `F`, so no red-sweep loop-back |
| REQ-STATE | met | durable `Current cycle 4` / `Plan revision r1` / `Contract revision rv1` / `Attempt n 1/3` plus the defect history read from this file across dispatches; scope sealed before the first edit (E01) |

### Counted delta attribution (top files; exact partition)

- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` **+292**, `src/nextorm.core/DataContext/SqlSourceRenderer.cs`
  **+19**, `src/nextorm.core/MemberInfoExtensions.cs` **+17**, `src/nextorm.core/Visitors/MemberTranslator.cs`
  **+13**, `src/nextorm.core/Visitors/NavigationExpansion.cs` **+12**,
  `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs` **+7**.
- Exact partition: positive **+393** / negative **-1** = **+392** (the -1 is `IEqualityComparerExtensions`,
  total unchanged at 48 - deviation (a)).

### New facts

- **11 new integration test classes, 50 `[Fact]` items** (11 + 5 + 9 + 5 + 5 + 3 + 3 + 2 + 2 + 2 + 3):
  `PreparerBatteryBranchCoverage4Tests` (11), `PreparationDifferentialBranchCoverage4Tests` (5),
  `PlanEqualityDifferentialBranchCoverage4Tests` (9), `SourceRenderingDifferentialBranchCoverage4Tests` (5),
  `EagerGraphBranchCoverage4Tests` (5), `SqlServerPreparationDifferentialBranchCoverage4Tests` (3),
  `ClickHousePreparationDifferentialBranchCoverage4Tests` (3), `SqlServerSourceRenderingBranchCoverage4Tests` (2),
  `PostgresSourceRenderingBranchCoverage4Tests` (2), `PostgresPreparationDifferentialBranchCoverage4Tests` (2),
  `CorrelatedAggregateBranchCoverage4Tests` (3). **0 product edits.**

### Filed defects

- **#303** CREATE - aggregate terminal in a predicate (`c4-builder-aggregate-terminal-in-predicate`).
- **#304** CREATE - correlated reference-hop navigation scope (`c4-correlated-reference-hop-navigation-scope`).
- **#296** duplicate comment **6245668235** - typed null cast in a correlated predicate
  (`c4-typed-null-cast-in-correlated-predicate`).
- 2 `not-a-defect` with probes (`c4-clickhouse-twin-descriptor-mismatch`, `c4-reference-adapter-not-rejected`)
  and 1 observation (`c4-iequalitycomparerextensions-branch-loss`); the three filed records (#303, #304, the
  #296 comment) carry milestone **`1.0.9-rc4`**; applied fixes **0** for every key, verified with
  `gh issue view` / `gh api` (`rv1/results/issue-verify.log`).

### Accepted deviations (verbatim, not hidden)

- (a) **Denominator drift +36/+37 and the `IEqualityComparerExtensions` -1 branch** - six product files were
  modified in the shared working tree at `2026-10-18 08:49:39 +0500` by a concurrent dispatch (after the pinned
  baseline build, before `F`): branches 38281 -> **38317** (+36), lines 59676 -> **59713** (+37), methods
  8189 -> 8191 (+2). Raw delta **+392**; subtracting the whole +36 leaves **+356** test-attributable, still
  above the +250 floor. The per-file partition is exact (+393/-1 = +392) but the literal "0 files and 0 classes
  losing a branch" clause is FALSE for `IEqualityComparerExtensions` (1 branch, total unchanged at 48).
  Recorded here and in `rv1/results/coverage-delta.md`; no exclusion broadened, no threshold lowered.
- (b) **Journal `inner` -> `boundary` reclassification of the 10 whole-solution `B` builds** - authorised repair
  (orchestrator decision quoted in the Notice above): only `events[i].phase` (19 fields) and `events[i].note`
  changed on 20 event records; argv, timestamps, exit codes, logs, revisions, kinds, event count and order
  untouched. Consequence: `journal` rc 0 and `project`/`report` report exactly the accepted reason.
- (c) **`project` / `report` rc 2 solely `FAIL: multiple boundary solution builds: 11`** - the accepted reason
  (10 repaired `B` builds + the already-`boundary` `boundary-build`); recorded, never retried, nothing waived.
  `inputs/report.json` is the module's own mechanical `project_journal` projection (50 executions, byte-identical
  to a fresh projection), never hand-edited.
- (d) **Shared `pd4_line` table name** between `PreparationDifferentialBranchCoverage4Tests` and
  `PlanEqualityDifferentialBranchCoverage4Tests` under the serialised `[Collection("Sqlite")]` with
  DROP-before-CREATE in each class constructor -> 0 interference observed in `F` (4450/0).
- (e) **Pre-existing `AGENTS.md` / `.github/workflows/dotnet.yml` hunks preserved** (the `.pdca/` workspace bullet
  and the `.pdca/scripts/**` path rewrites): cycle 4 changed only the two measured-baseline numbers
  (67.4 %/76.6 % -> 68.3 %/77.0 %); nothing pre-existing was reverted or re-attributed; CRLF preserved.
- (f) **`PreparationDifferential` 4 -> 5 `[Fact]` log reconciliation** - `d1-differential-run2.log` records
  Total 4, `d1-PreparationDifferentialBranchCoverage4Tests.log` records Total 5; the late
  `ForJsonDifferential_DocumentClause...` fact was added after the 4-item run and the frozen-tree class run is
  the 5-item green run. The append-only logs are left as captured.

### Residual / next plan inputs

- The integration branch gate **stays RED** at 68.3 %. On the pinned denominator, 75 % needs **28711** of 38281,
  so the residual is **2512 branch points** (28711 - 26199); on the current denominator 38317 the 75 % target is
  28738, i.e. **2539** residual. The gate is not made green by any threshold or exclusion change.
- Remaining uncovered (raw cobertura targeting view, not a counted claim): `QueryCommand.QueryPreparer` 620 of
  2360 (now ~328 after +292), `CorrelatedQueryExpressionVisitor` 307/726, `EntityBuilder` 365/1056,
  `SqlBuilder` 279/1063, `QueryPlanEqualityComparer` 202/436, `ExpressionPlanEqualityComparer` 158/375,
  `SqlSourceRenderer` 136/518 (now ~117), `EntityBuilderEagerLoading` 47/242.
- The deferred levers with activation triggers live in `rv1/inputs/deferred.json` (**17 deferred rows**, every
  one with an activation trigger; 13 partial + 21 closed rows are also recorded there). Product defects #303 and
  #304 are open and need separate scoped cycles.

### Transferable lessons

1. The **battery + differential** pattern (A->A->B->B->A on ONE context) remains the highest-yield lever:
   `QueryPreparer` alone accounts for **+292** of the +392 counted branches.
2. Container/provider shapes must be tied to an **existing passing neighbour fact** or they die at the boundary:
   D1's dropped `Sample` / `TableSample` / sub-query hints and D5's dropped sub-scenarios were correctly
   deferred instead of gambled.
3. Two structurally-equal descriptors were **NOT reproducible**, so a red one-shot probe is a candidate, not a
   defect - confirm before filing (`c4-clickhouse-twin-descriptor-mismatch` never reproduced).
4. Journal phase classification (`B` at `boundary`) must be **written correctly at capture time**, not repaired
   later: the repair was authorised and audited, but it cost a full re-freeze.
5. A **concurrent dispatch can move coverage denominators mid-cycle**: snapshot the tree and attribute the delta
   before claiming a gain (this cycle's +36/+37 drift and -1 branch).

### Progress log - ACT dispatch (continued)

2026-10-18T04:47:00Z | ACT | revision r1 | iteration n 1/3 | CHECK verdict recorded: PASS on plan r1 / attempt n 1/3 / contract rv1; 14/15 binding rows met (R01..R07, V01..V05, E01, E02), E03 classified diagnostic_nonbinding ("CHECK verdict pending"), 0 blocking findings, 5 accepted non-blocking findings; verdict file written from the CHECK result (no new B/F/T, no tree change, no receipt edit) | rv1/results/check-verdict.md
2026-10-18T04:48:00Z | ACT | revision r1 | iteration n 1/3 | E03 closed in the contract: status open -> met, evidence = verdict {kind:verdict, path:rv1/results/check-verdict.md, value:PASS} + log validator-manifest.txt (manifest rc 0, required_rows exact); 15/15 rows met, 0 open | rv1/inputs/contract.json
2026-10-18T04:49:00Z | ACT | revision r1 | iteration n 1/3 | evidence re-freeze: generate run 1 rc 0 (new generation 39f17a170386b3584075220460d4278cd76a1f615050651cdb08f6d4335ec9ee, tree unchanged 5e6078a0f6e890b6061592df3a8fa00bd6cce6e7); lint run 3 rc 2 with exactly 15 duplicate-row-id diagnostics naming the superseded generation 400006e1 -> copied aside byte-identically as text to inputs/superseded-generation-400006e1/ (15 receipts + NOTE) and removed -> generate run 2 rc 0, lint run 4 rc 0 | rv1/results/generate.log, rv1/results/lint.log, rv1/inputs/superseded-generation-400006e1/
2026-10-18T04:49:30Z | ACT | revision r1 | iteration n 1/3 | validator rc set: generate 0 / lint 0 / manifest 0 / journal 0 / brief 0 / project 2 / report 2, the two rc 2 solely `FAIL: multiple boundary solution builds: 11` (accepted reason, recorded not retried); inputs/report.json byte-identical to a fresh mechanical project_journal projection (50 executions, sha256 65175796b4497898962e75e47b9eac78f0ef5498cfbc2c509f76f3cefce65136) | rv1/results/validator-act.txt, rv1/results/validator-manifest.txt, rv1/results/validator-journal.txt, rv1/results/validator-brief.txt, rv1/results/validator-project.txt, rv1/results/validator-report.txt
2026-10-18T04:50:30Z | ACT | revision r1 | iteration n 1/3 | finalization verification: HEAD 1b140d23cd12ecc19af4bfbad3563b6e8281aa63 unchanged; git diff --name-only -- src/ = the six concurrent-dispatch files only (0 cycle-authored src change); docs/ + docs/ru/ untouched by this cycle (pre-existing/concurrent entries only); 11 new test classes + .pdca/** present; 100 pre-existing entries preserved (0 removals / 0 status changes); CRLF on AGENTS.md and dotnet.yml; MIN_LINE_COVERAGE=85 / MIN_BRANCH_COVERAGE=75 / MIN_INTEGRATION_LINE_COVERAGE=65 / MIN_INTEGRATION_BRANCH_COVERAGE=75 unchanged; NO COMMIT, NO PUSH | rv1/results/tree-preserved-final.txt
2026-10-18T04:51:00Z | ACT | revision r1 | iteration n 1/3 | ACT closed (EXIT): Status CLOSED, Phase `ACT closed (EXIT)`; durable state unchanged (Current cycle 4, Plan revision r1, Contract revision rv1, Attempt n 1/3 - no replan, no rejected candidate); defect history retained (6 keys, applied fixes 0); gate stays RED with residual 2512 (pinned denominator) / 2539 (current 38317); no commit and no push | .pdca/status/integration-branch-coverage-4.md
