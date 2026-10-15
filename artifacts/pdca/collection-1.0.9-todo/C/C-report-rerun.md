# Collection 1.0.9-todo — C (collection CHECK) re-run report (after CCORR-201)

- branch: `1.0.9-rc3` @ `3fb2a528` (base `58025147` ancestor; 207 commits in range)
- date: 2026-10-15
- verdict: **PASS**
- supersedes: `C-report.md` (FAIL, 2026-10-15, @ `df2b30ca`)

## Raw measurements (real exit codes)

| stream | command | exit | key numbers |
|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning(s), 0 Error(s) |
| unit/sql-gen | `dotnet test nextorm.slnx -c Debug --no-build` | 0 | 12086 total / 8915 passed / 0 failed / 3171 DB skips; 12 assemblies |
| integration #1 | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug --no-build` | 1 | 3978 / 3780 / 1 failed / 198 skips; 0 provider skips; 5 containers |
| integration #2 (warm) | same | 0 | 3978 / 3780 / 0 failed / 198 skips; 0 provider skips |
| coverage | `dotnet-coverage collect` + `reportgenerator` | 0/0/0 | line 87.5% (>=85), branch 79.3% (>=75) |
| docfx | `dotnet docfx docs/docfx.json` | 0 | 2 pre-existing warnings, 0 errors |
| tree | `git status --porcelain` | - | no half-merge; 53 dirty (50 M + 3 ??) |
| suppression | manual scan over `git diff 58025147..HEAD -- src tests` | - | 0 hard suppressions, 0 disabled tests |

Integration run #1 single failure: `ClickHouseIntegrationTests.ArrayMap_ShouldMapElements` (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:1862`), `HttpClient.Timeout` 120s — pre-existing test (`c5040d91`, out of range); passes 3/3 isolated + class 150/150 warm + full run #2; transient load-timing flake (P2 accepted, not a product defect).

## Cross-task invariants (met)

- collection states terminal: all `done`/`done-with-caveat` except T201 `incomplete`, T53 `held` (only held); no pending/ready active.
- shared-command cache stickiness: no `Cache =` write in `src/**` in range; local `storeInCache: false` bypass (`QueryPlanner.cs:596,666,688`, `DataContext.cs:463`).
- T53 public-API freeze: HELD / not activated (no `PublicAPI*.txt`, no `PublicApiAnalyzers`, no `RS00*`).

## Corrective verification (CCORR-201)

- D01 (global-filter policy reuse): closed — fail-closed metadata guard `src/nextorm.core/DataContext/QueryPlanner.cs:627-631` + `QueryCommand.QueryPreparer.cs:4021-4115` (metadata-only, no user filter fn executed); unit 22/22.
- D02 (cached-preparation liveness): closed — same-thread publish-assert `tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs:141-142`, parity `:151-152`.
- manifest `artifacts/pdca/CCORR-201/rv1/manifest.json` 11/11 met; corrective commit `e91e2f21`.

## Accepted (pre-dispositioned, unchanged)

- #237 PIVOT/UNPIVOT identifier safety `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs:148,199` — accepted risk.
- #238 T232 residual evidence-provenance — task closed `incomplete (product green)`.

## Disposition

PASS. Collection may proceed to A (completion/report; single group -> no worktrees to remove; no push).

## Evidence log paths (scratch)

/tmp/nextorm-C-recheck-20261015/{git-state.log,build.log,unit-sweep.log,integration.log,integration-run2.log,coverage-*.log,docfx.log,ch-arraymap-*.log,ch-class.log}
