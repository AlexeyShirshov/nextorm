# rc2-177-native-json-reclaim-1 — JSON streaming Phase 3: DB-side JSON fast-path (FOR JSON / json_agg / JSONEachRow)

- collection: rc2-reclaim (single lane; current worktree/branch 1.0.9-rc2)
- selected_variant: pdca-dotnet; cycle N=1; plan_revision r=2; evidence contract rv=2; iteration n=1
- baseline: cf34f910; issue #177 (milestone 1.0.9-rc2 #20)
- supersedes: prior terminal attempt docs/specs/status/rc2-177-native-json-1.md (r=2/rv=2, CHECK completeness/variant FAIL; preserved unchanged); reference-only patch docs/specs/status/rc2-177-evidence/D177-INCOMPLETE.patch
- product: IN TREE at commit 2ac20818; reclaim = native-path test/evidence completion, NOT reimplementation
- plan_state: ACT complete (CHECK PASS r=2/rv=2/n=1; committed; issue #177 closed)

## Goal
Close exactly the six approved native-path gaps (A1 non-TableAlias/TableColumn MethodCallExpression native; A9 native-path repeated-call state isolation; R7 recursive Shape -> IsEligible==false; R15 pre-existing ForJsonClause guard; R16 native->managed->native on one context; A2/E177-14 native params parity + predecessor binding/pre-cancel), then demonstrate the in-tree product and predecessor guarantees remain intact. No current P1 product defect is established.

## Acceptance criteria (each with negative)
1. A1 gap: supported non-alias/non-column MethodCallExpression executes natively with logically equivalent UTF-8 JSON (neg: fallback-only equivalence must fail a native-selection assertion).
2. A9 gap: repeated native calls preserve preparation/parameters/call-local state (neg: stale params/subquery-state leakage/sticky cache-disabling).
3. R7: recursive Shape rejected by native eligibility (neg: eligible merely because projected CLR type looks eligible).
4. R15: existing ForJsonClause prevents another native rewrite (neg: duplicate wrapping/clause; scalar ForJson unchanged).
5. R16: native->managed->native on one context works (neg: middle call not actually managed / last not actually native / inherited state).
6. A2/E177-14 gap: native params overloads preserve binding+parity on both surfaces, sync+async; D178 pre-cancellation intact (neg: missing/reordered/stale args; pre-cancelled op must not execute or write output).
7. Existing A1-A11 guarantees remain satisfied (bounded pump, no managed row serialization, cancellation, ownership, cleanup, Unicode/empty, fallback, fail-closed) (neg: native tests that silently fall back/skip/assert only output equality do not close native rows).
8. Complete the pinned evidence contract incl. AC1-AC5, mutations, measurements, all-provider evidence, documentation verification (neg: missing reports/unavailable providers/surviving required mutants/unaccounted variants prevent PASS).
AC2 — Preserve existing discovery and reconcile required additions. Retain the baseline's existing discovered cases; do not delete, disable, skip, or consolidate them to offset additions. New discovered cases are permitted when they directly close one of the six R177 native-path gaps. Establish baseline B from the collection revision immediately preceding the R177 test changes, using the same discovery invocation, project set, and configuration as the final measurement. Record that revision, the exact invocation, evidence artifacts, and baseline count. The supplied current-baseline evidence reports B=6263; the historical 6247 total is not the acceptance target. For every R177 addition, record its requirement/gap, actual discovered test identity, and discovered-case contribution. Reconcile the final count against B and the complete R177 delta. Any unrelated collection changes must be identified separately, not absorbed into R177's delta. Acceptance requires complete reconciliation, preservation of the existing cases, and satisfaction of all six gap-closure criteria.

## DO task list (all fix now; sequential)
- P(r=2): replace AC2's absolute/count-preserving discovery constraint with baseline-relative, requirement-mapped discovery accounting; retain distinct required tests; preserve all behavioral acceptance criteria.
- D177-R1 persist gate-1 plan + new evidence dir; pin contract/assumptions/predecessor obligations.
- D177-R2 eligibility+SQL assertions: tests/nextorm.core.tests/JsonNativeStreamTests.cs:19; NextORM.SqlServer.Tests.SqlServerNativeJsonSqlTests (:12) -> close R7/R15; strengthen A1 native-selection + SQL assertions (locate methods semantically; no invented selectors).
- D177-R3 live native state/binding tests: NextORM.Integration.Tests.SqlServerNativeJsonStreamTests (:45) -> A1/A9/R16/A2-E177-14 via actual SQL Server, both surfaces, sync+async.
- D177-R4 regression+predecessor evidence: close E177-01..08, E177-13/14, A1..A11; scalar/managed/cache/temp-TVP/pre-cancel.
- D177-R5 mutation+performance evidence: close E177-09/10; six targeted mutants at semantically confirmed exercised sites, restore each; managed/native measurements + acceptance benchmark.
- D177-R6 docs+contract reconciliation: verify EN/RU (edit only a demonstrated discrepancy); close E177-11/12 + AC1-AC5.

### R177 observed variants (r=2)
- R177-V-A1-CORE -> tests/nextorm.core.tests/JsonNativeStreamTests.cs:250 SupportedMethodCallProjection_ShouldBeNativeWithEquivalentUtf8 (gap A1; +1 discovery)
- R177-V-A1-SQL -> tests/nextorm.sqlserver.tests/SqlServerNativeJsonSqlTests.cs:96 SupportedMethodCallProjection_ShouldBeNativeAndRenderForJson (gap A1; +1 discovery)
- R177-V-R15 -> tests/nextorm.sqlserver.tests/SqlServerNativeJsonSqlTests.cs:119 PreExistingForJsonClause_ShouldNotAttachSecondNativeRewrite (gap R15; +1 discovery)
- R7 in-place strengthening = 0 discovery: tests/nextorm.core.tests/JsonNativeStreamTests.cs:227 NestedShape_ShouldStayManaged (strengthened existing case, not a new discovered case).

Product files NOT permanently touched: src/nextorm.core/Query/Json/JsonNativeStream.cs (:20, :39, :105, :131/:185, :244); DataContext.PrepareJsonStream:374 (:393-409); QueryExecutor.WriteJson:1162 / WriteJsonAsync:1214 / native :1254/:1268; provider impls, public APIs, shared-command/cache, parameter binding, D176/D178/D180 impl. A reproduced product failure changes this only via CHECK->PLAN.

## Variant matrix (all test/guard now; none deferred except noted)
A1 managed/native logical equivalence+UTF-8 + supported non-alias/non-column method-call + explicit native-selection assertion; A2 both surfaces x sync/async x default/explicit x absent/present CT x native params; A3 zero native serializer count + bounded pump + 0/1/100/10000 rows + fragmentation/multi-chunk; A4 valid but ineligible -> managed; A5 unsupported fail-closed pre-output + recursive-shape/existing-clause guards; A6 pre-cancel + cancel during prep/exec/read/write; A7 caller-owned destination open, no Stream.Flush, resource release on success/cancel/fault; A8 empty + Unicode across row/char/byte boundaries + fragmented IO; A9 repeated calls/changed params/native->managed->native/cache-prepared-temp-TVP isolation/no sticky Cache=false; A10 scalar ForJson unchanged; A11 SQL Server live native + SQLite/PG/MySQL/MariaDB/ClickHouse managed/fail-closed + coverage/mutations/measurements/EN+RU. Missing rows: M177-A1, M177-A9, M177-R7, M177-R15, M177-R16, M177-A2-E177-14 (each distinct row). Provider scope: SQL Server native fast-path in-tree; PG json_agg / ClickHouse JSONEachRow new product impl DEFERRED to a separately approved provider scope (their existing managed/provider regression evidence NOT deferred). Value/reference, nullable/non-nullable, null/default/non-default, eligible/ineligible covered using existing public options discovered via Roslyn.

## Test strategy
Commands (exact): B `dotnet build --no-restore`; U `dotnet test --no-build --verbosity normal`; C core filter JsonNativeStreamTests|JsonStreamWriterTests|JsonShapeWriterTests; S sqlserver filter SqlServerNativeJsonSqlTests|JsonStreamingSqlLoweringTests; L sqlite JsonStreamingTests; P postgres / Y mysql / H clickhouse JsonStreamingSqlLoweringTests; I full integration with DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock -noColor; N integration -class NextORM.Integration.Tests.SqlServerNativeJsonStreamTests; M -class MariaDbJsonStreamTests; F -class SqlServerForJsonTests; V coverage collect; R reportgenerator; K `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; D `dotnet docfx docs/docfx.json`. Layers: unit eligibility; SQL-gen both surfaces/scalar; rendering/pump/fault; live execution N/M/F then I; provider regressions; coverage >=85/75 (baseline 86.8/79.4); mutations one per missing row (green -> mutant fails -> restore -> green); predecessors E177-13/14 mapped to current tests. Load integration skill before container runs; missing socket -> recovery procedure.

## Docs plan
Verify EN+RU guide/28-streaming-data.md, guide/14-json.md, advanced/api-reference.md (already in-tree). No planned prose rewrite; correct only a verified discrepancy in both languages; preserve scalar guarantees; no public->docs/specs links. Run D regardless.

## Performance decision
Required. Native pumping is per-row/per-chunk (JsonNativeStream.cs:131/:185; dispatch QueryExecutor:1254/:1268), so a one-time exemption is inapplicable. Extend an existing native integration test entry with measurement subcases (no discovery-count change): compare managed/native at 0/1/100/10000 rows (elapsed, app allocation/buffer, output bytes, native serializer count); managed local baseline; no invented threshold. Run K: exactly 7 cases, 0 fail, <=4 min; cached/prepared ratio vs 1.87.

## Reconnaissance decision
Required, already completed (D177-1 transport prototype). Record its actual source artifacts + why it still applies; no repeat unless a reproduced transport defect invalidates it.

## Unit execution mode
Sequential, one tree, one lane. Tests share context/state contracts and the product footprint; temporary mutants are exclusive and restored before other verification.

## Design-checklist verdict
Acceptable conditional on execution evidence: no public API/provider-capability/SQL-semantics/cache-policy redesign; existing native/managed/fail-closed boundaries retained; native-path assertions prevent green fallback-only tests; cancellation/ownership/cleanup explicit; predecessor guarantees active; CRLF/warnings-as-errors/coverage/live-providers/bilingual docs accounted.

## Evidence contract (rv=2)
Artifact root (planned): docs/specs/status/rc2-177-reclaim-evidence/; row artifact <root><row-id>.md; logs <root>logs/<alias>.log. All rows unconditionally applicable (no N/A); owner coder records, check validates. Exact invocations include A1-A4-style audit/mutation/proto/doc coder Task briefs (footprint/CRLF/predecessor audit; prototype provenance; six mutation checks; doc verification).
- E177-01 both surfaces native SQL + scalar -> C+S+F.
- E177-02 native selection/zero serialization/bounded pump -> C+N.
- E177-03 options/types/capabilities + R7/R15 guards -> C+S+L+P+Y+H.
- E177-04 CT/ownership/cleanup/faults -> C+N.
- E177-05 mixed/repeated/cache/temp-TVP/scalar -> C+U+N+F.
- E177-06 all-provider managed/live incl MariaDB -> L+P+S+Y+H+M+I.
- E177-07 build/warnings/footprint/CRLF -> B+AUDIT.
- E177-08 coverage -> V+R.
- E177-09 mutation sensitivity (six) -> MUT.
- E177-10 managed/native + acceptance perf -> N+K.
- E177-11 EN/RU + scalar -> DOC+D.
- E177-12 transport prototype provenance -> PROTO.
- E177-13 preserve D176/D178/D180 -> AUDIT+U+I.
- E177-14 D180 binding/D178 pre-cancel/native params -> C+S+N+U.
- A1..A11 rows mapped to the above commands; six reclaim rows M177-* with the same discipline; AC1 build 0W/0E; AC2 11 projects 6246+1skip/0fail; AC3 3555/0err/0fail/197skip + `grep -c "is not available"`=0 (grep exit 1 on zero); AC4 >=85/75; AC5 docfx 2w/0e.
Completeness repair: keep all original obligations; each missing variant its own stable row + negative assertion; record actual test identity/result/path assertion/source evidence (not class-level green alone); reconcile contract->ledger->artifact existence->applicability->result; collection §2/§3/§4/§4b/§5. CHECK re-gather budget: 2 owner-dispatch rounds per CHECK, owner check; missing evidence not a product defect; after 2 rounds open rows -> CHECK unresolved, never PASS.

### Evidence contract amendment (rv=2)
rv=2 supersedes rv=1's count-preservation wording ONLY (the AC2 constraint above): the absolute 6247-preservation requirement is replaced by baseline-relative, requirement-mapped discovery accounting. Every other rv=1 obligation survives unchanged: A1/A9/R7/R15/R16/A2-E177-14 rows (each its own stable row + negative assertion), all negative cases, and all build/integration/coverage/product-immutability obligations. The amendment relaxes no row, gate, or provider requirement.

## Risks / assumptions
- Distinct required scenarios need distinct tests (planner r=2): a host subcase cannot satisfy every R177 constraint without weakening a row, so new discovered cases are permitted when they map to a closed R177 gap; the final count must be reconciled against baseline B plus the complete R177 delta, with unrelated collection changes identified separately (else report to PLAN).
- Existing seams can observe native selection/serialization/bounded pump (assumption; else targeted scout, no output-equality substitution).
- Prior prototype evidence recoverable/applicable (assumption; else E177-12 open; repeat only on invalidating defect).
- MariaDB/required live providers executable under supplied setup (prerequisite/risk; follow integration-skill recovery; persistent inability = unresolved rows, not waiver).
- Current totals/docfx warning count remain as supplied (assumption; mismatch reported, not normalized).
- Saved patch read-only reference; never apply wholesale.

## Handoff metadata
- Write footprint: expected three native test classes (JsonNativeStreamTests, SqlServerNativeJsonSqlTests, SqlServerNativeJsonStreamTests) + status/evidence + EN/RU only for demonstrated corrections. No future symbols/source lines claimed.
- Predecessors: D176/D178/D180 unchanged; E177-13/14 carry preservation/regression evidence.
- Refs: issue https://github.com/AlexeyShirshov/nextorm/issues/177; milestone #20; baseline cf34f910; product 2ac20818; preserved prior status/evidence/patch; integration skill.

## Prior attempt (superseded, preserved)
rc2-177-native-json-1.md: r=2/rv=2; CHECK n=3 completeness/variant FAIL (no P1 product defect; decision (c) STOP); recommended scope = the six native-path tests above.

## Findings register
- F177-R3-01 (deferred product defect): projected `SqlFunctions.Parameter<T>` is admitted native-eligible but rendered without a required `FOR JSON` alias, so the native SQL Server route throws (`Column expressions and data sources without names or aliases cannot be formatted as JSON text using FOR JSON clause.`) while the managed (ordinal) path works. Tracked as issue #208 — https://github.com/AlexeyShirshov/nextorm/issues/208 (milestone 1.0.9-rc2). Preserved artifacts: findings/native-parameter-projection-defect.R3-failing.log (immutable failing run, 16/3) + findings/native-parameter-projection-defect.md (analysis, source refs, product baseline 2ac20818). OUT of the tests-only R177 footprint — no `src/**` change in this unit; recommendation = decide eligibility-reject vs alias-render with regression coverage.
- Evidence: docs/specs/status/rc2-177-reclaim-evidence/evidence.json → `findings[]` (id `R177-D177-R3-param-projection`), rows A9/R16/A2-E177-14 = candidate-closed (CHECK closes), A1 live evidence added, discovery 10→16.

## Progress log
- PLAN persisted (r=1, rv=1) — collection P-phase boundary; plan_state=ready; DO not started.
- 2026-10-09T13:51Z | DO->PLAN candidate | r=2 | n=1/3 | AC2 absolute-count 6247 stale vs tip 6263; planner (b) revised plan r=1->r=2, rv=1->rv=2, keep distinct tests; contract recorded; R2 behavior green (build 0/0; core JsonNativeStreamTests 52/52; sqlserver SqlServerNativeJsonSqlTests 7/7). | docs/specs/status/rc2-177-reclaim-evidence/
- 2026-10-09T14:06Z | DO R3 correction | r=2 | n=1/3 | TEMP_DumpNativeSql diagnostic removed (not counted); A9/A2-E177-14 re-scoped to the supported native shape (parameters via WHERE and the WriteJson params overloads); slash assertion corrected (`FOR JSON` escapes `/` as `\/`); build 0W/0E; integration SqlServerNativeJsonStreamTests 16/16 exit 0 (live SQL Server); discovery 10->16 (+6, existing 10 retained); discovered product defect (projected `SqlFunctions.Parameter<T>` unaliased under native FOR JSON) preserved and tracked as issue #208 (milestone 1.0.9-rc2). | docs/specs/status/rc2-177-reclaim-evidence/ (evidence.json, logs/sqlserver-nativejsonstream.log, findings/)
- 2026-10-09T14:14Z | DO->CHECK boundary (R4) | r=2 | n=1/3 | Boundary sweep all green: build 0W/0E exit 0; full core 1970/1970; full sqlserver 756/756; full integration (DOCKER_HOST, all providers) Total 3561 / 0 err / 0 fail / 197 skip, `grep -c "is not available"`=0; unit aggregate 11 projects total=6266 (6265+s1) rc_all=0; coverage line 86.8% / branch 79.4% (>=85/75); docfx 2 warnings / 0 errors exit 0. Discovery MEASURED = 6266, NOT 6272: R3(+6) is integration-only and outside the baseline-matching 11-project set (integration 3555->3561 confirms it); mismatch disclosed, not normalized. R4 rows: E177-13 (preserve D176/D178/D180), E177-14 (D180 binding / D178 pre-cancel / native params), scalar `.ForJson()` / managed / cache / temp-TVP — all cited and green in the boundary runs. `python3 scripts/validate_inner_loop.py report` exit 0. Scope clean: no `src/**`, no public `docs/**`/`docs/ru/**`, no `coverage.settings.xml`/`.github` change. | docs/specs/status/rc2-177-reclaim-evidence/ (evidence.json, logs/, audits/validate-report.out)
- 2026-10-09T19:28Z | DO R5 (mutation + performance evidence) | r=2 | n=1/3 | Six targeted mutants applied at semantically confirmed exercised sites, each restored then green: A1 caught (core `SupportedMethodCallProjection_ShouldBeNativeWithEquivalentUtf8` + sqlserver twin, exit 2), A9 caught (`Native_RepeatedCalls...`, exit 2), R15 caught (`PreExistingForJsonClause...`, exit 2), R16 caught (`NativeThenManagedThenNative...`, exit 2), A2 caught on the QueryCommand surface (exit 2) with the EntityBuilder single-param case NOT sensitive (exit 0, disclosed), R7 shape-guard mutant NOT caught because `Columns.Length==0` rejects the nested shape first (exit 0) but the supplementary `JsonShapePlan.Build` shape-descriptor mutant IS caught (exit 2). Acceptance benchmark 7/7 cases, 0 fail, wall 1:01.20 (<=4 min), cached/prepared ratio 1.98 vs baseline 1.87 (+5.9%, within noise), exit 0. Native measurement added as a sub-scenario of the existing `Native_UnicodeAndHtml...` entry (no discovery change): rows 0/1/100/10000 native 1.32/1.22/1.01/14.14 ms, 10824/10792/16672/1060936 alloc bytes, 2/24/2389/271493 output bytes, native serializer count 1; managed 4.44/3.53/4.58/22.38 ms, 57224/57312/75424/1371712 alloc bytes, 2/43/4190/451494 output bytes, native serializer count 0; no per-invocation counter seam, so the guard-bearing route flag is disclosed as the closest observable; class run 16/16 exit 0. `python3 scripts/validate_inner_loop.py report docs/specs/status/rc2-177-reclaim-evidence/evidence.json` exit 0. | docs/specs/status/rc2-177-reclaim-evidence/ (evidence.json mutations[]/performance{}, logs/native-measurement.log, audits/validate-report.out)
- 2026-10-09T14:32Z | DO R6 (docs + contract reconciliation) | r=2 | n=1/3 | Docs EN+RU verified against the in-tree FOR JSON fast-path / managed fallback / fail-closed (docs/guide/28-streaming-data.md + RU, docs/guide/14-json.md + RU, docs/advanced/api-reference.md + RU): no demonstrated discrepancy, no public-doc edit; no public->docs/specs link introduced. Prototype provenance (D177-1) recorded and still applies (transport product 2ac20818 unchanged, no src/** in this unit; the only reproduced defect is the alias/eligibility gap #208, not a chunk-transport defect); no repeat. Contract reconciled: E177-01..14, A1..A11, M177-* six gaps, AC1..AC5 added/held candidate-closed with current pointers; existing rows preserved, rv=2 unchanged. `python3 scripts/validate_inner_loop.py report docs/specs/status/rc2-177-reclaim-evidence/evidence.json` exit 0. DO complete, ready for CHECK; plan r=2/rv=2/n=1 unchanged. | docs/specs/status/rc2-177-reclaim-evidence/ (docs-verification.md, prototype-provenance.md, evidence.json, audits/validate-report.out)
- 2026-10-09T14:39Z | DO provenance correction | r=2 | n=1/3 | Pre-CHECK evidence provenance corrected: (a) stale line pointers in evidence.json rows[]/discovery.r3_additions[]/r177_additions[] and the status R177 observed variants updated to Roslyn-verified locations (R2: JsonNativeStreamTests.cs:250 SupportedMethodCallProjection_ShouldBeNativeWithEquivalentUtf8, SqlServerNativeJsonSqlTests.cs:96 SupportedMethodCallProjection_ShouldBeNativeAndRenderForJson, :119 PreExistingForJsonClause_ShouldNotAttachSecondNativeRewrite; R7: JsonNativeStreamTests.cs:227 NestedShape_ShouldStayManaged; R3: SqlServerNativeJsonStreamTests.cs:441 Native_MethodCallProjection_LiveSqlServer, :470 Native_RepeatedCalls_ChangedParams, :506 NativeThenManagedThenNative, :558 NativeParams_QueryCommandSurface, :605 NativeParams_EntityBuilderSurface, :659 NativeParams_PreCancelled); (b) literal exit codes captured: build-final.log:34 exit_code=0 (re-run `dotnet build nextorm.slnx -c Debug`), acceptance-benchmark.log exit_code=0 citing acceptance-benchmark.time.log "Exit status: 0", coverage-collect.log:22448 exit_code=0, coverage-report.log:10 exit_code=0; AC1/AC4 + E177-07/E177-08/E177-10 + performance.acceptance_benchmark/coverage cite these literal sources; (c) ClickHouse participated: all 5 Testcontainers started and Exited(0) - ff3bb6dd042f postgres, 4ab2d4542432 sqlserver, d703aedb9b21 mysql, 914f06a0ba5f mariadb, 525af9d6a314 clickhouse (clickhouse/clickhouse-server:25.8-alpine, reuse-id:nextorm-clickhouse); targeted `ClickHouseIntegrationTests` re-run Total 148 / Passed 148 / Skipped 0 exit_code=0 (logs/clickhouse-participation.log + .trx); AC3 provider set recorded. `python3 scripts/validate_inner_loop.py report` exit 0. (Timestamp is true UTC; host TZ is +05, so prior entries labelled local time as Z.) | docs/specs/status/rc2-177-reclaim-evidence/ (evidence.json, logs/build-final.log, logs/acceptance-benchmark.log, logs/coverage-collect.log, logs/coverage-report.log, logs/clickhouse-participation.log, logs/clickhouse-participation.trx, audits/validate-report.out)
- 2026-10-09T14:44Z | CHECK | r=2 | n=1/3 | PASS (r=2, rv=2, n=1): build 0W/0E exit 0; unit aggregate 6266 (=6263+3)/0 fail; integration 3561/0 err/0 fail/197 skip, `grep -c "is not available"`=0, 6 providers, ClickHouse 148/148; coverage line 86.8% / branch 79.4%; docfx 2 warnings / 0 errors; six mutations caught (A1 core+sqlserver, A9, R15, R16, A2 QueryCommand, R7 shape-descriptor) with the two disclosed gaps (A2 EntityBuilder single-param not sensitive; R7 eligibility guard masked by `Columns.Length==0`, supplementary `JsonShapePlan.Build` mutant caught); `python3 scripts/validate_inner_loop.py report` exit 0; deferred product defect tracked as issue #208 (OPEN). | docs/specs/status/rc2-177-reclaim-evidence/ (evidence.json, audits/validate-report.out, logs/, mutations[])
- 2026-10-09T14:44Z | ACT | r=2 | n=1/3 | ACT: committed; issue #177 closed; #208 left OPEN (separate defect). | docs/specs/status/rc2-177-reclaim-evidence/

## Priority matrix (completeness-repair addition, 2026-10-09; collection CHECK gap G4)

Derived at the collection CHECK from the recorded variant dispositions (`:41-42`) and the rv=2
amendment (`:81`); the source carried no explicit priority heading. No existing disposition is
changed.

- **P0 — unconditional correctness/gates:** A1 managed/native logical equivalence + explicit
  native-selection assertion; A4 valid-but-ineligible -> managed; A5 unsupported fail-closed
  pre-output; A6 cancellation; A7 ownership/cleanup; A9 mixed/repeated/cache/no sticky
  `Cache=false`; A10 scalar `ForJson` unchanged; A11 live providers + coverage/mutations/EN+RU;
  the AC1–AC5 rows (`:42,:78`).
- **P1 — required breadth:** A2 both surfaces x sync/async; A3 zero-serializer + row counts +
  fragmented pump; A8 empty/Unicode/fragmented IO; the six `M177-*` reclaim rows (`:42`).
- **P2 — deferred, trigger recorded:** PostgreSQL `json_agg` / ClickHouse `JSONEachRow` new product
  implementation (trigger: separately approved provider scope); the existing managed/provider
  regression evidence is NOT deferred (`:42,:81`).
