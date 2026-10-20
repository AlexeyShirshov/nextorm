# P03c screening verdict — flat COW root (candidate C)

**Screening, not closure.** This run only tests whether the P03a flat-root representation
(candidate C) meets the pre-registered screening bar. It does not close P03, E30/E31 or #297.
A failure returns to PLAN with P03 still active.

## Setup

- Protocol unchanged: `run_r4.py attribute --rounds 6 --order ABBA --warmup 5 --iters 30`,
  one process per block under `taskset -c 1`, `DOTNET_gcServer=0`, host lock
  `/tmp/opencode/nextorm-bdn-host.lock`, 60 s quiet window, benchmark DB sha
  `32bf10a0…72295` checked before/after every block (unchanged; 12/12 blocks exit 0).
- Baseline A = `0d4d5df2` (probe-a). Candidate C = product HEAD `c4b34178` (P03a flat root)
  plus the exact B probe set (probe-c). The runner still names its two sides A/B internally;
  every "B/C" number below is the **candidate C** side.
- Product diff B→C is only `git diff 9d633521..HEAD -- src`: 3 files, +206/−378,
  sha256 `b03ffcf8d194e489b3a0be11e870b274e181f9498bd7ab788c942bcb78000ea7`
  (`.pdca/T297/rv5/logs/product-diff-b-c-p03c.log`).
- Instrumentation of C and the shifted COW line numbers: `instrumentation-C.md` in this directory.
- Raw evidence: `phase-table.json`, `phase-table.md`, `reconciliation.md`, `runs/`, `meta.json`,
  `hashes.txt`; run log `.pdca/T297/rv5/logs/screening-accessors-p03c.log` (rc 0, wall 69.5 s).

## Per-arm end-to-end (C/A)

| arm | A ns/op | C ns/op | ratio | 95 % CI | e2e direction |
|---|---|---|---|---|---|
| cte | 125865.5 | 129807.3 | 1.0626 | [0.8997, 1.0809] | >1, matches anchor 1.076 |
| recursive | 121828.8 | 131394.2 | 1.0931 | [0.8170, 1.1129] | >1, matches anchor 1.101 |
| inlist | 118977.4 | 127008.1 | 1.0672 | [1.0200, 1.1216] | >1, matches anchor 1.082 |
| leftjoin | 83049.9 | 92067.6 | 1.1118 | [1.0755, 1.1300] | >1, matches anchor 1.104 |

The candidate never inverts the ABBA causal direction; all four baseline-relative e2e ratios
stay positive. (The inlist magnitude is 1.067 vs the 1.082 anchor, still the same direction.)

## Screening criterion

Bar for each named phase in each arm: **paired median C/A ratio ≤ 1.02 and CI upper bound
≤ 1.05**, with no displaced new unexplained cost, while the e2e direction still matches the
ABBA anchors.

| arm | phase | A ns/op | C ns/op | ratio | 95 % CI | share of e2e delta | bar |
|---|---|---|---|---|---|---|---|
| cte | planner_preamble | 47182.8 | 52739.3 | 1.1528 | [1.0067, 1.1741] | 89.1 % | FAIL |
| cte | planner_recipe | 46878.8 | 52415.3 | 1.1530 | [1.0070, 1.1745] | 88.6 % | FAIL |
| cte | plan | 977.4 | 1387.2 | 1.4172 | [1.2211, 1.4649] | 5.2 % | FAIL |
| cte | construct | 46208.2 | 44822.7 | 0.9894 | [0.8493, 1.0369] | −6.2 % | PASS |
| recursive | planner_preamble | 52547.3 | 58370.5 | 1.1123 | [0.9230, 1.1290] | 52.6 % | FAIL (median) |
| recursive | planner_recipe | 52246.1 | 57989.6 | 1.1127 | [0.9231, 1.1288] | 52.5 % | FAIL (median) |
| recursive | plan | 944.5 | 1354.2 | 1.4567 | [1.2216, 1.5126] | 3.8 % | FAIL |
| recursive | construct | 34000.2 | 36481.3 | 1.1077 | [0.7558, 1.1284] | 32.0 % | FAIL (median) |
| inlist | planner_preamble | 12300.7 | 13150.2 | 1.0567 | [1.0189, 1.1110] | 8.7 % | FAIL |
| inlist | planner_recipe | 12201.2 | 13014.0 | 1.0522 | [1.0160, 1.1089] | 7.9 % | FAIL |
| inlist | plan | 9400.5 | 11308.0 | 1.1962 | [1.1556, 1.2528] | 23.3 % | FAIL |
| inlist | construct | 19416.0 | 19876.1 | 1.0353 | [0.9841, 1.0999] | 8.4 % | FAIL (median) |
| inlist | prepare_pipeline | 26239.9 | 28887.1 | 1.1127 | [1.0569, 1.1768] | 36.9 % | FAIL |
| leftjoin | planner_preamble | 26950.7 | 32864.8 | 1.2340 | [1.2041, 1.2569] | 66.6 % | FAIL |
| leftjoin | planner_recipe | 26875.6 | 32764.0 | 1.2337 | [1.2044, 1.2568] | 66.3 % | FAIL |
| leftjoin | plan | 965.2 | 1401.1 | 1.4411 | [1.3730, 1.5301] | 4.6 % | FAIL |
| leftjoin | construct | 22080.6 | 23563.9 | 1.0686 | [0.9975, 1.0766] | 16.2 % | FAIL (median) |

`prepare_pipeline` is 0 in cte/recursive/leftjoin because those arms take the raw-recipe path
(`prepare_call = prepare_pipeline = 0`); only the InList arm exercises the `PrepareCommand`
pipeline, and there it fails the bar.

### Criterion outcome

- `planner_recipe` / `planner_preamble`: FAIL in all four arms (median 1.05–1.23, CI upper
  1.11–1.26).
- `plan`: FAIL in all four arms (median 1.20–1.46, CI upper 1.25–1.53).
- `construct`: PASS only in cte (0.9894; CI upper 1.0369); FAIL in recursive/inlist/leftjoin.
- InList `prepare_pipeline`: FAIL (1.1127; CI upper 1.1768).
- e2e direction: PASS (all ratios > 1, matching the ABBA anchors).
- **Overall: FAIL.** The flat-root representation does not meet the screening bar. The residual
  cost is concentrated in `planner_recipe` (52–89 % of the e2e gap in cte/recursive/leftjoin)
  and `prepare_pipeline` in InList (36.9 %), so this is not a displaced new phase.

## Reconciliation residual and overhead

| arm | unattributed residual Δ | share of e2e Δ | ratio [CI] |
|---|---|---|---|
| cte | +166.0 ns/op | 2.11 % | 1.0376 [0.9771, 1.1288] |
| recursive | +68.3 ns/op | 0.61 % | 1.0176 [0.8400, 1.0739] |
| inlist | +91.1 ns/op | 1.15 % | 1.0243 [0.9695, 1.1148] |
| leftjoin | +210.3 ns/op | 2.26 % | 1.0503 [1.0170, 1.0997] |

All residuals are ≤ 2.26 % of the e2e delta, so **no displaced unexplained cost** is observed;
the measured named phases account for the gap. Instrumentation overhead (pair cost × boundary
calls/op): A 714/1043/1647/1592 ns/op, C 956/1574/1742/1742 ns/op, delta +242/+531/+95/+150 ns/op
(cte/recursive/inlist/leftjoin). The C side carries 4–9 extra boundary pairs/op because of the
B-only COW counters; the overhead delta is an order of magnitude below the measured phase gaps.

## Decision

**P03c screening: FAIL** — the flat COW root does not bring `planner_recipe`/`planner_preamble`,
`plan`, `construct` or the InList `prepare_pipeline` within the screening bar. Return to PLAN;
P03 stays active and is **not** marked done. This artifact is screening evidence only, not
closure.
