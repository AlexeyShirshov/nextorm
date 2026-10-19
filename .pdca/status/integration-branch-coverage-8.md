# integration-branch-coverage-8
Status: CLOSED
Current cycle: 8
Plan revision: 2
Contract revision: rv2
Attempt: n 2/3 (r2)
Phase: ACT closed (EXIT)

## Goal

Raise counted integration-suite branch coverage by >= +250 (baseline 27286/38317, target >= 27536)
with tests against REAL DBMS only; thresholds 85/75/65/75 byte-identical; no product edits; the
protected set this cycle is EMPTY (verified at D0).

## Acceptance criteria

- REQ-COV: counted covered branches >= 27536 and counted delta >= 250 on the cycle-7 counting
  procedure (baseline 27286/38317); negative: smaller gain, incompatible counting, modified exclusions.
- REQ-LIVE: every new case runs against file SQLite or a live container provider; no
  InMemoryDataContext; FluentAssertions only; negative: rendered-SQL-only or vacuous fact.
- REQ-QUALITY: `dotnet build nextorm.slnx -c Debug` 0/0; guards 2/2; CRLF; no suppression; negative:
  warning, failed new case, forbidden assertion.
- REQ-BOUNDARY: exactly ONE accepted fixed F+T at DO->CHECK, all providers exercised; negative:
  silent retry, provider opt-out, unaccounted skip. An environmental red may be replaced by exactly
  ONE replan-authorized sweep with identical commands.
- REQ-DOCS: only measured numbers in `AGENTS.md` and `.github/workflows/dotnet.yml`; thresholds
  byte-identical.
- REQ-DEFECTS: every product defect has stable key + live-DB repro + dedup search + issue in
  milestone `1.0.9-rc4` (comment on existing match, never duplicate); environmental provider
  timeouts recorded, not filed.
- REQ-STATE: protected set EMPTY recorded and verified; no `src/**` change; durable state survives
  restart.

## Tasks (owner coder, sequential, one working tree)

| id | file / provider | targets | forecast | cap |
| D0 | state/evidence/API map | seal, baseline, public-call map | 0 | 0 |
| D1 | tests/nextorm.integration.tests/Coverage8/Cycle8FilterValidationTests.cs; SQLite | QueryFilterValidator 42-56,161-245,258-383,414-461 via global filter + INSERT/MERGE/bulk routes | +90 | 26 |
| D2 | Coverage8/Cycle8EfFilterBridgeTests.cs; SQLite | EfQueryFilterTranslator 55-144,152-291 via EF HasQueryFilter -> NextOrmModelMapper.Register | +85 | 24 |
| D3 | Coverage8/Cycle8GroupingTests.cs; Postgres + SQL Server | CUBE/grouping sets live; SqlBuilder 1351-1430 fallback | +60 | 18 |
| D4 | Coverage8/Cycle8SqlServerResidualTests.cs; SQL Server | index-ignore hint (SqlServerDialect 306 + SqlSourceRenderer 620), typed/date/string 385-635, with-ties 703/707 | +45 | 16 |
| D5 | Coverage8/Cycle8ClickHouseResidualTests.cs; ClickHouse | SAMPLE (flag 494) on class-owned MergeTree; date/concat/nullable 116-123,546-555,673-819 | +45 | 14 |
| D6 | Coverage8/Cycle8PostgresMySqlResidualTests.cs; Postgres + MySQL | PG hints/math/date 242-269,413-434,499-516; MySQL default/last-id/date/json/type 31-40,217-222,247-333,386-399 | +45 | 16 |
| D7 | docs/AGENTS.md, workflow, status/final evidence | boundary, counted delta, measured docs, registers | 0 | 0 |

Forecast +370 (forecasts are not evidence). Fact budget <=120 total, allocated 114.

Variant matrix V01..V18 and evidence contract rows R01-R07, E01-E03, V01-V18 as the planner
specified; each V-row closes as test / guard / deferred+trigger.

## Test strategy

Inner loop per unit `-class NextORM.Integration.Tests.<Class>`; SQLite inner with
`NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,mariadb,clickhouse`; provider inner with
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; rebuild
`dotnet build nextorm.slnx -c Debug` after each .cs edit; ONE boundary F+T with the fixed commands
from the brief.

## Docs plan

Measured numbers only in `AGENTS.md` + workflow comments; thresholds byte-identical.

Perf: not needed (tests-only, no production path change).

Recon: no general spike; <=2 bounded probes per unit inside D1..D6.

Unit mode: sequential, one tree, no worktrees/commits/push.

## Deferred

Carry the 38 cycle-7 rows by reference from
`.pdca/integration-branch-coverage-7/rv2/inputs/deferred.json`; reopen D2-GROUPING-CUBE +
D2-GROUPING-SETS-LIVE in D3, D1-CH-SAMPLE in D5, D1-SS-INDEX-IGNORE in D4; keep
D1-08-SS-SUBQUERY-HINT, D6-CH-SETTINGS-SCALAR, *INMEMORY*, D2-CH-QUALIFIED-SERVER, D3-MYSQL-*
deferred; D1B-GROUPING-SETS-SQLITE stays deferred.

Blockers: #303/#304/#307/#308/#309 never asserted.

## Deferred register

- Carried: 38 rows (17 carried + 21 cycle-7 new) in `rv1/inputs/deferred.json`, carried by reference and unchanged.
- Cycle-8 reopened reconciliation (4 rows): 4 met / 0 still-deferred — `D2-GROUPING-CUBE` and
  `D2-GROUPING-SETS-LIVE` (unit D3), `D1-CH-SAMPLE` (unit D5), `D1-SS-INDEX-IGNORE` (unit D4),
  each with the observed public route and `rv1/logs/D3-*.log` / `D5-*.log` / `D4-*.log` evidence.
- New cycle-8 rows: 21 deferred observations (`C8-D1-*` 2, `C8-D2-*` 7, `C8-D3-*` 2, `C8-D4-*` 4,
  `C8-D5-*` 2, `C8-D6-*` 4), disposition `deferred`, no fact slot consumed.
- File: `rv2/inputs/deferred-cycle8.json` (validated rc 0, `rv2/logs/D7d-deferred.log`); defect keys stay
  in `rv2/inputs/defects.json` and are not duplicated here.

## Final measured block (rv2 / D7c)

- Accepted sweep: **F rc 0** - Total 4822 / Passed 4627 / Failed 0 / Skipped 195 / Not Run 0 / 90.116 s
  (`rv2/logs/D7c-F.log`); **T rc 0** (`rv2/logs/D7c-T.log`). The red r1 F is preserved and not credited.
- Literal Summary: line 47659/59949 = 79.4 %, branch 27733/38407 = 72.2 %, methods 8219
  (`rv2/results/final-Summary.txt`).
- Counted: **branch 27668/38317 = 72.208 %** (delta **+382** over 27286; floor 250 met; target 27536 met),
  **line 47477/59713 = 79.509 %** (delta +372 over 47105), methods denominator 8191.
- Residual to 75 %: 28738 - 27668 = **1070 branches**; the integration gate is still red and the
  thresholds stay untouched.
- Denominator drift: literal branch total 38407 vs pinned 38317 = 90 (drift vs baseline 0); literal line
  59949 vs pinned 59713 = 236 (drift vs baseline 0).
- Skip budget: 195 skips / 37 reasons, pinned matrix matched (`rv2/logs/D7c-skip-budget.log` rc 0).
- Protected set: **EMPTY**, delta 0 (`git diff --stat -- src` empty, `git status --porcelain -- src` empty).
- Per-file raw branch delta: +235 total (15 positive, 1 negative: `QueryPlan.cs` -1)
  (`rv2/inputs/counts-final.json`).
- Docs: measured numbers only in `AGENTS.md` + `.github/workflows/dotnet.yml`; threshold proof
  `rv2/results/threshold-diff.txt` (diff rc 0; `coverage*.settings.xml` sha256 unchanged).
- Defect history: `rv2/inputs/defects.json` - `CACHEDPREP-RAW-INDEX-GENERATION-RACE` recorded, not filed
  (r1 n1, r2 n1, fixes 0); `D4-SS-FETCH-WITH-TIES` -> #298 comment posted; `D5-CH-DATE-DIFF-INT32` ->
  #255 comment verified; 0 new issues, 0 applied fixes.
- Evidence map: `rv2/inputs/{contract.json,README.md,defects.json,dedup-search.log,counts-final.json}`, `rv2/results/{coverage-delta.md,final-Summary.txt,threshold-diff.txt,docs.diff,tree-final.txt,evidence-generate.txt,evidence-lint.txt,validator-manifest.txt}`,
  `rv2/coverage/{counter-final.log,measure-final.py}`, `rv2/logs/{D7c-F.log,D7c-T.log,D7c-skip-budget.log}`, frozen evidence
  `rv2/manifest.json` + `rv2/generations/<digest>/results/*.json` (28 receipts).

## ACT closure (cycle 8, EXIT)

CHECK returned PASS at attempt `n 2/3` (r2) on plan r2 / contract rv2: all 7 acceptance criteria met,
evidence closure 28/28 rows met, generate/lint/manifest rc 0, tree binding
`8f955dc77fad6c40923e6e66363ee36cdc429abd`, generation
`59bd0646ea7f80686f5a5d7350ec62b570f772bbfbc50f899989599c82e6c6e6`, 28 receipts. No loop-back.
ACT changed no code, test, doc or threshold; the only ACT write is this status file, so `commit` and
`push` stay false.

### Final numbers (accepted rv2 sweep)

- Counted branch: **27668/38317 = 72.208 %**, delta **+382** over 27286; floor 250 met with **+132**
  margin; target 27536 met with **+132**.
- Counted line: **47477/59713 = 79.509 %** (delta **+372** over 47105).
- Literal Summary: branch 27733/38407 = 72.2 %, line 47659/59949 = 79.4 %, methods 8219.
- Residual to 75 %: **1070 counted branches** (28738 − 27668); counted denominator drift **0**.
- Protected set: **EMPTY (0 files)**, protected delta **0** (`git diff --stat -- src` empty,
  `git status --porcelain -- src` empty).
- Suite (F): 4822 total / 4627 passed / 0 failed / 195 skipped / 0 not run / 90.116 s; skip budget
  195/37 matches the pinned matrix.
- Build: 0 warnings / 0 errors; guards 4/4.
- Hygiene: 6 files / 3367 lines / 77 live facts / 231 `.Should()` / 0 `Assert.` / 0 skips /
  0 InMemory / 0 suppressions / CRLF 3367/3367.

### Facts authored

6 new test files under `tests/nextorm.integration.tests/Coverage8/`; facts by unit: D1 17, D2 18,
D3 10, D4 10, D5 12, D6 10 (77 live facts = 4745 → 4822).

### Defects

- `CACHEDPREP-RAW-INDEX-GENERATION-RACE` — recorded, **not filed**: pre-existing test-side race under
  coverage instrumentation (observed 1/4822 in the red r1 sweep; 3/3 isolation pass; 734/0 with
  Coverage8); revisions observed r1 n1, r2 n1; applied fixes 0.
- `D4-SS-FETCH-WITH-TIES` → existing OPEN #298, comment `6251074151` (no duplicate).
- `D5-CH-DATE-DIFF-INT32` → existing OPEN #255, comment verified (no duplicate).
- Totals: filed 0 / duplicates 0 / applied fixes 0. Blockers #303/#304/#307/#308/#309 untouched.

### Docs

`AGENTS.md` and `.github/workflows/dotnet.yml` carry only the accepted measured numbers; thresholds
85/75/65/75 byte-identical (`rv2/results/threshold-diff.txt` rc 0); no `docs/**` edit.

### Deferred

- 38 carried rows (`rv1/inputs/deferred.json`, 17 carried + 21 cycle-7 new), unchanged by reference.
- 4 reopened rows, all `met`: `D2-GROUPING-CUBE`, `D2-GROUPING-SETS-LIVE` (unit D3), `D1-CH-SAMPLE`
  (D5), `D1-SS-INDEX-IGNORE` (D4). These 4 are a subset of the 38, so the register is **59 distinct
  rows** = 38 carried + 21 new, not 59 carried.
- 21 new cycle-8 rows (`rv2/inputs/deferred-cycle8.json`), including `C8-D3-GROUPING-SETS-WITH-TOTALS`
  (deferred: ClickHouse `WithTotals` is the only public trigger and `ClickHouseDialect` never sets
  `SupportsGroupingSets`).

### Accepted deviations

a. The red r1 acceptance F (1/4822, pre-existing CachedPreparation race under coverage instrumentation)
   is preserved and never credited. Exactly ONE planner-r2-authorized replacement F+T
   (`rv2/logs/D7c-F.log`, `rv2/logs/D7c-T.log`) was the only counted sweep; the red r1 sweep remains
   non-counting evidence.
b. The CHECK→DO loop-back unit D7e was evidence-contract closure only — no `.cs` edit and no
   build/test/coverage re-run.
c. `counts-final.json` was moved `rv2/results/` → `rv2/inputs/` because the evidence lint treats every
   `*.json` under `results/` as a receipt; the append-only 14:35 progress-log line keeps the old path
   as history.
d. `pdca_evidence.py generate` attempt 1 exited rc 2 on a missing evidence file (the lint/manifest run
   logs were not yet pre-created) and succeeded on the recorded second attempt; the rc-2 multi-attempt
   noise accepted in cycles 1–7 is recorded, not fabricated (`rv2/results/evidence-generate.txt`).

### Evidence map

- Frozen evidence: `rv2/manifest.json` (tree binding `8f955dc77fad6c40923e6e66363ee36cdc429abd`,
  generation `59bd0646ea7f80686f5a5d7350ec62b570f772bbfbc50f899989599c82e6c6e6`,
  28 receipts in `rv2/generations/59bd0646ea7f80686f5a5d7350ec62b570f772bbfbc50f899989599c82e6c6e6/results/`).
- Contract/validators: `rv2/inputs/contract.json` (28/28 rows met),
  `rv2/results/{evidence-generate.txt,evidence-lint.txt,validator-manifest.txt}` (rc 0).
- Counting: `rv2/inputs/counts-final.json` (moved path), `rv2/results/{coverage-delta.md,final-Summary.txt}`,
  `rv2/logs/{D7c-F.log,D7c-T.log,D7c-skip-budget.log}`.
- Red r1 history: `rv1/logs/D7-F.log`, `rv1/results/F-failures.txt`,
  `rv1/logs/{D7b-isolation-1.log,D7b-isolation-2.log,D7b-isolation-3.log,D7b-with-coverage8.log}`.
- Defects/deferred: `rv2/inputs/{defects.json,dedup-search.log,deferred-cycle8.json}`,
  `rv1/inputs/deferred.json`, `rv2/logs/D7d-deferred.log`.
- Docs/threshold/tree: `rv2/results/{threshold-diff.txt,docs.diff,tree-final.txt}`.

### Final durable state

```yaml
task: integration-branch-coverage
cycle: 8
plan_revision: 2
contract_revision: 2
phase: "ACT closed (EXIT)"
status: CLOSED
attempt: "n 2/3 (r2)"
baseline:
  branch: "27286/38317"
  line: "47105/59713"
  methods: 8191
final:
  counted_branch: "27668/38317 (72.208 %)"
  counted_line: "47477/59713 (79.509 %)"
  literal_branch: "27733/38407 (72.2 %)"
  literal_line: "47659/59949 (79.4 %)"
  methods: 8219
delta_branches: 382
margin_over_target: 132
residual_branches_to_75pct: 1070
denominator_drift: 0
thresholds: [85, 75, 65, 75]
gate_integration: RED
protected_files: 0
protected_delta: 0
product_edits: 0
test_files: 6
plan_facts: 77
fact_cap: 120
# 59 distinct = 38 carried (4 of them reopened, now met) + 21 new cycle-8 rows
deferred_rows: 59
commit: false
push: false
units:
  D0: done
  D1: done
  D2: done
  D3: done
  D4: done
  D5: done
  D6: done
  D7: done
  D7a: superseded
  D7b: done
  D7c: done
  D7d: done
  D7e: done
```

### Transferable lessons

1. Evidence-contract closure is a separate DO unit: run generate/lint/manifest and write the static
   receipts before declaring DO complete, not after CHECK.
2. A coverage-instrumentation-only flake must be classified with bounded isolation runs and a single
   planner-authorized replacement sweep, never retried silently.

## Progress log

2026-10-18T13:32:01Z | PLAN | revision r1 | iteration n 1/3 | plan r1 accepted by planner; D0 seal in progress | .pdca/status/integration-branch-coverage-8.md
2026-10-18T13:32:01Z | DO | revision r1 | iteration n 1/3 | D0 started | .pdca/integration-branch-coverage-8/rv1/
2026-10-18T13:35:33Z | DO | revision r1 | iteration n 1/3 | D0 seal complete: protected EMPTY (0 files, src diff empty); baseline archived (cobertura sha 3be5805b..., Summary sha ffc5bc72...) and counted reproduction PASS rc 0 (branch 27286/38317 = 71.211 %, line 47105/59713 = 78.886 %, methods 8191; literal report 27351/38407, 47287/59949; raw 19268/27814, 33945/44242); counter recipe .pdca/integration-branch-coverage-8/rv1/coverage/counter-recipe.txt (new script sha 3889cd85...); scope-D1..D6 validate_inner_loop.py brief rc 0 each; validators --help rc 0; api-map 42 rows: D1 reachable, D2 reachable with EF-first probe caveats (98-99/131-132 unreachable), D3 partial (243-250 unreachable on PG/SS, 276 no public chain; gain from 294-295 + 1351-1430), D4 reachable, D5 reachable, D6 partial (MySQL MakeTypeName numeric only; string/bool/DateTime/DTO/TimeSpan unreachable); 38-row deferred register copied verbatim (sha 9b86e30b...), reopened map recorded | .pdca/status/integration-branch-coverage-8.md, .pdca/integration-branch-coverage-8/rv1/inputs/{baseline.json,contract.json,api-map.md,api-map.json,deferred.json,deferred-reopened.json,scope-D1..D6.json}, .pdca/integration-branch-coverage-8/rv1/coverage/{baseline-integration.cobertura.xml,baseline-Summary.txt,counter-recipe.txt,counter.log,count.py}, .pdca/integration-branch-coverage-8/rv1/state/{git-head.txt,tree-d0.txt,protected.sha256,protected-verify.log,staged.txt,threshold-before.txt,worktree-count.txt,state-snapshot.json}, .pdca/integration-branch-coverage-8/rv1/logs/{D0-validators-help.log,D0-scope-brief.log}
2026-10-18T13:40:58Z | DO | revision r1 | iteration n 1/3 | D1 facts authored: 17 (cap 26); selected/passed/failed/skipped 17/17/0/0 (D1-run-2 exit 0; first run exit 1, 1 failed, test-side null overload, corrected); build nextorm.slnx exit 0, 0 warnings/0 errors; arms reached: CreateEntityCheck 42-56, ValidateSourceAsync 161-172, BuildEntityPredicates 174-202, BuildColumnPredicates 204-251, CanonicalPropertyKey/PropertyKeyOf/ResolveOnConcreteType 258-291, BuildSourceViolation 318-385, Omitted 414-420, Unsupported 425-427, UnsupportedFunc 435-437, NullValue 441-443, FilterKeyText 447-452 (all three arms), RequireValue 458-464, ParameterReferenceFinder 472-490, ColumnSubstitutionVisitor 501-538, ResultMemberRewriteVisitor 545-582, ConcreteMemberRewriteVisitor 589-604; deferred: ResolveInterfaceImplementation 293-316 and the MERGE validator calls MergeBuilder 134/139/161 — observed: an interface member read through a cast is not an entity-parameter read (column write fails closed as 'cannot validate', entity write validates), and SQLite rejects every filtered MERGE in EnsureFilteredFormSupported (NotSupportedException 'cannot isolate the write target') before any validator call | .pdca/integration-branch-coverage-8/rv1/inner/D1.report.json, rv1/logs/{D1-report.log,D1-build.log,D1-build-solution.log,D1-build-project.log,D1-run-2.log}, rv1/tests file tests/nextorm.integration.tests/Coverage8/Cycle8FilterValidationTests.cs
2026-10-18T13:53:31Z | DO | revision r1 | iteration n 1/3 | D2 facts 18 (cap 24); selected/passed/failed/skipped 18/18/0/0 (D2-run-3 exit 0; D2-run-2 17/17 green; D2-run-1 exit 1 with 16 passed/1 failed - the interface-typed HasQueryFilter(LambdaExpression) probe was rejected by EF before the bridge, so the fact was removed, not skipped); build nextorm.slnx exit 0, 0 warnings/0 errors (project build exit 0); arms reached: Translate 53-60 root walk + empty/non-empty declared set, 67-80 named/anonymous keys + context provenance, ResolveKey 85-92 anonymous/named and whitespace-key rejection, TranslateFilter 101-137 one entity parameter + context parameter + bool lambda, OwnerRewriter 154-168 DbContext owner constant (member re-read live) and supported literals, 171-181 system static member (string.Empty) vs static-member capture throw, 183-192 closure capture throw, 195-198 reference and collection navigation rejection, 201-217 EF.Property, EF.Functions (extension reference and DbFunctions object override), nested query, 234-242 IsRootedInEntity, 244-254 IsSystemDeclaringType/IsSupportedLiteral, 258-286 IsUnsupportedMemberType (primitive, string, byte[], IEnumerable, reference nav); deferred with observation: 98-99 no expression - HasQueryFilter(key, null) removes the slot and the live fact returns the unfiltered rows (probed); 131-132 non-bool body - EF EntityType.SetQueryFilter rejects any lambda without exactly one entity-typed parameter and a bool result (observed in D2-run-1); 70-72 duplicate named key - EF keeps one slot per key and the second declaration replaces the first (live fact proves the replacement); 106-108 + 118-123 + 141-144 EntityParameterAdapter/non-assignable parameter - NextOrmModelMapper.RegisterAndCollect:74-76 rejects every mapped inheritance hierarchy before Translate, and EF rejects an interface-typed lambda, so the adapter is unreachable through the public model (D2-run-1 log); 162 true arm needs a hand-built Expression.Constant(db, typeof(DbContext)) plus a DbContext-declared member (no source-level route); 220-221/225-226 invocation and QueryRoot are not expressible in a C# filter lambda; 165-166 needs a hand-built non-scalar constant | .pdca/status/integration-branch-coverage-8.md, .pdca/integration-branch-coverage-8/rv1/inner/D2.report.json, .pdca/integration-branch-coverage-8/rv1/logs/{D2-build-project.log,D2-build-solution.log,D2-run-1.log,D2-run-2.log,D2-run-3.log}, tests/nextorm.integration.tests/Coverage8/Cycle8EfFilterBridgeTests.cs
2026-10-18T13:58:00Z | DO | revision r1 | iteration n 1/3 | D3 facts 10 (cap 18); selected/passed/failed/skipped 10/10/0/0 (D3-run-3 exit 0 after the solution build; D3-run-2 also 10/10 green; D3-run-1 exit 1 with 9 passed/1 failed - the SQL Server empty-input row asserted PostgreSQL's grand-total semantics, corrected by asserting the measured per-engine difference, neither skipped nor removed); build nextorm.slnx exit 0, 0 warnings/0 errors (project build exit 0); arms reached: SqlBuilder 294-295 out-of-range grouping-set index (index == declared count and negative index, both range operands, BuildSqlCommandException before any SQL on live PG and MSSQL), 1351-1367 portable extreme-row outer parameter pass on a warmed plan (entity shape takes the selectList-null short-circuit plus the expression-sort walk; projection shape takes the select-list walk plus the ordinal-sort false arm; proven by QueryPlanner.FallbackRefreshes 0->1 per shape and ParamRefreshRecipe.FastBindHits 0), 1421-1453 parameter-mode rank-expression walk in the same pass; live CUBE (one and two columns with grand totals) and GROUPING SETS ([0]+() real-NULL-group vs subtotal, [0]+[1] multi-set, empty input) re-prove the reopened D2-GROUPING-CUBE / D2-GROUPING-SETS-LIVE capability and public route (already covered by Postgres/SqlServer RenderingBranchCoverage5 and Coverage6/PostgresRenderingBranchCoverage6, so no new line coverage); deferred with code-level observation: 243-250 unsupported grouping is unreachable on PG/SS (PostgresDialect.cs:289-293 and SqlServerDialect.cs:222-227 both report SupportsRollup/SupportsCube/SupportsGroupingSets true), 275-276 GROUPING SETS + WITH TOTALS has no public chain (WithTotals is ClickHouse-only and ClickHouseDialect never sets SupportsGroupingSets, so :272 rejects first); observed engine difference recorded: SQL Server returns no row for grouping sets over an empty input (raw probe "select count(*) from c8g_marker where id < 0 group by grouping sets ((grp), ())" -> no rows) while PostgreSQL returns one count-0 grand-total row | .pdca/integration-branch-coverage-8/rv1/inner/D3.report.json, .pdca/integration-branch-coverage-8/rv1/logs/{D3-build-project.log,D3-build-solution.log,D3-run-1.log,D3-run-2.log,D3-run-3.log}, tests/nextorm.integration.tests/Coverage8/Cycle8GroupingTests.cs
2026-10-18T14:03:35Z | DO | revision r1 | iteration n 1/3 | D4 facts 10 (cap 16); selected/passed/failed/skipped 10/10/0/0 (D4-inner-run2 exit 0 after the solution build; D4-inner-run1 exit 1 with 11 selected/10 passed/1 failed - the offset-bearing WITH TIES probe reproduced tracked defect #298 'Incorrect syntax near the keyword with' from the public Page(1,1).WithTies() shape, so the probe fact was removed, not skipped: SQL Server accepts WITH TIES only on TOP(n) WITH TIES, never on OFFSET/FETCH; dedup search 'with ties' -> #298 open, milestone 1.0.9-rc4, no duplicate filed); build nextorm.slnx exit 0, 0 warnings/0 errors (project build exit 0); arms reached: SqlServerIndexHintRenderer.RenderIndexHint index-only hint route and index-ignore rejection ('SQL Server has no index-ignore hint'), numeric MakeTypeName conversions (byte/short/long/float/double/decimal), MakeDateTrunc microseconds/milliseconds folds, MakeDateAdd decade/century/millennium year scaling, MakeDateAdd DTO year/month default field arm, TSqlDatePart milliseconds (date_diff/date_diff_big), MakeNow local getdate(), one-argument round, MakeCase boolean-CASE materialisation in projection and predicate contexts; deferred with observation: MakeTypeName DateTimeOffset (SqlServerDialect.cs:394) has no public SQL-generation call site on SQL Server (numeric-only conversion table; result-schema route unsupported), MakeDateTrunc decade/century/millennium throw 497-498 is rejected earlier by SupportsDateTruncField, MakeSubqueryPredicate non-exists/any/all fallthrough 653 has no public keyword, index-empty throw 919 is unreachable because the ignore kind throws first, and SqlServerDialect.cs:703/707 plus SqlSourceRenderer.cs:620 were already fully covered by the live suite; paging/with-ties fallback thus yielded a defect observation, not new coverage | .pdca/integration-branch-coverage-8/rv1/inner/D4.report.json, .pdca/integration-branch-coverage-8/rv1/defects/D4-SS-FETCH-WITH-TIES.md, .pdca/integration-branch-coverage-8/rv1/logs/{D4-build.log,D4-build-project.log,D4-inner-run1.log,D4-inner-run2.log}, tests/nextorm.integration.tests/Coverage8/Cycle8SqlServerResidualTests.cs
2026-10-18T14:09:07Z | DO | revision r1 | iteration n 1/3 | D5 facts 12 (cap 14); selected/passed/failed/skipped 12/12/0/0 (D5-inner-run2 exit 0; D5-inner-run1 exit 1 with 12 selected/9 passed/3 failed - two test-side assertion corrections retained and corrected, not skipped: ClickHouse toLastDayOfMonth materialises a date-only DateTime, and date_trunc requires a literal field string so the three unsupported-field probes became explicit calls; the third failure is tracked product defect D5-CH-DATE-DIFF-INT32 / issue #255 'ClickHouse: date_diff returns Int64 but is declared int?' (dedup search 'date_diff clickhouse'/'dateDiff Int64'/'date_diff Int32' -> #255 open, milestone 1.0.9-rc4; comment posted with the independent cycle-8 live repro, no duplicate; the fact now asserts the same MakeDateDiff hook through date_diff_big (long?) instead of carrying a red fact)); build nextorm.slnx exit 0, 0 warnings/0 errors (project build exit 0); arms reached: SupportsSample 494 via public FromOptions.Sample over a class-owned MergeTree with sample by sid (ratio 1.0 = every row + rendered SQL contains 'sample'; 0.5 and 0.5/0.1 = seeded-row subset only), MakeCreateTableAsSelect 115-120 (rendered 'engine = MergeTree order by tuple() as' + live ToTable read-back), MakeConcat 123 (+ and string.Concat), MakeColumnReference/MakeParam/MakeCoalesce/MakeBool 546-555, MakeDateTrunc 673-685 (day + milliseconds/microseconds folds; decade/century/millennium NotSupportedException at build time), full MakeDateAdd 691-713 field table incl. decade/century/millennium scaling + unknown-field throw, DateTimeOffset scaled MakeDateAdd 738-757 (day/hour/minute/second/millisecond factors + month normalized fallback), MakeDateDiff 760-770 via date_diff_big, MakeEndOfMonth 785, MakeDateFromParts 788-789, MakeStringAgg/MakeFilteredStringAgg 792-797, MakeDurationType/MakeNullableDurationType 816-820; deferred: MakeJoinKeyword 177-217 and grouping/totals 430/513 not needed because primary arms were reachable and both are already executed live by ClickHouseIntegrationTests/RenderingBranchCoverage5Tests/ClickHouseDateAndSessionExecutionTests; defect history: D5-CH-DATE-DIFF-INT32 r1 n1, fixes applied 0, evidence D5-run-1.log, tracked #255 | .pdca/integration-branch-coverage-8/rv1/inner/D5.report.json, .pdca/integration-branch-coverage-8/rv1/defects/D5-CH-DATE-DIFF-INT32.md, .pdca/integration-branch-coverage-8/rv1/logs/{D5-build.log,D5-build-project.log,D5-run-1.log,D5-run-2.log}, tests/nextorm.integration.tests/Coverage8/Cycle8ClickHouseResidualTests.cs
2026-10-18T14:13:39Z | DO | revision r1 | iteration n 1/3 | D6 facts 10 (cap 16); selected/passed/failed/skipped 10/10/0/0 (D6-run-1 exit 0 on the first execution; two test-side compile shapes - target-typed new with AddTicks and an Equal(reason) overload - corrected before the recorded build, no fact removed or skipped); build nextorm.slnx exit 0, 0 warnings/0 errors (acceptance; project build exit 0); arms reached: PostgreSQL RenderQueryHints 242-269 (top-level select + CTE quoted-name/underscore/paren/single-quote scan with the literal "select from here"), MakeMathFunction 499-516 (log->ln, floating-point round cast, pow), MakeDateAdd DateTimeOffset 413-416 + plain delegate, MakeDateDiffBig 422-423, MakeFullText 431-434 (contains/freetext); MySQL MakeDefaultValues 38 (all-generated entity -> "() values ()") + MakeLastInsertId 31 (ReturningIdentity -> last_insert_id read-back), MakeDateAdd 247-268 (decade/century/millennium year scaling), MakeDateAdd DateTimeOffset 287-304 (day->hour*24 + month no-factor fallback), MakeDateDiff 309-326 (microseconds factor 1, milliseconds fold /1000 cast as signed, week/quarter/year), MakeEndOfMonth 330, MakeDateFromParts 333-334, MakeTextJsonFunction 217-222 (json_value/json_query/json_modify live, absent path NULL), MakeTypeName 386-393 (byte/short/int/long/float/double/decimal); deferred with observation: MakeDateAdd/MakeDateDiff _ => throw (262,321) unreachable because BuiltinFunctionTranslator validates SupportsDateAddField/SupportsDateDiffField first and rejects fortnight (rejection asserted), MakeTypeName uint/int? plus string/bool/DateTime/DTO/TimeSpan arms and the type.Name fallback have no public conversion call site (TypeFacts.TryGetNumericConversion numeric targets only, uint excluded), IndexHints 531 fallback not needed, PostgreSQL ordered fallbacks MakeDeleteJoin/TableSample/CTAS not needed (MakeDeleteJoin already live-covered by Coverage6/PostgresRenderingBranchCoverage6Tests); no product defect observed, no issue filed | .pdca/integration-branch-coverage-8/rv1/inner/D6.report.json, .pdca/integration-branch-coverage-8/rv1/logs/{D6-build-project.log,D6-build-solution.log,D6-run-1.log}, tests/nextorm.integration.tests/Coverage8/Cycle8PostgresMySqlResidualTests.cs
2026-10-18T14:22:00Z | DO | revision r1 | iteration n 1/3 | D7a boundary STOP on red sweep: build nextorm.slnx rc 0, 0 Warning(s)/0 Error(s) (D7-build.log); guards IntegrationSuiteScopingTests|AssertionStyleTests rc 0, 4 total/4 passed/0 failed/0 skipped (D7-guard.log); hygiene PASS on the 6 Coverage8 files (67 Fact + 5 Theory MemberData x2 engines = 72 declarations, 231 .Should(), 0 Assert., 0 skip, 0 InMemoryDataContext, 0 #pragma/SuppressMessage/NoWarn, 3367/3367 CRLF lines, 0 LF-only); preflight HEAD 9ffb8bdc, git diff --stat -- src EMPTY, git status -- src EMPTY, 0 untracked/modified src files, protected.sha256 EMPTY marker re-verified (sha256sum -c rc 1 expected), status delta vs tree-d0 = only ?? tests/nextorm.integration.tests/Coverage8/ (D7-preflight.log); **F rc 1** (D7-F.log, fixed command, NOT retried): Total 4822 / Passed 4626 / Failed 1 / Skipped 195 / Not Run 0 / 92.631 s; single failure NextORM.Integration.Tests.MySqlIntegrationTests.CachedPreparation_StreamingAndNonStreamingResultsMatch - "Expected CachedPreparationRawCandidateIsLive(... Where(x => x.Id > 7) ...) to be True ... but found False" at tests/nextorm.integration.tests/CommonTestSuite/Caching/CommonTestSuite.CachedPreparation.cs:141 (pre-existing tracked, unmodified common fact; thread-local QueryPlanStore raw-index oracle read after the buffered MySQL terminal, i.e. an async thread-affinity assumption; all six Coverage8 classes passed, total 4745 -> 4822 = +77 exactly); skip budget rc 0 OK 195 skips match pinned 195/37 (D7-skip-budget.log); T (reportgenerator) and the counted delta vs 27286/38317 were NOT executed per the brief stop rule - cobertura from the red run kept as non-counting evidence sha  baaa8225...; blocked pending orchestrator decision (rerun / recorded flake / new unit for the thread-affinity fact) | .pdca/integration-branch-coverage-8/rv1/logs/{D7-build.log,D7-guard.log,D7-hygiene.log,D7-preflight.log,D7-F.log,D7-skip-budget.log}, .pdca/integration-branch-coverage-8/rv1/results/{test-hygiene.md,F-failures.txt}
2026-10-18T14:24:17Z | PLAN | revision r2 | iteration n 1/3 | Replanned: red r1 acceptance F (1/4822, CachedPreparation raw-index generation race, classified pre-existing, not filed); exactly ONE replacement F+T authorized | rv2/
2026-10-18T14:35:00Z | DO | revision r2 | iteration n 1/3 | D7c replacement boundary sweep ACCEPTED: F rc 0 (Total 4822/Passed 4627/Failed 0/Skipped 195/NotRun 0/90.116 s), T rc 0; counted branch 27668/38317 (72.208 %, +382 over 27286, floor 250 met), line 47477/59713 (79.509 %, +372), residual 1070 to 75 %; denominators pinned/drift 0; skip budget 195/37 rc 0; protected EMPTY delta 0; docs+threshold proof done (diff rc 0); defects: CACHEDPREP race recorded not filed, #298 comment posted, #255 comment verified; CHECK entry: D0-D7c closed; C in progress; ACT pending | rv2/results/{coverage-delta.md,counts-final.json,threshold-diff.txt}, rv2/logs/D7c-F.log, .pdca/status/integration-branch-coverage-8.md
2026-10-18T14:59:36Z | DO | revision r2 | iteration n 1/3 | D7d deferred register written and reconciled (reopened: 4 met / 0 still-deferred; new rows: 21); DO closed; CHECK entry recorded | rv2/inputs/deferred-cycle8.json
2026-10-18T15:09:52Z | DO | revision r2 | iteration n 2/3 | D7e evidence-contract closure: 28 rows met, static receipts written, generate/lint/manifest rc 0; no test/coverage rerun | rv2/manifest.json, rv2/results/{evidence-generate.txt,evidence-lint.txt,validator-manifest.txt}
2026-10-18T15:12:51Z | ACT | revision r2 | iteration n 2/3 | CHECK PASS (28/28 rows met; generate/lint/manifest rc 0); cycle closed: counted 27668/38317 (+382, residual 1070), suite 4822/4627/0/195; no commit/push | rv2/manifest.json
