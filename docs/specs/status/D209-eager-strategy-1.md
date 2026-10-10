# T209 — #209 eager-load strategy (explicit strategy + auto-dispatcher)

- task_id: T209
- issue: #209 — https://github.com/AlexeyShirshov/nextorm/issues/209
- collection: 1.0.9-todo (docs/specs/status/collection-1.0.9-todo.md)
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=1
- baseline ref: branch `1.0.9` @ `58025147`
- phase: P (collection TASK PLAN) — STOPPED at the PLAN→DO boundary (no DO)
- plan_state: gap (blocked)

## Blocker (resource, not a task defect)

PLAN Decide (role `planner`, medium tier) is unavailable: every dispatch failed with the
provider error `Insufficient account funds` — `planner` initial Decide, `planner` resumed,
and `pdca-planner` (fresh). The Gather beat completed (scout + design/test lenses). No
approved PLAN can be authored, because the orchestrator does not author plans. T209 stays a
gap; the collection ALL-barrier is not passed; no DO, no branches/worktrees, no product code.

## Preserved gather evidence (file:line)

Current code — explicit strategy choice partially exists:
- src/nextorm.core/EagerLoadMode.cs:8 — public enum EagerLoadMode { Default=0, SplitQuery=1, SingleQuery=2 } (XML-doc on type+members).
- src/nextorm.core/Builders/EntityBuilder.cs:563 — LoadWith(..., EagerLoadMode mode = EagerLoadMode.Default); EnsureValidEagerLoadMode:603 (ArgumentOutOfRangeException); ResolveEagerLoadMode:625 (conflicting explicit mode -> NotSupportedException:633).
- Builders/EntityBuilderExtensions.cs:489,:496,:518 dispatch; EntityBuilder.BuildSingleQueryJoins:1227 (NotSupportedException:1229-1243); CreatePairCommand:1345.
- Builders/EntityBuilderEagerLoading.cs:65-88 — EagerLoadSpec has NO strategy field; Execute:196/204; BuildChild:221.
- JoinInto: EntityBuilder.cs:662 (joinType :683); single-query CreateJoinIntoPairCommand:1335; Builders/Joins/JoinIntoStitcher.cs:60.
- DI/DataContextBuilder.cs:13 — no options object; UseX pattern (:34/:124, :63/:191, :73/:206); NO eager default.
- Precedence precedent: Query/QueryCommand.QueryPreparer.cs:28-33 (cmd.X ?? cmd._dataContext.X); DataContext/Roles/IContextEnvironment.cs:34-71; EntityBuilder.DataProvider:93.
- Plan key: DataContext/Cache/QueryPlan.cs:8 — no strategy/load-shape field today; Query/QueryPlanEqualityComparer.cs:199-205,550-556.
- No LinqOptions/DefaultEagerLoadingStrategy/Correlated/Auto anywhere; IEagerLoadSpec has exactly 1 implementation -> a plain enum suffices (no strategy hierarchy/interface).

Spec docs/specs/roadmap/todo_eager_loading_pipeline.md (draft under review): §3:54-70 (strategies {Split,Single,Correlated,Auto}, Auto rule :65, fail-closed :68, JoinInto fixed Single :70); §4:72-95 (ordering/top-N); §9:130-134 (strategy into plan-cache key, no sticky Cache=false); §11:141-148 (frontend matrix); §12:152-155 (acceptance); §13:159-161 (docs EN+RU + two out-of-scope removals); §15:172-175 (open naming/placement); §16:179 (#209=E1, #211=E3, #212=E4, #213=E5).

Docs: docs/advanced/eager-loading.md (:43 Mode selection, :83 Single-query) + docs/ru/advanced/eager-loading.md (:43,:83); remove "strategy" out-of-scope at docs/specs/comparison/linq2db-comparison.md:233,:277 and docs/specs/roadmap/linq2db-backlog-gap-analysis.md:54,:88; registry docs/specs/design/API-NAMING-REVIEW.md:55,:5682-5691.

Tests: tests/nextorm.core.tests/EagerLoadingModeTests.cs (:260 sticky-cache, :341 conflict, :393 api-surface), EagerLoadingTests.cs, JoinIntoPlanKeyTests.cs:38,:54; tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs (SqlRecordingInterceptor:18; :45,:82,:224,:285); provider JoinInto SQL-gen (postgres/sqlserver/mysql/clickhouse); integration tests/nextorm.integration.tests/CommonTestSuite.EagerLoading.cs.

Perf acceptance (nextorm overlay, mandatory for query-path/plan-cache changes): dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance (exactly 7 cases; <=4 min gate; >20% cached-vs-prepared ratio growth -> investigate, not hard fail). Evidence contract: rv=1, manifest artifacts/pdca/D209/rv1/manifest.json, validated by scripts/validate_inner_loop.py manifest.

## Next allowed step

Re-dispatch `planner` (medium tier) Decide for T209 once provider funds are restored, reusing the preserved gather above; it authors r=1 (naming decision, fail-closed scope for Correlated/Auto given top-N is #211, context-level default, plan-cache scope, test strategy + variant matrix, docs plan, perf/recon/unit decisions, pinned versioned evidence contract rv=1). Then coder rewrites this file with the approved plan; then the collection ALL-barrier.
