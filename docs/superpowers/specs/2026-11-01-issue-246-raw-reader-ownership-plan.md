# Issue #246 — implementation plan: ToDataReader reader-descriptor partition

**GitHub:** [#246 — Perf: ToDataReader raw reader is slower than linq2db](https://github.com/AlexeyShirshov/nextorm/issues/246)
**Milestone:** `1.0.9-rc5`
**Design:** `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-design.md` (approved by user 2026-11-01)
**Status:** implementation plan, written 2026-11-01; the three open decisions were resolved by the user on 2026-11-01 (§0.1). **S0 is authorized once the `#268` lane releases the working tree. S1–S3 remain unauthorized until S0 reports.** This document implements design §3–§7 and adds no scope.
**Precedence:** the design wins on every conflict. Where this plan uses a concrete name, that name is a builder choice the design delegates (design §3, §7, §9).

## 0.1 Decisions resolved (user, 2026-11-01)

1. **Gate 0 is a hard stop**: if the addressable slice (§2) is below the noise band, no product code is written; S0's measured verdict is recorded and the plan ends there.
2. **Store capacity 64**, LRU by monotonic tick, mirroring `TerminalPlanCache`; eviction degrades to fallback, never rejection.
3. **Sequencing**: `#246` work does not run concurrently with the `#268` lane in the same tree — S0 starts after `#268` releases the working tree (no concurrent builds, benchmark processes or container runs).

## 0. Contract of this plan

- Scope: `ToDataReader` sync/async only. No other terminal changes route or ownership.
- The design's ownership model is implemented literally: immutable detached descriptor + per-call fresh command/reader/owner.
- Architecture acceptance is not performance acceptance: completing this plan **does not close #246**.
- If the measured addressable slice is below the noise band, the plan stops at S0 with no product change (see §2).

## 1. Verified current state (file:line)

Terminal surface — core only, no provider overrides:

- `src/nextorm.core/Query/QueryCommandExtensions.cs:308,326` (sync), `:362,380` (async).
- Routing by locator: `:334-336` and `:388-390` —
  `Dialect.LobLocatorColumn is not null ? OpenResultReader : OpenLobReader`.
- Wrapper construction and pre-transfer failure release: `:337-345`, `:391-399`.

Ownership types (already exact-once; this plan does not modify them):

- `src/nextorm.core/DataContext/CommandReaderOwner.cs:18` — `internal sealed class : IDisposable, IAsyncDisposable`; `CloseReader :101-111`, `Dispose :114-144`, `DisposeAsync :148-177`; each copies the reader, nulls the field **before** release, releases command/lease in `finally`.
- `src/nextorm.core/DataContext/Cache/CommandLease.cs:31` — `internal struct` with `int _done`; `Fail :102-133`, `Dispose :136-174`, both `Interlocked.Exchange(ref _done, 1)` first.
- `src/nextorm.core/DataContext/LobDataReader.cs:20,24` — owns the `CommandReaderOwner`; `Close :124-130`, `Dispose :322-341`, `DisposeAsync :344-359`.

Preparation path actually taken today:

- `src/nextorm.core/DataContext/DataContext.cs:557` `PrepareLobCommand`, `:638` `PrepareResultCommand`; both call `GetPreparedQueryCommand(..., storeInCache: false, cache: PreparedCommandCache.None, ...)` at `:578` and `:662-670`.
- `src/nextorm.core/DataContext/QueryPlanner.cs:707-708` — `PreparedCommandCache.None` forces `storeInCache = false`.
- Consequence A: the whole raw-recipe block `:729-774` is skipped (its gate includes `storeInCache`), so `CaptureRawShape`/`TryGetRaw`/`SetRaw` never run on this path.
- Consequence B: the normal plan-cache block `:812-821` is skipped.
- Consequence C: `:776` `queryCommand.PrepareCommand(!storeInCache, ct)` runs **full preparation and SQL rendering on every call**.

Storage precedent:

- `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs:20` — `[ThreadStatic] Dictionary<int, List<RawCandidate>>? _rawIndex` (`:55-56`), `RawCandidate :43-48`, generation transition `:58-73`, `TryGetRaw :118-152` (bucket by `shape.Fingerprint`, `Shape.Matches`, holder liveness `:140`, generation recheck `:144`), `SetRaw :159-179` (**no capacity**), `Clear :181`.
- Bounded patterns in the codebase: `src/nextorm.core/Query/TerminalPlanCache.cs:22,65-108` (LRU, capacity 64); `src/nextorm.core/DataContext/ScalarArtifactCache.cs:116-172` (FIFO, 16); `src/nextorm.core/DataContext/MapperCache.cs:66-90` (4096 / 1024).

Raw types reused as *reference only, never as a key*:

- `QueryCommand.RawShape` `src/nextorm.core/Query/QueryPreparer.cs:3968`, `Fingerprint :4029`, `CaptureRawShape :4234`, `ApplyPreparationFrom :4713`.

Design-doc symbol corrections (the plan uses the real names):

- `QueryPlan.CloneForCache` does not exist; the clone is `QueryCommand.CloneForCache()` (`src/nextorm.core/Query/QueryCommand.Clone.cs:238`), called from `QueryPlan.GetCacheVersion()` (`src/nextorm.core/DataContext/Cache/QueryPlan.cs:148-156`).
- `LookupPartitions` is a `QueryCommand` field (`src/nextorm.core/Query/QueryCommand.cs:189`), copied by reference at `QueryCommand.Clone.cs:33` — not a `QueryPlan` member.

## 2. Gate 0 — attribution before any product code

The design states the historical numbers are not proof of a current bottleneck (design §1, §8). The recorded evidence also shows construction is not the bulk of the empty-arm gap (construction ceiling ≈2.78 KB against a 10.4 KB residual), so a descriptor that removes only SQL rendering may be too small to matter.

S0 must therefore answer, on the current tree, one question:

> How many bytes and nanoseconds of one `ToDataReader` call are the slice this design can remove — SQL text rendering and descriptor reconstruction only (design §4.2)?

Method: phase markers on both arms (empty and 10k), single process, pinned DB, `GC.GetAllocatedBytesForCurrentThread` blocks, existing `SqliteBenchmarkStreaming` harness extended **only in a scratch copy** if instrumentation would otherwise alter the product path. Report the slice in B and ns per call with its measurement error.

**Hard stop:** if the addressable slice is below the noise band, stop here, record the number, and do not implement S1–S3. That is a legitimate outcome, not a failure.

## 3. Stages

### S0 — attribution (no product code)
Deliverable: phase ledger + verdict "slice addressable / not addressable". Evidence `.pdca/T246/rv3/` (rv1/rv2 are read-only history).

### S1 — descriptor store, key, publication (no hit path)
- New `ReaderDescriptorStore` beside `QueryPlanStore`, sharing its generation lifecycle.
- New `RawReaderDescriptor` and `RawReaderKey`.
- Publication after a normal (miss) preparation, gated exactly as design §4 requires.
- No behaviour change yet: nothing reads the store, so any regression is a bug in publication gating only.
- Tests: key identity matrix, retention (weak references), opt-out, fallback, `Clear`/eviction.

### S2 — hit path for `OpenResultReader` (SQLite locator flavour)
- On hit: skip SQL text rendering and descriptor reconstruction; still extract parameter values per call, create a fresh `DbCommand`, execute, transfer ownership through the unchanged `CommandReaderOwner`.
- Tests: hit/miss equivalence on SQL text and rows; no sticky cache mutation; exact-once disposal on both paths.
- Revert point: S2 alone can be reverted without touching S1.

### S3 — hit path for `OpenLobReader`
- Same shape for the non-locator flavour (Postgres, SQL Server, non-rowid SQLite).
- Revert point: independent of S2.

### S4 — tests, invariants, gates
Full matrix of §8 plus build/test gates of §9.

### S5 — measurement and record
Two-tier comparison of §9 against the in-round baseline; record the residual honestly. No threshold change.

## 4. Types (concrete, all `internal`)

All names below are working names; the design §3 explicitly leaves them to the builder.

`RawReaderDescriptor` (immutable metadata):

- `string SqlText` — the rendered statement.
- `RawParameterSpec[] Parameters` — name, position, provider type, size, precision/scale, direction. **No values.**
- `RawReaderFlavor Flavor` — locator-suppressed vs LOB.
- `long Generation` — matches the store generation at publication.

Invariants (checked in a debug assertion and in tests): no closure, no `DbParameter`, no `QueryCommand`, no context/connection, no reader, no `QueryPlan`.

`RawReaderKey` (value-free, detached):

- structural shape fingerprint (normalized description, not rendered SQL),
- terminal mode (sync/async share one identity),
- reader flavor,
- dialect/provider identity + SQL-affecting configuration (locator suppression, sequential behaviour),
- rendering settings hash (timeouts, naming, quoting, literals, converters, mappings, filter identity or generation),
- parameter signature hash (name/order/type/precision/scale/conversions),
- dynamic collection shape digest where relevant.

Collision handling: structural equality on the hit, never hash equality alone.

`ReaderDescriptorStore`:

- `[ThreadStatic]` instance, capacity **64**, LRU by monotonic tick, eviction on insert when full (mirrors `TerminalPlanCache` capacity; eviction degrades to fallback, never rejection).
- `bool TryGet(in RawReaderKey key, out RawReaderDescriptor descriptor)` — drops the partition if its generation is stale, then compares structurally.
- `void TryPublish(in RawReaderKey key, RawReaderDescriptor descriptor)` — rejects stale generations and non-keyable forms; never throws to the caller.
- `void Clear()` — invalidates by generation; active readers are untouched because descriptors are not resources (design §6).

## 5. Hit/miss flow

Miss: today's path unchanged, then publish if keyable and eligible.

Hit: build the command from the descriptor's SQL text and parameter specs, bind current values through the existing extraction walk, execute, transfer ownership. No prepared `QueryCommand` is read from, or written to, the descriptor.

Gates, in this order:

1. `queryCommand.Cache == false` → no lookup, no publication, existing path (design §4).
2. key not constructible → existing uncached path, no mutation of `command.Cache`.
3. key capability mismatch → fall back **before** opening a command.
4. errors from query/provider/extraction → propagate; fallback must not swallow them.

## 6. Ownership invariants (unchanged)

Implementing the hit path must not alter any of the following; each is covered by a test in §8:

- acquisition/open/wrapper failure and cancellation release acquired resources exactly once, before ownership transfers (`CommandReaderOwner.cs:101-177`, `CommandLease.cs:102-174`);
- after transfer the caller releases through the owner; `Dispose`/`DisposeAsync`/`Close` are idempotent in any mix;
- ownership is marked released **before** attempting resource release; if reader disposal throws, command/lease cleanup is still attempted in `finally`, and later calls do not retry;
- existing exception propagation/aggregation behaviour is preserved — no new aggregation policy;
- `Clear`/eviction do not dispose an open reader; no shared mutable command/parameter state is introduced; no same-plan lease gate is added;
- LOB owner behaviour and output-parameter close semantics unchanged.

## 7. Placement of the hook

`DataContext.PrepareResultCommand` (`DataContext.cs:638`) and `DataContext.PrepareLobCommand` (`:557`) already receive the command, the parameters and the token, and already know the flavour. The lookup and the publication belong there, immediately around the existing `GetPreparedQueryCommand` call, so the `QueryPlanner` gates (`:707-708`, `:729-774`, `:812-821`) stay untouched and the existing raw recipe keeps its current eligibility.

## 8. Test matrix

Core (unit, no DB):

- `tests/nextorm.core.tests/Lob/LobDataReaderTests.cs` — extend with hit/miss disposal parity, mixed `Close`/`Dispose`/`DisposeAsync`, cancellation, throwing disposal.
- New `tests/nextorm.core.tests/Raw/ReaderDescriptorStoreTests.cs` — capacity/eviction, generation staleness, structural collision, weak-reference retention (key, descriptor and any delegates must not retain closures, context, command or reader after the call objects are released), opt-out leaves the store untouched.
- Key identity: same shape → same key; each of `IN` cardinality, filter identity, provider/config difference, reader mode, conversion, collection shape → different key; unsupported form → fallback.

SQLite (unit, file DB):

- `tests/nextorm.sqlite.tests/Raw/ToDataReaderSqliteTests.cs:18` — the whole existing file must stay green; add SQL-text equality across hit and miss, per-call command disposed exactly once on both paths (`:471-525` pattern), no sticky-flag mutation (`:220`, `:444`), failure paths (`:528`, `:556`, `:584`, `:621`), cancel-after-open (`:658`, `:684`).

Integration (live DB, fail-closed):

- `tests/nextorm.integration.tests/CommonTestSuite/Lob/CommonTestSuite.Lob.cs` — rows (`:798`, `:815`), context usable after dispose (`:832`, `:852`), cancelled-before-open (`:872`, `:889`), plus hit/miss in the same test where a second identical call happens.
- Providers: SQLite, PostgreSQL, SQL Server (the three with `SupportsSequentialAccess`; `LobLocatorColumn` only on SQLite). MySQL/ClickHouse stay on their existing paths and must not be perturbed.

Style: FluentAssertions only; `Assert.Skip*` only for skips; `.NET 10` warnings are errors.

## 9. Measurement protocol

Unchanged two-tier policy and thresholds from the issue. Canonical harness `Nextorm.Benchmark.SqliteBenchmarkStreaming` (`benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs:33`):

- 10k: `A_Nextorm_Prepared_ToDataReader` (`:184`), `B_Nextorm_ToDataReader` (`:192`), `Dapper_ToDataReader` (`:202`), `Linq2Db_ToDataReader` (`:210`), `Provider_Direct_ToDataReader` (`:245`).
- Empty: `B_RawReader_Empty` (`:217-261`).
- Command shape from `docs/specs/status/D246-raw-datareader-2.md:91-100`: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkStreaming*" --anyCategories <cats> --invocationCount <inv> --unrollFactor 1 --launchCount 3 --warmupCount 5 --iterationCount 10 --exporters json csv --artifacts <dir>`, `INV=64` for 10k, `INV=4096` for empty, `NEXTORM_BENCH_FULL=1`, `NEXTORM_BENCH_DB` pinned.
- Matched session: baseline and candidate in the same round, one process at a time, direct provider and linq2db arms visible.

## 10. Risks

1. **The key costs as much as the render it replaces.** This is the main risk; S0 exists to price it. If the key walk cannot be made cheaper than rendering, the form falls back and the plan yields nothing for it.
2. **`PrepareCommand` does more than render** (filters, conversions, select list, parameter list). The descriptor must carry every dimension that changes SQL or parameter shape; anything not representable falls back.
3. **Provider divergence**: locator (SQLite only), sequential access (SQLite/SQL Server/PostgreSQL), LOB vs result readers.
4. **Closure forms**: closures must not permanently exclude a query form (design §4) — the key is value-free, so a changing closure value is a hit with new parameter values.
5. **Accidental scope growth** into the shared raw recipe, other terminals, or a common owner refactor — all explicitly forbidden by the design.

## 11. Definition of done

Done means: S1–S4 merged (S2/S3 as far as the measured slice justifies), full gates green, evidence and residual recorded, status record `.pdca/status/T246-raw-datareader-4.md` updated.

Not done, and never implied by this plan: a speedup promise, a threshold change, or closing #246. Closing requires a separate performance acceptance under the issue's unchanged two-tier policy.
