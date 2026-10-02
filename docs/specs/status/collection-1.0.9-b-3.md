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
| 2 | #145 | - | group-1 | 1.0.9-b | done | delete-join-145-1.md |
| 3 | #147 | - | group-1 | 1.0.9-b | done | api-create-builders-147-1.md |
| 4 | #155 | - | group-1 | 1.0.9-b | done | native-extreme-row-155-1.md |
| 5 | #146 | A | group-1 | 1.0.9-b | pending | - |
| 6 | #146 | B | group-1 | 1.0.9-b | pending | - |
| 7 | #148 | A | group-1 | 1.0.9-b | pending | - |
| 8 | #148 | B | group-1 | 1.0.9-b | pending | - |

## Decisions

- Clustering (planner, 2026-10-01): one group. #145/#146/#147/#155/#148 form one overlapping footprint component (`DataContextExtensions.cs`, `QueryCommand*`, `SqlBuilder.cs`, docs EN+RU); #149 is independent but coalesced (sequential harness). No new worktrees/branches/merge.
- Order: #149 → #145 → #147 → #155 → #146-A → #146-B → #148-A → #148-B.
- Approval gates for #147/#148: the user explicitly requested autonomous execution of all open 1.0.9-b tasks — that supersedes the historical "not authorized / approval pending" text. Proceed; ordinary PDCA gates apply.
- #145 (planner): public seam `CreateDeleteJoinBuilder<T1..Tn>() -> DeleteJoinBuilder<TProjection>.Returning()/Returning<TResult>()`; remove the 14 ambiguous joined `Returning` overloads; DELETE/UPDATE SQL + #143 behavior unchanged; compile-negative tests in `nextorm.alias.tests`.
- Lane unavailable (nested Task) → flat primary.

## Done / Verified / Incomplete

- #149 done — deterministic eviction (internal timestamp-aging hooks, 8 tests); CHECK PASS; 20/20 live lifecycle runs; coverage 88.2/78.9; commit 1568992
- #145 done — explicit CreateDeleteJoinBuilder<T1..Tn> starter; 14 ambiguous joined Returning overloads removed; compile-contract + mutation campaign; CHECK PASS; coverage 86.4/77.8; commit 3a6b44d
- #147 done — 16 DML factory renames to Create…Builder + 11 additive CreateQueryBuilder* forwarders; executing Update/UpdateAsync unchanged; CHECK PASS; coverage 88.3/78.9; integration 2995/0; commit ed3d475
- #155 done — lazy factory-backed ExtremeRowDescription.Payload + single native select-list build (reused for description/aliases/outer); generated SQL/cache keys/public surface unchanged; CHECK PASS; coverage 86.5/77.7; integration 2995/0/187; commit <hash>

## Report

- (pending)
