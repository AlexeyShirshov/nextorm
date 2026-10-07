# rc1-133-streaming-lob-1

- Task: D133
- Issue: #133 (https://github.com/AlexeyShirshov/nextorm/issues/133)
- Collection: 1.0.9-rc1
- Group: G01
- Branch: 1.0.9-rc1
- Cycle: N=1
- Plan revision: r=2
- Attempt: n=1
- Mode: autonomous + auto-commit
- Push/merge: none
- Evidence contract: rv=D133.r2.ec1

## Goal

Streaming LOB: `ToStream`/`ToTextReader` for MySQL/MariaDB and ClickHouse (sequential access
where the driver allows it); acceptance per issue = tests + EN/RU docs.

## Reconnaissance (persisted; not yet re-verified)

- Issue #133 "Streaming LOB: MySQL/MariaDB и ClickHouse"; OPEN; enhancement; milestone 1.0.9-rc1;
  no PRs/comments. Body: `ToStream`/`ToTextReader` work for PG/SQL Server/SQLite; MySQL/MariaDB and
  ClickHouse throw `NotSupportedException` (no sequential access); wants sequential access where the
  driver allows; AC tests + EN/RU. References `docs/guide/30-large-objects.md` (stale; actual
  `docs/guide/26-large-objects.md`) and `ISqlDialect.SupportsSequentialAccess`.
- Surface: `QueryCommandExtensions.cs:96` `ToStream(QueryCommand<byte[]>)`, async `:124,:133`,
  named-column `:212`; `ToTextReader(QueryCommand<string>) :154`, async `:182,:191`.
  `ISqlDialect.cs:683` `SupportsSequentialAccess => false` (DIM); `SqlDialectBase.cs:223` virtual
  false; `ISqlDialect.cs:692` `LobLocatorColumn => null` (SQLite rowid). Opt-ins:
  `PostgresDialect.cs:51`, `SqlServerDialect.cs:30`, `SqliteDialect.cs:15` (+locator `:18`). **No
  override in MySql/MariaDb/ClickHouse dialects.** Streaming terminals are a `QueryCommand`
  extension surface; **no `EntityBuilder<TEntity>` streaming terminals found**.
- Implementation path: gate `DataContext.cs:433-435` (throws when `!SupportsSequentialAccess`,
  message names PG/MSSQL/SQLite); second gate `QueryCommandExtensions.cs:480-484`; open path
  `DataContext.cs:415-427`; `QueryExecutor.cs:404-420,428-444`; ownership wrappers `LobStream.cs:8`,
  `LobTextReader.cs:8`, `LobDataReader.cs:18`; SequentialAccess bit
  `DbPreparedQueryCommand.cs:93-94`, `PreparedCommandOptions.cs:20`; locator `SqlBuilder.cs:527`;
  streaming prep gate `QueryPlanner.cs:570,583`; CT `DataContext.cs:417,424`,
  `QueryCommandExtensions.cs:141,146,199,204`; in-memory fallback
  `QueryCommandExtensions.cs:110-114,138-143,168-172,196-201`.
- **Why missing:** MySQL/MariaDB/ClickHouse inherit the `false` default — no per-provider override
  and no finer flag than `SupportsSequentialAccess`. MySqlConnector 2.6.2
  (`Directory.Packages.props:29`) supports `GetStream`/`GetTextReader` under `SequentialAccess`
  (`LobCapabilityProbeTests.cs:8-30,143-157,186,435,494`). ClickHouse.Driver 1.4.0
  (`Directory.Packages.props:9`): **driver streaming support not found / unconfirmed** — likely a
  blocker for ClickHouse. MariaDB inherits `MySqlDialect` (`MariaDbDialect.cs:11`).
- Tests: `CommonTestSuite.Lob.cs:14` (skip predicates `RequireLobStreaming:28-31`,
  `RequireLobUnsupported:33-36`; tests `:38,:50,:82,:92`); provider flags `MySqlTestProvider.cs:53`
  false, `ClickHouseTestProvider.cs:51` false, PG/SQLServer/SQLite true, declaration
  `ITestProvider.cs:150,157`; ClickHouse refusal `ClickHouseIntegrationTests.cs:1374-1399`; core
  `LobStreamingTests.cs:13`, `LobDataReaderTests.cs:16,76,83,134`, `LobWrappersTests.cs:12`; SQLite
  `SqliteLobGuardTests.cs:17`, `LobSqlGenerationTests.cs:20`, `SqliteRowIdLobProbeTests.cs:34`;
  probes `LobCapabilityProbeTests.cs:31` (skip unless `NEXTORM_LOB_PROBE=1`),
  `LobPerfHarnessTests.cs:16` (skip unless `NEXTORM_LOB_PERF=1`).
- Docs EN/RU: `docs/guide/26-large-objects.md:114-116,118-129,131,191,193-205` +
  `docs/ru/guide/26-large-objects.md:111-113,115-126,128-139`;
  `docs/guide/28-streaming-data.md:77`. Specs
  `roadmap/design-review-todos-2026-09-24.md:71,106` (todo_streaming_lob removed, #27),
  `design/code-smells-review.md:5364,7972` (MySQL/MariaDB GetStream/GetTextReader capability open).
- **Contradictions:** status pointer to this file existed but the file was absent; issue cites stale
  `30-large-objects.md`; `CommonTestSuite.Lob.cs:9-12` header says "only PG and SQL Server" but
  SQLite is supported (stale).
- Open PLAN questions: MySQL/MariaDB scope (sync+async, `ToStream`+`ToTextReader`); ClickHouse
  feasibility (driver streaming unconfirmed → likely deferred/blocked); the per-provider capability
  flag/override; docs EN+RU; tests/probes (real containers); whether `ToDataReader` is in scope.

## Plan r=2 (replan from r=1)

- Decision: #133 closes as a **documented limitation**. No capability is enabled.
- Rationale (measurement-backed, see probe results): neither MySQL/MariaDB (`MySqlConnector 2.6.2`)
  nor ClickHouse (`ClickHouse.Driver 1.4.0`) provides memory-bounded LOB streaming. MySQL/MariaDB
  `GetStream`/`GetTextReader` buffer the whole value and `SequentialAccess` does not change
  allocations; ClickHouse `GetStream` throws `NotImplementedException` and `GetTextReader` buffers.
  Therefore `SupportsSequentialAccess` (dialects) and provider `SupportsLobStreaming` stay **false**
  for MySQL/MariaDB/ClickHouse — that is the correct value, not an omission.
- Evidence contract: `rv=D133.r2.ec1` — every acceptance row below must cite a log path under
  `/tmp/nextorm-D133/` or a `file:line` in the tree; no assertion without a pointer.

### Probe results (persisted)

| Probe | Driver / server | Result |
|---|---|---|
| Standalone `D133Probe` (1→4 MiB) `/tmp/nextorm-D133/probe.log` | MySqlConnector 2.6.2 / MySQL 8.4.11 | BLOB `GetStream` buffered ratio **4.00**; CLOB `GetTextReader` buffered **4.00** |
| Standalone `D133Probe` (1→4 MiB) `/tmp/nextorm-D133/probe.log` | MySqlConnector 2.6.2 / MariaDB 11.4.13-MariaDB | BLOB `GetStream` buffered **4.00**; CLOB `GetTextReader` buffered **4.00** |
| In-repo `LobCapabilityProbeTests` (1→8 MiB) `/tmp/nextorm-D133/lob-capability-probe.log` | MySqlConnector 2.6.2 (mysql/mariadb) | `GetStream` seq on/off buffered **7.99**; `GetTextReader` seq on/off buffered **8.00** |
| Standalone `D133Probe` `/tmp/nextorm-D133/probe-clickhouse.log` | ClickHouse.Driver 1.4.0 / server 25.8.33.6 | BLOB `GetStream` **`System.NotImplementedException`**; CLOB `GetTextReader` buffered **3.99** |

### Acceptance criteria

- **R133-01** Probe evidence persisted (ratios/versions above); no driver shown memory-bounded.
- **R133-02** `LobCapabilityProbeTests.cs` no longer claims "memory-bounded"; names + XML-doc state
  the real buffered behavior and the probe cannot be read as evidence to set `SupportsLobStreaming=true`.
- **R133-03** Capability flags unchanged: MySQL/MariaDB/ClickHouse dialects inherit
  `SupportsSequentialAccess => false`; test providers keep `SupportsLobStreaming => false`.
- **R133-04** EN + RU `guide/26-large-objects.md` state drivers buffer, why the flag is deliberately
  false, and the buffered alternative; PG/SQL Server/SQLite remain supported.
- **R133-05** `CommonTestSuite.Lob.cs` header corrected (SQLite included); spec registry
  (`code-smells-review.md`) records the measurement-backed disposition.
- **R133-06** Build `0` warnings/`0` errors; probe run green with counts; `git diff --check` clean;
  all edited files CRLF.

### DO units

- D133-01 probe-claim correction + buffered-guard (`tests/nextorm.integration.tests/LobCapabilityProbeTests.cs`).
- D133-02 EN guide 26 (`docs/guide/26-large-objects.md`).
- D133-03 RU guide 26 (`docs/ru/guide/26-large-objects.md`).
- D133-04 `CommonTestSuite.Lob.cs` header.
- D133-05 spec registry (`docs/specs/design/code-smells-review.md`).
- D133-06 status file + evidence rows.
- D133-07 verification (build / probe / `git diff --check` / CRLF).

### Evidence rows

- **E133-01** `/tmp/nextorm-D133/probe.log:3-9` — MySQL/MariaDB standalone ratio 4.00 (MySqlConnector 2.6.2).
- **E133-02** `/tmp/nextorm-D133/lob-capability-probe.log:3-18,21-36` — in-repo ratio 7.99/8.00.
- **E133-03** `/tmp/nextorm-D133/probe-clickhouse.log:11-12` + `run-clickhouse.log:23-24` — ClickHouse `GetStream` NotImplementedException, `GetTextReader` 3.99.
- **E133-04** `tests/nextorm.integration.tests/LobCapabilityProbeTests.cs` diff (D133-01).
- **E133-05** `docs/guide/26-large-objects.md`, `docs/ru/guide/26-large-objects.md`, `tests/nextorm.integration.tests/CommonTestSuite.Lob.cs` diffs.
- **E133-06** `docs/specs/design/code-smells-review.md` disposition (D133-05).
- **E133-07** `/tmp/nextorm-D133/build.log` (0/0), `/tmp/nextorm-D133/run-lobprobe-r2.log` (exit + counts), `git diff --check`, CRLF check.

### CHECK re-gather budget

`CHECK may re-gather evidence at most twice (budget = 2).`

## DO verification (r=2)

- **Build** `/tmp/nextorm-D133/build.log`: `dotnet build nextorm.slnx -c Debug` — exit **0**,
  **0 Warning(s) / 0 Error(s)** (all 19 projects built).
- **Probe** `/tmp/nextorm-D133/run-lobprobe-r2.log`: `dotnet test tests/nextorm.integration.tests
  -c Debug --no-build --filter "FullyQualifiedName~LobCapabilityProbeTests"` with `NEXTORM_LOB_PROBE=1`
  and `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` — exit **0**,
  **total 2 / failed 0 / succeeded 2 / skipped 0**. Verdicts stay `buffered` (ratio 7.99/8.00), so the
  new guard passes and the probe cannot be read as memory-bounded. Log
  `/tmp/nextorm-D133/lob-capability-probe-r2.log:1-36`.
- **Diff hygiene**: `git diff --check` — clean (no whitespace errors).
- **CRLF**: all 6 edited files `crlf == lines` (status 133/133, probe 664/664, guide EN 226/226,
  guide RU 223/223, CommonTestSuite.Lob 1244/1244, code-smells-review 8261/8261).
- **Capability flags unchanged** (R133-03): no `src/**` file touched by this change
  (`git status` shows only docs/tests); MySQL/MariaDB/ClickHouse dialects still inherit
  `SupportsSequentialAccess => false`; providers keep `SupportsLobStreaming => false`.
- **`guide/28-streaming-data.md:77`** checked: the sentence rejects streaming LOB columns in the JSON
  projection whitelist — orthogonal to provider capability, no contradiction; no edit needed.
- **`code-smells-review.md:5364`** checked: it is finding 111 (SQL `MakeUpdate`/`MakeDelete` DRY),
  unrelated to LOB (the recon line number drifted). The disposition was recorded at `:7972` and in the
  LOB-tail list (`:8080`…), the actual MySQL/MariaDB/ClickHouse capability locations.

## ACT

- **CHECK PASS**: r=2, n=1/3, evidence contract `rv=D133.r2.ec1`; rows R133-01..R133-06 all PASS.
- **Freeze point**: plan r=2 frozen; no further probe/replan; `rv=D133.r2.ec1` is the accepted evidence revision.
- **Disposition (documented limitation)**: measured that neither MySqlConnector 2.6.2 (MySQL/MariaDB) nor
  ClickHouse.Driver 1.4.0 streams LOBs memory-bounded — MySQL/MariaDB `GetStream`/`GetTextReader` buffer the
  whole value (ratios 4.00 / 7.99 / 8.00) and `SequentialAccess` does not change allocations; ClickHouse
  `GetStream` throws `System.NotImplementedException` and `GetTextReader` buffers (3.99). Capability flags
  stay `false` (`SupportsSequentialAccess` inherited default; provider `SupportsLobStreaming => false`) —
  that is the correct value, not an omission. **No production change.** EN+RU guide corrected, probe claim
  corrected, buffered-behavior guard added.
- **Commit plan**: one D133 commit on branch `1.0.9-rc1`, staging only the 6 task files
  (`LobCapabilityProbeTests.cs`, `CommonTestSuite.Lob.cs`, guide EN, guide RU, `code-smells-review.md`, this
  file); `artifacts/` excluded; a separate bookkeeping commit updates the collection status.
- **Issue outcome**: #133 closed as a documented limitation; reopen trigger = a driver providing true
  memory-bounded streaming.
- **Push/merge**: none (push never authorized).

## Progress log

`Recon persisted (run 9 start).`

- Notice: host has no todowrite tool; this file is the progress log.
- 2026-10-06T06:31Z | PLAN | r=2 | iteration 1/3 | replan to documented limitation; evidence contract rv=D133.r2.ec1 | this file
- 2026-10-06T06:31Z | DO | r=2 | iteration 1/3 | claiming D133-01..D133-07 | brief + probe logs under /tmp/nextorm-D133
- 2026-10-06T06:33Z | DO | r=2 | iteration 1/3 | D133-01 probe claim corrected + buffered guard; D133-02/03 guide EN+RU; D133-04 header; D133-05 spec disposition | file diffs
- 2026-10-06T06:33Z | DO | r=2 | iteration 1/3 | `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors | /tmp/nextorm-D133/build.log
- 2026-10-06T06:34Z | DO | r=2 | iteration 1/3 | probe test exit 0, total 2 / failed 0 / succeeded 2 / skipped 0; verdicts buffered 7.99/8.00 | /tmp/nextorm-D133/run-lobprobe-r2.log
- 2026-10-06T06:34Z | DO | r=2 | iteration 1/3 | `git diff --check` clean; all 6 edited files CRLF | git + crlf check
- 2026-10-06T06:34Z | DO | r=2 | iteration 1/3 | D133-01..D133-07 complete; no production capability flag changed | this file
- 2026-10-06T06:41Z | CHECK | r=2 | iteration 1/3 | task-level CHECK PASS, rv=D133.r2.ec1; R133-01..R133-06 all PASS | this file
- 2026-10-06T06:41Z | ACT | r=2 | iteration 1/3 | documented-limitation disposition frozen; no production change; 6-file commit planned; #133 close | this file
