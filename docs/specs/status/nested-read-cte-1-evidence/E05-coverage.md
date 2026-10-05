# E05 — build + coverage (C1, C3, C4, C5, refreshed DO r2 n1 D6.4), issue #166

All commands under `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0`, sequential (no parallel test
projects), `timeout 900`.

## C1 — full solution build (rerun r2 D6.4)

Args (exact): `["dotnet", "build", "nextorm.slnx", "-c", "Debug"]`.

- exit code = **0**; `Build succeeded.`; **0 Warning(s) 0 Error(s)**; elapsed 6.57 s
- log: `docs/specs/status/nested-read-cte-1-evidence/C1-build.log`

## C3 — core tests (rerun r2 D6.4)

- class filter `--filter "FullyQualifiedName~Iteration14CteLookupTests"`: exit **0**, total **12**,
  succeeded **12**, failed **0**, skipped **0** (`C3-class.log`).
- full core: `dotnet test tests/nextorm.core.tests -c Debug`: exit **0**, total **1524**,
  succeeded **1524**, failed **0**, skipped **0** (`C3-full-core.log`).

## SQLite full project (rerun r2 D6.4)

`["dotnet", "test", "tests/nextorm.sqlite.tests", "-c", "Debug"]`: exit **0**, total **1013**,
succeeded **1012**, failed **0**, skipped **1** (pre-existing `SqliteRowIdLobProbeTests`,
`NEXTORM_LOB_SQLITE_PROBE` gate) — `C3-full-sqlite.log`. The two r2 D6.1 terminal tests are included
and selected: `--filter "...NestedReadCte_SyncTerminal_Executes|...NestedReadCte_AsyncTerminal_Executes"`
→ exit **0**, total **2**, succeeded **2**, skipped **0** (`SA-READ-combined.log`).

## C4 — coverage collection

Resolved from `.github/workflows/dotnet.yml` (`dotnet-coverage collect -s coverage.settings.xml -f
cobertura -o ... "dotnet test --no-build --verbosity normal"`).

**r2 D6.4 refresh: SQLite only** (core/postgres/sqlserver product+tests unchanged in r2, so their rv=1
collections are retained with provenance — see timestamps below):

```
["dotnet", "tool", "run", "dotnet-coverage", "collect", "-s", "coverage.settings.xml", "-f", "cobertura", "-o", "docs/specs/status/nested-read-cte-1-evidence/cov-sqlite.cobertura.xml", "dotnet test tests/nextorm.sqlite.tests --no-build --verbosity normal"]
```

| Project | provenance | total | succeeded | failed | skipped | exit | log |
|---------|-----------|-------|-----------|--------|---------|------|-----|
| `nextorm.core.tests` | retained rv=1 (2026-10-05 18:01) | 1524 | 1524 | 0 | 0 | 0 | `C4-core.log` |
| `nextorm.sqlite.tests` | **refreshed r2 D6.4** (18:24) | 1013 | 1012 | 0 | 1 | 0 | `C4-sqlite.log` |
| `nextorm.postgres.tests` | retained rv=1 (18:03) | 742 | 742 | 0 | 0 | 0 | `C4-postgres.log` |
| `nextorm.sqlserver.tests` | retained rv=1 (18:03) | 556 | 556 | 0 | 0 | 0 | `C4-sqlserver.log` |

SQLite collect exit **0**, cobertura nonempty (8,725,406 bytes, 18:24). No database, no containers.

## C5 — reportgenerator (combined, rerun r2 D6.4)

```
["dotnet", "tool", "run", "reportgenerator", "-reports:<4 cobertura files>", "-targetdir:docs/specs/status/nested-read-cte-1-evidence/c5-report", "-reporttypes:Html;TextSummary;Cobertura", "-riskhotspotassemblyfilters:+nextorm.*"]
```

- exit code = **0**; `Parser: MultiReport (4x Cobertura)`, Assemblies = 4, Classes = 553, Files = 322
- report: `docs/specs/status/nested-read-cte-1-evidence/c5-report/Summary.txt`; log: `C5-reportgenerator.log`

| Assembly | Line coverage | Branch coverage |
|----------|---------------|-----------------|
| `nextorm.core` | 82.2 % | 74.4 % |
| `nextorm.postgres` | 77.3 % | 74.2 % |
| `nextorm.sqlite` | 82.6 % | 58.9 % |
| `nextorm.sqlserver` | 77.5 % | 75.3 % |
| **overall** | **82.0 %** (42295/51555) | **74.2 %** (21816/29392) |

Changed getter `NextORM.Core.QueryCommand.get_HasDataModifyingCte()`: **line 100 % (22/22),
branch 100 %** (`c5-report/nextorm.core_QueryCommand.html:153`). SQLite figures are essentially
unchanged (82.6/58.9): the new terminal tests execute the already-covered getter/terminal path.

Policy verdict (branch `1.0.9-b`, **not** `main`): below the hard thresholds `85 / 75`; per
`.github/workflows/dotnet.yml` this is a **warning only** off `main`.

## Retained (NOT rerun in r2; product behavior unchanged since their green run)

- C6 acceptance — valid, `E06-perf.md` / `E06-acceptance.log` provenance (DO r1).
- C7 `eng/perf/iteration14_gate.py` — valid, `E07-gate.md` / `E07-gate.log` provenance (DO r1).
