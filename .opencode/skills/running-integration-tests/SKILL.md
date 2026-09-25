---
name: running-integration-tests
description: Run nextorm's xunit v3 integration tests (tests/nextorm.integration.tests) against PostgreSQL, SQL Server, MySQL, ClickHouse and SQLite via Testcontainers. Use when running, filtering or debugging integration tests, or when Testcontainers/Podman/Docker connection fails — DOCKER_HOST, podman socket, container startup, skipped provider tests.
---

# Running nextorm integration tests

## MANDATORY — read this before claiming any result

- Never run `tests/nextorm.integration.tests` **without** `DOCKER_HOST` and then call it a pass. Without it only SQLite runs and PostgreSQL/SQL Server/MySQL/ClickHouse are all `skipped`; **skips are not evidence**.
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

Whole suite (starts PostgreSQL, SQL Server, MySQL and ClickHouse containers lazily):

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
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

## Behaviour and gotchas

- **Without `DOCKER_HOST` only SQLite runs.** `ProviderTestSuite` calls `Assert.SkipUnless(Provider.IsAvailable, ...)` in its constructor, so PostgreSQL/SQL Server/MySQL/ClickHouse tests are reported as skipped — a green run proves nothing about those providers.
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
