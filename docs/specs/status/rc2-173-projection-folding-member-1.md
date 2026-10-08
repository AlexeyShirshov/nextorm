# D173 — Mapping: projection folding ignores Member

- collection: `1.0.9-rc2`
- task_id: `D173` (issue `#173`)
- selected_variant: `pdca-dotnet`
- cycle: `N=1`; plan revision: `r=1`; evidence contract: `rv1`
- phase: **done** — CHECK PASS r1/rv1/n2; ACT complete; product commit `29e794fd`
- base: branch `1.0.9-rc2` @ `18659e41`; collection status: `docs/specs/status/collection-1.0.9-rc2.md` (not modified here)
- issue URL: https://github.com/AlexeyShirshov/nextorm/issues/173

## 1. Goal

Remove `ProjectionEntityItem.Member` as an omitted discriminator from **both** select-plan
comparison and row-mapper signatures, without changing how projections are constructed or
materialized.

**Done:** member-sensitive regression tests fail on the predecessor implementation and pass after
the change; actual plan-cache keys separate the affected shapes; existing null-member and tuple
projections remain compatible; required build, coverage, and cached-path performance evidence is
recorded. This fixes the omission in #173 — not every possible collision in an integer hash.

## 2. Scope

### Chosen: fix now — include Member in both key paths

| Alternative | Benefits | Cost / risk | Decision |
|---|---|---|---|
| Pin `Member == Item{slot+1}` with guards/assertions only | Small change; documents current producers | Leaves the omission intact; could reject legitimate future named bindings; null ctor positions need exceptions | Reject |
| Include `Member` in comparer equality/hash and mapper signature | Directly fixes the missing discriminator; tolerates existing null and future named bindings | Small extra comparison/hash work on cache-key paths; perf evidence required | **Choose** |
| Replace mapper signature with structural, collision-resolving identity | Solves broader integer-signature limitation | Larger cache-contract change; unnecessary for this bounded omission | Out of scope |

Fix-now edit list:
1. `src/nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs` — `Equals` projection comparison at **:71–78** include `Member`; `GetHashCode` projection contribution at **:116** include `Member`.
2. `src/nextorm.core/DataContext/RowMapperFactory.cs` — `BuildSignature` **:456–481**, projection contribution **:475** include member-identity hashing.
3. Regression, compatibility, and real cache-key separation tests.
4. Remove resolved **Latent P2** item at `docs/specs/roadmap/todo_navigation_properties.md:344–348` at ACT, after evidence passes.

**Identity policy:** `PropertyInfo` equality (not name alone, not imposed reference identity). Null compares equal to null and has a defined null hash. Equal members must yield equal hashes.

Guards / exclusions:
- Guard producer behavior with existing tuple-identity tests (extend only if needed): `QueryCommand.QueryPreparer.cs:825,839,900,937`, `JoinedReturningProjection.cs:104–105`, `JoinReturningIdentityTests.cs:92`.
- Do **not** change producers, `RowMaterializerBuilder.cs:177`, or `BuildSignature`’s signature.
- Do not claim unequal objects must universally have unequal integer hashes; require different hashes for selected regression fixtures and equality separation at the plan-cache seam.
- No currently required slice is deferred. If reconnaissance exposes a separate concrete cache-collision defect beyond the missing discriminator, create a separate active unit/issue in **1.0.9-rc2** (milestone invariant 8); documentation alone does not discharge it.

## 3. Acceptance criteria

| ID | Observable acceptance | Negative case |
|---|---|---|
| **R173-01** | Two select expressions identical except for distinct non-null projection members, equal `EntityType`+`Slot`, compare unequal; selected fixture also hashes differently. | Before fix they compare equal / hash alike; a mutant omitting member comparison is detected. |
| **R173-02** | `BuildSignature` and resulting mapper keys distinguish the same member-only variation, all other inputs and `TResult` constant. | Predecessor signature/key aliases the fixture; omitting the member hash fails the regression test. |
| **R173-03** | Equal projection shapes (incl. equal member identity and null/null) compare equal and hash alike; equivalent mapper inputs give equal keys. | Reference-only identity, inconsistent equality/hash, or null/null unequal fails tests. |
| **R173-04** | Null/non-null members, different entity types, different slots, scalar/entity shapes follow the variant matrix; constructor-parameter projections remain usable. | Null deref, scalar/entity aliasing, or rejection of a legitimate null-member ctor position fails tests. |
| **R173-05** | Actual `QueryPlan`/`QueryPlanStore` keys separate a member-only variation with SQL and context type held equal; two stored values retrieved independently; equal-shape control retains reuse. | Comparer-only test is insufficient; an aliasing key/store test fails. |
| **R173-06** | Existing tuple binding, mapper, cached-path behavior passes affected suites + boundary sweep; build 0 warnings/0 errors. | Skipped/unselected tests, or passing only after broad warning suppression, do not satisfy. |
| **R173-07** | All seven cached-path acceptance benchmark cases execute and meet the acceptance spec incl. cached/prepared ratios. | Missing cases, different benchmark settings baseline/candidate, or violated limits fail. |
| **R173-08** | Resolved latent item removed; issue/register disposition identifies #173 + verified URL; production changes stay in bounded scope. | Leaving the completed item as deferred, deleting unrelated backlog text, or changing public API/cache architecture fails scope review. |
| **R173-09** | Coverage and targeted mutation evidence show the member-related changes are exercised and omission regressions detectable. | A surviving member-omission mutant, unexplained uncovered changed branch, or missing coverage report is insufficient. |

Red ↔ green: add member-only regression tests **before** the production change; capture failures
separately for comparer equality, comparer hash, and mapper signature/key — assertion failures
attributable to the omission, not build/setup failures. For R173-05, extend
`tests/nextorm.sqlite.tests/PlanKeyUniquenessTests.cs` at the existing cache-key seam with a
test-only shape differing only in `ProjectionItem.Member`.

## 4. Variant matrix (from execution path)

Every row closed as **test** or **guard**; none deferred.

| Variant | Comparer equality/hash | Mapper signature/key | Closure |
|---|---|---|---|
| Same type/slot, null/null member | Equal; equal hashes | Equal keys | **Test** |
| Same type/slot, null/non-null member (both argument orders) | Unequal; selected fixture hashes differ | Selected fixture keys differ | **Test** |
| Same type/slot, equal non-null member identity | Equal; equal hashes | Equal keys | **Test** (incl. repeated reflection lookup) |
| Same type/slot, different non-null members | Unequal; selected fixture hashes differ | Selected fixture keys differ | **Test** (primary regression) |
| Same member name on different declaring types | Not conflated by name | Identity distinction retained | **Test** |
| Same slot, different `EntityType` | Unequal; existing discriminator retained | Retained | **Test** |
| Same type, different slot | Unequal; existing discriminator retained | Retained | **Test** |
| Scalar column vs entity item | Unequal; no member deref for scalar | Different shape keys | **Test** |
| Both scalar columns, otherwise equal | Unchanged | Unchanged | **Test** |
| Ctor-parameter position, `Member == null` | Supported | Supported; materializer ctor test green | **Test** |
| Production `Item1`/`Item2` tuple bindings | Binding identity preserved | Cached behavior preserved | **Guard + existing tests** |
| Value-type vs reference-type mapped result/entity | No new type-dependent policy | Same identity rule | **Test** (test-local fixtures) |
| Existing streaming/non-streaming flag | Not a new comparer dimension | Flag distinction retained | **Guard + `RowMapperFactoryStreamingKeyTests`** |
| Other select metadata, converters, defaults, duration/provider type | Existing discriminators untouched | Untouched | **Guard** (existing tests + diff review) |
| SQL providers | Member identity is provider-neutral | Shared key path exercised by provider suite | **Test** via common integration boundary |

Arbitrary non-`ItemN` members are deliberately test-only future-shape fixtures, not a claim that
current query preparation produces them.

## 5. Footprint (+ uncertainty)

Planned:
- `src/nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs:71–78,116`
- `src/nextorm.core/DataContext/RowMapperFactory.cs:456–481`
- `tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs:38–68`
- `tests/nextorm.core.tests/RowMapperFactoryStreamingKeyTests.cs:14`
- `tests/nextorm.sqlite.tests/PlanKeyUniquenessTests.cs:27`
- `docs/specs/roadmap/todo_navigation_properties.md:344–348` (ACT)
- this D173 status/evidence record

Conditional/bounded: `RowMaterializerBuilderTests.cs:65,113,177,206`; `JoinReturningIdentityTests.cs:92`;
`CachedPathCharacterizationTests.cs:21`; `docs/specs/design/API-NAMING-REVIEW.md:71,559,3142–3154` (read
for overlap; edit only if a tracked obligation actually changes); an existing Russian internal
mirror if found. No project/package, public-doc, public-API, or new production type.

Explicit uncertainty:
1. Private mapper-key test access pattern not yet established (existing internal/test access preferred; localized test-only reflection acceptable; no production API expansion).
2. Exact `QueryPlan`/`QueryPlanStore` fixture constructors/methods to be resolved via Roslyn; R173-05 is mandatory regardless.
3. Selected distinct members must have distinct member hashes in the test environment (record fixture precondition; never skip the test if false).
4. Collection overlap with #148-B/#160 unverified.
5. Exact global evidence-contract text and perf acceptance limits not in the pack (fail-closed prerequisite D173.1).

## 6. Predecessor-result requirements

Base: branch `1.0.9-rc2`, commit `18659e41`. Before DO: record actual branch/HEAD/status and base→worktree
delta; record accepted predecessor revision/results if integrated (preserve its changes); obtain
predecessor summaries for **#148-B** and **#160** or an explicit scheduling record they are not
predecessors; compare footprints against both production files, key contracts, affected tests; require
CHECK-accepted output for any overlapping prerequisite; if target signatures/cache architecture changed,
return to PLAN before implementing against stale assumptions. Do not reset to `18659e41` or revert
unrelated collection changes.

## 7. Assumptions / prerequisites

| Assumption / prerequisite | Verification / blocker rule |
|---|---|
| Issue stays #173, milestone 1.0.9-rc2, no approved sibling design supersedes scope | Verify issue metadata + collection manifest; conflict → PLAN |
| `Member` stays `PropertyInfo?`; producers enforce tuple binding | Roslyn members/refs + producer inspection; conflict → PLAN |
| Null identifies a supported ctor-parameter position | Materializer test :206 + ctor doc; failure blocks completion |
| `PropertyInfo` equality/hash is the right identity policy | Equal-lookup and distinct declaring-type/member fixtures; custom reflection behavior needs a focused experiment |
| Shared key behavior testable without new public API | Inspect existing test access/seams; inability is a test prerequisite, not a reason to omit R173-02/05 |
| Toolchain/integration facilities available | Verify .NET 10, tool manifest, integration skill; documented recovery before classifying an infra blocker |
| Full mandatory skill contract available before sealing | D173.1 scout supplies the `pdca-dotnet` versioned evidence contract, collection/nextorm gates, CI coverage commands, perf limits |
| Baseline comparable | Record revision, runtime, machine, config, benchmark settings |

Contract sealing: DO must attach authoritative skill/spec excerpts checked against §13 before
implementing. Additional unconditional obligations are added, not waived.

## 8. Test strategy

Focused:
- **C-CORE** `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~SelectExpressionPlanEqualityComparerTests|FullyQualifiedName~RowMapperFactoryStreamingKeyTests|FullyQualifiedName~RowMaterializerBuilderTests|FullyQualifiedName~JoinReturningIdentityTests"`
- **C-SQLITE** `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~PlanKeyUniquenessTests|FullyQualifiedName~CachedPathCharacterizationTests"`
- Zero-selection run = failure.

Build/rebuild: **C-BUILD** `dotnet build nextorm.slnx -c Debug` after every edited step (no `--no-build`
to hide stale binaries); zero warnings/errors; CRLF preserved; no new blanket suppression.

Integration: **required at boundary** (shared runtime cache-key change). Load
`.opencode/skills/running-integration-tests/SKILL.md`, then **C-INTEGRATION**
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`
with documented socket recovery; PostgreSQL/SQL Server/MySQL/ClickHouse/SQLite must be visible;
container-provider skips are not green.

Single boundary sweep (after focused tests + mutations, production restored): C-BUILD → full unit
projects `nextorm.core.tests`, `nextorm.sqlite.tests`, `nextorm.postgres.tests`,
`nextorm.sqlserver.tests` → C-INTEGRATION → coverage → acceptance benchmark → final diff/scope/register
review.

Coverage: scope `nextorm.{core,sqlite,postgres,sqlserver}`; record **85% line / 75% branch** thresholds
and actuals (hard-fail only on `main`; on `1.0.9-rc2` keep the project warning policy, do not invent a
new global gate); require changed member-related lines/branches exercised; no unexplained regression vs
a comparable baseline.

Mutation (targeted, manual): (1) omit member equality; (2) omit comparer member hash; (3) omit mapper
member hash; (4) break null/equal-member behavior. Each omission mutant must compile and fail the
relevant regression assertions (build failure ≠ killed mutant); restore and rerun focused tests after.
No Stryker dependency is added for this slice.

CHECK priority: R173-01..05 and their null/identity/key/store/compat assertions are **P1**; R173-06/07/09
**P1**; R173-08 + register/scope hygiene = completion gate; generic integer-hash collision limitation is
pre-existing architectural risk and cannot be used to downgrade P1 (the latent-P2 label does not downgrade).

## 9. Docs plan

Remove only the completed #173 latent item from `todo_navigation_properties.md:344–348` at ACT after
successful CHECK; record #173 + verified URL in the collection status; synchronize an existing Russian
internal mirror if found; review #148-B register refs for overlap without marking unrelated findings
resolved; **no public EN/RU docs change**; no public links to `docs/specs`; comments near hashing (if
any) explain member-sensitive identity, not that only `ItemN` is permissible.

## 10. Perf-measurement decision — REQUIRED

`SelectExpressionPlanEqualityComparer.cs:41,90` participates in plan-key equality/hash;
`RowMapperFactory.cs:219 → :395–404 → :456–481` computes mapper keys — both within nextorm’s mandatory
query-path/cache/materialization perf rule. **C-PERF**
`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
run baseline before source changes and candidate after final implementation, same machine/runtime/
settings; retain all seven cases, cached/prepared ratios, raw output + BenchmarkDotNet artifacts; use
limits from `docs/specs/performance/acceptance-benchmarks.md` (exact limits = D173.1 prerequisite; do
not invent). Budget ≤4 min; timeout/incomplete cases = fail.

## 11. Reconnaissance decision — REQUIRED (narrow, no design prototype)

D173.1 scout must establish: (1) authoritative skill/overlay evidence obligations + perf limits;
(2) private signature/key and plan-store test seams via Roslyn; (3) #148-B/#160 predecessor footprints +
accepted results; (4) CI coverage tool invocations + available baselines; (5) internal mirror/register
status. Observable success: facts-only report with source/spec anchors, actual callable test seams,
exact benchmark limits, predecessor dispositions, and a demonstration that R173-02/05 are testable
without production API expansion. If custom reflection behavior is found, allow one focused identity
experiment (equal member resolutions compare/hash consistently; distinct bindings stay distinct);
failure → PLAN.

## 12. Unit execution mode — one unit, one tree, sequential

- **D173.1** prerequisite/scout closure (seal rv1)
- **D173.2** red tests (focused fixtures, capture predecessor failures)
- **D173.3** minimal production fix (member equality + both member hashes)
- **D173.4** green + compatibility tests (incl. real cache-store separation)
- **D173.5** targeted mutation + boundary sweep + coverage + perf evidence
- **D173.6** ACT hygiene (remove resolved latent item; record disposition only after successful CHECK)

Serialize against #148-B/#160 whenever files or cache contracts overlap; no concurrent edits to the two
production files; keep test fixtures local.

## 13. Evidence contract rv1

- Plan `r1`; contract `rv1` (initial, no supersession); requirement IDs R173-01..R173-09; artifact root
  **`/tmp/nextorm-D173-r1/`** (persist a durable copy with the collection record before completion).
- CHECK re-gather budget: at most **two targeted requests per CHECK attempt**, owned by `check`, routed
  through the collection orchestrator; exhaustion leaves the criterion unproven (absence alone does not
  authorize DO or a contract revision).

Commands: C-PRE (`git branch --show-current`, `git rev-parse HEAD`, `git status --short`,
`git diff --name-status 18659e41`, `git diff --stat 18659e41`); C-SCOUT (orchestrator `scout` Task:
evidence contract + gates + perf limits + Roslyn test seams + #148-B/#160 + mirror/register);
C-SWEEP `/tmp/nextorm-D173-r1/run-boundary.sh` (C-BUILD, four unit projects, C-INTEGRATION; preserve
per-command exit codes, fail on any failure) wrapped by `dotnet-coverage collect ... --settings
coverage.settings.xml --output-format cobertura --output /tmp/nextorm-D173-r1/coverage.cobertura.xml`
then `reportgenerator ...`; C-MUTATION (coder Task: four targeted mutations serially, patch + compile +
assertion failure + exit code each, restore, rebuild, rerun focused); C-DIFF (`git diff --check` +
`git diff --name-status 18659e41` + scoped diff). D173.1 verifies tool syntax/options; collection scope
and thresholds cannot be weakened.

Mandatory rows (id / requirement / scenario / evidence / required result / artifact / owner):
- **E173-00** all / prerequisites + contract sealing / C-PRE + C-SCOUT / facts report, accepted predecessor state, all mandatory slots reconciled / `predecessor.txt`, `scout.txt`, `contract-rv1` / scout; planner approves sealing
- **E173-01** R173-01,02 / predecessor red / C-CORE before fix / nonzero from expected member assertions, not build/setup, no skipped regression / `red-core.log`, `red-test-diff.patch` / coder
- **E173-02** R173-01,03,04 / comparer variants / C-CORE final / exit 0, nonzero selected, all matrix assertions pass / `green-core.log`, `variant-map.txt` / coder, CHECK verifies
- **E173-03** R173-02,03,04 / direct signature + mapper key / C-CORE final / exit 0, both seams tested, non-member inputs constant / `green-core.log`, `mapper-key-evidence.txt` / coder, CHECK verifies
- **E173-04** R173-05 / real plan/store separation + equal control / C-SQLITE / exit 0, executed separation assertions, independent retrieval / `green-sqlite.log`, `plan-store-evidence.txt` / coder, CHECK verifies
- **E173-05** R173-04,06 / ctor, tuple, streaming controls / C-CORE + C-SQLITE / exit 0, no unexpected skips / focused logs + `variant-map.txt` / coder
- **E173-06** R173-06 / final build + boundary units / C-SWEEP / exit 0, 0 warnings/errors, each project runs / `boundary.log`, `boundary-results.txt` / coder, CHECK verifies
- **E173-07** R173-06 / container/provider boundary / C-INTEGRATION / exit 0, required providers executed, none silently skipped / `integration.log`, `provider-counts.txt` / coder, CHECK verifies
- **E173-08** R173-09 / coverage + branch delta / coverage commands / both exit 0, thresholds reported, no unexplained delta/uncovered changed branch / `coverage.cobertura.xml`, `coverage-report/`, `coverage-delta.txt` / coder, CHECK verifies
- **E173-09** R173-01,02,03,09 / targeted mutants / C-MUTATION / four valid mutants detected; final C-CORE/C-SQLITE exit 0 / `mutation/`, `mutation-summary.txt`, `restored-diff.patch` / coder, CHECK verifies
- **E173-10** R173-07 / comparable baseline / C-PERF before edits / exit 0, seven cases, spec-compatible / `perf-baseline/`, `perf-environment.txt` / coder
- **E173-11** R173-07 / candidate acceptance / C-PERF final / exit 0, seven cases, all limits satisfied / `perf-candidate/`, `perf-comparison.txt` / coder, CHECK verifies
- **E173-12** R173-08 / scope/docs hygiene / C-DIFF + issue metadata / diff check exit 0, bounded footprint, resolved entry removed, correct URL/milestone / `final-diff.patch`, `scope-review.txt`, `tracking.txt` / coder, CHECK verifies
- **E173-13** R173-06,08,09 / suppression/slop accounting / C-DIFF + CHECK review / 0 new warning suppressions, test skips, excluded changed lines, weakened assertions, or acceptance-limit adjustments / `slop-counters.txt` / CHECK

Hash caveat is not slop: the contract promises selected fixtures differ, not universal integer-hash
uniqueness. A justified revision keeps these IDs/obligations, marks the superseded rv, and adds IDs
for new variants.

## 14. Risks

| Risk | Mitigation / trigger |
|---|---|
| Integer hashes can still collide | Bounded guarantee + equality/store/mapper checks; separate observed defect → same-milestone unit |
| Member identity as name/reference only | Equal-resolution + same-name/different-declaring-type tests; identity mutant |
| Null ctor positions regress | Null/null, null/non-null + materializer ctor tests are P1 |
| Cache test passes for unrelated query differences | Hold SQL/context/result type/all other shape fields constant; record fixture delta |
| #148-B/#160 overwrite/invalidate | Predecessor evidence + serialized footprint scheduling |
| Private test seams cause API expansion | Existing access first, localized test reflection second; architecture change → PLAN |
| Added hash work regresses cached path | Mandatory comparable 7-case perf evidence |
| Integration infra unavailable | Integration-skill recovery; persistent resource failure → planner, never marked green |
| Mutation contaminates final sources | Serialized edits, restoration diff, rebuild, final focused rerun |
| Missing global contract/spec evidence | Fail-closed sealing prerequisite; targeted scout first, no invented thresholds |
| CRLF/warnings/scope drift | Final diff/format review + zero new suppression/slop counters |

Confidence: high in the defect and minimal fix from the evidence; test-seam, predecessor, and
authoritative skill/spec details remain prerequisite facts (D173.1), not assumed results.

## Handoff

- `plan_state=ready`; stopped at PLAN→DO. No DO, no code, no branches, no commits.
- Predecessors: #148-B, #160 (predecessor-result requirements §6).
- Prerequisites: D173.1 reconnaissance/contract sealing (§11, fail-closed).

## DO progress

- 2026-10-08T13:11:14Z | DO | revision r1 | iteration 1/3 | DO started; uncommitted D173 changes recovered from `/tmp/nextorm-D173-r1/fix.patch` + `red-test-diff.patch` (`git apply --check` ok, applied; CRLF normalized; 5 files, 239 insertions(+), 0 deletions(-); production files match `boundary2/mutbak/original.md5`) | `/tmp/nextorm-D173-r1/restore/`
- red→green: predecessor red captured on the same fixtures; after fix both `nextorm.core.tests` selectors and `nextorm.sqlite.tests` selector pass (exit 0).
- C-BUILD Debug: 0 Warning(s) / 0 Error(s) (`/tmp/nextorm-D173-r1/restore/build-debug.log`); Release 0/0.
- focused green: `SelectExpressionPlanEqualityComparerTests` exit 0 (succeeded 9, failed 0, skipped 0); `RowMapperFactoryStreamingKeyTests` exit 0 (succeeded 9, failed 0, skipped 0); `PlanKeyUniquenessTests` exit 0 (succeeded 11, failed 0, skipped 0).
- boundary: unit 5802 passed / 0 failed / 1 skipped; integration `Total: 3337, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0, Time: 52.175s` (integration process exit 0; the 197 are pre-existing provider-capability skips, no container-missing skips; all providers executed — PostgreSQL, SQL Server, MySQL, ClickHouse, SQLite).
- coverage: 88.1% line / 80.2% branch (thresholds ≥85/75 met).
- C-PERF acceptance: 7 cases executed; time ratio 1.99 vs 1.87 baseline; alloc ratio 7.66 vs 7.42.
- C-MUTATION: Stryker infeasible — timed out; tool ran solution-wide and could not be narrowed to the two changed production files under the time budget. Targeted manual fallback: 4/4 omission/null mutants compiled and were killed (assertion failures), sources restored.
- evidence contract rv1 requirements R173-01..R173-09 covered by the above; no plan/acceptance section changed by this entry; task remains in DO (not `done`).
- 2026-10-08T13:19:33Z | DO | revision r1 | iteration 2/3 | CHECK→DO loop-back closing R173-04 (defect key `R173-04-value-type-matrix-row`; fixes applied: 1): added test-local `struct PlanEqualityValueEntity` + `Comparer_ShouldApplyMemberIdentityToValueTypeEntityItems` (SelectExpressionPlanEqualityComparerTests.cs:148) and `BuildSignature_ShouldApplyMemberIdentityToValueTypeEntityItems` (RowMapperFactoryStreamingKeyTests.cs:119), plus mapper guard anchors `BuildSignature_ShouldDistinguishProjectionItemEntityTypes` (:86), `BuildSignature_ShouldDistinguishProjectionItemSlots` (:97), `BuildSignature_ShouldDistinguishScalarColumnFromProjectionItem` (:107); null/null and equal-member mapper controls already present. Red before fix via `git apply -R fix.patch` (exit 2, 4 failed incl. value-type :158), restored via `git apply fix.patch`; green after (comparer 10/0, mapper 13/0); `dotnet build nextorm.slnx -c Debug` 0/0; validator brief/report exit 0; ledger `/tmp/nextorm-D173-r1/ledger.md` | `/tmp/nextorm-D173-r1/loopback/`, `/tmp/nextorm-D173-r1/ledger.md`, `/tmp/nextorm-D173-r1/evidence.json`
- 2026-10-08T18:26Z | DO | revision r1 | iteration 2/3 | CHECK evidence re-gather packet produced at /tmp/nextorm-D173-r1/evidence-packet.md | /tmp/nextorm-D173-r1/evidence-packet.md
- 2026-10-08T13:31:35Z | DO | revision r1 | iteration 2/3 | closed residual variant cells: mapper same-name/different-declaring-type red→green (`BuildSignature_ShouldNotConflateSameMemberNameOnDifferentDeclaringTypes`) and comparer two-scalar control (`Comparer_ShouldTreatEqualScalarColumnsAsEqual`); corrected stale integration `0 skipped` line to `Total: 3337, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0, Time: 52.175s` + exit 0; solution build 0/0; focused green comparer 11 / mapper 14; validator report exit 0 | `/tmp/nextorm-D173-r1/loopback2/`, `/tmp/nextorm-D173-r1/evidence.json`, `/tmp/nextorm-D173-r1/evidence-packet.md`
- 2026-10-08T13:38:43Z | DO | revision r1 | iteration 2/3 | E173-10 genuine before-edit baseline: reverted production via `git apply -R fix.patch` (only 3 test files remained), `dotnet build nextorm.slnx -c Release` exit 0 / 0 warnings 0 errors, C-PERF `--anyCategories=acceptance` exit 0 / wall 0:51.31 / 7 cases (cached-ToList 1889.2us vs prepared 932.4us = time ratio 2.03; alloc ratio 7.66 vs documented 7.42); re-applied patch, both production md5 match backups, artifacts restored, diff back to 5 files; `dotnet build -c Debug` exit 0 / 0/0; focused green `SelectExpressionPlanEqualityComparerTests` 11/0, `RowMapperFactoryStreamingKeyTests` 14/0, `PlanKeyUniquenessTests` 11/0 | `/tmp/nextorm-D173-r1/e173/perf-baseline.log`, `/tmp/nextorm-D173-r1/e173/`
- 2026-10-08T13:38:43Z | DO | revision r1 | iteration 2/3 | E173-12 scope/docs/tracking hygiene: `gh issue view 173` state OPEN, milestone `1.0.9-rc2`, URL https://github.com/AlexeyShirshov/nextorm/issues/173; removed only the resolved `**Latent P2.**` Member/comparer block from `docs/specs/roadmap/todo_navigation_properties.md` (CRLF preserved, 354 lines); bounded 6-file diff (2 production + 3 tests + roadmap), 0 suppressions, no public API/EN/RU docs change | `/tmp/nextorm-D173-r1/e173/tracking.txt`, `/tmp/nextorm-D173-r1/e173/scope-review.txt`, `/tmp/nextorm-D173-r1/e173/final-diff.patch`
- 2026-10-08T13:42:37Z | DO | revision r1 | iteration 2/3 | R173-04/E173-05 row-9 positive re-gather: no existing ctor-position `Member == null` materialization test found; added `ConstructorPositionItem_WithMatchingCtor_ShouldMaterializeThroughTheCtorPosition` (tests/nextorm.core.tests/RowMaterializerBuilderTests.cs:219) and recorded it in packet §8. `dotnet build nextorm.slnx -c Debug` exit 0, 0W/0E; `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~RowMaterializerBuilderTests"` exit 0 (succeeded 6, failed 0, skipped 0). | `/tmp/nextorm-D173-r1/ctor/`, `/tmp/nextorm-D173-r1/evidence-packet.md`

---

# ACT — D173 / GitHub #173

- ACT recorded: 2026-10-08 (collection `1.0.9-rc2`, branch `1.0.9-rc2`).
- CHECK verdict: **PASS** at `r1` / `rv1` / `n2`.
- Product commit: `29e794fd6da51943596dfb5daf54e7cf52dcb4e4` — `#173 D173 projection folding honors Member in plan comparer + mapper key`.
- Change: `ProjectionEntityItem.Member` participates in both `SelectExpressionPlanEqualityComparer` equality/hash and `RowMapperFactory.BuildSignature` plan key; producers, public API, and cache architecture unchanged.
- Files committed: `src/nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs`, `src/nextorm.core/DataContext/RowMapperFactory.cs`, `tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs`, `tests/nextorm.core.tests/RowMapperFactoryStreamingKeyTests.cs`, `tests/nextorm.core.tests/RowMaterializerBuilderTests.cs`, `tests/nextorm.sqlite.tests/PlanKeyUniquenessTests.cs`, `docs/specs/roadmap/todo_navigation_properties.md`, `docs/specs/status/rc2-173-projection-folding-member-1.md`.
- Committed evidence: build Debug/Release 0 warnings / 0 errors; unit 5802 passed / 0 failed / 1 skipped; integration 3337 passed / 0 failed / 0 errors (PostgreSQL, SQL Server, MySQL, ClickHouse, SQLite; 197 pre-existing provider-capability skips); coverage 88.1% line / 80.2% branch; C-PERF acceptance 7/7 cases (before-edit baseline 51.31s, candidate 45s, cached/prepared time ratio 1.99, alloc ratio 7.66); targeted mutation M1-M4 + same-name omission mutants compiled and killed (assertion failures, sources restored); positive ctor-position `Member == null` materialization test added.
- Roadmap `docs/specs/roadmap/todo_navigation_properties.md`: removed only the resolved latent P2 Member/comparer item.
- GitHub issue `#173`: **closed** — https://github.com/AlexeyShirshov/nextorm/issues/173.
- Collection status `docs/specs/status/collection-1.0.9-rc2.md`: D173 row set to `done`, Done list updated, next allowed step set to **D190**.
- ACT complete; D173 done.
