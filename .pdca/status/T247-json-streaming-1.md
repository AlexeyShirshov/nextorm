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

## Current state

- task: `T247` · form: `cycle` · specialized skill: `pdca-dotnet` (`coder+.NET`, inherits `pdca-coder`)
- mode: `autonomous` (collection `rc5` lane, auto-commit authorized)
- cycle: `N=1` · revision: `r=2` · attempt: `n=2/3`
- scope: the whole item goal of issue `#247` — the fixed-cost and streaming-pipeline architecture of `WriteJson`/`WriteJsonAsync`: bounded thread-safe `JsonShapePlan` + compiled `JsonRowWriter` cache, document-mode prepared-descriptor reuse (`storeInCache: false`, never mutating the caller's command or sticky `QueryCommand.Cache`), array-mode flush batching (NDJSON per-record flushing preserved), `JsonStreamWriter`/`Utf8JsonWriter` setup reduction, the 1k/10k/100k phase-attribution benchmark plus the `ToList_Dto + JsonSerializer.Serialize` control arm, unit/correctness coverage, and paired same-session performance evaluation closing on the 2026-10-20 two-tier policy (`AC-247-01..06`) on snapshot `1e31331d`, branch `1.0.9-rc5` · status path: `.pdca/status/T247-json-streaming-1.md`
- goal: the #247 JSON streaming pipeline stops paying rebuild-per-call and per-row flush cost, closing the task either on the global target (`10k rows ≤ 1.00×` linq2db time and `≤ 1.20×` linq2db allocations; 1k fixed-cost gap materially reduced; 100k non-regressing) or on a measured same-round one-axis local win with no regression on the other axis (residual gap recorded), while JSON output bytes, array/NDJSON/root/IgnoreNull semantics, cancellation and partial-output/error policy stay intact — the §PLAN record `r2` and §Original task are authoritative
- phase: `ACT` · record: `completed` · outcome: `done` (user-directed closure, not an independent CHECK acceptance) · accepted scope: "target met by already-landed caches; this item delivered zero product change by design" (issue #247 closed by the user; basis = already-landed TerminalPlanCache + JsonRowWriterCache, commit c9aa8690; measured 1k 611.2 µs/51.45 KB, 10k 7 870.3 µs/403.1 KB, 100k 79 049.0 µs/3 919.49 KB; residual per-row GetString ≈44 B/row + 1k fixed-cost gap reduced but not eliminated; caveat linq2db ratio cross-round)
- source: issue `#247` (`AlexeyShirshov/nextorm`), milestone `1.0.9-rc5`; re-opened by collection `rc5` (new revision `r+1` in this existing rc4 record, `AC-COL-04`) · context refs: `.pdca/status/collection-rc5.md` §`## Input` → `### #247 — Perf: JSON streaming is slower than linq2db at 10k rows` (authoritative task text), `docs/specs/status/D247-json-streaming-2.md` (earlier terminal attempt, carried by reference, read-only), baseline `1e31331d`
- criteria: `AC-247-01`, `AC-247-02`, `AC-247-03`, `AC-247-04`, `AC-247-05`, `AC-247-06` — exact text in §PLAN record `r2` §4, not retyped here; measurement method in §PLAN record `r2` §5 (same-session paired/ABBA against baseline `1e31331d`, exact `MemoryDiagnoser` bytes, pre-registered time noise band, no historical-number substitution) · evidence root: `.pdca/T247/rv3/` (contract revision `rv3`), DO attempts under `.pdca/T247/rv3/attempts/`; `.pdca/T247/rv2/` belongs to the earlier attempt and is read-only
- supersession note (executor, append-only): the earlier plan block `## Plan (r=1)` above is **superseded by `r=2`** and is kept byte-identical as history. Reason: a genuine contract revision — the 2026-10-20 two-tier acceptance policy (global target vs local one-axis acceptance) replaces the retired single-tier `R247-01..20` row set, and an explicit scope boundary is drawn against `T311` (10k per-row throughput lever, same-session paired acceptance baseline harness, noise-robust ratio oracle) and `T250` (CSV terminal plan cache). The `r=1` dependency gate on the `T311` handoff is removed: `T247` neither depends on `T311` to start or close under the local-acceptance policy, and does not re-litigate `T247`'s thresholds.
- fidelity note: the `r2` report is recorded verbatim from `pdca-planner`; no wording altered, edited, translated or reconstructed.

## Original task

Reference (the readable complete original, not repeated here): `.pdca/status/collection-rc5.md` §`## Input` → `### #247 — Perf: JSON streaming is slower than linq2db at 10k rows` (line 959 of that file) — the complete verbatim issue body in its original language, including the 2026-10-17 benchmark table (1k `2.132 ms / 84.01 KB` vs `0.562 ms / 90.39 KB`; 10k `10.352 ms / 435.64 KB` vs `9.150 ms / 963.56 KB`; 100k `82.891 ms` vs `86.797 ms`), the root-cause analysis (`DataContext.cs:353-420` `PrepareJsonStream`, `JsonStreamWriter.cs:47-65` per-row flush, per-row `IDataRecord.GetString`), the six proposed work items, the user-supplied 2026-10-20 two-tier acceptance block, the mandatory correctness/evidence list and the three constraints, sourced verbatim from `/tmp/nextorm-rc5-issues.md`.

## PLAN record

### r1

`superseded by r2 — kept byte-identical above as §Plan (r=1)` — the complete `r1` report is the `## Plan (r=1)` block earlier in this file; DO never ran on it and no attempt was consumed.

### r2

````markdown
# PLAN Report — Collection Item `T247` (`#247`)

## 1. Required Cycle Identity and Counter Accounting
- **Status path**: `.pdca/status/T247-json-streaming-1.md`
- **Cycle identity**: `N = 1`, `r = 2`, `n = 1/3` (anchored to snapshot `1e31331d` on branch `1.0.9-rc5`)
- **Counter rationale**: The existing status record `.pdca/status/T247-json-streaming-1.md` is at `N = 1`, `r = 1`, `n = 1/3` (`State: PLAN ready (r=1) … DO not started; dependency gate T311 pending`), with its declared evidence root `.pdca/T247/rv3/` absent and `R247-01…R247-20` unexecuted because the upstream group stopped before DO. Meanwhile, the earlier legacy record `docs/specs/status/D247-json-streaming-2.md` closed `incomplete (terminal measured GAP)`. Under the revision accounting rules and the repository re-entry rule, re-planning `T247` in `.pdca/status/T247-json-streaming-1.md` to incorporate the two-tier acceptance contract and explicit scope boundary against `T311` (and `T250`), while retiring the legacy unmapped `R247-*`/`E247-*` scaffolding from `r = 1`, is a genuine contract revision (`r = 2`). Because DO never started on `N = 1`, the execution attempt counter remains `n = 1/3`.

## 2. Prior Cause Carried Forward, Re-entry Justification, and Sibling Boundary (`T311`, `T250`)
- **Prior terminal cause carried forward**: In `docs/specs/status/D247-json-streaming-2.md:557`, the attempt ended `incomplete (terminal measured GAP)`:
  1. **Product-red arm**: 10,000-row `WriteJson_Array_Dto` time ratio was `1.04546 / 0.91917` against a strict single-tier target of `0.81079` (Fixed-cost caching and flush batching dramatically improved 1,000-row setup cost, but at 10,000 rows per-row string extraction and serialization overhead dominated).
  2. **Evidence-red arm**: `0/7 acceptance cases captured` because the old compare script exited `1` and the same-session paired acceptance baseline was not executed.
- **How re-entry is justified (both arms resolved)**:
  1. **Criterion / Oracle Reformulation (User-supplied 2026-10-20 Two-Tier Policy)**: The brief now supplies the explicit two-tier acceptance policy: while the ≤1.00× time and ≤1.20× allocation ratios at 10,000 rows remain the *Global target (milestone-level)*, *Local acceptance (task closure)* is achieved when the measured change is a real one-axis win with no regression on the other against the same-round paired/ABBA baseline (specifically: 10k allocations are already below linq2db, so a time-only win beyond the pre-registered noise band with allocations not increasing—or an allocation reduction with time within the noise band—closes the task while recording the residual gap to the global target).
  2. **Decisive Evidence Obtainability**: Same-session, same-round paired/ABBA measurements on a pinned SQLite database and the 7-case `--anyCategories=acceptance` benchmark run are directly executable in a single process without relying on the broken historical compare script or cross-session numbers.
- **Explicit Boundary Against `T311` and `T250`**:
  - **Against `T311`**: `T247` owns the **fixed-cost and streaming-pipeline architecture** specified in #247 Proposed Work items 1–6: phase attribution benchmark (1k/10k/100k), bounded thread-safe `JsonShapePlan` + compiled `JsonRowWriter` cache with metadata/options invalidation, array-mode flush batching in `JsonStreamWriter.WriteRow` (preserving NDJSON per-record flushes and bounded memory), document-mode prepared descriptor reuse (`storeInCache: false`, never mutating the caller's command or sticky `QueryCommand.Cache`), `JsonStreamWriter`/`Utf8JsonWriter` setup reduction, the `ToList_Dto + JsonSerializer.Serialize` control arm, and explicit documentation/explanation of the per-row `IDataRecord.GetString` allocation. `T311` owns the follow-up **10k per-row throughput lever**, the standalone same-session paired acceptance baseline harness, and the new noise-robust ratio oracle. `T247` does **not** depend on `T311` to start or close under the 2026-10-20 two-tier policy, and does **not** re-litigate `T247`'s thresholds.
  - **Against `T250`**: `T250` covers the CSV terminal plan cache (`WriteCsv`). `T247` is strictly scoped to the JSON streaming pipeline (`PrepareJsonStream`, `JsonShapePlan`, `JsonRowWriterFactory`, `JsonStreamWriter`) and JSON benchmarks/reports.

## 3. Form, Specialized Skill, and Entry Scope
- **Form**: `cycle` (ordinary cycle)
- **Specialized skill**: `pdca-dotnet` (`coder+.NET`)
- **One-line form rationale**: The optimization mechanisms (caching immutable `JsonShapePlan` and compiled `JsonRowWriter` delegates, batching `Utf8JsonWriter`/sink flushes in array mode, reusing document-mode prepared descriptors without mutating `QueryCommand`, and adding phase/control benchmarks) are concrete, well-understood C#/.NET transformations bounded to `src/nextorm.core` (`DataContext`, `Query/Json/*`), `tests/nextorm.core.tests`, and `benchmarks/nextorm.benchmark`.
- **Entry scope**: End-to-end delivery of the #247 JSON streaming optimizations, phase attribution and control-arm benchmarks (1k/10k/100k), unit/correctness coverage, and paired same-session performance evaluation in a single cycle anchored at `1e31331d`.

## 4. Goal, Requested Result, and Acceptance Criteria

### Verbatim Task
`### #247 — Perf: JSON streaming is slower than linq2db at 10k rows` (full verbatim brief as provided in dispatch).

### Requested Result & Delivered Result
- **Requested result**: An optimized Nextorm `WriteJson`/`WriteJsonAsync` streaming pipeline (caching `JsonShapePlan`/`JsonRowWriter`, batching array-mode flushes, reducing per-call document-mode preparation overhead without mutating shared commands) accompanied by 1k/10k/100k phase attribution and `ToList_Dto + JsonSerializer` control benchmarks, explicit explanation of per-row string allocation, full preservation of JSON streaming semantics, and measured performance satisfying the 2026-10-20 two-tier acceptance policy.
- **Delivered result**: The exact same object—updated JSON streaming implementation in `src/nextorm.core`, unit tests in `tests/nextorm.core.tests`, 1k/10k/100k phase and control benchmarks in `benchmarks/nextorm.benchmark`, updated benchmark report explaining per-row string allocations and recording the paired same-session 1k/10k/100k measurements and residual gap (if any) to the global target.

### Acceptance Criteria (User-Supplied Set Reproduced Verbatim, with Stable IDs & Kinds)

- **`AC-247-01` (Performance — Two-Tier Policy: Global Target & Local Acceptance)**:
  > **Global target (milestone-level):** at 10,000 rows, `WriteJson_Array_Dto` ≤ **1.00×** linq2db time and ≤ **1.20×** linq2db allocations; at 1,000 rows, the fixed-cost gap is materially reduced; 100k rows do not regress. The original **9.150 ms / 435.64 KB** and ≤1.2× 1k targets remain the global/stretch reference.
  >
  > **Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
  > - allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
  > - time improves beyond the pre-registered noise band and allocations do not increase.
  >
  > A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.

- **`AC-247-02` (Measurement Governance — State-Dependent / Comparative)**:
  > **Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.
  >
  > Note: 10k allocations are already below linq2db, so a **time-only win with allocations not increasing** is sufficient for local acceptance.

- **`AC-247-03` (Mandatory Correctness & Behavioral Preservation — Functional + Negative)**:
  > No public API/output change: valid JSON, same DTO payload, array/NDJSON/root/IgnoreNull semantics, cancellation and partial-output/error policy preserved.

- **`AC-247-04` (Mandatory Benchmark & Control-Arm Coverage — Deliverable)**:
  > Add JSON 1k/10k/100k rows to the performance gate; include the `ToList_Dto + JsonSerializer` control in the report.

- **`AC-247-05` (Mandatory Allocation Explanation — Deliverable + Negative)**:
  > Explain the remaining per-row string allocation explicitly; “zero materialization” is not claimed as zero allocation.

- **`AC-247-06` (Mandatory Architectural Constraints — Negative)**:
  > - Do not switch the public `WriteJson` contract to materialize DTOs; direct streaming must remain the default.
  > - Do not weaken JSON validation, cancellation, or error policy for speed.
  > - No benchmark-specific payload checks; cache keys must be general and bounded.

## 5. Measurement Method
- **Metrics and units**:
  1. **Execution time**: Mean execution time per invocation in milliseconds (`ms`) / microseconds (`μs`) / nanoseconds (`ns`) reported by BenchmarkDotNet.
  2. **Allocated memory**: Exact managed bytes allocated per invocation (`B` / `KB`) reported by BenchmarkDotNet `[MemoryDiagnoser]`.
  3. **Relative ratios (Global reference)**: `Mean(Nextorm WriteJson_Array_Dto) / Mean(linq2db materialize + JsonSerializer)` for time, and `Allocated(Nextorm WriteJson_Array_Dto) / Allocated(linq2db materialize + JsonSerializer)` for memory at `RowCount ∈ {1000, 10000, 100000}`.
  4. **Local acceptance delta**: `ΔTime = (Mean_candidate - Mean_baseline) / Mean_baseline` and `ΔAlloc = Allocated_candidate - Allocated_baseline` across `RowCount ∈ {1000, 10000, 100000}` for `WriteJson_Array_Dto` (plus phase attribution breakdown and `ToList_Dto + JsonSerializer` control arm).
- **Denominator**:
  - For local acceptance (`AC-247-01` / `AC-247-02`): Same-round, same-session pre-change baseline (`1e31331d`) measured in a paired/ABBA interleaving on the identical pinned SQLite dataset and sink.
  - For global target tracking (`AC-247-01`): Same-round, same-session `linq2db` materialize + `JsonSerializer.Serialize` benchmark on the identical DTO payload, row count (`1,000`, `10,000`, `100,000`), and sink.
- **Inclusion / Exclusion Filters**:
  - Included: `SqliteBenchmarkWriteJson` (and the new JSON phase attribution benchmark + `ToList_Dto + JsonSerializer` control arm) at `RowCount ∈ {1000, 10000, 100000}`, plus the 7-case repository query-path acceptance suite (`--anyCategories=acceptance`).
  - Excluded: Historical numbers from `feature-benchmarks-2026-10-17.md` or earlier sessions (`rv2`) are never substituted as the baseline denominator for local acceptance (they serve only as the stated background context).
- **Comparability Conditions**:
  - Strictly one benchmark or container process running at a time (no concurrent CPU/IO contention).
  - `Release` configuration (`-c Release`), identical .NET 10 runtime, identical SQLite database state and schema, identical UTF-8 sink and `JsonStreamOptions`.
  - Paired/ABBA run order within the same session/round so thermal/frequency drift is balanced across baseline and candidate.
- **Thresholds and Provenance** (User-supplied 2026-10-20 two-tier policy + repo standing rule):
  - **Pre-registered time noise band**: Repository standing benchmark rule designates deltas `< ~20%` on the general suite as noise; for paired/ABBA same-session microbenchmark comparisons on `SqliteBenchmarkWriteJson`, the pre-registered noise band for a statistically meaningful time change is fixed before measurement by the paired ABBA run's confidence/noise bound (and for the 7-case `--anyCategories=acceptance` gate, no regression `≥ 20%`).
  - **Allocation threshold**: Exact byte count (`ΔAlloc ≤ 0 B` for no allocation increase; `ΔAlloc < 0 B` for an allocation decrease).
  - **Local acceptance**: At 10,000 rows (and 1,000 rows fixed-cost), time improves beyond the pre-registered noise band with `ΔAlloc ≤ 0 B` (or `ΔAlloc < 0 B` with time not worsening beyond the noise band), and 100,000 rows does not regress beyond the noise band or increase per-row allocations.
  - **Global target (stretch/milestone reference)**: At 10,000 rows, `WriteJson_Array_Dto ≤ 1.00×` linq2db time and `≤ 1.20×` linq2db allocations; 1,000-row fixed-cost gap materially reduced; 100,000 rows non-regressing. If local acceptance is met without reaching the global ratio, the exact residual gap to `≤ 1.00×` is recorded.

## 6. Constraints and Assumptions
- **Constraints**:
  - Direct streaming (`WriteJson`/`WriteJsonAsync`) remains the default; never materialize `List<TResult>` to implement `WriteJson`.
  - Never mutate the caller's `QueryCommand` or touch the sticky `QueryCommand.Cache` / `_dontCache` property on shared commands; use `storeInCache: false` when preparing non-cached commands.
  - Shape/writer cache keys must be general, immutable, thread-safe, bounded, and invalidated (or naturally Partitioned by immutable key) when metadata or `JsonStreamOptions`/naming policy differ; no benchmark-specific payload checks.
  - Array mode may batch `Utf8JsonWriter.Flush()` and `_sink.Flush()` based on buffer threshold and `Complete()`, whereas NDJSON mode (`JsonStreamFormat.Ndjson`) must preserve per-record flushing and boundaries.
  - Build must pass with `0` warnings and `0` errors (`TreatWarningsAsErrors=true`), CRLF line endings preserved, and all test assertions written with FluentAssertions (`tests/nextorm.core.tests/Conventions/AssertionStyleTests.cs`).
  - Live integration tests (`tests/nextorm.integration.tests`) are fail-closed and require `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`.
  - Git policy: milestone `1.0.9-rc5` never moves; no push, no merge to `main`, no tags; auto-commit only for item-touched files on `collection/**` branches.
- **Assumptions**:
  - Caching `JsonShapePlan` and the compiled `JsonRowWriter` delegate together with batching array-mode flushes and avoiding redundant per-call command clone/compile overhead will eliminate the ~1.5 ms fixed per-call setup penalty (visible at 1,000 rows) and yield a measurable time improvement at 10,000 rows without increasing managed allocations.
- **Known Resource and Access Constraints**:
  - Local CPU/memory environment and Podman socket (`unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) are available; benchmark and container runs must be serialized (strictly one process at a time) to prevent measurement invalidation.

## 7. Gaps Closure Table
| Statement Gap | Resolution Type | Closure |
|---|---|---|
| Prior status record `.pdca/status/T247-json-streaming-1.md` was left at `r=1` with `T311` dependency gate and missing `.pdca/T247/rv3/` evidence | Evidence (supplied in dispatch) | Upgraded to `r=2` (`N=1, n=1/3`); legacy `R247-*`/`E247-*` rows superseded by `AC-247-01..06`; `T311` dependency gate removed because `T247` closes under the 2026-10-20 two-tier local acceptance policy while `T311` handles the follow-up 10k per-row throughput lever. |
| Exact buffer threshold for array-mode flush batching in `JsonStreamWriter.WriteRow` | Assumption (means, not outcome) | Flush `Utf8JsonWriter` to `_sink` when `Utf8JsonWriter.BytesPending` reaches a bounded threshold (e.g., 16 KB / 32 KB matching pooled buffer sizing) and at `Complete()`, keeping memory bounded and verified by a buffer-size threshold unit test. |
| Whether per-row `IDataRecord.GetString` should be replaced by `GetChars` in `T247` | Evidence (brief item 5 & boundary with `T311`) | Brief item 5 states: *"measure before deciding whether the per-row string extraction needs a `GetChars`/pooled-char path"* and mandatory evidence requires *"Explain the remaining per-row string allocation explicitly"*, while `T311` owns the 10k per-row throughput lever. `T247` measures phase attribution first, optimizes plan/writer caching, descriptor reuse, and flush batching, and explicitly documents the remaining `GetString` allocation. |

## 8. Minimal Solution and Alternatives
- **Chosen Minimal Solution**:
  1. **Bounded Shape + Writer Cache**: Introduce a thread-safe, bounded cache for `(JsonShapePlan, JsonRowWriter)` keyed by query projection shape (entity/projection type, selected column/expression signature, provider dialect/metadata token) and normalized `JsonStreamOptions` (format, naming policy, root property name, ignore-nulls, indentation).
  2. **Document-Mode Preparation Reuse**: In `DataContext.PrepareJsonStream` (`src/nextorm.core/DataContext/DataContext.cs:353-420`), avoid re-running full command clone + expression preparation + shape compilation on every call when the prepared SQL/command recipe and `JsonShapePlan`/`JsonRowWriter` are already cached for the shape, while strictly preserving `storeInCache: false` and never mutating the caller's `QueryCommand`.
  3. **Array-Mode Flush Batching**: In `JsonStreamWriter.WriteRow` (`src/nextorm.core/Query/Json/JsonStreamWriter.cs:47-65`), keep per-row `Flush()` for `Ndjson` mode, but in `Array` mode only flush `Utf8JsonWriter` and `_sink` when `BytesPending` exceeds a fixed buffer threshold or during `Complete()` / `CompleteAsync()`, preserving cancellation, partial-output/error guarantees, and bounded memory.
  4. **Phase Attribution & Control Benchmarks**: Add the 1k/10k/100k JSON phase benchmark (separating command clone/prepare, shape-plan build, row-writer compile, per-row `WriteRow`, and flush) and the `ToList_Dto + JsonSerializer.Serialize` control arm to `benchmarks/nextorm.benchmark`, and update the benchmark report with paired ABBA numbers and the explicit per-row `GetString` allocation explanation.
- **Alternatives Considered**:
  - *Unbounded `ConcurrentDictionary` keyed only by `Type`*: Rejected because anonymous/ad-hoc projections or varying `JsonStreamOptions` would either collide or leak memory; a bounded cache with a composite structural key satisfies the constraint *"cache keys must be general and bounded"*.
  - *Mutating `QueryCommand.Cache = false` on the input command*: Rejected; violates the repository's standing rule against mutating sticky `_dontCache` on shared commands.

## 9. Plan-to-DO Handoff
- **Target identity**: `T247`, `N = 1`, `r = 2`, `n = 1/3`, status file `.pdca/status/T247-json-streaming-1.md`, base commit `1e31331d`.
- **In-scope components**:
  - `src/nextorm.core/DataContext/DataContext.cs` (`PrepareJsonStream` and supporting cache/preparation helpers)
  - `src/nextorm.core/Query/Json/JsonShapePlan.cs`, `JsonRowWriterFactory.cs`, `JsonStreamWriter.cs` (and any bounded cache helper under `src/nextorm.core/Query/Json/`)
  - `tests/nextorm.core.tests` (unit tests for shape/writer cache hit/invalidation/bounds, array-mode buffer-size threshold flushing vs NDJSON per-row flushing, cancellation/error/IgnoreNull/root semantics, and non-mutation of shared `QueryCommand`)
  - `benchmarks/nextorm.benchmark` (1k/10k/100k phase attribution benchmark, `ToList_Dto + JsonSerializer` control arm, and `SqliteBenchmarkWriteJson` 1k/10k/100k gate coverage)
  - `docs/comparisons/feature-benchmarks-2026-10-17.md` (or the designated benchmark report artifact) documenting the control arm, paired results, residual gap to global target, and explicit per-row `GetString` allocation explanation.
- **Mandatory Prohibitions**:
  - Do not materialize DTOs in `WriteJson`/`WriteJsonAsync`.
  - Do not mutate `QueryCommand.Cache` / `_dontCache`.
  - Do not weaken JSON validation, cancellation, or error handling.
  - Do not introduce benchmark-specific payload checks.
  - Do not run concurrent benchmark or container processes.
  - Do not leave LF line endings; normalize edited files to CRLF.

## 10. Risks and Stop Conditions
- **Risks**:
  - *Cache key collision across different projections of the same CLR type* (e.g., two queries projecting different subsets of columns into `Dto`): Mitigated by including the resolved column/projection signature in the cache key alongside CLR type and `JsonStreamOptions`.
  - *Buffered bytes in array mode not flushed on exception/cancellation Rules*: Mitigated by ensuring `Complete`/`CompleteAsync` and disposal/error paths follow the exact existing partial-output/error contract and testing cancellation/error mid-stream.
  - *Thermal/CPU throttling during 100k-row benchmarks*: Mitigated by paired/ABBA execution in a single serialized process.
- **Stop Conditions**:
  - If every legitimate caching and flush-batching candidate either regresses allocations (`ΔAlloc > 0 B`) or fails to improve time beyond the noise band at 1k/10k rows (or regresses 100k rows), revert the candidate and stop rather than weakening correctness or faking cache keys.
  - If the 7-case `--anyCategories=acceptance` suite shows a query-path regression `≥ 20%` caused by the change that cannot be resolved within `n ≤ 3`, revert and stop.

## 11. Confidence and Gaps
- **Verified**:
  - Root causes in `DataContext.cs:353-420` (`PrepareJsonStream`) and `JsonStreamWriter.cs:47-65` (`WriteRow` per-row flush) are directly identified in the brief.
  - Prior failure modes (`D247-json-streaming-2.md` single-tier ratio gap and unexecuted compare script, plus unstarted `r=1` in `.pdca/status/T247-json-streaming-1.md`) and the `T311`/`T250` boundaries are fully accounted for.
  - The 2026-10-20 two-tier acceptance criteria and measurement method are determinate and executable with available tools.
- **Remaining Assumptions**:
  - Exact millisecond/byte improvement of the combined shape/writer cache + array flush batching at 10,000 rows will be established by the paired ABBA benchmark run during DO/CHECK.
````

## Approval record

`not applicable: autonomous mode` — no ordinary plan approval was required or invented for this cycle (base §Control modes; the plan was produced and consumed inside the authorized `rc5` collection run, `plan_state=ready`).

In-force auto-commit policy (verbatim, repo `AGENTS.md` §Git): "a `pdca-collection` cycle started with explicit auto-commit permission may (a) commit on `collection/**` branches inside the cycle worktree and (b) `git merge --no-ff` a group branch into the current branch." No `git push`, no merge into `main`, no tag; a commit touches only the files changed by item `T247`.

## DO result

### Attempt n=2/3 (r=2)

`blocked: no safe implementation lever identified across all three mechanisms` — M1 bounded shape-plan/row-writer cache already present in code (commit c9aa8690, TerminalPlanCache + JsonRowWriterCache adapter; reused, not rebuilt). M2 document-mode prepared-descriptor reuse cannot be implemented safely without a pre-preparation key and risks temp-table/param correctness; not attempted. M3 JsonStreamWriter/Utf8JsonWriter setup reduction has no safe lever that does not touch array-mode flush semantics (AC-247-03) or risk correctness; not attempted. Build green (0W/0E); core JSON 188/188; sqlite JSON 135/135; integration 7733 passed/0 failed/201 skipped. Baseline captured: WriteJson_Array_Dto 1k 611.2 us/51.45 KB, 10k 7870.3 us/403.1 KB, 100k 79049.0 us/3919.49 KB. No candidate produced; residual to ACT. Evidence: `.pdca/T247/rv3/attempts/attempt2/`.

## CHECK record

`not run: no DO candidate existed for this revision` — no independent CHECK was dispatched and no candidate was produced; CHECK is recorded as absent, never as a pass. No verdict, no per-criterion matrix, no findings. The two DO attempts are recorded under §DO result and in the progress log; neither produced a candidate that could be checked.

## ACT record

`incomplete` — DO → ACT, no candidate produced, no CHECK ran, no PASS, no `done`, no credit.

Outcome: `incomplete` (terminal, no accepted scope). `N=1`, `r=2`, `n=2/3`; attempts exhausted (2 of 3 consumed; the third was not opened because no permitted continuation remained — all three planned mechanisms non-moving).

Both attempts, with their commits:
- attempt 1 (`42dc4a59`, status only): array-mode flush batching broke the partial-output/error-policy tests (AC-247-03) — altered the completed-row prefix / Flush=0 contract; reverted; no candidate; no axis moved.
- attempt 2 (`d1aebc3e`, status only, zero product change): all three planned mechanisms exhausted without moving an axis:
  - M1 bounded shape-plan/row-writer cache — **already exists** in code (`TerminalPlanCache` + `JsonRowWriterCache` adapter, from the earlier #180 work, commit `c9aa8690`); reused, not rebuilt. No new lever.
  - M2 document-mode prepared-descriptor reuse — judged **not safely implementable**: no pre-preparation key, temp-table/parameter correctness risk; not attempted.
  - M3 `JsonStreamWriter`/`Utf8JsonWriter` setup reduction — **no safe lever** without touching the AC-247-03 flush semantics or risking correctness; not attempted.

Candidate == baseline (no axis moved): `WriteJson_Array_Dto` 1 k **611.2 µs / 51.45 KB** (ratio 1.000), 10 k **7 870.3 µs / 403.1 KB** (1.000), 100 k **79 049.0 µs / 3 919.49 KB** (1.000). Neither Tier A nor Tier B is met (no time move beyond the pre-registered noise band, no allocation decrease).

Derived allocation bar (from the cold-vs-warm decomposition, per the #250 forward obligation — never a copied `≥8 KB/column` figure): `ΔAlloc ≤ 0 B` on `WriteJson_Array_Dto`, with a time move beyond the pre-registered noise band. Decomposition: `.pdca/T247/rv3/attempts/attempt2/decomposition.md`.

Residual to the global target: `1.000×` time / `1.000×` alloc vs baseline ⇒ the 10 k `≤1.00×` linq2db ratio remains **unmeasured in this item** — no paired same-session linq2db arm was run here, so the global ratio is neither met nor refuted by this item; the residual gap to `≤ 1.00×` is not quantified in this cycle.

Gates recorded at the current identity: build `0 W / 0 E`; core JSON **188/188**; sqlite JSON **135/135**; fail-closed integration **7 733 passed / 0 failed / 201 skipped**; `status_lint` rc 0. Evidence root `.pdca/T247/rv3/` (`attempts/attempt1/`, `attempts/attempt2/`, `decomposition.md`).

"Must never claim" list (no win, no Tier A/B credit, no verified ratio, no closure): no local-acceptance win on either axis; no Tier A or Tier B credit; no verified `≤1.00×` linq2db ratio at 10 k; no closure of issue #247; no `done`. The three mechanisms are recorded as non-moving, not as implemented. Issue #247 stays OPEN.

Handoff (next authorized action): a new authorized PLAN under §State machine and counters is required to reopen this item; this cycle is closed as `incomplete` and is not reopened automatically. The residual per-row `IDataRecord.GetString` allocation and any future lever belong to a separate `P:` task (issue #311 owns the 10 k per-row throughput lever).

### User-directed finalization (explicit user instruction, not an independent CHECK acceptance)

- Issue `#247` closed by the user: the ticket's global target is met by machinery already in the tree (`TerminalPlanCache` + `JsonRowWriterCache`, commit `c9aa8690`), with no new lever and zero product change by this item. This is a user decision, not a CHECK verdict.
- Accepted basis: already-landed `TerminalPlanCache` + `JsonRowWriterCache` (`c9aa8690`).
- Measured numbers: `1k 611.2 µs / 51.45 KB`, `10k 7 870.3 µs / 403.1 KB`, `100k 79 049.0 µs / 3 919.49 KB`.
- Residual: per-row `GetString` ≈44 B/row and a 1k fixed-cost gap reduced but not eliminated.
- Caveat: the linq2db ratio was cross-round, not same-round (same-round corroboration will be appended when run).
- No attempt/defect counter changes: `N=1`, `r=2`, `n=2/3` unchanged; no new attempt consumed; defect history unchanged.
- No criterion was rewritten; `AC-247-01..06` remain as recorded.
- This closure does not claim an independent CHECK verdict: no CHECK ran on this item; the verdict is the user's explicit instruction.

## Revision history

2026-10-30T07:05:59Z | N=1 r=1 → N=1 r=2 | genuine contract revision inside the same cycle `N=1`: the 2026-10-20 two-tier acceptance policy (global target vs local one-axis acceptance) replaces the retired single-tier `R247-01..20` row set with `AC-247-01..06`, and an explicit scope boundary is drawn against siblings `T311` (10k per-row throughput lever, same-session paired acceptance baseline harness, noise-robust ratio oracle) and `T250` (CSV terminal plan cache); the `r=1` dependency gate on the `T311` handoff is removed because `T247` closes under the local-acceptance policy. Outgoing attempt identity: `N=1, r=1, n=1/3` — DO never ran on `r=1` and no attempt was consumed, so `n` stays `1/3`.

## Defect history

Finalized for this cycle (DO → ACT, incomplete, no candidate). No stable defect key produced a fix that survived; both attempts are recorded as unaccepted:
- `T247-D1` | observed revisions/attempts: r=2 / n=1 (attempt 1); fixes applied: 0 (reverted) | evidence: `.pdca/T247/rv3/attempts/attempt1/`, commit `42dc4a59` | last recurrence/escalation: attempt 1 reverted — array-mode flush batching broke AC-247-03 (partial-output/error policy: completed-row prefix + Flush=0); no candidate.
- `T247-D2` | observed revisions/attempts: r=2 / n=2 (attempt 2); fixes applied: 0 | evidence: `.pdca/T247/rv3/attempts/attempt2/`, commit `d1aebc3e`, decomposition `.pdca/T247/rv3/attempts/attempt2/decomposition.md` | last recurrence/escalation: all three mechanisms non-moving (M1 cache already present c9aa8690; M2 not safely implementable; M3 no safe lever without touching AC-247-03); candidate == baseline; no axis moved; no Tier A/B credit.
Carried by reference, never re-litigated: the earlier attempt's terminal arms in `docs/specs/status/D247-json-streaming-2.md:557` (10k time ratio `1.04546 / 0.91917` vs the strict single-tier `0.81079`; `0/7` acceptance cases captured) — contextual evidence for the `r=2` reformulation, not a defect row of this record.

## Changed files

Task-owned records only: `.pdca/status/T247-json-streaming-1.md` (this file, ACT finalization — phase `ACT`, `incomplete`, no candidate, no CHECK); `.pdca/T247/rv3/attempts/attempt1/`, `.pdca/T247/rv3/attempts/attempt2/` (including `decomposition.md`, `baseline.md`) — local evidence, gitignored. `.pdca/status/collection-rc5.md` (queue row T247 + §Done/Verified/Incomplete + progress log) updated in the same commit. No product, test or benchmark file changed.

## Git state

`status path: .pdca/status/T247-json-streaming-1.md — modified this cycle (ACT finalization) · tracked (durable on disk, pending commit)` — branch `1.0.9-rc5`, baseline `1e31331d`. Evidence under `.pdca/T247/rv3/attempts/` untracked/local. No product, test or benchmark file changed. Work outside this item's delta untouched and unclaimed. Commit authorized for this status file plus `.pdca/status/collection-rc5.md` only (explicit paths, no `-A`), subject `#247 T247 ACT: incomplete — no candidate, all three planned mechanisms non-moving; derived allocation bar from cold-vs-warm decomposition; no credit; evidence .pdca/T247/rv3/`. No push.

## Progress log

- PLAN ready (r=1) — collection handoff at PLAN→DO; DO not started; dependency gate T311 pending

2026-10-30T07:05:59Z | PLAN | revision r=2 | attempt n=1 | PLAN r2 completed — handoff at PLAN→DO boundary (form=cycle, overlay=coder+.NET, N=1, r=2, n=1/3, plan_state=ready; criteria AC-247-01..06; r1 superseded by a genuine contract revision — two-tier acceptance + explicit scope boundary against T311/T250; DO never ran, no attempt consumed) | `.pdca/status/T247-json-streaming-1.md` §PLAN record r2, §Revision history; evidence root `.pdca/T247/rv3/`
2026-10-31T04:25:00Z | DO | attempt n=1 | blocked | no safe implementation lever; flush batching reverted (partial-output AC); build 0W/0E, core JSON 188/188; no candidate; residual to ACT | `.pdca/status/T247-json-streaming-1.md` §DO result

2026-10-31T05:30:00Z | DO | attempt n=2/3 | blocked | all three mechanisms exhausted (M1 cache already present, M2 not safely implementable, M3 no safe lever); build 0W/0E; core JSON 188/188; sqlite JSON 135/135; integration 7733 passed/0 failed; baseline 1k 611.2us/51.45KB 10k 7870.3us/403.1KB 100k 79049us/3919.49KB; no candidate; residual to ACT | `.pdca/status/T247-json-streaming-1.md` §DO result; `.pdca/T247/rv3/attempts/attempt2/
2026-10-31T06:00:00Z | ACT | revision r=2 | attempt n=2/3 | incomplete (DO -> ACT, no candidate, no CHECK): attempt 1 (42dc4a59) flush batching reverted (AC-247-03); attempt 2 (d1aebc3e) all three mechanisms non-moving (M1 cache already present c9aa8690, M2 not safely implementable, M3 no safe lever); candidate == baseline 1k 611.2us/51.45KB 10k 7870.3us/403.1KB 100k 79049.0us/3919.49KB; no Tier A/B credit; derived bar DeltaAlloc <= 0 B with time move beyond noise band (attempts/attempt2/decomposition.md); gates build 0W/0E, core JSON 188/188, sqlite JSON 135/135, integration 7733/0/201; issue #247 stays OPEN | `.pdca/status/T247-json-streaming-1.md` section ACT record; `.pdca/T247/rv3/`
2026-10-31T06:30:00Z | ACT | revision r=2 | attempt n=2/3 | user-directed finalization: issue #247 closed by the user — global target met by already-landed TerminalPlanCache + JsonRowWriterCache (c9aa8690), zero product change by this item; outcome done; no CHECK verdict claimed; no counter/criterion change | `.pdca/status/T247-json-streaming-1.md` §ACT record (user-directed finalization block)
