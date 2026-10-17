---
name: running-integration-tests
description: Run nextorm's xunit v3 integration tests (tests/nextorm.integration.tests) against PostgreSQL, SQL Server, MySQL, ClickHouse and SQLite via Testcontainers. Use when running, filtering or debugging integration tests, or when Testcontainers/Podman/Docker connection fails — DOCKER_HOST, podman socket, container startup, skipped provider tests.
---

# Running nextorm integration tests

## MANDATORY — read this before claiming any result

- Never run `tests/nextorm.integration.tests` **without** `DOCKER_HOST` and then call it a pass. The suite is **fail-closed**: PostgreSQL/SQL Server/MySQL/ClickHouse tests **fail** when their container is unavailable — they no longer skip. The only opt-out is `NEXTORM_SKIP_PROVIDERS=<name,...>` (see "Skip policy"), and a run with opt-outs is not evidence for those providers.
- If `ls -l /mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` fails, do **not** stop at "cannot run": start the machine with `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start`, wait a few seconds and re-check the socket (see Troubleshooting), then run with `DOCKER_HOST`.
- Run the container suite whenever a change can affect SQL generation, providers or the query engine — not only when explicitly asked. Loading this skill is the first step, before running the tests.

## Environment: Podman runs on Windows, WSL talks to it over a socket

- Podman is a **Windows Podman Desktop WSL2 machine** named `podman-machine-default`. It is **not installed in WSL**: `podman` is not on the WSL `PATH`, so `podman machine list` fails with `command not found`.
- The Docker-compatible API is exposed into WSL as a unix socket:
  `unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`
  (general form `/mnt/wsl/podman-sockets/<machine-name>/podman-user.sock`; a `podman-root.sock` sits next to it).
- Testcontainers discovers it purely through `DOCKER_HOST`; nothing else is normally required. Verified against Podman server 5.8.6 / Docker API 1.44.
- To inspect the host from WSL, use the Windows binary:
  `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine list` (also `ps`, `images`, `logs`).

## Run

Always run from the repo root and set `DOCKER_HOST` first.

Whole suite (starts PostgreSQL, SQL Server, MySQL and ClickHouse containers lazily). The canonical
run excludes the opt-in diagnostic harnesses (`Category=Diagnostic`), so the skip set matches the
pinned budget:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- \
  -noColor -notrait "Category=Diagnostic" > /tmp/opencode/integration.log 2>&1

python3 eng/check-integration-skip-budget.py --log /tmp/opencode/integration.log
```

Single provider (much faster — only that provider's container starts; the first run initializes the database, later runs reuse the stopped container and start in a second or two):

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- \
  -class nextorm.integration.tests.PostgresIntegrationTests -noColor
```

Test classes accepted by `-class`:
`SqliteIntegrationTests`, `SqliteSpecificTests`, `PostgresIntegrationTests`, `PostgresSpecificTests`, `SqlServerIntegrationTests`, `SqlServerSpecificTests`, `MySqlIntegrationTests`, `MySqlSpecificTests`, `ClickHouseIntegrationTests`.
(SQLite needs no container.)

Equivalent with `dotnet test` (VSTest filter):

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet test tests/nextorm.integration.tests -c Debug \
  --filter "FullyQualifiedName~PostgresIntegrationTests"
```

## Cost discipline — build once, serialise, don't repeat filters

- **Build once, then run with `--no-build`.** `dotnet run --project tests/X` (and plain `dotnet test`) rebuild the project on every invocation — a re-run costs a full MSBuild pass and re-reads the whole project graph. Build the solution once, then add `--no-build`:

  ```bash
  dotnet build nextorm.slnx -c Debug
  DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
    dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- \
    -class nextorm.integration.tests.PostgresIntegrationTests -noColor
  ```

- **Run provider suites one at a time, never in parallel.** Four concurrent test projects (plus build + editors) saturate 8 vCPU and push disk reads to ~1.9 GiB/s for minutes. Serialise them.
- **Never re-run the same filter.** Each distinct `--dotnet-test-pipe /tmp/<id>` is a real execution; re-issuing `--filter FullyQualifiedName~Csv` (or the same `-class`) is wasted work — run it once. For a provider subset use a single `--filter "FullyQualifiedName~Csv&FullyQualifiedName~<Provider>IntegrationTests"` instead of repeating.
- **Scope the run to the change.** For a core/CSV-streaming change the relevant targets are `nextorm.integration.tests --filter FullyQualifiedName~Csv` and `nextorm.core.tests`; the per-provider SQL-generation suites (`tests/nextorm.*.tests`) need no database and need not all run for such a change. Reserve a full sweep for final validation, once.
- Repeated `[.NET TP Worker]` / `[RequestBuilder]` children under a `dotnet run`/`dotnet test` mean you are rebuilding — switch to `--no-build`.

## Behaviour and gotchas

- **Fail-closed availability.** `ProviderTestSuite` asserts `Provider.IsAvailable` in its constructor: an unavailable provider fails every test of that suite with the container error. It is never silently skipped. Intentional local reductions require `NEXTORM_SKIP_PROVIDERS` (below).

## Skip policy — capability matrix, opt-out and budget

- **Capability skips are pinned.** `Assert.SkipUnless(Provider.SupportsX, ...)` in `CommonTestSuite` is legitimate only for the reviewed matrix in `Providers/ITestProvider.cs`. `CrossProvider/ProviderCapabilityContractTests` pins every flag per provider, so a flag that flips by accident is a red test instead of a disappearing fact you never notice.
- **Explicit opt-out.** `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,clickhouse` (case-insensitive, comma/semicolon separated) is the only way to skip an unavailable provider. Use it for SQLite-only local runs, never for evidence.
- **Diagnostic harnesses.** `LobCapabilityProbeTests`, `LobPerfHarnessTests` and `SqliteWarmAggregateAllocationTests` carry `[Trait("Category", "Diagnostic")]` and are opt-in via `NEXTORM_LOB_PROBE` / `NEXTORM_LOB_PERF` / `D243_ATTRIBUTION_REPORT`. Exclude them with `-notrait "Category=Diagnostic"`.
- **Skip budget.** `tests/nextorm.integration.tests/skip-budget.json` pins the capability skips by reason. After a full run, check it with
  `python3 eng/check-integration-skip-budget.py --log <log>`; the gate fails on any new or vanished skip. Intentional changes require updating the pinned matrix first, then `--update`. Diagnostic skips are ignored by the script, so the gate works with or without `-notrait`.
- Containers start lazily per provider; each is built with `WithReuse(true)` + a `reuse-id` label, so at the end of the run it is **stopped but kept** and the next run starts the same container (skipping PostgreSQL/SQL Server/MySQL/ClickHouse initialization — the heavy disk I/O that can stall WSL). The `DatabaseContainers` assembly fixture performs the stop.
- To force a clean database, delete the reused containers. `podman.exe` defaults to the **rootful** connection and will not list them; use the rootless connection (the one the socket exposes): `"/mnt/c/Program Files/RedHat/Podman/podman.exe" --connection podman-machine-default ps -a --filter label=reuse-id`, then `... rm -f <id>`. Changing a builder configuration (image, env, port bindings) changes the reuse hash and creates a **new** container, leaving the previous one behind — purge from time to time.
- Reuse does **not** leak rows between runs: every provider's `EnsureSeeded` runs `drop table if exists …` before `create table`. Do not rely on a dirty container in a test.
- **`TESTCONTAINERS_RYUK_DISABLED=true` is not needed** — Ryuk starts fine against this Podman. Add it only as a fallback if the run fails while starting Ryuk. Reused containers are not registered with Ryuk (reuse disables it for them) — by design they survive a killed run.
- Pre-pulled images: `postgres:17-alpine`, `mcr.microsoft.com/mssql/server:2025-latest`, `mysql:8.4`, `testcontainers/ryuk`.
  **ClickHouse is not pre-pulled** (`clickhouse/clickhouse-server:25.8-alpine`); the first ClickHouse run pulls it (needs network). Pre-pull with:
  `"/mnt/c/Program Files/RedHat/Podman/podman.exe" pull docker.io/clickhouse/clickhouse-server:25.8-alpine`.
- Point a provider at an existing server instead of a container with `NEXTORM_POSTGRES_CONNECTION`, `NEXTORM_SQLSERVER_CONNECTION`, `NEXTORM_MYSQL_CONNECTION`, `NEXTORM_CLICKHOUSE_CONNECTION` (see `tests/nextorm.integration.tests/Providers/*Container.cs`). When set, no container is started for that provider.
- Provider SQL-generation tests (`tests/nextorm.*.tests`) never need a database; only `nextorm.integration.tests` does.

## Troubleshooting

- `podman: command not found` in WSL → expected. Use the socket and/or `podman.exe`; do not try to install Podman inside WSL.
- Testcontainers hangs or fails to connect → confirm the machine is running (`podman.exe machine list`, expect `Currently running`) and the socket exists: `ls -l /mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`. Start Podman Desktop if it is missing.
- Sanity-check the socket without Testcontainers:
  `curl -s --unix-socket /mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock http://d/v1.40/_ping` → `OK`.
