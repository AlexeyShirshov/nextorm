# Collection 1.0.9-b-5

- collection-id: `1.0.9-b-5`
- input: open issues of GitHub milestone `1.0.9-b` (#183, #166, #164)
- mode: autonomous + auto-commit; 2 groups → one worktree per group, branches `collection/1.0.9-b-5/group-1` and `collection/1.0.9-b-5/group-2`; `git merge --no-ff` of `done` groups into `1.0.9-b`; push never
- base: branch `1.0.9-b`

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| group-1 | #183, #166 | 183 → (166 only if trigger) | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1` | `collection/1.0.9-b-5/group-1` | done |
| group-2 | #164 | 164 | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-2` | `collection/1.0.9-b-5/group-2` | done |

## Tasks

| task | group | branch | status | reason + patch | task status file |
|---|---|---|---|---|---|
| #183 | group-1 | `collection/1.0.9-b-5/group-1` | done | Stage A done; B1/B2 incomplete-with-fallback (patches preserved); C done; #166 not-executed | `iteration-15-cached-path-183-3.md`; `iteration-15-cached-path-results.md` |
| #166 | group-1 | `collection/1.0.9-b-5/group-1` | not-executed | deferred trigger | — |
| #164 | group-2 | `collection/1.0.9-b-5/group-2` | done | — | `pdca-evidence-164-1.md` |

## Decisions

- Clustering: #183 ∩ #166 share `QueryPlanner.cs` / `QueryCommand*` / `CteHoister.cs` / plan-cache / `Iteration14CteLookupTests.cs` → same group, #183 first; #166 deferred-with-trigger on `CteHoister.Hoist`. #164 has zero repo-code overlap → own group.
- Merge: after all lanes terminal, `git merge --no-ff` only `done` groups, sequentially.
- Evidence contract: rv=1, rows `G1-PERF`, `G1-CACHE`, `G1-CTE`, `G2-LIVE` (authoritative rows live in each task's cycle status file).
- Escalate (trigger 5) for #183: collection mandate is not code authorization; #183 = `incomplete` (awaiting written spec review); no `src/**`, `benchmarks/**`, `tests/**`, `eng/**` edits. Stage A also gated; reduced Stage A with profile unavailable would fail Gate A. Profiler availability unconfirmed (no `dotnet-trace` in `.config/dotnet-tools.json`) — open question to reviewer. #166 deferred, trigger not fired (`CteHoister.Hoist` untouched). Group-1 terminal as `incomplete`; issue #183 NOT closed.
- Recovery 2026-10-04 (current dialog): пользователь одобрил письменную спецификацию `docs/specs/performance/iteration-15-cached-path-design.md` — **written-spec review passed**. Это не approval детального плана, не выбор execution method и не разрешение реализации. #183 остаётся `incomplete`: awaiting detailed implementation plan review and execution-method selection; no implementation authorization. Group-1 всё ещё `incomplete`; #166 deferred, trigger not fired; issue #183 NOT closed.

## Общая верификация и восстановление (Common verification and recovery)

- last C: FAIL — parent CHECK not certifiable (evidence-completeness gate)
- reason: (i) mandatory-evidence gate not closable within CHECK budget; (ii) EXTERNAL commit 158233f #186 (5 src/** + tests/docs) interleaved into 1.0.9-b during the collection by a concurrent session — out of this collection's scope/ownership; the integrated tree is therefore not a clean collection snapshot
- verification state: unverified (STOP)
- defect id/history: C-scope-1 (fixed); C-evidence-1 (rv=5 N/A recorded); C-external-186 (open, external)
- next allowed step: parent CHECK (rv=5 provider-evidence trace attached)
- history: #183 written-spec review passed 2026-10-04; B1 stopped by escalation fallback (c) (loadavg(1m) 3.11–4.60, precondition <1.0 unmeetable); B2/C remain explicit remaining units in milestone `1.0.9-b`; #166 deferred, trigger not fired
- group-1 done: #183 DONE (with limitation) — B1/B2 rejected/reverted (attributable time speedup not provable on shared host); patches preserved; #166 not triggered.

## External interference

- `158233f #186 "Unify result-set traversal…"` (5 src files: `BatchBuilder.cs`, `BatchRunner.cs`, `IResultSetCursorSource.cs`, `ProcedureResult.cs`, `ResultSet.cs`; + tests/docs), committed 2026-10-04 20:11 +0500, parent `5137ec8`, child `7eb5899`.
- It is an ancestor of the frozen snapshot `52c4b3a`.
- `01b89fd..HEAD` itself is `src/**`-clean (the collection's own merges changed no product source).

## Done / Verified / Incomplete

- Done: group-1 #183 (Stage A; B1/B2 fallback; docs/benchmarks only), group-2 #164 (CHECK PASS rv=1) — merges `81e61c0`/`2914966`
- Done: #183 (with limitation), #164
- Verified: (none)
- Incomplete/Unverified: parent collection C (STOP)
- Incomplete: group-1 B1/B2 sub-stages (patches preserved)
- Deferred: #166 — trigger not fired (`CteHoister.Hoist` / nested read-CTE `PrepareCtes` untouched)
- Preserved: group worktrees + branches (no cleanup)
