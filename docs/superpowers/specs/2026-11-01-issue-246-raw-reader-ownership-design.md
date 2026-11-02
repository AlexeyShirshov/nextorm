# Issue #246 — raw-reader ownership and preparation reuse

**GitHub:** [#246 — Perf: ToDataReader raw reader is slower than linq2db](https://github.com/AlexeyShirshov/nextorm/issues/246)  
**Milestone:** `1.0.9-rc5`  
**Status:** scope/design agreed in chat; independent builder check completed; written design approved by user 2026-11-01; implementation plan not written or approved; execution not authorized.  
**Implementation plan:** none exists.

## 1. Intent

Decouple reuse of immutable ToDataReader descriptor metadata from ownership of each execution. Exact-once execution is compatible with caching preparation metadata: it does not require retaining or reusing a mutable command. Internal `PreparedCommandCache.None` is the execution command ownership policy; it does not itself enable or disable the new descriptor partition.

This is an ownership/lifetime design, not a claim that the issue's historical performance data proves a current bottleneck. Architecture acceptance is not performance acceptance and never authorizes closing #246. Historic performance claims and harness data are not current proof. Routing is precisely: `LobLocatorColumn != null` uses `OpenResultReader` (SQLite rowid); `null` uses `OpenLobReader`. Do not repeat the issue's stale routing theory as fact.

## 2. Goals, non-goals, and anti-goals

The limited contract is `ToDataReader`, synchronous and asynchronous. Existing normalization, preparation, filtering, and dynamic-shape resolution happen before the new reader-descriptor lookup, or existing prepared state is used. It covers all currently supported forms after this normal preparation, with safe uncached fallback wherever a sound key cannot be made; not every form is necessarily cacheable. No universal raw/pre-preparation shortcut, new providers, or lazy-temp-table support.

Only ToDataReader sync/async explicitly opt in, including where `OpenLobReader`, `PrepareLobCommand`, or `OpenResultReader` are shared seams. ToStream, ToTextReader, CSV, JSON, and other terminals retain their existing preparation/cache/ownership routes even if they share outputs or internals. No broad behavior switch inferred from dialect or sequential-access settings. Do not remove wrappers, pool resources, reuse connection-owned mutable commands, expose a naked provider reader, require `Prepare()`, mutate sticky `QueryCommand.Cache`, build a universal normalizer, add public API, alter performance thresholds, or promise a speedup.

## 3. Ownership model

Use `RawReaderDescriptor` as a working internal name, not an approved new public abstraction; `readerDescriptorIndex` is a working name for the new partition, not a final API/name requirement. This descriptor is immutable metadata describing SQL, parameter metadata, and safe binding rules. Its detached structural key is value-free. Per-call execution owns parameter buffers, a fresh command, reader, and the `CommandReaderOwner`/`CommandLease` that retains disposal responsibility.

Neither key nor descriptor may retain current values, closures, `DbParameter[]`, mutable `Parameter` objects with `Value`, a prepared `QueryCommand`, context, connection, reader, resources, or a caller-holding `QueryPlan` cache clone. A factory may create a command from immutable specs. Existing compiled helpers are usable only if proven not to capture call-specific objects. Intentional structural SQL literals and immutable constants differ from runtime values; if they affect SQL, they participate in identity.

`QueryPlan.CloneForCache` currently retains prepared expressions and `LookupPartitions` by reference. It is not a safe key or descriptor backing object. This design does not repair legacy caches.

## 4. Lookup and execution pipeline

After existing normalization/preparation, filtering, and dynamic-shape resolution (or using pre-existing prepared state), perform the new ToDataReader reader-descriptor lookup. This lookup does not bypass any of that existing preparation.

1. Attempt a safe detached key. Unknown or non-keyable constructs take the original uncached fresh-command route, without changing `command.Cache`.
2. On hit, retain a local descriptor reference; skip SQL text rendering and descriptor reconstruction only. Continue per-call value extraction/binding and command creation. A current prepared `QueryCommand` may be used execution-locally for parameter extraction, but is never retained by key/descriptor. Parameter-only render/extraction walks are allowed. No claim is made that this solves all preparation costs.
3. On miss, render normally; derive immutable metadata from SQL/specs/binding structure without retaining values; validate key and descriptor invariants; then publish.
4. Create a fresh `DbCommand` for every execution, including hits. Execute with per-call command policy `PreparedCommandCache.None`, independently of descriptor reuse.

Cache eligibility is not command ownership. If `queryCommand.Cache == false`, neither look up nor publish a new descriptor; use the existing uncached path and do not mutate the sticky flag. Descriptor reuse is independently gated by `queryCommand.Cache` and call eligibility; `PreparedCommandCache.None` does not override explicit user plan-cache opt-out. Reuse must not globally enable existing `storeInCache=true` or the connection-holder path. A key/capability mismatch falls back before opening a command. Actual query, provider, or extraction errors propagate normally; fallback must not hide them. Do not execute captured getters twice for hit validation or observably reorder evaluation. If reuse cannot preserve current evaluation behavior, use uncached fallback. Closures alone must not permanently exclude a query form. Fresh and already-prepared/reused `QueryCommand` instances are both in scope; prepared-state validity/invalidation follows existing rules. No per-instance memo is required, and no query instance is cached in the descriptor key/value. Lack of descriptor-cache eligibility does not reject either kind.

## 5. Key correctness

Identity for the new reader-descriptor partition must include normalized structure; result/terminal mode; reader-descriptor flavor; provider/dialect SQL-affecting configuration (including locator suppression and sequential behavior); resolved rendering settings (timeouts, naming, quoting, literals, converters, mappings, filter identity or generation); parameter signature, name/order/type, precision/scale/conversions; and dynamic collection shape where relevant.

Do not assume context type alone is sufficient or that existing `QueryPlan` covers flags. The existing raw shape/preparation recipe and its `CaptureRawShape`/`ApplyPreparationFrom`/`TryGetRaw`/`SetRaw` path, holder-liveness guards, shared cache contents, and metrics remain unchanged. The new ToDataReader-only reader-descriptor partition is beside existing `QueryPlanStore._rawIndex`; its independence and closure-eligibility rules apply only to that new partition. Never widen eligibility of the existing raw recipe or reuse its caller-holding `QueryPlan` as a detached key. A key cannot retain a context, connection, caller expression graph, or captured object. Validate structural equality on collision, not hashes alone. Metadata available only after rendering can be checked on a miss; any fast key mapping not provably stable uses the safe noncached route. Keying only on already-rendered SQL is not claimed to speed preparation or SQL rendering. Missing sound configuration identity means fallback. The concrete key representation and extraction adapter are builder choices, with an obligation not to omit any SQL-affecting dimension.

## 6. Lifetime and state transitions

Descriptor: cache-owned immutable metadata → local borrowed reference, which remains valid across eviction or `Clear`. Execution: create/bind/open → transfer to owner → close/dispose. Before transfer, acquisition/open/wrapper failure and cancellation release acquired resources exactly once. After transfer, the caller releases through the owner. Successful `Dispose`, `DisposeAsync`, and `Close` are idempotent, including mixed repeated calls. Mark wrapper/owner ownership released before attempting resource release. If reader disposal throws, command/lease cleanup is still attempted in `finally`, and later `Close`/`Dispose`/`DisposeAsync` do not retry or re-dispose resources already released. Preserve existing exception propagation/aggregation behavior; define no new aggregation policy for simultaneous reader and command cleanup failures. This is an exact-once invariant, not authorization for a common owner refactor.

Cancellation during reads preserves the existing disposal obligation; it does not silently auto-close. Context/connection ownership and provider concurrent-reader restrictions remain unchanged. Cache sharing adds no shared mutable command/parameter state and no same-plan lease gate; it does not promise that providers allow multiple active readers. Clear/eviction do not affect active resource ownership or dispose descriptors as resources. Do not alter LOB owner behavior or output-parameter close semantics.

## 7. Storage and lifecycle

Add a ToDataReader-only reader-descriptor partition beside the existing `QueryPlanStore._rawIndex`, sharing the existing thread-local/generation lifecycle and `Clear`, but independent of normalized-holder liveness and the `Values`/`ResetConnection` mechanism. It contains zero holders requiring `ResetConnection`. Existing raw recipe contents, behavior, and metrics are unchanged. `Clear` invalidates descriptor lookups according to existing lazy-generation semantics; a new publication must not cross a stale generation. Cross-thread cache sharing is not required; asynchronous execution may keep a local immutable descriptor across awaits.

**Architect calls (clarifications, not new user questions):** bound the new reader-descriptor partition; select its concrete capacity/eviction policy in the implementation plan. Honor explicit plan-cache opt-out (`queryCommand.Cache == false`). On disposal exceptions, ownership is marked released before resource release, cleanup is still attempted, and already-released resources are not retried. Memory pressure or key novelty degrades only to eviction/fallback, never rejection. Other caches remain unmodified. No generic cache framework or new public API.

## 8. Design-level acceptance and later verification

This is a design matrix, not a runnable test plan. Eventual verification must cover:

- Per-call ownership and exact-once release for normal completion, acquisition/open/wrapper failure, cancellation, disposal throwing, and mixed `Close`/`Dispose`/`DisposeAsync`; post-throw state and no-retry behavior as specified above, without a new simultaneous-failure aggregation policy.
- Miss/hit with changing current closure values; independent same-shape queries must not leak values. Separate identities for `IN` cardinality, lookup/filter, provider/configuration, reader mode, conversions, and structural key collisions.
- Unsupported forms still reject as before; supported but unsafe forms fall back. Weak-reference retention checks ensure key, descriptor, and delegates do not retain closures, context, command, or reader after call objects are released. Isolate variables/query handles so caller-owned references are not mistaken for leaks.
- `Clear`/eviction while a reader is open does not dispose it; later lookup uses a fresh generation. Rejected candidates are never published. No shared sticky cache-flag mutation. `queryCommand.Cache == false` performs neither descriptor lookup nor publication.
- Provider correctness for currently supported SQLite/Postgres/SqlServer, existing raw/LOB guards and regressions, and shared core tests. No-DB unit tests belong in core/provider projects; live-database tests only in integration. FluentAssertions only; .NET 10 warnings are errors. Before any eventual integration run, load `.opencode/skills/running-integration-tests/SKILL.md`.

No tests are run during this design task. Later performance work compares current baseline, candidate, direct provider, and linq2db in a matched session under the issue's unchanged two-tier policy, including empty and 10k cases. Do not relax thresholds. Architecture gives no speedup guarantee; design acceptance does not close the issue.

## 9. Decisions and handoff

**Already agreed:** limited `ToDataReader` sync/async scope; post-normalization/preparation lookup for all supported forms with safe fallback; no new providers or lazy-temp-table support; separated immutable descriptor/key and per-call ownership; a new partition beside the existing raw index, with no changes to the existing shared-raw recipe; active readers survive cache clear; other terminals unchanged. The builder check is complete; reviewer questions were resolved within this agreed scope and the clarifications below, with no new user question or scope change.

**Architect calls:** bounded reader-descriptor partition with capacity/eviction selected in the implementation plan; explicit plan-cache opt-out; exact-once post-throw disposal state. **Builder choices:** precise key representation and extraction adapter, subject to all identity and lifetime invariants above.

**Approval and handoff:** Written design approved by user 2026-11-01. Independent builder check completed. The implementation plan is not written or approved; execution is not authorized. The next permitted stage is separate implementation planning only; this approval authorizes neither implementation nor an autonomous cycle.

Tracking: issue `https://github.com/AlexeyShirshov/nextorm/issues/246`, milestone `1.0.9-rc5`; local design artifact `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-design.md`. Issue remains open; no tracking changes are requested. Scope/design was agreed in chat; independent builder check completed; written design approved by user 2026-11-01; implementation plan not written/approved; execution not authorized; next stage is separate implementation planning only.
