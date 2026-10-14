# D226 / rv1 — D1 real-driver probe observations

- task: T226 (#226), cycle N=1, plan_revision r=1, evidence_contract rv=1
- owner: coder/G1; verifier: check
- date: 2026-10-14 (probe run 12:11 UTC)
- question: does the chosen algorithm — **snapshot `RecordsAffected` pre-close, release the reader,
  prefer the post-release value else the pre-close value else `-1`** — reproduce the driver's DML
  total on real drivers, under sync/async release, early Dispose and a partially-read reader?

## Command and exit codes (reproducible from tree-bound sources)

```
bash artifacts/pdca/D226/rv1/run-d1-probe.sh      # script exit code: 0
  -> dotnet build artifacts/pdca/D226/rv1/probe/Probe.csproj -c Debug   # 0 Warning(s) 0 Error(s)
  -> NEXTORM_PROBE_PROVIDERS=sqlite,postgres,sqlserver,mysql,clickhouse (default)
     DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock
     dotnet run --project .../probe/Probe.csproj -c Debug --no-build -- <json>   # probe exit code: 0
```

Artifacts: `release-probe.json` (machine-readable observations), `logs/d1-probe.log` (transcript +
both exit codes), `logs/d1-probe-stdout.log` (raw stdout incl. the container-runtime transcript),
`probe/Probe.csproj` + `probe/Program.cs` (the harness), `run-d1-probe.sh` (the runner).

Drivers are real ADO.NET drivers: `Microsoft.Data.Sqlite` 10.0.12 (in-process file database),
`Npgsql` 10.0.3, `Microsoft.Data.SqlClient` 6.1.7, `MySqlConnector` 2.6.2, `ClickHouse.Driver` 1.4.0;
the four server drivers run through Testcontainers with the same images the integration suite pins
(`postgres:17-alpine`, `mcr.microsoft.com/mssql/server:2025-latest`, `mysql:8.4`,
`clickhouse/clickhouse-server:25.8-alpine`).

## Observations (known DML total N = 3)

| provider | scenario | exp | pre-release | post-release | chosen | post-release error |
|---|---|---|---|---|---|---|
| sqlite | dml-sync-dispose-no-read | 3 | 3 | 3 | 3 | — |
| sqlite | dml-sync-close-no-read | 3 | 3 | 3 | 3 | — |
| sqlite | dml-async-dispose-no-read | 3 | 3 | 3 | 3 | — |
| sqlite | dml-then-select-sync-dispose | 3 | 3 | 3 | 3 | — |
| sqlite | dml-then-select-async-dispose | 3 | 3 | 3 | 3 | — |
| sqlite | dml-then-select-partial-sync-dispose | 3 | 3 | 3 | 3 | — |
| sqlite | select-sync-dispose | -1 | -1 | -1 | -1 | — |
| postgres | all 6 DML scenarios above | 3 | 3 | 3 | 3 | — |
| postgres | select-sync-dispose | -1 | -1 | -1 | -1 | — |
| postgres | native-batch-async-dispose (`update; select`) | 3 | 3 | 3 | 3 | — |
| sqlserver | all 6 DML scenarios above | 3 | 3 | 3 | 3 | — |
| sqlserver | select-sync-dispose | -1 | -1 | -1 | -1 | — |
| sqlserver | native-batch-async-dispose (`update; select`) | 3 | 3 | 3 | 3 | — |
| mysql | all 6 DML scenarios above | 3 | 3 | 3 | 3 | — |
| mysql | select-sync-dispose | -1 | -1 | -1 | -1 | — |
| mysql | native-batch-async-dispose (`update; select`) | 3 | 3 | 3 | 3 | — |
| clickhouse | insert-async-dispose (observation) | 0 | 0 | 0 | 0 | — |
| clickhouse | select-async-dispose (observation) | -1 | 0 | 0 | 0 | — |

31/31 oracle rows (sqlite 7 — no `DbBatch`; postgres/sqlserver/mysql 8 each) satisfy
`chosen == expected`; the probe's own exit code is 0 on that criterion.

## Conclusions

1. **Pre-close == post-release == N on all four oracle drivers**, for the bare DML shape and for the
   `dml; select` shape that every nextorm batch ends with, on sync close, sync Dispose and
   `DisposeAsync`, and for a reader released early without being advanced. No driver needs a
   provider-specific workaround; the chosen algorithm reproduces the total.
2. **Post-release reads never throw** on these four drivers (no `ObjectDisposedException` /
   `InvalidOperationException` observed), so the safe-read helper is defensive rather than
   load-bearing here — it still has to tolerate a throwing getter for other/older drivers.
3. **Partial read is not a completion oracle**: releasing the reader before advancing still reports
   the driver's DML total, which is exactly why the cache is published at *release* and the getter
   must not report a live-reader value (`-1` until release).
4. **The sentinel survives for inapplicable metadata**: a pure SELECT reports `-1` on
   sqlite/postgres/sqlserver/mysql. ClickHouse's driver reports `0` for both INSERT and SELECT
   (its own semantics; the plan's C5 wording is "`-1`/`0`"), and its lightweight `DELETE` is refused
   (`Code: 36 ... DELETE query is not supported`, recorded verbatim in `release-probe.json`), so
   ClickHouse contributes observations only and no DML oracle.
5. Multi-statement batches expose **one** driver total through both the native `DbBatch`
   (postgres/sqlserver/mysql, `update; select` → 3) and the joined-command fallback (all four
   drivers, `dml; select` → 3), so the batch path can snapshot the total at terminal release
   (pre-close) without a second Close.

**D1 PASS** — the chosen algorithm reproduces the total without a provider-specific workaround.
Production edits (D2) may proceed.
