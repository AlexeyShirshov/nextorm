# Issue #186 — Direct result-set iteration for `BatchResult` and `ProcedureResult`

Status: design (implementation in progress)
Tracking: GitHub issue #186, milestone `1.0.9-b`
Branch: `1.0.9-b`

## Problem

Two multi-result-set surfaces expose different traversal APIs:

| | `BatchResult` (`src/nextorm.core/Builders/BatchBuilder.cs:366`) | `ProcedureResult` (`src/nextorm.core/DataContext/ProcedureResult.cs:20`) |
|---|---|---|
| traversal | `ResultSetCount` + positional `Read<TResult>()` | `ReadSets()` / `ReadSetsAsync()` → `IEnumerable<ResultSet>` / `IAsyncEnumerable<ResultSet>` |
| cursor metadata | none | `ResultSet.Index` / `FieldCount` / `ColumnNames` |
| buffer | eager, not disposable | live reader, disposable |

The user requirement: iteration over result sets must have the same semantics. Today a raw/procedure result supports `foreach (var set in procedureResult.ReadSets())` while a batch result does not support cursor enumeration at all, and the `ReadSets` name is an unnecessary intermediate step.

## Decision

Make the result object itself enumerable, on both types, and remove the intermediate methods.

* `ProcedureResult : IEnumerable<ResultSet>, IAsyncEnumerable<ResultSet>`; delete `ReadSets()` / `ReadSetsAsync()`.
* `BatchResult : IEnumerable<ResultSet>, IAsyncEnumerable<ResultSet>`; each cursor reads the already-buffered set.
* `ResultSet` becomes backend-agnostic via an internal cursor-source abstraction.

Target usage:

```csharp
foreach (var set in result)                 // BatchResult or ProcedureResult
{
    var rows = set.Read<MyRow>();           // Index / FieldCount / ColumnNames available
}

await foreach (var set in result)           // async twin
{
    await foreach (var row in set.ReadAsync<MyRow>()) { }
}
```

## Design

### `ResultSet` backend abstraction

`ResultSet` is today a `readonly struct` holding `ProcedureResult? _owner` and delegating `Read<T>` / `ReadAsync<T>` to it. Generalize the owner to an internal interface implemented by both result types:

```csharp
internal interface IResultSetCursorSource
{
    IReadOnlyList<T> ReadCurrentSet<T>(ResultSet cursor);
    IAsyncEnumerable<T> ReadCurrentSetAsync<T>(ResultSet cursor, CancellationToken cancellationToken);
}
```

`ResultSet` keeps its public surface (`Index`, `FieldCount`, `ColumnNames`, `Read<T>`, `ReadAsync<T>`) and swaps `ProcedureResult? _owner` for `IResultSetCursorSource? _source`. `internal Source` replaces `internal Owner`; the staleness guard (`ProcedureResult.BeginCursorRead`, `:380`) compares `ReferenceEquals(cursor.Source, this)`.

The class members `ReadCurrentSet` / `ReadCurrentSetAsync` stay `internal` and satisfy the interface through explicit implementations so no new public API leaks.

### `ProcedureResult`

* `GetEnumerator()`: `ThrowIfDisposed(); ThrowIfReaderClosed(); BeginSetTraversal(); return ReadSetsCore().GetEnumerator();`
* `GetAsyncEnumerator(ct)`: same guards, `return ReadSetsAsyncCore(ct).GetAsyncEnumerator(ct);`
* Delete `ReadSets()` / `ReadSetsAsync()`; keep the private `ReadSetsCore` / `ReadSetsAsyncCore` iterators and the whole one-shot/`_traversalActive` / `_currentCursorConsumed` state machine unchanged.
* Positional `Read<T>()` / `ReadAsync<T>()` and their mixing guard (`ThrowIfSetTraversalStarted`) stay; they become the legacy single-style path.

The existing semantics are preserved: one-shot forward-only sequence, column-less sets skipped and not counted, each set read once, stale cursor throws, disposal via the result.

### `BatchResult`

* Constructor gains `IReadOnlyList<string[]> columnNames`.
* Add cursor-traversal state mirroring `ProcedureResult`: `_setsStarted`, `_setsEnumerated`, `_traversalActive`, `_currentCursorConsumed`, `_legacyReadUsed`, `_cursorIndex`.
* `Read<TResult>()` (positional, kept): call `ThrowIfSetTraversalStarted()` and set `_legacyReadUsed = true` before the existing type-checked read.
* `GetEnumerator()` / `GetAsyncEnumerator(ct)`: `BeginSetTraversal()` then a buffered iterator over `_sets`, yielding `new ResultSet(this, i, _columnNames[i])`.
* Explicit `IResultSetCursorSource` implementation:
  * rejects stale/uninitialized/foreign cursors and double reads with the same exception wording as `ProcedureResult`;
  * type-checks against `_resultTypes[Index]` (batch knows the declared projected type) and returns `(IReadOnlyList<T>)_sets[Index]`;
  * the async path yields the same buffered list (no reader involved).
* `BatchResult` stays `sealed`, eager and not disposable.

`BatchRunner.ReadMultiple` / `ReadMultipleAsync` (`src/nextorm.core/DataContext/BatchRunner.cs:296`, `:319`) snapshot `reader.GetName(i)` for the current set before materialising it and pass the names to the constructor.

### Semantics kept per type (documented, not forced)

* Wrong `T`: `BatchResult` throws `InvalidOperationException` **without** consuming the set; `ProcedureResult` advances the cursor before mapping, so a bad `T` consumes the set. This is inherent — a raw command has no declared schema.
* `ProcedureResult` has no `ResultSetCount`: a live reader cannot count without draining. `BatchResult.ResultSetCount` stays.
* Positional `Read` remains on both but cannot be mixed with enumeration.

## Breaking changes

* Removed `ProcedureResult.ReadSets()` and `ProcedureResult.ReadSetsAsync(CancellationToken)` — superseded by direct enumeration.
* No public-API analyzer baseline exists, so no baseline update is required.

## Test plan

* Rewrite `tests/nextorm.sqlite.tests/RawResultSetTraversalTests.cs` to iterate `procedureResult` (sync and `await foreach`).
* Update `tests/nextorm.sqlite.tests/RawCommandTests.cs` multi-set reads.
* Update `tests/nextorm.integration.tests/CommonTestSuite.Raw.cs` and provider-specific procedure tests.
* Add batch cursor tests to `tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs`: enumerate a `BatchResult`, check `Index`/`FieldCount`/`ColumnNames`, per-set `Read<T>`, one-shot guard, mixing with positional `Read` throws.
* Keep the core ownership gate `tests/nextorm.core.tests/ResultSetEnumeratorOwnershipTests.cs` green.

## Docs

Reconcile `docs/guide/23-sql-batch.md` and `docs/guide/12-raw-sql.md` (EN + RU) onto the single model: the result object is enumerable, `ResultSet` is the common cursor, and only the buffering/disposal difference between the two types is called out.

## References

* `docs/specs/roadmap/evidence-02-resultset-unification.md`
* `docs/specs/design/API-NAMING-REVIEW.md:5264-5284`
* `docs/specs/status/procedure-result-sets-1.md`
