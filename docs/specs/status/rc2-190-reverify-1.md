# rc2-190-reverify-1 — Fix whole-entity selection from JOIN projections (re-verification)

- collection: rc2-reclaim (single lane; current worktree/branch 1.0.9-rc2)
- selected_variant: pdca-dotnet; cycle N=1; plan_revision r=2; evidence contract rv=2; iteration n=1
- baseline: cf34f910; issue #190 (CLOSED 2026-10-08; stays closed; milestone 1.0.9-rc2 #20); follow-up #207
- boundary B (reconciliation reference) = cf34f91045781faccdeb3b7163be2cc5f1dce0f2 (full hash of cf34f910)
- final tip F (acceptance target) = ee42f183ffa1d769da76a0ccaa024ec53a7885c9 (full hash of current HEAD; predecessor accepted HEAD was 1ff47762)
- pre-existing worktree delta: AGENTS.md has one pre-existing uncommitted content edit authored outside this cycle; it is OUT OF FOOTPRINT and is neither staged, reverted nor normalized.
- predecessor: docs/specs/status/rc2-190-join-whole-entity-1.md (accepted at r1/rv1, HEAD 1ff47762; preserved unchanged; authoritative for its accept-PASS)
- verdict: NO-OP RE-CERTIFICATION plan; no product change authorized; re-run existing evidence on the CURRENT tip
- plan_state: ACT complete (CHECK PASS r=2/rv=2/n=1; no-op re-verification committed; #190 stays closed; #207 carried)

## Goal
Re-certify R190 on the current tip (intervening core commits have landed since the accepted snapshot) and close a fresh rv=2 evidence ledger. The residuals recorded on D190 are accepted evidence limitations, not demonstrated defects. No speculative development; #190 is not reopened.

## Acceptance criteria (each with negative)
1. AC1 build: `dotnet build --no-restore` exit 0, 0W/0E (neg: any warning/error/nonzero).
2. AC2 unit: At the locked final-tip baseline F, execute the complete original 11-project unit scope with the contract's commands/counting. Require 0 failed and exactly the 1 approved skip (identity+reason preserved). Reconcile against boundary B: 6247 + R184(+16) + R175(0) + R177(+3) = 6266 (6265 passed + 1 skipped). Per-project results + predecessor->test mapping; no unexplained disappearance/duplicate/extra skip/omitted project. Evidence identifies F, commands, exit codes, artifacts. A different aggregate requires reconciliation+disposition, not restoring 6247. (neg: missing projects/zero-selected selectors/unexpected skips/omitted project/count mismatch).
3. AC3 integration: At F, execute the complete original integration scope across all six required providers incl. ClickHouse. Require Errors=0, Failed=0, Unavailable=0, exactly the 197 approved capability skips (classifications preserved). Reconcile against B: 3555 + R184(0) + R175(0) + R177(+6) = 3561. Provider-level results + mapping of R177's six SQL Server integration cases. Missing/unavailable providers or infrastructure skips do not satisfy. (neg: unavailable-provider skip passed off as success).
4. AC4 coverage: line>=85, branch>=75; record vs predecessor 86.8/79.4; disclose branch evidence + two unverified mutants (neg: below threshold or unverified mutants presented as killed).
5. AC5 docfx: exit 0, 2 warnings/0 errors; existing EN/RU + registers consistent (neg: extra warnings/errors/broken refs/premature closure).
6. R190 semantics: existing checks establish root/typed projection + first/middle/last slots; missing-vs-present outer (null vs non-null); SQL/in-memory parity; regression guards; compatible cache reuse (neg: scalar empty selections/refused-expansion silent emptiness/ambiguous mapping/incompatible reuse).
7. Performance: exactly 7 acceptance cases, 0 fail, <=240s; record Mean/Allocated/cached-prepared ratio per predecessor definition (neg: timeout/missing/failed/missing metrics).
8. Scope: no tracked product/test/public-doc changes; no commits; no replacement of predecessor status; tested tip unchanged during execution (neg: unexplained footprint changes).
9. Completeness: every applicable rv=2 row has actual current evidence (neg: missing evidence prevents PASS but is not itself a product defect).
Count mismatches require measured evidence + CHECK->PLAN; no pre-authorized relaxation.

### Amendment (r=2/rv=2)
The EV190-* row IDs are preserved unchanged. The evidence revision is raised `rv=1`->`rv=2` solely because the acceptance totals are restated as baseline-relative (B = cf34f910 -> F = current tip) after predecessor R184/R177 moved the historical boundary; no row is dropped, renamed or newly invented. Historical boundary measurements (unit 9235 / 198 skipped; coverage 88.1% line / 80.1% branch; cached/prepared ratio 2.171) are **comparisons, not acceptance targets**. Coverage gates stay line>=85 / branch>=75. Perf keeps its accepted scenarios and reports the final-tip measurement against the 2.171 (predecessor) / 1.87 (documented baseline) historical comparison.

## DO task list (all fix now; sequential; NO product edits)
- D190-V1 persist contract; record HEAD/worktree baseline; load integration skill + tool/container prerequisites; preserve predecessor status.
- D190-V2 audit provenance, historical RED, scope, guards (predecessor status, artifacts/pdca/rc2-190/test-scope.json:16-28, current Roslyn locations).
- D190-V3 build; rerun the 13 selectors, full unit collection, coverage/report generation.
- D190-V4 full container-enabled integration; verify direct/regression results, six-provider participation, counts, infra log.
- D190-V5 bounded acceptance benchmark + DocFX; compare/report baseline metrics + doc/register consistency.
- D190-V6 complete actual-evidence ledger, branch/mutation disclosure, residual register, final footprint audit; hand off to CHECK.
- P190-FINAL-TIP lock F = full hash of current HEAD; run scope/commands/artifacts against F; reconcile aggregates against B = cf34f910; confirm the pre-existing AGENTS.md delta is out of footprint.
If DO finds a potential defect -> report observation; CHECK verifies before any corrective PLAN.

## Variant matrix (closure mode)
R190-01 direct root/typed + first/middle/last slots = test (ROOT,SQL,INTEGRATION-DIRECT); R190-02 missing outer->null / present-default->non-null / parity = test+guard (NULL,INTEGRATION-DIRECT; residual (a) disclosed); R190-03 scalar/value/reference/composite/nested/bare + mapping/ctor + CTE/arities = test+guard (REGRESSION,SQL,INTEGRATION-REGRESSION); R190-04 same-shape reuse vs incompatible identity + shared-command + temp prep = test+guard (CACHE,SHARED,TEMP; residual (b) disclosed); R190-05 six providers + real containers = test (SQL,INTEGRATION-DIRECT,INTEGRATION-REGRESSION,INFRA); R190-06 build/coverage/branch/mutation disclosure = test+guard (BUILD,COVERAGE,BRANCH,MUTATION); R190-07 seven benchmark cases = test (PERF); R190-08 EN/RU docs+registers = guard+DocFX (DOCS,REGISTERS); R190-09 scope/safety/provenance/boundary/completeness = guard (PREFLIGHT,SCOPE,RED,AUDIT). All 21 EV190 rows are P1 acceptance obligations; CHECK may not lower/waive.

## Test strategy
Commands: B `dotnet build --no-restore`; U `dotnet test --no-build --verbosity normal`; T(S) `dotnet test --no-build --verbosity normal --filter "FullyQualifiedName~S"` for each of the 13 recorded selectors: EntityItemProjectionInMemoryTests, RowMaterializerBuilderTests, SelectExpressionPlanEqualityComparerTests, JoinReturningIdentityTests, PlanKeyUniquenessTests, EntityItemProjectionTests, JoinWholeEntitySqlGenerationTests, JoinWholeEntity_, MariaDbJoinWholeEntityIntegrationTests, ClickHouseJoinWholeEntityIntegrationTests, Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData, Cte_Typed_SelfJoin_ShouldReturnBothSides, JoinArities_ToSqlAndUpdateJoin_ShouldRender; C coverage collect; G reportgenerator; I integration with DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock -noColor; P `timeout 240 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; F `dotnet docfx docs/docfx.json`; Z `git rev-parse HEAD`; W `git status --short`. Container-dependent selectors run with DOCKER_HOST exported.

## Docs plan
No public-doc edits. Only the new status + generated verification artifacts; validate existing EN/RU material + registers.

## Performance decision
Required (EV190-PERF in scope). Use the acceptance benchmark + accepted predecessor baseline; record the actual baseline metrics (do not invent). No one-time-work exemption over this explicit obligation.

## Reconnaissance decision
No product spike. Existing implementation/checks determine the solution; read-only provenance/evidence audits are verification, not design exploration.

## Unit execution mode
Sequential, one tree. Builds/coverage/provider infra/final logs share a footprint; no worktrees/parallel lane.

## Design-checklist verdict
PASS for a verification-only boundary: no API change, new abstraction, mapping redesign, cache mutation, test weakening, or provider workaround; existing shared-command/cache invariants remain mandatory.

## Evidence contract (rv=2)
Artifact root planned E = artifacts/pdca/rc2-190-reverify-1/. Every row unconditionally applicable (no N/A); owner coder for ledger; scout for the indicated read-only audit `A(subject)`. For each shell run preserve command/tested HEAD/stdout/stderr/exit/result in the named log. Planned locations are not yet claims. Preserve EV190 IDs + historical RED subrecord.
- EV190-PREFLIGHT -> Z,W,A(preflight+integration prereqs) -> HEAD 2ce46f5f, prerequisites ready.
- EV190-SCOPE -> W,A(scope/predecessor) -> only allowed footprint, no unexplained changes.
- EV190-RED -> A(historical EV190-RED-CORE + recovery),T(EntityItemProjectionInMemoryTests),T(RowMaterializerBuilderTests) -> cite historical 5 selected/3 failed/exit 2 without recreating; current selections>0/0 failed/exit 0.
- EV190-ROOT -> T(EntityItemProjectionInMemoryTests),T(EntityItemProjectionTests),T(JoinArities_ToSqlAndUpdateJoin_ShouldRender).
- EV190-NULL -> T(RowMaterializerBuilderTests),T(EntityItemProjectionInMemoryTests),I,A(guards+residual a).
- EV190-REGRESSION -> T(JoinReturningIdentityTests),T(Cte_Typed_*x2),U,A(mapping).
- EV190-CACHE -> T(SelectExpressionPlanEqualityComparerTests),T(PlanKeyUniquenessTests),A(residual b).
- EV190-SHARED -> U,A(inherited SHARED checks) -> no per-call sticky cache mutation.
- EV190-TEMP -> U,A(inherited TEMP checks) -> no shared-state leakage.
- EV190-SQL -> T(JoinWholeEntitySqlGenerationTests),T(JoinArities_ToSqlAndUpdateJoin_ShouldRender),U,A(six-provider SQL).
- EV190-INTEGRATION-DIRECT -> T(JoinWholeEntity_),T(MariaDb...),T(ClickHouse...),I.
- EV190-INTEGRATION-REGRESSION -> I -> 3555/0err/0fail/197skip.
- EV190-INFRA -> A(infra/six-provider), `grep -c "is not available" E/integration.log` -> 0 (grep exit 1 on zero).
- EV190-BUILD -> B -> 0W/0E.
- EV190-COVERAGE -> U,C,G -> >=85/75.
- EV190-BRANCH -> G,A(current branches + inherited findings).
- EV190-MUTATION -> A(no-tool authorization + two unverified mutants: RowMaterializerBuilder.cs:182, QueryCommand.QueryPreparer.cs:1035) -> explicit not-run/unverified; no fabricated kill score.
- EV190-PERF -> P,A(baseline/ratio) -> 7 cases/0 fail/<=240s + metrics.
- EV190-AUDIT -> A(guard validity/test safety/all rows),Z,W.
- EV190-DOCS -> F,A(EN/RU consistency) -> 2w/0e.
- EV190-REGISTERS -> A(predecessor outcome/registers/#190 closed/#207 scope+milestone uncertainty) -> new ledger complete; prior status preserved.
- R190-01..09 and AC1-AC5 all mapped to the rows above.
A(subject) = scout read-only evidence audit Task using predecessor status, artifacts/pdca/rc2-190/test-scope.json, current artifacts; Roslyn for symbols; facts only.
CHECK re-gather budget: owner check, max 2 targeted rounds. Missing evidence -> re-gather not DO iteration; verified defect -> FAIL + CHECK->PLAN; exhausted budget -> completeness blocked, route open rows via escalation; new required variant -> explicit supersession.

## Residual register
(a) no direct SQL assertion for a present row with all non-PK mapped columns simultaneously NULL/default (prior RowMaterializerBuilder.cs:287-301; null-semantics.md §5) = accepted evidence limitation; revisit on guard invalidation/reproduced provider null error/explicit direct-SQL requirement.
(b) mapper-instance identity only indirectly asserted (cache-isolation.md §5) = accepted; revisit on incompatible reuse reproduction/mapper change/direct identity requirement.
Mutant RowMaterializerBuilder.cs:182 and QueryCommand.QueryPreparer.cs:1035 = accepted, unverified (authorized disclosure); revisit on usable mutation tooling/behavior change/explicit requirement.
Uniform projection-build cancellation handling = deferred to #207 (milestone NOT recorded; do not infer #20). Prior line numbers are provenance anchors, not current locations.

## Risks / assumptions
- Current totals after intervening changes: supplied totals remain binding; measured mismatch -> CHECK->PLAN, not auto-adjust.
- Performance baseline numeric values extracted from predecessor evidence; none invented.
- Mutation tooling remains unusable; carry forward the authorized no-tool disclosure (assumption); changed tooling -> reassessment.
- Commit-subject vs appended accept-PASS discrepancy: status record authoritative; preserve as provenance note.
- Inheritance mode: normal requires `go`; autonomous proceeds after gate 1.

## Handoff metadata
- Expected write footprint: new status file + verification artifacts, coverage/benchmark/DocFX outputs, ordinary ignored build outputs. NO tracked product/test/public-doc/predecessor-status edits.
- Predecessors: all R190-01..09 + 21 EV rows retained; accepted no-tool disclosure retained honestly.
- Refs: issue #190, follow-up #207, milestone 1.0.9-rc2 #20.

## Prior attempt (preserved, authoritative for its accept-PASS)
rc2-190-join-whole-entity-1.md: r1/rv1; product fix; CHECK n1 product defect fixed, n3 BLOCKED evidence-completion, escalation accept-PASS (HEAD 1ff47762). Issue #190 closed.

## Progress log
- PLAN persisted (r=1, rv=1) — collection P-phase boundary; plan_state=ready; DO not started.
- 2026-10-09 14:50 UTC | PLAN | r=2 | n=1/3 | PLAN r=1->r=2 (stale absolute totals from predecessors R184/R177); contract recorded; no-op scope retained | docs/specs/status/rc2-190-reverify-1.md
- 2026-10-09 14:54 UTC | DO | r=2 | n=1/3 | P190-FINAL-TIP locked F=ee42f183; build green 0W/0E exit 0; 13 selectors / 18 runs all exit 0 and >=1 selected; initial dirty state + AGENTS.md delta recorded out of footprint; ledger rows PREFLIGHT/SCOPE/RED/ROOT started | artifacts/pdca/rc2-190-reverify-1/
- 2026-10-09 15:02 UTC | DO | r=2 | n=1/3 | R190 final-tip sweeps on F=ee42f183: build 0W/0E exit 0; unit aggregate 6266 = 6265 passed + 1 approved skip / 0 failed, all 11 rc=0 (reconcile B 6247 +16 R184 +0 R175 +3 R177 = 6266); full integration Total 3561 / Errors 0 / Failed 0 / Skipped 197 / Not Run 0 exit 0, six providers incl. ClickHouse, grep -c "is not available"=0 (grep rc 1) (reconcile B 3555 +6 R177); coverage line 86.8% / branch 79.4% (gates 85/75; historical 88.1/80.1); benchmark 7/7 cases 0 failed 50.27s (<240s) cached/prepared ratio 2.01 (historical 2.171 / 1.87); DocFX exit 0 2 warnings/0 errors | artifacts/pdca/rc2-190-reverify-1/logs/
- 2026-10-09 15:02 UTC | DO | r=2 | n=1/3 | EV190 rv=2 ledger bound: 21 EV190-* rows + R190-01..09 + AC1..AC5 (historical EV190-* IDs + EV190-RED subrecord preserved; mutation kept as authorized no-tool disclosure of unverified mutants RowMaterializerBuilder.cs:182 / QueryCommand.QueryPreparer.cs:1035, no kill score); `validate_inner_loop.py report` exit 0 (audits/validate-report.out); scope guard PASS — only R190 status modified tracked, no product/test/public-doc change, pre-existing AGENTS.md diff sha256 unchanged 990cc12d; #190 CLOSED (milestone #20); #207 carried and DEFERRED (OPEN, milestone null, not inferred) | artifacts/pdca/rc2-190-reverify-1/evidence.json, artifacts/pdca/rc2-190-reverify-1/scope/scope-guard.md
- 2026-10-09 15:11 UTC | CHECK | r=2 | n=1/3 | PASS (r=2, rv=2, n=1): build 0W/0E exit 0; unit aggregate 6266 (6265 passed + 1 approved skip) / 0 failed, reconciled B 6247 + R184(+16) + R175(0) + R177(+3) = 6266; full integration Total 3561 / Errors 0 / Failed 0 / Skipped 197 / Not Run 0 exit 0, six providers incl. ClickHouse, unavailable=0 and `grep -c "is not available"`=0; all 13 recorded selectors / 18 runs exit 0 with >=1 selected; coverage line 86.8% / branch 79.4% (gates 85/75); acceptance benchmark 7/7 cases 0 failed 50.27s (<240s), cached/prepared ratio 2.01; docfx exit 0 2 warnings / 0 errors; scope guard PASS (no R190 product/test/public-doc change; pre-existing AGENTS.md diff sha256 990cc12d unchanged); mutation kept as authorized no-tool disclosure (unverified mutants RowMaterializerBuilder.cs:182, QueryCommand.QueryPreparer.cs:1035; no kill score); `validate_inner_loop.py report` exit 0; #190 stays CLOSED (milestone #20); #207 carried (OPEN, milestone not inferred). | artifacts/pdca/rc2-190-reverify-1/ (evidence.json, logs/, audits/validate-report.out, scope/scope-guard.md)
- 2026-10-09 15:11 UTC | ACT | r=2 | n=1/3 | ACT: no-op re-verification complete; #190 stays closed; #207 carried. | docs/specs/status/collection-rc2-reclaim.md
