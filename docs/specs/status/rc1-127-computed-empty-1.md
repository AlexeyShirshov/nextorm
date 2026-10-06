# Status — corrective task rc1-127-computed-empty-1 (issue #127, D127 E08/E09)

- task: rc1-127-computed-empty-1
- issue: #127 — https://github.com/AlexeyShirshov/nextorm/issues/127
- parent cycle: docs/specs/status/rc1-127-mssql-output-into-1.md (D127, r=2, CHECK PASS, committed)
- collection: `1.0.9-rc1`
- group: G01
- branch: `1.0.9-rc1`
- worktree: current (no separate worktree)
- cycle N=1; plan revision r=1; attempt n=1/3
- evidence contract: rv=1 (rows rc1-build / rc1-unit / rc1-e08 / rc1-e09 / rc1-reg / rc1-safe)
- mode: autonomous; auto-commit authorized; **DO does not commit** (ACT commits); no push / no merge
- unit execution mode: sequential, single tree (no parallel writers)

## Goal

Close the two residual D127 criteria left open by the parent cycle:
**AC08** — a builder-generated `INSERT ... SELECT` whose source matches **0 rows** must return an empty
read-back and leave the target table unchanged (sync + async);
**AC09** — a **computed** column value must round-trip through the table-variable read-back for
INSERT / UPDATE / DELETE (`inserted`/`inserted`/`deleted`), sync + async, on a real SQL Server.

## Reconnaissance decision

`needed / bounded` — **done**. Facts (files, line ranges, fluent chain, parameter-name convention,
mapper shape) were gathered before DO and are recorded below; no re-research from scratch.

### Reconnaissance (gathered, bounded)

- Integration tests: `tests/nextorm.integration.tests/SqlServerSpecificTests.cs`.
  Existing D127 tests: INSERT table-variable 1920–1955, UPDATE 1957–1999, DELETE 2001–2038,
  sync/async 2040–2125. Helpers: `ExecuteTableVariableBatch(IDataContext,string,params ProcedureParameter[])`
  2127–2132 (`ctx.ExecuteRaw(sql, parameters).Read<OutputIntoRow>()`); `OutputIntoRow` DTO
  `[SqlTable("output_into_row")]` 2134–2142; raw-DDL `Execute(IDataContext,string)` 2144–2149
  (`EnsureConnectionOpen()` + `((DataContext)ctx).CreateCommand(sql).ExecuteNonQuery()`); try/finally
  create→drop example 620–643.
- Fluent chain (Roslyn): `InsertBuilder<TEntity>.Values<TSource,TResult>(EntityBuilder<TSource> source,
  Expression<Func<TSource,TResult>> mapping)` returns `InsertBuilder<TEntity>`; then
  `.Returning<TResult>(Expression<Func<TEntity,TResult>>)` → `InsertReturningBuilder`; then
  `.OutputIntoTableVariable(string variableName, string columnDefinitions)`
  (`InsertReturningBuilder.cs:172`), terminal `OutputIntoBuilder` with `ToSql()/Execute()/ExecuteAsync()`.
- UPDATE is predicate-based (`UpdateBuilder<TEntity>.Set(...)`, `.Where(...)`, `.Returning(...)`);
  DELETE predicate-based (`DeleteBuilder<TEntity>.Where(...)`, `.Returning(...)`). No from-source
  update/delete.
- INSERT excludes computed columns (`InsertBuilder.Values.cs:127-130` skips IsIdentity||IsComputed);
  a lambda/selector naming a computed column throws (`InsertBuilder.cs:423,518`, `UpdateBuilder.cs:376`).
  So the computed column is never set/selected in the write; it appears only in `Returning(...)`.
- Computed mapping attribute: `[DatabaseGenerated(DatabaseGeneratedOption.Computed)]`
  (`System.ComponentModel.DataAnnotations`) + `[Column("...")]` (`System.ComponentModel.DataAnnotations.Schema`);
  `[SqlTable("...")]` from `NextORM.Core`.
- Parameter names: a constant value form (`.Value` / `.Values(entity)`) binds positionally as `@p0`,
  `@p1`, …; a captured closure local in a predicate binds under its **source name** (e.g. `@marker`,
  `@id`) — see the parent-cycle defect `D127-param-name-missingId`.
- Async shape: `await <chain>.ExecuteAsync(TestContext.Current.CancellationToken)`;
  raw async read uses `await ctx.ExecuteRawAsync(sql, parameters, ct)` + `await foreach (var r in
  result.ReadAsync<TRow>(ct))`.
- SQL-gen shape pins (optional, not a substitute for integration): `InsertSqlGenerationTests.cs:433-444,446-457`,
  `UpdateSqlGenerationTests.cs:134-146`, `DeleteSqlGenerationTests.cs:92-103`.

## Acceptance criteria

- **AC08**: adding a new `OutputIntoTableVariable` integration test for `INSERT ... SELECT` with a
  source predicate matching no rows; `ExecuteTableVariableBatch` returns an **empty** list; the
  `insert_entity` row count is unchanged; both a sync and an async variant run green on a real SQL
  Server. Emitted SQL pinned (`declare @t table (` prefix, `output inserted.id, inserted.name into @t
  (id, name)`, `; select id, name from @t` suffix).
- **AC09**: adding new `OutputIntoTableVariable` integration tests for a real table with a persisted
  computed column (`total as (basis * 2) persisted`); INSERT reads back `Total == 42` (`Basis == 21`)
  with `output inserted.*` (incl. `inserted.total`); UPDATE reads back `Total == 44` with
  `output inserted.total`; DELETE reads back `Total == 44` with `output deleted.total`; sync + async for
  all three operations; declaration text contains no `identity` / ` as ` and the read-back selects the
  plain declared columns.
- **ACREG**: `dotnet test tests/nextorm.sqlserver.tests -c Debug` stays fully green (existing SQL-gen
  guards/rejections for `OutputIntoTableVariable` unchanged); full `SqlServerSpecificTests` class on a
  real SQL Server stays green (existing 6 D127 cases + new E08/E09 cases), 0 failed / 0 skipped.
- **ACSAFE**: no public API change; no plan-cache-key change (declaration + variable name only); shared
  commands not mutated; `command.Cache = false` never used (only `storeInCache:false` if ever required);
  product code changed only if a red demands the minimal `WrapTableVariableReadBack`/`AppendOutputClauses`
  fix.

## Tasks

- **D0 — reconnaissance / facts gather** — `completed` (bounded recon recorded above; no re-research).
- **D1 — create this corrective status file** (plan record r=1, rv=1 contract, blank progress log).
- **D2 — add E08 integration test** `OutputIntoTableVariable_Insert_EmptyResultSet_ShouldReturnEmptyAndNotChangeTarget`
  (builder-generated `INSERT ... SELECT` matching 0 rows; sync + async; SQL pins; row-count control).
- **D3 — add E09 integration tests** `OutputIntoTableVariable_{Insert,Update,Delete}_ComputedColumn_ShouldReadBackComputedValue`
  (real computed table via `try/finally`; local entity; INSERT 21→42, UPDATE 22→44, DELETE 44; sync + async;
  SQL pins incl. `inserted`/`deleted` pseudo-tables).
- **D4 — CRLF normalization + build** each changed file; `dotnet build nextorm.slnx -c Debug` 0 warnings / 0 errors.
- **D5 — inner-loop evidence** (logs under `artifacts/d127/rc1-127-computed-empty-1/`):
  unit suite `tests/nextorm.sqlserver.tests`; full `SqlServerSpecificTests` class on real SQL Server
  (with `DOCKER_HOST`), 0 failed / 0 skipped.

## Risks

- Parameter-name mismatch when executing builder-generated SQL through the raw batch helper
  (`@p0` for constants, source name for captured locals) → the parent-cycle defect; mitigated by
  copying the existing D127 parameter-name pattern.
- `Values(source, mapping)` with an empty source could throw at build/validation time instead of
  producing 0 rows → E08 would be red; if so, determine whether it is a test issue or a product gap
  (minimal fix only, STEP 3).
- `NotContainEquivalentOf(" as ")` / `"identity"` over the whole SQL could collide with unrelated
  tokens → assert on the actual rendered text and adjust only if a real false positive appears.
- Shared `insert_entity` table mutated by a buggy E08 → guarded by before/after row-count control.
- Table-variable table name collision between the three E09 tests (same collection, sequential) →
  each test drops at start and in `finally`.

## Test strategy

- Unit/build: `dotnet build nextorm.slnx -c Debug`; `dotnet test tests/nextorm.sqlserver.tests -c Debug`
  (SQL-gen guards/rejections; affected project only, already-built).
- Boundary: `DOCKER_HOST=... dotnet run --project tests/nextorm.integration.tests -c Debug -- -class
  nextorm.integration.tests.SqlServerSpecificTests -noColor -result-xml <log>` — full class on real
  SQL Server; all existing 6 D127 cases + new E08/E09 must pass, 0 failed / 0 skipped.
- The structured test scope in the DO brief substitutes for `scripts/validate_inner_loop.py`
  (which does not exist in this repo); the validator is not created or invoked.

## Docs plan

- No public docs change in this corrective cycle: the D127 EN+RU docs already describe the
  `OutputIntoTableVariable` table-variable form; E08/E09 close test-only residual criteria.
- This internal status file is the only documentation touched.

## Perf / recon decisions

- Perf decision: **not needed** — test-only change plus a conditional minimal SQL-render fix; no
  per-row work, no hot-path change.
- Recon decision: **needed / bounded** — done before DO (see above).

## Design checklist / lenses

- [ ] public API unchanged (`OutputIntoBuilder`, `OutputIntoClause`, returning builders untouched)
- [ ] plan-cache key unchanged (declaration text + variable name only)
- [ ] shared commands not mutated; no sticky `Cache=false`
- [ ] `WrapTableVariableReadBack` / `AppendOutputClauses` touched only if a red demands it
- [ ] destination column order + pseudo-table selection + declaration text preserved
- [ ] integration tests real-server only (no handwritten-SQL fallback for E08)
- [ ] build 0 warnings / 0 errors; CRLF preserved
- Lenses: correctness (computed identity/read-back), concurrency/shared-state (command cache), SQL
  rendering (column/alias spelling), test isolation (row-count control, table lifecycle).

## Closed variant matrix

| Variant | Decision |
| --- | --- |
| empty INSERT…SELECT sync/async | TEST (E08) |
| nonempty INSERT | TEST (E09 / inherited D127) |
| computed INSERT/UPDATE/DELETE sync/async | TEST (42/44/44; inserted/inserted/deleted) |
| computed vs NULL/0 | TEST |
| nullable/reference computed | DEFERRED (trigger: requested support-specific regression) |
| plain storage vs AS/identity | TEST (E09 declaration) |
| OutputInto vs OutputIntoTableVariable | GUARD/test (unchanged API) |
| SQL Server vs unsupported dialects | GUARD/test (unchanged capability; full 5-provider run at boundary) |
| invalid/null variable names | DEFERRED (trigger: validation/API change) |
| cache/uncached + shared commands | GUARD (unchanged key/state; only `storeInCache:false` if ever needed) |

## Evidence contract (rv=1; results recorded)

| Id | Command / check | Exit | Counts | Evidence |
| --- | --- | --- | --- | --- |
| rc1-build | `dotnet build nextorm.slnx -c Debug` | 0 | 0 warnings / 0 errors | `artifacts/d127/rc1-127-computed-empty-1/build.log` |
| rc1-unit | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | 0 | total 697 / succeeded 697 / failed 0 / skipped 0 | `artifacts/d127/rc1-127-computed-empty-1/test-sqlserver.log` |
| rc1-e08 | class run; `OutputIntoTableVariable_Insert_EmptyResultSet_ShouldReturnEmptyAndNotChangeTarget` | 0 | Pass | `sqlserver-class.xml` / `sqlserver-class.log` |
| rc1-e09 | class run; `OutputIntoTableVariable_{Insert,Update,Delete}_ComputedColumn_ShouldReadBackComputedValue` | 0 | 3 × Pass (42 / 44 / 44; `inserted` / `inserted` / `deleted`) | `sqlserver-class.xml` / `sqlserver-class.log` |
| rc1-reg | class run `-class nextorm.integration.tests.SqlServerSpecificTests` on real SQL Server | 0 | Total 91 / Passed 91 / Failed 0 / Skipped 0 / Not Run 0; all 6 existing D127 cases + 4 new pass | `sqlserver-class.log` / `sqlserver-class.xml` |
| rc1-safe | `git diff --stat -- src` empty; unit SQL-gen suite green | 0 | no API / cache-key / shared-command change | `git diff` output (src empty) + `test-sqlserver.log` |

Real SQL Server run command (DOCKER_HOST set to the Podman socket; `--no-build` after the solution build):

`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class nextorm.integration.tests.SqlServerSpecificTests -noColor -result-xml artifacts/d127/rc1-127-computed-empty-1/sqlserver-class.xml`

## Defect history

- **defect key `rc1-127-param-indexer-name`** — observed on class run 1 (`r=1`, `n=1`): the E09 UPDATE case failed
  with `Microsoft.Data.SqlClient.SqlException : Must declare the scalar variable "@p1"` and the E09 DELETE case
  with `... "@p0"`. Cause: the predicate captured an **indexer** (`ids[0]`/`ids[1]`) rather than a simple
  local; the generated SQL bound those operands positionally (`@p0`/`@p1`, after the `SET` `@p0`) instead of
  under a source name, so the raw batch parameters were mis-named. **Fixes applied: 1** — test-only: bind
  `var id1 = ids[0]; var id2 = ids[1];` and pass parameters `id1`/`id2` (matching the D127 simple-local
  convention). No plan change (`r` stays 1, `n` stays 1); no candidate rejected; production code unchanged.
  Evidence: `sqlserver-class.log` (first run, 2 failed / 91 total) → `sqlserver-class.log` + `sqlserver-class.xml`
  (final run, 0 failed / 91 total). Not recurring.

## Product code

- **No product change** — `git diff --stat -- src` is empty. The E08/E09 reds were a test parameter-naming
  defect (`rc1-127-param-indexer-name`), fixed entirely in the test file. `WrapTableVariableReadBack` /
  `AppendOutputClauses` were not touched; API, plan-cache key and shared-command state unchanged.

## Progress log

2026-10-06T00:00:00Z | DO started | revision r=1 | iteration 1/3 | Step 0 status file created (plan record r=1, rv=1 contract, blank log) | docs/specs/status/rc1-127-computed-empty-1.md
2026-10-06T03:18:00Z | DO | revision r=1 | iteration 1/3 | D2/D3 added E08 + 3 E09 integration tests, generic sync/async batch helpers and the computed entity/read-back DTOs in tests/nextorm.integration.tests/SqlServerSpecificTests.cs; CRLF normalized (lone LF = 0) | tests/nextorm.integration.tests/SqlServerSpecificTests.cs
2026-10-06T03:19:00Z | DO | revision r=1 | iteration 1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors | artifacts/d127/rc1-127-computed-empty-1/build.log
2026-10-06T03:19:34Z | DO | revision r=1 | iteration 1/3 | class run 1 RED: `rc1-127-param-indexer-name` (E09 UPDATE `@p1`, DELETE `@p0`); fix 1 applied (test-only simple locals); r/n unchanged | artifacts/d127/rc1-127-computed-empty-1/sqlserver-class.log
2026-10-06T03:20:00Z | DO | revision r=1 | iteration 1/3 | D4/D5 evidence complete: build 0/0; unit 697/697/0/0 exit 0; class run 2 exit 0 Total 91 / Passed 91 / Failed 0 / Skipped 0 / Not Run 0; E08 + 3 E09 Pass; existing 6 D127 Pass; `git diff --stat -- src` empty | artifacts/d127/rc1-127-computed-empty-1/


## Boundary gate-C evidence (DO→CHECK / parent sweep)

- Product code unchanged: `git diff --stat -- src` empty.
- E08/E09 pass in the fresh SQL Server class run (`sqlserver-class.xml`).
- All boundary commands exit 0; logs under `artifacts/d127/rc1-127-computed-empty-1/`.

| Id | Command | Exit | Counts | Log |
| --- | --- | --- | --- | --- |
| rc1-build | `dotnet build nextorm.slnx -c Debug` | 0 | 0 warnings / 0 errors | `build.log` |
| rc1-unit-ss | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | 0 | total 697 / passed 697 / failed 0 / skipped 0 | `test-sqlserver.log` |
| rc1-unit-lite | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` | 0 | total 1059 / passed 1058 / failed 0 / skipped 1 | `test-sqlite.log` |
| rc1-unit-pg | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build` | 0 | total 771 / passed 771 / failed 0 / skipped 0 | `test-postgres.log` |
| rc1-unit-my | `dotnet test tests/nextorm.mysql.tests -c Debug --no-build` | 0 | total 284 / passed 284 / failed 0 / skipped 0 | `test-mysql.log` |
| rc1-e08-e09 | `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class nextorm.integration.tests.SqlServerSpecificTests -noColor -result-xml …` | 0 | Total 91 / Passed 91 / Failed 0 / Skipped 0 / Not Run 0 | `sqlserver-class.xml` / `sqlserver-class.log` |
| rc1-full | full integration, all providers, `-noColor -result-xml …` | 0 | Total 3195 / Passed 3002 / Failed 0 / Skipped 193 / Not Run 0 | `integration.xml` / `integration.log` |

Per-provider (parsed from `integration.xml`; all five providers executed):

| Provider | Total | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| PostgreSQL | 778 | 753 | 0 | 25 |
| SQL Server | 718 | 675 | 0 | 43 |
| MySQL | 658 | 579 | 0 | 79 |
| SQLite | 692 | 648 | 0 | 44 |
| ClickHouse | 177 | 177 | 0 | 0 |

(Bonus, not among the five: MariaDB 50 / 50 / 0 / 0; provider-agnostic 122 / 120 / 0 / 2 — includes
`LobCapabilityProbeTests` 2 skipped.)

- No provider wholesale-skipped: each of the five shows a large executed count and 0 failed; the 193
  skips are per-test `Assert.SkipUnless` capability gaps, not whole-provider skips.
- SQL Server line: `Microsoft SQL Server 2025 (RTM-CU9) (KB5122048) - 17.0.5005.3 (X64)` — the run
  logs do not emit `@@VERSION`; queried from the reused container (`mcr.microsoft.com/mssql/server:2025-latest`,
  id `4ab2d4542432`).

2026-10-06T03:23:30Z | CHECK (boundary gate-C) | revision r=1 | iteration 1/3 | build 0/0; unit ss 697/697/0/0, lite 1059/1058/0/1, pg 771/771/0/0, my 284/284/0/0; fresh class 91/91/0/0 (E08 + 3×E09 Pass); full integration 3195/3002/0/193, all 5 providers executed; `git diff --stat -- src` empty; SQL Server 2025 (RTM-CU9) 17.0.5005.3 | artifacts/d127/rc1-127-computed-empty-1/{build,test-sqlserver,test-sqlite,test-postgres,test-mysql,sqlserver-class,integration}.*

## Execution manifest (rv=1, boundary)

| Row id | Exact command | Runner exit code | Key counts | Log / artifact path |
| --- | --- | --- | --- | --- |
| rc1-build | `dotnet build nextorm.slnx -c Debug` | exit 0 | 0 warnings / 0 errors | `artifacts/d127/rc1-127-computed-empty-1/build.log` |
| rc1-unit-ss | `dotnet test tests/nextorm.sqlserver.tests -c Debug` | exit 0 | total 697 / passed 697 / failed 0 / skipped 0 | `artifacts/d127/rc1-127-computed-empty-1/test-sqlserver.log` |
| rc1-unit-lite | `dotnet test tests/nextorm.sqlite.tests -c Debug` | exit 0 | total 1059 / passed 1058 / failed 0 / skipped 1 (pre-existing `SqliteRowIdLobProbeTests…MemoryBounded`) | `artifacts/d127/rc1-127-computed-empty-1/test-sqlite.log` |
| rc1-unit-pg | `dotnet test tests/nextorm.postgres.tests -c Debug` | exit 0 | total 771 / passed 771 / failed 0 / skipped 0 | `artifacts/d127/rc1-127-computed-empty-1/test-postgres.log` |
| rc1-unit-my | `dotnet test tests/nextorm.mysql.tests -c Debug` | exit 0 | total 284 / passed 284 / failed 0 / skipped 0 | `artifacts/d127/rc1-127-computed-empty-1/test-mysql.log` |
| rc1-e08-e09 (class) | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -class nextorm.integration.tests.SqlServerSpecificTests -noColor -result-xml artifacts/d127/rc1-127-computed-empty-1/sqlserver-class.xml` | exit 0 | total 91 / passed 91 / failed 0 / skipped 0 / not-run 0; E08 `SqlServerSpecificTests.cs:2130`, E09 `:2174/:2221/:2277` all Pass | `.../sqlserver-class.log`, `.../sqlserver-class.xml` |
| rc1-full | `DOCKER_HOST=... dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml artifacts/d127/rc1-127-computed-empty-1/integration.xml` | exit 0 | total 3195 / passed 3002 / failed 0 / skipped 193 / not-run 0; per-provider PG 778 (753/0/25), SQL Server 718 (675/0/43), MySQL 658 (579/0/79), SQLite 692 (648/0/44), ClickHouse 177 (177/0/0), MariaDB 50 (50/0/0), agnostic 122 (120/0/2) — all 5 required providers executed | `.../integration.log`, `.../integration.xml` |
| rc1-safe | `git diff --stat -- src` | exit 0 | empty (product code unchanged) | n/a |
| rc1-reg | full E01–E10 mapping: E01–E03 SQL-gen and E10 docs unchanged; E04–E07 cited lines unchanged (all <2127); E08/E09 newly covered | see above rows |  | parent `docs/specs/status/rc1-127-mssql-output-into-1.md:63-72` |

2026-10-06T00:00:00Z | CHECK | revision r=1 | iteration 1/3 | re-gather #2: execution manifest (exit codes + log paths) recorded for rv=1; guards/counters/E01-E10 mapping supplied; awaiting final CHECK

## ACT

- CHECK verdict: **PASS** — rv=1, r=1, n=1/3. Freeze point: the audited working tree (product code unchanged).
- Criteria: **D127-E08 closed** (INSERT empty-result set) and **D127-E09 closed** (computed source value through
  the `OutputInto` table-variable for INSERT/UPDATE/DELETE); parent rows updated to `closed`.
- **Product code unchanged** — `git diff --stat -- src` is empty; the only code edit is test-only
  (`SqlServerSpecificTests.cs`), defect `rc1-127-param-indexer-name` (1 fix, test-only).
- Disposition: residual E08/E09 criteria are satisfied by the new real-server integration tests; no plan
  change (r=1, n=1), no rejected candidate, no supersession of the parent D127 scope.
- Commit sha: `8ed865b4`.
- Evidence pointers: `artifacts/d127/rc1-127-computed-empty-1/` (`build.log`, `test-sqlserver.log`,
  `test-sqlite.log`, `test-postgres.log`, `test-mysql.log`, `sqlserver-class.log`, `sqlserver-class.xml`,
  `integration.log`, `integration.xml`); class run exit 0 Total 91 / Passed 91 / Failed 0 / Skipped 0;
  full integration exit 0 Total 3195 / Passed 3002 / Failed 0 / Skipped 193.

2026-10-06T00:00:00Z | ACT | revision r=1 | CHECK PASS (rv=1); E08/E09 closed; cycle complete
