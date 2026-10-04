# Collection 1.0.9-b-5

- collection-id: `1.0.9-b-5`
- input: open issues of GitHub milestone `1.0.9-b` (#183, #166, #164)
- mode: autonomous + auto-commit; 2 groups → one worktree per group, branches `collection/1.0.9-b-5/group-1` and `collection/1.0.9-b-5/group-2`; `git merge --no-ff` of `done` groups into `1.0.9-b`; push never
- base: branch `1.0.9-b`

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| group-1 | #183, #166 | 183 → (166 only if trigger) | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1` | `collection/1.0.9-b-5/group-1` | pending |
| group-2 | #164 | 164 | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-2` | `collection/1.0.9-b-5/group-2` | pending |

## Tasks

| task | group | branch | status | reason + patch | task status file |
|---|---|---|---|---|---|
| #183 | group-1 | `collection/1.0.9-b-5/group-1` | pending | — | — |
| #166 | group-1 | `collection/1.0.9-b-5/group-1` | pending | deferred, trigger: #183 changes `CteHoister.Hoist` | — |
| #164 | group-2 | `collection/1.0.9-b-5/group-2` | pending | — | — |

## Decisions

- Clustering: #183 ∩ #166 share `QueryPlanner.cs` / `QueryCommand*` / `CteHoister.cs` / plan-cache / `Iteration14CteLookupTests.cs` → same group, #183 first; #166 deferred-with-trigger on `CteHoister.Hoist`. #164 has zero repo-code overlap → own group.
- Merge: after all lanes terminal, `git merge --no-ff` only `done` groups, sequentially.
- Evidence contract: rv=1, rows `G1-PERF`, `G1-CACHE`, `G1-CTE`, `G2-LIVE` (authoritative rows live in each task's cycle status file).

## Common verification and recovery

- last C: (none)
- reason: (none)
- verification state: unverified
- defect id/history: (none)
- corrective task status file: (none)
- next allowed step: run lanes

## Done / Verified / Incomplete

- Done: (none)
- Verified: (none)
- Incomplete: (none)
