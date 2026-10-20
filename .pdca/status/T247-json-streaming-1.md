# T247 — oracle-gated JSON streaming optimization

> **2026-10-20 — rc5 acceptance reformulation (policy supersession).** The GitHub issue now uses the two-tier policy: **global** allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db; **local** acceptance = one-axis improvement with no regression on the other (allocations down + time not worse, or time better + allocations not up). This file's frozen thresholds and plan are the historical rc3 attempt. Before any rc5 execution, a new plan revision (`r+1`) must be planned from the reformulated issue; `N`/`r`/`n` are not reset.

## Durable state

- Task: T247; issue: #247.
- Status file: `.pdca/status/T247-json-streaming-1.md`.
- State: PLAN ready (r=1) — collection handoff at PLAN→DO; DO not started; dependency gate T311 pending.
- N=1; r=1; n=1/3.
- Contract revision: rv3. Plan revision and contract revision are separate counters.
- Evidence root: `.pdca/T247/rv3/`.
- Manifest: `.pdca/T247/rv3/manifest.json`.
- Execution: sequential; one implementation tree; lane cap 1.
- CHECK re-gather budget: 2 total, shared across all rows; owner `check`.
- PLAN→DO owner: collection parent.
- Dependency gate: T311 handoff pending (not yet produced).
- Collection: rc3-4; collection baseline ref: `0d4b0cf6`; current HEAD: `0d4b0cf6`.

## Plan (r=1)

### P: actual change of plan

Replace the unsuccessful measurement/retry approach with:
1. a cross-task, pre-registered T311 oracle and working acceptance capture;
2. attribution-based selection of one implementation lever;
3. implementation and verification against every preserved T247 criterion.

This is not a renamed retry. Tasks, dependencies and the basis for selecting a change have changed. No threshold, tolerance, denominator or original acceptance obligation changes.

### Goal and acceptance

Reduce JSON streaming overhead using a lever supported by T311's attribution and pre-registered oracle, while preserving output, public contracts, provider behavior and cache semantics.

Acceptance is the conjunction of R247-01…R247-20 below, including:
- all original performance thresholds, independently in every required round;
- byte-identical output against the control outside timing;
- exact completed-row prefix on failure, destination left open and Flush=0;
- command/plan-cache correctness, shared and bounded delegates, non-colliding keys;
- native/fallback behavior and in-memory fail-closed behavior;
- green tests, build 0 warnings/0 errors, unchanged coverage requirements;
- complete 7/7 acceptance capture and both manifest validators passing.

Negative cases are mandatory: a threshold miss, missing case, invalid round, incompatible reader accepted, key collision, altered partial-output policy, provider omission, unexplained skip, incomplete attribution or required row left open prevents PASS.

### Prior-terminal disposition

Both prior terminal arms are reproducible:
- product-red arm: 1.04546 / 0.91917 versus 0.81079;
- evidence-red arm: 0/7 acceptance cases captured.

The old compare returned 1; acceptance was never executed. Neither arm is reclassified as passing or “only missing a report.”

The new attribution target, pre-registered oracle and gate-integrated capture machinery are the substantive changes. Historical results remain evidence, not substitutes for new paired rounds.

### Dependency decision

**T311 → T247: T311 owns oracle specification, capture writer and residual attribution; T247 consumes them and implements the selected lever.**

This is a cross-task collection dependency, not a new in-cycle T247 prerequisite unit.

T247 DO may start only after the predecessor handoff demonstrates all of:

1. A committed oracle specification, with immutable Git reference, repository path and content hash.
2. Parameters registered before candidate measurements: metrics, units, fitting/model choices, attribution/residual treatment, inputs and acceptance rules.
3. The gate-integrated `cases.json` writer exists, with its source revision identified.
4. A demonstrated same-session 7/7 acceptance capture, including the exact invocation, exit code, session identity and artifacts.
5. T311 residual-attribution output sufficient to select a lever without silently attributing unexplained residual to it.
6. The collection's immutable baseline reference is bound to the supplied `0d4b0cf6`.

Record these in `.pdca/T247/rv3/dependency.json`; retain the predecessor source references and copied evidence hashes. Record the oracle snapshot in `.pdca/T247/rv3/oracle/`.

T247 does not author, tune or repair the oracle/capture writer. If the handoff fails this gate, retain “dependency gate: T311 handoff pending (not yet produced).” Starting implementation first would repeat the failed criterion unchanged and is forbidden.

Collection owner authorization dated 2026-10-19 permits auto-commit in the collection's own task commits and `git merge --no-ff` for a `done` group. Task commits use `git add <exact task paths>`, never `git add -A`. The 45+ foreign dirty paths remain untouched. Push is never authorized. “Committed oracle” remains a requirement on the predecessor handoff.

### Minimal solution

- Essence: remove measured avoidable JSON-writing overhead, not redesign streaming.
- Hard constraints: original thresholds and semantics; no public-API delta; no sticky command-state mutation; provider-independent valid cache keys; no weakened tests or measurements.
- Optimum: the smallest supported lever, followed by paired public-path measurements. Do not bundle unrelated micro-optimizations.

| Alternative | Benefit | Cost / risk | Decision |
|---|---|---|---|
| Target one attributed lever | Small diff; falsifiable benefit | May not close the total gap | Chosen |
| Combine all suspected hot spots | Larger theoretical saving | Confounded attribution; correctness and cache risks | Not initially |
| Repeat measurements without oracle/capture changes | Little implementation cost | Repeats the two terminal failures | Rejected |

### Lever hypotheses and disposition

Expected savings below are hypotheses, not measured claims. Numerical estimates come from the predecessor attribution; none are invented here.

| Supplied source reference | Hypothesis / expected saving | Risk | Selection evidence | Disposition |
|---|---|---|---|---|
| `src/nextorm.core/Query/Json/JsonStreamWriter.cs:47-63`; `src/nextorm.core/Query/Json/PooledStreamBufferWriter.cs:166` | Remove avoidable per-row sink/drain work; saving bounded by its measured per-row contribution | Buffer growth, delayed exceptions, changed completed-row prefix or Flush behavior | Sink/drain attribution plus failure-prefix tests | Fix now only if selected by oracle; otherwise deferred until this component is dominant and existing criteria remain unmet |
| `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs:540` | Reduce per-cell `GetString` cost only where a semantically equivalent path exists | Provider storage differences, null and encoding behavior | Reader/string attribution across differing storage types | Same conditional fix-now rule; never bypass compatibility guards |
| `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs:459` | Remove `Array.GetValue` boxing for supported typed-array paths | Value/reference/null/default and array-shape regressions | Boxing/allocation contribution and typed-array comparison | Fix now if selected; otherwise defer until attributed boxing prevents meeting a criterion |
| `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs:603` | Reduce escaping work on the ordinary-string path | Invalid JSON or different escaping bytes | Escaping attribution with escape-heavy and ordinary strings | Fix now if selected; otherwise defer until escaping is a demonstrated bottleneck |
| Delegate invocation; exact location resolved with Roslyn before editing | Remove invocation overhead only if separately attributable | Larger generated writers, cache growth, changed delegate sharing | Compile-only / steady-writer / public-total evidence | Deferred unless T311 identifies it as material; resolve symbol location before selecting |

Selection order is evidence-driven, not the order of this table. If no eligible lever plausibly closes the required gap, return to PLAN; do not implement speculative churn.

### Footprint and exclusions

Authorized implementation footprint:
- `src/nextorm.core/Query/Json/JsonStreamWriter.cs`
- `src/nextorm.core/Query/Json/PooledStreamBufferWriter.cs`
- `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs`

Conditional implementation paths, only on demonstrated attribution and only after returning to PLAN:
- `src/nextorm.core/DataContext/QueryExecutor.cs` — per-row loop at `:1186-1190`, `:1235-1239`;
- `src/nextorm.core/DataContext/DataContext.cs` — per-call cache consult at `:433`;
- `src/nextorm.core/Query/Json/JsonRowWriterCache.cs`.

Authorized test footprint:
- `tests/nextorm.core.tests/Json/JsonStreamWriterTests.cs`
- `tests/nextorm.core.tests/Json/JsonRowWriterCacheTests.cs`
- `tests/nextorm.sqlite.tests/Json/JsonStreamingTests.cs`
- `tests/nextorm.integration.tests/CommonTestSuite/Json/CommonTestSuite.JsonStream.cs`
- `tests/nextorm.integration.tests/CommonTestSuite/Json/JsonStreamNested.cs`

Use Roslyn for symbol analysis before editing. Do not invent future test symbols.

Authorized benchmark footprint, only if instrumentation is required:
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWriteJson.cs`
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkJsonFixedCost.cs`
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkJsonStreamPhase2.cs`

Instrumentation must not author or modify T311's oracle/capture machinery or alter frozen measurement criteria.

Authorized records:
- `.pdca/status/T247-json-streaming-1.md`;
- `.pdca/T247/rv3/**`.

Read-only dependencies/reference inputs:
- `.pdca/scripts/perf/t247_json_gate.py`;
- `.pdca/scripts/perf/t247_measure.py`;
- `.pdca/scripts/validate_inner_loop.py`;
- `.pdca/scripts/pdca_evidence.py`;
- T311 handoff, oracle and capture implementation;
- benchmark sources outside the conditional instrumentation footprint, benchmark data, coverage configuration and public EN/RU documentation;
- `docs/specs/status/D247-json-streaming-2.md`;
- historical `.pdca/T247/rv2/**`.

Out of scope:
- authoring or modifying the oracle/capture machinery;
- public API, provider SQL/dialect behavior, unrelated cache changes;
- thresholds, tolerances, denominators, coverage exclusions or skip budgets;
- unrelated benchmark/harness changes;
- collection status file;
- edits to historical/frozen records;
- `git push`, merges outside the collection's group-level `--no-ff` rule, and staging foreign dirty paths.

If a required fix falls outside this footprint, return to PLAN with the concrete dependency and proposed expansion before editing.

### What the request did not specify

| Gap | Disposition |
|---|---|
| T311 has no current plan/oracle/artifact | Explicit cross-task dependency; no T247 DO before handoff |
| Quantitative saving for each lever | Unproven hypothesis until T311 attribution; no invented percentage |
| Historical assembly/host identity incomplete | Historical results are contextual; fresh paired runs require stronger identity records |
| Preflight records DB hash but does not enforce it | Explicit pin comparison is required; recording alone does not meet the gate |
| Public documentation changes | None expected because API/semantics stay unchanged; audit EN/RU consistency. A factual correction requiring new paths returns to PLAN |

### DO units

All units are active; none is marked done by this plan. Original acceptance obligations remain attached throughout.

- **D247-01 — Bind predecessor and measurement contract.**
  Dependency gate: T311 handoff pending (not yet produced). Verify handoff gate; record the exact footprint; write contract.json, dependency.json, source/DB/toolchain identity records and command journal configuration.
- **D247-02 — Establish fresh paired baselines and choose lever.**
  Depends on D247-01. Preserve historical-pre-change and shipped-cache identities separately; prepare the detached temporary baseline worktree; obtain baseline/capture evidence; consume T311 attribution and record the selected lever and falsifiable expected effect.
- **D247-03 — Implement the selected minimal lever.**
  Depends on D247-02. Modify only selected authorized sources, add focused tests, preserve cache and partial-output invariants. Unselected hypotheses remain deferred with the triggers above.
- **D247-04 — Complete correctness and boundary verification.**
  Depends on D247-03. Run focused unit tests during implementation; perform the single DO→CHECK boundary sweep below. Record all provider outcomes and coverage.
- **D247-05 — Run final paired performance/capture and report.**
  Depends on D247-03 and passing focused correctness tests. Two required rounds; measure baseline and candidate back-to-back in one inherited-lock session; run compare, gate, acceptance and attribution reporting. Do not cherry-pick rounds.
- **D247-06 — Assemble evidence and freeze DO→CHECK.**
  Depends on D247-04 and D247-05. Audit docs/registry, normalize CRLF, remove the temporary baseline worktree and record the cleanup receipt, make authorized task commits using exact task paths only, freeze tree identity and manifest, run both validators, hand over to CHECK.

A blocker report is not completion. Missing evidence alone causes targeted evidence recovery, not a new implementation iteration.

### Execution mode

Sequential, one implementation tree, lane cap 1:
- source files and writer/cache contracts overlap;
- benchmarking requires an exclusive host lane;
- T311 owns the shared measurement contract;
- parallel changes would confound attribution and baseline identity.

No implementation worktrees are planned.

The historical baseline uses a detached temporary measurement worktree outside the repository under `/tmp/opencode/`, as specified in the performance protocol. It is not an implementation worktree, creates no task branch and no commits. Do not switch source states in place in the main worktree or disturb its 45+ foreign dirty paths.

### Test strategy and variant matrix

Use FluentAssertions; preserve CRLF; add no suppressions or skips.

| Variant / negative case | Closure |
|---|---|
| `QueryCommand<TResult>` / `EntityBuilder<T>`, sync / async | Test all four surface combinations; P1 |
| Managed / native / supported fallback | Byte-equality tests plus live-provider boundary evidence |
| In-memory path | Guard test: fail closed; unit project only |
| DTO and existing supported shapes | Test against control; unsupported shapes retain explicit guard |
| `null` / `default`, nullable/non-nullable | Test; distinguish absent/null from ordinary values |
| Value / reference values; typed arrays / boxed-array path | Test applicable supported cases; unsupported shapes guarded |
| Different provider storage types | Key equality/non-collision and reader compatibility tests |
| Incompatible reader | Guard rejects; must not reuse an incompatible writer |
| Naming / IgnoreNull / options / shape | Test non-collision and byte identity |
| Converter / policy | Test required bypass behavior; no unsafe cached reuse |
| Ordinary / escaping / Unicode / empty strings | Byte-equality tests, including malformed-output prevention |
| Empty / one row / boundaries / 100 / 1k / 10k / 100k | Boundary tests and prescribed measurements |
| Cancellation / read, conversion or destination failure after completed rows | Exact completed-row prefix; destination open; Flush=0 |
| Repeated calls / reused context / shared delegates | Cache stability, boundedness and no sticky state mutation |
| Unselected optimization hypotheses | Deferred only as implementation; their correctness variants remain tested/guarded |

All supported variants are test obligations, not deferred because the chosen lever does not touch them.

Affected subsets:
- `tests/nextorm.core.tests`: Json-related tests;
- `tests/nextorm.sqlite.tests`: `JsonStreamingTests`;
- integration: `CommonTestSuite.JsonStream` across live providers.

One DO→CHECK boundary sweep:
1. Core and SQLite JSON subsets.
2. One full live integration run, including `CommonTestSuite.JsonStream`, under integration coverage collection.
3. Coverage/report generation and convention/build audit.

Using the full integration suite once avoids claiming an integration coverage gate from a JSON-only denominator. Do not separately rerun the integration subset at the same boundary.

Before integration, load `.opencode/skills/running-integration-tests/SKILL.md`. Recover the Podman machine/socket as documented if needed. Provider skips are not provider-pass evidence; preserve only the established justified skip budget.

Coverage:
- measured project gate: line ≥85%, branch ≥75%;
- integration gate remains line ≥65%, branch ≥75%, with the existing `NextORM.Core.InMemory*` exclusion only;
- do not change thresholds, denominator rules or skip budgets.

### Priority matrix

P1 by construction:
- R247-01…R247-20: frozen acceptance, execution-path invariants, binding predecessor/evidence gates;
- every supported variant above;
- all changed executable source lines and their negative paths.

CHECK may not downgrade these. Cosmetic prose changes outside these obligations are P2; they cannot compensate for an open P1 row.

### Documentation plan

- Write rv3 report, dependency/identity records and current status/registry entry.
- Audit existing EN/RU public JSON documentation for agreement with unchanged behavior.
- No public documentation edit is planned.
- Do not link public docs to internal specs.
- Do not modify the collection status file or historical status files.

### Performance protocol — required

This is per-row/per-cell work, not one-time setup:
`src/nextorm.core/Query/Json/JsonStreamWriter.cs:47-63`,
`src/nextorm.core/Query/Json/PooledStreamBufferWriter.cs:166`,
`src/nextorm.core/Query/Json/JsonRowWriterFactory.cs:459,540,603`.

Measure:
- JSON/control 100, 1k, 10k and 100k where prescribed;
- compile-only, steady writer and public total;
- byte equality outside timing;
- two rounds;
- all original thresholds, without tolerance additions.

Seven acceptance cases, unchanged:
1. `InMemoryBenchmarkAggregates.Nextorm_Count`
2. `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount`
3. `SqliteBenchmarkAny.Nextorm_Cached`
4. `SqliteBenchmarkCachedPlan.Prepared_ToList`
5. `SqliteBenchmarkCachedPlan.Cached_ToList`
6. `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param`
7. `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`

The original seven-case gate remains ≤240 s and each case ≤120% of its paired baseline mean per round.

Baseline roles:
- historical pre-change source: `c99cc512450006f7d96cadc6fffde78ce9fe5ad6`;
- historical shipped-cache source: `c9aa86906c900130f5ec21bb80d63439aff4e35f`;
- collection baseline and edited-tree base: `0d4b0cf6`;
- final: frozen candidate source/tree identity.

Do not substitute shipped-cache baseline for a historical-pre-change obligation, or vice versa.

Concrete baseline-source mechanism:
- Build the pre-change baseline arm from immutable historical commit `c99cc512450006f7d96cadc6fffde78ce9fe5ad6` in the detached temporary worktree `/tmp/opencode/t247-baseline-c99cc512`.
- Build and run the candidate arm from the main worktree `/home/alex/sources/nextorm`.
- Measure baseline and candidate back-to-back in one session under the inherited host flock; record exact commit/tree identity for each arm and round.
- Use the same identified T311 measurement instrumentation for both arms without replacing the historical product implementation.
- Do not switch historical sources into the main worktree. Do not stage or modify foreign paths.
- The temporary worktree is measurement-only, creates no task branch and no commits, and is removed at task closure. Record removal in `.pdca/T247/rv3/cleanup-receipt.json`.
- If T311's pre-registered same-session pairing protocol differs, T311's protocol wins and T247 returns to PLAN before measuring.

Pin DB:
`benchmarks/nextorm.benchmark/data/test.db`
SHA256:
`32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`.

For every source state/round record: role, Git ref, owned diff/tree digest, relevant source/harness hashes, Release assembly hashes, DB hash, SDK/runtime, host, command, session and round IDs, validity and exit code. T311 instrumentation applied to baseline states must be identical and separately identified.

Use inherited exclusive flock:
`/tmp/opencode/nextorm-bdn-host.lock`.

A single outer `flock -x /tmp/opencode/nextorm-bdn-host.lock bash` session executes C04 and C07 back-to-back before releasing the lock. These direct driver invocations inherit the same lock descriptor; do not nest independently locking wrappers inside that session. Preliminary baseline work in D247-02 does not replace the required paired baseline/final session in D247-05.

Missing inherited lock, changed DB, mismatched source/toolchain or incomplete cases invalidates the round. Preserve invalid attempts; do not silently discard them.

The historical invalid `1b140d23…` attempt remains invalid.

### Reconnaissance decision

No additional discovery spike is needed before this plan.

The existing phase harness provides compile-only / steady-writer / public-total separation. T311 supplies the missing oracle and residual attribution. After that handoff, D247-02 performs the planned measurement experiment, not an open-ended scout.

Observable selection criterion: the pre-registered oracle identifies an eligible component and predicts a falsifiable reduction sufficient to address the remaining original criteria. Otherwise return to PLAN without speculative implementation.

### Command register

Paths below are repository-relative unless absolute. Execute from the main repository directory except where a command explicitly selects the baseline repository. Capture exact argv, cwd, exit, stdout/stderr and identity records under `logs/` and `commands.json`.

- C01: `git show --no-patch --format=fuller c99cc512450006f7d96cadc6fffde78ce9fe5ad6 c9aa86906c900130f5ec21bb80d63439aff4e35f`
- C02: `sha256sum benchmarks/nextorm.benchmark/data/test.db`
- C03: `flock -x /tmp/opencode/nextorm-bdn-host.lock python3 .pdca/scripts/perf/t247_json_gate.py preflight --evidence-root .pdca/T247/rv3`
- C04: `python3 .pdca/scripts/perf/t247_json_gate.py baseline --repo /tmp/opencode/t247-baseline-c99cc512 --rounds 2 --evidence-root /home/alex/sources/nextorm/.pdca/T247/rv3`
- C05: `flock -x /tmp/opencode/nextorm-bdn-host.lock python3 .pdca/scripts/perf/t247_json_gate.py phase --rounds 2 --evidence-root .pdca/T247/rv3`
- C06: `python3 .pdca/scripts/perf/t247_json_gate.py phases --evidence-root .pdca/T247/rv3`
- C07: `python3 .pdca/scripts/perf/t247_json_gate.py final --repo /home/alex/sources/nextorm --rounds 2 --evidence-root /home/alex/sources/nextorm/.pdca/T247/rv3`
- C08: `python3 .pdca/scripts/perf/t247_json_gate.py compare --evidence-root .pdca/T247/rv3`
- C09: `flock -x /tmp/opencode/nextorm-bdn-host.lock python3 .pdca/scripts/perf/t247_json_gate.py gate --baseline .pdca/T247/rv3/baseline --final .pdca/T247/rv3/final --evidence-root .pdca/T247/rv3`
- C10: `flock -x /tmp/opencode/nextorm-bdn-host.lock python3 .pdca/scripts/perf/t247_json_gate.py acceptance --baseline .pdca/T247/rv3/baseline --evidence-root .pdca/T247/rv3`
- C11: `dotnet build nextorm.slnx -c Debug`
- C12: `dotnet build nextorm.slnx -c Release`
- C13: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~Json"`
- C14: `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~JsonStreamingTests"`
- C15: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o .pdca/T247/rv3/coverage/integration.cobertura.xml "dotnet test tests/nextorm.integration.tests -c Debug --verbosity normal"`
- C16: `dotnet tool run reportgenerator -reports:.pdca/T247/rv3/coverage/integration.cobertura.xml -targetdir:.pdca/T247/rv3/coverage/integration-report -reporttypes:"Html;TextSummary" -riskhotspotassemblyfilters:"+nextorm.*" -classfilters:"-NextORM.Core.InMemory*"`
- C17: `python3 .pdca/scripts/perf/t247_json_gate.py coverage --scope nonintegration --evidence-root .pdca/T247/rv3`
- C18: `python3 .pdca/scripts/perf/t247_json_gate.py audit --base 0d4b0cf6 --evidence-root .pdca/T247/rv3`
- C19: `python3 .pdca/scripts/perf/t247_json_gate.py docs --evidence-root .pdca/T247/rv3`
- C20: `git diff --check`
- C21: `python3 .pdca/scripts/validate_inner_loop.py manifest .pdca/T247/rv3/manifest.json`
- C22: `python3 .pdca/scripts/pdca_evidence.py lint --repo . --manifest .pdca/T247/rv3/manifest.json`

CHECK invocation:
- C23: Task to `check` with prompt:
  “Check T247 r=1 rv3 against .pdca/status/T247-json-streaming-1.md and .pdca/T247/rv3/manifest.json. Verify every required row, predecessor registration/capture, source and baseline identity, variant coverage, source invariants, CRLF, suppressions/skips, documentation consistency and raw logs. Preserve P1 classification. CHECK re-gather budget is 2 total. Append dispositions; do not rewrite frozen evidence.”

C04/C07 inherit one outer session's flock as specified above; C05 obtains an inherited lock through its direct `flock` invocation. Commands consuming artifacts do not manufacture missing prerequisites.

Each required command must exit 0 and meet its semantic result. Exit 1 is a threshold failure; 2 input error; 3 inconclusive; 4 invalidated round; 5 unresolved prerequisite; wrapper 90 duplicate guard. None is PASS.

### Versioned evidence contract — rv3

Representation: the following header and row table are the authoritative plan representation. Coder writes a matching canonical object to `.pdca/T247/rv3/contract.json` before DO.

Header:
- task: T247
- issue: "#247"
- status_file: `.pdca/status/T247-json-streaming-1.md`
- path: `.pdca/T247/rv3/contract.json`
- base_ref: `0d4b0cf6`
- collection_baseline_ref: `0d4b0cf6`
- footprint: exact authorized paths above, with benchmark instrumentation conditional and additional implementation paths requiring return to PLAN
- exclusions: exclusions above
- contract_rv: 3
- supersedes: rv2 evidence obligations and manifest for prospective verification only; historical evidence remains immutable
- required_rows: R247-01…R247-20
- rows[]: the rows below

No rv2 contract.json existed. Author rv3 fresh; do not claim a nonexistent file was migrated.

Common row fields:
- stable `id`: as listed;
- initial `status`: open;
- initial `reason`: “Planned evidence; not yet executed or verified”;
- `requirements`: row requirement, owner, required scenario, command IDs, expected result and planned evidence sources listed below;
- `predicate`: observable applicability listed below;
- `evidence[]`: only actual verified evidence, using `{kind,path,command?,exit?,value?}`. Planned paths are not evidence claims.
- every row belongs to contract_rv=3.

All artifact paths below are beneath `.pdca/T247/rv3/`. Owner names denote streams, not permission to waive requirements.

| ID | Requirement / negative case | Observable predicate | Owner | Commands / planned evidence kinds and artifacts |
|---|---|---|---|---|
| R247-01 | 1k DTO ratio ≤1.2; 10k ≤1.0 and allocation ≤446,095 B/op; 100k ≤ paired pre-change ratio. Any required-round miss fails | T247 candidate measurements exist | perf | C03–C09; benchmark/raw logs in `baseline/`, `final/`; comparison JSON `comparison.json`; identity audit |
| R247-02 | `ToList_Dto + JsonSerializer` control at 1k/10k/100k; bytes equal outside timing. Missing/mismatched control fails | JSON benchmark inputs | perf | C04,C07,C08; control results and byte-equality records in baseline/final; `variant-matrix.json` |
| R247-03 | Tests green; managed/native/shape/option/surface/boundary matrix byte-identical. Unsupported variants explicitly guarded | Supported JSON surfaces/providers | tests | C13–C17,C23; test logs, coverage, `variant-matrix.json` |
| R247-04 | Partial-output policy: exact completed-row prefix, destination open, Flush=0. Mid-row corruption or extra flush fails | Streaming success and injected failure scenarios | tests | C13–C15,C23; focused test logs and `fault-policy.json` |
| R247-05 | Command/plan cache preserved; delegates shared, key distinguishes, bounded. Sticky command mutation or unbounded growth fails | Cached writer/command execution | implementation + tests | C13–C15,C18,C23; tests/source audit, `cache-contract.json`, `key-contract.json` |
| R247-06 | No public-API delta; in-memory fail-closed; native/fallback correct | Changed implementation and provider paths | implementation + tests | C11–C15,C18,C23; build/test/source audit, `api-provider-contract.json` |
| R247-07 | Build 0 warnings/0 errors and all provider evidence. Skipped/unrun provider is not pass | Frozen candidate | tests | C11,C12,C15; build logs, integration results, `provider-results.json` |
| R247-08 | CRLF; 0 new suppressions/skips; project coverage ≥85/75. Preserve integration gate/configuration | Changed sources/tests and coverage reports | tests + audit | C16–C18,C20,C23; raw coverage/Summary, `coverage/`, `source-hygiene.json` |
| R247-09 | JSON 1k/10k/100k enter perf gate; unchanged seven cases, ≤240 s, each ≤120% paired baseline per round | Each of two acceptance rounds | perf | C04,C07,C09,C10; raw cases, gate result, `acceptance/`, `acceptance-capture.json` |
| R247-10 | EN/RU/report/registry agree. Contradictory public statements or obsolete registry claims fail | Candidate behavior and published/internal descriptions | docs | C19,C23; `report.md`, `docs-audit.json`, current status |
| R247-11 | rv manifest passes both helpers; CHECK re-gather ≤2 total, owned by check | DO→CHECK and CHECK evidence completion | check | C21,C22,C23; validator logs, `manifest.json`, `check-regather.json` |
| R247-12 | 100-row mean ≤0.50 and fitted intercept ≤0.50, both positive, in each of two rounds; preserve original units/model | Required small-row and fitted measurements | perf | C04–C08; `small-row.json`, `intercept-fit.json`, raw inputs |
| R247-13 | Provider-independent key; differing storage types; incompatible reader rejected | Writer reuse across providers/readers | implementation + tests | C13–C15,C18,C23; tests/audit, `key-contract.json` |
| R247-14 | Naming/IgnoreNull/options/shape non-collision; converter/policy bypass | Options/shape/converter variants | implementation + tests | C13–C15,C23; tests/audit, `key-contract.json` |
| R247-15 | P1 matrix: QueryCommand<TResult>/EntityBuilder<T>, sync/async; no silent surface omission | Four public surface combinations | tests | C13–C15,C23; `variant-matrix.json`, raw test results |
| R247-16 | Compile-only vs steady writer vs public total; residual explicitly labelled unattributed | Phase measurements and attribution report | perf | C05,C06,C08,C23; `phase-attribution.json`, raw phase artifacts |
| R247-17 | Full manifest and both validators, preserving legacy E247-01…19 obligations and identifiers | Final evidence package | check | C21–C23; `manifest.json`, `legacy-evidence-index.json`, validator logs |
| R247-18 | New: actual gate-integrated cases.json capture, same-session 7/7, baseline and final counterparts. Empty, mixed-session or duplicate capture fails | Each required baseline/final acceptance session | perf; predecessor gate owned by T311 | C04,C07,C10,C23; `baseline/round-{1,2}/acceptance/cases.json`, final counterparts, `acceptance-capture.json`; predecessor capture provenance |
| R247-19 | New: consume the committed pre-registered T311 oracle without authoring/tuning it after measurements | Before any T247 DO and throughout candidate evaluation | dependency | C01–C03,C23 plus exact predecessor command retained in `dependency.json`; oracle commit/path/hash, `oracle/`, `dependency.json` |
| R247-20 | New: consume T311 residual closure and demonstrate the selected lever's effect under that oracle; do not assign unexplained residual to the lever | Lever selection and final attribution | perf; predecessor attribution owned by T311 | C05–C08,C23; predecessor provenance, `lever-selection.json`, `phase-attribution.json`, `oracle-result.json` |

The legacy E247-01…19 index must preserve its actual source identifiers and obligations; do not invent a correspondence. Absence of a mapping is an open evidence obligation, not permission to delete it.

At DO→CHECK:
- freeze manifest against the exact tree revision/digest and contract_rv=3;
- retain raw artifacts and commands;
- binding/applicability dispositions are append-only in CHECK;
- no PASS while a binding required row is open;
- `na` requires an observed false predicate and a documented reason; it cannot remove an unconditional acceptance obligation.

Before execution, planned paths may be bound to actual predecessor artifact locations without changing obligations. A substantive contract change needs explicit supersession, preserved existing IDs and obligations, and new IDs for genuinely new variants. Missing reports alone do not justify contract revision.

### Risks, terminal fork and escalation

- Main risk: the attributed removable cost is insufficient to meet the unchanged thresholds.
- Other risks: baseline comparability, buffering/failure semantics, provider reader differences, boxing/escaping correctness and cache growth.
- If T311 is not ready: dependency gate T311 handoff pending; no speculative T247 implementation.
- If valid measurements fail a criterion: product-red; return the concrete failing row and attribution to PLAN.
- If capture/identity/evidence is incomplete: evidence-red; first use targeted recovery within CHECK's budget.
- If scope or shared contracts must change: PLAN before edits.
- A genuine external authority/resource blocker: recommend that the orchestrator invoke escalate.
- Persistently low confidence after a targeted scout: invoke escalate under trigger 5, even without proving externality.
- Third failed CHECK in this same plan revision: escalate; no fourth attempt. A wording change or session reset does not create a new revision.
- A real revised plan changes tasks/dependencies/corrective actions and preserves original criteria; only then increment r and reset n.

### Confidence and references

High confidence in the required contract, command forms, prior failure disposition and dependency ownership.
Lever effectiveness remains unproven pending T311 attribution.
Exact footprint paths and collection baseline `0d4b0cf6` are supplied facts, not initialization gaps.

References:
- `docs/specs/status/D247-json-streaming-2.md:54-64,82-87,183-191`
- `.pdca/scripts/perf/t247_json_gate.py:127-129,474-502,1987,1993-1999,2020-2043`
- `.pdca/scripts/perf/t247_measure.py:53-68`
- `docs/specs/status/collection-1.0.9-rc3-open-3.md:88,94,102` — read-only
- Collection rc3-4 status line 7 and task brief: baseline `0d4b0cf6`
- Collection owner authorization dated 2026-10-19: task auto-commits and group-level `git merge --no-ff`
- `pdca-dotnet`: Versioned evidence contract
- project AGENTS.md; running-integration-tests skill

## Progress log

- PLAN ready (r=1) — collection handoff at PLAN→DO; DO not started; dependency gate T311 pending
