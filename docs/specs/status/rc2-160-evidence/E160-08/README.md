# E160-08 — R160-09: six-provider container-backed integration execution

- task: D160, cycle 1, plan revision r=2, attempt n=1/3, **contract rv=3**
- branch: `1.0.9-rc2`
- phase: DO
- tree identity: `69ed192d` + working-tree change (`tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs`:
  5 new D160 root-alias / mixed-join cases) + this evidence package
- skill: `.opencode/skills/running-integration-tests/SKILL.md` loaded first; Podman socket present
  (`/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, server 5.8.6 / API 1.44)

> **STEP-3 re-run (post-C1).** The C1 core fix (`SqlBuilder.MakeSelect` / `EntityBuilder` alias-state
> rebasing, commit `9d6a9f59`) changed compiled sources after the n=1 integration run below
> (tree `69ed192d`), so the whole container-backed boundary was re-executed on the current tree
> (`4e2f43b2` + D160 evidence). Fresh evidence: `integration-step3.log` and
> `integration-results-step3.log` (both exit **0**, same summary **Total 3357 / Errors 0 / Failed 0 /
> Skipped 197 / Not Run 0**), with `provider-inventory-step3.txt` + `results-step3.xml` as the fresh
> machine-readable per-test proof. The sections below describe the superseded n=1 run and are kept
> for history.

## Exact invocation (required command, argument array)

```
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

- exit_code: `0`
- summary: **Total 3357, Errors 0, Failed 0, Skipped 197, Not Run 0, Time 53.541s**
- full output: `integration.log`

### Supporting machine-readable run (per-provider inventory)

The required command's default reporter does not emit per-test outcomes, so the same suite was run once
more (containers warm, `--no-build`) with an xUnit v3 XML result to produce the per-provider breakdown
and prove the D160 cases ran:

```
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor \
  -result-xml docs/specs/status/rc2-160-evidence/E160-08/results.xml
```

- exit_code: `0`; same summary: **Total 3357, Errors 0, Failed 0, Skipped 197, Not Run 0, Time 34.062s**
- committed: `integration-results.log` (full output) and `provider-inventory.txt` (per-provider + D160
  per-case table extracted from the raw XML)
- the n=1 raw XML (`results.xml`) and its discovery inventory (`tests-discovered.txt`) were local,
  uncommitted inputs and are superseded by the STEP-3 `results-step3.xml` + `provider-inventory-step3.txt`
  committed in this package (the n=1 run used `-result-xml …/results.xml`)

## Per-provider executed / passed / failed / skipped

Every provider actually executed (no provider reported as unavailable/skipped). Skips are per-test
capability skips (`Assert.Skip*`), not provider-availability skips.

| Provider | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| PostgreSQL (`*Postgres*`) | 845 | 819 | 0 | 26 |
| SQL Server (`*SqlServer*`) | 733 | 685 | 0 | 48 |
| MySQL (`*MySql*`) | 673 | 593 | 0 | 80 |
| SQLite (`*Sqlite*`) | 723 | 683 | 0 | 40 |
| MariaDB (`*MariaDb*`, `DynamicColumnsMariaDb*`) | 55 | 55 | 0 | 0 |
| ClickHouse (`*ClickHouse*`) | 205 | 205 | 0 | 0 |
| shared/contract (LOB probe/harness, non-provider) | 123 | 120 | 0 | 3 |
| **TOTAL** | **3357** | **3160** | **0** | **197** |

- MariaDB classes executed: `MariaDbCsvIntegrationTests` 2, `MariaDbFunctionsIntegrationTests` 18,
  `MariaDbImplicitNavigationTests` 14, `MariaDbJsonStreamTests` 6, `MariaDbRawSourceBindingTests` 4,
  `MariaDbTupleExecutionTests` 3, `MariaDbTupleInExecutionTests` 5, `MariaDbTypedCteIntegrationTests` 2,
  `DynamicColumnsMariaDbContainerTests` 1 — 55/55 pass.
- All 5 containers started from the socket (PostgreSQL, SQL Server, MySQL `mariadb:11.4` unused for MySQL,
  MariaDB `mariadb:11.4`, ClickHouse); SQLite is in-process. MariaDB image is `mariadb:11.4`, env override
  `NEXTORM_MARIADB_CONNECTION` (not set here — the Testcontainers instance was used).

## D160 root-alias / mixed-join cases (added to `CommonTestSuite.JoinAlias.cs`)

Five provider-agnostic cases now execute through every provider that inherits `CommonTestSuite`
(PostgreSQL, SQL Server, MySQL, SQLite) — 5 × 4 = **20 executions, 20 passed, 0 failed, 0 skipped**:

| Case | Assertion |
|---|---|
| `Root_alias_inner_join_returns_matched_rows` | `.WithAlias(Alias.Root)` + `Alias.Buyer` join → ids `[10,20]` |
| `Root_alias_item1_and_root_name_resolve_to_the_same_slot` | `p.Root.Id` ≡ `p.Item1.Id`; order ids `[1,2]` |
| `Root_alias_supports_a_positional_join_after_it` | root alias → positional join → `p.Item2.Id` `[10,20]` |
| `Mixed_alias_then_positional_join_executes_and_keeps_slot_identity` | alias slot ≡ subsequent `Item3` positional slot |
| `Mixed_positional_then_alias_join_executes_and_keeps_slot_identity` | positional `Item2` ≡ trailing `Alias.Approver` slot |

Result: `results-step3.xml` — every case `Pass`, `type` = `NextORM.Integration.Tests.{Postgres,SqlServer,MySql,Sqlite}IntegrationTests` (the n=1 `results.xml` covered the same 20 executions).

### Coverage boundary (explicit, not silently absent)

`ClickHouseIntegrationTests` and the MariaDB test classes deliberately do **not** inherit `CommonTestSuite`
(ClickHouse uses provider-specific behaviour; MariaDB owns its own `MariaDbContainer`). Their containers do
**not** seed the shared `orders`/`person` alias fixtures, so the new common root-alias/mixed cases do not run
there. Root-alias real execution on those two providers is therefore not covered by this common case set;
their six-provider root-alias surface is covered by the SQL-generation row E160-02
(`tests/nextorm.{mariadb,clickhouse}.tests/JoinAliasSqlGenerationTests.cs`, 16/16 each). No provider reported
skipped; this is a suite-membership boundary, recorded for CHECK, not a provider failure.

## Failures

- D160-caused failures: **none** (0 failed).
- Non-D160 / environmental failures: **none** (0 failed, exit 0).

## Files

- `integration.log` — full output of the exact required command (exit 0)
- `integration-results.log` — full output of the supporting `-result-xml` run (exit 0)
- `results-step3.xml` — xUnit v3 per-test outcomes (per-provider + D160 case proof; STEP-3 re-run)
- `provider-inventory-step3.txt` — per-provider + D160 per-case table extracted from `results-step3.xml`
