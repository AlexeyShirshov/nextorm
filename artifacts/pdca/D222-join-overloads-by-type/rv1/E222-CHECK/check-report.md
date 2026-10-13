# E222-CHECK — T222 (#222) check evidence (r=1, rv=1, n=3/3)

## CHECK rounds

- **CHECK #1 — FAIL.** (a) docs claimed a null-predicate parity with the source-explicit form although the
  explicit form accepts a `null` predicate while the source-less form throws first; (b) the terminal-arity-8
  "compile-time rejection / no further join" claim was false (the inherited generic-base member compiles);
  (c) V18 options parity and V24/V25 edge evidence incomplete; (d) evidence provenance ragged.
- **CHECK #2 — FAIL.** A222-05 residual (the same arity-8 claim in 4 EN + 4 RU locations) + A222-07
  completeness (stale `selected_count`, unpinned V24 baseline SHA, missing COMPAT fixture `file:line`,
  missing variant-closure index).
- **CHECK #3 — product criteria MET, blocked ONLY on evidence completeness.** Product/code criteria passed;
  the blocker was that the mandatory `E222-CHECK` row was `open` with no report, the ninth-source-less-join
  probe was not linked into the canonical compatibility indexes, and two provenance labels were stale.

## Per-criterion matrix (A222-01..07) — all MET

| ID | Status | Decisive evidence |
|---|---|---|
| A222-01 | MET | `E222-API/members.json` (exactly 28 new source-less members); `E222-COMPAT` positive fixture |
| A222-02 | MET | `E222-CORE` (type-only == explicit at every arity, 4 kinds, options); `E222-SQL` byte-identical SQL; `E222-INTEGRATION` result parity |
| A222-03 | MET | `E222-CORE/case-index.json` variant_closure V05-V20; `E222-SQL` six suites; null-predicate guard test |
| A222-04 | MET | `E222-BUILD` 0/0; `E222-COMPAT` positive (existing source/CTE/QueryCommand/named-args forms) |
| A222-05 | MET | `E222-DOCS/claim-registry.md` (27 mentions; 0 inaccurate compile-time-cap claims); `E222-DOCS/docs-review.md`; D53 register |
| A222-06 | MET | `E222-BUILD` 0/0; `E222-COVERAGE/thresholds.json` line 88.7 / branch 80.7 (pass); `E222-INTEGRATION` 3779 total / 0 failed / 197 capability skips / 0 provider-unavailable |
| A222-07 | MET | `manifest.json` + `evidence.json` (validators exit 0); this report + `receipt.json` |

## Evidence-repair round (this step)

- Ninth source-less join probe linked: `T222-COMPAT-NINTH` in `compatibility/manifest.json` and
  `E222-COMPAT/case-index.json` (sources `compatibility/ninth/NinthJoin.cs:27,38`; real exit 0;
  log `CHECK3-NINTH/stdout.log`).
- Provenance reconciled: `manifest.json.tree` -> HEAD `2ab0fddb`; `evidence.json.current_tree_revision` = `2ab0fddbc7d107dd20b44fe69bee71c4a8fee178`
  (executions keep their true historical `source_revision`).
- `E222-CHECK` row closed to `met` with `final_verdict = PASS-pending-orchestrator-ACT` (schema permits only
  met/open/na, so the verdict lives in the `final_verdict` field).

## Accepted notes (not blockers)

- `docs/guide/05-subqueries.md:59` / `docs/ru/guide/05-subqueries.md:61` — terminology "cap"/"потолок":
  behaviour is correct (compose past eight via `As`/derived table); registered as
  `accurate (terminology residuum)` — a wording cleanup, no semantic claim.
- `docs/guide/02-joins.md:27` — cosmetic wording only.

## Verdict

All product criteria (A222-01..A222-07) are MET and no product defect remains. The sole previous blocker was
evidence completeness, now repaired. Final verdict: **PASS-pending-orchestrator-ACT**.
