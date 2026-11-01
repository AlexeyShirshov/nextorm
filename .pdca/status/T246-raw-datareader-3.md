# Cycle status: T246 / cycle 3

## Current state

- task: `T246` · form: `cycle` · specialized skill: `pdca-dotnet` (`coder+.NET`, inherits `pdca-coder`)
- mode: `autonomous`
- disposition: `closed` (incomplete; DO→ACT; no candidate; issue #246 OPEN)
- cycle: `N=3` · revision: `r=1` · attempt: `n=2/3` (entering; `n=3/3` in reserve; prior `n=1/3` spent in `N=2` on reverted `OpenResultReader` + lean `LobDataReader` candidate, empty arm +17.3 %)
- scope: `#246` only — `ToDataReader` raw reader vs linq2db on fresh-fluent path (10k + empty arms), construction-cost lever under `G1-SCF-01`; status path `.pdca/status/T246-raw-datareader-3.md`
- goal: measured performance improvement on `ToDataReader` (Tier A global ratios or Tier B one-axis local win) committed on `1.0.9-rc5`, or measured residual proof recorded `incomplete` with issue left open — §PLAN record and §Original task authoritative
- phase: `ACT` · record: `completed` · outcome: `incomplete (no in-scope construction lever without design change; DO→ACT)`
- source: issue `#246` (`AlexeyShirshov/nextorm`), milestone `1.0.9-rc5`; re-entry cycle `N=3` after `N=2` terminal `incomplete` (`.pdca/status/T246-raw-datareader-2.md`, read-only) · context refs: caller dispatch "three residuals of the closed rc5 collection; item #246"; retained wins `84e98272`, `2ea3ef45`, `F7` in HEAD `04d35452`
- criteria: `AC-246-01..04` — exact text in §PLAN record `r1` (source: caller dispatch), not retyped here
- evidence root: `.pdca/T246/rv8/`

## Original task

Exact reference to the caller's dispatch: "three residuals of the closed rc5 collection; item #246", plus the verbatim user-supplied acceptance criteria for #246 (AC-246-01..04).

Verbatim user-supplied acceptance criteria (AC-246-01..04), source: caller dispatch / frozen plan §3.2:

**AC-246-01 (Global target / Local acceptance):**
> **Global target (milestone-level):** raw reader at 10,000 rows ≤ **1.00×** linq2db time and ≤ **1.20×** linq2db allocations; the empty-result arm likewise (or a documented, owner-approved tolerance). The original **3.942 ms / 943.00 KB** and ≤1.00× absolute/ratio numbers remain the global/stretch reference.
>
> **Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
> - allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
> - time improves beyond the pre-registered noise band and allocations do not increase.
>
> A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.

**AC-246-02 (Measurement rules):**
> **Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.

**AC-246-03 (Mandatory correctness and evidence):**
> - No materialization/result change; LOB terminals (`ToStream`/`ToTextReader`) retain their locator/sequential-access behavior and tests.
> - `CommandReaderOwner` dispose semantics remain: disposing the reader releases the per-call command and provider reader; the context stays open.
> - Add a benchmark/gate row for the raw reader; run the streaming report on all relevant providers if the seam changes cross-provider.

**AC-246-04 (Constraints):**
> - Do not remove LOB support or change `ToStream`/`ToTextReader` semantics.
> - Do not require callers to use `Prepare()`; the measured path remains fresh-fluent.
> - No benchmark-specific SQL or SQLite-only production hardcode; the seam choice must be dialect-driven.

## PLAN record

### r1

````markdown
# PDCA Entry Plan — Three `rc5` Residuals (`#246`, `#267`, `#311`)

## 1. Form, Specialized Skill & Entry Scope

- **Form:** Three independent **`cycle`** executions (invoked sequentially in a single-process lane: `#246` → `#267` → `#311`), **not** a container `collection`.
- **Specialized skill / overlay:** `pdca-dotnet` (`coder+.NET`, inheriting `pdca-coder`).
- **One-line form rationale:** Each of the three open issues (`#246`, `#267`, `#311`) is an independent, bounded .NET performance goal with its own target surface, benchmark harness, acceptance criteria, and remaining attempt budget (`r=1`, `n=2/3` starting after 1 prior spent attempt) where the systemic finding `G1-SCF-01` bounds the candidate space to construction-cost reduction (or proving no in-scope lever exists) without cross-item code coupling or a shared container-level integration gate.
- **Why three `cycle`s rather than a `collection`:**
  1. `plan.md` §Entry assigns `cycle` when each goal's method and volume are bounded, and `collection` when managing a batch container with a unified integration gate. Here, the user explicitly forbade `collection/*` branches (`No push, no merge, no tag, no collection/* branch`), required commits directly on `1.0.9-rc5` with `#N` per item, and noted that the prior `rc5` collection failed container-level integration acceptance for batch evidence-packaging reasons (`AC-COL-01/04`) even when individual items had clear outcomes.
  2. Each issue has disjoint product touchpoints (`LobDataReader` / `QueryCommandExtensions` / `QueryExecutor` for `#246`; `examples/nextorm.examples.postgres.aviasales` / PG expression construction for `#267`; `JsonRowWriterFactory` / JSON streaming pipeline for `#311`) and independent terminal closure (`gh issue close` per accepted item vs. `incomplete` leaving that issue open).
  3. Sequential single-lane execution of three independent `cycle`s satisfies the single-process benchmark/container rule while isolating each item's status record, evidence root, and terminal verdict.
- **Entry scope:**
  - **Cycle 1 (`#246`):** Raw `ToDataReader` / `ToDataReaderAsync` per-call/per-row overhead vs `linq2db` on the fresh-fluent path (10,000-row arm and empty-result fixed-cost arm), plus mandatory raw-reader benchmark/gate row and LOB/dispose invariants.
  - **Cycle 2 (`#267`):** `Nextorm_DelayPercentilesByModel` per-call allocation ratio vs `Linq2Db_DelayPercentilesByModel` in `examples/nextorm.examples.postgres.aviasales` (targeting the `G1-SCF-01` construction-cost dominant share of ≈18.4 KB / 28.25 KB residual while preserving SQL semantics, entity mappings, `FromSql` fallback, and 8-row output).
  - **Cycle 3 (`#311`):** JSON streaming (`WriteJson`) 10k per-row throughput lever with operation-count attribution, same-session 7/7 acceptance baseline capture (`<baseline>/round-{1,2}/acceptance/cases.json`), and pre-registered noise-robust ratio oracle before live candidate measurement.
- **Anchor snapshot:** Repo `/home/alex/sources/nextorm`, branch `1.0.9-rc5`, HEAD `04d35452`. One untracked file `docs/assets/diagrams/selectwheremin-current.svg` exists in the working tree and must remain untouched and unstaged. Retained wins from prior attempts (`84e98272` on `#267`, `2ea3ef45` on `#311`, `F7` exact-once disposal and control arms on `#246`) are in `HEAD` and are part of the starting baseline.

## 2. Attempt Accounting, Status Paths & Evidence Roots

Per `plan.md` §Revision accounting, a fresh entry on the **same task with unchanged user-supplied criteria** does not erase prior attempt history or reset `r`/`n`, while the caller's constraint forbids rewriting or reusing the closed `rc5` status files and evidence roots. Therefore:
- Each item opens a **new cycle status file (`N+1`)** so closed `rc5` records stay strictly read-only, while **carrying forward the task's cumulative attempt counter (`r=1`, 1 attempt already spent → entering attempt `n=2/3`, with `n=3/3` remaining in reserve)** and full defect/candidate history from the prior cycle.
- **Status files and evidence roots (collision-free against closed `rc5` paths):**
  1. **#246:**
     - Prior read-only records: `.pdca/status/T246-raw-datareader-1.md`, `.pdca/status/T246-raw-datareader-2.md`; prior evidence `.pdca/T246/rv4..rv6`.
     - **New status file:** `.pdca/status/T246-raw-datareader-3.md` (`N=3`, carrying `r=1`, prior `n=1/3` spent on the reverted `OpenResultReader` + lean `LobDataReader` candidate that regressed the empty arm by +17.3%; entering at `n=2/3`).
     - **New evidence root:** `.pdca/T246/rv7/` (and `rv8/` if `n=3/3` is used).
  2. **#267:**
     - Prior read-only record: `.pdca/status/T267-rc5-1.md`; prior evidence `.pdca/rc5/items/T267/rv1`.
     - **New status file:** `.pdca/status/T267-delay-percentiles-2.md` (`N=2`, carrying `r=1`, prior `n=1/3` spent on `84e98272` `readonly record struct` projections which reduced allocations from 29.70/29.76 KB to 28.25/28.23 KB [5.08×]; entering at `n=2/3`).
     - **New evidence root:** `.pdca/T267/rv2/` (and `rv3/` if `n=3/3` is used).
  3. **#311:**
     - Prior read-only record: `.pdca/status/T311-json-streaming-1.md`; prior evidence `.pdca/T311/rv2`.
     - **New status file:** `.pdca/status/T311-json-streaming-2.md` (`N=2`, carrying `r=1`, prior `n=1/3` spent on `2ea3ef45` `JsonEncodedText.Encode` + unrecoverable `C311-02` capture order / 0-pair `N=9` oracle; entering at `n=2/3`).
     - **New evidence root:** `.pdca/T311/rv3/` (and `rv4/` if `n=3/3` is used).

## 3. Goal, Requested/Delivered Result & Verbatim Acceptance Criteria

### 3.0 Original Task (verbatim)
Reproduced in Section 1 of the caller's dispatch above: execute the three open residual issues (`#246`, `#267`, `#311`) from the closed `rc5` collection on branch `1.0.9-rc5` (`HEAD 04d35452`) in AUTONOMOUS mode with auto-commit authorized on `1.0.9-rc5`, carrying `G1-SCF-01` and all retained wins (`84e98272`, `2ea3ef45`, `F7`), under `Measurement first` and the two-tier acceptance rules.

### 3.1 Requested Result & Delivered Result (identical object)
- **Requested result:** Measured performance improvements (meeting Tier A global ratio targets or Tier B one-axis local wins without regression on the other axis, plus all mandatory item deliverables and correctness invariants) committed on branch `1.0.9-rc5` with issues closed for accepted items, or—where no in-scope construction/per-row lever can win without regression—a measured residual proof under `G1-SCF-01` recorded as `incomplete` with the issue left open, plus a ≤10-line summary report.
- **Delivered result:** The exact same set of committed and verified performance changes on `1.0.9-rc5` (with `#N` and evidence pointers, linted status files, and closed GitHub issues for accepted cycles) and/or measured `incomplete` residual records for any item with no viable in-scope lever, summarized in ≤10 lines.

### 3.2 User-Supplied Acceptance Criteria (verbatim; immutable set)

#### Cycle 1: `#246` — "Perf: ToDataReader raw reader is slower than linq2db"
- **AC-246-01 (Global target / Local acceptance):**
  > **Global target (milestone-level):** raw reader at 10,000 rows ≤ **1.00×** linq2db time and ≤ **1.20×** linq2db allocations; the empty-result arm likewise (or a documented, owner-approved tolerance). The original **3.942 ms / 943.00 KB** and ≤1.00× absolute/ratio numbers remain the global/stretch reference.
  >
  > **Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
  > - allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
  > - time improves beyond the pre-registered noise band and allocations do not increase.
  >
  > A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.
- **AC-246-02 (Measurement rules):**
  > **Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.
- **AC-246-03 (Mandatory correctness and evidence):**
  > - No materialization/result change; LOB terminals (`ToStream`/`ToTextReader`) retain their locator/sequential-access behavior and tests.
  > - `CommandReaderOwner` dispose semantics remain: disposing the reader releases the per-call command and provider reader; the context stays open.
  > - Add a benchmark/gate row for the raw reader; run the streaming report on all relevant providers if the seam changes cross-provider.
- **AC-246-04 (Constraints):**
  > - Do not remove LOB support or change `ToStream`/`ToTextReader` semantics.
  > - Do not require callers to use `Prepare()`; the measured path remains fresh-fluent.
  > - No benchmark-specific SQL or SQLite-only production hardcode; the seam choice must be dialect-driven.

#### Cycle 2: `#267` — "Perf: examples/PG DelayPercentilesByModel allocates 5.67× linq2db"
- **AC-267-01 (Allocation & time target / Local acceptance per dispatch):**
  > - `Nextorm_DelayPercentilesByModel` allocated ≤ **1.2×** `Linq2Db_DelayPercentilesByModel` (stretch ≤1.0×), with no time regression and the same 8 rows in both arms.
  > - *(Per caller dispatch rules on Tier A / Tier B)*: Tier A = the target ratio (≤ 1.2×); Tier B = a real one-axis win with no regression beyond the band, accepted when the attempt budget is spent; keep and commit a win, never revert it to leave a clean tree.
- **AC-267-02 (Build cleanliness):**
  > - `dotnet build examples/nextorm.examples.postgres.aviasales -c Release` stays 0W/0E.
- **AC-267-03 (Before/after runs posted):**
  > - Before/after values (at least two runs each) posted here.
- **AC-267-04 (Constraints):**
  > **Constraints:** Do not change the modelled SQL semantics or the entity mappings to game allocation. Do not remove the linq2db arm or the `FromSql` fallback. `ShortRun` is noisy: validate with at least two runs before/after.

#### Cycle 3: `#311` — "Perf: JSON streaming — 10k per-row throughput lever, same-session acceptance pairing, noise-robust ratio oracle"
- **AC-311-01 (Global target / Local acceptance):**
  > **Global target (milestone-level):** JSON streaming at 10,000 rows — time ≤ **1.00×** linq2db and allocations ≤ **1.20×** linq2db, same-session paired measurement.
  >
  > **Local acceptance (task closure):** if the chosen method cannot reach the global target, accept it when the measured change is a real one-axis win with no regression on the other:
  > - allocations decrease vs the same-round baseline and time does not worsen beyond the pre-registered noise band; **or**
  > - time improves beyond the pre-registered noise band and allocations do not increase.
  >
  > A method that improves neither axis, or improves one while regressing the other, is rejected and reverted. Reaching the global ratios is **not** required to close the task: record the residual gap to the global target and the measured local win.
- **AC-311-02 (Measurement rules):**
  > **Measurement rules:** same session/round and pinned DB, paired/ABBA measurement; no historical-number substitution; no threshold/tolerance/denominator edits after measurement. Allocation changes are judged by exact byte counts; time changes by the pre-registered noise band.
- **AC-311-03 (Task-specific deliverables):**
  > - A per-row throughput lever for 10k is attributed, implemented and measured; operation-count change is shown.
  > - A same-session paired acceptance baseline (`<baseline>/round-{1,2}/acceptance/cases.json`, 7/7 cases) is captured before the product change.
  > - A new noise-robust ratio oracle is pre-registered before its first live measurement; T247's frozen thresholds, tolerances, denominators and historical GAP are **not** edited.
  > - Output bytes, streaming/error/cancellation contracts and provider behavior are preserved; cache bounds/isolation remain.

## 4. Measurement Method (Pointable Specification per Item)

All measurements obey the global comparability rules: **single benchmark/container process active at a time**, `Measurement first` (capture the current-tree baseline in the same session before applying a candidate change), **paired/ABBA same-round structure** on a **pinned database**, **no substitution of historical numbers**, and **no post-measurement edits to thresholds, tolerances, or denominators**.

### 4.1 `#246` Measurement Method (`ToDataReader` Raw Reader)
- **Harness & arms:** `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs` (`B_RawReader` category, lines `165, 169, 190-198, 202, 210`).
  - Measured subject arms: NextORM fresh-fluent `ToDataReader` / `ToDataReaderAsync` at **10,000 rows** (large arm) and **0 rows / empty-result** (small fixed-cost arm).
  - Reference denominator arms: `linq2db` equivalent reader at **10,000 rows** and **0 rows / empty-result**, plus direct-provider-reader control arm (`SqliteDataReader`).
- **Metrics & units:**
  - Execution time: mean microseconds / milliseconds per operation (`Mean`, `us`/`ms`) from BenchmarkDotNet.
  - Allocations: exact managed bytes allocated per operation (`Allocated`, `B`/`KB`) from BenchmarkDotNet `MemoryDiagnoser`.
- **Denominator & ratios:**
  - Global ratio denominator: `linq2db` same-round mean time and allocated bytes at the matching row count (`10,000` rows and `0` rows).
  - Local (Tier B) denominator: same-round paired baseline of the current tree (`HEAD` with `F7` retained) at both `10,000` rows and `0` rows.
- **Comparability conditions & filters:**
  - Pinned SQLite database file: `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, single-process execution, Release build (`-c Release`).
  - Paired **ABBA** interleaving (`Baseline(A) → Candidate(B) → Candidate(B) → Baseline(A)`) within the same session on the pinned DB.
  - Both the **10,000-row arm** and the **empty-result arm** must be evaluated; a candidate that regresses the empty-result arm beyond the pre-registered noise band (as the prior candidate did at `+17.3%`) is rejected even if 10k is flat or improved.
- **Thresholds & provenance (user-supplied in `#246`):**
  - **Tier A (Global):** 10k rows time ≤ `1.00×` linq2db AND allocations ≤ `1.20×` linq2db; empty-result arm likewise.
  - **Tier B (Local win):** Exact byte decrease (`ΔAllocated < 0 B`) with time within the pre-registered noise band on both arms, **OR** time improvement beyond the pre-registered noise band with `ΔAllocated ≤ 0 B` on both arms.

### 4.2 `#267` Measurement Method (`examples/PG DelayPercentilesByModel`)
- **Harness & arms:** `examples/nextorm.examples.postgres.aviasales/Benchmarks.cs` (`:22, :88, :91`), configured in `Program.cs` (`:47, :69-89, :116-126`) against the PostgreSQL `demo` dataset (`DemoDatabase.cs`).
  - Measured subject arm: `Nextorm_DelayPercentilesByModel` (currently retaining `84e98272` `readonly record struct` projections at `AviasalesQueries.cs:407-426, 568-574`).
  - Reference denominator arm: `Linq2Db_DelayPercentilesByModel` (plus `FromSql` arm retained intact).
  - Additionally, measure or attribute the build-only / fixed query-construction allocation component (`G1-SCF-01`) alongside the full execution arm so construction vs execution bytes are separated.
- **Metrics & units:**
  - Allocations: exact managed bytes allocated per operation (`Allocated`, `B`/`KB`).
  - Execution time: mean milliseconds per operation (`Mean`, `ms`).
  - Correctness observable: exact 8-row result set matching between `Nextorm_DelayPercentilesByModel` and `Linq2Db_DelayPercentilesByModel`.
- **Denominator & comparability conditions:**
  - Single active container (PostgreSQL only; no concurrent SQL Server/MySQL/ClickHouse containers) via `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, verified before each run.
  - At least **two paired runs before** (current tree baseline with `84e98272`) and **two paired runs after** the candidate change in the same session against the same pinned PostgreSQL instance/dataset (`ShortRun` noise mitigation required by `#267`).
- **Thresholds & provenance (user-supplied in `#267` + caller dispatch):**
  - **Tier A (Global):** `Nextorm_DelayPercentilesByModel` allocated ≤ `1.20×` `Linq2Db_DelayPercentilesByModel` (stretch ≤ `1.00×`), no time regression beyond the noise band, identical 8 rows.
  - **Tier B (Local win, accepted when attempt budget is spent):** Exact allocation byte reduction vs the same-round current-tree baseline (`< 28.23 KB`) with no time regression beyond the noise band (or time improvement beyond the noise band with no allocation increase), identical 8 rows, and `0W/0E` Release build.

### 4.3 `#311` Measurement Method (JSON Streaming `WriteJson` 10k + Acceptance Baseline + Ratio Oracle)
- **Harness & arms:** `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWriteJson.cs` (`:231` NextORM `WriteJson`, `:311` `linq2db`), plus the 7-case JSON streaming acceptance suite (`cases.json`).
  - Row-count arms: `10,000` rows (primary target arm), plus `1,000` rows (small fixed-cost arm) and `100,000` rows (large scaling arm).
- **Metrics & units:**
  - Execution time (`Mean`, `us`/`ms`), managed allocations (`Allocated`, `B`/`KB`), and attributed per-row operation count (`ops/row` or call count per 10k rows).
  - Ratio oracle statistic: paired log-difference ratio `log(T_nextorm / T_linq2db)` evaluated under a pre-registered noise-robust oracle specification (`docs/specs/performance/json-ratio-oracle-2.md` — a new versioned spec file so `json-ratio-oracle-1.md` and `T247`'s frozen thresholds/tolerances/denominators/historical GAP remain unedited).
- **Comparability conditions & mandatory pre-change deliverables:**
  1. **Pinned DB & single process:** `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, single process, Release build.
  2. **Same-session paired acceptance baseline before product change:** Capture `<baseline>/round-1/acceptance/cases.json` and `<baseline>/round-2/acceptance/cases.json` (7/7 cases passing) on the current tree (`HEAD` with `2ea3ef45` retained) **before** applying any new candidate code change.
  3. **Pre-registered noise-robust ratio oracle before first live candidate measurement:** Register the updated oracle protocol (fixing the capture-order / pairing flaw that caused `C311-02` to yield `0` valid pairs in `rv2`, without editing `T247`'s frozen thresholds, tolerances, denominators, or historical GAP) before running live candidate measurements.
  4. **Paired/ABBA candidate vs baseline measurement** across the small (`1k`) and target (`10k`, `100k`) arms in the same session.
- **Thresholds & provenance (user-supplied in `#311`):**
  - **Tier A (Global):** At `10,000` rows, time ≤ `1.00×` linq2db AND allocations ≤ `1.20×` linq2db in same-session paired measurement.
  - **Tier B (Local win):** Exact allocation decrease vs same-round baseline (`2ea3ef45`) with time within the pre-registered noise band, **OR** time improvement beyond the pre-registered noise band with no allocation increase, accompanied by attributed per-row operation-count reduction, 7/7 acceptance baseline rounds, and preserved output bytes / streaming / cancellation / cache-bound contracts.

## 5. Cycle Terminal-Outcome Rules & Mapping to GitHub Issues

For each of the three independent cycles (`#246`, `#267`, `#311`), the cycle terminates under one of the following rules:

1. **`accepted` (Tier A — Global target met):**
   - A candidate meets the global target ratios (`≤ 1.00×` time and `≤ 1.20×` allocations vs `linq2db`) and all mandatory correctness/deliverable criteria (`AC-246-*`, `AC-267-*`, or `AC-311-*`).
   - **Action:** Commit touched product/test/benchmark paths on `1.0.9-rc5` with `#N` and the evidence pointer, record `accepted` in the item's new status file (`status_lint.py` rc 0), post before/after evidence to the issue, and close the issue via `gh issue close #N`.
2. **`accepted` (Tier B — Local one-axis win when Tier A is not reached within the attempt budget):**
   - When the attempt budget (`n ≤ 3/3`) is exhausted without reaching Tier A, or when `G1-SCF-01` makes Tier A unreachable without out-of-scope architectural redesign, any candidate that achieves a verified **Tier B local win** (real improvement on time beyond the noise band or exact byte reduction in allocations, with zero regression beyond the band on the other axis and on the small/empty arm, plus all mandatory item deliverables satisfied) is **kept, committed on `1.0.9-rc5`, and never reverted**.
   - **Action:** Commit the win on `1.0.9-rc5` with `#N` + evidence pointer, record both the measured local win and the residual gap to the global target in the status file (`accepted` under Local Acceptance / Tier B), post the before/after numbers and residual to `#N`, and close the issue via `gh issue close #N`. *(Note: for `#267` and `#311`, previously retained commits `84e98272` and `2ea3ef45` are already in `HEAD` from `n=1/3`; if a new attempt in `n=2/3..3/3` produces a further win, commit it; if no further candidate improves on the current tree, see rule 3 below while keeping `84e98272` and `2ea3ef45` intact in the tree.)*
3. **`incomplete` (No viable in-scope lever / attempt budget exhausted without a new net win):**
   - If all remaining attempts (`n=2/3` and `n=3/3`) fail their paired/ABBA gate or prove that no in-scope construction/per-row lever exists without violating constraints (e.g., requiring `Prepare()`, breaking LOB/streaming semantics, or altering modelled SQL/mappings), any regressing candidate is reverted **back to the starting `HEAD` (or latest kept win)**—never reverting previously retained wins (`84e98272`, `2ea3ef45`, `F7`).
   - **Action:** Record `incomplete` in the item's new status file with the measured residual and the `G1-SCF-01` / seam attribution (`status_lint.py` rc 0), leave the GitHub issue **open** (do **not** run `gh issue close`), and proceed to the next cycle.

## 6. Constraints, Prohibitions & Assumptions

### Constraints & Prohibitions (Carried to DO & CHECK)
- **Git & Branch Discipline:**
  - Stay on branch `1.0.9-rc5` (starting `HEAD 04d35452`).
  - **No `git push`, no `git merge`, no `git tag`, no `collection/*` branch.**
  - Auto-commit is authorized **only** on `1.0.9-rc5` for accepted wins/deliverables, staging **touched paths only**, with `#N` and the evidence root pointer in the commit message.
  - Never stage, modify, or delete the untracked file `docs/assets/diagrams/selectwheremin-current.svg`.
  - Never rewrite, move, or overwrite closed `rc5` status records (`.pdca/status/collection-rc5.md`, `T246-raw-datareader-1.md`, `T246-raw-datareader-2.md`, `T267-rc5-1.md`, `T311-json-streaming-1.md`) or their evidence directories.
  - **Never revert a kept win** (`84e98272`, `2ea3ef45`, `F7`, or any new win achieved in this run) to "leave a clean tree".
- **Technical & Repo Standards (`pdca-dotnet` + `AGENTS.md`):**
  - C# symbol discovery and impact analysis must use the `roslyn` tool via `execute` (`tools.roslyn`), never `rg`/`grep` for C# symbols.
  - Preserve **CRLF** line endings across all edited files (`perl -pi -e 's/\r?\n/\r\n/g' <file>`).
  - Central Package Management (`Directory.Packages.props`), `TreatWarningsAsErrors=true` (0 warnings / 0 errors across Debug and Release), and **FluentAssertions-only** in `tests/**` (`AssertionStyleTests` and `IntegrationSuiteScopingTests` must stay green).
  - Shared query command trap: never mutate `queryCommand.Cache = false` (`_dontCache` is sticky across the context); if uncached preparation is ever needed, pass `storeInCache: false` locally.
- **Process & Container Isolation:**
  - **One benchmark or container process at a time.**
  - Check `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` before any container test or PG benchmark; bring providers up **one at a time** (SQL Server previously OOM-killed at `exited 137` around ~1.17 GB peak when sharing memory).
  - Integration tests are fail-closed; never present a provider-skipped run as cross-provider evidence.

### Statement Gaps & Resolution
| Statement Gap | Resolution Type | Decision / Grounding |
| :--- | :--- | :--- |
| Form choice (`collection` vs 3 `cycle`s) | Evidence (`plan.md` §Entry + caller dispatch) | Three sequential `cycle`s on `1.0.9-rc5`; avoids `collection/*` branches (forbidden) and decouples per-issue closure. |
| Status file naming & `N`/`r`/`n` continuity | Evidence (`plan.md` §Revision accounting) | New status files (`T246-raw-datareader-3.md`, `T267-delay-percentiles-2.md`, `T311-json-streaming-2.md`) preserve read-only `rc5` files while carrying `r=1, n=2/3` (2 attempts left per item). |
| How `#267` Tier B interacts with retained commit `84e98272` | Evidence (caller dispatch) | `84e98272` (5.34× → 5.08×) is already in `HEAD` from `n=1/3`. Cycle `#267` uses `n=2/3` (and `3/3` if needed) to target construction cost (`G1-SCF-01`, ~18.4 KB build-only). If a new construction-cost lever wins, commit and close `#267` under Tier A/B; if no further in-scope lever exists, keep `84e98272` in the tree and close the cycle according to whether the caller's Tier B threshold counts the net task win or requires a new win over `84e98272` (see Section 7). |
| How `#311` repairs `C311-02` (0-pair oracle) | Evidence (`AC-311-03`) | Pre-register a new oracle file (`docs/specs/performance/json-ratio-oracle-2.md`) that fixes pair capture ordering before live candidate measurement, without editing `json-ratio-oracle-1.md` or `T247`'s frozen artifacts, and capture `<baseline>/round-{1,2}/acceptance/cases.json` (7/7) before product changes. |

## 7. Minimal Solution Direction & Alternatives per Item (Guidance for DO, Not Steps)

Under `G1-SCF-01`, per-call query construction (expression tree visit, descriptor allocation, plan-key computation) dominates fresh-fluent overhead.

1. **Cycle 1 (`#246` — `ToDataReader` raw reader):**
   - *Prior failure to avoid:* Routing `ToDataReader` through `OpenResultReader` + lean `LobDataReader` regressed the empty-result arm by `+17.3%`.
   - *Direction:* Measure the exact fixed-cost (empty arm) and per-row (10k arm) breakdown on `HEAD` first. On locator-free dialects (like SQLite), `ToDataReader` currently wraps the provider reader in `LobDataReader` (`QueryCommandExtensions.cs:334-339, 388-393`) and incurs fresh-fluent query preparation / `CommandReaderOwner` allocations on every call. Any candidate seam (such as bypassing `LobDataReader` allocation when the dialect is locator-free and no LOB stream locator is active, or trimming per-call `QueryCommand` / `CommandReaderOwner` construction overhead on the fresh-fluent `ToDataReader` path) must be dialect-driven, preserve `CommandReaderOwner` exact-once disposal (`F7`) and `ToStream`/`ToTextReader` behavior, and pass the paired ABBA gate on **both** the empty arm and the 10k arm. Add the mandatory raw-reader benchmark/gate row.
2. **Cycle 2 (`#267` — `examples/PG DelayPercentilesByModel`):**
   - *Prior state:* Projections in `AviasalesQueries.cs:413-426` are already `readonly record struct` (`84e98272`), leaving ~28.25 KB vs 5.56 KB linq2db (~5.08×), of which ~18.44 KB is build-only query construction (`G1-SCF-01`: CTE + window/percentile expression tree construction and visitor passes).
   - *Direction:* Target the **construction allocation** of the fluent query and/or core expression-building/visitor allocations invoked during `DelayPercentilesByModel` construction (without altering SQL semantics, entity mappings, or requiring `Prepare()` if that violates the example contract), or—if `AviasalesQueries.cs` / core builder construction allocations can be trimmed in-place (e.g., reducing intermediate expression/dictionary/list allocations in the CTE/subquery/select construction path)—measure the before/after delta across ≥2 paired runs against live PostgreSQL.
3. **Cycle 3 (`#311` — JSON streaming `WriteJson` 10k):**
   - *Prior state:* Compile-time `JsonEncodedText.Encode` (`2ea3ef45`) is in `HEAD` (allocations `0.42×` linq2db at 10k; time ratio `0.916×` at 10k and `0.900×` at 100k, `1.089×` at 1k), but `n=1/3` failed acceptance because the paired acceptance baseline and the `N=9` oracle pairing (`0` pairs due to capture order) were broken.
   - *Direction:* First, capture the mandatory 7/7 same-session paired acceptance baseline (`<baseline>/round-{1,2}/acceptance/cases.json`) and pre-register the corrected noise-robust ratio oracle (`docs/specs/performance/json-ratio-oracle-2.md`) without touching `T247`'s frozen files. Second, attribute and implement a genuine 10k per-row throughput lever in the JSON writer/materializer loop (or construction + per-row `Utf8JsonWriter` / reader column access path, showing the exact operation-count reduction per row across 10k rows) and run the pre-registered paired oracle across 1k / 10k / 100k.

## 8. Plan-to-DO Handoff

Every DO dispatch for Cycles 1, 2, and 3 carries by reference:
1. **Original task & verbatim criteria:** Section 1 and Section 3.2 (`AC-246-01..04`, `AC-267-01..04`, `AC-311-01..03`).
2. **Measurement method & `Measurement first` rule:** Section 4. No product edit is allowed before the same-session baseline (and for `#311`, the 7/7 acceptance baseline rounds and pre-registered oracle spec) is captured on `HEAD`.
3. **Status & evidence binding:** Section 2 (`.pdca/status/T246-raw-datareader-3.md` → `.pdca/T246/rv7/`; `.pdca/status/T267-delay-percentiles-2.md` → `.pdca/T267/rv2/`; `.pdca/status/T311-json-streaming-2.md` → `.pdca/T311/rv3/`). Validate every status update with `.pdca/scripts/status_lint.py` (rc 0).
4. **Constraints & prohibitions:** Section 6 (branch `1.0.9-rc5` only, no push/merge/tag/`collection/*` branch, never touch `docs/assets/diagrams/selectwheremin-current.svg`, never rewrite closed `rc5` status/evidence files, never revert retained wins `84e98272`/`2ea3ef45`/`F7`, Roslyn-only for C# symbols, CRLF, 0W/0E, FluentAssertions-only, one benchmark/container process at a time).

## 9. Risks & Stop Conditions

- **Risk 1 — Container OOM / `exited 137` or missing Podman socket:**
  - *Mitigation / Stop condition:* Verify `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` before starting any container. Run at most **one** database container at a time (stop PostgreSQL before starting SQL Server or vice versa). If the Podman machine is stopped, start it and re-verify; if it cannot be started after recovery, record a resource blocker rather than faking or skipping container evidence.
- **Risk 2 — Empty-arm / small-arm fixed-cost regression masking as a 10k win (`#246`, `#311`):**
  - *Mitigation / Stop condition:* Both the small/empty arm (`0` rows for `#246`, `1k` rows for `#311`) and the `10k` arm must be measured in the same paired round. Any candidate that regresses the small/empty arm beyond the pre-registered noise band is rejected and reverted to the prior kept rung.
- **Risk 3 — Exhausting the remaining attempt budget (`n=2/3`, `n=3/3`) under `G1-SCF-01`:**
  - *Stop condition:* Strictly enforce the **no-4th-pass rule** (`n ≤ 3` per revision `r`). When `n=3/3` is spent (or earlier if DO/CHECK proves no in-scope construction/per-row lever exists without violating task constraints), stop attempting new candidates for that item, preserve all previously kept wins in the working tree, and finalize the cycle (`accepted` under Tier B if a verified net local win and all mandatory deliverables are in place, or `incomplete` with the measured residual and the issue left open).

## 10. Confidence & Verification State

- **Verified from supplied pointers and `plan.md`:**
  - Exact branch (`1.0.9-rc5`), HEAD (`04d35452`), untracked SVG path, read-only `rc5` status/evidence paths, retained commit SHAs (`84e98272`, `2ea3ef45`, `F7`), harness file/line pointers, and verbatim issue acceptance criteria for `#246`, `#267`, and `#311`.
- **Assumptions:**
  - None that alter the requested result or acceptance criteria; `/tmp/nextorm-bench/test.db` and the Podman socket at `/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` are assumed accessible to DO/CHECK as stated in the dispatch (and governed by the fail-closed stop condition if unavailable).
---

## END FROZEN PLAN
````

## PLAN record

### r1 resumption

````markdown
# PLAN — Resumption of `rc5` Residual Lane (`#246`, `#267`, `#311`)

- **Shared form, overlay & anchor (by reference to frozen entry plan `.pdca/status/T246-raw-datareader-3.md` §`## PLAN record` r1):** Three sequential, single-process ordinary `cycle`s; specialized skill `pdca-dotnet` (`coder+.NET`). No form reclassification. Anchored to branch `1.0.9-rc5` at HEAD `5c162b06` (two untracked user files `docs/assets/diagrams/selectwheremin-current.svg` and `docs/superpowers/specs/2026-11-01-query-components-proof-design.md` untouched).
- **Binding treatment of `G1-SCF-01` (verdict `LIMITED`):** Treated strictly as an investigation-grade hypothesis from a CTE construction workload (`9,025.344 / 9,145.344 B → 4,272 B/query` under expression hoisting; caveats 1–9 binding). Its bytes and percentages are **never** imported as baseline or proof for `#246`, `#267`, or `#311`.
- **Hard stop across all three blocks:** At most **one** evidence-completion round per criterion per revision; after it, take the honest verdict (implementation → DO, contract → PLAN, exhausted → ACT).

## Block 1: `#246` Resume — Raw `DbDataReader` (`ToDataReader`)

- **Attempt accounting, status & evidence root:** `N=3`, `r=1`, entering `n=2/3` (`n=3/3` in reserve). Status: `/home/alex/sources/nextorm/.pdca/status/T246-raw-datareader-3.md`. Evidence root: `.pdca/T246/rv8/` (fresh per-round root; reuses `.pdca/T246/rv7/logs/baseline-full.log` as the paired pre-change baseline).
- **Requested vs delivered result (same object):** Measurable reduction of fixed per-call time and allocation overhead on the fresh-fluent `ToDataReader` execution path relative to `linq2db` and provider-direct `DbDataReader` on `1.0.9-rc5`, delivered as a committed in-scope code change with paired benchmark and fail-closed test evidence, or an honest `incomplete` verdict if the in-scope ceiling cannot yield a Tier A/B win.
- **Cycle scope & acceptance criteria (by reference):** `ToDataReader` execution/preparation/builder seam in `src/nextorm.core/**` and dialect providers, plus unit/integration tests and benchmarks. Criteria: **`AC-246-01`, `AC-246-02`, `AC-246-03`, `AC-246-04`** by reference to `.pdca/status/T246-raw-datareader-3.md` §3.2 (verbatim, unchanged).
- **Ceiling quantification & gap analysis:**
  - *Established baseline (`rv7`):* At 10k rows, `B_Nextorm_ToDataReader` (`5,619.81 us / 973,504 B`) is already near parity with `Linq2Db_ToDataReader` (`5,519.80 us / 965,632 B`, **1.018× time / 1.008× alloc**, delta **+7,872 B/call**; per-row delta ~0 at `95.75` vs `96.00 B/row`), and prepared 10k (`947.91 KB`) differs by only **~2.78 KB**. On the empty arm (`0` rows), `B_Nextorm_ToDataReader_Empty` (`292.83 us / 16,048 B`) exceeds `Linq2Db_Empty` (`91.06 us / 5,648 B`) by **+201.77 us (3.216×)** and **+10,400 B/call (2.841×)**.
  - *Ceiling:* Any construction/preparation lever is bounded above by the **10,400 B/call (and ~202 us/call) empty-arm fixed gap**, and by **~2.78–10.4 KB (~0.3–1.1%)** on the 10k arm. Therefore, the lever targets the **fixed per-call overhead** (directly governing `B_Nextorm_ToDataReader_Empty` and small-N calls while keeping 10k ≤1.018×/1.008×).
- **Measurement method:**
  - *Subject & denominator arms:* `B_Nextorm_ToDataReader` (10k) and `B_Nextorm_ToDataReader_Empty` (0 rows) vs `Linq2Db_ToDataReader` / `Linq2Db_Empty` (primary denominator) and `Provider_Direct` / `Provider_Direct_Empty` (reference floor), plus prepared control arm.
  - *Metric, unit & filters:* Mean execution time (`us`/op) and managed allocation (`B`/op), Release build, pinned SQLite DB (`NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`), single process, paired same-round execution with both 10k and empty (small) arms visible.
  - *Thresholds & provenance (frozen plan §3.2):* **Tier A:** meets target ratios in `AC-246-01..02` across required arms with `AC-246-03..04` green. **Tier B:** statistically repeatable reduction on ≥1 primary axis (time or `B`/op on empty or 10k arm) vs the `rv7` baseline (`16,048 B` / `292.83 us` empty; `973,504 B` / `5,619.81 us` 10k) with no regression beyond the noise band on the other axis/arm and `AC-246-03..04` satisfied.
- **Candidate levers for DO (in-scope vs out-of-scope):**
  1. *Internal raw-shape / preparation cache reuse on the uncached `ToDataReader` path:* Extend `OpenLobReader`/`PrepareLobCommand` (`DataContext.cs:543-578`) and `OpenResultReader` (`:586-638`) → `QueryPlanner.GetPreparedQueryCommand` (`:702-776`) so fresh-fluent `ToDataReader` calls can reuse pre-prepared SQL/factory artifacts via `QueryPlanStore` without mutating `queryCommand.Cache` (`_dontCache` sticky trap) and without requiring user `.Prepare()`.
  2. *Per-call builder / expression-rewrite allocation reduction on `From → Where → Select → ToDataReader`:* Avoid avoidable per-call `EntityBuilder.Clone()`, `ReplaceParameterExpressionVisitor` allocation, or reader-owner wrapper churn when building fresh fluent commands, and/or hoist static expression trees in the benchmark/call-site only where permitted by `AC-246-04`.
  - *Out-of-scope:* `ToStream`/`ToTextReader`; relaxing `CommandReaderOwner` exact-once disposal (`AC-246-03`); requiring `.Prepare()` (`AC-246-04`); mutating shared `QueryCommand.Cache`; non-dialect-driven LOB branching.
- **Stop condition & `incomplete` trigger:** Stop as soon as a paired same-round run after the candidate lever(s) + at most one evidence-completion round finishes. If neither Lever 1 nor Lever 2 reduces the `10,400 B` / `201.77 us` empty-arm gap or 10k metrics (or if `n=3/3` is exhausted without a Tier A/B win), route to ACT as `incomplete` and leave `#246` open. Any measured Tier B win is committed and retained.
````

## Approval record

`not applicable: autonomous mode` — no ordinary plan approval was required or invented for this cycle.

## DO result

No product code change was made in this attempt (`n=2/3`): `Measurement first` baseline re-captured on the current tree (`HEAD 5c162b06`, F7 retained) on the pinned DB into `.pdca/T246/rv8/logs/baseline-round1.log` (exit 0, 9 reader arms). Candidate attribution (fixed vs per-row, same as prior cycle): fixed (empty) term dominates - NextORM empty 272.95 us / 16.16 KB vs provider-direct 112.46 us / 5.3 KB (delta alloc ~10.86 KB); per-row overhead ~0 (NextORM 95.75 B/row vs provider-direct ~96 B/row). Prepared 10k arm 8,105.04 us / 943.41 KB vs fresh 20,485.54 us / 951.17 KB - only ~7.76 KB separates prepared from fresh across 10k rows, so query construction/preparation is a small share of the fixed cost; the bulk of the ~10.86 KB fixed gap is preparation/execution overhead (command, plan, reader, LOB seam).

Lever assessment:
- Lever 1 (raw-shape/preparation cache reuse on the uncached `ToDataReader` path via `QueryPlanStore`): not safely implementable within the constraints - raw candidate publication (`QueryPlanStore.SetRaw`) is gated on `storeInCache && queryCommand.Cache`, and reader paths use `PreparedCommandCache.None`/`storeInCache: false` precisely because the command is minted per execution and disposed exactly once (AC-246-03 exact-once disposal). Publishing a normalized entry would share a disposed command across executions (ownership hazard), previously reverted. A separate raw-only immutable store would be a new subsystem, out of scope for one bounded attempt.
- Lever 2 (per-call builder/expression-rewrite allocation reduction): the benchmark's projection lambda is already compiler-cached (no capture); `EntityBuilder.Clone()`/`ReplaceParameterExpressionVisitor` are not on the measured fresh-fluent reader path in a trimmable way without risking correctness; no safe, measurable reduction identified.
Conclusion: no in-scope construction/per-row lever under the task constraints delivers a Tier B one-axis win (exact byte decrease with time within band, or time beyond band with no alloc increase) on both arms without regression. Cycle stops under the plan stop condition (no viable in-scope lever); `#246` stays OPEN. No product/test/benchmark source changes; no commit of product code.

Gates: build Debug 0W/0E, Release 0W/0E (nextorm.core); `dotnet test tests/nextorm.core.tests --filter FullyQualifiedName~Lob` 92 passed / 0 failed; status_lint rc 0. Evidence root `.pdca/T246/rv8/`.

Evidence-completion round (this file, n=2/3, no product change): the 20,485.54 us round-1 figure for `B_Nextorm_ToDataReader` (10k) is **not** a reproducible property of the tree. Re-run under the reported host state into `.pdca/T246/rv8/logs/baseline-recheck.log` (Release, pinned DB, ShortRun, single process, same filters) gave `B_Nextorm_ToDataReader` 10k = 18,193 us / 974,000 B (allocations byte-stable vs round-1 974,000 B and rv7 973,504 B; Linq2Db 10k 22,684 us / 965,680 B, Provider_Direct 10k 12,123 us / 965,416 B in the same run). An isolated single-arm cold run gave 15,509 us / 969,344 B. The full-class rv7 run (22 benchmarks, prior warmup) gave 5,620 us / 973,504 B. Time varies 5.6 -> 20.5 -> 18.2 -> 15.5 ms while allocations stay byte-stable (973,504 / 974,000 / 974,000 / 969,344 B), and the NextORM/linq2db ratio swings 1.018x -> 4.08x -> 0.80x -> 0.69x. Classification: **(b) load/first-run (warmup/ordering) artifact**, not (a). No tree change, no product edit, no candidate, no commit. Empty-arm residual (clean rv7 reference): `B_Nextorm_ToDataReader_Empty` 292.83 us / 16,048 B vs `Linq2Db_ToDataReader_Empty` 91.06 us / 5,648 B (3.216x time / 2.841x alloc) and vs `Provider_Direct_ToDataReader_Empty` 81.25 us / 5,384 B (delta +211.58 us, +10,664 B). The earlier "no in-scope lever" conclusion on the empty arm stands; the 10k arm is already at parity (1.018x) on the clean run.

## CHECK record

`not run: DO → ACT` (no candidate implemented; no CHECK ran).

## ACT record

outcome: `incomplete` · terminal: `execution` · accepted scope: `none` · issue `#246` left OPEN (no `gh issue close`).

Cause: No in-scope construction/per-row lever exists for #246 under the stated constraints (AC-246-03 exact-once `CommandReaderOwner` disposal; AC-246-04 no `Prepare()` requirement, no LOB/streaming change). Lever 1 (raw-shape/preparation reuse on `PreparedCommandCache.None` reader paths) is blocked by the exact-once disposal contract — sharing a minted-then-disposed command is an ownership hazard. Lever 2 (per-call builder/expression-rewrite trim) has no safe measurable trim on the fresh-fluent reader path. Therefore no Tier A/B candidate was produced.

Completed work: `Measurement first` baseline re-captured on the current tree (HEAD 5c162b06, F7 retained) on the pinned DB into `.pdca/T246/rv8/logs/baseline-round1.log` (exit 0, 9 reader arms); evidence-completion re-run into `.pdca/T246/rv8/logs/baseline-recheck.log`; attribution (fixed term dominates: ~10.86 KB fixed gap vs provider-direct; per-row overhead ~0; prepared 10k allocates only ~7.76 KB less than fresh across 10k). Build Debug/Release 0W/0E; `dotnet test tests/nextorm.core.tests --filter FullyQualifiedName~Lob` 92 passed / 0 failed; status_lint rc 0. No product/test/benchmark source change; no commit of product code.

Remaining work (unmet/unverified): AC-246-01 global ratios unmet (empty arm 3.216× time / 2.841× alloc vs linq2db; 10k arm already at parity 1.018× time / 1.008× alloc on the clean rv7 run); AC-246-02 measurement rules satisfied for the rounds captured; AC-246-03 mandatory correctness/evidence partly unverified (no candidate seam, no benchmark/gate row added, no cross-provider streaming report); AC-246-04 constraints honored (no LOB change, no Prepare() requirement, no dialect hardcode). Residual to be addressed by a future design change (raw-only immutable store / seam redesign) under a new authorized cycle — not by this attempt.

Residual (from frozen rv7, same tree family, single process, clean host): empty arm `B_Nextorm_ToDataReader_Empty` 292.83 us / 16,048 B vs `Linq2Db_ToDataReader_Empty` 91.06 us / 5,648 B (3.216× time / 2.841× alloc) and vs `Provider_Direct_ToDataReader_Empty` 81.25 us / 5,384 B (+211.58 us / +10,664 B); 10k `B_Nextorm_ToDataReader` 5,619.81 us / 973,504 B vs `Linq2Db_ToDataReader` 5,519.80 us / 965,632 B (1.018× / 1.008×); per-row overhead ≈0; construction share ≈2.78–2.84 KB per call (prepared-vs-fresh across 10k), i.e. a ceiling well below the 10.4 KB empty gap.

Measurement-quality statement: the two rv8 measurement rounds are environment-unstable and not evidence-grade — round-1 (`.pdca/T246/rv8/logs/baseline-round1.log`) 10k NextORM 20,485.54 us / 974,000 B vs linq2db 5,020.74 us / 965,680 B; recheck (`.pdca/T246/rv8/logs/baseline-recheck.log`) 10k 18,193 us vs linq2db 22,684 us; isolated cold single arm 15,509 us. Allocations byte-stable (973,504 / 974,000 / 974,000 / 969,344 B) while times swing 5.6–20.5 ms on an unchanged tree, under host load (VBCSCompiler 78–201%, 7 MSBuild nodes). No ratio in those rounds is claimable as a tree property; the frozen rv7 baseline (`.pdca/T246/rv7/logs/baseline-full.log`, single process, clean host) remains the residual basis. `n=3/3` stays unspent, unused.

## Revision history

`none`

## Defect history

Carried by reference from prior cycles `.pdca/status/T246-raw-datareader-1.md` and `.pdca/status/T246-raw-datareader-2.md` (read-only). No new product defect key for `N=3`.

Non-defect note (environment/measurement quality, not a defect): rv8 rounds 1–2 were load-compromised — host under load (VBCSCompiler 78–201%, 7 MSBuild nodes); time swings 5.6–20.5 ms on an unchanged tree while allocations stay byte-stable (973,504 / 974,000 / 974,000 / 969,344 B); ratios in those rounds are not claimable as tree properties. Frozen rv7 baseline (`.pdca/T246/rv7/logs/baseline-full.log`, single process, clean host) remains the residual basis. No fix applied, no escalation.

## Changed files

- `.pdca/status/T246-raw-datareader-3.md` → modified this cycle (PLAN r1 + resumption; DO result; CHECK not run; ACT finalized `incomplete`; evidence root rv8).
- `.pdca/T246/rv8/logs/baseline-round1.log` → baseline benchmark log (evidence, untracked).
- `.pdca/T246/rv8/logs/baseline-recheck.log` → evidence-completion re-run (untracked, no product change).
- No product/test/benchmark source changes. No commit of product code.

## Git state

`status path: .pdca/status/T246-raw-datareader-3.md — modified this cycle, tracked (durable on disk; commit pending in this ACT)`; branch `1.0.9-rc5`, baseline `5c162b06`. No product paths modified. Untracked `docs/assets/diagrams/selectwheremin-current.svg` and `docs/superpowers/specs/2026-11-01-query-components-proof-design.md` are foreign and untouched. Evidence root `.pdca/T246/rv8/` untracked, not committed. No commit of product code prior to this ACT; status file committed in this ACT.

## Checkpoint

- disposition: `paused` (user stop at `DO→ACT` boundary; not a verdict, not an ACT STOP)
- resume point: ACT for #246 at the `DO→ACT` exit, finalizing `incomplete` with the measured residual (no in-scope construction/per-row lever under the constraints), issue #246 left open; OR, only if the out-of-lane design decision on `G1-SCF-01` (fluent/expression construction cost) yields a concrete in-scope lever, resume DO at attempt `n=2/3` with that lever and the same ABBA gate.
- frozen inputs to reuse (do not re-measure, do not re-derive):
  - baseline (same session, Release, pinned DB `/tmp/nextorm-bench/test.db`, ShortRun, one process; log `.pdca/T246/rv7/logs/baseline-full.log`, run exit 0): `B_Nextorm_ToDataReader` 10k 5619.81 us / 973504 B; linq2db 10k 5519.80 us / 965632 B (1.018× time / 1.008× alloc); empty NextORM 292.83 us / 16048 B vs linq2db 91.06 us / 5648 B (3.216× / 2.841×); provider-direct 10k 4956.45 us / 965368 B, empty 81.25 us / 5384 B; fixed-term Δalloc 10664 B vs provider-direct; per-row overhead ~0 (0.533 vs 0.488 us/row, 95.75 vs 96.00 B/row).
  - evidence log path: `.pdca/T246/rv7/logs/baseline-full.log`.
  - plan: verbatim in `## PLAN record` (r1).
  - acceptance criteria: AC-246-01..04 (unchanged).
  - constraints: branch `1.0.9-rc5`, no push/merge/tag/`collection/*`, retained `F7` fix and control arms, untouched `docs/assets/diagrams/selectwheremin-current.svg`.
- what was done: baseline + attribution only.
- what was not done: no candidate implemented, no tests/streaming report, no CHECK, no commit of product code, no `gh issue close`, `#267`/`#311` not started.

## Progress log

append-only, one row per event, never edited retroactively:

2026-11-01 | PLAN | revision r=1 | attempt n=2/3 | status file created with frozen plan recorded verbatim; DO started | `.pdca/status/T246-raw-datareader-3.md`, evidence root `.pdca/T246/rv7/`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | baseline captured (Release, pinned DB, ShortRun): NextORM 10k 5619.81 us/973504 B vs linq2db 5519.80 us/965632 B; empty 292.83 us/16048 B vs linq2db 91.06 us/5648 B; provider-direct 10k 4956.45 us/965368 B, empty 81.25 us/5384 B; fixed term dominates (Δalloc 10664 B) | `.pdca/T246/rv7/logs/baseline-full.log`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | attribution: per-row overhead ~0 (0.533 vs 0.488 us/row, 95.75 vs 96.00 B/row); prepared arm allocates only ~2.78 KB less than fresh over 10k; no in-scope lever under constraints (seam+recipe would require publishing disposed commands; LobDataReader trim addresses single instance) → DO stops, incomplete, no code change | `.pdca/T246/rv7/logs/baseline-full.log`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | DO completed: incomplete/blocked report for ACT; no CHECK ran; issue #246 stays OPEN; no commit | `.pdca/status/T246-raw-datareader-3.md`
2026-11-01T06:38:16Z | DO→ACT | revision r=1 | attempt n=2/3 | lane paused by user at DO→ACT boundary; no product change to revert | `.pdca/status/T246-raw-datareader-3.md`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | resumed per dispatch; PLAN r1 resumption recorded (shared header + Block 1); evidence root rv8 | `.pdca/status/T246-raw-datareader-3.md`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | Measurement first baseline re-captured (Release, pinned DB, ShortRun, single process): B_Nextorm_ToDataReader 10k 20485.54 us / 951.17 KB; Linq2Db 10k 5020.74 us / 943.05 KB; Provider_Direct 10k 4833.08 us / 942.79 KB; empty NextORM 272.95 us / 16.16 KB vs Linq2Db 94.95 us / 5.56 KB; Provider_Direct empty 112.46 us / 5.3 KB; prepared 10k 8105.04 us / 943.41 KB | `.pdca/T246/rv8/logs/baseline-round1.log`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | attribution: fixed term dominates (delta alloc ~10.86 KB vs provider-direct); per-row overhead ~0; Lever 1 not safely implementable (ownership hazard under AC-246-03); Lever 2 no safe measurable reduction → no candidate, incomplete | `.pdca/T246/rv8/logs/baseline-round1.log`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | gates: build Debug/Release 0W/0E; Lob tests 92 passed; status_lint rc 0; no commit of product code; #246 stays OPEN | `.pdca/status/T246-raw-datareader-3.md`
2026-11-01 | DO | revision r=1 | attempt n=2/3 | evidence-completion round (no product change): 20.5 ms round-1 figure for B_Nextorm_ToDataReader (10k) re-run under host state into .pdca/T246/rv8/logs/baseline-recheck.log = 18,193 us / 974,000 B (alloc byte-stable vs round-1 974,000 B and rv7 973,504 B); isolated cold run 15,509 us / 969,344 B; rv7 full-class 5,620 us / 973,504 B. Classification (b) load/first-run (warmup/ordering) artifact; time 5.6->20.5->18.2->15.5 ms with allocs byte-stable and ratio 1.018x->4.08x->0.80x->0.69x. No candidate, no commit; #246 stays OPEN | `.pdca/T246/rv8/logs/baseline-recheck.log`
2026-11-01T08:54:10Z | ACT | revision r=1 | attempt n=2/3 | finalized incomplete (DO→ACT): no in-scope lever (Lever 1 ownership hazard under AC-246-03; Lever 2 no safe trim); issue #246 left OPEN; no gh issue close; n=3/3 unspent; rv8 rounds load-compromised, rv7 residual basis | `.pdca/status/T246-raw-datareader-3.md`


## G1-SCF-01 diagnostic input

- CTE construction (`BuildCte`): 9,024.74 / 9,024.74 / 9,024.67 B/construction; expression-hoist control: −52.7% / −53.3% (9,025.3 / 9,145.3 baseline vs 4,272 B/query hoisted).
- T267 construction prefix: inner chain 13.28–13.52 KB/construction; full chain 18.61–18.88 KB/construction.
- Verdict: LIMITED; the CTE delta is not proven to come from expression-tree construction alone (static, capture-free shape); see limitations in GitHub comment.
- Local evidence (not a public link): `.pdca/G1-SCF-01/RESULTS.md` and `.pdca/G1-SCF-01/diag-20261101/`.
- GitHub comments: #267 https://github.com/AlexeyShirshov/nextorm/issues/267#issuecomment-6555409789; #311 https://github.com/AlexeyShirshov/nextorm/issues/311#issuecomment-6555410226; #246 https://github.com/AlexeyShirshov/nextorm/issues/246#issuecomment-6555410684.
- Resume meaning: the out-of-lane G1-SCF-01 decision now yields an investigation-grade in-scope lever (reuse of expression trees), satisfying the checkpoint condition; this is not an approved design. Resume DO at n=2/3 only after design approval, retaining the same ABBA gate.
