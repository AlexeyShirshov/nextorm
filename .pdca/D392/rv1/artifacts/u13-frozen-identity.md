# U13 final frozen candidate identity

- Worktree: `/home/alex/sources/nextorm-worktrees/d392-vector-search`
- Branch: `d392-vector-search`
- Tested HEAD (canonical verification): `9cd05457ecaa6376669fa6e82e0d3958e37b6aac`
- Final candidate HEAD Z (status-only commit): `e2de4f4bb3e639a9d7b373ec3dff1c978007ea11`
- Product-content recipe at each HEAD: `git ls-tree -r <HEAD> -- src tests docs | git hash-object --stdin`
- Tested-HEAD digest: `ebe5b84c96bfb3555d7e5ee4ab1d9aae60c38df4`
- HEAD-Z digest: `ebe5b84c96bfb3555d7e5ee4ab1d9aae60c38df4`
- Digest comparison: identical. HEAD Z changes only `.pdca/status/D392-1.md`; no `docs`, `src`, or `tests` tree entry changed.
- Product paths in the complete `src/tests/docs` tree listing: `3668`.
- D392 changed product paths relative to cycle baseline `b1e2db6a42c27d4dc81aac500a2b65cfbdb616cd`: `51`; manifest footprint paths: `50`.
- Manifest regenerated at HEAD Z with `python3 .pdca/scripts/pdca_evidence.py generate --repo . --contract .pdca/D392/rv1/inputs/contract.json --output .pdca/D392/rv1`; `git_head=e2de4f4bb3e639a9d7b373ec3dff1c978007ea11`, `tree=tree_binding=943131383fa9a434c6d77915b5a761d086ca81f8`. Final `pdca_evidence.py lint` rc=0; `validate_inner_loop.py manifest` rc=0.
- `git status --porcelain` after HEAD Z and before final evidence staging: ` M .pdca/D392/rv1/manifest.json` (identity artifact is ignored until explicitly staged).
- `git status --porcelain` immediately before final evidence commit (only identity artifact and manifest pending; no product/status path pending):

```text
A  .pdca/D392/rv1/artifacts/u13-frozen-identity.md
M  .pdca/D392/rv1/manifest.json
```

- Canonical integration evidence: `.pdca/D392/rv1/logs/u13-integration-full.log` (`rc=0`, 8737 total / 8540 passed / 0 failed / 197 skipped; pinned skip budget 197/197).
