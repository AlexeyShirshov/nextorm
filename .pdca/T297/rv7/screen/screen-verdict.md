# E42 screening verdict — T297 r=7 — adopted-description candidate (D) vs baseline A

Pre-registered criterion (`.pdca/T297/rv7/README.md` §Stop-loss, written before the run):
**median ≤ 1.02 and 95 % CI upper ≤ 1.05 for every registered comparison**. E42 reuses E39's
exact uninstrumented six-round paired ABBA screen. The screen is a screening gate, **not
closure**: passing it would have admitted the candidate to full acceptance, failing it does not
by itself close or reopen #297.

Protocol: A = baseline `0d4d5df2`, D = candidate `ccc8c8a3` (E41 adoption).
12/12 blocks exit 0, order ABBA, one continuous host lock, DB sha256
`32bf10a0...72295` stable before/after every block, 60 s quiet window, no competing process.
Evidence: `meta.json`, `protocol.md`, `host.log`, `runs.json`, `phase-table.md`,
`reconciliation.md`, `recipe-decomposition.md`.

## Verdict: FAIL (screen, not closure)

Per-arm end-to-end A→D (candidate/baseline) multipliers:

| arm | e2e A→D | 95 % CI | criterion met? |
|---|---|---|---|
| Cte | 1.0505 | [1.005, 1.140] | **FAIL** (median > 1.02, upper > 1.05) |
| RecursiveCte | 1.0623 | [1.043, 1.148] | **FAIL** (median > 1.02, upper > 1.05) |
| In_ListContains_Captured | 1.0441 | [0.952, 1.145] | **FAIL** (upper > 1.05) |
| LeftJoin | 1.0842 | [1.027, 1.106] | **FAIL** (median > 1.02, upper > 1.05) |

Phase-level contributors (Cte / RecursiveCte / InList / LeftJoin):

- `planner_recipe` 1.129 / 1.078 / 1.031 / 1.160;
- `plan` 1.52 / 1.41 / 1.19 / 1.46;
- `construct` 1.02 / 1.11 / 1.01 / 1.05;
- InList `prepare_pipeline` 1.102 [0.992, 1.251];
- unattributed residual −45 / +46 / −90 / +151 ns/op (all insignificant, phase sum reconciles);
- instrumentation overhead +54 / +349 / +52 / −8 ns/op.

## Mechanism — the removed half was not the expensive half

The `CopyTo` call disappears (A 1.56–1.59 µs/call → D 0; instrumented `rcp_copyto` A
1.558 / 1.122 / 1.434 µs/op for Cte/Rec/LeftJoin, 0 in InList where the raw-recipe path is not
reached), but the element-wise non-description transfer (`rcp_transfer` / `transfer_exclusive`)
costs 1.84–2.11× (1.8392 / 2.1082 / 1.8675 for Cte/Rec/LeftJoin), so the net end-to-end result
is still > 1.00 on every arm. Adopting the description by reference removed a cheap write while
the remaining per-member transfer and the shape/fingerprint capture still dominate the recipe.

## Stop-loss triggered

Per the pre-declared rule: **no further candidate, no retry, no threshold relaxation**.
**#297 stays open.** Measured residuals (Cte / Rec / InList / LeftJoin):

| candidate | e2e multiplier |
|---|---|
| B — original COW storage (r=3/r=4) | 1.076 / 1.101 / 1.082 / 1.104 |
| C — flat COW root, reverted (r=5/r=6) | 1.063 / 1.093 / 1.067 / 1.112 |
| D — description adoption, reverted (r=7, this screen) | 1.0505 / 1.0623 / 1.0441 / 1.0842 |

D is the smallest residual of the three but still above 1.00 with a CI excluding 1.00 on three
arms (InList CI includes 1.00 at 9.2 % half-width).

## Next lever (for a future cycle; not started here)

1. **InList:** `qplan` hash/fingerprint (`rcp_qplan_hash`, 76.9 % in the E38 decomposition) and
   capture/fingerprint (`rcp_capture` / `rcp_fingerprint`).
2. **All arms:** bulk preparation transfer with a clone-on-write preparation block — requires
   widening the change footprint to `DataContext/Cache/QueryPlanEqualityComparer.cs` and
   `Query/QueryCommand.Clone.cs` (out of the r=7 bounded footprint by construction).

## Measurement-provenance note

The report's flagged premise is recorded, not re-run: the 47-phase probe set existed only in the
A/C screening trees; D carries A's instrumentation (it does **not** carry B's 32-phase set).
This affects only phase-level attribution continuity, not the uninstrumented ABBA e2e screen
that produced the verdict above; direction match with the E21 ABBA anchors holds for all four
arms (Cte 1.076/1.147, Rec 1.101/1.136, InList 1.082/1.075, LeftJoin 1.104/1.048).
