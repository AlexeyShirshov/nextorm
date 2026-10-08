# D174 — JoinIntoSpec.IdentitySelectorCache not cleared by DataContextCache.Clear()

- collection-id: `1.0.9-rc2`
- task_id: `D174`
- github issue: `#174`
- selected_variant: `pdca-dotnet`
- cycle_id: `1`
- plan_revision: `r1`
- iteration: `n2`
- plan_state: **done** (CHECK PASS r1/n2; ACT complete; commit `6bc4ae41`)
- base: branch `1.0.9-rc2` at `18659e41`
- status-file role: child-task status file for the collection; the collection status file `docs/specs/status/collection-1.0.9-rc2.md` is **not** modified by this task.
- footprint (planned, with uncertainty):
  - `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs` (`internal static class JoinIntoSpecHelpers` ~:551; `IdentitySelectorCache` :664) — add internal clear hook. Uncertainty: exact insertion line shifts.
  - `src/nextorm.core/DataContext/DataContextCache.cs` (`Clear()` :125-140; XML doc :112-124) — invoke hook + doc the new cleared store.
  - planned new `tests/nextorm.core.tests/JoinIntoIdentitySelectorCacheTests.cs` + non-parallel collection definition. Uncertainty: registration/in-memory test setup to be confirmed by the planned prerequisite scout.
  - `docs/infrastructure/01-query-reuse-and-caching.md` (+ `docs/ru/**` mirror) — document selector invalidation.
  - `docs/specs/roadmap/todo_navigation_properties.md:352-353` — ACT resolution only after CHECK passes.
  - planned generated artifacts under `TestResults/D174/**` (not source).
- predecessor-result requirements: **none hard**. Clustering must serialize shared-file footprint with **D170** (`JoinIntoSpec.cs`, `EntityMetadataBuilder.cs`) and **D175** (JoinInto tests); an integrated predecessor must supply its diff + CHECK result, not just a closed issue.
- assumptions/prerequisites: `DataContextCache` is process-wide static (`DataContextCache.cs:21`), so its explicit `Clear()` is the correct lifecycle boundary; the compiled selector retains no metadata reference but bakes the key-property choice, so surviving `Clear()` can be observably wrong after key-configuration change; no public API change; clearing is behaviorally safe; atomic concurrent clear/build freshness is explicitly out of scope.
- pre-DO prerequisite (planned, read-only): scout supported metadata registration/re-registration, value-type/composite constraints, reusable in-memory JoinInto setup, exact coverage-workflow commands, and benchmark policy before DO.
- next step: none — D174 ACT complete (see ACT section at end of file).

---

# PLAN — D174 / GitHub #174
**Branch:** `1.0.9-rc2`
**Plan revision:** `r1`, first DO attempt `n1`
**Evidence contract:** `rv1`
**Status:** PLAN only; no implementation or worktree creation authorized by this document.

All existing `file:line` references below are verified scout evidence supplied for this session. New files, methods and evidence artifacts are explicitly **planned**, not existing evidence.

## 1. Goal and scope

Make `DataContextCache.Clear()` invalidate the process-wide identity-selector cache used by `JoinInto`, so subsequent selector construction uses the current metadata.

### Lifetime and correctness assessment

- The cache has process lifetime and retains one entry per distinct CLR `Type` for which a non-null selector was built. It has no expiry or eviction: `JoinIntoSpec.cs:652-664`.
- Growth follows the number of successfully cached types, **not** the number of rows or contexts. Repeated contexts using the same types do not create additional entries.
- Cached delegates retain no metadata object after compilation. However, they retain the **compiled choice of key properties**: `JoinIntoSpec.cs:666-700`.
- Consequently, surviving `Clear()` is not merely a hygiene concern. If the same CLR type is registered afterward with a different key configuration, the old selector can return the wrong identity and affect stitching/grouping.
- `DataContextCache` is itself process-wide, not an instantiated per-context object: `DataContextCache.cs:21`. The appropriate lifecycle boundary is its explicit `Clear()`, not context disposal.

### Included

1. An internal clearing hook on `JoinIntoSpecHelpers`.
2. Invocation of that hook from `DataContextCache.Clear()`.
3. Core regression tests, including changed-key registration and an in-memory `JoinInto` scenario.
4. XML documentation and EN/RU caching-guide updates.
5. Required verification and ACT tracking.

### Excluded

No public API, cache redesign, TTL, capacity limit, per-context cache, query-plan-key change, metadata-registration redesign, SQL change, or atomic synchronization of all stores cleared by `DataContextCache.Clear()`.

## 2. Acceptance criteria, including negative cases

| ID | Acceptance criterion | Negative case / failure signal |
|---|---|---|
| R174-01 | Sequential builds for a registered keyed type reuse the cached delegate. After a **quiescent** `DataContextCache.Clear()`, building while metadata remains absent returns null for every type populated by the test. | A surviving old delegate is returned after metadata has been cleared; removing the new call from `DataContextCache.Clear()` must fail this regression. |
| R174-02 | Re-registering the same type with equivalent metadata after `Clear()` creates a different delegate with equivalent results. Re-registering it with a different single-key property creates a selector returning the new key. | A fixture with distinct old/new key values still returns the old key after re-registration. |
| R174-03 | Missing metadata and keyless metadata return null without poisoning later registration of a keyed mapping. | A previously null result prevents a subsequent selector from being built. |
| R174-04 | Clearing an empty cache, clearing twice, and clearing a cache containing multiple types are safe. Single-key and composite-key behavior is preserved. | Any populated test type still produces its old selector immediately after the global clear; an empty clear throws. |
| R174-05 | Concurrent builds and clears are exception-free and preserve valid results for the unchanged mapping. A final clear **after workers finish** invalidates remaining selectors. | Dictionary races cause exceptions/corruption, or the final quiescent clear fails. No assertion promises atomic freshness during overlapping operations. |
| R174-06 | A representative in-memory `JoinInto` execution groups/stitches correctly after clearing and re-registering metadata. Existing core and selected provider dialect suites pass. | Duplicate/misgrouped/missing results after rebuilding selectors; altered generated SQL or existing regressions. |
| R174-07 | Build passes with warnings treated as errors; changed executable lines are covered; branch delta is reported. Project coverage policy is evaluated at 85% line / 75% branch. | Warnings, uncovered new clearing statements, missing branch evidence, or coverage represented as passing without respecting branch-specific policy. |
| R174-08 | All seven cached-path acceptance benchmarks execute before and after the change, with comparable artifacts and no unresolved regression. | Exit code zero with missing cases is insufficient; a reproducible regression fails CHECK. |
| R174-09 | XML docs and both public guide mirrors accurately describe invalidation; ACT updates issue/roadmap/status only after CHECK passes. | Documentation promises per-context isolation or atomic concurrent reconfiguration; task is marked complete before CHECK. |

**Concurrency boundary:** `.Clear()` on a `ConcurrentDictionary` does not prevent a concurrent builder from inserting afterward. The selected fix establishes the sequential/quiescent lifecycle contract, consistent with the supplied evidence for the existing multi-store `Clear()`. Stronger concurrent metadata-reconfiguration guarantees are outside this task.

## 3. Minimal solution: objective, constraints, optimum

1. **Essential objective:** invalidate compiled identity selectors at the same explicit boundary that invalidates their metadata.
2. **Non-negotiable constraints:** no new public API; preserve null-not-cached behavior; preserve provider independence and normal cached lookup behavior; do not mutate shared query-command cache flags.
3. **Optimum within those constraints:** add one internal hook and one call, following the existing hooked-static pattern.

### Alternatives

| Option | Approach | Advantages | Cost / risk |
|---|---|---|---|
| **A — selected** | Add planned `internal static void Clear()` to `JoinIntoSpecHelpers`; call it from `DataContextCache.Clear()`. | Matches existing static-cache hooks; no new abstraction; no hot-path work; directly fixes the stale-selector lifecycle. | Two small production changes. Does not make clear/build atomic. |
| B | Introduce ownership, generations, or another lifecycle abstraction. | Could support stronger lifetime/concurrency semantics if separately specified. | More concepts and consumers to change; generation checks may enter the lookup path. Not justified by this one consumer. |
| C | Leave the cache and document a trigger. | No immediate production change. | Leaves demonstrable sequential stale-key risk and contradicts the lifecycle requested by the issue. |

**Choice:** A. Place the call alongside the existing helper-cache clearing calls, without claiming a new ordering guarantee: `DataContextCache.cs:125-140`.

### Test observability

Add **only the internal `Clear()` hook**, not `Count` or another observer.

Tests call the existing internal builder through `InternalsVisibleTo`, then observe:

- delegate reference reuse before clear;
- null after global clear removes metadata;
- a new delegate after re-registration;
- the key value produced by that delegate.

This proves invalidation of all types populated by the test without reflection or a product-visible test counter. Tests must exercise **`DataContextCache.Clear()`**, not substitute a direct call to the new helper hook.

## 4. Findings: fix now versus deferred

| Finding | Decision | Trigger / disposition |
|---|---|---|
| Static selector cache omitted from global clear: `JoinIntoSpec.cs:664`, `DataContextCache.cs:125-140` | **Fix now** | Hook and global call. |
| Cached selector can embody obsolete key configuration: `JoinIntoSpec.cs:668-699` | **Fix now** | Changed-key regression proves re-derivation. |
| No dedicated regression coverage | **Fix now** | New isolated core test fixture. |
| Null results deliberately uncached: `JoinIntoSpec.cs:648-651` | **Preserve now** | Explicit absent/keyless registration tests. |
| Unbounded growth between explicit clears | **Deferred** | Investigate bounded/weak storage if measured workloads demonstrate continuing distinct-type growth or require collectible-type unloading. This fix does not claim to bound a process that never clears. |
| Atomic clear/build/metadata replacement | **Deferred** | Revisit if a supported requirement is introduced for concurrent metadata reconfiguration with post-clear freshness. Requires a cross-store contract, not just a dictionary change. |
| Dedicated `Count` observer | **Do not add** | Reconsider only if a second consumer needs direct occupancy diagnostics not expressible through behavior. |
| Automatic Stryker execution | **Deferred; see §8** | No mutation tooling/configuration is supplied, and introducing it is disproportionate to this two-statement hookup. Mandatory targeted negative-control mutation remains in scope. |

## 5. What the issue did not specify: assumptions and prerequisites

| Gap | Resolution |
|---|---|
| “Context cache lifetime” could mean instance disposal. | Verified evidence establishes process-wide `DataContextCache`. Use its explicit `Clear()`, not an invented instance lifetime. |
| Whether stale selectors are equivalent after metadata changes. | They are equivalent only when the key configuration is equivalent. Changed-key metadata makes the compiled selector observably wrong. |
| Registration APIs usable by tests, including replacement of keyless metadata. | **Pre-DO targeted scout prerequisite.** Identify existing supported test setup using Roslyn; do not invent an API or modify registration behavior to make the test possible. |
| Value-type metadata support and composite identity representation. | Scout existing generic constraints/metadata paths. Close supported variants with tests; unsupported variants with actual constraints/guards. Do not infer support from the dictionary’s `Type` key. |
| Exact project coverage invocation and benchmark policy. | Scout the relevant configuration/workflow before DO. Retain 85/75 policy and the seven-case benchmark obligation. |
| Whether the guide enumerates every cleared store. | Update EN/RU regardless: amend the enumeration if present; otherwise add a concise behavior sentence at the supplied `Clear()` paragraph. |
| Atomic semantics under concurrent reconfiguration. | Explicit risk, not an acceptance relaxation: guarantee quiescent invalidation only. |
| Global skill/overlay details beyond the supplied extract. | Orchestrator must verify the mandatory evidence-contract/gate clauses before DO. Any additional mandatory slots or checks must be added without weakening this plan. |

**Pre-DO prerequisite call — planned, read-only:**

`Task(subagent_type="scout", prompt="D174 prerequisite evidence only. Using Roslyn first for C# symbols, identify supported metadata registration/re-registration setup in core tests, value-type constraints, composite identity result shape, and an existing in-memory JoinInto test/setup. Read only relevant pdca-dotnet/nextorm overlay evidence-contract and gate clauses, coverage workflow/settings/tool configuration, benchmark acceptance configuration, and the EN/RU Clear paragraphs. Return verified file:line, exact existing commands/policies, and any hard incompatibility; no edits or recommendations.")`

If registration incompatibility invalidates a required test, return to planner with that concrete evidence. Do not silently omit changed-key or null-reprobe coverage.

## 6. Footprint, uncertainty, and predecessor-result requirements

### Planned footprint

| File / area | Planned change | Uncertainty |
|---|---|---|
| `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:551,652-700` | Internal hook adjacent to the existing cache. | Exact insertion line will shift; builder logic remains unchanged. |
| `src/nextorm.core/DataContextCache.cs:112-140` | Hook invocation and XML-doc entry. | None at design level. |
| **Planned:** `tests/nextorm.core.tests/JoinIntoIdentitySelectorCacheTests.cs` | New lifecycle/concurrency/in-memory regression fixture and local collection definition. | Existing registration and JoinInto setup must be identified by prerequisite scout. |
| `docs/infrastructure/01-query-reuse-and-caching.md:187,197` | Explain selector invalidation/rebuilding. | Enumeration versus paragraph editing depends on existing prose. |
| RU mirror of that guide | Equivalent update. | Exact lines are not supplied; locate through documentation-only scouting. |
| `docs/specs/roadmap/todo_navigation_properties.md:352-353` | ACT resolution and assessment. | Only after successful CHECK. |
| `docs/specs/status/collection-1.0.9-rc2.md:38` | Persist D174 plan/contract and lifecycle status. | Use the collection’s existing child-status representation. |
| **Planned generated artifacts:** `TestResults/D174/**` | Logs, coverage, benchmark reports, mutation evidence. | Confirm ignored-artifact handling; do not add generated reports to source changes. |

**Not planned:** `EntityMetadataBuilder.cs`, provider production files, dependencies, public signatures, generated documentation, or unrelated JoinInto tests.

### Predecessor-result requirements

There is **no hard predecessor output**.

Collection clustering must nevertheless account for:

- **D170:** shared `JoinIntoSpec.cs`; its metadata work also touches `EntityMetadataBuilder.cs`. Serialize overlapping production-file changes. If D170 integrates first, verify its resulting metadata behavior before D174 tests are finalized.
- **D175:** JoinInto test/setup overlap and global-cache interference. Keep D174 in its own fixture, serialize conflicting shared test-file edits, and rerun D174 after integration.

An integrated predecessor must supply its actual resulting tree/diff and CHECK result, not merely an issue marked done. D174 does not require either predecessor to complete before starting.

## 7. DO tasks and unit execution mode

### D: tasks

1. **D174.1 — establish prerequisites and freeze evidence contract.**
   Obtain §5 evidence; coder persists this plan and contract in the collection status **before production/test edits**. Confirm clean ownership of the shared footprint and applicable normal/autonomous authorization gate.

2. **D174.2 — implement the lifecycle hook.**
   **Fix now:** `JoinIntoSpec.cs:551,664` and `DataContextCache.cs:125-140`. Add the internal hook and call. Leave lookup, null handling, compilation, and query-command state unchanged.

3. **D174.3 — add isolated regression coverage.**
   **Fix now:** planned core test fixture. Use the non-parallel global-clear collection pattern evidenced by `DataContextCacheClearTests.cs:12` and `QueryCacheControlsTests.cs:9`; clear at setup/cleanup. No reflection or occupancy hook.

4. **D174.4 — update documentation.**
   **Fix now:** XML documentation and both public guide mirrors. Record lifecycle assessment in child status. Do not mark roadmap/issue resolved yet.

5. **D174.5 — collect implementation evidence.**
   Build, focused and complete selected suites, coverage/branch delta, targeted negative control, and paired acceptance benchmarks.

6. **D174.6 — submit to CHECK; ACT only after pass.**
   Update roadmap/status and GitHub issue according to the collection’s ACT policy. No commit, merge, or push is authorized here.

### Unit execution mode

**Single unit, sequentially in one existing tree. No worktree sub-tasks.**

Production files and a process-wide cache contract overlap; splitting implementation/tests would not buy isolation. Test-worker concurrency in one regression is not parallel unit execution.

Preserve CRLF in all changed files, normalize edits, and honor nullable/analyzer warnings-as-errors. No packages are needed.

## 8. Test strategy and execution-path variant matrix

### Planned test project and symbols

All new tests belong to `tests/nextorm.core.tests`. Planned fixture: `JoinIntoIdentitySelectorCacheTests`.

Planned method names—not existing symbols:

- `CachesAndRebuildsSingleAndCompositeKeySelectors`
- `ClearInvalidatesSelectorsForAllPopulatedTypes`
- `ClearRebuildsSelectorAfterKeyConfigurationChanges`
- `MissingMetadataDoesNotPoisonLaterRegistration`
- `KeylessMetadataDoesNotPoisonLaterKeyRegistration`
- `ClearIsIdempotentForAnEmptyCache`
- `ConcurrentBuildAndClearRemainSafe`
- `JoinIntoGroupsCorrectlyAfterClear`

These are unit/core behavioral tests. The last test executes the in-memory path; it is not a database integration test.

### Variant matrix

| Variant / execution path | Closure |
|---|---|
| Registered reference type, one key | **Test:** warm reference reuse, invalidation, equivalent rebuilding. |
| Registered type, composite keys: `JoinIntoSpec.cs:672-699` | **Test:** preserve identity behavior before/after clear using the actual existing representation. |
| Multiple distinct keyed types | **Test:** populate at least two types; immediately after clear both return null while metadata is absent. |
| Missing metadata: `JoinIntoSpec.cs:668` | **Test:** null, then successful registration/build without an intervening clear. |
| Registered keyless type: `JoinIntoSpec.cs:678-679` | **Test:** null, then supported registration of keyed metadata without clearing the selector cache. |
| Empty cache; repeated clear | **Test:** no exception, subsequent keyed registration/build succeeds. |
| Same CLR type, changed single-key configuration | **Test:** fixture contains different old/new values; post-clear selector returns the new one. |
| Key property whose value is null, if mapping permits it | **Test:** preserve the existing selector result before/after rebuilding. If prohibited, **guard evidence** from actual mapping validation. |
| Value-type entity | **Test** if the metadata/JoinInto constraints admit it; otherwise **guard evidence** from those constraints. No fabricated unsupported mapping. |
| Null entity passed to a compiled selector | **Guard:** this fix does not change selector invocation or introduce null-input semantics. Existing supported caller behavior must remain unchanged; no promise of accepting null entities. |
| Concurrent build and clear | **Test:** fixed bounded workers/iterations, no concurrent metadata replacement; results are either null or valid for the one mapping. Final quiescent clear must invalidate. |
| Spec constructor callers: `JoinIntoSpec.cs:222,351,468` | **Guard:** unchanged delegation to the same helper; full core suite. |
| Stitch/execution callers: `JoinIntoStitcher.cs:120`, `EntityBuilderEagerLoading.cs:150` | **Test:** representative in-memory JoinInto regression; **guard:** remaining callers unchanged. |
| Provider independence | **Test:** complete SQLite, PostgreSQL and SQL Server dialect suites; **guard:** cache remains keyed only by CLR type and has no provider/SQL branch. |
| MySQL, MariaDB, ClickHouse-specific permutations | **Guard:** no provider production code, SQL builder, or provider discriminator changes. No new provider-specific behavioral claim. |

### Integration/container decision

**No container-backed integration run is required for D174.** The change is a core static-cache lifecycle hookup; SQL generation, database execution and provider mapping rules are untouched.

The selected provider projects are database-free dialect tests. Do not describe them as container integration evidence or describe unrun container providers as passing. If the implementation expands into database/provider behavior, return to PLAN and add the required container evidence.

### Coverage and branch delta

- Capture baseline and final coverage using the repository’s existing workflow invocation identified by the prerequisite scout.
- Apply **85% line / 75% branch** policy: hard gate on `main`, warning/reporting on `1.0.9-rc2`. Do not turn branch warnings into an invented hard repository gate.
- Independently require both new clearing statements to be exercised by the regression.
- Report changed-line and changed-branch coverage explicitly. The hook/call should introduce no conditionals.
- Cover relevant existing selector outcomes: hit/miss, missing metadata, no keys, single/composite keys. Do not claim complete helper branch coverage without the report.

### Mutation-testing plan

**Mandatory targeted negative control:** temporarily omit the new global-clear invocation in the owned patch, run the focused regression, observe failure, then restore and rerun successfully. If useful, repeat by making the helper hook a no-op. Never mutate unrelated collection changes.

**Stryker.NET automation deferred:** no existing configuration/invocation is supplied; adding mutation tooling solely for two clearing statements would expand scope without improving this direct coupling check.

**Trigger:** execute targeted Stryker.NET when a collection mutation lane/configuration is available, or when this work expands into selector lookup/compilation branches or a concurrency-generation mechanism. Record the deferred item and trigger; do not claim a mutation score.

## 9. Priority matrix

Issue severity remains **P2**. CHECK row priority is separate.

No project class-priority table was supplied. Apply the execution-path/invariant fallback:

| Evidence rows | Priority | Basis |
|---|---|---|
| E174-01 prerequisite/gate evidence | P1 gate | Required before DO; prevents invented registration/tooling contracts. |
| E174-02–E174-05 lifecycle, key changes, null behavior, in-memory execution | **P1** | Execution correctness and explicit acceptance invariants. |
| E174-06 build and selected suites | **P1** | Build discipline and regression protection. |
| E174-07 coverage/branch evidence | **P1** for evidence completeness | Numerical enforcement remains branch-specific. |
| E174-08 negative control | **P1** | Demonstrates detection of the original defect. |
| E174-09 paired performance evidence | **P1** | Mandatory cached-path verification for this plan. |
| E174-10 docs and ACT | P2, mandatory | Lifecycle documentation/tracking, not an optional deliverable. |

CHECK must not downgrade execution-path P1 rows or waive a mandatory row because another suite passes.

## 10. Documentation plan

1. **XML:** update `DataContextCache.Clear()` documentation at `DataContextCache.cs:112-124` to include JoinInto identity-selector invalidation.
2. **EN/RU guides:** update the supplied caching-guide paragraphs and mirror. Amend a store enumeration if present; otherwise add a short sentence that selectors are rebuilt from current metadata after clearing.
3. Avoid exposing the private cache as a public API, promising per-context isolation, or claiming atomic concurrent reconfiguration.
4. **ACT:** update the roadmap entry at `todo_navigation_properties.md:352-353` with the lifecycle assessment and implemented resolution after CHECK passes.
5. No public rename, numbered-page changes, DocFX generation, or public links to internal specs.

## 11. Performance-measurement decision

**Required: paired seven-case cached-path acceptance benchmark.**

The hook itself runs only during explicit clear: `DataContextCache.cs:125-140`. No new operation is added to normal lookup: `JoinIntoSpec.cs:652-661`. Nevertheless, this changes a cache used by declaration/stitching paths, so this plan takes the mandatory cache/query-path acceptance route rather than exempting it.

- Baseline: current integrated tree, before D174 implementation, Release.
- Final: same host/runtime and benchmark configuration, after implementation.
- Exact suite command is defined below; all **seven cases** must execute.
- Retain means, reported errors and allocation columns where available.
- Apply any existing repository acceptance policy found by prerequisite scout.
- If no numerical policy exists, use a declared noise-envelope check: flag a case where final mean exceeds baseline mean plus both reported errors, or reported allocations increase. One paired repeat may investigate noise; an unresolved/reproducible change returns to PLAN.
- Exit code alone is not a performance conclusion.

## 12. Reconnaissance decision

**One targeted prerequisite scout is required; no implementation spike is required.**

It proves only the missing mechanics listed in §5: supported registration/re-registration, generic constraints, existing in-memory setup, and exact mandatory tooling/policy.

**Observable completion:** a source-backed report containing the requested APIs/constraints, existing commands, relevant documentation statements, and mandatory contract clauses. No speculative recommendations.

The architecture choice needs no experiment: the existing hooked-static pattern is already evidenced. If the scout reveals incompatible registration semantics or a stronger documented concurrency guarantee, that concrete result is a replanning input.

## 13. Design-checklist result

| Lens | Result |
|---|---|
| SOLID / responsibilities | **Clean:** helper owns clearing its private dictionary; central cache lifecycle calls the owner. |
| DRY / abstraction threshold | **Clean:** follows existing hooks; no generic cache abstraction for one additional consumer. |
| Type/API design | **Clean:** internal method only; no count observer, public API, dependency or new cache type. |
| Performance anti-patterns | **Clean by construction:** no per-row work, lookup lock, generation check, reflection, or extra allocation added to the cached path. |
| Static state/lifecycle | **Finding fixed:** omitted static-store invalidation. Remaining non-atomic concurrent replacement is explicitly scoped out. |
| Sealedness | **Clean:** existing helper is static; no new production inheritable type. New test fixture should be sealed if compatible with existing conventions. |
| Query-cache invariants | **Clean:** no shared `QueryCommand.Cache` mutation, plan-key change or provider metadata-cache seeding. |
| Build/repository hygiene | **Required guards:** warnings-as-errors, CRLF, no new package versions, no unauthorized commit/push/merge. |

## 14. Versioned evidence contract — `rv1`

### Contract conventions

All entries below are mandatory obligations, except the explicitly deferred **automatic Stryker** item in §8. Applicability is observable, not based on whether a report happens to exist.

**Sources marked “planned” are not current evidence.** Proposed test symbols carry no invented `file:line`.

Owners are responsibility streams within the **single sequential unit**, not parallel worktrees:

- `SCOUT`: prerequisite facts;
- `DO-CORE`: production change and negative-control patch;
- `DO-TEST`: test/coverage evidence;
- `DO-DOC`: documentation and status;
- `DO-PERF`: benchmark evidence;
- `CHECK`: independent evaluation and re-gather.

### Exact invocation definitions

Commands run from the repository root; capture complete stdout/stderr and actual exit code in the named artifact. Logging must preserve the command’s exit status.

| Invocation ID | Exact command / call |
|---|---|
| I174-01 | The exact `Task(subagent_type="scout", prompt=...)` call in §5. |
| I174-02 | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~JoinIntoIdentitySelectorCacheTests"` |
| I174-03 | `dotnet test tests/nextorm.core.tests -c Debug` |
| I174-04 | `dotnet test tests/nextorm.sqlite.tests -c Debug` |
| I174-05 | `dotnet test tests/nextorm.postgres.tests -c Debug` |
| I174-06 | `dotnet test tests/nextorm.sqlserver.tests -c Debug` |
| I174-07 | `dotnet build src/nextorm.core/nextorm.core.csproj -c Debug` |
| I174-08 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --artifacts TestResults/D174/perf/baseline` |
| I174-09 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance --artifacts TestResults/D174/perf/final` |
| I174-10 | `Task(subagent_type="coder", prompt="D174 negative control: in the owned D174 patch only, temporarily omit the JoinIntoSpecHelpers clearing call from DataContextCache.Clear. Run I174-02 and record the expected lifecycle-test failure and exit code. Restore the call, rerun I174-02, and record success. Preserve unrelated changes and CRLF. Do not leave the mutation applied.")` |
| I174-11 | `Task(subagent_type="coder", prompt="D174 coverage: execute the exact existing coverage workflow commands returned by I174-01, unchanged except for D174 artifact destinations. Capture baseline and final reports, 85/75 branch-policy evaluation, changed-line/branch delta, and selector outcome coverage. Report command strings and exit codes; do not substitute focused-only coverage for project coverage.")` |
| I174-12 | `Task(subagent_type="scout", prompt="D174 final evidence audit only: inspect the owned diff and EN/RU documentation. Verify the helper hook/global call, unchanged hot path and callers, no public API or provider changes, CRLF, XML and guide accuracy, and ACT status. Use Roslyn first for C# symbols. Return file:line and diff-backed findings, no edits.")` |

**Coverage binding prerequisite:** I174-11 is an exact delegated invocation, but its underlying shell commands are not asserted as known. I174-01 must supply them, and coder must record their literal strings in the contract before implementation. This is command-binding refinement, not permission to omit coverage or reset `r1/n1`. If binding requires different verification scope or tooling changes, return to planner.

### Contract rows

| Row ID / requirement ID / `rv` | Required scenario and evidence kinds | Expected sources, invocation and result | Planned artifacts | Owner | Applicability predicate |
|---|---|---|---|---|---|
| **E174-01 / R174-01–09 / rv1** | Prerequisite source evidence; mandatory skill/gate compatibility; registration/constraints/tool commands. | **Planned:** scout report via I174-01. Required facts resolved with verified sources; unsupported variants identified by real guards. | `TestResults/D174/prerequisites.md`; frozen contract in collection status. | SCOUT; DO-DOC persists | Always, before implementation. |
| **E174-02 / R174-01,04 / rv1** | Cache hit, multiple-type invalidation, equivalent rebuild, empty/repeated clear. Behavioral assertions and test execution. | **Planned:** new core fixture; I174-02 exit **0**, named scenarios actually executed, no skips. | `TestResults/D174/focused-tests.log`; test result output. | DO-TEST | Always. |
| **E174-03 / R174-02,03 / rv1** | Changed-key registration; absent/keyless null re-probe. Behavioral assertions. | **Planned:** new core fixture; I174-02 exit **0**; distinct old/new values and successful post-null registration demonstrated. | Focused log plus source-backed assertion audit. | DO-TEST | Always. |
| **E174-04 / R174-04,05 / rv1** | Composite/null/value variants and bounded concurrency. Tests or actual unsupported-input guards. | **Planned:** fixture and prerequisite constraint sources; I174-02 exit **0**. Concurrent test must execute; conditional variants must have a test or verified guard. | Focused log; `TestResults/D174/variant-matrix.md`. | DO-TEST | Concurrency/composite always; value/null-key test applies when supported by observed mapping constraints. |
| **E174-05 / R174-06 / rv1** | In-memory JoinInto rebuild/grouping and unchanged remaining callers/provider independence. Test plus structural evidence. | **Planned:** fixture, final audit; I174-02 and I174-12. Exact expected group membership/counts pass; no altered caller/SQL branch. | Focused log; `TestResults/D174/final-audit.md`. | DO-TEST; SCOUT | Always. |
| **E174-06 / R174-06,07 / rv1** | Build and complete selected test suites. Execution evidence. | I174-03–07, each exit **0**, no warnings/build errors or unexplained skips. | Separate `core`, `sqlite`, `postgres`, `sqlserver`, and `build` logs under `TestResults/D174/`. | DO-TEST | Always. |
| **E174-07 / R174-07 / rv1** | Baseline/final coverage, changed-line/branch delta, repository threshold evaluation. Machine reports plus assessment. | **Planned:** existing workflow commands bound by E174-01; I174-11. Collection/report generation exits **0**; new statements covered; 85/75 evaluated using branch policy, warnings explicitly recorded. | `TestResults/D174/coverage/{baseline,final}/`; command/exit log; delta assessment. | DO-TEST; CHECK evaluates | Always. |
| **E174-08 / R174-01,02 / rv1** | Negative-control mutation detects original omission; restored implementation passes. | I174-10: mutated I174-02 exits **nonzero because of the lifecycle regression**, restored I174-02 exits **0**. Tool/setup failures do not count as detection. | `TestResults/D174/mutation-negative.log`; restoration/diff confirmation. | DO-CORE; DO-TEST | Always. |
| **E174-09 / R174-08 / rv1** | Paired cached-path benchmark; seven cases; timings/errors/allocations. | I174-08 and I174-09 each exit **0**, all seven cases present, no benchmark errors; comparison meets §11 policy or returns an explicit failure. | `TestResults/D174/perf/{baseline,final}/`; comparison report and command logs. | DO-PERF; CHECK evaluates | Always for this plan. |
| **E174-10 / R174-09 / rv1** | XML, EN/RU guide, scope/API/CRLF audit, and gated ACT tracking. Source/diff evidence. | I174-12 plus coder’s ACT status update. Both language mirrors and XML accurate; no completion claim before CHECK pass. | Final audit; persisted plan/status; ACT issue/roadmap record. | DO-DOC; SCOUT; CHECK | Docs always; ACT completion only after passing CHECK. |

### CHECK re-gather budget and ownership

**Owner: CHECK. Budget: two targeted re-gather rounds total for this revision.**

1. Round 1: recover a specifically missing source, result, artifact, or command exit.
2. Round 2: independently verify one remaining ambiguity; it may include the single paired benchmark repeat allowed in §11.

This does not authorize two full implementation retries. Missing evidence is **not** a passing row, a reason to weaken the contract, or by itself a reason to start another DO iteration.

After exhaustion, route unresolved evidence to planner; persistent low confidence recommends `escalate` under trigger 5.

### Revision and supersession rules

`rv1` has no predecessor contract.

Any justified future contract revision must explicitly name the superseded revision, preserve these row/requirement IDs and obligations, and add IDs for newly discovered variants. A real implementation-plan change increments `r` and resets `n`; evidence binding/clarification alone does neither. No renaming or session reset constitutes replanning.

## 15. Confidence, risks, and return-to-PLAN classification

**High confidence:** lifecycle mismatch, stale-key correctness risk, selected hook pattern, internal test access, provider-independent production scope.

**Unresolved before DO:** supported registration mechanics, value/null-key constraints, exact coverage/tooling clauses, benchmark policy, and reusable in-memory fixture setup. These are bounded prerequisite facts, not permission to infer APIs.

Principal risks:

- Global-clear tests interfere with other tests: use a disabled-parallelization collection and cleanup.
- Concurrent builders may repopulate after a clear: explicitly do not promise atomic freshness; test final quiescent invalidation.
- D170/D175 integration invalidates assumptions: serialize overlap and rerun affected evidence.
- Benchmark noise: retain baseline/error data and use the finite repeat budget.

**If DO returns a blocker:**

- Missing supported setup/tool prerequisite → classify as **in-cycle prerequisite**; preserve the original D174 acceptance and remaining work as active, blocked on the added dependency.
- Evidence uncertainty → **targeted scout first**.
- Concurrent atomicity concern without a new requirement → **explicit accepted risk**, not a reason to expand scope.
- Demonstrated authority/resource blocker → recommend orchestrator call **`escalate`**.
- Persistent low confidence after bounded gathering → **`escalate`, trigger 5**, even if externality is not proven.

A DO report never marks D174 done. Completion requires CHECK and the gated ACT update.

---

# DO progress log (append-only)

Format: `<UTC time> | <phase> | revision r | iteration n/3 | <event> | <evidence pointer>`

- `2026-10-08T11:31:xxZ | DO | r1 | n1/3 | DO started after PLAN (plan_state=ready); DO brief/scope validated fail-closed before any edit | /tmp/opencode/rc2-174/scope.json; validate_inner_loop.py brief exit 0 (initial brief JSON lacked the required unit/scope wrapper; re-wrapped with identical fields, not a scope change)`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | D174.1 prerequisite facts recorded (read-only) | see findings below`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | D174.2 lifecycle hook + global call implemented | JoinIntoSpec.cs:664-672 (ClearIdentitySelectorCache); DataContextCache.cs:140 (call)`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | D174.3 isolated core fixture added (8 methods, R174-01..05 incl. negative control + quiescent concurrency) | tests/nextorm.core.tests/JoinIntoIdentitySelectorCacheTests.cs`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | D174.4 docs: XML on DataContextCache.Clear + EN/RU guide mirrors | DataContextCache.cs:112-124; docs/infrastructure/01-query-reuse-and-caching.md:187; docs/ru/infrastructure/01-query-reuse-and-caching.md:187`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | focused fixture green after fix | I174-02: 8 selected / 8 passed / 0 failed, exit 0 (/tmp/opencode/rc2-174/focused-restored.log)`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | targeted negative control (fix removed): lifecycle regression detected | I174-10 mutation: 8 selected / 5 failed / 3 passed, exit 2 (/tmp/opencode/rc2-174/mutation-negative.log); restored: 8/8 exit 0`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | DO->CHECK boundary sweep single run: full core + 3 dialect suites green | core 1765/0/0 exit 0; sqlite 1160 passed/1 skipped exit 0; postgres 803/0/0 exit 0; sqlserver 727/0/0 exit 0`
- `2026-10-08T11:32:55Z | DO | r1 | n1/3 | D174.5 (coverage/branch delta + paired acceptance benchmark) and D174.6 (CHECK/ACT) NOT executed in this DO slice; remain pending, D174 not done | logs under /tmp/opencode/rc2-174/`

## D174.1 prerequisite findings (verified file:line)

- Metadata registration/re-registration: `DataContextCache.Metadata` is a public seam (`DataContextCache.cs:51`); `EntityMetadataBuilder<T>.Build()` (`EntityMetadataBuilder.cs:27`) builds it; tests already inject it directly (`RelationshipMetadataTests.cs:351`, `JoinIntoRejectionTests.cs:83`). `From<T>()` (`DataContextExtensions.cs:1019`) calls `ResolveMetadata` (`:220`), which rebuilds when the entry is absent or its table name is empty (`:258`), so re-calling `From<T>()` after `DataContextCache.Clear()` re-registers metadata.
- Key configuration: `[Key]` attribute and fluent `.Key()`; the `Id`/`<Type>Id` convention only runs on the auto-build path (`EntityMetadataBuilder.cs:293-436`), not the declared-property path (`:60`), so declared mappings control `IsKey` precisely.
- Composite identity: declaring two key properties yields the composite branch and the internal `JoinIntoKey` (`JoinIntoSpec.cs:666-700`, struct at `:737`).
- In-memory JoinInto setup: `ctx.From<T>(b => b.HasMany(...)).WithData([...])` (`JoinIntoInMemoryTests.cs:12-29`); `JoinInto` constructs `_childIdentity` in the spec constructor (`JoinIntoSpec.cs:222,351,468`), stitcher uses `BuildIdentitySelector<TEntity>` (`JoinIntoStitcher.cs:120`).
- Non-parallel global-clear collection: reuse `[Collection("Query cache controls")]` (`QueryCacheControlsTests.cs:9-19`); the new fixture uses it.
- Coverage-command binding (I174-11): `.github/workflows/dotnet.yml:37-55` — `mkdir -p tests/coverage` then `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`, then `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura"`; thresholds `MIN_LINE_COVERAGE=85`/`MIN_BRANCH_COVERAGE=75`, hard only on `main` (`:27-28,63-76`).
- Benchmark policy: acceptance filter `--anyCategories=acceptance`; observed **8** `[BenchmarkCategory("acceptance")]` attributes, while the plan states **seven** cases — recorded as an evidence-binding discrepancy for CHECK (perf evidence is D174.5, not run here).
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 build evidence: full-solution Debug and Release, each 0 warnings / 0 errors | exit 0; /tmp/opencode/rc2-174/build-debug.log; /tmp/opencode/rc2-174/build-release.log`
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 coverage per .github/workflows/dotnet.yml:37-55 (coverage.settings.xml scope core/sqlite/postgres/sqlserver): Line 86.7% (>=85), Branch 79.3% (>=75); thresholds met (rc2 = warn-only) | coverage collect exit 0; /tmp/opencode/rc2-174/coverage.log; reportgenerator exit 0; TestResults/D174/coverage/Summary.txt; only reportgenerator -targetdir adapted`
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 branch delta: changed product lines covered (JoinIntoSpec.cs:672 hits=1; DataContextCache.cs:140 hits=1); change adds no conditional/branch rows; new fixture is a test assembly, excluded by coverage ModulePaths | TestResults/D174/coverage/coverage.cobertura.xml`
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 acceptance final (I174-09): 7/7 executed, 0 failures, exit 0; wall 49 s; BDN global 47.25 s; cached/prepared time ratio 1.98, alloc ratio 7.66 | /tmp/opencode/rc2-174/acceptance-final.log; TestResults/D174/perf/final; TestResults/D174/perf/summary.md`
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 baseline (I174-08) produced without touching tree via detached worktree at 5b7fb9e7 (verified no ClearIdentitySelectorCache): 7/7, exit 0, wall 51 s, BDN global 40.51 s, ratio 2.12, alloc ratio 7.66; worktree removed after copy | /tmp/opencode/rc2-174/acceptance-baseline.log; TestResults/D174/perf/baseline`
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 case-count fact: 8 acceptance attributes in source; 8th is #168 SqlServerBufferedNumericAcceptanceBenchmark, whose acceptance/**/*.cs is <Compile Remove>-ed from the normal build, so not selected; true executed count 7; all names match acceptance-benchmarks.md, none new/renamed | docs/specs/performance/acceptance-benchmarks.md; benchmarks/nextorm.benchmark/nextorm.benchmark.csproj`
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 artifact-path fact: --artifacts ignored (NextormConfig pins ArtifactsPath; BenchmarkArtifacts looks for nextorm.sln, absent since repo uses nextorm.slnx) so reports landed in the tracked CWD BenchmarkDotNet.Artifacts; copied to TestResults/D174/perf/{final,baseline} and `git restore`d the tracked reports | git status`
- `2026-10-08T11:39:00Z | DO | r1 | n1/3 | D174.5 evidence complete; D174.6 CHECK/ACT still pending, D174 not done | logs under /tmp/opencode/rc2-174/`
- 2026-10-08T11:42:56Z | DO | r1 | n1/3 | boundary evidence re-expressed in the accepted xunit v3 native-runner form (host gate caps unfiltered `dotnet test` boundary sweeps at 1): `dotnet run --project tests/nextorm.{core,sqlite,postgres,sqlserver}.tests -c Debug --no-build -- -noColor`; core 1765 total/0 failed/0 skipped exit 0, sqlite 1161/0/1 skip exit 0, postgres 803/0/0 exit 0, sqlserver 727/0/0 exit 0; literal `dotnet test` sweeps retained as corroboration; report validator exit 0 | boundary-run-{core,sqlite,postgres,sqlserver}.log; evidence.json; evidence-validate.log (all /tmp/opencode/rc2-174/)
- `2026-10-08T11:47:49Z | DO | r1 | n2/3 | CHECK->DO loop-back attempt n2 (same plan, no replan): closed both open variant rows with real tests, no scope drift. null-key-value -> test; value-type entity -> test incl. struct-parent in-memory JoinInto stitch | tests/nextorm.core.tests/JoinIntoIdentitySelectorCacheTests.cs:203,227`
- `2026-10-08T11:47:49Z | DO | r1 | n2/3 | E174-04 variant-matrix artifact produced: every plan section-8 row closed by test or diff-backed guard; residual open rows = none; automatic Stryker remains deferred per plan (not a variant row) | TestResults/D174/variant-matrix.md`
- `2026-10-08T11:47:49Z | DO | r1 | n2/3 | focused inner loop re-run (I174-02, unchanged selector): 10 selected / 10 passed / 0 failed / 0 skipped, exit 0; no warnings | TestResults/D174/focused-n2.log`
- `2026-10-08T11:47:49Z | DO | r1 | n2/3 | targeted container integration permitted: integration project build 0 warnings / 0 errors exit 0 | TestResults/D174/integration-build.log`
- `2026-10-08T11:47:49Z | DO | r1 | n2/3 | targeted container-backed integration (JoinInto subset, DOCKER_HOST set, --no-build): SQLite 17/17, PostgreSQL 17/17, SQL Server 17/17, MySQL 17/17; 0 failed / 0 skipped each, exit 0 each (skips are not pass) | TestResults/D174/integration-{sqlite,postgres,sqlserver,mysql}-joininto.log`
- `2026-10-08T11:47:49Z | DO | r1 | n2/3 | DO n2 complete; D174 NOT done (submit to CHECK) | docs/specs/status/rc2-174-identity-selector-cache-1.md`
- `2026-10-08T11:49:45Z | DO | r1 | n2/3 | boundary evidence refreshed on final n2 tree (machine-checkable): core 1767/0/0 exit 0, sqlite 1161/0/1 skipped exit 0, postgres 803/0/0 exit 0, sqlserver 727/0/0 exit 0; focused inner 10/10 exit 0; evidence.json unit/scope wrapper unchanged; validate_inner_loop.py report exit 0 (clean stdout) | /tmp/opencode/rc2-174/boundary-run-{core,sqlite,postgres,sqlserver}-n2.log; inner-run-n2.log; evidence.json; evidence-validate.log`
- `2026-10-08T11:57:26Z | DO | r1 | n2/3 | D174 evidence materialization: materialized every planned E174 artifact so each row points at a real file under TestResults/D174/; no product/test/doc edit | E174-01 prerequisites.md; E174-02/03/04 focused-tests.log (+focused-n2.log, focused-tests-pre-n2.log, focused-inner.log); E174-05/10 final-audit.md; E174-06 core.log/sqlite.log/postgres.log/sqlserver.log/build-debug.log/build-release.log; E174-07 coverage/baseline/ + coverage/final/ + coverage/delta.md; E174-08 mutation-negative.log. Baseline detached worktree at 5b7fb9e7: build 0 warnings/0 errors (exit 0), collect 9121 total/2 pre-existing unrelated ParamRefreshRecipe failures (exit 2), line 86.7% (47224/54448), branch 79.3% (25529/32181); final line 86.7% (47230/54450), branch 79.3% (25535/32181); new lines JoinIntoSpec.cs:672 and DataContextCache.cs:140 covered (hits=1), 0 new branch rows; worktree removed after copy; D174 NOT done (ACT pending CHECK)`
- `2026-10-08T12:01:46Z | DO | r1 | n2/3 | D174 evidence-binding pass (escalate-directed): created final-aggregate.md (E174-01..10 table + pinned contract rows 358-369 verbatim + C-E01..C-E04 verdicts + priority matrix + ClickHouse N/A + git diff --stat src); appended prerequisites "Verification results" (frozen contract rc2-174:358-369); extended variant-matrix with 10-test traceability (line span :86..:320 re-verified, unchanged); added perf baseline-vs-final Mean(±Error)/Allocated table (final alloc ratio 7.66 = baseline alloc ratio 7.66, no allocation increase); no src/tests edit, no commit; r/n unchanged; validate_inner_loop.py report exit 0 | TestResults/D174/final-aggregate.md; prerequisites.md; variant-matrix.md; perf/summary.md; /tmp/opencode/rc2-174/evidence.json (validator exit 0)`
- `2026-10-08T12:13:27Z | CHECK | r1 | n2/3 | CHECK evidence-gap closure (final CHECK artifact pass; no r/n change, no src/tests edit): ran the identical JoinInto container selector against ClickHouse — `FullyQualifiedName~JoinInto&FullyQualifiedName~ClickHouseIntegrationTests` selects 0 tests (exit 8; ClickHouse does not derive CommonTestSuite and has no JoinInto_ tests), so ran the ClickHouse provider suite instead (117/117, 0 failed / 0 skipped, exit 0); quoted R174-05 `ConcurrentBuildAndClearRemainSafe` assertions verbatim into final-aggregate.md; added rv/DO-ledger bindings + container-integration row (SQLite/PostgreSQL/SQL Server/MySQL 17/17 each) to final-aggregate.md; validate_inner_loop.py report exit 0 | TestResults/D174/integration-clickhouse-joininto.log, TestResults/D174/integration-clickhouse-provider.log, TestResults/D174/final-aggregate.md; /tmp/opencode/rc2-174/evidence.json

---

# ACT — D174 / GitHub #174

- ACT recorded: 2026-10-08 (collection `1.0.9-rc2`, branch `1.0.9-rc2`).
- CHECK verdict: **PASS** at `r1` / `n2` (revision binding `5b7fb9e7+d174-do-n2`).
- Product commit: `6bc4ae41b8edf282ba83aa51fd7cc8e3480a7573` — `#174 D174: invalidate JoinInto identity-selector cache on DataContextCache.Clear()`.
- Files committed: `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs`, `src/nextorm.core/DataContext/DataContextCache.cs`, `tests/nextorm.core.tests/JoinIntoIdentitySelectorCacheTests.cs`, `docs/infrastructure/01-query-reuse-and-caching.md`, `docs/ru/infrastructure/01-query-reuse-and-caching.md`, `docs/specs/status/rc2-174-identity-selector-cache-1.md`.
- Evidence bundle: `artifacts/pdca/rc2-174/` (**gitignored** — local only, not committed, consistent with prior tasks `rc2-184`/`rc2-191`).
- GitHub issue `#174`: **closed** via `gh issue close 174` (exit 0), comment references the product commit and evidence bundle.
- Roadmap `docs/specs/roadmap/todo_navigation_properties.md` retained (only one covered bullet; the plan is not fully implemented).
- Collection status `docs/specs/status/collection-1.0.9-rc2.md`: D174 row set to `done`, Done list updated, next allowed step set to **D175**.
- ACT complete; D174 done.`
