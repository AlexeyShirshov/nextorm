# D175 integration guard (EC175-08 / A4) + AC3 container sweep

## Guard determination

`git diff --name-only` contains no path under `tests/nextorm.integration.tests/` and no `CommonTestSuite.*`
source. The R175 change is limited to five `tests/nextorm.core.tests/*.cs` files, so EC175-08 is a **guard**
(no integration-suite annotation/behavior change is required by the diff); integration source is
read-only by plan (`X03` guard: `CommonTestSuite.JoinInto.cs:221-222` unchanged).

## Unconditional AC3 container sweep (still run)

Per AC1..AC5 the full integration suite was executed anyway, with the Podman socket:

```
env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

- exit 0; `=== TEST EXECUTION SUMMARY === nextorm.integration.tests Total: 3555, Errors: 0, Failed: 0,
  Skipped: 197, Not Run: 0`.
- Provider availability: `grep -c "is not available" collection/integration.log` = 0 (grep exit 1 = zero
  occurrences). All providers actually ran; the 197 skips are capability skips (e.g. SQL Server CTAS /
  provider-specific SQL), not provider-unavailable skips.
- Artifact: `collection/integration.log`.
