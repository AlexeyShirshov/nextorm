# T311 — JSON streaming: attribution and noise-robust acceptance

> **2026-10-20 — rc5 acceptance reformulation (policy supersession).** The GitHub issue now uses the two-tier policy: **global** allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db; **local** acceptance = one-axis improvement with no regression on the other (allocations down + time not worse, or time better + allocations not up). This file's frozen thresholds and plan are the historical rc3 attempt. Before any rc5 execution, a new plan revision (`r+1`) must be planned from the reformulated issue; `N`/`r`/`n` are not reset.

`selected_variant: pdca-dotnet; collection: rc3-4; task: T311; issue: #311; N=1; r=1; n=1; plan_state: ready; DO: not started`

- Issue: [#311](https://github.com/AlexeyShirshov/nextorm/issues/311); milestone: **1.0.9-rc3**.
- Status file: `.pdca/status/T311-json-streaming-1.md`; evidence contract: **rv1**.
- Current repository **R**: `/home/alex/sources/nextorm`; branch: `1.0.9-rc3`.
- Evidence root **E**: `/home/alex/sources/nextorm/.pdca/T311/rv1`.
- These are gate clarifications, not a redesign: **r=1, n=1 remain unchanged**.
- **Collection PLAN ends at PLAN→DO. No DO, measurements, staging, or commits occur at this boundary.**

## Goal

Attribute the remaining JSON streaming fixed/per-row costs, remove independently measured avoidable work, and evaluate the 10k lever using a newly pre-registered, reproducible oracle.

Constraints:
- Preserve JSON bytes, streaming/error/cancellation contracts, provider behavior, and bounded context-local caching.
- Never alter T247's frozen thresholds or rewrite its historical GAP.
- Capture a clean-product-HEAD baseline before any product edit, including temporary instrumentation.
- Execute sequentially in the **current worktree/branch**; one collection group, lane cap **1**; create no task/group branches or worktrees.
- Protect all **77 foreign dirty paths**: 45 tests, 16 BDN result artifacts, 10 `.pdca`, 4 `docs/superpowers`, `.github/workflows/dotnet.yml`, and `AGENTS.md`. Never edit or stage them.
- At ACT, auto-commit is authorized: stage only explicitly registered T311-owned paths using `git add <task-owned paths>`; **never `git add -A`**. Commit message starts **`#311`**. No push or merge; collection parent owns integration.

Minimal solution: measure before optimizing; change only demonstrated hotspots; add a separate T311 oracle/collector rather than modifying T247's gate.

Design checklist: no public API expansion; no unnecessary dependency; no mutable shared-command state; no unbounded/process-wide cache; no weakened assertions, coverage gates, or provider execution.

## Acceptance criteria (with IDs)

| ID | Required result | Negative case |
|---|---|---|
| R311-01 | Preserve T247 history, frozen files, and predecessor identity. | Changed threshold bytes, historical GAP relabelled PASS, or missing predecessor code blocks acceptance. |
| R311-02 | Freeze new oracle/spec/collector before their first live measurement; validate collector computation at DO admission. | Missing receipt, post-measurement rule changes, or collector validation failure blocks measurement. |
| R311-03 | Two same-session baseline rounds, seven controls and JSON arms, from declared clean product HEAD before edits. | Missing/duplicate cases, contaminated baseline, broken session, or unrecorded replacement invalidates pairing. |
| R311-04 | Independent phase ledger closes public time within `max(25,000 ns/call, 5% of public time)` per measured round/size. | Subtraction alone, overlapping phases, or excessive unexplained residual fails attribution. |
| R311-05 | Remove/reduce a measured 10k avoidable lever, prove operation-count change, and pass the new primary oracle. | Attribution without implementation, unmeasured savings, or FAIL/INCONCLUSIVE is not completion. |
| R311-06 | All seven acceptance controls satisfy fixed regression limits and execution budget. | Missing control, timeout, regression, or case substitution blocks acceptance. |
| R311-07 | Preserve the complete variant matrix, including failure/partial-output behavior. | Changed bytes, swallowed cancellation, invalid partial output, or incorrect provider bypass fails. |
| R311-08 | Preserve cache bounds, isolation, generation invalidation, bypasses, and shared-command safety. | Sticky command mutation, stale shape, duplicate terminal caches, or unbounded growth fails. |
| R311-09 | Scoped tests and one boundary sweep pass; coverage meets 85/75 and integration 65/75. | Zero selected tests, missing branch evidence, unexpected provider omission, or lowered gates fails. |
| R311-10 | Run Stryker on changed product types; kill or individually justify survivors. | Unexplained survivor, missing run, or tool failure remains open. |
| R311-11 | Accurate affected EN/RU prose; stale benchmark comment corrected; explicit XML-doc decision. | Unsupported claims, unilateral language changes, or stale compile-per-call claim fails. |
| R311-12 | Complete ledger, staged bodies, independent review, and both manifest validators succeed. | Missing bodies, drift, missing verdict, or applicable open row forbids PASS. |

**All R311-01…12 and their binding evidence rows are P1 by construction.** The stale-comment correction itself is P2 hygiene and cannot substitute for a P1 obligation.

## Order & dependency (T247↔T311)

1. T247 implementation is already in HEAD; its re-entry is **acceptance-oracle re-entry**, not missing implementation.
2. T311 owns the new oracle/spec, collector, paired baseline, attribution, and optimization.
3. Order: **T311 pre-registration → baseline → attribution/optimization → CHECK → T247 acceptance-only re-entry**.
4. T247 requires neither its own PLAN nor a commit first. Its later acceptance-only PLAN must reference the approved new oracle and preserve its original obligations/history.
5. Required predecessor: HEAD `0d4b0cf69489253249305e0900f9fe5e4f69c1c5`, including the live bounded row-writer cache and preparation wiring; committed clean `.pdca/T247/rv2/**`.
6. Historical T247 rounds are provenance/fixture inputs, not the new live paired baseline. Historical acceptance `cases.json` is not fabricated.
7. **Overlay invariant 12:** re-scope the failed timing proof to the new reproducible oracle; do not silently retry the same discordant-round proof.
8. Missing/failed arms remain missing/failed/inconclusive; never substitute cases or average disagreement into PASS.
9. T250 and T247/T311 share **one bounded terminal-plan-cache mechanism**. Any preparation-cache extension reuses the agreed context-owned mechanism, not a second LRU. Overlapping work is sequential.
10. T311 completion does not complete T247's unfinished D/R units or erase its terminal GAP; their acceptance requires their own CHECK.

**Obtainability argument supporting `ready`:**
- Declared HEAD contains the committed T247 product/harness; committed baseline/final round files contain executed-arm `mean_ns`/`allocated_b`.
- JSON arms, seven controls, and BDN filters are committed and have executed.
- The new collector's statistical/normalization layer is pure computation over these input kinds plus a documented `cases.json` writer; it introduces no unavailable product arm or historical-state requirement.
- D1 validates that computation against committed inputs and explicit fixtures. The fresh live paired baseline is **D2**, not missing Gate-1 evidence.
- D1 failure means **DO→PLAN**, not a silent waiver or a second planned PLAN phase.

## New oracle spec (pre-registered)

New spec: **`docs/specs/performance/json-ratio-oracle-1.md`**.
Planned collector: **`.pdca/scripts/perf/t311_json_gate.py`**.
Planned tests: **`.pdca/scripts/perf/tests/test_t311_json_gate.py`**.

| Option | Benefit | Cost/risk |
|---|---|---|
| Min-of-9 per arm | Simple lower-envelope estimate; suppresses positive noise. | Different minima may represent different conditions; selection bias. |
| Paired log-difference with noise guard | Uses pairing, explicit uncertainty, no best-run selection. | More samples; near-threshold results can remain inconclusive. |
| Paired primary plus min-of-9 diagnostic | Auditable comparison without favourable-statistic substitution. | Extra reporting, not extra acceptance power. |

**Choose option 3.** Only paired log-difference decides acceptance; min-of-9 is diagnostic.

Frozen spec content:
- Sizes: **1k/10k/100k**; public arms `WriteJson_Array_Dto` and `Linq2Db_ToList_Json`.
- Two rounds; **N=9 implementation pairs per size/snapshot/round**; no optional stopping or extra confirmation runs in rv1.
- Each pair uses identical BDN jobs/configuration; alternate implementation order using a seed derived from the frozen spec digest.
- Retain raw BDN observations, means, allocations, process results, and order.
- Define `d_i=ln(nextorm_mean_ns/linq2db_mean_ns)`; report `exp(mean(d))` and 99% interval `exp(mean(d) ± 3.355×sample_sd(d)/√9)` separately per round.
- T311 v1 limits: **1k ≤1.20; 10k ≤1.00; 100k ≤0.95**. These are new rules, not amended T247 thresholds.
- PASS: every round's upper bound ≤ limit. FAIL: both rounds' lower bounds > limit. Otherwise INCONCLUSIVE; never average discordant rounds.
- Freeze noise bound `s=max(0.19,0.20,0.30)=0.30`, from documented approximately 19%, 20%, and ±30% variability. This is an operational ceiling, not an estimated standard deviation or threshold allowance.
- Mark unstable if any paired ratio differs from its median by a multiplicative factor >1.30, or first-three/last-three geometric means differ by >1.30. Unstable → INCONCLUSIVE.
- Diagnostic minimum ratio: `min(nextorm)/min(linq2db)`; cannot promote FAIL/INCONCLUSIVE.
- Baseline→final 10k improvement uses differences of normalized log ratios indexed by round/sample; its 99% interval upper bound must be <0 in both rounds. Report temporal-drift limitations.
- Record allocations at every size; 10k remains **≤446,095 B/op**, without KiB rounding.
- Seven controls: each final-round mean ≤1.20× corresponding baseline-round mean; collect all seven within **240 seconds per acceptance round**, measured wall time. Timeout fails; do not change jobs/cases afterward.
- Reject missing/zero/duplicate samples, non-finite values, mismatched parameters/jobs, incorrect source/DB identity, or fewer than nine pairs.
- Exit protocol: `0=valid success/PASS`, `1=FAIL`, `2=INCONCLUSIVE/invalid`; structured reasons distinguish invalid input.
- Audit preserves T247 outcomes separately; an old GAP does not become a new PASS.

Frozen `cases.json` schema:
- Top level: `schema_version,task,contract_rv,spec_sha256,collector_sha256,snapshot,source_identity,session_id,baseline_generation,round,generated_utc,db_sha256,environment,cases[]`.
- Case: `case_id,kind,implementation,parameters,pair_index,order,job_fingerprint,mean_ns,allocated_b,raw_artifacts[],command,exit_code,elapsed_seconds`.
- Controls use `implementation=nextorm`; JSON uses nextorm+linq2db. Do not invent linq2db versions of nextorm-only controls.
- Comparison records samples/statistics/intervals/noise checks/limits/verdict/reasons, improvement intervals, diagnostic minima, allocations, and separate T247 audit rows.
- Execution registry records commands, stdout/stderr, exits, timestamps, counts, source/tool/dependency identities, and receipt hashes.

Pre-registration:
- Freeze spec, collector, fixture tests, exact commands/jobs, schema, seed, limits, stopping rule, HEAD/product identity, and DB pin.
- Write `E/prereg/receipt.json`, frozen copies and SHA-256 inventory before the first live measurement.
- Bind new uncommitted task-owned spec/harness bytes by digest and patch, alongside HEAD and UTC/monotonic timestamps; no pre-measurement commit is required.
- Verify protected T247 bytes at admission, before baseline/final, and at CHECK.
- Rule changes require PLAN and explicit rv supersession, preserving IDs/obligations and adding new variant IDs; never overwrite measured rv1.

| Protected file | SHA-256 |
|---|---|
| `.pdca/scripts/perf/t247_json_gate.py` | `d0c6a4b8c806d206e837d5c73ca646acb48559193eec92e1a3cdd3821212d8cb` |
| `docs/specs/status/D247-json-streaming-2.md` | `b9522ae8a9aadc666212282b1c9dbc3b122179fab3791da0c5189a14b8481034` |
| `docs/specs/status/D247-json-streaming-1.md` | `9f4c2d6d611e3ba7e67df65f5ca43ebbb8d04d7bf759111a477815a553e69274` |
| `docs/specs/performance/feature-benchmark-followups-2026-10-17.md` | `ed4dc824f2398432c2d425221ac1c41f52ac9f6a2b7f93b6674c66880c9c8cb7` |

Inventory every other supplied frozen-threshold encoding, including collection/public comparison bytes and issue #247 snapshot. Do not edit them.
Run the original evaluator on copied evidence for audit. Preserve PASS/FAIL/INCONCLUSIVE and `t247-budget-rows-missing`; do not manufacture budget rows.

## Same-session paired baseline

Canonical files:
- `E/baseline/round-1/acceptance/cases.json`
- `E/baseline/round-2/acceptance/cases.json`
- `E/baseline/round-{1,2}/public-json/cases.json`
- Corresponding `E/final/round-{1,2}/...`; raw results beneath each round's `results/`.

Seven exact controls:
`InMemoryBenchmarkAggregates.Nextorm_Count`; `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount`; `SqliteBenchmarkAny.Nextorm_Cached`; `SqliteBenchmarkCachedPlan.Prepared_ToList`; `.Cached_ToList`; `.Cached_PlanOnly_Param`; `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync`.

Capture nextorm+linq2db JSON at 1k/10k/100k, two rounds. Discover/assert identities; explicitly exclude the eighth SQL Server acceptance match.
Collect the complete 33-case JSON class once per round as auxiliary T247-audit input; it does not replace the nine primary pairs.

Host protocol:
1. Verify declared HEAD/product bytes, protected digests, foreign-path inventory, toolchain/jobs, and both DB copies.
2. Acquire non-blocking `/tmp/opencode/t311-measure.guard`; duplicate exits 90. Acquire exclusive `/tmp/opencode/nextorm-bdn-host.lock`; retain owner/session logs through baseline, attribution, treatment, and final.
3. Set `NEXTORM_BENCH_FULL=1`; DB pin is `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` for both known DB paths.
4. Initial baseline must precede **any product edit**. Record completion before instrumenting.
5. **Prescribed sequence:** initial baseline → instrument/attribute → remove instrumentation and restore original product bytes → **always re-capture baseline** → apply the declared lever → final.
6. Archive the initial baseline losslessly under `E/baseline-history/generation-1/`, with its receipt/digests. Publish the fresh generation-2 baseline at the canonical paths; retain both generations and the replacement ledger.
7. Temporary instrumentation is never the final treatment. Any diagnostic/undeclared product change after baseline invalidates that pairing until removed and baseline re-captured before applying the final lever.
8. Only the registered final-treatment delta may separate the accepted baseline and final. Any other product/collector/job/DB/environment drift invalidates the comparison.

The valid product baseline is declared HEAD **with T247 already implemented**. Foreign dirty paths remain unchanged and fingerprinted; they are neither silently absorbed into T311 nor restored.
Session loss/suspension or unintended drift requires a stopped run, preserved evidence, and DO→PLAN admission/restart handling; no relabelling a later run.
The predeclared generation-1→generation-2 recapture changes evidence generation, not contract rv.

Planned collector commands, run from R; substitute R/E with the absolute paths in metadata:
- `python3 .pdca/scripts/perf/t311_json_gate.py prereg --repo R --evidence-root E --spec docs/specs/performance/json-ratio-oracle-1.md`
- `python3 .pdca/scripts/perf/t311_json_gate.py baseline --repo R --evidence-root E --rounds 2 --pairs 9 --generation 1`
- `python3 .pdca/scripts/perf/t311_json_gate.py attribution --repo R --evidence-root E`
- `python3 .pdca/scripts/perf/t311_json_gate.py baseline --repo R --evidence-root E --rounds 2 --pairs 9 --generation 2`
- `python3 .pdca/scripts/perf/t311_json_gate.py final --repo R --evidence-root E --rounds 2 --pairs 9`
- `python3 .pdca/scripts/perf/t311_json_gate.py compare --repo R --evidence-root E`
- `python3 .pdca/scripts/perf/t311_json_gate.py audit --repo R --evidence-root E`

The driver logs exact BDN invocations: `dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- --filter <frozen exact filters> --exporters json csv --artifacts <absolute task-owned sample directory>`.
Freeze seven exact control filters, the two verified JSON-method filters, and auxiliary class filter. Do not use broad category selection or the unrelated legacy SQL Server baseline wrapper.

## Attribution & levers

After initial baseline, use temporary phase instrumentation, timer-overhead calibration, and matched uninstrumented public invocations. Sampling traces corroborate stacks; they do not replace timing.

Measure exclusive phases in **ns/call**, allocations/call, counts/call, and **ns/row**:
- Clone/bind/shape/cache lookup/build; writer creation and sink rent.
- Connection/command/reader setup, first-read, access/value dispatch, JSON emission.
- Flush/destination writes, async awaits, disposal, explicit unclassified time.
- Use 1/10/100 rows for intercept diagnostics and 1k/10k/100k for slope/closure.

Require closure within **max(25 µs, 5%)** per round/size; exclude double-counting and report instrumentation overhead.
Historical 485,799/776,716 ns remains labelled algebraic residual; do not claim it independently measures preparation.

| Verified location | Hypothesis / bounded action—not claimed savings |
|---|---|
| `DataContext.cs:415,428,432-433`; `JsonShapePlan.cs:131-280` | Repeated clone/shape work; reuse immutable preparation within the single bounded mechanism if dominant. |
| `JsonStreamWriter.cs:36-37`; `PooledStreamBufferWriter.cs:30,155` | Writer/rent fixed costs; simplify only with measured benefit and ownership safety. |
| `JsonStreamWriter.cs:47-86` | Per-row flush/reset/destination overhead; batching must preserve visibility/backpressure/cancellation contracts. |
| `JsonRowWriterFactory.cs:535-546,654-706` | Repeated dispatch/boxing; bind typed decisions once if measured. |
| `JsonRowWriterFactory.cs:455-461` | Array access; optimize only if represented and dominant. |
| `SqliteBenchmarkJsonFixedCost.cs:13` | Correct stale compile-every-invocation comment as hygiene. |

Select the largest independently measured avoidable component contributing ≥10% of 10k public time; apply the smallest compatible lever.
No qualifying lever or excessive required scope → DO→PLAN. Prove removed repetition with counts/final timings; account for necessary I/O rather than claiming all residual removable.

## Variant matrix

| Axis / variants | Closure |
|---|---|
| 1k / 10k / 100k | **Test+measure** both JSON implementations, two rounds; 10k improvement criterion. |
| Array / NDJSON / root / IgnoreNull | **Test** core+SQLite suites; byte-identity guards. |
| Nullable/default/string/numeric/date/Guid/Base64/array | **Test** writer/shape/column branches, null/value and signed/unsigned values. |
| Value/reference / nested shapes | **Test** semantic shape/cache keys; no unsupported-shape expansion. |
| Sync / async | **Test** both terminals; registered performance arm stays unchanged. |
| Cancellation / partial output / sink failure | **Test** core and real SQLite; preserve existing contracts. |
| Managed / native SQL Server | **Test** eligibility/bypass plus live native behavior. |
| Cache cold/warm/cleared/capacity/isolation | **Test+count** preparation; preserve bound 64 and generation invalidation. |
| SQLite/Postgres/SQL Server/MySQL/ClickHouse/MariaDB | **Test** registered live-provider JSON behavior at boundary; preserve existing alias/capability guards. |
| In-memory | **Guard+test** unsupported terminals throw; unit-only. |
| Non-SQLite provider performance | **Deferred** until provider-specific claim or changed provider/native execution; correctness remains mandatory. |

## Test strategy

Every unit brief records `projects,selectors,files,rebuild,boundary,rationale,integration_projects,integration_boundary_runs`; validate before edits using `python3 .pdca/scripts/validate_inner_loop.py brief <brief.json>`.
Before editing a candidate file, compare it with the foreign-path inventory. If foreign-dirty, do not edit it: use task-owned companion tests or return PLAN if replacement is essential.

Tests use `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0`, `timeout 600`, stdout/stderr logs and actual exit codes; `tee` must not hide failures.
- Harness: `python3 -m unittest discover -s .pdca/scripts/perf/tests -p test_t311_json_gate.py`; repeat with `test_t247_json_gate.py`.
- Planned fixtures cover intervals/noise boundaries, zero variance, missing/duplicate/zero data, source/order/pre-registration errors, timeout, lock conflict, and no diagnostic promotion.
- Writer unit: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~NextORM.Core.Tests.JsonStreamWriterTests|FullyQualifiedName~NextORM.Core.Tests.JsonShapeWriterTests|FullyQualifiedName~NextORM.Core.Tests.JsonColumnTests"`.
- Cache/native unit: same project with `"FullyQualifiedName~NextORM.Core.Tests.JsonRowWriterCacheTests|FullyQualifiedName~NextORM.Core.Tests.JsonRowWriterCacheDataContextTests|FullyQualifiedName~NextORM.Core.Tests.JsonNativeStreamTests"`.
- SQLite unit: `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NextORM.Sqlite.Tests.JsonStreamingTests"`.
- Companion tests in new task-owned files use planned traits `T311=writer`, `T311=cache`, or `T311=sqlite`; run the corresponding project with `--filter "T311=<unit>"`. Verify actual symbols through Roslyn; do not claim future symbols exist.
- Every selector must execute >0 tests. FluentAssertions only; preserve CRLF. Scoped unit runs unlimited; no integration inside units.

**One DO→CHECK boundary sweep; `integration_boundary_runs=2`:**
1. `dotnet build -c Debug`; warnings remain errors.
2. `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o E/coverage/solution.cobertura.xml "dotnet test --no-build -c Debug"`.
3. `dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o E/coverage/integration.cobertura.xml "dotnet test tests/nextorm.integration.tests --no-build -c Debug"`.
4. Inner tests receive the safety environment/600-second timeout via the runner; retain outer collector exits/logs too.
5. Load integration skill; set `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; start/recheck Podman if needed. No provider opt-outs.
6. Run reportgenerator for both files with separate E/coverage targets and `-reporttypes:"Html;TextSummary"`; integration adds `-classfilters:"-NextORM.Core.InMemory*"`.
7. Require solution **85% line/75% branch**, integration **65% line/75% branch**. Record counts, changed-code branch coverage/deltas and comparable baseline deltas where available; never invent a matched historical measurement.
8. Record skips/reasons and provider execution. Diagnostics use `FullyQualifiedName~WriteJson` or verified provider selectors; never `~CommonTestSuite`. Further live runs require PLAN.

Mutation: `dotnet stryker --config-file E/mutation/stryker-config.json`, restricted to changed product types and scoped core tests.
Record tool/configuration, killed/surviving mutants and individual equivalence/guard evidence. Missing tooling is a prerequisite, not passing N/A.

## Docs plan

- Create the internal oracle spec; leave frozen acceptance/follow-up/comparison files byte-identical.
- Correct `SqliteBenchmarkJsonFixedCost.cs:13`.
- Update affected EN/RU JSON/streaming guides only for actual observable changes; qualify performance statements.
- Publish results, if needed, in new `docs/comparisons/json-streaming-t311.md` and `docs/ru/comparisons/json-streaming-t311.md`; no public links to specs.
- Change execution/cache explanations in EN/RU only if the implementation changes them.
- **Public XML-doc/API signatures untouched by default**; if semantics require changes, update corresponding EN/RU prose together.
- Generated API/site output untouched; verify `dotnet docfx docs/docfx.json`.

## Perf/recon/unit-mode decisions

- **Perf required:** paths are per-call/per-row (`DataContext.cs:432`, `QueryExecutor.cs:1182-1241`, `JsonStreamWriter.cs:47-86`); use fresh paired baseline, not historical ratios.
- **Recon required within DO:** collector fixtures and phase instrumentation; success means valid computation, actual complete artifacts, and numeric closure. No benchmark in this PLAN.
- **Mode:** sequential current-worktree execution; footprint overlaps preparation/writer/cache/test contracts, collection forbids extra branches/worktrees.
- Confidence: predecessor/obtainability argument **high**; collector implementation **to validate in D1**; live baseline **to capture in D2**; hotspot/savings **unknown until measurement**.
- A failed admission is DO→PLAN. Persistent low confidence after targeted reconnaissance routes to `escalate`, trigger 5.

## Versioned evidence contract

Contract **rv1**, no superseded revision. Persist before DO; machine-readable form `E/contract.json`.
Frozen manifest: **`.pdca/T311/rv1/manifest.json`**.
Sources/artifacts below are **planned**, except declared HEAD and committed T247 evidence.

Common slots for every row: `rv=1`, stable requirement/row IDs, required scenario, kinds/sources, exact invocation, required result/exit, stdout/stderr log, expected artifact locations, owner, observable predicate.
All applicable rows require successful results; expected negative fixtures and historical audit outcomes are recorded explicitly. Missing/failed/not-run/blocked evidence stays open.

| Row / requirement | Required check and invocation | Evidence kinds/sources → artifacts | Owner; applicability |
|---|---|---|---|
| E311-01 / R01 | Admission/source check; collector `prereg` | Ancestry/diff, protected hashes, foreign ownership inventory, historical GAP → `E/prereg/source.json`, receipt/log | source-scout; always |
| E311-02 / R02 | Freeze plus both Python unittest commands | Frozen bodies/digests, committed-input computation, synthetic fixtures explicitly labelled → `E/prereg/`, `E/logs/oracle-tests.log` | oracle; always |
| E311-03 / R02,03 | Admission obtainability reconciliation; `prereg` | Committed arm/control/filter references, prior execution inputs, writer validation → `E/prereg/obtainability.json` | oracle + scout; always |
| E311-04 / R03 | Both `baseline` commands, generations 1/2 | Cases/raw BDN, pre-edit/restoration receipts, DB/job/source/lock identity → canonical baseline paths and `E/baseline-history/` | perf; always |
| E311-05 / R04 | Collector `attribution` | Exclusive phases/counts/calibration/closure, temporary patch and removal proof → `E/attribution/` | attribution; always |
| E311-06 / R05 | Collector `final`, then `compare` | Product diff/counts, paired intervals/verdict/exits → `E/final/`, `E/compare/oracle.json` | engine + perf; always |
| E311-07 / R01,05 | Collector `audit` | Original evaluator results/reasons and no-edit hashes; old GAP allowed, invocation errors not allowed → `E/audit/` | oracle; always |
| E311-08 / R06 | Collector `compare` | Seven case regressions/counts/elapsed budgets → `E/compare/acceptance.json` | perf; always |
| E311-09 / R07 | Writer selector and companion-trait commands | Verified tests/counts/results/logs → `E/tests/writer/` | writer-tests; always |
| E311-10 / R07,08 | Cache/native selector and companion commands | Bounds/invalidation/isolation/native guards → `E/tests/cache-native/` | cache-tests; always |
| E311-11 / R07 | SQLite selector and companion commands | Real SQLite semantics/failure/cancellation → `E/tests/sqlite/` | sqlite-tests; always |
| E311-12 / R09 | Validator `brief <unit brief>`, `report E/report.json`, `journal E/journal.json`, `project E/project.json` | Valid scope, executions/exits, boundary projection → `E/briefs/`, named reports/logs | executor; always |
| E311-13 / R07,09 | Boundary/coverage/reportgenerator commands above | Actual providers/skips, coverage counts/branch deltas/gates → `E/boundary/`, `E/coverage/` | boundary-tests; always |
| E311-14 / R10 | Stryker command above | Tool/config/reports/survivor disposition → `E/mutation/`; N/A requires observed zero product-type diff | mutation; changed product types |
| E311-15 / R11 | Protected audit, EN/RU/XML review, DocFX command | Doc inventory/body review/build exit → `E/docs/` | docs; always; unchanged decisions explicit |
| E311-16 / R12 | Manual body copies; independent scout/Roslyn invocation | Original/staged paths, hashes, working-state identity/equality, resolved symbols → `E/reviewer-pack/files/`, receipt | executor stages; scout verifies; always |
| E311-17 / R12 | Independent `check` invocation against manifest/pack | Row reconciliation, P1 verdict, drift check → `E/check/verdict.md` | check; always |
| E311-18 / R12 | `python3 .pdca/scripts/validate_inner_loop.py manifest E/manifest.json`; `python3 .pdca/scripts/pdca_evidence.py lint --repo R --manifest E/manifest.json` | Both exit **0**, full logs/frozen manifest → `E/manifest.json`, `E/logs/manifest-*.log` | evidence + check; always |

Ledger records actual evidence per row/rv, commands, exits, verified symbols, logs, predicates and failures. N/A needs an observed predicate, never absent reporting.
Contract JSON includes `task,issue,status_file,path,base_ref,collection_baseline_ref,footprint,exclusions,contract_rv,supersedes,required_rows,rows`.
Freeze manifest last/atomically at DO→CHECK: `task,tree,contract_rv,status_file,required_rows[],rows[]{id,status:met|open|na,evidence[]|reason|predicate}`.
Binding applicable open row forbids PASS.

Reviewer pack contains full changed-file bodies, copied not moved. Independent scout checks content equality and Roslyn resolution against the same current-worktree snapshot, including uncommitted identity—not HEAD alone.
Drift invalidates review and requires new rv/receipt/CHECK. CHECK registry amendments are append-only.

**CHECK re-gather budget: 2 targeted read-only gathers; owner: `check`.**
Allowed: missing logs/bodies and unchanged-snapshot digest/registry reconciliation. No new measurements, product edits, mutation, or integration runs.
Exhaustion leaves evidence open. Missing reports alone do not justify DO/revision. New required variants require PLAN, explicit supersession, preserved IDs/obligations and added variant IDs.

## DO tasks

All tasks are active, **fix now**, unstarted. Validate briefs before edits; update todos/ledger at every transition.

- **D1 — Admission and pre-registration.** Verify HEAD/predecessor, protected hashes, 77-path foreign inventory and task ownership. Build/validate planned collector/spec and fixtures; freeze them against HEAD/current-state identity; write receipt. No product edit. Failure → DO→PLAN.
- **D2 — Initial paired baseline.** Acquire host/session lock; collect both rounds/controls/JSON before any product edit; validate counts, identities and artifacts.
- **D3 — Attribute and restore.** Instrument verified preparation/writer/executor phases, calibrate and close totals; identify qualifying lever. Remove instrumentation and prove exact original product restoration.
- **D4 — Re-capture baseline.** Preserve generation 1 and its receipt; always capture generation 2 at canonical paths before final treatment. Recheck frozen collector/protected bytes and session identity.
- **D5 — Implement measured lever and tests.** Apply smallest qualifying action; preserve shared-command/cache contract. Use only task-owned files; companion tests avoid foreign paths. Agree the single T250/T247 mechanism before cache extension.
- **D6 — Mutation, hygiene and docs.** Run scoped tests/Stryker, resolve survivors, correct stale comment and affected EN/RU prose. Finish all executable changes before final measurement.
- **D7 — Final measurement and boundary.** Same session: final → compare → T247 audit; then one planned boundary sweep and coverage. No post-result tuning; failure/inconclusive → PLAN.
- **D8 — Evidence/CHECK/ACT handoff.** Validate reports, stage reviewer copies, independent scout receipt, frozen manifest and both validators; request CHECK. Only after accepted CHECK, ACT stages explicit T311-owned paths and commits with `#311` prefix. No push/merge; parent owns integration.

No implicit extra optimization iteration. Prerequisites block the original D task without completing/superseding it; actual scope replacement requires explicit superseded→replacement mapping.
Ignored evidence is retained in E; do not force-add it unless collection policy explicitly requires it.

## Risks

- Noise may leave parity inconclusive; no post-hoc band, statistic switch, or extra samples.
- Collector implementation errors are caught at D1; committed inputs establish obtainability, not correctness of unwritten code.
- Instrumentation perturbation requires calibration, closure, restoration and mandatory baseline recapture.
- Long host-lock session may be interrupted; preserve evidence and stop rather than silently repair pairing.
- Flush batching can alter visibility/backpressure/cancellation; existing contracts remain P1.
- Cache reuse can leak mutable bindings; bound/isolation/invalidation/shared-command guards remain mandatory.
- Foreign test/config changes can affect execution: fingerprint them, never edit/stage them, and report resulting blockers.
- Missing tooling is an additive prerequisite; unrelated coverage deficits require PLAN rather than broad opportunistic edits.
- New oracle PASS and historical T247 GAP are separate statements; neither automatically completes T247.
- Unspecified matters: baseline identity proven; historical acceptance absence resolved prospectively; hotspot measured in D3; public API unchanged; non-SQLite performance deferred with trigger; no open Gate-1 blocker.

## Refs

- [#311](https://github.com/AlexeyShirshov/nextorm/issues/311), [#247](https://github.com/AlexeyShirshov/nextorm/issues/247); milestone 1.0.9-rc3.
- `D247-json-streaming-2.md:54,62,68-72,570-582`; committed `.pdca/T247/rv2/{baseline,final}/round-{1,2}/round.json`.
- `.pdca/T247/rv2/phases/phase-attribution.json:54-61`; `t247_json_gate.py:120-129,1048-1097,1773-1778`.
- `acceptance-benchmarks.md:43-51,77-79,112-116`; `decisions-and-lessons.md:106,233-237,252-257`.
- `DataContext.cs:415-433`; `QueryExecutor.cs:1182-1280`; `JsonStreamWriter.cs:36-86`; `JsonRowWriterFactory.cs:455-706`; `JsonRowWriterCache.cs:251-342`.
- Canonical `pdca-dotnet` contract; nextorm overlay invariant 12; collection current-worktree/auto-commit policy.

**PLAN→DO boundary: `plan_state: ready`; DO not started. Persist this status file only; no execution or commit at this collection PLAN boundary.**

## Current state

- task: `T311` · form: `cycle` · specialized skill: `pdca-dotnet` (`coder+.NET`)
- mode: `autonomous` (collection `rc5` lane)
- cycle: `N=1` · revision: `r=2` · attempt: `n=1` (DO never ran on this cycle; no attempt consumed)
- scope: the whole item goal of issue `#311` — pre-registered noise-robust ratio oracle `docs/specs/performance/json-ratio-oracle-1.md`, same-session paired `7/7` acceptance baseline, and an attributed, implemented and ABBA-measured 10,000-row per-row JSON streaming throughput lever in `src/nextorm.core` on snapshot `1e31331d`, branch `1.0.9-rc5` · status path: `.pdca/status/T311-json-streaming-1.md`
- goal: the 10k JSON streaming per-row path is attributed, optimized and judged under the pre-registered noise-robust oracle, closing the task either on the global target (time `≤ 1.00×` / allocations `≤ 1.20×` linq2db at 10,000 rows) or on a measured same-round one-axis local win with no regression on the other axis (residual gap recorded), while output bytes, streaming/error/cancellation contracts, provider behavior and cache bounds stay intact — the §PLAN record `r2` and §Original task are authoritative
- phase: `DO` · record: `completed` · outcome: `pending` (DO attempt n=1/3: oracle spec written, per-row lever (JsonEncodedText property names) implemented + unit-tested; paired baseline/ABBA measurement not yet captured — residual; candidate delivered for CHECK, no verdict issued)
- source: issue `#311` (`AlexeyShirshov/nextorm`), milestone `1.0.9-rc5`; re-opened by collection `rc5` · context refs: `.pdca/status/collection-rc5.md` §`## Input` → `### #311 — Perf: JSON streaming — 10k per-row throughput lever, same-session acceptance pairing, noise-robust ratio oracle (#247 follow-up)` (authoritative task text), `docs/specs/status/D247-json-streaming-2.md` (predecessor terminal attempt, read-only), baseline `1e31331d`
- criteria: `C311-01`, `C311-02`, `C311-03`, `C311-04`, `C311-05` — exact text in §PLAN record `r2` §4, not retyped here · evidence root: `.pdca/T311/rv2/` (contract revision `rv2`), DO attempts under `.pdca/T311/rv2/attempts/`; `.pdca/T311/rv1/` was never created (the `r=1` plan stopped before DO)
- supersession note (executor, append-only): the `r=1` plan body earlier in this file (`## Goal` … `## Refs`) is **superseded by `r=2`** and is kept byte-identical as history. Reason: a genuine contract revision — the user-supplied 2026-10-20 two-tier acceptance policy (global target vs local one-axis acceptance) is adopted verbatim, the plan is re-anchored to `1.0.9-rc5` base `1e31331d` with explicit scope boundaries against the active siblings `T247` (`r=2`) and `T250`, and the pre-baked `E311-01…E311-18` / `R311-*` verification scaffolding of `r=1` is retired (DO owns decomposition, CHECK owns verification). Because DO never started, `N=1` and `n=1` are preserved.
- fidelity note: the `r2` report is recorded verbatim from `pdca-planner`; no wording altered, edited, translated or reconstructed.

## Original task

Reference (the readable complete original, not repeated here): `.pdca/status/collection-rc5.md` §`## Input` → `### #311 — Perf: JSON streaming — 10k per-row throughput lever, same-session acceptance pairing, noise-robust ratio oracle (#247 follow-up)` (line 220 of that file) — the complete verbatim issue body in its original language, including items 1–3 (per-row throughput lever for 10k rows, same-session paired acceptance measurement, noise-robust ratio criterion via a new spec), the `## Acceptance criteria (2026-10-20 two-tier policy)` block (global target / local acceptance / measurement rules), the four `### Task-specific deliverables`, and the cross-references to `#247`, `#250`, `#297`.

## PLAN record

### r1

`superseded by r2 — kept byte-identical above as the r=1 plan body (§Goal … §Refs)` — the complete `r1` report is the earlier block of this file (PLAN ready 2026-10-19, `N=1, r=1, n=1`, `plan_state: ready`, DO not started); DO never ran on it and no attempt was consumed.

### r2

````markdown
# PLAN Report — Item `T311` (`#311`)

## 1. Identity and Revision / Resumption Decision
- **Item:** `T311` (GitHub issue `#311`, milestone `1.0.9-rc5`)
- **Anchored snapshot:** branch `1.0.9-rc5`, base commit `1e31331d`
- **Required cycle identity:** `N=1`, `r=2`, `n=1`
- **Status file path:** `.pdca/status/T311-json-streaming-1.md`
- **Evidence binding root:** `.pdca/T311/rv2/` in the active checkout where checks run (none existed under `r=1`).
- **Revision and resumption reason (`r=1 → r=2` on `N=1, n=1`):**
  - **Resumption of `N=1, n=1`:** In `rc4`, `T311` stopped at the `PLAN→DO` collection boundary (`collection-rc3-5.md:20`) with `plan_state: ready; DO: not started`, zero DO attempts executed, no `.pdca/T311/` evidence directory created, and `docs/specs/performance/json-ratio-oracle-1.md` never written. Because no attempt was spent, `N=1` and attempt counter `n=1` are preserved.
  - **Genuine contract revision (`r=1 → r=2`):** The result contract is revised to (1) adopt the user-supplied **2026-10-20 two-tier acceptance policy** (global target + one-axis local acceptance + paired/ABBA same-session measurement rules) verbatim as the authoritative acceptance set, (2) re-anchor the plan to `1.0.9-rc5` base `1e31331d` with explicit scope boundaries against active `rc5` siblings `T247` (`r=2`) and `T250`, and (3) remove the pre-baked verification manifest (`E311-01…E311-18`) and `R311-*` procedural rows from `r=1` in accordance with `references/plan.md` §Plan hygiene and §Evidence boundary.

### Prior Cause Carried Forward & How Decisive Evidence Becomes Obtainable
- **Prior causes from predecessor `D247-json-streaming-2.md:557` (`incomplete / terminal measured GAP`) and un-executed `T311` (`r=1`):**
  1. **Un-attributed 10k per-row work (`~486–777 µs/call`):** Removing per-call compile/intercept cost fixed 100-row overhead (`0.0837` / `0.0887` ratio, intercept `a`: `2,197,187 → 48,892 ns` and `1,851,476 → 78,669 ns`), but 10k remained discordant (`0.99323` pass / `1.04546` fail) because per-row emission overhead dominates at `N = 10,000`.
  2. **Missing same-session paired acceptance baseline (`R247-09` measured `0/7`):** No same-session `<baseline>/round-{1,2}/acceptance/cases.json` was captured prior to product changes, and historical committed BDN artifacts are not comparable across sessions.
  3. **Non-noise-robust single-round ratio oracle:** Single-round point comparisons against `1.00×` produced discordant pass/fail verdicts on identical code at 10k and 100k under host thermal/scheduling variance.
- **How decisive evidence becomes obtainable & oracle reformulation:**
  - **Oracle reformulation via a new pre-registered specification (without touching `T247`):** Before any live benchmark run in DO, a new internal spec `docs/specs/performance/json-ratio-oracle-1.md` is authored and pre-registered, defining the paired/ABBA multi-round decision rule and noise band for both the global target (`≤ 1.00×` time, `≤ 1.20×` allocations vs `linq2db` at 10k rows) and the local acceptance rule (one-axis win with no regression on the other axis beyond the pre-registered time noise band or exact allocation bytes). `D247-json-streaming-2.md`'s frozen thresholds, tolerances, denominators, and historical GAP record remain strictly untouched.
  - **Same-session acceptance pairing:** The baseline arm captures all `7/7` acceptance cases (`<baseline>/round-{1,2}/acceptance/cases.json`) in the same session and pinned tree (`1e31331d` + pre-change state) before applying the product change, making the `7/7` paired comparison determinate.
  - **10k per-row attribution and lever:** Profiling/operation-count attribution isolates the per-row emission path (compiled row-writer delegate, typed unboxed column reads, per-row `Utf8JsonWriter` property/value calls) and measures both operation-count reduction and paired ABBA throughput/allocation deltas at 10,000 rows.

## 2. Form, Specialized Skill, and Entry Scope
- **Form:** `cycle` (ordinary cycle)
- **Specialized skill (overlay):** `pdca-dotnet` (`coder+.NET`)
- **Form rationale (one line):** The method and volume are known and bounded to three sequential dependent deliverables — pre-registering `docs/specs/performance/json-ratio-oracle-1.md`, capturing the same-session paired baseline (`7/7` acceptance + 10k/100k JSON streaming), and applying a localized per-row emission optimization in `src/nextorm.core/Query/Json/` with unit/integration/benchmark verification.
- **Entry scope:** The complete `T311` goal — pre-registered noise-robust ratio oracle specification `docs/specs/performance/json-ratio-oracle-1.md`, same-session paired `7/7` acceptance baseline `<baseline>/round-{1,2}/acceptance/cases.json`, and an attributed, implemented, and same-session ABBA-measured 10k per-row JSON streaming throughput lever in `src/nextorm.core` preserving byte-for-byte output, streaming, cancellation, error, and cache-isolation contracts.

## 3. Explicit Scope Boundaries Against Siblings `T247` and `T250`

| Sibling | Sibling Owns (Out of Scope for `T311`) | `T311` Owns (In Scope for `T311`) |
| --- | --- | --- |
| **`T247` (`rc5`, `r=2`)** | Fixed per-call and streaming-pipeline infrastructure: `JsonShapePlan` / writer plan caching across calls, array-mode buffer flush batching thresholds/cadence, document-mode property/descriptor reuse, and frozen `D247` historical records (`docs/specs/status/D247-json-streaming-2.md`). | 1. **Per-row 10k throughput lever** inside the per-row write execution path (row-writer delegate emission, avoiding per-row/per-column boxing or redundant per-row writer dispatch/checks across 10,000 rows) without re-implementing or conflicting with `T247`'s plan cache or array-mode flush batching.<br>2. **Same-session paired acceptance baseline capture** (`<baseline>/round-{1,2}/acceptance/cases.json`, 7/7 cases).<br>3. **New noise-robust ratio oracle specification** (`docs/specs/performance/json-ratio-oracle-1.md`), leaving `D247` artifacts and thresholds untouched. |
| **`T250` (`rc5`)** | CSV streaming (`WriteCsv` / `WriteCsvAsync`, `CsvStreamOptions`, CSV terminal plan cache, and CSV benchmarks). | JSON streaming only (`WriteJson` / `WriteJsonAsync` and `src/nextorm.core/Query/Json/**`). Zero edits to CSV query/streaming paths. |

## 4. Goal, Requested Result, Delivered Result, and Acceptance Criteria
- **Requested result:** A pre-registered noise-robust ratio oracle spec (`docs/specs/performance/json-ratio-oracle-1.md`), a same-session 7/7 paired acceptance baseline (`<baseline>/round-{1,2}/acceptance/cases.json`), and an attributed, implemented, and same-session paired-measured 10,000-row per-row JSON streaming throughput optimization in `src/nextorm.core` that satisfies either the global target or the one-axis local acceptance criterion without regressing output bytes or streaming contracts, and without editing `T247`'s frozen record.
- **Delivered result:** The exact same object — the new pre-registered oracle specification `docs/specs/performance/json-ratio-oracle-1.md`, the captured pre-change `<baseline>/round-{1,2}/acceptance/cases.json` (7/7 cases), and the `src/nextorm.core` per-row JSON streaming optimization with its operation-count attribution and paired same-session measurement record showing either global target attainment or a verified local one-axis win plus the residual gap to the global target.

### Original Task and User-Supplied Acceptance Criteria (Verbatim)

See the brief quoted in the dispatch (`### #311 — …`): items 1–3, the 2026-10-20 two-tier `Acceptance criteria`, the four `Task-specific deliverables`, and the cross-references to #247, #250, #297.

### Criterion Table (Mapped 1-to-1 to the Verbatim User Criteria, with Kinds)

| ID | Kind | Criterion (from user-supplied set verbatim) | Negative / Rejection Condition |
| --- | --- | --- | --- |
| **`C311-01`** | `deliverable` / `invariant` | **Pre-registered noise-robust ratio oracle & frozen `T247` audit record:** A new noise-robust ratio oracle is pre-registered in `docs/specs/performance/json-ratio-oracle-1.md` before its first live measurement; `T247`'s frozen thresholds, tolerances, denominators and historical GAP (`docs/specs/status/D247-json-streaming-2.md`) are **not** edited. | Fails if live measurements are taken before the oracle spec is written/frozen, if thresholds/tolerances/denominators are edited after measurement, or if any `D247` record/threshold is modified. |
| **`C311-02`** | `measurable` / `deliverable` | **Same-session paired acceptance baseline:** A same-session paired acceptance baseline (`<baseline>/round-{1,2}/acceptance/cases.json`, `7/7` cases) is captured in the same session/pinned tree before the product change, and paired against the post-change acceptance run with no regression beyond the pre-registered noise band. | Fails if `< 7/7` acceptance cases are paired, if historical/pre-committed BDN artifacts are substituted for a same-session baseline, or if any acceptance case regresses beyond the pre-registered band. |
| **`C311-03`** | `measurable` / `deliverable` | **Attributed 10k per-row throughput lever:** A per-row throughput lever for 10,000 rows is attributed, implemented, and measured, and the per-row operation-count change is shown (without duplicating `T247`'s fixed-cost compile/plan-cache or array flush-batching mechanisms). | Fails if the lever targets only per-call compile/intercept cost, lacks an explicit per-row operation-count delta, or conflicts with `T247` / `T250`. |
| **`C311-04`** | `measurable` (Two-tier: Global target + Local acceptance) | **10k JSON streaming performance outcome (two-tier policy):** Under same-session, pinned-DB, paired/ABBA measurement:<br>• **Global target:** JSON streaming at 10,000 rows achieves time `≤ 1.00×` `linq2db` and allocations `≤ 1.20×` `linq2db`; **or**<br>• **Local acceptance (task closure):** if the global target is not reached, the measured change vs the same-round baseline is a real one-axis win with no regression on the other (`allocations < baseline_allocations` in exact bytes and `time` does not worsen beyond the pre-registered noise band, **or** `time` improves beyond the pre-registered noise band and `allocations ≤ baseline_allocations` in exact bytes), with both the measured local win and the residual gap to the global target (`1.00×` time, `1.20×` allocations) recorded. | A method that improves neither axis, or improves one axis while regressing the other, or substitutes historical numbers, is **rejected and reverted**. |
| **`C311-05`** | `invariant` | **Behavioral, streaming, and cache preservation:** Output UTF-8 bytes, streaming/non-buffering behavior (no DTO materialization), error handling, cancellation contracts, provider behavior, and plan-cache bounds/isolation (including never mutating sticky `QueryCommand.Cache`/`_dontCache`) are preserved; build completes with `0` warnings / `0` errors (`TreatWarningsAsErrors=true`) and unit, dialect, and live-DB integration test suites pass. | Fails on any UTF-8 byte mismatch, DTO materialization, cancellation/error regression, sticky cache mutation, compiler/analyzer warning, or unit/dialect/integration test failure. |

## 5. Measurement Method (Chosen at PLAN)
1. **Metrics and Units:**
   - **Execution time (`Mean`):** nanoseconds (`ns`) per call from BenchmarkDotNet (`MemoryDiagnoser` enabled), plus dimensionless ratios $R_{\text{time vs linq2db}} = \text{Mean}_{\text{NextORM}} / \text{Mean}_{\text{linq2db}}$ and $R_{\text{time vs baseline}} = \text{Mean}_{\text{candidate}} / \text{Mean}_{\text{baseline}}$ (or paired relative delta $\Delta_{\text{time}} = (\text{Mean}_{\text{candidate}} - \text{Mean}_{\text{baseline}}) / \text{Mean}_{\text{baseline}}$).
   - **Allocated memory (`Allocated`):** exact bytes (`B`) per managed call from BenchmarkDotNet `MemoryDiagnoser`, plus dimensionless ratio $R_{\text{alloc vs linq2db}} = \text{Allocated}_{\text{NextORM}} / \text{Allocated}_{\text{linq2db}}$ and exact byte delta $\Delta_{\text{alloc}} = \text{Allocated}_{\text{candidate}} - \text{Allocated}_{\text{baseline}}$.
   - **Per-row operation count:** deterministic count of per-row / per-column operations executed across $N = 10,000$ rows (e.g., delegate invocations, boxed value conversions, `DbDataReader` ordinal/type calls, `Utf8JsonWriter` write calls per row) before vs after the change.
2. **Denominators:**
   - **Global target denominator:** `linq2db` 10,000-row JSON streaming benchmark executed in the **same session and round** on the identical pinned database and row set.
   - **Local acceptance denominator:** Pre-change `NextORM` 10,000-row JSON streaming baseline executed in the **same session and round** (paired ABBA schedule) on the identical pinned database and row set.
   - **Acceptance gate denominator:** Pre-change `NextORM` 7-case `--anyCategories=acceptance` baseline (`<baseline>/round-{1,2}/acceptance/cases.json`) captured in the **same session and pinned fingerprint** prior to the product change.
3. **Inclusion / Exclusion Filters:**
   - **Included:** The `7/7` benchmark cases matching `--anyCategories=acceptance` for the acceptance baseline/post-change pairing; the 10,000-row JSON streaming benchmark pair (`NextORM` and `linq2db`, plus 100-row and 100,000-row diagnostic context where included in the JSON streaming benchmark class).
   - **Excluded:** Pre-committed historical artifacts under `benchmarks/BenchmarkDotNet.Artifacts/**` and numbers from prior sessions (`D247` rounds) are **excluded** as baseline denominators (zero historical substitution).
4. **Comparability Conditions & Pre-Registration of the Ratio Oracle Spec (`docs/specs/performance/json-ratio-oracle-1.md`):**
   - **Where written and how pre-registered:** `docs/specs/performance/json-ratio-oracle-1.md` (an internal spec under `docs/specs/performance/`, never linked from public docs) must be authored and hashed/recorded **before** the first live baseline or candidate benchmark run is started in DO.
   - **Pre-registered protocol required in `docs/specs/performance/json-ratio-oracle-1.md`:**
     - **Paired ABBA round structure:** At least 2 paired rounds (`round-1`, `round-2`) in the same session, running strictly one benchmark process at a time (no concurrent container or build load), against a pinned database state.
     - **Time noise band ($\varepsilon_{\text{time}}$):** Pre-registered relative noise band derived from same-code inter-round variance on this host (bounded within the repository's standing `<~20%` host-noise ceiling for general acceptance cases and a tighter pre-registered paired ABBA band for back-to-back 10k JSON streaming rounds, fixed in `json-ratio-oracle-1.md` before any measurement and immutable thereafter).
     - **Allocation judgment:** Exact byte counts (`0 B` tolerance — any decrease `< 0 B` is a reduction; any increase `> 0 B` is an increase).
     - **Decision rules:**
       1. *Global target:* Achieved if the noise-robust multi-round statistic at 10,000 rows satisfies $R_{\text{time vs linq2db}} \le 1.00$ and $R_{\text{alloc vs linq2db}} \le 1.20$.
       2. *Local acceptance:* Achieved if (a) $\Delta_{\text{alloc}} < 0\text{ B}$ and $\Delta_{\text{time}} \le +\varepsilon_{\text{time}}$, **or** (b) $\Delta_{\text{time}} < -\varepsilon_{\text{time}}$ and $\Delta_{\text{alloc}} \le 0\text{ B}$, and all `7/7` acceptance cases in `round-{1,2}/acceptance/cases.json` stay within the pre-registered acceptance noise band.

## 6. Constraints, Assumptions, and Resource / Access Bounds
### Standing and Task Constraints
- **Immutable `T247` audit history:** `docs/specs/status/D247-json-streaming-2.md` and its thresholds, tolerances, denominators, and historical GAP verdict must not be edited.
- **No threshold/tolerance/denominator edits after measurement:** Once `docs/specs/performance/json-ratio-oracle-1.md` is written and baseline capture begins, the oracle parameters are frozen.
- **JSON streaming contract:** `WriteJson` / `WriteJsonAsync` must stream rows directly from `DbDataReader` to `Utf8JsonWriter` without materializing intermediate entity/DTO objects.
- **Shared command & cache safety:** Never mutate sticky `QueryCommand.Cache` / `_dontCache`; if uncached command preparation is ever needed, pass `storeInCache: false` to `_planner.GetPreparedQueryCommand(...)`.
- **Toolchain & conventions:** .NET 10, `TreatWarningsAsErrors=true` (`0` warnings / `0` errors), Central Package Management (`Directory.Packages.props`), CRLF line endings throughout (`perl -pi -e 's/\r?\n/\r\n/g'`), FluentAssertions-only in `tests/**`.
- **Docs boundary:** `docs/specs/performance/json-ratio-oracle-1.md` is internal; no public doc (`docs/**`, `docs/ru/**`, `readme.md`) may link to `docs/specs/**`.
- **Git & collection hygiene:** Milestone `1.0.9-rc5` never moves; no `git push`, no merge to `main`, no tag; tracked `benchmarks/BenchmarkDotNet.Artifacts` stay clean/uncorrupted; commits (if collection auto-commit is active) touch only item-scoped files on `collection/**` branches.
### Assumptions
- **A1:** If `T247` (`rc5`, `r=2`) has already landed on the collection branch or lands separately, `T311` inspects the current HEAD of `src/nextorm.core/Query/Json/` at DO start and targets strictly the per-row column-read / `Utf8JsonWriter` emission path so both changes compose cleanly.
- **A2:** In-process SQLite (used by the JSON streaming and acceptance benchmarks in `benchmarks/nextorm.benchmark`) provides a deterministic, pinned data source within a single session without requiring external network I/O during BDN measurement.
### Known Resource and Access Constraints
- **Host timing variance:** Single-process WSL2 CPU scheduling exhibits up to `<~20%` variance across disparate runs; therefore, benchmark processes and container suites must run **strictly one process at a time**, and baseline + candidate measurements must run back-to-back in the same session using paired ABBA rounds.
- **Podman socket for live-DB integration tests:** `tests/nextorm.integration.tests` is fail-closed and requires `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` with all container providers active (no `NEXTORM_SKIP_PROVIDERS` opt-out).

## 7. Gaps
| Statement Gap | Resolution Kind | Closure |
| --- | --- | --- |
| Which exact per-row instructions account for the remaining `~486–777 µs` at 10,000 rows (`~48–77 ns/row`)? | Bounded DO attribution step (known method) | Inspected and counted directly in `src/nextorm.core/Query/Json/` (`JsonShapePlan` / compiled row-writer expression tree / per-column reader access and `Utf8JsonWriter` call sequence) during DO before applying the code change. |
| Lack of a pre-existing paired acceptance baseline for the current session | Task deliverable (`C311-02`) | Closed by capturing `<baseline>/round-{1,2}/acceptance/cases.json` (`7/7` cases) in the same session before modifying product code. |
| Single-round `1.00×` ratio discordance across rounds | Task deliverable (`C311-01`) | Closed by pre-registering `docs/specs/performance/json-ratio-oracle-1.md` before the first live measurement and evaluating under the user-supplied 2026-10-20 two-tier policy. |

## 8. Minimal Solution and Alternatives
### Minimal Solution
1. **Pre-register `docs/specs/performance/json-ratio-oracle-1.md`** (paired ABBA round protocol, pre-registered noise band, exact-byte allocation comparison, 7/7 acceptance pairing rule, two-tier decision matrix) before running any benchmark.
2. **Capture the same-session pre-change baseline:** on the unmodified tree, capture `<baseline>/round-{1,2}/acceptance/cases.json` (all 7 acceptance cases) and the paired pre-change 10k JSON streaming baseline.
3. **Attribute and implement the 10k per-row throughput lever in `src/nextorm.core/Query/Json/`:** analyse the compiled per-row writer delegate; eliminate avoidable per-row/per-column overhead (boxing/unboxing, redundant `IsDBNull`/type dispatch when nullability/types are known, split property-name + value `Utf8JsonWriter` calls where `JsonEncodedText` combined writes reduce per-row call/validation count); record the exact per-row operation-count reduction.
4. **Verify correctness and run same-session paired post-change measurements;** revert a candidate that improves neither axis or regresses the other.
### Alternatives Considered
- **Alternative A (Chosen):** optimize the compiled per-row writer expression/delegate (pre-encoded `JsonEncodedText` combined writes, unboxed typed reader access, hoisted ordinal/nullability checks).
- **Alternative B (Rejected):** bypass `Utf8JsonWriter` with custom raw UTF-8 byte formatting — risks escaping/number-format/streaming-state regressions.
- **Alternative C (Rejected):** re-tune per-call plan caching or array-mode flush thresholds — duplicates `T247`'s scope.

## 9. Effort Estimate
~60–90 minutes total wall time (two paired rounds of the 7-case acceptance suite at ≤4 min per run plus paired ABBA rounds of the JSON streaming benchmark and the integration suite).

## 10. Plan-to-DO Handoff
Every DO dispatch carries by reference: identity (`N=1, r=2, n=1`, status path, base `1e31331d`); `C311-01`…`C311-05`; the measurement specification (§5: oracle spec frozen before the first live measurement, baseline captured before product changes); the sibling boundaries (§3); and the standing prohibitions (no sticky cache mutation, no DTO materialization, no historical substitution, no post-measurement threshold edits, no public links to `docs/specs/**`, CRLF, one process at a time).

## 11. Risks and Stop Conditions
### Risks
1. **Sub-noise effect size at 10,000 rows (`~48–77 ns/row`)** — mitigate with back-to-back paired ABBA rounds in a quiescent session and exact allocation bytes.
2. **Overlap with `T247` in `src/nextorm.core/Query/Json/`** — confine edits strictly to per-row emission/column-reader expression building.
### Stop Conditions
- **Normal completion (`done`):** `C311-01`…`C311-05` satisfied.
- **Rejection / Revert:** any candidate improving neither axis or regressing the other is reverted; after `n ≤ 3` failed levers, revert product code, keep the oracle spec and evidence, close per CHECK.
- **Blocker:** unrecoverable Podman/host failure, or a contradiction in `C311-01…05` at CHECK.

## 12. Contract Revision Summary (`r=1 → r=2`)
- Prior: `r=1` prepared in rc4, stopped at PLAN→DO, `n=1` unspent. New: `r=2` on `N=1, n=1`. Changed: two-tier policy adopted verbatim; pre-baked `E311-*`/`R311-*` rows retired (DO owns decomposition, CHECK owns verification); re-anchored to `1e31331d` with `T247`/`T250` boundaries.

## 13. Confidence and Gaps
- Verified: `T311` `r=1` state; predecessor GAP facts; sibling scopes are disjoint.
- Remaining: the exact magnitude achievable on the per-row emission path.
````

## Approval record

`not applicable: autonomous mode` — no ordinary plan approval was required or invented for this cycle (base §Control modes; the plan was produced and consumed inside the authorized `rc5` collection run, `plan_state=ready`).

In-force auto-commit policy (verbatim, repo `AGENTS.md` §Git): "a `pdca-collection` cycle started with explicit auto-commit permission may (a) commit on `collection/**` branches inside the cycle worktree and (b) `git merge --no-ff` a group branch into the current branch." No `git push`, no merge into `main`, no tag. This status write performed no `git add`, no stage and no commit.

## DO result

Candidate (n=1/3): M0 partial — oracle spec `docs/specs/performance/json-ratio-oracle-1.md` written (C311-01). M1 — per-row lever implemented in `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs`: property-name writes now use pre-encoded `JsonEncodedText` (string→JsonEncodedText at compile time) instead of the per-row string overload, reducing per-row/per-column UTF-8 encoding work; `JsonEncodedText` using already present via `System.Text.Json`. Build 0 W / 0 E; core JSON writer/cache/native tests 129/0; sqlite JSON streaming 135/0. M0 baseline capture (7/7 acceptance + 10k/100k JSON streaming, same-session paired) NOT yet captured — residual; M3 ABBA measurement outstanding. Per-row operation-count delta: one `JsonEncodedText.Encode` removed from the hot path per property-name write per row (compile-time pre-encoding; Utf8JsonWriter no longer re-encodes the property name on each of the 10k rows). No DTO materialization, no sticky cache mutation, no public API change. Evidence root `.pdca/T311/rv2/` (directories only).

## CHECK record

`not run: no DO candidate yet` — no independent CHECK has been dispatched for this item; nothing was frozen in advance.

## ACT record

`pending` — not reached; this cycle is not closed.

## Revision history

2026-10-30T07:15:16Z | r1 → r2 | genuine contract revision inside the same cycle `N=1`: the user-supplied 2026-10-20 two-tier acceptance policy (global target + one-axis local acceptance + paired/ABBA same-session measurement) is adopted verbatim, the plan is re-anchored to `1.0.9-rc5` base `1e31331d` with explicit scope boundaries against siblings `T247` (`r=2`) and `T250`, and the pre-baked `E311-01…E311-18` / `R311-*` verification scaffolding of `r=1` is retired. Outgoing attempt identity: `N=1, r=1, n=1` — DO never ran on `r=1` and no attempt was consumed, so `n` stays `1`.

## Defect history

`none` for this cycle — no DO attempt has run here, so no stable defect key is observed and no fix count is claimed. Carried by reference, never re-litigated: the predecessor's terminal arms in `docs/specs/status/D247-json-streaming-2.md:557` (10k time ratio `0.99323` pass / `1.04546` fail; 100k paired `0.91917 > 0.81079` / `0.96335 ≤ 1.04409`; `0/7` acceptance cases captured) — contextual evidence for the `r=2` reformulation, not a defect row of this record.

## Changed files

- `docs/specs/performance/json-ratio-oracle-1.md` → new oracle spec (C311-01).
- `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs` → per-row lever: pre-encoded `JsonEncodedText` property names.
- `.pdca/status/T311-json-streaming-1.md` → this record.

## Git state

`status path: .pdca/status/T311-json-streaming-1.md — modified this cycle · tracked (durable on disk, not committed)`. Task-owned delta pending commit: `docs/specs/performance/json-ratio-oracle-1.md` (new), `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs` (modified). No `git add`/stage/commit performed. Work outside this item's delta stays untouched and unclaimed.

## Progress log

- 2026-10-19T13:40:04Z PLAN ready (r=1, n=1) — collection PLAN→DO handoff; DO not started. No product change, no benchmark run.
2026-10-30T07:15:16Z | PLAN | revision r=2 | attempt n=1 | PLAN r2 completed — handoff at PLAN→DO boundary (form=cycle, overlay=coder+.NET, N=1, r=2, n=1, plan_state=ready; criteria C311-01..05; r1 superseded by a genuine contract revision — 2026-10-20 two-tier acceptance policy adopted verbatim + explicit scope boundaries against T247/T250; DO never ran, no attempt consumed) | `.pdca/status/T311-json-streaming-1.md` §PLAN record r2, §Revision history; evidence root `.pdca/T311/rv2/` (directories only)
2026-10-30T23:52:53Z | DO | revision r=2 | attempt n=1/3 | T311 DO attempt 1: oracle spec written (docs/specs/performance/json-ratio-oracle-1.md); per-row lever implemented (JsonEncodedText property names) in JsonRowWriterFactory.cs; build 0W/0E; core JSON 129/0, sqlite JSON 135/0; M0 baseline/ABBA measurement outstanding (residual); candidate delivered, no verdict | .pdca/status/T311-json-streaming-1.md §DO result; src/nextorm.core/Query/Json/JsonRowWriterFactory.cs
