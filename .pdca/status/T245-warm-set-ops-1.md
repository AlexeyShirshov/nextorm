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
