# M12 #3 — verified preparation reuse on the cached path

- Task: T201
- Issue: #201
- Milestone: 1.0.9
- Design revision: 2
- Status: proposed; separate written-design approval required
- Public API change: none
- Implementation-plan revision: r=2
- Execution evidence contract: rv1, finalized before first DO

## Approved goal

- Perf M12 #3 — parity of NextORM's cached path with LINQ to DB in time and allocations. Category B scenario: a fresh-fluent **non-prepared** NextORM query hitting the implicit plan-cache vs a **non-compiled** LINQ to DB query with a warm implicit cache. Time: per-scenario `cached NextORM / cached LINQ to DB <= 1.05` (5% approved); allocations: per-scenario `<= 1.00` (0→0); `Warm_Reused = 0 B` preserved.
- Categories: Cte, RecursiveCte, Except, Intersect, Distinct.
- `Prepare()` is a no-regression control only, not a goal; the public compiled/prepared API is not a route.
- Milestone stays **1.0.9**.

## Context, authorization and review order

The owner confirmed the M12 #3 trigger and authorized conversational design
coordination. The trigger is recorded at:
`docs/specs/performance/iteration-15-cached-path-results.md:47`.

The preceding design reserved Stage C for a separately reviewed written design:
`docs/specs/performance/iteration-15-cached-path-design.md:145-157`.

Authorization is not approval of this mechanism. Required order remains:

1. Conversational design coordination.
2. Write this specification.
3. Separate written-specification approval.
4. Detailed implementation-plan and execution-mode approval.
5. Implementation or benchmark instrumentation.

The previous design/plan candidate failed PLAN review. That is not a product
failure or a failed product CHECK attempt.

## What is established

### Preparation precedes normalized lookup

`QueryPlanner.cs:574-575` prepares an unprepared command before:

- `GetOrCreatePlanKey`, `:614`;
- `QueryPlanStore.TryGet`, `:615`.

`QueryCommand.Plan.cs:30-44` constructs the normalized key from the command,
including IN and PreWhere shape state.

`QueryPlan.ComputeHash` and `QueryPlanEqualityComparer.GetHashCodeCore` consume
preparation-derived fields:

- From `:391`, Result `:397`, Where `:441`, IN shape `:444`;
- Columns `:446`, Join `:449`, Sorting `:452`, Grouping `:462`;
- PreWhere shape `:524`, Union `:536`, ReferencedQueries `:539`;
- Ctes `:548`, Hints `:551`, Windows `:569`.

These hashes are assigned by preparation, refresh, copy or reset operations.
A fresh command therefore has no usable normalized key before preparation.

### Store ownership

`QueryPlanStore` is thread-local, generation-invalidated, and keyed by
`(Type ContextType, QueryPlan Plan)`:

- fields and ownership: `QueryPlanStore.cs:22-35`;
- generation handling: `:29,42,72`;
- TryGet/Set/Clear/Values: `:52,66,72,74-81`.

No raw-shape index currently exists. Values enumeration is used by connection
disposal/reset and tests, not by a raw lookup:
`DbConnectionManager.cs:273,299`.

### There is only one selected consumer

Nested CTE preparation does not own a QueryPlanStore entry. It prepares through
`QueryCommand.PrepareCommand` into `QueryPreparer.Prepare`, and folds its shape
into `CtesPlanHash`:

- `QueryCommand.QueryPreparer.cs:304-385,381-383`;
- `QueryCommand.Prepare.cs:17`.

The earlier claim of an independent nested-CTE cache consumer was incorrect.
This design removes it.

## Decision

Implement specialized preparation reuse for the existing top-level
`GetPreparedQueryCommand<TResult>` route.

The implementation consists of:

1. A pre-prepare raw-shape candidate index inside QueryPlanStore.
2. Immutable, value-free preparation data owned by an existing Entry.
3. Structural/domain verification against the current command.
4. Transactional reconstruction of the required normalized preparation state.
5. The existing normalized lookup and execution branch.

There is one top-level consumer. Nested commands are part of its verified
whole-graph preparation state, not a separate cache consumer.

Do not introduce a recipe service, interface, provider plugin, general matching
framework, second holder cache or independent lifetime owner. Keep specialized
data in the existing Entry/store and preparation implementation. New abstraction
or indirection requiring a second consumer is not authorized by this design.

## Alternatives

| Alternative | Benefit | Reason not selected |
|---|---|---|
| Local allocation cleanup | Small change | Leaves full preparation on every fresh cached invocation |
| Fingerprint-only lookup | Potentially cheap | A collision or unrepresented semantic distinction can select the wrong holder |
| Normalized-key lookup before preparation | Reuses existing store directly | Impossible for a fresh command: required normalized hashes do not yet exist |
| Independent nested-CTE owner | Could reuse fragments | No such owner exists; creating one adds scope and lifetime complexity |
| Public Prepare/compiled/reused queries | Faster existing route | Changes the approved Category B scenario |
| Process-wide raw cache | Wider reuse | Adds retention and invalidation scope without necessity |

The selected mechanism's performance is measured, not guaranteed.

## Pre-prepare lookup and owner validation

### Index location and domain

Add the raw candidate index beside the existing thread-local dictionary in
QueryPlanStore.

A candidate records:

- raw fingerprint and immutable raw structural descriptor;
- the existing owner's normalized Key;
- the owner's holder identity;
- its cache-generation domain;
- immutable preparation data and applicability/domain requirements.

It must not retain the raw command, DataContext, user closure, current parameter
values, token, reader, or mutable command-owned collection.

The fingerprint partitions candidates. Full verification decides compatibility.

The descriptor includes query graph topology, scope, result/projection types,
expression/member/method identity, SQL-affecting values and flags, bound
provider/converter/mapping identity, and literal/parameter distinctions.

Where a required identity cannot be obtained and proved before preparation,
that case is ineligible. Do not assume that context type alone proves every
mapping distinction.

### Publication

Before ordinary preparation mutates an eligible command, capture its raw
description. Publish preparation data only after:

1. ordinary preparation succeeds;
2. normalized key construction succeeds;
3. the ordinary cacheable holder is stored through Set.

Associate the candidate with that actual stored owner.

Do not publish on cancellation, preparation failure, storeInCache:false,
cache-disabled execution, an ineligible route, or a holder hit whose necessary
raw-to-normalized preparation data has not been established.

A cache hit without a recipe may use the original full-preparation path.
It is not permission to invent a recipe from incomplete state.

### Lookup before the fresh normalized key exists

A fresh command:

1. Enters the existing store's generation synchronization.
2. Computes its raw candidate fingerprint.
3. Reads only the corresponding candidate bucket.
4. Verifies its raw structure and applicability against the current graph.
5. Checks the candidate's historical normalized Key in the live normalized
   dictionary and verifies the same holder identity is still its owner.
6. Rechecks generation validity before recipe commit.

This uses a normalized key from the historical owner, not an unavailable
normalized key from the fresh command.

Do not enumerate Values to search for a compatible holder.

### Replacement and invalidation

Both dictionaries must be reset under the same existing generation transition.
Every raw lookup and publication uses that transition; Clear must not leave
a usable auxiliary alias.

If Set replaces a holder at the same normalized Key, an alias to the former
holder is rejected by holder-identity validation and may be removed lazily.

No per-entry eviction is assumed. Owner validation remains mandatory anyway.

All owner references remain thread-local. Do not share holders across threads
through immutable recipe data.

A Clear racing after a completed lookup has the same live-execution semantics
as the existing store; this design does not claim that Clear revokes already
obtained commands. A generation change observed before commit rejects the
candidate and uses the ordinary path.

## Verification and fresh-value binding

Verification must be free of command mutation and user-expression evaluation.

The supported descriptor represents the complete raw command graph, including:

- all SQL-affecting clauses, flags, ordering and limits;
- projection/result and bound provider types;
- methods/members and converter/mapping identity;
- set-operation kind and operand order;
- CTE scope, dependencies, recursion and repeated references;
- literal versus parameter classification;
- shape-sensitive IN/PreWhere distinctions.

A hash match alone never grants a hit.

Preparation data must reference the current graph through stable paths or
indices. No extractor may close over the cache-populating command or closure.

Unsupported or effectful shape discovery falls back before effects. Initial
recipe ineligibility includes temp-table/TVP branches and any IN-shape case for
which side-effect-free verification and equivalent binding are not proved.

The five required Category B scenarios cannot be removed from acceptance by
such a guard. If their required shapes remain ineligible, this mechanism has
not met its goal.

## Transactional application and state-consumer mapping

Apply preparation data into isolated, current-command-owned staged state.
Do not mutate the input command or mark it prepared before validation completes.

Reuse only immutable structures whose downstream writes have been audited.
Rebuild mutable normalized state with current graph/value sources.

Construct the staged normalized key and validate it through the existing
normalized store before commit. On an application, key or owner mismatch,
discard staged state and use ordinary preparation of the untouched input.

Do not evaluate user parameters during speculative staging. Parameter extraction
continues under the existing execution/refresh contract.

| State reproduced or preserved | Producer anchors | Consumer / proof obligation |
|---|---|---|
| Normalized clause/graph state, prepared lists and prepared flag | QueryPreparer.cs:85-135 | All downstream prepared-command consumers; no partially prepared command |
| From, Columns, Join, Sorting, Where, Grouping hashes | :137-142 | QueryPlanEqualityComparer hashing at :391,441,446,449,452,462 |
| Union and ReferencedQueries hashes/relationships | :154,162,174 | Hash/equality at :536,539; fresh nested values |
| CTE normalized graph and CtesPlanHash | :304-385,381-383 | :548 and equivalent hoisting/reachability invariants |
| Hints and Windows state/hashes | :403,3267 | :551,569 |
| IN and PreWhere shapes | :3120,3145; refresh :208/243,213/247,219/253 | QueryCommand.Plan.cs:34-35 and comparer :444,524 |
| ResultPlanHash | QueryCommand.TResult.cs:37 | comparer :397; do not bypass TResult-specific preparation semantics |
| Plan-key memo/reset state | QueryCommand.Plan.cs:30-44; copy/reset sites in Clone.cs and QueryCommand.cs | No stale _planKeySql or key derived from prior state |
| Parameter-refresh applicability and current sources | QueryPlanner.cs:763-764 | Preserve !NoParams && NeedsParamRefresh behavior |
| Holder/enumerator/lifecycle state | QueryPlanner.cs:739-773 | Existing holder hit and on-demand enumerator path only |

The implementation inventory must map actual fields and their writers/readers
to this table through Roslyn. An unproved field is not silently copied.

After commit, continue through existing normalized lookup/hit behavior.
No separate SQL renderer, mapper, database-command creation or execution path
is introduced.

## Shared-command guard

There is no existing shared/fresh property.

The shared terminal route is established by:

- EntityBuilderExtensions.GetAnyCommand, :1378-1406;
- QueryCache.AnyCommand, QueryCache.cs:19;
- Any/AnyAsync storeInCache:true calls, EntityBuilderExtensions.cs:298,323;
- ReplaceCommand, QueryCommand.cs:888-897.

Before recipe probing, exclude the actual shared AnyCommand instance obtained
from the applicable DataContext's QueryCache. Audit all uses of that shared
instance through Roslyn.

Use route/instance identity, not IsPrepared, TResult, command shape, or a guessed
new ownership flag.

This excludes optimization-side mutation; the route's original behavior remains.

Never set Cache=false or change sticky _dontCache:
QueryCommand.cs:53,343-348. Per-call bypass remains storeInCache:false.

Already-prepared commands, public Prepare(), cache-disabled commands and
storeInCache:false retain their original routes.

## Correctness and lifecycle tests

Use ordinary preparation as a differential oracle and run existing regression
suites unchanged.

Required tests include:

- raw collisions, owner replacement, Clear and thread separation;
- changed closure values, no stale captures and equivalent evaluation;
- projection/type/mapping/converter distinctions;
- CTE reachability, dependency order, recursion, repeated references and scopes;
- Except/Intersect operand changes and Distinct semantics;
- unsupported graphs/nodes failing or falling back as ordinary preparation does;
- streaming/non-streaming, cancellation, partial enumeration and disposal;
- shared terminal operations before/after bypass and later cacheable queries;
- temp-table and SQL Server TVP branches, configured metadata precedence and
  metadata-cache Clear behavior.

Nested CTE tests exercise whole-graph reconstruction, not a fictional nested
owner/hit.

The canonical M9 symbol inventory still requires read-only discovery. No claim
is made that it has already been enumerated or passed.

## Impact surface and public API

Planned production modifications:

- src/nextorm.core/DataContext/QueryPlanner.cs
  — recipe entry, shared-instance exclusion, transactional application,
    existing normalized lookup/hit continuation.
- src/nextorm.core/Query/QueryCommand.QueryPreparer.cs
  — specialized raw capture, normalized state capture/application and fallback.
- src/nextorm.core/DataContext/Cache/QueryPlanStore.cs
  — Entry-owned data, raw candidate index, live-owner validation and common
    generation invalidation.

Audited dependencies, not planned production edits:

- QueryCommand.Plan.cs:30-44;
- QueryPlan.cs and QueryPlanEqualityComparer.cs;
- QueryCommand.TResult.cs:37,47-50;
- QueryCommand.Prepare.cs:17;
- CteHoister.cs:29,76,157,443;
- EntityBuilderExtensions.cs and QueryCache.cs shared route.

If actual implementation requires another production file, return to PLAN
before expanding scope.

Public API impact: none. Compare compiler-metadata public inventories before
and after production edits and retain public behavioral tests.

## Deciding performance oracles

### Category B inventory and commands

The broad filter selects 55 B_* methods, including EF Core and Dapper.
It does not select only the required ten arms.

Source: SqliteBenchmarkFeaturesFairCached.cs.
Capture the source-backed 55-arm inventory, then match the required pairs by
exact full name:

| Scenario | NextORM | LINQ to DB | Source lines |
|---|---|---|---|
| Cte | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Cte | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Linq2Db_Cte | 489 / 504 |
| RecursiveCte | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_RecursiveCte | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Linq2Db_RecursiveCte | 522 / 534 |
| Except | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Except | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Linq2Db_Except | 591 / 607 |
| Intersect | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Intersect | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Linq2Db_Intersect | 559 / 575 |
| Distinct | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Distinct | NextORM.Benchmark.SqliteBenchmarkFeaturesFairCached.B_Linq2Db_Distinct | 224 / 240 |

Baseline:

    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --exporters json --artifacts artifacts/pdca/T201/rv1/baseline/category-b

Candidate:

    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --exporters json --artifacts artifacts/pdca/T201/rv1/candidate/category-b

Read:
`<artifacts>/results/<Name>-report-full-compressed.json`.

Validate selected inventory against the source-backed 55-arm list. Only the
ten named arms determine the five approved parity comparisons. Do not pair by
substring, method order, baseline marker, or another library's arm.

Keep LINQ to DB/SQLite 6.5.0 and the existing fresh/non-prepared versus
non-compiled semantics.

### Native iteration14 gate: unchanged 28-row × 2-toolchain oracle

The deciding command is not a Warm_Reused-only full-job command.

Run from repository root:

    env -u NEXTORM_BENCH_FULL python3 eng/perf/iteration14_gate.py --manifest eng/perf/iteration14-budgets.json --reports-dir benchmarks/BenchmarkDotNet.Artifacts/results

Then reconcile the same newly generated reports:

    env -u NEXTORM_BENCH_FULL python3 eng/perf/iteration14_gate.py --manifest eng/perf/iteration14-budgets.json --reports-dir benchmarks/BenchmarkDotNet.Artifacts/results --no-run

Required: both commands exit 0; all 28 rows are represented for both toolchains.

The gate invokes:

    dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWarmDecompose* *SqliteBenchmarkCachedPlan* *SqliteBenchmarkFeaturePlanBuild* --job short --exporters json

NEXTORM_BENCH_DB is supplied by the gate. Its default timeout is 1800 seconds.

With NEXTORM_BENCH_FULL unset, NextormConfig.cs:23-25 supplies
InProcessEmitToolchain and --job short supplies Default. Setting FULL=1 would
remove the required InProcessEmit job.

Pinned manifest:
schemaVersion 1, iteration 14, planRevision r3,
logical_ops_per_invocation 100, bdn_operations_per_invoke 1,
required_toolchains Default and InProcessEmitToolchain.

Inventory from iteration14-budgets.json:13-40:

| Class | Method | Kind | Maximum B/op |
|---|---|---|---:|
| SqliteBenchmarkWarmDecompose | Cte_Warm_Reused | zero | 0 |
| SqliteBenchmarkWarmDecompose | RecursiveCte_Warm_Reused | zero | 0 |
| SqliteBenchmarkWarmDecompose | Join4_Warm_Reused | zero | 0 |
| SqliteBenchmarkWarmDecompose | Cte_Prepare_NoHash | budget | 15152 |
| SqliteBenchmarkWarmDecompose | Cte_Prepare_Hash | budget | 16222 |
| SqliteBenchmarkWarmDecompose | Cte_Warm_PlanOnly | budget | 16822 |
| SqliteBenchmarkWarmDecompose | RecursiveCte_Prepare_NoHash | budget | 16740 |
| SqliteBenchmarkWarmDecompose | RecursiveCte_Prepare_Hash | budget | 18400 |
| SqliteBenchmarkWarmDecompose | RecursiveCte_Warm_PlanOnly | budget | 18730 |
| SqliteBenchmarkWarmDecompose | Join4_Prepare_NoHash | budget | 14102 |
| SqliteBenchmarkWarmDecompose | Join4_Prepare_Hash | budget | 14252 |
| SqliteBenchmarkWarmDecompose | Join4_Warm_PlanOnly | budget | 15362 |
| SqliteBenchmarkWarmDecompose | InAtIn_Inline_Prepare_NoHash | budget | 5760 |
| SqliteBenchmarkWarmDecompose | InAtIn_Captured_Prepare_NoHash | budget | 5520 |
| SqliteBenchmarkWarmDecompose | InAtIn_Inline_Prepare_Hash | budget | 7100 |
| SqliteBenchmarkWarmDecompose | InAtIn_Captured_Prepare_Hash | budget | 6800 |
| SqliteBenchmarkWarmDecompose | InAtIn_Inline_Warm_Reused | budget | 840 |
| SqliteBenchmarkWarmDecompose | InAtIn_Inline_Warm_PlanOnly | budget | 7830 |
| SqliteBenchmarkWarmDecompose | Cte_Construct | budget | 10752 |
| SqliteBenchmarkWarmDecompose | RecursiveCte_Construct | budget | 12600 |
| SqliteBenchmarkWarmDecompose | Join4_Construct | budget | 10862 |
| SqliteBenchmarkWarmDecompose | InAtIn_Inline_Construct | budget | 4080 |
| SqliteBenchmarkWarmDecompose | InAtIn_Captured_Construct | budget | 3840 |
| SqliteBenchmarkFeaturePlanBuild | Build_Baseline_SimpleSelect | budget | 8202 |
| SqliteBenchmarkCachedPlan | RePrepare_PlanOnly_Param | budget | 2780 |
| SqliteBenchmarkCachedPlan | Build_Sql | budget | 8982 |
| SqliteBenchmarkCachedPlan | Cached_PlanOnly_Param | budget | 7002 |
| SqliteBenchmarkCachedPlan | Prepared_ToList | no_growth | 780 |

Totals: 28 rows: zero ×3, budget ×24, no_growth ×1.
The existing gate governs its normalization and no_growth interpretation.

The zero-allocation Warm_Reused obligation maps to the three zero-kind rows.
InAtIn_Inline_Warm_Reused is a separate 840 B/op budget row, not a fourth
zero-kind row.

Before each native run, archive only the reports for these three pinned classes,
so historical reports cannot fill missing current results. Record run start,
generated file hashes, source tree and commands. Copy fresh reports/logs to
the corresponding baseline/candidate artifact directory, then restore historical
tracked report files. Do not delete unrelated reports.

The unchanged gate's report-only reconciliation is demonstrated during DO.
Historical tracked reports and a scout's permission-denied attempt are not
passing post-change evidence.

### Seven-case acceptance inventory

Exact active cases:

1. NextORM.Benchmark.InMemoryBenchmarkAggregates.Nextorm_Count
   — source :39-41.
2. NextORM.Benchmark.InMemoryBenchmarkGroupBy.Nextorm_GroupByCount
   — source :39-41.
3. NextORM.Benchmark.SqliteBenchmarkAny.Nextorm_Cached
   — source :125-127.
4. NextORM.Benchmark.SqliteBenchmarkCachedPlan.Prepared_ToList
   — source :43-45.
5. NextORM.Benchmark.SqliteBenchmarkCachedPlan.Cached_ToList
   — source :58-60.
6. NextORM.Benchmark.SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param
   — source :72-74.
7. NextORM.Benchmark.SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync
   — source :131-134.

Canonical inventory: scripts/iteration15_evidence.py:48-56.
SqlServerBufferedNumericAcceptanceBenchmark's case at :61 is build-excluded
by nextorm.benchmark.csproj:44-46; it is not an eighth required case.

Baseline:

    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --exporters json --artifacts artifacts/pdca/T201/rv1/baseline/acceptance

Candidate:

    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --exporters json --artifacts artifacts/pdca/T201/rv1/candidate/acceptance

Record wall time, Mean, Error and Allocated for every case, with:

- time Cached_ToList / Prepared_ToList;
- allocations Cached_ToList / Prepared_ToList.

Show current same-host baseline and historical ratios 1.87 and 7.42.
Comparable ratio growth >20% requires investigation, not automatic failure.
Prepared_ToList is a no-regression control, never evidence of Category B parity.

## Executable ratio and noise decisions

Use unrounded machine-readable measurements from matching jobs.

For each Category B pair:

    point = nextorm.mean / linq.mean
    ratio_low = nextorm.ci_low / linq.ci_high
    ratio_high = nextorm.ci_high / linq.ci_low

Require positive finite means and positive finite CI bounds. Both arms must
provide the same exported confidence level, at least 99%. Missing/invalid CI
data makes the time decision OPEN.

Per valid round:

    if point <= 1.05 and ratio_high <= 1.05:
        time_decision = MET
    elif point > 1.05 and ratio_low > 1.05:
        time_decision = FAIL
    else:
        time_decision = OPEN

This is a conservative uncertainty rule, not a changed tolerance.

Allocation decision, using raw bytes per BDN operation:

    if linq_bytes == 0:
        allocation_decision = MET if nextorm_bytes == 0 else FAIL
    else:
        allocation_decision = MET if nextorm_bytes <= linq_bytes else FAIL

Do not divide Category B or acceptance measurements by the iteration14 gate's
100-operation normalization. The native gate alone applies its own manifest.

Perform initial baseline/candidate collection and, if needed, at most two
additional targeted full-job repeat rounds. A repeat contains both competitor
arms with the same data/job/runtime; do not select only the NextORM arm.

Across all valid candidate rounds for a scenario:

- MET only if every round is MET for time and allocations;
- FAIL if every time round is decisively FAIL, or any round fails allocations;
- otherwise OPEN.

Preserve every valid round. Invalidate a run only for a recorded operational
reason established independently of its ratio, such as benchmark failure,
wrong job or stale reports. A favorable run is not a reason to discard another.

For seven-case control uncertainty, mark an individual case noisy when its
exported Error >= Mean, or CI lower bound <=0. Report it, but do not derive a
reliable improvement claim from it.

For cached/prepared ratio growth, calculate both point ratios and conservative
ratio intervals. Investigate if point growth exceeds 20%, or uncertainty does
not exclude growth exceeding 20%. Resolve through recorded same-host repeats
or a documented cause; otherwise the control row stays OPEN.

The project's ~20% noise guidance never relaxes the approved 5% bound.

## Evidence instrumentation and completeness

Add the planned task-specific wrapper `eng/perf/t201_evidence.py`.
It validates inventories, records commands/wall times, runs the unchanged
native gate, implements the rules above, and assembles evidence.

Do not change benchmark query bodies, LINQ to DB version, prepared status,
data semantics, gate budgets, gate code or toolchain configuration.

Manifest:
`artifacts/pdca/T201/rv1/manifest.json`.

Schema required by scripts/validate_inner_loop.py:

- top-level nonempty strings: task, tree, status_file;
- contract_rv: integer >=1, not bool;
- required_rows: nonempty unique strings;
- rows: nonempty objects;
- per row: unique id and status met/open/na;
- met: nonempty evidence entries, each with kind and path, optional exit;
- open: nonempty reason;
- na: nonempty predicate;
- rows exactly cover required_rows: no missing or undeclared IDs.

For T201, all sixteen required rows are unconditional:
na is forbidden. Any open row prohibits PASS, even if the schema validator
returns 0.

Requirement IDs, owners, scenarios, applicability and rv stay in the status
contract table, not invented manifest fields.

Validate with:

    python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T201/rv1/manifest.json

Exit 0 means schema-valid, not acceptance PASS. Exit 2 means violations;
exit 1 means usage error. CHECK additionally verifies row truth, existing
evidence paths, exits/results and the no-open/no-na rules.

No future artifact is asserted to exist or pass.

## Risks and stopping rules

Risks:

- raw verification remains too expensive;
- staging/reconstruction allocations exceed LINQ to DB;
- incomplete state capture or wrong CTE/configuration identity;
- retained closures or mutable templates;
- shared-command exclusion misses an actual route;
- native gate reports mix old/new runs;
- uncertainty prevents a 5% decision.

The owner-lookup design is established by explicit invariants, but its
implementation must pass collision, replacement, generation and thread tests.
If discovery shows those invariants cannot be implemented within the actual
store contract, stop that DO unit and return evidence to PLAN.

A correctness guard cannot remove a required Category B scenario from the goal.
Persistent low confidence about owner-domain soundness triggers a narrow
trigger-5 escalation. A measured performance miss requires a real replan,
not changed tolerance, prepared-query substitution or a revision-name reset.

No milestone movement, commit, merge or push is authorized by this design.
