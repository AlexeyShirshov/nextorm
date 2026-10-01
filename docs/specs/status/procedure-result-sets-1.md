# PDCA #118 / round 1 — ProcedureResult one-shot result-set traversal

- issue: #118 (milestone `1.0.9-b`)
- branch: `1.0.9-b` (single-group collection; work in place, no worktree/branch/commit/push)
- round: 1 of N. This file is the frozen contract + D0–D6 plan + evidence ledger.
- scope of this round: **D0–D6 complete** (status/plan, core API, full edge tests, docs EN+RU, coverage/perf, audits, final record) on `1.0.9-b`.

## 1. Frozen contract (implement exactly)

### 1.1 Public API — `src/nextorm.core/DataContext/ProcedureResult.cs` (public sealed; existing signatures unchanged)
- `public IEnumerable<ResultSet> ReadSets();`
- `public IAsyncEnumerable<ResultSet> ReadSetsAsync(CancellationToken ct = default);`

### 1.2 New top-level `public readonly struct ResultSet` — new file `src/nextorm.core/DataContext/ResultSet.cs`
- `public int Index { get; }` — 0-based among column-bearing sets only.
- `public int FieldCount { get; }`
- `public IReadOnlyList<string> ColumnNames { get; }` — stable `string[]` snapshot taken when the cursor is produced; readable after the outer cursor advances.
- `public IReadOnlyList<T> Read<T>();` — eager, twin of the existing `Read<T>`.
- `public IAsyncEnumerable<T> ReadAsync<T>(CancellationToken ct = default);` — lazy, twin of the existing `ReadAsync<T>`.
- Internal constructor; no new generic constraints; no public members beyond the above.

### 1.3 Semantics
- Cursor stores the owner ref, the set index and the metadata snapshot. `Read*` validates: owner disposed ⇒ `ObjectDisposedException`; default/uninitialized/stale cursor (index != `ProcedureResult._resultSetIndex`) or use after the outer iteration ended ⇒ `InvalidOperationException`.
- Outer iteration is one-shot. `MoveNext` advances via the existing internal `ResultSetNavigator.MoveToNextResultSet`/`MoveToNextResultSetAsync` (mirror `BatchRunner.cs:296-336`); unread rows of the current set are auto-skipped; leading/intermediate/trailing column-less sets (DDL/DML) are skipped and do NOT count toward `Index`. Zero column-bearing sets ⇒ empty enumeration; a column-bearing set with zero rows is still yielded. Invalidate the current cursor before advancing and in the iterator `finally` (exhaustion, break, cancellation, failure).
- One consuming read attempt per set (sync eager or async lazy); mixing legacy `ProcedureResult.Read*`/`ReadAsync*` with the new traversal is rejected (`InvalidOperationException`); re-enumeration of `ReadSets*` is rejected; overlapping reader operations rejected.
- Async: propagate `[EnumeratorCancellation]`; inner `ReadAsync` honors its own token; cancellation ⇒ `OperationCanceledException`, invalidates traversal, forbids restart.
- Outputs: `OutputParameters`/`ReturnValue` still work after sets are exhausted (existing drain/close semantics). If outputs were read first ⇒ `ReadSets*` throws via `ThrowIfReaderClosed` (`ProcedureResult.cs:441`; `:433` before the D1/D4 edits). Accessing outputs during an active traversal is rejected.
- Outer enumerator disposal invalidates cursors but must NOT dispose `ProcedureResult`/reader (caller retains ownership).
- Reuse `ResultSetNavigator` unchanged; add private current-set mapping helpers on `ProcedureResult` (`ReadCurrentSet<T>`/`ReadCurrentSetAsync<T>`) and private guarded iterator cores; do not alter existing `Read<T>`/`ReadAsync<T>` behavior or advance-before-map.

## 2. D0–D6 list

| id | deliverable | files | status |
|----|-------------|-------|--------|
| D0 | frozen contract, D-plan, collection row, baselines to `/tmp/opencode/118/` | this file, `collection-1.0.9-b.md` | done (this round) |
| D1 | core API: `ResultSet` struct + `ReadSets`/`ReadSetsAsync` + cursor guards + mapping helpers | `src/nextorm.core/DataContext/ResultSet.cs` (new), `ProcedureResult.cs` | done (this round) |
| D2 | behavioural test suite for the full contract (sync + async edge cases) | `tests/nextorm.sqlite.tests/RawResultSetTraversalTests.cs` | done (24 traversal tests; V8/V9/V12 async variants + V16/V18) |
| D3 | docs EN+RU (`docs/guide/12-raw-sql.md`, `docs/advanced/api-reference.md` + `docs/ru/**`) | docs | done (EN+RU incl. `ResultSet` api-reference + pointer fix) |
| D4 | coverage close-out + perf acceptance (cached path unchanged) | tests/benchmarks | done (88.2 line / 78.9 branch; `ProcedureResultSetsBenchmark`; acceptance 7/7) |
| D5 | audits: `nextorm-code-auditor` + `nextorm-design-engineer` | — | done |
| D6 | final status record / collection close-out | this file | done |

## 3. Variant matrix (must end green or fail fast)

| # | form | expected |
|---|------|----------|
| V1 | two column-bearing sets, sync `ReadSets` + `Read<T>` | yielded in order, `Index` 0/1, metadata snapshot |
| V2 | two column-bearing sets, async `ReadSetsAsync` + `ReadAsync<T>` | same, lazy inner token |
| V3 | leading column-less set (DDL) | skipped, does not consume `Index` |
| V4 | trailing column-less set (DML after the result-bearing statement) | skipped, does not consume `Index` |
| V5 | zero column-bearing sets | empty enumeration |
| V6 | column-bearing set with zero rows | still yielded, `Read*` empty |
| V7 | unread rows of the current set | auto-skipped on advance |
| V8 | cursor from a set the outer cursor advanced past | `InvalidOperationException` |
| V9 | second read of the same set (sync or async) | `InvalidOperationException` |
| V10 | legacy `Read<T>` first, then `ReadSets` | `InvalidOperationException` |
| V11 | `ReadSets` first, then legacy `Read<T>` | `InvalidOperationException` |
| V12 | re-enumeration of a `ReadSets` sequence | `InvalidOperationException` |
| V13 | outer enumerator disposed mid-traversal | cursor invalidated; `ProcedureResult`/reader not disposed; outputs readable |
| V14 | `OutputParameters`/`ReturnValue` read first | `ReadSets*` throws via `ThrowIfReaderClosed` |
| V15 | `OutputParameters` during an active traversal | `InvalidOperationException` |
| V16 | owner disposed, then cursor `Read*` | `ObjectDisposedException` |
| V17 | default/uninitialized `ResultSet` cursor `Read*` | `InvalidOperationException` |
| V18 | cancellation of `ReadSetsAsync` / inner `ReadAsync` | `OperationCanceledException`, traversal invalidated, restart forbidden |

## 4. Test strategy

- **D1 smoke + contract tests (delivered):** `tests/nextorm.sqlite.tests/RawResultSetTraversalTests.cs` (14 tests, V1–V14, V17, V18-adjacent metadata) against a real SQLite database — this is where raw execution is reachable.
- **Regression:** the existing `tests/nextorm.sqlite.tests/RawCommandTests.cs` multi-set tests (`MultipleResultSets_ReadSequentially_ThenThrows`, `MultipleResultSets_ReadSequentiallyAsync_ThenThrows`) must stay green, proving the legacy path is unchanged.
- **Core:** `tests/nextorm.core.tests` is the regression gate for the shared engine (the in-memory provider cannot execute raw), so `ResultSet`/`ProcedureResult` compile/behavioral regressions surface there.
- **D2 (next round):** add the remaining edges (intermediate column-less sets, cancellation with a non-default inner token, overlapping reader operations, `OutputParameters`-first on procedures with real output parameters) and any provider-specific row shapes.
- Placeholder/temp SQLite connections only; no database container needed.

## 5. Docs plan (D3)

- EN: `docs/guide/12-raw-sql.md` (new "Result-set cursors" subsection beside the existing multiple-result-sets text) and `docs/advanced/api-reference.md` (add `ResultSet`).
- RU mirrors under `docs/ru/**` in the same change; renumber/link hygiene per `AGENTS.md`; no links to `docs/specs/**` from public pages.

## 6. Perf plan (D4)

- The new traversal is additive and does not touch the prepared/cached query path. Acceptance: the existing 7-case perf suite must stay within its investigation band; the legacy `Read<T>`/`ReadAsync<T>` fast path is unchanged (no extra allocation).

## 7. Baselines / evidence (D0 + D1, 2026-10-01)

| gate | command | result | log |
|------|---------|--------|-----|
| D0 baseline build | `dotnet build nextorm.slnx -c Debug` | 0 Warning / 0 Error | `/tmp/opencode/118/build-debug.log` |
| new contract tests | `dotnet run --project tests/nextorm.sqlite.tests -c Debug -- -class NextORM.Sqlite.Tests.RawResultSetTraversalTests` | 14 total / 0 failed / 0 skipped | `/tmp/opencode/118/sets-tests.log` |
| legacy raw tests | `... -class NextORM.Sqlite.Tests.RawCommandTests` | 44 total / 0 failed / 0 skipped | `/tmp/opencode/118/rawcommand-existing.log` |
| core regression | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | 1215 total / 0 failed / 0 skipped | `/tmp/opencode/118/core-tests.log` |

## 8. Done / Verified / Incomplete

- Done:
  - **D0.** This frozen contract, the D0–D6 plan, the variant matrix and the evidence ledger; collection row `#118` set to `in-progress` with this file as its task status file.
  - **D1.** `ResultSet` (public readonly struct, internal ctor) and `ProcedureResult.ReadSets`/`ReadSetsAsync`. One-shot iterator cores over the unchanged `ResultSetNavigator`; per-set cursor identity via the existing `_resultSetIndex` plus a consumed flag; stale/uninitialized/disposed/ended validation; legacy-mixing and re-enumeration guards; outputs blocked while a traversal is active and readable after it ends; outer enumerator disposal invalidates cursors only.
- Verified:
  - `dotnet build nextorm.slnx -c Debug` = 0 Warning / 0 Error (after the new API and tests).
  - New `RawResultSetTraversalTests` 14/14 green; existing `RawCommandTests` 44/44 green; core 1215/1215 green.
  - All touched/new text files are CRLF.
- Incomplete:
  - none — D2–D6 closed (see §9).

## 9. Round finalization — D0–D6 done (2026-10-01)

- **D0–D6 done.** Contract frozen; `ResultSet` + `ReadSets`/`ReadSetsAsync` delivered; full edge suite, docs EN+RU, coverage/perf and audits closed; this status file and the collection row finalized.
- **Activation on first `MoveNext`/`MoveNextAsync`.** The one-shot/mode guard is claimed eagerly by `BeginSetTraversal()`, but `_traversalActive` and reader activation happen inside the iterator cores, so an un-enumerated `ReadSets*` sequence never blocks `OutputParameters`/`ReturnValue`.
- **Async lifetime fix.** `ReadCurrentSetAsync<T>` deliberately does NOT `InvalidateCurrentCursor()` on completion: an inner lazy read that finishes after the outer cursor advanced must not zero `_resultSetIndex` and impersonate that advance. The consumed flag keeps a stale copy from re-reading the set.
- **Tests.** `RawResultSetTraversalTests` now covers V1–V18 incl. V16 (disposed owner ⇒ `ObjectDisposedException`), V18 (cancellation ⇒ `OperationCanceledException`, traversal invalidated, restart forbidden) and the async V8/V9/V12 variants; plus integration `~ResultSet` 42/42 and `~Raw` 170/170 across SQLite/PostgreSQL/SQL Server/MySQL. RED→GREEN on the sqlite traversal suite: **4 failed → 0**; full suite 7115 total / 0 failed / 188 capability skips (all providers live).
- **Docs.** `docs/guide/12-raw-sql.md` + `docs/advanced/api-reference.md` and their `docs/ru/**` mirrors updated with the result-set cursors and `ResultSet`; a stale pointer in the api-reference link was fixed. `dotnet docfx docs/docfx.json` 0 Warning / 0 Error.
- **Flake hardening (#125).** Test-only serialization of the shared-global EF query-filter integration classes via `[Collection("EF query filter lifecycle")]`; assertions unchanged.
- **Deferred-with-trigger.** Deterministic (non-sleep) eviction for the sliding-expiration lifecycle test — reopen if it recurs under verified isolation.
