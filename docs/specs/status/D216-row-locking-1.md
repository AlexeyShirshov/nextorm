# D216 — Row locking: FOR UPDATE (whole query) vs FOR UPDATE OF (per source)

```yaml
task: T216
issue: "#216"
selected_variant: pdca-dotnet
milestone: 1.0.9
branch: 1.0.9
cycle_N: 1
attempt_n: 3
plan_revision_r: 2
evidence_contract_revision_rv: 2
plan_state: ready
status_file: docs/specs/status/D216-row-locking-1.md
frozen_manifest: artifacts/pdca/D216/rv2/manifest.json
prior_contract: rv=1 superseded by rv=2
```

## Current state

- **Owner decision received (2026-10-13) — вариант (i):** MariaDB source-level lock is **unsupported by design** (fail-closed); #216 text/criteria narrowed and updated; issue **CLOSED**; D-07 `blocked` → `resolved`; заморозка публичного API (T53/#53) **переехала в майлстон `1.0.9-rc3`** (из `1.0.9`), решение по ней принимает владелец в её собственной задаче. Full record: §10 «Owner decision (received)».
- Phase: **ACT complete — done-with-constraint** (escalation **(A)**: close in-cycle as done-with-constraint; no full PASS; no r+1; no product change). D-01…D-06 `done`, **D-07 `blocked`** (owner decision pending); **#216 remains OPEN**; T53 freeze HELD.
- Cycle N=1, plan revision r=2, attempt n=3, evidence contract rv=2 (rv=1 superseded).
- Selected variant: `pdca-dotnet`. Single collection group → current worktree/branch `1.0.9`, no worktree/branches/merge.
- Blocks DO: none. **R-03/MariaDB is an open owner constraint**, surfaced upward; it does not stop the executable plan (routed escalation decision).

**Ready means authorization to DO per the escalation decision, not fulfillment of the entire original #216 requirement.**

**R-03/MariaDB — provisional, pending owner.** Source-level locking on MariaDB is implemented fail-closed: capability `false`, `NotSupportedException`, with no global substitution and no ignoring of the target. The original obligation to support MariaDB `OF` is neither declared fulfilled nor removed.

### Durable state

- Current cycle N: 1
- Plan revision r: 2
- Attempt n: 3
- Defect history: D216-JOIN-SRCLOCK (first/alias join source lock not attached) — observed r=2/n=1, fixed in commit `3ef11a3c`, green; D216-SS-HINT-ORDER (SQL Server `WITH (hint) AS alias` invalid) — observed r=2/n=1 via live probe, fixed in commit `1849164e`, green. D216-ALIAS-CASE (R1, Critical) — `FOR UPDATE OF "T1"` under `KeywordCase.Upper` case-folded the quoted target alias; observed r=2/n=1 CHECK, fixed r=2/n=2. D216-SS-APPLY-GLOBAL-LOCK (R2, Warning) — whole-query lock hint dropped on APPLY-join sources; observed r=2/n=1 CHECK, fixed r=2/n=2.

### DO unit states (D-01..D-04M; production surface + shared contract + provider renderers)

| Unit | State | Evidence |
|---|---|---|
| D-01 | done | SQL Server all-source red→green regression: `A/redgreen` analog `/tmp/nextorm-d01-red.log` (exit 2, selected 2, failed 2) → `/tmp/nextorm-d01-green.log` (exit 0, selected 2) |
| D-02 | done | shared contract: options carriers, `ILockRenderer.SupportsSourceLocks`, target-aware `Render(..., string?)`, fail-closed default; commit `4d979efc` |
| D-03 | done | mappings/clones/equality/plan-key identity/guards (`FromExpressionPlanEqualityComparer`, `QueryCommand.SourceLockClause`, `QueryPreparer` attach+guards); commit `4d979efc` |
| D-04 | done | SQL Server all-source hints, PG/MySQL `OF <alias>`, alias addressing; join-source-lock fix commit `3ef11a3c` |
| D-04M | done | MariaDB `SupportsSourceLocks=false` + deterministic `NotSupportedException`; whole grammar preserved; red→green `/tmp/nextorm-maria-red.log` (exit 2, selected 3, failed 2) → `/tmp/nextorm-maria-green.log` (exit 0, selected 3) |
| D-05 | done | matrix closure: focused unit/provider matrix rows, `PlanKeyUniquenessTests` (13), two-transaction integration locking (PG/MySQL/SQL Server 2 passed each; MariaDB dedicated 3 fail-closed), capability contract 5; commit `d6891104` + `1849164e` |
| D-06 | done | EN/RU docs (8 files; committed `3247774b`), `dotnet docfx` exit 0, DO→CHECK boundary sweep (7x `PDCA=T216` + LockRendererDefault + PlanKeyUniqueness + integration), acceptance 7/0 + T216 6/0, coverage line 88.7% / branch 80.8%; boundary evidence `artifacts/pdca/D216/rv2/boundary/evidence.json` (validator exit 0) |
| D-07 | **resolved (owner decision (i), 2026-10-13)** | original remainder: R-03/MariaDB source-level `OF` — per the owner decision MariaDB source-level locking is **unsupported by design** (fail-closed), so the original `OF` obligation is superseded by a narrowed criterion rather than met. History: was provisional fail-closed / **NOT PASS**, owner decision pending (narrow MariaDB to unsupported vs defer MariaDB source lock beyond 1.0.9). Evidence: `artifacts/pdca/D216/rv2/redgreen/mariadb-*.log`, `focused-mariadb.log`; decision — §10. **#216 CLOSED.** |

### ACT complete — done-with-constraint

**Final verdict:** PASS on the executed scope; **R-03/MariaDB remains a constraint pending the owner decision; not a full PASS** (`A/check/verdict.md`, `A/check/rows.json`). Acceptance R-01…R-14: R-03 = constraint-pending-owner; R-04/R-07 conditional; R-12 provenance caveat (report Summary 88.7/80.8 vs raw Cobertura XML 88.10/80.32, both ≥ 85/75; baseline ratio 1.98 → 2.03); all others met. V-matrix: 23 clean, 6 conditional-clean (V-04/V-05/V-06/V-18/V-22/V-24), 1 deferred-constraint (V-28).

**Owner options (pending, disclosed):** (i) approve narrowing MariaDB source-lock to unsupported and update #216 text/criteria, or (ii) defer MariaDB source lock beyond 1.0.9. MariaDB source lock is **fail-closed** (not a PASS).

**#216 remains OPEN**; D-07 `blocked`; T53 freeze HELD. T53 candidates registered in `docs/specs/status/collection-1.0.9-todo.md` (10 members, tag `#216`): `FromOptions.ForUpdate()/ForUpdate(LockWaitMode)/ForShare()/ForShare(LockWaitMode)`, the same four on `JoinOptions`, `ILockRenderer.SupportsSourceLocks`, `ILockRenderer.Render(LockMode, LockWaitMode, KeywordCase, string? targetAlias)`.

### ACT-prep evidence persistence (n=3)

Escalation decision **(A)**: close in-cycle as **done-with-constraint**, no `r+1`, no product change; **#216 stays OPEN**; counters n=3/3, r=2, rv=2.

- EV-02/EV-14 accepted reviews persisted: `A/contract-review.md` (ACCEPTED; additive/symmetric, `SupportsSourceLocks` DIM false, fail-closed `Render`, no breaking signature, 10 public members XML-documented; `<exception>`-doc nit), `A/docs/parity-review.md` (ACCEPTED EN/RU, 8 files).
- EV-16 owner constraint materialized (not received, not a PASS): `A/check/owner-constraint.md`, `A/check/owner-decision.json` (two owner options verbatim).
- EV-18 freeze: `A/check/freeze.json`, `A/manifest-validation.log` (exit 0), `A/check/manifest-validation.exit.json`.
- EV-12 T216-harness baseline: **N/A** — `benchmarks/nextorm.benchmark/Focused/T216LockingBenchmarks.cs` is absent at the base `666cf9ac` (`git cat-file -e` → absent); no historical baseline. R-12 judged by the acceptance baseline: ratio **1.98 → 2.03** (+2.5 %), below the **2.376** trigger. Recorded in `A/perf/provenance.json`.
- Manifest updated: EV-02/EV-14/EV-18 `met`, **EV-16 open** (disclosed constraint), EV-15 open (CHECK-side), EV-17 `met`; validator exit 0. MariaDB `OF` fail-closed / not PASS; **#216 OPEN**. No product code or test change.

### DO completion n=3/3 (CHECK n=2 completeness gate)

**EV-12 pre-change baseline** (mandated before D-02..D-04M): detached measurement worktree `git worktree add --detach /tmp/nextorm-base 666cf9ac` (= `4d979efc~1`), `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, 7 cases / 0 failures; worktree removed (`git worktree remove --force`). Baseline `Prepared_ToList` 891.7 us / 76.14 KB, `Cached_ToList` 1,763.6 us / 589.44 KB → ratio **1.98**, alloc ratio 7.74; after **2.03** (+2.6%), alloc 7.77 — below the 1.98×1.20 = 2.376 trigger. Logs `artifacts/pdca/D216/rv2/perf/baseline/acceptance.log`, comparison `perf/provenance.json`.

**EV-11 unfiltered `C-BOUNDARY`**: `dotnet build nextorm.slnx -c Debug` exit 0 (0/0); serialized unfiltered `dotnet test tests/nextorm.<p>.tests -c Debug --no-build` — core 2461/0f, sqlite 1673/0f/1 skip (`SqliteRowIdLobProbeTests...`, unrelated LOB probe), postgres 909/0f, sqlserver 819/0f, mysql 387/0f, mariadb 268/0f, clickhouse 663/0f. Summary `artifacts/pdca/D216/rv2/boundary/boundary-summary.json` + per-project logs.

**EV-10/V-27 live MariaDB whole-lock**: new `tests/nextorm.integration.tests/MariaDbRowLockingLiveTests.cs` against MariaDB 11.4: live whole-query `ForUpdate` SkipLocked probe (row held by another transaction is skipped), live whole-query `ForShare` execution (native `lock in share mode`), and source-lock fail-closed. Run `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter 'FullyQualifiedName~MariaDbRowLockingLiveTests'` with `DOCKER_HOST` → exit 0, selected 3, passed 3, skipped 0 (container executed). Log `artifacts/pdca/D216/rv2/integration-mariadb-live.log`; inventory `provider-inventory.json`.

Manifest updated: EV-02/EV-10/EV-11/EV-12/EV-14 `met` (scout accepted public-API and EN/RU parity; EV-10/EV-11/EV-12 now point at real artifacts), EV-15/EV-18 remain CHECK-side `open`, **EV-16 open/pending-owner**, EV-17 `met`; `validate_inner_loop.py manifest` exit 0. R-03/MariaDB fail-closed / **not PASS**; **#216 OPEN**; D-07 `blocked`.

### DO correction n=2/3 — boundary + coverage re-run and evidence completeness

Boundary sweep re-run after the R1/R2 fixes: `dotnet build nextorm.slnx -c Debug` exit 0 (0/0); `PDCA=T216` `-c Debug --no-build` per project — core 2, postgres 12, mysql 7, mariadb 3, sqlserver 5, sqlite 1, clickhouse 1 (all exit 0); `LockRendererDefaultTests` 5, `PlanKeyUniquenessTests` 13; targeted integration `PDCA=T216` (DOCKER_HOST) 9 selected / 8 passed / 0 failed / 1 SQLite capability skip. Evidence JSON `artifacts/pdca/D216/rv2/boundary/evidence.json` (with `per_project` counts); `validate_inner_loop.py report` exit 0.

Coverage re-run (CI reproduction, DOCKER_HOST): 11252 tests, 0 failed, 199 skipped; **line 88.7% / branch 80.8%** (28224/34925) — above 85/75. Preserved under `artifacts/pdca/D216/rv2/coverage/`: `coverage.cobertura.xml`, `report/` (HTML + `Cobertura.xml` + `Summary.txt`), `json/Summary.json`, `summary.txt`, `collect.log`, `report.log`.

Added evidence artifacts the CHECK flagged missing: EV-01 `recon.md`/`escalation.md`/`environment.json`; EV-02 `public-api.json` + `changed-paths.json` (scout review still pending → row `open`); EV-03/EV-11 `exit-records.json`; EV-04…EV-09 `cases-<provider>.json` (+ `cases-core-lockrenderer.json`, `cases-sqlite-plankey.json`); EV-10 `provider-inventory.json` (dedicated MariaDB 3/3 fail-closed; SQLite capability skip, not a DOCKER_HOST skip); EV-12 `perf/provenance.json` (baseline `docs/specs/performance/acceptance-benchmarks.md`, after ratio 2.03 < 2.244); EV-13 coverage outputs above. Manifest re-validated, exit 0; EV-16 open/pending-owner, EV-17 satisfied. R-03/MariaDB fail-closed / **not PASS**; **#216 OPEN**; D-07 `blocked`.

### DO correction n=2/3 (loop-back after CHECK FAIL)

CHECK returned **FAIL** and looped back to DO (attempts `n=2/3`, plan `r=2`, contract `rv=2`); only implementation was fixed.

**D216-ALIAS-CASE (R1, Critical) — fixed.** Under `KeywordCase.Upper` the target alias was case-folded by `SqlKeywords.Of(Upper, fragment)` (`SqlKeywords.cs:18`), so `FOR UPDATE OF "t1"` became `FOR UPDATE OF "T1"` (non-existent alias). Fixed in `PostgresDialect.cs` (`PostgresLockRenderer.Render(..., string?)`) and `MySqlDialect.cs` (`MySqlLockRenderer.Render(..., string?)`): the `for update of`/`for share of` prefix and the wait suffix are case-folded, the already-escaped/quoted alias is concatenated verbatim. Permanent regressions (GREEN): `SourceLock_Upper_DefaultAlias_ShouldPreserveAliasCase`, `SourceLock_Upper_ShareNoWait_ShouldPreserveAliasCase`, `SourceLock_Upper_OnJoin_ShouldTargetOnlyTheJoinAliasCase` (PG 3/3, MySQL 3/3) plus the closed whitespace-target guard (`MySqlDialect` test added); logs `/tmp/fix-r1-pg.log`, `/tmp/fix-r1-mysql.log`.

**D216-SS-APPLY-GLOBAL-LOCK (R2, Warning) — fixed.** `MakeApplyJoin` rendered the applied source without the global lock, so `outer apply <table> as [t2]` got no hint. Fixed in `SqlSourceRenderer.cs`: `MakeApplyJoin` now takes `LockClause? globalLock` and passes `GlobalLock: globalLock` into its `MakeFrom`, so every physical source is hinted. Permanent regressions (GREEN): `ForUpdate_WithOuterApply_ShouldHintApplySource`, `ForShare_WithCrossApply_ShouldHintApplySource` (SQL Server 2/2); log `/tmp/fix-r2-ss.log`.

Hygiene (P2): BOM removed from `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`; final newline added to `src/nextorm.core/Expressions/FromExpression.cs`; per-source lock hint now uses the non-allocating single-token `AppendHint` (`SqlSourceRenderer.cs`) instead of `AppendHints(hints, [token])`.

Re-verification: `dotnet build nextorm.slnx -c Debug` exit 0 (0/0, `/tmp/fix-sln-build.log`); `PDCA=T216` core 2 / postgres 12 / mysql 7 / mariadb 3 / sqlserver 5, all exit 0; MariaDB `SourceLock_OnPrimary_OnMariaDb_ShouldFailClosed` exit 0 / selected 1. R-03/MariaDB remains fail-closed / not PASS; **#216 OPEN**; D-07 stays `blocked`.

### Frozen evidence manifest + DO→CHECK notes

Frozen manifest: `artifacts/pdca/D216/rv2/manifest.json` (rv=2, rows EV-01…EV-18; `validate_inner_loop.py manifest` exit 0). EV-17 (SQL Server red→green) **satisfied**; EV-16 (owner obligation) **open/pending-owner** — not a PASS. Open rows EV-02/EV-14/EV-15/EV-16/EV-18 are CHECK-side actions.

**MariaDB (R-03/R-13/R-14):** source-level `FOR UPDATE OF`/`FOR SHARE OF` is **fail-closed / unsupported provisional** — `SupportsSourceLocks=false`, deterministic `NotSupportedException`, no `OF`, no whole-query substitution, no silent ignore; whole-query locks keep MariaDB's own grammar. R-13 implemented, R-03 not PASS, R-14 retained. Pending owner decision: (i) narrow MariaDB to unsupported and update #216 text, or (ii) defer MariaDB source lock beyond 1.0.9. **#216 stays OPEN.**

**Open variant-matrix rows:** V-04 (`FromSql`) and V-06 (CTE) guards exist in `QueryPreparer.AttachPrimarySourceLock`/`IsLockablePhysicalSource` but have no public source-lock path today (recorded, not asserted). V-18 (ref/value entity forms), V-22 (outer JOIN/native unsupported shapes), V-24 (clone/reuse/repeated cached prepare) and V-30 (public API freeze/T53 inventory) remain for CHECK adjudication.

**Boundary integration skip:** the single `skipped` in the `PDCA=T216` integration run is `SqliteIntegrationTests.RowLock_SourcePrimary_ShouldLockTheTargetRow` — a capability skip (`SqliteDialect.Lock is null`), **not** a missing `DOCKER_HOST`/container skip; PostgreSQL, MySQL and SQL Server executed (0 skipped).

### D-05 part 4 (DO->CHECK boundary sweep + coverage)

One `dotnet build nextorm.slnx -c Debug` (exit 0, 0/0, `/tmp/nextorm-boundary-build.log`), then all runs `-c Debug --no-build`, `phase=boundary`:
- `PDCA=T216`: core 2, postgres 9, mysql 3, mariadb 3, sqlserver 3, sqlite 1, clickhouse 1 — all exit 0
- `FullyQualifiedName~LockRendererDefaultTests` (core): 5, exit 0
- `FullyQualifiedName~PlanKeyUniquenessTests` (sqlite): 13, exit 0
- integration `PDCA=T216` (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`): total 9, passed 8, failed 0, **skipped 1** (SQLite `RowLock_SourcePrimary` capability skip — no row locking), exit 0
- Evidence JSON `/tmp/nextorm-t216-evidence.json` (+ gitignored `artifacts/pdca/D216/rv2/boundary/evidence.json`); `python3 scripts/validate_inner_loop.py report` exit **0**

Coverage (`--no-build` full suite under `dotnet-coverage`, DOCKER_HOST set; 11243 tests, 0 failed, 199 skipped):
- `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` — exit 0 (`/tmp/nextorm-cov-collect.log`)
- `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"` — exit 0 (`/tmp/nextorm-cov-report.log`)
- Result: **Line coverage 88.7%** (52481/59120) and **Branch coverage 80.8%** (28219/34919), 4 assemblies — both **above** the 85% line / 75% branch thresholds. Summary `artifacts/pdca/D216/rv2/perf/coverage-summary.txt`.

### D-05 part 3 (perf acceptance, cached path)

Acceptance command (`--anyCategories=acceptance`): exit 0, **7** executed benchmarks, **0** failures, external wall clock **51 s** (BDN `Global total time` 48.65 s) — under the 4 min budget. Logs `/tmp/nextorm-acceptance.log` (copy `artifacts/pdca/D216/rv2/perf/after/acceptance.log`, gitignored).

| Case | Mean | Allocated |
|---|---|---|
| Nextorm_Count | 2.107 ms | 382.81 KB |
| Nextorm_GroupByCount | 58.69 ms | 50.12 MB |
| Nextorm_Cached | 2.039 ms | 618.05 KB |
| Prepared_ToList | 892.0 us | 76.14 KB |
| Cached_ToList | 1,806.8 us | 591.78 KB |
| Cached_PlanOnly_Param | 554.2 us | 515.64 KB |
| Nextorm_Cached_ToListAsync | 1.957 ms | 616.27 KB |

Cached/prepared ratio `Cached_ToList / Prepared_ToList` = **2.03** (1806.8/892.0); documented baseline **1.87**, +8.3 %, well below the 20 % investigation trigger (1.87 x 1.20 = **2.244**). Allocated ratio **7.77** (baseline 7.42, +4.7 %). `Nextorm_Count` is the high-variance row (this run Error 0.3839 ms < Mean 2.107 ms); no row is used for regression assertions.

T216 cached-path benchmark (new `benchmarks/nextorm.benchmark/Focused/T216LockingBenchmarks.cs`, category `T216`): `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=T216` — exit 0, **6** cases, 0 failures, wall 52 s (BDN 49.07 s). Logs `/tmp/nextorm-t216-bench.log` (+ gitignored copy). cached/prepared ratio: NoLock **1.04** (425.5/410.2 us), WholeLockJoin **0.79** (740.4/937.4 us), SourceLockJoin **0.79** (720.4/911.1 us); the source-lock prepare/render path is not slower than the whole-lock path (within `ShortRun` noise). Numbers only; interpretation belongs to CHECK.

### D-05 part 2 (focused integration locking)

Executed serially with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, `-c Debug --no-build`, `--filter "FullyQualifiedName~<Provider>IntegrationTests&FullyQualifiedName~RowLock"`:
- PostgresIntegrationTests: exit 0, passed 2, failed 0, skipped 0 (`/tmp/nextorm-int-PostgresIntegrationTests-lock.log`)
- MySqlIntegrationTests: exit 0, passed 2, failed 0, skipped 0 (`/tmp/nextorm-int-MySqlIntegrationTests-lock.log`)
- SqlServerIntegrationTests: exit 0, passed 2, failed 0, skipped 0 (`/tmp/nextorm-int-ss-lock2.log`)
- SqliteIntegrationTests: exit 0, passed 0, failed 0, **skipped 2** — capability skip (SQLite has no row locking), NOT a DOCKER_HOST skip (`/tmp/nextorm-int-SqliteIntegrationTests-lock.log`)
- MariaDbRowLockingSpecificTests (dedicated, no container; MariaDB is NOT under the MySQL provider path): exit 0, passed 3, failed 0, skipped 0 — source lock fails closed (`NotSupportedException`, no `OF`), whole grammar preserved (`/tmp/nextorm-int-maria-lock.log`)
- DialectCapabilityContractTests (incl. `SupportsSourceLocks_ShouldBeExplicitPerDialect`, `SourceLockTarget_ShouldBeTargetedOrFailClosed`): exit 0, passed 5 (`/tmp/nextorm-int-capability.log`)

Defect fixed here (surfaced by the live SQL Server probe): SQL Server rejects `WITH (hint) AS alias`; table hints are now emitted **after** the alias (`FROM t AS a WITH (hint)`), which also corrects the D-04 all-source hint output. Existing SQL Server SQL-gen assertions updated; unit focused suites re-verified green.

Still open for the boundary step: V-18/V-22/V-24/V-30 review rows, EN/RU docs, `C-BOUNDARY` full sweep, acceptance/T216 benchmarks, coverage, frozen rv=2 manifest.

### D-05 part 1 (closed in this step)

Closed (focused `PDCA=T216`, `-c Debug --no-build`): whole+source → `InvalidOperationException`; non-physical
derived-query source → `NotSupportedException`; SQLite/ClickHouse/in-memory source-only rejection;
Update/Share × Wait/NoWait/SkipLocked preserved incl. MariaDB own grammar; multi-source different
mode/wait each targeted; capability fail-closed (MariaDB primary+join); empty/whitespace target →
`ArgumentException`; child `LoadWith` source-only guard; extreme-row source-only guard; plan-key
identity for source locks (`PlanKeyUniquenessTests`). V-04 (FromSql) and V-06 (CTE) guards exist in
`QueryPreparer` but have no public source-lock path today (recorded, not asserted). Remaining for the
boundary step: V-18/V-22/V-24/V-30 review rows, integration live probes, EN/RU docs, `C-BOUNDARY`,
acceptance/T216 benchmarks, coverage, frozen rv=2 manifest.

Focused inner-loop evidence (trait `PDCA=T216`, `-c Debug --no-build`, filter `PDCA=T216`):
`/tmp/nextorm-core-focused.log` (exit 0, selected 5), `/tmp/nextorm-ss-t216b.log` (exit 0, selected 2),
`/tmp/nextorm-postgres-t216b.log` (exit 0, selected 4), `/tmp/nextorm-mysql-t216b.log` (exit 0, selected 3),
`/tmp/nextorm-mariadb-t216b.log` (exit 0, selected 3). Scope brief validated: `/tmp/nextorm-t216-scope.json` exit 0.
Build: `src/nextorm.core` + all six provider projects exit 0 / 0 warnings / 0 errors (`/tmp/nextorm-d02-build2.log`, `/tmp/nextorm-d04-*.log`).

**R-03/MariaDB remains a constraint pending owner decision; not a PASS and #216 stays open.**


## Goal

Separate:
1. **Whole-query lock** — query-level `ForUpdate`/`ForShare`, covering all FROM/JOIN sources of the current statement per native provider semantics.
2. **Source-level lock** — a lock on a specific physical source, set via `FromOptions` or `JoinOptions` and addressed by the existing SQL alias.

Existing query-level methods are preserved. SQL Server primary-only → all-sources is a notable behavior change for multi-table queries.

## Acceptance criteria

| ID | Positive check | Negative case |
|---|---|---|
| R-01 | On an allowed query with a primary and two physical INNER JOINs: PG/MySQL/MariaDB have a global trailing lock; SQL Server has corresponding hints on all three sources. | Primary-only SQL Server, or a global clause accidentally restricted to the primary alias, is unacceptable. |
| R-02 | Source API symmetric in `FromOptions`/`JoinOptions`; default and explicit aliases address the chosen source. | Lock must not leak to a neighbouring JOIN, another instance of the same CLR type, or another alias. |
| R-03 | PG/MySQL: targeted `OF <alias>`; SQL Server: hint only on the chosen source. Multiple targets preserved. **MariaDB: the original OF criterion is provisional, pending owner; NOT PASS.** | Must not substitute a targeted lock with a global one, ignore the alias, or declare the MariaDB original criterion fulfilled. |
| R-04 | Source lock allowed on entity, physical `From(string)`, physical JOIN. | Source lock on FromSql, query/builder subquery, or CTE throws `NotSupportedException` at prepare, before SQL execution. |
| R-05 | SQLite/ClickHouse/in-memory reject whole and source locks. | Successful preparation with an ignored lock is unacceptable, including JOIN options. |
| R-06 | `Update/Share × Wait/NoWait/SkipLocked` preserved across transfer and rendering. MariaDB preserves its own whole-lock grammar. | No loss of wait mode, no MySQL `FOR SHARE` syntax on MariaDB, no `OF` leak via inheritance. |
| R-07 | Options → carriers → command/definition → clones → plan key preserve locks. Different scope/target/mode/wait differ in cache identity. | No cache collision, mutable leakage, or mutation of an already-prepared query. |
| R-08 | Old renderer overloads preserved; new overload/capability additive and fail-closed. | A legacy renderer must not accept a target and silently emit a global lock. |
| R-09 | Child-query, extreme-row and in-memory guards cover source-only locks too. | `QueryCommand.RowLock == null` must not bypass existing lock restrictions. |
| R-10 | EN/RU docs reflect the API, provider matrix, physical-only, aliases, the conflict rule and the SQL Server behavior change. | No claim of MariaDB OF support, no stale primary-only description, no public link to `docs/specs/**`. |
| R-11 | Build exit 0, 0 warnings/errors; focused/boundary/live suites run with proven nonzero test selection. | Zero selected tests, missing logs and container-driven provider skips do not count as pass. |
| R-12 | Acceptance benchmarks, T216 benchmarks and the coverage recipe are run; results compared with baseline. | No substituting measurement with the argument "SQL is built once", no invented baseline for the new API, no hidden coverage exclusion. |
| R-13 (new) | MariaDB explicitly has `SupportsSourceLocks=false`; targeted render and source options throw `NotSupportedException`; whole locks keep working. | Inherited MySQL OF, global substitution, silent ignore, and subquery/CTE emulation are forbidden. |
| R-14 (new) | CHECK and the final report explicitly retain `R-03/MariaDB — constraint pending owner decision`. | Must not issue a full PASS of the original task, close #216, or declare the owner decision received without evidence. |

Native restrictions (e.g. PostgreSQL locking with DISTINCT/GROUP BY/window/set operations, or a nullable side of an outer JOIN) are not turned into a support promise. The required all-sources positive case uses an allowed physical INNER JOIN.

## Minimal solution

- **Essence:** store the global lock separately from the lock of the selected source.
- **Constraints:** source API only via options; existing aliases; physical-only; one existing renderer contract; capabilities; fail-closed unsupported behavior.
- **Minimal implementation:** immutable source-lock metadata in the existing carriers, the existing alias pipeline, an additive renderer overload and a per-dialect capability.

Alternatives: **A. Lock in source carriers** (metadata follows the source across clone; no extra map/source-picker; natural options parity) vs **B. Immutable source-keyed map in the command** (centralized walk; extra identity/map synchronization at joins/aliases/clones; larger footprint and mismatch risk). **Choice: A** — the confirmed carriers and clone seams suffice; no spike is needed for the choice.

### Shared contract (to be frozen in D-02)

1. `QueryCommand.RowLock` and `QueryDefinition.RowLock` stay **whole-query**.
2. `LockClause` stays an immutable mode/wait payload.
3. The canonical source lock is stored on `FromExpression` as internal immutable metadata modelled on `SubQueryHint`.
4. `JoinOptions` transfers the lock into the corresponding physical source `JoinExpression.From`; `JoinExpression` does not get a second independent lock storage.
5. Copy/override/cache/preparation paths preserve this payload; JOIN equality/hash accounts for it via the source carrier.
6. Aliases are not stored as a separate string in the lock metadata. The target is resolved at render time via the existing alias provider/resolver.
7. With source locking the alias must actually be declared in FROM/JOIN. One must not emit `OF t1` if there is no `AS t1` declaration.
8. The renderer receives an already-resolved, correctly quoted SQL alias via the new overload: `Render(LockMode, LockWaitMode, KeywordCase, string? targetAlias)`. `null` means whole-query rendering. An empty/whitespace target is an `ArgumentException`.
9. `ILockRenderer.SupportsSourceLocks` has a safe default `false`. Whole-lock capability stays `Dialect.Lock != null`; `UsesTableHints` keeps placement.
10. Old overloads remain. The default implementation of the new overload does not ignore an unsupported target.
11. SQL Server global hints are added locally per source; `cmd.TableHints` and shared query commands are not mutated.
12. MariaDB has an explicit capability/targeted-render override preventing an inherited MySQL `OF`.

Config rules: whole + source simultaneously → `InvalidOperationException` at prepare. One effective `LockClause` per source; a repeated options setter replaces the source's prior lock. Different sources may have different mode/wait. One target does not appear in multiple locking clauses. Source-lock clauses are not merged into a global clause. For MariaDB, `Wait` means normal blocking without a suffix; native `WAIT n` is documented as provider grammar and no new timed-wait API is added. MySQL forbids mixing a clause without OF and a clause with OF; this backs the chosen fail-fast contract; complex mode/wait normalization is out of scope.

## What the statement did not say

| Gap | Closure |
|---|---|
| Alias API/core representation | Confirmed: generated consumer `Alias`, core `IAliasProvider`; root/join seams at `EntityBuilder.cs:3055,3409`; default `t{n}`. No new picker. |
| When an alias becomes SQL | Rendering via resolver/source renderer and `MakeTableAlias`; the lock uses the same resolved alias. |
| Carrier and clone/identity paths | Gather confirmed From/Join carriers, mappings, clone and comparer seams; apply shared contract. |
| MariaDB OF feasibility | Confirmed impossible; escalation prescribes provisional fail-closed, without removing the original criterion. |
| MariaDB inheritance | Mandatory explicit override and a negative test against MySQL rendering leakage. |
| Whole + source | Fail-fast frozen; identical for supporting dialects. |
| Empty OF/default/null | No public OF-list. An empty source-lock set adds nothing; `null` target = global; empty string rejected. |
| Outer JOIN / unsupported SELECT shapes | Native restrictions documented and tested; not emulated. |
| MariaDB wait versions | NOWAIT/native WAIT ≥10.3; SKIP LOCKED ≥10.6, InnoDB; live fixture 11.4. |
| MariaDB live harness | Existing MariaDB container, outside the shared provider matrix; use dedicated tests. |
| Evidence/perf/coverage rules | Fully supplied; recipes pinned below. |
| Sealedness | Query 69/125, Dialect 3/31, carriers 5/7; baseline method = ref-type declaration enumeration. |
| Sibling-task impact | Their changes are not assumed. Parent supplies a cumulative diff and prevents concurrent shared-file edits. |

The only open external decision is R-03/MariaDB. It does not block DO per the routed escalation, but it blocks attesting full fulfillment of the original criterion.

## Frozen variant / priority matrix

Matrix **`T216-P1-SQL-OPERATOR-r2`** frozen before DO.

P1 by construction: invariants R-01…R-09, R-13/R-14; the "new SQL function/operator" class (SQL-form parity, capability per dialect, every-provider tests, nullable/default semantics); execution-path propagation, clones, guards and plan identity. CHECK does not downgrade P1 due to environment complexity or a missing report.

| ID | Variant | Closure | Priority |
|---|---|---|---|
| V-01 | Physical entity primary | test: whole/source | P1 |
| V-02 | Physical `From(string)` | test: source, alias quoting | P1 |
| V-03 | Physical JOIN, multiple targets | test: each target and multiple locks | P1 |
| V-04 | FromSql primary/JOIN | guard + test: source lock NSException | P1 |
| V-05 | Query and builder subqueries | guard + test both forms | P1 |
| V-06 | CTE source | guard + test | P1 |
| V-07 | Default/explicit alias | test, incl. actual AS declaration | P1 |
| V-08 | Primary/first/subsequent JOIN | test: target isolation | P1 |
| V-09 | Mode × wait | test: renderer Cartesian matrix and query placement | P1 |
| V-10 | PG/MySQL/MariaDB whole; PG/MySQL source | test; original MariaDB source criterion retained in V-28 | P1 |
| V-11 | SQL Server | test: all-source/global and selected-source hints | P1 |
| V-12 | SQLite/ClickHouse/in-memory | guard + test both scopes | P1 |
| V-13 | Builder/command/definition | test: supported public paths, prepare, clones | P1 |
| V-14 | No lock/default options/null payload | test: prior SQL and cache behavior | P1 |
| V-15 | Renderer target null | test: whole output | P1 |
| V-16 | Empty source-lock set | test: no OF/clause; explicit whole preserved | P1 |
| V-17 | Empty/whitespace target | guard + test: ArgumentException | P1 |
| V-18 | Reference/value entity forms | test allowed existing forms; guard generic constraints for disallowed | P1 |
| V-19 | Whole + source | guard + test: InvalidOperationException | P1 |
| V-20 | Multiple sources, different mode/wait | test: no lost/duplicated targets | P1 |
| V-21 | Table/index hints + lock; KeywordCase | test: correct composition and case | P1 |
| V-22 | Outer JOIN/native unsupported shapes | test: allowed target; explicit native rejection for forbidden shape, no emulation | P1 |
| V-23 | Extreme-row/eager child | guard + test source-only bypass | P1 |
| V-24 | Clone/reuse/repeated cached prepare | test: equality/hash and no leakage | P1 |
| V-25 | MariaDB source options/targeted renderer | guard + test: capability false, NSException | P1 |
| V-26 | MariaDB inherits MySQL implementation paths | test: OF/FOR SHARE do not leak | P1 |
| V-27 | MariaDB whole mode/wait | test SQL + live 11.4; version restrictions documented | P1 |
| V-28 | MariaDB original OF obligation | guard against false PASS; **deferred pending owner**, trigger: recorded owner decision | P1, constraint |
| V-29 | Legacy/custom renderer | guard + test: additive DIM, no silent broadening | P1 |
| V-30 | Public API freeze/T53 | test/review: inventory of all new members | P1 |

`V-28` does not mean an agreed removal of the requirement or an automatic milestone move. Out of scope: `NO KEY UPDATE`, `KEY SHARE`, source locks on logical sources, a timed-wait API. The support trigger is a separate agreed requirement; the current rejection guards are executed now.

## DO units, mode and dependencies

### Mode

**Sequential in one tree.** All units overlap on `DataContext/**`, `Dialect/**`, `Builders/**`, carriers and tests, which T211/T212/T213/T201/T215/T217/T218/T53 also touch. Worktrees are unnecessary: the task is not isolated, and the shared contract matters more than parallelism. The parent prevents concurrent edits, supplies a current cumulative diff and coordinates the public API with T53. No unknown sibling outputs are assumed.

### Units

| Unit | Concrete actions | Fix/dependency |
|---|---|---|
| D-01 | Preserve the supplied gather/escalation evidence; take a benchmark baseline; add a SQL Server all-source regression and record an observable red assertion. | fix now; before implementation |
| D-02 (shared contract) | Add the four methods to each options class, internal immutable source metadata, renderer overload/capability; freeze the conflict/target rules and the T53 inventory. | fix now; before consumers |
| D-03 | Update all options mappings, immutable source replacement, cache/preparation clones, source equality/hash and guards. | fix now; after D-02 |
| D-04 | SQL Server global hints on all sources; targeted hints; PG/MySQL OF; aliases/quoting; multiple clauses without duplicate targets. | fix now; after D-03 |
| D-04M (additive) | Explicit MariaDB capability false and targeted rejection; preserve MariaDB whole grammar/waits; prevent inheritance leakage. | fix now; after D-02, before live tests |
| D-05 | Close V-01…V-27/V-29/V-30; add tagged unit/SQL/cache/guard tests and two-transaction live cases; dedicated MariaDB tests without changing the shared matrix. | fix now |
| D-06 | EN/RU docs; full boundary/build; acceptance/T216 benchmarks after; coverage; freeze the evidence manifest. | fix now |
| D-07 (original remainder) | Preserve R-03/MariaDB pending-owner, pass the unresolved choice upward and do not close #216. | active, blocked on owner decision; not done |

Reference sites: `FromOptions.cs:29`, `JoinOptions.cs:38`, `DialectCapabilities.cs:567`; `FromExpression.cs:161,204-226,243-251,276-289`; `JoinExpression.cs:140,196-235`; `EntityBuilder.cs:90,781-784,2668-2671,3415`; `QueryCommand.Clone.cs:98,142,178-193`; carrier comparers and the query comparer; `SqlBuilder.cs:144-148,185,441-449`; `SqlSourceRenderer.cs:524,560,651,694,897`; child guard `EntityBuilder.cs:1209,1257-1258` / `EntityBuilderEagerLoading.cs:270-275`; `Query/ExtremeRowCompatibility.cs:23,67-68`; `DataContext/InMemoryQueryBuilder.cs:98,112-113`.

## Test strategy

New/affected T216 cases get `[Trait("PDCA","T216")]`. Scenario labels are evidence labels, not invented existing test symbols:
`whole/all-sources`; `source/primary`, `source/join`, `source/multiple`; `alias/default`, `alias/explicit`, `alias/same-entity`; `reject/fromsql`, `reject/subquery-query`, `reject/subquery-builder`, `reject/cte`; `renderer/mode-wait-target`, `renderer/legacy`; `cache/scope-target-mode-wait`, `clone/no-leak`; `reject/extreme`, `reject/child`, `reject/inmemory`; `mariadb/source-fail-closed`, `mariadb/no-inherited-of`, `mariadb/whole`; `live/global-coverage`, `live/target-isolation`.

Unit/SQL generation: exact SQL, all-source coverage, aliases, capabilities, waits, exceptions, composition, clone/equality/hash. Integration: SQL is actually accepted by the server; bounded two-transaction probes distinguish a lock on the target from a non-target (use NOWAIT or a bounded timeout, always release transactions/connections; for Share check conflict with a writer, not a second reader).

MariaDB: dedicated tests use the existing `MariaDbContainer`, image `mariadb:11.4`; source-lock rejection is checked before SQL execution; whole-lock live checks use MariaDB native grammar; do not add MariaDB to the shared provider matrix. Unsupported-capability skips in shared locking cases are allowed only with explicit negative tests. Provider-initialization skips due to a missing Docker host are not pass.

Per-unit scope / boundary:

| Unit | Affected projects / exact focused selector | Boundary point |
|---|---|---|
| D-01 | SQL Server: native trait `PDCA=T216`, red assertion; benchmarks | Baseline before D-02 |
| D-02 | core: trait `PDCA=T216` | Full core after D-03 |
| D-03 | core/sqlite: trait; integration child tests: same trait | Full core + sqlite before D-04 |
| D-04 | postgres/mysql/sqlserver: trait | All provider projects after D-05 |
| D-04M | mariadb: trait | MariaDB unit + dedicated live tests |
| D-05 | All seven unit/provider projects and integration: trait | Full integration in D-06 |
| D-06 | No selector: affected-project boundary sweep | Before DO→CHECK |

Each focused run selects **>0** tests, and the scenario map shows coverage of the applicable V IDs. Coverage includes only core/sqlite/postgres/sqlserver; the MariaDB exclusion is not evidence of its test coverage. Thresholds **85% line / 75% branch**; on `1.0.9` the CI warning policy stands and deviations are explicitly reflected in CHECK.

## Documentation

Change: `docs/guide/32-provider-specific-features.md`, `docs/ru/guide/32-provider-specific-features.md`, `docs/advanced/api-reference.md`, `docs/ru/advanced/api-reference.md`, `docs/connecting-providers/sqlserver.md`, `docs/ru/connecting-providers/sqlserver.md`, `docs/advanced/limitations.md`, `docs/ru/advanced/limitations.md`.

Required content: whole/source distinction and options methods; existing default/explicit alias forms; physical-only, conflict rule, native restrictions; provider matrix with MariaDB source-level unsupported; SQL Server primary-only → all-sources behavior warning; MariaDB whole-lock spelling/wait versions. The pending-owner marker lives in status/evidence/final report; public docs describe the actually available capability, not internal workflow.

Do not change: `toc.yml`, page numbering, generated `docs/api/**`/`docs/_site/**`, public names of existing methods. Public docs must not link to `docs/specs/**`.

## Performance measurement

**Required, including mandatory cached-path acceptance.** The change touches source identity and plan lookup (`QueryPlanEqualityComparer.cs:121,181,192,519`; From/Join equality/hash; cache/preparation clones), so one-time SQL emission in `SqlBuilder.cs:441-449` does not exempt it from cached-path measurement. Per-row binding/TVP/materialization are unchanged; no separate per-row benchmark is needed. Lock-related per-row allocations are forbidden.

- Mandatory acceptance set, before and after implementation: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` — exactly 7 cases, no failures.
- Additional category `T216` (separate from acceptance): `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=T216`. Add a small SQL-generation/prepare harness: no-lock cached/prepared pair; whole-lock physical JOIN cached/prepared pair; after implementation a source-lock JOIN pair. The baseline harness uses only the existing API; the new source-lock pair has no historical baseline and is recorded without a fictitious comparison.
- Baseline is taken after adding the harness, **before D-02…D-04M**; after — same host, SDK, Release config, job configuration; record a working-tree fingerprint and environment.
- Threshold: wall-time target **≤4 minutes per run**; record wall time, Mean, Allocated; ratio `Mean(cached)/Mean(prepared)` for comparable pairs; **>20% growth of a comparable ratio → mandatory investigation**, not an automatic hard-fail; no failures; no new lock-related per-row allocations.

## Reconnaissance

**No new reconnaissance is needed to choose the solution.** Alias/carrier paths, MariaDB feasibility, recipes and escalation are confirmed. Do not repeat the pre-PLAN gather. A red→green proof is needed as regression evidence, not an architecture spike: the SQL Server all-source assertion fails on baseline and passes after D-04. A build failure due to a missing future symbol does not count as that red.

## Risks / known issues

| Risk | Severity / status | Mitigation |
|---|---|---|
| R-03/MariaDB differs from the issue acceptance | High / pending owner | Explicit constraint; not a PASS and not an issue closure |
| MySQL renderer leaks into MariaDB | High / mitigated by plan | Explicit override + tagged negative tests |
| Alias declaration/OF mismatch | High / mitigated by plan | One resolved alias; declaration and target tests |
| Cache collision/mutable leakage | High / mitigated by plan | Carrier clone/equality/hash tests |
| SQL Server contention/deadlocks after all-source lock | High / known behavior change | Bounded live probes, docs warning |
| Native SELECT/outer JOIN restrictions | Medium / known | Positive supported shapes, negative native cases |
| Concurrent sibling edits | High / parent-controlled | Sequential ownership, cumulative-diff preflight |
| False green from skips/zero selection | High / mitigated by contract | Counts/provider inventory/log requirements |
| MariaDB image not pre-pulled | Medium / operational | Existing harness pull/start; do not declare skipped a pass |
| Perf regression | Medium / measured | Baseline, acceptance + T216, ratio investigation |

## Deferred + trigger

- Deferred: `V-28` — the original MariaDB `OF` obligation; trigger: a recorded owner decision (approve narrowing to unsupported and update #216, or defer MariaDB source lock beyond 1.0.9). Stays in milestone 1.0.9; no milestone move without an owner decision.
- Out of scope: `NO KEY UPDATE`, `KEY SHARE`, source locks on logical sources, a timed-wait API; trigger: a separate agreed requirement.

## Planned write-footprint

Production (expected edit envelope, not a requirement to edit each file; coder confirms symbol locations via Roslyn before editing; a new subsystem/package/source-generator edit requires returning to PLAN):
`src/nextorm.core/DataContext/FromOptions.cs`, `src/nextorm.core/Builders/JoinOptions.cs`, `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`, `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs`, `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs`, `src/nextorm.core/Expressions/FromExpression.cs`, `src/nextorm.core/Expressions/JoinExpression.cs`, `src/nextorm.core/Query/QueryCommand.cs`, `src/nextorm.core/Query/QueryCommand.Clone.cs`, `src/nextorm.core/Query/QueryDefinition.cs`, `src/nextorm.core/Query/QueryPlanEqualityComparer.cs`, `src/nextorm.core/Expressions/FromExpressionPlanEqualityComparer.cs`, `src/nextorm.core/Expressions/JoinExpressionPlanEqualityComparer.cs`, `src/nextorm.core/Query/ExtremeRowCompatibility.cs`, `src/nextorm.core/DataContext/InMemoryQueryBuilder.cs`, `src/nextorm.core/DataContext/SqlBuilder.cs`, `src/nextorm.core/DataContext/SqlSourceRenderer.cs`, `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs`, `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.mysql/MySqlDialect.cs`, `src/nextorm.mariadb/MariaDbDialect.cs`, `src/nextorm.sqlserver/SqlServerDialect.cs`.

Tests/benchmark (planned destinations, not assertions that symbols exist): `tests/nextorm.core.tests/LockRendererDefaultTests.cs`, `tests/nextorm.core.tests/InMemoryTests.cs`, `tests/nextorm.core.tests/EagerLoadingSingleQueryP1Tests.cs`, `tests/nextorm.core.tests/T216RowLockingTests.cs`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs`, `tests/nextorm.mysql.tests/SqlGenerationTests.cs`, `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`, `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`, `tests/nextorm.sqlite.tests/SqlGenerationTests.cs`, `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`, `tests/nextorm.sqlite.tests/PlanKeyUniquenessTests.cs`, `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite.Locking.cs`, `tests/nextorm.integration.tests/MariaDbRowLockingSpecificTests.cs`, `benchmarks/nextorm.benchmark/Focused/T216LockingBenchmarks.cs`.

Also: eight docs files, this status file, `artifacts/pdca/D216/rv2/**`, generated coverage output under `tests/coverage/**`. Not planned: package/CPM, CI thresholds, target frameworks, source generator, provider matrix, container implementation, generated docs edits.

New PUBLIC API for T53: `FromOptions` → `ForUpdate()`, `ForUpdate(LockWaitMode)`, `ForShare()`, `ForShare(LockWaitMode)`; `JoinOptions` → the same four; `ILockRenderer.SupportsSourceLocks`; `ILockRenderer.Render(LockMode, LockWaitMode, KeywordCase, string? targetAlias)`. New carrier fields/helpers are internal; no new public constructors/accessors are needed — if implementation reveals such a need, return to PLAN/API coordination before adding.

## Evidence contract rv=2

**rv=1 superseded.** All planned artifacts below are expectations, not obtained results. Frozen manifest: `artifacts/pdca/D216/rv2/manifest.json`, frozen at DO→CHECK, append-only during CHECK.

Command registry (run from workspace root; each invocation stores stdout/stderr, the actual exit code and duration; shell logging must not hide the exit code — `pipefail`/`PIPESTATUS`):
- C-BUILD: `dotnet build nextorm.slnx -c Debug`
- C-CORE: `dotnet run --project tests/nextorm.core.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-PG: `dotnet run --project tests/nextorm.postgres.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-MYSQL: `dotnet run --project tests/nextorm.mysql.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-MARIA: `dotnet run --project tests/nextorm.mariadb.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-SS: `dotnet run --project tests/nextorm.sqlserver.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-SQLITE: `dotnet run --project tests/nextorm.sqlite.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-CH: `dotnet run --project tests/nextorm.clickhouse.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-INT-FOCUS: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait "PDCA=T216" -noColor`
- C-INT: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`
- C-ACCEPTANCE: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
- C-T216: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=T216`
- C-COV-COLLECT: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
- C-COV-REPORT: `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`
- C-DOCFX: `dotnet docfx docs/docfx.json`
- C-MANIFEST: `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D216/rv2/manifest.json`
- C-BOUNDARY: `for p in core postgres mysql mariadb sqlserver sqlite clickhouse; do dotnet test tests/nextorm.$p.tests -c Debug || exit $?; done` (each inner command must exit 0).

Coverage commands run with the integration host configured so the bundled `dotnet test` does not turn provider skips into pass. Before C-INT-FOCUS/C-INT load `.opencode/skills/running-integration-tests/SKILL.md`; if the socket is missing apply its start/wait/recheck recipe and retry. The MariaDB image pull is done by the existing harness.

Required rows (prefix `A = artifacts/pdca/D216/rv2/`):

| Row / requirement IDs | Check; expected evidence | Exact command / invocation and required result | Planned artifacts | Owner stream | Applicability | rv |
|---|---|---|---|---|---|---|
| EV-01 / R-02,03,07,08,12,13 | Preserve the closed gather and routed escalation; textual evidence from supplied inputs | coder Task persisting the GAP-A..E closures and escalation decision without re-gathering; success; all closures/decision present | `A/recon.md`, `A/escalation.md`, `A/environment.json` | coder-records | Always | 2 |
| EV-02 / R-02,04,07,08,09 | Implementation contract/API review; Roslyn + diff semantic facts | scout Task reviewing implemented T216 contracts; success; all seams covered, no silent broadening | `A/contract-review.md`, `A/public-api.json`, `A/changed-paths.json` | scout-contract | Always | 2 |
| EV-03 / R-11 | Build | C-BUILD — exit 0, 0 warnings/0 errors | `A/build.log`, `A/build.exit.json` | coder-build | Always | 2 |
| EV-04 / R-04,05,07,08,09 | Core renderer/default/clone/guard cases | C-CORE — exit 0, selected >0, applicable tagged cases pass | `A/focused-core.log`, `A/focused-core.exit.json`, `A/cases-core.json` | coder-core | Always | 2 |
| EV-05 / R-01,02,03,06 | PG SQL/alias/mode/wait/target | C-PG — exit 0, selected >0, no applicable failures | `A/focused-postgres.log`, `A/focused-postgres.exit.json`, `A/cases-postgres.json` | coder-postgres | Always | 2 |
| EV-06 / R-01,02,03,06 | MySQL SQL/alias/multiple-target/conflict | C-MYSQL — exit 0, selected >0, no applicable failures | `A/focused-mysql.log`, `A/focused-mysql.exit.json`, `A/cases-mysql.json` | coder-mysql | Always | 2 |
| EV-07 / R-01,03,06,13 | MariaDB whole-lock preservation + source fail-closed; original OF obligation explicitly pending | C-MARIA — exit 0, selected >0; whole/negative tests pass; NOT a PASS of R-03/MariaDB | `A/focused-mariadb.log`, `A/focused-mariadb.exit.json`, `A/cases-mariadb.json`, `A/mariadb-constraint.md` | coder-mariadb | Always; unsupported capability does not remove the row | 2 |
| EV-08 / R-01,02,03,06 | SQL Server global/targeted/composed hints | C-SS — exit 0, selected >0; all-source and isolation assertions pass | `A/focused-sqlserver.log`, `A/focused-sqlserver.exit.json`, `A/cases-sqlserver.json` | coder-sqlserver | Always | 2 |
| EV-09 / R-05,07 | Unsupported-provider/caching negatives | C-SQLITE and C-CH — both exit 0, each selected >0; explicit exception cases pass | `A/focused-sqlite.log`, `A/focused-clickhouse.log`, `A/unsupported-exits.json`, `A/cases-unsupported.json` | coder-negative | Always | 2 |
| EV-10 / R-01,03,06,09,11,13 | Live focused transaction probes + full integration boundary; server versions | C-INT-FOCUS and C-INT — both exit 0; focused selected >0; provider execution inventory explicit; MariaDB dedicated tests executed; no host-induced provider skips | `A/integration-focused.log`, `A/integration.log`, `A/integration-exits.json`, `A/provider-execution.json`, `A/live-lock-cases.json` | coder-integration | Always | 2 |
| EV-11 / R-11 | Seven-project unit/provider boundary | C-BOUNDARY — each command exit 0; complete per-project counts/logs | `A/boundary-*.log`, `A/boundary-summary.json` | coder-boundary | Always | 2 |
| EV-12 / R-12 | Acceptance/T216 baseline and after; BDN exports, wall/Mean/Allocated/ratios | C-ACCEPTANCE and C-T216, baseline and after — all exit 0; acceptance exactly 7 cases/no failures; thresholds evaluated | `A/perf/baseline/**`, `A/perf/after/**`, `A/perf/runs.json`, `A/perf/comparison.json` | coder-perf | Always: plan identity touched | 2 |
| EV-13 / R-12 | Coverage collect/report; Cobertura/TextSummary/HTML, thresholds, assembly inventory | C-COV-COLLECT then C-COV-REPORT — both exit 0; preserve produced files; line/branch values and 85/75 comparison recorded | `A/coverage/collect.log`, `A/coverage/report.log`, `A/coverage/exits.json`, `A/coverage/coverage.cobertura.xml`, `A/coverage/report/`, `A/coverage/summary.json` | coder-coverage | Always | 2 |
| EV-14 / R-10 | Public docs build and EN/RU/provider-claim parity | C-DOCFX — exit 0; scout Task reviewing the eight EN/RU docs; success, all obligations covered | `A/docs/docfx.log`, `A/docs/docfx.exit.json`, `A/docs/parity-review.md` | coder-docs + scout-docs | Always | 2 |
| EV-15 / R-01…14 | CHECK completeness, row-level verdict and priority application | check Task evaluating frozen rv2 rows and `T216-P1-SQL-OPERATOR-r2`; preserve R-03/MariaDB as constraint pending owner; success; every required row reported; no false overall PASS | `A/check/verdict.md`, `A/check/rows.json`, `A/manifest.json` | check-C01 | Always | 2 |
| EV-16 (new) / R-03,14 | Preserve owner obligation/disclosure | check Task verifying R-03/MariaDB remains explicitly pending owner and the final report states the two owner choices; success; absent owner decision remains constraint, not PASS | `A/check/owner-constraint.md`, `A/owner-decision.json` | check-C01 | Always; owner-decision artifact may record "not received" | 2 |
| EV-17 (new) / R-01,11 | Reproducible SQL Server red→green regression | C-SS before D-04: nonzero from the expected all-source assertion (not compilation/environment error); C-SS after D-04: exit 0; selected >0 both runs | `A/redgreen/sqlserver-red.log`, `A/redgreen/sqlserver-green.log`, `A/redgreen/results.json` | coder-regression | Always | 2 |
| EV-18 (new) / R-11,14 | Frozen manifest structural validation and identity/revision proof | C-MANIFEST — exit 0; correct task/rv, complete required rows, append-only CHECK history | `A/check/manifest-validation.log`, `A/check/manifest-validation.exit.json`, `A/check/freeze.json` | check-C01 | Always | 2 |

Verdict and pending owner: technical MariaDB fail-closed checks must pass; **the original R-03/MariaDB gets no PASS**; the pending obligation stays open/constraint and is disclosed in CHECK. While any required row is `open`, a full PASS is forbidden. The routed escalation authorizes implementation and reporting of the executed part with the retained constraint; it does not authorize falsely completing #216. An absent owner answer does not by itself trigger a pointless corrective DO iteration and does not reset attempts. Gates/no-fourth remain.

Finite CHECK re-gather budget — owner check-C01: at most **2 targeted scout calls** to verify implementation evidence and at most **1 targeted rerun** of an already-pinned command for incomplete/lost evidence (total max 3 actions); no repeated feasibility gather and no repo-wide re-gather. After exhaustion, missing evidence is a GAP (not PASS, not automatic DO). Persistent low confidence is routed to `escalate` (trigger 5). The known MariaDB owner constraint does not consume budget to re-prove impossibility.

## Final report and next step

The final report must contain: **"R-03 (MariaDB, `OF <alias>`) infeasible; owner must decide: (i) approve narrowing to unsupported and update #216 text/criteria, or (ii) defer MariaDB source lock beyond 1.0.9."** This text does not itself perform either owner choice. The task and all its executable work stay in **1.0.9**; no move without an owner decision.

Exact next allowed step: coder saves this plan verbatim in the status file before DO; the separately requested status-commit does not authorize implementation commits (no push/merge). Control returns to the collection parent at the PLAN→DO boundary. After gate-1 and shared-file ownership are fixed, the parent hands coder **D-01**, then **D-02**, sequentially. No further `go` is awaited inside the collection handoff; DO is not executed from the planner role. After D-06: freeze the rv=2 manifest → CHECK with the pinned budget and the retained MariaDB constraint.

## Changed files

- `docs/specs/status/D216-row-locking-1.md` (this file; created at the PLAN step, committed with prefix `#216`).

## Pointers

- Issue: https://github.com/AlexeyShirshov/nextorm/issues/216
- Collection: `docs/specs/status/collection-1.0.9-todo.md`
- Frozen manifest (planned): `artifacts/pdca/D216/rv2/manifest.json`
- Superseded contract: rv=1

## 10. Owner decision (received) — вариант (i)

**Дата:** 2026-10-13. **Решение владельца:** (i) — признать MariaDB source-level lock (`FOR UPDATE OF`/`FOR SHARE OF`)
**неподдерживаемым by design** и обновить текст/критерии #216. Вариант (ii) (defer за пределы 1.0.9) не выбран.

Что это означает:

- Реализованное поведение не меняется и не является дефектом: `ILockRenderer.SupportsSourceLocks = false` для MariaDB,
  детерминированный `NotSupportedException` на source-level lock, без подмены whole-query локом, без игнорирования
  таргета и без эмиссии `OF`. Требование **R-13** выполнено.
- Исходное **R-03** (собственно `OF <alias>` на MariaDB) снимается как критерий: критерий сужен до
  «unsupported / fail-closed». **R-14** (retain the constraint) исполнено — ограничение задокументировано явно.
- #216: текст/критерии обновлены на GitHub, issue **закрыт** (комментарий с решением и уликами).
- D-07: `blocked` → `resolved` — это был единственный остаток исходного #216 requirement.
- Публичная документация правок **не требует**: EN/RU уже описывают MariaDB как unsupported/fail-closed
  (`docs/advanced/limitations.md:27`, `docs/advanced/api-reference.md:160`, `docs/guide/32-provider-specific-features.md:111-122`
  и RU-зеркала) — R-10 остаётся выполненным.
- Заморозка публичного API (**T53/#53**) **переехала в майлстон `1.0.9-rc3`** (из `1.0.9`) и ведётся в её собственной задаче — решение по ней в записи T216 не требуется; 10 публичных членов с тегом #216 остаются кандидатами в её реестре.

**Запись решения:** `artifacts/pdca/D216/rv2/check/owner-decision.json` (`status: received`, `chosen_option: (i)`) и
`artifacts/pdca/D216/rv2/check/owner-constraint.md` (раздел Resolution). **Замороженный rv=2 манифест не переписывается:**
строка EV-16 остаётся `open` как снапшот состояния цикла на момент ACT; решение — внешнее событие после ACT и фиксируется
здесь, в `owner-decision.json` и в GitHub #216.

## Progress log

```text
2026-10-12T04:39:15Z | PLAN | r=2 | n=1/3 | PLAN ready — awaiting confirmation (collection plan_handoff; DO not started) | status file docs/specs/status/D216-row-locking-1.md
2026-10-13T00:00:00Z | DO | r=2 | n=1/3 | DO started — D-01..D-04M; planner footprint corrections applied (docs guide/32, Expressions comparers) | scope /tmp/nextorm-t216-scope.json (brief exit 0)
2026-10-13T00:10:00Z | DO | r=2 | n=1/3 | D-01..D-04M implemented; contracts+renderers+guards committed 4d979efc; join-source-lock fixed 3ef11a3c | logs /tmp/nextorm-d01-*.log /tmp/nextorm-{core,postgres,mysql,mariadb,sqlserver}-t216b.log /tmp/nextorm-maria-{red,green}.log
2026-10-13T00:20:00Z | DO | r=2 | n=1/3 | D-05 part 1: PlanKeyUniqueness 13->14 selected; full slnx build 0/0; focused matrix rows closed (guards/unsupported/mode-wait/empty-target); CRLF normalized | logs /tmp/nextorm-plankey.log /tmp/nextorm-full-build2.log /tmp/nextorm-{core,sqlite,ch,pg,mysql,mariadb,ss}-*t216*.log
2026-10-13T00:30:00Z | DO | r=2 | n=1/3 | D-05 part 2: focused integration locking run (PG/MySQL/SS 2 passed each; SQLite 2 capability-skipped; MariaDB dedicated 3 fail-closed; capability 5); fixed SQL Server hint-after-alias ordering; full slnx build 0/0 | logs /tmp/nextorm-int-*-lock*.log /tmp/nextorm-full-build3.log
2026-10-13T00:40:00Z | DO | r=2 | n=1/3 | Docs (EN+RU): whole-query vs source-level row locking, SQL Server all-source behavior change, PG/MySQL OF, MariaDB fail-closed; docfx exit 0 (2 pre-existing source-generator warnings) | logs /tmp/nextorm-docfx.log
2026-10-13T00:50:00Z | DO | r=2 | n=1/3 | Perf acceptance 7 cases / 0 failures (wall 51 s; cached/prepared ratio 2.03 vs baseline 1.87, below 2.244); T216 cached-path benchmark 6 cases (no slower than whole-lock); artifacts committed 04442903 | logs /tmp/nextorm-acceptance.log /tmp/nextorm-t216-bench.log
2026-10-13T01:00:00Z | DO | r=2 | n=1/3 | Boundary sweep (7x PDCA=T216 + LockRendererDefaultTests 5 + PlanKeyUniqueness 13 + integration 9 [1 SQLite skip]) all exit 0; validate_inner_loop report exit 0; coverage line 88.7% / branch 80.8% (>= 85/75) | logs /tmp/nextorm-boundary-*.log /tmp/nextorm-cov-*.log
2026-10-13T01:10:00Z | DO | r=2 | n=1/3 | DO complete → CHECK: frozen rv=2 manifest `artifacts/pdca/D216/rv2/manifest.json` (EV-01..EV-18, validator exit 0; EV-17 satisfied, EV-16 open/pending-owner); D-01..D-06 done, D-07 blocked; #216 OPEN | manifest artifacts/pdca/D216/rv2/manifest.json
2026-10-13T01:30:00Z | DO | r=2 | n=2/3 | CHECK FAIL loop-back: fixed D216-ALIAS-CASE (alias case-fold under KeywordCase.Upper) and D216-SS-APPLY-GLOBAL-LOCK (APPLY source hint); slnx build 0/0; PDCA=T216 core 2 / pg 12 / mysql 7 / maria 3 / ss 5 all green; MariaDB fail-closed green | logs /tmp/fix-*.log
2026-10-13T01:45:00Z | DO | r=2 | n=2/3 | DO complete again → CHECK: boundary re-run (core 2/pg 12/mysql 7/maria 3/ss 5/sqlite 1/ch 1; integration 9 [1 SQLite skip]; report validator exit 0); coverage line 88.7% / branch 80.8% preserved; EV case maps/exit-records/provider-inventory/perf-provenance/public-api/recon added; manifest re-validated exit 0; EV-16 open, EV-17 met | manifest artifacts/pdca/D216/rv2/manifest.json
2026-10-13T02:00:00Z | DO | r=2 | n=3/3 | DO started (n=3): CHECK n=2 FAIL (completeness) routed back; fix stale attempt n; fulfil pre-change baseline (EV-12), unfiltered C-BOUNDARY (EV-11), live MariaDB whole-lock (EV-10/V-27) | status docs/specs/status/D216-row-locking-1.md
2026-10-13T02:30:00Z | DO | r=2 | n=3/3 | DO complete → CHECK: pre-change baseline at 666cf9ac (ratio 1.98 vs after 2.03, +2.6%); unfiltered C-BOUNDARY all 7 provider projects 0 failed; live MariaDB whole-lock 3/3 executed, source fail-closed; manifest re-validated exit 0; EV-16 open, EV-17 met | manifest artifacts/pdca/D216/rv2/manifest.json
2026-10-13T03:00:00Z | DO | r=2 | n=3/3 | ACT-prep: escalation (A) done-with-constraint, no product change; EV-02/EV-14 accepted reviews, EV-16 owner constraint materialized (open), EV-18 freeze persisted, EV-12 T216 baseline N/A at 666cf9ac (acceptance 1.98->2.03, +2.5%); manifest re-validated exit 0; #216 OPEN | manifest artifacts/pdca/D216/rv2/manifest.json
2026-10-13T03:30:00Z | ACT | r=2 | n=3/3 | ACT complete — done-with-constraint: EV-15 verdict+rows persisted, manifest EV-15 met (validator exit 0); T53 candidates registered (10, #216), freeze HELD; collection T216 row pending→done; next pending T217 (order 11); #216 OPEN, D-07 blocked | artifacts/pdca/D216/rv2/check/verdict.md
2026-10-13 | OWNER | r=2 | n=3/3 | Owner decision received — вариант (i): MariaDB source-level lock unsupported by design; #216 criteria narrowed+updated, issue CLOSED; D-07 blocked→resolved; R-03 superseded by the narrowed criterion, R-14 retained; docs unchanged (EN/RU already say unsupported); T53 freeze moved to milestone 1.0.9-rc3 (was 1.0.9) | artifacts/pdca/D216/rv2/check/owner-decision.json; https://github.com/AlexeyShirshov/nextorm/issues/216
```
