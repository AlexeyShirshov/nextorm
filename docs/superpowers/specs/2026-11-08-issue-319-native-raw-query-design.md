# Issue #319 native raw query design (draft)

Status: fresh independent builder-check and final coherence verification are COMPLETE; owner written-design approval recorded 2026-11-08 (`апрув`). Planning only — no implementation authorization. This is not a plan and not a PDCA cycle. The earlier 2026-11-02 design approval and prototype permission do **not** carry forward.

Tracking and evidence references:

- Repository: `git@github.com:AlexeyShirshov/nextorm.git`
- Issue #319: https://github.com/AlexeyShirshov/nextorm/issues/319
- Issue #319 state: `OPEN` (reopened 2026-11-08 for approved-design planning in 1.0.9; implementation not authorized)
- Issue #319 milestone: `1.0.9`, #21, open
- Historical (dated 2026-11-08 and earlier): issue #319 was `CLOSED`, milestone `1.0.9-rc5` (#23, open).
- Verified appended tracking comment (initial): https://github.com/AlexeyShirshov/nextorm/issues/319#issuecomment-6718148572
- Verified appended tracking comment (follow-up): https://github.com/AlexeyShirshov/nextorm/issues/319#issuecomment-6718396940
- Approved full text comment (immutable): https://github.com/AlexeyShirshov/nextorm/issues/319#issuecomment-6718483437
- Verified owner approval comment: https://github.com/AlexeyShirshov/nextorm/issues/319#issuecomment-6718840910
- Design draft location: `docs/superpowers/specs/2026-11-08-issue-319-native-raw-query-design.md`
- Pinned source SHA: `dcc752dba827df28fe1a58c0f94fbc6ea5875fe0`
- Separate detached worktree: `/tmp/opencode/nextorm-build-once-design-20261108` (detached HEAD, no branch)
- Earlier sorting spike preserved separately at `/tmp/opencode/nextorm-sorting-struct-spike-20261107`
- Existing 2026-11-02 design: `docs/superpowers/specs/2026-11-02-issue-319-compatible-descriptor-design.md`
- Status records: `.pdca/status/T319-rc5-2.md` (notably lines 284-302), plus `T297` and `T363-1`
- Decisions/lessons: `docs/specs/performance/decisions-and-lessons.md` (R15, lines 184-208 and 336-339)
- Benchmarks: `docs/comparisons/benchmarks.md` (2026-11-07), plus actual CSV names such as `FeaturesFairCached`

No skill edits or status-history rewrites are part of this draft.

## 1. Intent and why

Reduce per-call fresh fluent construction and cache-lookup overhead, especially for the four currently over-budget warm comparison arms. This is not an instruction to make all of `QueryCommand` a struct.

- Allocation goal: `<= 1.20x` linq2db per eligible arm, ideally lower.
- Time goal: no NextORM time regression versus the same-round pinned baseline on **each** target and control arm.
- Breaking public/protected source/binary API is allowed by the owner because this is pre-release. This allowance does **not** permit branch aliasing, user-visible copying burden, or wrong SQL/results.

Users see the normal fluent API with transparent, independent branches. There is no `Fork`, manual ownership, hoisting, or mandatory manual `Prepare`. Explicit `Prepare` remains available, and ordinary cached queries must benefit.

## 2. Scope and membership

Latest committed 2026-11-07 SQLite comparison matrix initial targets:

| Target | NextORM | linq2db | Ratio |
| --- | ---: | ---: | ---: |
| CTE | 11649 B | 9063 B | 1.285 |
| Recursive | 13789 B | 8540 B | 1.615 |
| Except | 6399 B | 5055 B | 1.266 |
| Intersect | 6383 B | 5008 B | 1.275 |

Approximate needed reductions: `774 B`, `3541 B`, `333 B`, and `374 B`. The rounded inputs above are not new acceptance floors.

The population is not frozen as a hardcoded 34 controls. All NextORM arms in the 15 report classes are timing controls except `ToDataReader` and raw-reader arms, which the user explicitly excludes. Eligible linq-paired rows at or below `1.20` allocation are allocation controls. The historical "34 controls" count is provisional only, not the frozen acceptance population. Exact membership and source SHA must be frozen at approved PLAN using the complete baseline and manifest in section 14 (Q3). All truly comparable rows over `1.20` belong; do not cherry-pick only the four if the baseline finds more. There is no automatic moving target after freeze.

`ToDataReader` is explicitly out of scope, including all raw-reader paths, plan-caching/descriptor work, and any #246 analysis or optimization. Existing reader tests remain semantic regression guards. Shared type changes that require reader-specific refactoring must raise a written scope delta before touching reader code.

No JSON/CSV/EF/provider feature expansion is included. Their currently compared cases are controls. Performance benchmarking is SQLite only; correctness on supported providers remains required.

## 3. Evaluation contract (owner-decided)

Collection via `Where`, `Select`, joins, CTEs, set operations, and `ToCommand` must only construct a raw description plus cheap argument guards (null checks and similar). It must not perform complex semantic/provider checks, cache-key calculation, SQL rendering, preparation, or connection access during collection.

At execution or explicit `Prepare`:

- necessary normalization/validation of the key occurs before lookup;
- full translation, provider checks, and build occur on miss;
- a hit does not rebuild SQL, but still performs execution-dependent validation and fresh value binding.

Checks required to avoid false key matches cannot be deferred until miss. Known-valid construction error timing may change (linq2db 6.5 behavior is the reference), but errors must not vanish on hits. Framework C# expression building remains eager as required; this design does not promise zero operator cost.

## 4. Proposed architecture (architect call)

Use a native, complete, immutable raw query description separate from prepared/compiled/execution state.

- Prefer compact readonly value handles for public command/builder results.
- Immutable compact nodes, arrays, and expressions may be held by reference where needed for graph lifetime and sharing.
- Do not convert the whole 872 B mutable `QueryCommand` verbatim to a struct.
- Do not add a new class inside the old wrapper for every operation and claim objects are saved.
- Do not put heavyweight prepared columns, resolved bindings, cache hash slots, mappers, or execution holders in each raw node.

Operators produce a value copy and/or a new minimal immutable affected node, safely share unchanged state, and a branch does not mutate source or sibling. Lambda-captured runtime values may vary normally; raw-description immutability does not freeze external closure data. Stored prepared plans must not capture caller closure instances or reuse old values.

In-place internal mutation is allowed only for strictly private construction or unique execution state, with no observable aliasing and no owner-tracking burden for users.

## 5. Native AST versus failed chain

The description exposes the current logical shape. It is **not** a linked program of deferred fluent instructions replayed into the old command on cache hit. Eagerly compose lambda expressions where existing semantics require it. Do not automatically revive deferred repeated `Where` from old #319.

The key is derived from the native complete raw description plus context/config at materialization. It is never reconstructed from `QueryCommand` prepared fields or a partially applied recipe. There is only one authoritative source of raw shape; no shadow mutable builder state.

Typed CTE and derived sources must be canonical independently of prepared aliases. #268 and #269 are explicit danger regression cases, not optional new features.

## 6. Key and build responsibilities

Conceptual responsibilities, not mandated public API names:

- distinct logical raw query;
- canonical key;
- compiled plan;
- execution bindings.

The key carries all SQL/result-shape-affecting state. Hashing is an optimization, not proof; structural equality guards collisions. Parameters and runtime values rebind on each execution. Parameterizable values are excluded only with valid extraction/binding semantics; literal values affecting SQL remain identity.

The key must include and validate, as applicable:

- entity/result/projection types;
- from/joins/CTE graph and recursion;
- set operations and outer bindings;
- conditions, group, having, prewhere, sort, null order, paging, window;
- provider clauses, hints, tag, quoting, naming, timeout, options;
- filters and configuration/converter/mapping generation;
- eager load, collection/document/component, and terminal shape.

Inventory current comparer/preparer fields. Do not assume existing `QueryDefinition` 32 properties or `QueryShape` 20 properties are complete. Metadata resolution needed for identity may occur at the key boundary. Full normalized SQL graph occurs only on miss unless explicit `Prepare` is forced. Cache-hit state never reads preparation state back into the raw graph. Mutable compiled key components must not change after insertion. Context filter/config changes must invalidate or partition the key. Preserve cache-clear semantics. No global new cache or retained closures without separately approved policy.

## 7. Prepared and execution state

Immutable compiled SQL plus binder metadata and result shape are reusable. Per-execution parameters and resource ownership remain separate. Existing cache lifetimes, eviction, and threading are not globally redesigned as part of this project without a scope delta.

The shared `Any`/`Count` sticky cache trap remains: never set `queryCommand.Cache = false` on a per-call basis; use call-local store flags. Preserve query isolation, SQL/row semantics, cache hit/miss and explicit `Prepare` behavior, cancellation, and connection ownership. No cached execution resources from the reader route.

APIs may change inheritance, `IQueryRegistry`, comparer return types, and interfaces to remove boxing/allocations. Interface compatibility is not required. Generic constrained/static internal comparison may replace per-command comparer objects. Exact class/struct layout and hash algorithm are explicitly left to the builder within measured gates; choose by total cost, not struct ideology.

The hot path must actually avoid legacy factory creation. The current `QueryPlan` constructor forces all six, so a standalone helper alone is not a product optimization.

## 8. Alternatives and tradeoffs

- Mechanical whole-`QueryCommand` struct: rejected as default because of large copies, prepared mutation, and references that do not deep-clone.
- Repeating old copy-on-write without a causal hypothesis: rejected because time failed.
- Keeping public class facades as a requirement: rejected as a requirement because the API constraint is waived. They may be used only if measured and not needed for compatibility.
- Native compact raw plus late compile: chosen hypothesis. It carries canonicalization risk and is not a promised win.
- A compact class control may benchmark against the value handle if struct copying loses. Representation is an implementation choice without changing behavior or acceptance.

## 9. Prior evidence, honestly

- #297 copy-on-write saved allocations but time regressed. This is not proof that all copy-on-write is impossible.
- #319 `QueryShape`/deferred `Where` failed and rolled back.
- #363 state-dependent raw recipe failed.
- #317 per-new-command memoization was ineffective.
- #318 empty copies were already absent.
- Sorting throwaway spike pinned at `82c3d238` in `/tmp/opencode/nextorm-sorting-struct-spike-20261107`: validated 9 cases, 7 arrays, 34 lookups; retained class 24 B, value 0 B, interface boxed 24 B. Corrected timings show CI overlap, so no time win was established. The spike is standalone, not engine code, and 24 B does not solve a 3.5 KB recursive gap.
- Do not cite the earlier invalid retained timing with a 70% penalty.
- R15 remains relevant as historical context, not as a separate conflicting strict acceptance gate for this design: state-independent raw identity, #268/#269 SQL/row parity, and historical time `<= 1.00` are retained as historical evidence. Historical capture+apply `< 50%` prepare remains a precondition and is not waived by API freedom. The current design's time verdict is governed only by section 14 (Q4); do not waive the cost guard or claim that old failed approaches passed.
- The new native path does not necessarily have capture/apply. PLAN must explicitly map its identity/lookup/binding cost to the historical oracle before claiming R15 is satisfied. If not meaningfully mappable, seek owner decision on that delta; do not silently waive or substitute an inflated budget.
- The hypothesis is not already successful or scheduled.

## 10. Validation and design gates (not an implementation plan)

First establish a semantic oracle for all targets and controls and branched/retained queries:

- equality, hashing, collision, mutable capture, parameter/config invalidation;
- repeated actual row checks, not count-only checks;
- SQL parity unless an intentional SQL change is explicitly reviewed;
- #268/#269 state independence.

Verify miss/hit/explicit `Prepare`, cold/warm, and no hidden preparation or keys at collection via observable phase counters or semantic markers. Do not make those permanent production overhead.

Then run an isolated bounded prototype with actual fresh fluent construction, not user-hoisted commands, using a frozen baseline and candidate, same-round paired ABBA per the existing harness. Measure contributions from raw construction, key, validation, build, and binding, plus total counts and bytes.

Full complete matrix allocation and time are required. Point optimizations cannot hide control regression. Exact B/query normalized operation count and fair linq strategy must be used. Existing short-run matrix and micro-spike choose candidates; they are not acceptance.

Time/noise acceptance is governed by the owner's neutral-noise criterion in section 14 (Q4): no actual NextORM time regression; small point-estimate changes inside an empirically confirmed same-round noise envelope are neutral/acceptable when there is no statistically confirmed positive paired regression. A confidence interval containing `1.00` alone is not sufficient when the harness is underpowered or the point estimate is outside qualified noise. Qualified small within-noise changes pass; confirmed regressions fail; unqualified or wide unstable measurements are unverified, not success. This supersedes the prior draft statement that a confidence interval crossing `1.00` is automatically unverified/not-pass. No blanket 20% noise allowance is imported. If costs cannot close any target or regress time, stop and revise the design rather than relax the floor.

Correctness requires full unit, provider SQL, and live integration per repository instructions and `.opencode/skills/running-integration-tests/SKILL.md` before containers, with no skips presented as green. Test immutable branch/operand isolation, multiple terminals on the same query, and shared `Any`/`Count` cache guards. Tests use FluentAssertions. Build with .NET 10 and warnings as errors.

Document breaking public APIs in English and Russian. Examples use normal fluent construction. Do not preserve obsolete binary compatibility by default.

Exact commands, repetition counts, provider containers, and physical size budgets are left to a future approved PLAN. No invented thresholds now.

## 11. Non-goals and exclusions

- No `ToDataReader` / #246 revival.
- No hand-hoisted benchmarks or manual `Fork`.
- No LINQ provider rewrite or new source generator.
- No unrelated prepared execution, row mapper, or provider SQL optimizations.
- No sticky cache mutation.
- No new global memoizing of user queries or closures.
- No change to accepted residual status or old cycles as if passed.

## 12. Success, stop, and staging

Design approval leads to planning only, not implementation. A new PLAN is authorized separately. No PDCA cycle is initiated here. Prototype permission is not inferred from the old spike.

Freeze the four targets plus any new verified baseline over-budget rows before PLAN approval. Acceptance requires all target allocations `<= 1.20x` linq. Time acceptance follows the owner-qualified noise contract in section 14 (Q4): no statistically confirmed positive paired time regression under qualified same-round noise; small within-envelope changes are neutral/acceptable; unqualified or high-variance measurements are UNVERIFIED, not success. Controls that were `<= 1.20` remain `<= 1.20` on allocation, with no time regression under the same qualified contract. API changes are allowed; branch transparency and late checking are hard. The shorthand goal of "no regression" remains acceptable as long as it is read through section 14 (Q4).

No auto-closing or reopening of the issue, no commits, and no merge.

This draft must be read by a fresh builder-check before user review, then receive written user approval, then a separate implementation plan.

## 13. Decision classification

| Category | Items |
| --- | --- |
| User/owner decisions | breaking API allowance; no manual Fork/hoisting/mandatory Prepare; four initial target membership from latest matrix; ToDataReader out of scope; neutral-noise time criterion; R15 mapping must be settled before PLAN acceptance |
| Architect choices | native compact raw plus late compile; conceptual key/build responsibilities; evaluation contract; alternatives rejection; miss-boundary legacy compile allowed; raw state covers builder lifetime and Select/ToCommand; population manifest; filters/captured-collection contracts; test/doc migration; D201 machinery; SQL parity default |
| Builder choices left open | exact class/struct layout, hash algorithm, commands, repetition counts, provider containers, physical size budgets, statistical method/repetitions/noise qualification |

## 14. Builder-check resolutions (architect decisions)

These resolutions answer the builder-check questions. They are architect decisions, not user approval of implementation.

### Q1. Miss boundary

The new path owns raw description, key, and lookup. On miss it may materialize an internal legacy compile command graph once and reuse existing `QueryPlanner`/`SqlBuilder`/provider translation/mappers. That adapter runs only at materialization, not during fluent collection, and never on cache hit. No SQL engine/provider rewrite is required. The cached artifact is immutable with fresh current value bindings as needed; it is not a mutable caller command. Legacy public `QueryCommand` need not remain public. Explicit `Prepare` may use existing compile options/ownership semantics and need not fill the shared cache merely because operator collection changed. Hit must not materialize the heavy graph or replay fluent construction. Uncacheable cases fail closed to compile each time rather than silently using a partial key.

### Q2. Raw state scope

The new raw state covers both the entire fluent builder lifetime and `Select`/`ToCommand` output. `EntityBuilder` clone/copy allocation is in optimization scope, not only the final command record. There is one authoritative raw shape from initial source through derived builders, CTEs, and set operations. Provider builder extensions adapt to the new raw API as needed without adding features. Fallback does not mean a parallel mutable copy of the raw description. Exact value-handle/internal-node physical layout remains a builder choice gated by measurements.

### Q3. Complete population

The complete population comes from all existing comparison report classes in the committed 2026-11-07 batch `f855b884`: seven end-to-end benchmark classes, `FeaturesFair`, `FeaturesFairCached`, and six tier-1 classes, for 15 total. The token `7e2e` is not a class or commit identifier; the actual class names are resolved from the committed reports and recorded in the manifest. Freeze the actual source/report SHA and an explicit machine-readable arm manifest before PLAN approval.

All NextORM arms in those committed reports are timing controls, except `ToDataReader` and raw-reader arms, which the user explicitly excludes. Eligible allocation targets are all over-`1.20` NextORM-versus-linq2db matched-scenario rows, retaining the published pairing as documented. Unpaired NextORM variants are controls, not invented linq targets. The existing four rows are initial targets; the provisional historical "34 controls" count is not the acceptance population. EF and Dapper arms are not optimization targets, but they may run for pairing or reference. JSON, CSV, projection, paging, and aggregates are existing controls and may remain legacy if correctness and no time regression are maintained. Extra unpublished `Focused`, `InMemory`, and T287 probes are not new allocation targets and may be used as semantic or profiling sentinels.

The manifest must record method names, provider, scenario, operation count, pair, cache mode, target or control status, and source artifact. Baseline freeze may find additional over-budget rows before PLAN approval; after that freeze, membership does not float.

### Q4. Time and noise

No actual NextORM time regression. Small point-estimate changes inside an empirically confirmed same-round noise envelope are neutral/acceptable when there is no statistically confirmed positive paired regression. The `+0.3%` within `±1%` owner example is an example, not a universal 1% tolerance. Measure the environment A/A repeatability envelope before candidate, and lock noise qualification, statistical method, and confidence level in the future PLAN. Do not estimate a bigger band from a bad candidate and do not import a blanket 20% docs band. Use paired A/B or ABBA, report point estimate and uncertainty. A confidence interval containing `1.00` alone is not sufficient if the harness is underpowered/high variance or the point estimate is outside qualified noise. Qualified small within-noise changes with no confirmed regression pass; stable/statistically confirmed regressions fail; unqualified or wide unstable measurements are unverified, not success. Previously failed historical approaches are not retroactively passed. The future PLAN chooses the precise statistical method, repetitions, and qualification before DO; the user does not approve implementation here.

### Q5. Filters

Correctness is required; universal new fast-path coverage is not. Snapshot/evaluate applicable filter shape and config generation at the key boundary; bind changing filter values per execution only when extraction is correct. Never reuse a plan across different active filter shapes, context configuration, or frozen old closure values. If key completeness cannot be proven, fail closed to compilation without the new fast path. Retaining filter semantics must not depend on the current raw recipe's prepared fields. Filter bypass cannot silently exclude target arms or the loss goal; each actual target must meet final allocation and time acceptance.

### Q6. Captured collections

Preserve the baseline code contract. `WHERE`/`PREWHERE` are shape-keyed with current values/arity rebound; unkeyed clauses such as `HAVING`, join, and select remain uncacheable unless separately proven correct. There is no requirement to expand cacheability for all clauses; fallback is existing behavior, not new functional work. Cardinality, nullability, element type, parameter ordering, and mutable captures must be tracked by key/binder appropriately. Do not key or retain caller closure identities as a replacement for proper identity and binding. Existing captured `IN` variants are controls.

### Q7. Test and documentation migration

Intentional public/protected API and exception-timing test changes are allowed according to the approved contract. A full suite pass means migrated tests, not unchanged assertions. Semantic errors move to materialization/execution/`Prepare`; cheap guards remain at construction. Replace obsolete E19/API-shape expectations where an API break is intentional; do not delete tests covering isolation, filters, cache correctness, resources, or results. Add phase-guard tests including `Select`/`ToCommand` showing no hash/prepare/SQL/connection until materialization. Update English and Russian docs and samples for public renames in the same change. Unrelated semantics are not waived.

### Q8. D201 machinery

The migrated native path must not call old `RawShapeCapture`/`RawCandidate`/`ApplyPreparationFrom` or build two indices/authoritative sources for each query. Legacy raw recipe may remain only for untouched legacy entry points/control routes while migration occurs, explicitly partitioned by route/key version with no accidental interference. Once unreachable, remove obsolete bits and update their tests as cleanup within this work; a full upfront rewrite of all legacy helpers is not required. Fallback for a native query is materialize-and-compile (raw recipe bypass), not an old sidecar patch. The PLAN must identify route switches and tests proving no old capture on target native hits.

### Q9. SQL parity

Default is byte-identical SQL, parameter order/names, and result column order; adapter reuse makes this feasible. Fresh parameter values are expected to vary. Semantic equivalence alone is not permission to mass-rebaseline dialect tests. Any intentional SQL change needs a separately reviewed scope amendment. Breaking API permission does not waive SQL parity. A state-independent canonical key may normalize identity without changing render order.

### R15 mapping deliverable

The historical precondition is retained; no arbitrary new budget is introduced. Mapping the new path's identity/lookup/binding cost to the historical oracle is a required PLAN deliverable before approval. It must repeat old matched oracles/cases, including #268 and #269, and document which native identity/lookup/binding work corresponds to old capture+apply and the prepared denominator. If meaningful mapping is not possible, raise a narrow owner decision before execution; do not claim R15 is waived or satisfied merely because API breaking is allowed. The owner's neutral-noise time criterion is new and supersedes the old strict statistical verdict for this design only.

## 15. Tracking update

- Issue #319 is `OPEN` (reopened 2026-11-08): https://github.com/AlexeyShirshov/nextorm/issues/319
- Milestone is `1.0.9` (#21, open).
- Historical (dated 2026-11-08 and earlier): issue #319 was `CLOSED`, milestone `1.0.9-rc5` (#23, open).
- Reopen tracking comment: https://github.com/AlexeyShirshov/nextorm/issues/319#issuecomment-6719149011
- Initial tracking comment: https://github.com/AlexeyShirshov/nextorm/issues/319#issuecomment-6718148572
- Follow-up tracking comment: https://github.com/AlexeyShirshov/nextorm/issues/319#issuecomment-6718396940
- This draft remains design-only. No implementation permission, scheduling, commit, push, or merge is granted.

## Open question

The only explicit open question is whether the new native path can meaningfully map its identity/lookup/binding cost to the historical R15 capture+apply oracle. This is a required PLAN deliverable before approval, not a user question now. It does not block describing desired behavior.