# D169 / issue #169 — Table hints: TablesInScopeHints dropped on multi-table DELETE/UPDATE join paths

- Collection: `collection-1.0.9-rc2`; variant: `pdca-dotnet`; milestone: `1.0.9-rc2` (#20)
- Base: branch `1.0.9-rc2` @ `18659e41`; mode: autonomous, auto-commit authorized, push never
- Task id: `D169`; issue: `#169`
- Cycle N = 1; plan revision r = 1; CHECK attempt n = 2; evidence contract rv = 1
- plan_state: `done`; task status: `done` (CHECK PASS r=1 / rv=1 / n=2/3; ACT committed)
- Status file: `docs/specs/status/rc2-169-dml-scope-hints-1.md`
- Artifact root: `/tmp/nextorm-rc2-169-r1`

## Progress log
- `PLAN(r=1) authored by planner; persisted by coder` — plan_state ready, awaiting DO start.
- DO not started. No worktree/branch created (collection phase P).
- `2026-10-09T05:46:08Z | DO | revision r=1 | iteration 1/3 | DO started | /tmp/nextorm-rc2-169-r1/gate.md`
- Gate-prep facts: base commit `f1c7d696` (worktree HEAD, branch `1.0.9-rc2`); plan r=1 / rv=1 / N=1, first CHECK attempt n=1; artifact root `/tmp/nextorm-rc2-169-r1`; authority = autonomous, auto-commit authorized, push never; predecessor hint APIs present (`FromRenderOptions.cs:27` `TableHints`, `:31` `TablesInScopeHints`; `SqlBuilder.cs:1696` `AppendHint`, `:1713` `CollectInlineHints`, `:1745` `MergeHints`); verified issue URL `https://github.com/AlexeyShirshov/nextorm/issues/169`.
- Durable state: Current cycle N=1; Plan revision r=1; Attempt n=1. Defect ledger: none yet.
- D169.1 started: baseline + red scenarios.
- `2026-10-09T05:51:42Z | DO | revision r=1 | iteration 1/3 | D169.0 complete (gate.md) | /tmp/nextorm-rc2-169-r1/gate.md`
- D169.0: `gate.md` written with base `f1c7d696`, predecessor #130 API locations, defect map, and provider capability/placement map (SQL Server structural; PostgreSQL/MySQL inline; SQLite/ClickHouse neither). Scope brief `brief` validation exit 0. Design decision settled: option (a) inline comment rendered in `SqlBuilder`, no provider-dialect edits.
- D169.1 baseline: `CMD-P` exit 0, wall 66s, 7 acceptance cases Mean/Allocated recorded (`perf/baseline/cmd-p.log`, `perf/baseline/summary.json`). Supplemental non-acceptance `DmlScopeHintBenchmark` (6 cases) exit 0, wall 39s, unfixed baseline in `perf/baseline/supplemental-dml-scope-hint.log`. Tracked BDN artifacts restored via `git checkout`; no tracked artifact diff remains.
- `2026-10-09T05:51:42Z | DO | revision r=1 | iteration 1/3 | D169.1 red evidence | /tmp/nextorm-rc2-169-r1/red/`
- D169.1 red (`--filter FullyQualifiedName~DmlScopeHint`, unfixed source): postgres exit 2 (5 total / 4 failed / 1 guard passed), sqlite exit 2 (2 / 1 failed / 1 guard passed), sqlserver exit 2 (3 / 3 failed), mysql exit 2 (2 / 2 failed), clickhouse exit 0 (1 / 1 guard passed). Failure reasons match the defect: inline comment absent (PG/MySQL), structural `WITH` absent (SQL Server), `NotSupportedException` not thrown (SQLite). Logs `red/<provider>-dmlscopehint.log`, `red/summary.json`.
- Defect ledger: `DEF-169-01` scope hints dropped on joined DELETE/UPDATE (PG inline, SQL Server structural, MySQL inline) — observed r=1/n=1, fixes applied=0; `DEF-169-02` unsupported-dialect rejection missing for nonempty scope hints (SQLite) — observed r=1/n=1, fixes applied=0. Evidence pointers: `red/summary.json`, `gate.md`.
- Next (D169.2): propagate `TablesInScopeHints` through `MakeDeleteJoin`/`MakeUpdateJoin` target + join branches; add SELECT-consistent capability rejection; render PG/MySQL inline comment in `SqlBuilder`.
- `2026-10-09T05:59:21Z | DO | revision r=1 | iteration 1/3 | D169.2 fix applied, previously-red filters green | /tmp/nextorm-rc2-169-r1/green/`
- D169.2: `SqlSourceRenderer.MakeJoinParts` gained `tablesInScopeHints` (named-arg forward; `join.TableHints`/conditions preserved). `SqlBuilder.MakeDeleteJoin`/`MakeUpdateJoin` now compute `scopeHints` (structural-capable only), reject nonempty scope hints on dialects with neither inline nor structural support (mirroring `MakeSelect:100-103`), collect+merge inline hints (`CollectInlineHints`/`MergeHints`), and forward `scopeHints` into the target `FromRenderOptions` and every `MakeJoinParts`/`MakeJoin` call. New private `ApplyInlineHints` inserts the single `/*+ ... */` comment after the leading `DELETE`/`UPDATE` keyword; no provider-dialect edits. Hint-free DML byte-identical (guards pass).
- D169.2 green (`--filter FullyQualifiedName~DmlScopeHint`, fixed source; `--no-build` after per-project build): postgres exit 0 (6/6), sqlite exit 0 (2/2), sqlserver exit 0 (4/4), mysql exit 0 (3/3), clickhouse exit 0 (1/1). Class filters also green: postgres update 21 / delete 24, sqlite 6 / 7, sqlserver 7 / 12, mysql 5 / 7, clickhouse delete 5. Logs `green/*.log`. `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s).
- `2026-10-09T05:59:21Z | DO | revision r=1 | iteration 1/3 | D169.3 variants + mutation + perf after | /tmp/nextorm-rc2-169-r1/variants.md`
- D169.3 variants: V01–V12 and V15 closed per `variants.md` (new: PG/MySQL inline composition with join hint, SQL Server hinted→unhinted→hinted parity/leakage). V11 target-only table hints deliberately left unchanged (P2 deferred); V15 proven non-reachable (sole callers `QueryPlanner:461`/`:510`; temp-table/TVP route through `MakeSelect`).
- D169.3 mutation ledger `mutation.md`: M1 structural-off → sqlserver 3 fail; M2 inline-off → postgres 5 / mysql 3 fail; M3 validation-off → sqlite 1 fail. Each reverted immediately; final `SqlBuilder.cs` SHA1 `4d6d5fe6560ce18a30ca25f02a7325867a952fd2` matches pre-mutation. No `MUTATION-` residue.
- D169.3 perf after `perf/after/`: `CMD-P` exit 0, wall 52s, 7 cases within baseline noise; supplemental `DmlScopeHintBenchmark` exit 0, wall 44s, 6 cases — no-hint arms allocate identically, hinted arms grow ~38–48 KB because the hint is now emitted (expected). Cached/prepared SELECT arms unchanged. Tracked BDN artifacts restored.
- `2026-10-09T05:59:21Z | DO | revision r=1 | iteration 1/3 | D169.4 docs done | docs/guide/13-query-hints.md`
- D169.4: EN+RU `guide/13-query-hints.md` gained "Tables-in-scope hints on joined DELETE/UPDATE" (structural vs inline, placement, unsupported rejection, hint-free unchanged). EN/RU `advanced/limitations.md` reviewed; no contradicting statement, so unchanged. `roadmap/todo_hint-followups.md` section D kept (finalization is ACT). No public `docs/specs/**` links added.
- `2026-10-09T05:59:21Z | DO | revision r=1 | iteration 1/3 | inner-loop report validated | /tmp/nextorm-rc2-169-r1/evidence.json`
- `validate_inner_loop.py report evidence.json` exit 0. D169.2–D169.4 complete. Boundary (D169.5 CMD-I + CMD-BOUNDARY + coverage + CHECK) not run in this dispatch. Defect ledger updated: `DEF-169-01` fixes applied=1 (structural+inline propagation), `DEF-169-02` fixes applied=1 (capability rejection); both closed in DO, pending CHECK.
- `2026-10-09T06:04:25Z | DO | revision r=1 | iteration 1/3 | D169.5 boundary sweep | /tmp/nextorm-rc2-169-r1/boundary/`
- D169.5 builds: `dotnet build nextorm.slnx -c Release` exit 0 (0 Warning(s) 0 Error(s)); `dotnet build nextorm.slnx -c Debug` exit 0 (0 Warning(s) 0 Error(s)). Logs `boundary/build-release.log`, `boundary/build-debug.log`.
- D169.5 CMD-I (DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock, Podman 5.8.6 connected, 5 reusable containers started): exit 0, Total 3555, Errors 0, Failed 0, Skipped 197. Every skip is a per-test capability reason (LOB/CTAS/TVP/lateral/etc.); no provider-unavailable skip (`PostgreSQL/MySQL/SQL Server/ClickHouse is not available.` absent). ClickHouseIntegrationTests had 0 skips (provider available). Log `boundary/integration.log`.
- D169.5 CMD-BOUNDARY + coverage: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` exit 0, solution-wide run Total 9778, Failed 0, Succeeded 9580, Skipped 198 (real DBs via DOCKER_HOST). `reportgenerator` exit 0. Aggregate line 88.3% (≥85) and branch 80.3% (≥75) — PASS. Changed types: `SqlBuilder` line 91.9% / branch 83.8%, `SqlSourceRenderer` line 95.2% / branch 88.9%; changed methods `MakeDeleteJoin` 83% branch, `MakeUpdateJoin` 85%, `CollectInlineHints` 88%, `MergeHints` 100%, `ApplyInlineHints` 75%, `MakeJoinParts` 88%. Logs `boundary/coverage-collect.log`, `boundary/coverage-report.log`, `boundary/coverage-summary.txt`, `boundary/coverage-changed-branch.txt`. `coverage.settings.xml` preserved.
- D169.5 diff identity: full tracked diff sha256 `d63f8f7bdbe799f55d48f0e6ac3750b1e3d5510c02d6131a1ed86ffb873ea8e6`; src-only diff sha256 `02d038f790a8b8f5ad2730665cc154af6084ab7c61c5e2f5488de254c79a02f6`; `SqlBuilder.cs` SHA1 `4d6d5fe6560ce18a30ca25f02a7325867a952fd2` (unchanged from post-fix); no `MUTATION-` residue. Diffs `boundary/final-wt.diff`, `boundary/final-src.diff`.
- DO complete through D169.5. Handoff to CHECK: evidence `evidence.json` (inner), `red/`, `green/`, `mutation.md`, `variants.md`, `perf/baseline|after/`, `boundary/`. Open item for CHECK: V11 target-only `TableHints` intentionally not forwarded (plan P2 deferral, brief said "(and TableHints)").
- `2026-10-09T06:14:16Z | DO | revision r=1 | iteration n=2/3 | CHECK FAIL r=1 n=1 corrections applied | /tmp/nextorm-rc2-169-r1/variants.md`
- CHECK attempt n=1 returned FAIL (completeness/variant gate). Revision unchanged (r=1); this is iteration n=2/3 of the same revision, no replan. Defect family: "CHECK completeness/variant gate".
- Resolved audit findings (behavior NOT changed, per re-gather): Finding 1 (CTE inline placement) is correct — `WITH … delete /*+ … */` mirrors SELECT; only a discriminating test was added. Finding 3 (`source.Hints` statement-level query hints on SQL Server DML) is a pre-existing gap outside #169 — recorded deferred, scope not expanded.
- Corrections applied: (1) V10 rewritten as `UpdateJoin_DmlScopeHint_SameContextRepeatedCalls_ShouldNotLeakCachePolicy` (sqlserver:111) asserting identical hinted SQL, hint-free parity and `command.Source.Cache == true`; (2) DELETE composition + rejection: PG `DeleteJoin_DmlScopeHint_ShouldComposeWithJoinHintInOneComment` (postgres:335) and PG `UpdateJoin_DmlScopeHint_UnsupportedJoinTableHint_ShouldThrow` (postgres:378), MySQL `DeleteJoin_DmlScopeHint_ShouldComposeWithJoinHintInOneComment` (mysql:87), SQL Server `DeleteJoin_DmlScopeHint_ShouldMergeWithJoinTableHint` (sqlserver:151); (3) V06 vacuous tests rewritten as `..._EmptyArguments_ShouldThrowArgumentException` (postgres:325, sqlite:93); (4) Finding 6 CTE tests (`..._FromCte_ShouldPlaceCommentAfterUpdateVerb` postgres:357, `..._FromCte_ShouldPlaceCommentAfterDeleteVerb` postgres:351 + mysql:102); (5) `MakeJoin` XML doc for `tablesInScopeHints` (SqlSourceRenderer.cs:178); (6) EN/RU guide CTE wording corrected.
- n=2 inner evidence (per-project build 0/0, then `--no-build`): `~DmlScopeHint` postgres exit 0 (10/10), sqlite 0 (2/2), sqlserver 0 (5/5), mysql 0 (5/5), clickhouse 0 (1/1); core `~TablesInScopeHint` 0 (2/2); class filters postgres 23+26, sqlite 6+7, sqlserver 7+13, mysql 5+9, clickhouse delete 5 — all exit 0. Logs `green/`. `evidence.json` refreshed (amendment for the core selector) and `validate_inner_loop.py report` exit 0.
- E169 ledger mapping added to `variants.md`; E169-12 (`docs-review.md`) remains CHECK-owned. Boundary evidence from n=1 (`boundary/`) unchanged and still applicable (no behavior change in n=2, only a doc comment). No commits.
- `2026-10-09T06:16:50Z | DO | revision r=1 | iteration n=2/3 | boundary refresh (test/docs-only delta) | /tmp/nextorm-rc2-169-r1/boundary-n2/`
- n=2 boundary builds: `dotnet build nextorm.slnx -c Debug` exit 0 (0/0); `-c Release` exit 0 (0/0). Logs `boundary-n2/build-debug.log`, `boundary-n2/build-release.log`.
- n=2 covered boundary: `dotnet-coverage collect … "dotnet test --no-build --verbosity normal"` exit 0 (DOCKER_HOST set) — Total 9785, Failed 0, Succeeded 9587, Skipped 198 (all capability). `reportgenerator` exit 0. Aggregate line 88.3% (≥85) / branch 80.3% (≥75) — PASS. Changed types `SqlBuilder` line 91.9% / branch 83.8%, `SqlSourceRenderer` line 95.2% / branch 88.9%; changed methods `MakeDeleteJoin` 83% branch, `MakeUpdateJoin` 85%, `CollectInlineHints` 88%, `MergeHints` 100%, `ApplyInlineHints` 75%, `MakeJoinParts` 88%. Logs `boundary-n2/coverage-collect.log`, `coverage-report.log`, `coverage-summary.txt`, `coverage-changed-branch.txt`; `coverage.settings.xml` preserved.
- CMD-I carried from n=1 (unchanged product behavior; n=2 src delta is an XML doc comment only): Total 3555, Failed 0, 197 capability skips, 0 provider-unavailable skips, all providers ran — `boundary/integration.log`. Not re-run.
- n=2 diff identity: full tracked sha256 `ea8f9a97d2ea76fad9d8528ef1b923b19be0723391bd15196b28028e256ffd3e`; src-only sha256 `d26e00de27d6033286bf49bdc50989ee9e7420c0406ab5d4179f56a6c124f478`; no `MUTATION-` residue. Diffs `boundary-n2/final-wt.diff`, `final-src.diff`. Ready for CHECK attempt n=2.

---

## ACT — finalization (CHECK PASS, r=1 / rv=1 / n=2/3)

- `2026-10-09T06:24:14Z | ACT | revision r=1 | iteration n=2/3 | CHECK PASS → finalize + commit | task commit bbc3b47e79e2659f52ccfbbcd489ae70b3df6c9a`
- Verdict: **CHECK PASS** (r=1 / rv=1 / n=2/3). Task state `done`; plan revision unchanged (no replan).

### R169 acceptance matrix (final)
| Req | Result | Evidence |
|---|---|---|
| R169-01 Supported propagation | PASS | PG/MySQL inline + SQL Server structural target+join tests (`green/*-DmlScopeHint.log`) |
| R169-02 Unsupported handling | PASS | SQLite `..._ShouldThrowBecauseNotSupported`, ClickHouse capability guard |
| R169-03 Composition | PASS | V07/V08 update+delete composition, single comment/clause |
| R169-04 Compatibility/cache | PASS | hint-free byte-identical; `..._SameContextRepeatedCalls_ShouldNotLeakCachePolicy`, `Source.Cache == true` |
| R169-05 Evidence/delivery | PASS | builds 0/0; boundary 9785/0/9587/198; coverage 88.3/80.3; EN+RU docs; no provider-unavailable skip |

### Evidence / artifact paths
- Inner: `/tmp/nextorm-rc2-169-r1/evidence.json` (`validate_inner_loop.py report` exit 0), `red/`, `green/`.
- Boundary n=1: `/tmp/nextorm-rc2-169-r1/boundary/` (CMD-I `integration.log`: Total 3555 / Failed 0 / 197 capability skips / all providers ran).
- Boundary n=2: `/tmp/nextorm-rc2-169-r1/boundary-n2/` (builds 0/0; covered sweep Total 9785 / Failed 0 / 9587 / 198; reportgenerator exit 0).
- Coverage: aggregate line 88.3% / branch 80.3% (≥85/75); `SqlBuilder` 91.9% / 83.8%; `SqlSourceRenderer` 95.2% / 88.9%.
- Mutation ledger: `/tmp/nextorm-rc2-169-r1/mutation.md` (M1 structural-off / M2 inline-off / M3 validation-off all detected; restored SHA1 `4d6d5fe6…52fd2`).
- Perf: `/tmp/nextorm-rc2-169-r1/perf/{baseline,after}/`.
- Docs review: `/tmp/nextorm-rc2-169-r1/docs-review.md`; variants: `variants.md`.
- Diff identity: full tracked sha256 `ea8f9a97d2ea76fad9d8528ef1b923b19be0723391bd15196b28028e256ffd3e`; src-only `d26e00de27d6033286bf49bdc50989ee9e7420c0406ab5d4179f56a6c124f478`; `SqlBuilder.cs` SHA1 `4d6d5fe6…52fd2`; no `MUTATION-` residue.

### Deferred / out of scope
- V11 target-only table hints (P2, plan deferral; only `TablesInScopeHints` forwarded).
- V12 INSERT…SELECT (unaffected); V13 APPLY/PIVOT; V14 temporal/index; V15 temp-table/TVP (proven non-reachable).
- Out of scope: `source.Hints` statement-level query hints on SQL Server DML — pre-existing gap, not part of #169 (`TablesInScopeHints` only).

### Delivery
- Task commit: `bbc3b47e79e2659f52ccfbbcd489ae70b3df6c9a` (branch `1.0.9-rc2`, never pushed).
- Issue #169: **closed** (`gh issue close 169`, 2026-10-09).
- Roadmap: `docs/specs/roadmap/todo_hint-followups.md` section D marked ADDRESSED.
- Bookkeeping commit: recorded in `docs/specs/status/collection-1.0.9-rc2.md`.

--- PLAN BODY (verbatim) ---

# D169 / #169 — PLAN

**Collection:** 1.0.9-rc2 · **r=1, N=1, first CHECK attempt n=1** · **contract rv=1**
**Proposed status file:** `docs/specs/status/rc2-169-dml-scope-hints-1.md`

This is a plan, not authorization to modify code. `coder` must persist it before DO and before requesting `go` in normal mode. Autonomous execution requires recorded autonomous authority and completion of gate 1.

## 1. Goal, scope and acceptance

### Goal
Prevent query-wide table hints from being silently lost when rendering joined DELETE/UPDATE commands. Supported hints must reach the appropriate DML rendering mechanism; unsupported hints must fail explicitly.

### Scope
Fix **scope-hint propagation end-to-end in both DML renderers**, not merely the missing argument at `SqlSourceRenderer.cs:299`:

- Joined sources rendered through `MakeJoinParts`.
- Target/physical sources and joined sources rendered through `MakeJoin`.
- PostgreSQL/MySQL inline-hint collection and merging.
- Capability-based rejection.
- Preservation of existing joined-table hints, cache behaviour and hint-free SQL.

No public API changes, new operators, dialect capability changes, or general repair of temporal/index/APPLY/PIVOT metadata.

### Acceptance criteria

| Requirement | Observable acceptance | Negative case |
|---|---|---|
| **R169-01 Supported propagation** | DELETE/UPDATE retain scope hints on every **applicable physical source**, or in the provider’s statement-level inline comment. Target and joined sources are covered. | A scope hint must not disappear from either branch or be attached only to a target alias/pseudo-source. |
| **R169-02 Unsupported handling** | SQLite and other incapable dialects reject nonempty scope hints consistently with SELECT. | No silent omission or newly accepted unsupported SQL. Hint-free DML continues working. |
| **R169-03 Composition** | Single/multiple scope hints compose with supported join-table hints using existing provider rules. Inline hints appear once in the proper statement position. | No duplicated clauses/comments, lost join hint, or accidental application of a join-only hint to another table. |
| **R169-04 Compatibility/cache** | Hint-free SQL and JOIN conditions remain unchanged. Cached/prepared execution preserves the requested hints without sticky shared-command state changes. | A hinted call must not alter a subsequent unhinted call or disable caching on the shared command. |
| **R169-05 Evidence and delivery** | Build, applicable provider tests, real-provider boundary sweep, coverage assessment, performance evidence and EN/RU documentation are complete. | A skipped provider, empty test selection, absent log or unmeasured performance claim is not passing evidence. |

Scope means **rendering**, not proof that an optional PostgreSQL/MySQL optimizer extension honours a particular hint.

## 2. Minimal solution

**Three answers**

1. **Essential change:** route the existing scope-hint collection through DELETE/UPDATE, respecting structural versus inline provider behaviour.
2. **Non-negotiable constraints:** preserve capability flags, existing SQL without hints, JOIN semantics and shared-command state; no speculative metadata propagation.
3. **Optimum within those constraints:** reuse existing hint preparation/rendering mechanisms in both DML entry points, passing validated structural scope hints to physical-source rendering and using existing inline collection/merge semantics where applicable.

| Alternative | Benefit | Cost / risk | Decision |
|---|---|---|---|
| Add only `TablesInScopeHints` at `MakeJoinParts:299` | Smallest textual diff | PostgreSQL consumes scope hints through inline collection, absent from DML; SQLite is unsupported. This does not establish the issue’s observable outcome. | Reject |
| Repair only PostgreSQL/SQLite `MakeJoinParts` routes | Narrow provider footprint | Leaves identical DML losses in target rendering and SQL Server/MySQL `MakeJoin` branches. | Reject |
| Repair scope hints across both DML renderers and both branches | Complete, consistent scope-hint behaviour; two existing consumers | Requires provider-specific composition and placement tests | **Choose** |

Do not create a new general hint framework. A shared helper is permitted only if the two DML consumers genuinely need it and it reduces concepts.

## 3. Missing information and prerequisites

These are explicit gate dependencies, not permission to guess.

| Gap | Closure / disposition |
|---|---|
| Exact global `pdca-dotnet` **Versioned evidence contract** text is absent | Orchestrator supplies that section before DO. Validate the concrete contract below against it; retain every additional unconditional obligation. A mismatch returns to PLAN. |
| Collection base, allocation and verified issue URL are absent | Collection controller supplies base commit, permitted tree/group, verified #169 URL and predecessor-result ledger. No inferred repository URL or branch. |
| Whether D161/#130 has shipped | Verify its recorded result and required hint APIs on the assigned base. If unavailable, D169 remains active but blocked on that predecessor; do not recreate or supersede it. |
| Exact DML inline placement, target-source shape and table-hint restrictions | Targeted Roslyn/source scout of the existing renderers/dialects before production edits. Reuse actual provider-supported hint examples. |
| Actual test symbols, cache-test hooks and acceptance benchmark membership | Scout supplies existing symbols and locations. Planned tests are scenarios below, not invented symbols. |
| Literal CI coverage commands/tool availability and priority-register additions | Scout reports `.github/workflows/dotnet.yml`, local tool configuration and applicable project registers. Preserve CI inclusion settings and thresholds. |
| Integration infrastructure | Load the integration skill; verify/start Podman as required. Provider skips do not close this dependency. |

**Working assumptions:** existing capability flags are authoritative; D169 changes rendering rather than per-row binding; existing hint validation/composition semantics should be reused. Contradictory evidence requires PLAN review, not silent reinterpretation.

No speculative implementation spike is needed. The targeted reconnaissance above **is required**: its observable result is a provider-by-provider placement/source map, existing test and benchmark symbols, and resolved execution prerequisites. If legal DML placement remains uncertain, add a small SQL-generation/provider-execution experiment before production changes.

## 4. Concrete tasks and footprint

Execute these units sequentially in the collection-assigned tree.

- **D169.0 — Gate preparation / fix now.** Obtain the missing facts above; record base, predecessors, authority, contract and artifact locations. Persist the plan. Do not edit production code while these dependencies remain unresolved.
- **D169.1 — Baseline and red evidence / fix now.** Record clean/base diff identity and acceptance benchmark baseline. Add regression scenarios; demonstrate supported hints currently disappear and unsupported DML handling is inconsistent where applicable. Preserve genuinely already-passing cases as regression guards.
- **D169.2 — DML scope propagation / fix now.**
  - `SqlBuilder.cs:1260-1414`: capability handling, target/physical-source propagation, both branch calls, inline collection/merge.
  - `SqlSourceRenderer.cs:285-319`, especially `:299`: accept/forward scope hints through `MakeJoinParts`.
  - Use named option arguments for added metadata. Preserve existing table hints and JOIN conditions.
- **D169.3 — Composition/cache/provider evidence / fix now.** Complete the variant matrix, verify cached/prepared parity and absence of shared-state leakage, run inner-loop tests and post-change measurements.
- **D169.4 — Docs / fix now.** Update EN/RU hint guidance; finish the roadmap item only after CHECK passes.
- **D169.5 — Boundary evidence / fix now.** Build, covered boundary sweep, all-provider integration evidence, changed-branch review and CHECK handoff.

### Expected footprint

“Confirmed” means the planned change requires this file, not that a diff has already been inspected.

| Area | Files/globs | Confidence |
|---|---|---|
| Source | `src/nextorm.core/DataContext/SqlSourceRenderer.cs`, `src/nextorm.core/SqlBuilder.cs` | **Confirmed** |
| Tests | `tests/nextorm.postgres.tests/UpdateJoinSqlGenerationTests.cs`, `DeleteSqlGenerationTests.cs`; `tests/nextorm.sqlite.tests/UpdateJoinSqlGenerationTests.cs` | **Confirmed** |
| Tests | `tests/nextorm.sqlserver.tests/**`, `tests/nextorm.mysql.tests/**`, `tests/nextorm.clickhouse.tests/**`, `tests/nextorm.core.tests/**` | **Likely**; exact test files require discovery |
| Integration | `tests/nextorm.integration.tests/CommonTestSuite.*.cs`, provider-specific test files | **Possible**; use common tests only for genuinely common behaviour |
| Benchmarks | `benchmarks/nextorm.benchmark/**` | **Possible**; only if existing cases cannot measure DML preparation |
| Public docs | `docs/guide/13-query-hints.md`, `docs/ru/guide/13-query-hints.md` | **Confirmed** |
| Public docs | EN/RU provider overview and limitations pages | **Possible**; change only contradicting statements |
| Internal docs | `docs/specs/roadmap/todo_hint-followups.md:9-12`; proposed status file | **Confirmed** |

The pack cannot establish the final minimal changed set. No changes to `FromRenderOptions`’ public shape or provider capability flags are planned.

## 5. Variant matrix and priorities

Each provider row expands over **DELETE and UPDATE**, **null/empty/single/multiple scope hints**, and **target plus one/multiple joined physical sources** where that DML form is supported. Use existing hint values from provider tests.

| Variant ID | Provider / branch / hint case | Closure | Priority |
|---|---|---|---|
| **V01** | PostgreSQL; DELETE `MakeJoinParts`/USING, UPDATE `MakeJoinParts`/FROM; scope hints | SQL-generation tests: inline comment, correct position, no loss/duplication | **P1** |
| **V02** | SQLite; UPDATE `MakeJoinParts`, DELETE `MakeJoin`; nonempty scope hints | Rejection tests; hint-free supported DML guards | **P1** |
| **V03** | SQL Server; DELETE/UPDATE `MakeJoin`; scope hints on target and joined physical sources | SQL-generation tests: proper structural clauses and alias handling | **P1** |
| **V04** | MySQL; DELETE/UPDATE `MakeJoin`; scope hints | SQL-generation tests: inline comment and statement placement | **P1** |
| **V05** | ClickHouse; actual DML support and hint capabilities | Capability guard. If joined DML is unsupported, assert existing DML rejection; otherwise assert scope-hint rejection unless actual flags say supported | **P1** |
| **V06** | All applicable providers; no hints, null/default and empty scope list | Exact existing SQL/exception regression guards; empty-list semantics match SELECT | **P1** |
| **V07** | Structural provider; scope plus explicit joined-table hints | Single merged clause per physical table, multiple hints retained, join-only hint stays local | **P1** |
| **V08** | Inline providers; scope plus supported joined-table and statement hints | Existing composition/deduplication rules retained; unsupported table-hint forms reject | **P1** |
| **V09** | All supported DML routes; reference/value/nullable mapped columns used in JOIN/SET predicates | Regression guard using existing fixture models; SQL predicates and parameter binding unchanged | **P1** |
| **V10** | Cached and explicitly prepared DML; hinted→unhinted→hinted calls | Parity, reuse and no shared-command/cache-flag leakage tests | **P1** |
| **V11** | Explicit **target-only table hints**, independent of scope hints | Existing behaviour guard; repair deferred pending a confirmed defect and assigned rc2 unit | **P2** |
| **V12** | INSERT…SELECT | SELECT scope-hint regression guard; no DML scope change, `RenderMutation:155` | **P2** |
| **V13** | APPLY/PIVOT scope propagation | Deferred: dedicated rc2 follow-up triggered by a supported reproducible case, `:394/:952` | **P2** |
| **V14** | Temporal/index hints, `IndexHintKind`, `SourceParameter` omitted on DML | Deferred: dedicated rc2 follow-up triggered by confirmed failing supported DML metadata case | **P2** |
| **V15** | Temp-table/TVP query branches | Scout reachability guard. If reachable from changed code, existing branch regression tests become mandatory P1 evidence; otherwise explicit non-reachability record | **P1 conditional** |

**Priority rule:** R169-01–04 and executed DML-path rows are P1 by construction. Project register additions can raise priority; CHECK cannot lower it. No new SQL function/operator is introduced, so that class contract is inapplicable; capability flags and provider coverage remain obligatory.

All deferred items remain **1.0.9-rc2**. They are not concealed acceptance gaps for scope hints.

## 6. Tests, coverage and performance

### Test strategy

- **Primary:** database-free SQL-generation tests. They directly expose lost hints, duplicated output and capability rejection.
- **Core/unit:** validation, cache-state and prepared/cached parity using discovered existing hooks.
- **Integration:** one final collection-task boundary sweep, including real PostgreSQL, SQL Server, MySQL and ClickHouse plus SQLite. Unsupported joined DML must be tested as rejection, not forced into an executable form.
- **Red→green:** preserve logs for each new P1 defect scenario. Where a scenario already passes, label it a guard; do not manufacture red evidence.
- **Mutation decision:** no new mutation-tool dependency. Perform controlled fault-injection checks in the working tree: remove structural propagation, inline collection and unsupported-provider validation separately; relevant tests must fail. Restore each mutation immediately and verify the final diff.
- **Coverage:** preserve `coverage.settings.xml`. Assess line **85%** and branch **75%**, with the repository’s main-hard-fail/non-main-warning policy. Independently require exercised new P1 branches and a changed-branch report; a non-main warning cannot excuse an untested acceptance path.
- **Shared test contract:** common integration tests must express common outcomes only. Structural SQL details and unsupported provider exceptions belong in provider-specific tests.

### Command catalogue

Use `/tmp/nextorm-rc2-169-r1` as artifact root **E**. Shell execution must preserve command exit codes through logging (`pipefail`); logs record commit/diff identity and discovered/executed/skipped counts.

| ID | Exact command / invocation |
|---|---|
| **CMD-B** | `dotnet build nextorm.slnx -c Debug` |
| **CMD-C** | `dotnet test tests/nextorm.core.tests -c Debug` |
| **CMD-SQ** | Run `dotnet test tests/nextorm.<provider>.tests -c Debug` for the explicit provider set `{postgres,sqlite,sqlserver,mysql,clickhouse}`. These are whole-project selectors; no unverified test-symbol filters. |
| **CMD-I** | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` |
| **CMD-P** | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` |
| **CMD-BOUNDARY** | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test nextorm.slnx -c Debug` |
| **CALL-COV** | `Task(coder, "Run CMD-BOUNDARY once under the repository CI dotnet-coverage/reportgenerator recipe; preserve coverage.settings.xml; record exact expanded commands, exits, provider counts, aggregate and changed-branch coverage in /tmp/nextorm-rc2-169-r1.")` |
| **CALL-SCOUT** | `Task(scout, "Close D169.0 gaps listed in this plan using Roslyn for C# symbols; return evidence locations, literal CI/tool commands, predecessor records, provider capability/placement map, existing test/benchmark symbols and the normative evidence-contract requirements. No recommendations or code dump.")` |

The literal CI coverage recipe is missing from the pack. `CALL-COV` is the exact owner invocation; its expanded shell commands must be captured and fixed in the persisted contract before execution. Do not substitute an assumed workflow recipe.

### Performance decision: **measurement required**

This is query preparation/rendering on a cached-query path: `QueryPlanner.cs:430-467,479-516` calls the changed DML renderers. It is **not per-row binding**, but cache use does not prove negligible cost.

- Run **CMD-P before and after**, on the same base/environment and with matching benchmark arguments.
- Verify the acceptance category discovers actual benchmarks.
- Verify whether those cases exercise joined DML. If not, add a narrowly scoped preparation benchmark using existing benchmark conventions; preserve baseline measurements of the unfixed production code.
- Record preparation time, allocations and cached-versus-prepared observations separately. SQL-output growth is expected only for hinted commands.
- A statistically supported slowdown or new hint-free allocation triggers investigation before acceptance; do not claim “no regression” from unrelated cases or noise.

## 7. Execution mode, docs and risks

### Mode
**Sequential units in one collection-assigned tree.** Source, test contracts and docs overlap; parallel editing creates avoidable conflicts. Read-only scout gathering may overlap independent environment checks. No worktree, commit, merge or push is authorized by this plan.

### Docs
- Update both hint guides to state joined DELETE/UPDATE scope behaviour, structural versus inline rendering, and unsupported-provider rejection.
- Update overview/limitations EN+RU only if existing text contradicts that behaviour.
- Mark roadmap section D addressed only after passing CHECK, referencing verified #169 and the status record.
- No public links to `docs/specs/**`; no guide renumbering or public API rename.

### Risks
- **P1:** treating a named-argument change as sufficient although PostgreSQL’s actual consumer is inline collection.
- **P1:** attaching structural hints to a target alias rather than the physical source.
- **P1:** inline placement/composition errors and silent unsupported-provider handling.
- **P1:** changing shared command/cache state, or accepting skipped provider tests.
- **P2:** unnecessarily repairing unrelated metadata or creating a general abstraction.
- **P2:** baseline/toolchain instability masking performance or coverage conclusions.

**Confidence:** high that the defect crosses both DML branches; medium on the exact minimal implementation and legal placement until targeted reconnaissance completes. No implementation or measurement has yet been verified.

## 8. Versioned evidence contract — rv=1

### Common mandatory slots

Every row below inherits:

- **Revision:** `rv=1`; stable requirement and evidence IDs.
- **State/source:** all future outputs are **planned**, not existing evidence.
- **Artifact root:** `E=/tmp/nextorm-rc2-169-r1`; status stores exact commands, exits/results, artifact paths and producer/base/diff identity.
- **Evidence provenance:** actual test symbols and final `file:line` are recorded after discovery/edits; none are invented here.
- **Completeness rule:** required evidence must exist, match the applicable scenario and current source snapshot, and demonstrate the stated result. Exit 0 alone is insufficient.
- **Owner:** named producer below; CHECK owns validation and applicability decisions.
- **Applicability:** determined from observable capability/reachability/branch facts, never from a missing report.
- **Obligations:** rows marked unconditional cannot be skipped. Conditional rows require positive applicability evidence or explicit proven non-applicability.

| Evidence ID → requirement | Scenario / applicability predicate | Kinds and planned sources | Exact command/call; required result | Artifacts | Producer |
|---|---|---|---|---|---|
| **E169-01 → R169-05** | Unconditional gate and predecessors | Scout facts; collection ledger; normative skill contract; status file | **CALL-SCOUT**; all dependencies resolved and contract compatible before production DO | `gate.md`, predecessor/base record, persisted contract | Scout / coder |
| **E169-02 → R169-01,02** | New defect scenarios exist | Red test output against unfixed production source | **CMD-SQ**, relevant discovered scenarios; failure must match missing hint/rejection defect, not setup failure | `red/`, test inventory and source identity | Coder |
| **E169-03 → R169-01,03** | V01: PG supported DELETE/UPDATE | SQL-generation results, actual SQL and assertions | PG instance of **CMD-SQ**; exit 0, nonzero cases, complete V01 expansion | `postgres.log`, SQL/assertion mapping | Coder |
| **E169-04 → R169-02,04** | V02: SQLite capability/branch cases | SQL-generation results and rejection assertions | SQLite instance of **CMD-SQ**; exit 0; both DML routes covered | `sqlite.log`, scenario mapping | Coder |
| **E169-05 → R169-01,03** | V03/V07: SQL Server structural cases | SQL-generation output for target/join/composition | SQL Server instance of **CMD-SQ**; exit 0; no alias-only or duplicate-clause false pass | `sqlserver.log`, SQL mapping | Coder |
| **E169-06 → R169-01,03** | V04/V08: MySQL inline cases | SQL-generation output and composition assertions | MySQL instance of **CMD-SQ**; exit 0; correct placement and count | `mysql.log`, SQL mapping | Coder |
| **E169-07 → R169-02** | V05: discovered ClickHouse capabilities | Capability facts and positive/rejection test output | ClickHouse instance of **CMD-SQ**; exit 0; unsupported DML rejection accepted only with capability evidence | `clickhouse.log`, capability mapping | Scout / coder |
| **E169-08 → R169-03,04** | V06–V12; V15 if reachable | Unit/provider results, cache observations, reachability and SQL diffs | **CMD-C + CMD-SQ**; exit 0; variant inventory complete; no shared-state leakage; V15 has tests or non-reachability proof | `core.log`, variant/cache/reachability report | Coder |
| **E169-09 → R169-01–04** | Unconditional build and changed branches | Compiler output, coverage, controlled mutation results | **CMD-B + CALL-COV**; build 0 warnings/errors; CI threshold policy assessed; each temporary mutation rejected by relevant tests | `build.log`, coverage reports, mutation ledger, restored-diff identity | Coder |
| **E169-10 → R169-05** | Unconditional real-provider boundary | Integration output, executed provider counts, infrastructure facts | **CMD-I** before the final covered **CMD-BOUNDARY**; exit 0; required provider tests executed, not skipped | `integration.log`, covered boundary log, provider ledger | Coder |
| **E169-11 → R169-04,05** | Unconditional query-path measurement | Benchmark raw results and cache/preparation observations | Baseline and post-change **CMD-P**; exit 0, nonempty discovery; DML-relevant comparison or supplemental benchmark | `perf/baseline/`, `perf/after/`, comparison and environment | Coder |
| **E169-12 → R169-05** | Unconditional documentation/diff review | EN/RU diffs, roadmap status, CRLF and scope review | `Task(check, "Validate D169 docs, CRLF, no public spec links, no API/capability drift, deferred rc2 ownership and final diff against this contract.")`; explicit pass | `docs-review.md`, final diff manifest | CHECK |

V13/V14 deferrals must appear in the status ledger with their rc2 trigger/owner; they do not count as passing implementation evidence.

### CHECK re-gather budget
**Owner: CHECK. Maximum two targeted re-gather rounds total per CHECK attempt:**

1. One scout round for provenance, applicability or existing-record gaps.
2. One coder round for missing/stale logs, artifacts or narrowly rerun evidence.

Record requested IDs and results. After the budget, return an explicit insufficient-evidence finding; do not infer success, initiate implementation merely because a report is absent, or silently relax the contract.

**Supersession:** none; this is the initial contract. Genuine future changes must retain stable row IDs and obligations, add IDs for new variants, and explicitly record supersession. Merely obtaining missing reports does not increase `r`/`rv` or reset attempts.

--- END ---
