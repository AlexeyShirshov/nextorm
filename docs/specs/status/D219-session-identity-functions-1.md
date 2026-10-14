---
task: T219 (#219)
title: Session / server identity functions across providers
issue: https://github.com/AlexeyShirshov/nextorm/issues/219
milestone: 1.0.9
collection: 1.0.9-todo
collection_status: docs/specs/status/collection-1.0.9-todo.md
group: G1 (single group; no worktree/branch/merge)
cycle: N=1
plan_revision: r=1
iteration: n=1
plan_state: ready
evidence_contract_rv: 1
manifest: artifacts/pdca/D219/rv1/manifest.json
selected_variant: pdca-dotnet
base_commit: 706a83d1
baseline_ref: 706a83d1
---

# T219 PLAN r=1 — session/server identity functions

Milestone 1.0.9, cycle N=1.

## 1. Goal and acceptance criteria
Add typed server/connection identity functions preserving the distinction between client host, server host, database user and login.
- R219-01 — form/API: new functions follow the form of SQL siblings on the existing sentinel objects. Negative: new top-level objects, ambiguous analogs and unrecorded public additions are forbidden.
- R219-02 — translation/capabilities: every supported call emits the SQL from the table below; SQL Server additionally gated through `ISqlServerFunctions`. Negative: an unsupported function/provider throws `NotSupportedException` at translation, no SQL fallback.
- R219-03 — every provider: SQL-generation/capability checks cover all 15 functions × 6 providers. Negative: a SQLite-only run or a skipped provider is not evidence.
- R219-04 — types/null: results match the stated CLR types; PostgreSQL network functions allow SQL NULL. Negative: NULL becomes default/empty string, or a numeric ID loses range.
- R219-05 — runtime: positive functions execute on real supporting providers; in-memory fail-closed. Negative: successful generation without materialization/runtime evidence is insufficient.
- R219-06 — docs: EN/RU reconciled, exclusions/roadmap corrected, DocFX succeeds. Negative: stale "Not exposed", wrong paths or public links to specs remain.
- R219-07 — quality: build Debug = 0 warnings/0 errors; coverage ≥85% line and ≥75% branch for the project-included set. Negative: a warning-only threshold on a branch does not replace acceptance.
- R219-08 — isolation: sibling changes preserved, T53 candidates recorded, status/manifest valid. Negative: a mixed commit, lost baseline or unregistered public member.

## 2. Minimal solution (decision)
New zero-argument scalar functions on one existing session-info mechanism `Supports/Render`. Chosen: **hybrid surface** — shared session functions stay shared; vendor semantics explicit. No new public abstraction, no query/cache/executor moves.
Exactly-added public members (T53 candidates):
- `SqlFunctions.SqlServer`: `server_name(): string?`, `host_id(): string?`, `host_name(): string?`, `app_name(): string?`, `suser_name(): string?`, `original_login(): string?`
- `SqlFunctions.Sql`: `pg_backend_pid(): int`, `inet_server_addr(): string?`, `inet_server_port(): int?`, `inet_client_addr(): string?`, `inet_client_port(): int?`, `connection_id(): ulong`, `user(): string`
- `SqlFunctions.ClickHouse`: `host_name(): string`, `tcp_port(): int`
- `ISqlServerFunctions`: `bool SupportsSessionIdentity` with default implementation `false`; SQL Server enables via explicit interface implementation.
Total 15 functions + 1 interface property; no other public additions. Existing sentinel properties unchanged. T53 stays held; record these as held-freeze candidates (basis `collection-1.0.9-todo.md:66`).

## 3. Per-provider contract
`NS` = `NotSupportedException` at translation.
| Receiver/function, CLR | SQL Server | PostgreSQL | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|
| SqlServer.server_name, string? | `@@SERVERNAME` | NS | NS | NS | NS |
| SqlServer.host_id, string? | `HOST_ID()` | NS | NS | NS | NS |
| SqlServer.host_name, string? | `HOST_NAME()` (client) | NS | NS | NS | NS |
| SqlServer.app_name, string? | `APP_NAME()` | NS | NS | NS | NS |
| SqlServer.suser_name, string? | `SUSER_NAME()` | NS | NS | NS | NS |
| SqlServer.original_login, string? | `ORIGINAL_LOGIN()` | NS | NS | NS | NS |
| Sql.pg_backend_pid, int | NS | `pg_backend_pid()` | NS | NS | NS |
| Sql.inet_server_addr, string? | NS | `CAST(inet_server_addr() AS text)` | NS | NS | NS |
| Sql.inet_server_port, int? | NS | `inet_server_port()` | NS | NS | NS |
| Sql.inet_client_addr, string? | NS | `CAST(inet_client_addr() AS text)` | NS | NS | NS |
| Sql.inet_client_port, int? | NS | `inet_client_port()` | NS | NS | NS |
| Sql.connection_id, ulong | NS | NS | `CAST(CONNECTION_ID() AS UNSIGNED)` | `CAST(connectionId() AS UInt64)` | NS |
| Sql.user, string | NS | NS | `USER()` | NS | NS |
| ClickHouse.host_name, string | NS | NS | NS | `hostName()` (server) | NS |
| ClickHouse.tcp_port, int | NS | NS | NS | `CAST(tcpPort() AS Int32)` | NS |
PG address returned as text of `inet`, no new CLR network type. `server_name()` renders the documented `@@SERVERNAME` (do not assume a native `SERVER_NAME()`); record this correction in issue/status/docs. `user()` is client `user@host`, does not replace existing `current_user()`.

## 4. Unstated by the statement / prerequisites
- Full global Versioned evidence contract format and test-scope schema not handed over → coder serializes the stated commitments losslessly, not weakening them.
- Existing translator/capability helper signatures not handed over → bounded Roslyn scout before edits; do not guess symbols or widen public API.
- Server versions, native NULL/reader behavior, CLI-tool availability → runtime/toolchain preflight, results in artifacts.
- Dirty EN/RU/specs tree → baseline diff and ownership of specific hunks agreed before writing; an intersection that cannot be safely isolated is a blocker, not permission to rewrite a file.
- Assumption/risk: nullable SQL Server strings = conservative API contract; docs do not promise non-empty/trusted HOST/APP values.
- Prerequisite: load the running-integration-tests skill; on a missing socket run the prescribed startup/recheck; never accept skips.

## 5. D-tasks
- D219-01 fix now: baseline/ownership/toolchain preflight; Roslyn impact + sibling-function form; preserve initial dirty hunks.
- D219-02 fix now: add exactly the listed API: `Query/SqlFunctions.cs:19,320`, `SqlFunctions.SqlServer.cs:15`, `SqlFunctions.ClickHouse.cs:34`; T53 registration.
- D219-03 fix now: `DialectCapabilities.cs:30,693` — session interface signatures UNCHANGED; add SQL Server DIM flag. Extend all provider `Supports/Render` per the table.
- D219-04 fix now: `SessionInfoFunctionTranslator.cs:24-45`, dispatch `NormSqlTranslator.cs:450-456`: exact registry of the new zero-arg members; vendor keys `sqlserver.*`/`clickhouse.*`, shared keys for `Sql.*`; fail-closed before Render.
- D219-05 fix now: dialect implementations `SqlServerDialect.cs:725`, `PostgresDialect.cs:611`, `MySqlDialect.cs:544`, `ClickHouseDialect.cs:805`, `SqliteDialect.cs:394`; MariaDB inheritance `MariaDbDialect.cs:11` verified separately.
- D219-06 fix now: provider SQL-gen/capability tests, integration/NULL/in-memory tests, red→green; do not regress the existing session family.
- D219-07 fix now: EN/RU + the two roadmap files, issue correction, DocFX.
- D219-08 fix now: single boundary sweep, coverage, frozen-manifest validation, status/collection evidence, task-only commits.
- Deferred with trigger: `system_user`/`suser_sname` synonyms — need separate-semantics/overload requirement; ClickHouse `serverUUID/FQDN/getServerPort(name)` — need the corresponding identity/argument function requirement. Do not move the milestone and do not create a future-milestone issue.
- Sibling docs restructure, benchmark artifacts, skill/readme changes — DO NOT touch; trigger to include = separate explicit permission.

## 6. Unit mode / siblings
One sequential unit in the current `1.0.9` tree. API, dialects, translators and docs overlap; streams/worktrees/branches/merge not needed. Sibling tasks get exclusive ownership of specific files/sections for the duration of D219; no concurrent edits of `SqlFunctions*.cs`, `Dialect/**`, provider tests, system-functions docs. T53 stays held; snapshot/candidate list updated but freeze acceptance not declared done. A sibling API change requires PLAN, not local improvisation.

## 7. Variant matrix and test strategy
| Axis/variant | Closure |
|---|---|
| All 6 providers × all 15 functions | test: 90 SQL-gen combinations per the table, incl. MariaDB |
| Supported / unsupported | test: native SQL/type or NS before SQL execution |
| Capability absent / flag false / Supports false | test + guard: NS; default DIM preserves custom-dialect compatibility |
| PG TCP network values | integration test: addresses/ports materialize, backend PID positive |
| PG unix-socket NULL | guard + test: nullable API and scalar-NULL materialization; **deferred native unix fixture** until a managed socket fixture exists, stays in 1.0.9 |
| SQL Server nullable strings / client-controlled strings | test: type/null propagation; docs guard — not security identity |
| MySQL/MariaDB USER vs CURRENT_USER | test: SQL differs; runtime checked against native USER |
| connection_id unsigned / ClickHouse port | test: CLR materialization, cast SQL, unsigned boundary via core scalar mapping tests |
| In-memory / direct sentinel invocation | test + guard: NS, no local/default evaluation |
| Function input null/default, value/reference inputs, overloads | guard: functions are zero-arg; no such inputs/overloads; nullable outputs checked separately |
| Existing session functions / custom dialect defaults | regression tests: prior SQL and behavior preserved |
Unit: SQL-gen/capability in six provider projects; scalar mapping/in-memory in core. Integration: SQLServer/Postgres/MySql/ClickHouse SpecificTests; MariaDB runtime case via explicit MariaDB external connection/fixture, not MySQL runtime passed off as MariaDB. Native dynamic identity verified on the same connection or checked as allowed form/range. Red→green: new missing API/unsupported/null cases first, preserve failure, then green focused runs.

Exact commands:
- C1: `dotnet build nextorm.slnx -c Debug`
- C2: `for p in sqlserver postgres mysql mariadb clickhouse sqlite; do dotnet test tests/nextorm.$p.tests -c Debug --filter "FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests" || exit $?; done`
- C3: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~InMemoryTests"`
- C4: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~SpecificTests|FullyQualifiedName~DialectCapabilityContractTests"`
- C5 (single DO→CHECK boundary sweep): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" --settings coverage.settings.xml --output-format cobertura --output artifacts/pdca/D219/rv1/coverage.cobertura.xml`
- C6: `reportgenerator "-reports:artifacts/pdca/D219/rv1/coverage.cobertura.xml" "-targetdir:artifacts/pdca/D219/rv1/coverage-report" "-reporttypes:JsonSummary"`
- C7: `python3 -c 'import xml.etree.ElementTree as E; r=E.parse("artifacts/pdca/D219/rv1/coverage.cobertura.xml").getroot(); assert float(r.attrib["line-rate"])>=.85; assert float(r.attrib["branch-rate"])>=.75'`
- C8: `dotnet docfx docs/docfx.json`
- C9: `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D219/rv1/manifest.json`

Structured test scope JSON for the DO brief:
```json
{"task_id":"T219","plan_revision":1,"rv":1,"variant":"pdca-dotnet","class":"new SQL function/operator","inner":{"commands":["C2","C3","C4"],"provider_projects":["sqlserver","postgres","mysql","mariadb","clickhouse","sqlite"],"core":["in-memory","scalar-null","unsigned-materialization"],"integration":["SQLServer","Postgres","MySql","MariaDB","ClickHouse"],"selectors":["FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests","FullyQualifiedName~InMemoryTests","FullyQualifiedName~SpecificTests|FullyQualifiedName~DialectCapabilityContractTests"]},"boundary":{"command":"C5","count":1,"scope":"nextorm.slnx"},"coverage":{"settings":"coverage.settings.xml","min_line":85,"min_branch":75},"forbid":["zero-tests-as-pass","provider-skips-as-pass","unrelated-sweep-per-edit"]}
```

## 8. Priorities / design checklist
P1 by construction: E219-01 form; E219-02 capability/fail-closed; E219-03 every provider; E219-04 nullable; runtime execution E219-05 and all acceptance invariants. CHECK does not lower them. SOLID/DRY: existing capability renders, translator dispatches; one registry, no provider type-checks in executor. Type design: explicit nullable/unsigned types, no new hierarchy/network type; DIM default false preserves compatibility. Perf anti-patterns: no sticky command mutation, cache disabling, per-row reflection/registry construction; registry built once. Structural sealedness: no measured ratio in the pack; scout records affected concrete types; do not introduce unsealed extensibility and do not seal existing public types without separate grounds.

## 9. Docs
Change `docs/scalar-functions/09-system-functions.md:26-28,82-98` and the RU mirror: API/table/support/null/client-vs-server/security caveat; remove realized exclusions. Do NOT create new pages: `10-system-functions.md`/`11-sqlserver-scalars.md` are stale issue paths. `docs/scalar-functions/toc.yml:19-20`, `docs/ru/toc.yml:118-138`: verify links; NO structure/toc changes planned. Guide renumbering not required. `docs/specs/roadmap/sql-function-coverage-gap.md:207-215`: lift realized exclusions, leave optional scope explicit. `docs/specs/roadmap/sql-capabilities-gap-analysis.md:42`: extend session/info completion to #219 with the exact support matrix. Public docs do not link specs; generated `docs/api`, `_site` not staged.

## 10. Perf / reconnaissance / risks
Cached-path acceptance benchmark NOT required: the change is scalar translation (`SessionInfoFunctionTranslator.cs:24-45`, dispatch `NormSqlTranslator.cs:450-456`), not query-path/cache/executor/materialization implementation. Prove via diff/impact: translation at prepare time, no new per-row work. If scout/diff finds new per-execution/row work or a cache-path change, the basis disappears: PLAN must add baseline/BenchmarkDotNet measurement, not a CHECK waiver. Reconnaissance needed, bounded: one scout round before DO (Roslyn receiver/dispatch/capability/reader impact, global contract schema, CLI availability, vendor validation of chosen mappings, MariaDB fixture availability). Risks: dirty docs/index, custom-dialect false-positive Supports, unsigned reader conversion, containers/MariaDB resource, PG unix fixture.

## 11. Write-footprint and commit
Public/shared: `src/nextorm.core/Query/SqlFunctions{,.SqlServer,.ClickHouse}.cs`, `DataContext/Dialect/DialectCapabilities.cs`, the five provider dialect files, `Visitors/{SessionInfoFunctionTranslator,NormSqlTranslator}.cs`. Tests: existing `SqlGenerationTests.cs`/`*DialectTests.cs` in the six provider projects; core in-memory/scalar tests; integration `DialectCapabilityContractTests.cs`, `{SqlServer,Postgres,MySql,ClickHouse}SpecificTests.cs`, MariaDB runtime fixture/test in the same integration project. Docs/status: EN/RU system guide; two roadmap files; D219 status; task-specific hunks of collection status and D53 candidate register; `artifacts/pdca/D219/rv1/**`. Normalize ONLY changed text files: `perl -pi -e 's/\r?\n/\r\n/g' <explicit-files>`. Plan commit: `git add -- docs/specs/status/D219-session-identity-functions-1.md`; `git commit -m "#219 plan session/server identity functions (r1)" -- docs/specs/status/D219-session-identity-functions-1.md`. Implementation commit: `#219 add session/server identity functions`; explicit authorized paths/task hunks only. Never `git add -A`, push/branch/worktree/merge.

## 12. Frozen versioned evidence contract — rv=1
Manifest: `artifacts/pdca/D219/rv1/manifest.json`. required_rows = `["E219-01","E219-02","E219-03","E219-04"]`; additional unconditional rows E219-05..E219-09 also mandatory. Each row: stable requirement/row ID, scenario, evidence kinds/sources, exact invocation, expected result/log, artifacts, owner, observable applicability, rv=1.
| Req/row/P | Check and planned evidence source | Invocation/expected | Planned artifacts/owner/applicability |
|---|---|---|---|
| R219-01/E219-01/P1 | API form, exact list, T53; Roslyn API/diff + sibling comparison | Roslyn members for `NextORM.Core.SqlFunctions.CommonFunctions`, `SqlServerFunctions`, `ClickHouseFunctions`, `NextORM.Core.ISqlServerFunctions`; list matches | api-form.json, api-diff.patch, T53 status reference / coder API / always |
| R219-02/E219-02/P1 | flags, 90-case Supports/Render/fail-closed, custom default | C2; exit 0, positive native SQL, negative NS, nonzero tests | capabilities.log, mapping/test inventory / coder tests / always |
| R219-03/E219-03/P1 | each of six providers, regressions | C2 and C5; exit 0, all six executed | provider-matrix.json, inner-provider.log, boundary.log / coder tests / always |
| R219-04/E219-04/P1 | null/type/unsigned, connection variants, no-input guard | C3,C4,C5; exit 0; core mapping tests in C5; native unix fixture deferred only as in matrix | nullable-types.json, core/integration logs / coder tests / always; native unix — fixture available |
| R219-05/E219-05/P1 | native runtime/materialization; in-memory NS | C3,C4,C5; exit 0, required provider cases executed, skips not pass | runtime.log, connection/provider metadata / coder integration / always |
| R219-06/E219-06/P1 | EN/RU parity, exclusions, links, stale paths | C8; exit 0, clean build/no broken-link warnings; scoped doc diff review | docfx.log, docs-review.json, docs diff / coder docs / always |
| R219-07/E219-07/P1 | build and project coverage | C1,C5,C6,C7; all exit 0; 0 Warn/Err; ≥85/75 | build.log, cobertura XML, JsonSummary, threshold log / coder verification / always |
| R219-08/E219-08/P1 | baseline preservation, CRLF, candidate registration, status/manifest | scoped baseline-vs-task diff/index review; C9 exit 0; no unrelated staged hunks | baseline.patch, task.patch, ownership.json, manifest / coder status / always |
| R219-08/E219-09/P1 | perf exemption, design/recon results | Roslyn callers of resolved translator symbols + scoped diff review; no execution/per-row/cache changes | perf-applicability.json, design-review.json, scout facts / scout→coder / always |
CHECK re-gather budget: owner `check`; max 2 targeted rounds. Missing report does not justify an rv/r revision or a new DO iteration; re-gather first. Supersession absent: r=1/rv=1 first revision.

## 13. Assumptions/risks and sibling requirements (persist explicitly)
- Assumption/risk: nullable SQL Server string returns are a conservative API contract; HOST/APP values are client-supplied and untrusted, documented as such.
- Assumption: PostgreSQL inet functions return NULL on unix sockets; native fixture deferred with trigger (managed socket fixture).
- Sibling requirements: exclusive ownership of `SqlFunctions*.cs`, `Dialect/**`, provider tests, `docs/scalar-functions/*` during D219; T53 remains held; a sibling public-API change requires its own PLAN.

## T53 public-API candidates (recorded here only; D53 untouched)
Held freeze; recorded for `docs/specs/status/D53-public-api-freeze-1.md` (basis `collection-1.0.9-todo.md:66`), not registered there and freeze not declared done:
- `SqlFunctions.SqlServer.server_name(): string?`
- `SqlFunctions.SqlServer.host_id(): string?`
- `SqlFunctions.SqlServer.host_name(): string?`
- `SqlFunctions.SqlServer.app_name(): string?`
- `SqlFunctions.SqlServer.suser_name(): string?`
- `SqlFunctions.SqlServer.original_login(): string?`
- `SqlFunctions.Sql.pg_backend_pid(): int`
- `SqlFunctions.Sql.inet_server_addr(): string?`
- `SqlFunctions.Sql.inet_server_port(): int?`
- `SqlFunctions.Sql.inet_client_addr(): string?`
- `SqlFunctions.Sql.inet_client_port(): int?`
- `SqlFunctions.Sql.connection_id(): ulong`
- `SqlFunctions.Sql.user(): string`
- `SqlFunctions.ClickHouse.host_name(): string`
- `SqlFunctions.ClickHouse.tcp_port(): int`
- `ISqlServerFunctions.SupportsSessionIdentity` (bool, default interface implementation `false`)

## Durable state
- Current cycle N: 1
- Plan revision r: 1
- Attempt n: 1/3
- Evidence contract rv: 1
- plan_state: ready
- defect_key registry: D219-C4-MYSQL-RESERVED-ALIAS (r1/n1, 1 fix applied), D219-C4-PG-INETNETMASK (r1/n1, 1 fix applied)
- Defect history: D219-C4-MYSQL-RESERVED-ALIAS — observed r1/n1 on C4 attempt 1 (MySQL/MariaDB `current_user` collided with a reserved word in the integration test projection: 3 failed / exit 2), fixed in-chunk (1 fix applied), final C4 0 failed; evidence artifacts/pdca/D219/rv1/logs/c4-specific-attempt1.log. D219-C4-PG-INETNETMASK — observed r1/n1 on the same attempt (PostgreSQL renders `inet` as text with its netmask, e.g. 10.0.0.1/32), fixed in-chunk (1 fix applied), final C4 0 failed; same evidence. No attempt/revision reset: r stays 1, n stays 1/3.

## Progress log

2026-10-13T00:56:00Z | PLAN | r1 | n1/3 | PLAN persisted (r=1); plan_state: ready; DO has NOT started — PLAN→DO boundary handoff to the collection parent | docs/specs/status/D219-session-identity-functions-1.md
2026-10-14T09:50:31Z | DO | r1 | n1/3 | production + unit tests green (C1/C2/C3); C5 boundary deferred | artifacts/pdca/D219/rv1/{report.json,logs/*}
2026-10-14 | DO | r1 | n1/3 | integration/runtime (C4) executed on SQLServer/PostgreSQL/MySQL/ClickHouse/MariaDB/SQLite; 394 executed / 0 skipped (386 SpecificTests + 8 DialectCapabilityContractTests, exit 0); MariaDB green on an explicit mariadb:11.4 container (native version() contains MariaDB) | artifacts/pdca/D219/rv1/{scope-integration.json,logs/c4-*}
2026-10-14T09:58:00Z | DO | r1 | n1/3 | defects D219-C4-MYSQL-RESERVED-ALIAS + D219-C4-PG-INETNETMASK observed on the first C4 attempt and fixed in-chunk (2 fixes applied); 3 failed -> 0 failed | artifacts/pdca/D219/rv1/logs/c4-specific-attempt1.log
2026-10-14T10:45:00Z | DO | r1 | n1/3 | docs chunk 3/3 (D219-07): EN+RU `scalar-functions/09-system-functions.md` reconciled (15-function per-provider identity table, null semantics, client-vs-server, security caveat, exclusions lifted), `roadmap/sql-function-coverage-gap.md` + `roadmap/sql-capabilities-gap-analysis.md:42` updated, toc links verified; C8 docfx exit 0 with 0 broken-link warnings | artifacts/pdca/D219/rv1/logs/c8-docfx.log

## Notes

- Correction recorded in docs: `SqlFunctions.SqlServer.server_name()` renders the documented `@@SERVERNAME` — SQL Server has no native `SERVER_NAME()` function, so that name is never emitted.
- `Sql.user()` is the MySQL/MariaDB client `user@host` pair and does NOT replace `current_user()` (the effective authenticated account).
- No new pages created (`10-system-functions.md`/`11-sqlserver-scalars.md` remain stale paths); no toc/structure/guide renumbering; public docs contain no links to `docs/specs/**`.
2026-10-14T10:14:20Z | DO | r1 | n1/3 | DO chunk 4/4 complete (D219-08): product+tests+docs committed (b5f8b360, 28 files, explicit paths only); single DO->CHECK boundary sweep C5 exit 0 (11838 total / 11639 succeeded / 0 failed / 199 capability-based skips, 0 provider skips); C6 exit 0; C7 exit 0 (line-rate 0.8812 >= 0.85, branch-rate 0.8039 >= 0.75); C9 manifest + report/brief validators exit 0; frozen evidence manifest rv=1 written | artifacts/pdca/D219/rv1/manifest.json
2026-10-14T11:55:54Z | CHECK | r1 | n1/3 | CHECK re-gather round 1 (E219-02/E219-04 negative/boundary evidence): 13 targeted filtered re-runs exit 0 (capability-false + custom-dialect default Supports=false -> NotSupportedException; SQL Server NULL propagation for the 6 nullable string identities; ulong connection_id max/zero boundary; in-memory fail-closed for the 15 functions); CRLF artifact 28/28 'i/lf w/crlf'; manifest row E219-08 command-in-path replaced by real artifacts and validate_inner_loop.py manifest exit 0 | artifacts/pdca/D219/rv1/check/rel/negative-boundary-evidence.json, artifacts/pdca/D219/rv1/crlf-check.txt
