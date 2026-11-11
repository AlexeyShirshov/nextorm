# D384 — PDCA process defects

Internal spec. Not linked from public docs.

## PD-D384-01 — Repeated inconsistency of derived evidence (CHECK-19)

- **Stable key:** `PD-D384-01`
- **Symptom:** The frozen generation simultaneously carried `0W` (`G/results/AC-7.json:42`, `E/docfx.log:41-46`) and `4W` (`G/results/AC-8.json:31`); `P/README.md:9,31` pointed at stale identity (`adf792…`, `20261111T001138Z`, `2def65…`); `P/test-index.md:129` assigned red fractions `53423/60527`, `30223/36386` to a green `rc=0`; `P/test-index.md:132` claimed "both sides all rc=0" against baseline `rc=2`; `G/results/AC-7.json:39,43` assigned the main theory `18 A + 5 B` instead of `14 A + 5 B + 2 C`.
- **Cause / hypothesis:** Derived artefacts (README, test-index, generation results) were hand-edited or regenerated from a stale template instead of being produced by the single generation pipeline (`build_generation.py` + `pdca_evidence.py`), so the cross-part invariants were never enforced.
- **Impact on attempts:** Recurred across `N=2, r=3, n=1` and `n=2`; CHECK-19 (major, confirmed, repeat).
- **Evidence pointers:** `.pdca/D384/rv3/check-pack/README.md`, `test-index.md:129,:132`; `.pdca/D384/rv3/generations/20261111T010000Z/results/AC-7.json`, `AC-8.json`; `.pdca/status/D384-2.md` CHECK record n=2.
- **Prevention:** Run `build_generation.py` → `pdca_evidence.py generate` → `finalize_manifest.py` for every generation; enforce via `crosscheck.py` (scope + artifact path + run identity + rc/runner counters + generation/tree + local class count); tampered inputs must yield non-zero (`verification/crosscheck-negative.log`).

## PD-D384-02 — Mixing of phase reports (CHECK-21)

- **Stable key:** `PD-D384-02`
- **Symptom:** The CHECK `n=1` block in `.pdca/status/D384-2.md:469` contained DO `n=2` results (`:502-504`) while claiming "persisted verbatim" (`:744,:747`); the original full CHECK n=1 text is not recoverable from the supplied materials.
- **Cause / hypothesis:** A single append conflated the CHECK report with subsequent DO dispositions; no separation of immutable phase-report blocks.
- **Impact on attempts:** CHECK-21 (major, confirmed); limits restorability of historical evidence.
- **Evidence pointers:** `.pdca/status/D384-2.md` (mixed block); `.pdca/D384/rv3/check-reports/` (absent original).
- **Prevention:** Record each phase report in its own immutable block; save the original CHECK text as a separate artefact under `rv<k>/check-reports/` before any DO disposition is appended. Where the original is unavailable, state "original full CHECK n=1 not saved / unavailable" explicitly and describe restorability limits (this spec).

## Restorability limit

The original full CHECK n=1 report is **not saved / unavailable**. The mixed text in
`.pdca/status/D384-2.md` (lines 469-524) is preserved as a damaged historical record;
DO dispositions for n=2 are recorded separately. No reconstruction from summaries is
performed.

## PD-D384-03 — Unstated baseline red (CHECK-20)

- **Stable key:** `PD-D384-03`
- **Symptom:** `P/test-index.md:132` asserted "both sides all rc=0" while the baseline SQL Server side was in fact rc=2 (`E/branch-delta-paired/delta.md:15-16,140-151`); the red baseline was disclosed only in the generation, not in the pack's index.
- **Cause / hypothesis:** The pack's narrative prose was hand-edited and not regenerated from the measurement artifacts, so the rc status of the baseline side drifted out of sync with `delta.md`.
- **Impact on attempts:** CHECK-20 (warning, confirmed); CHECK-19 recurrence.
- **Evidence pointers:** `.pdca/D384/rv3/check-pack/test-index.md:132`; `.pdca/D384/rv3/branch-delta-paired/delta.md`; `.pdca/D384/rv3/check-pack/README.md`.
- **Prevention:** The baseline-side rc is generated from the paired-run artifacts by `build_generation.py` and surfaced in `README.md`, `contract.json` (`baseline all rc=0: False`) and the AC-6 row; `crosscheck.py` records the fraction set and the lint gate fails on untraceable numeric claims.

## PD-D384-04 — Broken historical evidence pointers (CHECK-22)

- **Stable key:** `PD-D384-04`
- **Symptom:** `E/generations/` held only `20261111T010000Z/`, while `README.md` cited the removed `20261111T001138Z/`; the archived live manifest kept stale `results_dir`/`manifest` (`E/superseded/manifest-adf792f2….json:431,437`).
- **Cause / hypothesis:** Historical pointers were hand-patched instead of being resolved through the append-only `superseded/` archive and `generation_history`.
- **Impact on attempts:** CHECK-22 (major, confirmed); breaks restorability of prior evidence.
- **Evidence pointers:** `.pdca/D384/rv3/superseded/`; `.pdca/D384/rv3/generations/`; `.pdca/D384/rv3/check-pack/README.md`; `.pdca/D384/rv3/check-reports/`.
- **Prevention:** Historical pointers are never rewritten; the `superseded/` archive and `generation_history` are the resolution mechanism; the lint gate verifies every pointer in the status record and README resolves.

## PD-D384-05 — Unproven measured/current tree identity (CHECK-23)

- **Stable key:** `PD-D384-05`
- **Symptom:** No retained cross-tree diff proving measured tree `adf792f2…` equals current tree `099780b9…`; the retained `crosscheck-run.log:132-144` verified the old tree with 129 fractions, not the claimed new tree/143 fractions.
- **Cause / hypothesis:** The bridge between the measured and current trees was asserted in prose ("comment-only") rather than retained as a verifiable artifact.
- **Impact on attempts:** CHECK-23 (major, confirmed); provenance gap for AC-6/7/8.
- **Evidence pointers:** `.pdca/D384/rv3/generation/crosscheck-run.log`; `.pdca/D384/rv3/branch-delta-paired/`; `.pdca/D384/rv3/post-n2-delta.patch` (verification/).
- **Prevention:** The cross-tree identity is established by the retained diff (`verification/post-n2-delta.patch`) plus the `git status`/`git diff` evidence that only `.cs` comment lines changed; the lint gate fails on stale hashes/generation ids/rv paths.
