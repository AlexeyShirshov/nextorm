# E10 — provenance ledger for the rv=2 contract (issue #166, CHECK gather)

Read-only gather. No source edits, no stage/commit. Branch `1.0.9-b`, `HEAD 79b48fd744e372fe1abde708f7d0509b0b5941ef`.
UTC gather time: 2026-10-05T13:30Z. Sources read (not re-run): all artifacts under `docs/specs/status/nested-read-cte-1-evidence/` + `nested-read-cte-1.md`.

Convention: "from log" = the artifact itself prints the invocation (a `$ …` line) or the exact arg array; otherwise the command is cross-recorded in `E05-coverage.md` / `E03-matrix.md` / `E02-allocations.json` and marked accordingly. Literal `|` inside a cell is escaped `\|`.

## Fresh build baseline (this gather)

`["dotnet", "build", "nextorm.slnx", "-c", "Debug"]` → **exit 0**, `Build succeeded.` **0 Warning(s) / 0 Error(s)**, 9.15 s.
Log: `/tmp/opencode/E10-build.log` (outside repo, transient). Matches recorded C1 baseline (6.57 s / 11.83 s in retained logs).

## Provenance table — C0–C8, SA-READ, SA-MUTATION

| Row | Exact command / invocation (source) | Exit code | Selected / passed | Artifact / log path |
| --- | --- | --- | --- | --- |
| C0 | `roslyn` structure/members/refs/callers — **invocation array N/A, explicitly justified by the rv=2 contract**; no array is printed in E01 | N/A | resolved: getter internal, exactly **1** production caller (`QueryPlanner.cs:559`) | `E01-paths.md` |
| C1 | `["dotnet","build","nextorm.slnx","-c","Debug"]` (array in `E05-coverage.md`; **not** present in `C1-build.log`) | not recorded exactly (log: `Build succeeded.`; status/E05 record 0) | 0 warnings / 0 errors | `C1-build.log` (retained r1 `E09-build.log` 0W/0E) |
| C2 green | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~NestedReadCteWarmReuse_AllocatesZero"]` (array in `E02-allocations.json` `boundary_sweep_green`; not in `E02-target-green.log`) | not recorded exactly (log: `Passed!`; JSON records `exit_code: 0`) | total **1** / succeeded **1** / skipped 0; **bytes = 0** | `E02-target-green.log` |
| C2 red | same filter (array in `E02-allocations.json` `red_before_F1_allocation`, historical) | **2** (printed: `Exit code: 2`, `non-success exit code: 2`) | total **1** / failed **1**; **allocated 1,760,000 B** | `E02-allocation-red.log` |
| C3 class | `$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~Iteration14CteLookupTests"` (`$` line in `E03-matrix.md`; not in `C3-class.log`) | not recorded exactly (log: `Passed!`; status/E05 record 0) | total **12** / succeeded **12** / failed 0 / skipped 0 | `C3-class.log` |
| C3 full core | no filter (array in `E03-matrix.md`/`E05-coverage.md`; not in `C3-full-core.log`) | not recorded exactly (log: `Passed!`; status/E05 record 0) | total **1524** / succeeded **1524** / failed 0 / skipped 0 | `C3-full-core.log` |
| C3 full sqlite (r2) | `["dotnet","test","tests/nextorm.sqlite.tests","-c","Debug"]` (array in `E05-coverage.md`; not in log) | not recorded exactly (log: `Passed!`; status/E05 record 0) | total **1013** / succeeded **1012** / failed 0 / skipped **1** (pre-existing probe) | `C3-full-sqlite.log` |
| C4 core | `$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o docs/specs/status/nested-read-cte-1-evidence/cov-core.cobertura.xml "dotnet test tests/nextorm.core.tests --no-build --verbosity normal"` (from `$` line) | not recorded exactly (log: `Passed!`; E05 records 0) | total **1524** / succeeded **1524** / skipped 0 | `cov-core.cobertura.xml` (+ `C4-core.log`) — retained rv=1 |
| C4 sqlite | `["dotnet","tool","run","dotnet-coverage","collect","-s","coverage.settings.xml","-f","cobertura","-o","…/cov-sqlite.cobertura.xml","dotnet test tests/nextorm.sqlite.tests --no-build --verbosity normal"]` (array in `E05-coverage.md`; `C4-sqlite.log` has **no** `$` line) | not recorded exactly (log: `Passed!`; E05 records 0) | total **1013** / succeeded **1012** / skipped 1 | `cov-sqlite.cobertura.xml` (8,725,406 B) + `C4-sqlite.log` — refreshed r2 |
| C4 postgres | `$ … dotnet-coverage collect … -o …/cov-postgres.cobertura.xml "dotnet test tests/nextorm.postgres.tests --no-build --verbosity normal"` (from `$` line) | not recorded exactly (log: `Passed!`) | total **742** / succeeded **742** / skipped 0 | `cov-postgres.cobertura.xml` + `C4-postgres.log` — retained rv=1 |
| C4 sqlserver | `$ … dotnet-coverage collect … -o …/cov-sqlserver.cobertura.xml "dotnet test tests/nextorm.sqlserver.tests --no-build --verbosity normal"` (from `$` line) | not recorded exactly (log: `Passed!`) | total **556** / succeeded **556** / skipped 0 | `cov-sqlserver.cobertura.xml` + `C4-sqlserver.log` — retained rv=1 |
| C5 | reportgenerator options printed verbatim in the log `Arguments` block (`-reports:<4 cobertura>`, `-targetdir:…/c5-report`, `-reporttypes:Html;TextSummary;Cobertura`, `-riskhotspotassemblyfilters:+nextorm.*`); no array/exit printed | not recorded exactly (log has no exit; `E05-coverage.md` records 0) | Assemblies 4 / Classes 553 / Files 322; overall line **82.0 %**, branch **74.2 %** (getter 100 %/100 %, 22/22) | `C5-reportgenerator.log`, `c5-report/` (`Summary.txt`, `Cobertura.xml`, `index.html`) |
| C6 | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` (exact in `E06-perf.md`) | not recorded exactly in `E06-acceptance.log`; `E06-perf.md` records **0** | **7** executed / 0 failed; BDN global total **53.6 s** (shell 63 s) | `E06-acceptance.log`, `E06-perf.md` — retained (not rerun) |
| C7 | `["python3","eng/perf/iteration14_gate.py"]` (exact in `E07-gate.md`) | `E07-gate.md` records **0**; log line `benchmark run exit code 0, wall 843.3s` (sub-run) | **56** row/job verdicts within budget; zero-budget CTE rows 0.00 B/op both toolchains | `E07-gate.log`, `E07-gate.md`, 3× `E07-*-report-full-compressed.json` — retained (not rerun) |
| C8 | `git diff --check` + scoped diff review (`src/nextorm.core`, `tests/nextorm.core.tests`, `tests/nextorm.sqlite.tests`, status) | `E08-review.md` records **0** | 3 files, +319 / −6 (per-file: +21/−6, +248/0, +50/0); CRLF preserved | `E08-review.md` |
| SA-READ sync | `$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NestedReadCte_SyncTerminal_Executes"` (exact in `E03-matrix.md`; not in `SA-READ-sync.log`) | not recorded exactly (log: `Passed!`; status records 0) | total **1** / succeeded **1** / skipped **0** | `SA-READ-sync.log` |
| SA-READ async | `$ … dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NestedReadCte_AsyncTerminal_Executes"` (exact in `E03-matrix.md`; not in log) | not recorded exactly (log: `Passed!`; status records 0) | total **1** / succeeded **1** / skipped **0** | `SA-READ-async.log` |
| SA-READ combined | `$ … dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~NestedReadCte_SyncTerminal_Executes\|FullyQualifiedName~NestedReadCte_AsyncTerminal_Executes"` (exact in `E03-matrix.md`) | not recorded exactly (log: `Passed!`) | total **2** / succeeded **2** / skipped **0** | `SA-READ-combined.log` |
| SA-MUTATION | `$ DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 900 dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~DataModifyingCtePlanCacheTests"` (exact in `E03-matrix.md`; not in log) | not recorded exactly (log: `Passed!`; status records 0) | total **4** / succeeded **4** / skipped **0** | `SA-MUTATION-classification.log` |

Notes on non-`$` invocations: this harness does not emit the command line into test/build logs, so exit codes for green runs are derivable only from `Test run summary: Passed!` / `Build succeeded.` plus the status/E05/E03 narrative; only the two red logs (`E02-allocation-red.log`, `E04-assertion-red.log`, `E04-terminal-probe.log`, `E04-behavior-baseline.log`) print an explicit `Exit code: 2`. The rv=2 contract's "exact resolved arg arrays" are met for C2 (JSON) and C4-core/postgres/sqlserver (`$` lines); the remaining rows carry exact arrays in `E05-coverage.md`/`E03-matrix.md`/`E06-perf.md`/`E07-gate.md` rather than in the per-row log.

## Suppression / slop-watch scan (r=2 diff)

Tooling: **slopwatch is NOT available** — absent from `.config/dotnet-tools.json` (only `dotnet-coverage`, `dotnet-reportgenerator-globaltool`, `docfx`) and from `dotnet tool list`. Scan is manual (`git diff` added lines + `git grep`, ignore-aware).

| Marker | Added by r=2 diff | Pre-existing in touched projects |
| --- | ---: | ---: |
| `#pragma warning disable` | 0 | 5 (all `src/nextorm.core`) |
| `[SuppressMessage]` | 0 | 6 (all `src/nextorm.core`) |
| `<NoWarn>` | 0 | 0 (the single "NoWarn" hit in `tests/nextorm.core.tests` is the method name `…_NoWarning`, not a directive) |
| `Skip=` | 0 | 0 |
| empty `catch { }` | 0 | 0 |
| `Task.Delay` | 0 | 0 (in touched projects) |
| `TODO` | 0 | 4 (`src/nextorm.core`) |
| `FIXME` | 0 | 0 |
| `HACK` | 0 | 0 |

**Suppressed:justified ratio = 0:11** (0 added suppressions; 11 pre-existing directives in the touched projects, all inline-justified except `InMemoryLinqSource.cs:82` `#pragma warning disable CS8714`, which has no inline comment — a pre-existing gap, not introduced by #166). Overall added slop markers: **0**.

## Scope

- `git status --porcelain`: exactly the three scoped files modified — `src/nextorm.core/Query/QueryCommand.cs`, `tests/nextorm.core.tests/Iteration14CteLookupTests.cs`, `tests/nextorm.sqlite.tests/TypedCteTests.cs`. Pre-existing unrelated dirty/untracked left untouched: `.opencode/skills/nextorm-brainstorming/SKILL.md`, `docs/advanced/limitations.md`, `docs/ru/advanced/limitations.md` (modified); `benchmarks/BenchmarkDotNet.Artifacts/**`, `docs/superpowers/**`, `docs/specs/status/nested-read-cte-1.md`, `docs/specs/status/nested-read-cte-1-evidence/` (untracked).
- `git diff --stat -- src/nextorm.core tests/nextorm.core.tests tests/nextorm.sqlite.tests`: 3 files, **+319 / −6** (QueryCommand.cs 21/6, Iteration14CteLookupTests.cs 248/0, TypedCteTests.cs 50/0).
- `git diff --check`: **exit 0**.
- `git diff --cached`: empty; **nothing staged, no commit** (HEAD unchanged at `79b48fd`).
- CRLF preserved: `file` reports all three as "with CRLF line terminators".

## Public API / CS1591

- `roslyn members NextORM.Core.QueryCommand`: `HasDataModifyingCte` is **internal** property (`QueryCommand.cs:512`), getter **internal** (`:514`). The diff changes only its body. No public member added/changed; the other changed files are test projects (no public API surface). The rv=1 `E09-mechanical-audit.md` line reference (`:504`) is stale vs the current tree (`:512`) — line shift only.
- `GenerateDocumentationFile=true` is set in `src/Directory.Build.props:11`; `CS1591` is not in any `<NoWarn>`. No new public member ⇒ **no new CS1591 obligation**; fresh build **0 Warning(s) / 0 Error(s)** confirms.

## Evidence pointers referenced by the status file

Every artifact path referenced by `docs/specs/status/nested-read-cte-1.md` was checked on disk.

**Missing pointers: none.** All present: `E01`–`E09`, `C1`–`C5`, `SA-READ-{sync,async,combined}`, `SA-MUTATION-classification`, `cov-{core,sqlite,postgres,sqlserver}.cobertura.xml`, `c5-report/`, `E06-perf.md`/`E06-acceptance.log`, `E07-gate.md`/`E07-gate.log`/3× E07 JSON, `E02-*`, `E03-*`, `E04-*`, `E09-build.log`.

Prose discrepancy (not a missing artifact, no verdict): the status file's `Changed files` text states `QueryCommand.cs (+9/−2, …)` and `Iteration14CteLookupTests.cs (+150/−0, r1)`, whereas the actual r=2 tree diff is `+21/−6` and `+248/−0`; `E08-review.md` carries the correct r2 figures. The r=2 DIFF against baseline therefore matches `E08-review.md`, not the stale summary line.

## Row-status summary

| Row | Evidence present? | Command recorded exactly? | Explicit exit code in artifact? |
| --- | --- | --- | --- |
| C0 | yes | N/A by contract | N/A |
| C1 | yes | array in E05 | no (Build succeeded) |
| C2 green / red | yes / yes | JSON array / JSON array | no / **yes (2)** |
| C3 class / full core / full sqlite | yes | `$` in E03 / E03,E05 / E05 | no / no / no |
| C4 core / sqlite / postgres / sqlserver | yes | `$` line / E05 / `$` line / `$` line | no (all `Passed!`) |
| C5 | yes | Arguments block (no array) | no |
| C6 | yes | E06-perf.md exact | no (E06-perf.md records 0) |
| C7 | yes | E07-gate.md exact | sub-run line `exit code 0` |
| C8 | yes | plain `git diff --check` | yes (E08 records 0) |
| SA-READ sync / async / combined | yes | E03-matrix exact | no |
| SA-MUTATION | yes | E03-matrix exact | no |

All rows have evidence present; no row is entirely unrecorded. The only systematically weak slot is the **exit code of green runs**, which no log prints (inferred from `Passed!`/`Build succeeded.` and the status narrative) — flagged, no verdict issued.
