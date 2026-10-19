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

Probe e2e B/A = 1.0626 [0.8997, 1.0809] (hw 8.5 %); ABBA anchor 1.076/1.147; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 46208.2 | 44822.7 | 0.9894 | [0.8493, 1.0369] | +9.5% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | -6.2% |
| prepare | 49493.8 | 55595.4 | 1.1564 | [1.0140, 1.1775] | +7.1% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +95.8% |
| plan | 977.4 | 1387.2 | 1.4172 | [1.2211, 1.4649] | +8.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +5.2% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 53.5 | 99.6 | 1.8725 | [1.5814, 1.9990] | +11.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.6% |
| get_db_command | 106.2 | 80.5 | 0.7750 | [0.6573, 0.8139] | +10.1% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.3% |
| execute | 19852.4 | 19529.8 | 0.9838 | [0.7608, 1.0245] | +13.4% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | -4.1% |
| read_rows | 3968.6 | 3866.4 | 0.9876 | [0.8324, 1.0151] | +9.3% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | -0.6% |
| map_rows | 1575.9 | 1503.8 | 0.9602 | [0.8251, 1.0553] | +12.0% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | -0.8% |
| cow_description | 0.0 | 480.0 | — | — | — | 0.00/4.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +6.1% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 301.6 | 324.7 | 1.0785 | [0.9636, 1.1588] | +9.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.3% |
| planner_recipe | 46878.8 | 52415.3 | 1.1530 | [1.0070, 1.1745] | +7.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +88.6% |
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
| prepare_exclusive | 1318.9 | 1389.1 | 1.0704 | [1.0013, 1.1376] | +6.4% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.2% |
| legacy_prepare_exclusive | 48467.9 | 54097.6 | 1.1497 | [1.0080, 1.1724] | +7.2% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +89.8% |
| planner_preamble | 47182.8 | 52739.3 | 1.1528 | [1.0067, 1.1741] | +7.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +89.1% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| end_to_end | 125865.5 | 129807.3 | 1.0626 | [0.8997, 1.0809] | +8.5% | 1/1 | -528.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 52871.5 | 58925.6 | 1.1518 | [1.0095, 1.1722] | +7.1% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +98.9% |
| residual_unattributed | 4510.5 | 4827.2 | 1.0376 | [0.9771, 1.1288] | +7.3% | — | — | unattributed residual (all measured phases added back) | +2.1% |

Verdict: e2e delta +7857.3 ns/op; full phase sum +7436.8; unattributed residual +166.0 ns/op (+2.1%); dispatch six-term residual +7771.1 ns/op (+98.9%); combined CI hw 13086.3 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, cow_shape, planner_preamble, planner_recipe.

## recursive

Probe e2e B/A = 1.0931 [0.8170, 1.1129] (hw 13.5 %); ABBA anchor 1.101/1.136; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 34000.2 | 36481.3 | 1.1077 | [0.7558, 1.1284] | +16.8% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +32.0% |
| prepare | 54727.0 | 61025.7 | 1.1194 | [0.9278, 1.1364] | +9.3% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +58.1% |
| plan | 944.5 | 1354.2 | 1.4567 | [1.2216, 1.5126] | +10.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +3.8% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 57.3 | 93.7 | 1.6882 | [1.4031, 1.9254] | +15.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.3% |
| get_db_command | 92.4 | 111.8 | 1.1831 | [0.9109, 1.2626] | +14.9% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.2% |
| execute | 20925.1 | 21583.0 | 1.0332 | [0.7134, 1.0675] | +17.1% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +6.1% |
| read_rows | 6329.0 | 6347.7 | 1.0107 | [0.8402, 1.0430] | +10.0% | 6.00/6.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.6% |
| map_rows | 1785.2 | 1870.8 | 1.0603 | [0.8374, 1.1097] | +12.8% | 5.00/5.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.9% |
| cow_description | 0.0 | 718.8 | — | — | — | 0.00/8.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +6.4% |
| cow_clone | 0.0 | 273.0 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +2.4% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 307.7 | 312.6 | 1.0175 | [0.8933, 1.2150] | +15.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.0% |
| planner_recipe | 52246.1 | 57989.6 | 1.1127 | [0.9231, 1.1288] | +9.2% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +52.5% |
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
| prepare_exclusive | 1122.5 | 1207.5 | 1.1248 | [0.8546, 1.1998] | +15.3% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.2% |
| legacy_prepare_exclusive | 53738.6 | 59571.9 | 1.1126 | [0.9213, 1.1304] | +9.4% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +53.9% |
| planner_preamble | 52547.3 | 58370.5 | 1.1123 | [0.9230, 1.1290] | +9.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +52.6% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| end_to_end | 121828.8 | 131394.2 | 1.0931 | [0.8170, 1.1129] | +13.5% | 1/1 | -840.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 57480.7 | 63810.7 | 1.1127 | [0.9240, 1.1304] | +9.3% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +57.7% |
| residual_unattributed | 3924.7 | 4010.6 | 1.0176 | [0.8400, 1.0739] | +11.5% | — | — | unattributed residual (all measured phases added back) | +0.6% |

Verdict: e2e delta +11257.1 ns/op; full phase sum +11219.2; unattributed residual +68.3 ns/op (+0.6%); dispatch six-term residual +6490.4 ns/op (+57.7%); combined CI hw 20202.6 ns; reconciles=True; residual insignificant=True; named=plan, cow_shape.

## inlist

Probe e2e B/A = 1.0672 [1.0200, 1.1216] (hw 4.8 %); ABBA anchor 1.082/1.075; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 19416.0 | 19876.1 | 1.0353 | [0.9841, 1.0999] | +5.6% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +8.4% |
| prepare | 62842.7 | 68780.2 | 1.1015 | [1.0620, 1.1481] | +3.9% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +80.3% |
| plan | 9400.5 | 11308.0 | 1.1962 | [1.1556, 1.2528] | +4.1% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +23.3% |
| bind | 13958.2 | 14666.0 | 1.0464 | [0.9928, 1.1078] | +5.5% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +8.4% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.0% |
| get_db_command | 103.2 | 118.2 | 1.1432 | [1.0766, 1.1768] | +4.4% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.2% |
| execute | 26812.2 | 27052.8 | 1.0244 | [0.9678, 1.0644] | +4.7% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +8.2% |
| read_rows | 4959.4 | 4974.1 | 1.0278 | [0.9427, 1.2406] | +14.5% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +1.7% |
| map_rows | 1212.5 | 1224.2 | 0.9829 | [0.8704, 1.1228] | +12.8% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | -0.3% |
| cow_description | 0.0 | 159.2 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +2.0% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 99.5 | 149.2 | 1.4734 | [1.2391, 1.6897] | +15.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.6% |
| planner_recipe | 12201.2 | 13014.0 | 1.0522 | [1.0160, 1.1089] | +4.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +7.9% |
| prepare_pipeline | 26239.9 | 28887.1 | 1.1127 | [1.0569, 1.1768] | +5.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call | +36.9% |
| prep_preamble | 517.5 | 943.7 | 1.8969 | [1.7683, 2.0105] | +6.4% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType | +5.7% |
| nav_expand | 370.0 | 397.6 | 1.0925 | [1.0056, 1.1220] | +5.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings | +0.4% |
| get_from | 241.7 | 249.1 | 1.0140 | [0.9792, 1.1060] | +6.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd) | +0.0% |
| prepare_from | 138.8 | 136.3 | 1.0803 | [0.9118, 1.3959] | +22.4% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock | +0.1% |
| prepare_join | 87.4 | 112.3 | 1.3165 | [1.1634, 2.2212] | +40.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin | +0.3% |
| prepare_columns | 8320.8 | 8836.7 | 1.0582 | [1.0071, 1.1383] | +6.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability | +6.2% |
| prepare_where | 11839.3 | 12270.5 | 1.0414 | [0.9820, 1.1107] | +6.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters | +6.1% |
| prepare_prewhere | 73.7 | 70.0 | 0.9621 | [0.8267, 0.9952] | +8.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere | -0.0% |
| prepare_arrayjoin | 66.0 | 66.7 | 0.9653 | [0.7552, 1.1857] | +22.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin | -0.0% |
| prepare_grouping | 405.4 | 616.3 | 1.4544 | [1.3178, 1.6692] | +12.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows | +2.4% |
| prepare_sorting | 96.2 | 60.7 | 0.6675 | [0.5182, 0.8344] | +23.7% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting | -0.4% |
| prepare_union | 39.5 | 107.5 | 2.7491 | [2.3695, 3.2104] | +15.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips | +0.9% |
| prepare_ctes | 336.0 | 397.6 | 1.1976 | [1.0612, 1.2487] | +7.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes | +0.8% |
| prepare_hints | 70.7 | 94.0 | 1.2534 | [0.7344, 1.6325] | +35.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints | +0.2% |
| unkeyed_scans | 1530.2 | 1849.9 | 1.2210 | [1.1507, 1.2966] | +6.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues | +4.3% |
| from_hash | 985.2 | 1053.6 | 1.1351 | [1.0630, 1.2096] | +6.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from) | +1.6% |
| prepare_exclusive | 860.1 | 1010.6 | 1.1796 | [1.1497, 1.2124] | +2.7% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.9% |
| legacy_prepare_exclusive | 39519.3 | 42913.2 | 1.0975 | [1.0520, 1.1508] | +4.5% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +48.9% |
| planner_preamble | 12300.7 | 13150.2 | 1.0567 | [1.0189, 1.1110] | +4.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +8.7% |
| pipeline_remainder | 975.3 | 1611.0 | 1.6580 | [1.5335, 1.7205] | +5.6% | 1.00/1.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +8.1% |
| end_to_end | 118977.4 | 127008.1 | 1.0672 | [1.0200, 1.1216] | +4.8% | 1/1 | -32.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 65815.0 | 71674.3 | 1.0965 | [1.0555, 1.1447] | +4.1% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +79.4% |
| residual_unattributed | 3803.7 | 3892.7 | 1.0243 | [0.9695, 1.1148] | +7.1% | — | — | unattributed residual (all measured phases added back) | +1.2% |

Verdict: e2e delta +7922.7 ns/op; full phase sum +7690.1; unattributed residual +91.1 ns/op (+1.2%); dispatch six-term residual +6292.5 ns/op (+79.4%); combined CI hw 6609.9 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, get_db_command, planner_preamble, planner_gate, planner_recipe, prepare_pipeline, prep_preamble, nav_expand, prepare_join, prepare_columns, prepare_grouping, prepare_union, prepare_ctes, unkeyed_scans, from_hash.

## leftjoin

Probe e2e B/A = 1.1118 [1.0755, 1.1300] (hw 2.5 %); ABBA anchor 1.104/1.048; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 22080.6 | 23563.9 | 1.0686 | [0.9975, 1.0766] | +3.7% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +16.2% |
| prepare | 28918.9 | 35386.3 | 1.2415 | [1.2078, 1.2634] | +2.2% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +74.0% |
| plan | 965.2 | 1401.1 | 1.4411 | [1.3730, 1.5301] | +5.5% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +4.6% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 50.8 | 96.7 | 1.8457 | [1.6845, 2.1001] | +11.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.5% |
| get_db_command | 95.6 | 72.6 | 0.6883 | [0.5855, 0.8817] | +21.5% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.3% |
| execute | 17143.7 | 17528.4 | 1.0293 | [1.0079, 1.0531] | +2.2% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +5.4% |
| read_rows | 5245.3 | 5294.5 | 1.0049 | [0.9661, 1.0378] | +3.6% | 11.00/11.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.3% |
| map_rows | 5346.0 | 5615.4 | 1.0326 | [0.9951, 1.0792] | +4.1% | 10.00/10.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +1.9% |
| cow_description | 0.0 | 191.7 | — | — | — | 0.00/2.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +2.1% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 77.4 | 101.0 | 1.2150 | [1.0945, 1.3935] | +12.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.2% |
| planner_recipe | 26875.6 | 32764.0 | 1.2337 | [1.2044, 1.2568] | +2.1% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +66.3% |
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
| prepare_exclusive | 926.1 | 1060.1 | 1.1559 | [1.0909, 1.2235] | +5.7% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.5% |
| legacy_prepare_exclusive | 27904.1 | 33909.9 | 1.2327 | [1.2004, 1.2543] | +2.2% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +68.6% |
| planner_preamble | 26950.7 | 32864.8 | 1.2340 | [1.2041, 1.2569] | +2.1% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +66.6% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| end_to_end | 83049.9 | 92067.6 | 1.1118 | [1.0755, 1.1300] | +2.5% | 1/1 | -64.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 32243.0 | 38971.0 | 1.2174 | [1.1902, 1.2395] | +2.0% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +75.5% |
| residual_unattributed | 4245.1 | 4507.2 | 1.0503 | [1.0170, 1.0997] | +3.9% | — | — | unattributed residual (all measured phases added back) | +2.3% |

Verdict: e2e delta +9289.0 ns/op; full phase sum +9004.9; unattributed residual +210.3 ns/op (+2.3%); dispatch six-term residual +7016.8 ns/op (+75.5%); combined CI hw 2573.6 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, cow_shape, execute, planner_preamble, planner_gate, planner_recipe.

