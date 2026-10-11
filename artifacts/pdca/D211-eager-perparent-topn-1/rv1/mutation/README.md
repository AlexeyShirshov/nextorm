# D211 mutation evidence (D:06)

## Canonical report

- `mutation-report.json` / `mutation-report.html` — refreshed from `run-final2/reports/`
  (the **final** run: spans recomputed on the final corrected tree, post-02:35 edits).
- Diagnostic source config: `stryker-config.corrected.json`; span map `mutate-spans.json`
  (generator `gen-mutate-spans.py`). Earlier configs are retained:
  `stryker-config.corrected-lf-offsets.bak.json` (pre-CRLF correction),
  `stryker-config.corrected.prefinal-stale-0235.bak.json` + `mutate-spans.prefinal-stale-0235.bak.json`
  (spans that predated the 02:35 production edits — the `run-final/` diagnostic).
- Command: `mutation-command-final2.sh` (`dotnet tool run dotnet-stryker --config-file
  stryker-config.corrected.json --output run-final2 --concurrency 4 --skip-version-check
  --verbosity info`), executed from this directory with no `DOCKER_HOST` (core + SQLite test
  projects only). Full run log: `mutation-final2.log`.
- Refreshed report copies: overwritten by `run-final2/reports/mutation-report.{json,html}`.

## Counts (canonical run: run-final2)

| Status | Count |
|---|---|
| Killed | 150 |
| Survived | 34 |
| NoCoverage | 26 |
| Timeout | 4 |
| **Total active** | **214** |
| Mutation score | **71.96 %** = (150 Killed + 4 Timeout) / 214 |

## Span binding to the final corrected tree

The previous corrected spans were computed before the 02:35 production edits and resolved to
stale line ranges: `run-final/reports/` had 17 active mutants **outside** the current changed-line
ranges (16 `EntityBuilderEagerLoading.cs` + 1 `EntityBuilderExtensions.cs`). The spans were
recomputed from `git diff -U0 b527be6f -- src/**/*.cs` mapped to CRLF raw-byte offsets
(`gen-mutate-spans.py` → `mutate-spans.json`; `DataContext/SqlBuilder.cs` range is the current
`628..901`). Independent verification on `run-final2/reports/mutation-report.json`: **all 214
active mutants fall inside the recomputed changed-line ranges (0 outside)**. Machine-readable
binding: `span-binding.json` (214/214, `bound: true`, report sha256).

## Survivor / NoCoverage / Timeout dispositions

`survivors.json` and `survivors.md` list every one of the 64 non-killed mutants with its
`file:line`, mutator, replacement and a code-tied disposition:

- **equivalent** (26): the mutation cannot change observable query behavior (pool returns,
  message text, parameter-mode builder discard, discarded alias flags, plan-hash composition).
- **defensive** (34): unreachable or duplicated guard through the public eager surface —
  param-mode mirrors (Group B), pool-return hygiene, redundant in-memory rejections, unreachable
  composite-key partition rendering/root fallbacks.
- **timeout** (4): non-terminating isolated mutant runs (`QueryCommand.QueryPreparer.cs:131`,
  `QueryPlanEqualityComparer.cs:468/471`); Stryker counts them as detected, and each is a
  behavior-preserving null-coalescing or plan-hash mutation (reasons in `survivors.json`).

`SqlBuilder.cs:734` (partition separator `i == 0 ? Kw("partition by ") : ", "`) is **Killed**
(statement, conditional true/false, equality and both string-literal mutants); the prior
"composite split unreachable / defensive" claim for this line is withdrawn (see
`coverage/branch-dispositions.md`).

## CRLF byte-span correction (history)

The first attempt passed Stryker the changed-line spans as byte offsets computed against the **LF**
representation of the source files. The working tree is CRLF, so those offsets resolved to the
wrong locations and the run captured mutants outside the T211 footprint
(`mutation-run0-aborted.log`, `prior-run-survivors.json`). The config was corrected to byte spans
computed against the on-disk CRLF files (`stryker-config.corrected.json`, span map in
`mutate-spans.json`); that corrected run is `run-survivors/`. After the 02:35 production edits the
spans were recomputed again for the final run (`run-final2/`); `run-survivors/` and `run-final/`
are retained as history.

## Open risk

Composite child keys (multi-column `partition by`) are executable through the public split eager
path (`Split_CompositeKey_PagedSql` renders `partition by tenant, parent_id`); the single-query
mode still rejects composite keys in `JoinIntoSpecHelpers.BuildKeyEquality`. The pre-existing
child plan-key divergence (fresh `Expression.Constant(List<TKey>)` per execution → cache miss) is
recorded in `groupb-classification.md` as a performance characteristic, outside D211 scope.
