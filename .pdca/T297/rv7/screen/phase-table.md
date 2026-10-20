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

Probe e2e B/A = 1.0505 [1.0052, 1.1400] (hw 6.4 %); ABBA anchor 1.076/1.147; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 49017.7 | 49681.0 | 1.0151 | [0.9537, 1.0895] | +6.7% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +10.6% |
| prepare | 53374.0 | 60518.1 | 1.1339 | [1.0742, 1.2118] | +6.1% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +104.9% |
| plan | 1046.5 | 1565.1 | 1.5199 | [1.3788, 1.6139] | +7.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +7.9% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 70.2 | 91.0 | 1.3234 | [1.1517, 1.5297] | +14.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.3% |
| get_db_command | 115.9 | 116.2 | 1.0359 | [0.8588, 1.0933] | +11.3% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.1% |
| execute | 21136.6 | 20943.0 | 0.9876 | [0.9496, 1.0984] | +7.5% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | -3.8% |
| read_rows | 4383.9 | 4211.5 | 0.9620 | [0.9262, 1.0792] | +7.9% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | -2.4% |
| map_rows | 1639.7 | 1764.9 | 1.0484 | [0.9639, 1.1746] | +10.0% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +1.2% |
| cow_description | 0.0 | 325.6 | — | — | — | 0.00/3.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +4.8% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 313.0 | 393.7 | 1.2229 | [1.1633, 1.3153] | +6.2% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +1.0% |
| planner_recipe | 50576.4 | 57076.2 | 1.1285 | [1.0737, 1.2063] | +5.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +95.4% |
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
| rcp_capture | 34990.7 | 39339.9 | 1.1243 | [1.0482, 1.1964] | +6.6% | 2.00/2.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +63.9% |
| rcp_fingerprint | 12373.2 | 13868.9 | 1.1134 | [1.0587, 1.1947] | +6.1% | 2.00/2.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | +20.7% |
| rcp_filter_scan | 4007.0 | 3968.9 | 0.9897 | [0.9642, 1.1303] | +8.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | -0.6% |
| rcp_lookup | 15016.5 | 15500.0 | 1.0321 | [0.9917, 1.1039] | +5.4% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | +7.1% |
| rcp_matches | 16560.1 | 16970.6 | 1.0240 | [0.9720, 1.1045] | +6.5% | 2.00/2.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +5.8% |
| rcp_transfer | 1794.2 | 3372.3 | 1.8392 | [1.7964, 1.9721] | +4.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +22.7% |
| rcp_copyto | 1558.1 | 0.0 | 0.0000 | [0.0000, 0.0000] | — | 1.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | -22.9% |
| rcp_copyto_members | 41.7 | 0.0 | 0.0000 | [0.0000, 0.0000] | — | 1.00/0.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | -0.6% |
| rcp_transfer_edges | 141.2 | 3163.4 | 22.5266 | [21.1568, 25.7709] | +10.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +44.5% |
| rcp_staged | 4430.1 | 5986.6 | 1.3592 | [1.2858, 1.4097] | +4.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +22.9% |
| rcp_qplan_ctor | 66.8 | 132.1 | 1.8802 | [1.6335, 2.2311] | +15.9% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +0.9% |
| rcp_qplan_hash | 1510.2 | 2156.9 | 1.4638 | [1.3969, 1.5193] | +4.2% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +9.9% |
| rcp_equals1 | 2547.4 | 3153.2 | 1.2409 | [1.1667, 1.2938] | +5.1% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +9.1% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 1393.4 | 1454.0 | 1.0674 | [0.8439, 1.0978] | +11.9% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.3% |
| legacy_prepare_exclusive | 52254.2 | 58911.7 | 1.1274 | [1.0663, 1.2036] | +6.1% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +97.7% |
| planner_preamble | 50904.1 | 57471.1 | 1.1290 | [1.0744, 1.2070] | +5.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +96.4% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| capture_exclusive | 22533.1 | 25471.0 | 1.1304 | [1.0425, 1.1974] | +6.9% | 2.00/2.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +43.1% |
| lookup_exclusive | -1678.6 | -1480.6 | — | — | — | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | +2.2% |
| copyto_exclusive | 1513.8 | 0.0 | 0.0000 | [0.0000, 0.0000] | — | 1.00/0.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | -22.2% |
| qplan_ctor_exclusive | -1440.7 | -2024.8 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -9.0% |
| transfer_exclusive | 238.8 | 3372.3 | 14.4640 | [13.5187, 15.5275] | +6.9% | 1.00/1.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +46.3% |
| staged_exclusive | 1832.0 | 2711.9 | 1.5108 | [1.4156, 1.5662] | +5.0% | 1.00/1.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | +13.6% |
| planner_recipe_exclusive | -9673.0 | -10969.4 | — | — | — | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | -20.1% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 135143.8 | 142074.6 | 1.0505 | [1.0052, 1.1400] | +6.4% | 1/1 | -784.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 57053.4 | 63924.9 | 1.1205 | [1.0737, 1.2065] | +5.9% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +100.9% |
| residual_unattributed | 4869.3 | 4847.5 | 0.9908 | [0.9556, 1.1143] | +8.0% | — | — | unattributed residual (all measured phases added back) | -0.7% |

Verdict: e2e delta +6811.6 ns/op; full phase sum +6993.6; unattributed residual -45.4 ns/op (-0.7%); dispatch six-term residual +6871.5 ns/op (+100.9%); combined CI hw 10389.3 ns; reconciles=True; residual insignificant=True; named=plan, cow_shape, planner_preamble, planner_gate, planner_recipe.

## recursive

Probe e2e B/A = 1.0623 [1.0426, 1.1480] (hw 5.0 %); ABBA anchor 1.101/1.136; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 35903.3 | 40805.7 | 1.1125 | [1.0631, 1.2152] | +6.8% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +57.0% |
| prepare | 59872.0 | 67527.6 | 1.0837 | [1.0569, 1.1646] | +5.0% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +62.8% |
| plan | 1043.3 | 1473.3 | 1.4115 | [1.3610, 1.5170] | +5.5% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +5.3% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 68.2 | 86.3 | 1.2675 | [1.2093, 1.4535] | +9.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.2% |
| get_db_command | 101.2 | 109.5 | 1.0143 | [0.8446, 1.2557] | +20.3% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.0% |
| execute | 22361.7 | 23434.1 | 1.0154 | [0.9718, 1.0616] | +4.4% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +4.2% |
| read_rows | 6806.2 | 6751.2 | 0.9755 | [0.9351, 1.0317] | +4.9% | 6.00/6.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | -2.1% |
| map_rows | 2013.9 | 2094.1 | 1.0331 | [1.0143, 1.0723] | +2.8% | 5.00/5.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.8% |
| cow_description | 0.0 | 667.7 | — | — | — | 0.00/7.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +8.2% |
| cow_clone | 0.0 | 247.0 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +3.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 307.6 | 415.0 | 1.2544 | [1.1734, 1.5333] | +14.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +1.0% |
| planner_recipe | 57223.4 | 64266.7 | 1.0780 | [1.0500, 1.1563] | +4.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +55.7% |
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
| rcp_capture | 78744.8 | 85135.1 | 1.0450 | [1.0063, 1.1077] | +4.9% | 4.00/4.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +43.4% |
| rcp_fingerprint | 18240.7 | 19633.6 | 1.0355 | [0.9969, 1.1072] | +5.3% | 4.00/4.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | +7.9% |
| rcp_filter_scan | 2085.7 | 2109.3 | 0.9980 | [0.9472, 1.0662] | +6.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | -0.1% |
| rcp_lookup | 12950.0 | 14017.1 | 1.0354 | [1.0124, 1.1022] | +4.3% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | +5.5% |
| rcp_matches | 20665.2 | 22107.7 | 1.0248 | [0.9893, 1.0860] | +4.7% | 3.00/3.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +6.4% |
| rcp_transfer | 1364.4 | 2905.4 | 2.1082 | [1.9346, 2.2692] | +7.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +18.9% |
| rcp_copyto | 1122.1 | 0.0 | 0.0000 | [0.0000, 0.0000] | — | 1.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | -13.7% |
| rcp_copyto_members | 91.4 | 222.9 | 2.3666 | [1.8328, 2.8076] | +20.6% | 2.00/1.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | +1.5% |
| rcp_transfer_edges | 148.3 | 2743.4 | 19.8325 | [16.6232, 22.2950] | +14.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +31.7% |
| rcp_staged | 4381.7 | 5608.6 | 1.2768 | [1.1967, 1.3639] | +6.5% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +16.1% |
| rcp_qplan_ctor | 61.8 | 101.0 | 1.6134 | [1.5682, 1.7480] | +5.6% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +0.5% |
| rcp_qplan_hash | 1487.0 | 1942.2 | 1.2939 | [1.1948, 1.3970] | +7.8% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +6.0% |
| rcp_equals1 | 2596.8 | 3119.5 | 1.2385 | [1.1573, 1.2997] | +5.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +7.7% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 1206.5 | 1320.0 | 1.0677 | [0.9978, 1.1991] | +9.4% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.0% |
| legacy_prepare_exclusive | 58769.9 | 65969.0 | 1.0780 | [1.0504, 1.1589] | +5.0% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +57.4% |
| planner_preamble | 57528.1 | 64649.0 | 1.0793 | [1.0506, 1.1580] | +5.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +56.4% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| capture_exclusive | 60504.1 | 65501.5 | 1.0479 | [1.0092, 1.1079] | +4.7% | 4.00/4.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +35.5% |
| lookup_exclusive | -7715.2 | -8046.4 | — | — | — | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | -1.4% |
| copyto_exclusive | 1031.8 | -222.9 | -0.2071 | [-0.2371, -0.1862] | -12.3% | 1.00/0.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | -15.2% |
| qplan_ctor_exclusive | -1424.6 | -1832.6 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -5.4% |
| transfer_exclusive | 242.3 | 2905.4 | 12.5059 | [11.1129, 13.2716] | +8.6% | 1.00/1.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +32.5% |
| staged_exclusive | 1777.0 | 2388.1 | 1.3316 | [1.2285, 1.4451] | +8.1% | 1.00/1.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | +8.1% |
| planner_recipe_exclusive | -42243.1 | -45468.8 | — | — | — | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | -23.3% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 131226.5 | 145030.0 | 1.0623 | [1.0426, 1.1480] | +5.0% | 1/1 | -1568.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 62861.2 | 70495.4 | 1.0771 | [1.0538, 1.1564] | +4.8% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +59.3% |
| residual_unattributed | 4263.9 | 4284.0 | 1.0109 | [0.9419, 1.0774] | +6.7% | — | — | unattributed residual (all measured phases added back) | +0.6% |

Verdict: e2e delta +8172.2 ns/op; full phase sum +8273.3; unattributed residual +46.2 ns/op (+0.6%); dispatch six-term residual +4847.3 ns/op (+59.3%); combined CI hw 8108.9 ns; reconciles=True; residual insignificant=True; named=construct, plan, cow_shape, map_rows, planner_preamble, planner_gate, planner_recipe.

## inlist

Probe e2e B/A = 1.0441 [0.9523, 1.1447] (hw 9.2 %); ABBA anchor 1.082/1.075; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 21081.0 | 21368.1 | 1.0144 | [0.9157, 1.0460] | +6.4% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +5.3% |
| prepare | 67449.5 | 74444.6 | 1.0839 | [0.9887, 1.2081] | +10.1% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +100.1% |
| plan | 10182.5 | 12413.5 | 1.1916 | [1.0868, 1.3048] | +9.1% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +34.5% |
| bind | 15028.4 | 15748.9 | 1.0462 | [0.9305, 1.1345] | +9.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +12.2% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.0% |
| get_db_command | 108.2 | 110.0 | 0.9818 | [0.9122, 1.1674] | +13.0% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.0% |
| execute | 29162.0 | 28997.4 | 0.9919 | [0.9054, 1.0857] | +9.1% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | -4.1% |
| read_rows | 5269.0 | 5302.3 | 0.9817 | [0.8820, 1.1353] | +12.9% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | -1.7% |
| map_rows | 1331.0 | 1405.7 | 1.0642 | [0.9336, 1.1373] | +9.6% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +1.5% |
| cow_description | 0.0 | 162.6 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +2.9% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 110.7 | 151.0 | 1.3201 | [1.2427, 1.5553] | +11.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.7% |
| planner_recipe | 13337.6 | 13815.8 | 1.0312 | [0.9328, 1.1302] | +9.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +7.4% |
| prepare_pipeline | 27862.0 | 31520.9 | 1.1023 | [0.9920, 1.2508] | +11.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call | +50.9% |
| prep_preamble | 546.7 | 1049.3 | 1.9387 | [1.6909, 2.1606] | +12.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType | +9.0% |
| nav_expand | 381.6 | 403.0 | 1.0203 | [0.9053, 1.1213] | +10.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings | +0.1% |
| get_from | 250.3 | 264.8 | 1.0400 | [0.8493, 1.1867] | +16.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd) | +0.2% |
| prepare_from | 144.6 | 152.4 | 1.0313 | [0.9597, 1.2268] | +12.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock | +0.1% |
| prepare_join | 93.9 | 95.0 | 0.9735 | [0.7913, 1.1654] | +19.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin | -0.0% |
| prepare_columns | 8994.6 | 9534.4 | 1.0413 | [0.9157, 1.1862] | +13.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability | +6.6% |
| prepare_where | 12698.2 | 13488.1 | 1.0350 | [0.9320, 1.1829] | +12.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters | +7.9% |
| prepare_prewhere | 74.0 | 72.6 | 0.9267 | [0.8366, 1.1314] | +15.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere | -0.1% |
| prepare_arrayjoin | 88.2 | 62.0 | 0.6780 | [0.6215, 0.8289] | +15.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin | -0.5% |
| prepare_grouping | 401.5 | 693.0 | 1.7240 | [1.4640, 1.8143] | +10.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows | +5.0% |
| prepare_sorting | 84.0 | 65.4 | 0.7762 | [0.6403, 0.9702] | +21.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting | -0.3% |
| prepare_union | 40.6 | 111.9 | 2.5197 | [1.9820, 3.0466] | +21.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips | +1.2% |
| prepare_ctes | 316.0 | 443.6 | 1.3940 | [1.2727, 1.4899] | +7.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes | +2.2% |
| prepare_hints | 63.6 | 102.5 | 1.5502 | [1.0121, 1.8726] | +27.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints | +0.6% |
| unkeyed_scans | 1669.6 | 2023.1 | 1.2229 | [1.1240, 1.3922] | +11.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues | +6.4% |
| from_hash | 1053.5 | 1147.8 | 1.0601 | [0.9259, 1.1987] | +12.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from) | +1.2% |
| rcp_capture | 10877.9 | 11256.9 | 1.0225 | [0.9313, 1.1237] | +9.4% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +4.3% |
| rcp_fingerprint | 7072.2 | 7121.5 | 0.9966 | [0.9142, 1.0911] | +8.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | -0.4% |
| rcp_filter_scan | 1517.8 | 1581.5 | 1.0148 | [0.9333, 1.1109] | +8.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | +0.4% |
| rcp_lookup | 532.3 | 565.7 | 1.0830 | [0.8471, 1.1525] | +14.1% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | +0.8% |
| rcp_matches | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +0.0% |
| rcp_transfer | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +0.0% |
| rcp_copyto | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | +0.0% |
| rcp_copyto_members | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | +0.0% |
| rcp_transfer_edges | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +0.0% |
| rcp_staged | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +0.0% |
| rcp_qplan_ctor | 598.7 | 789.5 | 1.3707 | [1.2203, 1.4636] | +8.9% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +4.1% |
| rcp_qplan_hash | 1643.2 | 2470.1 | 1.4560 | [1.3076, 1.6004] | +10.1% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +13.1% |
| rcp_equals1 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +0.0% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 933.1 | 1086.6 | 1.1602 | [1.0217, 1.2238] | +8.7% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +2.7% |
| legacy_prepare_exclusive | 42238.7 | 46530.1 | 1.0753 | [0.9802, 1.2126] | +10.8% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +56.3% |
| planner_preamble | 13443.5 | 13954.1 | 1.0332 | [0.9357, 1.1332] | +9.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +7.9% |
| pipeline_remainder | 1091.8 | 1837.6 | 1.6641 | [1.5700, 1.8494] | +8.4% | 1.00/1.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +13.5% |
| capture_exclusive | 3867.3 | 4226.7 | 1.0637 | [0.9615, 1.1918] | +10.8% | 1.00/1.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +4.3% |
| lookup_exclusive | 532.3 | 565.7 | 1.0830 | [0.8471, 1.1525] | +14.1% | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | +0.8% |
| copyto_exclusive | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | +0.0% |
| qplan_ctor_exclusive | -1057.6 | -1671.5 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -9.7% |
| transfer_exclusive | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +0.0% |
| staged_exclusive | -598.7 | -789.5 | — | — | — | 0.00/0.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | -4.1% |
| planner_recipe_exclusive | 437.9 | 499.6 | 1.1399 | [0.9915, 1.5542] | +24.7% | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | +1.1% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 128983.0 | 135582.5 | 1.0441 | [0.9523, 1.1447] | +9.2% | 1/1 | -360.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 70641.3 | 77476.2 | 1.0777 | [0.9856, 1.2007] | +10.0% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +97.2% |
| residual_unattributed | 4199.1 | 4280.9 | 0.9786 | [0.9326, 1.1164] | +9.4% | — | — | unattributed residual (all measured phases added back) | -1.6% |

Verdict: e2e delta +5627.0 ns/op; full phase sum +5663.8; unattributed residual -89.7 ns/op (-1.6%); dispatch six-term residual +5470.1 ns/op (+97.2%); combined CI hw 13473.5 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, planner_gate, prep_preamble, prepare_grouping, prepare_union, prepare_ctes, prepare_hints, unkeyed_scans.

## leftjoin

Probe e2e B/A = 1.0842 [1.0267, 1.1055] (hw 3.6 %); ABBA anchor 1.104/1.048; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 24086.5 | 26083.4 | 1.0545 | [0.9668, 1.1024] | +6.4% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +17.0% |
| prepare | 31420.5 | 37016.0 | 1.1676 | [1.1253, 1.1930] | +2.9% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +68.1% |
| plan | 1033.9 | 1527.6 | 1.4569 | [1.3357, 1.5042] | +5.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +6.1% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 65.5 | 83.4 | 1.2527 | [1.2150, 1.3574] | +5.7% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.2% |
| get_db_command | 99.7 | 97.4 | 0.9618 | [0.8635, 1.1766] | +16.3% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.0% |
| execute | 18192.9 | 19062.3 | 1.0388 | [0.9736, 1.0512] | +3.7% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +9.2% |
| read_rows | 5595.1 | 5754.4 | 0.9992 | [0.9703, 1.0426] | +3.6% | 11.00/11.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | -0.1% |
| map_rows | 5809.6 | 5928.8 | 1.0097 | [0.9508, 1.0466] | +4.7% | 10.00/10.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.7% |
| cow_description | 0.0 | 200.5 | — | — | — | 0.00/2.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +2.6% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 80.2 | 111.9 | 1.3446 | [1.1979, 1.4753] | +10.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.4% |
| planner_recipe | 29269.9 | 34236.2 | 1.1598 | [1.1191, 1.1810] | +2.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +60.2% |
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
| rcp_capture | 10771.2 | 12108.4 | 1.1211 | [1.0638, 1.1403] | +3.4% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +16.7% |
| rcp_fingerprint | 5472.1 | 6106.2 | 1.0985 | [1.0258, 1.1309] | +4.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | +6.9% |
| rcp_filter_scan | 2972.7 | 3032.4 | 1.0308 | [0.9841, 1.0531] | +3.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | +1.2% |
| rcp_lookup | 9252.9 | 9793.8 | 1.0395 | [1.0016, 1.0686] | +3.2% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | +4.7% |
| rcp_matches | 7772.2 | 8273.1 | 1.0415 | [1.0056, 1.0697] | +3.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +4.1% |
| rcp_transfer | 1631.0 | 3112.5 | 1.8675 | [1.8034, 1.9504] | +3.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +18.6% |
| rcp_copyto | 1434.0 | 0.0 | 0.0000 | [0.0000, 0.0000] | — | 1.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | -18.5% |
| rcp_copyto_members | 42.0 | 0.0 | 0.0000 | [0.0000, 0.0000] | — | 1.00/0.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | -0.5% |
| rcp_transfer_edges | 123.6 | 2902.0 | 24.0725 | [21.6322, 29.4030] | +16.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +35.8% |
| rcp_staged | 4177.1 | 5660.2 | 1.3196 | [1.2583, 1.3920] | +5.1% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +17.1% |
| rcp_qplan_ctor | 59.3 | 117.2 | 1.9119 | [1.7662, 2.4497] | +17.9% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +0.7% |
| rcp_qplan_hash | 1407.4 | 1931.3 | 1.3710 | [1.2790, 1.4823] | +7.4% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +6.5% |
| rcp_equals1 | 2421.3 | 3082.8 | 1.2659 | [1.2091, 1.3088] | +3.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +8.3% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 995.0 | 1089.4 | 1.1383 | [0.9651, 1.2524] | +12.6% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.7% |
| legacy_prepare_exclusive | 30343.2 | 35406.2 | 1.1580 | [1.1161, 1.1835] | +2.9% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +62.3% |
| planner_preamble | 29348.2 | 34348.1 | 1.1605 | [1.1195, 1.1815] | +2.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +60.6% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| capture_exclusive | 5327.1 | 6084.0 | 1.1348 | [1.1038, 1.1600] | +2.5% | 1.00/1.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +9.1% |
| lookup_exclusive | 1520.1 | 1538.4 | 1.0061 | [0.9782, 1.0897] | +5.5% | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | +0.1% |
| copyto_exclusive | 1388.3 | 0.0 | 0.0000 | [0.0000, 0.0000] | — | 1.00/0.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | -17.9% |
| qplan_ctor_exclusive | -1346.1 | -1798.5 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -5.9% |
| transfer_exclusive | 216.4 | 3112.5 | 14.5598 | [12.6815, 15.9809] | +11.3% | 1.00/1.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +36.9% |
| staged_exclusive | 1694.6 | 2362.5 | 1.3877 | [1.2730, 1.5063] | +8.4% | 1.00/1.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | +8.2% |
| planner_recipe_exclusive | 497.2 | 678.4 | 1.3173 | [1.2239, 1.4276] | +7.7% | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | +2.0% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 89876.5 | 98610.1 | 1.0842 | [1.0267, 1.1055] | +3.6% | 1/1 | -320.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 35205.3 | 40672.4 | 1.1515 | [1.1008, 1.1758] | +3.3% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +69.3% |
| residual_unattributed | 4610.1 | 4769.3 | 1.0331 | [0.9059, 1.0876] | +8.8% | — | — | unattributed residual (all measured phases added back) | +1.9% |

Verdict: e2e delta +7763.2 ns/op; full phase sum +7811.3; unattributed residual +150.7 ns/op (+1.9%); dispatch six-term residual +5381.8 ns/op (+69.3%); combined CI hw 4082.8 ns; reconciles=True; residual insignificant=True; named=plan, cow_shape, planner_preamble, planner_gate, planner_recipe.

