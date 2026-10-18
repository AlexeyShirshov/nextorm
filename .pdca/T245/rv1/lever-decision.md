# T245 / D245-1 — narrow-lever decision (rv=1)

decision: insufficient
No permitted narrow lever was demonstrated. D245-3 remains blocked/unimplemented; production code was never modified.

## Acceptance (unchanged)

| Case | Per-query cap | Raw B/op cap | Ratio arm |
|---|---:|---:|---|
| EXCEPT | ≤5.05 KiB/query = 5,171.2 B/query | ≤51,712 B/op | ≤1.00× same-round linq2db |
| INTERSECT | ≤5.01 KiB/query = 5,130.24 B/query | ≤51,302.4 B/op | ≤1.00× same-round linq2db |

Per-case +20% noise band; prepared controls unchanged; no averaging of rounds or cases.

## Entry baseline (2/2 valid rounds)

| Case | nextorm B/op | nextorm B/query | linq2db B/query | Ratio | Required reduction |
|---|---:|---:|---:|---:|---:|
| EXCEPT | 65,281 | 6,528.1 | 5,176.2 | 1.2612 | −1,356.9 B/query (to cap) |
| INTERSECT | 65,121 | 6,512.1 | 5,128.2 | 1.2699 | −1,383.9 B/query (to 1.00×) |

Rounds: `entry-r1` admitted on provenance (docs/status-only commit, zero measured-path diff) and `entry-r2` valid; raw allocation is byte-identical across both rounds (65,281 / 65,121), so the per-query reference is the same. No round was averaged.

## Allocation closure (probe B/query; V0 residual 0.00)

| Category | EXCEPT | INTERSECT | Disposition |
|---|---:|---:|---|
| Operand shells | 1,632 | 1,632 | two commands; workload |
| Clone shell | 816 | 816 | mandatory — G4 |
| Caller lambdas | 1,072 | 1,072 | compiler-emitted workload |
| `QueryDefinition` records | 480 | 480 | 2 × 240 B |
| Plan comparers | 984 | 984 | 3 × 328 B lazy init |
| Remaining preparation/hash/lookup | 1,264 | 1,264 | frozen preparation |
| Execution | 808 | 792 | prepared controls 760 / 744 |
| **Total (V0)** | **7,056** | **7,040** | residual **0.00** |

BDN reconcile: prepare block 2,248 B/query matches BDN 2,248.12 (EXCEPT) / 2,248.10 (INTERSECT), delta −0.12 / −0.10 B (≤0.12 B). Probe-vs-BDN V0 delta is +527.9 B/query in both cases and is recorded as a cross-domain (probe process vs InProcessEmit) difference, not interchangeable with BDN totals.

## Candidate table

| Candidate | B/query | Blocker (`file:line`) | Available |
|---|---:|---|---:|
| Plan comparers | 984 | capture the owning command: `src/nextorm.core/Query/QueryCommand.Plan.cs:72-87`; fields `src/nextorm.core/Query/QueryPlanEqualityComparer.cs:15-19,28-36`, `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs:49-50`, `src/nextorm.core/Expressions/FromExpressionPlanEqualityComparer.cs:12,16,32`; registry resolution `src/nextorm.core/DataContext/QueryPlanner.cs:813-815,889`; identity assert `src/nextorm.core/DataContext/Cache/QueryPlan.cs:150`; clone propagation `src/nextorm.core/Query/QueryCommand.Clone.cs:126-131`; D240 protected list `docs/specs/status/D240-construction-allocation-1.md:139,326` | 0 |
| `QueryDefinition` → struct | 480 | `public sealed record QueryDefinition` `src/nextorm.core/Query/QueryDefinition.cs:18`; public members and public parameter positions `src/nextorm.core/Query/QueryCommand.TResult.cs:21`, `src/nextorm.core/DataContext/DataContextExtensions.cs:20`; a struct would be an R245-07 public-API change | 0 |
| Operand shells | 1,632 | clone-before-mutation is mandatory (G4): `src/nextorm.core/Query/QueryCommand.TResult.cs:1076,1108`, `src/nextorm.core/Query/QueryCommand.cs:1164-1182`; operands are the measured workload | 0 |
| Clone shell | 816 | same G4; `Clone()` is required for the reusable-operand contract, and no flag proves a command is non-reused | 0 |
| Frozen preparation | 1,264 | R245-13; frozen recipe/cache-key/equality; `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:91` rejects the raw-shape shortcut | 0 |
| Execution | 808 / 792 | outside the lever; the prepared control (760 / 744 B/query) already proves the lean execution path | 0 |

Candidate pool 1,464 B/query (comparers 984 + records 480) is formally above the absolute-cap delta, but it is below what the ≤1.00× ratio arm requires; that arm needs a shift of the preparation block itself.

## Guards — explicitly NOT violated

- **R245-07 (public API):** no public API change. `git status --porcelain src` was empty at every step; `api/entry.json` is the only surface export and no surface was edited.
- **R245-08 (operand immutability):** clone preserved. G4 facts recorded above; `NestedSetOperations_KeepOperandsUnchanged` passed (C-CORE 11/11, 0 failed).
- **R245-12 (per-case reporting):** EXCEPT and INTERSECT are reported separately here and in `attribution.json`; no averaged set-operation number exists.
- **R245-13 (frozen recipe):** recipe, cache-key and equality code untouched; there is no `src` diff to review because no production file was modified.

## Next revision requirements

- The gap remains −1,357 B/query (EXCEPT) and −1,384 B/query (INTERSECT), with ratios 1.26–1.27×.
- Prerequisite 1: comparer ownership redesign — shared/stateless comparers without capture of the owning `QueryCommand`.
- Prerequisite 2: explicit owner authorization for the D240-protected files and for the public `QueryDefinition` shape.
- Adjacent risk: the accepted T244 CTE residual; do not absorb it into a T245 claim.
- The deferred unit and milestone `1.0.9-rc3` are retained. D245-3 remains unauthorised until a revised PLAN demonstrates a permitted lever.

## Provenance

All paths are under `.pdca/T245/rv1/`: `bdn/entry-r1/`, `bdn/entry-r2/`, `bdn/controls-entry/`, `bdn/decompose-entry/`, `attribution/probe-variants.json`, `attribution/probe-variants2.json`, `tests/core-attribution2.log`, `api/entry.json`, `logs/entry-r2-quiet-window.txt`, `logs/controls-entry-quiet-runner.txt`, `logs/decompose-entry-quiet-runner.txt`, `logs/*-argv.txt`, `logs/*-head-*.txt`, `logs/*-binary-sha-*.txt`, `logs/db-sha256.txt`. Exact commands are recorded in those argv files and in the block logs; `attribution.json` carries the machine-readable assembly.
