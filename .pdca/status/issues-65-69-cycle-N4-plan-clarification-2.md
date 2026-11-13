# N=4 / r1 / n0 — Result-Contract Clarification (Same Pending PLAN)

## 1. Fixed Clarification State (Preserved)
- **Phase / Counters**: Same pending `N=4 / r1 / n0` PLAN continuation (prior N4 PLAN source immutable; no new cycle, revision, or reclassification).
- **Form & Overlays**: `cycle`; overlays: `pdca-coder` + `pdca-dotnet` + `nextorm-pdca`.
- **Requested / Delivered Result**: The repository state satisfying the unchanged issue-#65/#69 acceptance set and repository constraints (identical object).
- **AC-65-3 Mapping**: Default-path acceptance benchmark and tolerances (`nextorm.benchmark` default-path baseline/regression tolerances), **not** TVP metadata cache or `DecimalPrecision`.
- **Other Unchanged Criteria**: All other ACs (`AC-65-1`, `AC-65-2`, `AC-65-3`, `AC-69-1`, `AC-69-2`, `AC-69-3`, etc.) remain verbatim as previously supplied.

## 2. Reconciliation Assessment on `AC-REPO-1`
- **Why evidence-classification / scope reconciliation fails (`references/plan.md:29–31, 49–51, 106–114, 166–169`)**:
  - In `.pdca/status/issues-65-69-cycle-N2-plan.md:44–45` (§3, the sole `AC-REPO-1` definition in the named N2 plan), `AC-REPO-1` explicitly states as its **accepted outcome**:
    1. Zero warnings/errors build and repo conventions,
    2. API/docs (`docs/**` and `docs/ru/**`) consistency, and
    3. Green unit, dialect, **and live integration suites of all supported providers** (subject to skip budget).
  - Protected dirty work is recorded as a **constraint** in `.pdca/status/issues-65-69-cycle-N2-plan.md:64–68` (§5) and status line 12, not in `AC-REPO-1` (§3 lines 44–45).
  - Treating `AC-REPO-1` as *"build + conventions + protected dirty work, not whole-live-suite-green"* changes the **accepted outcome** judged by the criterion (omitting API/docs and negating the explicit requirement of green unit, dialect, and live integration suites across all supported providers).
  - Under `references/plan.md:49–51` and `166–169`, PLAN cannot narrow, weaken, or silently omit required outcomes in verbatim criterion text under the label of "evidence classification" or "scope distinction".

## 3. Non-Dispatchable PLAN Blocker
- **Status**: **Non-dispatchable PLAN blocker** (`acceptance criteria cannot be determined` without resolving contradictory authoritative inputs, `references/plan.md:69–70, 78–79, 108–114`).
- **Conflicting Source Statements**:
  1. **Immutable N2 Plan Criterion Text (`.pdca/status/issues-65-69-cycle-N2-plan.md:44–45`, §3)**: `AC-REPO-1` explicitly requires API/docs consistency and green unit, dialect, **and live integration suites of all supported providers, subject to skip budget** (with protected dirty work separate in §5 lines `64–68` and status line `12`).
  2. **Dispatch Instruction on Criterion Preservation**: Preserve exact `AC-REPO-1` wording verbatim and do not weaken or silently omit API/docs or test requirements.
  3. **Supplied User Fact on `AC-REPO-1` Scope**: Asserts that `AC-REPO-1`'s recorded scope in the N2 plan is *"build + conventions + protected dirty work, not whole-live-suite-green"*.
- **Blocker Effect**: Statements (1)+(2) and Statement (3) are mutually exclusive on the accepted outcome of `AC-REPO-1`. Dispatch to DO is blocked until the driver/user resolves which authoritative input governs without silent criterion weakening.
