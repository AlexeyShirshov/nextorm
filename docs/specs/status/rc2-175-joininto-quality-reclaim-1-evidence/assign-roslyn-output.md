# Roslyn raw output — `NextORM.Core.EagerLoadSpec` (EC175-03)

Captured from the `roslyn` tool at HEAD `3ebaa4ee`.

## `roslyn members NextORM.Core.EagerLoadSpec`

```
NextORM.Core.EagerLoadSpec<TEntity, TChild, TKey>
  public constructor ...EagerLoadSpec(...)  src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:84:12
  internal method ...AddChildren(...)  src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:280:19
  internal method ...Assign(System.Collections.Generic.IReadOnlyList<TEntity>, System.Collections.Generic.Dictionary<TKey, System.Collections.Generic.List<TChild>>)  src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:307:19
  internal method ...AssignChildrenFromRows(...)  src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:146:19
  ... (other members elided)
```

`Assign` = `internal method`, line `307:19`.

## `roslyn refs NextORM.Core.EagerLoadSpec.Assign`

```
def src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:307:19  NextORM.Core.EagerLoadSpec<TEntity, TChild, TKey>.Assign(System.Collections.Generic.IReadOnlyList<TEntity>, System.Collections.Generic.Dictionary<TKey, System.Collections.Generic.List<TChild>>)
  src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:164:9
  src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:201:9
  src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:209:9
  src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs:331:16
  src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:308:16
  src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:424:16
6 reference(s)
```

Every reference path starts with `src/nextorm.core/`. No reference outside `nextorm.core`.
