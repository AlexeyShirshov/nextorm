# Evidence 02: multi-result-set surfaces — `BatchResult` vs `ProcedureResult`

## Question

Read-only fact-gathering in `/home/alex/sources/nextorm`. The user states that guide 23
("Multiple result sets" / "Несколько наборов результатов") and guide 12 (which also describes
multiple result sets) are NOT unified, and that result-set handling must have identical semantics.

Collect the ground truth about the two APIs and any prior agreed design decisions:

1. Full public surface of `BatchResult`, `ProcedureResult`, `ResultSet`, any shared base/helper.
2. Exact runtime semantics of each (`Read<T>` type mismatch, read past last, re-read, buffering,
   column-less sets, streaming, `ReadSets`, outputs).
3. Docs text of guide 23 and guide 12 (EN + RU), with semantic divergences.
4. Prior decisions/specs about batches, multi-result sets, `ReadSets`, unified semantics.
5. Tests pinning each semantic, and the providers they run on.
6. Whether the two are recorded as intentionally different and whether a unification issue exists.

## Commands

All commands run in `/home/alex/sources/nextorm` on 2026-10-04 (env date), git worktree branch
`1.0.9-b`. The working tree has unrelated uncommitted edits to `docs/guide/12-raw-sql.md` and
`docs/ru/guide/12-raw-sql.md` (a `BindEntity` example, lines ~242-255), NOT to the
multiple-result-set sections; guide 23 is unmodified.

- `roslyn members NextORM.Core.BatchResult`
- `roslyn members NextORM.Core.ProcedureResult`
- `roslyn types ResultSet`
- `roslyn refs NextORM.Core.BatchResult`
- `roslyn refs NextORM.Core.ResultSetNavigator`
- `roslyn callers NextORM.Core.BatchResult.Read`
- `roslyn callers NextORM.Core.ProcedureResult.Read`
- `gh issue list --repo AlexeyShirshov/nextorm --search "<term>" --state all --limit 30 --json number,title,state,milestone`
  for terms: `result set`, `ReadSets`, `BatchResult`, `ProcedureResult`, `resultset`, `unify result`, `унифицировать`
- `gh issue view 25 --repo AlexeyShirshov/nextorm --json number,title,state,milestone,body`
- `git diff --stat -- docs/guide/12-raw-sql.md docs/ru/guide/12-raw-sql.md`
- `git diff -- docs/guide/12-raw-sql.md | head -200`

## Facts

### 1. Implementation types and full public surface

#### `BatchResult` — `src/nextorm.core/Builders/BatchBuilder.cs:366`

`public sealed class BatchResult` (line 366) is NOT disposable (no `IDisposable`/`IAsyncDisposable`
in the declaration). Class doc (lines 361-365):

> "The eagerly materialised result sets of a batch ... Every set is fully buffered in memory, so the
> instance owns no reader or connection and is not disposable."

Members (roslyn `members`):

- `internal BatchResult(IReadOnlyList<IList> sets, IReadOnlyList<Type> resultTypes)` —
  `BatchBuilder.cs:372:14` (internal ctor).
- `private readonly IReadOnlyList<IList> _sets` — `:368`; `_resultTypes` — `:369`; `_index` — `:370`.
- `public int ResultSetCount => _sets.Count;` — `:379`.
- `public IReadOnlyList<TResult> Read<TResult>()` — `:395`. Implementation `:395-407`.

There is no `ReadAsync`, no `ReadSets`/`ReadSetsAsync`, no `Index`/`FieldCount`/`ColumnNames`, no
streaming path, and no `IDisposable`. A batch streams only through the single-result terminal
`BatchQuery<TResult>.ToAsyncEnumerable` (`src/nextorm.core/Builders/BatchBuilder.cs:517-521`).

#### `ProcedureResult` — `src/nextorm.core/DataContext/ProcedureResult.cs:20`

`public sealed class ProcedureResult : IAsyncDisposable, IDisposable` (line 20). Members (roslyn
`members`):

- `internal ProcedureResult(DataContext context, CommandReaderOwner owner)` — `:35`.
- `public IReadOnlyList<ProcedureOutputParameter> OutputParameters` — `:46`.
- `public object? ReturnValue` — `:61`.
- `public IEnumerable<ResultSet> ReadSets()` — `:87`.
- `public IAsyncEnumerable<ResultSet> ReadSetsAsync(CancellationToken cancellationToken = default)` — `:103`.
- `public IReadOnlyList<T> Read<T>()` — `:120`.
- `public async IAsyncEnumerable<T> ReadAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default)` — `:155`.
- `public void Dispose()` — `:172`; `public async ValueTask DisposeAsync()` — `:184`.
- internal `ReadCurrentSet<T>(ResultSet cursor)` — `:352`; `ReadCurrentSetAsync<T>(ResultSet cursor, CancellationToken)` — `:366`.
- private navigation/guards: `MoveToNextResultSet` `:194`, `MoveToNextResultSetAsync` `:216`,
  `EnsureOutputsRead` `:238`, `ReadSetsCore` `:272`, `ReadSetsAsyncCore` `:313`,
  `BeginCursorRead` `:380`, `SnapshotColumns` `:400`, `InvalidateCurrentCursor` `:409`,
  `BeginSetTraversal` `:415`, `ThrowIfSetTraversalStarted` `:428`, `AlreadyEnumerated` `:434`,
  `Normalize` `:437`, `ThrowIfDisposed` `:439`, `ThrowIfReaderClosed` `:441`, `NoMoreResultSets` `:447`.

There is no `ResultSetCount` property on `ProcedureResult` (not in the members list), no `Index`,
and no random access.

#### `ResultSet` — `src/nextorm.core/DataContext/ResultSet.cs:16`

`public readonly struct ResultSet` (line 16), produced only by
`ProcedureResult.ReadSets`/`ReadSetsAsync` (doc `:4-5`). Public members:

- `public int Index => _index;` — `:30` ("The 0-based position of this set among the column-bearing
  result sets of the command.").
- `public int FieldCount => _columnNames?.Length ?? 0;` — `:33`.
- `public IReadOnlyList<string> ColumnNames => _columnNames ?? Array.Empty<string>();` — `:36`
  ("snapshotted when the cursor was produced").
- `public IReadOnlyList<T> Read<T>()` — `:43` (delegates to `_owner.ReadCurrentSet<T>(this)`).
- `public IAsyncEnumerable<T> ReadAsync<T>(CancellationToken ct = default)` — `:54`.
- internal `Owner` `:59`, `IsInitialized` `:61`.

No `ResultSet` analogue exists for `BatchResult`; the two batch sets are not surfaced as cursors.

#### Shared abstraction — low-level reader navigation only

`ResultSetNavigator` — `src/nextorm.core/DataContext/ResultSetNavigator.cs:9` — doc (lines 5-8):

> "Shared result-set navigation for commands that return more than one set. Moved **verbatim out of
> `BatchRunner`** so the batch reader and the raw-command reader advance identically."

Methods: `AdvanceToResultSet` `:15`, `AdvanceToResultSetAsync` `:28`, `MoveToNextResultSet` `:43`,
`MoveToNextResultSetAsync` `:59`. `roslyn refs` shows use in `BatchRunner.cs` (188,198,276,287,300,
312,323,336,365,376) and `ProcedureResult.cs` (200,207,222,229,288,300,327,339). `BatchResult` does
**not** reference it (only `BatchRunner` does, while materialising the sets before constructing
`BatchResult` — `BatchRunner.cs:296-343`).

**There is no shared base class, interface or surface abstraction between `BatchResult` and
`ProcedureResult`.** `ResultSetNavigator` is a shared *static low-level helper* over `DbDataReader`;
the two public result types remain separate. The duplicated high-level paths are:

- `BatchRunner.ReadMultiple` `:296-317` and `ReadMultipleAsync` `:319-343` (advance + materialise all).
- `ProcedureResult.MoveToNextResultSet` `:194-214`, `MoveToNextResultSetAsync` `:216-236`,
  `ReadSetsCore` `:272-311`, `ReadSetsAsyncCore` `:313-350` (advance + yield / consume).

Where each `Read<T>` lives: `BatchResult.Read<TResult>` is `BatchBuilder.cs:395-407`;
`ProcedureResult.Read<T>` is `ProcedureResult.cs:120-138`;
`ProcedureResult.ReadAsync<T>` is `ProcedureResult.cs:155-169`;
`ResultSet.Read<T>` is `ResultSet.cs:43-46`.

### 2. Runtime semantics (batch vs procedure)

**`BatchResult`:**

- Wrong type on `Read<TResult>` → `InvalidOperationException`; the set is **not** consumed. Code
  `BatchBuilder.cs:400-405`:
  ```csharp
  var expected = _resultTypes[_index];
  if (expected != typeof(TResult))
      throw new InvalidOperationException($"Result set {_index} projects '{expected.Name}', not '{typeof(TResult).Name}'; read it with the matching type.");
  var set = (IReadOnlyList<TResult>)_sets[_index];
  _index++;
  ```
  Test `tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs:136-160`
  (`Read_WithWrongTypeInSecondPosition_ShouldThrowAndNotAdvance`) proves the retry.
- Reading past the last set → `InvalidOperationException` (`:397-398`
  `"The batch has {_sets.Count} result set(s); every result set has already been read."`). Test
  `BatchMultipleResultTests.cs:184-204`.
- Re-read: forward-only; `_index` advances on every successful `Read`, so a set is read once. Order
  is the order the queries were added (`:382-389`).
- Buffering: fully eager/in-memory (`_sets` holds `IList`); no reader/connection held; not disposable.
- Column-less sets: `BatchResult` only ever holds the declared query result sets. `BatchRunner`
  skips preceding zero-column side-effecting sets with `ResultSetNavigator.AdvanceToResultSet`
  (`:276,287,300,323`) and throws if a declared set is missing (`MissingResultSet` `:307,313,338`,
  defined `:360-361`).
- `ReadSets`/async/streaming for `BatchResult`: **none** (see §1).

**`ProcedureResult`:**

- Wrong type on `Read<T>` → no declared-type comparison. `Read<T>` advances first, then builds the
  mapper for whatever `T` was asked: `_resultSetIndex++` (`:130`) then
  `RawMapperFactory.GetOrBuild<T>(_context, reader)` (`:131`). A type mistake therefore surfaces as a
  mapper error, not a type-mismatch guard, and the set is already consumed:
  - entity with no matching columns → `InvalidOperationException`
    (`tests/nextorm.sqlite.tests/RawCommandTests.cs:230-247`);
  - unmappable type → `NotSupportedException` (`RawCommandTests.cs:351-367`).
  Comment `:128-129`: "Advance the cursor before mapping ... a mapping failure must not make the next
  Read re-read this set (the reader is already positioned past it)." The same in `ReadAsync<T>`
  `:163-165`.
- Reading past the last set → `InvalidOperationException` from `MoveToNextResultSet` (`:204-205`
  `if (!reader.NextResult()) throw NoMoreResultSets();` and `:210-211` `if (reader.FieldCount == 0)`).
  `NoMoreResultSets()` `:447-448`: `"The command has no further result sets to read. ..."`.
  Tests `RawCommandTests.cs:250-268`, `:271+`, and `BatchMultipleResultTests`-independent.
- Re-read: positional `Read` advances each call, already-read sets not revisited. For cursors,
  `BeginCursorRead` rejects a second read via `_currentCursorConsumed` (`:393-394`
  `"This result set has already been read; each set can be read once, either eagerly or lazily."`).
  Tests `RawResultSetTraversalTests.cs:178-200`, `:510-549`.
- Streaming/reader ownership: holds the ADO.NET command + reader (`CommandReaderOwner`) open until
  `Dispose`/`DisposeAsync` (`:20`, `:172-192`); the connection stays owned by the context (doc
  `:17-18`, `docs/guide/12-raw-sql.md:303`). `Read<T>` materialises one set into `List<T>`
  (`:133-137`) but keeps the reader for the next set; `ReadAsync<T>` is a lazy iterator over the
  current set (`:155-169`); `ResultSet.ReadAsync<T>` is lazy (`ResultSet.cs:54-57`).
- Column-less sets: leading/intermediate/trailing sets without columns are skipped and do not count
  toward `ResultSet.Index` (doc `:71-77`; `ReadSetsCore` `:288-290`, `:300`;
  `ReadSetsAsyncCore` `:327-329`, `:339`). Test `RawResultSetTraversalTests.cs:60-85`,
  `RawCommandTests.cs:665-681`.
- `ReadSets()` cursors:
  - `Index` 0-based among column-bearing sets; `FieldCount`; `ColumnNames` snapshot
    (`ResultSet.cs:29-36`).
  - One-shot / forward-only outer sequence (`BeginSetTraversal` `:415-426`; `AlreadyEnumerated`
    `:434-435`). Re-enumeration throws. Test `RawResultSetTraversalTests.cs:244-263`.
  - Mixing with legacy `Read<T>`/`ReadAsync<T>` throws
    (`ThrowIfSetTraversalStarted` `:428-432`; `BeginSetTraversal` checks `_legacyReadUsed` `:417-418`).
    Tests `:202-242`.
  - Cursor validity: `BeginCursorRead` `:380-398` rejects uninitialized / not-owner / traversal-not-
    active / `cursor.Index != _resultSetIndex` ("stale"), and already-consumed. Tests `:154-200`,
    `:472-549`.
  - `OutputParameters`/`ReturnValue`: `EnsureOutputsRead` `:238-270` calls `_owner.CloseReader()`
    (`:248`) and snapshots parameters, discarding unread results; `ThrowIfReaderClosed` `:441-445`
    blocks later reads. Reading outputs during an active traversal throws `:243-244`. Tests
    `RawResultSetTraversalTests.cs:265-317`, `SqlServerSpecificTests.cs:979-993`.

**Divergence table (both are sequential, forward-only, once-per-set, "past the end throws
`InvalidOperationException`", and skip column-less sets):**

| Point | `BatchResult` | `ProcedureResult` |
|---|---|---|
| Surface | `ResultSetCount` + `Read<TResult>()` only | `Read<T>`, `ReadAsync<T>`, `ReadSets`, `ReadSetsAsync`, `OutputParameters`, `ReturnValue` |
| Type of a set | enforced against pre-declared projected type | no declared-type check; mapper built from reader for the requested `T` |
| Wrong `T` | `InvalidOperationException`, set NOT consumed (retry works) | mapper error (`InvalidOperationException` / `NotSupportedException`); set already consumed (advance-before-map) |
| Past the end | `"every result set has already been read."` | `"The command has no further result sets to read."` |
| Buffering | fully eager, buffered; owns no reader/connection | streams; owns reader+command until disposed |
| Disposable | no (not `IDisposable`) | yes (`IDisposable`, `IAsyncDisposable`) |
| Cursor / per-set `Index`/`FieldCount` | none | `ResultSet` cursors |
| Async row read | none (`BatchResult` has no `ReadAsync`) | `ReadAsync<T>`, `ReadSetsAsync` |
| Column-less sets | hidden by `BatchRunner` before `BatchResult` exists | skipped in traversal; never counted |
| Count | `ResultSetCount` | none (traverse to count) |

### 3. Docs text and divergences

#### `docs/guide/23-sql-batch.md`

- `## Multiple result sets` — lines 175-215. Key claims:
  - `:206-211` `BatchResult` members table: `ResultSetCount`; `Read<TResult>()`.
  - `:213`:
    > "`Read<TResult>()` is typed: the requested type must match the projected type of the next
    > query, otherwise it throws `InvalidOperationException`; reading past the last set throws
    > `InvalidOperationException` as well. `BatchResult` is **eager** and owns no reader or
    > connection — every set is buffered before `Execute()` returns, so it is not disposable and does
    > not stream."
- `## Limitations` — lines 230-238. Multi-result bullets:
  - `:232`: "A batch ends with one or more result-bearing queries ... The statements before them are
    side-effecting ... which return no columns."
  - `:233`:
    > "Multi-result execution is **sequential and eager**: `BatchResult.Read<TResult>()` returns the
    > sets in the order the queries were added, each set may be read once, and every set is fully
    > buffered in memory before `Execute()`/`ExecuteAsync()` returns — there is no streaming and no
    > random access. Only the single-result `BatchQuery<TResult>` terminal streams, through
    > `ToAsyncEnumerable`."

`docs/ru/guide/23-sql-batch.md` — same line ranges (175-215, 230-238); RU text at `:213` and `:233`
is a literal mirror of the above.

#### `docs/guide/12-raw-sql.md`

- `### Multiple result sets` — lines 362-372. `:364`:
  > "Each `Read<T>()` call advances to the next result set and materialises all of its rows.
  > Already-read sets are not revisited. When the command has no further result set, `Read<T>()`
  > throws `InvalidOperationException`."
- `#### Result-set cursors (ReadSets)` — lines 374-412:
  - `:376` cursor rationale, per-set `T`, `Read<T>()`/`ReadAsync<T>(ct)`, async twin `ReadSetsAsync(ct)`.
  - `:397-402` cursor members: `Index`, `FieldCount`, `ColumnNames` (snapshot), `Read<T>()`/`ReadAsync<T>(ct)`.
  - `:404-410` traversal rules: one-shot/forward-only; one read per set; stale cursor → exception;
    disposed owner → `ObjectDisposedException`; column-less sets skipped and not counted; unread rows
    auto-skipped; cancellation; outputs; ownership.
  - `:412`: "A buffering `ReadAllAsync` that materialises every set at once is intentionally not
    provided ...".
- Related `ProcedureResult` semantics: `:277` ("hand back a `ProcedureResult`"), `:299`
  ("holds the command and its reader open until disposed"), `:303` (dispose), `:331` (scalar first
  `Read` skips leading column-less), `:342` (entity mapping), `:443` / `:459` (outputs close the reader).

`docs/ru/guide/12-raw-sql.md` — `### Несколько наборов результатов` at 367-377, and
`#### Курсоры наборов результатов (ReadSets)` at 379-417 (same content; the trailing
`ReadAllAsync` sentence is at `:417` instead of `:412`).

#### Semantic points where guide 23 and guide 12 DIFFER / omit

1. **Type enforcement.** Guide 23 `:213` states `Read<TResult>()` is type-checked against the
   projected type and mismatch throws. Guide 12 `:364` describes `Read<T>` generically ("advances to
   the next result set") and `:331`/`:342` describe scalar/entity mapping — no projected-type check,
   no mismatch case.
2. **Wrong-type retry.** Guide 23 (with the source remark `BatchBuilder.cs:390-392`) says a
   wrong-type call throws *without consuming the set*. Guide 12 does not describe any wrong-type
   behaviour; the code advances before mapping, so the set is consumed.
3. **Eager vs live reader / disposal.** Guide 23 `:213` says eager, no reader/connection, not
   disposable. Guide 12 `:299`/`:303` says `ProcedureResult` holds the command and reader open and
   must be disposed.
4. **Cursor API.** Guide 12 `:374-412` documents `ReadSets`/`ReadSetsAsync`, `ResultSet`,
   `Index`/`FieldCount`/`ColumnNames`. Guide 23 documents none of these for `BatchResult`.
5. **Count.** Guide 23 `:210` has `ResultSetCount`; guide 12 has no count (only cursor `Index`).
6. **Async row read.** Guide 23's `BatchResult` has no async read (only `ExecuteAsync` on the
   builder). Guide 12 has `ReadAsync<T>` and `ReadSetsAsync`.
7. **Column-less handling.** Guide 12 `:407` explicitly: leading/intermediate/trailing column-less
   sets are skipped and do not count. Guide 23 `:232` only says side-effecting statements "return no
   columns"; it does not state the skip/normalisation rule.
8. **Streaming.** Guide 23 `:233` says only the single-result `BatchQuery<TResult>` streams. Guide 12
   `:412` says a buffering `ReadAllAsync` is intentionally absent (the cursor path streams per set).

Points where they agree: forward-only; sets read in order, each once; reading past the last set
throws `InvalidOperationException` (guide 23 `:213`, guide 12 `:364`).

### 4. Prior decisions / specs

- **`docs/specs/design/API-NAMING-REVIEW.md:5280`** — explicit recorded decision that the two
  contracts intentionally differ:
  > "`Read<TResult>()` использует уже принятый на raw-пути глагол `ProcedureResult.Read<TResult>()`;
  > контракт при этом **иной** (`BatchResult` — eager-буфер, **несовпадение типа не расходует
  > набор**, тогда как `ProcedureResult` — live-reader, **продвигающий курсор до маппинга**), на
  > именование это не влияет".
  State: implemented (audit of phase 4 #70, 2026-09-26).
- **`docs/specs/design/API-NAMING-REVIEW.md:5264`** — agreed decision rejecting a separate raw-path
  `ReadAll<T>`/`MultiResult`:
  > "`ReadAll<T>`/`MultiResult` — отдельный публичный API **на сыром пути** не заводится:
  > несколько result-set'ов читаются через общий helper навигации, извлечённый из существующего
  > `BatchRunner.AdvanceToResultSet` (фаза 0 #70, поглощает #25). ... терминалы стриминга остаются
  > только на `QueryCommand<T>`."
  > "**Уточнение (26.09.2026, фаза 4 #70):** отказ касался raw-пути; LINQ/батч-поверхность получила
  > явный терминал (`Execute`/`ExecuteAsync`), поэтому новый публичный читатель наборов
  > `BatchResult` введён".
- **`docs/specs/design/API-NAMING-REVIEW.md:5266-5284`** — phase-4 #70 audit: `BatchResult` is a
  "новый публичный `sealed` тип; eager, владеет только буферизованными наборами, поэтому **не**
  `IDisposable`" (`:5275`); `Read<TResult>()` "с проверкой проектируемого типа; выход за конец и
  несовпадение типа — `InvalidOperationException`" (`:5277`).
- **`docs/specs/design/code-smells-review.md:7960`** — open/deferred, not decided:
  > "**Deferred (цикл 4).** Стриминг multi-result: `BatchResult` буферизует все наборы (eager).
  > Триггер: запрос пользователя / #27-стиль."
- **`docs/specs/status/procedure-result-sets-1.md`** (PDCA #118 frozen contract, 2026-10-01) —
  agreed + implemented design for `ProcedureResult` one-shot traversal and the `ResultSet` cursor.
  `:10-12` public API `ReadSets`/`ReadSetsAsync`; `:14-20` `ResultSet` members; `:22-29` semantics;
  `:29` "Reuse `ResultSetNavigator` unchanged; ... do not alter existing `Read<T>`/`ReadAsync<T>`
  behavior or advance-before-map"; `:47-64` variant matrix V1-V18.
- **`docs/specs/comparison/linq2db-backlog-gap-analysis.md:170`** — multi-result "реализовано
  (2026-09-26) фазой 4 `todo_stored_procedures.md` (#70, включая #25): `BatchBuilder.AddQuery<TResult>`
  + `Execute`/`ExecuteAsync` → `BatchResult.Read<TResult>()`; на raw-пути навигация — **общий helper
  из `BatchRunner.AdvanceToResultSet`**." Also `:44-45`, `:58`, `:397`, `:419`, `:461`.
- **`docs/specs/roadmap/sql-capabilities-gap-analysis.md:409-412`** — "Phase 2 (several result sets
  from one command) is now **implemented** (2026-09-26) by phase 4 of `todo_stored_procedures.md`
  (#70, including #25): the batch surface exposes `BatchBuilder.AddQuery<TResult>` +
  `Execute`/`ExecuteAsync` → `BatchResult.Read<TResult>()` ...".
- **`docs/specs/roadmap/design-review-todos-2026-09-24.md:127-130`** — plans unified around #70
  "Фаза 0 — Основа": "владелец reader'а с отдельной `DbCommand` на вызов, **общий helper навигации
  result-set'ов**, мапперы произвольного `T`); #25 (несколько result-set'ов) слит в #70 фазу 4."
- **`docs/specs/release-1.0.8-b.md:27`** — "#70 | Хранимые процедуры и функции (вызов,
  output-параметры, несколько result-set)".
- **`docs/specs/release-1.0.7-b.md:26`** — "#15 | `OUTPUT INTO`, несколько result-set'ов,
  upsert-with-output".

State summary: the *shared low-level* navigation is an agreed design (helper extracted from
`BatchRunner.AdvanceToResultSet`); the *divergent public contracts* of `BatchResult` and
`ProcedureResult` are explicitly acknowledged (`API-NAMING-REVIEW.md:5280`); `BatchResult` streaming
is deferred with trigger (`code-smells-review.md:7960`). No spec found that mandates identical
semantics between the two result types or proposes unifying them.

### 5. Tests pinning the semantics

**`BatchResult` (eager, type-checked):**

- `tests/nextorm.sqlite.tests/BatchMultipleResultTests.cs` (real SQLite temp DB):
  `Execute_MultipleResultQueries_ShouldMaterialiseEverySetInOrder` `:49`;
  `ExecuteAsync_MultipleResultQueries_...` `:71`;
  `Execute_ZeroRowResultSetFollowedByNonEmpty_...` `:93`;
  `Execute_MixedTypeResultSets_ShouldReadEachSetWithItsOwnType` `:115`;
  `Read_WithWrongTypeInSecondPosition_ShouldThrowAndNotAdvance` `:137`;
  `Read_WithWrongType_ShouldThrow` `:163`; `Read_PastLastResultSet_ShouldThrow` `:184`;
  `AddQuery_ThenMutation_ShouldThrow` `:207`; `AddQuery_ThenQuery_ShouldThrow` `:227`;
  `Execute_WithoutResultQuery_ShouldThrow` `:247`.
- `tests/nextorm.integration.tests/CommonTestSuite.BatchMultipleResults.cs` (per provider,
  `Assert.SkipUnless(Provider.SupportsBatch, ...)`):
  `Batch_MultipleResults_ShouldMaterialiseEverySetInOrder` `:9`;
  `Batch_MultipleResultsAsync_...` `:33`; `Batch_SideEffectThenMultipleResults_...` `:57`.
  Runs on every provider whose `SupportsBatch` is true — PostgreSQL, SQL Server, MySQL/MariaDB,
  SQLite; ClickHouse and in-memory skip.

**`ProcedureResult` (streaming / cursor / outputs):**

- `tests/nextorm.sqlite.tests/RawCommandTests.cs` (real SQLite temp DB):
  `MultipleResultSets_ReadSequentially_ThenThrows` `:250`;
  `MultipleResultSets_ReadSequentiallyAsync_ThenThrows` `:271`;
  `DdlThenSelect_AdvancesPastEmptyResultSet` `:666`; `DdlThenSelectAsync_...` `:684`;
  `EntityRead_NoMatchingColumns_Throws` `:230`; `UnsupportedResultType_ThrowsNotSupported` `:351`.
- `tests/nextorm.sqlite.tests/RawResultSetTraversalTests.cs` (real SQLite temp DB; 24 traversal
  tests per status doc): `ReadSets_YieldsSetsWithIndexFieldCountAndStableColumnNames` `:27`;
  `ReadSets_SkipsLeadingAndTrailingColumnLessSets_WithoutCountingThem` `:61`;
  `ReadSets_ZeroRowColumnBearingSet_IsStillYielded` `:88`; `ReadSets_NoColumnBearingSets_IsEmpty` `:117`;
  `ReadSets_UnreadRowsOfCurrentSet_AreAutoSkippedOnAdvance` `:134`;
  `ReadSets_CursorFromAdvancedSet_IsStale` `:155`; `ReadSets_SecondReadOfSameSet_Throws` `:179`;
  `ReadSets_AfterLegacyRead_Throws` `:203`; `LegacyRead_AfterReadSets_Throws` `:223`;
  `ReadSets_ReEnumeration_Throws` `:245`;
  `ReadSets_OuterEnumeratorDisposal_...` `:266`;
  `ReadSets_OutputsDuringActiveTraversal_AreRejected` `:294`;
  `DefaultResultSetCursor_Read_Throws` `:320`;
  `ReadSetsAsync_YieldsSetsAndLazyReadAsync` `:328`;
  `ReadSets_OwnerDisposed_CursorRead_ThrowsObjectDisposed` `:585`;
  `ReadSetsAsync_OwnerDisposed_CursorReadAsync_...` `:609`;
  `ReadSetsAsync_Cancelled_RestartForbidden` `:641`.
- `tests/nextorm.integration.tests/CommonTestSuite.Raw.cs` (per provider, `SupportsBatch` skip):
  `ExecuteRaw_MultipleResultSets_ShouldReadSequentially` `:127`;
  `ExecuteRaw_MultipleResultSets_ReadSets_HeterogeneousPerSetTypes_ShouldReadInOrder` `:140`;
  `ExecuteRawAsync_MultipleResultSets_ReadSetsAsync_HeterogeneousPerSetTypes_...` `:166`;
  `..._ReadSets_SkipsLeadingIntermediateAndTrailingColumnLessSets` `:196`;
  `..._ReadSets_CursorFromAdvancedSet_IsStale` `:224`;
  `..._ReadSets_ColumnNamesRemainStableAfterOuterAdvance` `:243`;
  `..._ReadSets_UnreadRowsAreAutoSkippedAndNextSetStaysIntact` `:263`;
  `..._ReadSets_OutputsRemainReadableAfterExhaustion` `:279`;
  `..._ReadSets_WhenOutputsReadFirst_TraversalThrows` `:301`;
  `..._ReadSets_EarlyBreak_KeepsOwnerUsable` `:318`.
- Provider-specific:
  `tests/nextorm.integration.tests/SqlServerSpecificTests.cs`
  `ExecuteProcedure_MultipleResultSets_ShouldReadSequentially` `:738` (reads at `:749-750`);
  `Procedure_ReadSets_Heterogeneous_OutputsAfterExhaustion` `:759`;
  `Procedure_OutputsFirst_ReadSetsThrows` `:802`;
  `ExecuteRaw_OutputParametersWithUnreadResultSet_ShouldDiscardIt` `:979`.
  `tests/nextorm.integration.tests/MySqlSpecificTests.cs`
  `ExecuteProcedure_WithInAndOutputAndSelect_ShouldReadSetAndOutput` `:459`;
  `ExecuteProcedureAsync_WithInAndOutputAndSelect_ShouldReadSetAndOutput` `:488`.
- Core compile/ownership gate: `tests/nextorm.core.tests/ResultSetEnumeratorOwnershipTests.cs`
  (`ResultSetEnumerator`/reader ownership; the in-memory context cannot execute raw).

Provider coverage: SQLite tests use a real temp-file DB. `CommonTestSuite` variants run on all
`SupportsBatch` providers (PostgreSQL, SQL Server, MySQL/MariaDB, SQLite); ClickHouse/in-memory skip.
Stored-procedure cursor tests are provider-specific (SQL Server, MySQL).

### 6. Intentionally different semantics / GitHub issues

- Recorded as intentionally different: `docs/specs/design/API-NAMING-REVIEW.md:5280` (verbatim
  quote in §4) — "контракт при этом **иной** ... `BatchResult` — eager-буфер, несовпадение типа не
  расходует набор, тогда как `ProcedureResult` — live-reader, продвигающий курсор до маппинга".
- `docs/specs/design/code-smells-review.md:7960` records `BatchResult` streaming as deferred
  (trigger: user request / #27-style).
- GitHub `gh issue list --repo AlexeyShirshov/nextorm --search ... --state all` results:
  - `result set` / `resultset`: **#25** `CLOSED`, milestone `1.0.8-b` — "TODO: Multi-resultset
    support" (body: "Implement split queries with single network call"); **#118** `CLOSED`,
    milestone `1.0.9-b` — "Стриминг нескольких result set'ов в ProcedureResult (гетерогенный курсор
    на набор)"; **#150** `OPEN`, milestone `1.0.9-rc1` — "ClickHouse native extreme-row parity for
    float/double keys" (unrelated); **#15** `CLOSED`, `1.0-b.1` — "TODO: `OUTPUT INTO`, несколько
    result-set'ов, upsert-with-output"; **#63**, **#75**, **#119**, **#146** (unrelated/batch context).
  - `ReadSets`: only **#118** (`CLOSED`, `1.0.9-b`).
  - `BatchResult`: only **#70** (`CLOSED`, `1.0.8-b`) — "TODO: хранимые процедуры и функции (вызов,
    output-параметры, несколько result-set)".
  - `ProcedureResult`: **#118**, **#117** (`CLOSED`, `1.0.9-b`), **#70** (`CLOSED`, `1.0.8-b`),
    **#119** (`CLOSED`, `1.0.9-b`).
  - `unify result`: **empty**. `унифицировать`: only **#180** `OPEN`, `1.0.9-rc2` — JSON streaming
    conversion details (unrelated).
- **No GitHub issue about unifying `BatchResult` and `ProcedureResult` (or their result-set
  semantics) was found.** The related issues are all closed and shipped the two surfaces separately
  (#25/#70/#15 for batch/raw multi-result; #118 for `ProcedureResult` cursors).

## Not found

- No shared base class, interface or common surface type for `BatchResult` and `ProcedureResult`
  (only the static `ResultSetNavigator` low-level helper).
- No `ReadSets`/`ReadSetsAsync`/cursor/`Index`/`FieldCount`/async read on `BatchResult`.
- No `ResultSetCount` on `ProcedureResult`; no `IDisposable` on `BatchResult`.
- No spec mandating identical semantics, and no open GitHub issue proposing unification.
- The referenced plan files `docs/specs/**/todo_stored_procedures.md` and
  `docs/specs/**/todo_output_into.md` are absent from the current tree (glob `docs/specs/**/todo_*.md`
  lists only `todo_hint-followups`, `todo_sharding`, `todo_interface_poco`, `todo_csv_streaming`,
  `todo_clickhouse_aggregate_function_state`, `todo_public_api_freeze`, `todo_json_streaming`,
  `todo_navigation_properties`, `todo_mapping_scope`); their content survives only in the review /
  status / gap-analysis docs cited above.

## Open questions

1. The user's claim "guide 23 and guide 12 are NOT unified": is the intended target the *doc prose*
   (different sections, different member tables) or the *runtime contracts*? The evidence shows the
   runtime contracts are explicitly recorded as intentionally different
   (`API-NAMING-REVIEW.md:5280`), not an accidental doc drift.
2. "Result-set handling must have identical semantics" — no spec or issue currently states this;
   the only recorded decision is divergence. Which semantics is the desired single one (batch's
   eager type-checked model, procedure's live streaming/mapper model, or a new shared abstraction)?
3. Is `BatchResult` streaming (deferred in `code-smells-review.md:7960`, trigger "user request /
   #27-style") part of the unification intent, or out of scope?
4. Should `BatchResult` gain a cursor/`ReadSets`-style surface and `ProcedureResult` a
   `ResultSetCount`-style member, or should the guides merely be reconciled (cross-links, one
   canonical section)?
5. Are the closed issues #25/#70/#118 to be reopened/linked, or is a new issue required for a
   unification decision? (No such issue exists today.)
