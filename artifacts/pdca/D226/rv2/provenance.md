# D226 / rv2 — provenance erratum (P226-R2-04, additive)

- task: T226 (#226), revision r=2, contract rv=2.
- scope: this erratum corrects claims made in the *r=1* records. It is additive: the rv1 artifacts
  (`artifacts/pdca/D226/rv1/**`) are immutable history and are NOT edited; the corrected statements live
  in the rv2 records (`mutation-report.json`, `manifest.json`, this file).

## Corrections

1. **`stryker_re_run` (rv1 `mutation-report.json`) is withdrawn.** The rv1 report carried a
   `stryker_re_run` entry describing why a Stryker re-run was "not reproducible". That entry mixed two
   different things (a config-scope observation and a claim about a re-run that was never performed) and
   must not be read as evidence of any post-fix Stryker verification. No rv2 record carries a
   `stryker_re_run` key.
2. **The scoped Stryker output that does exist is PRE-FIX and of unverified identity.** The r=1 CHECK
   re-run left scoped Stryker artifacts under `artifacts/pdca/D226/rv1/check/`
   (`stryker-output/reports/mutation-report.json`, `stryker.log`, `stryker-config.json` scoping
   `--mutate` to the T226 files). They exist as history, but their identity against the *final accepted*
   tree was never verified — the sources were mutated again afterwards — so **they are NOT evidence of a
   substantiated post-fix / final-tree Stryker rerun**, and rv2 makes no such claim.
3. **X1–X3 substantiate the three former survivors — by hand-mutant artifacts, not by Stryker.** The
   three statement-removal mutants that the scoped Stryker run had reported as survivors are each now
   killed by a *hand-mutant patch* executed and classified in this revision (rv2 records, verified
   classifier):
   - `remove-publish-after-release-closereader` → killed by
     `RecordsAffectedTests.CloseReader_PublishesTheTotal_PreferingThePostReleaseValue`
     (“Expected result.RecordsAffected to be 5, but found 2”);
   - `remove-publish-after-release-disposeasync` → killed by
     `RecordsAffectedTests.AsyncRelease_PrefersThePostReleaseValue`
     (“Expected result.RecordsAffected to be 6, but found 2”);
   - `remove-command-disposeasync` → killed by `RecordsAffectedTests.AsyncRelease_DisposesTheCommand`
     (“Expected command.DisposeAsyncCount to be 1, but found 0”).
   These are substantiated kills with patch, build/test exit codes, designated test and assertion
   records in `$R/mutation-report.json` and `$R/mutations/*.patch`.
4. **The two `ConfigureAwait` removals stay `justified` — reasoning, never a kill.** Removing
   `.ConfigureAwait(false)` from `reader.DisposeAsync()` / `command.DisposeAsync()` changes only the
   continuation context; the library captures no `SynchronizationContext` and the test host has none, so
   no assertion can observe it and no `RecordsAffected` behaviour depends on it. These entries are
   labelled `justified` with `basis: reasoning`, and are not counted as kills anywhere.
5. **No other rv1 claim is upgraded.** rv1's E226-09 remains invalid (its row-count patch counted
   columns/result sets); it is superseded prospectively by `E226-09-BATCH-ROWS` and `E226-09-N6`, and no
   rv2 row reuses it as proof.

## Verification

`python3 artifacts/pdca/D226/rv2/verify-provenance.py` — checks this file's statements, the rv2 report's
`provenance` block (no `stryker_re_run`, `stryker_post_fix_rerun = not-substantiated`, X1–X3 killed with
artifacts, ConfigureAwait justified), the presence of the pre-fix rv1 Stryker artifacts as history, and
the rv1 byte-identity manifest.


## P2 erratum — withdrawn sub-hash (appended)

The provenance **sub-hash `9ef7a206…`** in `artifacts/pdca/D226/rv2/provenance-determinism.json` was computed with an **undocumented serialization method** (a compact, key-sorted dump of the provenance sub-object) and is **not independently reproduced**, so it is **withdrawn as verification evidence**. What the independent runs did establish is report **byte reproducibility**: the whole `mutation-report.json` is byte-identical across two consecutive generator runs, with SHA-256 **`bc0d0a38103a2f51ad99c6a99da0546f8ec87415a0818c8956c5099d77b720b5`**, which is also the bound/validated report instance. No serialization method is invented and the unrelated `7a493880…` sub-hash is not substituted for it.
