# D226 rv1 — CHECK #2 fresh re-run (worktree /home/alex/sources/nextorm, branch 1.0.9-rc3)

All logs under `artifacts/pdca/D226/rv1/check2/`. No product/test/doc edits; no commits.

## 1. Build
`dotnet build nextorm.slnx -c Debug` → exit 0; `0 Warning(s)`, `0 Error(s)` (log `build.log`).

## 2. Filtered runs (`--no-build -c Debug`, no `-- -noColor` tail)
| args | filter | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| `dotnet test tests/nextorm.core.tests --no-build -c Debug` | `FullyQualifiedName~RecordsAffected` | 0 | 16 | 16 | 0 | 0 | `core-RecordsAffected.log` |
| `dotnet test tests/nextorm.sqlite.tests --no-build -c Debug` | `FullyQualifiedName~RawCommandTests\|FullyQualifiedName~RawResultSetTraversalTests\|FullyQualifiedName~BatchMultipleResultTests` | 0 | 94 | 94 | 0 | 0 | `sqlite-raw-batch.log` |
| `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests --no-build -c Debug` | `FullyQualifiedName~RecordsAffected` | 0 | 27 | 27 | 0 | 0 | `integration-RecordsAffected.log` |
| `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests --no-build -c Debug` | `FullyQualifiedName~ClickHouseRecordsAffected` | 0 | 5 | 5 | 0 | 0 | `integration-ClickHouseRecordsAffected.log` |

No capability skips on the ClickHouse surface (0 skipped). Container runs serialised.

## 3. Flake adjudication (2 failures inside the DO coverage-collect, `../logs/coverage.log`)
Class `NextORM.Integration.Tests.ClickHouseExtremeRowNativeSpecificTests` (untouched by T226 — `git status` clean for that file):
- `EmptyFilteredGlobal_ShouldReturnZeroRows_WhenSuppressed` (test line 502) — 2m 00s 029ms
- `NativeParity_ScalarAndObjectAndNoDefaultCtorProjections_ShouldMatchPortable` (test line 322) — 2m 00s 005ms

Both: `System.Threading.Tasks.TaskCanceledException : The request was canceled due to the configured HttpClient.Timeout of 120 seconds elapsing` through `ClickHouse.Driver…ClickHouseCommand.ExecuteDbDataReader` — HTTP timeout, not an assertion failure. DO run totals: total 11888 / failed 2 / succeeded 11687 / skipped 199; TestingPlatform exit code 2.
Fresh isolated re-runs (DOCKER_HOST, serial):
- class alone → exit 0, total 35, passed 35, failed 0, skipped 0 (10s 884ms) — `integration-ClickHouseExtreme-isolated.log`
- the two tests alone → exit 0, total 2, passed 2, failed 0 (6s 919ms) — `integration-ClickHouseExtreme-two-failing.log`
**Verdict: load-induced 120 s ClickHouse HTTP timeouts under the 12-project parallel coverage-collect; reproducible-green (exit 0) in isolation; not a real defect.**

## 4. Verifier / validator exit codes (verbatim)
- `verify-coverage.sh ../coverage.xml 85 75` → **0** (`Line coverage: 88.7%`, `Branch coverage: 80.8%`) — `verify-coverage.log`
- `verify-mutations.sh` → **0** (M1–M5 all `killed=yes`, `restored_byte_identical: True`) — `verify-mutations.log`
- `verify-plan-evidence.sh` → **0** — `verify-plan-evidence.log`
- `validate_inner_loop.py report ../evidence.json` → **0** — `validate-report.log`
- `validate_inner_loop.py manifest ../manifest.json` → **0** — `validate-manifest.log`
(DO's `mutation.log`/`mutation-report.json` backed up as `mutation.log.do-backup`/`mutation-report.json.do-backup` before re-running.)

## 5. Baseline fidelity probe (`/tmp/nextorm-d226-baseline/`)
- `tests/nextorm.core.tests/RecordsAffectedTests.cs` → **ABSENT** ✓
- four product files (`Builders/BatchBuilder.cs`, `DataContext/BatchRunner.cs`, `DataContext/CommandReaderOwner.cs`, `DataContext/ProcedureResult.cs`) → **all DIFFER** from working tree ✓
- `ReadRecordsAffected` → **absent** in baseline `src/` (present in current `src/`, T226-new) ✓
- provenance: the rv1 baseline XML came from `dotnet-coverage collect` running the full suite **inside** `/tmp/nextorm-d226-baseline` (`../baseline/baseline-coverage.log`; totals 11838/1/11638/199, exit 2)
- decisive pre-T226 signal: `NextORM.Core.CommandReaderOwner` instrumented lines = **42** (both baseline XMLs) vs **104** (final XML)

Occurrences in each XML:
| XML | `get_RecordsAffected` | `ReadRecordsAffected` | RecordsAffected total | lines covered/valid | branches covered/valid |
|---|---|---|---|---|---|
| `/tmp/nextorm-d226-baseline/tests/coverage/coverage.cobertura.xml` (stale 09:41) | 1 | 0 | 1 | 37438 / 43166 (86.73%) | 19875 / 25000 (79.5%) |
| `../baseline/coverage.cobertura.xml` (DO baseline, 18:10) | 1 | 0 | 1 | 38111 / 43249 (88.12%) | 20237 / 25176 (80.38%) |
| `../coverage.xml` (FINAL) | 3 | 1 | 4 | 38148 / 43286 (88.13%) | 20244 / 25182 (80.39%) |

reportgenerator (4 assemblies, coverage.settings.xml): baseline covered 52728 / coverable 59390 = 88.78%, branches 28489/35237 = 80.85%; final 52767/59429 = 88.78%, branches 28496/35243 = 80.86%. Δ = +39 covered lines, +7 branches (<0.01 pp) ⇒ the identical **88.7% / 80.8%** display is a **rounding coincidence**, not a measurement error.
**Explicitly: the baseline XML does NOT look like it measured the FINAL tree** — it has 0 `ReadRecordsAffected` and `CommandReaderOwner` = 42 instrumented lines (final = 104), and its log shows execution under `/tmp/nextorm-d226-baseline`.

## 6. Tree unchanged
`git status --porcelain` identical before/after (51 lines); `git diff --stat` identical (45 files changed, 1213 insertions(+), 326 deletions(-)); `CommandReaderOwner.cs` sha256 `cf030526…1b98`, `BatchRunner.cs` sha256 `9bf20094…3683` match the mutation script's post-restore. `artifacts/` is gitignored (`.gitignore:7`).

## Anomaly / contradiction
The baseline tree also carries a **stale** `tests/coverage/coverage.cobertura.xml` (09:41, 86.73%) which is NOT the DO baseline — the DO baseline is the 18:10 `rv1/baseline/coverage.cobertura.xml`. A checker reading the stale file would see different numbers; both, however, are genuinely pre-T226.
