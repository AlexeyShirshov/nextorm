# E22 phase table — T297 r=4 — P02b prepare-pipeline decomposition

Paired B/A per round (6 paired rounds per arm = three ABBA groups). CI = bootstrap 95 % CI
(10 000 resamples, seed 297). Hw = relative CI half-width. Share = phase delta / e2e delta.
Alloc delta is arm-level (bytes/op) and is shown on the end-to-end row only.

Decomposition (P02b): `prepare` is a span over `plan`, `bind`, `enum_create`, `cow_shape`,
`prepare_pipeline` (the `PrepareCommand` call) and `planner_preamble` (`planner_gate` + `planner_recipe`).
`prepare_exclusive = prepare −` that nested list; `legacy_prepare_exclusive` is the P02 derivation
(`prepare − plan − bind − enum_create − cow_shape`). `pipeline_remainder = prepare_pipeline −` the
instrumented `Prepare` sub-step sum (unlisted Prepare steps and the tail union/referenced hashes).
`prepare_call` is the entry counter for `QueryPreparer.Prepare` (no time charged; with the raw-recipe
path it is 0 because `ApplyPreparationFrom` marks the command prepared before `PrepareCommand`).

Uninstrumented ABBA causal anchor (E21, B/A): cte 1.076/1.147 (default/inprocess); recursive 1.101/1.136 (default/inprocess); inlist 1.082/1.075 (default/inprocess); leftjoin 1.104/1.048 (default/inprocess)

## cte

Probe e2e B/A = 1.0903 [1.0129, 1.1021] (hw 4.1 %); ABBA anchor 1.076/1.147; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 43440.8 | 44869.9 | 1.0234 | [0.9248, 1.0645] | +6.8% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +9.4% |
| prepare | 47878.4 | 55723.6 | 1.1718 | [1.1241, 1.2137] | +3.8% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +76.5% |
| plan | 942.7 | 1400.4 | 1.4546 | [1.4130, 1.5235] | +3.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +4.1% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 53.8 | 85.9 | 1.6283 | [1.5136, 1.7549] | +7.4% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.3% |
| get_db_command | 105.3 | 85.7 | 0.7788 | [0.7211, 0.9529] | +14.9% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.2% |
| execute | 19121.5 | 19315.3 | 1.0168 | [0.9771, 1.0267] | +2.4% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +3.0% |
| read_rows | 3823.8 | 3903.8 | 1.0192 | [0.9201, 1.0523] | +6.5% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.7% |
| map_rows | 1481.6 | 1483.9 | 1.0055 | [0.9010, 1.0250] | +6.2% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.1% |
| cow_description | 0.0 | 378.3 | — | — | — | 0.00/4.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +3.5% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 280.4 | 358.4 | 1.2520 | [1.2276, 1.3255] | +3.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.6% |
| planner_recipe | 45142.7 | 52355.4 | 1.1690 | [1.1173, 1.2104] | +4.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +70.9% |
| prepare_pipeline | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call | +0.0% |
| prep_preamble | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType | +0.0% |
| nav_expand | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings | +0.0% |
| get_from | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd) | +0.0% |
| prepare_from | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock | +0.0% |
| prepare_join | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin | +0.0% |
| prepare_columns | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability | +0.0% |
| prepare_where | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters | +0.0% |
| prepare_prewhere | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere | +0.0% |
| prepare_arrayjoin | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin | +0.0% |
| prepare_grouping | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows | +0.0% |
| prepare_sorting | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting | +0.0% |
| prepare_union | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips | +0.0% |
| prepare_ctes | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes | +0.0% |
| prepare_hints | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints | +0.0% |
| unkeyed_scans | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues | +0.0% |
| from_hash | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from) | +0.0% |
| prepare_exclusive | 1307.7 | 1405.2 | 1.0939 | [0.9554, 1.1951] | +11.0% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.1% |
| legacy_prepare_exclusive | 46887.6 | 54262.2 | 1.1653 | [1.1170, 1.2078] | +3.9% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +72.1% |
| planner_preamble | 45426.5 | 52714.6 | 1.1697 | [1.1180, 1.2109] | +4.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +71.7% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| end_to_end | 120055.7 | 129869.9 | 1.0903 | [1.0129, 1.1021] | +4.1% | 1/1 | -744.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 50776.2 | 58756.0 | 1.1655 | [1.1143, 1.1978] | +3.6% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +77.5% |
| residual_unattributed | 4422.6 | 4580.1 | 1.0251 | [0.9810, 1.0561] | +3.7% | — | — | unattributed residual (all measured phases added back) | +1.0% |

Verdict: e2e delta +10750.1 ns/op; full phase sum +10681.0; unattributed residual +109.9 ns/op (+1.0%); dispatch six-term residual +8332.8 ns/op (+77.5%); combined CI hw 6531.0 ns; reconciles=True; residual insignificant=True; named=plan, cow_shape, planner_preamble, planner_gate, planner_recipe.

## recursive

Probe e2e B/A = 1.1097 [1.0475, 1.1509] (hw 4.7 %); ABBA anchor 1.101/1.136; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 32738.9 | 36943.4 | 1.1284 | [1.0625, 1.1963] | +5.9% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +32.2% |
| prepare | 53732.8 | 60990.1 | 1.1420 | [1.0814, 1.1698] | +3.9% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +58.5% |
| plan | 919.4 | 1357.6 | 1.5172 | [1.4138, 1.5713] | +5.2% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +3.6% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 53.5 | 83.7 | 1.6188 | [1.4048, 1.7856] | +11.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.2% |
| get_db_command | 88.7 | 100.0 | 1.1303 | [1.0361, 1.7393] | +31.1% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.1% |
| execute | 20795.9 | 21429.2 | 1.0352 | [0.9709, 1.0836] | +5.4% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +5.6% |
| read_rows | 6062.9 | 6232.8 | 1.0334 | [0.9668, 1.0717] | +5.1% | 6.00/6.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +1.5% |
| map_rows | 1814.8 | 1820.6 | 1.0222 | [0.9704, 1.0515] | +4.0% | 5.00/5.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.3% |
| cow_description | 0.0 | 705.8 | — | — | — | 0.00/8.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +5.4% |
| cow_clone | 0.0 | 236.8 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +1.8% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 282.6 | 347.5 | 1.2302 | [1.1473, 1.3040] | +6.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.5% |
| planner_recipe | 51458.2 | 57942.9 | 1.1318 | [1.0752, 1.1626] | +3.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +52.0% |
| prepare_pipeline | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call | +0.0% |
| prep_preamble | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType | +0.0% |
| nav_expand | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings | +0.0% |
| get_from | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd) | +0.0% |
| prepare_from | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock | +0.0% |
| prepare_join | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin | +0.0% |
| prepare_columns | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability | +0.0% |
| prepare_where | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters | +0.0% |
| prepare_prewhere | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere | +0.0% |
| prepare_arrayjoin | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin | +0.0% |
| prepare_grouping | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows | +0.0% |
| prepare_sorting | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting | +0.0% |
| prepare_union | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips | +0.0% |
| prepare_ctes | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes | +0.0% |
| prepare_hints | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints | +0.0% |
| unkeyed_scans | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues | +0.0% |
| from_hash | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from) | +0.0% |
| prepare_exclusive | 1068.9 | 1258.3 | 1.1906 | [1.0226, 1.2324] | +8.8% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.5% |
| legacy_prepare_exclusive | 52800.6 | 59552.2 | 1.1343 | [1.0750, 1.1633] | +3.9% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +54.4% |
| planner_preamble | 51743.9 | 58303.3 | 1.1323 | [1.0762, 1.1628] | +3.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +52.5% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| end_to_end | 118952.8 | 131995.9 | 1.1097 | [1.0475, 1.1509] | +4.7% | 1/1 | -1568.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 56598.1 | 63787.9 | 1.1318 | [1.0768, 1.1646] | +3.9% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +57.2% |
| residual_unattributed | 3905.7 | 4005.6 | 1.0255 | [0.9923, 1.0991] | +5.2% | — | — | unattributed residual (all measured phases added back) | +0.8% |

Verdict: e2e delta +13043.1 ns/op; full phase sum +12966.4; unattributed residual +98.4 ns/op (+0.8%); dispatch six-term residual +7459.0 ns/op (+57.2%); combined CI hw 7017.0 ns; reconciles=True; residual insignificant=True; named=construct, prepare_exclusive, plan, cow_shape, get_db_command, planner_preamble, planner_gate, planner_recipe.

## inlist

Probe e2e B/A = 1.0751 [1.0527, 1.1064] (hw 2.5 %); ABBA anchor 1.082/1.075; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 19186.2 | 19714.1 | 1.0250 | [1.0064, 1.0525] | +2.2% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +5.5% |
| prepare | 61207.6 | 67978.0 | 1.1254 | [1.0916, 1.1580] | +2.9% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +87.2% |
| plan | 9248.1 | 11069.5 | 1.1929 | [1.1614, 1.2512] | +3.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +20.1% |
| bind | 13482.5 | 14810.4 | 1.1014 | [1.0460, 1.1244] | +3.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +15.6% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.0% |
| get_db_command | 102.6 | 107.1 | 1.0571 | [0.9479, 1.2634] | +14.9% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.1% |
| execute | 26423.7 | 26622.6 | 1.0071 | [0.9966, 1.0724] | +3.8% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +2.2% |
| read_rows | 4825.1 | 4857.1 | 1.0011 | [0.9714, 1.0268] | +2.8% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.1% |
| map_rows | 1202.7 | 1208.2 | 1.0024 | [0.9696, 1.1284] | +7.9% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.0% |
| cow_description | 0.0 | 143.8 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +1.6% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 94.7 | 134.0 | 1.4186 | [1.2452, 1.4651] | +7.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.4% |
| planner_recipe | 12010.1 | 12491.4 | 1.0705 | [1.0286, 1.0888] | +2.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +9.3% |
| prepare_pipeline | 25385.4 | 28875.2 | 1.1334 | [1.1181, 1.1791] | +2.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call | +38.8% |
| prep_preamble | 494.7 | 923.7 | 1.9557 | [1.6468, 1.9816] | +8.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType | +5.2% |
| nav_expand | 379.1 | 372.0 | 0.9819 | [0.9749, 1.0550] | +4.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings | -0.1% |
| get_from | 231.3 | 242.0 | 1.0858 | [0.8678, 1.1432] | +12.7% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd) | +0.2% |
| prepare_from | 140.5 | 131.4 | 0.9925 | [0.8129, 1.0269] | +10.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock | -0.0% |
| prepare_join | 81.0 | 116.8 | 1.4501 | [1.2315, 1.9142] | +23.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin | +0.4% |
| prepare_columns | 8144.9 | 8932.2 | 1.1045 | [1.0601, 1.1538] | +4.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability | +9.5% |
| prepare_where | 11498.7 | 12301.0 | 1.0714 | [1.0289, 1.1078] | +3.7% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters | +9.4% |
| prepare_prewhere | 80.4 | 69.0 | 0.8489 | [0.8131, 0.9843] | +10.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere | -0.1% |
| prepare_arrayjoin | 68.2 | 62.7 | 0.9852 | [0.8616, 1.0640] | +10.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin | -0.0% |
| prepare_grouping | 389.0 | 605.7 | 1.5967 | [1.4500, 1.6125] | +5.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows | +2.6% |
| prepare_sorting | 97.3 | 93.9 | 0.9564 | [0.8115, 1.0990] | +15.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting | -0.1% |
| prepare_union | 34.7 | 100.6 | 2.5698 | [2.5440, 3.1016] | +10.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips | +0.7% |
| prepare_ctes | 302.2 | 390.3 | 1.2739 | [1.1955, 1.5130] | +12.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes | +1.0% |
| prepare_hints | 66.8 | 90.8 | 1.3881 | [1.2577, 1.4927] | +8.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints | +0.3% |
| unkeyed_scans | 1486.7 | 1791.3 | 1.2115 | [1.1744, 1.2739] | +4.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues | +3.5% |
| from_hash | 926.8 | 1082.3 | 1.1979 | [1.1419, 1.2257] | +3.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from) | +2.0% |
| prepare_exclusive | 850.8 | 954.9 | 1.1449 | [1.0920, 1.1675] | +3.3% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.4% |
| legacy_prepare_exclusive | 38481.7 | 42378.8 | 1.1159 | [1.0909, 1.1491] | +2.6% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +50.6% |
| planner_preamble | 12114.7 | 12627.3 | 1.0737 | [1.0311, 1.0905] | +2.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +9.8% |
| pipeline_remainder | 959.7 | 1600.3 | 1.6735 | [1.5915, 1.7436] | +4.5% | 1.00/1.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +7.3% |
| end_to_end | 116960.4 | 125147.5 | 1.0751 | [1.0527, 1.1064] | +2.5% | 1/1 | -360.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 64273.3 | 71037.8 | 1.1212 | [1.0873, 1.1540] | +3.0% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +88.5% |
| residual_unattributed | 3866.1 | 4036.5 | 1.0548 | [1.0266, 1.0913] | +3.1% | — | — | unattributed residual (all measured phases added back) | +2.4% |

Verdict: e2e delta +8779.4 ns/op; full phase sum +8567.7; unattributed residual +211.7 ns/op (+2.4%); dispatch six-term residual +7765.7 ns/op (+88.5%); combined CI hw 3504.2 ns; reconciles=True; residual insignificant=True; named=construct, prepare_exclusive, plan, bind, planner_preamble, planner_gate, planner_recipe, prepare_pipeline, prep_preamble, prepare_join, prepare_columns, prepare_where, prepare_grouping, prepare_union, prepare_ctes, prepare_hints, unkeyed_scans, from_hash.

## leftjoin

Probe e2e B/A = 1.1257 [1.1100, 1.1312] (hw 0.9 %); ABBA anchor 1.104/1.048; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 21880.7 | 24115.8 | 1.0843 | [1.0573, 1.1232] | +3.0% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +18.4% |
| prepare | 28313.5 | 35282.5 | 1.2413 | [1.2318, 1.2570] | +1.0% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +66.6% |
| plan | 927.6 | 1393.0 | 1.4895 | [1.4489, 1.5499] | +3.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +4.4% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 52.0 | 84.9 | 1.6480 | [1.5492, 1.7433] | +5.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.3% |
| get_db_command | 78.0 | 74.6 | 0.8758 | [0.7582, 1.1012] | +19.6% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.1% |
| execute | 16765.2 | 17565.0 | 1.0387 | [1.0341, 1.0537] | +0.9% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +6.3% |
| read_rows | 5154.4 | 5195.9 | 1.0082 | [0.9820, 1.0279] | +2.3% | 11.00/11.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.4% |
| map_rows | 5292.8 | 5329.7 | 1.0027 | [0.9859, 1.0399] | +2.7% | 10.00/10.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.1% |
| cow_description | 0.0 | 209.1 | — | — | — | 0.00/2.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +2.0% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 78.8 | 101.3 | 1.2885 | [1.0294, 1.3913] | +14.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.2% |
| planner_recipe | 26275.2 | 32699.7 | 1.2349 | [1.2217, 1.2537] | +1.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +60.2% |
| prepare_pipeline | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call | +0.0% |
| prep_preamble | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType | +0.0% |
| nav_expand | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings | +0.0% |
| get_from | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd) | +0.0% |
| prepare_from | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock | +0.0% |
| prepare_join | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin | +0.0% |
| prepare_columns | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability | +0.0% |
| prepare_where | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters | +0.0% |
| prepare_prewhere | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere | +0.0% |
| prepare_arrayjoin | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin | +0.0% |
| prepare_grouping | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows | +0.0% |
| prepare_sorting | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting | +0.0% |
| prepare_union | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips | +0.0% |
| prepare_ctes | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes | +0.0% |
| prepare_hints | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints | +0.0% |
| unkeyed_scans | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues | +0.0% |
| from_hash | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from) | +0.0% |
| prepare_exclusive | 903.0 | 1056.8 | 1.1849 | [0.9986, 1.3106] | +13.2% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.6% |
| legacy_prepare_exclusive | 27305.9 | 33802.3 | 1.2329 | [1.2206, 1.2487] | +1.1% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +61.6% |
| planner_preamble | 26353.0 | 32798.2 | 1.2342 | [1.2219, 1.2541] | +1.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +60.2% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| end_to_end | 81724.9 | 92227.4 | 1.1257 | [1.1100, 1.1312] | +0.9% | 1/1 | -280.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 31633.0 | 38834.0 | 1.2220 | [1.2131, 1.2434] | +1.2% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +68.2% |
| residual_unattributed | 4263.3 | 4589.6 | 1.0866 | [1.0571, 1.1440] | +4.0% | — | — | unattributed residual (all measured phases added back) | +3.6% |

Verdict: e2e delta +10282.1 ns/op; full phase sum +9747.9; unattributed residual +372.4 ns/op (+3.6%); dispatch six-term residual +7017.2 ns/op (+68.2%); combined CI hw 1236.4 ns; reconciles=True; residual insignificant=True; named=construct, plan, cow_shape, execute, planner_preamble, planner_gate, planner_recipe.

