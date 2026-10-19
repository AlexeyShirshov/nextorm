# T297 r=5 — D:P03a representation review (flat COW root)

Task: flatten the copy-on-write description storage. Representation only, one axis, no behavior
optimization beyond the mechanical redirection.

## 1. What moved

All slots of the four former block classes were moved into the single `QueryDescription` root
(`src/nextorm.core/Query/QueryCommand.Description.cs`); the four block classes, their static `Empty`
singletons and their `Shared` flags were deleted.

| Former block | Former block members | New root slot(s) |
|---|---|---|
| `QueryPredicates` | `FilterScope` | `QueryDescription.FilterScope` (default `QueryFilterScope.None`) |
| `QueryRelations` | `Joins`, `Ctes`, `ReferencedQueries`, `OuterRefs`, `OuterRegistry`, `Union`, `UnionType` | same names on the root |
| `QueryClauses` | `LimitBy`, `DistinctOn`, `ExtremeRow`, `TableSample`, `Temporal`, `RowLock`, `SourceLockClause`, `SampleRatio`, `SampleOffset`, `Settings`, `Windows`, `ArrayJoinKind`, `GroupingType`, `GroupingSets`, `ForJsonClause`, `ForXmlClause`, `NavigationPaths` | same names on the root |
| `QueryOptions` | `Logger`, `Tag`, `Hints`, `TableHints`, `IndexHints`, `IndexHintKind`, `TablesInScopeHints`, `QuoteIdentifiers`, `NamingConvention`, `KeywordCase`, `CommandTimeout`, `EagerLoadMode`, `EagerLoadShape`, `HasPerParentEagerTopN`, `PerParentPaging`, `PerParentPartition`, `PerParentPagingEmbedded`, `IgnoreColumns`, `JunctionChildFilter`, `NavigationChain` | same names on the root |

`WritePredicates()`/`WriteClauses()`/`WriteOptions()` were removed; their call sites now use
`WriteDescription()`. `WriteRelations()` was reduced to `WriteDescription()` plus clearing the one
root-level collection-sharing bit (see §2). `MarkShared()` now marks only the root; `CloneShellForWrite()`
is still `MarkShared()` + `MemberwiseClone()` + `Shared = false`; `WriteDescription()` is still the single
write barrier and clones a shared root once. `_description` remains lazy and nullable; no read allocates it.

Copy constructor (`QueryCommand.cs:203-210`): both branches keep their exact conditions (fast root-sharing
branch at `:192-201`; slow `CloneShellForWrite` branch), and the former
`description.Relations = new QueryRelations(...) { Joins = ... }` write is now the root write
`description.Joins = forCache ? CloneForCache(description.Joins) : CloneJoinArray(description.Joins);`
— collection ownership (`CloneForCache` deep clone for the cache key, `CloneJoinArray` for a live clone)
is unchanged.

## 2. Root state added: `SharedRelations` (evidence-based)

Exactly one root-level bit was added beyond `Shared`: `QueryDescription.SharedRelations`.

Reason: the former relation block's `Shared` flag was read by three in-place list mutators —
`ReplaceCommand` (`QueryCommand.cs:1392`), `IQueryRegistry.AddCommand` (`:1421`) and
`AddOuterReference` (`:1472`). They append to `ReferencedQueries` / `OuterRefs`, which are `List<T>`
objects copied by reference by `MemberwiseClone`. After a shell clone the root's own `Shared` is
`false` while those lists are still shared with the sibling, so checking only `Shared` would append to
a sibling's list. The three sites now test `_description!.SharedRelations`; `WriteRelations()` clears
the bit after a relation write, mirroring the old relation-block clone lifecycle. No other group holds
an in-place-mutated collection (all clause/option collections are replaced by their setters).

No materialization-presence bits were added: nothing reads whether a group was materialized. Every
reader is an accessor that coalesces `_description?.Slot` to the old block default
(`QueryFilterScope.None`, `0d`, `default`, `false`, `null`), `GetFlag`, or the definition memo; the four
former `Empty` singletons had no reader outside `QueryCommand.Description.cs` (Roslyn references below).
Adding presence bits would be dead state.

## 3. Accessor redirection (exact old → new forms)

Mechanical, same null-coalescing/sentinel semantics:

| Old form | New form |
|---|---|
| `_description?.Options.Tag` | `_description?.Tag` |
| `_description?.Options.EagerLoadMode` (etc.) | `_description?.EagerLoadMode` |
| `_description?.Clauses.LimitBy` (etc.) | `_description?.LimitBy` |
| `_description?.Relations.Joins` | `_description?.Joins` |
| `_description?.Predicates.FilterScope ?? QueryFilterScope.None` | `_description?.FilterScope ?? QueryFilterScope.None` |
| `_description?.Relations.UnionType ?? default` | `_description?.UnionType ?? default` |
| `_description?.Clauses.SampleOffset ?? 0d` | `_description?.SampleOffset ?? 0d` |
| `_description?.Clauses.ArrayJoinKind ?? default` | `_description?.ArrayJoinKind ?? default` |
| `_description?.Options.IgnoreColumns ?? false` (etc.) | `_description?.IgnoreColumns ?? false` |
| `_description?.Relations.OuterRegistry` | `_description?.OuterRegistry` |
| `WriteOptions().X = v` / `WriteClauses().X = v` / `WritePredicates().X = v` | `WriteDescription().X = v` |
| `WriteRelations().X = v` | `WriteRelations().X = v` (reduced writer, clears `SharedRelations`) |
| `GetFlag(...)` / `SetFlag(...)` | unchanged |
| `_description!.Relations.Shared` | `_description!.SharedRelations` |

Every setter keeps its no-op/elision guard (`SetFlag` early return when the flag already equals; each
accessor returns before writing when the incoming reference/value is already current). No setter
materializes `_description` on read.

Files touched mechanically:
`QueryCommand.Description.cs`, `QueryCommand.cs`, `QueryCommand.EagerLoading.cs`.
`QueryCommand.Storage.cs`, `QueryCommand.Binding.cs`, `QueryCommand.Plan.cs` needed no change: they
never reference `_description` or a block member (Roslyn `refs` on `QueryCommand._description` and on
each `QueryDescription` group field only reported the three files above).

## 4. Roslyn reference counts

Before (block types, including their definition sites):

| Symbol | References |
|---|---|
| `NextORM.Core.QueryPredicates` | 7 |
| `NextORM.Core.QueryRelations` | 9 |
| `NextORM.Core.QueryClauses` | 7 |
| `NextORM.Core.QueryOptions` | 7 |
| **total** | **30** |

After: `tools.roslyn({action:"refs"})` for each of the four symbols returns
`(symbol not found: ...)` — 0 remaining references. `NextORM.Core.QueryDescription` is the only
description type (28 references: 1 definition + 3 constructor sites + 24 `QueryDescription.Flag*`
qualifiers in `QueryCommand.cs`).

## 5. Frozen files

`git diff --name-only -- src/nextorm.core/DataContext/QueryPlanner.cs
src/nextorm.core/DataContext/Cache/QueryPlanStore.cs
src/nextorm.core/Query/QueryPlanEqualityComparer.cs
src/nextorm.core/Query/QueryCommand.Clone.cs` → empty output (none changed).

The whole solution builds with those files unchanged against the new accessors, so `CopyTo`,
`QueryPlanEqualityComparer`, `QueryPlanner` and `QueryPlanStore` compile as-is.

## 6. Verification (final binary)

- Build: `MSBUILDDISABLENODEREUSE=1 dotnet build nextorm.slnx -c Debug` → exit 0,
  `0 Warning(s) 0 Error(s)`; log `.pdca/T297/rv5/logs/p03a-build.log`.
- Focused core: `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 600 dotnet test
  tests/nextorm.core.tests -c Debug --no-build --filter
  "FullyQualifiedName~CteDescriptionCowTests|FullyQualifiedName~CachedPreparationRecipeTests"` →
  exit 0, total 33 / succeeded 33 / failed 0 / skipped 0; log
  `.pdca/T297/rv5/logs/p03a-core-focused.log`.
- E19: `... dotnet test tests/nextorm.publicextensibility.tests -c Debug --no-build --filter
  "FullyQualifiedName~T297ProtectedFieldCompatibilityTests"` → exit 0, total 9 / succeeded 9 /
  failed 0; log `.pdca/T297/rv5/logs/p03a-e19-focused.log`.
- Full core project: `... dotnet test tests/nextorm.core.tests -c Debug --no-build` → exit 0,
  total 2658 / succeeded 2658 / failed 0 / skipped 0; log
  `.pdca/T297/rv5/logs/p03a-core-full.log`.
- Inner-loop scope gate: `python3 .pdca/scripts/validate_inner_loop.py brief
  .pdca/T297/rv5/test-scope.json` → exit 0; log
  `.pdca/T297/rv5/logs/test-scope-p03a.log`.

The 0 B warm-memo test (`Descriptions_Join4DefinitionRead_MemoIsValueTypeAndWarmReadsAllocateNothing`)
is part of `CteDescriptionCowTests` and passed unchanged.
