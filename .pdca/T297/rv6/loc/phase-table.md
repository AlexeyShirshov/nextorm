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

Probe e2e B/A = 1.1118 [0.9707, 1.1433] (hw 7.8 %); ABBA anchor 1.076/1.147; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 48221.9 | 49366.1 | 1.0419 | [0.8890, 1.0858] | +9.4% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +14.1% |
| prepare | 53257.6 | 62081.3 | 1.1913 | [1.0724, 1.2433] | +7.2% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +70.4% |
| plan | 1035.8 | 1539.5 | 1.5168 | [1.2075, 1.5826] | +12.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +3.6% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 74.9 | 104.7 | 1.4280 | [1.0455, 1.6327] | +20.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.2% |
| get_db_command | 117.5 | 93.5 | 0.8003 | [0.7055, 0.9505] | +15.3% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.2% |
| execute | 20794.2 | 21347.2 | 1.0486 | [0.9384, 1.0869] | +7.1% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +6.9% |
| read_rows | 4069.6 | 4211.6 | 1.0515 | [0.9420, 1.1129] | +8.1% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +1.4% |
| map_rows | 1663.5 | 1705.1 | 1.0447 | [0.8524, 1.0894] | +11.3% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.5% |
| cow_description | 0.0 | 617.9 | — | — | — | 0.00/4.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +4.3% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 319.6 | 385.2 | 1.2086 | [1.0246, 1.2372] | +8.8% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.5% |
| planner_recipe | 50300.6 | 58594.8 | 1.1895 | [1.0669, 1.2405] | +7.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +65.9% |
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
| rcp_capture | 34818.6 | 37389.7 | 1.0937 | [0.9694, 1.1488] | +8.2% | 2.00/2.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +22.8% |
| rcp_fingerprint | 12623.9 | 13293.6 | 1.0619 | [0.9751, 1.1496] | +8.2% | 2.00/2.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | +5.4% |
| rcp_filter_scan | 3825.0 | 4128.5 | 1.0412 | [0.9880, 1.1425] | +7.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | +1.2% |
| rcp_lookup | 14926.4 | 15869.9 | 1.0735 | [0.9552, 1.1118] | +7.3% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | +7.8% |
| rcp_matches | 16566.3 | 17435.6 | 1.0687 | [0.9515, 1.1222] | +8.0% | 2.00/2.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +7.9% |
| rcp_transfer | 1813.5 | 6052.3 | 3.3644 | [3.1686, 3.7443] | +8.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +29.9% |
| rcp_copyto | 1591.5 | 5597.4 | 3.5497 | [3.3578, 4.0500] | +9.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | +28.3% |
| rcp_copyto_members | 43.5 | 288.9 | 7.2226 | [5.9362, 7.6397] | +11.8% | 1.00/1.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | +1.7% |
| rcp_transfer_edges | 142.3 | 370.4 | 2.5943 | [2.3308, 2.7988] | +9.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +1.5% |
| rcp_staged | 4492.5 | 5770.3 | 1.2878 | [1.2130, 1.4039] | +7.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +9.0% |
| rcp_qplan_ctor | 67.1 | 107.3 | 1.6812 | [1.5710, 1.7796] | +6.2% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +0.3% |
| rcp_qplan_hash | 1517.2 | 1960.0 | 1.2941 | [1.1825, 1.5324] | +13.5% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +3.0% |
| rcp_equals1 | 2526.1 | 3161.0 | 1.2521 | [1.1963, 1.3184] | +4.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +4.4% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 1374.8 | 1478.8 | 1.0778 | [1.0283, 1.1626] | +6.2% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +0.7% |
| legacy_prepare_exclusive | 52068.2 | 60442.0 | 1.1862 | [1.0674, 1.2368] | +7.1% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +66.8% |
| planner_preamble | 50613.1 | 58977.6 | 1.1887 | [1.0675, 1.2404] | +7.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +66.1% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| capture_exclusive | 22226.6 | 24132.9 | 1.1121 | [0.9663, 1.1484] | +8.2% | 2.00/2.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +17.3% |
| lookup_exclusive | -1639.9 | -1565.7 | — | — | — | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | -0.1% |
| copyto_exclusive | 1552.3 | 5281.1 | 3.4613 | [3.2770, 3.9430] | +9.6% | 1.00/1.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | +26.5% |
| qplan_ctor_exclusive | -1450.1 | -1844.5 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -2.7% |
| transfer_exclusive | 236.5 | 465.8 | 1.9893 | [1.8261, 2.0494] | +5.6% | 1.00/1.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +1.5% |
| staged_exclusive | 1905.0 | 2490.9 | 1.3299 | [1.2145, 1.5081] | +11.0% | 1.00/1.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | +4.3% |
| planner_recipe_exclusive | -9632.8 | -10435.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | -4.5% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 132966.2 | 143330.2 | 1.1118 | [0.9707, 1.1433] | +7.8% | 1/1 | -408.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 56793.3 | 65635.5 | 1.1838 | [1.0588, 1.2370] | +7.5% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +72.0% |
| residual_unattributed | 4990.7 | 5098.3 | 1.0710 | [0.9155, 1.1372] | +10.4% | — | — | unattributed residual (all measured phases added back) | +2.4% |

Verdict: e2e delta +14382.6 ns/op; full phase sum +13932.7; unattributed residual +341.8 ns/op (+2.4%); dispatch six-term residual +10355.2 ns/op (+72.0%); combined CI hw 13262.8 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, cow_shape, planner_preamble, planner_gate, planner_recipe.

## recursive

Probe e2e B/A = 1.1354 [1.0622, 1.1540] (hw 4.0 %); ABBA anchor 1.101/1.136; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 36160.1 | 41580.2 | 1.1556 | [1.0499, 1.1901] | +6.1% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +31.8% |
| prepare | 59914.7 | 69235.0 | 1.1508 | [1.1133, 1.1928] | +3.5% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +52.0% |
| plan | 1036.0 | 1494.5 | 1.4469 | [1.3597, 1.6109] | +8.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +2.6% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 67.6 | 99.2 | 1.4572 | [1.3450, 2.4839] | +39.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.2% |
| get_db_command | 106.0 | 99.5 | 0.9297 | [0.8174, 1.0507] | +12.5% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.0% |
| execute | 22695.0 | 23734.5 | 1.0579 | [0.9773, 1.0822] | +5.0% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +7.4% |
| read_rows | 6642.6 | 7069.7 | 1.0840 | [0.9865, 1.1243] | +6.4% | 6.00/6.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +3.2% |
| map_rows | 1958.9 | 2066.8 | 1.0663 | [0.9342, 1.0929] | +7.4% | 5.00/5.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +0.7% |
| cow_description | 0.0 | 803.9 | — | — | — | 0.00/8.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +4.6% |
| cow_clone | 0.0 | 295.8 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +1.7% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 313.1 | 351.7 | 1.1288 | [1.0615, 1.1954] | +5.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.2% |
| planner_recipe | 57212.1 | 65843.3 | 1.1474 | [1.1083, 1.1865] | +3.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +48.6% |
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
| rcp_capture | 79246.8 | 85801.7 | 1.0737 | [1.0387, 1.1067] | +3.2% | 4.00/4.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +34.0% |
| rcp_fingerprint | 18515.2 | 19798.3 | 1.0593 | [1.0236, 1.0928] | +3.3% | 4.00/4.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | +6.4% |
| rcp_filter_scan | 2050.8 | 2158.3 | 1.0693 | [0.9855, 1.1181] | +6.2% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | +0.8% |
| rcp_lookup | 12941.1 | 13659.1 | 1.0555 | [1.0247, 1.1077] | +3.9% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | +4.1% |
| rcp_matches | 20446.1 | 21557.0 | 1.0496 | [1.0196, 1.1093] | +4.3% | 3.00/3.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +6.0% |
| rcp_transfer | 1349.1 | 4465.4 | 3.2752 | [3.1374, 3.5226] | +5.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +17.6% |
| rcp_copyto | 1103.7 | 3986.7 | 3.5637 | [3.3726, 3.8178] | +6.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | +16.2% |
| rcp_copyto_members | 88.7 | 448.5 | 5.0312 | [4.8893, 5.2041] | +3.1% | 2.00/2.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | +2.0% |
| rcp_transfer_edges | 143.3 | 377.9 | 2.7175 | [2.3585, 3.0564] | +12.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +1.4% |
| rcp_staged | 4322.0 | 5692.0 | 1.3133 | [1.2572, 1.3505] | +3.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +7.6% |
| rcp_qplan_ctor | 62.9 | 108.2 | 1.7011 | [1.5521, 1.8672] | +9.3% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +0.3% |
| rcp_qplan_hash | 1465.7 | 1885.3 | 1.2663 | [1.2582, 1.4110] | +6.0% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +2.2% |
| rcp_equals1 | 2560.3 | 3159.1 | 1.2343 | [1.1926, 1.3163] | +5.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +3.4% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 1209.6 | 1351.3 | 1.0828 | [1.0488, 1.1478] | +4.6% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +0.6% |
| legacy_prepare_exclusive | 58787.3 | 67570.2 | 1.1455 | [1.1085, 1.1845] | +3.3% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +49.3% |
| planner_preamble | 57519.8 | 66184.0 | 1.1474 | [1.1084, 1.1862] | +3.4% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +48.9% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| capture_exclusive | 60769.9 | 66003.4 | 1.0811 | [1.0404, 1.1109] | +3.3% | 4.00/4.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +28.6% |
| lookup_exclusive | -7505.1 | -7981.4 | — | — | — | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | -1.6% |
| copyto_exclusive | 1012.4 | 3531.8 | 3.4405 | [3.2412, 3.6904] | +6.5% | 1.00/1.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | +14.3% |
| qplan_ctor_exclusive | -1404.3 | -1778.0 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -2.0% |
| transfer_exclusive | 235.8 | 470.2 | 2.0414 | [1.8394, 2.2264] | +9.5% | 1.00/1.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +1.4% |
| staged_exclusive | 1746.1 | 2395.3 | 1.3614 | [1.3121, 1.4707] | +5.8% | 1.00/1.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | +3.6% |
| planner_recipe_exclusive | -42696.6 | -45815.6 | — | — | — | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | -15.8% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 131531.3 | 147815.1 | 1.1354 | [1.0622, 1.1540] | +4.0% | 1/1 | -856.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 62745.0 | 72137.8 | 1.1461 | [1.1054, 1.1880] | +3.6% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +52.8% |
| residual_unattributed | 4097.7 | 4416.9 | 1.0688 | [0.9773, 1.0947] | +5.5% | — | — | unattributed residual (all measured phases added back) | +1.6% |

Verdict: e2e delta +17623.8 ns/op; full phase sum +17383.0; unattributed residual +290.5 ns/op (+1.6%); dispatch six-term residual +9312.9 ns/op (+52.8%); combined CI hw 7039.8 ns; reconciles=True; residual insignificant=True; named=construct, prepare_exclusive, plan, cow_shape, planner_preamble, planner_gate, planner_recipe.

## inlist

Probe e2e B/A = 1.1026 [1.0145, 1.1411] (hw 5.7 %); ABBA anchor 1.082/1.075; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 21099.9 | 21951.7 | 1.0643 | [0.9464, 1.0896] | +6.7% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +10.3% |
| prepare | 67476.5 | 76078.4 | 1.1444 | [1.0491, 1.1762] | +5.6% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +74.4% |
| plan | 10215.9 | 12281.0 | 1.2302 | [1.1787, 1.2440] | +2.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +17.7% |
| bind | 15042.5 | 16099.8 | 1.0875 | [1.0006, 1.1319] | +6.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +10.1% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.0% |
| get_db_command | 110.9 | 123.5 | 1.0624 | [0.9469, 1.2985] | +16.5% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | +0.1% |
| execute | 28841.8 | 29316.5 | 1.0501 | [0.9820, 1.1304] | +7.1% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +10.7% |
| read_rows | 5359.7 | 5476.5 | 1.0685 | [0.9752, 1.1032] | +6.0% | 3.00/3.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +2.7% |
| map_rows | 1351.2 | 1467.4 | 1.1174 | [0.9236, 1.2340] | +13.9% | 2.00/2.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +1.2% |
| cow_description | 0.0 | 190.6 | — | — | — | 0.00/1.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +1.5% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 106.2 | 162.9 | 1.5326 | [1.2801, 1.8334] | +18.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.4% |
| planner_recipe | 13320.0 | 14279.6 | 1.0762 | [0.9316, 1.1080] | +8.2% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +7.9% |
| prepare_pipeline | 27830.2 | 32188.1 | 1.1726 | [1.0919, 1.2087] | +5.0% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call | +36.7% |
| prep_preamble | 531.6 | 1003.0 | 1.8869 | [1.8005, 1.9913] | +5.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType | +3.6% |
| nav_expand | 411.6 | 391.6 | 0.9663 | [0.8090, 1.0539] | +12.7% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings | -0.1% |
| get_from | 246.7 | 269.6 | 1.0727 | [1.0085, 1.1405] | +6.2% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd) | +0.1% |
| prepare_from | 142.1 | 154.2 | 1.0398 | [1.0024, 1.1985] | +9.4% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock | +0.0% |
| prepare_join | 101.6 | 104.5 | 1.0292 | [1.0072, 1.0836] | +3.7% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin | +0.0% |
| prepare_columns | 9046.0 | 9910.6 | 1.1157 | [1.0445, 1.1349] | +4.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability | +7.7% |
| prepare_where | 12493.5 | 13872.5 | 1.1243 | [1.0388, 1.1849] | +6.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters | +11.9% |
| prepare_prewhere | 72.9 | 68.5 | 0.9398 | [0.8526, 1.0314] | +9.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere | -0.0% |
| prepare_arrayjoin | 90.5 | 78.1 | 0.8238 | [0.7775, 0.8788] | +6.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin | -0.1% |
| prepare_grouping | 431.7 | 655.6 | 1.5019 | [1.3731, 1.7283] | +11.8% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows | +1.6% |
| prepare_sorting | 86.7 | 61.6 | 0.7051 | [0.6301, 0.8275] | +14.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting | -0.2% |
| prepare_union | 39.9 | 104.1 | 2.6533 | [2.0066, 2.9373] | +17.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips | +0.5% |
| prepare_ctes | 315.0 | 417.0 | 1.3553 | [1.2805, 1.4060] | +4.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes | +0.9% |
| prepare_hints | 59.7 | 94.8 | 1.5981 | [1.1932, 2.3620] | +36.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints | +0.3% |
| unkeyed_scans | 1675.9 | 1997.3 | 1.2060 | [1.1335, 1.3031] | +7.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues | +2.7% |
| from_hash | 1049.9 | 1101.4 | 1.1148 | [0.9983, 1.1646] | +7.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from) | +0.9% |
| rcp_capture | 10823.3 | 11601.2 | 1.0799 | [0.9133, 1.1143] | +9.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +6.7% |
| rcp_fingerprint | 6850.2 | 7333.7 | 1.0771 | [0.9655, 1.1070] | +6.6% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | +4.1% |
| rcp_filter_scan | 1467.2 | 1567.9 | 1.0913 | [0.9466, 1.1359] | +8.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | +1.0% |
| rcp_lookup | 583.6 | 548.3 | 0.9044 | [0.8739, 0.9829] | +6.0% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | -0.4% |
| rcp_matches | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +0.0% |
| rcp_transfer | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +0.0% |
| rcp_copyto | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | +0.0% |
| rcp_copyto_members | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | +0.0% |
| rcp_transfer_edges | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +0.0% |
| rcp_staged | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +0.0% |
| rcp_qplan_ctor | 572.9 | 845.1 | 1.4383 | [1.3651, 1.5623] | +6.9% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +2.0% |
| rcp_qplan_hash | 1632.8 | 2377.3 | 1.4986 | [1.3868, 1.6113] | +7.5% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +6.1% |
| rcp_equals1 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +0.0% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 943.0 | 1077.3 | 1.1559 | [1.0073, 1.2654] | +11.2% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +1.1% |
| legacy_prepare_exclusive | 42195.5 | 47723.6 | 1.1463 | [1.0344, 1.1754] | +6.2% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +47.2% |
| planner_preamble | 13424.1 | 14458.2 | 1.0797 | [0.9344, 1.1136] | +8.3% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +8.4% |
| pipeline_remainder | 1086.1 | 1807.9 | 1.7100 | [1.5656, 1.7823] | +6.3% | 1.00/1.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +5.7% |
| capture_exclusive | 3996.6 | 4270.7 | 1.0820 | [0.8410, 1.1411] | +13.9% | 1.00/1.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +2.5% |
| lookup_exclusive | 583.6 | 548.3 | 0.9044 | [0.8739, 0.9829] | +6.0% | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | -0.4% |
| copyto_exclusive | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | +0.0% |
| qplan_ctor_exclusive | -1058.0 | -1541.1 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -4.0% |
| transfer_exclusive | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +0.0% |
| staged_exclusive | -572.9 | -845.1 | — | — | — | 0.00/0.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | -2.0% |
| planner_recipe_exclusive | 424.1 | 516.1 | 1.1682 | [1.0595, 1.5078] | +19.2% | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | +0.5% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 128033.7 | 138825.7 | 1.1026 | [1.0145, 1.1411] | +5.7% | 1/1 | -32.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 70701.6 | 79311.8 | 1.1394 | [1.0435, 1.1678] | +5.5% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +75.2% |
| residual_unattributed | 4166.3 | 4310.7 | 1.0329 | [0.9459, 1.0809] | +6.5% | — | — | unattributed residual (all measured phases added back) | +1.1% |

Verdict: e2e delta +12922.4 ns/op; full phase sum +12685.0; unattributed residual +141.1 ns/op (+1.1%); dispatch six-term residual +9718.4 ns/op (+75.2%); combined CI hw 8824.2 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, bind, planner_gate, prepare_pipeline, prep_preamble, get_from, prepare_from, prepare_join, prepare_columns, prepare_where, prepare_grouping, prepare_union, prepare_ctes, prepare_hints, unkeyed_scans.

## leftjoin

Probe e2e B/A = 1.1386 [1.0193, 1.1600] (hw 6.2 %); ABBA anchor 1.104/1.048; direction match: yes.

| phase | A ns/op | B ns/op | paired ratio | 95% CI | rel CI half-width | calls/op A/B | alloc delta B/op | resolved file:line | share of e2e delta |
|---|---|---|---|---|---|---|---|---|---|
| construct | 23736.6 | 25696.1 | 1.0957 | [0.9595, 1.1140] | +7.0% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap) | +18.4% |
| prepare | 31322.1 | 39667.7 | 1.2646 | [1.1479, 1.3026] | +6.1% | 1.00/1.00 | — | QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand) | +67.5% |
| plan | 1025.6 | 1590.7 | 1.4857 | [1.3565, 1.6457] | +9.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet) | +4.1% |
| bind | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult> | +0.0% |
| enum_create | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult> | +0.0% |
| cow_shape | 68.6 | 88.8 | 1.2707 | [1.1998, 1.4290] | +9.0% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape | +0.2% |
| get_db_command | 101.1 | 80.6 | 0.7947 | [0.6502, 0.9022] | +15.9% | 1.00/1.00 | — | DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore | -0.2% |
| execute | 18197.3 | 18969.8 | 1.0655 | [0.9456, 1.0827] | +6.4% | 1.00/1.00 | — | DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync | +9.7% |
| read_rows | 5835.1 | 5782.6 | 1.0092 | [0.9199, 1.0578] | +6.8% | 11.00/11.00 | — | DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList | +0.4% |
| map_rows | 5789.1 | 5810.3 | 1.0229 | [0.9085, 1.0778] | +8.3% | 10.00/10.00 | — | DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList | +1.0% |
| cow_description | 0.0 | 184.5 | — | — | — | 0.00/2.00 | — | Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only) | +1.5% |
| cow_clone | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only) | +0.0% |
| prepare_call | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged) | +0.0% |
| planner_gate | 85.6 | 116.0 | 1.3359 | [0.9717, 1.7857] | +30.5% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span) | +0.2% |
| planner_recipe | 29178.1 | 36753.8 | 1.2632 | [1.1431, 1.2933] | +5.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key) | +62.2% |
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
| rcp_capture | 10945.0 | 12224.9 | 1.1238 | [1.0233, 1.1639] | +6.3% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:3948 CaptureRawShape(QueryCommand, int depth) inclusive (every recursion level; contains rcp_fingerprint) | +11.0% |
| rcp_fingerprint | 5568.2 | 6152.6 | 1.1087 | [1.0312, 1.1397] | +4.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4088 inside CaptureRawShape: the Fingerprint(shape, cmd) call (nested in rcp_capture) | +5.1% |
| rcp_filter_scan | 2868.4 | 3019.7 | 1.0516 | [0.9063, 1.0898] | +8.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:637 GetPreparedQueryCommand: HasApplicableFiltersInRawGraph call site | +1.2% |
| rcp_lookup | 9110.2 | 9869.0 | 1.0876 | [0.9927, 1.1056] | +5.2% | 1.00/1.00 | — | DataContext/Cache/QueryPlanStore.cs:120 TryGetRaw inclusive (contains rcp_matches) | +6.7% |
| rcp_matches | 7659.5 | 8302.9 | 1.0860 | [0.9881, 1.1085] | +5.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:3751 RawShape.Matches inclusive (every recursion level; nested in rcp_lookup) | +5.4% |
| rcp_transfer | 1702.7 | 5463.2 | 3.2975 | [3.0771, 3.4809] | +6.1% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4430 ApplyPreparationFrom inclusive (contains rcp_copyto and rcp_transfer_edges) | +30.6% |
| rcp_copyto | 1487.3 | 5013.8 | 3.5055 | [3.2225, 3.6785] | +6.5% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4438 ApplyPreparationFrom: the clone.CopyTo(this, copyAll: true) call (nested in rcp_transfer) | +28.9% |
| rcp_copyto_members | 43.2 | 188.0 | 4.4498 | [3.6692, 5.5078] | +20.7% | 1.00/1.00 | — | Query/QueryCommand.Clone.cs:136 QueryCommand.CopyTo: the if (copyAll) member block (nested in rcp_copyto) | +1.2% |
| rcp_transfer_edges | 131.6 | 357.4 | 2.7974 | [2.5089, 3.0655] | +9.9% | 1.00/1.00 | — | Query/QueryCommand.QueryPreparer.cs:4444 ApplyPreparationFrom: the four writes after CopyTo (nested in rcp_transfer) | +1.9% |
| rcp_staged | 4169.4 | 5388.6 | 1.3137 | [1.1385, 1.3928] | +9.7% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:655 recipe probe: staged plan-key block GetOrCreatePlanKey -> outcome (contains the qplan/equals/discard leaves) | +10.5% |
| rcp_qplan_ctor | 61.9 | 104.9 | 1.6034 | [1.5654, 1.9214] | +11.1% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:43 QueryPlan(QueryCommand, string?) assignments before ComputeHash (nested in rcp_staged) | +0.3% |
| rcp_qplan_hash | 1450.7 | 1811.0 | 1.2687 | [1.0992, 1.4576] | +14.1% | 1.00/1.00 | — | DataContext/Cache/QueryPlan.cs:50 QueryPlan ctor: the ComputeHash call (nested in rcp_staged) | +3.0% |
| rcp_equals1 | 2434.3 | 3076.7 | 1.2776 | [1.1037, 1.3502] | +9.6% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:657 staged block: recipePlan.Equals(stagedKey) (nested in rcp_staged) | +5.5% |
| rcp_equals2 | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | DataContext/QueryPlanner.cs:663 staged block: stagedKey.Equals(recipePlan), only when the first is false (nested in rcp_staged) | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | Query/QueryCommand.QueryPreparer.cs:4467 DiscardStagedPreparation (nested in rcp_staged) | +0.0% |
| prepare_exclusive | 967.7 | 1087.7 | 1.1057 | [1.0008, 1.2209] | +10.0% | 1.00/1.00 | — | prepare span (QueryCommand.TResult.cs:303-342) minus plan/bind/enum_create/cow_shape/prepare_pipeline/planner_preamble (planner_gate+planner_recipe) | +0.8% |
| legacy_prepare_exclusive | 30223.6 | 37962.6 | 1.2577 | [1.1398, 1.2909] | +6.0% | 1.00/1.00 | — | legacy P02 derivation: prepare span minus plan/bind/enum_create/cow_shape only | +63.3% |
| planner_preamble | 29263.4 | 36859.2 | 1.2634 | [1.1439, 1.2932] | +5.9% | 1.00/1.00 | — | DataContext/QueryPlanner.cs:594-662 GetPreparedQueryCommand preamble = planner_gate + planner_recipe | +62.5% |
| pipeline_remainder | 0.0 | 0.0 | — | — | — | 0.00/0.00 | — | prepare_pipeline minus the instrumented Prepare sub-step sum (unlisted steps and tail hashes) | +0.0% |
| capture_exclusive | 5376.8 | 6088.2 | 1.1303 | [1.0154, 1.1991] | +8.1% | 1.00/1.00 | — | rcp_capture − rcp_fingerprint (nested Fingerprint call inside CaptureRawShape) | +5.7% |
| lookup_exclusive | 1517.7 | 1585.3 | 1.0522 | [1.0163, 1.1401] | +5.9% | 1.00/1.00 | — | rcp_lookup − rcp_matches (nested RawShape.Matches inside QueryPlanStore.TryGetRaw) | +0.6% |
| copyto_exclusive | 1448.6 | 4836.3 | 3.4796 | [3.1865, 3.6541] | +6.7% | 1.00/1.00 | — | rcp_copyto − rcp_copyto_members (nested copyAll member block inside CopyTo) | +27.8% |
| qplan_ctor_exclusive | -1393.5 | -1708.4 | — | — | — | 1.00/1.00 | — | rcp_qplan_ctor − rcp_qplan_hash (nested ComputeHash inside the QueryPlan ctor) | -2.8% |
| transfer_exclusive | 226.0 | 452.8 | 2.0813 | [1.8932, 2.2360] | +8.2% | 1.00/1.00 | — | rcp_transfer − rcp_copyto (ApplyPreparationFrom without the CopyTo call; includes the edges) | +1.9% |
| staged_exclusive | 1728.9 | 2248.4 | 1.3172 | [1.1510, 1.5285] | +14.3% | 1.00/1.00 | — | rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard) | +4.2% |
| planner_recipe_exclusive | 499.6 | 636.1 | 1.3047 | [1.1366, 1.3702] | +9.0% | 1.00/1.00 | — | planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged); the un-timed eligibility gate plus CaptureRawGraph of the root | +1.2% |
| recipe_residual | 0.0 | 0.0 | — | — | — | 1.00/1.00 | — | planner_recipe − (planner_recipe_exclusive + all nested top-level terms added back); 0 by construction, reported separately | +0.0% |
| end_to_end | 89612.9 | 100553.7 | 1.1386 | [1.0193, 1.1600] | +6.2% | 1/1 | -64.0 | end-to-end terminal call (ToList / ToListAsyncCore) | +100.0% |
| phase_residual | 34993.5 | 43356.8 | 1.2452 | [1.1246, 1.2743] | +6.0% | — | — | dispatch six-term residual (contains nested plan/bind/enum_create/cow_shape) | +69.7% |
| residual_unattributed | 4669.4 | 4847.2 | 1.0650 | [0.9422, 1.0911] | +7.0% | — | — | unattributed residual (all measured phases added back) | +2.5% |

Verdict: e2e delta +12195.4 ns/op; full phase sum +11828.6; unattributed residual +300.9 ns/op (+2.5%); dispatch six-term residual +8500.4 ns/op (+69.7%); combined CI hw 7063.9 ns; reconciles=True; residual insignificant=True; named=prepare_exclusive, plan, cow_shape, planner_preamble, planner_recipe.

