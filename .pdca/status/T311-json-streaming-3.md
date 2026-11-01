# Cycle status: T311-json-streaming / cycle 3

## Current state

- task: `T311-json-streaming` · form: `cycle` · specialized skill: `pdca-coder+.NET`
- mode: `autonomous`
- cycle: `N=3` · revision: `r=3` · attempt: `n=3/3`
- scope: pre-registered `#311` JSON streaming oracle execution on retained tree `2ea3ef45` (no product edit) · status path: `.pdca/status/T311-json-streaming-3.md`
- goal: execute the frozen oracle and emit an honest per-size verdict for `1k/10k/100k`, plus `AC-311-01..03` assessment and routing — the §PLAN record and §Original task are authoritative
- phase: `ACT` · record: `completed` · outcome: `incomplete` · accepted_scope: `none` · terminal: `execution (attempt budget exhausted at n=3/3)` · `#311` OPEN
- source: caller brief `T311-json-streaming-3` · context refs: `docs/specs/performance/json-ratio-oracle-2.md` (`b6f9925a`), `.pdca/status/T311-json-streaming-2.md`, `.pdca/status/T246-raw-datareader-3.md:126-141`
- criteria: `AC-311-01..03` (issue) and `AC-CYCLE-01..06` (cycle) — exact text in §PLAN record `r1` (source: caller brief + `docs/specs/performance/json-ratio-oracle-2.md`), not retyped here

## Original task

````markdown
New cycle for `#311` (JSON streaming, `#247` follow-up). Branch `1.0.9-rc5`, HEAD `9e7fb4b3`. No push, no merge, no tag, no `collection/*` branch. Auto-commit authorized for touched paths only, `#N` + evidence pointer.

## Read before planning
- `docs/specs/performance/json-ratio-oracle-2.md` — the **pre-registered** protocol for this item (committed `b6f9925a`). It binds you: limits `1k ≤ 1.20`, `10k ≤ 1.00`, `100k ≤ 0.95`, `10k` allocation `≤ 446,095 B/op` exact; baseline-first on the retained tree `2ea3ef45` **before any product edit**, otherwise 0 valid pairs; two paired ABBA rounds per session, `N=2` (the old `N=9` is explicitly no longer mandated — it produced 0 valid pairs); statistic `d_i = ln(nextorm/linq2db)` with `exp(mean(d))`, both rounds must agree in direction, never average discordant rounds; PASS = every round ≤ limit, FAIL = both rounds above, else INCONCLUSIVE; noise band `s=0.30`, instability factor >1.30 → INCONCLUSIVE. Frozen after registration: rule changes need PLAN + rv supersession, plus the capture-order rule, the `cases.json` schema (§10), 7 acceptance controls within 240 s, the rejection list, exit codes 0/1/2.
- `.pdca/status/T311-json-streaming-2.md` — `n=2/3` spent, `n=3/3` unspent, `ACT` finalized `incomplete`, and the PLAN r1 stop condition that already fired on purpose (construction too small to close the 1k gap). Its decomposition stands: removable per-call construction+preparation at 1k = 4,451 B (8.45% alloc) / 12,610 ns (2.17% time); at 10k = 4,434 B (1.07%), time in noise; residual 1k gap 8.9% (`1.089×`).
- Issue `#311` body and `AC-311-01..03` (verbatim in `.pdca/status/T246-raw-datareader-3.md:127-141`).

## What this cycle is, and what it is not
This is the **evidence run the spec pre-registers and that has never been executed** — not a new lever attempt, so it does not consume `n=3/3`, and no product edit is expected. `#311` cannot be honestly closed or honestly failed until its own oracle has run: the stored point estimates (`10k 0.9159/0.9034`, `1k 1.089`, `100k 0.900`) were never produced by the registered protocol and cannot be promoted into a verdict.

## DO
1. Build the missing collector/gate exactly as §10/§8/§9 prescribe — `t311_json_gate.py` was never created; `.pdca/scripts/perf/` currently holds only the `t247_*` scripts. Implement the frozen `cases.json` schema, the 7 acceptance controls within their time budget, and exit codes `0/1/2`. Do not silently change the schema; if the spec is internally inconsistent, report it and route to PLAN instead of improvising.
2. Execute the registered protocol: two paired ABBA rounds per session on the **retained tree, unmodified** (baseline-first), pinned SQLite DB `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, single process, no concurrent load, sizes 1k/10k/100k, arms `WriteJson_Array_Dto` vs `Linq2Db_ToList_Json`, HEAD/DB fingerprint per round. Exact existing command family: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson*` (add `--no-build --exporters json csv --artifacts <dir>`); freeze and record the exact per-arm filters you use in the case file.
3. Apply the registered statistic and emit the verdict per size (PASS / FAIL / INCONCLUSIVE) with the arithmetic, the two per-round ratios, the direction-agreement check, and the noise/instability diagnostics. Do not invent a pass the spec does not define, and do not cite `AC-311-04` — no such criterion exists in this repo.
4. If a size comes out INCONCLUSIVE on the registered protocol, that is a legitimate verdict: record it, do not add rounds to force a pass, and do not switch to an unregistered statistic.

## CHECK → ACT
- Independent CHECK against `AC-311-01..03` and the oracle's own verdict rule.
- If every size PASSes → close `#311` on an accepted scope, citing the oracle receipt (cases file path + commit).
- If any size FAILs or is INCONCLUSIVE → keep `#311` **open**, record the exact per-size residual, and state plainly which criterion is unmet and what would be needed (a core-side throughput lever, i.e. a separate design/PLAN). No speculative product edit to move a number.
- Post the verdict as a comment on `#311` (`gh issue comment`) with the per-size table and the evidence path; milestone unchanged; nothing pushed.
- Commit only the spec-derived collector, the case/result artifacts that belong under versioned `docs/specs/**`, and the status record: `#311` + evidence pointer, touched paths only, `status_lint.py` rc 0, CRLF.

Containers: none needed (SQLite); if a provider container is required use `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` after a socket check, one at a time.
Never touch or stage the untracked user files `docs/assets/diagrams/selectwheremin-current.svg` and `docs/superpowers/specs/2026-11-01-query-components-proof-design.md`.

Hard stop on evidence loops: one evidence-completion round per criterion per revision, then take the honest verdict and route it.
````

## PLAN record

### r1

````markdown
# PLAN — `#311` (`T311-json-streaming-3`, PLAN `r=1`)

## 1. Form, specialized skill, status path, and entry scope

- **Form:** `cycle` (ordinary cycle)
- **Specialized skill (overlay):** `coder+.NET` (`pdca-coder` + `pdca-dotnet`, because the collector/gate wraps and invokes the .NET 10 BenchmarkDotNet suite `benchmarks/nextorm.benchmark` in this repository).
- **One-line form rationale:** The measurement protocol, collector schema, 7 acceptance controls, 3 JSON sizes, paired ABBA schedule (`N=2` rounds), and decision arithmetic are already fully specified in the frozen oracle `docs/specs/performance/json-ratio-oracle-2.md` (`b6f9925a`), bounding the work to one deterministic collector implementation and one two-round evidence execution with no product code change.
- **Cycle identity and status path:**
  - **Status path:** `.pdca/status/T311-json-streaming-3.md` (new cycle `N=3` for `#311`, since `.pdca/status/T311-json-streaming-2.md` finalized in `ACT` with terminal outcome `incomplete`).
  - **Cycle counters:** `N=3`, `r=1`, `n=1/3` (for this evidence cycle's own execution budget).
  - **Lever-budget continuity note for ACT:** In the prior lever cycle (`T311-json-streaming-2.md`), `n=2/3` was spent and `n=3/3` remained unspent when the PLAN `r=1` stop condition fired. Because this cycle is the pre-registered oracle execution on the retained product tree (`2ea3ef45`) and **not** a new product-code lever attempt, it does not consume the unspent `n=3/3` product-lever attempt from `T311-json-streaming-2`.
- **Re-anchor snapshot:** Branch `1.0.9-rc5`, HEAD `9e7fb4b3` (ancestor `2ea3ef45` holds the retained product/benchmark tree; commits `2ea3ef45..9e7fb4b3` are docs/status-only: `b6f9925a`, `04d35452`, `9e7fb4b3`).
- **Evidence root & contract revision:**
  - Local cycle evidence root: `.pdca/T311/rv4/`
  - `contract_rv`: `"rv4"`
  - Versioned spec/case/result receipt path under `docs/specs/**`: `docs/specs/performance/t311-oracle-rv4/` (alongside `docs/specs/performance/json-ratio-oracle-2.md`).
- **Entry scope:** Whole cycle goal — implement the missing `t311_json_gate.py` collector/gate conforming to `docs/specs/performance/json-ratio-oracle-2.md` (§8/§9/§10), execute the two-round paired ABBA measurement session on the unmodified retained product tree, compute the per-size verdicts (`1k`, `10k`, `100k`) and 7/7 control checks, and route `#311` (close only if all sizes PASS; keep open with exact residuals otherwise) with a GitHub issue comment and scoped commit.

## 2. Goal, requested vs delivered result, and cycle scope vs issue closure

### Requested result & delivered result (identical object)

- **Requested result:** A conforming `t311_json_gate.py` collector/gate (`docs/specs/performance/json-ratio-oracle-2.md` §8/§9/§10), a completed 2-round paired ABBA measurement session on the unmodified retained tree (`2ea3ef45` product identity) producing schema-valid `cases.json` and verdict artifacts for 1k/10k/100k and the 7 acceptance controls, an honest per-size verdict (`PASS` / `FAIL` / `INCONCLUSIVE`) and `AC-311-01..03` assessment, the resulting `#311` issue routing (`closed` only if all sizes `PASS`; kept `open` with exact residuals if any size is `FAIL` or `INCONCLUSIVE`) posted via `gh issue comment`, and a scoped commit of versioned artifacts + status record.
- **Delivered result:** The exact same collector/gate script, 2-round paired ABBA `cases.json` and verdict artifacts, `AC-311-01..03` evaluation, `#311` comment + state routing, and scoped commit.

### Explicit distinction: Cycle Deliverable vs. Issue `#311` Closure

1. **Cycle deliverable (`AC-CYCLE-01..06`):** Execute the pre-registered oracle faithfully and emit its honest verdict.
   - If the collector is valid, all 7 acceptance controls pass within 240 s/round, both ABBA rounds execute validly on the retained tree without rejection-list violations, and a size's oracle verdict comes out `FAIL` or `INCONCLUSIVE`, **this PDCA cycle itself succeeds (`done`)** because the requested result of this cycle is the valid oracle execution, verdict determination, and routing.
   - The cycle fails (`incomplete`) only if the oracle run itself is invalid (e.g. schema violation, rejection-list trigger, missing rounds, control timeout/regression, or unexecuted protocol).
2. **Issue-level criteria (`AC-311-01..03`) and caller's stricter closing rule:**
   - CHECK evaluates and reports both the cycle criteria (`AC-CYCLE-01..06`) and the verbatim issue-level criteria (`AC-311-01..03`).
   - Although `AC-311-01` contains a local-acceptance clause permitting task closure on a one-axis local win with a recorded residual gap, **the caller's brief imposes a stricter closing rule for this cycle**: close `#311` **only if every size (`1k`, `10k`, `100k`) PASSes** the registered oracle (including `10k` allocation `≤ 446,095 B/op` exact and 7/7 controls `≤ 1.20×` within 240 s). If any size is `FAIL` or `INCONCLUSIVE`, `#311` **must remain OPEN**, recording the exact per-size ratios, residuals, which criterion (`AC-311-01..03`) is unmet, and that closing the remaining gap requires a separate core-side throughput lever / design cycle.

## 3. Acceptance Criteria

### Part A — Issue `#311` criteria

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

**(b) `AC-311-03` bullet 1 (prior-cycle lever attribution):** The first bullet of `AC-311-03` (*"A per-row throughput lever for 10k is attributed, implemented and measured; operation-count change is shown"*) had its product-code lever attributed and committed in the prior cycle (`2ea3ef45`, pre-encoded `JsonEncodedText` property-name writes, with attribution and operation-count evidence recorded in `.pdca/T311/rv2/`). Because this cycle makes no product-code edit, it contributes only the *measurement under the registered oracle* to that bullet; CHECK therefore evaluates and reports that bullet as an issue-level item carried from prior evidence (`2ea3ef45` / `.pdca/T311/rv2/`) combined with this cycle's oracle measurement, and does not restate or claim the lever implementation as a fresh deliverable of this cycle.

### Part B — Cycle Execution Criteria (derived from the caller's brief and `docs/specs/performance/json-ratio-oracle-2.md`)

- **`AC-CYCLE-01` (Collector & frozen §10 schema compliance):** `.pdca/scripts/perf/t311_json_gate.py` is created and implements `docs/specs/performance/json-ratio-oracle-2.md` §8/§9/§10 without mutating `t247_*` scripts or `T247` frozen outcomes. Every written `cases.json` (`baseline/round-{1,2}/acceptance/cases.json` and `baseline/round-{1,2}/public-json/cases.json`, plus candidate counterparts if emitted) contains the exact §10 top-level keys (`schema_version, task, contract_rv, spec_sha256, collector_sha256, snapshot, source_identity, session_id, baseline_generation, round, generated_utc, db_sha256, environment, cases[]`) and exact §10 per-case keys (`case_id, kind, implementation, parameters, pair_index, order, job_fingerprint, mean_ns, allocated_b, raw_artifacts[], command, exit_code, elapsed_seconds`), conforming to the schema rulings in §4 below. Negative condition: any missing/renamed §10 key or invented `linq2db` control case fails the criterion.
- **`AC-CYCLE-02` (Retained-tree identity & 2-round paired ABBA capture):** Two paired rounds (`round-1`, `round-2`) are captured in a single session with one benchmark process at a time and no concurrent build/container load, with `git diff 2ea3ef45 -- src/ benchmarks/` empty (proving unmodified product and benchmark code from `2ea3ef45`). Implementation order alternates by round (`ABBA` across `round-1` and `round-2`) and is recorded in `order`. All three sizes (`1000`, `10000`, `100000`) and both arms (`WriteJson_Array_Dto`, `Linq2Db_ToList_Json`) yield at least 2 paired observations per size (`N=2` rounds), with zero rejection-list violations (§7: no missing/zero/duplicate samples, non-finite values, mismatched parameters/jobs, moving HEAD/DB identity within a round, or post-product-edit baseline).
- **`AC-CYCLE-03` (7/7 acceptance controls within 240 s budget):** Both rounds capture the exact 7 acceptance controls (`implementation="nextorm"` only, pinned DB `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`), excluding the 8th `[BenchmarkCategory("acceptance")]` method `SqliteBenchmarkCsvFixedCost.Nextorm_WriteCsv_3Col` and excluding SQL Server classes. Every acceptance round completes in `≤ 240.0` seconds total and every control satisfies `final_round_mean_ns <= 1.20 * baseline_round_mean_ns`. Negative condition: any control exceeding `1.20×` or any acceptance round exceeding `240 s` fails the gate.
- **`AC-CYCLE-04` (Registered statistic, verdict rule, and exit codes `0/1/2`):** For each size (`1k`, `10k`, `100k`), `t311_json_gate.py` computes per-round `d_i = ln(nextorm_mean_ns_i / linq2db_mean_ns_i)`, per-round ratios `exp(d_i)`, overall `exp(mean(d))`, direction-agreement check against the size limit (`1k ≤ 1.20`, `10k ≤ 1.00`, `100k ≤ 0.95`), `10k` exact allocation check (`allocated_b <= 446095` B/op with `0 B` tolerance), and noise/instability diagnostics (`s = 0.30`; ratio differing from median by factor `> 1.30` or first-half/last-half geometric mean factor `> 1.30` → `INCONCLUSIVE`; diagnostic `min(nextorm)/min(linq2db)` reported only and never used to promote `FAIL`/`INCONCLUSIVE`). Discordant rounds are never averaged into a `PASS`. Exit codes strictly follow §8: `0` = valid `PASS`, `1` = `FAIL`, `2` = `INCONCLUSIVE` or invalid input (with structured reasons distinguishing invalid input from measurement `INCONCLUSIVE`). Direction agreement is defined strictly as `sign(d_1) == sign(d_2)` where `d_i = ln(nextorm_mean_ns_i / linq2db_mean_ns_i)` (equivalently, both per-round ratios `exp(d_1)` and `exp(d_2)` lie on the same side of `1.0`, with `sign(0) == 0`), and is reported as a required diagnostic alongside — never instead of — the per-size decision rule (`PASS`: every round's ratio `<= limit`; `FAIL`: both rounds' ratios `> limit`; otherwise `INCONCLUSIVE`); no direction-agreement check may promote a `FAIL`/`INCONCLUSIVE` or demote a `PASS`.
- **`AC-CYCLE-05` (Honest verdict routing & `#311` issue update):** No extra rounds are added to force a pass and no unregistered statistic is used.
  - If all three sizes (`1k`, `10k`, `100k`), `10k` allocation (`≤ 446,095 B/op`), and `7/7` controls `PASS` (gate exit code `0`): close `#311` citing the versioned oracle receipt and commit.
  - If any size is `FAIL` (exit code `1`) or `INCONCLUSIVE` (exit code `2`): keep `#311` **OPEN**, record the exact per-size per-round ratios, geometric means, allocation bytes, and residual gaps to the global limits, state plainly which of `AC-311-01..03` is unmet and why (requiring a separate core-side throughput lever / design cycle), and post the complete per-size verdict table and evidence paths on `#311` via `gh issue comment` without changing the milestone.
- **`AC-CYCLE-06` (Repository & status hygiene):** Versioned oracle case/verdict receipts under `docs/specs/performance/t311-oracle-rv4/` and `.pdca/status/T311-json-streaming-3.md` are committed on `1.0.9-rc5` with `#311` and the evidence pointer in the commit message; CRLF line endings are preserved; `.pdca/scripts/status_lint.py` exits `0`; untracked user files (`docs/assets/diagrams/selectwheremin-current.svg`, `docs/superpowers/specs/2026-11-01-query-components-proof-design.md`) and `.pdca/` gitignored paths remain untouched and unstaged; no push, merge, tag, or `collection/*` branch is created.

## 4. Measurement method & rulings on `json-ratio-oracle-2.md` under-specifications

No rule in `docs/specs/performance/json-ratio-oracle-2.md` is contradicted or changed; therefore **no `rv` supersession of `json-ratio-oracle-2.md` is required**. The 7 known under-specifications are resolved as deterministic field encodings inside the existing §10 schema keys:

1. **Under-specification 1 (`size` field in §10):** §10 defines `parameters` on each `Case` object. Encode the benchmark parameter inside `parameters` as `{"RowCount": 1000}` (or `10000`, `100000`), and include the size in `case_id` (e.g., `SqliteBenchmarkWriteJson.WriteJson_Array_Dto:RowCount=1000`). No new top-level or case-level key outside §10 is needed.
2. **Under-specification 2 (`HEAD` fingerprint in §10):** §10 defines `snapshot` and `source_identity` at the top level. Record the exact `git rev-parse HEAD` SHA (`9e7fb4b3...`) in `snapshot`, and record a structured object in `source_identity` containing `{"head_sha": "<full_head_sha>", "retained_product_sha": "2ea3ef45...", "product_tree_clean_vs_retained": true, "product_tree_sha256": "<tree_hash>"}`. Verify it does not move within a round.
3. **Under-specification 3 (Frozen per-arm filter strings in §10):** §10 defines `command` and `parameters` on each `Case`. Record the exact invoked argv/command string in `command` and include `"filter": "<exact_filter>"` inside `parameters` (and in `environment.frozen_filters` at the top level). The frozen per-arm filter strings are:
   - **NextORM JSON arm:** `*SqliteBenchmarkWriteJson.WriteJson_Array_Dto*`
   - **linq2db JSON arm:** `*SqliteBenchmarkWriteJson.Linq2Db_ToList_Json*`
   - **7 Acceptance controls (exact 7-filter list, excluding `SqliteBenchmarkCsvFixedCost.Nextorm_WriteCsv_3Col`):**
     1. `*InMemoryBenchmarkAggregates.Nextorm_Count*`
     2. `*InMemoryBenchmarkGroupBy.Nextorm_GroupByCount*`
     3. `*SqliteBenchmarkAny.Nextorm_Cached*`
     4. `*SqliteBenchmarkCachedPlan.Prepared_ToList*`
     5. `*SqliteBenchmarkCachedPlan.Cached_ToList*`
     6. `*SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param*`
     7. `*SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync*`
4. **Under-specification 4 (`raw_artifacts[]`, `command`, `exit_code`, `elapsed_seconds`):** Populate all four required §10 per-case keys directly from each BDN invocation and its exported JSON/CSV report paths.
5. **Under-specification 5 & baseline/candidate legs when no product edit occurs:** see BLOCK B item (d).
6. **Under-specification 6 (`pair_index` across rounds):** `round-1` cases carry `pair_index = 1` (one `nextorm` + one `linq2db` leg per size); `round-2` cases carry `pair_index = 2` (one `linq2db` + one `nextorm` leg per size), yielding `N = 2` paired observations per size (`i = 1, 2`).
7. **Under-specification 7 (`ε_time` in §10 local-acceptance prose):** Because this cycle makes no product edit (`Δalloc` and `Δtime` vs a prior tree are not used to claim a local-acceptance override) and the caller's brief mandates applying only the global verdict rule (§3–§4: `1k ≤ 1.20`, `10k ≤ 1.00`, `100k ≤ 0.95`, `10k alloc ≤ 446,095 B/op`), `ε_time` is unused and does not affect the verdict.
8. **Note on `NEXTORM_BENCH_DB`:** `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db` is set in the environment for all runs and its SHA-256 is recorded in `db_sha256` (governing the SQLite acceptance controls via `BenchDb.cs`, while `SqliteBenchmarkWriteJson` seeds its own per-run temp SQLite DB as designed in `2ea3ef45`).

## 5. Terminal outcome check at PLAN entry

- **Does a terminal PLAN outcome apply now?** **No.**
  - The criteria are determinate (`AC-311-01..03` + `AC-CYCLE-01..06`).
  - The measurement method, instrument (`BenchmarkDotNet` on `benchmarks/nextorm.benchmark`) and comparability basis are available and functional.
  - The spec's under-specifications are cleanly accommodated within the existing §10 fields without any contradiction.
  - The oracle has never been executed, so the result is not yet established.

## 6. Constraints, prohibitions, and assumptions

- **No product or benchmark source edits:** `src/**`, `tests/**`, and `benchmarks/**` must remain bit-identical to `2ea3ef45` (`git diff 2ea3ef45 -- src/ tests/ benchmarks/` must be empty).
- **Git & branch restrictions:** Stay on branch `1.0.9-rc5` (starting at `9e7fb4b3`). **Forbidden:** `git push`, `git merge`, `git tag`, or creating/using any `collection/*` branch. **Authorized:** auto-commit on `1.0.9-rc5` **only** for touched paths that belong in version control (`docs/specs/performance/t311-oracle-rv4/**` and `.pdca/status/T311-json-streaming-3.md`), with `#311` and the evidence pointer in the commit message. `.pdca/scripts/perf/t311_json_gate.py` and `.pdca/T311/rv4/**` live under gitignored `.pdca/` paths (`.gitignore:30-32`); the collector is additionally mirrored to a versioned path (BLOCK B item (d)2).
- **Untouchable user files:** Never touch, modify, delete, or stage `docs/assets/diagrams/selectwheremin-current.svg` and `docs/superpowers/specs/2026-11-01-query-components-proof-design.md`.
- **Execution hygiene:** Single benchmark process at a time; no concurrent container, build, or test load during benchmark capture. Build `benchmarks/nextorm.benchmark` in `Release` once before starting Round 1, then run all benchmark legs with `--no-build`. Preserve CRLF on all versioned files.
- **Prohibitions on verdict manipulation:** Do not add rounds beyond `round-1`/`round-2` to force a `PASS`. Do not average discordant rounds. Do not promote `min(nextorm)/min(linq2db)` into a verdict. Do not invent or cite `AC-311-04`. One evidence-completion round per criterion per revision maximum.

## 7. Gaps, minimal solution, risks, and stop conditions

| Gap in statement / spec | Resolution |
| --- | --- |
| §10 prose lacks explicit top-level `head_sha`, `size`, `filter` keys | Encoded inside §10's existing `snapshot`, `source_identity`, `parameters`, `command` fields (§4 Rulings 1-3); no schema change or `rv` supersession needed. |
| `.gitignore` ignores `.pdca/scripts/` while the brief says "Commit only the spec-derived collector, the case/result artifacts that belong under versioned `docs/specs/**`" | Dual placement per BLOCK B item (d)2. |
| `AC-311-01` allows local-win closure vs. the caller's brief requiring all sizes to `PASS` to close `#311` | The caller's stricter closing rule governs `#311` state in ACT; CHECK reports both sets transparently. |

### Minimal solution

1. Implement `.pdca/scripts/perf/t311_json_gate.py` (stdlib Python 3, modeled on the BDN runner/parser infrastructure in `t247_json_gate.py` but emitting the exact §10 `cases.json` schema, enforcing the 7-control filter list and 240 s timeout, executing ABBA round ordering for `WriteJson_Array_Dto` and `Linq2Db_ToList_Json`, and computing the §3/§4/§5/§8 verdict and exit codes `0/1/2`).
2. Pre-build `benchmarks/nextorm.benchmark -c Release`, verify `2ea3ef45` product/benchmark tree identity and `/tmp/nextorm-bench/test.db` hash, and run the 2-round session (`round-1` AB, `round-2` BA for JSON; paired acceptance legs per round).
3. Run the gate evaluation to produce the structured verdict report and copy the versioned receipts (`cases.json` files, verdict summary JSON/MD, and collector script) into `docs/specs/performance/t311-oracle-rv4/`.
4. Have CHECK independently verify — later phases do that; DO stops after the candidate + evidence are recorded.

### Risks and stop conditions

- **Risk 1 (Expected 1k `FAIL` or `INCONCLUSIVE`):** Prior point estimates showed `1k` around `1.089×` (within `1.20×`) and `10k` around `0.916×` (within `1.00×`), or run-to-run jitter exceeding `1.30×` or crossing `1.00×`. **Rule:** Neither `FAIL` nor `INCONCLUSIVE` is a stop or retry trigger; record the honest verdict, exit with the matching code (`1` or `2`), and keep `#311` open in ACT.
- **Stop condition 1:** Maximum one evidence-completion round per criterion per revision. Never re-run healthy benchmark rounds to shop for a better ratio.
- **Stop condition 2:** If the 7 acceptance controls exceed 240 s, record the job fingerprint and keep both rounds identical; do not extend the budget.
- **Stop condition 3:** If a genuine spec contradiction surfaces at runtime, stop and report it (do not improvise).

## 8. Plan-to-DO handoff

Carry by reference: original task verbatim; `AC-311-01..03` + `AC-CYCLE-01..06`; §10 schema rulings 1-8 with the exact filter strings; constraints/prohibitions/untouchable files; evidence root `.pdca/T311/rv4/`, versioned receipt root `docs/specs/performance/t311-oracle-rv4/`, status file `.pdca/status/T311-json-streaming-3.md`, `contract_rv = "rv4"`.

## 9. Confidence and gaps

- **Verified from supplied facts:** the spec is frozen and defines limits, `N=2` paired ABBA rounds, `d_i = ln(nextorm/linq2db)`, 7 controls within 240 s, rejection rules, exit codes `0/1/2`, §10 fields. `2ea3ef45` is an ancestor of `9e7fb4b3`. `/tmp/nextorm-bench/test.db` exists (753,664 B). Both JSON arms live in `SqliteBenchmarkWriteJson` with `[Params(1_000, 10_000, 100_000)]`.
- **Assumptions:** the 7 explicit acceptance filters complete within the 240 s budget; DO freezes one job setting (`NextormConfig` default `Job.ShortRun` in-process, i.e. `NEXTORM_BENCH_FULL` unset) across all cases in both rounds and records the job fingerprint.

**(d) Deterministic rulings replacing the conditionals in §4 Ruling 5:**

1. **Candidate artifacts ARE unconditionally produced:** Both `baseline/round-{1,2}/{acceptance,public-json}/cases.json` and `candidate/round-{1,2}/{acceptance,public-json}/cases.json` are unconditionally produced in this session with identical `baseline_generation` on the unmodified `2ea3ef45` product tree: for `acceptance`, each round executes the baseline 7-control leg first and the back-to-back candidate 7-control leg second so §6's `"final-round mean <= 1.20x corresponding baseline-round mean"` comparison has both legs; for `public-json`, each round captures the paired ABBA `nextorm`/`linq2db` cases into `baseline/round-{1,2}/public-json/cases.json` first and then writes `candidate/round-{1,2}/public-json/cases.json` with the same `baseline_generation`. This satisfies §11's baseline-first rule because all `baseline/round-{1,2}/**` artifacts are captured on `2ea3ef45` before `candidate/round-{1,2}/**` is evaluated and zero product edits occur at any point (`git diff 2ea3ef45 -- src/ tests/ benchmarks/` remains empty).
2. **Collector placement:** `t311_json_gate.py` is cycle-specific spec-verification tooling (never read or invoked by CI or product tooling), so `AGENTS.md`'s rule against CI/product tooling reading `.pdca/**` is not violated. Write the working script to `.pdca/scripts/perf/t311_json_gate.py` (gitignored) and commit one byte-identical versioned copy at `docs/specs/performance/t311-oracle-rv4/t311_json_gate.py` (nothing under `tools/`).
````

### r2

````markdown
# PLAN Revision (`r=2`) — `#311` (`T311-json-streaming-3`)

## Revision Delta (`r=1` → `r=2`)

1. **Why `r=1` is revised:**
   - PLAN `r=1` (§1 and `AC-CYCLE-02`) rested on the false premise that commits `2ea3ef45..9e7fb4b3` were docs/status-only and therefore asserted `git diff 2ea3ef45 -- src/ benchmarks/` would be empty on `HEAD` (`9e7fb4b3`).
   - DO's preflight refuted that premise before any collector, build, or benchmark run was produced: `2ea3ef45..9e7fb4b3` contains 43 commits touching 6 files in `src/` and `benchmarks/` (`EntityBuilder.cs`, `TypeFacts.cs`, `ClickHouseDialect.cs`, `Program.cs`, `nextorm.benchmark.csproj`, `T269AirportOnTimeProbe.cs`) and 9 files in `tests/`.
2. **Decision among (A), (B), (C) — Ruling: Option (A) (Capture in a temporary detached worktree at `2ea3ef45`):**
   - **Option (B) is rejected:** Capturing on `HEAD` (`9e7fb4b3`) violates `docs/specs/performance/json-ratio-oracle-2.md:18-21` (*"MUST be captured on the retained tree (`2ea3ef45`) BEFORE any product edit or candidate commit; a run whose baseline was captured after a product change is invalid and yields 0 valid pairs"*), knowingly producing an invalid run.
   - **Option (C) is rejected:** `src/nextorm.core/Builders/EntityBuilder.cs` and `src/nextorm.core/Visitors/TypeFacts.cs` changed between `2ea3ef45` and `9e7fb4b3` and lie on the `nextorm.core` query-construction/visitor path executed by the benchmarks and acceptance controls. Redefining "the retained tree (`2ea3ef45`)" to mean `9e7fb4b3` would weaken both the frozen spec and the caller's explicit instruction (*"on the retained tree `2ea3ef45` before any product edit"*).
   - **Option (A) is selected and licensed:** DO will create a temporary detached worktree at `2ea3ef45` under the pre-approved temp directory (`/tmp/opencode/t311-retained-2ea3ef45` via `git worktree add --detach /tmp/opencode/t311-retained-2ea3ef45 2ea3ef45`), build and execute all benchmark rounds against that exact `2ea3ef45` checkout (`tree b3026856…`), write all evidence and receipts into the main worktree (`.pdca/T311/rv4/` and `docs/specs/performance/t311-oracle-rv4/`), and remove the detached worktree (`git worktree remove /tmp/opencode/t311-retained-2ea3ef45`) before ACT commits.
   - **Why Option (A) is within the caller's workspace authorization:** A detached worktree creates **no branch** (neither `collection/*` nor any other branch), performs **no commit, merge, tag, or push**, leaves the main worktree on branch `1.0.9-rc5` at `HEAD 9e7fb4b3` throughout, and uses `/tmp/opencode` (explicitly pre-approved in the environment contract).
3. **Ruling on `rv` supersession (`contract_rv` stays `"rv4"`):**
   - Because Option (A) measures literally on the unmodified commit `2ea3ef45`, **no rule or threshold in `docs/specs/performance/json-ratio-oracle-2.md` is reinterpreted or weakened**, so the frozen spec requires no supersession.
   - Because DO stopped at preflight in `r=1` before creating the collector or writing any measurement artifact under `.pdca/T311/rv4/` or `docs/specs/performance/t311-oracle-rv4/`, `contract_rv` remains `"rv4"` and the evidence/receipt paths `.pdca/T311/rv4/` and `docs/specs/performance/t311-oracle-rv4/` remain unchanged.
4. **Counter and evidence-budget bookkeeping (`r=2`):**
   - Per `references/plan.md` §Revision accounting, `pdca-executor` preserves the outgoing `r=1, n=1/3` preflight-blocker attempt in `.pdca/status/T311-json-streaming-3.md` (history is never erased) and books this genuine contract revision as `r=2` (with the active attempt counter booked by `pdca-executor` per the status schema; `T311-json-streaming-2`'s unspent product-lever `n=3/3` remains untouched).
   - Because zero benchmark/evidence rounds were executed under `r=1`, `r=2` holds the single permitted evidence-execution pass (`1` evidence-completion round per criterion per revision); this revision does not authorize a second live measurement pass if the first valid run yields `FAIL` or `INCONCLUSIVE`.
5. **Untouched rulings from `r=1` (including the `r=1` clarification):**
   - Exit codes (`0` = valid `PASS`, `1` = `FAIL`, `2` = `INCONCLUSIVE` or invalid input) and all verdict rules (`1k ≤ 1.20`, `10k ≤ 1.00`, `100k ≤ 0.95`, `10k` allocation `≤ 446,095 B/op` exact, `s = 0.30` instability factor `> 1.30` → `INCONCLUSIVE`, `d_i = ln(nextorm/linq2db)`, direction agreement `sign(d_1) == sign(d_2)` reported as a diagnostic alongside the per-size decision rule, 7/7 acceptance controls `≤ 1.20×` within 240 s) are **completely untouched**.
   - All `r=1` clarification rulings — verbatim `AC-311-01..03` in §3 Part A, prior-cycle attribution of `AC-311-03` bullet 1, unconditional production of `candidate/round-{1,2}/{acceptance,public-json}/cases.json`, and collector placement at `.pdca/scripts/perf/t311_json_gate.py` + `docs/specs/performance/t311-oracle-rv4/t311_json_gate.py` — remain intact in full.

## Replacement for §1 (Re-anchor snapshot bullet only)

- **Re-anchor snapshot & capture worktree:**
  - **Host worktree (`/home/alex/sources/nextorm`):** Branch `1.0.9-rc5`, HEAD `9e7fb4b3`. Between `2ea3ef45` and `9e7fb4b3`, 43 commits landed on `1.0.9-rc5`, touching 6 files in `src/` and `benchmarks/` (`src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Visitors/TypeFacts.cs`, `src/nextorm.clickhouse/ClickHouseDialect.cs`, `benchmarks/nextorm.benchmark/Program.cs`, `benchmarks/nextorm.benchmark/nextorm.benchmark.csproj`, `benchmarks/nextorm.benchmark/Attribution/T269AirportOnTimeProbe.cs`) and 9 test files for `#269`/`#359`/`#360`, while `src/nextorm.core/Query/Json` and `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWriteJson.cs` are bit-identical between `2ea3ef45` and `9e7fb4b3`.
  - **Measured target worktree (`/tmp/opencode/t311-retained-2ea3ef45`):** A temporary detached worktree checked out at `2ea3ef45` (`2ea3ef45^{tree}` = `b3026856…`), created before building/running the benchmarks and removed via `git worktree remove` before ACT commits. All `dotnet build` and `dotnet run` benchmark invocations execute against `/tmp/opencode/t311-retained-2ea3ef45/benchmarks/nextorm.benchmark`.

## Replacement for `AC-CYCLE-02` in §3 Part B

- **`AC-CYCLE-02` (Retained-tree `2ea3ef45` detached-worktree identity & 2-round paired ABBA capture):**
  - Benchmark compilation and all 2-round paired ABBA benchmark executions run against a temporary detached git worktree `<wt>` (`/tmp/opencode/t311-retained-2ea3ef45`) checked out at `2ea3ef45`.
  - Before each round starts and after each round completes, the collector asserts all four of the following exact checks:
    1. `git -C <wt> rev-parse HEAD` equals the full 40-character SHA of `2ea3ef45` (and `git -C <wt> rev-parse HEAD^{tree}` equals `b3026856…`).
    2. `git -C <wt> diff 2ea3ef45 -- src/ tests/ benchmarks/` is **empty**.
    3. `git -C <wt> status --porcelain` is **empty** (no untracked or modified files inside `<wt>`).
    4. In the host worktree (`/home/alex/sources/nextorm` at `9e7fb4b3`), `git diff 2ea3ef45 HEAD -- src/nextorm.core/Query/Json benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWriteJson.cs` is **empty**.
  - **Negative condition:** If any of checks (1)–(4) is non-empty or changes within a round, the collector/gate aborts immediately and exits with code `2` (invalid source identity, `0` valid pairs).
  - Two paired rounds (`round-1`, `round-2`) are captured in a single session on `<wt>` with one benchmark process at a time and no concurrent build/container load. Implementation order alternates by round (`ABBA` across `round-1` and `round-2`) and is recorded in `order`. All three sizes (`1000`, `10000`, `100000`) and both arms (`WriteJson_Array_Dto`, `Linq2Db_ToList_Json`) yield at least 2 paired observations per size (`N=2` rounds), with zero rejection-list violations (§7). The temporary detached worktree `<wt>` is removed via `git worktree remove` once capture completes.

## Replacement for §4 Ruling 2 (fingerprints and capture-basis recording)

2. **Under-specification 2 (`HEAD` fingerprint in §10 and capture-basis recording):**
   - In every `cases.json`, `snapshot` must be set to the full 40-character SHA of the measured commit `2ea3ef45` (`git -C <wt> rev-parse HEAD`).
   - In every `cases.json`, `source_identity` must be a structured object containing `capture_commit_sha`, `capture_tree_sha` (`b3026856…`), `capture_worktree_path`, `capture_worktree_clean`, `host_branch`, `host_head_sha`, `json_surface_diff_2ea3ef45_to_host_head_empty`, and a `capture_basis` sentence stating that all benchmark builds and 2-round paired ABBA measurements were executed in a clean detached worktree at the retained commit `2ea3ef45` (tree `b3026856…`, zero diff across `src/`, `tests/`, `benchmarks/`), while collector orchestration, evidence storage (`.pdca/T311/rv4/`) and versioned receipts (`docs/specs/performance/t311-oracle-rv4/`) reside on branch `1.0.9-rc5` at HEAD `9e7fb4b3`.
   - **Mandatory capture-basis statement in `verdict.md`:** the same sentence, verbatim, in a `## Capture basis` section.

## Replacement for §6 (source-edit and git/worktree bullets)

- **No product or benchmark source edits:** In `<wt>`, `src/**`, `tests/**`, `benchmarks/**` must remain bit-identical to `2ea3ef45` (checks 2 and 3 of `AC-CYCLE-02` must stay empty). In the host worktree, `src/**`, `tests/**`, `benchmarks/**` must not be modified (`git diff HEAD -- src tests benchmarks` empty).
- **Git, branch, and temporary worktree rules:** Host worktree stays on `1.0.9-rc5` from `9e7fb4b3`. **Licensed temporary worktree:** `git worktree add --detach /tmp/opencode/t311-retained-2ea3ef45 2ea3ef45`, removed via `git worktree remove` after capture and before ACT commits. **Forbidden:** `git push`, `git merge`, `git tag`, creating/using any `collection/*` branch or any branch in the temporary worktree, committing inside the temporary worktree. **Authorized commit in host worktree:** auto-commit on `1.0.9-rc5` only for `docs/specs/performance/t311-oracle-rv4/**` and `.pdca/status/T311-json-streaming-3.md`.

# DO work for `r=2`

Everything from the previous DO brief that is not superseded above still applies — in particular the collector surface, the §10 field sets, the frozen filters, the ABBA order (`round-1` nextorm→linq2db, `round-2` linq2db→nextorm), the 7 acceptance controls, the 240 s budget, the statistic, the exit codes, the rejection list, the four `cases.json` locations per round (baseline+candidate × acceptance+public-json), `verdict.json`/`verdict.md`/`session.json`/`run.log`, and the report format. Additional instructions:

1. **Collector CLI must accept `--bench-worktree <path>`** (default `/tmp/opencode/t311-retained-2ea3ef45`) and run every `dotnet build`/`dotnet run` against `<path>/benchmarks/nextorm.benchmark`. Evidence and receipts keep absolute paths under `/home/alex/sources/nextorm/.pdca/T311/rv4/`.
2. **Worktree lifecycle:** create it, capture `rev-parse HEAD`/`HEAD^{tree}`, run checks 1-3 of `AC-CYCLE-02`, build once there, then for each round run checks 1-3 before and after, then remove it (`git worktree remove`, no `--force`; if it refuses, report the reason and do not force-delete). Log the worktree add/remove commands and their exit codes.
3. **No second measurement pass.** One capture. An infrastructure failure (build error, 0 selected, crash, missing artifact) licenses exactly one recorded re-run of that leg with the failed attempt's log kept. A `FAIL`/`INCONCLUSIVE` measurement is never re-run.
4. **Write the versioned receipt tree** `docs/specs/performance/t311-oracle-rv4/` with: the eight `cases.json` files (same relative layout `baseline|candidate/round-N/<acceptance|public-json>/cases.json`), `verdict.json`, `verdict.md`, `session.json`, and the byte-identical collector `t311_json_gate.py` (verify with `sha256sum` against `.pdca/scripts/perf/t311_json_gate.py` and record both).
5. **Bump the status file's `## DO result`** with changed files, the identity block, build rc, every command with exit code and log path, the per-size numbers as measured, the 7/7 control numbers and per-round acceptance elapsed seconds, the collector exit code, and residuals. CRLF everywhere.

Same prohibitions as before: no edits to `src/**`, `tests/**`, `benchmarks/**`, the frozen spec, or `t247_*` scripts; never touch or stage the two untracked user files; no `git add`/`commit`/`push`/`merge`/`tag` in DO. Wall-clock cap 45 minutes for the whole session — if exceeded, stop and report what completed.

Return the compact report described before, plus the worktree add/remove exit codes and both collector sha256 hashes.
````

### PLAN Boundary Decision on `CHECK → DO` (`r=2`, `n=2/3`)

````markdown
## PLAN Boundary Decision on `CHECK → DO` (`T311-json-streaming-3`, `r=2`, `n=2/3`)

1. **Budget and Revision Accounting (`r=2, n=2/3` — no `r=3`, no `rv` supersession).** This is a `CHECK → DO` repair and single evidence-completion iteration booked as `r=2, n=2/3`, with `contract_rv` remaining `"rv4"`. A change of implementation strategy, a command, a verification method or a session reset is not a revision and resets nothing; CHECK found implementation and evidence defects, not a criteria contradiction. **One and only one** corrected recapture (`session-2`) is authorized after the collector is repaired and passes its offline self-tests. **Hard stop:** if `session-2` fails validation (`INVALID_INPUT`, exit 2, or any §7 rejection-list violation), DO must not run a third session; the cycle proceeds through CHECK to ACT as `incomplete` with `#311` kept OPEN. If `session-2` is protocol-valid, its verdict (`0` PASS / `1` FAIL / measurement `INCONCLUSIVE`) is final for this cycle.
2. **Not outcome-shopping.** The single corrected recapture is forced solely by a mechanical collector defect in `session-1` (both arms invoked `A, B` in round 2 instead of reversed, and `order` assigned retrospectively from `pair_index`), not by the numeric ratios; the identical limits (`1k ≤ 1.20`, `10k ≤ 1.00`, `100k ≤ 0.95`, `10k` allocation `≤ 446,095 B/op`) and verdict rules apply unchanged, and a valid `FAIL` or `INCONCLUSIVE` on `session-2` is a legitimate, terminal measurement.
3. **Evidence layout (`contract_rv = "rv4"` preserved).** (a) Move session-1 losslessly into `.pdca/T311/rv4/session-1-invalid/` (do not reconstruct any missing transcript); run the repaired validator against it to write `session-1-invalid/revalidated-verdict.json` with `exit_code: 2`, `status: "INVALID_INPUT"`, reasons `ROUND2_EXECUTION_ORDER_VIOLATION` and `MISSING_ROUND_BOUNDARY_IDENTITY_TRANSCRIPT`. (b) Capture `session-2` into `.pdca/T311/rv4/session-2/` and populate the canonical top-level `.pdca/T311/rv4/{session.json,verdict.json,verdict.md,run.log,baseline/**,candidate/**}` plus the versioned `docs/specs/performance/t311-oracle-rv4/` from `session-2`. (c) `verdict.json` carries `authoritative_session: "session-2"`, `authoritative_session_id`, and `invalidated_sessions[]` naming `session-1-invalid` with its id, exit 2, `INVALID_INPUT` status and the two reasons. `verdict.md` opens with a **Session Provenance** block saying `session-1-invalid` was rejected by CHECK (`T311-CHECK-01..03`, exit 2, `INVALID_INPUT`, 0 valid pairs, preserved at that path) and that every verdict comes exclusively from the repaired `session-2`.
4. **Tightened observables (no new criterion IDs).**
   - `AC-CYCLE-01`/`AC-CYCLE-02`: **one separate BDN process per implementation arm** — round 1 leg 1 `--filter *SqliteBenchmarkWriteJson.WriteJson_Array_Dto*` (nextorm, order 1), leg 2 `*...Linq2Db_ToList_Json*` (linq2db, order 2); round 2 leg 1 linq2db (order 1), leg 2 nextorm (order 2). `order` is derived from the chronological completion sequence and **verified against each leg's BDN report/console log**, never from `pair_index`. Log in `run.log` and `session.json`: the worktree add/list/remove commands with timestamps/stdout/exit codes; before and after each round the four git checks (worktree `HEAD` == full `2ea3ef45` sha, `HEAD^{tree}` == full `b3026856…` sha, `git -C <wt> diff 2ea3ef45 -- src/ tests/ benchmarks/` empty, `git -C <wt> status --porcelain` empty), the host check `git diff 2ea3ef45 HEAD -- src/nextorm.core/Query/Json benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkWriteJson.cs` empty, and sha256 of `/tmp/nextorm-bench/test.db` before and after the round (identical within the round and across the session).
   - `AC-CYCLE-04`: capture and verdict must reject with exit `2`, `status: "INVALID_INPUT"` and structured `invalid_reasons[{code,detail}]` (distinct from measurement `INCONCLUSIVE`, also exit 2) on: `SCHEMA_VIOLATION` (missing/extra/mistyped §10 key), `MISSING_OR_DUPLICATE_PAIRS` (missing/duplicate/`<2` pairs per size), `NONFINITE_OR_ZERO_SAMPLE` (NaN/±Inf or `<=0` `mean_ns`, non-finite or `<0` `allocated_b`), `NONZERO_CASE_EXIT` / `MISSING_RAW_ARTIFACT`, `IDENTITY_OR_FINGERPRINT_MISMATCH` (job_fingerprint, baseline_generation, session_id, snapshot, source_identity, db_sha256), `WITHIN_ROUND_DRIFT` (pre/post git or DB drift), `EXECUTION_ORDER_VIOLATION` (round 1 must be nextorm→linq2db, round 2 linq2db→nextorm, verified against artifacts), `CONTROL_BUDGET_OR_COUNT_VIOLATION` (acceptance round > 240.0 s or ≠ 7 controls).
   - `AC-CYCLE-04`(cont.): a `selftest` mode with deterministic offline fixtures, receipt at `.pdca/T311/rv4/selftest-receipt.json` (mirrored), asserting exact exit code and classification for: all-PASS → 0; valid size-FAIL → 1; discordant rounds → 2 INCONCLUSIVE; instability > 1.30 → 2 INCONCLUSIVE; **the actual `.pdca/T311/rv4/session-1-invalid/` directory → 2 INVALID_INPUT + `EXECUTION_ORDER_VIOLATION`**; schema violation → 2; duplicate/`<2` pairs → 2; NaN/Inf/0 mean → 2; nonzero case exit → 2; identity/db/job/baseline mismatch → 2.
   - `AC-CYCLE-06`: before handoff write `.pdca/T311/rv4/audit-receipt.json` (mirrored) plus a copy of the frozen spec at `.pdca/T311/rv4/json-ratio-oracle-2.md`, recording: sha256 of the spec at both paths and their equality; sha256 equality of the collector at `.pdca/scripts/perf/t311_json_gate.py` and the versioned mirror; the command and output of `git diff HEAD -- .pdca/scripts/perf/t247_json_gate.py .pdca/scripts/perf/t247_measure.py docs/specs/status/ .pdca/status/T246-raw-datareader-3.md` (must be empty); host `git status --porcelain` and `git diff HEAD -- src/ tests/ benchmarks/` (clean; the two untouchable user files present only as untracked and unstaged); a CRLF check of the touched versioned files and `.pdca/scripts/status_lint.py` exit 0; and an explicit historical-status record for `AC-311-03` citing `.pdca/status/T311-json-streaming-1.md:503,541-545,566-569` and `.pdca/T311/rv2/`, without claiming it met.
5. **ACT constraints.** `AC-311-03` remains **`unmet`** for `#311` after this cycle (the prior lever cycle captured its acceptance baseline after the lever commit and left attribution unverified; a retained-tree measurement cannot repair that chronology). ACT must never close `#311`, must never cite `session-1-invalid` as a performance PASS/FAIL, must never reconstruct missing transcripts or alter T247 records, must post the honest `session-2` per-size table with `AC-311-01..03` status and residuals, and must commit only `docs/specs/performance/t311-oracle-rv4/**` and `.pdca/status/T311-json-streaming-3.md`.

# Work

1. **Repair the collector** in place (`.pdca/scripts/perf/t311_json_gate.py`): per-arm scheduling as in §4, observed `order`, full rejection validation with structured reasons, `selftest`, DB/git transcripts, `--session-dir`, and a `revalidate <dir>` mode that judges an existing session directory without running anything.
2. **Archive session-1** (`mv` its files into `.pdca/T311/rv4/session-1-invalid/`, leaving nothing of it at the top level), then `revalidate` it and keep the `revalidated-verdict.json`. It must come out `INVALID_INPUT`/exit 2 with the execution-order reason — if the repaired validator instead passes it, stop and report that as a contradicting fact, do not massage the verdict.
3. **Run the selftest** and retain the receipt. Do not run the live session before the selftest is green on all ten fixtures.
4. **One recapture** as `session-2`: recreate the detached worktree, build, run round 1 then round 2 with the per-arm leg order above, the acceptance legs per round as before, the before/after transcripts, then remove the worktree. No third session under any outcome.
5. **Populate** the canonical top-level `.pdca/T311/rv4/` from `session-2`, the versioned mirror `docs/specs/performance/t311-oracle-rv4/` (`baseline|candidate/round-N/<acceptance|public-json>/cases.json`, `verdict.json`, `verdict.md`, `session.json`, `selftest-receipt.json`, `audit-receipt.json`, the byte-identical collector, the spec copy), and `audit-receipt.json`.
6. **Update the status file**: `n=2/3`, defect history, `## DO result` with the session-2 numbers, the selftest receipt path and rc, the revalidated session-1 verdict path, the audit-receipt result, and every command's exit code. CRLF.
````

### PLAN Decision (r=3, n=3/3) — offline repair boundary

````markdown
## PLAN Decision (1): Reconciling the `#311` closure rule

- `r=2` boundary §5's blanket "ACT must never close `#311`" contradicted the caller's conditional ("If every size PASSes → close `#311` on an accepted scope"). It is replaced.
- `AC-311-03` remains **`unmet`** on historical grounds (`T311-json-streaming-1.md:503,541-545,566-569`: post-lever baseline capture and unverified attribution); it is never waived, never marked `met`, and never silently converted into a blanket blocker.
- **ACT closes `#311` if and only if both hold:** (1) the final independent CHECK (`n=3/3`) **accepts** the cycle's oracle receipt (`AC-CYCLE-01..06` and `AC-311-01..02` `met`), and (2) every size (`1k ≤ 1.20`, `10k ≤ 1.00`, `100k ≤ 0.95`), the `10k` allocation (`≤ 446,095 B/op` exact) and `7/7` controls `PASS` (exit 0). It then closes on that accepted scope, citing the versioned verdict + commit, while explicitly recording `AC-311-03` as an `unmet` historical residual outside the accepted scope.
- **`#311` stays OPEN if either:** any size/allocation/control is `FAIL`/`INCONCLUSIVE`/`INVALID_INPUT`, **or** the final CHECK returns `not accepted`. ACT then records `incomplete` (`accepted_scope: none`), posts the table with the limitations, and commits.

## PLAN Decision (2): Offline repair boundary (`r=3, n=3/3`)

- Booked as the cycle's third and final attempt. It does **not** consume an evidence-completion round because it captures nothing: it repairs the validator/mirror/audit and re-judges the already-captured, immutable `session-2` and `session-1-invalid`.
- Required offline repairs in `t311_json_gate.py`: (1) `verdict` calls `revalidate_session` before `compute_verdict` and refuses to emit `PASS`/`FAIL` (exit 2, `INVALID_INPUT`, structured `invalid_reasons` persisted into `verdict.json`) when validation fails; (2) align `revalidate_session` with the field names actually written in `session-2/session.json` (`pre`/`post`, `db_sha_before`/`db_sha_after`) and assert presence/non-emptiness before comparing so checks cannot pass vacuously; (3) real artifact-based order verification — parse the actual public-json console logs and/or BDN `*-report-full*.json` to confirm each leg's executed method matches the case's `implementation` and that round 1 is nextorm→linq2db and round 2 linq2db→nextorm by timestamp; (4) strict value types on all 14 top-level and 14 per-case keys, candidate-pair and `source_identity` validation alongside baseline, removal of the `timeout -> None -> exit_code 0` path (a timed-out process records a nonzero exit and triggers rejection), structured error JSON persisted on capture/verdict failure, and capture code that mirrors baseline public-json to candidate instead of running duplicate candidate processes (future runs only); (5) `selftest` with real `NaN`/`+Inf`/`-Inf` cases, synthetic console logs carrying real method headers/timestamps exercised by the order parser, and fixtures using the live `session.json` schema; re-run it offline and refresh `selftest-receipt.json`; (6) regenerate the versioned mirror **after** all edits, prove `collector_sha_equal: true`, prove mirror SHA-256 equality and CRLF across **every** file in `docs/specs/performance/t311-oracle-rv4/**` plus `.pdca/status/T311-json-streaming-3.md`, and record the exact `status_lint.py` command, stdout and `rc=0` in `audit-receipt.json`.
- Because the collector is edited after capture, `revalidate_session` validates each `cases.json`'s `collector_sha256` against the **capture-time** hash `9e98ad130ad6afeb1eec986414cbae14d9dc7d98439641a324ce3568716ea0a5` and records the repaired validator's own hash as `validator_sha256` in `verdict.json` and `audit-receipt.json`.
- Mandatory neutral disclosure (never reconstruction) in `verdict.json` (`protocol_deviations[]`), `verdict.md` (`## Protocol Deviations & Provenance Limitations`) and the status file: (a) `session-2` ran **12** BDN processes — 4 acceptance, 4 baseline public-json, 4 duplicate **candidate** public-json legs — instead of 8; `compute_verdict` evaluates only the 4 baseline public-json legs, and the candidate legs were not used in any verdict; (b) provenance gaps: `session.json` recorded `pre`/`post` git checks and `db_sha_before`/`db_sha_after` at all four round boundaries, while `run.log` recorded `git worktree list` but not the `worktree add`/`remove` executions, did not record exact per-leg finish timestamps, and used a constant host `HOST_HEAD` (verified live post-hoc in `audit-receipt.json`). State both neutrally — they neither validate nor invalidate the baseline numbers; that judgement is CHECK's.
- No-4th rule: after this pass CHECK runs once. `accepted` → ACT closes/accepted scope; `not accepted` → ACT records `incomplete`, `#311` OPEN. No `n=4`, no fourth DO, no return to PLAN.
````

## Approval record

`not applicable: autonomous mode` — caller-driven dispatch: the delegating session's brief explicitly authorizes this scope and auto-commit for touched paths; no interactive user gate is reachable from this dispatch.

## DO result

`Offline repair pass r=3, n=3/3 completed. Session-2 verdict remains exit code 0 (all sizes PASS); validator now enforces revalidation first.`

- Identity block:
  - Host worktree: branch `1.0.9-rc5`, HEAD `9e7fb4b36c4cf902eed3fa2c76333b5ab3359732`.
  - Measured worktree: `/tmp/opencode/t311-retained-2ea3ef45`, detached HEAD `2ea3ef452711ae452ceacbd1f6d462655bd8d577`, tree `b30268568aaa997e8ba105c766fc4d46d9c25e97`.
  - Checks 1-4 of `AC-CYCLE-02`: all PASS before and after every round (worktree head/tree, diff, status, host json surface).
  - Worktree add exit code: `0`; worktree remove exit code: `0`.
  - DB: `/tmp/nextorm-bench/test.db`, sha256 before/after each round recorded in `session.json`/`transcripts.json` (identical).
- Build: `dotnet build .../nextorm.benchmark.csproj -c Release` in worktree, exit `0`, elapsed `2.334 s`, log `.pdca/T311/rv4/build.log`.
- Commands (`session-2`, per-arm scheduling):
  - round-1 acceptance baseline: exit `0`, elapsed `51.136 s`, cases `7`
  - round-1 acceptance candidate: exit `0`, elapsed `50.156 s`, cases `7`
  - round-1 public-json nextorm (order 1): exit `0`, elapsed `32.103 s`, cases `3`
  - round-1 public-json linq2db (order 2): exit `0`, elapsed `30.400 s`, cases `3`
  - round-1 candidate public-json nextorm: exit `0`, elapsed `34.013 s`, cases `3`
  - round-1 candidate public-json linq2db: exit `0`, elapsed `27.349 s`, cases `3`
  - round-2 acceptance baseline: exit `0`, elapsed `50.176 s`, cases `7`
  - round-2 acceptance candidate: exit `0`, elapsed `49.728 s`, cases `7`
  - round-2 public-json linq2db (order 1): exit `0`, elapsed `29.708 s`, cases `3`
  - round-2 public-json nextorm (order 2): exit `0`, elapsed `31.065 s`, cases `3`
  - round-2 candidate public-json linq2db: exit `0`, elapsed `28.109 s`, cases `3`
  - round-2 candidate public-json nextorm: exit `0`, elapsed `30.964 s`, cases `3`
  - Full log: `.pdca/T311/rv4/run.log`; per-leg consoles under `.../{baseline,candidate}/round-N/{acceptance,public-json}/`.
- Capture artifacts: `.pdca/T311/rv4/{baseline,candidate}/round-{1,2}/{acceptance,public-json}/cases.json` (8 files), `session.json`, `transcripts.json`, `run.log`, `build.log`.
- Verdict: `.pdca/T311/rv4/verdict.json`, `.pdca/T311/rv4/verdict.md`; collector `verdict` exit code `0`.
- Session provenance: `verdict.json` has `authoritative_session: "session-2"`, `authoritative_session_id: t311-20261101T111315Z`, `invalidated_sessions: [session-1-invalid]`; `verdict.md` opens with Session Provenance block.
- Selftest: `.pdca/T311/rv4/selftest-receipt.json`, rc `0`, all 10 fixtures pass.
- Revalidated session-1: `.pdca/T311/rv4/session-1-invalid/revalidated-verdict.json`, rc `2`, status `INVALID_INPUT`, reasons include `EXECUTION_ORDER_VIOLATION`, `MISSING_ROUND_BOUNDARY_IDENTITY_TRANSCRIPT`, plus moved-artifact reasons.
- Audit receipt: `.pdca/T311/rv4/audit-receipt.json` (spec sha equality, collector sha equality, t247/status diff empty, host status clean except two untracked user files, CRLF checks, status_lint rc 0, AC-311-03 unmet citation).
- Per-size result (session-2):
  - `1k`: PASS (round ratios `1.115625`, `1.056459`; exp(mean(d)) `1.085639`; unstable `false`; direction agree `true`)
  - `10k`: PASS (round ratios `0.933859`, `0.914260`; exp(mean(d)) `0.924008`; unstable `false`; direction agree `true`; nextorm alloc `412748` / `412748` B/op vs limit `446095`, alloc_ok `true`)
  - `100k`: PASS (round ratios `0.901249`, `0.909219`; exp(mean(d)) `0.905226`; unstable `false`; direction agree `true`)
- 7/7 acceptance controls: all pass, all rounds within `240 s` (round elapsed: r1 baseline `51.136`, candidate `50.156`; r2 baseline `50.176`, candidate `49.728`).
- Collector sha256: `.pdca/scripts/perf/t311_json_gate.py` = `9e98ad130ad6afeb1eec986414cbae14d9dc7d98439641a324ce3568716ea0a5`; versioned copy identical.
- Versioned receipts: `docs/specs/performance/t311-oracle-rv4/` (8 `cases.json`, `verdict.json`, `verdict.md`, `session.json`, `transcripts.json`, `selftest-receipt.json`, `audit-receipt.json`, `json-ratio-oracle-2.md`, `t311_json_gate.py`).
- Residual: none for registered oracle (all sizes PASS, alloc OK, 7/7 controls). `AC-311-03` remains unmet per boundary decision. `#311` routing is CHECK/ACT. No product edit made; no third measurement session.

- Offline repair pass (r=3, n=3/3, no benchmark execution):
  - Collector repairs: verdict calls revalidate before compute; strict types on 13? 14 top-level + 14 case keys; candidate-pair validation; timeout no longer maps to exit 0; structured error JSON; per-arm console/report order verification; capture mirrors baseline public-json to candidate.
  - Selftest: rc `0`, 10/10 fixtures pass (including NaN/+Inf/-Inf and order parser).
  - Revalidation: `session-2` rc `0` VALID; `session-1-invalid` rc `2` INVALID_INPUT (EXECUTION_ORDER_VIOLATION + MISSING_ROUND_BOUNDARY_IDENTITY_TRANSCRIPT + moved-artifact reasons).
  - Final verdict: rc `0`, validator sha `1a7d68e9366bca1ee6810994e34518a36e3950797a418598e1b13076fcdb09ca`, authoritative session `session-2`.
  - Disclosure: session-2 ran 12 BDN processes (4 acceptance, 4 baseline public-json, 4 duplicate candidate legs); compute_verdict evaluates only the 4 baseline public-json legs; provenance gaps in run.log (no worktree add/remove, no per-leg finish timestamps, constant host HEAD) recorded neutrally.
  - Audit receipt: `.pdca/T311/rv4/audit-receipt.json` (collector_sha_equal true, all mirror files CRLF, status_lint rc 0, protected diff empty, untouchable user files untracked/unstaged).
  - Mirror: `docs/specs/performance/t311-oracle-rv4/` byte-identical (collector sha `1a7d68e9366bca1ee6810994e34518a36e3950797a418598e1b13076fcdb09ca`).

## CHECK record

**Verdict: not accepted.** Terminal consequence: ACT records `incomplete`, `accepted_scope: none`; `#311` stays OPEN. No fourth pass. The numerical results remain PASS/PASS/PASS. Acceptance is blocked by incomplete validator repairs, missing required disclosure, and unresolved provenance/audit evidence — not by a demonstrated performance failure.

Matrix:

| Criterion | Status | Basis |
|---|---|---|
| AC-311-01 | unverified | Recorded 10k numerical targets are satisfied; acceptance-grade measurement validity remains unresolved. |
| AC-311-02 | unverified | Observed session-2 ABBA order remains correct, but provenance and complete rejection enforcement remain insufficient. |
| AC-311-03 | unmet | Historical post-lever baseline and attribution gaps remain. |
| AC-CYCLE-01 | unmet | Required schema fields exist, but malformed input can still escape structured rejection; capture identity is now hard-coded to an older collector. |
| AC-CYCLE-02 | unmet | Original twelve-process deviation and missing lifecycle/finish records persist. Offline repair cannot reconstruct them. |
| AC-CYCLE-03 | met | Seven expected controls per acceptance leg; all fourteen ratios ≤1.20; all budgets satisfied. |
| AC-CYCLE-04 | unmet | Verdict admission is repaired, but order verification, transcript validation and negative-fixture coverage remain incomplete. |
| AC-CYCLE-05 | unmet | JSON discloses deviations; mandatory Markdown disclosure is absent. Numerical PASS is not an accepted receipt. |
| AC-CYCLE-06 | unmet | Collector equality and CRLF are reported, but directory-wide mirror hash proof is absent; status defect keys were reassigned. Some repository-preservation claims remain unverified. |

Arithmetic re-derivation (unchanged across the three readings): ratios 1k `1.115625 / 1.056459` → G `1.085639` vs limit 1.20 (residual −0.114361); 10k `0.933859 / 0.914260` → G `0.924008` vs 1.00 (−0.075992); 100k `0.901249 / 0.909219` → G `0.905226` vs 0.95 (−0.044774). Directions agree throughout; first/last instability factors ≈ 1.05600 / 1.02144 / 1.00884 and maximum median deviations ≈ 1.02800 / 1.01072 / 1.00442, all < 1.30. 10k allocations 412,748 / 412,748 B/op, each 33,347 below 446,095; ratios to linq2db ≈ 0.418266 / 0.418315. Controls all within 1.20 (R1/R2: Count 0.9794/0.9996, GroupByCount 0.9847/1.0054, Any cached 0.9985/1.0143, Prepared_ToList 0.9991/1.0026, Cached_ToList 1.0066/0.9707, Cached_PlanOnly_Param 0.9611/0.9490, Cached_ToListAsync 1.0185/1.0035). Acceptance elapsed baseline/candidate R1 51.135530/50.156343 s, R2 50.175506/49.727828 s.

Stable finding dispositions: `T311-CHECK-01` execution order — **refuted** (session-2 ABBA ordering independently established); `T311-CHECK-02` validator enforcement — **confirmed, blocking** (order verification uses filesystem mtimes not embedded console timestamps, transcript validation accepts arbitrary check names, malformed types can raise instead of exiting 2); `T311-CHECK-03` provenance — **confirmed, major** (disclosed, not reconstructable); `T311-CHECK-04` `AC-311-03` historical gap — **confirmed, issue-level, permanent**; `T311-CHECK-05` twelve processes including four duplicate candidate legs — **confirmed**; `T311-CHECK-06` mirror/audit completeness — **confirmed, major**.

Verification limits recorded by CHECK: byte-for-byte file identity and the absolute absence of unlogged execution unverified; complete mirror equality, live repository/GitHub state and the versioned mirror outside direct scope unverified. No exception accepted.

## ACT record

Outcome: `incomplete` · accepted_scope: `none` · terminal: `execution (attempt budget exhausted at n=3/3)` · `#311` OPEN.

Cause: independent CHECK returned `not accepted` at `n=3/3`; the mandatory expert review (ground 2, attempts exhausted) found no criteria defect and recommended terminal ACT `incomplete`. The numerical results remain PASS/PASS/PASS, but acceptance is blocked by incomplete validator repairs, missing required disclosure, and unresolved provenance/audit evidence — not by a demonstrated performance failure.

Expert review reference (recorded above in the CHECK-phase handoff and treated as the terminal-review reference): decision — terminate this cycle through ACT as `incomplete`, no accepted scope, `#311` open; the remaining findings are an implementation/evidence insufficiency at an exhausted budget, not a criteria defect. The criteria are satisfiable and nothing is wrong with what they ask for; the blocking findings concern the instrument and its paperwork. A PLAN revision would only relax evidence-hygiene criteria after the measurement, which is adjacent to `AC-311-02`'s ban on post-measurement edits. The defect list is finite and closed, but capture-time provenance cannot be repaired for `session-2` without a new capture, and the rules forbid reconstructing it; no finite criterion inside the current contract makes `session-2` acceptable, and reclassifying provenance items would be a genuine criteria revision owned by PLAN (with fresh approval in normal mode), not something an exhausted revision can absorb. Recorded uncertainty: CHECK's reading of each finding was assumed, not re-verified; the numbers could still contain an undetected instrument fault that the validator gaps cannot rule out. The terminal record may report the captured numbers only labelled as unaccepted. Forbidden: "PASS"/"accepted"/"closes the question", use of these numbers as release or docs performance evidence, presenting `AC-311-03` as repairable by this evidence, or presenting the numbers as an accepted residual-gap record for `AC-311-01`.

Closure decision: `#311` stays OPEN. No accepted scope exists. The closure rule requires an accepted oracle receipt **and** every size/allocation/control passing. Neither condition is met (CHECK `not accepted`).

Carry-forward for any future cycle:
(a) a new, explicit measurement authorisation is required;
(b) the six offline instrument repairs — including the negative mis-order fixture — must be proven **before** that authorisation;
(c) the collector must record worktree add/remove, per-leg finish timestamps and the live host HEAD at capture time;
(d) the `AC-311-03` historical gap needs a separate decision by PLAN and the user (accept as a documented permanent exception, or re-scope);
(e) reusing `session-2` would be a criteria revision, not a continuation.

Results section (numerically within the registered limits, **as captured, unaccepted** — no accepted receipt exists; the instrument was not accepted by CHECK; not a PASS verdict, not release or docs performance evidence):

| size | limit | round 1 ratio | round 2 ratio | exp(mean(d)) | residual to limit | numerical status |
|---|---:|---:|---:|---:|---:|---|
| 1k | 1.20 | 1.115625 | 1.056459 | 1.085639 | −0.114361 | within limit, unaccepted |
| 10k | 1.00 | 0.933859 | 0.914260 | 0.924008 | −0.075992 | within limit, unaccepted |
| 100k | 0.95 | 0.901249 | 0.909219 | 0.905226 | −0.044774 | within limit, unaccepted |

- 10k allocation: 412,748 / 412,748 B/op vs limit 446,095 (33,347 below); ratio to linq2db ≈ 0.418266 / 0.418315.
- Controls: 7/7 within 1.20 (R1/R2: Count 0.9794/0.9996, GroupByCount 0.9847/1.0054, Any cached 0.9985/1.0143, Prepared_ToList 0.9991/1.0026, Cached_ToList 1.0066/0.9707, Cached_PlanOnly_Param 0.9611/0.9490, Cached_ToListAsync 1.0185/1.0035).
- Acceptance elapsed (baseline/candidate): R1 51.135530 / 50.156343 s; R2 50.175506 / 49.727828 s (all ≤ 240.0 s).
- Directions agree throughout; instability factors ≈ 1.05600 / 1.02144 / 1.00884 and maximum median deviations ≈ 1.02800 / 1.01072 / 1.00442, all < 1.30.
- Disclosure: session-2 ran 12 BDN processes (4 acceptance, 4 baseline public-json, 4 duplicate candidate legs not used by the verdict); provenance gaps (worktree add/remove not logged, no per-leg finish timestamps, constant host HEAD) recorded neutrally; `session-1-invalid` preserved and invalid (exit 2).
- Per-criterion status: `AC-311-01` unverified; `AC-311-02` unverified; `AC-311-03` unmet (historical, `T311-json-streaming-1.md:503,541-545,566-569`).

Must not be claimed: any "PASS"/"accepted"/"closes the question" wording; release or docs performance evidence; `AC-311-03` repaired by this evidence; or an accepted residual-gap record for `AC-311-01`.

## Revision history

`2026-11-01T10:40:00Z | r1 → r2 | r=1 preflight blocker: identity gate refuted (git diff 2ea3ef45 -- src benchmarks non-empty); revised to detached worktree at 2ea3ef45 (Option A)`
`2026-11-01T12:00:00Z | r2 → r3 | CHECK→DO not accepted; offline repair boundary; n=3/3; no benchmark execution`

## Defect history

- `T311-CHECK-01` | observed r=2 (n=1/3 and n=2/3) | fixes applied 1 (r=3) | evidence `.pdca/T311/rv4/session-1-invalid/` and `.pdca/T311/rv4/session-2/` | execution order: refuted for session-2 (ABBA independently established); original session-1 finding stands.
- `T311-CHECK-02` | observed r=2 (n=2/3) | fixes applied 1 (r=3) | evidence `.pdca/T311/rv4/` | validator enforcement: confirmed, blocking (order verification uses filesystem mtimes not embedded console timestamps; transcript validation accepts arbitrary check names; malformed types can raise instead of exiting 2).
- `T311-CHECK-03` | observed r=2 (n=1/3 and n=2/3) | fixes applied 0 (cannot reconstruct capture-time provenance) | evidence `.pdca/T311/rv4/session-1-invalid/` and `.pdca/T311/rv4/session-2/` | provenance: confirmed, major (disclosed, not reconstructable).
- `T311-CHECK-04` | observed r=2 | fixes applied 0 (historical, permanent) | evidence `.pdca/status/T311-json-streaming-1.md:503,541-545,566-569`, `.pdca/T311/rv2/` | `AC-311-03` historical gap: confirmed, issue-level, permanent.
- `T311-CHECK-05` | observed r=2 (n=2/3) | fixes applied 0 (disclosed, not removed) | evidence `.pdca/T311/rv4/run.log`, `.pdca/T311/rv4/session.json` | twelve processes including four duplicate candidate legs: confirmed.
- `T311-CHECK-06` | observed r=2 (n=2/3) | fixes applied 1 (r=3) | evidence `.pdca/T311/rv4/audit-receipt.json`, `docs/specs/performance/t311-oracle-rv4/` | mirror/audit completeness: confirmed, major (directory-wide CRLF proof and equality now recorded; status defect keys preserved).

## Changed files

- `.pdca/scripts/perf/t311_json_gate.py` → new collector/gate (capture + verdict, exit codes 0/1/2)
- `.pdca/T311/rv4/` → evidence (8 `cases.json`, `session.json`, `run.log`, `build.log`, `verdict.json`, `verdict.md`, per-leg consoles/artifacts)
- `docs/specs/performance/t311-oracle-rv4/` → versioned receipts (8 `cases.json`, `verdict.json`, `verdict.md`, `session.json`, byte-identical `t311_json_gate.py`)
- `.pdca/status/T311-json-streaming-3.md` → r=2 DO result and verdict

## Git state

`status path: .pdca/status/T311-json-streaming-3.md — committed this cycle (774b3e0c) · tracked` ; branch `1.0.9-rc5`, HEAD before commit `9e7fb4b3`. Committed paths: `.pdca/status/T311-json-streaming-3.md` and `docs/specs/performance/t311-oracle-rv4/**` (17 files). `.pdca/scripts/perf/t311_json_gate.py` and `.pdca/T311/rv4/**` remain gitignored/untracked by design. The two untouchable user files remain untracked and unstaged.

## Progress log

`2026-11-01T10:29:08Z | DO | revision r1 | attempt n1 | DO started | status file created`
`2026-11-01T10:35:00Z | DO | revision r1 | attempt n1 | identity gate failed | git diff 2ea3ef45 -- src benchmarks non-empty (6 files); no build/capture/verdict`
`2026-11-01T10:40:00Z | DO | revision r2 | attempt n1 | replanned r1→r2 | detached worktree at 2ea3ef45 (Option A); contract_rv stays rv4`
`2026-11-01T10:49:04Z | DO | revision r2 | attempt n1 | capture completed | session t311-20261101T104904Z; 8 runs, all exit 0; worktree remove exit 0`
`2026-11-01T11:05:00Z | DO | revision r2 | attempt n1 | verdict computed | exit code 1 (100k FAIL); 1k/10k PASS; 7/7 controls PASS; alloc OK`
`2026-11-01T11:10:00Z | DO | revision r2 | attempt n2 | CHECK→DO boundary decision applied | n=2/3; contract_rv rv4; one corrected recapture authorized`
`2026-11-01T11:25:00Z | DO | revision r2 | attempt n2 | session-2 capture completed | all 12 legs exit 0; per-arm order observed`
`2026-11-01T11:35:00Z | DO | revision r2 | attempt n2 | verdict computed | exit code 0; all sizes PASS; selftest rc 0; revalidated session-1 rc 2`
`2026-11-01T12:00:00Z | DO | revision r3 | attempt n3 | replanned r2→r3 | offline repair boundary; n=3/3; no benchmark execution`
`2026-11-01T12:30:00Z | DO | revision r3 | attempt n3 | offline repairs completed | selftest rc 0; session-2 revalidated rc 0; verdict rc 0; audit receipt refreshed`
`2026-11-01T11:39:25Z | ACT | revision r3 | attempt n3 | ACT closed incomplete | accepted_scope none; #311 OPEN; no fourth pass`
