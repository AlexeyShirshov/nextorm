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
| #183 | group-1 | `collection/1.0.9-b-5/group-1` | in-progress | resumed 2026-10-04: retry B1 controlled measurement + continue B2/C (Stage A done @752943d) | `docs/specs/status/iteration-15-cached-path-183-2.md`; `docs/specs/status/iteration-15-cached-path-b1.md` |
| #166 | group-1 | `collection/1.0.9-b-5/group-1` | pending | deferred, trigger not fired (Hoist/nested read-CTE not touched; `CteHoister.Hoist` / nested read-CTE `PrepareCtes` unchanged) | — |
| #164 | group-2 | `collection/1.0.9-b-5/group-2` | pending | — | — |

## Decisions

- Clustering: #183 ∩ #166 share `QueryPlanner.cs` / `QueryCommand*` / `CteHoister.cs` / plan-cache / `Iteration14CteLookupTests.cs` → same group, #183 first; #166 deferred-with-trigger on `CteHoister.Hoist`. #164 has zero repo-code overlap → own group.
- Merge: after all lanes terminal, `git merge --no-ff` only `done` groups, sequentially.
- Evidence contract: rv=1, rows `G1-PERF`, `G1-CACHE`, `G1-CTE`, `G2-LIVE` (authoritative rows live in each task's cycle status file).
- Escalate (trigger 5) for #183: collection mandate is not code authorization; #183 = `incomplete` (awaiting written spec review); no `src/**`, `benchmarks/**`, `tests/**`, `eng/**` edits. Stage A also gated; reduced Stage A with profile unavailable would fail Gate A. Profiler availability unconfirmed (no `dotnet-trace` in `.config/dotnet-tools.json`) — open question to reviewer. #166 deferred, trigger not fired (`CteHoister.Hoist` untouched). Group-1 terminal as `incomplete`; issue #183 NOT closed.
- Recovery 2026-10-04 (current dialog): пользователь одобрил письменную спецификацию `docs/specs/performance/iteration-15-cached-path-design.md` — **written-spec review passed**. Это не approval детального плана, не выбор execution method и не разрешение реализации. #183 остаётся `incomplete`: awaiting detailed implementation plan review and execution-method selection; no implementation authorization. Group-1 всё ещё `incomplete`; #166 deferred, trigger not fired; issue #183 NOT closed.

## Общая верификация и восстановление (Common verification and recovery)

- parent verification: **not run** — no `done` group to integrate (group-1 `incomplete`, group-2 `pending`); `git merge --no-ff` not applicable
- verification state: unverified
- defect id/history: **(none)** — no product defect established; B1 `incomplete` is an acceptance/environment blocker, not a product defect
- next allowed step: run #183 B1 measurement retry, then B2/C (group-1)
- history: #183 written-spec review passed 2026-10-04; B1 stopped by escalation fallback (c) (loadavg(1m) 3.11–4.60, precondition <1.0 unmeetable); B2/C remain explicit remaining units in milestone `1.0.9-b`; #166 deferred, trigger not fired

## Done / Verified / Incomplete

- Done: (none)
- Verified: (none)
- Incomplete: group-1 — lane halted after B1 `incomplete`; not merged
- Incomplete: #183 — B1 incomplete (time-speedup not proved under controlled conditions); B2/C remaining
- Deferred: #166 — trigger not fired (`CteHoister.Hoist` / nested read-CTE `PrepareCtes` untouched)
- Pending: #164 — group-2, not started
