# issues-65-69 — pending-PLAN clarification r1 — source: pdca-planner

# Clarification / Correction Addendum to Pending PLAN (`r = 1`, `n = 1/3` — unchanged)

This is a **same-pending-PLAN clarification** (`references/plan.md` §Step boundary, lines 261–263), **not** a reclassification and **not** a revision:
- **Counters preserved:** `r = 1`, `n = 1/3`.
- **Form and overlay preserved:** `incremental`, `coder+.NET` (`nextorm-pdca`).
- **Original task and all original acceptance criteria preserved verbatim** by reference to the existing `r = 1` plan, with no additions, removals, weakenings, or reformulations.

---

## 1. Superseded Assumptions (Withdrawn)

The following unsupported statements from the immediately preceding addendum are **withdrawn and superseded** by the authoritative scout facts:
1. **Withdrawn:** Any claim that `MappingScope` is already partially implemented in C# source.
2. **Withdrawn:** Any invented proxy metric or numeric floor/threshold such as *"baseline criterion count = 0/7"* or *"minimum delta = +1 criterion per step"*. Under `references/plan.md` (§Numeric thresholds and calibration, lines 180–183, 197–198), *"the planner invents neither a metric nor a proxy"* and *"a metric or floor is a specialisation for measurable goals, not a universal gate."*

---

## 2. Authoritative Current-State Facts (Comparable Baseline)

The read-only scout gather establishes the exact current state of the repository at the anchored snapshot:
- **No `MappingScope` implementation or tests exist:** `MappingScope` is planned only in the roadmap (`docs/specs/roadmap/todo_mapping_scope.md:2,10-18`).
- **Type-only plan/from cache:** `QueryPlanner._fromCache` is a static `ConcurrentDictionary<Type, FromExpression>` keyed by entity `Type` alone, with no scope component (`src/nextorm.core/DataContext/QueryPlanner.cs:1140-1163`).
- **Existing schema/table qualification scope:** Schema/table qualification exists only for explicit query overrides and bulk insert (`src/nextorm.core/Builders/EntityBuilder.cs:5423-5428`; `src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs:831`; `src/nextorm.core/DataContext/BulkInsertDestination.cs:71,112`).
- **No sharding implementation exists:** There are no C# matches in the solution for `AcrossShards`, `ShardKeyAttribute`, `ConsistentHashRing`, `ShardRouter`, `ShardTopology`, or `ResolveShardScope`; sharding is planned only in the roadmap (`docs/specs/roadmap/todo_sharding.md:13-17,40-48,103-113,141-148`).

---

## 3. Reconsidered Comparable Baseline & Mandatory Campaign Recipe

### Why `goal cannot be measured under the available means` does not apply
- Under `references/plan.md` (lines 180–188, 197–198), a numeric metric/floor is required only when the goal itself is a numeric measurement; for a software implementation campaign governed by discrete behavioural/architectural acceptance criteria, no proxy metric or numeric threshold may be invented.
- The available means (the codebase state and build/test verification of the unchanged acceptance criteria) cleanly distinguish the current state (neither `MappingScope` nor sharding types implemented; `_fromCache` keyed only by `Type`) from the target state required by the original acceptance criteria.

### Comparable Basis (Without Proxy Metrics or Invented Thresholds)
- **Basis of comparison:** Direct evaluation of the unchanged acceptance criteria set against the anchored repository state (presence and behavioural conformance of the required `MappingScope`, scoped cache/qualification, and sharding capabilities under the existing solution constraints), plus absence of regressions in existing functionality.
- **No proxy metric or numeric floor:** No numeric count, ratio, or forecast share is defined or used as a gate.

### Mandatory Incremental Campaign Recipe (`references/plan.md` §Entry and first step & §Step boundary)
1. **Accepted Baseline (Measurement First — `references/plan.md:152-155, 251-254`):**
   - Per `references/plan.md:152-155`, an incremental campaign opens from the current state established on the comparable basis, and per `references/plan.md:251-254`, *"no change-producing step is opened before an accepted baseline."*
   - The authoritative scout pointers above (`docs/specs/roadmap/todo_mapping_scope.md:2,10-18`, `src/nextorm.core/DataContext/QueryPlanner.cs:1140-1163`, `EntityBuilder.cs:5423-5428`, `SqlSourceRenderer.cs:831`, `BulkInsertDestination.cs:71,112`, `docs/specs/roadmap/todo_sharding.md:13-17,40-48,103-113,141-148`) establish the pre-change baseline state (zero `MappingScope` and sharding types implemented; `_fromCache` keyed by `Type` only; qualification limited to explicit overrides and `BulkInsertDestination`).
   - If the campaign driver books an explicit baseline step (`step-01`) to record and CHECK this current state before opening a change-producing step, that step is a pure read-only current-state verification (no product edits) accepted as a **knowledge increment** (`references/plan.md:251-254`). Once the baseline is accepted (or where the driver books the scout-established state directly under `references/plan.md:153-154`), the first change-producing step begins from that baseline.
2. **First Change Increment (Entry Scope):**
   - Because sharding (`ResolveShardScope`, shard-scoped table/schema routing, and cross-shard execution) depends on `MappingScope` and scope-aware query/cache resolution (`QueryPlanner._fromCache` and SQL source qualification), the first change-producing increment targets the **foundational `MappingScope` & scope-aware metadata/cache/qualification contract** in `nextorm.core` (whose concrete design/caching representation is validated in the first change step before dependent sharding routing/execution steps are opened).
3. **Step Boundary Progression (`references/plan.md:237-260`):**
   - Each subsequent step is opened **only** after an independent CHECK accepts the preceding increment (as a result increment or a confirmed knowledge increment).
   - Later sharding increments remain conditional on the accepted `MappingScope` increment rather than a pre-frozen unit backlog.
   - Campaign completion (`done`) requires a whole-goal CHECK confirming that **all original acceptance criteria (verbatim)** are satisfied simultaneously with no regressions.
