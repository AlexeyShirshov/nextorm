# R190 re-verification (rv=2) — scope guard

- tested final tip **F** = `ee42f183ffa1d769da76a0ccaa024ec53a7885c9` (current HEAD, unchanged during execution)
- reconciliation boundary **B** = `cf34f91045781faccdeb3b7163be2cc5f1dce0f2`
- frozen initial snapshot: `scope/git-status-initial.txt` (taken at the P190-FINAL-TIP boundary, before the rv=2 plan was written into the status file)
- final snapshot: `scope/git-status-final.txt`

## `git diff --name-only` (final, tracked modifications)

```
AGENTS.md
docs/specs/status/rc2-190-reverify-1.md
```

## Product / test / public-doc change

`git diff --name-only -- src tests docs readme.md ':!docs/specs/status/rc2-190-reverify-1.md'` is **empty** (`scope/product-test-doc-diff.txt`).
→ **NO** R190 product, test or public-doc change.

## Pre-existing `AGENTS.md` edit

- The single existing delta is a one-line content edit in `AGENTS.md` authored **outside** this cycle. It is OUT OF FOOTPRINT and was neither staged, reverted nor normalized.
- Initial diff sha256: `990cc12da2f14aa410f9a42fd02b9c11b98232e4adfda5c3718c43b129ba904d` (`scope/agents-md-initial.diff.sha256`).
- Final diff sha256:   `990cc12da2f14aa410f9a42fd02b9c11b98232e4adfda5c3718c43b129ba904d` (`scope/agents-md-final.diff.sha256`).
- **Final hash == initial hash** → the pre-existing edit is unmodified.

## Allowed footprint check

- Only the R190 status file `docs/specs/status/rc2-190-reverify-1.md` is a tracked modification added by this cycle.
- `AGENTS.md` remains exactly the pre-existing out-of-footprint edit (identity above).
- All other untracked entries predate this cycle (other cycles' status/evidence dirs).
- `artifacts/pdca/rc2-190-reverify-1/` and build outputs are gitignored and do not appear in `git diff`.
- BenchmarkDotNet overwrote tracked files under `BenchmarkDotNet.Artifacts/results/`; these were restored with `git checkout -- BenchmarkDotNet.Artifacts` immediately after the run, and are absent from the final diff.
- No commit, no push, no branch/worktree change.

## Verdict

Scope guard **PASS**: no R190 product/test/public-doc change; pre-existing `AGENTS.md` edit unchanged; only the R190 status file is modified tracked.
