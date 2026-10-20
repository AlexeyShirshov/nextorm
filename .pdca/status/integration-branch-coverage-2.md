# integration-branch-coverage-2

Status: CLOSED
Current cycle: 2
Plan revision: r1
Attempt: n 1/3
Phase: ACT closed (EXIT)

## Goal

Raise the counted integration-suite branch coverage by at least **+250 branches** (baseline 25 136 / 38 281 = 65.6 %;
line 45 153 / 59 676 = 75.6 %) with tests that execute against real databases only. The 75 % gate is the task-level
goal, not this cycle's promise. Thresholds must not be lowered.

## Acceptance criteria

- AC1 - new tests use real DBs; full boundary sweep F reports 0 failed; skip multiset unchanged (37 reasons / 195).
  Negative: mock/in-memory, empty selection, or an extra provider skip = not accepted.
- AC2 - run the pinned F/T; report the counted branch delta, the denominator and per-file contributions.
  Negative: +249, raw XML instead of Summary, or an unexplained denominator change = not accepted.
- AC3 - sync the baseline in `AGENTS.md` + `.github/workflows/dotnet.yml`.
  Negative: changing thresholds/filters/denominator to get a result = not accepted.
- AC4 - every discovered product defect has a minimal repro and an issue/comment in milestone `1.0.9-rc4`.
  Negative: a duplicate, a lost defect, or a different milestone = not accepted.
- AC5 - coder finalizes `.pdca/status/integration-branch-coverage-2.md`; pre-existing working-tree changes
  preserved; no commit/push.
- AC6 - brief/journal/manifest/generate/lint rc 0; project/report may be rc 2 ONLY with the accepted reason
  `FAIL: multiple boundary solution builds: N`.

## Baseline (measured, carried from cycle 1's green boundary)

Source of truth: `tests/coverage/integration-report/Summary.txt` (reportgenerator over
`tests/coverage/integration.cobertura.xml`).

- branch: 25 136 / 38 281 = 65.6 %
- line: 45 153 / 59 676 = 75.6 %
- full suite: 0 failed, 195 skipped / 37 reasons

## Tasks (round r1)

| id | file / class | facts | forecast | notes |
| --- | --- | --- | --- | --- |
| D1 | `tests/nextorm.integration.tests/Sqlite/RawShapeBranchCoverage2Tests.cs` | 16 | +60..100 | `RawShape` :3625-3888, `RawShape.Matches` :3702-3763, `CaptureRawShape` :3889, `CtesEqual` :3766, `FilterScopeEqual` :3839 |
| D2 | `tests/nextorm.integration.tests/Sqlite/PlanEqualityBranchCoverage2Tests.cs` | 18 | +65..105 | `Query/QueryPlanEqualityComparer.cs` + `Query/ExpressionPlanEqualityComparer.cs` via `QueryCommand.Plan.cs:72,75` |
| D3 | `tests/nextorm.integration.tests/Sqlite/CorrelatedBranchCoverage2Tests.cs` | 16 | +65..105 | correlated null/non-null, outer nesting, navigation chain |
| D4 | `tests/nextorm.integration.tests/Sqlite/EagerModeBranchCoverage2Tests.cs` | 12 | +25..45 | eager Default/SplitQuery/SingleQuery/Auto; `Correlated` + forbidden shapes = guard with live counterpart |
| D5 | `tests/nextorm.integration.tests/Sqlite/SourceRenderingBranchCoverage2Tests.cs` | 14 | +30..55 | raw/derived/builder sources, null/default, empty/non-empty IN |
| D6 | `tests/nextorm.integration.tests/Sqlite/InPreparationBranchCoverage2Tests.cs` | 12 | +35..65 | IN/PreWhere preparation shapes |
| D7 | `AGENTS.md` + `.github/workflows/dotnet.yml` | - | - | baseline sync, numbers only |
| D8 | defect register + filing | - | - | search first, then create/comment in milestone `1.0.9-rc4` |
| D9 | evidence collection + status finalization | - | - | manifest/generate/lint, status close |

Portfolio: 88 facts, forecast 280..475, midpoint 377.5, all SQLite.

Deferred with trigger: new container-only `ClickHouseScalarFunctions`/`ClickHouseDialect`/`SqlServerSpecificFunctions`
classes - trigger = a measured shortfall against +250 after the reachable SQLite variants are exhausted (a new
PLAN r2 is required). `PreparePreWhere` has no public setter - deferred, trigger = a proven public `JoinInto`
rewrite path. APPLY/temporal/tablesample/provider-only hints: only the actually reachable SQLite rejects with live
counterparts (guard); successful provider variants stay with the existing F.

## Variant matrix (all closed)

- SQLite D1..D6 = test
- other providers = covered by the existing full F, no new container-only classes
- new ClickHouse/SqlServer function classes = deferred + trigger
- sync/async terminals = test where both public forms exist, else a guard fact for the missing API
- raw/derived/builder sources, null/default, empty/non-empty IN = test (D1/D5/D6)
- equal/unequal constant/member/unary shapes = test (D2)
- correlated null/non-null, outer nesting, navigation chain = test (D3)
- eager Default/SplitQuery/SingleQuery/Auto = test (D4)
- eager `Correlated` + forbidden child shapes/JoinInto combinations = guard with a live counterpart (D4)
- `PreparePreWhere` without a public setter = deferred + trigger
- APPLY/temporal/tablesample/provider hints = guard (reachable SQLite rejects) + existing F

## Test strategy

Inner loop = `B` per cluster then `I(C)`, unlimited; `G` mandatory; boundary = exactly ONE `F` at DO->CHECK;
assertions FluentAssertions only; CRLF; every live probe scoped to class-owned markers (never a global row count);
each guard paired with a live counterpart; unreachability proven by public-path analysis + the guard's own branch
+ a numeric list of unreachable baseline points, never by a synthetic tree; before implementing each fact, match
it to an uncovered line and to existing tests to avoid repeating a cycle-1 sweep under a new name.

## Docs plan

Only D7 (the two baseline numbers). `docs/**` and `docs/ru/**` are not touched (no public API/behaviour change;
no product fix in scope).

## Perf decision

No benchmark - test-only change, the production per-row/query path is untouched
(`QueryCommand.QueryPreparer.cs:195-280`, `SqlSourceRenderer.cs:424-456` unchanged).

## Recon decision

None needed - harness and targets already measured by the two gather streams; Roslyn is used in DO only for
symbol/public-reachability checks.

## Unit mode

One tree, sequential (shared SQLite file, fixtures, coverage artifact and evidence contract); no worktrees;
all 83 pre-existing working-tree entries preserved.

## Design checklist

KISS; no abstraction without a second consumer; measure-never-assert; public API purity; Roslyn for C# symbols and
ignore-aware search for text (never `grep -r`/`find`); build discipline (`TreatWarningsAsErrors`, CRLF, CPM);
nothing moves outside milestone `1.0.9-rc4`; PLAN owns plan quality; an unprovable criterion is a GAP, not `ready`.

## Risks

- container preflight for F (live socket, `NEXTORM_SKIP_PROVIDERS` unset, all five container providers + in-process
  SQLite; provider skips are never green)
- a red first F -> CHECK returns DO at n=2 for implementation fixes, then one new F on the next DO->CHECK
  transition, the original result retained
- if +250 is not reached: rank the shortfall per file numerically and fix the uncovered variants inside D1..D6;
  a real change of tasks/dependencies needs a new `P:` (r2/n1) with explicit contract supersession
- unreachable branches are never counted; if the floor is unreachable that is a GAP, not a successful close
- a product defect goes to D8 while the original D stays active (not done/superseded)
- persistent low confidence after a targeted scout -> recommend `escalate` (trigger 5)

## Pinned commands

- B (build, phase `boundary`): `dotnet build nextorm.slnx -c Debug`
- G (guard, kind `unit`): `dotnet test tests/nextorm.core.tests --filter FullyQualifiedName~IntegrationSuiteScopingTests`
- I(C) (inner, kind `other`):
  `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class <full class name> -noColor`
- F (boundary sweep, exactly ONE at DO->CHECK):
  `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o tests/coverage/integration.cobertura.xml "dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -notrait Category=Diagnostic"`
- T (coverage delta): reportgenerator over `tests/coverage/integration.cobertura.xml` into
  `tests/coverage/integration-report` (`Html;TextSummary`)

## Evidence contract (rows)

| row | subject | evidence |
| --- | --- | --- |
| R1 | AC1-B build | `results/d1-build.log`, `results/d2-build.log`, `results/d3-build.log`, `results/d4-build.log` (rc 0, 0 warnings/errors) |
| R2 | AC1-G guard | `results/g1.log` (rc 0, selected 2) |
| V1 | D1 cluster inner run | `results/d1-inner.log` |
| V2 | D2 cluster inner run | `results/d2-inner.log` |
| V3 | D3 cluster inner run | `results/d3-inner.log` (rc 0, selected 16, failed 0, skipped 0) |
| V4 | D4 cluster inner run | `results/d4-inner.log` (rc 0, selected 12, failed 0, skipped 0) |
| V5 | D5 cluster inner run | `results/d5-inner.log` |
| V6 | D6 cluster inner run | `results/d6-inner.log` |
| R3 | AC1-F boundary sweep | `results/boundary.log` |
| R4 | AC2-T coverage delta >= +250 | `results/coverage-delta.md` + `tests/coverage/integration-report/Summary.txt` |
| R5 | AC1-S skip budget | `results/skip-budget.log` + `inputs/baseline-skips.json` |
| R6 | AC3 baselines | `results/agents-baseline.diff`, `results/workflow-baseline.diff` |
| R7 | AC4 issue search | `results/issue-search.log` |
| R8 | AC4 create/comment | `results/issue-filing.log` + `inputs/defects.json` |
| R9 | AC5 status/tree preserved, no commit | this status file + `results/tree-preserved-final.txt` |
| E1 | brief validator | `results/validator-brief.txt` |
| E2 | journal validator | `results/validator-journal.txt` |
| E3 | project+report validators | `results/validator-project.txt`, `results/validator-report.txt` |
| E4 | generate+manifest+lint | `results/generate.log`, `results/lint.log` |

B journalled phase `boundary`; I(C) kind `other`; G kind `unit`. Accepted rc 2
(`multiple boundary solution builds: N`) is recorded as a Notice, never hidden or fabricated away.
CHECK re-gather: owner `check`, at most 2 targeted requests per attempt, no new F/B, no tree change.

## Durable state

- Current cycle: 2
- Plan revision: r1
- Attempt: n 1/3
- Integration boundary runs planned: 1
- Integration boundary runs executed: 1 (F, rv1, green: Total 4274, Failed 0, Skipped 195, exit 0)

## Defect history

| defect key | revisions / attempts observed | fixes applied | evidence | last recurrence / outcome |
| --- | --- | --- | --- | --- |
| `type-binary-not-translated` (`is <type>` predicate is dropped, the bare operand becomes the SQL condition) | r1 / n 1/3 (discovered in D2) | 0 | `rv1/results/c2-repro-typebinary.md`, `rv1/results/c2-repro-typebinary.log` (exit 1: observed values), `rv1/results/c2-repro-typebinary-observed.log` (exit 0: characterization) | open - filing belongs to D8 (milestone `1.0.9-rc4`); repro `_sut.ComplexEntity.Where(x => x.String is string).Select(x => x.Id)` prepares `where somestring` and returns `[]` instead of `[1,2]` |
| `null-equality-not-is-null` (a nullable value-type column compared with `== null`/`!= null` renders `col = null`/`col != null` instead of `is null`/`is not null`, and a *captured* null renders `col = $param`, so all three silently return zero rows) | r1 / n 1/3 (discovered in D3) | 0 | `rv1/results/c2-repro-nullequality.md`, `rv1/results/c2-repro-nullequality.log` (exit 1: observed rows + generated SQL) | open - filing belongs to D8 (milestone `1.0.9-rc4`); repro `_sut.ComplexEntity.Where(x => x.Int == (int?)null).Select(x => x.Id)` prepares `where nullableint = null` and returns `[]` instead of `[1]`; `!x.Int.HasValue` throws `BuildSqlCommandException: Cannot resolve column for member HasValue` |

## DO results (round r1, attempt n 1/3 - D1 + D2 dispatch)

- D1 `tests/nextorm.integration.tests/Sqlite/RawShapeBranchCoverage2Tests.cs`,
  class `NextORM.Integration.Tests.RawShapeBranchCoverage2Tests`, **16 `[Fact]`**: inner run selected 16,
  `Failed: 0`, `Skipped: 0`; whole-solution build `0 Warning(s) 0 Error(s)`.
  Differential arms driven with live rows: self/null short circuit, entity/result/projection types, column
  list (same arity), closure ineligibility + rebinding, literal vs closure eligibility, paging, DISTINCT +
  hints, sorting, joins (inner/left/absent), tag + tables-in-scope hint, grouping/HAVING, grouping type
  (CUBE), CTE list (same body vs changed body), CTE name pairing, union (ALL vs DISTINCT vs absent), source
  identity (raw SQL / derived command / bound vs unbound name source), and the purge -> republish cycle.
- D2 `tests/nextorm.integration.tests/Sqlite/PlanEqualityBranchCoverage2Tests.cs`,
  class `NextORM.Integration.Tests.PlanEqualityBranchCoverage2Tests`, **18 `[Fact]`**: inner run selected 18,
  `Failed: 0`, `Skipped: 0`; whole-solution build `0 Warning(s) 0 Error(s)`.
  Branches driven both ways: `Compare2` type switch (Binary/Conditional/Constant/Default/Lambda/Member/
  MemberInit/MethodCall/NewArray/New/Parameter/TypeBinary/Unary), `CompareConstant` (value + type guard),
  `CompareMember` (constant-receiver fast path and the MemberInfo path), `CompareUnary` (convert and NOT),
  `CompareTypeBinary`, `VisitConstant`, `VisitMember` (both the constant-receiver and the captured
  `QueryCommand` branch, cache miss then hit), plus two whole-plan-key facts through the cached path.
- Gate `G` re-run after the last edit: rc 0, selected 2, failed 0 (`rv1/results/g2.log`).
- Boundary sweep `F` is **not** run in this dispatch (exactly one F at DO->CHECK, per the plan).
- Reachability findings recorded in the D1 file: a `Where` placed *before* a `Join` lowers the left source
  to a derived source and makes the whole graph ineligible (`CaptureRawShape` returns null); `GROUP BY CUBE`
  is capturable but not SQLite-renderable; the `Settings`/`GroupingSets`/`SampleRatio`/`IndexHints`/
  `TableHints`/`ForJson`/`ForXml`/`TableSample`/`Temporal`/`LimitBy`/`DistinctOn` and the internal
  per-parent/document/final flags have no public entry point, so their `Matches` arms are recorded as
  unreachable (with the member list) instead of fabricated.

### Notice - accepted validator rc 2 (recorded, not hidden)

`validate_inner_loop.py project rv1/journal.json` exits **2** with exactly one failure:
`FAIL: multiple boundary solution builds: 2` (two whole-solution builds at phase `boundary`, one per
cluster, as the dispatch requires). This is the accepted reason of the plan
(`project/report may be rc 2 ONLY with the accepted reason 'multiple boundary solution builds: N'`).

### Notice - evidence generation regenerated inside DO

The pre-DO snapshot of `manifest.json`/`generations/` was computed before the D1/D2 files existed, so
`pdca_evidence.py generate` refused to run over it (`preflight: existing evidence is stale`). The stale
generation was removed and regenerated at the DO close; `generate` and `lint` are both rc 0 afterwards
(`rv1/results/generate.log`, `rv1/results/lint.log`).

### Command history of this dispatch (evidence-bearing commands are journalled)

The raw journal carries the evidence-bearing executions (gate G, the two boundary builds, the two cluster
inner runs, and the two repro-probe runs) with their logs. The remaining commands below are real
iterations whose output was not retained (scratch probes deleted, compile-only builds overwritten) and are
recorded here for completeness:

- `dotnet build tests/nextorm.integration.tests -c Debug` x 9 (compile iterations while writing/patching
  D1 and D2; 7 succeeded, 1 failed on `Should().Equal(..., reason)` argument binding, 1 failed on a
  forbidden pattern-matching operator in an expression tree) - final state rc 0.
- `dotnet run ... -class NextORM.Integration.Tests.RawShapeBranchCoverage2Tests -noColor` x 4 (rc 1, rc 1,
  rc 1, rc 0; the red runs are the D1 facts fixed above).
- `dotnet run ... -class NextORM.Integration.Tests.PlanEqualityBranchCoverage2Tests -noColor` x 2 (rc 1, rc 0).
- `dotnet run ... -class NextORM.Integration.Tests.ScratchRawShapeProbe -noColor` x 1 (rc 1, diagnostic
  probe that established the pre-join-`Where` ineligibility; file deleted).
- `dotnet run ... -class NextORM.Integration.Tests.ScratchPlanProbe -noColor` x 3 (rc 1 diagnostic, rc 1
  repro, rc 0 characterization; file deleted - see the Notice above).
- `python3 .pdca/scripts/validate_inner_loop.py brief|journal|project ...` and
  `python3 .pdca/scripts/pdca_evidence.py generate|lint ...` (all rc recorded in `rv1/results/`).

## DO results (round r1, attempt n 1/3 - D3 + D4 dispatch)

- D3 `tests/nextorm.integration.tests/Sqlite/CorrelatedBranchCoverage2Tests.cs`,
  class `NextORM.Integration.Tests.CorrelatedBranchCoverage2Tests`, **16 `[Fact]`**: whole-solution build
  `0 Warning(s) 0 Error(s)` (`rv1/results/d3-build.log`); inner run selected 16, `Failed: 0`, `Skipped: 0`
  (`rv1/results/d3-inner.log`). Shapes driven with live rows: a correlated scalar subquery as the left
  operand of an equality, `exists` combined with `||`, the collection terminals
  (`Any`/`Count`/`Count` property/`LongCount`) in predicates and projections over a nullable foreign key,
  a duplicate-child-name `Count` (no dedup), the navigation-count narrowing probe with a sibling `int`
  conversion, a scalar subquery inside `&&`, a subquery over a nested outer member, a captured value and a
  null literal inside the inner predicate, the debug-logger cache-miss path, and the member-init and
  whole-scalar projection forms.
- D4 `tests/nextorm.integration.tests/Sqlite/EagerModeBranchCoverage2Tests.cs`,
  class `NextORM.Integration.Tests.EagerModeBranchCoverage2Tests`, **12 `[Fact]`**: whole-solution build
  `0 Warning(s) 0 Error(s)` (`rv1/results/d4-build.log`); inner run selected 12, `Failed: 0`, `Skipped: 0`
  (`rv1/results/d4-inner.log`). Shapes driven with class-owned marker rows: Default/SplitQuery/SingleQuery/
  Auto resolution with the execution-count proof per mode, per-parent top-N on both paths, the two-level
  nested graph stitched on both paths, and the rejected `Correlated` mode, non-list terminal, per-parent
  paging without ordering / with `WITH TIES`, the single-query fold rejections (`Distinct`, nested paged,
  nested `Distinct`), mixed nested modes, the cyclic graph, and the `EagerLoadShape` plan-key separation -
  each rejection paired with a live counterpart of the same builder shape.
- Gate `G` re-run after the last D4 edit: rc 0, selected 2, failed 0 (`rv1/results/d3d4-guard.log`).
- Boundary sweep `F` is **not** run in this dispatch (exactly one F at DO->CHECK, per the plan).

- **D3: done** and **D4: done** - both closed green (whole-solution build rc 0 with `0 Warning(s) 0 Error(s)`,
  inner run rc 0 with `selected == fact count` and `Failed: 0`/`Skipped: 0`, gate `G` rc 0). D5..D9, the
  boundary sweep `F` and the coverage run `T` stay **open**; nothing here is superseded and no `D:`-unit
  was marked done without its gate.

### D3/D4 overlap avoided (read, deliberately not repeated)

- `Sqlite/CorrelatedSubquerySweepTests.cs` owns the correlated **terminal matrix**
  (`Count`/`LongCount`/`Sum`/`Min`/`Max`/`Avg`/`Any`/`exists`/`First`/`FirstOrDefault` in a projection,
  `exists`/`any`/`all`/nested `exists` predicates in `Where`, correlated ordering/grouping, and the
  variance terminals `Stdev`/`Stdevp`/`Var`/`Varp`), and the shared `CommonTestSuite.CorrelatedQuery`
  owns the main correlated shapes; D3 therefore drives the **collection-navigation** correlated shapes
  (nullable foreign key, presence/count per parent, the navigation-count narrowing probe) plus the
  projection/member-init/subquery-in-binary/logger forms instead.
- `SqliteBuilderGuardCoverageTests.cs` owns the eager/JoinInto **builder-level** rejections (duplicate
  collection member, undefined `EagerLoadMode`, conflicting explicit modes, `JoinInto` unsupported kind /
  key-type mismatch / combined with `Select` / without a declared relationship, `LoadWith` combined with
  `Select`/`Join`) and `SqlitePreparationShapeCoverageTests.cs` owns the dynamic-store `JoinInto`
  rejection; D4 therefore drives the **mode/paging/graph** matrix and the plan-time rejections
  (`Correlated`, non-list terminal, per-parent paging shapes, the single-query fold, mixed modes, cyclic).

### D3/D4 unreachable targets (recorded, never fabricated)

- D3 (from its own header): the reference-scalar lowering `TryLowerReferenceScalar` :993,
  `LowerMultiHopReference` :1070, `BuildNavigationChainCommand` :1082, `ResolveHopOuterKey` :1138 /
  `ResolveHopMatchKey` :1144, `BuildReferenceExists` :1155, `BuildReferenceCorrelation` :1192,
  `IsReferenceScalarValueAccess` :1257, `VisitAlignedReferenceComparison` :1266,
  `TryGetReferenceNullComparison` :1286 (the null-constant binary :1290-1295), `TryResolveReferenceAccess`
  :1307, the in-memory collection rejection :463-470, `BuildReferencePrefixKey` :796, the multi-hop
  collection prefix branch :736-743, the non-prepare `Any<TResult>` :1808 and the non-prepare exists
  branch :183-202 - all reachable only through `InMemoryDataContext`, which the integration project must
  not use.
- D4 (from its own header): `EagerLoadShape.StructuralHash`/`GetHashCode`
  (`Query/QueryCommand.EagerLoading.cs`:130-146,170) and `Equals(object)` (:167) - the type is `internal`,
  the plan comparer uses the typed `Equals` and `AddTo` for hashing, and the only other non-empty producer,
  `EagerLoadGraph.ToShape()` (`Builders/EntityBuilderEagerLoading.cs`:1359), has no production caller;
  `EagerLoadGraph.Find`/`MaxDepth`/`IsEmpty`/`Empty` (:1300-1351); the
  `JoinIntoProjectionFactory.MaxChildCount` arity rejection (:395-399, requires >7 distinct child
  collections in one graph); and the in-memory-only rejections (`EagerLoadSpec.BuildChildCommand`:681,
  `ToJoinIntoSpec`:571) which stay with the unit suites.

### Notice - defect candidate C2-D8-2 (`null-equality-not-is-null`) found in the D3 dispatch

While writing D3 the coder found that a nullable value-type column compared with `== null`/`!= null`
renders `col = null`/`col != null` instead of `is null`/`is not null`, and a **captured** null renders
`col = $param`, so all three silently return zero rows. The probe (`ScratchCorrNullProbe`) was run once
with rc **1** (it throws with the observed rows and the generated SQL), the observed matrix was frozen in
`rv1/results/c2-repro-nullequality.md`, and the probe file was then deleted; no separate green
characterization run exists (the characterization *is* the recorded matrix in the `.md`). Filing belongs
to D8 (milestone `1.0.9-rc4`); the D3 class keeps only the correct shapes. Log:
`rv1/results/c2-repro-nullequality.log`.

## DO results (round r1, attempt n 1/3 - D5 + D6 dispatch)

- D5 `tests/nextorm.integration.tests/Sqlite/SourceRenderingBranchCoverage2Tests.cs`,
  class `NextORM.Integration.Tests.SourceRenderingBranchCoverage2Tests`, **14 `[Fact]`**: whole-solution
  build `0 Warning(s) 0 Error(s)` (`rv1/results/d5-build.log`); inner run selected 14, `Failed: 0`,
  `Skipped: 0` (`rv1/results/d5-inner.log`). Shapes driven with live rows: one CTE read twice through two
  aliases, a chained CTE declaration (several entries in one `with`), a paged/filtered CTE body rendered
  in isolation, the SQLite database qualifier, the live `WithTableExpression` derived source, `FromSql`
  with named parameters and a joined raw source, the subquery-hint and table-hint gates each paired with
  a live counterpart, the SQLite index-hint rendering (`NOT INDEXED` live plus the multi-name
  `INDEXED BY` rejection), the `CROSS APPLY` dialect gate paired with a live inner join, a
  CTE-primary-source join, a single predicate and a nested AND/OR group, and a composite-key sort with a
  qualified renamed column.
- D6 `tests/nextorm.integration.tests/Sqlite/InPreparationBranchCoverage2Tests.cs`,
  class `NextORM.Integration.Tests.InPreparationBranchCoverage2Tests`, **12 `[Fact]`**: whole-solution
  build `0 Warning(s) 0 Error(s)` (`rv1/results/d6-build.log`); inner run selected 12, `Failed: 0`,
  `Skipped: 0` (`rv1/results/d6-inner.log`). Shapes driven with live rows: a captured single-element
  array, a many-element `List`, an empty collection (folded to always-false) paired with its non-empty
  counterpart, a null cell against a non-nullable and against a nullable column (the `or ... is null`
  arm), a membership test in a projection and in a join condition (unkeyed clauses), the
  captured-variable-versus-inline-literal plan separation, the null flip inside a captured nullable list
  while the plan stays cached, the shape-stable inline list reusing the plan, a command first prepared
  with `storeInCache:false` then reused through the cache (the `!ShapeScanned` fold), and the
  nullable-tuple-element rejection paired with a live flat tuple list.
- Gate `G` re-run after the last D6 edit: rc 0, selected 2, failed 0 (`rv1/results/d5d6-guard.log`).
- One transient red inner run: the first D6 run failed 3 facts with `SQLite Error 1: 'incomplete input'`
  because three facts ordered by the whole entity (`OrderBy(x => x)`) before the scalar `Select`; the
  three call sites were changed to `OrderBy(e => e.Id)`, the integration project was rebuilt and the
  cluster then ran green (rc 0, selected 12, failed 0) - the retained `d6-inner.log` is that green run.
- Boundary sweep `F` is **not** run in this dispatch (exactly one F at DO->CHECK, per the plan).

- **D5: done** and **D6: done** - both closed green (whole-solution build rc 0 with `0 Warning(s) 0 Error(s)`,
  inner run rc 0 with `selected == fact count` and `Failed: 0`/`Skipped: 0`, gate `G` rc 0). D7, D8, D9, the
  boundary sweep `F` and the coverage run `T` stay **open** for the final dispatch; no `D:`-unit was marked
  done without its gate and no candidate was accepted on a red run (the transient red D6 run above was
  fixed and re-run green before D6 was closed).

### D5 overlap avoided (read, deliberately not repeated)

- `Sqlite/RawSourceBindingGuardTests.cs` owns `From(string)`+`BindEntity`, the `FromSql` binding and the
  composition guard; `Sqlite/SqlBuilderGuardTests.cs` owns tags and the extreme-row rejections;
  `Sqlite/BuilderValidationSweepTests.cs` owns TABLESAMPLE, `WITH TIES` and the statement overrides;
  `Sqlite/SingleFeatureBuilderTests.cs` owns the single-CTE read, the set operations and the three basic
  join kinds; `Sqlite/CoreTerminalSweepTests.cs` owns the terminals; D1's
  `Sqlite/RawShapeBranchCoverage2Tests.cs` owns the raw-shape differentials (raw SQL and derived sources
  as *shape* ineligibility, not as rendering); the shared `CommonTestSuite.Join` owns
  INNER/LEFT/RIGHT/FULL/CROSS with plain conditions and the derived-source read;
  `SqlitePreparationShapeCoverageTests.cs` owns derived-source projections. D5 therefore drives the
  disjoint shapes listed above (CTE read twice / chained declaration / CTE-primary-source join, the
  database qualifier, the live table expression, `FromSql` with parameters and joined, the two hint gates,
  the index-hint rendering, the APPLY gate, the nested AND/OR group and the renamed qualified column).

### D5 unreachable targets (recorded, never fabricated)

- `MakeJoinParts` :293-327 is called only by the multi-table `DELETE ... USING` renderer;
  `SqliteDialect` leaves `SupportsDeleteJoin` at its `false` default (`DeleteJoinRequiresUsing` stays
  `true`), so the method is dead on SQLite.
- `MakeFrom` :472-473 (tables-in-scope hints) is unreachable: `SqlBuilder.cs:136-139` rejects them first
  because `SqliteDialect.SupportsTablesInScopeHints` and `SupportsInlineHints` are both `false`, so
  `scopeHints` is always `null` by the time `MakeFrom` runs.
- `MakeFrom` :440-441 (temporal on a non-table source) is unreachable: `SqlBuilder` rejects
  `FOR SYSTEM_TIME` first (`SqliteDialect.SupportsTemporalTable` is `false`).
- `MakeFrom` :446-447 (pivot) and :449-450 (`xml.nodes()`) are unreachable: `SqliteDialect.Pivot` and
  `SqliteDialect.XmlFunctions` are both `null` (the base defaults) and no public builder entry point
  constructs those sources.
- `MakeFrom` :437 (SelectMany/GroupJoin) is in-memory only; `MakeRawSqlSource` :681-682's
  `RequireSubqueryAlias` disjunct is unreachable (`SqliteDialect` leaves `RequireSubqueryAlias` `false`);
  `MakeApplyJoin` :404-410 is unreachable (`SqliteDialect.SupportsApply` is `false`), so only its gate at
  :399-400 runs (D5 fact 11).

### D6 overlap avoided (read, deliberately not repeated)

- `Sqlite/TupleInValuesSweepTests.cs` owns the *tuple* value list in the projection, HAVING, join
  condition, CTE and set operation; the shared `CommonTestSuite.In` owns the three-value
  `SqlFunctions.Sql.@in` literal list, the zero-value and one-value literal list, the captured two-value
  list/array, the nullable `@in` list containing null, the string list with null, and the
  grown/reassigned captured collection between two executions; `SqlitePlanCacheShapeCoverageTests.cs`
  owns the plan-key separation on an `@in` cardinality change and the same-shape reuse; D2's
  `Sqlite/PlanEqualityBranchCoverage2Tests.cs` owns the expression/plan comparer arms. D6 therefore drives
  the disjoint shapes listed above (instance `Contains` on a captured nullable list, the null flip while
  cached, captured-versus-literal plan separation, `storeInCache:false` then cached, the scalar `Contains`
  in a join condition and the nullable-tuple-element rejection).

### D6 unreachable targets (recorded, never fabricated)

- Every `PreparedPreWhere` arm (`QueryCommand.QueryPreparer.cs`:227-240 and :267-278) needs a prepared
  PreWhere; `SqliteDialect` leaves `SupportsPreWhere` at its `false` default and no public builder setter
  exists (the plan records `PreparePreWhere` as deferred with a trigger), so those branches are dead here.
- The `EnsureProviderSupportsTupleInValues` preflight (:209-210) is the SQL Server tuple gate and is
  unreachable on a dialect with a row-value renderer (`SqliteDialect.Tuple` is non-null).

### Notice - accepted validator rc 2 (recorded, not hidden)

`validate_inner_loop.py project rv1/journal.json` exits **2** with exactly one failure:
`FAIL: multiple boundary solution builds: 6` (six whole-solution builds at phase `boundary`, one per
cluster D1..D6, as the dispatch requires; the same run reported `multiple boundary solution builds: 4`
after the D3/D4 dispatch and `2` after D1/D2). This is the accepted reason of the plan
(`project/report may be rc 2 ONLY with the accepted reason 'multiple boundary solution builds: N'`).

### Notice - evidence generation regenerated inside DO

The generation snapshot of `manifest.json`/`generations/` predated the D3..D6 files, so
`pdca_evidence.py generate` refused with `preflight: existing evidence is stale` (the frozen tree no
longer matched). The stale generation directory and `manifest.json` were removed and regenerated at the
DO close of this dispatch; `generate` and `lint` are both rc 0 afterwards (`rv1/results/generate.log`,
`rv1/results/lint.log`).

### Command history of this dispatch (evidence-bearing commands are journalled)

The raw journal carries the evidence-bearing executions (the six whole-solution builds, the six cluster
inner runs, the three gate runs and the two defect probes). The remaining commands below are real
iterations whose output was not retained (compile-only builds overwritten, scratch probes deleted) and are
recorded here for completeness:

- `dotnet build tests/nextorm.integration.tests -c Debug` x 3 while writing D5/D6 (rc 0, rc 0, rc 0; the
  middle one re-ran after the D6 `OrderBy` fix; final state 0 warnings / 0 errors).
- `dotnet run ... -class NextORM.Integration.Tests.SourceRenderingBranchCoverage2Tests -noColor` x 1 (rc 0).
- `dotnet run ... -class NextORM.Integration.Tests.InPreparationBranchCoverage2Tests -noColor` x 2
  (rc 1 with the 3 `incomplete input` failures, then rc 0 after the fix).
- `dotnet run ... -class NextORM.Integration.Tests.ScratchCorrNullProbe -noColor` x 1 (rc 1, the D3 defect
  probe; file deleted - see the C2-D8-2 Notice above).
- `python3 .pdca/scripts/validate_inner_loop.py journal|project ...` and
  `python3 .pdca/scripts/pdca_evidence.py generate|lint ...` (all rc recorded in `rv1/results/`).

## DO results (round r1, attempt n 1/3 - boundary dispatch: preflight + F + T + coverage delta)

This dispatch ran the **single** boundary sweep of this DO and its reporting; no test or product source was
edited in it.

- **Preflight (recorded before the sweep, `rv1/results/boundary-preflight.log`)**: the Podman socket
  `unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` answered `OK`
  (`curl --unix-socket ... http://d/v1.40/_ping`, rc 0, machine already running); `NEXTORM_SKIP_PROVIDERS` was
  **unset** (fail-closed, no provider opt-out); the baseline cobertura was backed up byte-identically to
  `/tmp/nextorm/rc2/baseline-cobertura.xml` (sha256 `1da8525bfa2bc0c22d00c746bad5ddd8dcf761602e0e72b72594bab27286749d`,
  equal to the pinned `1da8525b...6749d`). Five reuse containers (postgres, sqlserver, mysql, mariadb,
  clickhouse) were reused - started at t+0.7 s / t+9.2 s and stopped-but-kept at the end; SQLite is in-process.
- **Baseline confirmed in writing before the sweep**: the pre-existing `/tmp/nextorm/rc/Summary.xml` is a
  cycle-1 snapshot reporting 24828/38281 branches and 44655 lines, i.e. **not** the pinned baseline, so the
  per-class baseline report was regenerated from the sha256-pinned backup into
  `/tmp/nextorm/rc2/baseline-report/` (rc 0) and reads **25136 / 38281** branches (65.6 %) and
  **45153 / 59676** lines (75.6 %) - the pinned numbers.
- **F - the boundary sweep (exactly one)**: rc **0**; runner summary
  `nextorm.integration.tests Total: 4274, Errors: 0, Failed: 0, Skipped: 195, Not Run: 0, Time: 61.054s`
  (`rv1/results/boundary.log`). Per-provider `[SKIP]` counts: `SqliteIntegrationTests` 41,
  `PostgresIntegrationTests` 26, `SqlServerIntegrationTests` 48, `MySqlIntegrationTests` 80,
  `ClickHouseIntegrationTests` 0, all `*SpecificTests` 0 - 195 skips total.
- **T - coverage report**: rc 0; source of truth `tests/coverage/integration-report/Summary.txt`:
  branches **25413 / 38281 = 66.3 %**, lines **45515 / 59676 = 76.2 %**
  (`rv1/results/boundary-t.log`). Delta vs the pinned baseline: **+277 branches** and **+362 lines**, with the
  denominator unchanged (38281 / 59676).
- **Skip budget**: rc 0 - `OK: 195 skips match the pinned budget`, the pinned 37-reason multiset is unchanged
  (`rv1/results/skip-budget.log`, `rv1/inputs/baseline-skips.json`).
- **Counted delta (`rv1/results/coverage-delta.md`)**: per-class and per-file counted branches come from the
  same reportgenerator model exported as Clover, whose project totals reproduce the Summary exactly on both
  sides (25136/38281 and 25413/38281). **No class and no file lost a branch (0 / 0)**; the positive per-class
  gains sum to **+277** and the positive per-file gains sum to **+277** - both equal the measured delta.
  Top classes: `QueryCommand<TResult>` +30, `QueryCommand` +29, `QueryCommand<TResult, TKey>` +29,
  `SqlSourceRenderer` +24, `ExpressionPlanEqualityComparer` +23. Top files:
  `QueryCommand.QueryPreparer.cs` +114, `ExpressionPlanEqualityComparer.cs` +45, `SqlSourceRenderer.cs` +24,
  `EntityBuilderEagerLoading.cs` +22, `QueryExecutor.cs` +12.
  Model caveat recorded in the delta file: reportgenerator's counted model is the one that defines the pinned
  baseline (an outer class entry also carries its nested types), so the tables are an attribution of that same
  total, not disjoint sets; the raw cobertura line-level basis is smaller (29610 total branches, +155 covered).
- **Verdict of this dispatch: GREEN.** AC1 (0 failed, skip multiset unchanged) and the AC2 delta reporting hold,
  and the cycle floor **+250 is met (+277)**.
- Evidence rows now filled: R3 (`results/boundary.log`), R4 (`results/coverage-delta.md` +
  `tests/coverage/integration-report/Summary.txt`), R5 (`results/skip-budget.log`). **D7, D8, D9 stay open** -
  no `D:`-unit is marked done by this dispatch, and the frozen manifest/`generations` snapshot predates the
  boundary artifacts (D9 owns regenerating them).

### Notice - accepted validator rc 2 (recorded, not hidden)

`validate_inner_loop.py journal rv1/journal.json` is **rc 0** after the boundary events were appended
(`rv1/results/validator-journal.txt`). `validate_inner_loop.py project rv1/journal.json` is **rc 2** with
exactly one failure - `FAIL: multiple boundary solution builds: 6` - i.e. the accepted reason of the plan
(the six whole-solution builds of D1..D6; unchanged by this dispatch). The journal command list was adjusted
so that no other hygiene failure is reported: the pre-sweep baseline report generation is journalled as the two
equivalent split invocations (`-reporttypes:Xml`, then `-reporttypes:TextSummary`) that were actually re-run,
instead of the combined `-reporttypes:"Xml;TextSummary"` argv whose `;` the journal hygiene rule flags as a
compound command.

## DO results (round r1, attempt n 1/3 - D7 + D8 + D9 dispatch: baseline sync, defect filing, evidence finalization)

This dispatch closed the DO. It edited only `AGENTS.md` and `.github/workflows/dotnet.yml` (two baseline
numbers), filed two product defects, and finalized the evidence + status. No test source, no product source,
no threshold and no `docs/**` page was touched.

### D7 - baseline sync (AC3, row R6). Numbers only, thresholds untouched

- `AGENTS.md:75` (Coverage section) and `.github/workflows/dotnet.yml:31` (the integration-gate comment) carried
  the superseded `75.6 % line / 65.6 % branch` pair; both now read **`76.2 % line / 66.3 % branch`** (the numbers
  measured by T).
- Thresholds verified untouched: `MIN_INTEGRATION_LINE_COVERAGE=65` / `MIN_INTEGRATION_BRANCH_COVERAGE=75`
  (`.github/workflows/dotnet.yml:33-34`) and the separate `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`
  sentence (`AGENTS.md:74`). `rg -n "75\.6|65\.6" AGENTS.md .github/workflows/dotnet.yml` now returns **rc 1 (no
  matches)**; `rg -n "76\.2|66\.3"` matches exactly the two edited lines.
- D7-only diffs (pre-D7 snapshot -> final, `diff -U0`): `results/agents-baseline.diff` and
  `results/workflow-baseline.diff` - exactly **one changed line each** (`@@ -75 +75 @@`, `@@ -31 +31 @@`), no
  context or threshold line. Both edited files stay CRLF (`file`: "with CRLF line terminators").
- `docs/**` and `docs/ru/**` untouched (no public API/behaviour change).
- **D7: done** (row R6 met).

### D8 - defect filing (AC4, rows R7/R8). Search first, then create

Both candidates were **re-validated on this worktree** with a real command before filing. A temporary scratch
probe (`tests/nextorm.integration.tests/Sqlite/ScratchD8RevalidateProbe.cs`) was built, run once, then
**deleted** and the integration project rebuilt (0 warnings / 0 errors) - it does not remain in the repo. A
second throwaway shape probe (printing the expression-tree node shapes) was likewise deleted and the project
rebuilt.

- Re-validation run: `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse dotnet run --project
  tests/nextorm.integration.tests -c Debug --no-build -- -class NextORM.Integration.Tests.ScratchD8RevalidateProbe
  -noColor` -> **rc 1** (the probe throws on purpose so the observed matrix is printed; `Total: 1, Failed: 1`),
  `results/d8-revalidate.log`. The observed SQL/rows reproduce the frozen evidence exactly.

| candidate | decision | issue | milestone | repro re-validation (rc 1) |
| --- | --- | --- | --- | --- |
| `type-binary-not-translated` | **created** | [#295](https://github.com/AlexeyShirshov/nextorm/issues/295) | `1.0.9-rc4` | `x.String is string && x.Id > 1` -> `where (somestring and (id > 1))` -> `[]` (expected `[2]`); `x.RequiredString is string && x.Id > 1` -> `where (requiredstring and (id > 1))` -> `[3]` (expected `[2,3]`); `(object)x.RequiredString is string` -> `[3]`; `x.Int is int` -> `where nullableint` -> `[2,3]` (accidentally correct) |
| `null-equality-not-is-null` | **created** | [#296](https://github.com/AlexeyShirshov/nextorm/issues/296) | `1.0.9-rc4` | `x.Int == (int?)null` -> `where nullableint = null` -> `[]` (expected `[1]`); `!= null` -> `nullableint != null` -> `[]`; captured null -> `nullableint = $capturedNull` -> `[]`; `x.String == (string?)null` -> `where somestring is null` -> `[3]` (correct); `x.String == capturedStringNull` -> `[]`; `!x.Int.HasValue` -> `BuildSqlCommandException: Cannot resolve column for member HasValue` |

- Duplicate search (mandatory step 0, before any create): `gh issue list --repo AlexeyShirshov/nextorm --state all
  --search <q>` over 14 keyword variants (`is string`, `TypeBinary`, `type test`, `is <type>`, `pattern matching`,
  `is operator`, `type check`, `translation`, `= null`, `nullable`, `is null`, `HasValue`, `null comparison`,
  `null equality`), plus `gh search issues --author @me` (open + closed) and the full open-milestone `1.0.9-rc4`
  list (40 issues). **No duplicate**: `TypeBinary`, `HasValue` and `null comparison` return 0 hits; the closest
  neighbours are #288 (`IS [NOT] DISTINCT FROM`, `1.0.9-rc4`) and #20 (`TODO: LINQ support`), both linked from
  the filed bodies as *related*, neither covering these defects. `gh search issues --state all` is rejected by
  gh 2.101 (valid: `open|closed`), so the equivalent `gh issue list --state all --search` was used for every query.
- Register: `inputs/defects.json` (one record per candidate: decision / issue / url / milestone / state / repro
  command / evidence paths / duplicate queries); raw transcript `results/defects.log`, plus
  `results/issue-search.log` and `results/issue-filing.log`.
- **D8: done** (rows R7 and R8 met). No candidate turned out to be expected behaviour; no repro was fabricated
  and none could not be re-validated.

### D9 - evidence finalization + status (AC5/AC6, rows R9, E1..E4)

- `inputs/deferred.json` written - the three deferred clusters and their activation triggers: **(A)** new
  container-only `ClickHouseScalarFunctions` (287 uncovered) / `ClickHouseDialect` (199) /
  `SqlServerSpecificFunctions` (264) classes, trigger = a measured shortfall against +250 after the reachable
  SQLite variants are exhausted (this cycle met +277, so A stays deferred and a **new PLAN r2** is required to
  activate it); **(B)** `PreparePreWhere` (`QueryCommand.QueryPreparer.cs:3353-3368`, arms `:227-240` / `:267-278`),
  no public builder setter, trigger = a proven public `JoinInto` rewrite path; **(C)** the SQLite-unreachable
  render paths recorded in D5 (`MakeJoinParts:293`, `MakeFrom:440-441` / `446-447` / `449-450` / `472-473`,
  `MakeRawSqlSource:681`, `MakeApplyJoin:404-410`) and D6 (`:227-240` / `:267-278`), trigger = a provider that
  renders them (container dialect work). Production footprint unchanged.
- `inputs/report.json` = the script's own mechanical projection (`validate_inner_loop.project_journal` over
  `journal.json`, 31 executions, nothing invented or dropped), written in `inputs/` so the receipt-scanner
  (`results/`, `receipts/`, `generations/**/results/`) does not mistake it for a receipt.
- Contract rows updated: **19/19 `met`** (R1..R9, V1..V6, E1..E4) with real evidence paths - nothing marked met
  that is not, and no row left non-met. Stale pre-D1 generation + `manifest.json` removed and regenerated:
  `pdca_evidence.py generate` **rc 0**, `lint` **rc 0**, `validate_inner_loop.py manifest` **rc 0** (frozen tree
  `50f2a97405633d4ca980ecf8ee22a9e6910056d5`, 19 receipts, manifest `git_head` `cc8113f7...`).
- Validators (final re-run on the regenerated manifest): `brief` **rc 0**, `journal` **rc 0**, `manifest`
  **rc 0**; `project` **rc 2** and `report` **rc 2** with the single accepted reason (Notice below). Full
  transcript `results/validator-d9.txt`; per-mode evidence `results/validator-brief.txt`,
  `results/validator-journal.txt`, `results/validator-project.txt`, `results/validator-report.txt`.
- Final tree check (`results/tree-preserved-final.txt`): `git status --short` has the **same 90 entries** as the
  D5/D6 snapshot - **0 missing, 0 new** (D7 edited two already-modified tracked files; D8/D9 wrote only inside
  the already-untracked `.pdca/` directories, which git collapses into one entry each). `git diff --check` rc 2
  with **pre-existing only** offenders (LF->CRLF warnings on 12 `benchmarks/BenchmarkDotNet.Artifacts/...`
  files; "new blank line at EOF" in `docs/specs/performance/acceptance-benchmarks.md:123` and
  `benchmark-report.md:3262` - all pre-existing entries, untouched by this cycle). CRLF preserved on every file
  this cycle touched. **HEAD unchanged** `cc8113f746fe1954e81f7489d6d2b08df96ee7a8`; **no commit, no push, no
  merge**.
- **D7, D8, D9: done** - DO closed. Durable state unchanged: Current cycle 2, Plan revision r1, Attempt n 1/3
  (no replan, no rejected candidate). No `D:`-unit was marked done without its gate.

### Boundary F/T numbers and the coverage delta (already reported, restated for the close)

- **F** (the single boundary sweep, phase `boundary`): rc **0**; `Total: 4274, Errors: 0, Failed: 0, Skipped: 195,
  Not Run: 0, Time: 61.054s` (`results/boundary.log`). Skip budget rc **0**: 195 skips, the pinned 37-reason
  multiset unchanged (`results/skip-budget.log`, `inputs/baseline-skips.json`).
- **T**: `tests/coverage/integration-report/Summary.txt` -> branches **25413 / 38281 = 66.3 %**, lines
  **45515 / 59676 = 76.2 %**. Delta vs the pinned baseline (25136 / 38281, 45153 / 59676): **+277 branches**,
  **+362 lines**, denominator unchanged -> the cycle floor **+250 is met**.
- `results/coverage-delta.md`: **0 classes and 0 files lost a branch**; the positive per-class gains and the
  positive per-file gains each sum to **+277**, equal to the measured delta. Top classes `QueryCommand<TResult>`
  +30, `QueryCommand` +29, `QueryCommand<TResult, TKey>` +29, `SqlSourceRenderer` +24,
  `ExpressionPlanEqualityComparer` +23; top files `QueryCommand.QueryPreparer.cs` +114,
  `ExpressionPlanEqualityComparer.cs` +45, `SqlSourceRenderer.cs` +24, `EntityBuilderEagerLoading.cs` +22,
  `QueryExecutor.cs` +12.

### Notice - accepted validator rc 2 (recorded, not hidden)

`validate_inner_loop.py project rv1/journal.json` and `validate_inner_loop.py report rv1/inputs/report.json
--journal rv1/journal.json` both exit **2** with exactly one FAIL: `FAIL: multiple boundary solution builds: 6`
(six whole-solution builds at phase `boundary`, one per cluster D1..D6, as the dispatch requires; the validator
caps boundary solution builds at 1 and exposes no allowance knob for builds). `project` stdout is empty on rc 2,
so `inputs/report.json` was produced by the same module's mechanical projection (`project_journal`) - the same
resolution used in cycle `integration-branch-coverage-1`. The projection-completeness rule reports no violation,
i.e. the report is a faithful projection and adds no violation of its own. This is the accepted reason of the
plan (AC6) and is neither hidden nor fabricated away.

## Progress journal (append-only)

2026-10-17T21:02:51Z | PLAN | revision r1 | iteration n 1/3 | plan r1 recorded (D1..D9, portfolio 88 facts, forecast 280..475, floor +250) | .pdca/status/integration-branch-coverage-2.md
2026-10-17T21:04:58Z | DO | revision r1 | iteration n 1/3 | setup: scope brief validator rc 0 before any edit; rv1 evidence root, contract (19 rows) and pinned skip baseline created | .pdca/integration-branch-coverage-2/rv1/results/validator-brief.txt
2026-10-17T21:05:06Z | DO | revision r1 | iteration n 1/3 | G (unit scoping guard) rc 0, selected 2, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/g1.log
2026-10-17T21:05:10Z | DO | revision r1 | iteration n 1/3 | journal seeded with the real G execution, validator rc 0 | .pdca/integration-branch-coverage-2/rv1/results/validator-journal.txt
2026-10-17T21:05:30Z | DO | revision r1 | iteration n 1/3 | evidence generate rc 0, lint rc 0 (pre-DO snapshot) | .pdca/integration-branch-coverage-2/rv1/results/generate.log
2026-10-17T21:10:42Z | DO | revision r1 | iteration n 1/3 | DO started: first D1 edit (Sqlite/RawShapeBranchCoverage2Tests.cs created) | tests/nextorm.integration.tests/Sqlite/RawShapeBranchCoverage2Tests.cs
2026-10-17T21:18:06Z | DO | revision r1 | iteration n 1/3 | defect candidate C2-D8-1 found while writing D2 (`is <type>` is not translated); minimal repro + observed SQL captured | .pdca/integration-branch-coverage-2/rv1/results/c2-repro-typebinary.md
2026-10-17T21:19:29Z | DO | revision r1 | iteration n 1/3 | B (whole solution) rc 0, 0 Warning(s) 0 Error(s) | .pdca/integration-branch-coverage-2/rv1/results/d1-build.log
2026-10-17T21:19:37Z | DO | revision r1 | iteration n 1/3 | I(D1) rc 0, selected 16, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/d1-inner.log
2026-10-17T21:19:43Z | DO | revision r1 | iteration n 1/3 | B (whole solution) rc 0, 0 Warning(s) 0 Error(s) | .pdca/integration-branch-coverage-2/rv1/results/d2-build.log
2026-10-17T21:19:46Z | DO | revision r1 | iteration n 1/3 | I(D2) rc 0, selected 18, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/d2-inner.log
2026-10-17T21:19:55Z | DO | revision r1 | iteration n 1/3 | G re-run after the last edit rc 0, selected 2, failed 0 | .pdca/integration-branch-coverage-2/rv1/results/g2.log
2026-10-17T21:21:08Z | DO | revision r1 | iteration n 1/3 | C2-D8-1 characterization probe rc 0 (observed rows locked in as evidence, probe file removed) | .pdca/integration-branch-coverage-2/rv1/results/c2-repro-typebinary-observed.log
2026-10-17T21:21:15Z | DO | revision r1 | iteration n 1/3 | evidence regenerated at the DO close: generate rc 0, lint rc 0 (stale pre-DO generation removed) | .pdca/integration-branch-coverage-2/rv1/results/lint.log
2026-10-17T21:21:20Z | DO | revision r1 | iteration n 1/3 | project validator rc 2 with the single accepted reason `multiple boundary solution builds: 2` (Notice above) | .pdca/integration-branch-coverage-2/rv1/results/validator-project.txt
2026-10-17T21:40:13Z | DO | revision r1 | iteration n 1/3 | DO started: first D3 edit (Sqlite/CorrelatedBranchCoverage2Tests.cs created, 16 facts) | tests/nextorm.integration.tests/Sqlite/CorrelatedBranchCoverage2Tests.cs
2026-10-17T21:42:34Z | DO | revision r1 | iteration n 1/3 | D4 edit: Sqlite/EagerModeBranchCoverage2Tests.cs created (12 facts) | tests/nextorm.integration.tests/Sqlite/EagerModeBranchCoverage2Tests.cs
2026-10-17T21:42:56Z | DO | revision r1 | iteration n 1/3 | B (whole solution) rc 0, 0 Warning(s) 0 Error(s) | .pdca/integration-branch-coverage-2/rv1/results/d3-build.log
2026-10-17T21:42:58Z | DO | revision r1 | iteration n 1/3 | I(D3) rc 0, selected 16, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/d3-inner.log
2026-10-17T21:43:07Z | DO | revision r1 | iteration n 1/3 | B (whole solution) rc 0, 0 Warning(s) 0 Error(s) | .pdca/integration-branch-coverage-2/rv1/results/d4-build.log
2026-10-17T21:43:09Z | DO | revision r1 | iteration n 1/3 | I(D4) rc 0, selected 12, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/d4-inner.log
2026-10-17T21:43:14Z | DO | revision r1 | iteration n 1/3 | G (unit scoping guard) rc 0, selected 2, failed 0 | .pdca/integration-branch-coverage-2/rv1/results/d3d4-guard.log
2026-10-17T21:44:20Z | DO | revision r1 | iteration n 1/3 | defect candidate C2-D8-2 found in D3 (`null-equality-not-is-null`): probe rc 1 with the observed rows and the generated SQL; the probe file was deleted after the run | .pdca/integration-branch-coverage-2/rv1/results/c2-repro-nullequality.log
2026-10-17T21:44:44Z | DO | revision r1 | iteration n 1/3 | C2-D8-2 observed matrix characterized (`== null`/`!= null` on a nullable value column render `= null`/`!= null`; a captured null renders `= $param`; all silently return zero rows); filing belongs to D8 | .pdca/integration-branch-coverage-2/rv1/results/c2-repro-nullequality.md
2026-10-17T21:45:12Z | DO | revision r1 | iteration n 1/3 | I(D3) final green re-run after the scratch probe was deleted and the project rebuilt: rc 0, selected 16, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/d3-inner.log
2026-10-17T21:45:15Z | DO | revision r1 | iteration n 1/3 | I(D4) final green re-run after the scratch probe was deleted and the project rebuilt: rc 0, selected 12, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/d4-inner.log
2026-10-17T21:45:20Z | DO | revision r1 | iteration n 1/3 | G re-run after the scratch-probe deletion and rebuild: rc 0, selected 2, failed 0 | .pdca/integration-branch-coverage-2/rv1/results/d3d4-guard.log
2026-10-17T21:53:12Z | DO | revision r1 | iteration n 1/3 | D5 edit: Sqlite/SourceRenderingBranchCoverage2Tests.cs created (14 facts) | tests/nextorm.integration.tests/Sqlite/SourceRenderingBranchCoverage2Tests.cs
2026-10-17T21:54:14Z | DO | revision r1 | iteration n 1/3 | D6 edit: Sqlite/InPreparationBranchCoverage2Tests.cs created (12 facts) | tests/nextorm.integration.tests/Sqlite/InPreparationBranchCoverage2Tests.cs
2026-10-17T21:54:36Z | DO | revision r1 | iteration n 1/3 | B (whole solution) rc 0, 0 Warning(s) 0 Error(s) | .pdca/integration-branch-coverage-2/rv1/results/d5-build.log
2026-10-17T21:54:38Z | DO | revision r1 | iteration n 1/3 | I(D5) rc 0, selected 14, failed 0, skipped 0 | .pdca/integration-branch-coverage-2/rv1/results/d5-inner.log
2026-10-17T21:54:43Z | DO | revision r1 | iteration n 1/3 | B (whole solution) rc 0, 0 Warning(s) 0 Error(s) | .pdca/integration-branch-coverage-2/rv1/results/d6-build.log
2026-10-17T21:54:45Z | DO | revision r1 | iteration n 1/3 | I(D6) rc 0, selected 12, failed 0, skipped 0 (after the `OrderBy(x => x)` -> `OrderBy(e => e.Id)` fix; the first run was rc 1 with 3 `incomplete input` failures) | .pdca/integration-branch-coverage-2/rv1/results/d6-inner.log
2026-10-17T21:54:50Z | DO | revision r1 | iteration n 1/3 | G (unit scoping guard) rc 0, selected 2, failed 0 | .pdca/integration-branch-coverage-2/rv1/results/d5d6-guard.log
2026-10-17T21:55:15Z | DO | revision r1 | iteration n 1/3 | tree re-snapshot: 88 pre-existing entries intact, +2 new (D5/D6), 0 missing | .pdca/integration-branch-coverage-2/rv1/results/tree-preserved.txt
2026-10-17T21:55:52Z | DO | revision r1 | iteration n 1/3 | catch-up recorded in this dispatch for the D3/D4 close: journal validator rc 0, tree re-snapshot 88 pre-existing intact +2 (D3/D4), 0 missing, project validator rc 2 with the single accepted reason `multiple boundary solution builds: 4` (Notice above) | .pdca/integration-branch-coverage-2/rv1/results/validator-project.txt
2026-10-17T21:55:55Z | DO | revision r1 | iteration n 1/3 | journal validator rc 0 after the D5/D6 events were appended | .pdca/integration-branch-coverage-2/rv1/results/validator-journal.txt
2026-10-17T21:55:55Z | DO | revision r1 | iteration n 1/3 | project validator rc 2 with the single accepted reason `multiple boundary solution builds: 6` (Notice above) | .pdca/integration-branch-coverage-2/rv1/results/validator-project.txt
2026-10-17T21:56:33Z | DO | revision r1 | iteration n 1/3 | evidence regenerated at the DO close: stale generation + manifest removed, generate rc 0 | .pdca/integration-branch-coverage-2/rv1/results/generate.log
2026-10-17T21:56:34Z | DO | revision r1 | iteration n 1/3 | evidence lint rc 0 over the regenerated manifest | .pdca/integration-branch-coverage-2/rv1/results/lint.log
2026-10-17T22:00:16Z | boundary | revision r1 | iteration n 1/3 | preflight: podman socket `_ping` OK, NEXTORM_SKIP_PROVIDERS unset, baseline cobertura sha256 `1da8525b...6749d` backed up byte-identical; the cycle-1 `/tmp/nextorm/rc/Summary.xml` (24828/38281) is not the baseline, so the per-class baseline report was regenerated from the pinned backup -> 25136/38281 branches and 45153/59676 lines confirmed in writing before the sweep | .pdca/integration-branch-coverage-2/rv1/results/boundary-preflight.log
2026-10-17T22:02:04Z | boundary | revision r1 | iteration n 1/3 | F boundary sweep (exactly one, kind integration) rc 0: Total 4274, Errors 0, Failed 0, Skipped 195, Not Run 0, Time 61.054s; five reused containers (postgres, sqlserver, mysql, mariadb, clickhouse), SQLite in-process; per-provider skips sqlite 41 / postgres 26 / sqlserver 48 / mysql 80 / clickhouse 0 | .pdca/integration-branch-coverage-2/rv1/results/boundary.log
2026-10-17T22:02:14Z | boundary | revision r1 | iteration n 1/3 | skip budget rc 0: 195 skips match the pinned 37-reason multiset, unchanged | .pdca/integration-branch-coverage-2/rv1/results/skip-budget.log
2026-10-17T22:02:16Z | boundary | revision r1 | iteration n 1/3 | T coverage report rc 0: branches 25413/38281 (66.3%), lines 45515/59676 (76.2%) -> +277 branches / +362 lines vs 25136/38281 and 45153/59676, denominator unchanged | tests/coverage/integration-report/Summary.txt (+ .pdca/integration-branch-coverage-2/rv1/results/boundary-t.log)
2026-10-17T22:08:53Z | boundary | revision r1 | iteration n 1/3 | counted delta: +277 branches (floor +250 MET), 0 classes / 0 files lost a branch, per-class and per-file gains both sum to +277 | .pdca/integration-branch-coverage-2/rv1/results/coverage-delta.md
2026-10-17T22:09:10Z | boundary | revision r1 | iteration n 1/3 | journal validator rc 0 after the boundary events (F / T / skip budget / delta / baseline regeneration) were appended; project validator rc 2 with the single accepted reason `multiple boundary solution builds: 6` | .pdca/integration-branch-coverage-2/rv1/results/validator-journal.txt
2026-10-17T22:09:10Z | boundary | revision r1 | iteration n 1/3 | boundary dispatch verdict: GREEN - F 0 failed, skip multiset unchanged, +277 >= +250 floor; no test or product source edited in this dispatch, D7..D9 remain open | this status file
2026-10-17T22:10:30Z | DO | revision r1 | iteration n 1/3 | D7 baseline sync: AGENTS.md:75 and .github/workflows/dotnet.yml:31 changed 75.6 % line / 65.6 % branch -> 76.2 % line / 66.3 % branch (numbers only); thresholds MIN_INTEGRATION_LINE_COVERAGE=65 / MIN_INTEGRATION_BRANCH_COVERAGE=75 verified untouched (rg for the old numbers now rc 1); D7-only diffs written (one changed line each), CRLF preserved | .pdca/integration-branch-coverage-2/rv1/results/agents-baseline.diff, .pdca/integration-branch-coverage-2/rv1/results/workflow-baseline.diff
2026-10-17T22:13:30Z | DO | revision r1 | iteration n 1/3 | D8 re-validation: temporary ScratchD8RevalidateProbe built + run rc 1 - both defects reproduced on this worktree with their observed SQL/rows; probe deleted and the integration project rebuilt (0 Warning(s) 0 Error(s)) | .pdca/integration-branch-coverage-2/rv1/results/d8-revalidate.log
2026-10-17T22:13:50Z | DO | revision r1 | iteration n 1/3 | D8 duplicate search (mandatory step 0): 14 keyword variants over open+closed, the @me author search and the full 1.0.9-rc4 milestone list -> no duplicate (TypeBinary / HasValue / null comparison return 0 hits) | .pdca/integration-branch-coverage-2/rv1/results/issue-search.log
2026-10-17T22:17:22Z | DO | revision r1 | iteration n 1/3 | D8 filing: issue #295 created (type-binary-not-translated) and issue #296 created (null-equality-not-is-null) - both milestone 1.0.9-rc4, label bug, state OPEN, verified with gh issue view | .pdca/integration-branch-coverage-2/rv1/results/issue-filing.log
2026-10-17T22:19:00Z | DO | revision r1 | iteration n 1/3 | D8 register + transcript written: inputs/defects.json (2 records, decision=created) and results/defects.log | .pdca/integration-branch-coverage-2/rv1/inputs/defects.json
2026-10-17T22:20:00Z | DO | revision r1 | iteration n 1/3 | D9 deferred.json written: cluster A (container-only ClickHouse/SqlServer classes), B (PreparePreWhere), C (SQLite-unreachable render paths) with activation triggers; production footprint unchanged | .pdca/integration-branch-coverage-2/rv1/inputs/deferred.json
2026-10-17T22:20:30Z | DO | revision r1 | iteration n 1/3 | D9 report projection: validate_inner_loop.py project rc 2 (stdout empty) with the accepted reason `multiple boundary solution builds: 6`; inputs/report.json written as the module's own project_journal projection (31 executions, nothing invented or dropped) | .pdca/integration-branch-coverage-2/rv1/inputs/report.json
2026-10-17T22:21:00Z | DO | revision r1 | iteration n 1/3 | D9 contract rows updated to 19/19 met with real evidence paths; stale pre-D1 generation + manifest removed; pdca_evidence.py generate rc 0, lint rc 0, validate_inner_loop.py manifest rc 0 (frozen tree 50f2a974..., 19 receipts, git_head cc8113f7...) | .pdca/integration-branch-coverage-2/rv1/results/generate.log
2026-10-17T22:21:04Z | DO | revision r1 | iteration n 1/3 | D9 final tree check: 90/90 porcelain entries preserved, 0 missing, 0 new; git diff --check rc 2 with pre-existing-only offenders; CRLF preserved everywhere touched; HEAD unchanged cc8113f746fe1954e81f7489d6d2b08df96ee7a8, no commit, no push | .pdca/integration-branch-coverage-2/rv1/results/tree-preserved-final.txt
2026-10-17T22:21:35Z | DO | revision r1 | iteration n 1/3 | D9 validators final re-run on the regenerated manifest: brief rc 0, journal rc 0, manifest rc 0; project rc 2 and report rc 2 with the single accepted reason `multiple boundary solution builds: 6` (Notice above) | .pdca/integration-branch-coverage-2/rv1/results/validator-d9.txt
2026-10-17T22:22:19Z | DO | revision r1 | iteration n 1/3 | D7, D8, D9 done - DO closed: +277 branches (floor +250 met), 2 defects filed (#295, #296), 19/19 contract rows met; phase set to `DO closed - C: in progress (CHECK)`; no CHECK verdict and no ACT section written | .pdca/status/integration-branch-coverage-2.md

## ACT (round r1, attempt n 1/3) - CHECK verdict, final numbers, accepted findings, residual

### CHECK verdict (pointer)

- **CHECK: PASS** on plan **r1**, attempt **n 1/3**: **19/19 contract rows `met`**, frozen tree
  `50f2a97405633d4ca980ecf8ee22a9e6910056d5`, all six acceptance criteria (AC1..AC6) met.
- The checker reconciled `results/boundary.log`, `results/boundary-t.log`, `results/coverage-delta.md`,
  `inputs/defects.json`, `results/tree-preserved-final.txt` and `results/validator-d9.txt` against the contract
  rows, and issued **one targeted re-gather** (owner `check`, no new F/B, no tree change). Its findings were
  adjudicated **accepted non-binding** and are recorded under "Accepted non-binding findings" below; none is a
  gate failure and none required a code, test or evidence change.

### Final numbers (source of truth `tests/coverage/integration-report/Summary.txt`, produced by the pinned T run over the green F sweep)

| metric | baseline | final | delta | floor |
| --- | --- | --- | --- | --- |
| branch covered/total | 25136/38281 (65.6 %) | **25413/38281 (66.3 %)** | **+277** | +250 met |
| line covered/total | 45153/59676 (75.6 %) | **45515/59676 (76.2 %)** | **+362** | - |

- Full suite (F, phase `boundary`, exactly one): **4274 tests, 0 failed, 195 skipped, errors 0, 61.054 s**; all
  five container providers (postgres, sqlserver, mysql, mariadb, clickhouse) plus in-process SQLite ran; skip
  budget rc 0 (195 skips / 37 reasons, multiset equal to `inputs/baseline-skips.json`).
- **88 new facts** (16+18+16+12+14+12 across D1..D6), **0 product edits**.
- **0 classes and 0 files lost a branch**; per-class and per-file positive gains each sum to exactly **+277**.
- Top files by counted branch gain: `QueryCommand.QueryPreparer.cs` +114, `ExpressionPlanEqualityComparer.cs` +45,
  `SqlSourceRenderer.cs` +24, `EntityBuilderEagerLoading.cs` +22, `QueryExecutor.cs` +12. Top classes:
  `QueryCommand<TResult>` +30, `QueryCommand` +29, `QueryCommand<TResult,TKey>` +29, `SqlSourceRenderer` +24,
  `ExpressionPlanEqualityComparer` +23.

### Criteria matrix (AC1..AC6) - all met

| AC | status | one-line closure |
| --- | --- | --- |
| AC1 | met | real-DB sweep F 4274 / 0 failed / 195 skipped + scoping guard G rc 0; no mock/in-memory, no extra provider skip |
| AC2 | met | counted delta **+277 >= +250** with the denominator unchanged (38281 / 59676) and the per-file contributions listed |
| AC3 | met | both baseline files now read **76.2 % line / 66.3 % branch**; thresholds 65/75 (integration) and 85/75 (whole solution) untouched |
| AC4 | met | #295 + #296 filed as `bug` in milestone `1.0.9-rc4`, both verified OPEN with minimal repros |
| AC5 | met | status finalized, 90/90 tree entries preserved, HEAD `cc8113f746fe1954e81f7489d6d2b08df96ee7a8`, no commit/push |
| AC6 | met | brief/journal/manifest/generate/lint rc 0; project/report rc 2 with the single accepted reason only |

### Filed defects

- **#295 `type-binary-not-translated`** - `x.String is string` mis-translates to a bare operand (`where somestring`),
  i.e. SQL truthiness instead of a type test, returning wrong/empty rows. Minimal repro:
  `x.String is string && x.Id > 1` -> `[]`, expected `[2]`.
- **#296 `null-equality-not-is-null`** - `x.Int == (int?)null` renders `where nullableint = null` and returns zero
  rows; `!= null` and a captured null behave the same; a string-literal null renders correctly as `is null`;
  `!x.Int.HasValue` throws `BuildSqlCommandException`. Root cause pinned to the bare-`ConstantExpression` null check
  missing `Convert(Constant(null, object), int?)` (`src/nextorm.core/Visitors/WhereExpressionVisitor.cs:27-36,57`).
- Both: label `bug`, milestone `1.0.9-rc4`, state OPEN, verified with `gh issue view`. Duplicate search before
  filing: 14 keyword variants + `--author @me` + the full milestone list, no duplicate. Register
  `inputs/defects.json`.

### Accepted non-binding findings (from the CHECK re-gather)

Five findings, all **accepted non-binding** - test-quality notes for the next cycle, not gate failures:

- (a) `Sqlite/RawShapeBranchCoverage2Tests.cs:344,372` - global `HaveCount(2)` over the shared `complex_entity`.
- (b) `Sqlite/PlanEqualityBranchCoverage2Tests.cs:338,536` - global `BeEmpty()`.
- (c) `Sqlite/InPreparationBranchCoverage2Tests.cs:116` - `BeEmpty()` on the empty-`Contains` fold (tautologically
  empty).
- (d) `Sqlite/PlanEqualityBranchCoverage2Tests.cs:520` overlaps `SqlitePlanCacheShapeCoverageTests.cs:414`.
- (e) `Sqlite/RawShapeBranchCoverage2Tests.cs:73-84` partly overlaps `SqlitePlanCacheShapeCoverageTests.cs:434`.

Rationale recorded for the adjudication: `complex_entity` is a 3-row seed-only table with no suite writer on SQLite
outside the seed SQL (established by the cycle-1 audit), the predicates filter an immutable seed domain, and the
4274-test full sweep was green; coverage is a set union, so an overlapping fact adds no false branches; editing
after the freeze would invalidate the frozen tree and breach the `integration_boundary_runs: 1` cap for zero
coverage or correctness change. These are recorded as inputs for the next cycle rather than counted as clean.

### Accepted non-binding tooling deviation

`validate_inner_loop.py project` and `report` return **rc 2** with the single verbatim failure
`FAIL: multiple boundary solution builds: 6` - six real whole-solution builds, one per cluster D1..D6, each
journalled with its real phase/command/exit/timestamp. The validator caps boundary solution builds at 1 and rejects
a broad build in `inner`, with no allowance knob. No other validator error exists; `brief`/`journal`/`manifest`
rc 0 and the projection covers 31/31 executions. Nothing was deleted, moved or invented.

### Residual / next plan inputs (the gate stays RED)

- At the measured denominator **38281**, the 75 % integration branch gate needs **28711** covered branches; the
  final figure is **25413** -> **residual 3298 branches (8.7 pp)**. The gate stays RED: this cycle met its own floor
  (+277 >= +250) but not the task-level 75 %.
- Next-cycle lever list with **PRE-cycle** uncovered counts (a **fresh per-class parse of the new cobertura is
  required before targeting** - these are cycle-1 / pre-cycle numbers, not post-cycle):
  - container-only `ClickHouseScalarFunctions` (287) + `ClickHouseDialect` (199) + `SqlServerSpecificFunctions`
    (264) - now unlocked as the **primary lever**, since the SQLite cheap clusters are largely harvested;
  - remaining SQLite-adjacent hotspots: `QueryCommand.QueryPreparer.cs` (~610 after +114), `EntityBuilder.cs`
    (~325), `SqlBuilder.cs` (290, untouched by this cycle - the renderer work landed in `SqlSourceRenderer`),
    `CorrelatedQueryExpressionVisitor` (~318 pre-cycle), `QueryPlanEqualityComparer` (~191 after the D2 gain),
    `SqlSourceRenderer` (~136 after +24), `EntityBuilderEagerLoading` (~44 after +22).
- A **cycle 3 must open its own PLAN (r1)**; the deferred clusters' activation trigger (a measured shortfall after
  the SQLite variants are exhausted) has effectively fired.

### Transferable lessons

1. The cheapest high-yield lever on this codebase is still the **public-API shape sweep over one context**: the
   plan-cache/equality + raw-shape + renderer clusters delivered **+277 with 88 facts and zero product edits**.
2. A **"reject/guard" fact is only worth writing if it is paired with a live counterpart**, and a live probe must
   be **scoped to class-owned data** - four global seed-table assertions survived the sweep but are latent
   fragility and must be fixed in the next cycle rather than counted as clean.

### Finalization checks

- HEAD unchanged **`cc8113f746fe1954e81f7489d6d2b08df96ee7a8`**; **no commit** - the new test sources and `.pdca/**`
  remain uncommitted (`git status --short` still shows the pre-existing entries plus this cycle's additions);
  **no push**.
- `git diff --name-only -- src/` **empty**; `docs/**` and `docs/ru/**` untouched; CRLF preserved on every file this
  cycle touched.
- Status file line count: **664** lines before this ACT append, **788** lines at the ACT close
  (this section plus its journal entries included).
- **ACT re-froze the evidence**: `generate` / `lint` / `manifest` / `journal` / `brief` were re-run after this
  section was appended; the frozen tree stays **`50f2a97405633d4ca980ecf8ee22a9e6910056d5`** (the status file lives
  under `.pdca/**`, which the contract excludes from the overlay, so appending ACT text cannot move the tree), and
  the generation digest is recorded in `results/validator-act.txt`. This one line supersedes the D9 tree reference
  above. `project` / `report` stay rc 2 with the same accepted reason (recorded, not retried).

2026-10-17T22:30:14Z | ACT | revision r1 | iteration n 1/3 | CHECK verdict PASS on plan r1 / n 1/3: 19/19 contract rows met, frozen tree 50f2a974..., AC1..AC6 all met; the checker reconciled boundary.log / boundary-t.log / coverage-delta.md / inputs/defects.json / tree-preserved-final.txt / validator-d9.txt and issued one targeted re-gather whose findings were accepted non-binding | .pdca/integration-branch-coverage-2/rv1/results/validator-d9.txt
2026-10-17T22:30:14Z | ACT | revision r1 | iteration n 1/3 | ACT accepted: final numbers branches 25413/38281 (66.3%, +277 >= floor +250) and lines 45515/59676 (76.2%, +362); 5 non-binding findings and the accepted tooling deviation recorded; residual 3298 branches (8.7 pp) - gate stays RED, cycle 3 must open its own PLAN r1 | this status file
2026-10-17T22:30:14Z | ACT | revision r1 | iteration n 1/3 | ACT defect record: #295 (type-binary-not-translated) and #296 (null-equality-not-is-null), both milestone 1.0.9-rc4, label bug, OPEN, verified; duplicate search clean | .pdca/integration-branch-coverage-2/rv1/inputs/defects.json
2026-10-17T22:30:14Z | ACT | revision r1 | iteration n 1/3 | ACT finalization: HEAD unchanged cc8113f746fe1954e81f7489d6d2b08df96ee7a8, no commit, no push, git diff --name-only -- src/ empty, docs/** untouched, 90/90 working-tree entries preserved, CRLF preserved | this status file
2026-10-17T22:31:36Z | ACT | revision r1 | iteration n 1/3 | evidence re-frozen after the ACT append: generate rc 0, lint rc 0, manifest rc 0, journal rc 0, brief rc 0; frozen tree unchanged 50f2a97405633d4ca980ecf8ee22a9e6910056d5, generation f1c3c16bdc3a58a750334f621a9c3729e4134085a22e3aacfda542f8ad9f4009 (19/19 rows); project/report rc 2 with the same accepted reason `multiple boundary solution builds: 6` - recorded, not retried | .pdca/integration-branch-coverage-2/rv1/results/validator-act.txt
2026-10-17T22:31:36Z | ACT | revision r1 | iteration n 1/3 | ACT closed (EXIT): status CLOSED, cycle 2 plan r1 attempt n 1/3; durable state unchanged (no replan, no rejected candidate); defect history retained (2 keys, 0 fixes applied); 786 lines before the ACT append, 788 lines at the close | .pdca/status/integration-branch-coverage-2.md
