# D182 CHECK packet - SQL Server scalar-function parity (#182)
- HEAD 3e3a462ec06839ae33c07713767f6ca2886e3b10 | branch 1.0.9-rc1 | N=1 r=2 n=1/3 | rv=D182.r2.ec1
- changed src+tests sha256 (10 files, concatenated content): 5e0f31b3d1e68ded161cc12a95dd9e4757b50e4a8319d5a022aaea5a563d421a

## Manifest disposition
61 audited = 51 covered + 10 excluded. Covered = 47 new (Date 10, Binary 4, Other 2, Metadata A10/B7/C5/D9) + 4 pre-existing (NEWID, GETDATE/GETUTCDATE, DATEFROMPARTS). Excluded E1 CURRENT_REQUEST_ID; E2 CURRENT_TRANSACTION_ID/XACT_STATE; E3 APP_NAME/HOST_ID/HOST_NAME; E4 IDENT_CURRENT; E5 MIN_ACTIVE_ROWVERSION; E6 ROWCOUNT_BIG; E7 SCOPE_IDENTITY. FROMPARTS 6 total/5 new; date 13 total/10 new.

## Load-bearing invocations (all exit 0 unless noted)
I1 `dotnet build nextorm.slnx -c {Debug|Release}` | I2 `dotnet test tests/<proj> -c Debug [--filter FullyQualifiedName~SqlServerFunctions]` | I3 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | I4 `dotnet-coverage collect ...` + `reportgenerator ...` | I5 `dotnet docfx docs/docfx.json` | I6 `git diff --check` / `git status --short`

## Contract rows D182-EC01..EC11 (all P1; CHECK may not downgrade)
| EC | scenario | applicability | invocation | achieved (exit + counts) | artifact | owner | DO binding (class.method file:line) |
| EC01 | manifest provenance/completeness | plan | static review | exit n/a; 61=51+10, no residual | docs/specs/status/rc1-182-mssql-scalars-1.md | planner | manifest rc1-182-mssql-scalars-1.md:34 |
| EC02 | architecture/guards (translator gate + capability + renderer) | all 47 + 4 | I2 | exit 0; 25 passed/0 failed/0 skipped | artifacts/d182/metadata-sqlserver.log | DO | SqlServerScalarFunctionTranslator.TryTranslate SqlServerScalarFunctionTranslator.cs:85 |
| EC03 | Date (10 new; 13 total) | SQL Server | I2 | exit 0; 25/0/0 (Date subset) | artifacts/d182/metadata-sqlserver.log | DO | SqlServerFunctions.sysdatetime SqlFunctions.SqlServer.cs:336 |
| EC04 | Binary checksum/compress (4) | SQL Server | I2 | exit 0; 25/0/0 (Binary subset) | artifacts/d182/metadata-sqlserver.log | DO | SqlServerFunctions.checksum SqlFunctions.SqlServer.cs:424 |
| EC05 | Other RAND/STUFF (2) | SQL Server | I2 | exit 0; 25/0/0 (Other subset) | artifacts/d182/metadata-sqlserver.log | DO | SqlServerFunctions.rand SqlFunctions.SqlServer.cs:458 |
| EC06 | Metadata A-D (31) | SQL Server | I2 | exit 0; 25/0/0 (Metadata A-D subset) | artifacts/d182/metadata-sqlserver.log | DO | SqlServerFunctions.col_length SqlFunctions.SqlServer.cs:489 |
| EC07 | provider gate matrix (rejection) | SQLite/PG/MySQL/MariaDB/CH | I2 per rejection project | exit 0 per project; sqlite 8, postgres 3, mysql 3, mariadb 3, clickhouse 3 passed/0 failed | artifacts/d182/metadata-reject-*.log | DO | AssertUnsupported postgres/SqlServerFunctionsRejectionTests.cs:102 |
| EC08 | container integration native semantics | all providers | I3 | exit 0; Total 3169, Errors 0, Failed 0, Skipped 193; SQL Server 655 pass/43 skip, PostgreSQL 751/25, MySQL 579/79, MariaDB 50/0, ClickHouse 173/0, SQLite 648/44; SQL Server 2025 (RTM-CU9) 17.0.5005.3 | artifacts/d182/integration.log, integration-results.xml, sqlserver-version.txt | DO | CommonTestSuite.CrossProviderScalarFunctions.cs:13 |
| EC09 | build/coverage | solution | I1 + I4 | Debug 0W/0E, Release 0W/0E; collect 0, report 0; overall line 87.1% / branch 78.8% | artifacts/d182/boundary-build-*.log, coverage-report/Summary.txt | DO | n/a (whole solution) |
| EC10 | docs + docfx | EN+RU docs/specs | I5 | exit 0; 0 errors, 2 pre-existing duplicate-source warnings, no new xref/link warnings | artifacts/d182/docfx.log | DO | docs/scalar-functions/03-date-and-time.md, 04-conditionals-and-conversion.md |
| EC11 | branch/CRLF/commit | repo | I6 | branch 1.0.9-rc1; CRLF preserved; no commit/push (19 modified + 6 untracked) | artifacts/d182/git-diff-check.txt, git-status-short.txt | DO | n/a |

## Criterion -> file:line
- Production: declarations SqlFunctions.SqlServer.cs:336-765 (Date :336-415, Binary :424-450, Other :458-480, Metadata :489-765); renderer SqlServerDialect.cs:971 (Supports), :991 (Render); translator SqlServerScalarFunctionTranslator.cs:85 (TryTranslate), :62 (Variadic), :72/:137 (Precision), :69/:105 (FORMATMESSAGE cap); capability DialectCapabilities.cs:596; hook ISqlDialect.cs:514.
- Tests positive: tests/nextorm.sqlserver.tests/SqlServerFunctionsSqlGenerationTests.cs:164-519 (Date/Binary/Other/Metadata), gate :521-545.
- Rejection: tests/nextorm.sqlite.tests/SqlServerFunctionsSqlGenerationTests.cs:73-156; tests/nextorm.{postgres,mysql,mariadb,clickhouse}.tests/SqlServerFunctionsRejectionTests.cs:17-100.
- Integration: tests/nextorm.integration.tests/CommonTestSuite.CrossProviderScalarFunctions.cs:13 + full container suite (SQL Server 2025 CU9).

## Evidence numbers
- Build: Debug exit 0 (0W/0E); Release exit 0 (0W/0E) [boundary-build-debug.log, boundary-build-release.log].
- Unit Debug (total/succeeded/failed/skipped): core 1547/1547/0/0; sqlserver 581/581/0/0; postgres 770/770/0/0; mysql 283/283/0/0; mariadb 191/191/0/0; sqlite 1058/1057/0/1; clickhouse 501/501/0/0 [boundary-unit-*.log].
- Container integration: exit 0; 3169 total, 0 errors, 0 failed, 193 skipped; providers all executed (skip != pass) [integration.log, integration-results.xml]; SQL Server version [sqlserver-version.txt].
- Coverage: collect exit 0, report exit 0; line 87.1% / branch 78.8% (>=85/75); changed files: SqlServerDialect.cs line 97.2% (1058/1088) branch 90.6% (540/596); SqlServerScalarFunctionTranslator.cs line 95.7% (178/186) branch 93.5% (86/92); SqlFunctions.SqlServer.cs 0/204 (declaration-only stubs) [coverage-collect.log, coverage-report/Summary.txt, coverage.cobertura.xml].
- Docs: docfx exit 0 [docfx.log]. Hygiene: git diff --check exit 0 [git-diff-check.txt]; git status --short = 19 modified + 6 untracked [git-status-short.txt].

## Priority / deferred / defects
- Priority: EC01..EC11 all P1 (CHECK may not downgrade); no downgrade applied.
- Deferred: `CHECKSUM(*)` wildcard; extra SQL types.
- Defect history: none (no CHECK failures / fixes applied; r=2, n=1/3). Follow-ups: none.
