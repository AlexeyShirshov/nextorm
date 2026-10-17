# D245 — Warm EXCEPT / INTERSECT allocations — cycle status

task: T245
issue: #245
issue_title: Warm EXCEPT and INTERSECT allocation parity
milestone: 1.0.9-rc3
collection: collection-1.0.9-rc3-open-3
collection_status_path: docs/specs/status/collection-1.0.9-rc3-open-3.md
group: unallocated_by_collection_ALL_barrier
selected_variant: pdca-dotnet
cycle: 1
attempt: 1
plan_revision: 1
evidence_contract_rv: 1
plan_state: ready
status: plan_ready
blocked_reason: none
baseline_ref: 957fcfe7
base_ref: 01815f3a
- **Re-anchor (2026-10-17):** base `b5ab6502` — benchmarks and tests were reorganized by topic; paths updated, decisions/criteria/counters unchanged. Repo test conventions apply to any test this task adds: FluentAssertions-only assertions (`Assert.Skip*` allowed) and integration tests must exercise a real DBMS (no `InMemoryDataContext` in `tests/nextorm.integration.tests`).
branch: 1.0.9-rc3
worktree: /home/alex/sources/nextorm
execution_mode: autonomous
evidence_dir: artifacts/pdca/D245/rv1/
frozen_manifest: artifacts/pdca/D245/rv1/manifest.json
contract_input: artifacts/pdca/D245/rv1/contract.json
check_regather_budget: 2
check_regather_owner: check
dependency: collection measurement lock; settled predecessor patches; T244 preparation/clone boundary
plan_scope: collection_task_PLAN_to_DO_handoff

## PLAN (r=1)

### Goal

Reduce fresh-fluent, warm-cache allocations independently:

- EXCEPT: from approximately 6.38 KB/query to ≤5.05 KB/query and ≤1.00× same-round linq2db.
- INTERSECT: from approximately 6.36 KB/query to ≤5.01 KB/query and ≤1.00× same-round linq2db.

An allocation ratio ≤1.20× is a checkpoint, never task completion.

Preserve:

- Existing SQL text, result multisets, duplicate semantics and observable in-memory ordering.
- Public API and provider capability behavior.
- The #240 shared-tail clone-before-mutation guard.
- Prepared controls and every existing `Warm_Reused = 0 B` row.
- Cache-key semantics and the frozen D201-3/#240 recipe.
- Per-case reporting: no averaged set-operation result.

SQL without an explicit ordering clause has no ordering guarantee. This task must not introduce one or change existing ordered-query behavior.

### Acceptance criteria

| ID | Observable acceptance | Negative case |
|---|---|---|
| R245-01 | EXCEPT satisfies both the absolute 5.05 KB cap and raw allocation ratio ≤1.00 against linq2db in each of two final same-round reports. | Either round exceeds either limit, or the comparator is from another round. |
| R245-02 | INTERSECT satisfies both the absolute 5.01 KB cap and raw allocation ratio ≤1.00 in each final round. | Either round exceeds either limit; passing EXCEPT cannot compensate. |
| R245-03 | Per-case final nextorm means show no regression beyond the ShortRun +20% noise band against the two-round entry baseline on the same host. Both rounds and individual means are reported. | A repeatable >20% slowdown, conflicting unexplained rounds, or concurrent benchmark activity. |
| R245-04 | Existing exact SQL snapshots remain unchanged; added boundary cases render the same SQL before and after optimization. | Updating expected SQL to accommodate the optimization, or changed operand grouping/order. |
| R245-05 | SQL results retain their multisets; in-memory results retain their sequence where currently observable. Ordered set-operation cases retain their existing behavior. | Correct counts with changed values, changed in-memory sequence, or changed explicit-order results. |
| R245-06 | UNION/UNION ALL and supported INTERSECT ALL/EXCEPT ALL retain duplicate behavior; unsupported providers retain their existing exception behavior. | ALL becomes DISTINCT, duplicate multiplicity changes, or an unsupported variant starts rendering SQL. |
| R245-07 | Roslyn-based before/after public-surface comparison reports no public API changes. | Added, removed or changed public/protected signatures or constraints. |
| R245-08 | `NestedSetOperations_KeepOperandsUnchanged` and sibling builder-sharing regressions pass; operands remain reusable after chained operations. | Re-rendering `b` after `a.Union(b).Except(c)` changes its SQL or state. |
| R245-09 | Prepared EXCEPT/INTERSECT controls retain their per-operation allocations; timing remains within the same noise policy. | Warm fresh-fluent gains are purchased by increased prepared allocations. |
| R245-10 | Every existing Cte/RecursiveCte/Join4/InAtIn `Warm_Reused` row remains exactly 0 B/op. | Any existing row becomes nonzero or disappears. |
| R245-11 | Two new native allocation-gate rows cover fresh-fluent warm EXCEPT and INTERSECT, use 100 logical operations, and enforce strict byte budgets. Existing gate rows still pass. | Rows cover preparation only, use 10 operations, have inflated budgets, or a synthetic over-budget report passes. |
| R245-12 | Each operation has entry/final construction, preparation/hash and warm-path attribution, plus its own final BDN allocation and time. | Claiming savings from empty metadata without measurement, unexplained residual allocation, or an averaged set-operation number. |
| R245-13 | Frozen recipe/cache-key semantics are unchanged; #240 residual obligations retain their IDs, threshold and historical results. | Raw-shape eligibility changes, frozen-key/equality changes, threshold relaxation or silent closure of the #240 residual. |
| R245-14 | Focused tests, one full boundary sweep and container-backed integration pass on the final frozen tree; coverage follows repository policy without concealing regressions. | Missing provider runs, skipped container suites presented as passing, stale tests, or unreported coverage deterioration. |
| R245-15 | EN/RU comparison tables, follow-up registry and internal performance record agree with the final per-case artifacts. | Stale ratios, missing RU update, an interim checkpoint presented as parity, or a public link to internal specs. |
| R245-16 | Canonical evidence manifest has exact contract-row coverage, valid provenance and a matching frozen tree; every applicable row is met. | Missing evidence, stale-tree evidence, an unjustified `na`, or a missing row hidden by contract revision. |

**Units:** use raw exporter bytes, not rounded Markdown cells. The absolute KB caps use BenchmarkDotNet’s 1024-byte KB convention: EXCEPT ≤5171.2 B/op; INTERSECT ≤5130.24 B/op. Verify exporter units before admission; an incompatible unit interpretation is an evidence precondition, not permission to relabel results.

### Minimal solution & variants

**Essential change:** remove only the allocations demonstrated to be avoidable on these two fresh-fluent warm paths.

**Hard boundary:** retain full preparation and structural-cache correctness unless an allocation can be removed without changing the frozen recipe, identity rules or observable command state.

**Optimum:** the smallest measured change meeting both final caps, with unchanged controls.

The empty-metadata-clone hypothesis is **inert for this benchmark**: `_joins`, `_sorting` and `_arrayJoins` enter construction as null. `CopyTo(copyAll:true)` performs reference assignments and does not take its allocating cache-copy branch. Neither is an accepted attribution without contrary measurements.

| Alternative | Benefit | Cost / risk | Decision |
|---|---|---|---|
| Attribution-led removal of redundant command construction/reset or non-frozen preparation allocations | Smallest plausible change; preserves current cache lookup route | Savings must be measured; state/aliasing mistakes can affect six operations | **Recommended**, with a decision checkpoint after D245-1. |
| Broader immutable representation or shared preparation-state redesign | Potentially larger savings | Large semantic footprint; CTE overlap; API/state and cache risks | Deferred within `1.0.9-rc3`; trigger: attribution proves the narrow approach cannot meet the caps. Requires PLAN revision before implementation. |
| Admit set operations into the raw-recipe/cache shortcut or alter plan-key equality | Could avoid recurring full preparation | Violates this task’s frozen-recipe/cache-key constraint | Rejected for this plan. A measured need for this route returns to PLAN; it is not an authorized fallback. |

No implementation lever is selected merely because it is easy to edit. D245-1 must identify allocation sites and demonstrate a savings budget sufficient to reach the final caps. If it cannot, D245-3 remains blocked and the orchestrator returns to PLAN.

### What the request did not specify

| Gap | Closure |
|---|---|
| Which allocation sites account for the approximately 1.3 KB excess? | Required decomposition/allocation-stack experiment in D245-1; no guessed cause. |
| Whether preparation-state reuse is safe for different metadata shapes | Tests and guards cover preparation flags, aliases, metadata and frozen cache behavior before accepting such a lever. |
| Exact ordering expectations | SQL unordered results are compared as multisets; existing explicit ordering and in-memory sequences are preserved. |
| Coverage baseline on this branch | Capture entry coverage and apply repository thresholds: 85% line / 75% branch; hard gate on `main`, explicit warnings on this branch. Do not hide new deterioration. |
| Whether CS1591 is enabled for the affected projects | Inspect effective build configuration before editing. No public API change is authorized; preserve any existing enforcement. |
| Final native-gate method IDs | Planned sources only. Bind manifest rows to actual implemented benchmark symbols after Roslyn discovery; do not invent symbols in this plan. |
| Existing native-gate CLI details | Read its supported entry point during D245-1. The planned scoped evidence adapter must invoke that entry point and record the exact underlying invocation. It must not reimplement a weaker gate. |
| Python, profiling and coverage-tool availability | D245-1 prerequisite checks; record tool versions. Missing tools are an in-cycle prerequisite unless unavailable beyond permitted resources. |
| Which collection tasks precede T245 | Collection planner decides after the ALL-barrier. The dependency requirements below apply to whichever tasks actually precede it. |
| Existing behavior exposed as defective by new tests | Record the entry-tree failure. Do not silently expand this task into a semantic fix; return to PLAN if it prevents proving unchanged behavior. |

### Task list

All sources below are **planned evidence sources** until produced. New tests go into existing discovered classes/files; this plan does not assign future C# test-symbol names.

| Unit | Scope and disposition | Affected test subset | Boundary / integration |
|---|---|---|---|
| **D245-1 — Establish entry evidence and attribution decision** | **Fix now / prerequisite.** Capture entry tree, toolchain, public surface, SQL/results, controls and two isolated BDN rounds. Extend per-case decomposition in `SqliteBenchmarkWarmDecompose.cs:259-292` for EXCEPT/INTERSECT. Attribute construction, `Prepare` and hash/equality costs separately; allocation stacks supplement, not replace, BDN totals. Inspect `QueryCommand.cs:319-400`, `QueryCommand.Clone.cs:18-169`, `QueryCommand.QueryPreparer.cs:115,135-164`, `QueryPlanner.cs:609-654,836-871`, and `QueryPlan.cs:40-48,72-97` through Roslyn. Record chosen eligible lever or return to PLAN. | `C-CORE`; `C-SQL`; existing perf-tool tests through `C-PY`. Attribution scaffolding must not change production behavior. | No full integration boundary required for measurement-only edits. Entry integration behavior needed by new semantic cases is captured through `C-INT`; it does not replace the final boundary. |
| **D245-2 — Add narrow evidence admission and native-gate coverage** | **Fix now.** Add two 100-operation fresh-fluent warm set-operation benchmark arms in the natural native-gate host; preserve existing arms. Update `scripts/perf/iteration14-budgets.json` and only necessary gate/report wiring. Planned scoped sources: `scripts/perf/t245_evidence.py`, `scripts/perf/tests/test_t245_evidence.py`. Keep `t240_evidence.py` and its five-scenario/default-threshold contract unchanged. | `C-PY`; `C-NATIVE`. Test absent/duplicate rows, wrong operation count, raw-byte units, round mismatch, one-case failure and strict over-budget rejection. | Gate/report boundary only. No production changes in this unit. |
| **D245-3 — Implement the measured narrow optimization and semantic guards** | **Fix now, blocked on D245-1 decision.** Candidate region: six methods at `QueryCommand<TResult>.cs:1042-1122`, cloning/state preparation boundaries and non-frozen allocation sites identified by measurement. Preserve `QueryCommand.cs:1171-1181`. `QueryCommand.Clone.cs:18-169` is conditional footprint, not an instruction to edit it. Frozen recipe regions remain read-only. Add boundary tests in existing `T240CommandConstructionTests.cs`, `T240BuilderConstructionTests.cs`, `InMemoryTests.cs`, provider `SqlGenerationTests.cs`, and `CommonTestSuite.SetOperations.cs` as applicable. | After each production edit: `C-CORE` and `C-SQL`; selected new cases are admitted through these existing class filters. Run `C-PY` if evidence/gate code changes. | Final code exit requires `C-BUILD`, `C-CORE`, `C-SQL`, `C-INT`, `C-COVERAGE` and `C-NATIVE`. Exactly **one complete final boundary sweep** is mandatory; source changes afterward invalidate affected evidence and require another sweep. |
| **D245-4 — Final measurement, documentation and evidence freeze** | **Fix now.** Two final comparison rounds; prepared controls; native gate; per-case attribution reconciliation; EN/RU tables and internal records. Reopen #240’s existing residual IDs for the affected retuning claim without changing its recipe or 1.20 threshold. | `C-PY`, `C-COMPARE`, `C-CONTROLS`, `C-NATIVE`, `C-DOCS`, `C-LINT`. | No extra integration sweep for documentation-only edits. Any production or execution-relevant benchmark edit returns to D245-3’s boundary requirement. |
| **Deferred — broad metadata/CTE redesign** | Remains in **`1.0.9-rc3`**, not a new milestone. Trigger: measured allocation attribution proves it necessary, or T244 changes invalidate the narrow lever. Requires a revised PLAN with preserved acceptance criteria. | Existing guards remain mandatory meanwhile. | Not authorized by this revision. |

D245-1’s experiment and D245-2’s measurement harness are prerequisites, not replacements for D245-3. A blocker report does not complete D245-3.

### Unit execution mode

**Sequential in one tree.**

Reasons:

- Production state, clone behavior, benchmark hosts, budgets and evidence admission overlap.
- Baseline and final measurements require an unchanged, exclusively owned measurement tree.
- Separate worktrees do not isolate CPU, memory or storage noise on the shared host.
- No worktree, commit, merge or push is authorized by this plan.

Read-only fact gathering may be concurrent only outside timed runs and without competing host load.

### Commands & evidence paths

Run from `/home/alex/sources/nextorm`. Create artifact directories before use. Capture command, environment, start/end time, exit code and complete output. Shell pipelines use `set -o pipefail`.

Commands for planned tooling are **planned interfaces**, implemented and tested in D245-2. They are not claims that those tools exist at PLAN time.

#### Test commands

**C-BUILD**

```text
dotnet build -c Debug
dotnet build -c Release
```

**C-CORE**

```text
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T240CommandConstructionTests|FullyQualifiedName~T240BuilderConstructionTests|FullyQualifiedName~InMemoryTests"
```

**C-SQL**

```text
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~SqlGenerationTests"
dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~SqlGenerationTests"
dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~SqlGenerationTests"
dotnet test tests/nextorm.mariadb.tests -c Debug --filter "FullyQualifiedName~SqlGenerationTests"
```

The solution-wide boundary sweep covers other provider projects. New cases must run under the existing class selectors; require nonzero discovered/executed counts.

**C-INT**

Load `.opencode/skills/running-integration-tests/SKILL.md` before execution.

```text
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

Require actual SQLite, PostgreSQL, SQL Server, MySQL and ClickHouse execution. Report the provider inventory explicitly. MariaDB dialect coverage is not represented as container-backed MariaDB coverage.

If the socket is absent, follow the skill’s recovery sequence, including:

```text
"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start
```

Wait, re-check the socket and rerun. A provider-skipped run is not passing evidence.

**C-BOUNDARY**

```text
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test -c Debug
```

Use this together with `C-BUILD`, `C-INT` and coverage for the full final boundary.

**C-PY**

```text
python -m pytest scripts/perf/tests
```

Require a nonempty test inventory, including the existing 13 `test_t240_evidence.py` tests, native-gate tests and planned T245 admission tests. Runner availability is a D245-1 prerequisite; do not accept zero collected tests.

#### Coverage

**C-COVERAGE**

Use installed repository-compatible tools:

```text
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect "dotnet test -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D245/rv1/coverage/final.cobertura.xml
reportgenerator "-reports:artifacts/pdca/D245/rv1/coverage/final.cobertura.xml" "-targetdir:artifacts/pdca/D245/rv1/coverage/final" "-reporttypes:JsonSummary;TextSummary"
```

Capture the entry baseline with the same invocation and `entry` paths. Confirm compatibility with `.github/workflows/dotnet.yml` before collection. Include only the repository’s configured coverage assemblies; do not broaden exclusions.

#### Comparison benchmarks

Unset `NEXTORM_BENCH_FULL`; use the existing ShortRun/InProcessEmit configuration.

**C-FAIR — entry round 1**

```text
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --exporters json --artifacts artifacts/pdca/D245/rv1/bdn/entry-r1
```

Repeat with these exact artifact substitutions:

- `entry-r2`
- `final-r1`
- `final-r2`

The broad verified filter deliberately retains same-class linq2db comparators. It does not expand acceptance to unrelated scenarios.

**C-CONTROLS**

```text
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeatures.Nextorm_Prepared_Except*' '*SqliteBenchmarkFeatures.Nextorm_Prepared_Intersect*' --exporters json --artifacts artifacts/pdca/D245/rv1/bdn/controls-entry
```

Repeat with `controls-final`. Keep the control dataset, configuration and normalization unchanged.

**C-DECOMPOSE**

```text
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkWarmDecompose*' --exporters json --artifacts artifacts/pdca/D245/rv1/bdn/decompose-entry
```

Repeat with `decompose-final`. New set-operation phase arms are planned; their real symbols must be recorded from the implemented benchmark inventory.

**C-NATIVE — collection**

```text
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkWarmDecompose*' '*SqliteBenchmarkCachedPlan*' '*SqliteBenchmarkFeaturePlanBuild*' --exporters json --artifacts artifacts/pdca/D245/rv1/bdn/native-final
```

**C-NATIVE — admission, planned interface**

```text
python scripts/perf/t245_evidence.py native --reports artifacts/pdca/D245/rv1/bdn/native-final --budgets scripts/perf/iteration14-budgets.json --output artifacts/pdca/D245/rv1/native-gate.json
```

This adapter must execute the existing native gate through its actual supported entry point, preserve all existing rows and strict comparison rules, and record its underlying invocation and exit. It must not substitute a T245-only approximation.

**C-COMPARE — planned interface**

```text
python scripts/perf/t245_evidence.py compare --entry-r1 artifacts/pdca/D245/rv1/bdn/entry-r1 --entry-r2 artifacts/pdca/D245/rv1/bdn/entry-r2 --final-r1 artifacts/pdca/D245/rv1/bdn/final-r1 --final-r2 artifacts/pdca/D245/rv1/bdn/final-r2 --output artifacts/pdca/D245/rv1/comparison.json
```

**C-CONTROL-CHECK — planned interface**

```text
python scripts/perf/t245_evidence.py controls --entry artifacts/pdca/D245/rv1/bdn/controls-entry --final artifacts/pdca/D245/rv1/bdn/controls-final --native artifacts/pdca/D245/rv1/native-gate.json --output artifacts/pdca/D245/rv1/controls.json
```

**C-ATTRIBUTION — planned interface**

```text
python scripts/perf/t245_evidence.py attribution --entry artifacts/pdca/D245/rv1/bdn/decompose-entry --final artifacts/pdca/D245/rv1/bdn/decompose-final --comparison artifacts/pdca/D245/rv1/comparison.json --output artifacts/pdca/D245/rv1/attribution.json
```

Phase measurements may overlap and must not be naively summed. The report must distinguish direct allocation-stack attribution, phase totals, calculated deltas and unexplained residuals.

**C-API**

Before and after editing, the scout uses:

```text
tools.roslyn({ action: "structure", solution: "/home/alex/sources/nextorm/nextorm.slnx" })
```

Discover affected type identities from that output, then use Roslyn `members` with those returned identities. Save the actual calls and complete member results in `api/entry.json` and `api/final.json`; include all affected public/protected surfaces.

Planned comparison interface:

```text
python scripts/perf/t245_evidence.py api --entry artifacts/pdca/D245/rv1/api/entry.json --final artifacts/pdca/D245/rv1/api/final.json --output artifacts/pdca/D245/rv1/api/diff.json
```

Incomplete surface exports are invalid evidence, not an empty API diff.

**C-DOCS**

```text
dotnet docfx docs/docfx.json
```

Additionally review the EN/RU source diff and run the existing table generator through its documented supported invocation, recording that invocation. Do not hand-edit calculated cells inconsistently with the generator.

**C-LINT**

```text
python scripts/validate_inner_loop.py manifest artifacts/pdca/D245/rv1/manifest.json
python scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/D245/rv1/manifest.json
```

Both must exit 0 on the final frozen tree.

### Measurement protocol

1. **Exclusive host ownership:** the collection driver grants a single measurement token covering BDN, allocation profiling and concurrent builds/test loads. T243/T244/T246/T247 cannot run BDN while T245 holds it. A recorded collection ordering/token interval is the lock evidence; separate artifact directories alone are insufficient.
2. Record host, SDK/runtime, BenchmarkDotNet configuration, environment, database fixture, entry-tree fingerprint and predecessor patch inventory.
3. Capture **two entry rounds**, then **two final rounds**, using `C-FAIR`. Run all benchmark commands serially. Keep each invocation’s comparators together.
4. Use each final report’s own `B_Linq2Db_Except` / `B_Linq2Db_Intersect` allocation. Report raw bytes, displayed KB, nextorm/linq2db ratios and means separately.
5. Allocation acceptance must hold in **both** final rounds. Do not average one failure into a pass.
6. Compare paired entry/final nextorm means per case. A ratio ≤1.20 is consistent with the agreed ShortRun non-regression band. A larger apparent speedup is reported as improvement, not concealed as noise; investigate configuration/provenance if implausible.
7. Conflicting timing rounds or invalid isolation mean insufficient evidence. Do not cherry-pick a round or silently loosen the band. Any additional confirmation must be explicitly recorded and respect the CHECK re-gather budget.
8. Prepared controls retain raw normalized allocations. Timing uses the same noise policy. Existing `Warm_Reused` gate rows must remain exactly zero.
9. New native arms must execute the same fresh-fluent, warmed-cache, materializing query shape as the fair benchmark, with **100 actual logical operations**. They must not measure only `Warm_PlanOnly`.
10. Set each new strict integer byte budget no higher than the applicable absolute cap rounded down and the smaller same-round linq2db allocation from the final reports. Preserve all existing budgets. Verify cross-host normalization and dataset equivalence before admission; do not enlarge a budget to accommodate a different workload.

**Oracle decision:** retain `t240_evidence.py` unchanged. Its overall failure remains legitimate because unrelated RecursiveCte can fail. The narrow T245 adapter admits only EXCEPT/INTERSECT by reading the canonical per-scenario/raw-report data and validating the same numerator/denominator pairing. It does not reinterpret the T240 overall exit as success and does not add a scenario-selector feature to that tool.

The adapter’s exit contract is: 0 = all requested applicable checks met; 1 = measured violation; 2 = missing, ambiguous or invalid evidence. Unit tests must exercise each exit class.

### Test strategy

Use unit tests for command-state isolation, preparation/reset invariants, metadata aliases and in-memory semantics. Use exact SQL snapshots for dialect/rendering invariants. Use container-backed integration for actual provider results and duplicate semantics.

#### Variant matrix

| Axis | Rows | Closure |
|---|---|---|
| Set-operation kind | EXCEPT, INTERSECT | **Test:** fair benchmarks, native arms, SQL snapshots, in-memory and integration results. |
| Other kinds sharing the implementation | UNION, UNION ALL | **Test:** existing in-memory cases `InMemoryTests.cs:891,900`, integration scalar/entity cases `CommonTestSuite.SqlCommand.cs:795,813`; duplicate assertions. |
| ALL variants | INTERSECT ALL, EXCEPT ALL | **Test:** existing cases at `InMemoryTests.cs:927,936` and integration `CommonTestSuite.SetOperations.cs:39,51`; add explicit multiplicity assertions. |
| Operand topology | Two operands; `a.Union(b).Except(c)`; reusable operands | **Test:** existing chained cases and `T240CommandConstructionTests.cs:196`; add equivalent INTERSECT sharing coverage in the existing class. |
| Execution state | Fresh-fluent warm cache; prepared command | **Test:** final fair rounds and prepared controls. |
| Cache opt-out | Existing `_dontCache` behavior | **Guard + test:** preserve opt-out and call-local preparation behavior; no sticky command mutation to obtain the optimization. Exercise uncached set-operation behavior and subsequent context reuse. |
| Metadata | `_joins`, `_sorting`, `_arrayJoins`, `_windows`, `_ctes`: null/empty/non-empty | **Test + guard:** benchmark’s verified null path; explicit empty and non-empty metadata states through supported builders/internal test access; preserve distinctions and aliases. No manufactured invalid production query states. |
| Composition | After JoinInto; after CTE; with ORDER BY | **Test:** baseline/final SQL, values and operand reuse in existing test classes. CTE recipe is guarded, not retuned. |
| Executor | SQL; in-memory | **Test:** provider snapshots/integration and `InMemoryTests`; preserve in-memory sequence independently of SQL ordering. |
| Capability | `SupportsIntersectExceptAll = true` | **Test:** supported ALL SQL and multiplicities; preserve the provider’s existing flag value. |
| Capability | `SupportsIntersectExceptAll = false` | **Test:** existing unsupported exception cases, including SQLite/SQL Server snapshot tests and integration `:63,77`. |
| Providers | SQLite, PostgreSQL, SQL Server, MariaDB | **Test:** named SQL-generation projects. Container coverage for the configured integration providers is additionally mandatory. |
| Other configured providers | MySQL, ClickHouse | **Test:** solution boundary and container suite; use actual provider flags, not assumed ALL support. |
| Element shape | Value scalar benchmark; reference/entity projections | **Test:** scalar benchmark and integration entity/set-operation cases. |
| Values | Duplicate, null and default element values | **Test:** add explicit result/multiplicity cases through existing test classes, including nullable/reference projections where supported. |
| Invalid input | Null operand/default command arguments where callable | **Guard + test:** capture existing entry exception type and timing and preserve them; no new public validation contract. |
| Broader nonempty-metadata optimization | Allocation tuning unrelated to measured set-operation excess | **Deferred in current milestone:** trigger is attribution identifying it as necessary. Current semantic guards remain mandatory. |

Snapshot expectations at the verified PostgreSQL/SQLite/SQL Server/MariaDB locations must not be rewritten to bless a changed renderer.

#### Boundary scope and count

- Every production unit: `C-CORE` and `C-SQL`.
- Final boundary: **one complete sweep** comprising `C-BUILD`, `C-BOUNDARY`, `C-INT`, `C-COVERAGE`, `C-PY` and `C-NATIVE`.
- Full integration project: `tests/nextorm.integration.tests`.
- Any source change invalidating a boundary result requires another affected sweep; the count is then recorded explicitly.
- No benchmark concurrency during test/build/coverage work.

#### Coverage policy

The configured included assemblies remain `nextorm.{core,sqlite,postgres,sqlserver}`. Record line and branch percentages against entry evidence. The 85%/75% thresholds are hard on `main` and warnings on `1.0.9-rc3`; warnings and any deterioration remain visible. Missing coverage is not a passing result.

### Priority matrix

No project class-priority table was supplied. Accordingly, acceptance invariants and affected execution-path evidence are P1 by construction.

| Contract rows | Priority | Reason |
|---|---|---|
| E245-01–E245-03 | P1 | User’s allocation/time targets. |
| E245-04–E245-08 | P1 | SQL, semantics, public API and operand immutability. |
| E245-09–E245-13 | P1 | Protected controls, native enforcement, attribution and frozen recipe. |
| E245-14–E245-16 | P1 | Final-tree regression evidence, published claim accuracy and canonical admission. |

CHECK applies these priorities and must not downgrade a row to obtain acceptance.

### Dependency requirements

These are collection-planning constraints, **not a DAG or group assignment**.

| Task | Inputs that must be settled if it precedes T245 | Outputs T245 provides |
|---|---|---|
| T243 | `EntityBuilderExtensions.AggregateCore` / `SelectParent` behavior; any shared benchmark, native-gate, budget and comparison-table edits. Record any change affecting command construction or fixture/setup. | Stable set-operation benchmark/gate entries and their byte normalization; no ownership claim over aggregate methods. |
| T244 | Settled clone/preparation/CTE behavior, specifically edits to `QueryCommand.Clone.cs`, `QueryCommand.QueryPreparer.cs`, `CteHoister` and related benchmark/gate rows. Identify non-frozen regions and preserve frozen recipe semantics. | Set-operation alias/state guards, measured fresh-fluent reductions and unchanged CTE-composition behavior. |
| T246 | Settled shared executor/materialization or fixture changes, if any; final native-gate/budget entries. A raw-reader-only change outside this path is not a semantic prerequisite. | Materializing set-operation controls and independent benchmark rows. |
| T247 | Settled shared preparation changes, if any, and benchmark/gate edits. JSON-only `PrepareJsonStream`/`JsonShapePlan`/`JsonStreamWriter` behavior is otherwise outside this task. | Confirmation that the SQL materializing path did not change JSON behavior or public API. |

For any predecessor:

- Integrate its approved output before the T245 entry rounds.
- Record its patch/tree fingerprint. `01815f3a` remains the declared base; the actual measurement entry tree also includes explicitly identified predecessor outputs.
- Do not integrate patches or change shared source between entry and final measurement without invalidating the affected evidence.
- Preserve `957fcfe7` as the collection’s historical baseline, not a substitute for the measured T245 entry tree.

For any successor:

- It may rely on the admitted T245 gate rows, guards and per-case evidence.
- If it changes the shared preparation/construction path or benchmark workload, it must revalidate the affected T245 rows.
- An unrelated successor still requires the same exclusive measurement token.

**Ordering recommendation:** settle T244 before T245’s optimization decision and baseline. Both own clone/preparation territory, and T244 can change the allocation attribution. If the collection chooses the reverse order, T244 must explicitly preserve/revalidate T245’s affected evidence.

#### Shared-footprint inventory

| Path | T245 ownership / sharing rule |
|---|---|
| `src/nextorm.core/Query/QueryCommand.Clone.cs` | Conditional T245 edit; shared with T244. One owner at a time, resolved contract before edits. |
| `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` | Conditional non-frozen allocation edits only; shared with T244. Frozen recipe behavior remains protected. |
| Six set-operation bodies and `QueryCommand.cs:1171-1181` | T245 semantic responsibility; shared-tail guard cannot be removed or weakened. |
| Frozen plan/equality/store files | Read-only for T245; inspect changes from predecessors for compliance. |
| `benchmarks/nextorm.benchmark/**` | T245 owns its set-operation arms; preserve other tasks’ fixtures/configuration and arms. |
| `scripts/perf/iteration14-budgets.json` and native gate | Shared serialized edits; T245 adds its two rows, never relaxes existing rows. |
| `scripts/perf/t245_evidence.py` and tests | T245-owned planned files; no task-wide replacement of existing evidence tools. |
| Comparison table EN/RU and table generator | Shared serialization; T245 changes only its rows and necessary generated content. |
| `docs/specs/performance/**` | Shared serialization; retain other tasks’ conclusions and residual IDs. |
| `docs/specs/status/D245-warm-set-ops-1.md` and D245 artifacts | T245-owned; status excluded from the frozen source tree per canonical rules. |

### Docs plan

Update:

- `docs/comparisons/feature-benchmarks-2026-10-17.md:86-95`, specifically EXCEPT/INTERSECT.
- `docs/ru/comparisons/feature-benchmarks-2026-10-17.md:147` and matching explanatory prose.
- `docs/specs/performance/feature-benchmark-followups-2026-10-17.md:15-16,22-25`.
- `docs/specs/performance/benchmark-report.md` with isolated rounds, per-case attribution, controls and final gate results.
- D245 status and evidence contract.
- #240 residual evidence under its **existing IDs**, as required by `D240-construction-allocation-1.md:330`, without erasing historical failures or changing the frozen recipe/1.20 threshold.

Do not change public API documentation, guides or samples unless an actual unchanged-behavior explanation requires it. No public API change is expected or authorized. Inspect CS1591 enforcement; preserve it, and document any changed internal implementation where useful.

Public documentation must not link to `docs/specs/**`. EN/RU tables must agree and cite publishable benchmark context rather than internal status links.

### Perf decision

**Required.** This is a per-query allocation optimization, not one-time work.

Evidence locations:

- Fresh command construction and preparation: `SqliteBenchmarkFeaturesFairCached.cs:43-49`.
- Recurring set-operation preparation: `QueryPreparer.cs:115,152-164`.
- Warm full preparation/cache lookup: `QueryPlanner.cs:609-654,836-871`.

Baseline is two entry rounds on the actual integrated entry tree. Final acceptance requires two isolated final rounds, prepared controls, existing zero-allocation controls, attribution and strict native-gate enforcement.

### Recon decision

**Required, bounded to D245-1.**

The experiment must prove:

1. Which objects allocate on each set-operation path.
2. Whether construction, recursive preparation, hash/equality or materialization dominates the excess.
3. That the proposed removable allocations total enough to reach both caps.
4. That removal is possible without entering frozen recipe/cache-key territory.
5. That the new native workload is equivalent after 100-operation normalization.

Observable output: per-case phase B/op, allocation-stack/object attribution, baseline SQL/results, control values and a written lever decision.

Failure to identify a sufficient permitted lever is a return-to-PLAN condition, not authorization for speculative optimization.

### Toolchain & lenses

- .NET 10; production/tests `net10.0`.
- Warnings are errors in Debug and Release.
- Roslyn first for every C# symbol/reference/impact question.
- Preserve CRLF in changed tracked files, including this status file.
- Central Package Management applies if a dependency becomes necessary; no new dependency is preapproved merely for profiling.
- Review lenses: allocation lifetime, clone/alias ownership, preparation state, cache invariants, provider capabilities, SQL/results, benchmark normalization and evidence provenance.

### Design checklist

| Severity / location | Finding | Disposition and one-line action | Applicable invariant |
|---|---|---|---|
| P1 — `QueryCommand.cs:1171-1181` | Shared-tail mutation would corrupt reusable operands. | **Fix now guard:** retain clone-before-`SetOperation`; expand regression coverage. | #240 immutable shared-tail behavior. |
| P1 — `QueryPlanner.cs:609-654`; `QueryPreparer.cs:3906` | Set operations deliberately miss the raw-shape shortcut. | **Fix now guard:** do not change eligibility to obtain parity. | Frozen D201-3/#240 recipe; no cache-key semantic changes. |
| P1 — `QueryCommand.cs:96,197`; `QueryCommand.Clone.cs:35-36` | Preparation flags have deliberate copy/reset asymmetry. | **Fix now:** test state transitions before reusing/skipping any work. | Preparation correctness and cache safety. |
| P1 — `QueryCommand.cs:1082-1122` | Reset clears `_from`, stability and plan-key state. | **Fix now:** preserve required reset effects; remove only measured redundant allocations. | Cache/preparation invariants. |
| P1 — `iteration14_gate.py:314-318,399` | Fair benchmark’s 10 iterations cannot host native-gate rows. | **Fix now:** use equivalent 100-operation arms and strict budgets. | Native gate normalization and enforcement. |
| P1 — `t240_evidence.py`; `D240…:247-256,330` | Overall T240 failure and residual obligations cannot be hidden. | **Fix now:** per-scenario T245 admission; reopen affected existing residual IDs. | Honest evidence; frozen residual threshold. |
| P1 — `CommonTestSuite.SetOperations.cs:39,51`; `InMemoryTests.cs:927,936` | ALL multiplicity and ordering coverage is incomplete. | **Fix now:** add baseline/final multiplicity and in-memory ordering assertions. | Unchanged duplicates/order semantics. |
| P2 — `QueryCommand.cs:331,352,400`; `EntityBuilder.cs:40,47,52` | Empty metadata allocation hypothesis does not explain this null-shaped benchmark. | **Guard now:** reject it as attribution. **Defer** unrelated empty-metadata tuning in current milestone; trigger is measured relevance. | Minimal measured optimization. |
| P1 — shared `Clone.cs` / `QueryPreparer.cs` footprint | T244 can invalidate the selected lever and baseline. | **Fix now prerequisite:** settle ownership and entry-tree outputs before measuring. | Collection isolation and source ownership. |
| P1 — benchmark isolation at `collection…:30` | Concurrent BDN invalidates results. | **Fix now prerequisite:** exclusive collection measurement token and interval log. | Reproducible performance evidence. |

### Backup & rollback

- Record the entry-tree fingerprint and a T245-scoped patch before edits; preserve unrelated user/predecessor changes.
- Keep entry reports immutable.
- Roll back T245-scoped changes with patches, not repository-wide reset, commits or merges.
- Retain failed experiments and reports as evidence; do not overwrite passing or failing round directories.
- If the chosen lever cannot meet the caps within the constraints, keep D245-3 active/blocked and return to PLAN.
- Do not raise budgets, change operation counts or weaken acceptance to make rollback unnecessary.

### Risks

- The required savings may lie primarily in preparation paths constrained by the frozen recipe.
- A fresh-fluent improvement may change state reuse, aliasing or CTE behavior.
- The 100-operation gate fixture may expose different allocation totals if it does not exactly reproduce the fair benchmark workload.
- ShortRun timing can be inconclusive even when allocation is stable.
- T244 integration can invalidate attribution; other collection changes can invalidate shared benchmark evidence.
- New multiplicity/state tests may expose pre-existing defects. Such defects are not automatically in scope.
- Tool or container availability can require an additive prerequisite.

### DO → PLAN classification

No DO blocker has yet been reported.

On return:

- **Missing local setup, fixture or permitted tooling:** additive prerequisite; original D unit remains active and blocked, with all criteria and remaining work preserved.
- **Sufficient measured narrow lever:** proceed under this plan without resetting revision/attempt counters.
- **Measured need for different tasks/dependencies or broader implementation:** create a new `P:` task; revise `r` only when actions actually change.
- **Missing report or ambiguous attribution:** targeted scout/re-gather first; absence alone does not justify DO or contract revision.
- **Persistent low confidence after targeted investigation:** recommend that the orchestrator call `escalate`, trigger 5, even without proof of an external blocker.
- **True blocker outside authority/resources:** recommend orchestrator escalation.
- A claim that implementation is finished never marks a D unit done without CHECK admission.

### Versioned evidence contract — rv=1

All rows below are **planned**, initially `open`. Every row is mandatory. Predicates specify valid applicability and evidence conditions; they do not permit dropping a required criterion.

Contract identity:

- `task`: T245
- `issue`: #245
- `status_file`: `docs/specs/status/D245-warm-set-ops-1.md`
- `base_ref`: `01815f3a`
- `contract_rv`: 1
- `supersedes`: none
- `required_rows`: E245-01 through E245-16, exact two-way coverage
- Footprint: admitted T245 source/test/benchmark/tool/docs changes listed above, with actual files recorded before freeze.
- Exclusions: canonical `artifacts/**` and `docs/specs/status/**`; no extra source exclusion.
- Each manifest row carries its stable requirement ID, row ID, `rv`, predicate, owner, command, expected result, evidence sources and artifact paths.
- Every `met` row has nonempty `{kind,path[,exit]}` evidence; every `open` row has a reason; `na` requires an observed predicate and cannot waive an unconditional requirement.

| Row / requirement / rv | Required scenario and planned evidence sources | Exact command / invocation and expected result | Expected artifacts | Owner / observable applicability predicate |
|---|---|---|---|---|
| **E245-01 / R245-01 / 1** | EXCEPT raw BDN allocations and same-round linq2db denominators in both final rounds. | `C-FAIR` entry/final invocations, then `C-COMPARE`; benchmark exits 0; admission exits 0; each EXCEPT ratio ≤1.00 and allocation ≤5171.2 B/op. | `bdn/entry-r1/**`, `entry-r2/**`, `final-r1/**`, `final-r2/**`; `comparison.json`, EXCEPT record and logs. | **perf** / Always required; each final round must contain exactly one valid arm pair with matching job/configuration. Missing pair is invalid. |
| **E245-02 / R245-02 / 1** | INTERSECT raw reports and same-round denominators. | Same exact `C-FAIR`/`C-COMPARE` calls; admission exits 0; each INTERSECT ratio ≤1.00 and allocation ≤5130.24 B/op. | Same raw reports; separate INTERSECT record in `comparison.json`. | **perf** / Always required; valid distinct arm pair in each final round. |
| **E245-03 / R245-03 / 1** | Per-case timing, isolation intervals, host/toolchain and entry/final tree metadata. | `C-COMPARE` exits 0; both cases satisfy the stated two-round +20% non-regression rule; no concurrent benchmark/build interval. | `comparison.json`, `measurement-environment.json`, `measurement-lock.log`, command logs. | **perf + collection driver** / Always required; timestamps and host identity are observable. Invalid isolation prevents admission. |
| **E245-04 / R245-04 / 1** | Existing exact SQL assertions plus entry/final boundary renderings. | `C-SQL` and `C-CORE` exit 0 with executed tests; expected snapshot strings unchanged. | `tests/sql-*.log`, `tests/core.log`, `semantics/sql-entry.json`, `sql-final.json`, reviewed snapshot diff. | **tests** / Always required; all touched shared operation branches and planned composition cases must be represented. |
| **E245-05 / R245-05 / 1** | SQL multisets, explicit-order behavior and in-memory sequences. | `C-CORE` and `C-INT` exit 0; entry/final recorded values agree under the correct sequence/multiset comparison. | `tests/core.log`, `tests/integration.log`, `semantics/results-entry.json`, `results-final.json`. | **tests** / Always required; SQL sequence equality applies only to explicit ordering; in-memory existing sequence behavior remains required. |
| **E245-06 / R245-06 / 1** | Duplicate-rich ALL/non-ALL cases and provider unsupported cases. | `C-CORE`, `C-SQL`, `C-INT` exit 0; explicit multiplicities and existing exceptions pass. | `semantics/multiplicity.json`, provider capability inventory, test logs. | **tests** / Always required; supported providers execute results; unsupported providers execute rejection tests according to observed flag values. |
| **E245-07 / R245-07 / 1** | Complete Roslyn public/protected surface before/after. | `C-API` tool calls and planned API comparison invocation; API comparator exits 0 with zero differences and complete exports. | `api/entry.json`, `api/final.json`, `api/diff.json`, actual Roslyn call log. | **scout + review** / Always required; affected types are discovered semantically, not guessed. |
| **E245-08 / R245-08 / 1** | Existing #240 regression and new operand-reuse cases in existing classes. | `C-CORE` exits 0; `NestedSetOperations_KeepOperandsUnchanged` is present/executed; sibling builder-sharing cases pass. | `tests/core.log`, operand SQL/state records and source review. | **tests** / Always required; shared-tail implementation is on the affected path even if its lines are unchanged. |
| **E245-09 / R245-09 / 1** | Prepared EXCEPT/INTERSECT raw control reports. | `C-CONTROLS` entry/final and `C-CONTROL-CHECK`; exits 0; raw B/op unchanged and timing meets the stated noise rule. | `bdn/controls-entry/**`, `controls-final/**`, `controls.json`. | **perf** / Always required; both prepared methods must be present with unchanged fixture/normalization. |
| **E245-10 / R245-10 / 1** | All existing Cte/RecursiveCte/Join4/InAtIn zero-allocation control rows. | `C-NATIVE` and `C-CONTROL-CHECK` exit 0; all pre-existing `Warm_Reused` rows present and exactly 0 B/op. | `bdn/native-final/**`, `native-gate.json`, `controls.json`. | **perf/gate** / Always required; compare existing row inventory against the entry manifest. |
| **E245-11 / R245-11 / 1** | Two actual fresh-fluent gate rows; normalization and fail-closed negative fixtures. | `C-PY` and both `C-NATIVE` calls exit 0; 100 operations verified; over-budget/wrong-count/missing-row fixtures are rejected by their expected nonzero paths. | `tests/perf-tools.log`, `native-gate.json`, actual gate invocation log, reviewed budget diff. | **gate** / Always required; actual row symbols bind after implementation. No existing row may be removed. |
| **E245-12 / R245-12 / 1** | Per-case phase measurements, stack attribution and final reconciliation. | `C-DECOMPOSE` entry/final and `C-ATTRIBUTION` exit 0; two separate cases, sufficient attributed savings, no double-counted phase totals or hidden residual. | `bdn/decompose-entry/**`, `decompose-final/**`, `attribution.json`, profiler invocation/output, `lever-decision.md`. | **perf + review** / Always required; observed allocation stacks determine the lever. An unproven attribution remains open. |
| **E245-13 / R245-13 / 1** | Frozen-file/recipe review, state invariants and #240 residual continuity. | `git diff 01815f3a -- src/nextorm.core docs/specs/status/D240-construction-allocation-1.md` reviewed with Roslyn impact evidence; `C-CORE`, `C-SQL` pass. Expected: no forbidden recipe/key/equality changes; existing residual IDs and 1.20 threshold retained. | `review/frozen-recipe.md`, source diff, Roslyn impact log, residual evidence cross-reference. | **review** / Always required; predecessor changes are separately identified, not silently attributed to T245. |
| **E245-14 / R245-14 / 1** | Final build/test boundary, provider inventory and coverage. | `C-BUILD`, `C-BOUNDARY`, `C-INT`, `C-COVERAGE`, `C-PY` exit 0; no required provider skipped; coverage percentages and branch policy explicitly reported. | `tests/build-*.log`, `boundary.log`, `integration.log`, `coverage/**`, boundary-run inventory. | **tests/check** / Always required; all evidence must match the final frozen source tree. |
| **E245-15 / R245-15 / 1** | EN/RU tables, registry and internal performance record tied to per-case evidence. | `C-DOCS` exits 0; generator’s recorded supported invocation exits 0; `git diff -- docs/comparisons docs/ru/comparisons docs/specs/performance` reviewed for matching values and no forbidden public spec links. | `docs/docfx.log`, generator command log, `docs/reconciliation.md`, documentation diff. | **docs/review** / Always required; publishing no success claim is insufficient when final task acceptance is claimed. |
| **E245-16 / R245-16 / 1** | Canonical contract, exact row coverage, frozen tree and complete CHECK inputs. | Both `C-LINT` commands exit 0; all E245-01…16 applicable rows met; no unaccounted source change after freeze. | `contract.json`, `manifest.json`, `validation/manifest.log`, `validation/lint.log`, CHECK verdict. | **check** / Always required; missing/stale evidence is open/invalid, never automatically `na`. |

All artifact paths in this table are relative to `artifacts/pdca/D245/rv1/` unless written in full.

**Revision rule:** rv=1 has no predecessor. A justified future rv must explicitly supersede rv=1, retain existing E/R IDs and obligations, and add IDs for genuinely new variants. Missing reports alone justify neither supersession nor a DO iteration.

### Freeze & CHECK

Before DO admission, coder writes this complete plan and rv=1 contract to disk with CRLF. Initial rows remain `open` with planned sources.

At the final boundary:

1. Record actual changed-file footprint and inherited predecessor outputs.
2. Produce the canonical frozen-tree manifest using the project’s existing evidence workflow.
3. Admit only final-tree evidence; run `C-LINT`.
4. CHECK applies every P1 row and verifies provider execution, benchmark isolation, controls and frozen constraints.
5. **CHECK re-gather budget: 2**, owned by **check**. Each use names the row, missing evidence, exact targeted invocation and result. It may recover/revalidate evidence, not edit implementation or weaken criteria.
6. After budget exhaustion, unresolved evidence returns to PLAN; persistent low confidence recommends escalation trigger 5.
7. An artifact-only re-gather does not revise `r`, reset `n`, or complete an active D unit.

### Confidence & next gate

**High confidence:** verified path, null metadata shape, #240 guard, benchmark comparators, native-gate normalization and frozen constraints.

**Conditional confidence:** the permitted lever can remove enough allocation. That is intentionally unresolved until D245-1 produces observable attribution.

**Next gate:** collection PLAN admission. Coder persists the plan/contract; the autonomous driver verifies gate-1 completeness, collection ownership and measurement prerequisites, then hands off to DO without a user-approval wait. This collection planning task itself stops at PLAN→DO.

## Progress log

- PLAN ready (collection handoff).

## Collection handoff

- **selected_variant:** `pdca-dotnet` with `nextorm-pdca`.
- **N/r/n:** `1/1/1`; **contract rv:** `1`.
- **Footprint:** conditional set-operation construction/clone/non-frozen preparation changes; semantic tests; set-operation decomposition/native arms; native budgets; scoped evidence adapter/tests; EN/RU comparison and internal performance records.
- **Other tasks also need:** `QueryCommand.Clone.cs`, `QueryCommand.QueryPreparer.cs`, `benchmarks/nextorm.benchmark/**`, `scripts/perf/**`, native budgets, comparison EN/RU and `docs/specs/performance/**`.
- **Dependency requirements:** settle and fingerprint any predecessor’s shared-path changes before entry measurement; preserve frozen recipe semantics; successors touching the same path revalidate affected T245 rows.
- **Measurement isolation:** one exclusive host measurement token across T243–T247; no concurrent BDN, profiling, builds or test load.
- **T244 recommendation:** settle T244’s clone/preparation output before T245 attribution and optimization. Collection planner assigns ordering/groups after the ALL-barrier; this plan creates no DAG.

## Lane disposition (2026-10-17)

Not executed — lane G1 stopped: predecessor T246 reached a terminal `incomplete` (measurement-protocol GAP).
Task status: `incomplete` («группа остановлена»). Plan retained; nothing executed, nothing superseded.
