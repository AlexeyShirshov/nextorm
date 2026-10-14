# D226 / rv1 — D0 preflight (excerpts, Roslyn lifecycle/ownership review, rv1 conflict check)

- task: T226 (#226), cycle N=1, plan_revision r=1, evidence_contract rv=1
- owner: coder/G1; verifier: check
- date: 2026-10-14
- worktree: /home/alex/sources/nextorm, branch `1.0.9-rc3`

## 1. Required excerpts (exact current code)

### `src/nextorm.core/DataContext/CommandReaderOwner.cs:16,31`

```
16: internal sealed class CommandReaderOwner : IDisposable, IAsyncDisposable
17: {
18:     private DbCommand? _command;
19:     private DbDataReader? _reader;
21:     internal CommandReaderOwner(DbCommand command, DbDataReader reader)
28:     internal DbCommand Command => _command ?? throw new ObjectDisposedException(nameof(CommandReaderOwner));
31:     internal DbDataReader Reader => _reader ?? throw new ObjectDisposedException(nameof(CommandReaderOwner));
34:     internal bool IsReaderClosed => _reader is null;
40:     internal void CloseReader()          // reader released, command kept (output parameters)
48:     public void Dispose()                // reader then command; idempotent
61:     public async ValueTask DisposeAsync()
```

### `src/nextorm.core/DataContext/ProcedureResult.cs:125,160`

```
125: public IReadOnlyList<T> Read<T>()      // sync positional legacy read
160: public async IAsyncEnumerable<T> ReadAsync<T>(CancellationToken)  // lazy async iterator
177: public void Dispose()      // idempotent via _disposed; _owner.Dispose()
189: public async ValueTask DisposeAsync()
243: private void EnsureOutputsRead()  // -> _owner.CloseReader() (line 253) before reading output params
449: private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
```

### `src/nextorm.core/DataContext/BatchRunner.cs:93,116,296,321`

```
 93: public BatchResult RunBatchMultiple(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers)
 99:     if (!plan.UseJoinedCommand && conn.CanCreateBatch) { using var batch = CreateBatch(...); using var reader = batch.ExecuteReader(); return ReadMultiple(reader, plan, materializers); }
106:     using var command = CreateJoinedCommand(conn, plan); using var joinedReader = command.ExecuteReader(); return ReadMultiple(joinedReader, plan, materializers);
116: public async Task<BatchResult> RunBatchMultipleAsync(...)
296: private static BatchResult ReadMultiple(DbDataReader reader, BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers)
318:     return new BatchResult(sets, resultTypes, columnNames);
321: private static async Task<BatchResult> ReadMultipleAsync(...)
346:     return new BatchResult(sets, resultTypes, columnNames);
```

## 2. Roslyn lifecycle/ownership review (tool `roslyn`, batched in one `execute`)

Commands used (all `tools.roslyn({ action: ... })`, no rg/grep for symbols):
- `members NextORM.Core.CommandReaderOwner`
- `members NextORM.Core.ProcedureResult`
- `members NextORM.Core.BatchResult`
- `refs NextORM.Core.CommandReaderOwner`
- `refs NextORM.Core.CommandReaderOwner.CloseReader`
- `refs NextORM.Core.CommandReaderOwner.Reader`
- `refs NextORM.Core.BatchResult` (covers the internal ctor)
- `refs NextORM.Core.BatchRunner.RunBatchMultiple`
- `members NextORM.Core.IBatchExecutor`
- `members NextORM.Core.BatchPlan`

Facts established:

1. `CommandReaderOwner` is **internal sealed**, constructed only inside `QueryExecutor`
   (`OpenReader` :358, `OpenReaderAsync` :385, `OpenLobReader` :416, `OpenLobReaderAsync` :443,
   `OpenResultReader` :474, `OpenResultReaderAsync` :501), i.e. the raw/procedure path, the LOB path
   and the plain result-reader path. The owner owns the per-call command **and** the reader; the
   connection stays owned by the context (`DataContext.cs` — never closed here).
2. `CloseReader()` has exactly **one** production caller: `ProcedureResult.EnsureOutputsRead`
   (`ProcedureResult.cs:253`). It nulls `_reader` and disposes the reader, keeping `_command`
   so ADO.NET output/return-value parameters become readable. So the "reader released, command
   alive" state is already part of the contract.
3. `Reader`/`Command` getters throw `ObjectDisposedException` after release; `IsReaderClosed`
   is the only non-throwing probe. Consequence for D2: the new `RecordsAffected` getter must be a
   **cached int** and must never touch a released reader (C3 negative).
4. `ProcedureResult.Dispose()`/`DisposeAsync()` are idempotent through the `_disposed` flag and
   delegate to the owner; `_owner` is a readonly field that is never nulled, so a cached getter on
   `ProcedureResult` reading only the owner cache cannot throw after Dispose (C3).
5. `BatchRunner` creates its reader inside `using var reader = ...` in `RunBatchMultiple`/
   `RunBatchMultipleAsync` and disposes it **after** `ReadMultiple`/`ReadMultipleAsync` returns.
   The batch record value therefore has to be snapshotted inside `ReadMultiple`/`ReadMultipleAsync`
   (pre-dispose), because the reader is gone once the public method returns. `ReadMultiple` is the
   only result-set materialisation site; `RunBatchMultiple`/`RunBatchMultipleAsync` are called
   only from `QueryExecutor.RunBatchMultiple`/`RunBatchMultipleAsync`
   (`QueryExecutor.cs:617,625`), which are reached through `IBatchExecutor.ExecuteBatchResults`/
   `ExecuteBatchResultsAsync` (`DataContext.cs:1470,1473`).
6. `BatchResult`'s internal ctor has exactly **two** call sites — `BatchRunner.cs:318` and
   `BatchRunner.cs:346` — both inside the batch runner. Adding `int recordsAffected` to it is
   therefore a private, two-call-site change with no public-API surface other than the new getter.
   `BatchResult` owns no reader (all sets are buffered) and is not `IDisposable` — the value is a
   plain int captured at construction, which satisfies C2's "returned already materialized".
7. No close-order/ownership change is needed: D2 adds capture *around* the existing
   `reader.Dispose()`/`DisposeAsync()` calls without touching who calls them or in what order;
   the command is still disposed after the reader. Cleanup exceptions from
   `reader.Dispose()`/`command.Dispose()` are not wrapped, so they keep propagating (C3 negative
   "existing Close/Dispose errors are not masked").

Ownership conclusion: the invariant "one writer of the reader = CommandReaderOwner (raw path) /
`using` in BatchRunner (batch path)" stays intact; the new state is one cached `int` per owner and
one readonly `int` per `BatchResult`. No new type, no new interface, no per-row work — the capture
runs at release only (O(1)), consistent with the plan's perf decision.

## 3. rv1 conflict check (does anything already claim this contract?)

- Roslyn `members NextORM.Core.ProcedureResult` and `members NextORM.Core.BatchResult`: **no**
  `RecordsAffected` (nor any affected-row/row-count member) exists on either type today — no
  member, no interface implementation (neither type implements anything declaring it).
- `BatchResult` exposes `ResultSetCount` only (a **result-set** count), which C4 explicitly forbids
  as a substitute oracle; it is not an affected-row member and stays unchanged.
- Text search (`grep`, docs/strings only — not used for symbols) for the literal `RecordsAffected`:
  the only production occurrence is the LOB wrapper `LobDataReader.RecordsAffected` (delegating to
  `_inner`), plus `override int RecordsAffected` in test/benchmark fake readers. None of them is a
  public nextorm API on a result type; no naming or behaviour conflict with `public int
  RecordsAffected { get; }` on `ProcedureResult`/`BatchResult`.
- Cross-check against rv1 rows E226-01..E226-09: the rows require exactly the signatures
  `public int RecordsAffected { get; }` on both types, one shared safe metadata read, and no
  setter / no row counting. Nothing in the current tree implements or blocks any of them, and
  `IBatchExecutor`/`BatchPlan` need no signature change (the batch ctor is internal). **No conflict.**

## 4. Outcome

D0 PASS. Preconditions for D1 are met; proceed to the real-driver probe before any production edit.
