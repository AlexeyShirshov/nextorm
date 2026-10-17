# Collection 1.0.9-rc3-open-3 — status

- collection-id: `1.0.9-rc3-open-3`
- slug: `collection-1.0.9-rc3-open-3`
- input: **open issues of milestone `1.0.9-rc3`**, re-read 2026-10-17 via `gh`: **#243, #244, #245, #246, #247**.
- base: branch `1.0.9-rc3` @ `957fcfe7` (owner snapshot: 2026-10-17 warm-path feature report, BDN artifacts, table-generator + CI step, cached-preparation WIP; build Debug 0W/0E, targeted suites green; pushed by explicit owner request).
- mode: **autonomous + auto-commit** (explicit owner request: «автономный pdca с автокомитам по открытым задачам rc3»). Push is **not** part of the collection; only the bootstrap snapshot was pushed on explicit request.
- phase: **P — bootstrap done; own PLAN per task not started**.

## Tasks

| id | issue | title | variant | status | plan_state | cycle N | r | rv | status file | baseline ref |
|----|-------|-------|---------|--------|-----------|---|--|----|-------------|--------------|
| T243 | #243 | Perf: warm-path Count/Sum allocations exceed linq2db | pdca-dotnet | pending | pending | — | — | — | — | 957fcfe7 |
| T244 | #244 | Perf: warm CTE/recursive CTE allocations exceed linq2db (#240 follow-up) | pdca-dotnet | pending | pending | — | — | — | — | 957fcfe7 |
| T245 | #245 | Perf: warm EXCEPT/INTERSECT allocations exceed linq2db (#240 follow-up) | pdca-dotnet | pending | pending | — | — | — | — | 957fcfe7 |
| T246 | #246 | Perf: ToDataReader raw reader is slower than linq2db | pdca-dotnet | pending | pending | — | — | — | — | 957fcfe7 |
| T247 | #247 | Perf: JSON streaming is slower than linq2db at 10k rows | pdca-dotnet | pending | pending | — | — | — | — | 957fcfe7 |

## Groups (allocated only after ALL-barrier)

| group | tasks (topological order) | worktree | branch/ref | status |
|-------|---------------------------|----------|------------|--------|
| — | — | — | — | not allocated (ALL-barrier not passed) |

## Phase decisions

- **P.1 Bootstrap (2026-10-17):** scope = the 5 open issues of milestone `1.0.9-rc3`. Variant by language rule: all C#/.NET → `pdca-dotnet`. Input snapshot committed as `957fcfe7` (owner decision: «закомитить запушить»).
- **Known cross-task constraints (input to P.2/P.4, not a schedule):**
  - All five are **warm-path allocation/time** tasks measured with BenchmarkDotNet on this single host; concurrent BDN runs invalidate each other's numbers (ShortRun noise band ±20 %). PLAN must state how its measurement is isolated.
  - Likely shared footprint: `benchmarks/nextorm.benchmark/**` (scenario/decomposition benches), `eng/perf/**` (gate + budgets), `docs/comparisons/feature-benchmarks-2026-10-17.md`, `docs/specs/performance/**` — the planner decides whether that merges the tasks into one connected component.
  - Uncommitted WIP from the owner is **now part of the baseline** (`ShapeValuesStable`/`HasEverPrepared`, thread-static visited sets, InValues stability). #244 item 2 (per-call `HashSet` churn) may already be partially addressed by it — PLAN must check, not assume.
- **P.2 Own PLAN per task — pending.**
- **P.3 ALL-barrier — not passed.**
- **P.4 Clustering — not started.**

## Lane log

- 2026-10-17 | bootstrap | owner snapshot committed and pushed as `957fcfe7`; collection `1.0.9-rc3-open-3` created with 5 tasks.

## General verification & recovery

- state: **`unverified`** (no C yet).
- last C result / evidence: none.
- defect id / history: none.
- correction-task status file: none.
- next allowed step: **P.2 — own PLAN for T243..T247, then ALL-barrier.**

## Done / Verified / Incomplete

- **Done:** none.
- **Verified:** none.
- **Incomplete:** none.
