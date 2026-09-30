# Collection 1.0.9-b

- collection-id: `1.0.9-b`
- input: open issues of GitHub milestone `1.0.9-b` (#39, #112, #113, #115, #116, #118, #123, #124, #125, #135, #136)
- mode: autonomous + auto-commit; **single group → no worktree/branch, commits go into the current branch** `1.0.9-b`; push never
- cap: 1 (single lane — all 11 tasks share query-path files)
- execution: **collapsed to the current worktree** (per user decision 2026-09-30): the per-group worktree and `collection/**` branches were removed; the 3 committed tasks were fast-forwarded into `1.0.9-b` and the in-flight #135 work was re-applied as a working-tree patch. `lane` subagent was unusable (nested Task depth limit = 1) → `subagent_depth: 3` added to the config (needs an opencode restart); until then — flat primary.

## Group

| id | tasks (order) | worktree | branch | status |
|---|---|---|---|---|
| group-1 | 116,136,115,113,135,39,112,123,124,125,118 | (current worktree) | `1.0.9-b` (current) | blocked |

## Tasks

| id | issue | group | branch | status | task status file |
|---|---|---|---|---|---|
| 1 | #116 | group-1 | 1.0.9-b | done | - |
| 2 | #136 | group-1 | 1.0.9-b | done | - |
| 3 | #115 | group-1 | 1.0.9-b | done | - |
| 4 | #113 | group-1 | 1.0.9-b | blocked | - |
| 5 | #135 | group-1 | 1.0.9-b | done | - |
| 6 | #39  | group-1 | 1.0.9-b | done | - |
| 7 | #112 | group-1 | 1.0.9-b | done | - |
| 8 | #123 | group-1 | 1.0.9-b | blocked | - |
| 9 | #124 | group-1 | 1.0.9-b | blocked | - |
| 10 | #125 | group-1 | 1.0.9-b | blocked | - |
| 11 | #118 | group-1 | 1.0.9-b | blocked | - |

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
- #135 done on 1.0.9-b (466e870bfa35d4323b50840cbf5e37140f0c38a0); build 0/0; CHECK PASS — M:N execution (derived `row_number()` link + two flat joins with spec JoinType, occurrence dedup `(ParentKey, ChildKey, Occurrence)`, junction-instance token in-memory); R3 metadata-lifecycle fix (configured junction mapping wins; `MemberInfoExtensions` column-name cache invalidated on `DataContextCache.Clear()`); warning `JoinInto.MultipleCollections` + `SuppressCartesianWarning()`; docs EN+RU. Evidence: core 991/991; JoinInto core 79, sqlite 33, postgres 8, mysql 4, sqlserver 4, clickhouse 12; container JoinInto 68/68 all 4 providers; full integration 2622/0 failed/187 capability skips; coverage 85.4/76.9; perf 7-case acceptance pass + new `SqliteBenchmarkManyToManyJoinInto` (14.08 ms / 2.28 MB, per-row). Residual deferrals (rejected by design, not backlog): composite junction selectors and M:N under `AsSingleQuery`.
- #39 Streaming JSON to a Stream (WriteJson/WriteJsonAsync) done on 1.0.9-b; build 0/0; core 993/993; SQLite JsonStreaming 24/24; WriteJson integration 40/40 across SQLite/PG/SQL Server/MySQL/ClickHouse/MariaDB, 0 skipped; full suite 2662/0 failed/187 capability skips; coverage 87.4/78.1; perf `SqliteBenchmarkWriteJson` (scalar allocation flat sync+async, variable-width transient, no Gen2; bounded 64 KiB buffer, no per-row retention). Accepted P2 debts: 2 zero-hit `DeferFlush` branches; no dedicated JSON `DefaultOnNull` test. Temp-table P1 fix included; commit f29c642.
- #112 Streaming CSV to a Stream (WriteCsv/WriteCsvAsync) done on 1.0.9-b; build 0/0; core 1107/1107; CSV integration 69/69 across SQLite/PG/SQL Server/MySQL/ClickHouse/MariaDB, 0 skipped; full suite 2731/0 failed/187 capability skips; coverage 85.7/77.1; perf `WriteCsv` 0.77× ToList allocation + acceptance 7/7. Options: `IncludeHeader`/`Delimiter`/`NullMarker` (`\N`)/`ExcelMode`/`ValueTransform`; base implemented by adapting `refs/heads/exp112/upstream` (`c29de9d`). Accepted P2 debts: remaining zero-hit branch partials; whole-field `byte[]` memory bound (documented, deferred with trigger). Commit 04b5bc2.
- Verified:
- Blocked:
- #113 skipped by user decision (scope: runtime WithAlias vs source-generator p.Alias unresolved). Runtime WithAlias slice saved as /tmp/opencode/task-4-113-withalias.patch; not merged.

## Stopped

Autonomous run stopped after #112. #123/#124/#125/#118 not started — each is an independent feature requiring its own PLAN→DO→CHECK→ACT. Resume from PLAN per task. #113 remains blocked (user-skipped).
