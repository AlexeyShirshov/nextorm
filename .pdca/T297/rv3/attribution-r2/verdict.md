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

e2e delta +10750.1 ns/op; full phase sum +10681.0; unattributed residual +109.9 (+1.0%); dispatch six-term residual +8332.8 (+77.5%); combined CI hw 6531.0 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | True | True | True | True | False |
| prepare_exclusive | False | True | True | True | True | False |
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
| planner_preamble | 1.1697 | [1.1180, 1.2109] | `++++++` | 3/3 | 3/3 | +7702.5 | +71.7% |
| planner_recipe | 1.1690 | [1.1173, 1.2104] | `++++++` | 3/3 | 3/3 | +7621.8 | +70.9% |
| plan | 1.4546 | [1.4130, 1.5235] | `++++++` | 3/3 | 3/3 | +438.3 | +4.1% |
| planner_gate | 1.2520 | [1.2276, 1.3255] | `++++++` | 3/3 | 3/3 | +69.9 | +0.6% |
| cow_shape | 1.6283 | [1.5136, 1.7549] | `++++++` | 3/3 | 3/3 | +33.5 | +0.3% |

Overhead: pair cost A 54.08 / B 54.53 ns; boundary calls/op A 13.00 / B 17.00; estimated overhead A 703 / B 927 ns/op (delta +224 ns/op).

Dominant prepare sub-step: **planner_preamble** (+7702.5 ns/op, +71.7% of the e2e delta).

Top prepare-level contributors: planner_preamble +7702.5 ns (+71.7%); planner_recipe +7621.8 ns (+70.9%); planner_gate +69.9 ns (+0.6%).

## recursive

e2e delta +13043.1 ns/op; full phase sum +12966.4; unattributed residual +98.4 (+0.8%); dispatch six-term residual +7459.0 (+57.2%); combined CI hw 7017.0 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | True | True | True | True | True | True |
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
| planner_preamble | 1.1323 | [1.0762, 1.1628] | `++++++` | 3/3 | 3/3 | +6845.6 | +52.5% |
| planner_recipe | 1.1318 | [1.0752, 1.1626] | `++++++` | 3/3 | 3/3 | +6783.6 | +52.0% |
| construct | 1.1284 | [1.0625, 1.1963] | `++++++` | 3/3 | 3/3 | +4204.4 | +32.2% |
| plan | 1.5172 | [1.4138, 1.5713] | `++++++` | 3/3 | 3/3 | +471.0 | +3.6% |
| prepare_exclusive | 1.1906 | [1.0226, 1.2324] | `++++++` | 3/3 | 3/3 | +201.2 | +1.5% |
| planner_gate | 1.2302 | [1.1473, 1.3040] | `++++++` | 3/3 | 3/3 | +65.1 | +0.5% |
| cow_shape | 1.6188 | [1.4048, 1.7856] | `++++++` | 3/3 | 3/3 | +31.8 | +0.2% |
| get_db_command | 1.1303 | [1.0361, 1.7393] | `++++++` | 3/3 | 3/3 | +11.2 | +0.1% |

Overhead: pair cost A 54.08 / B 54.53 ns; boundary calls/op A 19.00 / B 28.00; estimated overhead A 1028 / B 1527 ns/op (delta +499 ns/op).

Dominant prepare sub-step: **planner_preamble** (+6845.6 ns/op, +52.5% of the e2e delta).

Top prepare-level contributors: planner_preamble +6845.6 ns (+52.5%); planner_recipe +6783.6 ns (+52.0%); planner_gate +65.1 ns (+0.5%).

## inlist

e2e delta +8779.4 ns/op; full phase sum +8567.7; unattributed residual +211.7 (+2.4%); dispatch six-term residual +7765.7 (+88.5%); combined CI hw 3504.2 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | True | True | True | True | True | True |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | False | True | True | False |
| get_db_command | False | False | True | True | True | False |
| execute | False | False | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | False | True | True | True | False |
| planner_preamble | True | True | True | True | True | True |
| planner_gate | True | True | True | True | True | True |
| planner_recipe | True | True | True | True | True | True |
| prepare_pipeline | True | True | True | True | True | True |
| prep_preamble | True | True | True | True | True | True |
| nav_expand | False | False | True | True | True | False |
| get_from | False | False | True | True | True | False |
| prepare_from | False | False | True | True | True | False |
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
| from_hash | True | True | True | True | True | True |

Top contributors with a positive delta and a 95 % CI excluding 0 (per-round sign string: round 1..6, `+` = ratio > 1):

| component | B/A ratio | 95 % CI | signs | A→B >1 | B→A >1 | delta ns/op | share of e2e delta |
|---|---|---|---|---|---|---|---|
| prepare_pipeline | 1.1334 | [1.1181, 1.1791] | `++++++` | 3/3 | 3/3 | +3410.1 | +38.8% |
| plan | 1.1929 | [1.1614, 1.2512] | `++++++` | 3/3 | 3/3 | +1762.1 | +20.1% |
| bind | 1.1014 | [1.0460, 1.1244] | `++++++` | 3/3 | 3/3 | +1367.7 | +15.6% |
| planner_preamble | 1.0737 | [1.0311, 1.0905] | `++++++` | 3/3 | 3/3 | +862.0 | +9.8% |
| prepare_columns | 1.1045 | [1.0601, 1.1538] | `++++++` | 3/3 | 3/3 | +838.0 | +9.5% |
| prepare_where | 1.0714 | [1.0289, 1.1078] | `++++++` | 3/3 | 3/3 | +829.0 | +9.4% |
| planner_recipe | 1.0705 | [1.0286, 1.0888] | `++++++` | 3/3 | 3/3 | +817.6 | +9.3% |
| construct | 1.0250 | [1.0064, 1.0525] | `++++++` | 3/3 | 3/3 | +480.1 | +5.5% |

Overhead: pair cost A 54.08 / B 54.53 ns; boundary calls/op A 30.00 / B 31.00; estimated overhead A 1623 / B 1691 ns/op (delta +68 ns/op).

No single prepare sub-step dominates (largest **prepare_pipeline**, +3410.1 ns/op, +38.8% of the e2e delta).

Top prepare-level contributors: prepare_pipeline +3410.1 ns (+38.8%); planner_preamble +862.0 ns (+9.8%); prepare_columns +838.0 ns (+9.5%); prepare_where +829.0 ns (+9.4%).

## leftjoin

e2e delta +10282.1 ns/op; full phase sum +9747.9; unattributed residual +372.4 (+3.6%); dispatch six-term residual +7017.2 (+68.2%); combined CI hw 1236.4 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | False | True | True | True | True | False |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | True | True | True | True | True | True |
| get_db_command | False | False | True | True | True | False |
| execute | True | True | True | True | True | True |
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
| planner_preamble | 1.2342 | [1.2219, 1.2541] | `++++++` | 3/3 | 3/3 | +6192.4 | +60.2% |
| planner_recipe | 1.2349 | [1.2217, 1.2537] | `++++++` | 3/3 | 3/3 | +6189.6 | +60.2% |
| construct | 1.0843 | [1.0573, 1.1232] | `++++++` | 3/3 | 3/3 | +1888.4 | +18.4% |
| execute | 1.0387 | [1.0341, 1.0537] | `++++++` | 3/3 | 3/3 | +644.6 | +6.3% |
| plan | 1.4895 | [1.4489, 1.5499] | `++++++` | 3/3 | 3/3 | +454.1 | +4.4% |
| cow_shape | 1.6480 | [1.5492, 1.7433] | `++++++` | 3/3 | 3/3 | +33.7 | +0.3% |
| planner_gate | 1.2885 | [1.0294, 1.3913] | `++++++` | 3/3 | 3/3 | +22.5 | +0.2% |

Overhead: pair cost A 54.08 / B 54.53 ns; boundary calls/op A 29.00 / B 31.00; estimated overhead A 1568 / B 1691 ns/op (delta +122 ns/op).

Dominant prepare sub-step: **planner_preamble** (+6192.4 ns/op, +60.2% of the e2e delta).

Top prepare-level contributors: planner_preamble +6192.4 ns (+60.2%); planner_recipe +6189.6 ns (+60.2%); planner_gate +22.5 ns (+0.2%).

**attribution: planner_preamble, planner_recipe, plan, planner_gate** — satisfies (a)-(e) in all four arms.
