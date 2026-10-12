# D212 — eager-load nested (level 2+ LoadWith)

- collection: `1.0.9-todo`
- collection_status: `docs/specs/status/collection-1.0.9-todo.md`
- task: T212
- issue: #212 — https://github.com/AlexeyShirshov/nextorm/issues/212
- selected_variant: `pdca-dotnet`
- cycle_id: N=1
- plan_revision: r=1
- evidence contract: rv1 (frozen manifest planned at `artifacts/pdca/T212/rv1/manifest.json`)
- phase: ACT complete / cycle closed
- plan_state: `done`
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
- DO started (collection phase D; single group; current worktree).
- D1 (reconnaissance + baseline) complete — result: nested level-2+ LoadWith is DROPPED, not executed (Split sync+async and SingleQuery load level-1 only; nested child factory never invoked; nested graph invisible to EagerLoadShape/plan key). Plan architecture NOT invalidated. Predecessor contracts T209/T211 intact, no revert. Evidence: artifacts/pdca/T212/rv1/A/{E01.log,characterization.md,E08-baseline.log}. D2 started.
- 2026-10-12T10:46:32Z | DO | revision r1 | iteration 1/3 | D2 (graph contract) complete — new internal EagerLoadGraph / EagerLoadNode / EagerLoadGraphContext / IEagerLoadNodeSource / IEagerLoadNodeSpec; ancestor-composite identity (L1 key + L2 key + ...), graph-wide mode/filter context, deterministic cycle guard + nested-mode conflict rejection, frontend normalization NormalizeEagerLoadGraph; no Split/Single orchestration, no public ThenInclude, T209/T211 intact, QueryCommand.Cache untouched. Evidence: artifacts/pdca/T212/rv1/A/D2/{build.log,build-core-tests.log,inner.log,boundary.log}; inner 156/156 pass, boundary 2334/2334 pass. D3 next.
- 2026-10-12T10:59:39Z | DO | revision r1 | iteration 1/3 | D3 (Split + in-memory) complete — iterative descendant orchestration on the split path: EagerLoadSpec.Execute/ExecuteAsync resolve the scoped child builder once, load one batched child query per node/key-chunk, assign by key, then recurse into the child builder's own specs via EntityBuilderEagerLoading.ExecuteLoadedChildren(Async); root validates the whole graph once through EagerLoadGraph.Build (cycle + nested-mode conflict fail-closed); section-7 filter-scope union propagated level by level, null-key ignored, read-only-null fail-closed; #211 per-parent paging honored per level (in-memory rejects at the nested node). T209 single-graph mode, T211 contracts, QueryCommand.Cache untouched. Evidence: artifacts/pdca/T212/rv1/A/D3/{build.log,inner.log,boundary-core.log,boundary-sqlite.log,boundary-sqlite-filtered.log,report.json}; inner 172/172, core 2350/2350, sqlite filtered 54/54, sqlite full 1632/1633 (1 pre-existing LOB probe skip); report validated exit 0.
- D2 (graph contract) complete — IEagerLoadNodeSpec/IEagerLoadNodeSource, EagerLoadNode.CompositeIdentity, EagerLoadGraphContext(Mode,FilterScope), EagerLoadGraph, Build<TEntity> (single graph-wide mode via T209), cycle guard, ToShape() seam, NormalizeEagerLoadGraph. New tests NestedEagerLoadingGraphContractTests.cs; core 2334/2334 green; build 0/0. D3 started.
- 2026-10-12T11:17:06Z | DO | revision r1 | iteration 1/3 | D4 (Single) complete — expressible nested single-query executes with ancestor-identity assembly; unexpressible child shape / nested per-parent paging / projection-arity overflow (>7 children) / nested mode conflict fail closed with NotSupportedException before SQL; no silent descendant drop or mode fallback. `EagerLoadSpec.PlanSingleQuery` + `EagerLoadNestedJoinIntoSpec` + recursive `StitchSingleQueryCore`; `BuildInMemoryJoinIntos` type-aware re-root with null guard for childless intermediates. New `tests/nextorm.core.tests/NestedEagerLoadingSingleQueryTests.cs` (11 tests) + 2 SQLite nested SQL-gen tests in `EagerLoadingSqlGenerationTests.cs`; D1 single-query characterization converted to positive. Scope amendment: footprint expanded to `Joins/JoinIntoStitcher.cs` and `EntityBuilderExtensions.cs` (stitcher nested slice + graph validation before building), selector unchanged. Evidence: artifacts/pdca/T212/rv1/A/D4/{brief.json,report.json,build.log,inner-core.log,boundary-core.log,boundary-{sqlite,postgres,sqlserver,mysql,clickhouse}.log}; build 0/0; inner core 183/183, sqlite eager 56/56, core boundary 2361/2361, all five dialect eager filters green; validate_inner_loop.py report exit 0. D5 next.
- 2026-10-12T16:33:32Z | DO | revision r1 | iteration 1/3 | D5 (verification) complete — +12 dialect nested SQL/capability tests (postgres/sqlserver/mysql/clickhouse + sqlite), +2 core grandchild rejections, +5 nested integration (CommonTestSuite), +2 ClickHouse-specific nested; integration 78/78 passed 0 skipped all 5 providers executed; boundary core 2363/0/0, dialect suites green; build 0/0; report.json exit 0. D6 started.
- D6a (docs) complete — EN+RU nested eager-loading docs, provider/frontend matrix, fail-closed forms, limitations/query-filters/guide-29/index/comparisons updated; internal specs synced; docfx build 0; no specs links from public pages. D6b (perf + frozen manifest) next.
- 2026-10-12T11:57:49Z | DO | revision r1 | iteration 1/3 | D6b-part1 (perf + frozen evidence map) complete — E08 acceptance perf 7/7 exit 0, Allocated byte-identical vs baseline; all >5% time deltas classified NOISE after two further paired runs (broad-spectrum, non-monotonic, and the 7 acceptance benchmarks never invoke the eager path); E02 core 2363/0/0, E03 sqlite 56 + pg/sqlserver/mysql/clickhouse 7 each (0 fail), E04 integration 78/0/0 with all 5 providers executed (no skips), E05 build 0/0, E07 docfx 0 errors; docs-audit done. Evidence: artifacts/pdca/T212/rv1/A/{E08-final.log,E08-final-rerun1.log,E08-final-rerun2.log,E02.log,E03-{sqlite,postgres,sqlserver,mysql,clickhouse}.log,E04.log,E05.log,core-case-map.md,provider-shapes.md,integration-case-map.md,docs-audit.md}; benchmarks/BenchmarkDotNet.Artifacts tree restored to its pre-run state. Frozen manifest sign-off remains for D6b-part2.
- 2026-10-12T12:04:24Z | DO | revision r1 | iteration 1/3 | E06/R9 coverage (CI flow) PASS — line 88.6% >= 85, branch 80.6% >= 75 on covered scope (core 88.4%, sqlite 90.5%, postgres 90.2%, sqlserver 95%); full-solution `dotnet test --no-build` under dotnet-coverage collect: 10941 total, 10742 passed, 198 skipped, 1 failed — environmental SQL Server container `Execution Timeout Expired` in `SqlServerSpecificTests.XmlMethods_ShouldReturnValues` (unrelated to eager path; no T212 test touched). Evidence: artifacts/pdca/T212/rv1/A/E06.log; report tests/coverage/report/Summary.txt.
- D6b (evidence) complete — frozen manifest artifacts/pdca/T212/rv1/manifest.json validated exit 0; perf PASS, coverage 88.6/80.6 PASS, build 0/0, core 2363/0/0, integration 78/0/0 all 5 providers. DO gate 2 satisfied; entering CHECK.
- Corrective DO (attempt 2/3): EB:1869 same-CLR-type binding fixed (JoinExpression.EagerParentItemNumber); matrix rows A/B/C/D closed with tests; EEL:360-363 rejection covered (zero SQL/no partial graph); build 0/0, core eager 193, core full 2371, integration 78 all 5 providers; manifest refreshed. Re-CHECK pending.
- CHECK(r1) attempt 1/3 FAIL: mandatory matrix A/B/C/D + R4/R5 deficits.
- Corrective DO (attempt 2/3): EB:1869 same-CLR-type binding fixed (red->green); matrix rows closed; rejection branch covered.
- CHECK(r1) attempts 2/3 and 3/3 FAIL: evidence-blocked on R8 only (R1-R7,R9 clean; variants A-D closed).
- escalate: accept R8 as met (GroupByCount +5.50% not attributable to T212, unreachable + non-reproducible). No 4th CHECK.
- ACT: cycle closed. R1-R9 met.

## Acknowledgement — T209 rv1 shared contract

- Contract revision: `rv=1` — `artifacts/pdca/D209/rv1/sibling-contract.md`.
- This sibling keeps its own plan revision: **r=1** (unchanged).
- Compatibility verdict: **compatible; obligation noted** — #212 must extend `EagerLoadShape` identity and add negative tests wherever nested execution changes, must not bypass T209 precedence/resolution, and must never mutate the shared `QueryCommand.Cache`.

## Done / Verified / Incomplete
- Done: R1–R9 met (ACT — cycle closed).
- Verified: build 0W/0E; core 2371/2371; core eager 193/193; dialect eager sqlite 59 / postgres 7 / sqlserver 7 / mysql 9 / clickhouse 7 (0 fail/skip); integration 78/78 all 5 providers 0 skips; coverage line 88.6% / branch 80.6%; frozen manifest `artifacts/pdca/T212/rv1/manifest.json` E01–E08 all met, validator exit 0; R8 per escalate (`check2/E08-paired.md`, `E08-disposition.md`); EB:1869 fix red→green.
- Incomplete: none.
- Note: EN/RU docs updated but left UNCOMMITTED (entangled with the external unrelated docs reorg).
- CHECK(r1) FAIL (attempt 1/3): mandatory matrix rows A/B/C/D open (R4/R5), uncovered rejection branch EEL:360-363; correctness candidates EB:1869 / EEL:342,558 be characterized; E06 exit labeling. Corrective DO started (attempt 2/3).
