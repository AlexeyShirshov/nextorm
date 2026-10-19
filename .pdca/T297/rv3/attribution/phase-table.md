# E22 phase table — T297 r=4

Paired B/A per round (6 paired rounds per arm = three ABBA groups). CI = bootstrap 95 % CI
(10 000 resamples, seed 297). Hw = relative CI half-width. Share = phase delta / e2e delta.
Alloc delta is arm-level (bytes/op) and is shown on the end-to-end row only.

Uninstrumented ABBA causal anchor (E21, B/A): cte 1.076/1.147 (default/inprocess); recursive 1.101/1.136 (default/inprocess); inlist 1.082/1.075 (default/inprocess); leftjoin 1.104/1.048 (default/inprocess)

## cte

Probe e2e B/A = 1.0863 [1.0782, 1.1059] (hw 1.3 %); ABBA anchor 1.076/1.147; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 43480.1 | 44885.1 | 1.0312 | [1.0187, 1.0478] | +1.4% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +13.5% |
| prepare | 46765.5 | 55742.2 | 1.1834 | [1.1664, 1.2033] | +1.6% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +84.2% |
| plan | 910.9 | 1347.1 | 1.4594 | [1.4435, 1.5135] | +2.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:698-711 MakeSelect (plan-key + QueryPlanStore.TryGet) | +4.2% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:874-881 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:891-898 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 110.4 | 95.6 | 0.8656 | [0.7815, 0.8922] | +6.4% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:198-288 RefreshInValuesShape | -0.1% |
| get_db_command | 79.0 | 86.3 | 1.0234 | [0.9697, 1.2286] | +12.7% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.0% |
| execute | 18952.7 | 19295.1 | 1.0333 | [1.0107, 1.0457] | +1.7% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +6.1% |
| read_rows | 3803.1 | 3856.4 | 1.0092 | [0.9732, 1.0641] | +4.5% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.3% |
| map_rows | 1506.3 | 1598.5 | 1.0513 | [0.9757, 1.1910] | +10.2% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.7% |
| cow_description | 0.0 | 406.1 | — | — | — | 0.00/4.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +4.0% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_exclusive | 45710.9 | 54273.6 | 1.1777 | [1.1616, 1.1990] | +1.6% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape | +79.8% |
| end_to_end | 119712.5 | 129621.6 | 1.0863 | [1.0782, 1.1059] | +1.3% | 1/1 | -784.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 5523.3 | 5948.9 | 1.0820 | [1.0255, 1.1040] | +3.6% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +4.3% |
| residual_unattributed | 4511.6 | 4480.4 | 1.0031 | [0.9446, 1.0370] | +4.6% | — | — | unattributed residual (all measured phases added back) | +0.1% |

Verdict: e2e delta +10257.9 ns/op; full phase sum +10244.0; unattributed residual +13.6 ns/op (+0.1%); dispatch six-term residual +446.0 ns/op (+4.3%); combined CI hw 2010.9 ns; reconciles=True; residual insignificant=True; named=construct, prepare_exclusive, plan, execute.

## recursive

Probe e2e B/A = 1.1060 [1.0937, 1.1197] (hw 1.2 %); ABBA anchor 1.101/1.136; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 32294.8 | 36648.4 | 1.1283 | [1.0935, 1.1675] | +3.3% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +32.6% |
| prepare | 53449.2 | 60779.7 | 1.1341 | [1.1173, 1.1474] | +1.3% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +57.2% |
| plan | 887.0 | 1294.8 | 1.4784 | [1.4098, 1.6075] | +6.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:698-711 MakeSelect (plan-key + QueryPlanStore.TryGet) | +3.3% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:874-881 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:891-898 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 108.0 | 82.5 | 0.7856 | [0.7283, 0.8779] | +9.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:198-288 RefreshInValuesShape | -0.2% |
| get_db_command | 98.7 | 103.3 | 1.0464 | [0.9755, 1.3890] | +19.8% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.0% |
| execute | 20779.8 | 21834.4 | 1.0491 | [1.0309, 1.0582] | +1.3% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +8.1% |
| read_rows | 6233.3 | 6392.5 | 1.0223 | [0.9964, 1.0669] | +3.4% | 6.00/6.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +1.1% |
| map_rows | 1755.2 | 1849.8 | 1.0782 | [0.9988, 1.0883] | +4.2% | 5.00/5.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +1.1% |
| cow_description | 0.0 | 697.2 | — | — | — | 0.00/8.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +5.5% |
| cow_clone | 0.0 | 233.2 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +1.8% |
| prepare_exclusive | 52487.5 | 59320.4 | 1.1291 | [1.1124, 1.1410] | +1.3% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape | +54.0% |
| end_to_end | 118408.8 | 131419.4 | 1.1060 | [1.0937, 1.1197] | +1.2% | 1/1 | -1680.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 4765.1 | 5358.1 | 1.1079 | [1.0687, 1.1362] | +3.0% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +4.1% |
| residual_unattributed | 3795.0 | 3977.1 | 1.0294 | [0.9633, 1.0762] | +5.5% | — | — | unattributed residual (all measured phases added back) | +0.9% |

Verdict: e2e delta +12615.0 ns/op; full phase sum +12541.1; unattributed residual +108.4 ns/op (+0.9%); dispatch six-term residual +522.7 ns/op (+4.1%); combined CI hw 2121.0 ns; reconciles=True; residual insignificant=True; named=construct, prepare_exclusive, plan, execute.

## inlist

Probe e2e B/A = 1.0638 [1.0393, 1.0816] (hw 2.0 %); ABBA anchor 1.082/1.075; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 19158.8 | 19640.4 | 1.0042 | [0.9826, 1.0498] | +3.3% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +1.1% |
| prepare | 59902.8 | 66526.6 | 1.1034 | [1.0861, 1.1330] | +2.1% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +84.4% |
| plan | 9166.9 | 11155.3 | 1.2123 | [1.1775, 1.2441] | +2.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:698-711 MakeSelect (plan-key + QueryPlanStore.TryGet) | +26.3% |
| bind | 13749.7 | 14275.6 | 1.0355 | [1.0181, 1.0645] | +2.2% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:874-881 MakeSelect -> RefreshParameters<TResult> | +6.7% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:891-898 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:198-288 RefreshInValuesShape | +0.0% |
| get_db_command | 104.7 | 114.8 | 1.1056 | [1.0335, 1.1950] | +7.3% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.2% |
| execute | 26178.7 | 26820.1 | 1.0147 | [0.9874, 1.0330] | +2.3% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +5.3% |
| read_rows | 4872.6 | 4969.5 | 1.0079 | [0.9938, 1.0522] | +2.9% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.5% |
| map_rows | 1232.5 | 1243.5 | 1.0081 | [0.9442, 1.0916] | +7.3% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.1% |
| cow_description | 0.0 | 163.0 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +2.2% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_exclusive | 37033.4 | 41128.7 | 1.1107 | [1.0798, 1.1310] | +2.3% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape | +55.6% |
| end_to_end | 115282.7 | 123487.8 | 1.0638 | [1.0393, 1.0816] | +2.0% | 1/1 | -360.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 26775.4 | 29426.8 | 1.0852 | [1.0712, 1.1244] | +2.5% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +31.2% |
| residual_unattributed | 3902.9 | 3957.8 | 0.9998 | [0.9702, 1.0546] | +4.2% | — | — | unattributed residual (all measured phases added back) | -0.0% |

Verdict: e2e delta +7331.5 ns/op; full phase sum +7385.7; unattributed residual -1.5 ns/op (-0.0%); dispatch six-term residual +2285.6 ns/op (+31.2%); combined CI hw 2801.2 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, bind, get_db_command.

## leftjoin

Probe e2e B/A = 1.1248 [1.1088, 1.1495] (hw 1.8 %); ABBA anchor 1.104/1.048; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 21893.7 | 23727.1 | 1.0845 | [1.0625, 1.0980] | +1.6% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +18.1% |
| prepare | 27941.5 | 35195.4 | 1.2502 | [1.2259, 1.2793] | +2.1% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +69.1% |
| plan | 897.4 | 1357.4 | 1.5297 | [1.4419, 1.6141] | +5.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:698-711 MakeSelect (plan-key + QueryPlanStore.TryGet) | +4.6% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:874-881 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:891-898 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 99.9 | 84.6 | 0.8920 | [0.8015, 0.9479] | +8.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:198-288 RefreshInValuesShape | -0.1% |
| get_db_command | 68.7 | 79.5 | 1.2658 | [0.9544, 1.4661] | +20.2% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.1% |
| execute | 16616.6 | 17671.0 | 1.0599 | [1.0252, 1.0885] | +3.0% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +9.6% |
| read_rows | 5173.8 | 5386.4 | 1.0367 | [1.0251, 1.0509] | +1.2% | 11.00/11.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +1.8% |
| map_rows | 5156.0 | 5373.3 | 1.0379 | [1.0167, 1.0526] | +1.7% | 10.00/10.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +1.9% |
| cow_description | 0.0 | 207.0 | — | — | — | 0.00/2.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +2.0% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_exclusive | 26943.4 | 33684.5 | 1.2453 | [1.2169, 1.2700] | +2.1% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape | +65.3% |
| end_to_end | 81150.6 | 92139.4 | 1.1248 | [1.1088, 1.1495] | +1.8% | 1/1 | -320.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 5215.2 | 5951.7 | 1.1208 | [1.0713, 1.1812] | +4.9% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +6.2% |
| residual_unattributed | 4239.8 | 4462.7 | 1.0271 | [0.9913, 1.1200] | +6.3% | — | — | unattributed residual (all measured phases added back) | +1.1% |

Verdict: e2e delta +10325.9 ns/op; full phase sum +10165.1; unattributed residual +115.1 ns/op (+1.1%); dispatch six-term residual +636.1 ns/op (+6.2%); combined CI hw 1920.8 ns; reconciles=True; residual insignificant=True; named=construct, prepare_exclusive, plan, execute, read_rows, map_rows.

