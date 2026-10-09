# rc2-177-native-json-reclaim-1 — JSON streaming Phase 3: DB-side JSON fast-path (FOR JSON / json_agg / JSONEachRow)

- collection: rc2-reclaim (single lane; current worktree/branch 1.0.9-rc2)
- selected_variant: pdca-dotnet; cycle N=1; plan_revision r=1; evidence contract rv=1
- baseline: cf34f910; issue #177 (milestone 1.0.9-rc2 #20)
- supersedes: prior terminal attempt docs/specs/status/rc2-177-native-json-1.md (r=2/rv=2, CHECK completeness/variant FAIL; preserved unchanged); reference-only patch docs/specs/status/rc2-177-evidence/D177-INCOMPLETE.patch
- product: IN TREE at commit 2ac20818; reclaim = native-path test/evidence completion, NOT reimplementation
- plan_state: ready (persisted at PLAN->DO boundary; DO not started in P-phase)

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
AC2 count constraint: preserve the discovery total (6247) by adding subcases/assertions to existing discovered tests; if unsuitable, report the count impact and return to PLAN.

## DO task list (all fix now; sequential)
- D177-R1 persist gate-1 plan + new evidence dir; pin contract/assumptions/predecessor obligations.
- D177-R2 eligibility+SQL assertions: tests/nextorm.core.tests/JsonNativeStreamTests.cs:19; NextORM.SqlServer.Tests.SqlServerNativeJsonSqlTests (:12) -> close R7/R15; strengthen A1 native-selection + SQL assertions (locate methods semantically; no invented selectors).
- D177-R3 live native state/binding tests: NextORM.Integration.Tests.SqlServerNativeJsonStreamTests (:45) -> A1/A9/R16/A2-E177-14 via actual SQL Server, both surfaces, sync+async.
- D177-R4 regression+predecessor evidence: close E177-01..08, E177-13/14, A1..A11; scalar/managed/cache/temp-TVP/pre-cancel.
- D177-R5 mutation+performance evidence: close E177-09/10; six targeted mutants at semantically confirmed exercised sites, restore each; managed/native measurements + acceptance benchmark.
- D177-R6 docs+contract reconciliation: verify EN/RU (edit only a demonstrated discrepancy); close E177-11/12 + AC1-AC5.
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

## Evidence contract (rv=1)
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

## Risks / assumptions
- Existing test entries can host subcases without changing discovery totals (assumption; else report to PLAN, preserve AC2 until revised).
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

## Progress log
- PLAN persisted (r=1, rv=1) — collection P-phase boundary; plan_state=ready; DO not started.
