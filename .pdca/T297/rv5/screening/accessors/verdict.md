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

e2e delta +7857.3 ns/op; full phase sum +7436.8; unattributed residual +166.0 (+2.1%); dispatch six-term residual +7771.1 (+98.9%); combined CI hw 13086.3 ns; reconciles=True; residual insignificant=True.

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
| planner_preamble | 1.1528 | [1.0067, 1.1741] | `++-+++` | 2/3 | 3/3 | +6997.0 | +89.1% |
| planner_recipe | 1.1530 | [1.0070, 1.1745] | `++-+++` | 2/3 | 3/3 | +6960.0 | +88.6% |
| plan | 1.4172 | [1.2211, 1.4649] | `++++++` | 3/3 | 3/3 | +405.3 | +5.2% |
| prepare_exclusive | 1.0704 | [1.0013, 1.1376] | `+-++++` | 3/3 | 2/3 | +92.6 | +1.2% |
| cow_shape | 1.8725 | [1.5814, 1.9990] | `++++++` | 3/3 | 3/3 | +45.8 | +0.6% |

Overhead: pair cost A 54.91 / B 56.21 ns; boundary calls/op A 13.00 / B 17.00; estimated overhead A 714 / B 956 ns/op (delta +242 ns/op).

Dominant prepare sub-step: **planner_preamble** (+6997.0 ns/op, +89.1% of the e2e delta).

Top prepare-level contributors: planner_preamble +6997.0 ns (+89.1%); planner_recipe +6960.0 ns (+88.6%); planner_gate +23.6 ns (+0.3%).

## recursive

e2e delta +11257.1 ns/op; full phase sum +11219.2; unattributed residual +68.3 (+0.6%); dispatch six-term residual +6490.4 (+57.7%); combined CI hw 20202.6 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | True | True | True | True | False |
| prepare_exclusive | False | False | True | True | True | False |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | True | True | True | True | False |
| execute | False | True | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | True | True | True | True | False |
| planner_preamble | False | True | True | True | True | False |
| planner_gate | False | False | True | True | True | False |
| planner_recipe | False | True | True | True | True | False |
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
| plan | 1.4567 | [1.2216, 1.5126] | `++++++` | 3/3 | 3/3 | +426.7 | +3.8% |
| cow_shape | 1.6882 | [1.4031, 1.9254] | `++++++` | 3/3 | 3/3 | +36.6 | +0.3% |

Overhead: pair cost A 54.91 / B 56.21 ns; boundary calls/op A 19.00 / B 28.00; estimated overhead A 1043 / B 1574 ns/op (delta +531 ns/op).

Dominant prepare sub-step: **planner_preamble** (+5921.7 ns/op, +52.6% of the e2e delta).

Top prepare-level contributors: planner_preamble +5921.7 ns (+52.6%); planner_recipe +5905.5 ns (+52.5%); planner_gate +5.3 ns (+0.0%).

## inlist

e2e delta +7922.7 ns/op; full phase sum +7690.1; unattributed residual +91.1 (+1.2%); dispatch six-term residual +6292.5 (+79.4%); combined CI hw 6609.9 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | True | True | True | True | False |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | True | True | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | False | True | True | False |
| get_db_command | True | True | True | True | True | True |
| execute | False | True | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | False | True | True | True | False |
| planner_preamble | True | True | True | True | True | True |
| planner_gate | True | True | True | True | True | True |
| planner_recipe | True | True | True | True | True | True |
| prepare_pipeline | True | True | True | True | True | True |
| prep_preamble | True | True | True | True | True | True |
| nav_expand | True | True | True | True | True | True |
| get_from | False | False | True | True | True | False |
| prepare_from | False | False | True | True | True | False |
| prepare_join | True | True | True | True | True | True |
| prepare_columns | True | True | True | True | True | True |
| prepare_where | False | True | True | True | True | False |
| prepare_prewhere | False | False | True | True | True | False |
| prepare_arrayjoin | False | False | True | True | True | False |
| prepare_grouping | True | True | True | True | True | True |
| prepare_sorting | False | False | True | True | True | False |
| prepare_union | True | True | True | True | True | True |
| prepare_ctes | True | True | True | True | True | True |
| prepare_hints | False | False | True | True | True | False |
| unkeyed_scans | True | True | True | True | True | True |
| from_hash | True | True | True | True | True | True |

Top contributors with a positive delta and a 95 % CI excluding 0 (per-round sign string: round 1..6, `+` = ratio > 1):

| component | B/A ratio | 95 % CI | signs | A→B >1 | B→A >1 | delta ns/op | share of e2e delta |
|---|---|---|---|---|---|---|---|
| prepare_pipeline | 1.1127 | [1.0569, 1.1768] | `++++++` | 3/3 | 3/3 | +2922.6 | +36.9% |
| plan | 1.1962 | [1.1556, 1.2528] | `++++++` | 3/3 | 3/3 | +1844.7 | +23.3% |
| planner_preamble | 1.0567 | [1.0189, 1.1110] | `++++++` | 3/3 | 3/3 | +689.0 | +8.7% |
| planner_recipe | 1.0522 | [1.0160, 1.1089] | `++++++` | 3/3 | 3/3 | +628.9 | +7.9% |
| prepare_columns | 1.0582 | [1.0071, 1.1383] | `++-+++` | 2/3 | 3/3 | +494.7 | +6.2% |
| prep_preamble | 1.8969 | [1.7683, 2.0105] | `++++++` | 3/3 | 3/3 | +450.5 | +5.7% |
| unkeyed_scans | 1.2210 | [1.1507, 1.2966] | `++++++` | 3/3 | 3/3 | +342.4 | +4.3% |
| prepare_grouping | 1.4544 | [1.3178, 1.6692] | `++++++` | 3/3 | 3/3 | +186.9 | +2.4% |

Overhead: pair cost A 54.91 / B 56.21 ns; boundary calls/op A 30.00 / B 31.00; estimated overhead A 1647 / B 1742 ns/op (delta +95 ns/op).

No single prepare sub-step dominates (largest **prepare_pipeline**, +2922.6 ns/op, +36.9% of the e2e delta).

Top prepare-level contributors: prepare_pipeline +2922.6 ns (+36.9%); planner_preamble +689.0 ns (+8.7%); planner_recipe +628.9 ns (+7.9%); prepare_columns +494.7 ns (+6.2%).

## leftjoin

e2e delta +9289.0 ns/op; full phase sum +9004.9; unattributed residual +210.3 (+2.3%); dispatch six-term residual +7016.8 (+75.5%); combined CI hw 2573.6 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | True | True | True | True | False |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | False | True | True | True | False |
| execute | True | True | True | True | True | True |
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
| planner_preamble | 1.2340 | [1.2041, 1.2569] | `++++++` | 3/3 | 3/3 | +6182.3 | +66.6% |
| planner_recipe | 1.2337 | [1.2044, 1.2568] | `++++++` | 3/3 | 3/3 | +6158.7 | +66.3% |
| execute | 1.0293 | [1.0079, 1.0531] | `++++++` | 3/3 | 3/3 | +498.5 | +5.4% |
| plan | 1.4411 | [1.3730, 1.5301] | `++++++` | 3/3 | 3/3 | +428.8 | +4.6% |
| prepare_exclusive | 1.1559 | [1.0909, 1.2235] | `++++++` | 3/3 | 3/3 | +140.3 | +1.5% |
| cow_shape | 1.8457 | [1.6845, 2.1001] | `++++++` | 3/3 | 3/3 | +44.3 | +0.5% |
| planner_gate | 1.2150 | [1.0945, 1.3935] | `++++++` | 3/3 | 3/3 | +17.9 | +0.2% |

Overhead: pair cost A 54.91 / B 56.21 ns; boundary calls/op A 29.00 / B 31.00; estimated overhead A 1592 / B 1742 ns/op (delta +150 ns/op).

Dominant prepare sub-step: **planner_preamble** (+6182.3 ns/op, +66.6% of the e2e delta).

Top prepare-level contributors: planner_preamble +6182.3 ns (+66.6%); planner_recipe +6158.7 ns (+66.3%); planner_gate +17.9 ns (+0.2%).

**attribution: plan** — satisfies (a)-(e) in all four arms.
