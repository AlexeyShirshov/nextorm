# Collection 1.0.9-b-2

- collection-id: `1.0.9-b-2`
- input: open issues of GitHub milestone `1.0.9-b` (#143, #144)
- mode: autonomous + auto-commit; single group → no worktree/branch, commits go into the current branch `1.0.9-b`; push never
- cap: 1 (single group — shared query-path files)
- execution: flat primary (lane unusable in the active profile: `subagent_depth` unset → nested Task depth = 1), current worktree
- status: CLOSED

## Group

| id | tasks (order) | worktree | branch | status |
|---|---|---|---|---|
| group-1 | 143,144 | (current worktree) | `1.0.9-b` (current) | done |

## Tasks

| id | issue | group | branch | status | task status file |
|---|---|---|---|---|---|
| 1 | #143 | group-1 | 1.0.9-b | done | identity-returning-143-1.md |
| 2 | #144 | group-1 | 1.0.9-b | done | native-extreme-row-144-1.md |

## Decisions

- Clustering (planner, 2026-10-01): one group. No functional dependency between #143 and #144, but footprints overlap on `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/DataContext/SqlBuilder.cs`, `tests/nextorm.integration.tests/Providers/PostgresTestProvider.cs` → separate groups would conflict on merge.
- Order: #143 then #144 (scheduling only: establish Returning behavior, then optimize extreme-row against the verified baseline).
- Merge: none — single group, no `merge --no-ff`; commits land directly on `1.0.9-b`.
- Lane: unusable in the active profile (nested Task depth 1) → flat-primary orchestration.
- Design specs (untracked, approved design): `docs/specs/design/issue-143-identity-returning.md`, `docs/specs/design/issue-144-native-extreme-row.md`.

## Done / Verified / Incomplete

- Done: task 1 (#143 identity joined Returning) passed via escalate acceptance (CHECK#3 evidence-only, closed by the evidence ledger in `identity-returning-143-1.md`); task 2 (#144 native `SelectWhereMax`/`SelectWhereMin` fast paths for PostgreSQL/ClickHouse) closed by escalation via the `## Closing addendum (ACT)` in `native-extreme-row-144-1.md`.
- Verified: task 2 — full suite 7447 / 0 failed (run1/run2), coverage line 88.2 / branch 78.9, DocFX 0 errors; accepted debts recorded in the addendum.
- Incomplete: none — collection complete.

## Verified (phase C, 2026-10-01, HEAD ccc77488e17c0c412f98595816c81f3beb296330)

- Build Debug + Release: 0 Warning / 0 Error.
- Full solution: 7449 total / 7261 passed / 0 failed / 188 skipped (all capability/env-gated; 0 availability); two runs identical; PostgreSQL/SQL Server/MySQL/ClickHouse containers live.
- Integration-only: 2995 total / 0 failed / 187 skipped.
- Coverage (configured core+sqlite+postgres+sqlserver): line 88.2% / branch 78.9% (>=85/75).
- Perf acceptance: 7 cases / 0 failures, wall 44.43 s, Cached_ToList/Prepared_ToList 2.02 (baseline 2.10 — within noise).
- No in-progress merge/unmerged paths; no collection worktrees/branches created (single group → current worktree/branch).

## Report

- Group group-1 (single lane, no worktree/branch/merge): #143 then #144.
- Done (2): #143 (commit 0acfb7a), #144 (commit ccc7748).
- Incomplete (0).
- Both tasks closed r=1 via `escalate` acceptance (evidence-completeness objections only after three CHECKs each; no established product defect; all thresholds met); two real defects found and fixed with red→green (#143 derived parity + alias fail-closed; #144 temp-table native throw + alias collision).
- Known pre-existing flake `EfCoreQueryFilterLifecycleTests.Lifecycle_ClearOrEvict_ColdWarmPrepared_Live` recorded under #125 deferred-with-trigger; passed in the final runs.
- Branch `1.0.9-b` ahead of `origin/1.0.9-b` by 7 commits; push never performed.
- GitHub: #143 and #144 closed with commit links.
- Follow-up debts filed as separate issues in milestone `1.0.9-b`: #149 (lifecycle flake), #150 (CH float/double extreme parity), #151 (Stryker re-run), #152 (pin CHECK anchors at PLAN), #153 (sealed->class API decision), #154 (capability DTO ctors), #155 (unused payload / duplicate alias), #156 (XML cref to internal SqlBuilder), #157 (register finding 25).
- Status: CLOSED.
