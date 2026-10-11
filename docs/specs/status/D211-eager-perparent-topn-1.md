# D211 — eager-load per-parent Take/Skip (#211)

## Identity and state

- task_id: T211; collection_id: `1.0.9-todo`
- issue: #211; absorbs closed #210
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=1
- frozen evidence-contract revision: rv=1
- plan_state: **ready**
- base: branch `1.0.9` @ `58025147`
- mode: autonomous; collection own-PLAN only
- status_file: `docs/specs/status/D211-eager-perparent-topn-1.md`
- permitted auto-commit: task/collection status files only; push never
- phase: P; DO has not started and is not authorised by this handoff

The earlier planner-resource failure is resolved. This is the first issued plan, not a replan: no supersession, attempt reset or product blocker is inferred from that failure.

## Goal

Apply a child query's existing Take/Skip **independently to each parent**, through the existing eager-load pipeline. Child filtering and ordering precede paging. Parent selection, root paging and resource ownership remain unchanged.

No new public fluent API, provider package or production abstraction is required.

## Acceptance criteria

Every criterion below is an rv1 obligation.

| ID | Observable acceptance | Negative case |
|---|---|---|
| REQ-01 | For each parent, the loaded collection equals the independently computed `filter → order → Skip → Take` reference result. Cover Take, Skip, combined paging and empty results. | A globally limited child query must not starve later parents; adversarial data makes this error observable. |
| REQ-02 | Both existing eager execution modes preserve the same parent set, empty collections, root paging and supported nesting. Each paged navigation is evaluated independently. | No parent disappears because it has zero qualifying children; child ranking must not alter root Take/Skip. |
| REQ-03 | Existing child predicates, projections, supported key shapes and ordering survive the rewrite. Paging normalization follows `Paging`, not a second implementation. | Invalid or unsupported shapes are rejected before SQL execution, not silently converted into global paging or client-side relational paging. |
| REQ-04 | Existing `EntityBuilder<TEntity>` and `QueryCommand<TResult>` execution surfaces retain sync/async behavior, existing argument/params parity and async cancellation/resource ownership. No public rename/remove occurs. | Cancellation and translation failure do not leak readers/connections, dispose caller-owned resources or leave partial reusable state. |
| REQ-05 | Plan caching separates paged/unpaged shapes and rebinds values correctly. Repeated preparation does not mutate shared commands or disable caching. | Alternating paging values and interleaving Any/Count on the same context must not retain old bounds or sticky `_dontCache` state. |
| REQ-06 | SQLite, PostgreSQL, SQL Server, MySQL, MariaDB and ClickHouse generate valid SQL and return the reference result on their supported configurations. | Existing version/capability gates reject unsupported window/derived-source configurations; no optimistic SQL emission. |
| REQ-07 | Unpaged eager loading and previously rejected unrelated query shapes retain their behavior. No public API expansion is hidden in this change. | Removing the old blanket paging rejection must not remove guards for unrelated unsupported forms. |
| REQ-08 | EN/RU documentation explains semantics, ordering ties, supported modes and fail-closed restrictions. | No stale global-paging example, broken bilingual link or public link into `docs/specs/**`. |
| REQ-09 | Builds are warning-free; coverage, changed-branch evidence and targeted mutation evidence meet the rules below. | A warning, empty filtered run, surviving non-equivalent P1 mutant or uncovered feasible changed P1 branch cannot be presented as passing. |
| REQ-10 | Query-path acceptance benchmarks complete exactly seven cases without failures within four minutes. Record wall time, Mean, Allocated and cached/prepared ratios; investigate comparable growth above 20%. | Missing cases, failure, timeout or missing baseline/comparison is unresolved evidence, not a green benchmark run. |
| REQ-11 | Evidence is reproducible, row-complete and bound to the frozen content tree and rv1. | A skipped required provider, absent report or unresolved manifest reference leaves its row open. |

## Minimal solution

**What changes:** carry the child query's existing ordering/paging semantics through its eager-load node; prepare a ranked child source and attach the bounded results through the existing pipeline.

**What cannot change:** root cardinality/paging, supported child semantics, parameter/cache isolation, public API, ownership, cancellation, fail-closed behavior and bilingual documentation.

**Optimum inside those constraints:** use the existing window/derived-source SQL construction rather than introduce a second loader, a new dialect framework or per-parent database round trips.

| Approach | Advantages | Disadvantages / price / risk |
|---|---|---|
| **Partitioned ROW_NUMBER derived child source — selected** | One strategy for both eager modes; batches remain batches; reuses existing window and derived-source machinery. | Correct aliasing, projection, partition keys and capability guards require focused tests. |
| Provider-specific lateral/APPLY with local LIMIT/OFFSET | Natural per-parent formulation on some providers. | Additional provider branches and concepts; broader capability/version surface. Not selected. |
| Fetch all children and page on the client | Small relational translation change. | Unbounded transfer/materialization and changed query-path behavior. Not acceptable as a relational fallback. |

### Translation and semantic rules

1. Capture ordering and normalized paging without mutating the supplied child builder/query command.
2. Apply child predicates before ranking.
3. Partition by the complete existing parent-link key, including composite components. Use existing link semantics for nullable keys.
4. Order the window by the child query's effective ordering. Preserve direction and existing provider null-ordering behavior.
5. If no order was supplied and ordinary paging accepts that form, use a provider-valid constant-order window; selection is intentionally unspecified, as with unordered paging. Do not invent an ordering guarantee or append an implicit key.
6. Ordering ties remain ties: exact tied-row membership is not promised. Tests assert exact results only for total orderings; tie tests assert valid membership, count and parent isolation.
7. Select ranks after Skip and, when present, no later than Skip + Take. Use overflow-safe bound handling consistent with existing paging normalization.
8. Apply rank filtering inside the child source before attachment. Do not put child rank conditions in a final WHERE that removes empty parents.
9. Keep internal rank/link columns out of user materialization. Preserve existing projection requirements.
10. Split loading ranks within the batch of requested parent keys; it must not issue one query per parent. Single-query loading uses the same child-source semantics.
11. Take(0), Skip beyond the collection and parents without matches produce empty child collections without changing parents.
12. Reuse existing provider capability/version decisions. Unsupported ranking, derived-source, ordering or child shape fails before execution. No relational client fallback.
13. Remove only the rejection that this implementation makes obsolete. Retain all unrelated single-query restrictions.
14. If the existing in-memory eager route supports the same navigation, execute the same per-parent reference semantics there without SQL ranking. If that navigation is already unsupported in-memory, retain and test its existing rejection; this plan does not introduce a new in-memory loader.

## What the request did not specify

| Question | Resolution and evidence basis |
|---|---|
| Public entry points | Use existing entry points; no new overloads. `EntityBuilder.cs:563-569` identifies the child-builder LoadWith factory; JoinInto overload anchors are `:662/683/741/764/809`. |
| Which modes | Both existing eager modes. Existing single-query rejection is identified at `EntityBuilder.cs:1119` and `EntityBuilderEagerLoading.cs:176-179`; only the paging-related prohibition is replaced. |
| Paging validation, negative/default inputs | Existing `Paging` semantics are the oracle (`Builders/Paging.cs:17/26/44/49`). Tests compare normalization/rejection with that existing path rather than inventing numeric rules. |
| Unordered/tied selection | No stronger deterministic guarantee than ordinary paging. Unordered provider SQL is proved by execution; unsupported forms fail closed. |
| Provider versions | Retain existing negotiated minimum-version/capability decisions from the preserved pack. No new numeric minimum is guessed. Simulated unsupported configurations test the guard; live supported configurations test execution. |
| In-memory applicability | Conditional on observable baseline support of the same eager navigation. Either positive equivalence or unchanged rejection is required evidence. |
| MariaDB execution evidence | MySQL SQL generation alone is insufficient. Add a dedicated MariaDB integration fixture using the existing MySQL-compatible provider path and a declared `mariadb:11.4` container image; record its resolved image identity. |
| Decisive oracle | Planned deterministic parent/child fixture plus an independent collection-level reference calculation. Existing SQL-generation and provider integration harnesses make this declarable and reproducible; no unavailable external data is required. |
| Runtime availability | A DO precondition, not a current gap. Follow the integration skill's machine-start/retry procedure. Missing runtime evidence cannot waive a provider row. |

**Obtainability:** the preserved pack establishes paging, SQL construction and provider/test harness sources. The decisive fixture, reference calculator, trait selectors and evidence driver below are explicitly planned sources, not existing test symbols or claimed results. No acceptance criterion depends on an inaccessible service or unspecified oracle.

## Prerequisites and sibling requirements

- Existing eager pipeline and entry points: `docs/specs/roadmap/todo_eager_loading_pipeline.md:72-95/112-121/141-148`, `EntityBuilder.cs:563-569`, `Builders/EntityBuilderEagerLoading.cs:12/60`.
- Existing window emission: `DataContext/SqlBuilder.cs:1113-1156`.
- Existing derived query source: `Expressions/FromExpression.cs:107`; construction precedent: `Builders/Joins/JunctionLinkSourceFactory.cs:87-120`.
- Existing paging normalization: `Builders/Paging.cs:17/26/44/49`.
- Existing provider gates, SQL-generation harnesses and integration families: preserved gather finding. Their exact executed configurations and symbols must be entered in the DO ledger.
- No implementation prerequisite on #209/#212/#213/#201/#53.
- **Serialise against #209/#212/#213** wherever their changes touch eager-node/builder/dialect contracts. T211 must consume the resulting current contract, not overwrite sibling changes.
- Before implementation, record the actual integrated input tree. If a sibling changes semantics assumed here, route the concrete conflict to PLAN; do not silently weaken criteria or revise rv.
- Collection ALL-barrier and the task's DO authorisation remain external prerequisites. This task-ready plan does not itself satisfy the collection-wide barrier.

## Tasks and execution mode

**Mode: sequential units in one tree.** Core metadata, SQL generation, cache behavior and tests share one contract. Parallel implementation would create conflicting ownership. No new branch/worktree, commit or merge is requested. Read-only result inspection may be parallel; implementation and evidence-tree freezing are serial.

All items below are **fix now**. No part of the requested capability is deferred to another milestone.

| Unit | Concrete work and anchors | Completion evidence |
|---|---|---|
| D:01 — establish reproducible probes and baseline | Read the integration skill; establish the container runtime; write the planned evidence driver and fixture/reference sources. Capture input-tree identity, baseline public surface, unpaged behavior, coverage and acceptance performance before production edits. | Nonempty harness selection; baseline logs; independent oracle proven on an unpaged fixture. |
| D:02 — bounded translation experiment | Using `SqlBuilder.cs:1113-1156`, `FromExpression.cs:107` and `JunctionLinkSourceFactory.cs:87-120`, prove derived ranking, partition keys, alias visibility, constant ordering and bound parameterization in the existing harness. | Generated SQL and live reference equality across the provider matrix. Retain probe sources/results. |
| D:03 — retain child ordering/paging metadata | Update the existing load node and builder capture at `EntityBuilderEagerLoading.cs:12/60` and `EntityBuilder.cs:563-569`; preserve immutability, projection/link requirements and cache-key inputs. | Metadata/copy/cache tests, including different paging values and unpaged reuse. |
| D:04 — integrate per-parent execution | Build/filter ranked child sources in existing SQL construction; integrate split and single-query modes; remove only obsolete paging rejection at `EntityBuilder.cs:1119` / `EntityBuilderEagerLoading.cs:176-179`. Preserve applicable in-memory behavior. | REQ-01–07 scenario tests and fail-closed tests. |
| D:05 — execution and provider regressions | Exercise both public execution surfaces, sync/async, cancellation, ownership, nesting, root paging and query-count bounds. Add MariaDB-specific execution fixture; cover provider capability rejection. | Core, SQL-generation and six-provider integration reports; no required provider skipped. |
| D:06 — coverage and mutations | Collect configured-project coverage; map changed lines/branches to the base; run targeted Stryker.NET mutations and resolve uncovered branches/survivors. | Coverage, branch-delta and mutation reports satisfying the rules below. |
| D:07 — docs and performance | Update the exact EN/RU pages below; add the dedicated eager benchmark without adding an eighth acceptance case. Run final acceptance and dedicated measurements. | DocFX/link evidence, seven-case report, comparable baseline analysis and eager measurements. |
| D:08 — freeze handoff evidence | Record actual sources, symbols, invocations, exits and artifacts by row. Freeze content-tree identity and rv1 manifest; validate it. | All required rows resolved; manifest validation exit 0. D remains active until CHECK accepts it. |

The experiment has a maximum of two candidate translations within the chosen row-number strategy: ordinary ordered ranking and the provider-valid unordered convention. A failure that requires a different architecture returns to PLAN with the concrete failing probe; it does not authorise silent lateral/client fallback.

## Planned footprint

Paths below are exact intended locations, **not claims that every planned source already exists**. New sources are explicitly marked.

### Production writes

- `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs` — **SERIALISED with #209/#212/#213**
- `src/nextorm.core/Builders/EntityBuilder.cs` — **SERIALISED with #209/#212/#213**
- `src/nextorm.core/DataContext/SqlBuilder.cs`

### Shared contract dependency — no write in r1

- `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` — **SERIALISED contract-read with #209/#212/#213**.
- Reuse existing capability/version gates; do not add a new public dialect capability in this plan.
- Provider implementation changes are not silently authorised. If an existing capability contract cannot express the necessary guard, return the concrete prerequisite to PLAN.

### Planned test sources

- `tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs` — new
- `tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs` — new
- `tests/nextorm.postgres.tests/EagerLoadPerParentPagingTests.cs` — new
- `tests/nextorm.sqlserver.tests/EagerLoadPerParentPagingTests.cs` — new
- `tests/nextorm.mysql.tests/EagerLoadPerParentPagingTests.cs` — new; includes MariaDB SQL-generation variants
- `tests/nextorm.mysql.tests/nextorm.mysql.tests.csproj` — add a MariaDB project reference if absent; no package version
- `tests/nextorm.clickhouse.tests/EagerLoadPerParentPagingTests.cs` — new
- `tests/nextorm.integration.tests/CommonTestSuite.EagerLoadPerParentPaging.cs` — new, in the existing shared suite
- `tests/nextorm.integration.tests/Providers/MariaDbEagerPagingFixture.cs` — new
- `tests/nextorm.integration.tests/MariaDbEagerPagingTests.cs` — new

### Benchmark and documentation writes

- `benchmarks/nextorm.benchmark/EagerPerParentPagingBenchmark.cs` — new
- `docs/advanced/eager-loading-per-parent-paging.md` — new
- `docs/ru/advanced/eager-loading-per-parent-paging.md` — new
- `docs/guide/02-joins.md`
- `docs/ru/guide/02-joins.md`
- `docs/guide/29-implicit-navigation.md`
- `docs/ru/guide/29-implicit-navigation.md`
- `docs/specs/status/D211-eager-perparent-topn-1.md`

### Declared evidence sources/artifacts

Under `artifacts/pdca/D211-eager-perparent-topn-1/rv1/`:

- `probe.py` — new, retained evidence driver with source hash
- `coverage-run.sh` — new, retained coverage command script
- `stryker-config.json` — new, retained configuration
- `tools/` — isolated measurement tools; record installed versions
- row logs/reports and the manifest specified below

No writes to provider production projects, `Paging.cs`, `FromExpression.cs`, `JunctionLinkSourceFactory.cs`, public API naming review, package-version files or guide numbering are planned. A newly discovered required production file is a concrete scope adjustment for PLAN, not an undisclosed edit.

## Test strategy

### Harness and selectors

New feature tests carry the **planned** xUnit trait `Feature=D211`. This is a declared selector, not an invented existing test symbol.

Filtered invocation for each listed project:

`dotnet run --project <listed-project> -c Debug -- -noColor -trait "Feature=D211"`

Exact projects:

1. `tests/nextorm.core.tests`
2. `tests/nextorm.sqlite.tests`
3. `tests/nextorm.postgres.tests`
4. `tests/nextorm.sqlserver.tests`
5. `tests/nextorm.mysql.tests`
6. `tests/nextorm.clickhouse.tests`
7. `tests/nextorm.integration.tests`

The evidence driver runs these seven explicit invocations. Each must exit 0 and report a nonzero selected/executed count. MariaDB execution is in project 7; MariaDB SQL generation is in project 5.

Broad regression invocation for each of those same seven projects:

`dotnet test <listed-project> -c Debug`

For project 7, both filtered and broad commands receive:

`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`

Before them, load `.opencode/skills/running-integration-tests/SKILL.md`. If the socket is absent, follow its recovery procedure, including:

`"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start`

Then wait, verify the socket and rerun. A zero exit with skipped required providers is insufficient.

### Shared test contract

**EagerPerParentPagingReference-v1**:

- Seed at least three parents with uneven child counts, overlapping sort values and distinct filtering outcomes; include a parent with no matching children.
- Independently group fixture children by the complete link key, filter, order, normalize paging according to the existing Paging contract, then page each group.
- Compare parent identities and each parent's collection with the eager result.
- Use unique ordering for exact sequence assertions.
- For unordered/tied cases, assert counts, membership, bounds and parent isolation, not an invented deterministic order.
- Record query counts: split remains batched by existing pipeline behavior; no per-parent N+1.
- SQL-generation tests also assert that root bounds and child rank bounds are distinct, bound values are parameters where the existing builder parameterizes them, and no rank column escapes materialization.

Unit tests establish normalization, metadata, cache and guard behavior. SQL-generation tests establish provider translation. Container-backed tests establish actual SQL semantics, sync/async behavior and ownership.

### Variant matrix

Every row is closed by **test** or **guard + test**; none is deferred.

| Path variant | Closure |
|---|---|
| No child paging | Test unchanged eager behavior and cache reuse. |
| Take only / Skip only / Skip+Take | Test exact per-parent results in both modes. |
| Zero/default bounds / negatives / extreme bounds | Test differential Paging normalization/rejection; guard arithmetic overflow and invalid shapes. |
| Empty source / parent without matches / Skip past end | Test empty collections and unchanged parents. |
| Ordered ascending/descending / multiple keys | Test exact sequence, filter-before-rank and provider SQL. |
| No order / tied order | Test valid nondeterministic membership/count; guard unsupported provider form. |
| Scalar/composite parent-link keys | Test partition isolation, including adversarial overlapping key components. |
| Nullable/reference key components / default value keys | Test existing link/null semantics; no cross-parent leakage. |
| Reference entities / supported value projections | Test supported shapes; guard missing link/order materialization dependencies. |
| Child predicate / supported projection | Test predicate-before-rank and projection preservation. |
| Aggregate/grouped/distinct/set-operation child forms not supported by the existing eager contract | Guard + negative test; retain existing restrictions. |
| Split / single-query | Test equivalence, empty-parent preservation and query counts. |
| Nested navigation / two independently paged collections | Test independent ranking and existing duplicate/materialization semantics. |
| Root Take/Skip plus child paging | Test independent root and child bounds. |
| LoadWith child factory / applicable JoinInto overloads | Test capture through the existing entry-point family; overloads without a child-query input receive an unchanged-behavior test, not an invented paging API. |
| Implicit navigation without paging metadata | Test no behavioral change at `NavigationExpansion.cs:43` path. |
| Builder / prepared QueryCommand execution | Test both existing surfaces and their argument parity. |
| Sync / async / token already cancelled / cancellation during reading | Test results or expected cancellation and resource ownership. |
| Cached / noncached local preparation / rebind / same-context interleaving | Test shape separation, value rebinding and no sticky cache mutation. |
| SQLite / PostgreSQL / SQL Server / MySQL / MariaDB / ClickHouse | SQL-generation and real-provider tests. |
| Unsupported window/version flag / unsupported derived-source form | Guard + pre-execution rejection test. |
| In-memory eager-supported navigation | Test reference equivalence when the baseline path supports it. |
| In-memory eager-unsupported navigation | Guard + unchanged rejection test when baseline support is absent. |

### Coverage and mutation

- Coverage scope: existing `coverage.settings.xml`, covering core/SQLite/PostgreSQL/SQL Server.
- Thresholds: `MIN_LINE_COVERAGE=85`, `MIN_BRANCH_COVERAGE=75`. On branch `1.0.9`, report the existing CI warning policy accurately; do not claim main-gate success below either threshold.
- Require no comparable aggregate coverage regression against the captured baseline.
- Changed-path acceptance: every new/changed feasible P1 line and branch is executed. Any claimed infeasibility needs a retained explanation and guard/reference evidence; it is not silently excluded.
- Produce a branch-delta report against `58025147`, identifying changed conditions and their executing scenarios. Coverage percentages alone do not establish that paging/cache guards were exercised.
- Use Stryker.NET on changed production lines in the three production files. Core and SQL-generation tests form the mutation test set; integration remains separate decisive SQL evidence.
- Mutation targets include rank bounds, partition components, predicate/rank ordering, mode guards, cache-key inputs and command-copy behavior.
- Acceptance: no surviving or uncovered non-equivalent P1 mutant. Every excluded equivalent mutant has a retained equivalence justification. Tool failure/timeout is unresolved evidence, not a passing score.
- Install measurement tools into the evidence directory, record exact resolved versions, and retain the config/tool receipts. These are not production package dependencies.

## Priority matrix

CHECK applies these priorities without downgrading them.

| P1 by construction | Reason |
|---|---|
| Capture/copy of child ordering/paging and effective cache-key inputs | Direct execution-path state and cache invariants. |
| Partition, rank bounds, filter placement, projection/link columns and empty-parent attachment | Core issue #211 invariants. |
| Capability/version and unsupported-shape guards | Fail-closed provider contract. |
| Both public execution surfaces; sync/async, cancellation, argument parity and ownership | Project query-path class-priority table. |
| Local cache preparation and shared Any/Count command interaction | Project sticky-command/cache invariant. |
| Provider-specific emitted SQL exercised by live integration | A parser-only or SQL-text-only pass is not semantic proof. |

Test helper formatting, prose and report presentation are P2, but their required evidence rows remain mandatory.

## Documentation plan

- Add matching EN/RU advanced pages at the exact planned paths.
- Explain per-parent versus root paging, filter/order/rank order, both eager modes, ordering ties, empty collections, batching and unsupported shapes.
- Update examples/links in `02-joins.md` and `29-implicit-navigation.md`, in both languages.
- Link the advanced article from these existing guide pages. Do not create/move/delete numbered guides or add a separate section; **no renumbering** is required.
- No public rename/remove; curated API naming review and generated API output are not edited.
- No public links to `docs/specs/**`.
- Build with `dotnet docfx docs/docfx.json`; verify the four guide links and EN/RU article parity.

## Performance decision

**Required.** This is a query-generation, eager-execution and plan-cache change, not a one-time-only operation.

### Mandatory acceptance

Exact command:

`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`

Run before production edits and after completion on the same host/runtime/configuration. Retain the baseline comparison to `docs/specs/performance/acceptance-benchmarks.md`.

Require exactly seven completed cases, no failures and wall time ≤240 seconds. Record per-case Mean and Allocated, wall time, and cached/prepared ratio. Comparable growth >20% requires investigation and an explicit disposition; do not treat sub-20% movement as a demonstrated gain/regression.

### Dedicated eager measurement

Planned benchmark command:

`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*EagerPerParentPagingBenchmark*" --job short`

Do not assign the new benchmark to the `acceptance` category.

Use deterministic SQLite data, both eager modes, repeated prepared/cached execution, and at least `1×8`, `100×20`, `1000×20` parent/child sizes. Compare server-paged results with the existing unpaged-load-plus-reference-page calculation. Record Mean, Allocated, query count and child-row count; distinguish unlike-workload comparisons from comparable baseline regressions.

Parameter binding is covered by rebinding/cache tests and these measurements. No TVP implementation changes are planned; existing split-loader transport is preserved.

## Reconnaissance decision

**Required bounded implementation experiment, D:02; no new PLAN gather.**

It proves that existing window/derived-source construction can retain partition keys, child order and predicates while exposing a filtered source usable by both eager modes.

Observable pass:
- SQL-generation tests retain root/child bound separation;
- supported live providers match the independent per-parent oracle;
- unsupported capabilities reject before execution;
- repeated values rebind without command mutation.

Maximum two translation candidates within the selected strategy. A failed architectural premise returns to PLAN with SQL, provider error and source anchors. The preserved pack supplies enough facts to plan this experiment; its future outcome is not asserted.

## Design-checklist result

- **KISS:** extend the existing node/source path; one paging strategy, no second loader.
- **Single-consumer abstractions:** no new production type/interface. Planned fixture/benchmark/driver sources have explicit test, measurement or evidence consumers.
- **Public API end-to-end:** no rename/remove; both public execution surfaces and bilingual usage examples covered.
- **Provider fail-closed:** existing gates reused; supported SQL proved live; required providers cannot be skipped.
- **Cache safety:** no `queryCommand.Cache=false`; preparation-local decisions only. Shared-command invariants tested.
- **Build:** Debug and Release, warnings treated as errors, nullable preserved.
- **CPM:** no package versions added to project files; no production package changes.
- **Line endings:** preserve CRLF for repository writes; verify no mixed/LF-only edited source.
- **Measure, never assert:** retained acceptance/dedicated measurements and >20% investigation rule.
- **Milestone:** all work remains in 1.0.9; no feature slice deferred to 1.1.
- **Collection isolation:** shared files/contracts serialised; no sibling implementation dependency invented.
- **Git:** status-only auto-commit allowance does not authorise implementation commits, push, merge or worktrees.

## Evidence machinery and exact invocations

Evidence root:

`artifacts/pdca/D211-eager-perparent-topn-1/rv1/`

Frozen manifest:

`artifacts/pdca/D211-eager-perparent-topn-1/rv1/manifest.json`

The **planned** retained `probe.py` is an evidence driver, not a substitute oracle. It:
- records every expanded command, environment relevant to results, start/end time and process exit;
- stores raw stdout/stderr, native test reports and machine-readable result summaries;
- rejects empty test selections and missing required provider executions;
- runs the fixed commands specified here;
- maps actual test symbols/source references to scenario IDs;
- computes content-tree identity and artifact hashes;
- returns nonzero for failed checks or unresolved required outputs.

Each subcommand writes `<operation>.log` and `<operation>.json` under the evidence root; native reports go in `<operation>/`. Sources/configs are retained with hashes. Planned locations are expectations, not existing evidence.

Fixed subcommand behavior:

- `baseline`: record `git rev-parse HEAD`, `git status --porcelain=v1`, baseline API/fixture observations; run broad regression, coverage and the exact acceptance command before production edits. Store results under `baseline/`.
- `oracle`: run the declared core fixture/reference tests; prove the reference on the existing unpaged path and record fixture data.
- `core`: run the exact filtered core-project invocation.
- `sql`: run the exact filtered invocations for the five listed SQL-generation projects, including MariaDB variants within MySQL.
- `integration`: run the exact filtered integration invocation with the stated DOCKER_HOST; require six provider families, both eager modes and sync/async evidence.
- `regression`: run the seven exact broad regression invocations with integration runtime configured.
- `api-cache`: run the filtered core and integration commands and extract the actual API/cache/cancellation/ownership scenarios; compare the baseline public surface.
- `guards`: run the filtered core and SQL-generation commands; extract actual unsupported-form/capability scenarios.
- `build`: run `dotnet build -c Debug` and `dotnet build -c Release`; verify warnings and CRLF on the edited footprint.
- `coverage`: execute the coverage commands below; generate aggregate and changed-branch reports.
- `mutation`: execute the mutation commands below and classify every relevant mutant.
- `docs`: run `dotnet docfx docs/docfx.json`; check declared links, bilingual semantic coverage and absence of public specs links.
- `perf`: run the exact acceptance and dedicated eager benchmark commands; enforce seven cases/240 seconds and produce the baseline comparison.
- `freeze`: record content-tree identity, commands/results/source hashes and populate all required manifest rows.

Coverage tool setup and execution, captured by `coverage`:

- `dotnet tool install dotnet-coverage --tool-path artifacts/pdca/D211-eager-perparent-topn-1/rv1/tools`
- `dotnet tool install dotnet-reportgenerator-globaltool --tool-path artifacts/pdca/D211-eager-perparent-topn-1/rv1/tools`
- `artifacts/pdca/D211-eager-perparent-topn-1/rv1/tools/dotnet-coverage collect "bash artifacts/pdca/D211-eager-perparent-topn-1/rv1/coverage-run.sh" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D211-eager-perparent-topn-1/rv1/coverage/coverage.xml`
- `artifacts/pdca/D211-eager-perparent-topn-1/rv1/tools/reportgenerator "-reports:artifacts/pdca/D211-eager-perparent-topn-1/rv1/coverage/coverage.xml" "-targetdir:artifacts/pdca/D211-eager-perparent-topn-1/rv1/coverage/report" "-reporttypes:JsonSummary;Html"`

`coverage-run.sh` contains the seven exact broad project commands listed above, propagates any nonzero exit, and supplies DOCKER_HOST for integration.

Mutation setup and execution, captured by `mutation`:

- `dotnet tool install dotnet-stryker --tool-path artifacts/pdca/D211-eager-perparent-topn-1/rv1/tools`
- `artifacts/pdca/D211-eager-perparent-topn-1/rv1/tools/dotnet-stryker --config-file artifacts/pdca/D211-eager-perparent-topn-1/rv1/stryker-config.json`

The planned retained configuration fixes the production project, three production files, changed-line mutation scope, listed core/SQL-generation test projects, reporting paths and timeout. Record resolved tool versions before measurement; subsequent reruns use those same installed versions.

## Frozen evidence contract — rv1

Every row below contains stable requirement/row IDs, scenario, expected evidence/source, exact invocation and result/log requirements, artifact locations, owner, applicability and revision.

All relative artifact locations in this table are under the evidence root defined above.

| Stable row / requirement | Required check or scenario | Expected evidence kinds and sources | Exact invocation; exit/result/log requirement | Expected artifacts | Owner stream | Observable applicability | rv |
|---|---|---|---|---|---|---|---|
| EV-01 / REQ-11 | Baseline identity and obtainable fixture/oracle | Git/content identity, baseline reports, declared fixture/reference sources; planned sources above | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py baseline`; exit 0; expanded commands/raw logs and baseline input state required | `baseline.log`, `baseline.json`, `baseline/`, retained driver/source hashes | DO-baseline | Always | 1 |
| EV-02 / REQ-01 | Independent per-parent oracle; adversarial global-limit case | Fixture data, reference calculation, nonempty core test report and actual source references | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py oracle`; exit 0; reference proof and adversarial mismatch demonstration required | `oracle.log`, `oracle.json`, `oracle/` | DO-core | Always | 1 |
| EV-03 / REQ-01, REQ-03 | Paging/order/filter/key/default/overflow matrix | Core test results, actual symbols, normalization differential and source references | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py core`; exit 0; nonzero count and matrix-to-test mapping required | `core.log`, `core.json`, `core/` | DO-core | Always | 1 |
| EV-04 / REQ-02, REQ-06 | Both-mode provider SQL, partition/bound/alias/projection correctness | Five SQL-generation project reports, six dialect-family SQL samples and capability configurations | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py sql`; exit 0; all listed projects nonempty; SQL samples and actual symbols required | `sql.log`, `sql.json`, `sql/` | DO-sql | Always | 1 |
| EV-05 / REQ-01, REQ-02, REQ-06 | Real execution across six providers, both modes, root/nested paging and batching | Integration native reports, fixture results, image/server identities and query counts | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py integration`; exit 0; six families executed, required scenarios passed, no required skips; raw logs required | `integration.log`, `integration.json`, `integration/` | DO-integration | Always | 1 |
| EV-06 / REQ-04, REQ-05, REQ-07 | Public surfaces, sync/async, cancellation, ownership, cache/rebinding and unchanged API | Core/integration results, baseline/final public-surface comparison, actual scenario references | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py api-cache`; exit 0; every stated scenario resolved; no public rename/remove or sticky command mutation | `api-cache.log`, `api-cache.json`, `api-cache/` | DO-execution | Always | 1 |
| EV-07 / REQ-03, REQ-06, REQ-07 | Unsupported capability/shape/version rejection; applicable in-memory positive or negative behavior | Guard tests, pre-execution rejection evidence, baseline in-memory applicability result | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py guards`; exit 0; rejection before database execution; in-memory branch resolved according to observed baseline | `guards.log`, `guards.json`, `guards/` | DO-guards | Always; in-memory positive/negative subscenario selected by recorded baseline support | 1 |
| EV-08 / REQ-07, REQ-09 | Broad regression across seven projects | Native test reports from the seven exact broad commands | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py regression`; exit 0; no failures or required-provider skips; all command exits logged | `regression.log`, `regression.json`, `regression/` | DO-regression | Always | 1 |
| EV-09 / REQ-09 | Debug/Release warning-free build and CRLF | Build logs, diagnostic counts, edited-file line-ending report | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py build`; exit 0 for both builds; zero warnings/errors and no mixed/LF-only edited repository source | `build.log`, `build.json`, `build/` | DO-build | Always | 1 |
| EV-10 / REQ-09 | Aggregate coverage and changed-P1 branch coverage | Cobertura, ReportGenerator summary, baseline comparison, changed-branch/scenario map | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py coverage`; tool/test exits 0; report 85/75 comparison, no comparable regression, all feasible changed P1 branches covered; logs required | `coverage.log`, `coverage.json`, `coverage/coverage.xml`, `coverage/report/`, `coverage/branch-delta.json` | DO-coverage | Always | 1 |
| EV-11 / REQ-09 | Targeted mutation evidence | Stryker config/version receipt, mutant report, survivor/equivalence dispositions | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py mutation`; exits 0; no surviving/uncovered non-equivalent P1 mutant; tool incompletion is unresolved | `mutation.log`, `mutation.json`, `mutation/`, `stryker-config.json` | DO-mutation | Always | 1 |
| EV-12 / REQ-08 | EN/RU documentation and generated-site/link validity | Declared article sources, DocFX log, bilingual/link/specs-link checks | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py docs`; DocFX/check exits 0; required links resolve and no public specs links | `docs.log`, `docs.json`, `docs/`; generated `docs/_site/` is disposable, not committed | DO-docs | Always | 1 |
| EV-13 / REQ-10 | Seven-case acceptance and dedicated eager measurements | BenchmarkDotNet reports, wall timer, baseline/spec comparison, investigation disposition | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py perf`; underlying commands exit 0; exactly seven acceptance cases, no failures, ≤240 seconds; Mean/Allocated/ratios and >20% investigation recorded | `perf.log`, `perf.json`, `perf/`, copied native benchmark reports | DO-perf | Always | 1 |
| EV-14 / REQ-11 | Content-tree binding, row completeness and manifest validity | Frozen manifest, content-tree inventory/hash, DO ledger, validator output | `python3 artifacts/pdca/D211-eager-perparent-topn-1/rv1/probe.py freeze` followed by `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D211-eager-perparent-topn-1/rv1/manifest.json`; both exit 0; no unresolved required row/reference | `freeze.log`, `freeze.json`, `tree.json`, `ledger.json`, `manifest.json`, `manifest-validation.log` | DO-evidence | Always | 1 |

### Required rows

`EV-01, EV-02, EV-03, EV-04, EV-05, EV-06, EV-07, EV-08, EV-09, EV-10, EV-11, EV-12, EV-13, EV-14`

### DO ledger and frozen manifest

- Record actual evidence against each stable row ID and rv1: verified symbols or source references, executed commands, invocation results/exits, raw logs and artifact paths.
- Minimum manifest fields: `task`, `tree`, `contract_rv`, `status_file`, `required_rows`, `rows[]`.
- `task` is `D211-eager-perparent-topn-1`; `contract_rv` is `1`.
- Each row has `id` and `status` (`met|open|na`).
- A met row has resolving `evidence[]` entries containing `{kind, path, command?, exit?, value?}`.
- An open row has `reason`; a na row has its observed `predicate`.
- All listed rows are unconditional overall obligations; conditional in-memory subscenarios do not permit EV-07 to be marked na.
- Every required row must appear. A missing report/resolving reference remains open.
- Because implementation commits are not authorised, tree binding must not be HEAD alone. Record HEAD plus a content inventory/hash of tracked changes and planned untracked source files, including the evidence driver/configs. Exclude generated reports and the manifest itself from the source-tree hash to avoid self-reference.
- Freeze at DO→CHECK. CHECK reads this manifest first. Existing evidence is not rewritten during CHECK; permitted additions are append-only and retain the same tree/rv binding.
- rv1 is the initial contract: **no supersession**. A future justified revision must preserve existing IDs/obligations, explicitly supersede rv1 and add IDs for genuinely new variants. Missing reports alone do not justify revision.

### CHECK re-gather budget

Owner: **CHECK-evidence stream**, dispatched by the orchestrator.

Budget: **two targeted re-gather dispatches total**, each limited to one existing row and one reproducible invocation, maximum 15 minutes each. No source modification.

Use only for retrieval, verification or rerunning a missing/ambiguous existing probe on the frozen tree. Append evidence to the manifest. After exhaustion, CHECK reports the unresolved row; it does not invent evidence, initiate DO or lower acceptance.

## Risks and classification

- Alias/projection leakage, nullable/composite partitions and child-filter placement: high correctness risk; P1 unit, SQL and live-provider probes.
- Single-query parent loss/duplicate materialization: P1 empty-parent/nested/multi-collection tests.
- Cache-key omission or shared-command mutation: P1 alternating-bounds and Any/Count interleaving tests.
- Provider-specific unordered windows or ClickHouse execution restrictions: live probe and pre-execution guards; architectural failure returns to PLAN.
- MariaDB fixture startup and .NET 10-compatible mutation tooling: reproducible DO prerequisites, not presumed successes.
- Existing coverage debt on branch 1.0.9: report baseline and CI policy honestly; no regression or untested changed P1 branch is acceptable.
- Host benchmark noise: compare same-host runs; investigate >20%, do not advertise smaller deltas.

**DO→PLAN classification now:** not applicable; no DO report exists. The resolved funds failure is historical and creates no active dependency. Future probe/tool failure is classified from observed evidence: first a precise prerequisite or targeted scout; a genuinely external blocker goes to escalate; persistently low confidence goes to escalate trigger 5. An absent report is not itself a product defect or a reason to supersede D.

## Confidence

High confidence in the minimal architecture and evidence obtainability from the preserved pack. Runtime SQL, cache behavior, provider availability and tool compatibility remain obligations to prove, not assumptions presented as tested facts.

No current decisive-evidence row is genuinely unobtainable; therefore `plan_state=ready`.

## Next allowed step

**Coder persists this r=1 plan verbatim into the status file and records the ready PLAN handoff for T211. Then stop.**

Do not start DO, create a branch/worktree, change implementation, run probes or treat this task-ready state as passage of the collection ALL-barrier. A later collection authorisation is required to begin D:01.

## Handoff (plan_handoff)

- task_id: T211
- plan_state: **ready**
- selected_variant: pdca-dotnet
- plan_revision: r=1
- frozen evidence-contract revision: rv=1
- PLAN commit: <filled by coder>
- ALL-barrier for T211: **NOT passed** — collection-wide barrier/DO authorisation is external.
- Next allowed step: collection authorises DO for T211 → begin D:01.

## Preserved gather evidence (pack)

The five read-only gather streams are preserved below; raw pack stayed in the collection transcript.

- pipeline spec §4/§7/§11 — `docs/specs/roadmap/todo_eager_loading_pipeline.md:72-95/112-121/141-148`
- public frontends — `EntityBuilder.cs:563-569` (sole `LoadWith` child-builder factory + `EagerLoadMode`), 5 `JoinInto` overloads `:662/683/741/764/809`, implicit-nav `Visitors/NavigationExpansion.cs:43`
- load-node model — `Builders/EntityBuilderEagerLoading.cs:12/60` (no order/predicate field today)
- single-query rejection list — `EntityBuilder.cs:1119`, throw `EntityBuilderEagerLoading.cs:176-179`
- `Paging` semantics — `Builders/Paging.cs:17/26/44/49`
- window/derived-source construction — `DataContext/SqlBuilder.cs:1113-1156`, `Expressions/FromExpression.cs:107`, precedent `Builders/Joins/JunctionLinkSourceFactory.cs:87-120`
- provider min versions + fail-closed flags; per-provider SQL-gen harnesses; test families; evidence/CI/perf/integration commands; sibling DAG footprints (#209/#212/#213/#201/#53)

### Resource-blocker history (resolved)

Two prior PLAN-decide attempts failed with upstream `Insufficient account funds`
(subagent ids `ses_ed8bea5ffffeuNStTpCrG952Zk`, `ses_ed8bd4d84ffei2tAIePgbxBQep`).
Funds restored; this first issued r=1 plan supersedes the earlier `plan_state: gap`.
No content blocker remains.

## Acknowledgement — T209 rv1 shared contract

- Contract revision: `rv=1` — `artifacts/pdca/D209/rv1/sibling-contract.md`.
- This sibling keeps its own plan revision: **r=1** (unchanged).
- Compatibility verdict: **compatible; obligation noted** — #211 consumes T209's frozen `EagerLoadMode`/`DataContextBuilder`/`EntityBuilderEagerLoading`/`ISqlDialect` surface and owns the deferred correlated/per-parent top-N **execution** that T209 rejects fail-closed (V10/V18). Obligations: revise the `Auto` rule deliberately when correlated execution lands, test provider-supported **and** provider-unsupported branches, do not rename enum members/values, and never mutate the shared `QueryCommand.Cache`.

## Progress log

2026-10-11T14:23:46Z | DO | r1 | n1/3 | DO started — plan `D211-eager-perparent-topn-1.md` (rv=1) executed sequentially D:01..D:08 in one tree; no worktree/branch; ISqlDialect read-only. | input tree base `1.0.9` @ `b527be6f37b9301ed9d2245b803a1bd3e5e43ffc` (clean worktree)
2026-10-11T14:48:11Z | DO | r1 | n1/3 | D:04a added (r1 additive prerequisite) — carry child order metadata and render effective child ordering on the outer pair command for embedded (single-query) per-parent paging; D:04 remains active, blocked on D:04a | n1/3; status file update only, plan r=1 unchanged, rv=1 unchanged
2026-10-11T15:03:06Z | DO | r1 | n1/3 | D:04 obsolete per-parent Top-N rejection tests repaired: Split/Auto/async per-parent paging now asserted as supported (partitioned row_number SQL, per-parent bounds, cache) and a fail-closed catch remains for unsupported paged child shapes (Distinct/GroupBy); EagerLoadingSqlGenerationTests 49/49 exit 0 (pre-fix red 47 total/4 failed), core Feature=D211 4/4 exit 0, sqlite Feature=D211 20/20 exit 0, `dotnet build nextorm.slnx -c Debug` 0 Warning(s)/0 Error(s) exit 0, `validate_inner_loop.py report` exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv1/{scope.json,inner-loop-evidence.json,inner-loop/*.log}
2026-10-11T15:21:19Z | DO | r1 | n1/3 | D:05 six-provider integration executed, no required provider skipped: SQLite 12/0, PostgreSQL 12/0, SQL Server 12/0, MySQL 12/0, ClickHouse 3/0, MariaDB 3/0 (all Skipped 0, containers started); provider SQL-gen PostgreSQL 7/7, SQL Server 7/7, MySQL 11/11 (7 MySQL + 4 MariaDB variants), ClickHouse 7/7; the existing MariaDbContainer fixture was reused instead of creating a separate MariaDbEagerPagingFixture.cs — justified deviation from the plan wording; MariaDB first run red on a test-fixture seed defect (settable ICollection navigation bound as a parameter; MySqlConnector rejects List<...>), fixed to a get-only initialized collection and re-run green; validate_inner_loop.py report exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv1/integration/{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}.log; artifacts/pdca/D211-eager-perparent-topn-1/rv1/integration/mariadb-fail-seed.log; artifacts/pdca/D211-eager-perparent-topn-1/rv1/inner-loop-evidence.json; artifacts/pdca/D211-eager-perparent-topn-1/rv1/integration/validate-report.out; artifacts/pdca/D211-eager-perparent-topn-1/rv1/d05/build-integration-tests.log
2026-10-11T15:30:58Z | DO | r1 | n1/3 | D:07 docs+perf complete — new EN/RU advanced page docs/advanced/eager-loading-per-parent-paging.md + docs/ru/advanced/eager-loading-per-parent-paging.md (advanced toc EN+RU; cross-links in guide 02-joins and 29-implicit-navigation EN+RU; corrected stale per-parent-top-N rejection claims in advanced/eager-loading.md EN+RU); dotnet docfx docs/docfx.json exit 0 (2 pre-existing generator warnings, 0 errors, docs/_site|api gitignored); added benchmarks/nextorm.benchmark/EagerPerParentPagingBenchmark.cs (category eager-perparent-paging, NOT acceptance; 1x8/100x20/1000x20, split+single+unpaged reference, setup proves page equivalence); acceptance --anyCategories=acceptance exit 0, exactly 7 cases/0 failures, wall 51 s, cached-vs-prepared ratio 2.19 vs baseline 1.87 = +17% (<20%, no investigation triggered); dedicated --filter *EagerPerParentPagingBenchmark* --job short exit 0, 9 InProcessEmitToolchain cases (Default out-of-process job blocked by pre-existing gitignored D208 snapshot nextorm.benchmark.csproj duplicate — environmental, not code); benchmarks/BenchmarkDotNet.Artifacts restored to tracked state | artifacts/pdca/D211-eager-perparent-topn-1/rv1/perf/{summary.md,acceptance.log,eager-perparent.log,benchmark-build.log,docfx.log,native/}
2026-10-11T20:41:56Z | DO | r1 | n1/3 | D:06 coverage+mutation complete — coverage 241/262 changed lines, 154/190 changed branch conditions; CI thresholds MIN_LINE_COVERAGE=85 met (aggregate 88.5%) and MIN_BRANCH_COVERAGE=75 met (aggregate 80.5%, warn-only on 1.0.9); resolved branches SqlBuilder.cs:747–750 covered and 742/747 resolved by the new ordinal/plan-cache follow-up tests; Group B (SqlBuilder.cs:824/827/829/831) classified unreachable/defensive via the pre-existing split-loader child plan-key divergence (groupb-classification); mutation run scoped to the T211 changed spans with the corrected CRLF byte-span config — Killed 138 / Survived 40 / NoCoverage 26 (score 67.65%), every active mutant lies inside the changed-line ranges, all 66 survivors/NoCoverage disposed (28 equivalent, 35 defensive, 3 killed by newly added Feature=D211 Skip-only/outer-ORDER-BY tests; multi-column composite-key partition renderer defensive-only — pre-existing unsupported composite keys, open risk recorded); canonical mutation-report refreshed from run-survivors | artifacts/pdca/D211-eager-perparent-topn-1/rv1/mutation/{survivors.md,survivors.json,README.md,mutation-report.json,mutation-report.html,groupb-classification.md,mutation-disposition.log}; artifacts/pdca/D211-eager-perparent-topn-1/rv1/{inner-loop-evidence.json,inner-loop/sqlite-trait-D211.log}
2026-10-11T20:46:05Z | DO | r1 | n1/3 | D211-COMP added under D:04/obligation :236 (r1) — composite parent-link key partition-isolation closure; rv1 unchanged | status file update only; plan r=1 unchanged, rv=1 unchanged
2026-10-11T20:57:54Z | DO | r1 | n1/3 | D211-COMP complete — row :236 closed by positive split tests + negative tests for composite-single-query and SQL Server tuple-IN; :241 intersection = split positive / single-query fail-closed; deviation: "positive in both modes" impossible for composite keys due to PRE-EXISTING guards, no REQ conflict, no production change | artifacts/pdca/D211-eager-perparent-topn-1/rv1/composite/{build-sqlite-tests.log,build-integration-tests.log,sqlite-composite-class.log}; artifacts/pdca/D211-eager-perparent-topn-1/rv1/integration/composite-{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}.log; artifacts/pdca/D211-eager-perparent-topn-1/rv1/inner-loop-evidence.json (validate_inner_loop.py report exit 0); docs/advanced/eager-loading-per-parent-paging.md + docs/ru/advanced/eager-loading-per-parent-paging.md (composite-key limitation EN+RU)
2026-10-11T21:12:32Z | DO | r1 | n1/3 | D:08 freeze complete — frozen rv1 manifest artifacts/pdca/D211-eager-perparent-topn-1/rv1/manifest.json; `python3 scripts/validate_inner_loop.py manifest .../manifest.json` exit 0; tree freeze/tree.json = 41-file T211 footprint, all CRLF; ledger freeze/ledger.json EV-01..EV-14 all met (0 open / 0 na, no gaps); every row's evidence path exists; collection autocommit for T211 follows | artifacts/pdca/D211-eager-perparent-topn-1/rv1/{manifest.json,freeze/tree.json,freeze/ledger.json,freeze/manifest-validation.log}
