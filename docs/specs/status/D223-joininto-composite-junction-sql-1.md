# D223 — Composite-junction M:N SQL carrier

| Field | Value |
|---|---|
| Task / issue | T223 / #223 — https://github.com/AlexeyShirshov/nextorm/issues/223 ; milestone 1.0.9 (#4) |
| Prior slice | T213 / #213 — https://github.com/AlexeyShirshov/nextorm/issues/213 ; closed partial r=6/rv5 |
| Cycle / plan / evidence revision | N=1 / r=1 / rv=1; final DO attempt n=2/3; **cycle done** |
| Status file | `docs/specs/status/D223-joininto-composite-junction-sql-1.md` |
| Artifact root | `artifacts/pdca/D223/rv1/` |
| Frozen manifest | `artifacts/pdca/D223/rv1/manifest.json`, task=`D223` |
| Collection | `1.0.9-todo`; single group G1; one tree |
| Plan state | **done** (CHECK PASS r=1; rv=1; n=2/3; no supersession or attempt reset) |
| Execution | sequential, current worktree/branch `1.0.9`; no task/group worktrees or branches; `Builders/Joins/**` exclusive serial ownership |
| DO handoff prerequisite | satisfy T216 collection-level scheduling condition (`collection-1.0.9-todo.md:95`) |

All proposed tests, benchmark workloads, new files, and artifact sources below are **planned**, not existing evidence. T213's failures are inherited design evidence, not D223 CHECK attempts.

## 1. Goal and acceptance criteria

Make junction rows with multi-component parent or child foreign keys travel through the existing SQL JoinInto assembly as materializable internal items. Preserve every key component and junction occurrence through preparation, SQL projection, materialization, and stitching, including the SingleQuery dispatcher. Keep scalar junction behavior, existing in-memory execution, and public APIs unchanged. Do not introduce another executor or fall back to split queries.

| ID | Acceptance criterion | Required negative case |
|---|---|---|
| AC01 | Junction FK shapes 1/2, 2/1, and 2/2 prepare and execute; joins and partition identities use **all** components in metadata order. The design must not impose an undocumented two-component maximum. | Keys sharing their first component but differing in their last component must not cross-link. Reversing declared component order must not silently produce valid-looking wrong results. |
| AC02 | Standalone JoinInto and SingleQuery use the existing SQL assembly and materializable entity-item route; SingleQuery executes one materialization query, not split execution. | A composite junction must neither hit the current rejection nor succeed by silently selecting another executor. |
| AC03 | Junction identity and occurrence scope survive materialization and stitching; repeated pairs, distinct junction occurrences, and multiple relationship scopes remain distinguishable according to inherited semantics. | Same pair in distinct occurrences/scopes must not be lost, merged across scopes, or assigned to another parent. |
| AC04 | SQL generation and database execution are demonstrated for SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, and ClickHouse. | A missing, skipped, or zero-test provider run is not passing evidence. |
| AC05 | Supported nullable/default keys, Left/Inner behavior, and in-memory behavior retain established semantics; unsupported shapes fail during preparation. | Null components must not accidentally match valid components; zero/default valid values must not be treated as null; rejection must occur before connection acquisition. |
| AC06 | Scalar M:N, composite non-junction relationships, identity/cache isolation, and existing stitching remain correct. Public API is unchanged. | Warm-cache execution with another key shape, metadata order, or scope must not reuse an incompatible carrier/plan; no public carrier or new fluent overload may appear. |
| AC07 | Query-path/cache performance is measured against a obtainable baseline, with documented results; required coverage and regression evidence are complete. | Missing baseline, zero selected benchmarks, unexplained scalar-path regression, or incomplete evidence cannot be reported as accepted. |
| AC08 | EN/RU documentation and relevant internal registries accurately distinguish newly supported junctions from remaining limitations. | Neither mirror may continue rejecting the supported path or advertise unsupported behavior. |

**Boundary:** internal link carrier only, on the materializable entity-item path. No public surface change, separate composite executor, split-routing workaround, or reopening of T213's rejected projection carriers.

## 2. Minimal solution and alternatives

**Essential outcome:** preserve scalar FK components and occurrence through the existing assembler.

**Non-negotiable constraints:** AC01–AC08; no public API changes; one executor; existing identity and metadata order; existing cache safety; six providers.

**Minimum within those constraints:** retain the working scalar carrier path and add one internal, flat, materializable carrier shape for composite junctions, plus its mapping into the existing stitch identity. Avoid nested projections and composite objects as SQL member-access targets.

| Approach | Benefit | Cost / risk | Decision |
|---|---|---|---|
| Flat internal entity-item carrier with individually mapped FK components and occurrence | Avoids the three known failed paths; fits the required materialization boundary | Must prove aliasing, entity expansion, registration, and arity-general shape support | **Selected, subject to D01 decisive probe** |
| Extend nested/derived projection translation and materialization | Could reuse attempted carriers | Cross-cutting changes; directly revisits T213's alias/materializer failures | Rejected |
| Separate composite executor or split execution | Isolates implementation | Violates inherited execution boundary | Rejected |

The carrier is a **planned shape**, not an assertion that a suitable shape-generation facility already exists. No specific CLR emission mechanism is prescribed without evidence.

### Concrete consumers and invariant decisions

- Carrier-source construction: `JunctionLinkSourceFactory.BuildSource` (`JunctionLinkSourceFactory.cs:117,125–126`).
- Link metadata/item selection: `JoinIntoManyToManySpec<TEntity,TChild>` (`JoinIntoManyToManySpec.cs:235–250,277–280,403,457`).
- SQL assembly/registration: existing pair-command and dispatcher path (`EntityBuilder.cs:1336,1451,1471–1472`).
- Materialization endpoint: existing entity-item materialization, not derived/nested projection items (`RowMaterializerBuilder.cs:465–468`).
- Identity adapter's consumer: existing stitcher (`JoinIntoStitcher.cs:214–241`) and existing component identity (`JoinIntoSpec.cs:12,27,41,48,816–818`).

This names a consumer for every proposed abstraction. **Concept budget:** one carrier-shape concept and one carrier-to-existing-identity mapping; no carrier executor, provider-specific carrier hierarchy, public wrapper, or duplicate identity model.

Keep scalar `JoinIntoLink` behavior unchanged (`JoinIntoLink.cs:20,23,29`). Do not mutate reusable query-command cache state; preparation-only cache bypass must remain call-local. The global cache invariant is binding even though its detailed implementation locations were not supplied.

T213 terminal causes are mandatory probe inputs:

1. Derived projection materialization failure: `RowMaterializerBuilder.cs:465–468`.
2. Nested FK access translation failure: `MemberTranslator.cs:827`.
3. Nested projection alias failure: `MemberTranslator.cs:847`, followed by materializer failure.

D01 must demonstrate avoidance, not retry those carriers under different names.

## 3. What the statement did not say

| Gap | Closure |
|---|---|
| Full global `pdca-dotnet` versioned-contract definition and nextorm priority/class-obligation registry were not included | **Closed (clarification r=1):** canonical contract slots and the three-class nextorm table were supplied; no concrete missing unconditional obligation. |
| Exact inherited duplicate-pair, occurrence-scope, and nullable-key expected outputs were not supplied | **Explicit assumptions:** distinct junction occurrences remain represented according to established scalar stitching semantics; SQL composite FK equality containing null does not create an association. D01 must reconcile these assumptions with existing tests before freeze. Contradiction returns to PLAN; it does not relax acceptance. Relevant fixtures: `JoinIntoCompositeNullSemanticsTests.cs`, `JoinIntoStitchingRegressionTests.cs`; stitch path `JoinIntoStitcher.cs:214–241`. |
| Supported junction arity is unspecified | **Requirement interpretation:** preserve all declared components, not merely two. Include a >2-component probe. A fixed two-slot carrier is not acceptable without an explicit scope decision. Metadata stores component collections (`JoinIntoSpec.cs:27,41`; `JoinIntoManyToManySpec.cs:235–238`). |
| Suitable materializable flat-shape construction is unproven | **D01 prerequisite experiment.** No production implementation is authorized by an assumed generator or assumed materializer capability. |
| Key CLR/provider type combinations and converter support are not enumerated | **Assumption:** support existing valid mapped key types without broadening the mapping system. D01 establishes concrete numeric, reference/string, nullable, and converted fixtures. Unsupported mapping shapes retain a preparation guard. |
| MariaDB database harness is not described; its dialect project lacks the M:N helper/file | **G02 — execution prerequisite, classified as in-cycle work.** Provide an actual MariaDB execution endpoint and positive fixture; the MySQL run is not MariaDB evidence. Missing helper is in-cycle work, not grounds for excluding MariaDB (`mariadb.tests/JoinIntoCompositeSqlGenerationTests.cs`, scout report). Obtain the endpoint via the standard integration-test skill procedure. |
| T216 external-document ownership and the collection's lane allocation were not supplied | **Closed (clarification r=1):** single-lane sequential ownership structurally satisfies the single-writer requirement; T216 remains a DO-lane scheduling prerequisite (`collection-1.0.9-todo.md:95`), not a plan-design blocker. |
| Verified issue URL is absent from the pack | **Closed (clarification r=1):** verified #223 / #213 URLs and milestone recorded in the header. |
| Exact CI coverage collection/report commands are absent | Planned commands below require preflight validation against the canonical workflow before freeze. Thresholds are known: 85% lines / 75% branches (`.github/workflows/dotnet.yml:27–28`). |
| Performance acceptance limit is unspecified | **Explicit proposed acceptance assumption:** no >5% scalar-path mean-time or allocation regression in a stable matched workload; a noisy crossing receives one repeat before classification. Composite execution has no runnable HEAD baseline because HEAD rejects it (`JunctionLinkSourceFactory.cs:125–126`); report its absolute cost/cardinality scaling, not a fabricated speedup. Confirm this assumption before freeze. |
| Working tree contains unrelated churn | Preserve it. Record starting tracked/untracked state and relevant source digests; do not reset, normalize unrelated files, commit, merge, or overwrite existing benchmark artifacts. |

## 4. Execution-path variant matrix

“Test” means a required assertion, not merely running a project. Dimensions may share data-driven cases, but the manifest must enumerate their actual intersections.

| Axis / variants | Closure |
|---|---|
| Parent/child junction FK arity: 1/1, 1/2, 2/1, 2/2 | **Test:** scalar control plus all composite shapes, each with last-component collision data. |
| Higher arity: 3/1, 1/3, 3/3 | **Test:** arity-general probe and all-component matching; no silent two-column limit. |
| Component order: declared order / deliberately reversed metadata fixture | **Test:** correct mapping order; invalid/incompatible mapping rejected rather than silently cross-linked. |
| Junction occurrences: one / repeated same pair with distinct junction identities | **Test:** inherited multiplicity and identity assertions. |
| Occurrence scope: one JoinInto / two independently scoped relationships sharing entities | **Test:** no scope leakage or cross-stitching. |
| Dispatch: standalone / SingleQuery | **Test:** same graph and component correctness; actual dispatcher and command-count assertion. |
| SQL providers: all six | **Test:** per-provider generation and live execution; skips fail evidence completeness. |
| Composite nullable FK: null first / null last / all null / all non-null | **Test:** approved inherited semantics on SQL and in-memory paths. |
| Default value: zero/default / nondefault | **Test:** valid defaults remain keys, not missing associations. |
| Key representation: value type / mapped reference type / nullable / supported converter | **Test:** numeric and string/reference fixtures, nullable cases, and a supported converted mapping. |
| Unmapped/unbindable component or inconsistent FK metadata | **Guard:** preparation rejection, diagnostic, zero connections. |
| Join type: Left / Inner | **Test:** unmatched parent retention/removal and empty association behavior. Other public join types: **guard or test according to existing eligibility**, established in D01; no new eligibility. |
| Execution: SQL / in-memory | **Test:** graph parity for supported fixtures; existing in-memory path remains unchanged (`EntityBuilder.cs:1658,1716,1757`). |
| Cache: cold / repeated warm / changed shape, order, configuration, scope | **Test:** correct graph and compatible reuse; no sticky cache disabling. |
| Public projection eligibility: materializable entity item / unsupported mixed or nested projection | **Test / guard:** support only the stated entity-item boundary; preserve unrelated rejection behavior (`sqlite.tests/JoinIntoMixedProjectionPublicTests.cs:15`). |

**Deferred functional variants: none.** Other eager-loading features, new public projections, and new key-mapping capabilities are outside this task. A newly discovered supported execution variant becomes an added contract row, not an implicit deferral.

## 5. DO units and execution mode

**Sequential, one group, one tree.** `Builders/Joins/**` is single-writer; carrier, specification, and stitching contracts intersect. Provider tests may run concurrently only after production writes stop, with isolated artifact paths. No worktrees or commits.

| Unit | Work and acceptance | Footprint / disposition |
|---|---|---|
| **D01 — Establish readiness and decisive carrier probe** | Discharge G01–G04 ownership/harness prerequisites; add targeted positive and negative probe cases; demonstrate baseline red at the current composite guard and candidate flat-item green through SQL preparation/materialization/stitching. Establish inherited null/occurrence expectations, >2 arity, benchmark discovery/baseline, and exact coverage invocations. If the carrier cannot satisfy the entity-item route, return to PLAN with evidence. | Existing `JoinIntoCompositeCarrierSpikeTests.cs`, `JoinIntoCompositeJunctionRejectionTests.cs`; junction guard `JunctionLinkSourceFactory.cs:125–126`; failure sites above. **Fix now.** Experimental source changes remain bounded and are not production acceptance. |
| **D02 — Implement the internal carrier and identity mapping** | Replace only composite fail-closed routing with the proven carrier; retain scalar routing, full-component/order semantics, scoped occurrences, preparation guards, and compatible cache identity. | `JunctionLinkSourceFactory.cs:117,224,236`; `JoinIntoManyToManySpec.cs:235–250,277–280,403,457`; `JoinIntoStitcher.cs:214–241`; likely registration touch `EntityBuilder.cs:1471–1472`; planned internal carrier file. **Fix now.** |
| **D03 — Complete execution-path and provider evidence** | Add positive SQL generation to six dialect projects, MariaDB support fixtures/helper, and live composite-junction fixtures covering both dispatchers. Complete null/default/order/occurrence/cache and scalar regression assertions. | Core JoinInto test files; six provider `JoinIntoCompositeSqlGenerationTests.cs`; planned MariaDB M:N fixture; `CommonTestSuite.JoinInto.cs:558,584` and junction integration fixtures. **Fix now.** Existing rejection assertions change only for newly supported shapes. |
| **D04 — Complete performance, docs, registries, and handoff** | Run candidate benchmarks against retained baseline; update authorized EN/RU docs and internal support registries; normalize only touched files to CRLF; verify API and footprint; prepare the completed manifest inputs. | Documentation paths below; planned benchmark workload under `benchmarks/nextorm.benchmark/`; status/artifacts. **Fix now.** |
| **DO→CHECK boundary** | One boundary sweep after D02–D04 are complete. CHECK validates the frozen manifest, performs the defined sweep, and issues the verdict. | No broad sweep after every edit. Focused unit runs remain permitted during DO. |

A new prerequisite discovered during DO leaves its originating D unit **active and blocked**, not superseded. A failed probe is not a completed implementation unit. Initial PLAN classification: no DO-return blocker report exists yet.

## 6. Test strategy and decisive oracle

### Focused units

Selectors are **planned selections**, not claims that future test symbols already exist. Each invocation must report at least one selected test; the manifest records actual discovered symbols after D01.

| Unit | Projects and focused selectors |
|---|---|
| D01 | `nextorm.core.tests`: `FullyQualifiedName~JoinIntoCompositeCarrierSpike`, `FullyQualifiedName~JoinIntoCompositeJunctionRejection`. `nextorm.sqlite.tests`: `FullyQualifiedName~JoinIntoMixedProjectionPublic`. |
| D02 | `nextorm.core.tests`: separate selections for `JoinIntoCompositeRemainder`, `JoinIntoCompositeNullSemantics`, `JoinIntoJunctionKeyOrder`, `JoinIntoIdentitySelectorCache`, `JoinIntoStitchingRegression`, `JoinIntoManyToManySingleQuery`, and the carrier probe. |
| D03 | Each of six dialect projects: `FullyQualifiedName~JoinIntoCompositeSqlGeneration`; M:N selection `FullyQualifiedName~JoinIntoManyToMany` where present or added. Integration: native method selection `*JoinInto*CompositeJunction*`, planned and discovery-validated. Core remaining matrix cases stay in named JoinInto fixtures. |
| D04 | No broad test run. Benchmark comparisons, documentation checks, public-surface verification, and evidence completion. |

**Case names are assertions**, not invented CLR symbols:

- Changing only the last FK component changes the linked child.
- Repeated junction occurrences retain the inherited result multiplicity.
- Two occurrence scopes never exchange links.
- SingleQuery materializes the expected graph in one query.
- A null FK component does not become a valid association.
- Zero/default remains a valid key.
- Reusing a context across carrier shapes preserves correctness and cache eligibility.
- Unsupported projection/mapping rejects before opening a connection.

### Obtainable red↔green and decisive-probe arms

1. Add the same supported composite-junction fixture and positive assertion **before** production carrier changes. On the retained starting source it must fail at `JunctionLinkSourceFactory.cs:125–126`; an unrelated failure does not establish the arm.
2. On the candidate carrier, the unchanged assertion must pass through preparation, SQL execution, materialization, and stitching. Capture commands, SQL, graph/occurrence assertions, and command counts.
3. Use collision data where removing the last FK component would yield a demonstrably wrong graph. A bounded local perturbation omitting that component must turn this assertion red, then restoration must turn it green.
4. Obtain a corresponding scoped-occurrence perturbation arm if occurrence preservation cannot be decisively demonstrated by the fixture itself.
5. Preserve T213 terminal-cause references and document why the successful probe avoids each failure.

Thus the baseline red arm has a known source cause, and a sensitive candidate oracle has a prescribed experiment. **Until these invocations actually select cases and produce the required arms, the manifest is not frozen and the plan is not ready.** An undemonstrable oracle is a GAP.

### Priority matrix

| Priority | Rows |
|---|---|
| **P1 by construction** | AC01–AC07: full-component identity, occurrence/scope, correct dispatcher, materialization, six-provider parity, null/default correctness, fail-before-connect, public API boundary, cache isolation, decisive oracle, query-path performance. |
| **P1 documentation truth** | AC08 support/limitation statements and EN/RU consistency. |
| **P2** | Explanatory wording and nonessential internal narrative cleanup. |

CHECK may not downgrade P1 because a test is inconvenient. The absent canonical class-priority table remains G01.

Coverage includes core/SQLite/PostgreSQL/SQL Server (`coverage.settings.xml:12`). Record both 85% line and 75% branch thresholds. On branch `1.0.9`, the supplied project policy is warning-only; do not falsely present it as the main-branch hard gate or substitute aggregate coverage for P1 assertions.

## 7. Documentation plan

Changes require G03 ownership clearance.

- **EN/RU limitations — change:** `docs/advanced/limitations.md:19` and `docs/ru/advanced/limitations.md:19`. Remove the obsolete blanket rejection of composite relationship/junction keys and M:N SingleQuery. State that mapped composite-junction M:N is supported on the materializable entity-item JoinInto path, including SingleQuery; retain only individually verified unsupported projection/mapping limitations. Do not claim broader projection support.
- **Relationships — change:** correct support statements/examples at `docs/advanced/relationships.md:194,282–283`, with corresponding RU content. Explain full-component matching and preserved junction occurrence semantics without exposing the internal carrier.
- **Guide — change only affected support/example text:** `docs/guide/02-joins.md:209`, plus RU mirror. No page move or renumbering.
- **Internal registries — change:** composite-junction follow-up status in `todo_navigation_properties.md:31–32,80–81,261–263,323`, `sql-capabilities-gap-analysis.md:30,724`, and `todo_eager_loading_pipeline.md:105`.
- Preserve T213's closed partial history (`D213-joininto-remainder-1.md:6,12,14,159,167`); append resolution references rather than rewriting its rejected evidence.
- No generated `docs/api/` or `docs/_site/` edits; no public links to internal specs; unrelated documentation churn is untouched.

## 8. Performance and reconnaissance

**Performance measurement: required.** This changes query preparation, registration, materialization, and stitching—not only one-time setup (`EntityBuilder.cs:1336,1451,1471–1472`; `RowMaterializerBuilder.cs:465–468`; `JoinIntoStitcher.cs:214–241`).

Use BenchmarkDotNet in the existing harness:

- Before production changes: matched scalar M:N cold/warm preparation and execution workloads, with several junction cardinalities.
- Candidate: identical scalar workloads plus composite workloads at the same cardinalities.
- Record time, allocated bytes, runtime/hardware, query counts, input sizes, and carrier/plan reuse observations.
- The current composite rejection is **not** a performance baseline. Do not compare against split execution as an acceptance substitute.
- Isolate artifacts from the pre-existing benchmark-result churn.

**Reconnaissance: required, D01.** Prove that an arity-general flat item, with scalar FK members and occurrence, survives the existing entity-item path and gives a sensitive graph oracle. Harness: existing core carrier-spike fixtures, SQLite execution, existing stitcher, and targeted provider generation. Observable result: baseline guard-red, candidate graph-green, last-component perturbation-red, no T213 alias/materializer failure, and no alternate executor. Failure returns to PLAN before wider implementation.

## 9. Evidence contract — rv=1

### Common execution rules

- Every source below is **planned** until executed and attached.
- Commands run from the workspace root. Preserve process exit codes; use `pipefail` when teeing.
- Logs include exact command, source state/digest, start/end time, exit code, selected/executed/passed/failed/skipped counts, and relevant scenario/provider identities.
- Freeze only after actual discovery and decisive arms are established. Manifest includes these row IDs, requirement IDs, applicability, commands, source digests, evidence/artifact locations, and results.
- **No row becomes inapplicable because a report is missing.**
- `D223/rv1` is not a D213 contract supersession. Initial rv=1 supersedes nothing.

Command aliases below are **exact parameterized invocations**, with parameters restricted to the listed values:

- `F(P,S)` = `dotnet test tests/nextorm.P.tests -c Debug --filter "FullyQualifiedName~S"`
- `SQL(P)` = `F(P,JoinIntoCompositeSqlGeneration)` followed by `F(P,JoinIntoManyToMany)`.
- `LIVE(S)` = `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -method 'S'`

Load the integration-running skill before `LIVE`. Missing socket requires the prescribed machine-start/recheck/retry procedure, not a skipped-suite success. MariaDB endpoint/setup is explicitly G02.

| Requirement / stable row ID | Required scenario; evidence kinds and planned sources | Exact invocation and required result | Expected artifacts | Owner / applicability / revision |
|---|---|---|---|---|
| AC01, AC02 / **D223-EC-ORACLE** | Baseline guard-red, flat-carrier green, omitted-component perturbation-red; T213 terminal-cause comparison. Runtime/test logs, SQL, graph assertions, source digests. | `F(core,JoinIntoCompositeCarrierSpike)` on each retained arm. Red: nonzero with the expected assertion/cause, not build failure. Green: exit 0, selected tests >0. | `probe/{baseline-red,candidate-green,component-red,restored-green}.log`; `probe/RESULT.md`; arm source digests | coder-probe; always; rv=1 |
| AC01, AC03, AC05 / **D223-EC-CORE** | Full matrix: arity/order, collisions, occurrences/scopes, null/default/type, Left/Inner, in-memory parity. Assertion evidence from core fixtures. | `F(core,S)` separately for the D02 selectors above and added matrix selections. Each exit 0, selected >0, no unexpected skips; manifest enumerates assertions/intersections. | `tests/core/*.log`; `tests/variant-matrix.json` | coder-core; always; rv=1 |
| AC02, AC05 / **D223-EC-GUARDS** | Valid composite no longer rejected; unsupported mappings/projections still reject before connection acquisition. | `F(core,JoinIntoCompositeJunctionRejection)`; `F(sqlite,JoinIntoMixedProjectionPublic)`. Exit 0; explicit zero-connection negative assertion. | `tests/guards/*.log` | coder-core; always; rv=1 |
| AC04 / **D223-EC-SQL-SQLITE** | SQLite composite and scalar SQL shape; every component/alias and both dispatchers. | `SQL(sqlite)`; both exit 0, selected >0. | `providers/sqlite/sql.log`, SQL/assertion references | coder-provider; SQL path; rv=1 |
| AC04 / **D223-EC-SQL-POSTGRES** | Same obligations for PostgreSQL. | `SQL(postgres)`; same result requirements. | `providers/postgres/sql.log` | coder-provider; SQL path; rv=1 |
| AC04 / **D223-EC-SQL-SQLSERVER** | Same obligations for SQL Server. | `SQL(sqlserver)`; same result requirements. | `providers/sqlserver/sql.log` | coder-provider; SQL path; rv=1 |
| AC04 / **D223-EC-SQL-MYSQL** | Same obligations for MySQL. | `SQL(mysql)`; same result requirements. | `providers/mysql/sql.log` | coder-provider; SQL path; rv=1 |
| AC04 / **D223-EC-SQL-MARIADB** | Same obligations for MariaDB; planned positive M:N fixture/helper. | `SQL(mariadb)`; same result requirements; missing selector is failure, not N/A. | `providers/mariadb/sql.log` | coder-provider; SQL path; rv=1 |
| AC04 / **D223-EC-SQL-CLICKHOUSE** | Same obligations for ClickHouse. | `SQL(clickhouse)`; same result requirements. | `providers/clickhouse/sql.log` | coder-provider; SQL path; rv=1 |
| AC02–AC05 / **D223-EC-LIVE** | Six named providers execute composite junctions through standalone and SingleQuery; graph, occurrence, null, and query-count assertions. | `LIVE(*JoinInto*CompositeJunction*)`; exit 0; each provider has executed positive cases, no provider skip; G02 must supply actual MariaDB execution configuration. | `integration/composite-junction.log`; six-provider assertion/result map | coder-integration; always; rv=1 |
| AC06 / **D223-EC-CACHE** | Cold/warm/change-shape/config/order/scope execution; scalar and non-junction regression; no sticky cache mutation. Tests plus semantic/diff review. | `F(core,JoinIntoIdentitySelectorCache)`; `F(core,JoinIntoPlanKey)`; `F(core,JoinIntoLocalConfig)`; `F(core,JoinIntoManyToManySingleQuery)`; `F(core,JoinIntoCompositeRemainder)`. Exit 0, selected >0. | `tests/cache/*.log`; `review/cache.md` | coder-core; query-path/cache change—always; rv=1 |
| AC06 / **D223-EC-BOUNDARY** | Internal-only carrier; unchanged public declarations; executor route and bounded footprint. Roslyn-backed review plus diff. | `roslyn` action=`structure`, then action=`members`/`refs` for the discovered changed carrier and impacted public types; record literal invocations after D01 discovery. `git diff --check` must exit 0. Unknown future symbols are not predeclared. | `review/api-and-route.md`; tool outputs; touched-file list | coder-review; always; rv=1 |
| AC07 / **D223-EC-PERF** | Matched scalar baseline/candidate; composite candidate/cardinality scaling; cache reuse. BenchmarkDotNet reports. | Baseline and candidate scalar: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*JoinInto*Scalar*' --artifacts artifacts/pdca/D223/rv1/perf/STATE`. Candidate composite: same command with `--filter '*JoinInto*'`, candidate artifact path. Planned workload discovery must be nonempty; all runs exit 0. | `perf/baseline/`, `perf/candidate/`; `perf/RESULT.md` | coder-perf; query-path/executor change—always; rv=1 |
| AC07 / **D223-EC-BUILD-SWEEP** | Single boundary build and whole affected-project regression sweep. | `dotnet build nextorm.slnx -c Debug`: exit 0, zero warnings/errors. `dotnet test tests/nextorm.P.tests -c Debug` for P=`core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse`: exit 0. `LIVE(*JoinInto*)`: six-provider non-skipped results. | `check/build.log`; `check/projects/*.log`; `check/integration.log` | check; once at DO→CHECK; rv=1 |
| AC07 / **D223-EC-COVERAGE** | Included projects' line/branch coverage and threshold classification. Planned tool/workflow validation in D01. | For P=`core,sqlite,postgres,sqlserver`: `dotnet-coverage collect "dotnet test tests/nextorm.P.tests -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D223/rv1/coverage/P.xml`; then `reportgenerator "-reports:artifacts/pdca/D223/rv1/coverage/*.xml" "-targetdir:artifacts/pdca/D223/rv1/coverage/report" "-reporttypes:Html;TextSummary"`. Tool/test exits 0; report both 85/75 thresholds and branch policy. | `coverage/*.xml`, `coverage/report/`, `coverage/RESULT.md` | check; always; rv=1 |
| AC08 / **D223-EC-DOCS** | Authorized EN/RU support statements, registry updates, no public spec links, preserved numbering. Diff/content review plus DocFX. | `dotnet docfx docs/docfx.json`: exit 0; review invocation recorded as `Task(subagent_type="check", prompt="D223 rv1 AC08: verify paired support statements, registries, links and ownership against the touched diff")`. | `docs/docfx.log`; `docs/RESULT.md`; generated site N/A—ignored output, not committed | coder-docs/check; always; rv=1 |
| AC01–AC08 / **D223-EC-MANIFEST** | Frozen contract complete; actual symbols/commands/evidence linked; applicability and row results checked. | `Task(subagent_type="check", prompt="Validate D223 rv1 artifacts/pdca/D223/rv1/manifest.json against every D223-EC row, source digests, required outcomes and P1 priorities; return explicit PASS/GAP/FAIL per row")`. PASS requires every applicable obligation, not merely manifest existence. | `manifest.json`; `check/contract-verdict.md` | check; always; rv=1 |

`STATE` in the performance command is restricted to `baseline` or `candidate`; retain the source digest for each. The new benchmark selectors are planned and must be validated before freeze.

### Mandatory class applicability

- **Query-path/plan-cache/executor: applies.** Covered by CACHE, PERF, BOUNDARY, LIVE, and sweep rows.
- **SQL-function/operator addition: does not apply as a new function/operator feature.** The chosen design introduces no SQL function/operator. Existing equality/conjunction/component parity nevertheless applies and is required by all six SQL rows. A newly required operator changes this applicability and requires added obligations before implementation.
- **Streaming: not applicable to this slice.** No streaming API, iterator ownership, JSON/CSV output, or disposal contract change is planned. Discovery of such an impact returns to PLAN.
- **Repository Stryker configuration is not applicable as-is:** it mutates `PostgresExtremeRowRenderer`, not this carrier. The local decisive perturbation is required; do not claim the unrelated configured mutation run proves carrier sensitivity.
- Additional canonical mandatory classes remain G01; absence from the pack is not evidence of non-applicability.

### CHECK re-gather budget

**Owner: CHECK. Budget: two targeted re-gather rounds maximum per CHECK.**

Each round names the missing row(s), requests only their source/log/result, and records its use. No production edits or hidden DO iteration during re-gather. If the budget expires, report GAP and route to PLAN. Missing reports alone do not justify contract revision, attempt reset, or implementation retry.

An actual revised obligation requires explicit rv supersession, preservation of existing row IDs/obligations, and new IDs for added variants. Renaming or rewording is not a new plan revision.

## 10. Risks, confidence, and collection handoff

**Verified:** the current composite guard, existing assembly/stitch path, inherited rejected carriers, provider-generation gap, coverage thresholds, and collection handoff locations.
**Unproven:** flat-carrier compatibility, exact inherited null/multiplicity expectations, MariaDB live execution setup, benchmark sensitivity, and the absent canonical obligation/ownership data.

Principal risks are alias registration, metadata-order mismatches, per-row allocation, cache-shape collisions, duplicate occurrence loss, and interference with sibling/unrelated changes. Resolve them with D01 probes, full-component collision fixtures, explicit scoped identities, matched benchmarks, and single-writer ownership—not broadened projection machinery.

### Planned write footprint

| Certainty | Paths |
|---|---|
| Firm | `docs/specs/status/D223-joininto-composite-junction-sql-1.md`; `artifacts/pdca/D223/rv1/**`; `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs`; `JoinIntoManyToManySpec.cs`; core carrier/rejection and matrix fixtures; six providers' `JoinIntoCompositeSqlGenerationTests.cs`; authorized EN/RU limitations and affected registry entries |
| Likely | `JoinIntoStitcher.cs`; `EntityBuilder.cs` registration points; one new internal carrier file under `Builders/Joins/`; MariaDB M:N fixture/helper; integration junction fixture; planned benchmark file under `benchmarks/nextorm.benchmark/`; EN/RU relationships/guide examples |
| Not authorized by this minimal plan | Broad `MemberTranslator`/`RowMaterializerBuilder` redesign, nested projection support, public declarations, unrelated docs/benchmark outputs, generated docs, global mutation configuration |

Paths abbreviated above retain their supplied project directory context. New filenames are planned paths, not existing `file:line` claims.

### Sibling requirements and references

- T213 is landed/closed partial; do not reopen it (`D213-joininto-remainder-1.md:6,12,14,159,167`).
- No unspecified T225 implementation result is assumed. Only the known slice relationship is recorded (`collection-1.0.9-todo.md:73`).
- Coordinator must reserve `Builders/Joins/**` exclusively and grant external-document ownership before edits.
- Preserve collection ordering: own PLAN completion → re-pass ALL-barrier → re-cluster (`collection-1.0.9-todo.md:95`). This plan does not independently release the paused lane.
- References: #223, #213, milestone #4; verified URL recording remains G04.

**Readiness determination (clarification r=1, supersedes the initial pending line):** `plan_state=ready`. The contract permits reconnaissance as the first DO unit, so PLAN readiness does not require executing D01's green arm. The decisive baseline and rejection oracle already exist in committed sources (guard `JunctionLinkSourceFactory.cs:125–126`; committed `JoinIntoCompositeJunctionRejectionTests.cs:95` asserting rejection with `ConnectionsRequested==0`), so obtainability does not depend on hypothetical future test symbols or forbidden PLAN-phase implementation. This is a clarification of r=1; it is `rv=1`; no supersession or attempt reset. D01 remains the mandatory first DO unit; D01's probe evidence remains planned until executed. The ALL-barrier is satisfied for plan readiness; the lane prerequisites below must still be checked at handoff, and neither D01 completion nor acceptance success is claimed.

**Before DO, re-check D01 applicability:** confirm the composite-junction path still has the rejection guard at `JunctionLinkSourceFactory.cs:125–126` and the committed rejection oracle at `tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:95`, including `ConnectionsRequested==0`; confirm the lane ownership and the T216 prerequisite (`collection-1.0.9-todo.md:95`).

## 11. Next allowed step

**PLAN persisted (r=1 / rv=1, plan_state=ready). Do not start production carrier work or declare the collection barrier passed from this handoff.**

After canonical obligations, ownership, tracking, and execution authorization are supplied, the **first DO unit is D01 — readiness and decisive carrier probe**, beginning with the positive composite-junction test against the retained baseline guard. The collection parent owns the PLAN→DO boundary: this phase yields the `plan_handoff` and the lane later resumes the **same** cycle from DO after the applicability re-check above.

D02 is blocked on successful D01 and manifest freeze. If D01 invalidates the selected design, return a new evidence-backed PLAN task; do not silently retry a rejected T213 carrier or reset attempts.

## Progress log

2026-10-12T19:56:52Z | PLAN | revision r=1 | iteration n/3 | PLAN ready — awaiting confirmation | planner r=1 report persisted in this file
2026-10-12T19:56:52Z | PLAN | revision r=1 | iteration n/3 | PLAN persisted (r=1, rv=1, plan_state=ready); handing off to collection parent at PLAN→DO boundary (single group G1; awaiting lane resume) | collection caller handoff; DO not started
2026-10-13T08:26:19Z | DO | revision r=1 | iteration n=1/3 | DO started (D01 readiness+probe) — single group G1, current branch 1.0.9 | orchestrator dispatch
2026-10-13T08:26:19Z | D01 | revision r=1 | iteration n=1/3 | D01 done: guard present at Builders/Joins/JunctionLinkSourceFactory.cs:127-128; oracle tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:104-108 (ConnectionsRequested==0); baseline-red exit2, candidate-green exit0 (SQLite exec+stitch), last-component perturbation red->restored green, arity 3/1 green; no T213 causes; evidence artifacts/pdca/D223/rv1/probe/RESULT.md | coder report
2026-10-13T08:30:41Z | D02 | revision r=1 | iteration n=1/3 | D02 done: productionized composite carrier (CompositeJunctionLinkFactory.cs new + JunctionLinkSourceFactory.cs + JoinIntoManyToManySpec.cs); fixed in-memory placeholder index pairing; carrier naming/reverse-lookup hardening; fail-closed component guard; 9/9 D02 selectors green (core.tests, 59 tests), build nextorm.slnx 0W/0E; only D01-owned rejection oracle still red by design (D03 updates); evidence artifacts/pdca/D223/rv1/evidence-d02.json + tests/variant-matrix.json; validator exit 0 | coder report
2026-10-13T08:43:27Z | D03 | revision r=1 | iteration n=1/3 | D03 done: post-rebuild six-provider SQL green (CompositeSqlGeneration sqlite 6/postgres 6/sqlserver 6/mysql 6/mariadb 7/clickhouse 6; JoinIntoManyToMany sqlite 4/postgres 6/sqlserver 4/mysql 4/mariadb 4/clickhouse 4); LIVE composite-junction 12/12 passed, 0 skipped across sqlite/postgres/sqlserver/mysql/mariadb/clickhouse (DOCKER_HOST podman socket, mariadb:11.4 present); build nextorm.slnx 0W/0E exit0; validator exit 0 | evidence artifacts/pdca/D223/rv1/evidence-d03.json, check/build-sweep.log, providers/*/{sql,JoinIntoManyToMany}.log, integration/composite-junction.log, integration/provider-map.json; coder report
2026-10-13T08:49:13Z | D04 | revision r=1 | iteration n=1/3 | D04 docs+registries portion done (perf+manifest portion pending, separate dispatch): EN/RU limitations + relationships composite-junction/M:N-single-query support statements corrected (full-component matching + preserved occurrence, fluent helpers single-column, no internal carrier named); todo_navigation_properties.md, sql-capabilities-gap-analysis.md, todo_eager_loading_pipeline.md registries updated to implemented (#213/#223); D213 resolution section appended without rewriting rejected evidence; guide/02-joins.md has no affected junction/M:N text at :209 (verified NONE, no-op; no page move) | docfx exit 0 (2 pre-existing unrelated sourcegenerator warnings, 0 errors); git diff --check exit 0; all 8 files CRLF; evidence artifacts/pdca/D223/rv1/docs/RESULT.md, artifacts/pdca/D223/rv1/docs/docfx.log; coder report
2026-10-13T09:07:33Z | DO→CHECK | revision r=1 | iteration n=1/3 | DO→CHECK boundary sweep complete: build nextorm.slnx exit0 0W/0E; 7 project suites exit0 (core 2449, sqlite 1664 +1 opt-in skip, postgres 897, sqlserver 814, mysql 380, mariadb 265, clickhouse 662); LIVE JoinInto 124/124 passed, 0 skipped, six providers; coverage line 83.4% / branch 76.0% (thresholds 85/75; branch 1.0.9 warning-only); manifest frozen 17/17 rows, validator exit 0 | artifacts/pdca/D223/rv1/{check,coverage,manifest.json}; coder report, awaiting CHECK
2026-10-13T09:23:50Z | CHECK | revision r=1 | iteration n=1/3 | CHECK FAIL r=1 n=1/3 (verdict pointer) — findings W1 (P2, atomic carrier emission), W2 (P1 locked AC01, order/type correspondence), W3 (P2, rejected-arm assertion) | awaiting corrective DO
2026-10-13T09:23:50Z | DO | revision r=1 | iteration n=2/3 | Corrective DO started n=2/3 (same revision r=1) — fix W2 fail-closed declared correspondence + same-typed reversed-order SQL regression; W1 atomic carrier emission; W3 zero-connection rejected-arm assertion | orchestrator dispatch
2026-10-13T09:30:52Z | D02c | revision r=1 | iteration n=2/3 | Corrective DO n=2/3 done: W2 declared correspondence (JoinIntoManyToManySpec.BuildJoinPairs: member-identity + nullable type check, fail closed before connection) + sensitive reversed-order SQL regression (red exit2 blind-position /green exit0 fixed); W1 atomic Lazy<CarrierInfo> emission (CompositeJunctionLinkFactory.cs:87); W3 positive arm zero-open/zero-execution assertion restored; build nextorm.slnx 0W/0E; selectors core order2/carrier5/rejection4/singlequery3 + sqlite SqlGen6 + 5 providers SqlGen all green; variant-matrix.json 29 rows (19 test/4 guard/6 deferred+trigger); report validator exit0 | artifacts/pdca/D223/rv1/{evidence-d02c.json,scope-d02c.json,tests/variant-matrix.json,tests/d02c}; coder report
2026-10-13T09:38:55Z | D02d | revision r=1 | iteration n=2/3 | TASK1 variant-matrix closure: arity 1/2, 2/2, 1/3, 3/3 + string key + supported converter + M:N junction null component added as non-vacuous tests (core in-memory fixtures + SQLite SQL execution on the emitted arity-general carrier); variant-matrix.json 29 rows all test/guard, deferred_summary empty (plan §4 "Deferred functional variants: none"); TASK2 perf repeat scalar ExecuteWarm 4000 = baseline 165.15 / candidate 173.56 / repeat 167.40 ms -> noise, NOT a reproducible scalar regression (allocations identical 27.3 MB); build nextorm.slnx 0W/0E exit0; selectors core Remainder17/NullSemantics4/JunctionKeyOrder2/CarrierSpike5 + sqlite CarrierSpike9 all exit0; report validator exit0; no commit | artifacts/pdca/D223/rv1/{scope-d02d.json,evidence-d02d.json,tests/variant-matrix.json,tests/d02d/*.log,perf/baseline-repeat.log,perf/RESULT.md}; coder report
2026-10-13T09:44:30Z | DO→CHECK | revision r=1 | iteration n=2/3 | DO→CHECK (n=2/3, corrected) boundary sweep on the corrected (W1/W2/W3) tree: build nextorm.slnx exit0 0W/0E; 7 project suites exit0 (core 2456, sqlite 1672 +1 opt-in skip, postgres 897, sqlserver 814, mysql 380, mariadb 265, clickhouse 662); LIVE JoinInto 124/124, 0 skipped, six providers; coverage line 83.4% / branch 76.0%; manifest re-frozen 17/17 rows (digests+evidence refreshed; order-collision red→green; 29-row matrix all test/guard, no deferred; perf repeat classified noise) validator exit 0 | artifacts/pdca/D223/rv1/{check/**,coverage,manifest.json}; coder report, awaiting CHECK
2026-10-13T09:48:42Z | CHECK | revision r=1 | iteration n=2/3 | CHECK PASS r=1 rv=1 n=2/3 — all AC01–AC08 met; W1 (atomic Lazy<CarrierInfo> emission) / W2 (fail-closed declared correspondence + reversed-order red→green) / W3 (zero-connection rejected-arm assertion) resolved; matrix 29/29; perf scalar repeat classified noise; coverage line 83.4% / branch 76.0% accepted under 1.0.9 warning-only policy; manifest 17/17 met, validator exit 0 | artifacts/pdca/D223/rv1/check/contract-verdict.md; manifest.json
2026-10-13T09:48:42Z | ACT | revision r=1 | iteration n=2/3 | ACT finalize: plan/cycle state set to done (CHECK PASS r=1 rv=1 n=2/3); verdict persisted at artifacts/pdca/D223/rv1/check/contract-verdict.md (points to manifest.json); collection T223 row -> done; DOCS/registries already landed (D04) | coder report; ACT handoff to collection parent (next pending T216, order 10)
