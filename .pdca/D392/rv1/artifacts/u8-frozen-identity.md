# D392 frozen candidate identity (U8)

- Observed at: 2026-11-14T09:41:38Z.
- Candidate HEAD: `ea7edf1ff5f5b0284acd231190f7b0f787826ee4`.
- `git status --porcelain`: empty (clean worktree at observation).
- Cycle base: `b1e2db6a42c27d4dc81aac500a2b65cfbdb616cd`.
- Changed product paths: 49, counted by `git diff --name-only b1e2db6a42c27d4dc81aac500a2b65cfbdb616cd..HEAD -- docs src tests | wc -l`.

The product-path count excludes `.pdca/**`; it includes tracked paths under `docs/`, `src/`, and `tests/` changed from the supplied cycle base to the observed candidate HEAD.
