# Collection 1.0.9-rc1

- collection-id: `1.0.9-rc1`
- input: open issues of GitHub milestone `1.0.9-rc1` (milestone #19): 182, 181, 168, 163, 161, 150, 141, 140, 134, 133, 132, 131, 128, 127, 126
- mode: autonomous; auto-commit authorized by explicit user request and the repo AGENTS `pdca-collection` exception; **push never**
- base: branch `1.0.9-rc1` = `main` = merge of `1.0.9-b` (tag `v1.0.9-b`); working tree clean
- collection revision: r1; evidence revision: rv1

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| G01 | D141, D126, D181, D182, D168, D131, D128, D133, D127, D163 | D141 → D126 → D181 → D182 → D168 → D131 → D128 → D133 → D127 → D163 | current worktree | 1.0.9-rc1 | done |

Single group ⇒ no group/task worktrees or branches are created; authorized commits go to the current branch `1.0.9-rc1`; no `git merge --no-ff`.

## Tasks

| task | group | branch | status | reason + patch | task status file |
|---|---|---|---|---|---|
| D141 | G01 | 1.0.9-rc1 | done | issue #141 — version gates MariaDB 13 + PostgreSQL FILTER aggregates | docs/specs/status/rc1-141-version-gates-1.md |
| D126 | G01 | 1.0.9-rc1 | done | issue #126 — tuple constructor on MySQL/MariaDB/SQLite | docs/specs/status/rc1-126-tuple-ctor-1.md |
| D181 | G01 | 1.0.9-rc1 | done | issue #181 — SQLite FTS3/4/5 query surface + FTS5 FROM source | docs/specs/status/rc1-181-sqlite-fts-1.md |
| D182 | G01 | 1.0.9-rc1 | done | issue #182 — SQL Server scalar-function parity | docs/specs/status/rc1-182-mssql-scalars-1.md |
| D168 | G01 | 1.0.9-rc1 | done | issue #168 — SQL Server MapColumnExpression numeric boxing | docs/specs/status/rc1-168-mssql-boxing-1.md |
| D131 | G01 | 1.0.9-rc1 | done | issue #131 — PostgreSQL native json/jsonb column mapping | docs/specs/status/rc1-131-pg-json-column-1.md |
| D128 | G01 | 1.0.9-rc1 | done | issue #128 — ClickHouse native JSON column mapping (bare JsonObject read/write; B deferred #198) | docs/specs/status/rc1-128-ch-json-column-1.md |
| D133 | G01 | 1.0.9-rc1 | done | issue #133 — streaming LOB MySQL/MariaDB/ClickHouse (documented limitation) | docs/specs/status/rc1-133-streaming-lob-1.md |
| D127 | G01 | 1.0.9-rc1 | done | issue #127 — SQL Server OUTPUT INTO table variable | docs/specs/status/rc1-127-mssql-output-into-1.md |
| D163 | G01 | 1.0.9-rc1 | done | issue #163 — ClickHouse ref→collection accepted limitation (docs/disposition) | docs/specs/status/rc1-163-ch-refcollection-doc-1.md |

## Decisions

- **One group (G01).** The actionable-issue footprint graph is connected through the core dialect contracts (`ISqlDialect.cs`, `SqlDialectBase.cs`, `DialectCapabilities.cs`), shared core query/planner/context files, the provider dialects (ClickHouse, SQLite, MySQL/MariaDB, PostgreSQL, SQL Server), shared integration suites and shared EN+RU docs. Disjoint-footprint groups are impossible; a single sequential lane is the honest minimal grouping.
- The intra-group order is a **serialization dependency** (footprint overlap), not a functional chain.
- **Excluded from lanes (not actionable now):**
  - **#161** blocked — deferred Medium work with conflicting rc1/rc2 milestone; re-entry: explicit release-placement/priority decision or a demonstrated correctness/security requirement.
  - **#150** blocked — an approved spec is not implementation authorization for rc1; implementation not started.
  - **#140** blocked — "free column list" has no confirmed meaning or identifiable capability.
  - **#134** blocked — duplicates rc2 design issue #189; ownership/release placement unresolved.
  - **#132** already-done/close — the JSON1 family is already declared (`SqlFunctions.Sqlite.cs:83-273`) and rendered (`SqliteFunctionRenderer.cs`); recommend closing the original request; any concrete residual defect becomes separately scoped work.
- Each admitted task owns its full `pdca-dotnet` cycle (own PLAN/DO/CHECK/ACT, own status file, own acceptance contract).

## Evidence contract r1/rv1

| requirement ID | row ID | check / scenario | command or invocation + required result | planned artifacts | owner | applicability | rv |
|---|---|---|---|---|---|---|---|
| truthful admission | C-E01 | Validate collection r1 admission against the 15-issue brief: dispositions, excluded issues, G01 membership, ordered task IDs. | `Task → check`; explicit PASS/FAIL with discrepancies (no exit code) | collection status + admission-check report | collection CHECK | always | rv1 |
| exclusive ownership | C-E02 | Validate G01 union footprint, registered child footprints, and absence of concurrent overlapping child execution. | `Task → check`; explicit PASS/FAIL | footprint register, child execution ledger, check report | collection CHECK | before first dispatch, on footprint/admission change, before merge | rv1 |
| verified completion | C-E03 | For each admitted task, verify its frozen pdca-dotnet evidence contract, final CHECK and ACT, required execution logs/artifacts, and final done status; missing evidence is not completion. | `Task → check`; explicit PASS/FAIL per stable task ID | child status files, execution logs, CHECK/ACT reports, completion ledger | child cycle supplies; collection CHECK audits | every admitted task | rv1 |
| safe integration | C-E04 | Pre-merge authorization and complete-group checks pass, then integration. | `git merge --no-ff --no-edit collection/1.0.9-rc1/g01`; exit 0, no unresolved conflicts. (Single group ⇒ no group branch/merge performed; row applies only if a group branch exists.) | integration-check report, merge log, HEAD id | collection CHECK gates; coder executes | only after G01 is `done` | rv1 |

CHECK re-gather budget: at most 2 targeted evidence requests per collection CHECK invocation, owned by collection CHECK.

## Общая верификация и восстановление

- Last common C: D163 task-level CHECK PASS (all 10 G01 tasks done; queue terminal). Corrective `rc1-g01-c-evidence-1` has **completed its documentation scope (CHECK PASS, r=1, n=3/3)** and supplies the C-E03/C-E04 evidence pointers for the parent gate-C `check` invocation.
- **Documentation outcome:** D141 — `report` gate not met on the helper, covered by waiver `W-C-D141-REPORT-CAP-1`; D127 — D127-E01..E07/E10 closed, D127-E08/E09 open gaps; D126 — E01..E13 recorded; C-E03 evidence supplied; C-E04 `N/A` (single group — no group branch/merge).
- **Collection gate C: BLOCKED** — outstanding obligations: the D127-E08 (INSERT empty-result-set) and D127-E09 (computed source value through an `OutputInto` table variable) product-test gaps, which are outside this docs-only cycle's scope, plus the parent gate-C independent `Task → check` re-run over the supplied C-E03 pointers.
- Blocking reason: D127-E08 (no INSERT empty-result-set test) and D127-E09 (no computed-column `OutputInto*` test) product-test gaps outside docs-only scope; parent gate-C independent `Task → check` re-run not yet performed.
- Verification state: documentation outcome recorded at HEAD `36e540e2`; the corrective `rc1-g01-c-evidence-1` completed its documentation scope (CHECK PASS, r=1, n=3/3). The historical aggregate `pass` in the `## C run` section remains an aggregate result only. Collection gate C is **BLOCKED** on the obligations above (D127-E08/E09 product-test gaps + the parent gate-C independent `Task → check` re-run).
- Defect id and history: `D141-REPORT-CAP` — historical `report` exit 2 (3-sweep cap), fresh `report` exit 2 (4-sweep / 3-build helper caps); 0 fixes applied, no plan change, waived. `D127-E08/E09` — open gaps (no INSERT empty-result-set test; no computed-column `OutputInto*` test); not closed. Evidence: `docs/specs/status/rc1-g01-c-evidence-1/provenance.md`.
- Related corrective-task status file: `docs/specs/status/rc1-g01-c-evidence-1.md`
- Next allowed step: parent gate-C `check` runs an independent `Task → check` over the supplied evidence pointers (C-E03); the D127-E08/E09 product-test gaps are outside the docs-only scope and remain outstanding; C-E04 is `N/A` (single group — no group branch/merge); work remains on branch `1.0.9-rc1`.
- Notice: host has no todowrite tool; task status files carry the progress log instead.
- Notice: gh CLI was available; issue #141 was closed remotely (glab/gh exit 0, state CLOSED) — documented in the D141 status ACT. The commit is unpushed (push never authorized).
- Notice: D126 is done — issue #126 closed; commit 4c10548a (unpushed); CHECK PASS r=4/rv=5 recorded in rc1-126-tuple-ctor-1.md; the earlier run-1 "remains pending" note is superseded.
- Notice: D181 is done — task-level CHECK PASS (r=3, n=2/3, rv=D181.r3.ec1); issue #181 closed; commit 99c41de2 (unpushed); FTS3/4/5 query surface + FTS5 FROM source shipped, with FTS5 maintenance/control deferred to follow-ups #195/#196 (milestone 1.0.9-rc1); see docs/specs/status/rc1-181-sqlite-fts-1.md. The earlier "looping back" note is superseded.
- Notice: D182 is done — task-level CHECK PASS (r=2, n=1/3, rv=D182.r2.ec1); issue #182 closed; commit c75ef84a (unpushed); SQL Server scalar-function parity shipped for the pinned 61-entry manifest = 51 covered + 10 excluded (E1–E7); defect D182-c1 closed; see docs/specs/status/rc1-182-mssql-scalars-1.md. The earlier "in-progress (PLAN gather dispatched)" note is superseded.
- Notice: D168 is done — task-level CHECK PASS (r=2, n=1/3, rv=D168.r2.ec1); issue #168 closed; commit 4bba446d (unpushed); the general buffered SQL Server numeric mapping is now storage-typed with no per-row source boxing (perf gate B PASS: allocations 464→0 B/row, boxing 9→0, median ratio 0.3382); see docs/specs/status/rc1-168-mssql-boxing-1.md. Supersedes the earlier "D168 in-progress (PLAN gather dispatched)" note.
- Notice: D131 is done — task-level CHECK PASS (r=1, n=1/3, rv1); issue #131 closed (already-implemented + verification/docs closure); commit c28b70e3 (unpushed); native `[JsonColumn]` `json`/`jsonb` read/write verified on real PostgreSQL 17.11 (new `PostgresJsonColumnTests` 2/2 + existing `CommonTestSuite.JsonColumn` 3/3, 0 skipped), stale parameter-path-only claims corrected in EN+RU docs, no production mapping change; bare `JsonNode` read follow-up #197 (milestone 1.0.9-rc1); see docs/specs/status/rc1-131-pg-json-column-1.md.
- Notice: D128 is done — task-level CHECK PASS (r=2, n=1/3, rv=D128.r2.ec1); issue #128 closed; commit a4fd86c6 (unpushed); bare `JsonObject` read/write over a native ClickHouse `JSON` column is supported provider-locally (driver 1.4.0/server 25.8.33.6; unit 8/8, CH class 107/107, container 3185/0 failed, perf +0.9% alloc), `[JsonColumn]`/`Auto`/`SupportsJson` unchanged, option B (`JsonDocument`/`JsonElement`/legacy `Object('json')`) deferred to follow-up #198; see docs/specs/status/rc1-128-ch-json-column-1.md.
- Notice: D133 is done — task-level CHECK PASS (r=2, n=1/3, rv=D133.r2.ec1); issue #133 closed as a documented limitation; commit 6f46c774 (unpushed); measured that MySqlConnector 2.6.2 and ClickHouse.Driver 1.4.0 do not stream LOB memory-bounded (MySQL/MariaDB `GetStream`/`GetTextReader` buffer the whole value, ratios 4.00/7.99/8.00, `SequentialAccess` ignored; ClickHouse `GetStream` throws `NotImplementedException`, `GetTextReader` buffers 3.99), so `SupportsSequentialAccess` and provider `SupportsLobStreaming` stay false; EN+RU guide and the probe claim corrected plus a buffered-behavior guard added; no production change; reopen trigger = a driver providing true streaming; see docs/specs/status/rc1-133-streaming-lob-1.md.
- Notice: D127 is done — task-level CHECK PASS (r=2, n=1/3, rv=D127.r2.ec1); issue #127 closed; commit b8a7c47a (unpushed); `OutputIntoTableVariable(variableName, columnDefinitions)` explicit declaration on INSERT/UPDATE/DELETE returning builders, one batch `DECLARE @t TABLE(...); <DML> OUTPUT … INTO @t; SELECT mapped cols FROM @t;`, MERGE excluded; defect D127-param-name-missingId closed; see docs/specs/status/rc1-127-mssql-output-into-1.md.
- Notice: D163 is done — task-level CHECK PASS (r=1, n=1/3, rv=1); issue #163 closed as completed; commit 09c73590 (unpushed); verification/disposition closure of the ClickHouse reference→collection accepted provider limitation: explicit gate retained (`ClickHouseDialect.SupportsReferenceToCollectionNavigation=false`, precise `NotSupportedException`), rejection test passed on a real containerized ClickHouse (1/1, 0 skipped), EN+RU docs + revisit trigger intact, milestone note `1.0.9-rc1` (historical `1.0.9-rc2` does not bind); docs-only, no production/test/config change; see docs/specs/status/rc1-163-ch-refcollection-doc-1.md.
- Notice: collection G01 is **done at task level** — all 10 tasks (D141, D126, D181, D182, D168, D131, D128, D133, D127, D163) completed and their issues closed. The collection gate C itself remains **BLOCKED** on the outstanding D127-E08/E09 product-test gaps and the parent gate-C independent `Task → check` re-run (see the durable fields above); the historical aggregate PASS is an aggregate result only. This is a single-group collection, so no group branch was created and **no `git merge --no-ff` was performed**; all authorized commits are on branch `1.0.9-rc1` (unpushed).

## C run (aggregate, 2026-10-06 02:28 UTC)

- Scope: collection-level aggregate verification of branch `1.0.9-rc1` (HEAD `36e540e2`, the 10 G01 collection commits). All logs under `/tmp/nextorm-rc1-coll-c/`. Product code not touched; no commit.
- Step 1 — `git log --oneline -4` / `git status --porcelain`: exit 0; HEAD `36e540e2 #163 record collection status for D163 (G01 done)`; tree clean at start except untracked `artifacts/`. Logs `01-git-log.txt`, `02-git-status.txt`.
- Step 2 — `dotnet build nextorm.slnx -c Debug`: exit 0; `0 Warning(s) 0 Error(s)` (35.55 s). Log `03-build-debug.txt`.
- Step 3 — `dotnet build nextorm.slnx -c Release`: exit 0; `0 Warning(s) 0 Error(s)` (21.78 s). Log `04-build-release.txt`.
- Step 4 — `dotnet test nextorm.slnx -c Debug --no-build`: exit 0; total 8438, succeeded 5915, failed 0, skipped 2523 (container integration providers skip without `DOCKER_HOST`). Log `05-test-sln.txt`.
- Step 5 — integration with containers (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`): exit 0; total 3191, Errors 0, Failed 0, Skipped 193, Not Run 0 (28.6 s, xUnit v3 native runner).
  - Per-provider executed (each container started; 0 failures; skip counts are capability-based): PostgreSQL — ran, 25 skipped; SQL Server — ran, 43 skipped; MySQL — ran, 79 skipped; ClickHouse — ran, 0 skipped, 0 failed, container `914f06a0ba5f` started (177 ClickHouse cases discovered, none skipped/failed); SQLite — ran, 43 skipped. MariaDB (outside the required list) also ran. A run that skipped a provider is **not** accepted; no provider was skipped here.
  - Logs `06-integration.txt` (aggregate run), `06b-list-tests.txt` (per-provider discovery).
- Step 6 — coverage (CI reproduction): `dotnet tool restore` exit 0 (`dotnet-coverage 18.11.2`, `reportgenerator 5.5.11`, `docfx 2.78.5`); `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` exit 0 (test totals 8438 / 5915 / 0 / 2523); `dotnet tool run reportgenerator ...` exit 0. `tests/coverage/report/Summary.txt`: **line coverage 87.1%** (45384/52077), **branch coverage 78.8%** (23790/30166), method 77.6% — both above the CI thresholds (85 / 75). Logs `07-tool-restore.txt`, `08-coverage-collect.txt`, `09-reportgen.txt`.
- Step 7 — `dotnet docfx docs/docfx.json`: exit 0; build succeeded with 2 warnings, 0 errors (pre-existing duplicate `AnalyzerReleases.*.md` in `nextorm.core.sourcegenerator`). Log `10-docfx.txt`.
- Step 8 — perf acceptance gate (`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`): exit 0; exactly **7** acceptance benchmarks executed, 0 failures: `InMemoryBenchmarkAggregates.Nextorm_Count`, `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount`, `SqliteBenchmarkAny.Nextorm_Cached`, `SqliteBenchmarkCachedPlan.Prepared_ToList`, `SqliteBenchmarkCachedPlan.Cached_ToList`, `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param`, `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`. Log `11-perf-acceptance.txt`.
- Step 9 — CRLF/hygiene: `git status --porcelain` shows 15 tracked BDN result artifacts modified by step 8 (BDN wrote workspace LF); normalized back to CRLF (`git ls-files --eol` → no `w/lf` remaining). Untracked `artifacts/` remains pre-existing. No product code touched; no commit. Logs `12-git-status-final.txt`, `13-eol.txt`, `14-changed-tracked.txt`.
- **Aggregate result: PASS** — Debug/Release builds 0/0; unit 8438 total / 0 failed; container integration 3191 total / 0 failed (all 5 required providers executed); coverage 87.1% line / 78.8% branch; docs 0 errors; perf 7/7 acceptance cases, 0 failures.

## C re-gather

- CHECK returned **`fail`** as an **evidence-completeness blocker**: C-E01 (truthful admission) **met**, C-E02 (exclusive ownership) **met**, C-E03 (verified completion) and C-E04 (safe integration) **unverified** at this invocation.
- This is a **re-gather within the CHECK budget** (≤2 targeted evidence requests per collection CHECK invocation), **not** a DO/PLAN loop-back: no plan revision, no attempt-counter reset, no task status change — the `fail` is caused solely by missing/unresolved evidence for C-E03/C-E04, not by a defect in the work.
- Aggregate log directory: **`/tmp/nextorm-rc1-coll-c/`** (this C invocation's artifacts; see the `## C run (aggregate, 2026-10-06 02:28 UTC)` section above for the per-step log filenames).
- **D131 header reconciliation:** `docs/specs/status/rc1-131-pg-json-column-1.md` header line 3 was updated from `DO complete … awaiting CHECK` to reflect the recorded ACT (`done — CHECK PASS (rv1); ACT done (cycle N=1, revision r=1, attempt n=1/3)`), removing the durable-status inconsistency between the header and the ACT; the historical progress log and plan/evidence content were left unchanged.

## C-E03 evidence completion (corrective `rc1-g01-c-evidence-1`)

Corrective status file: `docs/specs/status/rc1-g01-c-evidence-1.md` (cycle N=1, revision r=1, attempt n=3/3;
CHECK PASS for the corrective documentation scope; rv=CEvidence.ContractA.1). Fresh collection-level
evidence JSON (at attempt n=2/3 the `scope.selectors`
were reconciled to name the unfiltered aggregate/targeted executions; no execution, count, revision or exit
code changed and no `--filter` was added):
`docs/specs/status/rc1-g01-c-evidence-1.json`; provenance:
`docs/specs/status/rc1-g01-c-evidence-1/provenance.md`.

**C-E03 requires an independent `Task → check` run — a run, not merely a document.** This corrective task
supplies the evidence pointers below for that parent gate-C `check` invocation; it does not itself perform
the collection CHECK.

| stable ID | frozen contract rv | final CHECK PASS pointer | ACT done | required artifacts pointer | done status |
|---|---|---|---|---|---|
| D141 | `D141.ContractA.strong.1` | `rc1-141-version-gates-1.md:302` (r=2, n=2) | `:300-308` | `/tmp/nextorm-D141-ContractA-strong/` (`r2/pg-full.log` 756, `r2/mdb-full.log` 166, `r2/core-full.log` 1524) | done — **R141-VERIFY not met on the helper gate** (report `EXIT 2`); waiver `W-C-D141-REPORT-CAP-1` |
| D126 | `rv=5` | `rc1-126-tuple-ctor-1.md:349-351` (r=4, n=1) | `:349-368` | `/tmp/nextorm-D126-rplus1/final/integration-results.xml` (container-bound acceptance: 3143/2950/0/193; MySQL 579, MariaDB 50); per-row files: `final/build-exit.txt` (E02), `final/mysql-inner.log` (E04), `check/boundary-rplus1-*.log` (E12), `final/coverage/report/Summary.txt` (E13) | done |
| D181 | `D181.r3.ec1` | `rc1-181-sqlite-fts-1.md:362-364` (r=3, n=2/3) | `:362-388` | `artifacts/d181/c2-integration.log` (r=3 final container integration 3169/2976/0/193; SQLite FTS 26/26), `artifacts/d181/final-build.log` | done |
| D182 | `D182.r2.ec1` | `rc1-182-mssql-scalars-1.md:146-149` (r=2, n=1/3) | `:146-154` | `artifacts/d182/check/D182-CHECK-packet.md` | done |
| D168 | `D168.r2.ec1` | `rc1-168-mssql-boxing-1.md:173-175` (r=2, n=1/3) | `:173-186` | `artifacts/d168/bdn-gate-summary.md` (perf gate B PASS: allocations 464→0 B/row, boxing 9→0, median ratio 0.3382), `artifacts/d168/integration-run.log` | done |
| D131 | `rv=1` | `rc1-131-pg-json-column-1.md:3,140` (r=1, n=1/3) | `:140-148` | `/home/alex/sources/nextorm/artifacts/d131/pg-new-json-column.log` (PostgresJsonColumnTests 2/2) + `artifacts/d131/pg-common-json-column.log` (CommonTestSuite.JsonColumn 3/3) | done |
| D128 | `D128.r2.ec1` | `rc1-128-ch-json-column-1.md:173-181` (r=2, n=1/3) | `:173-181` | `/home/alex/sources/nextorm/artifacts/d128/ch-class2.log` (ClickHouse integration class 107/107) + `artifacts/d128/integration-full.log` (container integration 3185/0, 193 skipped) | done |
| D133 | `D133.r2.ec1` | `rc1-133-streaming-lob-1.md:149-151` (r=2, n=1/3) | `:149-179` | `/tmp/nextorm-D133/lob-capability-probe-r2.log` (in-repo probe 2/2, buffered 7.99/8.00), `/tmp/nextorm-D133/probe.log`, `/tmp/nextorm-D133/probe-clickhouse.log` | done |
| D127 | `D127.r2.ec1` | `rc1-127-mssql-output-into-1.md:163` (r=2, n=1/3) | `:165-172` | `artifacts/d127/test-sqlserver.log` 697, `sqlserver-class.log` 87, `integration.log` 3191/0/193 | done — **two open gaps E08/E09** |
| D163 | `rv=1` | `rc1-163-ch-refcollection-doc-1.md:122,126-128` (r=1, n=1/3) | `:126-128` | `/tmp/nextorm-d163/ch-rejection.log` (containerized ClickHouse rejection 1/1) | done |

D141 validator disposition (actual): `brief EXIT 0`; `report EXIT 2` with verbatim
`FAIL: more than one comprehensive boundary test sweep: 4` and `FAIL: multiple boundary solution builds: 3`
(the helper's single-cycle caps vs the collection-level aggregate plus the historical three targeted D141
sweeps); no execution rewritten, omitted or reclassified; waiver below.

D127 open gaps: `D127-E08` (INSERT empty-result set — no test exists) and `D127-E09` (computed source value
through an `OutputInto` table-variable — no `OutputInto*` test references a computed column); both remain
open and the D127 `done` claim is not verified against the full frozen criteria. D127 is **not** marked
superseded.

## C-E04 safe integration

- `git show-ref --verify refs/heads/collection/1.0.9-rc1/g01` → `fatal: 'refs/heads/collection/1.0.9-rc1/g01' - not a valid ref` (exit 128).
- Single-group route (`:15`) ⇒ no group/task worktree or branch was created and no `git merge --no-ff` was
  performed or is applicable.
- Disposition: **`N/A — applicability false; no merge performed`**.
- Note: if a group branch existed, a merge run would be required by C-E04; this docs-only corrective cycle
  forbids it (no `src`/`tests`/config, no commits by DO).

## Waiver `W-C-D141-REPORT-CAP-1`

- **Required:** R141-VERIFY requires `validate_inner_loop.py report` exit 0.
- **Observed:** `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py report docs/specs/status/rc1-g01-c-evidence-1.json` → exit **2**; artifact `docs/specs/status/rc1-g01-c-evidence-1/validator-report.txt`; verbatim:
  - `FAIL: more than one comprehensive boundary test sweep: 4`
  - `FAIL: multiple boundary solution builds: 3`
- **Replacement evidence:** aggregate HEAD `36e540e2` — unit 8438/0 failed/2523 skipped; container integration 3191/0 failed/193 skipped; Debug/Release builds 0/0; coverage line 87.1% / branch 78.8%; docfx 0 errors; perf 7/7 (`/tmp/nextorm-rc1-coll-c/`, `tests/coverage/report/Summary.txt`); plus the three green targeted PG/MariaDB/core runs (756/166/1524) with real commands, revisions and logs (`/tmp/nextorm-D141-ContractA-strong/r2/`).
- **Reason:** the helper caps comprehensive boundary sweeps and boundary solution builds at one, while the collection-level aggregate plus the historical D141 plan-r2 required three full affected-project sweeps (and Debug/Release builds); no execution was rewritten, omitted or reclassified.
- **Authority:** collection `escalate` decision route (a).
- **Scope:** exception to D141 validator completion only; it does **not** waive the D127-E08/E09 gaps or any other gate-C requirement.
- **Collection CHECK disposition:** placeholder — parent gate-C `check` to confirm.

## Done / Verified / Incomplete

- Done: D141 (#141) — commit 45107d9c; D126 (#126) — commit 4c10548a; D181 (#181) — commit 99c41de2; D182 (#182) — commit c75ef84a; D168 (#168) — commit 4bba446d; D131 (#131) — commit c28b70e3; D128 (#128) — commit a4fd86c6; D133 (#133) — commit 6f46c774; D127 (#127) — commit b8a7c47a; D163 (#163) — commit 09c73590
- Verified: D141 CHECK PASS, rv=D141.ContractA.strong.1; D126 CHECK PASS, rv=5; D181 CHECK PASS, rv=D181.r3.ec1; D182 CHECK PASS, rv=D182.r2.ec1; D168 CHECK PASS, rv=D168.r2.ec1; D131 CHECK PASS, rv=1; D128 CHECK PASS, rv=D128.r2.ec1; D133 CHECK PASS, rv=D133.r2.ec1; D127 CHECK PASS, rv=D127.r2.ec1; D163 CHECK PASS, rv=1
- Incomplete: — (all 10 G01 tasks done at task level); collection gate C remains **BLOCKED** on the D127-E08/E09 product-test gaps and the parent gate-C independent `Task → check` re-run (see durable fields above).
