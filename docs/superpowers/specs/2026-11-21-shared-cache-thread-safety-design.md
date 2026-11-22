# Shared plan-cache thread safety

- GitHub issue: [#16](https://github.com/AlexeyShirshov/nextorm/issues/16)
- Issue URL: `https://github.com/AlexeyShirshov/nextorm/issues/16`
- Milestone: `1.0.12`
- Date: 2026-11-21
- Design path: `docs/superpowers/specs/2026-11-21-shared-cache-thread-safety-design.md`
- Issue state: OPEN
- Status: the global-compatible implicit L1 approach and cold-miss semantics are agreed. The fresh builder-check is complete. The written design was approved by the user on 2026-11-21. The next artefact is an implementation plan, not yet written. No production implementation is authorized or started. Design approval does not decide delivery of #16 in `1.0.12` or change the milestone.

## Decision and goal

Use a process-wide, compatibility-keyed L1 for ordinarily cache-eligible implicit SQL plans. Compatible contexts and requests should reuse a plan across independent `DataContext` instances and physical thread hops. The goal is to maximize compatible reuse—not merely to avoid wrong results. A cache miss must not be caused solely by thread identity, connection identity, or an ordinary captured scalar value.

Keep mutable invocation binding separate from the shared plan, and keep execution command state in L2 per prepared object and exact `DbConnection`. The shared plan may contain a mapper and a value-independent binding recipe, but not caller-specific parameter values or references to caller-owned state.

Cold semantics are agreed: racing misses may build multiple safe candidates; publication selects one canonical plan per compatible key. Subsequent warm calls and plan indices use that published winner until ordinary invalidation. Exactly-once construction, single-flight, and a lock around execution are not required. The builder may choose the private representation and synchronization mechanism while preserving these contracts; no construction-count SLA or additional performance floor is promised.

## Scope and compatibility

This design covers ordinary SQL paths that are eligible for implicit plan caching. A compatibility decision must reflect query shape and effective provider/dialect, mapping, and SQL/translation semantics. It must include inputs that alter shape—such as literalization, `IN` cardinality, or null handling when applicable—without partitioning otherwise compatible plans on an ordinary bound scalar value. Do not use a hash alone as proof of equivalence or treat arbitrary context types as interchangeable.

Preserve existing intentionally non-cacheable paths and explicit `Prepare` contracts and policies. Explicit prepared commands remain separate from and bypass the implicit store; existing `None` behavior is unchanged. Do not invent a cross-process cache or a TTL policy. Preserve ordinary invalidation and purge behavior. Cache clears and mapping/configuration changes while queries are active are outside scope; existing quiescent operations remain unchanged.

Do not add a hidden thread-local front or fallback that silently stops reuse for eligible shapes simply to pass tests. An optional local hot front or single-flight mechanism is neither required nor promised; it would need future evidence and must preserve this contract.

## Ownership and concurrency

| State | Ownership and required boundary |
|---|---|
| `DataContext`, its connection, commands, and private execution state | Not safe for overlapping operations. The caller must ensure only one operation is active on a context or on a connection, including when two contexts wrap the same connection. The library does not promise a general overlap guard, serialization, or fail-fast exception; existing narrow guards remain intact. |
| Implicit global L1 | May be reached by compatible independent contexts and requests, regardless of thread or `AsyncLocal` flow. Store only shareable plan/mapper data and value-independent binding recipes. Published plans, keys, indices, and their reachable graphs must be safe for concurrent warm reads and must not retain a request closure, context, connection, or mutable caller state through nested/referenced expressions or auxiliary indices. |
| Per-invocation binding | Each invocation supplies fresh values from its own call/closure. Do not write caller values into a shared plan or move request data into L2 as shared per-plan scratch. |
| Prepared execution / L2 | Keep command state per prepared object and exact `DbConnection`, not per physical thread. Do not share `DbCommand`, parameters, or readers across connections. Preserve per-connection command reuse according to existing policy, including `None`; create and dispose a live reader for each execution. |
| Other shared stores | Audit actual ownership and reachability, including metadata, provider/static stores, their published values, and stored object graphs. Keep naturally local state local and fix only paths shown unsafe. |

`AsyncLocal` can propagate a reference but does not synchronize it. Sequential use across `await` may resume on another physical thread; it must not lose eligible L1 reuse for that reason. A stream remains an active operation until it is exhausted or disposed.

## Cold publication and warm reuse

Concurrent cold misses may construct more than one candidate for the same key. Publication must leave one canonical compatible entry, and future warm calls and associated indices must use that entry until normal invalidation. Losing candidates must not remain retained or leak resources. The design does not prescribe a per-key locking strategy, a whole-cache creation lock, or an exact construction count. Do not lock whole query execution.

Prove warm reuse with real plan-build counters and/or identity evidence while callers use different values and still receive their own results. Canonical L1 plan identity does not imply a shared database command: each connection retains its own prepared command state.

## Rejected alternatives and historical context

- **Thread-static L1 alone:** rejected for the agreed reuse goal because thread identity can partition otherwise compatible requests and thread hops.
- **Naive shared mutable holder:** rejected. In the probe-only B0 experiment at revision `9c09a635`, one public `ToList()` overlap returned row `[2]` to target 1, and the ADO.NET interceptor observed parameter `2` on both connections; the TLS arm returned `[1]`/`[2]`. The trace passed through shared holder-owned `DbCommandParams` refresh before copying values to per-connection commands (`QueryPlanner.RefreshParameters`, `ParamRefreshRecipe`, `DbPreparedQueryCommand.Bind`). This is evidence about that experimental B0 path, not a shipping TLS regression or a claim about an exact root field. The selected design separates invocation binding from the shared plan.
- **Exactly-once/single-flight or a global execution lock:** not required and not part of the contract. Optional mechanisms may be revisited only if evidence warrants them.

Historical source note: the current implicit store used `[ThreadStatic]` at `src/nextorm.core/DataContext/Cache/Plans/QueryPlanStore.cs:31–35,70–84`. `4f58c37d729` extracted the already-thread-static `_queryPlanCache` from `DbContext`; `7c0df1b07` changed invalidation. The examined material establishes no global-to-TLS performance rationale. The earlier “global too expensive” hypothesis and the naive B0 no-go/research notes are historical evidence, not current conclusions for split+shared L1.

## Evidence and limits

All experiments below were throwaway probes based on revision `9c09a635`; they did not change shipping product code and are not proof of current-product performance or general production readiness.

- **Correctness gate:** A1 (TLS + split binding) and B1 (shared L1 + the same split binding) each verified 320 public SQLite calls over 16 synthetic shapes and eight distinct connections. Every call returned the expected row and actual captured ADO.NET parameter; warm holder-build delta was zero.
- An earlier split-binding gate separately passed 1,158 captured-WHERE public calls across four modes, with no row/parameter mismatches and zero warm-build delta. Its separate warmed-only timing probe (n=4; median aggregate wall ms [observed min–max]) measured 1 worker: A1 113.69 [111.36–119.99], B1 110.05 [109.30–131.02]; 4 workers: A1 127.29 [125.43–128.78], B1 135.05 [129.56–140.59]. It is not combined with the mixed cold/replay series below.
- **Mixed cold/replay workload:** each worker ran 1,000 cold operations on one SQLite context, then 1,000 replay operations on a second context (warm L1/new L2). Four workers used two contexts each: eight contexts/connections and 8,000 total calls. At four workers, the memory snapshot saw 64 A1 TLS holders versus 16 B1 shared holders. Across B1 four-worker timing samples, 21–60 actual cold holder builds produced 16 published forms; replay had no holder builds.
- **Whole-harness timing:** n=4 paired samples; medians and observed min–max, in milliseconds. At 1 worker / 2,000 calls, A1 173.8 [150.7–192.9] vs B1 189.7 [164.9–197.8]. At 4 workers / 8,000 calls, A1 230.2 [190.1–241.3] vs B1 193.1 [179.0–229.9], with B1 faster in 3/4 pairs. Median paired effects were +1.6% and −8.6%, respectively; each is the median of same-index `(B1/A1 − 1)` values, not a ratio of arm medians. These are descriptive only, not a general speed claim.
- **Retained managed memory:** paired p01–p04 estimate L1-attributable bytes as `AfterContextDrop − AfterL1Clear`. A1 was 394,872 bytes; B1 median was 112,648 bytes [104,624–118,272]. B1 was lower in 4/4 pairs by a median 282,224 bytes (~71.5%). This `GC.GetTotalMemory` estimate is not RSS or exact object size and had no separate no-op control.

The split-binding evidence supports feasibility of the agreed global-compatible L1 approach for the tested synthetic workload and showed less duplicate managed state in these samples. It does not establish a general performance advantage, safety for all mappings or plan forms, or safety for authored/nested/referenced-query closures and their GC lifetimes. The 16 tested shapes are not a universal query-shape audit; no numeric benchmark threshold follows from these results.

Local evidence pointers (internal only): `/tmp/opencode/nextorm-16-20261121T091910Z-2185/spike16/followup/report.md`; `/tmp/opencode/nextorm-16-20261121T091910Z-2185/spike16/split-binding/gate-B1.md`; `/tmp/opencode/nextorm-16-20261121T091910Z-2185/spike16/split-binding/timing-report.md`; `/tmp/opencode/nextorm-16-20261121T091910Z-2185/spike16/split-binding/mixed-cold-memory/timing-report.md` and `summary.csv`.

## Acceptance criteria for any later authorized implementation

### Audit boundary and exit

Start from NextORM-owned stores that are shared or could be shared by two independent contexts during normal preparation, execution, or lazy mapping/metadata use—not an unlimited search for every static field. Seed the audit with:

- `QueryPlanStore`'s primary map, raw/native indices, and cold publication candidates; the reachable `QueryPlan`/holder/key/recipe/mapper graphs, including referenced-query expressions.
- `DataContextCache` metadata and the mapper, select-list, and expression stores reached through those paths; provider/static caches used on those paths.
- Per-context `QueryCache` and terminal commands, plus the holder's per-connection `CommandState`, as scope controls. Do not assume every static store is safe or every instance store is local by type alone.

Include other NextORM-owned stores reached by these normal paths. Exclude database-driver internals, external application state, unrelated utilities, unused static fields, and any new SQL L1 for deliberately uncached paths.

Deliver one inventory row per store or cohesive store family: owner/scope; the path linking independent contexts or evidence of non-escape; concurrent reads/writes and publication; post-publication mutability of the stored values/graph; verdict; and source/evidence pointer. Classify a store as local only when sole ownership/non-escape is source-proven. Classify shared state as safe only when immutable after safe publication or synchronized for every relevant mutation and for the published values—not merely because its outer dictionary is concurrent. A demonstrated or credible race-prone mutable path needs a targeted regression or equivalent executed proof; do not require a separate test for every immutable type. Exit when every reachable shared path in this boundary is classified.

### Binding, retention, and execution proof

- Cover a simple captured scalar and each distinct binding/retention path among supported cache-eligible authored, nested, or referenced-query closure forms (for example, referenced predicates and projections when they are independent paths). Prove fresh closures rebind correctly across callers and that a still-warm L1 does not strongly retain a dropped caller context/closure. Use a bounded `WeakReference`/GC lifetime test with a temporary owner out of scope, or equivalent inspectable reachability evidence; keep the plan alive and do not use raw GC byte counts as the lifetime oracle. Never try to collect an application-owned closure while it remains independently rooted. If a form is already intentionally ineligible, cite that existing policy rather than silently omitting newly affected forms; do not require a Cartesian product of operators and dialects.
- Treat “actual ADO.NET parameters” as values/ordinals observed on the live command at `CommandExecuting` or an equivalent immediately-pre-execute interception point, paired with each caller's expected rows—not SQL text or internal candidate arrays. Independent-connection SQLite integration tests are sufficient for the common binder path. Add focused live-provider coverage only when a distinct provider binding/execution path is implicated or changed, in addition to the repository's ordinary mandatory integration regression gates. Do not add an every-provider telemetry matrix; dialect-generation tests do not substitute for live ADO evidence.
- Prove warm eligible L1 reuse across unrelated contexts and physical thread hops without an `AsyncLocal` link or a thread/connection/ordinary-value-only miss. Concurrent public calls with different values must return each caller's expected rows and actual command parameters; provider/dialect, mapping, and query-shape differences must not share incompatible plans, and mapping-specific results must remain correct and isolated.
- On a coordinated cold race, allow multiple safe builds but prove one canonical entry is published for future warm calls and indices until ordinary invalidation. Prove warm reuse with real counters/identity; losing candidates are not retained and do not leak. Do not require exactly-once/single-flight, a particular construction count, or a whole-cache/execution lock.
- Preserve L2 command/parameter isolation and reuse per prepared object and exact connection; fresh/disposed readers; `None` and explicit `Prepare`; sequential-across-`await`; existing narrow guards; and quiescent clear/invalidation, including rebuilding an eligible plan after a quiescent clear. Do not expect an exception for unsupported overlap on one context or connection.
- Align public English/Russian plan-cache and interceptor docs and run appropriate unit/live tests if implementation is later authorized. Evidence figures are not numeric release gates. This design authorizes neither an implementation plan nor product changes.
