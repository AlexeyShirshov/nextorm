# D212 — eager-load nested (level 2+ LoadWith)

- collection: `1.0.9-todo`
- collection_status: `docs/specs/status/collection-1.0.9-todo.md`
- task: T212
- issue: #212 — https://github.com/AlexeyShirshov/nextorm/issues/212
- selected_variant: `pdca-dotnet`
- cycle_id: N=1
- plan_revision: r=1
- evidence contract: rv1 (frozen manifest planned at `artifacts/pdca/T212/rv1/manifest.json`)
- phase: P (own PLAN complete) — **stopped at PLAN->DO boundary** (collection phase P; no DO)
- plan_state: `ready`
- base: branch `1.0.9` @ `58025147`

## Goal
Materializing a declared finite nested eager-load graph loads every requested level via child-builder recursion (`LoadWith` on the child query), preserving ancestor-scoped identity, assignment, filter-scope, per-level ordering/top-N, and a single graph-wide mode; level-one behavior and cache invariants unchanged. Fail-closed on every unexpressible form.

## Acceptance criteria
- R1 arbitrary finite nesting; demonstrated at depth 2, depth 3, and a deep-chain stress case.
- R2 nested in Split and in expressible Single; one mode for the whole graph.
- R3 section 7 assembly/dedup/filter-scope/null-key at every level (composite level identity: L1 key + L2 key + ...).
- R4 independent per-parent order/top-N at successive levels (per-level order-key/top-N).
- R5 `LoadWith` / `JoinInto` / entity-materializing implicit-nav parity across the provider matrix.
- R6 existing level-one behavior and shared-command/plan-cache invariants unchanged.
- R7 docs EN+RU (+ internal spec sync); R8 perf measured; R9 coverage >=85% line / >=75% branch on the covered scope.
- Negatives (each implemented rejection branch needs its own negative test): unsupported Single child shape, unsupported provider translation, unsupported frontend/graph composition -> `NotSupportedException`; no silent descendant drop, no silent mode fallback, no partial graph. Null key -> no match/no crash. Incompatible explicit child modes -> existing rejection contract. Unsupported per-level order/top-N -> #211 fail-closed contract.

## Open question to resolve in D1
Split path `EagerLoadSpec.Load :232` / `LoadAsync :244` calls `BuildChild(...).ToList()`, and `ToList` consults `builder.LoadSpecs` (`EntityBuilderExtensions.cs:486,512-514`) — nested may execute rather than drop. UNPROVEN; must be characterized (dropped vs executed) before architecture is fixed.

## Variant matrix (from execution path; each row = test / guard / deferred+trigger)
- A declaration x strategy x shape: LoadWith/JoinInto/entity-materializing implicit-nav x {Split, expressible Single, unexpressible Single}; Default asserts #209-selected strategy. Guard: missing capability = tested rejection.
- B per-level controls: unordered/unlimited; order only; top-N+deterministic order; different order/top-N at two consecutive levels; empty intermediate result — both strategies where expressible. Unsupported combos -> guard+negative test. Tie behavior per #211.
- C identity/state: value/reference child; default/null key; nullable + composite key; repeated child key under different ancestors; duplicate join rows; sibling branches; empty/null/read-only collection; child/parent IgnoreFilters combos; sync/async. Shared command/cache state unchanged.
- D providers: SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse each positive-or-rejection for A/B + identity-sensitive SQL; integration verifies materialization; MariaDB inherits MySQL (parity guard; distinct nested behavior blocks inherited claim).

## Tasks (DO plan)
- D1 (fix now) — characterize + baseline: `EntityBuilderEagerLoading.cs:232,244`, `EntityBuilderExtensions.cs:485-539`, `EntityBuilder.cs:662-809`; nested characterization tests; resolve split dropped-vs-executed; roslyn inventory of Single reject/capability branches; map implicit-nav entity-materializing expansion; verify predecessor contracts; record pre-change perf. Deliver observation table before D2.
- D2 (fix now) — graph contract: `EntityBuilderEagerLoading.cs:12,60,65-74,351,397`, `EntityBuilder.cs:563,625`; shared internal node/path metadata, ancestor-composite identity, graph-wide mode/filter context, declaration validation, frontend normalization. No public ThenInclude / per-level strategy API.
- D3 (fix now) — Split + in-memory: `EagerLoadSpec` split path, `Joins/JoinIntoStitcher.cs:60`, terminals `EntityBuilderExtensions.cs:508,513,532,537`; iterative descendant orchestration, batch by graph node, apply section 7 + #211 per level; sync/async parity.
- D4 (fix now) — Single: `EntityBuilder.cs:1119-1191,1227`, `EntityBuilderEagerLoading.cs:176`; nested joins/derived shapes with inherited mode + provider capabilities; assemble by ancestor identity; validate unsupported shapes before execution; never silently discard nested metadata.
- D5 (fix now) — verification: extend existing eager/mode/rejection suites; add nested tests in core, all five dialect projects, integration; ClickHouse-specific integration (not a CommonTestSuite subclass).
- D6 (fix now) — docs + evidence: update EN/RU docs, provider/frontend matrix, predecessor provenance; run contract; CHECK.

## Test strategy
- Core (`tests/nextorm.core.tests`): planning, assembly, in-memory parity, rejections, cache isolation. Siblings: `EagerLoadingTests.cs:72`, `EagerLoadingSingleQueryTests.cs:54`, `EagerLoadingSingleQueryP1Tests.cs:169,181,193,240`, `EagerLoadingModeTests.cs:341,260`, `JoinIntoRejectionTests.cs:35`.
- Dialect SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql,clickhouse}.tests`): SQL/capability; only sqlite has `EagerLoadingSqlGenerationTests.cs` today; mirror per provider.
- Integration (`tests/nextorm.integration.tests/CommonTestSuite.EagerLoading.cs`): split/single parity, filter/order/top-N, graph materialization; CommonTestSuite subclasses = Sqlite/Postgres/MySql/SqlServer; **ClickHouse is separate** (`ClickHouseIntegrationTests.cs:18`).
- Boundary sweep: `dotnet test tests/<project> -c Debug` per project; integration with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`.
- Coverage: `coverage.settings.xml` scope core/sqlite/postgres/sqlserver >=85/75 this cycle regardless of branch.

## Docs plan (R7)
Update EN + RU: `docs/advanced/eager-loading.md`, `advanced/relationships.md`, `advanced/limitations.md`, `advanced/query-filters.md`, `guide/29-implicit-navigation.md`, `index.md`, `comparisons/capabilities.md` (and RU mirrors); inspect `docs/advanced/toc.yml:7-8`, `docs/ru/toc.yml`; no page move/deletion -> no renumbering. Public pages must not link to internal specs. Sync internal pipeline/gap/comparison specs.

## Performance measurement decision (R8) — MEASURE
Execution/materialization change is per-query/per-row (`EntityBuilderExtensions.cs:496-539`, `EntityBuilder.cs:1227`, `JoinIntoStitcher.cs:60`). Capture the 7 acceptance cases before/after on the same host/config: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`. Investigate >5% time/allocation regressions with two further paired runs; reject reproducible regression. Nested tests additionally assert no per-parent N+1 and stable execution counts as fan-out grows.

## Reconnaissance decision — REQUIRED (D1)
Executable characterization + roslyn impact map distinguishing dropped vs executed nested Split, actual frontend representation, and the Single capability/rejection inventory. Success = observation table backed by logs, not doc inference. If it invalidates the architecture -> revised PLAN before production edits.

## Unit execution mode
Sequential units in one working tree (shared builder/metadata/materialization/assembly footprints overlap). No worktrees. Provider test runs may be concurrent only after the shared contract is stable and resource limits checked.

## Write-footprint (planned) + uncertainty
`src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`, `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs`, `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs`, plus implicit-nav lowering/query files identified by D1; `tests/nextorm.core.tests/`, `tests/nextorm.{sqlite,postgres,sqlserver,mysql,clickhouse}.tests/`, `tests/nextorm.integration.tests/`; docs listed above; optional benchmark extensions.
Uncertainty: exact implicit-nav lowering files and additional Single translator files — resolve and record paths in D1 before editing.

## Prerequisites / assumptions
Approved B/Q scope + section 7 authoritative despite draft spec; .NET 10/tooling, coverage tools, DB/container access available (missing socket = in-cycle prerequisite with documented recovery, not waived evidence); finite acyclic declarations assumed (cycles need a deterministic guard, not a depth cap). Incompatible spec revision or frontend representation -> return to PLAN.

## DAG requirements (predecessor results)
- #209 -> T212: consumable mode/capability policy, Default resolution, fail-closed dispatch without per-level overrides.
- #211 -> T212: composable per-parent order/top-N metadata + execution/translation contract incl. tie behavior.
- #213 -> T212: JoinInto composite/M:N identity + assignment contract where those shapes participate.
- D1 runs independently; implementation of affected shapes waits for these results. Reuse, don't duplicate. Section 7 stays aligned with #20 phase 6.

## Evidence contract (frozen, rv1) — mandatory rows
- E01 / R0 reconnaissance (owner coder-D1): characterization + roslyn inventory; `A/E01.log`, `A/characterization.md`.
- E02 / R1-R6 core: `dotnet test tests/nextorm.core.tests -c Debug`; `A/E02.log`, `A/core-case-map.md`.
- E03 / R2,R4-R6 SQL: `dotnet test tests/nextorm.{sqlite,postgres,sqlserver,mysql,clickhouse}.tests -c Debug`; `A/E03-<provider>.log`, `A/provider-shapes.md`.
- E04 / R1-R6 integration: `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug`; no provider skips; `A/E04.log`, `A/integration-case-map.md`.
- E05 / R6 build: `dotnet build nextorm.slnx -c Debug` 0/0; `A/E05.log`.
- E06 / R9 coverage: dotnet-coverage collect + reportgenerator; line>=85/branch>=75; `A/E06.log`.
- E07 / R7 docs: `dotnet docfx docs/docfx.json` + doc audit; `A/E07.log`, `A/docs-audit.md`.
- E08 / R8 perf: acceptance benchmark baseline+final; regression rule; `A/E08-baseline.log`, `A/E08-final.log`.
Artifacts under `A=artifacts/pdca/T212/`. CHECK owns <=2 targeted re-gather batches; exhaustion -> evidence deficiency to PLAN; no supersession at rv1.

## Risks
Evidence supports execution surfaces + current test deficit, not nested runtime behavior or implicit-nav representation. Main risks: predecessor contract drift, Single Cartesian expansion, filter leakage, ancestor identity collisions.

## Progress log
- PLAN(r1) started (collection phase P).
- PLAN(r1) complete — stopped at PLAN->DO boundary. No DO, no branches, no worktrees, no product code.

## Acknowledgement — T209 rv1 shared contract

- Contract revision: `rv=1` — `artifacts/pdca/D209/rv1/sibling-contract.md`.
- This sibling keeps its own plan revision: **r=1** (unchanged).
- Compatibility verdict: **compatible; obligation noted** — #212 must extend `EagerLoadShape` identity and add negative tests wherever nested execution changes, must not bypass T209 precedence/resolution, and must never mutate the shared `QueryCommand.Cache`.

## Done / Verified / Incomplete
- Done: own PLAN r1.
- Verified: —
- Incomplete: —
