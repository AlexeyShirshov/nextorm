# Issue #199 — Restore scalar IN/Contains shared-command cache safety (task D199)

- task: `rc1-tail-199-sticky-cache-1` (D199)
- issue: #199
- collection: `1.0.9-rc1-tail`
- branch: `1.0.9-rc1`
- worktree: single, no branch/worktree/merge
- status: **closed — CHECK PASS** (r1, n=2/3, rv1); ACT complete; task-owned commit made
- cycle: N=1
- plan revision: r=1
- attempt: n=2/3
- contract: rv=1 (sealed at Gate 1; no supersession)
- defect key: `199:scalar-in:sticky-shared-querycommand-cache`
- mode: autonomous; one group, sequential; no commits until D6; no push/branch/worktree/merge
- milestone: `1.0.9-rc1`

## Task

Fix the sticky shared-command cache-policy defect caused by scalar `IN`/`Contains` SQL translation:
an invocation may bypass plan caching, but must **not** change persistent `QueryCommand.Cache` policy
or poison subsequent shared-command calls. Fix at the source (`InValuesTranslator`), not at call sites.

## Cycle goal

Restore scalar IN/Contains cache safety:

- An invocation may bypass plan caching for a call (call-local `storeInCache`), but must never flip
  persistent `QueryCommand.Cache` to `false` or otherwise poison later reuses of a shared command.
- Captured scalar SELECT and shared `.Any()` keep `Cache == true` after preparation; later shared-command
  calls reuse the cached plan.
- Unkeyed captured scalar collections bypass lookup **and** storage via call-local `storeInCache`, with
  classification computed before lookup and following the current referenced-query graph.

## Current state

- phase: `ACT` / `closed`
- plan revision: `r=1`
- attempt: `n=2/3`
- cycle: `N=1`
- contract: `rv1` sealed, no supersession
- CHECK: **PASS** (independent verifier, r1/n=2/3/rv1) — all applicable R199-01..R199-09 satisfied.
- E15 satisfied (independent certification PASS). E16 done (D6 task-owned commit, message begins `#199`).
- `Done / Verified` (evidence-bound): build **0 warnings / 0 errors**; D199 `D199ScalarInPlanCacheTests`
  **13/13**; core `QueryCacheControlsTests` **11/11**; D193 tuple suite green; E04 mutations sticky **8/13**
  and suppression **4/13** fail then candidate restored; boundary sweep **8929 passed / 0 failed / 8731
  succeeded / 198 skipped**; all-provider integration **3311 total / 0 failed**; coverage line **88.4 %** /
  branch **80.1 %** (≥ 85 / 75); perf **7/7** cases ratio **1.99** (≤ 2.244); **check PASS**.
- Next plan: — (flow closed)
- D0 complete (this status file written); D6 finalized the flow.

## Durable state

- `Current cycle N=1`
- `Plan revision r=1` (never replanned; rv1 sealed, no supersession)
- `Attempt n=2/3` (CHECK loop-back n1→n2 for missing mandatory planned tests; the counter is not reset by
  the PASS/ACT — it stays 2/3 as the final attempt index)
- `Defect history`: see table below (key `199:scalar-in:sticky-shared-querycommand-cache`).
- Session/task_id resets never reset `r`, `n`, or the defect history; those live in this file.

## Defect history

| defect key | observed | fixes applied | evidence pointer | last recurrence | escalation |
| --- | --- | --- | --- | --- | --- |
| `199:scalar-in:sticky-shared-querycommand-cache` | r1/n1 | 1 | fix: sticky write removed (`src/nextorm.core/Visitors/InValuesTranslator.cs`), planner suppression + refresh (`src/nextorm.core/DataContext/QueryPlanner.cs`); `check-verdict` | — (no product recurrence) | — |

- Root cause: `InValuesTranslator.cs:58-59` changes persistent command policy during SQL translation;
  `QueryCommand.cs:330-335` stores it in `_dontCache`; reset and shared-command replacement do not undo
  it. Fix at source.
- One applied fix (r1/n1→r1/n2 candidate) closes the defect; the CHECK r1/n1 FAIL and the r1/n2 loop-back
  were **evidence-completeness gaps** (missing mandatory planned tests: unkeyed `@in`, unkeyed captured
  array, duplicates/default unkeyed, 4th `storeInCache`×`Cache` combination) with **no implementation or
  security defect confirmed**. The final independent CHECK r1/n2/3 is **PASS**.
- CHECK/red/fix/loop-back events append the key and the applied-fix count to the progress log when
  applicable.

## Acceptance criteria

- **R199-01** SQL-backed regression fails on original and passes after fix; captured scalar SELECT and
  shared `.Any()` retain `Cache == true`; later shared-command calls reuse cached plan.
  *Negative:* InMemory-only test / zero selected / infra failure / flag still false or caching still disabled.
- **R199-02** Unkeyed captured scalar collections bypass lookup AND storage via call-local `storeInCache`;
  classification available before lookup and follows current referenced-query graph.
  *Negative:* classification at render time, stale state survives `ReplaceCommand`, unkeyed plan reused
  after collection change.
- **R199-03** Keyed scalar WHERE caching, scalar results, existing explicit cache policy, D193 tuple behavior
  remain correct. *Negative:* unsafe reuse, explicit disabled caching becomes enabled, tuple changes.
- **R199-04** Temp-table and SQL Server TVP routes execute and preserve cache policy.
  *Negative:* only ordinary scalar IN exercised, configured TVP metadata displaced, shared policy poisoned.
- **R199-05** `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings 0 errors; affected tests pass;
  CRLF/CPM/test-safety hold. *Negative:* warnings/timeout/mixed endings/dep outside CPM/skip.
- **R199-06** Exactly seven acceptance benchmarks within 240s; Mean/Allocated/comparable ratio recorded;
  artifacts + fixture restored. *Negative:* missing cases/timeout/noncomparable ratio comparison/unexplained
  deterioration/restoration mismatch.
- **R199-07** Coverage collected/reported vs 85% line / 75% branch under branch warning policy.
  *Negative:* missing/unreadable, absent comparison, falsely calling it main hard-gate pass.
- **R199-08** Status/ledger accurately records scope/results/deferred/risks.
  *Negative:* planned locations as executed evidence; silently upgrading collection verification.
- **R199-09** Independent check (author≠certifier) PASS with every applicable rv1 row satisfied.
  *Negative:* any applicable row missing/blocked/failed/unexamined.
- **R199-10** Only task-owned files committed, message starts `#199`; no push/branch/worktree/merge.
  *Negative:* unrelated changes, unauthorized git.

## Chosen fix (Approach A — preparation-time call-local scalar-unkeyed flag)

All names below are **PLANNED symbols** (verify with Roslyn once created):

- Add `internal bool HasUnkeyedScalarInValues` beside `HasUnkeyedTupleInValues`
  (`QueryCommand.cs:134-142`).
- Add `ScanUnkeyedScalarInValues` + FROM/metadata traversal helpers beside the tuple scanner
  (`QueryCommand.QueryPreparer.cs:1891-2113`). Mirror tuple-scanner clause/from coverage and cycle
  protection (`HashSet<QueryCommand> visited`). Detect supported scalar-collection `Contains` and scalar
  `@in` via existing translation eligibility; do **not** classify `string.Contains` or tuple collections
  as scalar IN; metadata only, no enumeration/evaluation. Exclude the rendering owner's root
  WHERE/PREWHERE (they own keyed partitions); **include** descendant WHERE/PREWHERE when scanning for the
  parent's policy (owner-relative exclusion), so shared `Any`/`Count` referenced conditions are classified
  unkeyed. Recurse `_referencedQueries` and other command-bearing sources. Preserve the
  `NewArrayExpression` exception (inline literal alone must not trigger suppression).
- Assign flag alongside tuple classification at `QueryCommand.QueryPreparer.cs:99`.
- Clear it in `ResetPreparation` beside `QueryCommand.cs:843`.
- Copy it in clone beside `QueryCommand.Clone.cs:35`.
- Add planned internal refresh entry point `RefreshUnkeyedScalarInValues` for already-prepared commands.
- In `QueryPlanner.cs:565-574`: preserve prepare/shape-refresh; for an already-prepared command refresh
  scalar classification against the current command graph; before `:601` lower local `storeInCache` when
  the flag is true. **Never set `Cache=true`.** Explicit caller policy authoritative.
- Remove only the sticky-write conditional at `InValuesTranslator.cs:58-59`; retain partition evaluation and
  tuple dispatch.
- Do **not** modify tuple translation, tuple scanner, `HasUnkeyedTupleInValues`, or tuple planner suppression.
- Traps resolved: `ReplaceCommand` does not reset scan flags → prepared-reuse refresh required; excluding
  WHERE at every recursive level would miss the shared `AnyCommand` case → owner-relative exclusion.
- Guard: removing the sticky write lets `RefreshInValuesShape` continue (it early-returns on `!cmd.Cache`) —
  never set `Cache=true`.

### What the task did not specify (recorded)

- InMemory unsuitable → SQLite red→green.
- Prepared replacement refresh required.
- Descendant partition ownership conservative.
- Scalar PREWHERE fixture deferred (F1).
- Perf tolerance 1.87 baseline / 2.244 threshold.
- Coverage warns off main.
- Preserve dirty artifacts.
- Add/link #199 lane without rewriting historical collection status.

## DO units / streams

| unit | stream | description | state |
| --- | --- | --- | --- |
| D0 | status | write plan/status (this file) + inventory dirty state/baseline commit/artifact fingerprints | done |
| D1 | coder-tests | add first two SQL-backed regressions; record assertion-only red on original; capture scalar rejection baseline | done |
| D2 | coder | implement flag/scanner/refresh/planner suppression/reset/clone; remove `InValuesTranslator.cs:58-59` | done |
| D3 | coder-tests | remaining variants, core guards, integration + shared-command + temp-table/TVP coverage, narrow mutations + restore | done |
| D4 | coder-build/perf/coverage | CRLF normalize; E06 boundary, E07 integration + E08 route evidence, E11 perf + E12 restore, E13 coverage all done; artifacts/fixture restored; ledger populated; augmented-suite boundary refresh re-verified (n2: 8929/0/8731/198, rc=0) | done |
| D5 | check | independent re-check (loop-back n2): re-gather batch 2 done (fresh coverage + E01–E17 reconciliation); verdict recorded | done |
| D6 | coder-git/status | record verdict + #199 lane/link without rewriting historical collection verification; commit task-owned only, message begins `#199` | done |
| F1 | — | scalar PREWHERE fixture | deferred |
| F2 | — | incremental/dirty-flag optimization of scalar rescanning | deferred |

## Decisions made

- Fix at source in `InValuesTranslator` (remove sticky write); do not patch call sites or shared-command
  consumers.
- Classification happens at preparation time, call-local; never enumerate/evaluate collections during scan.
- Owner-relative exclusion of root WHERE/PREWHERE, include descendants → shared `Any`/`Count` conditions
  classified unkeyed.
- Prepared-reuse refresh on already-prepared commands (because `ReplaceCommand` does not reset scan flags).
- Never set `Cache=true`; explicit caller policy is authoritative.
- Tuple/D193 translation, scanner, and planner suppression are out of scope and must stay unchanged.
- No public API rename; no public prose/RU mirror; no perf/design registry edits.

## Risks / known issues

| risk | mitigation |
| --- | --- |
| misclassify `Contains` / miss scalar `@in` | eligibility via existing translation logic; tests both entry points |
| recursive root-exclusion missing shared conditions | owner-relative exclusion + shared `Any`/`Count` tests |
| stale flags after `ReplaceCommand` | prepared-reuse refresh (planned `RefreshUnkeyedScalarInValues`) |
| conservative descendant suppression overhead | measured benchmark; F2 deferred optimization |
| green tests that never render SQL / zero selected | SQL-backed red test; nonzero executed count required |
| provider skips or asserted-but-unexecuted routes | all-provider integration + route-evidence.md |
| benchmark restore overwriting dirty artifacts | before/after inventories + SHA256; preserve pre-existing dirty |

- Confidence: high in root cause/ordering; perf + runtime branch execution remain measured obligations.

## Perf measurement

- Needed: yes.
- Command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
  — exactly 7 cases, ≤240s, record Mean/Allocated per case.
- Comparable ratio: `Cached_ToList / Prepared_ToList`; baseline 1.87, investigation threshold 2.244.
- Do **not** compare `Cached_PlanOnly_Param`.
- Preserve pre-existing dirty artifacts; apply documented `git checkout -- benchmarks/BenchmarkDotNet.Artifacts`;
  restore fixture byte-for-byte from `benchmarks/nextorm.benchmark/data/test.db`; record hashes.
- Never discard unrelated user changes.
- Benchmark numbers go in this cycle status, **not** in a baseline rewrite.

## Reconnaissance

Not needed — G1–G3 already establish reachability / ordering / ownership / lifecycle. Red tests and the
benchmark are validation, not solution-selection recon.

## Unit mode

Single worktree on branch `1.0.9-rc1`; one group, sequential. No separate branch, worktree, or merge.

## Variant matrix

Every row closed by test / guard / deferred+trigger:

| variant | closure |
| --- | --- |
| captured scalar list root WHERE | test |
| captured scalar array | test |
| inline `NewArrayExpression` | test + guard |
| HAVING / JOIN ON / SELECT / nested | test |
| referenced-query WHERE under shared `Any`/`Count` | test |
| PREWHERE scalar IN | guard now; execution **DEFERRED** (F1) — trigger: a scalar PREWHERE fixture is added or PREWHERE partition ownership/shape computation changes |
| other scanner clause/source families | guard traversal parity + existing boundary suite |
| empty collection / duplicates / scalar default | test |
| value vs reference scalar elements + nullable/null | test |
| null collection | guard/test: rejection unchanged, no policy mutation |
| `Contains` vs scalar `@in` | test both entry points |
| `storeInCache` true/false × `Cache` true/false | test all four |
| reset / clone / prepared replacement | test + review |
| tuple / D193 | existing tests + diff guard |
| providers | full dialect suites + SQLite/PG/SQL Server/MySQL/ClickHouse integration, no required skips |
| temp-table / TVP | test routes + policy; SQL Server configured/auto TVP metadata |

**Mutation check:** temporarily restore the sticky write (must fail policy regressions), then disable
scalar planner suppression (must fail unkeyed cache-safety), restore the candidate exactly.

## Per-unit test scope

- **D1** red tests: `tests/nextorm.sqlite.tests` — new class only, normal Debug build.
- **D2** implementation: `tests/nextorm.core.tests/QueryCacheControlsTests.cs` +
  `tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs` + `D193TupleInPlanCacheTests`, normal rebuild.
- **D3** variants/integration: same targeted suites + build integration.
- **D4** boundary — single broad sweep: solution build, full solution tests, explicit all-provider
  integration, coverage, perf acceptance; no `--no-build` until final successful solution build.
- **D5** independent CHECK; **D6** commit/bookkeeping (no code changes after certification).
- Coverage: new recognition, literal exclusion, true/false suppression, root/descendant handling,
  reset/replacement must be exercised; thresholds 85 line / 75 branch.

## Test strategy (planned)

New planned file `tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs`, setup adapted from
`D193TupleInPlanCacheTests.CreateDb()` / `SqliteTestContext.Create()`; isolate cache-control from parallel
interference. Planned tests:

- `CapturedScalarInSelect_ShouldPreserveCommandCachePolicy` (prepare `storeInCache:true`,
  `createEnumerator:false`; assert SQL prepared and `command.Cache==true`; **ORIGINAL MUST FAIL**)
- `SharedAny_ScalarContains_ShouldPreservePolicyAndLaterCacheHits` (seed; captured-list WHERE `.Any()`;
  assert result + shared `AnyCommand` policy; replace with ordinary equivalent twice; prepared-command
  reference equality; **ORIGINAL MUST FAIL** policy assertion)
- `SharedCount_ScalarContains_ShouldPreservePolicyAndLaterCacheHits`
- `UnkeyedCapturedScalarIn_ShouldBypassCacheAndReflectChangedValues` (HAVING/JOIN ON/SELECT/nested; change
  contents between equivalent preparations; correct results, retained policy, no stale reuse)
- `KeyedCapturedScalarIn_ShouldReusePlanAndRefreshValues` (list + array in WHERE; equal-shape hit; changed
  shape correct)
- `InlineScalarArray_ShouldPreserveExistingCacheBehavior`
- `ScalarIn_StoreInCacheFalse_ShouldNotMutatePolicy`
- `ScalarIn_ExplicitCacheFalse_ShouldRemainFalse`
- `ScalarIn_ResetAndClone_ShouldPreservePreparationPolicySemantics`

Supplementary planned core guards in `tests/nextorm.core.tests/QueryCacheControlsTests.cs`:
`CapturedScalarContains_PrepareWithoutCaching_ShouldPreserveCommandPolicy`;
`CapturedScalarContains_ShouldRespectExplicitDisabledPolicy` (NOT the SQL regression).

Planned integration cases in `tests/nextorm.integration.tests/CommonTestSuite.In.cs`: captured list/array
repetition + content changes; shared Any/Count then ordinary calls; supported unkeyed scalar SQL;
temp-table route and SQL Server TVP configured+auto.

## Priority matrix

- **P1** = R199-01..06, 09 and all modified query-execution/cache-policy lines, root/descendant recognition,
  reset/clone/replacement, lookup/store suppression, tuple isolation, provider + temp-table/TVP evidence,
  cached/prepared ratio, shared-command immutability.
- **P2** = R199-07, 08, 10 presentation/bookkeeping/hygiene (obligations remain mandatory; CHECK must not
  downgrade P1).

## Docs

- Add internal/XML comments on scalar-flag lifetime, root ownership, descendant conservatism, prepared
  refresh.
- Update target status + link #199 lane from collection status.
- **NO** public API rename, no public prose/RU mirror, no perf/design registry edits (registries describe
  existing invariants this fix restores).

## Deferred + trigger

- **F1** scalar PREWHERE fixture — deferred. Trigger: a scalar PREWHERE fixture is added or PREWHERE
  partition ownership/shape computation changes.
- **F2** incremental/dirty-flag optimization of scalar rescanning — deferred. Trigger: measured
  scanner-attributable overhead including any investigated ratio deterioration.

## Next plan + next todo

- Next plan: — (flow closed).
- Next todo: — (none).

## Changed files

Task-owned files (the D6 commit manifest):

- `src/nextorm.core/DataContext/QueryPlanner.cs`
- `src/nextorm.core/Query/InValues.cs`
- `src/nextorm.core/Query/QueryCommand.Clone.cs`
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`
- `src/nextorm.core/Query/QueryCommand.cs`
- `src/nextorm.core/Visitors/InValuesTranslator.cs`
- `tests/nextorm.core.tests/QueryCacheControlsTests.cs`
- `tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs` (new)
- `docs/specs/status/rc1-tail-199-sticky-cache-1.md` (this file; new)
- `docs/specs/status/collection-1.0.9-rc1-tail.md` (corrective reference only)

Excluded (not committed): `artifacts/pdca/` (pre-existing untracked), restored generated
`tests/coverage/coverage.cobertura.xml`, and restored `BenchmarkDotNet.Artifacts/` changes.

## Pointers

- Root-cause source: `src/nextorm.core/Visitors/InValuesTranslator.cs:58-59`.
- Persistent policy store: `src/nextorm.core/QueryCommand.cs:330-335`, reset at `:843`.
- Tuple counterpart: `HasUnkeyedTupleInValues` (`QueryCommand.cs:134-142`); scanner
  `QueryCommand.QueryPreparer.cs:1891-2113`; classification assignment `:99`; clone `QueryCommand.Clone.cs:35`.
- Planner suppress: `src/nextorm.core/Query/QueryPlanner.cs:565-574`, `:601`.
- Evidence root: `/tmp/nextorm-199-r1/`.
- Artifact root for this cycle: `/tmp/nextorm-199-r1/` (logs, inventories, hashes).

## Evidence contract rv1

Notation: `T[log](argv)` = `env DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 600 argv`,
teed to `/tmp/nextorm-199-r1/log`, Bash `pipefail`, record child exit code, exact argv/env, elapsed time,
log path; exit 124 = fail; green selectors need a nonzero executed count. Integration adds
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; coder loads the
integration skill first; start Podman if needed and recheck. Required provider skips never satisfy a row.
Expected artifacts for planned tests are expectations; populate the ledger with Roslyn-verified
symbols/locations once created.

| id | pri | evidence | owner | cadence | rv |
| --- | --- | --- | --- | --- | --- |
| E01 | P1 | SQL-backed red — `T[red.log](dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~D199ScalarInPlanCacheTests")` before production edits; nonzero, not 124, both fail on expected policy assertions not infra. Artifacts `red.log`, `red-source.diff`, baseline commit. | coder-tests | always | rv1 |
| E02 | P1 | `T[sqlite-targeted.log](dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~D199ScalarInPlanCacheTests|FullyQualifiedName~D193TupleInPlanCacheTests")` exit 0, selected executed. | coder-tests | | rv1 |
| E03 | P1 | `T[core-targeted.log](dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~QueryCacheControlsTests|FullyQualifiedName~D193TupleInTests|FullyQualifiedName~TupleContextSafetyTests")` exit 0, counts. | coder-tests | | rv1 |
| E04 | P1 | two narrow mutations (sticky restored; suppression disabled) each run the sqlite D199 filter, nonzero assertion failures; candidate restored exactly. | coder-tests | | rv1 |
| E05 | P1 | independent semantic review (scanner eligibility/coverage, owner-relative exclusions, prepared refresh, reset/clone, tuple isolation). | check | always | rv1 |
| E06 | P1 | boundary sweep `T[boundary.log](dotnet test nextorm.slnx -c Debug --no-build --verbosity normal)` exit 0, all projects accounted for; with integration environment. | coder-tests | | rv1 |
| E07 | P1 | all-provider integration `T[integration.log](dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor)` exit 0; SQLite/PG/SQL Server/MySQL/ClickHouse actually execute, no required skips. | coder-integration | | rv1 |
| E08 | P1 | temp-table route + SQL Server configured/auto TVP routes execute and policy retained; check must confirm branches executed via `route-evidence.md`. | coder-integration; certifier check | | rv1 |
| E09 | P1 | `dotnet build nextorm.slnx -c Debug` streamed to `build.log`; exit 0, 0 warnings, 0 errors. | coder-build | | rv1 |
| E10 | P1 | CRLF normalize each changed text file; `git diff --check`; scope/CPM compliance. | coder; certifier check | | rv1 |
| E11 | P1 | `timeout 240 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; exit 0, 7 cases, ≤240s; ratio ≤2.244 or completed investigation. | coder-perf | | rv1 |
| E12 | P1 | artifact + fixture restoration incl. pre-existing dirty; before/after inventories + SHA256. | coder-perf; certifier check | | rv1 |
| E13 | P2 | coverage `T[coverage.log](dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal")` exit 0, readable nonempty; record line/branch vs 85/75 + non-main warning policy. | coder-coverage; certifier check | | rv1 |
| E14 | P2 | status/ledger/deferred/docs + honest collection linkage review. | coder-status; certifier check | | rv1 |
| E15 | P1 | independent certification `check(r1,n1,rv1,candidate fingerprint,complete ledger)` explicit PASS only if all applicable rows satisfied. | independent check | | rv1 |
| E16 | P2 | task-only commit `git add -- <explicit manifest>`; `git commit -m "#199 Fix scalar IN sticky shared-command cache policy"`; `git show --stat --oneline HEAD`; no push. | coder-git | | rv1 |
| E17 | P1 | deferred scalar PREWHERE execution; predicate = this task changes PREWHERE partition ownership/shape computation or adds a scalar PREWHERE fixture (currently false). | coder-integration | | rv1 |

- E17 record justified N/A now (predicate currently false).

## CHECK re-gather budget

- Owner: independent check.
- At most two targeted re-gather batches, each ≤3 named rows (6 requests total).
- Missing reports consume the budget first and are **not** a product defect / DO iteration / contract
  revision. If still incomplete, no PASS.
- Demonstrated implementation defect → new P task via planner.
- Persistent low confidence after targeted gathering → escalation trigger 5.
- Any revision must supersede rv1, preserve row IDs, add new IDs.

## Progress log

- 2026-10-06T21:46:30Z | PLAN | r1 | n1/3 | PLAN ready — awaiting confirmation | status file created
- 2026-10-06T21:46:30Z | PLAN→DO | r1 | n1/3 | plan confirmed — DO started (autonomous) | —
2026-10-06T21:55:36Z | DO | r1 | n1/3 | D1 red recorded: 2/2 fail on policy assertion (command.Cache/AnyCommand.Cache false); rc=2 | /tmp/nextorm-199-r1/red.log
2026-10-06T21:55:36Z | DO | r1 | n1/3 | D2 implemented HasUnkeyedScalarInValues + scanner + ReplaceCommand recompute + planner storeInCache suppression; removed InValuesTranslator.cs:58-59 | build-d2.log, sqlite-targeted.log, core-targeted.log
2026-10-06T21:55:36Z | DO | r1 | n1/3 | D2 verified: build 0/0; D199 2 green (was 2 red); D193+core green; validator report rc=0 | —
2026-10-06T22:01:15Z | DO | r1 | n1/3 | D3 variant tests added: SharedCount, UnkeyedCapturedScalarIn, KeyedCapturedScalarIn, InlineScalarArray, StoreInCacheFalse, ExplicitCacheFalse, ResetAndClone; core guards 2; brief validator rc=0 | scope-d3.json, sqlite-d199-d3.log, core-qcc-d3.log
2026-10-06T22:01:15Z | DO | r1 | n1/3 | D3 green: sqlite D199 9/9 rc=0; core QCC 11/11 rc=0; build 0/0 | sqlite-d199-d3-final.log, core-qcc-d3-final.log, build-final-d3.log
2026-10-06T22:01:15Z | DO | r1 | n1/3 | E04 mutation-a (sticky restored): 5 policy-assertion failures rc=2; candidate restored sha256 44b3616c... equal | mutation-sticky.log
2026-10-06T22:01:15Z | DO | r1 | n1/3 | E04 mutation-b (suppression disabled): 1 stale-plan failure rc=2 (ParamCount 2!=1); candidate restored sha256 equal; report validator rc=0 | mutation-suppression.log, evidence-d3.json
2026-10-06T22:01:15Z | DO | r1 | n1/3 | D3 partial: integration/temp-table/TVP coverage not run in this slice (D4 boundary owns it); D3 stays pending | —
- 2026-10-06T22:03:24Z | DO | r1 | n1/3 | D3 variant tests + core guards green (SQLite D199 9/9, core QCC 11/11); E04 mutations both fail as required, candidate restored | sqlite-d199-d3-final.log, core-qcc-d3-final.log, mutation-sticky.log, mutation-suppression.log
- 2026-10-06T22:03:24Z | DO | r1 | n1/3 | D4a boundary: build 0/0, solution sweep result recorded | build.log, boundary.log
- 2026-10-06T22:07:30Z | DO | r1 | n1/3 | D4b E07 all-provider integration rc=0; Total 3311, Failed 0, Skipped 197, executed 3114; all five providers connected, no availability skip | /tmp/nextorm-199-r1/integration.log
- 2026-10-06T22:07:30Z | DO | r1 | n1/3 | D4b E08 route evidence: temp-table executed (SQLite/PG/MySQL; SQL Server capability-skipped; ClickHouse suite absent), TVP TvpMetadataCacheTests 4/4 + provider TVP executed, D199 shared-command 9/9 | /tmp/nextorm-199-r1/route-evidence.md
- 2026-10-06T22:10:14Z | DO | r1 | n1/3 | D4c E11 perf: acceptance rc=0, 7/7 cases, external wall 56s (BDN 47.77s), ≤240s; ratio Cached_ToList/Prepared_ToList=1.99 (baseline 1.87, threshold 2.244, +6.6% <20%) → PASS, no re-run | /tmp/nextorm-199-r1/perf.log, /tmp/nextorm-199-r1/perf-summary.md
- 2026-10-06T22:10:14Z | DO | r1 | n1/3 | D4c E12 restore: actual artifact path is repo-root BenchmarkDotNet.Artifacts (tracked, 75 files); 15 modified reports restored via git checkout + generated ignored BenchmarkRun-20261007-030837.log removed; status==pre-snapshot, fixture sha256 equal (32bf10a0…), artifact diff empty | /tmp/nextorm-199-r1/restoration.md

## D4b — E07 integration + E08 route evidence

- E07: rc=0; Total 3311, Failed 0, Errors 0, Skipped 197, Not Run 0 (executed 3114).
- Per provider (discovered / executed / failed / skipped): SQLite 714/674/0/40; PostgreSQL 826/800/0/26; SQL Server 724/676/0/48; MySQL+MariaDB 719/639/0/80; ClickHouse 205/205/0/0; non-provider 123/120/0/3.
- No availability skip: all five providers connected (Podman 5.8.6); all 197 skips are capability/opt-in. Discovery (`-preEnumerateTheories -list full/json`) = 3311, matching the run Total; executed = discovered − skipped.
- E08 route map pointer: `/tmp/nextorm-199-r1/route-evidence.md`.
  - temp-table (`src/nextorm.core/DataContext/DataContext.cs:399-413`): executed on SQLite/PG/MySQL (`tests/nextorm.integration.tests/CommonTestSuite.CreateTableAs.cs:40,66,93,114,135,159`); SQL Server capability-skipped; ClickHouse suite absent.
  - TVP (`tests/nextorm.core.tests/TvpMetadataCacheTests.cs` 4/4 rc=0; SQL Server 16 + SQLite 4 + ClickHouse 23 + PostgreSQL 6 + MariaDB 5 integration TVP tests executed).
  - shared-command (`tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs` 9/9: `SharedAny_...`, `SharedCount_...`); provider-independent plan-cache policy.

## D4c — E11 perf acceptance + E12 restore

- **E11 (`R199-06`)**: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` → **exit 0**, **7/7** cases, **0 failures**; external wall clock **56 s** (`/usr/bin/time -v` `Elapsed 0:55.38`), BDN `Global total time 47.77 s` — under the 240 s hard gate.
- Comparable ratio `Cached_ToList / Prepared_ToList` = **1.99** (1,754.9 us / 880.6 us); baseline **1.87**, threshold **2.244**; deterioration **+6.6 %** < 20 % → **PASS**, no investigation re-run. `Cached_PlanOnly_Param` deliberately not compared.
- Per-case Mean/Allocated: table in `/tmp/nextorm-199-r1/perf-summary.md`; raw `/tmp/nextorm-199-r1/perf.log`; wall `/tmp/nextorm-199-r1/perf-wall.txt`; baseline `docs/specs/performance/acceptance-benchmarks.md:61-69,109-116`.
- **E12 (`R199-06`)**: acceptance writes to **repo-root** `BenchmarkDotNet.Artifacts/` (tracked, 75 files; the brief's `benchmarks/BenchmarkDotNet.Artifacts` is a separate stale tracked set, 0 modified). 15 modified report files restored with `git checkout -- BenchmarkDotNet.Artifacts`; generated ignored `BenchmarkRun-20261007-030837.log` removed (pre-existing ignored logs preserved).
- Post-restore: `git status --short` == pre-snapshot; `benchmarks/nextorm.benchmark/data/test.db` + working `/tmp/nextorm-bench/test.db` SHA256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (equal to pre); `git diff --stat` over both artifact dirs **empty**. **E12 PASS** (`/tmp/nextorm-199-r1/restoration.md`).
- D4 remains **running** (E13 coverage pending).
- 2026-10-06T22:12:37Z | DO | r1 | n1/3 | D4d E13 coverage: rc=0; line 88.4% (48051/54295) >= 85, branch 80.1% (25828/32229) >= 75 -> PASS under non-main warn-only policy; report exists readable non-empty (9588629 B) | /tmp/nextorm-199-r1/coverage.log, /tmp/nextorm-199-r1/coverage-summary.md
- 2026-10-06T22:12:37Z | DO | r1 | n1/3 | D4 complete (E13 PASS); restored pre-existing tests/coverage/coverage.cobertura.xml (sha256 51684ab9...) - new-run report archived | /tmp/nextorm-199-r1/coverage.cobertura.new-run.xml
- 2026-10-06T22:12:37Z | DO->CHECK | r1 | n1/3 | D4 marked done; D5 independent check running | docs/specs/status/rc1-tail-199-sticky-cache-1.md

## D4d - E13 coverage (R199-07)

- **E13 (`R199-07`)**: exact argv `env DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock timeout 3600 dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` -> **exit 0**; inner `dotnet test` total 8925, failed 0, succeeded 8727, skipped 198 (1m 04.953s); all 11 test projects passed. Adjusted minimally from bare `dotnet-coverage` to the CI local-tool form (`dotnet tool run`), matching `.github/workflows/dotnet.yml:42-45`.
- Report `tests/coverage/coverage.cobertura.xml` exists, readable, non-empty (9,588,629 B, sha256 `9aac471a...`); `reportgenerator` TextSummary `/tmp/nextorm-199-r1/coverage-summary-reportgenerator.txt`.
- **Line 88.4 %** (48,051/54,295) >= 85; **branch 80.1 %** (25,828/32,229) >= 75 -> both **above** thresholds. Branch `1.0.9-rc1` != `main`: a below-threshold result would warn only. **E13 PASS**, no coverage-driven defect.
- Summary `/tmp/nextorm-199-r1/coverage-summary.md`; raw log `/tmp/nextorm-199-r1/coverage.log`; new-run report archive `/tmp/nextorm-199-r1/coverage.cobertura.new-run.xml`.
- Pre-existing `tests/coverage/coverage.cobertura.xml` (sha256 `51684ab9...`, 2026-10-07 01:56) restored byte-for-byte per preservation clause; `artifacts/pdca/` untouched. **D4 done; D5 (independent check) running.**
2026-10-06T22:23:18Z | CHECK | r1 | n1/3 | check verdict: FAIL — missing mandatory planned tests (unkeyed @in, unkeyed captured array, duplicates/default unkeyed, 4th storeInCache×Cache combination); no implementation/security defect confirmed | check-verdict
2026-10-06T22:23:18Z | DO | r1 | n2/3 | loop-back CHECK→DO: adding missing planned tests; n=2/3 | —
2026-10-06T22:25:51Z | DO | r1 | n2/3 | loop-back tests added: UnkeyedScalarInViaSqlFunctionsIn_ShouldPreservePolicyAndBypassCache, CapturedScalarArrayInUnkeyedClause_ShouldBypassCacheAndPreservePolicy, UnkeyedScalarIn_DuplicatesAndScalarDefault_ShouldPreservePolicy, ScalarIn_StoreInCacheFalseAndCacheFalse_ShouldRemainFalse | tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs
2026-10-06T22:25:51Z | DO | r1 | n2/3 | targeted: SQLite D199 13/13 rc=0; D193 12/12 rc=0; core QCC 11/11 rc=0; build 0 warnings 0 errors | sqlite-d199-loop2.log, sqlite-d193-loop2.log, core-qcc-loop2.log, build-core-loop2.log
2026-10-06T22:25:51Z | DO | r1 | n2/3 | E04 mutation-a (sticky restored): D199 8/13 fail rc=2 (3 new unkeyed tests fail); mutation-b (suppression disabled): D199 4/13 fail rc=2 (3 new unkeyed tests fail); candidate restored sha256 44b3616c... equal | mutation-sticky-loop2.log, mutation-suppression-loop2.log
2026-10-06T22:25:51Z | DO | r1 | n2/3 | validator report rc=0 (evidence-loop2.json); D4 done stands; boundary-sweep refresh (build/sweep/integration/perf/coverage) pending follow-up coder task | evidence-loop2.json
2026-10-06T22:31:13Z | DO | r1 | n2/3 | loop-back boundary sweep refresh: build 0 warnings / 0 errors rc=0; sweep rc=0, total 8929, failed 0, succeeded 8731, skipped 198; all 11 test projects passed | /tmp/nextorm-199-r1/build-loop2.log, /tmp/nextorm-199-r1/boundary-loop2.log
2026-10-06T22:31:13Z | DO | r1 | n2/3 | augmented-suite inclusion confirmed: D199ScalarInPlanCacheTests 13/13 rc=0 (incl. ScalarIn_StoreInCacheFalseAndCacheFalse_ShouldRemainFalse); core QueryCacheControlsTests 11/11 rc=0 | /tmp/nextorm-199-r1/sqlite-d199-boundary-loop2.log, /tmp/nextorm-199-r1/core-qcc-boundary-loop2.log
2026-10-06T22:31:13Z | DO | r1 | n2/3 | git diff --check rc=0; manifest unchanged (6 src + QueryCacheControlsTests + D199 13 facts + status file); all changed files CRLF | git status --short
2026-10-06T22:31:13Z | DO | r1 | n2/3 | D4 re-verified for augmented suite -> done; D5 independent re-CHECK pending -> running | docs/specs/status/rc1-tail-199-sticky-cache-1.md
2026-10-06T22:40:01Z | CHECK | r1 | n2/3 | CHECK re-gather (2nd batch) completed — fresh current-revision coverage rc=0 (8929/0/8731/198), line 88.4% (48051/54295) >= 85, branch 80.1% (25828/32229) >= 75; stay in CHECK r1/n2/3 | /tmp/nextorm-199-r1/coverage-summary-loop2.md, /tmp/nextorm-199-r1/coverage-loop2.log
2026-10-06T22:40:01Z | CHECK | r1 | n2/3 | reconciliation recorded: E01–E17 rows bound to verified artifacts + R199-01/03/04-E08/07/08 mappings; no product/test code changed | /tmp/nextorm-199-r1/check-reconciliation.md
2026-10-06T22:43:11Z | CHECK | r1 | n2/3 | check verdict: PASS — all applicable R199-01..R199-09 satisfied; R199-10/E16 post-PASS | check-verdict
2026-10-06T22:43:11Z | ACT | r1 | n2/3 | cycle closed; flow closed; task-owned commit made | __COMMIT_SHA__

## CHECK re-gather (2nd batch) — evidence reconciliation (r1/n2/3)

- Fresh current-revision coverage (R199-07): `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` → **rc=0**; total **8929**, failed 0, succeeded 8731, skipped 198; **line 88.4 % (48,051/54,295) ≥ 85**, **branch 80.1 % (25,828/32,229) ≥ 75**; report archived `coverage.cobertura.new-run-loop2.xml` (`ded4f307…`); pre-existing `tests/coverage/coverage.cobertura.xml` (`51684ab9…`) restored byte-for-byte. Summary `/tmp/nextorm-199-r1/coverage-summary-loop2.md`, log `coverage-loop2.log`.
- Row-by-row E01–E17 reconciliation: `/tmp/nextorm-199-r1/check-reconciliation.md`. Each row bound to a verified on-disk artifact:

  | id | binding | result | artifact |
  | --- | --- | --- | --- |
  | E01 | red on base `9b2d70fc` | exit 2; D199 2/2 fail on policy assertion | `red.log` (`red-source.diff` absent — flagged) |
  | E02 | D199 + D193 sqlite | 13/13 + 12/12 = 25/25 | `sqlite-d199-loop2.log`, `sqlite-d193-loop2.log` |
  | E03 | core QCC | 11/11 | `core-qcc-loop2.log` |
  | E04 | sticky / suppression mutations | 8/13 and 4/13 fail; candidate `44b3616c…` restored | `mutation-sticky-loop2.log`, `mutation-suppression-loop2.log` |
  | E05 | semantic review | verdict 1 fail→gap (status log 22:23:18Z); verdict 2 fail→evidence = this batch | status log; this section |
  | E06 | boundary sweep | 8925/0/198 (D4a); 8929/0/8731/198, 11/11 (loop2) | `boundary.log`, `boundary-loop2.log` |
  | E07 | all-provider integration | rc 0; 3311/0/3114 executed/197 skipped; per-provider as recorded | `integration.log` |
  | E08 | routes | temp-table SQLite/PG/MySQL; SS CTAS-temp capability-skip; CH N/A; TVP 4/4 + providers; shared policy D199 | `route-evidence.md` |
  | E09 | solution build | rc 0; 0 warnings / 0 errors | `build.log`, `build-loop2.log` |
  | E10 | constraints | `git diff --check` rc 0; CRLF; manifest 6 src + 2 tests + status; no CPM/config | working tree |
  | E11 | perf acceptance | 7/7, 56 s, ratio 1.99 vs 1.87 (thr 2.244) | `perf-summary.md` |
  | E12 | restoration | artifacts+fixture restored, status matched | `restoration.md` |
  | E13 | coverage | rc 0; line 88.4 %, branch 80.1 % | `coverage-summary-loop2.md` |
  | E14 | status/docs | status updated; no public-doc change (intentional); XML-doc on internals | this file |
  | E15 | independent certification | **satisfied — final PASS** (r1/n=2/3/rv1): all applicable R199-01..R199-09 satisfied | check-verdict |
  | E16 | commit | **done** (D6): task-owned commit `__COMMIT_SHA__`, message begins `#199`; no push | `git show --stat --oneline HEAD` |
  | E17 | PREWHERE | deferred F1; guard `QueryCommand.QueryPreparer.cs:2148,2294`; F2 trigger = measured scanner overhead | — |

- **R199-01**: red proof `red.log` (exit 2); cached-plan-reuse assertions `tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs:141-142` (shared Any, after the ordinary `x.Id > 0` replacement at `:156`), `:184-185` (shared Count), `:453-454` (ordinary replacement) — `ReferenceEquals(first, second).Should().BeTrue(...)`.
- **R199-03**: empty `tests/nextorm.integration.tests/CommonTestSuite.In.cs:20`; value-vs-reference `:42`/`:55`; nullable/null `:68`/`:82`/`:95`; tuple null rejection `tests/nextorm.core.tests/D193TupleInTests.cs:376`, `tests/nextorm.sqlite.tests/D193TupleInSqlGenerationTests.cs:368`, `tests/nextorm.sqlserver.tests/D193TupleInSqlGenerationTests.cs:53`; scalar null folds to empty, no exception `src/nextorm.core/Query/InValues.cs:354-363` (`nonNull = []` `:360`, returned `:363`).
- **R199-04/E08**: provider-independent planner gate `src/nextorm.core/DataContext/QueryPlanner.cs:575`; capability-based absence (SS CTAS-temp, CH temp-table suite) as recorded.
- **R199-07**: fresh coverage numbers, bound to the current revision (8929/8731/198 test set == `boundary-loop2.log`).
- **R199-08**: the reconciliation table itself (this section + `/tmp/nextorm-199-r1/check-reconciliation.md`).
- No product/test code changed in this batch; the independent re-CHECK returned **PASS** at r1/n2/3; E16 completed in D6.

## CHECK verdict (final, r1/n=2/3/rv1)

- **CHECK PASS** (independent verifier): all applicable R199-01..R199-09 satisfied; R199-10 satisfied by the
  D6 commit (E16 post-PASS).
- Evidence bound to the final tree: build **0 warnings / 0 errors**; `D199ScalarInPlanCacheTests` **13/13**;
  core `QueryCacheControlsTests` **11/11**; D193 tuple suite green; E04 mutations sticky **8/13** and
  suppression **4/13** fail then candidate restored (`44b3616c…`); boundary sweep
  **8929 / 0 / 8731 / 198**; all-provider integration **3311 / 0** (executed 3114, skipped 197, no
  availability skip); coverage line **88.4 %** (48051/54295) / branch **80.1 %** (25828/32229) ≥ 85 / 75;
  perf acceptance **7/7** cases, ratio `Cached_ToList / Prepared_ToList` **1.99** ≤ 2.244.
- E15 satisfied. E16 done. Reconciliation artifact `/tmp/nextorm-199-r1/check-reconciliation.md`.

## ACT / closure (r1/n=2/3, rv1)

- Cycle closed (`N=1`, `r=1`, `n=2/3`, contract `rv1`, no supersession). **Flow closed.**
- Task-owned commit records this file, the source fix, the tests and the collection corrective reference;
  no push / branch / worktree / merge.
- Transferable-lesson-free note: no transferable lesson recorded — the fix is the task-local source change
  (remove the sticky `QueryCommand.Cache` write; classify scalar-unkeyed collections at preparation time
  and suppress caching call-locally via `storeInCache`).
