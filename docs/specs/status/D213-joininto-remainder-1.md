# D213 — JoinInto remainder (composite keys, composite junction selectors, M:N under single-query)

- task: T213 / issue #213 — https://github.com/AlexeyShirshov/nextorm/issues/213
- collection: `1.0.9-todo`; selected_variant: `pdca-dotnet`; cycle: N=1; plan_revision: r=1 (contract rv=1)
- base ref: `58025147` (branch `1.0.9`)
- phase: **DO — started (r=1)**
- plan_state: **ready** — r=1 finalized (planner, medium tier); evidence contract rv=1 frozen

## Notice

The upstream planner resource blocker ("Insufficient account funds") is resolved. T213's existing plan is finalized as ready at r=1 with frozen evidence contract rv=1; no scope change, contract supersession, or attempt reset is required.

## Goal (essence)

Execute composite relationships and junctions through the existing JoinInto assembly, including the SingleQuery dispatcher.

Non-negotiable constraints: no public LoadWith/Join API change; no inferred FK relationships; no scalar/split-execution behavior change; preserve declared-only metadata resolution, occurrence semantics, cache isolation, paging scope, identity and alias uniqueness, existing unrelated restrictions.

Minimum solution: generalize the identified single-component consumers using the existing composite machinery; carry complete keys through projection, parent identity, association matching, junction partitioning; relax only dispatcher restrictions made obsolete.

Approach decision: (selected) extend existing JoinInto assembly + composite machinery; (rejected) separate composite executor (duplicates scalar logic, behavior drift); (rejected) route unsupported cases to split execution (violates SingleQuery/query-count + split constraints).

## Acceptance criteria (each with a negative case)

- AC01 Declared/local composite 1:N and 1:1, incl. explicit-key forms, execute correctly. Neg: crossed components do not match; malformed component counts fail deterministically.
- AC02 Scalar/composite parent identity deduplicates by the complete identity. Neg: same first component with different later components stays separate; keyless-parent behavior unchanged.
- AC03 Scalar/composite M:N works standalone and through SingleQuery; full-component partitioning preserves occurrences. Neg: missing children, duplicate links, colliding partial keys neither invent nor lose associations.
- AC04 Filters, ordering/paging, multiple declarations, terminals, sync/async, repeated cached execution retain behavior. Neg: no command-state/cache leakage, sibling alias collision, or extra SingleQuery execution.
- AC05 SQL generation, in-memory tests, real integration cover all six providers. Neg: skipped providers fail; MySQL cannot substitute for MariaDB.
- AC06 Metadata-only resolution and unrelated restrictions remain intact. Neg: undeclared navigation, invalid selector/equality, missing junction, arity overflow still reject; standalone JoinInto is not a split executor.
- AC07 EN+RU accurate; Debug solution build 0 warnings/0 errors; filtered coverage line >=85% and branch >=75%. Neg: stale rejection claims, either deficient metric, or mixed/LF endings in changed authored files fail.
- AC08 Acceptance benchmarks + relevant JoinInto in-memory measurements recorded against a comparable baseline. Neg: missing measurements or uninvestigated reproducible >=20% regression fail; smaller deltas are not claims.

## Ordered units (single tree) with states

Execution order D00 -> D01 -> D02 -> D03 -> D04 -> D05 -> D06; preserve red evidence before implementation; measure unmodified acceptance baseline before perf-affecting changes; establish supported in-memory comparators before D02.

- **P:T213-r1-close-readiness** — done — resolved reconnaissance + finalized r=1 readiness.
- **D00** — done — write this finalized status, retain complete frozen rv=1 contract + manifest obligations, replace Notice.
- **D01** — pending — red regressions and scalar controls in core JoinInto tests, all six provider SQL-test projects, `CommonTestSuite.JoinInto.cs`; establish comparable benchmark evidence and planned `benchmarks/nextorm.benchmark/JoinIntoBenchmarks.cs`.
- **D02** — pending — generalize complete-key projection/identity/matching/junction assembly at `EntityBuilder.cs:864-1009`; `JoinIntoSpec.cs:189/319/436/551/745`; `JoinIntoStitcher.cs:103-215`; `JoinIntoManyToManySpec.cs:221-339`; `JunctionLinkSourceFactory.cs:99-107`.
- **D03** — pending — integrate composite relationships/junction selectors into SingleQuery at `EntityBuilder.cs:1239-1241/1589/1630` and `JoinIntoManyToManySpec.cs:398/448`; retain unrelated guards and query-count controls.
- **D04** — pending — complete positive/negative coverage across all six SQL-test projects, core regressions, `CommonTestSuite.JoinInto.cs`, `ClickHouseJoinIntoOneToOneTests.cs`, focused ClickHouse/MariaDB adapters; run actual six-provider integration and final measurements.
- **D05** — pending — update EN+RU and internal docs (list below).
- **D06** — pending — collect/validate rv=1 evidence, coverage, logs; update status; address CHECK findings without weakening AC or evidence obligations.

Unit execution mode: sequential in the current tree. D01/D04 share tests; D02/D03 share implementation; sibling tasks overlap EntityBuilder, projection/stitcher, shared tests, specs. No assumed parallelism; no worktree requirement. Before any collection grouping obtain actual sibling footprints; until then serialize overlapping writes.

## Write-footprint

- D01: existing core JoinInto test files, six provider SQL-test projects, `CommonTestSuite.JoinInto.cs`; planned `benchmarks/nextorm.benchmark/JoinIntoBenchmarks.cs`.
- D02: `src/nextorm.core/Builders/EntityBuilder.cs:864-1009`, `Builders/Joins/JoinIntoSpec.cs:189/319/436/551/745`, `JoinIntoStitcher.cs:103-215`, `JoinIntoManyToManySpec.cs:221-339`, `JunctionLinkSourceFactory.cs:99-107`; identity/cache files only if proven necessary.
- D03: `EntityBuilder.cs:1239-1241/1589/1630`, `JoinIntoManyToManySpec.cs:398/448`, plus projection/link/source-resolver files only where semantics require.
- D04: all six SQL-test projects, existing core regression files, `CommonTestSuite.JoinInto.cs`, existing `ClickHouseJoinIntoOneToOneTests.cs`; focused ClickHouse/MariaDB integration adapters (MariaDB container already exists).
- D05: documentation files (see Docs plan).
- D06: evidence artifacts/status only + CHECK corrections.

Supplied line references are reconnaissance anchors, not claims about future edited line numbers; bind actual observed symbols/locations in evidence, never invent them.

## Design checklist

SOLID/DRY — extend existing responsibilities and reuse composite machinery; extract abstraction only for a demonstrated second consumer/observable seam. KISS/type design — minimize concepts; retain public types/signatures and existing valid-key/arity rules. Null/default — do not confuse a legitimate default-valued key with absence of a LEFT-joined child. Execution correctness — preserve occurrence semantics, alias uniqueness, paging scope, declared-only metadata. Perf anti-patterns — review new per-row allocations, boxing, repeated metadata/reflection, partial-key scans, extra queries; no optimization claim without measurement. Cache invariant — never mutate a shared QueryCommand's sticky cache state for an individual execution; any no-cache preparation stays call-local. Build discipline — .NET 10, TreatWarningsAsErrors, CRLF, CPM; no incidental public rename, commit, push, or merge authorization. Keep metadata / expression-generation / assembly responsibilities; no new runtime interface/type without a named 2nd consumer; new leaf types sealed; existing sealing changes need impact evidence. Nothing leaves milestone **1.0.9**.

## Test strategy

Core/in-memory tests for key semantics, identity, occurrence handling, defaults, repeated execution; provider SQL tests for conjunction/projection/alias/guard behavior; real integration for materialization, execution counts, provider-specific behavior. Coverage uses filtered production scope incl. `nextorm.{core,sqlite,postgres,sqlserver}`; require BOTH line >=85% AND branch >=75%. Provider functionality outside the coverage filter still requires its acceptance tests. Run the complete core project and six SQL projects, not only new tests; focused red/green via `--filter "FullyQualifiedName~JoinInto"`. SQL tests own complete predicates, aliases and component partitioning (`tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests`). Integrations own actual joins, result assembly and observed query count (`tests/nextorm.integration.tests/CommonTestSuite.JoinInto.cs` for its existing 4 providers; focused wrappers for ClickHouse and MariaDB).

### Variant closure (all rows closed; deferred rows: NONE)

- declared/local/explicit-key, 1:N/1:1, scalar/composite -> test; malformed counts/missing metadata -> guard+negative test;
- mapped identity, alternate principal key, ordering-type variations -> test, incompatible metadata -> guard+negative test;
- M:N parent-composite/child-composite/both-composite + scalar control -> test; standalone and SingleQuery -> test; existing split rejection -> guard+negative test;
- value/reference components, default/nullable values, absent LEFT child -> test;
- empty/one/multiple children, duplicate occurrences, same-prefix/different-suffix keys -> test;
- multiple collections/specs, filters, paging, non-list terminals, sync/async, repeated cache execution -> test;
- missing junction, undeclared relation, invalid selector/equality, keyless parent, arity>8 -> guard+negative test;
- six SQL-gen dialects and six actual integration providers -> separate provider evidence each, no skips/substitutions.

## Priority matrix

P1 by construction — AC01–AC06 execution invariants + negative cases; full-key projection/identity/matching/junction partitioning/dispatcher paths at D02/D03 sites; cache-state isolation, query counts, alias uniqueness, paging preservation, nullable/default/LEFT-child handling; mandatory AC07/AC08 acceptance obligations (build, coverage, documentation correctness, line endings, performance evidence). CHECK applies these priorities without downgrading required rows.

## Docs plan (EN + RU)

Update `docs/advanced/{relationships,limitations,eager-loading}.md`; `docs/guide/{02-joins,29-implicit-navigation}.md`; exact `docs/ru/` counterparts; `docs/specs/roadmap/{todo_eager_loading_pipeline,todo_navigation_properties}.md`; sweep `docs/specs/comparison/{capability-matrix,linq2db-comparison,linq2db-backlog-gap-analysis}.md` and `docs/specs/roadmap/sql-capabilities-gap-analysis.md`. Keep historical conclusions dated + add superseding notes; no public links to internal specs; not touched: public API names, guide page locations/numbering, generated `docs/api/` or `docs/_site/`, unrelated feature docs.

## Performance-measurement decision

**Required.** Change affects per-row assembly and identity/junction processing (`JoinIntoStitcher.cs:103-215`, `JoinIntoManyToManySpec.cs:221-339`), not merely one-time setup. Run before and after: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`. Add and run planned in-memory JoinInto benchmarks. Record source revision, harness revision, environment, dataset/scenario, sample distribution, timing, allocations. Compare supported equivalent scenarios with the same harness/inputs. New capability without an executable old counterpart must be identified as such, not given a fabricated baseline. Investigate reproducible >=20% regressions; smaller deltas alone support no performance claim.

## Reconnaissance decision

No additional design spike or repeated reconnaissance. Runtime tests and benchmarks remain required evidence; "reconnaissance resolved" does not imply they passed. Composite machinery already exists (`JoinIntoKey`, `JoinIntoIdentity.KeyMembers[]`, composite-capable identity-selector cache); only the single-component sites need generalizing; MariaDB/ClickHouse harnesses exist (container + focused tests). If key representation still proves uncertain during DO, authorize only a bounded harness-only experiment — not a speculative new runtime abstraction.

## Unit execution mode

**Sequential, one tree.** D02/D03 overlap `EntityBuilder`, JoinInto specs and the stitcher; tests/docs share sibling contracts. No worktrees justified or created; no commits/merges beyond the status-file checkpoint.

## Dependencies / prerequisites

- **#209** precedes T213 only if its dispatcher change lands first — consume a stable strategy-dispatch seam preserving public surface, declared-only metadata, query-count controls; otherwise implement against supplied base and hand off same contract.
- **#211/#212** serialize overlapping implementation/test/spec writes; preserve their paging scope, identity/alias uniqueness, nested-level behavior (T213 does not add those features).
- **#201/#53** preserve acceptance-benchmark compatibility and public-API freeze.
- Load the repository integration-running skill before container execution; follow its Podman recovery procedure if the socket is missing. Keep all work in milestone 1.0.9. No functional dependency on #20.

## Risks / assumptions ("What the statement did not say")

- Earlier planner availability failure = resolved resource interruption (no content-plan change). No unresolved design/content blocker remains.
- Composite collision/occurrence semantics = explicit negative tests AC01–AC03, complete-key matching; default/null vs absent child = explicit tests.
- Scalar/cache/paging/alias regression = controls + repeated-execution/query-count evidence AC02/AC04.
- MariaDB-vs-MySQL = separate actual-provider identity/execution evidence; provider availability = execution risk, not an acceptance waiver.
- Sibling footprints not yet available = sequential execution, no grouping until known; new composite cases lack old executable counterpart = record limitation, retain comparable supported baselines.
- Existing unrelated restrictions = preserve and test; final test symbols/edited line locations = bind actual observed, never invent.
- (a) Evidence: dispatcher exclusion = `EntityBuilder.cs:1235-1241`; existing two-join + assembly = `JoinIntoManyToManySpec.cs:398/448/291-332`; metadata-only = `IRelationshipMetadata.cs:6-8`; composite machinery = `JoinIntoSpec.cs:652-708`, `:745`; MariaDB container = `tests/nextorm.integration.tests/Providers/MariaDbContainer.cs:17`.
- (b) Assumptions/risks: "under single-query" means dispatcher composition (not merely standalone joins); no implicit maximum composite width; null-FK handling must agree across SQL/in-memory; junction multiplicity follows existing behavior.
- (c) Gaps: none remaining from reconnaissance. Persistent low confidence would go to `escalate` trigger 5.

## Frozen evidence contract (DO→CHECK; rv=1)

Manifest path: `artifacts/pdca/T213/rv1/manifest.json`; validate `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T213/rv1/manifest.json` (require exit 0, no validation failures, complete row->artifact linkage). Preserve complete existing rv=1 row records incl. frozen command/invocation bindings and artifact paths; all uncollected sources are planned, not passing evidence.

Common slots for every row: requirement IDs = listed AC IDs; row ID = E ID, revision rv=1; applicability predicate = "T213 implements the accepted scope" (true for all rows, none conditionally optional); evidence kinds = recorded invocation+exit status, result/log, row-specific artifacts; sources = planned sources later bound to observed revisions/locations; owner = DO executor/coder collects, CHECK independently verifies; artifact root `artifacts/pdca/T213/rv1/`; a missing report is missing evidence, not grounds to change rv or start another iteration.

Required rows:
- E-BUILD (AC07): `dotnet build nextorm.slnx -c Debug`; exit 0, 0 warnings/0 errors; full build log + exit record.
- E-CORE (AC01-AC04,06): `dotnet test tests/nextorm.core.tests -c Debug`; exit 0; required cases executed, no required-case skips; report/log + actual case mapping.
- E-SQL-sqlite (AC01,03,06): `dotnet test tests/nextorm.sqlite.tests -c Debug`; exit 0; required cases pass.
- E-SQL-postgres (AC01,03,06): `dotnet test tests/nextorm.postgres.tests -c Debug`.
- E-SQL-sqlserver (AC01,03,06): `dotnet test tests/nextorm.sqlserver.tests -c Debug`.
- E-SQL-mysql (AC01,03,06): `dotnet test tests/nextorm.mysql.tests -c Debug`.
- E-SQL-mariadb (AC01,03,06): `dotnet test tests/nextorm.mariadb.tests -c Debug`; separate report/log.
- E-SQL-clickhouse (AC01,03,06): `dotnet test tests/nextorm.clickhouse.tests -c Debug`.
- E-INT-sqlite (AC01-AC05): integration invocation below; exit 0; required SQLite scenarios execute without skips; provider identity + query-count evidence.
- E-INT-postgres (AC01-AC05): same invocation/result, specifically PostgreSQL.
- E-INT-sqlserver (AC01-AC05): same, specifically SQL Server.
- E-INT-mysql (AC01-AC05): same, specifically MySQL.
- E-INT-mariadb (AC01-AC05): same, specifically MariaDB; MySQL is not a substitute; separate MariaDB identity/results/query counts.
- E-INT-clickhouse (AC01-AC05): same, specifically ClickHouse; common suite + focused adapter + `ClickHouseJoinIntoOneToOneTests.cs`; identity/results/query counts.
- E-RED (AC01,03): demonstrated regressions before fix; frozen red-stage invocations; nonzero result attributable to the new composite/junction regressions incl. SingleQuery; unrelated controls pass; pre-implementation source revision; red logs + case mapping.
- E-PERF-BASE (AC08): exact acceptance command above + frozen in-memory baseline invocation; successful measurements, not merely successful launch; base/harness revisions; raw output, environment, allocations, distributions.
- E-PERF-FINAL (AC08): same acceptance command + frozen final in-memory invocation; successful measurements + >=20% regression investigation where applicable; final revision; raw output, comparison, investigation record.
- E-COVERAGE (AC07): preserve frozen CI-derived `dotnet-coverage collect` -> `reportgenerator` invocations; tool exits 0; line >=85% and branch >=75%; `coverage.settings.xml` + CI config; raw coverage, generated summary, threshold evidence.
- E-DOCS (AC07): preserve frozen documentation-review/hygiene invocations; successful result with no stale claims, public-to-spec links, or changed-file line-ending defects; listed EN+RU/internal docs; review findings, diff/hygiene, CRLF evidence.

Integration invocation: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`. A shared suite run may supply multiple integration rows ONLY with separate provider-specific evidence; six row IDs remain mandatory; a zero exit with required-provider skips is not a passing result.

CHECK re-gather budget: at most two targeted read-only rounds, owned by CHECK, for missing/ambiguous evidence; budget exhaustion does not turn absence into success; unresolved evidence proceeds through existing escalation rules; no silent contract revision.

## Reconnaissance (resolved) — facts

- Composite machinery exists: `JoinIntoSpec.cs:745` `JoinIntoKey : IEquatable` stores `object?[] _values` (`:747`), `Equals :754-766`, hash `:772-779`; `JoinIntoIdentity` (`:12`, sealed) holds `string[] KeyMembers :27` + `string[] JunctionKeyMembers :41`; `BuildIdentitySelector(Core) :652-708` + `IdentitySelectorCache :664` already handle composite (`keys.Count==1`→boxed `:691-694`, `>1`→`JoinIntoKey :695-705`; test `JoinIntoIdentitySelectorCacheTests.cs:85-109`). `JoinIntoStitcher.cs:106` parent dedup keys on the entity's own selector (`:176`) — composite-aware.
- Single-component sites to generalize: `JoinIntoSpec.cs:215-216`,`:464-465`,`:253-254` (OccurrenceScope); `JoinIntoManyToManySpec.cs:147-148`,`:160-163`,`:207`,`:410-411`; `JunctionLinkSourceFactory.cs:107` guard, `:112-113` `[0]`, partition `:129-131`. Explicit-key overload `JoinIntoSpec.cs:319` stays.
- Dispatcher: guard `EntityBuilder.cs:1235-1241` inside `BuildSingleQueryJoins()` (`:1227-1302`); `_joinIntos` filled `AddJoinInto :1075`; `IsManyToMany` `JoinIntoManyToManySpec.cs:275`; two slots `GetItemTypes(bool) :245-246` vs default `JoinIntoSpec.cs:141`, summed `JoinIntoStitcher.cs:26-51`, materialized `EntityBuilder.cs:1349-1357`,`:1370`; in-memory edges `:1589`/`:1630` (`:1594-1598`,`:1647-1651`).
- Providers: SQL-gen for SQLite/Postgres/SqlServer/MySQL/ClickHouse; MariaDB `tests/nextorm.mariadb.tests` has zero JoinInto tests (project exists). Integration containers all exist incl. `Providers/MariaDbContainer.cs:17` (image `mariadb:11.4`, env `NEXTORM_MARIADB_CONNECTION`); `CommonTestSuite` derived = SQLite/Postgres/SqlServer/MySQL only; ClickHouse has `ClickHouseJoinIntoOneToOneTests.cs:14 : ProviderTestSuite`; MariaDB has ~10 standalone `IDisposable` integration classes. Integration cmd `AGENTS.md:60-61`; skipped≠green `AGENTS.md:65`.
- Evidence-contract definition: `~/.config/opencode/skills/pdca-dotnet/SKILL.md:919-979` + `:1147-1168`; validator `scripts/validate_inner_loop.py` (`:586`,`:495-562`,`:554-561`, exit 2 `:606-610`); `artifacts/pdca/<task>/rv<k>/` is an existing convention; coverage scope `coverage.settings.xml:11-13`; thresholds `.github/workflows/dotnet.yml:27-28`.

## Acknowledgement — T209 rv1 shared contract

- Contract revision: `rv=1` — `artifacts/pdca/D209/rv1/sibling-contract.md`.
- This sibling keeps its own plan revision: **r=1** (unchanged).
- Compatibility verdict: **compatible; obligation noted** — #213 preserves the fixed-Single `JoinInto` behavior T209 freezes (R209-06), must extend plan-key identity for any admitted composite/junction shape, and must never mutate the shared `QueryCommand.Cache`.

## Done / Verified / Incomplete

- Done: reconnaissance; PLAN pass 1 (planner draft, all gate-1 sections present); PLAN r=1 finalized as ready (planner, medium tier).
- Done: D00 — status persistence/r=1 readiness retained; DO started (r=1).
- Verified: —
- Incomplete: —

## Progress log

- DO started (r=1); plan applicability verification dispatched.
- 2026-10-12T13:44:19Z | DO | r=1 | n=1/3 | D01: red composite/M:N-under-SingleQuery regressions + scalar controls authored across core, all six dialect SQL-gen projects and integration adapters; red subset captured (7 project runs, exit 2 each, all attributable to the targeted guards); unmodified acceptance baseline (7/7) + new in-memory JoinInto harness recorded; Debug build 0 warnings/0 errors | artifacts/pdca/T213/rv1/
- 2026-10-12T13:57:03Z | DO | r=1 | n=1/3 | D02/D03: composite relationship keys (1:N/1:1: composite principal/foreign key selectors via JoinIntoKey) + composite junction in-memory edges generalized; SingleQuery M:N JoinInto dispatcher rejection lifted (link+child edge pairing); obsolete composite/A composite-junction/M:N-SingleQuery rejection tests converted to positive; Debug build 0 warnings/0 errors; filtered FullyQualifiedName~JoinInto green in core+6 dialects (230 selected, 0 failed) | artifacts/pdca/T213/rv1/D02D03-inner-loop.json
- 2026-10-12T14:10:16Z | DO | r=1 | n=1/3 | D02-P1 bounded carrier spike FAILED → STOP (route to planner, no new carrier invented): Projection<TJunction,long> link item does NOT flow — TryExpandEntityItem has no metadata for the projection type so TryBuildProjectionSelectList falls back to the parent entity columns and RowMaterializerBuilder throws ArgumentException "'Id' is not a member of Projection<...>" at RowMaterializerBuilder.cs:468; separately a MemberInit entity-typed Item1 is rejected by RowMapperFactory ("Property 'Item1' ... type SpikeCarrierJunction which is not supported") at RowMaterializerBuilder.cs:465. No production source changed; spike harness archived and removed from the test tree | artifacts/pdca/T213/rv1/spike/RESULT.md, spike2.log
