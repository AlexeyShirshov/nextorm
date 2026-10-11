# D209-corrective — T209 (#209) r=1 n=2 CHECK-FAIL corrective record

- task_id: T209
- issue: #209 — https://github.com/AlexeyShirshov/nextorm/issues/209
- collection: `1.0.9-todo`
- cycle_id: N=1; plan revision: r=1 (unchanged); evidence contract: rv=1
- attempt: n=2/3 (corrective round after CHECK r=1 n=1 FAIL; no replan, revision not incremented)
- phase: DO → CHECK (D209.8 re-submitted); commits `8bdcee2a`, `0b642c50`
- companion status file of the main task: `docs/specs/status/D209-eager-strategy-1.md`

## CHECK FAIL verdict (r=1, n=1)

CHECK r=1 n=1 returned **FAIL** with confirmed P1 defects and evidence gaps. The plan revision
`r=1` was **not** changed (no substantive replan); the corrective round is attempt `n=2/3` on the
same revision. Defects F1/F2/F3/F4 were confirmed content defects; F13/F14 were missing
identity/dispatch regressions.

## Confirmed defects and fixes

| Key | Defect (CHECK finding) | Fixed at (file:line) | Fix |
|---|---|---|---|
| F1/F2 | `JoinInto + LoadWith` mixed-shape dispatch was rerouted: a `JoinInto` combined with an inherited (Default) `LoadWith` took the merged single-query path under Split/Single/Auto and threw under Correlated, instead of keeping the baseline JoinInto pair command + split child loads. | `src/nextorm.core/Builders/EntityBuilder.cs:147-160` — `EntityBuilder.SingleQuery` | Explicit non-`Default` `_eagerLoadMode` is authoritative (`SingleQuery` only when it equals `SingleQuery`); with no explicit choice, a `JoinInto` (`_joinIntos.Count > 0`) or a plain non-`LoadWith` builder returns `false`, so `JoinInto + LoadWith(Default)` keeps the baseline pair + split dispatch under every context default. `CreateJoinIntoPairCommand` stays pinned `SingleQuery`. |
| F3 | Invalid enum value blamed the wrong source: `EagerLoadResolver.Resolve` always threw `ArgumentOutOfRangeException` with `ParamName == "contextDefault"`, even when the offending value was the query override. | `src/nextorm.core/Query/QueryCommand.EagerLoading.cs:92-93` — `EagerLoadResolver.Resolve` | `ParamName` is attributed to `requested` when a query override was supplied, otherwise to `contextDefault` (`requested is not null ? nameof(requested) : nameof(contextDefault)`). |
| F4 | `EagerLoadSpec<TEntity,TChild,TKey>` memoized the child builder in an unsynchronized mutable `_resolvedChild` field — a data race under concurrent execution of a shareable builder. | `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:109` (field), `:188-193` (`ResolveChild`) | Replaced with a per-context `ConditionalWeakTable<IDataContext, Lazy<EntityBuilder<TChild>>>` using `LazyThreadSafetyMode.ExecutionAndPublication`: one thread-safe child build per context, no builder leaked between contexts, no unsynchronized cache. |
| F13 | Missing direct `EagerLoadShape` structural-identity tests and the V12 plan-cache contract test. | `tests/nextorm.core.tests/EagerLoadingModeTests.cs:754-807` (shape) and `:810-841` (V12) | Added `EagerLoadShape_EmptyAndDefault_AreEquivalentAndHashToZero` (`:754`), `…IsOrderSensitive` (`:770`), `…EqualParts_AreEqualWithEqualHashes` (`:782`), `…HashIsDeterministic_AndACollisionNeverImpliesEquality` (`:793`), `EagerPlan_ChildFilterParameterValueReusesThePlan_ButAStructureChangeIsolatesIt` (`:810`). |
| F14 | Missing mixed `JoinInto + LoadWith` dispatch regression coverage (core + SQLite, under 4 context defaults). | core `tests/nextorm.core.tests/EagerLoadingModeTests.cs:710-747`; SQLite `tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs:729-813` | Added `JoinInto_WithInheritedLoadWith_KeepsTheBaselinePairAndSplitDispatch` (`:710`) and `JoinInto_WithExplicitSingleQueryLoadWith_StillMerges` (`:730`); SQLite `JoinInto_WithLoadWithDefault_KeepsTheBaselinePairAndSplitDispatch(contextMode: SplitQuery\|SingleQuery\|Auto\|Correlated)` (`:729`) and `JoinInto_WithExplicitSingleQueryLoadWith_IssuesOneMergedCommand` (`:767`). |

## Commits

- `8bdcee2a` — `#209 T209 r1 n2/3: F1/F2 JoinInto+LoadWith baseline dispatch, F3 AOORE ParamName, F4 thread-safe child memo, F13/F14 regressions` (F1/F2/F3/F4 production + F13/F14 tests).
- `0b642c50` — `#209 T209 DO r1 n2/3: re-bind DO->CHECK boundary evidence to HEAD 8bdcee2a (build/suites/integration/coverage/perf/audit); report+manifest validators exit 0` (n=2 boundary evidence re-run against the n=2 HEAD).

## red → green evidence

| Defect | RED (before fix) | GREEN (after fix) |
|---|---|---|
| F1/F2 (core) | `artifacts/pdca/D209/rv1/core-f14-red.log` — exit code 2; `JoinInto_WithInheritedLoadWith_KeepsTheBaselinePairAndSplitDispatch` fails: `Expected builder.SingleQuery to be False … but found True.` (`EagerLoadingModeTests.cs:718`); total 5 / failed 1 / succeeded 4 | `artifacts/pdca/D209/rv1/core-eager-inner-n2.log` — exit 0; total 137 / failed 0. `artifacts/pdca/D209/rv1/core-joininto-inner-n2.log` — exit 0; total 118 / failed 0 |
| F1/F2 (sqlite) | `artifacts/pdca/D209/rv1/sqlite-f14-red.log` — exit code 2; `JoinInto_WithLoadWithDefault_KeepsTheBaselinePairAndSplitDispatch(contextMode: SplitQuery\|SingleQuery\|Auto\|Correlated)` fails: `Expected interceptor.Statements to contain 2 item(s) … but found 1`; total 5 / failed 4 / succeeded 1 | `artifacts/pdca/D209/rv1/sqlite-eager-inner-n2.log` — exit 0; total 31 / failed 0 |
| F3 | `artifacts/pdca/D209/rv1/core-f3-red.log` — exit code 2; `Resolver_UndefinedValue_IsRejectedWithTheOffendingSourceName` fails: expected `"requested"`, got `"contextDefault"` (`EagerLoadingModeTests.cs:617`); total 1 / failed 1 | `artifacts/pdca/D209/rv1/core-eager-inner-n2.log` — exit 0 (137/0) |
| F4 | correctness/thread-safety hardening (no dedicated red test); fix site `EntityBuilderEagerLoading.cs:188` | `artifacts/pdca/D209/rv1/core-eager-inner-n2.log` — 137/0; boundary suites below |
| F13/F14 | missing regressions demonstrated by the red core/sqlite logs above | core 137/0 + 118/0, sqlite 31/0 |

## n=2 DO→CHECK boundary re-bind (HEAD `8bdcee2a`)

All boundary evidence re-run fresh against n=2 HEAD:

- build: `artifacts/pdca/D209/rv1/n2/build.log` — exit 0, 0 Warning(s) 0 Error(s).
- generation suites: `n2/core-tests.log` (2094/0/0), `n2/sqlite-tests.log` (1342/0/1), `n2/postgres-tests.log` (855/0/0), `n2/sqlserver-tests.log` (772/0/0), `n2/mysql-tests.log` (332/0/0), `n2/clickhouse-tests.log` (620/0/0).
- integration (DOCKER_HOST, zero "is not available"): `n2/integration-{postgres,sqlserver,mysql,clickhouse,sqlite}.log`.
- coverage: `n2/coverage.cobertura.xml`, `n2/coverage-report/Summary.txt` — line 88.4% / branch 80.5%.
- performance: `n2/perf/{baseline,candidate}/console.log`, `n2/perf/ratios.md` — 7/7 cases, no ratio >20%.
- audit: `n2/suppression-scan.md`, `n2/branch-delta.md`.
- validator: `n2/evidence-validation.log` exit 0; `n2/manifest-validation.log` exit 0.

## Evidence artifacts produced in this corrective record

- `artifacts/pdca/D209/rv1/n2/cache-evidence.md` — R209-07/R209-08/V12/V13 → test symbols + reviewed diff (E209-08).
- `artifacts/pdca/D209/rv1/n2/scenario-map.md` — V01–V18 → runner-discovered symbols (E209-03/…).
- `artifacts/pdca/D209/rv1/n2/docs-review.md` — EN/RU agreement, no public specs links, DocFX re-run (E209-11).
- `artifacts/pdca/D209/rv1/n2/perf/investigation.md` — cached/prepared ratios + disposition (E209-10).
- `artifacts/pdca/D209/rv1/n2/docs.log` — DocFX re-run, exit 0.
- `artifacts/pdca/D209/rv1/n2/manifest-validation.log` — validator exit 0.
- `artifacts/pdca/D209/rv1/sibling-contract.md` — acknowledgement table filled (#211/#212/#213, rv=1).
- `docs/specs/status/D211-…md`, `D212-…md`, `D213-…md` — "Acknowledgement — T209 rv1 shared contract" sections.
- `docs/specs/status/D209-corrective-1.md` — this record.

## Status

- CHECK-owned rows `E209-08`, `E209-10`, `E209-12`, `E209-13` remain `open` in `manifest.json`
  (evidence now exists; CHECK renders the verdicts and closes them).
- `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D209/rv1/manifest.json` → exit 0.
- D209.8 re-submitted to CHECK; D209 is **not** marked done by this record.

## Escalation corrective round (r=1, n=2/3) — 2nd fix of the JoinInto+LoadWith class

A **strong-tier `escalate` decision** (rendered after the CHECK r=1 n=2 return) replaced the previous fix
approach and was implemented **as-is**. The plan revision `r=1` is unchanged and the attempt stays
`n=2/3`: this is the **second fix of the same defect class** and a further return triggers STOP, not a
third fix. The dispatcher (`EntityBuilder.SingleQuery`, `EntityBuilder.cs:151-155`) was deliberately
**not** touched — editing it is what reintroduced the defect in the previous round.

| Item | Detail |
|---|---|
| Defect key | `joininto-loadwith-unsupported-child-unguarded` (2nd fix of the JoinInto+LoadWith class) |
| Root cause | The `JoinIntos` branch of `EntityBuilderExtensions.MaterializeList*` (`:531-536`/`:557-562`) never calls `MarkPerParentTopN`, so the pair command runs and is plan-cached before `BuildChild` rejects — and an empty parent is fail-open; an explicit `LoadWith(..., Correlated)` never reaches `EagerLoadResolver.Resolve`. |
| Fix | One side-effect-free `EntityBuilderEagerLoading.ValidateLoadWithForJoinInto<TEntity>`, called as the **first statement** of `JoinIntoStitcher.Execute` (sync), `JoinIntoStitcher.ExecuteAsync` (before the first SQL `await`) and the shared `JoinIntoStitcher.Stitch`. |
| Rejects | Explicit `Correlated` (same explicit-vs-default criterion as the `SingleQuery` getter; identical text via shared `EagerLoadResolver.CorrelatedNotSupported`); a child `Limit`/`Offset`/`Page` (shared `HasPerParentTopN` + `PerParentTopNNotSupported`, reused by `MarkPerParentTopN`/`RequiresPerParentTopN` and the `BuildChild` defence-in-depth guard). |
| Preserves | Un-paged child; `JoinInto + Default` under **every** context default incl. `Correlated` → pair + split (exactly 2 statements); explicit `SingleQuery` merge unchanged; no new fields/interfaces/public API; no sticky/global state. |
| Evidence | `artifacts/pdca/D209/rv1/n2/{core-eager-inner-final,core-joininto-inner-final,sqlite-eager-inner-final,core-tests-final,sqlite-tests-final}.log`; build `n2/build-final-guard.log`; red→green `n2/{core-eager-red-guard,sqlite-eager-red-guard}.log` → `n2/*-green-guard.log`. |
| Commit | `dd5d08fb` — source + tests; a follow-up `#209` record commit carries this corrective/status update. |

**Red → green.** With the guard disabled: `dotnet test tests/nextorm.sqlite.tests --filter FullyQualifiedName~EagerLoad` → 46 total / **14 failed** (12 paged + 2 explicit-Correlated; the two regression tests still pass) and `dotnet test tests/nextorm.core.tests --filter FullyQualifiedName~EagerLoad` → 141 total / **1 failed**. With the guard restored: sqlite ~EagerLoad 46/0, core ~EagerLoad 141/0, core ~JoinInto 122/0; full core 2098/0/0 and full SQLite 1357 total / 1356 succeeded / 1 approved skip.
