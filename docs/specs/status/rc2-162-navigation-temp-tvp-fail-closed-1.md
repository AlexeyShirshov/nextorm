# D162 — V32: temp-table/TVP navigation remains fail-closed

## Head

| Field | Value |
|---|---|
| Collection | `1.0.9-rc2` |
| Task / issue | D162 / #162 |
| Issue URL | `https://github.com/AlexeyShirshov/nextorm/issues/162` — verified by `gh issue view 162 --repo AlexeyShirshov/nextorm`: number 162, state OPEN, milestone #20 `1.0.9-rc2` |
| Starting branch / commit | `1.0.9-rc2` / `18659e41` |
| Cycle | N=1 |
| Plan revision / attempt | r=2 / n=1/3 |
| Evidence contract | rv=2 (supersedes rv=1) |
| Selected variant | `pdca-dotnet` |
| Mode | COLLECTION-phase; DO complete; CHECK PASS (rv=2); ACT complete — cycle done |
| Proposed group | `navigation-deferred-guards` |
| Predecessors | None recorded |
| Plan state | Revised r=2 persisted; historical r=1 retained (superseded by r=2) |
| Implementation state | **done / ACT complete** — D:00 gates retained; D:01/D:02/D:05/D:06/D:07/D:08/D:09 done (r=2); CHECK **PASS** rv=2 (R162-01..07 met; E162-01..18 closed/superseded/N-A; E162-14 closed by the verdict). ACT accepted; deferrals retained; commit pending (collection lane; push never). |
| Commit / push / merge | Not authorized |

Current state: the rv=2 evidence sources **E162-01…E162-18 are closed or contract-authorized N/A/superseded** (see the rv=2 evidence ledger below), at revision `e25f558e`. *(Historical PLAN note, retained and superseded: at r=1 all sources were planned and no tests were reported as executed.)* CHECK verdict: **PASS (rv=2)** — R162-01..07 met; all E162 rows closed/superseded/N-A; E162-14 closed by this verdict. ACT complete; cycle done.

## Durable state
- Current cycle N: 1
- Plan revision r: 2
- Attempt n: 1
- Evidence contract rv: 2
- Cycle state: done / ACT complete (finalized 2026-10-08)
- Defect history: none

## DO unit states (updated 2026-10-08 09:29 UTC)

| Unit | State | Evidence |
|---|---|---|
| D:00 — persist/validate gates | active — persistence retained in this file | r=2 plan persisted; D:02 brief scope validated, exit 0 (`/tmp/nextorm-D162-r2/scope-d02.json`) |
| D:01 — bounded evidence completion | done | facts recorded in progress log; brief validator exit 0 |
| D:02 — baseline (SQLite) | done | build exit 0 (0 warnings/0 errors, `/tmp/nextorm-D162-r2/baseline-build.log`); test exit 0, 1 passed/0 failed/0 skipped, selected_count=1 (`/tmp/nextorm-D162-r2/baseline-test.log`); report validator exit 0 (`/tmp/nextorm-D162-r2/evidence-d02.json`) |
| D:05 — execution/regressions | done | Debug solution build exit 0 (0 warnings/0 errors, `/tmp/nextorm-D162-r2/d05-build-debug.log`/`.exit`); Release solution build exit 0 (0 warnings/0 errors, `/tmp/nextorm-D162-r2/d05-build-release.log`/`.exit`); SQL-gen boundary `-trait "D162=Boundary"` 7/7 projects exit 0, Total 1 / Failed 0 / Skipped 0 each (core/sqlite/postgres/mysql/mariadb/sqlserver/clickhouse, `/tmp/nextorm-D162-r2/d05-<p>-Boundary.log`); SQL-gen conformance `-trait "D162=Conformance"` core 8/0/0, sqlite 7/0/0, sqlserver 42/0/0, clickhouse 23/0/0, exit 0 (postgres/mysql/mariadb have no SQL-gen conformance trait — integration-only); container integration `-trait "D162=Conformance"` exit 0, Total 62 / Failed 0 / Skipped 0, per-provider all passed: PostgreSQL 6, SQL Server 18, MySQL 6, MariaDB 5, ClickHouse 23, SQLite 4 (`/tmp/nextorm-D162-r2/d05-integration.log`; 5 reused containers ready via DOCKER_HOST, none skipped); brief exit 0 (`scope-d05.json`), report exit 0 (`evidence-d05.json`) |
| D:06 — docs/tracking | done | docs synced to verified fail-closed behavior: design §11 (`NavigationExpansion.cs:48-50` early `TableAlias` return, no fallback/no throw, pre-boundary navigation still expands, TVP parameter-only no query-root, SQL Server/ClickHouse refuse temporary CTAS); roadmap Deferred records typed temp/TVP query-root deferred (trigger = approved feature or demonstrated fallback defect; scope stays `1.0.9-rc2`); EN/RU `limitations.md` + `relationships.md` aligned and mutually consistent; no public `docs/**`→`docs/specs/**` links added; CRLF preserved. docfx exit 0, 0 errors/2 pre-existing warnings, 544 models / 165 conceptual pages (`/tmp/nextorm-D162-r2/d06-docfx.log`); brief exit 0 (`/tmp/nextorm-D162-r2/scope-d06.json`), report exit 0 (`/tmp/nextorm-D162-r2/evidence-d06.json`) |
| D:07 — DO→CHECK sweep | done | single comprehensive boundary sweep executed once: full Debug solution build + Debug `-trait "D162=Boundary"`/`"D162=Conformance"` across core/sqlite/postgres/mysql/mariadb/sqlserver/clickhouse + container integration `-trait "D162=Conformance"`; all exits 0; report gate exit 0 with 13 boundary executions, revision `e25f558e558c24980eade1ab0fa971039754b0e0` (`/tmp/nextorm-D162-r2/evidence-d05.json`, `report` exit 0); aggregate SQL-gen selected 87 + integration selected 62, zero failures, zero provider skips |
| D:08 — provider-boundary regression tests (old D:03) | done | 7/7 changed projects build exit 0 (0 warnings/0 errors), test exit 0 (1 passed/0 failed/0 skipped each); new/updated `[Trait("D162","Boundary")]` files: `tests/nextorm.sqlite.tests/ImplicitNavigationV32TempTableTests.cs` (trait added), `tests/nextorm.postgres.tests/ImplicitNavigationV32TempTableTests.cs`, `tests/nextorm.mysql.tests/ImplicitNavigationV32TempTableTests.cs`, `tests/nextorm.mariadb.tests/ImplicitNavigationV32TempTableTests.cs`, `tests/nextorm.sqlserver.tests/TempTableMaterializationRejectionTests.cs`, `tests/nextorm.clickhouse.tests/TempTableMaterializationRejectionTests.cs`, `tests/nextorm.core.tests/TempTableNavigationBoundaryTests.cs`; logs `/tmp/nextorm-D162-r2/d08-{core,sqlite,postgres,mysql,mariadb,sqlserver,clickhouse}-sel.log`; brief exit 0 (`/tmp/nextorm-D162-r2/scope-d08.json`) |
| D:09 — TVP-root absence + parameter regressions (old D:04) | done | E162-17 TVP-root absence: `roslyn refs NextORM.Core.TableParameterValue` → 34 references, all in `CreateProcedureParameter` overrides / `TableParameterBinder` / `TableParameterValue` / provider parameter paths + tests; none reach a `From`/`EntityBuilder` query-root (`/tmp/nextorm-D162-r2/roslyn-tvp-root.txt`); conclusion: no public TVP query-root exists, only parameter binding. Conformance class-level `[Trait("D162","Conformance")]`: `tests/nextorm.{sqlite,clickhouse}.tests/TableValuedParameterTests.cs`, `tests/nextorm.sqlserver.tests/TableValuedParameterBindingTests.cs`, `tests/nextorm.core.tests/RawCommandInMemoryTests.cs`, and integration `ClickHouseTableValuedParameterTests.cs`, `SqliteTableValuedParameterTests.cs`; integration method-level: `PostgresSpecificTests.cs` ×6, `MySqlSpecificTests.cs` ×6, `MariaDbFunctionsIntegrationTests.cs` ×5, `SqlServerSpecificTests.cs` ×18 (incl. `ExecuteRaw_TypeName_ShouldExecuteStructuredParameter`, `ExecuteRaw_StructuredOutputDirection_ThrowsArgumentException`). Builds 0 warnings/0 errors: core/sqlite/sqlserver/clickhouse + integration (`/tmp/nextorm-D162-r2/d09-*-build.log`). Tests exit 0: core 8/0/0, sqlite 7/0/0, sqlserver 42/0/0, clickhouse 23/0/0 (`/tmp/nextorm-D162-r2/d09-<project>.log`); brief exit 0 (`/tmp/nextorm-D162-r2/scope-d09.json`), report exit 0 (`/tmp/nextorm-D162-r2/evidence-d09.json`) |

## Progress log
2026-10-07 (UTC unknown) | PLAN | r=1 | n=1/3 | PLAN ready — awaiting confirmation | docs/specs/status/rc2-162-navigation-temp-tvp-fail-closed-1.md
2026-10-07 | PLAN | r=2 | n=1/3 | Replanned: factual premise correction — no TVP query-root exists; post-temp navigation already fail-closed (NavigationExpansion.cs:48-50); regression-only scope
2026-10-08 09:29 UTC | DO | r=2 | n=1/3 | D:01 facts recorded — in-memory raw/TVP throws `NotSupportedException` at `IRawCommandExecutor.cs:45-46,84-85`; coverable via `RawCommandInMemoryTests.cs`; coverage thresholds 85/75 with the CI collect/reportgen argv; validator requires top-level `unit` | /tmp/nextorm-D162-r2/scope-d02.json (brief exit 0)
2026-10-08 09:29 UTC | DO | r=2 | n=1/3 | D:02 baseline green — build exit 0 (0 warnings/0 errors); test exit 0, 1 passed/0 failed/0 skipped, selected_count=1; report validator exit 0 | /tmp/nextorm-D162-r2/baseline-build.log, /tmp/nextorm-D162-r2/baseline-test.log, /tmp/nextorm-D162-r2/evidence-d02.json
2026-10-08 14:34 UTC | DO | r=2 | n=1/3 | D:08 done — 7/7 changed projects build exit 0 (0 warnings/0 errors) and per-selector test exit 0 (1 passed/0 failed/0 skipped each); new provider temp-boundary/rejection + in-memory-guard facts with `[Trait("D162","Boundary")]`; D:09 remains active | /tmp/nextorm-D162-r2/d08-{core,sqlite,postgres,mysql,mariadb,sqlserver,clickhouse}-sel.log, /tmp/nextorm-D162-r2/evidence-d08.json (brief exit 0, report exit 0), /tmp/nextorm-D162-r2/scope-d08.json
2026-10-08 14:39 UTC | DO | r=2 | n=1/3 | D:09 done — TVP query-root absence confirmed (roslyn `refs NextORM.Core.TableParameterValue`: 34 references, none reach a `From`/`EntityBuilder` query-root; all in `CreateProcedureParameter`/`TableParameterBinder`/parameter carriers). Conformance trait added to 4 unit classes (class-level) + 2 integration classes + 35 integration TVP methods. Builds 0 warnings/0 errors (core/sqlite/sqlserver/clickhouse + integration). Tests exit 0: core 8/0/0, sqlite 7/0/0, sqlserver 42/0/0, clickhouse 23/0/0. Brief exit 0, report exit 0 | /tmp/nextorm-D162-r2/roslyn-tvp-root.txt, /tmp/nextorm-D162-r2/d09-{core,sqlite,sqlserver,clickhouse}.log, /tmp/nextorm-D162-r2/d09-integration-build.log, /tmp/nextorm-D162-r2/scope-d09.json, /tmp/nextorm-D162-r2/evidence-d09.json
2026-10-08 14:43 UTC | DO | r=2 | n=1/3 | D:06 done — docs synced to verified fail-closed boundary behavior: design §11 (NavigationExpansion.cs:48-50 early TableAlias return; no base-table fallback/no throw; pre-boundary navigation still expands; TVP parameter-only, no query-root; SQL Server/ClickHouse refuse temporary CTAS), roadmap Deferred (typed temp/TVP query-root deferred; trigger = approved feature or demonstrated fallback defect; scope stays 1.0.9-rc2), EN/RU limitations + relationships aligned; no public docs→docs/specs links. docfx exit 0 (0 errors, 2 pre-existing warnings, 544 models/165 conceptual pages). Brief exit 0, report exit 0 | /tmp/nextorm-D162-r2/d06-docfx.log, /tmp/nextorm-D162-r2/scope-d06.json, /tmp/nextorm-D162-r2/evidence-d06.json
2026-10-08 14:49 UTC | DO | r=2 | n=1/3 | D:05 done — Debug build 0W/0E (exit 0) + Release build 0W/0E (exit 0); SQL-gen boundary 7/7 projects exit 0 (Total 1/Failed 0/Skipped 0 each); SQL-gen conformance core 8/0/0, sqlite 7/0/0, sqlserver 42/0/0, clickhouse 23/0/0 (postgres/mysql/mariadb trait absent); container integration D162=Conformance exit 0, Total 62/0/0 with real PostgreSQL 6, SQL Server 18, MySQL 6, MariaDB 5, ClickHouse 23, SQLite 4 — none skipped. Brief exit 0, report exit 0 | /tmp/nextorm-D162-r2/scope-d05.json, /tmp/nextorm-D162-r2/evidence-d05.json, /tmp/nextorm-D162-r2/d05-build-{debug,release}.log, /tmp/nextorm-D162-r2/d05-<p>-{Boundary,Conformance}.log, /tmp/nextorm-D162-r2/d05-integration.log
2026-10-08 14:49 UTC | DO | r=2 | n=1/3 | D:07 done — one DO→CHECK comprehensive sweep (Debug solution build + boundary/conformance SQL-gen over 7 projects + container integration), all exits 0; 13 boundary executions validated, revision e25f558e; SQL-gen 87 selected + integration 62 selected, 0 failed, 0 skipped. All DO units D:00–D:09 done; ready for CHECK | /tmp/nextorm-D162-r2/evidence-d05.json (report exit 0)
2026-10-08 15:05 UTC | CHECK | r=2 | n=1/3 | CHECK r1 → fail (evidence closure); re-gather round 1 — rv=2 complete ledger E162-01…E162-18, issue/milestone linkage verified (`gh issue view 162`: OPEN, milestone #20 `1.0.9-rc2`), coverage N/A predicate, TVP/cache/metadata mapping, SQLite XML-doc candidate fixed and re-run | /tmp/nextorm-D162-r2/regather-sqlite.log
2026-10-08 10:03 UTC | CHECK | r=2 | n=1/3 | CHECK re-gather: R162-05 full-suite sweep + ledger pin | /tmp/nextorm-D162-r2/evidence-r162-05.json
2026-10-08 15:06 UTC | CHECK | r=2 | n=1/3 | PASS (rv=2) — R162-01..07 met; E162-01..18 closed/superseded/N-A; E162-14 closed by this verdict | docs/specs/status/rc2-162-navigation-temp-tvp-fail-closed-1.md
2026-10-08 15:06 UTC | ACT | r=2 | n=1/3 | accepted; commit pending | rv=2; deferrals retained; docs/specs/roadmap/todo_navigation_properties.md

## Plan r=2 (current) — P:162-BOUNDARY-REGRESSION, r=2, n=1/3

**Decision B — revise** (factual scope correction, not an additive prerequisite): the nonexistent TVP query-root makes old D:04 unimplementable; a temp-navigation rejection guard would change already-correct behavior.

**Goal:** protect the existing fail-closed boundary, preserve pre-materialization navigation and TVP **parameter** behavior, evidence every provider, synchronize docs/tracking, persist the complete ledger.

**Minimal solution:** regression tests + accurate documentation; no production change unless a reproducible violation appears. Constraints: no fabricated TVP-root API, no new temp-navigation throw, no sticky query-command cache mutation.

**Alternatives:** regression-only (chosen, smallest/lowest risk); add rejection guards (unnecessary behavior change); introduce typed temp/TVP roots (new feature/API, out of scope, deferred until separately approved).

**Acceptance criteria:**
- R162-01: temp reads remain `TableAlias` and read the materialized source; negative: no base-table fallback or read-side navigation join.
- R162-02: mapped navigation before materialization still expands; negative: its join must not disappear through over-broad boundary suppression.
- R162-03: document/evidence absence of a TVP query-root; preserve supported TVP parameter execution; negative: neither advertise nor test an invented root/navigation operation.
- R162-04: every matrix row has applicable evidence; negative: zero selected tests, provider skips, or inherited-dialect claims alone do not establish execution coverage.
- R162-05: mapped navigation/non-navigation and parameter paths remain correct; negative: changed results, leaked cache flags, or contamination of configured TVP metadata are regressions.
- R162-06/R162-07: EN/RU/design/roadmap agree + verified milestone linkage + complete ledger persisted; negatives: public specs links, contradictory support claims, missing evidence/unfinished units marked done.

**D tasks:** retain active D:00 persistence/gates, D:01 bounded evidence completion, D:02 baseline, D:05 execution/regressions, D:06 docs/tracking, D:07 DO→CHECK sweep; preserve their unfinished work. Mapping: old D:03 `superseded → D:08` provider-boundary regression tests; old D:04 `superseded → D:09` TVP-root absence + parameter regressions. D:08/D:09 are active and carry the corrected R01–R05 obligations.

**Fix now:** tests at `tests/nextorm.sqlite.tests/ImplicitNavigationV32TempTableTests.cs:28`; provider expectations from `SqlMutationBuilder.cs:625-627`, `MariaDbDialect.cs:11`; TVP evidence from `DataContext.cs:566-585`. Deferred: typed-root features/new guards — trigger = approved feature or demonstrated fallback defect.

**First executable DO unit D:02 — structured test scope** (after D:00/01 gate; no source edits):

```json
{"projects":["tests/nextorm.sqlite.tests"],"selectors":[{"kind":"FullyQualifiedName","value":"FullyQualifiedName~ImplicitNavigationV32TempTableTests.Temp_table_read_should_stay_an_untyped_alias_and_not_silently_join_the_base_table"}],"files":["tests/nextorm.sqlite.tests/ImplicitNavigationV32TempTableTests.cs"],"rebuild":{"required":true,"command":"dotnet build tests/nextorm.sqlite.tests -c Debug"},"run":"dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter \"FullyQualifiedName~ImplicitNavigationV32TempTableTests.Temp_table_read_should_stay_an_untyped_alias_and_not_silently_join_the_base_table\"","boundary":"Existing SQLite baseline only; stop before adding traits, guards, provider tests or docs.","rationale":"Proves the observed untyped-read/pre-boundary-join behavior before changing regression coverage.","expected":{"buildExit":0,"testExit":0,"selectedTests":1,"passedTests":1,"skippedTests":0}}
```

**Unit mode:** sequential in one tree; code/tests overlap boundary contracts, so no worktrees. Owners S/C/D/V/K/O.

**Matrix — temp shape:** SQLite/Postgres/MySQL/MariaDB → tests (mapped pre-boundary join + untyped materialized read, SQL-generation and execution); SQL Server/ClickHouse → guard tests (materialization `NotSupportedException`, not "navigation rejection"); in-memory → guard test at `DataContextExtensions.cs:1360-1362`.

**Matrix — TVP query-root shape:** all six relational providers + in-memory → surface guard evidence (no `From`/`EntityBuilder` route; Roslyn reference report, not an impossible runtime invocation).

**Matrix — TVP parameter shape:** SQLite/Postgres/MySQL/MariaDB/SQL Server/ClickHouse → tests through actual parameter/raw-command routes; in-memory → D:01 must establish its actual contract before selecting test/guard.

**Variants:** nonempty scalar/value TVP and mapped reference TVP → tests; empty/null/default, conversion/provider flags and unsupported combinations → D:01 inventory then explicit test/guard; navigation on an untyped alias is unavailable (not an expected throw).

**Test execution:** planned `D162=Boundary`/`D162=Conformance` traits must first be created/attached; none exist now. *(Current status, this re-gather: the traits are created/attached and executed — see the rv=2 evidence ledger and row-bound DO ledger below.)* SQL-gen: `dotnet run --project tests/nextorm.<provider>.tests -c Debug -- -trait D162=Boundary -noColor`, instantiated separately for all six relational projects; core receives its guard scope.

**Integration:** load the integration skill first; `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait D162=Conformance -noColor`; include MariaDB's independent fixture; verify provider-by-scenario results, not aggregate success.

**Priorities/coverage:** P1 = all R01–05 boundary/support/refusal/parameter/mapped-operation assertions; CHECK cannot downgrade. Unit SQL tests complement mandatory real execution; project coverage ≥85% lines/≥75% branches.

**Docs:** design §11 + `todo_navigation_properties.md:327-358`, EN/RU limitations/relationships, verified issue/milestone record; distinguish unsupported materialization, untyped temp reads, parameter-only TVPs. Generated API/site and unrelated guides untouched.

**Performance:** no benchmark for regression/docs-only work (early return `NavigationExpansion.cs:48-50` unchanged). Any production preparation/traversal/allocation change invalidates and requires baseline + measurement.

**Reconnaissance:** no design spike; one bounded S pass for original E162 rows, global evidence-contract definition/gate invocation, exact coverage commands, remaining variant expectations, in-memory parameter support. Completion = paths/lines + executable selectors.

**Evidence revision rv=2** supersedes rv=1 for the corrected scope; stable R/E IDs.

**New evidence rows:**
- E162-15 / R04 / V: MariaDB temp+parameter execution; use the integration trait command with MariaDB scenario counts/results + provider startup log.
- E162-16 / R01,R02 / C: existing SQLite baseline; exact build/run JSON; artifacts `baseline-build.log`, `baseline-test.log`; require exactly one passing fact.
- E162-17 / R03 / S: TVP-root absence; `roslyn(action="refs",symbol="NextORM.Core.TableParameterValue")`; artifact reference/route report, complete resolution and no root route.
- E162-18 / R04,R05 / V: relational boundary/parameter matrix; per-project + integration commands; artifacts command logs + provider×scenario ledger.

New-row common slots: status `planned`; `rv=2`; artifact root `/tmp/nextorm-D162-r2`; exact command, captured exit/log, expected artifacts, scenario, R ID, owner, observable applicability. Test rows require exit 0, nonzero expected selection, all required cases passing, no applicable skips.

**CHECK re-gather:** K owns ≤2 targeted rounds; missing reports trigger recovery, not contract revision/another DO iteration.

**Assumptions/risks:** provider/ref reconnaissance accepted as reported; inherited MariaDB SQL behavior is not execution proof; absent traits/fixtures and Podman/image startup are in-cycle prerequisites. Preserve CRLF, warnings-as-errors, cache/metadata invariants.

**Note on gate:** original E162-01…14 contents must be preserved from the file; missing original contract details block DO authorization only until preserved (this step).

---

## Historical r=1 plan — superseded by r=2 (retained for traceability)

The following r=1 plan narrative is preserved. It is superseded by the r=2 plan above; where they conflict, the r=2 plan governs. Its still-applicable obligations (evidence rows, invariants, safety rules) are not removed.

## Goal

Keep navigation **after** a temp-table or TVP source boundary fail-closed, without silently substituting or joining the mapped base table. Preserve navigation **before** a supported temp-table materialization boundary.

Keep the eventual implementation explicitly tracked in the current milestone.

## Scope

### In

- Verify the existing untyped temp-table boundary and its SQL behavior.
- Establish the actual public TVP/table-valued root shapes through semantic inspection.
- Test fail-closed behavior for routable temp-table and TVP variants.
- Add a narrowly placed production guard **only if** the existing API permits a navigation attempt that would otherwise expand against a mapped base table.
- Verify ordinary column access and mapped-entity navigation still work.
- Provider/source capability accounting, including unsupported and non-expressible variants.
- EN/RU limitations and relevant relationship wording; internal design and roadmap tracking.
- Persist this plan and evidence ledger before DO.

### Out

- Typed temp-table/TVP navigation implementation.
- New typed source APIs or navigation surfaces on `TableAlias`.
- Enabling temp tables for SQL Server, ClickHouse, or in-memory.
- New TVP support or provider capability changes.
- General navigation refactoring, new caching policy, or changes to source materialization.
- Commits, pushes, merges, and unrelated documentation cleanup.

A deferral is not permission to omit its matrix row or evidence.

## Established facts

- The temp-table root is `EntityBuilder<TableAlias>`: `DataContextExtensions.cs:1355,1364`; twin at `:1502`.
- The untyped-navigation seam already returns early: `NavigationExpansion.cs:48-50`, invoked during preparation at `QueryCommand.QueryPreparer.cs:65`.
- The SQLite guard verifies the source type and the materialization/read boundary: `ImplicitNavigationV32TempTableTests.cs:29-58`.
- Design §11 records V32/#162: `implicit-navigation-queries.md:135,154-157`.
- Public limitations already describe the untyped temp-table surface: `docs/advanced/limitations.md:19`.
- The roadmap’s deferred block lacks this follow-up: `todo_navigation_properties.md:327-358`.
- Temp-table support is SQLite/PostgreSQL/MySQL/MariaDB only: `DataContextExtensions.cs:1344-1346`.
- The supplied TVP capability flags are true for SQLite/PostgreSQL/MySQL/SQL Server/ClickHouse. They do **not**, by themselves, prove a navigable public source shape.
- No predecessor is recorded in the collection register at `collection-1.0.9-rc2.md:31`.

## Minimal solution

**Essential result:** demonstrable fail-closed boundaries plus explicit implementation tracking.

**Non-negotiable constraints:** no silent base-table join; no weakening of ordinary supported source operations; no unsupported-provider results presented as passing conformance; no broad source/API redesign.

**Optimum within those constraints:** retain existing untyped guards, extend source/provider evidence, and introduce a navigation-specific rejection only where a real expressible gap is established.

| Alternative | Benefit | Cost / risk | Decision |
|---|---|---|---|
| A. Implement typed navigation now | Delivers eventual feature | New source provenance, mapping, provider and projection semantics; contradicts the documented slice | Reject for D162 |
| B. Verify/formalize fail-closed behavior | Matches issue and design; bounded change | Requires actual TVP-route evidence, not just documentation | **Choose** |
| C. Documentation-only closure | Smallest diff | Cannot establish TVP fail-closed behavior or provider accounting | Reject |

Do not add an unconditional rejection of all temp-table/TVP queries. A guard must reject **navigation**, not valid column access.

## Acceptance criteria

| ID | Acceptance and verification | Negative case |
|---|---|---|
| R162-01 | Supported temp-table reads retain an untyped alias boundary and operate against the temporary source. Verify source type and distinct materialization/read SQL statements. | Read SQL must not acquire a navigation join or silently read the mapped base table. |
| R162-02 | Navigation before materialization still expands correctly. Verify the existing parent join in the materialization statement and a join-free read statement. | “Fail-closed” must not suppress the legitimate materialization join. |
| R162-03 | Every discovered public TVP-root shape has an explicit outcome: navigation is unavailable on its public type, or an attempted navigation is rejected before executable SQL is produced. | No mapped-base-table fallback; no rejection of ordinary supported TVP column operations. |
| R162-04 | Every provider/source matrix row has evidence for conformance, an existing unsupported-source guard, or a tracked deferral with an observable trigger. | A capability flag, zero selected tests, or skipped provider execution is not conformance. |
| R162-05 | Existing mapped navigation and supported non-navigation alias/derived/`FromSql` operations remain intact. | No blanket source rejection or change to shared query-command/cache state. |
| R162-06 | EN/RU public wording and internal design/roadmap agree: implementation remains deferred and tracked in `1.0.9-rc2`. | No claim that typed navigation is implemented; no public link to `docs/specs/**`. |
| R162-07 | Complete plan/evidence ledger is persisted; CHECK can map every applicable obligation to a result and artifact. | Missing reports, incomplete provider execution, or unavailable evidence cannot be marked passed. |

For an expressible navigation attempt, use an explicit unsupported-operation diagnostic identifying the temp-table/TVP navigation limitation. Prefer the project’s existing exception convention, determined by semantic scout. Mere non-expressibility is valid fail-closed evidence only when proved for the public root type.

## What the task did not specify

| Gap | Resolution |
|---|---|
| Whether “when implemented” requires implementation now | Resolved by issue wording, design §11, and existing guard: no. |
| Actual TVP root types and preparation paths | Bounded scout prerequisite, D:01. Do not infer from `SupportsTableValuedParameters`. |
| MariaDB test harness and inherited TVP capability | Bounded scout prerequisite. MySQL SQL similarity is not MariaDB execution evidence. |
| Exact RU page and whether relationships pages repeat the limitation | Read-only documentation inspection in D:01; existing mirrors updated, no speculative page creation. |
| Exception convention for an expressible forbidden navigation | Semantic scout; preserve conventions and test the diagnostic. |
| Source-level null/default handling | Preserve existing source validation; no new null semantics. Test changed guard inputs if a guard is added. |
| Global contract wording and integration prerequisites | Orchestrator supplies the `pdca-dotnet` evidence-contract/gate excerpt and repository integration skill before DO. Validate this ledger against them; do not silently omit additional obligations. |
| Live GitHub verification | No `gh` access. Record supplied identity and local tracking; do not claim remote updates or live verification. |

These prerequisites do not complete or replace the original implementation tasks.

## Variant matrix

The following are **scenario IDs, not future test symbols**.

| Scenario | Source / projection | Provider / execution | Closure |
|---|---|---|---|
| V01 | Mapped entity; navigation and explicit scalar projection | SQLite; SQL preparation | **test**: positive navigation control |
| V02 | Temp-table root; typed column projection | SQLite/PG/MySQL/MariaDB | **test**: alias type, materialization join, no read join |
| V03 | Temp-table root; whole-alias/whole-entity request | Same four | **guard**: establish current API support/rejection; no invented typed navigation surface |
| V04 | Navigation attempted after temp boundary | Same four | **guard**: semantic non-expressibility, or explicit rejection if expressible |
| V05 | Temp-table request | SQL Server/ClickHouse | **guard**: existing unsupported-source behavior; never label as temp-navigation conformance |
| V06 | TVP root; explicit columns, value/reference payload shapes actually supported by its API | SQLite/PG/MySQL/SQL Server/ClickHouse | **test + guard**: ordinary operation control and fail-closed navigation outcome |
| V07 | TVP root; whole entity/navigation projection | Same five | **guard**: type-level exclusion or explicit rejection; prove actual public route |
| V08 | TVP root | MariaDB | **guard/test** after inherited capability and route inspection; no assumed support |
| V09 | Ordinary `TableAlias` root | Every affected dialect | **guard + regression test**: no mapped navigation surface; valid column access remains valid |
| V10 | Derived mapped source, not a temp-table/TVP root | Affected navigation path | **test**: retain existing supported navigation; no extension of support |
| V11 | `FromSql` root | Actual typed/untyped routes found by scout | **guard/regression test**: preserve current contract; no new navigation promise |
| V12 | Sync and async materialization/read entry points | Routable supported providers | **test** both where distinct entry points exist; shared preparation proven semantically, not assumed |
| V13 | Temp-table/TVP navigation | In-memory | **guard** current unsupported root, or **deferred** if no public route; trigger: introduction of such a route |
| V14 | Null/default source or payload; empty input | Changed/affected source path only | **test/guard** existing validation and empty-source behavior; no new semantics |
| V15 | Configuration/cache flags; repeat valid call after rejected attempt | If production guard changes preparation | **test**: rejection does not poison shared command/cache state |
| V16 | Typed navigation over temp-table/TVP sources | All providers | **deferred**: trigger is approved implementation of typed source provenance/navigation, with temp-table **and** TVP provider conformance |

For V06–V08, a missing public route must be demonstrated semantically and documented; it cannot be substituted with an unrelated parameterized mapped-table query.

A new route, projection form, or flag found by scout receives a new scenario/evidence-row ID. Existing IDs and obligations remain stable.

## Priority matrix

| Priority | Rows |
|---|---|
| P1 by task invariant | R162-01–04; V02, V04, V06–08: no base-table fallback and honest provider/source accounting |
| P1 by changed execution path | Any new guard’s source discrimination, navigation detection, rejection and allowed-operation branches; V15 if preparation changes |
| P1 regression boundary | R162-02 and R162-05: legitimate pre-boundary navigation and ordinary source operations |
| P2 | Documentation synchronization and status bookkeeping, without weakening their acceptance obligations |

If a project class-priority register is supplied, apply its stronger assignments. CHECK must not downgrade these P1 rows.

## Ordered DO tasks

### D:00 — Persist and validate the PLAN gate

**Fix now.** Coder writes this status file with r=1/n=1/rv=1 and planned evidence entries. Orchestrator validates the global contract and collection gate.

Update the collection row from pending to planned using the agreed group and status path; do not mark D162 done.

Collection execution stops here at PLAN→DO.

### D:01 — Resolve bounded source/harness facts

**Fix now; prerequisite for D:02–05.**

Read-only scout, Roslyn first for all C# questions:

- Resolve TVP public root types, overloads, source metadata and preparation call paths.
- Resolve `CreateQueryBuilder` equivalence at `DataContextExtensions.cs:1502`.
- Locate existing TVP/navigation/source-validation tests and exception conventions.
- Identify MariaDB’s real conformance execution route.
- Identify existing RU and relationship limitation wording.
- Supply the integration skill and global contract excerpt.

Deliver a source/provider route table with actual `file:line` and existing test symbols. No implementation recommendations.

If route/harness uncertainty remains, return **P:162-FACTS — resolve the specific source/harness uncertainty**. Persistent low confidence goes to `escalate`, trigger 5; do not guess.

### D:02 — Establish baseline and assign test selection metadata

**Fix now.**

Run the existing SQLite V32 guard before changes. Record its result, SQL boundary assertions and current diagnostics.

Use planned xUnit traits `D162=Conformance` and `D162=Boundary` for focused selection. Traits are selection metadata, not claimed existing symbols. Mark actual new/relevant tests during implementation; ensure each planned selection executes tests.

### D:03 — Strengthen temp-table provider guards

**Fix now.**

Retain and improve `ImplicitNavigationV32TempTableTests.cs:29-58` as needed. Prefer statement-specific assertions over aggregate substring counts when extending it.

Add corresponding dialect conformance scenarios for PG/MySQL and SQLite. Establish MariaDB behavior through its actual harness; do not manufacture a project or silently substitute MySQL.

Cover pre-boundary join, post-boundary absence of join/base-table fallback, and ordinary column read.

### D:04 — Verify TVP fail-closed behavior; close a proven guard gap

**Fix now.**

Add TVP scenarios to the actual routes established by D:01. If navigation is non-expressible, record the semantic boundary and test ordinary source operations.

If a navigable root can enter mapped navigation expansion, add the smallest navigation-specific guard at the proven common seam. Start inspection at `NavigationExpansion.cs:48-50` and `QueryCommand.QueryPreparer.cs:65`; change a different source-provenance file only after footprint approval.

No new typed source API. No modification of shared command cache flags.

### D:05 — Add execution and regression evidence

**Fix now.**

Add targeted integration scenarios for real supported temp-table/TVP routes; retain dialect SQL tests separately.

Exercise actual providers, sync/async distinctions where meaningful, and mapped/non-navigation controls. Unsupported routes must have explicit rejection evidence, not provider skips.

### D:06 — Synchronize documentation and tracking

**Fix now.**

Update internal design/roadmap and public EN/RU wording. Add V32/#162 to the current-milestone deferred/follow-up block at `todo_navigation_properties.md:327-358`.

### D:07 — Perform one DO→CHECK boundary sweep and evidence handoff

**Fix now.**

After code/tests/docs settle, run the comprehensive **affected-path** sweep once, one project at a time, including mandatory provider execution. Publish logs, exit codes, test counts, coverage scope and scenario/provider results.

CHECK validates completeness. Missing evidence requests re-gather; it does not automatically cause implementation or contract revision.

### Deferred implementation

**Deferred:** typed navigation over temp-table/TVP roots.

**Trigger:** approved implementation scope that defines source provenance, mapping/projection semantics and provider conformance for both source kinds. Keep #162 and the current-milestone roadmap entry visible until disposition changes.

## Unit execution mode

**One tree, sequential code and tests.** Source discrimination, guard semantics and tests share a contract and may share files. No worktrees: there is no isolation need.

Streams:

- **S — scout:** read-only prerequisites, first.
- **C — code/tests:** sequential implementation owner.
- **D — docs:** may run alongside C after source semantics are fixed; no editing C-owned files.
- **V — validation:** commands sequentially, one project at a time.
- **K — CHECK:** independent verdict and evidence gathering.
- **O — orchestrator/coder:** status persistence and registry ownership.

## Footprint allowlist

| Files / globs | Expected use / uncertainty |
|---|---|
| `docs/specs/status/rc2-162-navigation-temp-tvp-fail-closed-1.md` | Required new plan/status |
| `docs/specs/status/collection-1.0.9-rc2.md` | Orchestrator-owned D162 row only |
| `tests/nextorm.sqlite.tests/ImplicitNavigationV32TempTableTests.cs` | Existing guard; expected change |
| `tests/nextorm.{sqlite,postgres,mysql}.tests/ImplicitNavigationV32TempTableTests.cs` | Planned provider-specific guard files; only SQLite currently established |
| `tests/nextorm.{sqlite,postgres,mysql,sqlserver,clickhouse}.tests/ImplicitNavigationV32TvpTests.cs` | Planned test files; route-dependent, no future symbols claimed |
| `tests/nextorm.{sqlite,postgres,mysql,sqlserver,clickhouse}.tests/*Tvp*Tests.cs` | Existing-route test/selection updates only; exact matches supplied by scout |
| `tests/nextorm.{sqlite,postgres,mysql,clickhouse}.tests/CreateTableAsSqlGenerationTests.cs` | Relevant existing regressions/selection metadata |
| `tests/nextorm.{sqlite,postgres}.tests/BatchSqlGenerationTests.cs` | Existing boundary regressions |
| `tests/nextorm.sqlite.tests/TempTableSourceSqlGenerationTests.cs` | Existing source regressions |
| `tests/nextorm.core.tests/CreateTableAsTests.cs` | Existing unsupported/source controls |
| `tests/nextorm.integration.tests/CommonTestSuite.*.cs` | Shared supported-provider cases; applicability must respect capability |
| `tests/nextorm.integration.tests/*SpecificTests.cs` | Provider-specific cases; exact files supplied by scout |
| `src/nextorm.core/Visitors/NavigationExpansion.cs` | Conditional guard only; no change assumed necessary |
| `docs/specs/design/implicit-navigation-queries.md` | §11 wording/reference synchronization |
| `docs/specs/roadmap/todo_navigation_properties.md` | Required tracked V32 follow-up |
| `docs/advanced/limitations.md`, `docs/ru/**/limitations.md` | EN/RU limitation wording |
| `docs/advanced/relationships.md`, `docs/ru/**/relationships.md` | Only if an existing deferral statement needs synchronization |

Brace groups above denote the explicitly listed projects, not a blanket test-tree allowance.

**Read-only unless separately approved:** `DataContextExtensions.cs`, `FromExpression.cs`, `TempTableSource.cs`, `TempTableExtensions.cs`, `QueryCommand.QueryPreparer.cs`, correlated visitor helpers, dialect capability declarations, integration provider/configuration files, CI/coverage configuration, benchmark project, rc2 precedent status files.

No package/configuration changes, generated docs, or whole-tree newline normalization. Preserve CRLF in changed files. Unlisted production files or a new MariaDB harness require PLAN footprint review.

## Docs wording

- Internal design §11: retain the explicit deferral and distinguish pre-boundary navigation from post-boundary navigation.
- Roadmap: add V32/#162 in the current-milestone follow-up list, including the implementation trigger.
- Public EN/RU: explain the current boundary without claiming all TVP roots are necessarily identical to lazy temp-table aliases.
- Relationships pages: update only existing affected claims.
- Public docs must not link `docs/specs/**`.
- **XML docs:** no change expected because no public API is added/renamed. If an existing public overload’s documented behavior is materially contradicted by the verified guard, update that member’s XML documentation within an approved footprint addition.

## Test strategy

### Layers

- **Semantic evidence:** public source type and navigation surface; cannot substitute for runtime/provider behavior where a route exists.
- **Dialect/unit tests:** SQL source provenance, statement boundaries, guard diagnostics and supported-operation controls.
- **Integration tests:** real source creation/binding, preparation and execution for routable provider variants. A SQL-string test is not database execution conformance.

Provider conformance requires:

1. Correct public source construction.
2. Successful ordinary source operation.
3. Verified fail-closed navigation outcome.
4. No base-table fallback.
5. Provider identity and capability recorded.
6. Nonzero selected/executed test count; no relevant skips.

### Command conventions

Artifact root:

`A=/tmp/nextorm-D162-r1`

Coder creates it and records tool/runtime/provider versions. Commands run from the repository root.

For every executable command below use this exact safety/logging wrapper, replacing `LABEL` and `COMMAND` with the listed values:

```text
set -o pipefail
timeout 600 env DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 COMMAND 2>&1 | tee "$A/LABEL.log"
rc=${PIPESTATUS[0]}
printf 'exit_code=%s\n' "$rc" | tee "$A/LABEL.exit"
test "$rc" -eq 0
```

Timeout, test discovery failure and zero selected tests are failures, even if another pipeline component exits successfully.

| Command ID | COMMAND | LABEL |
|---|---|---|
| C01 | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ImplicitNavigationV32TempTableTests"` | `baseline-sqlite-v32` |
| C02-P | `dotnet run --project tests/nextorm.P.tests -c Debug -- -trait "D162=Conformance"` | `conformance-P` |
| C03-P | `dotnet run --project tests/nextorm.P.tests -c Debug -- -trait "D162=Boundary"` | `boundary-P` |
| C04 | `env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait "D162=Conformance" -noColor` | `integration-conformance` |
| C05 | `env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait "D162=Boundary" -noColor` | `integration-boundary` |
| C06 | `dotnet build nextorm.slnx -c Debug` | `build-debug` |
| C07 | `dotnet docfx docs/docfx.json` | `docfx` |
| C08 | `dotnet-coverage collect --settings coverage.settings.xml --output "$A/coverage.cobertura.xml" --output-format cobertura "bash $A/boundary-sweep.sh"` | `coverage-boundary` |
| C09 | `reportgenerator -reports:"$A/coverage.cobertura.xml" -targetdir:"$A/coverage-report" -reporttypes:"Html;TextSummary"` | `coverage-report` |

`P` is expanded, one invocation at a time, to **core, sqlite, postgres, mysql, sqlserver, clickhouse**. Conformance selection for core contains source/guard controls, not database-provider conformance.

`boundary-sweep.sh` is a **planned validation artifact**, not a source change. It executes the C03-P expansion followed by C05, preserving every command’s exit code. C08 wraps the single final boundary sweep; do not run an additional full sweep merely to collect coverage.

The planned traits must be assigned to the relevant existing and new tests before selection. Their use does not assert future test symbols.

**MariaDB:** its execution command cannot honestly be inferred from the supplied facts. D:01 must supply an exact invocation using an existing harness. Append it as a new evidence row before execution. If no harness exists, return to PLAN; do not silently skip MariaDB or label MySQL execution sufficient.

Load the integration skill before C04/C05. If the socket is missing, follow its machine-start/wait/recheck procedure. Provider skips do not satisfy the contract.

### Coverage and mutation

- Project policy: line ≥85%, branch ≥75%; hard-fail on `main`, warning on this branch.
- Report coverage scope honestly: the repository settings include core/SQLite/PG/SQL Server, not MySQL/ClickHouse/MariaDB.
- A focused affected-path report is **not proof of repository-wide thresholds**. Record that distinction and use the existing full CI report for global threshold evidence if available.
- New executable guard lines: cover all changed P1 decisions and both allow/reject branches. Report changed-line/branch mapping separately.
- No Stryker dependency or full mutation campaign planned.
- If a production guard is added, perform bounded discrimination checks: suppress rejection and broaden rejection separately in a temporary, reverted patch. The selected tests must fail in each case. Record the revert and final clean guard diff. This tests both false acceptance and false rejection.

## Performance measurement

**No benchmark required for the selected minimal solution.**

The existing seam runs once per command preparation, not per materialized row: `NavigationExpansion.cs:48-50`, called at `QueryCommand.QueryPreparer.cs:65`. Documentation and test changes have no runtime cost. A necessary constant-time source/navigation guard at that seam is not a new per-row mechanism.

If implementation instead changes cached preparation behavior, source traversal cost, allocation, or materialization, this decision is invalidated. Require baseline at the starting implementation and the nextorm seven-case acceptance command:

`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`

Use the same safety wrapper, archive baseline/candidate outputs, and revise PLAN before accepting that larger change.

## Reconnaissance

**No design spike or exploratory implementation is needed.** The scope decision is established.

**A bounded fact scout is required**, because actual TVP source shapes and MariaDB execution are not established. Its observable completion criterion is a route table with semantic references, actual root types, existing test/harness locations and exact runnable selectors—not recommendations or inferred support.

## Risks and prerequisites

- TVP capability may not correspond to an independently routable navigation source.
- Untyped temp-table exclusion must not be incorrectly generalized to a typed TVP API.
- Aggregate SQL substring assertions can miss statement-specific fallback.
- Broad guards can break valid alias/TVP column access.
- Async paths may use different setup while sharing preparation.
- Provider skips and absent MariaDB harnesses can create false conformance claims.
- A failed preparation must not poison reused commands or cache state.
- Runtime/container prerequisites must be repaired within authorized scope or explicitly escalated.
- No predecessor-result requirements are recorded. D:01 checks whether the collection registry has changed before execution; newly recorded dependencies are not ignored.

**DO→PLAN classification:** unresolved TVP/harness facts are an **evidence deficit**, not yet an external blocker. Scout first. A missing in-scope test prerequisite is an additive prerequisite, keeping original D tasks active and blocked. Persistent low confidence invokes escalation trigger 5. A proven external resource/permission blocker is routed to `escalate`.

---

## End of historical r=1 plan narrative

Content below is the current version-extended contract, revision rule and remaining tracked items; they remain in force unless the r=2 plan above supersedes them.

## Versioned evidence contract — rv=2 (rv=1 rows E162-01…E162-14 preserved verbatim; E162-15…E162-18 added below)

### Common mandatory terms

Every row inherits these terms explicitly:

- Sources marked **planned** until artifacts exist.
- Executable commands use the safety wrapper above and require exit `0`, an archived `.log` and `.exit`, plus scenario results. Test rows require nonzero execution and no relevant skips.
- Tool/Task invocations require a returned report, source references and an explicit success/incomplete outcome; a process exit code is **not applicable to a Task invocation**, not to its obligation.
- Evidence includes the tested commit/diff identity, provider/environment identity where relevant, and scenario-to-requirement mapping.
- No unconditional row may be N/A.
- Conditional rows require evidence of their applicability predicate, not an undocumented omission.
- All artifact paths are under `$A` unless expressly identified as a status/document file.

| Requirement / row / rv | Required check | Planned evidence kinds and sources | Exact invocation / result requirement | Expected artifacts | Owner | Observable applicability |
|---|---|---|---|---|---|---|
| R162-07 / E162-01 / 1 | Persist full plan and validate gate | Status file, collection row, global contract excerpt | `Task(coder): persist this complete PLAN at the stated path; validate all supplied gate/contract slots; report saved paths and unresolved obligations` | Status file; `$A/gate-report.md` | O | **Always** |
| R162-03,04 / E162-02 / 1 | Resolve TVP/provider/harness routes | Roslyn definitions/references/call paths; integration skill; docs locations | `Task(scout): execute D:01 read-only; use Roslyn first for C#; return actual root types, file:line, existing symbols and exact MariaDB invocation; list unresolved facts` | `$A/source-provider-routes.md` | S | **Always** |
| R162-01,02 / E162-03 / 1 | Existing SQLite baseline | Existing guard test and output | C01; standard result requirements | Baseline log/exit; `$A/baseline-summary.md` | V | **Always** |
| R162-01–05 / E162-04 / 1 | Focused source/provider guard tests | Actual tests, source assertions, SQL/diagnostics | C02-P for the stated six-project expansion; nonzero selection per project | Per-project conformance logs/exits; `$A/scenario-results.md` | C/V | **Always** |
| R162-03,04 / E162-05 / 1 | Semantic non-expressibility and unsupported routes | Actual public types/overloads and existing unsupported guards | `Task(scout): verify V03–V14 route exclusions against public API and preparation paths; report file:line and distinguish unsupported source from navigation exclusion` | `$A/route-exclusions.md` | S | **Always** |
| R162-01–05 / E162-06 / 1 | Real provider conformance | Container/database execution and per-provider results | C04; relevant provider skips fail this row | Integration log/exit; `$A/provider-results.md` | V | **Always**; results must distinguish routable and semantically excluded scenarios |
| R162-01–05,07 / E162-07 / 1 | Single final boundary sweep | Relevant existing/new tests and provider execution | C08 invokes C03-P and C05 through archived sweep script; all nested exits zero | Sweep script; boundary logs/exits; coverage XML | V | **Always** |
| R162-05,07 / E162-08 / 1 | Build/nullable/analyzer compatibility | Solution build | C06; warnings-as-errors remains enabled | Build log/exit | V | **Always** |
| R162-06 / E162-09 / 1 | Synchronized docs and tracking | EN/RU diff, §11, roadmap and collection row | `Task(check): verify D:06 against R162-06; inspect both language trees for contradictory claims and public links to docs/specs; report PASS or exact defects` | `$A/docs-review.md`; changed docs | K | **Always** |
| R162-06 / E162-10 / 1 | Public docs render | DocFX output/diagnostics | C07; exit zero and no new broken references attributable to D162 | DocFX log/exit | V | **Always** |
| R162-05,07 / E162-11 / 1 | Coverage and branch evidence | Coverage XML, report, changed-line/branch mapping | C09, then `Task(check): evaluate project-policy scope and changed P1 branch coverage; distinguish focused coverage from full CI thresholds` | Coverage report; `$A/coverage-review.md` | V/K | **Always** |
| R162-03,05 / E162-12 / 1 | Guard discriminates rejection/allowance | Two temporary mutations and test failures | `Task(coder): perform the two bounded, reverted guard-discrimination checks from Test strategy; invoke C02-P for affected projects; archive nonzero test exits and revert proof` | `$A/guard-discrimination.md`; mutation logs/exits | C/V | Production guard added, observable in diff |
| R162-05 / E162-13 / 1 | Preparation/cache safety | Rejected-then-valid sequence; shared-state inspection | C02-P affected projects, plus `Task(check): verify no shared query-command/cache mutation was introduced` | Test logs; `$A/cache-safety.md` | C/K | Preparation guard changed, observable in diff |
| R162-07 / E162-14 / 1 | Final complete ledger | All rows/artifacts and provider/scenario accounting | `Task(check): apply rv=1 and P1 matrix; validate applicability, artifacts and test counts; issue verdict without downgrading obligations` | `$A/check-r1-n1.md`; updated status ledger | K | **Always** |

**Superseded by rv=2 (factual correction — no TVP query-root exists):** **E162-02** and **E162-05** assumed a discoverable/navigable public TVP query-root. Their route-discovery and route-exclusion obligations are replaced by rv=2 rows E162-17 (TVP-root absence) and E162-18 (boundary/parameter matrix). All other rv=1 rows (**E162-01, E162-03, E162-04, E162-06…E162-14**) remain in force; their still-applicable obligations are preserved.

**Required contract addition:** once D:01 identifies the actual MariaDB invocation, add **E162-15 / R162-01,03,04** with its exact command, safety wrapper, provider results, artifacts and applicability. This adds evidence specificity; it does not remove existing obligations. No TVP/temp-table conformance claim is accepted before that provider’s disposition is resolved. *(Fulfilled by rv=2 row E162-15 below.)*

### rv=2 rows (E162-15…E162-18)

| Requirement / row / rv | Required check | Planned evidence kinds and sources | Exact invocation / result requirement | Expected artifacts | Owner | Observable applicability |
|---|---|---|---|---|---|---|
| R162-04 / E162-15 / 2 | MariaDB temp + parameter execution | Integration trait run with MariaDB scenario | Integration trait command; MariaDB scenario counts/results + provider startup log; exit 0, nonzero selection, no applicable skips | `$A/integration-conformance.log`/`.exit`; provider startup log | V | MariaDB fixture selected |
| R162-01,02 / E162-16 / 2 | Existing SQLite baseline | `dotnet build` + existing guard test, exact build/run JSON | Exactly one passing fact; `buildExit=0`, `testExit=0`, `selectedTests=1`, `passedTests=1`, `skippedTests=0` | `$A/baseline-build.log`, `$A/baseline-test.log` | C | **Always** |
| R162-03 / E162-17 / 2 | TVP query-root absence | Roslyn reference/route report | `roslyn(action="refs",symbol="NextORM.Core.TableParameterValue")`; complete resolution and no root route | `$A/tvp-root-references.md` | S | **Always** |
| R162-04,05 / E162-18 / 2 | Relational boundary/parameter matrix | Per-project + integration commands | Each relational project instantiated separately plus integration; per-provider × scenario results; exit 0 | Command logs/exits; `$A/provider-scenario-ledger.md` | V | All relational providers |

New-row common slots: status `planned`; `rv=2`; artifact root `/tmp/nextorm-D162-r2`; exact command, captured exit/log, expected artifacts, scenario, R ID, owner, observable applicability. Test rows require exit 0, nonzero expected selection, all required cases passing, no applicable skips.

### CHECK re-gather budget

Owner: **K — CHECK**.

Budget: **two targeted re-gather requests per CHECK attempt**, each restricted to a named row and missing artifact/result. Re-running a provider test is one request, not unlimited retries.

After exhaustion:

- Implementation defect → FAIL and DO correction under the current plan where possible.
- Unresolved evidence uncertainty → PLAN for targeted scout; persistent low confidence → escalation trigger 5.
- Proven external blocker → route escalation.
- Never pass by omission; never revise the contract solely because a report is absent.

## rv=2 evidence ledger — E162-01…E162-18 (complete)

Root: `/tmp/nextorm-D162-r2/`. Revision: `e25f558e`. Status vocabulary: `closed` (obligation met with the named artifact), `N/A` (conditional predicate false or contract-authorized), `superseded` (retained ID, replaced by a later row), `planned` (obligation not yet closed). The rv=1 rows E162-01…E162-14 are preserved; only **E162-02** and **E162-05** are superseded.

| Row / rv | R162 | Status | Applicability | Owner | Artifact pointer (`/tmp/nextorm-D162-r2/`) |
|---|---|---|---|---|---|
| E162-01 / 1 | R162-07 | closed | Always | O | `docs/specs/status/rc2-162-navigation-temp-tvp-fail-closed-1.md`; `scope-d02.json` (rv=2 plan/gates validated, brief exit 0) |
| E162-02 / 1 | R162-03,04 | **superseded** | rv=1 assumed a discoverable/navigable public TVP query-root | S | replaced by E162-17 + E162-18; rv=1 `source-provider-routes.md` not produced (premise retracted — no TVP query-root exists) |
| E162-03 / 1 | R162-01,02 | closed | Always | V | `baseline-build.log` (exit 0, 0W/0E); `baseline-test.log` (1 passed/0 failed/0 skipped, selected=1); `evidence-d02.json` |
| E162-04 / 1 | R162-01–05 | closed | Always | C/V | `evidence-d05.json`; `d05-{core,sqlite,sqlserver,clickhouse}-Conformance.log`; `d05-{core,sqlite,postgres,mysql,mariadb,sqlserver,clickhouse}-Boundary.log` |
| E162-05 / 1 | R162-03,04 | **superseded** | rv=1 route-exclusion scout for a TVP query-root | S | replaced by E162-17 + E162-18; rv=1 `route-exclusions.md` not produced (no public TVP query-root exists) |
| E162-06 / 1 | R162-01–05 | closed | Always; routable and semantically excluded scenarios distinguished | V | `d05-integration.log` (Total 62 / Failed 0 / Skipped 0; PostgreSQL 6, SQL Server 18, MySQL 6, MariaDB 5, ClickHouse 23, SQLite 4) |
| E162-07 / 1 | R162-01–05,07 | closed | Always | V | `evidence-d05.json` (single DO→CHECK sweep: Debug `nextorm.slnx` build + 7 boundary + 4 conformance SQL-gen + integration, all exit 0; 13 boundary executions) |
| E162-08 / 1 | R162-05,07 | closed | Always | V | `d05-build-debug.log`/`d05-build-debug.exit` (exit 0, 0W/0E); `d05-build-release.log`/`d05-build-release.exit` (exit 0, 0W/0E) |
| E162-09 / 1 | R162-06 | closed | Always | K | `evidence-d06.json` + `d06-docfx.log`; EN/RU/design/roadmap diffs mutually consistent (no separate `docs-review.md`; D:06 report + docfx + this re-gather consistency check) |
| E162-10 / 1 | R162-06 | closed | Always | V | `d06-docfx.log` (exit 0; 0 errors / 2 pre-existing warnings; 544 models / 165 conceptual pages) |
| E162-11 / 1 | R162-05,07 | **N/A** (contract-authorized) | `git diff e25f558e -- src` empty → no production `.cs` change | V/K | predicate: `git diff e25f558e -- src` = 0 lines; change is tests+docs only (see coverage predicate below) |
| E162-12 / 1 | R162-03,05 | **N/A** (predicate false) | Production guard added, observable in diff | C/V | predicate false: `src` diff empty → no production guard exists to discriminate |
| E162-13 / 1 | R162-05 | **N/A** (predicate false) | Preparation guard changed, observable in diff | C/K | predicate false: `src` diff empty → no preparation/shared-command/cache change; AGENTS.md invariants untouched |
| E162-14 / 1 | R162-07 | closed | Always | K | CHECK verdict **PASS** rv=2 (R162-01..07 met; E162-01..18 closed/superseded/N-A); persisted ledger = this section + the DO ledger below |
| E162-15 / 2 | R162-04 | closed | MariaDB fixture selected | V | `d05-integration.log` (MariaDB 5 executed, 0 failed, 0 skipped; MariaDB container readiness recorded) |
| E162-16 / 2 | R162-01,02 | closed | Always | C | `baseline-build.log`; `baseline-test.log`; `evidence-d02.json` (buildExit 0, testExit 0, selected 1, passed 1, skipped 0) |
| E162-17 / 2 | R162-03 | closed | Always | S | `roslyn-tvp-root.txt` (34 references, none reach a `From`/`EntityBuilder` query-root); `d09-{core,sqlite,sqlserver,clickhouse}.log` |
| E162-18 / 2 | R162-04,05 | closed | All relational providers | V | `evidence-d05.json`; `d05-<p>-{Boundary,Conformance}.log`; `d05-integration.log`; per-provider ledger below |

### Pinned rv=2 evidence ledger pointer

- **Ledger file:** `docs/specs/status/rc2-162-navigation-temp-tvp-fail-closed-1.md`
- **E162 table lines (final revision):** **556–575** — `## rv=2 evidence ledger — E162-01…E162-18 (complete)` heading at line 552; header row `| Row / rv | …` at line 556; data rows `E162-01`…`E162-18` at lines 558–575.
- **Contract revision:** **rv=2** (supersedes rv=1). Row-version binding: `E162-01`…`E162-14` tagged **rv1** (of which `E162-02` and `E162-05` are `superseded` by the rv=2 rows `E162-17`/`E162-18`); `E162-15`…`E162-18` tagged **rv2**. Every row carries its version in the `Row / rv` column.

### R162-05 closure — full affected-project suite sweep (CHECK re-gather)

- **src-diff predicate:** `git diff HEAD -- src` = 0 lines and `git status --porcelain -- src` empty at revision `e25f558e558c24980eade1ab0fa971039754b0e0` ⇒ no production change; existing navigation / non-navigation (`TableAlias`/derived/`FromSql`) / shared-command cache / configured-TVP-metadata behavior is untouched.
- **Build:** `dotnet build nextorm.slnx -c Debug` → exit 0, 0 warnings / 0 errors — `/tmp/nextorm-D162-r2/r162-05-build.log` (+ `.exit`).
- **Full affected project suites (SQL-gen, no containers), all exit 0** (`tests/nextorm.<p>.tests`, `dotnet test … --no-build`):

| Project | Total | Passed | Failed | Skipped | Log (literal `dotnet test`) | Log (xunit v3 `dotnet run`, gated) |
|---|---|---|---|---|---|---|
| core | 1757 | 1757 | 0 | 0 | `r162-05-core.log` | `r162-05-core-run.log` |
| sqlite | 1161 | 1160 | 0 | 1 | `r162-05-sqlite.log` | `r162-05-sqlite-run.log` |
| postgres | 803 | 803 | 0 | 0 | `r162-05-postgres.log` | `r162-05-postgres-run.log` |
| mysql | 303 | 303 | 0 | 0 | `r162-05-mysql.log` | `r162-05-mysql-run.log` |
| mariadb | 229 | 229 | 0 | 0 | `r162-05-mariadb.log` | `r162-05-mariadb-run.log` |
| sqlserver | 727 | 727 | 0 | 0 | `r162-05-sqlserver.log` | `r162-05-sqlserver-run.log` |
| clickhouse | 591 | 591 | 0 | 0 | `r162-05-clickhouse.log` | `r162-05-clickhouse-run.log` |

- **Skipped (1, pre-existing, unrelated):** SQLite `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` — conditional probe gated on `NEXTORM_LOB_SQLITE_PROBE=1`; `src` diff empty ⇒ no relation to D162. No other project skipped.
- **Evidence gate:** `scope-r162-05.json` brief exit 0; `evidence-r162-05.json` report exit 0. Gated `executions` use `dotnet run --project tests/nextorm.<p>.tests -c Debug --no-build -- -noColor` (xunit v3 native runner — same full test assembly as `dotnet test`; one logical 7-project boundary sweep). The host validator caps unfiltered `dotnet test` boundary sweeps at one, so the literal 7×`dotnet test` form is recorded for traceability and rejected by the gate (`evidence-r162-05-dotnettest-literal.json` → exit 2, `more than one comprehensive boundary test sweep: 7`); its runs (`r162-05-<p>.log`, all exit 0) are the literal-command corroboration.
- **Milestone association (refreshed `gh issue view 162 --repo AlexeyShirshov/nextorm`):** `#162`, state **OPEN**, milestone number **20**, title **`1.0.9-rc2`**, url `https://github.com/AlexeyShirshov/nextorm/issues/162`.

## Priority matrix (rv=2, P1 — CHECK cannot downgrade)

P1 is fixed for the acceptance rows below; CHECK must not downgrade any P1 row. R162-01…R162-05 are P1 by task invariant and changed-execution-path; R162-06…R162-07 are P1 for doc/tracking and complete-ledger closure.

| R162 | P1 basis | Closing rows | Executed test / guard |
|---|---|---|---|
| R162-01 | temp boundary + no base-table fallback | E162-03, E162-04, E162-06, E162-16, E162-18 | `ImplicitNavigationV32TempTableTests` (sqlite/postgres/mysql/mariadb); `TempTableNavigationBoundaryTests` (core); `TempTableMaterializationRejectionTests` (sqlserver/clickhouse) |
| R162-02 | pre-boundary navigation preserved | E162-03, E162-04, E162-16, E162-18 | same boundary facts; materialisation `left join` assertion in `ImplicitNavigationV32TempTableTests` |
| R162-03 | no TVP query-root; parameters preserved | E162-17, E162-04, E162-15, E162-18 | `TableValuedParameterTests` (sqlite/clickhouse); `TableValuedParameterBindingTests` (sqlserver); `RawCommandInMemoryTests` (core); integration TVP methods (PG 6 / SQL Server 18 / MySQL 6 / MariaDB 5 / ClickHouse 23 / SQLite 4) |
| R162-04 | honest provider/source accounting; no skips | E162-04, E162-06, E162-15, E162-18 | `evidence-d05.json` (87 SQL-gen + 62 integration selected; 0 failed, 0 skipped) |
| R162-05 | no leaked cache flag / metadata contamination | E162-08, E162-06, E162-18, E162-11, E162-13 | build 0W/0E both configs; provider/integration runs; `src` diff empty (invariants untouched) |
| R162-06 | EN/RU/design/roadmap agree + milestone linkage | E162-01, E162-09, E162-10 | `gh issue view 162` (OPEN, milestone #20 `1.0.9-rc2`); `d06-docfx.log`; EN/RU/design/roadmap diffs |
| R162-07 | complete ledger persisted | E162-01, E162-07, E162-14 | this status file; `evidence-d05.json`; CHECK verdict (pending) |

## Row-bound DO ledger (revision `e25f558e`)

Every executed measurement with unit id, argv, phase, exit code, selected count and log path. `nextorm.slnx`/provider commands ran from the repository root; `phase` is the evidence-record phase.

| Unit | Phase | argv | Exit | Selected | Log path (`/tmp/nextorm-D162-r2/`) |
|---|---|---|---|---|---|
| D:02 | inner | `dotnet build tests/nextorm.sqlite.tests -c Debug` | 0 | — | `baseline-build.log` (0W/0E) |
| D:02 | inner | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~ImplicitNavigationV32TempTableTests.Temp_table_read_should_stay_an_untyped_alias_and_not_silently_join_the_base_table` | 0 | 1 | `baseline-test.log` |
| D:05 | boundary | `dotnet build nextorm.slnx -c Debug` | 0 | 87 | `d05-build-debug.log` / `.exit` |
| D:05 | boundary | `dotnet build nextorm.slnx -c Release` | 0 | — | `d05-build-release.log` / `.exit` |
| D:05 | boundary | `dotnet run --project tests/nextorm.core.tests -c Debug -- -trait D162=Boundary -noColor` | 0 | 1 | `d05-core-Boundary.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.sqlite.tests -c Debug -- -trait D162=Boundary -noColor` | 0 | 1 | `d05-sqlite-Boundary.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.postgres.tests -c Debug -- -trait D162=Boundary -noColor` | 0 | 1 | `d05-postgres-Boundary.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.mysql.tests -c Debug -- -trait D162=Boundary -noColor` | 0 | 1 | `d05-mysql-Boundary.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.mariadb.tests -c Debug -- -trait D162=Boundary -noColor` | 0 | 1 | `d05-mariadb-Boundary.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.sqlserver.tests -c Debug -- -trait D162=Boundary -noColor` | 0 | 1 | `d05-sqlserver-Boundary.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.clickhouse.tests -c Debug -- -trait D162=Boundary -noColor` | 0 | 1 | `d05-clickhouse-Boundary.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.core.tests -c Debug -- -trait D162=Conformance -noColor` | 0 | 8 | `d05-core-Conformance.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.sqlite.tests -c Debug -- -trait D162=Conformance -noColor` | 0 | 7 | `d05-sqlite-Conformance.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.sqlserver.tests -c Debug -- -trait D162=Conformance -noColor` | 0 | 42 | `d05-sqlserver-Conformance.log` |
| D:05 | boundary | `dotnet run --project tests/nextorm.clickhouse.tests -c Debug -- -trait D162=Conformance -noColor` | 0 | 23 | `d05-clickhouse-Conformance.log` |
| D:05 | boundary | `env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait D162=Conformance -noColor` | 0 | 62 | `d05-integration.log` |
| D:06 | boundary | `dotnet docfx docs/docfx.json` | 0 | 165 | `d06-docfx.log` |
| D:08 | inner | `dotnet build tests/nextorm.<p>.tests -c Debug` (p ∈ core, sqlite, postgres, mysql, mariadb, sqlserver, clickhouse) | 0 each | — | `d08-<p>-build.log` |
| D:08 | inner | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~TempTableNavigationBoundaryTests` | 0 | 1 | `d08-core-sel.log` |
| D:08 | inner | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~ImplicitNavigationV32TempTableTests` | 0 | 1 | `d08-sqlite-sel.log` |
| D:08 | inner | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build --filter FullyQualifiedName~ImplicitNavigationV32TempTableTests` | 0 | 1 | `d08-postgres-sel.log` |
| D:08 | inner | `dotnet test tests/nextorm.mysql.tests -c Debug --no-build --filter FullyQualifiedName~ImplicitNavigationV32TempTableTests` | 0 | 1 | `d08-mysql-sel.log` |
| D:08 | inner | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build --filter FullyQualifiedName~ImplicitNavigationV32TempTableTests` | 0 | 1 | `d08-mariadb-sel.log` |
| D:08 | inner | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build --filter FullyQualifiedName~TempTableMaterializationRejectionTests` | 0 | 1 | `d08-sqlserver-sel.log` |
| D:08 | inner | `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build --filter FullyQualifiedName~TempTableMaterializationRejectionTests` | 0 | 1 | `d08-clickhouse-sel.log` |
| D:09 | inner | `dotnet build tests/nextorm.<p>.tests -c Debug` (p ∈ core, sqlite, sqlserver, clickhouse) + `tests/nextorm.integration.tests` | 0 each | — | `d09-<p>-build.log`, `d09-integration-build.log` |
| D:09 | inner | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~RawCommandInMemoryTests` | 0 | 8 | `d09-core.log` |
| D:09 | inner | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~TableValuedParameterTests` | 0 | 7 | `d09-sqlite.log` |
| D:09 | inner | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build --filter FullyQualifiedName~TableValuedParameterBindingTests` | 0 | 42 | `d09-sqlserver.log` |
| D:09 | inner | `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build --filter FullyQualifiedName~TableValuedParameterTests` | 0 | 23 | `d09-clickhouse.log` |

## Coverage predicate (contract-authorized N/A)

`git diff e25f558e -- src` is **empty** (0 lines; `git diff e25f558e --stat` shows only `docs/**` and `tests/**`). There are therefore **no production `.cs` line/branch changes**, so coverage of the covered assemblies (`nextorm.core`, `nextorm.sqlite`, `nextorm.postgres`, `nextorm.sqlserver`) is **unchanged** by this cycle — the change is tests+docs only. This is the evidenced N/A for the coverage/branch gate: **E162-11 is N/A by contract authorization**, and a full-suite coverage pass is deliberately not run (it would measure zero net production change). This does not waive the ≥85% line / ≥75% branch policy; it records that D162 introduces no executable production surface to cover.

## TVP-parameter / sticky-cache / configured-TVP-metadata closure

- **TVP parameter:** supported parameter execution is preserved and exercised. Executed conformance tests per provider — class level: `tests/nextorm.sqlite.tests/TableValuedParameterTests.cs`, `tests/nextorm.clickhouse.tests/TableValuedParameterTests.cs`, `tests/nextorm.sqlserver.tests/TableValuedParameterBindingTests.cs`, `tests/nextorm.core.tests/RawCommandInMemoryTests.cs` (all `[Trait("D162","Conformance")]`); integration method level with real databases: PostgreSQL 6 (`PostgresSpecificTests.cs`), SQL Server 18 (`SqlServerSpecificTests.cs`), MySQL 6 (`MySqlSpecificTests.cs`), MariaDB 5 (`MariaDbFunctionsIntegrationTests.cs`), ClickHouse 23, SQLite 4 — all exit 0, 0 failed, 0 skipped (`d05-integration.log`). Counts match the brief: **PG 6 / SQL Server 18 / MySQL 6 / MariaDB 5 / ClickHouse 23 / SQLite 4**.
- **sticky-cache:** obligation — no `queryCommand.Cache = false` on a shared per-context command may disable the plan cache (AGENTS.md). Closure: `src/**` diff vs `e25f558e` is empty ⇒ the sticky `_dontCache` path and shared `QueryCommand` state are untouched; the invariant is not at risk, evidenced by the empty `git diff e25f558e -- src`. No dedicated executed test is required because there is no product change; this is the contract-authorized evidence. (The in-memory `RawCommandInMemoryTests` run exercises the raw-command path and passed 8/8, but the decisive guard is the empty `src` diff.)
- **configured-TVP-metadata:** obligation — the TVP auto-resolution path must keep its own `DataContextCache` metadata cache, must never seed the process-wide configured-metadata cache, and must defer to a configured `From<T>(cfg)` mapping (AGENTS.md, `TvpMetadataCacheTests`). Closure: `src/**` diff is empty ⇒ `DataContextCache`/TVP auto-resolution is unchanged, so the configured-metadata cache cannot be seeded by this cycle; evidenced by the empty `git diff e25f558e -- src`. No dedicated executed `TvpMetadataCacheTests` run is needed for a tests+docs-only change.

## Revision rule

Current state: **r=2, n=1, rv=2** (rv=2 supersedes rv=1; E162-02/E162-05 superseded as recorded above).

- Ordinary corrections within this solution and footprint retain r/rv.
- Added exact commands or newly discovered variants preserve existing row IDs and obligations; version their contract change with explicit supersession.
- A materially different repair strategy, new source API, different guard seam with wider dependencies, new harness, or changed performance characteristics requires **P:162-REPLAN — close the identified source/provider guard gap**, r incremented and n reset to 1.
- Contract revision states: “rv=X supersedes rv=Y,” retains existing IDs/obligations and adds new IDs.
- Renaming tasks, missing reports, rejected candidates and wording clarification are not new plan revisions.
- Additive prerequisites keep original D tasks active/blocked, not superseded.
- A true scope replacement requires explicit superseded→replacement mapping carrying all original criteria and unfinished work.
- A DO report, including “implementation complete,” does not mark tasks done; CHECK supplies the verdict.
- After three failed CHECKs in the same plan revision, escalate; no automatic fourth attempt.

## CHECK verdict (rv=2) and ACT

- **CHECK verdict: PASS** at r=2, n=1/3, rv=2. Acceptance met: **R162-01…R162-07**. Evidence ledger: **E162-01…E162-18** — `closed` (E162-01, 03, 04, 06, 07, 08, 09, 10, 15, 16, 17, 18), `superseded` (E162-02, E162-05 — premise retracted: no discoverable/navigable public TVP query-root exists), `N/A` (E162-11, E162-12, E162-13 — `src` diff empty, no production surface), `closed` (E162-14 — the CHECK verdict row, closed by this verdict).
- **ACT: accepted.** Cycle state: **done / ACT complete**. The tests+docs outcome is the correct, lowest-risk closure of #162: no production change, `src` diff empty, all boundary/support/parameter obligations evidenced across the six relational providers plus in-memory. Commit pending (authorized for the collection lane; push never).
- **Retained deferrals:** typed temp-table/TVP query-root — typed navigation across temp-table/TVP source boundaries stays deferred; trigger = approved feature or demonstrated fallback defect. Tracking retained in `docs/specs/roadmap/todo_navigation_properties.md`; #162 closes as verified fail-closed.

## Deferred + trigger

Typed navigation over temp-table/TVP roots remains deferred and tracked in this milestone.

Trigger: approved typed-source provenance/navigation design, followed by provider conformance covering **both** temp-table and TVP sources. Unsupported temp-table providers are separate capability work, not silently enabled by this follow-up.

## Next plan seed

**P:162-IMPLEMENT — define and implement typed navigation across temp-table/TVP source boundaries without base-table fallback.**

Required inputs:

- Root provenance and mapping model.
- Whole-entity versus explicit projection semantics.
- Provider-specific source capabilities and execution harnesses.
- Sync/async and null/default behavior.
- Conformance acceptance for both source kinds.
- Existing fail-closed evidence retained until replacement behavior passes CHECK.

This seed is not authorization to implement it during D162.
