# D182 — SQL Server scalar-function parity (metadata / date-part / checksum)

- task: D182
- issue: #182 (https://github.com/AlexeyShirshov/nextorm/issues/182)
- collection: 1.0.9-rc1
- group: G01
- branch: 1.0.9-rc1
- cycle: N=1
- plan revision: r=2
- attempt: n=1
- mode: autonomous + auto-commit; no push/merge

## Goal

SQL Server scalar-function parity with linq2db (metadata / date-part / checksum families).

## Reconnaissance (persisted; not yet re-verified)

- Issue #182 "TODO: SQL Server scalar-function parity with linq2db (metadata / date-part / checksum families)"; OPEN; enhancement; milestone 1.0.9-rc1 (#19); no comments/PRs. Body residual ~61 of linq2db's 121; families metadata 41, date 13, binary/checksum 4, other 3. Proposed scope: add to `SqlServerFunctions`/`CommonFunctions`, capability-gate per name, translator+SQL-gen/integration tests, docs EN+RU, sync comparison spec.
- Surface: `src/nextorm.core/Query/SqlFunctions.SqlServer.cs:15` (`SqlServerFunctions : CommonFunctions`, 328 lines, 40 members; families JSON/XML/table/T-SQL scalars/trig/date/binary/system/SQL-JSON); `src/nextorm.sqlserver/SqlServerDialect.cs:962` (`SqlServerSpecificFunctions`, SupportedNames :967-972, Render :975-1003); `SqlServerScalarFunctions` :910 (cross-provider names :915-948); hook `:696`. No `SqlServerFunctionRenderer.cs`.
- Gaps (issue): metadata 41 (`COL_LENGTH`,`OBJECT_ID`,`SCHEMA_NAME`,`NEWID`,`SCOPE_IDENTITY`,`ISDATE`,`ISNUMERIC`,`STR`,`XACT_STATE`,…); date 13 (`GETDATE`,`GETUTCDATE`,`SYSDATETIME`,`SYSDATETIMEOFFSET`,`SYSUTCDATETIME`,`SWITCHOFFSET`,`TODATETIMEOFFSET`, `*FROMPARTS`×7); binary 4 (`CHECKSUM`,`BINARY_CHECKSUM`,`COMPRESS`,`DECOMPRESS`); other 3 (`APP_NAME`,`RAND`,`STUFF`).
- Contradictions/overlaps (effective residual < 61): `DATEFROMPARTS` `SqlFunctions.cs:707`; `DateTime.Now/UtcNow`→`getdate()/getutcdate()` `MemberTranslator.cs:22-26`+`SqlServerDialect.cs:554`; `gen_random_uuid`→`newid()` `SqlServerDialect.cs:690`; `CommonFunctions.current_user/current_schema/current_database/version` `SqlFunctions.cs:422-450`.
- Pattern: declaration `SqlFunctions.SqlServer.cs:144` (analog `patindex`); renderer `SupportedNames` `SqlServerDialect.cs:967` + `Render` `:975`; translator `src/nextorm.core/Visitors/SqlServerScalarFunctionTranslator.cs:21,44,51,73`; capability `DialectCapabilities.cs:592`, hook `ISqlDialect.cs:514`. Attribute-less per-name `Supports`/`Render`.
- Tests: `tests/nextorm.sqlserver.tests/SqlServerFunctionsSqlGenerationTests.cs:7` (8 tests), `SqlServerDialectTests.cs:329`, `CrossProviderScalarSqlGenerationTests.cs:7`; rejection `SqliteFunctionsRejectionTests.cs:11`,`MySqlFunctionsRejectionTests.cs:7`; integration `CommonTestSuite.CrossProviderScalarFunctions.cs:13`.
- Docs: `docs/scalar-functions/{04:70,07:89-157,index.md:27}` + RU; `docs/providers/sqlserver.md:108-142,279`; `docs/guide/provider-specific/sqlserver.md:14-100,231` + RU; specs `comparison/provider-feature-comparison.md:102-123`, `capability-matrix.md:83`, `evidence-01-provider-specific-features.md:120`; roadmap `sql-function-coverage-gap.md:57,171,182`, `sql-capabilities-gap-analysis.md:257,554`.
- Open design questions for PLAN: exact function list/scope (all 61 vs prioritised families); per-function capability gating; which functions are already covered by non-scalar paths; docs/tests matrix; whether integration execution needs the SQL Server container.

## Plan (revision r=2, attempt n=1)

- Evidence contract: `rv=D182.r2.ec1` (supersedes `rv=D182.r1.ec1`).
- Decision: escalate option A — issue #182 closes only when all 61 pinned-manifest entries are `covered` or `excluded` with a recorded reason.
- Durable state: cycle N=1, plan revision r=2, attempt n=1; defect history: none (no CHECK failures/fixes applied yet).

### Pinned manifest (61 entries)

- Provenance: linq2db v6.5.0, commit `47ed37b1db6c58eaf2cfd8a7fbe6aac294aae305`, `Source/LinqToDB/DataProvider/SqlServer/SqlFn.cs`.
- Counts: metadata 41, date 13 (5 clock + 2 offset + 6 FROMPARTS), binary/checksum 4, other 3 = 61.
- Disposition: **4 covered + 47 to-add + 10 excluded** → target **51 covered + 10 excluded**.

Covered (4):
- `NEWID` — `gen_random_uuid`→`newid()`, `SqlServerDialect.cs:690`; tests `SqlGenerationTests.cs:323-325`.
- `GETDATE`/`GETUTCDATE` — `MemberTranslator.cs:22-26`; tests `:1258-1259`.
- `DATEFROMPARTS` — `SqlFunctions.cs:707`; test `:2476`.

Excluded (10, E1–E7) — connection/session/server or statement-scope state, not per-row query scalars; "excluded" is an API-scope decision, not a claim they cannot run in SELECT; each has a review trigger:
- E1: `CURRENT_REQUEST_ID`.
- E2: `CURRENT_TRANSACTION_ID`, `XACT_STATE`.
- E3: `APP_NAME`, `HOST_ID`, `HOST_NAME`.
- E4: `IDENT_CURRENT`.
- E5: `MIN_ACTIVE_ROWVERSION`.
- E6: `ROWCOUNT_BIG`.
- E7: `SCOPE_IDENTITY`.

to-add (47):
- Date (10): `SYSDATETIME`, `SYSDATETIMEOFFSET`, `SYSUTCDATETIME`, `SWITCHOFFSET`, `TODATETIMEOFFSET`, `TIMEFROMPARTS`, `SMALLDATETIMEFROMPARTS`, `DATETIMEFROMPARTS`, `DATETIME2FROMPARTS`, `DATETIMEOFFSETFROMPARTS`.
- Binary (4): `CHECKSUM`, `BINARY_CHECKSUM`, `COMPRESS`, `DECOMPRESS`.
- Other (2): `RAND`, `STUFF`.
- Metadata A (10): `COL_LENGTH`, `COL_NAME`, `IDENT_INCR`, `IDENT_SEED`, `INDEX_COL`, `OBJECT_DEFINITION`, `OBJECT_ID`, `OBJECT_NAME`, `OBJECT_SCHEMA_NAME`, `STATS_DATE`.
- Metadata B (7): `DB_ID`, `DB_NAME`, `ORIGINAL_DB_NAME`, `SCHEMA_ID`, `SCHEMA_NAME`, `TYPE_ID`, `TYPE_NAME`.
- Metadata C (5): `FILEGROUP_ID`, `FILEGROUP_NAME`, `FILE_ID`, `FILE_IDEX`, `FILE_NAME`.
- Metadata D (9): `CURRENT_TIMEZONE`, `CURRENT_TIMEZONE_ID`, `FORMATMESSAGE`, `GETANSINULL`, `ISDATE`, `ISNUMERIC`, `PARSENAME`, `PUBLISHINGSERVERNAME`, `STR`.

### DO units (sequential, one tree)

- D182.1 lock (Roslyn discovery)
- D182.2 Date
- D182.3 Binary
- D182.4 Other
- D182.5 Metadata A
- D182.6 Metadata B
- D182.7 Metadata C
- D182.8 Metadata D
- D182.9 boundary/docs
- D182.10 CHECK/finish

Sites: declaration `SqlFunctions.SqlServer.cs:16-144` (analog `patindex`); translator `SqlServerScalarFunctionTranslator.cs:21,44,51,73`; renderer `SqlServerDialect.cs:962,967-972,975-1003,1006`; capability `DialectCapabilities.cs:592`, hook `ISqlDialect.cs:514`; tests `SqlServerFunctionsSqlGenerationTests.cs:7`, `SqlServerDialectTests.cs:329`, `CrossProviderScalarSqlGenerationTests.cs:7`, integration `CommonTestSuite.CrossProviderScalarFunctions.cs:13`.

### Test strategy / matrix

- Inner: core + sqlserver unit/SQL-gen (arity/type/null/precision/offset/nondeterminism); each of the 47 gets a positive SQL Server test + a negative pre-execution `NotSupportedException` test for every other dialect (SQLite/PG/MySQL/MariaDB/CH) + in-memory.
- Boundary: full solution + container integration (`DOCKER_HOST` per `.opencode/skills/running-integration-tests/SKILL.md`; skip ≠ pass).
- Coverage 85/75; `coverage.settings.xml` includes sqlserver.
- Variant matrix per family: clocks, offset, FROMPARTS, checksum varargs, COMPRESS/DECOMPRESS bytes, RAND seeded/unseeded, STUFF string/bytes, metadata A–D missing/null/optional, sql_variant/XML/UDT guard, 10 exclusions no dedicated wrapper.
- Deferred: `CHECKSUM(*)` wildcard; extra SQL types.

### Priority

- All D182-EC01..EC11 P1; CHECK may not downgrade.

### Docs

- `docs/scalar-functions/{04:70,07:89-157,index.md:27}` + RU; `docs/providers/sqlserver.md:108-142,279`; `docs/guide/provider-specific/sqlserver.md:14-100,231` + RU; specs `comparison/provider-feature-comparison.md:102-123`, `capability-matrix.md:83`, roadmap `sql-function-coverage-gap.md`, `sql-capabilities-gap-analysis.md:257,554` — record 61 audited = 51 covered + 10 excluded (FROMPARTS 6 total/5 new; date 13 total/10 new). No public→specs links.

### Perf / recon

- Perf: not required (declaration/translation/rendering only; flip condition = any general lookup/allocation/client per-row change).
- Recon: no design spike; D182.1 Roslyn discovery + container native semantics are mandatory fact verification.

### Evidence contract `rv=D182.r2.ec1`

- Rows D182-EC01 manifest provenance/completeness; EC02 architecture/guards; EC03 Date; EC04 Binary; EC05 Other; EC06 Metadata A–D; EC07 provider gate matrix; EC08 container integration native semantics; EC09 build/coverage; EC10 docs+docfx; EC11 branch/CRLF/commit.
- Artifacts under `artifacts/d182/` (repo-local; do not commit).
- CHECK re-gather budget 2, owner CHECK.
- Assumptions/blockers: CLR mappings for TimeSpan/DateTimeOffset; SQL Server 2025 container metadata/rights; manifest dedup residual count.

## Progress log

- Recon persisted (run 3 start).

- Notice: host has no todowrite tool; this file is the progress log.

- PLAN r=2 locked; rv=D182.r2.ec1; DO next.

- 2026-10-05T23:06Z | DO | r2 | n1/3 | D182.5-D182.8 metadata A-D implemented (31 names; declarations, per-name Supports/Render, translator names, formatmessage 1-leading flatten + <=20 arg guard) | src/nextorm.core/Query/SqlFunctions.SqlServer.cs, src/nextorm.sqlserver/SqlServerDialect.cs, src/nextorm.core/Visitors/SqlServerScalarFunctionTranslator.cs, src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs
- 2026-10-05T23:06Z | DO | r2 | n1/3 | build nextorm.slnx -c Debug: 0 warnings / 0 errors (exit 0) | artifacts/d182/build-metadata.log
- 2026-10-05T23:06Z | DO | r2 | n1/3 | SQL-gen SqlServerFunctions: 25 passed / 0 failed / 0 skipped (exit 0) | artifacts/d182/metadata-sqlserver.log
- 2026-10-05T23:06Z | DO | r2 | n1/3 | rejection SqlServerFunctions: sqlite 8, postgres 3, mysql 3, mariadb 3, clickhouse 3 passed / 0 failed (all exit 0) | artifacts/d182/metadata-reject-sqlite.log, artifacts/d182/metadata-reject-postgres.log, artifacts/d182/metadata-reject-mysql.log, artifacts/d182/metadata-reject-mariadb.log, artifacts/d182/metadata-reject-clickhouse.log
- 2026-10-05T23:06Z | DO | r2 | n1/3 | defect history: none (no CHECK failures/fixes applied) | -
- 2026-10-05T23:06Z | DO | r2 | n1/3 | DO stream complete; next stream D182.9 boundary/docs | rv=D182.r2.ec1

## Boundary D182.9 (run 2026-10-05T23:11Z)

Manifest disposition: 61 audited = 51 covered (47 to-add now covered + 4 already covered) + 10 excluded (E1–E7). Target 51 covered + 10 excluded met on DO; CHECK verifies.

- 2026-10-05T23:11Z | DO | r2 | n1/3 | boundary build nextorm.slnx -c Debug: 0W/0E (exit 0); -c Release: 0W/0E (exit 0) | artifacts/d182/boundary-build-debug.log, artifacts/d182/boundary-build-release.log
- 2026-10-05T23:11Z | DO | r2 | n1/3 | full unit projects Debug (succeeded/failed/skipped): core 1547/0/0 (exit 0), sqlserver 581/0/0 (exit 0), postgres 770/0/0 (exit 0), mysql 283/0/0 (exit 0), mariadb 191/0/0 (exit 0), sqlite 1057/0/1 (exit 0), clickhouse 501/0/0 (exit 0) | artifacts/d182/boundary-unit-*.log
- 2026-10-05T23:11Z | DO | r2 | n1/3 | container integration exit 0: Total 3169, Errors 0, Failed 0, Skipped 193; provider executed/skipped: SQL Server 655/43, PostgreSQL 751/25, MySQL 579/79, MariaDB 50/0, ClickHouse 173/0, SQLite 648/43; all container providers ran (skip != pass); SQL Server container mcr.microsoft.com/mssql/server:2025-latest = Microsoft SQL Server 2025 (RTM-CU9) 17.0.5005.3 | artifacts/d182/integration.log, artifacts/d182/integration-results.xml, artifacts/d182/integration-exit.txt, artifacts/d182/sqlserver-version.txt
- 2026-10-05T23:11Z | DO | r2 | n1/3 | coverage collect exit 0, reportgenerator exit 0: overall line 87.1% / branch 78.8% (>= 85/75); changed files: SqlServerDialect.cs line 97.2% (1058/1088) branch 90.6% (540/596), SqlServerScalarFunctionTranslator.cs line 95.7% (178/186) branch 93.5% (86/92), SqlFunctions.SqlServer.cs line 0.0% (0/204; declaration-only translated stubs, no executable body — same as CommonFunctions/SqlFunctions) | artifacts/d182/coverage-collect.log, artifacts/d182/coverage-report/Summary.txt, artifacts/d182/coverage.cobertura.xml
- 2026-10-05T23:11Z | DO | r2 | n1/3 | git diff --check exit 0 (no whitespace errors); working tree = 19 modified + 6 untracked (DO docs/tests + artifacts/) | artifacts/d182/git-diff-check.txt, artifacts/d182/git-status-short.txt
- 2026-10-05T23:11Z | DO | r2 | n1/3 | defect history: none (no CHECK failures/fixes applied) | -
- 2026-10-05T23:11Z | DO | r2 | n1/3 | D182.9 boundary complete; D182.10 CHECK/finish next | rv=D182.r2.ec1

- 2026-10-05T23:11Z | DO | r2 | n1/3 | D182.9 docs: EN+RU scalar-functions 03 (clock/offset/`*FROMPARTS`) + 04 (checksum/`RAND`/`STUFF`/`STR`/`ISDATE`/`ISNUMERIC`/`FORMATMESSAGE` + metadata A-D + 10 excluded), provider sqlserver EN+RU, guide provider-specific sqlserver EN+RU, specs provider-feature-comparison/capability-matrix/sql-function-coverage-gap/sql-capabilities-gap-analysis (61 audited = 51 covered + 10 excluded; FROMPARTS 6 total/5 new; date 13 total/10 new); docfx exit 0 (0 errors, 2 pre-existing duplicate-source warnings, no new xref/link warnings) | artifacts/d182/docfx.log
- 2026-10-06T04:14Z | CHECK | r2 | n1/3 | CHECK gather: slop ratio introduced 63/1344 = 4.69% (all `default!` declaration stubs; effective 0) + consolidated packet EC01-EC11, manifest 61=51+10 | artifacts/d182/check/slop-scan.txt, artifacts/d182/check/D182-CHECK-packet.md

## Defect history D182-c1 (fixed)

- D182-c1 (r=2, n=1; observed r=2, n=1; fixes applied: 1): SQL Server `STUFF` returns `varchar` for binary arguments (verified on the SQL Server 2025 container); the invented `byte[] stuff(byte[], long, long, byte[])` overload was invalid → removed; parity with linq2db is string-only. The failed case was `Stuff_ShouldMatchNativeForStringAndBytes` (`InvalidCastException: String → Byte[]`); after the fix the case is `Stuff_ShouldMatchNativeForString` and passes. Evidence: `artifacts/d182/fix-build.log`, `artifacts/d182/fix-core.log`, `artifacts/d182/integration2.log`.

- 2026-10-05T23:33Z | DO | r2 | n1/3 | defect D182-c1 FIXED (fixes applied: 1; invented byte[] STUFF overload removed; string-only parity) | artifacts/d182/fix-build.log, artifacts/d182/fix-core.log, artifacts/d182/integration2.log
- 2026-10-05T23:33Z | DO | r2 | n1/3 | build nextorm.slnx -c Debug: 0W/0E (exit 0); core 1548/0/0; sqlserver ~SqlServerFunctions 28/0/0; rejection sqlite 8, postgres 3, mysql 3, mariadb 3, clickhouse 3 (all 0 failed, exit 0) | artifacts/d182/fix-build.log, artifacts/d182/fix-core.log, artifacts/d182/fix-sqlserver-functions.log, artifacts/d182/fix-reject-*.log
- 2026-10-05T23:33Z | DO | r2 | n1/3 | container integration exit 0: Total 3179, Errors 0, Failed 0, Skipped 193; all 10 issue-182 cases executed+passed; SQL Server 2025 RTM-CU9 17.0.5005.3 | artifacts/d182/integration2.log, artifacts/d182/integration2-results.xml, artifacts/d182/sqlserver-version.txt
- 2026-10-05T23:33Z | DO | r2 | n1/3 | manifest 61 = 51 covered + 10 excluded (E1 CURRENT_REQUEST_ID; E2 CURRENT_TRANSACTION_ID/XACT_STATE; E3 APP_NAME/HOST_ID/HOST_NAME; E4 IDENT_CURRENT; E5 MIN_ACTIVE_ROWVERSION; E6 ROWCOUNT_BIG; E7 SCOPE_IDENTITY) | docs/specs/status/rc1-182-mssql-scalars-1.md:34
- 2026-10-05T23:33Z | DO | r2 | n1/3 | D182-c1 fix complete; final packet /tmp/d182-final-packet.md for CHECK | rv=D182.r2.ec1

## ACT (2026-10-06T04:40Z)

- Freeze point: CHECK PASS at r=2, n=1/3, rv=D182.r2.ec1; no new plan revision; cycle N=1 closed.
- CHECK verdict: PASS on evidence contract rows EC01–EC11 (consolidated packet `artifacts/d182/check/D182-CHECK-packet.md`); slop ratio introduced 63/1344 = 4.69% (all `default!` declaration-only stubs; effective 0).
- Manifest: 61 audited = 51 covered + 10 excluded (E1–E7: connection/session/server or statement-scope state, not per-row query scalars, each with a review trigger).
- Defect closure: D182-c1 CLOSED — 1 fix applied; the invented `byte[] STUFF` overload was removed (string-only parity with linq2db); regression case `Stuff_ShouldMatchNativeForString` passes.
- Commit plan: one D182 change set committed on branch `1.0.9-rc1`; collection status updated in a separate bookkeeping commit; no push, no merge.
- Issue outcome: #182 closed via `gh` with the 51-covered/10-excluded summary; commit unpushed.
- 2026-10-06T04:40Z | ACT | r2 | n1/3 | CHECK PASS frozen; D182 commit + collection bookkeeping + issue close | rv=D182.r2.ec1
