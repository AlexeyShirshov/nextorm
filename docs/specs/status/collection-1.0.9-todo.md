# Collection 1.0.9-todo

- collection-id: `1.0.9-todo`
- input: milestone **renamed `1.0.9` → `1.0.9-rc3`**; scope re-read 2026-10-14. Open in `1.0.9-rc3`: {#201 (perf), #213, #219, #225, **#232 (new)**, **#233 (new)**} + follow-ups {#234, #235}. Original set (now closed): #209, #210, #211, #212, #215, #216, #217, #218, #220, #221, #222, #223, #224, #226, #227, #228, #229, #230. Active lane: T225, T232, T233, T201 (perf last). #53 (freeze) is in the `1.0.9` milestone — **out of rc3 scope**, held.
- base: branch `1.0.9` @ `58025147` (merge of #214 `1.0.9-rc2`); the branch was later **renamed to `1.0.9-rc3`** by a concurrent actor (the collection's commits rode along); HEAD advanced by PLAN/DO commits.
- mode: autonomous; auto-commit authorized (task/collection status files and task paths only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- phase: **D — lane G1 resumed** (T217 accepted as **done-with-caveat** by the owner: product delivered/green, R217-08 evidence-provenance deferred to follow-up issue **#234**; fail-stop lifted); single group → current worktree/branch `1.0.9-rc3`, no worktree/branches/merge

## Input task list

Selected variant: `pdca-dotnet` for all tasks (C#/.NET repo). Each task runs its own real PLAN at the P phase, stops at the PLAN→DO boundary, and the lane resumes the **same** cycle from DO.

| id | issue | title | selected_variant | plan_state |
|---|---|---|---|---|
| T209 | #209 | eager-load strategy — explicit strategy + auto-dispatcher | pdca-dotnet | done (r=3) |
| T211 | #211 | eager-load per-parent Take/Skip (absorbs #210) | pdca-dotnet | done (r=4, rv=4) |
| T212 | #212 | eager-load nested — level 2+ LoadWith | pdca-dotnet | done (r=1) |
| T213 | #213 | JoinInto — composite keys / M:N single-query | pdca-dotnet | ready (r=1) |
| T201 | #201 | Perf: M12 #3 — cached-path parity with LINQ to DB | pdca-dotnet | ready (r=2) |
| T215 | #215 | убрать зависимость OneOf | pdca-dotnet | ready (r=1) |
| T216 | #216 | row locking — FOR UPDATE vs FOR UPDATE OF | pdca-dotnet | done (r=2, rv=2, n=3/3) — done-with-constraint → resolved: owner decision (i) 2026-10-13 (MariaDB source-level lock unsupported by design); #216 narrowed & CLOSED; D-07 resolved |
| T217 | #217 | DateTimeOffset — SQL-трансляция Now/UtcNow и частей даты | pdca-dotnet | done-with-caveat (r=5, rv=5; R217-01..07 met, R217-08 → #234) |
| T218 | #218 | SQL Server sequences (NEXT VALUE FOR), кросс-провайдерно | pdca-dotnet | done-with-caveat (r=1, rv=1; E07/E08/E10 → #235) |
| T53 | #53 | IF6: public API freeze (PublicApiAnalyzers + PublicAPI.*) | pdca-dotnet | ready (r=1) — **HELD by owner** |
| T232 | #232 | SQL Server PIVOT/UNPIVOT — типизированная проекция результата (вместо TableAlias по имени) | pdca-dotnet | pending (new, rc3 scope) |
| T233 | #233 | Window functions — перегрузка `Over` (один partitionBy + один WindowOrder) | pdca-dotnet | pending (new, rc3 scope) |

## Groups

| id | tasks (order) | worktree | branch/ref | status |
|---|---|---|---|---|
| G1 | T209–T213 (done) → T215 → T227 → T221 → T222 → T223 → T216 → T217 → T218 → T219 → T226 → T224 (done) → T225 (done) → T232 → T233 → T201 (perf) | no (single group) | current branch `1.0.9-rc3` | in-progress (scope rc3: +T232/T233; T53 out of rc3 scope, held) |

Re-cluster finalized (`planner`): single lane (shared builders/dialects/Query/docs force one writer). T224 (SQL-format blast radius, ~697 exact-string asserts) sits late, after all functional changes; T225 (docs-only) after T223+T224; T201 (perf) measures the final composition; T53 held last. Addendum contract `cluster-1`/`cluster-2`; task contracts unchanged.

## Tasks

| id | issue | group | order | status | plan_state | selected_variant | cycle_id | plan_revision | PLAN commit | status file |
|---|---|---|---|---|---|---|---|---|---|---|
| T209 | #209 | G1 | 1 | done | done | pdca-dotnet | N=1 | r=3 | 5e9f6756 | docs/specs/status/D209-eager-strategy-1.md |
| T211 | #211 | G1 | 2 | done | done | pdca-dotnet | N=1 | r=4 | 6da00a11 | docs/specs/status/D211-eager-perparent-topn-1.md |
| T212 | #212 | G1 | 3 | done | done | pdca-dotnet | N=1 | r=1 | 034e5b45 | docs/specs/status/D212-eager-nested-1.md |
| T213 | #213 | G1 | 4 | done | done (r=6, rv5) | pdca-dotnet | N=1 | r=6 | 9e7facff | docs/specs/status/D213-joininto-remainder-1.md |
| T215 | #215 | G1 | 5 | done | ready | pdca-dotnet | N=1 | r=1 | 23c83497 | docs/specs/status/D215-oneof-removal-1.md |
| T227 | #227 | G1 | 6 | done | done | pdca-dotnet | N=1 | r=1 | aea0a9ba | docs/specs/status/D227-clickhouse-flaky-grouped-float-1.md |
| T221 | #221 | G1 | 7 | done | done (r=1, rv=1, n=2/3) | pdca-dotnet | N=1 | r=1 | f884c52b | docs/specs/status/D221-interceptor-di-registration-1.md |
| T222 | #222 | G1 | 8 | done | done (r=1, rv=1, n=3/3) | pdca-dotnet | N=1 | r=1 | 5856fde2 | docs/specs/status/D222-join-overloads-by-type-1.md |
| T223 | #223 | G1 | 9 | done | done (r=1, rv=1, n=2/3) | pdca-dotnet | N=1 | r=1 | e2d749db | docs/specs/status/D223-joininto-composite-junction-sql-1.md |
| T216 | #216 | G1 | 10 | done | done-with-constraint → resolved (owner decision (i) 2026-10-13; #216 CLOSED) | pdca-dotnet | N=1 | r=2 | 1b7b9d8c | docs/specs/status/D216-row-locking-1.md |
| T217 | #217 | G1 | 11 | done-with-caveat | done (r=5, rv=5) — R217-01..07 met, R217-08 → follow-up #234; product `94782852` | pdca-dotnet | N=1 | r=5 | 4108dc6e | docs/specs/status/D217-datetimeoffset-1.md |
| T218 | #218 | G1 | 12 | done-with-caveat | done (r=1) — E07/E08/E10 → #235; product `22b96233` | pdca-dotnet | N=1 | r=1 | 60d881b4 | docs/specs/status/D218-sqlserver-sequences-1.md |
| T219 | #219 | G1 | 13 | done | done (r=1, rv=1, n=1/3) — CHECK PASS r=1/rv=1; E219-01..09 all met; product `b5f8b360` | pdca-dotnet | N=1 | r=1 | 4bfa5403 | docs/specs/status/D219-session-identity-functions-1.md |
| T226 | #226 | G1 | 14 | done | done (r=2, rv=2, n=2/3) — CHECK PASS; 11/11 rows met | pdca-dotnet | N=1 | r=2 | 485b1943 | docs/specs/status/D226-records-affected-1.md |
| T224 | #224 | G1 | 15 | done | done (r=1, rv=1, n=1) — CHECK PASS r=1/rv=1; R1–R7 met; DO `4e6a2abd` | pdca-dotnet | N=1 | r=1 | 4c0a403d | docs/specs/status/D224-sql-from-join-formatting-1.md |
| T225 | #225 | G1 | 16 | done | done (r=4, rv=3, n=1/3) — CHECK PASS r=4/rv=3; E01–E07 met, C1–C8 met | pdca-dotnet | N=1 | r=4 | 7cbbab3b | docs/specs/status/D225-joininto-mn-docs-delta-1.md |
| T232 | #232 | G1 | 17 | pending | ready | pdca-dotnet | N=1 | r=1 | 4f1e1282 | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md |
| T233 | #233 | G1 | 18 | pending | ready | pdca-dotnet | N=1 | r=1 | 82da47a9 | docs/specs/status/D233-window-over-overload-1.md |
| T201 | #201 | G1 | 19 (perf) | pending | ready | pdca-dotnet | N=1 | r=2 | bd4af157 | docs/specs/status/D201-cached-parity-1.md |
| T53  | #53  | — | out of rc3 scope (held) | held | ready | pdca-dotnet | N=1 | r=1 | (untracked, D53 file) | docs/specs/status/D53-public-api-freeze-1.md |

- Evidence contract (per task, frozen at that task's DO→CHECK): T209 rv=2 (done), T211 rv=2, T213 rv=5 (done), T216 rv=2, T226 rv=2 (done, 11/11 rows met), others rv=1. Manifests `artifacts/pdca/<task>/rv*/manifest.json`.
- T221 (#221) done: CHECK PASS r=1/rv=1/n=2/3; DO `f884c52b`, corrective DO `4b49b063`, ACT `9cb2e14f`. Evidence `artifacts/pdca/D221/rv1/manifest.json` (E221-01..09 met; build 0/0; UN 46/46, Q 10/10, QN 9/9, integration 21/21 on five providers, 0 skipped; Stryker 76/68 killed/8 survived, 0 NoCoverage), `coverage-delta.md`, `mutation-disposition.md`, `ownership-evidence.md`; status `docs/specs/status/D221-interceptor-di-registration-1.md`. Issue #221 closed. T53 freeze stays HELD.
- T211 history: r1 exhausted (3/3 CHECK fails; EV-11 exit-code capture gap, no product defect); owner-directed re-open → r=2 with a real exit-code capture wrapper; retained commits `e98ae60e`, `5bd3028f`, `15c6253f` (not reverted). r3/r4 evidence remediation closed EV-11 (canonical+restart real exits); CHECK PASS r=4/rv=4 (21/21 met, 0 open); evidence `artifacts/pdca/D211-eager-perparent-topn-1/rv4/`.
- T213 history: r=1 → r=6 (5 escalations). r=4 direction D narrowed scope (scalar M:N + composite keys stay; composite-junction M:N → #223); r=5 removed the broken D03 mixed-projection widening and handed off AC07 docs → #225; r=6 evidence recovery (keyless-parent proof + controlled coverage collect). CHECK PASS r=6/rv5. Closure: `T213 done partial: narrowed contract, AC03/AC05 → #223, AC07 → #225, both in milestone 1.0.9.` Evidence `artifacts/pdca/T213/rv5/manifest.json` (32/32 met/no-open, validators exit 0). Issue #213 kept OPEN (remaining slices #223/#225).
- T222 (#222) done: variant pdca-dotnet, r=1, rv=1, n=3/3; final CHECK PASS (A222-01..07 all met). DO `5d9ff209`, `d5cc558f`, `535fb429`; corrective DO `b1b44438`, `670cb470`; final corrective `2ab0fddb`; ACT-evidence `d8e6317e`. Evidence `artifacts/pdca/D222-join-overloads-by-type/rv1/` (E222-CHECK/check-report.md; manifest+evidence validators exit 0; build 0/0; core InMemoryJoinTests 29/29; six provider TypeOnlyJoinSqlGenerationTests 7/7; coverage line 88.7 / branch 80.7; integration 3779 total/0 failed with 0 provider-unavailable skips; ninth-join probe compiles). Status `docs/specs/status/D222-join-overloads-by-type-1.md`. Follow-ups: (a) 05-subqueries EN:59/RU:61 terminology “cap/потолок” → “scope bound” — trigger docs terminology sweep; (b) V30 separate MariaDB DB infrastructure — trigger infrastructure/provider regression; (c) no Cross/Apply source-less, no type-only raw-table, no new terminal-arity-8 overload — trigger separate approved task. T53 freeze stays HELD (28 members registered, not activated). Issue #222 left OPEN. **Next pending = T223.**
- T223 (#223) done: composite-junction M:N materializable link carrier across six providers (standalone + SingleQuery). r=1, rv=1, n=2/3; final CHECK PASS. Build 0/0; 7 project suites exit 0 (core 2456, sqlite 1672+1 opt-in skip, postgres 897, sqlserver 814, mysql 380, mariadb 265, clickhouse 662); LIVE `~JoinInto` 124/124, 0 skipped, six providers; six provider SQL-gen green; coverage line 83.4 / branch 76.0 (1.0.9 warning-only); manifest `artifacts/pdca/D223/rv1/manifest.json` 17/17 met, validator exit 0; variant matrix 29/29 test/guard, no deferred; perf scalar repeat classified noise. DO commit `d76c5ae3`; issue #223 closed. Status `docs/specs/status/D223-joininto-composite-junction-sql-1.md`. **Next pending = T216 (order 10).**

- T216 (#216) done (ACT, done-with-constraint): r=2, rv=2, n=3/3; escalation decision **(A)** close in-cycle as done-with-constraint, no r+1, no product change. DO commits `4d979efc`,`3ef11a3c`,`1849164e`; corrective DO `6d22a504` (D216-ALIAS-CASE, D216-SS-APPLY-GLOBAL-LOCK); evidence `artifacts/pdca/D216/rv2/manifest.json` (EV-01..EV-18; EV-16 open). **R-03/MariaDB constraint pending owner decision** (narrow MariaDB to unsupported vs defer); **#216 kept OPEN**; **D-07 blocked**; **T53 freeze stays HELD**. Status `docs/specs/status/D216-row-locking-1.md`. **Next pending = T217 (order 11).**
- **Group G1 fail-stop — SUPERSEDED by owner decision.** T217 failed terminally at r=5/rv=5 on the `D217-EVIDENCE-BINDING` family (fixes applied 3; no fourth attempt/replan); T218/T219/T226/T224/T225/T201 were marked `incomplete` («группа остановлена»). **Owner decision:** accept T217 as **done-with-caveat** — product delivered/green (`94782852`, MT:135 closed), R217-08 evidence-provenance deferred to **#234**; fail-stop lifted; T218…T201 reinstated `pending`; T53 stays `held`; #217 stays OPEN.

## Phase decisions

- **Own PLANs:** original 6 re-finalized `ready`. Expansion 1: T215/T216/T217/T218 planned `ready`; T211 re-opened to `ready (r=2)`. Expansion 2: T219/T221/T222/T223/T224/T225/T226/T227 planned `ready` (T225 re-opened r=2 after its external-docs blocker was resolved by a committed docs change; T227 re-run after an aborted attempt).
- **ALL-barrier: re-PASSED (2nd expansion)** — every active task has a persisted `plan_state=ready` (T53 deliberately `held`; T216 carries an owner-only open constraint but its plan is `ready`). Re-cluster on the expanded set is pending before the lane resumes.
- **Public-API candidates flagged for the held T53 freeze:** T216 adds `FromOptions`/`JoinOptions` lock members + `ILockRenderer.SupportsSourceLocks` + a new `Render` overload; T217 adds `ISqlDialect.SupportsDateTimeOffsetTranslation` + `MakeDateTimeOffsetNow` + DTO-aware `MakeDatePart`/`MakeDateAdd` overloads; T218 adds `SqlFunctions.SqlServer.next_value_for`/`nextval`; T213 adds `ISqlDialect.WrapIntegerWindowResult` (new public default interface method) + `QueryCommand.NeedsOuterJoinNullSettings` (ClickHouse scalar M:N fix); T215 changes the package graph (no surface delta); T221 adds `DataContextBuilder.AddInterceptor(Type)` + `AddInterceptor<T>()` (DI-registered interceptors; internal ctor is not a candidate). T53 stays held until the owner declares the API final.
- **T53 freeze candidates — D216 (#216) registered (freeze stays HELD, not activated):** `FromOptions.ForUpdate()`, `FromOptions.ForUpdate(LockWaitMode)`, `FromOptions.ForShare()`, `FromOptions.ForShare(LockWaitMode)`; the same four on `JoinOptions`; `ILockRenderer.SupportsSourceLocks`; `ILockRenderer.Render(LockMode, LockWaitMode, KeywordCase, string? targetAlias)` — **10 members**, tag `#216`.
- **T53 freeze candidates — D219 (#219) registered (freeze stays HELD, not activated):** `SqlFunctions.SqlServer.server_name(): string?`, `SqlFunctions.SqlServer.host_id(): string?`, `SqlFunctions.SqlServer.host_name(): string?`, `SqlFunctions.SqlServer.app_name(): string?`, `SqlFunctions.SqlServer.suser_name(): string?`, `SqlFunctions.SqlServer.original_login(): string?`; `SqlFunctions.Sql.pg_backend_pid(): int`, `SqlFunctions.Sql.inet_server_addr(): string?`, `SqlFunctions.Sql.inet_server_port(): int?`, `SqlFunctions.Sql.inet_client_addr(): string?`, `SqlFunctions.Sql.inet_client_port(): int?`, `SqlFunctions.Sql.connection_id(): ulong`, `SqlFunctions.Sql.user(): string`; `SqlFunctions.ClickHouse.host_name(): string`, `SqlFunctions.ClickHouse.tcp_port(): int`; `ISqlServerFunctions.SupportsSessionIdentity` (bool, default interface implementation `false`) — **16 members**, tag `#219`.
- **Re-cluster (`planner`, expansion):** no genuine parallelism (eager/JoinInto/locking overlap `EntityBuilder`/SQL/query contracts; T216/T217/T218 overlap provider dialects; docs have external uncommitted edits). **One group G1** = `T211 → T212 → T213 → T215 → T216 → T217 → T218 → T201 → T53 (held last)`. Single group → no worktree/branch/merge. Addendum contract `cluster-1`; task contracts unchanged.
- **Preconditions before the relevant writes (D-COLL-2/3):** establish ownership of the external dirty doc/benchmark intersections, and get the owner's T216 MariaDB decision, before T215/T216/T218 DO.

## Re-scope amendment 2 (owner directive)

- Milestone grew again; owner: **pick up the new tasks** (#219, #221, #222, #223, #224, #225, #226, #227); **performance (T201) and freeze (T53) at the end**.
- #223/#225 are the T213 slices (SQL composite-junction M:N link carrier; docs delta); #227 is the pre-existing ClickHouse flaky test.
- Barrier re-opened: the 8 new tasks have no PLAN yet → own PLANs required before the expanded lane resumes.
- Ordering: new tasks precede T201; T201 precedes the held T53.

## Owner constraint — T216 (#216) — RESOLVED (2026-10-13)

- R-03/MariaDB `FOR UPDATE OF <alias>`: MariaDB grammar has **no `OF`**. `escalate` routed a revised r=2 plan with **MariaDB fail-closed as provisional default** (not a waiver). Owner options were:
  - (i) approve narrowing MariaDB source-lock to **unsupported** (update #216 text/criteria), or
  - (ii) **defer** MariaDB source lock (whole-query lock only).
- **Decision (2026-10-13): (i).** MariaDB source-level locking is **unsupported by design** (fail-closed, not a defect); #216 text/criteria narrowed and updated; **#216 CLOSED**; D-07 `blocked` → `resolved`; docs unchanged (EN/RU already state unsupported). Record: `docs/specs/status/D216-row-locking-1.md` §10.
- T53 freeze is **not this task's concern**: #53 moved to milestone `1.0.9-rc3` (from `1.0.9`), so its release decision is taken in that task; the 10 public members tagged #216 remain candidates in its registry.

## Working-tree note

- The branch has **39 uncommitted entries not produced by this collection**: 15 `benchmarks/BenchmarkDotNet.Artifacts/results/*` (tracked, modified), ~20 EN/RU docs files (modified), and 4 untracked `docs/{,ru/}scalar-functions/10-system-functions.md` + `12-sequences.md`. The collection does **not** touch or commit them; tasks preserve them. Ownership/commit ordering must be settled before T215/T218 DO (their doc footprints overlap the untracked files).

## Общая верификация и восстановление

- Verification state (C): not reached — collection mid-flight (re-scope/re-cluster).
- Last collection `C` result: —
- Blocker: **none** — lane G1 moved on; **T219 (#219) done** (CHECK PASS r=1/rv=1/n=1/3; no fail-stop, no escalation). (Prior history: T218 CHECK/escalate in-cycle STOP → owner accepted `done-with-caveat`, E07/E08/E10 → #235; earlier T217 terminal `incomplete` `D217-EVIDENCE-BINDING` → owner accepted `done-with-caveat` → #234.)
- Defect id / history: `D217-EVIDENCE-BINDING` — r=2/n=3 → r=3/n=1 → r=4/n=1 → r=5/n=1, each a CHECK FAIL at the evidence gate (evidence-only regeneration / hash replacement); terminal `incomplete`.
- Corrective-task status file: — (none created; the remedy is an **outside-T217** proposal, a human decision)
- Next allowed step: (1) own PLANs for the new rc3-scope tasks **T232 (#232)** and **T233 (#233)**; (2) resume lane G1: **T225 → T232 → T233 → T201 (perf last)**. Done in this lane: T209/T211/T212/T213/T215/T216/T219/T221/T222/T223/T224/T226/T227; done-with-caveat: T217 (→#234), T218 (→#235). #53 (freeze) is **out of rc3 scope** and held. #216 CLOSED (owner decision (i)).

## Done / Verified / Incomplete

- Done: T209 (#209) — eager-load strategy (explicit strategy + auto-dispatcher); cycle N=1 r=3 n=2/3, CHECK PASS, contract rv=2.
- Done: T212 (#212) — eager-load nested level 2+ LoadWith (graph contract + split/single orchestration + tests); cycle N=1 r=1, CHECK escalated, ACT closed, R1–R9 met.
- Done: T213 (#213) — JoinInto composite-key remainder (partial) — composite keys (1:N/1:1) + in-memory composite junction + scalar M:N standalone/`SingleQuery` across six providers incl. MariaDB; explicit preparation rejection for SQL composite-junction M:N → #223; docs handoff → #225. Cycle N=1, CHECK PASS r=6/rv5. `T213 done partial: narrowed contract, AC03/AC05 → #223, AC07 → #225, both in milestone 1.0.9.` Issue #213 stays OPEN (remaining slices #223/#225).
- Verified: T209 (#209) — build 0/0; six dialect suites 0 failures; integration all 5 providers executed; coverage line 88.5% / branch 80.5%; acceptance benchmark 7/7, no ratio >20%; manifest `artifacts/pdca/D209/rv2/manifest.json` E209-01..21 all `met`, validator exit 0.
- Verified: T212 (#212) — build 0/0; core full 2371/2371 (core eager 193/193); dialect eager sqlite 59 / pg 7 / sqlsrv 7 / mysql 9 / ch 7 (0 fail/skip); integration 78/78 all 5 providers 0 skips; coverage line 88.6% / branch 80.6%; manifest `artifacts/pdca/T212/rv1/manifest.json` E01–E08 all `met`, validator exit 0; R8 accepted via escalate — `Nextorm_GroupByCount` +5.50% not attributable (unreachable in-memory path, allocations byte-identical, non-reproducible; `check2/E08-paired.md`, `E08-disposition.md`); EB:1869 red→green fix.
- Verified: T213 (#213) — build 0/0; core 2395/2395; six SQL suites 0 failed; integration all six providers, 0 required skips (ClickHouse 263/263 incl. the scalar M:N fix); coverage 88.6% line / 80.7% branch (4 assemblies incl. sqlserver 95%); manifest `artifacts/pdca/T213/rv5/manifest.json` 32/32 met/no-open, validators exit 0; CHECK PASS r=6.
- Done: T227 (#227) — ClickHouse flaky grouped-float fixture-order fix (eager `_sut` init before the portable oracle); cycle N=1 r=1 rv=1, CHECK PASS, all E01–E09 met; fix commit 4dcbc6ec, DO evidence 41c7fcaf.
- Verified: T227 (#227) — affected+solution builds 0/0; 16/16 isolated green; class 35/35; all-ClickHouse 265/265; validators exit 0; manifest artifacts/pdca/D227-clickhouse-flaky-grouped-float/rv1/manifest.json (tree 9f11957d, 9/9 met, 0 open).
- Done: T221 (#221) — DI-registered interceptors (`DataContextBuilder.AddInterceptor(Type)`/`AddInterceptor<T>()` resolving from the DI builder's scope + internal provider ctor + provider capture in both factories); CHECK PASS r=1/rv=1/n=2/3; DO `f884c52b`, corrective DO `4b49b063`; issue #221 closed.
- Verified: T221 (#221) — build 0/0; UN 46/46, Q 10/10, QN 9/9, integration 21/21 on SQLite/PostgreSQL/SQLServer/MySQL/ClickHouse (0 skipped); coverage after line 11.4% / branch 7.8% (`DataContextBuilder` 91.7%, `ServiceCollectionExtensions` 100%); Stryker 76 tested / 68 killed / 8 survived (0 NoCoverage); manifest `artifacts/pdca/D221/rv1/manifest.json` E221-01..09 met, manifest+report validators exit 0.
- Done-with-caveat: T217 (#217) — product delivered/green (`94782852`: DateTimeOffset server-side translation, core + 6 dialects + tests + docs; MT:135 closed). Evidence contract r=5/rv=5: R217-01..07 met, **R217-08 unmet** (evidence completeness/tree-bound provenance) → deferred to follow-up **#234** (atomic evidence generator + stale-hash lint). Issue #217 stays OPEN. Status `docs/specs/status/D217-datetimeoffset-1.md`.
- Incomplete (superseded): T211's earlier `incomplete`; T212/T213/T201/T53 «группа остановлена» — reinstated as `pending` (T53 `held`) by the expansion directive. (T213 no longer applies; it closed partial at r=6 — see above.)
- Done: T223 (#223) — composite-junction M:N materializable link carrier (internal flat entity-item carrier + identity mapping) across six providers incl. standalone and SingleQuery; cycle N=1 r=1 rv=1 n=2/3, final CHECK PASS. Guard `JunctionLinkSourceFactory` rejection replaced only for the composite path; scalar M:N and public API unchanged. `contract-verdict.md` at `artifacts/pdca/D223/rv1/check/`; issue #223 closed.
- Verified: T223 (#223) — build 0/0; 7 project suites exit 0; LIVE `~JoinInto` 124/124 six providers 0 skipped; six provider SQL-gen green; coverage line 83.4% / branch 76.0%; manifest `artifacts/pdca/D223/rv1/manifest.json` 17/17 met, validator exit 0; variant matrix 29/29 (no deferred); CHECK PASS r=1 rv=1 n=2/3.
- Done: T219 (#219) — session/server identity functions across six providers (SQL Server `server_name`/`host_id`/`host_name`/`app_name`/`suser_name`/`original_login` + `ISqlServerFunctions.SupportsSessionIdentity`; PostgreSQL `pg_backend_pid`/`inet_*`; shared `connection_id`; MySQL/MariaDB `user`; ClickHouse `host_name`/`tcp_port`; fail-closed `NotSupportedException`); cycle N=1 r=1 rv=1 n=1/3, CHECK PASS r=1/rv=1, acceptance R219-01..08 met. Commit product `b5f8b360` (28 files), evidence `fa1dc550`, CHECK re-gather `be468abc`. Issue #219 stays **OPEN**; T53 freeze stays HELD (16 members recorded, tag `#219`).
- Verified: T219 (#219) — build 0/0; C2 six providers 3496 tests 0 failed 0 skipped; C3 core InMemoryTests 183; C4 integration 394 executed / 0 skipped (SQLServer/PostgreSQL/MySQL/MariaDB/ClickHouse/SQLite); C5 boundary 11838 total / 0 failed / 199 capability-reason skips / 0 provider skips; coverage line 88.12% / branch 80.39% (≥85/75); C8 DocFX exit 0; manifest `artifacts/pdca/D219/rv1/manifest.json` E219-01..09 all met, validators exit 0; Stryker scoped (core+core.tests) 41 tested / 34 killed / 7 survived / score 80.95%, all 7 survivors record-only; security no findings; perf exemption confirmed independently.

## History (superseded current-state facts)

- Original input 6 tasks, G1 order `T209 → T211 → T212 → T213 → T53`; superseded by the amended order + expansion.
- T201 was `gap → excluded-gap` (`9bf13403`); superseded by the approved design rev 2 and r=2 plan (`bd4af157`).
- ALL-barrier previously passed on the original 6-task set; re-opened by the expansion, now re-passed.


## T226 (#226) — Done / Verified (appended)

- Lane update: **T226 done** (revision r=2, evidence contract rv=2, iteration n=2/3, CHECK PASS, 11/11 required rows met incl. E226-09-BATCH-ROWS and E226-09-N6); the G1 lane row is annotated accordingly and **G1 itself stays `in-progress`** (next task **T224**). The collection is NOT done and G1 is NOT done.
- Done: T226 (#226) — `ProcedureResult.RecordsAffected` / `BatchResult.RecordsAffected` captured at reader release and exposed get-only; literal returned-row mutation obligation satisfied; fail-closed mutation verifier with six negative controls; provenance erratum; T53 freeze stays HELD with both members registered (tag `#226`).
- Verified: T226 (#226) — manifest 11/11 `met` (validator exit 0), evidence report validator exit 0, audit `audit_miss=0`, seven-step seal ledger exit 0 each (`artifacts/pdca/D226/rv2/validation-ledger.{log,json}`), bound report sha256 `bc0d0a38103a2f51ad99c6a99da0546f8ec87415a0818c8956c5099d77b720b5`, coverage line 88.7 % / branch 80.8 %, five providers executed with 0 skips (ClickHouse 5/5), rv1 `sha256sum -c rv1-frozen.sha256` exit 0 (48/48), production bytes unchanged (`cf030526…`, `a166586f…`, `9e5a15b1…`, `9bf20094…`).


## T224 (#224) — Done / Verified (appended)

- Lane update: **T224 done** (revision r=1, evidence contract rv=1, iteration n=1/3, CHECK PASS, R1–R7 met and E01–E07 met/closed); the G1 lane row is annotated accordingly and **G1 itself stays `in-progress`** (next task **T225**, not started). The collection is NOT done and G1 is NOT done.
- Done: T224 (#224) — SQL formatting: unconditional LF before `FROM`/`JOIN` (default layout, no opt-in flag, no nesting indentation layer); library `AppendLine()`/platform newlines replaced by explicit `'\n'` across the SQL emitters (`SqlText.NewLine`), opaque raw/literal SQL contents and the parameter-only path untouched. DO commit `4e6a2abd`; issue #224 closed.
- Verified: T224 (#224) — build 0/0; 11/11 affected projects exit 0 (solution sweep 11888 total / 0 failed); D160 eight byte-exact baselines re-signed + positional invariant 3/3; integration 3904 total / 0 failed with SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse and the MariaDB container case executed; coverage line 87.3 % / branch 79.9 % (>=85/75); manifest `artifacts/pdca/D224/rv1/manifest.json` E01..E07 all `met`, validators exit 0; public-docs normalisation idempotent (34 EN + 34 RU pages), suppression scan 0/692 added lines; `src`/`tests` unchanged since HEAD `4e6aabde`.


## T232 (#232) — DO (r=3) typed SQL Server PIVOT/UNPIVOT (appended)

- Lane update (r=2/r=3 outcome note): **T232 in progress** — cycle N=1, plan `r=3`, evidence contract `rv=3`, iteration `n=2/3`; the binding `escalate` decision authorized exactly one `n=2` fix inside the r=3 branch (no planner r=4, no `n=3` for that defect). `D-05` is **evidence-complete** (r=3 delta sealed: C21 29/29, C22 11/11, C23 eight suites + container boundary 3922/0-failed/**0 provider skips**, C07 six-provider 6/6, C31 9/9 mutants killed) — this is **not** a CHECK verdict; `D-06` (docs/example) is now executed under this same r=3 branch, `D-07` (boundary/handoff) remains open. Evidence `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/`; status `docs/specs/status/D232-sqlserver-pivot-unpivot-1.md`.
- **T53 freeze candidates — D232 (#232) registered (freeze stays HELD, not activated):** typed `PIVOT`/`UNPIVOT` surface — `public sealed class NextORM.Core.PivotValues` + `public NextORM.Core.PivotValues.PivotValues()` + `public T NextORM.Core.PivotValues.Value<T>(string columnName)`; `public sealed class NextORM.Core.UnpivotValues` + `public NextORM.Core.UnpivotValues.UnpivotValues()` + `public T NextORM.Core.UnpivotValues.Name<T>(string nameColumn)` + `public T NextORM.Core.UnpivotValues.Value<T>(string valueColumn)`; `public static NextORM.Core.EntityBuilder<TShape> NextORM.SqlServer.SqlServerEntityBuilderExtensions.Pivot<TEntity, TShape>(this NextORM.Core.EntityBuilder<TEntity> builder, NextORM.Core.PivotAggregate aggregate, System.Linq.Expressions.Expression<System.Func<TEntity, object?>> aggregateColumn, System.Linq.Expressions.Expression<System.Func<TEntity, object?>> forColumn, System.Linq.Expressions.Expression<System.Func<TEntity, NextORM.Core.PivotValues, TShape>> projection)`; `public static NextORM.Core.EntityBuilder<TShape> NextORM.SqlServer.SqlServerEntityBuilderExtensions.Unpivot<TEntity, TShape>(this NextORM.Core.EntityBuilder<TEntity> builder, System.Linq.Expressions.Expression<System.Func<TEntity, NextORM.Core.UnpivotValues, TShape>> projection, params NextORM.Core.UnpivotColumn[] columns)` — **9 surfaces (2 new public types + 2 public parameterless ctors + 3 accessors + 2 overloads)**, tag `#232`. The legacy untyped `Pivot`/`Unpivot` overloads are unchanged and are **not** new candidates; `PIV-RD2` (P2 OPEN / [#53](https://github.com/AlexeyShirshov/nextorm/issues/53)) is preserved.
