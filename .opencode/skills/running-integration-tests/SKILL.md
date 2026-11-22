---
name: running-integration-tests
description: Run nextorm's xUnit v3 live database integration tests through the capped runner. Use for bounded provider selection, Podman/Testcontainers troubleshooting, and native test ID reconciliation; the current runner does not support manually provisioned wslc endpoints.
---

# Run nextorm integration tests

## Choose the runtime

- **`wslc` manual endpoints are currently unsupported by the capped runner.** See the wslc section before provisioning anything. SQLite runs in-process.
- **Podman/Testcontainers: only when explicitly requested.** Don't switch automatically, start Podman to diagnose a `wslc` failure, or stop a shared engine without authorization.
- **CI is unchanged.** Use bounded affected provider groups for local regression checks. Don't use `--full`, a bare full-suite invocation, or overlapping test hosts or database containers in this workflow. A separately declared integration/release gate remains complete and unchanged; run it only through its owning acceptance process when the existing scope requires it.

## Run serially

1. **Build the affected test project once.** Freeze the test assembly and xUnit discovery configuration, then reuse that build for each group (`--no-build` when using `dotnet run`). Exclude `Category=Diagnostic` unless the declared scope includes those tests.
2. **Select affected paths.** Inspect fixture and test usage, including per-row theory parameters, before choosing a class or method filter. Verify that the discovered roster matches the intended group. Keep mixed-provider and unresolved cases visible; don't run them until their engine lifecycle is known. Don't edit tests merely to make a filter work.
3. **Acquire the exclusive lock before provisioning.** Verify no other integration test host or conflicting workload is active. The `wslc` launcher holds `/tmp/opencode/integration/run.lock` through teardown. The Podman wrapper acquires that same non-reentrant lock itself, even when you choose a different log directory. Never remove a live lock file.
4. **Apply the project caps and timeout.** Use [`tools/integration/run-integration-capped.py`](../../../tools/integration/run-integration-capped.py) for the standard cgroup settings (`MemoryHigh=3G`, `MemoryMax=4G`, `MemorySwapMax=1G`). Apply the same settings and a bounded timeout to a manual or exact-ID launcher. Don't use an uncapped fallback, gate on Windows free RAM, add ad hoc database caps, or require swap to be disabled. Preserve provider-fixture resource settings.
5. **Record the first run.** Keep candidate and test assembly identities, selected scope, command, exit code, log, and known ownership/cleanup state together. For Podman, the wrapper writes `integration-receipt.jsonl` in its log directory or at `--receipt`. For `wslc`, capture these fields in the same launcher that runs the test. A Git identity alone doesn't prove which prebuilt binary ran. Don't record connection values or credentials.
6. **Reconcile results and cleanup.** Capture native xUnit events and exit status. Remove only a disposable endpoint owned by this run, then verify removal by ID, including on failure. For an external or shared endpoint, record cleanup as external or unverified; don't claim teardown. The receipt does not claim fixture cleanup it did not verify. Keep failures in evidence; retry only affected cases after a targeted probe confirms a material correction.

## Use `wslc` with private provider endpoints

Use the manual provider or exact-ID launcher for `wslc`. The Podman wrapper pins `DOCKER_HOST` and acquires a non-reentrant lock; don't call it while holding that lock or use it to run a manual `wslc` endpoint.

1. Read the selected fixture under `tests/nextorm.integration.tests/Providers/` for the required image, initialization and settings. Use an available `wslc` image; pull a missing image only if authorized.
2. Publish on a verified private loopback port (`127.0.0.1:HOST_PORT:CONTAINER_PORT`). If `inspect` omits port bindings, check the actual listener and verify its ownership with a unique response from an owned probe. Don't rely on an unverified bridge address.
3. Verify readiness, server version and fixture settings with an authenticated query from the test worker's namespace, using its provider driver and connection options.
4. Pass the appropriate `NEXTORM_POSTGRES_CONNECTION`, `NEXTORM_SQLSERVER_CONNECTION`, `NEXTORM_MYSQL_CONNECTION`, `NEXTORM_MARIADB_CONNECTION`, or `NEXTORM_CLICKHOUSE_CONNECTION` only to the test process. Run its selected IDs from the frozen build. `DOCKER_HOST` isn't required for a verified external endpoint. If a fixture ignores the override or starts another engine, leave the affected case outstanding; don't fall back automatically.
5. Stop and remove only the owned endpoint and verify teardown before releasing the lock. Report external resource cleanup as unverified unless its owner supplies evidence.

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

Choose one provider per invocation and verify the selected scope and standard caps. The wrapper configures `DOCKER_HOST` for this route. The `DatabaseContainers` fixture stops reused containers but leaves them available for the next run; verify they have stopped before proceeding. If SQL Server reports `container state improper`, inspect the owned container and obtain authorization before removing anything not owned by the run. Exit 137 after an intentional stop doesn't by itself prove OOM.
