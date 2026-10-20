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
| construct | 46208.2 | 44822.7 | -489.0 |
| prepare_exclusive | 1318.9 | 1389.1 | +92.6 |
| get_db_command | 106.2 | 80.5 | -24.6 |
| execute | 19852.4 | 19529.8 | -322.6 |
| read_rows | 3968.6 | 3866.4 | -49.9 |
| map_rows | 1575.9 | 1503.8 | -62.7 |
| plan | 977.4 | 1387.2 | +405.3 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 53.5 | 99.6 | +45.8 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 301.6 | 324.7 | +23.6 |
| planner_recipe | 46878.8 | 52415.3 | +6960.0 |
| planner_preamble | 47182.8 | 52739.3 | +6997.0 |
| legacy_prepare_exclusive | 48467.9 | 54097.6 | +7053.3 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 125865.5 | 129807.3 | +7857.3 |
| phase_residual | 52871.5 | 58925.6 | +7771.1 |
| residual_unattributed | 4510.5 | 4827.2 | +166.0 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +7436.8 ns/op vs e2e delta +7857.3 ns/op -> unattributed residual +166.0 ns/op (+2.1%), combined 95 % CI half-width 13086.3 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta -1083.6 ns/op -> `phase_residual` +7771.1 ns/op (+98.9%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.91 ns / B 56.21 ns; boundary calls per query A 13.00 / B 17.00; overhead A 714 ns/op / B 956 ns/op; delta +242 ns/op.

## recursive

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 34000.2 | 36481.3 | +3598.3 |
| prepare_exclusive | 1122.5 | 1207.5 | +135.9 |
| get_db_command | 92.4 | 111.8 | +17.1 |
| execute | 20925.1 | 21583.0 | +690.1 |
| read_rows | 6329.0 | 6347.7 | +68.6 |
| map_rows | 1785.2 | 1870.8 | +104.8 |
| plan | 944.5 | 1354.2 | +426.7 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 57.3 | 93.7 | +36.6 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 307.7 | 312.6 | +5.3 |
| planner_recipe | 52246.1 | 57989.6 | +5905.5 |
| planner_preamble | 52547.3 | 58370.5 | +5921.7 |
| legacy_prepare_exclusive | 53738.6 | 59571.9 | +6062.0 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 121828.8 | 131394.2 | +11257.1 |
| phase_residual | 57480.7 | 63810.7 | +6490.4 |
| residual_unattributed | 3924.7 | 4010.6 | +68.3 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +11219.2 ns/op vs e2e delta +11257.1 ns/op -> unattributed residual +68.3 ns/op (+0.6%), combined 95 % CI half-width 20202.6 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +4709.0 ns/op -> `phase_residual` +6490.4 ns/op (+57.7%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.91 ns / B 56.21 ns; boundary calls per query A 19.00 / B 28.00; overhead A 1043 ns/op / B 1574 ns/op; delta +531 ns/op.

## inlist

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 19416.0 | 19876.1 | +663.5 |
| prepare_exclusive | 860.1 | 1010.6 | +154.4 |
| get_db_command | 103.2 | 118.2 | +14.3 |
| execute | 26812.2 | 27052.8 | +650.7 |
| read_rows | 4959.4 | 4974.1 | +132.9 |
| map_rows | 1212.5 | 1224.2 | -20.7 |
| plan | 9400.5 | 11308.0 | +1844.7 |
| bind | 13958.2 | 14666.0 | +662.2 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 0.0 | 0.0 | +0.0 |
| prepare_pipeline | 26239.9 | 28887.1 | +2922.6 |
| planner_gate | 99.5 | 149.2 | +46.5 |
| planner_recipe | 12201.2 | 13014.0 | +628.9 |
| planner_preamble | 12300.7 | 13150.2 | +689.0 |
| legacy_prepare_exclusive | 39519.3 | 42913.2 | +3871.5 |
| pipeline_remainder | 975.3 | 1611.0 | +639.7 |
| end_to_end | 118977.4 | 127008.1 | +7922.7 |
| phase_residual | 65815.0 | 71674.3 | +6292.5 |
| residual_unattributed | 3803.7 | 3892.7 | +91.1 |

Prepare pipeline decomposition: `prepare_pipeline` A 26239.9 / B 28887.1 ns/op; sum of sub-step medians A 25118.2 / B 27262.6; `pipeline_remainder` A 975.3 / B 1611.0 ns/op. `prepare_call` calls/op A 1.00 / B 1.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +7690.1 ns/op vs e2e delta +7922.7 ns/op -> unattributed residual +91.1 ns/op (+1.2%), combined 95 % CI half-width 6609.9 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +1533.2 ns/op -> `phase_residual` +6292.5 ns/op (+79.4%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.91 ns / B 56.21 ns; boundary calls per query A 30.00 / B 31.00; overhead A 1647 ns/op / B 1742 ns/op; delta +95 ns/op.

## leftjoin

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 22080.6 | 23563.9 | +1504.1 |
| prepare_exclusive | 926.1 | 1060.1 | +140.3 |
| get_db_command | 95.6 | 72.6 | -31.2 |
| execute | 17143.7 | 17528.4 | +498.5 |
| read_rows | 5245.3 | 5294.5 | +25.3 |
| map_rows | 5346.0 | 5615.4 | +174.0 |
| plan | 965.2 | 1401.1 | +428.8 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 50.8 | 96.7 | +44.3 |
| prepare_pipeline | 0.0 | 0.0 | +0.0 |
| planner_gate | 77.4 | 101.0 | +17.9 |
| planner_recipe | 26875.6 | 32764.0 | +6158.7 |
| planner_preamble | 26950.7 | 32864.8 | +6182.3 |
| legacy_prepare_exclusive | 27904.1 | 33909.9 | +6374.7 |
| pipeline_remainder | 0.0 | 0.0 | +0.0 |
| end_to_end | 83049.9 | 92067.6 | +9289.0 |
| phase_residual | 32243.0 | 38971.0 | +7016.8 |
| residual_unattributed | 4245.1 | 4507.2 | +210.3 |

Prepare pipeline decomposition: `prepare_pipeline` A 0.0 / B 0.0 ns/op; sum of sub-step medians A 0.0 / B 0.0; `pipeline_remainder` A 0.0 / B 0.0 ns/op. `prepare_call` calls/op A 0.00 / B 0.00 (0 means the D201-3 raw-recipe path applied the prepared state before `PrepareCommand`).

Full phase sum delta +9004.9 ns/op vs e2e delta +9289.0 ns/op -> unattributed residual +210.3 ns/op (+2.3%), combined 95 % CI half-width 2573.6 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +2272.2 ns/op -> `phase_residual` +7016.8 ns/op (+75.5%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.91 ns / B 56.21 ns; boundary calls per query A 29.00 / B 31.00; overhead A 1592 ns/op / B 1742 ns/op; delta +150 ns/op.

The overhead estimate is deliberately kept out of the phase table: it is the same order for A and B
except for the B-only COW counters (cow_description/cow_clone), so it cannot produce the measured delta;
the per-arm overhead delta is the maximum bias that instrumentation can contribute to that arm's delta.
