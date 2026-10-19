# E22 instrumentation — T297 r=4 (P02)

Identical instrumentation in both throwaway trees: `src/nextorm.core/Probe/T297PhaseProbe.cs`
plus boundary calls. Trees: A = `0d4d5df2` (baseline), B = `9d633521` (candidate).
Nothing here is shipped; the product tree is untouched.

## Phase boundary table

| phase | A path:line + enclosing method | B path:line + enclosing method |
|---|---|---|
| construct | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap)` | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (T297PhaseProbe.OnEntry/OnEnd gap)` |
| prepare | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand)` | `QueryCommand.TResult.cs:303-313 ToList + :332-342 ToListAsyncCore (OnEntry -> OnPrepare around GetPreparedQueryCommand)` |
| plan | `DataContext/QueryPlanner.cs:698-711 MakeSelect (plan-key + QueryPlanStore.TryGet)` | `DataContext/QueryPlanner.cs:698-711 MakeSelect (plan-key + QueryPlanStore.TryGet)` |
| bind | `DataContext/QueryPlanner.cs:874-881 MakeSelect -> RefreshParameters<TResult>` | `DataContext/QueryPlanner.cs:874-881 MakeSelect -> RefreshParameters<TResult>` |
| enum_create | `DataContext/QueryPlanner.cs:891-898 MakeSelect -> CreateResultSetEnumerator<TResult>` | `DataContext/QueryPlanner.cs:891-898 MakeSelect -> CreateResultSetEnumerator<TResult>` |
| cow_shape | `Query/QueryCommand.QueryPreparer.cs:198-288 RefreshInValuesShape` | `Query/QueryCommand.QueryPreparer.cs:198-288 RefreshInValuesShape` |
| get_db_command | `DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore` | `DataContext/Cache/DbPreparedQueryCommand.cs:199-294 GetDbCommandCore` |
| execute | `DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync` | `DataContext/QueryExecutor.cs:181-189 RunReader + :211-221 RunReaderAsync` |
| read_rows | `DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList` | `DataContext/QueryExecutor.cs:757-765 ToListAsync + :809-817 ToList` |
| map_rows | `DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList` | `DataContext/QueryExecutor.cs:771-779 ToListAsync + :823-831 ToList` |
| cow_description | — (path absent in A) | `Query/QueryCommand.Description.cs:263-274 + :287-296 QueryDescription.MarkShared / CloneShellForWrite, :316-321 QueryCommand.WriteDescription (B only)` |
| cow_clone | — (path absent in A) | `Query/QueryCommand.cs:195-208 QueryCommand clone constructor (B only)` |

All paths are relative to `src/nextorm.core/` in the probe copies. Shared boundaries have identical
line numbers in both copies; B additionally carries the COW description/clone counters.

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

Resolution performed with the `roslyn` tool against `/tmp/opencode/t297-p02/A/nextorm.slnx`
and `/tmp/opencode/t297-p02/B/nextorm.slnx` (`members`: QueryPlanner, QueryExecutor,
DbPreparedQueryCommand<TResult>, QueryCommand, QueryDescription). Full bodies are in
`probe-src/` and `probe-src/instrumentation-{A,B}.diff`.

## COW counters (B only) and why

B replaces the shared mutable command state with a copy-on-write `QueryDescription`. A has no such
path, so `cow_description` and `cow_clone` are B-only counters: they split B's prepare span into
the COW-description work (`QueryDescription.MarkShared`, `CloneShellForWrite`, `QueryCommand.WriteDescription`)
and the clone-constructor adoption. They are nested inside `prepare` (and `cow_clone` inside the
clone constructor), so they are diagnostic and are **not** added to `e2e_phases`; the attribution
formula leaves them inside `prepare_exclusive`, where the feature's cost actually lives.

