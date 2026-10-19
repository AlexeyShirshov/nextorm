# E22 attribution verdict — T297 r=4 — P02b prepare-pipeline decomposition

Pre-registered rule (written in the runner before execution):

A phase is named as the cause only if **all** hold, in **every** arm:

1. (a) its paired B/A ratio median > 1 with bootstrap 95 % CI lower bound > 1;
2. (b) it reproduces in both ABBA pair directions — at least 2 of 3 A→B pairs > 1 and at least 2 of 3 B→A pairs > 1, and at least 5 of 6 pairs > 1 (per-round signs are in phase-table.md);
3. (c) its recorded boundary `file:line` resolves (Roslyn table) and its call count is non-zero in both trees;
4. (d) every measured exclusive phase reconciles with the e2e delta: the full phase sum (six exclusive phases plus the nested plan/bind/enum_create/cow_shape/prepare_pipeline/planner_gate/planner_recipe added back) differs from the e2e delta by no more than the combined 95 % CI half-width (quadrature);
5. (e) the unexplained positive residual is insignificant: `residual_unattributed` ≤ 0 or ≤ 5 % of the positive e2e delta.

Amendment recorded before the verdict was issued (P02): the dispatch's six-term `e2e_phases` omits exactly
the nested phases that its own `prepare_exclusive` definition subtracted, so the six-term residual contains
measured work by construction. The full phase sum adds them back; the six-term residual is still reported
as `phase_residual`. No threshold in (a), (b), (c) or (e) changed after the run.

Amendment 2 recorded before the P02b run (refined attribution): `prepare_exclusive` is re-derived as
`prepare − (plan + bind + enum_create + cow_shape + prepare_pipeline + planner_preamble)`, i.e. the whole
`PrepareCommand` pipeline and the planner preamble are measured separately instead of sitting inside the
exclusive remainder; `planner_preamble` is the sum of its two prescribed timers (`planner_gate`, `planner_recipe`).
The `Prepare` sub-steps are nested inside `prepare_pipeline`, so they are candidates for (a)-(c)/(e) but are
never added back to the full phase sum (that would double-count). The P02 derivation stays available as
`legacy_prepare_exclusive`. Protocols, thresholds and the verdict rule itself are unchanged.

Otherwise the verdict is `attribution: incomplete` with the numbers, and no cause is named.

## cte

e2e delta +14382.6 ns/op; full phase sum +13932.7; unattributed residual +341.8 (+2.4%); dispatch six-term residual +10355.2 (+72.0%); combined CI hw 13262.8 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | False | True | True | True | False |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | False | True | True | True | False |
| execute | False | False | True | True | True | False |
| read_rows | False | True | True | True | True | False |
| map_rows | False | False | True | True | True | False |
| planner_preamble | True | True | True | True | True | True |
| planner_gate | True | True | True | True | True | True |
| planner_recipe | True | True | True | True | True | True |
| prepare_pipeline | False | False | False | True | True | False |
| prep_preamble | False | False | False | True | True | False |
| nav_expand | False | False | False | True | True | False |
| get_from | False | False | False | True | True | False |
| prepare_from | False | False | False | True | True | False |
| prepare_join | False | False | False | True | True | False |
| prepare_columns | False | False | False | True | True | False |
| prepare_where | False | False | False | True | True | False |
| prepare_prewhere | False | False | False | True | True | False |
| prepare_arrayjoin | False | False | False | True | True | False |
| prepare_grouping | False | False | False | True | True | False |
| prepare_sorting | False | False | False | True | True | False |
| prepare_union | False | False | False | True | True | False |
| prepare_ctes | False | False | False | True | True | False |
| prepare_hints | False | False | False | True | True | False |
| unkeyed_scans | False | False | False | True | True | False |
| from_hash | False | False | False | True | True | False |

Top contributors with a positive delta and a 95 % CI excluding 0 (per-round sign string: round 1..6, `+` = ratio > 1):

| component | B/A ratio | 95 % CI | signs | A→B >1 | B→A >1 | delta ns/op | share of e2e delta |
|---|---|---|---|---|---|---|---|
| planner_preamble | 1.1887 | [1.0675, 1.2404] | `++++++` | 3/3 | 3/3 | +9506.9 | +66.1% |
| planner_recipe | 1.1895 | [1.0669, 1.2405] | `++++++` | 3/3 | 3/3 | +9481.2 | +65.9% |
| plan | 1.5168 | [1.2075, 1.5826] | `++++++` | 3/3 | 3/3 | +522.4 | +3.6% |
| prepare_exclusive | 1.0778 | [1.0283, 1.1626] | `++++++` | 3/3 | 3/3 | +103.5 | +0.7% |
| planner_gate | 1.2086 | [1.0246, 1.2372] | `+-++++` | 3/3 | 2/3 | +66.7 | +0.5% |
| cow_shape | 1.4280 | [1.0455, 1.6327] | `++-+++` | 2/3 | 3/3 | +30.4 | +0.2% |

Overhead: pair cost A 60.57 / B 61.01 ns; boundary calls/op A 29.00 / B 33.00; estimated overhead A 1757 / B 2013 ns/op (delta +257 ns/op).

Dominant prepare sub-step: **planner_preamble** (+9506.9 ns/op, +66.1% of the e2e delta).

Top prepare-level contributors: planner_preamble +9506.9 ns (+66.1%); planner_recipe +9481.2 ns (+65.9%); planner_gate +66.7 ns (+0.5%).

## recursive

e2e delta +17623.8 ns/op; full phase sum +17383.0; unattributed residual +290.5 (+1.6%); dispatch six-term residual +9312.9 (+52.8%); combined CI hw 7039.8 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | False | True | True | True | False |
| execute | False | True | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | False | True | True | True | False |
| planner_preamble | True | True | True | True | True | True |
| planner_gate | True | True | True | True | True | True |
| planner_recipe | True | True | True | True | True | True |
| prepare_pipeline | False | False | False | True | True | False |
| prep_preamble | False | False | False | True | True | False |
| nav_expand | False | False | False | True | True | False |
| get_from | False | False | False | True | True | False |
| prepare_from | False | False | False | True | True | False |
| prepare_join | False | False | False | True | True | False |
| prepare_columns | False | False | False | True | True | False |
| prepare_where | False | False | False | True | True | False |
| prepare_prewhere | False | False | False | True | True | False |
| prepare_arrayjoin | False | False | False | True | True | False |
| prepare_grouping | False | False | False | True | True | False |
| prepare_sorting | False | False | False | True | True | False |
| prepare_union | False | False | False | True | True | False |
| prepare_ctes | False | False | False | True | True | False |
| prepare_hints | False | False | False | True | True | False |
| unkeyed_scans | False | False | False | True | True | False |
| from_hash | False | False | False | True | True | False |

Top contributors with a positive delta and a 95 % CI excluding 0 (per-round sign string: round 1..6, `+` = ratio > 1):

| component | B/A ratio | 95 % CI | signs | A→B >1 | B→A >1 | delta ns/op | share of e2e delta |
|---|---|---|---|---|---|---|---|
| planner_preamble | 1.1474 | [1.1084, 1.1862] | `++++++` | 3/3 | 3/3 | +8613.1 | +48.9% |
| planner_recipe | 1.1474 | [1.1083, 1.1865] | `++++++` | 3/3 | 3/3 | +8567.7 | +48.6% |
| construct | 1.1556 | [1.0499, 1.1901] | `++++++` | 3/3 | 3/3 | +5611.3 | +31.8% |
| plan | 1.4469 | [1.3597, 1.6109] | `++++++` | 3/3 | 3/3 | +460.2 | +2.6% |
| prepare_exclusive | 1.0828 | [1.0488, 1.1478] | `++++++` | 3/3 | 3/3 | +107.4 | +0.6% |
| planner_gate | 1.1288 | [1.0615, 1.1954] | `++++++` | 3/3 | 3/3 | +40.1 | +0.2% |
| cow_shape | 1.4572 | [1.3450, 2.4839] | `++++++` | 3/3 | 3/3 | +31.1 | +0.2% |

Overhead: pair cost A 60.57 / B 61.01 ns; boundary calls/op A 41.00 / B 50.00; estimated overhead A 2483 / B 3050 ns/op (delta +567 ns/op).

No single prepare sub-step dominates (largest **planner_preamble**, +8613.1 ns/op, +48.9% of the e2e delta).

Top prepare-level contributors: planner_preamble +8613.1 ns (+48.9%); planner_recipe +8567.7 ns (+48.6%); planner_gate +40.1 ns (+0.2%).

## inlist

e2e delta +12922.4 ns/op; full phase sum +12685.0; unattributed residual +141.1 (+1.1%); dispatch six-term residual +9718.4 (+75.2%); combined CI hw 8824.2 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | False | True | True | True | False |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | True | True | True | True | True | True |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | False | True | True | False |
| get_db_command | False | False | True | True | True | False |
| execute | False | True | True | True | True | False |
| read_rows | False | True | True | True | True | False |
| map_rows | False | False | True | True | True | False |
| planner_preamble | False | True | True | True | True | False |
| planner_gate | True | True | True | True | True | True |
| planner_recipe | False | True | True | True | True | False |
| prepare_pipeline | True | True | True | True | True | True |
| prep_preamble | True | True | True | True | True | True |
| nav_expand | False | False | True | True | True | False |
| get_from | True | True | True | True | True | True |
| prepare_from | True | True | True | True | True | True |
| prepare_join | True | True | True | True | True | True |
| prepare_columns | True | True | True | True | True | True |
| prepare_where | True | True | True | True | True | True |
| prepare_prewhere | False | False | True | True | True | False |
| prepare_arrayjoin | False | False | True | True | True | False |
| prepare_grouping | True | True | True | True | True | True |
| prepare_sorting | False | False | True | True | True | False |
| prepare_union | True | True | True | True | True | True |
| prepare_ctes | True | True | True | True | True | True |
| prepare_hints | True | True | True | True | True | True |
| unkeyed_scans | True | True | True | True | True | True |
| from_hash | False | True | True | True | True | False |

Top contributors with a positive delta and a 95 % CI excluding 0 (per-round sign string: round 1..6, `+` = ratio > 1):

| component | B/A ratio | 95 % CI | signs | A→B >1 | B→A >1 | delta ns/op | share of e2e delta |
|---|---|---|---|---|---|---|---|
| prepare_pipeline | 1.1726 | [1.0919, 1.2087] | `++++++` | 3/3 | 3/3 | +4741.6 | +36.7% |
| plan | 1.2302 | [1.1787, 1.2440] | `++++++` | 3/3 | 3/3 | +2292.9 | +17.7% |
| prepare_where | 1.1243 | [1.0388, 1.1849] | `++++++` | 3/3 | 3/3 | +1535.0 | +11.9% |
| bind | 1.0875 | [1.0006, 1.1319] | `-+++++` | 2/3 | 3/3 | +1299.8 | +10.1% |
| prepare_columns | 1.1157 | [1.0445, 1.1349] | `-+++++` | 2/3 | 3/3 | +1000.5 | +7.7% |
| prep_preamble | 1.8869 | [1.8005, 1.9913] | `++++++` | 3/3 | 3/3 | +471.5 | +3.6% |
| unkeyed_scans | 1.2060 | [1.1335, 1.3031] | `++++++` | 3/3 | 3/3 | +346.0 | +2.7% |
| prepare_grouping | 1.5019 | [1.3731, 1.7283] | `++++++` | 3/3 | 3/3 | +213.1 | +1.6% |

Overhead: pair cost A 60.57 / B 61.01 ns; boundary calls/op A 36.00 / B 37.00; estimated overhead A 2180 / B 2257 ns/op (delta +77 ns/op).

No single prepare sub-step dominates (largest **prepare_pipeline**, +4741.6 ns/op, +36.7% of the e2e delta).

Top prepare-level contributors: prepare_pipeline +4741.6 ns (+36.7%); prepare_where +1535.0 ns (+11.9%); planner_preamble +1082.8 ns (+8.4%); planner_recipe +1027.2 ns (+7.9%).

## leftjoin

e2e delta +12195.4 ns/op; full phase sum +11828.6; unattributed residual +300.9 (+2.5%); dispatch six-term residual +8500.4 (+69.7%); combined CI hw 7063.9 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | True | True | True | True | False |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | False | True | True | True | False |
| execute | False | True | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | False | True | True | True | False |
| planner_preamble | True | True | True | True | True | True |
| planner_gate | False | True | True | True | True | False |
| planner_recipe | True | True | True | True | True | True |
| prepare_pipeline | False | False | False | True | True | False |
| prep_preamble | False | False | False | True | True | False |
| nav_expand | False | False | False | True | True | False |
| get_from | False | False | False | True | True | False |
| prepare_from | False | False | False | True | True | False |
| prepare_join | False | False | False | True | True | False |
| prepare_columns | False | False | False | True | True | False |
| prepare_where | False | False | False | True | True | False |
| prepare_prewhere | False | False | False | True | True | False |
| prepare_arrayjoin | False | False | False | True | True | False |
| prepare_grouping | False | False | False | True | True | False |
| prepare_sorting | False | False | False | True | True | False |
| prepare_union | False | False | False | True | True | False |
| prepare_ctes | False | False | False | True | True | False |
| prepare_hints | False | False | False | True | True | False |
| unkeyed_scans | False | False | False | True | True | False |
| from_hash | False | False | False | True | True | False |

Top contributors with a positive delta and a 95 % CI excluding 0 (per-round sign string: round 1..6, `+` = ratio > 1):

| component | B/A ratio | 95 % CI | signs | A→B >1 | B→A >1 | delta ns/op | share of e2e delta |
|---|---|---|---|---|---|---|---|
| planner_preamble | 1.2634 | [1.1439, 1.2932] | `++++++` | 3/3 | 3/3 | +7621.0 | +62.5% |
| planner_recipe | 1.2632 | [1.1431, 1.2933] | `++++++` | 3/3 | 3/3 | +7579.6 | +62.2% |
| plan | 1.4857 | [1.3565, 1.6457] | `++++++` | 3/3 | 3/3 | +498.7 | +4.1% |
| prepare_exclusive | 1.1057 | [1.0008, 1.2209] | `-+++++` | 2/3 | 3/3 | +101.7 | +0.8% |
| cow_shape | 1.2707 | [1.1998, 1.4290] | `++++++` | 3/3 | 3/3 | +18.9 | +0.2% |

Overhead: pair cost A 60.57 / B 61.01 ns; boundary calls/op A 42.00 / B 44.00; estimated overhead A 2544 / B 2684 ns/op (delta +140 ns/op).

Dominant prepare sub-step: **planner_preamble** (+7621.0 ns/op, +62.5% of the e2e delta).

Top prepare-level contributors: planner_preamble +7621.0 ns (+62.5%); planner_recipe +7579.6 ns (+62.2%); planner_gate +29.8 ns (+0.2%).

**attribution: plan, prepare_exclusive** — satisfies (a)-(e) in all four arms.
