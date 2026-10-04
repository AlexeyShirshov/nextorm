# Task #183 — Iteration 15 cached-path overhead (cycle `iteration-15-cached-path-b1`, Stage B1)

- task: #183 — https://github.com/AlexeyShirshov/nextorm/issues/183
- collection: `1.0.9-b-5`, group-1
- worktree: `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1`
- branch: `collection/1.0.9-b-5/group-1`
- base commit: `752943d` (#183 Stage A: cached-path decomposition, baseline and frozen budgets (rv=2))
- milestone: `1.0.9-b`
- design: `docs/specs/performance/iteration-15-cached-path-design.md` (§6 B1, §8 invariants, §9 testing)
- previous stage: `docs/specs/status/iteration-15-cached-path-183-2.md` (Stage A done via escalation decision a)
- cycle: 1 (this file is the B1 cycle status)
- plan revision: r=1
- attempt: n=1
- phase: DO (Stage B1)
- outcome: **incomplete — time-speedup not proved** (Stage B1; not accepted, not committed; work preserved as a patch)
- date: 2026-10-04

## Goal (Stage B1)

Reduce per-call parameter-scope allocations on the cached equality path in
`src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs` while preserving **full structural
equality**: shadowing, nested scopes, reentrancy (each `Equals` call isolated), and thread-safety.
No behavioural, public-API, hashing (`GetHashCode`), or `_tlsVisitor` change.

## Acceptance (R-B1-SCOPE / ISOLATION / CACHE / PERF / VALIDATION)

1. **R-B1-SCOPE** — inline small parameter scopes (up to 4 concurrent bindings) without a
   `Dictionary`; correct spill to a `Dictionary` on the 5th binding; no reverse migration; each
   lambda registers/unwinds only the bindings it added; removed inline slots cleared.
2. **R-B1-ISOLATION** — every `Equals` call starts from an empty scope; nested scopes shadow
   correctly; a failed comparison fully unwinds before the next (reentrancy).
3. **R-B1-CACHE** — equal plans reused; unequal plans with equal effective hash distinguished in
   the real `QueryPlanStore.TryGet` (forced-collision seam); no single-hash substitution for
   structural equality.
4. **R-B1-PERF** — 12 Stage A workloads within frozen budgets + `Prepared_ToList ≤780 B/op`;
   iteration14 gate incl. CTE zero rows.
5. **R-B1-VALIDATION** — 7 acceptance cases; build 0 Warning / 0 Error; coverage line≥85 /
   branch≥75; integration not skipped; provider-skipped ≠ pass.

## Requested-clarification provenance (S1/S2 recon done)

- **S1** — test-surface recon: `ExpressionPlanEqualityComparerNodeTests`,
  `ExpressionPlanEqualityComparerTests`, `PlanKeyStructureTests`,
  `CachedPathCharacterizationTests` exist in the worktree; the requested filters resolve. Inner-loop
  scope confirmed; `scripts/validate_inner_loop.py` is **absent** (confirmed), so manual discipline.
- **S2** — seam/representation recon: `QueryPlan` owns `_hashPlan` (frozen at construction,
  `QueryPlan.cs:22,39-59`); `QueryPlanStore.TryGet/Set` are `internal` (`QueryPlanStore.cs:52,66`);
  `InternalsVisibleTo` already exposes core internals to `nextorm.core.tests`, so no public API and
  no new visibility are needed. `GetHashCode`/`_tlsVisitor`/`_tlsVisitorOwner` are out of scope.
- No open clarification remains; S1/S2 recon is complete and the B1 scope is fixed.

## DO task list (D:A0–A5)

- **D:A0** — status + snapshot + evidence dir `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/`.
- **D:A1** — inline-4 parameter scope + spill, `ExpressionPlanEqualityComparer.cs` (one axis).
- **D:A2** — test-only forced-hash seam on `QueryPlan` near `_hashPlan` + collision regression in
  `tests/nextorm.core.tests/PlanKeyStructureTests.cs`, driven through the real
  `QueryPlanStore.Set/TryGet`; production path unchanged, no public API.
- **D:A3** — boundary/lifecycle/concurrency tests in
  `ExpressionPlanEqualityComparerNodeTests.cs`, `ExpressionPlanEqualityComparerTests.cs`,
  `PlanKeyStructureTests.cs`.
- **D:A4** — build / tests / integration / coverage / perf sweep (boundary).
- **D:A5** — internal report + handoff.

## Representation decision

In `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs:101-103` replace the private
`Dictionary` field on the nested `private struct ExpressionComparer` with an inline value-type
storage of **four** `(ParameterExpression a, ParameterExpression b)` pairs + count; identity-based
lookup; on the 5th concurrent binding spill **all** current bindings into a `Dictionary` and stay in
dictionary mode for that `Equals` (no reverse migration). Register/unwind via one private storage
abstraction; each lambda removes only the bindings it added; removed inline slots cleared.
`:218-258` and `:293-294` use the storage. Guards `:88-96,:118-126,:211-245,:160-163` unchanged.
Do NOT touch `GetHashCode`, `_tlsVisitor`, `_tlsVisitorOwner`. Explicitly rejected: blind ThreadStatic
scope, scope on the comparer, pooling without guaranteed clear, single-hash instead of equality.

## Collision seam

Add an internal test-only construction path on `QueryPlan` near `_hashPlan`
(`QueryPlan.cs:22,39-59`) that accepts a forced hash; production path unchanged; no public API;
existing IVT suffices. Test via the real `QueryPlanStore.Set/TryGet`.

## Variant matrix

| # | variant | disposition |
|---|---|---|
| 1 | null/ref both sides (`left==right`, null one side) | existing test (`NodeTests`) + guard `:88-96` unchanged |
| 2 | null param in scope (guard) | guard `:118-126` unchanged + test |
| 3 | identity / names ignored / value-vs-ref | `NodeTests.Parameter_ShouldBeCompared` + guard |
| 4 | arity mismatch incl. 0↔1 | **new test** (D:A3) |
| 5 | type mismatch first + late | existing `Lambda`/`DifferentShape` tests + unwind code `:211-245` |
| 6 | inline/spill boundaries, live count 0,1,3,4,5,8 | **new tests** (D:A3) |
| 7 | nested scopes shadowing | `PlanKeyStructureTests.Lambda_NestedShadowedParameter_ShouldCompareStructurally` |
| 8 | duplicate binding (same instance in scope) | existing `TryAdd` failure path + test |
| 9 | concurrent `Equals` on shared comparer with nested lambdas | **new test** (D:A3) |
| 10 | `GetHashCode` TLS owner (`_tlsVisitor`/`_tlsVisitorOwner`) | existing behaviour, unchanged; guard test |
| 11 | reentrancy / ownership | `Lambda_NestedScope_ShouldBeUnwoundAfterMismatch` + new |
| 12 | real store collision via seam | **new test** (D:A2) |
| 13 | unknown node throw (`:160-163`) | guard unchanged + existing coverage |
| 14 | sqlite characterization rows `:47-271` | `CachedPathCharacterizationTests` |
| 15 | providers | boundary sweep (D:A4), skip ≠ pass |

## Exact validation commands

Inner loop (mandatory D:A1 scope; build once, then `--no-build`):

```bash
dotnet build nextorm.slnx -c Debug
dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~ExpressionPlanEqualityComparerNodeTests|FullyQualifiedName~ExpressionPlanEqualityComparerTests|FullyQualifiedName~PlanKeyStructureTests"
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~CachedPathCharacterizationTests"
```

Boundary sweep (D:A4):

```bash
dotnet test tests/nextorm.core.tests -c Debug
dotnet test tests/nextorm.sqlite.tests -c Debug
dotnet test tests/nextorm.postgres.tests -c Debug
dotnet test tests/nextorm.sqlserver.tests -c Debug
dotnet test tests/nextorm.mysql.tests -c Debug
dotnet test tests/nextorm.mariadb.tests -c Debug
dotnet test tests/nextorm.clickhouse.tests -c Debug
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

Perf (evidence dir `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/`):

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=stage-a --exporters json --artifacts benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/A1
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=stage-a --exporters json --artifacts benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/B1
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=stage-a --exporters json --artifacts benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/A2
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=stage-a --exporters json --artifacts benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/B2
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkWarmDecompose*' '*SqliteBenchmarkCachedPlan*' '*SqliteBenchmarkFeaturePlanBuild*' --job short --exporters json
python3 eng/perf/iteration14_gate.py
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --exporters json --artifacts benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/acceptance
```

Coverage: per `.github/workflows/dotnet.yml` (`dotnet-coverage collect` → `reportgenerator`,
`coverage.settings.xml`), thresholds line≥85 / branch≥75.

## Evidence contract rv=1

| row | applies | predicate | sources / commands | owner |
|---|---|---|---|---|
| G1-PERF | #183 Stage B1, unconditional | **MEASURED** — 12 Stage A workloads within frozen budgets; `Prepared_ToList ≤780 B/op`; iteration14 gate incl. CTE zero rows; 7 acceptance cases; paired/interleaved runs | Stage A frozen budgets (`iteration-15-stage-a-manifest.json`, `acceptance-benchmarks.md:61-69`); the `stage-a` arms A1/B1/A2/B2; `--anyCategories=acceptance`; `eng/perf/iteration14_gate.py` | coder gathers, check accepts |
| G1-CACHE | #183 Stage B1, unconditional | structural equality incl. forced real-lookup collision, shadowing/nested/reentrancy/concurrency; no sticky `Cache=false`/`_dontCache`; build 0/0; coverage line≥85/branch≥75; real provider runs (skip ≠ pass) | targeted + boundary commands above; `PlanKeyStructureTests`; `CachedPathCharacterizationTests`; integration with `DOCKER_HOST` | coder executes, scout confirms scope, check accepts |
| G1-CTE | only if `CteHoister.Hoist` / nested read-CTE path via `PrepareCtes` changed | predicate=false → N/A with proof (empty diff); predicate=true → active #166 prerequisite, stop/re-scope | `git diff 752943d -- src` and `git diff 752943d -- src/nextorm.core/Visitors/CteHoister.cs` | scout semantic owner, orchestrator stop/re-scope, check verifies |

CHECK re-gather budget: **2** targeted requests, owner `check`; a simple missing report → re-gather,
not a plan revision and not a repeated DO.

## Inner-loop discipline

- `scripts/validate_inner_loop.py` — **CONFIRMED ABSENT** in this worktree; manual discipline.
- Inner loop is the affected filtered subset only: affected project built once, then
  `dotnet test <project> --no-build --filter <selector>`.
- One comprehensive boundary sweep at DO→CHECK (full core/sqlite suites, all-provider
  SQL-generation, postgres CTE, integration with `DOCKER_HOST`, coverage line≥85/branch≥75).
- Provider-skipped ≠ pass.

## Progress log

- 2026-10-04T17:50Z | PLAN | revision r1 | iteration n=1/3 | PLAN written | docs/specs/status/iteration-15-cached-path-b1.md
- 2026-10-04T17:50Z | DO | revision r1 | iteration n=1/3 | DO started (D:A0 status/snapshot/evidence dir; D:A1 inline-4+spill) | docs/specs/status/iteration-15-cached-path-b1.md
- 2026-10-04T17:52Z | DO | revision r1 | iteration n=1/3 | D:A0 done (status written; evidence dir benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/; snapshot base 752943d) | docs/specs/status/iteration-15-cached-path-b1.md
- 2026-10-04T17:52Z | DO | revision r1 | iteration n=1/3 | D:A1 done (inline-4 ParameterScope + one-way spill on 5th binding, identity lookup, compaction on remove; guard/CompareLambda/CompareParameter only); build exit 0, 0W/0E; core targeted exit 0 54P/0F; sqlite characterization exit 0 11P/0F | /tmp/nextorm-b1/build.log
- 2026-10-04T17:57Z | DO | revision r1 | iteration n=1/3 | D:A2 done (internal forced-hash QueryPlan ctor at QueryPlan.cs:49; collision regression PlanKeyStructureTests.QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality via real QueryPlanStore.Set/TryGet, class added to "Query cache controls" collection); roslyn refs: internal ctor referenced only at PlanKeyStructureTests.cs:287,288,289, no production caller | /tmp/nextorm-b1/build-b1-a2a3.log
- 2026-10-04T17:57Z | DO | revision r1 | iteration n=1/3 | D:A3 done (7 new boundary/lifecycle tests in ExpressionPlanEqualityComparerNodeTests + concurrent Equals test in ExpressionPlanEqualityComparerTests: arity 0<->1, inline/spill 0/1/3/4/5/8, nested spill, sibling shadowing, reused outer parameter TryAdd=false, null/free parameter guard, reentrancy, 4-thread concurrent Equals); build exit 0 0W/0E; core targeted exit 0 63P/0F (54+9); sqlite characterization exit 0 11P/0F | /tmp/nextorm-b1/test-core-targeted-a2a3.log
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 builds done: Debug exit 0 0W/0E (/tmp/nextorm-b1/build-debug.log), Release exit 0 0W/0E (/tmp/nextorm-b1/build-release.log); re-verified 0W/0E after paired-A restore (/tmp/nextorm-b1/build-release-restored.log, build-debug-restored.log, build-debug-final.log) | /tmp/nextorm-b1/build-debug.log
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 boundary test sweep (Debug, full): core 1491P/0F/0S exit 0 (first run 1490P/1F exit 2 = host-load timing flake QueryCacheControlsTests.SlidingExpiration_Should_Evict_After_Ttl, isolated 3/3 pass, full rerun green), sqlite 993P/0F/1S exit 0, postgres 742P/0F/0S exit 0, sqlserver 556P/0F/0S exit 0, mysql 257P/0F/0S exit 0, mariadb 157P/0F/0S exit 0, clickhouse 491P/0F/0S exit 0 | /tmp/nextorm-b1/test-nextorm.*.log
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 acceptance: exit 0, wall 44.54s (<4min), 7/7 cases 0 failures; Cached_ToList/Prepared_ToList time ratio 1.6717 vs baseline 1.8699 (-10.6%), alloc ratio 7.0948 vs 7.4232 (-4.42%); no comparable ratio grows >20% (max per-case alloc delta +3.0%); Prepared_ToList 779.7 B/op <=780 | benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/acceptance/acceptance.log
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 stage-a B1 (exit 0, wall 77s): 12/12 arms within frozen budgets; equality/lookup arms improved vs Stage A: Where_CachedHit_PlanOnly 6528.17->6096.17 (-6.62%), Where_CachedHit_ToList 7259.84->6827.92 (-5.95%), Join_CachedHit_PlanOnly 12184.82->11832.91 (-2.89%), Join_CachedHit_ToList 12939.31->12587.31 (-2.72%); Where_Prepared_ToList 779.60<=780; no arm >20% growth | benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/B1/
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 paired A1 (core reverted to 752943d, benchmark rebuilt; B1 restored via git apply, sha256 match): alloc deltas attribute the cached-arm gains to the comparer change; all arm times +2..+38% under loadavg~9 (timing noise-dominated, allocations are the frozen axis); no paired regression | benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/A1/
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 iteration14 gate: dotnet --filter 3 classes --job short exit 0 (wall 12m41s), eng/perf/iteration14_gate.py --no-run exit 0, all 56 row/job verdicts within budget, zero rows Cte_Warm_Reused/RecursiveCte_Warm_Reused/Join4_Warm_Reused all 0.00 B/op | benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/iteration14/iteration14-gate.log
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 G1-CTE predicate=false: git diff --exit-code 752943d -- src/nextorm.core/Builders/CteHoister.cs empty; git diff --stat 752943d -- src = only QueryPlan.cs (+18) and ExpressionPlanEqualityComparer.cs (+160/-10); CRLF preserved | docs/specs/status/iteration-15-cached-path-b1.md
- 2026-10-04T18:19Z | DO | revision r1 | iteration n=1/3 | D:A4 evidence-path note: --artifacts is ignored by NextormConfig.ArtifactsPath (BenchmarkArtifacts.Resolve looks for nextorm.sln, repo tracks nextorm.slnx), so stage-a/acceptance/iteration14 BDN runs emitted to repo-root BenchmarkDotNet.Artifacts/results; those runs left 44 tracked repo-root result files deleted (the earlier claim that the tracked dir was already restored was inaccurate); B1 pre-CHECK polish restored them via `git checkout 752943d -- BenchmarkDotNet.Artifacts/results/` (porcelain empty); per-run evidence copied into benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/{acceptance,B1,A1,iteration14}/ | benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/
- 2026-10-04T18:26Z | DO | revision r1 | iteration n=1/3 | D:A4/D:A5 complete; entering CHECK (coverage 87.1/78.8 PASS, delta +0.0/+0.1; integration exit 0 3136 total 0F/193S all providers live; B1 results appended) | docs/specs/status/iteration-15-cached-path-b1.md
- 2026-10-04T18:32Z | DO | revision r1 | iteration n=1/3 | B1 pre-CHECK polish (0C/0W, 5 Suggestions): #1 applied (collision test planA2 now built from a second independently issued cmdA2 so QueryPlan.Equals no longer short-circuits on command reference); #2 applied (arity test reframed to Lambda_DifferentDelegateTypes_ShouldRejectAtOuterTypeGuard, scope-leak assertion uses distinct param-bearing trees); #3 applied (GetInlineA/GetInlineB/SetInline default arms throw ArgumentOutOfRangeException instead of aliasing slot 3; no hot-path branch added); #4 applied (identity-lookup comments pin EqualityComparer<ParameterExpression>.Default semantics); #5 applied (44 tracked repo-root BenchmarkDotNet.Artifacts/results files restored from 752943d, porcelain empty; status wording corrected); #6 confirmed (nothing under tests/coverage/ or benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/ staged). Declined: none. Build Debug exit 0 0W/0E; core targeted exit 0 63P/0F/0S; modified 2/2 exit 0; sqlite characterization exit 0 11P/0F/0S | /tmp/nextorm-b1/
- 2026-10-04T18:55Z | CHECK | revision r1 | iteration n=1/3 | evidence close attempt 1→2 | counters/mutation-disposition/perf-stats/variant-citations/docs-lens recorded | docs/specs/status/iteration-15-cached-path-b1.md

## Done / Verified / Remaining

- Done: D:A0 (this status file), D:A1 implementation, D:A2 seam + collision regression, D:A3
  boundary/lifecycle/concurrency tests.
- Verified: targeted inner loop re-run after D:A2/D:A3 (core 63P/0F, sqlite characterization 11P/0F);
  build 0W/0E; seam referenced only by tests (roslyn refs). Boundary sweep still pending in D:A4.
- Remaining: D:A4 build/tests/integration/coverage/perf, D:A5 report + handoff; then B2, C (active,
  same milestone); #166 deferred with trigger (`CteHoister.Hoist` / nested read-CTE `PrepareCtes`).

## Note (D:A3 arity guard)

`CompareLambda`'s `b.Parameters.Count != n` check is defensively unreachable through the public
`Expression.Lambda` surface: the factory rejects a parameter count that does not match the delegate
type, so two lambda nodes with the same `Type` always carry the same arity. A 0-parameter vs
1-parameter lambda in fact has different delegate types (`Func<int>` vs `Func<int,int>`), so the outer
type guard rejects it before `CompareLambda`; the regression is framed accordingly
(`Lambda_DifferentDelegateTypes_ShouldRejectAtOuterTypeGuard`) and asserts the no-scope-leak contract
with distinct param-bearing trees that do reach the scope path. It does not fabricate an invalid tree
via reflection; per the brief, only public/internals-visible construction is used. No D:A1 defect found.

## B1 results

**Changed files (D:A1/D:A2), scope = one axis + seam only**

- `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs` (+160/-10): the `ExpressionComparer`
  private `Dictionary` parameter scope is replaced by an inline value-type storage of **4**
  `(ParameterExpression a, ParameterExpression b)` pairs + count; identity lookup; one-way spill of
  all live bindings into a `Dictionary` on the **5th** concurrent binding (no reverse migration);
  each lambda removes only the bindings it added and clears removed inline slots. Guards,
  `CompareLambda`, `CompareParameter` only; `GetHashCode`/`_tlsVisitor`/`_tlsVisitorOwner` untouched.
- `src/nextorm.core/DataContext/Cache/QueryPlan.cs` (+18): internal test-only forced-hash
  construction path near `_hashPlan`, used exclusively by the collision regression through the real
  `QueryPlanStore.Set/TryGet`; production constructor unchanged, no public API, existing IVT suffices.

**New tests (D:A2/D:A3)**

- `PlanKeyStructureTests.cs`: `QueryPlanStore_ForcedHashCollision_ShouldFallBackToStructuralEquality`
  (real store, forced equal hash → structural comparison decides).
- `ExpressionPlanEqualityComparerNodeTests.cs` (7): `Lambda_DifferentDelegateTypes_ShouldRejectAtOuterTypeGuard`,
  `Lambda_InlineAndSpillBoundaries_ShouldCompareAndUnwind`, `Lambda_NestedScopes_ShouldSpillAndUnwind`,
  `Lambda_SiblingScopes_ShouldShadowAndUnwind`, `Lambda_ReusedOuterParameter_ShouldReportNotEqualWithoutLeakingScope`,
  `Parameter_NullAndFreeInteractions_ShouldGuard`, `Reentrancy_FailedNestedComparison_ShouldNotLeakScope`.
- `ExpressionPlanEqualityComparerTests.cs`: `Equals_ShouldBeThreadSafe_WithNestedLambdas`.

**Build** — Debug exit 0, 0 Warning / 0 Error (`/tmp/nextorm-b1/build-debug.log`); Release exit 0,
0/0 (`/tmp/nextorm-b1/build-release.log`); integration build exit 0, 0 Error
(`/tmp/nextorm-b1/build-integration.log`).

**Full test counts (D:A4 boundary sweep, Debug)** — core 1491P/0F/0S; sqlite 993P/0F/1S; postgres
742P/0F/0S; sqlserver 556P/0F/0S; mysql 257P/0F/0S; mariadb 157P/0F/0S; clickhouse 491P/0F/0S; all
exit 0 (`/tmp/nextorm-b1/test-nextorm.*.log`). One host-load timing flake
(`QueryCacheControlsTests.SlidingExpiration_Should_Evict_After_Ttl`) passed isolated 3/3 and on the
full rerun.

**Acceptance (D:A4)** — exit 0, wall 44.54 s (<4 min), **7/7 cases**, 0 failures.
`Cached_ToList/Prepared_ToList` time ratio **1.6717** vs baseline 1.8699 (−10.6%); alloc ratio
7.0948 vs 7.4232 (−4.42%); no comparable ratio grows >20% (max per-case alloc delta +3.0%);
`Prepared_ToList` **779.7 B/op ≤ 780**. Evidence `.../b1/acceptance/acceptance.log`.

**Stage-a B1 arm** — exit 0, wall 77 s, **12/12 arms within frozen budgets**. Equality/lookup arms vs
Stage A: `Where_CachedHit_PlanOnly` 6528.17→6096.17 (−6.62%), `Where_CachedHit_ToList`
7259.84→6827.92 (−5.95%), `Join_CachedHit_PlanOnly` 12184.82→11832.91 (−2.89%),
`Join_CachedHit_ToList` 12939.31→12587.31 (−2.72%); `Where_Prepared_ToList` 779.60 ≤ 780; no arm
>20% growth. Evidence `.../b1/B1/`.

**Paired-A attribution** — A1 (core reverted to `752943d`, benchmark rebuilt; B1 restored via
`git apply`, sha256 match): allocation deltas attribute the cached-arm gains to the comparer change;
all arm times +2..+38% under loadavg ≈9 (timing noise-dominated; allocations are the frozen axis);
no paired regression. Evidence `.../b1/A1/`.

**Iteration14 gate** — `dotnet --filter` 3 classes `--job short` exit 0 (wall 12 m 41 s);
`eng/perf/iteration14_gate.py --no-run` exit 0; all **56 row/job verdicts within budget**; zero-row
arms `Cte_Warm_Reused`, `RecursiveCte_Warm_Reused`, `Join4_Warm_Reused` all **0.00 B/op**. Evidence
`.../b1/iteration14/iteration14-gate.log`.

**Coverage (CI workflow)** — `dotnet-coverage collect` exit 0; full run total 8012, 0 failed, 5517
succeeded, 2495 skipped (provider skips expected without `DOCKER_HOST` under coverage). Aggregate
**line 87.1% / branch 78.8%** → **PASS** vs ≥85 / ≥75. Per-project (line/branch): core 87.4/79.2,
sqlite 89.3/66.2, postgres 79.2/75.8, sqlserver 78.3/76.6. **Delta vs Stage A**: aggregate line
**0.0 pp**, branch **+0.1 pp**; core branch +0.1; sqlite/postgres/sqlserver unchanged. (Aggregate is
the enforced gate; sqlite branch 66.2 and sub-85 per-provider lines are informational.) Evidence
`benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/coverage/{Summary.txt,collect.log}`.

**Integration (container-backed, mandatory)** —
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run
--project tests/nextorm.integration.tests -c Debug --no-build -- -noColor`, exit **0**, Total
**3136**, Errors 0, **Failed 0**, **Skipped 193** (all capability `Assert.Skip`s; **no availability
skip**). Containers started: PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse. Per-provider
failed/skipped: PostgreSQL 0/25, SQL Server 0/43, MySQL 0/81, ClickHouse 0/0, SQLite 0/43 (MariaDB
probe 0/1; `LobPerfHarness` 0/1). **Not a provider-skipped run.** Evidence `.../b1/integration.log`.

**G1-CTE predicate = false** — `git diff --exit-code 752943d -- src/nextorm.core/Builders/CteHoister.cs`
empty (exit 0); `git diff --stat 752943d -- src` = only `QueryPlan.cs` (+18) and
`ExpressionPlanEqualityComparer.cs` (+160/−10); `CteHoister.Hoist` / `PrepareCtes` untouched (the only
`PrepareCtes` is in `QueryCommand.QueryPreparer.cs`, unchanged). CRLF preserved.

**Evidence contract rv=1** — **G1-PERF met** (12/12 stage-a within frozen budgets, acceptance 7/7,
iteration14 gate incl. CTE zero rows). **G1-CACHE met** (structural equality incl. forced real-store
collision, shadowing/nested/reentrancy/concurrency; no sticky `Cache=false`/`_dontCache`; build 0/0;
coverage aggregate ≥85/≥75; real provider runs, no provider-skipped). **G1-CTE N/A** (predicate=false,
proof above).

**Remaining** — units **B2** and **C** remain active in this milestone; **#166 stays deferred**
(trigger `CteHoister.Hoist` / nested read-CTE `PrepareCtes`). D:A4/D:A5 complete.

## CHECK evidence close (r1, attempt 1→2)

**Suppression / slop counters** (changed hunks; 5 files, 514 changed lines / 504 added, ~12 changed
functions): `#pragma warning` 0, `SuppressMessage` 0, `NoWarn` 0, `TODO|FIXME|HACK|XXX` 0,
`Debug.Assert` 0, `async void` 0, empty catch 0 → **0/514 = 0.00** each. Pre-existing, **not** in the
diff: `#pragma warning disable IDE0066` at `src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs:571`
(restore `:573`) — untouched, justified IDE0066 pattern; 3 pre-existing `Debug.Assert` at
`src/nextorm.core/DataContext/Cache/QueryPlan.cs:120-122` — untouched.

**Mutation-testing disposition** (changed production types): global `dotnet-stryker` 5.0.0 is present
at `/home/alex/.dotnet/tools/` but is **not** in the repo toolchain (`.config/dotnet-tools.json` =
coverage/reportgenerator/docfx only). Stryker vstest run on the 2 changed files with a test-case
filter selected the 63 focused tests but did not complete: coverage capture failed ("Disable coverage
based optimisation"), projects report "Microsoft.Testing.Platform … not yet supported by Stryker",
408 mutants remained untested (7498 CompileError, 22966 filtered). Disposition: **mutation testing
not completed / tool incompatible with the repo test runner**; manual justification — inline/spill
boundaries `ExpressionPlanEqualityComparerNodeTests.cs:468`, concurrent equality
`ExpressionPlanEqualityComparerTests.cs:63`, real store collision `PlanKeyStructureTests.cs:267`
cover the changed logic. Do NOT modify `.config/dotnet-tools.json`.

**Paired perf statistics** (A1 vs B1, BDN JSON, N=3, 99.9% CI margin): **no** of the 12 stage arms
shows a statistically reliable time difference (all deltas within the combined margin; A1 times
+2..+38% under loadavg ≈9). Allocation deltas on cached arms: `Where_CachedHit_PlanOnly` −6.62%,
`Where_CachedHit_ToList` −5.95%, `Join_CachedHit_PlanOnly` −2.89%, `Join_CachedHit_ToList` −2.72%.
Acceptance B1 vs baseline and vs Stage A: all deltas within error bars (e.g. `Prepared_ToList` +22.3%
vs Stage A has combined margin 1.357 ms > 0.199 ms). **Verdict: no statistically reliable end-to-end
time regression; criterion holds.** B1 cached/prepared ratio 1.6717 vs baseline 1.8699 (Stage A
2.0994).

**Variant-row citations** (test `file:line`): null/ref `ExpressionPlanEqualityComparerNodeTests.cs:48`;
null param guard `:516`; identity/ref `:75` + `PlanKeyStructureTests.cs:401`; arity/outer type
`NodeTests.cs:443`; type mismatch `:330,:443`; inline/spill `:468`; nested shadow
`PlanKeyStructureTests.cs:341`; duplicate `NodeTests.cs:506`; concurrency
`ExpressionPlanEqualityComparerTests.cs:63`; TLS hash `:30`; reentrancy `NodeTests.cs:531` +
`PlanKeyStructureTests.cs:366`; collision `PlanKeyStructureTests.cs:267`; sqlite characterization
`tests/nextorm.sqlite.tests/CachedPathCharacterizationTests.cs:21` (tests 48–272); providers
`tests/nextorm.integration.tests/CommonTestSuite.Cache.cs:6` + `PostgresIntegrationTests.cs:8`,
`SqlServerIntegrationTests.cs:8`, `MySqlIntegrationTests.cs:8`, `SqliteIntegrationTests.cs:5`,
`ClickHouseIntegrationTests.cs:15`. **Unknown-node throw**: implementation
`ExpressionPlanEqualityComparer.cs:318` has **no direct test** → row closed as **deferred+trigger**
(trigger: any change to visitor/unknown-node handling).

**Docs-lens disposition**: public API unchanged → no EN/RU public docs; only internal
status/report/design touched; no public links to specs.

## CHECK evidence close (r1, attempt 2→3)

**1. Stage-A / B1 allocation pairs + full table.** B/op = `Memory.BytesAllocatedPerOperation/100`;
sources `benchmarks/BenchmarkDotNet.Artifacts/iteration15-stage-a/stage-a-benchmark-full-compressed.json`
(StageA frozen) and
`benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/{A1,B1,A2,B2}/NextORM.Benchmark.SqliteBenchmarkStageA-report-full-compressed.json`.
A = base `752943d`, B = B1 inline-4 scope. `(C)` = unchanged controls (arms not targeted by the
equality-scope change); `(T)` = targeted cached-hit arms.

| arm | StageA | A1 | B1 | A2 | B2 |
|---|---|---|---|---|---|
| Where_Construct (C) | 2544.05 | 2544.05 | 2544.05 | 2544.05 | 2656.05 |
| Where_Prepare_NoHash (C) | 3880.09 | 3880.09 | 3880.10 | 3880.10 | 3992.10 |
| Where_Prepare_Hash (C) | 4352.11 | 4352.11 | 4352.11 | 4352.11 | 4464.11 |
| Join_Construct (C) | 6000.38 | 6000.41 | 6080.41 | 6000.41 | 6000.41 |
| Join_Prepare_NoHash (C) | 8512.56 | 8512.61 | 8592.61 | 8512.61 | 8512.56 |
| Join_Prepare_Hash (C) | 8952.63 | 8952.63 | 9032.64 | 8952.63 | 8952.63 |
| Where_Prepared_ToList (C) | 779.65 | 779.64 | 779.60 | 779.69 | 779.69 |
| Join_Prepared_ToList (C) | 802.25 | 802.21 | 802.25 | 802.25 | 802.25 |
| Where_CachedHit_PlanOnly (T) | 6528.17 | 6528.18 | 6096.17 | 6528.18 | 6208.17 |
| Join_CachedHit_PlanOnly (T) | 12184.82 | 12184.83 | 11832.91 | 12184.75 | 11752.80 |
| Where_CachedHit_ToList (T) | 7259.84 | 7259.84 | 6827.92 | 7259.93 | 6939.83 |
| Join_CachedHit_ToList (T) | 12939.31 | 12939.31 | 12587.31 | 12938.95 | 12507.31 |

**Unchanged-control no-growth.** Raw StageA→B1 pairs: `Where_Construct` 2544.05→2544.05 (0.00),
`Where_Prepare_NoHash` 3880.09→3880.10 (+0.00), `Where_Prepare_Hash` 4352.11→4352.11 (0.00),
`Join_Construct` 6000.38→6080.41 (+80.03), `Join_Prepare_NoHash` 8512.56→8592.61 (+80.05),
`Join_Prepare_Hash` 8952.63→9032.64 (+80.01), `Where_Prepared_ToList` 779.65→779.60 (−0.05),
`Join_Prepared_ToList` 802.25→802.25 (0.00). The three Join controls carry a uniform **+80 B/op** in
B1, but that is run-level jitter, not attributable: round 2 (A2→B2) leaves the whole Join family flat
(`Join_Construct` 6000.41→6000.41, …) and instead shifts the whole Where family **+112 B/op**
(`Where_Construct` 2544.05→2656.05; `Where_Prepare_NoHash` +112.00; `Where_Prepare_Hash` +112.00;
`Where_CachedHit_*` +112) on sha256-identical code. The offset is family-wide (every `BuildWhere` /
`BuildJoin` arm), excludes the `Prepared_ToList` controls (no fluent build), and flips family between
rounds → JIT/measurement jitter. Control-normalized change (B−A minus the family `*_Construct`
offset) is **0.00 ± 0.05 B/op for all 8 controls. No control grows reproducibly; the no-growth claim
holds.**

**Reproducible targeted reduction.** Raw cached-arm deltas are −320.0…−432.0 B/op; control-normalized
they collapse to **−432 B/op on all four targeted arms in both rounds** (spread ≤0.5):
`Where_CachedHit_PlanOnly` raw −432.01 (r1) / −320.01 (r2) → norm −432.01 / −432.01;
`Join_CachedHit_PlanOnly` −351.92 / −431.95 → −431.92 / −431.95; `Where_CachedHit_ToList` −431.92 /
−320.10 → −431.92 / −432.10; `Join_CachedHit_ToList` −352.00 / −431.64 → −432.00 / −431.64. That
−432 B/op is exactly the per-lookup `Dictionary` allocation the inline-4 scope removes.

**2. Paired time attribution (A2/B2 round added).** B1 diff saved to `/tmp/nextorm-b1/b1-core2.patch`;
base restored (`git checkout 752943d --` the 2 core files); A2 run
`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=stage-a --exporters json --artifacts benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/A2`
exit 0, wall 71.1 s, 12/12 (`--artifacts` ignored → results copied out of repo-root
`BenchmarkDotNet.Artifacts/results` and the untracked leftovers removed). B1 restored with
`git apply /tmp/nextorm-b1/b1-core2.patch`; `sha256sum -c` **OK for both files**; B2 run exit 0, wall
82.5 s, 12/12. Per-arm mean (ms), Δ(B−A) vs combined 99.9 % CI margin (quadrature):

| arm | A1 | B1 | Δ(B1−A1) ± margin | A2 | B2 | Δ(B2−A2) ± margin | verdict |
|---|---|---|---|---|---|---|---|
| Where_Construct | 0.120 | 0.126 | +0.006 ± 0.158 | 0.119 | 0.143 | +0.024 ± 0.055 | noise |
| Where_Prepare_NoHash | 0.253 | 0.286 | +0.033 ± 0.226 | 0.275 | 0.281 | +0.006 ± 0.251 | noise |
| Where_Prepare_Hash | 0.332 | 0.341 | +0.009 ± 0.410 | 0.336 | 0.372 | +0.036 ± 0.566 | noise |
| Join_Construct | 0.520 | 0.720 | +0.199 ± 0.640 | 0.563 | 0.553 | −0.010 ± 0.642 | noise |
| Join_Prepare_NoHash | 0.943 | 0.975 | +0.032 ± 1.383 | 1.110 | 1.006 | −0.104 ± 1.249 | noise |
| Join_Prepare_Hash | 1.115 | 1.274 | +0.159 ± 1.010 | 1.254 | 1.311 | +0.057 ± 3.167 | noise |
| Where_Prepared_ToList | 0.987 | 1.053 | +0.066 ± 1.643 | 1.172 | 0.948 | −0.224 ± 1.455 | noise |
| Join_Prepared_ToList | 1.023 | 1.111 | +0.088 ± 1.128 | 1.382 | 1.306 | −0.076 ± 1.132 | noise |
| Where_CachedHit_PlanOnly | 0.622 | 0.647 | +0.025 ± 0.591 | 0.697 | 0.703 | +0.006 ± 0.928 | noise |
| Join_CachedHit_PlanOnly | 1.791 | 1.827 | +0.037 ± 1.335 | 2.230 | 1.978 | −0.252 ± 6.432 | noise |
| Where_CachedHit_ToList | 1.832 | 1.912 | +0.080 ± 2.897 | 2.007 | 2.056 | +0.048 ± 2.899 | noise |
| Join_CachedHit_ToList | 4.110 | 4.254 | +0.144 ± 8.479 | 5.610 | 5.218 | −0.393 ± 11.643 | noise |

**Verdict: no arm in either round (nor pooled A1,A2 vs B1,B2) shows a time delta beyond the combined
margin under the loaded host (1-min loadavg 8.2 / 5-min 14.8 observed around the round) — no
attributable stage time speedup beyond noise. Attribution is the reproducible −432 B/op allocation
reduction; stated as allocation-only, not a time speedup.** Evidence
`.../iteration15-b1/{A2,B2}/{A2,B2}-stage-a.log`. Build note:
`dotnet build nextorm.slnx -c Release` cannot compile base-core + B1 tests (B1-only internal
`QueryPlan` ctor referenced at `tests/nextorm.core.tests/PlanKeyStructureTests.cs:297-299`, 3× CS1729),
so the A2 perf build is `dotnet build benchmarks/nextorm.benchmark -c Release` (exit 0, 0 W/0 E,
`/tmp/nextorm-b1/build-benchmark-A2.log`); after B1 restore the full solution builds exit 0, 0 W/0 E
(`/tmp/nextorm-b1/build-release-B2.log`).

**3. Integration provenance.** Command
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor`;
the log confirms the endpoint `Connected to Docker: Host: unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`,
Server Version 5.8.6 (Podman), API 1.44. Containers started
`[testcontainers.org 00:00:00.86] Start Docker container …` ×5: `525af9d6a314`, `4ab2d4542432`,
`a1877941aa2e`, `d703aedb9b21`, `914f06a0ba5f`; all ready, identified by readiness probe — Postgres
`a1877941aa2e` (`pg_isready`), SQL Server `4ab2d4542432` (`sqlcmd SELECT 1`), MySQL `d703aedb9b21`
(`mysql test --execute=SELECT 1`), MariaDB `914f06a0ba5f` (`healthcheck.sh --innodb_initialized`),
ClickHouse `525af9d6a314`; no separate ryuk start line (reuse path). Result
`Total: 3136, Errors: 0, Failed: 0, Skipped: 193, Not Run: 0` → **executed 2943, 0 failed**; skips are
capability `Assert.Skip`s — `MySqlIntegrationTests` 79, `SqlServerIntegrationTests` 43,
`SqliteIntegrationTests` 43, `PostgresIntegrationTests` 25, `LobCapabilityProbeTests` 3
(MySQL 2 / MariaDB 1), ClickHouse 0. Not a provider-skipped run. Evidence
`benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/integration.log:9-14,26-30,46-57,163,445`.

**4. Unknown-node trigger (replaces the vague "deferred+trigger").** Trigger: any change to
`ExpressionPlanEqualityComparer` expression-node dispatch
(`src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs:318` unknown-node throw) or addition of a
new `Expression` node type handled by the comparer; before such a change, add the direct test.

**5. Restore / cleanup.** No production logic change beyond the A2 restore round-trip:
`git diff --stat 752943d -- src` = exactly `QueryPlan.cs` (+18) and
`ExpressionPlanEqualityComparer.cs` (+170/−10); the two files' `sha256sum` matches the pre-revert
state after `git apply`; repo-root tracked `BenchmarkDotNet.Artifacts/results/` churn = 0; leftover
`StrykerOutput/` removed; stray `dotnet-stryker`/vstest processes (PID 3369287 and runners) killed.
CRLF preserved.

- 2026-10-04T19:07Z | CHECK | revision r1 | iteration n=1/3 | evidence close attempt 2→3 | control no-growth (control-normalized 0.00 B/op, all 8); targeted cached-arm allocation −432 B/op reproducible in both rounds; all arm times within combined 99.9% CI margin → allocation-only attribution, no attributable time speedup; integration provenance + unknown-node trigger recorded; `git diff --stat 752943d -- src` = 2 files; build 0/0 | docs/specs/status/iteration-15-cached-path-b1.md

## B1 outcome — incomplete (escalation decision (b) → fallback (c))

- cycle outcome: **incomplete — time-speedup not proved**. B1 is **not `done`**, **not accepted**, **not
  committed**; the work is preserved as a patch for a later controlled re-measure. The B1 stage target
  (a reproducible target-stage **time** speedup) is unmet; only the allocation reduction was proved.
- B2 and C remain active in the same milestone; #166 stays deferred (trigger not fired).

### 3 CHECK FAILs (attempts 1/3, 2/3, 3/3)

1. **CHECK 1/3 FAIL — evidence-close gaps.** Suppression/slop counters, mutation-testing disposition,
   paired perf statistics, variant-row citations and docs-lens disposition were not yet recorded. Closed
   by re-gather (evidence close 1→2, 2026-10-04T18:55Z).
2. **CHECK 2/3 FAIL — G1-PERF time-speedup sub-obligation.** Paired A1↔B1 attribution showed
   allocation-only improvement; no attributable target-stage time gain. A controlled target-stage
   measurement was requested (evidence close 2→3, 2026-10-04T19:07Z).
3. **CHECK 3/3 FAIL — time-speedup still not attributable.** After a second paired round (A2/B2) every
   one of the 12 stage arms still lies inside its combined 99.9 % CI margin under a loaded host (1-min
   loadavg ≈ 8.2, 5-min ≈ 14.8; several arm margins larger than the signal). CHECK budget 3/3 exhausted
   → **Escalated** (no fourth CHECK attempt).

### Escalation

- **Decision (b):** accept B1 only if a **controlled target-stage time measurement** is produced under
  `loadavg(1m) < 1.0`; otherwise apply the fallback.
- **r2 (controlled re-measure) infeasibility:** the precondition `loadavg(1m) < 1.0` is objectively
  unmeetable in this environment — sampled 1-min loadavg **3.11–4.60** (drifting up) across the
  measurement window; host is **AMD Ryzen 7 5800HS, 8 logical / 4 physical cores on a shared host**
  with **no process control** (cannot isolate/pin CPUs or quiesce co-tenants); the loaded round already
  observed 1-min loadavg ≈ 8.2 / 5-min ≈ 14.8.
- **Applied fallback (c):** **B1 = incomplete — time-speedup not proved under controlled conditions.**
  Not accepted, not committed; does not block progress; preserved for a later controlled re-measure.

### Preserved B1 work (patch)

- Durable patch: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/b1-incomplete.patch`
  (**26904 B**, sha256 `0be74f92aa4250c78a566849dc1574d97c1c74aa154e18f56650501e34a7dc50`), also at
  `/tmp/nextorm-b1/b1-incomplete.patch`; `git status --porcelain` snapshot at
  `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/b1-status.txt`.
- The patch records both source changes (`ExpressionPlanEqualityComparer.cs` inline-4 parameter scope +
  one-way spill; `QueryPlan.cs` internal test-only forced-hash seam), the 3 modified B1 test files
  (`ExpressionPlanEqualityComparerNodeTests.cs`, `ExpressionPlanEqualityComparerTests.cs`,
  `PlanKeyStructureTests.cs`), and the internal seam.
- B1 task paths were reverted to the last green tip `752943d` (`git checkout 752943d --` the 5
  `src`/`tests` paths); `git diff --exit-code 752943d -- src tests` is **empty**; `dotnet build
  nextorm.slnx -c Debug` exits **0 / 0 Warning / 0 Error** (`/tmp/nextorm-b1/build-post-revert.log`).

### All other B1 gates were green (only the time-speedup sub-obligation is unmet)

- build 0 W / 0 E; unit suites green (core 1491P/0F/0S, sqlite 993P/0F/1S, postgres 742P/0F, sqlserver
  556P/0F, mysql 257P/0F, mariadb 157P/0F, clickhouse 491P/0F); acceptance **7/7** cases, 0 failures
  (`Prepared_ToList` 779.7 B/op ≤ 780); coverage **line 87.1 % / branch 78.8 %** (≥85/≥75);
  integration **0 failed** (3136 total, 193 capability skips, no provider-skipped); iteration14 gate
  **56/56 row/job verdicts within budget** incl. CTE zero rows; control-normalized allocation
  **−432 B/op reproducible on all 4 targeted arms × 2 rounds** (exactly the removed per-lookup
  `Dictionary`).

**Evidence contract rv=1 (final)** — **G1-PERF `unmet (time-speedup sub-obligation)`**;
**G1-CACHE met**; **G1-CTE N/A** (predicate=false, empty-diff proof above).

- 2026-10-04T19:16Z | ACT | revision r1 | iteration n=1/3 | B1 finalized **incomplete — time-speedup not proved** (escalation decision (b) precondition loadavg(1m)<1.0 unmeetable → fallback (c)); 3 CHECK FAILs (evidence-close gaps; time-speedup not attributed; still not attributed after A2/B2 under load) recorded; r2 infeasibility: loadavg 3.11–4.60, Ryzen 7 5800HS 4C/8T shared, no process control; B1 reverted to 752943d (`git diff --exit-code 752943d -- src tests` empty; build 0W/0E); work preserved as patch (26904 B); G1-PERF unmet (time-speedup), G1-CACHE met, G1-CTE N/A; B2/C pending; branch tip unchanged | docs/specs/status/iteration-15-cached-path-b1.md
