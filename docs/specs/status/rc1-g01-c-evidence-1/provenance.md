# Provenance manifest — `rc1-g01-c-evidence-1`

Fresh collection-level evidence for group G01 (`1.0.9-rc1`). This manifest maps every execution recorded in
`docs/specs/status/rc1-g01-c-evidence-1.json` to its real command, revision, log/artifact path and counts.
No command was invented, no applicable execution omitted, no filter added retrospectively, and no broad
command was moved to obtain exit 0. The historical `/tmp/nextorm-D141-ContractA-strong/E141-evidence.json`
was **not modified**.

All recorded executions are **unfiltered**: the aggregate executions are full-solution (`nextorm.slnx`)
Debug/Release builds, a full-solution unit sweep, a full coverage collect, docfx and a full container
integration run; the PostgreSQL/MariaDB/core executions are unfiltered full-project runs **labeled
`targeted` in prose only** (below). **No `--filter` (or any other test selector) was applied to any
execution**; `scope.selectors` in the JSON names these aggregate/targeted executions rather than test
filters.

## Aggregate run at HEAD `36e540e2` (branch `1.0.9-rc1`)

Source: `/tmp/nextorm-rc1-coll-c/` and `docs/specs/status/collection-1.0.9-rc1.md:76-90`.

| ID | Command (argv) | Revision | Exit | Selected | Log / artifact |
|---|---|---|---|---|---|
| CE-P01 | `dotnet build nextorm.slnx -c Debug` | `36e540e2` | 0 | n/a (build) | `/tmp/nextorm-rc1-coll-c/03-build-debug.txt` — 0 Warning(s), 0 Error(s), 35.55 s |
| CE-P02 | `dotnet build nextorm.slnx -c Release` | `36e540e2` | 0 | n/a (build) | `/tmp/nextorm-rc1-coll-c/04-build-release.txt` — 0 Warning(s), 0 Error(s), 21.78 s |
| CE-P03 | `dotnet test nextorm.slnx -c Debug --no-build` | `36e540e2` | 0 | 8438 (5915 passed / 0 failed / 2523 skipped) | `/tmp/nextorm-rc1-coll-c/05-test-sln.txt` (summary at `:19968-19984`) |
| CE-P04 | `dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` (env `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) | `36e540e2` | 0 | 3191 (Errors 0 / Failed 0 / Skipped 193 / Not Run 0) | `/tmp/nextorm-rc1-coll-c/06-integration.txt`; discovery `/tmp/nextorm-rc1-coll-c/06b-list-tests.txt` |
| CE-P05 | `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` | `36e540e2` | 0 | 8438 (5915 / 0 / 2523) | `/tmp/nextorm-rc1-coll-c/08-coverage-collect.txt` |
| CE-P06 (aux) | `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura"` | `36e540e2` | 0 | n/a (report) | `tests/coverage/report/Summary.txt` — line 87.1% (45384/52077), branch 78.8% (23790/30166), method 77.6%; `/tmp/nextorm-rc1-coll-c/09-reportgen.txt` |
| CE-P07 | `dotnet docfx docs/docfx.json` | `36e540e2` | 0 | n/a (docs) | `/tmp/nextorm-rc1-coll-c/10-docfx.txt` — build succeeded with 2 warnings, 0 errors |
| CE-P08 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` | `36e540e2` | 0 | 7 acceptance benchmarks, 0 failures | `/tmp/nextorm-rc1-coll-c/11-perf-acceptance.txt` |
| CE-P00 (aux) | `dotnet tool restore` | `36e540e2` | 0 | n/a | `/tmp/nextorm-rc1-coll-c/07-tool-restore.txt` — dotnet-coverage 18.11.2, reportgenerator 5.5.11, docfx 2.78.5 |

**The single comprehensive boundary sweep at HEAD `36e540e2` is the container integration run CE-P04**
(3191 cases, all five required providers executed). CE-P03 unit and CE-P05 coverage are the aggregate unit
evidence; CE-P01/CE-P02 are the Debug/Release builds.

## Targeted D141 full-project sweeps (historical revision `815e0127+d141-do`)

These are **targeted** (prose label only) **unfiltered full-project** runs from the D141 strong cycle (r2);
no `--filter` was applied to them. Their real `source_revision` is
`815e0127+d141-do`, **NOT** HEAD `36e540e2` — recorded truthfully here. They are the historical three
boundary sweeps that the original D141 report (`/tmp/nextorm-D141-ContractA-strong/E141-evidence.json`)
recorded and that the validator's one-sweep cap rejected (`/tmp/nextorm-D141-ContractA-strong/validator.log`:
`FAIL: more than one comprehensive boundary test sweep: 3`). The original `/tmp` JSON is unchanged.

| ID | Command (argv) | Revision | Exit | Selected | Log / artifact |
|---|---|---|---|---|---|
| CE-P-T1 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build` | `815e0127+d141-do` | 0 | 756 (756 passed / 0 failed / 0 skipped) | `/tmp/nextorm-D141-ContractA-strong/r2/pg-full.log` |
| CE-P-T2 | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build` | `815e0127+d141-do` | 0 | 166 (166 passed / 0 failed / 0 skipped) | `/tmp/nextorm-D141-ContractA-strong/r2/mdb-full.log` |
| CE-P-T3 | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | `815e0127+d141-do` | 0 | 1524 (1524 passed / 0 failed / 0 skipped) | `/tmp/nextorm-D141-ContractA-strong/r2/core-full.log` |

The build supporting CE-P-T1..T3 is the D141 solution build recorded as an `inner`-supporting build in the
JSON (`dotnet build nextorm.slnx -c Debug`, `815e0127+d141-do`, exit 0) — taken from the historical D141
evidence set.

## Aggregate counts

- Debug/Release builds: 0 warnings / 0 errors each.
- Unit: 8438 total / 5915 passed / 0 failed / 2523 skipped.
- Container integration: 3191 total / 0 failed / 193 skipped (all five required providers executed).
- Coverage: line 87.1% (45384/52077 ≥ 85), branch 78.8% (23790/30166 ≥ 75), method 77.6%.
- Docs: 0 errors, 2 pre-existing duplicate-source warnings (`nextorm.core.sourcegenerator`).
- Perf acceptance: 7/7, 0 failures.

## Validator disposition

- Fresh re-run at corrective DO r=1, n=2/3 after `scope.selectors` was reconciled to name the
  unfiltered aggregate/targeted executions (no `--filter` added; no execution changed).
- `validate_inner_loop.py brief docs/specs/status/rc1-g01-c-evidence-1.json` → **EXIT=0** (no companion
  scope-only JSON was needed).
- `validate_inner_loop.py report docs/specs/status/rc1-g01-c-evidence-1.json` → **EXIT=2**, verbatim:
  - `FAIL: more than one comprehensive boundary test sweep: 4`
  - `FAIL: multiple boundary solution builds: 3`
- The first violation counts the aggregate solution unit sweep (CE-P03) plus the three historical targeted
  project sweeps (CE-P-T1..T3); the helper caps comprehensive boundary sweeps at one. The second counts the
  aggregate Debug (CE-P01), Release (CE-P02) and D141 (CE-P-T) solution builds; the helper caps boundary
  solution builds at one. Both are helper single-cycle caps applied to a collection aggregate plus the
  historical D141 plan's three full sweeps — not missing evidence. No execution was rewritten, omitted,
  reclassified or given a retrospective filter. Disposition is recorded in the collection waiver
  `W-C-D141-REPORT-CAP-1` (see `docs/specs/status/collection-1.0.9-rc1.md`).
