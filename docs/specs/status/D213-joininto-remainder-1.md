# D213 — JoinInto remainder (composite keys, composite junction selectors, M:N under single-query)

- task: T213 / issue #213 — https://github.com/AlexeyShirshov/nextorm/issues/213
- collection: `1.0.9-todo`; selected_variant: `pdca-dotnet`; cycle: N=1; plan_revision: r=1 (contract rv=1)
- base ref: `58025147` (branch `1.0.9`)
- phase: **PLAN — durable checkpoint at the PLAN→DO boundary** (no DO started, no branches/worktrees, no product code)
- plan_state: **gap** — blocker: required `planner` role unavailable (upstream "Insufficient account funds"); reconnaissance complete, plan finalize pending

## Notice

The PLAN decider (`planner`, medium tier) could not be re-run to finalize readiness: the upstream
provider returned "Insufficient account funds" on two attempts. The bounded reconnaissance the planner
requested in its first pass has completed successfully (see "Reconnaissance (resolved)"). Per the cycle
contract a required-but-unavailable role is a resource blocker: `plan_state` stays `gap` (not `ready`),
no DO is started, and no role is silently substituted. Next allowed step: re-dispatch `planner` (no
re-gather needed) once the medium-tier provider is funded; it either confirms `ready` or issues a
genuinely revised plan.

## Goal (essence)

Make declared composite relationships and composite junctions execute through the existing JoinInto
assembly — including the SingleQuery dispatcher — without changing public `LoadWith`/`Join` API,
without inferred (FK-convention) relationships, and without altering established scalar/split behavior.

## Acceptance criteria (each with a negative case)

- AC01 Declared/local composite 1:N and 1:1 relations + existing explicit-key forms produce correct results. Negative: crossed component pairs must not match; malformed component counts fail deterministically.
- AC02 Scalar/composite parent identity deduplicates parents without merging distinct full keys. Negative: identical first components with differing later components remain separate; keyless-parent behavior unchanged.
- AC03 Scalar/composite M:N junctions work standalone and through SingleQuery; full component partitioning preserves existing junction-occurrence semantics. Negative: absent children, duplicate links and colliding partial keys must not invent or lose associations.
- AC04 Filters, ordering/paging, multiple declarations, existing terminals, sync/async and repeated cached execution retain established behavior. Negative: no cross-call command-state/cache leakage, sibling alias collision, or extra query under SingleQuery.
- AC05 SQL-generation, in-memory and real integration evidence cover the 6-provider matrix. Negative: skipped providers or MySQL-only evidence labelled MariaDB do not pass.
- AC06 Metadata-only resolution and unrelated restrictions remain. Negative: undeclared navigation, invalid selector/equality, missing junction and arity overflow still reject; standalone JoinInto does not become a split-query executor.
- AC07 EN+RU describe supported forms accurately; `dotnet build nextorm.slnx -c Debug` 0/0; filtered coverage line >=85%, branch >=75%. Negative: stale rejection claims, either deficient metric, or mixed/LF endings fail.
- AC08 Required acceptance benchmarks + relevant JoinInto in-memory measurements recorded against a comparable baseline. Negative: missing measurements or a reproducible >=20% regression on comparable supported paths must be investigated; smaller deltas are not claims.

## Variant matrix (execution path; each row closed)

- Declared vs local config vs explicit-key overload; 1:N vs 1:1; scalar vs composite — **test** (SQL + core + integration); malformed/missing metadata — **guard + negative test**.
- Parent mapped identity scalar/composite; alternate principal key; component ordering/type compatibility — **test** full-key identity and relationship matching independently; incompatible metadata — **guard**.
- M:N parent-side composite / child-side composite / both composite; scalar control — **test** (SQL, in-memory, all-provider integration).
- Standalone M:N / SingleQuery dispatcher / ordinary LoadWith split / standalone JoinInto split rejection — **test** newly enabled dispatch and unchanged controls; split rejection — **guard + test**.
- Value/reference components, default values, nullable components, absent LEFT JOIN child — **test** CLR/SQL parity; null FK component must not manufacture a relation.
- Empty/one/multiple children; duplicate junction occurrences; same prefix / different suffix keys — **test** parent dedup, collection multiplicity, full-key isolation.
- Multiple collections/specs, filters, paging, non-list terminals, sync/async, repeated query/cache shape — **test** (existing regressions + targeted composite/M:N cases).
- Missing junction, undeclared relation, invalid selector/equality, keyless parent, arity > 8 — **guard + negative test** at existing boundaries.
- Six SQL dialects and six actual integration providers — **test** each provider separately; **no deferred rows**.

## Ordered units (single tree) and write-footprints

- **P:T213-r1-close-readiness** — fix now: the reconnaissance below (done). Acceptance = concrete representations/fixture paths and obtainable deciding commands.
- **D00** — fix now: write/complete `docs/specs/status/D213-joininto-remainder-1.md` (this file) with the frozen evidence contract before DO.
- **D01** — fix now: red regressions/controls in existing core JoinInto test files, six provider SQL-test projects, `CommonTestSuite.JoinInto.cs`; planned benchmark file `benchmarks/nextorm.benchmark/JoinIntoBenchmarks.cs`. Acceptance = targeted unsupported forms fail for the expected reason; scalar controls + harness-only baseline run.
- **D02** — fix now: `src/nextorm.core/Builders/EntityBuilder.cs:864-1009`, `Builders/Joins/JoinIntoSpec.cs:189/319/436/551/745`, `JoinIntoStitcher.cs:103-215`, `JoinIntoManyToManySpec.cs:221-339`, `JunctionLinkSourceFactory.cs:99-107`; identity/cache files only if proven necessary. Acceptance = AC01-03, unrelated guards retained.
- **D03** — fix now: `EntityBuilder.cs:1239-1241/1589/1630`, `JoinIntoManyToManySpec.cs:398/448`, plus projection/link/source-resolver files only where semantics require. Acceptance = AC03-04; two joins / occurrence partition include every component.
- **D04** — fix now: all six SQL-test projects, existing core regression files, `CommonTestSuite.JoinInto.cs`, existing `ClickHouseJoinIntoOneToOneTests.cs`; focused ClickHouse/MariaDB integration adapters (MariaDB container already exists). Acceptance = AC05 against real MariaDB (not MySQL-substituted).
- **D05** — fix now: documentation files (see Docs plan). Acceptance = AC07 docs portion; remove only delivered rejection predicates.
- **D06** — fix now: evidence artifacts/status only + CHECK corrections. Acceptance = complete manifest, AC01-08, no unaccounted failed/skipped rows.

## Test strategy

Unit/core tests own key equality, hashing, null/default, dedup, occurrence assembly, identity-selector/cache and rejection behavior (`tests/nextorm.core.tests`). SQL tests own complete predicates, aliases and component partitioning (`tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests`). Integrations own actual joins, result assembly and observed query count (`tests/nextorm.integration.tests/CommonTestSuite.JoinInto.cs` for its existing 4 providers; focused wrappers for ClickHouse and MariaDB). Run the complete core project and six SQL projects, not only new tests; focused red/green via `--filter "FullyQualifiedName~JoinInto"`. Coverage includes only the configured modules (`coverage.settings.xml` = core|sqlite|postgres|sqlserver); MariaDB/MySQL/ClickHouse remain mandatory functional evidence despite coverage exclusion. Both thresholds are hard gates for this cycle.

## Docs plan (EN + RU)

Update `docs/advanced/{relationships,limitations,eager-loading}.md`, `docs/guide/{02-joins,29-implicit-navigation}.md` and their exact `docs/ru/` counterparts: add composite examples/boundaries, the declared-only rule, and M:N under SingleQuery; retain O2O uniqueness and unrelated restrictions. Update internal specs `docs/specs/roadmap/{todo_eager_loading_pipeline,todo_navigation_properties}.md` and sweep the current statements in `docs/specs/comparison/{capability-matrix,linq2db-comparison,linq2db-backlog-gap-analysis}.md` and `docs/specs/roadmap/sql-capabilities-gap-analysis.md`. Historical dated conclusions stay historical with a superseding note. No public links to `docs/specs/**`, no public renames, no page moves/renumbering.

## Performance-measurement decision

**Required.** Planning/materialization/in-memory execution are touched (`EntityBuilder.cs:1239/1589/1630`, `JoinIntoStitcher.cs:103-215`, `JoinIntoManyToManySpec.cs:291-332`). Run `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` before and after the runtime change; add relevant in-memory JoinInto benchmarks (scalar standalone paths give same-harness baselines; newly supported composite/dispatcher forms get candidate measurements, not an exception baseline). Record allocations, environment, sample distribution and any reproducible >=20% regression.

## Reconnaissance / prototype decision

**Closed by reconnaissance** (no prototype yet): composite machinery already exists (`JoinIntoKey`, `JoinIntoIdentity.KeyMembers[]`, composite-capable identity-selector cache); only the single-component sites need generalizing; MariaDB/ClickHouse harnesses exist (container + focused tests). If key representation still proves uncertain during DO, authorize only a bounded harness-only experiment — not a speculative new runtime abstraction.

## Unit execution mode

**Sequential, one tree.** D02/D03 overlap `EntityBuilder`, JoinInto specs and the stitcher; tests/docs share sibling contracts. No worktrees justified or created; no commits/merges beyond the status-file checkpoint.

## Design checklist + overlay invariants

SOLID/DRY: keep metadata / expression-generation / assembly responsibilities; reuse existing equality + occurrence machinery, not a new framework. Avoid per-row reflection, selector compilation, component-array allocation, quadratic matching, sticky command mutations. No new runtime interface/type without a named 2nd consumer. New leaf types sealed; existing sealing changes need impact evidence. Preserve CPM, nullable/TreatWarningsAsErrors, CRLF. Nothing leaves milestone **1.0.9**.

## Risks / assumptions ("What the statement did not say")

- (a) Evidence: dispatcher exclusion = `EntityBuilder.cs:1235-1241`; existing two-join + assembly = `JoinIntoManyToManySpec.cs:398/448/291-332`; metadata-only = `IRelationshipMetadata.cs:6-8`; composite machinery = `JoinIntoSpec.cs:652-708`, `:745`; MariaDB container = `tests/nextorm.integration.tests/Providers/MariaDbContainer.cs:17`.
- (b) Assumptions/risks: "under single-query" means dispatcher composition (not merely standalone joins); no implicit maximum composite width; null-FK handling must agree across SQL/in-memory; junction multiplicity follows existing behavior.
- (c) Gaps: none remaining from reconnaissance; the only open item is the finalize decision blocked by the unavailable `planner` role (resource blocker, not missing evidence). Persistent low confidence would go to `escalate` trigger 5.

## Prerequisites and requirements on predecessor task results

No functional dependency on #20. **#209** is conditionally preceding only if its dispatcher change lands first: require a stable strategy-dispatch seam preserving public surface, declared-only metadata and query-count controls; otherwise implement T213 against the supplied base and hand off the same contract. **#211/#212**: serialize any overlapping `EntityBuilder`, join projection/stitcher, `CommonTestSuite` or shared-spec writes; require preservation of paging scope, identity/alias uniqueness and nested-level behavior (neither feature is added to T213). **#201/#53**: retain acceptance-benchmark compatibility and public-API freeze. Before grouping, obtain actual sibling footprints; no assumed independent parallelism, no silently inherited incomplete predecessor criterion.

## Evidence contract (frozen at DO→CHECK; rv=1)

Manifest path: `artifacts/pdca/T213/rv1/manifest.json` (validated by `python3 scripts/validate_inner_loop.py manifest <path>`).
Required rows: `E-BUILD` (AC07, `dotnet build nextorm.slnx -c Debug`), `E-CORE` (AC01-04,06, `dotnet test tests/nextorm.core.tests -c Debug`), `E-SQL-{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}` (AC01,03,06), `E-INT-{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}` (AC01-05; DOCKER_HOST; every listed provider must execute — no skips), `E-RED` (AC01,03), `E-PERF-BASE`/`E-PERF-FINAL` (AC08), `E-COVERAGE` (AC07; `dotnet-coverage collect` + `reportgenerator` + threshold assert), `E-DOCS` (AC07; `dotnet docfx docs/docfx.json` + `git diff --check` + EN/RU review + CRLF). CHECK re-gather budget: two targeted read-only rounds, owned by CHECK.

## Reconnaissance (resolved) — facts

- Composite machinery exists: `JoinIntoSpec.cs:745` `JoinIntoKey : IEquatable` stores `object?[] _values` (`:747`), `Equals :754-766`, hash `:772-779`; `JoinIntoIdentity` (`:12`, sealed) holds `string[] KeyMembers :27` + `string[] JunctionKeyMembers :41`; `BuildIdentitySelector(Core) :652-708` + `IdentitySelectorCache :664` already handle composite (`keys.Count==1`→boxed `:691-694`, `>1`→`JoinIntoKey :695-705`; test `JoinIntoIdentitySelectorCacheTests.cs:85-109`). `JoinIntoStitcher.cs:106` parent dedup keys on the entity's own selector (`:176`) — composite-aware.
- Single-component sites to generalize: `JoinIntoSpec.cs:215-216`,`:464-465`,`:253-254` (OccurrenceScope); `JoinIntoManyToManySpec.cs:147-148`,`:160-163`,`:207`,`:410-411`; `JunctionLinkSourceFactory.cs:107` guard, `:112-113` `[0]`, partition `:129-131`. Explicit-key overload `JoinIntoSpec.cs:319` stays.
- Dispatcher: guard `EntityBuilder.cs:1235-1241` inside `BuildSingleQueryJoins()` (`:1227-1302`); `_joinIntos` filled `AddJoinInto :1075`; `IsManyToMany` `JoinIntoManyToManySpec.cs:275`; two slots `GetItemTypes(bool) :245-246` vs default `JoinIntoSpec.cs:141`, summed `JoinIntoStitcher.cs:26-51`, materialized `EntityBuilder.cs:1349-1357`,`:1370`; in-memory edges `:1589`/`:1630` (`:1594-1598`,`:1647-1651`).
- Providers: SQL-gen for SQLite/Postgres/SqlServer/MySQL/ClickHouse; MariaDB `tests/nextorm.mariadb.tests` has zero JoinInto tests (project exists). Integration containers all exist incl. `Providers/MariaDbContainer.cs:17` (image `mariadb:11.4`, env `NEXTORM_MARIADB_CONNECTION`); `CommonTestSuite` derived = SQLite/Postgres/SqlServer/MySQL only; ClickHouse has `ClickHouseJoinIntoOneToOneTests.cs:14 : ProviderTestSuite`; MariaDB has ~10 standalone `IDisposable` integration classes. Integration cmd `AGENTS.md:60-61`; skipped≠green `AGENTS.md:65`.
- Evidence-contract definition: `~/.config/opencode/skills/pdca-dotnet/SKILL.md:919-979` + `:1147-1168`; validator `scripts/validate_inner_loop.py` (`:586`,`:495-562`,`:554-561`, exit 2 `:606-610`); `artifacts/pdca/<task>/rv<k>/` is an existing convention; coverage scope `coverage.settings.xml:11-13`; thresholds `.github/workflows/dotnet.yml:27-28`.

## Footprint uncertainty / blocker

High confidence in the required behavior and the existing execution seams; lower in key internals and the missing-provider (MariaDB/ClickHouse) CommonTestSuite wiring. Identity/cache files, new provider-fixture paths and exact comparison-spec paths remain explicitly unconfirmed. **Blocker: the `planner` medium-tier role is unavailable (upstream insufficient funds); finalize pending.**

## Done / Verified / Incomplete

- Done: reconnaissance; PLAN pass 1 (planner draft, all gate-1 sections present).
- Verified: —
- Incomplete: —
