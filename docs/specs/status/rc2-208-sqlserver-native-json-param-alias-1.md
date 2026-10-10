# Task T208 / #208 — SQL Server native JSON: projected SqlFunctions.Parameter<T> alias

- collection: `rc2-final`
- intent: own PLAN (COLLECTION TASK PLAN); phase P (no DO yet)
- selected_variant: `pdca-dotnet`
- cycle_id: N=1
- plan_revision: r=1
- baseline: `2ac20818`
- planning HEAD: `9a2a2871`
- plan_state: ready
- status file: `docs/specs/status/rc2-208-sqlserver-native-json-param-alias-1.md`
- persisted: 2026-10-09

---
# T208 — SQL Server native JSON: alias projected parameters
- Collection `rc2-final`; issue #208; variant `pdca-dotnet`; cycle N=1; plan revision r=1.
- Branch `1.0.9-rc2`; planning HEAD `9a2a2871`; regression baseline `2ac20818`.
- Status: `docs/specs/status/rc2-208-sqlserver-native-json-param-alias-1.md`; `plan_state=ready`.
- Planning only: persist this plan now; no implementation, tests, commits, or merges in this lane. Implementation prerequisites below remain mandatory.

## Goal and acceptance criteria
Restore native SQL Server JSON execution for eligible object projections containing `SqlFunctions.Parameter<T>`, without disabling native eligibility or changing query/cache state.
- **A208.1 — Alias:** every admitted projected parameter receives its exact projected-member alias. Negative: a bare `@pN` select item must fail the regression assertion; parameter aliases must not appear in WHERE expressions.
- **A208.2 — Execution:** eligible projected parameters execute as native SQL Server JSON and produce the expected parsed JSON. Negative: neither the unnamed-column SqlException nor silently selecting fallback counts as success.
- **A208.3 — Surfaces/state:** sync and async execution on `QueryCommand<TResult>` and `EntityBuilder<TEntity>` bind changed values correctly across repeated calls. Negative: stale values, leaked parameters, or sticky cache-policy changes fail.
- **A208.4 — Boundaries:** existing shape eligibility, unsupported-shape handling, ordinary SELECT behavior, and non-SQL-Server fallback remain valid. Negative: admitting an unsupported shape or forcing another provider onto SQL Server syntax fails.
- **A208.5 — Evidence/quality:** the same projected regression is red before the fix and green afterward; build, applicable tests, container providers, coverage and acceptance benchmarks have complete evidence. Negative: zero selected tests/benchmarks, skipped required providers, or absent reports are not passing evidence.

## Minimal solution and alternatives
- **Essence:** correct alias bookkeeping for a projected parameter expression.
- **Constraints:** preserve eligibility, exact aliases, binding, provider boundaries and cache policy; no serialization workaround or broad renderer rewrite.
- **Optimum:** use the existing alias contract at the expression translator, subject to confirming its projection/reset lifecycle.

| Approach | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Mark the Parameter arm alias-required | Small semantic fix; downstream alias machinery already exists | Shared SELECT translation; verify flags do not leak into WHERE or later columns | Selected |
| Require aliases centrally under `ExactProjectionAliases` | Restricts behavior to exact-alias rendering | Broader renderer policy and other expression cases | Alternative only if scout disproves the selected lifecycle |
| Reject projected Parameter as native-ineligible | Avoids the exception | Loses admitted native behavior and violates A208.2 | Rejected |

Do not invent `ColumnName`, mutate commands, or change eligibility merely to make tests green. A necessary change of approach returns to PLAN.

## Findings: fix now / deferred
- **Fix now:** missing parameter-expression alias signal, `NormSqlTranslator.cs:71-81`; resulting rendering at `SqlSourceRenderer.cs:1115-1136` and `SqlBuilder.cs:511-518`.
- **Fix now:** absent projected-Parameter regression in the SQL-generation, eligibility and live SQL Server test homes.
- **Preserve/test:** admission at `JsonShapePlan.cs:348-368` and `JsonNativeStream.cs:58-96`; admission is not itself evidence of an eligibility defect.
- **Deferred:** unrelated slash-escaping assertion from the historical log; trigger: reproduce independently after parsing JSON rather than comparing escaped text.
- **Deferred:** general unnamed-expression alias audit; trigger: another admitted expression fails the focused alias contract. Do not broaden T208 proactively.

## What the statement did not supply
- Authoritative `pdca-dotnet` contract schema, numbered nextorm invariants 1–10, class-priority registry and integration skill: **D208.0 prerequisite** obtains their text and records applicability.
- Alias-flag reset/projection lifecycle: **targeted Roslyn scout**, not an assumption.
- Array/root eligibility and null-output rules: establish from existing contracts/tests before selecting expected assertions.
- Issue URL, predecessor IDs and shared-file reservations: obtain collection registry evidence.
- Baseline test compatibility, acceptance benchmark coverage, tools and container socket: D208.0 verifies them.

## Tasks and execution mode
- **D208.0 — Prerequisites, fix now:** scout the flag lifecycle and existing shape contracts; load required skills/registries; verify issue tracking, baseline and tools; reserve shared files. Freeze the evidence manifest before implementation.
- **D208.1 — Regression, fix now:** add tests to the three test homes; tag focused projected regressions with `PDCA=T208`. Produce a test-only baseline patch and red evidence against `2ac20818`.
- **D208.2 — Implementation, fix now:** repair alias signaling at `NormSqlTranslator.cs:71-81`; retain existing downstream alias selection. Run focused alias, binding, WHERE and ordinary-SELECT tests.
- **D208.3 — Verification, fix now:** complete the variant matrix, full provider evidence, coverage and acceptance benchmark comparison; produce the manifest and handoff.
- **Mode:** sequential, single lane, current worktree. Shared translators/renderers prohibit concurrent edits.
- **Footprint:** expected production write `src/nextorm.core/Visitors/NormSqlTranslator.cs`; tests `SqlServerNativeJsonSqlTests.cs`, `SqlServerNativeJsonStreamTests.cs`, `JsonShapeWriterTests.cs`; status and `artifacts/pdca/D208/rv1/**`.
- **Uncertainty:** `SqlSourceRenderer.cs`/`SqlBuilder.cs` only if the selected contract cannot carry the alias; that requires replan.

## Test strategy and closed variant matrix
Unit/SQL-generation tests for deterministic alias and eligibility checks; live integration for SQL Server FOR JSON validation, binding and repeated execution. Assert parsed JSON, not slash spelling.

| Variant | Closure | Project / selector |
|---|---|---|
| Native-eligible object projection containing Parameter | test: exact alias plus actual native execution | `nextorm.sqlserver.tests` / `FullyQualifiedName~SqlServerNativeJsonSqlTests`; integration / `PDCA=T208` |
| Same parameter in array/root shapes | test + guard | `nextorm.core.tests` / `FullyQualifiedName~JsonShapeWriterTests`; integration / `PDCA=T208` |
| Sync versus async | test: both, incl. changed values on repeated calls | `nextorm.integration.tests` / `PDCA=T208` |
| QueryCommand and EntityBuilder surfaces | test: both surfaces × both modes | `nextorm.integration.tests` / `PDCA=T208` |
| SQL Server native versus non-SQL-Server fallback | test: observe native branch; exercise SQLite fallback; preserve other-provider suites | integration / `PDCA=T208`, then full integration suite |
| Unaliased-parameter negative | test: baseline missing-alias assertion and live unnamed-column exception | SQL-generation class and integration / `PDCA=T208` |
| Value/reference; default/null; binding placement | test: int nondefault/zero, string nonempty/null where admitted; guard unsupported; WHERE-only control | focused selectors above |

Coverage: included modules core/sqlite/postgres/sqlserver; thresholds **85% line / 75% branch**. Priority: A208.1–A208.4 and their contract rows are **P1 by construction**.

## Design checklist, docs, performance and reconnaissance
- SOLID/DRY: reuse translator-to-renderer alias responsibility; no provider-specific duplicate translator or alternate serialization path.
- Type/performance: preserve generic parameter/provider typing; avoid reflection, extra allocations and cache-key changes.
- Nextorm invariants 1–10: D208.0 records each as applicable/not applicable with reason and evidence-row mapping.
- Docs: update this status and evidence manifest; public guides/API and Russian mirrors **not touched** unless an actual public contract changes.
- **Performance required:** `NormSqlTranslator.cs:71-81` feeds query preparation; native preparation uses `DataContext.cs:397-410` and `QueryPlanner.cs:545`. Query-path/plan-cache work → run `--anyCategories=acceptance`. Baseline clean export of HEAD `9a2a2871` vs candidate; planned tolerance ≤5% median time regression and no allocation increase.
- **Reconnaissance required, bounded:** D208.0 proves flag reset/projection scope, shape policy, baseline compatibility and acceptance-case coverage through Roslyn references plus existing contracts. Observable completion: recorded lifecycle references, expectations and executable selectors.

## Versioned evidence contract — rv1
Pin **before implementation** at `artifacts/pdca/D208/rv1/manifest.json` (planned, not existing). Row IDs: A208.5/E208.00 prerequisites; A208.1–2/E208.01 red projected regression (baseline C2/C4 nonzero from missing alias, no infra failure); A208.1,4/E208.02 green alias + WHERE controls; A208.4/E208.03 shape/value/null guards; A208.2–4/E208.04 native/surfaces/repeated/fallback live; A208.3–5/E208.05 build + full integration (SQLite/PG/SQL Server/MySQL/ClickHouse executed, not skipped); A208.5/E208.06 coverage 85/75; A208.3,5/E208.07 acceptance benchmarks; A208.1–5/E208.08 independent CHECK.
Planned calls: C1 `dotnet build -c Debug`; C2 `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~SqlServerNativeJsonSqlTests"`; C3 `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~JsonShapeWriterTests"`; C4 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -trait "PDCA=T208"`; C5 full integration `-noColor`; C6 `bash artifacts/pdca/D208/rv1/run-red.sh 2ac20818`; C7 dotnet-coverage collect; C8 reportgenerator; C9 `bash artifacts/pdca/D208/rv1/run-perf.sh 9a2a2871 candidate`; C10 CHECK. CHECK re-gather budget: two targeted batches.

## Risks, prerequisites and handoff
- Risks: shared alias flags, baseline test-port compatibility, silently falling back instead of proving native execution, provider skips, unrelated changes contaminating performance.
- Predecessors: collection scheduler must identify/release any active owners of translator/render files; no specific predecessor supplied.
- **Handoff:** task=T208/#208; variant=pdca-dotnet; plan_state=ready; r=1; baseline=2ac20818; HEAD=9a2a2871; footprint=NormSqlTranslator + three test homes + status/evidence, conditional benchmark case; uncertainty=flag lifecycle/shape rules/registry prerequisites; predecessors=shared-file owners TBD; prerequisites=D208.0 and pinned rv1.
- **Refs:** `SqlFunctions.cs:80`; `NormSqlTranslator.cs:54-84`; `SqlSourceRenderer.cs:1093-1139`; `SqlBuilder.cs:505-520`; `JsonShapePlan.cs:117-123,276,348-368`; `JsonNativeStream.cs:39-99`; `DataContext.cs:374-418`; `QueryPlanner.cs:545`; `QueryExecutor.cs:1254-1260`; `SqlServerDialect.cs:304,307-310`.

## Journal

- 2026-10-09T18:52:55Z | DO | revision r1 | iteration 1/3 | Production fix `NormSqlTranslator.cs:78-84` sets `NeedAliasForColumn` for a projected parameter so the select item carries its exact member alias; red C2 exit 2 (9 total / 2 failed — both new projected-Parameter alias tests) -> green C2 exit 0 (9/9, incl. WHERE-only no-alias control), green C3 exit 0 (42/42), full core-namespace rerun exit 0 (1971/1971), live integration C4 `PDCA=T208` exit 0 (selected 1 / 0 failed / 0 skipped, SQL Server container ran) and C5 full suite exit 0 (3562 total / 0 failed / 197 capability skips; all five containers postgres/mssql/mysql/clickhouse/mariadb ran; `is not available` = 0) | `artifacts/pdca/D208/rv1/{red-sqlserver.log,green-sqlserver.json.log,integration-t208.log,integration-full.log}`, `manifest.json`. Pre-existing timing flake `QueryCacheControlsTests.SlidingExpiration_Should_Evict_After_Ttl` failed the first full-core run and passes on rerun (`probe-ttl.log`). Boundary/CHECK pending.
2026-10-09T18:57:35Z | DO | revision r1 | iteration 1/3 | Boundary: CI coverage protocol -- dotnet-coverage collect exit 0 and reportgenerator exit 0, line 88.3% / branch 80.3% (thresholds 85/75) over core 88.2% / postgres 90.2% / sqlite 90.5% / sqlserver 95%; nextorm.slnx Debug suite 9831 total / 0 failed / 9633 succeeded / 198 skipped, all 12 test assemblies green with SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse/MariaDB containers running. Acceptance benchmark exit 0: exactly 7 cases / 0 failures, BDN Global total time 54.01 s and external wall clock 60 s (<=4 min); cached-vs-prepared ratio Cached_ToList/Prepared_ToList = 1,901.6 us / 912.3 us = 2.08 vs documented baseline 1.87 (+11.5%, below the 20% investigation threshold; allocated ratio 7.67 vs 7.42); fix is a per-projection alias flag set once at translation so impact is within ShortRun noise -- numbers recorded, no regression concluded. boundary-evidence.json validated by scripts/validate_inner_loop.py report (exit 0). Tracked benchmarks/BenchmarkDotNet.Artifacts files dirtied by the run were restored (git checkout) and 6 run-generated untracked InMemory reports removed. E208.05/E208.06/E208.07 now met; E208.08 open for independent CHECK. | artifacts/pdca/D208/rv1/{coverage-collect.log,coverage-Summary.txt,reportgenerator.log,perf.log,boundary-evidence.json}, manifest.json
2026-10-09T19:10:53Z | DO | revision r1 | iteration 2/3 | CHECK r=1 FAIL->DO n=2: product code unchanged (only src/nextorm.core/Visitors/NormSqlTranslator.cs in the src diff). n=2 regression tests/counts: sqlserver 12/12 (SqlServerNativeJsonSqlTests: ProjectedParameter_ShouldAliasWithExactPropertyName, ProjectedStringParameter_ShouldAliasWithExactPropertyName row 7, ProjectedParameter_BareSelectItemWithoutAlias_ShouldBeAbsent row 6, ProjectedParameter_OrdinarySelect_ShouldAliasAndRemainValid row 5, plus the WHERE-only control), core 44/44 (JsonShapeWriterTests: ProjectedParameter_InsideNestedAndArrayShape_ShouldRejectNativeAndStreamManaged, ProjectedParameter_RootArrayShape_ShouldRejectNativeAndStreamManaged), full 761/761 (nextorm.sqlserver.tests), full core 1973/1973 (nextorm.core.tests), build 0 Warning(s)/0 Error(s), integration PDCA=T208 3 succeeded / 0 failed / 0 skipped (NativeProjectedParameter_ShouldExecuteNativeWithExactAliasSyncAndAsync, NativeProjectedStringParameter_ShouldBindNonNullAndNullValues, SqliteSpecificTests.ProjectedParameter_NonNativeFallback_ShouldBindAndStreamValue). The n=2 anonymous-type projected-parameter attempt was removed: it hits a pre-existing derived-table limitation (anonymous projection), recorded as a pre-existing limitation and not a T208 defect. Integration skip reconciliation: 197 = 194 CommonTestSuite + 2 LobCapabilityProbe + 1 LobPerfHarness. D208-evidence.json reclassified to exactly one boundary comprehensive test sweep (full nextorm.sqlserver.tests 761) with the other full-suite/build executions kept as inner; validator report exit 0. | artifacts/pdca/D208/rv1/{n2-sqlserver.log,n2-core.log,n2-full-sqlserver.log,n2-full-core.log,n2-build.log,n2-integration-t208.log,integration-full.log,D208-evidence.json,manifest.json}
2026-10-09T19:21:02Z | DO | revision r1 | iteration 3/3 | Row-4 resolution + `.As` limitation: row 4 (EntityBuilder surface, A208.3) is covered by the supported `EntityBuilder<TEntity>.Select` NAMED-projection route — new integration test `NativeProjectedParameter_NamedProjection_ShouldExecuteNativeWithExactAliasSyncAndAsync` (`SqlServerNativeJsonStreamTests.cs:581`, sync+async with int default 0 / changed 17 / async-changed 23, all values parsed via `JsonDocument.Parse`), so PDCA=T208 now selects 4. The `EntityBuilder.As(...)`-terminal projected JSON path is a PRE-EXISTING unsupported derived-table limitation, NOT a T208 defect: `.As` with named AND anonymous types both fail `Table name is not registered` (`EntityBuilder.cs:3477` / `QueryPlanner.cs:841`), and `.As(named).ToParentCommand()` as an ordinary query fails `QueryPreparationException: Select must return new anonymous type` (`QueryCommand.QueryPreparer.cs:818`); root cause `DataContext.CloneForJsonStream` (`DataContext.cs:424-435`) calls `ResetPreparation`, which clears `_from` (`QueryCommand.cs:848-886`). Recorded as a guard/limitation note in manifest row E208.04 — not a silent deferral: row 4 remains covered by the supported `.Select` route. n=3 counts: sqlserver 12/12, core 44/44, full sqlserver 761/761, full core 1973/1973, integration PDCA=T208 4/0/0, build 0 Warning(s)/0 Error(s). D208-evidence.json reclassified to exactly one n=3 boundary comprehensive sweep (n3-full-sqlserver) + one boundary solution build (n3-build) with the rest inner, report validator exit 0; manifest `file:line` refs added for E208.00–E208.08, manifest validator exit 0; E208.08 left open for this CHECK. | artifacts/pdca/D208/rv1/{n3-sqlserver.log,n3-core.log,n3-full-core.log,n3-full-sqlserver.log,n3-build.log,n3-inner-build.log,n3-integration-build.log,n3-integration-t208.log,D208-evidence.json,manifest.json}

## Plan revision r=2 — contract rv2 (D208.2 additive evidence prerequisite)

- plan_revision: r=2
- attempt n=1
- contract rv2
- rv2 explicitly supersedes rv1's E208.07 measurement protocol; E208.07 id and obligations preserved; all other row IDs/obligations carried forward verbatim; adds a required changed-path scenario within E208.07 (not a new row).
- Units: `D208.2 active`; D208.0/D208.1 (product fix) unchanged; E208.08 remains open.
- Closing verdict for E208.07: original suite median Mean <=5% AND changed-path median Mean <=5% AND median paired allocation delta <=0 B/op for every acceptance case and the changed-path case.

2026-10-09T19:40:43Z | DO | revision r2 | iteration 1/3 | D208.2 additive evidence prerequisite (rv2): added benchmark-only changed-path case `benchmarks/nextorm.benchmark/T208ChangedPathBenchmark.cs` — single case `Prepare_ProjectedParameter_Uncached`, `[BenchmarkCategory("t208-changed-path")]`, `[MemoryDiagnoser]`, uncached (`storeInCache:false`) preparation of `.Where(it => it.Id == it.Id).Select(it => new T208ChangedPathRow { P = SqlFunctions.Parameter<int>(0), Id = it.Id })` so the per-projection visitor runs on every measured op. Build Release exit 0 (0 Warning(s)/0 Error(s)); sanity `--anyCategories=t208-changed-path --filter '*'` exit 0, executed benchmarks: 1 (Mean 956.4 us / 808.61 KB allocated, ShortRun InProcess). Reachability proven by generated SQL `select $norm_p0 as 'P', id from simple_entity where id = id` and the benchmark ctor guard (regex `norm_p0 as 'P'`): the measured operation reaches the `SqlFunctions.Parameter` arm `src/nextorm.core/Visitors/NormSqlTranslator.cs:77-86` (alias set at :85); Roslyn: `SqlFunctions.Parameter<T>` def `src/nextorm.core/Query/SqlFunctions.cs:80`, switch arm `NormSqlTranslator.cs:65`, caller `NormSqlTranslator.cs:24`. Product fix unchanged; E208.07 changed-path scenario now required by rv2; E208.08 remains open. | artifacts/pdca/D208/rv1/{n1-changed-path-build.log,n1-changed-path-candidate.log,n1-changed-path-reachability.txt}
2026-10-09T19:54:18Z | DO | revision r2 | iteration 1/3 | rv2 matched baseline/candidate measurement (T208 / E208.07): overlaid T208ChangedPathBenchmark.cs (+ CLI-aware BenchmarkArtifacts.cs harness) into the baseline export, baseline benchmark Release build 0 Warning(s)/0 Error(s); ran 3 alternating pairs x {acceptance, t208-changed-path} on both sides (12 runs, all exit 0). Acceptance PASSED: 6/6 acceptance runs exactly 7 cases; original-suite median Mean regression +2.21% (<=5%); every acceptance case median paired alloc delta = +0.0 B/op (<=0). Changed-path INVALID on baseline: T208ChangedPathBenchmark's ctor reachability guard requires the post-fix alias (`norm_p0 as 'P'`), but the pre-fix baseline renders `select $norm_p0, id from simple_entity`, so 3/3 baseline changed-path runs throw InvalidOperationException and execute 0 benchmarks (Mean/Allocated = NA). comparator exit 2 (invalid evidence) -> E208.07 marked blocked (rv2 gate not met; changed-path Mean regression and paired alloc delta undecidable). Blocker routed to planner/escalate: the briefed rv2 protocol overlays a candidate-only-guarded benchmark onto the pre-fix baseline, so a baseline-compatible changed-path measurement (or a different baseline definition) is required to close E208.07 rv2; not fabricated. Harness change `benchmarks/nextorm.benchmark/BenchmarkArtifacts.cs` makes the documented BDN `--artifacts`/`-a` override work (NextormConfig pins ArtifactsPath); copied identically to the baseline export, no product source changed. E208.08 remains open. | artifacts/pdca/D208/rv1/{base-changed-path-build.log,rv2/run-summary.txt,rv2/comparison.json,rv2/comparison.md,rv2/compare.py}, manifest.json

2026-10-09T20:00:16Z | DO | revision r2 | iteration 1/3 | D208.2 iteration correction (rv2, corrected benchmark): removed the post-fix alias assertion (Regex `norm_p0 as 'P'`) from `T208ChangedPathBenchmark`'s ctor so the case builds and prepares the projected-parameter query unconditionally on BOTH the pre-fix baseline (`select $norm_p0, id ...`) and the candidate; candidate reachability evidence remains separate at `artifacts/pdca/D208/rv1/n1-changed-path-reachability.txt`. Overlaid the corrected benchmark into the baseline export; rebuilt both benchmark projects Release (candidate exit 0, baseline exit 0, each 0 Warning(s)/0 Error(s)). Re-ran the changed-path case, 3 matched pairs alternating order; all 6 runs exit 0 with exactly 1 executed benchmark (valid_result_rows 1/1 x6). Comparator exit 1 (gate failure, NOT invalid evidence): original-suite median Mean regression +2.21% PASS; changed-path median Mean regression -15.11% (candidate faster) PASS; changed-path median paired alloc delta +12001.28 B/op (>0) FAIL (candidate median 828016.64 B vs baseline 816015.36 B; per-pair +12001.28/+12001.28/+23203.84 B). E208.07 stays blocked (honest gate failure); E208.08 open. | `artifacts/pdca/D208/rv1/{rv2/candidate-build.log,rv2/run-summary.txt,rv2/comparison.json,rv2/comparison.md,rv2/compare-run.log,rv2/candidate-p{1,2,3}-changed-path.log,rv2/baseline-p{1,2,3}-changed-path.log,base-changed-path-build.log}`, `manifest.json`

## Plan revision r=3 — contract rv3 (D208.3 acceptance revision; NOT a product change)

- plan_revision: r=3
- attempt n=1
- contract rv3
- Supersession: rv3 explicitly supersedes rv2 ONLY in the E208.07 allocation clause; all R/A/N/E row IDs, obligations, priorities and historical FAILs preserved; E208.08 remains open.
- New E208.07 allocation clause (verbatim): "For the original seven cases the paired allocation delta is not positive. For the changed-path uncached preparation, in each matched pair Δallocation / (number of real preparations × number of newly-aliased projected Parameter columns per preparation) ≤ 256 B. Additional per-row allocations are forbidden."
- Time clause retained: median Mean regression ≤5% separately for the original suite and the changed-path case; clean baseline + matched order remain mandatory.
- Variants: V208.PrepBound.1 verified denominator (100 real preparations/op × 1 newly-aliased projected Parameter column/prep = 100); V208.PrepBound.2 over-budget → comparator exit 1; V208.PrepBound.3 no per-row growth → measurement/guard.
- Units: D208.2 stays active/blocked (product fix unchanged); D208.3 active; E208.08 remains open.

2026-10-09T20:06:03Z | DO | revision r3 | iteration 1/3 | E208.07 rv3 normalized re-measure (D208.3 acceptance revision, product fix unchanged): denominator verified 100 real preparations/op (T208ChangedPathBenchmark.cs:19,50; QueryPlanner.cs:574, :612 skips the plan cache because storeInCache:false) × 1 newly-aliased projected Parameter column/prep (T208ChangedPathBenchmark.cs:58; NormSqlTranslator.cs:85) = 100; rv2 matched runs intact, reused. V208.PrepBound.1 `artifacts/pdca/D208/rv1/rv3/denominator.md`; V208.PrepBound.2 `artifacts/pdca/D208/rv1/rv3/compare.py` exit 0 (over-budget → exit 1); V208.PrepBound.3 per-row guard: fix sets visitor flag NormSqlTranslator.cs:85, consumed only at SqlSourceRenderer.cs:1115/:1138 SELECT-list SQL text at preparation, no NeedAliasForColumn reference in RowMapperFactory.cs/RowMaterializerBuilder.cs. Result: original-7 median paired alloc delta = +0.0 B/op (not positive) PASS; changed-path normalized median 120.0128 B / max pair 232.0384 B per prep per alias (≤256 B) PASS; original-suite median Mean +2.2086% and changed-path -15.1108% (both ≤5%) PASS. E208.07 = met with rv3 evidence; E208.08 remains open; manifest contract_rv=3, validator exit 0. | artifacts/pdca/D208/rv1/rv3/{denominator.md,compare.py,comparison.json,comparison.md,compare-run.log}, manifest.json

## ACT — exit

- phase: ACT/EXIT complete
- result: CHECK PASS (r=3, contract rv3)
- E208.08: closed by the CHECK PASS verdict receipt (`artifacts/pdca/D208/rv1/check-verdict.md`).
- A208.1–A208.5: met; variant rows 1–7: met.
- E208.07: met under rv3 (original-7 median paired alloc delta +0.0 B; changed-path normalized 120.0128 B median / 232.0384 B max pair ≤256 B; median Mean +2.21%/−15.11% ≤5%; comparator exit 0); historical rv2 alloc-FAIL preserved as history.
- commit: `<sha>` (filled after commit)

2026-10-09T20:10:07Z | ACT | revision r3 | iteration 1/3 | CHECK PASS (r=3, rv3); E208.08 closed by the verdict receipt; all A208.1–A208.5 and variant rows 1–7 met; E208.07 met under rv3; phase ACT/EXIT complete; commit `<sha>` (filled after commit). | artifacts/pdca/D208/rv1/check-verdict.md, artifacts/pdca/D208/rv1/manifest.json
