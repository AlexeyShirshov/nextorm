# D176.5 r2-n1 — integration execution ledger

> **PRE-LOOPBACK (n=1, r=2)** — historical D176.5 run (total 3476 / JSON 159). **Current is
> `../loopback/provider-counts.md` (n=2, total 3511 / JSON 194).**

## Comprehensive container sweep (C04)

Command (run from repo root, socket verified in `setup-recovery.log`):

```
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml <this-dir>/c04-sweep.xml
```

Result (stdout `c04-sweep.log`, per-test `c04-sweep.xml`): **exit 0**,
`Total: 3476, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0`.

Per-provider counts are parsed from `c04-sweep.xml` (each `<test>` result), so
they are the actual executed-test counts, not discovery method counts:

| Provider | total | passed (executed) | failed | skipped |
|---|---|---|---|---|
| SQLite | 742 | **702** | 0 | 40 |
| PostgreSQL | 865 | **839** | 0 | 26 |
| SQL Server | 752 | **704** | 0 | 48 |
| MySQL | 692 | **612** | 0 | 80 |
| MariaDB | 76 | **76** | 0 | 0 |
| ClickHouse | 225 | **225** | 0 | 0 |
| provider-agnostic (`other`: in-memory/EF/Lob) | 124 | 121 | 0 | 3 |
| **total** | **3476** | **3279** | **0** | **197** |

All six required providers executed a positive number of tests; no required
provider ran zero tests. The 197 skips are the suite's declared provider-capability
skips (e.g. FULL JOIN on MySQL, APPLY/TVF on SQLite, LOB streaming on
ClickHouse/MySQL), not missing JSON coverage: the JSON-focused subset below has
**zero skips**.

## JSON-focused filtered subset (`WriteJson`)

Command:

```
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~WriteJson"
```

Result (`inner-integration-all-providers.txt`): **exit 0**,
`total 159, failed 0, succeeded 159, skipped 0`.

Per-provider JSON executed-test counts (discovered names matching `WriteJson`,
all of which passed in the subset run):

| Provider | JSON tests executed |
|---|---|
| SQLite | 27 |
| PostgreSQL | 28 |
| SQL Server | 27 |
| MySQL | 27 |
| MariaDB | 26 |
| ClickHouse | 24 |
| **total** | **159** |

Local inner-loop runs (no containers) recorded alongside:

- `inner-integration-sqlite.txt` — SQLite `WriteJson` subset: 27 passed / 0 failed / 0 skipped, exit 0.
- `core-writer.txt` — `JsonShapeWriterTests`: 34 passed / 0 failed / 0 skipped, exit 0.
- `sqlite-unit.txt` — SQLite `JsonStreamingTests`: 53 passed / 0 failed / 0 skipped, exit 0.
- `sqlserver-lowering.txt` — SQL Server `JsonStreamingSqlLoweringTests` (incl. the same-name-scopes regression): 3 passed / 0 failed / 0 skipped, exit 0.
- `build-slnx.txt` — `dotnet build nextorm.slnx -c Debug`: exit 0, 0 warnings / 0 errors.

## Defect fixed within this unit

`NextORM.Integration.Tests.SqlServerIntegrationTests.WriteJson_NestedSameNameDifferentScopes_ShouldBeAccepted`
failed with SQL Server `Ambiguous column name 'id'`: the JSON capture lowered
`new { x.Id, Child = new { x.Id } }` to `select id, id from complex_entity order by id`,
and T-SQL resolves `order by id` ambiguously when two output columns share the name.
Fixed in `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`
(`DisambiguateJsonLeafAliases`: every duplicate lowered leaf after the first gets a
distinct `__json_<ordinal>` output alias; the JSON property names stay scoped in the
captured descriptor). Regression: `SameNameLeavesAcrossScopes_ShouldAliasTheDuplicateOutput`
in `tests/nextorm.sqlserver.tests/JsonStreamingSqlLoweringTests.cs`.

## ClickHouse provider note

ClickHouse rejects duplicate output aliases in a whole-entity projection
(`MULTIPLE_EXPRESSIONS_FOR_ALIAS`), so the ClickHouse-local JSON slot fixture
(`json_slot_parent`/`json_slot_child`) uses distinct member names per side. The
same-name-across-slots scenario remains covered by the shared suite and MariaDB.
