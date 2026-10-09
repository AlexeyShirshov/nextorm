# rc2-175-joininto-quality-reclaim-1 — JoinInto test-quality debt: hash-inequality checks / EagerLoadSpec.Assign visibility

- collection: rc2-reclaim (single lane; current worktree/branch 1.0.9-rc2)
- selected_variant: pdca-dotnet; cycle N=1; plan_revision r=1; evidence contract rv=2
- baseline: cf34f910; issue #175 (milestone 1.0.9-rc2 #20)
- supersedes: prior terminal attempt docs/specs/status/rc2-175-joininto-test-quality-1.md (r=1, evidence-completeness/provenance STOP; preserved unchanged); reference-only patch docs/specs/status/rc2-175-evidence/D175-STOP-incomplete.patch
- plan_state: ready (persisted at PLAN->DO boundary; DO not started in P-phase)

## Goal
Restore the minimal test-only R175 changes and re-prove them under a full row-bound evidence contract (rv=2). Make the eight plan-difference checks independent of incidental hash-code differences (prove inequality via the correct comparer's `Equals == false`; drop unjustified hash `NotBe`). Keep execution/identity/cache-miss/hash-coherence checks. Confirm `EagerLoadSpec.Assign` is `internal` with no external consumer. Test-only reclaim; no runtime/API/public-doc change.

## Acceptance criteria (each with negative)
- R175-HASH: all H01-H08 prove inequality via `Equals == false` of the correct comparer; unjustified hash `NotBe` removed (neg: dependence on differing hashes / wrong comparer / an unproven site).
- R175-SEM: X01-X05 preserve execution count, reuse identity and miss; only necessary assertions change (neg: weakened execution/identity/miss or runtime diff).
- R175-COH: C01-C05 keep `Equals` and hash-equality for equal plans (neg: coherence check removed/weakened).
- R175-ASSIGN: `Assign` stays `internal`; all six references are inside `nextorm.core` (neg: visibility changed / external consumer / references unconfirmed by Roslyn).
- R175-TEST: all mandatory focused/full runs exit 0 and each selector actually executed tests (neg: zero selection, failure, unexplained skip, missing result).
- R175-SCOPE: source changes limited to the five core test files; CRLF preserved; production code/public docs/coverage policy unchanged (neg: runtime/API diff, stray file, mixed/LF endings, threshold change).
- R175-EVIDENCE: every applicable rv=2 row has actual evidence + provenance + an existing artifact (neg: historical report, promise, aggregate without row linkage, missing artifact, open obligation).
- AC1 build 0W/0E; AC2 unit 11 projects 6246+1skip=6247/0fail; AC3 integration 3555/0err/0fail/197skip + availability 0; AC4 coverage >=85/75 (supplied 86.8/79.4); AC5 docfx 2w/0e.

## DO task list (sequential; deps R1->R2->R3->R4)
- D:175.R1 initial state + foundation evidence: confirm branch/lane/baseline cf34f910/footprint; map H01-H08 to prior logical definitions incl. line-number drift; confirm intent comparers + `Assign` refs via Roslyn-only scout; focused core/sqlite baseline; create rv=2 contract + row-bound ledger (entries `planned/not-run`).
- D:175.R2 minimal assertions (5 core test files): ensure H01-H08 semantic inequality via correct comparer; remove redundant hash `NotBe`; do not weaken C01-C05/X01-X05; keep CRLF.
- D:175.R3 post-change confirmation: assertion/intent/scope audits; focused post; full core/sqlite; build; conditional EC175-08 (integration iff integration diff, else guard); unconditional collection unit/integration/coverage/DocFX.
- D:175.R4 reconciliation evidence: fill each rv=2 row; fill collection §2/§3/§4/§4b/§5; hand artifacts+row state to CHECK (DO does not issue final PASS).
Decision: repeat the small edits on the current baseline; the saved patch is reference-only (bases differ: plan 18659e41 / restored tip 04836505 vs current cf34f910).

## H/C/X matrix (planned disposition, not already closed)
H01 SelectExpressionPlanEqualityComparerTests.cs:34 (test, FC1); H02 :60 (test, FC1); H03 :72 (test, FC1); H04 ImplicitNavigationR3CountBoundaryTests.cs:102 (test, FC3); H05 RawSourceBindingFilterTests.cs:927 (test, FC2); H06 :948 (test, FC2); H07 PlanKeyStructureTests.cs:238 (test, FC4); H08 InMemoryTests.cs:331 (test, FC5). C01-C04 JoinIntoPlanKeyTests.cs:63,91,106,135 (test, FC6, keep equals+hash-coherence); C05 JoinIntoSqlGenerationTests.cs:136 (test, FS2, keep coherence). X01 JoinIntoExecutionTests.cs:55 (test, FS1, execution count); X02 :76 (test, FS1); X03 CommonTestSuite.JoinInto.cs:221-222 (guard if unchanged, else test all providers); X04 JoinIntoSqlGenerationTests.cs:159-162 (test, FS2, reuse identity); X05 same-site miss (test, FS2, identity/miss). Historical :35,61,88 are the same logical sites as :34,60,72 — R1 must prove correspondence; genuine divergence → PLAN revision with retained IDs.
Additional axes: null/default = guard; value/reference = guard (types/comparer preserved; inequality by equality not hash uniqueness); providers = test (SQLite focused/full + all container providers in AC3); new synthetic collision scenarios = deferred (trigger: comparer/hash change or explicit collision-robustness requirement).

## Test strategy
Commands: B `dotnet build --no-restore`; FC1 `dotnet test tests/nextorm.core.tests -c Debug --no-build --verbosity normal --filter "FullyQualifiedName~NextORM.Core.Tests.SelectExpressionPlanEqualityComparerTests"`; FC2 `...~NextORM.Core.Tests.RawSourceBindingFilterTests`; FC3 `...~...ImplicitNavigationR3CountBoundaryTests`; FC4 `...~...PlanKeyStructureTests`; FC5 `...~...InMemoryTests`; FC6 `...~...JoinIntoPlanKeyTests`; FS1 `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --verbosity normal --filter "FullyQualifiedName~NextORM.Sqlite.Tests.JoinIntoExecutionTests"`; FS2 `...~NextORM.Sqlite.Tests.JoinIntoSqlGenerationTests`; UCORE/USQLITE full; U `dotnet test --no-build --verbosity normal`; I integration with DOCKER_HOST socket; AVAIL `grep -c "is not available" ...`; COV1/COV2 coverage; DOC docfx. Baseline and post for focused runs archived separately. FC1 covers H01-H03, FC3 H04, FC2 H05-H06, FC4 H07, FC5 H08. Full core/sqlite post. EC175-08 conditional on integration diff; AC3 unconditional. AC4 fresh aggregate with current coverage.settings.xml (85/75 unchanged). Red->green not required (assertion-quality fix).

## Docs plan
Only the new status + new evidence; no public docs/`docs/ru`. `todo_navigation_properties.md:357-358` unchanged. AC5 still run.

## Performance decision
Not needed: changes are assertions on the H01-H08 sites (e.g. SelectExpressionPlanEqualityComparerTests.cs:34,60,72); production `JoinInto` (EntityBuilder.cs:621:35) and `Assign` (EntityBuilderEagerLoading.cs:307:19) unchanged; not a per-row execution path.

## Reconnaissance decision
No new spike; targeted scout already done. R1/R3 include mandatory current-state/site-mapping confirmation, not architecture exploration.

## Unit execution mode
Sequential, one tree (five overlapping test files + shared baseline + one ledger; worktrees add no isolation benefit).

## Design-checklist verdict
No architectural/API redesign needed; hash collisions are legal so inequality is proven by the equality comparer; `Assign` visibility already satisfies the requirement; cache/runtime invariants untouched.

## Evidence contract (rv=2; unconditional unless split; owner coder unless scout/check)
Artifact root E = docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/. Per-row slots: requirement ID, row ID, rv=2, scenario, evidence kinds/sources, exact invocation, exit/result/log, artifact, owner, applicability predicate. Shared logs allowed but each row cites its fragment + actual method/site. `planned/not-run/failed/missing/blocked` recorded explicitly; historical artifacts marked historical and do not close rv=2.
- EC175-01 intent audit -> A1 -> intent-audit.md.
- EC175-02 post assertion audit -> A2 -> assertion-audit.md.
- EC175-03 Assign refs -> A3 -> assign-refs.md + assign-roslyn-output.md.
- EC175-04 core focused baseline+post -> FC for both -> focused/core-baseline/, focused/core-post/.
- EC175-05 sqlite focused baseline+post -> FS for both -> focused/sqlite-baseline/, focused/sqlite-post/.
- EC175-06 full core post -> UCORE -> full/core.log.
- EC175-07 full sqlite post -> USQLITE -> full/sqlite.log.
- EC175-08 integration iff integration diff else guard -> I or A4 -> collection/integration.log or integration-guard.md.
- EC175-09 build baseline+post -> B -> build/baseline.log, build/post.log.
- EC175-10 scope/branch/base/CRLF/docs/coverage guards -> A4 -> scope-guard.md, integration-guard.md.
- H01..H08, C01..C05, X01..X05 -> per-row variants/<ID>.md + relevant FC/FS logs.
- AC175-01..05 -> B/U/I+AVAIL/COV1+COV2/DOC -> collection/{build,unit,integration,availability,coverage,docfx}.
Audit invocations A1-A4 are exact scout Task briefs (intent; assertion; Assign via Roslyn; scope/CRLF/no-runtime-diff/coverage-settings).
CHECK re-gather budget: max 2 batched rounds, owner check; no source edits during re-gather. Prior provenance gap fixed by per-(row ID, rv=2) ledger entries with actual invocation/exit/run ID/baseline/tree-state/artifact/fragment; shared logs cite row-specific fragments; aggregate summary cannot replace H/C/X audits; collection §2/§3/§4/§4b/§5 filled.

## Risks / assumptions
- Old patch applicability not needed (redo chosen); reference-only.
- H01-H03 line drift is the same logical sites; R1 must prove correspondence, else PLAN.
- Current post-state counts are planned, not verified; observed drift -> targeted reconciliation, not AC relaxation.
- Container recovery per project skill; persistent infra failure -> recommend escalate.
- Missing evidence -> CHECK re-gather first; persistent low confidence after targeted scout -> escalate trigger 5.
- Classification of prior STOP: evidence-completeness/provenance failure, not product defect.

## Handoff metadata
- Write footprint: five files in tests/nextorm.core.tests/: ImplicitNavigationR3CountBoundaryTests.cs, InMemoryTests.cs, PlanKeyStructureTests.cs, RawSourceBindingFilterTests.cs, SelectExpressionPlanEqualityComparerTests.cs; new status/evidence; generated coverage/DocFX/build outputs. SQLite/integration sources read-only by plan.
- Preconditions: .NET 10 + restored deps for --no-restore; successful current build before --no-build; integration skill + providers available; baseline cf34f910, lane 1.0.9-rc2.
- Predecessor: terminal rc2-175-joininto-test-quality-1.md:393-406; old dir rc2-175-evidence/ + patch preserved.
- Refs: issue https://github.com/AlexeyShirshov/nextorm/issues/175; milestone 1.0.9-rc2 #20.

## Prior attempt (superseded, preserved)
rc2-175-joininto-test-quality-1.md: r=1, terminal STOP, defect family "rv1 evidence-completeness / row-bound ledger provenance", 4x CHECK fail; no product defect; test files restored to green tip 04836505; #175 OPEN.

## Progress log
- PLAN persisted (r=1, rv=2) — collection P-phase boundary; plan_state=ready; DO not started.
- 2026-10-09T13:17Z | DO | r=1 | n=1/3 | DO resumed on current tree 3ebaa4ee+worktree; R2 edits validated: H01-H08 inequality via Equals==false and hash NotBe removed, C01-C05/X01-X05 unchanged, Assign internal with 6 refs all in nextorm.core, diff = five core test files only (CRLF) | assertion-audit.md, assign-refs.md, scope-guard.md
- 2026-10-09T13:17Z | DO | r=1 | n=1/3 | fresh boundary re-run on current tree: build 0W/0E exit 0; focused FC1-FC6/FS1-FS2 exit 0 (12/60/7/19/93/8/7/9); full core 1969/0/0, full sqlite 1310+1skip/0fail; integration 3555/0/0/197 availability 0; coverage 88.3/80.3; docfx 2w/0e | rc2-175-joininto-quality-reclaim-1-evidence/{build,focused,full,collection}/
- 2026-10-09T13:17Z | DO | r=1 | n=1/3 | rv=2 ledger EC175-01..10 / H01-H08 / C01-C05 / X01-X05 / AC175-01..05 filled run-pass/guard; AC175-02 unit 6263 (6262+1skip) reconciled as 6247+16 (R184 core 1953->1969) | evidence-contract.md
- 2026-10-09T13:17Z | DO | r=1 | n=1/3 | inner-loop validator: brief exit 0, report exit 0 | audits/validate-brief.out, audits/validate-report.out, evidence.json
- 2026-10-09T13:17Z | DO | r=1 | n=1/3 | R4 collection reconciliation: no R175 rows in frozen collection-1.0.9-rc2-C-evidence.md; reclaim §2/§3/§4/§4b/§5 owned by collection CHECK (R184 precedent). DO does not mark R175 done; handoff to CHECK | scope-guard.md
- 2026-10-09T18:25Z | DO | r=1 | n=2/3 | CHECK n=1 FAIL (evidence gate: scope-guard provenance, assertion-audit census, CRLF scope; no product defect) -> DO loop-back n=2; scope-guard coverage reconciled 86.8/79.4 -> 88.3/80.3 with coverage-collect/reportgenerator commands + exit codes + logs; byte-level CRLF verdict added (InMemoryTests.cs EOF-no-newline is pre-existing at base 3ebaa4ee, unchanged by R175); assertion-audit census corrected to all 7 live residual hash-inequality sites incl. RawSourceBindingFilterTests.cs:974 (out of H01-H08, assertion NOT removed); row-bound ledger completed in evidence.json (rows[])/evidence-contract.md/variants C01-C05,X01-X05 with test names, counts, predicates + version binding; validator report exit 0 | evidence.json, evidence-contract.md, scope-guard.md, assertion-audit.md, variants/, collection/coverage-report.log, audits/validate-report.out
- 2026-10-09T13:36Z | CHECK | r=1 | n=2/3 | CHECK r2 re-gather: literal process exit codes captured for build/core/sqlite/unit/integration/coverage-collect/reportgenerator/docfx; no source/test change | evidence.json (12 executions cite exit_code_source), audits/validate-report.out exit 0, build/post.log, full/{core,sqlite}.log, collection/{unit,integration,coverage-collect,coverage-report,docfx}.log, focused/{core-post,sqlite-post}/
- 2026-10-09T13:37Z | CHECK | r=1 | n=2/3 | CHECK PASS (r=1, rv=2, n=2): all gates green; build/post.log:101 0W/0E; full/core.log:21 1969/0/0; full/sqlite.log:27 1311/0; collection/unit.log:221 6263 (6262+1skip)/0; collection/integration.log:899 3555/0/0/197skip +0 unavailable; coverage 88.3/80.3; docfx 2w/0e; validate-report exit 0 | docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/
- 2026-10-09T13:37Z | ACT | r=1 | n=2/3 | ACT: committed; issue #175 closed; plan_revision r=1 unchanged | docs/specs/status/collection-rc2-reclaim.md

## Priority matrix (completeness-repair addition, 2026-10-09; collection CHECK gap G3)

Derived at the collection CHECK from the recorded H/C/X dispositions (`:29-31`) and acceptance
criteria (`:12-21`); the source carried no explicit priority heading. No existing disposition is
changed.

- **P0 — unconditional correctness/gates:** H01–H08 semantic inequality via the correct comparer
  (`:30`); C01–C05 keep `Equals` + hash-equality for equal plans (FS2); X01–X05 execution count /
  reuse identity / miss (FS1); X03 integration guard; the AC1–AC5 rows (`:21`).
- **P1 — required axes:** null/default guard; value/reference guard; providers test (SQLite
  focused/full + all container providers in AC3) (`:31`).
- **P2 — deferred, trigger recorded:** new synthetic collision scenarios (trigger: comparer/hash
  change or explicit collision-robustness requirement) (`:31`).
