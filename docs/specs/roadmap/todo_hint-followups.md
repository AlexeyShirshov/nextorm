# Hint follow-ups (findings from #130, milestone 1.0.9-b)

Deferred items found while implementing #130 per-join table hints. Both stay in milestone 1.0.9-b.

## A. Command-level WithTableHint does not normalize blank input — <span style="color:green">RESOLVED</span> (2026-09-30)

Resolved by the #121 API cleanup: `WithTableHint`/`WithIndex` now live on `FromOptions` (src/nextorm.core/DataContext/FromOptions.cs) and blank-only input is ignored, so `.WithTableHint(" ")` leaves the options unchanged — it neither pollutes the plan key nor clears a previously set hint; `WithoutIndex()` still sets the empty-ignore form.

## D. TablesInScopeHints not passed on multi-table DELETE/UPDATE join paths — <span style="color:green">ADDRESSED</span> (2026-10-09, #169)

Addressed by #169: `SqlBuilder.MakeDeleteJoin`/`MakeUpdateJoin` now propagate `TablesInScopeHints`
through the target and every join — structural `WITH (...)` on SQL Server, one inline `/*+ ... */`
comment after the DML verb on PostgreSQL/MySQL/MariaDB — and a dialect with neither form rejects a
nonempty scope hint consistently with `SELECT`. Verified issue: https://github.com/AlexeyShirshov/nextorm/issues/169.
Record: `docs/specs/status/rc2-169-dml-scope-hints-1.md` (CHECK PASS, r=1 / n=2).
