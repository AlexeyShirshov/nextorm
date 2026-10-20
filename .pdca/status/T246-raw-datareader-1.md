# T246 — #246 ToDataReader raw reader (cycle 1, plan r=5)

> **2026-10-20 — rc5 acceptance reformulation (policy supersession).** The GitHub issue now uses the two-tier policy: **global** allocations ≤ **1.20×** linq2db and time ≤ **1.00×** linq2db; **local** acceptance = one-axis improvement with no regression on the other (allocations down + time not worse, or time better + allocations not up). This file's frozen thresholds and plan are the historical rc3 attempt. Before any rc5 execution, a new plan revision (`r+1`) must be planned from the reformulated issue; `N`/`r`/`n` are not reset.

- task: T246
- issue: https://github.com/AlexeyShirshov/nextorm/issues/246
- collection: `.pdca/status/collection-rc3-4.md` (do not edit)
- selected_variant: `pdca-dotnet`; overlay `.opencode/skills/nextorm-pdca/SKILL.md`
- repo / branch: `/home/alex/sources/nextorm`, `1.0.9-rc3`; base `0d4b0cf6`
- cycle: **N=1** (the prior flow never reached ACT, so N does not grow)
- plan revision: **r=5** (r=1..4 are prior revisions; the r4 plan text below is historical, read-only) ; attempt: **n=1/3**
- contract revision: **rv5**; evidence dir `.pdca/T246/rv5/`; failure patch `.pdca/T246/rv5/failure/t246-r5-incomplete.patch`
- status: **incomplete** (terminal disposition recorded 2026-10-20; measured acceptance gate FAIL + insufficient benchmark budget — not a pass, not a waiver)
- phase: **terminal — DO stopped after D5-05 close** (D5-01..D5-05 done; D5-06 never started). Candidate rows E246-409/410 measured **FAIL**; E246-414 **met**; D5-06 obligations E246-408/411/412/413 **not run / GAP** (explicitly not waived)
- plan_state: **terminal `incomplete`** (planner decision; r=5 stays the last plan revision — no r=6 was opened, no attempt reset)
- prior terminal: rv3 GAP, host/measurement failure (`artifacts/pdca/T246/rv3/GAP.md`; `docs/specs/status/D246-raw-datareader-3.md`)

## Durable state

- Current cycle: N=1
- Plan revision: r=5
- Attempt: n=1/3
- Phase: **terminal `incomplete`** — DO stopped after D5-05; D5-06 never started; no CHECK verdict was issued by DO
- Terminal disposition (2026-10-20, planner decision): status **`incomplete`**; counters frozen and unchanged — **N=1, r=5, n=1/3, rv=5**. Reason: measured acceptance gate **FAIL** on three admissible candidate blocks (10k U **1.136955–1.180848**, 10k A **4,132,558–4,236,841 ns** vs the 3,942,000 ns cap, 10k allocation ratio **1.003080–1.003197**, empty U **1.763487–1.792835**, empty allocation ratio **5.040323**) plus budget exhaustion risk — remaining **1,334.35 s** cannot finance the three required admissible blocks (~1,530 s). Obligations: E246-409/410 measured **FAIL**, E246-414 **met**, E246-408/411/412/413 **not run / GAP** (explicitly not waived).
- Failure patch (preserved uncommitted evidence): `.pdca/T246/rv5/failure/t246-r5-incomplete.patch` — 34 paths, 4,285 lines, 236,238 B, sha256 `b226d4061d23f2c584061f15e5ca15919a36965fb9e2cc56feb7c0d53290b83d`. The working tree was **not** reverted; nothing staged/committed/pushed/merged.
- Evidence root (complete, on disk, excluded from the patch by design): `.pdca/T246/rv5/**` — `manifest.json`, `gate.json`, `audit.json`, `perf/baseline/block-1`, `perf/candidate/block-{1,2,3}`, `cached-path/`, `attribution/`, `tests/`, `harness/`, `failure/t246-r5-incomplete.patch`.
- Prior revisions: r=1 (`docs/specs/status/D246-raw-datareader-1.md`), r=2 (`-2.md`), r=3 (`-3.md`), r=4 (plan text below) — historical, read-only
- Prior evidence: `artifacts/pdca/T246/rv1`, `rv2`, `rv3`, `.pdca/T246/rv4/**` — historical, read-only (rv4 never rewritten)

## Defect history

| key | revisions/attempts | fixes applied | evidence | last recurrence | escalation outcome |
|---|---|---|---|---|---|
| `perf-oracle-window-noise` | r=1/n=1 | 0 | `docs/specs/status/D246-raw-datareader-1.md:457-459` (10k B spread 50.08 %; empty Error/Mean 8.49–11.74 %) | 2026-10-17 baseline2 | planner terminal GAP |
| `log-per-op-vs-iteration-total` | r=2 | fixed | `artifacts/pdca/T246/rv2/gap-disposition.json:111-137` | — | — |
| `literal-job-id-vs-resolved-job` | r=2 | fixed | same | — | — |
| `idle-vbcscompiler-foreign-activity` | r=2 | fixed | same | — | — |
| `foreign-competing-workload-mid-block` (invalidity reason, not a product defect key) | r=3 | 0 | `artifacts/pdca/T246/rv3/GAP.md:22-32`; `rv3/perf/drift-guard.json:237-281` | r=3 | terminal GAP (host/measurement) |
| `defunct-csc-foreign-false-positive` | r=4/n=1 | 1 | `.pdca/T246/rv4/perf/baseline/block-1/manifest.json` (`host_monitor.foreign_pids`: pid 3503625, `csc`, `<defunct>`); `.pdca/T246/rv4/perf/baseline/block-2-canceled-1/canceled.json` | r=4 block 1 | harness fix applied: zombies filtered from foreign-workload classification; attempt 1 invalidated, attempt 2 canceled before completion and resumed |
| `cached-path-multi-report-aggregation` | r=4/n=1 | 1 | `.pdca/T246/rv4/perf/acceptance-window-attempt-1.json` (`cached-path-report-missing`; BDN writes one report per class) | r=4 acceptance-window attempt 1 | harness fix applied: all class reports of the single command are aggregated; cached-path re-executed in attempt 2 |
| `reader-seam-cache-ownership` | r=4/n=1 | 0 (levers reverted) | `.pdca/T246/rv4/tests/lever-rejected-ownership-7fail.log`; `.pdca/T246/rv4/tests/sqlite-full-revert.log`; `.pdca/T246/rv4/gate.json` | r=4 D4-04 | STOP: lever unsupported without a broader cache-contract change (per-call command clone or separate preparation cache); return to PLAN |
| `t246-gate-fail-recipe-lever` (measured outcome, not a harness defect) | r=5/n=1 | 0 | `.pdca/T246/rv5/gate.json` (`status: FAIL`, 3 admissible blocks); `.pdca/T246/rv5/perf/candidate/block-{1,2,3}/manifest.json` | r=5 candidate blocks 1–3, 2026-10-19T20:59Z | measured FAIL recorded as-is: 10k U 1.137–1.181 > 1.00, 10k alloc 1.0031–1.0032 > 1.00, 10k A OPEN (4.13–4.24 ms vs 3.942 ms), empty U 1.763–1.793 > 1.00, empty alloc 5.0403 > 1.00; no MET manufactured, ACT/CHECK decision pending |

## Progress log

2026-10-19T13:44:43Z | PLAN | r=4 | n=1/3 | PLAN ready — awaiting collection handoff | `.pdca/status/T246-raw-datareader-1.md`; re-entry branch (b)+process repair (a); oracle redefined (paired-block 99.9%); rv4 contract

> **HISTORICAL — r=4 plan text (read-only, superseded by r=5).** Kept verbatim for provenance; the active plan is `# P:T246-r5` below.

# P:T246-r4 — Obtain admissible evidence and remove repeated raw-reader preparation

## 1. Goal and acceptance

Make ordinary, unprepared `ToDataReader()` competitive without changing results, ownership, LOB behaviour, or requiring caller-side `Prepare()`.

| Requirement | Acceptance | Negative case |
|---|---|---|
| 10,000 rows | Time upper bound **≤3.942 ms**, allocation **≤943 KiB**, time/allocation ratios to linq2db **≤1.00** | Any applicable bound exceeding its threshold cannot pass |
| Empty result | Time upper ratio and allocation ratio **≤1.00** | No tolerance, rounding allowance, or averaging with 10k |
| Standard cached path | Exactly **7 cases**, no failures, **≤240 s total wall clock**; per-case wall time, Mean, Allocated and cached/prepared ratio recorded | Missing case/metric, failure or overrun fails this gate |
| Results | Same schema, values, null handling, row count and supported reader operations | Wrong projection, stale parameter, lost row or changed exception fails |
| Ownership | Preserve `CommandReaderOwner` close/dispose/async-dispose semantics | Leak, double disposal, premature closure or ownership change fails |
| LOB compatibility | Preserve `ToStream`, `ToTextReader` and raw-reader LOB operations | Broken locator/sequential behaviour fails |
| Preparation | No caller preparation requirement; explicit cache opt-out respected | Requiring `Prepare()` or mutating sticky `QueryCommand.Cache` fails |
| Portability | Dialect-driven preparation; live cross-provider streaming report | SQLite hardcoding or an untested affected dialect branch fails |
| Deliverables | Direct-provider control, benchmark/gate documentation and CI evidence validation | Screenshots or synthetic checks alone are insufficient |

The standard cached-path suite is **an independent mandatory regression gate**, not part of the T246 statistical oracle.

## 2. Re-entry disposition and counters

### Changed proof

Choose **branch (b)**: replace the old per-launch `(MeanN+ErrorN)/(MeanL−ErrorL)` oracle with the pre-registered paired-block oracle in §7.

Also repair the process under **(a)**:

- Collection lane cap **1**.
- Exclusive host lock and continuous foreign-workload monitoring.
- One authoritative manifest with conjunctive admissibility.
- Empty-arm invocation count fixed at **8192**.
- Separate execution, harness-validity, lane-admissibility and acceptance states.

This does not guarantee that foreign work cannot recur. It guarantees that contaminated evidence cannot produce acceptance: the whole affected block becomes invalid, and invalidation propagates into the final manifest.

### Historical reconciliation

The final `rv3/GAP.md` lane-admissibility decision governs historical acceptance. The old `measure-status.json: PASS` establishes, at most, execution/harness validity—not three lane-admissible runs.

Preserve both records unchanged. Document the distinction in rv4 and test that the new status model cannot reproduce the divergence.

### State

- Continue **N=1/r=4/n=1**.
- The prior terminal cycle never reached **ACT**; therefore N does not grow.
- Reconcile the collection’s `N=1/r=0` placeholder to this basis.
- Status file: **`.pdca/status/T246-raw-datareader-1.md`**.
- Evidence: **`.pdca/T246/rv4/`**.
- `rv4` supersedes the rv3 **measurement protocol**, not historical evidence.
- Preserve existing requirement/row IDs and obligations, particularly `R246-02`.
- Original unfinished product work remains **active**, blocked on the new prerequisites—not superseded or done.
- These corrections complete the unpersisted r4 plan; they do not create r5 or reset attempts.
- No fourth failed CHECK of this revision; apply collection escalation rules.

## 3. Minimal solution and alternatives

**What changes?** Remove repeated preparation from ordinary raw-reader calls where existing cache semantics support it.

**What must remain?** Results, LOB routing, ownership, cancellation, explicit cache opt-out, provider independence and caller API.

**Why this minimum?** `storeInCache:false` demonstrably forces repeated preparation. Its contribution to the observed slowdown remains unmeasured.

| Approach | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Cache-compatible preparation for `ToDataReader` only | Targets known per-call SQL/command preparation | Cache identity, parameters and reader-purpose compatibility require proof | **Chosen, subject to spike** |
| Native-reader bypass or redesigned adapter fast path | Could reduce per-row overhead | Broader metadata, sequential-access, LOB and ownership risks | Deferred pending measured residual |
| Caller `Prepare()`, SQLite hardcoding or relaxed thresholds | Easier apparent result | Violates constraints | Rejected |

Do not change sibling terminal behaviour merely to obtain raw-reader caching.

Use the existing cache only if its identity covers query shape, parameter rebinding and reader preparation mode. If a broader cache-contract change is necessary, return to PLAN; do not silently reuse incompatible entries or introduce another cache.

## 4. Reconnaissance and attribution — required

Sources:

- `QueryCommandExtensions.cs:326–346,495–532`.
- `DataContext.cs:474–479,488–504,511–516,563–589`.
- `QueryPlanner.cs:695–802,875–879`.
- `LobDataReader.cs:339,393`.

Before product edits:

1. Roslyn-map preparation, cache opt-out and ownership paths.
2. Inspect cache identity and parameter rebinding.
3. Add a direct-provider-reader control with matched fixture, query shape, connection lifecycle, command behaviour and row-consumption work.
4. Measure prepared A, ordinary B, linq2db and direct-provider controls for empty and 10k.
5. Obtain allocation/call-stack attribution with available .NET diagnostic tooling; record exact tool/version/invocation.

**Observable decision:**

- Proceed with caching only if repeated preparation is observed on B, reduced/absent on the compatible prepared path, and cache/parameter safety is demonstrable.
- Report per-call and row-dependent residuals separately.
- Do not assign the historical 19% to preparation or the adapter from timing subtraction alone.
- Unsafe caching or evidence requiring an adapter redesign returns to PLAN before product changes.

**Cost:** one unit; at most **1,200 s** of the shared benchmark budget. No speculative adapter rewrite is authorised.

## 5. Execution units

New paths below are **planned**, not claims that files or test symbols already exist.

| Unit | Work and anchors | Disposition / verification |
|---|---|---|
| **D4-01** | Persist status/contract; reconcile historical GAP, counters, inherited IDs and unfinished work | Fix now; no product edit |
| **D4-02** | Paired harness, atomic statuses, contamination propagation, fixture/provenance checks | Fix now; Python tests and old/new self-tests |
| **D4-03** | Attribution/cache-safety spike; direct-provider control | Fix now; stop/replan if chosen lever is unsupported |
| **D4-04** | Cache-compatible raw-reader preparation at `DataContext.cs:488–504,563–589`; entry points `:474–479,511–516`; extension routing only if needed | Fix now; core and SQLite unit projects |
| **D4-05** | Cache/parameter/ownership tests and live-provider regression cases | Fix now; local builds, live execution at boundary |
| **D4-06** | Baseline/candidate measurement and complete evidence bundle | Fix now; provisional until CHECK |
| **D4-09** | Mandatory standard cached-path suite: exactly 7 cases, ≤240 s; report all required metrics | Fix now; inside the final locked quiet window, separate from T246 oracle |
| **D4-07** | Documentation, CI validator and scope audit; CI staging exception in §6 | Fix now |
| **D4-08** | Single DO→CHECK boundary sweep and independent adjudication | Required; only CHECK concludes acceptance |

Deferred:

- Adapter/native-reader redesign: trigger is attributed residual cost preventing acceptance.
- General `ToStream`/`ToTextReader` caching changes: trigger is a separately demonstrated defect.
- Other performance issues and foreign changes: outside this task.

## 6. Unit mode and footprint

### Execution mode

**Sequential in the current worktree:**

```text
/home/alex/sources/nextorm
branch 1.0.9-rc3
```

No task/group worktree, branch or isolation merge is planned or permitted in this collection mode.

Isolation consists of:

- Explicit-path staging.
- The CI-only hunk staging protocol below.
- Single collection lane.
- Host lock and exclusive quiet measurement window.
- Pre/post foreign-change audits.

Collection-authorised commits may contain only T246 changes. Push remains prohibited.

### Production footprint

- `src/nextorm.core/DataContext/DataContext.cs`
- `src/nextorm.core/Query/QueryCommandExtensions.cs`

Read-only semantic dependencies unless replanned:

- `DataContext/QueryPlanner.cs`
- `DataContext/QueryExecutor.cs`
- `DataContext/CommandReaderOwner.cs`
- `LobDataReader.cs`, sibling wrappers and shared command/cache implementations.

### Tests and benchmark

- `tests/nextorm.core.tests/ToDataReaderCacheTests.cs` — planned.
- `tests/nextorm.sqlite.tests/ToDataReaderCacheSqlTests.cs` — planned.
- `tests/nextorm.integration.tests/CommonTestSuite.ToDataReaderCaching.cs` — planned.
- `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs`.

The existing seven standard acceptance methods are measured but not modified.

### Harness, docs and evidence

- `.pdca/scripts/perf/t246_paired.py` — planned.
- `.pdca/scripts/perf/t246-paired-budgets.json` — planned.
- `.pdca/scripts/perf/tests/test_t246_paired.py` — planned.
- `.pdca/status/T246-raw-datareader-1.md`.
- `.pdca/T246/rv4/**`.
- `.github/workflows/dotnet.yml` — **T246-owned hunks only**.
- `docs/specs/performance/comparison-benchmark-scenarios.md`.
- `docs/specs/performance/acceptance-benchmarks.md`.
- `docs/specs/performance/t246-raw-reader-gate.md` — planned.

### CI collision: choose option (a)

Keep CI wiring because it is a deliverable, but never stage the entire workflow.

1. Record initial index/worktree status and the foreign workflow hunk at `@@ -30,3 +30,4 @@`, including its hash.
2. Preserve that foreign hunk unchanged.
3. Add non-overlapping T246 hunks.
4. Produce a reviewed patch containing **only T246 hunks**.
5. Stage with:

   ```bash
   git apply --cached --check .pdca/T246/rv4/staging/t246-ci.patch
   git apply --cached .pdca/T246/rv4/staging/t246-ci.patch
   ```

6. Audit:
   - HEAD→index contains only T246 workflow changes.
   - Index→worktree retains the original foreign hunk unstaged.
   - No foreign change entered any staged path.
7. If safe separation is impossible, stop and return to PLAN; do not stage the file wholesale or move the foreign hunk elsewhere.

Foreign snapshots remain local audit evidence, not material to commit. The evidence/build digest does not incorporate foreign workflow changes as T246-owned source.

**Never:** `git add -A`, whole-file staging of this workflow, blanket reset/restore, or staging foreign paths.

**Outside footprint:** all other foreign dirty paths; prior status/evidence revisions; tracked `benchmarks/BenchmarkDotNet.Artifacts/**`; DB contents; unrelated provider/product code; package versions.

## 7. Performance protocol and oracle

### Planned harness interface

These are specified future CLI entry points, not claims about existing commands:

```text
python3 .pdca/scripts/perf/t246_paired.py self-test
python3 .pdca/scripts/perf/t246_paired.py reconcile --manifest .pdca/T246/rv4/manifest.json
python3 .pdca/scripts/perf/t246_paired.py preflight --manifest .pdca/T246/rv4/manifest.json
python3 .pdca/scripts/perf/t246_paired.py attribution --manifest .pdca/T246/rv4/manifest.json
python3 .pdca/scripts/perf/t246_paired.py measure --manifest .pdca/T246/rv4/manifest.json --phase baseline
python3 .pdca/scripts/perf/t246_paired.py measure --manifest .pdca/T246/rv4/manifest.json --phase candidate
python3 .pdca/scripts/perf/t246_paired.py cached-path --manifest .pdca/T246/rv4/manifest.json
python3 .pdca/scripts/perf/t246_paired.py acceptance-window --manifest .pdca/T246/rv4/manifest.json
python3 .pdca/scripts/perf/t246_paired.py gate --manifest .pdca/T246/rv4/manifest.json
python3 .pdca/scripts/perf/t246_paired.py ci-check --manifest .pdca/T246/rv4/manifest.json --source-root .
python3 .pdca/scripts/perf/t246_paired.py boundary --manifest .pdca/T246/rv4/manifest.json
python3 .pdca/scripts/perf/t246_paired.py audit --manifest .pdca/T246/rv4/manifest.json
```

Exit convention:

- **0:** satisfied.
- **1:** measured/gate failure.
- **2:** invalid or incomplete evidence.
- **3:** exhausted-budget GAP.

Never convert 2/3 to success.

### Cases and baseline

Acceptance measures ordinary **B**, not prepared A:

- B versus linq2db, 10k.
- B versus linq2db, empty.

Report prepared A, direct-provider and Dapper controls separately.

Baseline is unchanged product at `0d4b0cf6` with the new harness/controls. Preserve benchmark-source hashes and show that control additions did not alter existing benchmark work.

- Baseline: **one admissible diagnostic block**.
- Candidate: **three admissible blocks**.
- Historical measurements do not substitute for either.

### Fixed paired-measurement settings

- Out-of-process measurement, not default InProcess/ShortRun.
- BDN **0.15.8**.
- One launch per arm execution.
- Five warmups, ten measured iterations, unroll 1.
- Invocation counts: 10k **64**; empty **8192**.
- No adaptive configuration after candidate collection starts.
- Preserve resolved jobs and raw exports.

Each block contains eight adjacent N/L pairs per case:

```text
NL, LN, LN, NL, NL, LN, LN, NL
```

Alternate the starting case between blocks. Controls are not pooled into acceptance.

### Pre-registered T246 oracle

For each case and block, with eight pairs:

- \(N_i,L_i\): arithmetic mean operation time from each arm’s measured iterations.
- \(d_i=\ln(N_i/L_i)\).
- \(q=t_{0.999,7}\), computed by a tested implementation.
- Relative upper bound:

\[
U=\exp(\bar d+q\,s_d/\sqrt8)
\]

For absolute 10k time:

\[
A=\bar N+q\,s_N/\sqrt8
\]

Every one of the three admissible candidate blocks must satisfy:

- \(U_{10k}\le1.00\).
- \(A_{10k}\le3{,}942{,}000\) ns.
- \(U_{empty}\le1.00\).
- Every paired allocation ratio \(Alloc_N/Alloc_L\le1.00\).
- Every 10k candidate allocation **≤965,632 bytes**.

Confidence is **one-sided 99.9% per reported bound**, not family-wise confidence.

Use unrounded bytes and times. No epsilon, empty tolerance, minimum-of-N, best-block selection, or pooling between cases.

Also compute corresponding lower bounds:

- Upper bound meets threshold: **MET**.
- Lower bound exceeds threshold: **FAIL**.
- Interval straddles threshold: **OPEN**, not accepted and not automatically a product defect.
- Allocation excess: **FAIL**.

All three blocks must be MET.

### Qualification

The old marginal-error/mode-spread caps are replaced, not retained as hidden filters.

A block is admissible only when:

- Every scheduled execution/iteration exists.
- Fixture, source, binary, job and case identities match.
- Times are finite/positive; allocations and result consumption are valid.
- Lock remains held.
- No competing collection lane, build, test or benchmark workload overlaps.
- No foreign-workload event occurs during the block.

No statistical outlier deletion. Poor precision produces OPEN, not a discard.

Monitor at least once per second: PID/start identity, ancestry, command and CPU deltas. Distinguish idle compiler infrastructure from active competing work.

Contamination invalidates the **whole block**; retain its raw data and reason.

### Mandatory standard cached-path gate

Run exactly:

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

The harness launches this command unchanged while the host lock is already held.

Expected seven method-level cases:

- `InMemoryBenchmarkAggregates.cs:40`
- `InMemoryBenchmarkGroupBy.cs:40`
- `SqliteBenchmarkWhere.cs:132`
- `SqliteBenchmarkCachedPlan.cs:44,59,73`
- `SqliteBenchmarkAny.cs:126`

Resolve and record their actual symbols with Roslyn; do not invent them.

Required:

- Exactly seven resolved and completed cases.
- Zero failures.
- Total command wall clock **≤240 s**, including startup/build overhead.
- Per-case measured wall interval, Mean and Allocated.
- Cached/prepared Mean ratio, using the correctly matched methods; record numerator/denominator identities.

Timestamp real per-case execution intervals; do not estimate them by dividing total time by seven.

Use the suite’s standard configuration, separate from the paired jobs. No invocation-count or case reduction to meet the deadline. Timeout terminates descendants and records **gate failure**, not an invalid block or silent retry.

Its numbers never enter the T246 paired oracle.

### Budget and retries

**7,200 s cumulative wall time in benchmark commands**, including startup/generated builds, attribution, controls, baseline, candidate and standard cached-path suite.

- Attribution/control allowance: **≤1,200 s**.
- Baseline: at most two attempts for one admissible block.
- Candidate: at most five attempts for three admissible blocks.
- Standard suite: one final gate execution, **≤240 s**.
- Invalid attempts consume budget.
- Valid OPEN/FAIL blocks cannot be rerun for favourable replacement.
- Exhaustion yields GAP, not relaxed thresholds.
- Cached-path overrun is a gate failure.

### Lock and quiet-window protocol

Every BDN execution must run under:

```bash
flock -x /tmp/opencode/nextorm-bdn-host.lock \
  python3 .pdca/scripts/perf/t246_paired.py <phase-command> \
  --manifest .pdca/T246/rv4/manifest.json
```

Attribution/baseline have their own guarded exclusive segments before product edits.

The final command is:

```bash
flock -x /tmp/opencode/nextorm-bdn-host.lock \
  python3 .pdca/scripts/perf/t246_paired.py acceptance-window \
  --manifest .pdca/T246/rv4/manifest.json
```

It retains one lock continuously across final pre-flight, standard cached-path gate, candidate measurement and controls. Child commands inherit the lock; they must not independently reacquire it.

All other collection lanes remain suspended. No parallel tests/builds overlap measurement. BDN’s own serial preparation happens outside timed operations and is recorded.

## 8. Measurement pre-flight

Before timed work:

1. Record lane cap **1**, lock ownership and process baseline.
2. Verify no competing build/test/BDN workload; monitor continuously.
3. Hash the actual opened DB and require:

   ```text
   32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295
   ```

   Recheck after measurement. Path, size and MD5 are insufficient.

4. Record actual fixture path and validate row/schema expectations.
5. Verify Podman:

   ```bash
   DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock podman info
   ```

6. Prebuild Release; record SDK/runtime, BDN version, source digest, binaries, resolved jobs and logs.
7. Require zero warnings/errors.
8. Audit environment so paired-job overrides do not leak into the standard acceptance suite.
9. Write outputs only under `.pdca/T246/rv4/`, never tracked BDN artifact directories.
10. Snapshot tracked artifact status. Preserve any unexpected task-produced output, then restore **only task-produced changes**—never foreign dirty artifacts.
11. Record initial workflow/index state for hunk-level staging.
12. Fixture/process/provenance failure stops measurement; it is not an admissible slow result.

## 9. Test strategy and variant matrix

FluentAssertions only; CRLF preserved; no package/version changes planned.

| Variant | Closure |
|---|---|
| Locator dialect, including SQLite | Live raw-reader/LOB values and ownership tests |
| Non-locator dialect | Live container-backed tests and branch report |
| Scalar/non-LOB projection | Unit contract plus live schema/value checks |
| Binary/text LOB and null LOB | Live raw-reader, `ToStream`, `ToTextReader` tests |
| Empty / one row / 10k | Unit/SQL and live empty/one-row; benchmark 10k |
| Sync / async | Unit ownership counters plus live execution |
| Cancellation before/during open/read | Deterministic unit faults; live provider-supported cancellation |
| Close / Dispose / DisposeAsync / repetition | Exact-once unit checks; live connection remains usable |
| Execution/read/disposal exceptions | Unit cleanup and ownership tests |
| Prepared / unprepared | Both tested; acceptance uses unprepared |
| Explicit cache opt-out | Guard and regression test; no sticky state mutation |
| Captured parameters / repeated calls | Unit/live fresh-value tests |
| Different shapes / interleaved terminals | Cache isolation regression |
| Sequential / non-sequential | Live supported paths; retain unsupported-path guards |
| Value/reference/nullable fields | Unit/live projection coverage |
| Null inputs / unsupported context | Existing guard behaviour preserved |
| Concurrent operations on one context | Deferred unless already supported; trigger: affected concurrency contract |
| Additional provider outside harness | Deferred; trigger: new provider or affected dialect implementation |

Every affected semantic case must execute. A feature skip is not evidence of passing that case.

### Local selectors

Harness:

```bash
python3 .pdca/scripts/perf/t246_evidence.py self-test
python3 .pdca/scripts/perf/t246_paired.py self-test
python3 -m unittest discover -s .pdca/scripts/perf/tests -p 'test_t246*.py'
```

Product/cache:

```bash
dotnet test tests/nextorm.core.tests -c Debug
dotnet test tests/nextorm.sqlite.tests -c Debug
```

Benchmark:

```bash
dotnet build benchmarks/nextorm.benchmark -c Release
```

New integration tests are built locally and executed live at the boundary. Project-level selectors intentionally avoid fabricated test symbols.

### Single boundary sweep

After code freeze and outside the measurement window:

- `dotnet build nextorm.slnx -c Debug`: zero warnings/errors.
- Every non-integration solution test project once.
- `tests/nextorm.integration.tests` once, live, under coverage.
- Convention checks included; discovered/executed counts recorded.
- Load the repository integration-running skill first and follow its Podman recovery procedure.
- PostgreSQL, SQL Server, MySQL and ClickHouse must actually execute.
- Enforce current pinned skip policy; historical 199 skips does not enlarge it.

The planned `boundary` command enumerates solution test projects, logs expanded commands and fails on missing reports, zero tests or unapproved skips.

Integration collection:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
dotnet tool run dotnet-coverage collect \
  -s coverage.integration.settings.xml -f cobertura \
  -o .pdca/T246/rv4/check/integration.cobertura.xml \
  "dotnet test tests/nextorm.integration.tests -c Debug --no-build --verbosity normal"
```

Collect unit coverage separately; merge with this same integration execution for whole-project reporting. Do not rerun integration solely for another denominator.

Unchanged thresholds:

- Whole-project: **85% line / 75% branch**.
- Integration-only: **65% line / 75% branch**, with the prescribed `NextORM.Core.InMemory*` exclusion.

Record literal Summary and counted results. Branch warning behaviour does not authorise weaker acceptance.

## 10. Priority matrix

| Priority | Obligations |
|---|---|
| **P1 by requirement** | T246 performance, standard cached-path gate, results/LOBs, ownership, no caller preparation, portability, shared-command invariant |
| **P1 execution path** | Cache identity, rebinding, cancellation/error cleanup, preparation routing |
| **P1 evidence integrity** | Fixture, exclusivity, provenance, contamination propagation, stale/missing evidence rejection |
| **P2** | Documentation formatting and explanatory narrative |
| **Deferred** | Broader adapter rewrite and unrelated terminal optimisation |

CHECK applies these priorities without downgrade.

## 11. Documentation and CI

Update:

- `comparison-benchmark-scenarios.md`: direct control, empty/10k and prepared/unprepared distinction.
- `acceptance-benchmarks.md`: T246 gate row and separate mandatory seven-case cached-path row.
- New `t246-raw-reader-gate.md`: thresholds, oracle, standard suite, exclusivity, statuses and commands.
- Cycle status: decisions, matrices, counters, dependencies and results.

**Public documentation and `docs/ru/**`: not touched.** No public rename or semantics change is planned. No new XML documentation is mandated. A public-surface change requires replanning and bilingual documentation.

CI wiring in `.github/workflows/dotnet.yml`, using §6’s hunk-only protocol:

1. Run old/new harness self-tests.
2. Validate the minimal rv4 evidence bundle with `ci-check`.
3. Require source/build-input identity, raw inputs, admissibility, T246 MET results and the seven-case cached-path result.
4. Prove missing/stale/tampered/contaminated evidence fails.
5. Do not substitute hosted-runner timings or synthetic checks for exclusive-host acceptance.

The CI bundle contains recomputable raw exports and validity/provenance inputs. Exclude binaries, DB files, foreign snapshots and unrelated BDN output.

## 12. What the statement did not say

| Gap | Closure |
|---|---|
| Owner tolerance | None; `R246-02` remains binding |
| Whether the host is now quiet | Not assumed; pre-flight and continuous monitoring |
| Source of slowdown | Mandatory attribution; no asserted 19% breakdown |
| Cache-purpose compatibility | Semantic proof/tests required; otherwise replan |
| Meaning of KB | **943 KiB = 965,632 bytes**, unrounded |
| Old PASS versus GAP | Harness validity is not lane admissibility; final GAP governs |
| CI exclusive-host availability | CI validates the exclusive-host evidence bundle |
| Contract text/inherited IDs | Reconcile canonical `pdca-dotnet` and inherited rows before product DO; targeted scout if missing |
| Historical skips | Current pinned policy governs |
| Status suffix | Cycle **N=1**, hence `-1.md`; rv remains 4 |
| Current-worktree isolation | Explicit staging, CI hunk separation, lock and quiet window—not a worktree |
| Standard suite metrics | Seven real case intervals and raw Mean/Allocated; matched cached/prepared identities |
| Foreign workflow hunk | Preserved unstaged; never included in T246 commit |

## 13. Versioned evidence contract

**Revision:** `rv4`.  
**Manifest:** `.pdca/T246/rv4/manifest.json`.  
**Status:** `.pdca/status/T246-raw-datareader-1.md`.

Before product DO, coder persists the complete contract and command expansions. Preserve inherited IDs/obligations verbatim. New rows below are additive; particularly, do not reinterpret `R246-02`.

Required manifest slots:

- Parent contract/status references and hashes.
- Explicit rv3→rv4 protocol supersession and rationale.
- Stable requirement and row IDs.
- Required check/scenario, kinds and sources of evidence.
- Exact invocation, expected exit/result/log.
- Expected artifacts, owner, observable applicability and `rv`.
- Source/build/job/fixture identities.
- Separate execution, harness-valid, lane-admissible and acceptance states.
- Immutable raw inputs, invalidation reasons and atomic final publication.
- Planned sources marked **planned** until obtained.
- No final PASS until every applicable mandatory row is satisfied.

All artifact paths below are relative to `.pdca/T246/rv4/`; every row has **rv=4**.

| Row / requirement | Evidence/source and invocation | Required result / artifacts | Owner; applicability |
|---|---|---|---|
| **E246-401 / re-entry and inherited obligations** | Status/repository reconciliation; `reconcile --manifest …` | Exit 0; counters/IDs resolved, old GAP preserved; `reconciliation.json`, `contract.json` | H; unconditional |
| **E246-402 / R246-02** | Issue/contract threshold audit; `audit --manifest …` | Exit 0; zero tolerance and frozen thresholds; `audit.json` | C; unconditional |
| **E246-403 / harness reliability** | Python tests and both self-tests in §9 | Exit 0; negative/positive logs, invalidation cannot retain PASS | H; unconditional |
| **E246-404 / measurement applicability** | Locked `preflight --manifest …` | Exit 0; fixture, lock, process and provenance logs; `preflight.json` | M; every BDN phase |
| **E246-405 / causal lever** | `attribution --manifest …`; Roslyn and measured controls | Exit 0 only for supported/safe cache lever; `attribution.json`, controls, semantic references | P/M; before product edit |
| **E246-406 / baseline** | Locked `measure --manifest … --phase baseline` | One admissible diagnostic block; `baseline/**`; no acceptance claim | M; unconditional |
| **E246-407 / cache correctness** | Core/SQLite commands in §9; diff review | Exit 0, nonzero tests; rebinding/opt-out intact; `tests/cache/**` | T/P; product change present |
| **E246-408 / results, LOBs and ownership** | `boundary --manifest …`, live integration | Exit 0; affected branches executed; `streaming-report.json`, test logs | T; unconditional |
| **E246-409 / 10k performance** | Candidate `measure`, then `gate --manifest …` | Exit 0; three MET blocks, all absolute/relative limits; `candidate/**`, `gate.json` | M/C; unconditional |
| **E246-410 / empty performance** | Same collection, independently evaluated | Exit 0; three MET blocks, zero tolerance; raw pairs and `gate.json` | M/C; unconditional |
| **E246-411 / build, tests and coverage** | `boundary --manifest …` | Exit 0; 0W/0E, tests/gates satisfied; `check/**` | T/C; unconditional |
| **E246-412 / CI gate** | `ci-check --manifest … --source-root .`; negative tests | Authentic bundle exits 0; missing/stale/tampered/contaminated bundle nonzero; `ci/**` | H/C; unconditional |
| **E246-413 / docs and scope** | `audit --manifest …`; pre/post index/worktree review | Exit 0; docs/CRLF correct, only T246 staged, foreign CI hunk untouched; `audit.json`, staging audit | P/C; unconditional |
| **E246-414 / standard cached-path gate** | Locked `cached-path --manifest …`, executing the exact §7 suite command | Exit 0 only for exactly 7 cases, no failures, ≤240 s, all metrics/ratio; `cached-path/results.json`, timestamped log and raw exports | M/C; unconditional because preparation/cache path changes |

`…` expands exactly to `.pdca/T246/rv4/manifest.json`.

### CHECK re-gather

Owner: **CHECK**.

- At most **two targeted requests**, **10 minutes total**.
- Allowed: retrieve existing logs/exports, verify hashes, resolve references.
- Not allowed: new measurements, edits, relaxed thresholds or dropped rows.
- Missing reports alone do not justify contract revision or another DO iteration.
- Persistent low confidence routes to `escalate`, trigger 5.

## 14. Risks and return classification

- **Unsafe cache identity/rebinding:** in-cycle prerequisite; replan before broadening production scope.
- **Safe but insufficient caching:** measured product shortfall; replan from attribution, preserving thresholds.
- **Valid OPEN intervals:** evidence gap, not automatically a code defect; no favourable rerolls.
- **Repeated foreign activity:** measurement failure; bounded diagnostics, then escalation if confidence remains low.
- **Unavailable external host/permissions/resources:** recommend orchestrator invoke `escalate`.
- **Missing canonical contract/inherited IDs:** targeted scout first; product DO stays blocked.
- **Cached-path timeout/failure:** mandatory gate failure, never hidden by a green T246 oracle.
- **CI hunks cannot be separated:** stop/replan; do not stage foreign material.
- **Additional footprint required:** explicit plan revision before edits.

Confidence is high in the known repeated-preparation path and historical measurement failure. The achievable speedup remains unproven until attribution and measurement.

## 15. Persistence and handoff

Coder writes the complete plan, matrices, rv4 contract, counter basis, historical reconciliation and active dependencies to:

```text
.pdca/status/T246-raw-datareader-1.md
```

Required log line:

```text
PLAN ready — awaiting collection handoff
```

The collection caller owns the PLAN→DO boundary; no user-approval pause is introduced.

For this invocation: persist PLAN only, then return to the collection caller. Do not run pre-flight, builds, tests, benchmarks or implementation.

2026-10-19T16:24:02Z | DO | r=4 | n=1/3 | DO started — units D4-01..D4-09, brief validated (exit 0), rv4 evidence root created | `.pdca/T246/rv4/`; `/tmp/opencode/t246-brief.json`
2026-10-19T17:03:30Z | DO | r=4 | n=1/3 | D4-01 close — status/counters/rv3 GAP reconciled (reconcile exit 0) | `.pdca/T246/rv4/reconciliation.json`
2026-10-19T17:03:30Z | DO | r=4 | n=1/3 | D4-02 close — paired harness, budgets, Python tests (12) + old tests (63) + both self-tests green | `.pdca/scripts/perf/t246_paired.py`; `.pdca/T246/rv4/harness/`
2026-10-19T17:03:30Z | DO | r=4 | n=1/3 | D4-03 evidence — direct-provider control + attribution + Roslyn cache-identity map; non-sequential seam proven cache-compatible, sequential lever proven unsafe | `.pdca/T246/rv4/attribution/`
2026-10-19T17:03:30Z | MEASURE | r=4 | n=1/3 | baseline attempt 1 invalidated — defunct csc misclassified as foreign workload (`defunct-csc-foreign-false-positive`); fix applied, attempt 2 canceled and resumed | `.pdca/T246/rv4/perf/baseline/block-1/manifest.json`
2026-10-19T17:04:45Z | MEASURE | r=4 | n=1/3 | baseline block-1 manifest reconstructed from raw exports after a resume stub overwrite; block remains invalid (`foreign-workload-event`, defunct csc, pre-fix); baseline block-2 rerun in progress | `.pdca/T246/rv4/perf/baseline/block-1/manifest.reconstructed.json`
2026-10-19T17:21:37Z | DO | r=4 | n=1/3 | D4-04 close — non-sequential result-reader preparation is cache-compatible (`storeInCache: !sequentialAccess`); sequential LOB path stays uncached | `src/nextorm.core/DataContext/DataContext.cs:563-589`
2026-10-19T17:21:37Z | DO | r=4 | n=1/3 | D4-05 close — red 2/4 (cache reuse, before fix) → green core 4/4 + sqlite 4/4; integration cases added (built/run at boundary) | `.pdca/T246/rv4/tests/`
2026-10-19T17:21:37Z | MEASURE | r=4 | n=1/3 | baseline block-2 admissible (diagnostic): U10k 1.1749, A10k 4,658,871 ns, Uempty 2.1083; acceptance-window started (preflight, cached-path, 3 candidate blocks, post-change controls) | `.pdca/T246/rv4/perf/baseline/block-2/manifest.json`
2026-10-19T18:21:15Z | DO | r=4 | n=1/3 | D4-04 rejected — cache-compatible result-reader preparation violates exact-once CommandReaderOwner ownership; 7 existing ownership facts failed, change reverted (full sqlite suite 1796 passed / 0 failed / 1 skipped) | `.pdca/T246/rv4/tests/lever-rejected-ownership-7fail.log`; `.pdca/T246/rv4/tests/sqlite-full-revert.log`
2026-10-19T18:21:15Z | DO | r=4 | n=1/3 | STOP — chosen lever unsupported without a broader cache-contract change (per-call command clone or separate preparation cache); provisional candidate, return to PLAN before any product change | `.pdca/T246/rv4/attribution/semantic-safety.json`; `.pdca/T246/rv4/gate.json`
2026-10-19T18:42:24Z | DO | r=4 | n=1/3 | unit sweep green without the lever; integration caching facts (16/16 live) execute the guard on unsupported providers instead of adding capability skips (skip budget stays 195) | `.pdca/T246/rv4/tests/integration-caching-focused.log`
2026-10-19T18:55:38Z | DO | r=4 | n=1/3 | D4-06 close — baseline block-2 admissible; candidate 3 admissible blocks measured with the rejected lever as diagnostics; `gate` exit 1 (FAIL: U10k 1.147-1.163, A10k 4.204-4.305 ms, alloc ratio 1.0028-1.0029; empty U 1.799-1.817, alloc ratio 4.75-4.86); budget 3556.8/7200 s | `.pdca/T246/rv4/gate.json`
2026-10-19T18:55:38Z | DO | r=4 | n=1/3 | D4-09 close — standard cached path 7/7, 0 failures, 48.2 s, Cached_ToList/Prepared_ToList = 1.6936 | `.pdca/T246/rv4/cached-path/results.json`
2026-10-19T18:55:38Z | DO | r=4 | n=1/3 | D4-07 close — docs (EN spec pages) + CI hunk-only step added; CRLF verified; thresholds audit exit 0 | `.pdca/T246/rv4/audit.json`
2026-10-19T18:55:38Z | DO | r=4 | n=1/3 | D4-08 close — boundary GREEN: build 0W/0E; 11 unit projects pass; live integration 5099 total / 0 failed / 195 pinned skips; coverage whole 89.7/82.2, integration 81.4/75.4 | `.pdca/T246/rv4/check/summary.json`; `.pdca/T246/rv4/check/integration.log`
2026-10-19T18:55:38Z | EVIDENCE | r=4 | n=1/3 | frozen manifest written and validated: `validate_inner_loop.py manifest` exit 0; `pdca_evidence.py lint` exit 0; `ci-check` exit 2 (expected `gate-not-met`, correct fail-closed) | `.pdca/T246/rv4/manifest.json`; `.pdca/T246/rv4/manifest-validation.log`; `.pdca/T246/rv4/lint.log`; `.pdca/T246/rv4/ci/ci-check.json`
2026-10-19T18:55:38Z | EVIDENCE | r=4 | n=1/3 | DO report gate exit 2 (deviation recorded, not legalised): 2 focused live integration checks ran inside DO, 3 boundary integration executions vs 1 planned, out-of-scope filter `FullyQualifiedName~ToDataReaderCaching`; raw journal retains every start | `.pdca/T246/rv4/inner-loop/journal.json`; `.pdca/T246/rv4/inner-loop/project.err`
2026-10-19T18:55:38Z | DO | r=4 | n=1/3 | provisional candidate: D4-04/D4-05/D4-08 product acceptance OPEN (lever rejected, no product change); STOP and return to PLAN for a broader cache-contract change or a different lever; CHECK has not run | `.pdca/T246/rv4/manifest.json`

2026-10-19T19:15:00Z | PLAN | r=5 | n=1/3 | Replanned: r4 lever unsupported by per-call ownership (r 4→5, iteration 1/3) | `.pdca/T246/rv4/attribution/semantic-safety.json`; `.pdca/T246/rv4/gate.json`; `.pdca/status/T246-raw-datareader-1.md`
2026-10-19T19:15:00Z | PLAN | r=5 | n=1/3 | plan approved — DO started (r=5) | budget 3556.8/7200 s consumed, 3643.2 s remaining; r4 process deviation non-green; rv4 history read-only

# P:T246-r5 — immutable command-recipe cache for raw readers (supersedes r=4)

## 1. Goal / new P-task
P5: establish whether an opt-in immutable command-recipe cache can meet T246's unchanged raw-reader acceptance criteria while minting and binding a fresh command on every invocation, including cache hits. This is a real plan revision: add recipe construction/storage/binding and ownership verification; do not broaden ordinary planner consumers. Expected performance gain is unproven; the feasibility spike may fail.
Fix now: open rows 405/407/409/410/412, recipe-cache prerequisite, ownership evidence, rv5 harness routing. Previously met rows retain historical evidence but require rv5 applicability/provenance validation. Original unfinished D-units remain active and blocked on the new prerequisites; their acceptance criteria and remaining work are unchanged. A provisional candidate or blocker report does not complete them.

## 2. Frozen acceptance + ownership
Preserve R246-02 → E246-402/E246-410 without reinterpretation. For 10k: U≤1.00, A≤3,942,000 ns, allocation≤965,632 B; empty time and allocation ratios≤1.00 independently, with no tolerance, rounding or averaging.
Use one-sided 99.9% per bound, not family-wise: U=exp(dbar+q*s_d/sqrt(8)), q=t_0.999,7; upper MET/lower FAIL/straddle OPEN. All three candidate blocks must be MET.
Eight adjacent pairs/case: NL,LN,LN,NL,NL,LN,LN,NL; initializations 64/8192 for 10k/empty; BDN 0.15.8 out-of-process, 5 warmups/10 iterations/unroll 1. No epsilon, pooling, min-of-N or best-block selection.
Baseline: one admissible diagnostic block, never acceptance evidence. Candidate: three admissible blocks. Preserve rv4 exit-0/1/2/3 meanings verbatim; only an authentic exit-0/MET bundle can pass.
T246-OWN: every raw-reader invocation receives a distinct, freshly minted/bound command; no disposable command, reader, connection, mutable parameter collection or invocation state is cached/shared. CommandReaderOwner ownership and cleanup on failure/cancellation remain intact.
Negative acceptance: any breached bound, OPEN/GAP, stale binding, sticky cache mutation, reused/disposed command, ownership leak, invalid provenance or tampered PASS prevents acceptance.

## 3. Minimal solution / variant matrix
Purpose: reuse safe preparation work; constraints: unchanged oracle, per-call ownership, opt-out/rebinding, isolated raw-reader scope; optimum: cache immutable recipes only, with safe uncached fallback.
Alternatives: retain uncached preparation—lowest implementation risk, no demonstrated gain; reuse prepared commands—rejected, violates ownership; immutable recipe—chosen, moderate implementation cost and unproven speedup.
Recipe content must be immutable metadata sufficient to recreate SQL/options/parameter binding; parameter values and disposable resources remain invocation-local. Unsupported recipe shapes use the existing uncached path.
Variants closed: sync/async → tests; miss/hit/repeated/overlapping reader lifetimes → tests; cache enabled/disabled and shared Any/Count/All commands → tests; changed parameters/null/default/value/reference inputs → rebinding tests.
Variants closed: parameterless/parameterized and supported/unsupported recipe shapes → tests plus fallback guard; empty/10k → separate measured gates; sequential/non-sequential options → identity/option guards and tests.
Variants closed: normal disposal/open failure/execute failure/cancellation before and after open → ownership tests; SQLite → unit+live integration; other providers → common live integration; ordinary planner consumers → isolation guards/regression tests.
Deferred: new public caching APIs, unrelated planner consumers and provider-specific optimizations; trigger = a separately approved requirement with its own evidence contract.

## 4. Test strategy / exact commands
Use FluentAssertions, CRLF and warnings-as-errors. New test symbols are planned, not claimed discovered; filters below must select nonzero relevant tests, with discovery/counts recorded.
Focused commands: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~ToDataReader"`; `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ToDataReader"`.
Full unit commands: `dotnet test tests/nextorm.core.tests -c Debug`; `dotnet test tests/nextorm.sqlite.tests -c Debug`; build: `dotnet build nextorm.slnx -c Debug`, requiring 0 warnings/0 errors.
Harness commands: `python3 -m unittest discover -s .pdca/scripts/perf/tests -p 'test_t246*.py'`; `python3 .pdca/scripts/perf/t246_evidence.py self-test`; `python3 .pdca/scripts/perf/t246_paired.py self-test`.
Boundary: `python3 .pdca/scripts/perf/t246_paired.py boundary --manifest .pdca/T246/rv5/manifest.json`; integration_boundary_runs=1, owned by CHECK; no focused integration execution inside DO.
Load the integration-running skill first. The boundary owns the sole full live integration execution, with Podman recovery if necessary and DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock.
Its integration command is `dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o .pdca/T246/rv5/check/integration.cobertura.xml "dotnet test tests/nextorm.integration.tests -c Debug --no-build --verbosity normal"`.
Record provider execution and the inherited authorized skip budget; provider opt-out/skips cannot substitute for live evidence. Report unrun providers explicitly.
For whole coverage, collect all non-integration test projects using coverage.settings.xml, then merge that output with the single integration collection; restrict the whole report to core/sqlite/postgres/sqlserver.
Exact unit collection: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o .pdca/T246/rv5/check/unit.cobertura.xml "bash -ec 'for p in tests/*.tests/*.csproj; do case \"$p\" in tests/nextorm.integration.tests/*) continue;; esac; dotnet test \"$p\" -c Debug --no-build; done'"`.
Merge: `dotnet tool run dotnet-coverage merge -f cobertura -o .pdca/T246/rv5/check/whole.cobertura.xml .pdca/T246/rv5/check/unit.cobertura.xml .pdca/T246/rv5/check/integration.cobertura.xml`.
Report whole with `dotnet tool run reportgenerator -reports:.pdca/T246/rv5/check/whole.cobertura.xml -targetdir:.pdca/T246/rv5/check/whole-report -reporttypes:TextSummary "-assemblyfilters:+nextorm.core;+nextorm.sqlite;+nextorm.postgres;+nextorm.sqlserver"`; require line≥85%, branch≥75%.
Report integration with `dotnet tool run reportgenerator -reports:.pdca/T246/rv5/check/integration.cobertura.xml -targetdir:.pdca/T246/rv5/check/integration-report -reporttypes:TextSummary "-classfilters:-NextORM.Core.InMemory*"`; require line≥65%, branch≥75%; do not lower gates.
Mutation: no applicable T246 Stryker configuration exists; do not claim the postgres break-0 configuration as evidence. Use four temporary targeted mutants: command sharing, stale rebinding, opt-out bypass, failure-cleanup omission; focused core/SQLite commands above must kill each, then restore and pass.
P1 by construction: every execution-path, oracle, ownership, binding, opt-out, provenance and CI-authentication assertion in rows 401–415; CHECK cannot downgrade them. Missing unrelated mutation infrastructure is not permission to omit targeted mutation evidence.

## 5. Performance: reuse versus rerun
Measurement is required: QueryPlanner.cs:774,799,815–817,851–873 creates/stores/returns prepared-command instances on the execution path; this is not one-time work.
Reuse rv4 only as immutable historical evidence: reconciliation/audit inputs, oracle specification and baseline raw data where complete fixture/runtime/source/control provenance establishes comparability. Never relabel rv4 candidate results as rv5.
If baseline provenance is incompatible, rerun one admissible diagnostic baseline block before the candidate; otherwise reference its original manifest/tree/hash without rewriting it.
Rerun attribution, all three candidate blocks, independently evaluated empty bounds, correctness/ownership checks, cached-path controls and authentic/tampered CI validation against rv5.
Cached-path command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; require exactly the frozen seven cases, zero failures, ≤240 s, and the inherited metrics/ratio predicate, not a newly invented threshold.
Frozen seven: InMemoryBenchmarkAggregates.Nextorm_Count; InMemoryBenchmarkGroupBy.Nextorm_GroupByCount; SqliteBenchmarkAny.Nextorm_Cached; SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param; SqliteBenchmarkCachedPlan.Cached_ToList; SqliteBenchmarkCachedPlan.Prepared_ToList; SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync.
rv4's 7/7, 48.175 s, ratio 1.6936 remains historical only. Preserve all raw exports and timestamped logs.
Use `flock -x /tmp/opencode/nextorm-bdn-host.lock`; fixture SHA256 must equal 32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295.
Cumulative performance budget is 7200 s: consumed 3556.8 s, remaining 3643.2 s. Spike and additional performance diagnostics count; no reset by revision. Preserve baseline retry≤2/candidate retry≤5 ceilings and inherited counting semantics; budget exhaustion is GAP.

## 6. Bounded feasibility spike + gate
D5-02 spike: ≤300 s of additional performance-budget time, also bounded by the remaining cumulative budget; no integration runs. Prove the recipe can be immutable, hit-path command identities differ, rebinding works, and opt-out/fallback remain isolated.
Use a narrow instrumented/profiling probe to establish avoided preparation work and candidate timing feasibility; record exact invocation, source identity, elapsed time and raw observations before running it. Diagnostic numbers cannot establish acceptance.
Gate: proceed only with passing structural/ownership probes, a supported attributable lever, and a documented feasible schedule for all remaining mandatory measurements within the residual budget.
If these observations fail, or feasibility cannot be supported, return DO→PLAN with evidence; do not weaken criteria, run speculative unlimited alternatives, or declare success. The expected gain remains unproven until the frozen candidate gates pass.

## 7. Docs / CI
Update `docs/specs/performance/t246-raw-reader-gate.md` (NEW), `comparison-benchmark-scenarios.md`, `acceptance-benchmarks.md` for recipe ownership, rv5 provenance and unchanged gates; public docs must not link to internal specs.
Update only T246-owned `.github/workflows/dotnet.yml` hunks for authentic rv5 validation; preserve foreign hunks. No public API rename or unrelated documentation renumbering.
Keep rv4 artifacts/status sections read-only, preserve CRLF, and record the r4 process deviation as non-green: two focused DO integration runs plus three boundary runs versus one planned; `validate_inner_loop.py project` exited 2.

## 8. Unit mode / footprint
Sequential work in the existing tree: implementation, tests and harness share contracts; no worktree isolation benefit, commits, merges or pushes.
Production seams: `DataContext.cs:488–504,564–598`; `QueryPlanner.cs:29,74–94,582–591,659,774–873`; `QueryPlanStore.cs:90–183`; immutable precedent `ParamRefreshRecipe.cs:33,88,160`.
Planned NEW internal immutable recipe/storage support belongs under core Cache; resolve actual symbols with Roslyn before editing. Keep the public `IQueryPlanner` contract unchanged; route only raw-reader opt-in requests through the new behavior.
Read-only unless a demonstrated prerequisite requires PLAN revision: QueryExecutor, CommandReaderOwner, LobDataReader, unrelated planner consumers and QueryCommandExtensions; retain existing ownership paths.
Tests: NEW absent `tests/nextorm.core.tests/ToDataReaderCacheTests.cs`; NEW/untracked `tests/nextorm.sqlite.tests/ToDataReaderCacheSqlTests.cs` and `tests/nextorm.integration.tests/CommonTestSuite.ToDataReaderCaching.cs`; extend existing ownership tests at reported lines 216,440,467–650.
Harness: three existing untracked perf files plus their tests; change rv5 root routing without modifying rv4 artifacts. Benchmark: existing `SqliteBenchmarkStreaming.cs` T246 hunks. Audit pre-existing changes before any edit.

## 9. Versioned evidence contract
Contract rv=5 supersedes rv4 for current evaluation, retaining E246-401…414, all existing requirement mappings/obligations, R246-02→402/410, and historical GAPs; add E246-415→T246-OWN. rv4 remains immutable.
Manifest: `.pdca/T246/rv5/manifest.json`; record source-tree identity, parent rv4 tree 6cd4e681a0459d1d4e73172f05929203f5fe20cf, commands, applicability, artifact hashes, result classification, counters, timers and process deviations.
Notation: V=.pdca/T246/rv5, M=$V/manifest.json, E=python3 .pdca/scripts/perf/t246_evidence.py, P=python3 .pdca/scripts/perf/t246_paired.py. All sources below are planned, not already obtained.
Every following row has rv=5, applicability=true, owner=CHECK, immutable raw-source/log provenance and expected exit 0 unless its stated negative scenario requires nonzero. Existing requirement IDs remain; named T246-* labels below are additional stable aliases.
Commands below are planned rv5 interface obligations: D5-01 must verify/support these exact argv, including manifest routing, without altering the oracle; unverified CLI compatibility is not passing evidence.
E246-401 | req=T246-REC + inherited | `$E reconcile --manifest "$M"` | counters/IDs/old GAP preserved; reconciliation report and original-source references → `$V/reconciliation.json`, `$V/inputs/contract.json`; dropping history fails.
E246-402 | req=R246-02 + inherited | `$E audit --manifest "$M"` | frozen thresholds, statistics and zero tolerance match exactly; contract/source audit → `$V/audit.json`; any relaxation fails.
E246-403 | req=T246-HARNESS + inherited | three exact Python commands in §4 | positive and deliberately invalidated/tampered self-tests behave correctly; test output/logs → `$V/harness/**`; invalidation retaining PASS fails.
E246-404 | req=T246-PREFLIGHT + inherited | `$P preflight --manifest "$M"` | lock/fixture/process/runtime/source provenance valid; observations → `$V/preflight.json`; hash/lock/contamination mismatch fails.
E246-405 | req=T246-ATTRIBUTION + inherited | `$P attribution --manifest "$M"` | exit 0 only for supported safe recipe lever; controls, semantic Roslyn refs and spike observations → `$V/attribution.json`, `$V/controls/**`; unsafe/unsupported attribution fails.
E246-406 | req=T246-BASELINE + inherited | `$P measure --phase baseline --manifest "$M"` if rerun; otherwise `$E audit --manifest "$M"` validates explicit immutable reuse | exactly one admissible diagnostic block; raw/provenance → `$V/baseline/**`; acceptance claims or incompatible reuse fail.
E246-407 | req=T246-CACHE + inherited | full core/SQLite commands in §4 | cache miss/hit, opt-out, null/default and changed-value binding pass; test logs/results → `$V/tests/cache/**`; stale/shared state fails.
E246-408 | req=T246-BOUNDARY + inherited | exact boundary command in §4, including its sole live integration coverage invocation | streaming/lifecycle/provider behavior passes; live logs → `$V/streaming-report.json`, `$V/check/integration*`; unrun required providers fail.
E246-409 | req=T246-CANDIDATE + inherited | `$P measure --phase candidate --manifest "$M"` then `$P gate --manifest "$M"` | three MET blocks satisfy every 10k bound; paired exports/statistics → `$V/candidate/**`, `$V/gate.json`; FAIL/OPEN/GAP cannot pass.
E246-410 | req=R246-02 + inherited | `$P gate --manifest "$M"` | each empty time/allocation bound independently MET with zero tolerance; raw pairs and calculations → `$V/candidate/**`, `$V/gate.json`; rounding/pooling fails.
E246-411 | req=T246-QUALITY + inherited | exact solution build/unit/coverage/report commands in §4, integration reused from row408 | 0W/0E, passing tests, 85/75 whole and 65/75 integration; logs/Cobertura/Summaries → `$V/check/**`; threshold or test failure fails.
E246-412 | req=T246-CI + inherited | `$E ci-check --source-root . --manifest "$M"` on authentic then independently copied/tampered bundle | authentic exit0, tampered nonzero, original unchanged; command/hash/result logs → `$V/ci/**`; false acceptance fails.
E246-413 | req=T246-SCOPE + inherited | `$E audit --manifest "$M"` plus `git diff --check` | docs/CRLF/scope and staged paths match T246 allowlist, foreign CI hunk unchanged; diff/status/docs audit → `$V/scope/**`; unrelated edits fail.
E246-414 | req=T246-CACHED + inherited | `$P cached-path --manifest "$M"` invokes exact §5 command | exact seven cases, 0 failures, ≤240 s and inherited metrics/ratio predicate; timestamped log/raw exports → `$V/cached-path/results.json`, `$V/cached-path/**`; missing/extra cases fail.
E246-415 | req=T246-OWN | focused core/SQLite commands plus four targeted mutants in §4 | distinct commands on hits/misses, fresh binding, correct ownership/failure cleanup, all mutants killed/restored; test/identity/mutation evidence → `$V/tests/ownership/**`; sharing/leaks/surviving mutant fails.
CHECK re-gather: owner=CHECK, maximum two targeted read-only evidence-recovery requests, ≤180 s combined, no test/benchmark reruns; missing reports alone justify neither contract revision nor DO. Remaining evidence gaps stay OPEN/GAP.

## 10. DO units / completion
D5-01 — fix now: persist this plan/contract first, reconcile inherited units/counters, preserve rv4, establish rv5 manifest/root/CLI routing and inventory pre-existing changes. Complete when reconciliation and harness self-tests pass; ordinary mode then requires user approval before implementation, autonomous mode requires gate 1.
D5-02 — fix now: execute §6 bounded spike, resolve semantic seams with Roslyn, document immutable recipe design/fallback and budget ledger. Complete only with explicit feasible/infeasible findings; infeasible returns to PLAN.
D5-03 — fix now, depends on D5-02 gate: implement raw-only immutable recipe reuse and fresh command creation/binding; preserve opt-out, cache clearing/options and owner cleanup. Complete with focused tests proving those invariants, not with a performance claim.
D5-04 — fix now: complete core/SQLite ownership, rebinding and fallback tests; prepare live-provider tests without running them; execute targeted mutation controls and restore sources. Complete with passing unit evidence and four killed mutants.
D5-05 — fix now: finalize docs/CI/harness, build benchmark Release, run attribution and bounded locked baseline/candidate/cached-path workflow. Complete only with authentic raw artifacts and honest gate classifications; do not manufacture MET.
D5-06 — CHECK-owned verification preparation: freeze source identity, run the single boundary/full coverage, validate authentic/tampered CI and audit scope/process journal. Completion is a provisional handoff; only CHECK issues the verdict.

## 11. Risks / unknowns / invalidity
Unspecified recipe shape/key details are implementation assumptions to prove in D5-02, not permission to retain mutable state. Performance feasibility and remaining-budget sufficiency are unresolved risks, not promised outcomes.
Contamination invalidates the entire block; retain raw data plus reason. Retry only within frozen ceilings and cumulative budget; exhaustion, missing admissible blocks or unsupported attribution = GAP, never a reduced gate.
DO→PLAN classification: implementation prerequisite → additive active task; acceptable detail → explicit risk without weakening criteria; insufficient evidence → targeted scout; persistent low confidence → recommend escalation trigger 5; genuine external blocker → recommend orchestrator invoke escalation.
Historical process deviation remains non-green even if technical evidence passes. No absent report, renamed task or session reset authorizes another revision/attempt reset.

## 12. Durable state
Set N=1; r=5; n=1/3; rv=5; phase=PLAN complete, with D5 units active/pending and original unfinished D-units blocked on prerequisites; replace the misleading current-phase STOP marker without rewriting rv4 history.
Record budget 3556.8/7200 s consumed; 3643.2 s remaining, process deviation non-green, confidence high in constraints but performance gain unproven. No STOP, acceptance or implementation-complete verdict is issued by this plan.

## DO state (r=5, cycle N=1) — appended 2026-10-20

- Current cycle: N=1; plan revision: r=5; attempt: n=1/3; contract rv=5
- Phase: **DO in progress — D5-01 done, D5-02 done (FEASIBLE), D5-03 done (focused), D5-04 done, D5-05 done (measurement evidence complete; gate measured FAIL), D5-06 pending**
- Budget: 7200 s cumulative; consumed **5865.65 s** (3570.716 inherited: 3556.756 rv4 + 13.96 s rv5 spike; + 7.0 s Release benchmark build; + 159.116 s attribution launches and trace; + 526.743 s rv5 baseline block; + 1543.204 s candidate launches (three blocks); + 58.871 s cached-path), remaining **1334.35 s**
- r4 process deviation stays **non-green** (two focused DO integration runs + three boundary runs vs one planned; `validate_inner_loop.py project` exit 2). rv4 artifacts, status sections and `.pdca/T246/rv4/**` untouched.

### Repair record (start of this DO dispatch)

- Pre-dispatch tree state: `git status` showed the partial r=5 planner wiring (`useRecipeCache` overload delegating to a parameter the body did not consume), the two new untracked `DataContext/Cache/CommandRecipe*.cs` files and the untracked T246 tests; `dotnet build nextorm.slnx -c Debug` was already **green (0W/0E)** on arrival — no uncompilable state survived to this dispatch.
- Action: did **not** revert the wiring; completed the minimal coherent wiring instead (recipe hit/publish in `QueryPlanner`, `PrepareLobCommand`/`PrepareResultCommand` opt-in, `DataContextCache`/`PurgeQueryCache` invalidation, `ToDataReader`/`ToDataReaderAsync` as the only opt-in call sites). D5-01 harness/evidence/routing changes and `.pdca/T246/rv4/**` were left untouched.

### D5-02 — bounded feasibility spike: **FEASIBLE**

- Pre-registration written before the run: `.pdca/T246/rv5/spike/plan.json` (invocation, source identity, budget, gate, schedule estimate).
- Locked probe (`flock -x /tmp/opencode/nextorm-bdn-host.lock`), exact argv in `spike/result.json`: `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~T246RecipeSpikeProbe"`, exit 0, 1 selected / 1 passed, wall **2.53 s** (final no-build run; plus 4.04 s and 7.39 s earlier runs retained in the journal).
- Raw: `.pdca/T246/rv5/spike/probe.json`; log `.pdca/T246/rv5/spike/probe-final.log`.
- Observations (final run): recipe-disabled **131,051 ns/op** (3,000 ordinary preparations) vs recipe-enabled **94,327 ns/op** (3,000 hits) → **36,724 ns/op avoided, 28.02 %**; hits 3,000 / misses 0; **3,000 distinct `DbCommand` instances for 3,000 invocations**; identical rows (150,000) and checksum (3,825,000) in both arms. Three runs reproduced 29.05 %, 29.24 %, 28.02 %.
- Structural/ownership probes: `.pdca/T246/rv5/tests/recipe-structural.log`, 8/8 passed — immutable recipe identity across calls, distinct command per invocation, per-call rebinding, `Cache=false` opt-out serves/publishes nothing, captured-closure shapes ineligible, purge drops the recipe.
- Schedule: baseline 510 s + three candidate blocks 1530 s + attribution 300 s + cached path 60 s + 500 s contingency = **2,900 s < 3,631.8 s residual**. Diagnostic numbers are not acceptance.

### D5-03 — raw-only immutable recipe reuse: implemented, focused tests green

- Scope: `CommandRecipe`/`CommandRecipeStore` (new, internal, immutable metadata only), `QueryPlanner` opt-in hit/publish, `PrepareLobCommand`/`PrepareResultCommand` opt-in used **only** by `ToDataReader`/`ToDataReaderAsync`; other planner consumers keep `useRecipeCache: false`; `storeInCache` remains false and `QueryCommand.Cache` is never mutated.
- `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~ToDataReader"` → exit 0, **14 selected / 14 passed / 0 failed / 0 skipped** (`.pdca/T246/rv5/tests/focused-core.log`).
- `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ToDataReader"` → exit 0, **29 selected / 29 passed / 0 failed / 0 skipped** (`.pdca/T246/rv5/tests/focused-sqlite-rerun.log`).
- `dotnet build nextorm.slnx -c Debug` → exit 0, **0 warnings / 0 errors** (`.pdca/T246/rv5/repair/build-solution.log`).
- `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` (full unit project, affected-seam regression guard) → exit 0, **1802 total / 1801 passed / 0 failed / 1 skipped** (single pre-existing skip) (`.pdca/T246/rv5/tests/sqlite-full-final.log`).
- **D5-03 done** (2026-10-20): recipe reuse implemented, focused core 14/14 + sqlite 29/29, full sqlite 1801 passed, solution build 0W/0E.
- **D5-04 started** (2026-10-20): core/SQLite ownership, rebinding and fallback tests; live-provider caching tests prepared but not executed; four targeted mutants + restore.

### D5-04 — ownership / rebinding / fallback / mutation: **done**

- Focused core `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~ToDataReader"` → exit 0, **21/21 passed**, 0 failed, 0 skipped (`.pdca/T246/rv5/tests/focused-core-d504.log`).
- Focused sqlite `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ToDataReader"` → exit 0, **31/31 passed**, 0 failed, 0 skipped (`.pdca/T246/rv5/tests/focused-sqlite-d504.log`).
- Full core project (no filter) → exit 0, **2655/2655 passed**, 0 failed, 0 skipped (`.pdca/T246/rv5/tests/core-full-d504.log`).
- Full sqlite project (no filter) → exit 0, **1804 total / 1803 passed / 0 failed / 1 skipped** (the single pre-existing pinned skip) (`.pdca/T246/rv5/tests/sqlite-full-d504.log`).
- `dotnet build nextorm.slnx -c Debug` → exit 0, **0 warnings / 0 errors** (`.pdca/T246/rv5/repair/build-solution-d504.log`).
- Four targeted mutants (a: one command shared across recipe hits; b: rebinding skipped on hit → stale parameters; c: `Cache=false` opt-out bypassed; d: failure cleanup omitted) each produced a RED focused run (exit 2) and were restored to GREEN (exit 0, 21/21 core and 31/31 sqlite); sources restored, no `MUTANT` marker left. Mutant (d) first applied to the LOB overload survived and was re-targeted at `QueryExecutor.OpenResultReader`; the revert and re-application are recorded.
- Evidence: `.pdca/T246/rv5/tests/ownership/` (`mutation-summary.json`, `mutant-{a,b,c,d}-{core,sqlite}.log`, `restored-{core,sqlite}*.log`). Live-provider caching tests were compiled only (`.pdca/T246/rv5/tests/integration-compile.log`); no integration execution happened inside DO.
- **D5-04 done** (2026-10-19T20:15Z): ownership/rebinding/fallback green and four mutants killed + restored.

2026-10-19T20:15:00Z | DO | r=5 | n=1/3 | D5-04 close — focused core 21/21, focused sqlite 31/31, full core 2655/2655, full sqlite 1803 passed/1 pre-existing skip; solution build 0W/0E; four mutants RED then RESTORED green | `.pdca/T246/rv5/tests/ownership/mutation-summary.json`; `focused-core-d504.log`; `focused-sqlite-d504.log`; `core-full-d504.log`; `sqlite-full-d504.log`; `repair/build-solution-d504.log`
2026-10-19T20:15:00Z | DO | r=5 | n=1/3 | D5-05 started — docs/CI to rv5 wording, Release benchmark build, locked preflight/attribution, baseline reuse decision | `.pdca/status/T246-raw-datareader-1.md`

### D5-05 — docs / CI / harness finalization, release build, preflight, attribution, baseline decision (**done**; measurement evidence complete, gate measured **FAIL**)

Docs and CI (rv4 wording → rv5 behaviour; thresholds, counts, oracle and numbers unchanged):

- `docs/specs/performance/t246-raw-reader-gate.md` rewritten for rv5: contract root `.pdca/T246/rv5/`, immutable `CommandRecipe`/`CommandRecipeStore` lever with a freshly minted and bound `DbCommand` per invocation (recipe hit included), T246-OWN ownership invariants, opt-out/fallback rules, per-block provenance (`identity`: source SHAs, runtime/BDN, fixture hash), unchanged admissibility/statuses/oracle, rv5 command list; the rv4 lever rejection is kept as history.
- `docs/specs/performance/comparison-benchmark-scenarios.md`: §3.1 and the prepared-vs-ordinary bullet now describe the rv5 recipe lever; new recipe-provenance bullet; no threshold or number changed.
- `docs/specs/performance/acceptance-benchmarks.md`: T246 rows point at the rv5 manifest, cached-path row renamed D4-09 → D5-04, added a frozen-thresholds/provenance paragraph; every gate value untouched.
- `.github/workflows/dotnet.yml`: only the T246-owned hunk changed — harness self-tests now also run `t246_evidence.py self-test` and the paired self-test against the rv5 manifest, `ci-check` and `validate_inner_loop.py manifest` route to `.pdca/T246/rv5/manifest.json`. No foreign hunk touched (the workflow diff contains exactly this one hunk).
- Harness finalization: `source_digest()` now hashes the real `CommandRecipeStore.cs` (it previously listed a non-existent `CommandRecipeCache.cs` and recorded `null`), the rv5 manifest now declares `budgets` + `plan_revision`, and `load_ledger()` seeds `inherited_used_seconds` (3570.716 s) so the 7200 s budget is never reset by a contract revision. `python3 -m unittest discover -s .pdca/scripts/perf/tests -p 'test_t246*.py'` → exit 0, **75 tests OK**; both self-tests exit 0; `t246_evidence.py audit --manifest .pdca/T246/rv5/manifest.json` → exit 0 (`ok: true`, docs present, CRLF ok, all seven threshold checks true, budgets routed to the rv5 file).
- `git diff --check` → exit 0; CRLF re-normalized on every touched file (status, three spec pages, workflow, budgets, manifest, harness, decisions); no public doc (`docs/guide/**`, `docs/ru/**`, `readme.md`) links to `docs/specs/**`.

Release build and locked measurement preparation:

- `dotnet build benchmarks/nextorm.benchmark -c Release` → exit 0, **0 warnings / 0 errors**, 6.28 s (`.pdca/T246/rv5/perf/bench-release-build.log`).
- Locked `preflight --manifest .pdca/T246/rv5/manifest.json` → exit 0: lock held, lane cap 1, seed/copied fixture SHA-256 `32bf10a0…d72295` (753,664 B), path `/home/alex/sources/nextorm/.pdca/T246/rv5/perf/fixture/test.db`, dotnet 10.0.401 / BDN 0.15.8 (`dotnet_info` sha256 `4eaa79e7…`), Podman socket ping OK, nine source hashes recorded (`.pdca/T246/rv5/perf/preflight.json`, `preflight.log`).
- Locked `attribution` → exit 0: 10k prepared 3.986 ms / 962,840 B, Dapper 3.598 ms / 960,744 B, direct provider 3.598 ms / 960,712 B; empty Dapper 13.4 µs / 760 B, direct provider 14.2 µs / 728 B; EventPipe `gc-verbose` trace captured (`dotnet-trace` exit 0; top-N report `attribution/ordinary-10k-topn.txt`). `.pdca/T246/rv5/attribution/attribution.json`, `perf/attribution.log`.
- Attribution is a supported-lever observation only; it does not establish acceptance.

Baseline decision (E246-406) — **immutable rv4 reuse rejected, one rv5 diagnostic block measured**:

- Reuse audit: rv4 `baseline/block-2` is admissible and its fixture hash, the benchmark-source hash (`bedd0037…`) and the runtime identity match rv5, **but** the block manifest carries no identity block at all, the only rv4 `preflight.json` postdates the block and records `DataContext.cs = 6fae04da…`, which equals neither HEAD (`10d3f62f…`) nor the current tree (`bcb27b69…`), and there is no control provenance linking the block to the rv4 controls. Complete fixture/runtime/source/control comparability is therefore **not** established → rv4 stays read-only historical evidence and is never relabelled.
- Locked `measure --phase baseline --manifest .pdca/T246/rv5/manifest.json` → exit 0 in 526.8 s: block-1 **admissible** (0 reasons, lock held before/after, 510 host samples, no foreign workload, no thermal throttle, no frequency shift >15 %, DB SHA unchanged, full `identity` block). Diagnostic statistics: `U10k 1.1420`, `A10k 4,153,113 ns`, `Uempty 1.7834`, allocation ratio max 1.0031 (10k) / 5.040 (empty); allocation max 963,936 B (10k).
- Scope note recorded in `.pdca/T246/rv5/perf/baseline-reuse-decision.json`: the rv5 block runs on the current tree, which already contains the recipe lever, so it characterizes the current product and is **not** an unchanged-product baseline; baseline blocks never enter the acceptance oracle, which is entirely within-block paired N/L. The diagnostic gap to linq2db is reported as measured, not as a verdict.

Candidate measurement (three paired blocks, one locked harness invocation, sequential — no overlap):

- Command (exact, lock held across the whole invocation): `flock -x /tmp/opencode/nextorm-bdn-host.lock python3 .pdca/scripts/perf/t246_paired.py measure --phase candidate --manifest .pdca/T246/rv5/manifest.json` → exit 0 in 1,543.44 s across three blocks. The frozen CLI has no per-block selector (`cmd_measure` runs `CANDIDATE_BLOCKS = 3` sequentially); splitting it would spawn blocks 4–5 and spend budget for duplicates, so one invocation executed the three required blocks one after another. Log `.pdca/T246/rv5/perf/candidate-measure.log`.
- block-1: **admissible** (0 reasons), 520.188 s, 505 host samples, lock held before/after, fixture + DB SHA `32bf10a0…d72295` unchanged, no foreign workload, no thermal throttle, no frequency shift >15 %; `U10k 1.177872`, `A10k 4,132,558 ns`, alloc ratio max `1.003163` (max `964,016 B`), `Uempty 1.779004`, empty alloc ratio `5.040323`.
- block-2: **admissible**, 513.295 s, 498 samples; `U10k 1.180848`, `A10k 4,236,841 ns`, alloc `1.003080` (`963,936 B`), `Uempty 1.792835`, empty alloc `5.040323`.
- block-3: **admissible**, 509.957 s, 495 samples; `U10k 1.136955`, `A10k 4,135,941 ns`, alloc `1.003197` (`964,048 B`), `Uempty 1.763487`, empty alloc `5.040323`.
- All three blocks carry the same `identity.source_digest`, runtime (dotnet 10.0.401 / BDN 0.15.8) and fixture hash as the rv5 baseline block — no source drift between baseline and candidate.
- No block was contaminated, invalid or interrupted; no retry was needed (candidate attempts used 3 of the 5 allowed).

Gate (E246-409 / E246-410) — **measured FAIL, no MET manufactured**:

- Locked `gate --manifest .pdca/T246/rv5/manifest.json` → **exit 1**, `gate.json.status = FAIL`, `admissible_blocks = 3`, `reasons = []`, budget field 5,806.779 s. Log `.pdca/T246/rv5/perf/gate.log`.
- Per bound (all three blocks): 10k **U FAIL** (1.177872 / 1.180848 / 1.136955 > 1.00); 10k **A OPEN** (upper 4,132,558 / 4,236,841 / 4,135,941 ns vs the frozen 3,942,000 ns cap, lower bound below the cap); 10k **alloc FAIL** (1.003163 / 1.003080 / 1.003197 > 1.00); empty **U FAIL** (1.779004 / 1.792835 / 1.763487 > 1.00); empty **A MET** (24,940 / 25,472 / 24,559 ns); empty **alloc FAIL** (5.040323 > 1.00).
- Rows E246-409 and E246-410 stay **open** with the measured FAIL and reasons recorded; nothing was relaxed, pooled or rounded.

Standard cached-path gate (E246-414) — **met**:

- Locked `cached-path --manifest .pdca/T246/rv5/manifest.json` → exit 0, `ok: true`: exactly **7** frozen cases, `missing []`, `extra []`, `failures {}`, exit 0, wall **58.871 s** ≤ 240 s. Ratio `Cached_ToList / Prepared_ToList = 1.723268` recorded with numerator/denominator identities (no bound on this metric).
- Evidence: `.pdca/T246/rv5/cached-path/results.json`, timestamped line log `.pdca/T246/rv5/cached-path/stdout-timestamped.jsonl`, command log `.pdca/T246/rv5/cached-path-command.log`, raw BDN CSV exports copied to `.pdca/T246/rv5/cached-path/raw/` with `SHA256SUMS.txt`.
- Scope note for D5-06/E246-413: the mandated §5 command writes to the tracked `benchmarks/BenchmarkDotNet.Artifacts/results/**`, so those 15 report files are now modified in the working tree (the rv4 audit recorded the same generated-path class). They are the authentic raw exports and were copied into `.pdca/T246/rv5/cached-path/raw/`; the scope audit must classify them accordingly. No product or other tracked hunk was touched by this dispatch.

Budget ledger after this dispatch: consumed **5,865.65 / 7,200 s**, remaining **1,334.35 s** (candidate blocks 1,543.44 s + cached-path 58.871 s). No integration, boundary, coverage or CI validation was executed in this dispatch.

2026-10-19T20:25:00Z | DO | r=5 | n=1/3 | D5-05a close — docs (3 spec pages) and T246-only CI hunk at rv5; harness provenance/budget fixes; 75 harness tests + both self-tests + audit exit 0; `git diff --check` 0; CRLF re-normalized | `docs/specs/performance/t246-raw-reader-gate.md`; `.pdca/T246/rv5/audit.json`; `.github/workflows/dotnet.yml`
2026-10-19T20:25:00Z | MEASURE | r=5 | n=1/3 | Release benchmark build exit 0 (0W/0E); locked preflight exit 0 (fixture 32bf10a0…d72295, lock held, dotnet 10.0.401/BDN 0.15.8); locked attribution exit 0 (controls recorded, EventPipe trace captured) | `.pdca/T246/rv5/perf/preflight.json`; `.pdca/T246/rv5/attribution/attribution.json`
2026-10-19T20:35:00Z | MEASURE | r=5 | n=1/3 | baseline reuse REJECTED (rv4 block-2 has no in-block identity; the only rv4 preflight postdates it and records DataContext 6fae04… ≠ HEAD/current); one admissible rv5 diagnostic baseline block measured (526.8 s, 0 reasons, U10k 1.1420, A10k 4,153,113 ns, Uempty 1.7834) | `.pdca/T246/rv5/perf/baseline-reuse-decision.json`; `.pdca/T246/rv5/perf/baseline/block-1/manifest.json`; `.pdca/T246/rv5/perf/baseline-measure.log`
2026-10-19T20:35:00Z | DO | r=5 | n=1/3 | budget after this dispatch — consumed 4263.575/7200 s, remaining 2936.425 s; no candidate block, gate, cached-path or integration executed | `.pdca/T246/rv5/perf/attempt-ledger.json`
2026-10-19T20:37:00Z | MEASURE | r=5 | n=1/3 | candidate phase started — one locked `measure --phase candidate` invocation runs the three required paired blocks sequentially inside the frozen harness (no per-block selector in the pinned CLI; blocks never overlap, single host lock held throughout) | `.pdca/T246/rv5/perf/candidate-measure.log`; `.pdca/T246/rv5/perf/attempt-ledger.json`
2026-10-19T21:00:52Z | MEASURE | r=5 | n=1/3 | candidate phase complete — 3/3 blocks admissible (block-1 520.188 s, block-2 513.295 s, block-3 509.957 s), 0 contamination/reasons, no retry; U10k 1.177872/1.180848/1.136955, A10k 4,132,558/4,236,841/4,135,941 ns, empty U 1.779004/1.792835/1.763487, empty alloc 5.040323; measure exit 0 | `.pdca/T246/rv5/perf/candidate/block-1/manifest.json`; `block-2/manifest.json`; `block-3/manifest.json`; `.pdca/T246/rv5/perf/candidate-measure.log`
2026-10-19T21:01:04Z | MEASURE | r=5 | n=1/3 | gate exit 1 — status FAIL, 3 admissible blocks: 10k U FAIL, 10k A OPEN, 10k alloc FAIL, empty U FAIL, empty A MET, empty alloc FAIL; measured outcome recorded, no MET manufactured | `.pdca/T246/rv5/gate.json`; `.pdca/T246/rv5/perf/gate.log`
2026-10-19T21:02:10Z | MEASURE | r=5 | n=1/3 | cached-path exit 0 — exactly 7 frozen cases, 0 missing/extra/failures, wall 58.871 s <= 240 s, Cached/Prepared ratio 1.723268 recorded; raw CSV exports retained | `.pdca/T246/rv5/cached-path/results.json`; `.pdca/T246/rv5/cached-path/stdout-timestamped.jsonl`; `.pdca/T246/rv5/cached-path/raw/`
2026-10-19T21:03:00Z | DO | r=5 | n=1/3 | D5-05 close — measurement evidence complete (baseline, 3 candidate blocks, gate, cached-path); rows E246-409/410 stay open with the measured FAIL, E246-414 set met; no integration/boundary/coverage/CI run | `.pdca/T246/rv5/manifest.json`; `.pdca/T246/rv5/generations/72db36024624f01c6bac4859c6cd8808788ae685b5f76d4882b9c8c74df2c7d6/results/E246-409.json`; `E246-410.json`; `E246-414.json`
2026-10-19T21:03:00Z | DO | r=5 | n=1/3 | budget after this dispatch — consumed 5865.65/7200 s, remaining 1334.35 s (candidate 1543.44 s + cached-path 58.871 s); candidate attempts 3/5 | `.pdca/T246/rv5/perf/attempt-ledger.json`

### Remaining for the next dispatch (D5-06, CHECK-owned preparation)

D5-05 is closed. The acceptance gate is a **measured FAIL** — E246-409 and E246-410 remain open and cannot be converted by any further DO-only work; closing them needs a plan-level decision (ACT/PLAN on the recipe lever) before another candidate measurement. Remaining D5-06 work, none of it started in this dispatch:

1. Freeze source identity for the post-D5-05 tree and record it in the manifest/evidence.
2. `boundary --manifest .pdca/T246/rv5/manifest.json` — build + every non-integration test project + exactly one live integration run (needs `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, load `.opencode/skills/running-integration-tests/SKILL.md`; E246-408).
3. Unit coverage (whole 85/75) merged with the single integration execution (65/75 with the `NextORM.Core.InMemory*` exclusion) — E246-411.
4. `t246_evidence.py ci-check` on the authentic bundle, then on an independently copied/tampered bundle (authentic exit 0, tampered nonzero, original unchanged) — E246-412. Note: per the rv4 precedent this check cannot pass while the gate is not MET.
5. `t246_evidence.py audit` + `git diff --check` for docs/CRLF/scope/staging — E246-413.
6. Report the measured FAIL to CHECK; no verdict is issued by DO.

## Terminal disposition — `incomplete` (2026-10-20)

- Status: **`incomplete`** — issued by the task's own planner, not by DO. No r=6, no retry, no counter reset: **N=1, r=5, n=1/3, rv=5** stay exactly as recorded before this disposition.
- Measured reason: `gate.json` status **FAIL** over three admissible candidate blocks — 10k U 1.177872 / 1.180848 / 1.136955 (> 1.00), 10k A 4,132,558 / 4,236,841 / 4,135,941 ns (cap 3,942,000 ns, lower bound below the cap → OPEN), 10k allocation ratio 1.003163 / 1.003080 / 1.003197 (> 1.00), empty U 1.779004 / 1.792835 / 1.763487 (> 1.00), empty allocation ratio 5.040323 (> 1.00); empty A met (24,940 / 25,472 / 24,559 ns).
- Budget reason: **5,865.65 s of 7,200 s used**, **1,334.35 s remaining**, while the three required admissible blocks cost ~1,530 s — the remaining budget cannot finance the acceptance protocol.
- Obligation state (explicit, nothing waived): E246-409 **FAIL**, E246-410 **FAIL**, E246-414 **met**; E246-408 (boundary + single live integration run), E246-411 (unit/integration coverage), E246-412 (authentic/tampered CI check), E246-413 (scope audit) are **not run / GAP**.
- Preserved evidence (no staging, no revert): `.pdca/T246/rv5/failure/t246-r5-incomplete.patch` — 236,238 B, 4,285 lines, sha256 `b226d4061d23f2c584061f15e5ca15919a36965fb9e2cc56feb7c0d53290b83d`; 34 paths (24 tracked diffs at `HEAD` bcb8e676 + 10 new files appended via `git diff --binary --no-index /dev/null`). Verified: `grep '^+++ b/'` → 34/34 expected paths, `git apply --numstat` parses 34 entries, no `GIT binary patch` hunks, no `.pdca/status/` or `.pdca/T246/` path, no T245/rc3-4 path. `.pdca/T246/**` and `.pdca/status/**` are intentionally excluded (evidence stays on disk).
- Group state: collection lane **rc3-5 G1 is stopped**; T250, T311 and T247 were **not executed** and are mirrored as `incomplete` (group stopped) in `.pdca/status/collection-rc3-5.md`.
- Patch path classification (explicit): included 24 tracked diffs — product (`DataContext.cs`, `DataContextCache.cs`, `QueryPlanner.cs`, `QueryCommandExtensions.cs`), harness (`.pdca/scripts/perf/t246_evidence.py`), benchmark (`SqliteBenchmarkStreaming.cs`), the T246-only `.github/workflows/dotnet.yml` hunk, two T246 spec pages, and the 15 tracked `benchmarks/BenchmarkDotNet.Artifacts/results/**` reports written by the mandated cached-path command — plus 10 new T246 files. Deliberately excluded (and not omitted by a command failure): `.pdca/T246/**` and `.pdca/status/**` (evidence stays on disk), foreign dirty paths (`docs/guide/**`, `docs/ru/**`, `docs/relationships/**`, `docs/advanced/**`, `docs/specs/design/join-alias-mixing-and-root-alias.md`, `examples/**`, untracked `docs/superpowers/specs/2026-10-19-tvf-runtime-builders-design.md`) and T245/rc3-4 status files.

2026-10-19T21:12:00Z | MEASURE | r=5 | n=1/3 | measured acceptance gate FAIL — 3/3 admissible blocks, 10k U FAIL, 10k A OPEN, 10k alloc FAIL, empty U FAIL, empty A MET, empty alloc FAIL; no MET manufactured, no threshold relaxed | `.pdca/T246/rv5/gate.json`; `.pdca/T246/rv5/perf/gate.log`
2026-10-19T21:12:00Z | ACT | r=5 | n=1/3 | planner terminal decision — `incomplete` (measured gate FAIL + remaining budget 1,334.35 s cannot finance the three required admissible blocks ~1,530 s); no r=6, no retry | `.pdca/status/T246-raw-datareader-1.md`; `.pdca/T246/rv5/perf/attempt-ledger.json`
2026-10-19T21:12:00Z | ACT | r=5 | n=1/3 | `incomplete` recorded — counters unchanged N=1, r=5, n=1/3, rv=5; D5-06 obligations E246-408/411/412/413 not run/GAP (not waived), E246-409/410 FAIL, E246-414 met; failure patch preserved uncommitted | `.pdca/T246/rv5/failure/t246-r5-incomplete.patch`
2026-10-19T21:12:00Z | ACT | r=5 | n=1/3 | collection lane rc3-5 G1 stopped — T250/T311/T247 not executed, mirrored as `incomplete` (group stopped); no issue closure | `.pdca/status/collection-rc3-5.md`
