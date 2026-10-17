# D211 — eager-load per-parent Take/Skip (#211)

## Identity and state

- task_id: T211; collection_id: `1.0.9-todo`
- issue: #211; absorbs closed #210
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: **r=4** (replan after CHECK(r3); A01 WithTies guard + A07/A08 + mutation budget; rv3 results historical, not rv4 certification)
- frozen evidence-contract revision: **rv=4** (supersedes rv3; contract `artifacts/pdca/D211-eager-perparent-topn-1/rv4/contract/rv4-contract.json`; manifest `artifacts/pdca/D211-eager-perparent-topn-1/rv4/evidence-manifest.json`, frozen during DO r=4)
- plan_state: **done**
- task status: **done — CHECK PASS (r=4, rv=4; 21/21 mandatory rows met, 0 open)**
- base: branch `1.0.9` @ `58025147`
- mode: autonomous; collection own-PLAN only
- status_file: `docs/specs/status/D211-eager-perparent-topn-1.md`
- permitted auto-commit: task/collection status files only; push never
- phase: ACT complete — CHECK PASS (r=4, rv=4); D:R4-00..D:R4-06 closed; D:06 EV-11 remainder resolved; D:08 accepted-handoff remainder closed

The r=1 terminal `incomplete` is preserved as history — retained commits `e98ae60e`, `5bd3028f`, `15c6253f`; patch `artifacts/pdca/D211-eager-perparent-topn-1/incomplete/D211-incomplete.patch`. Continuation is a genuinely revised plan r=2 per the escalate decision (evidence-capture gap, not a demonstrated product/config defect).

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

- `benchmarks/nextorm.benchmark/Focused/EagerPerParentPagingBenchmark.cs` — new
- `docs/advanced/eager-loading-per-parent-paging.md` — new
- `docs/ru/advanced/eager-loading-per-parent-paging.md` — new
- `docs/guide/04-joins.md`
- `docs/ru/guide/04-joins.md`
- `docs/guide/31-implicit-navigation.md`
- `docs/ru/guide/31-implicit-navigation.md`
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
- Update examples/links in `04-joins.md` and `31-implicit-navigation.md`, in both languages.
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
2026-10-11T15:30:58Z | DO | r1 | n1/3 | D:07 docs+perf complete — new EN/RU advanced page docs/advanced/eager-loading-per-parent-paging.md + docs/ru/advanced/eager-loading-per-parent-paging.md (advanced toc EN+RU; cross-links in guide 02-joins and 29-implicit-navigation EN+RU; corrected stale per-parent-top-N rejection claims in advanced/eager-loading.md EN+RU); dotnet docfx docs/docfx.json exit 0 (2 pre-existing generator warnings, 0 errors, docs/_site|api gitignored); added benchmarks/nextorm.benchmark/Focused/EagerPerParentPagingBenchmark.cs (category eager-perparent-paging, NOT acceptance; 1x8/100x20/1000x20, split+single+unpaged reference, setup proves page equivalence); acceptance --anyCategories=acceptance exit 0, exactly 7 cases/0 failures, wall 51 s, cached-vs-prepared ratio 2.19 vs baseline 1.87 = +17% (<20%, no investigation triggered); dedicated --filter *EagerPerParentPagingBenchmark* --job short exit 0, 9 InProcessEmitToolchain cases (Default out-of-process job blocked by pre-existing gitignored D208 snapshot nextorm.benchmark.csproj duplicate — environmental, not code); benchmarks/BenchmarkDotNet.Artifacts restored to tracked state | artifacts/pdca/D211-eager-perparent-topn-1/rv1/perf/{summary.md,acceptance.log,eager-perparent.log,benchmark-build.log,docfx.log,native/}
2026-10-11T20:41:56Z | DO | r1 | n1/3 | D:06 coverage+mutation complete — coverage 241/262 changed lines, 154/190 changed branch conditions; CI thresholds MIN_LINE_COVERAGE=85 met (aggregate 88.5%) and MIN_BRANCH_COVERAGE=75 met (aggregate 80.5%, warn-only on 1.0.9); resolved branches SqlBuilder.cs:747–750 covered and 742/747 resolved by the new ordinal/plan-cache follow-up tests; Group B (SqlBuilder.cs:824/827/829/831) classified unreachable/defensive via the pre-existing split-loader child plan-key divergence (groupb-classification); mutation run scoped to the T211 changed spans with the corrected CRLF byte-span config — Killed 138 / Survived 40 / NoCoverage 26 (score 67.65%), every active mutant lies inside the changed-line ranges, all 66 survivors/NoCoverage disposed (28 equivalent, 35 defensive, 3 killed by newly added Feature=D211 Skip-only/outer-ORDER-BY tests; multi-column composite-key partition renderer defensive-only — pre-existing unsupported composite keys, open risk recorded); canonical mutation-report refreshed from run-survivors | artifacts/pdca/D211-eager-perparent-topn-1/rv1/mutation/{survivors.md,survivors.json,README.md,mutation-report.json,mutation-report.html,groupb-classification.md,mutation-disposition.log}; artifacts/pdca/D211-eager-perparent-topn-1/rv1/{inner-loop-evidence.json,inner-loop/sqlite-trait-D211.log}
2026-10-11T20:46:05Z | DO | r1 | n1/3 | D211-COMP added under D:04/obligation :236 (r1) — composite parent-link key partition-isolation closure; rv1 unchanged | status file update only; plan r=1 unchanged, rv=1 unchanged
2026-10-11T20:57:54Z | DO | r1 | n1/3 | D211-COMP complete — row :236 closed by positive split tests + negative tests for composite-single-query and SQL Server tuple-IN; :241 intersection = split positive / single-query fail-closed; deviation: "positive in both modes" impossible for composite keys due to PRE-EXISTING guards, no REQ conflict, no production change | artifacts/pdca/D211-eager-perparent-topn-1/rv1/composite/{build-sqlite-tests.log,build-integration-tests.log,sqlite-composite-class.log}; artifacts/pdca/D211-eager-perparent-topn-1/rv1/integration/composite-{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}.log; artifacts/pdca/D211-eager-perparent-topn-1/rv1/inner-loop-evidence.json (validate_inner_loop.py report exit 0); docs/advanced/eager-loading-per-parent-paging.md + docs/ru/advanced/eager-loading-per-parent-paging.md (composite-key limitation EN+RU)
2026-10-11T21:12:32Z | DO | r1 | n1/3 | D:08 freeze complete — frozen rv1 manifest artifacts/pdca/D211-eager-perparent-topn-1/rv1/manifest.json; `python3 scripts/validate_inner_loop.py manifest .../manifest.json` exit 0; tree freeze/tree.json = 41-file T211 footprint, all CRLF; ledger freeze/ledger.json EV-01..EV-14 all met (0 open / 0 na, no gaps); every row's evidence path exists; collection autocommit for T211 follows | artifacts/pdca/D211-eager-perparent-topn-1/rv1/{manifest.json,freeze/tree.json,freeze/ledger.json,freeze/manifest-validation.log}
2026-10-11T21:30:11Z | DO | r1 | n2/3 | CHECK fail 1/3 (r1) → DO n2/3: C-1 split-shape fail-closed, unordered paging rejection, zero-bound semantics per Paging, doc corrections (planner clarification, rv1 unchanged) | dispatch brief T211 r=1 n=2/3 (rv=1); source artifacts/pdca/D211-eager-perparent-topn-1/rv1/check/{code-audit,test-lens}.md
2026-10-11T21:37:17Z | DO | r1 | n2/3 | CHECK C-1 closed: split BuildChildCommand + preparation seam now call the shared fail-closed ValidatePerParentPagingChild (unsupported shapes + unordered page) before any SQL; single-query uses the same guard; SqlBuilder rank guard fixed (partition mandatory, mode-independent, window ordering required). Red->green: pre-fix new negatives fail (theory 3/3 Failed, split-unordered 1/1, single-unordered 1/1, exit 1 each) vs post-fix green. Zero-bound Feature=D211 tests added (Take(0)/Skip(0)/default/limit0+offset). Docs EN+RU corrected (Take(0)!=empty, both-mode shapes, server prerequisite, MariaDB, unordered rejected); provider docstrings fixed. dotnet build nextorm.slnx -c Debug 0W/0E; core D211 4/4, sqlite D211 33/33, EagerLoadingSqlGenerationTests 53/53, provider D211 pg7/ss7/my11/ch7, full sqlite 1595/1skip, full core 2315; validate_inner_loop.py report exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv1/inner-loop/{n2-build,n2-core-trait-D211,n2-sqlite-trait-D211,n2-sqlite-class,n2-prefix-red}.log; artifacts/pdca/D211-eager-perparent-topn-1/rv1/inner-loop-evidence.json
2026-10-11T22:43:00Z | DO | r1 | n2/3 | DO correction finish (rv1) — evidence validator exit 0; build 0W/0E; provider-green coverage re-run 10846 tests (0 failed, no required provider skipped), aggregate 88.5% line / 80.6% branch (>=85/>=75; branch warn-only on 1.0.9); named changed-branch resolutions now covered: SqlBuilder.cs:734 (rv1 label :724, partition separator, 2/2), QueryPlanEqualityComparer.cs:166/167/170 (2/2, 4/4, 4/4), EntityBuilder.cs:1650 (4/4); Group B + param-mode mirrors classified defensive/unreachable (branch-dispositions.md); perf acceptance re-run 7/0 failures, tracked cached/prepared ratio 1.977 and all comparable Means within ±9% => noise-not-reproduced, no >20% growth | artifacts/pdca/D211-eager-perparent-topn-1/rv1/inner-loop/validate-report.out (validator exit=0); artifacts/pdca/D211-eager-perparent-topn-1/rv1/coverage/{summary.txt,branch-delta.txt,branch-delta.json,branch-dispositions.md}; artifacts/pdca/D211-eager-perparent-topn-1/rv1/perf/{acceptance-final-comparison.md,acceptance-final.log}
2026-10-11T22:43:01Z | DO | r1 | n2/3 | mutation evidence refreshed on the final corrected tree (defect mut-stale-span — previous spans predated the 02:35 production edits and run-final had 17 active mutants outside the changed ranges; spans recomputed from `git diff -U0 b527be6f -- src/**/*.cs` to CRLF raw-byte offsets, intended SqlBuilder.cs range now 628..901; Stryker re-run core+sqlite without DOCKER_HOST to run-final2): Killed 150 / Survived 34 / NoCoverage 26 / Timeout 4 (score 71.96%), 214/214 active mutants inside the recomputed changed-line ranges (0 outside), all 64 non-killed dispositioned (26 equivalent / 34 defensive / 4 timeout), SqlBuilder.cs:734 partition separator Killed; canonical mutation-report refreshed from run-final2 | artifacts/pdca/D211-eager-perparent-topn-1/rv1/mutation/{README.md,mutate-spans.json,stryker-config.corrected.json,survivors.md,survivors.json,mutation-report.json,mutation-report.html,mutation-final2.log,run-final2/reports/}
2026-10-11T22:50:40Z | DO | r1 | n2/3 | DO correction part 2c (rv1) — corrected-tree boundary re-verification: `dotnet build nextorm.slnx -c Debug` 0 Warning(s)/0 Error(s) exit 0; broad `--no-build` sweeps all exit 0 (core 2320/0, sqlite 1617/1 pre-existing skip, postgres 881/0, sqlserver 798/0, mysql 362/0, clickhouse 646/0); Feature=D211 integration sweep one provider at a time under DOCKER_HOST, all executed 0 skipped (SQLite 14/0, PostgreSQL 14/0, SQL Server 14/0, MySQL 14/0, ClickHouse 4/0, MariaDB 4/0); `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing warnings, 0 errors); freeze/tree.json re-frozen to the corrected 42-file footprint (file_set_sha256 a270418af71f89aad4cf1ef5388f682bb49937fc16d54728dc41a95476ab3a81); manifest EV-08/EV-12 paths refreshed, ledger caveats updated; `validate_inner_loop.py report` exit 0 and `manifest` exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv1/regression/{build-solution,core.tests,sqlite.tests,postgres.tests,sqlserver.tests,mysql.tests,clickhouse.tests,d211-integration-{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}}.log; artifacts/pdca/D211-eager-perparent-topn-1/rv1/perf/docfx-final.log; artifacts/pdca/D211-eager-perparent-topn-1/rv1/{regression.json,inner-loop-evidence.json,manifest.json,freeze/{tree,ledger}.json}}
2026-10-11T23:06:33Z | DO | r1 | n3/3 | P-task applicability V9/V11/V13/V21 (r1/rv1 unchanged) — DO n3/3; V13/V21 N/A with recorded evidence; V9/V11 split-path reachability closure; EV-06/EV-09/EV-10/EV-14 + XML prose fixes | artifacts/pdca/D211-eager-perparent-topn-1/rv1/freeze/applicability/{V9,V11,V13,V21}.md
2026-10-11T23:18:26Z | DO | r1 | n3/3 | Finalize (rv1) — coverage RE-SNAPSHOT on the final tree, provider-green: total 10850 / succeeded 10652 / failed 0 / skipped 198 (exit 0); aggregate line 88.5% (threshold 85) / branch 80.6% (threshold 75); changed-path delta vs base b527be6f UNCHANGED (254/279 lines, 160/194 branch conditions) — the only intervening production edit is comment-only XML-doc prose in SqlBuilder.MakePerParentRankExpression; content tree re-frozen to 48 files incl. the r1-n3 applicability artifacts (freeze/applicability/V9,V11,V13,V21.md) and the coverage/tool configs (coverage.settings.xml, .config/dotnet-tools.json), file_set_sha256 `6efc5e311d89b9411fb9c4624221a7e70f9edf922aa9528572c48bdc091d6f61`; `validate_inner_loop.py manifest` exit 0 and `report` exit 0; ledger/manifest updated with the single snapshot hash and file count | artifacts/pdca/D211-eager-perparent-topn-1/rv1/coverage/{coverage.xml,summary.txt,branch-delta.json,branch-delta.txt,branch-dispositions.md,coverage.log,reportgen.log,report/Summary.txt}; artifacts/pdca/D211-eager-perparent-topn-1/rv1/freeze/{tree.json,ledger.json,manifest-validation.log}; artifacts/pdca/D211-eager-perparent-topn-1/rv1/manifest.json

2026-10-12T00:32:17Z | ACT | r1 | n3/3 | STOP — task incomplete: r1 exhausted (3/3 CHECK fails); escalate re-gather (a): EV-14 met, EV-11 rejected (Stryker restart exit code inferred, not captured); no product/config defect demonstrated; remaining G1 tasks stopped; uncommitted T211 evidence saved as patch and reverted | artifacts/pdca/D211-eager-perparent-topn-1/incomplete/D211-incomplete.patch
2026-10-12T05:19:36Z | PLAN | r2 | plan_state ready; rv2 contract frozen; recon obtainability evidence (MUT_FINAL3_B exit 0, MUT_CORRECTED_B exit 0) recorded; ALL-barrier: re-opened by scope expansion — PASSED for the original set, must re-pass before the expanded lane continues (collection `1.0.9-todo` §Phase decisions); DO not started | artifacts/pdca/D211-eager-perparent-topn-1/rv2/recon/{SUMMARY.md,receipts/MUT_FINAL3_B.json,receipts/MUT_CORRECTED_B.json}
2026-10-12T06:24:45Z | DO | r2 | n1/3 | DO started — plan r=2 (rv=2) executed sequentially D:R2-01..D:R2-05 in the current worktree on branch 1.0.9; no worktree/branch/merge/push; ISqlDialect read-only; brief validated BEFORE any edit | artifacts/pdca/D211-eager-perparent-topn-1/rv2/brief.json; artifacts/pdca/D211-eager-perparent-topn-1/rv2/freeze/brief-validation.log (validate_inner_loop.py brief exit 0)
2026-10-12T06:24:45Z | DO | r2 | n1/3 | D:R2-01 closed (baseline/history) — identity #211 OPEN milestone 1.0.9, pinned base b527be6f ancestor, retained commits present, toolchain recorded; rv1 manifest+ledger copied byte-identically to history/; effective-rv1-index records EV-11 met→open citing status:529–538, EV-14 stays met; baseline tree 48-file file_set_sha256 89b0... (2 mismatches vs rv1: docs/ru/toc.yml sibling edit, status file r2 plan) | artifacts/pdca/D211-eager-perparent-topn-1/rv2/{baseline/identity.json,baseline/toolchain.json,baseline/tree.json,baseline/support.json,history/manifest.json,history/ledger.json,history/provenance.json,history/effective-rv1-index.json}
2026-10-12T06:24:45Z | DO | r2 | n1/3 | D:R2-02 closed (capture/probe/brief) — recon wrapper productionised at rv2/capture.py (direct child launch; real exit/signal/timeout; 253 launch-fail, 252 timeout; receipts + logs); probe.py subcommands baseline/oracle/core/sql/integration/api-cache/guards/regression/build/coverage/mutation/docs/perf/freeze/capture-audit/history; brief.json = plan line-640 JSON (26 files, selector Feature=D211, rebuild affected), validate_inner_loop.py brief exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv2/{capture.py,probe.py,brief.json,mutation/stryker-config.json,mutation/stryker-config.final3.json,coverage/changed_branch_delta.py}
2026-10-12T06:24:45Z | DO | r2 | n1/3 | D:R2-03 closed (recertification, evidence-only; no product edits) — build Debug/Release 0W/0E exit 0; Feature=D211 filtered: core 9/0, sqlite 58/0, PG 7/0, SS 7/0, MySQL 11/0, ClickHouse 7/0 (mariadb.tests has no D211 trait, excluded; MariaDB variants run in nextorm.mysql.tests); integration -trait Feature=D211 64/0/0 skipped, 6 families (5 containers+SQLite); whole-solution `dotnet test -c Debug --no-build` 10850 total / 0 failed / 10652 succeeded / 198 skipped exit 0; coverage aggregate 88.5% line / 80.6% branch (>=85/75), changed-delta 254/279 lines, 160/194 branches (identical to rv1); docfx exit 0 (2 pre-existing warnings); acceptance 7/7 cases wall 45 s, cached/prepared time 2.033 (+8.7% <20%), alloc 7.741 (+4.3% <20%); eager short run exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv2/{build/summary.json,core/summary.json,oracle/summary.json,sql/summary.json,guards/summary.json,api-cache/summary.json,integration/summary.json,regression/summary.json,coverage/summary.json,docs/summary.json,perf/summary.json,perf/acceptance-parsed.json,receipts/REGRESSION-SOLUTION-V2.json}
2026-10-12T06:24:45Z | DO | r2 | n1/3 | D:R2-04 closed (mutation remainder) — canonical Stryker `stryker-config.json` concurrency 4 real exit 0 (1148.183 s): Killed 147 / Survived 34 / NoCoverage 26 / Timeout 4 = 211 active, score 71.56%; restart `stryker-config.final3.json` concurrency 1 real exit 0 (1047.326 s): Killed 1 / Survived 6 / Timeout 0 = 7 active, score 14.29%; 64 non-killed reconciled (26 equivalent / 34 defensive bound to rv1 dispositions via documented +/-1 line offset; 4 historical Timeouts reclassified equivalent on the paired concurrency-1 restart), 0 unmapped / 0 feasible-open; Safe-Mode 8546 CompileError, none on target lines (QueryPreparer 326/433/2688/2953/4374/4781/5664/5752 ≠ 131; QueryPlanEqualityComparer none); span count 50 (plan prose says 49 — preserved as-is) | artifacts/pdca/D211-eager-perparent-topn-1/rv2/mutation/{summary.json,reconciliation.json,survivors.json,README.md}; artifacts/pdca/D211-eager-perparent-topn-1/rv2/receipts/{MUTATION-CANONICAL,MUTATION-RESTART,MUTATION-RECONCILE}.json
2026-10-12T06:24:45Z | DO | r2 | n1/3 | D:R2-05 closed + DO→CHECK boundary — report.json validated (executions: build Debug + inner core/sqlite Feature=D211 + solution sweep, all exit 0) exit 0; freeze/tree.json + freeze/ledger.json; manifest.json 17/17 rows met (EV-01..14 + RC-01..03), 0 open, validate_inner_loop.py manifest exit 0; capture-audit self-tests 0 and 7 both captured | artifacts/pdca/D211-eager-perparent-topn-1/rv2/{report.json,manifest.json,freeze/tree.json,freeze/ledger.json,freeze/report-validation.log,freeze/manifest-validation.log,capture-audit/receipts-index.json}

## Final disposition — STOP (ACT)

- task status: **incomplete** (terminal; no r=2, no product/config defect)
- reason: r1 exhausted (3/3 CHECK fails); escalate re-gather (a): EV-14 met, EV-11 rejected (Stryker restart exit code inferred, not captured); no product/config defect demonstrated; STOP
- retained commits (branch `1.0.9`, all kept, none reverted): `e98ae60e`, `5bd3028f`, `15c6253f`
- evidence root: `artifacts/pdca/D211-eager-perparent-topn-1/rv1/`
- uncommitted T211 evidence saved as a patch and reverted to the tip `15c6253f`: `artifacts/pdca/D211-eager-perparent-topn-1/incomplete/D211-incomplete.patch`
- group effect: lane G1 stopped; remaining tasks T212/T213/T201/T53 were never executed («группа остановлена»); single group G1 → no merge; branch `1.0.9` unchanged.

## PLAN r=2 (rv=2) — evidence-capture remediation

## T211 / #211 — FINAL PLAN r=2, evidence contract rv=2
Decision: Gate 1 is complete; r=2 is ready to persist. Recon proves the decisive probe is obtainable; it does NOT establish EV-11 acceptance. Same r=2; no new revision or artificial attempt reset.

### Goal and acceptance criteria
Preserve verified r1 product scope; remediate evidence capture, mutation reconciliation and current-tree binding. Each criterion includes its negative case:
- REQ-01: Collections equal independent `filter→order→Skip→Take`, including Take/Skip/combined/empty; negative: global limiting must not starve later parents.
- REQ-02: Both eager modes preserve parents, empty collections, root paging and supported nesting; navigations page independently; negative: child ranking cannot remove parents or alter root bounds.
- REQ-03: Predicates, supported projections/keys/order survive; normalization uses `Paging`; negative: invalid/unsupported shapes fail before SQL, never silently page globally/client-side.
- REQ-04: Builder/QueryCommand sync/async and arg/params parity, cancellation and ownership remain; no rename/remove; negative: no leaked resources, caller-owned disposal or partial reusable state.
- REQ-05: Cache separates shapes and rebinds values without shared-command mutation; negative: alternating bounds/interleaved Any/Count cannot retain bounds or sticky `_dontCache`.
- REQ-06: Six providers produce valid SQL/reference results on supported configurations; negative: existing window/version/derived-source restrictions reject unsupported configurations.
- REQ-07: Unpaged eager loading and unrelated rejected shapes retain behavior; negative: removing blanket paging rejection cannot remove unrelated guards or expand public API.
- REQ-08: Existing EN/RU docs explain semantics, ties, modes and fail-closed restrictions; negative: no global-paging example, broken bilingual link or public link into specs.
- REQ-09: Warning-free builds, coverage/changed-P1 branches and targeted mutation satisfy r1 rules, with actual process-exit receipts; negative: warnings, empty runs, feasible uncovered P1 branches, non-equivalent P1 survivors or unresolved applicable tool incompletion cannot pass.
- REQ-10: Exactly seven acceptance cases finish without failure within 240 seconds; retain wall/Mean/Allocated/ratios and investigate comparable growth >20%; negative: missing case/baseline, failure or timeout is unresolved.
- REQ-11: Evidence is reproducible, row-complete and bound to frozen tree/rv, with actual exits for mandatory commands; negative: required skips, absent receipts/reports or unresolved references leave rows open.

### Minimal solution and alternatives
Goal: trustworthy evidence. Constraints: unchanged product obligations, historical bytes, stable IDs, truthful incompletion. Optimum: artifact-local capture/probe pipeline plus scoped reconciliation.
- Infer historical exits — reject (unverifiable, contradicts ACT STOP).
- Fresh captured runs plus validated historical reuse — CHOOSE (moderate cost; repairs capture/binding).
- Replace mutation with ordinary tests — reject (weakens REQ-09).

### What the specification did not say
Current sibling fingerprints → record before reuse; compile-error relevance → full target-span accounting, not blanket exclusion; exact future test symbols → discover with Roslyn, never invent. Assumptions: retain r1's registered #211 URL/milestone and baseline-support classification; verify in D:R2-01. No remaining work moves to another milestone.

### Genuine r=2 units and original-unit mapping
- D:R2-01 — baseline/history, evidence-only, fix now: snapshot tree/toolchain/support/issue identity; copy historical manifest/ledger byte-identically; record effective EV-11 correction.
- D:R2-02 — capture/probe/brief, evidence-only, fix now: productionize recon wrapper under rv2; inventory mandatory invocations; validate brief before any implementation edit.
- D:R2-03 — recertification, evidence-only, fix now: bind reusable r1 evidence or rerun EV-02…10/12/13; requirements and product scope unchanged.
- D:R2-04 — mutation remainder, fix now: fresh canonical and restart receipts, full-span accounting and mutant disposition; add only tests needed for original D:06 obligations. No speculative production fix.
- D:R2-05 — report/freeze, evidence-only, fix now: validate report, complete ledger/current-tree binding and manifest; then freeze DO→CHECK.
Dependencies: 01→02→03/04→05, sequential. A verified product defect returns through CHECK/PLAN rather than silently expanding this evidence remediation. Original D:04/D:04a remain closed; retain other historical execution-completion records. D:06's rejected EV-11 acceptance remainder and D:08's accepted-handoff remainder remain active, dependent on R2-04/R2-05; neither is superseded. D:07 receives evidence recertification, not new product scope.

### Execution mode, footprint and sibling boundary
Sequential units in the current tree; no worktree, branch, commit, merge or push. Serialize with #209/#212/#213; record incoming/outgoing tree and file-set fingerprints. A sibling change invalidates affected evidence until rechecked.
Retained production footprint: `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`, `Builders/EntityBuilder.cs`, `DataContext/SqlBuilder.cs`; no speculative edits. `DataContext/Dialect/ISqlDialect.cs` remains read-only.
Tests: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,clickhouse}.tests/EagerLoadPerParentPagingTests.cs`; `tests/nextorm.mysql.tests/nextorm.mysql.tests.csproj`.
Integration: `tests/nextorm.integration.tests/CommonTestSuite.EagerLoadPerParentPaging.cs`, `Providers/MariaDbEagerPagingFixture.cs`, `MariaDbEagerPagingTests.cs`.
Benchmark/docs: `benchmarks/nextorm.benchmark/Focused/EagerPerParentPagingBenchmark.cs`; EN/RU `advanced/eager-loading-per-parent-paging.md`, `guide/04-joins.md`, `guide/31-implicit-navigation.md`.
Evidence/status: `artifacts/pdca/D211-eager-perparent-topn-1/rv2/**` including preserved `recon/**`; `docs/specs/status/D211-eager-perparent-topn-1.md`. Read rv1; never overwrite it.
No package/API-name/guide-numbering/provider-production/Paging/FromExpression/JunctionLinkSourceFactory changes.

### Design checklist
No new C# product types. Check metadata ownership/immutability, `Paging` reuse, equality/hash shape distinction, local cache policy, fail-closed capability gates, scalar/composite/null/default keys, projection/alias integrity, root-vs-child separation and independent navigations. Preserve async naming, cancellation/resource ownership, nullable/analyzer cleanliness and CRLF; no shared-command Cache mutation. Use Roslyn for every C# symbol/impact question.

### Versioned evidence contract — rv=2
Frozen path: `artifacts/pdca/D211-eager-perparent-topn-1/rv2/manifest.json`. Rv2 supersedes the active rv1 contract; preserve EV IDs and obligations, add RC IDs. All rows rv=2, applicability Always. Every row starts open.
Rows (ID/req — scenario; probe+leaf invocation; evidence/owner):
- EV-01/REQ-11 identity, fixture, support, fingerprints; `probe.py baseline`; baseline log/JSON/snapshot; DO-baseline.
- EV-02/REQ-01 independent oracle/adversarial global limit; `probe.py oracle` + core tests; reference-v1 inputs/results in oracle/; DO-core.
- EV-03/REQ-01,03 paging/order/filter/key/default/overflow matrix; `probe.py core` + core tests; core/; DO-core.
- EV-04/REQ-02,06 both-mode SQL partition/bounds/aliases/projection; `probe.py sql` + six provider SQL-gen tests separately; sql/; DO-sql.
- EV-05/REQ-01,02,06 real six-family execution/both modes; `probe.py integration` + integration trait run (DOCKER_HOST); integration/; DO-integration.
- EV-06/REQ-04,05,07 surfaces, parity, cancellation/ownership, rebind/interleaving/API unchanged; `probe.py api-cache` + core tests + solution regression; api-cache/; DO-execution.
- EV-07/REQ-03,06,07 shape/capability/version guards + baseline-selected in-memory behavior; `probe.py guards` + tests (core+seven); guards/; DO-guards.
- EV-08/REQ-07,09 seven-project targeted + whole-solution regression; `probe.py regression` + tests + `dotnet test`; regression/; DO-regression.
- EV-09/REQ-09 warning-free Debug/Release + CRLF; `probe.py build` + `dotnet build -c Debug`/`-c Release`; build/; DO-build.
- EV-10/REQ-09 aggregate thresholds + feasible changed-P1 branches; `probe.py coverage` + coverage collect/report/delta argv; coverage/; DO-coverage.
- EV-11/REQ-09 complete scoped mutation/dispositions; `probe.py mutation` + canonical + restart argv; mutation/; DO-mutation.
- EV-12/REQ-08 existing bilingual content/link checks + DocFX; `probe.py docs` + `dotnet docfx docs/docfx.json`; docs/; DO-docs.
- EV-13/REQ-10 seven acceptance cases + dedicated eager measurements; `probe.py perf` + acceptance/eager argv; perf/; DO-perf.
- EV-14/REQ-11 current content tree, row completeness, references; `probe.py freeze` + manifest validator; freeze/; DO-evidence.
- RC-01/REQ-09,11 fresh self-tests + complete receipt inventory; `probe.py capture-audit`; capture-audit/receipts-index.json, 0/7 self-test receipts; DO-evidence.
- RC-02/REQ-11 historical byte preservation/effective correction; `probe.py history`; history/manifest.json, history/ledger.json, hashes/provenance, effective-rv1-index.json; DO-evidence.
- RC-03/REQ-11 valid brief before edits/report before CHECK; inner-loop validator brief/report; brief.json, report.json, validator logs/receipts; DO-evidence.
Manifest retains valid id/status/evidence[]; statuses only met/open/na. Open rows carry reasons; na requires a proved observable predicate. No Always row is waived wholesale. No PASS while any required row is open. Reuse r1 evidence only with matching applicability/source/tool context, valid exit evidence and documented fingerprint verification; otherwise rerun.
History correction: preserve byte-identical rv1 files; separate effective index records EV-11 met→open, citing the rejected evidence/ACT STOP (`status:529–538`), without a fabricated defect key. Historical EV-14 stays met (`binding-reconciliation.md:10–56`).
Recon receipts/reports are pre-validated obtainability evidence only, not frozen DO mutation evidence. Fresh receipts/configs/reports are required for EV-11.
CHECK re-gather: owner CHECK-evidence, two dispatches total, ≤15 minutes each, one existing row/one reproducible invocation, no source edits; append-only. Exhaustion leaves CHECK unresolved; verified defects still FAIL.

### Commands, capture and evidence conventions (cwd R=/home/alex/sources/nextorm; A=artifacts/pdca/D211-eager-perparent-topn-1/rv2)
Capture wrapper `A/capture.py` records the real exit for EVERY mandatory command (direct child launch; actual exit/signal/timeout; mirrored wrapper exit; argv/cwd/environment/timestamps; source/artifact revision; retained stdout/stderr + receipt). Default expected exit 0; self-tests expect 0 and 7.
- core: `dotnet test tests/nextorm.<p>.tests -c Debug --filter Feature=D211` (p enumerated per row; ≥1 selected).
- integration: `dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait Feature=D211 -noColor` with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`.
- build: `dotnet build -c Debug` and `-c Release`.
- solution regression: `dotnet test -c Debug --no-build --verbosity normal` (same DOCKER_HOST).
- coverage: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "python3 A/capture.py --id coverage-tests -- dotnet test --no-build --verbosity normal"`; report: `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`; delta: `python3 A/coverage/changed_branch_delta.py`.
- mutation canonical: `dotnet tool run dotnet-stryker -- --config-file A/mutation/stryker-config.json --output A/mutation/run-canonical --concurrency 4 --skip-version-check --verbosity info`; restart: same with `A/mutation/stryker-config.final3.json`, output `A/mutation/run-restart`, concurrency 1; DOCKER_HOST unset for both; capture BOTH actual exits, never infer restart success; documented 1800-second bound each.
- perf: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; eager: same + `--filter *EagerPerParentPagingBenchmark* --job short`.
- docs: `dotnet docfx docs/docfx.json`.
- validators: `python3 scripts/validate_inner_loop.py brief A/brief.json`, `... report A/report.json`, `... manifest A/manifest.json`; expected exit 0.

### Tests, branch evidence and mutation disposition
xUnit `Feature=D211` across seven projects, independent `EagerPerParentPagingReference-v1`, whole-solution regression, real six-provider integration. Reject empty filtered runs and required provider skips. Load integration skill first; pin supplied images incl. MariaDB 11.4. Changed-delta rv2 derivative preserves rv1 algorithm but binds the full pinned r1 base and T211 ranges through retained commits/current tree — not merely `git diff HEAD`; record full base/hash, CRLF span map and P1 branch inventory. Aggregate comparison 85/75 (hard-fail main, warn elsewhere); feasible uncovered changed P1 branches remain failures independently. Preserve 49-span canonical scope, core+sqlite/perTest/MTP/Standard and 80/60/0; regenerate byte offsets against frozen CRLF sources with an old→new span map; exit 0/break0 is not mutation acceptance. Reconcile all scoped mutants, not only the two restart files; ordinary invalid mutants need compiler diagnostics distinct from Safe-Mode removal. Map Safe-Mode's 8546 CompileErrors/CS0165 against all applicable spans; proven outside-scope exclusions are retained caveats; unresolved applicable removal keeps EV-11 open. The canonical `QueryPlanEqualityComparer.cs:468:13` Timeout: paired concurrency-1 result establishes termination; its Survived outcome still requires equivalence/unreachability proof or a killing test. Reconcile all four historical Timeouts explicitly. No surviving/uncovered non-equivalent P1 mutant passes; defensive disposition requires guard/reachability evidence; no score-based waiver; needed tests stay inside the retained footprint.

### Variant matrix — all 24 rows closed by test or guard+test; none deferred
V01–02 unpaged eager: test; Take-only/Skip-only/combined: tests. V03–04 zero/default/negative/extreme bounds: normalization/guard tests; empty source/no matches/past-end: tests. V05–06 asc/desc/multi-key: tests; no-order/ties: documented ordering-contract tests. V07–08 scalar/composite links: supported tests/unsupported guards; nullable/reference/default keys: tests. V09–10 reference entities/supported value projections: tests; predicates/supported projections: tests. V11–12 aggregate/grouped/distinct/set-op forms: guard+negative tests; split/single: tests. V13–14 nested/two independently paged navigations: tests; root plus child paging: tests. V15–16 LoadWith factories/JoinInto overloads: tests; implicit nav without metadata: regression test (`NavigationExpansion.cs:43`). V17–18 builder/prepared commands: tests; sync/async/pre-cancel/read-cancel: tests. V19–20 cached/noncached/rebind/interleaving: tests; all six providers: SQL+real-execution tests. V21–22 unsupported window/version/derived sources: guard+rejection tests; supported in-memory nav: tests. V23–24 unsupported in-memory nav: guard test; normalization overflow/default boundary combinations: tests. V24 makes the supplied combined matrix's boundary combinations explicit; no new behavior. Record every subvariant, incl. baseline-selected in-memory support. Priority: P1 by construction for REQ invariants and their execution/translation/cache/guard lines; include applicable preparer/comparer spans despite read-only ownership; CHECK cannot downgrade P1.

### Docs, performance and reconnaissance
Docs: verify the six existing EN/RU pages, fail-closed restrictions/ties/links and DocFX; repair only demonstrated inaccuracies; preserve guide numbering; no public links into specs.
Perf required (`acceptance-benchmarks.md:15,37–69,109–116`): Count 2.915 ms/335.17 KB; GroupByCount 60.95 ms/50 MB; Any Cached 1.772 ms/534.42 KB; CachedPlan Prepared_ToList 923.8 μs/76.14 KB; Cached_ToList 1,727.4 μs/565.22 KB; Cached_PlanOnly_Param 518.3 μs/489.08 KB; Where Cached_ToListAsync 2.137 ms/692.07 KB. Exactly those seven cases, wall ≤240 s; baseline external/BDN 53/51.88 s, cached/prepared time 1.87 and allocation 7.42. Investigate >20% comparable growth; dedicated eager short job stays outside acceptance.
Recon satisfied: 0/7 capture self-tests and captured-exit MUT_FINAL3_B (951.61 s, exit 0) / MUT_CORRECTED_B (1117.94 s, exit 0) establish obtainability; their durations justify the new bound.

### Final DO-brief test-scope JSON
{"unit":"D:R2-evidence-remediation","scope":{"projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.postgres.tests","tests/nextorm.sqlserver.tests","tests/nextorm.mysql.tests","tests/nextorm.mariadb.tests","tests/nextorm.clickhouse.tests","tests/nextorm.integration.tests"],"selectors":["Feature=D211"],"files":["src/nextorm.core/Builders/EntityBuilderEagerLoading.cs","src/nextorm.core/Builders/EntityBuilder.cs","src/nextorm.core/DataContext/SqlBuilder.cs","tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.postgres.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.sqlserver.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.mysql.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.mysql.tests/nextorm.mysql.tests.csproj","tests/nextorm.clickhouse.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.integration.tests/CommonTestSuite.EagerLoadPerParentPaging.cs","tests/nextorm.integration.tests/Providers/MariaDbEagerPagingFixture.cs","tests/nextorm.integration.tests/MariaDbEagerPagingTests.cs","benchmarks/nextorm.benchmark/Focused/EagerPerParentPagingBenchmark.cs","docs/advanced/eager-loading-per-parent-paging.md","docs/ru/advanced/eager-loading-per-parent-paging.md","docs/guide/04-joins.md","docs/ru/guide/04-joins.md","docs/guide/31-implicit-navigation.md","docs/ru/guide/31-implicit-navigation.md","docs/specs/status/D211-eager-perparent-topn-1.md","artifacts/pdca/D211-eager-perparent-topn-1/rv2/capture.py","artifacts/pdca/D211-eager-perparent-topn-1/rv2/probe.py","artifacts/pdca/D211-eager-perparent-topn-1/rv2/coverage/changed_branch_delta.py","artifacts/pdca/D211-eager-perparent-topn-1/rv2/mutation/stryker-config.json","artifacts/pdca/D211-eager-perparent-topn-1/rv2/mutation/stryker-config.final3.json"],"rationale":"Same verified r1 feature scope; r2 repairs capture, mutation accounting and evidence binding. Inner tests select Feature=D211; all mandatory boundary checks remain required.","rebuild":"affected","boundary":"Warning-free Debug/Release builds; whole-solution regression and coverage; real six-provider integration; scoped mutation plus restart; docs; seven-case acceptance and dedicated eager measurements; report/manifest validation."}}

### Risks, handoff and CHANGE_PLAN
Risks: sibling drift, CRLF span drift, timing-dependent mutants, hidden Safe-Mode exclusions, incomparable perf environments; mitigate via fingerprints, span accounting, paired runs and explicit caveats/open rows.
CHANGE_PLAN: add real-exit capture for every mandatory command, byte-preserved history/effective EV-11 correction, complete scoped mutation reconciliation, validated brief/report and rv2 rebinding; unchanged product criteria, no superseded→replacement mapping.
Gate 1: complete. Ready to persist: yes. Confidence: high in plan/obtainability; mutation acceptance remains deliberately unproven until DO closes EV-11.

## PLAN handoff

- task_id: T211
- plan_state: **ready**
- plan_revision: **r=2**
- frozen evidence-contract revision: **rv=2** — `artifacts/pdca/D211-eager-perparent-topn-1/rv2/manifest.json`
- PLAN commit: d242778f
- Evidence-capture fix: real-exit capture for every mandatory command (`A/capture.py`); byte-preserved rv1 history with a separate effective index recording EV-11 met→open; complete scoped mutation reconciliation incl. the restart receipt; validated brief/report and rv2 rebinding.
- Planned write-footprint: production `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`, `Builders/EntityBuilder.cs`, `DataContext/SqlBuilder.cs`; tests `EagerLoadPerParentPagingTests.cs` across seven provider test projects + `nextorm.mysql.tests.csproj`; integration `CommonTestSuite.EagerLoadPerParentPaging.cs` / `MariaDbEagerPagingFixture.cs` / `MariaDbEagerPagingTests.cs`; `EagerPerParentPagingBenchmark.cs`; EN/RU eager-loading docs + guide 02/29; `artifacts/pdca/D211-eager-perparent-topn-1/rv2/**`; this status file.
- Prerequisites/assumptions: unchanged r1 product scope and registered #211 URL/milestone; `ISqlDialect` read-only; no package/API-name/guide-numbering/Paging/FromExpression/JunctionLinkSourceFactory change; assumptions verified in D:R2-01. Recon receipts (`artifacts/pdca/D211-eager-perparent-topn-1/rv2/recon/**`) are obtainability evidence only — not frozen DO mutation evidence; fresh receipts are required for EV-11.
- Requirements on siblings: serialisation with #209/#212/#213 in the current tree; record incoming/outgoing tree + file-set fingerprints; a sibling change invalidates affected evidence until rechecked; no worktree/branch/commit/merge/push during DO.
- ALL-barrier: **re-opened** by the scope expansion — it PASSED for the original set, and must re-pass before the expanded lane continues (collection `1.0.9-todo` §Phase decisions).
- Next allowed step: collection authorises DO for T211 → begin D:R2-01. DO has not started; this ready state is not passage of the ALL-barrier.

## PLAN r=3 (rv=3) — invocation-map + evidence corrections

# T211 / #211 — PLAN r=3, evidence contract rv=3

Cycle `D211-eager-perparent-topn-1`, N=1. Status file `docs/specs/status/D211-eager-perparent-topn-1.md`. Transition CHECK(r2,n1) FAIL → PLAN(r3) → DO(r3,n1/3). Return to PLAN does not consume an attempt. Decision: fix the mandatory invocation map; do NOT create duplicate MariaDB fixtures.

## 1. Goal and boundaries
Close the demonstrated D211 gaps: (1) implement independent `EagerPerParentPagingReference-v1`; (2) close V17 argument form and `.ToList(@params)`; (3) fix the wrong mandatory MariaDB invocation map; (4) fix the contradictory XML; (5) restore complete mutation accounting, mandatory-receipt inventory and consistent freeze. Do NOT change production behavior on unverified audit candidates. `ISqlDialect` and public API preserved. Do not mutate the shared `QueryCommand`, incl. sticky cache flags.

### Requirement acceptance (each with a negative case)
- REQ-01 independent reference + correct per-parent filter/order/paging incl. adversarial global-limit fixture; negative: global limit instead of per-parent or a wrong reference expectation is detected.
- REQ-02 both modes implement partition/bounds and correct aliases/projection in SQL; negative: SQL without partition or wrong bounds fails.
- REQ-03 paging/order/filter/key/default/overflow and shape/guard obligations preserved; negative: unsupported/incorrect input must not become silently wrong.
- REQ-04 execution surfaces + V17 incl. `.ToList(@params)`; negative: arg vs params divergence or ignored changed parameter detected.
- REQ-05 cancellation/ownership EV-06 preserved; negative: lost cancellation or ownership violation is not success.
- REQ-06 SQL + real execution cover six provider families and both supported modes; negative: missing provider/mode/empty selector not masked by an aggregate green.
- REQ-07 rebind/interleaving, API, guards, baseline-selected in-memory behavior and regressions preserved; negative: shared command/cache state leaks.
- REQ-08 XML consistent with behavior; bilingual/link checks and DocFX pass; negative: contradictory comment or broken link remains error.
- REQ-09 0 warnings/errors, CRLF; coverage ≥85% lines / ≥75% branches; feasible changed-P1 branches and scoped mutations have evidential dispositions; negative: green aggregate does not close an unexplained P1 branch/mutant.
- REQ-10 all seven acceptance cases + dedicated eager measurement per original perf criterion; negative: missing case, incomparable baseline or original-threshold breach not accepted.
- REQ-11 current content identity, 17 substantively closed rows, complete receipts, historical preservation and valid brief/report/manifest; negative: `met` without evidence, stale fingerprint, lost receipt or rewritten history not accepted.

## 2. Minimal solution
Essence: test/evidence corrections, mandatory-invocation fix and XML; do not change the production algorithm yet. Inviolable: preserve IDs, all provider/scenario obligations, oracle independence, honest exits, historical bytes. Optimum: run MariaDB variants where they already live (`nextorm.mysql.tests` and existing integration tests).
Alternatives: keep r2 and add MariaDB fixtures (plus: literal selector; minus: duplicate fixtures) — rejected; r3 fix the invocation map (chosen); delete the empty selector without replacement — forbidden (weakens the obligation).

## 3. What the statement did not say
Separate MariaDB fixture? No — `MariaDbContainer` reuse confirmed; `Providers/MariaDbEagerPagingFixture.cs` absent/not required. MariaDB D211 variants run in `nextorm.mysql.tests/MariaDbEagerLoadPerParentPagingTests.cs` and integration `MariaDbEagerPagingTests.cs`. Audit-candidate reachability: separate mandatory scout before production edits. Canonical span count: build from raw 50, not prose 49. Old green reports: baseline/history only unless applicability proven; r3 mandatory leaf runs are re-executed. Silent validator success = real exit 0 + receipt + honest execution metadata even with empty stdout. Tied ordering determinism: no new tie-breaker; reference accepts the contractual ordering/membership. DO authorisation: gate-1 complete; normal mode waits for explicit `go`, autonomous continues after gate 1.

## 4. Units and dependencies (all new sources below are PLANNED)
- D:R3-01 DO-evidence/DO-baseline — freeze r3/rv3, invocation map, content identity, brief; rv3 capture/probe helpers; self-tests 0/7; historical bytes/provenance. Rows EV-01, RC-01, RC-02, RC-03.
- D:R3-02 DO-core — independent reference-v1 + adversarial check; export real inputs/reference/results, not just a test summary. EV-02, EV-03.
- D:R3-03 DO-execution — params parity tests in existing D211 test files; close V17 incl. changed parameter values and re-execution. EV-06.
- D:R3-04 DO-sql/DO-guards/DO-regression — apply §7 map; both modes and six families; every mandatory targeted invocation selected_count ≥1. EV-04, EV-05, EV-07, EV-08.
- D:R3-05 DO-docs — fix XML `EntityBuilderEagerLoading.cs:70–72` without behavior change; bilingual/link checks + DocFX. EV-12.
- D:R3-06 scout, PLAN decision — gather reachability/guard/result evidence for §10 audit candidates; no production edit from a single raw remark. EV-03, EV-06, EV-07, EV-10, EV-11.
- D:R3-07 DO-build/coverage/mutation/perf — quality acceptance campaign; full P1 branch and canonical/restart mutation dispositions; seven perf cases + eager measurement. EV-09, EV-10, EV-11, EV-13.
- D:R3-08 DO-evidence — full receipt inventory; reconciled ledger/manifest; valid report before CHECK; freeze current tree. RC-01, RC-03, EV-14; reconcile EV-13.
- D:R3-09 CHECK-evidence — independently verify 17 rows and variants; bounded re-gather per §13.
- D:R3-10 DO-evidence — after independent CHECK prepare accepted-handoff remainder; DO does not issue the final verdict. EV-14 + original D:08.

Unfinished original units: D:06 stays active (original EV-11 acceptance remainder; existing R2-04/R2-05 deps; now also D:R3-07 and independent EV-11 check). D:08 stays active (original accepted-handoff remainder; existing deps; now also D:R3-08, CHECK, D:R3-10). Neither is done/superseded by a blocker report or candidate analysis. No superseded→replacement mapping: new tasks are additive prerequisites.

## 5. Execution mode and design checklist
Sequential in one tree. Tests, invocation map, report and evidence identity overlap; worktrees/parallel source edits not needed. Parallel read-only reconnaissance allowed if it does not change the tree and records a fingerprint of the inspected state.
Checklist: independent oracle must not use the verified SQL/window/ORM paging to compute expected; reference applies child filter and per-parent ordering/paging per contract; no new deterministic tie contract; args/params on the same dataset and with changed parameters; shared command/cache not mutated per call; cancellation/ownership/rebind/interleaving remain mandatory; `ISqlDialect`, public API and async naming unchanged; central packages unchanged without need; CRLF preserved; nullable/analyzer warnings not silently suppressed; MariaDbContainer reuse preserved and the nonexistent fixture must not appear as executed in evidence; C# definitions/references/reachability Roslyn-first; no commit/push/merge.

## 6. Oracle and test strategy
Independent reference-v1 lives in confirmed existing D211 test files (no future C# symbol assumptions): `tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs`; `tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs`; edge cases in `EagerLoadPerParentPagingEdgeTests.cs`. Reference computes expected from raw parent/child data in ordinary in-process logic, separate from the executed ORM path. Evidence records: reference version; raw data/filters/ordering/paging; expected per parent; actual per mode; comparison result; an adversarial case where a global limit differs. `probe.py oracle` must check these data. A repeated SQLite trait suite without reference inputs/results does NOT close EV-02. For total-order fixtures compare exact results; for ties without a contractual tie-breaker check admissible membership/cardinality and contractual ordering, not an invented ID sequence.
Unit/integration: core (guards, reference/bounds/default/overflow, execution surfaces, cache invariants); SQLite (execution/reference + edge cases without container provisioning); SQL-gen (each of six families separately); integration (real mandatory provider cases with `DOCKER_HOST`, both modes, no provider skips); whole solution (out-of-D211 compatibility); coverage (repo settings, both thresholds, separate changed-P1 branch accounting). Load `running-integration-tests` before integration; on missing socket do the skill's Podman recovery and rerun; never declare a missing provider green.

## 7. Exact invocation map
Seven targeted projects: core; SQLite; PostgreSQL; SQL Server; MySQL incl. MariaDB variants; ClickHouse; integration. Additionally the MariaDB SQL family has a separate filtered invocation in the MySQL project. `tests/nextorm.mariadb.tests` stays in whole-solution regression but gets NO empty D211 selector; the obligation transfer is reflected in scope, ledger and manifest.
| Mandatory selector/purpose | Real project and files |
| `Feature=D211`, core | `tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs` (types `EagerLoadPerParentPagingTests`, `EagerLoadPerParentPagingJoinIntoTests`, `EagerLoadPerParentPagingImplicitNavigationTests`) |
| `Feature=D211`, SQLite | `EagerLoadPerParentPagingTests.cs`, `EagerLoadPerParentPagingEdgeTests.cs`, `EagerLoadingSqlGenerationTests.cs` |
| `Feature=D211`, PostgreSQL | `tests/nextorm.postgres.tests/EagerLoadPerParentPagingTests.cs` |
| `Feature=D211`, SQL Server | `tests/nextorm.sqlserver.tests/EagerLoadPerParentPagingTests.cs` |
| `Feature=D211`, MySQL aggregate | `tests/nextorm.mysql.tests/EagerLoadPerParentPagingTests.cs` + `MariaDbEagerLoadPerParentPagingTests.cs` |
| `Feature=D211&FullyQualifiedName!~MariaDbEagerLoadPerParentPagingTests`, MySQL SQL family | MySQL project, without the MariaDB class |
| `Feature=D211&FullyQualifiedName~MariaDbEagerLoadPerParentPagingTests`, MariaDB SQL family | `tests/nextorm.mysql.tests/MariaDbEagerLoadPerParentPagingTests.cs` |
| `Feature=D211`, ClickHouse | `tests/nextorm.clickhouse.tests/EagerLoadPerParentPagingTests.cs` |
| native `-trait Feature=D211`, integration | `CommonTestSuite.EagerLoadPerParentPaging.cs`, `ClickHouseEagerPerParentPagingTests.cs`, `MariaDbEagerPagingTests.cs` |
| whole-solution, unfiltered boundary | all solution tests incl. `nextorm.mariadb.tests` |
For SQL EV-04 record six separate family results: SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse; an aggregate MySQL run does not replace the separate MariaDB result. Counts r2 are baseline, not a pre-set r3 result; each new count comes from actual runner output. Exit 8 / selected_count 0 = FAIL.

## 8. Commands and capture contract
All calls go through the wrapper. `cap() { python3 "$A/capture.py" --id "$1" --timeout "$2" -- "${@:3}"; }`. Receipt holds real child exit/signal/timeout, argv, cwd, env, timestamps, source/artifact revision; stdout/stderr retained. Wrapper mirrors the real result: ordinary mandatory command exit 0; self-tests 0 and 7; launch failure 253, timeout 252, signal 128+signal — not success. Logs `A/logs/<ID>.log`; receipts `A/receipts/<ID>.json`.
Leaf catalogue: SELFTEST_OK_RV3 (exit 0), SELFTEST_FAIL_RV3 (exit 7); T-CORE/T-SQLITE/T-PG/T-SS/T-MYSQL/T-CH = `dotnet test tests/nextorm.<p>.tests -c Debug --filter Feature=D211`; SQL-MYSQL = MySQL project `--filter 'Feature=D211&FullyQualifiedName!~MariaDbEagerLoadPerParentPagingTests'`; SQL-MARIADB = MySQL project `--filter 'Feature=D211&FullyQualifiedName~MariaDbEagerLoadPerParentPagingTests'`; INTEGRATION-D211 = `env DOCKER_HOST=$H dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait Feature=D211 -noColor`; BUILD-DEBUG/BUILD-RELEASE = `dotnet build -c Debug|Release`; REGRESSION-SOLUTION = `env DOCKER_HOST=$H dotnet test -c Debug --no-build --verbosity normal`; COVERAGE-COLLECT = `env DOCKER_HOST=$H dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "python3 ${A}/capture.py --id coverage-tests --timeout 1800 -- dotnet test --no-build --verbosity normal"`; COVERAGE-REPORT = reportgenerator; COVERAGE-DELTA = `python3 $A/coverage/changed_branch_delta.py`; MUTATION-CANONICAL = `env -u DOCKER_HOST dotnet tool run dotnet-stryker -- --config-file $A/mutation/stryker-config.json --output $A/mutation/run-canonical --concurrency 4 --skip-version-check --verbosity info`; MUTATION-RESTART same with `stryker-config.final3.json`, output `run-restart`, concurrency 1; PERF-ACCEPTANCE = `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; PERF-EAGER same + `--filter '*EagerPerParentPagingBenchmark*' --job short`; DOCFX = `dotnet docfx docs/docfx.json`; VALIDATE-BRIEF/REPORT/MANIFEST = `python3 scripts/validate_inner_loop.py {brief,report,manifest} $A/<f>.json`. Probe: for each s in baseline oracle core sql integration api-cache guards regression build coverage mutation docs perf freeze capture-audit history → `cap "PROBE-${s}" 900 python3 "$A/probe.py" "$s"`; probe exit 0 required in addition to substantively checked leaf evidence; a probe never turns a failed/zero-test leaf green.
Inner-loop/acceptance boundaries: `brief.json`/`report.json` cover the correction unit; at most one solution build (BUILD-DEBUG) and one unfiltered boundary sweep (REGRESSION-SOLUTION). BUILD-RELEASE, coverage collector, mutation, docs and perf are a separate mandatory acceptance campaign, not extra hidden inner boundary executions; their receipts are fully in the contract/inventory but referenced in `report.json` as acceptance evidence, not recorded as a permitted second build/sweep. Each `executions` entry has real argv, phase, source/artifact revision, exit and observed selected_count; do not substitute r2 counts. The report passes the validator before CHECK.

## 9. Versioned evidence contract — rv=3
rv3 supersedes rv2 only on the confirmed invocation-map/source mapping: MariaDB SQL/guard/targeted evidence from the existing MySQL/MariaDB and integration files; seven-targeted-project map explicitly includes integration; the empty MariaDB-project invocation is not repeated; the nested coverage capture is aligned with the mandatory wrapper CLI. All 17 row IDs and all REQ obligations preserved. Independent oracle, params parity, mutation accounting and inventory are execution of existing obligations, not weakening/substitution. Every row: rv=3; applicability predicate `Always`; the sources below are PLANNED rv3 sources; logs and receipts of all listed commands required; default exit 0; test leaves selected_count ≥1; test failure, provider skip in mandatory integration, launch failure and timeout are not `met`; evidence bound to the current source content fingerprint and artifact revision; before DO all rows active, `met` only after substantive confirmation. `P(s)` = the exact `PROBE-s` call.
- EV-01/REQ-11 identity, fixtures, support, fingerprints; `P(baseline)`; baseline log/receipt, `baseline/` JSON/snapshot, source identity, support/fixture inventory, invocation map, MariaDbContainer reuse confirmation; DO-baseline.
- EV-02/REQ-01 independent reference-v1 + adversarial global limit; `T-CORE`, `T-SQLITE`, `P(oracle)`; test receipts/results, `oracle/` reference-v1 inputs, independently computed expected, actual and comparison results (one trait rerun insufficient); DO-core.
- EV-03/REQ-01,03 paging/order/filter/key/default/overflow matrix; `T-CORE`, relevant `T-SQLITE`, `P(core)`; `core/` scenario/variant matrix and observed results; negative/guard evidence; logs/receipts; DO-core.
- EV-04/REQ-02,06 both-mode SQL partition/bounds/aliases/projection for six families; `T-SQLITE`, `T-PG`, `T-SS`, `SQL-MYSQL`, `SQL-MARIADB`, `T-CH`, `P(sql)`; `sql/` separate family/mode results, SQL/expectation evidence, receipts; six results mandatory; DO-sql.
- EV-05/REQ-01,02,06 real six-provider execution, both modes; `INTEGRATION-D211`, `P(integration)`; `integration/` provider/mode inventory, runner results, logs/receipts; mandatory provider cases really executed; DO-integration.
- EV-06/REQ-04,05,07 surfaces, args/params parity, cancellation/ownership, rebind/interleaving/API unchanged; `T-CORE`, needed `T-SQLITE`, `REGRESSION-SOLUTION`, `P(api-cache)`; `api-cache/` surface/parameter/rebind evidence, V17 result, ownership/cancellation results, API audit, test receipts; DO-execution.
- EV-07/REQ-03,06,07 shape/capability/version guards + baseline-selected in-memory behavior; all seven targeted project invocations, `SQL-MARIADB`, `P(guards)`; `guards/` input→guard→observed-result mapping, provider/version results, in-memory disposition; logs/receipts; DO-guards.
- EV-08/REQ-07,09 seven-project targeted + whole-solution regression; `T-CORE`, `T-SQLITE`, `T-PG`, `T-SS`, `T-MYSQL`, `T-CH`, `INTEGRATION-D211`, `REGRESSION-SOLUTION`, `P(regression)`; `regression/` targeted/solution results and counts, explicit mapping; mandatory provider evidence not skipped; DO-regression.
- EV-09/REQ-09 warning-free Debug/Release + CRLF; `BUILD-DEBUG`, `BUILD-RELEASE`, `P(build)`; `build/` summaries, 0W/0E and CRLF audit; both leaf receipts/logs; DO-build.
- EV-10/REQ-09 aggregate thresholds + feasible changed-P1 branches; `COVERAGE-COLLECT`, nested `coverage-tests`, `COVERAGE-REPORT`, `COVERAGE-DELTA`, `P(coverage)`; Cobertura/report snapshot in `coverage/`, threshold result, per-P1 changed branch delta/dispositions; all four receipts; DO-coverage.
- EV-11/REQ-09 complete scoped mutation accounting/dispositions; `MUTATION-CANONICAL`, `MUTATION-RESTART`, `P(mutation)`; configs, raw reports/logs/receipts, `mutation/` reconciliation with the full canonical span set, Safe-Mode and restart mapping; DO-mutation.
- EV-12/REQ-08 existing bilingual content/link checks, correct XML, DocFX; `P(docs)`, `DOCFX`; `docs/` XML diff/audit, bilingual/link results, DocFX result, receipts; DO-docs.
- EV-13/REQ-10 seven acceptance cases + dedicated eager measurement; `PERF-ACCEPTANCE`, `PERF-EAGER`, `P(perf)`; `perf/` baseline identity, seven case results, raw benchmark exports, dedicated eager results and comparisons; full artifact set; DO-perf.
- EV-14/REQ-11 current tree, row completeness, references; `P(freeze)`, `VALIDATE-MANIFEST`; `A/manifest.json`, `freeze/` ledger, validation evidence, tree/status snapshots and reconciliation; 17 unique rows, exact artifact references; DO-evidence.
- RC-01/REQ-09,11 fresh self-tests + complete mandatory-command inventory; `SELFTEST_OK_RV3`, `SELFTEST_FAIL_RV3`, `P(capture-audit)`; `capture-audit/receipts-index.json`, self-test receipts; inventory of all mandatory commands incl. nested coverage and validators; DO-evidence.
- RC-02/REQ-11 historical byte preservation/effective correction; `P(history)`; `history/{manifest,ledger}.json`, hashes/provenance, `effective-rv1-index.json`; rv1/rv2 bytes unchanged; DO-evidence.
- RC-03/REQ-11 valid brief before edits, valid report before CHECK; `VALIDATE-BRIEF`, `VALIDATE-REPORT`; `brief.json`, `report.json`, validation logs/receipts and freshness/scope evidence; DO-evidence.
Freeze/inventory: DO-evidence finalises the inventory AFTER the mandatory commands incl. validators and capture-audit; finalising the index as part of the capture helper post-receipt is allowed (post-processing, not child-exit substitution); the index includes its own completed receipt and validator receipts; self-tests cover this path. No cyclic hashes: hash immutable leaf evidence; aggregate index/ledger/manifest carry their own version; the aggregate need not hash itself; final source/status snapshot recorded separately; after freeze, source edits invalidate applicability and require new evidence, not editing an old receipt.

## 10. Variant, branch and mutation disposition
V01–V16 preserved each under its original ID and obligations; do not rename/merge into a single "covered". Baseline/probe carries each original row into the rv3 variant inventory; the report shows a separate observed status for each V01–V17.
Closure matrix: V17 args/`.ToList(@params)` = test (same dataset, both forms, changed parameter values, re-execution; EV-06). R3-V01 parent/child input distribution = test (multiple parents, skew, empty/non-empty children, adversarial global limit; EV-02/03). R3-V02 null/default, key/value/reference representations = test for supported forms, guard with observed error for unsupported; EV-03/07. R3-V03 filter/order/Skip/Take/default/overflow = test each original variant and boundary, guard for invalid forms; EV-03. R3-V04 split/single flags = test both supported modes, guard for unsupported combinations; EV-04/05/07. R3-V05 providers/version/capability flags = test six families, version/capability guards guard+observed test; EV-04/05/07. R3-V06 ties = test the accepted ordering contract (no new deterministic tie-breaker). R3-V07 cancellation/ownership/rebind/interleaving = test incl. context/command reuse; EV-06. R3-V08 baseline-selected in-memory behavior = test/guard per baseline, no support expansion; EV-07. Test helper/future test symbols are not declared here; their real names are fixed after implementation.
Audit-candidate reconnaissance (R3-A01..A09, facts before choosing a fix): A01 `SqlBuilder.cs:53` WITH TIES; A02 `:641` child CTE; A03 `:669` hints; A04 `:760` null sort ColumnIndex; A05 `:1232` cancellation; A06 comparer `:160–170,465–475`; A07 `EntityBuilder.cs:1653` ordinal ordering; A08 extensions `:505` child-factory side effect; A09 eager loading `:557` message. Each gets an ID and a disposition test / guard+test / deferred with a specific trigger; deferred only outside the mandatory acceptance path; a mandatory reachable defect is not deferred. A real production defect is returned to PLAN; do not add a speculative fix. Retain accepted dispositions: `SqlBuilder.cs:705` ties accepted; `:800` not live for the entity-shaped path; F4 defensive/parameter-mirror branches guard-backed.
P1 branch/mutation: P1 by construction — oracle independence and per-parent correctness; V17 and REQ-04/06; mandatory provider/mode paths; shared-command/cache and cancellation/ownership invariants; applicable production execution paths; completeness of mandatory mutation and receipt evidence. Apply any higher repository priority; CHECK does not downgrade. Aggregate coverage does not replace feasible P1 branch closure. Mutation: canonical accounting starts from ALL 50 raw spans, not prose 49; include preparer/comparer, `EntityBuilder.cs`, whole-method Safe-Mode removals in `CreatePairCommand` and `AppendEmbeddedChildOrdering`; every canonical span linked to raw evidence and a disposition; restart target set linked to canonical, not a reduced denominator; Safe-Mode removal is not automatically killed; equivalence/unreachability proven by facts; new oracle/params tests participate in the actual test selection; both real exits retained; a failed run is not declared successful because JSON exists.

## 11. Documentation
Change: XML `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:70–72`; status plan, invocation map, evidence/variant disposition; needed evidence helper material. Do not change without separate proof: public guides/API; generated `docs/api`, `docs/_site`; public links into internal specs. Existing bilingual content/link checks and DocFX remain mandatory EV-12. The XML must describe the real split/single per-parent top-N support, not promise unsupported forms.

## 12. Performance and reconnaissance
Perf measurement required, EV-13 unconditional: BenchmarkDotNet acceptance all seven cases; dedicated eager benchmark; original baseline with confirmed environment/content identity; original threshold regression <20%; retained r2 results +8.7%/+4.3%, wall 45 s are baseline/context, not a pre-known r3 result. No separate new benchmark for the XML comment (it is a comment, not per-row work); the mandatory campaign is not cancelled. Reconnaissance required (R3-A01–A09); completion criterion: Roslyn-resolved path/guard facts and an observed result, or a proof of unreachability; repeating a raw reviewer candidate is insufficient.

## 13. CHECK re-gather — finite budget
Owner `CHECK-evidence`. Budget: two dispatches total, each ≤15 min; prior dispatches deducted; a revision change does not refresh the budget. Each dispatch: one existing row, one reproducible invocation, no source edits, append-only. Dispatch 1 EV-11 `cap CHECK-EV11-REGATHER 900 python3 $A/probe.py mutation` (independent full canonical accounting over the existing raw runs, incl. Safe-Mode). Dispatch 2 RC-01 `cap CHECK-RC01-REGATHER 900 python3 $A/probe.py capture-audit` (independent receipt-inventory completeness). EV-13/EV-14 ledger reconciliation and validation receipts are prepared by D:R3-08 BEFORE CHECK; they are not hidden in the RC-01 dispatch (no third re-gather). An incomplete report first requires the prescribed targeted re-gather, not a new DO iteration. On budget exhaustion with persistent low confidence, recommend `escalate` (trigger 5).

## 14. Loop-back classification and risks
CHECK → PLAN classification: oracle/params/XML = demonstrated correction tasks; selector map = a real plan change; mutation/inventory/freeze = evidence correction, not demonstrated product defects; audit candidates = fact gap, scout first; no proven external blocker now. Risks: the oracle may accidentally repeat the product algorithm (mitigated by independent inputs/expected + adversarial test); tied fixtures may impose a new contract (use total order or permitted-result comparison); a compound filter may yield zero tests (exit/count checked); source edits after measurements invalidate freshness; validator boundary rules may be broken by mixing the acceptance campaign with the inner loop (separation fixed in §8); mutation reconciliation may lose Safe-Mode spans (raw canonical denominator mandatory); container provisioning (skill recovery, no skips); the last validator/audit receipt may be missing from inventory (post-receipt finalisation mandatory). Confidence: high on the necessity of the oracle/V17/XML/map corrections and the existing MariaDB test placement; audit-candidate reachability is not established (explicitly bounded reconnaissance, not a product-correctness assumption).

## 15. Final DO brief JSON
Create `A/brief.json` BEFORE test/source edits, validate with VALIDATE-BRIEF:
{"unit":"D211-r3-correction","scope":{"projects":["src/nextorm.core","tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.postgres.tests","tests/nextorm.sqlserver.tests","tests/nextorm.mysql.tests","tests/nextorm.clickhouse.tests","tests/nextorm.integration.tests"],"selectors":["Feature=D211","Feature=D211&FullyQualifiedName!~MariaDbEagerLoadPerParentPagingTests","Feature=D211&FullyQualifiedName~MariaDbEagerLoadPerParentPagingTests"],"files":["src/nextorm.core/Builders/EntityBuilderEagerLoading.cs","tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.sqlite.tests/EagerLoadPerParentPagingEdgeTests.cs","tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs","tests/nextorm.postgres.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.sqlserver.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.mysql.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.mysql.tests/MariaDbEagerLoadPerParentPagingTests.cs","tests/nextorm.clickhouse.tests/EagerLoadPerParentPagingTests.cs","tests/nextorm.integration.tests/CommonTestSuite.EagerLoadPerParentPaging.cs","tests/nextorm.integration.tests/ClickHouseEagerPerParentPagingTests.cs","tests/nextorm.integration.tests/MariaDbEagerPagingTests.cs","docs/specs/status/D211-eager-perparent-topn-1.md","artifacts/pdca/D211-eager-perparent-topn-1/rv3/capture.py","artifacts/pdca/D211-eager-perparent-topn-1/rv3/probe.py","artifacts/pdca/D211-eager-perparent-topn-1/rv3/coverage/changed_branch_delta.py","artifacts/pdca/D211-eager-perparent-topn-1/rv3/mutation/stryker-config.json","artifacts/pdca/D211-eager-perparent-topn-1/rv3/mutation/stryker-config.final3.json"],"rationale":"Implement the missing independent oracle and params parity, correct the XML and the mandatory invocation map. MariaDB D211 tests execute in nextorm.mysql.tests and nextorm.integration.tests; no empty Feature=D211 invocation is assigned to nextorm.mariadb.tests. Preserve all six provider families, both modes and all original acceptance obligations.","rebuild":"affected","boundary":"Correction-unit report permits at most one solution Debug build and one unfiltered Debug whole-solution regression sweep. The whole-solution sweep includes nextorm.mariadb.tests. Release build, coverage, mutation, DocFX and benchmarks are separately captured mandatory acceptance evidence, not additional inner-loop boundary executions."}}

## 16. Gate 1 and handoff
PLAN r3/rv3 is gate-1-complete and ready for `coder` to persist. Fixed: goal + acceptance with negatives; minimal solution + alternatives; D tasks, owners, deps and preserved remainders; invocation map and a validatable brief scope; design/test/variant/P1/branch/mutation strategy; full 17-row evidence contract with mandatory slots; docs, perf, recon and unit mode; risks, uncertainty and finite CHECK re-gather budget; explicit rv2→rv3 supersession without ID/obligation loss. Before source/test corrections: coder writes the plan into the status file, creates and successfully validates the brief, fixes the rv3 command/evidence scaffolding and satisfies applicable approval requirements. DO does not declare the cycle successful; before independent CHECK a valid report, full inventory and honest manifest are mandatory; final ACCEPT only from CHECK.

### Durable state (r=3)

- Current cycle N=1; Plan revision r=3; Attempt n=1/3.
- Defect history:
  - `D211-RV2-INVOCATION-MAP-EMPTY-RUN` — observed at CHECK(r2, n=1); evidence
    `artifacts/pdca/D211-eager-perparent-topn-1/rv2/check-gather/SUMMARY.md`;
    applied fixes: 0 (before r=3); outcome: replanned to r=3 (invocation/source
    mapping correction; IDs/obligations preserved).

## Progress log (r=3)

2026-10-12T06:52:05Z | PLAN | r3 | n1/3 | Replanned: CHECK(r2,n1) FAIL — fix mandatory invocation map (MariaDB variants live in nextorm.mysql.tests/integration; no empty Feature=D211 run), independent oracle, params parity, full mutation accounting, stale XML; rv3 supersedes rv2 only on invocation/source mapping, IDs/obligations preserved | artifacts/pdca/D211-eager-perparent-topn-1/rv3/{brief.json,probe.py,capture.py,mutation/stryker-config.json,mutation/stryker-config.final3.json}
2026-10-12T06:52:05Z | DO | r3 | n1/3 | DO started (r3, n=1/3) — rv3 scaffolding (capture/probe/coverage/mutation/history), brief validated exit 0, real-exit self-tests 0/7, fresh Debug/Release builds 0W/0E exit 0, baseline identity/tree/toolchain/support written; BLOCKER: r=3 plan body not supplied (oracle/params/XML edits pending) | artifacts/pdca/D211-eager-perparent-topn-1/rv3/{brief.json,receipts/VALIDATE-BRIEF.json,receipts/SELFTEST_OK_RV3.json,receipts/SELFTEST_FAIL_RV3.json,receipts/BUILD-DEBUG.json,receipts/BUILD-RELEASE.json,baseline/}
2026-10-12T07:07:07Z | DO | r3 | n1/3 | D:R3-02 closed (independent oracle, EV-02) — EagerLoadPerParentPagingOracleTests added to tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs; expected computed in-process from raw ADO-read child rows (filter→order→Skip→Take, never the ORM/SQL/window path) and compared to both eager modes for take2 / skip1_take1 / filtered_take2; adversarial global-limit case proves a naive limit differs; exports artifacts/pdca/D211-eager-perparent-topn-1/rv3/oracle/reference-v1.json (all_match=true, adversarial_case_differs=true); `probe.py oracle` checks the data (PROBE-oracle exit 0) | artifacts/pdca/D211-eager-perparent-topn-1/rv3/oracle/reference-v1.json; artifacts/pdca/D211-eager-perparent-topn-1/rv3/oracle/summary.json; artifacts/pdca/D211-eager-perparent-topn-1/rv3/receipts/PROBE-oracle.json
2026-10-12T07:07:07Z | DO | r3 | n1/3 | D:R3-03 closed (params parity, V17/EV-06) — Split_TopLevelParams_ArgumentForm_And_ParamsForm_Agree + Split_TopLevelParams_ChangedValue_RebindsAndReexecutes added: the compile-time argument form equals the `SqlFunctions.Parameter`/`.ToList(@params)` form equals the prepared `ToCommand().ToList(params)` surface on the same dataset; churned values (1→3→1) rebind and re-execute with unchanged per-parent paging | tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs
2026-10-12T07:07:07Z | DO | r3 | n1/3 | D:R3-05 closed (XML, EV-12) — src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:70–72 no longer claims per-parent top-N is rejected; now describes the split/single row_number-partition lowering; comment/XML-doc only, behavior unchanged | src/nextorm.core/Builders/EntityBuilderEagerLoading.cs
2026-10-12T07:07:07Z | DO | r3 | n1/3 | DO r3 leaves re-run after edits (all Feature=D211, one project at a time, real exit 0; selected counts from runner output): T-CORE 9, T-SQLITE 61, T-PG 7, T-SS 7, T-MYSQL 11, SQL-MYSQL 7, SQL-MARIADB 4, T-CH 7; BUILD-DEBUG 0W/0E exit 0, BUILD-RELEASE 0W/0E exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv3/receipts/; artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/
2026-10-12T07:59:30Z | DO | r3 | n1/3 | D:R3-04 accepted (invocation map) — INTEGRATION-D211 `env DOCKER_HOST=$H dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait Feature=D211 -noColor` real exit 0, Total 64 / Failed 0 / Skipped 0 (5 containers reused, 0 provider skips); REGRESSION-SOLUTION `env DOCKER_HOST=$H dotnet test -c Debug --no-build --verbosity normal` real exit 0, 10853 total / 0 failed / 10655 succeeded / 198 skipped; PROBE-sql/guards/regression/api-cache/integration exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/INTEGRATION-D211.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/REGRESSION-SOLUTION.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/receipts/
2026-10-12T07:59:30Z | DO | r3 | n1/3 | D:R3-07 coverage closed (EV-10) — COVERAGE-COLLECT exit 0 (nested coverage-tests 10853/0/198), COVERAGE-REPORT exit 0, COVERAGE-DELTA exit 0; aggregate line 88.5% (>=85) / branch 80.6% (>=75); changed delta 254/279 lines, 160/194 branch conditions; PROBE-coverage exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/COVERAGE-*.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/coverage/branch-delta.json; tests/coverage/report/Summary.txt
2026-10-12T07:59:30Z | DO | r3 | n1/3 | D:R3-07 perf closed (EV-13) — PERF-ACCEPTANCE exit 0, exactly 7/7 cases, wall 57.4 s (<=240); PERF-EAGER exit 0; cached/prepared time ratio 2.08 vs baseline 1.87 (+11.2% <20%), alloc ratio 7.74 vs 7.42 (+4.3%); means Nextorm_Count 2.476 ms, GroupByCount 64.81 ms, Cached 2.149 ms, Cached_ToList 1,883.4 us, Prepared_ToList 904.3 us, Cached_PlanOnly_Param 587.7 us, Cached_ToListAsync 1.998 ms; PROBE-perf exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/PERF-ACCEPTANCE.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/PERF-EAGER.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/receipts/
2026-10-12T07:59:30Z | DO | r3 | n1/3 | D:R3-07 docs closed (EV-12 partial) — DOCS-DOCFX `dotnet docfx docs/docfx.json` real exit 0, 0 error(s), 2 pre-existing warnings (duplicate AnalyzerReleases.*.md source); PROBE-docs exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/DOCS-DOCFX.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/receipts/PROBE-docs.json
2026-10-12T07:59:30Z | DO | r3 | n1/3 | D:R3-07 build closed (EV-09) — BUILD-DEBUG and BUILD-RELEASE 0W/0E exit 0; post-mutation REBUILD-AFTER-MUTATION `dotnet build nextorm.slnx -c Debug -t:Rebuild` exit 0 (0W/0E); PROBE-build exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/BUILD-*.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/REBUILD-AFTER-MUTATION.log
2026-10-12T07:59:30Z | DO | r3 | n1/3 | D:R3-07 MUTATION BLOCKED (EV-11) — MUTATION-CANONICAL hit the documented 1800 s bound (wrapper exit 252, timed_out=true, duration 1800.381 s): Stryker's per-test coverage capture alone took ~27 min (10822 tests) and only 214 mutants had begun; `run-canonical/` has no report; MUTATION-RESTART not run (shares the same coverage phase); `reconciliation.json` not built. STOPPED per the fail-leaf rule; not papered over | artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/MUTATION-CANONICAL.log; artifacts/pdca/D211-eager-perparent-topn-1/rv3/receipts/MUTATION-CANONICAL.json
2026-10-12T07:59:30Z | DO | r3 | n1/3 | D:R3-04/07 probe leaves run (all real exit 0): PROBE-baseline, -history, -capture-audit, -oracle, -core, -sql, -guards, -integration, -api-cache, -build, -docs, -regression, -perf, -coverage; PROBE-mutation not run (canonical timed out); PROBE-freeze deferred to D:R3-08 (no manifest yet) | artifacts/pdca/D211-eager-perparent-topn-1/rv3/receipts/PROBE-*.json; artifacts/pdca/D211-eager-perparent-topn-1/rv3/logs/PROBE-*.log
2026-10-12T07:59:30Z | DO | r3 | n1/3 | CAVEATS: (a) D:R3-05 XML fix adds one changed hunk vs base b527be6f, so the regenerated rv3 mutate scope is 51 raw spans (plan prose 50) — Mutation accounting/`reconciliation.json` must use the actual 51 and note the comment-only extra span; (b) `rv3/coverage/changed_branch_delta.py` initially carried the rv2 hardcoded ART path and, when first run, overwrote `rv2/coverage/branch-delta.json`; restored to the rv2 counter values (254/279, 160/194) from rv2's coverage.xml but not byte-identical (16163 B vs the original 17063 B serialization) — rv1/** and all other rv2/** bytes untouched | artifacts/pdca/D211-eager-perparent-topn-1/rv3/mutation/mutate-spans.json; artifacts/pdca/D211-eager-perparent-topn-1/rv2/coverage/branch-delta.json

## PLAN r=4 (rv=4) — A01 guard + A07/A08 + mutation budget

# PLAN r=4 / rv=4 — T211 / #211
Decision: adopt r4/rv4 — add A01 production guard, resolve A07/A08 via tests, repair evidence-output isolation, increase mutation budget without reducing obligations.
## §1 Goal & constraints
Complete #211 against the final implementation: correct supported per-parent paging semantics; preparation-time rejection of unsupported child shapes; no client fallback; no SQL execution or plan-cache insertion for rejected shapes; complete authoritative evidence for every mandatory row; preserved legacy requirements/priorities/variants/mutation obligations. `ISqlDialect` read-only. Do not mutate shared `QueryCommand` cache state. Do not implement per-parent WITH TIES rendering. Sequential, one tree, no worktrees/commits/push/merge.
## §2 Revision/preservation/provenance
rv4 supersedes rv3 as the applicable contract. Preserve REQ-01..11, EV-01..14, RC-01..03 (associations, positive/negative scenarios, applicability predicates, priorities, commands/evidence) except documented amendments. Add REQ-211-R4-A01/A07/A08/SCOPE with EV-R4-A01/A07/A08/SCOPE. D:06 stays active (EV-11 remainder). D:08 stays active (accepted handoff). D:04/D:04a and D:R3-02/03/05 closures preserved. rv3 results are historical, not rv4 certification. r4 DO starts n=1. Provenance incident (record): `rv2/coverage/branch-delta.json` overwritten by a stale output path; counters restored but NOT byte-identical (16163 B vs 17063 B); gitignored; no byte-restoration claim; do not depend on it for rv4.
## §3 Acceptance
Existing REQ-01..11 criteria/negatives reproduced unchanged in the persisted r4 section (incl. REQ-03 neg + rule 12: unsupported ranking/derived/ordering/child shape fails during preparation with NotSupportedException, before SQL and plan-cache insertion, no client fallback).
New: REQ-211-R4-A01 — public per-parent paged eager child with `WithTies` throws NotSupportedException during preparation before SQL/cache; negative: supported paging without WithTies still succeeds and a warm cache cannot bypass or contaminate. REQ-211-R4-A07 — supported ordinal child ordering demonstrably retained on embedded/SingleQuery per-parent paging (effective ordering + independent expected results); negative: incidental DB order cannot make a test pass; rejecting a supported ordinal is NOT acceptable. REQ-211-R4-A08 — tests establish documented child-factory invocation/memoization incl. validation and non-list terminals; docs match; negative: docs must not claim non-list terminals never invoke the factory or that validation is side-effect-free in that sense. REQ-211-R4-SCOPE — final test/mutation scope corresponds to the final tree; captures/manifests identify commands, real exits, timeouts, artifacts; output stays in rv4-owned locations; negative: stale spans, missing reports, receipt/process disagreement, unaccounted applicable span, or writes into historical evidence are not success. EV-11 preserves all existing mutation criteria plus authoritative completion, full span accounting, required restart reconciliation.
## §4 Minimal solution
Selected: common preparation-time A01 guard + A07/A08 tests + accurate docs + isolated evidence outputs + two bounded full-scope mutation runs. Alternatives: reject per-parent WithTies (selected) vs implement ties (defer to separate feature) vs leave as limitation (reject); retain mutation scope + increase budget (selected) vs reduce scope (reject) vs phase-split (not selected). A01 is inside #211's acceptance path.
## §5 Production & audit dispositions
A01 fix now: add `HasWithTies` capability rejection in `ValidatePerParentPagingChild` (EntityBuilderEagerLoading.cs:576–593) after confirming per-parent applicability, before execution/cache; `NotSupportedException`, full-sentence trailing period, e.g. "LoadWith cannot apply Limit/Offset/Page to a child query that also uses WithTies." Message asserted. Footprint: common eager child validation + tests; no dialect/renderer/API/cache change. Closes EV-R4-A01 + adds evidence to existing REQ-03/fail-closed rows (keep IDs).
A07 test now (supported semantics cannot be rejected): anchors EntityBuilder.cs:1212,1420–1422,1653; EntityBuilderEagerLoading.cs:588; sqlite test :574-583. Retain the SQL-gen support test; add embedded/SingleQuery execution test with independent expected order; assert effective ordering; expression-ordering control + non-embedded path. If a production defect is proven: return to PLAN with the failing test/structural evidence; do NOT add an ordinal rejection or improvise a repair. EV-R4-A07 stays open otherwise.
A08 tests+docs: anchors EntityBuilderEagerLoading.cs:207–212,600; EntityBuilder.cs:1312,1335; EntityBuilderExtensions.cs:505. Test validation, non-list terminal invocation, repeated resolution for same (spec,context), distinct-context resolution; correct the docs; do not change semantics or invent concurrency/exception-caching guarantees.
Remaining audit dispositions: A02 CTE guard + public-path rejection test; A03 hints/settings guard + rejection tests; A04 null-sort preparation guard + valid-sort control; A05 cancellation not applicable to current MakeSelect (deferred register, trigger: cancellation enters this path/ownership changes); A06 guard-backed cold/warm preparation + loader-path tests; A09 test/doc disposition, no speculative wording change. Reuse sufficient existing tests.
## §6 Units/ownership/deps
Order D:R4-00 → 01 → 02 → 03 → 04 → 05 → D:06 → D:R4-06 → CHECK/D:08. D:R4-00 contract expansion/snapshots/brief/status persistence. D:R4-01 A01 guard+tests (EV-R4-A01). D:R4-02 A07 test+structural evidence (EV-R4-A07). D:R4-03 A08 tests/docs + remaining audit dispositions (EV-R4-A08). D:R4-04 output isolation/capture correctness/final span derivation/brief refresh (EV-R4-SCOPE); stop for confirmed A07 defect. D:R4-05 final-tree recertification (build/integration/regression/coverage/oracle/parity/perf/DocFX). D:06 existing EV-11 canonical/restart/reconciliation. D:R4-06 assemble manifest + candidate CHECK handoff. D:08 existing accepted handoff.
## §7 Execution mode & design checklist
Sequential one tree. Checklist: guard in common preparation not only renderer; capability → NotSupportedException, invalid-input conventions unchanged; rejection precedes SQL + cache insertion; no client fallback; ISqlDialect read-only; no sticky shared-command cache mutation; supported ordinal not reclassified unsupported; child-factory semantics documented not changed; provider defaults/invariants unchanged; CRLF preserved; warnings-as-errors; Roslyn for C# discovery/impact; historical evidence not overwritten; no commit/push/merge/worktree.
## §8 Test strategy & variant closure
Unit/SQL-gen for preparation guards/structure; mandatory container-backed integration for materialized results. Variants: WithTies+per-parent limit (+offset/page) = guard+rejection tests; equivalent paging without WithTies = positive execution; WithTies without limit + normal non-eager = existing semantics retained/tested; cold vs warm cache = tests; SingleQuery/embedded vs other strategies = tests; ordinal vs expression ordering = A07 support tests + structural assertions; existing asc/desc/multi/mixed = preserve/test; multiple parents/ties/empty/short = independent expected + original matrix; validation/list/non-list factory resolution = A08; same vs different context = A08; SQL providers = required integration, no skipped required provider; in-memory unsupported paging = existing guard retained/tested; null/default/value/reference/flags = copy existing matrix unchanged (retain each test/guard/deferred+trigger); cancellation absent = A05 deferred entry with trigger. No fabricated future symbols; record actual symbols via Roslyn after implementation. Coverage thresholds line ≥85 / branch ≥75 + existing changed-line/branch obligations.
## §9 Priority
P1: A01 guard + reachable rejection/bypass branches; supported A07 ordering propagation/materialized paths; preparation-before-execution/cache invariants; applicable #211 execution paths; existing project P1 lines; changed-P1 mutation accounting. CHECK cannot downgrade. Documentation-only spans accounted with an explicit reason; absence of mutants does not remove neighbouring executable P1 spans.
## §10 Evidence paths/capture/commands
A=`.../artifacts/pdca/D211-eager-perparent-topn-1/rv4` (create: contract/, logs/, receipts/, coverage/, mutation/, oracle/, perf/, docs/, brief.json, evidence-manifest.json). Preserve predecessor contents before modifying the status file; resolve relocated config paths to the same actual projects/logical targets. Every call records argv/cwd/env/tool versions, start/end/monotonic wall, real wrapper/child exit + timeout flag, stdout/stderr + artifact identities, final source/diff identity + contract revision. **Timeout takes precedence over a zero child exit from SIGTERM; wrapper must return nonzero on timeout; test the race with a short controlled process before expensive campaigns.** Calls: build `["dotnet","build","-c","Debug"]`; focused tests `["dotnet","test",project,"-c","Debug","--no-build","--filter",selector]` (require executed relevant tests, not just exit 0); integration `["dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--no-build","--","-trait","Feature=D211","-noColor"]` + DOCKER_HOST (+ integration skill recovery; required providers must execute); regression `["dotnet","test","-c","Debug","--no-build","--verbosity","normal"]` + DOCKER_HOST; coverage `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","tests/coverage/coverage.cobertura.xml","dotnet test -c Debug --no-build --verbosity normal"]` + reportgenerator + `python3 $A/coverage/changed_branch_delta.py` (rv4 copy must derive output from the rv4 location, not the stale rv2 literal); perf `["dotnet","run","--project","benchmarks/nextorm.benchmark","-c","Release","--","--anyCategories=acceptance"]`; eager same + `--filter *EagerPerParentPagingBenchmark* --job short`; docs `["dotnet","docfx","docs/docfx.json"]`.
## §11 EV-11 mutation
Canonical cwd `$A/mutation`, DOCKER_HOST unset: `["dotnet","tool","run","dotnet-stryker","--","--config-file","stryker-config.json","--output","run-canonical","--concurrency","4","--skip-version-check","--verbosity","info"]`, wall bound **5400 s**. Retain project nextorm.core, tests core.tests+sqlite.tests, perTest, mtp, Standard, additional-timeout 30000, thresholds {80,60,0}, exclusions/dispositions. Restart same with `stryker-config.final3.json`/`run-restart`/concurrency 1, bound **5400 s**, additional-timeout 120000, rebase the two logical targets onto final source. Correct historical rv3 scope = 51 raw spans / 10 files; derive rv4 spans from the final tree + original pinned base; include the A01 guard; do not hardcode 51; map spans→selectors/mutants/dispositions; explain non-executable/no-mutant spans; preserve changed-P1; reconcile restart targets by source/operation identity not numeric mutant IDs. Artifacts: fresh canonical/restart reports, receipts, phase timings, span map, reconciliation.json. Max campaign 10800 s. If either run times out, return timings/artifacts; no scope reduction/additional run/success claim.
## §12 Contract preservation & new rows
Freeze `$A/contract/rv4-contract.json`; manifest `$A/evidence-manifest.json`. Mandatory set exactly: EV-01..EV-14, RC-01..RC-03, EV-R4-A01, EV-R4-A07, EV-R4-A08, EV-R4-SCOPE. Rows `{id,status met|open|na,evidence[{kind,path,command?,exit?,value?}],reason?,predicate}`; `required_rows` == full set; **no PASS while any required row open**; `na` needs an evaluated predicate+reason. Legacy expansion (mandatory sealing step): for each EV-01..14 and RC-01..03 copy all mandatory slots from the predecessor (requirement+row ID, scenario, evidence kinds/sources, exact invocation + exit/log requirement, artifacts, owner, applicability predicate) and set rv=4; amendments only: EV-11 budget/scope/capture/rv4 outputs; affected campaigns final-tree recertification + rv4 outputs; scope/provenance references + incident. New row slots: EV-R4-A01 (paged child WithTies rejection + positive control + cold/warm; owner D:R4-01; predicate: child requests per-parent paging and WithTies); EV-R4-A07 (embedded ordinal ordering, independent expected + structural; owner D:R4-02; predicate: embedded per-parent child has ordinal sorting); EV-R4-A08 (factory invocation/memoization tests + accurate docs + DocFX; owner D:R4-03; predicate: validation/terminal resolves child factory); EV-R4-SCOPE (final inventory, complete spans, output isolation, timeout-race handling, provenance; owner D:R4-04; predicate: final tree/evidence generation changes scope/artifacts). Focused command params frozen to actual values before capture.
## §13 Final-tree recertification
Rerun: Debug build; mandatory D211 container integration; solution regression; coverage/report/delta; oracle + params parity against final impl (do not regenerate expected from production); perf acceptance + eager; DocFX; canonical/restart mutation + reconciliation. Perf baseline time 1.87 / alloc 7.42, 7/7, wall ≤240 s, <20% limits. Classify pre-existing skips; required providers cannot be represented as executed by a skipped suite.
## §14 Docs/perf/recon
Docs: update the inaccurate A08 invocation description; public unsupported-shape guidance where the A01 rejection requires it; EN/RU counterparts; status/scope/evidence records; no public links into docs/specs; no API rename/guide renumbering; record the 2 pre-existing DocFX warnings separately. Perf required (preparation change at EntityBuilderEagerLoading.cs:576–593; #211 has an obligation). Recon: no design spike for A01; A07's targeted test is the bounded behavioural investigation.
## §15 Brief generation & sealing
Snapshot `rv3/brief.json` → `$A/contract/brief-rv3-preserved.json`; create `$A/brief.json` using the existing schema; update revision/unit/source/output/test-scope fields for r4; preserve core+sqlite canonical mutation selection; record actual new test symbols after Roslyn discovery; include A07 supported obligation, A01 rejection/control, A08 invocation, retained legacy variants; replace the stale "50 spans" assertion with corrected historical + final derived rv4 scope; run the existing `validate_inner_loop.py brief $A/brief.json` (capture exit 0); freeze identity before execution; refresh/revalidate after final span derivation. Do not substitute an invented JSON object. Gate-1 sealing checklist: REQ-01..11 copied; every legacy row expanded; new rows instantiated with actual calls; IDs match the set; snapshots/paths pinned; valid brief + validator exit 0; deps/variants/priorities/checklist recorded; mutation/CHECK budgets recorded; no concealed supported-contract failure. After these pass: record "Gate-1 complete. PLAN r=4 / evidence contract rv=4 sealed. Ready for DO n=1. Autonomous execution continues without a user go."
## §16 CHECK budget, risks, return rules
CHECK re-gather: owner check; two targeted scout batches to locate/validate existing evidence; missing evidence → targeted lookup (not a production iteration/weaker contract); persistent low confidence → recommend escalate trigger 5. Risks: A07 supported-ordering defect → return to PLAN with failing evidence, no rejection workaround; mutation >5400 s → return authoritative timeout + phase timings, no scope reduction; relocated config wrong paths → resolve identities; span shift → derive after edits; timeout-receipt race → controlled test; provider skips → start containers/rerun; provenance compromised → preserve incident, fresh evidence, no byte-restoration claim; invalid brief/legacy expansion → do not seal, report as in-cycle prerequisite. Completion: D:R4-06 prepares the candidate package; coder does not issue final PASS; CHECK accepts only when every mandatory row is met or demonstrably na under its predicate and all original criteria remain satisfied.

### rv4 mandatory rows (expanded from the rv3 contract — IDs/obligations preserved, rv=4)

Rows below: `rv=4`; applicability `Always`; all 17 legacy IDs preserved with their rv3 mandatory slots copied verbatim (amendments only: EV-11 budget/scope/capture, final-tree recertification, rv4 output paths per §12). New rows: EV-R4-A01, EV-R4-A07, EV-R4-A08, EV-R4-SCOPE.

- EV-01/REQ-11 identity, fixtures, support, fingerprints; `P(baseline)`; baseline log/receipt, `baseline/` JSON/snapshot, source identity, support/fixture inventory, invocation map, MariaDbContainer reuse confirmation; DO-baseline.
- EV-02/REQ-01 independent reference-v1 + adversarial global limit; `T-CORE`, `T-SQLITE`, `P(oracle)`; test receipts/results, `oracle/` reference-v1 inputs, independently computed expected, actual and comparison results (one trait rerun insufficient); DO-core.
- EV-03/REQ-01,03 paging/order/filter/key/default/overflow matrix; `T-CORE`, relevant `T-SQLITE`, `P(core)`; `core/` scenario/variant matrix and observed results; negative/guard evidence; logs/receipts; DO-core.
- EV-04/REQ-02,06 both-mode SQL partition/bounds/aliases/projection for six families; `T-SQLITE`, `T-PG`, `T-SS`, `SQL-MYSQL`, `SQL-MARIADB`, `T-CH`, `P(sql)`; `sql/` separate family/mode results, SQL/expectation evidence, receipts; six results mandatory; DO-sql.
- EV-05/REQ-01,02,06 real six-provider execution, both modes; `INTEGRATION-D211`, `P(integration)`; `integration/` provider/mode inventory, runner results, logs/receipts; mandatory provider cases really executed; DO-integration.
- EV-06/REQ-04,05,07 surfaces, args/params parity, cancellation/ownership, rebind/interleaving/API unchanged; `T-CORE`, needed `T-SQLITE`, `REGRESSION-SOLUTION`, `P(api-cache)`; `api-cache/` surface/parameter/rebind evidence, V17 result, ownership/cancellation results, API audit, test receipts; DO-execution.
- EV-07/REQ-03,06,07 shape/capability/version guards + baseline-selected in-memory behavior; all seven targeted project invocations, `SQL-MARIADB`, `P(guards)`; `guards/` input→guard→observed-result mapping, provider/version results, in-memory disposition; logs/receipts; DO-guards.
- EV-08/REQ-07,09 seven-project targeted + whole-solution regression; `T-CORE`, `T-SQLITE`, `T-PG`, `T-SS`, `T-MYSQL`, `T-CH`, `INTEGRATION-D211`, `REGRESSION-SOLUTION`, `P(regression)`; `regression/` targeted/solution results and counts, explicit mapping; mandatory provider evidence not skipped; DO-regression.
- EV-09/REQ-09 warning-free Debug/Release + CRLF; `BUILD-DEBUG`, `BUILD-RELEASE`, `P(build)`; `build/` summaries, 0W/0E and CRLF audit; both leaf receipts/logs; DO-build.
- EV-10/REQ-09 aggregate thresholds + feasible changed-P1 branches; `COVERAGE-COLLECT`, nested `coverage-tests`, `COVERAGE-REPORT`, `COVERAGE-DELTA`, `P(coverage)`; Cobertura/report snapshot in `coverage/`, threshold result, per-P1 changed branch delta/dispositions; all four receipts; DO-coverage.
- EV-11/REQ-09 complete scoped mutation accounting/dispositions; `MUTATION-CANONICAL`, `MUTATION-RESTART`, `P(mutation)`; configs, raw reports/logs/receipts, `mutation/` reconciliation with the full canonical span set, Safe-Mode and restart mapping; DO-mutation.
- EV-12/REQ-08 existing bilingual content/link checks, correct XML, DocFX; `P(docs)`, `DOCFX`; `docs/` XML diff/audit, bilingual/link results, DocFX result, receipts; DO-docs.
- EV-13/REQ-10 seven acceptance cases + dedicated eager measurement; `PERF-ACCEPTANCE`, `PERF-EAGER`, `P(perf)`; `perf/` baseline identity, seven case results, raw benchmark exports, dedicated eager results and comparisons; full artifact set; DO-perf.
- EV-14/REQ-11 current tree, row completeness, references; `P(freeze)`, `VALIDATE-MANIFEST`; `A/manifest.json`, `freeze/` ledger, validation evidence, tree/status snapshots and reconciliation; 17 unique rows, exact artifact references; DO-evidence.
- RC-01/REQ-09,11 fresh self-tests + complete mandatory-command inventory; `SELFTEST_OK_RV3`, `SELFTEST_FAIL_RV3`, `P(capture-audit)`; `capture-audit/receipts-index.json`, self-test receipts; inventory of all mandatory commands incl. nested coverage and validators; DO-evidence.
- RC-02/REQ-11 historical byte preservation/effective correction; `P(history)`; `history/{manifest,ledger}.json`, hashes/provenance, `effective-rv1-index.json`; rv1/rv2 bytes unchanged; DO-evidence.
- RC-03/REQ-11 valid brief before edits, valid report before CHECK; `VALIDATE-BRIEF`, `VALIDATE-REPORT`; `brief.json`, `report.json`, validation logs/receipts and freshness/scope evidence; DO-evidence.

### Progress log (r=4)

2026-10-12T08:13:46Z | PLAN | r4 | n1/3 | Replanned: CHECK(r3) — A01 WithTies silent loss guard + A07/A08 + mutation budget 5400s | artifacts/pdca/D211-eager-perparent-topn-1/rv4/contract/; artifacts/pdca/D211-eager-perparent-topn-1/rv4/brief.json
2026-10-12T08:13:46Z | DO | r4 | n1/3 | D:R4-00 closed — Gate-1 complete. PLAN r=4 / evidence contract rv=4 sealed. Ready for DO n=1. Autonomous execution continues without a user go. rv3 contract snapshotted (brief-rv3-preserved.json, rv3-preserved.json); rv4 brief validated exit 0; header set r=4/rv=4/DO n=1 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/brief.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-BRIEF.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/contract/
2026-10-12T08:18:16Z | DO | r4 | n1/3 | D:R4-01 closed (A01 guard, EV-R4-A01) — `ValidatePerParentPagingChild` now rejects a per-parent paged child with `Paging.HasWithTies` before SQL/cache: `NotSupportedException("LoadWith cannot apply Limit/Offset/Page to a child query that also uses WithTies.")`; tests Split_WithTiesPagedChild_IsRejectedBeforeSql (limit-only + offset, no SQL emitted), SingleQuery_WithTiesPagedChild_IsRejectedBeforeSql, Split_WithTiesPagedChild_WarmCache_StillRejectedAndDoesNotContaminate (positive control + warm cache + no plan contamination); T-SQLITE 70/0 | src/nextorm.core/Builders/EntityBuilderEagerLoading.cs; tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/T-SQLITE.log
2026-10-12T08:18:16Z | DO | r4 | n1/3 | D:R4-02 closed (A07 supported ordinal ordering RETAINED, EV-R4-A07) — added CreateOrdinalDb (insertion order != id order) + Split_OrdinalOrderBy_RetainsEffectivePerParentOrder and SingleQuery_OrdinalOrderBy_RetainsEffectivePerParentOrder with independent expected id order [11,12]/[21,22] plus expression control OrderBy(x => x.Id); both modes returned the ordinal id order, so the supported contract is NOT lost — no defect, no rejection workaround, no production edit | tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/T-SQLITE.log
2026-10-12T08:18:16Z | DO | r4 | n1/3 | D:R4-03 closed (A08 tests+docs, EV-R4-A08) — tests NonListTerminal_InvokesChildFactoryForValidationWithoutLoadingChildren, SplitListTerminal_ResolvesChildFactoryOncePerExecution, SameBuilder_RepeatedTerminal_ReusesTheResolvedChild, DifferentContext_BuildsItsOwnChild; fixed the inaccurate 'side-effect free' doc in EntityBuilderEagerLoading.cs to state the child factory can run (memoized per (spec,context)) while writing no cache/QueryCommand.Cache; added WithTies unsupported-shape guidance EN+RU; no execution-semantics change | src/nextorm.core/Builders/EntityBuilderEagerLoading.cs; docs/advanced/eager-loading-per-parent-paging.md; docs/ru/advanced/eager-loading-per-parent-paging.md; tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs
2026-10-12T08:18:16Z | DO | r4 | n1/3 | D:R4-04 partial (EV-R4-SCOPE) — timeout-race validated: a controlled SIGTERM-ignoring process under --timeout 2 returned wrapper 252 (timed_out=true, SIGKILL signal 9); rv4 output isolation: rv4/coverage/changed_branch_delta.py derives output from the rv4 location (no rv2 literal); rv4 mutate scope derived from the final tree + pinned base b527be6f = 52 raw spans / 10 files (not hardcoded); final3 restart targets rebased to QueryPreparer{8730..8808} + QueryPlanEqualityComparer{18793..19091} | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/TIMEOUT-RACE.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/TIMEOUT-RACE.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/mutate-spans.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/stryker-config.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/stryker-config.final3.json
2026-10-12T08:18:16Z | DO | r4 | n1/3 | D:R4-01/02/03 leaves re-run after edits (all Feature=D211, serialised, real exit 0; selected_count from runner): T-CORE 9, T-SQLITE 70, T-PG 7, T-SS 7, T-MYSQL 11, SQL-MYSQL 7, SQL-MARIADB 4, T-CH 7; BUILD-DEBUG 0W/0E exit 0, BUILD-RELEASE 0W/0E exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-04 output isolation closed — redirected test evidence writers to rv4: oracle EagerLoadPerParentPagingOracleTests -> artifacts/pdca/D211-eager-perparent-topn-1/rv4/oracle/reference-v1.json and EagerLoadPerParentPagingEdgeTests security/W-2 probes -> artifacts/pdca/D211-eager-perparent-topn-1/rv4/check/*; verified rv1/**, rv2/**, rv3/** have NO file modified in this session (find -newermt). No historical evidence writes | tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs; tests/nextorm.sqlite.tests/EagerLoadPerParentPagingEdgeTests.cs; artifacts/pdca/D211-eager-perparent-topn-1/rv4/oracle/; artifacts/pdca/D211-eager-perparent-topn-1/rv4/check/
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 INTEGRATION-D211 exit 0 — `env DOCKER_HOST=$H dotnet run --project tests/nextorm.integration.tests -c Debug -- -trait Feature=D211 -noColor`: Total 64 / Errors 0 / Failed 0 / Skipped 0 / Not Run 0 (all mandatory providers executed) | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/INTEGRATION-D211.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/INTEGRATION-D211.json
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 REGRESSION-SOLUTION — first run real exit 2: total 10862 / failed 1 / succeeded 10663 / skipped 198; the single failure is D211-UNRELATED and FLAKY: ClickHouseExtremeRowNativeSpecificTests.FloatNativeParity_GroupedFloat32Widening_MinAndMax_AcrossDatasets (ClickHouseExtremeRowNativeSpecificTests.cs:644 f32 group parity). Classified flaky: a targeted recheck failed 1/1, but the identical full suite passed under COVERAGE-COLLECT (10862/0/198) and PROBE-regression (10862/0/198, exit 0). Skips are pre-existing SqlServer/Csv unsupported-provider skips. Final authoritative REGRESSION-SOLUTION is PROBE-regression exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/REGRESSION-SOLUTION.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/RECHECK-CH-FLOAT.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/regression/summary.json
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 coverage closed — COVERAGE-COLLECT exit 0 (nested coverage-tests 10862/0/198), COVERAGE-REPORT exit 0, COVERAGE-DELTA exit 0 (rv4-isolated output); aggregate line 88.5% (>=85) / branch 80.6% (>=75); rv4 changed delta 257/282 lines, 162/196 branch conditions; rv3 coverage delta untouched | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/COVERAGE-*.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/coverage/branch-delta.json; tests/coverage/report/Summary.txt
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 oracle/parity recertification closed — PROBE-oracle exit 0 against the FINAL implementation: rv4/oracle/reference-v1.json present, reference EagerPerParentPagingReference-v1, 3 scenarios, all_match=true, adversarial_case_differs=true; expected computed in-process from raw rows (not regenerated from production); params-parity/V17 (Split_TopLevelParams_*) executed in T-SQLITE 70/0 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/oracle/reference-v1.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/oracle/summary.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/PROBE-oracle.json
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 perf closed — PERF-ACCEPTANCE exit 0, 7/7 cases, wall 48.5 s (<=240); PERF-EAGER exit 0; cached/prepared time ratio 2.01 vs baseline 1.87 (+7.5% <20%), alloc ratio 7.74 vs 7.42 (+4.3%); Means Nextorm_Count 2.380 ms, GroupByCount 57.84 ms, Cached 1.978 ms, Cached_ToList 1,820.5 us, Prepared_ToList 905.8 us, Cached_PlanOnly_Param 575.2 us, Cached_ToListAsync 2.044 ms | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/PERF-ACCEPTANCE.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/PERF-EAGER.log
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 docs closed — DOCS-DOCFX `dotnet docfx docs/docfx.json` real exit 0, 0 error(s), 2 warnings = the SAME 2 pre-existing duplicate AnalyzerReleases.*.md warnings (no new warnings); PROBE-docs exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/DOCS-DOCFX.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/PROBE-docs.json
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 probes closed (all real exit 0): PROBE-baseline, -core, -sql, -guards, -integration, -api-cache, -build, -docs, -regression, -perf, -coverage | artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/PROBE-*.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/PROBE-*.log
2026-10-12T08:37:48Z | DO | r4 | n1/3 | D:R4-05 builds closed — fresh BUILD-DEBUG 0 Warning(s)/0 Error(s) exit 0 and BUILD-RELEASE 0 Warning(s)/0 Error(s) exit 0 (rv4 wrapper) | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/BUILD-DEBUG.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/BUILD-RELEASE.log
2026-10-12T09:18:41Z | DO | r4 | n1/3 | D:06 MUTATION-CANONICAL closed — real wrapper exit 0, duration 1187.537 s (< 5400 s bound), DOCKER_HOST unset, cwd artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation; phases: analyze 13:38:20 -> per-test coverage capture 13:41:04-13:44:37 (3953 tests) -> mutation 13:44:39 -> report+finish 13:58:07; active 217: Killed 154 / Survived 35 / NoCoverage 26 / Timeout 2; score 71.89%; report artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/run-canonical/reports/mutation-report.json | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/MUTATION-CANONICAL.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/MUTATION-CANONICAL.json
2026-10-12T09:18:41Z | DO | r4 | n1/3 | D:06 MUTATION-RESTART closed — real wrapper exit 0, duration 1000.886 s (< 5400 s), concurrency 1, final3 targets QueryPreparer{8730..8808} + QueryPlanEqualityComparer{18793..19091}; active 7: Killed 1 / Survived 6 / NoCoverage 0 / Timeout 0; score 14.29%; report artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/run-restart/reports/mutation-report.json | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/MUTATION-RESTART.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/MUTATION-RESTART.json
2026-10-12T09:18:41Z | DO | r4 | n1/3 | D:06 post-mutation — REBUILD-AFTER-MUTATION `dotnet build nextorm.slnx -c Debug -t:Rebuild` exit 0 (0W/0E); NO test failed with MissingMethodException, so no failure-driven re-run; a POSTMUT-SANITY-T-SQLITE Feature=D211 run exit 0 (70/0) confirms clean rebuilt binaries | artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/REBUILD-AFTER-MUTATION.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/POSTMUT-SANITY-T-SQLITE.log
2026-10-12T09:18:41Z | DO | r4 | n1/3 | D:06 reconciliation closed (EV-11) — artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/reconciliation.json from the FINAL derived rv4 scope: 52 raw spans / 10 files (derived, not hardcoded; 34 spans have no active mutants = comment/no-mutant spans, accounted); every canonical span mapped (all 217 active mutants come from the configured spans); 63 non-killed dispositioned by rv1 lineage (file+mutator+column, nearest line): Survived->equivalent 27, Survived->defensive 8, NoCoverage->defensive 26, Timeout->equivalent 2 (paired concurrency-1 restart establishes termination; plan-hash-composition-only per rv1 reason); unmapped 0, feasible_open 0; Safe-Mode/CompileError 8546 across 51 files incl. whole-method removals CreatePairCommand + AppendEmbeddedChildOrdering; restart targets reconciled by source/operation identity (not mutant ids); raw canonical denominator used, not reduced | artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/reconciliation.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/survivors.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/README.md
2026-10-12T09:18:41Z | DO | r4 | n1/3 | EV-11 evidence status = met — both fresh mutation reports present with real exit 0 (no timeout), full span accounting, all non-killed dispositioned (unmapped 0, feasible_open 0) and restart reconciliation complete; D:06 closed. (A Stryker score is not acceptance; the changed-P1 obligations remain part of the independent CHECK.) D:08 accepted-handoff remainder and D:R4-06 manifest assembly remain | artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/
2026-10-12T09:20:42Z | DO | r4 | n1/3 | D:R4-06 closed — DO→CHECK boundary. Frozen rv4/evidence-manifest.json (contract_rv=4, required_rows=21: EV-01..14, RC-01..03, EV-R4-A01/A07/A08/SCOPE) all **met** (21 met / 0 open); rv4/report.json (10 executions: 1 boundary solution build + 1 boundary unfiltered sweep + 8 filtered inner) validated; VALIDATE-MANIFEST exit 0, VALIDATE-REPORT exit 0; freeze/tree.json file_count=20, file_set_sha256=f9e83841478058d02d24b2402e9c2a59e13b0b66cca2cda629b6c7a9d73437bb, all CRLF; capture-audit/receipts-index.json inventories 44 mandatory commands (0 missing) + self-tests 0/7 across 47 receipts incl. nested coverage-tests and both mutation runs; freeze/ledger.json reconciles the EV-13 full artifact set (mismatch fixed) and records the rv2 branch-delta provenance incident honestly (counters restored 254/279/160/194, NOT byte-identical, no byte-restoration claim); rv1/rv2/rv3 untouched; no commit | artifacts/pdca/D211-eager-perparent-topn-1/rv4/evidence-manifest.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/report.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/tree.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/ledger.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/capture-audit/receipts-index.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-MANIFEST.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-REPORT.json
2026-10-12T10:11:59Z | DO | r4 | n2/3 | CHECK(r4,n1) fixes — D211-R4-A07-STRUCTURAL-ORDER: added the PLAN-required structural ordering assertion to `SingleQuery_OrdinalOrderBy_RetainsEffectivePerParentOrder` (SQL interceptor; asserts the embedded window carries `over (partition by parent_id order by 1)`); ordinal order is RETAINED (no loss, no rejection, no production edit); T-SQLITE exit 0 (70/0) and focused A07 `Feature=D211&FullyQualifiedName~OrdinalOrderBy` exit 0 (3/0) | tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/T-SQLITE.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/T-SQLITE-A07-FOCUSED.log
2026-10-12T10:11:59Z | DO | r4 | n2/3 | EV-11 disposition resolved (option b, no speculative production edit) — `EntityBuilderEagerLoading.cs:596:43` `Equality mutation` re-dispositioned from the weak 'defensive/other-guard' rationale to a concrete reachability proof: `Count >= 0` differs from `Count > 0` only for a non-null empty `Sorting`, which `EntityBuilder.OrderBy` never produces (null when unordered, >=1 after OrderBy), so the mutation is equivalent on every reachable state (defensive/unreachable); no killing test exists. PROBE-mutation re-run (required for RC-01) real exit 0, duration 2183.183 s: nested canonical exit 0 (1239.246 s) K153/S36/NC26/TO2 score 71.43%, restart exit 0 (943.667 s) K1/S6 score 14.29%; reconciliation refreshed: 52 spans, non_killed 64 (equivalent 30 + defensive 34), unmapped 0, feasible_open 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/reconciliation.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/survivors.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/README.md; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/PROBE-mutation.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/MUTATION-CANONICAL.log; artifacts/pdca/D211-eager-perparent-topn-1/rv4/logs/MUTATION-RESTART.log
2026-10-12T10:11:59Z | DO | r4 | n2/3 | Evidence reconciliation (append-only) — EV-14: created artifacts/pdca/D211-eager-perparent-topn-1/rv4/contract/rv4-contract.json (contract_rv=4; required_rows=21 + obligations + commands); EV-10: created artifacts/pdca/D211-eager-perparent-topn-1/rv4/coverage/branch-dispositions.json/.md (all changed P1 branches defensive/parameter-mirror/unreachable; 0 feasible) and artifacts/pdca/D211-eager-perparent-topn-1/rv4/coverage/identity.json reconciling raw cobertura 87.90/80.12 vs reportgenerator 88.5/80.6 as metric classification of the SAME collect (threshold bound to reportgenerator per CI convention; raw also passes); RC-01: receipts-index now inventories 47 mandatory ids (present 47, missing 0) across 54 receipt files incl. PROBE-mutation, PROBE-freeze, nested coverage-tests and both mutation runs; EV-R4-SCOPE: capture/contract binding completed | artifacts/pdca/D211-eager-perparent-topn-1/rv4/contract/rv4-contract.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/coverage/identity.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/coverage/branch-dispositions.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/capture-audit/receipts-index.json
2026-10-12T10:11:59Z | DO | r4 | n2/3 | P2 bookkeeping — perf/summary.json case_count fixed 8->7 (spurious 'Method' token removed); added artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native-exports.json retaining the native export inventory and reconciling the 9 failed Default-toolchain duplicate jobs (environmental D208 snapshot duplicate, duplicates of the 9 InProcessEmitToolchain cases); freeze/tree.json deduped 20 entries -> 19 unique (duplicate status-file path) with an appended reconciliation preserving the previous hash artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/tree.json file_set_sha256_previous=f9e83841..., new=330af80b...; mutation/README.md summary dict fixed to the true aggregation; re-validated VALIDATE-MANIFEST exit 0 (21 met/0 open) and VALIDATE-REPORT exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/summary.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native-exports.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/tree.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/README.md; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-MANIFEST.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-REPORT.json
2026-10-12T10:19:45Z | CHECK-evidence | r4 | n2/3 | EV-14/RC-03/EV-R4-SCOPE current-source identity binding — recomputed the ACTUAL current hashes/sizes of all changed/owned files and appended `identity_reconciliation` to artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/tree.json (recorded history preserved; authoritative corrected identity): 2 changed — tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs 75b00925/78143 -> 948131e2/78731 (A07 structural assertion), docs/specs/status/D211-eager-perparent-topn-1.md 88c485a2/162137 -> 1ff5dcd6/167991; 2 added — docs/advanced/eager-loading-per-parent-paging.md b71fc4cf/11273, docs/ru/advanced/eager-loading-per-parent-paging.md a0509753/19111 (previously omitted); corrected_file_count=21; corrected file_set_sha256=7354767cf2d757d81c78197c077f89df5305e1b367b08a73fd1fce87a76c7ba1 (recorded 330af80b.../f9e83841... kept as history); src/nextorm.core/Builders/EntityBuilderEagerLoading.cs unchanged ed41fd67/39413 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/tree.json
2026-10-12T10:19:45Z | CHECK-evidence | r4 | n2/3 | report.json + ledger rebound — executions source_revision/artifact_revision refreshed from the stale f9e83841 binding to `rv4-corrected@42db0eabde44+7354767cf2d7`; freeze/ledger.json identity_binding updated; re-validated via the rv4 wrapper: VALIDATE-REPORT exit 0, VALIDATE-MANIFEST exit 0 (21 met / 0 open) | artifacts/pdca/D211-eager-perparent-topn-1/rv4/report.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/ledger.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-REPORT.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-MANIFEST.json
2026-10-12T10:19:45Z | CHECK-evidence | r4 | n2/3 | EV-13 retained native exports — copied the still-present rv4 acceptance BDN exports (5 classes x report-{github.md,csv,html} = 15 files) into artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native/acceptance/ and the rv4 dedicated-eager BDN log into artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native/eager/ (16 retained files with sha256/size), and updated artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native-exports.json with resolving repo-relative source paths + identities; statement recorded: the eager class produced ONLY a *.log export (its Default out-of-process `*-report-*` generation failed on the pre-existing D208 snapshot duplicate), so no eager report file is claimed | artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native-exports.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native/acceptance/; artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native/eager/
2026-10-12T10:19:45Z | CHECK-evidence | r4 | n2/3 | P2 numbers corrected — mutation/README.md canonical score 71.89% -> 71.43% (and non-killed 64: equivalent 30 + defensive 34); manifest EV-13 perf value corrected to time_ratio=2.06 (+10.2% vs 1.87) and alloc_ratio=7.74 (+4.3%), 7/7 cases, wall 48.5 s | artifacts/pdca/D211-eager-perparent-topn-1/rv4/mutation/README.md; artifacts/pdca/D211-eager-perparent-topn-1/rv4/evidence-manifest.json
2026-10-12T10:25:14Z | CHECK-evidence | r4 | n2/3 | EV-13 single-campaign re-bind — re-ran PERF-ACCEPTANCE through the rv4 wrapper: real exit 0, wall 53.33 s; ratios from THIS run time=2.11 (Cached_ToList 1849.6 us / Prepared_ToList 875.0 us), alloc=7.74 (589.44/76.14 KB), 7/7 cases; copied the 15 BDN report exports THIS run produced (5 acceptance classes x github.md/csv/html) into artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native/acceptance-campaign/ and bound them by sha256/size to the shared receipt PERF-ACCEPTANCE + log; removed the mismatched prior perf/native/acceptance/ copies (they came from a different run: ratio 2.03, 910.3/1850.4 us) and recorded the discrepancy; updated artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native-exports.json, artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/summary.json and the manifest EV-13 value to the same campaign; restored tracked benchmarks/BenchmarkDotNet.Artifacts/** to HEAD and removed run-generated untracked report files; appended the campaign binding to artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/tree.json + artifacts/pdca/D211-eager-perparent-topn-1/rv4/freeze/ledger.json (prior hashes preserved); re-validated VALIDATE-MANIFEST exit 0 and VALIDATE-REPORT exit 0 | artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native-exports.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/native/acceptance-campaign/; artifacts/pdca/D211-eager-perparent-topn-1/rv4/perf/summary.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-MANIFEST.json; artifacts/pdca/D211-eager-perparent-topn-1/rv4/receipts/VALIDATE-REPORT.json


## CHECK PASS (r=4, rv=4) — 21/21 met, 0 open

- Verdict: **CHECK PASS**. Authoritative evidence root: `artifacts/pdca/D211-eager-perparent-topn-1/rv4/`.
- Per-row tally: **21 met / 0 open** — EV-01..EV-14, RC-01..RC-03, EV-R4-A01, EV-R4-A07, EV-R4-A08, EV-R4-SCOPE; manifest `rv4/evidence-manifest.json` (`contract_rv=4`, `required_rows=21`); report `rv4/report.json` (VALIDATE-MANIFEST exit 0, VALIDATE-REPORT exit 0).
- Retained caveats:
  - Unrelated flaky ClickHouse test `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs:731` (Float32 group parity) caused the first REGRESSION-SOLUTION failure; the identical suite passed under COVERAGE-COLLECT and PROBE-regression (final authoritative sweep exit 0, 10862/0/198).
  - rv2 branch-delta provenance incident disclosed: `rv2/coverage/branch-delta.json` counters restored (254/279 lines, 160/194 branches) but **not byte-restored**; rv4 does not depend on it.
  - `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:596` survivor dispositioned **defensive** with a concrete unreachability proof (the differing state is not producible; no killing test exists).
  - Superseded EV-13 export list labelled **historical**: the earlier `perf/native/acceptance/` copies came from a different run; EV-13 is bound to the single self-consistent `perf/native/acceptance-campaign/` (receipt `PERF-ACCEPTANCE`, wall 53.33 s, time ratio 2.11, alloc ratio 7.74).
  - 2 pre-existing DocFX warnings (duplicate `AnalyzerReleases.*.md` source) — not introduced by #211.
- Remainders: **D:06 EV-11 remainder resolved** (canonical + restart both real exit 0: 153/36/26/2 score 71.43% and 1/6 score 14.29%; reconciliation unmapped 0 / feasible_open 0, all 52 spans accounted). **D:08 accepted-handoff remainder closed** (valid manifest + report; DO did not issue the verdict; independent CHECK accepted).
- `Notice:` the **#211 GitHub issue remains open for manual closure** — this ACT dispatch authorized local commits only, not external GitHub writes; the issue was NOT closed.

### Progress log (r=4, ACT)

2026-10-12T10:28:45Z | ACT | r4 | n3/3 | PASS — CHECK PASS (21 met / 0 open); authoritative evidence root `artifacts/pdca/D211-eager-perparent-topn-1/rv4/`; commits `#211` product+tests=6da00a11 (docs+status in the follow-up commit); rv1/rv2/rv3 untouched; no push, no merge, issue #211 left open for manual closure | rv4/evidence-manifest.json; rv4/report.json; rv4/contract/rv4-contract.json

## Owner decision (2026-10-14)

- **Issue closed by the owner (done).** #211 and its absorbed #210 are CLOSED; task T211 is r=4/rv=4, CHECK PASS, 21/21 mandatory rows met, 0 open. The r=1 terminal `incomplete` (EV-11 evidence-capture gap, not a product defect) stays historical. Record: https://github.com/AlexeyShirshov/nextorm/issues/211
