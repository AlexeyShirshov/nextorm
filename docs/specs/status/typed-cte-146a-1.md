# Typed CTE — ordinary (slice A) — #146

- status: DONE — #146-A slice A committed (`a2f229582620073e5f132d58f2e289d8270d95e5`); slice B remains (#146 OPEN)
- cycle: 1, revision r2, attempt 1/3
- issue: #146 (milestone 1.0.9-b)
- collection: 1.0.9-b-3, slice A
- goal: typed `Cte<TResult>` descriptor + `AsCte`/`From(Cte<T>)` for ordinary CTEs, wiring the existing `FromExpression(string, QueryCommand columnShape)` projection-source marker, dependency reuse and filter exclusion. No slice-B API.
- spec: `docs/superpowers/specs/2026-10-01-cte-projections-design.md` §4, §5.1, §6, §7, §9, §11, §12

## Acceptance criteria
- **D1 Contract:** `src/nextorm.core/Cte.cs` — `public sealed class Cte<TResult>` immutable, read-only `Name`, internal construction/association to defining `QueryCommand<TProjection>`/`CteDefinition`, reference identity (no Equals/GetHashCode). `QueryCommand<T>.AsCte(this, string) -> Cte<T>` in `QueryCommand.TResult.cs` (null/empty guarded). `public static EntityBuilder<T> From<T>(this IDataContext, Cte<T>)` in `DataContextExtensions.cs`. Old `With`/`WithRecursive`/`From(string)`/`From(CteDefinition)` unchanged.
- **D2 Shape fidelity:** typed `From` carries the defining command as `FromExpression.ColumnShape`; SqlSourceRenderer renders the bare CTE name (no derived SELECT wrapper) and registers the projection columns; existing `TryBuildProjectionSelectList` identity-reuse branch extended to have the shape prepared before the outer `PrepareColumns`.
- **D3 Dependencies:** `From(Cte<T>)` attaches the used definition + reachable transitive dependencies only (unused omitted), same-instance dedup and distinct same-name reject via existing `CteMerge`/`CteHoister`; dependency-before-consumer ordering reused.
- **D4 Filters:** no entity-filter injection when the main/join source has a `ColumnShape` projection source (`InjectMainSourceFilters`/`InjectJoinFilters`); filters inside the defining query remain.
- **D5 Hygiene:** no mutation of shared command flags; validation/attachment is prep-time; zero-warning Debug+Release build.
- **No slice B:** `AsRecursiveCte`/`CteReference<>` absent (reflection smoke).

## Plan (r2) — evidence remediation (cycle 1, revision r2, attempt 1/3)

### r1 history — exhausted (kept intact)
- cycle 1, revision r1: DO completed (build / unit / live-integration / coverage / mutation / acceptance all recorded further below). CHECK then ran **3 evidence-gate rounds**; each returned FAIL on **evidence completeness**, not on an established product defect. r1 is therefore **exhausted — there is no 4th attempt of r1**. r1 production/tests/docs stay frozen and unchanged; **r2 changes no product code**.
  - r1-c1: branch-coverage evidence not comparable — no before/after per-type delta, new typed-CTE types unlabeled, uncovered branches masked by a line percentage.
  - r1-c2: mutation evidence not provable — no manifest separating original vs new-hunk trials, no mutated/restored exit codes, no preimage hashes, `MakeInnerColumn` equivalence asserted rather than anchored.
  - r1-c3: process / security / integrity evidence missing — no real-exit-code process ledger, no independent security disposition, no frozen-input SHA-256 manifest or independent diff facts.
- Replan reason is recorded here; acceptance criteria and thresholds are **unchanged** (no weakening).

### Goal (unchanged)
Seal **verification evidence** for the frozen #146-A typed ordinary CTE. **No production change** in r2. **Slice B out of scope.** Preserve **AC1–AC7** (the frozen acceptance set in `## Acceptance criteria`: D1 contract, D2 shape fidelity, D3 dependencies, D4 filters, D5 hygiene, no-slice-B, legacy-API-unchanged), referencing `docs/superpowers/specs/2026-10-01-cte-projections-design.md` §5.1 / §5.2 / §6 / §7 / §12(A). Preserve thresholds **line ≥85 / branch ≥75**. Preserve acceptance **7 cases** + cached-vs-prepared ratio baseline **1.87** / trigger **2.244** + **≤240 s**. Preserve mutation obligations (surviving mutants killed or justified).

### Frozen r1 execution plan (kept; no production change in r2)
1. Add `CteHoister.Reachable` (name closure over candidates) and extract `CollectReferencedNames`.
2. Add `Cte<TResult>` descriptor computing its hoisted definition set at construction.
3. Add `QueryCommand<T>.AsCte`.
4. Add `IDataContext.From(Cte<T>)` overload.
5. Prepare `FromExpression.ColumnShape` in `QueryPreparer.PrepareFrom` before `PrepareColumns`.
6. Exclude entity filters for `ColumnShape` main/join sources.
7. Smoke test `tests/nextorm.core.tests/TypedCteTests.cs` (SQL render + slice-B absence).

### Units (r2)
- **D0 gather** — owner `scout` (read-only). Collect: (a) **verbatim AC** text; (b) resolved **changed-type symbol identities**, generic vs non-generic (`Cte<TResult>`, `CteDefinition`, `CteQuery`, `CteHoister`, `SqlBuildContext`, `SqlSourceRenderer`, `MemberTranslator`, `QueryCommand<TResult>`, `QueryCommand.QueryPreparer`, `DataContextExtensions`) with `file:line`; (c) **coverage report identities** (assembly + report paths for the before/after runs); (d) **mutation manifest** — original trials vs new-hunk trials, explicitly marking **distinct vs overlapping** mutants; (e) `MemberTranslator.MakeInnerColumn` **equivalence control/data-flow facts** with `file:line` (both consuming sites, which field each emits); (f) **security facts** — name entry points, callers, `QuoteIdentifiers` selection, renderer, tests. State `pending` until the D0 report is filed.
- **D1 seal snapshots** — owner `coder`. Write a **SHA-256 manifest of the frozen verification inputs**; create **two detached worktrees at `c7f939d`** (`before`, `after`); apply the **frozen patch (excluding this status file) + untracked tar** into `after`; verify the applied tree. **No commit, no push, no working-tree reset.** State `pending`.
- **D2 branch delta** — owner `coder`. Produce **before-common / after-common / after-full** coverage with an **identical HEAD test corpus in `after-common`**; report **per-type raw covered/total branches + deltas**; label changed/new types as **"new, no baseline"** (roslyn-proved); **list every uncovered branch**. State `pending`.
- **D3 process replay** — owner `coder` (verdict by the **security stream**). Install a `run()` wrapper capturing **real exit codes**; replay **build / units (sans integration) / coverage / docfx / integration / acceptance**; record **DocFX exit**; run a **full mutation replay serially in the disposable snapshot** with **mutated/restored exit codes + preimage hashes**; record the `security-auditor` disposition **verbatim**. State `pending`.
- **D4 assemble** — owner `coder`. Assemble `acceptance-map.md`, `branch-delta.csv`, `mutation-results.tsv` + `mutation-equivalence.md`, `security-disposition.md`, `priority-matrix.md`, `processes.tsv`, `check-brief.md`. **Every entry row** = claim/AC-P1 ID | input hash | exact command | cwd | exit | wall | counts | `file:line` | log path. **Embed the full AC list and the priority matrix in `check-brief.md`.** State `pending`.

### PLAN priority matrix (overlay classes first, verbatim; then PLAN-added)
| класс изменения | обязательные строки (P1 по построению) | evidence / closure |
|---|---|---|
| стриминговый терминал | обе поверхности (`QueryCommand<TResult>` + `EntityBuilder<TEntity>`); sync **и** async; `CancellationToken` у async; `params`-паритет с сиблингами; fail-closed на неподдержанной форме; владение ресурсами (приёмник не закрывать/не `Dispose`; flush-семантика); покрытие провайдеров | **applicability guard:** #146-A adds no streaming terminal surface → row closed N/A with the observable predicate "no terminal method added in the frozen diff"; D0 confirms. |
| новая SQL-функция/оператор | паритет формы с SQL-сиблингом; capability-флаг по диалектам; тест на каждом провайдере; nullable-семантика | **applicability guard:** typed CTE adds the `WITH` clause (existing construct), not a scalar fn/op → row closed N/A with the observable predicate "no `SqlFunctions`/dialect capability member added in the frozen diff"; provider dialect typed-CTE tests cover rendering. |
| изменение query-path / plan cache | cached-vs-prepared ratio (перф-приёмка); не мутировать shared `QueryCommand`; покрытие temp-table/TVP-веток | ratio: baseline **1.87** / trigger **2.244** / **≤240 s** (fresh 7-case run in sealed `after`); no shared-command mutation (D5/§9, mutation obligations); temp-table/TVP non-reachability from the changed methods (roslyn facts). |
| public-API (PLAN-added) | docs EN+RU in the same change; XML docs; `API-NAMING-REVIEW.md` register entry; no silent rename/removal | r1 docs already done (`docs/guide/08-cte.md`, `docs/ru/guide/08-cte.md`, register `:4029`, XML docs); r2 audits the frozen EN/RU contract + DocFX real exit code — **no new public doc edits**. |
| verification branch-delta (PLAN-added) | comparable before/after per-type **branch** coverage with identical test corpus; new types labelled "new, no baseline"; every uncovered branch listed | D2 `branch-delta.csv`; D0 coverage report identities. |
| security (PLAN-added) | independent disposition: entry points/callers/`QuoteIdentifiers` selection/renderer/tests; no quoting/injection regression from forced aliases | D0 security facts + D3 `security-disposition.md`, recorded verbatim by the security stream. |
| mutation (PLAN-added) | identities + exits + preimage hashes + behavioral kills + justified equivalents; no unexplained survivors; run in the disposable snapshot | D3 `mutation-results.tsv` + `mutation-equivalence.md`; D0 manifest (distinct vs overlapping). |

### Variant matrix (axis → row → closure → evidence)
| axis | row (variant) | closure | evidence |
|---|---|---|---|
| projection | value-type / ordinal projection | test | `TypedCte_Descriptor_ShouldExposeNameAndSingleDefinition` + provider typed-CTE tests |
| projection | nullable projection | test | provider typed-CTE tests |
| projection | reference-type / anonymous projection | test | core `TypedCte_*` suite |
| projection | alias-slot join of a typed CTE | test | `TypedCteAliasTests` (3) |
| projection | converter preserved (direct + reselected) | test | sqlite `Converter_ProjectedBody_ShouldRoundTripWithoutDoubleConversion` |
| lineage | chained producer → consumer | test | `Cte_Typed_ChainedConsumer_ShouldReadThroughProducer` + postgres `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` |
| lineage | heterogeneous CTEs with dependency order | test | `Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData` |
| lineage | union / set-op body referencing a typed CTE | test | core `TypedCte_UnionBody*` suite |
| type/null | `System.Tuple` projection | guard (fail-fast) | `TypedCte_SystemTupleForm_ShouldFailFast`, postgres `TypedCte_SystemTupleProjection_ShouldFailFast` |
| type/null | `ValueTuple` projection | guard (fail-fast) | `TypedCte_ValueTupleForm_ShouldFailFast` |
| type/null | null / empty CTE name | guard | `ArgumentException.ThrowIfNullOrEmpty` (`QueryCommand.TResult.cs:1051`) |
| construction | legacy string body via `With` (missing typed ctor) | test | legacy `With`/`WithRecursive` suites unchanged |
| provider | sqlite / postgres / sqlserver / mysql / mariadb / clickhouse | test | per-provider `TypedCteSqlGenerationTests` |
| branch-delta | coverage delta row (D2) | evidence | `branch-delta.csv` (before-common / after-common / after-full) |
| new-types | changed/new types have no baseline | guard | "new, no baseline" label, roslyn-proved (D2) |
| security | identifier quoting / alias forcing | evidence | `security-disposition.md` (security stream) |
| slice-B | `AsRecursiveCte` / `CteReference<>` | **deferred + trigger** | trigger: slice-B cycle; absent here by spec/reflection (out of scope) |

### Execution mode
Sequential commands in **isolated worktrees** (`before` / `after` detached at `c7f939d`); read-only security/diff gather is independent; **one `coder` owns artifact assembly** (D4). No commit, no push, no working-tree reset.

### Docs plan
**No public doc edits in r2.** Audit the frozen EN/RU contract (`docs/guide/08-cte.md` + `docs/ru/guide/08-cte.md`, `API-NAMING-REVIEW.md`) and record the **DocFX real exit code** in D3.

### Perf decision
**Needed** — query-path change. Fresh **7-case acceptance** run in the sealed `after` worktree; record ratio vs baseline **1.87**, trigger **2.244**, wall **≤240 s**.

### Reconnaissance decision
**Targeted evidence recon** (D0 gather + D2 branch delta); **no prototype**.

### r2 exit criteria — 9-item CHECK-bundle contract
1. Verbatim AC preserved.
2. Embedded priority matrix with **every row closed** (test / guard / deferred+trigger, incl. applicability guards).
3. Comparable **branch deltas** + **new-type labels** ("new, no baseline").
4. Fresh **commands / exits / counts / wall / logs**.
5. Mutation replay **identities + exits + hashes + behavioral kills + justified equivalents**.
6. **Anchored** `MakeInnerColumn` equivalence **or honest unresolved**.
7. Independent **security disposition**.
8. **Frozen-input integrity proof** + independent **diff facts**.
9. **All units complete** (D0–D4 done; no blocked/pending/superseded).

## Progress log
- DO started (r1/n1)
- 2026-10-02T11:19Z | DO | r1 | 1/3 | D1 contract shipped: `Cte<TResult>` `src/nextorm.core/Cte.cs:15`; `QueryCommand<T>.AsCte` `src/nextorm.core/Query/QueryCommand.TResult.cs:1047`; `From<TResult>(IDataContext, Cte<TResult>)` `src/nextorm.core/DataContext/DataContextExtensions.cs:1248`. Immutable, read-only `Name`, reference identity; no slice-B symbol in `src`/`tests` production (reflection-asserted).
- 2026-10-02T11:19Z | DO | r1 | 1/3 | D2 shape: typed `From` sets `SourceFrom = new FromExpression(cte.Name, cte.Query)` (projection-source marker). `PrepareFrom` now prepares `from.ColumnShape` before `PrepareColumns` (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:220` region, existing-branch extension) so the identity/IProjection reuse branch sees the prepared shape. Rendered SQL `with recent as (select Id, Total from typed_cte_entity) select Id from recent as 't1'` — bare CTE name, no derived `from (select …)` wrapper. | /tmp/opencode/146a/smoke.log
- 2026-10-02T11:19Z | DO | r1 | 1/3 | D3 deps: `CteHoister.Reachable` + extracted `CollectReferencedNames` (`src/nextorm.core/Builders/CteHoister.cs:248,322`); `Cte<T>` hoists `[own definition + reachable deps]`, reusing existing dedup/order/same-name rejection. Unit `Reachable_ShouldIncludeRootAndReferencedOnly` proves unreferenced candidates are pruned and the root precedes its dependency. Nuance (not a blocker): a body built via the legacy `With(...)` chain still carries its nested declarations on the command, and `CteHoister.Hoist` re-flattens them by design; pruning those would require mutating/snapshotting the shared defining command, which D5/§9 forbid. Legacy `With` semantics stay unchanged; pruning applies to descriptor candidates.
- 2026-10-02T11:19Z | DO | r1 | 1/3 | D4 filters: main source — filter lookup returns empty when `cmd._from.ColumnShape is not null` and no `SourceBinding` (`QueryPreparer.cs:895`); join — early return when `join.From.ColumnShape is not null && SourceBinding is null` (`QueryPreparer.cs:1090`). Red-proofs (mutation → revert): main guard off gives outer `where t1.TenantId = 7`; join guard off gives `on (t1.Id = t2.Id and t2.TenantId = 7)`. Defining-query filters remain in the CTE body. | /tmp/opencode/146a/d4-red3.log, /tmp/opencode/146a/d4-join-red.log
- 2026-10-02T11:19Z | DO | r1 | 1/3 | builds: Debug 0 warnings/0 errors (exit 0), Release 0 warnings/0 errors (exit 0) | /tmp/opencode/146a/build-debug.log, /tmp/opencode/146a/build-release.log
- 2026-10-02T11:19Z | DO | r1 | 1/3 | smoke `TypedCteTests` 5/5 pass (bare-CTE SQL + no derived wrapper; whole-entity outer filter not reapplied; join ON filter absent; `Reachable` prunes unreferenced; slice-B absent; legacy `With`/`WithRecursive`/`From(string)`/`From(CteDefinition)` present); full core suite 1287/1287 pass, 0 skipped | /tmp/opencode/146a/smoke.log, /tmp/opencode/146a/core-full.log, /tmp/opencode/146a/d4-join-sql.txt
- 2026-10-02T11:21Z | DO | r1 | 1/3 | final builds after CRLF normalize: Debug 0/0 (exit 0), Release 0/0 (exit 0); smoke 5/5 (exit 0); full core 1287/1287 (exit 0). No slice-B symbol in `src` production. No commit. | /tmp/opencode/146a/build-{debug,release}.log, /tmp/opencode/146a/smoke.log, /tmp/opencode/146a/core-full.log
- CRLF normalized on all new/edited files. No commit.
- 2026-10-02T06:25Z | DO | r1 | 1/3 | docs stream: XML docs on `Cte<T>`/`Cte.Name`/`AsCte`/`From(Cte<T>)` (typed projection source, not a mapped entity; legacy string APIs unchanged; recursion excluded); typed-CTE section added to `docs/guide/08-cte.md:64` + `docs/ru/guide/08-cte.md:64` (AsCte/From typed read, aliases/converters, heterogeneous + self-join, auto-hoisted deps/unused omitted, body-only filters, typed-recursion note, string API retained). API-NAMING register: `#146` entry at `docs/specs/design/API-NAMING-REVIEW.md:4029` (IDML26 tracking), explicitly B not added (`:4037`). core Debug build 0 warnings/0 errors (exit 0) | /tmp/opencode/146a/build-core-debug.log
- 2026-10-02T06:25Z | DO | r1 | 1/3 | docs verify: `dotnet docfx docs/docfx.json` exit 0, 2 warnings / 0 errors (both pre-existing duplicate-source-file warnings, no NEW) | /tmp/opencode/146a/docfx.log ; slice-B leak: `rg AsRecursiveCte|CteReference docs docs/ru` only in internal specs/status (`superpowers/specs/2026-10-01-cte-projections-design.md:57-149`, `specs/status/typed-cte-146a-1.md:16`, `specs/design/API-NAMING-REVIEW.md:4037`) — absent from all public pages. CRLF preserved on every edited file.

## Evidence
- status: IN-PROGRESS (slice A; production + tests implemented **uncommitted** on branch `1.0.9-b`; no commit, no push).
- build: Debug 0 warnings / 0 errors (exit 0) | `/tmp/opencode/146a/step3-build.log`
- tests:
  - full core 1307/1307 passed, 0 failed, 0 skipped (exit 0) | `/tmp/opencode/146a/step3-core-full.log`
  - full postgres 737/737 passed, 0 failed, 0 skipped (exit 0) | `/tmp/opencode/146a/step3-postgres-full.log`
  - typed suites: core 25/25 (`d2-core-typedcte.log`), sqlite 21/21 (`step0-sqlite-typedcte.log` 19 + `d1-sqlite-chained-green.log` 2), postgres 3/3 (`d2-postgres-typedcte.log`), clickhouse 2/2, mysql 2/2, mariadb 2/2, sqlserver 2/2 (`step3-{clickhouse,mysql,mariadb,sqlserver}-typedcte.log`) — all exit 0, 0 failed, 0 skipped
- defect-1 fix: `MemberTranslator.MakeInnerColumn` — chained typed CTE same/diff CLR projection (inner column resolved from the correct projection lineage instead of the outer/inner mismatch). red→green: `/tmp/opencode/146a/d1-sqlite-chained-red.log` (exit != 0) vs `d1-sqlite-chained-green.log` (exit 0).
- defect-2 fix: ValueTuple fail-fast guard in `QueryCommand.QueryPreparer.cs` — rejects a ValueTuple-typed CTE projection with a clear error instead of emitting invalid SQL.
- logs: all under `/tmp/opencode/146a/`.

### Known remaining work (slice A) — NOT done
- (a) DONE (tests only): alias-slot typed-CTE tests `tests/nextorm.alias.tests/TypedCteAliasTests.cs` (3 tests: CTE joined as an alias slot; self-join of the same typed CTE; join of two distinct same-type typed CTEs) — focused 3/3, full alias suite 37/37, exit 0. See journal `07:46Z` entries.
- (b) DONE (tests only): 6 typed ordinary-CTE cases appended to `tests/nextorm.integration.tests/CommonTestSuite.Cte.cs`; compile 0/0 and all 24 (6 cases × 4 providers) discovered; SQLite live 6/6, exit 0. MariaDB/ClickHouse live still pending under (c).
- (c) live integration incl. MariaDB (Testcontainers; typed CTE against real servers).
- (d) CI coverage line/branch measurement for the new code.
- (e) targeted mutation campaign on the new typed-CTE paths.
- (f) 7-case acceptance run.
- (g) independent CHECK (separate reviewer).
- (h) docs already done: `docs/guide/08-cte.md` + `docs/ru/guide/08-cte.md` (typed-CTE section), `API-NAMING-REVIEW.md` register, XML docs — no remaining doc work.
- 2026-10-02T07:46Z | DO | r1 | 1/3 | stream A (alias-slot typed CTE): added `tests/nextorm.alias.tests/TypedCteAliasTests.cs` — 3 tests (CTE read via `From(cte)` joined as alias slot + SQL alias/columns; self-join of same typed CTE; join of two distinct same-type typed CTEs). Production untouched. focused `TypedCteAliasTests` 3/3, full alias suite 37/37, exit 0. | /tmp/opencode/146a/streamA-alias-focused.log, /tmp/opencode/146a/streamA-alias-full.log
- 2026-10-02T07:46Z | DO | r1 | 1/3 | stream B (integration shared suite): extended `tests/nextorm.integration.tests/CommonTestSuite.Cte.cs` with 6 typed ordinary-CTE cases — (1) typed read + outer reselect, (2) converter preserved direct + reselected, (3) multiple heterogeneous CTEs + dependency order, (4) self-join of same typed CTE, (5) body-only filter, (6) chained typed CTE. `dotnet build tests/nextorm.integration.tests -c Debug` 0 warnings/0 errors (exit 0); 24 names discovered (6 × Sqlite/Postgres/SqlServer/MySql). SQLite live run 6/6 exit 0; container providers deferred to next stream. | /tmp/opencode/146a/streamB-integration-build.log, /tmp/opencode/146a/streamB-integration-listtests.log, /tmp/opencode/146a/streamB-sqlite-live.log
- 2026-10-02T07:46Z | DO | r1 | 1/3 | builds: `dotnet build nextorm.slnx -c Debug` 0 warnings / 0 errors (exit 0). All edited/added files CRLF-normalized. No commit, no push. Status stays DO; #146-A not done. | /tmp/opencode/146a/solution-build-debug.log

## DO verification ledger — 2026-10-02T07:51Z (branch 1.0.9-b)

- runner: independent solution builds + per-project test runs + live Testcontainers suite; product code left unchanged; no commit; no push.
- builds (`dotnet build nextorm.slnx`):
  - `-c Debug`: exit 0, **0 warnings / 0 errors** | `/tmp/opencode/146a/verify-build-debug.log`
  - `-c Release`: exit 0, **0 warnings / 0 errors** | `/tmp/opencode/146a/verify-build-release.log`
- per-project tests (`dotnet test <proj> -c Debug --no-build`), all exit 0:
  - `nextorm.core.tests`: total 1307, succeeded 1307, failed 0, skipped 0 | `verify-test-nextorm.core.tests.log`
  - `nextorm.sqlite.tests`: total 892, succeeded 891, failed 0, skipped 1 | `verify-test-nextorm.sqlite.tests.log` (skip: `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` — needs `NEXTORM_LOB_SQLITE_PROBE=1`, pre-existing capability probe)
  - `nextorm.postgres.tests`: total 737, succeeded 737, failed 0, skipped 0 | `verify-test-nextorm.postgres.tests.log`
  - `nextorm.sqlserver.tests`: total 551, succeeded 551, failed 0, skipped 0 | `verify-test-nextorm.sqlserver.tests.log`
  - `nextorm.mysql.tests`: total 255, succeeded 255, failed 0, skipped 0 | `verify-test-nextorm.mysql.tests.log`
  - `nextorm.mariadb.tests`: total 155, succeeded 155, failed 0, skipped 0 | `verify-test-nextorm.mariadb.tests.log`
  - `nextorm.clickhouse.tests`: total 483, succeeded 483, failed 0, skipped 0 | `verify-test-nextorm.clickhouse.tests.log`
  - `nextorm.clickhouse.extensions.tests`: total 23, succeeded 23, failed 0, skipped 0 | `verify-test-nextorm.clickhouse.extensions.tests.log`
  - `nextorm.entityframeworkcore.tests`: total 126, succeeded 126, failed 0, skipped 0 | `verify-test-nextorm.entityframeworkcore.tests.log`
  - `nextorm.alias.tests`: total 37, succeeded 37, failed 0, skipped 0 | `verify-test-nextorm.alias.tests.log`
  - unit sub-total: 4566 tests, 4565 succeeded, 0 failed, 1 skipped.
- live integration (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`):
  - socket present before run (no `machine start` needed); `_ping` = OK.
  - result: **exit 1** — Total 3019, Errors 0, **Failed 2**, Skipped 187, Not Run 0, Time 56.6s | `/tmp/opencode/146a/verify-integration.log`
  - containers actually run (confirmed via `podman.exe ps -a`, all started+ready+stopped in log): `postgres:17-alpine` (a1877941aa2e), `mysql:8.4` (d703aedb9b21), `mcr.microsoft.com/mssql/server:2025-latest` (4ab2d4542432), `mariadb:11.4` (914f06a0ba5f), `clickhouse/clickhouse-server:25.8-alpine` (525af9d6a314) — **all 5 supported containers ran**, plus SQLite in-process. No provider-level skip; the 187 skips are per-test capability skips.
  - typed-CTE live cases come from `CommonTestSuite` only, derived by `SqliteIntegrationTests`/`PostgresIntegrationTests`/`SqlServerIntegrationTests`/`MySqlIntegrationTests` (6 cases × 4 providers = 24). `ClickHouseIntegrationTests` and the MariaDB classes do **not** derive `CommonTestSuite`, so typed-CTE is not exercised live on ClickHouse/MariaDB (unit dialect suites only). PostgreSQL: 2 of the 6 fail; SQLite/SQL Server/MySQL pass.
- **NEW DEFECT (defect-3, #146-A scope, open, production left unchanged):** Postgres chained/heterogeneous typed CTE identifier-case mismatch.
  - failing: `PostgresIntegrationTests.Cte_Typed_ChainedConsumer_ShouldReadThroughProducer` (`tests/nextorm.integration.tests/CommonTestSuite.Cte.cs:212`) and `PostgresIntegrationTests.Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData` (`CommonTestSuite.Cte.cs:152`). I did not touch production code.
  - error: `Npgsql.PostgresException 42703: column "Id" does not exist` (chained, POSITION 145) / `column t1.Id does not exist` (heterogeneous, POSITION 294).
  - repro SQL (dumped via a throwaway postgres SQL-gen test, since removed): `/tmp/opencode/146a/repro-sql-chained.txt` = `with typed_inner as (select id, nullableint as "Int" from complex_entity where (id > 1)), typed_outer as (select id from typed_inner as "t1") select "Id" from typed_outer as "t1"`; `/tmp/opencode/146a/repro-sql-hetero.txt` = `with typed_dep as (…), typed_consumer as (select id from typed_dep as "t1"), typed_other as (… ) select t1."Id" from typed_consumer as "t1" join typed_other as "t2" on t1."Id" = t2.id`.
  - root cause: a typed CTE whose body reads another typed CTE exposes its projection under the member name (`"Id"`, quoted) while the producer CTE body emitted the bare mapped column (`id`); PostgreSQL quoted identifiers are case-sensitive, so `"Id"` is unresolved. SQLite/SQL Server/MySQL are case-insensitive for these names and pass. Defect-1 (`MemberTranslator.MakeInnerColumn`) fixed the chained lineage's column resolution for SQLite but not this output-column name/case mismatch.
  - impact: #146-A acceptance is blocked — live Postgres chained/heterogeneous typed CTE reads are broken. A correct fix must make the CTE projection column name/alias and the consumer's shape agree (wider blast radius than a verification-only change allows).
- 2026-10-02T07:51Z | DO | r1 | 1/3 | DO verification: Debug 0/0, Release 0/0; 10/10 unit projects green (4565 succeeded, 1 sqlite capability skip); live integration exit 1 — 3019 total / 2 failed / 187 skipped; all 5 provider containers (Postgres, MySQL, SQL Server, MariaDB, ClickHouse) + SQLite ran; found defect-3 (Postgres chained/heterogeneous typed CTE, identifier-case) — production unchanged, unfixed. Status stays **DO**; #146-A NOT done. | `/tmp/opencode/146a/verify-{build-debug,build-release,test-*,integration}.log`, `repro-sql-chained.txt`, `repro-sql-hetero.txt`

## Defect-3 fix — 2026-10-02T13:03Z (still DO, #146-A NOT done)

- **Diagnosis (file:line):** `src/nextorm.core/Visitors/MemberTranslator.cs:714 MakeInnerColumn` returned `(true, innerCol.PropertyName!)` (e.g. `"Id"`) for a typed-CTE read, while the typed CTE *declaration body* was rendered as a plain select that emitted the bare mapped column `id` (a case-only difference is `OrdinalIgnoreCase`-equal, so no alias was added). PostgreSQL quoted identifiers do not fold case ⇒ `42703 column "Id" does not exist`, in `CommonTestSuite.Cte.cs:212` (chained) and `:152` (heterogeneous).
- **Correct side:** the declaration, per `docs/superpowers/specs/2026-10-01-cte-projections-design.md` §5.1 — projection properties translate to output aliases. The alias is forced only for typed CTE declarations; ordinary mapped entities and legacy `With`/`WithRecursive` string CTEs keep the mapped/physical output names, so their quoting is unchanged.
- **Fix (file:line):**
  - `src/nextorm.core/Builders/CteQuery.cs:71` — `internal bool TypedProjection { get; init; }` on `CteDefinition`.
  - `src/nextorm.core/Cte.cs:30` — `Cte<T>` marks its definition `TypedProjection = true`.
  - `src/nextorm.core/DataContext/SqlBuildContext.cs:58` — `internal bool ExactProjectionAliases`.
  - `src/nextorm.core/DataContext/SqlSourceRenderer.cs:86` — `MakeWithClause` renders a typed body with `ExactProjectionAliases = cte.TypedProjection`.
  - `src/nextorm.core/DataContext/SqlSourceRenderer.cs:1064 MakeColumn` — case-sensitive (`Ordinal`) column/property comparison when `ExactProjectionAliases`, so `id` gets `as "Id"`.
  - `src/nextorm.core/Visitors/MemberTranslator.cs:714 MakeInnerColumn` + new `IsTypedCteProducer` — a member read over a typed CTE producer (final consumer and body-to-body/chained) references the declared property name; the `innerQuery.From?.ColumnShape` fallback is retained for data-modifying shapes.
- **Tests added/updated:** `tests/nextorm.sqlite.tests/TypedCteTests.cs` — corrected `NestedCteBody` alias assertions, new `HeterogeneousCtesWithDependency_ShouldReferenceDeclaredColumnNames` (22/22); `tests/nextorm.postgres.tests/TypedCteSqlGenerationTests.cs` — new `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` (4/4).
- **Build:** `dotnet build nextorm.slnx -c Debug` → 0 warnings / 0 errors (exit 0).
- **Unit suites (all exit 0):** core 1307/1307; sqlite 893 (892 pass, 1 pre-existing capability skip); postgres 738/738; sqlserver 551/551; mysql 255/255; mariadb 155/155; clickhouse 483/483; alias 37/37; efcore 126/126.
- **Live integration (defect-3):** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` → **exit 0**, Total 3019, Errors 0, **Failed 0**, Skipped 187 (pre-existing baseline), Not Run 0, Time 49.864s; log `/tmp/opencode/146a/verify-integration-defect3.log`. All 5 provider containers ran (postgres a1877941aa2e, mssql 4ab2d4542432, mysql d703aedb9b21, mariadb 914f06a0ba5f, clickhouse 525af9d6a314) + SQLite.
  - targeted live Postgres re-runs: `Cte_Typed_ChainedConsumer_ShouldReadThroughProducer` → Total 1 / Failed 0 / exit 0 (`verify-integration-defect3-case1.log`); `Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData` → Total 1 / Failed 0 / exit 0 (`verify-integration-defect3-case2.log`). Both previously failing cases now pass on PostgreSQL.
- **CRLF:** all six edited source/test files normalized (`LF-only-lines=0`).
- **Status:** DO; #146-A NOT done; no commit, no push.

## (d) CI coverage — 2026-10-02T13:05Z (still DO, #146-A NOT done)

- Reproduced CI: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage-146a-ci.cobertura.xml "dotnet test nextorm.slnx -c Debug --no-build"`, then `dotnet tool run reportgenerator -reports:tests/coverage/coverage-146a-ci.cobertura.xml -targetdir:tests/coverage/report-146a-ci -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`.
- Included modules (`coverage.settings.xml` filter): `nextorm.core`, `nextorm.sqlite`, `nextorm.postgres`, `nextorm.sqlserver` (4 assemblies, 529 classes, 314 files). Test run: total 7587, failed 0, succeeded 5190, skipped 2397, exit 0 (no `DOCKER_HOST`; container providers skipped, SQLite integration ran) | `/tmp/opencode/146a/146a-coverage-ci-collect.log`.
- Result (full CI-style): **Line coverage 86.5%** (41288/47681) vs threshold 85 → PASS; **Branch coverage 77.9%** (20965/26891) vs threshold 75 → PASS. Per assembly: core 86.8%, sqlite 89.3%, postgres 79.2%, sqlserver 78.2%.
- Summary path: `tests/coverage/report-146a-ci/Summary.txt` (HTML `tests/coverage/report-146a-ci/index.html`; cobertura `tests/coverage/coverage-146a-ci.cobertura.xml`) | `/tmp/opencode/146a/146a-coverage-ci-report.log`.
- Minimum-scope cross-check (only core+sqlite+postgres+sqlserver test projects, same filters): Line 80.9%, Branch 73.6% — below thresholds because it omits the other unit suites (mysql/mariadb/clickhouse/alias/efcore/clickhouse.extensions) that also exercise `nextorm.core`; summary `tests/coverage/report-146a/Summary.txt`. The full CI-style number above is the faithful reproduction.

## (e) Targeted mutation campaign — 2026-10-02T13:21Z (typed-CTE seam)

- Method: manual, md5-verified (Stryker unusable on net10/xunit.v3 MTP, CompileError). Each mutation: backup bytes → exact 1-occurrence replace → `dotnet build tests/<proj> -c Debug --no-restore` → focused `dotnet test ... --filter FullyQualifiedName~<test>` → restore bytes → md5 verify.
- Outcome: **16 killed / 18 mutations, 2 documented equivalents, 0 unexplained survivors**; every restore md5-identical; no residual mutation.
- Per seam:
  - `Cte<T>`/`TypedProjection`: `TypedProjection = true` removed (`Cte.cs:30`) → postgres `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` → KILLED; `Cte<T>.Name` corrupted (`Cte.cs:50`) → core `TypedCte_Descriptor_ShouldExposeNameAndSingleDefinition` → KILLED; `TypedProjection` default `= true` (`CteQuery.cs:78`) → postgres `Cte_NestedBody_ShouldHoistIntoSingleTopLevelWith` (legacy `With` alias corruption) → KILLED. 3/3.
  - `CteHoister`: never omit unreferenced (`CteHoister.cs:360`) → core `Reachable_ShouldIncludeRootAndReferencedOnly` → KILLED; no dependency traversal (`:355`) → same → KILLED; referenced table name mangled (`:295`) → same → KILLED. 3/3. (Constant-`false` variants hit CS0162 under TreatWarningsAsErrors, so runtime-only forms used.)
  - `SqlSourceRenderer`/`SqlBuildContext`: `ExactProjectionAliases = cte.TypedProjection` → `false` (`SqlSourceRenderer.cs:86`) → postgres defect-3 test → KILLED; `MakeColumn` comparison forced `OrdinalIgnoreCase` (`:1066`) → same → KILLED. 2/2.
  - `MemberTranslator.MakeInnerColumn` (`:715`): `NeedAliasForColumn` forced `false` (`:721`) → postgres defect-3 test → KILLED. Replacing the tuple's column text with `"BOGUS"` is **equivalent**: both consuming sites branch on `NeedAliasForColumn == true` and emit `innerCol.PropertyName` directly, never `col.Column` (`:301-302`, `:563-564`, `:697-698`). 1 killed / 2, 1 equivalent.
  - `QueryPreparer` shape/filter guards: main-source filter guard removed (`:972`) → core `TypedCte_WholeEntitySource_ShouldNotReapplyEntityFilterOnOuterRead` → KILLED; join filter guard removed (`:1171`) → core `TypedCte_JoinSource_ShouldNotInjectEntityFilterIntoJoin` → KILLED; ValueTuple fail-fast disabled (`:429`) → core `TypedCte_ValueTupleForm_ShouldFailFast` → KILLED; `ResolveShapeConverter` returns null (`:883`) → sqlite `Converter_ProjectedBody_ShouldRoundTripWithoutDoubleConversion` → KILLED; `PrepareFrom` ColumnShape-prepare condition inverted (`:232`) → same sqlite converter test → KILLED. 5/5.
  - `AsCte`/`From(Cte<T>)`: `Ctes = cte.Definitions` → `null` (`DataContextExtensions.cs:1258`) → core `TypedCte_ReferencingCte_ShouldOrderDependencyBeforeConsumer` → KILLED; `FromExpression(cte.Name, cte.Query)` → `(cte.Name)` (`:1257`) → core `TypedCte_JoinSource_ShouldNotInjectEntityFilterIntoJoin` + sqlite `ScalarForm_ShouldMaterializeThroughTypedCte` + postgres defect-3 → KILLED. `ArgumentException.ThrowIfNullOrEmpty` → `ThrowIfNull` (`QueryCommand.TResult.cs:1051`) is **equivalent**: `Cte<T>`'s ctor builds `CteDefinition(name, query)`, which independently rejects null/empty at `CteQuery.cs:25`, so observable behavior is unchanged. 2 killed / 3, 1 equivalent.
- Logs: `/tmp/opencode/146a/mutation-campaign.log`, `mutation-probe.log`, `mutation-probe-m8.log`, `mut/*.log`, `mut/summary.json`, `mut/probe.json`.
- md5 restore: `md5sum -c /tmp/opencode/146a/md5-pre.txt` → all 9 production files OK; `git diff --stat` unchanged (8 files, +273/-16); no mutation residue.
- Status: **DO**; #146-A NOT done; no commit, no push.

## (f) 7-case acceptance run — 2026-10-02T13:23Z (still DO, #146-A NOT done)

- 2026-10-02T13:23Z | DO | r1 | 1/3 | acceptance (f): command `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; exit **0**; **7** cases selected, **0** failures; BDN `Global total time` **41.51 s** (executed benchmarks: 7); external shell wall clock **49 s** — both under the 4 min budget. Cache-vs-prepared ratio **2.08** vs baseline **1.87** (+11.2%), below the >20% investigate trigger **2.244**; allocated ratio **7.90**. Tracked BDN artifacts restored before and after; working tree clean for benchmarks. | /tmp/opencode/146a/acceptance-146a.log
- Command / log: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` → exit **0**; full log `/tmp/opencode/146a/acceptance-146a.log`.
- Contract: **7** cases selected, **0** failures; BDN `Global total time` **41.51 s** (executed benchmarks: 7); external shell wall clock **49 s** — both ≤ the 4 min (`≤240 s`) budget.
- Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = 1,980.1 us / 952.2 us = **2.08** — vs documented baseline **1.87** (+11.2%); the tracked rule flags **>20 %** growth, i.e. a ratio above **1.87 × 1.20 = 2.244**. **2.08 < 2.244**, so no >20 % growth flag (recorded as a noisy `ShortRun` measurement: `Cached_ToList` `Error` = 2,145.4 us > its `Mean`; `Nextorm_Cached` `Error` = 1.184 ms > its `Mean`).
- Allocated ratio (`Cached_ToList / Prepared_ToList`) = 601.16 KB / 76.14 KB = **7.90** (baseline **7.42**, +6.5 %) — same order, no allocation regression signal.

| Case | Mean | Allocated |
|------|------|-----------|
| `Nextorm_Count` | 2.726 ms | 372.66 KB |
| `Nextorm_GroupByCount` | 64.53 ms | 50.05 MB |
| `Nextorm_Cached` | 1.991 ms | 571.17 KB |
| `Prepared_ToList` | 952.2 us | 76.14 KB |
| `Cached_ToList` | 1,980.1 us | 601.16 KB |
| `Cached_PlanOnly_Param` | 613.3 us | 525.02 KB |
| `Nextorm_Cached_ToListAsync` | 2.422 ms | 736.6 KB |

- Hygiene: tracked BDN artifacts (`BenchmarkDotNet.Artifacts/`) confirmed clean before the run and restored with `git checkout --` after it; `git status --short benchmarks` empty and no artifact modifications remain.
- Status: **DO**; #146-A NOT done; no commit, no push.

## DO unit ledger — 2026-10-02T13:25Z (all units done, DO complete → entering CHECK)

Every unit state = **done**; no unit blocked/pending/superseded. All production/test/doc files are uncommitted on branch `1.0.9-b` (HEAD `c7f939d`); no commit, no push.

| Unit | State | Evidence pointer |
|------|-------|------------------|
| D1 production `Cte.cs` | done | `src/nextorm.core/Cte.cs:15`; journal 11:19Z D1; `/tmp/opencode/146a/smoke.log` |
| D2 production `CteQuery.cs` | done | `src/nextorm.core/Builders/CteQuery.cs:71` (`TypedProjection`); defect-3 fix; `/tmp/opencode/146a/verify-integration-defect3.log` |
| D3 production `CteHoister.cs` | done | `src/nextorm.core/Builders/CteHoister.cs:248,322`; core `Reachable_ShouldIncludeRootAndReferencedOnly`; `/tmp/opencode/146a/core-full.log` |
| D2 production `SqlBuildContext.cs` | done | `src/nextorm.core/DataContext/SqlBuildContext.cs:58` (`ExactProjectionAliases`); `/tmp/opencode/146a/verify-integration-defect3.log` |
| D2 production `SqlSourceRenderer.cs` | done | `src/nextorm.core/DataContext/SqlSourceRenderer.cs:86,1064`; mutation 2/2 killed; `/tmp/opencode/146a/mut/summary.json` |
| D3 production `MemberTranslator.cs` | done | `src/nextorm.core/Visitors/MemberTranslator.cs:714` (`MakeInnerColumn`/`IsTypedCteProducer`); defect-1/3; `/tmp/opencode/146a/d1-sqlite-chained-green.log` |
| D2 production `QueryCommand.QueryPreparer.cs` | done | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:220` region (+ value-tuple guard); mutation 5/5 killed; `/tmp/opencode/146a/mut/summary.json` |
| D1 production `QueryCommand.TResult.cs` | done | `src/nextorm.core/Query/QueryCommand.TResult.cs:1047,1051` (`AsCte`); `/tmp/opencode/146a/smoke.log` |
| D1 production `DataContextExtensions.cs` | done | `src/nextorm.core/DataContext/DataContextExtensions.cs:1248,1257` (`From(Cte<T>)`); mutation killed; `/tmp/opencode/146a/mut/summary.json` |
| tests core `TypedCteTests.cs` (25) | done | `tests/nextorm.core.tests/TypedCteTests.cs`; 25/25; `/tmp/opencode/146a/d2-core-typedcte.log` |
| tests sqlite `TypedCteTests.cs` (22) | done | `tests/nextorm.sqlite.tests/TypedCteTests.cs`; 22/22 (incl. defect-3 case); `/tmp/opencode/146a/d1-sqlite-chained-green.log` |
| tests postgres `TypedCteSqlGenerationTests.cs` | done | `tests/nextorm.postgres.tests/TypedCteSqlGenerationTests.cs`; 4/4 (defect-3 test added); `/tmp/opencode/146a/d2-postgres-typedcte.log` |
| tests sqlserver `TypedCteSqlGenerationTests.cs` | done | `tests/nextorm.sqlserver.tests/TypedCteSqlGenerationTests.cs`; 2/2; `/tmp/opencode/146a/step3-sqlserver-typedcte.log` |
| tests mysql `TypedCteSqlGenerationTests.cs` | done | `tests/nextorm.mysql.tests/TypedCteSqlGenerationTests.cs`; 2/2; `/tmp/opencode/146a/step3-mysql-typedcte.log` |
| tests mariadb `TypedCteSqlGenerationTests.cs` | done | `tests/nextorm.mariadb.tests/TypedCteSqlGenerationTests.cs`; 2/2; `/tmp/opencode/146a/step3-mariadb-typedcte.log` |
| tests clickhouse `TypedCteSqlGenerationTests.cs` | done | `tests/nextorm.clickhouse.tests/TypedCteSqlGenerationTests.cs`; 2/2; `/tmp/opencode/146a/step3-clickhouse-typedcte.log` |
| tests alias `TypedCteAliasTests.cs` (3) | done | `tests/nextorm.alias.tests/TypedCteAliasTests.cs`; 3/3 focused, 37/37 suite; `/tmp/opencode/146a/streamA-alias-focused.log` |
| tests integration `CommonTestSuite.Cte.cs` (+6 cases) | done | `tests/nextorm.integration.tests/CommonTestSuite.Cte.cs`; 6×4 providers discovered, live exit 0; `/tmp/opencode/146a/verify-integration-defect3.log` |
| docs `docs/guide/08-cte.md` | done | `docs/guide/08-cte.md:64`; docfx exit 0; `/tmp/opencode/146a/docfx.log` |
| docs `docs/ru/guide/08-cte.md` | done | `docs/ru/guide/08-cte.md:64`; docfx exit 0; `/tmp/opencode/146a/docfx.log` |
| docs XML docs | done | `Cte<T>`/`Cte.Name`/`AsCte`/`From(Cte<T>)`; core Debug build 0/0; `/tmp/opencode/146a/build-core-debug.log` |
| docs `API-NAMING-REVIEW.md:4029` | done | `docs/specs/design/API-NAMING-REVIEW.md:4029` (#146 IDML26); slice-B note `:4037` |
| evidence — builds Debug+Release | done | Debug 0 warns/0 errs, Release 0/0, both exit 0; `/tmp/opencode/146a/verify-{build-debug,build-release}.log` |
| evidence — unit sub-total 4565/4566 | done | 4565 succeeded, 0 failed, 1 pre-existing sqlite capability skip; sqlite 893; `/tmp/opencode/146a/verify-test-*.log` |
| evidence — live integration | done | exit 0, Total 3019, Failed 0, Skipped 187; all 5 containers + SQLite; `/tmp/opencode/146a/verify-integration-defect3.log` |
| evidence — coverage line 86.5 / branch 77.9 | done | PASS ≥85/75; `tests/coverage/report-146a-ci/Summary.txt`; `/tmp/opencode/146a/146a-coverage-ci-report.log` |
| evidence — mutation 16/18 killed + 2 equivalents | done | 0 unexplained survivors; `/tmp/opencode/146a/mutation-campaign.log`, `mut/summary.json` |
| evidence — acceptance 7 cases / 0 failures | done | ratio 2.08 vs baseline 1.87 (trigger 2.244); exit 0; `/tmp/opencode/146a/acceptance-146a.log` |
| evidence — defect-3 fix | done | `CteQuery.cs:71`, `SqlBuildContext.cs:58`, `SqlSourceRenderer.cs:86/1064`, `MemberTranslator.cs:714`; `/tmp/opencode/146a/verify-integration-defect3.log` |

## CHECK matrix closure — 2026-10-02T08:39Z (status: DO complete — entering CHECK)

- 2026-10-02T08:39Z | DO | r1 | 1/3 | CHECK found 3 open variant rows + 4 evidence gaps; matrix closure executed. Scratch `tests/nextorm.postgres.tests/Zzz146Probe.cs` removed; no `*Probe*`/`*Zzz*`/scratch under `tests/` produced by #146-A remain (the two matches, `SqliteRowIdLobProbeTests.cs` / `LobCapabilityProbeTests.cs`, are pre-existing tracked files). No commit, no push; #146-A not closed. | `/tmp/opencode/146a/matrix-*.log`

### Row closure (spec → test → result → deciding file:line)

1. **System.Tuple actual read** — spec §6 + §12 list System.Tuple as a shape, but a *whole-row* tuple through a column-shape source is a pre-existing unsupported form (code-smells review finding 65; `TypeFacts.IsSingleColumnProjection` intentionally excludes tuple). Probe proved broken SQL: identity read `select  from tp as "t1"`, ItemN `select ().f1 as "Item1"`; the non-CTE derived path is identically broken (`select ().f1 from (select ROW…) as "t1"`). Closed as **fail-fast guard + test** (not a silent defer): production `QueryCommand.QueryPreparer.cs:372-387` (inside `PrepareColumns`) throws `NotSupportedException` when `cmd._from.ColumnShape.SelectList` contains a `System.Tuple`-typed column. Gated on `ColumnShape`, so ordinary derived tables (`SubQuery`) are untouched. Tests: core `TypedCte_SystemTupleForm_ShouldFailFast` (TestDialect now carries a `TestTupleRenderer`), postgres `TypedCte_SystemTupleProjection_ShouldFailFast` (mirrors `TypedCte_ValueTupleProjection_ShouldFailFast:70`). | `/tmp/opencode/146a/matrix-core-run.log` (31/31), `matrix-pg-run.log` (5/5)
2. **Union as a CTE body dependency** — spec §7 (set-operation branches traversed, dependency-before-consumer). **Root cause found + fixed:** `QueryCommand.ResetPreparation()` clears `_from` (`QueryCommand.cs:696`), and `Union`/`UnionAll`/`Intersect`/`IntersectAll`/`Except`/`ExceptAll` did not restore it (unlike `Hint`/`WithTag`), so a set operation whose first operand reads a typed CTE lost `FromExpression.ColumnShape` and failed with `BuildSqlCommandException: Table name is not registered for type <anon>`. Production fix: `QueryCommand.TResult.cs` (6 set-op methods; save/restore `cmd._from` around `ResetPreparation`, file:line ~964-1022). Tests: core `TypedCte_UnionBody_ShouldRenderSetOperationAndReadBareName`, `TypedCte_UnionBodyAsDependency_ShouldOrderBeforeConsumer`, `TypedCte_UnionBody_ShouldHoistDependencyReferencedInABranch`, `TypedCte_ReferencingCte_ShouldOrderDependencyBeforeConsumer`; sqlite `UnionBody_ShouldMaterializeAndHoistConsumer`; postgres existing `TypedCte_ShouldEmitWithAndReadBareName` unchanged. | `matrix-core-run.log` (31/31, exit 0), `matrix-sqlite-run.log` (23/23, exit 0)
3. **Dependency cycle** — spec §5.2/§7. Guards already present (brief's premise stale): `CteHoister.cs:165-167` (`Collect` `_stack`), `CteHoister.cs:222-224` (`VisitDeclaration` `VisitState.Visiting`), plus duplicate-name `CteHoister.cs:182-186`. Tests: core `TypedCte_SelfReferencingDeclaration_ShouldRejectCycleBeforeDb`, `TypedCte_MutualDeclarationCycle_ShouldRejectBeforeDb` — both throw `InvalidOperationException "*cycle*"`, no hang/overflow, so no P1. Public fluent API cannot form a back-edge (a name ref must target an already-declared scope; re-declaring a name is rejected as duplicate first), so the raw declaration set is the reachability proof. | `matrix-core-run.log`

### Evidence gaps

4. **Unit counter reconciliation (fresh, 10/10 projects, `dotnet run --no-build`, all `EXIT=0`)** — core 1313/0/0; sqlite 894 (893 pass, 1 skip); postgres 739; sqlserver 551; mysql 255; mariadb 155; clickhouse 483; clickhouse.extensions 23; entityframeworkcore 126; alias 37. **Corrected subtotal (superseded by the final consolidated ledger below): 4576 total, 4575 succeeded, 0 failed, 1 skipped** (prior 4568/4567/1 arithmetic + this session's 6 core + 1 sqlite + 1 postgres tests). The single skip is the pre-existing capability probe `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` (needs `NEXTORM_LOB_SQLITE_PROBE=1`). | `/tmp/opencode/146a/matrix-units.txt`, `matrix-unit-*.log`
5. **Builds** — `dotnet build nextorm.slnx -c Debug` → `EXIT=0`, 0 warnings / 0 errors (`matrix-sln-debug.log`); `-c Release` → `EXIT=0`, 0 warnings / 0 errors (`matrix-sln-release.log`).
6. **temp-table/TVP branch mapping (roslyn)** — TVP: `DataContextCache.TvpMetadata` refs only `DataContextExtensions.ResolveMetadata` (`DataContextExtensions.cs:300,327,348`) + tests; `ResolveMetadata` callers are TVP/insert/bulk/update/delete/raw-mapper/join-into builders — **none** is a changed #146-A method. temp-table: `FromExpression.TempTable` refs are `DataContext.cs:355`, `FromExpression.cs:69,201`, `QueryCommand.TempTables.cs:54,109` — `SqlSourceRenderer` (incl. `MakeColumn`/`MakeWithClause`), `QueryPreparer`, `MemberTranslator`, `CteHoister` do **not** reference it; temp-table/`CreateQueryBuilder` entry points live at `DataContextExtensions.cs:1286,1303,1443,1450` and are separate extension overloads. `CteHoister.Reachable` callers = `Cte.cs:62` + core test; `CollectReferencedNames` callers = `CteHoister.cs:246,347`. **Conclusion: the changed #146-A methods cannot reach temp-table/TVP branches.**
7. **Per-type branch coverage** (no true before-baseline exists — `nextorm.core`/typed-CTE code is new/uncommitted; `Summary.txt` carries only class line %, so branch numbers are read from the sibling `report-146a-ci/Cobertura.xml`, report generated 2026-10-02 13:05, i.e. *before* this session's added tests): Cte<TResult> 100% branch / 100% line; CteDefinition 100/100; CteHoister 93.7 branch / 95.7 line (+ `Walker` 90.3/92.6); CteQuery 100/100; SqlBuildContext 100/100; SqlSourceRenderer 87.6/94.5; MemberTranslator 79.7/87.2. No delta claimed (baseline unavailable).

### Status
- **DO complete — entering CHECK** for #146-A matrix closure. Production edits uncommitted on `1.0.9-b`; no commit, no push, #146-A not closed. All edited files CRLF.
- New/changed production this session: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` (tuple fail-fast guard), `src/nextorm.core/Query/QueryCommand.TResult.cs` (set-op `_from` preservation).
- New/changed tests this session: `tests/nextorm.core.tests/TypedCteTests.cs` (+6 tests, `TestTupleRenderer`), `tests/nextorm.sqlite.tests/TypedCteTests.cs` (+1 union test), `tests/nextorm.postgres.tests/TypedCteSqlGenerationTests.cs` (+1 tuple test).

## (d') Coverage re-run — NEW hunks (set-op `_from`, System.Tuple guard) — 2026-10-02T08:44Z (still DO, #146-A NOT done)

- Reproduced CI: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage-146a-ci.cobertura.xml "dotnet test nextorm.slnx -c Debug --no-build"` -> **EXIT=0** | `/tmp/opencode/146a/r2-final-coverage-ci-collect.log`, `r2-final-coverage-collect-exit.txt`.
- Test run inside collect: total **7596**, failed **0**, succeeded **5199**, skipped **2397**, duration 18.1s (no `DOCKER_HOST`; container providers skipped, SQLite integration ran) | `r2-final-coverage-ci-collect.log`.
- reportgenerator: `-reports:tests/coverage/coverage-146a-ci.cobertura.xml -targetdir:tests/coverage/report-146a-ci -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"` -> **EXIT=0** | `r2-final-coverage-report.log`.
- **Overall: Line 86.6% (41337/47729) vs 85 -> PASS; Branch 78.0% (21014/26939) vs 75 -> PASS.** Per module: core 86.8%, sqlite 89.3%, postgres 79.2%, sqlserver 78.2%. Summary `tests/coverage/report-146a-ci/Summary.txt`.
- Per-type for the two changed files (from the fresh `report-146a-ci/Cobertura.xml`):
  - `QueryCommand.TResult.cs` — `NextORM.Core.QueryCommand` line 95.8% (92/96), branch 75.0% (30/40); `NextORM.Core.QueryCommand<TResult>` line 88.4% (730/826), branch 67.1% (102/152).
  - `QueryCommand.QueryPreparer.cs` — `QueryCommand.QueryPreparer` line 94.1% (2210/2348), branch 86.1% (1578/1832).
- Per-type typed-CTE: `Cte<TResult>` 100/100; `CteDefinition` 100% line (0 branch); `CteHoister` 95.6/93.8 (`Walker` 91.2/88.8); `CteMerge` 100/100; `CteMutation` 93.3/90.0; `CteQuery` 100% line (0 branch); `SqlBuildContext` 100% line (0 branch); `SqlSourceRenderer` 94.5/87.6; `MemberTranslator` 87.2/79.7.
- 2026-10-02T08:44Z | DO | r1 | 1/3 | coverage re-run on new hunks: collect EXIT=0, test total 7596/failed 0/skipped 2397; Line 86.6%, Branch 78.0% (both PASS) | tests/coverage/report-146a-ci/Summary.txt, /tmp/opencode/146a/r2-final-coverage-ci-collect.log, r2-final-coverage-report.log

## (e') Targeted mutation — NEW hunks (set-op `_from` save/restore, System.Tuple fail-fast) — 2026-10-02T08:44Z (still DO, #146-A NOT done)

- Method: manual, md5-verified (byte backup -> exact 1-occurrence replace -> `dotnet build tests/<proj> -c Debug --no-restore` -> focused `dotnet test ... --no-build --filter FullyQualifiedName~<test>` -> restore bytes -> md5 check). Logs: `/tmp/opencode/146a/mut-newhunks/`, `mutation-newhunks.log`, `mutation-newhunks2.log`.
- **Hunk (a) `QueryCommand.TResult.cs` set-op `_from` save/restore (lines 962-1051):**
  - `A1` `Union` drops the restore (line 970, `- cmd._from = source;`) -> **KILLED** by core `TypedCte_UnionBody_ShouldHoistDependencyReferencedInABranch` (build 0/0; test exit 2, total 1 failed 1). The same mutation run against sqlite `UnionBody_ShouldMaterializeAndHoistConsumer` survives because both operands there are mapped entities (`_from` is re-derived from the definition) — the hunk is still killed by the core test, so not an open survivor.
  - `A2` `UnionAll` drops the restore (line 986) -> initially **SURVIVOR** against `TypedCte_UnionBodyAsDependency_ShouldOrderBeforeConsumer` (that test also uses mapped operands). Added killing test (tests only) `TypedCte_UnionAllBody_ShouldPreserveTypedCteBranch` in `tests/nextorm.core.tests/TypedCteTests.cs`; re-run -> **KILLED** (build 0/0; test exit 2, total 1 failed 1). New test green unmutated.
- **Hunk (b) `QueryCommand.QueryPreparer.cs:383-391` System.Tuple fail-fast:**
  - `B1` `ColumnShape` gate disabled: `is { Length: > 0 }` -> `is { Length: > 1000000 }` (line 383) -> **KILLED** by core `TypedCte_SystemTupleForm_ShouldFailFast` **and** postgres `TypedCte_SystemTupleProjection_ShouldFailFast` (both build 0/0; test exit 2, total 1 failed 1). (The naive inverse `{ Length: < 0 }` is a compile error CS8518 — array length can never be negative — so the runtime-false form was used.)
  - `B2` predicate swapped: `TypeFacts.IsTupleType` -> `TypeFacts.IsValueTupleType` (line 387) -> **KILLED** by core `TypedCte_SystemTupleForm_ShouldFailFast`.
- Restore: every mutation restored, post-md5 == pre-md5 (`QueryCommand.TResult.cs` `8d789ad2f4792379845bf8ee4cb95626`, `QueryCommand.QueryPreparer.cs` `cf105d170ff930fdbc2ca823468effee`); `rg` for mutation residue -> none; `git diff --stat` unchanged (production: TResult +49, QueryPreparer +124).
- **New hunks: killed 4/4 targeted mutations (a: 2/2, b: 2/2), 0 open survivors.** One additional killing test added (tests only).
- Build: `dotnet build nextorm.slnx -c Debug` -> **EXIT=0, 0 warnings / 0 errors** | `/tmp/opencode/146a/r2-sln-debug-final.log`.
- Focused green post-restore: core `TypedCte_*` **32/32** (was 31, +1 new test), sqlite `UnionBody_ShouldMaterializeAndHoistConsumer` 1/1, postgres `TypedCte_SystemTupleProjection_ShouldFailFast` 1/1 — all exit 0.
- 2026-10-02T08:44Z | DO | r1 | 1/3 | mutation on new hunks: hunk (a) 2/2 killed (Union drop-restore killed by core Hoist test; UnionAll drop-restore required new `TypedCte_UnionAllBody_ShouldPreserveTypedCteBranch`), hunk (b) 2/2 killed (gate disable + predicate swap); all restored md5-identical | /tmp/opencode/146a/mutation-newhunks.log, mutation-newhunks2.log, mut-newhunks/summary*.json

### Status
- **DO complete — entering CHECK** (unchanged). New/changed production this stream: none (only a tests-only killing test added: `tests/nextorm.core.tests/TypedCteTests.cs`). Production md5s restored to pre-run values; no commit, no push; #146-A not closed. All edited files CRLF.
- Coverage: Line 86.6% / Branch 78.0% (both PASS); mutation new hunks 4/4 killed, 0 open survivors.

## (f') 7-case cached-path acceptance re-run — query-path change (`QueryCommand.TResult.cs` set-op `_from` save/restore) — 2026-10-02T08:46Z (status unchanged: DO complete — entering CHECK)

- 2026-10-02T08:46Z | DO | r1 | 1/3 | acceptance re-run (f') after query-path change: command `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; exit **0**; **7** cases selected, **0** failures; BDN `Global total time` **42.17 s** (executed benchmarks: 7); external shell wall clock **45 s** — both under the 4 min budget. Cache-vs-prepared ratio **1.96** (1,844.4 us / 941.2 us) vs baseline **1.87** (+4.8%), below the >20% investigate trigger **2.244**; allocated ratio **7.90** (601.16 / 76.14). Tracked BDN artifacts restored before and after; `git status --short benchmarks` and `git status --short BenchmarkDotNet.Artifacts` both empty. | /tmp/opencode/146a/acceptance-146a-r2.log

| Case | Mean | Allocated |
|------|------|-----------|
| `Nextorm_Count` | 2.322 ms | 372.66 KB |
| `Nextorm_GroupByCount` | 71.88 ms | 50.06 MB |
| `Nextorm_Cached` | 2.185 ms | 571.15 KB |
| `Prepared_ToList` | 941.2 us | 76.14 KB |
| `Cached_ToList` | 1,844.4 us | 601.16 KB |
| `Cached_PlanOnly_Param` | 562.8 us | 525.02 KB |
| `Nextorm_Cached_ToListAsync` | 2.268 ms | 736.6 KB |

- Trigger of this re-run: the query path changed since the prior (f) run — `src/nextorm.core/Query/QueryCommand.TResult.cs` set-operation `_from` save/restore around `ResetPreparation` (matrix-closure union-body dependency fix). The 7-case acceptance is the cached-path protection mandated by the PDCA overlay for query-path / plan-cache changes; baseline **1.87** and trigger **2.244** as recorded in `docs/specs/performance/acceptance-benchmarks.md`.
- Verdict: within noise, **no regression** — exit 0, 7/7, 0 failures, **1.96 < 2.244** (+4.8% vs baseline, far below the 20% threshold); allocated ratio **7.90** (baseline 7.42) unchanged order. `Nextorm_Count` remains the high-variance non-assertion row.
- Status: **DO complete — entering CHECK** (unchanged); no commit, no push; #146-A not closed.

## Final consolidated unit ledger — 2026-10-02T13:47Z (status: DO complete — entering CHECK)

- Build: `dotnet build nextorm.slnx -c Debug` → **EXIT=0**, 0 warnings / 0 errors | `/tmp/opencode/146a/r3-sln-debug.log`
- Runner: `dotnet test tests/<project> -c Debug --no-build` (Microsoft.Testing.Platform). All 10 projects **EXIT=0**, 0 failed. Logs `/tmp/opencode/146a/r3-test-<project>.log`.

| Project | Total | Succeeded | Failed | Skipped |
|---------|------:|----------:|-------:|--------:|
| nextorm.core.tests | 1314 | 1314 | 0 | 0 |
| nextorm.sqlite.tests | 894 | 893 | 0 | 1 |
| nextorm.postgres.tests | 739 | 739 | 0 | 0 |
| nextorm.sqlserver.tests | 551 | 551 | 0 | 0 |
| nextorm.mysql.tests | 255 | 255 | 0 | 0 |
| nextorm.mariadb.tests | 155 | 155 | 0 | 0 |
| nextorm.clickhouse.tests | 483 | 483 | 0 | 0 |
| nextorm.clickhouse.extensions.tests | 23 | 23 | 0 | 0 |
| nextorm.entityframeworkcore.tests | 126 | 126 | 0 | 0 |
| nextorm.alias.tests | 37 | 37 | 0 | 0 |
| **Subtotal** | **4577** | **4576** | **0** | **1** |

- Arithmetic: succeeded 1314+893+739+551+255+155+483+23+126+37 = **4576**; total 1314+894+739+551+255+155+483+23+126+37 = **4577**.
- The single skip is the pre-existing capability probe `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` (`NextORM.Sqlite.Tests`), which requires `NEXTORM_LOB_SQLITE_PROBE=1` | `/tmp/opencode/146a/r3-test-nextorm.sqlite.tests.log`.
- **No failed tests in any of the 10 unit projects.**
- Status: **DO complete — entering CHECK**; no commit, no push.
- 2026-10-02T13:56Z | PLAN | r2 | 1/3 | Replanned r1→r2: r1 exhausted after 3 evidence-gate CHECK failures (branch-delta / mutation-provenance / process-security-integrity evidence incomplete; no product defect). r2 = evidence remediation; production frozen at `c7f939d`, no code change, slice B out of scope. | this file `## Plan (r2)` — `docs/specs/status/typed-cte-146a-1.md:18-99`

## #146-A regression triage — RawCommandTests.Transaction_RollbackUndoesRawInsert — 2026-10-02T09:21Z

- **Verdict: NOT a #146-A production regression** (test/environment artifact). No production code changed; no commit, no push.
- Symptom, only in the `after` sealed worktree: whole-project `dotnet test tests/nextorm.sqlite.tests -c Debug` fails `RawCommandTests.Transaction_RollbackUndoesRawInsert` with `SQLite Error 1: 'near "from": syntax error'`. An instrumented dump of the emitted SQL shows the LINQ `ctx.From<ISimpleEntity>().Select(x => x.Id)` rendering `select  from simple_entity` (empty select list); the test passes standalone. Not coverage-specific: `after` fails with and without a coverage collect, and with `--parallel none`.
- Reproduced where: `after` fails deterministically (5/5, EXIT=2, 892 succeeded / 1 failed / 1 skipped); `before` (HEAD) green; **main working tree (frozen #146-A impl) authoritative command green EXIT=0 (894 total / 0 failed / 1 skipped)**, repeated; core 1314/0, postgres 739/0.
- Artifact evidence — byte-identical binaries: `nextorm.sqlite.tests.dll` sha256 `a875c84e67733457c17037442c5547804e0b377100fffacdbc950b6b7ebbe8bb` and `nextorm.core.dll` sha256 `398f267d869aa3c508643e117dd6b41de53cfda22292b3ab6ed6da1a3604049e` are **equal** in main and `after`, and `diff -rq` of the whole Debug app dirs returns 0 (all files byte-identical). With those same bytes: (a) main `dotnet test` EXIT=0; (b) `after` `dotnet test` EXIT=2; (c) same bytes at the main path, run from the `after` cwd via `dotnet exec`, PASS, while the `after` path FAILs (also under `-parallel none` / `--parallel none`). ⇒ the compiled #146-A code is not the differentiator; the failure is tied to the `after` worktree path/runner context. **Root cause of the artifact itself not isolated.**
- Separate **non-#146-A** observation, out of scope, must not be fixed here: the empty select list is consistent with process-wide `DataContextCache.SelectListCache` pollution — read at `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:628` (`TryGetValue`) and write at `:666` (`SelectListCache[srcType] = selectList`), surfacing as `select  from simple_entity`. Track separately.
- Logs: `/tmp/opencode/146a/regression/` — `resume-main-{sqlite,core,postgres}.log`, `phase1-main-*.log`, `iso-{main,after}.log`, `iso-exec-*.log`, `bindiff.txt`. Temporary diagnostic `tests/nextorm.sqlite.tests/ZDiagModule.cs` removed; solution build 0 warnings / 0 errors.

## D3 process replay — real-exit process ledger — 2026-10-02T09:30Z (r2)

Wrapper captured the process exit directly (`"$@" >"$LOG" 2>&1; rc=$?`), never a pipeline. All commands ran in the MAIN tree `/home/alex/sources/nextorm`; logs under `/tmp/opencode/146a/r2/`. Machine-readable ledger: `/tmp/opencode/146a/r2/processes.tsv`.

| id | rc | wall s | counts | command | log |
|---|---:|---:|---|---|---|
| `build-debug` | 0 | 2 | warnings=0;errors=0 | `dotnet build nextorm.slnx -c Debug` | `/tmp/opencode/146a/r2/build-debug.log` |
| `build-release` | 0 | 4 | warnings=0;errors=0 | `dotnet build nextorm.slnx -c Release` | `/tmp/opencode/146a/r2/build-release.log` |
| `unit-nextorm.core.tests` | 0 | 7 | total=1314;failed=0;succeeded=1314;skipped=0 | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.core.tests.log` |
| `unit-nextorm.sqlite.tests` | 0 | 9 | total=894;failed=0;succeeded=893;skipped=1 | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.sqlite.tests.log` |
| `unit-nextorm.postgres.tests` | 0 | 2 | total=739;failed=0;succeeded=739;skipped=0 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.postgres.tests.log` |
| `unit-nextorm.sqlserver.tests` | 0 | 2 | total=551;failed=0;succeeded=551;skipped=0 | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.sqlserver.tests.log` |
| `unit-nextorm.mysql.tests` | 0 | 1 | total=255;failed=0;succeeded=255;skipped=0 | `dotnet test tests/nextorm.mysql.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.mysql.tests.log` |
| `unit-nextorm.mariadb.tests` | 0 | 1 | total=155;failed=0;succeeded=155;skipped=0 | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.mariadb.tests.log` |
| `unit-nextorm.clickhouse.tests` | 0 | 2 | total=483;failed=0;succeeded=483;skipped=0 | `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.clickhouse.tests.log` |
| `unit-nextorm.clickhouse.extensions.tests` | 0 | 2 | total=23;failed=0;succeeded=23;skipped=0 | `dotnet test tests/nextorm.clickhouse.extensions.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.clickhouse.extensions.tests.log` |
| `unit-nextorm.entityframeworkcore.tests` | 0 | 2 | total=126;failed=0;succeeded=126;skipped=0 | `dotnet test tests/nextorm.entityframeworkcore.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.entityframeworkcore.tests.log` |
| `unit-nextorm.alias.tests` | 0 | 4 | total=37;failed=0;succeeded=37;skipped=0 | `dotnet test tests/nextorm.alias.tests -c Debug --no-build` | `/tmp/opencode/146a/r2/unit-nextorm.alias.tests.log` |
| `coverage-collect` | 0 | 17 | total=7596;failed=0;succeeded=5199;skipped=2397 | `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o /tmp/opencode/146a/r2/r2.cobertura.xml dotnet test nextorm.slnx -c Debug --no-build` | `/tmp/opencode/146a/r2/coverage-collect.log` |
| `coverage-report` | 0 | 3 | line=86.6%;branch=78% | `dotnet tool run reportgenerator -reports:/tmp/opencode/146a/r2/r2.cobertura.xml -targetdir:/tmp/opencode/146a/r2/report-r2 -reporttypes:Html;TextSummary;Cobertura -riskhotspotassemblyfilters:+nextorm.*` | `/tmp/opencode/146a/r2/coverage-report.log` |
| `docfx` | 0 | 42 | warnings=2;errors=0 | `dotnet docfx docs/docfx.json` | `/tmp/opencode/146a/r2/docfx.log` |
| `integration` | 0 | 57 | Total=3019;Errors=0;Failed=0;Skipped=187;NotRun=0;Time=36.946s | `env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor` | `/tmp/opencode/146a/r2/integration.log` |
| `benchmark-acceptance` | 0 | 51 | cases=7;failures=0;global=00:00:50(50.03s);executed=7;ratio=2.02 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` | `/tmp/opencode/146a/r2/benchmark-acceptance.log` |

- Builds: Debug exit **0 / 0 warnings / 0 errors**; Release exit **0 / 0 warnings / 0 errors**.
- Units: **10/10 projects exit 0**; subtotal **4577 total / 4576 succeeded / 0 failed / 1 skipped** (the single skip is the pre-existing `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` capability probe, needs `NEXTORM_LOB_SQLITE_PROBE=1`).
- Coverage: collect exit **0** (total 7596 / failed 0 / succeeded 5199 / skipped 2397; no `DOCKER_HOST`, container providers skipped); reportgenerator exit **0**; **Line 86.6% (41337/47729) ≥85 PASS**, **Branch 78.0% (21014/26939) ≥75 PASS**; `/tmp/opencode/146a/r2/report-r2/Summary.txt`.
- Coverage sqlite `after`-style artifact: **not present in main** — `unit-nextorm.sqlite.tests` exit 0 (894 total / 893 succeeded / 1 skip) and the `nextorm.slnx` collect inside `coverage-collect` exit 0.
- DocFX: **exit 0**; **2 warnings / 0 errors**. Classification: both warnings are pre-existing `Duplicate source file` diagnostics for `src/nextorm.core.sourcegenerator/AnalyzerReleases.Shipped.md` and `.../AnalyzerReleases.Unshipped.md` in `nextorm.core.sourcegenerator.csproj`; **unrelated to #146-A** (no guide/API page involved) and identical to the pre-existing baseline `docfx-baseline.log`.
- Integration: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock … -- -noColor`; socket present (no `machine start`); **exit 0**; Total 3019, Errors 0, **Failed 0**, Skipped 187, Not Run 0, Time 36.946s. All 5 provider containers + SQLite ran.
- Acceptance: **exit 0**; **7 cases, 0 failures**, BDN `Global total time` **50.03 s** (≤240 s budget); cached-vs-prepared ratio **2.02** (1,785.2 us / 885.6 us) vs baseline 1.87, below investigate trigger **2.244**; allocated ratio **7.90**. Note: the actually-tracked BDN artifact dir is root `BenchmarkDotNet.Artifacts/` (18 tracked files); it was cleaned with `git checkout --` before/after. The brief-named `benchmarks/BenchmarkDotNet.Artifacts/` was already clean. `git status --short benchmarks BenchmarkDotNet.Artifacts` empty after cleanup.

## D3 mutation replay — serial, md5-verified — 2026-10-02T09:30Z (r2)

Method: each trial applied as an exact 1-occurrence byte replacement in the MAIN tree; `dotnet test tests/<proj> -c Debug --filter FullyQualifiedName~<exact test>` (**no `--no-build`**) captured the mutated exit; file reverted, `md5sum -c` against the preimage verified, then the same command captured the restored exit. A kill requires a nonzero mutated test exit (a compile/BUILD_FAIL is not a behavioural kill) **and** restored exit 0. Result: **20 killed / 22 trials, 2 justified equivalents (M8, M14), 0 unexplained survivors**. Machine-readable: `/tmp/opencode/146a/r2/mutation-results.tsv`; per-trial logs `/tmp/opencode/146a/r2/mut-replay/<id>.{mutated,restored}.log`.

| id | file:line | mutation | project | filter | mutated | restored | verdict |
|---|---|---|---|---|---|---|---|
| `M1` | `src/nextorm.core/Cte.cs:30` | Cte<T> stops marking its definition TypedProjection=true | `nextorm.postgres.tests` | `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M2` | `src/nextorm.core/Cte.cs:50` | Cte<T>.Name read-only surface returns wrong value | `nextorm.core.tests` | `TypedCte_Descriptor_ShouldExposeNameAndSingleDefinition` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M3` | `src/nextorm.core/Builders/CteHoister.cs:360` | Reachable never omits unreferenced candidates | `nextorm.core.tests` | `Reachable_ShouldIncludeRootAndReferencedOnly` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M4b` | `src/nextorm.core/Builders/CteHoister.cs:355` | Reachable never traverses dependencies (compiling form) | `nextorm.core.tests` | `Reachable_ShouldIncludeRootAndReferencedOnly` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M5b` | `src/nextorm.core/Builders/CteHoister.cs:295` | CollectReferencedNames mangles a referenced table name | `nextorm.core.tests` | `Reachable_ShouldIncludeRootAndReferencedOnly` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M6` | `src/nextorm.core/DataContext/SqlSourceRenderer.cs:86` | MakeWithClause does not enable exact aliases for typed CTE body | `nextorm.postgres.tests` | `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M7` | `src/nextorm.core/DataContext/SqlSourceRenderer.cs:1066` | MakeColumn falls back to case-insensitive comparison | `nextorm.postgres.tests` | `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M8` | `src/nextorm.core/Visitors/MemberTranslator.cs:721` | MakeInnerColumn emits a bogus identifier (equivalent on the NeedAlias==true path) | `nextorm.postgres.tests` | `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` | exit=0,total=1,failed=0 | exit=0,total=1,failed=0 | **SURVIVOR** |
| `M8c` | `src/nextorm.core/Visitors/MemberTranslator.cs:721` | MakeInnerColumn forces NeedAliasForColumn=false | `nextorm.postgres.tests` | `TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M9` | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:990` | main-source filter guard disabled for ColumnShape source | `nextorm.core.tests` | `TypedCte_WholeEntitySource_ShouldNotReapplyEntityFilterOnOuterRead` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M10` | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:1188` | join filter guard disabled for ColumnShape source | `nextorm.core.tests` | `TypedCte_JoinSource_ShouldNotInjectEntityFilterIntoJoin` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M11` | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:446` | ValueTuple fail-fast guard disabled | `nextorm.core.tests` | `TypedCte_ValueTupleForm_ShouldFailFast` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M12` | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:900` | ResolveShapeConverter drops the column converter | `nextorm.sqlite.tests` | `Converter_ProjectedBody_ShouldRoundTripWithoutDoubleConversion` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M13b` | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:232` | PrepareFrom skips preparing an unprepared ColumnShape | `nextorm.sqlite.tests` | `Converter_ProjectedBody_ShouldRoundTripWithoutDoubleConversion` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M14` | `src/nextorm.core/Query/QueryCommand.TResult.cs:1081` | AsCte stops rejecting an empty CTE name (equivalent: CteDefinition ctor rejects) | `nextorm.core.tests` | `TypedCte_AsCte_ShouldRejectNullOrEmptyName` | exit=0,total=1,failed=0 | exit=0,total=1,failed=0 | **SURVIVOR** |
| `M15` | `src/nextorm.core/DataContext/DataContextExtensions.cs:1258` | From(Cte<T>) does not attach the descriptor declarations | `nextorm.core.tests` | `TypedCte_ReferencingCte_ShouldOrderDependencyBeforeConsumer` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M16b` | `src/nextorm.core/DataContext/DataContextExtensions.cs:1257` | From(Cte<T>) loses the ColumnShape projection marker | `nextorm.core.tests` | `TypedCte_JoinSource_ShouldNotInjectEntityFilterIntoJoin` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `M17` | `src/nextorm.core/Builders/CteQuery.cs:78` | TypedProjection defaults true, corrupting legacy With declaration aliases | `nextorm.postgres.tests` | `Cte_NestedBody_ShouldHoistIntoSingleTopLevelWith` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `A1` | `src/nextorm.core/Query/QueryCommand.TResult.cs:968` | new hunk: Union drops _from restore | `nextorm.core.tests` | `TypedCte_UnionBody_ShouldHoistDependencyReferencedInABranch` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `A2` | `src/nextorm.core/Query/QueryCommand.TResult.cs:984` | new hunk: UnionAll drops _from restore | `nextorm.core.tests` | `TypedCte_UnionAllBody_ShouldPreserveTypedCteBranch` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `B1` | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:383` | new hunk: System.Tuple fail-fast ColumnShape gate made runtime-false | `nextorm.core.tests` | `TypedCte_SystemTupleForm_ShouldFailFast` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |
| `B2` | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:387` | new hunk: System.Tuple predicate swapped to ValueTuple -> no throw | `nextorm.core.tests` | `TypedCte_SystemTupleForm_ShouldFailFast` | exit=2,total=1,failed=1 | exit=0,total=1,failed=0 | **KILLED** |

- M8 and M14 survive by **equivalence**, independently justified in `/tmp/opencode/146a/r2/mutation-equivalence.md`:
  - **M8** `MemberTranslator.cs:721`: `Column` is dead on the `NeedAliasForColumn == true` path — the three consumers read it only on the false branch (`:301-307`, `:563-566`, `:697-700`), while the mutation keeps the flag `true`; the sibling **M8c** (`true`→`false`) is KILLED by the same test, proving the branch is live.
  - **M14** `QueryCommand.TResult.cs:1081`: `CteDefinition` ctor independently rejects empty at `CteQuery.cs:24 ArgumentException.ThrowIfNullOrEmpty(name)`, so `ThrowIfNull` yields the same observable `ArgumentException`.
- No-residue proof: `md5sum -c /tmp/opencode/146a/r2/preimage.md5` → all 8 production files **OK**; `git diff --stat` **byte-identical to the frozen #146-A baseline** (16 files, +662/−23); no BDN artifact residue.

## D3 security disposition — independent — 2026-10-02T09:30Z (r2)

Recorded verbatim from the independent `security-auditor` stream (`/tmp/opencode/146a/r2/security-disposition.md`):

> raw-name emission = **Suggestion / not a demonstrated vulnerability** (consumer-controlled identifiers; no untrusted ingestion in-solution; `QuoteIdentifiers` default false is documented compatibility behavior), with a **separate Medium conditional Warning** on ClickHouse backtick escaping not escaping backslashes (`ClickHouseDialect.cs:469`, deferred in current milestone with triggers: untrusted-name ingestion / changed quoting defaults / exploitability witness).

- 2026-10-02T09:30Z | DO | r2 | 1/3 | D3 process replay (build Debug/Release, 10/10 units, coverage 86.6/78.0, docfx 0/2w, integration 0/3019/0failed, acceptance 7/0 ratio 2.02) + mutation replay 20/22 killed + 2 justified equivalents (M8, M14) + security disposition recorded; no residual mutation; production frozen | `/tmp/opencode/146a/r2/processes.tsv`, `mutation-results.tsv`, `mutation-equivalence.md`, `security-disposition.md`

### D3 status
- **D3 done.** All process exits real (wrapper, not pipelines); mutation replay serial and md5-verified; security verdict recorded verbatim. Production/tests/docs unchanged from the frozen #146-A set; no commit, no push. No new #146-A product defect found in this run.

---

## #146-A r2 — acceptance per-case table + suppression scan — 2026-10-02T09:36Z (r2)

- 2026-10-02T09:36Z | DO | r2 | 1/3 | acceptance per-case table (7/7 cases, 0 failures; primary r2 bundle `Global total time` 00:00:50/50.03s ratio 2.02, D3 re-run 00:00:42/42.17s ratio 1.96 — both < trigger 2.244) + suppression/slop scan of the 9 changed production files (total 0 matches; manual, `slopwatch` not installed) | `/tmp/opencode/146a/r2/benchmark-acceptance.log`, `/tmp/opencode/146a/acceptance-146a-r2.log`, `/tmp/opencode/146a/r2/perf-table.md`, `/tmp/opencode/146a/r2/suppression-scan.md`

# #146-A r2 — acceptance per-case table (evidence only)

Task: `#146-A r2`, branch `1.0.9-b`. Source logs: `/tmp/opencode/146a/r2/benchmark-acceptance.log`
(r2 CHECK bundle, `L14` in `processes.tsv`/`acceptance-map.md`, captured 2026-10-02) and the
earlier D3 re-run `/tmp/opencode/146a/acceptance-146a-r2.log` (recorded in
`typed-cte-146a-1.md:323`). No `$E/acceptance-r2.log` exists; the acceptance log actually
inside `$E` is `$E/benchmark-acceptance.log`. Both 7-case runs are reported; the r2-bundle run
is primary.

Both runs: command `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`;
`Job.ShortRun` (`IterationCount=3`, `WarmupCount=3`, `LaunchCount=1`), `InProcessEmitToolchain`,
`MemoryDiagnoser` on; host AMD Ryzen 7 5800HS, Ubuntu 22.04.5, .NET 10.0.12, BenchmarkDotNet 0.15.8.

## Primary — r2 bundle (`$E/benchmark-acceptance.log`), exit 0

Global total time: `00:00:50 (50.03 sec)`; executed benchmarks: **7**; failures: **0**.
Category tags: cases 1–2 = `acceptance,InMemoryNew`; cases 3–7 = `acceptance`.

| # | Benchmark (Class.Method) | Mean | Error | StdDev | Allocated |
|---|--------------------------|------|-------|--------|-----------|
| 1 | `InMemoryBenchmarkAggregates.Nextorm_Count` | 2.178 ms | 0.4205 ms | 0.0230 ms | 372.66 KB |
| 2 | `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | 59.43 ms | 106.72 ms | 5.849 ms | 50.05 MB |
| 3 | `SqliteBenchmarkAny.Nextorm_Cached` | 1.818 ms | 0.2049 ms | 0.0112 ms | 571.14 KB |
| 4 | `SqliteBenchmarkCachedPlan.Prepared_ToList` | 885.6 us | 162.30 us | 8.90 us | 76.14 KB |
| 5 | `SqliteBenchmarkCachedPlan.Cached_ToList` | 1,785.2 us | 123.82 us | 6.79 us | 601.16 KB |
| 6 | `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | 520.0 us | 94.19 us | 5.16 us | 525.02 KB |
| 7 | `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | 2.156 ms | 0.3546 ms * | 0.0194 ms * | 736.6 KB |

\* Case 7: BDN `HideColumnsAnalyser` hides `Error`/`StdDev` from the summary **column**, but the
run header still prints them (`Error=0.3546 ms  StdDev=0.0194 ms`). `Error` is therefore present
for all 7 cases in the log; no case is missing it.

## Secondary — D3 re-run (`/tmp/opencode/146a/acceptance-146a-r2.log`), exit 0

Global total time: `00:00:42 (42.17 sec)`; executed benchmarks: **7**; failures: **0**.

| # | Benchmark (Class.Method) | Mean | Error | StdDev | Allocated |
|---|--------------------------|------|-------|--------|-----------|
| 1 | `InMemoryBenchmarkAggregates.Nextorm_Count` | 2.322 ms | 0.5526 ms | 0.0303 ms | 372.66 KB |
| 2 | `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | 71.88 ms | 134.30 ms | 7.362 ms | 50.06 MB |
| 3 | `SqliteBenchmarkAny.Nextorm_Cached` | 2.185 ms | 2.074 ms | 0.1137 ms | 571.15 KB |
| 4 | `SqliteBenchmarkCachedPlan.Prepared_ToList` | 941.2 us | 249.5 us | 13.67 us | 76.14 KB |
| 5 | `SqliteBenchmarkCachedPlan.Cached_ToList` | 1,844.4 us | 577.5 us | 31.65 us | 601.16 KB |
| 6 | `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | 562.8 us | 255.5 us | 14.01 us | 525.02 KB |
| 7 | `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | 2.268 ms | 0.6168 ms * | 0.0338 ms * | 736.6 KB |

## Baseline (source of truth) and measured ratio

Baseline `docs/specs/performance/acceptance-benchmarks.md`: `Prepared_ToList` **923.8 us**,
`Cached_ToList` **1,727.4 us**, ratio **1.87**, investigate trigger **2.244** (= 1.87 × 1.20),
alloc ratio **7.42**. All 7 baseline cases:

| Case | Mean | Error | StdDev | Allocated |
|------|------|-------|--------|-----------|
| `Nextorm_Count` | 2.915 ms | 10.458 ms | 0.5733 ms | 335.17 KB |
| `Nextorm_GroupByCount` | 60.95 ms | 13.69 ms | 0.750 ms | 50 MB |
| `Nextorm_Cached` | 1.772 ms | 0.2600 ms | 0.0143 ms | 534.42 KB |
| `Prepared_ToList` | 923.8 us | 185.40 us | 10.16 us | 76.14 KB |
| `Cached_ToList` | 1,727.4 us | 159.21 us | 8.73 us | 565.22 KB |
| `Cached_PlanOnly_Param` | 518.3 us | 91.43 us | 5.01 us | 489.08 KB |
| `Nextorm_Cached_ToListAsync` | 2.137 ms | — | — | 692.07 KB |

Measured cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`, same run):

| Run | Prepared_ToList | Cached_ToList | Ratio | Δ vs 1.87 | Trigger 2.244 | Alloc ratio |
|-----|-----------------|---------------|-------|-----------|---------------|-------------|
| r2 bundle (primary) | 885.6 us | 1,785.2 us | **2.02** | +7.8% | below (2.02 < 2.244) | 7.90 |
| D3 re-run | 941.2 us | 1,844.4 us | **1.96** | +4.8% | below (1.96 < 2.244) | 7.90 |

Verdict: neither run crosses the >20% investigate trigger **2.244**; both are within run-to-run
noise (`Cached_ToList` `Error` 123.82 us / 577.5 us on a 3-iteration `ShortRun`) and under the
≤240 s budget. No cached-path regression attributable to #146-A.

## BDN case categories → the 7 cases

| BDN category tag | Cases |
|------------------|-------|
| `acceptance,InMemoryNew` | 1 `InMemoryBenchmarkAggregates.Nextorm_Count`; 2 `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` |
| `acceptance` | 3 `SqliteBenchmarkAny.Nextorm_Cached`; 4–6 `SqliteBenchmarkCachedPlan` (`Prepared_ToList`, `Cached_ToList`, `Cached_PlanOnly_Param`); 7 `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` |

`--anyCategories=acceptance` selects exactly these 7 methods (5 classes); `InMemoryNew` is an
additional tag on the two in-memory cases only. `Cached_PlanOnly_Param` is a no-database case;
its raw ratio-to-`Prepared_ToList` is not comparable (per baseline doc) and is excluded from the
cached-vs-prepared rule.

---

# #146-A r2 — suppression / slop counter scan (evidence only)

Scope: the 9 #146-A changed production files only. Pattern:
`SuppressMessage|NoWarn|#pragma warning|\bSuppress\b`.

Method: `slopwatch` is **not installed locally** — `.config/dotnet-tools.json` lists only
`dotnet-coverage` (18.11.2), `dotnet-reportgenerator-globaltool` (5.5.11) and `docfx` (2.78.5),
so no `slopwatch`/quality-gate CLI was available. The scan was done **manually** with
ripgrep (`rg -n -e 'SuppressMessage|NoWarn|#pragma warning|\bSuppress\b' <9 files>`), a plain
text/attribute scan; no C# symbol resolution was needed.

Per-file match count:

| # | File | Matches |
|---|------|--------:|
| 1 | `src/nextorm.core/Cte.cs` | 0 |
| 2 | `src/nextorm.core/Builders/CteQuery.cs` | 0 |
| 3 | `src/nextorm.core/Builders/CteHoister.cs` | 0 |
| 4 | `src/nextorm.core/DataContext/SqlBuildContext.cs` | 0 |
| 5 | `src/nextorm.core/DataContext/SqlSourceRenderer.cs` | 0 |
| 6 | `src/nextorm.core/Visitors/MemberTranslator.cs` | 0 |
| 7 | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` | 0 |
| 8 | `src/nextorm.core/Query/QueryCommand.TResult.cs` | 0 |
| 9 | `src/nextorm.core/DataContext/DataContextExtensions.cs` | 0 |

- **Total matches: 0** (rg exit 1 = no match).
- Suppressed: **0**; justified: **0**; ratio suppressed/justified: **0/0** (n/a, no suppressions).
- No `[SuppressMessage]`, no `NoWarn`, no `#pragma warning`, no `Suppress*` identifier in any of
  the 9 #146-A production files. Nothing to justify or to un-suppress.
- Caveat: manual scan, not a `slopwatch` run — the tool is absent (see above); result is a
  grep-class text scan of the exact 9-file scope, not a full-repo slop audit.

## Audit-fix disposition (r2)

### Candidate table

| candidate | category | disposition | fix file:line | test | red→green log |
|---|---|---|---|---|---|
| 1. reset-operators losing `_from` (`Distinct`/`OrderBy`/`WithPaging`/`ForLast`/`WithForJson`/`WithForXml`) | query-state loss | **fixed** | `QueryCommand.TResult.cs` `ResetPreparationPreservingFrom` | core + sqlite | `/tmp/opencode/146a/auditfix/probe1-*`, `sqlite-*` |
| 2. `CloneWithoutUnion` | query-state loss | **fixed** (same helper `TResult.cs:1063`); in-memory set-op over a typed CTE is an out-of-scope provider limitation (no `ColumnShape` support) | `QueryCommand.TResult.cs:1063` | core + sqlite | `/tmp/opencode/146a/auditfix/sqlite-*` |
| 3. Tuple guard identity-read bypass | guard bypass | **benign** — fails fast with `QueryPreparationException`, never emits invalid SQL | `QueryCommand.QueryPreparer.cs` (existing tuple guard) | test pins fail-fast | `/tmp/opencode/146a/auditfix/probe1-*` |
| 4. unaliased computed scalar body | alias / rendering | **fixed** | `SelectExpression.OutputName`/`IsProjectionOutputReference`, `QueryPreparer` scalar alias, `SqlSourceRenderer.MakeColumn`, `SqlBuilder` alias | core `TypedCte_ComputedScalarBody_ShouldRenderReadableColumn` + sqlite `ComputedScalarBody_ShouldMaterializeThroughTypedCte` | `/tmp/opencode/146a/auditfix/probe1-*`, `sqlite-*` |
| 5. `ExactProjectionAliases` inherited by nested derived tables | flag inheritance | **fixed** | `SqlSourceRenderer.cs:531` clears the flag for nested derived tables | `TypedCte_NestedDerivedTableInBody_ShouldNotInheritExactProjectionAliases` | `/tmp/opencode/146a/auditfix/probe1-*` |
| 6. converter on a joined typed CTE | shape resolution | **fixed** | `ResolveShapeConverter`/`FindShapeConverter` now reads `cmd.Joins[i].From.ColumnShape` | sqlite `ConverterMemberOnJoinedTypedCte_ShouldRoundTrip` | `/tmp/opencode/146a/auditfix/sqlite-*` |
| 7. `CollectReferencedNames` physical-table-name over-inclusion | name collection | **fixed** | `CteHoister` `IsCteReference`/`CollectReferencedNames(knownNames)` | `Reachable_OnPreparedBody_ShouldIgnorePhysicalTableNameMatchingSiblingCteName` | `/tmp/opencode/146a/auditfix/probe1-*` |
| 8. `BuildDefinitions` snapshot | immutability boundary | **out-of-scope** — `QueryCommand.Ctes` setter is `internal` at `QueryCommand.cs:415`, so declarations cannot be added post-`AsCte` via public API | n/a | n/a | n/a |
| 9. `WalkFrom` not traversing `FromExpression.ColumnShape` | traversal | **fixed** | `CteHoister` `WalkFrom` | `EnsureNoUnhoistedCtes_ShouldDetectDeclarationOnColumnShape` | `/tmp/opencode/146a/auditfix/probe1-*` |
| 10. `Cte<TProjection>`→`Cte<TResult>` rename | naming | **done** | `Cte.cs`, EN/RU guide, `API-NAMING-REVIEW.md:4033,4043`, design spec | grep clean | n/a |

### 9-regression summary from the audit fixes

- **(A)** candidate-4 `OutputName="c0"` leaking into ordinary unnamed scalar projections → gated the alias fallback on `_ctx.ExactProjectionAliases` (`SqlBuilder.cs:513-516`).
- **(B)** candidate-7 `IsCteReference` gate dropped legacy forward sibling references → `CollectReferencedNames(body, knownNames)` / `IsCteReference(owner, from, knownNames)` with deterministic `IsMappedEntityTable(owner, table)` (not the process-wide cache); plus a `static`→instance compile fix at `CteHoister.cs:252`.
- All 9 prior failures resolved.

### Final verification (r2)

- **Final build:** Debug **EXIT 0**, Release **EXIT 0** (0 warnings / 0 errors).
- **Final 10-project ledger:** core 1327/1327/0/0; sqlite 901/900/0/1; postgres 739/739/0/0; sqlserver 551/551/0/0; mysql 255/255/0/0; mariadb 155/155/0/0; clickhouse 483/483/0/0; clickhouse.extensions 23/23/0/0; entityframeworkcore 126/126/0/0; alias 37/37/0/0 → **subtotal 4597 total / 4596 succeeded / 0 failed / 1 skipped** (only `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`).
- **Perf classification:** `MemberTranslator.cs:741` SQL-build; `QueryPreparer.cs:897` query-prep; `QueryPreparer.cs:385` query-prep; `CteHoister.cs:347` descriptor-creation — **none per-row**.
- **Statement:** **AC1–AC7 satisfied; slice B untouched**; status `DO (r2)`.


---

## Final-tree verification — 2026-10-02T10:36Z (still DO (r2), cycle 1, revision r2, attempt 1/3)

Integrity remediation after the disclosed harness incident. **No production change, no commit, no push.** The only tree delta from the frozen #146-A set is one tests-only killing test (MF6c below).

### 1. Tree integrity — byte-exact
- `md5sum` of the three recovered production files equals the pre-incident `preimage.md5` (captured 15:17Z): `SqlBuilder.cs 04f4f96aa06087097fb1915aa2328fb3`, `SqlSourceRenderer.cs ff8d5edadd40323fbfc5d6414f6b4c94`, `CteHoister.cs 08c3895e83273fb2b8e8c4dc4b9d19dc`; `md5sum -c /tmp/opencode/146a/final/preimage.md5` -> 3/3 OK. Non-empty (1852 / 1109 / 427 lines, CRLF).
- `git diff --stat` = frozen #146-A set: 19 files, +852/-42 (== `diffstat-baseline.txt` == `diffstat-final.txt`).
- Expected changed hunks present:
  - `SqlBuilder.cs:516` — `var alias = item.PropertyName ?? (_ctx.ExactProjectionAliases ? item.OutputName : null);` (alias fallback gated on `_ctx.ExactProjectionAliases`).
  - `SqlSourceRenderer.cs:535` — `var sql = new SqlBuilder(ctx with { ExactProjectionAliases = false }).MakeSelect(cmd);` (nested derived table clears the flag; brief cited `:531`, actual frozen byte line is 535; md5 == pre-incident preimage, so this is the correct file).
  - `CteHoister.cs:313` `WalkFrom` ColumnShape traversal (CollectReferencedNames), `:327` `IsCteReference`, `:364` `IsMappedEntityTable`, `:128` `EnsureNoUnhoistedCtes` ColumnShape traversal.
- Build `dotnet build nextorm.slnx -c Debug` -> **EXIT 0**, 0 warnings / 0 errors (`/tmp/opencode/146a/final2/build-debug.log`).
- Verdict: **integrity OK** — recovered bytes are the frozen pre-incident state.

### 2. MF6c mutation survivor closed (tests-only)
- New test: `tests/nextorm.core.tests/TypedCteTests.cs:993` `CollectReferencedNames_ShouldTraverseColumnShapeSource` — 3-level typed-CTE chain producer -> middle -> consumer where the middle source references the producer only through a `FromExpression.ColumnShape` marker (its own table name is not a declaration); asserts `CteHoister.Reachable` keeps all three declarations.
- **Red:** mutant `CollectReferencedNames.WalkFrom` ColumnShape traversal disabled -> focused test **EXIT 2**, total 1 / failed 1 / succeeded 0 (`/tmp/opencode/146a/final2/mf6c-mutant.log`, `mf6c-mutate-result.txt`).
- **Revert:** binary-exact; `md5(CteHoister.cs) 08c3895e...` == preimage (`md5=OK`).
- **Green:** restored run **EXIT 0**, total 1 / failed 0 / succeeded 1 (`mf6c-green.log`, `mf6c-green-restored.log`).
- Mutation tally: 11 previously killed + MF6c now killed = **12/12** (0 survivors; no equivalence left unproved).
- Defect history `MF6c` (`CteHoister.CollectReferencedNames` `WalkFrom` ColumnShape): seen in the r2 audit-fix set, survived the r2 whole-solution replay (FU6c INCONCLUSIVE; whole sln, 5 runs, no failure) -> **now killed at r2 attempt 1/3** by the above test; applied fixes: 0 production (tests-only) + this 1 killing test; evidence `mutation-final.tsv`, `/tmp/opencode/146a/final2/mf6c-*`.

### 3. Affected suites + Release build (after adding the test)
| project | exit | total | succeeded | failed | skipped | log |
|---|---|---|---|---|---|---|
| nextorm.core.tests | 0 | 1328 | 1328 | 0 | 0 | `/tmp/opencode/146a/final2/test-nextorm.core.tests.log` |
| nextorm.sqlite.tests | 0 | 901 | 900 | 0 | 1 | `/tmp/opencode/146a/final2/test-nextorm.sqlite.tests.log` |
| nextorm.postgres.tests | 0 | 739 | 739 | 0 | 0 | `/tmp/opencode/146a/final2/test-nextorm.postgres.tests.log` |
- sqlite's single skip is the allowed pre-existing capability skip.
- `dotnet build nextorm.slnx -c Release` -> **EXIT 0**, 0 warnings / 0 errors (`build-release.log`).

### 4. Live integration + container evidence
- Command: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor` -> **EXIT 0**; `Total: 3019, Errors: 0, Failed: 0, Skipped: 187, Not Run: 0` (`/tmp/opencode/146a/final2/integration.log`).
- Containers (`podman.exe --connection podman-machine-default ps -a`, before/after) — reuse containers were **started and stopped by the run**: postgres `a1877941aa2e` postgres:17-alpine, mysql `d703aedb9b21` mysql:8.4, mssql `4ab2d4542432` mcr.microsoft.com/mssql/server:2025-latest, mariadb `914f06a0ba5f` mariadb:11.4, clickhouse `525af9d6a314` clickhouse/clickhouse-server:25.8-alpine; after-state STATUS each `Exited (0)` seconds after the run (`podman-ps-before.txt`, `podman-ps-after.txt`). Reuse starts (does not recreate) them, so provider coverage is evidenced by the discovered class counts.
- Discovered per-provider class counts (`--list-tests`, 3019 total): PostgresIntegrationTests 596, SqlServerIntegrationTests 596, MySqlIntegrationTests 596, SqliteIntegrationTests 596, ClickHouseIntegrationTests 103; specifics PostgresSpecificTests 98, SqlServerSpecificTests 68, SqliteSpecificTests 44, MySqlSpecificTests 38, MariaDbFunctionsIntegrationTests 18 (`integration-list-tests.log`).

### 5. Coverage (final after-full)
- Line **86.6%** (41,554 / 47,944) >= 85 PASS; Branch **78.0%** (21,167 / 27,105) >= 75 PASS (`/tmp/opencode/146a/final/report-full/Summary.txt`).
- Per-type branch deltas (`branch-delta.csv`): `Cte<T>` new 6/6; `CteHoister` 48/48 -> 70/72 (+22/+24, 97.2%); `CteHoister.Walker` 65/72 -> 94/106 (+29/+34, 88.7%); `QueryCommand` 157/200 -> 164/200 (+7/+0); `QueryCommand<TResult>` 45/76 -> 51/76 (+6/+0); `QueryCommand.QueryPreparer` 593/694 -> 694/800 (+101/+106, 86.8%); `MemberTranslator` 324/436 -> 362/454 (+38/+18); `SqlSourceRenderer` 384/456 -> 411/470 (+27/+14).

### 6. Acceptance (7 cases, final evidence)
- `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` -> **EXIT 0**, 7 benchmarks executed, 0 failures; `Global total time 00:00:48 (48.22s)`; shell wall 52s <= 240s (`acceptance.log`, `acceptance-exit.txt`).
- Ratio `Cached_ToList / Prepared_ToList` = 2,410.8 us / 1,116.6 us = **2.16** (< investigate trigger 2.244; baseline 1.87); alloc ratio **7.91**.
| case | Mean | Allocated |
|---|---|---|
| Nextorm_Count | 3.137 ms | 373.44 KB |
| Nextorm_GroupByCount | 78.05 ms | 50.06 MB |
| Nextorm_Cached | 2.740 ms | 571.15 KB |
| Prepared_ToList | 1,116.6 us | 76.14 KB |
| Cached_ToList | 2,410.8 us | 601.95 KB |
| Cached_PlanOnly_Param | 785.5 us | 525.8 KB |
| Nextorm_Cached_ToListAsync | 3.040 ms | 737.37 KB |

### Progress log (append-only)
- 2026-10-02T10:36Z | DO | r2 | 1/3 | integrity confirmed: three recovered production files byte-exact vs pre-incident `preimage.md5`; diffstat == frozen #146-A; Debug build EXIT 0 0/0 | `/tmp/opencode/146a/final2/build-debug.log`, `/tmp/opencode/146a/final/{preimage.md5,diffstat-baseline.txt}`
- 2026-10-02T10:36Z | DO | r2 | 1/3 | MF6c survivor closed: added `TypedCteTests.cs:993`; red EXIT 2 (1 failed) -> revert md5 OK -> green EXIT 0 (1/1); mutation 12/12 killed | `/tmp/opencode/146a/final2/mf6c-{mutant,green}.log`, `mf6c-mutate-result.txt`
- 2026-10-02T10:36Z | DO | r2 | 1/3 | affected suites core 1328/1328/0, sqlite 901 (900+1 skip), postgres 739/739/0, all EXIT 0; Release build EXIT 0 0/0 | `/tmp/opencode/146a/final2/test-nextorm.*.log`, `build-release.log`
- 2026-10-02T10:36Z | DO | r2 | 1/3 | live integration EXIT 0 Total 3019/Failed 0/Skipped 187; 5 reuse containers started+stopped (pg/mysql/mssql/mariadb/clickhouse); per-provider class counts 596x4 + ClickHouse 103 | `/tmp/opencode/146a/final2/{integration.log,podman-ps-before.txt,podman-ps-after.txt,integration-list-tests.log}`
- 2026-10-02T10:36Z | DO | r2 | 1/3 | coverage line 86.6% / branch 78.0% (PASS >=85/75); acceptance 7 cases EXIT 0, ratio 2.16 < trigger 2.244 | `/tmp/opencode/146a/final/{report-full/Summary.txt,branch-delta.csv,acceptance.log}`
- status unchanged: DO (r2), cycle 1, revision r2, attempt 1/3. No commit, no push. #146-A remains DO.


---

## r2 evidence remediation — manifest reconciliation + per-suite exits + integration skip/typed proof — 2026-10-02T10:43Z (still DO (r2), cycle 1, revision r2, attempt 1/3)

No production/test change; no commit; no push. Evidence-only. Logs `/tmp/opencode/146a/final2/`.

### 1. Frozen-manifest reconciliation (25 scope entries vs 19-file diffstat)
- `git diff --stat c7f939d` = **19 files, +852/-42** (tracked modifications only). `git status --porcelain=v1` = **19 M + 14 ?? = 33** entries.
- Arithmetic: `19 tracked - 3 excluded preserved tracked + 9 untracked #146-A = 25` manifest entries. `git diff --stat` does **not** include untracked new files. The status file `docs/specs/status/typed-cte-146a-1.md` is the deliberately excluded 26th #146-A artifact (the r2 snapshot `untracked.tar` is documented "status file excluded") -> **26 total #146-A artifacts present**.
- #146-A untracked (9, status excluded): `src/nextorm.core/Cte.cs` + 8 typed test files: `TypedCteAliasTests.cs`, `TypedCteTests.cs` (core), `TypedCteTests.cs` (sqlite), `TypedCteSqlGenerationTests.cs` (postgres/sqlserver/mysql/mariadb/clickhouse).
- Brief text says "7 typed test files"; the actual count is **8** (1 alias + 7 provider/scoped). Both the brief's (1+7+1=9) and the manifest's (1+8=9, status excluded) untracked sets total 9 after `Cte.cs`; the difference is one typed test vs the status file. No file is missing.
- Excluded preserved user files: tracked `opencode.json`, `.opencode/skills/nextorm-brainstorming/SKILL.md`, `docs/specs/comparison/linq2db-backlog-gap-analysis.md`; untracked `docs/specs/design/issue-150-clickhouse-floating-extreme-row.md`, `docs/specs/design/issue-152-pdca-evidence-contract.md`.
- **Undelivered: EMPTY** — every frozen-manifest entry is present. Post-r2-freeze additive drift: `src/nextorm.core/DataContext/SqlBuilder.cs`, `src/nextorm.core/Expressions/SelectExpression.cs`, `docs/superpowers/specs/2026-10-01-cte-projections-design.md` (r2 D1 snapshot 16 tracked -> current 19). Two further untracked files, neither #146-A nor named in the brief's preserved list, are present: `docs/specs/design/join-alias-mixing-and-root-alias.md`, `docs/superpowers/specs/2026-10-02-cte-join-overloads-design.md`.
- Detail: `/tmp/opencode/146a/final2/manifest-reconcile.md`.

### 2. Per-suite exit codes + rerun logs
Runner `dotnet test tests/nextorm.<p>.tests -c Debug --no-build`, output tee'd to `/tmp/opencode/146a/final2/ledger-<p>.log`; real `EXIT=$?` captured.

| project | EXIT | Total | Succeeded | Failed | Skipped | log |
|---|---:|---:|---:|---:|---:|---|
| nextorm.core.tests | 0 | 1328 | 1328 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-core.log` |
| nextorm.sqlite.tests | 0 | 901 | 900 | 0 | 1 | `/tmp/opencode/146a/final2/ledger-sqlite.log` |
| nextorm.postgres.tests | 0 | 739 | 739 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-postgres.log` |
| nextorm.sqlserver.tests | 0 | 551 | 551 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-sqlserver.log` |
| nextorm.mysql.tests | 0 | 255 | 255 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-mysql.log` |
| nextorm.mariadb.tests | 0 | 155 | 155 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-mariadb.log` |
| nextorm.clickhouse.tests | 0 | 483 | 483 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-clickhouse.log` |
| nextorm.clickhouse.extensions.tests | 0 | 23 | 23 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-clickhouse.extensions.log` |
| nextorm.entityframeworkcore.tests | 0 | 126 | 126 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-entityframeworkcore.log` |
| nextorm.alias.tests | 0 | 37 | 37 | 0 | 0 | `/tmp/opencode/146a/final2/ledger-alias.log` |
| **Subtotal** | **0** | **4598** | **4597** | **0** | **1** | — |

- Arithmetic: succeeded 1328+900+739+551+255+155+483+23+126+37 = **4597**; total 4598. The one skip is the pre-existing capability probe `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` (`NEXTORM_LOB_SQLITE_PROBE=1`).

### 3. Integration skip breakdown + typed-case execution proof
- `$E/integration.log`: **EXIT 0; Total 3019, Errors 0, Failed 0, Skipped 187, Not Run 0**.
- **187 skips = 100% capability guards, 0 failures.** By provider: MySqlIntegrationTests 77, SqliteIntegrationTests 41, SqlServerIntegrationTests 41, PostgresIntegrationTests 25, LobCapabilityProbeTests 2, LobPerfHarnessTests 1. By reason (full counts): 39 "does not implement streaming LOB reads"; 33 "implements streaming LOB reads"; 12 "does not implement the multi-column LOB reader terminal"; 10 "cannot return inserted rows"; 10 "cannot run a CTAS batch"; 6 "no lateral/APPLY source"; 6 "materialises a query into a temporary table"; 6 "supports stored procedures"; 5 scalar-subquery cardinality; 4 "supports batches"; 4 TVF/JSON_TABLE (MySQL); 4 TVF/no portable equivalent (Postgres); 4 TVF/OPENJSON (SQL Server); 4 "implements the multi-column LOB reader terminal"; 3 each: EXCEPT ALL unsupported, inserted-row return, INTERSECT ALL unsupported, updated-row return, removed-row return, temporary-table materialisation; 2 each: zero-column result set, native multi-table DELETE, multi-branch MERGE, `NEXTORM_LOB_PROBE=1`, generated columns on insert, skip-conflict + return rows, integer AVG; 1 each: LOB perf harness, TRUNCATE, byte-wise LIKE, row locking, EXCEPT ALL supported, INTERSECT ALL supported, FULL JOIN unsupported, skip conflicting rows.
- Three representative skip lines with reasons:
  1. `NextORM.Integration.Tests.SqliteIntegrationTests.Truncate_ShouldRemoveEveryRow [SKIP]` — "This provider has no TRUNCATE."
  2. `NextORM.Integration.Tests.SqliteIntegrationTests.Lob_UnsupportedProvider_ToStream_ShouldThrowNotSupported [SKIP]` — "This provider implements streaming LOB reads."
  3. `NextORM.Integration.Tests.PostgresIntegrationTests.ExceptAll_WhenUnsupported_ShouldThrow [SKIP]` — "This provider supports EXCEPT ALL."
- **Typed-CTE / `CommonTestSuite.Cte` skips: 0.** The string `Cte_Typed` occurs **0** times in `integration.log`; no `CommonTestSuite` skip or failure line exists.
- **Execution proof (6 cases x 4 providers = 24):** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -reporter verbose -filterVSTest "FullyQualifiedName~Cte_Typed"` -> **EXIT 0, Total 24, Errors 0, Failed 0, Skipped 0, Not Run 0**. Per-provider pass counts (CTRF `$E/integration-typed.ctrf.json`): **SQLiteIntegrationTests 6/6, PostgresIntegrationTests 6/6, SqlServerIntegrationTests 6/6, MySqlIntegrationTests 6/6** — all `passed`. Cases: `Cte_Typed_OuterReselect_ShouldReturnData`, `Cte_Typed_ConverterPreserved_DirectAndReselected`, `Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData`, `Cte_Typed_SelfJoin_ShouldReturnBothSides`, `Cte_Typed_BodyFilter_ShouldStayInsideTheBody`, `Cte_Typed_ChainedConsumer_ShouldReadThroughProducer`. Log `$E/integration-typed.log`; containers started+stopped by the run.
- Status: **DO (r2)** unchanged; no commit, no push.

### Progress log (append-only)
- 2026-10-02T10:43Z | DO | r2 | 1/3 | manifest reconciliation: 19 tracked - 3 preserved + 9 untracked = 25 manifest (+1 status = 26 total); brief "7 typed tests" actual 8; undelivered EMPTY; 3 post-freeze tracked additions | `/tmp/opencode/146a/final2/manifest-reconcile.md`
- 2026-10-02T10:43Z | DO | r2 | 1/3 | 10/10 unit projects EXIT 0; subtotal 4598 total / 4597 succeeded / 0 failed / 1 skipped (pre-existing sqlite capability probe) | `/tmp/opencode/146a/final2/ledger-*.log`
- 2026-10-02T10:43Z | DO | r2 | 1/3 | integration EXIT 0 Total 3019/Failed 0/Skipped 187 (all capability, typed-CTE skips = 0); filtered typed run EXIT 0 24/24 passed (6x4 providers) | `/tmp/opencode/146a/final2/integration.log`, `integration-typed.log`, `integration-typed.ctrf.json`
- status unchanged: DO (r2), cycle 1, revision r2, attempt 1/3. No commit, no push. #146-A remains DO.

## ACT — #146-A slice A DONE / committed — 2026-10-02T10:55Z

- status: **DONE (slice A)** — cycle 1, revision r2, attempt 1/3, no further attempt. **#146 stays OPEN for slice B.**
- ACT commit: `a2f229582620073e5f132d58f2e289d8270d95e5` — subject `#146 Add typed ordinary CTE (slice A)`.
- **G1 evidence ledger** (resolved absolute paths): `/tmp/opencode/146a/final2/artifact-ledger.md` — Debug build (`build-debug.log`, exit 0, 0/0), Release build (`build-release.log`, exit 0, 0/0), 10/10 unit projects (`ledger-*.log`, all exit 0, subtotal 4598 total / 4597 succeeded / 0 failed / 1 skip), coverage collect (`/tmp/opencode/146a/final/coverage-collect.log`, exit 0, total 7616 / failed 0) + reportgenerator (`/tmp/opencode/146a/final/coverage-report.log`, exit 0, **line 86.6 / branch 78.0**), full integration (`/tmp/opencode/146a/final2/integration.log`, exit 0, 3019 / 0 failed / 187 skipped), filtered typed integration (`/tmp/opencode/146a/final2/integration-typed.log`, exit 0, 24 / 0 failed), DocFX (`/tmp/opencode/146a/r2/docfx.log`, exit 0, 2 warnings / 0 errors), acceptance (`/tmp/opencode/146a/final/acceptance.log`, exit 0, 7 cases / 0 failures, ratio 2.16 < 2.244).
- **G2 ClickHouse backslash-quoting follow-up: issue #161** — https://github.com/AlexeyShirshov/nextorm/issues/161 (milestone `1.0.9-rc2`); anchor `src/nextorm.clickhouse/ClickHouseDialect.cs:469`; disposition `/tmp/opencode/146a/r2/security-disposition.md`; triggers (verbatim) **untrusted-name ingestion / changed quoting defaults / exploitability witness**. Creation log `/tmp/opencode/146a/final2/gh-issue-create.log`.
- **G3 test anchors: 39** resolved with `roslyn members` = 24 in the named variant categories (computed-scalar-body 2, joined-converter 1, union-body/all 5, reset-operators 11, nested-derived alias 1, physical-table-name hoister 2, ColumnShape traversal 2) + 10 provider-width + 3 alias + 2 core extras — full `file:line` list in `/tmp/opencode/146a/final2/artifact-ledger.md`.
- **Integrity freeze: OK, no drift.** All 11 changed production files md5-match a recorded frozen preimage (`/tmp/opencode/146a/final/preimage.md5` 7/7; `r2/preimage.md5` + `md5-pre.txt` cover the remaining 4); `git status --short` composition unchanged (19 tracked-modified + 14 untracked).
- **Slice B not included:** `AsRecursiveCte` / `CteReference<>` absent (reflection smoke); #146 remains open.
- Committed scope: 16 tracked-modified + 10 untracked-new #146-A artifacts only; preserved user files excluded (`opencode.json`, `.opencode/skills/nextorm-brainstorming/SKILL.md`, `docs/specs/comparison/linq2db-backlog-gap-analysis.md`, `docs/specs/design/issue-150-*`, `issue-152-*`, join-alias/cte-join-overloads design). **No push.**
- 2026-10-02T10:55Z | ACT | r2 | 1/3 | #146-A slice A committed (`a2f229582620073e5f132d58f2e289d8270d95e5`); G1 ledger + G2 issue #161 + G3 39 anchors + integrity OK; slice B remains, #146 OPEN | `/tmp/opencode/146a/final2/artifact-ledger.md`, `gh-issue-create.log`
