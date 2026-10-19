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
| construct | 43440.8 | 44869.9 | +1007.7 |
| prepare_exclusive | 1307.7 | 1405.2 | +120.6 |
| get_db_command | 105.3 | 85.7 | -23.7 |
| execute | 19121.5 | 19315.3 | +321.4 |
| read_rows | 3823.8 | 3903.8 | +73.5 |
| map_rows | 1481.6 | 1483.9 | +8.1 |
| plan | 942.7 | 1400.4 | +438.3 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 53.8 | 85.9 | +33.5 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 280.4 | 358.4 | +69.9 |
| planner_recipe | 45142.7 | 52355.4 | +7621.8 |
| planner_preamble | 45426.5 | 52714.6 | +7702.5 |
| legacy_prepare_exclusive | 46887.6 | 54262.2 | +7754.5 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 120055.7 | 129869.9 | +10750.1 |
| phase_residual | 50776.2 | 58756.0 | +8332.8 |
| residual_unattributed | 4422.6 | 4580.1 | +109.9 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +10681.0 ns/op vs e2e delta +10750.1 ns/op -> unattributed residual +109.9 ns/op (+1.0%), combined 95 % CI half-width 6531.0 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +1390.2 ns/op -> `phase_residual` +8332.8 ns/op (+77.5%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.08 ns / B 54.53 ns; boundary calls per query A 13.00 / B 17.00; overhead A 703 ns/op / B 927 ns/op; delta +224 ns/op.

## recursive

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 32738.9 | 36943.4 | +4204.4 |
| prepare_exclusive | 1068.9 | 1258.3 | +201.2 |
| get_db_command | 88.7 | 100.0 | +11.2 |
| execute | 20795.9 | 21429.2 | +729.8 |
| read_rows | 6062.9 | 6232.8 | +198.7 |
| map_rows | 1814.8 | 1820.6 | +39.2 |
| plan | 919.4 | 1357.6 | +471.0 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 53.5 | 83.7 | +31.8 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 282.6 | 347.5 | +65.1 |
| planner_recipe | 51458.2 | 57942.9 | +6783.6 |
| planner_preamble | 51743.9 | 58303.3 | +6845.6 |
| legacy_prepare_exclusive | 52800.6 | 59552.2 | +7091.3 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 118952.8 | 131995.9 | +13043.1 |
| phase_residual | 56598.1 | 63787.9 | +7459.0 |
| residual_unattributed | 3905.7 | 4005.6 | +98.4 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +12966.4 ns/op vs e2e delta +13043.1 ns/op -> unattributed residual +98.4 ns/op (+0.8%), combined 95 % CI half-width 7017.0 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +5584.1 ns/op -> `phase_residual` +7459.0 ns/op (+57.2%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.08 ns / B 54.53 ns; boundary calls per query A 19.00 / B 28.00; overhead A 1028 ns/op / B 1527 ns/op; delta +499 ns/op.

## inlist

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 19186.2 | 19714.1 | +480.1 |
| prepare_exclusive | 850.8 | 954.9 | +120.0 |
| get_db_command | 102.6 | 107.1 | +5.9 |
| execute | 26423.7 | 26622.6 | +188.9 |
| read_rows | 4825.1 | 4857.1 | +5.6 |
| map_rows | 1202.7 | 1208.2 | +2.7 |
| plan | 9248.1 | 11069.5 | +1762.1 |
| bind | 13482.5 | 14810.4 | +1367.7 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 0.0 | 0.0 | +0.0 |
| prepare_pipeline | 25385.4 | 28875.2 | +3410.1 |
| planner_gate | 94.7 | 134.0 | +38.3 |
| planner_recipe | 12010.1 | 12491.4 | +817.6 |
| planner_preamble | 12114.7 | 12627.3 | +862.0 |
| legacy_prepare_exclusive | 38481.7 | 42378.8 | +4444.7 |
| pipeline_remainder | 959.7 | 1600.3 | +643.8 |
| end_to_end | 116960.4 | 125147.5 | +8779.4 |
| phase_residual | 64273.3 | 71037.8 | +7765.7 |
| residual_unattributed | 3866.1 | 4036.5 | +211.7 |

Prepare pipeline decomposition: `prepare_pipeline` A 25385.4 / B 28875.2 ns/op; sum of sub-step medians A 24422.7 / B 27305.7; `pipeline_remainder` A 959.7 / B 1600.3 ns/op. `prepare_call` calls/op A 1.00 / B 1.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +8567.7 ns/op vs e2e delta +8779.4 ns/op -> unattributed residual +211.7 ns/op (+2.4%), combined 95 % CI half-width 3504.2 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +772.1 ns/op -> `phase_residual` +7765.7 ns/op (+88.5%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.08 ns / B 54.53 ns; boundary calls per query A 30.00 / B 31.00; overhead A 1623 ns/op / B 1691 ns/op; delta +68 ns/op.

## leftjoin

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 21880.7 | 24115.8 | +1888.4 |
| prepare_exclusive | 903.0 | 1056.8 | +164.5 |
| get_db_command | 78.0 | 74.6 | -9.4 |
| execute | 16765.2 | 17565.0 | +644.6 |
| read_rows | 5154.4 | 5195.9 | +42.7 |
| map_rows | 5292.8 | 5329.7 | +14.3 |
| plan | 927.6 | 1393.0 | +454.1 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 52.0 | 84.9 | +33.7 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 78.8 | 101.3 | +22.5 |
| planner_recipe | 26275.2 | 32699.7 | +6189.6 |
| planner_preamble | 26353.0 | 32798.2 | +6192.4 |
| legacy_prepare_exclusive | 27305.9 | 33802.3 | +6332.3 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 81724.9 | 92227.4 | +10282.1 |
| phase_residual | 31633.0 | 38834.0 | +7017.2 |
| residual_unattributed | 4263.3 | 4589.6 | +372.4 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +9747.9 ns/op vs e2e delta +10282.1 ns/op -> unattributed residual +372.4 ns/op (+3.6%), combined 95 % CI half-width 1236.4 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +2736.6 ns/op -> `phase_residual` +7017.2 ns/op (+68.2%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.08 ns / B 54.53 ns; boundary calls per query A 29.00 / B 31.00; overhead A 1568 ns/op / B 1691 ns/op; delta +122 ns/op.

The overhead estimate is deliberately kept out of the phase table: it is the same order for A and B
except for the B-only COW counters (cow_description/cow_clone), so it cannot produce the measured delta;
the per-arm overhead delta is the maximum bias that instrumentation can contribute to that arm's delta.
