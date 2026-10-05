# E01 — resolved symbols and sync/async entry paths (issue #166)

C0 evidence. All resolution via the `roslyn` tool (symbols/callers/refs); no text search.

## Symbols

| Symbol | Kind | Location |
| --- | --- | --- |
| `NextORM.Core.QueryCommand.HasDataModifyingCte` | internal property | `src/nextorm.core/Query/QueryCommand.cs:504:19` (getter body `:506-546`) |
| `NextORM.Core.QueryCommand.IsPrepared` | public property | `src/nextorm.core/Query/QueryCommand.cs:306:17` (getter `:309`, backed by `_isPrepared`) |
| `NextORM.Core.QueryCommand.PrepareCommand(System.Threading.CancellationToken)` | public method | `src/nextorm.core/Query/QueryCommand.Prepare.cs:10:17` |
| `NextORM.Core.QueryCommand.PrepareCommand(bool, System.Threading.CancellationToken)` | public virtual method | `src/nextorm.core/Query/QueryCommand.Prepare.cs:17:21` |
| `NextORM.Core.QueryCommand.QueryPreparer.PrepareCtes(QueryCommand, bool, CancellationToken)` | private static method | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:267:29` |
| `NextORM.Core.QueryCommand.HasDataModifyingCteIn(QueryCommand, HashSet<QueryCommand>)` | private static helper | `src/nextorm.core/Query/QueryCommand.cs:600` |

## `HasDataModifyingCte` production caller — exactly one

`roslyn callers NextORM.Core.QueryCommand.HasDataModifyingCte` returns a single production call site:

- `NextORM.Core.QueryPlanner.GetPreparedQueryCommand<TResult>(QueryCommand<TResult>, bool, bool, bool, bool, CancellationToken)` at `src/nextorm.core/DataContext/QueryPlanner.cs:559:26`
  (`if (queryCommand.HasDataModifyingCte) storeInCache = false;`).

All other callers are tests. There is no second production read of this property.

## `PrepareCtes` — where `_ctes` is hoisted and `_isPrepared` set

- `QueryCommand.QueryPreparer.Prepare` calls `PrepareCtes(cmd, dontCalculateHash, cancellationToken)` at `QueryCommand.QueryPreparer.cs:92`.
- `PrepareCtes` (`:267`) calls `CteHoister.Hoist(cmd._ctes)` at `:276` when the root list is non-empty, flattening the transitive declaration tree into the root's `_ctes`; nested bodies' own `_ctes` are left in place (definitions are shared, not mutated).
- `cmd._isPrepared = true` is set at `QueryCommand.QueryPreparer.cs:95`, after `PrepareCtes`.

Consequence: on a **prepared** command the root `_ctes` is the complete flat, hoisted list; the getter's nested probe (`ctes[i].Query._ctes is { Count: > 0 }`, `QueryCommand.cs:525`) can still be `true` because a nested body keeps its own declarations, which is exactly the unneeded recursive `HashSet` allocation this issue removes.

## Sync and async entry paths — shared, no separate async check

Both terminal families reach the same planner gate (and therefore the same `HasDataModifyingCte` read at `QueryPlanner.cs:559`):

- `DataContext.GetPreparedQueryCommand<TResult>(QueryCommand<TResult>, bool, bool, CancellationToken)` — `src/nextorm.core/DataContext/DataContext.cs:287`.
- private overload `:298` forwards to `_planner.GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache && QueryCacheEnabled, false, streamingRows, cancellationToken)` at `:309`.
- `QueryPlanner.GetPreparedQueryCommand` overloads `:546`/`:549`/`:552` converge on `:552`, where line `:559` is the only `HasDataModifyingCte` read.

Sync terminals (all in `src/nextorm.core/Query/QueryCommand.TResult.cs`, each calls `DataContext.GetPreparedQueryCommand`):
`ToEnumerable` `:213`, `ToList` `:219`, `Any` `:372`, `ExecuteScalar` `:405`, `First` `:435`, `FirstOrDefault` `:472`, `Single` `:508`, `SingleOrDefault` `:543`.

Async terminals (same file):
`CreateAsyncEnumerator` `:66`, `CreateEnumeratorAsync` `:85`, `Pipeline` `:105`, `ToAsyncEnumerableCore` `:132`, `ToListAsync` `:227`/`:233`, `AnyAsync` `:397`, `ExecuteScalarAsync` `:414`, `FirstAsync` `:455`, `FirstOrDefaultAsync` `:492`, `SingleAsync` `:527`, `SingleOrDefaultAsync` `:562`.

There is **no** async-only `HasDataModifyingCte` check; async does reach the same `QueryPlanner.cs:559` check. No separate path exists.

## Fix axis (F1, for reference)

In the getter, after the mutation-first flat scan finds no mutation, enter the recursive `HashSet` path only when `nested && !IsPrepared`. A prepared command already has the complete hoisted flat `_ctes`, so the recursive probe is unnecessary. Cold/unprepared commands keep the recursive, cycle-safe path. No state is cleared or mutated.
