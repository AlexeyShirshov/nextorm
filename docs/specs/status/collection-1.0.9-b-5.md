# Collection 1.0.9-b-5

- collection-id: `1.0.9-b-5`
- input: open issues of GitHub milestone `1.0.9-b` (#183, #166, #164)
- mode: autonomous + auto-commit; 2 groups → one worktree per group, branches `collection/1.0.9-b-5/group-1` and `collection/1.0.9-b-5/group-2`; `git merge --no-ff` of `done` groups into `1.0.9-b`; push never
- base: branch `1.0.9-b`

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| group-1 | #183, #166 | 183 → (166 only if trigger) | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1` | `collection/1.0.9-b-5/group-1` | in-progress |
| group-2 | #164 | 164 | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-2` | `collection/1.0.9-b-5/group-2` | pending |

## Tasks

| task | group | branch | status | reason + patch | task status file |
|---|---|---|---|---|---|
| #183 | group-1 | `collection/1.0.9-b-5/group-1` | in-progress | written spec approved 2026-10-04 by owner; implementation authorized | `docs/specs/status/iteration-15-cached-path-183-1.md` |
| #166 | group-1 | `collection/1.0.9-b-5/group-1` | pending | deferred, trigger not fired (#183 made no CteHoister.Hoist change) | — |
| #164 | group-2 | `collection/1.0.9-b-5/group-2` | pending | — | — |

## Decisions

- Clustering: #183 ∩ #166 share `QueryPlanner.cs` / `QueryCommand*` / `CteHoister.cs` / plan-cache / `Iteration14CteLookupTests.cs` → same group, #183 first; #166 deferred-with-trigger on `CteHoister.Hoist`. #164 has zero repo-code overlap → own group.
- Merge: after all lanes terminal, `git merge --no-ff` only `done` groups, sequentially.
- Evidence contract: rv=1, rows `G1-PERF`, `G1-CACHE`, `G1-CTE`, `G2-LIVE` (authoritative rows live in each task's cycle status file).
- Escalate (trigger 5) for #183: collection mandate is not code authorization; #183 = `incomplete` (awaiting written spec review); no `src/**`, `benchmarks/**`, `tests/**`, `eng/**` edits. Stage A also gated; reduced Stage A with profile unavailable would fail Gate A. Profiler availability unconfirmed (no `dotnet-trace` in `.config/dotnet-tools.json`) — open question to reviewer. #166 deferred, trigger not fired (`CteHoister.Hoist` untouched). Group-1 terminal as `incomplete`; issue #183 NOT closed.
- Recovery 2026-10-04 (current dialog): пользователь одобрил письменную спецификацию `docs/specs/performance/iteration-15-cached-path-design.md` — **written-spec review passed**. Это не approval детального плана, не выбор execution method и не разрешение реализации. #183 остаётся `incomplete`: awaiting detailed implementation plan review and execution-method selection; no implementation authorization. Group-1 всё ещё `incomplete`; #166 deferred, trigger not fired; issue #183 NOT closed.

## Common verification and recovery

- last C: escalate — #183 authorization question
- recovery (2026-10-04): written-spec review passed (spec approved by user); implementation remains gated — detailed plan not reviewed, execution method not selected, no implementation authorization
- reason: written spec approved 2026-10-04; awaiting detailed implementation plan review and execution-method selection; no implementation authorization
- verification state: unverified
- defect id/history: (none)
- corrective task status file: (none)
- next allowed step: run #183 implementation stages A/B/C (group-1)

## Done / Verified / Incomplete

- Done: (none)
- Verified: (none)
- Incomplete: #183 — written spec approved 2026-10-04; awaiting detailed implementation plan review + execution-method selection; no implementation authorization
- Deferred: #166 — trigger not fired
