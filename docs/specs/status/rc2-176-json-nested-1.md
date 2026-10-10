# D176 — Collection task plan

## 1. Durable state and objective

```yaml
task: D176
issue: "#176 — JSON streaming Phase 2: nested projections and Projection<T1,T2>"
repository: /home/alex/sources/nextorm
branch: 1.0.9-rc2
base: 18659e41
status_file: docs/specs/status/rc2-176-json-nested-1.md
plan_state: ready
phase: ACT
r: 2
N: 1
rv: 2
active_plan_task: P176.2
execution_state: complete
final_check: PASS (r=2/rv=2, evidence-only re-gather, n unchanged)
```

**P176.1:** implement a JSON-specific recursive shape plan, bind it to scalar reader ordinals, and support nested projections, actual `Projection<T1,T2>` result shapes, and provider-native arrays without changing ordinary materialization.

The previous `gap` response was not an activated implementation plan. The r=1 finalization remained **r=1, N=1, rv=1**; the r=2 additive replan recorded here adds the D176.7 conditional-construction prerequisite and supersedes rv=1 (§15) while preserving the original criteria.

**Counter note (N vs n).** The durable state `N: 1` is the outstanding **cycle number**; the Progress-log field `iteration 2/3` (shorthand `n=2/3`) is the **DO attempt/iteration counter within cycle N=1**. They are separate counters and must not be conflated: `N` is not the iteration index.

Persist this plan as UTF-8/CRLF before DO. Normal mode requires the existing explicit `go` gate; inherited autonomous mode proceeds after its mandatory gate checks, without asking or waiting. No commit, push, or merge is authorized by this plan.

## 2. Acceptance criteria

| Requirement | Positive acceptance | Required negative case |
|---|---|---|
| **R176.01 — Shape fidelity** | Both existing terminal surfaces produce JSON equivalent to STJ serialization of the actual supported result type. Nested anonymous/named constructions retain object boundaries. | Nested objects must not flatten into the parent or bind a leaf to the wrong ordinal. |
| **R176.02 — Projection slots** | `Projection<T1,T2>` emits top-level `Item1` and `Item2`, each an object/scalar according to its item type. | Same member name in different slots must not trigger a global duplicate-name rejection. |
| **R176.03 — Names and nulls** | Uniqueness is checked per object scope. Constructed objects and nullable joined entities follow §5. | Duplicate effective names within one object fail before writing; an all-null constructed object must not become `null`. |
| **R176.04 — Arrays** | Provider-native rank-one `T[]`, including supported jagged arrays, serialize recursively using the supported STJ element contract. | `byte[]` must remain Base64; unsupported collection/element types fail closed rather than flattening or using arbitrary runtime serialization. |
| **R176.05 — Phase 1 and isolation** | Existing scalar/flat JSON, ordinary materialization, cancellation/disposal, and shared-command/plan-cache behavior remain intact. | No sticky `QueryCommand.Cache=false`, cross-call shape leakage, or change to ordinary `ToList`/join results. |
| **R176.06 — Providers** | Nested/object/slot scenarios run across SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, and ClickHouse. Native-array scenarios run where a native source exists. | Provider skips cannot masquerade as passing evidence; absence of a native array capability cannot be counted as an array test. |
| **R176.07 — Boundaries** | Unsupported shapes are rejected predictably during preparation. | Enum expansion, converter-backed collection support, child-collection query projection, and DB-side JSON must not enter this change accidentally. |
| **R176.08 — Quality/performance** | Required build, coverage, mutation, benchmark, and documentation evidence is recorded. | Exit zero with no relevant tests/cases, missing artifacts, or skipped required providers is insufficient. |

For supported results, compare parsed JSON with the STJ oracle, preserving array order and checking exact property names separately. Do not require byte-for-byte object-property ordering unless Phase 1 already guarantees it.

## 3. Minimum solution and alternatives

**Three answers**

1. **Essential goal:** recover the result’s object/array structure while continuing to read scalar columns directly.
2. **Hard constraints:** preserve Phase 1 and normal materialization; no dependency on #160/#172/#177/#178/#190; no per-row entity construction or whole-result buffering.
3. **Minimum implementation:** internal recursive JSON shape metadata, JSON-only scalar lowering/binding, and a recursive writer using the existing scalar write kinds.

| Approach | Advantages | Cost/risk | Decision |
|---|---|---|---|
| JSON descriptor + scalar lowering + recursive writer | Direct streaming; explicit ordinal mapping; narrow materialization impact | Requires preparer integration and null-presence metadata | **Selected** |
| Materialize each result, then use STJ | Naturally reconstructs CLR objects | Changes allocation/performance model; bypasses direct-reader streaming | Rejected |
| Recover structure inside the writer from flattened columns/names | Small apparent preparation change | Ambiguous nesting, duplicate names, and null/default behavior | Rejected |

There is **no remaining schema or collection-source fork**.

## 4. Shape recovery and preparer design

Anchors: `Query/QueryCommand.QueryPreparer.cs:523-536,618-628,825,839,900,912`; `Expressions/SelectExpression.cs:355-375`.

### Chosen mechanism

- Capture a JSON shape description **from the original projection expression before preparation makes nested construction opaque**.
- Recursively visit supported `NewExpression` and `MemberInitExpression` constructions. Record object nodes and their resolved result members; lower their scalar descendants through the existing expression/SQL preparation machinery.
- Bind each leaf to its **assigned prepared reader ordinal**, using explicit binding metadata from lowering. Do not reconstruct ordinal ownership from SQL aliases, property-name equality, or incidental traversal order after preparation.
- Preserve `ProjectionEntityItem.Slot/Member` for expanded entity groups. These identify `Item1`/`Item2` boundaries; their mapped scalar columns remain the reader inputs.
- Represent nullable-object presence separately from property values where required by §5. Hidden presence columns are not JSON properties.
- Build per-object name tables. Replace the global guard at `Query/Json/JsonShapePlan.cs:122,161-163` with scoped validation.
- Build array nodes from the declared array type. Classify elements recursively; test `byte[]` **before** general array handling (`JsonShapePlan.cs:193-194`).

### Blast-radius control

The lowering/binding entry is JSON-specific. Ordinary query preparation and `RowMaterializerBuilder` behavior remain unchanged.

The lowered JSON selection is detached from the shared source command. Do not mutate shared `SelectList`, result shape, or sticky cache flags. Where the new lowered preparation must avoid caching, use the per-call `storeInCache:false` mechanism, not `QueryCommand.Cache=false`. Preserve the existing Phase 1 preparation behavior.

**First DO confirmation:** whether existing prepared metadata can supply every binding directly. **Already-selected fallback:** add the JSON-only preparer entry that returns the descriptor and lowered scalar selection together. This is planned implementation work, not a new semantic alternative.

## 5. Null semantics

Anchors: `RowMaterializerBuilder.cs:53-59,72-188,236-253`; `SelectExpression.cs:270-276`; `RowMapperFactory.cs:88-100`; `JoinExpression.cs:81`; `QueryCommand.cs:341`.

**Semantic authority is STJ over the actual materialized result, not a generic "all leaves null" heuristic.**

- **Explicit `new` / member-initialized nested object:** write an object even when all its properties are null/default. Construction establishes object presence.
- **Explicit null-valued construction branch:** write `null`. Preserve the branch’s presence decision through a hidden scalar presence expression, rather than inferring it from visible leaves. Use provider-portable scalar lowering; do not assume providers can select a CLR-style Boolean expression unchanged.
- **Outer-join entity slot:** reproduce the existing materializer’s absence predicate over the original `DBNull` inputs of the full mapped entity group, before `DefaultOnNull` substitutes values. Carry that predicate in JSON preparation metadata.
- Consequently, **all leaves null is not a universal object-null rule**. If the ordinary materializer produces an object with null properties, JSON must produce that object; if it produces an absent joined entity, JSON must produce `null`.
- **Scalar item/leaf:** retain Phase 1 `DBNull`, nullable, and default behavior.
- **Array:** SQL `NULL` → `null`; empty array → `[]`; supported null elements → `null`.

The spike verifies access to the materializer’s presence inputs and parity in an unmatched join and an all-null constructed object. It does **not** choose new null semantics. If independent JSON logic cannot reproduce that predicate safely, extract/reuse the existing internal presence rule without changing ordinary materialization.

## 6. What the brief did not specify; predecessor results

| Gap/result | Closure |
|---|---|
| Phase 1 predecessor | Present in the supplied base: `JsonShapePlan.cs:103,122-198` and `DataContext.cs:390-391`. Preserve it and establish a pre-change test baseline. |
| Exact joined-result schema | Resolved: `Projection.cs:37,40,42`; `JoinedEntityBuilder.cs:13`; STJ-shaped `Item1`/`Item2`. |
| Reachable collection source | Resolved: native arrays, including ClickHouse examples at `Entities.cs:75-85`, `ClickHouseIntegrationTests.cs:1379-1389,1437-1447`. |
| `Projection<T1>` | Unavailable. **Do not depend on #160.** |
| #190 | No prerequisite established by this pack. Do not add one speculatively. |
| Nested-expression binding details | Bounded D176.1 spike; planned preparer fallback in §4. |
| Joined-entity absence implementation details | Existing materializer is authoritative; bounded parity probe, not permission to invent all-null semantics. |
| New test/benchmark symbols and exact doc pages | Not yet existing evidence. Planned sources only; record actual symbols/pages after implementation. |
| Project class-priority table | Not supplied. Use execution-path/invariant priorities in §11. |
| Baseline performance tolerance | Use published acceptance rules where specified; additionally investigate reproducible >5% degradation. Do not weaken published rules. |
| External DB readiness | In-cycle integration setup under the integration skill; an unavailable socket alone is not an external blocker. |
| Stryker availability | Resolved: no working repo-local invocation. Manual targeted mutation is required; D151 is not a prerequisite. |

## 7. DO tasks and footprint

| Unit | Action | Footprint and known anchors | Disposition |
|---|---|---|---|
| **D176.1** | Establish test/performance baseline; execute the bounded binding/null spike; record descriptor-to-ordinal and materializer-parity observations. | `QueryPreparer.cs:523-536,618-628`; `RowMaterializerBuilder.cs:236-253`; existing test projects | **Fix now** |
| **D176.2** | Add JSON-specific shape capture, scalar lowering/binding, slot grouping, and presence metadata. | `Query/QueryCommand.QueryPreparer.cs`; `Expressions/SelectExpression.cs:355-375`; `DataContext/DataContext.cs:390-391`; `Query/Json/*` | **Fix now**, depends on D176.1 |
| **D176.3** | Implement scoped-name validation and recursive object/array writing; retain scalar/Base64 behavior and fail-closed guards. | `Query/Json/*`; `JsonShapePlan.cs:122,128-130,161-198` | **Fix now**, depends on D176.2 |
| **D176.4** | Unit tests, boundary sweep, ordinary-materializer/cache regressions, both terminal surfaces. | `tests/nextorm.core.tests/**`; relevant `tests/nextorm.<provider>.tests/**` | **Fix now** |
| **D176.7** | Conditional nested construction: extend capture/lowering at `QueryCommand.QueryPreparer.cs:844,864,901`; recognize `ConditionalExpression` with a translatable predicate and a `New`/`MemberInit` construction arm; append a hidden nullable sentinel scalar (null on null-arm, non-null on construction arm) via `QueryPreparer.cs:1086` + existing `BaseExpressionVisitor.cs:833`→`PredicateTranslator.cs:177,222-227` CASE lowering; attach its ordinal through `QueryPreparer.cs:994,1156`; reuse `JsonShapePresence.AnyColumnNotNull`; hidden column never appears in JSON. Reject non-translatable predicates and unsupported arms fail-closed. Footprint: `Query/QueryCommand.QueryPreparer.cs`, `Query/Json/*`. Fix now; depends on D176.1–D176.4. D176.5 stays active/blocked on D176.7; D176.6 downstream. | `Query/QueryCommand.QueryPreparer.cs`; `Query/Json/*` | **Fix now**, depends on D176.1–D176.4 |
| **D176.5** | Shared integration scenarios and provider-specific array tests; wire MariaDB/ClickHouse mirrors. | `tests/nextorm.integration.tests/CommonTestSuite.*.cs`; `MariaDbJsonStreamTests.cs:14,21,31-52`; `ClickHouseIntegrationTests.cs:1352-1447`; test entities | **Fix now** |
| **D176.6** | Documentation and planned focused JSON benchmarks; execute all evidence obligations. | `docs/guide/**`, `docs/ru/**`, internal roadmap/status; `benchmarks/nextorm.benchmark/**`; evidence directory | **Fix now** |

**Footprint uncertainty:** exact new internal files and existing owning article/test files are determined during D176.1/D176.4 inventory. These are planned locations, not asserted symbols or future `file:line` evidence. Any expansion into general query planning/materialization beyond the listed isolation work returns to PLAN.

**Deferred findings**

- Child-collection query projection → **#172**; trigger: that issue supplies its query/materialization contract.
- New naming/options behavior → **#177**; trigger: its contract lands.
- Enum/converter expansion → **#178**; trigger: its supported-type contract lands.
- Stryker automation → **D151**; trigger: a working validated invocation lands.
- DB-side JSON generation and non-array collection support → separate issue/explicit scope approval.
- No unrelated source-generator, provider feature, or `Projection<T1>` work.

## 8. Unit mode and dependencies

**Sequential, one working tree.** D176.2/D176.3 share shape contracts and preparation files; parallel independent edits would create avoidable contract conflicts.

Order: **D176.1 → D176.2 → D176.3 → D176.4 → D176.5 → D176.6**, allowing test additions alongside the implementation only under the same owner.

Use a disposable isolated worktree only for mutation if isolation is needed to protect the implementation. No commits/merges; integrate any required changes by patch. Normalize changed tracked files to CRLF.

## 9. Test strategy and execution

### Unit/provider SQL tests

- `tests/nextorm.core.tests`: descriptor topology, leaf binding, scoped names, presence rules, recursive arrays, guards, both terminal surfaces, lifecycle/cache regressions.
- SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse dialect projects: relevant lowered SQL/presence expressions and preparation regressions.
- Run complete affected projects rather than inventing filters for future symbols.

**Boundary sweep:** scalar/object/array roots; depth 1/2/multiple; zero/one/many properties and array elements; ordinal boundaries; repeated names across scopes; duplicate names inside one scope; matched/unmatched joins; all-null and mixed-null leaves; empty/null/jagged arrays; cancellation/error/disposal.

### Integration

Shared cases belong in `CommonTestSuite.*.cs`; native arrays belong in provider-specific tests. Both terminal surfaces must be exercised. Explicitly mirror required bodies in the standalone MariaDB class and ClickHouse class—their inheritance/wiring is not interchangeable.

Run the full container-backed suite, not only SQLite. PostgreSQL, SQL Server, MySQL, ClickHouse, and MariaDB must have positive executed-test counts for their required scenarios. Load the integration skill before setup/run; use its machine-start/socket recovery procedure before classifying an infrastructure failure.

### Coverage

Project thresholds are **85% line / 75% branch**. Coverage scope excludes MySQL/MariaDB/ClickHouse (`coverage.settings.xml:12`); that does not remove their test obligations. On this non-main branch, below-threshold project totals are warnings according to CI, and must be reported honestly. All new P1 behaviors still require explicit scenario and mutation evidence.

## 10. Closed variant matrix

| Variant | Closure |
|---|---|
| Existing scalar and flat object; nullable/default/value/reference leaves | **Test** — Phase 1 regressions plus boundary cases |
| Nested anonymous `new` | **Test** — multiple levels, repeated sibling names, all-null children |
| Named construction/member initialization with resolvable members | **Test** — same topology and ordinal cases |
| Null-valued conditional nested construction with translatable predicate | **Test** — explicit presence versus all-null non-null object |
| Opaque factory/method-produced object or unresolvable constructor member mapping | **Guard** — reject before writing |
| `Projection<T1,T2>` entity/entity | **Test** — exact `Item1`/`Item2`, duplicate leaf names across slots |
| Supported scalar/entity and scalar/scalar slot forms | **Test** — scalar slot is not wrapped in an invented object |
| Inner/outer joins, matched/unmatched entity slots | **Test** — oracle parity before null/default substitution |
| Duplicate effective JSON name within one object | **Guard + test** |
| Same name in different object scopes | **Test** — accepted |
| Native rank-one scalar `T[]` | **Test** — ClickHouse string/numeric and PostgreSQL string source |
| Jagged native arrays | **Test** — concrete ClickHouse nested-array source plus unit boundaries |
| Null/empty arrays and supported nullable/reference elements | **Test** |
| `byte[]`, including `byte[]` inside a jagged array | **Test** — Base64 takes precedence recursively |
| Multidimensional arrays; unsupported array element types | **Guard + test** |
| `List<T>`, arbitrary `IEnumerable<T>`, dictionaries, converter-backed collections | **Guard + test** — no writer fallback |
| Enum/new converter types | **Guard**; **deferred to #178**, triggered by its contract |
| Child-collection queries | **Guard**; **deferred to #172**, triggered by reachable query support |
| New serializer options/naming policy behavior | **Deferred to #177**; existing Phase 1 behavior is tested unchanged |
| DB-side JSON | **Guard against accidental route**; **deferred**, triggered by explicit scope approval |
| Providers without a native array source | **Guard/capability record** — no synthetic child-query replacement; object/slot tests remain mandatory |
| Excessive depth/unsupported recursive shape | **Guard + test** — preserve applicable writer limits; no partial unsupported-shape fallback |
| Cancellation, reader failure, destination failure, disposal | **Test** — preserve existing terminal contracts |

No matrix row is TBD.

## 11. Priority matrix and risks

**P1 by construction:** all requirement invariants and changed execution-path branches. CHECK may not downgrade them.

| P1 rows/paths | Main risk | Required control |
|---|---|---|
| R176.01–03; shape capture, ordinal binding, slot presence, scoped-name checks | Correct-looking JSON with wrong values or null topology | STJ oracle, distinct sentinel leaf values, all-null/unmatched tests |
| R176.04; array dispatch and element recursion | Base64 regression, boxing/type mismatch, accidental permissive fallback | Recursive type-bound plans; driver-native arrays; guard mutations |
| R176.05; preparation and shared-command state | Persistent cache disablement or cross-call shape leakage | Repeated calls on one context; JSON then ordinary query and reverse |
| R176.06; provider wiring | Green report that did not execute MariaDB/ClickHouse cases | Provider/scenario execution ledger; skips explicitly rejected |
| R176.07; fail-closed boundaries | Scope bleed into adjacent issues | Negative tests before writer execution |
| R176.08; per-call/per-row changes | Preparation or row-loop regression | Baseline/post-change benchmarks and allocation evidence |

Other risks: SQL conditional-lowering differences; opaque construction metadata; nullable driver array values; recursive-writer depth; benchmark environment noise. Missing reports are an evidence problem, not grounds to change acceptance or repeat implementation.

## 12. Documentation plan

**Touch:**

- Existing public JSON-streaming guide and Russian mirror: supported nested shapes, exact `Item1`/`Item2` schema, native-array scope, Base64, null semantics, and explicit exclusions.
- Internal roadmap `docs/specs/roadmap/todo_json_streaming.md:60-64,199-212,281`: record the implemented interpretation and remaining issue boundaries.
- This status file and evidence index.

**Do not touch:** generated `docs/api/**`, `docs/_site/**`, unrelated guides, API renames, or guide numbering.

No public article/readme link to `docs/specs/**`. Locate owning public pages with ignore-aware documentation search; do not invent article paths. Build DocFX after changes.

## 13. Performance decision

**Required.** `PrepareJsonStream` is per call (`DataContext.cs:390-391`), not a one-time application startup operation. This plan changes preparation and introduces recursive per-row writing.

1. **Mandatory seven-case acceptance benchmark**, before implementation and after implementation:
   ```bash
   dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
   ```
   Exactly seven normal-build cases are expected (`nextorm.benchmark.csproj:38`; `docs/specs/performance/acceptance-benchmarks.md`). Record local baseline at the supplied base, environment, timing, allocations, and post-change ratios.

2. **Focused planned JSON benchmark category:** `json-stream-phase2`. Add—not pretend already to have—flat Phase 1, nested object, joined slots, native-array, and conditional/null/all-null-object workloads (a null-valued translatable conditional construction, plus an all-null non-null construction compared against the materialize+STJ oracle):
   ```bash
   dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=json-stream-phase2
   ```
   Compare direct streaming against the materialize+STJ oracle on the same data, with row counts exposing preparation and row-loop costs. Record allocations and throughput; verify no whole-result buffering.

Apply published acceptance rules. Investigate reproducible >5% regressions; a noisy first result requires measurement, not an automatic plan revision. Record the actual writer `file:line` after implementation; none was supplied in this brief.

## 14. Reconnaissance, confidence, and return classification

**Bounded in-cycle spike required: D176.1.** Maximum **60 minutes, two probes, one planned fallback attempt**.

- **Probe A:** nested anonymous and named projection with distinct leaves at repeated paths; observe prepared ordinals, JSON descriptor bindings, valid SQL, and equality with ordinary materialization/STJ.
- **Probe B:** matched/unmatched join plus non-null construction containing only null leaves; observe raw `DBNull`, existing materializer presence decisions, and JSON parity.
- **Fallback:** implement the JSON-only preparer binding entry and reuse/extract the existing presence predicate. Do not move reconstruction into a heuristic writer.

**Pass observable:** every visible leaf/presence input has an explicit binding; ordinary results are unchanged; both probes match the oracle. Preserve probe logs and actual source anchors.

If the bounded work fails:

- Missing internal plumbing within scope → **additive prerequisite**; original D remains active/blocked, never marked done or superseded.
- Incomplete evidence → targeted scout first.
- Persistent low confidence → recommend orchestrator call `escalate`, **trigger 5**, even without proving an external blocker.
- Truly unavailable external resources after prescribed recovery → recommend `escalate`.
- Actual scope/dependency change → new `P:` task and genuinely revised plan; only then increment `r` and reset `N`.
- Rejecting a proposed change resumes the original D; it does not reset attempts.

**Confidence:** high in schema/scope decisions; medium-high in implementation feasibility, with ordinal/presence plumbing deliberately verified by the bounded first DO step.

## 15. Versioned evidence contract — rv=2

**rv=2 supersedes rv=1.** E176.01–E176.12 are preserved verbatim (IDs, scenarios, invocations,
artifacts); rv=2 adds E176.13–E176.14 for the D176.7 conditional-construction behavior and guards.

All sources below are **planned evidence**, not claims that tests or artifacts already exist.

### Exact invocation registry

```text
C01 = dotnet build -c Debug
C02 = dotnet test tests/nextorm.core.tests -c Debug
C03a = dotnet test tests/nextorm.sqlite.tests -c Debug
C03b = dotnet test tests/nextorm.postgres.tests -c Debug
C03c = dotnet test tests/nextorm.sqlserver.tests -c Debug
C03d = dotnet test tests/nextorm.mysql.tests -c Debug
C03e = dotnet test tests/nextorm.clickhouse.tests -c Debug
C04 = DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
C05 = dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"
C06 = reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"
C07 = dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
C08 = dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=json-stream-phase2
C09 = dotnet docfx docs/docfx.json
C10 = git diff --check
C11 = Task(coder, "D176.1: execute the two ordinal/presence probes and the one planned preparer fallback under section 14; preserve observations, source anchors, commands, logs, and oracle comparisons.")
C12 = Task(coder, "E176.10 rv=1: execute isolated manual targeted mutations for ordinal binding, scoped duplicate-name rejection, constructed-object presence, joined-slot presence, byte-array precedence, and unsupported-kind rejection; run C02 and the applicable C03/C04 scenarios for each mutant; record patch and diagnosed test failure; restore each mutant; rerun the unmutated affected suite.")
C13 = Task(coder, "E176.12 rv=1: record the closed variant/provider execution ledger, actual test symbols and source anchors, changed-path priority inventory, UTF-8/CRLF verification, and evidence index; do not count missing or skipped scenarios as passing.")
C14 = dotnet run --project tests/nextorm.core.tests -c Debug --no-build -- -class NextORM.Core.Tests.JsonShapeWriterTests -noColor -result-xml
C15 = dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- -class NextORM.Sqlite.Tests.JsonStreamingTests -noColor -result-xml
```

C14/C15 are the per-class `-result-xml` runs that actually produced the per-symbol
`E176.13.log`/`E176.14.log`; **C02** (`dotnet test tests/nextorm.core.tests -c Debug`) and **C03a**
remain the canonical project-level runs.

C04 requires the loaded integration skill and its prescribed recovery. MariaDB must execute through its standalone class, not be assumed covered by the shared suite. C08’s category is a **planned implementation artifact**.

Evidence root: `docs/specs/status/evidence/rc2-176/r1-n1/`. Keep benchmark originals in `BenchmarkDotNet.Artifacts/**`; coverage artifacts at the paths specified above. Index all artifacts from the status file.

### Contract rows

| Row / requirement / priority | Required scenario and evidence kinds/sources | Invocation; required result/log | Expected artifacts | Owner; observable applicability |
|---|---|---|---|---|
| **E176.01 / R176.01,R176.03 / P1** | Bounded nested-binding and presence probes; implementation observations, scalar-ordinal map, ordinary-result/STJ comparisons | **C11**; pass observables from §14, actual observations—not merely "spike complete" | `spike.md`, probe logs, ordinal/presence maps, actual `file:line` anchors | **coder**; always applicable |
| **E176.02 / R176.01 / P1** | Anonymous/named nesting, root forms, both terminals, distinct sentinel values; unit assertions and shape/SQL inspection | **C02**; exit 0, relevant scenarios executed with positive counts, oracle equality | core test log, actual scenario→test-symbol map | **coder**, reviewed by **check**; nested projection scope exists |
| **E176.03 / R176.02,R176.03 / P1** | `Item1`/`Item2`, scalar/entity slots, scoped names, duplicate guard, explicit/all-null/outer-join cases | **C02 + C03a–e + C04**; exit 0, positive relevant execution counts, exact names/null topology; required provider skips fail evidence | slot/name/null test logs and oracle comparisons | **coder/check**; always applicable |
| **E176.04 / R176.04 / P1** | Native arrays, jagged arrays, null/empty/elements, Base64, multidimensional and unsupported-type guards; unit and real-driver evidence | **C02 + C03b + C03e + C04**; exit 0; PostgreSQL/ClickHouse native-source cases actually executed | array logs, source/capability ledger, JSON comparisons | **coder/check**; array support is in scope; native DB variants apply only to evidenced capabilities |
| **E176.05 / R176.05,R176.07 / P1** | Phase 1, normal materialization, repeated-context/cache state, lifecycle, fail-closed adjacent-issue boundaries | **C02 + C03a–e + C04**; exit 0, relevant regression/negative tests executed; no sticky or cross-call state change | regression logs, before/after command-state observations, negative-case map | **coder/check**; always applicable |
| **E176.06 / R176.06 / P1** | Six-provider parity and standalone/mirrored test wiring; real integration execution evidence | **C04**; exit 0; positive required scenario counts for SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse; no required provider skipped | full integration log, per-provider counts, setup/recovery log | **coder/check**; always applicable |
| **E176.07 / R176.08 / P1** | Compilation, nullable/analyzer correctness, affected dialect suites | **C01 + C02 + C03a–e**; all exit 0; warnings-as-errors build clean | build and project test logs; per-OS build artifacts | **coder/check**; always applicable |
| **E176.08 / R176.08 / P1** | Exact CI coverage collection and report; line/branch totals and changed-path evidence | **C05 + C06** after successful build; both exit 0, nonempty reports; record 85/75 outcome and non-main warning policy without false "threshold passed" | `tests/coverage/coverage.cobertura.xml`, `tests/coverage/report/**`, coverage log/summary | **coder/check**; always applicable |
| **E176.09 / R176.08 / P1** | Baseline/post-change preparation and execution performance; focused JSON throughput/allocations | **C07** before/after, **C08** after implementation; exit 0; exactly seven acceptance cases; focused planned cases present; published rules and reproducible-regression review satisfied | baseline/post logs, BenchmarkDotNet exports, ratios, allocation/buffering analysis | **coder/check**; C07 always applies because preparer changes are planned; C08 applies to the new row-writing paths |
| **E176.10 / R176.01–05,R176.07 / P1** | Manual targeted mutation of six critical decisions; killed-mutant and restoration evidence | **C12**; each meaningful mutant produces a diagnosed relevant test failure/nonzero test exit; restored suites exit 0; survivors investigated | mutation manifest, patches, per-mutant logs, restored clean test log | **coder**, adjudicated by **check**; changed critical decisions applicable; equivalence must be demonstrated, not asserted |
| **E176.11 / R176.08 / P1** | Public/Russian docs parity, correct scope/schema/null examples, no internal-spec links, formatting | **C09 + C10 + C13**; commands exit 0; no broken touched links; UTF-8/CRLF and documentation obligations verified | DocFX/diff logs, actual touched-page list, formatting record | **coder/check**; docs always applicable; formatting applies to every changed tracked text file |
| **E176.12 / R176.01–08 / P1** | Evidence completeness: variant matrix, actual test/source anchors, provider counts, P1 path inventory | **C13**; every applicable variant mapped to executed test or evidenced guard; every deferral has its issue/trigger; no invented symbols or missing P1 proof | `variants.md`, evidence index, actual anchors, CHECK input bundle | **coder**, verified by **check**; always applicable |
| **E176.13 / R176.COND-BEHAVIOR / P1** | Supported conditional matrix: root/nested-member/collection-element placements × both terminals; true/false; both null-arm orientations; all-null non-null construction; supported New/MemberInit; null/default predicate inputs | **C14 + C15** (per-class `-result-xml` runs, the actual commands of `E176.13.log`); **C02/C03a** remain the canonical project-level runs; exit 0, every mapped case executed/passed, no mapped skips | `docs/specs/status/evidence/rc2-176/r2-n1/E176.13.log` + `E176.13-cases.md` | **coder**; verified by **check** |
| **E176.14 / R176.COND-GUARDS / P1** | Untranslatable predicate, unsupported construction arm, hidden-column exclusion, unchanged ordinary materialization | **C15** (per-class `-result-xml` run, the actual command of `E176.14.log`); **C02/C03a** remain the canonical project-level runs; exit 0, rejected inputs fail-closed | `docs/specs/status/evidence/rc2-176/r2-n1/E176.14.log` + `E176.14-cases.md` | **coder**; verified by **check** |

### CHECK re-gather budget and ownership

**Owner: check. Budget: two targeted re-gather requests total per CHECK attempt.**

- One request targets one bounded missing/ambiguous evidence group.
- Re-gather may collect existing logs, resolve actual test/source anchors through Roslyn, verify counts, or rerun the specified verification. It does not authorize implementation changes.
- A missing report first consumes targeted re-gather; it does not alone justify a contract revision or another DO attempt.
- After two unresolved requests, CHECK records the precise evidence gap and returns it for classification. Persistently low confidence routes to escalation trigger 5.

Any justified future **rv** change explicitly supersedes this revision, preserves **E176.01–E176.12** and their obligations, and adds stable IDs for new variants. CHECK applies the P1 matrix without lowering it.

**Final disposition: `plan_state: ready`. Confidence: high on required behavior and scope; implementation plumbing is covered by an explicit, bounded first-DO verification and a preplanned fallback.**

## DO units / streams

| unit | state | criteria covered | superseded→replacement |
|---|---|---|---|
| D176.1 | pending | R176.01–03 (spike), baseline | — |
| D176.2 | pending | R176.01–03, R176.05 | — |
| D176.3 | pending | R176.02–04, R176.07 | — |
| D176.4 | pending | R176.01–05, R176.07 | — |
| D176.7 | pending | R176.01-03, R176.05, R176.07 (conditional presence) | — |
| D176.5 | done | R176.06 | — |
| D176.6 | done | R176.08, docs | — |

## Decisions made

- Variant: `pdca-dotnet` (whole repo is .NET/C#).
- Unit execution mode: sequential, one working tree (§8).
- Reconnaissance: bounded in-cycle D176.1 spike, two probes, one fallback (§14).
- Performance: required — C07 acceptance (7 cases) before/after, plus planned C08 `json-stream-phase2` (§13).
- Mutation: manual targeted only; no repo-local Stryker command (D151 not a prerequisite).
- Scope: no dependency on #160/#172/#177/#178/#190.

## Defect history

| Defect key | Observed revisions/attempts | Fixes applied | Evidence | Last state |
|---|---|---|---|---|
| `json-conditional-nested-construction` | r=1 / D176.4 | 0 | `docs/specs/status/evidence/rc2-176/r1-n1/d176.4/defects.md` | open — additive prerequisite D176.7 (r=2) closes it |

None other (PLAN-phase r=1, no other DO attempts recorded).

## Risks / known issues

See §11. Top: ordinal binding is verified by D176.1 with a preplanned fallback; provider wiring must show positive MariaDB/ClickHouse counts.

## Changed files

D176.3 (ACT-committed, CRLF):

- `src/nextorm.core/Query/Json/JsonShapePlan.cs` — `Classify` made internal/reusable; captured-shape `Build` now enforces the phase-1 converter/LOB fail-closed guards.
- `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs` — recursive writer compiled from `JsonShapeNode` (object/array recursion, per-object scoped name validation, presence, Base64-before-array, depth/rank/element guards), replacing the temporary D176.2 fail-closed throw.
- `tests/nextorm.core.tests/JsonShapeWriterTests.cs` — new, 17 focused direct-writer tests (fake `IDataRecord`).
- `tests/nextorm.sqlite.tests/JsonStreamingTests.cs` — +4 focused end-to-end tests (nested object, same-name different scopes, per-scope duplicate rejection, whole-entity root).

D176.2 carriers (ACT-committed): `src/nextorm.core/Query/Json/JsonShapeNode.cs`, `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/Query/QueryCommand.cs`, `src/nextorm.core/DataContext/DataContext.cs`.

D176.7 (ACT-committed, CRLF):

- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` — conditional-construction capture: `ConditionalExpression` accepted in `TryPrepareJsonShapeColumns`/`JsonShapeNeedsCapture`; `TryGetConditionalConstruction`/`IsJsonConstructionArm`/`IsJsonNullArm`; `BuildJsonConditionalNode` + `AppendJsonConditionalSentinel` (hidden nullable string sentinel lowered through the existing CASE path, reusing `JsonShapePresence.AnyColumnNotNull`; both-arms / construction-vs-non-null-arm fail closed).
- `tests/nextorm.core.tests/JsonShapeWriterTests.cs` — +2 sentinel-only presence tests (all-null members stay object; sentinel-null wins over payload).
- `tests/nextorm.sqlite.tests/JsonStreamingTests.cs` — +10 end-to-end conditional-matrix/guard tests (both orientations, all-null construction, MemberInit, root, sync+async, hidden-column exclusion, untranslatable predicate, both-arms, construction-vs-non-null-arm).

D176.5 (ACT-committed, CRLF):

- `tests/nextorm.integration.tests/CommonTestSuite.JsonStreamNested.cs` — new shared integration partial: nested anonymous/multi-level/named, all-null child, same-name-different-scopes, conditional (both orientations, all-null object, MemberInit, both-arms guard), `Projection<T1,T2>` slots (left/inner join, duplicate leaf names across slots, entity+scalar, scalar+scalar), nested `byte[]` Base64, and sync/async pairs, with internal static bodies for the standalone MariaDB/ClickHouse mirrors.
- `tests/nextorm.integration.tests/MariaDbJsonStreamTests.cs` — local `binary_entity` seed plus 19 explicit mirrors of the shared phase-2 bodies.
- `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs` — nested/conditional mirrors, a ClickHouse-local `json_slot_parent`/`json_slot_child` join fixture (distinct member names because ClickHouse rejects duplicate whole-entity output aliases), and native `Array(String)`/`Array(Int32)` and `group_array` jagged-array facts.
- `tests/nextorm.integration.tests/PostgresSpecificTests.cs` — provider-native `array_agg` string-array JSON fact.
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` — SQL Server defect fix (`DisambiguateJsonLeafAliases`): duplicate lowered JSON leaves after the first get a distinct `__json_<ordinal>` output alias so same-named scopes do not collide in SQL; JSON property names stay scoped in the descriptor.
- `tests/nextorm.sqlserver.tests/JsonStreamingSqlLoweringTests.cs` — regression `SameNameLeavesAcrossScopes_ShouldAliasTheDuplicateOutput`.

D176.6 (ACT-committed, CRLF):

- `benchmarks/nextorm.benchmark/SqliteBenchmarkJsonStreamPhase2.cs` — new C08 `json-stream-phase2` category: SQLite flat/nested/conditional-null/joined-slot/`byte[]` streaming vs materialize+STJ, plus writer-level native `int[]`/jagged workloads over a fake `IDataRecord`.
- `src/nextorm.core/nextorm.core.csproj` — `InternalsVisibleTo("nextorm.benchmark")` (benchmark-only; lets C08 drive the internal recursive writer's array path without a provider container).
- `docs/guide/28-streaming-data.md`, `docs/ru/guide/28-streaming-data.md` — public JSON-streaming guide (EN + RU): nested shapes, `Item1`/`Item2`, native/jagged arrays, Base64, null semantics, conditional construction, explicit exclusions/deferrals.
- `docs/specs/roadmap/todo_json_streaming.md` — phase-2 implemented interpretation and remaining issue boundaries.
- `docs/specs/status/evidence/rc2-176/r2-n1/` — D176.6 evidence tree (DocFX, perf C07/C08, mutation manifest + patches, coverage C05/C06, variants/ledger, verification, inner-loop).

## Pointers

- Issue: #176. Brief source: `/tmp/opencode/rc2/briefs.md` §#176.
- Roadmap: `docs/specs/roadmap/todo_json_streaming.md:60-64,199-212,281`.
- Plan revision r=2; cycle N=1; rv=2; ACT complete — CHECK PASS r=2/rv=2 (evidence-only re-gather, n unchanged); no further DO.

## Progress log

```text
2026-10-07T13:09:53Z | PLAN | revision 1 | iteration 1/3 | PLAN ready — awaiting confirmation | plan_state=ready, rv=1
2026-10-08T17:48:13Z | DO | revision 1 | iteration 1/3 | D176.1 baseline: C07 exit 0, 7 cases, wall 74s; C01 exit 0 0/0; C02 exit 0 1789 passed/0 failed/0 skipped | docs/specs/status/evidence/rc2-176/r1-n1/{baseline-acceptance,build-debug,core-tests}.txt
2026-10-08T17:52:50Z | DO | revision 1 | iteration 1/3 | D176.1 spike done (probe A/B, 4/4 inner tests, gate report exit 0): nested binding absent, join slots Slot0 ords0-2 / Slot1 ords3-5, unmatched-slot raw DBNull observed, duplicate-Id global guard blocks parity; fallback JSON-only preparer + presence reuse recorded for D176.2 | docs/specs/status/evidence/rc2-176/r1-n1/spike.md
2026-10-08T18:02:58Z | DO | revision 1 | iteration 1/3 | D176.2 done: JSON-only recursive shape capture + scalar ordinal binding + slot grouping (Slot0 ords[0,1], Slot1 ords[2,3,4,5]) + presence metadata + array nodes; C01 exit 0 0/0; inner sqlite JsonStreamingTests 24/0/0, clickhouse decimal 1/0/0, core InMemoryTests 159/0/0; boundary C02 exit 0 1789 passed/0 failed/0 skipped; throwaway probe 6/6 deleted; validate_inner_loop report exit 0; scoped git diff --check exit 0 | docs/specs/status/evidence/rc2-176/r1-n1/d176.2/{inner-sqlite-jsonstream,inner-clickhouse-decimal,inner-core-inmemory,boundary-core-tests,probe-d176.2}.txt, inner-loop/evidence-d176.2.json
2026-10-08T18:20:16Z | DO | revision 1 | iteration 1/3 | D176.3 done (final2, comment-only edit rebuilt): recursive shape writer (object/array recursion, per-object scoped names, construction/any-column presence, byte[]-before-array Base64, depth/rank/element guards) replaces the D176.2 fail-closed guard in JsonRowWriterFactory; tests added only (17 core direct-writer + 4 SQLite nested/whole-entity), comprehensive suite deferred to D176.4; C01 nextorm.slnx exit 0 0 warnings/0 errors; inner core JsonShapeWriterTests 17/0/0, sqlite JsonStreamingTests 28/0/0 (exit 0); boundary C02 1806 passed/0 failed/0 skipped, sqlite namespace-filtered 1186 passed/0 failed/1 skipped (exit 0); validate_inner_loop report exit 0; scoped git diff --check exit 0; CRLF preserved; no throwaway files | docs/specs/status/evidence/rc2-176/r1-n1/d176.3/{build-slnx,inner-core-jsonshapewriter,inner-sqlite-jsonstream,boundary-core-tests,boundary-sqlite-tests}.txt, inner-loop/evidence-d176.3.json
2026-10-08T18:44:33Z | DO | revision 1 | iteration 1/3 | D176.4 done: comprehensive boundary suite (core writer 32, SQLite end-to-end 43, dialect SQL-lowering +2 each in postgres/sqlserver/mysql/clickhouse), both terminal surfaces sync+async, join/slot/duplicate-name/cache/lifecycle regressions, STJ oracle; test exposed and fixed a real defect in the actual Projection<T1,T2> terminal (bare join flattened to duplicate names) via BuildJsonProjectionShape slot grouping in QueryCommand.QueryPreparer.cs; C01 build exit 0 0/0; boundary core 1821/0/0, sqlite namespace 1201 passed/0 failed/1 skipped, postgres 813/0/0, sqlserver 737/0/0, mysql 313/0/0, clickhouse 601/0/0; validate_inner_loop report exit 0; scoped git diff --check exit 0; CRLF; no throwaway files; DO→PLAN candidate: null-valued conditional nested construction unsupported (QueryPreparer.cs:844,901) | docs/specs/status/evidence/rc2-176/r1-n1/d176.4/{defects.md,probe-d176.4-*.txt,build-slnx.txt,inner-*,boundary-*}.txt, inner-loop/evidence-d176.4.json
2026-10-08T18:50:55Z | DO | revision 2 | iteration 1/3 | Replanned: r=2 — additive unit D176.7 closes unsupported null-valued conditional nested construction (Additive prerequisite; original criteria preserved) | docs/specs/status/evidence/rc2-176/r1-n1/d176.4/defects.md
2026-10-08T19:01:15Z | DO | revision 2 | iteration 1/3 | D176.7 done: JSON-only conditional-construction capture (root + nested member) lowers the New/MemberInit arm to a scoped object plus a hidden nullable sentinel CASE column consumed by the existing AnyColumnNotNull presence; both null-arm orientations, all-null non-null construction, MemberInit, sync+async; both-arms / construction-vs-non-null-arm / untranslatable-predicate rejected fail-closed; hidden column never a JSON member; ordinary materialization and sticky Cache unchanged; C01 build exit 0 0/0; inner core JsonShapeWriterTests 34/0/0, sqlite JsonStreamingTests 53/0/0; boundary core 1823/0/0, sqlite namespace 1211 passed/0 failed/1 skipped (unrelated LOB capability probe); validate_inner_loop report exit 0; scoped git diff --check exit 0; CRLF; no throwaway files | docs/specs/status/evidence/rc2-176/r2-n1/{E176.13.log,E176.13-cases.md,E176.14.log,E176.14-cases.md,boundary-core-tests.txt,boundary-sqlite-tests.txt,build-slnx.txt,inner-core-jsonshapewriter.txt,inner-sqlite-jsonstream.txt}, r2-n1/inner-loop/{scope-d176.7.json,evidence-d176.7.json}
2026-10-08T19:27:50Z | DO | revision 2 | iteration 1/3 | D176.5 done (integration layer): shared nested/object/slot/conditional + both-terminal cases in CommonTestSuite.JsonStreamNested.cs over SQLite/PostgreSQL/SQL Server/MySQL, explicit MariaDB binary_entity mirrors, ClickHouse mirrors + local slot fixture + native/jagged array facts, PostgreSQL array_agg native source; inner filtered container subset FullyQualifiedName~WriteJson exit 0 159 passed/0 failed/0 skipped; comprehensive C04 sweep exit 0 3476 total/3279 passed/0 failed/197 capability skips with positive executed counts SQLite 702, PostgreSQL 839, SQL Server 704, MySQL 612, MariaDB 76, ClickHouse 225; SQL Server defect fixed (Ambiguous column name 'id' on same-name scopes) via DisambiguateJsonLeafAliases in QueryCommand.QueryPreparer.cs + regression in JsonStreamingSqlLoweringTests; build exit 0 0/0; validate_inner_loop report exit 0; scoped git diff --check exit 0; CRLF; no throwaway files | docs/specs/status/evidence/rc2-176/r2-n1/integration/{c04-sweep.log,c04-sweep.xml,provider-counts.md,setup-recovery.log,core-writer.txt,sqlite-unit.txt,sqlserver-lowering.txt,inner-integration-sqlite.txt,inner-integration-all-providers.txt,build-slnx.txt}, r2-n1/inner-loop/{scope-d176.5.json,evidence-d176.5.json}
2026-10-08T19:47:00Z | DO | revision 2 | iteration 1/3 | D176.6 done (docs/perf/mutation/coverage/ledger): EN+RU guide 28 + roadmap updated; DocFX C09 exit 0 (2 pre-existing source-generator warnings); C07 acceptance exit 0 7 cases wall 47s cached/prepared ratio 2.09 vs baseline 2.20 (-4.6%, under 5%, no investigation); C08 json-stream-phase2 added exit 0 24 cases wall 187s, no whole-result buffering (conditional 50.4KB flat at 1k/10k rows); 6/6 manual mutations killed + restored; C05/C06 exit 0 line 86.8%/branch 79.3% (>=85/75 pass; per-assembly postgres 79.9% line below target recorded honestly); variants.md + verification.md + ledger written; validate_inner_loop brief/report exit 0; scoped git diff --check exit 0; CRLF; no throwaway files | docs/specs/status/evidence/rc2-176/r2-n1/{docfx.txt,perf/{acceptance-postchange,c08-json-stream-phase2}.txt,perf/perf-notes.md,mutation/manifest.md,coverage/{c05-coverage-collect.txt,c06-reportgenerator.txt,coverage-notes.md},variants.md,verification.md,inner-loop/{scope-d176.6.json,evidence-d176.6.json,brief-d176.6.log,report-d176.6.log}}
2026-10-09T01:25:33Z | DO | revision 2 | iteration 2/3 | CHECK r2-n2 loop-back closed: W2/W3/W5/W6/W7/W8 source fixes + W4/T1/T2 guard tests + T3/T4/T5/T6 integration/unit tests + T7 per-symbol E176.13/E176.14 logs; build exit 0 0/0; inner core JsonShapeWriterTests 40/40, sqlite JsonStreamingTests 63/63, dialect lowering 2/3/2/2; boundary core 1829/0/0, sqlite namespace 1222 total/1221 passed/1 skipped, postgres 813/0/0, sqlserver 738/0/0, mysql 313/0/0, clickhouse 601/0/0; container WriteJson 194/0/0 with all six providers positive (SQLite 32/PostgreSQL 34/SQLServer 32/MySQL 32/MariaDB 32/ClickHouse 32), full sweep 3511 total/0 failed/197 capability skips; validate_inner_loop report exit 0; scoped git diff --check exit 0; CRLF; no throwaway files; plan r=2 rv=2 n=2/3 (no bump) | docs/specs/status/evidence/rc2-176/r2-n1/{loopback/{dispositions.md,w4-probe.md,w4-probe.log,provider-counts.md,build-slnx.txt,inner-*,boundary-*,integration-writejson.txt,integration-full-sweep.{log,xml}},E176.13.log,E176.14.log,E176.13-cases.md,E176.14-cases.md,inner-loop/{evidence-d176.7-loopback.json,report-d176.7-loopback.log}}
2026-10-09T01:36:43Z | DO | revision 2 | iteration 2/3 | E176.08/09/10 refreshed on final r=2 n=2 tree (loop-back): C01 build exit 0 0/0; C05/C06 exit 0 line 86.8% (48991/56414) branch 79.3% (26435/33311) >=85/75 pass (per-assembly postgres 79.8%/sqlserver 81.5% line below 85 recorded honestly); C07 exit 0 7 cases wall 61s ratio 2.04 vs baseline 2.20 (-7.3%, favorable, repro 2.01, no case regressed); C08 re-run exit 0 24 cases wall 187s no whole-result buffering (conditional 50.68KB/50.77KB at 1k/10k rows); 6/6 mutations killed + restored (no target changed; core 40/40, sqlite 63/63); scoped git diff --check exit 0; CRLF; no r/rv bump | docs/specs/status/evidence/rc2-176/r2-n1/{coverage/{build-debug.txt,c05-coverage-collect.txt,c06-reportgenerator.txt,coverage-notes.md},perf/{acceptance-postchange.txt,ratio-repro.txt,c08-json-stream-phase2.txt,perf-notes.md},mutation/{manifest.md,mut1..mut6-*.txt,mut1..mut6-*.patch},variants.md,verification.md}
2026-10-09T01:43:23Z | DO | revision 2 | iteration 2/3 | Evidence-bookkeeping reconciliation (no r/rv/n bump): E176.13/E176.14 invocation labels corrected to C14+C15 per-class -result-xml runs; E176.13 hidden-sentinel row re-mapped to E176.14; integration/integration-writejson.txt added + indexed (canonical current, loopback copy retained); root inner-*.txt marked STALE superseded by loopback 40/63; n=1 provider-counts labelled pre-loopback, loopback n=2 current; W1 recorded unverified+accepted with 11,208 B/call boxing measurement; N-vs-n counter note added; scoped git diff --check exit 0; no throwaway files | docs/specs/status/rc2-176-json-nested-1.md, docs/specs/status/evidence/rc2-176/r2-n1/{E176.13-cases.md,E176.14-cases.md,variants.md,integration/{integration-writejson.txt,provider-counts.md},loopback/{provider-counts.md,dispositions.md},inner-core-jsonshapewriter.txt,inner-sqlite-jsonstream.txt}
2026-10-09T01:46:22Z | ACT | revision 2 | iteration 2/3 | Finalized: CHECK PASS r=2/rv=2 (evidence-only re-gather, n unchanged), 6/6 mutants killed, coverage 86.8/79.3, container 3511/0/197, issue #176 closed; phase=ACT execution_state=complete; no r/rv/n bump | docs/specs/status/rc2-176-json-nested-1.md
```
