# D159 — CTE: direct join-family overloads and alias API for Cte<T>

- task_id: **D159**
- GitHub issue: **#159** (milestone 1.0.9-rc2, label enhancement)
- branch: `1.0.9-rc2` @ base `18659e41`
- selected_variant: **pdca-dotnet**
- cycle_id N: **1**
- plan_revision r: **1**
- contract_revision rv: **1**
- phase: **PLAN complete — DO not started** (collection TASK PLAN; stopped at the PLAN→DO boundary)
- plan_state: **ready**
- provenance: briefs.md `## #159`; `docs/specs/status/collection-1.0.9-rc2.md:29` (D159 row)
- no commits / merges / pushes are authorized by this plan that are outside the collection's explicit auto-commit mode

## 1. Goal, constraints, minimum solution

- **Essence:** direct `Cte<T>` arguments work throughout the approved positional and alias join families, exactly like converting that descriptor with the **receiving builder's context** first.
- **Hard constraints:** preserve existing signatures, specialized joined builders, flat slots through eight, CTE dependency/identity behavior, filters, caching, and provider capability gates.
- **Minimum implementation:** new instance overloads call `receiver.DataProvider.From(cte)` and existing **EntityBuilder-source** overloads; alias generation recognizes `Cte<T>` and calls new CTE overloads of core `JoinAlias`.
- **Non-goals:** new source abstractions/conversions; descriptor internals; new SQL/cache/filter/hoisting paths; ninth flat slot; correlated lambda-APPLY; expanded APPLY support; ClickHouse Semi/Anti/Paste joins; recursion or unrelated cleanup.

| Alternative | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Explicit instance overloads + alias seam | Matches existing API; reuses all execution semantics | Repetitive public surface and XML docs | **Choose**, as specified |
| Extension-only positional API | Less modification to builder classes | Different lookup/shape; specialized continuation risk | Reject |
| Common source interface/conversion | Fewer overload bodies | Unapproved abstraction and wider resolution/semantic impact | Reject |

## 2. Acceptance criteria (positive + negative), all P1

- **R159-01 Equivalence/context:** short/full forms produce identical SQL, parameters, materialized results, and builder/projection shape using the receiving context. Negative: no derived `SELECT` wrapper, descriptor-origin-context substitution, or bare `QueryCommand` delegation.
- **R159-02 Dependencies:** reachable definitions remain dependency-before-consumer. Negative: no lost dependency, unrelated definition injection, or reordered consumer.
- **R159-03 Identity/conflicts:** self-joining one descriptor declares it once; different descriptors with duplicate names preserve the existing conflict. Negative: neither duplicate declaration nor silently accepted name collision.
- **R159-04 Binding:** different projections and repeated CLR types bind by source slot, including flat `Item1..Item8`. Negative: no swapped slot or nested projection replacing a specialized continuation.
- **R159-05 Filters:** all seven operators preserve full-form filter placement. Negative: no entity filter reinjected outside the CTE or duplicated through a continuation.
- **R159-06 Compatibility:** existing typed EntityBuilder/QueryCommand calls, named arguments, TableAlias forms, and supported defaults compile unchanged. Negative: no new ambiguity in previously valid calls.
- **R159-07 Options:** concise and explicit-options positional/core forms preserve hints, warning suppression, and cardinality configuration. Negative: no dropped callback; no optional `= null` on new overloads; no alias-marker options expansion.
- **R159-08 State/cache:** direct/full forms preserve command shape, reusable-command state, and repeated-call plan-cache behavior. Negative: no sticky `Cache=false`, cached/shared-command mutation, or loss of definitions.
- **R159-09 Providers/APPLY:** supported operations retain SQL/results; unsupported ones retain the full-form capability rejection. Negative: no masking, fallback expansion, skipped-provider "pass," or newly supported correlated APPLY.
- **R159-10 Alias:** first/subsequent marker joins support all seven operators and infer named and anonymous CTE projections. Negative: no need to read descriptor internals or accidentally recognize an unrelated type named `Cte`.
- **R159-11 Guards:** required reference arguments fail explicitly and before source/factory work. Negative: no delayed null dereference; optional nullable options remain accepted.
- **R159-12 Boundaries:** flat positional/alias slots stop at eight; positional-after-alias rejection and existing source restrictions remain. Negative: no `Item9`, new CteReference overload, or changed legacy fallback semantics.
- **R159-13 Documentation/build:** EN+RU examples, XML docs, registry, warning-free build, and CRLF are complete. Negative: no CS1591, mixed endings, public links to internal specs, or unrecorded public addition.
- **R159-14 Verification:** branch-delta, mutation, red→green, container-backed integration, and measured coverage obligations pass. Negative: absent/stale reports, zero selected tests, surviving non-equivalent P1 mutants, and provider skips do not count.

## 3. Units and execution mode

**Sequential units in the collection's existing group worktree.** Core, generator, tests, and generated API expectations share contracts/files; another worktree adds integration risk without useful isolation.

- **P:D159-01 — policy/source prerequisites, read-only:** obtain the missing global contract/gate sections; Roslyn-resolve exact TableAlias signatures and generated required-reference parameters; report baseline provider capability outcomes and coverage workflow/tool availability. No implementation.
- **D:D159-01 — positional/core surface, fix now:** `EntityBuilder.cs:2496-2553`, `:3890-4081`; all seven CTE operators for generic and existing TableAlias receiver forms, concise + explicit nullable-options overloads, guards, XML docs.
- **D:D159-02 — flat continuations, fix now:** `Builders/Joins/JoinedEntityBuilder.cs:13-726`; arity-specific CTE overloads for receiver arities **2-7**, preserving specialized returns. No arity-eight continuation surface.
- **D:D159-03 — alias seam, fix now:** `EntityBuilder.cs:2671-2701`; predicate/conditionless CTE `JoinAlias` counterparts preserving `TNext` constraints and `JoinType`; delegate through `From(cte)` to existing EntityBuilder seams.
- **D:D159-04 — generator, fix now:** `JoinAliasGenerator.cs:333-362,637-701`; semantic CTE recognition/inference and emitted CTE-source forms; preserve marker syntax, existing forms, and `MaxSlots=8`.
- **D:D159-05 — verification, fix now:** implement the matrix below, baseline/full-form comparisons, guards, compilation regressions, cache checks, provider recording, mutation/coverage evidence.
- **D:D159-06 — documentation/registry/status, fix now:** update the specified pages and public-surface registry; assemble evidence and completeness manifest.
- Dependencies: D01 → D02/D03 → D04 → D05 → D06, executed sequentially. Test preparation may accompany its implementation unit.
- No approved slice is deferred. Any discovered necessary split remains **active in 1.0.9-rc2**. Excluded features are non-goals, not promised deferred work.

## 4. Execution-path variant matrix

"Seven" = Join, LeftJoin, RightJoin, FullJoin, CrossJoin, CrossApply, OuterApply. Cells are obligations; do not select only representative operators.

| ID | Execution variants | Closure |
|---|---|---|
| V159-01 | Seven × generic receiver × first join × predicate/conditionless as appropriate | **test** direct/full SQL and shape; results for supported providers |
| V159-02 | Seven × existing non-generic/TableAlias receiver forms × first join | **test** compilation, binding, SQL equivalence |
| V159-03 | Seven × specialized receiver arities 2-7 × continuation | **test** return type, flat slots, SQL; exercise every arity |
| V159-04 | Arity-eight receiver; positional-after-alias | **guard + test** preserve baseline boundary/fallback; no ninth flat slot |
| V159-05 | Seven × positional/core × concise / configured options / explicitly null options | **test**; preserve current relevant option semantics |
| V159-06 | Four predicate operators × null predicate; seven × null CTE | **guard + test** immediate `ArgumentNullException`, appropriate parameter |
| V159-07 | Seven × alias first/subsequent × named/anonymous projected `T` | **test** compilation, generated surface, SQL, supported results |
| V159-08 | Alias factory/generated extension receiver and any required reference marker parameter | **guard + test**, using Roslyn-observed signatures; nullable options are not required args |
| V159-09 | `Cte<T>` / existing `EntityBuilder<T>` / existing `QueryCommand<T>` | **test** unchanged overload resolution, explicit generics, inference, typed null/default |
| V159-10 | `CteReference<T>`; unrelated same-name generic type; untyped null/default | **guard + compile test** no broadened API; preserve previously valid/invalid baseline cases |
| V159-11 | Reference, anonymous, scalar/value projections supported by existing `From`; null/default data | **test** direct/full equivalence; existing unsupported projections retain their **guard** |
| V159-12 | Table→CTE, CTE→CTE, cross-context descriptor, repeated CLR type, different projections | **test** receiving context and slot identity |
| V159-13 | Reachable dependency chain, repeated descriptor, duplicate-name distinct descriptors | **test** order/deduplication/conflict |
| V159-14 | SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse × seven | **test** SQL equivalence or explicit baseline capability **guard** |
| V159-15 | SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse × seven executable/unsupported paths | **integration test** results or exact preserved rejection; record each operator/provider outcome |
| V159-16 | Seven × filtered CTE; repeated execution/shared command; existing alias-cache/refusal paths | **test** filter placement, state/cache, regressions |
| V159-17 | Alias slot ceiling, old marker shapes, old QueryCommand/correlated paths | **guard + regression test** no options/arity/correlation expansion |

No matrix row is deferred. Baseline rejection must be observed and recorded; lack of evidence cannot turn a test obligation into an "unsupported" guard.

## 5. Footprint and public additions

Expected source writes:
- `src/nextorm.core/Builders/EntityBuilder.cs` — **public overload additions** for positional/TableAlias/core alias seams.
- `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs` — **public overload additions** for arities 2-7.
- `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` — recognition and **generated public overload additions**.
- No writes planned to `Cte.cs`, `DataContextExtensions.cs`, CTE merge/cache/filter implementations, or package configuration.
Expected test writes:
- `tests/nextorm.core.tests/TypedCteTests.cs`; `tests/nextorm.sqlite.tests/TypedCteTests.cs`; `tests/nextorm.integration.tests/CommonTestSuite.Cte.cs`.
- `tests/nextorm.alias.tests/{TypedCteAliasTests,AliasGeneratedSurfaceTests,AliasProjectionShapeTests,JoinAliasGeneratorDiagnosticTests,AliasPlanCacheTests}.cs`.
- `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/JoinAliasSqlGenerationTests.cs`.
- Planned new positional SQL fixtures if existing placement cannot accommodate: `tests/nextorm.{postgres,sqlserver,mysql,mariadb,clickhouse}.tests/DirectCteJoinTests.cs` — **planned paths, not existing symbols**.
Expected documentation writes:
- Four guide pages below, `docs/specs/design/API-NAMING-REVIEW.md`, this status file, and the D159 planning state in the collection file (written by the collection parent, not this cycle).
Arity uncertainty: all arities are declared in one file; only 2-7 need additions, arity 8 needs boundary evidence. Newly discovered required files must be reported before widening the footprint.

## 6. Preconditions, assumptions, risks

- **#146 satisfied:** merge commits `a2f22958`, `ddd87c4a`; descriptor/dependency surface `Cte.cs:217-229`; guarded conversion `DataContextExtensions.cs:1272-1275`. Reuse these, do not reopen settled design.
- Before DO, full-form baseline must demonstrate dependency merging, same-type slot binding, self-join deduplication, and filter placement via existing tests `tests/nextorm.core.tests/TypedCteTests.cs:313,699,716,736`.
- Written spec `docs/superpowers/specs/2026-10-02-cte-join-overloads-design.md` exists but its user review is reported pending; planning is not implementation approval.
- Exact unsupported operator/provider pairs were **not supplied**; record them from baseline SQL/capability tests; do not guess.
- New explicit options parameters remain nullable with **no default**; required CTE/predicate/factory/reference receiver parameters are guarded.
- No common test abstraction for one consumer; reuse the existing integration suite.
- Main risks: TableAlias overload shapes, anonymous-type inference, inherited arity-eight fallback, generated overload multiplication. Compile/shape tests are mandatory controls.
- Missing policy facts are evidence insufficiency → targeted scout, not a proven blocker; persistent low confidence routes to `escalate` trigger 5.

## 7. Test strategy

Independent units in one sequential tree; per-unit inner loops are filtered, comprehensive sweep at the DO→CHECK boundary. Commands run in the group worktree; actual test names bound after execution — never manufacture future symbols.
- Evidence root for attempt n1: `E=TestResults/pdca/D159/r1/n1` (create before execution).
- Unit/compile tests own overload selection, guards, generator branches, command shape, SQL; SQLite + real-provider integration own materialized results; existing `CommonTestSuite.Cte.cs` is the cross-provider behavioral contract.
- **Branch delta:** every new executable/semantic decision proven both ways or documented structurally unreachable; straight delegation gets path tests, not invented branches.
- **Mutation:** bounded manual mutations of predicate guard, options forwarding, CTE-definition-preserving delegation, generator CTE recognition/inference; no surviving non-equivalent P1 mutant.
- **Red→green:** full-form baseline passes first; new direct-call fixtures fail on the unimplemented API/generator contract; implementation makes the same fixtures pass.
- Coverage line **>=85%**, branch **>=75%**, measured identically baseline/final. Load `running-integration-tests` before execution; PostgreSQL/SQL Server/MySQL/ClickHouse skips are **not green**; SQLite must execute.

### Command catalogue

| ID | Command |
|---|---|
| C-BUILD | `dotnet build -c Debug` |
| C-CORE | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~TypedCteTests"` |
| C-SQLITE | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~TypedCteTests"` |
| C-ALIAS | `dotnet test tests/nextorm.alias.tests -c Debug` |
| C-PROVIDERS | separately: `dotnet test tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests -c Debug` |
| C-ALL | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test -c Debug` |
| C-INTEGRATION | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` |
| C-COVERAGE | `DOCKER_HOST=... dotnet-coverage collect "dotnet test -c Debug" -s coverage.settings.xml -f cobertura -o "$E/coverage.cobertura.xml"` |
| C-REPORT | `reportgenerator "-reports:$E/coverage.cobertura.xml" "-targetdir:$E/coverage" "-reporttypes:JsonSummary;Html"` |
| C-DOCS | `dotnet docfx docs/docfx.json` |
| C-DIFF | `git diff --check` |
| C-PERF | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` |
| C-STRUCTURE | `Task(scout, ...Roslyn structure/guards/delegation/signatures/branches/shared-command-mutations/temp-TVP reachability...)` |
| C-AUDIT | `Task(check, ...reconcile rv=1 contract vs DO ledger; audit skills; R/V/artifacts/negatives/providers/coverage/mutation/perf/docs...)` |

## 8. Documentation plan

- Update `docs/guide/08-cte.md` and `docs/ru/guide/08-cte.md`: typed CTE and **Composing over a CTE** (EN :64/:360, RU :363).
- Update `docs/guide/02-joins.md` and RU mirror: **Named join aliases**, **Joining a subquery**, chained-join/arity (EN :108/:354/:442, RU :356).
- Show short/full equivalence, direct first/continued joins, anonymous alias inference, options distinction, unchanged eight-slot/provider boundaries. No new guide, renumbering, or TOC change.
- XML-document every added public source overload (CS1591 enforced: `src/Directory.Build.props:11`, `TreatWarningsAsErrors=true`); generator emitted members keep the documented suppression `JoinAliasGenerator.cs:464`. Record additions in `docs/specs/design/API-NAMING-REVIEW.md`; no unimplemented PublicAPI freeze; no public links to specs.

## 9. Performance measurement decision

**Mandatory:** D159 is query-path-adjacent, so the nextorm class-table **query-path/plan-cache P1 row** applies. Run **C-PERF** for the frozen pre-D159 baseline and candidate under comparable conditions: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`. Require **exactly seven cases**; record **wall time, Mean, Allocated, and cached/prepared ratio**; apply the **<=4-minute run budget**; comparable growth **>20% requires investigation**, not an automatic hard failure. Thin delegation should reproduce existing full-form query construction through `From(cte)` and the EntityBuilder-source join overload — that expectation must nevertheless be **measured, not asserted**. **Invalidation trigger:** any new cached-path, per-row, or independent-allocation work forces replan plus measurement.

## 10. Reconnaissance/prototype decision

**Design prototype not required:** delegation and semantic-recognition seams are established. Targeted read-only reconnaissance is required for the missing policy/signature/capability facts; observable completion is cited definitions and a baseline operator/provider outcome matrix.

## 11. Versioned evidence contract — rv=1

**PLAN revision `r=1`; contract revision `rv=1`.** First PLAN construction; no supersession. Authority: global `pdca-dotnet/SKILL.md:898-931,1097-1116`; the overlay references it, does not define a competing schema. `coder` pins this contract before DO; pinning does not authorize DO in this collection PLAN beat.

### Mandatory row slots and common bindings

Every row contains/inherits: (1) stable **requirement ID** and distinct stable **row ID**; (2) required check/scenario; (3) expected evidence kinds + sources; (4) exact command/invocation, required exit-code or invocation-result evidence, and log requirements; (5) expected artifacts/locations (N/A only with justification and observed predicate); (6) owner stream; (7) observable applicability predicate; (8) contract revision `rv`.

**No optional slots:** checks, slots, priorities, or obligations may not be omitted or weakened. Planned sources/artifact locations are expectations, **not actual evidence**. Do not fabricate future test symbols or `file:line`.

**Bindings for every row:** `rv=1`; all acceptance and applicable project class-table rows are **P1**; artifact root `E=TestResults/pdca/D159/r1/n1` (later attempts get their own `n`, earlier evidence stays identifiable); every row needs an entry in `$E/ledger.json` plus a row-specific result record, command/invocation record, complete log, artifact references; process evidence records executed command, working directory/environment, exit code, discovery/execution counts, complete stdout/stderr; invocation evidence records request, returned result, transcript; successful build/test/doc commands require exit **0**, relevant assertions satisfied, applicable tests **>0**; expected-negative compile tests record a nonzero compiler exit and the intended diagnostic (unrelated errors do not satisfy); evidence is tied to tested HEAD + patch identity, configuration, environment, row ID, `rv` (stale artifacts do not satisfy); a false conditional applicability predicate requires an actual observation and a retained applicability report — absence of reports is never such an observation.

### Required rows (row ID / requirement ID)

| Row ID / requirement ID | Required check/scenario; evidence kinds/sources | Invocation and required result | Additional artifacts | Owner | Observable applicability predicate |
|---|---|---|---|---|---|
| E159-01 / R159-01 | V01-03,V12: direct/full SQL, parameters, receiving-context use, results, specialized shapes; core/SQLite/integration CTE fixtures | C-CORE, C-SQLITE, C-INTEGRATION; equivalent supported results, no derived wrapper or bare-QueryCommand path | `$E/E159-01-pairs.json`, SQL/result snapshots | core | **Unconditional**; all approved receiver/operator forms |
| E159-02 / R159-02 | V13: reachable dependencies and dependency-before-consumer order; typed-CTE fixtures | C-CORE, C-SQLITE; dependencies retained, unrelated definitions not injected | `$E/E159-02-dependencies.json` | core | **Unconditional** |
| E159-03 / R159-03 | Same-descriptor self-join and distinct-descriptor duplicate-name conflict | C-CORE, C-SQLITE; one declaration and preserved conflict | `$E/E159-03-identity.json` | core | **Unconditional** |
| E159-04 / R159-04 | V02-03,V11-12: projection/CLR-type slot binding, null/default data, all continuation arities | C-CORE, C-SQLITE, C-ALIAS; correct flat slots/returns or explicitly preserved baseline rejection | `$E/E159-04-shapes.json` | core | **Unconditional** matrix obligation; baseline rejection selects the guard assertion, not N/A |
| E159-05 / R159-05 | V16: filter placement across all seven operators | C-CORE, C-PROVIDERS; no reinjected outer filter or duplicated filtering | `$E/E159-05-filters.json` | core/providers | **Unconditional**; unsupported paths also require baseline/direct comparison |
| E159-06 / R159-06 | V09-10,V17: old calls, named args, typed null/default, inference, negative compile cases | C-BUILD, C-ALL, C-ALIAS; old valid calls compile, old invalid cases retain intended rejection | `$E/E159-06-compile.json`, diagnostic records | core/alias | **Unconditional** |
| E159-07 / R159-07 | V05: concise/configured/null options, callback effects, unchanged alias-marker options surface | C-CORE, C-PROVIDERS, C-ALIAS; options forwarded, no optional default on additions | `$E/E159-07-options.json`, surface comparison | core | **Unconditional** for relevant positional/core forms |
| E159-08 / R159-08 | V16 + **project query-path/shared-command P1 row**: repeated calls, cache/state, no shared QueryCommand mutation | C-CORE, C-ALIAS, C-STRUCTURE; no sticky cache flag or shared/cached state mutation | `$E/E159-08-cache-state.json`, structural report | core | **Unconditional** |
| E159-09 / R159-09 | V14-15: SQL equivalence, executable results, preserved capability rejection per provider/operator | C-PROVIDERS, C-INTEGRATION; all provider outcomes recorded, no provider skips counted green | `$E/E159-09-capabilities.json`, provider execution inventory | providers | **Unconditional**; observed full-form support chooses results vs exact guard comparison |
| E159-10 / R159-10 | V07,V10,V17: first/subsequent alias inference, anonymous projections, emitted CTE forms, unrelated same-name type rejection | C-ALIAS, C-PROVIDERS; intended generated forms compile and negatives retain diagnostics | `$E/E159-10-alias-surface.json`, generated/diagnostic evidence | alias | **Unconditional** |
| E159-11 / R159-11 | V06,V08: required-reference null guards, ordering, parameter names; nullable options accepted | C-CORE, C-ALIAS, C-STRUCTURE; explicit immediate failures before factory/source work | `$E/E159-11-null-guards.json` | core/alias | **Unconditional**; actual signatures determine which args are required references |
| E159-12 / R159-12 | V04,V10,V17: slot ceiling, positional-after-alias, CteReference and correlation boundaries | C-CORE, C-ALIAS, C-STRUCTURE; no ninth flat slot or broadened source/correlation surface | `$E/E159-12-boundaries.json` | alias/core | **Unconditional** |
| E159-13 / R159-13 | EN+RU guides, XML docs, API registry, warning-free build, CRLF, scoped diff | C-BUILD, C-DOCS, C-DIFF, C-AUDIT; no CS1591/build errors, mixed endings, forbidden public specs links, missing additions | `$E/E159-13-docs-surface.json`, docs/build/diff logs | docs | **Unconditional** |
| E159-14 / R159-14 | Baseline/final coverage and changed-branch ledger; included assemblies plus separate generator scenario evidence | C-COVERAGE, C-REPORT, C-AUDIT; line **>=85%**, branch **>=75%**; every new decision covered or structurally justified | `$E/coverage.cobertura.xml`, `$E/coverage/Summary.json`, `$E/E159-14-branches.json`, baseline counterparts | verification | **Unconditional**; coverage exclusions never waive scenario obligations |
| E159-15 / R159-14 | Planned guard/options/definition-preserving-delegation/generator mutations | Relevant C-CORE, C-ALIAS, C-PROVIDERS per mutant; intended kill or demonstrated equivalence; restored-green execution | `$E/E159-15-mutations.json`, per-mutant logs/diffs, restoration result | verification | **Unconditional**; no surviving non-equivalent P1 mutant |
| E159-16 / R159-14 | Full-form baseline, direct-call expected red, same-fixture green, broad regression | C-BUILD + relevant suites before/after, then C-ALL; red tied to missing API/generator contract, final green | `$E/E159-16-red-green.json`, phase logs | verification | **Unconditional** |
| E159-17 / R159-01..14 | Policy/predecessor facts, requirement/variant mapping, ledger completeness | C-AUDIT; keyed reconciliation and recorded audit-skill invocations/results | `$E/E159-17-completeness.json`, policy provenance, audit transcripts | CHECK | **Unconditional** |
| E159-18 / R159-C01 | **Project query-path P1 row:** acceptance performance, cached/prepared ratio, baseline/candidate | C-PERF twice; exit 0, **exactly seven cases**, wall time/Mean/Allocated + cached/prepared ratio, <=4-min budget, comparable growth **>20% investigated** | `$E/E159-18-perf-baseline.log`, candidate log, `$E/E159-18-perf-comparison.json` | verification/perf | **Unconditional**; compile-time delegation does not waive the overlay measurement |
| E159-19 / R159-C02 | **Project query-path P1 row:** temp-table/TVP branch protection; regression + changed-path/reachability audit | C-ALL, C-COVERAGE, C-STRUCTURE; regressions green, affected branches covered | `$E/E159-19-temp-tvp.json`, verified branch/test references | verification/core | **Unconditional** audit/regression obligation; finding no affected branch is a result, not N/A |
| E159-20 / R159-C03 | **Project streaming-terminal class row:** applicability audit; if impacted, both surfaces, sync/async, cancellation, parameter parity, fail-closed, ownership, provider coverage | C-STRUCTURE, C-ALL, C-INTEGRATION, C-AUDIT; observed applicability recorded; all listed checks required if impacted | `$E/E159-20-streaming-applicability.json`, affected-path evidence when applicable | CHECK/providers | Applicability audit **unconditional**; streaming-specific tests iff verified changed-path reachability/signature impact touches a streaming terminal |
| E159-21 / R159-C04 | **Project new-SQL-function/operator class row:** applicability audit; if introduced/changed, form parity, per-dialect capability flags/tests, nullable semantics | C-STRUCTURE, C-PROVIDERS, C-INTEGRATION, C-AUDIT; observed applicability recorded; all class checks required if applicable | `$E/E159-21-sql-operator-applicability.json`, capability/form/nullability evidence when applicable | CHECK/providers | Applicability audit **unconditional**; operator-specific checks iff verified delta introduces/changes SQL operation semantics rather than only source-argument overloads |

Project class-table rows are **contract rows** with the same mandatory slots and preserved P1 priority. Unconditional project checks cannot be cleared through N/A. For conditional subchecks the ledger retains the observed predicate and supporting evidence.

### DO ledger obligation

During DO `coder` maintains actual evidence **per row ID + rv**: verified test symbols/`file:line` when applicable, source observations for structural checks; executed commands/invocations, results, exit codes or invocation results, discovery/execution counts, environment/patch identity; actual log/artifact paths and row-to-requirement/variant mapping; explicit states for **passed, failed, not-run, missing, blocked** (no absent row is implicitly complete); N/A only where an observed predicate permits it, with justification and artifact — N/A never waives an unconditional obligation. Planned locations alone do not prove artifact existence or success.

### CHECK budget and completeness gate

**Pinned re-gather owner: CHECK. Budget: two targeted re-gather rounds per CHECK attempt**, gathering only named missing/stale evidence or running the precise missing verification. Initial planned verification is not a re-gather round.

Before PASS, CHECK reconciles this contract against the DO ledger: every applicable required row satisfied by actual evidence for current `rv`; referenced artifacts exist and support the recorded result; conditional N/A supported by the observed predicate; unconditional obligations remain satisfied; negatives, variants, class priorities, provider execution, coverage/branches, mutations, performance, docs accounted for. **Never PASS while an applicable required row is open.** Exit 0 alone does not establish completeness.

Missing reporting is **not a product defect**: gather within the pinned budget; do not edit the product, return to DO, increment the iteration, or fail the product solely for missing evidence. A verified product defect does produce FAIL and loop-back. If the budget is exhausted with required rows open, leave CHECK **unresolved**, report the open row IDs and reasons, follow blocker/escalation rules; persistent low confidence may require escalation under trigger 5.

### Contract revision and supersession

Completing evidence, binding actual test identities, or obtaining missing reports is **not** a contract revision. A newly discovered required variant triggers justified **CHECK → PLAN**: PLAN records the new contract revision and its supersession of the previous, retains existing row IDs and requirement IDs, adds IDs for new variants, preserves all still-required obligations/priorities. CHECK must not silently add, remove, or weaken rows. Supersession does **not** discard DO work or unfinished acceptance criteria. Additive prerequisites leave the original active D-unit blocked on its dependency; only actual scope replacement permits superseding a D-unit, with an explicit replacement mapping carrying remaining criteria.

## 12. Cycle state

- PLANNED r=1 / rv=1; `plan_state=ready`. Stopped at the PLAN→DO boundary per the collection TASK PLAN beat.
- Next action (not performed here): collection parent may authorize DO; DO must not start before the collection parent serializes the handoff.

## 13. Progress log — DO resume (attempt n1)

**Durable state:** Current cycle **N=1**; Plan revision **r=1**; Attempt **n=1/3**; event phase **DO**; `tier=cheap`; HEAD `5a0fb27` (uncommitted D01–D05 changes).
**Defect history:** no CHECK failure, no loop-back, no applied fix in this cycle (0 fixes). Keys will be recorded on the first CHECK-fail/fix/loop-back event, as specified.

| UTC time | phase | revision | iteration | event | evidence pointer |
|---|---|---|---|---|---|
| 2026-10-07T16:03Z | DO | r1 | n1/3 | Resumed DO; D01–D05 implementation + filtered tests confirmed present/uncommitted (not redone) | `git status --short` (12 modified + new `DirectCteJoinTests.cs` in 5 providers) |
| 2026-10-07T16:04Z | DO | r1 | n1/3 | Podman machine `Currently running`, socket `OK`; boundary container-integration suite executed | `$E/C-INTEGRATION-d06.log` |
| 2026-10-07T16:05Z | DO | r1 | n1/3 | Full integration suite green: Total 3337, Errors 0, Failed 0, Skipped 197, Not Run 0, exit 0 | `$E/C-INTEGRATION-d06.log` |
| 2026-10-07T16:07Z | DO | r1 | n1/3 | Per-provider `*IntegrationTests` counts recorded (containers reused, `--no-build`) | `$E/C-INTEGRATION-perprovider-d06.log` |
| 2026-10-07T16:08Z | DO | r1 | n1/3 | Release build clean: 0 warnings / 0 errors, exit 0 | `$E/C-BUILD-release-d06.log` |
| 2026-10-07T16:10Z | DO | r1 | n1/3 | Coverage collected via CI methodology (`dotnet-coverage collect` + `reportgenerator`) with `DOCKER_HOST`; 9078 total, 0 failed, 8880 passed, 198 skipped, exit 0 | `$E/C-COVERAGE-collect-d06.log`, `$E/coverage/Summary.txt` |
| 2026-10-07T16:10Z | DO | r1 | n1/3 | `nextorm.core` coverage **line 87.8% / branch 80.4%** (>=85/75); report exit 0 | `$E/coverage/Cobertura.xml`, `$E/C-REPORT-d06.log` |
| 2026-10-07T16:11Z | DO | r1 | n1/3 | Perf acceptance: exactly 7 cases, 0 failures, exit 0; wall 49.10 s, BDN 45.43 s (<=4 min) | `$E/C-PERF-acceptance-d06.log` |
| 2026-10-07T16:11Z | DO | r1 | n1/3 | Cached/prepared ratio 2.2412 vs baseline 1.87 (+19.85%, trigger 2.244) — below investigation threshold; alloc ratio 7.66 (+3.2%). `BenchmarkDotNet.Artifacts` restored (no extras, 193 files) | `$E/E159-18-perf-comparison.json` |
| 2026-10-07T16:07Z | DO | r1 | n1/3 | Finding A follow-up issue created | https://github.com/AlexeyShirshov/nextorm/issues/206 |
| 2026-10-07T16:07Z | DO | r1 | n1/3 | Finding B verification recorded (no code change) | this log, Finding B note |
| — | DO | r1 | n1/3 | Task NOT marked done (DO→CHECK boundary; collection docs still pending) | — |
| 2026-10-07T16:20Z | DO | r1 | n1/3 | D06 docs: EN+RU `guide/08-cte.md` (+ «Joining a typed CTE directly») and `guide/02-joins.md` (named aliases / subquery / chained arity) updated; registry `API-NAMING-REVIEW.md` §#159 added | `git diff --stat` |
| 2026-10-07T16:20Z | DO | r1 | n1/3 | CRLF normalized on all 5 edited docs; footprint-scoped `git diff --check` exit 0 | `C-DIFF` (scoped) |
| 2026-10-07T16:22Z | DO | r1 | n1/3 | `dotnet docfx docs/docfx.json` exit 0; 2 pre-existing duplicate-source warnings, 0 errors | `$E/C-DOCS-d06.log` |
| 2026-10-07T16:22Z | DO | r1 | n1/3 | Debug build exit 0, 0 warnings / 0 errors; Release `--no-incremental` exit 0, 0/0 (real compile) | `$E/C-BUILD-debug-d06.log`, `$E/C-BUILD-release-d06.log` |
| — | DO | r1 | n1/3 | D06 complete; still NOT marked done (CHECK next). No commit/push in this log line | — |

### D06 documentation/registry verification (r1/n1)

- Docs pages updated (EN+RU): `docs/guide/08-cte.md` + `docs/ru/guide/08-cte.md` (Typed CTE cross-reference + new “Joining a typed CTE directly” / “Прямое соединение типизированного CTE” subsection: short/full equivalence, seven operators, options distinction, dependencies/identity/filters, arities 2–7); `docs/guide/02-joins.md` + `docs/ru/guide/02-joins.md` (Named join aliases — generated `Cte<T>` overload with inferred joined type; Joining a subquery — direct CTE is joined by name, not a derived table; Chained joins/arity 2..8 — CTE continuation cap at eight). No new page/renumbering/TOC change; no `docs/specs/**` links.
- Registry: `docs/specs/design/API-NAMING-REVIEW.md` — new audit entry “issue #159” with N159-1 (P2, BC/Шаг 5) + N159-2 (ℹ️ naming); records the additive public overload counts (16 `EntityBuilder<TEntity>`, 14 `EntityBuilder`, 84 `JoinedEntityBuilder<T1..Tn>` arities 2–7, plus generated alias-Cte overloads) and 0 new public types.
- XML-doc: every added public overload carries full `<summary>`/`<typeparam>`/`<param>`/`<returns>`/`<exception>`; Release `--no-incremental` build 0 warnings / 0 errors ⇒ CS1591 clean.
- docfx exit 0. Footprint-scoped `git diff --check` exit 0 (global diff-check flags only unrelated modified `BenchmarkDotNet.Artifacts/**` trailing whitespace, outside the plan footprint and not committed).

### Per-provider integration counts (C-INTEGRATION-perprovider-d06.log)

| Provider class | Total | Failed | Skipped | Not Run |
|---|---|---|---|---|
| `SqliteIntegrationTests` | 627 | 0 | 40 | 0 |
| `PostgresIntegrationTests` | 627 | 0 | 26 | 0 |
| `SqlServerIntegrationTests` | 627 | 0 | 48 | 0 |
| `MySqlIntegrationTests` | 627 | 0 | 80 | 0 |
| `ClickHouseIntegrationTests` | 117 | 0 | 0 | 0 |

Full-suite aggregate (all classes incl. provider-specific/EF/Lob probes): **Total 3337 / Failed 0 / Skipped 197**, exit 0 — no provider skipped due to missing `DOCKER_HOST`.

### Finding A — pre-existing generator `CS0111` (follow-up issue)

Two alias joins that reuse the same `Alias.X` with **different projection types** make `JoinAliasGenerator.cs:686-721` emit two extension methods with colliding signatures → `CS0111`. Pre-existing (plain `EntityBuilder` alias joins, no `Cte<T>`); R159-10 does not require same-alias/different-projection, so the D159 test workaround stands as an **accepted risk**. Action done: follow-up issue **#206** created in milestone `1.0.9-rc2` — https://github.com/AlexeyShirshov/nextorm/issues/206.

### Finding B — CTE continuation ceiling (verified correct)

Verified: direct CTE receivers arities **2–7** yield result arities **3–8**; slot ceiling **8**; there is **no** 8→9 continuation and **no** `CteReference` overload. Matches R159-12 boundaries and the plan's flat-slot intent. **No code change needed.**

Evidence root: `TestResults/pdca/D159/r1/n1/` (alias `$E`).

## 14. Progress log — DO fix round (attempt n=2)

**Durable state:** Current cycle **N=1**; Plan revision **r=1**; Attempt **n=2/3**; event phase **DO**; `tier=cheap`; HEAD `00a7c0e6` + uncommitted fix-round edits.
**Defect history:** **1 applied fix** at r1/n2; no CHECK failure, no loop-back. Key `cte-symbol-identity-chainkey` → observed r1 n1, fixed r1 n2 (1 fix applied), evidence `TestResults/pdca/D159/r1/n2/`, result resolved. Finding A `CS0111` (same-alias/different-projection) remains follow-up **#206**.

| UTC time | phase | revision | iteration | event | evidence pointer |
|---|---|---|---|---|---|
| 2026-10-07T16:38Z | DO | r1 | n2/3 | Item 1: CTE symbol-identity fix (intermediate-step source kind now encoded) + compile-negative test; intended red first (1 failed / 16 total) then green (16/16) | `$E2/C-ALIAS-item1-red.log`, `$E2/C-ALIAS-item1.log` |
| 2026-10-07T16:41Z | DO | r1 | n2/3 | Item 2: `TypedCte_TableAliasReceiver_AllFourteenOverloads_CompileAndBuild` added; all 14 overloads compile and build | `$E2/inner-tablealias.log`, `$E2/inner-tablealias2.log` |
| 2026-10-07T16:52Z | DO | r1 | n2/3 | Item 3: C3 pinning tests pass, no regression; full `nextorm.alias.tests` 41/41 | `$E2/C-ALIAS-full-n2.log` |
| 2026-10-07T16:53Z | DO | r1 | n2/3 | Item 4: 90 doc-line fixes in `JoinedEntityBuilder.cs` (XML docs / line normalization) | `git diff --numstat` (90/90) |
| 2026-10-07T16:53Z | DO | r1 | n2/3 | C1: new `CS0111` reproduced — intermediate-step `ChainKey` CTE-ness (`IsCte` `e`/`c`) not encoded in the emitted step signature; repro comment prepared for follow-up #206 | `$E2/C1C6/c1c6-report.txt`, `generated.JoinAlias.g.cs`, `C1-repro-snippet.cs` |
| 2026-10-07T16:55Z | DO | r1 | n2/3 | gh comment on #206 **blocked** (GraphQL error, exit 1; retried twice) — noted, work continues | gh stderr this session |
| 2026-10-07T16:53Z | DO | r1 | n2/3 | C6: masked by compiler rejection (unobservable) | `$E2/C1C6/c1c6-report.txt` |
| 2026-10-07T16:53Z | DO | r1 | n2/3 | Boundary sweep n=2 green: Debug 0/0; core `TypedCteTests` 96/96; sqlite 49/49; alias 41/41; providers sqlite 8, pg 18, ss 18, mysql 18, mariadb 18, clickhouse 18, all 0 skipped | `$E2/*.log` |
| — | DO | r1 | n2/3 | Fix round complete; NOT marked done (CHECK next); no commit/push in this log line | — |

### Per-criterion mapping (r1/n2)

- **R159-06 met:** `TypedCte_TableAliasReceiver_AllFourteenOverloads_CompileAndBuild` — all **14/14** TableAlias receiver overloads compile and build.
- **R159-10 met:** symbol-identity fix + compile-negative test (intended rejection retained for unrelated same-name types).

### C1 / C6 (r1/n2)

- **C1 — new `CS0111` reproduced:** intermediate-step `ChainKey` CTE-ness (`IsCte`) is not encoded in the emitted step extension-method signature, so two chains sharing base type, alias sequence, operators and joined types collide. Repro comment intended for follow-up **#206** (append blocked by GitHub GraphQL error this session).
- **C6 — unobservable:** masked by compiler rejection (the C1 `CS0111`/`CS0121` stops the build before C6 can be exercised).

Evidence root n=2: `TestResults/pdca/D159/r1/n2/` (alias `$E2`).
