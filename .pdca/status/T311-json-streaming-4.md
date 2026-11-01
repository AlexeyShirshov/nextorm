# Cycle status: T311-json-streaming / cycle 4

## Current state

- task: `T311-json-streaming` · form: `cycle` · specialized skill: `pdca-coder+.NET`
- mode: `autonomous` (closure dispatched by a delegating session; the decision below is the user's)
- cycle: `N=4` · revision: `r=1` · attempt: `n/a (no DO, no attempt)`
- scope: closure of `#311` by explicit user decision — record `AC-311-03` as a documented permanent exception and accept the measured numbers with an explicit caveat; no product change, no re-measurement
- status path: `.pdca/status/T311-json-streaming-4.md`
- phase: `ACT` · record: `completed` · outcome: `closed by owner decision (documented permanent exception)` · accepted_scope: `none (owner exception recorded; not an evidence-grade acceptance)`
- source: user decision in the dispatching session quoted in `## Decision`; prior records `.pdca/status/T311-json-streaming-3.md`, `.pdca/status/T311-json-streaming-2.md`, `.pdca/status/T311-json-streaming-1.md`; evidence `docs/specs/performance/t311-oracle-rv4/**` (commit `774b3e0c`)

## Original task

Verbatim, the instruction this record executes:

````markdown
CLOSURE action for `#311` by explicit USER DECISION. Branch `1.0.9-rc5`, HEAD `774b3e0c`-family (verify actual HEAD first). No push, no merge, no tag, no `collection/*` branch. Auto-commit authorized for touched paths only, `#N` + pointer.

The user chose option 1 of three offered: «п1 - закрываем тикет как выполненный» — i.e. record `AC-311-03` as a documented permanent exception and accept `#311` on its measured numbers with an explicit caveat, then close the issue as done.

This is exactly the path `.pdca/status/T311-json-streaming-3.md:400` already reserved for PLAN + user ("accept as a documented permanent exception, or re-scope"). It is a user/owner override of the criteria, not a CHECK acceptance and not a repair of the historical gap.
````

The `#311` task itself is unchanged and stays at its original identity: `.pdca/status/T246-raw-datareader-3.md` §`## Original task` → `#### Cycle 3: #311` (`AC-311-01..03`), reproduced in `.pdca/status/T311-json-streaming-3.md:87-105`.

## Decision

Closed on **explicit user decision**, taken in the session that dispatched this record. The user's decision, quoted:

> «п1 - закрываем тикет как выполненный»

(option 1 of three offered: record `AC-311-03` as a documented permanent exception, accept `#311` on its measured numbers with an explicit caveat, then close the issue as done).

- This is an **owner override of the acceptance criteria** — not a CHECK acceptance, and not a repair of the historical gap. It takes the path `.pdca/status/T311-json-streaming-3.md:400` already reserved for PLAN + user ("accept as a documented permanent exception, or re-scope").
- **No criterion was re-verified, re-measured, waived by CHECK, or marked `met` on new evidence.** The independent CHECK verdict of the preceding cycle stands unrevised: `not accepted` (`.pdca/status/T311-json-streaming-3.md:364-384`).
- `.pdca/status/T311-json-streaming-3.md:392` forbids *presenting* the historical gap as repairable by evidence; it does not forbid a user decision to accept it as a documented exception, which is exactly what `:400` prescribes. Both hold here: the gap is not repaired and is not presented as repaired.
- No planner revision was issued and no CHECK was run for this record: the criteria themselves are user-supplied, and only a new explicit user instruction changes them. This record is that instruction, recorded — not a re-plan and not a verdict.
- Counters: this is record `N=4`, a closure record with **no PLAN, no DO and no attempt** (`n = n/a`). It changes no product file, no test, no benchmark and no earlier status record.

## Acceptance basis

Numbers **as captured, unaccepted by CHECK**. Source artefacts: `docs/specs/performance/t311-oracle-rv4/` (evidence commit `774b3e0c`); per-size arithmetic independently re-derived by CHECK in `.pdca/status/T311-json-streaming-3.md:380`.

| size | limit | round 1 ratio | round 2 ratio | exp(mean(d)) | direction | numerical status |
|---|---:|---:|---:|---:|---|---|
| 1k | ≤1.20 | 1.115625 | 1.056459 | 1.085639 | agree | within limit, unaccepted |
| 10k | ≤1.00 | 0.933859 | 0.914260 | 0.924008 | agree | within limit, unaccepted |
| 100k | ≤0.95 | 0.901249 | 0.909219 | 0.905226 | agree | within limit, unaccepted |

- `10k` allocation: `412,748` B/op in both rounds vs limit `446,095` B/op (`33,347` below; ≈`0.4183×` linq2db).
- Direction agrees at every size. Instability factors `1.056 / 1.021 / 1.009` (`<1.30`): these are CHECK's re-derivation at `.pdca/status/T311-json-streaming-3.md:380`, not fields of `verdict.json`, which records only `unstable: false` and `instability_factor_threshold: 1.3`.
- `7/7` acceptance controls `≤1.20` in both rounds; acceptance rounds `101.29 s` / `99.90 s` (baseline + candidate per round: `51.135530 + 50.156343` and `50.175506 + 49.727828`), all `<240 s`.
- Standing caveat, verbatim: **the receipt was rejected by CHECK for collector/validator quality and missing capture-time provenance, so the verdict is owner acceptance of a numerically passing measurement, not an acceptance-grade PASS.**
- Also standing and unrevised: `session-1-invalid` (exit `2`, `INVALID_INPUT`, `ROUND2_EXECUTION_ORDER_VIOLATION` + `MISSING_ROUND_BOUNDARY_IDENTITY_TRANSCRIPT`) is preserved and used by no verdict; `session-2` ran `12` BDN processes with `4` duplicate candidate legs not used by the verdict (`docs/specs/performance/t311-oracle-rv4/verdict.json:4-7`).
- `AC-311-01` and `AC-311-02` remain `unverified` in the standing CHECK matrix (`T311-json-streaming-3.md:370-371`); this record does not change that label.

## AC-311-03 — documented permanent exception

The four deliverables of the criterion, as written in the issue (also `.pdca/status/T311-json-streaming-3.md:100-104`):

1. A per-row throughput lever for 10k is attributed, implemented and measured; operation-count change is shown.
2. A same-session paired acceptance baseline (`<baseline>/round-{1,2}/acceptance/cases.json`, 7/7 cases) is captured before the product change.
3. A new noise-robust ratio oracle is pre-registered before its first live measurement; T247's frozen thresholds, tolerances, denominators and historical GAP are **not** edited.
4. Output bytes, streaming/error/cancellation contracts and provider behavior are preserved; cache bounds/isolation remain.

Per bullet:

- **Bullet 1 — not met, not repairable.** The lever is implemented and its operation-count change recorded (≈20,000 fewer per-row encoding operations at 10k; `2ea3ef45`), but no independent attribution phase ledger exists. The lever commit is already in the tree, so a pre-change attribution ledger can no longer be produced.
- **Bullet 2 — not met, not repairable.** The same-session paired acceptance baseline was **not** captured before the product change: the lever was committed first (`2ea3ef45`) and the baseline was afterwards re-captured from the parent tree `cce58e15` (`.pdca/status/T311-json-streaming-1.md:503,541-545,566-569`). The chronology cannot be reconstructed, and reconstructing it is forbidden.
- **Bullet 3 — met.** The noise-robust oracle was pre-registered before its first live measurement (`b6f9925a`, `docs/specs/performance/json-ratio-oracle-2.md`); T247's frozen thresholds, tolerances, denominators and historical GAP were not edited.
- **Bullet 4 — met.** Output bytes, streaming/error/cancellation contracts and provider behaviour are preserved; cache bounds/isolation remain (the retained lever changes only property-name encoding; `2ea3ef45` / `.pdca/T311/rv2/`).

Criterion status: **accepted by owner decision as a permanent documented exception — never `met`.** The attribution debt is thereby **documented, not solved**.

## Carry-forward

Two durability items outlive this closure:

- **(a)** A future streaming or caching change must not claim this receipt as acceptance-grade evidence. `docs/specs/performance/t311-oracle-rv4/` stays a numerically passing measurement whose instrument CHECK did not accept.
- **(b)** If a future cycle wants a genuine attribution of the 10k per-row lever, it needs its own PLAN with a pre-change baseline. **No such issue or milestone is created here** — the user authorised a closure, not a spin-off.

## Residual

Numbers that stay true after closure:

- Retained lever `2ea3ef45` (pre-encoded `JsonEncodedText` property-name writes) stays in the tree, unreverted.
- `10k` allocation stays ≈`0.42×` linq2db (`412,748` vs `986,736` B/op as captured).
- `#311`'s closure rests on an owner exception rather than on a clean evidence chain: no accepted receipt exists; `AC-311-01`/`AC-311-02` stay `unverified`; `AC-311-03` is unmet-but-excepted, never `met`.

## PLAN record

not applicable — no PLAN ran and no plan revision was issued. The acceptance treatment recorded in §Decision is a user instruction on user-supplied criteria, which no planner may restate, narrow or substitute; the last issued plan is `.pdca/status/T311-json-streaming-3.md` §`## PLAN record` (`r1`–`r3`) and is unchanged.

## Approval record

not applicable: autonomous mode. The authorising instruction is the user decision quoted in §Decision; no interactive gate is reachable from this dispatch and none was assumed.

## DO result

not applicable — no DO ran. No product code, test, benchmark, evidence artefact or earlier status record was created or modified by this record. Only this status file was written.

## CHECK record

not run: no new verification, by instruction and by design (§Decision). The standing verdict of cycle `N=3` is `not accepted` (`.pdca/status/T311-json-streaming-3.md:364-384`), with `AC-311-01`/`AC-311-02` `unverified`, `AC-311-03` `unmet`; it is not revised, superseded or converted into an acceptance here.

## ACT record

Outcome: closed by owner decision (documented permanent exception) · accepted_scope: none · #311 closed in GitHub with reason "completed" under the owner exception.

Cause: the user chose option 1 of three («п1 - закрываем тикет как выполненный»). No criterion was re-verified or waived by CHECK; the numbers are recorded as captured and unaccepted; AC-311-03 is recorded as a permanent documented exception and never as met.

Must not be claimed: any "PASS"/"accepted"/"acceptance-grade" wording; release or docs performance evidence; AC-311-03 repaired by this or by the rv4 evidence; an accepted residual-gap record for AC-311-01.

## Revision history

none — no PLAN and no revision in this record (N=4, closure only).

## Defect history

unchanged — `T311-CHECK-01..06` stand exactly as recorded in `.pdca/status/T311-json-streaming-3.md:427-432`. `T311-CHECK-04` (the `AC-311-03` historical gap) is documented here as a permanent exception; that is a disposition by owner decision, not a fix and not a closure of the finding.

## Changed files

- `.pdca/status/T311-json-streaming-4.md` → new closure record (this file). No product, test, benchmark or evidence path changed.

## Git state

status path: `.pdca/status/T311-json-streaming-4.md` — created this cycle; tracked after the single ACT commit for this closure (pending at write time; the commit carries only this path and reports `#311` plus the `774b3e0c` evidence pointer). Branch `1.0.9-rc5`. The two untracked user files `docs/assets/diagrams/selectwheremin-current.svg` and `docs/superpowers/specs/2026-11-01-query-components-proof-design.md` remain untracked and unstaged. No push, no merge, no tag.

## Progress log

`2026-11-01T11:52:24Z | ACT | revision r1 | n/a | status file created | .pdca/status/T311-json-streaming-4.md`
`2026-11-01T11:53:00Z | ACT | revision r1 | n/a | status_lint.py rc 0 | .pdca/status/T311-json-streaming-4.md`
`2026-11-01T11:53:30Z | ACT | revision r1 | n/a | ACT closed — owner decision (documented permanent exception); #311 to be closed as completed | .pdca/status/T311-json-streaming-4.md`
