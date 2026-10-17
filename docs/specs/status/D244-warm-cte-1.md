# D244 — Warm CTE / recursive CTE allocation parity (#244)

## Collection handoff

- `plan_state`: `ready`
- `selected_variant`: `pdca-dotnet`
- `cycle`: `N=1`
- `plan revision`: `r=1`
- `contract revision`: `rv=1`
- `attempt`: `n=1`
- `re-gather budget`: `2` (owner CHECK)
- `execution mode`: sequential, one group tree
- `measurement lock`: `/tmp/opencode/nextorm-bdn.host.lock`
- `status_file`: `docs/specs/status/D244-warm-cte-1.md`
- `evidence dir`: `artifacts/pdca/D244/rv1/`
- `base_ref`: resolved at DO start from the collection group branch tip
- `collection_baseline_ref`: `957fcfe7`
- **Re-anchor (2026-10-17):** base `b5ab6502` — benchmarks and tests were reorganized by topic; paths updated, decisions/criteria/counters unchanged. Repo test conventions apply to any test this task adds: FluentAssertions-only assertions (`Assert.Skip*` allowed) and integration tests must exercise a real DBMS (no `InMemoryDataContext` in `tests/nextorm.integration.tests`).

## 1. Goal and acceptance policy
Reduce the remaining allocations of **fresh-command, cached-warm CTE and Recursive CTE queries**, preserving behavior and existing performance guarantees.

### Non-negotiable constraints
- Do not edit the frozen D201-3 raw-shape capture/verify/apply recipe, closure walk or comparators belonging to that recipe (`docs/specs/status/D240-construction-allocation-1.md:129,151,322`).
- Do not change existing #240 thresholds or its evaluator (alloc <= 1.20x, time <= 1.05, `D240...:27,33-34`).
- No benchmark-specific product branches, public API changes, cache-policy changes or mutation of shared cached commands.
- Preserve owner WIP `957fcfe7` (`HasEverPrepared`, `ShapeValuesStable`, thread-static unkeyed-scan sets).
- `Cte_Warm_Reused` and `RecursiveCte_Warm_Reused` remain exactly `0 B` (`scripts/perf/iteration14-budgets.json:13-14`, `scripts/perf/iteration14_gate.py:397-414`).
- Prepared `A_*` controls do not regress.
- Assess all five #240 Category-B scenarios individually: Cte, RecursiveCte, Except, Intersect, Distinct.
- No CTE-family averages and no allocation averaging across toolchains, rounds or scenarios.

### Final target versus checkpoint
1. **FINAL:** both allocation ratios <= 1.00; every other criterion passes.
2. **CHECKPOINT/PARTIAL:** both <= 1.20 but at least one > 1.00; all checkpoint obligations pass.
Outcome 2 may close this cycle as a checkpoint but **must not mark T244/#244 finally done**: final requirements stay `open` with measured residuals and a current-milestone follow-up. Native gate output must distinguish checkpoint from final. If either ratio remains above 1.20 the checkpoint is NOT met — do not relabel a failed optimization as acceptance.

## 2. Minimal solution and alternatives
Essential goal: remove allocations actually executed on the measured warm path. Hard boundary: frozen recipe unchanged. Optimum inside it: attribution-selected, small construction/container and stateless-comparer changes; no speculative rewrite of cold CTE preparation.

| Alternative | Pros | Cost / risk | Decision |
|---|---|---|---|
| Optimize hoisting and its two HashSets immediately | Matches the issue's original hypothesis | These do not execute on a raw-recipe hit (`QueryPreparer.Prepare` is not entered; `PrepareCtes` is called only from `Prepare`) — likely irrelevant to these rows | Rejected as initial implementation |
| Attribute first; optimize warm construction and eligible plan-key overhead | Targets observed work; small reversible changes | May reach only the checkpoint if frozen recipe dominates | SELECTED |
| Redesign raw-shape caching / graph pooling | Potentially larger savings | Violates frozen boundary; ownership/reentrancy risk | Excluded from this cycle |

Measured-path fact that drives this decision: the two arms build a **fresh fluent command with constants on every call** (`SqliteBenchmarkFeaturesFairCached.cs:12-21,493-497,526-528`), the raw-shape recipe gate (`QueryPlanner.cs:609-618`) hits, `ApplyPreparationFrom`/`CopyTo` copies preparation (`QueryCommand.Clone.cs:18-169`; CTE list by reference `:140`), so `QueryPreparer.Prepare` (`QueryPlanner.cs:656`) and with it `CteHoister.Hoist`, `EnsureNoUnhoistedCtes` (`CteHoister.cs:78-79`), `CtesPlanHash` (`QueryPreparer.cs:444-461`) and `ValidateRecursiveShape` (`:396-406`) do NOT run on the measured arm. Parameter refresh does not run either (`QueryPlanner.cs:755-769,796,860`; `ParamRefreshRecipe.cs:284-285`). Per-iteration candidates that DO run: `DataContextExtensions.cs:1572-1573,1696-1697`; `Builders/CteQuery.cs:110-118,140-146`; `EntityBuilder.cs:343-377,2802-2828`; `Builders/CteMerge.cs:17-37`; `QueryCommand.TResult.cs:1058-1069`; `QueryCommand.cs:319-356,1164-1170`; `Cte.cs:155-214` (`ValidateRecursiveOwners`); `QueryPreparer.cs:3889,3929-3947,3951-4021,4026,4180-4207,4243,4276-4347`; `QueryCommand.Plan.cs:30-44,72-87`.

## 3. Measurement and acceptance oracle
### 3.1 Definitions
- FairCached bytes/query = `Memory.BytesAllocatedPerOperation / 10` (`scripts/perf/update_benchmark_tables.py:51-52`, `SqliteBenchmarkFeaturesFairCached.cs:29`).
- WarmDecompose bytes/operation = exported bytes / 100 (`SqliteBenchmarkWarmDecompose.cs:26`, `iteration14-budgets.json:5`, `iteration14_gate.py:30,397`).
- Reject missing, duplicate, `NA`, unexpected OPI or unverified logical-operation counts.
- Allocation ratio = Nextorm bytes/query divided by the **linq2db arm in the same class, job and round** (`:502-508`, `:534-538`; same process/data, `Linq2DbDataContext.cs:60-67,186-206`). Same-round oracle — no stored baseline.
- Report both jobs individually; acceptance uses the worst applicable job, not their mean.
- Verify actual loop counts for `A_*` during attribution and record them explicitly; do not apply `/100` or `/10` indiscriminately.

### 3.2 Commands
Planned new tool (not existing): `scripts/perf/t244_evidence.py`, unittest-compatible, retaining raw BDN data and exposing the underlying invocations verbatim in its command log.
```
A=artifacts/pdca/D244/rv1
flock -x /tmp/opencode/nextorm-bdn.host.lock \
  python3 scripts/perf/t244_evidence.py measure --repo . --phase baseline --round 1 \
  --output artifacts/pdca/D244/rv1/baseline/round-01
```
Candidate rounds: `--phase candidate`. Per round, run exactly:
```
env -u NEXTORM_BENCH_FULL dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --job short --exporters json
env -u NEXTORM_BENCH_FULL dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter '*SqliteBenchmarkFeaturesFair.A_*' --job short --exporters json
env -u NEXTORM_BENCH_FULL dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter '*SqliteBenchmarkWarmDecompose*' --job short --exporters json
```
Inventory the configured artifact directory (`benchmarks/BenchmarkDotNet.Artifacts`, `BenchmarkArtifacts.cs:34`) before/after each invocation and copy only that invocation's outputs into its round dir; never read stale JSON.
```
python3 scripts/perf/t244_evidence.py evaluate \
  --baseline artifacts/pdca/D244/rv1/baseline --candidate artifacts/pdca/D244/rv1/candidate \
  --output artifacts/pdca/D244/rv1/evaluation.json
python3 scripts/perf/t244_evidence.py attribute \
  --round artifacts/pdca/D244/rv1/baseline/round-01 \
  --output artifacts/pdca/D244/rv1/attribution.json
```
Evaluator exit: 0 = requested tier fully passed, 2 = unmet numeric requirement, 1 = malformed/missing evidence. Exit status alone is never the evidence.

### 3.3 Timing and repeats
- Two valid baseline rounds before product edits; two valid candidate rounds after.
- Warm elapsed-time acceptance: each CTE row's candidate time within **+20%** of its pre-T244 group baseline, job-matched (faster is fine). Keep reporting frozen #240 same-round time tolerance <= 1.05x linq2db.
- Prepared controls: allocation and elapsed time <= 1.03x baseline.
- If the first two paired timing comparisons disagree around a boundary, take one additional pair and use the **median of three per-row job-matched timing ratios** (timing noise only).
- Allocation gates must hold in **every valid candidate round**; a later round cannot erase an allocation failure. Maximum three valid pairs; persistent timing ambiguity = `inconclusive`, not pass.
- Invalid isolation/schema rounds are retained as invalid evidence and replaced at most once per intended round; persistent contamination blocks performance acceptance.

### 3.4 Criteria
| ID | Criterion and reproducible oracle | Negative case |
|---|---|---|
| R244-01 | Final CTE: M + EV, same-round Cte allocation ratio <= 1.00 in every valid candidate round/job | any ratio > 1.00 leaves it open |
| R244-02 | Final RecursiveCte: same, recursive row | same |
| R244-03 | Checkpoint: each row independently <= 1.20; M + EV | one row above 1.20 fails checkpoint |
| R244-04 | Warm timing per §3.3; frozen #240 time assessment preserved | persistent boundary failure / missing matched baseline |
| R244-05 | Both Warm_Reused rows exactly 0 B, both jobs; M + native gate | any nonzero / missing / NA |
| R244-06 | Every existing Nextorm prepared `A_*` control: alloc and time <= 1.03x baseline | any control regression or omitted control |
| R244-07 | Five Category-B scenarios: candidate allocation not above pre-T244 same-host baseline; timing within +20%; #240 reporting preserved | regression hidden by another scenario, average or old stored baseline |
| R244-08 | Both FairCached CTE rows participate in the native gate with /10, same-round references and explicit final/checkpoint targets | wrong divisor, missing reference/job, silently lowered target |
| R244-09 | Separate construction, preparation, hashing, raw-hit and execution attribution per CTE row | only a family total or cold-path speculation |
| R244-10 | Variant matrix closed with executable tests/guards and source mapping | untested applicable variant |
| R244-11 | Tables, narrative and gate documentation match retained raw data and acceptance tier | final-success wording for a checkpoint |
| R244-12 | Frozen recipe, public API, shared-command cache policy and WIP preserved | changed frozen block, sticky cache flag, benchmark-specific branch |
| R244-13 | Both evidence-validation gates exit 0 | lint/schema failure, missing required row |
| R244-14 | Changed-type branch-delta and mutation obligations satisfied; full boundary tests pass | surviving relevant mutant, uncovered changed branch, skipped provider boundary |
Existing Except/Intersect residual failures vs #240's absolute target remain visible; R244-07 does not require T244 to fix them and does not authorize worsening them.

## 4. Attribution spike — required before a product fix
Existing phase measurements do not isolate the raw-recipe hit. A **measurement-only spike is required**.
Extend `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWarmDecompose.cs` (existing anchors `:107-167,262-276`) with diagnostic phases covering separately: (1) construct a fresh CTE/recursive graph; (2) capture raw shape and fingerprint; (3) raw lookup and preparation replay; (4) plan-key/comparer creation after replay; (5) end-to-end fresh cached-warm execution; (6) existing prepared/reused execution.
Requirements:
- Internal diagnostic calls may use benchmark-only once-bound delegates; no per-operation reflection in measured regions.
- No diagnostic branches or counters in product code.
- Validate the isolated raw-hit probe against the unmodified end-to-end arm.
- Record verified raw hits/misses; use the observed hit plus the existing `!IsPrepared` guard to establish whether full preparation is entered.
- Report B/query and percentage of: total warm allocation, excess above same-round linq2db, construction/recipe/plan-key/reader-execution components.
- Inclusive phases must be labelled; subtract only correctly nested phases, never double-count a graph.
- Unattributed residual must be <= max(128 B/query, 10% of measured warm bytes); otherwise use the bounded targeted re-gather before choosing a fix.

### Frozen selection rule
Per row, `gap = max(0, NextormB - linq2dbB)`. A non-frozen component is eligible when its demonstrated removable allocation is >= 64 B/query or >= 10% of gap and its correctness proof holds.
1. Rank eligible components by removable B/query, separately for both rows.
2. Execute D244-02 and/or D244-03 in that order.
3. After each, rerun affected attribution and correctness tests.
4. Stop when the final target is met, or when both rows meet the checkpoint and no further eligible bounded candidate remains.
5. If only frozen-recipe work dominates, or candidates cannot reach the checkpoint: retain evidence and return to PLAN. Do not optimize cold HashSets merely because the issue names their lines.
6. If a supposed warm probe misses, fix the probe/setup first; if the **real** benchmark misses, document miss frequency and contribution — a cold-path implementation then needs a revised plan.

## 5. D-units
| Unit | Files / anchors | Concrete work and applicability | Tests and boundary |
|---|---|---|---|
| D244-01 — attribution and baseline | `SqliteBenchmarkWarmDecompose.cs:107-167,262-276`; planned `scripts/perf/t244_evidence.py`, `scripts/perf/tests/test_t244_evidence.py` | Implement measurement-only phases and evaluator; snapshot pre-T244 state; collect baseline; prove the raw-hit path; publish ranking and candidate eligibility | Python evaluator negative tests; Release benchmark build; two baseline rounds. Boundary: validate phase accounting before any product edit |
| D244-02 — construction containers | `Builders/CteQuery.cs:110-146`; `Builders/CteMerge.cs:17-37` | If eligible: remove redundant transient list/container materialization using exact-sized internal storage and existing empty/identity cases. Preserve declaration order, duplicate/conflict handling and independent command state. No pooling, no shared mutable storage, no dropped validation, no public return-type change | Core CTE/merge/ownership tests + provider generation projects. Boundary: construction/join/full-prepare matrix cases; rerun phase measurements |
| D244-03 — non-frozen plan-key overhead | `Query/QueryCommand.Plan.cs:30-44,72-87`; `Query/QueryPlanEqualityComparer.cs` (exact path confirmed by Roslyn before edit) | If eligible and comparer state is proven immutable/stateless: reuse comparers via type-correct immutable holders instead of instantiating per fresh command. Preserve equality/hash semantics, generic/runtime-type separation and concurrency. Do not change frozen raw-shape comparators, `CaptureRawShape`, fingerprint semantics or preparation replay | Core equality/cache/generic/concurrency tests; all five Category-B rows. Boundary: all unit/dialect tests + provider integration + phase remeasurement |
| D244-04 — native gate, final measurements, docs, package | `scripts/perf/iteration14_gate.py:86-90,155-190,344-465`; `scripts/perf/iteration14-budgets.json`; planned evaluator tests; docs §9 | Add the two FairCached rows with explicit reference arms, /10, dual-job validation, final/checkpoint targets; preserve existing rows and /100. Capture candidate rounds, classify the outcome, sync docs, generate the evidence package | Parser/evaluator self-tests; complete native gate; all test and evidence boundaries |
D244-02/D244-03 skip rule: record `not applicable` only with attribution plus proof that the guard is false — a skipped candidate is not a "done fix". A materially different implementation needs a revised `P:`.

### Test-scope records (frozen; validated by `scripts/validate_inner_loop.py brief` before any edit)
```json
[
  {"unit":"D244-01","projects":["benchmarks/nextorm.benchmark"],
   "selectors":["*SqliteBenchmarkWarmDecompose*","*SqliteBenchmarkFeaturesFairCached.B_*","*SqliteBenchmarkFeaturesFair.A_*"],
   "files":["benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWarmDecompose.cs","scripts/perf/t244_evidence.py","scripts/perf/tests/test_t244_evidence.py"],
   "rebuild":"affected","boundary":"attribution-before-product-edit",
   "rationale":"prove the measured execution path and the allocation ranking","integration_projects":[],"integration_boundary_runs":0},
  {"unit":"D244-02","projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.sqlserver.tests","tests/nextorm.postgres.tests","tests/nextorm.mysql.tests","tests/nextorm.mariadb.tests","tests/nextorm.clickhouse.tests","tests/nextorm.clickhouse.extensions.tests"],
   "selectors":["FullyQualifiedName~CteQueryTests","FullyQualifiedName~Iteration14CteLookupTests","FullyQualifiedName~TypedCteTests","FullyQualifiedName~CachedPreparationRecipeTests","FullyQualifiedName~DataModifyingCteRejectionTests","FullyQualifiedName~DirectCteJoinTests","FullyQualifiedName~TypedCteSqlGenerationTests"],
   "files":["src/nextorm.core/Builders/CteQuery.cs","src/nextorm.core/Builders/CteMerge.cs"],
   "rebuild":"affected","boundary":"after-construction-unit: dotnet test nextorm.slnx -c Debug",
   "rationale":"container ownership, merge semantics and dialect generation","integration_projects":["tests/nextorm.integration.tests"],"integration_boundary_runs":1},
  {"unit":"D244-03","projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.sqlserver.tests","tests/nextorm.postgres.tests","tests/nextorm.mysql.tests","tests/nextorm.mariadb.tests","tests/nextorm.clickhouse.tests"],
   "selectors":["FullyQualifiedName~CachedPreparationRecipeTests","FullyQualifiedName~CteQueryTests","FullyQualifiedName~TypedCteTests"],
   "files":["src/nextorm.core/Query/QueryCommand.Plan.cs","src/nextorm.core/Query/QueryPlanEqualityComparer.cs"],
   "rebuild":"affected","boundary":"after-plan-key-unit: dotnet test nextorm.slnx -c Debug",
   "rationale":"hash/equality isolation, concurrency and non-CTE cache safety","integration_projects":["tests/nextorm.integration.tests"],"integration_boundary_runs":1},
  {"unit":"D244-04","projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.sqlserver.tests","tests/nextorm.postgres.tests","tests/nextorm.mysql.tests","tests/nextorm.mariadb.tests","tests/nextorm.clickhouse.tests","tests/nextorm.clickhouse.extensions.tests","tests/nextorm.entityframeworkcore.tests","tests/nextorm.alias.tests","tests/nextorm.publicextensibility.tests","tests/nextorm.integration.tests"],
   "selectors":["FullyQualifiedName~T244CteAllocationTests"],
   "files":["scripts/perf/iteration14_gate.py","scripts/perf/iteration14-budgets.json","scripts/perf/tests/test_t244_evidence.py","tests/nextorm.core.tests/T244CteAllocationTests.cs"],
   "rebuild":"affected","boundary":"final-group-build-and-evidence: dotnet test nextorm.slnx -c Debug",
   "rationale":"complete semantic, provider, gate and evidence sweep","integration_projects":["tests/nextorm.integration.tests"],"integration_boundary_runs":1}
]
```
Note: `D244-04` has no in-DO inner loop (it is the boundary unit itself); its scope is dispatched only at the boundary. Its `projects`/`selectors` name the real test projects and one narrow selector (the T244-owned new test); the complete sweep is the single `dotnet test nextorm.slnx -c Debug` run stated in `boundary`, never an inner selector. Verified narrow test symbols: `CteQueryTests` (`tests/nextorm.core.tests/Cte/CteQueryTests.cs:5`), `Iteration14CteLookupTests` (`Iteration14CteLookupTests.cs:18`), `TypedCteTests` (`TypedCteTests.cs:21`; sqlite `TypedCteTests.cs:72`), `CachedPreparationRecipeTests` (`CachedPreparationRecipeTests.cs:36`, incl. `Recipe_RecursiveCte_` :571), `DataModifyingCteRejectionTests` (`tests/nextorm.sqlserver.tests/Cte/DataModifyingCteRejectionTests.cs:11`; sqlite :11), `DirectCteJoinTests` (`:13`), `TypedCteSqlGenerationTests` (`:11`). New core test source planned as `tests/nextorm.core.tests/T244CteAllocationTests.cs` (name confirmed by Roslyn/scout before creation).

The full verified test-project set of `nextorm.slnx` is `nextorm.core.tests`, `nextorm.sqlite.tests`, `nextorm.sqlserver.tests`, `nextorm.postgres.tests`, `nextorm.mysql.tests`, `nextorm.mariadb.tests`, `nextorm.clickhouse.tests`, `nextorm.clickhouse.extensions.tests`, `nextorm.entityframeworkcore.tests`, `nextorm.alias.tests`, `nextorm.publicextensibility.tests`, `nextorm.integration.tests` (12; `nextorm.alias.poc` is an exe, not a test project). The Python helper test file `scripts/perf/tests/test_t244_evidence.py` follows the repo convention: `unittest`, shebang, module docstring carrying `python3 -m unittest discover -s scripts/perf/tests -p 'test_t244_evidence.py' -v`, and the `sys.path.insert` + `import` pattern of `scripts/perf/tests/test_t240_evidence.py:1-27`.

### Exact invocations
```
dotnet test tests/nextorm.core.tests -c Debug --filter 'FullyQualifiedName~CteQueryTests'
python3 -m unittest discover -s scripts/perf/tests -p 'test_t244_evidence.py' -v
dotnet build nextorm.slnx -c Debug
dotnet build nextorm.slnx -c Release
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test nextorm.slnx -c Debug
```
All require exit 0, nonzero relevant test counts and no applicable provider skips. Load `.opencode/skills/running-integration-tests/SKILL.md` first; start Podman per that skill if the socket is missing. A D244-02 boundary may serve D244-03 only when both edits are present and both units name that same completed boundary; otherwise rerun.

## 6. Execution-path variant matrix
| Axis / path | Required cases | Closure |
|---|---|---|
| CTE family | Plain CTE and legacy RecursiveCte separately | Test: results, declarations, allocation/time rows |
| Cached warm / literal-only | Fresh graph, raw recipe hit, no capture/parameters | Test + measured guard: hit established; no full preparation inferred from hit/`IsPrepared` path |
| Full preparation | First use / raw miss, then hit; identical results and SQL | Test: no bypassed preparation or validation |
| Prepared controls | Every existing Nextorm `A_*`; both reused CTE rows | Test + measurement: <=3% controls, reused exactly 0 B |
| Cache policy | Default, `Cache=false`, `_dontCache`; subsequent cacheable/shared-command calls | Test: no sticky state leak, no accidental caching |
| Declaration graphs | One, multiple, nested, duplicate/conflicting names; declaration order | Test: valid graph preserved, invalid graph rejected |
| Unhoisted rejection | Deliberately invalid/unhoisted CTE | Test: `EnsureNoUnhoistedCtes` still rejects |
| Parameters | Bound values, changed values across warm calls, null/default | Test: correct refresh, no stale value |
| InValues | Empty/nonempty, changed size/content, value/reference items, null where supported | Test/guard: supported forms refresh; unsupported form keeps its explicit rejection |
| Captures | Captured variable changed between executions vs literal-only shape | Test: frozen recipe behaviour retained |
| Joins | Existing JoinInto path, ordinary join, `CteMerge`; empty/one/multiple sides | Test: result and declaration ownership/order |
| Recursive API | Legacy `WithRecursive`; typed `CreateRecursive`/`RecursiveReference` | Test: valid recursion and owner/shape rejection |
| Recursive flags | `maxRecursion` null/default/explicit; typed result with value/reference members | Test: same SQL/validation/provider semantics |
| Non-SQLite | SQL Server/PostgreSQL/MySQL/MariaDB/ClickHouse generation plus real provider execution | Test: generation and integration boundaries |
| No CTE | Empty/no-CTE commands, ordinary queries, all other #240 scenarios | Test + native gate: no overhead/semantic regression |
| Comparer lifetime | Different command/result types, repeated contexts, concurrent commands | Test if D244-03 applies: no shared mutable state |
| Cold-only optimization | Hoister allocation reduction, typed owner HashSet reduction | Deferred (trigger §13); correctness still tested now |
| Provider performance | CTE performance on non-SQLite databases | Deferred (trigger §13) |
No matrix row is closed merely by an unchanged file.

### Coverage, red/green, mutation
- Repo thresholds line 85% / branch 75% apply warning-level on this branch, not main's hard gate; do not change CI policy.
- Changed production types require a branch-delta report: every changed branch covered or explicitly proven unreachable/not applicable.
- Coverage: `DOCKER_HOST=... dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D244/rv1/tests/coverage.cobertura.xml`.
- Performance red/green: baseline shows the unmet target, candidate the improvement.
- Semantic regression tests need not fail on correct pre-optimization code; prove sensitivity with temporary mutants (drop/reorder a declaration; alias mutable state; bypass rejection; reuse an invalid comparer across types; leak cache policy). Each mutant: retained patch/hash -> exact scoped `dotnet test` command -> expected nonzero assertion failure -> restore -> green command. Mutants never enter the delivered patch. A surviving applicable mutant blocks R244-14.

## 7. Native gate changes
Extend, do not replace, `iteration14_gate.py` and its manifest schema:
- Existing rows keep budgets, job requirements and normalization (/100).
- New FairCached Cte and RecursiveCte rows declare: Nextorm arm; same-round linq2db reference arm; logical operations 10; `final_ratio = 1.00`; `checkpoint_ratio = 1.20`.
- Missing/mismatched references, jobs, OPI or duplicate rows fail closed.
- Planned CLI `--tier final|checkpoint`, default final; checkpoint output names the unresolved final requirements.
```
flock -x /tmp/opencode/nextorm-bdn.host.lock env -u NEXTORM_BENCH_FULL python3 scripts/perf/iteration14_gate.py --tier checkpoint
flock -x /tmp/opencode/nextorm-bdn.host.lock env -u NEXTORM_BENCH_FULL python3 scripts/perf/iteration14_gate.py --tier final
```
Checkpoint gate must return 0 for checkpoint acceptance; final gate returns 0 for FINAL and at checkpoint fails only the unresolved final targets. Any other unmet existing budget stays a failure. Parser tests must cover /10 vs /100, missing comparator, duplicate rows, missing toolchain, NA, non-1 OPI, nonzero reused values, one failing CTE row and the checkpoint/final distinction.

## 8. Performance isolation protocol
Measurement is mandatory: this changes per-query warm work, not one-time initialization.
Host-wide protocol, binding on T243–T247:
- Lock: `/tmp/opencode/nextorm-bdn.host.lock`, exclusive `flock`.
- Each measurement/gate process: acquires the lock; captures `ps -eo pid,ppid,args` before launch; refuses to start if an unrelated `nextorm.benchmark`/BDN workload runs; logs fixed environment, SDK/runtime, host identity, source state and command; uses `NEXTORM_BENCH_FULL` unset, `--job short`, existing InProcessEmit configuration; requires both `Default` and `InProcessEmitToolchain` records where the existing config/gate requires them; captures process state afterwards; releases the lock only after copying/logging that invocation's artifacts.
- The collection orchestrator reserves the host measurement window; a noncooperating benchmark process invalidates the window even if it never took the lock.
- Measurements run against the **group worktree's own Release build**, not owner binaries or a sibling worktree. Record source-state hash, benchmark executable hash and database hash. No product edits during a round.
- Baseline is the group state immediately before T244 product edits, including already integrated predecessor work; retain its dirty patch if applicable. `957fcfe7` is the collection baseline reference, **not** a substitute for a same-host pre-T244 measurement.
- Invalidate a round for wrong source/build/database, mixed artifacts, overlap, wrong environment, missing job/reference or malformed output. Do not invalidate a merely unfavorable valid result.

## 9. Documentation
| Path | Change |
|---|---|
| `docs/comparisons/feature-benchmarks-2026-10-17.md:76-77,141-148` | Update CTE/RecursiveCte values and follow-up status; preserve unrelated scenario data |
| `docs/comparisons/benchmarks.md` | Update affected generated tables only if produced from the measured source; retain generation evidence |
| `docs/specs/performance/acceptance-benchmarks.md` | Add native rows, divisors, final/checkpoint semantics, zero-byte obligations |
| `docs/specs/performance/benchmark-report.md` | Per-path attribution, baseline/candidate data, noise/isolation, residual narrative |
| `docs/specs/status/D244-warm-cte-1.md` | Plan, decisions, counters, contract, results, remaining work |
Not touched: frozen D240 status/recipe, historical iteration reports, unrelated performance specs, root readme, API references, public guide structure. `docs/ru/**` is NOT touched for this perf-only change; if implementation unexpectedly needs a public API/page change, stop and revise the plan including the mirror requirement. Public pages must never link to internal specs.

## 10. Footprint and collection coordination
| Area | Exact path / bounded uncertainty | Sharing |
|---|---|---|
| Product | `src/nextorm.core/Builders/CteQuery.cs` | potential shared core footprint |
| Product | `src/nextorm.core/Builders/CteMerge.cs` | potential T245 overlap |
| Product | `src/nextorm.core/Query/QueryCommand.Plan.cs` | potential T243/T245 overlap |
| Product | `src/nextorm.core/Query/QueryPlanEqualityComparer.cs` (Roslyn-confirmed path substitutes if different) | shared cache contract |
| Benchmark | `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWarmDecompose.cs` | **shared T243/T245, potentially T246/T247** |
| Harness | `scripts/perf/t244_evidence.py` (new) | T244-owned |
| Harness tests | `scripts/perf/tests/test_t244_evidence.py` (new) | T244-owned |
| Native gate | `scripts/perf/iteration14_gate.py` | **shared T243–T247** |
| Budgets | `scripts/perf/iteration14-budgets.json` | **shared T243–T247** |
| Core tests | `tests/nextorm.core.tests/T244CteAllocationTests.cs` (planned) | T244-owned additions |
| Integration tests | existing `tests/nextorm.integration.tests/CommonTestSuite.*.cs` partial selected by Roslyn, else one new T244 partial | shared suite contract |
| Dialect tests | existing CTE test sources (verified above) or new `T244CteTests.cs` per actually present provider test project | bounded to listed test projects |
| Docs | four prose files in §9 | **comparison tables and performance docs shared T243–T247** |
| Status | `docs/specs/status/D244-warm-cte-1.md` | T244-owned |
| Evidence | `artifacts/pdca/D244/rv1/**` | T244-owned |
| External lock | `/tmp/opencode/nextorm-bdn.host.lock` | host coordination, not repository output |
Forbidden edits: `CteHoister.cs`, `Cte.cs`, frozen regions of `QueryCommand.QueryPreparer.cs`, frozen recipe comparators, `QueryPlanner.cs`, owner WIP logic, public API, unrelated budgets.
Execution mode: sequential, one group tree, no extra task worktrees. Shared gate/schema/docs edits must be serialized and additive; sibling row IDs and thresholds stay untouched. Baselines must be recollected if another task changes relevant product or benchmark behaviour before T244's baseline is frozen. No commits/merges/pushes are authorized by this plan.

## 11. Priority matrix
| Priority | Rows / behaviour |
|---|---|
| P1 by construction | R244-01/02 final targets; R244-03 checkpoint; R244-05 zero-byte; R244-06 controls; R244-07 Category-B; R244-08 gate integrity; R244-12 frozen/API/WIP invariants |
| P1 execution-path correctness | changed ownership, merge, comparer/equality/hash branches; invalid CTE rejection; parameter refresh; full-preparation vs raw-hit separation; integration boundaries |
| P1 evidence integrity | R244-09 attribution, R244-13 evidence gates; missing/invalid data can never become a pass |
| P2 | documentation formatting after numeric/source consistency |
R244-04 timing and R244-10/14 test obligations remain acceptance conditions regardless of priority.

## 12. Preconditions, assumptions, gaps
No sibling optimization or gate rows are a mandatory dependency. Local prerequisites: group worktree exists and its starting source state is recorded; .NET 10 build with warnings-as-errors; `benchmarks/nextorm.benchmark/data/test.db` present and usable (record resolved path/hash; if absent, set it up before baseline capture); host lock protocol adopted; frozen regions and WIP boundaries mapped from history; baseline commands and diagnostic phases pass schema/accounting checks. Missing local build/tool/database setup is an in-cycle prerequisite, not an external blocker.
| Gap | Closure |
|---|---|
| Exact existing test symbols/files beyond those verified | Roslyn scout in D244-01; planned test sources explicitly labelled |
| Comparer statefulness | proven before D244-03, else candidate inapplicable |
| Mutability/ownership of declaration storage | proven before D244-02, else no sharing shortcut |
| Exact `A_*` logical-operation counts | inspect benchmark source and record a map before parsing |
| Old issue numbers vs current WIP | fresh group baseline wins; old values are historical context |
| Benchmark artifact-directory override behaviour | use the configured location and invocation inventory |
| Performance feasibility under the frozen recipe | explicit risk; checkpoint/final decided only by measurements |

## 13. Deferred work with triggers (all stay in milestone 1.0.9-rc3)
| Deferred item | Trigger |
|---|---|
| Hoister walker / two-HashSet optimization | real FairCached raw misses or full preparation contribute an eligible measured amount; requires a revised PLAN |
| Typed recursive owner-scan allocations | typed-API profile shows an eligible recurring cost |
| Frozen raw-recipe redesign | attribution proves it is the remaining limiting component and explicit authorization unfreezes that scope |
| Final <=1.00 after an accepted checkpoint | automatically activated by any final ratio > 1.00; keep measured bytes-to-target and ranking in #244 |
| Non-SQLite performance tuning | provider-specific profile shows a remaining bottleneck after the SQLite assessment |
| Other #240 scenario target failures | owned by existing follow-ups, particularly T245; T244 still forbids regression |

## 14. Versioned evidence contract — rv=1
Storage: human contract here; machine contract `artifacts/pdca/D244/rv1/contract.json`; map `artifacts/pdca/D244/rv1/execution-map.json`. Machine contract fields: `task: "T244"`, `issue: 244`, `status_file`, repo `path`, actual `base_ref` (resolved at DO start), `collection_baseline_ref: "957fcfe7"`, non-empty `footprint`, `rows`, `required_rows`, `contract_rv: 1`, `supersedes: []`, and each row's stable id, requirement ids, scenario, command, expected result/log, evidence kinds/sources, artifacts, owner, predicate, rv.
All sources below are planned until produced. `required_rows` includes every row, including final-target rows; at checkpoint they stay `open` with measured reasons and are not removed to make lint green.
Abbreviations: B/C = `artifacts/pdca/D244/rv1/baseline|/candidate/`; EV = evaluate command §3.2; Tests = §5; Owners: measurement, implementation, gate-docs, CHECK.
| Row / rv | Requirement; scenario | Required kinds and planned sources | Exact command / result and log requirement | Artifacts | Owner; applicability |
|---|---|---|---|---|---|
| E244-01 / 1 | R244-09; ranking + raw-hit proof | BDN JSON, probe counts, Roslyn map, derivation | AT rc 0; per-row B/query, eligible ranking, residual bound | `attribution.json/.md`, `execution-map.json` | measurement; always |
| E244-02 / 1 | R244-01; final CTE ratio | B/C raw JSON, EV table | M + EV, final CTE <= 1.00, else explicitly open | `evaluation.json`, `ratios.csv` | measurement; always |
| E244-03 / 1 | R244-02; final recursive ratio | same, recursive row | M + EV, recursive <= 1.00, else open | same, separate row | measurement; always |
| E244-04 / 1 | R244-03; checkpoint | B/C raw JSON, individual ratios | M + EV; checkpoint rc 0 only if both <= 1.20 in every valid round/job | `checkpoint.json` | measurement; always |
| E244-05 / 1 | R244-04; timing | job-matched B/C means, repeat log | M + EV; §3.3 band and repeat policy; frozen time report retained | `timing.csv`, `round-validity.json` | measurement; always |
| E244-06 / 1 | R244-05; reused zero | WarmDecompose JSON, native output | M + gate §7; both rows/jobs exactly zero | `warm-reused.csv`, gate logs | measurement; always |
| E244-07 / 1 | R244-06; prepared controls | complete `A_*` inventory, B/C JSON, logical-op map | M + EV; every applicable control <= 1.03x | `prepared-controls.csv`, `logical-ops.json` | measurement; always |
| E244-08 / 1 | R244-07; five Category-B rows | same-round JSON, baseline deltas, unchanged #240 report | EV plus `python3 scripts/perf/t240_evidence.py --round <recorded round dir> --alloc-tol 1.20 --time-tol 1.05 --output <recorded path>`; exact expanded calls + rc, including known residual failures | `category-b.csv`, per-round `t240-report.*`, commands log | measurement; always |
| E244-09 / 1 | R244-09; phase table | existing + added WarmDecompose results | M + AT; distinct construct/no-hash/hash/plan-only/raw-hit/reused rows for both families | `phases.csv`, `phases.md` | measurement; always |
| E244-10 / 1 | R244-08; native budgets | budget diff, parser tests, fresh gate output | both §7 gate commands; checkpoint rc 0; final rc 0 or explicitly unresolved final-only failure | `gate/checkpoint.log`, `gate/final.log`, `gate/results.json` | gate-docs; always |
| E244-11 / 1 | R244-10; matrix closure | source/test mapping, test logs | Tests; rc 0, nonzero counts, no applicable skips; every matrix row mapped | `tests/variant-matrix.json`, `tests/results/`, `tests/commands.log` | implementation; always |
| E244-12 / 1 | R244-14; branch/mutation/coverage | coverage XML, changed-branch map, mutant patches + red/green logs | §5/§6 commands; green rc 0; mutant failures attributable to assertions | `tests/coverage.cobertura.xml`, `tests/branch-delta.json`, `tests/mutations/` | implementation; predicate: production type changed, else explicit no-change guard |
| E244-13 / 1 | R244-11; docs sync | prose diff, source-to-table mapping | `python3 scripts/perf/t244_evidence.py docs-check --repo . --evaluation artifacts/pdca/D244/rv1/evaluation.json` rc 0; no final wording at checkpoint | `docs-check.json`, `docs-mapping.json` | gate-docs; always |
| E244-14 / 1 | R244-12; invariants/WIP/API | baseline-to-final diff, Roslyn impact map, frozen-region hashes | `python3 scripts/perf/t244_evidence.py invariants --repo . --execution-map artifacts/pdca/D244/rv1/execution-map.json --output artifacts/pdca/D244/rv1/invariants.json` rc 0; independent CHECK reviews the diff | `invariants.json`, `source-diff.patch`, `roslyn-impact.json` | implementation + CHECK; always |
| E244-15 / 1 | R244-08/13; fail-closed tooling | Python tests, malformed fixtures | `python3 -m unittest discover -s scripts/perf/tests -p 'test_t244_evidence.py' -v` rc 0, nonzero cases | `tests/perf-tools.log` | gate-docs; always |
| E244-16 / 1 | R244-13; package validity | generated manifest + validator logs | commands below; both mandatory gates rc 0 | `package/manifest.json`, `validation/*.log` | CHECK; always |
| E244-17 / 1 | R244-04/09; isolation/provenance | process snapshots, lock log, env/source/build/DB hashes | M and locked gate calls; no external overlap, correct group build | `provenance.json`, `processes/`, `commands.log` | measurement; always |
| E244-18 / 1 | R244-12/14; provider boundary | build/test logs, provider identities | Debug/Release builds and integration commands §5; rc 0, actual applicable provider execution | `tests/build-*.log`, `tests/integration.log` | implementation; always |
Mandatory packaging gates (all rc 0):
```
python3 scripts/pdca_evidence.py generate --repo . --contract artifacts/pdca/D244/rv1/contract.json --output artifacts/pdca/D244/rv1/package
python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D244/rv1/package/manifest.json
python3 scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/D244/rv1/package/manifest.json
```
Verify the generated manifest path from the tool output and retain it in the execution map. Structural lint success never turns an `open` numeric requirement into a pass.
### CHECK re-gather budget
Owner CHECK. Budget: two targeted re-gather dispatches total. Each names missing row ids, the exact missing artifact/query and the expected observable result. First: recover or independently confirm the missing/ambiguous evidence. Second: resolve a remaining contradiction. An absent report alone does not justify product DO or a contract revision. Persistent low confidence after the bounded scout goes to `escalate` (trigger 5). A justified contract revision must preserve ids/obligations, explicitly supersede rv1 and add ids for genuinely new variants.

## 15. Routing and risks
Current classification: initial PLAN with an attribution prerequisite. If DO returns: missing local setup -> new active prerequisite, original D stays active and blocked (not superseded); candidate proof fails -> reject that candidate, continue the unchanged unit; frozen boundary prevents the checkpoint -> new `P:` task from the measured residual/ranking; true external constraint -> recommend `escalate`; insufficient evidence -> targeted scout first, persistent low confidence -> escalate trigger 5.
Principal risks: frozen-recipe allocations may dominate; container savings may be too small; comparer reuse may not be state-free; noisy prepared-control timing; shared gate/doc edits; host contention. Confidence: high on the measured-path distinction and the safeguards; deliberately conditional on removable-byte attribution and ownership/comparer proofs. No future test result, allocation value or source location is assumed.

## Progress log
- 2026-10-17T00:00:00Z | PLAN | revision r=1 | iteration n=1/3 | PLAN ready — collection handoff (PLAN→DO boundary owned by pdca-collection); no DO started, no code edited, no benchmark run. | docs/specs/status/D244-warm-cte-1.md

## Lane disposition (2026-10-17)

Not executed — lane G1 stopped: predecessor T246 reached a terminal `incomplete` (measurement-protocol GAP).
Task status: `incomplete` («группа остановлена»). Plan retained; nothing executed, nothing superseded.

## Lane disposition — G2 stop (2026-10-17)

status: incomplete — outside-cycle blocker
Counters unchanged: N=1, r=1, rv=1, n=1/3.
No product edit was made.
Valid baseline rounds: 0/2.
Round-01 is invalid and is **not** acceptance evidence (benchmark assembly rebuilt 15 s into invocation 1: `b7514a17→e1d04479`; HEAD moved; tree 228→903→1042).
Own harness defect found and left unfixed: two empty-target `[GlobalSetup]` methods in `SqliteBenchmarkWarmDecompose` (`Verify`, `VerifyDiagnosticProbes`) → BDN refuses the class; the attribution arm never ran.
Escalate decision: **STOP** the lane with the resume conditions below.
Resume conditions:
1. the foreign campaign is finished, committed or parked and the tree is stable;
2. an exclusive host window is guaranteed;
3. a pre-T244 base commit is fixed;
4. paths are re-anchored to `Sqlite/…`;
5. the double `[GlobalSetup]` is fixed;
6. two consecutive valid baseline rounds are obtained.
Artifacts preserved at `/tmp/opencode/t244-preserve/` (harness, harness tests, `rv1/` evidence). Issue #244 stays open.
