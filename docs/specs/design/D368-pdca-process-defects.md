# D368 — PDCA process defects

Internal record of **process** defects observed while running the D368 `pdca` cycle (`#368`,
`SelectGroup`/`SelectGroupHaving` component-operator overhead). Product defects are not listed here;
every entry is a defect of the cycle's own evidence, records or decisions. This is not public
documentation and is not linked from `docs/**`.

---

## PD1 — `STATE-COUNTER-STALE`: the `## Current state` counter was not refreshed on loop-back

- **Symptom:** after CHECK r1 returned `not accepted → DO`, the `cycle` bullet in `## Current state` still
  read `attempt: n=1/3` while the same section's `phase` bullet and the progress log said `n=2/3`. CHECK r3
  reported the contradiction and lost part of its budget reconciling the record.
- **Root cause:** the attempt-2 update edited the `phase` bullet only; the counter lives on a separate
  bullet and was updated once in the whole cycle (at cycle creation).
- **Effect:** one CHECK report spent a finding on a bookkeeping contradiction; the driver had to reconcile
  the record in the middle of a recheck.
- **Evidence:** status `.pdca/status/D368-component-operator-overhead-1.md` §Current state (the `cycle`
  bullet), §Progress log, §CHECK record r3.
- **Prevention:** every event that changes `N`, `r` or `n` updates **all** carrier lines in the same write
  (`Current state` counter bullet + phase bullet + progress-log row), never just the phase bullet.

## PD2 — `DEFECT-HISTORY-EMPTY`: `## Defect history` stayed `none` after the first rejected CHECK

- **Symptom:** CHECK r1 returned five findings; the status still carried `## Defect history: none` until
  CHECK r2 flagged it.
- **Root cause:** the skeleton default `none` was never replaced on the loop-back; the finding keys lived
  only in the CHECK report section.
- **Effect:** the defect keys were not a durable, per-attempt ledger; the same-defect threshold could not be
  read from the record.
- **Evidence:** status §Defect history (now `D368-C1…C7`), §CHECK record r2.
- **Prevention:** a `CHECK → DO` loop-back writes the finding keys into `## Defect history` in the same
  update, with the observed attempt, the applied fix and the last outcome.

## PD3 — `PROTOCOL-ORACLE-CONTRADICTION`: the protocol file stated the wrong comparison direction

- **Symptom:** `perf/PROTOCOL.md` summarised the comparison as `|delta| <= Band`, while PLAN r1 fixed the
  one-sided `ΔTime ≤ Band_Time` (so a negative delta always passes).
- **Root cause:** the file was copied from the D362 protocol wording and not reconciled with the D368 plan
  text; the tables produced by the measurement script used the correct one-sided rule, so the documents
  disagreed while the numbers agreed with the plan.
- **Effect:** CHECK r1/r2 had to establish which rule applied before it could judge; a two-sided reading
  would have produced a different verdict.
- **Evidence:** `perf/PROTOCOL.md` (corrected), §PLAN record r1 AC1/AC2/AC4, `aacontrol/perf/gen_table.py`.
- **Prevention:** a protocol document is derived from the plan's own criterion text, and the plan fixates
  the comparison direction explicitly (it did); the executor must not paraphrase it from a previous cycle.

## PD4 — `MEASURE-UNDER-LOAD`: a benchmark session was started under heavy external load

- **Symptom:** one A-B-B-A session produced a 627 µs baseline arm against a 337 µs candidate arm on the same
  method (load average 24 during the run) and had to be discarded.
- **Root cause:** the session was launched without checking the host state after a build/coverage burst.
- **Effect:** ~10 minutes of benchmark time wasted and one session kept in the record as contaminated
  evidence.
- **Evidence:** `attempt2/perf/TABLES.md`, §DO result attempt 2.
- **Prevention:** record `uptime`/load before the first arm and after the last; treat a session whose A1/A2
  spread exceeds the previously observed band by an order of magnitude as contaminated **before** the
  comparison is published; do not run benchmarks beside builds, test suites or container churn.

## PD5 — `PER-SESSION-BAND-INSTABILITY`: the acceptance statistic flips verdicts between sessions

- **Symptom:** three valid sessions on identical code gave three different AC2/AC4 verdicts: session 1 —
  two operator methods outside band; session 3 — 10/10 and 9/9 inside; session 4 — two operator methods and
  two control methods outside. Per-method bands ranged 0.69 % to 90 %.
- **Root cause:** the prescribed statistic is a **within-session** A-B-B-A delta divided by a band estimated
  from a single A1/A2 pair (three iterations each). That band measures the session's own drift, which is
  itself a random variable; a low-drift session produces a tight band and a "regression", a high-drift
  session masks real deltas.
- **Effect:** acceptance cannot be decided from one session, and re-running until a favourable session
  appears is not evidence. The cycle spent attempts 2 and 3 on this question.
- **Evidence:** `perf/TABLES.md`, `attempt3/perf/TABLES.md`, `attempt4/perf/TABLES.md`,
  `aacontrol/perf/TABLES.md` (A/A control: 19/19), `aacontrol/REPRODUCIBILITY.md` (B/B probe: 1/38).
- **Prevention:** for a host-noise-sensitive acceptance, fix an inter-session pooling rule **in PLAN before
  DO** (median of ≥3 sessions with a pooled band, or an absolute floor tied to the machine's measured
  dispersion), and state the required number of sessions. A criterion whose oracle is a single 3-iteration
  session must not be used to refuse or accept a sub-5 % difference.

## PD6 — `SUITE-INVALID-ENV`: two whole-suite gate runs were invalidated by container state

- **Symptom:** the first suite run failed 129 tests with `Table … already exists` / `doesn't exist` seeding
  races; the second failed 3 644 tests after the ClickHouse and SQL Server containers died (`Exited (137)`,
  connection refused) under memory pressure. Only the third run, after purging the reused containers, was
  clean (23 419 total, 0 failed).
- **Root cause:** reused Testcontainers instances carried over from earlier runs plus memory pressure on an
  11 GB host; the seeding drop/create cycle raced and the containers were reaped mid-run.
- **Effect:** two invalid coverage artifacts kept in the evidence tree; ~25 minutes of gate time.
- **Evidence:** `coverage3/solution.log`, `coverage3/solution2.log`, `coverage3/solution3.log`.
- **Prevention:** before a gate run, remove the reused containers (`podman … ps -aq --filter label=reuse-id`),
  check free memory, and never run the suite beside a benchmark session; a red suite is investigated for the
  seeding/container signature before it is attributed to the product.

## PD7 — `TRANSCRIPTION-ERROR`: the status misnamed the method that violated the B/B probe

- **Symptom:** the evidence-round paragraph said the cross-session B/B probe violated on
  `Execute_Keyed` (+12.8 % vs 11.6 %); the artifact shows `Execute_CapturedCondition` (+8.66 % vs 8.26 %).
- **Root cause:** the sentence was written from a truncated tail of the generated table instead of the
  artifact.
- **Effect:** CHECK r3 refuted the summary and re-derived the number from the source artifact; the cycle lost
  a portion of a check round.
- **Evidence:** `aacontrol/REPRODUCIBILITY.md:10`, status §DO result (corrected).
- **Prevention:** every number quoted in a status record is copied from a named artifact line, and the quote
  is checked against the artifact before the write; a generated table is never summarised from a truncated
  view.

## PD8 — `DRIVER-FALLBACK`: autonomous dispatches died with the host restarts

- **Symptom:** the `pdca-orchestrator` dispatch was aborted twice by host restarts; the cycle was continued
  by the flat primary under the contract's documented fallback.
- **Root cause:** host-level; the subagent session did not survive the restart and no session id was
  returned to resume.
- **Effect:** one recovery round and a `Notice:` in the record; no data loss, because the status file was
  written before the crash.
- **Evidence:** §Progress log `RECOVERY` rows, §Current state `Notice` note.
- **Prevention:** none at cycle level beyond the existing recovery discipline; the status file must be
  current before each dispatch (it was), and the fallback is recorded rather than hidden.
