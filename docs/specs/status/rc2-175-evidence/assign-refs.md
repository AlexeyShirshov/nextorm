# D175 assign-refs (V175-A01/A02)

Tool: `roslyn` (refs), target `NextORM.Core.EagerLoadSpec<TEntity, TChild, TKey>.Assign`.

- Declaration: `internal method` at `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:307:19`.
- Definition + 6 references (evidence pack "6" = reference count; the pack enumeration added the
  definition, 7 total):
  - `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:164` (`IEagerLoadSpec.Execute` path)
  - `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:201` (`ExecuteAsync` path)
  - `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:209` (assign-from-rows path)
  - `src/nextorm.core/Builders/Joins/JoinIntoManyToManySpec.cs:331`
  - `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:308`
  - `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:424`
- All consumers are in `nextorm.core`. No consumer in tests, benchmarks or another assembly.
- Decision: keep `Assign` **internal**. No widening (no named external consumer); no weakening to
  `private` (cross-type calls from `JoinIntoSpec`/`JoinIntoManyToManySpec` would break).
