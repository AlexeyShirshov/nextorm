# E22 reconciliation — T297 r=4

`prepare_exclusive = prepare − (plan + bind + enum_create + cow_shape)` (nested spans).
Dispatch six-term sum: `e2e_phases = construct + prepare_exclusive + get_db_command + execute + read_rows + map_rows`;
its residual (`phase_residual`) **by construction contains** the nested phases that were subtracted from
`prepare`, so it is reported but is not the unexplained part.
Full phase sum adds the nested phases back: `e2e_phases + plan + bind + enum_create + cow_shape`.
`residual_unattributed = end_to_end − full phase sum` is the genuine unexplained remainder.
Instrumentation overhead is reported separately below (pair cost × instrumented boundary calls per
query), not inside the phase table.

## cte

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 43480.1 | 44885.1 | +1382.6 |
| prepare_exclusive | 45710.9 | 54273.6 | +8190.2 |
| get_db_command | 79.0 | 86.3 | +1.6 |
| execute | 18952.7 | 19295.1 | +622.0 |
| read_rows | 3803.1 | 3856.4 | +34.9 |
| map_rows | 1506.3 | 1598.5 | +75.2 |
| plan | 910.9 | 1347.1 | +430.1 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 110.4 | 95.6 | -14.8 |
| end_to_end | 119712.5 | 129621.6 | +10257.9 |
| phase_residual | 5523.3 | 5948.9 | +446.0 |
| residual_unattributed | 4511.6 | 4480.4 | +13.6 |

Full phase sum delta +10244.0 ns/op vs e2e delta +10257.9 ns/op -> unattributed residual +13.6 ns/op (+0.1%), combined 95 % CI half-width 2010.9 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +9811.9 ns/op -> `phase_residual` +446.0 ns/op (+4.3%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.77 ns / B 54.92 ns; boundary calls per query A 11.00 / B 15.00; overhead A 602 ns/op / B 824 ns/op; delta +221 ns/op.

## recursive

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 32294.8 | 36648.4 | +4116.1 |
| prepare_exclusive | 52487.5 | 59320.4 | +6813.1 |
| get_db_command | 98.7 | 103.3 | +4.5 |
| execute | 20779.8 | 21834.4 | +1016.6 |
| read_rows | 6233.3 | 6392.5 | +139.7 |
| map_rows | 1755.2 | 1849.8 | +135.8 |
| plan | 887.0 | 1294.8 | +413.8 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 108.0 | 82.5 | -22.5 |
| end_to_end | 118408.8 | 131419.4 | +12615.0 |
| phase_residual | 4765.1 | 5358.1 | +522.7 |
| residual_unattributed | 3795.0 | 3977.1 | +108.4 |

Full phase sum delta +12541.1 ns/op vs e2e delta +12615.0 ns/op -> unattributed residual +108.4 ns/op (+0.9%), combined 95 % CI half-width 2121.0 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +12146.2 ns/op -> `phase_residual` +522.7 ns/op (+4.1%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.77 ns / B 54.92 ns; boundary calls per query A 17.00 / B 26.00; overhead A 931 ns/op / B 1428 ns/op; delta +497 ns/op.

## inlist

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 19158.8 | 19640.4 | +79.1 |
| prepare_exclusive | 37033.4 | 41128.7 | +4074.2 |
| get_db_command | 104.7 | 114.8 | +11.5 |
| execute | 26178.7 | 26820.1 | +388.9 |
| read_rows | 4872.6 | 4969.5 | +39.2 |
| map_rows | 1232.5 | 1243.5 | +8.0 |
| plan | 9166.9 | 11155.3 | +1927.8 |
| bind | 13749.7 | 14275.6 | +487.6 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 0.0 | 0.0 | +0.0 |
| end_to_end | 115282.7 | 123487.8 | +7331.5 |
| phase_residual | 26775.4 | 29426.8 | +2285.6 |
| residual_unattributed | 3902.9 | 3957.8 | -1.5 |

Full phase sum delta +7385.7 ns/op vs e2e delta +7331.5 ns/op -> unattributed residual -1.5 ns/op (-0.0%), combined 95 % CI half-width 2801.2 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +5045.9 ns/op -> `phase_residual` +2285.6 ns/op (+31.2%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.77 ns / B 54.92 ns; boundary calls per query A 11.00 / B 12.00; overhead A 602 ns/op / B 659 ns/op; delta +57 ns/op.

## leftjoin

| component | A ns/op | B ns/op | delta ns/op |
|---|---|---|---|
| construct | 21893.7 | 23727.1 | +1873.2 |
| prepare_exclusive | 26943.4 | 33684.5 | +6745.7 |
| get_db_command | 68.7 | 79.5 | +15.2 |
| execute | 16616.6 | 17671.0 | +989.7 |
| read_rows | 5173.8 | 5386.4 | +189.6 |
| map_rows | 5156.0 | 5373.3 | +194.3 |
| plan | 897.4 | 1357.4 | +470.1 |
| bind | 0.0 | 0.0 | +0.0 |
| enum_create | 0.0 | 0.0 | +0.0 |
| cow_shape | 99.9 | 84.6 | -10.2 |
| end_to_end | 81150.6 | 92139.4 | +10325.9 |
| phase_residual | 5215.2 | 5951.7 | +636.1 |
| residual_unattributed | 4239.8 | 4462.7 | +115.1 |

Full phase sum delta +10165.1 ns/op vs e2e delta +10325.9 ns/op -> unattributed residual +115.1 ns/op (+1.1%), combined 95 % CI half-width 1920.8 ns, reconciles=True, residual insignificant=True.

Dispatch six-term sum delta +9775.7 ns/op -> `phase_residual` +636.1 ns/op (+6.2%) = nested plan/bind/enum_create/cow_shape + the unattributed remainder.

Instrumentation overhead: pair cost A 54.77 ns / B 54.92 ns; boundary calls per query A 27.00 / B 29.00; overhead A 1479 ns/op / B 1593 ns/op; delta +114 ns/op.

The overhead estimate is deliberately kept out of the phase table: it is the same order for A and B
except for the B-only COW counters (cow_description/cow_clone), so it cannot produce the measured delta;
the per-arm overhead delta is the maximum bias that instrumentation can contribute to that arm's delta.
