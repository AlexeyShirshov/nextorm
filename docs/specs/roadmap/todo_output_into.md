# TODO: `OUTPUT ... INTO` — remaining forms after #266

> Tracking issue: [#266](https://github.com/AlexeyShirshov/nextorm/issues/266).
> Milestone: `1.0.9-rc4`. Collection: `rc4`, task T266.
> Status: **batch capture shipped and live-verified** (T266, rv1); the forms below stay open.
> Registry entry: [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) item 59.

## Delivered by #266 (batch capture, SQL Server only)

- `BatchBuilder.OutputIntoTableVariable(mutation, variableName, columnDefinitions)` with one overload per
  returning terminal — `InsertReturningBuilder<TEntity, TResult>`, `UpdateReturningBuilder<TEntity, TResult>`,
  `DeleteReturningBuilder<TEntity, TResult>`, `MergeReturningBuilder<TEntity, TResult>`.
- Render shape: `DECLARE @t TABLE (<definitions>); <DML> OUTPUT ... INTO @t (<caller column names>)` as one
  batch statement, with **no automatic read-back and no extra result set**; a composed query replaces the
  read-back, and the whole batch is one server round trip.
- `BatchBuilder.FromOutput<TResult>(name)` — typed source over the capture's projection — and
  `BatchBuilder.FromOutputTable(name)` — `TableAlias` source — both composed through `Query`/`AddQuery`.
- MERGE capture always leads with `$action`, written into the caller's **first** declared column.
- Batch-only source ownership: a source read outside its owning batch, an unregistered or duplicate name,
  a cross-batch/foreign-context mutation and a provider without `SupportsOutputInto` all fail closed
  (typed `NotSupportedException`/`InvalidOperationException`, `batch output source`) before a command is
  allocated. Per-step `b{i}_` parameter prefixes keep two steps' captured variables distinct.
- Live SQL Server acceptance (T266 D2): zero/one/many rows, sync/async agreement, two captures, filter /
  order / join / aggregate, one round trip versus the naive two, refusals with zero executions.
- Docs: EN/RU guide 17/18/19/21/25 and `advanced/limitations.md`.
- PostgreSQL is untouched: its data-modifying CTE route (`With(name, insert.Returning(...)).From(name)`)
  stays the way to read modified rows there, and the new capture API refuses it.

## Still absent

| Form | Why it is absent | Trigger to reconsider |
|---|---|---|
| Fluent MERGE `OutputInto(target)` / `OutputIntoThenOutput(target)` | The MERGE output always carries `$action` as its leading expression, which the existing `OutputIntoBuilder` (no row terminals, target column list reused from the projection) cannot express; the batch capture is the only capture form today. | A request for a client-side `OutputInto`/`OutputIntoThenOutput` terminal on a MERGE, or a demonstrated need for it outside a batch. |
| Real-table capture-source helpers (a typed source over a table written by `OutputInto(target)`) | The capture is deliberately batch-owned: reading a real table already has `From(tableName)` / `TableAlias`, and a typed helper would need a new source kind plus a schema contract nextorm does not own. | A requested composable read over a **real** table that `OutputInto(target)` wrote in the same round trip. |
| Join-mutation capture (`CreateUpdateJoinBuilder` / `DeleteJoin` as a capture mutation) | Multi-table `UPDATE`/`DELETE` are not batch steps at all, so they have no capture seam to attach to. | A request to capture the rows of a multi-table update/delete, or join mutations becoming batch steps. |
| Capture outside SQL Server | Only SQL Server has `OUTPUT ... INTO`; PostgreSQL uses its data-modifying CTE, and MySQL/MariaDB/SQLite return rows only where a `RETURNING` form exists. | A provider gaining a scriptable capture form (not planned). |
