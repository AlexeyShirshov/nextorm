# Cycle status: T246 / cycle 3

## Current state

- task: `T246` · form: `cycle` · specialized skill: `pdca-dotnet` (`coder+.NET`, inherits `pdca-coder`)
- mode: `autonomous`
- disposition: `paused` (user stop, not a verdict)
- cycle: `N=3` · revision: `r=1` · attempt: `n=2/3` (next attempt; 2 of 3 attempts remain — `n=2/3`, `n=3/3` in reserve; prior `n=1/3` spent in `N=2` on reverted `OpenResultReader` + lean `LobDataReader` candidate, empty arm +17.3 %; this DO made no product change and consumed no correcting attempt)
- scope: `#246` only — `ToDataReader` raw reader vs linq2db on fresh-fluent path (10k + empty arms), construction-cost lever under `G1-SCF-01`; status path `.pdca/status/T246-raw-datareader-3.md`
- goal: measured performance improvement on `ToDataReader` (Tier A global ratios or Tier B one-axis local win) committed on `1.0.9-rc5`, or measured residual proof recorded `incomplete` with issue left open — §PLAN record and §Original task authoritative
- phase: `DO→ACT` · record: `completed` · outcome: `paused (user stop at DO→ACT boundary; not a verdict)`
- source: issue `#246` (`AlexeyShirshov/nextorm`), milestone `1.0.9-rc5`; re-entry cycle `N=3` after `N=2` terminal `incomplete` (`.pdca/status/T246-raw-datareader-2.md`, read-only) · context refs: caller dispatch "three residuals of the closed rc5 collection; item #246"; retained wins `84e98272`, `2ea3ef45`, `F7` in HEAD `04d35452`
- criteria: `AC-246-01..04` — exact text in §PLAN record `r1` (source: caller dispatch), not retyped here
- evidence root: `.pdca/T246/rv7/`

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

## Approval record

`not applicable: autonomous mode` — no ordinary plan approval was required or invented for this cycle.

## DO result

No product code change was made (no candidate implemented). `Measurement first` baseline captured on the current tree (`HEAD 04d35452`, F7 retained) on the pinned DB; attribution performed; conclusion: no in-scope construction/per-row lever exists under the task constraints that can produce a Tier B one-axis win without regression, so the cycle stops under the plan's stop condition (no viable in-scope lever). Build baseline `0 Warning(s) 0 Error(s)` (unchanged tree). Evidence root `.pdca/T246/rv7/`.

Baseline (same session, pinned DB `/tmp/nextorm-bench/test.db`, Release, ShortRun, log `.pdca/T246/rv7/logs/baseline-full.log`; exact bytes from BDN `// GC:` lines):
- `B_Nextorm_ToDataReader` (10k): 5619.81 us, 973504 B.
- `Linq2Db_ToDataReader` (10k): 5519.80 us, 965632 B.
- `Provider_Direct_ToDataReader` (10k): 4956.45 us, 965368 B.
- `B_Nextorm_ToDataReader_Empty`: 292.83 us, 16048 B.
- `Linq2Db_ToDataReader_Empty`: 91.06 us, 5648 B.
- `Provider_Direct_ToDataReader_Empty`: 81.25 us, 5384 B.
- `A_Nextorm_Prepared_ToDataReader` (10k, prepared cmd): 6141.90 us, 947904? (947.91 KB) — see log.

Ratios NextORM/linq2db: 10k time 1.018×, alloc 1.008×; empty time 3.216×, alloc 2.841×. Global target (≤1.00× time, ≤1.20× alloc) unmet on time for 10k and both axes for empty.

Attribution (fixed vs per-row, current tree):
- Fixed (empty) NextORM vs provider-direct: Δtime 211.58 us, Δalloc 10664 B.
- Per-row (10k−empty)/10000: NextORM 0.5327 us/row, 95.75 B/row; provider-direct 0.4875 us/row, 96.00 B/row. Per-row overhead is near zero; the empty/fixed term dominates.
- Prepared arm (`A_Nextorm_Prepared_ToDataReader`) allocates only ~2.78 KB less than the fresh arm across 10k rows, so query construction itself is a small share of the fixed cost; the bulk of the 10.6 KB fixed gap is preparation/execution overhead (command, plan, reader, LOB seam).

Candidate assessment:
- Prior candidate (N=2) already tried routing `ToDataReader` through `OpenResultReader` (removing the LOB seam on SQLite): empty time −13.4 % but allocations flat and 10k time inside noise; empty arm still +17.3 % in the candidate-bound ABBA. Combining with the raw-recipe/cache lever would require publishing a prepared command into `QueryPlanStore` for a reader path whose command is disposed per execution (ownership hazard, previously reverted in r=4), or a cached SQL/plan descriptor that is not implemented and would be a new subsystem — out of scope for a single bounded cycle and not safely doable under `CommandReaderOwner` exact-once disposal (AC-246-03) and the no-`Prepare()` constraint (AC-246-04).
- Trimming `LobDataReader` further (already AggressiveInlining + cancellation skip + F6/F7 fixes) addresses only the single wrapper instance, not the 10.6 KB fixed gap.
- Therefore no in-scope construction/per-row lever exists under the constraints that can deliver a Tier B win (exact byte decrease with time within band, or time beyond band with no alloc increase) on both arms without regression. The cycle stops per plan §9 Risk 3 / terminal rule 3.

Correctness/infra not re-run (no code change); standing baseline build 0 W/0 E recorded above; benchmark arms retained unchanged. Issue #246 stays OPEN.

## CHECK record

`not run: DO → ACT` (no candidate implemented; no CHECK ran).

## ACT record

`pending` — DO returned an incomplete/blocked report (no in-scope lever under constraints) for ACT to finalize; no CHECK ran. Cause, completed work, remaining work and residual are recorded in §DO result above. Issue #246 stays OPEN. No commit.

## Revision history

`none yet` — append-only.

## Defect history

Carried by reference from prior cycles `.pdca/status/T246-raw-datareader-1.md` and `.pdca/status/T246-raw-datareader-2.md` (read-only). No new defect keys recorded yet for `N=3`.

## Changed files

- `.pdca/status/T246-raw-datareader-3.md` → created/updated this cycle (status record).
- `.pdca/T246/rv7/logs/baseline-full.log` → baseline benchmark log (evidence, untracked).
- No product/test/benchmark source changes. No commit.

## Git state

`status path: .pdca/status/T246-raw-datareader-3.md — created in this cycle, untracked before this commit (durable on disk, not committed)`; branch `1.0.9-rc5`, baseline `04d35452`. No product paths modified. Untracked `docs/assets/diagrams/selectwheremin-current.svg` is foreign and untouched. Evidence root `.pdca/T246/rv7/` untracked, not committed.

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
