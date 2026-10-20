# D241 — T241 uniform cancellation handling for sorting/grouping preparation — cycle status

task: T241
issue: #241 — https://github.com/AlexeyShirshov/nextorm/issues/241
issue_title: Uniform cancellation handling during sorting/grouping build
milestone: 1.0.9-rc3
state: OPEN
labels: none
collection: 1.0.9-rc3-open-2
collection_status_path: docs/specs/status/collection-1.0.9-rc3-open-2.md
selected_variant: pdca-dotnet
cycle_id: N=1
plan_revision: r=1
attempt: n=1
plan_state: ready
evidence_contract_rv: 1
spec_version: T241-plan-v1
baseline_ref: 83e78d377cee5f307ecea7fc9cc0307e7cdfab78  (short 83e78d37)
pattern_commit: ec42c960  (#207 fix pattern delivered here)
branch: 1.0.9-rc3
worktree: /home/alex/sources/nextorm
canonical_status_path: docs/specs/status/D241-sorting-cancellation-1.md
evidence_dir: artifacts/pdca/D241/rv1/
evidence_contract_source: artifacts/pdca/D241/rv1/evidence-contract.json
evidence_contract_manifest: artifacts/pdca/D241/rv1/manifest.json
check_regather_owner: CHECK
check_regather_budget: 2
execution_mode: single-group (G1 = T241 -> T242, current worktree; no worktree/group-branch/task-branch, no merge, never push; autonomous + auto-commit authorised)

## PLAN (r=1)

**Plan revision:** r=1, first DO attempt n=1
**Evidence contract:** rv=1 (planned, not observed)
**Selected variant:** pdca-dotnet
**Authority note:** this plan was authored by `planner`; every test name, helper, config and artifact below is **planned**, not existing evidence. Existing references refer to the gathered baseline HEAD `83e78d37`.

### Goal (essence, not issue wording)

Surface cancellation **uniformly** across the sorting/grouping preparation paths: with a pre-cancelled `CancellationToken` each path must throw `OperationCanceledException` (OCE), never return a partial preparation result and never mask cancellation as (`QueryPreparationException`). Mirror the #207 fix (`ec42c960`). No public-API change, no cache-policy change, no scope expansion.

### Acceptance criteria (observable + negative)

| Requirement | Observable acceptance | Negative case |
|---|---|---|
| **R241-01** | `PrepareSorting` with a pre-cancelled token surfaces OCE, for both `dontCalculateHash` values. | Silent `break`, partial hash/state, or a masking exception fails. |
| **R241-02** | `BuildKeyColumns` with a pre-cancelled token surfaces OCE rather than returning an incompletely populated key array. | Returned array with unfilled `null` slots, normal return, or masking exception fails. |
| **R241-03** | `PrepareGrouping` preparation with a pre-cancelled token surfaces OCE. Its public path first reaches `BuildKeyColumns`; the later grouping-loop guard must also be replaced uniformly. | Claiming a pre-cancelled GroupBy test independently executes the dominated `:3530` guard fails evidence review. |
| **R241-04** | Active-token and `CancellationToken.None` preparation preserve baseline SQL, key-column order/count, applicable hashes and query results, for both hash flags. | Post-fix-only expectations, self-comparison, missing columns, reordered keys or changed results fail. |
| **R241-05** | No public API or cache-policy change. Cancellation cannot set `_isPrepared` or reach plan-cache storage; shared/sticky cache state is not mutated on behalf of a call. | New API/test hooks, sticky `Cache` mutation, or partial cache admission fails. |
| **R241-06** | `SqlBuilder` and `RawMapperFactory` consumers retain complete, correctly ordered results for non-cancelled execution. | SQL generation alone without result-materialization evidence is insufficient. |

Engineering obligations: **R241-07** clean build/source guards/CRLF/scoped change; **R241-08** baseline+final line/branch coverage with explained deltas; **R241-09** scoped mutation with survivor disposition; **R241-10** seven-case acceptance benchmark + ratio; **R241-11** register update + collection handoff; **R241-12** canonical manifest admission through both validators.

### Minimal solution

1. **Essential change** — replace exactly these three cancellation decisions with `cancellationToken.ThrowIfCancellationRequested()`, in `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`:
   - `:2716–2717` (`PrepareSorting`; was `if (cancellationToken.IsCancellationRequested) break;`)
   - `:3489–3490` (`BuildKeyColumns`; was `if (…) return columns;`)
   - `:3530–3531` (`PrepareGrouping`; was `if (…) return (groupingList, groupingPlanHash);`)
2. **Constraints:** preserve preparation order, token threading, normal results, public surface, hashing policy, and cache invariants.
3. **Optimum within those constraints:** three local replacements + regression tests + evidence. No cancellation wrapper, rollback mechanism, or pipeline redesign.

| Alternative | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Replace the three checks with `ThrowIfCancellationRequested()` | Mirrors #207; smallest behavioral change | Requires honest treatment of dominated/no-iteration guards | **Selected** |
| Add only a preparation-entry cancellation check | Cheap fail-fast | Does not fix the three local partial-result exits; moves cancellation placement | Rejected |
| Transactionally stage/rollback sorting/grouping | Could address cancellation after partial sorting mutation | Larger cache-sensitive refactor outside the frozen oracle | Deferred (see V241-18) |

### Scope / gaps

| Gap | Resolution |
|---|---|
| Issue line hints off by ~6 lines | Use the gathered baseline locations above; refresh via Roslyn after edits. |
| Whether a GroupBy pre-cancellation test independently exercises `:3530` | It does not — `BuildKeyColumns:3489` dominates. Use executable public-path tests + an explicit source guard for the later check. |
| Empty key expression, `argsCount == 0` | D241.0 feasibility obligation: establish whether an accepted query can produce this shape. No loop execution or OCE may be invented. An accepted counterexample → return to PLAN; do not redefine the oracle. |
| Partial sorting mutation after cancellation at `k > 0` | Known residual (`:2719–2724`); rollback out of scope. Pre-cancelled tests must not be presented as proving mid-loop atomicity. |
| Cache safety | `_isPrepared = true` at `:125`; cache store `QueryPlanner.cs:811–813` after preparation returns — verify unchanged. |
| Other `BuildKeyColumns` callers (`:3373,:3384,:3402,:3405,:3421`) | Guard token threading/shared-helper behavior; no new provider capability work. |
| Baseline preservation oracle | Capture original-source positive expectations before production edits; independent SQL/shape/result assertions, not post-fix-derived. |
| Test/helper accessibility | Resolve via Roslyn in DO; prefer the existing `PrepareCommand(…, token)` harness; private-helper access uses existing conventions/reflection, no production API change. Reflection must not be confused with public exception propagation. |
| Manifest precedent is lean and fails canonical lint | Do not copy it; generate a real tree-OID manifest; require both validators rc 0. |
| Validator file-operand CLI spelling | D241.0 resolves it from existing CLI help/parser; the evidence wrapper records the actual argv. No guessed flags or validator edits. |
| Public documentation relevance | DO searches EN/RU public docs before concluding none needs an update. |
| Dirty starting tree | Inventory and preserve unrelated work; an incompatible source baseline is an in-cycle precondition, not permission to reset user changes. |
| T242 availability | Not required; T241 must use the existing generator/linter or report its own admission blocker. |

### Units / WRITE footprint

One current tree; sequential `D241.0 → D241.1 → D241.2 → D241.3 → CHECK`; no worktree/branch/merge/push.

- **D241.0 — evidence admission & feasibility preflight (fix now).** Writes: `artifacts/pdca/D241/rv1/run_evidence.py` (planned evidence-only driver), `artifacts/pdca/D241/rv1/preflight/**`, `artifacts/pdca/D241/rv1/units/D241.0/**`, status progress. Actions: confirm branch/baseline/dirty-tree/tool availability; resolve validator argv; author/validate unit briefs; run **at most two** canonical-admission probe rounds (probe manifest over an existing source path; both `pdca_evidence.py lint` and inner-loop manifest validation must return 0; keep the probe separate from the final manifest); establish helper access and empty-key reachability via Roslyn; confirm the 13-project boundary inventory and container prerequisites. Failure → retain command/error and return to PLAN (no T242 dependency, no fake rc 0).
- **D241.1 — baseline, regression tests, red arm, minimal fix (fix now).** Production write: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`. Planned test writes: `tests/nextorm.core.tests/SortingGroupingCancellationTests.cs`, `tests/nextorm.sqlite.tests/SortingGroupingCancellationTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite.SortingGroupingCancellation.cs`. Evidence: `artifacts/pdca/D241/rv1/{baseline,red,focused}/**`, `…/units/D241.1/**`. Order: capture baseline build + full-suite coverage before edits → add positive preservation assertions and cancellation tests against unchanged production → execute the red arm (preserve commands/failures/selection counts/source identity/test patch) → apply the three replacements → run focused green (incl. materialization) → normalise touched files to CRLF.
- **D241.2 — boundary, coverage, mutation, performance, guards (fix now).** Evidence writes: `artifacts/pdca/D241/rv1/{boundary,guard,coverage,mutation,perf}/**`, `…/units/D241.2/**`. Clean Debug build + diff/source guards; full 13-project boundary sweep; final workflow-equivalent coverage + delta; scoped Stryker run; seven-case acceptance benchmark + ratio.
- **D241.3 — register, canonical manifest, handoff (fix now).** Writes: `docs/specs/design/code-smells-review.md`, this status file, `artifacts/pdca/D241/rv1/evidence-contract.json`, `…/manifest.json`, `…/handoff/**`, `…/units/D241.3/**`. Update Находка 241 (`code-smells-review.md:8346–8354`) to mark the sorting/grouping remainder resolved by #241 only after evidence supports it. Finalise the full footprint before generating the final tree-bound manifest. CHECK — not DO — issues the verdict.

**Explicitly NOT changed:** `QueryPlanner.cs`, `QueryCommand.Prepare.cs`, `SqlBuilder`, `RawMapperFactory`; public APIs, package versions, CPM/toolchain; root `stryker-config.json`, coverage settings, validator/evidence tooling; `BulkInsertBuilder.cs:502,507`, `InMemoryEnumerator.cs:140`; T242 implementation/status; generated docs/benchmark implementation; public docs unless DO identifies a real affected page (route back to PLAN); unrelated user changes.

### Variant matrix (`test` = executed regression evidence; `guard` = explicit structural/applicability check)

| ID | Variant / execution-path concern | Closure |
|---|---|---|
| V241-01 | Sorting-only OrderBy, no GroupBy; one/multiple sort keys | **test:** pre-cancelled OCE + active/None preservation, both hash flags; red must show the sorting boundary, not an earlier unrelated check. |
| V241-02 | GroupBy, one and composite keys | **test:** pre-cancelled OCE through `BuildKeyColumns`; positive SQL/keys/results. |
| V241-03 | Grouping-loop cancellation guard at `:3530` | **guard:** throwing check remains in the loop; dominance documented. Active/None may execute the line but do not prove the cancelled arm. |
| V241-04 | Direct `BuildKeyColumns` nonempty shapes | **test:** pre-cancelled OCE; complete active/None arrays, order and count. |
| V241-05 | LIMIT BY caller `:3373` | **guard:** unchanged helper/token path; key-shape coverage via V241-04. |
| V241-06 | DISTINCT ON caller `:3384` | **guard:** shared-helper obligation; existing provider boundary tests retained. |
| V241-07 | ExtremeRow callers `:3402/:3405` | **guard:** both call sites + argument/token threading. |
| V241-08 | Per-parent caller `:3421` | **guard:** unchanged shared-helper/token path. |
| V241-09 | Empty key args, `argsCount == 0`; empty grouping loop | **guard**, then **test if accepted input exists**. Record actual reachability; an accepted counterexample requires replan. |
| V241-10 | Active cancellable token vs `CancellationToken.None` | **test:** distinguish `CanBeCanceled=true` from None; neither returns partial results. |
| V241-11 | `dontCalculateHash=true/false` | **test:** cancellation and preservation theories cover both; compare only applicable baseline hash behavior. |
| V241-12 | Value/reference keys; nullable/default key values | **test:** both key categories and null/default data values in positive cases; preserve baseline null ordering per provider. |
| V241-13 | `selectList == null`, absent sorting/grouping; invalid null expression | **guard:** resolve reachable optional paths via Roslyn; no new validation; no claim an unentered path executed a guard. |
| V241-14 | Shared `SqlBuilder` consumption | **test:** baseline SQL + complete key-column shape + boundary sweep. |
| V241-15 | Shared `RawMapperFactory` consumption | **test:** integration execution compares complete ordered result sequences under active/None tokens. |
| V241-16 | In-memory and relational execution | **test:** core result checks + SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse integration; provider support unchanged. |
| V241-17 | Cache/sticky command behavior | **guard** + repeated-context execution: no cache-policy writes; failed preparation cannot reach the cache store. |
| V241-18 | Cancellation after `k > 0`; concurrent cancellation/rollback | **deferred:** trigger = an explicit atomicity requirement or reproducible mid-loop corruption. No rollback claim now. |

No row silently omitted. A newly discovered accepted variant preserves existing IDs/obligations; a material plan/contract change requires explicit revision.

### Test strategy

**Planned test symbols** (design names, NOT discovered symbols; no fabricated `file:line`):
- core `SortingGroupingCancellationTests`: `T241_PreCancelled_KeyColumns_ThrowsOperationCanceled`, `T241_KeyColumns_ActiveAndNone_ReturnCompleteKeys`, `T241_ActiveAndNone_InMemorySortingGrouping_KeepResults`.
- sqlite `SortingGroupingCancellationTests`: `T241_PreCancelled_Sorting_ThrowsOperationCanceled`, `T241_PreCancelled_Grouping_ThrowsOperationCanceled`, `T241_ActiveAndNone_SortingGrouping_KeepSqlKeysAndHashes`.
- integration shared-suite: `T241_ActiveAndNone_SortingGrouping_KeepQueryResults`.

Mirror existing #207 harness: `EntitySelectListBuilderCancellationTests.cs:22,34,47`; `EntityItemProjectionTests.cs:440–494`; the `PrepareCommand(bool, CancellationToken)` entry point; existing grouping fixtures/SQL examples. The accepted-empty-shape case, if applicable, folds into the theories; its behavior is not assumed.

**Red arm** (against unchanged production, after adding tests):
`dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T241_PreCancelled"` and the same for `tests/nextorm.sqlite.tests`. Required: selected > 0 in each; each intended cancellation scenario fails because OCE is not surfaced; no compile/discovery/unsupported-fixture/unrelated provider error counts as red; record actual nonzero exit codes with assertion diagnostics; preserve original production checksum/diff; attribute the GroupBy failure to the public grouping path, not to independent coverage of `:3530`. The evidence driver `red` stage returns 0 only when the expected red conditions are verified.

**Green arm:**
`dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T241_"`, `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~T241_"`, `dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~T241_"` — all exit 0, selected > 0, expected providers executed. Pre-cancellation assertions reject masking `QueryPreparationException`; assert token identity where the harness exposes it (public preparation tests must observe OCE directly). Positive expectations frozen before production editing: literal/independently derived SQL and key shapes, known fixture result sequences, original hash behavior per flag, complete arrays with no unfilled slots.

**Boundary sweep point:** (1) the declared baseline coverage boundary before edits; (2) D241.2 after focused green. No full-suite rerun after every local edit. Inventory via `git ls-files 'tests/*.csproj'` (freeze the observed 13 projects); driver runs `dotnet test "$p" -c Debug --no-build` per project. Load the running-integration-tests skill before container runs; `export DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; skipped providers (PostgreSQL/SQL Server/MySQL/ClickHouse) are NOT passing evidence.

### DO brief test scope

`files` identifies scope, not necessarily writes; helper/validator argv resolved and logged in D241.0.

```json
{"unit":"D241.0","scope":{"projects":["src/nextorm.core/nextorm.core.csproj"],"selectors":["admission:D241:rv1","feasibility:V241-09"],"files":["scripts/pdca_evidence.py","scripts/validate_inner_loop.py","src/nextorm.core/Query/QueryCommand.QueryPreparer.cs"],"rebuild":"none","boundary":"No test-suite sweep; at most two canonical manifest probe rounds and targeted semantic feasibility inspection.","rationale":"Establish existing-tool admission and oracle obtainability before production edits."}}
```

```json
{"unit":"D241.1","scope":{"projects":["tests/nextorm.core.tests/nextorm.core.tests.csproj","tests/nextorm.sqlite.tests/nextorm.sqlite.tests.csproj","tests/nextorm.integration.tests/nextorm.integration.tests.csproj"],"selectors":["FullyQualifiedName~T241_PreCancelled","FullyQualifiedName~T241_"],"files":["src/nextorm.core/Query/QueryCommand.QueryPreparer.cs","tests/nextorm.core.tests/SortingGroupingCancellationTests.cs","tests/nextorm.sqlite.tests/SortingGroupingCancellationTests.cs","tests/nextorm.integration.tests/CommonTestSuite.SortingGroupingCancellation.cs"],"rebuild":"affected","boundary":"One recorded original-source full-suite coverage baseline before edits; thereafter only focused selectors until green.","rationale":"Prove the defect with executed red tests, apply three local replacements, verify cancellation plus preservation."}}
```

```json
{"unit":"D241.2","scope":{"projects":["src/nextorm.core/nextorm.core.csproj","tests/nextorm.core.tests/nextorm.core.tests.csproj","tests/nextorm.sqlite.tests/nextorm.sqlite.tests.csproj","tests/nextorm.integration.tests/nextorm.integration.tests.csproj"],"selectors":["boundary:tracked-test-projects","mutation:three-cancellation-sites","benchmark:acceptance","coverage:baseline-final"],"files":["src/nextorm.core/Query/QueryCommand.QueryPreparer.cs","tests/nextorm.core.tests/SortingGroupingCancellationTests.cs","tests/nextorm.sqlite.tests/SortingGroupingCancellationTests.cs","tests/nextorm.integration.tests/CommonTestSuite.SortingGroupingCancellation.cs"],"rebuild":"affected","boundary":"After focused green: all 13 tracked test projects, workflow-equivalent coverage, isolated scoped mutation, seven acceptance cases.","rationale":"Query-path changes require consumer, branch/mutation, coverage and performance evidence beyond local cancellation tests."}}
```

```json
{"unit":"D241.3","scope":{"projects":["src/nextorm.core/nextorm.core.csproj"],"selectors":["register:241","manifest:D241:rv1","handoff:T241-to-T242"],"files":["docs/specs/design/code-smells-review.md","docs/specs/status/D241-sorting-cancellation-1.md","artifacts/pdca/D241/rv1/evidence-contract.json","artifacts/pdca/D241/rv1/manifest.json"],"rebuild":"none","boundary":"Documentation, provenance and dual-validator admission only; no rebuild or full test rerun for status/register edits.","rationale":"Resolve the tracked remainder and freeze auditable collection handoff without fictitious docs-only rebuilds."}}
```

Validate every brief and completed report with the existing `validate_inner_loop.py`. Finished executions need real selection counts and actual source/artifact revisions; non-test checks identify the selected validation target explicitly — never fabricate a test count.

### Priorities / docs / risks / perf

- **P1 by construction (CHECK may not downgrade):** R241-01–06 and V241-01–17 as applicable; rows `E241-RED`, `E241-TEST`, `E241-CONS`, `E241-GUARD`, `E241-COV`, `E241-MUT`, `E241-PERF`, `E241-ADMIT`, `E241-HANDOFF`. V241-18 stays an explicitly deferred requirement.
- **Docs:** update the internal Находка 241 remainder at `code-smells-review.md:8346–8354`, preserving the #207 projection resolution and identifying #241’s distinct completion; maintain this status file and the handoff. Search EN/RU public docs for an affected cancellation contract (currently none implicated). No generated docs / XML-doc needed for three private replacements. Do not close #241 until accepted evidence and the authorized collection workflow warrant it; do not close or implement #242.
- **Perf (required — query-path class):** `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; require **7 cases executed / 0 failures**; record `Cached_ToList / Prepared_ToList`; baseline **1.87**; investigation trigger **>2.244** (record disposition; not a hard threshold); record duration vs ≤4 min target; archive `benchmarks/BenchmarkDotNet.Artifacts/` outputs under the evidence dir. Do not enable the excluded D168/SQL Server buffered numeric harness or change benchmark sources.
- **Risks/mitigations:** earlier cancellation masking target coverage (require red against unchanged production + direct helper coverage); grouping guard overclaim (dominance explicit in matrix/mutation/CHECK); accepted empty-key shape invalidating the minimal fix (bounded preflight + conditional theory; return to PLAN without weakening OCE acceptance); canonical manifest incompatibility (dual-admission preflight; no tool patch, no T242 dependency); container availability (documented Podman recovery; skips ≠ green); benchmark noise (raw output + documented trigger); coverage denominator change (compare raw totals + changed paths); large preparer mutation scope (restrict to discovered cancellation ranges); evidence/source mismatch (final tree generation after source/register completion + both validators); accidental collection overlap (sequential; T242 only after accepted T241 handoff).

### Reconnaissance / unit execution mode

**Reconnaissance decision:** no solution-selection spike — the committed #207 pattern and the `PrepareCommand(bool, CancellationToken)` harness establish oracle obtainability. Bounded DO prerequisites (not design experiments): manifest dual-admission probe; Roslyn resolution of test access and call paths; empty-key reachability classification; an executed red arm against unchanged production. A contradictory accepted-input observation returns to PLAN.

**Unit execution mode:** sequential, one current tree: `D241.0 → D241.1 → D241.2 → D241.3 → CHECK → authorized auto-commit → T242`. Production/test/register/evidence identity overlap; no worktree, no parallel source edits, no merge, no push. Baseline/red experiments preserve unrelated files and restore only their own controlled changes; mutation runs use isolated output and leave no mutants in the final source.

### Frozen evidence contract — rv=1

Identity/representation:

```
task: T241
issue: "#241"
contract_rv: 1
status_file: docs/specs/status/D241-sorting-cancellation-1.md
path: artifacts/pdca/D241/rv1/manifest.json
base_ref: 83e78d377cee5f307ecea7fc9cc0307e7cdfab78
collection_baseline_ref: 83e78d377cee5f307ecea7fc9cc0307e7cdfab78
required_rows: [E241-ADMIT, E241-RED, E241-TEST, E241-GUARD, E241-CONS, E241-COV, E241-MUT, E241-PERF, E241-HANDOFF]
exclusions: [artifacts/**, docs/specs/status/**]
```

**No supersession:** initial contract, rv=1. The DO-authored `evidence-contract.json` carries all nine rows, stable requirement mappings and the final source/test/register footprint. Status/evidence remain explicit WRITE paths but are excluded from the frozen source tree.

Final manifest must: have a real **40-hex tree OID** (not a directory, commit or short hash); carry `base_ref`, `footprint`, `exclusions`; cover `required_rows` exactly; use existing repo-relative evidence paths; pass both validators; mark a row `met` only with its required evidence (`open` records a reason, never fabricated success); not use `na` for an unconditional obligation.

Planned evidence driver: `E=artifacts/pdca/D241/rv1`; `$E/run_evidence.py` is a **planned local helper** (not existing tooling) that executes the pinned child commands, captures argv/exit codes/counts/provenance and validates the stated predicates; it must not modify repository validators or suppress nonzero exits; it may resolve existing validator file-operand syntax from help/parser in D241.0 and records the actual argv. All stages return 0 only when their stated verification passes.

| Row — reqs — rv | Scenario / evidence sources (planned unless an existing source is named) | Exact invocation + required result | Expected artifacts | Owner | Applicability |
|---|---|---|---|---|---|
| **E241-ADMIT** — R241-12 — rv=1 | Probe + final canonical admission; generator/linter logs, inner-loop validation logs, real tree provenance, contract + manifest. Existing tooling: `pdca_evidence.py`, `validate_inner_loop.py`. | `python3 "$E/run_evidence.py" admission-preflight` — 0, ≤2 probe rounds. Final: `python3 scripts/pdca_evidence.py generate --repo . --contract "$E/evidence-contract.json" --output "$E/manifest.json"` — 0; `python3 scripts/pdca_evidence.py lint --repo . --manifest "$E/manifest.json"` — 0; `python3 "$E/run_evidence.py" validate-manifest` — 0 (inner-loop manifest validation). | `preflight/admission.log`, `handoff/admission.log`, `evidence-contract.json`, `manifest.json`, `handoff/tree-provenance.json` | D241.0 preflight; D241.3 final | Always |
| **E241-RED** — R241-01–03 — rv=1 | Executed original-production cancellation failures, selection, assertion diagnostics, original-source identity + test-only patch; grouping dominance recorded. | `python3 "$E/run_evidence.py" red` — 0 only after both pinned `dotnet test … --filter "FullyQualifiedName~T241_PreCancelled"` commands fail for the expected assertion reasons, selected > 0, no build/discovery failure; actual child nonzero codes recorded. | `red/core.log`, `red/sqlite.log`, `red/outcome.json`, `red/tests.patch`, `red/source-provenance.json` | D241.1 red | Always |
| **E241-TEST** — R241-01–06 — rv=1 | Focused cancellation + preservation theories, original positive expectations, token/hash/shape matrix, complete integration results. | `python3 "$E/run_evidence.py" focused` — 0; three pinned green commands above each exit 0 and selected > 0; required providers execute. | `focused/core.log`, `focused/sqlite.log`, `focused/integration.log`, `focused/matrix.json`, `focused/baseline-expectations.json` | D241.1 focused | Always |
| **E241-GUARD** — R241-03,05,07 — rv=1 | Three throwing sites; no partial exits; caller token threading; dominance/empty-shape classification; unchanged API/cache store ordering; CRLF + diff. Roslyn + actual diff. | `python3 "$E/run_evidence.py" guard` — 0; includes `git diff --check` — 0 and `dotnet build nextorm.slnx -c Debug` — 0 with 0 warnings/errors. Semantic checks via Roslyn, never C# symbol text search. | `guard/build.log`, `guard/diff-check.log`, `guard/semantic-findings.md`, `guard/api-cache-crlf.json`, `guard/empty-shape.md` | D241.0 feasibility; D241.2 guards | Always |
| **E241-CONS** — R241-04–06 — rv=1 | All tracked test projects; SQL/key shape + actual materialization; provider execution inventory. | `python3 "$E/run_evidence.py" boundary` — 0; `git ls-files 'tests/*.csproj'` frozen (13); `dotnet test "$p" -c Debug --no-build` per project, all exit 0; missing required providers / unexplained zero selection fails. | `boundary/projects.txt`, `boundary/project-*.log`, `boundary/providers.json`, `boundary/summary.json` | D241.2 boundary | Always |
| **E241-COV** — R241-08 (+R241-01–06) — rv=1 | Comparable original/final line+branch coverage, totals/denominators/deltas, changed-line attribution, threshold disposition. | `python3 "$E/run_evidence.py" coverage-baseline` and `coverage-final` — both 0; child commands = the workflow pair below, each 0; archive each snapshot before overwriting workflow output. | `baseline/coverage/…`, `coverage/final/…`, `coverage/delta.json`, `coverage/analysis.md` | D241.1 baseline; D241.2 final | Always |
| **E241-MUT** — R241-09 (+R241-01–03) — rv=1 | Scoped Stryker over the three cancellation sites, discovered post-edit ranges, mutant outcomes + P1 survivor analysis; dominated-site limits disclosed. | `dotnet stryker --config-file "$E/mutation/stryker-config.json"` — 0; `python3 "$E/run_evidence.py" mutation-review` — 0 only after complete scoped-result disposition. | `mutation/stryker-config.json`, `mutation/run.log`, `mutation/reports/**`, `mutation/analysis.md` | D241.2 mutation | Always |
| **E241-PERF** — R241-10 — rv=1 | Seven acceptance cases; cached/prepared ratio vs 1.87 and trigger 2.244; output + duration. | `python3 "$E/run_evidence.py" perf` — 0; executes the acceptance command — 0; 7 executed / 0 failures; any triggered investigation documented. | `perf/run.log`, `perf/summary.json`, `perf/analysis.md`, `perf/archive/**` | D241.2 perf | Always (query-path class) |
| **E241-HANDOFF** — R241-11,12 — rv=1 | Register remainder resolved; public-doc search; validated unit briefs/reports; exact final footprint; collection handoff; no unrelated edits. | `python3 "$E/run_evidence.py" handoff` — 0; validates briefs/reports via existing validator, register/docs checks and final `git diff --check` — 0. Final admission stays E241-ADMIT. | `handoff/register.patch`, `handoff/docs-search.log`, `handoff/summary.json`, `units/D241.*/{brief,report}.json`, updated status/register | D241.3 handoff | Always |

Coverage child commands (run with the required integration environment):

```bash
dotnet tool run dotnet-coverage collect \
  -s coverage.settings.xml -f cobertura \
  -o tests/coverage/coverage.cobertura.xml \
  "dotnet test --no-build --verbosity normal"

dotnet tool run reportgenerator \
  -reports:tests/coverage/coverage.cobertura.xml \
  -targetdir:tests/coverage/report \
  -reporttypes:"Html;TextSummary;Cobertura" \
  -riskhotspotassemblyfilters:"+nextorm.*"
```

Coverage interpretation: project thresholds line **85 %**, branch **75 %** (hard-fail only on `main`; this branch warns — record the actual status, never silently introduce/waive a threshold). Explain denominator changes from removing explicit branches; the BCL `ThrowIfCancellationRequested` is not a newly instrumented core branch; positive execution of `:3530` is not cancelled-arm evidence; unexplained execution-path coverage loss is a P1 evidence defect.

Mutation configuration: Stryker **5.0.0**; scoped config under `$E/mutation/`; never alter the root config; project `src/nextorm.core/nextorm.core.csproj`; test-projects core + SQLite; `test-runner: "mtp"`, `coverage-analysis: "perTest"`, concurrency 8, additional timeout 30000; precedent thresholds high 90 / low 70 / break 0; JSON+HTML reporters. Resolve post-edit ranges via Roslyn (do not reuse stale baseline line numbers); scope only the three cancellation checks. A dominated-guard survivor is not automatically globally equivalent — record its non-observability under pre-cancelled tests, the source guard and the out-of-oracle mid-loop risk. Zero generated mutants is not “100 % killed”; inadequate proof is routed, not hidden by `break: 0`.

Canonical tree/artifact handling: (1) admission preflight uses a scratch probe contract/manifest under `preflight/` over an existing source footprint — not the final manifest; (2) final generation uses the complete realized production/test/register footprint, pinned baseline, default exclusions; (3) do not copy D207’s directory-valued `tree`; (4) after final generation, no source/test/register change without regeneration + revalidation (status/evidence excluded); (5) evidence JSON lives in the named directories, not arbitrary `results/`/`receipts/`; if a receipt-scanned directory is used, its JSON must carry the required final `tree` and matching `tree_binding`; (6) red/baseline evidence keeps its actual source identity — never relabel as final-source execution; (7) benchmark/mutation archives must not import non-receipt JSON into receipt-scanned paths; (8) `artifacts/` stays untracked; auto-commit only the task’s source/test/docs paths.

CHECK re-gather: owner **CHECK**; maximum **2** targeted scout invocations over existing sources/artifacts only (recover a missing locator, clarify existing evidence or verify provenance — never a new experiment or hidden DO iteration). After exhaustion: missing proof → evidence-gap verdict; persistently low confidence → orchestrator invokes `escalate` (trigger 5); a missing report alone does not justify a contract revision or another DO attempt.

### Design checklist

- 🔴 **[Correctness/cancellation]** no normal partial-result return at the three sites; public cancellation must not be masked.
- 🔴 **[Cache/state]** no sticky `Cache` writes; preparation failure cannot reach `_isPrepared` assignment or cache storage.
- 🔴 **[Evidence]** dominated guards, empty loops, skipped providers and red failures classified truthfully.
- 🟡 **[SOLID/DRY]** reuse the framework cancellation primitive and the #207 pattern; no new abstraction for three identical local checks.
- ℹ️ **[Type design]** no new production types/public members/inheritance/signatures; test-only access must not leak into production APIs.
- ℹ️ **[Structural sealedness]** no new inheritable production type or changed hierarchy; sealing ratio not actionable here.
- 🟡 **[Performance]** no allocations, LINQ materialization, additional visitors or per-row work — yet the mandatory query-path benchmark still runs.
- 🟡 **[Test design]** independent baseline expectations, both token-preservation arms, both hash flags, real materialization, scoped mutation.
- ℹ️ **[Documentation]** internal remainder update only unless the public-doc search proves otherwise.

### Classification / confidence / feasibility

Initial PLAN; no DO→PLAN candidate and no revision supersession. **Feasibility: `ready`** for D241.0 and gated sequential DO. Confidence high for the three local replacements, the #207 pattern, cache-store unreachability and the available test harness; empty-shape reachability and canonical CLI admission are explicit bounded prerequisites, not assumed successes. No external blocker established. A failed preflight is a provisional DO report (in-cycle precondition / acceptable risk / evidence gap / true blocker), not STOP or completion. CHECK owns final acceptance; only then may the collection auto-commit T241’s tracked changes on `1.0.9-rc3`.

### Gate-1 self-check

Goal/negative criteria ✔; alternatives ✔; gaps ✔; footprints ✔; variant matrix (18 rows) ✔; red/green selectors ✔; unit briefs JSON ✔; docs plan ✔; performance decision (required, argued) ✔; reconnaissance decision (no spike, obtainability + red arm in DO) ✔; unit execution mode (sequential, current tree) ✔; design checklist ✔; priorities ✔; nine-row frozen contract rv=1 with command/exit/artifact/owner/applicability ✔; canonical dual-admission ✔; finite CHECK re-gather budget (2, owner CHECK) ✔; handoff metadata ✔.

## PLAN handoff (to the collection parent)

plan_handoff:
  task: T241
  issue: "#241"
  variant: pdca-dotnet
  collection: 1.0.9-rc3-open-2
  group: G1
  sequence: "T241 -> T242"
  cycle: 1
  r: 1
  n: 1
  rv: 1
  plan_state: ready
  goal: "Uniform OCE propagation in sorting/grouping preparation without partial results, API changes or cache-policy changes."
  acceptance_count: 6
  engineering_requirement_count: 6
  requirements: "R241-01 .. R241-12"
  plan_footprint_production: [src/nextorm.core/Query/QueryCommand.QueryPreparer.cs]
  plan_footprint_tests_planned: [tests/nextorm.core.tests/SortingGroupingCancellationTests.cs, tests/nextorm.sqlite.tests/SortingGroupingCancellationTests.cs, tests/nextorm.integration.tests/CommonTestSuite.SortingGroupingCancellation.cs]
  plan_footprint_docs: [docs/specs/design/code-smells-review.md, docs/specs/status/D241-sorting-cancellation-1.md]
  plan_footprint_evidence: [artifacts/pdca/D241/rv1/]
  footprint_uncertainty: ["test destinations/names are planned (confirm class/access conventions before writing)", "empty-key accepted-input reachability resolved in D241.0", "evidence helper/config/reports are planned artifacts", "public-doc additions require routing"]
  predecessor_outputs_required: ["#207 pattern delivered in ec42c960", "D207 status/commands are precedent, not a substitute for T241 execution", "no output from T242 required"]
  assumptions_prerequisites: ["baseline 83e78d37; preserve unrelated work", ".NET 10 toolchain + scoped Stryker 5.0.0", "container-backed providers run via documented Podman setup", "D241.0 dual-validator probe succeeds within two rounds", "accepted empty-key counterexample returns to PLAN (oracle unchanged)", "sequential current-tree execution; no worktree/merge/push", "auto-commit follows accepted CHECK; artifacts stay untracked"]
  refs: {issue: "#241 (URL https://github.com/AlexeyShirshov/nextorm/issues/241)", register: "docs/specs/design/code-smells-review.md:8346-8354", predecessor_status: "docs/specs/status/D207-projection-cancellation-1.md", pattern_commit: ec42c960, performance_contract: "docs/specs/performance/acceptance-benchmarks.md"}
  contract_rows_count: 9
  required_rows: [E241-ADMIT, E241-RED, E241-TEST, E241-GUARD, E241-CONS, E241-COV, E241-MUT, E241-PERF, E241-HANDOFF]
  evidence_dir: artifacts/pdca/D241/rv1/
  evidence_contract_source: artifacts/pdca/D241/rv1/evidence-contract.json
  frozen_manifest: artifacts/pdca/D241/rv1/manifest.json
  baseline_ref: 83e78d377cee5f307ecea7fc9cc0307e7cdfab78
  manifest_admission: "real tree OID; pdca_evidence.py lint rc=0 AND validate_inner_loop.py manifest rc=0"
  check_regather: {owner: CHECK, budget: 2, scope: "targeted scout over existing sources/artifacts only"}
  status_file: docs/specs/status/D241-sorting-cancellation-1.md

## ACT (r=1, rv=1) — closed

**Cycle:** N=1, plan revision r=1, attempt n=1; CHECK verdict **PASS** (rv=1) — no loop-back, no further DO iteration. Re-gather budget 0/2 used.

### Done / Verified

The frozen evidence manifest `artifacts/pdca/D241/rv1/manifest.json` carries all **9** required contract rows as `met`: **E241-ADMIT, E241-RED, E241-TEST, E241-GUARD, E241-CONS, E241-COV, E241-MUT, E241-PERF, E241-HANDOFF** (tree `2a6519e56ebe7bb6466066a6c16a6f33bcab6f4d`, `base_ref` `83e78d37`), validated by both `pdca_evidence.py lint` rc0 and `validate_inner_loop.py manifest` rc0.

- **OCE oracle** — pre-cancelled token now throws `OperationCanceledException` on all three paths (`QueryCommand.QueryPreparer.cs:2716` sorting, `:3488` key-columns, `:3528` grouping); active-token and `CancellationToken.None` preserve SQL, key-column order/count, plan hashes and results. RED (unchanged production): core 3/3, sqlite 4/4 failed ("OperationCanceledException expected … no exception was thrown"); GREEN: core 5/5, sqlite 6/6, integration 4/4 (PostgreSQL, SQL Server, MySQL, SQLite).
- **Builds** — Debug and Release `0 warnings / 0 errors`. **Boundary** — 12/13 frozen test projects exit 0 (selected 12175 / failed 0 / skipped 207); providers executed sqlite, postgres, sqlserver, mysql, clickhouse; `nextorm.alias.poc` is a non-test Exe (recorded; consumer smoke rc0).
- **Coverage** — line 88.2618 → 88.2656 % (+0.004 pp), branch 80.3412 → 80.3444 % (+0.003 pp); changed lines 2716/3488/3528 covered; thresholds 85/75 satisfied (warn-only on the rc branch).
- **Mutation** (Stryker 5.0.0) — 26 killed / 8 survived / 2 no-coverage / 8 ignored; score 72.22 % (≥ low 70). Sites A/B guard-removal kills accepted; site C (`:3528`) dominated guard — its "kill" was order-dependent and is **not** accepted as cancellation evidence (dominance documented).
- **Perf** — 7/7 acceptance cases, 0 failures, wall 45.9 s; `Cached_ToList/Prepared_ToList` = 1.661 (baseline 1.87, trigger 2.244 — not triggered).
- **Suppression/slop** 0:0; **security** none; `git diff --check` rc0; CRLF preserved.

### CHECK verdict

**PASS** — r=1 / rv=1, no loop-back, no blocking findings. Accepted ℹ️ residuals: (1) dominated `:3528` guard + 8 survivors on unchanged `sortingPlanHash` arithmetic (`:2728/:2733/:2736`); (2) ClickHouse is not a `CommonTestSuite` provider (no `T241_` integration case) but executed in the boundary sweep; (3) `nextorm.alias.poc` non-test Exe; (4) register `:8354` historical note vs `:8358`.

### Changed files

1. `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`
2. `tests/nextorm.core.tests/SortingGroupingCancellationTests.cs`
3. `tests/nextorm.sqlite.tests/SortingGroupingCancellationTests.cs`
4. `tests/nextorm.integration.tests/CommonTestSuite.SortingGroupingCancellation.cs`
5. `docs/specs/design/code-smells-review.md`
6. `docs/specs/status/D241-sorting-cancellation-1.md`

### Next plan

— (flow closed for T241; sorting/grouping cancellation is uniform). Deferred with trigger: V241-18 mid-loop (`k > 0`) atomicity. Follow-up owner: **T242 (#242)** in the same collection `1.0.9-rc3-open-2` / milestone `1.0.9-rc3`.

## Progress log

2026-10-16T16:31:07Z | DO | r1 | n1/3 | DO started for T241 (D241.0-D241.3); single-lane current tree, no worktree/branch/commit/push; evidence $E = artifacts/pdca/D241/rv1. | artifacts/pdca/D241/rv1/
2026-10-16T16:31:07Z | DO | r1 | n1/3 | Notice: pdca-dotnet sidebar todo tracker unavailable in this orchestrator session (no todowrite tool in the Code Mode catalog) — the todo mirror could not be maintained; this status file is the source of truth. | docs/specs/status/D241-sorting-cancellation-1.md
2026-10-16T17:00:00Z | DO | r1 | n1/3 | D241.1 baseline+red: baseline build rc0 0w/0e (63.26s); coverage collect rc0 over the workflow pair with DOCKER_HOST (12160 tests, 0 failed, 207 skipped, line 88.8%, branch 80.3%, archived under baseline/coverage); RED genuine against original source digest 98eb80d4 (core 3/3 failed, sqlite 4/4 failed - each 'Expected OperationCanceledException ... but no exception was thrown', i.e. the pre-fix partial result); pre-fix positive oracles confirmed (core 2/2, sqlite 2/2 passed) freezing the SQL literals. | artifacts/pdca/D241/rv1/red/outcome.json
2026-10-16T17:00:00Z | DO | r1 | n1/3 | D241.1 fix+focused: three cancellation checks in src/nextorm.core/Query/QueryCommand.QueryPreparer.cs replaced by cancellationToken.ThrowIfCancellationRequested() (PrepareSorting, BuildKeyColumns, PrepareGrouping; diff 3 insertions / 6 deletions, file digest 8716c2e4); focused green all rc0 - core 5/5, sqlite 6/6, core T241_PreCancelled 3/3, sqlite T241_PreCancelled 4/4, integration 4/4 (Sqlite/Postgres/SqlServer/MySql, 0 skipped); CRLF and git diff --check clean; report validated (validate_inner_loop.py report rc0). | artifacts/pdca/D241/rv1/units/D241.1/report.json
2026-10-16T17:40:27Z | DO | r1 | n1/3 | D241.2 guards: `git diff --check` rc0; `dotnet build nextorm.slnx -c Debug` and `-c Release` both rc0 with 0 Warning(s)/0 Error(s); production diff = 3 hunks / 3 `ThrowIfCancellationRequested` lines added / 6 partial-exit lines removed; CRLF-only (0 lone LF); Roslyn-confirmed three throwing sites (2716/3488/3528), absent partial exits and unchanged API + cache-store ordering; production digest 8716c2e4. | artifacts/pdca/D241/rv1/guard/{outcome.json,build.log,semantic-findings.md,api-cache-crlf.json}
2026-10-16T17:40:27Z | DO | r1 | n1/3 | D241.2 boundary: frozen 13-project inventory swept with `dotnet test <project> -c Debug --no-build` + DOCKER_HOST — 12 test projects rc0 (selected 12175 / passed 11968 / failed 0 / skipped 207); `nextorm.alias.poc` is a non-test Exe (`dotnet test` rc1 'No test projects were found', recorded) covered by its consumer smoke rc0; providers executed: sqlite, postgres, sqlserver, mysql, clickhouse (0 unavailable skips). | artifacts/pdca/D241/rv1/boundary/{summary.json,providers.json}
2026-10-16T17:40:27Z | DO | r1 | n1/3 | D241.2 coverage: workflow-equivalent pair rc0 (collect 12175 tests / 0 failed / 207 skipped, reportgen rc0); delta vs baseline line 88.2618%->88.2656% (+0.004pp) and branch 80.3412%->80.3444% (+0.003pp); denominators -3 lines / -6 branches = the three removed if-decisions; changed lines 2716/3488/3528 covered (hits 1); 85%/75% thresholds satisfied, warn-only on branch 1.0.9-rc3. | artifacts/pdca/D241/rv1/coverage/{final/delta.json,analysis.md}
2026-10-16T17:40:27Z | DO | r1 | n1/3 | D241.2 mutation (Stryker 5.0.0, spans resolved via Roslyn, rc0, 21m46s, score 72.22%): 44 in-span mutants, 36 tested (26 killed / 8 survived / 2 no-coverage); sites A(2716) and B(3488) guard-removal killed by the cancellation oracles; site C(3528, pre-edit 3530) reported killed only by order-dependent non-cancellation tests and NOT accepted as cancellation evidence (not reproducible; guard reachable for single-column keys; mid-loop k>0 residual stays deferred); 8 hash-arithmetic survivors explained, 2 no-coverage outside the contract. | artifacts/pdca/D241/rv1/mutation/{analysis.md,tally.json,run.log}
2026-10-16T17:40:27Z | DO | r1 | n1/3 | D241.2 perf: 7/7 acceptance cases, 0 failures, external wall 45.9s (<=240s), Cached_ToList/Prepared_ToList = 1.661 (baseline 1.87, trigger 2.244; two bring-up runs 1.816 / 2.030 disclosed, no breach). | artifacts/pdca/D241/rv1/perf/{summary.json,analysis.md}
2026-10-16T17:40:27Z | DO | r1 | n1/3 | D241.2 done: run_evidence.py extended with guard/boundary/coverage-final/coverage-delta/mutation-review/perf stages (each rc0 on its predicate); brief validated before running; units/D241.2/report.json (17 executions) validated by `validate_inner_loop.py report` rc0; no commit/push. | artifacts/pdca/D241/rv1/units/D241.2/{report.json,stages.json}
2026-10-16T17:40:45Z | DO | r1 | n1/3 | D241.2 mutation site-C verification: line 3528 was temporarily patched to `;` (production file byte-copied first), the two attributed killer tests still failed in isolation on the UNMUTATED source, and the file was restored byte-identically (digest back to 8716c2e4; `git diff --stat` unchanged 3 insertions / 6 deletions) — the reported kill is order-dependent-test noise, not cancellation evidence. | artifacts/pdca/D241/rv1/mutation/analysis.md
2026-10-16T17:40:45Z | DO | r1 | n1/3 | D241.2 hygiene: the prescribed acceptance benchmark regenerated 15 tracked `benchmarks/BenchmarkDotNet.Artifacts/results/*` files; the raw outputs were archived under `perf/archive/results/` and the tracked copies restored to HEAD, so the only tracked change left by D241.2 is none (production fix only); `git diff --check` rc0. | artifacts/pdca/D241/rv1/perf/archive/**
2026-10-16T17:48:00Z | DO | r1 | n1/3 | D241.3 register: `docs/specs/design/code-smells-review.md` Находка 241 remainder marked resolved by #241 (realized sites post-edit :2716/:3488/:3528, pre-edit :2716/:3489/:3530; mirrors #207 ec42c960; OCE oracle; residual = dominated :3528 guard + deferred k>0 mid-loop atomicity); #207 projection history retained; CRLF-only, 6 insertions; diff archived. | artifacts/pdca/D241/rv1/handoff/register.patch
2026-10-16T17:48:00Z | DO | r1 | n1/3 | D241.3 public-doc search (EN+RU; docs/** except docs/specs/**, readme.md): no public page documents a sorting/grouping cancellation contract; the only preparer mention is descriptive pipeline prose at docs/engine/01-query-pipeline.md:13 and needs no change; no public page updated. | artifacts/pdca/D241/rv1/handoff/docs-search.log
2026-10-16T17:48:00Z | DO | r1 | n1/3 | D241.3 canonical manifest: generate rc0 over contract rv=1; frozen tree 2a6519e56ebe7bb6466066a6c16a6f33bcab6f4d (git cat-file -t => tree; independent recompute equal); `pdca_evidence.py lint` rc0 AND `validate_inner_loop.py manifest` rc0; 9/9 required rows met with existing evidence. | artifacts/pdca/D241/rv1/handoff/admission.log
2026-10-16T17:48:00Z | DO | r1 | n1/3 | D241.3 handoff+report: T241->T242 handoff frozen (variant/r/rv, changed files, 9 rows, residuals, artifacts/** untracked, no commit, #241 not closed); units/D241.3/report.json validated by `validate_inner_loop.py report` rc0; DO does not self-verify. | artifacts/pdca/D241/rv1/handoff/summary.json
2026-10-16T17:55:03Z | ACT | r1 | n1/3 | ACT: status finalized; CHECK PASS r1/rv1; all 9 contract rows met; re-gather 0/2. | docs/specs/status/D241-sorting-cancellation-1.md
2026-10-16T17:55:03Z | ACT | r1 | n1/3 | Auto-commit of the T241 footprint (0ec7bc2c2c1a8551fa98402adc6267c7ca5c5a28); not pushed; issue #241 left open (collection parent owns closure). | 0ec7bc2c2c1a8551fa98402adc6267c7ca5c5a28
