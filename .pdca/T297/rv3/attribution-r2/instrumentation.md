# E22 instrumentation — T297 r=4 (P02 + P02b prepare decomposition)

Identical instrumentation in both throwaway trees: `src/nextorm.core/Probe/T297PhaseProbe.cs`
plus boundary calls. Trees: A = `0d4d5df2` (baseline), B = `9d633521` (candidate).
Nothing here is shipped; the product tree is untouched.

## Phase boundary table (P02 phases)

| phase | A path:line + enclosing method | B path:line + enclosing method |
|---|---|---|
| construct | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap)` | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap)` |
| prepare | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand)` | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand)` |
| plan | `DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet)` | `DataContext/QueryPlanner.cs:707-720 MakeSelect (plan-key + QueryPlanStore.TryGet)` |
| bind | `DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult>` | `DataContext/QueryPlanner.cs:883-890 MakeSelect -> RefreshParameters<TResult>` |
| enum_create | `DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult>` | `DataContext/QueryPlanner.cs:900-907 MakeSelect -> CreateResultSetEnumerator<TResult>` |
| cow_shape | `Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape` | `Query/QueryCommand.QueryPreparer.cs:231-321 RefreshInValuesShape` |
| get_db_command | `DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore` | `DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore` |
| execute | `DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync` | `DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync` |
| read_rows | `DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList` | `DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList` |
| map_rows | `DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList` | `DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList` |
| cow_description | — (path absent in A) | `Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only)` |
| cow_clone | — (path absent in A) | `Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only)` |

All paths are relative to `src/nextorm.core/` in the probe copies. Shared boundaries have identical
line numbers in both copies; B additionally carries the COW description/clone counters.

## P02b second-level decomposition (new boundaries)

Two timers in the `GetPreparedQueryCommand` preamble and one around the `PrepareCommand` call;
16 leaf, mutually exclusive timers around the top-level steps of `QueryPreparer.Prepare`, in execution
order. `planner_preamble` in the tables is the derived sum `planner_gate + planner_recipe`; the
`Prepare` sub-steps are nested inside `prepare_pipeline`.

| phase | A path:line + enclosing method | B path:line + enclosing method |
|---|---|---|
| prepare_call | `Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged)` | `Query/QueryCommand.QueryPreparer.cs:32 QueryPreparer.Prepare entry counter (no time charged)` |
| planner_gate | `DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span)` | `DataContext/QueryPlanner.cs:594-602 GetPreparedQueryCommand: HasDataModifyingCte gate (:600-601 inside the span)` |
| planner_recipe | `DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key)` | `DataContext/QueryPlanner.cs:614-662 GetPreparedQueryCommand: D201-3 raw-recipe probe (CaptureRawShape/HasApplicableFiltersInRawGraph/TryGetRaw/ApplyPreparationFrom/staged plan key)` |
| prepare_pipeline | `DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call` | `DataContext/QueryPlanner.cs:666-668 GetPreparedQueryCommand: the PrepareCommand call` |
| prep_preamble | `Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType` | `Query/QueryCommand.QueryPreparer.cs:33-88 Prepare: InvalidatePlanKey + resolved state + srcType` |
| nav_expand | `Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings` | `Query/QueryCommand.QueryPreparer.cs:93-99 Prepare: NavigationExpansion.Expand + ApplyProviderOuterJoinNullSettings` |
| get_from | `Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd)` | `Query/QueryCommand.QueryPreparer.cs:101-103 Prepare: cmd._from ?? GetFrom(srcType, cmd)` |
| prepare_from | `Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock` | `Query/QueryCommand.QueryPreparer.cs:104-107 Prepare: PrepareFrom + AttachPrimarySourceLock` |
| prepare_join | `Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin` | `Query/QueryCommand.QueryPreparer.cs:108-110 Prepare: PrepareJoin` |
| prepare_columns | `Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability` | `Query/QueryCommand.QueryPreparer.cs:111-116 Prepare: PrepareColumns + ValidateProjectionNullability` |
| prepare_where | `Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters` | `Query/QueryCommand.QueryPreparer.cs:117-119 Prepare: PrepareWhere + InjectMainSourceFilters` |
| prepare_prewhere | `Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere` | `Query/QueryCommand.QueryPreparer.cs:120-122 Prepare: PreparePreWhere` |
| prepare_arrayjoin | `Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin` | `Query/QueryCommand.QueryPreparer.cs:123-125 Prepare: PrepareArrayJoin` |
| prepare_grouping | `Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows` | `Query/QueryCommand.QueryPreparer.cs:127-134 Prepare: PrepareGrouping + LimitBy + DistinctOn + ExtremeRow + PerParentPartition + Windows` |
| prepare_sorting | `Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting` | `Query/QueryCommand.QueryPreparer.cs:135-137 Prepare: PrepareSorting` |
| prepare_union | `Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips` | `Query/QueryCommand.QueryPreparer.cs:139-143 Prepare: union PrepareCommand + TransferNestedFilterSkips` |
| prepare_ctes | `Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes` | `Query/QueryCommand.QueryPreparer.cs:144-146 Prepare: PrepareCtes` |
| prepare_hints | `Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints` | `Query/QueryCommand.QueryPreparer.cs:147-149 Prepare: PrepareHints` |
| unkeyed_scans | `Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues` | `Query/QueryCommand.QueryPreparer.cs:155-163 Prepare: ScanUnkeyedTupleInValues + ScanUnkeyedScalarInValues` |
| from_hash | `Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from)` | `Query/QueryCommand.QueryPreparer.cs:182-184 Prepare: FromPlanHash = GetFromExpressionPlanEqualityComparer().GetHashCode(from)` |

A and B share every P02b boundary line: `DataContext/QueryPlanner.cs:594-668` is byte-identical
in both copies, and `QueryCommand.QueryPreparer.cs:30-184` is identical; B's only `PrepareJoin`
difference is inside the called method at `:2101+` (`_definitionMemo = null`), after the boundary.
`prepare_call` records the `QueryPreparer.Prepare` entry counter only (no time charged).

## Derivation formulas

- `legacy_prepare_exclusive = prepare − (plan + bind + enum_create + cow_shape)` (P02, kept).
- `prepare_exclusive = prepare − (plan + bind + enum_create + cow_shape + prepare_pipeline + planner_gate + planner_recipe)`.
- `planner_preamble = planner_gate + planner_recipe`.
- `pipeline_remainder = prepare_pipeline − Σ(prep_preamble … from_hash)`.
- The full reconciliation sum adds back `plan/bind/enum_create/cow_shape/prepare_pipeline/planner_gate/planner_recipe`;
  the `Prepare` sub-steps are never added back (nested in `prepare_pipeline`).

## Roslyn resolutions

| phase | resolved symbol |
|---|---|
| construct | NextORM.Core.QueryCommand<TResult>.ToList / ToListAsyncCore (src/nextorm.core/Query/QueryCommand.TResult.cs:14) |
| prepare | NextORM.Core.QueryCommand<TResult>.ToList / ToListAsyncCore (src/nextorm.core/Query/QueryCommand.TResult.cs:14) |
| plan | NextORM.Core.QueryPlanner.MakeSelect (src/nextorm.core/DataContext/QueryPlanner.cs:549) |
| bind | NextORM.Core.QueryPlanner.RefreshParameters<TResult> (src/nextorm.core/DataContext/QueryPlanner.cs:919) |
| enum_create | NextORM.Core.QueryPlanner.CreateResultSetEnumerator<TResult> (src/nextorm.core/DataContext/QueryPlanner.cs:1030) |
| cow_shape | NextORM.Core.QueryCommand.RefreshInValuesShape (src/nextorm.core/Query/QueryCommand.Plan.cs:10) |
| get_db_command | NextORM.Core.DbPreparedQueryCommand<TResult>.GetDbCommandCore (src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:197) |
| execute | NextORM.Core.QueryExecutor.RunReader (:179) / RunReaderAsync (:209) |
| read_rows | NextORM.Core.QueryExecutor.ToListAsync<TResult> (:738) / ToList<TResult> (:790) |
| map_rows | NextORM.Core.QueryExecutor.ToListAsync<TResult> (:738) / ToList<TResult> (:790) |
| cow_description | NextORM.Core.QueryDescription.MarkShared (:261) / CloneShellForWrite (:282) and NextORM.Core.QueryCommand.WriteDescription (:312) [B] |
| cow_clone | NextORM.Core.QueryCommand.QueryCommand(IDataContext?, QueryCommand, bool) (src/nextorm.core/Query/QueryCommand.cs:174) [B] |
| prepare_call | NextORM.Core.QueryCommand.QueryPreparer.Prepare (QueryCommand.QueryPreparer.cs:30) |
| planner_gate | NextORM.Core.QueryCommand.HasDataModifyingCte (QueryCommand.cs:770) |
| planner_recipe | NextORM.Core.QueryCommand.CaptureRawShape (:3938) / QueryPlanStore.TryGetRaw / NextORM.Core.QueryCommand.ApplyPreparationFrom (:4412) |
| prepare_pipeline | NextORM.Core.QueryCommand.PrepareCommand (QueryCommand.Prepare.cs:17) -> QueryPreparer.Prepare (:30) |
| prep_preamble | NextORM.Core.QueryCommand.InvalidatePlanKey + QueryPreparer.Prepare preamble (:33-88) |
| nav_expand | NextORM.Core.NavigationExpansion.Expand / ApplyProviderOuterJoinNullSettings |
| get_from | NextORM.Core.IDataContext.GetFrom / QueryCommand._from |
| prepare_from | NextORM.Core.QueryCommand.QueryPreparer.PrepareFrom (:325) + AttachPrimarySourceLock (:358) |
| prepare_join | NextORM.Core.QueryCommand.QueryPreparer.PrepareJoin (:2099) |
| prepare_columns | NextORM.Core.QueryCommand.QueryPreparer.PrepareColumns (:555) + NavigationExpansion.ValidateProjectionNullability |
| prepare_where | NextORM.Core.QueryCommand.QueryPreparer.PrepareWhere (:3367) + InjectMainSourceFilters (:2159) |
| prepare_prewhere | NextORM.Core.QueryCommand.QueryPreparer.PreparePreWhere (:3402) |
| prepare_arrayjoin | NextORM.Core.QueryCommand.QueryPreparer.PrepareArrayJoin (:3430) |
| prepare_grouping | NextORM.Core.QueryCommand.QueryPreparer.PrepareGrouping (:3601) / PrepareLimitBy (:3444) / PrepareDistinctOn (:3462) / PrepareExtremeRow (:3477) / PreparePerParentPartition (:3499) / PrepareWindows (:3514) |
| prepare_sorting | NextORM.Core.QueryCommand.QueryPreparer.PrepareSorting (:2772) |
| prepare_union | NextORM.Core.QueryCommand._union.PrepareCommand + QueryPreparer.TransferNestedFilterSkips (:414) |
| prepare_ctes | NextORM.Core.QueryCommand.QueryPreparer.PrepareCtes (:423) |
| prepare_hints | NextORM.Core.QueryCommand.QueryPreparer.PrepareHints (:518) |
| unkeyed_scans | NextORM.Core.QueryCommand.QueryPreparer.ScanUnkeyedTupleInValues (:2887) / ScanUnkeyedScalarInValues (:3132) |
| from_hash | NextORM.Core.QueryCommand.GetFromExpressionPlanEqualityComparer (QueryCommand.Plan.cs:81).GetHashCode(from) |

Resolution performed with the `roslyn` tool against `/tmp/opencode/t297-p02/A/nextorm.slnx`
and `/tmp/opencode/t297-p02/B/nextorm.slnx` (`members`: QueryPlanner, QueryExecutor,
DbPreparedQueryCommand<TResult>, QueryCommand, QueryDescription, QueryPreparer). Full bodies are in
`probe-src/` and `probe-src/instrumentation-{A,B}.diff`.

## COW counters (B only) and why

B replaces the shared mutable command state with a copy-on-write `QueryDescription`. A has no such
path, so `cow_description` and `cow_clone` are B-only counters: they split B's prepare span into
the COW-description work (`QueryDescription.MarkShared`, `CloneShellForWrite`, `QueryCommand.WriteDescription`)
and the clone-constructor adoption. They are nested inside `prepare` (and `cow_clone` inside the
clone constructor), so they are diagnostic and are **not** added to `e2e_phases`; the attribution
formula leaves them inside `prepare_exclusive`, where the feature's cost actually lives.

