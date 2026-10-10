# D211 — eager-load per-parent Take/Skip (#211)

- task: T211 (issue #211, absorbs closed #210)
- selected_variant: pdca-dotnet
- cycle_id: N=1
- plan_revision: r=1 (no PLAN issued)
- evidence_revision: rv=1 (candidate only, not frozen)
- plan_state: **gap** — own PLAN could not be decided
- base: branch `1.0.9` @ `58025147`
- mode: autonomous; auto-commit for task/collection status files only; push never
- phase: P (collection own-PLAN) — stopped at the PLAN→DO boundary; no DO

## Status

- **PLAN gather (scout) completed.** Five read-only streams delivered the evidence pack:
  pipeline spec §4/§7/§11 (`docs/specs/roadmap/todo_eager_loading_pipeline.md:72-95/112-121/141-148`);
  public frontends (`EntityBuilder.cs:563-569` sole `LoadWith` overload with a child-builder
  factory + `EagerLoadMode`, 5 `JoinInto` overloads `:662/683/741/764/809`, implicit-nav
  `Visitors/NavigationExpansion.cs:43`); load-node model (`Builders/EntityBuilderEagerLoading.cs:12/60`
  — no order/predicate field today); single-query rejection list (`EntityBuilder.cs:1119`,
  throw `EntityBuilderEagerLoading.cs:176-179`); `Paging` semantics (`Builders/Paging.cs:17/26/44/49`);
  window/derived-source construction (`DataContext/SqlBuilder.cs:1113-1156`, `FromExpression.QueryCommand`
  `Expressions/FromExpression.cs:107`, precedent `Builders/Joins/JunctionLinkSourceFactory.cs:87-120`);
  provider min versions + fail-closed flags; per-provider SQL-gen harnesses; test families;
  evidence/CI/perf/integration commands; sibling DAG footprints (#209/#212/#213/#201/#53).
  The raw pack stays in the collection transcript.
- **PLAN decide (`planner`, medium tier) failed twice** with upstream `Insufficient account funds`
  (subagent task ids `ses_ed8bea5ffffeuNStTpCrG952Zk`, `ses_ed8bd4d84ffei2tAIePgbxBQep`).
  The PLAN decision role is required and the orchestrator must not author the plan.
  => **resource blocker (environment)** — not a product defect and not a design gap.
- **ALL-barrier: NOT passed for T211.** `plan_state = gap`; DO forbidden.

## Handoff (plan_handoff)

- task_id: T211
- plan_state: gap
- selected_variant: pdca-dotnet
- plan_revision: r=1 (no PLAN issued); evidence_revision: rv=1 (candidate, not frozen)
- PLAN commit(s)/refs: none — no PLAN persisted
- planned write-footprint (NOT frozen; anchors only, unverified): `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`,
  `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/DataContext/SqlBuilder.cs`,
  `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, provider dialects under
  `src/nextorm.{sqlserver,postgres,mysql,mariadb,sqlite,clickhouse}`, test projects
  `tests/nextorm.core.tests`, `tests/nextorm.<provider>.tests`, `tests/nextorm.integration.tests`,
  docs `docs/advanced/*` + `docs/ru/advanced/*`, `docs/guide/02-joins.md`, `docs/guide/29-implicit-navigation.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`
- prerequisites/assumptions: none satisfied for DO (plan not decided)
- requirements on predecessor/sibling results: T211 shares files with #209/#212/#213
  (`EntityBuilderEagerLoading.cs`, `EntityBuilder.cs`, `ISqlDialect.cs`) — the collection schedule must
  serialise those; otherwise T211 is independent and has no DO prerequisite on any sibling.
- blocker/gap: PLAN decision role (`planner`) unavailable due to upstream provider funds.
  Recovery: re-run COLLECTION TASK PLAN for T211 once the medium-tier provider is funded —
  only `planner` decide + this persist step remain (the scout pack is already gathered).

## Next allowed step

- Re-run the own PLAN of T211 (`planner` decide → `coder` persist). No DO, no branch/worktree.
