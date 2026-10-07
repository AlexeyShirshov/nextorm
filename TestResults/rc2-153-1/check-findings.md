# CHECK findings — D153 public-extensibility (cycle N=1)

## (a) CHECK r=1 / n=1 verdict — FAIL (documentation defect)

The read-only CHECK of the r=1/n=1 DO result closed the executable acceptance rows (EV-01/02/03,
EV-06) but returned one documentation defect and requested evidence completion. It is a
documentation-only loop-back, so the plan revision stays `r=1` and DO resumes at `n=2/3`; no
replan and no new D-unit were created.

Defect key: `DOC-extremerow-renderer-sample`

- `docs/advanced/api-reference.md:109` and `docs/ru/advanced/api-reference.md:109` claimed a `null`
  can come "directly or from `CanRender`". `IExtremeRowRenderer.CanRender(ExtremeRowDescription)`
  returns `bool` and cannot return `null`; the decline answer is `false`.
- `docs/advanced/select-where-extrema-native.md:116-131` and its RU mirror referenced an undeclared
  `MyRenderer.Instance`, its `CanRender` returned the constant `false` (making the shown `Render`
  dead code), and `Render` returned `request.SourceSql`, which does not select a winning source row.
- Evidence gaps: C-04/C-06/C-08 re-runs, a complete C-07 artifact, per-provider integration proof,
  and the evidence validator re-run were missing from the r=1/n=1 submission.

## (b) DO n=2 fix + evidence-completion actions

Fix 1 — `CanRender` contract wording (EN + RU): reworded to "returning `null` from the
`ExtremeRowRenderer` override — or returning `false` from `CanRender` — keeps the portable
lowering" / RU equivalent. The other four capability points on the line are unchanged.

Fix 2 — renderer sample (EN + RU): declared a coherent `MyRenderer : IExtremeRowRenderer` whose
`CanRender` accepts only the global (ungrouped) single-key form and whose `Render` returns a real
winning-row source (`request.SourceSql` + `order by "<key>" asc|desc limit 1`); kept the second
`=> null` override example that forces the portable strategy. EN and RU carry the same corrected
example.

Evidence completion (all under `TestResults/rc2-153-1/`; every command recorded its exit inside
its log):

| Call | Command (argument array) | exit | Result | Log |
|---|---|---|---|---|
| C-04 | `dotnet docfx docs/docfx.json` | 0 | 0 errors, 2 pre-existing `AnalyzerReleases` warnings | `docfx.log` |
| C-06 | `git diff --check` | 0 | empty/clean | `diff-check.log` |
| C-07 | `git diff HEAD` + `git diff --no-index /dev/null <new file>` for the two new consumer files | n/a | complete change artifact incl. untracked files | `task-diff.patch` |
| C-08 | `git grep -n -E 'docs/specs/\|(\.\./)+specs/' -- <4 pages>` | 1 | no matches (no public→specs links) | `public-spec-links.log` |
| C-08/EV | `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter FullyQualifiedName~PostgresExtremeRowNativeSpecificTests` (DOCKER_HOST) | 0 | total 16, succeeded 16, failed 0, skipped 0 | `integration-postgres.log` |
| C-08/EV | `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter FullyQualifiedName~ClickHouseExtremeRowNativeSpecificTests` (DOCKER_HOST) | 0 | total 35, succeeded 35, failed 0, skipped 0 | `integration-clickhouse.log` |
| EV | `python3 validate_inner_loop.py brief TestResults/rc2-153-1/scope-fix.json` | 0 | docs-only scope valid (`rebuild=none`) | `scope-fix.json` |
| EV | `python3 validate_inner_loop.py report TestResults/rc2-153-1/evidence.json` | 0 | scope + executions valid (8 executions, 2 amendments) | `validator-report.log`, `validator-report-exit.txt` |

Both container runs executed > 0 tests with 0 skipped, so PG and ClickHouse were both genuinely
exercised; no provider was reported as skipped.
