# Collection 1.0.9-todo — C (collection CHECK) report

- branch: `1.0.9-rc3` @ `df2b30ca` (base `58025147`, ancestor; 204 commits in range)
- date: 2026-10-15
- verdict: **FAIL**

## Raw measurements (real exit codes)

| stream | command | exit | key numbers |
|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning(s), 0 Error(s) |
| unit/sql-gen | `dotnet test <11 projects> -c Debug --no-build` | 0 x11 | 8102 total / 8093 passed / 0 failed / 9 capability skips |
| integration | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug --no-build` | 1 | run1 3978/3778/2/198; run2 3978/3779/1/198; 0 provider skips; 5 containers started |
| coverage | `dotnet-coverage collect` + `reportgenerator` | 0 | line 83.7% (<85, warning-only), branch 77.7% (>=75) |
| docfx | `dotnet docfx docs/docfx.json` | 0 | 2 pre-existing warnings, 0 errors |
| tree | `git status --porcelain` | - | no half-merge; 68 dirty (65 M + 3 ??); collection status file clean |
| suppression | manual scan over added lines (slopwatch not installed) | - | 0 hard suppressions, 0 disabled tests; 17 reasoned conditional skips; ratio 0:17 |

Integration failing test: `tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs:148`
`Expected CachedPreparationRawCandidateIsLive(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 7).Select(x => x.Id)) to be True, but found False.`
Targeted repro (filtered, 3 runs each): Postgres 3/3 FAIL, MySQL 1/3 FAIL. Test NEW in `a21e5d89` (#201 T201 D201-4).

## Findings (check, medium)

1. P1/High — global-filter policy reuse: `src/nextorm.core/DataContext/QueryPlanner.cs:629` (`ApplyPreparationFrom`, introduced by `6f7ad459` #201) reuses a prior prepared predicate without evaluating the current context-dependent builder filter. Real static defect; no runtime exploit test supplied. Route: corrective task D01.
2. P1 — reproducible integration acceptance failure (cache-observation instability, not proven incorrect results): `CommonTestSuite.CachedPreparation.cs:148`; new test from `a21e5d89`. Route: corrective task D02.
3. P1/High — identifier safety (T232 PIVOT/UNPIVOT non-escaping, `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs:148,199`; inherited `SqlServerDialect.cs:168,846-862`): already dispositioned "accepted risk, not a blocker" under **#237** (`artifacts/pdca/D232-sqlserver-pivot-unpivot/rv5/security/issue.json`). No duplicate corrective issue.

## Cross-task invariants (met)

- shared-command cache stickiness: no per-call mutation of shared `QueryCommand` in `src/**`; local bypass pattern at `DataContext/DataContext.cs:459-463`, `QueryPlanner.cs:592-596`.
- public-API freeze (T53): HELD / not activated (no `PublicAPI.*.txt`, no `PublicApiAnalyzers`, no `RS00*`).

## Disposition

FAIL. Corrective task `CCORR-201` opened (`docs/specs/status/D201-raw-preparation-regression-1.md`); re-run the FULL collection C after it.
