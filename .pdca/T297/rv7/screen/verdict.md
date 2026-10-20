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

e2e delta +6811.6 ns/op; full phase sum +6993.6; unattributed residual -45.4 (-0.7%); dispatch six-term residual +6871.5 (+100.9%); combined CI hw 10389.3 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | False | True | True | True | False |
| prepare_exclusive | False | True | True | True | True | False |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | False | True | True | True | False |
| execute | False | False | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | True | True | True | True | False |
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
| planner_preamble | 1.1290 | [1.0744, 1.2070] | `++++++` | 3/3 | 3/3 | +6567.0 | +96.4% |
| planner_recipe | 1.1285 | [1.0737, 1.2063] | `++++++` | 3/3 | 3/3 | +6499.8 | +95.4% |
| plan | 1.5199 | [1.3788, 1.6139] | `++++++` | 3/3 | 3/3 | +535.1 | +7.9% |
| planner_gate | 1.2229 | [1.1633, 1.3153] | `++++++` | 3/3 | 3/3 | +69.3 | +1.0% |
| cow_shape | 1.3234 | [1.1517, 1.5297] | `++++++` | 3/3 | 3/3 | +22.7 | +0.3% |

Overhead: pair cost A 59.78 / B 59.58 ns; boundary calls/op A 29.00 / B 30.00; estimated overhead A 1734 / B 1787 ns/op (delta +54 ns/op).

Dominant prepare sub-step: **planner_preamble** (+6567.0 ns/op, +96.4% of the e2e delta).

Top prepare-level contributors: planner_preamble +6567.0 ns (+96.4%); planner_recipe +6499.8 ns (+95.4%); planner_gate +69.3 ns (+1.0%).

## recursive

e2e delta +8172.2 ns/op; full phase sum +8273.3; unattributed residual +46.2 (+0.6%); dispatch six-term residual +4847.3 (+59.3%); combined CI hw 8108.9 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | False | True | True | True | True | False |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | False | True | True | True | False |
| execute | False | False | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | True | True | True | True | True | True |
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
| construct | 1.1125 | [1.0631, 1.2152] | `++++++` | 3/3 | 3/3 | +4657.3 | +57.0% |
| planner_preamble | 1.0793 | [1.0506, 1.1580] | `++++++` | 3/3 | 3/3 | +4612.8 | +56.4% |
| planner_recipe | 1.0780 | [1.0500, 1.1563] | `++++++` | 3/3 | 3/3 | +4555.7 | +55.7% |
| plan | 1.4115 | [1.3610, 1.5170] | `++++++` | 3/3 | 3/3 | +429.6 | +5.3% |
| planner_gate | 1.2544 | [1.1734, 1.5333] | `++++++` | 3/3 | 3/3 | +80.5 | +1.0% |
| map_rows | 1.0331 | [1.0143, 1.0723] | `++++++` | 3/3 | 3/3 | +68.7 | +0.8% |
| cow_shape | 1.2675 | [1.2093, 1.4535] | `++++++` | 3/3 | 3/3 | +18.0 | +0.2% |

Overhead: pair cost A 59.78 / B 59.58 ns; boundary calls/op A 41.00 / B 47.00; estimated overhead A 2451 / B 2800 ns/op (delta +349 ns/op).

Dominant prepare sub-step: **planner_preamble** (+4612.8 ns/op, +56.4% of the e2e delta).

Top prepare-level contributors: planner_preamble +4612.8 ns (+56.4%); planner_recipe +4555.7 ns (+55.7%); planner_gate +80.5 ns (+1.0%).

## inlist

e2e delta +5627.0 ns/op; full phase sum +5663.8; unattributed residual -89.7 (-1.6%); dispatch six-term residual +5470.1 (+97.2%); combined CI hw 13473.5 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | True | True | True | True | False |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | True | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | False | True | True | False |
| get_db_command | False | False | True | True | True | False |
| execute | False | False | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | True | True | True | True | False |
| planner_preamble | False | False | True | True | True | False |
| planner_gate | True | True | True | True | True | True |
| planner_recipe | False | False | True | True | True | False |
| prepare_pipeline | False | True | True | True | True | False |
| prep_preamble | True | True | True | True | True | True |
| nav_expand | False | False | True | True | True | False |
| get_from | False | False | True | True | True | False |
| prepare_from | False | False | True | True | True | False |
| prepare_join | False | False | True | True | True | False |
| prepare_columns | False | True | True | True | True | False |
| prepare_where | False | True | True | True | True | False |
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
| plan | 1.1916 | [1.0868, 1.3048] | `++++++` | 3/3 | 3/3 | +1941.2 | +34.5% |
| prep_preamble | 1.9387 | [1.6909, 2.1606] | `++++++` | 3/3 | 3/3 | +506.2 | +9.0% |
| unkeyed_scans | 1.2229 | [1.1240, 1.3922] | `++++++` | 3/3 | 3/3 | +361.7 | +6.4% |
| prepare_grouping | 1.7240 | [1.4640, 1.8143] | `++++++` | 3/3 | 3/3 | +281.3 | +5.0% |
| prepare_exclusive | 1.1602 | [1.0217, 1.2238] | `+-++++` | 3/3 | 2/3 | +149.8 | +2.7% |
| prepare_ctes | 1.3940 | [1.2727, 1.4899] | `++++++` | 3/3 | 3/3 | +123.7 | +2.2% |
| prepare_union | 2.5197 | [1.9820, 3.0466] | `++++++` | 3/3 | 3/3 | +65.6 | +1.2% |
| planner_gate | 1.3201 | [1.2427, 1.5553] | `++++++` | 3/3 | 3/3 | +38.7 | +0.7% |

Overhead: pair cost A 59.78 / B 59.58 ns; boundary calls/op A 36.00 / B 37.00; estimated overhead A 2152 / B 2204 ns/op (delta +52 ns/op).

Dominant prepare sub-step: **prepare_pipeline** (+2864.1 ns/op, +50.9% of the e2e delta).

Top prepare-level contributors: prepare_pipeline +2864.1 ns (+50.9%); prep_preamble +506.2 ns (+9.0%); prepare_where +446.2 ns (+7.9%); planner_preamble +444.4 ns (+7.9%).

## leftjoin

e2e delta +7763.2 ns/op; full phase sum +7811.3; unattributed residual +150.7 (+1.9%); dispatch six-term residual +5381.8 (+69.3%); combined CI hw 4082.8 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | True | True | True | True | False |
| prepare_exclusive | False | False | True | True | True | False |
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
| planner_preamble | 1.1605 | [1.1195, 1.1815] | `++++++` | 3/3 | 3/3 | +4707.5 | +60.6% |
| planner_recipe | 1.1598 | [1.1191, 1.1810] | `++++++` | 3/3 | 3/3 | +4676.0 | +60.2% |
| plan | 1.4569 | [1.3357, 1.5042] | `++++++` | 3/3 | 3/3 | +477.2 | +6.1% |
| planner_gate | 1.3446 | [1.1979, 1.4753] | `++++++` | 3/3 | 3/3 | +29.3 | +0.4% |
| cow_shape | 1.2527 | [1.2150, 1.3574] | `++++++` | 3/3 | 3/3 | +17.0 | +0.2% |

Overhead: pair cost A 59.78 / B 59.58 ns; boundary calls/op A 42.00 / B 42.00; estimated overhead A 2511 / B 2502 ns/op (delta -8 ns/op).

Dominant prepare sub-step: **planner_preamble** (+4707.5 ns/op, +60.6% of the e2e delta).

Top prepare-level contributors: planner_preamble +4707.5 ns (+60.6%); planner_recipe +4676.0 ns (+60.2%); planner_gate +29.3 ns (+0.4%).

**attribution: plan, planner_gate** — satisfies (a)-(e) in all four arms.
