---
name: running-integration-tests
description: Run Nextorm's live integration tests against existing wslc databases or in-process SQLite. Use for provider selection, connection troubleshooting, and native xUnit ID reconciliation.
---

# Run nextorm integration tests

## Choose the runtime

- **`wslc` is the local route for server providers.** The integration-test databases are already running in `wslc` containers. Connect to their existing test endpoints; the test run does not own these containers. SQLite runs in-process and needs no container.
- **Podman/Testcontainers: only when explicitly requested.** Keep any `wslc` connection failure visible; don't switch runtimes automatically.
- **CI is unchanged.** Use bounded affected provider groups for local regression checks. Don't use `--full`, a bare full-suite invocation, or overlapping test hosts or database containers in this workflow. A separately declared integration/release gate remains complete and unchanged; run it only through its owning acceptance process when the existing scope requires it.

## Run serially

1. **Build the affected test project once.** Freeze the test assembly and xUnit discovery configuration, then reuse that build for each group (`--no-build` when using `dotnet run`). Exclude `Category=Diagnostic` unless the declared scope includes those tests.
2. **Select affected paths.** Inspect fixture and test usage, including per-row theory parameters, before choosing a class or method filter. Verify that the discovered roster matches the intended group. Keep mixed-provider and unresolved cases visible; don't run them until their engine lifecycle is known. Don't edit tests merely to make a filter work.
3. **Serialize runs.** Hold `/tmp/opencode/integration/run.lock` throughout a direct test run. Check for competing integration test hosts before starting. The Podman wrapper takes the same lock itself; don't hold the lock when invoking it.
4. **Bound the run.** Apply a timeout to the selected group. Local integration testing does not require a `systemd` scope or specific cgroup settings.
5. **Record the run.** Keep candidate and test assembly identities, selected scope, command, exit code, log, and endpoint ownership together. A Git identity alone doesn't prove which prebuilt binary ran. Keep connection values and credentials out of the record.
6. **Reconcile results and cleanup.** Capture native xUnit events and exit status. Leave existing `wslc` containers running and record their ownership as external. Remove only disposable infrastructure owned by this run, and claim cleanup only if verified. Keep failures in evidence; retry only affected cases after a targeted probe confirms a material correction.

## Connect to existing `wslc` databases

Run the selected tests directly against the existing endpoints. The Podman wrapper is not the launcher for this route.

1. Read the selected fixture under `tests/nextorm.integration.tests/Providers/` for the required server version, initialization and connection options. Confirm that the selected tests accept an external endpoint.
2. Use the existing private test endpoint. Verify its identity, readiness, server version and fixture settings with an authenticated query from the test worker's namespace. An unavailable or mismatched endpoint blocks that provider group.
3. Pass the matching connection string to the test process via `NEXTORM_POSTGRES_CONNECTION`, `NEXTORM_SQLSERVER_CONNECTION`, `NEXTORM_MYSQL_CONNECTION`, `NEXTORM_MARIADB_CONNECTION`, or `NEXTORM_CLICKHOUSE_CONNECTION`. `DOCKER_HOST` isn't required for this route. If a test ignores the override or would use another runtime, leave that case outstanding.
4. Run the selected IDs from the frozen build. A class selector is sufficient when discovery confirms that it selects exactly the affected group.

## Reconcile the selected group by native xUnit ID

- Save the expected roster and native xUnit events for each selected group. Match case IDs and, for cases that expand at execution, account for every expected result ID. Every expected result must have exactly one terminal outcome: passed, failed, or skipped. Report missing, unexpected, duplicated, failed, and not-run results separately. A missing result is not a skip.
- Require all selected cases to finish with no failures. Compare skip reasons with [`skip-budget.json`](../../../tests/nextorm.integration.tests/skip-budget.json) only when the existing declared scope includes canonical full-suite acceptance; use the repository checker on its compatible full-suite log. Don't concatenate summaries or infer acceptance from totals alone. `NEXTORM_SKIP_PROVIDERS` is a local opt-out, not evidence for an affected provider.
- Record build/configuration, fixture version, exact selected IDs, result receipt and logs, plus cleanup status. Keep evidence separate if the build changes unless reuse is justified by equivalent inputs. Matching discovery establishes the same roster, not equivalent execution.

## Keep full acceptance separate

The local integration workflow does not authorize `--full`. When an existing acceptance or release
scope requires the canonical full suite, its owning process must run the complete integration,
skip-budget and applicable coverage gates with their existing commands and thresholds. Do not replace
that gate with bounded local groups, and do not run it for a narrow scope that does not declare it.

## Use Podman only on explicit request

Use the Windows `podman.exe` and the rootless socket at `unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`. If the socket is missing on this route, check `podman.exe machine list`; start only the required `podman-machine-default` machine if stopped. Verify the socket with Docker API `/_ping`.

```bash
python3 tools/integration/run-integration-capped.py --providers postgres --timeout 600
```

Choose one provider per invocation and verify the selected scope. If `systemd` is unavailable, pass `--no-caps` and keep the timeout. The wrapper configures `DOCKER_HOST` and writes `integration-receipt.jsonl`. The `DatabaseContainers` fixture stops reused containers but leaves them available for the next run; verify they have stopped before proceeding. If SQL Server reports `container state improper`, inspect the owned container and obtain authorization before removing anything not owned by the run. Exit 137 after an intentional stop doesn't by itself prove OOM.
