# D207 — T207 uniform cancellation handling during projection build — cycle status

task: T207
issue: #207 — https://github.com/AlexeyShirshov/nextorm/issues/207
issue_title: Uniform cancellation handling during projection build
milestone: 1.0.9-rc3
state: OPEN
labels: none
collection: 1.0.9-rc3-open
collection_status_path: docs/specs/status/collection-1.0.9-rc3-open.md
selected_variant: pdca-dotnet
cycle_id: N=1
plan_revision: r=1
attempt: n=1
plan_state: ready
evidence_contract_rv: 1
spec_version: T207-plan-v1
baseline_ref: c7ff89ca8ae339c920cc6cb0c6b6ef4558aa6481  (short c7ff89ca)
branch: 1.0.9-rc3
worktree: /home/alex/sources/nextorm
canonical_status_path: docs/specs/status/D207-projection-cancellation-1.md
evidence_dir: artifacts/pdca/D207-projection-cancellation/rv1/
evidence_contract_manifest: artifacts/pdca/D207-projection-cancellation/rv1/manifest.json
execution_mode: single-group (current worktree; no worktree/group-branch/task-branch, no merge, never push; autonomous + auto-commit authorised)

## PLAN (r=1)

**Plan revision:** r=1, first DO attempt n=1
**Evidence contract:** rv=1 (planned, not observed)

### Goal (essence, not issue wording)

An observed cancellation during projection preparation must surface `OperationCanceledException` (OCE), never a partial result or a masked query-shape error. Completion of this PLAN — not a new revision; no prior contract was frozen, so no supersession.

### Acceptance criteria (observable + negative)

- **AC207-A** — `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:653–654`: cancelled NewExpression throws OCE; **not** an empty select list.
- **AC207-B** — `QueryCommand.QueryPreparer.cs:758–759`: cancelled MemberInit throws OCE; **not** a `[NULL,…]` column array.
- **AC207-C** — `QueryCommand.QueryPreparer.cs:1628–1629` + `:939`: cancelled bare-join preparation throws OCE; **not** `QueryPreparationException("Select must return new anonymous type")`.
- **AC207-D** — `src/nextorm.core/DataContext/EntitySelectListBuilder.cs:30–31`, `QueryCommand.QueryPreparer.cs:869–871,1829`: cancellation never degrades into an incomplete expansion or a structural-refusal fallback; the caller receives OCE.
- **AC207-E** — `QueryCommand.QueryPreparer.cs:1825`: the legitimate refusal of an unsupported shape is preserved; without cancellation **no** OCE appears.
- **AC207-F** — `QueryCommand.QueryPreparer.cs:587–593,1164,1189`: existing direct-entity/JSON checks are preserved; cancellation is not masked by another error.
- **AC207-G** — every listed shape without cancellation keeps its SQL, column order/count and result; no missing or new null slots.
- **AC207-H** — shared-builder consumers `src/nextorm.core/DataContext/SqlBuilder.cs:964,1254` and `RawMapperFactory.cs:641–642` keep complete results without cancellation; no cache-state/API change.
- **AC207-P** — performance: the existing cached-vs-prepared ratio is measured comparably to baseline `1.87`; a value `>2.244` blocks acceptance pending investigation; the baseline is never rewritten.

### Minimal solution

Replace the cancellation-driven partial returns/refusals with `ThrowIfCancellationRequested`, and remove the now-unreachable incomplete-result handling at the consumers. Constraints: keep the shared builder's tuple signature, the legitimate structural refusal, the public API, sticky-cache invariants and the existing correct checks. One cancellation policy in the shared builder plus checks in the three projection loops; no token threading, no general refactor.

Rejected alternatives: (a) translating incomplete results in each consumer — duplicates the cancellation policy and leaves other consumers vulnerable; (b) new token-bearing/materializer-threading APIs — disproportionate scope and API risk, outside T207.

### Scope / gaps

- **Fix now:** AC207-A…H.
- **Active split unit** (separate, milestone **1.0.9-rc3**, invariant 8): sorting/grouping cancellation at `QueryCommand.QueryPreparer.cs:2722–2723,3495–3496,3536–3537`. Trigger = **this cycle's scope handoff**. In DO: first search open/closed duplicates (cancellation + sorting/grouping + partial-return cause), then create/update the tracking issue with milestone `1.0.9-rc3`, record the verified URL and owner. It must not be counted as covered by T207.
- **`RowMaterializerBuilder` — decision (b), token threading excluded.** `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:41–70` builds a complete array; no cancellation-driven partial-return exists. Auditable callers: `InMemoryRowMaterializer.GetMap<TResult,TEntity>` → `src/nextorm.core/DataContext/InMemoryRowMaterializer.cs:51,73`; private `RowMapperFactory.Build<TResult>` → `src/nextorm.core/DataContext/RowMapperFactory.cs:348`. All tokenless; the non-existent symbol `BuildMaterializer` must not appear in the plan. Deferred trigger: a separate materialization-cancellation requirement or a demonstrated silent partial result; any resulting unit stays in **1.0.9-rc3**.
- Strict "cancel after element k>0" is excluded from the test oracle — no deterministic seam exists. This does not license a partial return: the in-loop checks remain a guard obligation; the oracle is the reproducible pre-cancelled cancellation. (Overlay invariant 11.)
- XML-doc mode is not yet established: a targeted scout in D207.0 confirms `GenerateDocumentationFile`/CS1591; no public symbols are added. Do not invent requirements.
- No predecessor outputs are needed (the red arm is already obtained). No external product decision is pending; perf-infrastructure availability is checked in D207.0 and its unavailability must not be turned into passing evidence.

### Units / WRITE footprint

One current tree; streams `code`, `tests`, `docs/evidence`; sequential because the test/artifact footprint is shared; no branches, worktrees, commits, merge, push.

- **D207.0** — record the status/contract; preserve the recon provenance; confirm the benchmark CLI, XML-doc/tool prerequisites and tracked provider-test projects via scout.
- **D207.1** — add the tests and their expected baseline-red results before touching production (the new symbols below are **planned**, not existing evidence).
- **D207.2** — fix the loops `QueryCommand.QueryPreparer.cs:653–654,758–759,1628–1629`; the shared builder `EntitySelectListBuilder.cs:30–31`; the consumers `:869–871,1829`; preserve `:1825`.
- **D207.3** — functional/coverage/mutation/perf runs, variant mapping, branch delta and artifacts without changing the baseline.
- **D207.4** — the scope handoff and sorting/grouping tracking; update the internal record; hand to CHECK; do not self-declare the units done.

**Production WRITE:** `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/DataContext/EntitySelectListBuilder.cs`.
**Tests WRITE:** `tests/nextorm.sqlite.tests/EntityItemProjectionTests.cs`, new `tests/nextorm.core.tests/EntitySelectListBuilderCancellationTests.cs`.
**Supporting WRITE:** `docs/specs/design/code-smells-review.md`, `artifacts/pdca/D207-projection-cancellation/status.md`, dir `artifacts/pdca/D207-projection-cancellation/rv1/`.
**NOT changed:** the benchmark sources/config/baseline, the materializer, public docs. Any other tracked WRITE returns a concrete scope delta to PLAN.

### Variant matrix (all rows closed)

| Variant | Closure |
|---|---|
| Whole-entity direct `:587–593` | **Test:** pre-cancelled → OCE; active token and `None` keep the result; the existing regression also runs. |
| NewExpression ctor `:653–654` | **Test:** TARGET A red→green; negative — empty select list. |
| MemberInit `:758–759` | **Test:** TARGET B red→green; negative — null-filled columns. |
| Bare join / `TryBuildProjectionSelectList` `:1628–1629,939` | **Test:** TARGET C red→green; negative — masking `QueryPreparationException`. |
| Nested projection | **Test:** nested ctor/member-init, cancelled and happy path; **guard:** a dominating loop check may precede the inner one. |
| `TryExpandEntityItem` `:1825`/`:1829` | **Test:** legitimate structural refusal without cancellation; **guard + direct-builder test:** cancellation is never turned into `false`/refusal. |
| JSON `:1164,1189` | **Test:** both JSON forms, cancelled/happy; **guard:** the existing inner checks are not deleted even if an earlier one fires. |
| `EntitySelectListBuilder.Build` direct | **Test:** metadata from `EntityMetadataBuilder<T>.Build()`, nonempty properties; pre-cancelled → OCE; active token/`None` → complete ordered array. |
| Value/reference/null/default | **Test:** scalar value, nullable value, reference, null/default projected values and an unmatched left-join entity; a null value ≠ a missing `SelectExpression`. |
| Flags | **Test:** pre-cancelled/active/`None` × `dontCalculateHash=true/false`; **guard:** never mutate `command.Cache`/`_dontCache`. |
| Shared consumers | **Test:** nonempty entity expansion, SQL/subquery consumers and the raw-mapping path with `None`; existing suites + planned witness mapping. |
| `RowMaterializerBuilder` | **Deferred:** cancellation threading per the stated trigger; existing `RowMaterializerBuilderTests` run in the core suite. |
| Sorting/grouping | **Deferred in T207, active follow-up:** scope handoff, milestone 1.0.9-rc3; not counted as T207 coverage. |

### Test strategy

Red already executed (see Reconnaissance below): HEAD `c7ff89ca…`; recon build exit 0; probe exit 2, `4 total / 3 failed / 1 passed / 0 skipped`. Sources `/tmp/nextorm/D207-build.log`, `/tmp/nextorm/D207-recon-red.log`, `/tmp/nextorm/D207-recon-probe-details.txt` (outside the repo). The throwaway probe source was deleted and must not be presented as a current test. Green is produced in DO. No further production spike is needed; the defect reconnaissance is closed.

- Planned SQLite methods: `T207_PreCancelledProjection_ThrowsOperationCanceled`, `T207_NonCancelledProjection_PreservesSqlAndColumns`, `T207_UnsupportedEntityExpansion_PreservesStructuralRefusal`.
- Planned core methods: `T207_Build_PreCancelled_ThrowsOperationCanceled`, `T207_Build_NotCancelled_ReturnsCompleteColumns`, `T207_Build_NonCancellable_ReturnsCompleteColumns`.
- Theory case IDs must appear in the output; variants and witness assertions are bound to evidence rows; a single blanket "tests passed" is not enough. Happy-path SQL/column assertions are frozen **before** the production fix; supported and unsupported shapes are distinguished, never retro-fitted to the new result.
- Required: the full `tests/nextorm.core.tests`, `tests/nextorm.sqlite.tests`, and every tracked provider SQL-generation project discovered; the integration project is excluded from this set. No container-backed run is required (provider-independent preparation; SQLite reaches the defect; provider suites check the SQL consumers). Core/SQLite need no `DOCKER_HOST`. If SQL Server infra is needed for perf, load the `running-integration-tests` skill and raise Podman per its troubleshooting; a skipped/unexecuted case is not a success.
- Coverage: CI reference **line ≥85%, branch ≥75%**; on an rc branch breaches are recorded as CI warnings, never presented as a met threshold. A branch delta over the changed lines is mandatory.
- Mutation: a separate `artifacts/pdca/D207-projection-cancellation/rv1/mutation/stryker-config.json` scoped to the touched core types/changed spans; the repo-root config (postgres `PostgresExtremeRowRenderer`) must not be modified. Precedent: `artifacts/pdca/rc2-190/mutation/`. Unexplained/no-coverage mutants on P1 require a witness; equivalence caused by dominating guards is proven, not assumed.

### DO brief test scope

Recorded as given; the orchestrator must normalise `rebuild` to the validator enum `affected|none` before dispatch.

```json
{"D207.0":{"projects":[],"selectors":[],"files":["artifacts/pdca/D207-projection-cancellation/rv1/"],"rebuild":"none","boundary":"metadata/tooling only","rationale":"freeze contract and preserve executed red evidence"},"D207.1":{"projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests"],"selectors":["FullyQualifiedName~T207_"],"files":["tests/nextorm.core.tests/EntitySelectListBuilderCancellationTests.cs","tests/nextorm.sqlite.tests/EntityItemProjectionTests.cs"],"rebuild":"affected","boundary":"direct builder and SQLite preparation","rationale":"deterministic cancellation and unchanged-result oracles"},"D207.2":{"projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests"],"selectors":["FullyQualifiedName~T207_"],"files":["src/nextorm.core/Query/QueryCommand.QueryPreparer.cs","src/nextorm.core/DataContext/EntitySelectListBuilder.cs"],"rebuild":"affected","boundary":"projection cancellation only","rationale":"source fix and incomplete-consumer cleanup"},"D207.3":{"projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tracked provider SQL-generation projects discovered in D207.0","benchmarks/nextorm.benchmark"],"selectors":["full functional suites","acceptance: all eight cases"],"files":["artifacts/pdca/D207-projection-cancellation/rv1/"],"rebuild":"affected","boundary":"coverage/mutation/shared-consumer/performance regression","rationale":"P1 evidence and mandatory performance gate"},"D207.4":{"projects":[],"selectors":[],"files":["docs/specs/design/code-smells-review.md","artifacts/pdca/D207-projection-cancellation/status.md"],"rebuild":"none","boundary":"internal tracking only","rationale":"same-milestone scope handoff"}}
```

### Priorities / docs / risks / perf

- **P1 by construction:** every AC207-A…H execution-path row, the branch/mutation obligations and the class-priority row **cached-vs-prepared ratio**; CHECK may not downgrade. Documentation/tracking is P2 but mandatory.
- **Docs:** the internal record `docs/specs/design/code-smells-review.md` (fixed sites, remaining follow-up, issue URL). `docs/advanced/limitations.md` and the EN/RU public docs are **not touched**: no new capability/API/public limitation and no public rename.
- **Main risk** of removing the incomplete handling: changed behaviour for `SqlBuilder.cs:964,1254` and `RawMapperFactory.cs:641–642`; mitigation — shared-consumer rows, direct-builder `None`, happy-path order/count and the full provider suites. Other risks: dominating guards hiding the inner branch → guard/mutation evidence; cache leakage → the ban on mutating command state; flaky timing → deterministic tokens only; CRLF/warnings → normalise the touched files and keep the build clean.
- **Perf is mandatory:** query preparation and shared expansion are on a repeated path, not one-time work. Use the existing acceptance runner and the unchanged baseline parameters; do not introduce a new, non-comparable BDN job. The 7/8 discrepancy: seven main cases plus the separate `SqlServerBufferedNumericAcceptanceBenchmark` (class-level `[BenchmarkCategory("acceptance")]` at `benchmarks/nextorm.benchmark/acceptance/SqlServerBufferedNumericAcceptanceBenchmark.cs:61`); the runner `acceptance/acceptance_gate.py:42–43` runs the eighth separately. Baseline **1.87**, +20% trigger **2.244**, cost target ≤4 min; a breach is recorded and analysed without skipping cases or changing the baseline. The existing gate CLI is confirmed in D207.0 before use — do not guess flags.

### Reconnaissance

Closed by execution: the red arm was run against baseline `c7ff89ca…` with `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug","--filter","FullyQualifiedName~T207ReconCancellationProbe"]` → exit 2, `total 4 / failed 3 / succeeded 1 / skipped 0`; build exit 0. Per-probe: CONTROL `.Select(p => p.Item2)` → OCE (green, harness valid); TARGET A `new T207ProbePair(p.Item2, p.Item1.Id)` → completed normally, `selectList=0`, columns `[]`; TARGET B `new T207ProbeDto { Child = p.Item2, Name = p.Item1.Name }` → completed normally, `selectList=2`, columns `[NULL, NULL]`; TARGET C bare join `.ToCommand()` → `QueryPreparationException: Select must return new anonymous type` (masking). The `TryExpandEntityItem:1829`/`:869–871` incomplete sites are not independently exercised by A/B/C (the `:653` check fires first). The strict mid-build arm remains irreproducible; the oracle is re-scoped per invariant 11. Throwaway file deleted; tree returned to its pre-run state.

### Unit execution mode

One current tree; streams `code`, `tests`, `docs/evidence`; executed sequentially because the test/artifact footprint is shared. No worktree.

### Frozen evidence contract — rv=1

`contract_rv = 1`; `$E = artifacts/pdca/D207-projection-cancellation/rv1`; manifest `$E/evidence-contract.json`. All DO sources/artifacts below are **planned** except the explicitly executed recon.

`required_rows = ["E207-RED","E207-TEST","E207-GUARD","E207-CONS","E207-COV","E207-MUT","E207-PERF","E207-HANDOFF"]`, every row `rv=1`, applicability `true` unless stated.

- **E207-RED** (AC207-A,B,C,F) — the executed probe run above: exit 2, exactly 3 expected failures + 1 control pass; build exit 0. Sources: the real recon logs → `$E/red/`, `$E/red/provenance.json` (HEAD/argv/tree state). Owner: tests.
- **E207-TEST** (AC207-A…G) — for `p` in {`tests/nextorm.core.tests`, `tests/nextorm.sqlite.tests`}: `dotnet test "$p" -c Debug --filter 'FullyQualifiedName~T207_'`; green, exit 0, all case IDs executed, `skipped=0`. → `$E/tests/focused-*.log`, `$E/tests/variants.json`. Owner: tests.
- **E207-GUARD** (AC207-D,E,F,G) — `git diff --check`; `dotnet build -c Debug`; `git diff -- src/nextorm.core/Query/QueryCommand.QueryPreparer.cs src/nextorm.core/DataContext/EntitySelectListBuilder.cs`; the first two exit 0 with 0 warnings/errors; a semantic guard review confirms the sites/structural refusal/cache state. → `$E/guards/build.log`, `guards/diff.patch`, `guards/review.json`. Owner: code.
- **E207-CONS** (AC207-G,H) — `bash "$E/run-tests.sh"`: `dotnet test "$p" -c Debug` for core, SQLite and each tracked provider SQL-generation project found via `git ls-files 'tests/*.csproj' 'tests/**/*.csproj'`; all exit 0, skips not hidden. → `$E/tests/full/`, `$E/tests/consumer-map.json`. Owner: tests.
- **E207-COV** (AC207-A…H) — `dotnet-coverage collect "bash $E/run-tests.sh" -s coverage.settings.xml -f cobertura -o "$E/coverage/coverage.cobertura.xml"`, then `reportgenerator "-reports:$E/coverage/coverage.cobertura.xml" "-targetdir:$E/coverage/report" "-reporttypes:JsonSummary"`; both exit 0, real line/branch numbers and changed-branch classification. → `$E/coverage/…` + `$E/coverage/branch-delta.json`. Owner: tests.
- **E207-MUT** (AC207-A…H) — `dotnet stryker --config-file "$E/mutation/stryker-config.json"`; exit 0 and a scoped mutation report with no unexplained P1 survivor/no-coverage. → `$E/mutation/`. Owner: tests.
- **E207-PERF** (AC207-P) — the perf wrapper (`bash "$E/run-perf.sh"`, whose exact nested CLI is fixed in D207.0); exit 0, eight cases actually executed, baseline comparability confirmed, ratio ≤2.244; a threshold breach or a skip is not a pass. → `$E/perf/invocation.json`, `run.log`, `results.json`, `baseline-reference.json`. Owner: code.
- **E207-HANDOFF** (scope, invariant 8) — the handoff wrapper; exit 0, with the duplicate-search argv/results, the verified issue URL, milestone `1.0.9-rc3` and the active follow-up preserved. → `$E/handoff/`. Owner: docs/evidence.

Wrapper rule: the wrappers must preserve the exact nested argv, stdout/stderr and exit codes; an error must not be hidden behind a pipe or a successful last process.

CHECK re-gather: owner **CHECK**, at most **2** targeted scout invocations total, on existing sources/artifacts only; no new DO iteration because a report is missing. After the budget — an evidence-gap verdict; persistent low confidence → the orchestrator calls `escalate` (trigger 5). Any substantive revision keeps the row IDs/obligations, adds IDs for new variants and explicitly supersedes rv1; a missing report alone does not authorise a revision.

### Design checklist

- 🔴 P1 [Cancellation/atomicity] `QueryCommand.QueryPreparer.cs:653–654,758–759,1628–1629,939` — throw OCE instead of a partial/masking result; fix now; AC207-A/B/C; invariant 11.
- 🔴 P1 [Shared contract] `EntitySelectListBuilder.cs:30–31`, `QueryCommand.QueryPreparer.cs:869–871,1829` — eliminate cancellation-as-incomplete; fix now; AC207-D/H.
- 🟡 P1 [Scope] `QueryCommand.QueryPreparer.cs:2722–2723,3495–3496,3536–3537` — active separate fix on the handoff trigger; 1.0.9-rc3; invariant 8.
- ℹ️ P1 [Scope restraint] `RowMaterializerBuilder.cs:41–70` — token threading deferred on the explicit trigger; no partial-return defect established.

### Classification / confidence / feasibility

No DO→PLAN report exists; the reconnaissance confirms an implementable defect fix, not an external blocker. Confidence is high in the oracle and scope; the CLI / XML-doc / perf-environment checks are bounded D207.0 verifications, not assumed evidence. **Feasibility: `ready`** — non-feasibility in this workspace is not established; before DO, freeze the manifest/status, perform the mandatory gate-1 actions and never declare future checks as passed.

## ACT (r=1, rv=1) — closed

**Cycle:** N=1, plan revision r=1, attempt n=1; CHECK verdict **PASS** (rv=1) — no loop-back, no further DO iteration.

### Done / Verified

The frozen evidence manifest `artifacts/pdca/D207-projection-cancellation/rv1/manifest.json` carries all **8** required contract rows as `met`: **E207-RED, E207-TEST, E207-GUARD, E207-CONS, E207-COV, E207-MUT, E207-PERF, E207-HANDOFF**.

- **E207-COV** — coverage line **85.7 %** / branch **78.2 %** (CI reference 85/75; the rc-branch shortfall is a CI warning, never presented as a met threshold).
- **E207-CONS** — boundary sweep `ran=11 passed=11 failed=0`.
- **E207-PERF** — external wall clock **62 s** (cost target ≤4 min); cached-vs-prepared ratio **1.72** vs baseline **1.87**, below the +20 % trigger **2.244**.
- **E207-MUT** — tally **26 killed / 6 explained / 3 no-coverage (outside-diff) / 1 pending-unresolved (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:653`, explicit reason)** = **26+6+3+1 = 36** active mutants, score **74.29 %**. The frozen manifest's prose note double-counts the 3 no-coverage mutants inside the 8 outside-diff; the frozen manifest is **not** edited and the correct numbers are recorded here.

### CHECK verdict

**PASS** — r=1 / rv=1, no loop-back, no blocking findings. Four ℹ️ P2 accepted residuals: (1) the pending mutant at `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:653`; (2) malformed JSON in the gitignored scratch `run-tests.sh` outputs; (3) the manifest prose tally double-count; (4) `SqlServerBufferedNumericAcceptanceBenchmark` unexecuted and excluded from the default build — honestly recorded as **not-a-pass**.

### Changed files

1. `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`
2. `src/nextorm.core/DataContext/EntitySelectListBuilder.cs`
3. `tests/nextorm.core.tests/EntitySelectListBuilderCancellationTests.cs`
4. `tests/nextorm.sqlite.tests/EntityItemProjectionTests.cs`
5. `docs/specs/design/code-smells-review.md`
6. `docs/specs/status/D207-projection-cancellation-1.md`

### Next plan

— (flow closed for T207; projection-build cancellation is uniform).

Follow-up: issue **#241** (milestone **1.0.9-rc3**) owns the sorting/grouping cancellation slice; register entry `docs/specs/design/code-smells-review.md` Находка 241.

### Progress log

2026-10-16T10:11:35Z | PLAN | r1 | n1/3 | PLAN r=1 recorded; plan_state=ready; contract_rv=1; n=1. PLAN→DO boundary not crossed. | docs/specs/status/D207-projection-cancellation-1.md
2026-10-16T10:11:35Z | PLAN | r1 | n1/3 | RECON (red arm) executed at baseline c7ff89ca8ae339c920cc6cb0c6b6ef4558aa6481: build exit 0; probe exit 2 (total 4 / failed 3 / passed 1 / skipped 0); throwaway probe source created then deleted; tree restored. | /tmp/nextorm/D207-build.log, /tmp/nextorm/D207-recon-red.log, /tmp/nextorm/D207-recon-probe-details.txt
2026-10-16T10:11:35Z | PLAN | r1 | n1/3 | Notice: pdca-dotnet sidebar todo tracker unavailable in this orchestrator session (no todowrite tool exposed in the Code Mode catalog) — the todo mirror could not be maintained; this status file is the source of truth. | docs/specs/status/D207-projection-cancellation-1.md
2026-10-16T10:11:35Z | PLAN | r1 | n1/3 | PLAN beat ends here — DO not started (collection PLAN-only beat). | docs/specs/status/D207-projection-cancellation-1.md
2026-10-16T11:24:08Z | DO | r1 | n1/3 | DO started for T207 (D207.0-D207.4); one current tree, no worktree/branch/commit/push; evidence $E = artifacts/pdca/D207-projection-cancellation/rv1. | docs/specs/status/D207-projection-cancellation-1.md
2026-10-16T11:24:08Z | DO | r1 | n1/3 | D207.1 red baseline recorded (test-first, pre-fix): filtered red -- core 3 selected / 1 RED (exit 2), sqlite 8 selected / 3 RED (exit 2); happy-path SQL/column oracles frozen before the production fix. | /tmp/nextorm/D207/tests-prered.log
2026-10-16T11:24:08Z | DO | r1 | n1/3 | D207.2 fix applied: QueryCommand.QueryPreparer.cs:653,757,1625 (ThrowIfCancellationRequested) + :869,:1823 (now-unreachable !completed handling removed) + EntitySelectListBuilder.cs:30; dotnet build nextorm.slnx -c Debug exit 0, 0 warnings / 0 errors; filtered green after fix -- core 3/3 exit 0, sqlite 8/8 exit 0. | artifacts/pdca/D207-projection-cancellation/rv1/guards/build.log, tests/focused-core.log, tests/focused-sqlite.log
2026-10-16T11:24:08Z | DO | r1 | n1/3 | D207.3 rows met: E207-RED, E207-GUARD, E207-CONS, E207-TEST, E207-COV (coverage line 85.7 % / branch 78.2 % -- rc branch, CI warning, NOT presented as a met threshold; changed-branch delta recorded); full boundary sweep 11 tracked test projects all exit 0, 0 failed (skips visible: sqlite 1, sqlserver 8; integration project skipped -- needs DOCKER_HOST). E207-MUT partial: Stryker exit 0, 1180 s, score 74.29 %, 36 active mutants, 26 killed. | artifacts/pdca/D207-projection-cancellation/rv1/coverage/coverage.cobertura.xml, tests/cons-run.log, mutation/triage.json
2026-10-16T11:24:08Z | DO | r1 | n1/3 | D207.4 scope handoff: register finding Находка 241 (P1 cancellation, fixed) and open the follow-up issue #241 (sorting/grouping cancellation) in milestone 1.0.9-rc3 after a 9-variant duplicate search over open+closed. | docs/specs/design/code-smells-review.md:8342, artifacts/pdca/D207-projection-cancellation/rv1/handoff/record.json
2026-10-16T11:24:08Z | DO | r1 | n1/3 | E207-PERF executed (the missing mandatory perf gate): dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance -> exit 0; external wall clock 62 s (BDN global total 47.14 s) under the <=4 min target; 7 of the 8 declared acceptance sites executed, 0 failures; cached-vs-prepared ratio 1.72 (Cached_ToList 1584.3 us / Prepared_ToList 921.144 us), baseline 1.87, trigger 2.244 -- below both. The 8th site SqlServerBufferedNumericAcceptanceBenchmark is excluded from the default build (Compile Remove acceptance/**/*.cs, nextorm.benchmark.csproj:47) and needs the D168 harness + SQL Server: recorded UNEXECUTED, not a pass. | artifacts/pdca/D207-projection-cancellation/rv1/perf/run.log, results.json, baseline-reference.json, invocation.json
2026-10-16T11:24:08Z | DO | r1 | n1/3 | E207-MUT triage finalised WITHOUT re-running Stryker: 26 killed / 6 explained (1 equivalent on the dominated :1625, 5 outside-diff on the unchanged :753/:761) / 3 no-coverage outside-diff (:591-593, preserved body, declared NoCoverage -- never presented as covered) / 1 unresolved (:653, the CHANGED line, Stryker-Pending 'not fully tested', explicit reason; NOT equivalent) / 0 covered-by-other-project (the recorded run's test scope already includes tests/nextorm.sqlite.tests). | artifacts/pdca/D207-projection-cancellation/rv1/mutation/triage.json, triage-readback.log
2026-10-16T11:24:08Z | DO | r1 | n1/3 | Frozen evidence manifest written and validated: python3 scripts/validate_inner_loop.py manifest $E/manifest.json -> exit 0, all 8 required rows present exactly once; E207-PERF = met (7 cases actually executed with numbers), E207-MUT = met (the single genuinely unresolved mutant carries an explicit reason). DO->CHECK boundary reached; the units are NOT self-declared done. | artifacts/pdca/D207-projection-cancellation/rv1/manifest.json
2026-10-16T11:30:48Z | ACT | r1 | n1/3 | ACT: status finalized; CHECK PASS r1/rv1. | docs/specs/status/D207-projection-cancellation-1.md
2026-10-16T11:30:48Z | ACT | r1 | n1/3 | ACT closed; flow closed for T207. | docs/specs/status/D207-projection-cancellation-1.md
