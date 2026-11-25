# Two-level caching for explicit SQL batches

**Status:** Written spec approved 2026-11-25. The next artifact is the implementation plan, which has not been started. Implementation is not authorized and has not started.

**Tracking:** [#419 — Cache explicit SQL batches: static step plans and per-connection batch commands](https://github.com/AlexeyShirshov/nextorm/issues/419) (open), milestone [1.0.12](https://github.com/AlexeyShirshov/nextorm/milestone/10).

## Intent and scope

Repeated calls to the public [`ctx.CreateBatchBuilder()`](../../../src/nextorm.core/Builders/Bulk/BatchBuilder.cs#L46) API for one or more statements should avoid rerendering equivalent, independently reusable steps, even when the caller creates a new builder. On a warm connection, a compatible repeat should also avoid recreating the complete executable command object.

The scope includes single- and multiple-result batches, whether executed as a native batch or as a joined command. Neither cache stores results or data, and a hit never skips step execution. This design does not change implicit lazy-temp preparation or server plan caching.

## User-selected design

- **L1 — Static structural templates.** Like the existing ORM plan cache, L1 is static rather than builder-owned. An eligible independent step `A` shares a template between `[A, B]` and `[A, C]`. A step that uses a batch-produced temp table, output variable, or other batch-local state can reuse a template only within an equivalent complete ordered batch. It has no standalone cache entry; even a change to an unrelated trailing step prevents that batch-scoped hit. Eligibility depends on the step's context, not just its type or name.
- **L2 — Connection-owned executable objects.** L2 reuses the complete native `DbBatch`, including its child commands, or the single joined `DbCommand`. It does not cache a separately detachable command for `A`. Every successful checkout rebinds the current parameter values before execution.

## Rationale and alternatives

A builder-instance cache misses equivalent batches constructed with new builders. A static whole-batch-only L1 cache handles those repeats but loses reusable work for independent `A` when siblings change or move. The selected hybrid preserves that reuse while keeping dependent templates inside the context that gives them meaning. It does not attempt to find or cache a minimal dependency subgraph. L1 alone is insufficient because a template hit does not avoid rebuilding the executable object on a warm connection.

## Architect-proposed safeguards

### L1 template binding

L1 step templates describe logical local parameter slots and their occurrences, not physical names such as `b{stepIndex}`. For each complete ordered shape, map every occurrence, including repeated uses of a captured slot, deterministically to the batch's collision-free placeholder and statement layout. Bind from this invocation's steps and value sources. Separate closures remain distinct even when they capture variables with the same name; repeated occurrences of one slot still bind to that slot's current value. L1 retains neither the old closures or values nor strong references to connections or driver objects.

A change to a bindable value alone should hit on an otherwise eligible repeat. Preserve SQL literal specialization: if the required SQL changes, execute the correct specialized text. Changes in SQL literal or command text, placeholder count, order, type, or layout, or incompatible result shape, command properties, or execution route must never yield an incompatible L2 checkout. Specialize, rekey, or bypass reuse as needed; a structural L1 hit does not establish executable compatibility.

### L2 checkout and lifetime

The actual selected `DbConnection` owns checkout of the compatible complete executable. Native child commands belong to that executable's lifetime. Hold an exclusive lease through reader close, including streaming disposal or asynchronous completion. While the lease is outstanding, no other execution can reuse that mutable object. Preserve the provider's previously supported overlap behavior, for example through isolated uncached execution rather than aliasing a checked-out object or imposing a new global rejection.

Before running, refresh all bindings, the current transaction (including clearing a previous transaction when none applies), and all applicable execution settings. Failure, cancellation, and early stream exit must release the lease exactly once, discard unusable state, and leave neither stale bindings nor double disposal. Connection reset, eviction, cache clear, and context disposal follow existing connection and cache ownership contracts. Later checkouts must not return dead handles.

Ownership reference: `DbPreparedQueryCommand.cs:228–265,335–370,498–549`. These ranges inform the ownership review; they do not require copying assumptions that would be unsafe for batch checkout.

### Transparent opt-out

Caching is transparent to callers of `ctx.CreateBatchBuilder()`; it requires no new public knobs. Honor applicable existing cache disabling without mutating sticky or shared query flags. When caching is outright disabled, execute normally without reading or publishing either L1 or L2 entries. Disabling reuse is not an execution error.

Do not speculate on reuse for unsupported shapes. A step ineligible for standalone L1 reuse is distinct from an entire batch ineligible for L2; neither decision implies the other. The existing lazy-temp `PendingBatch` stays on its current opt-out path; explicit batches continue to support temp steps.

### Batch dependencies and opaque inputs

Standalone L1 reuse requires provable independence from batch-produced state. Known references to batch-produced names or scopes are dependent: this includes typed or named temporary tables and views, capture or output variables, and downstream steps that use them. Reading ordinary persisted rows written by an earlier step does not by itself make the reader's query plan batch-dependent. Every step still executes in order on every invocation.

Raw fragments and name-only sources can hide session state. Do not infer independence from a CLR step type, apparent SQL text, or a variable's spelling. For the ambiguous region, allow only batch-scoped reuse if the complete ordered batch, exact raw text, effective identifiers, execution route, parameter layout, and applicable lifetime together distinguish the executable well enough for safe reuse. Widen the ambiguity to the whole batch if its effects cannot be bounded. If even complete-batch identity, current rebinding, and the applicable lifetime cannot establish safety, bypass the affected caching rather than guess. This safeguard does not require a general SQL parser, a dependency-subgraph cache, or public hints.

## Proposed acceptance matrix

Exercise both native and joined executions on supported providers, covering one- and multiple-statement batches and single- and multiple-result shapes. Show cache behavior and database behavior separately: correct results alone do not establish reuse, and a reported hit alone does not establish correct execution.

| Scenario | Observable reuse | Observable database behavior |
| --- | --- | --- |
| Repeat an equivalent batch on the same warm connection with a new builder and a different bindable value. | The first call establishes the template and executable object; the repeat reuses the eligible L1 template without rerendering it and reuses the complete L2 object without recreating it. For native execution, this includes the child commands. | The repeat uses the new value and returns the matching results. |
| Change an independent sibling from `[A, B]` to `[A, C]`, with different physical command text or parameter layout. | `A` gets an independent L1 hit; the incompatible complete L2 object is not reused, and no detached command for `A` is checked out. | `A` binds correctly in the new batch, and `C` executes instead of `B`. |
| Shift independent `A` from `[A, B]` to `[B, A]`. Use separate closures in both steps with same-named captures, distinct current values, and repeated uses of a capture in `A`. | `A` gets an independent L1 hit at its new position. The ordered layout has no placeholder collisions and no incompatible L2 hit. | Each occurrence reads the correct current slot; the same-named captures do not alias, and statement and result order reflect the new batch. |
| Execute an equivalent batch on a different live connection with no matching L2 entry of its own. | An eligible static L1 entry can hit, but the second connection does not check out the first connection's executable object. | Execution uses the second connection and its current values. |
| Repeat `[produce temp, consume temp, B]`, then replace only the tail with `C`. | The exact repeat demonstrates a batch-scoped hit for the consumer. The changed tail misses that scoped entry, and the consumer has neither a standalone entry nor a standalone hit. | The consumer reads the temp state produced by the current execution, and the selected tail executes. |
| Execute `Raw` that creates a temp table, followed by a named read. Repeat with fresh bindable values, change only an unrelated tail, and then vary the raw definition and named projection to require a different schema. | The raw fragment and named read have no standalone hit. Exact repeats may reuse within the complete ordered batch only when identity, rebinding, and lifetime make it safe; otherwise bypass. A changed tail misses the old scoped entry, and the schema change never reuses an incompatible executable. | The named read sees the current temp schema and rows, with current parameter values and no stale data from prior runs. |
| Keep a provably independent normal read `A` over a persisted table while replacing an unrelated sibling. Change `A`'s bindable value and update the persisted rows between executions. | `A` gets a standalone L1 hit, without checking out an incompatible complete executable. | `A` returns fresh persisted rows for the current value and the correct result schema; the new sibling executes. |
| Change a value that requires different literal SQL. | No L2 checkout uses the old, incompatible physical command text. | Execution reflects the new literal. |
| Warm both caches, then repeat a batch with a measurable side effect and a read whose expected result changes. | Demonstrate actual template and executable-object reuse on the warm path. | No step or side effect is skipped. The side effect occurs again as requested, and the read returns fresh results rather than replaying prior rows. |
| Attempt an overlapping execution while a reader is outstanding, on a provider that supports overlap. | No execution aliases the leased object. Early disposal, cancellation, or failure releases the lease once and does not return unusable state. | Previously supported overlap remains supported; either execution sees only its own values. |
| Reuse a compatible executable with changed settings and transaction, including a repeat without a transaction after a transactional run. | Checkout refreshes the applicable execution settings, bindings, and transaction, including clearing the previous transaction. | Execution uses the current settings and transaction, not the previous run's state. |
| Execute a matching batch with caching disabled, including after warming both levels. | No cache lookup or publication at either level, and no mutation of shared query flags. | Normal execution with the current values; disabling caching is not an error. |

## Pending verification

- **Lifecycle:** Confirm provider constraints on checkout, return, and disposal of connection-owned executable objects, including active readers and failure paths.
- **Eligibility:** Confirm how existing batch inputs expose batch-local dependencies and SQL literal specialization for applying the stated policy.
