# D176 loop-back — integration provider counts

> **CURRENT (n=2, r=2)** — loop-back run (total 3511 / JSON 194); supersedes the pre-loopback
> `../integration/provider-counts.md` (n=1, total 3476 / JSON 159).

Environment: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`
(Podman socket present; all four container providers started; no `NEXTORM_*_CONNECTION` override).
Both runs `-c Debug --no-build` against the artifact built by `loopback/build-slnx.txt`.

## JSON-filtered subset (required scenarios)

```text
dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~WriteJson"
```

- exit **0**; summary **total 194 / failed 0 / skipped 0**.
- Log: `integration-writejson.txt`.

Per-provider executed `WriteJson` counts from the full-sweep XML (`integration-full-sweep.xml`),
grouped by test class:

| Provider | WriteJson executed | Passed | Failed | Skipped |
|---|---|---|---|---|
| SQLite | 32 | 32 | 0 | 0 |
| PostgreSQL | 34 | 34 | 0 | 0 |
| SQL Server | 32 | 32 | 0 | 0 |
| MySQL | 32 | 32 | 0 | 0 |
| MariaDB | 32 | 32 | 0 | 0 |
| ClickHouse | 32 | 32 | 0 | 0 |
| **Total** | **194** | **194** | **0** | **0** |

All six providers have positive executed JSON counts; no required provider is skipped.

## Full container sweep (comprehensive boundary)

```text
dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- \
  -noColor -result-xml <evidence>/integration-full-sweep.xml
```

- exit **0**; summary **total 3511 / errors 0 / failed 0 / skipped 197 / not-run 0** (40.5s).
- Log: `integration-full-sweep.log`; XML: `integration-full-sweep.xml`.
- The 197 skips are per-provider capability skips (LOB/streaming/batch/CTAS/row-value/`AVG`
  semantics), not JSON-shape skips. Executed counts by provider from the XML:

| Provider | Passed | Skipped |
|---|---|---|
| SQLite | 703 | 40 |
| PostgreSQL | 730 | 26 |
| SQL Server | 699 | 48 |
| MySQL | 617 | 80 |
| MariaDB | 32 | 0 |
| ClickHouse | 143 | 0 |
| Other (shared/container fixtures) | 390 | 3 |
| **Total** | **3314** | **197** |
