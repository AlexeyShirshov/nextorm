# Status — nested-read-CTE per-call allocation (issue #166)

Task: issue #166 (AlexeyShirshov/nextorm, Open, milestone 1.0.9-b) — remove per-call nested-read-CTE allocation in `QueryCommand.HasDataModifyingCte` and add a nested-read zero-allocation regression test. Trigger #166 has fired.

## Sealed plan

### r=2 plan (active) — supersedes r=1

Outgoing failed attempt: CHECK r1 n1 verdict FAIL — 3 open variant rows + tautological param assertion; loop-back DO r1 n2 closed D4 rows (a)/(b) as tests and (c) real sync/async terminal only as a static guard, unobservable provider-free. DEF-166-C (sync/async terminal unobservable in a provider-free core test) reopened r1 n2, 0 fixes, raised as DO→PLAN candidate (`QueryPlanner.cs:559`).

Replanned: sync/async terminal verification requires a provider-backed executed test; rv=1 row (c) could not be closed provider-free (DEF-166-C) (r 1→2, iteration 1/3)

Supersession: plan r=2, n=1; contract rv=2 supersedes rv=1; retain R01–R08 / C0–C8 IDs and obligations; add SA-READ and SA-MUTATION; preserve historical rv=1 provenance and counters (N=1). F1 product code frozen. D6 is additive.

Goal + AC1–AC3: unchanged (keep the existing text; see r=1 plan retained below).

F1: unchanged (nested && !IsPrepared after the mutation-first flat scan; `src/nextorm.core/Query/QueryCommand.cs`; no `_ctes` clear/cache-flag/shared mutation).

D6 tasks (sequential, current tree; planned symbols, no TBD):
- D6.1: add planned `NestedReadCte_SyncTerminal_Executes` and `NestedReadCte_AsyncTerminal_Executes` to `tests/nextorm.sqlite.tests/TypedCteTests.cs` (execute the nested READ CTE via `ToList()` / `ToListAsync()`; assert independently specified nonempty fixture results, not merely construction).
- D6.2: verify the D4 diamond/prepared-clone tests (`NestedReadCte_AcyclicSharedBody_ClassifiesAndReusesWarmPlan`, `NestedReadCte_PreparedClone_KeepsClassificationAndWarmAlloc`, `NestedMutation_PreparedClone_KeepsClassification`) close rows (a)/(b) with strong assertions; only if a genuine assertion gap remains, add planned `NestedReadCte_DiamondSharedBody_PreservesState` / `NestedReadCte_PreparedClone_PreservesState` to `tests/nextorm.core.tests/Iteration14CteLookupTests.cs`. Do not duplicate.
- D6.3: run existing `tests/nextorm.postgres.tests/DataModifyingCtePlanCacheTests.cs` classification regressions (`--filter "FullyQualifiedName~DataModifyingCtePlanCacheTests"`); nested prepared mutation must remain non-reusable.
- D6.4: record commands/selected/result/matrix changes + refresh artifacts; preserve rv=1 provenance.

Variant delta: sync nested-read = test SA-READ; async nested-read = test SA-READ; diamond/shared + prepared clone = test R03/R04; mutation prepared/unprepared classification = test SA-MUTATION; SQLite mutation execution = unsupported-provider guard; real mutation sync/async terminals = deferred, trigger: mutation/cache/terminal-path change or PostgreSQL integration scope. Cold/cyclic/flat/nested/read/mutation rows retained in R03.

Decisions: docs = none (no public behavior change; F1 XML remark frozen); perf = no rerun (D6 test-only; F1 already has R02/R06/R07 evidence; ratios 2.37 vs 1.87 recorded without asserting noise explains them); recon = done (SQLite nested-read executable `TypedCteTests.cs:478-495`, async model `JoinIntoExecutionTests.cs:65`, terminal path `QueryCommand.TResult.cs:217/227,233 → DataContext.cs:287,309 → QueryPlanner.cs:559`); unit mode = sequential, current tree.

Risks: provider-free classifier cannot replace executed read terminals; SQLite mutation unsupported; coverage 82.0/74.2 off-main warning only.

Negative criterion: DEF-166-C stays open if any read terminal is missing/unselected/failing/tautological or the mutation half is claimed verified via SQLite rejection alone.

### r=1 plan (superseded, retained)

#### Task
Task: issue #166 (AlexeyShirshov/nextorm, Open, milestone 1.0.9-b) — remove per-call nested-read-CTE allocation in `QueryCommand.HasDataModifyingCte` and add a nested-read zero-allocation regression test. Trigger #166 has fired.

#### Goal/essence
avoid redundant graph traversal on prepared/reused commands; preserve D5/§9 shared-command immutability, CTE classification, SQL, parameters, plan identity.

#### Acceptance criteria
- AC1: prepared nested-read warm reuse allocates 0 bytes (100 warmup / 10_000 measured); negative: same test fails on original impl with positive measured allocation.
- AC2: read-only graphs return false; reachable mutations return true and keep read reuse disabled. Negative: a nested mutation must not become reusable merely because its command is prepared.
- AC3: cold traversal remains cycle-safe; SQL, parameter values/types/order, plan-key equality unchanged; shared bodies/prepared clones retain state. Negative: must not clear `_ctes`, change cache flags, or mutate a defining command.

#### Chosen fix F1 (minimal, no new state)
in the `HasDataModifyingCte` getter, after the mutation-first flat scan finds no mutation, enter the recursive `HashSet` path only when `nested && !IsPrepared`; prepared ⇒ flat, so the flat scan is complete. Edit points: `src/nextorm.core/Query/QueryCommand.cs` getter `~:514-546` (nested-probe branch `:522-533`).

#### Proof
`Prepare` calls `PrepareCtes` (`QueryCommand.QueryPreparer.cs:92`) which hoists at `:276` whenever `_ctes` non-empty (`:269`); `_isPrepared=true` at `:95`. `CteHoister.Walker.Collect` (`CteHoister.cs:209-220`) transitive edge set (`Mutation.Source ?? Query`) covers the getter's `Query`-only recursion `QueryCommand.cs:600-619`; mutation short-circuits at `:516/:611`. Prepared clones copy `_isPrepared` (`QueryCommand.Clone.cs:22`) and already-hoisted `_ctes`.

#### Rejected
- clear inner `_ctes` (D5/§9 forbid mutating shared defining command)
- F2 explicit marker (unnecessary lifecycle/clone-copy)
- F3 lazy memoization (state/invalidation)

#### Decisions
- perf = REQUIRED (per-call work, not one-time): acceptance 7-case suite + target before/after GC measurement + `eng/perf/iteration14_gate.py`.
- Reconnaissance = not needed (F1 invariant proven by targeted scout); in-cycle prerequisites only (resolve test helpers, sync/async entry paths).
- Unit mode = sequential, one stream S1, current tree (getter+tests share footprint).
- Docs = public EN/RU docs untouched (no public symbol/API/SQL/behavior change); only status/evidence records.

#### Risks
- false green from zero selected tests
- measuring cold/setup/async-thread allocations
- unsupported cyclic fixtures
- unproven sync/async entry paths
each covered by contract rows.

#### Deferred + trigger (stays milestone 1.0.9-b)
- dedicated nested-read benchmark/budget until benchmark coverage expands
- automated `validate_inner_loop.py` until manual evidence maintenance warrants it

#### Notice
- `scripts/validate_inner_loop.py` is ABSENT in this repo → structured manual test evidence (exact arg arrays, `--filter`, exit codes, selected counts) is used; do not recreate it.
- `todowrite` unavailable in this harness; status file is the sole tracker.

#### Variant matrix (all rows closed as test; see evidence contract)
read/mutation × flat/nested × cold/prepared × acyclic; cyclic (existing cold tests); shared/non-shared; recursive/non-recursive; sync/async convergence at `QueryPlanner.cs:559`. Providers: classification is provider-independent (before rendering); core tests + SQLite/PG/SS SQL regression; MySQL/MariaDB/ClickHouse deferred until a provider-dependent path changes. No integration/Testcontainers runs.

## PINNED VERSIONED EVIDENCE CONTRACT (rv=2)

Owner stream S1 = coder; CHECK validates. Every row inherits: rv=2; owner S1=coder; applicability=true unless stated; transcript artifact `docs/specs/status/nested-read-cte-1-evidence/<row>-<phase>.log` with exact resolved arg arrays, stdout/stderr, exit/invocation result, selected counts, elapsed, source revision/diff identity. No optional slots. CHECK re-gather budget ≤1 targeted round owned by CHECK; no PASS while a row is open.

### R→C mapping
- R01 → C0 (roslyn structure/members/refs/callers; no arg array by contract).
- R02 → C2 (target before/after allocation).
- R03 → C3 (class + full core + full sqlite) + C4 sqlite refresh + C5 (sqlite assembly).
- R04 → C3 (SQL / parameters / plan-key / shared state / sync-async policy assertions).
- R05 → C1 + C4 (all four assemblies) + C5.
- R06 → C6 (retained rv=1 run; not re-run in r2 — product unchanged).
- R07 → C7 (retained rv=1 run; not re-run in r2 — product unchanged).
- R08 → C8.
- SA-READ → SA-READ-sync / SA-READ-async / SA-READ-combined.
- SA-MUTATION → SA-MUTATION-classification.

### Priority matrix
**P1 by construction:** every AC1–AC3 assertion and changed/reachable getter, prepare, sync/async terminal, state-preservation and mutation-classification branch; CHECK cannot downgrade these. **P2:** retained-rv=1 acceptance/gate rows (C6/C7) and the SQLite unsupported-provider mutation guard — evidence retained, not re-run; downgrade requires a product change.

### Coverage policy
85/75 hard-fail only on `main`; branch `1.0.9-b` is off-`main` → **warning-only**. Getter requirement: changed `QueryCommand.HasDataModifyingCte` getter line **100 %** / branch **100 %** (22/22). Assemblies reported: core / sqlite / postgres / sqlserver; r2 refreshed **sqlite only**, core/postgres/sqlserver retained rv=1 collections with provenance (E10).

### Commands
- C0: `roslyn` (exact invocation array N/A — explicitly justified); artifact E01-paths.md.
- C1: `dotnet build nextorm.slnx -c Debug` → exit 0, 0 warnings/0 errors.
- C2: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~NestedReadCteWarmReuse_AllocatesZero"` → exit 0, 1 selected, bytes=0.
- C3: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~Iteration14CteLookupTests"` → exit 0, all selected methods pass.
- C4: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o docs/specs/status/nested-read-cte-1-evidence/cov-<proj>.cobertura.xml "dotnet test tests/nextorm.<proj>.tests --no-build --verbosity normal"` ×{core,sqlite,postgres,sqlserver} → exit 0, nonempty each.
- C5: `dotnet tool run reportgenerator -reports:<4 cov paths> -targetdir:docs/specs/status/nested-read-cte-1-evidence/c5-report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"` → exit 0; evaluate 85/75 (warnings off-main).
- C6: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` (retained, no rerun).
- C7: `python3 eng/perf/iteration14_gate.py` (retained, no rerun).
- C8: `git diff --check` + diff review of src/core, tests/core, tests/nextorm.sqlite.tests, this status file → exit 0; CRLF preserved; unrelated changes untouched.

### Sealed rows (full fields)

#### R01 — hoist/clone/prepared invariant + entry paths
- requirement: prove `Prepare` hoists every CTE edge (`PrepareCtes` → `CteHoister.Walker.Collect`) so the prepared flat scan is complete, prepared clones copy `_isPrepared`/hoisted `_ctes`, and sync/async terminals converge on the one `QueryPlanner.cs:559` caller.
- scenario: static symbol/path resolution against the current tree; no executed test.
- evidence kinds/sources: roslyn structure/members/refs/callers + source read.
- exact command/invocation: `roslyn` (exact invocation array N/A — explicitly justified); no per-row arg-array log.
- required result/exit: getter internal, exactly **1** production caller; no bypass/post-prepare-write counterexample.
- artifacts: `E01-paths.md`.
- owner: S1=coder. applicability: true. rv: 2.

#### R02 — prepared nested-read target before/after allocation
- requirement: prepared nested-read warm reuse allocates **0 bytes** (AC1) and the negative red fails on the original impl.
- scenario: 100 warmup / 10_000 measured iterations on the prepared nested-read command.
- evidence kinds/sources: executed `NestedReadCteWarmReuse_AllocatesZero` + GC byte measurement.
- exact command/invocation: C2 array `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~NestedReadCteWarmReuse_AllocatesZero"]`.
- required result/exit: green exit 0, 1 selected / 1 succeeded / 0 skipped, **bytes=0**; red exit **2**, 1 selected / 1 failed, **1,760,000 B**.
- artifacts: `E02-target-green.log`, `E02-allocation-red.log`, `E02-allocations.json`.
- owner: S1=coder. applicability: true. rv: 2.

#### R03 — full variant matrix + existing regressions
- requirement: all variant rows classify correctly (read/mutation × flat/nested × cold/prepared × acyclic; cyclic cold; shared/non-shared) and existing regressions stay green.
- scenario: executed `Iteration14CteLookupTests` class, full core, full sqlite.
- evidence kinds/sources: executed tests + E03 matrix.
- exact command/invocation: class `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~Iteration14CteLookupTests"`; full core `dotnet test tests/nextorm.core.tests -c Debug`; full sqlite `dotnet test tests/nextorm.sqlite.tests -c Debug`.
- required result/exit: class exit 0 **12/12**; full core exit 0 **1524/1524**, 0 failed, 0 skipped; full sqlite exit 0 **1013** total / 1012 succeeded / **1** skipped (pre-existing probe).
- artifacts: `C3-class.log`, `C3-full-core.log`, `C3-full-sqlite.log`, `E03-matrix.md`.
- owner: S1=coder. applicability: true. rv: 2.

#### R04 — SQL / params / plan-key / shared state / sync-async
- requirement: SQL text, parameter values/types/order and plan-key equality unchanged; shared bodies/prepared clones retain state; sync and async policy match (reuse disabled for mutation, allowed for read).
- scenario: executed core assertions in the same class as R03.
- evidence kinds/sources: executed tests; red proof that the parameter assertion is non-tautological.
- exact command/invocation: C3 class filter (above).
- required result/exit: exit 0, all selected methods pass; identity/state unchanged.
- artifacts: `E04-behavior.md`, `E04-assertion-red.log`.
- owner: S1=coder. applicability: true. rv: 2.

#### R05 — build + coverage
- requirement: solution builds 0 W / 0 E and coverage over core/sqlite/postgres/sqlserver is collected and evaluated against the policy.
- scenario: build + `dotnet-coverage` collect ×4 + `reportgenerator`.
- evidence kinds/sources: build log; 4 cobertura files; report dir.
- exact command/invocation: C1 `dotnet build nextorm.slnx -c Debug`; C4 collect per assembly; C5 reportgenerator.
- required result/exit: C1 exit 0, 0 W / 0 E; C4 exit 0, nonempty each; C5 exit 0, overall **82.0 % / 74.2 %**, getter **100 % / 100 %**.
- artifacts: `C1-build.log`, `cov-{core,sqlite,postgres,sqlserver}.cobertura.xml`, `C4-*.log`, `C5-reportgenerator.log`, `c5-report/`, `E05-coverage.md`.
- owner: S1=coder. applicability: true. rv: 2.

#### R06 — 7-case acceptance regression
- requirement: acceptance suite unaffected by F1 (no noise claims).
- scenario: retained rv=1 run, not re-run in r2 (product unchanged).
- evidence kinds/sources: BenchmarkDotNet acceptance run.
- exact command/invocation: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`.
- required result/exit: exit 0, exactly **7** executed, 0 failed, elapsed ≤240 s.
- artifacts: `E06-acceptance.log`, `E06-perf.md`.
- owner: S1=coder. applicability: true (retained). rv: 2.

#### R07 — existing iteration-14 allocation budgets
- requirement: existing flat/recursive zero-CTE budgets preserved.
- scenario: retained rv=1 gate run, not re-run in r2 (product unchanged).
- evidence kinds/sources: `eng/perf/iteration14_gate.py` verdicts.
- exact command/invocation: `python3 eng/perf/iteration14_gate.py`.
- required result/exit: exit 0, **56** row/job verdicts within budget; `Cte_Warm_Reused`/`RecursiveCte_Warm_Reused` = 0.00 B/op both toolchains.
- artifacts: `E07-gate.log`, `E07-gate.md`, 3× `E07-*-report-full-compressed.json`.
- owner: S1=coder. applicability: true (retained). rv: 2.

#### R08 — scope / CRLF / D5-§9 / docs / tracking
- requirement: only the intended files change; no forbidden mutation (`_ctes` clear, cache-flag, `Cache=false`, `_dontCache`); CRLF preserved; unrelated dirty files untouched; nothing staged/committed.
- scenario: scoped diff review.
- evidence kinds/sources: `git diff --check` + scoped diff.
- exact command/invocation: `git diff --check` + diff review of `src/nextorm.core`, `tests/nextorm.core.tests`, `tests/nextorm.sqlite.tests`, this status file.
- required result/exit: exit 0; 3 files **+319 / −6**; CRLF preserved; forbidden mutations absent; unrelated untouched; `git diff --cached` empty.
- artifacts: `E08-review.md`, `E10-provenance-ledger.md`.
- owner: S1=coder. applicability: true. rv: 2.

#### SA-READ — executed SQLite sync/async nested-read terminals
- requirement: real sync and async nested-read CTE terminals execute against SQLite and return independently specified nonempty fixtures (closes `DEF-166-C` / the rv=1 guard-closure of row (c) as an executed test).
- scenario: `NestedReadCte_SyncTerminal_Executes` executes `ToList()` twice (second = warm); `NestedReadCte_AsyncTerminal_Executes` executes `ToListAsync()`; both assert rows `{1,2,3}` + `Total(Id==2)==20`.
- evidence kinds/sources: executed SQLite tests (`tests/nextorm.sqlite.tests/TypedCteTests.cs`).
- exact command/invocation: `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NestedReadCte_SyncTerminal_Executes"`; `… ~NestedReadCte_AsyncTerminal_Executes`; combined `… "FullyQualifiedName~NestedReadCte_SyncTerminal_Executes|FullyQualifiedName~NestedReadCte_AsyncTerminal_Executes"`.
- required result/exit: each exit 0, 1 selected / 1 succeeded / 0 skipped; combined exit 0, 2 selected / 2 succeeded.
- artifacts: `SA-READ-sync.log`, `SA-READ-async.log`, `SA-READ-combined.log`.
- owner: S1=coder. applicability: true. rv: 2.

#### SA-MUTATION — mutation classification regressions
- requirement: mutation classification regressions pass and nested prepared mutation remains non-reusable.
- scenario: executed postgres `DataModifyingCtePlanCacheTests`.
- evidence kinds/sources: executed tests.
- exact command/invocation: `dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~DataModifyingCtePlanCacheTests"`.
- required result/exit: exit 0, selected **4/4**, skipped 0, all pass; nested prepared mutation still non-reusable.
- artifacts: `SA-MUTATION-classification.log`.
- owner: S1=coder. applicability: true. rv: 2.

### Sibling-guard dispositions
- **cold/unprepared `Clone` and prepared cyclic read:** guard per R01 + retained cold `CyclicNestedGraph_TerminatesAndFindsMutation` / `CyclicNestedGraph_WithDeepMutation_TerminatesAndFindsMutation` tests; no new executed probe required (cold path unchanged by F1).
- **mutation terminal:** **deferred + trigger** — a provider-executed mutation sync/async terminal is deferred until a mutation/cache/terminal-path change or PostgreSQL integration scope; SQLite mutation is an **unsupported-provider guard only** and is not evidence of execution.

Planned additions are expectations, not evidence.

## PINNED VERSIONED EVIDENCE CONTRACT (rv=1) — SUPERSEDED

Owner stream S1 = coder; CHECK validates. Every row inherits: rv=1; applicability=true unless stated; transcript artifact `docs/specs/status/nested-read-cte-1-evidence/<row>-<phase>.log` with exact resolved arg arrays, stdout/stderr, exit/invocation result, selected counts, elapsed, source revision/diff identity. No optional slots. CHECK re-gather budget: owner CHECK, ≤2 rounds × ≤3 targeted requests; no PASS while an applicable row is open.

### Commands
- C0: `roslyn` structure/members/refs/callers to resolve getter/helper/prepare symbols and both sync/async entry paths (archive exact invocations/results).
- C1: `dotnet build nextorm.slnx -c Debug` → exit 0, 0 warnings/0 errors.
- C2: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~NestedReadCteWarmReuse_AllocatesZero"` → before F1: fails solely from positive allocation; after: exit 0, 1 selected, bytes=0.
- C3: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~Iteration14CteLookupTests"` → after: exit 0, all existing + new methods selected.
- C4: coverage over `nextorm.{core,sqlite,postgres,sqlserver}.tests` via `coverage.settings.xml` (see AGENTS.md) → exit 0, nonempty each.
- C5: `reportgenerator` on the cobertura → exit 0; evaluate 85/75 (warnings off-main).
- C6: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` → exit 0, exactly 7 completed cases, no failures, elapsed ≤240s.
- C7: `python3 eng/perf/iteration14_gate.py` → exit 0; existing flat/recursive zero CTE budgets preserved.
- C8: `git diff --check` + diff review of `src/nextorm.core`, `tests/nextorm.core.tests`, this status file → exit 0; CRLF preserved; unrelated changes untouched.

### Rows
- R01/E01: hoist completeness, prepared/cloned invariant, both entry paths — scout brief + C0 → E01-paths.md; no bypass/post-prepare-write counterexample.
- R02/E02: prepared nested-read target before/after — C2 → E02-allocations.json; red bytes>0 then green bytes=0.
- R03/E03: full variant matrix + existing regressions — C3 → E03-matrix.md; every applicable case selected/passing.
- R04/E04: SQL/params/plan-key identity, shared state, sync/async — C3 → E04-behavior.md; identity/state unchanged.
- R05/E05: build + core/dialect regressions + coverage — C1+C4+C5.
- R06/E06: 7-case acceptance regression — C6 → E06-perf.md; baseline comparison, no noise claims.
- R07/E07: existing perf budgets — C7 → E07-gate.md; zero budgets preserved.
- R08/E08: scope/CRLF/D5-§9/docs/tracking/contract — C8 → E08-review.md; no forbidden mutation/API change, no commits.

### DO units (sequential S1)
- D0 prerequisites/status write — done (E01-paths.md)
- D1 red evidence (add planned tests in `tests/nextorm.core.tests/Iteration14CteLookupTests.cs`; planned method names: NestedReadCteWarmReuse_AllocatesZero, CteReuse_VariantMatrix, NestedReadCte_PreservesSqlParametersAndPlanKey, NestedReadCte_SyncAsyncPolicyMatches — resolve real helpers via roslyn, do not fabricate) — done (all four added; red captured)
- D2 minimal fix in `QueryCommand.cs` — done (F1 applied; four tests + full class green)
- D3 gates C1/C4–C8 + status update — done (boundary sweep; C1/C3/C4/C5/C6/C7/C8 green; see Perf and Progress log)
- DO (iteration r1 n2) — REOPENED by CHECK FAIL: close the 3 open variant rows + assertion gap; same plan revision r=1, no replan — done
- D4 close variant-matrix rows — done: (a) `NestedReadCte_AcyclicSharedBody_ClassifiesAndReusesWarmPlan` (same CteDefinition instance via two bodies; false + warm plan identity + 0 B); (b) `NestedReadCte_PreparedClone_KeepsClassificationAndWarmAlloc` + `NestedMutation_PreparedClone_KeepsClassification` (real `Clone()`/`CloneForCache()`, resolved via roslyn); (c) real sync/async terminal row closed as **guard** (renamed `NestedReadCte_PreparedWarmPolicy_AllocatesZeroAndSharesPlan`; DO→PLAN candidate at `QueryPlanner.cs:559`)
- D5 assertion gap + nonblocking items — done: `:224-225` replaced with an independent expected snapshot (red proof `E04-assertion-red.log`), `:226` `GetType()` coupling dropped for value-CLR-type comparison, XML remark `QueryCommand.cs:490-503` updated (docs only), all logged test runs carry `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0`
- D6 (r=2, active): provider-backed executed sync/async terminal verification + mutation classification regressions
  - D6.1 done: added `NestedReadCte_SyncTerminal_Executes` / `NestedReadCte_AsyncTerminal_Executes` to `tests/nextorm.sqlite.tests/TypedCteTests.cs:497-546`; execute the SQLite terminal (sync `ToList` + second warm `ToList`; async `ToListAsync`) and assert independently specified rows `{1,2,3}` + `Total(Id==2)==20`; SA-READ-sync exit 0 selected 1/1, SA-READ-async exit 0 selected 1/1, combined exit 0 selected 2/2, skipped 0; DEF-166-C fix #1 applied
  - D6.2 done: rows (a)/(b) already closed by the D4 tests with fail-capable assertions — (a) `Iteration14CteLookupTests.cs:275`, (b) `:313` + `:329`; no duplicates added (E03-matrix.md)
  - D6.3 done: postgres `DataModifyingCtePlanCacheTests` exit 0 selected 4/4 skipped 0; nested prepared mutation still non-reusable (`DataModifyingCtePlanCacheTests.cs:62`); SQLite mutation is an unsupported-provider guard, not executed (`SqlDialectBase.cs:973` default false, only `PostgresDialect.cs:34` overrides true)
  - D6.4 done: C1 exit 0 0W/0E; C3 class exit 0 12/12, full core exit 0 1524/1524 0 failed; sqlite full exit 0 1013 total 1012 succeeded 1 skipped (pre-existing probe), both new terminal tests selected (filter 2/2 exit 0); C4 sqlite-only refresh exit 0 (core/postgres/sqlserver rv=1 collections retained with provenance); C5 combined exit 0 overall 82.0/74.2, sqlite 82.6/58.9, getter 100%/100% (22/22); C8 `git diff --check` exit 0, diff = 3 files (QueryCommand +21/-6, Iteration14CteLookupTests +248/0, TypedCteTests +50/0), no forbidden mutation, CRLF preserved, unrelated untouched; C6/C7 not rerun (retained E06/E07 provenance)

### Durable state
Current cycle N=1; Plan revision r=2; Attempt n=1 (fresh, replan reset); DO units D0–D6 all done (D6.1–D6.4 done); **DO→CHECK boundary reached**; next phase CHECK r=2 n=1. Defect history: `DEF-166-A` (open variant rows: acyclic shared/diamond, prepared Clone reuse, real sync/async terminal) observed r1/n1, 1 fix applied r1/n2 (`E03-matrix.md`: D4a/D4b closed as tests; D4c closed as guard), retained 1 fix, CHECK-verifiable at r2 — last event r1/n2. `DEF-166-B` (tautological param assertion `:224-225`, `GetType` coupling `:226`) observed r1/n1, 1 fix applied r1/n2 (`E04-assertion-red.log` proves non-tautology), retained 1 fix, CHECK-verifiable at r2 — last event r1/n2. `DEF-166-C` (sync/async terminal unobservable in a provider-free core test) opened r1/n2, 0 fixes, returned as DO→PLAN candidate (`QueryPlanner.cs:559`); **reopened at r1→r2 as the active r=2 obligation** — owner D6.1/SA-READ, **fix #1 applied at r2/n1** (SQLite executed sync/async terminals `SA-READ-sync.log`/`SA-READ-async.log`; full sqlite `C3-full-sqlite.log`; combined filter `SA-READ-combined.log`; classification `SA-MUTATION-classification.log`; E03-matrix.md), **pending independent CHECK verification at r2 n=1**. D6.4 done. D1 red is the intended negative, not a defect. No escalation.

### Changed files
`src/nextorm.core/Query/QueryCommand.cs` (+21/−6, frozen F1), `tests/nextorm.core.tests/Iteration14CteLookupTests.cs` (+248/−0), `tests/nextorm.sqlite.tests/TypedCteTests.cs` (+50/−0), this status file.

### Perf
- C1 full solution build: exit 0, 0 warnings/0 errors.
- C6 acceptance: exit 0; 7 executed, 0 failed; BDN total 53.6 s, shell 63 s (<=240 s); cached/prepared time ratio 2.37 (Error > Mean, noise-dominated), allocated ratio 7.11; baseline 1.87/7.42 — no improvement claimed, no confirmed regression (E06-perf.md).
- C4/C5 coverage (off-main warnings only, 85/75 not met; **r2 D6.4 refresh: sqlite recollected, core/postgres/sqlserver rv=1 retained with provenance**): core 82.2/74.4, sqlite 82.6/58.9, postgres 77.3/74.2, sqlserver 77.5/75.3; overall 82.0/74.2; getter 100%/100% (22/22) (E05-coverage.md).
- C6/C7 **not rerun** in r2 (product unchanged since green run): retained valid, `E06-perf.md`/`E07-gate.md` provenance.
- C7 iteration-14 gate: exit 0, 56 row/job verdicts within budget; Cte_Warm_Reused and RecursiveCte_Warm_Reused = 0.00 B/op both toolchains (E07-gate.md).

### Next plan
DO r=2 done (D6.1–D6.4); **DO→CHECK boundary reached**; next CHECK r2 n1 (verdict on rv=2 rows; independent verification of DEF-166-C fix #1; no commit).

## ACT

### Done / Verified
- F1 eliminates the per-call `HashSet` in `QueryCommand.HasDataModifyingCte`: prepared nested-read warm reuse **1,760,000 B / 10,000 iters → 0 B** (`E02-allocations.json`).
- AC1–AC3 met (AC1: bytes=0; AC2: read-only graphs false, reachable mutation true and read-reuse disabled; AC3: cold traversal cycle-safe, SQL/params/plan-key/state unchanged).
- R01–R08 + SA-READ + SA-MUTATION met on the rv=2 contract; all artifacts present (`E10-provenance-ledger.md`).
- CHECK r=2 n=1 **PASS** (`E09-mechanical-audit.md`, `E10-provenance-ledger.md`).

### Changed files
| Path | Role | r2 diff |
| --- | --- | --- |
| `src/nextorm.core/Query/QueryCommand.cs` | F1 product fix (internal getter body) | +21 / −6 |
| `tests/nextorm.core.tests/Iteration14CteLookupTests.cs` | zero-alloc + matrix/state regression tests | +248 / −0 |
| `tests/nextorm.sqlite.tests/TypedCteTests.cs` | executed sync/async terminal tests | +50 / −0 |
| `docs/specs/status/nested-read-cte-1.md` + `docs/specs/status/nested-read-cte-1-evidence/` | status + evidence | n/a |

### Residual / open
- issue **#166 remains OPEN**; the tree is **uncommitted** (no commit/push/stage; no auto-commit granted).
- Deferred: real mutation sync/async terminal (trigger: mutation/cache/terminal-path change or PostgreSQL integration scope); dedicated nested-read benchmark/budget (trigger: benchmark coverage expands); automated `validate_inner_loop.py` (absent in repo; trigger: manual evidence maintenance warrants it).

### Next plan
— (flow closed; single cycle; no further cycle planned)

## Progress log
2026-10-05T12:19:22Z | PLAN | revision 1 | iteration 0/3 | plan ready; status file opened; DO not started (n=0); next D1 | docs/specs/status/nested-read-cte-1.md
2026-10-05T12:22:20Z | DO | revision 1 | iteration 1/3 | DO started (n=1); single stream S1; worktree unchanged, no commits | docs/specs/status/nested-read-cte-1-evidence/E01-paths.md
2026-10-05T12:22:20Z | DO | revision 1 | iteration 1/3 | D0 done: roslyn resolved HasDataModifyingCte (QueryCommand.cs:504), IsPrepared (:306), PrepareCtes (QueryCommand.QueryPreparer.cs:267), sole production caller QueryPlanner.cs:559; sync+async share it, no separate async path | docs/specs/status/nested-read-cte-1-evidence/E01-paths.md
2026-10-05T12:25:06Z | DO | revision 1 | iteration 1/3 | D1 done: four tests added; allocation+syntax red on current code, matrix+SQL green; allocation=NestedReadCteWarmReuse_AllocatesZero 1,760,000 bytes over 10_000 iters (exit 2, selected 1); SyncAsync same 1,760,000 (exit 2, selected 3/2 passed); build exit 0 warnings 0 | docs/specs/status/nested-read-cte-1-evidence/E02-allocation-red.log, E04-behavior-baseline.log
2026-10-05T12:26:06Z | DO | revision 1 | iteration 1/3 | D2 done: F1 in QueryCommand.cs HasDataModifyingCte getter returns false after the flat scan when IsPrepared (cold recursive/cyclic path kept; no _ctes cleared, no cache/D5-§9 mutation); build exit 0 warnings 0; four tests exit 0 selected 4/4, allocated 0; full Iteration14CteLookupTests exit 0 selected 9/9 | docs/specs/status/nested-read-cte-1-evidence/E02-allocations.json, E04-behavior.md, E02-fours-green.log, E03-class-green.log
Notice: scripts/validate_inner_loop.py is ABSENT in this repo → structured manual test evidence (exact arg arrays, --filter, exit codes, selected counts) is used; do not recreate it. Notice: todowrite unavailable in this harness; status file is the sole tracker.
2026-10-05T12:28:32Z | DO | revision 1 | iteration 1/3 | C1 done: dotnet build nextorm.slnx -c Debug exit 0, 0 Warning(s) 0 Error(s) | docs/specs/status/nested-read-cte-1-evidence/C1-build.log
2026-10-05T12:29:00Z | DO | revision 1 | iteration 1/3 | C3 done: class filter exit 0 selected 9/9 (4 new + 5 existing); full core tests exit 0 total 1521/1521, failed 0, skipped 0 | docs/specs/status/nested-read-cte-1-evidence/C3-class.log, C3-full-core.log
2026-10-05T12:30:03Z | DO | revision 1 | iteration 1/3 | C4/C5 done: coverage collect exit 0 for core/sqlite/postgres/sqlserver (nonempty; 1521/1011/742/556 tests, 0 failed, 1 skipped sqlite); reportgenerator exit 0; overall line 82.0% branch 74.2% (core 82.2/74.4, sqlite 82.6/58.9, postgres 77.3/74.2, sqlserver 77.5/75.3); branch 1.0.9-b off-main → warnings only (85/75 not met) | docs/specs/status/nested-read-cte-1-evidence/E05-coverage.md
2026-10-05T12:32:29Z | DO | revision 1 | iteration 1/3 | C6 done: acceptance exit 0, exactly 7 executed, 0 failed; BDN 53.6 s, shell 63 s <=240 s; ratio time 2.37 alloc 7.11 (baseline 1.87/7.42); noise-dominated, no improvement/regression claim | docs/specs/status/nested-read-cte-1-evidence/E06-perf.md
2026-10-05T12:48:32Z | DO | revision 1 | iteration 1/3 | C7 done: iteration14_gate.py exit 0, benchmark sub-run exit 0 wall 843.3 s, all 56 row/job verdicts within budget; Cte_Warm_Reused / RecursiveCte_Warm_Reused / Join4_Warm_Reused = 0.00 B/op both toolchains | docs/specs/status/nested-read-cte-1-evidence/E07-gate.md
2026-10-05T12:49:59Z | DO | revision 1 | iteration 1/3 | C2 boundary re-measure: NestedReadCteWarmReuse_AllocatesZero exit 0 selected 1/1; target before (historical red) 1,760,000 B -> after 0 B | docs/specs/status/nested-read-cte-1-evidence/E02-allocations.json, E02-target-green.log
2026-10-05T12:51:01Z | DO | revision 1 | iteration 1/3 | C8 done: git diff --check exit 0; only QueryCommand.cs (+9/-2) and Iteration14CteLookupTests.cs (+150/-0) changed under src/tests; CRLF preserved; no _ctes clear / cache-flag change / Cache=false / _dontCache; unrelated dirty files untouched | docs/specs/status/nested-read-cte-1-evidence/E08-review.md
2026-10-05T12:51:30Z | DO | revision 1 | iteration 1/3 | D3 done; all DO units done (D0-D3); DO->CHECK boundary reached; artifacts dirs restored; no commit/push/stage | docs/specs/status/nested-read-cte-1.md
2026-10-05T12:56:12Z | CHECK | revision 1 | iteration 1/3 | CHECK r1 n1 FAIL — 3 open variant rows + tautological param assertion; loop-back DO (n->2/3); no CHECK->PLAN | docs/specs/status/nested-read-cte-1-evidence/E09-mechanical-audit.md
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | DO loop-back started (r=1 unchanged, n=2/3); D4/D5 pending; defect keys DEF-166-A (open variant rows), DEF-166-B (tautological param assertion) at 0 fixes | docs/specs/status/nested-read-cte-1.md
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | D4 started: close variant rows (a) acyclic shared/diamond, (b) prepared Clone/CloneForCache, (c) real sync/async terminal | tests/nextorm.core.tests/Iteration14CteLookupTests.cs
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | D4 done: added NestedReadCte_AcyclicSharedBody_ClassifiesAndReusesWarmPlan, NestedReadCte_PreparedClone_KeepsClassificationAndWarmAlloc, NestedMutation_PreparedClone_KeepsClassification; terminal probe shows provider-free execution impossible (E04-terminal-probe.log exit 2); DEF-166-A fix #1 applied | docs/specs/status/nested-read-cte-1-evidence/E04-terminal-probe.log
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | D5 started: replace tautological param assertion, drop GetType coupling, update XML remark | tests/nextorm.core.tests/Iteration14CteLookupTests.cs
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | D5 done: independent expected param snapshot (red proof {8}!={7}, E04-assertion-red.log exit 2) + value-CLR-type comparison replaces GetType==FakeParameter; XML remark QueryCommand.cs:490-503 updated (docs only); DEF-166-B fix #1 applied | docs/specs/status/nested-read-cte-1-evidence/E04-assertion-red.log
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | rerun C1 build: dotnet build nextorm.slnx -c Debug exit 0, 0 Warning(s) 0 Error(s) | docs/specs/status/nested-read-cte-1-evidence/E09-build.log
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | rerun C3a class: Iteration14CteLookupTests exit 0 selected 12/12 passed (3 new + rename + 8 existing) | docs/specs/status/nested-read-cte-1-evidence/E03-class.log
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | rerun C3b full core: nextorm.core.tests exit 0 total 1524/1524, failed 0, skipped 0 | docs/specs/status/nested-read-cte-1-evidence/C3-full-core.log
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | rerun C4/C5 coverage: collect exit 0 core 1524/1524, sqlite 1011 (1 skipped), postgres 742, sqlserver 556; reportgenerator exit 0; core 82.2/74.4, overall 82.0/74.2, getter line/branch 100%/100% (22/22); acceptance/perf NOT rerun (behavior unchanged) | docs/specs/status/nested-read-cte-1-evidence/E05-coverage.md
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | sync/async row closed as guard: both terminal families converge statically at QueryPlanner.cs:559 (guard closure), unobservable provider-free; DEF-166-C opened 0 fixes; DO->PLAN candidate raised (plan re-routes the row) | src/nextorm.core/DataContext/QueryPlanner.cs:559
2026-10-05T13:06:31Z | DO | revision 1 | iteration 2/3 | DO r1 n2 done (D4/D5 done); DO->CHECK boundary reached; no commit/push/stage; DEF-166-A/B each 1 fix, DEF-166-C 0 fixes (candidate) | docs/specs/status/nested-read-cte-1.md
2026-10-05T13:16:48Z | CHECK | revision 1 | iteration 1/3 | CHECK r1 n1 verdict FAIL -> CHECK→PLAN; planner issued r=2 | docs/specs/status/nested-read-cte-1.md
2026-10-05T13:16:48Z | PLAN | revision 2 | iteration 1/3 | Replanned r1→2 (iteration 1/3); rv=2 supersedes rv=1 | docs/specs/status/nested-read-cte-1.md
2026-10-05T13:16:48Z | PLAN | revision 2 | iteration 1/3 | PLAN r=2 READY | docs/specs/status/nested-read-cte-1.md
2026-10-05T13:18:40Z | DO | revision 2 | iteration 1/3 | D6.1 start: add NestedReadCte_SyncTerminal_Executes / NestedReadCte_AsyncTerminal_Executes (nested read CTE, outer body declares inner) to tests/nextorm.sqlite.tests/TypedCteTests.cs | tests/nextorm.sqlite.tests/TypedCteTests.cs
2026-10-05T13:20:45Z | DO | revision 2 | iteration 1/3 | D6.1 done: SA-READ sync exit 0 selected 1/1 skipped 0 (SA-READ-sync.log); async exit 0 selected 1/1 skipped 0 (SA-READ-async.log); combined exit 0 selected 2/2 (SA-READ-combined.log); both execute the SQLite terminal and assert rows {1,2,3} + Total(Id==2)==20; DEF-166-C fix #1 applied | docs/specs/status/nested-read-cte-1-evidence/SA-READ-combined.log
2026-10-05T13:20:45Z | DO | revision 2 | iteration 1/3 | D6.2 done: rows (a)/(b) already closed by D4 tests with fail-capable assertions — (a) Iteration14CteLookupTests.cs:275, (b) :313 + :329; no duplicates added (planned PreservesState tests NOT added) | docs/specs/status/nested-read-cte-1-evidence/E03-matrix.md
2026-10-05T13:20:45Z | DO | revision 2 | iteration 1/3 | D6.3 done: postgres DataModifyingCtePlanCacheTests exit 0 selected 4/4 skipped 0; nested prepared mutation non-reusable (DataModifyingCtePlanCacheTests.cs:62); SQLite mutation = unsupported-provider guard, not executed (SqlDialectBase.cs:973 default false; only PostgresDialect.cs:34 overrides true) | docs/specs/status/nested-read-cte-1-evidence/SA-MUTATION-classification.log
2026-10-05T13:20:45Z | DO | revision 2 | iteration 1/3 | core class rerun: Iteration14CteLookupTests exit 0 selected 12/12 skipped 0 | docs/specs/status/nested-read-cte-1-evidence/E03-class.log
2026-10-05T13:21:30Z | DO | revision 2 | iteration 1/3 | D6.1-D6.3 done; D6.4 pending (boundary sweep); no commit/push/stage; F1 product code under src/ untouched in r2 | docs/specs/status/nested-read-cte-1.md
2026-10-05T13:22:42Z | DO | revision 2 | iteration 1/3 | D6.4 C1 done: dotnet build nextorm.slnx -c Debug exit 0, 0 Warning(s) 0 Error(s), elapsed 6.57 s | docs/specs/status/nested-read-cte-1-evidence/C1-build.log
2026-10-05T13:22:52Z | DO | revision 2 | iteration 1/3 | D6.4 C3 class done: Iteration14CteLookupTests exit 0 total 12/12 succeeded 12 failed 0 skipped 0 | docs/specs/status/nested-read-cte-1-evidence/C3-class.log
2026-10-05T13:23:03Z | DO | revision 2 | iteration 1/3 | D6.4 C3 full core done: nextorm.core.tests exit 0 total 1524/1524 failed 0 skipped 0 | docs/specs/status/nested-read-cte-1-evidence/C3-full-core.log
2026-10-05T13:23:29Z | DO | revision 2 | iteration 1/3 | D6.4 sqlite full done: nextorm.sqlite.tests exit 0 total 1013 succeeded 1012 failed 0 skipped 1 (pre-existing SqliteRowIdLobProbeTests probe) | docs/specs/status/nested-read-cte-1-evidence/C3-full-sqlite.log
2026-10-05T13:23:53Z | DO | revision 2 | iteration 1/3 | D6.4 SA-READ inclusion confirmed: sqlite --filter NestedReadCte_Sync/AsyncTerminal_Executes exit 0 selected 2/2 skipped 0 (both r2 D6.1 tests present in the full project) | docs/specs/status/nested-read-cte-1-evidence/SA-READ-combined.log
2026-10-05T13:24:02Z | DO | revision 2 | iteration 1/3 | D6.4 C4 sqlite-only refresh done: dotnet-coverage collect exit 0 total 1013 succeeded 1012 skipped 1; cov-sqlite.cobertura.xml refreshed (8,725,406 B); core/postgres/sqlserver rv=1 collections retained with provenance | docs/specs/status/nested-read-cte-1-evidence/C4-sqlite.log
2026-10-05T13:24:30Z | DO | revision 2 | iteration 1/3 | D6.4 C5 combined done: reportgenerator exit 0 (4x Cobertura); overall line 82.0% (42295/51555) branch 74.2% (21816/29392); sqlite 82.6/58.9; getter line/branch 100%/100% (22/22); branch 1.0.9-b off-main -> warnings only | docs/specs/status/nested-read-cte-1-evidence/E05-coverage.md
2026-10-05T13:25:32Z | DO | revision 2 | iteration 1/3 | D6.4 C8 done: git diff --check exit 0; src/tests diff = QueryCommand.cs +21/-6, Iteration14CteLookupTests.cs +248/-0, TypedCteTests.cs +50/-0; SQLite hunk D6-only (two terminal tests); no _ctes clear / cache-flag / Cache=false / _dontCache; CRLF preserved; unrelated dirty files untouched | docs/specs/status/nested-read-cte-1-evidence/E08-review.md
2026-10-05T13:26:37Z | DO | revision 2 | iteration 1/3 | D6.4 done; D6.1-D6.4 all done; **DO->CHECK boundary reached**; DEF-166-C fix #1 applied at r2/n1, pending independent CHECK verification; C6/C7 not rerun (retained E06/E07 provenance); no commit/push/stage | docs/specs/status/nested-read-cte-1.md
2026-10-05T13:36:14Z | ACT | revision 2 | iteration 1/3 | CHECK r2 n1 PASS — all AC/rows met | docs/specs/status/nested-read-cte-1-evidence/E09-mechanical-audit.md, E10-provenance-ledger.md
2026-10-05T13:36:14Z | ACT | revision 2 | iteration 1/3 | ACT: registry updated; status finalized; issue #166 left OPEN (no auto-commit); no commit/push | docs/specs/design/API-NAMING-REVIEW.md:5239
2026-10-05T13:36:14Z | ACT | revision 2 | iteration 1/3 | ACT closed | docs/specs/status/nested-read-cte-1.md
