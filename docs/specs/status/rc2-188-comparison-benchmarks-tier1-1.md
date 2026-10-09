# D188 — Tier-1 cross-library comparison benchmarks

```yaml
task_id: D188
issue: "#188 — Comparison benchmarks tier 1: projection, aggregates, paging, streaming + cross-library JSON/CSV"
milestone: 1.0.9-rc2
selected_variant: "SQLite; matched query workloads; explicit reuse categories; shared observable sinks"
base: "branch 1.0.9-rc2 @ 18659e41 (collection-assigned; do not treat as final DO identity)"
cycle: D188
plan_revision: r1
plan_state: ready
phase: incomplete
artifact_root: benchmarks/BenchmarkDotNet.Artifacts/D188/r1
provenance:
  - "Issue #188 and supplied scout evidence"
  - "Existing docs/specs/performance/comparison-benchmark-scenarios.md"
  - "D189 is a predecessor for SQLite zero-materialization"
status_file: docs/specs/status/rc2-188-comparison-benchmarks-tier1-1.md
```

`ready` means **ready for the collection ALL-barrier and scheduling**, not permission to start implementation during collection phase P. Coder persists this plan verbatim. No implementation, benchmark execution, commit, or push occurs in P.

## Goal (in essence)

Make the public comparison substantiate how the four libraries behave on representative SQLite query and output workloads currently missing from the comparison.

The result must distinguish:

- SQL workload from client-side processing.
- Reused/compiled queries from ordinary cached queries.
- DTO materialization from reading rows without entity/DTO materialization.
- Native Nextorm output from other libraries' **closest equivalent: materialize → serialize**.

This is benchmark instrumentation and reporting, not a change to ORM behavior or a requirement to make Nextorm win.

## Scope

**Fix now:**

- Four new classes:
  - `SqliteBenchmarkProjection`
  - `SqliteBenchmarkAggregates`
  - `SqliteBenchmarkPaging`
  - `SqliteBenchmarkStreaming`
- Extend existing `SqliteBenchmarkWriteJson` and `SqliteBenchmarkCsv`.
- Shared row sink/BDN `Consumer`, semantic validation, bounded executed runs, retained reports.
- Update the existing performance spec and both language versions of the public comparison and linq2db scenario claim.

**Deferred, with triggers:**

| Exclusion | Trigger for a separate planned extension |
|---|---|
| LOB `ToStream` / `ToTextReader` | Agreed LOB benchmark issue and representative LOB fixture |
| DML and navigation/eager loading comparisons | Tier-2 comparison issue |
| Additional providers | Agreed cross-provider benchmark scope and fixtures |
| Full-duration statistical/ranking campaign | Request to publish statistically supported rankings |
| Extra synchronous query counterparts outside reader streaming | Request for a synchronous comparison campaign |
| Compiled/prepared cross-library serialization category | Request to compare reusable serialization pipelines; add all applicable Category A counterparts together |

D189's zero-materialization dependency is **not deferred scope**. It remains an active, predecessor-blocked part of D188. #190 is not required.

## Acceptance criteria (each with a negative case)

| ID | Acceptance criterion | Explicit negative case |
|---|---|---|
| AC01 | The benchmark project builds in Release with project warning/error policy; all six affected classes are discoverable through their class filters and carry `[MemoryDiagnoser]`. | A missing class, warning-as-error, missing diagnoser, or filter selecting no benchmarks fails. |
| AC02 | Projection, aggregates and paging execute equivalent workloads across the four libraries. Applicable Category A arms pair Nextorm preparation with EF and linq2db compiled-query reuse; Category B uses ordinary/cached forms. | Client-side aggregation, different filters/order/limits, mislabeled compilation, or unmatched reuse claims fail. |
| AC03 | Aggregates and paging use `large_table`; observed fixture counts and effective paging parameters are recorded. Results are validated outside timed operations. | Inferring 10,000 rows from prose, substituting `simple_entity`, empty paging caused by unsuitable parameters, or result disagreement fails. |
| AC04 | Streaming compares buffered DTOs, unbuffered DTO enumeration, and non-LOB raw-reader consumption using the same logical row workload and observable sink. Zero arms have no per-row entity/DTO construction or collection accumulation. | An empty loop, unconsumed reader, different selected columns, DTO construction in a zero arm, or undisposed resources fails. |
| AC05 | SQLite Nextorm zero-materialization consumes verified D189 behavior. Dapper and linq2db raw-reader counterparts run; EF is explicitly excluded from that subgroup. | Omitting the Nextorm zero arm, substituting another provider, silently falling back to materialization, or presenting EF LINQ as a raw-reader arm fails. |
| AC06 | JSON and CSV contain Dapper, linq2db and EF materialize→serialize counterparts, preserve applicable existing Nextorm arms, and validate equivalent logical output. | Different rows/options, unconsumed output, incorrectly claiming zero-materialization parity, or dropping existing coverage without justification fails. |
| AC07 | Each of the four new and two modified classes has an executed filtered ShortRun, zero benchmark failures, allocation/GC diagnostics, a saved report and log. | Build-only evidence, a timeout, zero executed cases, missing report/diagnostics, or BDN failure fails. |
| AC08 | Public EN/RU comparison pages, internal EN/RU linq2db scenario claims, and the existing performance spec accurately describe executed coverage, dataset counts, reuse and limitations. | Missing mirror, unsupported result claim, duplicate spec, broken report link, or public link into `docs/specs/**` fails. |
| AC09 | Scope stays within benchmark instrumentation/docs/artifacts; existing acceptance regression is checked where applicable. | Production query-path changes hidden inside this task, acceptance failures, sticky cache mutation, or unrelated fixture/package edits fail. |
| AC10 | Evidence is complete against the contract, including predecessor, applicability decisions, test scope and run identity. | A missing report is treated as incomplete evidence—not success, an automatic replan, or permission to omit an arm. |

## Minimal solution / alternatives

**What is essential?** Equivalent SQL/result workloads, explicit reuse categories, observable consumption and executed evidence.

**What cannot change?** Agreed tier-1 scope, SQLite zero-arm requirement, existing production semantics, EN/RU obligations and honest closest-equivalent reporting.

**What is optimal within those constraints?** Extend the current stand with four classes and small benchmark-only helpers; validate semantics in setup and measure only the intended operations.

| Approach | Advantages | Disadvantages / cost / risk |
|---|---|---|
| **Extend the existing stand — selected** | Small footprint; preserves configuration, fixtures and documentation conventions | Requires explicit validation because fixture size and some terminal capabilities are unverified |
| General benchmark abstraction framework | Could reduce repeated setup | Larger design and abstraction overhead; risks measuring adapters instead of libraries |
| Implement materialized comparisons now and omit zero arms | Earlier partial results | Does not satisfy #188; rejected as a completion strategy |

Implementation decisions:

- Use the existing constructor/repository patterns.
- Use a named, non-LOB DTO projection shared logically across libraries.
- Use deterministic ordering with a unique tie-breaker for paging and ordered output.
- Perform correctness checks in setup, outside measured bodies.
- Use async database execution for the principal comparison campaign. Reader streaming also includes matched synchronous reader arms.
- Do not introduce a Category A claim unless all applicable reusable-query counterparts are represented.
- Preserve existing native Nextorm JSON/CSV arms; separate them visibly from materialize→serialize competitors.
- "Zero-materialization" means **no entity/DTO materialization**, not zero allocation.

## Ordered tasks (fix now)

All tasks are active. Only the zero-arm portion of D05 is blocked on D189.

| Task | Action and evidence anchor |
|---|---|
| **D01 — Resolve prerequisites and pin capabilities** | Read the existing spec and applicable `pdca-dotnet` / `nextorm-pdca` contracts; inspect the inner-loop validator's documented invocation/schema; use Roslyn for the required C# APIs, overloads and compiled-query capabilities. Verify issue URL/milestone and the collection base. Observe fixture counts and runtime location without reseeding. Anchors: `BenchDb.cs:5–19`, csproj `:26–28`, `SqliteBenchmarkFeaturesFair.cs:239–268`, `Linq2DbDataContext.cs:46–216`. Record exact runner argv before its execution. |
| **D02 — Extend and review the existing written spec** | Update `docs/specs/performance/comparison-benchmark-scenarios.md`, not a duplicate. Specify arms, query/result equivalence, timing boundaries, parameters, serialization semantics, category applicability and D189 dependency. Existing discrepancy anchor: spec `:93`. Obtain in-cycle design review before implementation; autonomous mode does not require user `go`. |
| **D03 — Shared benchmark-only instrumentation** | Add `BenchmarkRowSink.cs`, `BenchmarkComparisonValidation.cs`, and `BenchmarkSerializationSink.cs`. Define observable consumption, semantic comparisons, output sink and deterministic resource cleanup. No production helper/API changes. |
| **D04 — Projection, aggregates and paging** | Add the three new classes. Aggregates include SQL-side Count, Sum and GroupBy→Count. Paging uses the large fixture and stable ordering. Reuse patterns: `SqliteBenchmarkWhere.cs:68–244`; category patterns: `SqliteBenchmarkFeaturesFair.cs`. |
| **D05 — Streaming, including predecessor-blocked zero arms** | Add `SqliteBenchmarkStreaming.cs`. Implement buffered DTO and async DTO enumeration arms. Consume D189's verified SQLite reader behavior before implementing/accepting Nextorm zero arms. Add matched Dapper/linq2db reader arms and shared consumption; no EF zero arm. Current empty-loop instrumentation is not copied. |
| **D06 — Cross-library JSON/CSV** | Modify `SqliteBenchmarkWriteJson.cs:108–168` and `SqliteBenchmarkCsv.cs:59–95`; add three competitor materialize→serialize paths with matched output semantics and a non-retaining output sink. Preserve existing Nextorm coverage. |
| **D07 — Execute, retain and document** | Build, execute six filtered runs, validate the evidence set, run applicable acceptance regression, retain reports/logs, update EN/RU docs and claims. Report actual fixture counts and ShortRun limitations. |
| **D08 — CHECK handoff** | Audit footprint, category/variant decisions, CRLF, artifacts and contract completeness. Leave every unfinished task active. No success claim before CHECK. |

D01 is an **additive prerequisite**, not replacement of D188. Discovery of a genuine production requirement returns to PLAN; it does not authorize an unplanned engine edit.

## Variant matrix (derived from the execution path)

Execution path:

**fixture → library/reuse mode → SQL terminal → optional DTO materialization → row/output sink → consumer → cleanup**

Legend:

- **T — test:** executed BDN case with setup validation and report evidence.
- **G — guard:** explicit, observable non-applicability; no benchmark claim.
- **D — deferred:** trigger stated below.
- "Compiled" means the library's real public compiled-query mechanism, not a cached delegate around ordinary execution.

### Async database execution

| Shape | Nextorm prepared | Nextorm cached | Dapper | linq2db regular | linq2db compiled | EF regular | EF compiled |
|---|---:|---:|---:|---:|---:|---:|---:|
| DTO projection | T | T | T | T | T | T | T |
| SQL Count | T | T | T | T | T | T | T |
| SQL Sum | T | T | T | T | T | T | T |
| SQL GroupBy→Count | T | T | T | T | T | T | T |
| Ordered paging | T | T | T | T | T | T | T |
| Buffered DTO list | T | T | T | T | T | T | T |
| Unbuffered DTO enumeration | T | T | T | T | T | T | T |
| Raw-reader, no DTOs | G1 | T¹ | T | T | G2 | G3 | G3 |
| JSON output | D1² | T² | T | T | D1 | T | D1 |
| CSV output | D1² | T² | T | T | D1 | T | D1 |

¹ Blocked on D189; cached means the normal applicable query path, without mutating the shared command cache flag.

² Preserve and execute existing native Nextorm arms regardless of their current reuse form. The deferred designation concerns **adding a cross-library Category A serialization comparison**, not removing an existing prepared arm.

- **G1:** A separate prepared-reader arm is not assumed from D189's terminal support. D01 determines public applicability. If applicable, activate an additional test row with a stable ID before implementation; otherwise record the API-based guard. The mandatory native Nextorm reader arm remains.
- **G2:** Raw-reader SQL execution is not a LINQ compiled-query comparison.
- **G3:** EF LINQ provides no agreed raw-reader counterpart; EF is excluded from zero-materialization.
- **D1 trigger:** an agreed reusable serialization comparison; introduce Nextorm prepared, EF compiled and linq2db compiled counterparts together.
- If a required **T** capability is unsupported, obtain PLAN review. Do not silently convert it to a guard or ordinary query.

### Synchronous database execution

| Shape / arms | Closure |
|---|---|
| Projection, aggregates, paging; all library/reuse arms | **D:** trigger = agreed synchronous comparison campaign |
| Buffered/unbuffered DTO streaming; all applicable arms | **D:** same trigger; do not compare them against async-only zero arms |
| Raw reader: Nextorm native, Dapper, linq2db regular | **T:** separate matched synchronous subgroup; Nextorm depends on D189 |
| Raw reader: separate Nextorm prepared form | **G1**, with the same capability predicate |
| Raw reader: linq2db compiled, EF regular/compiled | **G2/G3** |
| JSON/CSV synchronous database counterparts | **D:** synchronous campaign; synchronous serialization inside the selected async pipeline is still measured |

### Inputs, results and flags

| Variant | Closure and negative case |
|---|---|
| Nonempty fixture, selected scalar value/reference fields | **T:** validate equivalent values and counts; differing projections fail |
| Empty aggregate/group/page result | **T:** setup probes validate Count=0, agreed Sum empty semantics, empty grouping/page; provider mismatch fails |
| Nullable values/default scalar values | **T:** helper/sink/serializer probes; database nullable-field probes where the chosen mapped field exists. **G:** a database-null branch only if schema inspection proves no such selected field |
| JSON/CSV escaping, Unicode, null/default values | **T:** deterministic in-memory validation probes; malformed or semantically different output fails |
| Paging first/interior/beyond-end and ties | **T:** setup probes; measured interior page must be nonempty. Missing unique tie-breaker fails |
| Prepared/compiled vs ordinary reuse | **T:** lifecycle and category audit; compilation/preparation inside the measured body fails the declared reuse category |
| Dapper buffered/unbuffered flag | **T:** explicit terminal selection; an unbuffered label on buffered execution fails |
| EF tracking | **T:** use an explicit consistent no-tracking workload where applicable; record DTO tracking non-applicability |
| Connection lifetime, sink reset, reader/enumerator disposal | **T:** setup/cleanup audit and executed runs; leaked or cumulative work fails |
| Other providers, LOB and cancellation-specific scenarios | **D:** corresponding provider/LOB/cancellation benchmark issue |

## Footprint (expected + uncertainty)

Paths below are under `benchmarks/nextorm.benchmark/` unless stated otherwise.

**New files:**

- `SqliteBenchmarkProjection.cs`
- `SqliteBenchmarkAggregates.cs`
- `SqliteBenchmarkPaging.cs`
- `SqliteBenchmarkStreaming.cs`
- `BenchmarkRowSink.cs`
- `BenchmarkComparisonValidation.cs`
- `BenchmarkSerializationSink.cs`

**Modified files:**

- `SqliteBenchmarkWriteJson.cs`
- `SqliteBenchmarkCsv.cs`
- `docs/specs/performance/comparison-benchmark-scenarios.md`
- `docs/comparisons/benchmarks.md`
- `docs/ru/comparisons/benchmarks.md`
- `docs/specs/comparison/linq2db-comparison.md`
- `docs/specs/ru/comparison/linq2db-comparison.md`

**Status/evidence:**

- `docs/specs/status/rc2-188-comparison-benchmarks-tier1-1.md`
- `benchmarks/BenchmarkDotNet.Artifacts/D188/r1/**`
- BDN-generated `benchmarks/BenchmarkDotNet.Artifacts/results/*SqliteBenchmark{Projection,Aggregates,Paging,Streaming,WriteJson,Csv}*`
- Corresponding generated logs retained and indexed in the task artifact directory.

**Uncertainty bounded by D01:**

- A benchmark-only DTO or mapped large-table field may require a small addition to an existing benchmark model/repository file. Record the exact path before editing.
- Existing constructor/setup details may require local adjustments in the two modified classes.
- No `Program.cs`, shared config, acceptance harness or package edit is expected.

The csproj includes new `*.cs` through SDK globs: **no csproj edit is needed**. No `src/**`, `tests/**`, fixture database, SQL seed or package-version edit is planned.

## Predecessor-result requirements

**D189 supplies:**

1. Its result/status artifact identifying the implemented revision and successful CHECK.
2. SQLite support for Nextorm non-LOB projections through `ToDataReader` and `ToDataReaderAsync`.
3. Executed evidence that the reader returns the selected scalar columns/rows correctly and can be consumed and disposed in sync and async forms.

D188 consumes the API implementation and behavior—not D189 benchmark timing.

At integration, D01/D05 verify the predecessor is actually present in the assigned working base and run D188's own sink-based validation.

**Incomplete predecessor handling:**

- Missing sync/async support, unsupported selected non-LOB columns, incorrect results or missing validation leaves the zero-arm unit **blocked and active**.
- Other D188 work may proceed if the collection schedule permits.
- No fallback to `ToList`, omitted arm, "SQLite unavailable" pass or materialization mislabeled as zero.
- Return to PLAN with a new `P:` task describing the observed prerequisite defect. The original D05 criteria and remainder stay unchanged.
- An external blocker requires orchestrator escalation; an unproven blocker first receives targeted scout investigation.

#190 contributes no required artifact or behavior.

## Assumptions / prerequisites

| What the statement did not establish | Closure |
|---|---|
| Actual `large_table` size and active fixture path | **Prerequisite:** observe both at runtime. `BenchDb.cs:5–19` selects environment/tmp/cwd paths; do not assume the tracked file is the opened file. |
| Meaning of "~10k" vs small-table invariants | **Evidence:** tracked `data/test.db`; simple=10/complex=3 setup invariants; large-table fill is commented at `SqliteBenchmarkLargeIteration.cs:74–80`. Record distinct table populations and the existing discrepancy; do not reseed silently. |
| Exact selected fields, grouping key and paging parameters | **Prerequisite:** D01 schema/mapping inspection; D02 fixes the common workload. Use non-LOB fields, deterministic grouping and a unique ordering tie-breaker. |
| Async/compiled terminal API applicability | **Prerequisite:** Roslyn-backed capability inventory for pinned versions; no inferred symbols or overloads. |
| Serializer, CSV dialect and output comparison | **Design choice:** agree matching names/order/null/escaping/delimiter/newline rules in D02. Use semantic validation where native formatting differs; document differences rather than falsifying byte equivalence. |
| Exact validator CLI/schema | **Prerequisite:** inspect script documentation/help and pin real argv. Do not invent options. |
| Full local skill obligations | **Prerequisite:** D01 verifies both contracts and records applicable gates. Additional unconditional obligations are added before implementation; none are waived. |
| Verified GitHub URL and immutable collection base | **Prerequisite:** record repository-backed issue URL and assigned base; no fabricated URL/SHA. |
| Machine/runtime comparability | **Assumption/risk:** execute the comparison on one identified host/runtime/configuration and report it. ShortRun supports runnable coverage and indicative measurements, not definitive rankings. |
| Fixture mutation | **Constraint:** read-only workload; no database rewrite or uncontrolled seed operation. |

Pinned environment: .NET 10; BDN 0.15.8; Dapper 2.1.86; linq2db/SQLite 6.5.0; Microsoft.Data.Sqlite and EF Core 10.0.12; System.Linq.Async 7.0.1. CPM stays unchanged.

BDN artifacts are git-tracked and publicly linked. Retain relevant new reports; do not clear unrelated artifact history.

## Test strategy

Here, **test** means:

1. Release build.
2. Setup-time semantic validation of each benchmark's workload.
3. Actual filtered BDN ShortRun for every affected class.
4. Zero failed cases and saved allocation/GC reports.
5. Scope/applicability and documentation evidence.

No new library unit/integration tests are expected because no library behavior is changed. Benchmark helper edge cases run as deterministic setup validation, not timed work.

### Planned test-scope JSON

The semantic scope is fixed below. D01 maps it to the validator's real schema without reducing the boundary:

```json
{
  "projects": [
    "benchmarks/nextorm.benchmark/nextorm.benchmark.csproj"
  ],
  "selectors": [
    "--filter *SqliteBenchmarkProjection*",
    "--filter *SqliteBenchmarkAggregates*",
    "--filter *SqliteBenchmarkPaging*",
    "--filter *SqliteBenchmarkStreaming*",
    "--filter *SqliteBenchmarkWriteJson*",
    "--filter *SqliteBenchmarkCsv*"
  ],
  "rebuild": "affected",
  "boundary": "full filtered run of all four new classes and both modified classes",
  "rationale": "Benchmark-instrumentation change; exercise every affected class and all declared arms without changing production query paths."
}
```

The validator's invocation is not presumed to be `dotnet test`: this project is a BDN application. If the validator cannot represent BDN selectors, record its explicit limitation and obtain PLAN review; direct BDN evidence remains mandatory.

### Commands

All commands are **planned**, not executed in P. Run from repository root; save stdout/stderr, exit status and elapsed time.

```bash
dotnet build benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Release

timeout --signal=TERM 720s env NEXTORM_BENCH_FULL=0 \
  dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- \
  --filter '*SqliteBenchmarkProjection*'

timeout --signal=TERM 720s env NEXTORM_BENCH_FULL=0 \
  dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- \
  --filter '*SqliteBenchmarkAggregates*'

timeout --signal=TERM 720s env NEXTORM_BENCH_FULL=0 \
  dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- \
  --filter '*SqliteBenchmarkPaging*'

timeout --signal=TERM 720s env NEXTORM_BENCH_FULL=0 \
  dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- \
  --filter '*SqliteBenchmarkStreaming*'

timeout --signal=TERM 720s env NEXTORM_BENCH_FULL=0 \
  dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- \
  --filter '*SqliteBenchmarkWriteJson*'

timeout --signal=TERM 720s env NEXTORM_BENCH_FULL=0 \
  dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- \
  --filter '*SqliteBenchmarkCsv*'

timeout --signal=TERM 240s env NEXTORM_BENCH_FULL=0 \
  dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- \
  --anyCategories=acceptance
```

Exit 0 alone is insufficient: CHECK must also find executed cases, no BDN failures, validated setup and the corresponding saved reports. Exit 124/137 is failure/incomplete execution, not a passing benchmark.

**Regression boundary:** run the existing seven-case acceptance set as a benchmark regression safeguard; do not add the new heavy classes to its `acceptance` category. It is additionally mandatory if query setup/translation paths are touched. A required production edit triggers replan before execution of that edit, including the relevant unit/provider/integration scope.

**Coverage:** project thresholds remain line ≥85% and branch ≥75% for the configured production coverage set. This benchmark-only footprint does not alter that set or justify claiming new production coverage. Any expanded production footprint activates its coverage obligation. Required container tests may not be presented as green when providers are skipped.

## Docs plan

- Extend the existing performance spec with class/arm inventory, variant applicability, timing boundaries, sinks, predecessor requirements and report links.
- Update:
  - `docs/comparisons/benchmarks.md`
  - `docs/ru/comparisons/benchmarks.md`
  - `docs/specs/comparison/linq2db-comparison.md`
  - `docs/specs/ru/comparison/linq2db-comparison.md`
- State actual dataset counts, Category A/B definitions, raw-reader EF exclusion, closest-equivalent serialization, ShortRun configuration and limitations.
- Public links point to public pages or retained BDN artifacts, **never internal specs**.
- No XML-doc/public API work is expected.
- No generated DocFX content or unrelated numbered-guide changes.

## Performance measurement decision

**Required.** Measurement is the deliverable, not an incidental optimization.

- Baseline: matched Dapper/linq2db/EF arms on the same workload; retain existing Nextorm JSON/CSV arms. No numerical historical baseline is invented.
- Instrument: BDN `[MemoryDiagnoser]`; report `Allocated`, Gen0 and Gen2 where emitted, plus timing and job/environment.
- Use default ShortRun via `NEXTORM_BENCH_FULL=0`; do not silently activate `Job.Default`.
- Build separately, then execute one complete filtered class at a time.
- Budget: **12 minutes per affected class**, maximum **72 minutes** for the six class runs; acceptance set separately capped at **4 minutes**.
- No speedup threshold is imposed. Correctness, complete execution and diagnostic availability are gates.
- If a class exceeds its bound, return to PLAN to change the run partition or workload while preserving every required arm. Do not drop cases or treat a partial report as passing.
- Report ShortRun/InProcess limitations; full statistically supported comparisons remain deferred.

The overlay's ≤4-minute acceptance-set rule applies to the existing acceptance set—not to the six-class comparison campaign.

## Reconnaissance decision

**No prototype run before DO is required to choose the design.** The stand, scope and existing written spec support the selected approach.

**Targeted reconnaissance is required in D01** for:

- Actual fixture path/counts and schema.
- Public terminal/compiled-query applicability.
- Existing serialization semantics.
- Validator CLI/schema and local contract gates.
- D189 integration evidence.

Observable completion: a recorded capability/workload table, observed counts, exact commands and no unexplained required-arm gap.

The first executed class runs are DO validation, not disposable experiments replacing acceptance. A capability conflict receives targeted scout evidence first; persistent low confidence goes to escalation trigger 5.

## Unit execution mode

**Sequential units in one working tree.**

The new files are mostly independent, but shared sink/validation helpers, the spec and docs are common contracts. Establish those first, then implement classes, then measure and document.

No additional worktree is justified by D188 alone. The collection scheduler may isolate a group if its combined footprint requires it.

Collection coordination:

- D189 precedes the zero-arm unit.
- Shared comparison pages, specs and BDN artifact locations must be serialized or assigned a common owner across tasks.
- Do not assume task-level independence merely because C# filenames differ.
- Authorized collection commits are left to collection policy; never push or introduce a merge outside that policy.

## Design checklist

- [ ] Public C# API discovery/references use Roslyn, not text search.
- [ ] Projection DTO and selected non-LOB columns are equivalent.
- [ ] Aggregation/grouping happens in SQLite.
- [ ] Paging order includes a unique tie-breaker.
- [ ] Category A compiles/prepares outside timing; Category B accurately names reuse.
- [ ] No sticky mutation such as shared `queryCommand.Cache = false`.
- [ ] Every row/output is observably consumed; no empty loops.
- [ ] Sink reset, checksums and resource cleanup are deterministic.
- [ ] Zero arms create no entity/DTO collection; allocations are still reported honestly.
- [ ] JSON/CSV timing includes the declared materialize→serialize work.
- [ ] Required setup validation is outside timed operations.
- [ ] No production API/fixture/package edits are hidden in instrumentation.
- [ ] CRLF preserved; warning-as-error policy satisfied.
- [ ] Relevant reports retained; unrelated tracked artifacts untouched.
- [ ] EN/RU claims match executed evidence.

**Priority matrix:** AC01–AC10 and their execution-path invariant rows are **P1 by construction**. CHECK cannot lower them. Informative timing interpretations/rankings are P2. Explicitly deferred extensions are P3 until their trigger activates.

## Evidence contract

**Current contract: rv1.** Stable requirement IDs are AC01–AC10; stable evidence-row IDs follow below.

All sources/artifacts below are **planned**, except the supplied scout anchors. No future implementation `file:line` or test symbols are claimed.

### Shared mandatory bindings

These bindings are part of every row:

- **Revision:** `rv1`.
- **Owner:** named below; coder produces implementation/run evidence, scout supplies facts, CHECK evaluates.
- **Source identity:** assigned base + final working-tree identity, runtime/environment, timestamp and exact argv/invocation.
- **Artifact root A:** `benchmarks/BenchmarkDotNet.Artifacts/D188/r1`.
- **Artifacts per row:** `A/<row-ID>.md` evidence index, referenced logs/reports or structured inspection findings, observed result/exit status and applicability result.
- **Applicability:** evaluated from observable footprint/capability/run facts, never from whether a report happens to exist.
- **Negative cases:** retained explicitly below.
- Command references `BUILD`, `RUN-Projection`, `RUN-Aggregates`, `RUN-Paging`, `RUN-Streaming`, `RUN-WriteJson`, `RUN-Csv`, and `ACCEPTANCE` expand exactly to the commands in Test strategy.

| Row / requirement / priority | Required check and negative case | Expected kinds and sources | Exact command / invocation and expected result | Owner / applicability |
|---|---|---|---|---|
| **EC01 / AC10 / P1** | Resolve local gates, issue/base and validator interface. Negative: invented CLI or unresolved unconditional gate. | Scout facts from skills, script/help, issue metadata and collection assignment; prerequisite ledger | `Task(scout, "D188 EC01: read pdca-dotnet and nextorm-pdca contracts, existing performance spec, scripts/validate_inner_loop.py documentation/schema; return applicable gates and exact supported invocation, verified #188 URL/milestone, and collection base requirements; no code edits or recommendations.")`; `python3 scripts/validate_inner_loop.py --help`; `gh issue view 188 --json number,url,title,milestone`. Help/metadata calls exit 0; findings must resolve the prerequisites. | Scout; **always** |
| **EC02 / AC03 / P1** | Observe actual opened DB, table counts and selected schema. Negative: treating prose as row-count evidence. | Runtime setup observations, mapping/schema facts, fixture identity | `RUN-Aggregates` and `RUN-Paging`; setup must log DB path, simple/complex/large counts and effective parameters, then pass validation. | Coder; **always** |
| **EC03 / AC05 / P1** | Verify D189 integration and both reader forms. Negative: incomplete predecessor/fallback. | D189 result/status and executed predecessor evidence; D188 reader setup results | `Task(scout, "D188 EC03: inspect D189 result and integrated base using Roslyn; identify SQLite non-LOB ToDataReader/ToDataReaderAsync support and its executed evidence, including selected columns, row correctness and disposal; return findings only.")`; then `RUN-Streaming`. Both required forms must be validated. | Scout + coder; **zero-arm implementation**, mandatory for completion |
| **EC04 / AC02,AC04,AC06 / P1** | Written workload/arm contract agrees with scope. Negative: unfair categories or duplicate spec. | Updated existing spec and structured design review | `Task(planner, "Review D188 D02 spec summary against AC01–AC10 and rv1: arm/category parity, timing boundaries, sink semantics, variants, D189 and bounded runs. Return approve or concrete defects; no execution.")`. Explicit approval required before D03–D06 implementation. | Planner; **always** |
| **EC05 / AC01 / P1** | Release build, class attributes and discoverability. Negative: warnings, absent class/diagnoser. | Build log and Roslyn class/attribute inventory | `BUILD`, exit 0 with no warnings-as-errors; `Task(scout, "D188 EC05: use Roslyn to enumerate the six affected benchmark classes, benchmark methods/categories and MemoryDiagnoser attributes; return semantic findings.")`. | Coder + scout; **always** |
| **EC06 / AC02,AC07 / P1** | Complete projection run; matched DTO values and reuse categories. Negative: missing arm or result mismatch. | Setup validations, BDN log, saved timing/allocation report | `RUN-Projection`; exit 0, nonzero executed cases, zero failures, complete expected-arm inventory. | Coder; **always** |
| **EC07 / AC02,AC03,AC07 / P1** | Count/Sum/GroupBy execute SQL-side on large table. Negative: client grouping or differing empty semantics. | Translation/capability findings, setup probes, BDN report/log | `RUN-Aggregates`; exit 0, all required shapes/arms validated and reported. | Coder; **always** |
| **EC08 / AC02,AC03,AC07 / P1** | Stable sort/paging and equivalent pages. Negative: nondeterministic ties or empty measured page. | Parameter/count log, setup page probes, report/log | `RUN-Paging`; exit 0, complete matched arms and validated first/interior/beyond-end probes. | Coder; **always** |
| **EC09 / AC04,AC05,AC07 / P1** | Full vs zero streaming with observable sink. Negative: elided loop, materialized zero arm or missing reader form. | Sink/code-path inspection, setup checksums/counts, report with allocation/GC diagnostics | `RUN-Streaming`; exit 0, every required subgroup executes, semantic parity passes, zero-arm audit passes. | Coder; **always for completion** |
| **EC10 / AC06,AC07 / P1** | JSON cross-library output equivalence and honest pipeline label. Negative: different output workload or false zero claim. | Serializer/sink probes, setup comparison, report/log | `RUN-WriteJson`; exit 0, retained native arms plus three competitors execute and validate. | Coder; **always** |
| **EC11 / AC06,AC07 / P1** | CSV equivalence including escaping/null/default rules. Negative: incompatible dialect or unconsumed output. | CSV/sink probes, setup comparison, report/log | `RUN-Csv`; exit 0, retained native arms plus three competitors execute and validate. | Coder; **always** |
| **EC12 / AC09 / P1** | Existing acceptance regression remains bounded and green. Negative: timeout, skipped selected cases or changed category set. | Existing seven-case acceptance report/log | `ACCEPTANCE`; exit 0 within 240 seconds, all seven expected cases execute successfully. | Coder; **this planned benchmark regression**, also mandatory on relevant query-path touch |
| **EC13 / AC10 / P1** | Validator records the fixed test boundary. Negative: `dotnet test` substitute or reduced selectors. | Test-scope JSON, pinned validator argv, output/log | `Task(coder, "D188 EC13: execute scripts/validate_inner_loop.py using the real CLI/schema established by EC01 and the Test strategy JSON; persist exact argv, normalized scope, exit status and log. Do not reduce projects/selectors/rebuild/boundary or substitute build-only evidence.")`. Supported validation must succeed; inability to represent the scope returns to PLAN. | Coder; **always** |
| **EC14 / AC08 / P1** | EN/RU claims and report links reflect actual coverage. Negative: absent mirror, public internal-spec link or missing report. | Five edited documents, report index and link findings | `Task(scout, "D188 EC14: inspect the two public comparison pages, EN/RU linq2db scenario claims and existing performance spec; validate scenario/closest-equivalent/count/job claims against EC06–EC11 and verify links. Check public docs contain no links to docs/specs. Return file:line findings only.")`. No unresolved P1 finding. | Scout; **always** |
| **EC15 / AC02,AC04,AC09,AC10 / P1** | Variant/guard and footprint audit. Negative: silent downgrade, production edit, cache mutation or omitted active unit. | Roslyn impact/capability findings, diff inventory, variant ledger | `git diff --name-only`; `git diff --check`; `Task(scout, "D188 EC15: audit declared arms, capabilities, guards, compiled-query lifecycle, sink consumption, cleanup and production impact using Roslyn; verify active D189-dependent remainder and report footprint deviations.")`. Diff check exit 0; inventory within authorized footprint; every variant closed. | Coder + scout; **always** |
| **EC16 / AC09,AC10 / P1** | Expanded production testing/coverage if the footprint changes. Negative: silent omission or provider-skipped green claim. | Revised scope/contract and project-required test/coverage evidence | `Task(planner, "D188 EC16: production footprint or new unconditional local gate observed; define the additive/revised testing obligation and exact commands before related edits. Preserve AC01–AC10 and evidence IDs.")`. This is a replan gate, not acceptance evidence. | Planner; **observable production-footprint change or newly established unconditional obligation** |

**Contract revision rules:**

- rv1 has no predecessor and supersedes nothing.
- EC01's verified validator argv and any genuinely new applicability fact are pinned before implementation/execution. If they require a contract revision, use **rv2 explicitly superseding rv1**, preserving all IDs and obligations and adding IDs for newly applicable variants.
- Pure clarification does not increase plan revision `r1` or reset attempts.
- Missing evidence alone justifies neither contract revision nor another DO iteration.
- Every completed run is tied to the implementation identity being checked; stale reports are not substitutes.

## CHECK re-gather budget and completeness

Owner: **CHECK**, with orchestrator dispatching scout/coder requests.

Finite budget: **two targeted re-gather rounds**, at most **three requests per round**. Each request names missing row IDs and exact evidence sought.

Re-gather may retrieve existing reports/logs, inspect applicability or obtain focused factual clarification. It may not silently rerun the six-class campaign, change scope, waive a requirement or treat absence of a report as a code defect.

After the budget:

- Actual implementation defect → failed CHECK with a concrete new PLAN/DO correction.
- Evidence still absent → incomplete/non-passing verdict.
- Persistent low-confidence diagnosis → orchestrator calls `escalate`, trigger 5.

CHECK uses P1 priorities unchanged. Completion requires all unconditional rows, all applicable conditional rows, and justified guards/deferred triggers.

## Risks / confidence

**High confidence:** existing stand reuse, file inclusion, required classes/docs, report retention and the need for an observable sink.

**Medium confidence:** practical run duration, dataset suitability and matching native serialization semantics.

**Low-confidence decisions requiring D01 evidence:**

- Exact reusable-query terminal support, particularly asynchronous streaming and aggregates.
- Separate prepared-reader applicability.
- Validator schema/BDN support.
- D189's integrated completeness.

These are not assumed successful. A failed capability probe first receives focused scout investigation; persistent uncertainty triggers escalation 5 even without proving an external blocker.

Other risks:

- Environment-selected fixture differs from tracked `data/test.db`.
- Global BDN outputs overlap another collection task.
- Native serialization formatting differs despite equivalent values.
- ShortRun measurements are overinterpreted.
- D189 lands partially.

**DO → PLAN classification policy:** additive prerequisite → new active dependency with original criteria retained; acceptable assumption → explicit risk without weakened acceptance; true out-of-authority/resource blocker → recommend orchestrator `escalate`; insufficient evidence → scout first. A blocker report or "implementation finished" claim never completes D188.

## Plan self-check

- [x] Goal reconstructed as evidence-producing comparison, not presumed API work.
- [x] Every acceptance criterion includes a negative case.
- [x] Minimal solution, alternatives and constraints stated.
- [x] Execution-path matrix covers libraries/reuse, shapes, sync/async, null/default, input/output and flags.
- [x] Every gap has evidence, an explicit assumption, prerequisite or blocker.
- [x] Exact footprint, sink helpers and SDK-glob implication recorded.
- [x] D189 consumption and fail-closed incomplete-result handling specified; #190 excluded.
- [x] Six complete executed class runs, report obligations and bounded budgets specified.
- [x] Test scope, validator prerequisite and production-expansion gates recorded.
- [x] Existing spec extended; required EN/RU docs included.
- [x] Evidence rv1 has stable IDs, checks, negatives, sources, invocations, expected results, artifacts, owners and applicability.
- [x] CHECK re-gather budget, priorities and revision rules fixed.
- [x] Collection phase P stops at `plan_state=ready`; no implementation or execution performed.

## DO execution record (2026-10-09)

- **escalate decision (2026-10-09):** D188 proceeds; the D05 zero-materialization arm is **BLOCKED and active** — predecessor D189 did not CHECK-pass (EC03 unmet). Only the zero-arm portion of D05 is blocked; its input is "raw reader / zero materialization". Constraints: fail-closed; no `ToList` fallback; no silently omitted arm; no "SQLite unavailable" pass; no materialize→serialize mislabeled as zero; EF excluded from the zero arm (AC05). A non-zero unit inseparable from the zero-arm returns BLOCKED, not decided or forced.
- **phase:** `P` → `DO`; **DO started** (2026-10-09).
- **D01 closed:** fixture observed read-only — `BenchDb.FilePath` → `/tmp/nextorm-bench/test.db` (same schema/counts as tracked `data/test.db`): `simple_entity`=10, `complex_entity`=3, `large_table`=10000. In-repo validator present at `scripts/validate_inner_loop.py`.
- **D03 closed:** added `BenchmarkRowSink.cs`, `BenchmarkComparisonValidation.cs`, `BenchmarkSerializationSink.cs`.
- **D04 closed:** added `SqliteBenchmarkProjection.cs`, `SqliteBenchmarkAggregates.cs`, `SqliteBenchmarkPaging.cs` (`large_table`, A/B categories, setup-time validation).
- **D05 partially closed (non-zero):** added `SqliteBenchmarkStreaming.cs` with buffered + unbuffered DTO arms. The zero/raw-reader subgroup is **not implemented (blocked+active)**; no fallback, no relabel, no EF zero arm.
- **D06 closed:** extended `SqliteBenchmarkWriteJson.cs` and `SqliteBenchmarkCsv.cs` with Dapper/linq2db/EF materialize→serialize arms plus setup-time output-equivalence validation.
- **Build gate:** `dotnet build nextorm.slnx -c Debug` → 0 Warning(s) 0 Error(s); `dotnet build nextorm.slnx -c Release` → 0 Warning(s) 0 Error(s).
- **Not yet closed:** D02 spec extension (deferred), D07 executed six-class sweeps/artifacts (deferred to the next task), D08 CHECK handoff.
- **Journal:** `2026-10-09 | DO | r1 | n=1/3 | compile gate green, non-zero units closed, zero-arm blocked | docs/specs/status/rc2-188-evidence/D188-nonzero.patch`

## DO execution record addendum (2026-10-09, D02 + six-class sweeps)

```yaml
current_cycle: 1
plan_revision: r1
attempt: 1/3
```

- **D02 closed:** extended the existing single spec `docs/specs/performance/comparison-benchmark-scenarios.md` (no duplicate) with the implemented non-zero arm inventory (projection / aggregates / paging / streaming buffered+unbuffered DTO), the JSON/CSV competitor `materialize → serialize` semantics, timing boundaries (setup/correctness outside the timed region), Category A/B applicability, shared sinks, observed fixture counts (`large_table`=10000, `simple_entity`=10, `complex_entity`=3), and ShortRun/InProcess limitations.
  - The zero/raw-reader subgroup is documented as **blocked + active** pending a CHECK-passing #189; no `ToList` fallback, no relabel of a materialized arm, EF excluded from the zero subgroup. #189's unresolved perf predicate is **not** attributed to the D134 `ToDataReader` implementation as a post-D134 regression.
- **D07 (non-zero sweep campaign) executed.** Release build `dotnet build benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Release` → exit 0, 0 Warning(s) / 0 Error(s), log `TestResults/D188/r1/build-release.log`. All six sweeps run from repo root, wrapped in `timeout --signal=TERM 720s env NEXTORM_BENCH_FULL=0` (ShortRun + `InProcessEmitToolchain`), argv per class = `dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- --filter '<class>'`:

| Class filter | exit | executed cases | retained log |
|---|---|---|---|
| `*SqliteBenchmarkProjection*` | 0 | 7 | `TestResults/D188/r1/projection.run.log` |
| `*SqliteBenchmarkAggregates*` | 0 | 21 | `TestResults/D188/r1/aggregates.run.log` |
| `*SqliteBenchmarkPaging*` | 0 | 7 | `TestResults/D188/r1/paging.run.log` |
| `*SqliteBenchmarkStreaming*` | 0 | 13 | `TestResults/D188/r1/streaming.run.log` |
| `*SqliteBenchmarkWriteJson*` | 0 | 30 | `TestResults/D188/r1/writejson.run.log` |
| `*SqliteBenchmarkCsv*` | 0 | 8 | `TestResults/D188/r1/csv.run.log` |

- Zero BDN failures and zero `NA` allocation rows across all six logs; 86/86 cases executed. Total wall ≈ 678 s (< 72 min budget; slowest class `WriteJson` 290 s < 12 min).
- **Artifacts retained** under `TestResults/D188/r1/artifacts/` (per class: BDN `-report-github.md` / `-report.csv` / `-report.html` plus the run log). BDN's native output landed in the tracked root `BenchmarkDotNet.Artifacts/` because `BenchmarkArtifacts.Resolve()` searches for `nextorm.sln` (now `nextorm.slnx`) and falls back to the working directory — a pre-existing path mismatch, not introduced here.
- **Validator:** `python3 scripts/validate_inner_loop.py brief TestResults/D188/r1/test-scope-brief.json` → exit 0; `python3 scripts/validate_inner_loop.py report TestResults/D188/r1/evidence.json` → exit 0 (six `boundary` executions, filter values map 1:1 to the six scope selectors, `selected_count` 7/21/7/13/30/8, all `exit_code` 0).
- **Capability notes (rival arms):** no rival arm was unmeasurable in the non-zero campaign — Dapper / linq2db / EF Core `materialize → serialize` (JSON, CSV) and the Dapper / linq2db DTO forms all executed. The only non-measurable arm is the SQLite zero-materialization / raw-reader subgroup, which is a predecessor capability gap (#189), not a rival-library gap; it is recorded blocked+active, never counted as a skipped/zero-match pass and never substituted.
- **Still open:** D05 zero/raw-reader streaming subgroup **blocked + active** pending #189; D07 public EN/RU docs updates and D08 CHECK handoff remain. `#188` OPEN. No commit, no push.
- **Defect history:** no new defect key in this addendum (build and all six sweeps green). Prior blocker key `D189-zero-materialization` — observed r1 n1, 0 fixes applied, evidence `docs/specs/status/rc2-189-evidence/D189-STOP.patch`, escalation verdict: blocked+active, no fallback.
- **Journal:** `2026-10-09T08:25Z | DO | r1 | n=1/3 | D02 closed; six-class non-zero sweeps 86/86 cases, 0 failures; zero-arm remains blocked+active | TestResults/D188/r1/ (build-release.log, {projection,aggregates,paging,streaming,writejson,csv}.run.log, test-scope-brief.json, evidence.json, artifacts/)

## Terminal-preparation record (2026-10-09, CHECK handoff)

```yaml
current_cycle: 1
plan_revision: r1
attempt: 1/3
evidence_revision: rv1
```

- **Non-zero units closed:** D01 (prerequisites/capability pin), D02 (existing single spec extended, no duplicate), D03 (shared sink/validation helpers), D04 (projection/aggregates/paging), non-zero D05 (buffered + unbuffered DTO streaming), D06 (cross-library JSON/CSV materialize→serialize).
- **D07 (non-zero sweep campaign) closed:** six filtered ShortRun classes executed, **86/86 cases, 0 BDN failures, 0 `NA` allocation rows**; logs/artifacts retained under `TestResults/D188/r1/` and `TestResults/D188/r1/artifacts/`; validator `brief`/`report` exit 0. The D07 **sweep/evidence portion is done**; only its public-docs portion is blocked.
- **Blocked + active (incomplete scope, same milestone `1.0.9-rc2`):**
  - **D05 zero/raw-reader streaming subgroup** — predecessor D189 has not CHECK-passed (EC03 unmet). Not implemented; no `ToList` fallback, no relabel of a materialized arm, EF excluded from the zero subgroup (AC05). Criteria and remainder unchanged; unit stays active, not `superseded`.
  - **D07 public-docs portion** — the EN/RU public comparison pages and the internal EN/RU linq2db scenario claims are **deferred/blocked, same milestone**. No partial public comparison docs are published while the zero-arm set is incomplete; publishing now would misrepresent the incomplete coverage. Public-docs work resumes only after the unblock trigger.
- **D08 CHECK handoff: pending** — no success claim before CHECK; every unfinished unit stays active.
- **Unblock trigger:** D189 successful CHECK, **or** explicit user authorization to change the predecessor prerequisite. The D189→D188 prerequisite is not changed autonomously; a failed dependency is a blocker, not a substitute arm.
- **Issue state:** #188 OPEN. No commit, no push. Plan revision stays `r1`; evidence revision stays `rv1`.
- **Defect history:** key `D189-zero-materialization` — observed r1 n1, 0 fixes applied, evidence `docs/specs/status/rc2-189-evidence/D189-STOP.patch`, escalation verdict: blocked+active, no fallback. No new defect key in this record.
- **Journal:** `2026-10-09T13:30Z | DO | r1 | n=1/3 | terminal-preparation: non-zero units closed (D01–D04, non-zero D05, D06, D07 86/86 cases 0 failures, D02 spec); zero-arm + D07 public docs blocked; D08 CHECK pending | docs/specs/status/rc2-188-evidence/D188-nonzero.patch`

## CHECK verdict and terminal record (2026-10-09)

```yaml
current_cycle: 1
plan_revision: r1
attempt: 1/3
evidence_revision: rv1
phase: incomplete
terminal_status: incomplete
```

- **CHECK verdict: `fail-for-pass`; terminal status `incomplete` is correct.** D188 delivered the non-zero units only; it cannot pass because EC03 (D189 integration / both reader forms, mandatory for completion) is unmet and the zero/raw-reader arm is blocked+active.
- **Reason (predecessor-blocked):** predecessor **D189** is `incomplete` (r=4 paired A/B proven regression, case3 `L=1.0519 > 1.05`; see `docs/specs/status/collection-1.0.9-rc2.md` and `docs/specs/status/rc2-189-sqlite-datareader-1.md`), so the D05 zero/raw-reader streaming subgroup cannot be implemented or validated → **zero-arm blocked + active, EC03 unmet**. No `ToList` fallback, no relabel of a materialized arm, no "SQLite unavailable" pass, no EF in any zero arm. Criteria and remainder unchanged; the unit is active, not `superseded`.
- **Preserved:** plan revision stays `r1`; evidence revision stays `rv1`. Non-zero partial change preserved as patch `docs/specs/status/rc2-188-evidence/D188-nonzero.patch` (142574 bytes / 12 files; excludes the D189 test files, which remain separately uncommitted).
- **D07 public docs deferred (same milestone):** the EN/RU public comparison pages and internal EN/RU linq2db scenario claims remain deferred/blocked because publishing now would misrepresent the incomplete (zero-arm-missing) coverage. Not superseded.
- **D188 public-docs deferral trigger:** D189 **successful CHECK**, **or** explicit user authorization to change the D188→D189 predecessor prerequisite.
- **#188 state: OPEN.** No commit of product/test/benchmark/spec/evidence/artifact files; plan and evidence revisions unchanged.

### Scout-closed safety rows (verified absent)

| Safety check | Scout finding | Status |
|---|---|---|
| Zero/raw-reader arm present | **Absent** | blocked+active (expected) |
| `ToList` fallback in the zero arm | **None** | safe — no silent fallback |
| Materialization mislabeled as zero | **None** | safe — no mislabel |
| EF in any zero arm | **None** (AC05 exclusion honoured) | safe |
| Release build gate | `TestResults/D188/r1/build-release.log` = **0 Warning(s) / 0 Error(s)** | green |

- **Defect history:** key `D189-zero-materialization` — observed r1 n1, 0 fixes applied, evidence `docs/specs/status/rc2-189-evidence/D189-STOP.patch`, escalation verdict: blocked+active, no fallback. No new defect key in this verdict.
- **Journal:** `2026-10-09T13:31Z | CHECK | r1 | n=1/3 | verdict fail-for-pass; terminal incomplete (predecessor-blocked: D189 incomplete → zero-arm blocked+active, EC03 unmet); non-zero partial patch preserved; safety rows scout-closed (zero-arm absent, no ToList fallback, no mislabel, EF excluded); #188 OPEN; D07 public docs deferred | docs/specs/status/rc2-188-evidence/D188-nonzero.patch``
