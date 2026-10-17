# Collection 1.0.9-rc3-open-3 — status

- collection-id: `1.0.9-rc3-open-3`
- slug: `collection-1.0.9-rc3-open-3`
- input: **open issues of milestone `1.0.9-rc3`**, re-read 2026-10-17 via `gh`: **#243, #244, #245, #246, #247**.
- base: branch `1.0.9-rc3` @ `01815f3a` (owner snapshot `957fcfe7`: 2026-10-17 warm-path report, BDN artifacts, table generator + CI step, cached-preparation WIP; build Debug 0W/0E; targeted suites 23/23 + 84/84; pushed on explicit owner request).
- mode: **autonomous + auto-commit** (explicit owner request: «автономный pdca с автокомитам по открытым задачам rc3»). Push is **not** part of the collection; only the bootstrap snapshot was pushed on explicit request.
- phase: **D — lane G1 stopped (T243 done; T246 terminal GAP)**.

## Tasks

| id | issue | title | variant | status | plan_state | cycle N | r | rv | status file | baseline ref | commit |
|----|-------|-------|---------|--------|-----------|---|--|----|-------------|--------------|--------|
| T243 | #243 | Perf: warm-path Count/Sum allocations exceed linq2db | pdca-dotnet | done | ready | 1 | 1 | 1 | docs/specs/status/D243-warm-aggregates-1.md | 957fcfe7 | b5b717cc |
| T246 | #246 | Perf: ToDataReader raw reader is slower than linq2db | pdca-dotnet | incomplete | gap | 1 | 1 | 1 | docs/specs/status/D246-raw-datareader-1.md | 957fcfe7 | — |
| T244 | #244 | Perf: warm CTE/recursive CTE allocations exceed linq2db (#240 follow-up) | pdca-dotnet | incomplete («группа остановлена») | ready | 1 | 1 | 1 | docs/specs/status/D244-warm-cte-1.md | 957fcfe7 | — |
| T245 | #245 | Perf: warm EXCEPT/INTERSECT allocations exceed linq2db (#240 follow-up) | pdca-dotnet | incomplete («группа остановлена») | ready | 1 | 1 | 1 | docs/specs/status/D245-warm-set-ops-1.md | 957fcfe7 | — |
| T247 | #247 | Perf: JSON streaming is slower than linq2db at 10k rows | pdca-dotnet | incomplete («группа остановлена») | ready | 1 | 1 | 1 | docs/specs/status/D247-json-streaming-1.md | 957fcfe7 | — |

Rows are listed in **lane order** (topological). Plan files: 273 / 481 / 336 / 604 / 208 lines, all CRLF, all persisted by their own PLAN step (no code, no benchmarks, no commits).

## Groups (allocated only after ALL-barrier)

| group | tasks (topological order) | worktree | branch/ref | status |
|-------|---------------------------|----------|------------|--------|
| G1 | **T243 → T246 → T244 → T245 → T247** | none (single group) | current branch `1.0.9-rc3` | **incomplete** — stopped by terminal T246 GAP (T243 done; T246 incomplete; T244/T245/T247 not executed) |

## Phase decisions

- **P.1 Bootstrap (2026-10-17):** scope = the 5 open issues of milestone `1.0.9-rc3`. Variant by language rule: all C#/.NET → `pdca-dotnet`. Input snapshot committed as `957fcfe7` (owner decision: «закомитить запушить»).
- **P.2 Own PLAN per task — DONE (2026-10-17).** All five `plan_state=ready`, N=1, r=1, rv=1, CHECK re-gather budget 2, evidence manifests deferred to DO→CHECK:
  - **T243** — `V-D-owned-immutable-scalar-artifacts` (context-owned immutable scalar shape/normalization artifacts + invocation-local binding; fail-closed fallback). Contract `E243-01..14` / `R243-01..12`. Oracle reproducible (committed BDN CSV reproduces 3,824/2,864 and 4,226/3,490 B). Own gate `eng/perf/d243_aggregate_gate.py`; **no** shared `iteration14` edit; report page untouched (fragment only).
  - **T246** — `V2` (isolated reuse of immutable raw preparation + lightened existing wrapper). Contract `E246-01..15` / `R246-01..13`. Correction carried: on SQLite `LobLocatorColumn = "rowid"`, so `ToDataReader` already uses the **result-reader** seam; the issue's `OpenLobReader` seam is the PG/MSSQL branch. Own `eng/perf/t246_evidence.py` + `t246-budgets.json`; no shared `iteration14` edit.
  - **T244** — contract `E244-01..18` / `R244-01..14`. Corrected fact: the issue's hypothesized allocators (hoister / `EnsureNoUnhoistedCtes` / CTE plan hash) are **not** on the measured warm path (raw-recipe hit skips `Prepare`); attribution spike must rank real allocators first. Edits `iteration14_gate.py` + budgets (first owner).
  - **T245** — contract `E245-01..16` / `R245-01..16`; caps converted to 5,171.2 / 5,130.24 B/op. Verified inert: the "Clone copies empty metadata" hypothesis (`_joins`/`_sorting`/`_arrayJoins` are null for this shape). Adds 2 budget rows (second gate owner).
  - **T247** — contract `E247-01..19` / `R247-01..11`. Oracle needs a new `ToList_Dto + JsonSerializer` control arm (D247-01) before it is obtainable. Per-row flush removal deferred (would break exact-prefix cancellation tests); chosen fix = bounded 64-entry context-owned compiled-writer cache. Adds 3 gate rows (third gate owner).
- **P.3 ALL-barrier — PASSED (2026-10-17).** All five plans persisted and `ready`; no gap placeholders.
- **P.4 Clustering (2026-10-17) — ONE group G1.** All five share `benchmarks/nextorm.benchmark/**`, `docs/comparisons/feature-benchmarks-2026-10-17.md` (+RU) and `docs/specs/performance/**`; `eng/perf/iteration14*` is shared by T244/T245/T247; `DataContext.cs` by T243/T246/T247; `QueryPlanner.cs` by T243/T246; `SqliteBenchmarkWarmDecompose.cs` by T244/T245. Disjoint footprints are impossible → one connected component, sequential lane on the current branch, **no worktree/group branch/merge**. Hard DAG edge: **T244 → T245**.
- **D merge strategy:** n/a — single group; task commits land directly on `1.0.9-rc3`.
- **Schedule binding (rv=1):** order `T243 → T246 → T244 → T245 → T247`; all plans stay r=1/rv=1/N=1; the schedule adds coordination obligations only.
- **Measurement rules pinned before the lane:**
  - canonical host lock **`/tmp/opencode/nextorm-bdn-host.lock`** (supersedes every per-plan lock name);
  - one BDN process at a time; every round runs via `flock -x /tmp/opencode/nextorm-bdn-host.lock bash -lc "$ROUND_CMD"`; no second benchmark, including from another session;
  - DB bootstrap/mutation also requires measurement ownership; T246 must resolve the missing `/tmp/nextorm-bench/test.db` explicitly (no silent substitution);
  - gate ownership order: **T244 first, then T245 (+2 rows), then T247 (+3 rows)**; predecessor rows/validation never relaxed; T243/T246 use their own gate files;
  - report-page fragments are merged between tasks (single lane ⇒ no concurrent writer);
  - a round is invalid on lock bypass/overlap, source/environment drift, changed measurement inputs/DB/config, or incomplete evidence; a later edit invalidates earlier evidence only if it touches the same execution path or acceptance gate.
- **Model-region incident (2026-10-17):** the first P.2 launch failed with `Country, region, or territory not supported` on `opencode/gpt-6.1-sol`; probes showed `gemini-4-argon` and `claude-sonnet-5-5` reachable and `grok-4.7` endpoint-unavailable. Retry of `gpt-6.1-sol` succeeded; **no profile/config change was made** (backup only: `~/.config/opencode/pdca-backups/deepseek.jsonc.*`).

## Lane log

- 2026-10-17 | bootstrap | owner snapshot committed and pushed as `957fcfe7`; collection `1.0.9-rc3-open-3` created with 5 tasks.
- 2026-10-17 | P | five own PLANs persisted (`D243`, `D246`, `D244`, `D245`, `D247`); ALL-barrier passed; single group G1; schedule rv=1 bound.
- 2026-10-17 | prereq | benchmark DB `benchmarks/nextorm.benchmark/data/test.db` OK (`large_table` = 10,000 rows); Podman socket started and present; `flock` available; `/tmp/nextorm-bench/` absent (T246 owns bootstrap).
- 2026-10-17 | G1 | lane started on `1.0.9-rc3` (single tree, sequential queue).
- 2026-10-17 | G1 | T243 done — Count 2979.84→408 B/op, Sum 3389.44→970 B/op; gate green; 6/6 mutations killed; commit b5b717cc. Next: T246.
- 2026-10-17 | G1 | T246 incomplete — baseline precision predicates unattainable (10k B spread 50.08 %; empty Error/Mean 11.74/12.33 %); planner terminal GAP; no production change; artifacts preserved.
- 2026-10-17 | G1 | lane stopped — T244/T245/T247 marked incomplete «группа остановлена», not executed.

## General verification & recovery

- state: **stopped — T246 terminal GAP**.
- last C result / evidence: **CHECK pass — T243 R243-01..R243-12 met (12/12), 0 defects**; evidence `docs/specs/status/D243-warm-aggregates-1.md` (§Done / Verified) + `docs/specs/performance/d243-warm-aggregates.md`.
- defect id / history: `mutation-e-cache-flag-guard` — observed at r=1/n=1, survived first probe, 1 fix applied (focused `DisablingCacheAfterWarm_…` test), then **killed**; `suite-state-flake` (integration run-2, 124 failures) — not reproduced on identical binaries, 0 fixes, recorded as transient container-state, not a D243 defect. `perf-oracle-window-noise` (T246) — r=1/n=1, 0 fixes, planner terminal GAP (see D246 §GAP disposition).
- correction-task status file: none.
- next allowed step: **none in this lane — requires an approved measurement-protocol revision (see D246 GAP disposition).**

## Done / Verified / Incomplete

- **Done:** T243 (`b5b717cc`).
- **Verified:** T243 — CHECK pass 12/12.
- **Incomplete:** T246 (terminal GAP), T244/T245/T247 (lane stopped).
