# Collection rc2-reclaim — CHECK (C) evidence contract

Status artifact for the integrated collection CHECK. Self-contained; all claims either cite a
`file:line` in a per-task status file or a staged raw log under
`artifacts/pdca/collection-rc2-reclaim/C/`. Nothing here re-runs the suite; AC logs are already staged.

## 1. Scope & inputs

- **Collection id:** `rc2-reclaim` — `collection-rc2-reclaim.md:3`.
- **Branch / HEAD:** `1.0.9-rc2` @ `93fb92550b63347b98101903bf9939ef71ce27ea` (`git-state.txt:2`).
- **Mode:** single lane in the **current worktree** — per `collection-rc2-reclaim.md:6`, no group/task
  worktrees, no group branches, no `git merge --no-ff`; integration = the authorized commits on
  `1.0.9-rc2` (`collection-rc2-reclaim.md:24,42`).
- **Terminal DO state:** all 4 tasks DONE (R184, R175, R177, R190) — `collection-rc2-reclaim.md:24,59`.
- **Pre-existing tracked modifications:** the working tree carries 3 tracked modifications
  (`.opencode/skills/nextorm-pdca/SKILL.md`, `AGENTS.md`, `scripts/validate_inner_loop.py`) that are
  **NOT** part of the committed tree @ `93fb9255` (`git-state.txt:3-6`); none is product code. Build/test
  ran on this working tree; the untracked `??` entries in `git-state.txt:7-15` are non-collection status
  files. No product footprint difference is implied by these three files.
- **Inputs:** collection status `docs/specs/status/collection-rc2-reclaim.md`; per-task status files
  `docs/specs/status/rc2-184-eagerloadmode-reclaim-1.md`, `rc2-175-joininto-quality-reclaim-1.md`,
  `rc2-177-native-json-reclaim-1.md`, `rc2-190-reverify-1.md`; raw logs
  `artifacts/pdca/collection-rc2-reclaim/C/`.

## 2. Pinned rv table

`selected_variant` is `pdca-dotnet` for every row (`collection-rc2-reclaim.md:32-35,46`). `rv` = pinned
evidence-contract revision actually used; `r` = plan revision stated by the per-task status file.

| task | selected_variant | rv | r | source status file:`line` |
|---|---|---|---|---|
| R184 | pdca-dotnet | 2 | 1 | `rc2-184-eagerloadmode-reclaim-1.md:4` (rv=2, r=1) |
| R175 | pdca-dotnet | 2 | 1 | `rc2-175-joininto-quality-reclaim-1.md:4` (rv=2, r=1) |
| R177 | pdca-dotnet | 2 | 2 | `rc2-177-native-json-reclaim-1.md:4` (rv=2, r=2); amendment rv=2 `:81` |
| R190 | pdca-dotnet | 2 | 2 | `rc2-190-reverify-1.md:4` (rv=2, r=2); amendment rv=2 `:29` |

Resolved note — **R177** (G1) base contract block heading was labelled `rv=1` at `:62` while the header
(`:4`) and the amendment (`:81-82`) are `rv=2`; authoritative = `rv=2`. **CLOSED 2026-10-09:** the
heading is corrected to `## Evidence contract (rv=2)` at `:62`, consistent with `:4` and `:81-82`.

## 3. Mandatory-row-version ledger

Row IDs are cited from each status file's evidence contract; the satisfying evidence is the file's own
CHECK/ACT pointer plus its artifact root.

| task | mandatory rows (rv) | satisfying evidence (command/exit/artifact) |
|---|---|---|
| R184 | `E184-01..E184-11` (`:58-68`) + `R184-01..09` (`:69`) + `AC1..AC5` (`:70`), rv=2 | ACT `CHECK PASS r=1 rv=2 n=1` — `:100`; CHECK re-gather round 1 `:99`; artifact root A = `artifacts/pdca/rc2-184/reclaim-1` (`:56`) |
| R175 | `EC175-01..EC175-10` (`:53-62`) + `H01-H08`/`C01-C05`/`X01-X05` (`:63`) + `AC175-01..05` (`:64`), rv=2 | CHECK PASS `(r=1, rv=2, n=2)` — `:94`; DO boundary re-run `:88`, loop-back n=2 `:92`; artifact root E = `docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/` (`:52`) |
| R177 | `E177-01..E177-14` (`:64-77`) + `A1..A11` (`:78`) + `M177-*` six rows (`:42`) + `AC1..AC5` (`:78`), rv=2 | DO→CHECK boundary `:108`, R5 `:109`, CHECK PASS `(r=2, rv=2, n=1)` — `:112`; artifact root = `docs/specs/status/rc2-177-reclaim-evidence/` (`:63`) |
| R190 | `EV190-*` 21 rows (`:64-84`) + `R190-01..09` + `AC1..AC5` (`:85`), rv=2 | CHECK PASS `(r=2, rv=2, n=1)` — `:116`; DO final-tip sweeps `:114`; artifact root = `artifacts/pdca/rc2-190-reverify-1/` (`:63`) |

No row was fabricated; unresolved rows are in §8.

## 4. Priority + variant matrix

| task | priority class/rows (`:line`) | variant matrix (`:line`) | rows closed |
|---|---|---|---|
| R184 | `:102` `## Priority matrix` — P0/P1/P2 classes (completeness-repair addition; source had no explicit priority heading; old dispositions `:34-35`) | `:34` `## Variant matrix (disposition)`; rows `:35` | V01–V18 as recorded; new key types + ClickHouse eager extension deferred `:35,:32` |
| R175 | `:97` `## Priority matrix` — P0/P1/P2 classes (completeness-repair addition; old dispositions `:29-31`) | `:29` `## H/C/X matrix (planned disposition, not already closed)`; rows `:30-31` | H01–H08, C01–C05, X01–X05 + additional axes |
| R177 | `:115` `## Priority matrix` — P0/P1/P2 classes (completeness-repair addition; old dispositions `:41-42`) | `:41` `## Variant matrix (all test/guard now; none deferred except noted)`; rows `:42` | A1–A11 + M177-* six reclaim rows; provider extension deferred `:42` |
| R190 | `:42` — "All 21 EV190 rows are P1 acceptance obligations; CHECK may not lower/waive" | `:41` `## Variant matrix (closure mode)`; rows `:42` | R190-01..09 + 21 EV190-* |

**AC1–AC5 mapping** (see §5): every DONE task's contract includes build/unit/integration/coverage/docs
obligations satisfied by the corresponding AC artifact in §5; the per-task rows in §3 are the granular
form. No row was fabricated; unresolved rows are in §8.

**Priority-matrix closure (G2/G3/G4).** The three source status files carried no explicit priority
heading; each now carries a `## Priority matrix` section (R184 `:102`, R175 `:97`, R177 `:115`) that
promotes the already-recorded variant/DO dispositions under explicit **P0/P1/P2** classes without
changing any disposition. G2/G3/G4 are closed in §8; the priority column above cites the new sites.

## 4b. Per-row priority/variant disposition ledger

Scope: the 4 DONE collection tasks (R184, R175, R177, R190). Every row cites its source status file
`file:line`; dispositions are `test` / `guard` / `deferred`+trigger / `N-A` as recorded, else `missing`.
Evidence binds to the matrix closure site plus the task CHECK/ACT verdict and artifact root. No suite was
re-run for this ledger; every pointer is read from the committed status files. Rows with no explicit row
ID use their axis text as the label (`no id in source`).

### 4b.1 R184 — matrix `rc2-184-eagerloadmode-reclaim-1.md:35`; ACT `:100`; root `artifacts/pdca/rc2-184/reclaim-1`

- V01 omitted/Default/Split one collection → test (R03) — `:35`; `E184-03` `:60`.
- V02 Single one/many → test (R02) — `:35`; `E184-03/E184-04` `:60-61`.
- V03 Default→Single → test — `:35`.
- V04 Single/Split→Default inherits → test — `:35`.
- V05 equal explicit selections → test — `:35`.
- V06 opposing explicit → `NotSupportedException` → test — `:35`.
- V07 unknown enum → `ArgumentOutOfRangeException` → test — `:35`.
- V08 choice→copy→extra `LoadWith(Default)` → test — `:35`.
- V09 non-stitching terminals → test+guard — `:35`; `E184-03` `:60`.
- V10 empty/duplicate → test — `:35`; `E184-04` `:61`.
- V11 existing value/ref/null keys → test/guard (new key types deferred) — `:35`.
- V12 split 1001 keys → test — `:35`; `E184-04` `:61`.
- V13 sync/async → test — `:35`; `E184-03` `:60`.
- V14 in-memory/SQLite no DB → test — `:35`.
- V15 SQLite/PG/SS/MySQL live → test — `:35`; `E184-05` `:62`.
- V16 ClickHouse live gate → test (eager extension deferred) — `:35`.
- V17 XML/bench/tests/docs/inherited → test/guard — `:35`.
- V18 legacy single call without `LoadWith` → test/guard — `:35`.
- New key types / ClickHouse eager-specific extension → deferred (trigger recorded) — `:32`.

### 4b.2 R175 — matrix `rc2-175-joininto-quality-reclaim-1.md:30-31`; CHECK PASS `:94`; root `docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/`

- H01 `SelectExpressionPlanEqualityComparerTests.cs:34` → test (FC1) — `:30`; variants/H01 + focused/core-* `:63`.
- H02 `SelectExpressionPlanEqualityComparerTests.cs:60` → test (FC1) — `:30`.
- H03 `SelectExpressionPlanEqualityComparerTests.cs:72` → test (FC1) — `:30`.
- H04 `ImplicitNavigationR3CountBoundaryTests.cs:102` → test (FC3) — `:30`.
- H05 `RawSourceBindingFilterTests.cs:927` → test (FC2) — `:30`.
- H06 `RawSourceBindingFilterTests.cs:948` → test (FC2) — `:30`.
- H07 `PlanKeyStructureTests.cs:238` → test (FC4) — `:30`.
- H08 `InMemoryTests.cs:331` → test (FC5) — `:30`.
- C01–C04 `JoinIntoPlanKeyTests.cs:63,91,106,135` → test (FC6, keep equals+hash-coherence) — `:30`.
- C05 `JoinIntoSqlGenerationTests.cs:136` → test (FS2, keep coherence) — `:30`.
- X01 `JoinIntoExecutionTests.cs:55` → test (FS1, execution count) — `:30`.
- X02 `JoinIntoExecutionTests.cs:76` → test (FS1) — `:30`.
- X03 `CommonTestSuite.JoinInto.cs:221-222` → guard if unchanged, else test all providers — `:30`; integration-guard `:60`.
- X04 `JoinIntoSqlGenerationTests.cs:159-162` → test (FS2, reuse identity) — `:30`.
- X05 same-site miss → test (FS2, identity/miss) — `:30`.
- null/default axis → guard — `:31`.
- value/reference axis → guard — `:31`.
- providers axis → test (SQLite focused/full + all container providers in AC3) — `:31`.
- new synthetic collision scenarios → deferred (trigger: comparer/hash change or explicit collision-robustness requirement) — `:31`.

### 4b.3 R177 — matrix `rc2-177-native-json-reclaim-1.md:42`; CHECK PASS `:112`; root `docs/specs/status/rc2-177-reclaim-evidence/`

- A1 managed/native logical equivalence+UTF-8 + supported non-alias/non-column method-call + explicit native-selection assertion → test/guard — `:42`; `E177-02` `:65`, observed variants `:34-35`.
- A2 both surfaces × sync/async × default/explicit × absent/present CT × native params → test/guard — `:42`; `E177-14` `:77`.
- A3 zero native serializer count + bounded pump + 0/1/100/10000 rows + fragmentation/multi-chunk → test — `:42`; `E177-02` `:65`.
- A4 valid but ineligible → managed → test — `:42`; `E177-03` `:66`.
- A5 unsupported fail-closed pre-output + recursive-shape/existing-clause guards → test+guard — `:42`; `E177-03` `:66`, R7/R15 `:36-37`.
- A6 pre-cancel + cancel during prep/exec/read/write → test — `:42`; `E177-04` `:67`.
- A7 caller-owned destination open, no `Stream.Flush`, resource release on success/cancel/fault → test — `:42`; `E177-04` `:67`.
- A8 empty + Unicode across row/char/byte boundaries + fragmented IO → test — `:42`; `E177-04` `:67`.
- A9 repeated calls/changed params/native→managed→native/cache-prepared-temp-TVP isolation/no sticky `Cache=false` → test/guard — `:42`; `E177-05` `:68`.
- A10 scalar `ForJson` unchanged → test — `:42`; `E177-03` `:66`.
- A11 SQL Server live native + SQLite/PG/MySQL/MariaDB/ClickHouse managed/fail-closed + coverage/mutations/measurements/EN+RU → test — `:42`; `E177-06` `:69`.
- `M177-A1`, `M177-A9`, `M177-R7`, `M177-R15`, `M177-R16`, `M177-A2-E177-14` → each a distinct reclaim row (closed by the six R177 tests) — `:42`; observed variants `:34-37`.
- PG `json_agg` / ClickHouse `JSONEachRow` new product impl → deferred to a separately approved provider scope (existing managed/provider regression evidence NOT deferred) — `:42`.
- E177-01..14 contract rows → mapped to the C/S/L/P/Y/H/I/N/M/F/V/R/K/D commands — `:64-77`.

### 4b.4 R190 — matrix `rc2-190-reverify-1.md:42`; CHECK PASS `:116`; root `artifacts/pdca/rc2-190-reverify-1/`

- R190-01 direct root/typed + first/middle/last slots → test (ROOT, SQL, INTEGRATION-DIRECT) — `:42`; contract `:67,73`.
- R190-02 missing outer→null / present-default→non-null / parity → test+guard (NULL, INTEGRATION-DIRECT; residual (a) disclosed) — `:42`; `:68`.
- R190-03 scalar/value/reference/composite/nested/bare + mapping/ctor + CTE/arities → test+guard (REGRESSION, SQL, INTEGRATION-REGRESSION) — `:42`; `:69`.
- R190-04 same-shape reuse vs incompatible identity + shared-command + temp prep → test+guard (CACHE, SHARED, TEMP; residual (b) disclosed) — `:42`; `:70-72`.
- R190-05 six providers + real containers → test (SQL, INTEGRATION-DIRECT, INTEGRATION-REGRESSION, INFRA) — `:42`; `:73-76`.
- R190-06 build/coverage/branch/mutation disclosure → test+guard (BUILD, COVERAGE, BRANCH, MUTATION) — `:42`; `:77-80`.
- R190-07 seven benchmark cases → test (PERF) — `:42`; `:81`.
- R190-08 EN/RU docs+registers → guard+DocFX (DOCS, REGISTERS) — `:42`; `:83-84`.
- R190-09 scope/safety/provenance/boundary/completeness → guard (PREFLIGHT, SCOPE, RED, AUDIT) — `:42`; `:64-66,82`.
- All 21 `EV190-*` rows = P1 acceptance obligations; CHECK may not lower/waive — `:42`; contract `:64-84`.

## 5. AC1–AC5 references

Raw paths are under `artifacts/pdca/collection-rc2-reclaim/C/`. In this re-gather AC3 (`integration.log`) was re-run fresh and the per-provider/perf summaries produced; the remaining AC logs are staged (not re-run). HEAD `93fb9255`; provenance line in §6.

| AC | raw path(s) | key evidence |
|---|---|---|
| AC1 build | `build-debug.log`, `build-release.log` | each tail: `Build succeeded. 0 Warning(s) 0 Error(s)`; EXIT=0 (`build-debug.log:29-34`, `build-release.log:29-34`) |
| AC2 unit | `unit-nextorm.core.tests.log` (1970), `unit-nextorm.sqlite.tests.log` (1311 total = 1310 + 1 skip), `unit-nextorm.sqlserver.tests.log` (756), `unit-nextorm.postgres.tests.log` (844), `unit-nextorm.mysql.tests.log` (323), `unit-nextorm.mariadb.tests.log` (242), `unit-nextorm.clickhouse.tests.log` (607), `unit-nextorm.clickhouse.extensions.tests.log` (31), `unit-nextorm.alias.tests.log` (46), `unit-nextorm.entityframeworkcore.tests.log` (126), `unit-nextorm.publicextensibility.tests.log` (10) | 11 test projects; Σ **6265 passed / 0 failed / 1 skipped** (6266 total); the 1 skip is `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` (`unit-nextorm.sqlite.tests.log:2-3`); each log EXIT=0. `unit-nextorm.alias.poc.log` EXIT=1 = `No test projects were found.` (not a test project) |
| AC3 integration | `integration.log`, `integration-providers.txt` | tail verbatim: `nextorm.integration.tests  Total: 3561, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0, Time: 46.741s`; EXIT=0. **No** provider-unavailable skip: `grep -i "is not available" integration.log` = 0 matches; 5 reuse containers started (PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse). Providers RAN (defined methods / capability skips): PostgreSQL 876/26, SQL Server 778/48, MySQL 702/80, MariaDB 88/0, ClickHouse 238/0, SQLite 751/40; all 197 skips are per-test capability skips (e.g. `This provider cannot run a CTAS batch.`) — skips != pass |
| AC4 coverage | `coverage-Summary.txt`, `coverage-collect.log`, `reportgenerator.log` | Line **88.3%** (50594/57254), Branch **80.3%** (27180/33817) — both ≥ 85 / 75 (`coverage-Summary.txt:7,12`); `reportgenerator.log:9` EXIT=0 |
| AC5 docfx | `docfx.log` | tail: `Build succeeded with warning. 2 warning(s) / 0 error(s)`; EXIT=0 (`docfx.log:39-44`); 2 pre-existing duplicate `AnalyzerReleases` md warnings (`:4-5`) |
| spot-verification | `spot-R184.log`, `spot-R175.log`, `spot-R177.log`, `spot-R190.log` | R184: core `~EagerLoadingMode` 16/16, sqlite `~SingleQuery` 3/3, ch.ext `~ClickHouseExtensionStateTests` 8/8, mssql `~ProviderExtensionEagerGuardTests` 2/2 (`spot-R184.log:17-60`). R175: focused 260/260 (`spot-R175.log:14-24`). R177: core `~JsonNativeStreamTests` 52/52, mssql `~SqlServerNativeJsonSqlTests` 7/7, integration `~SqlServerNativeJsonStreamTests` 16/16 (`spot-R177.log:12-45`). R190: core `~EntityItemProjectionInMemoryTests` 7/7; 6 providers `~JoinWholeEntitySqlGenerationTests` 8 each (sqlite, postgres, sqlserver, mysql, mariadb, clickhouse); integration `~JoinWholeEntity` 20/20 + `~MariaDbJoinWholeEntityIntegrationTests` 2/2 + `~ClickHouseJoinWholeEntityIntegrationTests` 2/2 (`spot-R190.log:38-150`) |
| perf-acceptance | `perf-acceptance.log`, `perf-summary.txt` | exactly 7 cases: `Global total time: 00:00:49 (49.35 sec), executed benchmarks: 7` (`:798`), `EXIT=0` (`:805`), wall `real 0m52.292s` ≤240 (`:802`). Case table verbatim (`:646-648`): `Cached_PlanOnly_Param | 557.9 us ... 0.61`, `Prepared_ToList | 909.5 us ... 1.00`, `Cached_ToList | 1,834.5 us ... 2.02`. Comparable `Cached_ToList`/`Prepared_ToList` time ratio **2.017** vs baseline **1.87** (+7.9%); allocated ratio **7.67** (583.97/76.14) vs **7.42** (+3.4%) — both <20% threshold, not flagged. Other 4 cases: `Nextorm_Count` 2.178 ms (`:99`), `Nextorm_GroupByCount` 61.39 ms (`:199`), `Nextorm_Cached` 2.053 ms (`:332`), `Nextorm_Cached_ToListAsync` 1.943 ms (`:780`) |

### 5b. C-phase re-gather evidence

- Provider-count reconciliation: 3433 + 68 + 60 = 3561 — `artifacts/pdca/collection-rc2-reclaim/C/provider-count-reconciliation.md`.
- Mutation attempt (G5): `artifacts/pdca/collection-rc2-reclaim/C/stryker-attempt-summary.md`, `stryker-attempt.log`.

## 6. Provenance

Per-class producing role and origin (all lines in the per-task status files unless noted):

| task | producer(s) | origin |
|---|---|---|
| R184 | coder (DO) + check (CHECK re-gather, `:99`) + planner (ACT W1 accepted, no revision, `:100`) | owner "coder unless scout/check" `:55`; ACT `CHECK PASS r=1 rv=2 n=1` `:100`; root `artifacts/pdca/rc2-184/reclaim-1` `:56` |
| R175 | coder (DO, `:87-92`) + scout (audit briefs A1–A4, `:65`) + check (CHECK PASS `:94`; ACT committed `:95`) | owner "coder unless scout/check" `:51`; `A1-A4 are exact scout Task briefs` `:65`; root `docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/` `:52` |
| R177 | coder (DO, `:106-111`) + planner (r=1→r=2 replan, `:106`) + check (CHECK PASS `:112`; ACT `:113`) | owner "coder records, check validates" `:63`; planner revision `:106`; root `docs/specs/status/rc2-177-reclaim-evidence/` `:63` |
| R190 | coder (ledger owner) + scout (read-only audit `A(subject)`) + check (CHECK PASS `:116`; ACT `:117`) | contract "owner coder for ledger; scout for the indicated read-only audit `A(subject)`" `:63`; `A(subject)` definition `:86`; root `artifacts/pdca/rc2-190-reverify-1/` `:63` |

**Provenance (this re-gather):** `coder` (hands) with `scout` fact-gathering, HEAD `93fb9255` (`1.0.9-rc2`), 2026-10-09. Integration suite re-run, per-provider summary and perf extraction produced by `coder`; evidence-contract completion by `coder`; all AC artifacts staged under `artifacts/pdca/collection-rc2-reclaim/C/`.

## 7. Non-green characterization / debt

| task | prior terminal / reclaim classification | preserved artifact / follow-up |
|---|---|---|
| R184 | superseded prior attempt `rc2-184-loadwith-eagerloadmode-1.md`: r=2, rv=1, **blocked — evidence-contract gap** (product gates green; CHECK STOP on recurrence); no product defect — `rc2-184-...:6,88-89` | reference-only patch `artifacts/pdca/rc2-184/D184-blocked.patch` (`:8`); `artifacts/pdca/rc2-184/` |
| R175 | superseded prior attempt `rc2-175-joininto-test-quality-1.md`: r=1 terminal STOP, defect family "rv1 evidence-completeness / row-bound ledger provenance", 4× CHECK fail; no product defect; test files restored to green tip `04836505` — `rc2-175-...:6,82-83` | reference-only patch `docs/specs/status/rc2-175-evidence/D175-STOP-incomplete.patch` (`:6`) |
| R177 | superseded prior attempt `rc2-177-native-json-1.md`: r=2/rv=2, CHECK n=3 **completeness/variant FAIL** (no P1 product defect; decision (c) STOP); recommended scope = six native-path tests — `rc2-177-...:6,97-98` | reference-only patch `docs/specs/status/rc2-177-evidence/D177-INCOMPLETE.patch` (`:6`) |
| defect #208 | filed, **separate, OPEN** — projected `SqlFunctions.Parameter<T>` admitted native-eligible but rendered without a required `FOR JSON` alias (native SQL Server route throws; managed path works); OUT of the tests-only R177 footprint | issue `#208` `rc2-177-...:101-102`; preserved `findings/native-parameter-projection-defect.R3-failing.log` (16/3) + `.md` |
| defect #207 | **carried, OPEN** — uniform projection-build cancellation handling deferred; milestone NOT recorded, do not infer #20 | `rc2-190-...:5,93,116` |
| R190 | stays **CLOSED** (2026-10-08); no-op re-verification, #190 not reopened — `rc2-190-...:5,11,116` | predecessor `rc2-190-join-whole-entity-1.md` preserved unchanged `:9` |

## 8. Honest gaps / open rows

| # | gap | status | where declared |
|---|---|---|---|
| G1 | R177 base evidence-contract block label | **CLOSED** (2026-10-09) — heading corrected to `## Evidence contract (rv=2)` at `:62`, consistent with header `:4` and amendment `:81-82`; authoritative = `rv=2` | `rc2-177-native-json-reclaim-1.md:4,62,81-82` |
| G2 | R184 explicit priority class/rows | **CLOSED** (2026-10-09) — `## Priority matrix` added at `:102` with P0/P1/P2 classes promoting the existing dispositions `:34-35`; no disposition changed | `rc2-184-eagerloadmode-reclaim-1.md:102` |
| G3 | R175 explicit priority class/rows | **CLOSED** (2026-10-09) — `## Priority matrix` added at `:97` with P0/P1/P2 classes promoting the existing H/C/X dispositions `:29-31`; no disposition changed | `rc2-175-joininto-quality-reclaim-1.md:97` |
| G4 | R177 explicit priority class/rows | **CLOSED** (2026-10-09) — `## Priority matrix` added at `:115` with P0/P1/P2 classes promoting the existing variant dispositions `:41-42`; no disposition changed | `rc2-177-native-json-reclaim-1.md:115` |
| G5 | R190 EV190-MUTATION kill score | **attempted this CHECK; unusable result; accepted disclosure** — a bounded Stryker 5.0.0 run was executed this CHECK (`timeout 900 dotnet stryker --config-file artifacts/pdca/collection-rc2-reclaim/C/stryker-core-attempt.json -O ...` → exit 124, 33894 mutants created / 0 tested, per-test coverage capture Dubious, 120 Safe-Mode CS0165/CS0170/CS0411/CS0161/CS8081 compile errors, no usable score); the two unverified mutants `RowMaterializerBuilder.cs:182`, `QueryCommand.QueryPreparer.cs:1035` remain unverified and **no kill score is fabricated**. Tooling note: the reason "Stryker.NET is not installed" is **not** supported by the tree — `dotnet-stryker` 5.0.0 is declared in `.config/dotnet-tools.json` and installed at `~/.dotnet/tools/dotnet-stryker`; the score is absent because no run was made under the authorized no-tool disclosure, not because the tool is unavailable | `rc2-190-reverify-1.md:80,92,115-116`; `.config/dotnet-tools.json` |
