# D221 (#221) coverage delta — baseline → after (rv=1)

Comparable-selector coverage, captured with the frozen `coverage-driver.sh` (focused selectors, **not**
a full-suite threshold claim). Baseline: existing selectors only (U/Q/I-existing). After: existing
selectors + the added `Category=T221` selectors (UN/QN/I-feature).

| Phase | Line coverage | Branch coverage | DataContextBuilder | ServiceCollectionExtensions |
|---|---|---|---|---|
| baseline | 9.4% (5496/58393) | 6.5% (2267/34477) | 63.5% | 83.3% |
| after | 11.3% (6653/58450) | 7.8% (2701/34507) | 79.3% | 100% |

- The two changed production types improved and did not regress: `DataContextBuilder` 63.5% → 79.3%,
  `ServiceCollectionExtensions` 83.3% → 100%.
- Project thresholds (85% line / 75% branch) are configured as a hard gate on `main` and warn-only on
  branch `1.0.9`; these focused numbers are not evidence of full-suite threshold compliance.
- No comparable-selector regression: every existing selector's suite is green (see `core-u.log`,
  `sqlite-q.log`, and the `CoreApiContractTests` part of `integration.log`), and the overall focused
  line/branch coverage increased.

Evidence:
- `TestResults/D221/rv1/coverage-baseline.cobertura.xml`, `TestResults/D221/rv1/coverage-baseline/Summary.txt`
- `TestResults/D221/rv1/coverage-after.cobertura.xml`, `TestResults/D221/rv1/coverage-after/Summary.txt`
- `artifacts/pdca/D221/rv1/coverage-baseline-collect.log`, `artifacts/pdca/D221/rv1/coverage-after-collect.log`
