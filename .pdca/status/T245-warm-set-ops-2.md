# T245 — Warm EXCEPT and INTERSECT allocation parity — live status

> **2026-10-20 — rc5 acceptance reformulation (policy supersession).** The GitHub issue now uses the two-tier policy: **global** allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db; **local** acceptance = one-axis improvement with no regression on the other (allocations down + time not worse, or time better + allocations not up). This file's frozen thresholds and plan are the historical rc3 attempt. Before any rc5 execution, a new plan revision (`r+1`) must be planned from the reformulated issue; `N`/`r`/`n` are not reset.

task: T245
issue: 245
issue_url: https://github.com/AlexeyShirshov/nextorm/issues/245
issue_title: Warm EXCEPT and INTERSECT allocation parity
milestone: 1.0.9-rc3
cycle: 2
plan_revision: 1
evidence_contract_rv: 2
attempt: 1/3
terminal_reason: insufficient_evidence: unexplained_baseline_plus_1250B; no_proven_safe_sufficient_allocation_lever; final_clarification_budget_exhausted
stopped_group: G1
max_attempts: 3
selected_variant: pdca-dotnet
collection: rc3-4
mode: collection phase P — closed (incomplete) after DO
phase: closed (incomplete)
plan_state: ready
status: incomplete
branch: 1.0.9-rc3
base_ref: 0d4b0cf69489253249305e0900f9fe5e4f69c1c5
status_path: .pdca/status/T245-warm-set-ops-2.md
evidence_dir: .pdca/T245/rv2/
contract: .pdca/T245/rv2/contract.json (rv2; created in DO)
predecessor: .pdca/status/T245-warm-set-ops-1.md (cycle N=1, terminal `incomplete`/`insufficient` at r=2, preserved)
created: 2026-10-19

## Plan (r=1)

# T245 — warm set operations

## Identity and execution boundary

- **Issue:** https://github.com/AlexeyShirshov/nextorm/issues/245
- **Cycle:** N=2; **plan revision:** r=1.
- **Status:** `.pdca/status/T245-warm-set-ops-2.md`.
- **Base:** `0d4b0cf69489253249305e0900f9fe5e4f69c1c5`, branch `1.0.9-rc3`.
- **Contract revision:** rv2, separate from cycle number and plan revision.
- **Execution:** single main tree, one sequential group, lane cap **1**. No branches/worktrees.
- This document authorises a later DO phase; PLAN performs no edits, builds or benchmark runs.

The prior cycle ended `incomplete`/`insufficient` at r=2 with no production change. Preserve that status, its attempt history and its evidence. This cycle does not erase previous failures.

## 1. Goal and acceptance criteria

**Goal:** reduce ordinary warm cached EXCEPT and INTERSECT construction allocations to their individual limits and same-round linq2db parity, without changing behaviour or degrading existing workloads.

| ID | Observable criterion | Verification | Negative case |
|---|---|---|---|
| A1 | EXCEPT ≤5.05 KB/query **and** ≤1.00× same-round linq2db. | Raw BDN bytes, normalised exactly once; compare unrounded values against both limits. | Passing the absolute limit but exceeding parity fails. |
| A2 | INTERSECT ≤5.01 KB/query **and** ≤1.00× same-round linq2db. | Independent per-case comparison. | An EXCEPT pass or averaged set-op result cannot compensate. |
| A3 | No timing regression beyond the frozen ShortRun noise band, for either operation or any existing benchmark case. | Comparable baseline/candidate reports and complete case inventory. | Any case outside its band fails. |
| A4 | SQL, result ordering, duplicate handling and public API unchanged. | SQL assertions, unit tests, live-provider results and public-surface comparison. | Changed binding, SQL, ordering, duplicates or public declarations fails. |
| A5 | Prepared controls unchanged; `Warm_Reused = 0 B` unchanged. | Separate control rows and regression tests. | Moving allocation into preparation/reused execution fails. |
| A6 | Full unit and integration suites pass with no new skips/not-run cases; all five integration providers execute. | Complete test inventories, results and provider participation. | Missing provider or newly skipped test fails. |
| A7 | Attribution reconciles; final individual BDN numbers are recorded; both operation rows fail independently in the native allocation gate. | Manifest receipts, attribution, gate positive/negative tests and provenance lint. | Missing denominator, stale binary, unexplained residual or unenforced row fails. |

Use BDN’s 1,024-byte KB conversion and preserve raw bytes. **≤1.20× is only a checkpoint**, never final acceptance.

## 2. Constraints and resolved gaps

### Constraints

- General path only; no benchmark-specific shortcuts.
- Preserve `a.Union(b).Except(c)` shared-tail protection and mandatory clone isolation.
- Preserve cache-key semantics and D201-3 capture/lookup/apply/publish semantics.
- Public API unchanged, including the public sealed `QueryDefinition` record.
- Never disable caching through sticky state on a shared command.
- Preserve provider, configured-metadata and automatic-metadata behaviour.
- .NET 10, CPM, warnings-as-errors, FluentAssertions and CRLF.
- No push. No commits in PLAN. Later commits require explicitly enabled collection auto-commit and task-only staging.
- Preserve all **76 foreign dirty paths**, their contents and staging state. This expressly includes modified tracked `benchmarks/BenchmarkDotNet.Artifacts/results/**`.
- Never use broad staging, clean/reset/stash, or overwrite a colliding foreign path.

### Gaps closed

| Statement gap | Resolution |
|---|---|
| Prior plan excluded the remaining levers. | Owner authorisation dated 2026-10-19 permits protected comparers, preparation internals and internal `QueryDefinition` elision, conditional on the global gate. |
| Comparer savings were overstated. | Use **≤656 B/query**, not 984 B, pending fresh attribution. |
| Prior rounds were source-identical, not hash-identical. | Historical only. Fresh HEAD baseline, assembly hashes and source binding precede production edits. |
| Probe obtainability was uncertain. | Tracked benchmark sources, configuration, fixture, copy recipe, decomposition and divisor tooling establish reproducibility; details below. |
| `rv1` could collide with prior evidence. | Create rv2; preserve rv1 intact. |
| Native gate identity was unclear. | `.github/workflows/dotnet.yml` invokes `.pdca/scripts/perf/iteration14_gate.py`; the acceptance document is not itself a CI gate. |
| Test scope/schema was unknown. | Use the enforced `unit`/`scope` schema in §7. |
| Provider capability was ambiguous. | False dialect flags concern `ExceptAll`/`IntersectAll`, not ordinary EXCEPT/INTERSECT. Preserve both supported and unsupported contracts. |

## 3. Minimal solution and alternatives

### Recommended solution

Reuse the existing warm construction path, changing only proven allocation ownership:

1. Measure the current path and reconcile attribution.
2. Elide internal `QueryDefinition` allocations through equivalent shape storage while retaining the public record.
3. Make eligible comparers stateless and reuse registry instances only where ownership permits.
4. Remove the smallest remaining redundant preparation allocation without changing recipe semantics.
5. Stop production changes once the entire gate passes.

At the historical entry, 480 B plus at most 656 B gives at most **1,136 B** of obvious savings. Historical required reductions were **1,356.9/1,383.9 B**. Preparation is therefore an authorised, likely necessary lever. These estimates are not proof that savings compose.

`QueryShape` has no existing consumers. Map every required field, equality/hash contribution, captured value and lifetime before using it; do not treat its existence as proof of equivalence.

| Approach | Benefits | Cost/risk | Decision |
|---|---|---|---|
| Internal shape, safe comparer reuse, minimal preparation changes | Targets authorised costs; retains general path | Cache/ownership correctness and broad validation | **Chosen** |
| Revive operand-shell COW | Previously saved 1,136 B/query | Every screened variant missed timing; shared-tail risk | Excluded without new discriminating measurement and replan |
| Broader execution/cache redesign | Potentially larger savings | Unnecessary concepts and semantic exposure | Excluded |

## 4. Units and sequence

All units execute sequentially in one tree.

| Unit | Priority / owner | Result and evidence | Dependencies |
|---|---|---|---|
| **D245-1** | P0 / executor; CHECK validates | Tree snapshot, HEAD build, pinned fixture, complete runtime benchmark inventory, fresh baseline and frozen timing bands. No production edit. | Frozen PLAN |
| **D245-2** | P0 / executor | Fresh decomposition and ownership ledger; corrected comparer split and preparation reconciliation. | D245-1 |
| **D245-3** | P0 / coder | Internal shape elision, public API unchanged, focused tests and isolated measurement. | D245-2 |
| **D245-4** | P0 / coder | Safe stateless/reused comparers; registry/context/command isolation tests and incremental measurement. | D245-3 |
| **D245-5** | P0 / coder | Smallest necessary preparation reduction with unchanged recipe semantics. No production change if already unnecessary. | D245-4 checkpoint |
| **D245-6** | P0 / coder, then CHECK | Full tests, branch delta, mutations, coverage and complete benchmark comparison. | Required lever units |
| **D245-7** | P0 / executor, then CHECK | Native gate, docs/registry, complete rv2 evidence, validation and final verdict. | D245-6 |

**Sequence:** baseline at HEAD → decomposition → smallest authorised lever → focused tests and paired measurement → decision point → next necessary lever → global gate → final evidence/CHECK.

Use existing tests/helpers before adding machinery. A failed lever is reverted only through task-owned edits. Rejected candidates do not reset counters or justify unlimited variants.

## 5. Reconnaissance and measurement decisions

### Probe obtainability: demonstrated

The probe is obtainable from:

- Tracked `SqliteBenchmarkFeaturesFairCached.cs`, `SqliteBenchmarkWarmDecompose.cs`, `Program.cs`, benchmark project and `Infrastructure/NextormConfig.cs`.
- Tracked `benchmarks/nextorm.benchmark/data/test.db`.
- Committed fixture-copy recipe in `.github/workflows/benchmarks.yml:45` and `docs/specs/performance/benchmark-report.md:1217`.
- `BenchDb.cs:9-19` resolution through `NEXTORM_BENCH_DB`.
- Tracked `.pdca/scripts/perf/t244_evidence.py`, whose divisors are FairCached **10**, FeaturesFair **10**, WarmDecompose **100**, checked against `Iterations` and absence of `OperationsPerInvoke`.
- Existing tracked attribution/lever evidence and declared host lock.

Existing raw reports demonstrate prior execution, but are foreign dirty files and are neither staged nor accepted as this cycle’s baseline.

### Fresh baseline: required before production edits

Fresh HEAD baseline removes reliance on CHK-3’s source-identical historical rounds. Capture the complete suite before changing production code. Store all new reports under rv2, never the foreign results directory.

Before accepted measurements:

1. Verify task-relevant sources against the declared revision.
2. Record all foreign dirty paths and index state.
3. Verify both fixture copies have SHA-256
   `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`.
4. Build Release with zero warnings/errors.
5. Record source/diff, assembly, configuration and tooling hashes.
6. Record exact command, environment, expanded case inventory, jobs and divisors.
7. Acquire `flock -x /tmp/opencode/nextorm-bdn-host.lock`.

The recorded older binary is not reused without fresh build provenance.

### Measurement commands

For each `ROUND` of `baseline` or `candidate`, use a distinct immutable directory.

**Acceptance:**

`flock -x /tmp/opencode/nextorm-bdn-host.lock env -u NEXTORM_BENCH_FULL NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --exporters json --artifacts .pdca/T245/rv2/ROUND/faircached`

**Decomposition:** same command/environment, replacing the filter with
`'*SqliteBenchmarkWarmDecompose*'` and output with `.pdca/T245/rv2/ROUND/decompose`.

**Whole benchmark suite:** same command/environment, replacing the filter with
`'*'` and output with `.pdca/T245/rv2/ROUND/global`.

Default configuration is ShortRun plus `InProcessEmitToolchain`; explicitly unset `NEXTORM_BENCH_FULL`. Preserve comparable jobs and toolchains. The observed 65 classes/680 methods is a discovery cross-check, not the expanded runtime case count. Parameters and jobs must also be inventoried.

### Timing oracle and zero-degradation rule

Freeze the following before edits:

- For each existing case/job/toolchain, candidate mean must be ≤ **baseline mean + baseline BDN Error**.
- Candidate uncertainty cannot enlarge that allowance.
- Non-finite/missing Error, unstable controls or incomparable configuration yields **insufficient**, not pass.
- No existing case may increase allocated bytes per logical operation.
- No case may disappear, be substituted or be averaged with another case.
- Sub-20% timing deltas are not claimed as improvements on this host. The observed ~19% spread is not an automatic 20% regression allowance.
- An apparent failure may receive a recorded, predeclared confirmation run; all results remain visible. Persistent ambiguity returns to PLAN.

All five providers, all tests and all benchmark cases are mandatory. Any individual failure blocks the candidate.

## 6. Native gate and documentation route

### Native gate files

Modify only:

- `.pdca/scripts/perf/iteration14_gate.py`
- `.pdca/scripts/perf/iteration14-budgets.json`
- New `.pdca/scripts/perf/test_t245_gate.py`
- `.github/workflows/dotnet.yml`

Reuse `t244_evidence.py` scenario/divisor knowledge without changing its established results.

### Independent set-op budgets

Preserve all 28 existing logical budget rows and their 100-operation interpretation. Add two logical rows, producing **30** budget rows:

| Logical row | Divisor | Absolute raw invocation ceiling | Additional condition |
|---|---:|---:|---|
| `T245.Warm.Except` | 10 | 51,712 B | NextORM ≤ same-job linq2db |
| `T245.Warm.Intersect` | 10 | 51,302 B | NextORM ≤ same-job linq2db |

These integer ceilings conservatively implement 5.05/5.01 KiB/query. INTERSECT’s unrounded invocation ceiling is 51,302.4 B; integer-only input therefore permits at most 51,302 B.

Add a per-row divisor override; do not change the legacy default. Resolve the two existing NextORM arms and their linq2db references through the existing scenario mapping, verified against benchmark symbols.

Add a separate benchmark invocation using:

- `--filter '*SqliteBenchmarkFeaturesFairCached.B_*Except*' '*SqliteBenchmarkFeaturesFairCached.B_*Intersect*'`
- `--job short`
- JSON export to a task-owned artifact directory.

For each required job/toolchain:

- Require unique operation/implementation matches.
- Require raw integer allocation values and confirmed divisor 10.
- Enforce absolute ceiling and same-job parity separately.
- Preserve the existing worse-of-toolchains policy by requiring every required paired result to pass.
- Fail closed on missing, duplicate, malformed, unrecognised or incomparable required results.
- Never compare NextORM in one job with linq2db in another.

The gate is an allocation/parity gate; final timing acceptance remains the complete measured boundary gate.

### Timeout and CI

- Change gate invocation to `--timeout 3600`.
- Enforce a shared 3,600-second deadline: at most 1,200 seconds for legacy measurements and 2,400 seconds for the added set-op measurements.
- Set the allocation step’s `timeout-minutes: 65`.
- Wrap benchmark execution in the declared host lock; create its parent directory in CI if needed.
- Preserve existing gate unittests and add:

`python3 -m unittest discover -s .pdca/scripts/perf -p 'test_t245_gate.py'`

The new tests must demonstrate:

- EXCEPT-only absolute failure.
- INTERSECT-only absolute failure.
- EXCEPT-only parity failure.
- INTERSECT-only parity failure.
- Missing comparator/toolchain, duplicate result, non-integer allocation and wrong divisor failures.
- Existing 28 budgets remain unchanged and enforced.

A timeout is a failure, not permission to skip cases or silently increase the budget.

### Acceptance docs and registry

Modify:

- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFairCached.cs`
- `docs/specs/performance/acceptance-benchmarks.md`
- `docs/specs/performance/benchmark-report.md`

Add the acceptance category to the existing four ordinary warm arms: NextORM and linq2db for EXCEPT and INTERSECT. Do not change workload, method bodies, iteration count or operation normalisation merely to classify them.

Update the documented acceptance procedure:

- Preserve the existing seven cases.
- Add two paired operation rows, comprising four BDN cases.
- Update the exact acceptance result count from **7 to 11**.
- Keep the distinction between the documented acceptance set and native CI gate.
- Record each NextORM result, its same-round comparator, ratio, timing and prepared/reused controls.
- Add the new run to `benchmark-report.md`; retain historical entries.

These pages are internal specs. No public documentation/API change is planned, so no public EN/RU edit is required. If public documentation becomes necessary, replan its exact paired EN/RU paths; public pages must not link to internal specs.

## 7. Test strategy and mandatory scope

### Placement

- New database-free tests:
  `tests/nextorm.core.tests/WarmSetOperationTests.cs`.
- New live-provider shared tests:
  `tests/nextorm.integration.tests/CommonTestSuite/CommonTestSuite.WarmSetOperations.cs`.

The latter extends the existing `public abstract partial CommonTestSuite : ProviderTestSuite`, using its actual namespace and provider harness. Do not introduce another harness.

Plain EXCEPT/INTERSECT run against SQLite, PostgreSQL, SQL Server, MySQL and ClickHouse. Preserve existing capability-dependent `ExceptAll`/`IntersectAll` results and unsupported exceptions; their flags must not suppress ordinary operation tests.

### Test scope JSON

Write `.pdca/T245/rv2/inner-loop/brief.json` with this scope:

```json
{
  "unit": "T245-N2-single-tree",
  "scope": {
    "projects": [
      "tests/nextorm.core.tests",
      "tests/nextorm.sqlite.tests",
      "tests/nextorm.postgres.tests",
      "tests/nextorm.sqlserver.tests",
      "tests/nextorm.mysql.tests",
      "tests/nextorm.mariadb.tests",
      "tests/nextorm.clickhouse.tests",
      "tests/nextorm.clickhouse.extensions.tests",
      "tests/nextorm.entityframeworkcore.tests",
      "tests/nextorm.publicextensibility.tests",
      "tests/nextorm.alias.tests",
      "tests/nextorm.integration.tests"
    ],
    "selectors": [
      "FullyQualifiedName~WarmSetOperationTests",
      "FullyQualifiedName~WarmSetOperations",
      "FullyQualifiedName~CachedPreparation",
      "FullyQualifiedName~PlanKey",
      "FullyQualifiedName~ExpressionPlanEqualityComparerTests",
      "FullyQualifiedName~PlanKeyStructureTests",
      "FullyQualifiedName~AssertionStyleTests",
      "FullyQualifiedName~IntegrationSuiteScopingTests"
    ],
    "files": [
      "tests/nextorm.core.tests/WarmSetOperationTests.cs",
      "tests/nextorm.integration.tests/CommonTestSuite/CommonTestSuite.WarmSetOperations.cs"
    ],
    "rationale": "Internal shape, comparer and preparation changes cross cache identity, ownership and provider boundaries; focused loops are followed by every unit project and the complete live-provider suite.",
    "rebuild": "affected",
    "boundary": "Run all listed unit projects unfiltered and the complete integration project with SQLite, PostgreSQL, SQL Server, MySQL and ClickHouse; no new skips or missing providers.",
    "integration_projects": [
      "tests/nextorm.integration.tests"
    ],
    "integration_boundary_runs": 1
  }
}
```

The console `nextorm.alias.poc` is not a test project.

Validate:

`python3 .pdca/scripts/validate_inner_loop.py brief .pdca/T245/rv2/inner-loop/brief.json`

Maintain a matching execution journal and generate/report executions with command arrays, `phase`, source/artifact revisions and state. Every finished execution includes `exit_code` and `selected_count`.

Validate the completed report:

`python3 .pdca/scripts/validate_inner_loop.py report .pdca/T245/rv2/inner-loop/report.json --journal .pdca/T245/rv2/inner-loop/journal.json`

Also validate the journal:

`python3 .pdca/scripts/validate_inner_loop.py journal .pdca/T245/rv2/inner-loop/journal.json`

A focused command selecting zero tests fails the inner loop.

### Focused commands

- Core:
  `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~WarmSetOperationTests"`
- Integration:
  `dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~WarmSetOperations"`

Run existing relevant tests as well:

- Shared set-operation and cached-preparation tests.
- SQLite plan-cache, cached-path characterisation, builder and preparer tests.
- Provider PlanKey branch-coverage tests.
- Core expression comparer/plan-structure tests.
- Preparation recipe, LiveClone and detached-cache-clone tests.
- Assertion-style and integration-scoping conventions.

Resolve C# symbols and existing selectors with Roslyn; never infer them through text search.

### Variant matrix

| Axis | Required test/guard |
|---|---|
| EXCEPT / INTERSECT | Separate allocation, SQL and live-result assertions |
| Cached / prepared / reused | Independent controls and cache identity; reused allocation remains zero |
| Five live providers | Shared-suite execution with provider participation evidence |
| Registry hit / miss / clear / stale entry | Ownership, invalidation and rebinding tests |
| Same shape/different captures / different shapes | No captured-value leakage; unchanged cache semantics |
| Sequential / interleaved queries | Repeated construction and independent-query isolation |
| Concurrent independent contexts | Shared-registry safety and context isolation |
| Concurrent same context | Preserve documented support boundary; do not imply new support |
| Shared / non-shared tails | Explicit `a.Union(b).Except(c)` and unchanged original-query assertions |
| Empty / disjoint / overlapping / duplicate inputs | Exact ordered results and duplicate semantics |
| Recipe hit / miss / invalidation | Capture, lookup, apply and publish behaviour unchanged |
| `*All` supported / unsupported | Existing capability-specific results/exceptions unchanged |

No row is deferred to another release. Inapplicable same-context concurrency is closed by the existing documented contract, not by adding unsupported behaviour.

### Full boundary commands

Run `dotnet test <project> -c Debug`, without a filter, for **each of the eleven unit projects listed in the scope**.

Before integration runs, load `.opencode/skills/running-integration-tests/SKILL.md`.

Full integration:

`env -u NEXTORM_SKIP_PROVIDERS DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug`

Record provider execution and skip identities. The historical 195/37 skip budget is not automatically the current baseline.

### Branch delta, coverage and mutation checks

Collect baseline/candidate coverage using `coverage.settings.xml` and `coverage.integration.settings.xml`, preserving the workflow’s reporting filters and established integration InMemory exclusion.

Keep thresholds unchanged:

- Whole solution: **85% line / 75% branch**.
- Integration: **65% line / 75% branch**.

Produce a changed-method branch ledger. Exercise both outcomes of each introduced/changed decision; explain deleted branches and unchanged uncovered branches.

Use task-only manual mutants, one at a time:

1. Reuse a comparer bound to the preceding command.
2. Omit a shape/captured-value discriminator.
3. Apply stale preparation after invalidation.
4. Bypass shared-tail isolation.
5. Alter duplicate/order-sensitive processing.

Every applicable mutant must be killed by its designated focused test. Restore it before the next test or measurement. No new mutation dependency is required.

## 8. Evidence contract: rv2

### Route and paths

Create:

- `.pdca/T245/rv2/contract.json`
- `.pdca/T245/rv2/manifest.json`
- `.pdca/T245/rv2/inner-loop/**`
- `.pdca/T245/rv2/baseline/**`
- `.pdca/T245/rv2/candidate/**`
- `.pdca/T245/rv2/generations/**`

**Reason:** rv denotes contract revision, not cycle. The old rv1 contract lacks generator prerequisites and has no manifest. Superseding it with rv2 preserves all rv1 bytes and avoids retrofitting old evidence.

Contract fields include:

- `task`: `T245`
- `issue`: verified issue URL above
- `contract_rv`: `2`
- `supersedes`: `.pdca/T245/rv1/contract.json`
- `status_file`: `.pdca/status/T245-warm-set-ops-2.md`
- `path`: `.pdca/T245/rv2/manifest.json`
- `base_ref`: `0d4b0cf69489253249305e0900f9fe5e4f69c1c5`
- `footprint`: task-owned paths from §10
- `required_rows` and `rows`: exact matching IDs below

Use explicit exclusions for generated rv2 evidence and its status file rather than the default broad `.pdca/**`, so changed gate/evidence scripts remain tree-bound. Record auxiliary tooling hashes as well. Preserve foreign dirty paths; provenance describes them without staging them.

### Required rows

| Row | Required evidence |
|---|---|
| E245-01 | Cycle/base/candidate identities, dirty/index snapshot and predecessor reconciliation |
| E245-02 | Obtainable probe, fixture recipe/hash, build and assembly provenance |
| E245-03 | Fresh HEAD baseline and complete case/job inventory |
| E245-04 | Fresh reconciled attribution, corrected ownership and divisors |
| E245-05 | EXCEPT absolute limit and same-round parity |
| E245-06 | INTERSECT absolute limit and same-round parity |
| E245-07 | Individual timing comparisons and frozen bands |
| E245-08 | SQL/results/order/duplicates/public API invariance |
| E245-09 | Clone/shared-tail protection |
| E245-10 | Prepared controls and zero-allocation reused control |
| E245-11 | Comparer/registry/cache/recipe isolation and invalidation |
| E245-12 | Focused and complete unit results |
| E245-13 | Full integration results, five-provider participation and skip comparison |
| E245-14 | Coverage, changed branches and killed mutations |
| E245-15 | Native gate’s independent positive/negative tests and timeout behaviour |
| E245-16 | Whole benchmark zero-degradation comparison, docs/registry and final report |

Map predecessor row obligations to these rows; retain every original acceptance obligation. A row is `met` only with nonempty evidence. An `open` row blocks completion. `na` requires an actual predicate and cannot exempt a frozen acceptance criterion.

### Generate and validate

After final task edits and evidence verification:

`python3 .pdca/scripts/pdca_evidence.py generate --repo /home/alex/sources/nextorm --contract .pdca/T245/rv2/contract.json --output .pdca/T245/rv2`

Then:

`python3 .pdca/scripts/pdca_evidence.py lint --repo /home/alex/sources/nextorm --manifest .pdca/T245/rv2/manifest.json --scope-root .pdca/T245/rv2`

Require:

- `manifest.tree == recomputed tree`.
- Every receipt’s `tree` and `tree_binding` equal that tree.
- Exact `required_rows`↔`rows` coverage.
- Receipts under `generations/<digest>/results/<row-id>.json`.
- No stale receipts after edits; regenerate rather than relabel.
- Final brief/report/journal validation passes.

## 9. Overlay invariants

| # | Assessment |
|---|---|
| 1 | **Guarded:** no single-consumer abstraction split. Reuse existing shape/registry seams; new abstraction requires a demonstrated second consumer. |
| 2 | **Guarded:** minimal concepts. Any new type/indirection must name its concrete consumer and why existing storage cannot serve it. |
| 3 | **Satisfied by plan:** measurements and frozen comparisons replace assertions; sub-20% timing deltas are not claimed as wins. |
| 4 | **Guarded:** preserve rejected COW findings; consult relevant registers before reopening anything, requiring new measurement. |
| 5 | **Guarded:** public API unchanged. Any rename/removal requires replan and simultaneous public EN/RU updates. |
| 6 | **Satisfied by gates:** warnings-as-errors, nullable, CPM and CRLF verified during build/diff review. |
| 7 | **Guarded:** no facade/dialect restructuring; retain established architecture and composite dialect contract. |
| 8 | **Satisfied:** all units remain in `1.0.9-rc3`; no future-release deferral. |
| 9 | **Satisfied:** acceptance, negative cases, execution-path matrix and every row’s closure are explicit. |
| 10 | **Satisfied:** Roslyn-only C# symbol work; ignore-aware prose/config searches; no recursive grep/find. |
| 11 | **Satisfied:** tracked harness/configuration, tracked fixture and committed copy recipe demonstrate obtainable probes. Fresh HEAD baseline removes CHK-3 reliance. |
| 12 | **Satisfied:** prior terminal cause is explicit; new owner authorisation opens previously unavailable levers under the global gate, with corrected attribution and fresh evidence. Original targets are preserved. |

## 10. Metadata and planned footprint

Only the necessary subset may change.

### Production

- `src/nextorm.core/Builders/EntityBuilder.cs`
- `src/nextorm.core/QueryCommand.cs`
- `src/nextorm.core/QueryCommand.QueryPreparer.cs`
- `src/nextorm.core/QueryCommand.Plan.cs`
- `src/nextorm.core/QueryPlanEqualityComparer.cs`
- `src/nextorm.core/ExpressionPlanEqualityComparer.cs`
- `src/nextorm.core/Expressions/JoinExpressionPlanEqualityComparer.cs`
- `src/nextorm.core/DataContext/QueryPlanner.cs`
- `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs`

`QueryDefinition.cs`, clone and set-operation entry files remain read/guard targets; no public-record or clone-semantics change is authorised.

### Tests, benchmarks, gate and docs

- `tests/nextorm.core.tests/WarmSetOperationTests.cs`
- `tests/nextorm.integration.tests/CommonTestSuite/CommonTestSuite.WarmSetOperations.cs`
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFairCached.cs`
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWarmDecompose.cs` — instrumentation only if needed
- `.pdca/scripts/perf/t245_evidence.py`
- `.pdca/scripts/perf/iteration14_gate.py`
- `.pdca/scripts/perf/iteration14-budgets.json`
- `.pdca/scripts/perf/test_t245_gate.py`
- `.github/workflows/dotnet.yml`
- `docs/specs/performance/acceptance-benchmarks.md`
- `docs/specs/performance/benchmark-report.md`
- `.pdca/status/T245-warm-set-ops-2.md`
- `.pdca/T245/rv2/**`

Do not modify rv1, prior status files, tracked raw BDN reports or any other foreign dirty path. Before any permitted commit, compare the staged path list against this allowlist and the initial foreign index snapshot.

### Required predecessor reconciliation

Re-verify rather than silently transplant:

- Historical attribution totals 7,056/7,040 B and zero residual.
- Preparation 2,248.12/2,248.10 B uses the correct denominator.
- Corrected comparer ownership replaces the overstated split.
- COW was reverted; cited source interval is net-identical.
- Historical source identity is not binary/revision identity.
- Prior terminal outcome and rejected-lever history remain intact.

### Assumptions and uncertainty

Prerequisites: available host lock, pinned fixture, clean task-relevant paths, unchanged foreign dirt, Release build provenance, functioning live-provider environment and recorded runtime case inventory.

Confidence is **medium** that authorised ownership costs contain sufficient allocation room; combined timing success remains unverified until measurement. Savings may overlap and are not assumed additive.

References:

- https://github.com/AlexeyShirshov/nextorm/issues/245
- `.pdca/status/T245-warm-set-ops-1.md`
- `.pdca/T245/rv1/contract.json`
- `.pdca/T245/rv1/attribution.json`
- `.pdca/T245/rv1/lever-decision.md`
- `docs/specs/status/D240-construction-allocation-1.md`
- `docs/specs/performance/acceptance-benchmarks.md`
- `docs/specs/performance/benchmark-report.md`
- `.opencode/skills/nextorm-pdca/SKILL.md`

## 11. Risks and stop conditions

Return **GAP/STOP**, not success, if:

- Fresh baseline or attribution cannot be reconciled.
- Available savings require public API, cache-key, clone/shared-tail or recipe-semantic changes.
- Either operation misses either final allocation requirement.
- Any prepared/reused control, existing benchmark case, test or provider regresses.
- Runtime inventory is incomplete, a benchmark prerequisite is unavailable, or results remain incomparable.
- The native gate cannot distinguish the two failures independently.
- A required path collides with foreign work.
- Provenance lint or inner-loop validation fails.
- Evidence remains inconclusive after targeted confirmation.

No benchmark subset, new skip, relaxed budget, averaged result or fourth-attempt bypass may substitute for completion.

**DO → PLAN classification:** not applicable at entry. Later candidates must be classified by PLAN; the orchestrator routes rather than waives gates. Persistent low confidence warrants escalation under trigger 5.

## Progress log

- 2026-10-19T13:16:00Z | PLAN | revision r=1 | iteration 1/3 | PLAN gathered — two read-only reconnaissance passes (prior terminal state, code map, harness/gate/test inventory) | `.pdca/status/T245-warm-set-ops-1.md`, `.pdca/T245/rv1/`
- 2026-10-19T13:17:30Z | PLAN | revision r=1 | iteration 1/3 | PLAN decided by pdca-planner — r=1, PLAN_STATE ready, rv2 | `.pdca/status/T245-warm-set-ops-2.md`
- 2026-10-19T13:19:10Z | PLAN→DO | revision r=1 | iteration 1/3 | PLAN→DO boundary reached; handoff to collection parent; DO not started; no product change | `.pdca/status/collection-rc3-4.md`

Notice: pdca-scout was unavailable in this host; facts were gathered by two read-only pdca-executor passes and the plan was decided by pdca-planner.

## Handoff

- **plan_state:** `ready` (PLAN→DO boundary; DO not started; no product change).
- **Planned footprint:** exactly the §10 lists — production `src/nextorm.core/{Builders/EntityBuilder.cs, QueryCommand.cs, QueryCommand.QueryPreparer.cs, QueryCommand.Plan.cs, QueryPlanEqualityComparer.cs, ExpressionPlanEqualityComparer.cs, Expressions/JoinExpressionPlanEqualityComparer.cs, DataContext/QueryPlanner.cs, DataContext/Cache/QueryPlanStore.cs}`; tests `tests/nextorm.core.tests/WarmSetOperationTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite/CommonTestSuite.WarmSetOperations.cs`; benchmarks `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFairCached.cs`, `SqliteBenchmarkWarmDecompose.cs` (instrumentation only); gate/CI `.pdca/scripts/perf/{t245_evidence.py, iteration14_gate.py, iteration14-budgets.json, test_t245_gate.py}`, `.github/workflows/dotnet.yml`; docs `docs/specs/performance/{acceptance-benchmarks.md, benchmark-report.md}`; status/evidence `.pdca/status/T245-warm-set-ops-2.md`, `.pdca/T245/rv2/**`. Out of footprint: rv1, prior status files, tracked raw BDN reports, all other foreign dirty paths.
- **Uncertainty:** `medium` on allocation room — authorised ownership pools are 480 B (`QueryDefinition` elision) + ≤656 B (comparers) + preparation against a historical 1,356.9/1,383.9 B requirement, and savings are not assumed additive; `low` on combined timing until measured (sub-20 % deltas are not claimed as wins; the ~19 % host spread is not a regression allowance).
- **Predecessor requirements:** rv1 attribution totals 7,056/7,040 B with zero residual; preparation 2,248.12/2,248.10 B on the correct denominator; corrected comparer ownership (≤656 B, not 984 B) replacing the overstated split; COW revert leaves the cited source interval net-identical; prior terminal record `.pdca/status/T245-warm-set-ops-1.md` (`incomplete`/`insufficient` at r=2) preserved verbatim with its attempt history and evidence.
- **Assumptions / prerequisites:** host lock `/tmp/opencode/nextorm-bdn-host.lock` acquirable (`flock -x`, `--close`); both fixture copies sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; clean task-relevant paths; the 76 foreign dirty paths unchanged; fresh Release build provenance (no reused older binary); Podman socket `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` for all five providers; recorded runtime case inventory (65 classes/680 methods is only a discovery cross-check).
- **Refs:** https://github.com/AlexeyShirshov/nextorm/issues/245; `.pdca/status/T245-warm-set-ops-1.md`; `.pdca/T245/rv1/{contract.json, attribution.json, lever-decision.md}`; `docs/specs/status/D240-construction-allocation-1.md`; `docs/specs/performance/acceptance-benchmarks.md`; `docs/specs/performance/benchmark-report.md`; `.opencode/skills/nextorm-pdca/SKILL.md`.
- 2026-10-19T13:56:26Z | DO | revision r=1 | iteration 1/3 | DO started (r=1, n=1/3); D245-1 running (preflight + fresh HEAD baseline); no production edit | `.pdca/T245/rv2/preflight.md`
- 2026-10-19T13:56:26Z | DO | revision r=1 | iteration 1/3 | Preflight PASS: HEAD 0d4b0cf matches base, fixture sha256 both copies match, host lock acquirable, harness present; dirty snapshot 81 paths (plan said 76 — 5 additional foreign paths appeared after PLAN; none task-owned except untracked status file) | `.pdca/T245/rv2/preflight.md`, `.pdca/T245/rv2/dirty-snapshot.txt`
- 2026-10-19T15:56:00Z | DO | revision r=1 | iteration 1/3 | D245-1: preflight PASS, Release `dotnet build nextorm.slnx -c Release` 0 Warning(s)/0 Error(s) (69.19 s), both fixture copies sha256-pinned; acceptance and decomposition baselines complete for TWO rounds (55 and 58 BDN cases/round) | `.pdca/T245/rv2/preflight.md`, `.pdca/T245/rv2/provenance.json`, `.pdca/T245/rv2/baseline/**`
- 2026-10-19T15:56:00Z | DO | revision r=1 | iteration 1/3 | Whole-suite baseline round-1 killed by timeout: rc=124, 470 method headers started / 441 finished in 41 class batches / 358 (43.3 %) remained; round-2/global not started (same ~3 h suite, host budget exhausted). No case substituted, no subset accepted; complete runtime inventory and zero-degradation baseline are NOT obtainable → **D245-1 blocked/incomplete** | `.pdca/T245/rv2/global-baseline-failure.md`, `.pdca/T245/rv2/logs/baseline-round1-global.log`
- 2026-10-19T15:56:00Z | DO | revision r=1 | iteration 1/3 | D245-2 complete: fresh attribution round-2 EXCEPT 6528.10 B/query vs linq2db 5176.20 (1.2612×), INTERSECT 6512.00 vs 5128.50 (1.2698×); preparation 2248.11 B on the corrected denominator (Warm_PlanOnly−Construct), QueryDefinition 480, comparers ≤656 (984 overstated), residual stated; no product change | `.pdca/T245/rv2/attribution.json`
- 2026-10-19T15:56:00Z | DO | revision r=1 | iteration 1/3 | Provenance event: foreign commits moved HEAD 0d4b0cf→bcb8e676 at 14:12Z; zero measured-path diff, fixture/assembly hashes unchanged, rounds admitted on provenance; foreign dirty snapshot 81→6 by their owner, nothing staged/committed by this dispatch | `.pdca/T245/rv2/provenance-head-moved.md`
- 2026-10-19T15:56:00Z | DO | revision r=1 | iteration 1/3 | Journal valid (rc=0); `validate_inner_loop.py project` rc=2 — the canceled whole-suite attempt leaves its command group without a finished execution, so no report is emitted (fail-closed, intentional). D245-1 remains blocked; D245-3…D245-7 not started | `.pdca/T245/rv2/inner-loop/journal.json`, `.pdca/T245/rv2/inner-loop/validation-failure.txt`

## DO units and final state (r=1, n=1/3)

| Unit | State | Evidence |
|---|---|---|
| D245-1 | blocked | Whole-suite baseline rc=124 (GNU timeout at 5,400 s: 470 method headers started, 441 finished, 358 ≈43.3 % remained; estimated exhaustive round ≈2 h 40). The validator also requires a same-identity finished execution for every command group, which the canceled global group cannot provide. All other D245-1 sub-steps passed (preflight, fixture hashes, Release build, two acceptance/decomposition rounds). |
| D245-2 | done | Fresh decomposition and ownership ledger reproduced: preparation 2,248.11 B, QueryDefinition 480 B, comparers ≤656 B, residual 527.90/528.00 B cross-domain; `.pdca/T245/rv2/attribution.json`. |
| D245-3 | pending | Not started (`QueryShape`/internal shape elision). |
| D245-4 | pending | Not started (stateless/reused comparers). |
| D245-5 | pending | Not started (preparation reduction). |
| D245-6 | pending | Not started (full tests/coverage/benchmark comparison). |
| D245-7 | pending | Not started (native gate, docs, rv2 evidence, final verdict). |

No unit is `superseded`; no scope replacement occurred.

**Product diff: zero.** Nothing under `src/`, `tests/`, `benchmarks/`, `.github/` or `docs/` was modified by this task. Task-owned path check: `git status --porcelain -- src tests benchmarks .github docs` returned empty output (rc=0) — no task-owned modification outside `.pdca/**`. Nothing was staged, reverted, cleaned or deleted by this dispatch.

## Escalation outcome

- **Trigger:** escalation trigger 5 (persistent low confidence: no proven, safe, sufficient allocation lever at an affordable oracle cost), together with the sizing question of trigger 2 (whether the remaining scope could be re-sized instead of replanned).
- **Decision:** **B — STOP**; the cycle terminates `incomplete` at r=1, n=1/3. No r=2 plan is written in this lane.
- **Lever arithmetic:** the required reduction is ≈**1,590 B/query** in the worst same-round case (EXCEPT round-1 6,640.10 B − 5,050 B = 1,590.10 B against the 5.05 KB target; the strict absolute ceiling 5,171.2 B implies 1,468.9 B, and round-2 implies 1,356.9 B, INTERSECT 1,381.76 B). Available pools: `QueryDefinition` **480 B** (proven-disjoint); comparers **≤656 B** but **unsafe** (shared registry/ownership and cache-identity risk inside the frozen D201-3 recipe); `QueryShape` is missing **12 required fields**, so it cannot store an equivalent shape without new machinery. Even if every pool worked, the total is ≈**1,136 B** (480 + 656) — below the requirement. Savings are not assumed additive, and no single proven safe sufficient lever exists.
- **Oracle-cost facts:** the whole-suite baseline is the mandatory zero-degradation oracle, yet one exhaustive round costs ≈2 h 40 (round-1: 5,398 s wall, rc=124, 358/470 ≈43.3 % methods remaining, estimated finish 17:02Z). Every lever would need ≥2 such rounds (baseline + candidate) plus the full test/coverage/gate matrix, which is not affordable in this dispatch window; without the baseline, gate rows E245-03/E245-07/E245-16 stay unattainable. `validate_inner_loop.py project` rc=2 is the intended fail-closed signal: the canceled global command group has no finished same-identity execution.
- **Follow-up (recorded, NOT executed):** a bounded single-case bisect of the **+1,250 B** unexplained baseline delta between the prior-cycle commit and HEAD, then a **new plan for #245 with the acceptance criteria unchanged**. No further T245 cycle runs in this lane.

## Done / Verified

- **Done:** preflight PASS (HEAD `0d4b0cf`, both fixture copies sha256-pinned, harness present, host lock acquirable); Release `dotnet build nextorm.slnx -c Release` **rc=0, 0 warnings / 0 errors**, 69.19 s; two same-session baseline rounds (55 and 58 BDN cases) with per-case numbers recorded — EXCEPT **6,640.10 / 6,528.10 B** vs linq2db **5,328.20 / 5,176.20 B**; INTERSECT **6,624.10 / 6,512.00 B** vs linq2db **5,280.20 / 5,128.50 B**; attribution/ownership ledger reproduced (preparation **2,248.11 B**, QueryDefinition **480 B**, comparers **≤656 B**, residual **527.90 / 528.00 B** cross-domain); **zero product change**.
- **Verified by:** build rc=0; `validate_inner_loop.py journal` rc=0; `validate_inner_loop.py brief` rc=0; `validate_inner_loop.py project` rc=2 — canceled global group without a finished execution, **fail-closed by design** (no report emitted).
- **Not done:** D245-3 … D245-7; the whole-suite zero-degradation baseline; native gate rows (E245-15); docs/registry (E245-16 remainder); any acceptance gate row.

## Changed files

- `.pdca/T245/rv2/**` — new evidence only (`attribution.json`, `baseline/**`, `logs/**`, `inner-loop/**`, `preflight.md`, `provenance.json`, `provenance-head-moved.md`, `dirty-snapshot.txt`, `global-baseline-failure.md`, `incomplete.patch`).
- `.pdca/status/T245-warm-set-ops-2.md` — this status file.
- No product, test, benchmark, gate, CI or documentation path changed (zero product diff; `git status --porcelain -- src tests benchmarks .github docs` empty, rc=0).

## Next plan

`— (this cycle closed incomplete; no further cycle of T245 in this lane; follow-up is a bounded baseline bisect then a new plan for #245)`

## Progress log (continued)

- 2026-10-19T16:00:00Z | DO | revision r=1 | iteration 1/3 | DO started (r=1, n=1/3); D245-1 preflight + fresh HEAD baseline | `.pdca/T245/rv2/preflight.md`
- 2026-10-19T16:00:00Z | DO | revision r=1 | iteration 1/3 | D245-1 baseline rounds complete: two same-session rounds (55/58 BDN cases), per-case numbers recorded; EXCEPT 6,640.10/6,528.10 vs 5,328.20/5,176.20, INTERSECT 6,624.10/6,512.00 vs 5,280.20/5,128.50 | `.pdca/T245/rv2/baseline/**`, `.pdca/T245/rv2/attribution.json`
- 2026-10-19T16:00:00Z | DO | revision r=1 | iteration 1/3 | D245-1 global suite canceled rc=124 (timeout 5,400 s; 358/470 ≈43.3 % methods remained; exhaustive round ≈2 h 40) → whole-suite baseline/inventory unobtainable | `.pdca/T245/rv2/global-baseline-failure.md`, `.pdca/T245/rv2/logs/baseline-round1-global.log`
- 2026-10-19T16:00:00Z | DO | revision r=1 | iteration 1/3 | D245-2 done: attribution/ownership ledger reproduced (preparation 2,248.11 B; QueryDefinition 480 B; comparers ≤656 B; residual 527.90/528.00 B cross-domain); zero product change | `.pdca/T245/rv2/attribution.json`
- 2026-10-19T16:05:00Z | DO→PLAN | revision r=1 | iteration 1/3 | DO→PLAN candidate raised: D245-1 blocked on the whole-suite oracle; no safe sufficient lever demonstrated | `.pdca/T245/rv2/global-baseline-failure.md`
- 2026-10-19T16:10:00Z | PLAN | revision r=1 | iteration 1/3 | PLAN clarification 1: a targeted scout is required before any revised lever claim | `.pdca/status/T245-warm-set-ops-2.md`
- 2026-10-19T16:25:00Z | PLAN | revision r=1 | iteration 1/3 | PLAN clarification 2: terminal `incomplete`, final clarification budget exhausted, no r=2 | `.pdca/status/T245-warm-set-ops-2.md`
- 2026-10-19T16:30:00Z | ESCALATE | revision r=1 | iteration 1/3 | Escalation trigger 5 (plus trigger-2 sizing question) → decision B: STOP, no r=2; required ≈1,590 B vs ≈1,136 B of proven/risky pools; oracle ≈2 h 40/round | `## Escalation outcome`
- 2026-10-19T16:35:00Z | ACT | revision r=1 | iteration 1/3 | ACT closed; cycle incomplete; group G1 stopped; evidence preserved, zero product diff | `.pdca/T245/rv2/incomplete.patch`

Notice: pdca-scout is not dispatchable in this host; gather ran through the read-only `scout` role and read-only pdca-executor passes.
