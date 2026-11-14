# D392 frozen candidate identity

## Final F-02 rebinding observation

- Observed at: 2026-11-14T13:04:02Z.
- Candidate HEAD: `6125567cabf42b894e991feee4b1855716c5341d`.
- `git status --porcelain`: empty (clean at pre-DO re-anchor).
- Cycle base: `b1e2db6a42c27d4dc81aac500a2b65cfbdb616cd`.
- Changed product paths: 50, counted by `git diff --name-only b1e2db6a42c27d4dc81aac500a2b65cfbdb616cd..HEAD -- docs src tests | wc -l`.
- Product paths exclude `.pdca/**`; the final evidence commit is not part of the candidate identity.
- The canonical integration and DocFX evidence are the U9 runs; unit/build logs are the U8 runs at `ea7edf1ff5f5b0284acd231190f7b0f787826ee4`, with product code unchanged since that run as supplied in the final-evidence brief.

## Earlier U8 observation (historical; superseded for final manifest binding)

- Candidate HEAD: `ea7edf1ff5f5b0284acd231190f7b0f787826ee4`.
- `git status --porcelain`: empty (clean at that observation).
- Cycle base: `b1e2db6a42c27d4dc81aac500a2b65cfbdb616cd`.
- Changed product paths: 49.
