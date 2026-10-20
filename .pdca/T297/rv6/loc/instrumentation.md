# T297 r=6 P04a — bounded recipe decomposition: instrumentation + smoke

Diagnostic-only instrumentation of two throwaway probe trees. No measurement run, no product edits,
no commits. Both trees compile to `probe-a` / `probe-c` and never ship.

- A = `/tmp/opencode/t297-p02/A`, baseline `0d4d5df2`
- C = `/tmp/opencode/t297-p02/C`, flat-root candidate `777e9233`
- Scope gate: `python3 .pdca/scripts/validate_inner_loop.py brief .pdca/T297/rv5/test-scope.json`
  → **rc 0**, log `.pdca/T297/rv6/logs/test-scope-p04a.log`.

## Phase set

`T297PhaseProbe.PhaseCount` 32 → **47**. The 15 new phases (indices 32..46) extend the existing
phase set; every pre-existing phase (`planner_gate`, `planner_recipe`, `plan`,
`prepare_pipeline`, …) is unchanged, so `rcp_*` reconciles against `planner_recipe`.

| phase (index) | A `path:line` | C `path:line` | enclosing method |
|---|---|---|---|
| `rcp_capture` (32) | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:3948` | `.../QueryCommand.QueryPreparer.cs:3952` | `QueryCommand.CaptureRawShape(QueryCommand, int depth)` |
| `rcp_fingerprint` (33) | `QueryCommand.QueryPreparer.cs:4088` | `QueryCommand.QueryPreparer.cs:4092` | inside `CaptureRawShape` (the `Fingerprint(shape, cmd)` call) |
| `rcp_filter_scan` (34) | `src/nextorm.core/DataContext/QueryPlanner.cs:637` | `DataContext/QueryPlanner.cs:637` | `QueryPlanner.GetPreparedQueryCommand(...)`, recipe probe |
| `rcp_lookup` (35) | `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs:120` | `.../QueryPlanStore.cs:120` | `QueryPlanStore.TryGetRaw(...)` |
| `rcp_matches` (36) | `QueryCommand.QueryPreparer.cs:3751` | `QueryCommand.QueryPreparer.cs:3755` | `QueryCommand.RawShape.Matches(...)` |
| `rcp_transfer` (37) | `QueryCommand.QueryPreparer.cs:4430` | `QueryCommand.QueryPreparer.cs:4434` | `QueryCommand.ApplyPreparationFrom(...)` |
| `rcp_copyto` (38) | `QueryCommand.QueryPreparer.cs:4438` | `QueryCommand.QueryPreparer.cs:4442` | `ApplyPreparationFrom`, `clone.CopyTo(this, copyAll: true)` call |
| `rcp_copyto_members` (39) | `src/nextorm.core/Query/QueryCommand.Clone.cs:136` | `.../QueryCommand.Clone.cs:136` | `QueryCommand.CopyTo(...)`, the `if (copyAll)` member block |
| `rcp_transfer_edges` (40) | `QueryCommand.QueryPreparer.cs:4444` | `QueryCommand.QueryPreparer.cs:4452` | `ApplyPreparationFrom`, the four writes after `CopyTo` |
| `rcp_staged` (41) | `DataContext/QueryPlanner.cs:655` | `DataContext/QueryPlanner.cs:655` | recipe probe staged-key block (`GetOrCreatePlanKey` → outcome) |
| `rcp_qplan_ctor` (42) | `DataContext/Cache/QueryPlan.cs:43` | `.../QueryPlan.cs:43` | `QueryPlan(QueryCommand, string?)`, assignments before `ComputeHash` |
| `rcp_qplan_hash` (43) | `DataContext/Cache/QueryPlan.cs:50` | `.../QueryPlan.cs:50` | same ctor, the `ComputeHash` call |
| `rcp_equals1` (44) | `DataContext/QueryPlanner.cs:657` | `DataContext/QueryPlanner.cs:657` | staged block, `recipePlan.Equals(stagedKey)` |
| `rcp_equals2` (45) | `DataContext/QueryPlanner.cs:663` | `DataContext/QueryPlanner.cs:663` | staged block, `stagedKey.Equals(recipePlan)` (only when first is false) |
| `rcp_discard` (46) | `QueryCommand.QueryPreparer.cs:4467` | `QueryCommand.QueryPreparer.cs:4475` | `QueryCommand.DiscardStagedPreparation(...)` |

All line numbers are post-instrumentation. A↔C offsets are only `+4` in `QueryCommand.QueryPreparer.cs`
and `0` elsewhere.

## Boundary semantics (inclusive/exclusive)

- **Inclusive**: `rcp_capture`, `rcp_lookup`, `rcp_matches`, `rcp_transfer`, `rcp_staged`, and the
  method-level spans start before the first statement and end in `finally` after the last exit, so
  every early `return` is charged. They contain their nested leaf phases.
- **Nested (to be subtracted, never double-counted)**:
  - `rcp_fingerprint` ⊂ `rcp_capture`
  - `rcp_matches` ⊂ `rcp_lookup`
  - `rcp_copyto` and `rcp_transfer_edges` ⊂ `rcp_transfer`
  - `rcp_copyto_members` ⊂ `rcp_copyto` (only the `copyAll` branch of `CopyTo`)
  - `rcp_qplan_ctor`, `rcp_qplan_hash`, `rcp_equals1`, `rcp_equals2`, `rcp_discard` ⊂ `rcp_staged`
- **Top-level, mutually exclusive inside `planner_recipe`**: `rcp_capture`, `rcp_filter_scan`,
  `rcp_lookup`, `rcp_transfer`, `rcp_staged`. `planner_recipe` additionally covers the eligibility
  gate evaluation and `CaptureRawGraph`, which are intentionally not sub-timed.
- `rcp_filter_scan` replaces the `else if (HasApplicableFiltersInRawGraph(...))` condition with an
  equivalent `else { var has = ...; if (has) {...} }`; `rcp_staged` preserves the original
  short-circuit `A || B` order (equals2 executes only when equals1 is false).
- `rcp_qplan_ctor` excludes the hash; `rcp_qplan_hash` is the `ComputeHash` call only. The
  forced-hash test ctor is not instrumented (never on the production path).
- `rcp_copyto` is the transfer call site, so a `CopyTo(..., true)` reached from another clone path
  would not be charged to `rcp_copyto`; its `copyAll` member block is still charged to
  `rcp_copyto_members` (observed once per recursive arm, see smoke).

## Recursion handling

- `CaptureRawShape` recurses over union and CTE bodies (and captures a union body twice: once for the
  null check, once for the descriptor). The timer wraps the **recursive** overload, so `rcp_capture`
  accumulates the inclusive time of **every** invocation and `calls` counts every invocation
  (`1 per graph node`, not 1 per query). Nested levels are therefore present in the raw total and
  must be subtracted at analysis time; this is why the per-arm top-level sum can exceed
  `planner_recipe` for CTE arms.
- `RawShape.Matches` recurses through `NestedEqual`/`CtesEqual`; `rcp_matches` likewise accumulates
  every level and counts every call.
- `TryGetRaw`/`Matches`/`CaptureRawShape` are static here; `ApplyPreparationFrom` and the staged block
  are instance paths. All accumulators are plain `static long[]`/`long` fields; the probe is
  single-threaded by construction (one process, one pinned core) and allocates nothing on the hot
  path (only `Tick()`/`Add()` calls).

## Build

| project | command | rc | warnings | errors | log |
|---|---|---|---|---|---|
| probe-a | `dotnet build probe-a/probe-a.csproj -c Release` | 0 | 0 | 0 | `/tmp/opencode/t297-p02/p04/logs/build-probe-a.log` |
| probe-c | `dotnet build probe-c/probe-c.csproj -c Release` | 0 | 0 | 0 | `/tmp/opencode/t297-p02/p04/logs/build-probe-c.log` |

## Smoke (NOT evidence)

Commands (identical per binary; `iters 3 × 10 queries = 30 queries/arm`):

```bash
taskset -c 1 env NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db DOTNET_gcServer=0 \
  ./probe-a/bin/Release/net10.0/t297-probe-a --arm all --warmup 3 --iters 3
```

Both rc 0; all four arms produced a JSON line; verification passed
(`LeftJoin=10, Join4=3, Distinct=2, Case=3, ToUpper=1, Contains=1, InCaptured=2, InInline=2,
RowNumber=3, SumOver=3, Cte=2, RecursiveCte=5, Intersect=3, Except=7, Udf=1`).
Logs: `p04/logs/smoke-{a,c}.jsonl`, `p04/logs/smoke-{a,c}.err`.

Calls per query (A / C; identical in both trees):

| arm | capture | fingerprint | filter_scan | lookup | matches | transfer | copyto | copyto_members | transfer_edges | staged | qplan_ctor | qplan_hash | equals1 | equals2 | discard |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| cte | 2 | 2 | 1 | 1 | 2 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0 | 0 |
| recursive | 4 | 4 | 1 | 1 | 3 | 1 | 1 | 2 | 1 | 1 | 1 | 1 | 1 | 0 | 0 |
| inlist | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 1 | 0 | 0 | 0 |
| leftjoin | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0 | 0 |

Plausibility: `capture 1 per node` (cte root+body=2, recursive root+recursive body=4, plain=1);
`filter_scan 1`, `lookup 1` always; `matches ≥ 1` on every candidate bucket (0 on inlist because
`TryGetRaw` exits before `Matches` — no raw bucket, plain recipe miss); `copyto == staged ==
equals1 == 1` per recipe hit and `0` on the inlist miss; `equals2`/`discard` 0 (first `Equals`
always true in this smoke). Inlist still shows `qplan_ctor/hash == 1` from the ordinary plan-key
lookup at `QueryPlanner:728`, outside `rcp_staged`.

`planner_recipe` vs top-level sub-phase sum (ticks/query, A | C; inclusive raw values):

| arm | planner_recipe A | sum(top) A | planner_recipe C | sum(top) C | fs+lookup+transfer+staged A \| C |
|---|---|---|---|---|---|
| cte | 54603.0 | 64835.3 | 63070.9 | 74922.4 | 27732.1 \| 33220.0 |
| recursive | 63644.2 | 113281.3 | 70319.3 | 118559.4 | 21839.7 \| 26982.7 |
| inlist | 13797.8 | 13352.4 | 16253.4 | 15650.6 | 2117.2 \| 2385.7 |
| leftjoin | 33627.5 | 33120.4 | 38791.4 | 38119.2 | 19711.2 \| 25321.6 |

Inclusive top-level components (ticks/query): cte `capture 37103.2/41702.4`, `filter_scan
4408.9/4022.5`, `lookup 16395.0/16867.4`, `transfer 1940.6/6391.8`, `staged 4987.6/5938.3`;
recursive `capture 91441.6/91576.6`, `filter_scan 2179.0/2247.4`, `lookup 13699.3/14294.9`,
`transfer 1453.5/4652.9`, `staged 4507.8/5787.5`; inlist `capture 11235.2/13265.0`,
`filter_scan 1616.3/1714.7`, `lookup 500.9/671.0`; leftjoin `capture 13409.3/12797.6`,
`filter_scan 3154.6/3008.7`, `lookup 10308.0/10972.8`, `transfer 1707.9/5666.8`, `staged
4540.7/5673.3`.

Sanity: `filter_scan+lookup+transfer+staged ≤ planner_recipe` in every arm; the raw top-level sum
in the two CTE arms exceeds `planner_recipe` only because `capture` includes recursive levels
(capture is summed over calls, see Recursion handling). After subtracting the nested capture levels
at analysis time, the spans fit under `planner_recipe`; the residual is the un-timed eligibility
gate plus `CaptureRawGraph`.

Smoke is a wiring check only — no per-phase conclusions and no measurement are drawn from it.
