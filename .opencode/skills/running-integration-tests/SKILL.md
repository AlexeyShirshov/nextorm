---
name: running-integration-tests
description: Run nextorm's xUnit v3 live database integration tests. Use for local wslc runs, provider selection, endpoint or Testcontainers/Podman troubleshooting, and native test ID reconciliation.
---

# Run nextorm integration tests

## Choose the runtime

- **Local Windows/WSL default: `wslc`.** Run one engine and version at a time against an owned disposable container. SQLite runs in-process.
- **Podman/Testcontainers: only when explicitly requested.** Don't switch automatically, start Podman to diagnose a `wslc` failure, or stop a shared engine without authorization.
- **CI is unchanged.** Locally, cover the required scope with sequential groups. Don't use `--full`, a bare full-suite invocation, or overlapping test hosts or database containers. Run the affected live integration tests for changes to SQL generation, providers, or the query engine.

## Run serially

1. **Build once.** Freeze the test assembly and xUnit discovery configuration, then use the existing build for every group (`--no-build` if using `dotnet run`). Exclude `Category=Diagnostic` unless the request includes those tests. For acceptance, enumerate theories during discovery and use the same configuration for execution.
2. **Select by actual provider usage.** Inspect fixture and test paths, including per-row theory parameters, before using a class or method filter. Verify that its discovered roster is exactly the intended group. Keep mixed-provider and unresolved cases in the roster; don't run them until you can prove they stop and release each engine before starting another. Don't edit tests merely to make a filter work.
3. **Acquire the runner's exclusive lock before provisioning.** Verify no integration test host or conflicting database workload is active. Hold the lock through teardown. For `wslc`, follow the next section to provision and validate the endpoint.
4. **Use the project runner's standard cgroup caps and bounded timeout.** Follow [`tools/integration/run-integration-capped.py`](../../../tools/integration/run-integration-capped.py) for runner settings (`MemoryHigh=3G`, `MemoryMax=4G`, `MemorySwapMax=1G`). For a custom exact-ID launcher, apply and verify the same caps and an explicit timeout; the wrapper can fall back to uncapped execution and its per-class timeout is opt-in. Don't use an uncapped fallback. Don't gate on Windows free RAM, impose separate ad hoc database memory caps, or require swap to be disabled. Preserve resource settings that belong to the provider's test fixture.
5. **Reconcile the group and verify teardown.** Capture native events and exit status. Stop and remove only the owned `wslc` container and its disposable resources, then verify removal by ID, including on failure. Start the next engine only after verifying teardown. Keep failures in the evidence; retry only the affected cases after a targeted probe verifies a material correction.

## Use `wslc` with private provider endpoints

1. Read the selected fixture under `tests/nextorm.integration.tests/Providers/` for the required image/version, initialization, and server settings. Check the `wslc` image store before pulling; with authorization to download, pull only missing suite-required images, one at a time. Podman has a separate image store. No Ryuk is needed for manually owned endpoints.
2. Publish on an available loopback port (`127.0.0.1:HOST_PORT:CONTAINER_PORT`); verify the actual listener is loopback-only and reachable from Ubuntu. If `inspect` omits port bindings, check the Windows listener and confirm ownership with a unique response from an owned probe. Don't trust a bridge IP that answers after the container is removed. For a port conflict, check active listeners and reserved ranges and select another port; don't assume Podman owns it.
3. Verify readiness, version, and fixture settings using an authenticated query **from Ubuntu**, with the same provider driver and connection options as the runner. An internal `sqlcmd` alone won't catch SQL Server pre-login failures on the Ubuntu route. Don't log credentials or expose the database on LAN interfaces.
4. Pass the connection string only to the selected test process. Where the fixture honors the override, it uses that endpoint instead of launching Testcontainers:

   | Provider | Connection override |
   |---|---|
   | PostgreSQL | `NEXTORM_POSTGRES_CONNECTION` |
   | SQL Server | `NEXTORM_SQLSERVER_CONNECTION` |
   | MySQL | `NEXTORM_MYSQL_CONNECTION` |
   | MariaDB | `NEXTORM_MARIADB_CONNECTION` |
   | ClickHouse | `NEXTORM_CLICKHOUSE_CONNECTION` |

   `DOCKER_HOST` isn't required for a verified external endpoint. Don't assume that every direct container fixture honors the override. If a selected case can't run on the chosen route, record it as outstanding rather than switching runtimes or counting it as skipped.

## Reconcile acceptance by native xUnit ID

- Save the expected non-diagnostic roster and native xUnit events for each group. Match case IDs and, for any cases that expand at execution, account for all expected result IDs. Every expected result must have exactly one terminal outcome: passed, failed, or skipped. Report missing, unexpected, duplicated, failed, and not-run results separately. A missing result is not a skip.
- Require complete coverage, no failures, and exact skip counts **by reason** against [`skip-budget.json`](../../../tests/nextorm.integration.tests/skip-budget.json). Use the repository's skip-budget checker for compatible logs; for native events compare their reasons directly to the pinned map. Don't concatenate summaries or infer acceptance from totals alone. `NEXTORM_SKIP_PROVIDERS` is a local opt-out, not acceptance evidence.
- Record build and configuration, fixture version, exact selected IDs, results, and teardown. Use one frozen build for acceptance. If the build changes, keep evidence separate until reuse is explicitly justified and accepted. Matching discovery establishes the same roster, not equivalent execution.

## Use Podman only on explicit request

Use the Windows `podman.exe` and the rootless socket at `unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`. If the socket is missing on this route, check `podman.exe machine list`; start only the required `podman-machine-default` machine if stopped. Verify the socket with Docker API `/_ping`.

```bash
python3 tools/integration/run-integration-capped.py --providers postgres --timeout 600
```

Choose one provider per invocation and verify the selected scope and standard caps. The wrapper configures `DOCKER_HOST` for this route. The `DatabaseContainers` fixture stops reused containers but leaves them available for the next run; verify they have stopped before proceeding. If SQL Server reports `container state improper`, inspect the owned container and obtain authorization before removing anything not owned by the run. Exit 137 after an intentional stop doesn't by itself prove OOM.
