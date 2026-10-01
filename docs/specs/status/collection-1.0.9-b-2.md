# Collection 1.0.9-b-2

- collection-id: `1.0.9-b-2`
- input: open issues of GitHub milestone `1.0.9-b` (#143, #144)
- mode: autonomous + auto-commit; single group → no worktree/branch, commits go into the current branch `1.0.9-b`; push never
- cap: 1 (single group — shared query-path files)
- execution: flat primary (lane unusable in the active profile: `subagent_depth` unset → nested Task depth = 1), current worktree

## Group

| id | tasks (order) | worktree | branch | status |
|---|---|---|---|---|
| group-1 | 143,144 | (current worktree) | `1.0.9-b` (current) | pending |

## Tasks

| id | issue | group | branch | status | task status file |
|---|---|---|---|---|---|
| 1 | #143 | group-1 | 1.0.9-b | pending | - |
| 2 | #144 | group-1 | 1.0.9-b | pending | - |

## Decisions

- Clustering (planner, 2026-10-01): one group. No functional dependency between #143 and #144, but footprints overlap on `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/DataContext/SqlBuilder.cs`, `tests/nextorm.integration.tests/Providers/PostgresTestProvider.cs` → separate groups would conflict on merge.
- Order: #143 then #144 (scheduling only: establish Returning behavior, then optimize extreme-row against the verified baseline).
- Merge: none — single group, no `merge --no-ff`; commits land directly on `1.0.9-b`.
- Lane: unusable in the active profile (nested Task depth 1) → flat-primary orchestration.
- Design specs (untracked, approved design): `docs/specs/design/issue-143-identity-returning.md`, `docs/specs/design/issue-144-native-extreme-row.md`.

## Done / Verified / Incomplete

- Done:
- Verified:
- Incomplete:
