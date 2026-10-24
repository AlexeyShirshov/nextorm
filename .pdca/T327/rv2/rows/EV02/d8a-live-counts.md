# D8a live counts (r=3, rv3) - COMPLETE

Product-fix commits:
- `#327 D3: render inline char constants through the string policy`
- `#327 D4: record select-list parameter occurrences`
- `#327 D4: normalize ClickHouse char parameters`

Command (each run, sequential, under the collection lease):
`flock /tmp/opencode/nextorm-container-lease.lock -c 'DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- <selectors> -noColor'`
No `NEXTORM_SKIP_PROVIDERS`.

| Suite | selected | passed | failed | skipped | log |
|---|---|---|---|---|---|
| SqliteIntegrationTests `-trait Issue=327` | 10 | 10 | 0 | 0 | D8a-sqlite.log |
| PostgresIntegrationTests `-trait Issue=327` | 10 | 10 | 0 | 0 | D8a-postgres.log |
| SqlServerIntegrationTests `-trait Issue=327` | 10 | 10 | 0 | 0 | D8a-sqlserver.log |
| MySqlIntegrationTests `-trait Issue=327` | 10 | 10 | 0 | 0 | D8a-mysql.log |
| ClickHouseStringLiteralSafetyExecutionTests | 3 | 3 | 0 | 0 | D8a-clickhouse.log |
| MySqlStringLiteralSafetyExecutionTests (12 configs) | 12 | 12 | 0 | 0 | D8a-mysql-modes.log |
| MariaDbStringLiteralSafetyExecutionTests (12 configs) | 12 | 12 | 0 | 0 | D8a-mariadb.log |

MariaDB targeted count: **12 selected / 12 passed / 0 failed / 0 skipped** (fail-closed, zero skips).

## Inline char correction - live proof (D3 fix)

The D3 fix routes a CLR `char` through `RenderStringLiteral` in
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs` (`TryEmitValue`). Live results:

- Common suite (SQLite/Postgres/SQL Server/MySQL): inline `Select(e => <char>)` runs the full char set;
  PostgreSQL `default(char)` throws `NotSupportedException` (`*PostgreSQL*U+0000*`); the other
  providers return the exact char.
- ClickHouse: inline and captured char exact (`'a'`, `'\''`, `'\\'`, `'\x00'`, `'\x01'`).
- Shape level: six `InlineStringLiteralSqlGenerationTests` classes green
  (sqlite 25, sqlserver 25, postgres 24, mysql 26, mariadb 24, clickhouse 25; 0 failed/0 skipped).

## Resolved defects (details and original findings in d8a-blocker.md)

1. Select-list-only captured text parameters are now recorded in the statement template and
   MySQL/MariaDB lower them to `_utf8mb4 X'...'` (full 12/12 matrix green, EV03).
2. ClickHouse `char` parameters are normalized to a one-character string at every driver assignment
   (cold bind and refresh), so the captured-char cells are green (EV02/d8a-clickhouse-char.md).
## D8b full fail-closed boundary (r=3, single run)

One canonical full-suite run, serialized under the collection container lease, with no `NEXTORM_SKIP_PROVIDERS`: Total 5352, Failed 7, Skipped 197, Not Run 0, exit 1; skip budget 197/197 (delta 0). Log `.pdca/T327/rv2/logs/D8b-full-boundary.log`, details in `.pdca/T327/rv2/rows/EV09/d8b-boundary.json`. The boundary is red: 2 product regressions (DEF-T327-03, DEF-T327-04), 4 stale expectations under the declared representation (DEF-T327-06), 1 pre-existing unrelated SQLite failure (DEF-T327-05).

The seven D8a targeted classes emitted no `[FAIL]`/`[SKIP]` line in the full run, so the matrix cells counted above stand; EV02 stays open until the boundary is green.
