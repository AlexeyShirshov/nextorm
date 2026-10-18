# T245 — Warm EXCEPT and INTERSECT allocation parity — live status

task: T245
issue: 245
issue_title: Warm EXCEPT and INTERSECT allocation parity
milestone: 1.0.9-rc3
cycle: 1
plan_revision: 1
evidence_contract_rv: 1
attempt: 1
max_attempts: 3
mode: autonomous
plan_file: docs/specs/status/D245-warm-set-ops-1.md
phase: DO
status: do_started
branch: 1.0.9-rc3
entry_head: 8c94207f
evidence_dir: .pdca/T245/rv1/
contract_input: .pdca/T245/rv1/contract.json
frozen_manifest: .pdca/T245/rv1/manifest.json
check_regather_budget: 2
check_regather_owner: check
created: 2026-10-18

## Units

| Unit | State | Notes |
|---|---|---|
| D245-1 | active | Entry evidence + attribution decision |
| D245-2 | pending | Narrow evidence admission and native-gate coverage |
| D245-3 | blocked on D245-1 lever decision | Measured narrow optimization and semantic guards |
| D245-4 | pending | Final measurement, documentation and evidence freeze |
| Deferred | deferred in 1.0.9-rc3 | Broad metadata/CTE redesign; requires a revised PLAN |

## Measurements

| # | Block | Artifact directory | State |
|---|---|---|---|
| 1 | entry-r1 | `.pdca/T245/rv1/bdn/entry-r1` | not started |
| 2 | entry-r2 | `.pdca/T245/rv1/bdn/entry-r2` | not started |
| 3 | final-r1 | `.pdca/T245/rv1/bdn/final-r1` | not started |
| 4 | final-r2 | `.pdca/T245/rv1/bdn/final-r2` | not started |
| 5 | controls-entry | `.pdca/T245/rv1/bdn/controls-entry` | not started |
| 6 | controls-final | `.pdca/T245/rv1/bdn/controls-final` | not started |
| 7 | decompose-entry | `.pdca/T245/rv1/bdn/decompose-entry` | not started |
| 8 | decompose-final | `.pdca/T245/rv1/bdn/decompose-final` | not started |
| 9 | native-final | `.pdca/T245/rv1/bdn/native-final` | not started |

## Gate rows

| Row | State |
|---|---|
| R245-01 | open |
| R245-02 | open |
| R245-03 | open |
| R245-04 | open |
| R245-05 | open |
| R245-06 | open |
| R245-07 | open |
| R245-08 | open |
| R245-09 | open |
| R245-10 | open |
| R245-11 | open |
| R245-12 | open |
| R245-13 | open |
| R245-14 | open |
| R245-15 | open |
| R245-16 | open |

## Evidence contract rv=1

Contract: `.pdca/T245/rv1/contract.json` (`contract_rv: 1`, `base_ref: 01815f3a`, `entry_head: 8c94207f`, `evidence_root: .pdca/T245/rv1`).

| Row | State |
|---|---|
| E245-01 | open |
| E245-02 | open |
| E245-03 | open |
| E245-04 | open |
| E245-05 | open |
| E245-06 | open |
| E245-07 | open |
| E245-08 | open |
| E245-09 | open |
| E245-10 | open |
| E245-11 | open |
| E245-12 | open |
| E245-13 | open |
| E245-14 | open |
| E245-15 | open |
| E245-16 | open |

## Progress log

- PLAN closed; DO started (2026-10-18). Re-anchor note appended; evidence root moved to .pdca/T245/rv1/.
- Bootstrap: scope validated; status file created; contract.json written.
- 2026-10-18 | DO | revision r | iteration 1/3 | scope-bootstrap validated (validate_inner_loop.py brief rc=0) | .pdca/T245/rv1/scope-bootstrap.json
- 2026-10-18 | DO | revision r | iteration 1/3 | contract.json written, 16 rows E245-01..E245-16, JSON parse rc=0 | .pdca/T245/rv1/contract.json
- 2026-10-18T16:08:26Z | DO | revision r=1 | iteration 1/3 | D245-1 preflight complete: env captured, DB pin match=2/2, C-BUILD 0 warnings/0 errors rc=0, C-CORE 211 passed/0 failed/0 skipped rc=0, C-SQL sqlite 740 / postgres 831 / sqlserver 607 / mariadb 263 passed, 0 failed, 0 skipped, rc=0 each; C-PY 108 passed/0 failed rc=0; quiet window median 19.73% p95 24.41% (not quiet, no foreign benchmark/testhost/coverage process); API entry 14 calls verbatim, six set-op methods resolved 1042/1058/1074/1090/1106/1122; scope rc=0 with rebuild deviation | .pdca/T245/rv1/measurement-environment.json
- Deviation D245-1/a: brief scope carried rebuild=affected with files=[], which `validate_inner_loop.py brief` rejects (rc=2, fictitious rebuild); recorded verbatim at .pdca/T245/rv1/logs/scope-brief-verbatim.json and corrected to rebuild=none (no compiled input changed in this step; compile coverage is the full Debug build gate), rc=0 | .pdca/T245/rv1/scope-d245-1-preflight.json
- Deviation D245-1/b: `python3 -m pytest` unavailable (`No module named pytest`); both perf test modules are stdlib unittest and document unittest as the primary runner, so they were executed as `python3 -m unittest test_t240_evidence` (12) and `test_t244_evidence` (96) from .pdca/scripts/perf/tests, rc=0 each; no install performed | .pdca/T245/rv1/tests/perf-tools.log
- Deviation D245-1/c: raw tool logs were captured as emitted and then normalized to CRLF per repo line-ending policy; no content changes. HEAD verified f82edf36 (branch 1.0.9-rc3), base 01815f3a exists.
- 2026-10-18 | DO | revision r=1 | iteration 1/3 | D245-1 instrumentation added: 10 arms in `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWarmDecompose.cs` — Except_{Construct,Prepare_NoHash,Prepare_Hash,Warm_PlanOnly,Warm_Reused} and Intersect_{...} (category Warm_Except / Warm_Intersect); 48 pre-existing arms verified byte-identical (block hash + LOC delta 0 each); build gate `dotnet build nextorm.slnx -c Debug` 0 Warning(s) 0 Error(s) rc=0; C-CORE `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~T240CommandConstructionTests` 11 passed / 0 failed / 0 skipped rc=0 | .pdca/T245/rv1/logs/decompose-arm-inventory.txt
- Deviation D245-1/d: the file carried no `BenchmarkCategory` on any pre-existing arm, so the 10 new arms introduce operation-scoped categories `Warm_Except` / `Warm_Intersect` (additive; existing arms/categories untouched). Harness discovery of the new arms is deferred to the entry round: no BenchmarkDotNet command was run in this step per the step instruction (compile-only), and the Debug build proves the methods compile into nextorm.benchmark.dll.
