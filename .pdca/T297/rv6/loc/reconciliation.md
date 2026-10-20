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
| construct | 48221.9 | 49366.1 | +2029.6 |
| prepare_exclusive | 1374.8 | 1478.8 | +103.5 |
| get_db_command | 117.5 | 93.5 | -23.9 |
| execute | 20794.2 | 21347.2 | +986.1 |
| read_rows | 4069.6 | 4211.6 | +205.5 |
| map_rows | 1663.5 | 1705.1 | +73.8 |
| plan | 1035.8 | 1539.5 | +522.4 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 74.9 | 104.7 | +30.4 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 319.6 | 385.2 | +66.7 |
| planner_recipe | 50300.6 | 58594.8 | +9481.2 |
| planner_preamble | 50613.1 | 58977.6 | +9506.9 |
| legacy_prepare_exclusive | 52068.2 | 60442.0 | +9602.7 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 132966.2 | 143330.2 | +14382.6 |
| phase_residual | 56793.3 | 65635.5 | +10355.2 |
| residual_unattributed | 4990.7 | 5098.3 | +341.8 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +13932.7 ns/op vs e2e delta +14382.6 ns/op -> unattributed residual +341.8 ns/op (+2.4%), combined 95 % CI half-width 13262.8 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +3829.1 ns/op -> `phase_residual` +10355.2 ns/op (+72.0%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 60.57 ns / B 61.01 ns; boundary calls per query A 29.00 / B 33.00; overhead A 1757 ns/op / B 2013 ns/op; delta +257 ns/op.

## recursive

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 36160.1 | 41580.2 | +5611.3 |
| prepare_exclusive | 1209.6 | 1351.3 | +107.4 |
| get_db_command | 106.0 | 99.5 | -7.6 |
| execute | 22695.0 | 23734.5 | +1296.4 |
| read_rows | 6642.6 | 7069.7 | +563.9 |
| map_rows | 1958.9 | 2066.8 | +127.9 |
| plan | 1036.0 | 1494.5 | +460.2 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 67.6 | 99.2 | +31.1 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 313.1 | 351.7 | +40.1 |
| planner_recipe | 57212.1 | 65843.3 | +8567.7 |
| planner_preamble | 57519.8 | 66184.0 | +8613.1 |
| legacy_prepare_exclusive | 58787.3 | 67570.2 | +8682.6 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 131531.3 | 147815.1 | +17623.8 |
| phase_residual | 62745.0 | 72137.8 | +9312.9 |
| residual_unattributed | 4097.7 | 4416.9 | +290.5 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +17383.0 ns/op vs e2e delta +17623.8 ns/op -> unattributed residual +290.5 ns/op (+1.6%), combined 95 % CI half-width 7039.8 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +7749.7 ns/op -> `phase_residual` +9312.9 ns/op (+52.8%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 60.57 ns / B 61.01 ns; boundary calls per query A 41.00 / B 50.00; overhead A 2483 ns/op / B 3050 ns/op; delta +567 ns/op.

## inlist

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 21099.9 | 21951.7 | +1333.9 |
| prepare_exclusive | 943.0 | 1077.3 | +142.2 |
| get_db_command | 110.9 | 123.5 | +7.5 |
| execute | 28841.8 | 29316.5 | +1385.7 |
| read_rows | 5359.7 | 5476.5 | +346.9 |
| map_rows | 1351.2 | 1467.4 | +160.9 |
| plan | 10215.9 | 12281.0 | +2292.9 |
| bind | 15042.5 | 16099.8 | +1299.8 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 0.0 | 0.0 | +0.0 |
| prepare_pipeline | 27830.2 | 32188.1 | +4741.6 |
| planner_gate | 106.2 | 162.9 | +56.6 |
| planner_recipe | 13320.0 | 14279.6 | +1027.2 |
| planner_preamble | 13424.1 | 14458.2 | +1082.8 |
| legacy_prepare_exclusive | 42195.5 | 47723.6 | +6097.7 |
| pipeline_remainder | 1086.1 | 1807.9 | +737.4 |
| end_to_end | 128033.7 | 138825.7 | +12922.4 |
| phase_residual | 70701.6 | 79311.8 | +9718.4 |
| residual_unattributed | 4166.3 | 4310.7 | +141.1 |

Prepare pipeline decomposition: `prepare_pipeline` A 27830.2 / B 32188.1 ns/op; sum of sub-step medians A 26795.1 / B 30284.6; `pipeline_remainder` A 1086.1 / B 1807.9 ns/op. `prepare_call` calls/op A 1.00 / B 1.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +12685.0 ns/op vs e2e delta +12922.4 ns/op -> unattributed residual +141.1 ns/op (+1.1%), combined 95 % CI half-width 8824.2 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +3203.9 ns/op -> `phase_residual` +9718.4 ns/op (+75.2%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 60.57 ns / B 61.01 ns; boundary calls per query A 36.00 / B 37.00; overhead A 2180 ns/op / B 2257 ns/op; delta +77 ns/op.

## leftjoin

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 23736.6 | 25696.1 | +2238.5 |
| prepare_exclusive | 967.7 | 1087.7 | +101.7 |
| get_db_command | 101.1 | 80.6 | -20.2 |
| execute | 18197.3 | 18969.8 | +1186.8 |
| read_rows | 5835.1 | 5782.6 | +54.5 |
| map_rows | 5789.1 | 5810.3 | +127.3 |
| plan | 1025.6 | 1590.7 | +498.7 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 68.6 | 88.8 | +18.9 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 85.6 | 116.0 | +29.8 |
| planner_recipe | 29178.1 | 36753.8 | +7579.6 |
| planner_preamble | 29263.4 | 36859.2 | +7621.0 |
| legacy_prepare_exclusive | 30223.6 | 37962.6 | +7722.7 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 89612.9 | 100553.7 | +12195.4 |
| phase_residual | 34993.5 | 43356.8 | +8500.4 |
| residual_unattributed | 4669.4 | 4847.2 | +300.9 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +11828.6 ns/op vs e2e delta +12195.4 ns/op -> unattributed residual +300.9 ns/op (+2.5%), combined 95 % CI half-width 7063.9 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +3695.0 ns/op -> `phase_residual` +8500.4 ns/op (+69.7%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 60.57 ns / B 61.01 ns; boundary calls per query A 42.00 / B 44.00; overhead A 2544 ns/op / B 2684 ns/op; delta +140 ns/op.

The overhead estimate is deliberately kept out of the phase table: it is the same order for A and B
except for the B-only COW counters (cow_description/cow_clone), so it cannot produce the measured delta;
the per-arm overhead delta is the maximum bias that instrumentation can contribute to that arm's delta.
