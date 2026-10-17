# Collection 1.0.9-rc3-open-3 — status

- collection-id: `1.0.9-rc3-open-3`
- slug: `collection-1.0.9-rc3-open-3`
- input: **open issues of milestone `1.0.9-rc3`**, re-read 2026-10-17 via `gh`: **#243, #244, #245, #246, #247**.
- base: branch `1.0.9-rc3` @ `b5ab6502` (current collection base; benchmarks and tests grouped by topic, docs renumbered). Historical: branch `1.0.9-rc3` @ `01815f3a` (owner snapshot `957fcfe7`: 2026-10-17 warm-path report, BDN artifacts, table generator + CI step, cached-preparation WIP; build Debug 0W/0E; targeted suites 23/23 + 84/84; pushed on explicit owner request).
- mode: **autonomous + auto-commit** (explicit owner request: «автономный pdca с автокомитам по открытым задачам rc3»). Push is **not** part of the collection; only the bootstrap snapshot was pushed on explicit request.
- phase: **D — G1 stopped (T243 done; T246 terminal GAP), owner-approved re-schedule into G2: T246 rv2 → T244 → T245 → T247**.

## Tasks

| id | issue | title | variant | status | plan_state | cycle N | r | rv | status file | baseline ref | commit |
|----|-------|-------|---------|--------|-----------|---|--|----|-------------|--------------|--------|
| T243 | #243 | Perf: warm-path Count/Sum allocations exceed linq2db | pdca-dotnet | done | ready | 1 | 1 | 1 | docs/specs/status/D243-warm-aggregates-1.md | 957fcfe7 | b5b717cc |
| T246 | #246 | Perf: ToDataReader raw reader is slower than linq2db | pdca-dotnet | incomplete (terminal GAP rv3) | ready (rv3) | 1 | 3 | 3 | docs/specs/status/D246-raw-datareader-3.md | 587397b1 | — |
| T244 | #244 | Perf: warm CTE/recursive CTE allocations exceed linq2db (#240 follow-up) | pdca-dotnet | ready (resumed in G4) | ready | 1 | 1 | 1 | docs/specs/status/D244-warm-cte-1.md | 957fcfe7 | — |
| T245 | #245 | Perf: warm EXCEPT/INTERSECT allocations exceed linq2db (#240 follow-up) | pdca-dotnet | queued (G4) | ready | 1 | 1 | 1 | docs/specs/status/D245-warm-set-ops-1.md | 957fcfe7 | — |
| T247 | #247 | Perf: JSON streaming is slower than linq2db at 10k rows | pdca-dotnet | queued (G4) | ready | 1 | 1 | 1 | docs/specs/status/D247-json-streaming-1.md | 957fcfe7 | — |

Rows are listed in **lane order** (topological). Plan files: 273 / 481 / 336 / 604 / 208 lines, all CRLF, all persisted by their own PLAN step (no code, no benchmarks, no commits).

## Groups (allocated only after ALL-barrier)

| group | tasks (topological order) | worktree | branch/ref | status |
|-------|---------------------------|----------|------------|--------|
| G1 | **T243 → T246 → T244 → T245 → T247** | none (single group) | current branch `1.0.9-rc3` | **incomplete** — stopped by terminal T246 GAP (T243 done; T246 incomplete; T244/T245/T247 not executed) |
| G2 | **T246 (rv2) → T244 → T245 → T247** | none (single group) | current branch `1.0.9-rc3` | **stopped** — T246 incomplete (rv2 GAP); T244 incomplete (outside-cycle blocker); T245/T247 not executed («группа остановлена») |

## Phase decisions

- **P.1 Bootstrap (2026-10-17):** scope = the 5 open issues of milestone `1.0.9-rc3`. Variant by language rule: all C#/.NET → `pdca-dotnet`. Input snapshot committed as `957fcfe7` (owner decision: «закомитить запушить»).
- **P.2 Own PLAN per task — DONE (2026-10-17).** All five `plan_state=ready`, N=1, r=1, rv=1, CHECK re-gather budget 2, evidence manifests deferred to DO→CHECK:
  - **T243** — `V-D-owned-immutable-scalar-artifacts` (context-owned immutable scalar shape/normalization artifacts + invocation-local binding; fail-closed fallback). Contract `E243-01..14` / `R243-01..12`. Oracle reproducible (committed BDN CSV reproduces 3,824/2,864 and 4,226/3,490 B). Own gate `scripts/perf/d243_aggregate_gate.py`; **no** shared `iteration14` edit; report page untouched (fragment only).
  - **T246** — `V2` (isolated reuse of immutable raw preparation + lightened existing wrapper). Contract `E246-01..15` / `R246-01..13`. Correction carried: on SQLite `LobLocatorColumn = "rowid"`, so `ToDataReader` already uses the **result-reader** seam; the issue's `OpenLobReader` seam is the PG/MSSQL branch. Own `scripts/perf/t246_evidence.py` + `t246-budgets.json`; no shared `iteration14` edit.
  - **T244** — contract `E244-01..18` / `R244-01..14`. Corrected fact: the issue's hypothesized allocators (hoister / `EnsureNoUnhoistedCtes` / CTE plan hash) are **not** on the measured warm path (raw-recipe hit skips `Prepare`); attribution spike must rank real allocators first. Edits `iteration14_gate.py` + budgets (first owner).
  - **T245** — contract `E245-01..16` / `R245-01..16`; caps converted to 5,171.2 / 5,130.24 B/op. Verified inert: the "Clone copies empty metadata" hypothesis (`_joins`/`_sorting`/`_arrayJoins` are null for this shape). Adds 2 budget rows (second gate owner).
  - **T247** — contract `E247-01..19` / `R247-01..11`. Oracle needs a new `ToList_Dto + JsonSerializer` control arm (D247-01) before it is obtainable. Per-row flush removal deferred (would break exact-prefix cancellation tests); chosen fix = bounded 64-entry context-owned compiled-writer cache. Adds 3 gate rows (third gate owner).
- **P.3 ALL-barrier — PASSED (2026-10-17).** All five plans persisted and `ready`; no gap placeholders.
- **P.4 Clustering (2026-10-17) — ONE group G1.** All five share `benchmarks/nextorm.benchmark/**`, `docs/comparisons/feature-benchmarks-2026-10-17.md` (+RU) and `docs/specs/performance/**`; `scripts/perf/iteration14*` is shared by T244/T245/T247; `DataContext.cs` by T243/T246/T247; `QueryPlanner.cs` by T243/T246; `SqliteBenchmarkWarmDecompose.cs` by T244/T245. Disjoint footprints are impossible → one connected component, sequential lane on the current branch, **no worktree/group branch/merge**. Hard DAG edge: **T244 → T245**.
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
- **P.5 Re-schedule (2026-10-17) — OWNER-APPROVED.** After the G1 stop, the owner chose «Ревизия протокола T246, затем очередь». Decision: reopen **T246 only** with a **bounded measurement-protocol revision (rv2, r=2)** and keep the substantive acceptance gates unchanged (`time_upper_ratio ≤ 1.00`, `allocation_ratio ≤ 1.00`, zero empty tolerance); precision must be demonstrated **before** any production change. After T246 (done or a second terminal GAP) the queue continues with **T244 → T245 → T247** under their existing rv1 plans. Rationale for a new group rather than resuming G1: the collection rule forbids automatic rerun/re-schedule after a lane starts; G1 stays closed as `incomplete`, G2 carries only the remaining work. T246 rv1 contract (`E246-01..15` / `R246-01..13`) is preserved verbatim; rv2 may add protocol rows, never rewrite rv1 rows. T243 stays `done` and is not re-executed. **Stop semantics for G2 (owner-approved):** a second terminal GAP in T246 rv2 (precision still unattainable) closes T246 as `incomplete` with the evidence preserved and does **not** stop the lane — T246 makes no production change and T244/T245/T247 are semantically independent of it, so the queue continues with T244 → T245 → T247. Only a terminal `incomplete` in one of T244/T245/T247 stops the remaining queue.

## Lane log

- 2026-10-17 | bootstrap | owner snapshot committed and pushed as `957fcfe7`; collection `1.0.9-rc3-open-3` created with 5 tasks.
- 2026-10-17 | P | five own PLANs persisted (`D243`, `D246`, `D244`, `D245`, `D247`); ALL-barrier passed; single group G1; schedule rv=1 bound.
- 2026-10-17 | prereq | benchmark DB `benchmarks/nextorm.benchmark/data/test.db` OK (`large_table` = 10,000 rows); Podman socket started and present; `flock` available; `/tmp/nextorm-bench/` absent (T246 owns bootstrap).
- 2026-10-17 | G1 | lane started on `1.0.9-rc3` (single tree, sequential queue).
- 2026-10-17 | G1 | T243 done — Count 2979.84→408 B/op, Sum 3389.44→970 B/op; gate green; 6/6 mutations killed; commit b5b717cc. Next: T246.
- 2026-10-17 | G1 | T246 incomplete — baseline precision predicates unattainable (10k B spread 50.08 %; empty Error/Mean 11.74/12.33 %); planner terminal GAP; no production change; artifacts preserved.
- 2026-10-17 | G1 | lane stopped — T244/T245/T247 marked incomplete «группа остановлена», not executed.
- 2026-10-17 | P.5 | owner approved «Ревизия протокола T246, затем очередь» → G2 created: T246 (rv2) → T244 → T245 → T247; acceptance gates unchanged, precision demonstrated before production work.
- 2026-10-17 | G2 | rv2 PLAN for T246 dispatched (bounded protocol revision; r=2, rv=2, N=1).
- 2026-10-17 | PLAN/T246 | rv2 ready — N=1; r=2; rv=2; n=1; iteration 1/3; bounded measurement-protocol revision; production blocked until precision PASS; both ≤1.00 gates and empty tolerance 0 % unchanged.
- 2026-10-17 | G2 | T246 rv2 qualification: 4 executions, 0 valid runs (harness parser defect consumed the ceiling); 4/7 arms per attempt; empty stratum never started.
- 2026-10-17 | G2 | T246 terminal rv2 GAP — planner adjudicated; no production change; issue #246 stays open with measured evidence; lane continues with T244.
- 2026-10-17 | G2 | T244 baseline attempt: round-01 invalid (foreign build rebuilt the benchmark assembly 15 s into invocation 1; HEAD moved; tree 228→903→1042); round-01 numbers not acceptance evidence.
- 2026-10-17 | G2 | T244 own harness defect: two empty-target [GlobalSetup] in SqliteBenchmarkWarmDecompose → BDN refuses the class; unfixed, preserved.
- 2026-10-17 | G2 | T244 incomplete — outside-cycle blocker (concurrent session owns branch/tree; no exclusive host/stable tree; canonical flock bypassed). Escalate decision: STOP. T245/T247 not executed («группа остановлена»).
- 2026-10-17 | P.6 | benchmark/test/docs refactor committed as b5ab6502 (owner request) and pushed; plans D244/D245/D247 re-anchored to the new layout; repo test conventions (FluentAssertions-only, integration = real DBMS) now apply.
- 2026-10-17 | PLAN/T246 | T246 reopen rv3 — N=1; r=3; rv=3; n=1; iteration 1/3; harness-first Gate H before any measurement; production blocked; caps, ≤1.00 gates and empty tolerance 0 % unchanged.
- 2026-10-17 | G3 | T246 rv3 Gate H GREEN, 0 measurements (unit 63 tests rc=0; self-test rc=0 21 cases; parse-check 4/4 operations=64 relative_diff=0.0; resolved-job red→green); boundary green (build 0W/0E; core 2638/2638; sqlite 1783+1 skip; integration 3868/0/199 skips).
- 2026-10-17 | G3 | T246 rv3 qualification: 4/4 executions, 3 harness-valid, 1 lane-admissible; run-3/4 invalidated `foreign-competing-workload-mid-block`; qualify rc=2 `mode-pair-not-represented`; no protocol-lock ⇒ gate rc=2; ratios unavailable.
- 2026-10-17 | G3 | T246 rv3 terminal GAP — `incomplete`, no production change, #246 stays open; evidence artifacts/pdca/T246/rv3/GAP.md + D246-3 §Terminal GAP.
- 2026-10-17 | G3 | lane stopped — host-level role outage (`API key expired`; dispatch `Go requires a credential`, 7 attempts): cheap tier was bound to `opencode-go` and the replacement Console key has no active Go subscription. Roles restored via documented fallback `deepseek/deepseek-flash`; T244/T245/T247 not started.
- 2026-10-17 | P.7 | owner: continue the queue → G4 created: T244 → T245 → T247. Cheap tier (coder/scout/btw/pdca-executor/pdca-orchestrator) repointed to `deepseek/deepseek-flash` (OpenCode Go subscription absent on the replacement key); medium/strong tiers stay on `opencode/*` (Console PAYG, verified). Acceptance gates, caps and the T244→T245 edge unchanged.
- 2026-10-17 | G4 | T244 DO started — harness [GlobalSetup] defect fixed and BDN class accepted; path re-anchor to .pdca/scripts/perf recorder; baseline rounds next.
- 2026-10-17 | G4 | T244 baseline round 1 — INVALID (tool rc=2; 9 reason codes; tree 63→80; HEAD bb95d859 unchanged; harness rewrites 12 tracked BDN reports under benchmarks/BenchmarkDotNet.Artifacts/results/ that its own whole-porcelain source-state hash covers → self-invalidation; foreign lane writes + live foreign opencode/roslynq session in-block; inv1 rebuilt the benchmark assembly d08f81a5→bc8de537). 0/2 valid baseline rounds; round numbers are not acceptance evidence; next: planner decision on harness source-state scope and/or exclusive-tree isolation.

## General verification & recovery

- state: **`running — G4 (T244 → T245 → T247)`**.
- last C result / evidence: **CHECK pass — T243 R243-01..R243-12 met (12/12), 0 defects**; evidence `docs/specs/status/D243-warm-aggregates-1.md` (§Done / Verified) + `docs/specs/performance/d243-warm-aggregates.md`.
- defect id / history: `mutation-e-cache-flag-guard` — observed at r=1/n=1, survived first probe, 1 fix applied (focused `DisablingCacheAfterWarm_…` test), then **killed**; `suite-state-flake` (integration run-2, 124 failures) — not reproduced on identical binaries, 0 fixes, recorded as transient container-state, not a D243 defect. `perf-oracle-window-noise` (T246) — r=1/n=1, 0 fixes, planner terminal GAP (see D246 §GAP disposition), reopened by owner as rv2. `harness-report-precision` (T246 rv2) — r=2/n=1, 4 attempts, 0/3 valid runs; 1 fix applied (BDN log per-op normalisation) but only after the attempt ceiling was consumed, plus two further harness fixes (resolved-job semantics, idle `VBCSCompiler` exclusion); 0 contract rows met, evidence `docs/specs/status/D246-raw-datareader-2.md` §Terminal GAP — 2026-10-17 (rv2) + `artifacts/pdca/T246/rv2/GAP.md`; `concurrent-branch-ownership` (T244/G2) — outside-cycle blocker: foreign session owns branch/tree (`25a8a4ed`/`5179f6c1`), ~1042 dirty entries (703 test renames, 80 benchmark renames), no 120 s quiet window in 25 min, canonical flock bypassed; 0 fixes; lane stopped — see `docs/specs/status/D244-warm-cte-1.md` §Lane disposition. `foreign-workload-mid-block` (T246 rv3) — r=3/n=1: run-3/4 invalidated by foreign `dotnet build`/`run` integration, `dotnet-coverage` and `dotnet test` inside the block; 1 remedy (empty invocationCount 4096→8192); terminal GAP. `host-role-outage` (G3) — planner `API key expired`, all dispatch `Go requires a credential` (7 attempts); root cause: cheap tier bound to `opencode-go`, replacement Console key has no active Go subscription; 0 product fixes, roles restored on `deepseek/deepseek-flash`.
- correction-task status file: none.
- next allowed step: **resume the queue at T244** with the cheap tier on `deepseek/deepseek-flash` (Go subscription absent on the replacement key); T246 can only be reopened by owner authorisation plus a verified host-condition change (D246-3 §GAP rule).

## Done / Verified / Incomplete

- **Done:** T243 (`b5b717cc`).
- **Verified:** T243 — CHECK pass 12/12.
- **Incomplete:** T246 (terminal GAP in rv1; rv2 terminal **GAP — 0/3 valid runs, empty arm unmeasured**; rv3 **GAP — Gate H green and boundary green, but 4/4 qualifications spent, 1 lane-admissible run, protocol lock absent, ratios unavailable**; no production change, issue #246 stays open), T244 (incomplete — outside-cycle blocker), T245/T247 (**group stopped** («группа остановлена»), not executed).
- 2026-10-17 | G4 | T244 harness fix — `source_state()` now fingerprints only compiled inputs (`src`, `benchmarks/nextorm.benchmark`, `Directory.*.props`, `global.json`, `nextorm.slnx`, `.editorconfig`) with a post-filter that drops `BenchmarkDotNet.Artifacts/`, `artifacts/`, `TestResults/` and any `bin`/`obj` segment, so BDN rewriting 12 tracked reports no longer self-invalidates a round; 76 harness tests rc=0; staged Release build 0 Warning/0 Error (`nextorm.benchmark.dll` abc9110d…); baseline round-01 retained as `invalid-round-01-structure`; no measurement yet, 0/2 valid baseline rounds.
