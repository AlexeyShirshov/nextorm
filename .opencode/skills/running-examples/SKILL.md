---
name: running-examples
description: Run nextorm's runnable examples under examples/ (postgres aviasales, mssql adventureworks, clickhouse analytics) against real datasets with Testcontainers/Podman. Use when running, debugging or re-verifying the examples, when provisioning fails, or on OOM/container errors — DOCKER_HOST, dataset cache, expected OK/FAIL counts.
---

# Running nextorm examples

Three console apps under `examples/`, each provisions its own database with Testcontainers, loads a
cached dataset on first run, executes every query and prints `[ OK ]`/`[FAIL]` plus an
`n/m succeeded` summary:

| Project (under `examples/`) | Provider | Dataset | Env var | Fallback env var |
|---|---|---|---|---|
| `nextorm.examples.postgres.aviasales` | `nextorm.postgres` | Aviasales `bookings` (~133 MB dump) | `AVIASALES_CONNECTION` | `NEXTORM_DEMODB_POSTGRES_CONNECTION` |
| `nextorm.examples.mssql.adventureworks` | `nextorm.sqlserver` | AdventureWorks 2022 (~200 MB bak) | `ADVENTUREWORKS_CONNECTION` | `NEXTORM_DEMODB_MSSQL_CONNECTION` |
| `nextorm.examples.clickhouse.analytics` | `nextorm.clickhouse` | ClickHouse `datasets.hits_v1` (~800 MB xz) | `CLICKHOUSE_ANALYTICS_CONNECTION` | `NEXTORM_DEMODB_CLICKHOUSE_CONNECTION` |

Datasets download once into `~/.cache/nextorm/{aviasales,adventureworks,clickhouse}/` and are reused.
Passing `--connection "<cs>"` (or the env var) skips the container and the dataset download entirely.

## Environment

Same Podman-on-Windows machine as the integration tests; always set `DOCKER_HOST` (see the
`running-integration-tests` skill for socket discovery/troubleshooting):

```
unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock
```

## Run

Normal (foreground) run from the repo root:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/nextorm.examples.postgres.aviasales -c Debug
```

Interactive runs can take 2.5–4 minutes (container start + load), so **run the built DLL detached and
poll the log** instead of blocking the shell:

```bash
cd /home/alex/sources/nextorm
setsid env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet examples/nextorm.examples.<name>/bin/linux/Debug/net10.0/nextorm.examples.<name>.dll \
  > /tmp/opencode/<name>.log 2>&1 < /dev/null & disown
# then poll
grep -aE "^\[ OK \]|^\[FAIL\]|queries succeeded" /tmp/opencode/<name>.log
```

Prefer the already-built DLL over `dotnet run` — `dotnet run` keeps MSBuild workers alive (memory) and
the log only appears at the end.

## Expected results (last verified against Testcontainers)

| Example | Result | Documented `FAIL`s |
|---|---|---|
| postgres aviasales | **11/11** | — |
| mssql adventureworks | **11/11** | — |
| clickhouse analytics | **8/11** | `ArrayAnalytics`, `Incremental`, `Retention` (arrays / `-Merge` / `groupArray` have no LINQ surface) |

A `[FAIL]` is expected only for documented engine gaps (`NotSupportedException` /
`QueryPreparationException`); the `Program` catches those, but **any other exception aborts the run** —
so a nonzero/missing summary means a real regression, not a known gap.

**The authoritative, dated log is [`examples/VERIFICATION.md`](../../../examples/VERIFICATION.md)** — read
it before running to compare, and update it after every run (results, per-query reason, and the roadmap
`todo_*` item that tracks each `FAIL`).

## Gotchas

- **OOM on the WSL host (~7.5 GB).** Loading `hits_v1` (the 841 MB archive is copied through a memory
  stream) can be OOM-killed. Before a ClickHouse run free memory: `dotnet build-server shutdown`,
  `curl -sS --unix-socket $DOCKER_HOST -X POST http://localhost/containers/prune`, kill stray
  `nextorm.examples` processes, then run detached with `setsid`.
- **Concurrent work in the same tree** can break the build or `git clean` untracked files; retry the
  build until `0 Error` and keep any temporary probe files in `/tmp`, not the repo.
- Without `DOCKER_HOST` the examples fail to provision a container (unlike the integration tests, they
  do not silently fall back to SQLite).
- **A WSL restart clears `/tmp` and drops the `/mnt/wsl/podman-sockets/...` mount**, so a run then dies
  with `DockerUnavailableException` (worse: the log file it was writing may be gone). Recreate the mount
  with `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start` (idempotent), wait for
  `podman-user.sock` to reappear, and retry.
- Loading `hits_v1` is the long pole (~2.5 min even on a healthy machine); a `SOCKET_TIMEOUT`/exit 209 in
  the load is an environment symptom — restart the machine, free memory, and retry rather than treating it
  as a product failure.
- Output goes to the log only; wait for the process to exit before reading the summary.
