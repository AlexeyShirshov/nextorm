# D224 E05 — coverage reconciliation (R6)

Two renderings of **one** collection run. CI gates on the second
(`.github/workflows/dotnet.yml:62-69` reads `Line coverage:` / `Branch coverage:` from
reportgenerator's `Summary.txt`; thresholds `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75` at
`:27-28`). Both renderings pass.

## 1. Authoritative for the settings scope — raw cobertura

- Artifact: `artifacts/pdca/D224/rv1/coverage.cobertura.xml`.
- Produced by (log `E05-coverage.log`, exit 0):
  `dotnet-coverage collect -s coverage.settings.xml -f cobertura -o artifacts/pdca/D224/rv1/coverage.cobertura.xml "bash artifacts/pdca/D224/rv1/run-evidence.sh coverage"`.
- Root: `line-rate 0.8674` → **86.7 %** (`lines-covered 37545 / lines-valid 43286`);
  `branch-rate 0.7950` → **79.5 %** (`branches-covered 20019 / branches-valid 25182`).
- Contains exactly the **4 modules `coverage.settings.xml` scopes**:
  `nextorm.core` (721 `<class>` elements), `nextorm.postgres` (21), `nextorm.sqlserver` (22),
  `nextorm.sqlite` (15) → 779 total. Nothing outside the settings scope is measured here.

## 2. CI-gated rendering — reportgenerator

- Artifact: `artifacts/pdca/D224/rv1/coverage-report/Summary.txt` (plus the normalised
  `coverage-report/Cobertura.xml`); invocation logged in `E05-report.log` (`-reports:` the raw
  XML above, exit 0).
- reportgenerator (`Parser: Cobertura`) re-renders the *same* XML after its own class
  normalisation / partial- and generic-class merging: **Line coverage 87.3 %**,
  **Branch coverage 79.9 %** (`28189 of 35243`), 4 assemblies, 582 classes.
- The normalised `coverage-report/Cobertura.xml` carries `nextorm.core` = **562** classes
  (vs **721** raw) — that 721 → 562 delta is reportgenerator's class normalisation of one XML,
  **not** a second measurement. Line/branch rates are preserved by the merge (0.8739 / 0.7998).

## Verdict

- Settings scope (raw): line 86.7 ≥ 85, branch 79.5 ≥ 75 → **PASS**.
- CI gate (Summary.txt): line 87.3 ≥ 85, branch 79.9 ≥ 75 → **PASS**.
- The two figures differ only by reportgenerator's class/line normalisation of the identical
  `coverage.cobertura.xml`; there is no scope or run mismatch to reconcile.
