# D211 mutation evidence (D:06)

## Canonical report

- `mutation-report.json` / `mutation-report.html` — refreshed from `run-survivors/reports/`
  (the **corrected CRLF byte-span** run).
- Diagnostic source config: `stryker-config.corrected.json`; the pre-correction LF-offset
  config is retained at `stryker-config.corrected-lf-offsets.bak.json`.
- Command: `mutation-command-disposition.sh` (`dotnet tool run dotnet-stryker --config-file
  stryker-config.corrected.json --output run-survivors --concurrency 4`), executed from this
  directory with no `DOCKER_HOST` (core + SQLite test projects only).
- Full run log: `mutation-disposition.log`.

## Counts (canonical run)

| Status | Count |
|---|---|
| Killed | 138 |
| Survived | 40 |
| NoCoverage | 26 |
| **Total tested** | **178** (Killed + Survived) |
| Mutation score | **67.65 %** = 138 / (138 + 40 + 26) |

## CRLF byte-span correction

The first attempt passed Stryker the changed-line spans as **byte offsets computed against the
LF** representation of the source files. The working tree is CRLF, so those offsets resolved to
the wrong locations and the run captured mutants outside the T211 footprint
(`mutation-run0-aborted.log`, `prior-run-survivors.json`). The config was corrected to byte
spans computed against the on-disk CRLF files (`stryker-config.corrected.json`, span map in
`mutate-spans.json`); the corrected run is `run-survivors/`. Independent verification: every
one of the 204 active mutants (138 Killed + 40 Survived + 26 NoCoverage) falls inside the T211
changed-line ranges taken from `git diff -U0` (see `survivors.json` `in_changed_lines`).

## Survivor / NoCoverage dispositions

`survivors.json` and `survivors.md` list every one of the 66 non-killed mutants with its
`file:line`, mutator, replacement and a code-tied disposition:

- **equivalent** (28): the mutation cannot change observable query behavior.
- **defensive** (35): unreachable or duplicated guard through the public eager surface
  (parameter-mode mirrors — Group B, pool-return hygiene, redundant in-memory rejections,
  unreachable composite-key partition rendering).
- **killed** (3): closed by Feature=D211 tests added after this run —
  `Split_PagedSql_UsesPartitionedRowNumberAndBounds` (strengthened: asserts the split child's
  outer `ORDER BY`) and the new `Split_SkipOnly_AppliesToEachParentIndependently` /
  `SingleQuery_SkipOnly_AppliesToEachParentIndependently`. The canonical report above predates
  these tests, so those three still read Survived in `mutation-report.json`; a confirming
  mutation re-run is due in the freeze dispatch.

## Open risk

Composite child keys (multi-column `partition by`) are not executable through the public eager
path: the split loader's key-chunk predicate renders `parent_idparent_id`, and single-query
rejects composite keys in `JoinIntoSpecHelpers.BuildKeyEquality`. The multi-column partition
renderer (`SqlBuilder.cs:719-737`) is defensive-only through a successful public path. This is
a pre-existing gap outside D211 scope, recorded rather than silently excluded.
