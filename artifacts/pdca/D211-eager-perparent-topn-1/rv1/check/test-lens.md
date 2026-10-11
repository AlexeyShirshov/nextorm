# T211 (#211) CHECK — test / coverage / mutation lens — RAW candidates (no verdict)

Task: T211, branch `1.0.9`, reviewed diff `e98ae60e` (HEAD, commit 2026-10-12T02:14:09+05:00) vs base `b527be6f`.
Evidence root: `artifacts/pdca/D211-eager-perparent-topn-1/rv1/`.
Read-only. All test symbols below were resolved in the committed tree `e98ae60e` (working tree has only unrelated
docs changes; `git diff HEAD -- tests src` is empty). `Feature=D211` is expressed as `[Trait("Feature","D211")]`
(class-level on the new test classes, method-level in the shared integration suite).

## 1. Variant-matrix closure (plan `docs/specs/status/D211-eager-perparent-topn-1.md:228-252`)

| # | Matrix row (plan line) | Closing test(s) in committed tree (`file:line`) | Raw status |
|---|---|---|---|
| 1 | No child paging (:230) | `tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs:194` `Split_UnpagedChild_RetainsEveryChild`; `tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs:74` `UnpagedSplitChild_InMemory_StillLoadsEveryChild`, `:90` `UnpagedSingleQueryChild_InMemory_StillLoadsEveryChild`; `tests/nextorm.integration.tests/CommonTestSuite.EagerLoadPerParentPaging.cs:148` `EagerPerParentPaging_UnpagedChild_LoadsEveryChild` | closed (unpaged behavior). Unpaged **cache reuse** not asserted by a D211 test (see row 19). |
| 2 | Take only / Skip only / Skip+Take (:231) | sqlite:66 `Split_Take…`, :94 `Split_SkipAndTake…`, :118 `Split_SkipOnly…`, :287 `SingleQuery_Take…`, :316 `SingleQuery_SkipAndTake…`, :340 `SingleQuery_SkipOnly…`; `tests/nextorm.postgres.tests/EagerLoadPerParentPagingTests.cs:73,106`; `CommonTestSuite…:47,65,82`; `tests/nextorm.integration.tests/ClickHouseEagerPerParentPagingTests.cs:50,67`; `tests/nextorm.integration.tests/MariaDbEagerPagingTests.cs:93,108` | closed |
| 3 | Zero/default bounds / negatives / extreme bounds (:232) | — no `Feature=D211` test uses `Limit(0)`, a negative bound, an int-extreme bound, or asserts the `long` overflow arithmetic at `SqlBuilder.cs:888`. Only the pre-existing `Paging` normalization tests are run in broad regression. | **NONE — row not closed by a D211 test** |
| 4 | Empty source / parent without matches / Skip past end (:233) | no-match: sqlite:84,108,136 and `CommonTestSuite…:60,77,93` (parent 3 preserved); skip-past-end only as SQL-text `Split_SkipOnly` (`postgres:106`, Offset(3) with 2 children) — no execution assert. Empty child **table** not exercised. | partial (no-match closed; empty source + skip-past-end execution **NONE**) |
| 5 | Ordered asc/desc / multiple keys (:234) | sqlite:564 `SingleQuery_DescendingOrder…`, :590 `SingleQuery_MultipleSortKeys…`, :616 `Split_DescendingAndMultipleKeys…`; `postgres:120`; `CommonTestSuite…:114` | closed |
| 6 | No order / tied order (:235) | sqlite:650 `SingleQuery_NoOrder_PagedChild_ReturnsMembershipAndCountOnly`, :678 `SingleQuery_TiedOrder_PagedChild_…` | partial: membership/count closed **only in single-query on SQLite**; no split no-order/tied test; the "guard unsupported provider form" half has **NONE** |
| 7 | Scalar/composite parent-link keys (:236) | sqlite:748 `Split_CompositeKey_Take…`, :778 `Split_CompositeKey_SkipAndTake…`, :806 `Split_CompositeKey_PagedSql…`, :839 `SingleQuery_CompositeKey_IsRejected…`, :871 `SingleQuery_AnonymousKey_IsRejected…`; `CommonTestSuite…:300,340`; `ClickHouseEagerPerParentPagingTests.cs:102`; `MariaDbEagerPagingTests.cs:138` | closed (split positive / single-query fail-closed; confirmed by DO log) |
| 8 | Nullable/reference key components / default value keys (:237) | only sqlite:871 `SingleQuery_AnonymousKey_IsRejected…` (reference-key rejection). No nullable-key or default-value-key test, positive or negative. | **NONE for nullable/default; reference only as a single-query rejection** |
| 9 | Reference entities / supported value projections (:238) | — no D211 test projects a child to a value/record/`Select`; all load entity collections. | **NONE** |
| 10 | Child predicate / supported projection (:239) | sqlite:146 `Split_ChildFilterPrecedesRanking`, :365 `SingleQuery_ChildFilterPrecedesRanking`; `CommonTestSuite…:98`; filter-before-rank SQL: `postgres:73`; rank column excluded from projection: sqlite:218 | closed (predicate + rank-column projection; no value-projection). |
| 11 | Aggregate/grouped/distinct/set-op child forms (:240) | `tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs:697` (OrderBy), `:724` (Distinct/GroupBy); `postgres:167` (Distinct/GroupBy) | partial: Distinct/GroupBy/OrderBy closed; no set-operation (`Union`/`Except`/`Intersect`) negative test |
| 12 | Split / single-query (:241) | sqlite:416 `SingleQuery_And_Split_ProduceSamePerParentResultWithDifferentQueryCounts`; `CommonTestSuite…:164,185` | closed |
| 13 | Nested navigation / two independently paged collections (:242) | two sibling collections: sqlite:705 `SingleQuery_MultiplePagedNavigations_ArePagedIndependently`; `CommonTestSuite…:205` | partial: two-collection closed; genuine **nested (collection-in-collection) navigation NONE** |
| 14 | Root Take/Skip plus child paging (:243) | sqlite:258,390; `CommonTestSuite…:129`; `ClickHouseEagerPerParentPagingTests.cs:83`; `MariaDbEagerPagingTests.cs:122` | closed |
| 15 | LoadWith child factory / applicable JoinInto overloads (:244) | child factory used by every `LoadWith` test above; `JoinInto` behavior tests in `EagerLoadingSqlGenerationTests.cs:773,841,879,935,973,1005,1033,1061` (mostly pre-existing D209, not `Feature=D211`-tagged) | partial: child factory closed; of the 5 public `JoinInto` overloads (`EntityBuilder.cs:732,753,811,834,879`) the `TKey`-selector overloads (:834/:879) have no D211 test; no dedicated "unchanged behavior" test is tagged D211 |
| 16 | Implicit navigation without paging metadata (:245) | — no test references `NavigationExpansion.cs:43`/implicit navigation with an eager paged child; only pre-existing implicit-nav tests run in broad regression. | **NONE — no D211 closing test** |
| 17 | Builder / prepared QueryCommand execution (:246) | builder surface: all sqlite tests; prepared surface: `postgres:49-50` `GetPreparedQueryCommand`, `postgres:186` `PrepareCommand`; plan-cache: `EagerLoadingSqlGenerationTests.cs:580` | partial: both surfaces exercised; **argument/params parity has no explicit D211 test** |
| 18 | Sync / async / token already cancelled / cancellation during reading (:247) | sync: rows above; async: sqlite:170, `CommonTestSuite…:228,244`; pre-cancelled: `tests/nextorm.core.tests/EagerLoadingTests.cs:154`; cancel-between-chunks: `tests/nextorm.core.tests/EagerLoadingSingleQueryP1Tests.cs:729` | partial: sync/async closed; pre-cancelled + mid-read cancellation are **pre-existing, not `Feature=D211`**, and are not exercised on the per-parent paged path (EV-06 caveat admits this) |
| 19 | Cached / noncached / rebind / same-context interleaving (:248) | repeated same-context paged load: sqlite:534 `Split_PagedChild_RepeatedOnSameContext_StaysCorrectThroughThePlanCache`; plan-cache + sticky flag: `EagerLoadingSqlGenerationTests.cs:580` | partial: rebind + no-sticky closed; **no noncached local preparation (`storeInCache:false`), no paged-vs-unpaged shape-separation test, no `Any`/`Count`-interleaving test** (REQ-05 explicitly names interleaving) |
| 20 | SQLite / PG / SQL Server / MySQL / MariaDB / ClickHouse (:249) | SQL-gen: postgres/sqlserver/mysql/clickhouse `EagerLoadPerParentPagingTests.cs` + `tests/nextorm.mysql.tests/MariaDbEagerLoadPerParentPagingTests.cs`; integration: `CommonTestSuite…`, `ClickHouseEagerPerParentPagingTests.cs`, `MariaDbEagerPagingTests.cs` | closed |
| 21 | Unsupported window/version flag / unsupported derived-source form (:250) | Correlated: `postgres:179`, sqlite `EagerLoadingSqlGenerationTests.cs:407,432,525`; Distinct/GroupBy: `postgres:167`, sqlite `EagerLoadingSqlGenerationTests.cs:724` | partial: unsupported shape closed; **no version/capability-flag test** |
| 22 | In-memory eager-supported navigation (:251) | `tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs:74,90` | closed |
| 23 | In-memory eager-unsupported navigation (:252) | `tests/nextorm.core.tests/EagerLoadPerParentPagingTests.cs:48,61` | closed |

> Raw candidate: rows 3, 8, 9, 16 have **no** closing `Feature=D211` test; rows 4, 6, 11, 13, 15, 17, 18, 19, 21 are only partially closed. The plan header states "Every row is closed by test or guard + test; none is deferred."

## 2. Coverage delta (independent read)

Source artifacts: `coverage/branch-delta.json` (computed 2026-10-12T01:25 from `coverage/coverage.xml`, collected
2026-10-11T20:50 local / 15:50Z) and `coverage/summary.txt`.

- Changed lines: **241 / 262** covered (21 uncovered).
- Changed branch conditions: **154 / 190** covered (25 uncovered/partial).
- Aggregate: line 88.5% (>=85), branch 80.5% (>=75); the 20:50 run reported 537 integration failures (container
  timeouts) and is not a provider-green run.
- Per-branch dispositions exist **only** for Group B (`GROUP_B_CLASSIFICATION`, 12 branches
  `SqlBuilder.cs:822,824,825,827,828,829,830,831,832,833,834,835`, `defensive/unreachable`, root cause child
  plan-key divergence, `mutation/groupb-classification.md`). For the other 13 partial branches
  (`EntityBuilder.cs:1650`; `EntityBuilderEagerLoading.cs:349`; `SqlBuilder.cs:643,646,666,683,706,709,712,724,737,752,759,786,791,794,837,865`;
  `QueryPlanEqualityComparer.cs:166,167,170`) `branch-delta.json` carries **no per-branch disposition**; reasons
  only exist indirectly in `mutation/survivors.md`.
- Uncovered/partial changed **P1** branches with no (valid) justification:
  - `SqlBuilder.cs:724 (1/2)` — the partition-column separator `i == 0 ? "partition by " : ", "`. Its
    `mutation/survivors.md:33` disposition ("multiple partition columns require a composite child key, which the
    split loader cannot execute") is **contradicted** by the committed composite tests
    (`sqlite:806` asserts `partition by tenant, parent_id`; `CommonTestSuite…:300`; `ClickHouse…:102`).
  - `QueryPlanEqualityComparer.cs:166 (1/2)`, `:167 (3/4)`, `:170 (3/4)` — the per-parent plan-key separations
    (paged/unpaged, partition selector, embedded/top-level). No test prepares two such commands on one context;
    REQ-05 ("Plan caching separates paged/unpaged shapes") has no closing test.
  - `EntityBuilder.cs:1650 (3/4)` — `parentSorting is null` branch (paged embedded child with no parent `ORDER BY`);
    all single-query tests order the parent.
- **Provenance gap:** `coverage.xml` was collected 2026-10-11T15:50Z, before the committed test files were
  finalized (`tests/nextorm.sqlite.tests/EagerLoadPerParentPagingTests.cs` mtime 2026-10-12T01:49 local;
  `CommonTestSuite.EagerLoadPerParentPaging.cs` 01:53; composite integration tests 01:51-01:54). The D211-COMP
  composite tests and the final sqlite edits are therefore **absent from the coverage run**, which explains
  `SqlBuilder.cs:724 (1/2)` and other composite-renderer gaps. The reported 241/262 and 154/190 are not bound to
  the frozen tree's test suite. `coverage/branch-delta-merged.json` is likewise derived from the same run.

## 3. Mutation (independent read)

Source: `mutation/mutation-report.json` (refreshed 2026-10-12T01:41 from `mutation/run-survivors/reports/`,
run 01:24-01:31 local / 20:24-20:31Z), `mutation/survivors.json`, `mutation/survivors.md`,
`mutation/mutation-disposition.log`.

- Counts: **Killed 138 / Survived 40 / NoCoverage 26** (total tested 178; score 67.65%). Independently
  reproduced from `mutation-report.json` (`files[*].mutants[*].status`). The log also records 8546 CompileError
  and 25754+32 Ignored mutants (whole-file mutation, safe-mode removals) — not part of the score.
- `survivors.json` has **66** entries (40 Survived + 26 NoCoverage); every one has a `disposition` and
  `in_changed_lines == true`. Summary: equivalent 28, defensive 35, killed 3.
- Real-logic survivor with an invalid/no equivalence argument:
  - `src/nextorm.core/DataContext/SqlBuilder.cs:724:36` (Survived, `partition by` vs `, ` separator) — disposition
    "defensive: composite split unreachable" is false in the committed tree; the composite tests assert the
    two-column partition, so this is a real logic mutant that the frozen tests exercise and should kill. Same
    factual defect invalidates `SqlBuilder.cs:724:67` (NoCoverage) and weakens `:719:34`.
- **Stale run / unverified kills:** `survivors.md:44,57,58` claim 3 surviving mutants are "killed by newly added
  Feature=D211 tests" (`Split_PagedSql_UsesPartitionedRowNumberAndBounds`, the two `*_SkipOnly_*` tests). Those
  tests were added/finalized after the run (test file mtime 01:49 > run 01:31; README:49 admits "a confirming
  mutation re-run is due"). No re-run exists (`run-survivors/` unchanged), so the canonical report still lists
  all 3 as Survived. The composite-renderer mutants are also not re-evaluated against the committed composite
  tests. A mutation re-run on the frozen tree is absent; every "defensive/unreachable composite" disposition is
  therefore unverified against `e98ae60e`.

## 4. Claimed test names — existence check

Resolved in the committed tree (no non-existent claim found):

- `tests/nextorm.core.tests/EagerLoadingTests.cs:154` `LoadWith_SplitAsync_PreCancelled_ThrowsWithoutLoadingChildren` (EV-06 claim) — exists.
- `tests/nextorm.core.tests/EagerLoadingModeTests.cs:400` `EagerLoadMode_ApiSurface_HasTheExpectedShape` (EV-06 claim) — exists.
- `tests/nextorm.core.tests/EagerLoadingSingleQueryP1Tests.cs:729` `SplitQuery_CancelledBetweenChunks_StopsBeforeTheSecondChildQuery` — exists.
- Composite integration follow-up counts 14/14/14/14/4/4 (EV-05) — confirmed in
  `integration/composite-{sqlite,postgres,sqlserver,mysql,clickhouse,mariadb}.log`.
- `docs/specs/.../D211...md:245` anchor `Visitors/NavigationExpansion.cs:43` (`Expand`) — exists; no test claims it.

Raw inconsistency: the ledger EV-03 claims `sqlite Feature=D211 24/24` from
`inner-loop/sqlite-trait-D211.log` (run 2026-10-12T01:42), but the committed sqlite class has **28** test methods
(28 `[Fact]`), and the later `composite/sqlite-composite-class.log` (01:58) reports 28/28. The declared
`-trait "Feature=D211"` invocation therefore does not select the final 28-test class; only a class-filter run
(not the declared selector) covers it. Similarly EV-05's primary provider logs (`12/0`, 2026-10-11T20:13-20:20
local) predate the final `CommonTestSuite` (14 D211 methods, 01:53) and report lower totals than the frozen suite.

## Raw candidates (gather only — no PASS/FAIL)

1. Variant rows with no D211 closing test: zero/negative/extreme bounds (:232); nullable/default key components
   (:237); value projections (:238); implicit navigation without paging metadata (:245).
2. Partially closed rows: no-order/tied split + provider guard (:235); skip-past-end/empty-source execution
   (:233); set-operation negatives (:240); genuine nested navigation (:242); `JoinInto` TKey overloads +
   unchanged-behavior (:244); argument parity (:246); cancellation on the paged path (:247); noncached /
   shape-separation / Any-Count interleaving (:248); version/capability flag (:250).
3. Coverage evidence stale vs frozen tree: `coverage.xml` (15:50Z) predates all final D211 test edits
   (01:49-01:54 local); `SqlBuilder.cs:724 (1/2)` is an uncovered changed P1 branch whose stated defensive
   justification is contradicted by committed composite tests; `QueryPlanEqualityComparer.cs:166/167/170` and
   `EntityBuilder.cs:1650` have no per-branch disposition.
4. Mutation evidence stale vs frozen tree: run predates the final tests; 3 claimed kills unverified (still
   Survived in the canonical report); `SqlBuilder.cs:724:36` is a real logic survivor with a false equivalence
   argument; no mutation re-run bound to `e98ae60e`.
5. `Feature=D211` selector evidence mismatch: sqlite trait run 24 vs committed 28 tests; primary provider
   integration logs 12 vs committed 14 D211 methods.
