# D201 — cached-path parity

- task: T201
- issue: #201
- milestone: 1.0.9
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=2
- phase: DO (D201-3 completed — adjudicated classification (b), plan unchanged)
- plan_state: ready
- date: 2026-10-11
- evidence_contract: rv1

## Reviews (ordered approvals)

- Review #1 — written-design approval, design revision 2 — **PASS** (separate
  non-author lens; no corrections requested on the revised design).
- Review #2 — detailed r=2 plan / sequential execution mode / rv1 evidence
  contract — **PASS**.
- Readiness is granted by both ordered approvals; rv1 is frozen before first DO.

## Goal

- Perf M12 #3 — parity of NextORM's cached path with LINQ to DB in time and allocations. Category B scenario: a fresh-fluent **non-prepared** NextORM query hitting the implicit plan-cache vs a **non-compiled** LINQ to DB query with a warm implicit cache. Time: per-scenario `cached NextORM / cached LINQ to DB <= 1.05` (5% approved); allocations: per-scenario `<= 1.00` (0→0); `Warm_Reused = 0 B` preserved.
- Categories: Cte, RecursiveCte, Except, Intersect, Distinct.
- `Prepare()` is a no-regression control only, not a goal; the public compiled/prepared API is not a route.
- Milestone stays **1.0.9**.

### Acceptance criteria

| Requirement | Acceptance | Negative case |
|---|---|---|
| R201-DESIGN | Separate approval of revised written design, then detailed plan/mode approval, before implementation/instrumentation | Authorization mistaken for design approval; wrong order |
| R201-B-CTE | Exact Cte pair meets time <=1.05 and allocation <=1.00 under executable uncertainty rule | Missing arm, changed Category B semantics, failure or OPEN |
| R201-B-RECURSIVE | Exact RecursiveCte pair meets both independent bounds | Recursion changed or another scenario hides its failure |
| R201-B-EXCEPT | Exact Except pair meets both bounds | Wrong operands/results or failing/open decision |
| R201-B-INTERSECT | Exact Intersect pair meets both bounds | Wrong operands/results or failing/open decision |
| R201-B-DISTINCT | Exact Distinct pair meets both bounds | Distinct removed or failing/open decision |
| R201-WARM | Unchanged native iteration14 gate and no-run reconciliation pass all 28 rows for both toolchains; three zero rows remain zero | Warm-only/full-job command, missing toolchain, stale report, positive zero-row bytes, or 840 row treated as zero |
| R201-ACCEPTANCE | Seven active cases run in one command; wall/Mean/Error/Allocated and ratios recorded; regression/noise investigation resolved | Eighth excluded case substituted, missing case, unresolved control |
| R201-M9-STREAM | Existing M9 and differential whole-graph/lifecycle cases pass | Stale bindings, bad CTE semantics, altered evaluation, disposal/cancellation failure |
| R201-SHARED | Actual shared AnyCommand instance excluded; later cacheable calls retain cache behavior | Guessed shared flag or sticky Cache/_dontCache mutation |
| R201-TEMP-TVP | Both guarded branch families executed and covered where applicable | Guard-only claim, missing SQL Server execution, stale/configured metadata error |
| R201-API | Public metadata surface and public requirements unchanged | New public API or required public preparation |
| R201-CACHE | Pre-prepare index has live owner, structural verification and common generation/thread domain | Fresh normalized key assumed; hash-only hit; stale/replaced/cross-thread owner |
| R201-CONSUMERS | No unauthorized abstraction/indirection; one real top-level consumer documented; nested graph state reconstructed correctly | Phantom nested owner or generalized recipe layer with no second consumer |
| R201-QUALITY | Warnings-as-errors build, valid test inventory, measured coverage and touched-branch closure | Warning/build failure, skipped required providers or untested changed branch |
| R201-ORDER | Upstream results identified; G1 order 5 retained; T201 precedes T53; collection current state reconciled | Excluded-gap/current missing-design claims remain after approval or order omits T201 |

## PLAN decision

### Current loop-back P task

P:T201-close-unit-contracts-and-footprints —
make the existing D201-2..5 unit-boundary acceptance and negative cases explicit,
reconcile each unit's evidence outputs with its write-footprint, and assign the
post-approval collection reconciliation to an explicit PLAN register step.

The approved design revision 2 is unchanged. Its written-design approval remains
valid; this return concerns only the second, detailed-plan/mode/rv1 review.

No DO iteration or product CHECK attempt is consumed.

### Revision decision

Keep r=2.

The earlier transition from r=1 to r=2 materially changed the mechanism:
the fictional nested consumer was removed, the pre-prepare raw index/live-owner
contract became explicit, and native gate/inventory/manifest actions changed.

This bounded return closes contracts and footprints for those existing actions.
It does not change the approved mechanism, goal, DO dependency order, execution
mode, measurement strategy or evidence obligations. It therefore does not
create r=3 or reset an implementation attempt.

The first DO attempt for r=2 starts at n=1 after readiness. No product attempts
are erased.

Keep evidence contract rv1 as the first execution contract, finalized before
first DO. Plan r and evidence rv are independent.

The sixteen row IDs and requirement IDs remain unchanged. No superseded execution
manifest is claimed to exist. Retain rejected PLAN candidates and reviews as
history. If an actual earlier execution manifest is discovered, do not overwrite
it; return that fact to PLAN for explicit contract supersession.

### Minimum solution: three answers

- Objective: eliminate repeated full preparation cost for eligible fresh
  implicit-cache queries and meet all five independent parity bounds.
- Constraints: exact approved goal, no public API change, shared-command safety,
  correctness/streaming, unchanged native gate, milestone/order and review gates.
- Selected minimum: specialized Entry-owned immutable preparation data,
  pre-prepare raw candidate index, verified live owner, transactional current
  graph reconstruction, then existing normalized lookup and hit execution.

No new service/interface/general recipe framework is planned. There is one
actual top-level consumer.

### Findings: fix now or deferred

| Finding / anchor | Decision |
|---|---|
| Preparation precedes normalized lookup, QueryPlanner.cs:574-575,614-615 | Fix now with a new raw candidate index; never assume a fresh normalized key exists |
| Preparation-derived hashes, QueryCommand.Plan.cs:30-44 and comparer :381-574 | Explicit field/consumer mapping and transactional key validation now |
| Store lifetime, QueryPlanStore.cs:22-42,52-81 | Same thread/generation domain and live key+holder validation now |
| No nested CTE owner, QueryPreparer.cs:304-385 | No independent child consumer; whole-graph tests now |
| Shared AnyCommand route, EntityBuilderExtensions.cs:1378-1406; QueryCache.cs:19 | Actual instance exclusion and mutation audit now |
| Warm oracle | Unchanged native 28-row ×2-toolchain gate |
| B filter selects 55 methods | Source inventory and exact ten-arm extraction now |
| Seven-case acceptance inventory | Pin seven active cases and excluded eighth now |
| Manifest completeness | Authoritative schema validation plus no-open/no-na semantic check now |
| Noise judgment | Executable approved-design algorithm |
| Unit-boundary acceptance and output footprints | Explicit D201-2..5 contracts and artifact ownership now |
| Current collection exclusions/order contradictions | Reconcile through P201-READY after plan approval, not before |
| Temp-table/TVP acceleration | Deferred; trigger: measured benefit plus reviewed safe applicability. Guards/execution/coverage now |
| Other-provider parity | Deferred; trigger: approved fair provider-specific performance oracle. Correctness now |
| Generalized abstraction or independent child cache | Deferred; trigger: real second consumer and separately reviewed need |
| Unrelated construction changes | Deferred; trigger: measured residual bottleneck and revised reviewed scope |
| Milestone/body mismatch | Owner responsibility; 1.0.9 unchanged |

## Proposed DO units

Unit-boundary acceptance is required before a unit is marked satisfied or its
dependent unit starts. Each boundary report records actual commands, exits,
scenario coverage, produced artifact paths and unresolved obligations.

Intermediate unit acceptance closes that unit's assigned portion only.
It does not prematurely mark a final evidence row met when later validation
remains outstanding. Reports are provisional evidence; CHECK owns the final
verdict.

### P201-READY — approved-plan persistence and collection reconciliation

This is a PLAN register/persistence step, not a product DO unit.

Owner: coder, routed by the collection orchestrator.

Dependencies:
- existing written-design approval for unchanged design revision 2;
- new separate approval of this detailed r=2 plan, sequential mode and rv1.

Actions:
- Persist the approved plan, existing design approval reference and new
  detailed-plan approval reference.
- Record the rv1 freeze digest.
- Reconcile the collection's CURRENT-state assertions according to the
  instructions under Prerequisites / assumptions.
- Set T201 status ready and plan_state ready only after both approvals.
- Keep G1 order 5, T201 before T53, and the downstream execution-evidence hold.
- Leave other task rows untouched.
- Record a factual reconciliation audit; do not invent a review approval.

Write-footprint:
- docs/specs/status/D201-cached-parity-1.md
- docs/specs/status/collection-1.0.9-todo.md
- artifacts/pdca/T201/rv1/reviews/design.md
  — preserve/reference the actual existing written-design approval
- artifacts/pdca/T201/rv1/reviews/plan.md
  — preserve/reference the actual new detailed-plan approval
- artifacts/pdca/T201/rv1/reviews/collection-reconciliation.md

Boundary acceptance:
- Both ordered approvals and the freeze digest are recorded.
- Current T201 readiness/design/order assertions agree across the collection.
- The factual audit maps each supplied current-state anchor to its reconciliation.
- EC201-01 has actual approval evidence; EC201-16 gains register evidence but
  remains subject to execution/manifest/final order validation.

Negative:
- Missing approval, stale excluded-gap/missing-design claim, omitted T201 in G1,
  changed sibling row, or premature removal of T53's evidence hold.
  Keep plan_state gap and do not start D201-1.

### D201-1 — ownership/state/inventory report

Dependency: P201-READY satisfied.

Actions:
- Use Roslyn first for symbol/caller/writer analysis.
- Record the raw-index publication/lookup/invalidation invariants from the design.
- Map every recipe-applied field to its producers and downstream consumers,
  including TResult hash and memoized plan-key resets.
- Identify actual DataContext/QueryCache access for shared AnyCommand exclusion;
  audit all shared-instance routes.
- Capture source-backed 55-arm B inventory, exact ten-arm map, seven active
  acceptance cases, excluded eighth, and 28-row/two-toolchain gate inventory.
- Resolve existing M9 test symbols, coverage workflow and container prerequisites.
- Identify accepted upstream T209/T211/T212/T213 outcomes and tested baseline.

Initial semantic invocation: `roslyn(action="structure")`.
Subsequent calls use actual discovered symbols, not invented test names.

Write-footprint:
- docs/specs/status/D201-cached-parity-1.md
- artifacts/pdca/T201/rv1/discovery/**
- artifacts/pdca/T201/rv1/oracle/**

Unit-boundary acceptance:
- A source-backed ownership/state report supports implementable owner,
  generation, shared-instance and field-consumer invariants.
- Fixed benchmark/gate inventories are mapped to actual source.
- Existing M9 inventory, coverage execution details and upstream baseline
  identities are recorded.
- Evidence includes discovery/ownership-state.md, discovery/shared-route.md,
  discovery/m9-inventory.md, discovery/siblings.md and oracle inventories.
- These supply the discovery portions of EC201-09,10,13,14,15,16; they do not
  replace execution evidence.

Negative:
- An owner/domain/state invariant remains unprovable, an inventory is incomplete,
  or a required upstream dependency is unavailable.

A locally solvable dependency is additive: retain this D active and blocked.
Persistent low confidence in owner soundness requests trigger-5 escalation.

### D201-2 — evidence runner, negative validation and baseline

Dependency: D201-1 satisfied.

Actions:
- Add task-specific evidence wrapper.
- Add negative self-tests for inventory, exact pairing, units, CI decisions,
  missing/stale reports, failed/skipped execution and manifest completeness.
- Add public-surface export to the planned core test file.
- Build baseline and capture API before production edits.
- Run full-job Category B and seven-case baseline commands.
- Run the unchanged native gate with FULL unset, followed by no-run reconciliation.
- Archive/restore only pinned native report classes, preserving historical files.
- Store exact commands, exits, run identity, hashes, environment and wall times.
- Validate the source-backed oracle inventories and emit a baseline boundary report.

Write-footprint:
- eng/perf/t201_evidence.py — new
- tests/nextorm.core.tests/CachedPreparationRecipeTests.cs — new
- docs/specs/status/D201-cached-parity-1.md
- artifacts/pdca/T201/rv1/instrumentation/**
- artifacts/pdca/T201/rv1/oracle/**
  — validated inventories and reconciliation evidence
- artifacts/pdca/T201/rv1/baseline/**
  — build log, environment, Category B, acceptance and native gate evidence
- artifacts/pdca/T201/rv1/api/baseline.json
- artifacts/pdca/T201/rv1/api/baseline-export.log
- artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-2.md

Unit-boundary acceptance:

| Obligation | Concrete boundary evidence/check | Required boundary result |
|---|---|---|
| EC201-15 instrumentation/build portion | Build and W-S logs; negative self-test scenario map under instrumentation/** | Build exit 0 with warnings-as-errors; W-S exit 0 because all specified rejection/OPEN cases are correctly distinguished |
| EC201-02..06 baseline portion | B-BASE exit/log and fresh full-compressed JSON; oracle/b-arms.json and exact ten-arm extraction | Source-backed 55-arm selection reconciled; all ten deciding arms have valid baseline data; no current parity claim from baseline alone |
| EC201-07 baseline portion | G-LIVE and G-REPORT logs plus baseline/iteration14/** provenance | Both native exits 0; all 28 rows for both toolchains; correct three zero rows, 840 budget row and native normalization |
| EC201-08 baseline portion | A-BASE log/JSON and oracle/acceptance.json | Exactly seven active cases with wall/Mean/Error/Allocated and baseline cached/prepared ratios |
| EC201-12 baseline portion | api/baseline.json and baseline-export.log tied to pre-production tree | Nonempty compiler-metadata inventory captured before production edits; no assertion yet that candidate API is equal |
| Boundary record | reports/unit-boundaries/D201-2.md | Lists actual source tree, commands/exits, artifact paths, inventory checks and remaining candidate obligations |

Negative:
- Self-tests accept an invalid/open manifest as PASS, pair wrong arms, use wrong
  normalization, or miss a required failure.
- Missing/invalid baseline arm, API inventory or acceptance case.
- Historical reports supply missing current gate evidence; either native
  toolchain is absent; native run or reconciliation fails.
- API baseline is captured after production edits or cannot be tied to the
  baseline tree.

Any such result leaves D201-2 unsatisfied and blocks D201-3. Baseline evidence
does not close final candidate parity, API or control rows.

### D201-3 — specialized top-level preparation reuse

Dependency: D201-2 satisfied.

Actions:
- Capture immutable raw descriptor before ordinary preparation.
- Publish Entry-owned preparation data only after successful cacheable Set.
- Add raw candidate index within the existing thread-local store.
- Share existing generation transition across both indexes.
- Verify raw shape/domain, historical live Key and holder identity before apply.
- Handle collision, owner replacement and observed generation changes.
- Reconstruct complete current normalized state transactionally; compare staged
  normalized key/owner before committing to input.
- Retain original fallback and existing execution branch.
- Exclude actual shared AnyCommand instance and original bypass/prepared routes.
- Guard temp/TVP and unproved/effectful shape cases before effects.
- Add differential core and cache-lifetime tests.
- Run core tests and record the actual test-to-obligation map and unit boundary.

Write-footprint:
- src/nextorm.core/DataContext/QueryPlanner.cs
- src/nextorm.core/Query/QueryCommand.QueryPreparer.cs
- src/nextorm.core/DataContext/Cache/QueryPlanStore.cs
- tests/nextorm.core.tests/CachedPreparationRecipeTests.cs
- docs/specs/status/D201-cached-parity-1.md
- artifacts/pdca/T201/rv1/tests/unit-boundaries/D201-3/**
  — core test logs/results, actual scenario/test map and test-tree identity
- artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-3.md
  — state/ownership/call-site audit and acceptance record

No separate nested owner, generalized recipe layer, Values scan, fingerprint-only
hit, stored raw command/closure, or new execution path.

Unit-boundary acceptance:

| Obligation | Concrete boundary evidence/check | Required boundary result |
|---|---|---|
| EC201-13 core cache/state portion | `dotnet test tests/nextorm.core.tests -c Debug`; actual scenario map for miss/hit, collision, replacement, Clear, thread separation and staged-state equivalence | Exit 0; all mapped core scenarios execute; live-owner/generation and complete field-state invariants hold |
| EC201-10 core shared/bypass portion | Actual-instance exclusion and later-cache-hit core cases; mutation audit in boundary report | No optimization-side shared mutation; no sticky Cache/_dontCache change; bypass/prepared routes retain original behavior |
| EC201-09 core differential portion | Whole-graph/current-value/evaluation/fallback cases mapped to actual tests | Ordinary and recipe core results/state agree; no stale closure, speculative evaluation or partial prepared state |
| EC201-14 consumer/abstraction portion | Actual call-site/ownership audit and top-level execution cases | One real top-level consumer; no phantom child owner or unauthorized generalized abstraction |
| EC201-11 guard portion | Temp/TVP ineligibility and unsupported-node negative core cases | Guard selects ordinary path before effects; integration/branch obligations remain for D201-4 |
| Boundary record | tests/unit-boundaries/D201-3/** and reports/unit-boundaries/D201-3.md | Records executed cases, exits, audited state map, actual produced paths and remaining integration/performance obligations |

Negative:
- A fingerprint collision or replaced/stale/cross-thread owner reaches execution.
- Recipe application leaves incomplete prepared state or changes current-value
  binding, evaluation count/order, ordinary fallback or exceptions.
- Shared instance is inferred from IsPrepared/shape instead of actual identity,
  or Cache/_dontCache changes persist.
- Any required core scenario is absent/not executed, core tests fail, or the
  audit reveals an unauthorized abstraction/second owner.

Any such result leaves D201-3 unsatisfied and blocks D201-4. Passing core tests
does not close provider, streaming, branch-coverage or performance obligations.

### D201-4 — correctness, providers, branches, quality and API

Dependency: D201-3 satisfied.

Actions:
- Add shared integration cases and SQL Server-specific TVP cases.
- Run canonical existing M9 regressions unchanged and whole-graph differential
  tests for CTE/recursive/set-operation/Distinct correctness.
- Run streaming/non-streaming, cancellation/disposal and current-value cases.
- Verify shared command exclusion and later ordinary cache hits.
- Execute temp-table and TVP branches with branch evidence.
- Run build/tests/coverage and compare public API inventories.
- Record the quality/correctness/API reports and unit boundary.

Write-footprint:
- tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs — new
- tests/nextorm.integration.tests/SqlServerCachedPreparationSpecificTests.cs — new
- tests/nextorm.core.tests/CachedPreparationRecipeTests.cs
- docs/specs/status/D201-cached-parity-1.md
- artifacts/pdca/T201/rv1/build.log
- artifacts/pdca/T201/rv1/tests/**
- artifacts/pdca/T201/rv1/coverage/**
- artifacts/pdca/T201/rv1/api/candidate.json
- artifacts/pdca/T201/rv1/api/candidate-export.log
- artifacts/pdca/T201/rv1/api/diff.json
- artifacts/pdca/T201/rv1/reports/correctness.md
- artifacts/pdca/T201/rv1/reports/shared-command.md
- artifacts/pdca/T201/rv1/reports/temp-tvp.md
- artifacts/pdca/T201/rv1/reports/cache-contract.md
- artifacts/pdca/T201/rv1/reports/recipe-consumers.md
- artifacts/pdca/T201/rv1/reports/quality.md
- artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-4.md

Preserve D201-3 raw evidence; later reports may summarize it but must not discard
or replace its recorded runs.

Unit-boundary acceptance:

| Obligation | Concrete boundary evidence/check | Required boundary result |
|---|---|---|
| EC201-09,10,13,14 | W-T and actual M9/provider/lifecycle/scenario maps; corresponding reports | Exit 0; existing M9 plus assigned differential/lifecycle tests execute; all required provider executions present; cache/shared/consumer invariants hold |
| EC201-11 | W-T and W-Q; temp/TVP execution map and touched-branch evidence | Applicable temp and TVP branches executed, SQL Server included; guards, mapping precedence and Clear behavior proved |
| EC201-12 | W-API, baseline/candidate compiler-metadata inventories and diff | Exit 0; equal public surface and unchanged public behavior/requirements |
| EC201-15 | Build, W-T, W-Q logs and quality report | Warnings-as-errors build succeeds; coverage policy measured; touched branches closed; CRLF/CPM/nullable requirements honored |
| Boundary record | reports/unit-boundaries/D201-4.md | Enumerates complete test/provider/branch/API evidence and any remaining performance obligations |

Negative:
- A required provider is skipped or a mapped M9/lifecycle/branch scenario is not
  executed.
- Result/state/evaluation/disposal/cancellation differs from the ordinary path.
- Temp/TVP guard lacks execution or branch evidence, or configured metadata
  precedence fails.
- Public inventory differs, build fails, or required touched-branch evidence is
  missing.

Any such result leaves D201-4 unsatisfied and blocks D201-5. Passing correctness
and API checks does not imply performance parity.

### D201-5 — candidate oracles and complete DO→CHECK manifest

Dependency: D201-4 satisfied.

Actions:
- Run candidate Category B and seven-case full jobs.
- Run unchanged native gate with FULL unset and no-run reconciliation.
- Execute frozen ratio/noise rules and bounded repeats.
- Resolve >20% comparable cached/prepared growth investigation.
- Produce result report and sixteen-row manifest.
- Run schema validator and stricter local semantic completeness validation.
- Preserve raw reports, every valid measurement round and evidence path.
- Record unit-boundary outcome and preserve the DO→CHECK boundary snapshot.
- Submit provisional DO report; CHECK owns acceptance.

Write-footprint:
- docs/specs/performance/m12-3-prepare-command-results.md — new
- docs/specs/status/D201-cached-parity-1.md
- artifacts/pdca/T201/rv1/candidate/**
- artifacts/pdca/T201/rv1/reports/**
  — parity, iteration14, acceptance, investigations and manifest validation
- artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-5.md
- artifacts/pdca/T201/rv1/manifest.json
- artifacts/pdca/T201/rv1/manifest.boundary.json
  — preserved DO→CHECK snapshot

Unit-boundary acceptance:

| Obligation | Concrete boundary evidence/check | Required boundary result |
|---|---|---|
| EC201-02..06 | B-CAND and W-P; exact pair extraction, raw JSON and every valid round | All five independent time/allocation decisions MET under the approved executable rule |
| EC201-07 | G-LIVE, G-REPORT and W-W on fresh candidate reports | Native exits 0; all 28 rows ×2 toolchains pass unchanged gate; provenance and normalization valid |
| EC201-08 | A-CAND and W-A; seven-case report and investigations | Seven cases complete; wall/Mean/Error/Allocated and both cached/prepared ratios recorded; control/noise investigation resolved |
| EC201-01..16 completeness | W-M and V; semantic completeness report and actual evidence paths | Exact sixteen-row coverage; valid rv1/types/schema; all required evidence exists; no open/na row in a successful provisional handoff |
| Boundary record | Result report, reports/unit-boundaries/D201-5.md and preserved manifest.boundary.json | Tested tree and complete raw evidence identified; provisional DO outcome stated without issuing final CHECK PASS |

Negative:
- Any parity/gate criterion fails or remains OPEN.
- A required case/toolchain is missing, a report is stale, a control investigation
  remains unresolved, or repeats are cherry-picked.
- Manifest contains open/na, missing/extra/duplicate IDs, invalid rv/type,
  nonexistent evidence paths or unsupported met claims.

A negative result leaves D201-5 unsatisfied. Still emit a truthful diagnostic
manifest with open reasons when evidence is incomplete; do not falsely claim
unit completion or PASS. Route the provisional report according to the
DO→PLAN/evidence-recovery rules. DO does not issue STOP or a final verdict.

### Unit execution mode

Sequential in one tree.

Production state, index ownership, normalized equality and tests share one
contract. Baseline and candidate identity must not be mixed across trees.

Use the collection's existing G1 tree/worktree if supplied; no additional
worktree is needed. This plan grants no additional commit/merge/push permission.

## Prerequisites / assumptions

### What is established and what remains

| Item | Evidence / treatment |
|---|---|
| Trigger and conversational authorization | Owner confirmation; iteration-15-cached-path-results.md:47 |
| Written design revision 2 | Approved by the first ordered separate review; unchanged by this return |
| Detailed-plan/mode/rv1 approval | Second ordered review returned FAIL; new approval required before readiness |
| Fresh normalized key unavailable before prepare | Verified producer/consumer order and hash assignments |
| Existing normalized owner is available historically | Store key/Entry surface; raw alias will be new implementation |
| Common generation/thread domain can be implemented | Existing thread-local/generation store; explicit invariants and tests required |
| Independent nested owner | Does not exist; not introduced |
| Shared command identification | Actual AnyCommand route/instance supplied; access path audited in D201-1 |
| Native gate CLI/inventory | Verified; commands fixed below. Their DO exits are not yet established |
| Historical gate reports | Historical only, never post-change evidence |
| B/acceptance inventories | Verified source-backed counts/names; inventory artifacts still planned |
| Complete recipe-state implementation | D201-1 writer/consumer report and D201-3 differential proof |
| M9 canonical inventory | Remaining read-only gather need |
| Coverage tools/container readiness | Remaining local execution prerequisites |
| Performance feasibility | Unknown until candidate measurements; no assumed parity |
| Upstream accepted implementation state | Remaining collection evidence need |
| Wider provider parity | Not established by SQLite oracle |

### Sibling contract

G1 order remains:

T209 → T211 → T212 → T213 → T201 → T53

T201 remains order 5.

Before baseline collection, record accepted upstream result revisions, or explicit
evidence that an upstream outcome creates no implementation dependency.
PLAN-ready alone is not evidence that sibling implementation passed.

Do not absorb or modify sibling scope. Missing dependency adds an active
prerequisite while retaining the original D and its remaining acceptance.

T53 must not freeze before T201 accepted evidence.

### Collection reconciliation on approval

Responsible step: P201-READY.
Owner: coder, routed by the collection orchestrator.
This occurs after detailed-plan approval and before D201-1, not during an
unapproved implementation unit.

Coder must update CURRENT-state assertions, not only append a contradictory log:

- T201 row: status pending/re-open-needed → ready; plan_state gap → ready.
- Replace current excluded-gap/missing-design claims at the supplied :20,:32,:57
  anchors with the separately reviewed design fact.
- Include T201 at G1 order 5 in the groups table, consistent with :59/:64.
- Keep T201 before T53.
- Re-evaluate the ALL-barrier assertion using the unchanged collection rule and
  actual remaining rows; do not leave a stale assertion based on T201 exclusion.
- Keep collection phase consistent with reality: D only if collection execution
  is genuinely current and permitted; otherwise retain its applicable PLAN gate.
- Preserve the downstream freeze hold until T201 accepted execution evidence,
  not merely PLAN readiness.
- Leave all other task rows untouched.

Move previous exclusion/re-open facts into clearly labeled history if retained.
Do not retain them as current state after approval.

Required output:
artifacts/pdca/T201/rv1/reviews/collection-reconciliation.md,
mapping supplied current-state anchors to the actual reconciled register.
EC201-16 verifies this evidence together with execution order and manifest
completeness; the PLAN register step alone does not close that entire row.

### DO→PLAN classification

- Local prerequisite: add active dependency; original D stays active/blocked.
- Harmless implementation detail within fixed criteria: explicit assumption/risk.
- Insufficient evidence: targeted scout first.
- Persistent low confidence in lookup/domain/state soundness: escalate trigger 5.
- True external blocker: recommend orchestrator invoke escalate.
- Measured mechanism miss: new P task with changed actions before another r.
- Missing report alone: evidence recovery, not automatic new DO/revision.

### Test strategy and shared contract

CP201:
recipe and ordinary preparation are equivalent in normalized SQL/query graph,
results, current-value binding, evaluation/failures, state ownership, cache
isolation and execution lifecycle.

Core tests prove descriptor/state/owner/fallback invariants.
Integration tests prove database and lifecycle behavior.
Benchmarks prove cost, not correctness.

| Axis / variants | Closure |
|---|---|
| Type: value/reference TResult, nullable scalar, mixed DTO | test — result hash, mapping and current-state equivalence |
| Null/default: null reference, nullable values, default scalar, null/empty input | test — preserve structural versus parameter distinctions |
| Missing ctor / supported constructor / member-init | test — same supported behavior or same ordinary-path failure |
| Projection: scalar/entity/anonymous/DTO; changed member order/type/converter | test — equal verified shape versus miss/fallback |
| Cte/RecursiveCte/Except/Intersect/Distinct | test — whole-graph correctness and all five parity rows |
| Nested CTEs: dependencies/recursion/repeated refs/scopes | test — top-level graph reconstruction; no independent child owner |
| Parameters: none/current closures/nested values/literals | test — no retained source or repeated evaluation |
| IN/PreWhere cardinality and effectful enumeration | guard + test until side-effect-free identity and equivalent state are proved; required category cannot be dropped |
| Providers: SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse | test — applicable shared/provider semantics; skipped required execution is not green |
| MariaDB and in-memory | guard + core/dialect test of original fallback unless supported domain is proved; separate real-server/perf expansion deferred until an available approved harness |
| Flags: storeInCache true/false, Cache enabled/disabled, IsPrepared true/false, NoParams/NeedsParamRefresh | test — bypass and refresh combinations |
| Shared AnyCommand / ReplaceCommand | actual instance guard + test; no inferred ownership flag |
| Execution: sync/async, nonStreamUsing variants, cancellation, partial enumeration/disposal | test |
| Temp tables absent/present | guard + executed branch test |
| TVP automatic/configured, clear, supported null/empty/typed inputs | guard + SQL Server test and branch evidence |
| Lifetime: first miss/hit/collision/owner replacement/Clear/thread separation | test |
| Mapping/configuration changes | test or guard + negative test where pre-prepare domain identity is unproved |
| Unsupported node/unproved immutable structure | guard + test; no effects before fallback |
| Public Prepare/storeInCache:false | original route guard + tests/control, not parity route |
| Non-SQLite performance parity | deferred; trigger: approved provider-specific fair oracle |

No new abstraction requiring a second consumer is introduced.
R201-CONSUMERS audits that invariant and the real top-level route.

### Build, coverage and test level

- .NET 10; warnings-as-errors, nullable and analyzers.
- No package addition planned; any necessary package follows CPM and PLAN scope.
- Preserve CRLF.
- Run core/dialect regressions and required container integration suite.
- Load .opencode/skills/running-integration-tests/SKILL.md before integration.
  Start/recheck Podman as instructed; provider skips do not mean green.
- Collect coverage with coverage.settings.xml and repository workflow tools.
- Record 85% line /75% branch thresholds: hard on main, branch policy warning
  on 1.0.9; no unexplained deterioration or missing touched branch is hidden.
- Explicit temp/TVP, collision, replacement, clear, fallback and shared-route
  branch closure remains mandatory regardless of aggregate percentages.

### Priority matrix

All sixteen evidence rows are P1 by construction:

- goal/zero-allocation/API/approval invariants;
- query-cache class mandatory acceptance ratio, shared safety and temp/TVP;
- correctness/owner/lifecycle execution path;
- completeness, quality and collection order.

CHECK cannot downgrade these rows or mark them N/A.

### Docs plan

This bounded PLAN return changes only this status plan. The approved design
revision 2 is unchanged.

Execution touches internal status, result report and collection reconciliation.
Do not change public docs/Russian public docs/readme/API reference, historical
performance documents, issue milestone/body, native gate or budgets.

No public usage/API change means no public documentation migration.
Public pages must not link to internal specs.

### Performance decision

Required: QueryPlanner.cs:574-575 and preparation-derived hashes are per-call
fresh-query work.

- Category B full jobs: five exact independent pairs extracted from 55 arms.
- Native iteration14: unchanged 28-row ×2-toolchain gate, FULL unset.
- Acceptance full jobs: seven cases, prepared control and cached/prepared ratios.

Use the approved design's executable noise rule:
- positive finite matching means/CI bounds, same exported confidence >=99%;
- time MET only when point ratio <=1.05 and conservative ratio upper bound <=1.05;
- decisive FAIL when point and conservative lower bound exceed 1.05;
- otherwise OPEN;
- allocations compare raw bytes, with 0→0 MET and 0→positive FAIL;
- preserve all valid rounds; no cherry-picking;
- at most two additional targeted full-job rounds;
- control noise/regression investigations remain OPEN until resolved.

Approximately 20% noise guidance does not relax the 5% approved bound.

### Reconnaissance decision

Required after approval:

1. D201-1 read-only field/ownership/domain inventory.
2. D201-3/D201-5 bounded empirical implementation experiment.

Observable success:
live-owner/generation tests, equivalent complete state, current bindings,
five decisive parity passes and unchanged native gate.

Observable failure:
unproved owner/domain invariant, state mismatch, dominant residual verification,
excess allocation, or persistent OPEN measurements.

### Risks

Verification cost, staging allocations, state-map incompleteness, CTE scopes,
mapping identity, shared-route access and measurement uncertainty.

Confidence:
- high that an explicit raw index plus live owner validation can preserve the
  existing store ownership/invalidation model;
- implementation and performance confidence remain conditional on tests/probes;
- no claim that future artifacts or exits already exist.

## Planned write-footprint

Tracked files:

1. docs/specs/performance/m12-3-prepare-command-design.md
   — approved, unchanged by this return; no implementation-stage edit authorized
2. docs/specs/status/D201-cached-parity-1.md
3. docs/specs/status/collection-1.0.9-todo.md
   — P201-READY owns readiness/current-state reconciliation
4. src/nextorm.core/DataContext/QueryPlanner.cs
5. src/nextorm.core/Query/QueryCommand.QueryPreparer.cs
6. src/nextorm.core/DataContext/Cache/QueryPlanStore.cs
7. tests/nextorm.core.tests/CachedPreparationRecipeTests.cs — new
8. tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs — new
9. tests/nextorm.integration.tests/SqlServerCachedPreparationSpecificTests.cs — new
10. eng/perf/t201_evidence.py — new
11. docs/specs/performance/m12-3-prepare-command-results.md — new

Generated evidence ownership:
- P201-READY: reviews/design.md, reviews/plan.md,
  reviews/collection-reconciliation.md.
- D201-1: discovery/** and source-backed oracle/** inventories.
- D201-2: instrumentation/**, validated oracle/**, baseline/**,
  api/baseline.json, api/baseline-export.log,
  reports/unit-boundaries/D201-2.md.
- D201-3: tests/unit-boundaries/D201-3/** and
  reports/unit-boundaries/D201-3.md.
- D201-4: build.log, tests/**, coverage/**, candidate API/diff artifacts,
  correctness/shared/temp-TVP/cache/consumer/quality reports and
  reports/unit-boundaries/D201-4.md.
- D201-5: candidate/**, performance/investigation/manifest-validation reports,
  reports/unit-boundaries/D201-5.md, manifest.json and manifest.boundary.json.

All generated paths are under artifacts/pdca/T201/rv1/ unless explicitly stated.

Native gate transient output:
benchmarks/BenchmarkDotNet.Artifacts/results/** for only its three pinned classes;
archive originals, copy fresh evidence, restore historical tracked files.
No net historical-report edit is planned.

No benchmark body/configuration, iteration14 gate/budget, public API or additional
production file edit is authorized. Actual necessary expansion returns to PLAN.

## Evidence contract

### Freeze, IDs and manifest mapping

Execution contract rv1 is frozen in this status before first DO, after approvals.
Coder records its digest through P201-READY. DO writes the manifest at DO→CHECK:

artifacts/pdca/T201/rv1/manifest.json

Required row/requirement IDs remain stable.

The rejected PLAN candidates are retained as review history, not completed
execution contracts. No older execution manifest is asserted to exist, so no
manifest supersession is fabricated.

For a later justified required variant:
- CHECK→PLAN;
- preserve existing IDs and obligations;
- add new IDs;
- explicitly supersede rv1 with rv2;
- retain superseded manifest/evidence;
- freeze the new version before its DO boundary.
Missing evidence alone does not justify revision.

Status table slots:
row ID, requirement ID, scenario/check, exact command, result/log requirements,
evidence kind/source, artifacts, owner, observable applicability, rv.

Manifest mapping uses the authoritative fields only:

    {
      "task": "T201",
      "tree": "<nonempty tested-tree identity>",
      "status_file": "docs/specs/status/D201-cached-parity-1.md",
      "contract_rv": 1,
      "required_rows": ["EC201-01", "...", "EC201-16"],
      "rows": [
        {
          "id": "EC201-01",
          "status": "met",
          "evidence": [
            {"kind": "review", "path": "<actual existing artifact path>"}
          ]
        }
      ]
    }

The abbreviated example explains structure; the actual manifest must enumerate
all sixteen IDs and rows. Do not emit ellipses/placeholders.

Do not put owner, requirement ID, scenario, applicability or per-row rv into
the manifest as assumed validator fields; retain them in this contract table.

Schema validation:

    python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T201/rv1/manifest.json

- exit 0: schema valid only;
- exit 2: schema violations;
- exit 1: usage error.

Additional mandatory semantic checks:
- required_rows exactly equals the frozen sixteen IDs;
- rows exactly covers them, once each;
- contract_rv is integer 1, not bool/string;
- every evidence path exists and refers to the stated tested tree/run;
- command/result evidence contains actual exit and relevant output;
- no unconditional row is na;
- no required row is open at PASS.

A schema-valid manifest with open rows is not passing evidence.

During CHECK, preserve manifest.boundary.json and add evidence/resolution records
append-only. Do not remove/replace prior evidence, required IDs or obligations.
If materializing a row's open→met resolution, preserve the prior reason/boundary
snapshot and append the resolution record before regenerating the schema-valid
manifest projection. Revalidate it. No PASS may refer to a projection still
containing open or na.

### Exact command registry

Repository root is cwd. Wrapper is planned, not currently asserted to exist.

W-S:
    python3 eng/perf/t201_evidence.py --stage self-test --root artifacts/pdca/T201/rv1

W-B:
    python3 eng/perf/t201_evidence.py --stage baseline --root artifacts/pdca/T201/rv1

W-T:
    DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock python3 eng/perf/t201_evidence.py --stage tests --root artifacts/pdca/T201/rv1

W-C:
    python3 eng/perf/t201_evidence.py --stage candidate --root artifacts/pdca/T201/rv1

W-P:
    python3 eng/perf/t201_evidence.py --stage parity --root artifacts/pdca/T201/rv1

W-W:
    python3 eng/perf/t201_evidence.py --stage warm-gate --root artifacts/pdca/T201/rv1

W-A:
    python3 eng/perf/t201_evidence.py --stage acceptance --root artifacts/pdca/T201/rv1

W-API:
    python3 eng/perf/t201_evidence.py --stage api --root artifacts/pdca/T201/rv1

W-Q:
    python3 eng/perf/t201_evidence.py --stage coverage --root artifacts/pdca/T201/rv1

W-M:
    python3 eng/perf/t201_evidence.py --stage manifest --root artifacts/pdca/T201/rv1

V:
    python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T201/rv1/manifest.json

Build:
    dotnet build -c Release

CORE:
    dotnet test tests/nextorm.core.tests -c Debug

G-LIVE:
    env -u NEXTORM_BENCH_FULL python3 eng/perf/iteration14_gate.py --manifest eng/perf/iteration14-budgets.json --reports-dir benchmarks/BenchmarkDotNet.Artifacts/results

G-REPORT:
    env -u NEXTORM_BENCH_FULL python3 eng/perf/iteration14_gate.py --manifest eng/perf/iteration14-budgets.json --reports-dir benchmarks/BenchmarkDotNet.Artifacts/results --no-run

B-BASE:
    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --exporters json --artifacts artifacts/pdca/T201/rv1/baseline/category-b

B-CAND:
    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*' --exporters json --artifacts artifacts/pdca/T201/rv1/candidate/category-b

A-BASE:
    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --exporters json --artifacts artifacts/pdca/T201/rv1/baseline/acceptance

A-CAND:
    NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --exporters json --artifacts artifacts/pdca/T201/rv1/candidate/acceptance

W-B/W-C invoke the corresponding B/A commands and G-LIVE/G-REPORT.
They set FULL only for B/A, explicitly unset it for G, and isolate phase reports.
W-W validates archived gate evidence and invokes unchanged report-only gate
against the selected current phase when reconciliation is required.

CORE supplies the D201-3 core unit boundary; record stdout/stderr, actual exit,
test inventory and tested tree under tests/unit-boundaries/D201-3/**.

W-T includes existing core/dialect suites resolved in D201-1, CORE, and:

    DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor

W-Q records exact native coverage workflow commands after D201-1 discovery.
W-API uses compiler-metadata inventories emitted by planned core-test
instrumentation.

Wrapper exits:
0 complete stage meeting its required predicates;
1 measured failure or invalid/missing evidence;
2 inconclusive/operationally blocked.
None of 1/2 is PASS.

### required_rows and obligations

Mandatory required_rows:

EC201-01, EC201-02, EC201-03, EC201-04, EC201-05, EC201-06,
EC201-07, EC201-08, EC201-09, EC201-10, EC201-11, EC201-12,
EC201-13, EC201-14, EC201-15, EC201-16.

All sixteen rows are unconditional. Their applicability predicates below
explain why they apply; they are not N/A escape clauses.

| Row / requirement | Required check and exact command | Result/log requirement | Planned evidence kinds/sources and artifacts | Owner | Observable applicability | rv |
|---|---|---|---|---|---|---|
| EC201-01 / R201-DESIGN | Existing separate check task: `review T201 design revision 2 only; issue written-design verdict`; subsequently new task: `review T201 r=2 plan, rv1 contract and sequential mode against approved design; issue plan verdict` | Two ordered explicit approvals before implementation/instrumentation; existing design approval preserved | Review records/digests/timestamps: reviews/design.md, reviews/plan.md | check; coder persists through P201-READY | Always: proposed implementation | rv1 |
| EC201-02 / R201-B-CTE | B-BASE, B-CAND, W-P; exact Cte pair in approved design | exits 0; exact extraction; Cte time/allocation MET under frozen algorithm | BDN full-compressed JSON, exact inventory and calculations: baseline/category-b/**, candidate/category-b/**, oracle/b-arms.json, reports/parity.json/md | perf gather | Always: approved scenario | rv1 |
| EC201-03 / R201-B-RECURSIVE | Same commands; exact RecursiveCte pair | Both bounds MET; no changed recursive semantics | Same raw roots; identified RecursiveCte report entry and scenario validation | perf gather | Always | rv1 |
| EC201-04 / R201-B-EXCEPT | Same commands; exact Except pair | Both bounds MET; correct operands/results | Same raw roots; Except report entry | perf gather | Always | rv1 |
| EC201-05 / R201-B-INTERSECT | Same commands; exact Intersect pair | Both bounds MET; correct operands/results | Same raw roots; Intersect report entry | perf gather | Always | rv1 |
| EC201-06 / R201-B-DISTINCT | Same commands; exact Distinct pair | Both bounds MET; Distinct preserved | Same raw roots; Distinct report entry | perf gather | Always | rv1 |
| EC201-07 / R201-WARM | G-LIVE, G-REPORT, W-W | native exits 0; 28 rows ×Default/InProcessEmit; three zero rows 0; 840 row budget respected; normalization unchanged | Existing gate/budget, fresh JSON provenance, native logs: oracle/iteration14-inventory.json, baseline/iteration14/**, candidate/iteration14/**, reports/iteration14.md/json | perf gather | Always: zero/control invariant | rv1 |
| EC201-08 / R201-ACCEPTANCE | A-BASE, A-CAND, W-A | exits 0; exactly seven active cases; wall/Mean/Error/Allocated and cached/prepared ratios; >20%/noise investigation resolved | Full JSON/logs, canonical inventory and analysis: oracle/acceptance.json, baseline/acceptance/**, candidate/acceptance/**, reports/acceptance.md/json | perf gather | Always: query-path class mandatory | rv1 |
| EC201-09 / R201-M9-STREAM | W-T | exit 0; canonical M9 and new whole-graph/lifecycle cases executed; required providers not skipped | Test logs/results, actual symbol/scenario map: discovery/m9-inventory.md, tests/**, reports/correctness.md | tests gather | Always: preparation changes | rv1 |
| EC201-10 / R201-SHARED | W-T; actual AnyCommand exclusion and subsequent-cache scenarios | exit 0; route/instance guard; no Cache/_dontCache mutation; later cache hits | Roslyn route/mutation audit and tests: discovery/shared-route.md, tests/**, reports/shared-command.md | tests gather | Always: mandatory shared state invariant | rv1 |
| EC201-11 / R201-TEMP-TVP | W-T, W-Q | exits 0; temp/TVP applicable branch execution and guard coverage; SQL Server required; configured metadata precedence | Core/integration/branch evidence: tests/**, coverage/**, reports/temp-tvp.md | tests/coverage gather | Always: class-mandatory branch families | rv1 |
| EC201-12 / R201-API | W-API | exit 0; equal public inventories and unchanged public behavior | Compiler-metadata baseline/candidate/diff, public tests: api/baseline.json, api/candidate.json, api/diff.json and export logs | API gather | Always: AC5 | rv1 |
| EC201-13 / R201-CACHE | W-T; owner/index/state cases | exit 0; verified raw shape, live key+holder, common generation, collision/replacement/thread/state tests | Ownership/state map and tests: discovery/ownership-state.md, tests/**, reports/cache-contract.md | tests gather | Always: raw index introduced | rv1 |
| EC201-14 / R201-CONSUMERS | W-T plus source/impact audit | exit 0; one actual top-level consumer; no unauthorized abstraction/second owner; nested graph reconstruction equivalent | Call/ownership audit and executed cases: reports/recipe-consumers.md, tests/** | tests gather | Always: invariants 1–2 | rv1 |
| EC201-15 / R201-QUALITY | Build, W-S, W-T, W-Q | build/self-tests/tests valid; coverage policy and touched branches closed; CRLF/CPM/nullable honored | build.log, instrumentation/**, tests/**, coverage/**, reports/quality.md; baseline build evidence preserved separately | build/tests/coverage gather | Always | rv1 |
| EC201-16 / R201-ORDER | W-M, V, collection reconciliation review | W-M/V exits 0; exact sixteen-row coverage; no open/na at PASS; evidence paths valid; upstream identities/order/current state correct | manifest.json, manifest.boundary.json, reports/manifest-validation.md, discovery/siblings.md, reviews/collection-reconciliation.md | coder; check verifies | Always | rv1 |

Actual test symbols/file locations are added only after discovery/implementation.
Future paths above are planned, not claims of existence.

Unit-boundary reports are additional evidence indexes, not replacements for
the required raw reports, tests, logs or final row predicates.

### Manifest self-test matrix

W-S must reject or distinguish:

- missing required row;
- undeclared row;
- duplicate required ID or row ID;
- wrong contract_rv type/value;
- empty/missing evidence for met;
- nonexistent evidence path;
- open without reason;
- schema-valid open: validator may exit 0, local PASS check must fail;
- schema-valid na: local T201 check must fail;
- nonzero command exit or required provider skip;
- stale report filling a required gate row;
- wrong toolchain and wrong allocation normalization;
- wrong B-arm pairing or acceptance inventory.

### CHECK re-gather budget

Owner: check.

At most two targeted re-gather requests per CHECK attempt, total.
At most one may request a targeted performance rerun.
No nested retry multiplier.

Each identifies row IDs, missing evidence source, exact command and result.
No code, benchmark semantics, priorities, tolerance, required_rows or rv changes.

Preserve boundary evidence and append new evidence/resolution records.
After budget exhaustion, missing or inconclusive rows remain open and prohibit
PASS. Absence alone does not cause another DO iteration or contract revision.

DO measurement budget is separate:
initial baseline/candidate plus at most two additional targeted full-job rounds.
All valid rounds remain in the decision.

The same-revision no-fourth-failed-product-CHECK rule remains in force.
The ordered PLAN-review returns are not such attempts.

## Next allowed step

1. Coder persists this revised r=2 plan only; plan_state stays gap.
   Do not modify approved design revision 2.
2. Preserve/reference the existing written-design approval.
3. A new separate check lens reviews the detailed plan, rv1 contract and
   sequential mode against that approved design.
4. On approval, coder performs P201-READY: record approval/freeze digests and
   reconcile every supplied collection CURRENT-state assertion.
5. Set T201 row status ready and plan_state ready, retaining G1 order 5.
   Leave other task rows untouched and retain the T53 execution-evidence hold.
6. After P201-READY boundary acceptance, DO begins with D201-1, n=1, under
   autonomous collection gates. No additional user-go wait is introduced.

Do not treat owner authorization, persisted files or schema validation alone
as detailed-plan approval.

If the new review fails, return a new specific P task. Only actual changed
actions justify another plan revision.

## DO — D201-1 (ownership/state/inventory report)

- DO started for the cycle: N=1, plan revision r=2, attempt n=1, evidence contract rv1.
- Unit boundary: D201-1 is a **discovery** unit — a read-only ownership/state/inventory report.
  **No production `.cs` edit is authorized or made**; the single tracked write is this status file.
  Dependency P201-READY is satisfied; no worktree/branch/merge (single group, current tree).
- Method: Roslyn only for C# symbols (`roslynq` = the `roslyn` tool, solution `nextorm.slnx`), batched
  per action; `read` for line-anchored bodies; `rg --glob '*.cs'` (ignore-aware) only for non-symbol
  text. Raw Roslyn output captured verbatim under `discovery/raw/`.

- Produced artifacts (all under `artifacts/pdca/T201/rv1/`, gitignored = evidence only):
  - `discovery/ownership-state.md` — preparation-before-key confirmation; `QueryPlanStore`
    ownership/invalidation invariants; the full recipe-applied field → producer → consumer map
    (incl. `ResultPlanHash` and the three `InvalidatePlanKey` reset sites); design-vs-HEAD line drift.
  - `discovery/shared-route.md` — the actual shared `AnyCommand` instance and every route
    (route/instance identity, not shape), `ReplaceCommand`, and the `_dontCache`/`Cache` mutation audit.
  - `discovery/m9-inventory.md` — M9 regression symbols, plan-key/shared/whole-graph/lifecycle suites,
    native coverage workflow commands, and container prerequisites.
  - `discovery/siblings.md` — accepted upstream T209/T211/T212/T213 outcomes and the tested baseline identity.
  - `discovery/inner-loop/{brief,report}.json` — docs/evidence-only inner-loop scope (rebuild=none).
  - `discovery/raw/**` — raw Roslyn `structure`/`types`/`members`/`refs`/`callers` output.
  - `oracle/b-arms.json` (55 `B_*` arms, 12 categories, exact ten-arm map),
    `oracle/acceptance.json` (7 active cases + excluded eighth),
    `oracle/iteration14-inventory.json` (28 rows: 3 zero, 24 budget, 1 no_growth).

- Commands / exits (full argument arrays in `discovery/inner-loop/report.json`):
  - `git status --porcelain` → 0 (68 entries; ~39 foreign to this collection, preserved, never staged);
  - `git log --oneline -8` → 0; `git rev-parse --abbrev-ref HEAD` → 0 (`1.0.9-rc3`);
    `git rev-parse HEAD` → 0 (`71e29b197b4ca0d88acb217a9f930d0cdf3c848d`);
  - `roslynq nextorm.slnx structure` → 0; `members`/`refs`/`callers` for the recipe/storage/shared/test
    symbols → 0 (raw under `discovery/raw/`);
  - `python3` oracle generator → 0; `python3 scripts/validate_inner_loop.py brief …` → 0;
    `python3 scripts/validate_inner_loop.py report …` → 0. The docs/evidence-only scope was
    **accepted** (not rejected as empty/zero-selector); no fictitious rebuild was used.

- Boundary answers:
  - (a) `QueryPlanner.cs` still prepares before the normalized key at the cited lines — **yes**
    (`:574`/`:575` before `:614`/`:615`; the file is unchanged since the design commit `bd4af157`).
  - (b) Inventories confirmed by source: 55 `B_*` arms / 12 categories / exact ten-arm map; 7 active
    acceptance cases (+1 excluded eighth, build-excluded by `nextorm.benchmark.csproj:47`); **28
    rows** × 2 toolchains (3 zero, 24 budget, 1 `no_growth`), with `InAtIn_Inline_Warm_Reused = 840 B/op`
    as a **budget** row (not a fourth zero row). Mismatches found (drift, not mechanism):
    the design's `EntityBuilderExtensions.cs:1378-1406` (`GetAnyCommand`) is now `:1404-1432`;
    `QueryCommand.cs:888-897` (`ReplaceCommand`) is now `:933-951`;
    `scripts/iteration15_evidence.py:48-56` is now `:49-57`;
    `nextorm.benchmark.csproj:44-46` is now `:47`;
    the `QueryCommand.QueryPreparer.cs` anchors moved (file +220/−10 after the design) — the
    HEAD-based map in `discovery/ownership-state.md` supersedes them.
  - (c) No invariant proved un-implementable; no DO→PLAN candidate raised by this unit. Staged-field
    completeness must be re-derived from HEAD, not from the design's stale line numbers.

- Remaining obligation: the production recipe (raw candidate index, live-owner/generation validation,
  transactional staged application) is **D201-3** and is not implemented here; D201-2 (evidence
  runner, negative self-tests, baseline) is the next dependent unit.

## DO — D201-2 (evidence runner, negative validation and baseline)

- Unit boundary: D201-2 is instrumentation + measurement only. **No production `src/**` edit** was made
  (before or after the API baseline capture). Tracked writes are this status file,
  `eng/perf/t201_evidence.py` (new) and `tests/nextorm.core.tests/CachedPreparationRecipeTests.cs` (new).
  Cycle N=1, plan revision r=2, attempt n=1, contract rv1 — unchanged; single group, current tree,
  no worktree/branch/merge.
- Dependency D201-1 is satisfied (commit `bff40da2`); D201-3 is unblocked once this boundary is green.

### Deliverables

1. `eng/perf/t201_evidence.py` — stages `self-test|baseline|tests|candidate|parity|warm-gate|acceptance|api|coverage|manifest`
   under `--root artifacts/pdca/T201/rv1`, exits 0/1/2 exactly as contracted. It records exact argv,
   env set/unset, exit code, wall time, log sha256 and run identity in `runs.json`, validates the oracle
   inventories, runs the **unchanged** native gate, implements the frozen ratio/noise rules (point and
   conservative CI bounds for time, raw bytes for allocations, MET/FAIL/OPEN and across-round
   aggregation exactly as written) and assembles the evidence. No benchmark body, LINQ to DB version,
   prepared status, data semantics, gate budget, gate script or toolchain configuration was changed.
2. Negative self-tests (`--stage self-test`, exit **0**): **46/46** cases distinguished — the complete
   §Manifest self-test matrix as 20 rejected fixtures plus 1 truthfully accepted fixture (with the
   schema-only vs schema-valid-but-local-fail distinction), and 25 frozen-decision cases (Student-t
   99% critical values against published values, the real reports' implied ≥99% confidence, MET/FAIL/OPEN
   shapes, allocation 0→0/0→positive, round aggregation).
3. `tests/nextorm.core.tests/CachedPreparationRecipeTests.cs` — the public-surface (compiler-metadata)
   export mechanism only; 3 facts; exports 298 public types across 4 assemblies; asserts nothing about a
   candidate inventory (D201-4 owns the diff).
4. Baseline runs, exact registry commands (`NEXTORM_BENCH_FULL=1` for B/A, unset for G):
   - build baseline `dotnet build -c Release` exit 0, 2.731 s (`baseline/build.log`);
   - B-BASE exit 0, 779.940 s, **55** arms selected, exact ten-arm extraction OK;
   - A-BASE exit 0, 130.320 s, **7** active cases;
   - G-LIVE exit 0, 779.971 s; G-REPORT `--no-run` exit 0, 0.049 s; **56/56** row/jobs
     (28 rows × Default/InProcessEmitToolchain), the three zero rows exactly 0 B,
     `InAtIn_Inline_Warm_Reused` 744.00/744.01 B/op against the 840 budget, normalization divisor 100;
   - pinned-class reports archived (18 files) and the historical tracked reports restored exactly
     (302 files before/after, 0 differing; `git status` for that directory unchanged at 18 entries).
5. Inventory validation against the real runs: `oracle/reconciliation.json` — 55 arms OK, exact ten-arm
   map OK, 7 acceptance cases OK, 28 rows × 2 toolchains OK, no problems.
6. Boundary report: `reports/unit-boundaries/D201-2.md`.
7. `api/baseline.json` + `api/baseline-export.log` captured **before any production edit**, with the
   artifact recording its own tree identity in the `capture` block (`tree.head`/`head_tree`/branch/status
   digest, `pre_production: true`, per-assembly DLL sha256). No candidate-equality assertion is made.

### Stage exits

- W-S 0; W-B 0; W-A 0; W-W 0; W-API 0; inner loop exit 0 (3/3) and boundary build 0 Warning(s)/0
  Error(s) plus core suite 2549/2549.
- W-P (`--stage parity`) = **2 inconclusive by contract**: no candidate Category B round exists yet, so
  no parity decision may be issued from the baseline alone (`reports/parity.json` = `status: inconclusive`).
  This is neither a PASS nor a measured failure.
- `--stage tests`, `--stage candidate`, `--stage coverage` and `--stage manifest` are not invoked in this
  unit — they belong to D201-4/D201-5. Their preconditions fail closed (e.g. W-W without archives exits 2).

### Boundary answers

- (a) Self-tests: the local PASS check accepts a truthful manifest and rejects every specified
  invalid/open case; schema-only and schema-valid-but-forbidden cases are distinguished.
- (b) Baseline: all ten deciding arms carry valid baseline data, the source-backed 55-arm selection
  reconciles, and the gate covers all 28 rows for both toolchains with the correct three zero rows, the
  840 budget row and the native normalization. Baseline headline (informational, **not** a parity claim):
  the cached-path time bound is already met on the baseline (point ratios 0.54–0.72), while allocations
  fail on all five scenarios (NextORM 50 720–167 684 B vs LINQ to DB 40 080–92 802 B per B·op).
  No parity/API/control row is closed.
- (c) Recorded environment finding (contained, non-blocking): BDN refuses to run out-of-process while
  three foreign stale copies of `nextorm.benchmark.csproj` exist under `artifacts/pdca/D208/**` and
  `artifacts/pdca/D236-*/**` (`System.NotSupportedException: Found more than one matching project file`,
  0 benchmarks executed). Containment is a fail-closed opt-in guard (`NEXTORM_T201_QUARANTINE=1`) that
  renames those directories out of the tree for the measurement and always restores them
  (`instrumentation/benchmark-project-quarantine-baseline.json`: restored, zero problems). The registry
  commands stay byte-identical. D201-4/D201-5 need the same opt-in.
- (d) Interpretation gap recorded for CHECK/PLAN (evidence-format only, non-blocking): the design's
  "same exported confidence level, at least 99%" has no percentage field in BDN's JSON export
  (`ConfidenceInterval.Level` is a constant opaque identifier, observed `12` for every entry regardless
  of N). The wrapper implements the intent — both arms must export the same non-null level identifier
  **and** the interval is arithmetically verified as at least a two-sided 99% t-interval
  (`Margin/StandardError` = 4.14–4.32, i.e. implied confidence ≈0.999) — and records both in the
  evidence. The 1.05 tolerance, MET/FAIL/OPEN shapes and round aggregation are unchanged.
- (f) Baseline-capture integrity defect found at the boundary and fixed red→green: the recorded inner-loop
  run of the export test carried no `T201_API_EXPORT_PATH`, so the test wrote its default path and
  silently overwrote the captured `api/baseline.json` (the `capture` block was dropped; the wrapper
  correctly refused the capture-less file and re-captured — visible as the repeated
  `api/build-baseline`/`api/export-baseline` runs in `runs.json`). Left unfixed this would let a D201-4
  inner-loop run drop the capture and make a later `--stage api` re-capture **after** production edits,
  destroying the pre-production baseline. The test now falls back to
  `artifacts/pdca/T201/rv1/api/baseline-unconfigured-run.json` whenever the default path already holds a
  captured inventory. Evidence: same bare filtered command, exit 0 (3/3) — RED (pre-fix) dropped the
  `capture` block; GREEN (post-fix) leaves `api/baseline.json` byte-identical
  (sha256 `3a85c92d091c5416…`) and writes the side artefact instead. The artifact records its own tree
  identity (`capture.tree.head` / `head_tree` / status digest / per-assembly DLL sha256) and
  `pre_production: true`; no `src/**` edit exists before or after the capture.
- (e) No invariant proved un-implementable; no blocking DO→PLAN candidate. Items (c), (d) and (f) are
  recorded as explicit assumptions/risks for D201-3..D201-5 and for CHECK adjudication.

- Remaining obligation: candidate measurement, correctness/provider/branch suites, coverage, API diff and
  the sixteen-row manifest stay with D201-4/D201-5. D201-2 closes only its instrumentation,
  negative-validation and baseline portions.

## DO — D201-3 (specialized top-level preparation reuse) — STOP before production edit

- Unit boundary: D201-3 is the only unit authorized to edit production `src/**`. Outcome: **STOP before
  any production edit — DO→PLAN candidate (`plan contract gap`)**. The three authorized `src` files are
  byte-identical (`git status --porcelain` and `git diff --stat` for them are empty) and the tree is green.
  D201-4 is **not** unblocked. **No `D:`-unit completion is claimed**; D201-3 stays `blocked`.
- Cycle N=1, plan revision **r=2**, attempt **n=1/3**, contract **rv1** — unchanged. Single group, current
  tree, no worktree/branch/merge. Dependencies D201-1 (`bff40da2`) and D201-2 (`da9b8100`) satisfied.
- Preflight (mandatory test scope validated **before** any edit; `validate_inner_loop.py brief` exit 0):
  - `dotnet build tests/nextorm.core.tests -c Debug` exit 0, **0 Warning(s) 0 Error(s)**;
  - `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~CachedPreparationRecipeTests`
    exit 0 (**3/3**);
  - `--filter FullyQualifiedName~PlanKeyStructureTests` exit 0 (**19/19**) — **scope amendment**: the given
    `FullyQualifiedName~QueryPlanStore` matches only one xunit method name
    (`PlanKeyStructureTests.QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality`,
    `tests/nextorm.core.tests/PlanKeyStructureTests.cs:291`); the owner/collision/staged-key suite the plan
    assigns to the EC201-13 core portion is the class itself, so the class-level selector is added (a
    narrowing-to-the-real-symbol amendment, not a widening to a whole-project/all-selecting filter);
  - `--filter FullyQualifiedName~InMemoryTests` exit 0 (**183/183**).
- Audit delivered (source-anchored at HEAD; full text in the boundary report §2-§3):
  - writer→consumer→apply map of the complete prepared-state surface (`_selectList`/`_groupingList`/the
    prepared clause arrays, `Prepared*`, all `*PlanHash`, `_whereBasePlanHash`, `InValuesShapeHash`,
    `PreWhereShapeHash`, `ResultPlanHash`, `_isPrepared`, `_from`, `_ctes`, `_referencedQueries`,
    `_outerRefs`, `Resolved*`, `NavigationPaths`, `JsonShape*`) with the single production consumer and the
    writer sites;
  - **the post-apply `QueryPlan` validation cannot substitute for raw verification**: `EqualsCore`
    compares only the prepared forms (`QueryPlanEqualityComparer.cs:129,183,185,196`) and never the raw
    `_exp`/`_condition`/`_groupExp`/`_preWhere`/`_arrayJoins`, so applied owner data makes a *different*
    fresh raw graph still match → wrong-result false hit. A complete raw descriptor is mandatory.
  - the existing expression comparer is value-independent for closures
    (`ExpressionPlanEqualityComparer.cs:444-447`), while `ParamRefreshRecipe` rebinds from the **current**
    command's `PreparedCondition` (`ParamRefreshRecipe.cs:110-121,184-207`) → a copied condition yields
    stale parameters (the design's "changed closure values" negative).
- DO→PLAN candidate — three mechanism-defining decisions the approved design revision 2 does **not** fix
  (each a wrong-result-or-no-benefit fork; resolving them inside D201-3 would invent the mechanism):
  1. the complete raw-descriptor surface: `QueryDefinition` carries 23 raw shape fields but **not**
     `QuoteIdentifiers`/`NamingConvention`/`KeywordCase`/`CommandTimeout`/`EagerLoadMode`/`EagerLoadShape`/
     `HasPerParentEagerTopN`, i.e. exactly the inputs whose `Resolved*` outputs any apply overwrites and
     the key check then compares;
  2. nested-command pairing/apply for the CTE / recursive-CTE / set-operation shapes the five Category B
     scenarios require (`PrepareCtes` reassigns `_ctes` via `CteHoister.Hoist`, so pre- and post-prepare
     declaration shapes differ; `_referencedQueries`/`_outerRefs` are *created* by preparation and cannot
     be paired; a copied `_from` must not silently overwrite an explicitly-set raw one);
  3. the closure/value policy (closure-bearing commands ineligible, vs. path/indices-based substitution
     into the fresh graph).
  **No store-contract invariant failed**: the `QueryPlanStore` raw-index contract (thread-locality, the one
  generation transition, `Clear` leaving no usable alias, single caller) is implementable exactly as D201-1
  recorded; the gap is in the recipe's descriptor/apply contract, above the store.
- Guard/mutation audit (unchanged, re-confirmed): the shared-`AnyCommand` guard must use route/instance
  identity (`discovery/shared-route.md`); **no** `Cache`/`_dontCache` write exists or is needed; the
  bypass/prepared/`storeInCache:false` routes keep their original behaviour. `api/baseline.json` was not
  modified by D201-3 (mtime `17:55:29`, still carries the `capture` block, on-disk sha256
  `a9f60e83d7505e3c…`); the D201-3 preflight bare run wrote only `api/baseline-unconfigured-run.json`
  (no `capture`, same inventory `sha256 c4c217f1b56f3e8e…`), so the D201-2 red→green export fix is
  **re-verified green in D201-3**. Recorded discrepancy for CHECK: the D201-2 progress entry's
  `api/baseline.json` sha256 `3a85c92d091c5416…` does not match the on-disk file (pre-existing D201-2
  record; the file has not changed since the D201-2 capture).
- Boundary evidence: `artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-3.md` and
  `artifacts/pdca/T201/rv1/tests/unit-boundaries/D201-3/**` (brief, report, four preflight logs).
  `validate_inner_loop.py brief` exit 0 and `validate_inner_loop.py report` exit 0.
- Remaining obligation: D201-3's mechanism contract must return to PLAN (acceptance-table rows
  EC201-09/10/11/13/14 core portions are **not** closed by this unit; only the audit portions of
  EC201-10/EC201-14 are delivered). D201-4 stays blocked.

### D201-3 adjudication (DO→PLAN candidate → planner classification (b))

- event: the D201-3 STOP/DO→PLAN candidate was adjudicated by `planner` as **classification (b) —
  acceptable assumption/risk**. The plan is **unchanged**: plan_revision **r=2**, evidence contract
  **rv1**, iteration **n=1** (no attempt reset, no new `P:` task, no escalation, no spike). The three
  flagged items are **implementation obligations**, not a contract gap.
- D201-3 resumed as active immediately; the STOP record above is retained as history.
- Approved design revision 2 was **not** edited; no new evidence-contract rows were added (the added
  obligations were mapped into the existing rv1 rows).

### D201-3 completed-unit boundary

- Outcome: **implemented and green**. Production edits confined to the three authorized files plus the
  authorized core test file; `dotnet build nextorm.slnx -c Debug` **0 Warning(s) 0 Error(s)**; full core
  suite **2562/2562** (2549 + 13 new recipe facts). CRLF preserved on every edited file. No package,
  benchmark, gate, public-API or native-budget change. Integration tests and benchmarks **not** run
  (D201-4/D201-5).
- Mechanism actually implemented: raw candidate index inside the thread-local `QueryPlanStore`, reset under
  the **same** `_cacheGeneration` transition as the normalized dictionary (so `Clear()` leaves no usable
  alias); `QueryCommand.RawShape` = immutable value-free pre-preparation snapshot with a full raw
  structural differential (types/identity, clauses/flags/ordering/limits, the seven raw settings **and
  their effective `override ?? context` forms**, joins/sorting/windows/from/CTE-by-name/union,
  literal-vs-parameter and IN/PreWhere shape) plus a captured-value scan that makes closure/captured-command
  shapes ineligible; capture **before** ordinary preparation; publication only after successful
  preparation + normalized-key construction + cacheable `Set` **and** only for a value-independent plan;
  lookup via fingerprint bucket → raw verification → historical normalized Key still owned by the **same
  holder instance** in the live dictionary → generation recheck; transactional apply through the canonical
  clone copy path validated against the staged normalized key, with full discard (raw `from`/`joins`/`ctes`/
  `union` restored, every staged member cleared) and ordinary preparation on any mismatch; shared
  `AnyCommand` excluded by **actual instance** identity (`QueryCache.AnyCommand` of the applicable context).
  No `Cache=false`/sticky `_dontCache` write exists or is needed; the original fallback, execution branch
  and public API are unchanged; no second owner/abstraction was introduced.
- Proof obligations (observations, CHECK owns the verdict):
  - **RawDescriptorDifferential — met.** `Recipe_RawDescriptor_DistinguishesTheSevenSettingsAndRawClauses`,
    `Recipe_PreparedFormEqualityCannotMaskARawDifference` (the required negative case: prepared-form
    equality does not mask a differing raw input), `Recipe_DifferentNamedSourcesAreDistinguished`,
    `Recipe_SetOperationKindIsPartOfTheRawGraph`.
  - **CurrentGraphBinding — met.** `Recipe_WholeGraphCte_ReusesAndMatchesOrdinaryPreparation` (differential
    vs ordinary preparation), `Recipe_RecursiveCte_ReusesAndMatchesOrdinaryPreparation`,
    `Recipe_ClearDropsTheRawIndex`, `Recipe_SubquerySourceAndForeignNestedCommandsFallBackWithoutEffects`;
    CTE bodies paired by name (never by position), non-flat declaration trees ineligible, an explicit raw
    `from` compared and never silently overwritten, a rejected fast path leaves no partial effect.
  - **FreshClosureValues — met.** `Recipe_ChangedClosureValuesAreFresh_NoStaleCapture` (closure shape
    ineligible → ordinary route → fresh value 10→99 observed; no stale capture; no user expression
    evaluated during staging; no closure over the cache-populating command retained).
  - **All five Category B arm shapes eligible and reusing — met (core-level).**
    `Recipe_AllFiveCategoryBArmShapesAreEligibleAndReuse` asserts eligibility + exactly one publish + one
    reuse + SQL identity per arm (Cte with the entity join, RecursiveCte, Except, Intersect, Distinct); the
    *actual* SQLite benchmark arms execute in D201-5.
- Defects found and fixed red→green inside this unit (real, evidence-backed):
  1. context-default settings leak — `CommandTimeoutTests.ContextsWithDifferentTimeouts_ShouldNotShareCachedPlan`,
     `SqlGenerationTests.QuotedIdentifiers/NamingConvention_PlanCache_ShouldNotLeakBetweenContextDefaults`
     were RED (`tests/nextorm.sqlite.tests`, run exit 2, 5 failed) because the descriptor carried only the
     per-command overrides; fixed by adding the effective `override ?? context` settings → that suite is now
     exit 0, 1749 total, 0 failed, 1 pre-existing skip.
  2. raw `from` identity — `RawSourceBindingDiagnosticsTests.Lifecycle_EachOwningMissEmitsOnce_HitsNeverReplay`
     and `DisabledMiss_ThenEnabledHitAndUncachedCall_DoesNotReplay` were RED; fixed by comparing the raw
     source table/binding explicitly instead of relying on the from comparer alone.
  3. preparation-derived `OneColumn` did not travel with the applied state
     (`QueryPreparationException: Cannot get ctor from System.Int32`); fixed by copying it in the apply.
- Boundary evidence: `artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-3.md` (§8 completion, §1-§7 kept
  as history) and `artifacts/pdca/T201/rv1/tests/unit-boundaries/D201-3/**` (brief, report, per-step and
  boundary logs). `validate_inner_loop.py brief` exit 0 and `validate_inner_loop.py report` exit 0.
- Supplementary (not mandated) verification, all exit 0: `tests/nextorm.sqlite.tests` 1749 (1 pre-existing
  skip), `tests/nextorm.postgres.tests` 978, `tests/nextorm.sqlserver.tests` 970 (8 skipped),
  `tests/nextorm.mysql.tests` 456, `tests/nextorm.clickhouse.tests` 735. Logs under the same directory.
- `api/baseline.json` sha256 — **record correction (confirmed)**: the file is unmodified by D201-3 (mtime
  `2026-10-15 17:55:29`, `capture` block present, on-disk sha256 `a9f60e83d7505e3c…`, inventory sha
  `c4c217f1b56f3e8e…`); the D201-2 progress entry's recorded `3a85c92d091c5416…` is **wrong** for the
  on-disk file. The historical entry is not rewritten; this record is the correction. The baseline file
  itself was not touched; each bare export run in this unit wrote only `api/baseline-unconfigured-run.json`,
  so the D201-2 red→green fix is re-verified green.
- Remaining obligations for downstream units (not closed here): provider/integration behaviour over real
  databases, SQL Server temp-table/TVP branch execution, streaming/disposal/cancellation lifecycle,
  coverage thresholds and the public-API inventory diff → **D201-4**; the actual five Category B arm
  measurements and all parity/allocation decisions → **D201-5**. Performance parity is **not** claimed.
- Next: D201-3 boundary acceptance closes its assigned portion only; D201-4 is unblocked once CHECK accepts
  this boundary. No performance, provider or API row is closed by this unit.

## DO — D201-4 (correctness, providers, branches, quality and API)

- Unit boundary: D201-4 is test-and-evidence only. **No production `src/**` edit** was made (none was
  needed). Tracked writes: `tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs` (new),
  `tests/nextorm.integration.tests/SqlServerCachedPreparationSpecificTests.cs` (new) and this status file.
  `tests/nextorm.core.tests/CachedPreparationRecipeTests.cs` was used unchanged for the candidate export.
  Cycle N=1, plan revision **r=2**, attempt **n=1**, contract **rv1** — unchanged; single group, current tree.
- Preflight (mandatory test scope validated **before** any edit): `validate_inner_loop.py brief` exit 0.
- Builds: `dotnet build tests/nextorm.integration.tests -c Debug` exit 0 (0 Warning(s)/0 Error(s),
  `tests/unit-boundaries/D201-4/inner-build.log`); `dotnet build nextorm.slnx -c Debug` exit 0
  (**0 Warning(s) 0 Error(s)**, `artifacts/pdca/T201/rv1/build.log`).
- Inner loop: `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter FullyQualifiedName~CachedPreparation`
  exit 0, **48 selected** (4 `CommonTestSuite` providers x 11 shared + 4 SQL Server-specific),
  11 passed / 0 failed / 37 skipped without Docker (SQLite only) — `inner-cachedpreparation.log`.
- Boundary suites (each exit 0): core `dotnet test tests/nextorm.core.tests -c Debug --no-build`
  **2562/2562** (0 failed, 0 skipped); container integration
  `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor`
  **3978 total / 0 failed / 0 errors / 198 capability skips / 0 provider skips**, with **0** container-start
  failures -> PostgreSQL, SQL Server, MySQL and ClickHouse all executed; supplementary dialect sweep
  `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` **1749** (1748 pass, 1 pre-existing opt-in skip).
- All **48** `CachedPreparation` tests ran (0 skips): 11 shared per provider for SQLite/PostgreSQL/SQL Server/MySQL
  (publish+reuse, raw-graph non-aliasing, results equivalence, streaming/non-streaming, cancellation,
  partial enumeration + disposal, later cache hit after the shared terminal route, and the whole-graph
  CTE/recursive-CTE/EXCEPT+INTERSECT/Distinct differentials), plus 4 SQL Server-only cases (lazy temp-source
  guard, `#temp` read on the ordinary path, TVP binding never entering the recipe, TVP metadata
  auto/configured-precedence/`Clear`). The SQL Server temp-source case observed the guard (`CaptureRawShape`
  null, `NotSupportedException` from the ordinary render, unchanged plan-holder count).
- Coverage (native repository workflow, once, CI-faithful): `dotnet-coverage collect -s coverage.settings.xml
  -f cobertura -o artifacts/pdca/T201/rv1/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
  exit 0; `reportgenerator` exit 0 -> **Line 87.5%** (threshold 85), **Branch 79.2% (29785/37571)** (threshold 75),
  Method 78.7% — **warning-only on `1.0.9-rc3`**, both met. Recipe files: `QueryPlanStore.cs` 97.2/93.8,
  `QueryPlanner.cs` 96.9/90.9, `QueryCommand.QueryPreparer.cs` (QueryCommand) 96.5/82.5, `RawShape` 76.6/51.7.
  **Six touched-branch families have no directly targeted test** and are enumerated in `reports/quality.md`
  (`QueryPlanStore.cs:141`/`:145`, `QueryPlanner.cs:673-674`, per-field `RawShape.Matches` arms, the
  `CapturedValueScan` rejection arm, debug-only log/`Debug.Fail`).
- API: candidate public-surface export via the D201-2 in-test mechanism exit 0, 16/16 ->
  `api/candidate.json` (sha256 `9d2e3e35...`, `pre_production: false`) against `api/baseline.json`
  (sha256 `a9f60e83...`, `pre_production: true`, `head da9b8100`); `api/diff.json` **equal = true**
  (added/removed/changed = 0) -> identical public surface, `Prepare()` unchanged, no new required public
  preparation. Recorded evidence-tooling observation: `t201_evidence.py --stage api --phase candidate` was
  not invoked directly — its candidate capture block hard-codes `pre_production: true` /
  `source_revision: T201-D201-2-r2rv1`, and `--phase auto` emits a diff only once the benchmark `candidate/`
  directory exists (D201-5); the required result was produced with the same in-test export plus the wrapper's
  `api_diff`. This does not affect the measured verdict.
- Reports: `reports/{correctness,shared-command,temp-tvp,cache-contract,recipe-consumers,quality}.md`,
  `reports/unit-boundaries/D201-4.md`; evidence bundle `tests/unit-boundaries/D201-4/{brief,report}.json`
  with `validate_inner_loop.py brief` exit 0 and `report` exit 0.
- Boundary observations (CHECK owns the verdict): EC201-09/10/13/14, EC201-11, EC201-12 and EC201-15 each have
  observed evidence as tabulated in `reports/unit-boundaries/D201-4.md`. Open for CHECK: the
  MariaDB/ClickHouse recipe-differential coverage gap (neither derives from `CommonTestSuite`, so the shared
  differential does not run on them) and the six untargeted branch families.
- Remaining obligation: **D201-5** — the five Category B arm measurements, all parity/allocation decisions,
  the unchanged native iteration14 gate (28 rows x 2 toolchains) and the sixteen-row DO->CHECK manifest.
  Performance parity is **not** claimed by D201-4.

## Progress log

- 2026-10-11, history: owner confirmed trigger and authorized conversational
  coordination; earlier external prerequisite was resolved.
- 2026-10-11, history: r1 candidate written and separately reviewed; PLAN-review
  FAIL identified oracle, owner/consumer and completeness defects.
- 2026-10-11, history: design revision 2 and plan r=2 proposed; phantom nested
  owner removed and raw index/live owner/state contract made explicit.
- 2026-10-11: written design revision 2 passed the first ordered separate review.
  The approved design is unchanged.
- 2026-10-11: second ordered detailed-plan/mode/rv1 review returned FAIL for
  bounded unit-boundary acceptance and output-footprint corrections.
- 2026-10-11: P:T201-close-unit-contracts-and-footprints supplies explicit
  D201-2..5 acceptance/negative contracts, reconciled evidence footprints and
  P201-READY collection ownership; r=2 and rv1 retained.
- Pending: new detailed-plan/mode/rv1 approval.
- Pending after approval: P201-READY persistence, freeze digest, collection
  reconciliation and readiness.
- No DO iteration, deciding probe exit or product CHECK success is asserted.
- 2026-10-11, history: prior `plan_state=gap` recorded the design prerequisite
  as an unmet external approval gate (mandatory separately-reviewed design
  missing, and the M12 #3 trigger unverified).
- 2026-10-11: owner authorization recorded and the M12 #3 trigger confirmed
  (`docs/specs/performance/iteration-15-cached-path-results.md:47`).
- 2026-10-11, history: the r=1 PLAN candidate was reviewed and rejected by PLAN
  review; it is retained as history, not a completed execution contract.
- 2026-10-11: revised written design revision 2 and the r=2 plan (sequential
  execution mode, rv1 evidence contract) authored.
- 2026-10-11: Review #1 — written-design approval, design revision 2 — PASS by
  a separate non-author lens.
- 2026-10-11: Review #2 — detailed r=2 plan / sequential execution mode / rv1 —
  PASS.
- 2026-10-11: both ordered approvals obtained; `plan_state` set to ready and
  rv1 frozen before first DO.
- Next allowed step: DO — starts D201-1 at n=1; not started.
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | DO started: D201-1 discovery (ownership/state/inventory) | artifacts/pdca/T201/rv1/discovery/inner-loop/report.json
- 2026-10-15, DO D201-1 (n=1, r=2, rv1): discovery unit delivered — field→producer→consumer
  ownership map, shared `AnyCommand` route/mutation audit, M9/test/coverage/container inventory,
  upstream sibling outcomes and tested baseline identity, and the source-backed oracle inventories;
  inner-loop brief/report validators exit 0; no production `.cs` change; D201-2 is next.
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-2 evidence runner + W-S negative self-tests + baseline captured | artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-2.md
- 2026-10-15, DO D201-2 (n=1, r=2, rv1): `eng/perf/t201_evidence.py` (10 stages, exits 0/1/2), W-S
  46/46 self-test cases distinguished (exit 0), `CachedPreparationRecipeTests.cs` public-surface export
  (298 public types / 4 assemblies, pre-production tree identity recorded in the artifact `capture`
  block; no `src/**` edit before or after the capture), baseline B-BASE 779.940 s /
  A-BASE 130.320 s / G-LIVE 779.971 s / G-REPORT 0.049 s all exit 0 (`dotnet build -c Release` 2.731 s),
  55 arms + 7 acceptance cases + 56 gate row/jobs reconciled (`oracle/reconciliation.json`), historical
  `benchmarks/BenchmarkDotNet.Artifacts` restored exactly (302 files, 0 differing; git status 18 entries),
  inner loop exit 0 (3/3) and boundary `dotnet build nextorm.slnx -c Debug` 0 Warning(s)/0 Error(s) with
  core 2549/2549; W-P exit 2 inconclusive by contract (no candidate round); `validate_inner_loop.py`
  brief/report exit 0; no production `src/**` edit. Next: D201-3 (specialized top-level preparation reuse).
- 2026-10-15, DO D201-2 boundary fix (r=2, rv1, n=1 — no replan, no new attempt): a bare inner-loop run
  of the export test (no `T201_API_EXPORT_PATH`) overwrote the captured `api/baseline.json` and dropped
  its `capture` block, which would allow a post-edit re-capture in D201-4. Fixed red→green in the test
  file: bare runs now write `api/baseline-unconfigured-run.json` when the default path already holds a
  captured inventory. Same bare command exit 0 (3/3): RED dropped the capture, GREEN preserves
  `api/baseline.json` byte-identically (sha256 `3a85c92d091c5416…`). No production `src/**` change; the
  baseline stage, the native gate and the committed registry commands are unaffected.

- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-3: mandatory test scope validated BEFORE any edit (`validate_inner_loop.py brief` exit 0); preflight green on the unchanged tree (`dotnet build tests/nextorm.core.tests -c Debug` exit 0 0W/0E; filtered runs exit 0 with 3/19/183 tests selected, 0 failed) | artifacts/pdca/T201/rv1/tests/unit-boundaries/D201-3/preflight-build.log
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-3 STOP before production edit: DO→PLAN candidate (`plan contract gap`) — the audit proves the post-apply `QueryPlan` validation compares prepared forms only (QueryPlanEqualityComparer.cs:129,183,185,196) so a complete raw descriptor is mandatory, and the descriptor surface / nested-command apply for CTE+union shapes / closure-value policy are not fixed by design revision 2; no store invariant failed; the three authorized `src` files are byte-identical (git status + git diff --stat empty); no `D:` unit completion claimed — D201-3 stays blocked, D201-4 not unblocked | artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-3.md

- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-3 adjudicated: DO→PLAN candidate classified (b) acceptable assumption/risk by `planner`; plan unchanged (r=2/rv1/n=1, no reset, no new P task, no escalation); D201-3 resumed as active | artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-3.md
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-3 completed: raw candidate index + raw descriptor + live-owner/generation validation + transactional apply implemented in the three authorized src files; `dotnet build nextorm.slnx -c Debug` 0W/0E; core suite 2562/2562; recipe tests 16/16; supplementary dialect suites sqlite 1749 / postgres 978 / sqlserver 970 / mysql 456 / clickhouse 735 all exit 0; 3 real defects found and fixed red→green (context-default settings leak, raw-from identity, prepared OneColumn); `validate_inner_loop.py` brief/report exit 0 | artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-3.md
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-3 record correction (confirmed): `api/baseline.json` on-disk sha256 is `a9f60e83d7505e3c…` (file unmodified since 17:55:29, `capture` block present); the D201-2 progress entry's recorded `3a85c92d091c5416…` is wrong; baseline file untouched, bare export runs wrote only `api/baseline-unconfigured-run.json` | artifacts/pdca/T201/rv1/api/baseline.json
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-4 scope validated before any edit (`validate_inner_loop.py brief` exit 0); inner build `dotnet build tests/nextorm.integration.tests -c Debug` exit 0 0W/0E | artifacts/pdca/T201/rv1/tests/unit-boundaries/D201-4/inner-build.log
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-4 inner loop: filtered integration run exit 0 — 48 selected / 11 passed / 0 failed / 37 skipped (no Docker) | artifacts/pdca/T201/rv1/tests/unit-boundaries/D201-4/inner-cachedpreparation.log
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-4 boundary: solution build 0W/0E; core 2562/2562; container integration 3978 total / 0 failed / 0 provider skips (all 48 CachedPreparation executed); supplementary sqlite dialect sweep 1749 (1 pre-existing opt-in skip) | artifacts/pdca/T201/rv1/tests/unit-boundaries/D201-4/boundary-integration.log
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-4 coverage once: line 87.5% >= 85, branch 79.2% (29785/37571) >= 75 — warning-only on 1.0.9-rc3; 6 recipe branch families without a directly targeted test enumerated | artifacts/pdca/T201/rv1/coverage/report/Summary.txt
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-4 API: candidate (16/16 export, post-edit) vs baseline diff equal=true (added/removed/changed=0) — identical public surface | artifacts/pdca/T201/rv1/api/diff.json
- 2026-10-15 | DO | revision r=2 | iteration n=1/3 | D201-4 unit boundary reported as an observation only (satisfaction is CHECK's verdict; D201-5 remains); `validate_inner_loop.py` brief/report exit 0 | artifacts/pdca/T201/rv1/reports/unit-boundaries/D201-4.md
