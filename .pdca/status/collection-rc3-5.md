# Collection rc3-5 — 1.0.9-rc3 remaining perf issues (fresh lane)

- collection-id: `rc3-5`
- entry: user requested a **fresh lane** after `rc3-4` stopped (`blocked` C; T245 terminal `incomplete`); routing `pdca-collection`; four remaining rc3 perf tasks.
- mode: **autonomous**; **auto-commit explicitly authorised by the user**; **push is never allowed**; single group → no worktrees/branches/merge.
- repo / branch: `/home/alex/sources/nextorm`, `1.0.9-rc3`
- collection base ref: `bcb8e676` (HEAD at collection start). Reused task plans are bound to `0d4b0cf6`; `git diff --stat 0d4b0cf6..bcb8e676 -- src/` is **empty**, so the plan baselines remain product-equivalent. The lane must re-check this at DO.
- variant per task: `pdca-dotnet`; overlay `.opencode/skills/nextorm-pdca/SKILL.md`.
- lane cap: **1** — exclusive benchmark host window; one sequential group.
- host resources: BDN lock `/tmp/opencode/nextorm-bdn-host.lock`; pinned DB `sha256 32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; containers via `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`.
- worktree hygiene: the current dirty paths are only untracked `.pdca/status/*` plan/collection files. **Never `git add -A`**; stage only task-owned paths. Do not touch the T245 status/evidence or the rc3-4 collection file.
- T245 is **excluded**: terminal `incomplete` (`insufficient_evidence: unexplained_baseline_plus_1250B`); its follow-up is a bounded single-case bisect then a new plan, not part of this lane. Issue #245 stays open.

## Tasks (reused ready plans; ALL-barrier satisfied)

| id | issue | group | variant | plan_state | status | branch | cycle_id | status_path | plan_revision | baseline | notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| T246 | #246 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T246-raw-datareader-1.md` | r=5 | `0d4b0cf6` | **terminal `incomplete` (planner)**: measured gate FAIL (10k U 1.136955–1.180848; A10k 4,132,558–4,236,841 ns vs cap 3,942,000; alloc ratio 1.003080–1.003197; empty U 1.763487–1.792835; empty alloc 5.040323) and remaining budget 1,334.35 s cannot finance the three required admissible blocks (~1,530 s); E246-409/410 FAIL, E246-414 met, E246-408/411/412/413 not run/GAP (not waived); preserved patch `.pdca/T246/rv5/failure/t246-r5-incomplete.patch`; counters unchanged N=1/r=5/n=1/3/rv=5 |
| T250 | #250 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T250-csv-terminal-plan-cache-1.md` | r=1 | `0d4b0cf6` | **group stopped** — not executed (G1 stopped at T246 terminal `incomplete`); reused rv1 plan unchanged; shared terminal-plan cache not started |
| T311 | #311 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T311-json-streaming-1.md` | r=1 | `0d4b0cf6` | **group stopped** — not executed (G1 stopped at T246 terminal `incomplete`); reused rv1 plan unchanged; oracle never pre-registered, no measurement |
| T247 | #247 | G1 | pdca-dotnet | ready | incomplete | — | N=1 | `.pdca/status/T247-json-streaming-1.md` | r=1 | `0d4b0cf6` | **group stopped** — not executed (G1 stopped at T246; its T311 dependency never reached a `done` handoff); reused rv1 plan unchanged; frozen thresholds untouched |

**Plan-file note:** the reused plan files still name `collection-rc3-4` in their metadata; they are reused verbatim at this collection's PLAN→DO handoff. The authoritative queue/schedule is **this file**. A genuine plan revision must update the task file and rebind the vector below before dispatch.

## Groups

| group | tasks (order) | worktree | branch/ref | status |
|---|---|---|---|---|
| G1 | T246 → T250 → T311 → T247 | /home/alex/sources/nextorm | `1.0.9-rc3` @ `bcb8e676` (no commits) | **incomplete** — group stopped at T246 (terminal `incomplete`); remaining tasks (T250, T311, T247) not executed |

- Single-group policy: current worktree/branch; **no worktrees, no task/group branches, no merge**; auto-commits go to the current branch; push never.
- Order rationale: T246 first (independent, needs the exclusive quiet window); T250 independent; T311 before T247 (hard dependency: T247 consumes T311's pre-registered oracle and paired baseline); T250 deliberately before T311/T247 because all three touch the terminal JSON/CSV caches, and T250's engine extraction lands first.
- T246 must take the BDN host lock for every measurement; no parallel lane exists (cap=1).

## Decisions log

- 2026-10-19: fresh collection after rc3-4 `blocked` (T245 terminal `incomplete`; 0/5 done; zero product diff); user explicitly requested the fresh lane.
- 2026-10-19: scope = the four remaining rc3 tasks; **T245 excluded** (separate bounded diagnosis + new plan).
- 2026-10-19: ALL-barrier satisfied by the four persisted `ready` plans from rc3-4; no plan text changed; applicability to HEAD `bcb8e676` re-checked (src diff from `0d4b0cf6` empty).
- 2026-10-19: **PLAN-revision vector bound**: T246 N=1/r=4, T250 N=1/r=1, T311 N=1/r=1, T247 N=1/r=1. Rebind before any dispatch after a genuine revision.
- 2026-10-19: auto-commit authorised for task-owned paths only; no `git add -A`; commit message starts with the issue number; no push/merge.
- 2026-10-19: T247 dependency gate = T311 handoff (oracle spec + paired baseline + per-row lever); the lane must not start T247 before T311 is terminal `done` with its handoff evidence. If T311 ends `incomplete`, T247 is **group stopped**, not executed.
- 2026-10-19: T246 prerequisite = exclusive quiet window; if a competing workload is detected mid-block, the rv4 oracle's invalidity path applies (do not claim a contaminated run as green).
- 2026-10-20: T246 r=5 rebind (rv5 supersedes rv4; vector T246 N=1/r=5). Reason: the r4 cache lever was rejected by per-call `CommandReaderOwner` ownership; r5 targets an opt-in immutable command-recipe cache. G1 and T246 → in-progress; rv4 artifacts stay read-only.
- 2026-10-20: **T246 terminal `incomplete`** (planner decision, not a CHECK pass): the measured acceptance gate is FAIL over three admissible candidate blocks (10k U 1.136955–1.180848; A10k 4,132,558–4,236,841 ns vs cap 3,942,000; alloc ratio 1.003080–1.003197; empty U 1.763487–1.792835; empty alloc 5.040323), and the remaining benchmark budget (1,334.35 s of 7,200 s) cannot finance the three required admissible blocks (~1,530 s). Counters frozen: N=1, r=5, n=1/3, rv=5; E246-409/410 FAIL, E246-414 met, E246-408/411/412/413 not run/GAP (explicitly not waived).
- 2026-10-20: **G1 stopped** at T246. T250, T311 and T247 are recorded `incomplete` with reason **group stopped** — they were not executed, their plan revisions and baselines stay as recorded (`r=1`, `0d4b0cf6`), and no plan text was changed.
- 2026-10-20: **no issue closure**; issue #246 stays open with the measured FAIL, and #250/#311/#247 stay open as unexecuted.
- 2026-10-20: T246 work preserved as **uncommitted** evidence: working tree not reverted, nothing staged, plus patch `.pdca/T246/rv5/failure/t246-r5-incomplete.patch` (34 paths, 4,285 lines, 236,238 B, sha256 `b226d4061d23f2c584061f15e5ca15919a36965fb9e2cc56feb7c0d53290b83d`). No commit, push or merge occurred.
- 2026-10-20: **parent cleanup** — per the collection failure semantics, T246-owned paths were reverted to the last green tip `bcb8e676` after verifying the patch: `git apply -R --check` exit 0, `git apply -R` exit 0; 34 paths restored/deleted, patch sha256 unchanged; `.pdca/T246/**` and `.pdca/status/**` evidence preserved; foreign docs untouched. `git diff HEAD -- src/ tests/ benchmarks/nextorm.benchmark/` is empty; the 15 BDN reports are clean. Pre-revert snapshot: `.pdca/T246/rv5/failure/pre-revert-status.txt`.
- 2026-10-20: **collection C performed** (pdca-check, independent) — verdict **`blocked`**, not a code defect. 0/1 groups `done`, 0/4 tasks `done`; the measured T246 gate FAIL is a task-level unmet perf target + exhausted 7,200 s benchmark budget, not a regression in the integrated tree. The reverted tree at `bcb8e676` is clean and healthy: Debug build 0W/0E, core 2648/2648, sqlite 1792/1792 (1 pinned skip). A `pass` is not allowed with zero done groups and open/unmet obligations.

## General verification and recovery

- state: **`blocked`** (standard collection C; no `done` group; T246 task-level FAIL + budget exhaustion; no code defect)
- last C: **`blocked`** — completion 0/1 groups, 0/4 tasks; reason: T246 measured FAIL (10k U 1.136955–1.180848, A10k 4,132,558–4,236,841 ns vs 3,942,000; 10k alloc ratio 1.003080–1.003197; empty U 1.763487–1.792835; empty alloc 5.040323) over 3/3 admissible blocks + 1,334.35 s remaining budget vs ~1,530 s required, not a product code defect. E246-409/410 FAIL, E246-414 met, E246-408/411/412/413 GAP (not waived). Raw tree check: Debug build 0W/0E, core 2648/2648, sqlite 1792/1792 (1 skipped); zero diff in `src/`, `tests/`, `benchmarks/nextorm.benchmark/`; T246 patch/revert evidence preserved.
- defect id: — (none in the integrated tree; T246's task-level failure is recorded in `.pdca/status/T246-raw-datareader-1.md` and `.pdca/T246/rv5/gate.json`)
- correction task: — (no collection correction task; T246 requires a **new follow-up plan/lever** outside rc3-5, not a fix of the reverted tree)
- next allowed step: fresh collection lane on `bcb8e676` for **T250 → T311 → T247** (plans stay `ready`; same T311 → T247 dependency), with T245/T246 excluded; a separate T246 follow-up plan is required for a broader lever (per-row reader/adapter fast path or empty-call allocation path). No re-gather can unblock rc3-5; no cleanup while C is not PASS.

## Done / Verified / Incomplete

- Done: **0/4** tasks; **0/1** groups.
- Verified: integrated tree is clean/healthy (build 0W/0E, core 2648/2648, sqlite 1792/1792 with 1 pinned skip), but the collection C is **`blocked`** — no `done` group and open T246 obligations.
- Incomplete: **T246** (terminal `incomplete`: measured gate FAIL + insufficient budget; evidence `.pdca/T246/rv5/**`, patch `.pdca/T246/rv5/failure/t246-r5-incomplete.patch`, tree reverted to `bcb8e676`); **T250, T311, T247** (group stopped — not executed); **G1 incomplete**. T245 stays outside this collection.
- 2026-10-20: **all five open rc3 perf issues moved to `1.0.9-rc5`** by owner instruction — #245, #246, #247, #250, #311. rc3 has no open issues left; no further rc3 lane. The T246 patch/revert evidence stays preserved; T250/T311/T247 plans remain `ready` for rc5 scheduling.
- 2026-10-20: **acceptance policy reformulated** by owner — global goal: allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db; if the chosen method cannot reach it, local acceptance = **one-axis win with no regression on the other** (allocations down + time not worse, or time better + allocations not up). Reaching the global ratios is not required to close a task. Issues #245/#246/#247/#250/#311 updated; the historical rc3 plans require a new plan revision (`r+1`) before rc5 execution.
