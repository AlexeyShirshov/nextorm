# E22 reconciliation — T297 r=4 — P02b prepare-pipeline decomposition

`prepare_exclusive = prepare − (plan + bind + enum_create + cow_shape + prepare_pipeline + planner_gate + planner_recipe)`.
`planner_preamble = planner_gate + planner_recipe`; `legacy_prepare_exclusive = prepare − (plan + bind + enum_create + cow_shape)`
(the P02 derivation, kept for continuity).
Dispatch six-term sum: `e2e_phases = construct + prepare_exclusive + get_db_command + execute + read_rows + map_rows`;
its residual (`phase_residual`) **by construction contains** the nested phases that were subtracted from
`prepare`, so it is reported but is not the unexplained part.
Full phase sum adds the nested phases back: `e2e_phases + plan + bind + enum_create + cow_shape + prepare_pipeline + planner_gate + planner_recipe`.
The instrumented `Prepare` sub-steps are nested inside `prepare_pipeline` and are **not** added to the full
sum (no double count); they are reconciled against `prepare_pipeline` separately, and any gap is
`pipeline_remainder` (unlisted `Prepare` steps plus the union/referenced tail hashes).
`residual_unattributed = end_to_end − full phase sum` is the genuine unexplained remainder.
Instrumentation overhead is reported separately below (pair cost × instrumented boundary calls per
query), not inside the phase table.

## cte

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 49017.7 | 49681.0 | +724.2 |
| prepare_exclusive | 1393.4 | 1454.0 | +90.6 |
| get_db_command | 115.9 | 116.2 | +3.8 |
| execute | 21136.6 | 20943.0 | -260.1 |
| read_rows | 4383.9 | 4211.5 | -165.5 |
| map_rows | 1639.7 | 1764.9 | +81.9 |
| plan | 1046.5 | 1565.1 | +535.1 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 70.2 | 91.0 | +22.7 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 313.0 | 393.7 | +69.3 |
| planner_recipe | 50576.4 | 57076.2 | +6499.8 |
| planner_preamble | 50904.1 | 57471.1 | +6567.0 |
| legacy_prepare_exclusive | 52254.2 | 58911.7 | +6657.5 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 135143.8 | 142074.6 | +6811.6 |
| phase_residual | 57053.4 | 63924.9 | +6871.5 |
| residual_unattributed | 4869.3 | 4847.5 | -45.4 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +6993.6 ns/op vs e2e delta +6811.6 ns/op -> unattributed residual -45.4 ns/op (-0.7%), combined 95 % CI half-width 10389.3 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +318.4 ns/op -> `phase_residual` +6871.5 ns/op (+100.9%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 59.78 ns / B 59.58 ns; boundary calls per query A 29.00 / B 30.00; overhead A 1734 ns/op / B 1787 ns/op; delta +54 ns/op.

## recursive

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 35903.3 | 40805.7 | +4657.3 |
| prepare_exclusive | 1206.5 | 1320.0 | +81.8 |
| get_db_command | 101.2 | 109.5 | +1.1 |
| execute | 22361.7 | 23434.1 | +343.1 |
| read_rows | 6806.2 | 6751.2 | -169.3 |
| map_rows | 2013.9 | 2094.1 | +68.7 |
| plan | 1043.3 | 1473.3 | +429.6 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 68.2 | 86.3 | +18.0 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 307.6 | 415.0 | +80.5 |
| planner_recipe | 57223.4 | 64266.7 | +4555.7 |
| planner_preamble | 57528.1 | 64649.0 | +4612.8 |
| legacy_prepare_exclusive | 58769.9 | 65969.0 | +4694.6 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 131226.5 | 145030.0 | +8172.2 |
| phase_residual | 62861.2 | 70495.4 | +4847.3 |
| residual_unattributed | 4263.9 | 4284.0 | +46.2 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +8273.3 ns/op vs e2e delta +8172.2 ns/op -> unattributed residual +46.2 ns/op (+0.6%), combined 95 % CI half-width 8108.9 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +4114.8 ns/op -> `phase_residual` +4847.3 ns/op (+59.3%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 59.78 ns / B 59.58 ns; boundary calls per query A 41.00 / B 47.00; overhead A 2451 ns/op / B 2800 ns/op; delta +349 ns/op.

## inlist

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 21081.0 | 21368.1 | +296.8 |
| prepare_exclusive | 933.1 | 1086.6 | +149.8 |
| get_db_command | 108.2 | 110.0 | -2.1 |
| execute | 29162.0 | 28997.4 | -230.8 |
| read_rows | 5269.0 | 5302.3 | -98.3 |
| map_rows | 1331.0 | 1405.7 | +81.8 |
| plan | 10182.5 | 12413.5 | +1941.2 |
| bind | 15028.4 | 15748.9 | +687.9 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 0.0 | 0.0 | +0.0 |
| prepare_pipeline | 27862.0 | 31520.9 | +2864.1 |
| planner_gate | 110.7 | 151.0 | +38.7 |
| planner_recipe | 13337.6 | 13815.8 | +414.7 |
| planner_preamble | 13443.5 | 13954.1 | +444.4 |
| legacy_prepare_exclusive | 42238.7 | 46530.1 | +3167.6 |
| pipeline_remainder | 1091.8 | 1837.6 | +762.0 |
| end_to_end | 128983.0 | 135582.5 | +5627.0 |
| phase_residual | 70641.3 | 77476.2 | +5470.1 |
| residual_unattributed | 4199.1 | 4280.9 | -89.7 |

Prepare pipeline decomposition: `prepare_pipeline` A 27862.0 / B 31520.9 ns/op; sum of sub-step medians A 26901.0 / B 29709.0; `pipeline_remainder` A 1091.8 / B 1837.6 ns/op. `prepare_call` calls/op A 1.00 / B 1.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +5663.8 ns/op vs e2e delta +5627.0 ns/op -> unattributed residual -89.7 ns/op (-1.6%), combined 95 % CI half-width 13473.5 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +157.0 ns/op -> `phase_residual` +5470.1 ns/op (+97.2%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 59.78 ns / B 59.58 ns; boundary calls per query A 36.00 / B 37.00; overhead A 2152 ns/op / B 2204 ns/op; delta +52 ns/op.

## leftjoin

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 24086.5 | 26083.4 | +1317.3 |
| prepare_exclusive | 995.0 | 1089.4 | +133.5 |
| get_db_command | 99.7 | 97.4 | -3.8 |
| execute | 18192.9 | 19062.3 | +717.3 |
| read_rows | 5595.1 | 5754.4 | -5.0 |
| map_rows | 5809.6 | 5928.8 | +55.5 |
| plan | 1033.9 | 1527.6 | +477.2 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 65.5 | 83.4 | +17.0 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 80.2 | 111.9 | +29.3 |
| planner_recipe | 29269.9 | 34236.2 | +4676.0 |
| planner_preamble | 29348.2 | 34348.1 | +4707.5 |
| legacy_prepare_exclusive | 30343.2 | 35406.2 | +4838.0 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 89876.5 | 98610.1 | +7763.2 |
| phase_residual | 35205.3 | 40672.4 | +5381.8 |
| residual_unattributed | 4610.1 | 4769.3 | +150.7 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +7811.3 ns/op vs e2e delta +7763.2 ns/op -> unattributed residual +150.7 ns/op (+1.9%), combined 95 % CI half-width 4082.8 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +2615.6 ns/op -> `phase_residual` +5381.8 ns/op (+69.3%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 59.78 ns / B 59.58 ns; boundary calls per query A 42.00 / B 42.00; overhead A 2511 ns/op / B 2502 ns/op; delta -8 ns/op.

The overhead estimate is deliberately kept out of the phase table: it is the same order for A and B
except for the B-only COW counters (cow_description/cow_clone), so it cannot produce the measured delta;
the per-arm overhead delta is the maximum bias that instrumentation can contribute to that arm's delta.
