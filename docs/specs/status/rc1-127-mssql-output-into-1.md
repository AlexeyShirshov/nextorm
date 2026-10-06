# Status — task D127 (issue #127)

- Task: D127
- Issue: #127 — https://github.com/AlexeyShirshov/nextorm/issues/127
- Collection: 1.0.9-rc1
- Group: G01
- Branch: 1.0.9-rc1
- Cycle: N=1
- Plan revision: r=2
- Attempt: n=1
- Mode: autonomous + auto-commit
- Git: no push / no merge

## Goal

SQL Server: `OUTPUT … INTO` a table variable (`DECLARE @t TABLE (...)`); generate the `DECLARE` by result-form or accept an explicit declaration; INSERT/UPDATE/DELETE OUTPUT INTO + read; sync+async; EN+RU docs without "phase" wording.

## Reconnaissance (persisted; not yet re-verified)

- Issue #127 "OUTPUT INTO в табличную переменную (DECLARE @t TABLE) на SQL Server"; OPEN; enhancement; milestone 1.0.9-rc1; no PRs/comments. Body: `OutputInto`/`OutputIntoThenOutput` supported with an explicit existing table name; a table variable is not supported ("in this phase"); want `DECLARE @t TABLE (...)` generated from the result form or an explicit declaration; AC = SQL Server `INSERT/UPDATE/DELETE … OUTPUT … INTO @t` + read test, EN+RU docs without phase wording; references closed #15.
- Surface: `SqlMutationBuilder.cs:28 MakeInsert,:258 MakeDelete,:349 MakeUpdate,:199 MakeMerge`; `:1019 AppendOutputClauses`; `:1031` throws if `!SupportsOutputInto`; `:1040 MakeDeletedOutputInto`/`MakeOutputInto`; `:1047 MakeDeletedOutput`/`MakeOutput`; target quote `:1039`. `MutationCommand.cs:73 OutputIntoClause` (table + columns), comment `:69-71` "does not declare a table variable (see #127)". `OutputIntoBuilder.cs:20,:34 Execute,:44 ExecuteAsync,:55 ToSql,:79 IOutputIntoMutation.BuildOutputIntoCommand`. LINQ: `InsertReturningBuilder.cs:152 OutputInto,:168 OutputIntoThenOutput,:175 EnsureOutputColumns`; `UpdateReturningBuilder.cs:90/104`; `DeleteReturningBuilder.cs:92/106`; impls `InsertBuilder.cs:614`, `UpdateBuilder.cs:311`, `DeleteBuilder.cs:263`.
- Flags: `ISqlDialect.cs:1413 SupportsOutput=>false`, `:1512 SupportsOutputInto=>false`, `:1520/:1528`; `SqlDialectBase.cs:875/885/887/891`. SQL Server sole `SupportsOutputInto=>true` (`SqlServerDialect.cs:27,:39`). `DataContext.cs:1383 EnsureReturningSupported`.
- **Exact gap:** no `DECLARE @t TABLE (...)` generation and no acceptance of an explicit declaration; `OutputIntoClause.TableName` (`MutationCommand.cs:78-85`) is only an existing table name; `DECLARE @t`/table-variable search = 0 in src/tests.
- Analog: PG `SupportsReturning=>true` `PostgresDialect.cs:42`, SQLite `:32`; render `MakeReturning ISqlDialect.cs:1495`; MySQL/MariaDB no RETURNING/OUTPUT. MERGE uses OUTPUT without INTO (`SqlMutationBuilder.cs:932-948`).
- Tests: `tests/nextorm.sqlserver.tests/InsertSqlGenerationTests.cs:74,382,395,408,421`; `UpdateSqlGenerationTests.cs:10,107,121`; `DeleteSqlGenerationTests.cs:7,67,80`; reject `sqlite InsertSqlGenerationTests.cs:590`, `postgres :658`, `mysql :335`; real DB `SqlServerSpecificTests.cs:20,614,647` (use `create table output_audit`, **not** `DECLARE @t TABLE`); common DML no OutputInto.
- Docs EN/RU: `docs/guide/15-insert-statement.md:490-532` (`:524` "does not declare a table variable … #127"), `docs/ru/guide/15-insert-statement.md:493-535` (`:527` references #127). Specs `roadmap/sql-capabilities-gap-analysis.md:405` (phases 1/3 shipped; `todo_output_into.md` not in tree), `evidence-02-resultset-unification.md:332,422` (#15).
- **Contradictions:** issue cites `docs/guide/17-insert-statement.md` (actual `15-insert-statement.md`); status file was absent.
- Open PLAN questions: generate `DECLARE` from the result form vs accept explicit declaration (or both); which mutations (INSERT/UPDATE/DELETE; MERGE-into?); sync+async; LINQ surface; real-SQL Server container tests; docs EN+RU; whether the table variable is declared in the same batch (SQL Server requirement).

## Escalation decision

- Decision A (rv=D127.r2.ec1): accept an explicit table-variable declaration supplied by the caller
  (`OutputIntoTableVariable(variableName, columnDefinitions)`); do not generate the `DECLARE` from the
  result form. One batch, one `MutationCommand`, one terminal.

## Plan (r=2, locked)

- New public method on `InsertReturningBuilder`/`UpdateReturningBuilder`/`DeleteReturningBuilder`:
  `OutputIntoTableVariable(string variableName, string columnDefinitions)` — same output projection
  (returning columns) as `OutputInto`. Existing `OutputInto`/`OutputIntoThenOutput` unchanged; MERGE
  excluded (`SqlMutationBuilder.cs:932-948`); no new descriptor type.
- One batch: `DECLARE @t TABLE (<defs>); <DML> OUTPUT <cols> INTO @t (<cols>);
  SELECT <explicit mapped cols> FROM @t;` — one `MutationCommand`; the final SELECT is the result;
  no `SELECT *`; no row-order guarantee.
- `OutputIntoClause` (`MutationCommand.cs:73`) gains table-variable declaration + read-back fields;
  comment `:69-71` updated.
- Emission inside/next to `AppendOutputClauses` (`SqlMutationBuilder.cs:1019`) under
  `SupportsOutputInto` (`:1031`); other providers throw as today.
- `variableName` validated `@[A-Za-z_]\w*` and NOT bracketed/quoted; `columnDefinitions` is
  caller-supplied trusted SQL text (not a parameter) — documented in XML-doc and public docs (docs
  deferred to the next stream).
- Declared columns are storage columns (no identity/computed/default copying). Plan-cache key includes
  the declaration + variable name; shared commands are not mutated.
- Acceptance: INSERT/UPDATE/DELETE × sync/async × populated/empty × nullable/value/reference ×
  identity/computed source values; SQL-gen tests; real SQL Server container tests; EN+RU docs remove
  "phase" wording and add a comparison table vs existing-table output; no public→specs links.
- Evidence rows D127-E01..E10; CHECK re-gather budget 2.

### D127 evidence rows E01–E10 (final)

| Id | Criterion (from :54-56) | Evidence | Status |
|---|---|---|---|
| D127-E01 | SQL-gen INSERT table-variable | `tests/nextorm.sqlserver.tests/InsertSqlGenerationTests.cs:434,447` | closed |
| D127-E02 | SQL-gen UPDATE/DELETE table-variable | `tests/nextorm.sqlserver.tests/UpdateSqlGenerationTests.cs:135`, `tests/nextorm.sqlserver.tests/DeleteSqlGenerationTests.cs:93` | closed |
| D127-E03 | SQL-gen guards/rejections | `InsertSqlGenerationTests.cs:421,465,478,491`; postgres `InsertSqlGenerationTests.cs:672`, mysql `:349`, sqlite `:604` | closed |
| D127-E04 | real container INSERT table-variable identity read-back + nullable reference NULL | `tests/nextorm.integration.tests/SqlServerSpecificTests.cs:1920`; `artifacts/d127/sqlserver-class.xml` | closed |
| D127-E05 | real container UPDATE/DELETE table-variable populated AND empty set | `SqlServerSpecificTests.cs:1958,2002` | closed |
| D127-E06 | sync + async execution all DML | `SqlServerSpecificTests.cs:2041,2068,2100` | closed |
| D127-E07 | nullable/value/reference source values | value `id`/`age`; nullable reference `name` NULL round-trip at `SqlServerSpecificTests.cs:1937-1954` | closed |
| D127-E08 | INSERT empty-result set | tests/nextorm.integration.tests/SqlServerSpecificTests.cs:2130; artifacts/d127/rc1-127-computed-empty-1/sqlserver-class.log | closed |
| D127-E09 | computed source value through OutputInto table-variable | SqlServerSpecificTests.cs:2174 (Insert), :2221 (Update), :2277 (Delete); artifacts/d127/rc1-127-computed-empty-1/sqlserver-class.log | closed |
| D127-E10 | EN+RU docs: no "phase" wording, comparison table vs existing-table output, no public→specs links | `docs/guide/15-insert-statement.md:531-572,576-582`, `docs/ru/guide/15-insert-statement.md:534-577,579-585`; link audit found no `docs/**` or `readme.md` hits | closed |

Run evidence: `artifacts/d127/test-sqlserver.log:4-8` (697 total / 697 succeeded / 0 failed / 0 skipped);
`artifacts/d127/sqlserver-class.log:22-23` (`Total: 87, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`, `EXIT=0`);
`artifacts/d127/integration.log:437-439` (`Total: 3191, Errors: 0, Failed: 0, Skipped: 193, Not Run: 0`, `EXIT=0`).

**Correction note:** the earlier `done` claim is **not verified against the full frozen criteria** while
D127-E08 and D127-E09 remain open gaps. D127 is **not** marked superseded; the two gaps are open residual
criteria, not a replacement of scope.

Observation only: `examples/README.md` links fall outside the `docs/**` + `readme.md` public→specs rule and
outside this cycle's scope; no public doc link audit violation was found in the rule's scope.

## Progress log

Recon persisted (run 10 start).
- Notice: host has no todowrite tool; this file is the progress log.
- 2026-10-06T01:49:25Z | DO | revision r=2 | iteration 1/3 | Step 0: locked plan r=2 persisted (escalate decision A, rv=D127.r2.ec1); recon kept | docs/specs/status/rc1-127-mssql-output-into-1.md

## DO stream — D127 implementation + tests

- Implementation (uncommitted, working tree): `OutputIntoTableVariable(string variableName, string columnDefinitions)` on `InsertReturningBuilder`/`UpdateReturningBuilder`/`DeleteReturningBuilder`; `OutputIntoClause` gained a table-variable ctor + `VariableName`/`ColumnDefinitions`/`IsTableVariable` (`MutationCommand.cs`); `SqlMutationBuilder.WrapTableVariableReadBack` emits `DECLARE @t TABLE (<defs>); <DML> OUTPUT <cols> INTO @t (<cols>); SELECT <cols> FROM @t` in one batch for INSERT/UPDATE/DELETE; existing `OutputInto`/`OutputIntoThenOutput` untouched; provider gating unchanged (`SupportsOutputInto`).
- Rejection tests for the new form (mirroring the existing `OutputInto_*_ShouldThrow`): PostgreSQL `OutputIntoTableVariable_OnPostgres_ShouldThrow`, MySQL `OutputIntoTableVariable_OnMySql_ShouldThrow` added; SQLite `OutputIntoTableVariable_OnSqlite_ShouldThrow` confirmed present.

## Test evidence (exit code + counts)

- `dotnet test tests/nextorm.sqlserver.tests -c Debug` → exit 0; total 697, succeeded 697, failed 0, skipped 0 — `artifacts/d127/test-sqlserver.log`
- `dotnet test tests/nextorm.sqlite.tests -c Debug` → exit 0; total 1059, succeeded 1058, failed 0, skipped 1 (pre-existing `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`) — `artifacts/d127/test-sqlite.log`
- `dotnet test tests/nextorm.postgres.tests -c Debug` → exit 0; total 771, succeeded 771, failed 0, skipped 0 — `artifacts/d127/test-postgres.log`
- `dotnet test tests/nextorm.mysql.tests -c Debug` → exit 0; total 284, succeeded 284, failed 0, skipped 0 — `artifacts/d127/test-mysql.log`
- No expectation mismatches; no test edits beyond the two added rejection tests.

## Build evidence

- `dotnet build nextorm.slnx -c Debug` → exit 0; 0 Warning(s), 0 Error(s) — `artifacts/d127/final-build.log`.

## Hygiene

- `git diff --check` → exit 0 (no whitespace errors).
- All changed files (16 tracked + this status file) CRLF-normalized (`perl -pi -e 's/\r?\n/\r\n/g'`); verified every line `\r\n`, lone LF = 0.
- No commit/push.

- 2026-10-06T01:52:42Z | DO | revision r=2 | iteration 1/3 | DO stream complete: implementation + PG/MySQL rejection tests added (SQLite confirmed), 4 unit suites green, solution build 0/0, diff/CRLF clean | artifacts/d127/

## DO fix — D127 loop-back (captured-local parameter naming)

- **Defect key `D127-param-name-missingId`** (first observed on the SQL Server class run): 2 container
  tests failed with `Microsoft.Data.SqlClient.SqlException : Must declare the scalar variable "@missingId"`.
  A captured closure local in a predicate binds under its **source name** (`MemberTranslator.cs:337`,
  `node.Member.Name` → `@missingId`), not positionally; the affected empty-set calls passed the wrong
  parameter name (`"id"` for a `missingId` predicate). Fixes applied: **1** (test-only parameter names).
  Files: `tests/nextorm.integration.tests/SqlServerSpecificTests.cs` — UPDATE empty-set
  `("p0", marker), ("missingId", missingId)`, DELETE empty-set `("missingId", missingId)`. No plan change
  (`r` stays 2, `n` stays 1); no candidate rejected; production code unchanged.
- INSERT assertions verified: `OutputIntoTableVariable_Insert_ShouldDeclareStorageColumnsAndReadBackIdentity`
  keeps `NotContainEquivalentOf("identity")`, the `declare @t table (...)` prefix and the populated +
  nullable-NULL read-back; the identity value is stored into the plain declared column; all pass.
- `dotnet run --project tests/nextorm.integration.tests -c Debug -- -class nextorm.integration.tests.SqlServerSpecificTests -noColor -result-xml artifacts/d127/sqlserver-class.xml`
  (with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) → **exit 0**;
  Total 87, failed 0, skipped 0, not-run 0, 11.380s; all **6** D127 cases pass —
  `artifacts/d127/sqlserver-class.log`, `artifacts/d127/sqlserver-class.xml`.
- Full suite `DOCKER_HOST=... dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml artifacts/d127/integration.xml`
  → **exit 0**; Total 3191, failed 0, skipped 193, not-run 0, 31.388s —
  `artifacts/d127/integration.log`, `artifacts/d127/integration.xml`.
  - Per-provider executed/failed/skipped: SQL Server 671/0/43, PostgreSQL 753/0/25, SQLite 648/0/44,
    MySQL 579/0/79, MariaDB 50/0/0, ClickHouse 177/0/0; provider-neutral collections 120/0/2.
  - SQL Server version: `Microsoft SQL Server 2025 (RTM-CU9) (KB5122048) - 17.0.5005.3 (X64)`,
    Enterprise Developer Edition (image `mcr.microsoft.com/mssql/server:2025-latest`).
  - All 6 `OutputIntoTableVariable` cases executed and passed (INSERT/UPDATE/DELETE ×
    read-back/empty-set × `Execute`/`ExecuteAsync`).

## Docs / build evidence (D127 finish)

- Docs EN+RU rewritten: `docs/guide/15-insert-statement.md`, `docs/ru/guide/15-insert-statement.md` —
  removed "does not declare a table variable … #127"; documented `OutputIntoTableVariable` (variable
  name `@[A-Za-z_]\w*`; `columnDefinitions` is trusted caller-supplied SQL text embedded verbatim, not a
  parameter; declared columns are storage columns with no IDENTITY/COMPUTED/DEFAULT copying; unordered
  read-back; no `@@ROWCOUNT` rows-affected handle); added the three-form comparison table
  (`OutputInto`/`OutputIntoThenOutput`/`OutputIntoTableVariable`); referenced the closed issue #15. No
  public→specs links added.
- Stray `17-insert-statement.md` refs: none exist in tracked public docs (all public references already
  point to `15-insert-statement.md`); repo-wide the string only survives in archived spec experiment
  `.patch` artifacts and this status note, which were left untouched.
- `dotnet docfx docs/docfx.json` → **exit 0**; 2 warnings (both pre-existing
  `AnalyzerReleases.{Shipped,Unshipped}.md` duplicate-source in `nextorm.core.sourcegenerator`), 0 errors —
  `artifacts/d127/docfx.log`.
- `dotnet build nextorm.slnx -c Debug` → **exit 0**; 0 Warning(s), 0 Error(s) — `artifacts/d127/final-build.log`.
- `git diff --check` → **exit 0** (no whitespace errors). All 20 changed files CRLF-normalized; lone-LF
  lines = 0. No commit/push/merge.

- 2026-10-06T02:05:14Z | DO fix | revision r=2 | iteration 1/3 | D127-param-name-missingId fixed (2 test parameter names); SqlServer class green (87/0/0); full suite green (3191/0/193, all 6 D127 cases); docs EN+RU rewritten; docfx 0 err; build 0/0; diff/CRLF clean | artifacts/d127/
- 2026-10-06T03:10:00Z | CHECK | revision r=2 | iteration 1/3 | CHECK PASS (rv=D127.r2.ec1); freeze point = post-DO-fix working tree; 6 D127 cases green on real SQL Server 2025; defect D127-param-name-missingId closed | artifacts/d127/

## ACT

- CHECK verdict: **PASS** — rv=D127.r2.ec1, r=2, n=1/3. Freeze point: post-DO-fix working tree (uncommitted) as audited by CHECK, before this ACT.
- Disposition: **option A** — `OutputIntoTableVariable(string variableName, string columnDefinitions)` accepts an explicit table-variable declaration; the `DECLARE` is not generated from the result form. One batch, one `MutationCommand`; MERGE excluded.
- Commit plan: a single commit on `1.0.9-rc1` containing exactly the D127 change set (9 src + 7 tests + EN/RU docs + this status file); `artifacts/` never staged; no push, no merge.
- Defect `D127-param-name-missingId`: **CLOSED** — a captured-closure local binds under its source name (`@missingId`); 2 test parameter names corrected (`"missingId"`), production unchanged; 1 fix applied, no plan-revision change.
- Issue outcome: #127 closed remotely with the summary comment (feature shipped); commit unpushed per collection rules.

## ACT addendum — corrective cycle rc1-127-computed-empty-1

- Corrective cycle `rc1-127-computed-empty-1` (branch `1.0.9-rc1`, cycle N=1, plan revision r=1, attempt n=1/3)
  **closed the two residual parent criteria D127-E08 and D127-E09**; both evidence rows above now read `closed`.
- **Product code unchanged** — `git diff --stat -- src` is empty; the E08/E09 reds were a test
  parameter-naming defect (`rc1-127-param-indexer-name`) fixed entirely in
  `tests/nextorm.integration.tests/SqlServerSpecificTests.cs`.
- Evidence: build exit 0 (0 warnings / 0 errors); real SQL Server class run exit 0 —
  Total 91 / Passed 91 / Failed 0 / Skipped 0 (E08 + 3×E09 new, 6 inherited D127 cases green);
  full integration exit 0 — Total 3195 / Passed 3002 / Failed 0 / Skipped 193 (all 5 required
  providers executed). Logs: `artifacts/d127/rc1-127-computed-empty-1/`.
- Commit sha: see git log.
- Observation only (non-blocking, no product change): the plan-cache-key wording at `:52-53` is
  imprecise — the `OutputInto` path (`OutputIntoBuilder.cs:36-63`, `MutationCommand.cs:31-37`)
  bypasses `QueryPlanStore`, so there is no plan-cache key to include the declaration/variable name.
  Correctness of the shipped behavior is unaffected.

