# D201 — cached-path parity

- task: T201
- issue: #201
- milestone: 1.0.9
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=2
- phase: P
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
