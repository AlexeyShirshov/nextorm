# D240 - #240 Perf: fluent command-construction allocation cost (cycle 1)

## Metadata
- Issue: #240 - https://github.com/AlexeyShirshov/nextorm/issues/240 - milestone **1.0.9-rc3** (verified 2026-10-16).
- Collection: `1.0.9-rc3-open` (single group; collection mode autonomous + auto-commit; no worktree/group-branch/task-branch/merge; push never).
- Repo/branch/base: `/home/alex/sources/nextorm`, branch `1.0.9-rc3`, base HEAD `c7ff89ca`.
- Task id: `T240`; cycle N: 1; plan revision r: 1; attempt n: 1 (DO started 2026-10-16, unit D240-0).
- selected_variant: `pdca-dotnet`; plan_state: `ready`.
- Canonical status path: `docs/specs/status/D240-construction-allocation-1.md`.
- Design spec (APPROVED 2026-10-16): `docs/specs/design/issue-240-construction-allocation-budget.md` (spec_version 2026-10-16).
- Frozen evidence-contract revision: `rv1`; frozen evidence manifest: `artifacts/pdca/D240/rv1/manifest.json`.
- Baseline ref: `c7ff89ca` (branch `1.0.9-rc3`).

## Current state
- Phase: **ACT (closed)**.
- Cycle: N=1; plan revision r=1; evidence-contract revision rv1.
- CHECK verdict: **FAIL** (performance acceptance NOT met).
- Terminal disposition: **ESCALATED TO OWNER (spec §8)** - acceptance NOT MET.
- No further cycle of this task until the owner decides.

## Applicability amendment (DO start)
- DO tree: HEAD `be0cbadbae585e2d02629dc1fa17a6a7f4dc7994` (branch `1.0.9-rc3`), 5 commits ahead of the PLAN base `c7ff89ca`.
- T240 target files are unchanged since `c7ff89ca`: `git diff --stat c7ff89ca..be0cbadb -- src/nextorm.core/Query/QueryCommand.cs src/nextorm.core/Query/QueryCommand.Clone.cs src/nextorm.core/Query/QueryCommand.TResult.cs src/nextorm.core/Builders/EntityBuilder.cs` is empty, therefore every PLAN anchor (file:line) still holds.
- Predecessor `#207` (`ec42c960`, uniform projection-build cancellation) modified the PROTECTED `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` (9 insertions / 15 deletions). Therefore **EC240-14's protected-path diff must be evaluated against the DO base `be0cbadb`** (T240 must not touch protected files), NOT against `c7ff89ca`.

## Goal (essence)
Reduce allocations on the shared (all-provider) fluent command-construction path so that EACH of the five Category-B scenarios of `SqliteBenchmarkFeaturesFairCached` (Cte, RecursiveCte, Except, Intersect, Distinct) allocates <= 1.20 x LINQ to DB in the same round, while time stays <= 1.05, the D201-3 caching recipe is untouched, the public API, generated SQL and results are unchanged, and the "fresh fluent query per call" workload is preserved. No cross-scenario averaging.

## Prior terminal reason (required re-entry input)
#201/T201 terminal `incomplete` (`docs/specs/status/D201-cached-parity-1.md:9-10`, STOP `:1501-1551`): allocations only, time MET. Cycle N=1, r=2, rv1, n=1/3; EC201-02..06 FAIL. Frozen same-round B/op candidate vs LINQ: Cte 115130/92802, RecursiveCte 144243/87522, Except 71523/51760, Intersect 71363/51280, Distinct 43440/40080. D201-3 recipe contributed 20409/36706/21567/21630/9587 B/op = 91.4/64.7/109.1/107.7/285.3 % of the required gap -> recipe alone could not close Cte/RecursiveCte. #201 closed `not planned`; its frozen alloc criterion is NOT reused. T240 does NOT re-run that failed proof: it cuts the non-recipe construction cost by structural reduction plus ownership-proven pooling.

## Acceptance criteria (observable + negative)
- R240-01..05 (per scenario: Cte / RecursiveCte / Except / Intersect / Distinct): same-round allocated-byte ratio <= 1.20 in BOTH valid rounds; any missing or failing arm fails; no averaging. [spec sections 2, 9; `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkFeaturesFairCached.cs:29`]
- R240-06: corresponding mean-time ratio <= 1.05 in both rounds. [spec section 6]
- R240-07: public surface and source compatibility unchanged (successful consumer build + Roslyn public-symbol inventory diff). [spec section 6; `src/nextorm.core/Query/QueryDefinition.cs:18`; `src/nextorm.core/DataContextExtensions.cs:19-21`]
- R240-08: immutability - after any fluent step the previous `QueryCommand`/builder and sibling branches are unchanged, incl. after reuse/preparation. [spec section 4; `src/nextorm.core/Query/QueryCommand.TResult.cs:630,798,1017-1097`; `src/nextorm.core/Builders/EntityBuilder.cs:2239-2297`]
- R240-09: identical SQL/results and preserved cache behaviour on all applicable providers; a skipped required provider is not green. [spec section 6; `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:3599-4291`; `src/nextorm.core/DataContext/QueryPlanner.cs:608-651`]
- R240-10: warmed construction-only allocation improves for selected arbitrary non-benchmark shapes; unchanged control shapes must not regress repeatably. [spec section 9; `src/nextorm.core/Builders/EntityBuilder.cs:343,460`]
- R240-11: no retained scratch buffers, no unbounded retained-memory growth, no cross-branch contamination, no concurrency corruption. [spec section 6; `src/nextorm.core/Query/QueryCommand.cs:193,214`; `src/nextorm.core/Builders/EntityBuilder.cs:2254-2261`]
- R240-12: acceptance category remains exactly 7 cases and wall clock <= 240 s. [`docs/specs/performance/acceptance-benchmarks.md:37-45,114-116`]
- R240-13: coverage >= line 85 / branch 75 (`.github/workflows/dotnet.yml:27-28`, scope `coverage.settings.xml`); changed P1 branches covered; mutation plan executed.
- R240-14: protected recipe/snapshot paths unchanged (`git diff --exit-code c7ff89ca -- <protected>`).
- Negative cases (any one -> FAIL): any scenario > 1.20; any time ratio > 1.05; any public-API/SQL/result change; any leak/unbounded growth; any required provider skipped; any averaging or best-round selection.

## Oracle decision (<= 1.20 same-round)
The <= 1.20 same-round ratio has NO committed implementation: `scripts/perf/t201_evidence.py` alloc verdict is `MET iff NextORM <= LINQ` and its pairing oracle `oracle/b-arms.json` is uncommitted (only in untracked `artifacts/pdca/T201/rv1/oracle/`, generated by an ephemeral `python3 -` heredoc; `git ls-files artifacts/pdca/T201` is empty). Decision (confidence 0.88): commit a thin deterministic adapter `scripts/perf/t240_evidence.py` that pairs the EXISTING committed benchmark arms (derived from method/attribute names - no guessed symbols), reads exported BDN `Memory.BytesAllocatedPerOperation` + `Statistics.Mean`, enforces fixed limits 1.20 (alloc) / 1.05 (time), rejects duplicate/missing/NA arms, and reports each round separately. It reproduces the already-approved BDN same-round comparison; it is NOT a new allocation-oracle (spec section 10 non-goal). Fallback: if spec section 10 wording is held to forbid even this reproducibility adapter, STOP before implementation and request owner escalation (spec section 8) - no heredoc/untracked workaround, no threshold weakening.
Across-round rule (frozen): MET only if EVERY valid round is MET for time and allocations; FAIL if any round fails allocations; preserve all valid rounds, no cherry-picking, never select the best; a round is invalidated only for a recorded operational reason established independently of its ratio. [`docs/specs/performance/m12-3-prepare-command-results.md:90-91,106-109,124-126`; `docs/specs/performance/m12-3-prepare-command-design.md:513-546`]

## Oracle interpretation (escalate decision; owner confirmation pending)
- The escalate decision is implemented as a NEW committed script `scripts/perf/t240_evidence.py`: a reproducibility READER over the existing BDN same-round comparison (BenchmarkDotNet + same-round NextORM-vs-LINQ-to-DB ratios). It introduces no new measurement methodology and is NOT a new allocation-oracle (design spec §10 non-goal).
- `scripts/perf/t201_evidence.py` and every `#201` artifact are untouched; the T240 adapter is a separate file with its own self-tests (`scripts/perf/tests/test_t240_evidence.py`).
- Fallback if the owner holds §10 wording to forbid even this reproducibility adapter: STOP before product DO and request owner escalation (spec §8); no heredoc/untracked workaround, no threshold weakening.
- **Owner confirmation pending**: the §10 wording is marked for explicit owner confirmation before T240 relies on the adapter as the acceptance oracle.

## Reconnaissance (needed; already performed in PLAN)
Probe is reproducible from committed benchmark sources: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*Cte*' --exporters json` -> exit 0, wall 47.2 s (BDN runner 41.99 s); same-round ratios Cte 1.2271, RecursiveCte 1.6787 vs frozen 1.2406 / 1.6484 (delta -1.1 % / +1.8 %). The decisive probe runs and reproduces the red state (> 1.20). Harness caveats pinned: set `NEXTORM_BENCH_DB=/home/alex/sources/nextorm/benchmarks/nextorm.benchmark/data/test.db` (committed; `dotnet run --project` keeps cwd = repo root); keep the default in-process `Job.ShortRun` config (`benchmarks/nextorm.benchmark/Infrastructure/NextormConfig.cs:23-25`); untracked gitignored `artifacts/**` duplicates break the OUT-OF-PROCESS job, so do not use `NEXTORM_BENCH_FULL=1`. Bounded remaining recon in DO: audit definition-affecting mutations/collection escapes and attribute RecursiveCte construction allocations before choosing copy-on-write or pooling.

## Variant matrix (every row closed)
| Scenarios x arm x sub-path | Surface (cached / prepared / in-memory) | Providers | Closure |
| --- | --- | --- | --- |
| Five x structural x `Definition` reads | All three (test) | General path | One-pass clone snapshot; getter memoization NOT used without complete invalidation proof |
| Five x structural x join / array-join copies | All three (test) | General path | External arrays stay defensively copied; internal copy elision only with exclusive/frozen ownership proof |
| Five x structural x six builder collections | All three (test) | General path | Empty/nonempty tests; branch-after-share mutation tests; lazy allocation first, COW only if needed |
| Five x structural x set-op / Distinct clone | All three (test) | General path | New command per step preserved; left/right operand reuse, nested and repeated operators |
| Five x structural x construction `ToArray()` | All three (test) | General path | Remove only redundant measured materialization; retained collection copies are guards, not failures |
| Five x scratch x each sub-path | Test iff a measured removable temporary-buffer consumer exists | General path | Else deferred + trigger: activate only if structural arm stays red AND attribution identifies removable scratch |
| Five x both arms x provider behaviour | Cached/prepared SQL + integration; in-memory core | SQLite / Postgres / SQL Server / MySQL / ClickHouse (MariaDB inherits MySQL) | General-path implementation; provider-specific SQL/results tests |
| All changed paths x input shapes | Test supported; guard unsupported | Empty / single / many; reference vs value projection; nullable/default constants; captured/non-captured expressions; flags/settings | Preserve existing acceptance/rejection; never broaden API behaviour |
| Pool ownership (when activated) | Test normal / exception / parallel | Null-containing references; oversized buffers; repeated rent/return | `finally` return, reference clearing, size cap; no rented array reachable from retained objects |

## Test strategy (per-unit scope)
Unit tests cover construction, defensive copying, clone isolation, arbitrary-shape allocation and cache-state preservation; dialect tests compare exact pre-edit SQL; integration validates real results/cache reuse. Use planned trait selectors (not invented symbols).
| Unit | Affected project + exact planned filter | Required cases |
| --- | --- | --- |
| D240-1 | core: `Category=T240Baseline` | Original SQL, allocation samples, ownership/preparation observations |
| D240-2 | core: `Category=T240Command` | Snapshot completeness, caller arrays, set-ops, Distinct, stale state, generic variants |
| D240-3 | core: `Category=T240Builder` | Six collections empty/populated; independent siblings; arbitrary graphs |
| D240-4 (if activated) | core: `Category=T240Scratch` | Exception return/clearing, bounded retention, concurrent independent construction |
| D240-5 | core: `Category=T240GeneralAllocation`; each dialect: `Category=T240SqlParity`; integration: `Category=T240ProviderParity` | Construction deltas; exact SQL; real provider results/cache reuse |
Single boundary sweep: after D240-5, before DO->CHECK, run all core/dialect suites and the full container-backed integration once (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`); earlier units run only their affected subsets. Branch-delta: cover every changed decision's positive/negative branch; targeted temporary mutations must make relevant tests fail (omit defensive copy, omit COW detachment, suppress scratch return), then restore and re-run. Coverage targets: line 85 %, branch 75 % (configured assembly scope); changed-path/P1 evidence is not waived.

## Docs plan
No user-visible change -> no public EN/RU edits expected; update internal status/performance evidence and issue #240; do not reopen M12. Any unexpectedly changed public declaration requires XML-doc/CS1591 compliance and renewed API review; public rename/removal remains prohibited. (`src/Directory.Build.props:11` GenerateDocumentationFile=true; CS1591 not suppressed.)

## Perf measurement decision (needed)
Required. Historical reference: frozen D201-5 round, `docs/specs/performance/m12-3-prepare-command-results.md:93-104`; decisive comparisons use same-round LINQ. Capture a fresh five-scenario pre-edit baseline, then two final rounds with identical benchmark configuration/database/machine; preserve all completed valid rounds, never select the best. Use the committed SQLite db via `NEXTORM_BENCH_DB`; keep the demonstrated default (in-process ShortRun) config; do not clean unrelated artifacts. Mandatory cached-path acceptance: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` (exactly 7 cases, <= 240 s incl. wall). Intermediate deltas below ~20 % are inconclusive sanity signals, not justification for extra complexity; final approved ratios are hard limits.

## Unit execution mode
Sequential, one tree. Commands, builders, ownership contracts and tests overlap deeply (shared mutable core files) -> sequential; no worktrees, branches or merges. This cycle's deliverable is PLAN only; no implementation or commit here.

## Evidence contract (rv1; frozen before product DO)
Manifest: `artifacts/pdca/D240/rv1/manifest.json`; evidence root `artifacts/pdca/D240/rv1/`. Command templates (expand scenario/project/round args into manifest entries): E = `artifacts/pdca/D240/rv1`; T(p,k) = `dotnet test tests/nextorm.<p>.tests -c Debug --filter "Category=T240<k>"`; B(i) = `env -u NEXTORM_BENCH_FULL NEXTORM_BENCH_DB=/home/alex/sources/nextorm/benchmarks/nextorm.benchmark/data/test.db dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --exporters json --artifacts artifacts/pdca/D240/rv1/round-<i>`; G = `python3 scripts/perf/t240_evidence.py --manifest artifacts/pdca/D240/rv1/manifest.json --round artifacts/pdca/D240/rv1/round-1 --round artifacts/pdca/D240/rv1/round-2 --output artifacts/pdca/D240/rv1/ratios.json`; A = acceptance command above with `/usr/bin/time -p`; F = core+5 dialect test suites; I = container integration run.
| Row / requirement | Check and invocation | Evidence -> artifact | Owner; applicability |
| --- | --- | --- | --- |
| EC240-01 / R240-01 | B(1), B(2), G; Cte alloc <= 1.20 twice | BDN JSON + paired verdict -> `round-*/`, `ratios.json` | perf; always |
| EC240-02 / R240-02 | Same; RecursiveCte <= 1.20 twice | Same | perf; always |
| EC240-03 / R240-03 | Same; Except <= 1.20 twice | Same | perf; always |
| EC240-04 / R240-04 | Same; Intersect <= 1.20 twice | Same | perf; always |
| EC240-05 / R240-05 | Same; Distinct <= 1.20 twice | Same | perf; always |
| EC240-06 / R240-06 | G; all five times <= 1.05 twice | BDN means + verdict -> `ratios.json` | perf; always |
| EC240-07 / R240-07 | build + Roslyn public-symbol inventory/diff vs base | build exit + API inventory -> `api/` | code/check; always |
| EC240-08 / R240-08 | T(core,Command), T(core,Builder) | assertions + mutation negatives + ownership audit -> `immutability/` | tests; always |
| EC240-09 / R240-09 | T(<p>,SqlParity) x5; T(integration,ProviderParity); F; I | SQL baseline equality, results/cache assertions, provider counts -> `functional/` | tests; always |
| EC240-10 / R240-10 | T240_PHASE=baseline core baseline then compare-after | fixture counters + matching shape IDs -> `general-baseline.json` + logs | tests/perf; always |
| EC240-11 / R240-11 | T(core,Builder); T(core,Scratch) if pool introduced | forced-GC retained-memory batches, ownership/return counters -> `memory/` | tests; always (scratch iff rentals) |
| EC240-12 / R240-12 | `A` | case list + elapsed wall clock -> `acceptance/` | perf; always |
| EC240-13 / R240-13 | coverage commands + targeted unit mutations | coverage + changed-branch map + mutant fail/restore -> `coverage/`, `mutations/` | tests/check; always |
| EC240-14 / R240-14 | Roslyn impact/API audit; `git diff --exit-code c7ff89ca -- <protected>` | zero protected diff + inventories -> `guards/` | code/check; always |
Roslyn contract: `tools.roslyn({action:"structure",solution:"/home/alex/sources/nextorm/nextorm.slnx"})` then `members`/`refs`; no text-search substitute. Memory protocol: ten warmed batches of 10,000 fresh constructions with forced GC between batches, zero outstanding rentals. CHECK re-gather: owner `check`; at most 2 targeted batches / 15 minutes total, in addition to mandatory initial evidence; missing reports trigger re-gather, not contract revision or DO; persistent low confidence -> escalate trigger 5. Priority: all EC240 rows are P1 by construction (approved invariants / changed execution paths); CHECK must not downgrade.

## DO task list
- D240-0 (code/evidence): write status/rv1 manifest; obtain exact benchmark arm identities; implement+test the deterministic adapter incl. missing/duplicate/NA/threshold-boundary negatives. Freeze contract before product edits.
- D240-1 (tests/perf): baseline/ownership/SQL/general-shape fixtures; capture original measurements before production changes; complete mutation/escape attribution audit and ownership design.
- D240-2 (code+tests): `QueryCommand.cs:193,214,223-257`, `QueryCommand.Clone.cs:179`, `QueryCommand.TResult.cs:630-1097` - single-pass snapshots, redundant clone/materialization removal; retain required defensive copies; no cached getter initially.
- D240-3 (code+tests): `EntityBuilder.cs:2239-2297,2396-2398` and affected modifier writes - lazy empty collections; ownership-proven populated COW only if necessary; run affected subsets + construction sanity measurements.
- D240-4 (conditional active unit): if structural ratios stay red and attribution identifies scratch savings, implement bounded call-local pooling (rental returned in `finally`, references cleared, oversized retention rejected; only independent exact-size owned outputs escape). Otherwise record guarded non-applicability.
- D240-5 (tests/perf/docs): finish general/provider/memory/mutation evidence; run the single boundary sweep, coverage, the acceptance category and two final rounds; populate evidence + issue/status. Implementation reports never mark acceptance complete; CHECK decides.

## Risks
- High: RecursiveCte needs ~36 % non-recipe reduction; copy-on-write aliasing; definition staleness; preparation/cache-state interactions. Mitigation: attribution, explicit ownership, negative tests.
- High: pooled storage escapes or concurrent corruption; no rental-backed retained object permitted.
- Medium: noisy timing/memory; matched configs and both rounds.
- Medium: provider infrastructure; load `running-integration-tests` skill, start Podman if needed, never count skips as green.

## Deferred + trigger (stay in milestone 1.0.9-rc3)
- Getter memoization: deferred until all definition-affecting mutations and invalidation are proven.
- Scratch pooling: conditional D240-4 trigger above.
- Unrelated LINQ/loop cleanup, streaming pools, settled M1/M11/I2/I3/M12: deferred unless fresh measurements identify an in-scope construction cause.
- Not deferred (prohibited): D201-3 recipe edits, public struct conversion, benchmark-specific product branches, threshold change.

## Prerequisites / assumptions / open owner decisions
- Prerequisite: the deterministic adapter `scripts/perf/t240_evidence.py` (or an equivalent committed reproducibility adapter) must exist before the five ratios can be judged; if spec section 10 forbids it -> owner escalation (spec section 8) before DO.
- Assumption: committed `benchmarks/nextorm.benchmark/data/test.db` is usable; `DOCKER_HOST`/Podman available for container providers.
- Assumption: across-round rule = conjunction (both rounds pass) per the frozen rule quoted above.
- Open owner decision: none required now; the only escalation gate is the oracle-adapter/section-10 question and the spec section 8 unreachability fallback.

## Write footprint
Expected: `src/nextorm.core/Query/QueryCommand.cs`, `src/nextorm.core/Query/QueryCommand.Clone.cs`, `src/nextorm.core/Query/QueryCommand.TResult.cs`, `src/nextorm.core/Builders/EntityBuilder.cs`; tests under `tests/nextorm.core.tests`, `tests/nextorm.<provider>.tests`, `tests/nextorm.integration.tests`; NEW `scripts/perf/t240_evidence.py`; internal status/performance docs; ignored `artifacts/pdca/D240/rv1/**`. Uncertainty: a small internal core ownership helper may be needed ONLY after a demonstrated second consumer (decided by the escape audit); no benchmark workload/config edits, no dependency changes, no public docs changes expected.
Protected (do not modify): `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/Query/QueryCommand.Plan.cs`, `src/nextorm.core/Query/QueryPlanEqualityComparer.cs`, `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs`, `src/nextorm.core/Expressions/JoinExpressionPlanEqualityComparer.cs`, `src/nextorm.core/DataContext/QueryPlanner.cs`, `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs`.

## Feasibility statement
Plausible, not demonstrated; medium conviction. Repeated record snapshots and six-list copies provide credible structural opportunities, but available evidence does not prove RecursiveCte's required reduction or useful scratch savings. If attribution and the bounded structural/pooling alternatives cannot meet any scenario inside spec section 6, recommend spec section 8 owner escalation with measured residual gaps and ownership constraints. Never lower 1.20, modify the recipe, or call partial improvement complete.

## Plan findings (severity, file:line, one-line fix, invariant)
- [PERF] RED `src/nextorm.core/Query/QueryCommand.cs:223-257` - `Definition` getter allocates a fresh `QueryDefinition` on every read; one-pass clone snapshot (invariant 3/2).
- [PERF] RED `src/nextorm.core/Query/QueryCommand.cs:193,214` - `_joins.Clone()` + `ArrayJoins?.ToArray()` per construction; remove redundant materialization, keep required defensive copies (invariant 3).
- [PERF] RED `src/nextorm.core/Builders/EntityBuilder.cs:2248-2297` - six-list copy per modifier step; lazy empty collections first, ownership-proven COW only if needed (invariant 3).
- [PERF] YELLOW `src/nextorm.core/Query/QueryCommand.TResult.cs:630,654,701,736` - `with`-clones re-read `Definition` (extra record/alloc); follows from the getter fix.
- [PERF] YELLOW `src/nextorm.core/Builders/EntityBuilder.cs:351,467,4162` - `_joins?.ToArray()` / `_sorting?.ToArray()` on build.
- [PERF] INFO `src/nextorm.core/Query/QueryCommand.TResult.cs:1017-1097` - set-ops clone the whole command then set one slot; single-slot clone.
- Guard (do not touch): D201-3 recipe files (see Protected).
- Deferred: getter memoization; scratch pooling (triggers above). Do not reopen M12 (settled; invariant 4).

## DO unit state
| Unit | State | Evidence |
| --- | --- | --- |
| D240-0 | in_progress (deliverables complete; DO->CHECK gate pending) | `scripts/perf/t240_evidence.py` + self-tests 12/12; frozen manifest `artifacts/pdca/D240/rv1/manifest.json`; pre-edit baseline round-0 |
| D240-1 | pending | not started |
| D240-2 | in_progress (axis complete; DO->CHECK gate pending) | `tests/nextorm.core.tests/T240CommandConstructionTests.cs` (11/11); inner-loop report `artifacts/pdca/D240/rv1/inner-loop-D240-2.json` (validator exit 0); defect `D240-SETOP-CHAIN-OPERAND-MUTATION` fixed |
| D240-3 | in_progress (axis complete; DO->CHECK gate pending) | `tests/nextorm.core.tests/T240BuilderConstructionTests.cs` (13/13); inner-loop report `artifacts/pdca/D240/rv1/inner-loop-D240-3.json` (validator exit 0); defect `D240-BUILDER-ORDERING-ALIAS` fixed |
| D240-4 | pending (conditional; activate only if the structural arm stays red and attribution identifies removable scratch) | not started |
| D240-5 | in_progress (boundary sweep + acceptance + manifest freeze complete; DO->CHECK gate pending, acceptance NOT claimed) | build `artifacts/pdca/D240/rv1/build-slnx-boundary.log` (exit 0); sweep `artifacts/pdca/D240/rv1/sweep-*.log`; integration `artifacts/pdca/D240/rv1/integration.log` + retry `artifacts/pdca/D240/rv1/integration-clickhouse-retry.log`; acceptance `artifacts/pdca/D240/rv1/acceptance.log`; frozen manifest validator exit 0 |
| D240-r2.1 | done | patch `artifacts/pdca/D240/rv2/applied.patch` (patch-id 6629dbd193e08668fbeae0a9aba89e9471863f54 == rv1); Debug `artifacts/pdca/D240/rv2/build-debug.log` (exit 0); Release `artifacts/pdca/D240/rv2/build-release.log` (exit 0) |
| D240-r2.2 | done (correctness of the frozen patch; no performance claim) | `artifacts/pdca/D240/rv2/unit-command.log` (11/11, exit 0); `artifacts/pdca/D240/rv2/unit-builder.log` (13/13, exit 0); `artifacts/pdca/D240/rv2/immutability-receipts.json` |
| D240-r2.3 | done | F sweep `artifacts/pdca/D240/rv2/sweep-*.log` (every project exit 0); I `artifacts/pdca/D240/rv2/integration.log` + retry `artifacts/pdca/D240/rv2/integration-clickhouse-retry.log` |
| D240-r2.4 | done (DO->CHECK gate pending; acceptance verdict = CHECK) | `artifacts/pdca/D240/rv2/api-compatibility.json`; `artifacts/pdca/D240/rv2/guards/EC240-14-protected-diff.txt` (exit 0); `artifacts/pdca/D240/rv2/scope-r2.json` + `inner-loop-r2.json` (validators exit 0); `artifacts/pdca/D240/rv2/manifest.json` (validator exit 0); `artifacts/pdca/D240/rv2/act-footprint-precheck.txt` |

## Progress log
2026-10-16 10:05 UTC | PLAN | r1 | n0 | PLAN ready - collection task PLAN complete; handoff to parent (plan_state=ready) | docs/specs/design/issue-240-construction-allocation-budget.md
2026-10-16 13:02 UTC | DO | r1 | 1/3 | DO started - D240-0 evidence adapter + frozen manifest skeleton; no product code, no protected files | docs/specs/status/D240-construction-allocation-1.md
2026-10-16 13:16 UTC | DO | r1 | 1/3 | D240-0 adapter scripts/perf/t240_evidence.py created + self-tests 12/12 (unittest exit 0); manifest validated exit 0; contract rv1 frozen | scripts/perf/t240_evidence.py; scripts/perf/tests/test_t240_evidence.py; artifacts/pdca/D240/rv1/manifest.json
2026-10-16 13:16 UTC | DO | r1 | 1/3 | baseline BDN round-0 rc=0 wall=391s 55 arms; alloc Cte 1.2136 FAIL / RecursiveCte 1.6787 FAIL / Except 1.4034 FAIL / Intersect 1.4134 FAIL / Distinct 1.0838 MET; time MET all; overall FAIL (red baseline, expected) | artifacts/pdca/D240/rv1/round-0.ratios.json
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 start - QueryCommand construction axis; scope frozen and validated (brief exit 0) before any product edit | artifacts/pdca/D240/rv1/scope-D240-2.json
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 product edit - ownership-aware ctor + single-pass Definition/CacheCloneDefinition (QueryCommand.cs); self-clone paths (QueryCommand.Clone.cs:177, QueryCommand.TResult.cs:628) take ownership of the just-cloned joins; OrderBy/ForLast read Definition once (post-assign sorting); set-op chaining no longer mutates a reused operand | src/nextorm.core/Query/QueryCommand.cs; src/nextorm.core/Query/QueryCommand.Clone.cs; src/nextorm.core/Query/QueryCommand.TResult.cs
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 tests added - 11 T240Command cases (snapshot/immutability, sibling branches, set-op/Distinct operand reuse, caller-array defensive copy, repeated/nested operators, null/empty) | tests/nextorm.core.tests/T240CommandConstructionTests.cs
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 regression RED (defect D240-SETOP-CHAIN-OPERAND-MUTATION, pre-existing): `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter Category=T240Command` exit=2 selected=11 failed=1 (NestedSetOperations_KeepOperandsUnchanged: reused operand gained an EXCEPT) | artifacts/pdca/D240/rv1/regression-red-D240-2.log
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 regression GREEN after SetOperation owns a private copy of the chain tail: same filter exit=0 selected=11 failed=0 | artifacts/pdca/D240/rv1/inner-loop-core-D240-2.log
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 dialect SQL parity - `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~SqlGenerationTests` exit=0 selected=736 failed=0; `--filter FullyQualifiedName~PlanKeyUniquenessTests` exit=0 selected=13 failed=0 | artifacts/pdca/D240/rv1/inner-loop-sqlite-D240-2.log; artifacts/pdca/D240/rv1/inner-loop-sqlite-plankeys-D240-2.log
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 build gate - `dotnet build nextorm.slnx -c Debug` exit=0, 0 Warning(s) 0 Error(s); all written files CRLF; inner-loop report validated exit 0; AXIS COMPLETE, acceptance NOT claimed (CHECK pending) | artifacts/pdca/D240/rv1/build-slnx-D240-2.log; artifacts/pdca/D240/rv1/inner-loop-D240-2.json
2026-10-16 13:27 UTC | DO | r1 | 1/3 | D240-2 defect ledger updated | docs/specs/status/D240-construction-allocation-1.md
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 start - EntityBuilder<TEntity> fluent-construction axis; scope frozen and validated (brief exit 0) before any product edit | artifacts/pdca/D240/rv1/scope-D240-3.json
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 product edit - CopyTo shares the never-in-place-mutated collections (_windows, _loadSpecs, _joinIntos) instead of copying them; CopyProjectionIndependentStateTo no longer double-copies _settings; Window/OrderBy detach the shared _windows/_sorting list before the first append; the builder-owned _joins?.ToArray() is handed to QueryCommand via the D240-2 ownJoins seam (no defensive clone) at SelectCore/ToCommand/untyped-Select | src/nextorm.core/Builders/EntityBuilder.cs; src/nextorm.core/DataContext/DataContextExtensions.cs
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 tests added - 13 T240Builder cases (modifier immutability values+SQL, sibling OrderBy/Window independence, collection-free clone holds no empty lists, repeated/nested modifiers, null Where/Having toggles, join/settings/array-join sibling isolation) | tests/nextorm.core.tests/T240BuilderConstructionTests.cs
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 regression RED (defect D240-BUILDER-ORDERING-ALIAS, pre-existing): temporary mutation restoring the pre-fix in-place OrderBy append -> `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~SecondOrderBy_DoesNotGrowTheParentOrFirstBranch` exit=2 selected=1 failed=1 (first branch gained the second sort key) | artifacts/pdca/D240/rv1/regression-red-D240-3.log
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 regression GREEN after restoring the copy-on-write detach: `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter Category=T240Builder` exit=0 selected=13 failed=0 | artifacts/pdca/D240/rv1/inner-loop-core-D240-3.log
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 dialect SQL parity - `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~SqlGenerationTests` exit=0 selected=736 failed=0; `--filter FullyQualifiedName~PlanKeyUniquenessTests` exit=0 selected=13 failed=0 | artifacts/pdca/D240/rv1/inner-loop-sqlite-D240-3.log; artifacts/pdca/D240/rv1/inner-loop-sqlite-plankeys-D240-3.log
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 build gate - `dotnet build nextorm.slnx -c Debug` exit=0, 0 Warning(s) 0 Error(s); all written files CRLF; inner-loop report validated exit 0; AXIS COMPLETE, acceptance NOT claimed (CHECK pending) | artifacts/pdca/D240/rv1/build-slnx-D240-3.log; artifacts/pdca/D240/rv1/inner-loop-D240-3.json
2026-10-16 13:35 UTC | DO | r1 | 1/3 | D240-3 defect ledger updated | docs/specs/status/D240-construction-allocation-1.md

## Defect history (durable)
| defect key | first observed | revisions/attempts | fixes applied | evidence | status |
| --- | --- | --- | --- | --- | --- |
| D240-SETOP-CHAIN-OPERAND-MUTATION | D240-2 DO | r1 / n1/3 | 1 | red `artifacts/pdca/D240/rv1/regression-red-D240-2.log` (exit 2) -> green `artifacts/pdca/D240/rv1/inner-loop-core-D240-2.log` (exit 0) | fixed in D240-2; boundary sweep/CHECK to confirm no regression |
| D240-BUILDER-ORDERING-ALIAS | D240-3 DO | r1 / n1/3 | 1 | red `artifacts/pdca/D240/rv1/regression-red-D240-3.log` (exit 2) -> green `artifacts/pdca/D240/rv1/inner-loop-core-D240-3.log` (exit 0) | fixed in D240-3 (via the copy-on-write detach); boundary sweep/CHECK to confirm no regression |
2026-10-16 13:44 UTC | DO | r1 | 1/3 | D240 rv1 measurement - Release build for the round: `dotnet build benchmarks/nextorm.benchmark -c Release` exit=0, 0 Warning(s) 0 Error(s); outputs current (core dll 18:37:11 local newer than both edited sources) | artifacts/pdca/D240/rv1/build-bench-release-r1.log
2026-10-16 13:44 UTC | DO | r1 | 1/3 | B(1) round-1 BDN run rc=0 wall=397 s (runner 393.41 s), executed benchmarks 55, filter '*SqliteBenchmarkFeaturesFairCached.B_*'; env NEXTORM_BENCH_FULL unset, NEXTORM_BENCH_DB=benchmarks/nextorm.benchmark/data/test.db, DOTNET_GCHeapHardLimit=0x80000000 (default in-process ShortRun job) | artifacts/pdca/D240/rv1/round-1.log; artifacts/pdca/D240/rv1/round-1/
2026-10-16 13:44 UTC | DO | r1 | 1/3 | round-1 ratios (adapter exit=1; alloc tol 1.20, time tol 1.05): alloc Cte 1.1747 MET / RecursiveCte 1.5543 FAIL / Except 1.2890 FAIL / Intersect 1.2979 FAIL / Distinct 1.0239 MET; time 0.5552 / 0.5723 / 0.4614 / 0.4434 / 0.5165 all MET | artifacts/pdca/D240/rv1/round-1.ratios.json
2026-10-16 13:44 UTC | DO | r1 | 1/3 | two-round parity round-0 x round-1 (adapter exit=1): overall FAIL (expected - round-0 is the pre-edit baseline); across-rounds Cte FAIL, RecursiveCte FAIL, Except FAIL, Intersect FAIL, Distinct MET; all five time verdicts MET | artifacts/pdca/D240/rv1/parity-r0-r1.json
2026-10-16 13:44 UTC | DO | r1 | 1/3 | baseline comparison NextORM B/op round-0 -> round-1: Cte 115927->110807 (-5120, 4.4 %), RecursiveCte 144242->137042 (-7200, 5.0 %), Except 72641->66721 (-5920, 8.1 %), Intersect 72481->66561 (-5920, 8.2 %), Distinct 43441->41041 (-2400, 5.5 %); every delta is inside the ~20 % inconclusiveness band, so the structural arm is not yet shown to move the allocations (LINQ arms 95527->94327, 85927->88167, 51762->51762, 51282->51282, 40082->40082); NextORM means +3.3..4.7 % vs round-0 (noise; time still MET) | artifacts/pdca/D240/rv1/round-1.ratios.json; artifacts/pdca/D240/rv1/parity-r0-r1.json
2026-10-16 13:44 UTC | DO | r1 | 1/3 | manifest EC240-01..06 remained `open` with reasons updated to the round-1 numbers: only the first of the two required final rounds exists, so the frozen across-round conjunction cannot be met yet (Cte/Distinct pass round-1 but still need round-2); validator exit 0 | artifacts/pdca/D240/rv1/manifest.json
2026-10-16 13:44 UTC | DO | r1 | 1/3 | acceptance category and the full test sweep deliberately NOT run in this measurement step - both belong to the DO->CHECK boundary step | docs/specs/status/D240-construction-allocation-1.md


## D240-4 - QueryShape / ownJoins construction-path seam completion (rv1 label `D240-4`)
- State: **COMPLETE and GREEN** for the submitted seam (whole-solution build 0 Warning(s)/0 Error(s); inner-loop + dialect parity exit 0). This is the builder-to-command construction seam sub-step; it does NOT activate the plan's conditional scratch-pooling unit and claims no acceptance (CHECK pending).
- Route: **completed** (no escape hatch). The mid-refactor tree compiled after a single XML-doc fix; the `QueryShape` struct + ctors and the `ownJoins` ownership hand-off are adopted at the builder sites, the `CreateSelf`/`CreateSelfForClone`/`ForLast`/`OrderBy`(x3)/`WithPaging`/`CreateDocumentCommand` copy-ctor rewires are intact, and no `CacheCloneDefinition` reference remains (it resolves at build).
- Invariants: no protected file touched; `Definition` stays internal; public ctor / `CreateCommand` signatures unchanged; SQL/results and immutability preserved (dialect parity + construction tests).
- Build gate: `dotnet build nextorm.slnx -c Debug` exit=0, 0 Warning(s) 0 Error(s) -> `artifacts/pdca/D240/rv1/build-D240-4.log`.
- Inner loop: `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter Category=T240Command` exit=0 selected=11 failed=0; `--filter Category=T240Builder` exit=0 selected=13 failed=0 -> `artifacts/pdca/D240/rv1/t240-command.log`, `artifacts/pdca/D240/rv1/t240-builder.log`.
- Dialect parity: `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~SqlGenerationTests` exit=0 selected=736 failed=0; `--filter FullyQualifiedName~PlanKeyUniquenessTests` exit=0 selected=13 failed=0 -> `artifacts/pdca/D240/rv1/sqlite-sqlgen-single.log`, `artifacts/pdca/D240/rv1/sqlite-plankeys-single.log`.
- Inner-loop artifacts: `artifacts/pdca/D240/rv1/scope-D240-4.json` (validator `brief` exit 0) + `artifacts/pdca/D240/rv1/inner-loop-D240-4.json` (validator `report` exit 0). No BDN/benchmark run in this step (none permitted).
- **Notice:** the not-mine validator-policy work `scripts/validate_inner_loop.py` (#235 canonical sync, commit `044d03e2`; #238 `issue-238-pdca-audit-gate-boundaries.md`) is NOT T240's and must NOT enter T240's commit. Observed tree state: the script is committed/clean on this branch (observed HEAD `044d03e2`, one commit ahead of the brief's `be0cbadb`); this step only READ it for the `brief`/`report` gate and did not modify it.

2026-10-16 14:10 UTC | DO | r1 | 1/3 | D240-4 seam start - inspected the mid-refactor dirty tree (5 modified src files + new tests/status); first build error CS1574 XML-doc cref `QueryCommand.QueryShape` (QueryShape is namespace-scope, not nested) in DataContextExtensions.cs:34 | scripts/perf-state; artifacts/pdca/D240/rv1/build-D240-4.log
2026-10-16 14:10 UTC | DO | r1 | 1/3 | D240-4 seam fix - corrected the cref to `QueryShape`; kept the `QueryShape`/`ownJoins` seam (no back-out); no protected file touched; whole solution builds 0 Warning(s) 0 Error(s), all test projects included | src/nextorm.core/DataContext/DataContextExtensions.cs; artifacts/pdca/D240/rv1/build-D240-4.log
2026-10-16 14:10 UTC | DO | r1 | 1/3 | D240-4 inner loop - core `Category=T240Command` exit=0 selected=11 failed=0; `Category=T240Builder` exit=0 selected=13 failed=0 (recorded without a `|` compound token per validator hygiene) | artifacts/pdca/D240/rv1/t240-command.log; artifacts/pdca/D240/rv1/t240-builder.log
2026-10-16 14:10 UTC | DO | r1 | 1/3 | D240-4 dialect parity - sqlite `SqlGenerationTests` exit=0 selected=736 failed=0; `PlanKeyUniquenessTests` exit=0 selected=13 failed=0; SQL/plan-key unchanged | artifacts/pdca/D240/rv1/sqlite-sqlgen-single.log; artifacts/pdca/D240/rv1/sqlite-plankeys-single.log
2026-10-16 14:10 UTC | DO | r1 | 1/3 | D240-4 artifacts - scope validated (`brief` exit 0) then inner-loop report validated (`report` exit 0); no BDN run; seam COMPLETE, acceptance NOT claimed (CHECK pending) | artifacts/pdca/D240/rv1/scope-D240-4.json; artifacts/pdca/D240/rv1/inner-loop-D240-4.json
2026-10-16 14:10 UTC | DO | r1 | 1/3 | D240-4 Notice - `scripts/validate_inner_loop.py` (#235/#238 validator-policy, not T240's) must not enter T240's commit; observed committed/clean at HEAD 044d03e2; read-only use only | scripts/validate_inner_loop.py
2026-10-16 14:21 UTC | DO | r1 | 1/3 | D240 rv1 round-2 measurement - Release build for the round: `dotnet build benchmarks/nextorm.benchmark -c Release` exit=0, 0 Warning(s) 0 Error(s) | artifacts/pdca/D240/rv1/build-bench-release-r2.log
2026-10-16 14:21 UTC | DO | r1 | 1/3 | B(2) round-2 BDN run rc=0 wall=6:33.05 (393 s), executed benchmarks 55, filter '*SqliteBenchmarkFeaturesFairCached.B_*'; env NEXTORM_BENCH_FULL unset, NEXTORM_BENCH_DB=benchmarks/nextorm.benchmark/data/test.db, DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 (default in-process ShortRun job) | artifacts/pdca/D240/rv1/round-2.log; artifacts/pdca/D240/rv1/round-2/
2026-10-16 14:21 UTC | DO | r1 | 1/3 | round-2 ratios (adapter exit=1; alloc tol 1.20, time tol 1.05): alloc Cte 1.1852 MET / RecursiveCte 1.5669 FAIL / Except 1.2426 FAIL / Intersect 1.2511 FAIL / Distinct 0.9640 MET; time 0.5200 / 0.6164 / 0.5376 / 0.4844 / 0.5257 all MET | artifacts/pdca/D240/rv1/round-2.ratios.json
2026-10-16 14:21 UTC | DO | r1 | 1/3 | baseline comparison NextORM B/op round-0 -> round-1 -> round-2: Cte 115927 -> 110807 -> 113127 (r2 -2.4 % vs r0), RecursiveCte 144242 -> 137042 -> 134642 (-6.7 %), Except 72641 -> 66721 -> 64321 (-11.5 %), Intersect 72481 -> 66561 -> 64161 (-11.5 %), Distinct 43441 -> 41041 -> 38641 (-11.0 %); LINQ arms r0/r1/r2 95527/94327/95450, 85927/88167/85927, 51762/51762/51762, 51282/51282/51282, 40082/40082/40082 | artifacts/pdca/D240/rv1/round-0.ratios.json; artifacts/pdca/D240/rv1/round-1.ratios.json; artifacts/pdca/D240/rv1/round-2.ratios.json
2026-10-16 14:21 UTC | DO | r1 | 1/3 | two final rounds now exist (round-1 + round-2): Cte both MET (1.1747/1.1852), Distinct both MET (1.0239/0.9640), all five times MET in both; RecursiveCte (1.5543/1.5669), Except (1.2890/1.2426), Intersect (1.2979/1.2511) FAIL in both - still above 1.20. Residual cut to reach 1.20 from round-2 B/op: RecursiveCte ~30.6 % (target <= 103112), Intersect ~4.3 % (<= 61538), Except ~3.6 % (<= 62114); Cte/Distinct met. Manifest EC240-01..06 remain `open` with reasons updated to the round-2 numbers; validator `manifest` exit 0 - acceptance verdict deferred to CHECK | artifacts/pdca/D240/rv1/manifest.json
2026-10-16 14:33 UTC | DO | r1 | 1/3 | DO->CHECK boundary step (coder; no code change, no commit, no push) - tree 044d03e2; one comprehensive sweep + mandatory acceptance + freeze rv1 manifest | docs/specs/status/D240-construction-allocation-1.md
2026-10-16 14:33 UTC | DO | r1 | 1/3 | boundary build gate - `dotnet build nextorm.slnx -c Debug` exit=0, 0 Warning(s) 0 Error(s) | artifacts/pdca/D240/rv1/build-slnx-boundary.log
2026-10-16 14:33 UTC | DO | r1 | 1/3 | boundary full sweep (sequential, --no-build, capped): core exit=0 2602/0 fail 0 skip; sqlite exit=0 1760/0/1 (env-gated LOB probe); postgres exit=0 981/0/0; sqlserver exit=0 980/0/8; mysql exit=0 459/0/0; clickhouse exit=0 738/0/0; also alias 147/0, publicextensibility 10/0, mariadb 341/0, clickhouse.extensions 31/0, entityframeworkcore 126/0 all exit=0 | artifacts/pdca/D240/rv1/sweep-*.log
2026-10-16 14:33 UTC | DO | r1 | 1/3 | boundary container integration - Podman socket present, machine running, `_ping`=OK; nextorm.integration.tests Total 3985 Errors 0 Failed 1 Skipped 198 (capability skips, NOT availability); PostgreSQL/SQL Server/MySQL/ClickHouse/SQLite all executed; the 1 failure = ClickHouseJoinAliasIntegrationTests Seed HttpClient 120 s timeout (container still starting) -> rc=1 | artifacts/pdca/D240/rv1/integration.log
2026-10-16 14:33 UTC | DO | r1 | 1/3 | integration flake retry - `-class ClickHouseJoinAliasIntegrationTests` Total 12 Failed 0 Skipped 0 rc=0 -> confirmed infrastructure flake, not a T240 regression (dialect SQL parity green) | artifacts/pdca/D240/rv1/integration-clickhouse-retry.log
2026-10-16 14:33 UTC | DO | r1 | 1/3 | mandatory acceptance - `--anyCategories=acceptance` rc=0 wall real 46.13 s, exactly 7 cases 0 failures; cached/prepared ratio time 1671.0/1026.1 = 1.63 (baseline 1.87, -12.8 %); alloc 431.62/76.14 = 5.67 (baseline 7.42, -23.6 %) - neither grew >20 %, no investigation flag | artifacts/pdca/D240/rv1/acceptance.log
2026-10-16 14:33 UTC | DO | r1 | 1/3 | manifest frozen rv1 - tree=044d03e2; EC240-01..06 stay open (overall perf acceptance NOT met; terminal escalate decision B: RecursiveCte 1.5669 / Except 1.2426 / Intersect 1.2511 unreachable within spec section 6; Cte 1.1852 / Distinct 0.9640 <= 1.20, all five times MET); EC240-07 met (build 0/0, consumer 10/10, Roslyn only internal QueryShape added), EC240-08 met (T240 tests 11+13 green + 2 mutation negatives), EC240-09 met (dialect SQL parity x5 + container integration after flake retry), EC240-10/11/13 open, EC240-12 met (7 cases, <= 240 s), EC240-14 met (protected diff vs 044d03e2 = 0); validator `manifest` exit 0 -> CHECK | artifacts/pdca/D240/rv1/manifest.json
2026-10-16 14:33 UTC | DO | r1 | 1/3 | tree hygiene - the acceptance run regenerated tracked benchmarks/BenchmarkDotNet.Artifacts/results/* reports; restored to 044d03e2; `git status --short` shows exactly the pre-step set (5 modified T240 src files) + the same untracked files (artifacts/** gitignored); no protected/scripts/other-task file touched | git status --short

## Done / Verified (against tests/build/commits)
- Build: `dotnet build nextorm.slnx -c Debug` -> exit 0, **0 Warning(s) / 0 Error(s)** (`artifacts/pdca/D240/rv1/build-slnx-boundary.log`).
- Full sweep (all exit 0): core **2602**; sqlite **1759** (1 env skip); postgres **981**; sqlserver **972** (8 skip); mysql **459**; clickhouse **738**; mariadb **341**; alias **147**; publicextensibility **10**; clickhouse.extensions **31**; entityframeworkcore **126** (`artifacts/pdca/D240/rv1/sweep-*.log`).
- Container integration: **3985** total; SQLite / PostgreSQL / SQL Server / MySQL / ClickHouse all executed; the single failure (ClickHouse seed-timeout flake) retried green **12/12** (`artifacts/pdca/D240/rv1/integration.log`, `artifacts/pdca/D240/rv1/integration-clickhouse-retry.log`).
- SQL generation snapshots unchanged: sqlite SqlGenerationTests **736/736**; PlanKeyUniquenessTests **13/13**.
- Acceptance category: exactly **7** cases, **0** failures, wall **46.13 s <= 240 s**; cached/prepared ratio time **1.63** / alloc **5.67** (baseline 1.87 / 7.42; no >20 % growth) (`artifacts/pdca/D240/rv1/acceptance.log`).
- Two PRE-EXISTING immutability defects found and fixed with red->green proofs: `D240-SETOP-CHAIN-OPERAND-MUTATION` (`a.Union(b).Except(c)` mutated the reused operand `b`) and `D240-BUILDER-ORDERING-ALIAS` (`_sorting` aliasing); red `artifacts/pdca/D240/rv1/regression-red-D240-{2,3}.log`, green `artifacts/pdca/D240/rv1/inner-loop-core-D240-{2,3}.log`.

## Not met
- **R240-02 RecursiveCte** allocation <= 1.20.
- **R240-03 Except** allocation <= 1.20.
- **R240-04 Intersect** allocation <= 1.20.

## Residual table (round-2, same-round B/op vs LINQ to DB)
| Scenario | NextORM B/op | ratio | verdict |
| --- | ---: | ---: | --- |
| Cte | 113127 | 1.1852 | MET |
| Distinct | 38641 | 0.9640 | MET |
| RecursiveCte | 134642 | 1.5669 | FAIL (needs <= 103112, -30.6 %) |
| Except | 64321 | 1.2426 | FAIL (-3.4 %) |
| Intersect | 64161 | 1.2511 | FAIL (-4.1 %) |

Time: all 5 same-round ratios **0.44-0.62 -> MET** (tolerance <= 1.05).

## Progression (r0 -> r1 -> r2 same-round ratios)
| Scenario | r0 | r1 | r2 |
| --- | ---: | ---: | ---: |
| RecursiveCte | 1.6787 | 1.5543 | 1.5669 |
| Except | 1.4034 | 1.2890 | 1.2426 |
| Intersect | 1.4134 | 1.2979 | 1.2511 |
| Cte | 1.2136 | 1.1747 | 1.1852 |
| Distinct | 1.0838 | 1.0239 | 0.9640 |

Frozen D201-3 recipe contribution (B/op): 20409 / 36706 / 21567 / 21630 / 9587 (Cte / RecursiveCte / Except / Intersect / Distinct).

## Levers applied (verified)
- **D240-2** - ownership seam + single-pass definition read + clone/materialization removal + set-op mutation fix (`QueryCommand.cs`, `QueryCommand.Clone.cs`, `QueryCommand.TResult.cs`).
- **D240-3** - EntityBuilder COW (`_windows` / `_loadSpecs` / `_joinIntos`) + `_joins?.ToArray()` elision (`EntityBuilder.cs`, `DataContextExtensions.cs`).
- **D240-4** - `internal readonly struct QueryShape` + `QueryCommand` copy ctor removing per-step `QueryDefinition` materialization on the internal path.

## Levers NOT applied + reasons
- Bounded call-local scratch pooling: attribution showed **no removable scratch of the needed size**.
- Set-op single-slot clone: ownership judgement; ~2-3k B/op estimate; does not affect RecursiveCte; immutability risk.
- Full COW for `_joins` / `_settings` / `_arrayJoins`: needs `src/nextorm.clickhouse` mutation-path changes outside T240's footprint.

## Owner escalation (spec §8)
Authorize a **bounded additional budget**, or accept the unmet scenarios as a **named release residual in milestone 1.0.9-rc3** - without changing the 1.20 threshold or the recipe. Until then **T240 is `incomplete`**.

## Next plan
- (awaiting owner decision; no further cycle of this task).

## Changed files
| Path | State |
| --- | --- |
| `src/nextorm.core/Query/QueryCommand.cs` | modified |
| `src/nextorm.core/Query/QueryCommand.Clone.cs` | modified |
| `src/nextorm.core/Query/QueryCommand.TResult.cs` | modified |
| `src/nextorm.core/Builders/EntityBuilder.cs` | modified |
| `src/nextorm.core/DataContext/DataContextExtensions.cs` | modified |
| `tests/nextorm.core.tests/T240CommandConstructionTests.cs` | new (untracked) |
| `tests/nextorm.core.tests/T240BuilderConstructionTests.cs` | new (untracked) |
| `scripts/perf/t240_evidence.py` | new (untracked) |
| `scripts/perf/tests/test_t240_evidence.py` | new (untracked) |
| `docs/specs/status/D240-construction-allocation-1.md` (this status file) | new (untracked) |
| `artifacts/pdca/D240/rv1/**` | ignored (untracked) |

## Pointers
- Spec: `docs/specs/design/issue-240-construction-allocation-budget.md`.
- Baseline: `docs/specs/performance/m12-3-prepare-command-results.md`.
- Reader adapter: `scripts/perf/t240_evidence.py`.
- Manifest: `artifacts/pdca/D240/rv1/manifest.json`.
- Gather: `artifacts/pdca/D240/rv1/check-gather.md`.
- Acceptance: `artifacts/pdca/D240/rv1/acceptance.log`.

2026-10-16 14:40 UTC | CHECK | r1 | 1/3 | verdict FAIL - performance acceptance NOT met; R240-02 RecursiveCte 1.5669 / R240-03 Except 1.2426 / R240-04 Intersect 1.2511 above 1.20 in both valid rounds; R240-01 Cte 1.1852 MET, R240-05 Distinct 0.9640 MET; all five time ratios MET; escalated to owner (spec §8) | artifacts/pdca/D240/rv1/check-gather.md
2026-10-16 14:40 UTC | ACT | r1 | 1/3 | ACT closed - FAIL, escalated to owner (spec §8); changes kept uncommitted | docs/specs/status/D240-construction-allocation-1.md

## Terminal — incomplete (2026-10-16)
- Task **T240 (#240)** is terminally **`incomplete`**; no further cycle of this task runs until the owner decides.
- Cause: the allocation goal (`<= 1.20 x` LINQ to DB, same round) is **not reachable within the approved §6 scope**: RecursiveCte **1.5669**, Except **1.2426**, Intersect **1.2511** (both valid rounds, no averaging; Cte 1.1852 and Distinct 0.9640 MET).
- Time: **MET on all five** scenarios (same-round ratios 0.44-0.62, tolerance <= 1.05).
- CHECK verdict **FAIL** (2026-10-16 14:40 UTC) -> **STOP**; owner decision pending: **authorize a bounded extra budget OR accept the residual as a named milestone-1.0.9-rc3 release residual** (threshold and D201-3 recipe untouched).
- Disposition: the uncommitted T240 changes were captured as a patch at `artifacts/pdca/D240/rv1/T240-uncommitted.patch` (copy: `/tmp/nextorm/T240-uncommitted.patch`) and T240's own paths were rolled back to the pre-DO tip `044d03e2`; the branch tip is **unchanged at `044d03e2`**.
- Retained: this evidence/status file, `artifacts/pdca/D240/**` (incl. the patch), the four untracked `docs/specs/design/issue-23*.md` specs and the collection status file.

2026-10-16 14:44 UTC | DO | r1 | 1/3 | failure protocol - T240 terminal `incomplete`; uncommitted changes saved to `artifacts/pdca/D240/rv1/T240-uncommitted.patch` (+ `/tmp/nextorm/T240-uncommitted.patch`) and T240's tracked/untracked paths rolled back to tip `044d03e2`; branch tip unchanged | artifacts/pdca/D240/rv1/T240-uncommitted.patch
## Revised plan (r2, rv2)

Goal: integrate the frozen T240 patch onto `ee922687`, proving fidelity, compatibility and correctness — NOT renewed performance acceptance. Base `ee922687`; patch `artifacts/pdca/D240/rv1/T240-uncommitted.patch` (84977 B); evidence root `artifacts/pdca/D240/rv2/`; rv1 kept unchanged. Owner decision: the 1.20x allocation goal is NOT pursued; the RecursiveCte/Except/Intersect residual is ACCEPTED (2026-10-16); no threshold/recipe change; milestone stays 1.0.9-rc3.

Footprint W (exactly 10 paths): `docs/specs/performance/performance-findings.md`, `scripts/perf/t240_evidence.py` (new), `scripts/perf/tests/test_t240_evidence.py` (new), `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/DataContext/DataContextExtensions.cs`, `src/nextorm.core/Query/QueryCommand.Clone.cs`, `src/nextorm.core/Query/QueryCommand.TResult.cs`, `src/nextorm.core/Query/QueryCommand.cs`, `tests/nextorm.core.tests/T240BuilderConstructionTests.cs` (new), `tests/nextorm.core.tests/T240CommandConstructionTests.cs` (new).

Protected P (must not change; guard base `ee922687`): `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `QueryCommand.Plan.cs`, `QueryPlanEqualityComparer.cs`, `ExpressionPlanEqualityComparer.cs`, `src/nextorm.core/Expressions/JoinExpressionPlanEqualityComparer.cs`, `src/nextorm.core/DataContext/QueryPlanner.cs`, `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs`.

Acceptance rows rv2 (R240B / EC240B-01..10): 01 exact pinned patch applied, all W, no unrelated hunks; 02 `dotnet build nextorm.slnx -c Debug` 0W/0E; 03 `-c Release` 0W/0E; 04 T240Command 11 Facts + T240Builder 13 Facts green; 05 core suite green; 06 five dialect suites green; 07 container integration with real providers (PostgreSQL/SQL Server/MySQL/ClickHouse/SQLite), capability skips only; 08 the two immutability defects retain red→green provenance (`D240-SETOP-CHAIN-OPERAND-MUTATION`, `D240-BUILDER-ORDERING-ALIAS`); 09 no public-API change (Roslyn) and no SQL/result change; 10 the ACT commit contains exactly W. Negative per row (missing/altered hunk, warning, zero-selection, failing/skipped-as-green provider, API delta, foreign file in the commit).

rv2 contract: supersedes rv1 execution scope; rv1 kept. Retained rows EC240-07/08/09/12/14 → `met` by reference to their rv1 evidence, RE-VERIFIED this cycle; EC240-01..06, 10, 11, 13 → `na` with the observed predicate "the owner accepted the 1.20x allocation residual on 2026-10-16; this cycle's acceptance is correctness of the exact frozen patch; they reopen under their existing IDs on any retuning or performance-acceptance claim". Manifest `artifacts/pdca/D240/rv2/manifest.json`. CHECK re-gather budget: 2 targeted requests, owner CHECK.

DO tasks: D240-r2.1 patch application + fidelity + builds; D240-r2.2 focused T240 tests + defect receipts; D240-r2.3 boundary sweep F (core + 5 dialects) then I (integration) once; D240-r2.4 rv2 manifest + guards. Unit mode: sequential, one tree. Single boundary sweep point: after the final patch application, run F then I once. Docs: only the pinned `performance-findings.md` hunk; public EN/RU docs unchanged. Perf measurement: not needed — the owner accepted the residual, no perf criterion this cycle (historical receipts kept). Recon: not needed — base/patch/protected/commands are pinned. Risks: patch offsets; container availability; unfamiliar unstaged state. Write footprint: W + status + `artifacts/pdca/D240/rv2/**`. Feasibility: high.

2026-10-16 15:31 UTC | PLAN | r2 | 1/3 | Replanned: owner accepted the allocation residual; acceptance re-scoped to correctness of the frozen patch (r 1->2, iteration 1/3) | docs/specs/status/D240-construction-allocation-1.md
2026-10-16 15:31 UTC | PLAN | r2 | 1/3 | P: D240 r2 n=1 - revised plan r2 recorded; footprint W = 10 paths + protected P pinned; single boundary sweep F then I after the final patch application | docs/specs/status/D240-construction-allocation-1.md
2026-10-16 15:31 UTC | DO | r2 | 1/3 | D: D240 r2 n=1 - started sequential DO (D240-r2.1 patch application + fidelity + builds; D240-r2.2 focused T240 tests; D240-r2.3 boundary sweep F then I; D240-r2.4 rv2 manifest + guards); one tree | artifacts/pdca/D240/rv2/
2026-10-16 15:33 UTC | DO | r2 | 1/3 | D240-r2.1 done - pinned patch applied via plain `git apply --index` (exit 0, --3way not needed); patch-id applied == rv1 `6629dbd193e08668fbeae0a9aba89e9471863f54`; `git diff --cached --stat` = exactly the 10 W paths (1433 insertions / 80 deletions); protected-P and scripts/tools/benchmarks guards vs ee922687 exit 0; Debug build 0 Warning(s) 0 Error(s); Release build 0 Warning(s) 0 Error(s); all `.cs`/`.md` CRLF | artifacts/pdca/D240/rv2/applied.patch; artifacts/pdca/D240/rv2/build-debug.log; artifacts/pdca/D240/rv2/build-release.log
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.2 done - focused T240 tests on the frozen patch: `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter Category=T240Command` exit=0 selected=11 failed=0 skipped=0; `--filter Category=T240Builder` exit=0 selected=13 failed=0 skipped=0 (host capped DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0) | artifacts/pdca/D240/rv2/unit-command.log; artifacts/pdca/D240/rv2/unit-builder.log
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.2 immutability receipts - both rv1 defects keep their red->green provenance (D240-SETOP-CHAIN-OPERAND-MUTATION, D240-BUILDER-ORDERING-ALIAS; rv1 red exit 2) and the rv2 covering runs are green | artifacts/pdca/D240/rv2/immutability-receipts.json; artifacts/pdca/D240/rv1/regression-red-D240-2.log; artifacts/pdca/D240/rv1/regression-red-D240-3.log
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.3 boundary F sweep (per project, --no-build, capped, sequential) - core 2602/0 fail/0 skip; sqlite 1760/0/1; postgres 981/0/0; sqlserver 980/0/8; mysql 459/0/0; clickhouse 738/0/0; mariadb 341/0/0; alias 147/0/0; publicextensibility 10/0/0; clickhouse.extensions 31/0/0; entityframeworkcore 126/0/0 - every run exit 0 | artifacts/pdca/D240/rv2/sweep-*.log
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.3 boundary I integration - Podman socket present, `_ping`=OK; nextorm.integration.tests Total 3985 Errors 0 Failed 1 Skipped 198 (capability skips, Not Run 0); SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse all executed; the 1 failure = ClickHouseJoinAliasIntegrationTests HttpClient 120 s timeout (container still starting) rc=1; retry `-class ClickHouseJoinAliasIntegrationTests` Total 12 Failed 0 rc=0 (infrastructure flake, not a T240 regression) | artifacts/pdca/D240/rv2/integration.log; artifacts/pdca/D240/rv2/integration-clickhouse-retry.log
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.4 API compatibility re-verified with roslyn - public surface of QueryCommand/QueryCommand<T>/EntityBuilder<T>/QueryDefinition/DataContextExtensions.CreateCommand unchanged vs rv1 (src identical between 044d03e2 and ee922687; all 25 public/protected patch lines are internal-QueryShape members or XML-doc prose); QueryShape = `internal readonly struct` (QueryCommand.cs:13), 8 refs all inside nextorm.core | artifacts/pdca/D240/rv2/api-compatibility.json; artifacts/pdca/D240/rv2/api/EC240-07-public-surface.txt
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.4 guards - `git diff --exit-code ee922687 -- <7 protected files>` exit 0; `git diff --exit-code ee922687 -- scripts/ tools/ benchmarks/` exit 0; tracked-tree delta vs ee922687 = the 10 W paths + this status file | artifacts/pdca/D240/rv2/guards/EC240-14-protected-diff.txt
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.4 inner-loop artefacts - scope `brief` validator exit 0; report `report` validator exit 0 (boundary build + inner 11/13 + F per-project sweeps, all green) | artifacts/pdca/D240/rv2/scope-r2.json; artifacts/pdca/D240/rv2/inner-loop-r2.json
2026-10-16 15:46 UTC | DO | r2 | 1/3 | D240-r2.4 rv2 manifest frozen - tree ee922687, contract_rv 2, 24 required rows; EC240-07/08/09/12/14 met (rv1 refs + rv2 re-verification), EC240-01..06/10/11/13 na (owner accepted the 1.20x residual 2026-10-16; reopen on any retuning/performance claim), EC240B-01..09 met, EC240B-10 met; validator `manifest` exit 0; pre-commit staged index == exactly the 10 W paths (1433 insertions/80 deletions) | artifacts/pdca/D240/rv2/manifest.json; artifacts/pdca/D240/rv2/act-footprint-precheck.txt

## ACT (r2) — closure (2026-10-16)

- **Partial improvements LANDED** with commit **`3ae2e80ed38675e12330a7681f8a5c2d2250b8ef`** on branch `1.0.9-rc3` (message `#240 T240: land frozen construction-allocation patch (QueryCommand/EntityBuilder structural reductions + 2 immutability fixes); 1.20x residual owner-accepted (spec 8)`), parent `git rev-parse HEAD^` == **`ee92268752efc28ecf539a132daa90e12ae0daa1`** = the rv2 base tree `ee922687`.
- Footprint: **exactly the 10 committed paths** (1433 insertions / 80 deletions); `git diff --exit-code ee922687 HEAD --name-only` = exit 1 because the trees differ **by design by exactly that 10-path footprint** (no unrelated path), and `git diff ee922687 HEAD | git patch-id --stable` = **`6629dbd193e08668fbeae0a9aba89e9471863f54`** == the pinned rv1 patch-id, so the committed delta is byte-identical to the frozen patch.
- **1.20x allocation residual ACCEPTED by the owner on 2026-10-16**: RecursiveCte **1.5669**, Except **1.2426**, Intersect **1.2511** (both valid rounds, no averaging) stand as a named release residual in milestone `1.0.9-rc3`. **No threshold change** (stays 1.20 x) and **no D201-3 recipe change**; Cte 1.1852 and Distinct 0.9640 were already MET; all five time ratios MET (0.44-0.62 <= 1.05).
- Patch origin: `artifacts/pdca/D240/rv1/T240-uncommitted.patch` (84977 B, patch-id `6629dbd193e08668fbeae0a9aba89e9471863f54`), applied to `ee922687` with plain `git apply --index` (exit 0); copy of the applied patch `artifacts/pdca/D240/rv2/applied.patch`.
- **CHECK = PASS** (r2, correctness of the exact frozen patch): whole-solution Debug/Release build 0 Warning(s)/0 Error(s); T240Command 11/11 + T240Builder 13/13; core 2602/2602; five dialect suites green; container integration executed on SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse (3985 total, 198 capability skips, 1 pre-existing ClickHouseJoinAlias infra flake re-run green 12/12); both immutability defects keep red->green provenance; 0 public-API change, 0 SQL/result change; manifest validator exit 0. Gather: `artifacts/pdca/D240/rv2/check/check-gather-rv2.md`.
- **Done/Verified**: the frozen construction-allocation patch is integrated, committed and verified for fidelity, builds, focused+suite+integration tests, immutability, API and SQL parity. **Accepted residual (not met)**: the per-scenario 1.20 x allocation goal for RecursiveCte/Except/Intersect — owner-accepted, named release residual, threshold and recipe untouched.
- Owner decision applied; **no push** was performed (`git log --oneline origin/1.0.9-rc3..HEAD | wc -l` = 1, i.e. the commit is local only); the GitHub issue #240 remains **OPEN** (closure belongs to the collection parent).

### Changed files (committed, r2 ACT — 10 paths)

| Path | State at commit `3ae2e80e` |
| --- | --- |
| `docs/specs/performance/performance-findings.md` | modified (pinned hunk) |
| `scripts/perf/t240_evidence.py` | added |
| `scripts/perf/tests/test_t240_evidence.py` | added |
| `src/nextorm.core/Builders/EntityBuilder.cs` | modified (COW `_windows`/`_loadSpecs`/`_joinIntos`, `_joins?.ToArray()` elision) |
| `src/nextorm.core/DataContext/DataContextExtensions.cs` | modified (single-pass definition read) |
| `src/nextorm.core/Query/QueryCommand.Clone.cs` | modified (clone/materialization removal) |
| `src/nextorm.core/Query/QueryCommand.TResult.cs` | modified (D240-2 ledger/materialization removal) |
| `src/nextorm.core/Query/QueryCommand.cs` | modified (`internal readonly struct QueryShape` + copy ctor, ownership seam) |
| `tests/nextorm.core.tests/T240BuilderConstructionTests.cs` | added (13 Facts) |
| `tests/nextorm.core.tests/T240CommandConstructionTests.cs` | added (11 Facts) |

Not committed (unchanged intent): this status file (kept unstaged), `artifacts/pdca/D240/**` (ignored), the four untracked `docs/specs/design/issue-23*.md` specs.

### Evidence pointers (r2)
- Manifest: `artifacts/pdca/D240/rv2/manifest.json` (validator `manifest` exit 0; EC240B-10 carries the commit hash + tree-diff outcome); receipt `artifacts/pdca/D240/rv2/act-commit-receipt.txt`; pre-commit footprint `artifacts/pdca/D240/rv2/act-footprint-precheck.txt`.
- Builds: `artifacts/pdca/D240/rv2/build-debug.log`, `build-release.log` (exit 0). Tests: `unit-command.log`, `unit-builder.log`, `sweep-*.log`, `integration.log`, `integration-clickhouse-retry.log`. API: `artifacts/pdca/D240/rv2/api-compatibility.json`. Guards: `artifacts/pdca/D240/rv2/guards/EC240-14-protected-diff.txt` (exit 0).

## Next plan
- — (owner decision applied; **no further cycle of this task**; GitHub issue closure is the collection parent's).

2026-10-16 15:52 UTC | CHECK | r2 | 1/3 | C: D240 r2 - CHECK PASS (correctness), perf residual owner-accepted | artifacts/pdca/D240/rv2/check/check-gather-rv2.md; artifacts/pdca/D240/rv2/manifest.json
2026-10-16 15:56 UTC | ACT | r2 | 1/3 | A: D240 r2 - ACT closed; committed 3ae2e80ed38675e12330a7681f8a5c2d2250b8ef (10-file footprint); no push | artifacts/pdca/D240/rv2/act-commit-receipt.txt
