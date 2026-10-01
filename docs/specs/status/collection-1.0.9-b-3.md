# Collection 1.0.9-b-3

- collection-id: `1.0.9-b-3`
- input: open issues of GitHub milestone `1.0.9-b` (#145, #146, #147, #148, #149, #155)
- mode: autonomous + auto-commit; single group → no worktree/branch, commits go into the current branch `1.0.9-b`; push never
- cap: 1 (single group — overlapping query-path footprint; flat primary, no lanes)
- base: `cbb9896`
- execution: flat primary (nested Task unavailable), current worktree
- status: IN-PROGRESS

## Group

| id | tasks (order) | worktree | branch | status |
|---|---|---|---|---|
| group-1 | 149,145,147,155,146-A,146-B,148-A,148-B | (current worktree) | `1.0.9-b` (current) | in-progress |

## Tasks

| id | issue | slice | group | branch | status | task status file |
|---|---|---|---|---|---|---|
| 1 | #149 | - | group-1 | 1.0.9-b | done | cache-eviction-149-1.md |
| 2 | #145 | - | group-1 | 1.0.9-b | pending | - |
| 3 | #147 | - | group-1 | 1.0.9-b | pending | - |
| 4 | #155 | - | group-1 | 1.0.9-b | pending | - |
| 5 | #146 | A | group-1 | 1.0.9-b | pending | - |
| 6 | #146 | B | group-1 | 1.0.9-b | pending | - |
| 7 | #148 | A | group-1 | 1.0.9-b | pending | - |
| 8 | #148 | B | group-1 | 1.0.9-b | pending | - |

## Decisions

- Clustering (planner, 2026-10-01): one group. #145/#146/#147/#155/#148 form one overlapping footprint component (`DataContextExtensions.cs`, `QueryCommand*`, `SqlBuilder.cs`, docs EN+RU); #149 is independent but coalesced (sequential harness). No new worktrees/branches/merge.
- Order: #149 → #145 → #147 → #155 → #146-A → #146-B → #148-A → #148-B.
- Approval gates for #147/#148: the user explicitly requested autonomous execution of all open 1.0.9-b tasks — that supersedes the historical "not authorized / approval pending" text. Proceed; ordinary PDCA gates apply.
- Lane unavailable (nested Task) → flat primary.

## Done / Verified / Incomplete

- #149 done — deterministic eviction (internal timestamp-aging hooks, 8 tests); CHECK PASS; 20/20 live lifecycle runs; coverage 88.2/78.9; commit 1568992

## Report

- (pending)
