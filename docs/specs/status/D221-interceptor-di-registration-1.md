---
task: T221 (#221)
title: Interceptor DI registration — builder-scope interceptor resolution
issue: https://github.com/AlexeyShirshov/nextorm/issues/221
milestone: 1.0.9
collection: 1.0.9-todo
group: G1 (single group; no worktree/branch/merge)
selected_variant: pdca-dotnet
cycle: N=1
plan_revision: r=1
evidence_contract_rv: 1
iteration: n=1/3
plan_state: ready
status_path: docs/specs/status/D221-interceptor-di-registration-1.md
baseline_ref: 706a83d1
manifest: artifacts/pdca/D221/rv1/manifest.json
---

# T221 — PLAN r=1 / rv=1 / n=1

**Goal.** Let a DI-created builder obtain interceptors from its own service-provider scope, preserving registration order and DI ownership/lifetimes, without moving resolution into query execution or changing standalone instance registration. Deliver implementation, executable evidence, bilingual documentation and naming registration before the held T53 freeze.

**Minimum solution — three answers:** achieve builder-level resolution; preserve existing signatures, scope ownership, event pipelines and unrelated work; use one ordered registration list and resolve it once per `CreateDataContext()` call.

**In scope:** two overloads, DI-aware builder construction, ordered resolution/deduplication, error boundaries, tests, EN/RU docs, naming/T53 candidate records.

**Out of scope:** global registration; builder `AddInterceptor(sp => …)`; resolving on `DataContext`; provider/event-pipeline refactors; activating T53; new packages/interfaces; commits, branches, worktrees or merges.

## Decisions and unstated requirements

| Approach | Benefits | Cost/risk | Decision |
|---|---|---|---|
| One `List<object>` containing validated instance-or-`Type` entries | One storage concept; preserves mixed order; no public registration model | Requires centralized validation/dispatch | **Choose** |
| Tagged registration class/record plus list | Explicit representation | Extra type/indirection for one consumer | Reject under KISS/second-consumer invariant |
| Keep independent query/connection lists plus type list | Smaller local edits | Multiple sources of truth; mixed ordering/deduplication harder | Reject |

**API:** in `src/nextorm.core/DI/DataContextBuilder.cs`: `public DataContextBuilder AddInterceptor(Type interceptorType)`; `public DataContextBuilder AddInterceptor<T>() where T : class`; `internal DataContextBuilder(IServiceProvider serviceProvider)`. Preserve the existing public parameterless constructor; it is not a new API addition.

**Storage/resolution:** generic delegates to `Type`; instance overloads feed the same list. Centralized creation resolves entries through the captured provider's `GetRequiredService(type)`, completes resolution before invoking the context factory, then uses existing attachment mechanisms. No cached resolved instance, root-provider substitution or query-time resolution.

**Assumption A1 — deduplication:** "by type" means exact registration key: `instance.GetType()` for instances, supplied `Type` for type entries; first registration wins, including mixed entries. Distinct service aliases are not merged by their eventual runtime implementation. Observable and documented, not an accidental optimization.

**Assumption A2 — dual-interface objects:** each retained entry attaches to every interceptor interface it implements. Existing ambiguous dual-interface instance calls still require a cast; new overloads do not silently choose one interface.

**Assumption A3 — eligible service keys:** closed reference types, including interface/abstract service keys, are accepted when assignable to either interceptor interface. Open generic and value-type `Type` entries are rejected for parity with `<T> where T : class`; existing boxed-instance behavior remains supported.

**Errors:** null `interceptorType` → `ArgumentNullException(nameof(interceptorType))`; invalid type → `ArgumentException("Type '<type>' must be a closed reference type assignable to IQueryInterceptor or IConnectionInterceptor.", nameof(interceptorType))`; standalone type entry → `InvalidOperationException("Interceptor type '<type>' requires a DI-backed DataContextBuilder.")`; missing service → native `GetRequiredService` `InvalidOperationException`, without wrapping. Test native messages by exception/type identification, not runtime-specific punctuation.

**Additional assumptions:** transient services follow normal DI behavior, resolving on each creation; DI owns interceptor disposal. Existing factory/keyed/generic builder-producing entrypoints must carry their actual provider. Direct paths that do not construct a builder remain unchanged.

**Closed gaps:** no interface constraint because either interface is valid; no eager resolution because scope/order/error timing matter; no extra factory overload despite the issue title. Unverified internal consumers/tool compatibility are explicit D221-0 prerequisites, not invented facts.

## Acceptance criteria — every criterion includes a negative case

- **R221-01 API/errors:** overloads return the same builder; validation is immediate and leaves registration state unchanged on failure; missing services/standalone type entries fail at creation before factory invocation. Negative: invalid, null, open/value types and missing service cannot produce a context.
- **R221-02 ordering/dedupe:** retained entries execute in registration order within each event pipeline; exact-key first-wins applies to instance/type/mixed registrations; dual-interface entries serve both pipelines. Negative: duplicates neither resolve nor invoke twice.
- **R221-03 DI ownership:** scoped instances are reused within and differ across scopes; singleton identity is shared; transient resolution occurs per creation; standard DI factories and applicable keyed/generic entrypoints work. Negative: no cross-scope capture, eager resolution or builder disposal of DI-owned instances.
- **R221-04 compatibility:** public parameterless construction, existing instance overloads and per-context additions continue working; in-memory results/event behavior match their baseline. Negative: a standalone type entry cannot silently disappear, including on the in-memory path.
- **R221-05 execution regressions:** query/connection events, error/stream/async paths and JSON interception remain intact; container-backed provider checks actually execute. Negative: skipped providers or zero selected tests are not passes.
- **R221-06 delivery/quality:** EN/RU docs and naming/freeze candidates agree with behavior; warning-free build, changed-branch evidence and mutation disposition exist; unrelated edits are unchanged. Negative: missing artifacts, warnings, stale docs or unexplained relevant surviving mutants prevent completion.

## Variant matrix

Names below are planned scenario labels, not claims that new test symbols already exist; new scenarios carry `Category=T221`.

| Variant / scenario label | Closure |
|---|---|
| Query vs connection × generic vs `Type` × DI-backed creation — `V01-KindOverload` | **test**, core; SQLite verifies actual callbacks |
| Query/connection instance overloads; same-builder return — `V02-InstanceCompatibility` | **test**, existing `InterceptorBuilderTests` plus regression cases |
| Typed null instance vs null `Type`; generic `default` type argument | **test**, existing null tests plus `V03-NullType`; **guard**, generic type argument is a compile-time type, not a nullable value |
| Wrong interface / open generic / value-type key — `V04-InvalidType` | **test**, immediate exceptions and unchanged registration |
| Interface/abstract service keys; boxed struct instance — `V05-ServiceKeyParity` | **test**, valid DI keys and preserved instance behavior |
| Missing DI registration; standalone type entry — `V06-CreationErrors` | **test**, exception boundary and factory not called |
| Instance→type and type→instance order; distinct types — `V07-MixedOrder` | **test**, recorded event sequences |
| Duplicate type, duplicate instance type, mixed duplicate; distinct aliases — `V08-Dedupe` | **test**, first wins and resolution counters |
| Dual-interface entry; explicit interface casts — `V09-DualInterface` | **test**, both pipelines; **guard**, existing overload ambiguity remains documented |
| Scoped / singleton / transient; DI-owned disposal — `V10-Lifetime` | **test**, identities, creation counters and disposal ownership |
| Ordinary / keyed / generic builder-producing DI routes; DI service factories — `V11-DiRoutes` | **test**; non-builder direct routes **guard** by Roslyn impact review |
| Standalone instance-only / no interceptors / repeated creation — `V12-Standalone` | **test**, existing core/SQLite cases plus repeats |
| In-memory with instance/type entries and creation errors — `V13-InMemory` | **test**, baseline parity and uniform resolution |
| Sync / async / streaming / failure / connection / JSON — `V14-Execution` | **test**, existing SQLite suite plus targeted JSON scenario |
| SQLite / PostgreSQL / SQL Server / MySQL / ClickHouse — `V15-Providers` | **test**, targeted container-backed integration scenarios; MariaDB-specific behavior **deferred**, trigger: provider-specific change, within 1.0.9 |

## Design review, units and dependencies

| Finding / location | Fix now / deferred + trigger; invariant |
|---|---|
| `DataContextBuilder.cs:281–305`: separate storage cannot express the agreed mixed registrations | **fix now**, one list and centralized processing; DRY/KISS |
| `ServiceCollectionExtensions.cs:122–127,133–160`: builder factories do not capture their provider | **fix now**, internal provider constructor at every applicable creation site; lifetime/SRP |
| `DataContextBuilder.cs:281,296`: overload compatibility and dual-interface dispatch | **fix now**, preserve signatures and test explicit casts/first-wins; LSP |
| `IQueryInterceptor.cs:34–51`, `IConnectionInterceptor.cs:26–30` | **guard**, unchanged event contracts; no new abstractions |
| Public builder inheritance/sealedness | **deferred**, trigger: measured extension-design finding/T53 review in 1.0.9; no sealing change or invented ratio. New test fakes are sealed; no new production class |

**Mode:** sequential units in the current tree, group G1; no worktrees. Shared contract is R221-01–06/A1–A3; DI, tests and docs cannot independently reinterpret it.

- **D221-0 pending — prerequisites/baseline:** capture dirty-worktree and owned-file snapshots, coverage baseline; use Roslyn for list consumers, creation paths and changed-type impact; load integration skill; verify coverage/Stryker/MTP compatibility and predecessor/sibling stability. A genuine incompatibility returns to PLAN with evidence.
- **D221-1 pending — implementation:** builder/list/errors and DI creation changes at the locations above; XML documentation, nullable/warning discipline, CRLF; no dependency-version changes.
- **D221-2 pending — tests:** expand `InterceptorBuilderTests.cs`; add planned `tests/nextorm.core.tests/InterceptorDiRegistrationTests.cs`, SQLite counterpart and `tests/nextorm.integration.tests/CommonTestSuite.InterceptorDiTests.cs`; all V01–V15 closures.
- **D221-3 pending — documentation/tracking:** update the pages/registers below, recording #221 and its verified URL; preserve pre-existing untracked content.
- **D221-4 pending — evidence handoff:** execute frozen checks, populate artifact slots and submit CHECK; this unit does not declare final acceptance.

**Priorities:** all contract rows below are **P1 by construction**: explicit deliverables or changed execution-path invariants. CHECK cannot downgrade them. Unchanged historical/type-sealing/provider-specific findings are deferred, not substituted for P1 obligations.

## Test, documentation and measurement strategy

**Tests:** core units prove registration/resolution/lifetimes; SQLite exercises callbacks and JSON; targeted real-provider integration proves wiring. Existing selectors below cover `InterceptorBuilderTests`, `DependencyInjectionTests`, all ten `InterceptorTests` and `CoreApiContractTests`; selectors selecting zero tests fail.

**Coverage:** capture comparable baseline/after coverage and changed-line/branch disposition. Target every changed executable branch, with any unreachable branch explicitly proved. Report project 85% line/75% branch thresholds as configured: hard gate on `main`, warnings on `1.0.9`; focused coverage is not evidence of full-suite threshold compliance. Comparable existing-selector coverage must not regress.

**Mutation:** Stryker.NET mutates `DataContextBuilder` and `ServiceCollectionExtensions`; kill every non-equivalent mutant affecting changed P1 behavior. Prove equivalent survivors individually; unchanged-area survivors remain separately reported. Tool/runner failure is a prerequisite problem, not waived mutation evidence.

**DO brief `test scope` JSON:** `{"projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.integration.tests"],"selectors":["Category=T221","NextORM.Core.Tests.InterceptorBuilderTests","NextORM.Core.Tests.DependencyInjectionTests","NextORM.Sqlite.Tests.InterceptorTests","CoreApiContractTests"],"files":["src/nextorm.core/DI/DataContextBuilder.cs","src/nextorm.core/DI/ServiceCollectionExtensions.cs","tests/nextorm.core.tests/InterceptorBuilderTests.cs","tests/nextorm.core.tests/InterceptorDiRegistrationTests.cs","tests/nextorm.sqlite.tests/InterceptorDiRegistrationTests.cs","tests/nextorm.integration.tests/CommonTestSuite.InterceptorDiTests.cs"],"rebuild":true,"boundary":"builder registration, DI creation and unchanged interceptor attachment/event contracts","rationale":"Core proves service-resolution semantics; SQLite observes lifecycle/order/JSON; real-provider tests detect construction-boundary regressions. No provider implementation change is planned."}`

**Docs:** change `docs/infrastructure/02-interceptors.md` + RU; `docs/getting-started/04-dependency-injection.md` + RU; check/update EN infrastructure toc, actual RU toc and both index links only as needed. Update `API-NAMING-REVIEW.md:4632–4649`, relevant `code-smells-review.md:6545`, T53 status/candidate records and collection candidate list `:66`.

**T53 candidates:** both new public `AddInterceptor` signatures; record exact signatures and #221 in naming/T53 records, with **freeze HELD**. Internal ctor is not a public candidate; parameterless ctor is preserved, not newly added. Do not add public links to internal specs or activate analyzers/freeze.

**Performance:** cached-path acceptance **N/A**. Registration/storage at `DataContextBuilder.cs:281–305` and DI construction at `ServiceCollectionExtensions.cs:122–127` are context-construction work, not per-row work. Linear deduplication is acceptable here; no unsupported speed claim. Roslyn/diff evidence must confirm query/plan-cache paths unchanged; otherwise return to PLAN and add the required exactly-seven-case `--anyCategories=acceptance` ≤4-minute measurement.

**Reconnaissance:** no design prototype: agreed issue plus existing DI/event seams selects the minimal solution. D221-0 performs bounded compatibility probes only; observable pass = consumers accounted for, scoped-provider routes identified and tools produce usable reports.

## Frozen evidence contract rv=1

**Persist before DO:** `artifacts/pdca/D221/rv1/manifest.json`; all sources below are planned. Every row carries stable requirement/row IDs, scenario, kinds/sources, exact invocation reference, success/log rules, artifact slot, owner, applicability and `rv=1`. No supersession at initial revision.

**Command dictionary:** run from repository root unless stated; retain command, exit code, selected/passed/failed/skipped counts and complete stdout/stderr in the named log.

- **B:** `dotnet build src/nextorm.core/nextorm.core.csproj -c Debug`.
- **U:** `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~NextORM.Core.Tests.InterceptorBuilderTests|FullyQualifiedName~NextORM.Core.Tests.DependencyInjectionTests"`; **UN:** `dotnet run --project tests/nextorm.core.tests -c Debug -- -trait "Category=T221"`.
- **Q:** `dotnet run --project tests/nextorm.sqlite.tests -c Debug -- -class NextORM.Sqlite.Tests.InterceptorTests`; **QN:** `dotnet run --project tests/nextorm.sqlite.tests -c Debug -- -trait "Category=T221"`.
- **I:** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~CoreApiContractTests|Category=T221"`. Load the integration skill first; if missing, start `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start`, wait/recheck and retry; failure returns with diagnostic evidence.
- **C(p):** `dotnet-coverage collect --settings coverage.settings.xml --output TestResults/D221/rv1/coverage-p.cobertura.xml --output-format cobertura "bash artifacts/pdca/D221/rv1/coverage-driver.sh p"`; substitute literal `baseline` or `after` for `p`. Frozen driver executes U/Q/I-existing in both phases and UN/QN/I-feature after; records comparable-selector and added-test contributions separately.
- **R(p):** `reportgenerator -reports:TestResults/D221/rv1/coverage-p.cobertura.xml -targetdir:TestResults/D221/rv1/coverage-p -reporttypes:"TextSummary;Html"` with the same literal substitution.
- **M:** `(cd tests/nextorm.core.tests && dotnet stryker --config-file ../../artifacts/pdca/D221/rv1/stryker.json)`; persist resolved config targeting both changed production types and core/SQLite tests, JSON/HTML reporters, with output under `artifacts/pdca/D221/rv1/mutation/`.
- **F:** `dotnet docfx docs/docfx.json`; **A:** `git status --porcelain=v1 --untracked-files=all` and `git diff --check -- src/nextorm.core/DI tests/nextorm.core.tests tests/nextorm.sqlite.tests tests/nextorm.integration.tests docs/infrastructure docs/ru/infrastructure docs/getting-started docs/ru/getting-started docs/specs`.
- **K:** CHECK-role invocation: `T221 r=1 n=1 rv=1 status=docs/specs/status/D221-interceptor-di-registration-1.md manifest=artifacts/pdca/D221/rv1/manifest.json required_rows=E221-01..E221-09`; verify scenario coverage, docs/candidates, baseline-relative scope/CRLF and construction-only footprint.

| Row → requirement / scenario | Planned evidence/source; invocation → required outcome | Artifact slot; owner; applicability; rv |
|---|---|---|
| E221-01 → R221-01,06 / compilation | Build log from B → exit 0, no warnings; test builds also warning-free | `artifacts/pdca/D221/rv1/build.log`; coder; production/API changed; 1 |
| E221-02 → R221-01–04 / V01–V13 | Test logs from U+UN → exit 0, nonzero selection, all scenarios accounted for | `artifacts/pdca/D221/rv1/core-*.log`; coder; registration/DI changed; 1 |
| E221-03 → R221-02,04,05 / V01,V07–09,V12,V14 | Q+QN logs → exit 0, actual ordered callbacks and JSON scenario pass | `artifacts/pdca/D221/rv1/sqlite-*.log`; coder; attachment behavior exercised; 1 |
| E221-04 → R221-03,05 / V11,V15 | I log/provider results → exit 0; all five named providers execute, none skipped | `artifacts/pdca/D221/rv1/integration.log`; coder; DI/context construction changed; 1 |
| E221-05 → R221-06 / baseline | C(baseline)+R(baseline) → exit 0, parseable line/branch reports | `TestResults/D221/rv1/coverage-baseline*`; coder; executable changes planned; 1 |
| E221-06 → R221-06 / branch delta | C(after)+R(after) → exit 0; changed branches closed, comparable coverage nonregressing; thresholds correctly labelled | `TestResults/D221/rv1/coverage-after*`, `artifacts/pdca/D221/rv1/coverage-delta.md`; coder; executable changes present; 1 |
| E221-07 → R221-06 / fault sensitivity | M → exit 0 and parseable mutant report; relevant survivors killed or individually proved equivalent | `artifacts/pdca/D221/rv1/mutation/**`, `mutation-disposition.md`; coder; changed types present; 1 |
| E221-08 → R221-06 / docs/API | F+K → DocFX exit 0, no new attributable warnings/broken links; EN/RU and held freeze records agree | `artifacts/pdca/D221/rv1/docfx.log`, `docs-api-review.md`; coder/check; public API added; 1 |
| E221-09 → R221-01–06 / completeness/scope | A+K → no new attributable whitespace/scope defects; original dirty content preserved; every required slot/scenario covered | `artifacts/pdca/D221/rv1/scope-baseline/**`, `scope-review.md`, `check-verdict.md`; coder/check; always; 1 |

**`required_rows`:** E221-01 through E221-09, without omission. All applicability predicates are currently true; no missing report can make a row N/A. Manifest records planned sources, resolved commands/configs, artifacts and later evidence references.

**CHECK re-gather budget:** owner `check`; at most **two targeted requests total per CHECK**, each confined to one missing/ambiguous slot. Persistent absence yields `incomplete`, not invented evidence or automatic DO. Any justified contract revision explicitly supersedes rv=1, preserves IDs/obligations and adds IDs for new variants.

## Status, risks and handoff

**Coder persists** the complete goal/scope, A1–A3, criteria, matrices, decisions, command dictionary/contract, manifest link, D221-0…4 states, dependencies and iteration ledger (`N=1`, `r=1`, `rv=1`, `n=1/3`, `plan_state=ready`, implementation not done). Update todos at each unit transition; write before DO and respect the parent's actual authorization mode.

**Exact three-line boundary note:**

DoD: Implementation, tests, bilingual docs and every required rv=1 evidence row are accepted by CHECK.
Not DoD: Plan approval, a DO completion report, missing artifacts or skipped provider tests do not complete T221.
Next step: Persist this r=1 plan and frozen manifest; execute D221-0, then D221-1 through D221-4 under the parent cycle's authorization.

**Risks/prerequisites:** A1/A2 can change repeated-instance/dual-interface behavior and therefore require prominent documentation/tests; provider disposal and repeated creation need explicit evidence. Before D221-1, predecessor results must be integrated/stable and siblings must not concurrently edit `DI/**`, `DataContextBuilder.cs` or `Interceptors/**`; conflict becomes an additive prerequisite, never implicit supersession. Preserve dirty/untracked docs; T53 is downstream/HELD, not a prerequisite to activate. All deferred tracking remains in milestone 1.0.9.

**Return classification:** no current DO→PLAN blocker. New tool/consumer uncertainty → targeted scout first; genuine external blocker or persistent low confidence → recommend escalation, including trigger 5 where applicable.

**Confidence: medium.**

## Progress log

- PLAN (r=1) issued by `planner`, persisted by `coder`. Phase stopped at PLAN→DO boundary; handoff to
  collection parent (no DO started, no branches/worktrees, no code).
- DO started (r=1, rv=1, n=1/3, N=1), current tree, collection lane G1 (single group, no worktree/branch/merge).
- D221-0 done: dirty-worktree/owned-file snapshots, coverage baseline, Roslyn creation-site impact, Podman
  socket and tool compatibility. Mandatory scope validated (`validate_inner_loop.py brief` exit 0).
- D221-1 done: `DataContextBuilder` unified ordered entry list + `AddInterceptor(Type)`/`AddInterceptor<T>()`
  + internal provider ctor (public parameterless ctor preserved); both DI builder factories capture `sp`.
  Build 0 warnings / 0 errors.
- D221-2 done: expanded `InterceptorBuilderTests`; new core/sqlite `InterceptorDiRegistrationTests` and
  `CommonTestSuite.InterceptorDiTests` (+ ClickHouse class). U 31/31, UN 37/37, Q 10/10, QN 8/8, I 21/21
  (SQLite/PostgreSQL/SQLServer/MySQL/ClickHouse, 0 skipped), all exit 0.
- D221-3 done: EN/RU interceptor and DI pages; `API-NAMING-REVIEW`, `code-smells-review`, T53 candidate
  records (freeze HELD), collection candidate list. Issue #221 verified via `gh`.
- D221-4 done (stopped at DO→CHECK): build exit 0; DocFX exit 0 (2 pre-existing sourcegenerator warnings);
  coverage baseline 9.4%/6.5% → after 11.3%/7.8% (`DataContextBuilder` 63.5%→79.3%,
  `ServiceCollectionExtensions` 83.3%→100%); Stryker final 68 killed / 8 survived (89.47%), 0 NoCoverage;
  manifest `validate_inner_loop.py manifest` exit 0; evidence `validate_inner_loop.py report` exit 0.
- Durable state: N=1, r=1, rv=1, n=1/3. DO does not declare CHECK; candidate implementation complete is
  provisional.
- Defect ledger: `D221-MUT-COVERAGE` — Stryker's `test-projects` (core + sqlite) exclude
  `nextorm.integration.tests`, so the `ServiceCollectionExtensions` argument guards had no killer under
  mutation (run-1 survivors 18523/18527/18531/18535/18539/18542; 18524/18528/18536 were killed by the same
  additions); fixed once by adding core-project null-guard assertions (runs 2–3). No product defect was
  found in DO.
- Notices: (1) local tool shims for `dotnet-coverage`/`reportgenerator` are not on PATH, so the frozen
  commands ran as `dotnet dotnet-coverage` / `dotnet reportgenerator` (equivalent). (2) The evidence report
  records the combined-filter runs as split `-class`/single-filter invocations because
  `validate_inner_loop.py` rejects a `|` inside a command token (hygiene rule); the same tests ran.
  (3) Stryker run-3 mislabels mutant 18498 `Survived` although the temporary-mutation check shows
  `AddInterceptor_WithOpenGenericType_Throws` fails under it (empirically killed; see
  `mutation-disposition.md`).

## Done / Verified / Incomplete

- Done: own PLAN r1 persisted; D221-0…D221-4 executed (implementation, tests, bilingual docs, evidence,
  manifest, mutation disposition).
- Verified: build 0/0; U 31/31, UN 37/37, Q 10/10, QN 8/8, I 21/21 (five providers, 0 skipped); DocFX exit 0;
  coverage non-regressing; Stryker 68 killed / 8 survived, 0 NoCoverage; manifest + report validators exit 0.
- Incomplete: CHECK not run — the verdict is the `check` role's; `D:`-units remain pending/blocked until
  CHECK accepts (DoD not met by a DO report).
