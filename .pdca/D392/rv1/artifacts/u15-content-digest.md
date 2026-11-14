# D392 r2 product-content identity

- Worktree: `/home/alex/sources/nextorm-worktrees/d392-vector-search`
- Branch: `d392-vector-search`
- Digest-anchor HEAD: `a4159c4277769d12b684cd7e87f60922d6c5086c` (commit `#392 r2: D392-P4 and content-digest identity binding`)
- `git status --porcelain=v1` at digest anchor: empty (clean).
- `D_full` recipe: `git ls-tree -r HEAD -- src tests docs | git hash-object --stdin`.
- `D_code` recipe: `git ls-tree -r HEAD -- src tests docs | grep -v 'docs/specs/' | git hash-object --stdin`.
- `D_full(HEAD)`: `7a8e46032d6f57321189b2adad39be3d9a8fd2ca`.
- `D_code(HEAD)`: `0786188ba42f75aced07d661564abeb8983d20a0`.
- `D_code(9cd05457ecaa6376669fa6e82e0d3958e37b6aac)`: `0786188ba42f75aced07d661564abeb8983d20a0`.
- `D_code` equality: `True`.
- Complete `src/tests/docs` tree-entry count: `3668`.
- `git diff --exit-code 9cd05457ecaa6376669fa6e82e0d3958e37b6aac..HEAD -- src tests`: exit `0`, no output (empty diff).
- `git diff --name-only 9cd05457ecaa6376669fa6e82e0d3958e37b6aac..HEAD -- docs`: `docs/specs/design/D392-pdca-process-defects.md` only; this is the D392-P4 spec delta.
- The D392-P4 spec was committed before digest calculation and is included in `D_full`; the strictly internal `docs/specs/**` delta is excluded from `D_code`.
- Proof log: `.pdca/D392/rv1/logs/u15-content-digest.log`.
- U13 canonical integration full test log was copied byte-identically into `.pdca/D392/rv1/logs/u13-integration-full-tests.log`; copy proof: `.pdca/D392/rv1/logs/u15-u13-integration-log-persist.log`.
- Final status/evidence commits may change HEAD but are identity-neutral only if they touch `.pdca/**`; final `D_full` equality is checked after the DO commit.

## r2 evidence reconciliation

- Reconciliation HEAD after the evidence-generation commit: `36e009aae6d6faa30083d8a92880e697416ef821`.
- `D_full(HEAD)`: `7a8e46032d6f57321189b2adad39be3d9a8fd2ca` (unchanged).
- `D_code(HEAD)`: `0786188ba42f75aced07d661564abeb8983d20a0` (unchanged).
- The only commits after digest anchor `a4159c4277769d12b684cd7e87f60922d6c5086c` through this reconciliation HEAD are `d1d80b8cf99bc224115fa3a743cdabe3137d5152` and `36e009aae6d6faa30083d8a92880e697416ef821`; both touch only `.pdca/**`, identity-neutral per the r=2 contract.
- `git status --porcelain` was empty after the reconciliation commit.
