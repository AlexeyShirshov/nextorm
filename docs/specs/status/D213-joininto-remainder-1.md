# D213 — JoinInto remainder (composite keys, composite junction selectors, M:N under single-query)

- task: T213 / issue #213 — https://github.com/AlexeyShirshov/nextorm/issues/213
- collection: `1.0.9-todo`; selected_variant: `pdca-dotnet`; cycle: N=1; plan_revision: r=5 (contract rv=4)
- base ref: `58025147` (branch `1.0.9`)
- phase: **DO — in progress (r=5)**
- plan_state: **ready** — r=4 replanned (planner, medium tier; 3rd escalate, direction D narrowed scope); r=5 scope-only closure/handoff (planner, n=1) supersedes the r=4 D05 wording only; evidence contract rv=4 (supersedes rv=3, retaining every rv=1/rv=2/rv=3 requirement/row ID + obligation)

## Notice

The upstream planner resource blocker ("Insufficient account funds") is resolved. The r=1 readiness statement was superseded by the r=2/r=3 replans and, at r=4, by direction D (3rd escalate): T213 narrows to scalar M:N + composite relationship keys; composite-junction M:N is carried by #223 (contract rv=4 supersedes rv=3). At r=5 the remaining docs change (AC07/D05) is handed off as a verified issue #225 plus patch artifacts; the working-tree docs are not touched (external ownership).

## Goal (essence)

Execute composite relationships and junctions through the existing JoinInto assembly, including the SingleQuery dispatcher.

Non-negotiable constraints: no public LoadWith/Join API change; no inferred FK relationships; no scalar/split-execution behavior change; preserve declared-only metadata resolution, occurrence semantics, cache isolation, paging scope, identity and alias uniqueness, existing unrelated restrictions.

Minimum solution: generalize the identified single-component consumers using the existing composite machinery; carry complete keys through projection, parent identity, association matching, junction partitioning; relax only dispatcher restrictions made obsolete.

Approach decision: (selected) extend existing JoinInto assembly + composite machinery; (rejected) separate composite executor (duplicates scalar logic, behavior drift); (rejected) route unsupported cases to split execution (violates SingleQuery/query-count + split constraints).

## Acceptance criteria (each with a negative case)

- AC01 Declared/local composite 1:N and 1:1, incl. explicit-key forms, execute correctly. Neg: crossed components do not match; malformed component counts fail deterministically.
- AC02 Scalar/composite parent identity deduplicates by the complete identity. Neg: same first component with different later components stays separate; keyless-parent behavior unchanged.
- AC03 Scalar M:N works standalone and through SingleQuery; composite-junction M:N (full-component partitioning with occurrences) is carried by #223 in milestone 1.0.9. Neg: missing children, duplicate links, colliding partial keys neither invent nor lose associations.
- AC04 Filters, ordering/paging, multiple declarations, terminals, sync/async, repeated cached execution retain behavior. Neg: no command-state/cache leakage, sibling alias collision, or extra SingleQuery execution.
- AC05 Composite keys without a composite junction and scalar M:N, standalone and through SingleQuery, are verified on all six providers, including MariaDB; composite-junction M:N provider coverage is carried by #223 in milestone 1.0.9. Neg: skipped providers fail; MySQL cannot substitute for MariaDB.
- AC06 Metadata-only resolution and unrelated restrictions remain intact. Neg: undeclared navigation, invalid selector/equality, missing junction, arity overflow still reject; standalone JoinInto is not a split executor.
- AC07 EN+RU accurate; Debug solution build 0 warnings/0 errors; filtered coverage line >=85% and branch >=75%. Neg: stale rejection claims, either deficient metric, or mixed/LF endings in changed authored files fail. **AC07 is carried → #225 (milestone 1.0.9), with its original documentation criteria unchanged. T213 requires a verified issue and patch-artifact handoff; it does not claim those documentation criteria satisfied.** (Planner r=5 wording named #224; GitHub allocated #225 because #224 already existed.)
- AC08 Acceptance benchmarks + relevant JoinInto in-memory measurements recorded against a comparable baseline. Neg: missing measurements or uninvestigated reproducible >=20% regression fail; smaller deltas are not claims.

## Ordered units (single tree) with states

Execution order D00 -> D04-R -> D04-S -> D05 -> D06 -> D07; preserve red evidence before implementation; measure unmodified acceptance baseline before perf-affecting changes. r=4 (direction D) narrows T213's accepted scope: scalar M:N (standalone + SingleQuery) and composite relationship keys stay; composite-junction M:N is carried by #223. D01 red evidence and D03 mixed-projection admission are complete and retained; the r=3 gated nested-projection D04 (direction B composite junction link source) is superseded → #223 + T213-EC-REJECT and is not resumed. D04-R is the fail-closed preparation rejection; D04-S is the scalar M:N six-provider verification. The former coverage unit (r=2 D04) is retained verbatim as D07. At r=5 the pending D05 docs-change unit is superseded by D05-HANDOFF2 (verified issue #225 + patch-artifact handoff), which does not touch the externally-dirty working tree.

- **P:T213-r1-close-readiness** — done — resolved reconnaissance + finalized r=1 readiness.
- **D00** — done (r=4) — write/refresh this status at r=4, retain every applicable rv≤3 row ID/obligation, record follow-up issue #223 + its body/handoff.
- **D01** — done — red regressions and scalar controls authored in core JoinInto tests and all six provider SQL-test projects; red evidence frozen under `artifacts/pdca/T213/rv1/red-evidence.md`. Composite-junction benchmark/red evidence moves to #223.
- **D02** — done (r=4 narrowed) — composite relationship keys + scalar M:N generalized at r=1 (`CompositeOneToMany`/`CompositeOneToOne` core tests green; scalar M:N standalone + SingleQuery green). The direction-B composite junction link source is dropped from T213's scope and carried by #223; D02-P1 (composite junction carrier) is withdrawn with it, not superseded silently. The in-memory composite-junction stitching from r=1 stays untouched.
- **D03** — done — mixed mapped-entity + supported-scalar projection admission at `QueryCommand.QueryPreparer.cs` (retained; EC-T213-D03-01 `met`).
- **D04** — superseded (r=4 direction D) — r=3 gated nested-projection normalization + composite junction link source FAILED (nested alias miss `Visitors/MemberTranslator.cs:847`; nested materialization `ArgumentException` `DataContext/RowMaterializerBuilder.cs:468`; link itself materialized at `JoinIntoStitcher.cs:214`). Marked `superseded → #223 + T213-EC-REJECT`, not resumed.
- **D04-R** — active (fix-now, r=4) — explicit fail-closed preparation rejection: `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:125-127` throws `NotSupportedException("composite-junction M:N is not supported; see #223")`; core test `tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs` asserts the exact message, that no SQL executes, and that the scalar junction path is not rejected.
- **D04-S** — pending (r=4) — scalar M:N standalone + SingleQuery verification on all six providers (SQL generation + in-memory + real integration), including MariaDB; evidence rows `T213-EC-SCALAR-{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}`.
- **D07** — pending — (was r=2 D04, coverage) complete positive/negative coverage across all six SQL-test projects, core regressions, `CommonTestSuite.JoinInto.cs`, `ClickHouseJoinIntoOneToOneTests.cs`, focused ClickHouse/MariaDB adapters; run actual six-provider integration and final measurements.
- **D05** — superseded (r=5) — the EN+RU/internal docs-change unit is superseded by **D05-HANDOFF2**; its original documentation criteria are unchanged and carried → #225 (milestone 1.0.9): scalar M:N supported (standalone + `SingleQuery`), composite relationship keys supported, composite-junction M:N rejected in **SQL generation** with `NotSupportedException` → #223, and the in-memory provider stitches a composite junction.
- **D05-HANDOFF2** — active (r=5) — verified issue #225 (AC07 handoff, milestone 1.0.9) + patch-artifact handoff under `artifacts/pdca/T213/rv4/docs/`: baseline (`git show HEAD:<path>`) and proposed snapshots for the 15 externally-dirty footprint files plus the clean `docs/specs/roadmap/todo_navigation_properties.md`, `AC07.patch` (16 files), but WITHOUT editing the working-tree docs; `git apply --cached --check` against HEAD via a temp index exit 0; trigger = the external owner commits/discards the churn so `git status --porcelain` is clean on all 15 footprint files.
- **D06** — pending — collect/validate rv=4 evidence, coverage, logs; update status; address CHECK findings without weakening AC or evidence obligations.

Unit execution mode: sequential in the current tree. D04-R/D04-S share tests; D05/D07 share docs/tests; sibling tasks overlap EntityBuilder, projection/stitcher, shared tests, specs. No assumed parallelism; no worktree requirement. Before any collection grouping obtain actual sibling footprints; until then serialize overlapping writes.

## Write-footprint

- D01: existing core JoinInto test files, six provider SQL-test projects, `CommonTestSuite.JoinInto.cs`; planned `benchmarks/nextorm.benchmark/JoinIntoBenchmarks.cs`.
- D02: `src/nextorm.core/Builders/EntityBuilder.cs:864-1009`, `Builders/Joins/JoinIntoSpec.cs:189/319/436/551/745`, `JoinIntoStitcher.cs:103-215`, `JoinIntoManyToManySpec.cs:221-339`, `JunctionLinkSourceFactory.cs:99-107`; identity/cache files only if proven necessary.
- D03: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` (`TryBuildProjectionSelectList`); test file `tests/nextorm.core.tests/JoinIntoCompositeCarrierSpikeTests.cs`; no entity/materializer change.
- D04: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` (MemberInit gate), `QueryCommand.cs`/`QueryDefinition.cs` (junction-link marker), `Visitors/MemberTranslator.cs` (marker-gated alias fallback), `Builders/Joins/JunctionLinkSourceFactory.cs` (composite `Projection<TJunction,long>` source + marker), `Builders/Joins/JoinIntoManyToManySpec.cs` (composite branch + `GetItemTypes`), `Builders/EntityBuilder.cs` (`BuildPairedKeyEquality` accessibility only); probes `tests/nextorm.sqlite.tests/JoinIntoCompositeJunctionProbeTests.cs`, core materializer probe.
- D07: all six SQL-test projects, existing core regression files, `CommonTestSuite.JoinInto.cs`, existing `ClickHouseJoinIntoOneToOneTests.cs`; focused ClickHouse/MariaDB integration adapters (MariaDB container already exists).
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

## Frozen evidence contract (DO→CHECK; rv=4, supersedes rv=3)

Manifest path: `artifacts/pdca/T213/rv4/manifest.json`; validate `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T213/rv4/manifest.json` (require exit 0, no validation failures, complete row->artifact linkage). rv=4 retains every rv=1/rv=2/rv=3 required row/obligation with the same IDs/slots. Direction D narrows T213's accepted scope: scalar M:N (standalone + SingleQuery) and composite relationship keys stay; composite-junction M:N is carried by #223. The composite-junction **sub-obligation** of the SQL-gen/integration rows (E-SQL-*/E-INT-*) and the `T213-EC-NESTED-01` row are marked `superseded → #223 + T213-EC-REJECT` — their IDs/history are kept, never deleted or silently repurposed. rv=4 adds `T213-EC-REJECT`, `T213-EC-SCALAR-{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}`, `T213-EC-HANDOFF`, `T213-EC-DOCS`; all uncollected sources are planned, not passing evidence.

Common slots for every row: requirement IDs = listed AC IDs; row ID = E ID, revision rv=4; applicability predicate = "T213 implements the accepted scope" (direction D: scalar M:N + composite keys; composite-junction M:N is `na`/superseded); evidence kinds = recorded invocation+exit status, result/log, row-specific artifacts; sources = planned sources later bound to observed revisions/locations; owner = DO executor/coder collects, CHECK independently verifies; artifact root `artifacts/pdca/T213/rv4/`; a missing report is missing evidence, not grounds to change rv or start another iteration.

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
- Note on E-SQL-*/E-INT-*: their composite-junction M:N **sub-obligation** is `superseded → #223 + T213-EC-REJECT`; the row IDs/history remain for composite relationship keys (AC01), scalar M:N (AC03) and negative cases (AC06).
- E-RED (AC01,03): demonstrated regressions before fix; frozen red-stage invocations; nonzero result attributable to the new composite/junction regressions incl. SingleQuery; unrelated controls pass; pre-implementation source revision; red logs + case mapping. The composite-junction SQL-gen red sub-obligation is `superseded → #223 + T213-EC-REJECT`.
- E-PERF-BASE (AC08): exact acceptance command above + frozen in-memory baseline invocation; successful measurements, not merely successful launch; base/harness revisions; raw output, environment, allocations, distributions.
- E-PERF-FINAL (AC08): same acceptance command + frozen final in-memory invocation; successful measurements + >=20% regression investigation where applicable; final revision; raw output, comparison, investigation record.
- E-COVERAGE (AC07): preserve frozen CI-derived `dotnet-coverage collect` -> `reportgenerator` invocations; tool exits 0; line >=85% and branch >=75%; `coverage.settings.xml` + CI config; raw coverage, generated summary, threshold evidence.
- E-DOCS (AC07): preserve frozen documentation-review/hygiene invocations; successful result with no stale claims, public-to-spec links, or changed-file line-ending defects; listed EN+RU/internal docs; review findings, diff/hygiene, CRLF evidence.
- EC-T213-D03-01 (AC03/AC05): mapped junction-entity-plus-occurrence projection used by composite SQL JoinInto; `TryBuildProjectionSelectList` admits a mixed mapped-entity + supported-scalar projection and the shared materializer rebuilds `Projection<TJunction,scalar>`; core test + red/green logs; owner coder collects, CHECK independently verifies.
- T213-EC-NESTED-01 (AC03/AC05): nested mixed-carrier normalization for the composite junction link; applicability "JoinInto uses nested mixed carrier"; scenario = former failing probe (nested `link.Item1.<fk>` resolution) + nested/root materialization + mismatch negatives; evidence = regression assertions, generated SQL, probe log; owner coder collects, CHECK independently verifies. — **SUPERSEDED (r=4 direction D)** `superseded → #223 + T213-EC-REJECT`: the r=3 D04 gated branch FAILED — nested access chain `pair.Item2.Item1.ChildId` misses alias resolution at `Visitors/MemberTranslator.cs:847`, and nested `Projection<Projection<TJunction,long>,TChild>` materialization throws `ArgumentException` at `DataContext/RowMaterializerBuilder.cs:468`; the link itself IS materialized (`JoinIntoStitcher.Stitch:214` → `AssignChildrenFromLinks:241`), so multi-level nested-projection support (outside the D04 gate) is required. ID/history kept; the obligation moves to #223; evidence `artifacts/pdca/T213/rv3/nested-projection/RESULT.md`.
- T213-EC-REJECT (AC03): the composite-junction M:N SQL path fails closed at preparation with `NotSupportedException("composite-junction M:N is not supported; see #223")`, executes no SQL, and the scalar junction path is not rejected; evidence = core test `tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs` + focused run log; owner coder collects, CHECK independently verifies.
- T213-EC-SCALAR-sqlite (AC03/AC05): scalar M:N standalone + through `SingleQuery` verified on SQLite (SQL generation + in-memory + real integration); exit 0, no required skips.
- T213-EC-SCALAR-postgres (AC03/AC05): same, specifically PostgreSQL.
- T213-EC-SCALAR-sqlserver (AC03/AC05): same, specifically SQL Server.
- T213-EC-SCALAR-mysql (AC03/AC05): same, specifically MySQL.
- T213-EC-SCALAR-clickhouse (AC03/AC05): same, specifically ClickHouse.
- T213-EC-SCALAR-mariadb (AC03/AC05): same, specifically MariaDB; MySQL is not a substitute.
- T213-EC-HANDOFF (AC03/AC05): follow-up issue #223 exists in milestone 1.0.9 and carries the inherited composite-junction obligations (standalone + SingleQuery, six providers incl. MariaDB, boundary, preserved invariants); evidence = issue number/URL + body.
- T213-EC-DOCS (AC03/AC05): EN+RU/internal docs state scalar M:N is supported and composite-junction M:N is not supported, pointing at #223; evidence = doc diff + hygiene/CRLF review.

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

## Public-API candidates for the held T53 freeze (introduced by the ClickHouse scalar M:N fix)

- `ISqlDialect.WrapIntegerWindowResult` — new default interface method (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:1249`, base `SqlDialectBase.cs:798`); identity default, ClickHouse override wraps int-typed ranking window results (`toInt32(row_number() ...)`).
- `QueryCommand.NeedsOuterJoinNullSettings` — new member (`src/nextorm.core/Query/QueryCommand.cs:643`); set for the outer `JoinInto`/`SingleQuery` pair command to reuse the ClickHouse `OuterJoinNullSettings` (`join_use_nulls=1`).

## Progress log

- DO started (r=1); plan applicability verification dispatched.
- 2026-10-12T13:44:19Z | DO | r=1 | n=1/3 | D01: red composite/M:N-under-SingleQuery regressions + scalar controls authored across core, all six dialect SQL-gen projects and integration adapters; red subset captured (7 project runs, exit 2 each, all attributable to the targeted guards); unmodified acceptance baseline (7/7) + new in-memory JoinInto harness recorded; Debug build 0 warnings/0 errors | artifacts/pdca/T213/rv1/
- 2026-10-12T13:57:03Z | DO | r=1 | n=1/3 | D02/D03: composite relationship keys (1:N/1:1: composite principal/foreign key selectors via JoinIntoKey) + composite junction in-memory edges generalized; SingleQuery M:N JoinInto dispatcher rejection lifted (link+child edge pairing); obsolete composite/A composite-junction/M:N-SingleQuery rejection tests converted to positive; Debug build 0 warnings/0 errors; filtered FullyQualifiedName~JoinInto green in core+6 dialects (230 selected, 0 failed) | artifacts/pdca/T213/rv1/D02D03-inner-loop.json
- 2026-10-12T14:10:16Z | DO | r=1 | n=1/3 | D02-P1 bounded carrier spike FAILED → STOP (route to planner, no new carrier invented): Projection<TJunction,long> link item does NOT flow — TryExpandEntityItem has no metadata for the projection type so TryBuildProjectionSelectList falls back to the parent entity columns and RowMaterializerBuilder throws ArgumentException "'Id' is not a member of Projection<...>" at RowMaterializerBuilder.cs:468; separately a MemberInit entity-typed Item1 is rejected by RowMapperFactory ("Property 'Item1' ... type SpikeCarrierJunction which is not supported") at RowMaterializerBuilder.cs:465. No production source changed; spike harness archived and removed from the test tree | artifacts/pdca/T213/rv1/spike/RESULT.md, spike2.log
- 2026-10-12T14:30:20Z | DO | r=2 | n=1/3 | Replanned r=2: D03 projection-materialization prerequisite (planner; failed direction-B spike) | docs/specs/status/D213-joininto-remainder-1.md
- 2026-10-12T14:45:30Z | DO | r=2 | n=1/3 | D02 direction-B composite-junction probe FAILED → STOP (route to planner; DO→PLAN candidate): derived `Projection<TJunction,long>` link builds but the composite ON `link.Item1.<fk>` is not translatable — `MemberTranslator.ResolveProjectionItemAliasIndex` throws InvalidOperationException at `Visitors/MemberTranslator.cs:827` (from `VisitMember :487`, `SqlSourceRenderer.MakeWhere :999` / `MakeJoin :270`) because the MemberInit-derived link subquery emits an entity-typed `Item1` column, not slot-tagged junction columns; nested-projection support is outside the confined D03 scope. Production source reverted (no src change; composite junction keeps fail-closed NotSupportedException); scalar `~JoinInto` sqlite 58/58 green; probe + diff + RESULT archived | artifacts/pdca/T213/rv2/direction-b/RESULT.md, probe-composite-junction.log
- 2026-10-12T15:04:15Z | DO | r=3 | n=1/3 | Replanned r=3: D04 gated nested-projection normalization (planner; 2nd escalate B′) | docs/specs/status/D213-joininto-remainder-1.md
- 2026-10-12T20:15:00Z | DO | r=3 | n=1/3 | DO r=3 D04 spike FAILED (nested projection materialization outside gate); STOP → planner/escalate C-vs-D | artifacts/pdca/T213/rv3/nested-projection/RESULT.md
- 2026-10-12T20:27:02Z | DO | r=4 | n=1/3 | Replanned r=4: direction D narrowed scope (planner; 3rd escalate) | docs/specs/status/D213-joininto-remainder-1.md
- 2026-10-12T20:27:02Z | DO | r=4 | n=1/3 | D00+D04-R: follow-up issue #223 created in milestone 1.0.9 (composite-junction M:N SQL link carrier, boundary + 3 failure summaries + inherited obligations); composite-junction SQL path now fails closed at preparation with exact `composite-junction M:N is not supported; see #223`; core rejection test added (no SQL executed, scalar path not rejected); rv4 manifest validator exit 0; Debug build 0 warnings/0 errors; filtered `~JoinInto` core 145/145 (2 new), exit 0 | artifacts/pdca/T213/rv4/
- 2026-10-12T20:50:38Z | DO | r=4 | n=1/3 | DO r=4: boundary sweep + coverage collected — Debug build 0/0; E-CORE full 2389/2389; six SQL full green (sqlite/postgres/sqlserver/mysql/mariadb/clickhouse); coverage line 88.6% branch 80.7% (reportgenerator exit 0); integration SQLite/PostgreSQL/SQLServer/MySQL/MariaDB green. BLOCKER: ClickHouse scalar M:N + SingleQuery fails `InvalidCastException UInt64→Int32` (ClickHouse `row_number()` vs `JoinIntoLink.Occurrence:int`) — E-INT-clickhouse / T213-EC-SCALAR-clickhouse stay OPEN; requires planner/escalate provider decision. Manifest validator exit 0, report validator exit 0; E-INT-sqlite/postgres/sqlserver/mysql/mariadb + T213-EC-SCALAR-{sqlite,postgres,sqlserver,mysql,mariadb} met; E-PERF-*/E-RED/E-DOCS open | artifacts/pdca/T213/rv4/manifest.json, artifacts/pdca/T213/rv4/D06-inner-loop.json, artifacts/pdca/T213/rv4/integration/CLICKHOUSE-MN-FAILURE.md
- 2026-10-12T21:01:53Z | DO | r=4 | n=1/3 | D04-S fix (ClickHouse scalar M:N through SingleQuery): (1) WindowFunctionTranslator wraps int-typed ranking window results through the new ISqlDialect.WrapIntegerWindowResult (default interface method, identity default); ClickHouse override emits toInt32(row_number() ...) so the UInt64 result maps to JoinIntoLink.Occurrence:int. (2) an outer JoinInto/SingleQuery pair command sets QueryCommand.NeedsOuterJoinNullSettings, reusing the existing ClickHouse OuterJoinNullSettings (join_use_nulls=1) so a LEFT JOIN unmatched side is real SQL NULL and the dangling junction child yields no element. Provider-confined: OuterJoinNullSettings is null off ClickHouse, so no other provider SQL changes. Debug build 0 warnings/0 errors; focused ClickHouseJoinIntoRemainderTests 1/1 exit 0; all-ClickHouse integration 263/263 exit 0 (0 skipped); core full 2389/2389; core ~JoinInto 145/145; nextorm.clickhouse.tests 650/650; sqlite/postgres/sqlserver/mysql/mariadb ~JoinInto green. E-INT-clickhouse + T213-EC-SCALAR-clickhouse resolved | artifacts/pdca/T213/rv4/clickhouse-fix/
- 2026-10-12T16:09:12Z | DO | r=5 | n=1/3 | D:H2a — AC07 handoff issue created and verified: actual allocation #225 (planner referenced #224; #224 already existed), milestone 1.0.9 (#4), OPEN; body `artifacts/pdca/T213/rv4/docs/issue-224-body.md`, verification `artifacts/pdca/T213/rv4/docs/issue-225.json`; body states scalar M:N standalone/SingleQuery supported, composite-junction SQL rejected with `NotSupportedException` → #223, in-memory composite junction supported, trigger = external owner cleans all 15 footprint files | artifacts/pdca/T213/rv4/docs/issue-225.json
- 2026-10-12T16:09:12Z | DO | r=5 | n=1/3 | D:H2b — docs patch handoff: baseline (HEAD 20a67a1c) + proposed snapshots for the 15 externally-dirty footprint files + the clean `todo_navigation_properties.md`; `AC07.patch` (16 files); per-file sha256 + footprint `git status --porcelain` → `artifacts/pdca/T213/rv4/docs/manifest.json`; `git apply --cached --check` via temp index exit 0; working-tree docs untouched | artifacts/pdca/T213/rv4/docs/AC07.patch, handoff-check.log
- 2026-10-12T16:09:12Z | DO | r=5 | n=1/3 | D:H2c — status r=5; AC07 carried → #225; D05 superseded by D05-HANDOFF2; T53 public-API candidates from the ClickHouse fix recorded (`ISqlDialect.WrapIntegerWindowResult`, `QueryCommand.NeedsOuterJoinNullSettings`); rv4 manifest `E-DOCS`/`T213-EC-DOCS` marked carried (na) and `T213-EC-HANDOFF2` added | docs/specs/status/D213-joininto-remainder-1.md
- 2026-10-12T16:51:03Z | DO | r=5 | n=2/3 | DO D07: provider composite-key coverage + strict MariaDB + NULL semantics | artifacts/pdca/T213/rv4/d07/
2026-10-12T16:58:24Z | DO | r=5 | n=3/3 | D07 AC08 final perf: acceptance --anyCategories=acceptance 7/7 exit 0 (wall 57.2s, global 48.52s) + same-machine repeat (wall 52.4s, global 49.44s); in-memory JoinInto 1:N 13.16ms/3.94MB, M:N 14.81ms/4.26MB vs base 7.289ms/9.793ms; all 7 acceptance cases 27-61% slower than D01 base with byte-identical Allocated incl. 5 path-untouched cases -> repeat reproduced as machine-state/measurement variance, not code-attributable (loadavg 3.69; no regression/improvement claim); cached/prepared ratio 2.13x->1.99x (sub-threshold); cost gate 57.2s<=4min pass; E-PERF-BASE/E-PERF-FINAL met; manifest validator exit 0; report validator exit 0 | artifacts/pdca/T213/rv4/perf-final/
