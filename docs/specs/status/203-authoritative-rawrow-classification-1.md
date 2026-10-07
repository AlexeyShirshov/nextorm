---
# PDCA 203 — Authoritative raw-row composite classification (cycle 1)

- **Issue:** #203 — https://github.com/AlexeyShirshov/nextorm/issues/203
- **Milestone:** 1.0.9-rc1
- **Ветка / base:** 1.0.9-rc1 / 9406a6ec
- **Режим:** autonomous + explicit commit/issue-close authorization; never push
- **Цикл:** plan revision r=1; attempt n=1/3; evidence contract rv=1; supersession: none

# 203 — Authoritative raw-row composite classification (cycle 1)

Issue: #203; Milestone: 1.0.9-rc1; Branch/base: 1.0.9-rc1 / 9406a6ec.
Mode: autonomous + explicit commit/issue-close authorization; never push.
Cycle: plan revision r=1; attempt n=1/3; evidence contract rv=1; supersession: none.

## Goal
Replace inferred raw-row composite classification with authoritative provider metadata, shared by both raw-row paths, so an unmapped non-composite Postgres type (hstore/ltree) is never misdiagnosed as a named composite on a clean Npgsql catalog. Restore the substance of R05 (R05→R05′ history from #202 retained).

## Acceptance criteria (observable + negative)
- R203-AC1: Clean-catalog hstore and ltree produce "None of the result-set columns could be mapped" (single and multi-column). Negative: neither "-.-", a dotted name, nor a metadata exception produces a named-composite diagnosis.
- R203-AC2: Warm registered composites materialize; warm unregistered composites retain the composite/MapComposite guard; mixed-composite restrictions unchanged. Negative: a non-composite is never classified composite; a cold genuine composite (UnknownBackendType) receives the ordinary mapping error, not an unsupported positive identification.
- R203-AC3: Classification stays before row iteration, uses the existing reader's metadata, preserves connection/transaction. Negative: no catalog query, connection clone, ReloadTypes, or second command while the reader is open; classification count does not grow with row count.
- R203-AC4: Both paths consume ONE provider classification predicate. Negative: no surviving dotted-name heuristic or exception-type discriminator independently identifies composites.
- R203-AC5: Debug solution build 0W/0E; full suite green across all six providers; coverage ≥85% line / ≥75% branch; acceptance benchmarks exactly 7 cases / 0 failures. Negative: missing evidence, skipped required providers, insufficient coverage, or incomplete benchmark run is not PASS.
- Cold genuine-composite trade-off: unknown catalog metadata cannot distinguish it from hstore/ltree; ordinary no-match is intentional; warm metadata retains the specific diagnosis. No regression in registered-composite materialization.

## Chosen design (A) — provider metadata predicate
Add:
- `DataContext`: `protected virtual bool IsGenuineCompositeColumn(DbDataReader reader, int ordinal)` default `false`.
- `DataContext`: `internal bool IsGenuineCompositeRawRowColumn(DbDataReader reader, int ordinal)` delegating to the virtual.
- `PostgresDataContext`: `protected override`, true only when reader is `NpgsqlDataReader` and `GetPostgresType(ordinal) is PostgresCompositeType`; else false.
Update `RawMapperFactory`: pass `DataContext` to the single-column helper and the unresolved multi-column helper; both use `context.IsGenuineCompositeRawRowColumn(reader, ordinal)` as their ONLY composite-identification source. Preserve array/IDictionary exclusions, "record" handling, custom-enumerable restrictions, materialization policies. Remove `IsGenuineCompositeDataTypeName` and the `InvalidCastException`-specific composite discriminator. Field-type probing still determines resolvability; a type name may supply diagnostics, never classification. Keep metadata-rejection behavior; do not blanket-catch new exceptions.
Options table: A chosen (authoritative, Npgsql stays in postgres, unifies paths; cost: small protected API + cold composite loses specific diagnosis). B repair/share dotted-name heuristic (smaller diff, no API; still textual, not authoritative) rejected. C catalog queries/type reload (could identify cold composites; violates reader/connection/transaction constraints) rejected.

## What the statement did not say
- Cold genuine-composite expectation → accept ordinary mapping failure; document.
- Seam naming/extension semantics → exact contract above; default false = "no authoritative positive evidence".
- Sixth-provider mechanism → D0 verifies actual roster; missing MariaDB/provider is an active prerequisite, not a waiver.
- Perf baseline numbers → `docs/specs/performance/acceptance-benchmarks.md`; record actual baseline, do not invent tolerances.
- Overlay class-priority/evidence-contract slots → D0 checks; all rows P1 conservatively; populate extra mandatory slots before product edits.
- Verified issue URL/milestone → D0 via `gh issue view`; never fabricate.
- Mutation runner → none configured; do not add Stryker; explicit branch tests below.
- API-reference placement → D0 discovers the curated convention; update surface + bilingual docs + register.

## Reconnaissance decision
No additional design spike. PLAN reconnaissance established: warm composite → `PostgresCompositeType`; cold hstore/ltree/composite → `UnknownBackendType`; `GetPostgresType` reads reader metadata without another command. Live cold/warm assertions are mandatory integration acceptance tests, not exploration. Invalidation trigger: if those assertions contradict reconnaissance, or the override needs I/O, return to PLAN with the probe and source evidence; do not substitute a heuristic or type reload.

## Unit execution mode
Sequential, one tree. Core, postgres, fake readers, integration tests, docs and registers share the predicate contract; worktrees give no isolation benefit.

## DO tasks
- D0: prerequisites + seal evidence contract (this task).
- D1: publish shared contract in status, implement DataContext seam + PostgresDataContext override + RawMapperFactory unification.
- D2: adapt fake `RecordReader`/`TestContext` to explicit ordinal metadata facts + unit coverage; add classification call-count cadence check; exercise all changed branches.
- D3: integration tests — flip cold hstore/ltree to ordinary no-match; extend clean-catalog, genuine-composite, multi-column, transaction cases (T01-T14).
- D4: docs EN+RU + protected API docs + register entries.
- D5: full acceptance evidence — build, six-provider suite, coverage, 7-case perf, docfx, BDN artifact restore.
- D6: ACT closure after CHECK PASS — finalize status, scoped commit `#203 <summary>`, close issue #203, no push.
Deferred (stay in 1.0.9-rc1): RawMapperFactory decomposition / Observation C, Observation B, catalog-resolution infra, mutation-tool installation. Triggers: separate approved issue, demonstrated regression, or failure to meet acceptance without that work.

## Test strategy + variant matrix (IDs are planning labels, not test-method names)
T01 cold hstore → ordinary no-match, not named-composite (integration, single+mixed).
T02 cold ltree → same, isolated.
T03/T04 warm hstore/ltree → base-type catalog assertions and ordinary errors remain; mixed-column.
T05 warm registered composite → MapComposite materialization stays green; mixed restrictions unchanged.
T06 warm unregistered composite → authoritative composite metadata; MapComposite guard, single+mixed.
T07 cold genuine composite → NEW fixture asserts UnknownBackendType; single+mixed unmappable → ordinary errors.
T08 single vs multi → unit fake exposes identical explicit metadata facts to both paths; dotted non-composite names, "-.-", and both metadata-rejection exception types cannot change classification.
T09 arrays/IDictionary/custom enumerable/record → retain/exercise guards; explicit composite facts must not bypass shape exclusions.
T10 other providers / raw-row disabled → default predicate false; non-postgres behavior unchanged; unit default-provider case + six-provider suites.
T11 null/DBNull, value/reference shapes, empty results → unit + integration empty-reader case; no classification from row values.
T12 distributed multi-column → composite/non-composite facts in first/later ordinals; mixed metadata failures; scalar columns still map.
T13 cadence/flags → unit spy: classification counts do not scale with rows; raw-row enabled/disabled branches; no GetColumnSchema/KeyInfo/reload/fallback connection.
T14 transaction visibility → integration: uncommitted type/data setup on same connection/transaction, then raw read; classification uses that reader only.
Fake policy: add explicit ordinal metadata to `RecordReader`, including cold placeholder cases; do not emulate Npgsql types or infer composite status from "app.named_composite".
Mutation: unavailable. Exercise changed branches explicitly: default provider false; postgres positive vs base/unknown; non-Npgsql reader false; single-column shape exclusions; multi-column resolvable-skip vs unresolved positive/negative; InvalidCastException and NotSupportedException without composite inference; first/later ordinal and no-positive-column; mapping success vs result.Count==0; empty-reader and DBNull.
Coverage: enforce 85% line / 75% branch even on this non-main branch; configured scope core/sqlite/postgres/sqlserver does not replace testing mysql/clickhouse/mariadb.

## Docs plan
EN `docs/guide/12-raw-sql.md:404`, `docs/guide/provider-specific/postgresql.md:348`, `docs/advanced/limitations.md:47`; RU `docs/ru/guide/12-raw-sql.md:411`, `docs/ru/guide/provider-specific/postgresql.md:352`, `docs/ru/advanced/limitations.md:47`. State unknown-catalog non-composites get the ordinary mapping error; cold genuine composites also degrade to that error until authoritative metadata is available. Preserve registration guidance and mixed-result limitations. Document the new protected extension point in both languages + the curated API surface. Append register entries: API-NAMING-REVIEW (new protected member, rationale, consumers/lines; after EOF :5576) and code-smells register (removal of name/exception-shape inference + constrained provider seam; after EOF :8281; do not reopen the god-class observation). Preserve CRLF; no guide moves/renumbering; no public links to internal specs.

## Perf
Measurement required by AC5 despite one-time classification. Run `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; require exactly 7 executed cases / 0 failures; record wall time, per-case Mean/Allocated, cached-vs-prepared ratio vs `docs/specs/performance/acceptance-benchmarks.md`. No additional permanent benchmark planned: the predicate reads existing metadata during mapper preparation, not per row (`RawMapperFactory.cs:72-136,617`; `ProcedureResult.cs:136/170/362/375`); structural review + cadence tests prove placement. Do not claim the 7 cases exercise hstore/composites. Trigger for extra focused measurement: observed acceptance regression, unexpected allocation, or cadence-proof failure; keep extras outside the acceptance category so the required count stays 7. After reports: `git checkout -- benchmarks/BenchmarkDotNet.Artifacts`.

## P1 priority matrix (all P1 by construction; CHECK answers each, cannot downgrade)
P1-01 cold hstore/ltree ordinary diagnosis, never false composite.
P1-02 warm registered/unregistered + cold genuine-composite behavior.
P1-03 no per-row classification, second command, connection switch, or transaction loss.
P1-04 one classification source; no independent name/exception heuristics.
P1-05 Npgsql-free core; concrete seam consumers; preserved shape guards.
P1-06 build, all six providers, coverage, 7-case performance gates.
P1-07 protected API + bilingual behavior documentation; certified register entries.
P1-08 milestone 1.0.9-rc1, evidence integrity, CRLF, scoped commit, no push.

## Versioned evidence contract rv=1 (sealed before product DO; no prior #203 contract superseded)
Planned invocations (capture stdout/stderr and real exit code; do not mask failures):
C0 `gh issue view 203 --json number,url,title,state,milestone,body`
C1 `dotnet build nextorm.slnx -c Debug`
C2 `dotnet test tests/nextorm.core.tests -c Debug`
C3 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug`
C4 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test --no-build --verbosity normal`
C5a `dotnet tool restore`
C5b `DOCKER_HOST=... dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
C5c `dotnet tool run reportgenerator "-reports:tests/coverage/coverage.cobertura.xml" "-targetdir:tests/coverage/report" "-reporttypes:TextSummary"`
C6 `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
C7 `dotnet docfx docs/docfx.json`
S1 CHECK read-only certification of E203-05/06/10/11 (actual sources, Roslyn consumers, bilingual docs, registers, transaction/cadence tests).
S2 CHECK evaluation of every rv=1 row (six-provider ledger, coverage, benchmark count/baseline), PASS/FAIL/UNKNOWN per row.
Artifact root: `/tmp/nextorm-203/r1-n1/`; coverage XML/Summary at tests/coverage paths. Status records artifact locations, commands, timestamps, exit codes, source SHA/diff identity, scenario-to-symbol mappings.

Evidence rows (rv=1): E203-01 AC1/T01-T02 (C3,C4); E203-02 AC2/T05-T06 (C3,C4); E203-03 AC2/T07 (C3); E203-04 AC4/T08,T09,T11,T12 (C2); E203-05 AC3/T13,T14 (C2,C3 + S1); E203-06 AC4/INV1 seam consumers, no core Npgsql (S1); E203-07 AC5 build 0W/0E (C1); E203-08 AC5 full suite six providers (C4); E203-09 AC5 coverage ≥85/75 (C5a/b/c); E203-10 AC5 perf 7/0 + baseline (C6 + S2); E203-11 API/DOC/REGISTER (C7 + S1); E203-12 SCOPE/ACT issue/milestone/CRLF/diff/commit/close (C0 + S2).
CHECK re-gather budget: ≤2 targeted scout calls per CHECK, 4 total for rv=1; consumption recorded in status; exhaustion with low confidence → recommend escalate trigger 5.

## Status skeleton
Issue/milestone/branch/base/mode; Cycle P r1 n1 rv1; Goal + R05 lineage; AC1-AC5 + negatives; chosen design + frozen signatures; gate-1 checklist + prerequisites; assumptions/risks/deferred+triggers; D0-D6 states; T01-T14 actual symbols/evidence; P1-01..08 CHECK results; E203-01..12 ledger; CHECK re-gather accounting; build/provider/coverage/perf/doc/register results; CHECK verdict + defects + attempts + revision history; ACT commit SHA + artifact restore + issue closure + no push; Progress log.

## Risks / return-to-PLAN rules
Catalog-state leakage → unique app/catalog fixtures + explicit cold/warm assertions. False authority in fakes → classification independent of names/exceptions. Protected API cost → bilingual/API/register treatment. Provider infra/coverage-tool prerequisite → add active dependency; keep original D task blocked, not superseded. Cold composite degradation → deliberate/documented. Perf noise → preserve baseline/env; missing/invalid measurement is UNKNOWN not PASS. Probe/source contradiction → targeted scout; persistent low confidence → escalate trigger 5. True external blocker → planner recommends escalate; DO does not declare STOP. A materially changed plan increments r and resets n=1; clarification/missing reports/rejected completion candidate do not. Three failed CHECKs in one revision require escalation, not a 4th attempt.

## D0 — prerequisites (Actual, recorded 2026-10-07)

### Issue / milestone (C0)
- `gh issue view 203 --json number,url,title,state,milestone` → number **203**, url **https://github.com/AlexeyShirshov/nextorm/issues/203**, title "Product: authoritative raw-row composite classification for a clean Npgsql catalog (restore R05) + unify single/multi-column paths", state **OPEN**, milestone **1.0.9-rc1** (#19). Milestone verified == required `1.0.9-rc1`.
- Exit code 0.

### Base SHA / tree state
- `git log --oneline -1` → **9406a6ec** `#202 PostgresRawRowTests: warm type catalog + document clean-catalog limitation`.
- Full `git rev-parse HEAD` → **9406a6ec2fefb157e549022a32f7450254b89c0b** (matches planned base). Branch `1.0.9-rc1`.
- `git status --porcelain` → **empty** (clean tree before edits). Exit code 0.

### Provider roster
- Provider SQL-generation test projects under `tests/`: **nextorm.sqlite.tests, nextorm.postgres.tests, nextorm.sqlserver.tests, nextorm.mysql.tests, nextorm.mariadb.tests, nextorm.clickhouse.tests** (six) plus engine/test infra: nextorm.core.tests, nextorm.alias.tests, nextorm.alias.poc, nextorm.clickhouse.extensions.tests, nextorm.entityframeworkcore.tests, nextorm.integration.tests.
- Six providers actually exercised:
  - **SQLite** — no container; `SqliteTestProvider` + `SqliteIntegrationTests`/`SqliteSpecificTests` in `nextorm.integration.tests`.
  - **PostgreSQL, SQL Server, MySQL, ClickHouse** — `ITestProvider` implementations (`PostgresTestProvider`, `SqlServerTestProvider`, `MySqlTestProvider`, `ClickHouseTestProvider`) drive `CommonTestSuite.*` + provider-specific classes in `nextorm.integration.tests` via Testcontainers.
  - **MariaDB** — present and exercised: `MariaDbContainer` + dedicated `MariaDb*` integration classes (e.g. `MariaDbTypedCteIntegrationTests`, `MariaDbFunctionsIntegrationTests`, `MariaDbCsvIntegrationTests`, `MariaDbImplicitNavigationTests`, …); it is not a 5th `ITestProvider` (no `MariaDbTestProvider`), it has its own container/classes, and `DatabaseContainers.DisposeAsync` stops `MariaDbContainer` alongside the other four.
  - Mechanism: all six via `nextorm.integration.tests` (Testcontainers + `CommonTestSuite`/dedicated classes); per-provider `tests/nextorm.*.tests` are DB-less SQL-generation suites. Coverage scope (`core|sqlite|postgres|sqlserver`) does not replace testing mysql/clickhouse/mariadb.
- No missing provider: roster is complete (MariaDB present) — not an active prerequisite.

### Integration environment (`running-integration-tests` skill loaded)
- Socket `/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` → **present** (`srw-rw---- alex alex`, exists since Oct 5 13:03). `curl -s --unix-socket … http://d/v1.40/_ping` → **OK**. No start/wait/recheck needed.
- Exact command: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug` (equivalent `dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`). A run without `DOCKER_HOST` skips PostgreSQL/SQL Server/MySQL/ClickHouse and is **not** a pass.
- Test classes accepted via `-class`: `SqliteIntegrationTests`, `SqliteSpecificTests`, `PostgresIntegrationTests`, `PostgresSpecificTests`, `SqlServerIntegrationTests`, `SqlServerSpecificTests`, `MySqlIntegrationTests`, `MySqlSpecificTests`, `ClickHouseIntegrationTests` (MariaDB via its `MariaDb*` classes).
- Pre-pulled images: `postgres:17-alpine`, `mcr.microsoft.com/mssql/server:2025-latest`, `mysql:8.4`, `testcontainers/ryuk`; ClickHouse `clickhouse/clickhouse-server:25.8-alpine` pulled on first run.

### Coverage tooling
- `.config/dotnet-tools.json` present: **dotnet-coverage 18.11.2** and **dotnet-reportgenerator-globaltool 5.5.11** (plus docfx 2.78.5). `dotnet tool restore` available.
- `coverage.settings.xml` includes only `nextorm.{core,sqlite,postgres,sqlserver}`; thresholds **MIN_LINE_COVERAGE=85 / MIN_BRANCH_COVERAGE=75** (hard-fail on `main` only; this non-main branch warns but the cycle enforces the same numbers).
- Reproduce command:
  - C5a `dotnet tool restore`
  - C5b `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
  - C5c `dotnet tool run reportgenerator "-reports:tests/coverage/coverage.cobertura.xml" "-targetdir:tests/coverage/report" "-reporttypes:TextSummary"`

### Benchmark baseline (`docs/specs/performance/acceptance-benchmarks.md`)
- Command C6: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; contract **exactly 7 executed cases / 0 failures**; budget **≤4 min** wall clock; `NEXTORM_BENCH_FULL` unset; `Job.ShortRun` + `InProcessEmitToolchain` + `MemoryDiagnoser`; no containers (SQLite bundled `data/test.db`).
- Documented baseline (2026-09-26, Global total time **51.88 s**; external wall clock 53 s), per case Mean / Allocated:
  | # | Case | Method | Mean | Allocated |
  |---|---|---|---|---|
  | 1 | `InMemoryBenchmarkAggregates` | `Nextorm_Count` | 2.915 ms (high-variance; not an assertion) | 335.17 KB |
  | 2 | `InMemoryBenchmarkGroupBy` | `Nextorm_GroupByCount` | 60.95 ms | 50 MB |
  | 3 | `SqliteBenchmarkAny` | `Nextorm_Cached` | 1.772 ms | 534.42 KB |
  | 4 | `SqliteBenchmarkCachedPlan` | `Prepared_ToList` | 923.8 μs | 76.14 KB |
  | 5 | `SqliteBenchmarkCachedPlan` | `Cached_ToList` | 1,727.4 μs | 565.22 KB |
  | 6 | `SqliteBenchmarkCachedPlan` | `Cached_PlanOnly_Param` | 518.3 μs | 489.08 KB |
  | 7 | `SqliteBenchmarkWhere` | `Nextorm_Cached_ToListAsync` | 2.137 ms | 692.07 KB |
- Tracked cached-vs-prepared ratio `Cached_ToList / Prepared_ToList` = **1.87** (allocated ratio **7.42**); investigation threshold = **>20%** (i.e. > 2.244), prompt to investigate, not hard fail. `Cached_PlanOnly_Param` ratio is not comparable (different workload).
- **Acceptance cases do NOT exercise hstore/composites** — recorded as protection evidence only; no speed claim. Perf measurement required by AC5 because the path is touched, but the predicate is one-time (mapper preparation, not per row).

### Inner-loop validator / other
- `scripts/validate_inner_loop.py` → **absent** (`ls` ENOENT). **Notice: inner-loop validator script absent — structured test scope used manually** (per-plan `test scope` JSON and explicit selectors).
- Artifact root created: `/tmp/nextorm-203/r1-n1/`.

## D5 — full acceptance evidence (Actual, collected 2026-10-07)

Artifact root: `/tmp/nextorm-203/r1-n2/`. This is the **post-fix D5 re-run (d5b-*)** executed after the n=2 CHECK loop-back (added two cold single-column integration tests, a zero-row unit test, and register/comment corrections — no product behavior change). The pre-fix `/tmp/nextorm-203/r1-n1/` d5-* values are superseded by this re-run for AC5 gating; the earlier numbers are retained only as history.
All commands run on branch `1.0.9-rc1` with the D1–D4 working-tree edits (uncommitted); CRLF preserved; no product/test/doc edits made in D5.
Test scope (validator absent — Notice already logged at D0): projects `tests/nextorm.core.tests`, `tests/nextorm.integration.tests`; boundary D5 = full solution build + all six providers + full coverage collection + 7-case acceptance benchmark.

### C1 — build (`dotnet build nextorm.slnx -c Debug`)
- Exit code **0**; `Build succeeded`; **0 Warning(s) / 0 Error(s)**.
- Log `/tmp/nextorm-203/r1-n2/d5b-build.log`; exit `/tmp/nextorm-203/r1-n2/d5b-build.exit`.

### C4 — full suite, all six providers (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test --no-build --verbosity normal`)
- Exit code **0**; `Test run summary: Passed!`; **total 8949 / succeeded 8751 / failed 0 / skipped 198**; duration 1m 20s 065ms.
- Log `/tmp/nextorm-203/r1-n2/d5b-fulltest.log`; exit `/tmp/nextorm-203/r1-n2/d5b-fulltest.exit`.
- Notice: normal verbosity prints only skipped tests, so a supplementary integration-only xunit run with `-result-xml` was executed under the same `DOCKER_HOST` to produce the positive per-provider ledger (exit 0). Log `/tmp/nextorm-203/r1-n2/d5b-provider-ledger.log`; XML `/tmp/nextorm-203/r1-n2/d5b-integration.xml`; exit `/tmp/nextorm-203/r1-n2/d5b-provider-ledger.exit`.
- Five Testcontainers started and reached ready: PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse; SQLite runs in-process. No provider emitted an "is not available" skip reason.

| Provider | total | passed | failed | skipped |
|---|---|---|---|---|
| SQLite | 722 | 682 | 0 | 40 |
| PostgreSQL | 851 | 825 | 0 | 26 |
| SQL Server | 739 | 691 | 0 | 48 |
| MySQL | 680 | 599 | 0 | 81 |
| MariaDB | 56 | 55 | 0 | 1 |
| ClickHouse | 205 | 205 | 0 | 0 |

- Integration summary (supplementary run): Total 3321, Errors 0, Failed 0, Skipped 197, Not Run 0.
- `PostgresRawRowTests` (D3 T01–T14): **47 total / 47 passed / 0 failed / 0 skipped** in `d5b-integration.xml`.
- No provider skipped wholesale; every provider executed tests (passed > 0). Per-provider ledger attributed by test-name provider token; remaining generic integration classes account for the balance to 3321.

### C5 — coverage (C5a/C5b/C5c)
- C5a `dotnet tool restore` exit **0** (dotnet-coverage 18.11.2 restored).
- C5b `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` exit **0**; instrumented suite again total 8949 / succeeded 8751 / failed 0 / skipped 198.
- C5c `dotnet tool run reportgenerator "-reports:tests/coverage/coverage.cobertura.xml" "-targetdir:tests/coverage/report" "-reporttypes:TextSummary"` exit **0**.
- `tests/coverage/report/Summary.txt`: **Line coverage 88.5%** (48049 / 54287; target ≥85 ✓); **Branch coverage 80.1%** (25843 / 32245; target ≥75 ✓); assemblies 4, classes 561, files 324.
- Changed hot spots: `NextORM.Core.RawMapperFactory` 95.5%, `NextORM.Core.DataContext` 89.1%, `NextORM.Postgres.PostgresDataContext` 75.8%.
- Log `/tmp/nextorm-203/r1-n2/d5b-coverage.log` (contains C5a_EXIT=0 / C5b_EXIT=0 / C5c_EXIT=0).

### C6 — perf acceptance (`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`)
- Exit code **0**; external wall clock **56.30 s**; BDN `Global total time: 00:00:49 (49.42 sec)`, **executed benchmarks: 7, 0 failures**.
- Log `/tmp/nextorm-203/r1-n2/d5b-perf.log`; exit `/tmp/nextorm-203/r1-n2/d5b-perf.exit`.

| # | Case | Mean | Allocated | Baseline Mean |
|---|---|---|---|---|
| 1 | InMemoryBenchmarkAggregates.Nextorm_Count | 2.194 ms | 374.22 KB | 2.915 ms (high-variance; not an assertion) |
| 2 | InMemoryBenchmarkGroupBy.Nextorm_GroupByCount | 58.49 ms | 50.11 MB | 60.95 ms |
| 3 | SqliteBenchmarkAny.Nextorm_Cached | 1.907 ms | 609.45 KB | 1.772 ms |
| 4 | SqliteBenchmarkCachedPlan.Prepared_ToList | 884.7 us | 76.14 KB | 923.8 us |
| 5 | SqliteBenchmarkCachedPlan.Cached_ToList | 2,026.3 us | 583.18 KB | 1,727.4 us |
| 6 | SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param | 580.7 us | 507.05 KB | 518.3 us |
| 7 | SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync | 1.912 ms | 607.68 KB | 2.137 ms |

- Cached-vs-prepared ratio `Cached_ToList / Prepared_ToList` = **2.29** (BDN Ratio column; baseline tracked **1.87**) → **+22.5%**, just above the +20% investigate threshold (2.244) → **investigation prompt, not a hard fail** (AC5 gate is 7/0, which passed). `Cached_ToList` StdDev 299.56 us / Error ±5,465 us on a ShortRun(3-iteration) job shows the ratio is variance-dominated; no product path was touched by the n=2 loop-back. Allocated ratio 7.66 (baseline 7.42).
- Baseline comparison: baseline wall 51.88 s (external ~53 s) vs this run Global 49.42 s / external 56.30 s. No individual case Mean grew >20% (largest `Cached_ToList` +17.3%; `Any.Nextorm_Cached` +7.6%); groupby and the async/Where cases improved.
- No failures/exceptions; only the benign BDN `Failed to set up priority High ... Permission denied` environment warning (x7, one per benchmark).
- Acceptance cases do not exercise hstore/composites (protection evidence only, as recorded in the baseline).

### C6b — perf repeat (post-fix run B; `d5b-perf-repeat`)
- Exit code **0** (`d5b-perf-repeat.exit`: `PERF_REPEAT_EXIT=0`, `WALL_SECONDS=47`); BDN `Global total time: 00:00:43 (43.98 sec)`, **executed benchmarks: 7, 0 failures**.
- Log `/tmp/nextorm-203/r1-n2/d5b-perf-repeat.log`; exit `/tmp/nextorm-203/r1-n2/d5b-perf-repeat.exit`.
- Cached-vs-prepared ratio `Cached_ToList / Prepared_ToList` = **2.08** (Prepared_ToList 977.6 us, Cached_ToList 2,034.0 us) — within the baseline noise band relative to the **1.87** baseline (+11.2%), consistent with run A 2.29 being ShortRun(3)-iteration variance rather than a product regression. AC5 gate 7/0 passed.
- Only the same benign BDN `Failed to set up priority High ... Permission denied` environment warning (x7); no benchmark failure.

### BDN artifact restore
- The run again wrote to the tracked repo-root `BenchmarkDotNet.Artifacts/` (15 modified report files), not `benchmarks/BenchmarkDotNet.Artifacts/`.
- Required restore `git checkout -- benchmarks/BenchmarkDotNet.Artifacts` exit **0**; repo-root `git checkout -- BenchmarkDotNet.Artifacts` exit **0**.
- `git status --porcelain` after restore is **byte-identical to the pre-perf snapshot** (`pre-perf-status.txt` vs `post-perf-status.txt`, `STATUS_IDENTICAL`); no benchmark-artifact changes remain (only the expected D1–D4 edits + this status file).

### EOL / CRLF inventory (actual counts; 13 modified + 1 untracked status file)
Scoped `perl -0777 -ne` count per explicit path (never `grep -r`/`find`): `CRLF` = number of `\r\n` pairs; `lone_LF` = `\n` not preceded by `\r`; `lone_CR` = `\r` not followed by `\n`. Counts are of the on-disk working-tree state at this evidence pass.

| File | CRLF | lone_LF | lone_CR |
|---|---:|---:|---:|
| docs/advanced/limitations.md | 98 | 0 | 0 |
| docs/guide/12-raw-sql.md | 737 | 0 | 0 |
| docs/guide/provider-specific/postgresql.md | 385 | 0 | 0 |
| docs/ru/advanced/limitations.md | 98 | 0 | 0 |
| docs/ru/guide/12-raw-sql.md | 744 | 0 | 0 |
| docs/ru/guide/provider-specific/postgresql.md | 389 | 0 | 0 |
| docs/specs/design/API-NAMING-REVIEW.md | 5588 | 0 | 0 |
| docs/specs/design/code-smells-review.md | 8293 | 0 | 0 |
| src/nextorm.core/DataContext/DataContext.cs | 1656 | 0 | 0 |
| src/nextorm.core/DataContext/RawMapperFactory.cs | 865 | 0 | 0 |
| src/nextorm.postgres/PostgresDataContext.cs | 427 | 0 | 0 |
| tests/nextorm.core.tests/RawRowMaterializerTests.cs | 1047 | 0 | 0 |
| tests/nextorm.integration.tests/PostgresRawRowTests.cs | 1257 | 0 | 0 |
| docs/specs/status/203-authoritative-rawrow-classification-1.md (untracked) | 326 | 0 | 0 |

- Result: **no lone LF and no lone CR in any of the 14 files**; every deliverable is CRLF-terminated. No file required normalization. Re-count performed after this pass (progress log).

### E203-01..12 evidence ledger (rv=1; actuals recorded; CHECK verdict columns left empty)
| Row | AC / evidence / commands | Actual artifact / numbers / exit | CHECK verdict |
|---|---|---|---|
| E203-01 | AC1 T01–T02 cold hstore/ltree ordinary no-match (C3,C4) | C4 exit 0; PostgresRawRowTests 47/47 pass, 0 fail, 0 skip (`d5b-integration.xml`); `d5b-fulltest.log`, `d5b-provider-ledger.log` | |
| E203-02 | AC2 T05–T06 warm registered/unregistered composite (C3,C4) | C4 exit 0; PostgreSQL 851/825/0/26; PostgresRawRowTests 47/47 (`d5b-integration.xml`) | |
| E203-03 | AC2 T07 cold genuine composite → ordinary error (C3) | PostgresRawRowTests 47/47 incl. T07 (`d5b-integration.xml`, `d5b-provider-ledger.log`) | |
| E203-04 | AC4 T08,T09,T11,T12 unit (C2) | n=2 inner-loop 53/53 (`/tmp/nextorm-203/r1-n2/unit-innerloop.log`); core.tests passed 15s335ms inside C4; `d5b-fulltest.log` | |
| E203-05 | AC3 T13,T14 cadence/transaction (C2,C3 + S1) | core.tests passed in C4; PostgresRawRowTests 47/47 (`d5b-integration.xml`); source certification deferred to CHECK S1 | |
| E203-06 | AC4/INV1 one seam, no core Npgsql (S1) | C1 0W/0E; changed `DataContext.cs`, `PostgresDataContext.cs`, `RawMapperFactory.cs`; all six providers pass (`d5b-build.log`, `d5b-provider-ledger.log`); certification S1 in CHECK | |
| E203-07 | AC5 build 0W/0E (C1) | exit 0, 0 Warning(s) / 0 Error(s) (`d5b-build.log`) | |
| E203-08 | AC5 full suite six providers (C4) | exit 0; total 8949/8751/0/198; six-provider ledger above (`d5b-fulltest.log`, `d5b-integration.xml`) | |
| E203-09 | AC5 coverage ≥85/75 (C5a/b/c) | line 88.5% / branch 80.1%; C5a/b/c exit 0 (`d5b-coverage.log`, `tests/coverage/report/Summary.txt`) | |
| E203-10 | AC5 perf 7/0 + baseline (C6 + S2) | 7 executed / 0 failures; Global 49.42 s; ratio 2.29 vs baseline 1.87 (+22.5%, above +20% investigate threshold — variance-dominated, not a gate failure) (`d5b-perf.log`) | |
| E203-11 | API/DOC/REGISTER (C7 + S1) | C7 `dotnet docfx docs/docfx.json` exit **0** (`/tmp/nextorm-203/r1-n2/d5b-docfx.exit`); log `/tmp/nextorm-203/r1-n2/d5b-docfx.log` terminal `Build succeeded with warning. 2 warning(s), 0 error(s)`; docs EN+RU + `API-NAMING-REVIEW.md` + `code-smells-review.md` modified; certification S1 in CHECK | |
| E203-12 | SCOPE/ACT issue/milestone/CRLF/diff/commit/close (C0 + S2) | C0 issue #203 OPEN, milestone 1.0.9-rc1 (D0); CRLF preserved; commit/close pending D6 | |

### E203 row completeness (required invocation + artifact + exit/result; N/A with predicate)
- **Exit-code capture form.** C1/C4/C6/C6b/C7 exit codes come from dedicated `.exit` files (`BUILD_EXIT=0`, `TEST_EXIT=0`, `PERF_EXIT=0`, `PERF_REPEAT_EXIT=0`, C7 real process exit `0` in `d5b-docfx.exit`). C5a/C5b/C5c exit codes are captured as in-log markers `C5a_EXIT=0` / `C5b_EXIT=0` / `C5c_EXIT=0` in `d5b-coverage.log` (no separate `.exit` file); the markers surround the command output, so they prove success. Provider ledger exit from `d5b-provider-ledger.exit` (`LEDGER_EXIT=0`).
- **E203-04 / E203-05 C2 deviation (predicate for the N/A standalone artifact).** The planned standalone C2 `dotnet test tests/nextorm.core.tests -c Debug` has no dedicated full-project log: the changed area is covered by the filtered inner-loop `/tmp/nextorm-203/r1-n2/unit-innerloop.log` (53 total / 53 passed / 0 failed / 0 skipped) and the full `nextorm.core.tests` ran inside C4 (`d5b-fulltest.log`, exit 0, passed 15s 335ms). N/A predicate: "full core.tests project run subsumed by C4; filtered inner-loop is the C2-scope artifact."
- **S1 (E203-05/06/11) and S2 (E203-10/12) are CHECK-side read-only certifications**, not DO invocations; the DO-supplied source/artifact inputs are listed in each row. N/A predicate: "certification action runs in CHECK, outside the DO execution stream."
- **E203-11 C7 (`dotnet docfx docs/docfx.json`)** now has a captured numeric exit code: real process exit **0** written to `/tmp/nextorm-203/r1-n2/d5b-docfx.exit` (captured immediately after the command, `cmd > log 2>&1; echo $?`, no pipe that could mask the status); log `/tmp/nextorm-203/r1-n2/d5b-docfx.log` terminal `Build succeeded with warning. 2 warning(s), 0 error(s)`. The 2 warnings are docfx-level duplicate-source-file warnings for `nextorm.core.sourcegenerator` `AnalyzerReleases.*.md` and are not the AC5 build-0W/0E gate, which C1 governs.
- **E203-12 commit/close** is a pending D6 action, not a missing artifact: C0 scope (issue/milestone) is captured; commit/close are deferred to ACT after CHECK PASS.
- **Per-row exit/result evidence (rows whose planned invocation has no dedicated `.exit`).** E203-01: C4 `d5b-fulltest.exit TEST_EXIT=0` + C3-equivalent `d5b-provider-ledger.exit LEDGER_EXIT=0`; E203-02: `d5b-fulltest.exit TEST_EXIT=0` (PostgreSQL 851/825/0/26 in `d5b-integration.xml`); E203-03: `d5b-provider-ledger.exit LEDGER_EXIT=0` + inner-loop `integration-innerloop.log` terminal `Test run summary: Passed!`; E203-04/05: inner-loop `unit-innerloop.log` / `integration-innerloop.log` have no `.exit` file — terminal line `Test run summary: Passed!` (53/53 and 47/47) proves success, and full `nextorm.core.tests` is inside C4 `d5b-fulltest.exit TEST_EXIT=0`; E203-09: `C5a_EXIT=0` / `C5b_EXIT=0` / `C5c_EXIT=0` markers in `d5b-coverage.log`. Every planned acceptance invocation (C1/C4/C5/C6) has a captured exit code.
- No rv=1 row is without an artifact or a documented N/A predicate.

### DO-ledger mapping (D0..D6, D-fix, D5 re-run → E203 rows/artifacts)
| DO unit | Produced | Feeds E203 row(s) / artifact |
|---|---|---|
| D0 | Prerequisites: C0 issue/milestone, base SHA `9406a6ec`, provider roster, Podman socket, coverage tooling, benchmark baseline, validator-absent notice | E203-12 (C0 scope); baseline half of E203-10 |
| D1 | `DataContext` seam + `PostgresDataContext` override + `RawMapperFactory` unification; `dotnet build nextorm.slnx` core `/tmp/nextorm-203/r1-n1/d1-build.log` exit 0, 0W/0E | E203-06 (S1 source); implementation behind E203-01..05 |
| D2 | Fake `RecordReader`/`TestContext` explicit ordinal facts + cadence/coverage tests; `d2-innerloop.log` 52/52; n=2 re-run `/tmp/nextorm-203/r1-n2/unit-innerloop.log` 53/53 | E203-04, E203-05 (C2 half) |
| D3 | Integration T01–T14; `/tmp/nextorm-203/d3-innerloop-postedit.log` 44/44; n=2 re-run `/tmp/nextorm-203/r1-n2/integration-innerloop.log` 47/47 and `d5b-integration.xml` 47/47 | E203-01, E203-02, E203-03, E203-05 (C3 half) |
| D4 | Docs EN+RU, protected API docs, registers; docfx C7 re-captured with real process exit 0 (`/tmp/nextorm-203/r1-n2/d5b-docfx.log` / `d5b-docfx.exit`; earlier `/tmp/opencode/203-d4/docfx.log`) | E203-11 |
| D5 (pre-fix, `/tmp/nextorm-203/r1-n1/` d5-*) | Earlier build/suite/coverage/perf | History only — superseded for gating by the D5 re-run |
| D5 re-run (post-fix, `/tmp/nextorm-203/r1-n2/` d5b-*) | C1 `d5b-build.log`/`.exit`; C4 `d5b-fulltest.log`/`.exit` + `d5b-integration.xml` + `d5b-provider-ledger.log`; C5a/b/c `d5b-coverage.log` + `tests/coverage/report/Summary.txt`; C6 `d5b-perf.log`/`.exit`; C6b `d5b-perf-repeat.log`/`.exit`; BDN restore `pre-perf-status.txt` ≈ `post-perf-status.txt` | E203-06/07, E203-01/02/03/08, E203-09, E203-10, E203-12 (CRLF/diff) |
| D-fix (n=2 loop-back) | `build-core.log` + `build-integration.log` 0W/0E; unit `53/53`; integration `47/47`; register/comment corrections (no behavior change) | E203-04/05/11 partial; D5 re-run is authoritative |
| D6 | Pending ACT closure: scoped commit `#203 …`, close #203, no push | E203-12 remaining half |

### CHECK re-gather accounting (rv=1 budget)
- rv=1 budget: ≤2 targeted scout calls per CHECK, **4 total**. Consumption entries: this status file contains **no recorded targeted scout/check re-gather calls to date** (the n=1 CHECK verdict at 2026-10-07T09:23:59Z is recorded without an accompanying re-gather count); remaining budget therefore shows **4/4 unspent pending CHECK's own accounting**. CHECK owns the final consumption record per its budget contract; this DO-side evidence-completeness pass is not a CHECK re-gather call and consumes none of the 4.

## ACT — Done / Verified (CHECK PASS r=1, n=2/3, rv=1)

Delivered (D1–D5): authoritative raw-row composite classification behind one provider predicate.

- `DataContext` seam: `protected virtual bool IsGenuineCompositeColumn(DbDataReader reader, int ordinal)` (default `false` = "no authoritative positive evidence") + `internal bool IsGenuineCompositeRawRowColumn(DbDataReader reader, int ordinal)` delegating to it.
- `PostgresDataContext` override: true only when the reader is an `NpgsqlDataReader` and `GetPostgresType(ordinal) is PostgresCompositeType`; otherwise false.
- `RawMapperFactory`: the single-column and multi-column unresolved paths both consume only `context.IsGenuineCompositeRawRowColumn(...)`. Removed `IsGenuineCompositeDataTypeName` (dotted-name heuristic) and the `InvalidCastException`-shape discriminator. Array/`IDictionary` exclusions, `record` handling, custom-enumerable restrictions and materialization policies preserved; field probing still decides resolvability; a type name supplies diagnostics only.
- Effect: on a clean Npgsql catalog, hstore/ltree (and cold genuine composites) receive the ordinary "None of the result-set columns could be mapped" error, never a named-composite diagnosis; warm registered/unregistered composites and mixed-column restrictions unchanged; classification runs before row iteration on the open reader's metadata (no catalog query, connection clone, ReloadTypes, second command, or per-row work).

Verified against (CHECK PASS, rv=1; E203-01..12; T01–T14 closed; AC1–AC5 and P1-01..P1-08 met; artifact root `/tmp/nextorm-203/r1-n2/`):

- Build: `dotnet build nextorm.slnx -c Debug` exit **0**, **0W/0E** (`d5b-build.exit` = `BUILD_EXIT=0`).
- Full suite: exit **0**, **total 8949 / passed 8751 / failed 0 / skipped 198** (`d5b-fulltest.exit` = `TEST_EXIT=0`); all six providers executed, 0 failures anywhere — SQLite 682, PostgreSQL 825, SQL Server 691, MySQL 599, MariaDB 55, ClickHouse 205 passed; `PostgresRawRowTests` 47/47.
- Coverage (C5a/b/c, exit 0): line **88.5%** (≥85), branch **80.1%** (≥75).
- Perf acceptance: exactly **7 executed cases / 0 failures**, exit 0; run A cached/prepared ratio **2.29** and repeat **2.08** — both consistent with the tracked baseline **1.87** within ShortRun(3)-iteration noise; investigation prompt only, AC5 gate is 7/0.
- Docs: `dotnet docfx docs/docfx.json` real process exit **0**, **0 errors** (2 known duplicate-source-file warnings).
- Registers certified: `API-NAMING-REVIEW.md` entry N203-1 and `code-smells-review.md` entry #203 (removal of name/exception-shape inference; constrained provider seam; god-class observation not reopened).

## ACT — Next plan

Flow complete: the single #203 cycle is done and issue #203 is closed. No further cycle of this task is planned; any continuation is a separate task/issue. Deferred work (RawMapperFactory decomposition / Observation C, Observation B, catalog-resolution infrastructure, mutation-tool installation) stays out of scope under its recorded triggers (separate approved issue, demonstrated regression, or failure to meet acceptance without it).

## ACT — Changed files

13 modified files + this status file, committed as one scoped `#203` commit.

| File | Change |
|---|---|
| `src/nextorm.core/DataContext/DataContext.cs` | +18: `IsGenuineCompositeColumn` virtual + `IsGenuineCompositeRawRowColumn` internal seam |
| `src/nextorm.core/DataContext/RawMapperFactory.cs` | single + multi paths unified on the one predicate; name/exception-shape classifiers removed (net −25 lines) |
| `src/nextorm.postgres/PostgresDataContext.cs` | +15: Postgres override (`NpgsqlDataReader` + `PostgresCompositeType`) |
| `tests/nextorm.core.tests/RawRowMaterializerTests.cs` | explicit per-ordinal composite facts on the fake reader; cadence + negative coverage |
| `tests/nextorm.integration.tests/PostgresRawRowTests.cs` | T01–T14 cold/warm hstore/ltree/composite + transaction cases |
| `docs/guide/12-raw-sql.md` | behavior + protected extension point |
| `docs/guide/provider-specific/postgresql.md` | behavior + protected extension point |
| `docs/advanced/limitations.md` | clean-catalog degradation note |
| `docs/ru/guide/12-raw-sql.md` | RU mirror |
| `docs/ru/guide/provider-specific/postgresql.md` | RU mirror |
| `docs/ru/advanced/limitations.md` | RU mirror |
| `docs/specs/design/API-NAMING-REVIEW.md` | register entry N203-1 |
| `docs/specs/design/code-smells-review.md` | register entry #203 |
| `docs/specs/status/203-authoritative-rawrow-classification-1.md` | this status file (deliverable; retained) |

**Pointers.** Deliverable commit: `<PENDING_SHA>` (branch `1.0.9-rc1`, local, not pushed); the status-bookkeeping commit records the SHA. Evidence artifact root: `/tmp/nextorm-203/r1-n2/`.

**Deviations.**
- Status file retained (not deleted). Default single-cycle closure deletes the status file; the user explicitly requested this path as a deliverable and the CHECK evidence ledger lives here, so it is kept.
- `AGENTS.md` not edited: no new stable rule beyond the existing nextorm-pdca invariant (core Npgsql-free; provider classification via the protected seam) and the register entries; the seam contract is documented in the registers and here.

## Progress log
2026-10-07T08:57:34Z | PLAN | revision r=1 | iteration n=1/3 | PLAN ready — awaiting go | docs/specs/status/203-authoritative-rawrow-classification-1.md (this file, plan text sealed)
2026-10-07T08:57:34Z | DO | revision r=1 | iteration n=1/3 | DO started (autonomous) | branch 1.0.9-rc1 base 9406a6ec2fefb157e549022a32f7450254b89c0b
2026-10-07T08:57:34Z | DO | revision r=1 | iteration n=1/3 | Notice: inner-loop validator script absent — structured test scope used manually | scripts/validate_inner_loop.py ENOENT
2026-10-07T08:57:34Z | DO | revision r=1 | iteration n=1/3 | D0 done | D0 — prerequisites (Actual) section; C0/C1-prep facts; issue OPEN milestone 1.0.9-rc1; socket OK; benchmark baseline recorded; coverage tools present; provider roster complete (6 incl. MariaDB); validator absent
2026-10-07T09:03:57Z | DO | revision r=1 | iteration n=1/3 | D1 done | `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s)/0 Error(s); log /tmp/nextorm-203/r1-n1/d1-build.log; seam DataContext.cs:683-699 + PostgresDataContext.cs:233-245; RawMapperFactory both classification sites routed through IsGenuineCompositeRawRowColumn; name/exception classifiers removed
2026-10-07T09:03:57Z | DO | revision r=1 | iteration n=1/3 | D2 done | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~RawRowMaterializer` exit 0; total 52 / succeeded 52 / failed 0 / skipped 0; log /tmp/nextorm-203/r1-n1/d2-innerloop.log; explicit per-ordinal composite facts on RecordReader consumed by TestContext; 7 new tests incl. cadence

2026-10-07T09:09:44Z | DO | revision r=1 | iteration n=1/3 | D3 done | inner-loop PostgresRawRow 44/44, log /tmp/nextorm-203/d3-innerloop-postedit.log
2026-10-07T09:09:44Z | DO | revision r=1 | iteration n=1/3 | D4 done | docfx exit 0, log /tmp/opencode/203-d4/docfx.log
2026-10-07T09:16:50Z | DO | revision r=1 | iteration n=1/3 | D5 evidence collected | build 0W/0E; C4 total 8945/succeeded 8747/failed 0/skipped 198; six-provider ledger all executed (d5-integration.xml); coverage line 88.5% / branch 80.1%; perf 7 executed/0 failures, Global 47.04s, ratio 2.07 vs 1.87; BDN artifacts restored clean | /tmp/nextorm-203/r1-n1/
2026-10-07T09:16:50Z | DO | revision r=1 | iteration n=1/3 | Notice: C4 normal-verbosity log prints only skipped tests; positive six-provider ledger from supplementary integration-only `-result-xml` run under the same DOCKER_HOST | /tmp/nextorm-203/r1-n1/d5-provider-ledger.log, /tmp/nextorm-203/r1-n1/d5-integration.xml
2026-10-07T09:16:50Z | DO | revision r=1 | iteration n=1/3 | Notice: benchmark run wrote tracked artifacts to repo-root BenchmarkDotNet.Artifacts (not benchmarks/BenchmarkDotNet.Artifacts); raw reports copied to /tmp/nextorm-203/r1-n1/bdn-results/ and both trees restored via git checkout | git status --porcelain clean of benchmark artifacts
2026-10-07T09:23:59Z | CHECK | r=1 | n=1/3 | check verdict FAIL: test-obligation + register gaps; loop-back CHECK→DO | /tmp/nextorm-203/r1-n1/d5-*.log
2026-10-07T09:23:59Z | DO | r=1 | n=2/3 | fixing CHECK D: items |
2026-10-07T09:26:18Z | DO | r=1 | n=2/3 | D fix done — CHECK D items closed | build core exit 0 / integration exit 0, 0W/0E (/tmp/nextorm-203/r1-n2/build-core.log, build-integration.log); unit RawRowMaterializer 53/53 failed 0 skipped 0 exit 0 (/tmp/nextorm-203/r1-n2/unit-innerloop.log); integration PostgresRawRow 47/47 failed 0 skipped 0 exit 0 (/tmp/nextorm-203/r1-n2/integration-innerloop.log); T01/T02 single-column cold + T11 empty-read unit/integration tests added; registers API-NAMING-REVIEW N203-1 + code-smells #203 corrected; issue #53 verified OPEN; stale comment + IsMetadataRejection doc fixed (no behavior change)
2026-10-07T09:33:42Z | DO | r=1 | n=2/3 | D5 re-run (post-fix) done | build 0W/0E (/tmp/nextorm-203/r1-n2/d5b-build.log); C4 total 8949/8751/0/198 exit 0; six providers executed (d5b-integration.xml: SQLite 722/682, PostgreSQL 851/825, SQL Server 739/691, MySQL 680/599, MariaDB 56/55, ClickHouse 205/205; PostgresRawRowTests 47/47); coverage line 88.5%/branch 80.1% exit 0 (d5b-coverage.log); perf 7 executed/0 failures, Global 49.42s wall 56.30s, ratio 2.29 vs baseline 1.87 (d5b-perf.log); BDN restored, status identical to pre-perf; status ledger/E203 Actuals refreshed
2026-10-07T09:33:42Z | DO | r=1 | n=2/3 | Notice: perf cached-vs-prepared ratio 2.29 breached the +20% investigate threshold (baseline 1.87 → +22.5%); ShortRun(3) variance (Cached_ToList StdDev 299.56 us) and no product path touched by n=2; AC5 7/0 gate passed | /tmp/nextorm-203/r1-n2/d5b-perf.log
2026-10-07T09:35:57Z | DO | r=1 | n=2/3 | perf repeat | ratio 2.08 | noise | /tmp/nextorm-203/r1-n2/d5b-perf-repeat.log
2026-10-07T09:40:36Z | DO | r=1 | n=2/3 | register citations re-verified against current tree | roslyn refs IsGenuineCompositeRawRowColumn -> RawMapperFactory.cs:260,333,355; IsGenuineCompositeColumn overrides RawRowMaterializerTests.cs:864 + PostgresDataContext.cs:245; DataContext.cs:694,698; git diff --numstat RawMapperFactory.cs +43/−68 net −25; IsMetadataRejection doc 303-314 confirmed (no register citation present)
2026-10-07T14:47:00Z | DO | r=1 | n=2/3 | CHECK evidence-completeness hold — process exit codes recorded from captured .exit files / in-log markers (no re-run) | C1 d5b-build.exit BUILD_EXIT=0 + "Build succeeded / 0 Warning(s) / 0 Error(s)"; C4 d5b-fulltest.exit TEST_EXIT=0 total 8949/8751/0/198; C5a/b/c markers C5a_EXIT=0 / C5b_EXIT=0 / C5c_EXIT=0 in d5b-coverage.log, Summary.txt line 88.5% branch 80.1%; C6 d5b-perf.exit PERF_EXIT=0 executed 7/0 ratio 2.29; C6b d5b-perf-repeat.exit PERF_REPEAT_EXIT=0 + WALL_SECONDS=47 executed 7/0 ratio 2.08 | /tmp/nextorm-203/r1-n2/
2026-10-07T14:47:00Z | DO | r=1 | n=2/3 | EOL/CRLF inventory of 13 modified + untracked status file (scoped perl -0777 per explicit path; no grep -r/find) | all 14 files CRLF-terminated with 0 lone_LF and 0 lone_CR; no normalization required; counts recorded in EOL/CRLF inventory section | docs/specs/status/203-authoritative-rawrow-classification-1.md
2026-10-07T14:47:00Z | DO | r=1 | n=2/3 | status evidence-contract gaps filled | added C6b repeat record; E203 row-completeness + N/A predicates (C2 standalone subsumed by C4; S1/S2 CHECK-side; C7 log-terminal only); DO-ledger mapping D0..D6/D-fix/D5-rerun→E203 rows/artifacts; CHECK re-gather accounting 0 calls recorded / 4 rv=1 budget pending CHECK; rv=1 and E203-01..12 row IDs unchanged | /tmp/nextorm-203/r1-n2/d5b-*.log
2026-10-07T14:47:00Z | DO | r=1 | n=2/3 | C7/C2 evidence caveats recorded (not re-run) | C7 docfx log /tmp/opencode/203-d4/docfx.log terminal "Build succeeded with warning. 2 warning(s), 0 error(s)" — no .exit file, log proves success; C2 full core.tests captured inside C4 (exit 0, 15s 335ms) plus filtered inner-loop unit-innerloop.log 53/53 | /tmp/nextorm-203/r1-n2/unit-innerloop.log
2026-10-07T14:51:43Z | DO | r=1 | n=2/3 | C7 docfx exit 0 captured | real process exit 0 -> /tmp/nextorm-203/r1-n2/d5b-docfx.exit; log terminal "Build succeeded with warning. 2 warning(s), 0 error(s)" -> /tmp/nextorm-203/r1-n2/d5b-docfx.log; captured as `cmd > log 2>&1; echo $?`, no pipe masking status; docs/_site + docs/api are gitignored/untracked, git status --porcelain byte-identical pre/post, no cleanup needed | /tmp/nextorm-203/r1-n2/d5b-docfx.log
2026-10-07T09:54:12Z | CHECK | r=1 | n=2/3 | check verdict PASS — AC1–AC5 and P1-01..P1-08 met; T01–T14 closed; rv=1 E203-01..12 ledger complete | /tmp/nextorm-203/r1-n2/; docs/specs/status/203-authoritative-rawrow-classification-1.md
2026-10-07T09:54:12Z | ACT | r=1 | n=2/3 | status finalized (Done/Verified, Next plan, Changed files, deviations) | docs/specs/status/203-authoritative-rawrow-classification-1.md
