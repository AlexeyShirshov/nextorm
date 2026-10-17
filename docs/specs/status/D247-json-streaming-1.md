# D247 — T247 JSON streaming performance — cycle status
task: T247
issue: #247 — https://github.com/AlexeyShirshov/nextorm/issues/247
issue_title: Perf: JSON streaming is slower than linq2db at 10k rows
milestone: 1.0.9-rc3
state: OPEN
labels: enhancement
collection: 1.0.9-rc3-open-3
collection_status_path: docs/specs/status/collection-1.0.9-rc3-open-3.md
selected_variant: pdca-dotnet

# T247 / #247 — JSON streaming performance

## Current state
- `phase: P`; `plan_state: ready-for-DO-boundary`; `N=1`; `r=1`; `n=1`; evidence contract `rv=1`.
- `selected_variant: pdca-dotnet`; collection: `1.0.9-rc3-open-3`.
- `collection_status_path: docs/specs/status/collection-1.0.9-rc3-open-3.md`.
- Status file: `docs/specs/status/D247-json-streaming-1.md`.
- Repository baseline: branch `1.0.9-rc3`, commit `01815f3a`.
- **Re-anchor (2026-10-17):** base `b5ab6502` — benchmarks and tests were reorganized by topic; paths updated, decisions/criteria/counters unchanged. Repo test conventions apply to any test this task adds: FluentAssertions-only assertions (`Assert.Skip*` allowed) and integration tests must exercise a real DBMS (no `InMemoryDataContext` in `tests/nextorm.integration.tests`).
- **STOP at PLAN→DO.** Persist this plan; this own-PLAN step authorizes no implementation or measurements.

## Goal and immutable constraints
Make managed JSON streaming competitive at 1k/10k rows without sacrificing the 100k advantage.
Preserve public signatures, JSON bytes and validation, cancellation/error-prefix semantics, native eligibility/fallback, destination ownership, and ordinary-query plan caching.
Never mutate shared commands to disable caching; retain call-local `storeInCache:false`. Never invoke destination `Stream.Flush()` or dispose the destination.
Do not change the unverified baseline preparation recipe merely to improve these measurements.

## Acceptance criteria
All evidence below is **planned**, not already obtained.

| ID | Required result and reproducible oracle | Negative case |
|---|---|---|
| R247-01 | In each of two final full-job rounds: 1k DTO streaming mean / same-run linq2db mean ≤1.2; 10k ratio ≤1.0 and allocation ≤446,095 B/op, conservatively converting 435.64 KiB; 100k ratio ≤the corresponding pre-change same-session ratio. | Missing/mismatched rows, slower ratios, or allocation overflow fail; historical absolute timings cannot establish a pass. |
| R247-02 | Add and report NextORM `ToList_Dto + JsonSerializer` at all three row counts; validate byte equality outside timing. Capture pre-change measurements after benchmark-only instrumentation, before production edits. | Materialize-only `ToList_Dto`, different output, or a post-change “baseline” is insufficient. |
| R247-03 | Existing JSON tests remain green; matrix below closes managed/native, shape, option, surface and boundary variants with byte-identical output. | Different naming, order, null/default handling, framing, whitespace or newline fails. |
| R247-04 | Cancellation and faults retain existing partial-output policy, including exact completed-row prefix; destination remains open and destination Flush count stays zero. | Lost completed-row prefix, extra bytes, swallowed cancellation, disposal or destination Flush fails. |
| R247-05 | Repeated calls preserve command/plan-cache state; cached delegates are safely shared, keys distinguish writer-relevant shapes/options, and storage remains bounded. | Cross-shape output, shared mutable writer state, stale bindings, unbounded entries or disabled plan caching fails. |
| R247-06 | No public API change; in-memory calls remain fail-closed; SQL Server native path and managed fallback remain correct. | Public signature delta, in-memory acceptance, or changed native selection/output fails. |
| R247-07 | Build exits 0 with zero warnings/errors; full affected projects and real-provider integration boundary sweep pass with no missing provider evidence. | Filtered-only evidence or skipped required providers is not a pass. |
| R247-08 | CRLF preserved; new suppression/skip counters zero; changed-branch coverage reviewed; project coverage ≥85% lines / ≥75% branches. | Silent warning suppression, uncovered changed behavior, mixed EOLs or sub-threshold coverage fails this cycle. |
| R247-09 | JSON 1k/10k/100k enter the perf gate; existing allocation rows retain their semantics; seven-case acceptance set remains seven, ≤240 s, with no case >120% of its paired baseline mean in either round. | Missing params/jobs, altered legacy normalization, unbudgeted JSON timing, or acceptance regression fails. |
| R247-10 | Report, EN/RU comparisons, follow-up registry and findings agree; distinguish current measurements from historical issue numbers. | One-language update, unsupported performance claim, or public link to internal specs fails. |
| R247-11 | Complete rv1 manifest passes both evidence validators; CHECK respects its finite re-gather budget. | Missing evidence cannot be relabeled N/A or used alone to justify another DO iteration. |

**GAP handling:** historical 1k milliseconds and “0.95×” cannot be reproduced as absolute same-host criteria; they are replaced by R247-01 ratios, not carried as placeholders. No other criterion is waived. Missing decisive evidence blocks acceptance.

## Minimal solution: three answers
1. **What:** eliminate repeated expression compilation on the managed warm path using a bounded, context-owned compiled-writer cache.
2. **What must remain:** clone/prepare/shape validation and actual-reader binding checks still happen per invocation; no preparation shortcut or flushing change.
3. **Optimum within those constraints:** cache only immutable writer descriptors/delegates, not commands, readers, streams, `Utf8JsonWriter`, or caller options objects.

| Alternative | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Cache compiled writer after fresh shape construction | Removes verified per-call `Compile`; narrow behavioral footprint | Structural key construction and bounded-cache synchronization | **Choose first.** |
| Cache prepared descriptor and shape as well | Potentially removes more fixed work | Command invalidation, captured values, mutable naming policies, WIP recipe interaction | Deferred with milestone trigger below. |
| Batch array sink writes | Potential per-row I/O reduction | Exact-prefix cancellation and error semantics require redesign | Deferred; not needed to establish the first intervention. |

Cache design: context-owned maximum **64 entries**, synchronized lookup/publication/eviction, bounded LRU; invoke delegates outside the cache lock.
The immutable structural key covers ordered ordinals/names, declared/value/provider/enum types, kind, nullable/default behavior, direct-pass-through, recursive structure, relevant option values and converter identity/configuration.
Do not use `JsonShapePlan` or `JsonStreamOptions` reference identity as the whole key. Rebuild the shape first, preserving custom-policy evaluation; include resolved names. Unsupported/unrepresentable converter metadata **bypasses caching**, never approximates equality.
Freeze the descriptor captured by the delegate. Preserve per-reader validation. Native streaming bypasses this managed cache.

## Reconnaissance and decision gate
**Required spike D247-01:** compilation is verified, but its cost share and remaining preparation cost are unknown.
Reuse streaming and writer-only phase benches; add one **planned compile-only benchmark arm** using the setup-built shape. Report compile, steady writer and total streaming separately; residual is not claimed to be an independently measured preparation phase.
Observable result: exported compile-only distribution/allocation, existing writer-only distribution and total streaming at 1k/10k, on the pinned baseline, under the host lock.
This selects no speculative prepare-cache/batching work. If compilation is immaterial or cached lookup cannot produce a warm hit, return a new `P247:` decision task before production optimization.
A missing phase report is an evidence-recovery problem, not proof that another design is needed.

## Units and execution mode
**Sequential, one tree.** Production/cache contracts overlap; benchmark, gate and reporting outputs overlap sibling tasks. No worktree is required by this plan.
DO must update the task list when starting/completing each unit.

| Unit | Action; fix now/deferred | Footprint and local verification |
|---|---|---|
| D247-00 | **Fix now:** establish clean/dirt-aware baseline, URL/tool CLI facts, provider readiness, artifact inventory, shared-file leases and BDN lock protocol. Validate WIP recipe with existing mutation/cache regressions before interpreting perf. | Existing `QueryCommand.cs:96,197`, `QueryCommand.QueryPreparer.cs:134,245`, `QueryPlanner.cs:610`; inspection only. If baseline fails, retain this unit active and return `P247-BASELINE`. |
| D247-01 | **Fix now:** add NextORM materialize+serialize control and compile-only diagnostic; build evidence/gate helper; collect two pre-production rounds plus phase evidence. Freeze baseline fingerprints and formulas. | `SqliteBenchmarkWriteJson.cs:42,112-127,237`; `SqliteBenchmarkJsonStreamPhase2.cs:246,272-295`; planned `eng/perf/t247_json_gate.py`. |
| D247-02 | **Fix now:** implement bounded managed writer cache; preserve fresh preparation, shape build, reader validation and native path. | `DataContext.cs:385-429`; `JsonRowWriterFactory.cs:90,149,161,239-244`; planned `Query/Json/JsonRowWriterCache.cs`. Core/SQLite filtered tests below. |
| D247-03 | **Fix now:** add cache correctness and boundary regressions using assertion names below; retain cancellation semantics without batching. | `tests/nextorm.core.tests/Json/{JsonShapeWriterTests,JsonStreamWriterTests}.cs`; `tests/nextorm.sqlite.tests/Json/JsonStreamingTests.cs`; integration JSON partials only where existing cases do not cover the matrix. |
| D247-04 | **Fix now:** extend perf tooling with parameter-aware JSON rows and per-row logical-op override; add timing-ratio/control validation and parser tests. | `eng/perf/iteration14_gate.py:15-44,364-378,451-495`; `iteration14-budgets.json`; planned `eng/perf/tests/test_t247_json_gate.py`. |
| D247-05 | **Fix now:** two serialized final rounds, legacy/JSON gates, acceptance-set comparisons, report and EN/RU documentation. | Benchmark artifacts and docs listed below. Threshold failure returns `P247-PERF`; do not silently add another optimization. |
| D247-06 | **Fix now:** after all edits, one DO→CHECK boundary sweep; coverage, hygiene, evidence manifest and handoff. No further production edits without invalidating affected evidence. | All affected test projects; `artifacts/pdca/D247/rv1/**`; this status file. CHECK owns verdict. |

No original `D:` is superseded or completed by a blocker report.

## Variant matrix by execution path
“Test” includes existing tests plus explicit gaps filled in D247-03; no row is implicitly deferred.

| Execution path / variants | Closure |
|---|---|
| Managed Array DTO, 1k/10k/100k | **test:** same-round BDN, byte equality and allocation/time gates, each bucket separately. |
| 0 and 1 row; empty scalar/object/array/root | **test:** exact framing/bytes, no stale delegate state. |
| Array, NDJSON, root object; indented array/root | **test:** existing framing tests plus cache-hit repeats; NDJSON has trailing newline. |
| NDJSON+Root / NDJSON+WriteIndented | **guard + test:** existing rejection remains before output. |
| IgnoreNull on/off, naming policies, null/default options | **test:** alternate options on the same context; policies producing different names cannot reuse an incompatible writer. Null/default overload behavior remains unchanged. |
| DTO, wide DTO, scalar, anonymous and nested projection | **test:** existing shape suites; equal-column-count but structurally different cache keys. |
| QueryCommand and EntityBuilder; sync/async; default/options/params overloads | **test:** surface parity, including empty params and changing parameter values between calls. |
| Cancellation before start and mid-stream at 1k/10k/100k | **test:** sync/async deterministic cancellation after a completed first row; exact array/NDJSON prefix; pre-cancel emits no bytes. |
| Sink lifecycle, writer/destination faults, partial output | **test:** exact existing prefix policy, destination not disposed, Flush count zero; original exception/token retained. |
| Managed providers; SQL Server native and forced fallback | **test:** SQL-lowering suites plus real-provider JSON integration; no cache participation in native path. |
| In-memory query/builder sync/async | **guard + test:** existing fail-closed tests. |
| Enum numeric and enum-string projection | **test:** enum output and non-mutation suites; alternate enum/provider metadata. |
| Scalar `byte[]`, null, nullable/value/reference, DefaultOnNull | **test:** base64/null/default bytes and typed binding; null/default versus non-null cache-hit calls. |
| Repeat JSON→ordinary and ordinary→JSON on one context | **test:** existing plan-cache/non-mutation cases plus changing captured values and reset/reprepare. |
| Shared compiled writer in parallel | **test:** independent records/writers/sinks, simultaneous hit/miss publication; no claim that DataContext itself supports concurrent queries. |
| Key collisions, eviction and cache lifetime | **test:** same types/different names, ordinals, options, nested shapes and converter metadata; >64 shapes remain bounded and evicted shapes rebuild correctly. |
| 100k and >64 KiB output / very large cell | **test:** rollover and byte identity; no change to write/flush timing. |

### Planned assertion names, not claimed existing test symbols
- “Same context, changed shape/options/parameters produces the correct bytes and keeps ordinary caching enabled.”
- “Concurrent users of one cached delegate never share a record, sink or JSON writer.”
- “Every writer-relevant key difference prevents an incompatible hit; more than 64 shapes stays bounded.”
- “Reader-binding mismatch is rejected on both cache miss and cache hit.”
- “Cancellation after row one preserves the exact prefix at 1k, 10k and 100k.”
- “Zero/one row and null/default/base64/enum nested shapes match uncached bytes.”
- Add these to existing test classes; discover actual symbols with Roslyn, never invent filtered future symbols.

## Test strategy and priority
Unit: core cache/key/writer lifecycle tests; SQLite tests: actual managed query preparation, surfaces, state, output and cancellation.
Integration: real-provider conversions, SQL Server native/fallback and nested JSON. SQL-generation projects remain database-independent.
Per-unit filters:
- Core: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~JsonStreamWriterTests|FullyQualifiedName~JsonShapeWriterTests|FullyQualifiedName~JsonNativeStreamTests|FullyQualifiedName~InMemoryTests"`.
- SQLite: `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~JsonStreamingTests"`.
- SQL lowering: each affected provider project with `--filter "FullyQualifiedName~JsonStreamingSqlLoweringTests|FullyQualifiedName~SqlServerNativeJsonSqlTests"`.
- Focused integration: integration project with `--filter "FullyQualifiedName~Json"`; the boundary run is unfiltered.
- `integration_projects: [tests/nextorm.integration.tests]`; `integration_boundary_runs: 1` successful, unfiltered, container-backed run after all edits.
- Boundary full projects: core, sqlite, sqlserver, postgres, mysql, clickhouse, integration. Helper runs each `dotnet test tests/<project> -c Debug`, exits nonzero on any failure/missing provider.
- Load `.opencode/skills/running-integration-tests/SKILL.md`; use `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`. Start Podman/retry per that skill; skipped required providers are a blocker.
- Coverage: helper mirrors checked-in CI collector/reportgenerator settings; retain raw coverage and summary. Enforce 85/75 and inspect changed branches for key equality, hit/miss/eviction/bypass, binding and error paths.
- **Mutation: no usable Stryker tooling/configuration is established by the fact pack.** Do not claim a mutation score or install packages implicitly. Perform isolated fault-seeding checks for an omitted key component, disabled bound and skipped binding validation; each must fail a targeted test, then restore and rerun. Record this limited substitute explicitly.

| Priority | Contract rows; reason |
|---|---|
| P1 | All R247-01..11 and E247-01..19: explicit acceptance, invariants or changed execution-path obligations. CHECK cannot downgrade them. |
| P2 | Diagnostic phase attribution and observations beyond the required report; cannot substitute for P1 measurements. |

## Perf protocol and gate design
**Measurement required:** per-call compilation at `JsonRowWriterFactory.cs:149,244` and per-row writes at `JsonStreamWriter.cs:52-84` affect the measured path.
Use a host-wide advisory lock `/tmp/opencode/nextorm-bdn.lock`; **every collection task and gate driver must acquire it**. No uncoordinated BDN process may coexist. Lock the outer driver only; nested launches inherit its ownership rather than deadlock by relocking.
Baseline/final use the same host, runtime, CPU/power conditions, benchmark source shape, database fixture and full config. Record source/tool/environment fingerprints.
Exact driver interfaces below are **planned deliverables**, not existing commands:
- `python3 eng/perf/t247_json_gate.py baseline --rounds 2 --output artifacts/pdca/D247/rv1/baseline`
- `python3 eng/perf/t247_json_gate.py final --rounds 2 --baseline artifacts/pdca/D247/rv1/baseline --output artifacts/pdca/D247/rv1/final`
- Both launch, under `flock -x /tmp/opencode/nextorm-bdn.lock`, `env NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkWriteJson*' --exporters json --artifacts <round-directory>`.
- Phase commands use `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkJsonStreamPhase2*' --exporters json --artifacts <phase-directory>` and the analogous `'*JsonStreamPhase2WriterBenchmark*'`; ShortRun is diagnostic only.
- Acceptance command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --exporters json --artifacts <round-directory>`; driver verifies exactly seven cases and measures invocation wall time.
Full config is the existing `NEXTORM_BENCH_FULL=1` `Job.Default`/`InProcessEmitToolchain`; do not treat phase ShortRun as decisive.
Report each row-count bucket separately: means, errors, allocations, ratios, baseline ratios and control; retain all exports, not only markdown.
The ~20% ShortRun band is **not** a tolerance added to R247-01. Both full rounds must meet literal thresholds. Discordant/uncertain results are inconclusive, not averaged into a pass; CHECK may use its bounded re-gather, then escalate if confidence remains low.
Extend allocation gate matching with explicit `RowCount` and per-row `logical_ops_per_invocation=1`; legacy omitted overrides remain 100. Do **not** wrap existing JSON benchmarks in 100-iteration loops.
Three JSON allocation rows: 10k budget 446,095 B/op; 1k/100k budgets equal their frozen pre-change measured bytes, taking the worse baseline job. Timing gate checks R247-01 for all three buckets. Require both allocation toolchain jobs and reject missing/ambiguous parameters.
Driver invokes `python3 eng/perf/iteration14_gate.py --manifest eng/perf/iteration14-budgets.json --reports-dir <gate-directory> --bench-src-dir benchmarks/nextorm.benchmark --db sqlite --timeout 1800`; preserve legacy filters/jobs, add the JSON filter.
Write BDN output into dedicated directories. Inventory pre-existing tracked/untracked artifacts first; restore only run-generated tracked changes and remove only owned untracked reports. Never broad-reset sibling/user work.

## Footprint and predecessor requirements
Production: `src/nextorm.core/DataContext/DataContext.cs`, `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs`, planned `src/nextorm.core/Query/Json/JsonRowWriterCache.cs`.
Tests: core/SQLite files above; conditional integration additions in `tests/nextorm.integration.tests/CommonTestSuite/Json/CommonTestSuite.JsonStream.cs` or `.JsonStreamNested.cs` if existing coverage cannot close a matrix row.
Bench/gate: `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWriteJson.cs`, `SqliteBenchmarkJsonStreamPhase2.cs`, `eng/perf/{iteration14_gate.py,iteration14-budgets.json,t247_json_gate.py}`, `eng/perf/tests/test_t247_json_gate.py`.
Docs: this status; planned `docs/specs/performance/T247-json-streaming-report.md`; `docs/comparisons/feature-benchmarks-2026-10-17.md`; RU mirror; `docs/specs/performance/{feature-benchmark-followups-2026-10-17.md,performance-findings.md}`.
Other collection tasks also need: benchmark directory, `eng/perf/**`, comparison EN/RU files and performance registries; T246 also needs `DataContext.cs`. These shared paths require an exclusive edit/measurement lease.
No semantic predecessor on T243–T246. Before starting an overlapping edit, its owner must finish/checkpoint and provide the exact diff/base plus retained contracts. T246 must preserve JSON seams; no common preparation-cache abstraction is required.
Gate ownership ordering: prior owner publishes manifest/schema/parser changes before D247-04; T247 preserves all legacy rows and supplies compatibility tests. No simultaneous shared-file edits.
Baseline WIP recipe needs validation **inside D247-00**, not an assumed upstream certification. It is already present at HEAD; do not revert it or attribute its gains to T247.

## Docs, assumptions, risks and deferred work
Docs: add reproducible report/control and all row counts; update EN+RU feature comparison, #247 follow-up and a new findings entry. Do not regenerate unrelated README tables. Public pages never link internal specs.
XML-doc: no public API change, so no XML-doc change is mandated; update internal cache ownership/invalidation comments. `AGENTS.md` changes only if a reusable stable rule is demonstrated.
Assumptions/prerequisites: .NET 10/tools and provider setup can run; all BDN users adopt the lock; options remain init-only but custom policies may be mutable; final footprint is re-gathered with Roslyn. Validator CLI spelling below must be checked in D247-00; incompatibility requires an explicit contract correction before implementation, not a fabricated successful invocation.
Risks: structural-key omissions, retained converter/options state, cache growth and lock contention; native divergence; baseline WIP invalidation errors; gate normalization changes; host noise; T246/shared-file overlap. Mitigations are bounded immutable keys, bypass guards, unchanged preparation/native/flush paths, negative tests and serialized measurements.
**Deferred with milestone-local triggers:** shape/prepared-descriptor caching → R247-01 fails after writer reuse and phases identify fixed preparation cost; batching → same failure plus evidence of sink-call dominance; getter/transcoding specialization → measured steady-writer dominance. Each triggers a new `P247:` task before closing `1.0.9-rc3`, not silent scope growth or next-milestone deferral.
DO→PLAN classification: baseline/setup failure is an additive prerequisite, preserving original D active/blocked; missing reports require targeted scout/re-gather first; inaccessible external resources → recommend `escalate`; persistent low confidence → `escalate`, trigger 5. No such blocker is currently proven.
Confidence: high that compilation repeats and semantic hazards are real; medium that bounded writer reuse alone meets every ratio. Performance success is deliberately unclaimed.

## Versioned evidence contract — rv=1
All sources/artifacts in this table are **planned**. Owner streams: `F` facts/scout, `C` coder, `K` check. Every row has `rv=1`; IDs remain stable.
Let `A=artifacts/pdca/D247/rv1`; helper invocations below define the required interface of planned `eng/perf/t247_json_gate.py`.
Each helper must save underlying commands, exit codes, stdout/stderr, source fingerprints and machine-readable results; exit 0 only when its stated checks pass. Raw test/BDN evidence must accompany summaries.

| Row / requirement | Required check; kinds and sources | Exact invocation; required result | Expected artifact; owner; applicability; rv |
|---|---|---|---|
| E247-01 / R247-02,05 | Baseline facts, WIP regression results, CLI help, repository URL, artifact/lease inventory | `python3 eng/perf/t247_json_gate.py preflight --output artifacts/pdca/D247/rv1/preflight` →0, no unresolved prerequisites | `$A/preflight/**`; F+C; always; 1 |
| E247-02 / R247-01,02 | Two pre-production BDN rounds, raw exports, fingerprints, frozen budgets | Baseline driver command above →0, all 33 control-inclusive cases represented per round | `$A/baseline/**`; C; always; 1 |
| E247-03 / R247-02 | Phase distributions and compile-only evidence; benchmark source diff | `python3 eng/perf/t247_json_gate.py phases --output artifacts/pdca/D247/rv1/phases` →0, no unsupported attribution | `$A/phases/**`; C; always; 1 |
| E247-04 / R247-03,04,05 | Core targeted tests, assertion-to-matrix mapping, binding/parallel/bound tests | Core filtered command above →0; TRX/output copied by helper | `$A/tests/core-focused/**`; C; always; 1 |
| E247-05 / R247-03,04,05 | SQLite targeted tests, all overloads/buckets/exact prefixes | SQLite filtered command above →0; matrix rows identified by actual discovered symbols | `$A/tests/sqlite-focused/**`; C; always; 1 |
| E247-06 / R247-03,06 | Provider SQL-lowering/native eligibility results | `python3 eng/perf/t247_json_gate.py lowering --output artifacts/pdca/D247/rv1/lowering` →0; executes listed filtered provider commands | `$A/lowering/**`; C; always; 1 |
| E247-07 / R247-07 | Build log | `dotnet build nextorm.slnx -c Debug` →0, 0 warnings/0 errors; capture full log | `$A/build.log`; C; always; 1 |
| E247-08 / R247-03..07 | Full affected non-integration suites, totals and raw logs | `python3 eng/perf/t247_json_gate.py boundary --scope nonintegration --output artifacts/pdca/D247/rv1/boundary` →0, all six projects pass | `$A/boundary/**`; C; always; 1 |
| E247-09 / R247-03,04,06,07 | Unfiltered real-provider boundary suite; provider/container logs | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug` →0; no required provider skipped | `$A/integration/**`; C; always; 1 |
| E247-10 / R247-08 | Coverage raw data, CI-equivalent summary, changed-branch map | `python3 eng/perf/t247_json_gate.py coverage --output artifacts/pdca/D247/rv1/coverage` →0, ≥85/75; collect during the single boundary sweep, not an extra integration sweep | `$A/coverage/**`; C; always; 1 |
| E247-11 / R247-05,08 | Fault-seeding diffs/results/restoration proof; explicit no-Stryker statement | `python3 eng/perf/t247_json_gate.py fault-checks --output artifacts/pdca/D247/rv1/fault-checks` →0 only if all three faults killed and restored tests pass | `$A/fault-checks/**`; C; always; 1 |
| E247-12 / R247-01,02 | Two final full BDN rounds and per-bucket oracle calculations | Final driver command above →0 only when every literal R247-01 comparison passes | `$A/final/**`; C; always; 1 |
| E247-13 / R247-09 | Parser regression tests: legacy100/JSON1, params, missing jobs/results, unit conversion | `python3 -m unittest discover -s eng/perf/tests -p 'test_t247_json_gate.py'` →0, all positive/negative cases pass | `$A/gate-tests.log`; C; always; 1 |
| E247-14 / R247-01,09 | Legacy allocation gate plus three-bucket JSON timing/allocation gate | `python3 eng/perf/t247_json_gate.py gate --baseline artifacts/pdca/D247/rv1/baseline --final artifacts/pdca/D247/rv1/final --output artifacts/pdca/D247/rv1/gate` →0, underlying iteration14 invocation above exits0 | `$A/gate/**`; C; always; 1 |
| E247-15 / R247-09 | Two paired acceptance runs, seven-case count and elapsed time | `python3 eng/perf/t247_json_gate.py acceptance --baseline artifacts/pdca/D247/rv1/baseline --output artifacts/pdca/D247/rv1/acceptance` →0, seven cases, ≤240s and ratio limits | `$A/acceptance/**`; C; always; 1 |
| E247-16 / R247-06,08 | Public API comparison, CRLF byte audit, diff/suppression/skip counters, final footprint | `python3 eng/perf/t247_json_gate.py audit --base 01815f3a --output artifacts/pdca/D247/rv1/audit` →0; public delta/new suppressions/new skips=0 | `$A/audit/**`; F+C; always; 1 |
| E247-17 / R247-10 | EN/RU/registry/report consistency and link audit | `python3 eng/perf/t247_json_gate.py docs --output artifacts/pdca/D247/rv1/docs` →0; both languages present, claims trace to exports, no public specs links | `$A/docs/**`; C; always; 1 |
| E247-18 / R247-11 | Manifest structure and complete required-row inventory | `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D247/rv1/manifest.json` →0, all required rows accounted for | `$A/manifest-validation.log`; K; always; 1 |
| E247-19 / R247-11 | Evidence source/applicability lint | `python3 scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/D247/rv1/manifest.json` →0, no unresolved errors | `$A/evidence-lint.log`; K; always; 1 |

Manifest: `artifacts/pdca/D247/rv1/manifest.json`; top-level `task`, `tree`, `status_file`, `contract_rv:1`, `required_rows:E247-01..19`, `rows` with stable IDs, status, evidence/reason/predicate.
All current predicates are unconditional; project/build/full-suite/CRLF/no-suppression/perf/EN+RU rows **cannot be N/A**. Narrow cache bypasses require positive guard evidence, not row removal.
CHECK re-gather budget: **2 total across DO/CHECK**, owner **check**; maintain counter and exact requests in this status. No third re-gather; unresolved evidence → blocker/escalation, not fabricated pass.
No supersession exists at rv1. A justified future revision must explicitly supersede rv1, retain IDs/obligations and add IDs for new scenarios. Actual plan changes increment `r` and reset `n=1`; clarifications/rejected candidates do neither.

## Progress log
- PLAN r1/n1: fact pack reviewed; minimal bounded-writer-cache intervention selected; required spike and evidence contract specified.
- Pending: coder persists plan; collection records PLAN→DO stop. No implementation or measurement has run under this plan.

## Lane disposition (2026-10-17)

Not executed — lane G1 stopped: predecessor T246 reached a terminal `incomplete` (measurement-protocol GAP).
Task status: `incomplete` («группа остановлена»). Plan retained; nothing executed, nothing superseded.
