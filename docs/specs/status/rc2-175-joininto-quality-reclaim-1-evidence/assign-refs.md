# D175 assign-refs (EC175-03 / A3)

Tool: `roslyn` (`members` + `refs`), target `NextORM.Core.EagerLoadSpec<TEntity,TChild,TKey>.Assign`.
Current tree HEAD `3ebaa4ee`.

- Declaration: `internal method ... .Assign(IReadOnlyList<TEntity>, Dictionary<TKey, List<TChild>>)`
  at `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:307:19` (Roslyn `members`).
- Roslyn `refs` output: `6 reference(s)`, all under `src/nextorm.core`:
  - `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:164:9` (`IEagerLoadSpec.Execute` path)
  - `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:201:9` (`ExecuteAsync` path)
  - `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:209:9` (assign-from-rows path)
  - `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs:331:16`
  - `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:308:16`
  - `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:424:16`
- All consumers are in `nextorm.core`. No consumer in tests, benchmarks or another assembly.
- Decision: keep `Assign` **internal**. No widening (no named external consumer); no narrowing to
  `private` (cross-type calls from `JoinIntoSpec`/`JoinIntoManyToManySpec` would break).

Raw tool output: `assign-roslyn-output.md`.
