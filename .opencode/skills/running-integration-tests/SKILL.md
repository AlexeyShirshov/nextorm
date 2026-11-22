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
3. **Acquire the runner's exclusive lock before provisioning.** For the supported Podman/Testcontainers route, the runner acquires its lock before tests provision containers. Don't acquire its `run.lock` in a parent shell: the lock is non-reentrant. Verify no other integration host or conflicting workload is active.
4. **Use the project runner and receipt.** Follow [`tools/integration/run-integration-capped.py`](../../../tools/integration/run-integration-capped.py) for standard cgroup settings (`MemoryHigh=3G`, `MemoryMax=4G`, `MemorySwapMax=1G`). It refuses an implicit uncapped run if systemd caps are unavailable. `--no-caps` is not an acceptance route. The default per-class timeout is opt-in; set a bounded timeout. Don't gate on Windows free RAM, add separate database memory caps, or require swap to be disabled. Preserve provider-fixture resource settings. Optional `--build` uses the same cgroup wrapper and settings.
5. **Record the first run.** The runner appends start and completion events to `integration-receipt.jsonl` in its log directory (or the path passed with `--receipt`). The receipt records Git worktree identity, selected scope, exact argument array, exit code, log path, and known ownership/cleanup state. It separately fingerprints the test assembly when exactly one configured output is found; otherwise it marks the binary identity unverified. A Git identity does not prove which prebuilt binary ran. Review the receipt with the full log; don't put endpoint values or credentials in a receipt.
6. **Reconcile results and cleanup.** Capture native xUnit events and exit status. Remove only a disposable endpoint owned by this run, then verify removal by ID, including on failure. For an external or shared endpoint, record cleanup as external or unverified; don't claim teardown. The receipt does not claim fixture cleanup it did not verify. Keep failures in evidence; retry only affected cases after a targeted probe confirms a material correction.

## wslc endpoint route is blocked

`run-integration-capped.py` pins `DOCKER_HOST` to the Podman socket, checks that socket in preflight, and acquires its lock only when the runner starts. It cannot hold the lease across manual `wslc` provisioning. A connection override does not change those facts or validate teardown.

Until a lease-aware runner supports this lifecycle, don't provision a `wslc` endpoint for a run through this wrapper, pass an external override as a workaround, inherit or bypass `run.lock`, or switch to Podman automatically. If an affected live integration is required, report the missing safe route as a blocker; use Podman only after explicit authorization.

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
