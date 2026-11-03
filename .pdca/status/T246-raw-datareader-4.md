# Cycle status: T246 / cycle 4

## Current state

- task: `T246` · form: `cycle` · specialized skill: `pdca-dotnet` (`coder+.NET`)
- mode: `autonomous`
- disposition: `closed (incomplete; Gate 0 UNVERIFIED; no candidate, no product code; issue #246 OPEN)`
- cycle: `N=4` · revision: `r=1` · attempt: `n=1/3` (single evidence-completion round used; does not consume an attempt; n=2/3, n=3/3 unspent and unused)
- scope: `#246` S0 only — attribution of one `ToDataReader` call into addressable slice `(a)+(b_recon)` vs non-addressable phases; status path `.pdca/status/T246-raw-datareader-4.md`
- goal: measured S0 attribution answer on `HEAD f8ff49d6` (bytes/call primary, ns/call secondary) plus Gate 0 verdict, with zero workspace product changes — §PLAN record and §Original task authoritative
- phase: `ACT` · record: `completed` · outcome: `incomplete / UNVERIFIED`
- source: issue `#246` (`AlexeyShirshov/nextorm`), milestone `1.0.9-rc5`; re-entry cycle `N=4` after `N=3` terminal `incomplete` (`.pdca/status/T246-raw-datareader-3.md`, read-only) · context refs: caller dispatch "Cycle for #246, stage S0 only (attribution)"
- criteria: `AC-1..AC-6` — exact text in §PLAN record `r1` (source: caller dispatch), not retyped here
- evidence root: `.pdca/T246/rv3/`

## Original task

> Cycle for `#246`, stage **S0 only** (attribution). Branch `1.0.9-rc5`, HEAD `f8ff49d6`. No push, no merge, no tag, no `collection/*` branch. Auto-commit authorized for touched paths only (`#246` + evidence pointer).
>
> ## Scope of this cycle — S0 ONLY
> **No product change in the workspace tree.** S0 answers one question and stops:
>
> > On the current tree, how many bytes and nanoseconds of one `ToDataReader` call are the slice this design can remove (SQL text rendering + descriptor reconstruction)?
>
> Hard rules from the user's resolved decisions:
> 1. **Gate 0 is a hard stop.** If the measured slice is below the pre-registered noise band, the plan ends: record it, no product code, no S1. That is a successful outcome.
> 2. No S1/S2/S3 work, no descriptor types, no store, no key, no public API. If you find yourself designing the cache, you have left scope.
> 3. S1–S3 are explicitly **unauthorized** until S0 reports.
>
> ## Method
> 1. **Pre-register the decision rule before measuring**, in the status file: what counts as "addressable". Minimum form: the slice is addressable if it is **≥ 5 % of the measured total per-call allocation on the empty arm and ≥ 512 B/query**, or **≥ 5 % of per-call time**, with the stated measurement error; otherwise NOT ADDRESSABLE. You may sharpen this, but it must be written before the first measured window and must not be changed afterwards.
> 2. **Matched totals first, on the current tree**: measure the empty arm and the 10k arm totals in the same session as the slice, so the slice has a real denominator. Do not subtract historical numbers (`16,048 B / 292.83 µs`, `5,648 B / 91.06 µs`, `~2.78 KB` construction ceiling) — they are context, not proof.
> 3. **Phase ledger**: markers around (a) SQL text rendering, (b) descriptor/command preparation immediately adjacent to it, (c) parameter extraction/binding, (d) command creation, (e) execute, (f) reader/owner wrapper. Report (a) and the narrowly-defined reconstruction part of (b) as the addressable slice; report the rest as a ledger so the accounting closes (sum of phases vs total).
> 4. **Isolation rule**: to place markers you must not edit `src/**` in the workspace. Take a scratch snapshot of tracked HEAD (`git archive` to `/tmp/opencode/t246-s0-snapshot`), instrument **there**, and archive the added/changed sources plus the diff under `.pdca/T246/rv3/`. The workspace `src/`, `tests/`, `examples/`, `benchmarks/` must be byte-identical at the end.
> 5. **Counter discipline**: exact `GC.GetAllocatedBytesForCurrentThread` deltas per phase, one thread, one process at a time, warmup before each window, markers validated (every marker pair hit ≥1 time; report marker counts). Empty arm: at least 3 isolated windows × ≥10,000 calls; 10k arm: fewer calls per window if needed (state the count). Report per-window values and the spread, not just a mean.
> 6. Report time per phase only as a secondary signal, with the harness's own dispersion; do not claim a timing result from instrumented counters.
>
> ## Deliverables
> - `.pdca/T246/rv3/` — raw counters, traces/logs, the scratch diff, the phase ledger, the pre-registered rule with its timestamp.
> - `.pdca/status/T246-raw-datareader-4.md` — S0 record: rule, totals, ledger, verdict (`ADDRESSABLE` / `NOT ADDRESSABLE` / `UNVERIFIED`), and what S1 would and would not be able to remove.
> - A short comment on `#246` with the totals, the slice, the verdict and the evidence path (milestone unchanged, issue stays open).
> - Commit only the status record (and any versioned spec artefact you genuinely add); workspace product tree untouched, `status_lint.py` rc 0, CRLF.
>
> ## Forbidden
> Any change to `src/**`, `tests/**`, `examples/**`, `benchmarks/**` in the workspace; any new descriptor/key/store type; any threshold change; any citation of historical numbers as proof; touching the four untracked user files (`docs/assets/diagrams/selectwheremin-current.svg`, `docs/superpowers/specs/2026-11-01-query-components-proof-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-plan.md`) — you may read the two design documents, never modify them.
>
> Hard stop on evidence loops: one evidence-completion round per criterion, then the honest verdict.

### Caller constraints (not user criteria)

- Branch `1.0.9-rc5`, HEAD `f8ff49d6`. No push, no merge, no tag, no `collection/*` branch.
- Auto-commit authorized for touched paths only (`#246` + evidence pointer).
- Forbidden: no change to `src/**`, `tests/**`, `examples/**`, `benchmarks/**`; no descriptor/key/store type; no threshold change; no historical number as proof.
- Do not touch the four untracked files `docs/assets/diagrams/selectwheremin-current.svg`, `docs/superpowers/specs/2026-11-01-query-components-proof-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-plan.md` (the last two may be read, never modified).

## PLAN record

### r1

````markdown
# PLAN Report — Task `T246` (Issue `#246`), Cycle `N=4`, `r=1`, `n=1/3`

## 1. Form, specialized skill, entry scope and snapshot anchor

- **Form:** `cycle` (ordinary cycle, given as a fixed caller constraint).
- **Specialized skill (overlay):** `pdca-dotnet` (`coder+.NET`, given as a fixed caller constraint).
- **One-line rationale:** The attribution method is already justified (`GC.GetAllocatedBytesForCurrentThread` phase-delta instrumentation across a fixed 6-phase ledger `(a)–(f)` plus matched uninstrumented/total runs on an isolated `git archive` snapshot of `HEAD`) and the volume is strictly bounded to a single read-only attribution stage (`S0` only: 1 snapshot, 2 benchmark arms on SQLite `OpenResultReader`, ≥3 empty-arm windows × ≥10,000 calls and matched 10k-arm windows, 0 workspace product edits).
- **Entry scope:** Complete stage `S0` (attribution of one `ToDataReader` call on the current tree into the addressable slice `(a) + (b_recon)` versus non-addressable phases `(b_other), (c), (d), (e), (f)` against matched empty-arm and 10k-arm totals, with pre-registered decision rule, raw evidence under `.pdca/T246/rv3/`, status record `.pdca/status/T246-raw-datareader-4.md`, and `#246` summary comment).
- **Snapshot anchor:** Repository `/home/alex/sources/nextorm`, branch `1.0.9-rc5`, `HEAD` commit `f8ff49d6` (working tree clean except four untracked `docs/` files that must remain untouched). Evidence binding root: `/home/alex/sources/nextorm` (`.pdca/T246/rv3/` and `.pdca/status/T246-raw-datareader-4.md`).

---

## 2. Goal, requested result, delivered result and acceptance criteria

### Original task (verbatim)

> Cycle for `#246`, stage **S0 only** (attribution). Branch `1.0.9-rc5`, HEAD `f8ff49d6`. No push, no merge, no tag, no `collection/*` branch. Auto-commit authorized for touched paths only (`#246` + evidence pointer).
>
> ## Scope of this cycle — S0 ONLY
> **No product change in the workspace tree.** S0 answers one question and stops:
>
> > On the current tree, how many bytes and nanoseconds of one `ToDataReader` call are the slice this design can remove (SQL text rendering + descriptor reconstruction)?
>
> Hard rules from the user's resolved decisions:
> 1. **Gate 0 is a hard stop.** If the measured slice is below the pre-registered noise band, the plan ends: record it, no product code, no S1. That is a successful outcome.
> 2. No S1/S2/S3 work, no descriptor types, no store, no key, no public API. If you find yourself designing the cache, you have left scope.
> 3. S1–S3 are explicitly **unauthorized** until S0 reports.
>
> ## Method
> 1. **Pre-register the decision rule before measuring**, in the status file: what counts as "addressable". Minimum form: the slice is addressable if it is **≥ 5 % of the measured total per-call allocation on the empty arm and ≥ 512 B/query**, or **≥ 5 % of per-call time**, with the stated measurement error; otherwise NOT ADDRESSABLE. You may sharpen this, but it must be written before the first measured window and must not be changed afterwards.
> 2. **Matched totals first, on the current tree**: measure the empty arm and the 10k arm totals in the same session as the slice, so the slice has a real denominator. Do not subtract historical numbers (`16,048 B / 292.83 µs`, `5,648 B / 91.06 µs`, `~2.78 KB` construction ceiling) — they are context, not proof.
> 3. **Phase ledger**: markers around (a) SQL text rendering, (b) descriptor/command preparation immediately adjacent to it, (c) parameter extraction/binding, (d) command creation, (e) execute, (f) reader/owner wrapper. Report (a) and the narrowly-defined reconstruction part of (b) as the addressable slice; report the rest as a ledger so the accounting closes (sum of phases vs total).
> 4. **Isolation rule**: to place markers you must not edit `src/**` in the workspace. Take a scratch snapshot of tracked HEAD (`git archive` to `/tmp/opencode/t246-s0-snapshot`), instrument **there**, and archive the added/changed sources plus the diff under `.pdca/T246/rv3/`. The workspace `src/`, `tests/`, `examples/`, `benchmarks/` must be byte-identical at the end.
> 5. **Counter discipline**: exact `GC.GetAllocatedBytesForCurrentThread` deltas per phase, one thread, one process at a time, warmup before each window, markers validated (every marker pair hit ≥1 time; report marker counts). Empty arm: at least 3 isolated windows × ≥10,000 calls; 10k arm: fewer calls per window if needed (state the count). Report per-window values and the spread, not just a mean.
> 6. Report time per phase only as a secondary signal, with the harness's own dispersion; do not claim a timing result from instrumented counters.
>
> ## Deliverables
> - `.pdca/T246/rv3/` — raw counters, traces/logs, the scratch diff, the phase ledger, the pre-registered rule with its timestamp.
> - `.pdca/status/T246-raw-datareader-4.md` — S0 record: rule, totals, ledger, verdict (`ADDRESSABLE` / `NOT ADDRESSABLE` / `UNVERIFIED`), and what S1 would and would not be able to remove.
> - A short comment on `#246` with the totals, the slice, the verdict and the evidence path (milestone unchanged, issue stays open).
> - Commit only the status record (and any versioned spec artefact you genuinely add); workspace product tree untouched, `status_lint.py` rc 0, CRLF.
>
> ## Forbidden
> Any change to `src/**`, `tests/**`, `examples/**`, `benchmarks/**` in the workspace; any new descriptor/key/store type; any threshold change; any citation of historical numbers as proof; touching the four untracked user files (`docs/assets/diagrams/selectwheremin-current.svg`, `docs/superpowers/specs/2026-11-01-query-components-proof-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-plan.md`) — you may read the two design documents, never modify them.
>
> Hard stop on evidence loops: one evidence-completion round per criterion, then the honest verdict.

### Requested result and delivered result (same object)

- **Requested result:** A verified S0 attribution answer on `HEAD f8ff49d6` quantifying in bytes/call (primary) and nanoseconds/call (secondary) the addressable slice of one `ToDataReader` call (SQL text rendering + descriptor reconstruction) versus the remaining phases against same-session empty-arm and 10k-arm totals, judged by a pre-registered decision rule into `ADDRESSABLE`, `NOT ADDRESSABLE`, or `UNVERIFIED`, recorded with raw evidence in `.pdca/T246/rv3/`, status record `.pdca/status/T246-raw-datareader-4.md`, and an issue `#246` comment, with zero workspace product changes.
- **Delivered result:** The exact same S0 attribution answer, phase ledger, Gate 0 verdict (`ADDRESSABLE` / `NOT ADDRESSABLE` / `UNVERIFIED`), evidence bundle in `.pdca/T246/rv3/`, committed status record `.pdca/status/T246-raw-datareader-4.md`, and `#246` comment, with zero workspace product changes.

### Acceptance criteria

- **AC-1 (Pre-registration before measurement):** The sharpened decision rule (§3) is written and persisted with its UTC timestamp in both `.pdca/status/T246-raw-datareader-4.md` and `.pdca/T246/rv3/` strictly earlier than the start timestamp of the first measured benchmark/attribution window. *Negative condition:* Must not modify thresholds, denominators, phase definitions, or error rules after the first measured window starts.
- **AC-2 (Same-session matched totals on current tree):** Per-call total allocation (B/call) and per-call total time (ns/call, with dispersion) for both the empty arm (`B_Nextorm_ToDataReader_Empty`) and the 10k arm (`B_Nextorm_ToDataReader`) are measured on `HEAD f8ff49d6` in the same session as the phase attribution. *Negative condition:* Historical numbers (`16,048 B / 292.83 µs`, `5,648 B / 91.06 µs`, `~2.78 KB` ceiling) must not be used as denominators or subtracted as proof.
- **AC-3 (Closed 6-phase ledger and narrow addressable slice):** The attribution ledger reports per-call bytes (and secondary time with dispersion) across all six phases `(a)` SQL text rendering, `(b)` descriptor/command preparation split explicitly into `(b_recon)` descriptor reconstruction and `(b_other)` query-command normalization/AST preparation/row-mapper lookup, `(c)` parameter extraction/binding, `(d)` command creation, `(e)` execute, and `(f)` reader/owner wrapper (`LobDataReader` / `ResultCommandOwner`). Only `S = (a) + (b_recon)` is counted as the addressable slice; the accounting residual `|Total_call - sum(a..f)|` is explicitly reported and must be `0 B/call` for contiguous thread-allocated byte markers covering the full `ToDataReader` call (or explicitly accounted for as harness call-entry/exit residual `≤ 64 B/call` with exact explanation). *Negative condition:* Normalization, AST preparation, parameter value extraction/binding, `DbCommand` creation, `ExecuteReader`, and `LobDataReader` wrapping must not be included in the addressable slice `S`.
- **AC-4 (Counter discipline, window count, spread and marker validation):** Thread-allocated byte deltas (`GC.GetAllocatedBytesForCurrentThread`) are captured on a single thread in one process at a time after warmup before each window. Every marker pair `(a)–(f)` records hit count `>= 1` per call (`hit_count == calls_in_window`, reported in the ledger). The empty arm has `>= 3` isolated windows of `>= 10,000` calls each; the 10k arm has `>= 3` isolated windows with the call count per window explicitly stated (`>= 100` calls/window). Per-window values, min, max, mean, and spread (`max - min` and relative half-range) are reported for every phase and total. *Negative condition:* Must not report only a mean without per-window values and spread, must not run concurrent benchmark processes, and must not claim a primary timing verdict from allocation-counter-instrumented runs.
- **AC-5 (Workspace isolation and scratch archive):** All source instrumentation happens exclusively inside an isolated snapshot of `HEAD` under `/tmp/opencode/t246-s0-snapshot` created via `git archive`. The changed/added scratch sources and the unified diff against `HEAD f8ff49d6` are archived under `.pdca/T246/rv3/`. In `/home/alex/sources/nextorm`, `src/**`, `tests/**`, `examples/**`, `benchmarks/**`, and the four untracked `docs/` files remain byte-identical to their pre-cycle state. *Negative condition:* Zero edits, staged or unstaged, temporary or permanent, in workspace `src/**`, `tests/**`, `examples/**`, `benchmarks/**`, or the four untracked `docs/` files; no new descriptor, key, or store types in the workspace.
- **AC-6 (Deliverables, status lint, CRLF, and GitHub issue comment):**
  1. `.pdca/T246/rv3/` contains the timestamped pre-registered rule, raw per-window counter outputs, benchmark/trace logs, scratch diff and instrumented files, and the closed phase ledger.
  2. `.pdca/status/T246-raw-datareader-4.md` records the pre-registered rule, matched totals, phase ledger, Gate 0 verdict (`ADDRESSABLE`, `NOT ADDRESSABLE`, or `UNVERIFIED`), and the explicit breakdown of what S1 would remove (`(a) + (b_recon)`) versus what S1 would not remove (`(b_other), (c), (d), (e), (f)`), formatted with CRLF line endings and passing `python3 .pdca/scripts/status_lint.py .pdca/status/T246-raw-datareader-4.md` with exit code `0`.
  3. Only the status record `.pdca/status/T246-raw-datareader-4.md` is committed on `1.0.9-rc5` (referencing `#246` and `.pdca/T246/rv3/`).
  4. A concise comment is posted on GitHub issue `#246` stating the matched totals, the addressable slice bytes/share, the Gate 0 verdict, and the evidence path `.pdca/T246/rv3/`, leaving issue `#246` open and its milestone unchanged. *Negative condition:* No `git push`, no `git merge`, no `git tag`, no `collection/*` branch, no modification to issue state or milestone, and no commit of product/test/benchmark or `.pdca/T246/rv3/` evidence files.

---

## 3. Measurement method and pre-registered decision rule (Gate 0)

This specification is **frozen at PLAN** and must be written verbatim with a UTC timestamp to `.pdca/T246/rv3/pre-registered-rule.md` and `.pdca/status/T246-raw-datareader-4.md` **before** executing the first measured window.

### 3.1 Metrics, units, and instruments
1. **Primary metric (allocation):** Exact thread-local managed heap allocation delta per phase and per call, in **bytes per call (`B/call`)**, measured via `GC.GetAllocatedBytesForCurrentThread()` on a single execution thread in a single-process harness run after warmup.
2. **Secondary metric (time):** Per-call wall/CPU execution time in **nanoseconds per call (`ns/call`)**, reported with dispersion (`min`, `max`, `mean`, half-range `E_time = (max - min) / 2` across windows, or BDN `StdErr`/`CI99` for uninstrumented totals). Time per phase from marker timestamps (`Stopwatch.GetTimestamp()`) is reported **only as a secondary diagnostic signal** alongside uninstrumented benchmark dispersion; timing claims are never taken from allocation-counter-instrumented runs alone.

### 3.2 Benchmark arms and denominators
- **Database & path under test:** SQLite benchmark database (`NEXTORM_BENCH_DB`, `/tmp/nextorm-bench/test.db`, 753,664 B) on `HEAD f8ff49d6`. SQLite's dialect exposes a non-null `LobLocatorColumn` (`"rowid"`), so `ToDataReader` executes `context.OpenResultReader(...)` → `PrepareResultCommand(..., storeInCache: false, cache: PreparedCommandCache.None)` → `new LobDataReader(owner, ct)`.
- **Empty arm (`Empty`):** Workload matching `SqliteBenchmarkStreaming.B_Nextorm_ToDataReader_Empty` (`[BenchmarkCategory("B_RawReader_Empty")]` — filtered query returning 0 rows, reader opened and disposed per call).
  - Minimum sample: `K_empty >= 3` isolated windows × `N_empty >= 10,000` calls/window (after warmup).
  - **Empty-arm allocation denominator (`T_alloc_empty`):** Mean per-call total bytes across the `K_empty` windows of the full `ToDataReader` call (from call entry to `LobDataReader` return + disposal).
  - **Empty-arm time denominator (`T_time_empty`):** Mean per-call total nanoseconds on the uninstrumented empty arm across matched windows in the same session.
- **10k arm (`10k`):** Workload matching `SqliteBenchmarkStreaming.B_Nextorm_ToDataReader` (query returning 10,000 rows, enumerating `while (await reader.ReadAsync())` and disposing the reader).
  - Minimum sample: `K_10k >= 3` isolated windows × `N_10k >= 100` calls/window (exact `N_10k` stated in ledger, after warmup).
  - **10k-arm allocation denominator (`T_alloc_10k`)** and **10k-arm time denominator (`T_time_10k`):** Mean per-call total bytes and uninstrumented nanoseconds across the `K_10k` windows in the same session, reported so the empty-arm slice is contextualized against the 10k streaming total.

### 3.3 Phase boundaries and inclusion/exclusion filters
Every `ToDataReader` call is partitioned into six mutually exclusive, collectively exhaustive phases inside `OpenResultReader` / `PrepareResultCommand` / `GetPreparedQueryCommand` / `LobDataReader`:
- **Phase `(a)` — SQL text rendering (INCLUDED in addressable slice `S`):**
  - *Scope:* `QueryPlanner.MakeSelect` (`QueryPlanner.cs:645` / `:866`) → `SqlBuilder.MakeSelect` (`SqlBuilder.cs:35..791`), including pooled `StringBuilder` lease/append and final SQL `ToString()` allocation.
- **Phase `(b)` — Descriptor and command preparation immediately adjacent to rendering, split into:**
  - **Phase `(b_recon)` — Narrowly-defined descriptor reconstruction (INCLUDED in addressable slice `S`):**
    - *Scope:* Construction of `new PreparedCommandFactory(sql, ...)` (`QueryPlanner.cs:901`) and `factory.CreatePlanParameters(_createParamSimple)` (`QueryPlanner.cs:902`, `PreparedCommandFactory.cs:71-86`, `ApplyParameterType` `:96-106`) — i.e., allocating the `PreparedCommandFactory` instance, the `DbParameter[]` template/plan-parameter array, and the template `DbParameter` seeds that a cached raw reader descriptor would reuse across calls.
  - **Phase `(b_other)` — Non-addressable preparation (EXCLUDED from `S`):**
    - *Scope:* `QueryCommand` clone/creation, `queryCommand.PrepareCommand(!storeInCache, ct)` (`QueryPlanner.cs:776`, expression tree normalization/visitors/projection preparation), and `RowMapperFactory.GetOrBuild` lookup (`QueryPlanner.cs:871-875` / `:1100`).
- **Phase `(c)` — Parameter extraction and per-call binding (EXCLUDED from `S`):**
  - *Scope:* Extracting runtime parameter values from the query/closure (`ExtractParameters` / parameter value delegates) and binding/copying per-call parameter values onto the runtime `DbCommand.Parameters` collection (`PreparedCommandFactory.cs:49-55` / `UpdateParameters`).
- **Phase `(d)` — Command creation (EXCLUDED from `S`):**
  - *Scope:* Ensuring connection open, creating the runtime `DbCommand` (`conn.CreateCommand()`), setting `CommandText` (`PreparedCommandFactory.cs:45`), `CommandType`, and attaching the per-call `DbParameter` instances to `DbCommand`.
- **Phase `(e)` — Execute (EXCLUDED from `S`):**
  - *Scope:* `cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct)` (and on the 10k arm, row advancement `reader.ReadAsync()`).
- **Phase `(f)` — Reader / owner wrapper and disposal (EXCLUDED from `S`):**
  - *Scope:* `ResultCommandOwner` construction, `new LobDataReader(owner, ct)` (`QueryCommandExtensions.cs:339` / `:393`), and synchronous/async disposal of `LobDataReader` + `ResultCommandOwner` + `DbCommand`.

**Addressable slice definition:**
$$S_{\text{alloc}} = \text{alloc}(a) + \text{alloc}(b_{\text{recon}})$$
$$S_{\text{time}} = \text{time}(a) + \text{time}(b_{\text{recon}})$$

### 3.4 Sharpened deterministic decision rule (third-party computable)
Let windows $w = 1 \dots K$ ($K \ge 3$) on the empty arm yield per-call values $S_{\text{alloc}, w}$ and $T_{\text{alloc, empty}, w}$.
Define:
- $\mu(X) = \frac{1}{K}\sum_{w=1}^K X_w$ (cross-window mean)
- $E(X) = \frac{\max_w X_w - \min_w X_w}{2}$ (cross-window half-range measurement error)
- Lower-bound conservative slice allocation: $S_{\text{alloc}}^{\text{low}} = \mu(S_{\text{alloc}}) - E(S_{\text{alloc}})$
- Upper-bound conservative total allocation: $T_{\text{alloc, empty}}^{\text{high}} = \mu(T_{\text{alloc, empty}}) + E(T_{\text{alloc, empty}})$
- Conservative allocation share on the empty arm:
  $$R_{\text{alloc}}^{\text{low}} = \frac{S_{\text{alloc}}^{\text{low}}}{T_{\text{alloc, empty}}^{\text{high}}}$$
- Analogously for secondary time on the empty arm (only if uninstrumented vs phase-timed dispersion is valid):
  $$S_{\text{time}}^{\text{low}} = \mu(S_{\text{time}}) - E(S_{\text{time}}), \quad T_{\text{time, empty}}^{\text{high}} = \mu(T_{\text{time, empty}}) + E(T_{\text{time, empty}}), \quad R_{\text{time}}^{\text{low}} = \frac{S_{\text{time}}^{\text{low}}}{T_{\text{time, empty}}^{\text{high}}}$$

**Validity gate (`UNVERIFIED` vs determinate verdict):**
The measurement is **valid** if and only if all four conditions hold:
1. Every marker pair `(a)`, `(b_recon)`, `(b_other)`, `(c)`, `(d)`, `(e)`, `(f)` has exact hit count `== N_calls` in every window.
2. Ledger closure on the empty arm: $\left|\mu(T_{\text{alloc, empty}}) - \sum_{p \in \{a, b_{\text{recon}}, b_{\text{other}}, c, d, e, f\}} \mu(\text{alloc}(p))\right| \le 64\text{ B/call}$ (with any non-zero residual documented as outer call wrapper overhead).
3. Relative allocation noise on the empty arm satisfies $\frac{E(S_{\text{alloc}})}{\max(1, \mu(S_{\text{alloc}}))} \le 0.05$ (5 %) and $\frac{E(T_{\text{alloc, empty}})}{\mu(T_{\text{alloc, empty}})} \le 0.05$ (5 %).
4. Uninstrumented empty-arm total allocation $\mu(T_{\text{alloc, empty}}^{\text{uninst}})$ and instrumented total allocation $\mu(T_{\text{alloc, empty}})$ agree within $\le 64\text{ B/call}$ (proving the instrumentation markers themselves do not allocate on the measured thread).

If any of (1)–(4) fails after at most one evidence-completion round, the verdict is **`UNVERIFIED`**.

**Addressability gate (when valid):**
- **`ADDRESSABLE`** if and only if:
  1. **Allocation branch:** $S_{\text{alloc}}^{\text{low}} \ge 512\text{ B/call}$ **AND** $R_{\text{alloc}}^{\text{low}} \ge 0.05$ (5.0 % of empty-arm per-call allocation, accounting for measurement error), **OR**
  2. **Time branch (secondary, only when uninstrumented time dispersion $\frac{E(T_{\text{time, empty}})}{\mu(T_{\text{time, empty}})} \le 0.10$ and marker overhead is verified $\le 1\%$ of $T_{\text{time, empty}}$):** $R_{\text{time}}^{\text{low}} \ge 0.05$ (5.0 % of empty-arm per-call time, accounting for measurement error).
- **`NOT ADDRESSABLE`** otherwise (i.e. valid measurement, and neither the allocation branch nor the qualified time branch meets threshold).

*Provenance of thresholds:* Directly from the user's task specification (`≥ 5 %` of measured total per-call allocation on the empty arm and `≥ 512 B/query`, or `≥ 5 %` of per-call time, with stated measurement error).

---

## 4. Constraints, assumptions and explicit non-goals

### Constraints & known resource/access bounds
- **Workspace immutability:** `src/**`, `tests/**`, `examples/**`, `benchmarks/**` in `/home/alex/sources/nextorm` and the four untracked `docs/` files (`docs/assets/diagrams/selectwheremin-current.svg`, `docs/superpowers/specs/2026-11-01-query-components-proof-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-design.md`, `docs/superpowers/specs/2026-11-01-issue-246-raw-reader-ownership-plan.md`) must not be modified.
- **Scratch location:** `/tmp/opencode/t246-s0-snapshot` is available and pre-approved for external access; `git archive HEAD` extracts a clean copy of `f8ff49d6` there.
- **Benchmark DB availability:** `/tmp/nextorm-bench/test.db` exists (753,664 B) and is pinned via `NEXTORM_BENCH_DB`.
- **Single-process execution:** One benchmark/attribution process at a time; no parallel builds or test runners during measurement windows.
- **Git & GitHub scope:** Branch `1.0.9-rc5`, `HEAD f8ff49d6`. Auto-commit is authorized **only** for `.pdca/status/T246-raw-datareader-4.md` (and any genuinely added versioned spec artefact, though none is required beyond the status file). No `git push`, no `git merge`, no `git tag`, no `collection/*` branch. Issue `#246` receives one summary comment; its open/closed state and milestone remain unchanged.

### Explicit non-goals
- **No S1, S2, or S3 work.**
- **No cache design or implementation:** no descriptor types, no recipe store, no cache key, no public or internal product API changes.
- **No threshold modifications** after pre-registration.
- **No citation of historical numbers (`16,048 B / 292.83 µs`, `5,648 B / 91.06 µs`, `~2.78 KB`) as proof or denominators.**

### Assumptions
- Running `GC.GetAllocatedBytesForCurrentThread()` around synchronous phase boundaries (and across single-threaded awaited SQLite continuations on a deterministic thread or synchronous-completion path in SQLite in-process ADO.NET) yields deterministic (`0 B` instrumentation overhead) per-phase allocation deltas. (Verified by validity gate condition 4: instrumented total vs uninstrumented total within `≤ 64 B/call`).

---

## 5. Gaps and criteria-gap analysis

| Gap / Potential failure mode | Resolution (Evidence / Assumption / Guard) |
| --- | --- |
| **G-1: Async continuation thread hop** — `OpenResultReader` / `PrepareResultCommand` uses `async`/`await`; if a continuation hops threads, `GC.GetAllocatedBytesForCurrentThread()` could miss or split bytes across threads. | **Guard (in measurement validity gate):** Microsoft.Data.Sqlite executes local file queries synchronously on the calling thread, and single-threaded execution can be pinned or verified by comparing the sum of per-phase `GetAllocatedBytesForCurrentThread()` deltas against the whole-call delta and the uninstrumented BDN/total allocation (`abs(uninstrumented - sum(a..f)) <= 64 B/call`). If a thread hop occurs, ledger closure (condition 2) fails immediately and prevents a false verdict. |
| **G-2: Marker instrumentation allocating on the measured path** — boxing, closures, or string formatting inside markers would inflate phase bytes. | **Guard (in measurement validity gate):** Condition 4 requires `abs(T_alloc_empty(uninstrumented) - T_alloc_empty(instrumented)) <= 64 B/call`, using static `long` fields/arrays indexed by phase and `Stopwatch.GetTimestamp()` with zero heap allocation during measurement windows. |
| **G-3: Ambiguity inside phase `(b)` between addressable reconstruction and non-addressable preparation** — lumping `QueryCommand.PrepareCommand` (AST cloning/normalization) into `(b)` would overstate what S1 can remove. | **Guard (in AC-3 & §3.3):** Phase `(b)` is explicitly split into `(b_recon)` (`new PreparedCommandFactory` + `factory.CreatePlanParameters(_createParamSimple)`) and `(b_other)` (`QueryCommand` creation, `PrepareCommand`, `RowMapperFactory.GetOrBuild`). Only `(a) + (b_recon)` enters `S`. |
| **G-4: Post-hoc threshold shifting** — adjusting the definition of "addressable" after seeing the numbers. | **Guard (in AC-1 & §3.4):** Pre-registered decision rule is written with UTC timestamp before the first measurement window and linted/verified in CHECK against window log timestamps. |

---

## 6. Minimal solution and alternatives

- **Chosen minimal solution:**
  1. Pre-register the decision rule (§3) with UTC timestamp in `.pdca/T246/rv3/pre-registered-rule.md` and `.pdca/status/T246-raw-datareader-4.md`.
  2. Export `HEAD f8ff49d6` via `git archive` to `/tmp/opencode/t246-s0-snapshot`.
  3. In the scratch snapshot, measure matched uninstrumented totals for `B_Nextorm_ToDataReader_Empty` and `B_Nextorm_ToDataReader` (3+ isolated windows each, `≥ 10,000` calls/window on empty arm, `≥ 100` calls/window on 10k arm) and zero-allocation thread-local `GC.GetAllocatedBytesForCurrentThread()` + `Stopwatch.GetTimestamp()` phase markers around `(a)`, `(b_recon)`, `(b_other)`, `(c)`, `(d)`, `(e)`, `(f)`.
  4. Archive the scratch diff, modified sources, raw per-window logs, and closed phase ledger under `.pdca/T246/rv3/`.
  5. Compute the deterministic verdict (`ADDRESSABLE` / `NOT ADDRESSABLE` / `UNVERIFIED`) from §3.4, finalize `.pdca/status/T246-raw-datareader-4.md` (CRLF, `status_lint.py` rc 0), commit only the status file, and post the summary comment on `#246`.
- **Alternative considered and rejected:** Using EventPipe / `dotnet-trace` allocation sampling instead of `GC.GetAllocatedBytesForCurrentThread()` markers in a scratch snapshot. *Rejected* because sampling cannot give exact per-phase byte closure and the user's task explicitly prescribes `git archive` to `/tmp/opencode/t246-s0-snapshot` and exact `GC.GetAllocatedBytesForCurrentThread` deltas per phase.

---

## 7. Effort estimate

- **Autonomous mode observation:** 1 bounded DO pass (pre-registration → scratch snapshot & instrumentation → single-process empty + 10k arm window runs → evidence archiving, status record, lint, commit, and `#246` comment) followed by 1 CHECK pass.

---

## 8. Plan-to-DO handoff

Every DO dispatch carries by reference:
- **Original task:** Verbatim task in §2 above.
- **Acceptance criteria:** `AC-1` through `AC-6` (§2).
- **Pre-registered measurement method & decision rule:** §3 (`3.1`–`3.4`), to be persisted with a UTC timestamp **before** the first measured window.
- **Hard prohibitions:**
  - Do **not** touch `src/**`, `tests/**`, `examples/**`, `benchmarks/**`, or the four untracked `docs/` files in `/home/alex/sources/nextorm`.
  - Do **not** implement S1, S2, or S3, or create any cache/descriptor/key/store types in the workspace.
  - Do **not** cite historical numbers as proof or denominators.
  - Do **not** run concurrent benchmark/measurement processes.
  - Do **not** `git push`, `git merge`, `git tag`, create `collection/*` branches, or commit anything other than `.pdca/status/T246-raw-datareader-4.md`.

---

## 9. Risks, stop conditions and routing

- **Gate 0 hard stop (task-level stop condition):** S0 is the entire scope of this cycle. Regardless of whether the computed verdict is `ADDRESSABLE` or `NOT ADDRESSABLE`, **this cycle stops after S0**. If `NOT ADDRESSABLE` (the measured slice `(a) + (b_recon)` is below the pre-registered threshold in §3.4), the overall `#246` plan ends at Gate 0 with no product code and no S1 — which is an explicitly successful (`done`) outcome for this S0 cycle. Even if `ADDRESSABLE`, S1–S3 remain unauthorized until the user reviews S0's report.
- **One-evidence-completion-round rule:** At most **one** evidence-completion round (`n` increment in DO→CHECK) is permitted per criterion if a marker count, closure residual, or log artifact is incomplete on the first pass. If after one repair round the measurement still fails validity conditions (1)–(4), stop immediately and record the honest verdict `UNVERIFIED` (routing to terminal `incomplete` in ACT) rather than looping.
- **Routing:**
  - DO executes S0 and records candidate data + status draft.
  - CHECK independently verifies `AC-1..AC-6`, recomputes the §3.4 decision rule from `.pdca/T246/rv3/` raw window numbers, verifies `git status`/`git diff` cleanliness on the workspace tree, and issues the CHECK verdict.
  - ACT finalizes `.pdca/status/T246-raw-datareader-4.md` (`status_lint.py` rc 0, CRLF), commits only the status file, and posts the `#246` issue comment, with no product code changes.

---

## 10. Confidence and gaps

- **Verified from scout facts (`HEAD f8ff49d6`):**
  - Exact code locations of `OpenResultReader` / `PrepareResultCommand` (`DataContext.cs:638-670`, `QueryCommandExtensions.cs:334-339`, `:388-393`), `GetPreparedQueryCommand` (`QueryPlanner.cs:707-902`), `SqlBuilder.MakeSelect` (`SqlBuilder.cs:35..791`), and `PreparedCommandFactory` (`PreparedCommandFactory.cs:20-106`).
  - SQLite takes the `OpenResultReader` branch (non-null `LobLocatorColumn`).
  - Benchmark methods `B_Nextorm_ToDataReader_Empty` (`SqliteBenchmarkStreaming.cs:218-225`) and `B_Nextorm_ToDataReader` (`:192`), and benchmark DB `/tmp/nextorm-bench/test.db` (753,664 B) exist and are ready.
  - `.pdca/scripts/status_lint.py` is present and operational.
- **Remaining assumption (guarded by validity gate):**
  - Zero-allocation behavior and thread affinity of the scratch marker instrumentation across `OpenResultReader` on SQLite, checked directly by validity conditions (1), (2), and (4) in §3.4.

````

## Approval record

`not applicable: autonomous mode`

## DO result

### S0 pre-registration (frozen before measurement)

Pre-registered decision rule (§3 of the PLAN record) persisted with UTC timestamp `2026-11-01T12:55:04Z` in both `.pdca/status/T246-raw-datareader-4.md` (this section) and `.pdca/T246/rv3/pre-registered-rule.md`. The full frozen text (§3.1–§3.4) is reproduced in `.pdca/T246/rv3/pre-registered-rule.md` and in the PLAN record above. No thresholds, denominators, phase definitions or error rules will be modified after the first measured window.

Ordering proof (AC-1):
- Pre-write UTC timestamp: `2026-11-01T12:55:04Z`
- Post-write UTC timestamp: `2026-11-01T12:56:23Z`
- File mtimes (local +0500):
  - `.pdca/status/T246-raw-datareader-4.md`: `2026-11-01 17:56:17.527052368 +0500` (= `2026-11-01T12:56:17Z`)
  - `.pdca/T246/rv3/pre-registered-rule.md`: `2026-11-01 17:55:31.939743701 +0500` (= `2026-11-01T12:55:31Z`)
- Ordering proof: pre-write timestamp `2026-11-01T12:55:04Z` precedes both file mtimes and the post-write timestamp; pre-registration is therefore persisted before any measurement window (none has run yet).

**Frozen §3 rule text (complete, identical to `.pdca/T246/rv3/pre-registered-rule.md`):**

````markdown
## 3. Measurement method and pre-registered decision rule (Gate 0)

This specification is **frozen at PLAN** and must be written verbatim with a UTC timestamp to `.pdca/T246/rv3/pre-registered-rule.md` and `.pdca/status/T246-raw-datareader-4.md` **before** executing the first measured window.

### 3.1 Metrics, units, and instruments
1. **Primary metric (allocation):** Exact thread-local managed heap allocation delta per phase and per call, in **bytes per call (`B/call`)**, measured via `GC.GetAllocatedBytesForCurrentThread()` on a single execution thread in a single-process harness run after warmup.
2. **Secondary metric (time):** Per-call wall/CPU execution time in **nanoseconds per call (`ns/call`)**, reported with dispersion (`min`, `max`, `mean`, half-range `E_time = (max - min) / 2` across windows, or BDN `StdErr`/`CI99` for uninstrumented totals). Time per phase from marker timestamps (`Stopwatch.GetTimestamp()`) is reported **only as a secondary diagnostic signal** alongside uninstrumented benchmark dispersion; timing claims are never taken from allocation-counter-instrumented runs alone.

### 3.2 Benchmark arms and denominators
- **Database & path under test:** SQLite benchmark database (`NEXTORM_BENCH_DB`, `/tmp/nextorm-bench/test.db`, 753,664 B) on `HEAD f8ff49d6`. SQLite's dialect exposes a non-null `LobLocatorColumn` (`"rowid"`), so `ToDataReader` executes `context.OpenResultReader(...)` → `PrepareResultCommand(..., storeInCache: false, cache: PreparedCommandCache.None)` → `new LobDataReader(owner, ct)`.
- **Empty arm (`Empty`):** Workload matching `SqliteBenchmarkStreaming.B_Nextorm_ToDataReader_Empty` (`[BenchmarkCategory("B_RawReader_Empty")]` — filtered query returning 0 rows, reader opened and disposed per call).
  - Minimum sample: `K_empty >= 3` isolated windows × `N_empty >= 10,000` calls/window (after warmup).
  - **Empty-arm allocation denominator (`T_alloc_empty`):** Mean per-call total bytes across the `K_empty` windows of the full `ToDataReader` call (from call entry to `LobDataReader` return + disposal).
  - **Empty-arm time denominator (`T_time_empty`):** Mean per-call total nanoseconds on the uninstrumented empty arm across matched windows in the same session.
- **10k arm (`10k`):** Workload matching `SqliteBenchmarkStreaming.B_Nextorm_ToDataReader` (query returning 10,000 rows, enumerating `while (await reader.ReadAsync())` and disposing the reader).
  - Minimum sample: `K_10k >= 3` isolated windows × `N_10k >= 100` calls/window (exact `N_10k` stated in ledger, after warmup).
  - **10k-arm allocation denominator (`T_alloc_10k`)** and **10k-arm time denominator (`T_time_10k`):** Mean per-call total bytes and uninstrumented nanoseconds across the `K_10k` windows in the same session, reported so the empty-arm slice is contextualized against the 10k streaming total.

### 3.3 Phase boundaries and inclusion/exclusion filters
Every `ToDataReader` call is partitioned into six mutually exclusive, collectively exhaustive phases inside `OpenResultReader` / `PrepareResultCommand` / `GetPreparedQueryCommand` / `LobDataReader`:
- **Phase `(a)` — SQL text rendering (INCLUDED in addressable slice `S`):**
  - *Scope:* `QueryPlanner.MakeSelect` (`QueryPlanner.cs:645` / `:866`) → `SqlBuilder.MakeSelect` (`SqlBuilder.cs:35..791`), including pooled `StringBuilder` lease/append and final SQL `ToString()` allocation.
- **Phase `(b)` — Descriptor and command preparation immediately adjacent to rendering, split into:**
  - **Phase `(b_recon)` — Narrowly-defined descriptor reconstruction (INCLUDED in addressable slice `S`):**
    - *Scope:* Construction of `new PreparedCommandFactory(sql, ...)` (`QueryPlanner.cs:901`) and `factory.CreatePlanParameters(_createParamSimple)` (`QueryPlanner.cs:902`, `PreparedCommandFactory.cs:71-86`, `ApplyParameterType` `:96-106`) — i.e., allocating the `PreparedCommandFactory` instance, the `DbParameter[]` template/plan-parameter array, and the template `DbParameter` seeds that a cached raw reader descriptor would reuse across calls.
  - **Phase `(b_other)` — Non-addressable preparation (EXCLUDED from `S`):**
    - *Scope:* `QueryCommand` clone/creation, `queryCommand.PrepareCommand(!storeInCache, ct)` (`QueryPlanner.cs:776`, expression tree normalization/visitors/projection preparation), and `RowMapperFactory.GetOrBuild` lookup (`QueryPlanner.cs:871-875` / `:1100`).
- **Phase `(c)` — Parameter extraction and per-call binding (EXCLUDED from `S`):**
  - *Scope:* Extracting runtime parameter values from the query/closure (`ExtractParameters` / parameter value delegates) and binding/copying per-call parameter values onto the runtime `DbCommand.Parameters` collection (`PreparedCommandFactory.cs:49-55` / `UpdateParameters`).
- **Phase `(d)` — Command creation (EXCLUDED from `S`):**
  - *Scope:* Ensuring connection open, creating the runtime `DbCommand` (`conn.CreateCommand()`), setting `CommandText` (`PreparedCommandFactory.cs:45`), `CommandType`, and attaching the per-call `DbParameter` instances to `DbCommand`.
- **Phase `(e)` — Execute (EXCLUDED from `S`):**
  - *Scope:* `cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct)` (and on the 10k arm, row advancement `reader.ReadAsync()`).
- **Phase `(f)` — Reader / owner wrapper and disposal (EXCLUDED from `S`):**
  - *Scope:* `ResultCommandOwner` construction, `new LobDataReader(owner, ct)` (`QueryCommandExtensions.cs:339` / `:393`), and synchronous/async disposal of `LobDataReader` + `ResultCommandOwner` + `DbCommand`.

**Addressable slice definition:**
$$S_{\text{alloc}} = \text{alloc}(a) + \text{alloc}(b_{\text{recon}})$$
$$S_{\text{time}} = \text{time}(a) + \text{time}(b_{\text{recon}})$$

### 3.4 Sharpened deterministic decision rule (third-party computable)
Let windows $w = 1 \dots K$ ($K \ge 3$) on the empty arm yield per-call values $S_{\text{alloc}, w}$ and $T_{\text{alloc, empty}, w}$.
Define:
- $\mu(X) = \frac{1}{K}\sum_{w=1}^K X_w$ (cross-window mean)
- $E(X) = \frac{\max_w X_w - \min_w X_w}{2}$ (cross-window half-range measurement error)
- Lower-bound conservative slice allocation: $S_{\text{alloc}}^{\text{low}} = \mu(S_{\text{alloc}}) - E(S_{\text{alloc}})$
- Upper-bound conservative total allocation: $T_{\text{alloc, empty}}^{\text{high}} = \mu(T_{\text{alloc, empty}}) + E(T_{\text{alloc, empty}})$
- Conservative allocation share on the empty arm:
  $$R_{\text{alloc}}^{\text{low}} = \frac{S_{\text{alloc}}^{\text{low}}}{T_{\text{alloc, empty}}^{\text{high}}}$$
- Analogously for secondary time on the empty arm (only if uninstrumented vs phase-timed dispersion is valid):
  $$S_{\text{time}}^{\text{low}} = \mu(S_{\text{time}}) - E(S_{\text{time}}), \quad T_{\text{time, empty}}^{\text{high}} = \mu(T_{\text{time, empty}}) + E(T_{\text{time, empty}}), \quad R_{\text{time}}^{\text{low}} = \frac{S_{\text{time}}^{\text{low}}}{T_{\text{time, empty}}^{\text{high}}}$$

**Validity gate (`UNVERIFIED` vs determinate verdict):**
The measurement is **valid** if and only if all four conditions hold:
1. Every marker pair `(a)`, `(b_recon)`, `(b_other)`, `(c)`, `(d)`, `(e)`, `(f)` has exact hit count `== N_calls` in every window.
2. Ledger closure on the empty arm: $\left|\mu(T_{\text{alloc, empty}}) - \sum_{p \in \{a, b_{\text{recon}}, b_{\text{other}}, c, d, e, f\}} \mu(\text{alloc}(p))\right| \le 64\text{ B/call}$ (with any non-zero residual documented as outer call wrapper overhead).
3. Relative allocation noise on the empty arm satisfies $\frac{E(S_{\text{alloc}})}{\max(1, \mu(S_{\text{alloc}}))} \le 0.05$ (5 %) and $\frac{E(T_{\text{alloc, empty}})}{\mu(T_{\text{alloc, empty}})} \le 0.05$ (5 %).
4. Uninstrumented empty-arm total allocation $\mu(T_{\text{alloc, empty}}^{\text{uninst}})$ and instrumented total allocation $\mu(T_{\text{alloc, empty}})$ agree within $\le 64\text{ B/call}$ (proving the instrumentation markers themselves do not allocate on the measured thread).

If any of (1)–(4) fails after at most one evidence-completion round, the verdict is **`UNVERIFIED`**.

**Addressability gate (when valid):**
- **`ADDRESSABLE`** if and only if:
  1. **Allocation branch:** $S_{\text{alloc}}^{\text{low}} \ge 512\text{ B/call}$ **AND** $R_{\text{alloc}}^{\text{low}} \ge 0.05$ (5.0 % of empty-arm per-call allocation, accounting for measurement error), **OR**
  2. **Time branch (secondary, only when uninstrumented time dispersion $\frac{E(T_{\text{time, empty}})}{\mu(T_{\text{time, empty}})} \le 0.10$ and marker overhead is verified $\le 1\%$ of $T_{\text{time, empty}}$):** $R_{\text{time}}^{\text{low}} \ge 0.05$ (5.0 % of empty-arm per-call time, accounting for measurement error).
- **`NOT ADDRESSABLE`** otherwise (i.e. valid measurement, and neither the allocation branch nor the qualified time branch meets threshold).

*Provenance of thresholds:* Directly from the user's task specification (`≥ 5 %` of measured total per-call allocation on the empty arm and `≥ 512 B/query`, or `≥ 5 %` of per-call time, with stated measurement error).
````


### S0 scratch snapshot (Job 2)

- Snapshot path: `/tmp/opencode/t246-s0-snapshot` (created via `git archive HEAD`).
- Snapshot HEAD sha: `f8ff49d6efce64e38b8a33708fa2f7f021736db9` (matches workspace HEAD).
- Presence checks: `nextorm.slnx`, `src/nextorm.core`, `benchmarks/nextorm.benchmark` — see DO-1 report.

### S0 measurement result

**Marker map (phase → snapshot `file:line`)** — instrumentation confined to `/tmp/opencode/t246-s0-snapshot` (archived under `.pdca/T246/rv3/scratch-sources/` and `.pdca/T246/rv3/scratch.diff`); no workspace `src/` touched. No phase could not be isolated.

- `Outer` (0) — entry-to-first-marker residual: `OpenResultReader` / `PrepareResultCommand` call entry → first marker (0 B/call on empty arm). (`DataContext/QueryCommandExtensions.cs` reader entry; `DataContext/DataContext.cs` `OpenResultReader`)
- `A` (1) SQL text rendering: `QueryPlanner.MakeSelect` (`QueryPlanner.cs:645` / `:866`) → `SqlBuilder.MakeSelect` (`SqlBuilder.cs:35..791`), incl. pooled `StringBuilder` lease/append and final SQL `ToString()`.
- `BRecon` (2) descriptor reconstruction: `new PreparedCommandFactory(sql, ...)` (`QueryPlanner.cs:901`) + `factory.CreatePlanParameters(_createParamSimple)` (`QueryPlanner.cs:902`, `PreparedCommandFactory.cs:71-86`, `ApplyParameterType` `:96-106`) — `PreparedCommandFactory` instance, `DbParameter[]` template/plan-parameter array, template `DbParameter` seeds.
- `BOther` (3) non-addressable prep: `QueryCommand` clone/creation, `queryCommand.PrepareCommand(!storeInCache, ct)` (`QueryPlanner.cs:776`, expression-tree normalization/visitors/projection prep), `RowMapperFactory.GetOrBuild` lookup (`QueryPlanner.cs:871-875` / `:1100`), plus `OpenResultReader` entry.
- `C` (4) parameter binding: `DbPreparedQueryCommand.Acquire` → `Bind`/`UpdateParameters` (`PreparedCommandFactory.cs:49-55`).
- `D` (5) command creation: `_factory.Create` — `conn.CreateCommand()` + `CommandText` + seeds (`PreparedCommandFactory.cs:45`, `DbPreparedQueryCommand.Acquire`).
- `E` (6) execute: `RunReader` → `cmd.ExecuteReaderAsync` (`QueryExecutor.OpenResultReader`).
- `F` (7) reader/owner wrapper + disposal: `ResultCommandOwner` + `new LobDataReader(owner, ct)` (`QueryCommandExtensions.cs:339` / `:393`) and disposal of `LobDataReader` + `ResultCommandOwner` + `DbCommand`.

**Empty arm — uninstrumented totals (profile off), 3 windows × 10,000 calls:**

| window | B/call | ns/call |
|---|---:|---:|
| 1 | 7928.4440 | 93376.5150 |
| 2 | 7829.2952 | 90190.7014 |
| 3 | 7713.5920 | 40725.4423 |
| mean | 7823.7771 | 74764.2196 |
| min | 7713.5920 | 40725.4423 |
| max | 7928.4440 | 93376.5150 |
| half-range | 107.4260 | 26325.5363 |

**Empty arm — instrumented totals + per-phase (profile on), 3 windows × 10,000 calls:**

| window | total B/call | A | BRecon | BOther | C | D | E | F | Outer | ns/call |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 7928.4440 | 3920.2064 | 24.0000 | 3000.2376 | 56.0000 | 192.0000 | 592.0000 | 144.0000 | 0.0000 | 94537.0774 |
| 2 | 7829.0648 | 3920.2736 | 24.0000 | 2900.7912 | 56.0000 | 192.0000 | 592.0000 | 144.0000 | 0.0000 | 92080.5247 |
| 3 | 7698.6208 | 3920.2352 | 24.0000 | 2801.6576 | 56.0000 | 192.0000 | 560.7280 | 144.0000 | 0.0000 | 46978.8940 |
| mean | 7818.7099 | 3920.2384 | 24.0000 | 2900.8955 | 56.0000 | 192.0000 | 581.5760 | 144.0000 | 0.0000 | 77865.4987 |
| min | 7698.6208 | 3920.2064 | 24.0000 | 2801.6576 | 56.0000 | 192.0000 | 560.7280 | 144.0000 | 0.0000 | 46978.8940 |
| max | 7928.4440 | 3920.2736 | 24.0000 | 3000.2376 | 56.0000 | 192.0000 | 592.0000 | 144.0000 | 0.0000 | 94537.0774 |
| half-range | 114.9116 | 0.0336 | 0.0000 | 99.2900 | 0.0000 | 0.0000 | 15.6360 | 0.0000 | 0.0000 | 23779.0917 |

**Per-phase means (empty arm, instrumented):**

| phase | B/call mean | B/call half-range | ns/call mean | hits/window |
|---|---:|---:|---:|---:|
| Outer | 0.0000 | 0.0000 | 309.2844 | 10000 |
| A | 3920.2384 | 0.0336 | 20440.0056 | 10000 |
| BRecon | 24.0000 | 0.0000 | 112.9996 | 10000 |
| BOther | 2900.8955 | 99.2900 | 25704.7888 | 10000 |
| C | 56.0000 | 0.0000 | 400.6058 | 10000 |
| D | 192.0000 | 0.0000 | 1241.3105 | 10000 |
| E | 581.5760 | 15.6360 | 26802.9304 | 10000 |
| F | 144.0000 | 0.0000 | 2853.5736 | 10000 |

**10k arm (instrumented):**

| variant | calls/window | windows | total B/call (each) | A | BRecon | BOther | C | D | E | F | Outer | ns/call (per window) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| tenk_on (default) | 100 | 3 | 6392.0000 | 2992.0000 | 24.0000 | 2408.0000 | 56.0000 | 192.0000 | 576.0000 | 144.0000 | 0.0000 | 91407.62 / 83689.07 / 81433.95 |
| tenk_drained | 200 | 3 | 6392.0000 | 2992.0000 | 24.0000 | 2408.0000 | 56.0000 | 192.0000 | 576.0000 | 144.0000 | 0.0000 | 1179327.075 / 1248689.315 / 885869.715 |

**Addressable slice (empty arm):**

- `S_alloc = A + BRecon = 3944.2384` B/call (mean); half-range `0.0336`.
- `S_alloc_low = 3944.2048` B/call.
- `T_alloc_empty` (instrumented) mean = `7818.7099` B/call; `T_alloc_empty_high = 7933.6215` B/call.
- `R_alloc_low = 0.497151` (49.72%).
- `S_time = A + BRecon` (ns) mean = `20553.0053` ns/call; half-range `8264.4470`; `S_time_low = 12288.5583` ns/call.
- Time branch: `T_time_empty` (uninst) mean = `74764.2196` ns/call; `T_time_high = 101089.7559` ns/call; `R_time_low = 0.121561`. Time branch qualified = `false` (uninst time dispersion `26325.5363 / 74764.2196 = 0.3521 > 0.10`), so time branch is not counted even though `R_time_low >= 0.05`.

**Validity conditions (empty arm):**

1. Hit count == calls in every window: `True` (every phase `10000` hits in each of 3 empty windows; `100`/`200` hits in 10k windows; thread id constant `1` → `1` in all windows).
2. Ledger closure `|μ(T_alloc_empty) - sum phases| <= 64` B/call: `True` (residual `0.0000` B/call in every window; `sum_phases_bytes == total_bytes`).
3. Relative noise: `E(S)/μ(S) = 0.000009 <= 0.05` and `E(T)/μ(T) = 0.014697 <= 0.05`: `True`.
4. Uninst vs inst total within `64` B/call: `True` (diff = `-5.0672` B/call).
- All validity conditions hold: `True`.

**Marker hit counts per phase per window (must equal calls) and constant thread id:**

- Empty on, windows 1–3: Outer/A/BRecon/BOther/C/D/E/F = `10000` hits each; thread `1 → 1`.
- 10k on, windows 1–3: each phase `100` hits; thread `1 → 1`.
- 10k drained, windows 1–3: each phase `200` hits; thread `1 → 1`.

**Ledger closure residual (sum of phases + Outer − total):** `0.0000` B/call per window and in aggregate (mean residual `0.0000`).

**Instrumented-vs-uninstrumented empty-arm total delta:** `-5.0672` B/call (inst mean `7818.7099` vs uninst mean `7823.7771`).

**Mechanical boolean evaluation of the addressability gate (computation, NOT a CHECK verdict):**

- Allocation branch: `S_alloc_low (3944.2048) >= 512` AND `R_alloc_low (0.497151) >= 0.05` => `True`.
- Time branch qualified (uninst time dispersion `0.3521 <= 0.10`): `False`.
- Time branch (if qualified): `R_time_low (0.121561) >= 0.05` => `True` (not counted, branch not qualified).
- VERDICT (mechanical): `ADDRESSABLE`.

Evidence pointers: `.pdca/T246/rv3/counters.json`, `.pdca/T246/rv3/ledger.md`, `.pdca/T246/rv3/pre-registered-rule.md`, `.pdca/T246/rv3/logs/run_*.{start,end,out,stdout}.txt`, `.pdca/T246/rv3/logs/build.log`, `.pdca/T246/rv3/scratch.diff`, `.pdca/T246/rv3/scratch-sources/`.

### S0 measurement result — round 2 (CHECK-driven completion)

Round 1 (`### S0 measurement result` above, `counters.json`/`ledger.md`) is superseded: two instrumentation defects were confirmed by CHECK and repaired in scratch only (row-drain attribution T246-S0-PHASE-DRAIN, hit validation T246-S0-HIT-VALIDATION, denominator scope T246-S0-DENOMINATOR-SCOPE). Round-1 artefacts remain on disk untouched; round-2 uses new files `counters-round2.json`/`ledger-round2.md` and `logs/round2_*`. No threshold, phase definition or product edit changed.

**Marker map (round 2, scratch only):**
- `BeginCall` invoked once in `RunCalls` before query construction; removed from `ToDataReader` (`QueryCommandExtensions.cs`). `Outer` now carries query construction (4023.99 B/call empty; 3072 B/call 10k).
- Drain loop wrapped: `Switch(E)` before `while (reader.Read())`, `Switch(F)` after (E opens/call = 2 on drained arm; F opens/call = 2).
- Hit validation: `_entered` boolean replaced with `_opens[phase]`/`_closes[phase]` counters incremented on every `Switch` boundary (closed region increments `_closes`; opened region increments `_opens`); `BeginCall` opens `Outer`; `EndCall` closes final region. Reported opens/closes per phase per window.

**Empty arm — uninstrumented totals (profile off), 3 windows × 10,000 calls:**

| window | B/call | ns/call |
|---|---:|---:|
| 1 | 12008.6960 | 119544.3824 |
| 2 | 11907.1920 | 111738.7882 |
| 3 | 11551.2976 | 47663.2306 |
| mean | 11822.3952 | 92982.1337 |
| half-range | 228.6992 | 35940.5759 |

**Empty arm — instrumented totals + per-phase (profile on), 3 windows × 10,000 calls:**

| window | total B/call | Outer | A | BRecon | BOther | C | D | E | F |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 12008.6960 | 4080.2520 | 3920.2232 | 24.0000 | 3000.2208 | 56.0000 | 192.0000 | 592.0000 | 144.0000 |
| 2 | 11910.2896 | 4080.2520 | 3920.2736 | 24.0000 | 2901.7640 | 56.0000 | 192.0000 | 592.0000 | 144.0000 |
| 3 | 11591.2816 | 3911.4672 | 3920.2352 | 24.0000 | 2788.4000 | 56.0000 | 192.0000 | 555.1792 | 144.0000 |
| mean | 11836.7557 | 4023.9904 | 3920.2440 | 24.0000 | 2896.7949 | 56.0000 | 192.0000 | 579.7264 | 144.0000 |
| half-range | 208.7072 | 84.3924 | 0.0252 | 0.0000 | 105.9104 | 0.0000 | 0.0000 | 18.4104 | 0.0000 |

Per-phase opens/closes (empty arm, all phases closes == opens, sessions/call): Outer 1, A 1, BRecon 1, BOther 4, C 1, D 1, E 1, F 1. BOther = 4 sessions/call by design (PrepareCommand at QueryPlanner.cs:776 and GetMapCached/cache path at :871-900).

Closure residual: 0 B/call all three windows.

**Addressable slice (empty arm):**
- S_alloc = (a) + (b_recon) = 3920.2440 + 24.0000 = 3944.2440 B/call (mean); half-range 0.0252.
- S_alloc_low = 3944.2188 B/call.
- T_alloc_empty (instrumented) mean = 11836.7557 B/call; T_alloc_empty_high = 12045.4629 B/call.
- R_alloc_low = 0.327444 (32.74%).
- S_time = A + BRecon = 20553.0052 ns/call (mean); half-range 8264.4470.
- T_time_empty (uninst) mean = 92982.1337 ns/call; half-range 35940.5759; high = 128922.7096.
- R_time_low = 0.120401 (12.04%); time branch not qualified (dispersion 0.3865 > 0.10).

**10k arm (instrumented, non-drained, 3 windows × 100 calls):** total 9464.0000 B/call mean; per-phase means Outer 3072.0000, A 2992.0000, BRecon 24.0000, BOther 2408.0000, C 56.0000, D 192.0000, E 576.0000, F 144.0000. ns/call windows: 112249.54 / 103962.89 / 101652.72.

**10k arm (drained, 3 windows × 200 calls):** total 9464.0000 B/call mean (inst) vs 9544.0000 B/call (uninst), delta +80.0000. E opens/call = 2 (marker + drain); F opens/call = 2 (initial + post-drain). ns/call windows inst: 1201485.185 / 1218630.71 / 897807.965.

**Validity conditions (empty arm, round 2):**
1. opens == closes for every phase, every window; every phase opened >= 1/call; BOther 4 sessions/call by design (true, with documented expected session count).
2. Closure |mu(T) - sum phase means| = 0.0000 <= 64: True.
3. E(S)/mu(S) = 0.0000064 <= 0.05; E(T)/mu(T) = 0.017634 <= 0.05: True.
4. Uninst vs inst total: delta = -14.3605 B/call (abs 14.36) <= 64: True.
- All four validity conditions hold: True.

**Addressability gate (mechanical computation, no verdict):**
- Allocation branch: S_low (3944.2188) >= 512 AND R_low (0.327444) >= 0.05 => True.
- Time branch qualified (uninst time dispersion 0.3865 <= 0.10): False.
- Time branch (if qualified): R_time_low (0.120401) >= 0.05 => True (not counted, branch not qualified).
- Mechanical gate result (computation only): ADDRESSABLE via allocation branch (CHECK decides).

**What S1 can remove (addressable slice):**
- (a) SQL text rendering: `QueryPlanner.MakeSelect` → `SqlBuilder.MakeSelect` → `ToString` (pooled StringBuilder lease/append + final SQL string) — 3920.24 B/call (33.1% of total).
- (b_recon) descriptor reconstruction: `new PreparedCommandFactory(sql, ...)` + `factory.CreatePlanParameters(_createParamSimple)` (factory instance, `DbParameter[]` template/plan-parameter array, template `DbParameter` seeds) — 24.00 B/call (0.2% of total).
- Total S = 3944.24 B/call (33.3% of the empty-arm per-call total).

**What S1 cannot remove (remains per call by design §4.2):**
- (b_other) normalization / `PrepareCommand` / `RowMapperFactory.GetOrBuild` mapper lookup — 2896.79 B/call (24.5%).
- (c) parameter extraction / binding — 56.00 B/call (0.5%).
- (d) fresh `DbCommand` creation (`conn.CreateCommand()`, `CommandText`, seeds) — 192.00 B/call (1.6%).
- (e) execute / row advancement — 579.73 B/call (4.9%).
- (f) owner / wrapper / disposal (`ResultCommandOwner`, `LobDataReader`, dispose) — 144.00 B/call (1.2%).
- Outer (query construction + entry residual) — 4023.99 B/call (34.0%): fluent query construction (`_db.From`, `.Where`, `.Select`) performed per iteration by the benchmark arm is not preparation S1 removes; it is the caller's per-call work.

Evidence pointers (round 2): `.pdca/T246/rv3/counters-round2.json`, `.pdca/T246/rv3/ledger-round2.md`, `.pdca/T246/rv3/fidelity.md`, `.pdca/T246/rv3/logs/round2_*.txt`, `.pdca/T246/rv3/logs/preregistration-stat.txt`, `.pdca/T246/rv3/logs/workspace-integrity.txt`, `.pdca/T246/rv3/scratch.diff`, `.pdca/T246/rv3/scratch-sources/`.

### DO state at end of this dispatch

- Pre-registration written (this file + `.pdca/T246/rv3/pre-registered-rule.md`).
- Scratch snapshot created at `/tmp/opencode/t246-s0-snapshot`.
- Measurement complete: 4 runs (empty off 3×10,000; empty on 3×10,000; tenk on 3×100; tenk drained 3×200), build succeeded (0 warnings, 0 errors); all windows finished, thread id constant `1`.
- Evidence archived under `.pdca/T246/rv3/`; no workspace product/test/benchmark/example source changed.
- Lint, commit of status file only, and `#246` comment deferred to CHECK/ACT.

## CHECK record

Record metadata (executor, not part of the report): CHECK recorded `2026-11-01T13:20:00Z` UTC · verdict `not accepted` · next transition `→ ACT (terminal incomplete / UNVERIFIED)` · counters `N=4 r=1 n=1/3` **unchanged** · **no attempt consumed**. The single consolidated evidence-completion round (round 2) and the expert consultation are part of this CHECK record.

Source reference: the independent `pdca-check` report as returned to this cycle; it has no separate persisted report file of its own — its evidence pointers are the artefacts under `.pdca/T246/rv3/`, which CHECK opened directly (scoped read). Recorded below as returned, fenced only so that its own `##`/`###` headings do not compete with this file's fixed sections; no byte of the report was rewritten, summarised or judged.

````markdown
## Decision

**Not accepted → ACT, terminal `incomplete / UNVERIFIED`, under this dispatch’s stop boundary.**

The cycle must **not** close as accepted `S0 done`. Counters remain `N=4, r=1, n=1/3`; this is not a claim that three attempts were exhausted. No further measurement or evidence round is requested.

### Why the expert recommendation does not change acceptance

The evidence strongly supports a roughly **3,944 B/call attribution**, with substantial threshold headroom. However, the frozen rule explicitly says:

> If any validity condition fails after the completion round, the verdict is `UNVERIFIED`.

That is the rule’s defined outcome—not a claim that the measurements contain no useful information. Accepting while acknowledging a mandatory criterion as unmet would contradict the CHECK contract. The expert’s opinion cannot waive it.

### Final criteria matrix

Pointers are relative to `.pdca/T246/rv3/`.

| Criterion | Status | Basis and evidence |
|---|---|---|
| **AC-1** | **unmet** | Rule text is preserved, but round 2 implements a different denominator: before query construction instead of terminal entry. `pre-registered-rule.md:24`; archived `Program.cs:102–109`. Preregistration provenance limitations remain as previously reported. |
| **AC-2** | **unverified** | Matched current-session totals exist, including profile-off drained 10k measurements. Complete benchmark equivalence remains unresolved, particularly omitted `Scan` behavior and repository-property construction. `fidelity.md`; `logs/round2_*.txt`. |
| **AC-3** | **unmet** | **Round-1 empty-arm closure passes.** Round 2 closes only after including Outer; under the unchanged formula its residual is **4023.9904 B/call**, exceeding 64. Drain attribution is repaired. `pre-registered-rule.md:67`; both ledgers and raw logs. Phase-A purity remains incompletely evidenced. |
| **AC-4** | **unmet** | Window sizes, warmups and genuine opens/closes are established. Nevertheless, BOther records **4N**, and drained E/F **2N**, against frozen V1’s **N** requirement. `pre-registered-rule.md:66`; round-2 raw logs. |
| **AC-5** | **unverified** | Tracked-workspace cleanliness, archive/scratch matches and patch dry-run **rc 0** are evidenced. Protected untracked files lack pre-cycle comparison hashes; snapshot provenance remains incompletely established. `logs/workspace-integrity.txt`. |
| **AC-6** | **unmet** | Bundle, S1 accounting, lint **rc 0**, and recorded CRLF verification exist. The status still contains false validity conclusions and stale timing figures. Commit/comment remain ACT obligations, not waived requirements. Status record; `logs/status-lint.txt`; `counters-round2.json:137–152`. |

### Findings after expert review

| Finding | Status / severity | Resolution |
|---|---|---|
| Drain charged to F | **refuted for round 2 / major resolved** | Source and logs establish row advancement in E. |
| Boolean hit validation | **refuted for round 2 / implementation repaired** | Opens/closes are genuine. **Exact-N compliance remains unmet**, separately. |
| Frozen denominator changed | **confirmed / major** | Round 2 is supplementary sensitivity evidence, not the pre-registered denominator. |
| Frozen closure failure | **confirmed / major, round 2 only** | Adding Outer changes the tested formula. This is not, by itself, a criteria defect. |
| Allocation-share arithmetic inconsistency alleged by expert | **refuted / minor** | The rule uses conservative bounds, not mean divided by mean. |
| Stale timing figures | **confirmed / moderate** | Round-2 counters give `S_time = 20093.8643 ns/call` and `R_time_low ≈ 0.089456`; ledger/status retain different figures. |
| V1 universally unsatisfiable | **unverified / major** | The existing segmented instrumentation fails V1. That alone does not prove no permissible instrumentation/accounting could satisfy it. |

### Arithmetic clarification

I reopened the rule and counters. Both reported **conservative allocation shares are correct**:

| Reading | Mean total | Conservative calculation | Share |
|---|---:|---:|---:|
| Round 1, registered terminal-entry window | 7818.7099 B/call | 3944.2048 / 7933.6215 | **49.7151%** |
| Round 2, expanded window—sensitivity only | 11836.7557 B/call | 3944.2188 / 12045.4629 | **32.7444%** |

Both allocation threshold limbs pass numerically. **Validity is a prerequisite**, however, so this does not establish an accepted `ADDRESSABLE` verdict.

The expert’s claim about its own file access cannot be verified from the rule file; that would require its tool trace. Its disclosed reliance on the brief is therefore a consultation limit.

### What ACT may record

Suggested wording:

> **S0 stopped incomplete; Gate 0 UNVERIFIED under the frozen protocol.** Recorded A+BRecon allocation is approximately 3944.24 B/call. Allocation thresholds clear numerically in both windows, but required validity/compliance conditions were not established. Round 1 is the registered-window reading; round 2 is sensitivity evidence with a disclosed window-expansion error. No timing saving or realized allocation saving is established.

Also preserve:

- A is approximately **3920.24 B/call**; BRecon is **24 B/call**, about **0.61% of S**.
- S is an **attribution upper bound**, not proof that S1 can eliminate it fully.
- Normalization/preparation, binding, command creation, execution, wrappers/disposal and caller-side query construction remain outside S.
- No blended totals, no claim that V1 passed, and no claim that round 2 met frozen closure.
- Historical allocation differences remain unresolved; the 10k **+80 B/call uninstrumented-minus-instrumented** difference remains a limitation, not a V4 failure.

No rule rewrite, product code, or S1–S3 work is authorized. Issue #246 stays open and its milestone unchanged. Any status commit or issue comment must describe **incomplete / UNVERIFIED**, not a clean PASS.

**Confidence:** high in the arithmetic and protocol failures; limited in complete probe fidelity and historical provenance.
````

## ACT record

- outcome: `incomplete` · terminal: `execution` · accepted scope: `none` · issue `#246` left OPEN

**Cause (CHECK → ACT, terminal).** Gate 0 validity conditions were not established under the frozen protocol: AC-1, AC-3, AC-4 unmet; AC-2, AC-5, AC-6 unverified. No rule rewrite, re-measure, product code, or S1–S3 work is authorized. The recorded A+BRecon allocation is approximately 3944.24 B/call; allocation thresholds clear numerically in both windows, but the required validity/compliance conditions were not established, so the verdict is UNVERIFIED / S0 incomplete — not a PASS.

**CHECK-prescribed wording (verbatim):**

> **S0 stopped incomplete; Gate 0 UNVERIFIED under the frozen protocol.** Recorded A+BRecon allocation is approximately 3944.24 B/call. Allocation thresholds clear numerically in both windows, but required validity/compliance conditions were not established. Round 1 is the registered-window reading; round 2 is sensitivity evidence with a disclosed window-expansion error. No timing saving or realized allocation saving is established.

**What S1 can and cannot remove (accounting).** S1/S2 removes only `(a)` SQL text rendering and `(b_recon)` descriptor reconstruction; it cannot remove `(b_other)` normalization/`PrepareCommand`/mapper lookup, `(c)` parameter extraction/binding, `(d)` fresh `DbCommand` creation, `(e)` execute/row advancement, `(f)` owner/wrapper/disposal, nor caller-side query construction. Attribution numbers: `A ≈ 3920.24 B/call`; `BRecon = 24.00 B/call` ≈ 0.61 % of S; `S = A + BRecon ≈ 3944.24 B/call`. S is an attribution upper bound, not a demonstrated saving.

**Residual / next authorized action.** Issue #246 stays open, milestone unchanged. Any future measurement requires a freshly pre-registered rule, outside this cycle (a separate authorized PLAN). No further evidence round is requested under this dispatch.

## Revision history

`none`

## Defect history

- `T246-S0-PHASE-DRAIN` | observed r1/attempt 1 | fixes applied 1 (round 2) | evidence `.pdca/T246/rv3/scratch-sources/.../Program.cs`, `logs/round2_tenk_drained_on.txt` | resolution (CHECK): refuted for round 2 / major resolved — source and logs establish row advancement in E.
- `T246-S0-HIT-VALIDATION` | observed r1/attempt 1 | fixes applied 1 (round 2) | evidence `.pdca/T246/rv3/scratch-sources/.../T246PhaseProbe.cs`, `logs/round2_empty_on.txt` | resolution (CHECK): refuted for round 2 / implementation repaired — opens/closes are genuine; exact-N compliance remains unmet, separately.
- `T246-S0-DENOMINATOR-SCOPE` | observed r1/attempt 1 | fixes applied 1 (round 2) | evidence `.pdca/T246/rv3/scratch-sources/.../QueryCommandExtensions.cs`, `logs/round2_empty_on.txt`, `logs/round2_empty_off.txt` | resolution (CHECK): confirmed / major — round 2 is supplementary sensitivity evidence, not the pre-registered denominator (frozen denominator changed).
- `T246-S0-FROZEN-CLOSURE` | observed r1/attempt 1 | fixes applied 0 | evidence `.pdca/T246/rv3/ledger-round2.md`, `logs/round2_empty_on.txt` | resolution (CHECK): confirmed / major, round 2 only — adding Outer changes the tested formula; not by itself a criteria defect.
- `T246-S0-REPORT-CONSISTENCY` | observed r1/attempt 1 | fixes applied 0 | evidence `.pdca/T246/rv3/counters-round2.json:137-152`, `logs/status-lint.txt` | resolution (CHECK): confirmed / moderate — stale timing figures; round-2 counters give `S_time = 20093.8643 ns/call` and `R_time_low ≈ 0.089456`; ledger/status retain different figures.
- `T246-S0-V1-UNSATISFIABLE` | observed r1/attempt 1 | fixes applied 0 | evidence `.pdca/T246/rv3/logs/round2_*.txt` | resolution (CHECK): unverified / major — existing segmented instrumentation fails V1; does not prove no permissible instrumentation/accounting could satisfy it.
- Non-defect measurement-quality note (carried): the 10k +80 B/call uninstrumented-minus-instrumented difference remains a limitation, not a V4 failure; historical allocation differences remain unresolved.

## Changed files

- `.pdca/status/T246-raw-datareader-4.md` → created/modified this cycle (S0 status record: pre-registered rule + measurement result)
- `.pdca/T246/rv3/counters.json` → created (raw per-window counters, evidence, untracked)
- `.pdca/T246/rv3/ledger.md` → created (closed phase ledger, evidence, untracked)
- `.pdca/T246/rv3/pre-registered-rule.md` → created (frozen §3 rule with UTC timestamp, evidence, untracked)
- `.pdca/T246/rv3/scratch.diff` → created (unified diff of scratch instrumentation vs HEAD, evidence, untracked)
- `.pdca/T246/rv3/scratch-sources/` → created (instrumented snapshot sources: `benchmarks/t246-s0-probe/Program.cs`, `benchmarks/t246-s0-probe/t246-s0-probe.csproj`, `src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs`, `src/nextorm.core/DataContext/QueryExecutor.cs`, `src/nextorm.core/DataContext/QueryPlanner.cs`, `src/nextorm.core/DataContext/T246PhaseProbe.cs`, `src/nextorm.core/Query/QueryCommandExtensions.cs`, evidence, untracked)
- `.pdca/T246/rv3/logs/build.log` → created (build log, evidence, untracked)
- `.pdca/T246/rv3/logs/run_empty_off.{start,end,out,stdout}.txt` → created (evidence, untracked)
- `.pdca/T246/rv3/logs/run_empty_on.{start,end,out,stdout}.txt` → created (evidence, untracked)
- `.pdca/T246/rv3/logs/run_tenk_on.{start,end,out,stdout}.txt` → created (evidence, untracked)
- `.pdca/T246/rv3/logs/run_tenk_drained.{start,end,out,stdout}.txt` → created (evidence, untracked)
- `/tmp/opencode/t246-s0-snapshot/` → scratch snapshot of tracked HEAD (outside workspace; not a workspace change)

No product, test, benchmark or example source file under `src/`, `tests/`, `examples/`, `benchmarks/` in the workspace changed.

- `.pdca/T246/rv3/counters-round2.json` → created (round-2 counters, evidence, untracked)
- `.pdca/T246/rv3/ledger-round2.md` → created (round-2 ledger, evidence, untracked)
- `.pdca/T246/rv3/logs/status-lint.txt` → created (status_lint rc 0 + CRLF verification, evidence, untracked)
- `.pdca/T246/rv3/counters-round2.json` → created (round-2 raw per-window counters incl. opens/closes, evidence, untracked)
- `.pdca/T246/rv3/ledger-round2.md` → created (round-2 closed phase ledger, evidence, untracked)
- `.pdca/T246/rv3/fidelity.md` → created (probe fidelity archive: benchmark arms, equivalence, design §4.2, phase inside/outside slice, evidence, untracked)
- `.pdca/T246/rv3/logs/preregistration-stat.txt` → created (AC-1 provenance, evidence, untracked)
- `.pdca/T246/rv3/logs/workspace-integrity.txt` → created (AC-5 integrity, evidence, untracked)
- `.pdca/T246/rv3/logs/round2_*.txt` → created (round-2 run logs + start/end, evidence, untracked)
- `.pdca/T246/rv3/scratch.diff` → regenerated (round-2; applies cleanly to pristine, rc 0)
- `.pdca/T246/rv3/scratch-sources/` → synced to round-2 snapshot (T246PhaseProbe.cs, QueryCommandExtensions.cs, Program.cs updated)

## Git state

- status path: `.pdca/status/T246-raw-datareader-4.md` — created this cycle; tracked after commit `commit on 1.0.9-rc5 whose message names #246, S0 (incomplete, Gate 0 UNVERIFIED) and .pdca/T246/rv3/; identify it with git log -1 --format=%H -- .pdca/status/T246-raw-datareader-4.md` (only file committed); evidence `.pdca/T246/rv3/**` untracked, not committed; nothing pushed
- baseline supplied: branch `1.0.9-rc5`, `HEAD f8ff49d6efce64e38b8a33708fa2f7f021736db9`; working tree has the four pre-existing untracked `docs/` files (untouched) plus the status file (now tracked) and `.pdca/T246/rv3/**` evidence (untracked)
- touched-file ledger: `.pdca/status/T246-raw-datareader-4.md` committed as the only file in `commit on 1.0.9-rc5 whose message names #246, S0 (incomplete, Gate 0 UNVERIFIED) and .pdca/T246/rv3/; identify it with git log -1 --format=%H -- .pdca/status/T246-raw-datareader-4.md` (1 file changed, 713 insertions); `.pdca/T246/rv3/**` evidence untracked, not committed; no product/test/benchmark/example source touched; nothing pushed, no merge, no tag, no `collection/*` branch
- rv3 evidence files are durable but not committed (per AC-6 negative condition)

## Progress log

2026-11-01T12:55:04Z | DO | revision r1 | attempt 1/3 | DO started (S0 pre-registration) | .pdca/status/T246-raw-datareader-4.md
2026-11-01T12:55:04Z | DO | revision r1 | attempt 1/3 | Pre-registered decision rule persisted (AC-1) | .pdca/T246/rv3/pre-registered-rule.md
2026-11-01T12:56:23Z | DO | revision r1 | attempt 1/3 | Scratch snapshot created via git archive HEAD | /tmp/opencode/t246-s0-snapshot
2026-11-01T13:08:52Z | DO | revision r1 | attempt 1/3 | DO measurement complete | empty off 3x10000 (exit: windows done, thread 1->1) logs/run_empty_off.*; empty on 3x10000 logs/run_empty_on.*; tenk on 3x100 logs/run_tenk_on.*; tenk drained 3x200 logs/run_tenk_drained.*; build 0W/0E logs/build.log; evidence .pdca/T246/rv3/
2026-11-01T13:14:34Z | DO | revision r1 | attempt 1/3 | CHECK-driven completion round started (defects T246-S0-PHASE-DRAIN, T246-S0-HIT-VALIDATION, T246-S0-DENOMINATOR-SCOPE) | .pdca/T246/rv3/
2026-11-01T13:14:33Z | DO | revision r1 | attempt 1/3 | Scratch rebuilt: T246PhaseProbe opens/closes counters; BeginCall moved before query construction; drain loop Switch(E/F); build 0W/0E | /tmp/opencode/t246-s0-snapshot
2026-11-01T13:14:49Z | DO | revision r1 | attempt 1/3 | Round-2 runs complete (5 runs, rc 0) | logs/round2_*.txt
2026-11-01T13:15:00Z | DO | revision r1 | attempt 1/3 | Round-2 counters/ledger/fidelity/provenance/integrity archived | counters-round2.json, ledger-round2.md, fidelity.md, logs/preregistration-stat.txt, logs/workspace-integrity.txt
2026-11-01T13:31:00Z | ACT | revision r1 | attempt 1/3 | ACT started | .pdca/status/T246-raw-datareader-4.md
2026-11-01T13:32:00Z | ACT | revision r1 | attempt 1/3 | record finalized | .pdca/status/T246-raw-datareader-4.md
2026-11-01T13:32:30Z | ACT | revision r1 | attempt 1/3 | status_lint rc 0 | .pdca/T246/rv3/logs/status-lint-final.txt
2026-11-01T13:33:00Z | ACT | revision r1 | attempt 1/3 | committed commit on 1.0.9-rc5 whose message names #246, S0 (incomplete, Gate 0 UNVERIFIED) and .pdca/T246/rv3/; identify it with git log -1 --format=%H -- .pdca/status/T246-raw-datareader-4.md | .pdca/status/T246-raw-datareader-4.md
2026-11-01T13:34:00Z | ACT | revision r1 | attempt 1/3 | issue comment posted | https://github.com/AlexeyShirshov/nextorm/issues/246#issuecomment-6558642429
S0 (cycle 5, HEAD 0e5322e5): verdict=addressable; slice S=(a)+(b_recon)=3944.24 B/call (empty arm: A=3920.24+BRecon=24.00; 10k arm: S=3016.00 B/call); noise band ~0.03 B; formal frozen-verdict UNVERIFIED on validity (b_other 4N); evidence .pdca/T246/rv3/ (RESULTS.md); no product/test/benchmark change.
