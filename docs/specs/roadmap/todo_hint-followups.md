# Hint follow-ups (findings from #130, milestone 1.0.9-b)

Deferred items found while implementing #130 per-join table hints. Both stay in milestone 1.0.9-b.

## A. Command-level WithTableHint does not normalize blank input — <span style="color:green">RESOLVED</span> (2026-09-30)

Resolved by the #121 API cleanup: `WithTableHint`/`WithIndex` now live on `FromOptions` (src/nextorm.core/DataContext/FromOptions.cs) and blank-only input is ignored, so `.WithTableHint(" ")` leaves the options unchanged — it neither pollutes the plan key nor clears a previously set hint; `WithoutIndex()` still sets the empty-ignore form.

## D. TablesInScopeHints not passed on multi-table DELETE/UPDATE join paths

`SqlSourceRenderer.MakeJoinParts` and `SqlBuilder.cs` omit `TablesInScopeHints` for joined tables in multi-table DELETE (PostgreSQL) and UPDATE (PostgreSQL, SQLite), so scope hints are dropped there.
Trigger: next work on scope hints or multi-table DML.
