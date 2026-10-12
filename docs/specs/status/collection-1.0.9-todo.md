# Collection 1.0.9-todo

- collection-id: `1.0.9-todo`
- input: milestone `1.0.9` (milestone #4) — **expanded**: originally {#213, #212, #211, #209, #201, #53}; owner added {#215, #216, #217, #218}. Active set: T211, T212, T213, T201, T215, T216, T217, T218 (+ T209 done). T53 held by owner directive.
- base: branch `1.0.9` @ `58025147` (merge of #214 `1.0.9-rc2`); HEAD advanced by P-phase PLAN commits (`d242778f`).
- mode: autonomous; auto-commit authorized (task/collection status files and task paths only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- phase: **P re-open complete → ALL-barrier re-passed → re-cluster pending** (single group → current worktree/branch `1.0.9`, no worktree/branches/merge)

## Input task list

Selected variant: `pdca-dotnet` for all tasks (C#/.NET repo). Each task runs its own real PLAN at the P phase, stops at the PLAN→DO boundary, and the lane resumes the **same** cycle from DO.

| id | issue | title | selected_variant | plan_state |
|---|---|---|---|---|
| T209 | #209 | eager-load strategy — explicit strategy + auto-dispatcher | pdca-dotnet | done (r=3) |
| T211 | #211 | eager-load per-parent Take/Skip (absorbs #210) | pdca-dotnet | ready (r=2, rv=2) |
| T212 | #212 | eager-load nested — level 2+ LoadWith | pdca-dotnet | ready (r=1) |
| T213 | #213 | JoinInto — composite keys / M:N single-query | pdca-dotnet | ready (r=1) |
| T201 | #201 | Perf: M12 #3 — cached-path parity with LINQ to DB | pdca-dotnet | ready (r=2) |
| T215 | #215 | убрать зависимость OneOf | pdca-dotnet | ready (r=1) |
| T216 | #216 | row locking — FOR UPDATE vs FOR UPDATE OF | pdca-dotnet | ready (r=2, rv=2) — **owner constraint open** |
| T217 | #217 | DateTimeOffset — SQL-трансляция Now/UtcNow и частей даты | pdca-dotnet | ready (r=1) |
| T218 | #218 | SQL Server sequences (NEXT VALUE FOR), кросс-провайдерно | pdca-dotnet | ready (r=1) |
| T53 | #53 | IF6: public API freeze (PublicApiAnalyzers + PublicAPI.*) | pdca-dotnet | ready (r=1) — **HELD by owner** |

## Groups

| id | tasks (order) | worktree | branch/ref | status |
|---|---|---|---|---|
| G1 | T209 (done) → T211 → T212 → T213 → T201 → {T215, T216, T217, T218} → T53 (held) | no (single group) | current branch `1.0.9` | pending re-cluster |

Order within `{T215, T216, T217, T218}` and T53's placement are pending `planner` re-cluster on the expanded set. Shared-file serialization is expected to keep this a single lane.

## Tasks

| id | issue | group | order | status | plan_state | selected_variant | cycle_id | plan_revision | PLAN commit | status file |
|---|---|---|---|---|---|---|---|---|---|---|
| T209 | #209 | G1 | 1 | done | done | pdca-dotnet | N=1 | r=3 | 5e9f6756 | docs/specs/status/D209-eager-strategy-1.md |
| T211 | #211 | G1 | 2 | pending | ready | pdca-dotnet | N=1 | r=2 | d242778f | docs/specs/status/D211-eager-perparent-topn-1.md |
| T212 | #212 | G1 | 3 | pending | ready | pdca-dotnet | N=1 | r=1 | 034e5b45 | docs/specs/status/D212-eager-nested-1.md |
| T213 | #213 | G1 | 4 | pending | ready | pdca-dotnet | N=1 | r=1 | 9e7facff | docs/specs/status/D213-joininto-remainder-1.md |
| T201 | #201 | G1 | 5 | pending | ready | pdca-dotnet | N=1 | r=2 | bd4af157 | docs/specs/status/D201-cached-parity-1.md |
| T215 | #215 | G1 | TBD | pending | ready | pdca-dotnet | N=1 | r=1 | 23c83497 | docs/specs/status/D215-oneof-removal-1.md |
| T216 | #216 | G1 | TBD | pending | ready | pdca-dotnet | N=1 | r=2 | 1b7b9d8c | docs/specs/status/D216-row-locking-1.md |
| T217 | #217 | G1 | TBD | pending | ready | pdca-dotnet | N=1 | r=1 | ee0cb743 | docs/specs/status/D217-datetimeoffset-1.md |
| T218 | #218 | G1 | TBD | pending | ready | pdca-dotnet | N=1 | r=1 | 60d881b4 | docs/specs/status/D218-sqlserver-sequences-1.md |
| T53  | #53  | G1 | last | held | ready | pdca-dotnet | N=1 | r=1 | (untracked, D53 file) | docs/specs/status/D53-public-api-freeze-1.md |

- Evidence contract (per task, frozen at that task's DO→CHECK): T209 rv=2 (done), T211 rv=2, T216 rv=2, others rv=1. Manifests `artifacts/pdca/<task>/rv*/manifest.json`.
- T211 history: r1 exhausted (3/3 CHECK fails; EV-11 exit-code capture gap, no product defect); owner-directed re-open → r=2 with a real exit-code capture wrapper; retained commits `e98ae60e`, `5bd3028f`, `15c6253f` (not reverted).

## Phase decisions

- **Own PLANs:** original 6 re-finalized `ready` (T201 via approved design rev 2 → r=2). Expansion: T215/T216/T217/T218 planned `ready`; T211 re-opened to `ready (r=2)`.
- **ALL-barrier: re-PASSED** — every active task now has a persisted `plan_state=ready` (except T53, deliberately `held`, and T216 whose plan is `ready` but carries an owner-only open constraint). Expanded lane allocation is not allowed until the re-cluster binding is persisted.
- **Public-API candidates flagged for the held T53 freeze:** T216 adds `FromOptions`/`JoinOptions` lock members + `ILockRenderer.SupportsSourceLocks` + a new `Render` overload; T217 adds `ISqlDialect.SupportsDateTimeOffsetTranslation` + `MakeDateTimeOffsetNow` + DTO-aware `MakeDatePart`/`MakeDateAdd` overloads; T218 adds `SqlFunctions.SqlServer.next_value_for`/`nextval`; T215 changes the package graph (no surface delta). T53 stays held until the owner declares the API final.
- **Re-cluster:** pending `planner` on the expanded set (10 tasks, 9 active).

## Open owner constraint — T216 (#216)

- R-03/MariaDB `FOR UPDATE OF <alias>`: MariaDB grammar has **no `OF`**. `escalate` routed a revised r=2 plan with **MariaDB fail-closed as provisional default** (not a waiver). The owner must choose:
  - (i) approve narrowing MariaDB source-lock to **unsupported** (update #216 text/criteria), or
  - (ii) **defer** MariaDB source lock (whole-query lock only).
- Until decided: CHECK must treat this as a **constraint, not a criterion PASS**, and **#216 must not be closed**. Not blocking T211/T212/T213/T201 (T216 is later in the lane).

## Working-tree note

- The branch has **39 uncommitted entries not produced by this collection**: 15 `benchmarks/BenchmarkDotNet.Artifacts/results/*` (tracked, modified), ~20 EN/RU docs files (modified), and 4 untracked `docs/{,ru/}scalar-functions/10-system-functions.md` + `12-sequences.md`. The collection does **not** touch or commit them; tasks preserve them. Ownership/commit ordering must be settled before T215/T218 DO (their doc footprints overlap the untracked files).

## Общая верификация и восстановление

- Verification state (C): not reached — collection mid-flight (re-scope/re-cluster).
- Last collection `C` result: —
- Blocker: — (T211 `incomplete` superseded by the owner-directed r=2 re-open; patch retained)
- Defect id / history: —
- Corrective-task status file: —
- Next allowed step: `planner` re-cluster on the expanded set → persist schedule → resume the lane from T211 (DO, r=2) → T212 → T213 → T201 → T215/T216/T217/T218 (serialized) → T53 only when the owner unfreezes.

## Done / Verified / Incomplete

- Done: T209 (#209) — eager-load strategy (explicit strategy + auto-dispatcher); cycle N=1 r=3 n=2/3, CHECK PASS, contract rv=2.
- Verified: T209 (#209) — build 0/0; six dialect suites 0 failures; integration all 5 providers executed; coverage line 88.5% / branch 80.5%; acceptance benchmark 7/7, no ratio >20%; manifest `artifacts/pdca/D209/rv2/manifest.json` E209-01..21 all `met`, validator exit 0.
- Incomplete (superseded): T211's earlier `incomplete`; T212/T213/T201/T53 «группа остановлена» — reinstated as `pending` (T53 `held`) by the expansion directive.

## History (superseded current-state facts)

- Original input 6 tasks, G1 order `T209 → T211 → T212 → T213 → T53`; superseded by the amended order + expansion.
- T201 was `gap → excluded-gap` (`9bf13403`); superseded by the approved design rev 2 and r=2 plan (`bd4af157`).
- ALL-barrier previously passed on the original 6-task set; re-opened by the expansion, now re-passed.
