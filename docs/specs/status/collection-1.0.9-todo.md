# Collection 1.0.9-todo

- collection-id: `1.0.9-todo`
- input: milestone `1.0.9` (milestone #4) — **expanded**: originally {#213, #212, #211, #209, #201, #53}; owner added {#215, #216, #217, #218}. Active set now: T211, T212, T213, T201, T215, T216, T217, T218 (+ T209 done). T53 held by owner directive.
- base: branch `1.0.9` @ `58025147` (merge of #214 `1.0.9-rc2`)
- mode: autonomous; auto-commit authorized (task/collection status files and task paths only, explicit paths, never `-A`, message prefix `#<issue>`); push **never**
- skills: `pdca-collection` + `pdca-dotnet` + `nextorm-pdca`
- phase: **P re-open (re-scope)** — expanding the collection with the new tasks; the G1 lane is re-opened (T211 fail-stop overridden by owner directive); single group → current worktree/branch `1.0.9`, no worktree/branches/merge

## Input task list

Selected variant: `pdca-dotnet` for all tasks (C#/.NET repo). Each task runs its own real PLAN at the P phase, stops at the PLAN→DO boundary, and the lane resumes the **same** cycle from DO.

| id | issue | title | selected_variant | plan_state |
|---|---|---|---|---|
| T209 | #209 | eager-load strategy — explicit strategy + auto-dispatcher | pdca-dotnet | done (r=3) |
| T211 | #211 | eager-load per-parent Take/Skip (absorbs #210) | pdca-dotnet | ready (r=1) → **re-open needed (r=2)** |
| T212 | #212 | eager-load nested — level 2+ LoadWith | pdca-dotnet | ready (r=1) |
| T213 | #213 | JoinInto — composite keys / M:N single-query | pdca-dotnet | ready (r=1) |
| T201 | #201 | Perf: M12 #3 — cached-path parity with LINQ to DB | pdca-dotnet | ready (r=2) |
| T215 | #215 | убрать зависимость OneOf | pdca-dotnet | pending (new) |
| T216 | #216 | row locking — FOR UPDATE vs FOR UPDATE OF | pdca-dotnet | pending (new) |
| T217 | #217 | DateTimeOffset — SQL-трансляция Now/UtcNow и частей даты | pdca-dotnet | pending (new) |
| T218 | #218 | SQL Server sequences (NEXT VALUE FOR), кросс-провайдерно | pdca-dotnet | pending (new) |
| T53 | #53 | IF6: public API freeze (PublicApiAnalyzers + PublicAPI.*) | pdca-dotnet | ready (r=1) — **HELD by owner** |

## Groups

| id | tasks (order) | worktree | branch/ref | status |
|---|---|---|---|---|
| G1 | T209 (done) → T211 → T212 → T213 → T201 → {T215, T216, T217, T218} → T53 (held) | no (single group) | current branch `1.0.9` | pending re-cluster |

Order within `{T215, T216, T217, T218}` and the final placement of T53 are to be recomputed by `planner` after all own PLANs are `ready` (re-cluster on the expanded set).

## Tasks

| id | issue | group | order | status | plan_state | selected_variant | cycle_id | plan_revision | PLAN commit | status file |
|---|---|---|---|---|---|---|---|---|---|---|
| T209 | #209 | G1 | 1 | done | done | pdca-dotnet | N=1 | r=3 | 5e9f6756 | docs/specs/status/D209-eager-strategy-1.md |
| T211 | #211 | G1 | 2 | pending (PLAN re-open) | ready→r2 | pdca-dotnet | N=1 | r=2 | e18dc143 | docs/specs/status/D211-eager-perparent-topn-1.md |
| T212 | #212 | G1 | 3 | pending | ready | pdca-dotnet | N=1 | r=1 | 034e5b45 | docs/specs/status/D212-eager-nested-1.md |
| T213 | #213 | G1 | 4 | pending | ready | pdca-dotnet | N=1 | r=1 | 9e7facff | docs/specs/status/D213-joininto-remainder-1.md |
| T201 | #201 | G1 | 5 | pending | ready | pdca-dotnet | N=1 | r=2 | bd4af157 | docs/specs/status/D201-cached-parity-1.md |
| T215 | #215 | G1 | TBD | pending | pending | pdca-dotnet | N=1 | — | — | docs/specs/status/D215-oneof-removal-1.md |
| T216 | #216 | G1 | TBD | pending | pending | pdca-dotnet | N=1 | — | — | docs/specs/status/D216-row-locking-1.md |
| T217 | #217 | G1 | TBD | pending | pending | pdca-dotnet | N=1 | — | — | docs/specs/status/D217-datetimeoffset-1.md |
| T218 | #218 | G1 | TBD | pending | pending | pdca-dotnet | N=1 | — | — | docs/specs/status/D218-sqlserver-sequences-1.md |
| T53  | #53  | G1 | last | held | ready | pdca-dotnet | N=1 | r=1 | (untracked, D53 file) | docs/specs/status/D53-public-api-freeze-1.md |

- Evidence contract (per task, frozen at that task's DO→CHECK): `rv=1`; manifests `artifacts/pdca/<task>/rv1/manifest.json`.
- T211 history: r1 was exhausted (3/3 CHECK fails); the last gate (EV-11) was rejected for a **missing exit-code capture** of the Stryker timeout re-run — an evidence-capture gap, **not** a demonstrated product/config defect. Owner directive re-opens T211 via a revised PLAN (r=2) that fixes the evidence capture; the retained commits stay (none reverted by the re-open).

## Phase decisions

- **Bootstrap (original):** 6 tasks (#209, #211, #212, #213, #201, #53), stable ids, briefs, `selected_variant=pdca-dotnet`.
- **Own PLANs (original):** all re-finalized `ready` after the medium-tier `planner` provider was funded; T201 later re-opened to `ready (r=2)` with an approved, separately-reviewed written design (`docs/specs/performance/m12-3-prepare-command-design.md`, rev 2).
- **ALL-barrier:** PASSED for the original set. **Re-opened** by the scope expansion: T215–T218 have no PLAN yet and T211 needs a revised r=2 → the barrier must re-pass before the expanded lane continues.
- **Clustering:** original **one group G1** (single lane; shared files force serialization). Pending **re-cluster** on the expanded set by `planner` once T215–T218 and T211-r2 are `ready`.
- **PLAN revision vector (schedule binding):** T209 r=3 (done), T211 r=2 (pending), T212 r=1, T213 r=1, T201 r=2, T53 r=1 (held); T215–T218 pending.

## Re-scope amendments (user directives, post-lane-start)

1. **#201 precedes the freeze (#53)** — T201's outcome may change the public API, so T53's baseline must include it.
2. **Milestone grew; do not close the public API yet.** New tasks #215 (drop OneOf), #216 (row locking), #217 (DateTimeOffset), #218 (SQL Server sequences) are added to the collection. **T53 is HELD** — the public-API freeze will not run until the owner says the API is final.
3. **Expansion mode:** extend the current collection (not a fresh run); T211 fail-stop overridden by owner directive (re-open with a revised r=2 plan).

## Общая верификация и восстановление

- Verification state (C): not reached — collection mid-flight (re-scope).
- Last collection `C` result: —
- Blocker: — (T211 incomplete superseded by the owner-directed re-open; its patch is retained as evidence)
- Defect id / history: —
- Corrective-task status file: —
- Next allowed step: complete own PLANs for T215–T218 and re-open T211 (r=2) → re-pass ALL-barrier → re-cluster (`planner`) → resume the expanded lane from T211.

## Done / Verified / Incomplete

- Done: T209 (#209) — eager-load strategy (explicit strategy + auto-dispatcher); cycle N=1 r=3 n=2/3, CHECK PASS, contract rv=2; status file `docs/specs/status/D209-eager-strategy-1.md`.
- Verified: T209 (#209) — rv=2 evidence (build 0/0; six dialect suites 0 failures; integration all 5 providers executed; coverage line 88.5% / branch 80.5%; acceptance benchmark 7/7, no ratio >20%); manifest `artifacts/pdca/D209/rv2/manifest.json` E209-01..21 all `met`, validator exit 0; CHECK report `artifacts/pdca/D209/rv3/check-report.md`.
- Incomplete: T211 (#211) — the earlier `incomplete` (r1 exhausted; EV-11 exit-code capture gap) is **superseded by the owner-directed re-open**; patch `artifacts/pdca/D211-eager-perparent-topn-1/incomplete/D211-incomplete.patch`; retained commits `e98ae60e`, `5bd3028f`, `15c6253f`.
- Incomplete (superseded by re-scope): T212/T213/T201/T53 were marked «группа остановлена» by the T211 fail-stop; reinstated as `pending` (T53 `held`) under the expansion directive.

## History (superseded current-state facts)

- Original input was 6 tasks with G1 order `T209 → T211 → T212 → T213 → T53`; superseded by the amended order and the expansion set above.
- T201 was recorded as `gap → excluded-gap` (PLAN commit `9bf13403`); superseded by the approved design rev 2 and r=2 plan (`bd4af157`), `plan_state=ready`.
- The ALL-barrier previously passed on the original 6-task set; re-opened by the expansion (new tasks + T211 r=2).
