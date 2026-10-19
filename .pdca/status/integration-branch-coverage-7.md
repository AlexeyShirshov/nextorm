# integration-branch-coverage-7

Status: CLOSED
Current cycle: 7
Plan revision: r2
Contract revision: rv2
Attempt: n 2/3
Phase: ACT closed (EXIT)

## Goal

Raise the counted integration-suite branch coverage by **+250** covered branches on the
unchanged denominator **38317** (baseline 26877 -> target **27127**), using tests that execute
against REAL databases only. Thresholds stay byte-identical at `MIN_LINE_COVERAGE=85`,
`MIN_BRANCH_COVERAGE=75`, `MIN_INTEGRATION_LINE_COVERAGE=65`,
`MIN_INTEGRATION_BRANCH_COVERAGE=75`. No denominator edit, no exclusion-filter edit, no
InMemory substitution, no product-code change. The five protected src files' coverage delta
is never credited to this cycle. Proven-unreachable arms are recorded with the exact
observation and never asserted as passing.

## Baseline (pinned, cycle-6 r2 T247-excluded counted model)

| metric | value |
|--------|-------|
| branch covered/total | 26877/38317 (70.1 %) |
| line covered/total | 46560/59713 (78.0 %) |
| methods total | 8191 |
| full integration suite | 4637 discovered / 4442 passed / 0 failed / 195 skipped / 37 skip reasons |
| residual to 75 % | 1861 branches (75 % = 28738/38317) |
| working tree | HEAD `6c9dd49b`, `git diff --stat -- src` empty, 28 `git status --short` entries (29 with this status file) |
| protected files | 5/5 sha256 recorded in `rv1/protected.sha256`, `sha256sum -c` rc 0 |

Source of truth: `rv1/results/baseline-verify.txt` over
`.pdca/integration-branch-coverage-6/rv1/results/final2-Summary.txt`
(sha256 `9c8efe86…`) and `.pdca/integration-branch-coverage-6/rv1/logs/F2.log`
(sha256 `10c1a6d3…`). Literal Summary 26924/38381, 46714/59908, 5670/8213; excluding the
four concurrent #247 files (-47 branch, -154 line, -22 method denominators) restores
38317/59713/8191 exactly. Observed drift at D0: HEAD moved from cycle-6 ACT `6069fac7` to
`6c9dd49b`; `git diff --stat -- src` is empty, so no src drift is present at baseline.

## Acceptance criteria

| id | requirement | negative case |
|----|-------------|---------------|
| REQ-COV | counted covered branches >= 27127 on the unchanged 38317 denominator after exactly ONE green boundary F then T; the protected five files' delta excluded | raw-cobertura gain, denominator or exclusion-filter edit, protected-file delta credited, partial/red sweep presented as green |
| REQ-LIVE | every new fact executes against a real DBMS (file SQLite or a Testcontainers provider); no InMemoryDataContext; provider shapes asserted on their own engine; FluentAssertions only | translation-only or vacuous fact, InMemory substitution, ClickHouse shape asserted on SQLite |
| REQ-QUALITY | `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors; CRLF; no suppression/NoWarn; guard 2/2; no threshold, denominator, exclusion or product edit | disabling/suppressing a warning, editing production code to make a fact pass |
| REQ-BOUNDARY | exactly ONE F with the fixed `dotnet-coverage collect` command and exactly ONE T with the fixed `reportgenerator` command | provider skipped, second sweep without a planner revision, broadened inner scope |
| REQ-DOCS | only measured numbers in `AGENTS.md` and `.github/workflows/dotnet.yml`; thresholds 85/75/65/75 byte-identical; no public docs link to specs | threshold byte change, projection stated as measured, `docs/**` link to `docs/specs/**` |
| REQ-DEFECTS | each defect has a stable key, a live-DB minimal repro, a deduplicated search log, a verified issue URL and an applied-fix count; milestone `1.0.9-rc4` | duplicate ticket, no repro, failing fact silently dropped |
| REQ-STATE | durable state (`cycle`, `plan_revision`, `attempt`, phase) and defect history survive restart; scope sealed before the first test edit; contract complete (R01..R07, E01..E03, V01..V16) | state reconstructed from a dispatch, scope amended after edits |

## Tasks (round r1)

| id | owner | files (PLANNED) | targets | provider | forecast | fact cap |
|----|-------|-----------------|---------|----------|----------|----------|
| D0 | coder | status, `rv1/{inputs,results,inner,logs,variants}`, D0.brief.json, protected sha256, baseline, reachability map | state sealed before any test edit; validators CLI confirmed; 5 protected sha256; baseline reproduced | none | 0 | 0 |
| D1 | coder | `tests/nextorm.integration.tests/Sqlite/Cycle7ExpressionCacheTests.cs` | ExpressionPlanEqualityComparer Compare2 :311, CompareSwitchCaseList :642, CompareCatchBlockList :671; equal-prefix one-field differential pairs on one context | SQLite | +70 | 30 (28 planned) |
| D2 | coder | `tests/nextorm.integration.tests/Coverage7/Cycle7ClickHouseDialectTests.cs` | ClickHouseDialect MakeDictionaryFunction :377, MakeQualifiedTableName :535, MakeTrim :577, MakeOrdinal :595, MakeRegexMatch :602, MakeRegexReplace :606, MakeNow :610 | ClickHouse | +90 | 26 (24 planned) |
| D3 | coder | `tests/nextorm.integration.tests/Coverage7/Cycle7MySqlDialectTests.cs` | MySqlDialect MakeQualifiedTableName :368, MakeFullText :194, MakeNow :492, MatchesSelect :153, IsIdentifierChar :162 | MySQL | +55 | 20 (18 planned) |
| D4 | coder | `tests/nextorm.integration.tests/Sqlite/Cycle7CompositionTests.cs` | EntityBuilder UnsupportedChildShapes :1208, CreatePairCommand :1528, get_HasCompositionState :2433, AliasRoot :3278, IsPlainScalarScope :1300 | SQLite | +65 | 26 (22 planned) |
| D5 | coder | `tests/nextorm.integration.tests/Coverage7/Cycle7SqlAssemblyTests.cs` | SqlBuilder MakeSelect :36, MakeExtremeRowOuter :1273, MakeUpdateJoin :1696, MakeDeleteJoin :1582, MakeNamedWindow :2115 | SQLite SELECT/windows, MySQL joined DML | +40 | 18 (16 planned) |
| D6 | coder | boundary evidence, manifest, measurement, attribution, docs, defect/state ledgers | build, guard, ONE F+T, counted delta, protected-excluded attribution | all providers | 0 | 0 |

Fallbacks: D1 QueryPlanEqualityComparer WindowsEqual :249 / EqualsCore :67 (not
GroupingSetsEqual); D2 MakeResultColumnType :447, MakeTypeName :800, WrapTableFunction :463;
D3 MakeGrouping, MakeDurationType, MakeRegexReplace, MakeIsJson; D4
CorrelatedQueryExpressionVisitor TryResolveReferenceAccess :1308, VisitBinary :1684,
TryLowerReferenceScalar :994; D5 SqlSourceRenderer MakeFrom :433, MakeJoin :187,
MakeTableFunction :754, MakeWithClause :23. MySQL FULLTEXT needs its own index+data; #309
shapes are never asserted.

## Fact budget

108 facts planned, hard cap **120**. Per-unit caps: D1 30, D2 26, D3 20, D4 26, D5 18. Any
overflow requires a planner revision; facts that cannot reach their planned arm are recorded
in `rv1/inputs/deferred.json` with the exact observation and do not consume a passing slot.

## Test strategy

- Inner loop uses only `-class NextORM.Integration.Tests.<Class>`; no whole-project or
  whole-solution test runs inside DO.
- SQLite inner:
  `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -class NextORM.Integration.Tests.<Class>`.
- Provider inner:
  `env -u NEXTORM_SKIP_PROVIDERS DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -class NextORM.Integration.Tests.<Class>`.
- Rebuild with `dotnet build nextorm.slnx -c Debug` after any `.cs` edit; never test stale
  assemblies.
- Exactly ONE boundary F+T at DO->CHECK with the fixed commands (see Boundary policy).

## Variant matrix

| id | unit | scenario |
|----|------|----------|
| V01 | D1 | equal-prefix single-operand differential pairs; SQLite; Compare2 primary, EqualsCore fallback |
| V02 | D1 | optional child null/non-null, lengths 0/1/2, first/last element difference |
| V03 | D1 | switch/catch lists and order |
| V04 | D1 | residual WindowsEqual window-frame boundaries/exclusion flags, no duplicates of cycle-6 pairs |
| V05 | D2 | ClickHouse dictionary hit/miss/default |
| V06 | D2 | ClickHouse table qualification and quoted identifiers |
| V07 | D2 | trim sides/chars and ordinal bounds |
| V08 | D2 | regex match/no-match/replacement and now |
| V09 | D3 | MySQL qualified identifiers, SELECT recognizer bounds, identifier chars |
| V10 | D3 | MySQL FULLTEXT hit/miss and now (no wall-clock equality assertion) |
| V11 | D4 | plain/composed scalar scope and unsupported child shape |
| V12 | D4 | reference present/absent and aliases |
| V13 | D5 | SELECT source/no-source and extreme-row ties/empty |
| V14 | D5 | joined update/delete matched/unmatched with exact affected data |
| V15 | D5 | named windows, table functions and CTE supported forms only |
| V16 | all | null/default, nullable, value/reference, empty/one/many, flags on/off distributed in V01-V15; forbidden combos asserted as guard |

Every row closes as test, guard, or deferred+trigger.

## Boundary policy

Exactly ONE acceptance F + T at DO->CHECK, with the commands fixed below. A red F is
preserved as evidence and never retried without a planner revision. The F command is:
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o tests/coverage/integration.cobertura.xml "dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -notrait Category=Diagnostic"`.
The T command is:
`dotnet tool run reportgenerator -reports:tests/coverage/integration.cobertura.xml -targetdir:tests/coverage/integration-report -reporttypes:"Html;TextSummary" -riskhotspotassemblyfilters:"+nextorm.*" -classfilters:"-NextORM.Core.InMemory*"`.
A green F below 27127 returns to PLAN with measured numbers; thresholds, denominators and
exclusions are never adjusted.

## Evidence contract rv2

Row texts are the rv1 contract carried into rv2 unchanged; the only rv2 delta is **V04 `na`**
(append-only disposition, see "Accepted deviations" (c)). The machine-readable contract is
`rv2/inputs/contract.json` (`contract_rv: 2`, 26 rows, 25 `met` + 1 `na`).

| row | requirement | owner | verifier | artifact |
|-----|-------------|-------|----------|----------|
| R01 | counted branch delta >= 250 on 38317, protected-excluded | coder | check | `rv1/results/coverage-delta.md`, `rv1/results/final-Summary.txt`, `rv1/logs/T.log` |
| R02 | live DB matrix, no InMemory, skip budget | coder | check | `rv1/inner/*.report.json`, `rv1/inputs/skip-ledger.json` |
| R03 | build 0/0, CRLF, FA only, guard 2/2 | coder | check | `rv1/logs/B-final.log`, `rv1/logs/G-final.log`, `rv1/results/tree-d6.txt` |
| R04 | exactly ONE fixed F + T | coder | check | `rv1/logs/F.log`, `rv1/logs/T.log`, `rv1/results/boundary-preflight.log` |
| R05 | measured docs numbers, thresholds byte-identical | coder | check | `rv1/results/threshold-diff.txt`, `rv1/results/docs-baseline.diff` |
| R06 | defect keys, live repro, dedup, milestone, fix count | coder | check | `rv1/inputs/defects.json` |
| R07 | durable state and complete contract | coder | check | `.pdca/status/integration-branch-coverage-7.md`, `rv1/inputs/contract.json`, `rv1/manifest.json` |
| E01 | protected 5 sha256 and tree snapshot | coder | check | `rv1/protected.sha256`, `rv1/results/tree-d0.txt`, `rv1/results/tree-d6.txt` |
| E02 | protected-excluded attribution | coder | check | `rv1/results/coverage-delta.md` |
| E03 | manifest, fact and skip inventories | coder | check | `rv1/manifest.json`, `rv1/inputs/facts.json`, `rv1/inputs/skip-ledger.json` |
| V01 | equal-prefix single-operand pairs | coder | check | `rv1/results/v01.txt` |
| V02 | optional child lengths/presence/order | coder | check | `rv1/results/v02.txt` |
| V03 | switch/catch lists and order | coder | check | `rv1/results/v03.txt` |
| V04 | window frame boundaries/exclusion | coder | check | `rv1/results/v04.txt` |
| V05 | ClickHouse dictionary hit/miss/default | coder | check | `rv1/results/v05.txt` |
| V06 | ClickHouse qualification/quoting | coder | check | `rv1/results/v06.txt` |
| V07 | trim sides/chars + ordinal bounds | coder | check | `rv1/results/v07.txt` |
| V08 | regex match/replace + now | coder | check | `rv1/results/v08.txt` |
| V09 | MySQL identifiers/recognizer/chars | coder | check | `rv1/results/v09.txt` |
| V10 | MySQL FULLTEXT + now | coder | check | `rv1/results/v10.txt` |
| V11 | scalar scope + unsupported child shape | coder | check | `rv1/results/v11.txt` |
| V12 | reference presence + aliases | coder | check | `rv1/results/v12.txt` |
| V13 | SELECT/extreme-row forms | coder | check | `rv1/results/v13.txt` |
| V14 | joined update/delete data | coder | check | `rv1/results/v14.txt` |
| V15 | named windows/table functions/CTE | coder | check | `rv1/results/v15.txt` |
| V16 | cross-cutting value/flag combinations | coder | check | `rv1/results/v16.txt` |

Invocations: coder-run commands plus `.pdca/scripts/validate_inner_loop.py`
(`brief` / `report` / `manifest`) and `.pdca/scripts/pdca_evidence.py`
(`generate` / `lint`); exact argv is captured in `rv1/logs/D0-validators-help.log`.
CHECK re-gather budget: 2 scout calls, 1 offline audit, 0 test/coverage runs.

## Docs plan

D6 writes only measured numbers in `AGENTS.md` and `.github/workflows/dotnet.yml`; the four
threshold lines stay byte-identical. No `docs/**` or `docs/ru/**` edit is planned and no
public page may link to `docs/specs/**`.

## Perf decision

Not needed: tests-only change. The production cached preparation path is exercised, never
changed; no benchmark is run and no performance acceptance applies.

## Recon decision

No general spike: `.pdca/integration-branch-coverage-6/rv1/inputs/targeting.md` plus cycle-6
evidence suffice. Only bounded D0/unit
reachability probes (<= 2 per unit) are allowed.

## Unit execution mode

Sequential units in one working tree; no worktrees, no commits, no push. The 28 pre-existing
tree entries are preserved as found.

## Deferred register (38 rows: 17 carried + 21 cycle-7 observations)

| id | disposition | trigger |
|----|-------------|---------|
| D1-CH-SAMPLE | deferred | a class-owned MergeTree-family table (with a sampling key) that the provider suite may create on ClickHouse |
| D1-SS-INDEX-IGNORE | deferred | a public SQL Server index-ignore form is added; the negative pair over a real index then uses two real indexes |
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
| D1B-GROUPING-SETS-SQLITE | deferred | a SQLite build with grouping-set support, or a provider-neutral live form rendered without the ANSI extension |
| D3-CH-LEGACY-WITHRECURSIVE | deferred | ClickHouse starts gating the legacy WithRecursive declaration, or the legacy form is removed in favour of AsRecursiveCte |

#303, #304, #307, #308 and #309 remain open blockers; no fact in this revision asserts
behaviour behind them.

### Cycle-7 unreachable / not-authored observations (21 new rows)

Machine-readable register: `rv2/inputs/deferred.json` (**38 rows** = 17 carried verbatim + 21 new).
Each new row carries the exact `file:line` observation from the unit arms file; none is asserted
as passing and none consumes a fact slot.

| id | unit | observation (source arm) | trigger |
|----|------|--------------------------|---------|
| D1-LAMBDA-PARAM-MISMATCH | D1 | `CompareLambda` parameter count :396 / type :407; `Equals` rejects the differing delegate `Type` first and `Expression.Lambda` validates arity/types | a public construction path or comparer entry point accepting differing delegate types |
| D1-COMPARE2-DEFAULT-THROW | D1 | `Compare2` default throw :341; every standard `ExpressionType` maps to an arm; no public unhandled non-Extension node | a new `ExpressionType`, or a public API producing an unhandled node |
| D1-NULL-CHILD-LISTS | D1 | null-list arms :502, :647/:676, :554/:619; standard child lists are non-null empty collections, null top-level rejected in `Equals` | a custom extension/deserialization path leaving a child list null |
| D1-BINDING-DEFAULT-THROW | D1 | `CompareBinding` default throw :598; `MemberBindingType` has exactly three values | `MemberBindingType` gains a fourth value |
| D1-SWITCH-COMPARISON-NONNULL | D1 | `CompareSwitch` non-null `Comparison` :475; only null/null exercised | a translator emits a `SwitchExpression` with a comparison method |
| D1-VISITOR-EXTENSION-HASH | D1 | `Visitor.VisitExtension` :1019 via `GetHashCode` avoided (re-enters dispatch); `Compare2` Extension arm covered by `Equals` | extension dispatch becomes re-entrancy-safe, or a non-recursive hash route |
| D2-CH-DICT-DEFAULT | D2 | `MakeDictionaryFunction` `_ => name` :390; only six fixed literals reach it | a seventh public dictionary function |
| D2-CH-QUALIFIED-SERVER | D2 | `MakeQualifiedTableName` `server is not null` :536; `SqlSourceRenderer.MakeFrom` :496 rejects `WithServer` first (`SupportsLinkedServer == false`) | `SupportsLinkedServer` true, or a route bypassing `MakeFrom` |
| D3-MYSQL-TRUNCATED-SELECT | D3 | `MatchesSelect` `index + keyword.Length > sql.Length` :155; no query renders a truncated `select` tail | a rendering path emits a truncated trailing keyword |
| D3-MYSQL-QUOTED-SCAN | D3 | `RenderQueryHints` doubled-quote arm :134; `TryEmitValue` :783 never doubles an embedded quote (`D3-run-1.log` syntax error) | literal emission starts doubling quotes, or a raw hint string passes through |
| D3-MYSQL-HINT-TRAILING-RETURN | D3 | `RenderQueryHints` trailing `return sql` :148; every hinted statement has a top-level `select` | a hinted statement without a top-level `select` becomes renderable |
| D4-CHILD-PAGING-LOCK | D4 | `UnsupportedChildShapes` Limit/Offset/Page :1213; `HasPerParentTopN` :572 routes a paged child away first (extends carried `D5-CHILD-PAGING-GUARD`, `duplicate_of` recorded) | a nested paging form both the per-parent source and the single-query fold reject |
| D4-CHILD-JOIN-SOURCE-LOCK | D4 | second disjunct of `ForUpdate/ForShare OF` :1259; a child factory must be `EntityBuilder<TChild>`, `Join` returns `JoinedEntityBuilder` (CS0029) | a `Join`-derived builder accepted as child factory |
| D4-PAIRCMD-INMEMORY | D4 | `CreatePairCommand` `if (!isSql)` :1565-1599 is the in-memory engine; banned in the integration project | an in-memory pair path assertable in a unit project, or the ban lifted |
| D4-PAIRCMD-SOURCEENTITYTYPE-JOINSNULL | D4 | `_sourceEntityType` :1554 (ArrayJoinElement projection has no collection member) and `joins is null` :1556/:1674 | such a projection exists, or a call site omits joins |
| D4-HASCOMPOSITION-RAW-OPERANDS | D4 | `HasCompositionState` raw-source precondition :2412-2418 rejects first: `_query`, `_joins`, `_joinIntos`, `_loadSpecs`, `_eagerLoadMode`, `_tableExpression`, `_sourceEntityType`, `_bindArrayJoinElement` | `BindEntity` accepts a builder carrying one of those states |
| D4-ALIASROOT-INMEMORY-JOININTOS | D4 | in-memory arm :3281 and the `_joinIntos` side of :3285 (`_joinIntos` implies `_joins`; no public `AliasRoot` on `JoinedEntityBuilder`) | an in-memory `AliasRoot` route, or `AliasRoot` with joinIntos state |
| D4-ALIASROOT-SORTING-SHAPE | D4 | non-single-parameter sorting shapes :3381; no public builder writes one | a public sorting overload accepts such a shape |
| D4-CORRELATED-INMEMORY-REFNAV | D4 | `TryResolveReferenceAccess` :1308, `TryLowerReferenceScalar` :994, reference-null :1286 all `IsInMemory`-gated (:54, :997, :1691, :1701, :1712, :1791) | a live non-in-memory path reaches them, or a unit-project assertion is admitted |
| D5-NAMED-WINDOW-GROUPS-REJECT | D5 | `MakeNamedWindow` GROUPS rejection :2171; SQLite is GROUPS-capable (positive frame exercised) | a provider rejecting GROUPS frames |
| D5-NAMED-WINDOW-EXCLUSION-SQLITE | D5 | `MakeNamedWindow` EXCLUSION rejection :2174; SQLite is EXCLUSION-capable | a provider rejecting EXCLUSION frames |

Not deferred (recorded only, no row): the MySQL `MakeQualifiedTableName` server arm is
**covered** by a direct `Dialect.MakeQualifiedTableName` call — only the public `.WithServer`
route is rejected earlier; SQLite FULL JOIN rejection is **covered on MySQL** because SQLite
supports FULL JOIN. Both observations stay in `rv1/inputs/mysql7-arms.md` and
`rv1/inputs/sqlassembly7-arms.md`.

## Risks

- Residual arm yield: the +250 floor is met only if the five units land near forecast; a
  shortfall returns to PLAN with measured numbers, it is never padded with raw coverage.
- D2 dictionary/FULLTEXT setup (D3) needs provider-specific indexes and data; a container
  timeout stops that unit for PLAN and is never retried inside DO.
- Overlap with cycle-6 coverage: facts must target still-uncovered arms; duplicate pairs add
  no delta and are replaced, not counted.
- Stale assemblies: every `.cs` edit requires a rebuild before the filtered run.
- Provider cleanup timeouts are environmental and never filed as product defects.

## Defect history

| key | class | status | filed | duplicates | applied fixes | evidence | last occurrence |
|-----|-------|--------|-------|------------|---------------|----------|-----------------|
| CH-CLEANUP-TIMEOUT | environmental (ClickHouse container cleanup timeout, HttpClient 120 s in DropMarker) — not a product defect | carried from cycle 6, not observed in cycle 7 (absent from the r1 F, the r2 isolation run and the r2 green F) | 0 | 0 | 0 | `.pdca/integration-branch-coverage-6/rv1/logs/F.log`, `F2.log` | cycle 6 r1/n1 |
| CH-HTTP-TIMEOUT-EXTREME-ROW | environmental (pre-existing ClickHouse `HttpClient` 120 s timeout in `ClickHouseExtremeRowNativeSpecificTests`, not a product defect, not diagnosed) | observed in the ONE r1 boundary F (2 of 4745, exit 1) and in the r2 non-counting isolation run (1 of 35, exit 1); **not repeated** in the r2 green acceptance F (4745 / 0 failed); not filed, not retried | 0 | 0 | 0 | `rv1/logs/D6-F.log` lines 447-506; `rv2/logs/D6b-isolation.log`; `rv2/inputs/defects.json` | cycle 7 r2/n1 (isolation only) |

Blockers **#303 / #304 / #307 / #308 / #309** remain open. The r2 boundary sweep exposed no product defect:
`CH-HTTP-TIMEOUT-EXTREME-ROW` is environmental (two pre-existing `ClickHouseExtremeRowNativeSpecificTests`
HTTP-client timeouts in the r1 red F, one in the r2 isolation run, none in the r2 green F), recorded and not filed;
`CH-CLEANUP-TIMEOUT` did not repeat. `filed: 0`, `duplicates: 0`, `applied_fixes: 0`.

## Durable state (DO/CHECK snapshot, superseded by the ACT canonical block)

```yaml
task: integration-branch-coverage-7
cycle: 7
plan_revision: 2
contract_revision: 2
supersedes: rv1
attempt: n 2/3
attempt_note: contract rows and the generation were sealed at the r2 DO boundary under n 1/3; the CHECK loop-back only incremented the counter (the sole row text change is V04 open -> na, append-only)
phase: CHECK
status: OPEN
r1_boundary: RED (environmental, not credited)
r1_measurement: kept (non-protected counted +295, counted absolute 27285/38317, literal 27350/38407)
r2_isolation: 34/35 passed, 1 pre-existing environmental ClickHouse HttpClient timeout (Total 35 / Passed 34 / Failed 1 / Skipped 0, 130.452 s, exit 1); non-counting, not retried
r2_boundary: GREEN (F exit 0, 4745 total / 4550 passed / 0 failed / 195 skipped / 0 not run / 109.861 s; T exit 0, Summary 27351/38407 branch, 47287/59949 line, 5745/8219 methods)
r2_measurement: counted absolute 27286/38317 branch (71.211 %) and 47105/59713 line (78.886 %); counted non-protected delta +296 (margin +46 over the +250 floor); +159 over target 27127
v04_disposition: na (append-only row edit) - fallback-only row, primary ExpressionPlanEqualityComparer.Compare2 reached, frame/exclusion axes already covered by cycle 6; proof rv1/results/v04.txt + cycle-6 v01/v02; no row promoted to met, contract_rv stays 2
evidence_regeneration: the pre-na publication (manifest + generation 9086710..., 26 receipts) was archived byte-identically under rv2/superseded/ because the lint forbids duplicate row ids across generation dirs; regenerated publication generation ffad52cb..., generate/lint/manifest rc 0 (rv2/logs/D6c-regenerate.log)
baseline:
  branch_covered: 26877
  branch_total: 38317
  branch_pct: 70.1
  line_covered: 46560
  line_total: 59713
  line_pct: 78.0
  methods_total: 8191
  suite_discovered: 4637
  suite_passed: 4442
  suite_failed: 0
  suite_skipped: 195
  skip_reasons: 37
  head: 6c9dd49b
target: 27127
delta_required: 250
residual_to_75pct: 1861
thresholds: [85, 75, 65, 75]
gate_integration: RED
protected_files: 5/5 OK
product_edits: 0
plan_facts: 108
fact_cap: 120
unit_caps: {D1: 30, D2: 26, D3: 20, D4: 26, D5: 18}
commit: false
push: false
evidence:
  contract: rv2/inputs/contract.json
  preflight: rv2/logs/D6b-preflight.log
  isolation: rv2/logs/D6b-isolation.log
  sweep: rv2/logs/D6b-F.log
  report: rv2/logs/D6b-T.log
  summary: rv2/results/final-Summary.txt
  coverage_delta: rv2/results/coverage-delta.md
  contributions: rv2/inputs/contributions.json
  skip_ledger: rv2/inputs/skip-ledger.json
  skip_budget: rv2/results/skip-budget.log
  defects: rv2/inputs/defects.json
  manifest: rv2/manifest.json
  gate_manifest: rv2/results/gate-manifest.txt
  gate_lint: rv2/results/gate-lint.txt
  validator_manifest: rv2/results/validator-manifest.txt
  regenerate_log: rv2/logs/D6c-regenerate.log
  superseded_publication: rv2/superseded/README.md
  test_hygiene: rv2/results/test-hygiene.md
  hygiene_assertion_style: rv2/logs/hygiene-assertion-style.log
  hygiene_guard: rv2/logs/hygiene-guard.log
  docs_diff: rv2/results/docs.diff
  threshold_diff: rv2/results/threshold-diff.txt
  r1_sealed_checksums: rv2/inputs/r1-checksums.txt
  r1_boundary_f: rv2/inputs/r1-F.log
  r1_summary: rv2/inputs/r1-Summary.txt
  r1_cobertura: rv2/inputs/r1-integration.cobertura.xml
  brief: rv1/inner/D0.brief.json
  baseline: rv1/results/baseline-verify.txt
  threshold_before: rv1/results/threshold-before.txt
  tree: rv1/results/tree-d0.txt
  protected: rv1/protected.sha256
  reachability: rv1/inputs/reachability.md
  build: rv1/logs/D6-build.log
  guard: rv1/logs/D6-guard.log
  facts: rv1/inputs/facts.json
  tree_d6: rv1/results/tree-d6.txt
units:
  D0: completed (seal: protected 5/5 sha256, baseline reproduced, validators CLI confirmed)
  D1: completed (28 facts green, live SQLite, primary ExpressionPlanEqualityComparer.Compare2 reached)
  D2: completed (24 facts green, live ClickHouse 25.8)
  D3: completed (18 facts green, live MySQL 8.4)
  D4: completed (23 facts green, live SQLite)
  D5: completed (15 facts green: 10 SQLite + 5 MySQL)
  D6: superseded -> D6b (blocked-by-budget, NOT done: r1 boundary F was RED on 2 pre-existing environmental ClickHouse HttpClient timeouts; the r1 one-F budget was consumed and the red F stays red — the r1 +295 measurement is kept but never credited; no green r1 sweep exists)
  D6b: completed (r2 replacement for D6: preflight rc 0, 1 non-counting isolation 34/35, ONE green acceptance F + T, counted +296, manifest generate/lint/manifest-validator rc 0)
unit_mapping:
  D6: D6b (actual scope replacement per the r2 plan; D6b carries every D6 acceptance criterion plus the r2 evidence, D6 itself is never marked done)
defect_history:
  - key: CH-CLEANUP-TIMEOUT
    class: environmental (ClickHouse container cleanup timeout, HttpClient 120 s in DropMarker) - not a product defect
    observed: cycle-6 r1/n1 only; not repeated in the cycle-7 r1 F, the r2 isolation run or the r2 green F
    status: carried, not observed in cycle 7
    filed: 0
    duplicates: 0
    fixes_applied: 0
    last_occurrence: cycle 6 r1/n1
  - key: CH-HTTP-TIMEOUT-EXTREME-ROW
    class: environmental (pre-existing ClickHouse HttpClient 120 s timeout in ClickHouseExtremeRowNativeSpecificTests; not a product defect, not diagnosed)
    observed: cycle-7 r1 boundary F (2 of 4745, exit 1) and cycle-7 r2 non-counting isolation (1 of 35, exit 1); NOT repeated in the r2 green acceptance F (4745 / 0 failed)
    status: recorded, not filed, not retried
    filed: 0
    duplicates: 0
    fixes_applied: 0
    last_occurrence: cycle 7 r2/n1 (isolation only)
blockers: ["#303", "#304", "#307", "#308", "#309"]
deferred_rows: 17
```

## ACT closure (r2, CHECK PASS at attempt n 2/3)

CHECK returned **PASS** on the r2 contract (7/7 criteria); no loop-back, no plan revision, no
further sweep. ACT only recorded state, closed the deferred register and re-froze the evidence.

## Final numbers

| metric | baseline (pinned) | final counted | note |
|--------|-------------------|---------------|------|
| branch | 26877 / 38317 (70.1 %) | **27286 / 38317 (71.211 %)** | counted model, protected- and #275-excluded |
| literal T branch | — | **27351 / 38407 (71.2 %)** | `rv2/results/final-Summary.txt`, denominator +90 |
| counted delta (non-protected) | — | **+296** | r1's `+295` is **not** added; margin **+46** over the +250 floor |
| over target 27127 | — | **+159** | target = baseline + 250 |
| line | 46560 / 59713 (78.0 %) | **47287 / 59949 (78.886 %)** literal; **47105 / 59713** counted | |
| methods | 8191 total | **5745 / 8219** | 69.8 % |
| counted denominator | 38317 | **38305** | protected-file drift **−12** reconciled (+12 branch / +14 total) |
| file attribution | — | **18 positive / 0 negative** of 25 changed (raw) | protected-5 **+12 branch / +18 line excluded** |
| gate `MIN_INTEGRATION_BRANCH_COVERAGE=75` | — | **RED, 71.2 % < 75 %** | residual to 75 % = **1452** counted (28738 − 27286) |

Suite (r2 acceptance F, fixed command, exit **0**): Total **4745** / Passed **4550** / Failed **0**
/ Skipped **195** / Not Run **0** / **109.861 s**; skip budget 195/37 matches the pinned budget.
Isolation (one non-counting run, exit 1): **35 / 34 / 1** — the single failure is the pre-existing
environmental ClickHouse `HttpClient` timeout, never re-credited and never retried.

### CHECK criteria matrix

| id | verdict | decisive numbers |
|----|---------|------------------|
| REQ-COV | **met** | counted **27286 / 38317** ≥ 27127 (**+159**); non-protected delta **+296** ≥ 250 (margin **+46**); exactly ONE green F (exit 0, 4745/4550/0/195) + ONE T (exit 0); no denominator, exclusion or threshold edit |
| REQ-LIVE | **met** | 108 facts in 6 files, every fact against a real DBMS (file SQLite / ClickHouse 25.8 / MySQL 8.4); 0 `InMemoryDataContext`; 0 `Assert.` (210 `.Should()`); 0 added skips (195 pinned) |
| REQ-QUALITY | **met** | build rc 0, **0 warnings / 0 errors**; guards **2/2** (`AssertionStyleTests`) and **2/2** (`IntegrationSuiteScopingTests`); 6/6 CRLF, 0 LF-only; 0 `#pragma`/`SuppressMessage`/`NoWarn`; `git diff -- src/` empty, protected 5/5, 0 staged |
| REQ-BOUNDARY | **met** | r1: exactly ONE F (RED, preserved, never credited); r2: exactly ONE non-counting isolation + exactly ONE F (GREEN) + exactly ONE T; no third sweep |
| REQ-DOCS | **met** | no docs edit required — `AGENTS.md` already stated 78.9 % line / 71.2 % branch (measured match); thresholds **85 / 75 / 65 / 75 byte-identical**; no public page links to `docs/specs/**` |
| REQ-DEFECTS | **met** | **0 filed / 0 duplicates / 0 applied fixes**; both timeout keys environmental, never filed; blockers #303/#304/#307/#308/#309 untouched |
| REQ-STATE | **met** | cycle 7 / plan **r2** / attempt **n 2/3** / phase preserved; scope sealed before the first test edit; contract rv2 complete (26 rows, V04 `na`); manifest lint rc 0, tree binding `dd684e57…` |

### Facts and product edits

- **108 facts** (cap 120): D1 **28**, D2 **24**, D3 **18**, D4 **23**, D5 **15** (10 SQLite + 5 MySQL)
  in **6 new test files**, 3881 lines, 108 `[Fact]`, 210 `.Should()`.
- **0 product edits**: `git diff --name-only -- src/` empty; **0 staged**; protected **5/5** sha256 OK.
- **No commit, no push**: HEAD at ACT `9ffb8bdc` (concurrent collection commit, not created here).

### Defects

**0 filed / 0 duplicates / 0 applied fixes.** `CH-CLEANUP-TIMEOUT` (cycle 6) did not repeat;
`CH-HTTP-TIMEOUT-EXTREME-ROW` is environmental (two pre-existing ClickHouse `HttpClient` 120 s
timeouts in the r1 red F, one in the r2 non-counting isolation, **none** in the r2 green F) and was
never filed or retried. Blockers `#303 / #304 / #307 / #308 / #309` untouched.

### Accepted deviations

(a) The r1 boundary F was **RED** on two pre-existing ClickHouse `HttpClient` timeouts; it is
preserved and its `+295` is **never re-credited** — the r2 numbers are recomputed from the original
baseline. (b) Exactly **ONE** r2 sweep was authorized by the r2 replan; there is no third sweep.
(c) **V04 `na`** (append-only row edit): fallback-only row, the primary
`ExpressionPlanEqualityComparer.Compare2` was reached, frame/exclusion axes were already covered by
cycle 6 (`rv1/results/v04.txt` + cycle-6 v01/v02); no row was promoted to `met`, `contract_rv`
stays 2. (d) HEAD moved `6069fac7 → adca5cdd` during r2 (concurrent **#275** commits) and further to
`9ffb8bdc` before ACT (concurrent **#297** collection commits) — recorded, not credited; the
footprint is unchanged (`git diff --name-only adca5cdd HEAD -- <footprint>` empty), so the frozen
tree stays `dd684e57…`. The #275-excluded counted delta **+290** is also over the +250 floor.

### Residual / next plan inputs

- Gate remains **RED** at **71.211 %** branch; residual to 75 % = **1452 counted branches**
  (28738 − 27286). Thresholds are not to be lowered.
- Remaining measured levers for a successor cycle: ClickHouse and MySQL dialect residual arms,
  `QueryFilterValidator` 205 uncovered, `EfQueryFilterTranslator` 265, `Postgres` 184,
  `SqlBuilder` / `SqlSourceRenderer` residual, plan-key comparer residuals (the `InMemory` paths
  are excluded by policy).
- Deferred register is now **38 rows** (17 carried + 21 new cycle-7 observations) in
  `rv2/inputs/deferred.json`; the in-memory-gated and non-constructible arms are recorded there,
  never asserted as passing.
- An integration-coverage successor must budget more than one acceptance sweep up front, or pin a
  preflight that guarantees no concurrent environmental timeout consumes the one-F budget.

### Transferable lessons

1. **Provider engine placement matters as much as the route.** Asserting a provider shape on
   SQLite proves nothing; the three provider units only yielded delta because each fact ran on its
   own live engine (ClickHouse/MySQL) and asserted returned values, not rendered SQL.
2. **Public dialect routes exist for 0 % renderers.** `DataContext.Dialect` (and the public
   `SqlFunctions` surface) turned 0 %-covered renderers (`DictionarySqlTranslator` 0/20,
   `MakeDurationType`, `MakeIsJson`) into reachable, live-assertable arms.
3. **Single-sweep budget discipline**: an environmental timeout in the one acceptance F is a
   planning event — the red F is preserved, never retried, and only a planner revision can
   authorize a second sweep.
4. **Environmental vs product defect**: pre-existing ClickHouse HTTP-client timeouts must be
   classified, recorded and passed over; filing them would pollute the defect ledger and the
   dedup history.
5. **Fallback-only rows close as `na`, not `met`**: V04 keeps its primary-path proof and stays
   append-only, so no pass is claimed for an arm the fact did not execute.
6. **Recompute, never add**: the r1 `+295` and the r2 `+296` are alternative measurements of the
   same baseline, not increments; only the protected-excluded recomputation is credited.

### Durable state (ACT, canonical)

```yaml
task: integration-branch-coverage-7
cycle: 7
plan_revision: 2
contract_revision: 2
phase: ACT closed (EXIT)
status: CLOSED
attempt: n 2/3
attempt_note: r2 boundary sealed under n 1/3; CHECK loop-back only incremented the counter (V04 open -> na, append-only); CHECK PASS at n 2/3, no further loop-back
baseline:
  branch_covered: 26877
  branch_total: 38317
  branch_pct: 70.1
  line_covered: 46560
  line_total: 59713
  line_pct: 78.0
  methods_total: 8191
  head: 6c9dd49b
final:
  branch_literal: 27351/38407
  branch_counted: 27286/38317
  branch_pct_counted: 71.211
  branch_counted_delta_nonprotected: 296
  margin_over_floor: 46
  over_target: 159
  line_literal: 47287/59949
  line_pct_literal: 78.886
  line_counted: 47105/59713
  methods: 5745/8219
  counted_denominator: 38305
  denominator_drift: -12
  protected_delta: {branch: 12, line: 18}
  c275_delta: {branch: 6, line: 10}
  positive_files: 18
  negative_files: 0
suite:
  total: 4745
  passed: 4550
  failed: 0
  skipped: 195
  not_run: 0
  seconds: 109.861
  exit: 0
isolation:
  total: 35
  passed: 34
  failed: 1
  seconds: 130.452
  counting: false
  note: pre-existing environmental ClickHouse HttpClient timeout; never retried, never credited
target: 27127
delta_required: 250
residual_to_75pct: 1452
thresholds: [85, 75, 65, 75]
gate_integration: RED
protected_files: 5/5
product_edits: 0
test_files: 6
plan_facts: 108
fact_cap: 120
unit_caps: {D1: 30, D2: 26, D3: 20, D4: 26, D5: 18}
deferred_rows: 38
deferred_breakdown: {carried: 17, cycle7_new: 21}
head_at_act: 9ffb8bdc
head_note: baseline 6c9dd49b -> r2 boundary adca5cdd -> ACT 9ffb8bdc (concurrent #275/#297 collection commits; footprint unchanged, no commit created by this cycle)
commit: false
push: false
units:
  D0: completed
  D1: completed (28 facts, live SQLite)
  D2: completed (24 facts, live ClickHouse 25.8)
  D3: completed (18 facts, live MySQL 8.4)
  D4: completed (23 facts, live SQLite)
  D5: completed (15 facts: 10 SQLite + 5 MySQL)
  D6: superseded -> D6b (r1 red F, one-F budget consumed, never marked done)
  D6b: completed (r2 replacement: preflight rc 0, one isolation, ONE green F + T, counted +296)
unit_mapping:
  D6: D6b
defect_history:
  - key: CH-CLEANUP-TIMEOUT
    class: environmental
    status: carried, not observed in cycle 7
    filed: 0
    duplicates: 0
    fixes_applied: 0
    last_occurrence: cycle 6 r1/n1
  - key: CH-HTTP-TIMEOUT-EXTREME-ROW
    class: environmental
    status: recorded, not filed, not retried
    observed: r1 boundary F (2 of 4745) and r2 non-counting isolation (1 of 35); absent from the r2 green F
    filed: 0
    duplicates: 0
    fixes_applied: 0
    last_occurrence: cycle 7 r2/n1 (isolation only)
blockers: ["#303", "#304", "#307", "#308", "#309"]
evidence:
  manifest: .pdca/integration-branch-coverage-7/rv2/manifest.json
  tree_binding: dd684e5710744a11b3f90bd03e252da4aa891932
  generation: ffad52cb5e7f1aead477b237f6684a20a8d440f522d13ee52a7a0b4217072fc2
  receipts: 26
  act_gate: {generate: 0, lint: 0, manifest: 0}
  act_generate: rv2/results/act-generate.txt
  act_lint: rv2/results/act-lint.txt
  act_manifest: rv2/results/act-manifest.txt
  tree_preserved: rv2/results/tree-preserved-final.txt
  deferred: rv2/inputs/deferred.json
  contribution: rv2/inputs/contributions.json
  coverage_delta: rv2/results/coverage-delta.md
  summary: rv2/results/final-Summary.txt
  sweep: rv2/logs/D6b-F.log
  report: rv2/logs/D6b-T.log
  isolation: rv2/logs/D6b-isolation.log
  test_hygiene: rv2/results/test-hygiene.md
```

## Progress log

2026-10-18T11:40:00Z | PLAN | revision r1 | iteration n 1/3 | plan r1 accepted (amended before DO: mandated F/T command, no audit.py) | .pdca/status/integration-branch-coverage-7.md
2026-10-18T11:45:00Z | DO | revision r1 | iteration n 1/3 | DO started; D0 seal in progress | .pdca/integration-branch-coverage-7/rv1/
2026-10-18T11:55:00Z | DO | revision r1 | iteration n 1/3 | D0 seal complete: rv1 tree created; validators CLI confirmed (brief/report/manifest + generate/lint); D0 brief validated rc 0; protected 5/5 sha256; baseline 26877/38317 branch, 46560/59713 line, 8191 methods, suite 4637/4442/0/195/37 reproduced; thresholds snapshotted; contract + reachability written; HEAD 6c9dd49b, src diff empty | rv1/results/tree-d0.txt, rv1/results/baseline-verify.txt, rv1/results/threshold-before.txt, rv1/inputs/reachability.md
2026-10-18T11:51:47Z | DO | revision r1 | iteration n 1/3 | D1 facts authored: 28 (cap 30); selected 28 / passed 28 / failed 0 / skipped 0 (green run D1-run-3); solution build exit 0, 0 warnings / 0 errors; SQLite-only inner loop; arms reached: Compare2 type switch (incl. Block/Goto/Label/Loop/Invocation/Index/RuntimeVariables/Switch/Try/Extension), CompareSwitchCaseList/CompareSwitchCase, CompareCatchBlockList/CompareCatchBlock, CompareMemberList/CompareMemberBindingList/CompareBinding, CompareElementInit(List), CompareLambda 5-parameter scope spill, Visitor hash arms; not reached/not asserted: lambda arity/type mismatch arms, Compare2 default throw, null-list arms, Switch comparison non-null, VisitExtension hash (recorded in expression-arms.md); report validator rc 0 | .pdca/integration-branch-coverage-7/rv1/logs/D1-build-4.log, .pdca/integration-branch-coverage-7/rv1/logs/D1-run-3.log, .pdca/integration-branch-coverage-7/rv1/inputs/expression-arms.md, .pdca/integration-branch-coverage-7/rv1/inner/D1.report.json, .pdca/integration-branch-coverage-7/rv1/logs/D1-report.log
2026-10-18T12:04:28Z | DO | revision r1 | iteration n 1/3 | D2 facts authored: 24 (cap 26); selected 24 / passed 24 / failed 0 / skipped 0 (green run D2-run-3); affected-project build exit 0, 0 warnings / 0 errors (solution acceptance build D2-build-4 also 0/0); live ClickHouse container; arms reached: MakeDictionaryFunction 6 of 6 names + default unreachable, MakeQualifiedTableName database/schema/both-rejected/bare-name + server arm unreachable, MakeTrim trimBoth/Left/Right, MakeOrdinal false+true, MakeRegexMatch/Replace both (?i) arms, MakeNow now()/now(UTC), MakeResultColumnType plain+nullable, MakeTypeName all 11 arms, WrapTableFunction numbers_mt + seeded generateRandom; report validator rc 0 | .pdca/integration-branch-coverage-7/rv1/logs/D2-build-project.log, .pdca/integration-branch-coverage-7/rv1/logs/D2-run-3.log, .pdca/integration-branch-coverage-7/rv1/inputs/clickhouse7-arms.md, .pdca/integration-branch-coverage-7/rv1/inner/D2.report.json, .pdca/integration-branch-coverage-7/rv1/logs/D2-report.log
2026-10-18T12:16:18Z | DO | revision r1 | iteration n 1/3 | D3 facts authored: 18 (cap 20); selected 18 / passed 18 / failed 0 / skipped 0 (green run D3-run-2, exit 0); affected-project build exit 0, 0 warnings / 0 errors (solution acceptance build D3-build-3 also 0/0); live MySQL 8.4 container, no opt-out; arms reached: MakeQualifiedTableName database-only (live information_schema read) / schema-only / bare-name / database+schema rejected / linked-server rejected (direct dialect call) / quoted special-character name, MakeFullText contains (boolean mode, quicksilver prefix miss) + freetext (natural language), MakeNow now() vs utc_timestamp() separated by a +05:00 session time zone, MatchesSelect+IsIdentifierChar top-level index 0 / CTE depth skip / single-quote scan / backtick scan / underscore guard (my7_select) / trailing-char guard (selects), fallbacks MakeGrouping with-rollup vs plain, MakeDurationType time vs time(3) verified through information_schema.column_type, MakeRegexReplace/MakeRegexMatch both case arms, MakeIsJson predicate vs value context; not reached/not asserted: truncated-select guard, doubled-quote scan arm, RenderQueryHints trailing return (recorded in mysql7-arms.md); report validator rc 0 | .pdca/integration-branch-coverage-7/rv1/logs/D3-build-project.log, .pdca/integration-branch-coverage-7/rv1/logs/D3-build-3.log, .pdca/integration-branch-coverage-7/rv1/logs/D3-run-2.log, .pdca/integration-branch-coverage-7/rv1/inputs/mysql7-arms.md, .pdca/integration-branch-coverage-7/rv1/inner/D3.report.json, .pdca/integration-branch-coverage-7/rv1/logs/D3-report.log
2026-10-18T12:25:12Z | DO | revision r1 | iteration n 1/3 | D4 facts authored: 23 (cap 26); selected 23 / passed 23 / failed 0 / skipped 0 (green run D4-run-4, exit 0); affected-project build exit 0, 0 warnings / 0 errors (solution acceptance build D4-build-6 also 0/0); live SQLite only; arms reached: UnsupportedChildShapes Having/DistinctOn/LimitBy/Final/PreWhere/ArrayJoin/TableSample/Sample/ForSystemTime/ForUpdate-ForShare/Settings/TableHint/IndexHint/ForUpdate-ForShare OF/Cte, CreatePairCommand suppressed cartesian warning + CTE parent, HasCompositionState LimitBy/DistinctOn/PreWhere/ArrayJoin/Settings/GroupingType/GroupingSets/GroupByWithTotals/Final, IsPlainScalarScope plain + condition/order/join/group/limit/final/settings/prewhere/limitby/arrayjoin/sample/rowlock/temporal/in-scope/table-sample/table-hint/index-hint/grouping/raw/derived scopes, AliasRoot derived CREATE root + missing Item1 + condition/having/group/preWhere re-binds, VisitBinary SQL coalesce arm; unreachable recorded: UnsupportedChildShapes paging and join-source-lock disjunct (deferred), CreatePairCommand in-memory block, HasCompositionState raw-unreachable operands, AliasRoot in-memory/joinIntos/sorting arms, CorrelatedQueryExpressionVisitor reference-navigation fallbacks (IsInMemory-gated) | .pdca/integration-branch-coverage-7/rv1/logs/D4-build-6.log, .pdca/integration-branch-coverage-7/rv1/logs/D4-run-4.log, .pdca/integration-branch-coverage-7/rv1/inputs/composition7-arms.md, .pdca/integration-branch-coverage-7/rv1/inner/D4.report.json, .pdca/integration-branch-coverage-7/rv1/logs/D4-report.log
2026-10-18T17:41:00Z | DO | revision r1 | iteration n 1/3 | D5 facts authored: 15 (cap 18; 10 SQLite + 5 MySQL); SQLite selected 10 / passed 10 / failed 0 / skipped 0 (D5-sqlite-run-1, exit 0); MySQL selected 5 / passed 5 / failed 0 / skipped 0 (D5-mysql-run-2, exit 0; D5-mysql-run-1 retained red 4/5 on the RETURNING message, widened to '*cannot return*'); affected-project build exit 0, 0 warnings / 0 errors (D5-build-4; solution acceptance builds D5-build-2/3 also 0/0, D5-build-1 red with 2 CS0833); live SQLite file + live MySQL 8.4 container; arms reached: MakeSelect star/tag via Any/Count, MakeExtremeRowOuter portable All/NULL/grouped rank, MakeNamedWindow GROUPS, MakeTableFunction built-in bare + user-defined joined alias, MakeWithClause two CTEs + legacy MaxRecursion, MakeUpdateJoin matched/unmatched (SQLite and MySQL), MakeDeleteJoin SQLite guard + MySQL matched/unmatched + RETURNING rejection, MakeJoin hint rejection/null-condition/MySQL full-join gate, ApplyInlineHints; unreachable: MakeNamedWindow GROUPS/EXCLUSION rejection (SQLite capable), SQLite FULL JOIN rejection (dialect supports) converted to MySQL; report validator rc 0 | .pdca/integration-branch-coverage-7/rv1/logs/D5-build-4.log, .pdca/integration-branch-coverage-7/rv1/logs/D5-sqlite-run-1.log, .pdca/integration-branch-coverage-7/rv1/logs/D5-mysql-run-2.log, .pdca/integration-branch-coverage-7/rv1/inputs/sqlassembly7-arms.md, .pdca/integration-branch-coverage-7/rv1/inner/D5.report.json, .pdca/integration-branch-coverage-7/rv1/logs/D5-report.log
2026-10-18T17:47:00Z | DO | revision r1 | iteration n 1/3 | D6 boundary complete: build rc 0, 0 Warning(s)/0 Error(s); scoping guard 2/2 rc 0; ONE F rc 1 RED (Total 4745 / Passed 4548 / Failed 2 / Skipped 195 / Not Run 0 / 270.715 s; two pre-existing environmental ClickHouse HTTP timeouts, not retried); ONE T rc 0; Summary 27350/38407 branch, 47286/59949 line, 5745/8219 methods; literal +473 branch / +726 line; protected 5 delta +12 branch / +18 line; concurrent #275 +6/+10; non-protected counted delta +295 (margin +45 over the +250 floor); counted absolute 27285/38317 (71.20 %) and 47104/59713 (78.88 %), +158 over 27127; counted denominator 38305 (drift -12); skip 195/37 matches pinned; docs 78.9 % line / 71.2 % branch, thresholds 85/75/65/75 byte-identical; defects 0 filed (2 environmental); manifest generate/lint/manifest-validator rc 0; 18 positive / 1 negative file, top ExpressionPlanEqualityComparer +104, EntityBuilder +45, ClickHouseDialect +40, MySqlDialect +40, SqlBuilder +23 | rv1/logs/D6-F.log, rv1/logs/D6-T.log, rv1/results/final-Summary.txt, rv1/results/coverage-delta.md, rv1/results/gate-manifest.txt, rv1/results/gate-lint.txt, rv1/results/validator-manifest.txt, rv1/results/tree-d6.txt, rv1/inputs/contract.json, rv1/manifest.json
2026-10-18T12:50:37Z | PLAN | revision r2 | iteration n 1/3 | r1 boundary RED on 2 pre-existing ClickHouse HttpClient timeouts (environmental, not credited); +295 non-protected counted already over the +250 floor; one bounded non-counting isolation run + exactly ONE new acceptance F+T authorized; no third sweep | .pdca/integration-branch-coverage-7/rv2/
2026-10-18T12:57:23Z | DO | revision r2 | iteration n 1/3 | r2 boundary complete: preflight rc 0 (socket ping OK, NEXTORM_SKIP_PROVIDERS unset rc 1, protected 5/5, HEAD adca5cdd, src diff empty, 30 status entries, r1 sealed 3/3); ONE non-counting isolation rc 1 (Total 35 / Passed 34 / Failed 1 / Skipped 0 / 130.452 s, pre-existing environmental ClickHouse HttpClient timeout, not retried); ONE acceptance F rc 0 GREEN (Total 4745 / Passed 4550 / Failed 0 / Skipped 195 / Not Run 0 / 109.861 s; no Cycle7* failure); ONE T rc 0; Summary 27351/38407 branch, 47287/59949 line, 5745/8219 methods; literal vs pinned 26877/38317 = +474 branch / +727 line; raw per-file +308/+16 branch, +380/+25 line, 18 positive / 0 negative / 25 changed; protected 5 +12 branch / +18 line; concurrent #275 +6/+10; counted_delta_nonprotected +296 (margin +46 over the +250 floor); counted absolute 27286/38317 (71.211 %) and 47105/59713 (78.886 %), +159 over 27127; counted denominator 38305 (drift -12); skip 195/37 matches pinned; docs unchanged (78.9/71.2 already current, r2 docs diff empty), thresholds 85/75/65/75 byte-identical; defects 0 filed / 0 duplicates / 0 fixes (1 environmental key); manifest generate/lint/manifest-validator rc 0; r1 boundary artifacts sealed byte-identically | rv2/logs/D6b-preflight.log, rv2/logs/D6b-isolation.log, rv2/logs/D6b-F.log, rv2/logs/D6b-T.log, rv2/results/final-Summary.txt, rv2/results/coverage-delta.md, rv2/inputs/contributions.json, rv2/inputs/skip-ledger.json, rv2/inputs/defects.json, rv2/results/gate-manifest.txt, rv2/results/gate-lint.txt, rv2/results/validator-manifest.txt, rv2/manifest.json
2026-10-18T13:06:51Z | CHECK | revision r2 | iteration n 2/3 | CHECK FAIL loop-back: V04 open binding row -> na disposition with cycle-6 evidence; hygiene audit added; units/defect history synced; no test edits, r2 sweep stands | rv2/results/test-hygiene.md
2026-10-18T13:09:00Z | CHECK | revision r2 | iteration n 2/3 | CHECK PASS 7/7 (REQ-COV: counted 27286/38317, +159 over target 27127, +296 non-protected, margin +46; ONE green F 4745/4550/0/195 + ONE T exit 0); hygiene 0 Assert. / 6/6 CRLF / guards 2+2; defects 0 filed / 0 duplicates / 0 fixes; V04 na stands; no further loop-back | rv2/results/test-hygiene.md, rv2/results/coverage-delta.md, rv2/manifest.json
2026-10-18T13:11:30Z | ACT | revision r2 | iteration n 2/3 | ACT closed (EXIT): deferred register 38 rows (17 carried + 21 cycle-7 observations); tree preserved (HEAD 9ffb8bdc identical to the pre-ACT reference, src diff empty, index empty, protected 5/5 sha256 OK); evidence re-freeze generate/lint/manifest rc 0/0/0, generation ffad52cb unchanged (26 receipts), tree dd684e57 unchanged; no commit, no push | rv2/results/tree-preserved-final.txt, rv2/inputs/deferred.json, rv2/results/act-generate.txt, rv2/results/act-lint.txt, rv2/results/act-manifest.txt
