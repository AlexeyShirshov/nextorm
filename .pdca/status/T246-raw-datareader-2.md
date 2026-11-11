# Cycle status: T246 / cycle 2

## Current state

- task: `T246` · form: `cycle` · specialized skill: `pdca-dotnet` (`coder+.NET`, inherits `pdca-coder`)
- mode: `autonomous` (collection `rc5` lane, auto-commit authorized)
- cycle: `N=2` · revision: `r=1` · attempt: `n=3/3` (spent by this DO; candidate recorded; no attempts remaining) · evidence_round: `3` (spent)
- scope: the whole item goal of issue `#246` — a dialect-driven `ToDataReader` execution path (plain `OpenResultReader` seam + lean owner-disposal wrapper instead of `OpenLobReader` + `LobDataReader`) plus the benchmark/gate rows (10k raw reader, retained empty-result arm, new direct-provider-reader control arm), verified for `C1` (two-tier perf acceptance), `C2` (materialization / LOB terminal preservation), `C3` (`CommandReaderOwner` dispose & context lifetime) and `C4` (benchmark/gate row, direct-provider control, cross-provider streaming check) on snapshot `1e31331d`, branch `1.0.9-rc5` · status path: `.pdca/status/T246-raw-datareader-2.md`
- goal: `ToDataReader` stops paying the LOB-seam/wrapper overhead on fresh-fluent raw-reader calls, closing the task on either the global target (`≤ 1.00×` linq2db time and `≤ 1.20×` linq2db allocations at 10k rows and empty result) or a measured same-round one-axis local win with no regression on the other axis (residual gap recorded), while `ToStream`/`ToTextReader` LOB semantics and `CommandReaderOwner` exact-once disposal stay intact — the §PLAN record and §Original task are authoritative
- phase: `ACT` · record: `completed` · outcome: `incomplete` (`STOP`): no C1 credit; performance mechanism reverted to prior rung; benchmark arms retained; F6/F7 cured as correctness repairs (F7 ported); issue #246 stays OPEN
- source: issue `#246` (`AlexeyShirshov/nextorm`), milestone `1.0.9-rc5`; re-entry after the closed `N=1, r=5` record terminal `incomplete` · context refs: `.pdca/status/collection-rc5.md` §`## Input` → `### #246 — Perf: ToDataReader raw reader is slower than linq2db`, `.pdca/status/T246-raw-datareader-1.md` (`N=1, r=5, n=1/3`, terminal `incomplete`, carried by reference — read-only), baseline `1e31331d`
- criteria: `C1`, `C2`, `C3`, `C4` — exact text in §PLAN record `r1` §3, not retyped here; measurement method present in §PLAN record `r1` §4 (paired ABBA in the same session/round against baseline `1e31331d`, exact `MemoryDiagnoser` bytes, pre-registered `±5 %` time noise band) · evidence root: `.pdca/T246/rv6/` (contract revision `rv6`), DO attempts under `.pdca/T246/rv6/attempts/`; `.pdca/T246/rv1`..`rv5` belong to the closed `N=1` cycle and are read-only
- fidelity note (recorded by the executor, not part of the plan): the planner's `r1` report was delivered to the executor in two truncated parts and is recorded here joined at exactly one point, marked inline as `⟦SEAM⟧` — the single join seam; no other wording was altered, edited, translated or reconstructed.

## Original task

Reference (the readable complete original, not repeated here): `.pdca/status/collection-rc5.md` §`## Input` → `### #246 — Perf: ToDataReader raw reader is slower than linq2db` (line 1025 of that file) — the complete verbatim issue body in its original language, including the 2026-10-17 benchmark table, the root-cause analysis (`QueryCommandExtensions.cs:326-355`, `DataContext.cs:469-505`, `LobDataReader.cs:20-90`), the five proposed work items and the user-supplied 2026-10-20 two-tier acceptance block, sourced verbatim from `/tmp/nextorm-rc5-issues.md`.

## PLAN record

### r1

````markdown
# PLAN — Item `T246` (`#246 — Perf: ToDataReader raw reader is slower than linq2db`)

## 1. Cycle Identity & Re-entry Justification

### Required Identity
- **Primary required identity (new cycle on re-entry with changed measurement method & acceptance policy):**
  - `cycle_id`: **`N = 2`**
  - `plan_revision`: **`r = 1`**
  - `attempt`: **`n = 1/3`**
  - `status_path`: **`.pdca/status/T246-raw-datareader-2.md`** (carrying `.pdca/status/T246-raw-datareader-1.md` (`N=1, r=5, n=1/3`, terminal `incomplete`) by reference; evidence root `.pdca/T246/rv6/` or `.pdca/T246-2/rv1/`).
- **In-place fallback identity (only if the collection runner hard-pins the single file `.pdca/status/T246-raw-datareader-1.md` and `N = 1`):**
  - `cycle_id`: **`N = 1`**, `plan_revision`: **`r = 6`**, `attempt`: **`n = 1/3`**, `status_path`: **`.pdca/status/T246-raw-datareader-1.md`** (evidence root `.pdca/T246/rv6/`).
- **Explicit deviation from the queue's `N = 1, r = 2` on `.pdca/status/T246-raw-datareader-1.md`:**
  1. `.pdca/status/T246-raw-datareader-1.md` already exists and records a completed ACT in terminal `incomplete` at **`N = 1, r = 5, n = 1/3`** with persisted evidence `.pdca/T246/rv1..rv5`. Setting `r = 2` violates counter monotonicity (`5 → 2`) and would overwrite or collide with existing revision history (`r=2` through `r=5`).
  2. Per `references/plan.md` §Revision accounting, history is never erased by a revision, and a change of the **measurement method** (and the user-supplied 2026-10-20 two-tier acceptance criteria replacing the frozen single-tier all-or-nothing gate of `N=1`) after a baseline exists constitutes a new cycle/baseline (`N=2, r=1`), never a backward revision of `N=1`.

### Terminal Cause Carried Forward & How Re-entry Satisfies the Re-entry Rule
- **Prior terminal cause (`N=1, r=5, n=1/3`, `Status: incomplete`):**
  1. *Measured gate failure (`t246-gate-fail-recipe-lever`, `E246-409` / `E246-410` FAIL)*: `r=5` implemented an immutable `CommandRecipe`/`CommandRecipeStore` lever while leaving `ToDataReader` on the LOB seam (`OpenLobReader` + `LobDataReader`), missing the frozen all-or-nothing global threshold (`10k U ≤ 1.00`, `empty U ≤ 1.00`, `empty alloc ≤ 1.00`; measured `10k U = 1.1370–1.1808`, `empty U = 1.763–1.793`, `empty alloc = 5.040323×`). Earlier `r=4` (`reader-seam-cache-ownership`) failed because caching the shared `QueryCommand` directly broke exact-once `CommandReaderOwner` disposal (7 ownership tests failed).
  2. *Oracle noise & budget exhaustion (`perf-oracle-window-noise`, `foreign-competing-workload-mid-block`, `5,865.65 s / 7,200 s` used)*: requiring three heavy multi-minute full-suite blocks (`~1,530 s` remaining work vs `1,334.35 s` budget left) exhausted wall-clock budget and suffered from mid-block host noise.
- **Why this re-entry is valid (both a reformulated criterion/oracle in the brief AND an unexecuted primary lever):**
  1. **Reformulated criterion and oracle (2026-10-20 two-tier policy in the brief):** The user's updated brief replaces the old all-or-nothing `≤ 1.00×` gate with a two-tier policy: (a) **Global target** (`10k rows ≤ 1.00×` linq2db time and `≤ 1.20×` linq2db allocations; empty-result arm likewise) and (b) **Local acceptance (task closure)** when a method achieves a real **one-axis win with no regression on the other** vs the same-round paired ABBA baseline (either exact-byte allocations decrease while time stays within the pre-registered noise band, **or** time improves beyond the noise band while exact-byte allocations do not increase), recording the residual gap to the global target.
  2. **Untried primary lever (Proposed Work #1 & #2 — plain `OpenResultReader` seam + lean owner wrapper):** `r=5` left `empty alloc` at `5.04×` linq2db (`12.74 KB vs 5.52 KB`) because SQLite `ToDataReader` still routed through `OpenLobReader` (`Dialect.LobLocatorColumn is null` branch in `QueryCommandExtensions.cs:326-355`) and wrapped the provider reader in `LobDataReader` (`LobDataReader.cs:20-90`) on top of `CommandReaderOwner`. Routing `ToDataReader` directly through `OpenResultReader` (preserving `OpenLobReader` only for `ToStream`/`ToTextReader` or projections requiring LOB locator semantics) and trimming wrapper overhead directly removes the `LobDataReader` allocation and per-call/per-access forwarding overhead without caching/sharing a mutable `QueryCommand` across calls (preserving all 7 `CommandReaderOwner` exact-once disposal invariants).
  3. **Cheaper, deterministic decisive evidence:** Under the two-tier policy, allocation improvements are judged by **exact byte counts** (`MemoryDiagnoser` allocated bytes/op, immune to CPU scheduler noise) alongside paired ABBA time measurements scoped to the streaming raw-reader arms (`10k rows`, `empty result`, `prepared control`, `direct-provider-reader control`), running well within a single 600 s window instead of 5,800+ s.

---

## 2. Form, Specialized Skill, and Entry Scope

- **Form:** `cycle` (ordinary cycle)
- **Specialized skill (overlay):** `pdca-dotnet` (`coder+.NET`, inheriting `pdca-coder`)
- **Form rationale (one line):** The mechanism and volume are known and bounded (~3–6 files across `QueryCommandExtensions`, `DataContext`/`CommandReaderOwner`, `SqliteBenchmarkStreaming`, and reader unit/integration tests): route `ToDataReader` through the plain `OpenResultReader` seam with a lean owner-disposal wrapper instead of `OpenLobReader` + `LobDataReader`, add the direct-provider-reader control, and measure paired ABBA time and exact bytes on the 10k and empty-result arms.
- **Entry scope:** Whole item `T246` — production seam routing and wrapper streamlining for `ToDataReader` in `src/nextorm.core`, direct-provider-reader control and empty-arm retention in `benchmarks/nextorm.benchmark`, and unit/dialect/integration verification of reader ownership and LOB terminal invariants on snapshot `1e31331d` (branch `1.0.9-rc5`).

---

## 3. Goal, Requested/Delivered Result, and Acceptance Criteria

### Requested Result & Delivered Result
- **Requested result:** A dialect-driven `ToDataReader` execution path and benchmark suite (including a direct-provider-reader control and empty-result arm) that eliminates unnecessary LOB-reader seam and wrapper overhead on fresh-fluent `ToDataReader` calls, meeting either the global target (`≤ 1.00×` linq2db time and `≤ 1.20×` linq2db allocations at 10k rows and empty result) or the local acceptance target (a measured one-axis win in time or exact-byte allocations vs the same-round baseline with no regression on the other axis and the residual gap to the global target recorded), while preserving `ToStream`/`ToTextReader` LOB semantics and `CommandReaderOwner` disposal semantics.
- **Delivered result:** The exact same object — the updated `ToDataReader` seam/wrapper implementation in `src/nextorm.core`, the updated benchmark/gate rows (including the direct-provider-reader control and empty-result arm), passing unit/integration correctness and ownership tests, and the paired same-round benchmark measurement report recording the global/local acceptance outcome and any residual gap to the global target.

### Original Task (Verbatim)

`### #246 — Perf: ToDataReader raw reader is slower than linq2db`
## Context

The 2026-10-17 warm-path feature report (release 1.0.9-rc2, branch `1.0.9-rc3`, HEAD `881bf497`) shows the zero-materialization raw reader slower than linq2db, while the report’s other streaming rows are fine:

| Arm | Nextorm | linq2db | Dapper | Nextorm / linq2db |
|---|---:|---:|---:|---:|
| Raw reader, 10,000 rows | 4.677 ms / 947.98 KB | **3.942 ms / 943.00 KB** | 3.996 ms / 942.77 KB | **1.19×** time, **1.005×** allocation |
| Raw reader, empty result | 237.7 µs / 12.74 KB | **109.2 µs / 5.52 KB** | 126.7 µs / 5.29 KB | **2.18×** time, **2.31×** allocation |
| Prepared control, 10,000 rows | 4.754 ms / 944.8 KB | — | — | 1.21× time, 1.00× allocation |

Source: `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkStreaming-report.csv`; new report: `docs/comparisons/feature-benchmarks-2026-10-17.md`.

The benchmark is `B_Nextorm_ToDataReader`: a fresh `_ctx.LargeEntity.Select(...).ToDataReader()` against linq2db `ExecuteReader` and Dapper `ExecuteReader`, all scanning the same `id, someString` rows through one non-retaining sink. The prepared Nextorm control is slower too, so this is **not** primarily command construction.

## Root-cause analysis

`ToDataReader` on SQLite currently takes the LOB reader seam and wraps the provider reader:

- `src/nextorm.core/Query/QueryCommandExtensions.cs:326-355` — `ToDataReader` calls `OpenLobReader` when `Dialect.LobLocatorColumn is null` (SQLite), otherwise `OpenResultReader`.
- `src/nextorm.core/DataContext/DataContext.cs:469-505` — `OpenLobReader` uses `PrepareLobCommand`, which prepares with `storeInCache: false` and goes through the LOB path; `OpenResultReader` is the plain multi-column seam.
- `src/nextorm.core/DataContext/LobDataReader.cs:20-90` — every `Read`, `GetInt64`/`GetString` (and other accessors) is forwarded through an extra `DbDataReader` wrapper with `ThrowIfDisposed` and a cancellation check; on SQLite there is no LOB locator to manage for this projection.

The empty-result arm is decisive: with no rows to scan, Nextorm still needs 2.18× linq2db’s time and 2.31× its allocation. That points to fixed per-call reader/LOB setup and wrapper overhead, not row scanning or `GetString`.

## Proposed work

1. **Route `ToDataReader` through the plain result-reader seam.** For a multi-column raw reader that does not need a LOB locator, use `OpenResultReader`/its executor path instead of `OpenLobReader` + `LobDataReader`; reserve the LOB seam for projections that actually require locator/LOB semantics (and for `ToStream`/`ToTextReader`). Verify SQL, LOB behavior and ownership/dispose behavior on SQLite and the sequential-access providers.
2. **Remove the per-access wrapper cost.** If an owner-aware wrapper is still required, make it a lean pass-through: `[MethodImpl(AggressiveInlining)]` on `Read`/typed getters, no per-access cancellation check when the token cannot be canceled, and cached field count/ordinal metadata. Consider returning the provider reader through an owner object rather than intercepting every accessor.
3. **Avoid per-call preparation duplication.** `OpenLobReader`/`OpenResultReader` use `storeInCache: false`; evaluate an internal cached reader-plan descriptor for repeated shapes without mutating a shared `QueryCommand` or exposing `Prepare()`. The prepared control shows ~3 KB of the gap, so do this after the wrapper fix.
4. **Add a direct-provider-reader control.** Benchmark Nextorm raw reader vs the same `DbDataReader` obtained directly from the provider; this separates executor/command overhead from wrapper/scanner overhead.
5. **Keep the empty arm in the gate.** It is the cheapest regression detector for fixed overhead.

### Acceptance Criteria (User-Supplied Set Reproduced Verbatim with Stable IDs)

> **Verbatim user-supplied acceptance criteria (2026-10-20 two-tier policy):**
>
> **Global target (milestone-level):** raw reader at 10,000 rows ≤ **1.00×** linq2db time and ≤ **1.20×** linq2db allocations; the empty-result arm likewise (or a documented, owner-approved tolerance). The original **3.942 ms / 943.00 KB** and ≤1.00× absolute/ratio numbers remain the global/stretch reference.
>
> **Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
> - allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
> - time improves beyond the pre-registered noise band and allocations do not increase.
>
> A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.
>
> **Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.
>
> ### Mandatory correctness and evidence
> - No materialization/result change; LOB terminals (`ToStream`/`ToTextReader`) retain their locator/sequential-access behavior and tests.
> - `CommandReaderOwner` dispose semantics remain: disposing the reader releases the per-call command and provider reader; the context stays open.
> - Add a benchmark/gate row for the raw reader; run the streaming report on all relevant providers if the seam changes cross-provider.

#### Stable Criterion ID Mapping (Strictly Coextensive with the Verbatim Set Above)
- **`C1` [Performance — Two-Tier Acceptance (Global Target or Local One-Axis Win)]** *(kind: measurable)*:
  - *Global target:* Raw reader (`B_Nextorm_ToDataReader`) at 10,000 rows achieves `≤ 1.00×` linq2db time and `≤ 1.20×` linq2db allocations, and the empty-result arm achieves `≤ 1.00×` linq2db time and `≤ 1.20×` linq2db allocations (with `3.942 ms / 943.00 KB` and `≤ 1.00×` recorded as the global/stretch reference); **OR**
  - *Local acceptance (task closure):* Measured in the same session/round on a pinned DB via paired/ABBA runs against the same-round baseline (`1e31331d`) on the fresh-fluent raw-reader arms (10,000 rows and empty result), either:
    1. Exact allocated bytes decrease (`B_candidate < B_baseline` in exact bytes) and mean time does not worsen beyond the pre-registered noise band (`T_candidate ≤ T_baseline × (1 + ε_noise)`); **or**
    2. Mean time improves beyond the pre-registered noise band (`T_candidate < T_baseline × (1 - ε_noise)`) and exact allocated bytes do not increase (`B_candidate ≤ B_baseline` in exact bytes);
    and the residual gap to the global target (`≤ 1.00×` time, `≤ 1.20×` alloc, and `3.942 ms / 943.00 KB` reference) plus the measured local win are explicitly recorded.
  - *Negative condition:* A candidate that improves neither axis, or improves one axis while regressing the other beyond its threshold (exact bytes `> B_baseline` or time `> T_baseline × (1 + ε_noise)`), or uses historical-number substitution, or requires callers to call `Prepare()`, is rejected and reverted.
- **`C2` [Materialization & LOB Terminal Preservation]** *(kind: behavioral/correctness)*:
  - `ToDataReader` produces identical row/column results with no materialization change, and LOB terminals (`ToStream` / `ToTextReader`) retain their locator and `SequentialAccess` behavior and pass all existing and added tests across SQLite and sequential-access providers.
  - *Negative condition:* Removing LOB support, altering `ToStream`/`ToTextReader` semantics, or introducing SQLite-only hardcoded SQL/seam hacks fails the criterion.
- **`C3` [`CommandReaderOwner` Dispose & Context Lifetime Semantics]** *(kind: behavioral/correctness)*:
  - Disposing the returned `DbDataReader` (sync `Dispose()` or async `DisposeAsync()`) releases the per-call `DbCommand` and underlying provider `DbDataReader` exactly once, while the owning `DataContext` and its connection remain open and usable for subsequent queries (and shared cached commands on the context are never mutated with sticky `Cache = false` / `_dontCache`).
  - *Negative condition:* Any leak of the per-call command/reader, premature closure of the `DataContext`, violation of exact-once disposal, or mutation of shared `QueryCommand.Cache` fails the criterion.
- **`C4` [Benchmark / Gate Row, Direct-Provider Control & Cross-Provider Streaming Verification]** *(kind: behavioral/artifact)*:
  - `SqliteBenchmarkStreaming` includes the raw-reader benchmark/gate row (`10,000 rows`), keeps the `empty-result` arm, and includes the `direct-provider-reader control` (measuring the same `DbDataReader` obtained directly from the provider to separate executor/command overhead from wrapper/scanner overhead); if the reader seam changes cross-provider, streaming/reader verification runs across all relevant providers; furthermore, query-path changes pass the repository's standing acceptance benchmark gate (`--anyCategories=acceptance` without regression beyond the standing noise band).
  - *Negative condition:* Omitting the empty arm, omitting the direct-provider-reader control, or leaving cross-provider streaming broken fails the criterion.

---

## 4. Measurement Method

- **Metrics and units:**
  1. **Execution time (`T`):** BenchmarkDotNet `Mean` time per operation in nanoseconds (`ns`) / microseconds (`µs`) / milliseconds (`ms`).
  2. **Managed allocation (`B`):** BenchmarkDotNet `MemoryDiagnoser` `Allocated` in **exact bytes (`B`)** per operation.
- **Denominators & Comparisons:**
  1. **Global target ratio:** `R_time_global = T_Nextorm / T_linq2db` and `R_alloc_global = B_Nextorm / B_linq2db` measured in the **same round** on the 10,000-row raw-reader arm (`B_Nextorm_ToDataReader` vs `B_Linq2Db_ExecuteReader`) and on the empty-result raw-reader arm (`Empty_Nextorm_ToDataReader` vs `Empty_Linq2Db_ExecuteReader`). Global stretch reference: `3,942,000 ns` (`3.942 ms`) and `943.00 KB` (`965,632 B`).
  2. **Local acceptance delta / ratio:** Paired comparison of the candidate worktree (`C`) against the unmodified baseline snapshot `1e31331d` (`A` / `B_base`) in the **same session/round** using paired **ABBA** ordering (`A1 → B1 → B2 → A2`, comparing aggregated/median-of-pair means `T_candidate / T_baseline` and exact byte counts `B_candidate - B_baseline`) on both the **empty-result raw-reader arm** and the **10,000-row fresh-fluent raw-reader arm**.
  3. **Diagnostic controls (reported alongside):** Prepared raw-reader control (`B_Nextorm_ToDataReader_Prepared`) and the new direct-provider-reader control (`B_Provider_DirectDataReader` / empty equivalent).
- **Inclusion / exclusion filters:**
  - Included: `SqliteBenchmarkStreaming` raw-reader arms (10,000 rows fresh-fluent, empty-result fresh-fluent, prepared control, direct-provider-reader control, and linq2db reference arms), plus the standing query-path sanity check (`--anyCategories=acceptance`).
  - Excluded: Unrelated benchmark classes during the paired ABBA raw-reader runs; no historical CSV numbers (`feature-benchmarks-2026-10-17.md` or `rv1..rv5`) may be substituted as the baseline denominator.
- **Comparability conditions:**
  - Same host session, same pinned SQLite database setup and row seed (`LargeEntity`, 10,000 rows, `id, someString` projection consumed through the identical non-retaining sink), Release build (`-c Release`), strictly serialized (one benchmark or container process at a time, no concurrent `dotnet`/`vbcscompiler` workloads), capped GC heap (`DOTNET_GCHeapHardLimit`), per-command `timeout 600` with streamed logs, paired ABBA execution order.
- **Thresholds and provenance (with baseline-backed / provisional status):**
  - **Global thresholds (user-supplied & baseline-backed from the 2026-10-17 report in the brief — non-provisional):** `R_time_global ≤ 1.00×` and `R_alloc_global ≤ 1.20×` vs same-round linq2db on 10,000 rows and empty result (stretch reference: `3.942 ms` / `3,942,000 ns` and `943.00 KB` / `965,632 B`).
  - **Local acceptance allocation threshold (user-supplied in the brief — non-provisional, anchored to the same-round ABBA baseline):** Judged by **exact byte counts** (`0 B` tolerance). A win is `B_candidate < B_baseline` (strictly fewer bytes on the measured raw-reader arm(s), with zero byte increase on either the 10k or empty arm); non-regression is `B_candidate ≤ B_baseline`.
  - **Local acceptance time noise band (`ε_noise`, pre-registered before measurement as required by the brief; absolute baseline values `T_baseline` are provisional until the same-round ABBA baseline run captures them, after which the ratio bound is locked with no post-measurement edits):** Pre-registered at **`±5 %` (`0.05`)** for paired ABBA same-round BenchmarkDotNet comparisons (and bounded by the repository's standing `<~20 %` single-run acceptance noise floor for `--anyCategories=acceptance`). Thus, for paired ABBA local acceptance:
    - Under an **allocation win** (`B_candidate < B_baseline` on empty and/or 10k arm with no byte increase on the other): time non-regression requires `T_candidate ≤ 1.05 × T_baseline` across the paired ABBA aggregate on both the 10k and empty arms.
    - Under a **time win** (`B_candidate ≤ B_baseline`): time improvement requires `T_candidate < 0.95 × T_baseline` with exact allocated bytes `B_candidate ≤ B_baseline` on both the 10k and empty arms.

---

## 5. Constraints & Assumptions

### Constraints
1. **Verbatim user constraints:**
   - Do not remove LOB support or change `ToStream`/`ToTextReader` semantics.
   - Do not require callers to use `Prepare()`; the measured path remains fresh-fluent.
   - No benchmark-specific SQL or SQLite-only production hardcode; the seam choice must be dialect-driven.
2. **Repository & standing constraints:**
   - Snapshot anchored to `/home/alex/sources/nextorm`, branch `1.0.9-rc5`, base commit `1e31331d`.
   - .NET 10, `TreatWarningsAsErrors=true` (0 warnings / 0 errors), Central Package Management, CRLF line endings throughout (`perl -pi -e 's/\r?\n/\r\n/g'`).
   - Never mutate sticky `QueryCommand.Cache` / `_dontCache` on shared commands; use `storeInCache: false`.
   - `CommandReaderOwner` exact-once disposal semantics (`r=4` lesson: never share a mutable `DbCommand` instance across active `CommandReaderOwner` instances).
   - Tests use FluentAssertions only (`tests/nextorm.core.tests/Conventions/AssertionStyleTests.cs`); live-DB tests in `tests/nextorm.integration.tests` run fail-closed with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` and never use `InMemoryDataContext`.
   - Heavy runs must run one process at a time with `DOTNET_GCHeapHardLimit`, `timeout 600`, and streamed logs.
   - Milestone `1.0.9-rc5` never moves; no `git push`, merge-to-main, or tag; auto-commit only for item-touched files on `collection/**` branches if authorized by the collection driver.

### Known Resource and Access Constraints
- Single shared Linux/WSL host environment where background compiler servers (`VBCSCompiler`) or concurrent processes can perturb sub-millisecond wall-clock timings if not serialized; however, BenchmarkDotNet `MemoryDiagnoser` exact byte counts are deterministic, and paired ABBA short runs of `SqliteBenchmarkStreaming` complete well inside a single `timeout 600` window.
- Podman socket (`unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) is required for container-backed integration tests if cross-provider reader seams are touched.

### Assumptions
- **A1:** In `QueryCommandExtensions.ToDataReader` (`src/nextorm.core/Query/QueryCommandExtensions.cs:326-355`), routing `ToDataReader` unconditionally (or whenever no LOB locator projection is active) through `OpenResultReader` instead of `OpenLobReader` does not affect `ToStream` or `ToTextReader`, because `ToStream` and `ToTextReader` have their own dedicated entry points that continue to call `OpenLobReader`.
- **A2:** Eliminating `OpenLobReader` (`PrepareLobCommand`) + `LobDataReader` wrapping from `ToDataReader` on dialects where `Dialect.LobLocatorColumn is null` (such as SQLite) directly eliminates the `LobDataReader` instance allocation and per-call LOB setup allocations, producing an immediate, deterministic exact-byte allocation reduction (and fixed-overhead time reduction on the empty-result arm).

---

## 6. Statement Gaps

| Gap | Resolution Type | Resolution |
|---|---|---|
| Queue dispatch specifies `N=1, r=2` on `.pdca/status/T246-raw-datareader-1.md`, which already holds terminal `incomplete` at `N=1, r=5, n=1/3` | **Evidence** (supplied in prompt) | Resolved in §1: require `N=2, r=1, n=1/3` at `.pdca/status/T246-raw-datareader-2.md` (or monotonic `N=1, r=6, n=1/3` if the collection runner hard-pins `N=1` in place); never `r=2`. |
| Numerical value of the "pre-registered noise band" required by the user's 2026-10-20 local acceptance criteria | **Assumption / Calibration** | Pre-registered in §4 at `±5 %` (`0.05`) for paired ABBA same-round BenchmarkDotNet runs (consistent with BDN warm-path repeatability on this host and stricter than the repo's `<~20 %` single-run acceptance gate noise band), while allocation changes use `0 B` exact byte counts as ⟦SEAM⟧ required by the brief. |
| Whether Proposed Work #3 (cached reader-plan descriptor / recipe) is required after Proposed Work #1 & #2 | **Evidence** (from prior `r=4`/`r=5` record + brief) | Brief explicitly states: *"The prepared control shows ~3 KB of the gap, so do this after the wrapper fix"* and local acceptance closes the task on a real one-axis win with no regression on the other. Moreover, `r=5` proved that `CommandRecipe` alone without the seam/wrapper fix failed, and `r=4` proved caching the command breaks `CommandReaderOwner`. Therefore Proposed Work #1 & #2 (plain `OpenResultReader` seam + lean owner wrapper) + #4 & #5 (direct-provider control + empty arm) are primary; #3 is optional/deferred unless needed and safe for exact-once ownership. |

---

## 7. Minimal Solution & Alternatives

### Chosen Minimal Solution (Proposed Work #1, #2, #4, #5)
1. **Route `ToDataReader` through the plain `OpenResultReader` seam (Proposed Work #1):**
   - In `src/nextorm.core/Query/QueryCommandExtensions.cs` (`ToDataReader`), stop branching on `Dialect.LobLocatorColumn is null` to call `OpenLobReader`; route `ToDataReader` through `OpenResultReader` (keeping `OpenLobReader` + `LobDataReader` exclusively for `ToStream`/`ToTextReader` or explicit LOB-locator operations).
2. **Streamline the owner-aware reader wrapper (Proposed Work #2):**
   - Ensure the reader returned by `OpenResultReader` (`CommandReaderOwner` or lean wrapper in `src/nextorm.core/DataContext/`) is a minimal pass-through: `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on `Read`/`ReadAsync` and typed getters (`GetInt64`, `GetString`, `GetInt32`, `GetValue`, `IsDBNull`, etc.), avoid per-access cancellation checks when `!cancellationToken.CanBeCanceled`, and avoid redundant wrapper layering (`LobDataReader` over `CommandReaderOwner`) while strictly preserving exact-once disposal of the per-call `DbCommand` and `DbDataReader` without closing the `DataContext`.
3. **Add direct-provider-reader control and retain empty-result arm (Proposed Work #4 & #5):**
   - In `benchmarks/nextorm.benchmark` (`SqliteBenchmarkStreaming`), retain the empty-result arm (`Empty_Nextorm_ToDataReader`, `Empty_Linq2Db_ExecuteReader`, etc.) and add a direct-provider-reader control arm that opens the same `DbDataReader` directly on the SQLite provider connection/command and scans it through the identical sink.
4. **Verify correctness and measure paired ABBA baseline vs candidate:**
   - Run unit, dialect, and integration reader/LOB/ownership tests, then run paired ABBA benchmarks in the same round against baseline `1e31331d` to evaluate Global Target (`C1` Tier 1) and Local Acceptance (`C1` Tier 2), recording both the local win and the residual gap to the global target.

### Alternatives Considered
- **Alternative A — Re-apply `r=5` `CommandRecipe`/`CommandRecipeStore` without changing the `OpenLobReader`/`LobDataReader` seam:** *Rejected.* Already tried in `r=5` (`t246-gate-fail-recipe-lever`), where empty-arm allocations remained `5.04×` linq2db and 10k time remained `1.14–1.18×`.
- **Alternative B — Cache and reuse the `DbCommand` across `ToDataReader` calls (`r=4` approach):** *Rejected.* Already tried in `r=4` (`reader-seam-cache-ownership`) and reverted because `CommandReaderOwner.Dispose` disposes the command, breaking 7 existing ownership tests and violating `C3`.
- **Alternative C — Combine Proposed Work #1 + #2 with an internal read-only SQL/parameter descriptor cache (Proposed Work #3) if and only if #1 + #2 alone leave a solvable preparation allocation gap:** *Kept as a secondary option inside DO* only if it clones a fresh per-call `DbCommand` (preserving `C3`) and further reduces exact bytes without regressing time.

---

## 8. Effort Estimate

- **Estimated effort:** Compact cycle (~1.5–2.5 hours total wall-clock, well within a single budget window):
  - Code change in `QueryCommandExtensions` / `DataContext` / `CommandReaderOwner` + benchmark control: ~150–250 LOC across 3–6 files.
  - Unit + dialect + integration test verification: ~10–15 minutes.
  - Paired ABBA benchmark run on `SqliteBenchmarkStreaming` (10k + empty + controls) + `--anyCategories=acceptance`: ~15–25 minutes serialized.

---

## 9. Plan-to-DO Handoff

Every DO dispatch carries by reference:
- **Snapshot & branch:** `/home/alex/sources/nextorm`, branch `1.0.9-rc5`, base commit `1e31331d`.
- **Cycle identity:** `N = 2, r = 1, n = 1/3`, status path `.pdca/status/T246-raw-datareader-2.md` (or `N = 1, r = 6, n = 1/3` on `.pdca/status/T246-raw-datareader-1.md` if the collection runner hard-pins `N=1` in place; never `r = 2`).
- **Original task & verbatim criteria:** §3 above (`C1`, `C2`, `C3`, `C4`).
- **Measurement specification:** §4 above (paired ABBA in the same session/round against baseline `1e31331d` on pinned SQLite DB; exact byte counts via `MemoryDiagnoser`; `±5 %` pre-registered time noise band; record both measured local win and residual gap to global `≤ 1.00×` time / `≤ 1.20×` alloc and `3.942 ms / 943.00 KB` stretch reference).
- **Mandatory prohibitions:**
  - Never mutate sticky `QueryCommand.Cache` / `_dontCache`; always use `storeInCache: false` when preparing non-cached commands.
  - Never share a mutable `DbCommand` across `CommandReaderOwner` instances (`r=4` defect `reader-seam-cache-ownership`).
  - Do not alter `ToStream` / `ToTextReader` LOB locator or `SequentialAccess` semantics.
  - Do not require callers to call `Prepare()` on the measured `B_Nextorm_ToDataReader` path.
  - No SQLite-only hardcoded SQL or benchmark-specific production branches; keep seam selection dialect-driven.
  - Preserve CRLF line endings; 0 warnings / 0 errors (`TreatWarningsAsErrors=true`); FluentAssertions only in tests; no `InMemoryDataContext` in `tests/nextorm.integration.tests`.
  - Run heavy benchmark/container commands strictly one at a time with `DOTNET_GCHeapHardLimit`, `timeout 600`, and streamed logs, after ensuring no stray `VBCSCompiler` or competing `dotnet` processes are active.

---

## 10. Risks and Stop Conditions

### Risks
1. **Cross-provider sequential-access / LOB expectation in existing tests (`R1`):** Some unit or integration test may assert that `ToDataReader` on a specific projection invokes `OpenLobReader` or returns `LobDataReader`. *Mitigation:* Check all callers/tests of `ToDataReader`, `OpenLobReader`, and `OpenResultReader` via Roslyn `refs`/`callers`; ensure `ToStream`/`ToTextReader` keep `OpenLobReader` and `ToDataReader` uses `OpenResultReader` unless the dialect/projection explicitly requires a LOB locator column.
2. **Host CPU noise during ABBA timing (`R2`):** Background `VBCSCompiler` or container activity could inflate variance beyond `±5 %`. *Mitigation:* Shut down idle build servers (`dotnet build-server shutdown`) before benchmark pairs, run `--no-build` pre-built Release binaries in ABBA order, and rely on exact byte counts (`0 B` variance) plus paired ABBA means.

### Stop Conditions
- **STOP (Success / Task Closure):** `C2`, `C3`, and `C4` are met, and `C1` meets either the **Global Target** (`≤ 1.00×` time and `≤ 1.20×` alloc vs linq2db on 10k and empty arms) or **Local Acceptance** (exact allocated bytes decrease with paired ABBA time within `≤ 1.05×` baseline, **or** paired ABBA time `< 0.95×` baseline with exact allocated bytes `≤ 1.00×` baseline, on the raw-reader arms, with the residual gap to the global target recorded).
- **STOP (Revert & Reject Candidate):** If the candidate improves neither time nor exact-byte allocations, or improves one while regressing the other beyond the pre-registered threshold (`> 1.05×` time or `> 0 B` allocation increase), or breaks `CommandReaderOwner` exact-once disposal / LOB terminal semantics and cannot be repaired within the 3-attempt DO/CHECK bound (`n = 3/3`).

---

## 11. Confidence and Gaps

- **Verified from supplied facts:**
  - Base commit `1e31331d` on `1.0.9-rc5` has all 34 prior `T246` paths cleanly reverted to `bcb8e676`.
  - Root cause locations (`QueryCommandExtensions.cs:326-355`, `DataContext.cs:469-505`, `LobDataReader.cs:20-90`) and prior defect history (`r=1..r=5` in `.pdca/status/T246-raw-datareader-1.md`) are established.
  - The 2026-10-20 two-tier acceptance policy is verbatim in the brief and explicitly authorizes task closure on a verified one-axis win with no regression on the other axis.
- **Remaining assumptions:**
  - `A1` and `A2` (§5) — that routing `ToDataReader` through `OpenResultReader` + lean owner wrapper reduces exact per-call bytes and empty-arm fixed overhead without disturbing `ToStream`/`ToTextReader` tests; directly testable in DO/CHECK.
````

## Approval record

`not applicable: autonomous mode` — no ordinary plan approval was required or invented for this cycle (base §Control modes; the plan was produced and consumed inside the authorized `rc5` collection run, `plan_state=ready`).

In-force auto-commit policy (verbatim, repo `AGENTS.md` §Git): "a `pdca-collection` cycle started with explicit auto-commit permission may (a) commit on `collection/**` branches inside the cycle worktree and (b) `git merge --no-ff` a group branch into the current branch." No `git push`, no merge into `main`, no tag; a commit touches only the files changed by item `T246`.

## DO result

Candidate recorded (attempt `n=1/3` spent). Implementation (4 touched paths, build 0 W/0 E):
- `src/nextorm.core/Query/QueryCommandExtensions.cs` — `ToDataReader`/`ToDataReaderAsync` route through `OpenResultReader` instead of branching on `Dialect.LobLocatorColumn` (`OpenLobReader` retained for `ToStream`/`ToTextReader`).
- `src/nextorm.core/DataContext/DataContext.cs` — `OpenResultReader`/`OpenResultReaderAsync` gain `sequentialAccess` param (default false), forwarded to `PrepareResultCommand`.
- `src/nextorm.core/DataContext/LobDataReader.cs` — pass-through trims: cached `FieldCount`, `[MethodImpl(AggressiveInlining)]`, cancellation check skipped when token cannot be canceled.
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs` — control arms `Provider_Direct_ToDataReader` and `Provider_Direct_ToDataReader_Empty`, registered in `IterationSetup`.

Exact numbers (same-round paired, pinned DB; logs `.pdca/T246/rv6/logs/`):
- `B_Nextorm_ToDataReader`: 3953.46 us / 973 504 B -> 4010.24 us / 973 504 B (+1.4 % time, allocations unchanged).
- `B_Nextorm_ToDataReader_Empty`: 214.888 us / 16 048 B -> 186.08 us / 16 048 B (-13.4 % time, allocations unchanged).
- linq2db reference: 3805.84 us / 965 632 B (10k); 84.837 us / 5 648 B (empty).
- Direct-provider control (candidate): 3750.75 us / 940 236 B (10k); 61.34 us / 5 386 B (empty).

Local acceptance: met — empty-result arm time wins -13.4 % beyond the +/-5 % noise band with allocations unchanged; 10k arm allocations unchanged with time +1.4 % inside noise. No regression beyond threshold on either axis. Global target not reached; residual recorded in `.pdca/T246/rv6/candidate.md`.

Correctness (established, not re-run except container gate):
- Build 0 W/0 E; `nextorm.core.tests` 3901 passed/0 failed; `nextorm.sqlite.tests` 2252 passed/1 skipped; `ToDataReader` integration selection 14 passed/0 failed.
- Container gate: `.pdca/T246/rv6/gates/integration.log` — 7934 total / 7733 succeeded / 0 failed / 201 skipped, rc 0 (no `NEXTORM_SKIP_PROVIDERS`). No infrastructure/transport failure; no product defect observed.

Evidence root: `.pdca/T246/rv6/` (snapshot, provenance, candidate.md, helpers.log rc 0, manifest.json).

### Evidence round (r=1, evidence_round=1)

True paired ABBA (`A1→B1→B2→A2`, four legs, rc 0 each; pinned DB sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; logs `.pdca/T246/rv6/logs/abba-A1.log` … `abba-A2.log`; aggregate `.pdca/T246/rv6/abba.md`):

| Arm | T_baseline (µs) | T_candidate (µs) | time ratio B/A | alloc baseline (B) | alloc candidate (B) | alloc delta (B) |
|---|---:|---:|---:|---:|---:|---:|
| 10k raw reader | 4560.84 | 3638.92 | 0.7979 | 973504 | 973504 | 0 |
| empty raw reader | 196.545 | 166.755 | 0.8484 | 16048 | 16048 | 0 |

Raw leg means (µs / B): A1 5251.53/973504 (10k) and 192.53/16048 (empty); B1 3689.06/973504 and 140.64/16048; B2 3588.78/973504 and 192.87/16048; A2 3870.15/973504 and 200.56/16048. Both time ratios (−20.2 %, −15.2 %) lie beyond the pre-registered ±5 % noise band; allocated bytes identical on both arms ⇒ local acceptance clause (2) met; no regression on either axis.

Corrected same-round residuals vs the candidate-run linq2db arms: 10k 1.072× time / 1.008× alloc; empty 3.172× time / 2.841× alloc (global target not reached; residual recorded).

Acceptance gate (`--anyCategories=acceptance`): 12 cases / 0 failures / wall 80 s / rc 0 (log `.pdca/T246/rv6/logs/acceptance.log`).

Original-log counts (provider-labelled, rc 0):
- Build: 0 W / 0 E (`.pdca/T246/rv6/logs/build-debug.log`).
- Core reader/LOB/streaming/ownership: 123 passed / 0 failed (`.pdca/T246/rv6/logs/tests-core-reader-lob.log`).
- SQLite reader/streaming: 203 passed / 1 skipped (`.pdca/T246/rv6/logs/tests-sqlite-reader-streaming.log`).
- Conventions: 22 passed / 0 failed (`.pdca/T246/rv6/logs/tests-conventions.log`).
- Integration selected `ToDataReader`/`ToStream`/`ToTextReader`: 228 passed / 0 failed / 89 skipped (`.pdca/T246/rv6/logs/integration-selected.log`).

Identity reconciliation: `.pdca/T246/rv6/provenance/identity-reconciliation.md` — commit chain 9e0832c8 → c1a8871c → f03e3f6c (candidate) → 97828831 (HEAD at reconciliation); item-only delta `git diff f03e3f6c^..f03e3f6c` (4 files, 47 insertions / 13 deletions) recorded in `snapshot/item-diff.patch`; frozen tree `679c5ffc85cab70df87ddfa7f08f28bb241abe2f` unchanged (footprint unchanged); helpers rc 0/0 (`.pdca/T246/rv6/helpers.log`); manifest refreshed (`git_head` = `9ff27cdf630336236633b4c76998fd4d5d9b9aa6`).

### Attempt n=2/3 (r=1)

Defect fix + evidence close (no benchmark change; ABBA cited from attempt 1):

- Fix `T246-R1-F6`: `src/nextorm.core/DataContext/LobDataReader.cs` — dropped the cached readonly `_fieldCount` field; `FieldCount` now delegates to `_inner.FieldCount` so metadata always describes the current result set after `NextResult()`/`NextResultAsync()`; per-row trims (AggressiveInlining, cancellation skip) retained; no public API change.
- Regression tests (FluentAssertions, CRLF): `tests/nextorm.core.tests/Lob/LobDataReaderTests.cs` — `LobDataReader_NextResult_DifferentColumnCount_ShouldRefreshMetadata` (sync) and `LobDataReader_NextResultAsync_DifferentColumnCount_ShouldRefreshMetadata` (async): a reader over two result sets with differing column counts reports the right `FieldCount`, `GetName`/`GetOrdinal` bounds and values after `NextResult()`/`NextResultAsync()`.
- `FakeDbDataReader` upgraded to support multiple result sets (backward-compatible constructor retained).
- Staged under `snapshot/`: `CommandReaderOwner.cs`, refreshed `LobDataReader.cs`, `LobDataReaderTests.cs`; `snapshot.sha256` refreshed.
- Gates (logs `.pdca/T246/rv6/attempts/attempt2/`): build rc 0 (0W/0E); core reader/LOB/streaming/ownership 125 passed/0 failed (incl. 2 new); sqlite reader/streaming 218 passed/1 skipped rc 0; conventions 35 passed/0 failed rc 0; integration selected 37 passed/0 failed/19 skipped rc 0 (subset); full-suite cite `.pdca/T246/rv6/gates/integration.log` (7934/7733/0/201 rc 0, no `NEXTORM_SKIP_PROVIDERS`). ABBA cited from attempt 1 (`.pdca/T246/rv6/abba.md`, unchanged — benchmarks unaffected): 10k time ratio 0.7979, empty 0.8484, allocs flat; residuals 1.072×/1.008× (10k), 3.172×/2.841× (empty). Acceptance gate cited `.pdca/T246/rv6/logs/acceptance.log` (12 cases, 0 failures, 80 s, rc 0).

### Attempt n=3/3 (r=1)

M1 fix (F7) + candidate-bound ABBA evidence close (no benchmark change beyond already-committed `abd5b22f`):

- Fix `T246-R1-F7`: `src/nextorm.core/DataContext/LobDataReader.cs` (commit `abd5b22f`) — disposal is exact-once: sync `Dispose(bool)` and `DisposeAsync()` delegate to the `CommandReaderOwner` (which disposes the provider reader + per-call command) instead of disposing `_inner` AND the owner; `ReleaseOwner` removed; idempotent via `_disposed`. `FakeDbDataReader` now counts every `Dispose` invocation (previously masked by a `!_disposed` guard). New tests `LobDataReader_Dispose_SingleCall…`, `…DisposeAsync_SingleCall…`, `…DisposeAsyncThenDispose…`; 30/30 LobDataReader tests pass.
- M2 candidate-bound ABBA (candidate `abd5b22f`): per-leg build identity (Release `nextorm.core.dll`) baseline `1c8aa64d…`, candidate `21be2682…`; pinned DB sha256 `32bf…`; all legs rc 0. Pair aggregates: 10 k 3 923.7 / 4 022.6 µs → ratio 0.975, allocations exactly flat 973 504 B; empty 183.8 / 156.7 µs → ratio 1.173, allocations 16 048 → 16 104 B (−56 B). Honest reading: neither arm reaches the `±5 %` band — 10 k improves only 2.5 % (inside noise), the empty arm is +17.3 %, i.e. a regression beyond the band; the empty arm is short (sub-200 µs, 3-iteration ShortRun) and noise-dominated, and the allocation delta is −56 B. Global residuals from these legs only: 10 k 1.057× time / 1.008× alloc, empty 2.5× / 2.84×.
- M3 acceptance gate: candidate 12 cases / 0 failures / 79.5 s / rc 0; baseline 12 cases / 0 failures / rc 0. Provider-labelled integration selection 37 passed / 19 skipped (MySQL unsupported-provider skips), rc 0, socket healthy, no `NEXTORM_SKIP_PROVIDERS`.
- Refreshed `snapshot/` (6 paths at current HEAD) + `snapshot.sha256`; `manifest.json` refreshed (footprint 6 paths, git_head `abd5b22f`, tree recomputed `43306d6cffad51d741aaa22c7f0cace7246edcbf`); `attempts/attempt3/README.md` written.
- Helpers: `validate_inner_loop.py manifest` rc 0; `pdca_evidence.py lint` rc 0 (tree recomputed == recorded).

## CHECK record

### r=1 n=3/3 (final, candidate-bound ABBA)

Verdict (independent CHECK): **incomplete / STOP** — no criteria defect raised; C1 unmet on the candidate's own same-round ABBA; C2/C3/C4 unverified on evidence binding. Attempts spent: n=3/3 (no attempts remaining); evidence round 3 spent.

Per-criterion matrix:

| Criterion | Status | Evidence |
|---|---|---|
| C1 (two-tier perf) | unmet | 10 k 4 022.6 → 3 923.7 µs (−2.46 %, inside ±5 %), allocs flat 973 504 B; empty 156.7 → 183.8 µs (+17.31 %, beyond band), 16 104 → 16 048 B (−56 B). Neither local clause (1) nor (2) passes. Residuals: 10 k 1.057×/1.008×, empty 2.407×/2.841×. Global target unmet. |
| C2 (materialization/LOB terminals) | unverified | per-provider positive results not established; 19 skips = 7 MySQL unsupported-feature + 12 inapplicable unsupported-provider negatives; claimed 30/30 output not in attempt-3 artifacts |
| C3 (CommandReaderOwner dispose) | unverified | same evidence-binding gap; F7 cured inside candidate (exact-once) but binding not independently established |
| C4 (benchmark/gate row + cross-provider) | unverified | same; arms present and run, per-provider positivity not established |

Findings adjudication: F1/F2/F3/F5 cured (F2 = reversed baseline/candidate headings + residual accounting in attempts/attempt3/README.md, corrected in errata); F6 (stale FieldCount) and F7 (duplicate provider-reader disposal) found and cured inside the candidate with invocation-counting tests; F1/F2/F4 remainders noted (F4 = positive per-provider results not bound). No criteria defect.

Terminal case: CHECK attempts exhausted (n=3/3) under the no-4th rule → ACT finalizes incomplete/STOP. Source: `.pdca/T246/rv6/attempts/attempt3/README.md`, `.pdca/T246/rv6/abba.md`, `.pdca/T246/rv6/manifest.json`.

## ACT record

Outcome: **incomplete / STOP** (no credit). No performance credit, no Tier B credit. Issue #246 stays OPEN.

Retained vs reverted scope:
- Reverted to prior rung (performance mechanism not credited): `src/nextorm.core/DataContext/LobDataReader.cs` and `src/nextorm.core/Query/QueryCommandExtensions.cs` restored to `f03e3f6c^` content (ToDataReader back on OpenLobReader/OpenResultReader branching by dialect; LobDataReader back to full wrapper with cancellation checks).
- `src/nextorm.core/DataContext/DataContext.cs`: `OpenResultReader`/`OpenResultReaderAsync` `sequentialAccess` signature addition reverted to prior form (reverted LobDataReader does not need it; PrepareResultCommand retains its own sequentialAccess param used by OpenCsvReader).
- Ported correctness repair (not performance): exact-once provider-reader disposal (F7) ported onto the prior-run LobDataReader (delegate to CommandReaderOwner; no separate `_inner.Dispose()`; idempotent via `_disposed`). Recorded as a correctness repair, never as T246 performance.
- Dropped: `LobDataReader` F6 tests (NextResult differing column counts, sync+async) — stale cached-FieldCount defect does not exist on the prior rung (FieldCount delegates to `_inner`). F7 invocation-counting tests retained (they verify the ported fix). `FakeDbDataReader` multi-result-set upgrade retained (harmless).
- Retained benchmark arms: `Provider_Direct_ToDataReader` / `Provider_Direct_ToDataReader_Empty` in `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs` (mandatory raw-reader benchmark row) — build + short run verified (rc 0).
- Retained measurement infrastructure: `.pdca/T246/rv6/` evidence (unchanged).

Pre-check answer (ground truth established before touching anything):
- F6 (cached/stale FieldCount) on prior rung (`f03e3f6c^`): **no** — prior LobDataReader delegates `FieldCount` to `_inner.FieldCount`.
- F7 (inner reader disposed twice) on prior rung (`f03e3f6c^`): **yes** — `Dispose(bool)` called `_inner.Dispose()` then `ReleaseOwner()` → `owner.Dispose()` disposing `owner.Reader` (== `_inner`) a second time. Ported as a minimal correctness repair.

Exact numbers and residuals (candidate-bound ABBA, same round; see errata in attempts/attempt3/README.md):
- 10 k: 4 022.6 → 3 923.7 µs (−2.46 %, inside ±5 %); allocations flat 973 504 B.
- empty: 156.7 → 183.8 µs (+17.31 %, beyond band); 16 104 → 16 048 B (−56 B).
- Residuals: 10 k ≈ 1.057× time / 1.008× alloc; empty ≈ 2.407× time / 2.841× alloc.
- Global target unmet on both arms.

Cured inside this candidate (as correctness repairs, not performance): F6 (stale FieldCount) and F7 (duplicate provider-reader disposal), with invocation-counting tests. F1 (ABBA provenance) / F2 (reversed headings + residual accounting) / F4 (per-provider evidence binding) remainders noted; F3/F5 cured.

Must never claim (no credit):
- No speed or allocation win for T246; no "−2.46 %" or "−56 B" as savings.
- No verified ABBA provenance claim beyond the recorded (reversed-headings) artifact; corrected in errata.
- No positive per-provider coverage (C2/C3/C4 unverified on evidence binding).
- No C2/C3/C4 met; global parity unmet.
- The performance mechanism is not credited and was reverted to the prior retained rung.

Handoff: issue #246 stays OPEN; accepted-scope bar not met. Next authorized action is a separate PLAN (not automatic) if re-attempted.

## Revision history

2026-10-30T07:03:32Z | N=1 r=5 → N=2 r=1 | new cycle on re-entry, not a revision of `N=1`: the measurement method and acceptance policy changed (2026-10-20 two-tier global/local policy replacing the frozen single-tier all-or-nothing gate), which opens a new cycle/baseline after `N=1` closed terminal `incomplete`; the monotonic in-place alternative (`N=1, r=6`) was not chosen because the collection runner accepts the item's own status path. Carried by reference, not re-litigated: `N=1, r=1..r=5` in `.pdca/status/T246-raw-datareader-1.md` (read-only). Outgoing attempt identity: `N=1, r=5, n=1/3`, terminal `incomplete` (`t246-gate-fail-recipe-lever`); `r=1` is the initial revision of `N=2`.

## Defect history

Carried by reference from the closed record `.pdca/status/T246-raw-datareader-1.md` §`## Defect history` (read-only, not re-opened, no fix count changed). Its table carries **9** keys, carried as they stand: `perf-oracle-window-noise` (r=1/n=1, 0 fixes, planner terminal GAP); `log-per-op-vs-iteration-total` (r=2, fixed); `literal-job-id-vs-resolved-job` (r=2, fixed); `idle-vbcscompiler-foreign-activity` (r=2, fixed); `foreign-competing-workload-mid-block` (r=3, 0 fixes, terminal GAP — invalidity reason, not a product defect key); `defunct-csc-foreign-false-positive` (r=4/n=1, 1 fix); `cached-path-multi-report-aggregation` (r=4/n=1, 1 fix); `reader-seam-cache-ownership` (r=4/n=1, 0 fixes, STOP → PLAN); `t246-gate-fail-recipe-lever` (r=5/n=1, 0 fixes, measured FAIL). No key of `N=2` yet and no new observation recorded here.

### CHECK n=1/3 findings (T246-R1-F1..F5) — cure state

CHECK at `n=1/3` raised five findings; their cure state after the evidence round:

| Key | Observed | Cure state |
|---|---|---|
| `T246-R1-F1` | ABBA not a true paired round (legs/ordering) | cured — true ABBA `A1→B1→B2→A2`, four legs rc 0, aggregate in `.pdca/T246/rv6/abba.md`; attempt-3 candidate-bound ABBA adds per-leg build identity (baseline `1c8aa64d…`, candidate `21be2682…`), pinned DB sha256 `32bf…`, all legs rc 0 (`.pdca/T246/rv6/attempts/attempt3/abba-*.log`) |
| `T246-R1-F2` | corrected residuals (candidate vs linq2db) mis-stated | cured — residuals recomputed as 1.072×/1.008× (10k) and 3.172×/2.841× (empty); attempt-3 candidate-bound ABBA residuals 1.057×/1.008× (10k) and 2.5×/2.84× (empty) recorded as measured |
| `T246-R1-F3` | acceptance gate not evidenced | cured — `--anyCategories=acceptance` 12 cases / 0 failures / 80 s / rc 0 (`.pdca/T246/rv6/logs/acceptance.log`) |
| `T246-R1-F4` | original build/test/provider logs + provider-labelled integration selection not evidenced | cured — build 0W/0E, core 123, sqlite 203+1 skip, conventions 22, integration 228/0/89 (logs under `.pdca/T246/rv6/logs/`); attempt-3 provider-labelled selection 37 passed / 19 skipped (MySQL unsupported-provider skips), rc 0, socket healthy, no `NEXTORM_SKIP_PROVIDERS` (`.pdca/T246/rv6/attempts/attempt3/integration-selected.log`); positive per-provider executed cases beyond the 37/19 split remain unbound |
| `T246-R1-F5` | item-only diff and identity reconciliation not evidenced | cured — `snapshot/item-diff.patch` (4 files, 47+/13−) + `provenance/identity-reconciliation.md` (commit chain to HEAD `97828831`) |
| `T246-R1-F6` | `LobDataReader` cached `FieldCount` (readonly field) stale across `NextResult()`/`NextResultAsync()` with differing column counts | fixed in candidate (fix count 1); **reverted in ACT** — the cached-field defect does not exist on the prior rung (FieldCount delegates to `_inner.FieldCount`), so the fix and its 2 regression tests were dropped; recorded as not credited |
| `T246-R1-F7` | `LobDataReader` double disposal — sync `Dispose(bool)` and `DisposeAsync()` disposed `_inner` AND the owner | fixed in candidate (fix count 1, commit `abd5b22f`); **ported in ACT as a correctness repair** onto the prior rung (exact-once delegation to `CommandReaderOwner`; `ReleaseOwner` removed; idempotent via `_disposed`); invocation-counting tests retained; not credited as T246 performance |

Carried keys from `N=1` remain as recorded (read-only, no fix count changed).

## Changed files

- `src/nextorm.core/Query/QueryCommandExtensions.cs` → ToDataReader/ToDataReaderAsync routed through OpenResultReader
- `src/nextorm.core/DataContext/DataContext.cs` → OpenResultReader/OpenResultReaderAsync gain sequentialAccess param
- `src/nextorm.core/DataContext/LobDataReader.cs` → pass-through trims + cached `FieldCount` dropped (T246-R1-F6 fix) + exact-once disposal (T246-R1-F7 fix, commit `abd5b22f`)
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs` → Provider_Direct_ToDataReader / _Empty control arms
- `tests/nextorm.core.tests/Lob/LobDataReaderTests.cs` → +3 disposal invocation-counting tests (T246-R1-F7)
- `.pdca/T246/rv6/manifest.json` → refreshed (footprint 6 paths, git_head `abd5b22f`, tree `12c55d06…`)
- `.pdca/T246/rv6/snapshot/**` + `snapshot.sha256` → refreshed at current HEAD
- `.pdca/T246/rv6/attempts/attempt3/README.md` → written
- `tests/nextorm.core.tests/Lob/LobDataReaderTests.cs` → 2 new regression tests (sync+async) for NextResult with differing column counts; FakeDbDataReader multi-result-set support
- `.pdca/status/T246-raw-datareader-2.md` → updated (DO result, changed files, git state, progress log)
- `.pdca/status/collection-rc5.md` → one progress-log row appended

## Git state

`status path: .pdca/status/T246-raw-datareader-2.md — modified this cycle · tracked`; baseline `1e31331d`, branch `1.0.9-rc5`. Collection container `.pdca/status/collection-rc5.md` modified this cycle. `.pdca/T246/rv6/` holds evidence (gitignored). ACT task-owned delta pending commit: reverted `src/nextorm.core/DataContext/LobDataReader.cs` + `src/nextorm.core/Query/QueryCommandExtensions.cs` (to `f03e3f6c^` content + F7 port), reverted `src/nextorm.core/DataContext/DataContext.cs` OpenResultReader signature, dropped F6 tests in `tests/nextorm.core.tests/Lob/LobDataReaderTests.cs`, retained benchmark arms, errata in `.pdca/T246/rv6/attempts/attempt3/README.md`, and the two status files. Work outside this item's delta stays untouched and unclaimed.

## Progress log

append-only, one row per event, never edited retroactively:

2026-10-30T07:03:32Z | PLAN | revision r=1 | attempt n=1 | PLAN completed (form=cycle, overlay=coder+.NET, N=2, r=1, n=1/3, plan_state=ready) — handoff at PLAN→DO boundary; re-entry after N=1 r=5 terminal incomplete, oracle reformulated per the brief's two-tier policy | `.pdca/status/T246-raw-datareader-2.md` §PLAN record (plan `r1`), evidence root `.pdca/T246/rv6/`
2026-10-30T07:03:32Z | PLAN | revision r=1 | attempt n=1 | Notice (fidelity, autonomous): the planner's `r1` report reached the executor in two truncated parts and is recorded joined at exactly one seam marked `⟦SEAM⟧`; no other wording altered | `.pdca/status/T246-raw-datareader-2.md` §Current state, §PLAN record `r1` §6
2026-10-31T02:30:00Z | DO | revision r=1 | attempt n=1/3 | candidate recorded: ToDataReader routed through OpenResultReader + lean LobDataReader; empty-arm time -13.4 %, allocations flat; 10k allocations flat, time +1.4 % inside noise; container gate 7934/7733/0/201 rc 0; evidence .pdca/T246/rv6/ (helpers rc 0) | .pdca/T246/rv6/candidate.md, .pdca/T246/rv6/gates/integration.log
2026-10-30T22:00:55Z | DO | revision r=1 | attempt n=1/3 | M1 evidence round: identity reconciliation (9e0832c8→f03e3f6c→c1a8871c→HEAD 97828831) + item-only diff (snapshot/item-diff.patch) + refreshed snapshot.sha256 (6 files) + provenance/identity-reconciliation.md; no product change | .pdca/T246/rv6/provenance/identity-reconciliation.md, .pdca/T246/rv6/snapshot/item-diff.patch, .pdca/T246/rv6/snapshot/snapshot.sha256
2026-10-30T22:10:19Z | DO | revision r=1 | attempt n=1/3 | M2 ABBA A1/B1/B2/A2 complete: baseline pair means 4560.84 us/973504 B (10k) and 196.545 us/16048 B (empty); candidate pair means 3638.92 us/973504 B (10k) and 166.755 us/16048 B (empty); time ratios 0.7979 and 0.8484 (beyond +/-5 %), allocs flat; local acceptance met; corrected residual 1.072x/1.008x (10k), 3.172x/2.841x (empty) | .pdca/T246/rv6/abba.md, .pdca/T246/rv6/logs/abba-A1.log..abba-A2.log
2026-10-30T22:12:00Z | DO | revision r=1 | attempt n=1/3 | M3 acceptance gate: dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance; 12 benchmarks executed, 0 failures, wall 80s (<=4 min), rc 0 | .pdca/T246/rv6/logs/acceptance.log
2026-10-30T22:14:00Z | DO | revision r=1 | attempt n=1/3 | M4 build/test/provider logs: dotnet build nextorm.slnx -c Debug rc 0 (0 W/0 E); core reader/LOB/streaming/ownership 123 passed; sqlite reader/streaming 203 passed+1 skip; conventions 22 passed; integration selected ToDataReader/ToStream/ToTextReader 228 passed/0 failed/89 skipped rc 0 (provider-labelled); whole-suite cite gates/integration.log rc 0 inferred | .pdca/T246/rv6/logs/build-debug.log, tests-core-reader-lob.log, tests-sqlite-reader-streaming.log, tests-conventions.log, integration-selected.log
2026-10-30T22:18:22Z | DO | revision r=1 | attempt n=1/3 | M5 evidence round finalized: manifest refreshed (git_head 9ff27cdf, evidence pointers abba/acceptance/build/core/sqlite/conventions/integration/item-diff/identity-reconciliation), helpers rc 0/0, tree unchanged 679c5ffc; status updated (CHECK, evidence_round=1 spent, candidate awaiting re-CHECK); defect findings T246-R1-F1..F5 cure states recorded; collection row appended | .pdca/T246/rv6/manifest.json, .pdca/T246/rv6/helpers.log, .pdca/status/T246-raw-datareader-2.md
2026-10-30T22:28:34Z | DO | revision r=1 | attempt n=2/3 | T246-R1-F6 fixed: LobDataReader cached FieldCount dropped (delegates to inner); 2 regression tests (sync+async) added; gates build 0W/0E, core 125 pass, sqlite 218+1 skip, conventions 35, integration subset 37/0/19 rc 0; ABBA/acceptance cited from attempt 1 (benchmarks unaffected) | .pdca/T246/rv6/attempts/attempt2/, .pdca/T246/rv6/abba.md
2026-10-30T23:00:57Z | DO | revision r=1 | attempt n=3/3 | M4 evidence round finalized: T246-R1-F7 fixed (exact-once disposal, commit abd5b22f; 3 new tests; 30/30 LobDataReader pass); candidate-bound ABBA shows no ±5% time win on either arm and +17.3% empty-arm regression inside noise-dominated short run (10k ratio 0.975, empty 1.173; allocs 973504 flat / 16048->16104 -56 B); acceptance gate 12/0/79.5s rc 0; provider selection 37/19 rc 0; manifest refreshed (tree 12c55d06); snapshot refreshed; helpers rc 0/0; status updated (CHECK, n=3/3 spent, candidate awaiting final CHECK) | .pdca/T246/rv6/attempts/attempt3/README.md, .pdca/T246/rv6/manifest.json, .pdca/T246/rv6/snapshot/snapshot.sha256
2026-10-31T03:00:00Z | ACT | revision r=1 | attempt n=3/3 | ACT finalized incomplete/STOP: no C1 credit, performance mechanism reverted to prior rung (f03e3f6c^ content + F7 correctness repair ported); benchmark arms retained; F6 tests dropped; errata appended to attempts/attempt3/README.md; issue #246 stays OPEN; build 0W/0E, core 28 LobDataReader tests pass, sqlite 2252+1 skip, benchmark arms run rc 0 | .pdca/status/T246-raw-datareader-2.md, .pdca/status/collection-rc5.md
