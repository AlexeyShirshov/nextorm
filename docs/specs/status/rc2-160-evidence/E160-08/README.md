# E160-08 — six-provider integration (D160.3-6 verification, plan r=3, rv=4)

## Command and result

```
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor \
  -result-xml docs/specs/status/rc2-160-evidence/E160-08/integration-results.xml
```

- exit **0**; machine-readable totals `Total: 3377, Errors: 0, Failed: 0, Skipped: 200, Not Run: 0`
  (wall clock 58 s; runner time 34.643 s)
- **0 provider-availability skips** (Podman socket present, `_ping` = OK; every skip is an
  `Assert.Skip*` capability skip with a capability reason)
- six providers executed: PostgreSQL, SQL Server, MySQL, MariaDB, SQLite, ClickHouse

## Per-provider inventory

| Provider | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| PostgreSQL | 848 | 822 | 0 | 26 |
| SQL Server | 736 | 688 | 0 | 48 |
| MySQL | 676 | 596 | 0 | 80 |
| MariaDB | 59 | 59 | 0 | 0 |
| SQLite | 727 | 683 | 0 | 44 |
| ClickHouse | 209 | 209 | 0 | 0 |
| shared/contract | 122 | 120 | 0 | 2 |
| **assembly** | **3377** | **3177** | **0** | **200** |

Full inventory: `provider-inventory.txt`. Raw log: `integration.log`.

## D160 root/mixed/correlated case counts (machine-readable)

| Bucket | Passed | Capability-skipped | Providers |
|---|---:|---:|---|
| root alias (`.WithAlias(Alias.Root)`) | 16 | 0 | PG 3, SS 3, MySQL 3, SQLite 3, ClickHouse 2, MariaDB 2 |
| mixed positional/alias | 10 | 0 | PG 2, SS 2, MySQL 2, SQLite 2, ClickHouse 1, MariaDB 1 |
| correlated APPLY alias | 9 | 3 | PG 3, SS 3, MySQL 3 pass; SQLite 3 skip (`SupportsApply=false`) |
| **D160 total** | **35** | **3** | 0 provider-availability skips |

The 3 correlated-APPLY skips are the documented SQLite no-lateral/APPLY capability
(`Assert.SkipUnless(Provider.SupportsApply)`), not provider unavailability.

## Fixture defect found and fixed in this unit (test-only)

Pre-fix run (`integration-prefix-fail.log`, `integration-results-prefix-fail.xml`):
`MariaDbJoinAliasIntegrationTests.Root_alias_inner_join_returns_matched_rows` **FAIL** —
`Expected ids to be equal to {10, 20}, but {10} contains 1 item(s) less`. The MariaDB seed has a
single order (`id=1, buyer_id=10`), whereas the shared `CommonTestSuite` variant seeds two orders;
the expected value had been copied without the seed. The product result `{10}` is correct.

Fix (bounded, test-only, in D160 footprint): `tests/nextorm.integration.tests/MariaDbJoinAliasIntegrationTests.cs:42`
`ids.Should().Equal(10, 20)` → `ids.Should().Equal(10)`. No product source changed. Re-run
(`integration.log`) is green with the same totals. This is a test-fixture defect, **not** a product
defect and **not** a DO→PLAN candidate.

## Artifacts

- `integration.log` (post-fix canonical, exit 0)
- `integration-results.xml` (post-fix machine-readable)
- `integration-prefix-fail.log`, `integration-results-prefix-fail.xml` (pre-fix failure evidence)
- `provider-inventory.txt`
