# Cycle status: T311 / cycle 2

## Current state

- task: `T311` · form: `cycle` · specialized skill: `pdca-dotnet` (`coder+.NET`)
- mode: `autonomous`
- cycle: `N=2` · revision: `r=1` · attempt: `n=2/3` (terminal; `n=3/3` unspent per stop condition)
- scope: `#311` JSON streaming (`ToStream`/`ToTextReader`) — pre-lever measurement + acceptance baseline + oracle-2; no lever implemented; ACT finalization; status path `.pdca/status/T311-json-streaming-2.md`
- goal: measurable improvement on the remaining `#311` JSON streaming 1k arm (`1.089×`) while preserving the retained `2ea3ef45` 10k win and all 7/7 acceptance/oracle checks — §PLAN record authoritative
- phase: `ACT` · record: `completed` · outcome: `incomplete`
- source: issue `#311` (`AlexeyShirshov/nextorm`), milestone `1.0.9-rc5`; re-entry cycle `N=2` after `N=1` terminal `incomplete` (`.pdca/status/T311-json-streaming-1.md`, read-only); context refs: `.pdca/status/T246-raw-datareader-3.md` §`## Original task` → `#### Cycle 3: #311` (`AC-311-01..03`); caller dispatch (this DO dispatch)
- criteria: `AC-311-01`, `AC-311-02`, `AC-311-03` (by reference to `.pdca/status/T246-raw-datareader-3.md` §3.2 and `.pdca/status/T311-json-streaming-1.md`) — exact text in §PLAN record `r1`, not retyped here
- evidence root: `.pdca/T311/rv3/`

## Original task

Reference (readable complete original, not repeated here): `.pdca/status/T246-raw-datareader-3.md` §`## Original task` → `#### Cycle 3: #311` (`AC-311-01..03`) and `.pdca/status/T311-json-streaming-1.md`. Unchanged. Verbatim user-supplied acceptance criteria `AC-311-01..03` as recorded in `.pdca/status/T246-raw-datareader-3.md` §4.3 and §`#### Cycle 3: #311`.

## PLAN record

### r1

```
- **Shared form, overlay & anchor (by reference to frozen entry plan `.pdca/status/T246-raw-datareader-3.md` §`## PLAN record` r1):** Three sequential, single-process ordinary `cycle`s; specialized skill `pdca-dotnet` (`coder+.NET`). No form reclassification. Anchored to branch `1.0.9-rc5` at HEAD `5c162b06` (two untracked user files … untouched).
- **Binding treatment of `G1-SCF-01` (verdict `LIMITED`):** Treated strictly as an investigation-grade hypothesis from a CTE construction workload (`9,025.344 / 9,145.344 B → 4,272 B/query` under expression hoisting; caveats 1–9 binding). Its bytes and percentages are **never** imported as baseline or proof for `#246`, `#267`, or `#311`.
- **Hard stop across all three blocks:** At most **one** evidence-completion round per criterion per revision; after it, take the honest verdict (implementation → DO, contract → PLAN, exhausted → ACT).

## Block 3: `#311` New Cycle — JSON Streaming (`ToStream` / `ToTextReader`)

- **Attempt accounting, status & evidence root:** `N=2`, `r=1`, entering `n=2/3` (`n=3/3` in reserve). Status: `/home/alex/sources/nextorm/.pdca/status/T311-json-streaming-2.md` (prior read-only record: `.pdca/status/T311-json-streaming-1.md`). Evidence root: `.pdca/T311/rv3/`.
- **Requested vs delivered result (same object):** Measurable improvement on the remaining `#311` JSON streaming arm (`1k` small-arm time ratio `1.089×` while preserving the retained `2ea3ef45` 10k win of `~0.42×` alloc / `0.903–0.916×` time and all 7/7 acceptance/oracle checks), delivered as a committed change on `1.0.9-rc5` or an immediate clean exit if per-call construction is negligible.
- **Cycle scope & acceptance criteria (by reference):** JSON streaming path (`Query/Json/**`, `QueryCommandExtensions.ToStream` / `ToTextReader`, `DataContext.cs:501` JSON writer cache, and fluent query construction feeding JSON streaming) plus tests/benchmarks. Criteria: **`AC-311-01`, `AC-311-02`, `AC-311-03`** by reference to `.pdca/status/T246-raw-datareader-3.md` §3.2 and `.pdca/status/T311-json-streaming-1.md` (including the 7/7 acceptance baseline and JSON payload oracle requirements, verbatim, unchanged).
- **Plausibility assessment & pre-lever measurement requirement:**
  - *Gap:* Per-call query construction share in the JSON streaming arms (1k and 10k) is **unmeasured**; `G1-SCF-01` CTE numbers are **not** applicable.
  - *Plausibility:* Compiled JSON writers are already cached (`DataContext.cs:501`), and at 10k rows (`412,735 B/op`) UTF-8 serialization dominates. A construction lever is **implausible on 10k**, and **plausible on the 1k arm (`1.089×` time) only if** the 1k benchmark constructs a fresh fluent query + uncached `PreparedCommandCache.None` command on every iteration and that construction accounts for ≥5–10% of 1k time/alloc.
  - *Closure:* DO must first measure the **per-call construction + preparation share** on the actual `#311` 1k and 10k JSON streaming benchmark arms against the retained `2ea3ef45` baseline before writing any product edit.
- **Measurement method:**
  - *Subject & denominator arms:* NextORM JSON streaming (`ToStream` / `ToTextReader`) at **1k** (small arm, visible) and **10k** rows vs the benchmark's denominator arm, anchored to retained `2ea3ef45` (`10k`: `412,735 B/op` vs `986,736 B/op` = `0.42×`, time ratio `0.9159 / 0.9034`; `1k`: time ratio `1.089×`).
  - *Metric, unit & filters:* Mean execution time (`us` or `ms`/op), managed allocation (`B`/op), and time/allocation ratios on both 1k and 10k arms, plus 7/7 payload/oracle equivalence (`AC-311-03`); Release build, pinned DB (`NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`), single process, paired same-round execution.
  - *Thresholds & provenance (frozen plan §3.2):* **Tier A:** all 7/7 criteria in `AC-311-01..03` satisfied (including closing the 1k `1.089×` gap to target while keeping 10k time and `0.42×` alloc wins). **Tier B:** repeatable improvement on the 1k ratio (or 10k time/alloc) vs `2ea3ef45` with zero regression on the 7/7 oracle and 10k wins.
- **Candidate levers for DO (in-scope vs out-of-scope):**
  1. *Conditional construction/preparation reuse on the JSON streaming entry seam:* Only if the pre-lever probe shows non-trivial per-call query construction/preparation cost in the 1k arm, apply raw-shape / preparation cache reuse on the `ToStream`/`ToTextReader` command-preparation path (`DataContext` / `QueryPlanner`) and/or static expression hoisting on the streaming chain without mutating `queryCommand.Cache`.
  - *Out-of-scope:* Breaking streaming backpressure/chunking, regressing `2ea3ef45` (`JsonEncodedText.Encode`), altering JSON output bytes (`AC-311-03`), or touching `ToDataReader`.
- **Stop condition & `incomplete` trigger:** If the pre-lever measurement shows per-call construction is zero or too small to close the 1k `1.089×` gap, **stop immediately** without speculative edits and route to ACT (`incomplete` if `AC-311-01..03` Tier A remains unmet on 1k, retaining `2ea3ef45` committed and leaving `#311` open). Otherwise test Lever 1 in a single paired round (max one evidence-completion round); if Tier A is not reached by `n=3/3`, retain any measured Tier B win, record `incomplete`, and leave `#311` open.
```

## Approval record

`not applicable: autonomous mode`.

## DO result

Pre-lever measurement completed; no product edit. Removable per-call construction+preparation cost on the `#311` JSON streaming arms (single process, Release, pinned DB `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`, host quiesced; `.pdca/T311/rv3/logs/decomposition.log`): 1k = 4,451 B (8.45% of allocations) and 12,610 ns (2.17% of time); 10k = 4,434 B (1.07% of allocations), time within noise (−365,238 ns); query-building alone at 1k = 2,416 B (4.59%) / 1,382 ns (0.24%). The 1k time gap to parity is 8.9% (`1.089×`), so the construction seam cannot close it even if fully removed — stop condition triggered, no lever implemented, no speculative edit. Deliverables completed and committed at `b6f9925a` (`AC-311-03`): same-session paired acceptance baseline 7/7 in both rounds at `.pdca/T311/rv3/baseline/round-{1,2}/acceptance/cases.json`; pre-registered noise-robust oracle `docs/specs/performance/json-ratio-oracle-2.md` (`json-ratio-oracle-1.md` and T247's frozen thresholds/tolerances/denominators/GAP untouched). Retained `2ea3ef45` win stays exactly as it is (`0.42×` allocations, `0.903–0.916×` time at 10k, `1.089×` at 1k); nothing reverted. `n=3/3` unspent but unused per the plan's stop condition. Units: none added; no superseded→replacement mapping. Candidate is not a verdict.

## CHECK record

`not run: DO → ACT` (no candidate produced; pre-lever measurement refuted the lever's plausibility).

## ACT record

Outcome: `incomplete` · terminal = `execution` · accepted scope: `none` · issue `#311` left OPEN (no `gh issue close`). Completed deliverables (commit `b6f9925a`, `AC-311-03`): (1) same-session paired acceptance baseline 7/7 in both rounds at `.pdca/T311/rv3/baseline/round-{1,2}/acceptance/cases.json`; (2) pre-registered noise-robust oracle `docs/specs/performance/json-ratio-oracle-2.md` (oracle-1 and T247's frozen thresholds/tolerances/denominators/GAP untouched). Measured residual: removable per-call construction+preparation cost at 1k = 4,451 B (8.45% of allocations) / 12,610 ns (2.17% of time); at 10k = 4,434 B (1.07%), time within noise (−365,238 ns); query-building alone at 1k = 2,416 B (4.59%) / 1,382 ns (0.24%). The 1k time gap to parity is 8.9% (`1.089×`), so the construction seam cannot close it even if fully removed; hence no lever implemented, no speculative edit. Why incomplete, not accepted: `AC-311-01` Tier A (time ≤1.00× / alloc ≤1.20× at 10k) remains unmet on the 1k arm's time ratio; no Tier B local win produced in this attempt; retained `2ea3ef45` win stays exactly as it is (`0.42×` allocations, `0.903–0.916×` time at 10k, `1.089×` at 1k). Retention: `2ea3ef45` retained; nothing reverted; `n=3/3` unspent but unused per the plan's stop condition. Verification limits: the decomposition isolates construction+preparation by a cached-vs-uncached arm pair (`D` vs `A`) and a query-building-only arm (`B`); it does not claim a general library-wide construction cost, and it does not import any `G1-SCF-01` CTE byte. Next authorized action: a further cycle is a separate authorized PLAN under the state machine; issue `#311` stays open. No CHECK ran; no `PASS`; no `done`.

## Revision history

`none` (`N=2`, `r=1`, no replan; terminal `incomplete` at `n=2/3`).

## Defect history

`none` (no product defect key; no implementation change in this cycle).

## Changed files

- `.pdca/status/T311-json-streaming-2.md` → this record (ACT finalization)
- `docs/specs/performance/json-ratio-oracle-2.md` → new pre-registered oracle (commit `b6f9925a`)
- `.pdca/T311/rv3/baseline/round-{1,2}/acceptance/cases.json` → 7/7 each (gitignored evidence)
- `.pdca/T311/rv3/logs/decomposition.log` → pre-lever decomposition (gitignored evidence)
- No product code changed; `2ea3ef45` retained untouched.

## Git state

`status path: .pdca/status/T311-json-streaming-2.md — modified this cycle, tracked (durable on disk; commit pending in this ACT)`; branch `1.0.9-rc5`, baseline `b6f9925a`. No product paths modified. Untracked `docs/assets/diagrams/selectwheremin-current.svg` and `docs/superpowers/specs/2026-11-01-query-components-proof-design.md` are foreign and untouched. Evidence root `.pdca/T311/rv3/` untracked, not committed. Status file committed in this ACT.

## Progress log

- 2026-11-01T14:43:23Z | DO | revision r=1 | attempt n=2/3 | T311 N=2 part-1 dispatch started: status file created; no product edit yet | `.pdca/status/T311-json-streaming-2.md`
- 2026-11-01T09:51:09Z | DO | revision r=1 | attempt n=2/3 | T311 N=2 part-1 complete: acceptance 7/7 x2, oracle-2 pre-registered, decomposition (construction+prep <=8.45% alloc/~2.2% time at 1k) -> stop condition triggered, no lever; commit fdc5bc1b | `.pdca/T311/rv3/`, `docs/specs/performance/json-ratio-oracle-2.md`
- 2026-11-01T09:52:31Z | ACT | revision r=1 | attempt n=2/3 | finalized incomplete (DO→ACT): construction share 2.17% of 1k time cannot close the 8.9% gap; 7/7 baseline + oracle-2 delivered; issue #311 left OPEN; no gh issue close; n=3/3 unspent; evidence .pdca/T311/rv3/ | `.pdca/status/T311-json-streaming-2.md`
