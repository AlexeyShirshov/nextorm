# SQL Server MapColumnExpression numeric boxing — #168 (task D168)

- task: D168
- issue: #168 (https://github.com/AlexeyShirshov/nextorm/issues/168)
- collection: 1.0.9-rc1, group G01
- branch: 1.0.9-rc1 (current worktree)
- cycle: N=1, revision r2, attempt n=1/3, contract rv=D168.r2.ec1
- mode: autonomous + auto-commit; no push; no merge

## Goal

SQL Server: the general buffered `MapColumnExpression` boxes every numeric value
(`Convert.ChangeType(GetValue(index), …)`); read numbers without boxing (perf), preserving provider
parity.

## Plan r2 (rv=D168.r2.ec1)

### Escalation decision B (hard gate)

- Hard gate: **zero numeric-source boxing** on the general buffered numeric path, proven by
  `Allocated B/op` **drop** in BenchmarkDotNet.
- Time gate: median candidate/baseline **≤ 1.05** over paired **ABBA** BDN rounds, **no round > 1.20**,
  and **fail if the regression is reproduced in ≥ 2/3** rounds.
- No merge on a failed gate; perf evidence is mandatory because this is the per-row hot path
  (`nextorm-pdca` cached-path acceptance).

### Acceptance criteria (D168-R1..R9)

- **D168-R1** Zero numeric-source boxing on the general buffered path for storage types
  byte/short/int/long/float/double/decimal (no `GetValue`/`Convert.ChangeType(object)` taken per row).
- **D168-R2** Typed getter selected by the reader's **source** (runtime field) type, not by the
  destination CLR type.
- **D168-R3** The getter/conversion lookup is **shared** with the CSV hook
  (`GetNumericGetter`/`GetTypedConversion`); no duplicated mapping table.
- **D168-R4** Existing `Null`/`DefaultOnNull` branches preserved on the buffered path.
- **D168-R5** Parity with the previous `Convert.ChangeType` semantics across the closed numeric matrix
  (rounding half-to-even, overflow, NaN/Infinity, nullable, `DefaultOnNull`).
- **D168-R6** No `Expression.Convert` unchecked cast substituted for `Convert.ChangeType`; an
  out-of-set storage type falls back to the legacy object path.
- **D168-R7** PostgreSQL and every other provider are unchanged.
- **D168-R8** `dotnet build nextorm.slnx -c Debug` → 0 warnings / 0 errors.
- **D168-R9** Perf gate per the escalation decision above (deferred to the next stream).

### Mechanism

- Compile a runtime **field-type dispatch** in `SqlServerDataContext.MapColumnExpression`: assign
  `record.GetFieldType(index)` to a local once per row, then read the column with the typed getter whose
  storage type matches and convert with the matching static `Convert.To<Target>(storage)` overload.
- Reuse `GetNumericGetter` and `GetTypedConversion` (shared with `MapTypedColumnExpression`).
- Keep the legacy `GetValue` + `Convert.ChangeType` arm only as the dispatch **fallback** for a storage
  type outside the closed numeric set (must never be taken for the seven supported types).
- **No** `Expression.Convert` unchecked cast as a substitute for `Convert.ChangeType`.
- Preserve the `column.Converter is not null`, `!IsNumeric(type)`, `Nullable` and `DefaultOnNull`
  branches exactly; only the numeric `value` expression changes.

### Closed numeric matrix (77 source→dest cases)

- 7 sources × 7 destinations = **49** present-value pairs (byte/short/int/long/float/double/decimal).
- 7 sources × 4 null-policy shapes = **28** cases (nullable-null, nullable-value, DefaultOnNull-null,
  DefaultOnNull-value).
- Total **77**; rounding/overflow/NaN/Infinity pinned by an independent `Convert.ChangeType` oracle.

### DO units

- **D1** Baseline capture + characterization oracle + strict-`DbDataReader` parity/no-boxing scaffold.
- **D2** Typed numeric path in general buffered `SqlServerDataContext.MapColumnExpression` (+ XML docs).
- **D3** Docs EN/RU update for the changed buffered behavior.
- **D4** BenchmarkDotNet harness + ABBA perf gate (deferred).
- **D5** SQL Server container integration run + full-solution verification (deferred).

### Docs plan

- `docs/providers/sqlserver.md:9-12` (and RU mirror): the provider no longer reads numeric columns of
  the buffered path through `Convert.ChangeType`; describe the runtime field-type dispatch.
- `docs/advanced/limitations.md:52` + `docs/guide/28-streaming-data.md:117,284` (and RU mirrors):
  remove the claim that the buffered `MapColumnExpression` path boxes on `ToList`.
- In-repo XML docs of changed members updated with the code change.

### Perf decision

- BenchmarkDotNet micro-benchmark of the buffered numeric read (candidate vs baseline) with paired ABBA
  rounds; judge on `Allocated B/op` (must drop) and time median (≤1.05, no round >1.20, ≥2/3 rule).
- Deferred to the next stream; **D168-R9 stays pending**.

### Recon decision

- Keep the persisted reconnaissance: scope is **perf/boxing, not correctness** (issue label enhancement;
  `docs/specs/roadmap/todo_csv_streaming.md:33-39,75-79` §2(c), out of #112).
- Correctness is guarded by the characterization oracle, not by re-deriving behavior; PostgreSQL and the
  other providers are out of scope.

### Design checklist

- [x] Nullable / `DefaultOnNull` branches preserved.
- [x] Converter bypass (`column.Converter is not null` → base) preserved.
- [x] Fallback for out-of-set storage preserved.
- [x] No public API surface change.
- [x] XML docs of changed members updated.
- [x] CRLF preserved; `TreatWarningsAsErrors` clean.
- [x] Docs EN/RU update (D3): `docs/providers/sqlserver.md` (+RU), `docs/guide/28-streaming-data.md` (+RU), `docs/advanced/limitations.md` (+RU), `docs/specs/roadmap/todo_csv_streaming.md`, `todo_json_streaming.md` — buffered numeric path now storage-typed/no-boxing, #168 referenced.
- [x] BDN perf gate (D4): PASS — allocations 464→0 B/row, boxing 9→0, ABBA ratios 0.3114/0.3382/0.3495 (median 0.3382).
- [x] Container integration (D5): exit 0, 3179 total / 0 failed / 0 errors / 193 provider-capability skips; SQL Server 2025 (RTM-CU9) 17.0.5005.3.

### Evidence rows

| Id | Evidence | Pointer | Status |
| --- | --- | --- | --- |
| D168-E01 | Baseline HEAD sha / env capture | `artifacts/d168/baseline.txt` | done |
| D168-E02 | Characterization oracle (`Convert.ChangeType`) facts | `artifacts/d168/test-newclass.log` | done |
| D168-E03 | 49-pair buffered/oracle parity theory | `artifacts/d168/test-newclass.log` | done |
| D168-E04 | 28-case null-policy theory | `artifacts/d168/test-newclass.log` | done |
| D168-E05 | Strict reader (no `GetValue`) box-free read | `artifacts/d168/test-newclass.log` | done |
| D168-E06 | Runtime storage-type dispatch (distinguishes CSV hook) | `artifacts/d168/test-newclass.log` | done |
| D168-E07 | `dotnet build nextorm.slnx -c Debug` 0/0 | `artifacts/d168/build-slnx.log` | done |
| D168-E08 | `nextorm.sqlserver.tests` exit + counts | `artifacts/d168/test-sqlserver.log` | done |
| D168-E09 | `nextorm.core.tests` exit + counts | `artifacts/d168/test-core.log` | done |
| D168-E10 | Docs EN/RU diff (D3) | `docs/providers/sqlserver.md`, `docs/guide/28-streaming-data.md`, `docs/advanced/limitations.md` (+RU); `docs/specs/roadmap/todo_{csv,json}_streaming.md` | done |
| D168-E11 | BDN allocation/time report (D4) | `artifacts/d168/bdn-gate-report.json`, `bdn-gate-summary.md`, `bdn-r*.log` | done |
| D168-E12 | SQL Server container integration run (D5) | `artifacts/d168/integration-run.log`, `integration-results.xml` | done |

### Acceptance perf verdict (D4, appended)

- Paired ABBA (`git archive` baseline `6bf362958ee4336e83a2a7550e49038184ff1caf` vs candidate; same
  `AcceptanceHarness.targets` injection; Release; SDK 10.0.401; fresh process per run; 3 rounds × A-B-B-A;
  20 iterations / 5 warmups; `--filter *SqlServerBufferedNumericAcceptanceBenchmark* --anyCategories=acceptance`).
  `--filter '*'` was scoped to the D168 type because six other pre-existing `acceptance`-category benchmarks
  in the project would otherwise be measured; the category filter is unchanged.
- Per-row (`OperationsPerInvoke=10000`, 11 numeric columns incl. `int?`/`decimal?` null and non-null):
  - **Allocations**: baseline **464 B/row** (Gen0 142/1000) → candidate **0 B/row** (Gen0 0) — drop.
  - **Numeric-source boxing count** (strict-reader `GetValue` calls/row): baseline **9** → candidate **0**.
  - **Time** (candidate/baseline per round): r1 **0.3114**, r2 **0.3382**, r3 **0.3495**; median **0.3382**;
    no round > 1.20.
- **Verdict: PASS** (allocations dropped, boxing = 0, median ratio ≤ 1.05 with no round > 1.20).
  `D168-R9` is satisfied by this evidence. No harness/environment blocker.

### CHECK re-gather budget

- CHECK may re-gather evidence at most **2** times for this revision.

## Reconnaissance (persisted; not yet re-verified)

Scope = **perf/boxing, not correctness** (issue label enhancement; source `docs/specs/roadmap/todo_csv_streaming.md:33-39,75-79` §2(c), out of #112).

- `MapColumnExpression` is a **method** (`NextORM.Core.DataContext`): base virtual `DataContext.cs:621` → `RowMapperFactory.MapColumn:48`; overridden only by SQL Server `SqlServerDataContext.cs:374` (boxing) and PostgreSQL `:235` (Range-only). MySQL/SQLite/ClickHouse/MariaDB use base typed getters.
- Boxing site: `SqlServerDataContext.cs:390-392` `Convert(Convert.ChangeType(GetValue(index), type), PropertyType)`; MethodInfo `:19` `GetValueMethod`, `:21` `ChangeTypeMethod`; `IsNumeric:525`; `Nullable`/`DefaultOnNull :394-410`. SQL Server boxes because typed SqlClient getters are strict to widening (`:386-388`).
- CSV typed hook already avoids it: base `MapTypedColumnExpression DataContext.cs:637`; SQL Server override `SqlServerDataContext.cs:426`; `SupportsTypedColumnMapping:490`; `GetNumericGetter:499`; `GetTypedConversion:511`. Common-mapper call sites: `DataContext.cs:766,775,1313`, `RawMapperFactory.cs:96`, `QueryCommandExtensions.cs:410,452`.
- Tests: `tests/nextorm.sqlserver.tests/CsvTypedColumnMappingTests.cs:16,20,32,44,53,66,81` (CSV hook only); `SqlServerTestContext.cs:34,42` exposes the protected hook; integration `SqlServerSpecificTests.cs:1535,1556`; `CommonTestSuite.JsonStream.cs:112-138`. **No test asserts absence of boxing in the general buffered path.**
- Docs EN/RU: `docs/providers/sqlserver.md:9-12` (describes `Convert.ChangeType`; will need update), `docs/guide/28-streaming-data.md:12,117,209`, `docs/advanced/limitations.md:52`; RU mirrors.
- Specs: `docs/specs/roadmap/todo_csv_streaming.md:33-39,75-79`; `todo_json_streaming.md:108-109,286-287,317` (same SQL Server `GetValue`+`Convert.ChangeType` flagged as zero-boxing blocker).
- Open questions for PLAN: exact mechanism to preserve widening parity without boxing (typed getters + explicit widening converters); which consumers hit the general buffered path; performance-measurement requirement (this is a hot/row path → benchmark likely mandatory); test strategy for "no boxing".

## Progress log

- 2026-10-05 23:52 UTC | PLAN | r2 | n=1/3 | plan r2 persisted (rv=D168.r2.ec1); escalation decision B recorded; DO units D1-D5 defined | docs/specs/status/rc1-168-mssql-boxing-1.md
- 2026-10-05 23:52 UTC | DO | r2 | n=1/3 | D1 baseline captured (HEAD 6bf362958ee4336e83a2a7550e49038184ff1caf) | artifacts/d168/baseline.txt
- 2026-10-05 23:55 UTC | DO | r2 | n=1/3 | D1 characterization + parity/no-boxing tests added (77-case matrix) | tests/nextorm.sqlserver.tests/SqlServerBufferedNumericMappingTests.cs
- 2026-10-05 23:55 UTC | DO | r2 | n=1/3 | D2 typed runtime-dispatch numeric path implemented; shared GetNumericGetter/GetTypedConversion; legacy fallback kept | src/nextorm.sqlserver/SqlServerDataContext.cs
- 2026-10-05 23:53 UTC | DO | r2 | n=1/3 | build nextorm.slnx -c Debug 0 warnings/0 errors (exit 0) | artifacts/d168/build-slnx.log
- 2026-10-05 23:55 UTC | DO | r2 | n=1/3 | new class 102 passed/0 failed; sqlserver 686 passed/0 failed; core 1548 passed/0 failed | artifacts/d168/test-newclass.log, test-sqlserver.log, test-core.log
- 2026-10-05 23:55 UTC | DO | r2 | n=1/3 | D3 docs EN/RU pending; D4 BDN + D5 container integration deferred to next stream | artifacts/d168/

- Historic: Recon persisted (run 6 start).

- Notice: host has no todowrite tool; this file is the progress log.
- 2026-10-06 05:09 UTC | DO | r2 | n=1/3 | D3 docs EN/RU updated (provider/streaming/limitations + roadmap specs; #168 referenced; no public→specs links) | docs/providers/sqlserver.md, docs/guide/28-streaming-data.md, docs/advanced/limitations.md, docs/specs/roadmap/todo_csv_streaming.md, todo_json_streaming.md (+RU)
- 2026-10-06 05:08 UTC | DO | r2 | n=1/3 | D4 acceptance harness added ([MemoryDiagnoser], acceptance category, strict mock reader, MSBuild injection target, ABBA gate) | benchmarks/nextorm.benchmark/acceptance/, artifacts/d168/bdn-gate-report.json
- 2026-10-06 05:08 UTC | DO | r2 | n=1/3 | D4 gate PASS: allocations 464→0 B/row, boxing 9→0, ratios 0.3114/0.3382/0.3495 (median 0.3382) | artifacts/d168/bdn-gate-summary.md, bdn-gate-report.json
- 2026-10-06 05:10 UTC | DO | r2 | n=1/3 | D5 integration exit 0: 3179 total / 0 failed / 0 errors / 193 capability skips (SqlServer 680 pass/43 skip; PG 766/25; MySql 594/80; CH 173/0; Sqlite 656/43) | artifacts/d168/integration-run.log, integration-results.xml
- 2026-10-06 05:11 UTC | DO | r2 | n=1/3 | SQL Server version: 2025 (RTM-CU9) KB5122048, 17.0.5005.3 X64, Enterprise Developer, Ubuntu 24.04.4 | artifacts/d168/integration-run.log
- 2026-10-06 05:11 UTC | DO | r2 | n=1/3 | docfx exit 0 (2 pre-existing sourcegenerator warnings); git diff --check exit 0 | artifacts/d168/docfx.log, git-diff-check.log
- 2026-10-06 05:12 UTC | DO | r2 | n=1/3 | durable state unchanged: cycle N=1, revision r2, attempt n=1/3; no new defect keys; D168-R9 satisfied | docs/specs/status/rc1-168-mssql-boxing-1.md

## ACT

- Freeze point: CHECK PASS on revision r2, attempt n=1/3, evidence contract rv=D168.r2.ec1.
  Durable state unchanged: cycle N=1, plan revision r2, attempt n=1/3; no new defect keys.
- Commit plan: stage only the D168 change set and commit
  `#168 SQL Server typed buffered numeric mapping (no source boxing)`; then a separate
  bookkeeping commit for `docs/specs/status/collection-1.0.9-rc1.md`; never push, never merge,
  never `git add -A`; `artifacts/` is not committed.
- Perf gate B result (D168-R9): PASS — allocations 464→0 B/row, numeric-source boxing 9→0,
  paired ABBA candidate/baseline ratios 0.3114/0.3382/0.3495 (median 0.3382, no round > 1.20).
- Issue outcome: #168 closed as completed; general buffered numeric mapping is now storage-typed
  with no per-row source boxing; the commit remains unpushed.

- 2026-10-06 05:16 UTC | ACT | r2 | n=1/3 | CHECK PASS (rv=D168.r2.ec1) finalized; freeze point recorded; commit + bookkeeping + issue close | docs/specs/status/rc1-168-mssql-boxing-1.md
