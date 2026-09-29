# Hint follow-ups (findings from #130, milestone 1.0.9-b)

Deferred items found while implementing #130 per-join table hints. Both stay in milestone 1.0.9-b.

## A. Command-level WithTableHint does not normalize blank input

`EntityBuilder.WithTableHint` (src/nextorm.core/Builders/EntityBuilder.cs) stores an empty list for blank-only input instead of normalizing to null (as the new `WithJoinTableHint` does), so `.WithTableHint(" ")` yields a distinct plan key and clears previously set hints. The XML comment claiming `WithJoinTableHint` mirrors `WithTableHint` is inverted.
Trigger: next work touching command-level table/index hints, or the #122 API cleanup.

## D. TablesInScopeHints not passed on multi-table DELETE/UPDATE join paths

`SqlSourceRenderer.MakeJoinParts` and `SqlBuilder.cs` omit `TablesInScopeHints` for joined tables in multi-table DELETE (PostgreSQL) and UPDATE (PostgreSQL, SQLite), so scope hints are dropped there.
Trigger: next work on scope hints or multi-table DML.
