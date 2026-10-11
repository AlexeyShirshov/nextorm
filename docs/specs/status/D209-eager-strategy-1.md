# T209 — #209 eager-load strategy (explicit strategy + auto-dispatcher)

- task_id: T209
- issue: #209 — https://github.com/AlexeyShirshov/nextorm/issues/209
- collection: 1.0.9-todo (docs/specs/status/collection-1.0.9-todo.md)
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=1
- baseline ref: branch `1.0.9` @ `58025147`
- phase: P (collection TASK PLAN) — PLAN complete; STOPPED at the PLAN→DO boundary (no DO started)
- plan_state: ready
- evidence contract: rv=1 (manifest artifacts/pdca/D209/rv1/manifest.json)
- note: the earlier resource blocker (planner "Insufficient account funds") is RESOLVED; this plan was authored by planner with funds restored.

## Approved PLAN r=1

⟦09:43:24⟧ T209 starts at **r=1, n=1; evidence contract rv=1**. The failed provider invocation was not a rejected plan or a content blocker.  
Preserve `EagerLoadMode`; add a context default, deterministic dispatch, structural cache identity, and fail-closed unsupported strategies.  
T209 does **not** implement per-parent top-N or correlated SQL; those remain explicit dependencies on #211 within milestone **1.0.9**.  
Execute sequentially in one tree; freeze the shared contract before the collection ALL-barrier. All future execution evidence below is **planned**, not already passing.

# P: T209-r1 — explicit eager-load strategy and conservative auto-dispatch

## 1. Goal and acceptance criteria

Provide one coherent eager-loading strategy contract across configuration, query preparation, execution, plan caching, and documentation, without introducing a strategy hierarchy.

| ID | Acceptance criterion and observable oracle | Negative case |
|---|---|---|
| R209-01 | Existing enum values and `LoadWith(..., mode = Default)` remain compatible. Existing supported eager-load queries retain their results and materialization semantics. | Invalid enum values throw `ArgumentOutOfRangeException`; they never select a fallback. |
| R209-02 | A context-wide default is configurable through the existing builder pattern. A query override wins over that default; absent either, the default is `SplitQuery`. | An explicit query choice is not overwritten by configuration, and one query does not alter subsequent queries’ defaults. |
| R209-03 | Explicit `SplitQuery` and `SingleQuery` use their existing execution paths. Conflicting explicit choices retain the existing rejection behavior. | Unsupported Single shapes fail before executing SQL; conflicts are not silently resolved by load order. |
| R209-04 | `Auto` follows the deterministic rule in §2. Resolved strategy is fixed during preparation and consumed consistently during execution. | Auto does not catch execution errors and retry with another strategy, or silently remove child filtering/limits. |
| R209-05 | `Correlated` is recognized but explicitly unsupported in E1. Unsupported per-parent top-N/correlated requests fail before SQL execution and before cache insertion. | Neither explicit Correlated nor Auto may reinterpret per-parent top-N as a global child limit or an unrestricted load. |
| R209-06 | `JoinInto` remains fixed Single independently of the context default. Its existing stitching and key distinctions survive. | Context `SplitQuery`, `Auto`, or `Correlated` does not convert `JoinInto` into another execution model. Existing incompatible mixed-shape guards remain effective. |
| R209-07 | Cache equality and hashing distinguish every admitted difference that changes eager-load execution or materialization. Equivalent resolved plans can reuse a plan. | Different strategy, navigation/load shape, join semantics, or child-expression structure cannot reuse an incompatible plan. Hash collisions alone are never equality. |
| R209-08 | Strategy resolution never mutates the shared command’s sticky cache flag. Any uncached preparation uses the existing per-call `storeInCache: false` mechanism. | Running eager loading or a rejected strategy cannot disable caching for subsequent `Any`/`Count`/`All` queries on the context. |
| R209-09 | EN/RU public documentation and the internal scope/naming records agree with the shipped E1 behavior. | No documentation claims Correlated/top-N support, leaves “strategy” wholly out of scope, or links public pages to internal specs. |
| R209-10 | Builds, functional suites, real-provider integration evidence, and coverage satisfy §5. | Skipped required provider integrations, warnings, unaccounted coverage shortfalls, or missing negative-case evidence are not a passing CHECK. |
| R209-11 | The pinned baseline and candidate complete the seven-case acceptance benchmark within the specified gate; ratio growth above 20% receives an investigation. | Missing cases, different case identities, timeout, or an unexplained flagged ratio cannot be represented as successful performance evidence. |
| R209-12 | The rv1 manifest validates, and #211/#212/#213 acknowledge the shared contract before the ALL-barrier passes. | Missing reports, incompatible sibling plans, or a validator failure cannot be replaced by a narrative “ready.” |

## 2. Minimal solution and naming

### Three answers

1. **Essential objective:** make strategy selection explicit, configurable, deterministic, and cache-correct.
2. **Hard constraints:** preserve existing public values and supported behavior; no strategy interface/options hierarchy; no sticky cache mutation; fail closed on missing correlated/top-N execution; bilingual docs; same milestone.
3. **Minimum implementation:** extend the existing enum, add one scalar context setting and one nullable query override, use existing dispatch paths, and extend structural plan identity.

### ExactExpectedNames

These are **planned API/field names**, not claims about existing symbols.

| Location | Frozen name/meaning |
|---|---|
| `EagerLoadMode` | Preserve `Default=0`, `SplitQuery=1`, `SingleQuery=2`; add `Correlated=3`, `Auto=4`. |
| `DataContextBuilder` | `UseEagerLoadMode(EagerLoadMode mode)`; follows the existing `UseX` pattern. |
| `IContextEnvironment` / concrete `DataContext` | Read-only `EagerLoadMode` environment setting, initialized when the context is built. |
| `QueryCommand` | Nullable `EagerLoadMode` override; `null` means inherit. |
| `EagerLoadSpec` | `EagerLoadMode`, retaining the requested selection needed for validation/preparation. |
| `QueryPlan` | `EagerLoadMode` for the **resolved** strategy and `EagerLoadShape` for structural load identity. |

No `LinqOptions`, `DefaultEagerLoadingStrategy`, `IEagerLoadingStrategy`, or parallel strategy enum.

`UseEagerLoadMode(Default)` is rejected: `Default` is an inheritance sentinel, not a recursively inheritable context setting. Invalid numeric values are also rejected. The builder accepts the four concrete selections; selecting Correlated is legal configuration, but an applicable `LoadWith` fails as unsupported during preparation.

Keep the environment addition source-compatible for existing implementers by supplying the legacy `SplitQuery` default through a default interface getter. Concrete `DataContext` exposes its configured value; configuring a context must not modify a static/global value.

### Precedence and timing

- Merge explicit non-Default `LoadWith` choices through the existing conflict rule.
- A merged explicit choice becomes the query’s override.
- Map `Default` to absence, not to an executable strategy.
- Resolve using **`cmd.EagerLoadMode ?? dataContext.EagerLoadMode`**, following `QueryPreparer:28–33`.
- Dispatch Auto once during preparation; store the resolved result in the plan.
- Child-command construction receives that decision rather than re-resolving against a different default.
- `JoinInto` remains its separate, fixed-Single operation. Do not broaden existing mixed-shape support as part of T209.

### Frozen E1 Auto rule and fail-closed boundary

| Requested operation | E1 behavior |
|---|---|
| Ordinary supported `LoadWith`, `Auto` | Resolve to `SplitQuery`. |
| Ordinary `LoadWith`, explicit Split/Single | Existing Split/Single implementation and guards. |
| `LoadWith`, explicit Correlated | `NotSupportedException` during preparation; no SQL/cache insertion. |
| A request requiring **per-parent** top-N or correlated execution | Reject during preparation until #211 supplies the complete supported path. Auto does not erase the requirement. |
| `JoinInto` | Fixed Single; unchanged. |
| No eager loads | No eager-loading dispatch or eager-load-specific command mutation. |

This is a **conservative dispatcher**, not a cost-based optimizer. Automatic Single selection, arbitrary APPLY/LATERAL construction, and provider capability guessing are not introduced.

The preserved summary cites the draft’s Auto rule but does not quote its algorithm. Therefore this plan does **not** claim to reproduce unseen wording. The rule above is the explicit E1 decision and must be written into the draft and EN/RU docs. If collection review requires a different heuristic, that is a substantive PLAN revision before the ALL-barrier—not an implementation assumption.

### Alternatives

| Approach | Benefits | Cost/risk | Decision |
|---|---|---|---|
| Existing enum + scalar default + conservative dispatch | Fewest concepts; compatible; clear extension seam for #211 | Auto initially chooses Split for ordinary loads | **Selected.** Correctness precedes optimization. |
| New options object and strategy abstraction | Extensible configuration surface | Second configuration concept; no second strategy implementation; broader API/cache footprint | Rejected by project invariants. |
| Implement correlated SQL and aggressive Auto now | More immediate capabilities | Expands into #211 and provider-specific semantics without preserved capability evidence | Rejected for T209; #211 remains active in 1.0.9. |

## 3. Cache identity and shared contract

### Cache key scope

Extend `QueryPlan` equality **and hashing**, using structural comparison rather than reference identity or a hash-only key.

Include:

- resolved executable strategy;
- ordered eager-load paths/member identities and relationship/cardinality identity;
- parent/child linkage and relevant mapped entity/configuration identity;
- join kind and nesting needed for stitching/materialization;
- child filter/projection structure and any other admitted child-command structure that changes SQL or materialization;
- the existing provider/dialect and parameter-shape identity, without weakening their current treatment.

Preserve existing parameterization rules: runtime parameter **values** are not a new shape dimension merely because eager loading exists.

Do not separately distinguish `Auto → SplitQuery` from explicit Split when their complete resolved shape is identical. Conversely, do not allow the root command’s equality alone to stand in for an eager graph.

Ordering/top-N fields become cache obligations when a sibling admits those shapes. In E1, unsupported variants are rejected **before** key construction/insertion. No `Cache=false` workaround.

Represent load identity using existing structural-expression/key mechanisms and immutable primitive/tuple storage. Do not add a public descriptor type or strategy interface. One centralized resolution path is shared by preparation, execution, and cache construction.

### Requirements on sibling plans

Record this contract in `artifacts/pdca/D209/rv1/sibling-contract.md`, acknowledged in each sibling’s collection plan.

| Shared input | T209 freezes | Siblings may change |
|---|---|---|
| `EntityBuilderEagerLoading.cs` | Requested/resolved-mode distinction; propagation to child preparation; early unsupported guard; shape identity ownership | #211 may replace the top-N/correlated guard with tested execution. #212/#213 may add their scoped behaviors, but must extend shape identity and negative tests where execution changes. |
| `EntityBuilder.cs` | Enum validation, query override/conflict semantics, precedence, centralized dispatch, existing Single guards | Add supported shapes or execution actions; do not bypass precedence, resolve independently, or weaken rejection into silent fallback. |
| `ISqlDialect.cs` | **No correlated capability is asserted by T209; no edit planned.** Strategy naming/resolution does not depend on guessed provider support. | #211 owns any correlated/top-N capability additions. Defaults must be fail-closed; each enabled provider requires SQL-generation and real-provider evidence. #212/#213 must reconcile any overlapping capability additions. |

Additional mandatory sibling obligations:

- Do not rename enum members, change numeric values, or add a parallel strategy abstraction.
- Do not mutate shared `QueryCommand.Cache`.
- An admitted ordering/top-N/shape-changing flag must enter structural equality and hashing.
- #211 must revise the Auto rule deliberately when correlated execution becomes supported; it must test provider-supported and provider-unsupported branches.
- #212/#213 cannot advertise correlated support merely because the enum exists.
- Shared-file ownership is serialized. The ALL-barrier requires compatible plans from all four tasks; T209 alone cannot pass it.
- Replacement of E1 guards later requires its own contract revision with preserved IDs and added variant IDs—not retroactive claims that E1 supported those variants.

## 4. Ordered DO tasks and footprint

All tasks below are **fix now**. The original D209 remains active until CHECK passes.

| Task | Action and anchors |
|---|---|
| **D209.1** | Coder writes this r1 plan, rv1 contract, exact names, and sibling acknowledgements into the cycle status/artifacts **before DO**. Pin the performance sources described in §7 before product edits. |
| **D209.2** | Extend/document `EagerLoadMode.cs:8`; implement builder/environment default wiring using `DataContextBuilder.cs:13,34,124` and `IContextEnvironment.cs:34–71`; add the nullable command override. |
| **D209.3** | Update `EntityBuilder.cs:563,603,625` and `EntityBuilderEagerLoading.cs:65–88,196,204,221`; implement centralized precedence, conservative Auto, conflict validation, and early unsupported rejection. Reconcile dispatch at `EntityBuilderExtensions.cs:489,496,518`. |
| **D209.4** | Extend structural key construction, equality, and hashing at `QueryPlan.cs:8`, `QueryPlanEqualityComparer.cs:199–205,550–556`; preserve existing Single/JoinInto paths at `EntityBuilder.cs:1227,1335,1345` and stitching at `JoinIntoStitcher.cs:60`. |
| **D209.5** | Add positive/negative tests under the shared `D209 eager strategy contract`; cover §5 including cache reuse/isolation, no-SQL rejection, and sticky-cache regression. Run build and focused suites. |
| **D209.6** | Update EN/RU public pages, internal draft, naming registry, and the two scope-removal documents. Review API surface and CRLF. |
| **D209.7** | Run complete provider generation suites, required real-provider integrations, coverage, and candidate acceptance benchmarks; investigate flagged ratios. Populate and validate the manifest. |
| **D209.8** | Submit CHECK against rv1 and reconcile the ALL-barrier records. Do not mark D209 done based on an implementation report alone. |

### Planned write-footprint

**Production**
- `src/nextorm.core/EagerLoadMode.cs`
- `src/nextorm.core/DI/DataContextBuilder.cs`
- `src/nextorm.core/DataContext/Roles/IContextEnvironment.cs`
- `src/nextorm.core/DataContext/DataContext.cs`
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`
- **New planned partial:** `src/nextorm.core/Query/QueryCommand.EagerLoading.cs`
- `src/nextorm.core/Builders/EntityBuilder.cs`
- `src/nextorm.core/Builders/EntityBuilderExtensions.cs`
- `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`
- `src/nextorm.core/DataContext/QueryPlanner.cs`
- `src/nextorm.core/DataContext/Cache/QueryPlan.cs`
- `src/nextorm.core/Query/QueryPlanEqualityComparer.cs`

**Tests**
- `tests/nextorm.core.tests/EagerLoadingModeTests.cs`
- `tests/nextorm.core.tests/EagerLoadingTests.cs`
- `tests/nextorm.core.tests/JoinIntoPlanKeyTests.cs`
- `tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs`
- **New planned files:** `tests/nextorm.{postgres,sqlserver,mysql,clickhouse}.tests/EagerLoadingStrategyTests.cs` — four explicit files, not new projects
- `tests/nextorm.integration.tests/CommonTestSuite.EagerLoading.cs`

**Documentation**
- `docs/advanced/eager-loading.md`
- `docs/ru/advanced/eager-loading.md`
- `docs/specs/roadmap/todo_eager_loading_pipeline.md`
- `docs/specs/comparison/linq2db-comparison.md`
- `docs/specs/roadmap/linq2db-backlog-gap-analysis.md`
- `docs/specs/design/API-NAMING-REVIEW.md`

**Cycle records/evidence**
- `artifacts/pdca/D209/status.md`
- `artifacts/pdca/D209/rv1/manifest.json`
- `artifacts/pdca/D209/rv1/sibling-contract.md`
- Logs, coverage and benchmark outputs under `artifacts/pdca/D209/rv1/`.

`ISqlDialect.cs`, `JoinIntoStitcher.cs`, package versions, generated documentation, and benchmark product code are **not planned edits**. If semantic analysis proves additional wiring files necessary, return the concrete dependency to PLAN; do not silently expand the footprint.

## 5. Execution mode, tests, and priorities

### Execution mode

**Sequential, one tree.** The shared builders, mode contract, and cache identity overlap with #211/#212/#213. Worktrees would not isolate those contracts and would add integration risk. Serialize shared-file edits; independent read-only CHECK evidence gathering may run concurrently.

No commit, push, merge, or new worktree is authorized by this plan.

### Test strategy and variant closure

Use unit tests for resolution/key semantics, provider SQL-generation tests for dispatch/no-SQL guards, and the existing common integration suite for execution/materialization.

The shared test contract is scenario-based: IDs below are stable evidence identifiers, **not invented future test symbols**. The manifest records actual symbols and final locations after implementation.

| Variant ID | Execution-path variant | Closure |
|---|---|---|
| V01 | Legacy Default and no configured default | **Test:** Split behavior and existing API defaults. |
| V02 | Context Split/Single/Auto/Correlated; query absent/default | **Test:** precedence and corresponding dispatch/rejection. |
| V03 | Explicit query choice against a different context default | **Test:** override wins; later query still inherits the unchanged default. |
| V04 | Same explicit modes, incompatible explicit modes, differing load order | **Test:** deterministic merge or existing conflict rejection. |
| V05 | Default passed to context builder; invalid numeric enum values | **Test:** validation; no SQL/cache activity. |
| V06 | No eager loads; one load; multiple/nested loads | **Test:** no-op, resolved-mode propagation, shape identity and materialization. Existing nesting restrictions remain **guards**. |
| V07 | Empty parent result, missing optional child, empty collection, nullable linkage | **Test:** unchanged supported materialization and no spurious children. |
| V08 | Value-type keys and reference/string keys; nullable/default values | **Test:** linking/equality uses existing key semantics, not reference identity or default-value shortcuts. |
| V09 | Supported Single shape and Single shape rejected by `BuildSingleQueryJoins:1229–1243` | **Test:** existing supported path; **guard test:** rejection before SQL. |
| V10 | Auto ordinary shape; explicit Correlated; shape requiring per-parent top-N | **Test/guard:** Split dispatch or early unsupported rejection. Supported top-N execution is **deferred to #211**, trigger: its provider implementation and revised contract pass CHECK in 1.0.9. |
| V11 | JoinInto under every context mode; nullable child and different join kind | **Test:** fixed Single, stitching and plan-key distinctions. |
| V12 | Equivalent resolved plans; changed strategy/path/nesting/join/filter structure; changed parameter value only | **Test:** reuse versus isolation; equality/hash consistency. |
| V13 | Shared `Any`/`Count`/`All` after eager execution and rejected preparation; per-call uncached preparation | **Test:** cache flag unchanged and subsequent caching still functional. |
| V14 | SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse | **Test:** provider generation suites plus real-provider common integration scenarios. No newly advertised correlated capability. |
| V15 | In-memory/default environment implementation | **Test:** legacy default and supported behavior; **guard:** no relational correlated support claim. |
| V16 | MariaDB | **Guard/build:** no new SQL/capability branch; retain inherited MySQL behavior. Independent MariaDB container execution is not claimed as evidence for T209’s provider-independent selection logic. |
| V17 | Cache enabled/disabled through existing supported controls; cold/warm preparation | **Test:** no new sticky state; **benchmark:** cached/prepared paths. |
| V18 | Ordering/top-N/provider flags introduced by siblings | **Guard now** for unsupported semantics; **deferred execution tests** to the introducing sibling, trigger: admission of the variant in its same-milestone plan. |

Coverage: reproduce the project’s collector/report flow using `coverage.settings.xml`. Require **line ≥85% and branch ≥75%** for its configured scope, even though non-main CI merely warns. Global coverage does not substitute for the scenario matrix. Add targeted tests for every changed dispatch/guard branch.

### P1 matrix

P1 is fixed by construction; CHECK cannot downgrade it:

- R209-01 through R209-08 and R209-12: public compatibility, dispatch, rejection, shared-state/cache invariants, and collection contract.
- Every changed line in strategy validation/resolution, child propagation, cache equality/hash, and pre-execution guards.
- V02–V05, V09–V13, and every new branch needed to preserve result correctness.
- Required integration provider execution and performance/coverage evidence obligations remain mandatory even where an individual documentation or reporting line is not an execution-path P1 line.

R209-09 documentation remains a release acceptance obligation. No project class-priority table was supplied; do not invent one.

## 6. Documentation

**Touched now:**

- EN/RU eager-loading pages at `:43` and `:83`: exact names, precedence, default, conservative Auto table, Correlated/top-N rejection, JoinInto exception, migration-compatible enum values.
- Draft pipeline: settle §15 naming/placement; make §3 match the E1 rule; preserve #211 ownership and cache obligations.
- Remove “strategy is out of scope” entries at:
  - `linq2db-comparison.md:233,277`
  - `linq2db-backlog-gap-analysis.md:54,88`
- Update naming registry at `API-NAMING-REVIEW.md:55,5682–5691`.

**Not touched:** guide numbering/TOCs, unrelated guides, generated `docs/api` or `_site`, provider capability claims beyond this contract. No public link to `docs/specs`.

## 7. Performance and reconnaissance

### Performance: required

This changes query-path resolution and plan-cache identity (`EntityBuilderExtensions.cs:489–518`, `QueryPlan.cs:8`, `QueryPlanEqualityComparer.cs:199–205,550–556`); it is not one-time administrative work.

Exact acceptance command:

`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`

- Exactly **seven completed cases**.
- Wall-clock command gate **≤240 seconds per run**.
- Compare each applicable cached/prepared ratio between baseline and candidate.
- Ratio growth **>20%** requires investigation and a recorded disposition, not an automatic performance failure. Smaller deltas are treated as machine noise, not asserted improvements.

### Pinned oracle and obtainability

Use a **fresh source-based baseline**, not an unidentified historical report:

- Product baseline: supplied commit `58025147`, record its full resolved commit ID.
- Export it outside the working tree using `git archive`; no worktree/branch/commit needed.
- Pin the acceptance harness, project/tool configuration, and seven-case identities from committed sources. Record source commit IDs and content hashes.
- Candidate: record HEAD plus the complete uncommitted diff and its hash.
- Keep SDK/runtime, machine, configuration and command constant.

The supplied, already-verified seven-case acceptance command is the source-availability evidence accepted by PLAN. It is **not** baseline timing evidence. No future test symbol or old artifact is assumed.

**Oracle-redefinition boundary:** this plan defines fresh paired measurements as decisive evidence. It does not require a missing pre-existing “pinned report.” If exporting the pin reveals that the seven-case harness cannot build/run against that baseline, mark E209-09 **GAP** before product DO; do not invent results or turn an unproven harness port into an ordinary implementation prerequisite. Return the exact incompatibility for an oracle revision.

### Reconnaissance: no solution-selection spike

The preserved facts establish the existing enum, dispatch paths, precedence precedent, and single strategy consumer. No experiment is needed to choose an interface hierarchy or speculative provider SQL.

Baseline source/run verification is required evidence preparation, **not renewed discovery**. DO uses Roslyn for actual semantic edits and impact resolution; it must not reopen the preserved findings without a new observed contradiction.

## 8. Versioned evidence contract — rv=1

**Contract:** `D209-eager-strategy`, revision **rv=1**.  
**Manifest:** `artifacts/pdca/D209/rv1/manifest.json`.  
**Status file:** `artifacts/pdca/D209/status.md`.

No earlier authored contract exists; therefore **no supersession**. A later justified revision must explicitly supersede rv1, preserve these row/requirement IDs and obligations, and add IDs for new variants.

### Exact command catalogue

Commands are planned invocations, not executed results.

| ID | Invocation |
|---|---|
| C01 | `git rev-parse 58025147^{commit}` and `git ls-tree -r 58025147 -- benchmarks/nextorm.benchmark scripts/validate_inner_loop.py` |
| C02 | `dotnet build -c Debug` |
| C03 | `dotnet test tests/nextorm.core.tests -c Debug` |
| C04 | `dotnet test tests/nextorm.sqlite.tests -c Debug` |
| C05 | `dotnet test tests/nextorm.postgres.tests -c Debug` |
| C06 | `dotnet test tests/nextorm.sqlserver.tests -c Debug` |
| C07 | `dotnet test tests/nextorm.mysql.tests -c Debug` |
| C08 | `dotnet test tests/nextorm.clickhouse.tests -c Debug` |
| C09 | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` |
| C10 | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect "dotnet test -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D209/rv1/coverage.cobertura.xml` |
| C11 | `reportgenerator -reports:artifacts/pdca/D209/rv1/coverage.cobertura.xml -targetdir:artifacts/pdca/D209/rv1/coverage-report -reporttypes:Html\;TextSummary` |
| C12 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` — once in the exported baseline, once in the candidate tree |
| C13 | `dotnet docfx docs/docfx.json` |
| C14 | `git diff --check` and `git diff -- src/nextorm.core tests docs` |
| C15 | `scripts/validate_inner_loop.py manifest` |

Use the existing validator’s schema and invocation convention; do not modify it to make incomplete evidence pass. Load the integration-running skill before C09/C10. Start/recheck Podman and rerun as prescribed if the socket is absent.

### Required rows

Every row below has `rv=1`. `A` means `artifacts/pdca/D209/rv1/`. All sources are **planned**, except the explicitly supplied gather anchors and source-availability evidence.

| Row ID / requirements | Required check/scenario; evidence kinds and sources | Exact invocation; required result/log | Expected artifacts | Owner | Observable applicability |
|---|---|---|---|---|---|
| **E209-01** / R01,R12 | Pin and source obtainability; git source inventory, supplied seven-case command | C01; exit 0; resolved commit and declarable harness/validator sources. Record seven-case inventory with baseline preparation, not invented names. | `A/source-pin.json`, source inventory, `A/manifest.json` | coder | Always, before product DO |
| **E209-02** / R01,R02,R10 | Production/test compile, nullable/analyzer/API compatibility | C02; exit 0; no warnings or errors | `A/build.log` | coder | Always |
| **E209-03** / R01–R08 | V01–V13,V15,V17; unit results and actual final test locations | C03; exit 0; named scenarios passed, none silently omitted | `A/core-tests.log`, scenario-to-test map | coder | Always |
| **E209-04** / R03–R06,R08 | V09–V11,V14; SQL recording and zero-command rejection evidence | C04; exit 0; positive command traces and zero SQL for rejected requests | `A/sqlite-tests.log`, SQL/no-SQL evidence references | coder | Always |
| **E209-05** / R03–R07,R10 | Provider matrix and fail-closed portability; provider test results | C05–C08; every command exit 0; no newly enabled correlated capability | Four provider logs under `A/` | coder | Always; all four named providers |
| **E209-06** / R03,R06,R10 | Results/materialization on SQLite, PostgreSQL, SQL Server, MySQL, ClickHouse | C09; exit 0; required providers actually executed, no provider skips accepted | `A/integration.log`, provider execution/count inventory | coder | Always; container absence is failure to obtain evidence, not N/A |
| **E209-07** / R10 | Configured line/branch coverage plus changed-branch scenario mapping | C10,C11; both exit 0; line ≥85%, branch ≥75%; coverage scope recorded | `A/coverage.cobertura.xml`, `A/coverage-report/`, threshold record | coder | Always |
| **E209-08** / R07,R08 | Cache equality/hash and sticky-cache isolation; unit/scenario evidence plus diff review | C03,C14; exit 0; V12/V13 passed; no per-call sticky cache mutation | `A/cache-evidence.md`, mapped test results, reviewed diff | check | Always; cache/query path changes |
| **E209-09** / R11 | Fresh pinned baseline and candidate; BDN results, case identity and elapsed time | C12 in both trees; both exit 0; identical seven complete cases; each ≤240s | `A/perf/baseline/`, `A/perf/candidate/`, source hashes, timing and ratio table | coder | Always; source/run obtainability failure is explicit GAP |
| **E209-10** / R11 | Investigation of cached/prepared ratio growth | C12 outputs plus recorded measurement review; all ratios calculated; every >20% finding investigated and disposition justified | `A/perf/investigation.md` | check | Always for ratio review; individual investigations apply when measured growth >20% |
| **E209-11** / R01,R09 | Exact names, EN/RU consistency, scope removals, source formatting | C13,C14; exit 0; DocFX succeeds; documented names/rules match; CRLF review recorded | `A/docs.log`, `A/docs-review.md`, `A/format-review.md` | coder + check | Always |
| **E209-12** / R12 | Sibling reconciliation and collection ALL-barrier | CHECK reviews `A/sibling-contract.md` against the three sibling plans; result must be explicit compatible acknowledgement, not assumed agreement | `A/sibling-contract.md`, barrier decision in status | collection orchestrator + check | Always before ALL-barrier |
| **E209-13** / R01–R12 | Complete machine-valid evidence inventory and final verdict | C15; exit 0; no missing applicable rows, invalid references, or unexplained obligations | `A/manifest.json`, `A/manifest-validation.log`, CHECK report | check | Always before PASS |

Coverage collection may also produce duplicate integration evidence; it does not waive E209-06’s provider accounting.

### CHECK re-gather budget

Owner: **check**. Budget: **two targeted re-gather requests total per CHECK attempt**, restricted to existing output/source references or one precisely identified missing observation.

- Missing logs first trigger targeted evidence recovery, not a new DO iteration or contract revision.
- Exhaustion without decisive evidence produces an explicit GAP; no PASS by inference.
- Newly measured content defects may justify corrective DO or replan.
- Only a substantive changed plan increments `r`; clarification does not reset `n`. No fourth failed CHECK attempt of the same revision.

## 9. Findings, gaps, and risks

### Fix now versus deferred

| Finding/scope | Decision |
|---|---|
| Partial explicit mode API; no spec strategy field/default/key field | Fix now: D209.2–D209.4. |
| Conflicts, invalid enum values, Single restrictions | Preserve and test now; do not remove guards. |
| Shared-command sticky-cache risk | Fix/prevent now, with V13 regression evidence. |
| Docs’ strategy-out-of-scope statements and open naming | Fix now. |
| Per-parent top-N/correlated SQL | Deferred **only to active #211 in 1.0.9**; E1 guard implemented now. |
| Behavior introduced by #212/#213 | Their active scope, not guessed here; T209 freezes their integration obligations. |
| Existing settled architecture findings | No reopening without a new observation. |

### What the task did not specify

| Gap | Closure |
|---|---|
| Exact names and configuration placement | Decision in §2; no options object. |
| Literal draft Auto heuristic | Explicit conservative E1 oracle; draft reconciled now. No claimed fidelity to unprovided text. |
| Provider correlated capabilities | Not assumed; fail-closed E1. #211 owns measured capability admission. |
| Existing historical pinned performance report | Oracle redefined to fresh paired, pinned-source runs. Missing old report is not a blocker. |
| Actual future test symbols/locations | Planned scenario IDs now; runner-discovered symbols/final locations recorded after implementation. |
| Full implementation dependencies beyond preserved anchors | Roslyn during DO; any extra required write path returns a concrete dependency to PLAN. |
| Detailed E4/E5 content | No invented feature scope; their plans must acknowledge the shared contract before the barrier. |

### Risks and confidence

High confidence in the enum/no-hierarchy choice, existing dispatch reuse, precedence precedent, and cache-state prohibition. The new source-compatibility default getter and child propagation need compilation and semantic tests; they are decisions, not already verified implementation facts.

Principal risks:

- incomplete load-shape equality or inconsistent hashing;
- re-resolving defaults during child execution;
- silently admitting top-N without correlated semantics;
- sibling changes overwriting the shared contract;
- baseline harness incompatibility or benchmark timeout;
- provider suites appearing green through skips.

Mitigations are explicit P1 tests, source-based pinning, pre-execution guards, provider execution accounting, and sequential shared-file ownership.

### Re-entry classification

This is an initial PLAN, not DO → PLAN; the resource error requires no semantic classification or revision increment.

If DO later reports a problem:

- additive wiring prerequisite: add an active dependency; original D209 stays active/blocked with unchanged acceptance;
- acceptable implementation detail: record a risk without weakening criteria;
- evidence deficiency: targeted scout/re-gather first; persistent low confidence recommends escalation under trigger 5;
- genuine authority/resource blocker: recommend the orchestrator invoke `escalate`.

Coder writes the complete status and rv1 contract first. In normal mode, wait for explicit user `go` before product DO; in autonomous mode, continue only after the mandatory gate-1 obligations and collection reconciliation are satisfied.

## Preserved gather evidence (file:line)

Current code — explicit strategy choice partially exists:
- src/nextorm.core/EagerLoadMode.cs:8 — public enum EagerLoadMode { Default=0, SplitQuery=1, SingleQuery=2 } (XML-doc on type+members).
- src/nextorm.core/Builders/EntityBuilder.cs:563 — LoadWith(..., EagerLoadMode mode = EagerLoadMode.Default); EnsureValidEagerLoadMode:603 (ArgumentOutOfRangeException); ResolveEagerLoadMode:625 (conflicting explicit mode -> NotSupportedException:633).
- Builders/EntityBuilderExtensions.cs:489,:496,:518 dispatch; EntityBuilder.BuildSingleQueryJoins:1227 (NotSupportedException:1229-1243); CreatePairCommand:1345.
- Builders/EntityBuilderEagerLoading.cs:65-88 — EagerLoadSpec has NO strategy field; Execute:196/204; BuildChild:221.
- JoinInto: EntityBuilder.cs:662 (joinType :683); single-query CreateJoinIntoPairCommand:1335; Builders/Joins/JoinIntoStitcher.cs:60.
- DI/DataContextBuilder.cs:13 — no options object; UseX pattern (:34/:124, :63/:191, :73/:206); NO eager default.
- Precedence precedent: Query/QueryCommand.QueryPreparer.cs:28-33 (cmd.X ?? cmd._dataContext.X); DataContext/Roles/IContextEnvironment.cs:34-71; EntityBuilder.DataProvider:93.
- Plan key: DataContext/Cache/QueryPlan.cs:8 — no strategy/load-shape field today; Query/QueryPlanEqualityComparer.cs:199-205,550-556.
- No LinqOptions/DefaultEagerLoadingStrategy/Correlated/Auto anywhere; IEagerLoadSpec has exactly 1 implementation -> a plain enum suffices (no strategy hierarchy/interface).

Spec docs/specs/roadmap/todo_eager_loading_pipeline.md (draft under review): §3:54-70 (strategies {Split,Single,Correlated,Auto}, Auto rule :65, fail-closed :68, JoinInto fixed Single :70); §4:72-95 (ordering/top-N); §9:130-134 (strategy into plan-cache key, no sticky Cache=false); §11:141-148 (frontend matrix); §12:152-155 (acceptance); §13:159-161 (docs EN+RU + two out-of-scope removals); §15:172-175 (open naming/placement); §16:179 (#209=E1, #211=E3, #212=E4, #213=E5).

Docs: docs/advanced/eager-loading.md (:43 Mode selection, :83 Single-query) + docs/ru/advanced/eager-loading.md (:43,:83); remove "strategy" out-of-scope at docs/specs/comparison/linq2db-comparison.md:233,:277 and docs/specs/roadmap/linq2db-backlog-gap-analysis.md:54,:88; registry docs/specs/design/API-NAMING-REVIEW.md:55,:5682-5691.

Tests: tests/nextorm.core.tests/EagerLoadingModeTests.cs (:260 sticky-cache, :341 conflict, :393 api-surface), EagerLoadingTests.cs, JoinIntoPlanKeyTests.cs:38,:54; tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs (SqlRecordingInterceptor:18; :45,:82,:224,:285); provider JoinInto SQL-gen (postgres/sqlserver/mysql/clickhouse); integration tests/nextorm.integration.tests/CommonTestSuite.EagerLoading.cs.

Perf acceptance (nextorm overlay, mandatory for query-path/plan-cache changes): dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance (exactly 7 cases; <=4 min gate; >20% cached-vs-prepared ratio growth -> investigate, not hard fail). Evidence contract: rv=1, manifest artifacts/pdca/D209/rv1/manifest.json, validated by scripts/validate_inner_loop.py manifest.

## Next allowed step

PLAN r=1 is written and `plan_state: ready`. The orchestrator STOPS here at the PLAN→DO boundary (COLLECTION TASK PLAN); DO is NOT started in this phase. Next: the collection parent dispatches DO for T209 against this r=1 plan and rv=1 evidence contract (D209.1–D209.8), beginning with D209.1 (status/artifacts + perf source pin) before any product edits.
