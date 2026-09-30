# Collection 1.0.9-b

- collection-id: `1.0.9-b`
- input: open issues of GitHub milestone `1.0.9-b` (#39, #112, #113, #115, #116, #118, #123, #124, #125, #135, #136)
- mode: autonomous + auto-commit; **single group → no worktree/branch, commits go into the current branch** `1.0.9-b`; push never
- cap: 1 (single lane — all 11 tasks share query-path files)
- execution: **collapsed to the current worktree** (per user decision 2026-09-30): the per-group worktree and `collection/**` branches were removed; the 3 committed tasks were fast-forwarded into `1.0.9-b` and the in-flight #135 work was re-applied as a working-tree patch. `lane` subagent was unusable (nested Task depth limit = 1) → `subagent_depth: 3` added to the config (needs an opencode restart); until then — flat primary.

## Group

| id | tasks (order) | worktree | branch | status |
|---|---|---|---|---|
| group-1 | 116,136,115,113,135,39,112,123,124,125,118 | (current worktree) | `1.0.9-b` (current) | in-progress |

## Tasks

| id | issue | group | branch | status | task status file |
|---|---|---|---|---|---|
| 1 | #116 | group-1 | 1.0.9-b | done | - |
| 2 | #136 | group-1 | 1.0.9-b | done | - |
| 3 | #115 | group-1 | 1.0.9-b | done | - |
| 4 | #113 | group-1 | 1.0.9-b | blocked | - |
| 5 | #135 | group-1 | 1.0.9-b | in-progress | - |
| 6 | #39  | group-1 | 1.0.9-b | pending | - |
| 7 | #112 | group-1 | 1.0.9-b | pending | - |
| 8 | #123 | group-1 | 1.0.9-b | pending | - |
| 9 | #124 | group-1 | 1.0.9-b | pending | - |
| 10 | #125 | group-1 | 1.0.9-b | pending | - |
| 11 | #118 | group-1 | 1.0.9-b | pending | - |

## Decisions

- Clustering: one lane. All tasks touch shared query-path files (`QueryCommand*`, `EntityBuilder`, `Dialect/*`), provider test projects and shared docs pages (`query-filters.md`, `08-cte.md`, `12-raw-sql.md`) — parallel groups would conflict on merge.
- Order: dependency-first (#116→#136, #39→#112); rest by planner chain.
- Merge: none — single group, so no `merge --no-ff`; commits land directly on `1.0.9-b`.
- Units inside each task: decided per-task PLAN.
- Lane attempt (13:03) failed with "Subagent depth limit reached (1)"; root fix: `subagent_depth: 3` in the opencode config (restart required). Until then — flat primary per pdca-collection Fallback.
- **Collapse to current worktree (2026-09-30, user decision)**: with one group the worktree/branch isolation added nothing. `#116/#136/#115` fast-forwarded into `1.0.9-b`; in-flight `#135` (slice A 1:1 + M:N metadata/API) re-applied as a working-tree patch; worktree `collection-1.0.9-b/group-1` and all `collection/1.0.9-b/*` branches deleted. Skill `pdca-collection` updated: one group ⇒ work in the current worktree/branch.
- Git worktrees are now used only for multi-group collections.

## Done / Verified / Blocked

- Done:
- #116 done on collection/1.0.9-b/task-1-1 (828c4c4); build 0/0; CHECK PASS (live ClickHouse nested-CTE 1/1; cache identity; all-provider sql-gen; coverage 85.1/76.6).
- #136 done on collection/1.0.9-b/task-1-2 (09d8f555a63c00bc82a0878b3d8b36aa271da447); build 0/0; CHECK PASS (single+join UPDATE/DELETE CTE bodies w/ explicit-projection RETURNING, PostgreSQL-only; live PG 11/11 + arities 3/3; coverage 87.1/77.9). Scope: identity whole-Projection Returning deferred to issue #143 (milestone 1.0.9-b). Accepted P2 debts: join-returning terminal duplication; JoinedMutationSource per-call placeholder re-prepare.
- #115 done on collection/1.0.9-b/task-1-3 (9dd018a4e241424d51e3fd88c9ea63f903db0dc0); build 0/0; CHECK PASS (SelectWhereMax/Min whole-row+projection, global/per-group, One/All, portable window lowering on all 6 SQL providers + in-memory; live integration 64/64 incl. ClickHouse; coverage 85.1/76.7). Native PG DISTINCT ON / CH argMax fast paths deferred to issue #144. Test-isolation fix: Query plan cache collection (serializes purge vs cache-hit assertions).
- In progress:
- #135 (JoinInto 1:1 + M:N) — slice A (1:1 `HasOneToOne` + public `JoinInto` nullable-ref overloads; LEFT→null / INNER→excluded / >1 child→throw; core+sqlite+postgres SQL-gen + live integration 44/44) and M:N metadata/API (`RelationshipJunctionMetadata`, `HasManyThrough`, local `JoinOptions.OneToOne/ManyToMany`) are done and green in the working tree (uncommitted). M:N **execution** (link projection item + `row_number` occurrence token, junction-row multiplicity, mixed joins, `JoinInto.MultipleCollections` warning) is NOT done; design from escalate captured but not implemented.
- Verified:
- Blocked:
- #113 skipped by user decision (scope: runtime WithAlias vs source-generator p.Alias unresolved). Runtime WithAlias slice saved as /tmp/opencode/task-4-113-withalias.patch; not merged.
