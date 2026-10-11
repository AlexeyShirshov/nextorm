# T209 — #209 eager-load strategy (explicit strategy + auto-dispatcher)

- task_id: T209
- issue: #209 — https://github.com/AlexeyShirshov/nextorm/issues/209
- collection: 1.0.9-todo (docs/specs/status/collection-1.0.9-todo.md)
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=3
- baseline ref: branch `1.0.9` @ `58025147`; r=2/r=3 production base `dd5d08fb`
- phase: ACT — cycle done (N=1, r=3, n=2/3; CHECK PASS). Prior DO: r=3 n=2 corrective `D:T209-required-tests`, tests-only; r=3 is a wording-only revision, no production mechanism change; unit D209-NL
- DO complete: rv3 boundary @ `5e9f6756` (production base `dd5d08fb`); build 0W/0E; suites core 2270 / sqlite 1532 / postgres 874 / sqlserver 791 / mysql 351 / clickhouse 639 all 0 failed; integration all 5 providers executed, 0 failed, 0 "is not available"; coverage line 88.5% / branch 80.5%; benchmark 7/7 cases, no ratio >20%. rv3 n=2 corrective adds the async-writer / projection / `EagerLoadShape(object?)` evidence: core `~EagerTerminalGuard` 212/0, sqlite `~EagerTerminalGuard` 199/0, `~EagerLoadShape` 5/0 (all exit 0).
- deferral: the P (collection TASK PLAN) STOPPED at the PLAN→DO boundary was the plan-authoring phase; DO is now dispatched against r=3
- plan_state: done
- evidence contract: rv=2 (manifest `artifacts/pdca/D209/rv2/manifest.json`, `contract_rv: 2`, rows E209-01..E209-21 all `met`; rv3 artifacts under `artifacts/pdca/D209/rv3/`, CHECK report `artifacts/pdca/D209/rv3/check-report.md`)
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

## DO (dispatched by pdca-executor, r=1, n=1)

2026-10-11T04:52:45Z | DO started — D209.1 (scope validated exit 0; source-pin.json + sibling-contract.md written; baseline 58025147 archived to /tmp/nextorm-d209-baseline). Product edits begin with D209.2.
2026-10-11T10:03:30Z | DO | r1 | n1 | PLAN r=1 approved footprint extension `src/nextorm.core/Query/QueryCommand.Clone.cs:62-64` (necessary plan-key wiring: clone must carry EagerLoadMode/ResolvedEagerLoadMode/EagerLoadShape); r=1 unchanged.
2026-10-11T10:10:20Z | DO | r1 | n1 | D209.5 tests (V01–V17) added: `EagerLoadingModeTests`, `EagerLoadingTests`, `JoinIntoPlanKeyTests`, sqlite `EagerLoadingSqlGenerationTests`, four provider `EagerLoadingStrategyTests`, integration `CommonTestSuite.EagerLoading` Auto↔Split. Build 0W/0E. Inner loop: core EagerLoad 130/0, core JoinInto 116/0, sqlite EagerLoad 22/0, provider EagerLoad 4/0 each (amended scope validated exit 0). Integration scenario compile-verified; container execution deferred to DO→CHECK boundary.
2026-10-11T10:10:20Z | DO | r1 | n1 | defect `per-parent-topn-guard-after-parent-sql`: the per-parent top-N guard fires in `BuildChild` after the parent statement (R209-05 "before SQL execution"). Incorrectly-limited child SQL is never issued; single-query variant is truly zero-SQL. Recorded as DO→PLAN candidate; no production change made in this test-only DO.
2026-10-11T10:10:50Z | DO | r1 | n1 | D209.5 committed | ed9ac341 "#209 T209 DO r1: eager-load strategy D209.2-D209.5 + variant tests V01-V17"
2026-10-11T05:21:12Z | DO | r1 | n1 | defect `per-parent-topn-guard-after-parent-sql` fixed (fix applied #1; r=1, n unchanged): the per-parent top-N is now rejected during preparation, before SQL execution and before cache insertion. The split parent command records `QueryCommand.HasPerParentEagerTopN` (set in `EntityBuilderExtensions.MarkPerParentTopN`, from the reused `IEagerLoadSpec.RequiresPerParentTopN` child build); `QueryCommand.QueryPreparer` throws `NotSupportedException("*Limit/Offset/Page*")` before `_isPrepared`/plan key. `BuildChild` guard kept as defence in depth. Single/Auto reject identically; Single keeps its existing `ToJoinIntoSpec` rejection. Evidence: `artifacts/pdca/D209/rv1/core-eager-inner.log` 130/0, `core-joininto-inner.log` 116/0, `sqlite-eager-inner.log` 26/0; sqlite `Split_PerParentTopN_IsRejectedBeforeTheChildSql` now asserts 0 executed statements and 0 `QueryPlanStore` entries; new negative coverage: context Auto/Correlated + async + later query caching. Build 0W/0E.
2026-10-11T05:21:42Z | DO | r1 | n1 | fix committed | d69e6569 "#209 T209 DO r1 fix: reject per-parent eager child top-N during preparation (pre-SQL, pre-cache)"
2026-10-11T05:26:53Z | DO | r1 | n1 | D209.6 docs | EN/RU eager-loading pages (mode table + numeric values, context default, precedence, conservative Auto, Correlated/per-parent top-N fail-closed before SQL/cache, fixed-single JoinInto); todo_eager_loading_pipeline §3 Auto rule + §15 names/placement settled to shipped symbols; linq2db-comparison + linq2db-backlog-gap-analysis scope-removals replaced; API-NAMING-REVIEW EL2 block + rc2 rows annotated; docfx exit 0 | artifacts/pdca/D209/rv1/docs.log; commit 369830fd
2026-10-11T05:33:20Z | DO | r1 | n1 | D209.7 boundary build+generation sweep: C02 `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s); C03-C08 full-project dialect suites all exit 0: core 2087/0/0, sqlite 1337/0/1(approved), postgres 855/0/0, sqlserver 772/0/0, mysql 332/0/0, clickhouse 620/0/0 | artifacts/pdca/D209/rv1/build-boundary.log; artifacts/pdca/D209/rv1/{core,sqlite,postgres,sqlserver,mysql,clickhouse}-tests.log
2026-10-11T05:33:45Z | DO | r1 | n1 | D209.7 real-provider integration (DOCKER_HOST, one class at a time, all exit 0, zero provider-availability skips; grep 'is not available'=0): Postgres 673 exec/0F/26 capability-skip, SqlServer 673/0F/48, MySql 673/0F/80, ClickHouse 148/0F/0, Sqlite 673/0F/40 | artifacts/pdca/D209/rv1/integration-{postgres,sqlserver,mysql,clickhouse,sqlite}.log
2026-10-11T05:34:04Z | DO | r1 | n1 | D209.7 evidence contract: evidence.json written; `validate_inner_loop.py report` exit 0. Adjustment (no coverage weakened): the validator caps unfiltered boundary `dotnet test` sweeps at one, so the core full-project suite is the single sweep in executions[] and the other five full-project suites are recorded verbatim under additional_boundary_runs[]. git diff --check exit 0; git status clean pre-commit | artifacts/pdca/D209/rv1/evidence.json; artifacts/pdca/D209/rv1/format-review.md
2026-10-11T05:38:00Z | DO | r1 | n1 | D209.7 coverage (C10,C11) exit 0: `dotnet tool run dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D209/rv1/coverage.cobertura.xml` (DOCKER_HOST set; total 10161, failed 0, succeeded 9963, skipped 198, wall 116s) then reportgenerator exit 0; configured scope line 88.4% (50969/57607) >= 85%, branch 80.5% (27414/34043) >= 75% | artifacts/pdca/D209/rv1/coverage-collect.log; artifacts/pdca/D209/rv1/coverage-report.log; artifacts/pdca/D209/rv1/coverage-report/Summary.txt; artifacts/pdca/D209/rv1/coverage-threshold.md
2026-10-11T05:40:50Z | DO | r1 | n1 | D209.7 acceptance benchmark (C12) exit 0 in both trees: baseline wall 55s / 7 cases, candidate wall 52s / 7 cases (each <=240s, 0 failures); cached/prepared ratios Cached_ToList/Prepared_ToList 2.1366->2.0600 (-3.58%), Cached_PlanOnly_Param/Prepared_ToList 0.6411->0.6247 (-2.56%); no ratio >20% growth, no GAP; no optimization conclusion drawn | artifacts/pdca/D209/rv1/perf/baseline/console.log; artifacts/pdca/D209/rv1/perf/candidate/console.log; artifacts/pdca/D209/rv1/perf/ratios.md
2026-10-11T05:41:12Z | DO | r1 | n1 | D209.7 evidence manifest (C15): rv1 manifest written with E209-01..E209-13 (9 coder rows met, 4 check rows open); `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D209/rv1/manifest.json` exit 0; validator unmodified | artifacts/pdca/D209/rv1/manifest.json; artifacts/pdca/D209/rv1/manifest-validation.log
2026-10-11T10:46:43Z | DO→CHECK | r1 | n1 | DO complete (D209.1–D209.7); D209.8 CHECK submitted. Commits ed9ac341, 2700a59c, d69e6569, 36956f9f, 369830fd, e9737c85, 5addd257, 874b78bd, f90ffee3. Evidence: build 0W/0E; suites core 2087 / sqlite 1337 / postgres 855 / sqlserver 772 / mysql 332 / clickhouse 620 all 0 failed; integration all 5 providers executed 0 failed 0 "is not available"; coverage line 88.4% / branch 80.5%; benchmark 7/7 cases, no >20% ratio. Re-validated: report + manifest validators exit 0, 0 FAIL lines. | artifacts/pdca/D209/rv1/evidence-validation.log; artifacts/pdca/D209/rv1/manifest-validation.log

## Defect history (durable)

| Defect key | Observed at r/n | Fixes applied | Evidence | Last occurrence / disposition |
|---|---|---|---|---|
| per-parent-topn-guard-after-parent-sql | r1/n1 | 1 | artifacts/pdca/D209/rv1/sqlite-eager-inner.log | fixed r1/n1 (guard moved to preparation) |
| fi-f2-joininto-loadwith-reroute | r1/n1 | 1 | artifacts/pdca/D209/rv1/core-f14-red.log; artifacts/pdca/D209/rv1/sqlite-f14-red.log | fixed r1/n2 — `EntityBuilder.SingleQuery` no longer treats a JoinInto as forcing the merged LoadWith path; mixed combination takes the baseline pair+split dispatch under every context default |
| f3-aoore-paramname | r1/n1 | 1 | artifacts/pdca/D209/rv1/core-f3-red.log | fixed r1/n2 — `EagerLoadResolver.Resolve` attributes `ParamName` to `requested` vs `contextDefault` |
| f4-resolvedchild-racy-memo | r1/n1 | 1 | artifacts/pdca/D209/rv1/core-eager-inner-n2.log | fixed r1/n2 — per-context `ConditionalWeakTable<IDataContext, Lazy<EntityBuilder<TChild>>>` (ExecutionAndPublication) replaces the unsynchronized `_resolvedChild` field |
| f13-eagerloadshape-identity-tests | r1/n1 | 1 | artifacts/pdca/D209/rv1/core-eager-inner-n2.log | fixed r1/n2 — direct `EagerLoadShape` equality/hash + V12 plan-cache tests added |
| f14-mixed-dispatch-regression | r1/n1 | 1 | artifacts/pdca/D209/rv1/core-eager-inner-n2.log; artifacts/pdca/D209/rv1/sqlite-eager-inner-n2.log | fixed r1/n2 — core + SQLite (4 context defaults) mixed-dispatch regressions added, red→green demonstrated |

## DO n=2/3 corrective round (r=1, plan unchanged)

2026-10-11T11:07:00Z | CHECK FAIL→DO | r1 | n2/3 | CHECK r=1 returned FAIL with confirmed P1 defects F1/F2 (JoinInto+LoadWith mixed-shape reroute), F3 (AOORE ParamName), F4 (unsynchronized `_resolvedChild`), F13/F14 (missing identity/dispatch regressions); plan r=1 unchanged, no replan.
2026-10-11T11:07:00Z | DO | r1 | n2/3 | F1/F2 fixed: `EntityBuilder.SingleQuery` gates the merged path (explicit SingleQuery, or context-default Single with no JoinInto); `JoinInto + LoadWith(Default)` keeps the baseline JoinInto pair command + split child loads under Split/Single/Auto/Correlated; `CreateJoinIntoPairCommand` stays pinned SingleQuery.
2026-10-11T11:07:00Z | DO | r1 | n2/3 | F3 fixed: `EagerLoadResolver.Resolve` attributes `ArgumentOutOfRangeException.ParamName` to `requested` (query override) vs `contextDefault` per the actual offending source.
2026-10-11T11:07:00Z | DO | r1 | n2/3 | F4 fixed: `EagerLoadSpec.ResolveChild` resolves through a per-context `ConditionalWeakTable<IDataContext, Lazy<EntityBuilder<TChild>>>` (ExecutionAndPublication) — no unsynchronized mutable cache, single child build preserved.
2026-10-11T11:07:00Z | DO | r1 | n2/3 | F13/F14 tests: `EagerLoadShape` empty/default, order-sensitivity, equal⇒equal hash, collision-consistency; V12 param-value reuse vs child-filter structure isolation; core + SQLite mixed JoinInto+LoadWith dispatch (4 context defaults) + explicit SingleQuery merge.
2026-10-11T11:07:00Z | DO | r1 | n2/3 | red→green: with F1/F2 reverted the F14 regressions fail (core SingleQuery=true; SQLite Split/Single/Auto 1 merged statement, Correlated throws); with F3 reverted the resolver ParamName test fails (contextDefault vs requested); restored → both green.
2026-10-11T11:07:00Z | DO | r1 | n2/3 | verify: build 0W/0E (`build-attempt-n2-final.log`); core ~EagerLoad 137/0 (`core-eager-inner-n2.log`), core ~JoinInto 118/0 (`core-joininto-inner-n2.log`), sqlite ~EagerLoad 31/0 (`sqlite-eager-inner-n2.log`).
2026-10-11T11:12:00Z | DO | r1 | n2/3 | n=2 DO→CHECK boundary re-bind @ HEAD 8bdcee2afdc63155cc48022b72fa6c5fcbc98c2a (production changed in n=2; all boundary evidence re-run fresh). build 0W/0E (`n2/build.log`); full-project generation suites core 2094/0/0, sqlite 1342/0/1, postgres 855/0/0, sqlserver 772/0/0, mysql 332/0/0, clickhouse 620/0/0 (`n2/{core,sqlite,postgres,sqlserver,mysql,clickhouse}-tests.log`).
2026-10-11T11:12:00Z | DO | r1 | n2/3 | n=2 integration (DOCKER_HOST, one class at a time, all exit 0, zero provider-availability skips; grep 'is not available'=0): Postgres 673/0F/26S, SqlServer 673/0F/48S, MySql 673/0F/80S, ClickHouse 148/0F/0S, Sqlite 673/0F/40S (`n2/integration-*.log`).
2026-10-11T11:12:00Z | DO | r1 | n2/3 | n=2 coverage (DOCKER_HOST): collect exit 0 (total 10173 / failed 0 / succeeded 9975 / skipped 198), reportgenerator exit 0; line 88.4% (50981/57610) ≥85%, branch 80.5% (27422/34051) ≥75% (`n2/coverage.cobertura.xml`, `n2/coverage-report/Summary.txt`, `n2/coverage-threshold.md`, `n2/branch-delta.md`).
2026-10-11T11:12:00Z | DO | r1 | n2/3 | n=2 acceptance benchmark (C12) exit 0 in both trees: baseline wall 60s / 7 cases, candidate wall 60s / 7 cases (each ≤240s, 0 failures); cached/prepared ratios Cached_ToList/Prepared 2.0170→1.9610 (-2.78%), Cached_PlanOnly/Prepared 0.6134→0.5980 (-2.51%); no ratio >20% growth, no GAP (`n2/perf/{baseline,candidate}/console.log`, `n2/perf/ratios.md`).
2026-10-11T11:12:00Z | DO | r1 | n2/3 | n=2 audit/slop scan (manual; slopwatch NOT installed): introduced suppressions 0/0 (`#pragma warning disable`/`[SuppressMessage]`/`NoWarn`/`UnconditionalSuppressMessage`), TODO/FIXME/HACK/NotImplemented 0, `.Result`/`.Wait()`/broad-catch/magic-number 0 production; 4 test-only `dynamic` SQL-capture lines; ratio 0 production findings / 511 added src lines; branch coverage of changed types recorded, pre-feature baseline branch measurement unavailable (`n2/suppression-scan.md`, `n2/branch-delta.md`).
2026-10-11T11:12:00Z | DO→CHECK | r1 | n2/3 | n=2 boundary evidence re-validated @ HEAD 8bdcee2afdc63155cc48022b72fa6c5fcbc98c2a: `validate_inner_loop.py report` exit 0 (`n2/evidence-validation.log`), `validate_inner_loop.py manifest` exit 0 (`n2/manifest-validation.log`); manifest tree/pins set to change base d12b740ea804322b65367fbb6ef734b94db76013 + HEAD, coder rows point at n2 artifacts, CHECK rows E209-08/10/12/13 remain open; D209.8 re-submitted to CHECK.

2026-10-11T06:15:39Z | DO | r1 | n2/3 | corrective evidence artifacts produced for CHECK-owned rows: cache-evidence.md, scenario-map.md, docs-review.md, perf/investigation.md, n2/docs.log (docfx exit 0), n2/manifest-validation.log (exit 0); sibling acknowledgements (D211/D212/D213 rv=1) + sibling-contract.md table filled; D209-corrective-1.md created. CHECK rows E209-08/10/12/13 remain open. | artifacts/pdca/D209/rv1/n2/{cache-evidence,scenario-map,docs-review}.md; artifacts/pdca/D209/rv1/n2/perf/investigation.md; docs/specs/status/D209-corrective-1.md

## DO r1 n=2/3 escalation corrective round — 2nd fix of the JoinInto+LoadWith class (r=1, plan unchanged)

2026-10-11T11:32:00Z | ESCALATE (strong) → DO | r1 | n2/3 | CHECK r=1 n=2 confirmed the JoinInto + LoadWith defect class (R209-05): the `JoinIntos` branch of `MaterializeList*` never called `MarkPerParentTopN`, so an unsupported **per-parent top-N** child was fail-open (empty parent) or threw only in `BuildChild:299-303` after the pair SQL ran and was plan-cached, and an explicit `LoadWith(..., Correlated)` was silently reinterpreted (never reached `EagerLoadResolver.Resolve`). Plan r=1 unchanged; this is the **2nd fix of the class** — a further return triggers STOP, not a 3rd fix.
2026-10-11T11:32:00Z | DO | r1 | n2/3 | implemented the escalation decision **as-is**: one side-effect-free `EntityBuilderEagerLoading.ValidateLoadWithForJoinInto<TEntity>` called as the first statement of `JoinIntoStitcher.Execute`, `JoinIntoStitcher.ExecuteAsync` (before the first SQL `await`) and the shared `JoinIntoStitcher.Stitch`. It rejects an explicit `Correlated` (same explicit-vs-default criterion as the `EntityBuilder.SingleQuery` getter `EntityBuilder.cs:151-155`; identical message via shared `EagerLoadResolver.CorrelatedNotSupported`) and a child `Limit/Offset/Page` (shared `HasPerParentTopN` predicate + `PerParentTopNNotSupported`, reused by `MarkPerParentTopN`/`RequiresPerParentTopN` and the `BuildChild` defence-in-depth guard). No command, no cache write, no `QueryCommand.Cache` mutation; the dispatcher was **not** touched; non-JoinInto paths unchanged.
2026-10-11T11:32:00Z | DO | r1 | n2/3 | tests: SQLite `JoinInto + LoadWith(Limit|Offset|Page)` × empty/non-empty parent × sync/async → `NotSupportedException`, **0** statements, `QueryPlanStore` empty; `JoinInto + LoadWith(..., Correlated)` sync/async → throw before SQL, no cache write; regression `JoinInto + Default` under context-default `Correlated` → exactly 2 statements (pair + split); explicit SingleQuery merge unchanged; core in-memory guards.
2026-10-11T11:32:00Z | DO | r1 | n2/3 | red→green: with the guard disabled, sqlite ~EagerLoad 46 total/**14 failed** (12 paged + 2 Correlated) and core ~EagerLoad **1 failed**; restored → sqlite ~EagerLoad 46/0, core ~EagerLoad 141/0, core ~JoinInto 122/0.
2026-10-11T11:32:00Z | DO | r1 | n2/3 | verify: build 0W/0E; core ~EagerLoad 141/0, core ~JoinInto 122/0, sqlite ~EagerLoad 46/0; full core 2098/0/0, full sqlite 1357 total / 1356 succeeded / 1 approved skip. Evidence: `artifacts/pdca/D209/rv1/n2/{build-final-guard,core-eager-inner-final,core-joininto-inner-final,sqlite-eager-inner-final,core-tests-final,sqlite-tests-final}.log`; red `n2/{core-eager-red-guard,sqlite-eager-red-guard}.log` → green `n2/*-green-guard.log`.
2026-10-11T11:32:00Z | DO | r1 | n2/3 | committed | dd5d08fb (source + tests); status/corrective record in the follow-up #209 commit

## Final DO→CHECK boundary re-bind — production revision dd5d08fb (r=1, n=2/3)

2026-10-11T11:47:00Z | DO→CHECK | r1 | n2/3 | final boundary bound to dd5d08fb (full `dd5d08fbf4b634b944d277da1016499b1d43e025`). Worktree HEAD is `d1cbae73441de9453873c5c5f5f2091e5d4a1709`, a docs-only status commit with no compiled-input delta vs dd5d08fb; every compiled input is pinned to dd5d08fb. All boundary evidence re-run fresh under `artifacts/pdca/D209/rv1/final/` (r=1 unchanged, no replan).
2026-10-11T11:47:00Z | DO | r1 | n2/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s), 41.16 s → `final/build.log`. Dialect suites `--no-build` all exit 0: core 2098/0F/0S, sqlite 1356/0F/1S, postgres 855/0F/0S, sqlserver 772/0F/0S, mysql 332/0F/0S, clickhouse 620/0F/0S → `final/{core,sqlite,postgres,sqlserver,mysql,clickhouse}-tests.log`.
2026-10-11T11:47:00Z | DO | r1 | n2/3 | integration (DOCKER_HOST, one class at a time, all exit 0, `grep "is not available"`=0 in every log): Postgres 673 total/0F/26S, SqlServer 673/0F/48S, MySql 673/0F/80S, ClickHouse 148/0F/0S, Sqlite 673/0F/40S → `final/integration-{postgres,sqlserver,mysql,clickhouse,sqlite}.log`.
2026-10-11T11:47:00Z | DO | r1 | n2/3 | coverage (DOCKER_HOST) collect exit 0: total 10192 / failed 0 / succeeded 9994 / skipped 198, wall 1m10s; reportgenerator exit 0; configured scope line 88.5% (51018/57643) ≥85%, branch 80.5% (27442/34071) ≥75% → `final/coverage.cobertura.xml`, `final/coverage-report/`, `final/coverage-threshold.md`.
2026-10-11T11:47:00Z | DO | r1 | n2/3 | acceptance benchmark (C12) exit 0 in both trees: baseline wall 49 s / 7 cases, candidate wall 55 s / 7 cases (each ≤240 s, 0 failures); cached/prepared ratios Cached_ToList/Prepared +0.83%, Cached_PlanOnly/Prepared -3.68%; no ratio >20% growth, no GAP → `final/perf/{baseline,candidate}/console.log`, `final/perf/ratios.md`.
2026-10-11T11:47:00Z | DO | r1 | n2/3 | guard-test regression sanity (all 0 failed): F13 `~EagerLoadShape` 4, core F1/F2/F14 `~JoinInto_With` 9, F3 `~Resolver_` 6, F4 `~Poison` 5, SQLite F1/F2/F14 `~JoinInto_With` 20 (44 focused cases) → `final/guard-tests.md`, `final/guard-*.log`.
2026-10-11T11:47:00Z | DO→CHECK | r1 | n2/3 | re-bound: `validate_inner_loop.py report artifacts/pdca/D209/rv1/evidence.json` exit 0 (`final/evidence-validation.log`); `validate_inner_loop.py manifest` exit 0 (`final/manifest-validation.log`). Manifest pins baseline `d12b740ea804322b65367fbb6ef734b94db76013` + HEAD `dd5d08fbf4b634b944d277da1016499b1d43e025`; coder rows E209-01..07,09,11 `met` → `final/`; CHECK rows E209-08/10/12/13 remain `open`. D209.8 re-submitted to CHECK.

## Approved PLAN r=2 — escalate-mandated revised plan (P209-r2)

`plan_revision: r=2`, cycle `N=1`, attempt `n=1`. r=1 is exhausted (two failed CHECK attempts on the JoinInto+LoadWith class; the second returned via strong-tier escalate). This section supersedes the r=1 execution content only where stated; it **preserves R209-01..R209-12, E1, V01–V18 and E209-01..E209-13 unchanged**.

### Single product unit D209-NL

**D209-NL** (guards + tests + docs) — close the R209-05 silent-drop for **non-list terminals**: a `LoadWith` per-parent top-N (`Limit`/`Offset`/`Page`) or an explicit `LoadWith(..., Correlated)` must be rejected early (`NotSupportedException`) before any command build / shared-command mutation / cache insertion / SQL. No new eager materialization.

The original **D209** stays **active and blocked on D209-NL**; its acceptance criteria and remaining scope are unchanged. D209-NL is an **additive** active dependency — not a `superseded→replacement` of D209.

### Change

- Reuse the existing side-effect-free `EntityBuilderEagerLoading.ValidateLoadWithForJoinInto<TEntity>` (`src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:506`). It throws on an explicit `Mode == Correlated` and on any load spec whose `RequiresPerParentTopN` is true; it ignores a context-default Correlated and does not touch `QueryCommand.Cache`.
- Call it at the terminal boundary on the **original** builder, before `WithoutJoinIntos`/projection:
  - `EntityBuilder<TEntity>.ToParentCommand()` (`src/nextorm.core/Builders/EntityBuilder.cs:1285`) — covers `ToEnumerable`, `ToAsyncEnumerable`, `HashSet`, `Dictionary`, `First/FirstOrDefault`, `Single/SingleOrDefault`, `Last/LastOrDefault`, `Any/AnyAsync`, `AnyCommand`, `Prepare`, `WriteCsv/WriteJson` extension terminals.
  - `EntityBuilder<TEntity>.SelectParent<TResult>(exp)` (`:1287`) — covers `Count`/`CountAsync`/`LongCount`/`LongCountAsync` and every aggregate (`Min`/`Max`/`Avg`/`Sum` cores at `EntityBuilderExtensions.cs:1408–1434`, entry points `:1002–1336`).
- **Not** itself guarded: `WithoutJoinIntos` (`:1267`), `ToCommand` (`:449`), `CreatePairCommand` (`:1441`) — although `CreatePairCommand`'s parent-paging branch calls `ToParentCommand()` (`:1520`) and therefore reaches the guard indirectly (same `NotSupportedException`, no false rejection; candidate #4 wording aligned). `MarkPerParentTopN` (`EntityBuilderExtensions.cs:503`) and `MaterializeList` (`:522/:546`) are not modified. List-like terminals (`ToList`/`ToListAsync`, `ToArray`/`ToArrayAsync`) keep their existing stitching/rejection behavior.

### New variants (added to the preserved contract)

| Variant ID | Execution-path variant | Closure |
|---|---|---|
| V19 | `JoinInto + LoadWith(child.Limit)` on non-list terminals | **Test:** `NotSupportedException`, zero SQL/cache. |
| V20 | plain `LoadWith(child.Limit)` on non-list terminals | **Test:** `NotSupportedException`, zero SQL/cache. |
| V21 | plain `LoadWith(child.Offset)` | **Test:** `NotSupportedException`, zero SQL/cache. |
| V22 | plain `LoadWith(child.Page)` | **Test:** `NotSupportedException`, zero SQL/cache. |
| V23 | explicit `LoadWith(..., Correlated)` on non-list terminals | **Test:** `NotSupportedException`, zero SQL/cache. |
| V24 | post-throw state: cache count unchanged, `QueryCache.AnyCommand` not replaced/mutated, later valid query still caches | **Test:** SQLite assertions. |
| V25 | positive controls: plain `LoadWith` (no top-N) on non-list terminals unchanged; no eager loads → no dispatch; context-default `Correlated` does not trigger the guard; list/`ToArray` paths unchanged | **Test:** core + SQLite. |

Each negative variant is exercised across the terminal families (`ToEnumerable`, `ToAsyncEnumerable`, HashSet, Dictionary, First/FirstOrDefault, Single/SingleOrDefault, Last/LastOrDefault, Any/AnyAsync, Count/LongCount, aggregates, Prepare, WriteCsv/WriteJson), sync **and** async, asserting `NotSupportedException` with zero command-build / cache / executed-SQL events (interceptor / recording connection). A terminal family is marked structural N/A only where the overload does not exist.

### Evidence contract rv2 (supersedes rv1)

- Contract `D209-eager-strategy` revision **rv=2**; base path `B=artifacts/pdca/D209/rv2/`.
- Preserves all rv1 row/requirement IDs (`E209-01..E209-13`) and their obligations. Adds planned rows **E209-14..E209-21**:
  - **E209-14** (V19–V23, non-list terminal families, sync+async): core guard tests → `B/core-guard.log`.
  - **E209-15** (V19–V23, zero-SQL/zero-cache): SQLite guard tests with interceptor + plan store → `B/sqlite-guard.log`.
  - **E209-16** (V14 portability): four provider guard files → `B/{postgres,sqlserver,mysql,clickhouse}-guard.log`.
  - **E209-17** (V24): rejected preparation leaves the cache clean and the shared `AnyCommand` unmutated; later valid queries still cache → `B/cache-guard.md`.
  - **E209-18** (V25): positive controls (plain LoadWith, no-eager, context-default Correlated, list/ToArray) → `B/guard-positive.md`.
  - **E209-19** (red→green): revert guard → negative test fails (SQL/cache observed); restore → passes; both exit codes → `B/red/`, `B/green/`.
  - **E209-20** (R209-05 wording): the non-list per-parent top-N and explicit Correlated now fail before parent SQL, before command build/cache insertion → `B/no-sql-evidence.md`.
  - **E209-21** (R209-09): EN/RU eager-loading docs updated without promising eager materialization; docfx exit 0 → `B/docs.log`.
- Integration A (rv1) and B (rv2) never rewrite each other.

### Recurrence rule

A further return of the `per-parent-topn-guard` defect class → **escalate/STOP**; no 4th attempt. No fourth failed CHECK attempt of the same revision.

### DO r=2 n=1 — inner-loop re-run and docs (r=2, plan unchanged)

2026-10-11T07:17:59Z | DO | r2 | n1/3 | docs parity: EN terminal-boundary non-list-validation/rejection-before-materialization sentence mirrored into `docs/ru/advanced/eager-loading.md`; both eager-loading docs CRLF-normalized → `docs/advanced/eager-loading.md`, `docs/ru/advanced/eager-loading.md`.
2026-10-11T07:17:59Z | DO | r2 | n1/3 | `dotnet docfx docs/docfx.json` exit 0, 0 error(s) 2 warning(s) → `artifacts/pdca/D209/rv2/docs.log`.
2026-10-11T07:17:59Z | DO | r2 | n1/3 | affected build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s) after final guard restore → `artifacts/pdca/D209/rv2/build.log`.
2026-10-11T07:17:59Z | DO | r2 | n1/3 | inner loop all `--no-build` exit 0: core `~EagerLoad` 142, core `~JoinInto` 123, core `~EagerTerminalGuard` 159, sqlite `~EagerLoad` 46, sqlite `~EagerTerminalGuard` 164 → `rv2/{core-eager,core-joininto,core-guard,sqlite-eager,sqlite-guard}.log`.
2026-10-11T07:17:59Z | DO→CHECK | r2 | n1/3 | `validate_inner_loop.py brief artifacts/pdca/D209/rv2/scope.json` exit 0; `validate_inner_loop.py report artifacts/pdca/D209/rv2/evidence.json` exit 0 → `artifacts/pdca/D209/rv2/evidence-validation.log`.

## rv2 DO→CHECK boundary re-bind — candidate `1.0.9` @ 68828204 (r=2, n=1/3)

2026-10-11T12:28:00Z | DO→CHECK | r2 | n1/3 | rv2 boundary bound to candidate `1.0.9` @ `68828204bf524d739cb744f00c5b7b08e24e9d9b` (change diff base `d12b740ea804322b65367fbb6ef734b94db76013`; perf baseline @ `58025147bcb7d4b12d2978c077f91cd02ba22268`, exported `/tmp/nextorm-d209-baseline`). r=2 unchanged, no replan. All boundary evidence re-run fresh under `artifacts/pdca/D209/rv2/`.
2026-10-11T12:28:00Z | DO | r2 | n1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s) → `rv2/build-boundary.log`.
2026-10-11T12:28:00Z | DO | r2 | n1/3 | dialect suites `--no-build` all exit 0: core 2257/0F/0S, sqlite 1521/0F/1S, postgres 866/0F/0S, sqlserver 783/0F/0S, mysql 343/0F/0S, clickhouse 631/0F/0S → `rv2/{core,sqlite,postgres,sqlserver,mysql,clickhouse}-tests.log`.
2026-10-11T12:28:00Z | DO | r2 | n1/3 | integration (DOCKER_HOST, one class at a time, all exit 0, `grep "is not available"`=0 in every log): Postgres 673/0F/26S, SqlServer 673/0F/48S, MySql 673/0F/80S, ClickHouse 148/0F/0S, Sqlite 673/0F/40S → `rv2/integration-{postgres,sqlserver,mysql,clickhouse,sqlite}.log`.
2026-10-11T12:28:00Z | DO | r2 | n1/3 | coverage (DOCKER_HOST) collect exit 0 (total 10559 / failed 0 / succeeded 10361 / skipped 198), reportgenerator exit 0; configured scope line 88.4% (51012/57649) ≥85%, branch 80.5% (27436/34071) ≥75% → `rv2/coverage.cobertura.xml`, `rv2/coverage-report/`, `rv2/coverage-threshold.md`.
2026-10-11T12:28:00Z | DO | r2 | n1/3 | acceptance benchmark exit 0 in both trees: baseline wall 44 s / 7 cases, candidate wall 55 s / 7 cases (each ≤240 s, 0 failures); cached/prepared ratios Cached_ToList/Prepared -0.84%, Cached_PlanOnly_Param/Prepared -1.88%; no ratio >20% growth → `rv2/perf/{baseline,candidate}/console.log`, `rv2/perf/ratios.md`.
2026-10-11T12:28:00Z | DO→CHECK | r2 | n1/3 | manifest frozen `artifacts/pdca/D209/rv2/manifest.json` (contract_rv 2, `status_file` this file, `required_rows` E209-01..E209-21, tree @ 68828204); coder rows E209-01..07,09,11,14..20 `met` → rv2 artifacts; CHECK/collection rows E209-08,10,12,13,21 `open`. `validate_inner_loop.py manifest` exit 0 → `rv2/manifest-validation.log`; `validate_inner_loop.py report artifacts/pdca/D209/rv2/evidence.json` exit 0 → `rv2/evidence-validation.log`. D209-NL re-submitted to CHECK.
2026-10-11T12:28:00Z | DO→CHECK | r2 | n1/3 | frozen rv2 row indexing: E209-14..18 = V19–V23 non-list terminal-shaped negatives (core + SQLite guard logs; E209-16 also carries four-provider portability guard logs), E209-19 = V24 cache/shared-command invariant, E209-20 = V25 positive controls, E209-21 = terminal-surface Roslyn audit (open). This per-variant indexing refines the §Evidence contract rv2 bullet grouping (which grouped V19–V23 into E209-14/15/16 and put docs at E209-21); CHECK to reconcile if the plan intends the grouped indexing.
2026-10-11T12:28:00Z | DO | r2 | n1/3 | committed | #209 rv2 boundary evidence + manifest freeze (status record; `artifacts/**` is gitignored, evidence stays local).

## rv2 CHECK re-gather — DO evidence completion (r=2, n=1/3)

2026-10-11T13:19:00Z | DO | r2 | n1/3 | CHECK re-gather: completed the five rv2 evidence artifacts under `artifacts/pdca/D209/rv2/` — `terminal-surface-audit.md` (E209-21: guard sites `EntityBuilder.cs:1292` ToParentCommand / `:1302` SelectParent; every non-list terminal mapped to a guarded call chain; 7 `ValidateLoadWithForJoinInto` callers via Roslyn; `ToArray`/`ToList` confirmed list-like; **no unguarded terminal found**), `cache-evidence.md` (E209-08: `QueryPlan`/`QueryPlanEqualityComparer` equality+hash include `ResolvedEagerLoadMode`+`EagerLoadShape`; V12/V13 reuse + sticky-cache tests; no `Cache` mutation in the diff; `QueryPlanStore` `[ThreadStatic]` determinism caveat), `suppression-scan.md` (src: 0 suppressions / 0 TODO-FIXME-HACK / 0 `.Result`/`.Wait()`/broad-catch/dynamic; 4 test-only `dynamic` + 1 test-only `catch (Exception)`; 603 added src lines, 3891 added total LOC; ratio 0.000; `slopwatch` NOT installed), `stryker.md` (mutation run **blocked**: 34200 mutants whole-assembly, 122 Safe-Mode instrumentation compile errors (117 CS0165), 10528 tests across 12 assemblies, per-test coverage capture Dubious, EXIT=143; **no score** — precedents `docs/specs/status/api-create-builders-147-1.md:59`, `docs/specs/status/collection-rc2-reclaim-C-evidence.md:206`), `manifest-row-crosswalk.md` (§Evidence-contract rv2 duty grouping ↔ frozen E209-01..21; no row renumbered). | `rv2/{terminal-surface-audit,cache-evidence,suppression-scan,stryker,manifest-row-crosswalk}.md`
2026-10-11T13:19:00Z | DO | r2 | n1/3 | cleanup: untracked `stryker-d209.json` removed (never tracked — `git ls-files` errors); lingering `Stryker.CLI` process terminated (`pkill -f Stryker.CLI`); final `rv2/stryker-run{,-aborted1,-aborted2}.log` captured with `EXIT=143`; `rv2/stryker/` empty (only `.gitignore`), no mutation report. | `rv2/stryker-run.log`
2026-10-11T13:19:00Z | DO | r2 | n1/3 | committed | #209 rv2 CHECK re-gather evidence note. No product code changed; r=2, n=1/3 unchanged; no replan. Status file CRLF-normalized; explicit-path commit only; not pushed.

## Approved PLAN r=3 — wording-only revision (supersedes r=2; no production mechanism change)

`plan_revision: r=3`, cycle `N=1`, attempt `n=1`. r=2 is **superseded** by this wording-only revision. r=3 changes **no production mechanism**: the shipped guard/memoization implementation after r=2 (production base `dd5d08fb`) is unchanged. The evidence contract stays **`contract_rv: 2`**; the rv2 manifest `artifacts/pdca/D209/rv2/manifest.json` and its `E209-01..E209-21` rows are preserved **losslessly** — no row renumbered, removed, added or status-changed. Only the acceptance **wording** carried by two rows is reformulated:

| Row (unchanged id) | Reformulated wording (verbatim) |
|---|---|
| **E209-01** — R209-01 (**check-owned**) | "public signatures and valid-query results/SQL unchanged; the child factory is invoked at most once per `(spec, context)` at first terminal validation; factory side effects are a documented assumption (factory must be pure and idempotent)." |
| **E209-20** — V25 (**check-owned**) | "Memoization strictly per `(spec, context)`, no static state, memoized child reused during execution. Perf one-time per spec/context, hot path unaffected." |

Every other row keeps its rv2 wording unchanged. Both reformulated rows stay **check-owned** (CHECK renders the verdict; DO supplies the test/doc evidence).

### Additive unit D209-NL stays active (not superseded→replacement)

**D209-NL** (guards + tests + docs) remains the **active** product unit with its **unchanged remaining work/criteria** — the non-list terminal rejection of a per-parent top-N child (`Limit`/`Offset`/`Page`) or an explicit `LoadWith(..., Correlated)`, before any command build / shared-command mutation / cache insertion / SQL. The original **D209** likewise stays **active and blocked on D209-NL**. This r=3 revision is an **additive** reformulation of R209-01 and V25 wording; it is **not** a `superseded→replacement` of D209 or D209-NL, adds no new active unit, and leaves all rv2 acceptance criteria and remaining scope intact.

### Registered test/doc evidence (planned, not yet passing)

- **D209-T (tests).** Extend `tests/nextorm.core.tests/EagerTerminalGuardTests.cs`, `tests/nextorm.sqlite.tests/EagerTerminalGuardTests.cs` and the four provider `EagerTerminalGuardTests.cs` (postgres/sqlserver/mysql/clickhouse parity) to pin, across the terminal matrix (rejected **and** valid), sync and async: the factory is invoked **≤1 per `(spec, context)`** at first terminal validation (any second invocation fails); a rejection produces **0 SQL / 0 plan-cache activity / 0 child SQL** (any activity fails); factory exceptions **propagate unchanged**; **two contexts resolve independently** (cross-context reuse fails); a valid **non-paged** `LoadWith` retains baseline results/SQL; `Limit`, `Offset` and `Page` are rejected alike. Tighten the two r=2 relaxed assertions (`tests/nextorm.core.tests/EagerLoadingSingleQueryTests.cs:236`, `tests/nextorm.core.tests/EagerLoadingSingleQueryP1Tests.cs:814`) to the reformulated acceptance without altering valid-query expectations.
- **D209-D (docs).** EN `docs/advanced/eager-loading.md` and RU `docs/ru/advanced/eager-loading.md` gain the factory-purity/idempotence sentence; `dotnet docfx docs/docfx.json` → exit 0 → `artifacts/pdca/D209/rv3/docs.log`.
- Both streams are **planned** evidence; nothing here is claimed already passing.

### Journal

2026-10-11T08:23:13Z | DO | r3 | n1/3 | r=3 plan persisted: wording-only revision (no production mechanism change); D209-NL stayed active with unchanged remaining work/criteria (additive, not superseded→replacement); contract_rv 2 and E209-01..21 preserved losslessly; only E209-01 (R209-01) and E209-20 (V25) wording reformulated and both stay check-owned | `docs/specs/status/D209-eager-strategy-1.md`
2026-10-11T08:23:13Z | DO | r3 | n1/3 | r=3 recorded in the companion corrective record | `docs/specs/status/D209-corrective-1.md`
2026-10-11T08:28:13Z | DO | r3 | n1/3 | D209-T done: extended the six `EagerTerminalGuardTests.cs` (core, SQLite, postgres, sqlserver, mysql, clickhouse) with the terminal-matrix factory-count / 0-SQL / 0-cache / exception-propagation / per-context-independence pins and the valid non-paged baseline + Limit/Offset/Page rejected-alike pins; tightened the two r=2 relaxed assertions (`EagerLoadingSingleQueryTests.cs`, `EagerLoadingSingleQueryP1Tests.cs`) to the per-`(spec, context)` acceptance (second context resolves its own child). **Factory-count pins:** for `Limit`/`Offset`/`Page`/`JoinInto+Limit` the factory is invoked **exactly 1** per `(spec, context)` at the first terminal validation (a second invocation fails the assertion); for explicit `Correlated` it is invoked **0** (rejected before the factory). **Rejection:** core plan cache empty (`QueryPlanStore.Values`), SQLite interceptor **0 statements / 0 plan-cache entries / 0 child SQL** across the whole matrix; valid non-paged `LoadWith` keeps the parent-only baseline (1 statement) and reuses the memo; a throwing factory propagates the same exception instance unchanged; two contexts resolve independently (shared counter 1→2). | `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,clickhouse}.tests/EagerTerminalGuardTests.cs`, `tests/nextorm.core.tests/EagerLoadingSingleQuery*.cs`
2026-10-11T08:28:13Z | DO | r3 | n1/3 | scope: `python3 scripts/validate_inner_loop.py brief artifacts/pdca/D209/rv3/scope.json` exit 0 (amendment `FullyQualifiedName~EagerTerminalGuard` validated_before_run true); rv2 scope also still exit 0 | `artifacts/pdca/D209/rv3/scope-validation.log`
2026-10-11T08:28:13Z | DO | r3 | n1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s) | `artifacts/pdca/D209/rv3/build.log`
2026-10-11T08:28:13Z | DO | r3 | n1/3 | inner loop `--no-build` all exit 0: core `~EagerTerminalGuard` 172/0F, core `~EagerLoad` 142/0F, core `~JoinInto` 123/0F, sqlite `~EagerTerminalGuard` 175/0F, sqlite `~EagerLoad` 46/0F, provider `~EagerTerminalGuard` postgres/sqlserver/mysql/clickhouse 19/0F each | `artifacts/pdca/D209/rv3/{core-guard,core-eager,core-joininto,sqlite-guard,sqlite-eager,postgres-guard,sqlserver-guard,mysql-guard,clickhouse-guard}.log`
2026-10-11T08:28:13Z | DO | r3 | n1/3 | D209-D done: EN factory-purity/idempotence sentence added to `docs/advanced/eager-loading.md` and the RU mirror to `docs/ru/advanced/eager-loading.md`; `dotnet docfx docs/docfx.json` exit 0, 0 error(s) 2 warning(s) | `docs/advanced/eager-loading.md`, `docs/ru/advanced/eager-loading.md`, `artifacts/pdca/D209/rv3/docs.log`

## rv3 DO→CHECK boundary re-bind — candidate `1.0.9` @ 5e9f6756 (r=3, n=1/3)

2026-10-11T08:37:08Z | DO→CHECK | r3 | n1/3 | rv3 boundary bound to candidate `1.0.9` @ `5e9f6756bd9c8d9c90d14ea58ad685108689428b` (change diff base `d12b740ea804322b65367fbb6ef734b94db76013`; perf baseline @ `58025147bcb7d4b12d2978c077f91cd02ba22268`, exported `/tmp/nextorm-d209-baseline`). r=3 unchanged, no replan. All boundary evidence re-run fresh under `artifacts/pdca/D209/rv3/`. Production identical to r=2 `dd5d08fb`; r=3 adds tests+docs only.
2026-10-11T08:37:08Z | DO | r3 | n1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s) → `rv3/build-boundary.log`.
2026-10-11T08:37:08Z | DO | r3 | n1/3 | dialect suites `--no-build` all exit 0: core 2270/0F/0S, sqlite 1532/0F/1S, postgres 874/0F/0S, sqlserver 791/0F/0S, mysql 351/0F/0S, clickhouse 639/0F/0S → `rv3/{core,sqlite,postgres,sqlserver,mysql,clickhouse}-tests.log`.
2026-10-11T08:37:08Z | DO | r3 | n1/3 | integration (DOCKER_HOST, one class at a time, all exit 0, `grep "is not available"`=0 in every log): Postgres 673/0F/26S, SqlServer 673/0F/48S, MySql 673/0F/80S, ClickHouse 148/0F/0S, Sqlite 673/0F/40S → `rv3/integration-{postgres,sqlserver,mysql,clickhouse,sqlite}.log`.
2026-10-11T08:37:08Z | DO | r3 | n1/3 | coverage (DOCKER_HOST) collect exit 0 (total 10615 / failed 0 / succeeded 10417 / skipped 198), reportgenerator exit 0; configured scope line 88.5% (51021/57649) ≥85%, branch 80.5% (27437/34071) ≥75% → `rv3/coverage.cobertura.xml`, `rv3/coverage-report/`, `rv3/coverage-threshold.md`.
2026-10-11T08:37:08Z | DO | r3 | n1/3 | acceptance benchmark exit 0 in both trees: baseline wall 54 s / 7 cases, candidate wall 58 s / 7 cases (each ≤240 s, 0 failures); cached/prepared ratios Cached_ToList/Prepared -1.77%, Cached_PlanOnly_Param/Prepared -3.73%; no ratio >20% growth → `rv3/perf/{baseline,candidate}/console.log`, `rv3/perf/ratios.md`.
2026-10-11T08:37:08Z | DO→CHECK | r3 | n1/3 | rv2 manifest refreshed in place: `tree` @ `5e9f6756`, `contract_rv` 2 unchanged; coder rows E209-01..07,09,11,14..20 `met` → rv3 evidence paths; CHECK/collection rows E209-08,10,12,13,21 remain `open` unchanged. `validate_inner_loop.py manifest artifacts/pdca/D209/rv2/manifest.json` exit 0 → `rv3/manifest-validation.log`; `validate_inner_loop.py report artifacts/pdca/D209/rv3/evidence.json` exit 0 → `rv3/evidence-validation.log`. D209-NL re-submitted to CHECK.
2026-10-11T08:37:08Z | DO | r3 | n1/3 | committed | #209 T209 r3 n1/3 DO→CHECK boundary re-bind + rv2 manifest tree-pin refresh + rv3 evidence/status record; explicit-path commit, CRLF-normalized, not pushed.

## r=3 n=2 corrective DO — `D:T209-required-tests` (tests-only, no product mechanism change)

CHECK r=3 n=1 returned **FAIL** on evidence completeness only (no demonstrated production regression)
and routed `D:T209-required-tests` at `r3/n2`, no priority reduction. This DO adds tests, persistence
and status/metadata only; **production is unchanged** (identical to `dd5d08fb` / rv3 `5e9f6756`).

2026-10-11T13:50:00Z | DO | r3 | n2/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s) | `artifacts/pdca/D209/rv3/build-required-tests.log`
2026-10-11T13:50:00Z | DO | r3 | n2/3 | async writers: `WriteCsvAsync`/`WriteJsonAsync` added to the core + SQLite terminal matrices and dedicated tests assert `NotSupportedException` before any SQL, 0 plan-cache entries, no child SQL, and a 0-byte destination (strongest available "no command construction" observation; no `CreateCommand` counter exists per `rv2/async-zerobuild.md:9-12`). Closes `rv2/async-zerobuild.md:84-93`. | `tests/nextorm.core.tests/EagerTerminalGuardTests.cs`, `tests/nextorm.sqlite.tests/EagerTerminalGuardTests.cs`
2026-10-11T13:50:00Z | DO | r3 | n2/3 | projection terminal overloads: `FirstOrFirstOrDefaultCommand(exp)` / `SingleOrSingleOrDefaultCommand(exp)` added to the matrices + dedicated reject-before-SQL/cache tests. | same test files
2026-10-11T13:50:00Z | DO | r3 | n2/3 | `EagerLoadShape.Equals(object?)` branch closure: direct `object?` equality unit test (true+false) covering L167 2/2 and the L154 null-parts/length-mismatch sub-branches (targeted collect → L154 100% 4/4, L167 100% 2/2). | `tests/nextorm.core.tests/EagerLoadingModeTests.cs`, `artifacts/pdca/D209/rv3/eagerloadshape-branch.md`, `rv3/shape.cobertura.xml`
2026-10-11T13:50:00Z | DO | r3 | n2/3 | verify inner (`--no-build`, all exit 0): core `~EagerTerminalGuard` 212/0/0, core `~EagerLoadShape` 5/0/0, sqlite `~EagerTerminalGuard` 199/0/0 | `rv3/{core-guard,core-eagerloadshape,sqlite-guard}.log`
2026-10-11T13:50:00Z | DO | r3 | n2/3 | CHECK dispositions persisted in the rv2 manifest: E209-08 → met, E209-10 → met (rv3 ratio pointer, no >20% investigation), E209-12 → met (D211:503-507/D212:95-99/D213:157-161 + ALL-barrier `collection-1.0.9-todo.md:58`), E209-21 → met (terminal-surface audit); E209-13 remains **open**; `required_rows` E209-01..21 and `contract_rv: 2` unchanged. `validate_inner_loop.py manifest` exit 0. | `rv2/manifest.json`, `rv3/manifest-validation.log`
2026-10-11T13:50:00Z | DO | r3 | n2/3 | candidate crosswalk (#1,#3,#4,#5,#6,#7,#8,#9,#10,#11 + F5/F8/F11) with dispositions | `rv3/candidate-crosswalk.md`
2026-10-11T13:50:00Z | DO | r3 | n2/3 | metadata refresh: this header (r=3 n=2, rv3 counts, `rv=2` contract), `artifacts/pdca/D209/status.md`, collection revision vector → T209 r=3; #4 wording aligned (`CreatePairCommand` `:1441`, parent-paging branch reaches `ToParentCommand()` `:1520`). | `docs/specs/status/D209-eager-strategy-1.md:8-15,523`, `docs/specs/status/collection-1.0.9-todo.md:16,42,60`

## ACT — cycle done (N=1, r=3, n=2/3, contract rv=2)

CHECK r=3 n=2 returned **PASS** (report: `artifacts/pdca/D209/rv3/check-report.md`). The cycle is
`done`; both the original **D209** and the additive unit **D209-NL** are satisfied. This status file
is **kept** (the collection references it). The rv2 evidence contract (`contract_rv: 2`,
`required_rows` E209-01..E209-21) is preserved losslessly; only E209-13 moved `open → met`.

2026-10-11T14:05:00Z | ACT | r3 | n2/3 | CHECK PASS: complete machine-valid evidence inventory E209-01..E209-21, no missing applicable row; E209-13 set `met`; `required_rows` and `contract_rv: 2` unchanged, all other dispositions preserved. `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D209/rv2/manifest.json` **exit 0** → `rv3/manifest-validation.log`; CHECK report attached → `rv3/check-report.md`. | `artifacts/pdca/D209/rv2/manifest.json`; `artifacts/pdca/D209/rv3/check-report.md`
2026-10-11T14:05:00Z | ACT | r3 | n2/3 | acceptance behavior: one eager-load strategy contract spanning configuration, preparation, execution, plan cache and docs — explicit `EagerLoadMode` + context default (`UseEagerLoadMode`; `Default` rejected as a sentinel) + query override precedence `cmd.EagerLoadMode ?? dataContext.EagerLoadMode` + conservative Auto (ordinary `LoadWith` → `SplitQuery`) + **fail-closed** explicit `Correlated` and per-parent top-N (`Limit`/`Offset`/`Page`) on every non-list terminal family (sync + async) before any command build / cache insertion / SQL + fixed-Single `JoinInto`; structural plan-cache identity (`ResolvedEagerLoadMode` + `EagerLoadShape` in equality **and** hashing); no sticky `QueryCommand.Cache` mutation. Declared factory/memoization contract (r=3 wording of R209-01/V25): the child factory is invoked **≤1 per `(spec, context)`** at first terminal validation, the memoized child is reused during execution, no static state, hot path unaffected; factory side effects are a documented assumption (factory must be pure and idempotent). | `rv2/manifest.json` E209-01..E209-21; `rv3/check-report.md`
2026-10-11T14:05:00Z | ACT | r3 | n2/3 | key evidence (rv=2): build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s); six dialect suites 0 failures — core 2270 / sqlite 1532 / postgres 874 / sqlserver 791 / mysql 351 / clickhouse 639; integration all **5** providers executed (Postgres, SqlServer, MySql, ClickHouse, Sqlite), 0 failures, 0 `is not available`; coverage configured scope line **88.5%** (≥85) / branch **80.5%** (≥75); acceptance benchmark **7/7** cases, no cached-vs-prepared ratio growth **>20%**. | `rv3/{build-boundary,core-tests,sqlite-tests,postgres-tests,sqlserver-tests,mysql-tests,clickhouse-tests,integration-*}.log`; `rv3/coverage-threshold.md`; `rv3/perf/ratios.md`
2026-10-11T14:05:00Z | ACT | r3 | n2/3 | revision history: **r=1** (initial PLAN, rv=1) → exhausted after **two failed CHECK attempts** on the JoinInto+LoadWith class (fail-open empty parent / throw only after the pair SQL was cached / explicit `Correlated` silently reinterpreted), returned via strong-tier **escalate** on the 2nd fix → **r=2** escalate-mandated revised plan (rv=2, additive unit `D209-NL`; original D209 stayed active/blocked, **not** superseded→replacement; side-effect-free `ValidateLoadWithForJoinInto` guard at `ToParentCommand()`/`SelectParent()`; variants V19–V25) → **r=3** wording-only revision (R209-01/V25 reformulated; no production mechanism change; `contract_rv` stays 2; rows preserved losslessly) → **r=3 n=2** corrective `D:T209-required-tests` (tests/persistence only, production unchanged) → CHECK PASS. | `D209-eager-strategy-1.md` §PLAN r=1 / §Approved PLAN r=2 / §Approved PLAN r=3; `D209-corrective-1.md`
2026-10-11T14:05:00Z | ACT | r3 | n2/3 | known limitations (declared): (1) **indirect command-construction evidence** — no `CreateCommand` counter exists; "zero command build" is observed via 0 SQL / 0 plan-cache entries / 0-byte destination; (2) **thread-local cache observations** — `QueryPlanStore` is `[ThreadStatic]`, cache assertions are per-thread; (3) **Stryker.NET blocked** (Safe-Mode instrumentation errors, EXIT=143, no mutation score) and **slopwatch absent** (manual suppression scan instead, ratio 0 production findings). | `rv3/candidate-crosswalk.md`; `rv2/cache-evidence.md`; `rv2/stryker.md`; `rv2/suppression-scan.md`
2026-10-11T14:05:00Z | ACT | r3 | n2/3 | committed | #209 T209 ACT: cycle done — manifest E209-13 `met` + CHECK report + status/collection finalization (explicit-path commit, CRLF-normalized, not pushed).
