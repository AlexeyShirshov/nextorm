# SQLite FTS5 maintenance/control command surface — issue #195 (task D195)

- task: D195
- issue: #195
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: done
- cycle: N=1
- plan revision: r=1
- attempt: n=2/3
- contract: rv=1
- mode: autonomous (auto-commit authorized; push never)
- git: no push/merge; this DO stream edits source + this status file only; auto-commit is performed by a later unit
- notice: host has no `pdca-orchestrator` agent and no `todowrite` for subagents; the flat primary drives this cycle and this status file carries the progress log.

## Current state

**DO correction complete (r=1, n=2/3).** The first CHECK failed on the P1 defect
`D195-FTS5-IDENTIFIER-QUOTING`: `SqlMutationBuilder.MakeSqliteFts5` emitted the caller-supplied
`command.TableName` verbatim when `quoteIdentifiers == false` (the `DataContextBuilder` default) and only
conditionally quoted `rank`, breaking unusual names and permitting identifier/SQL injection. The renderer
now ALWAYS quotes both the FTS5 table name and the `rank` special column through
`ISqlDialect.QuoteIdentifier` (the same helper `MakeDropTableIfExists` uses), the now-unused
`quoteIdentifiers` parameter was dropped and the caller in `DataContext.BuildSqliteFts5Sql` updated. The
regression tests (red→green) and the remaining frozen variant rows were added; every re-run suite is
green. CHECK re-adjudicates EN-01..EN-09.

DO ledger:

| unit | status | evidence |
|---|---|---|
| D195.0 contract/preflight | done | this status file + frozen FC195-1 (rv=1) + reconnaissance/preflight |
| D195.1 semantic command + builder + factory | done | `SqliteFts5Command.cs`, `SqliteFts5CommandBuilder.cs`, `DataContextExtensions.cs` |
| D195.2 renderer + dispatch + capability guard | done | `SqlMutationBuilder.MakeSqliteFts5` + `DataContext.BuildMutationSql`/`BuildSqliteFts5Sql` + capability guard |
| D195.3 SQL-generation + core + provider-rejection tests | done | core/sqlite + five `SqliteFunctionsRejectionTests.cs`; `/tmp/nextorm-d195-r1/{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-tests.log` |
| D195.4 real-SQLite execution tests | done | `SqliteSpecificTests` + full integration; `/tmp/nextorm-d195-r1/{integration-sqlite,integration}.log` |
| D195.5 docs EN+RU | done | 6 pages; `/tmp/nextorm-d195-r1/docfx.log` (0 errors) |
| D195.6 verification/handoff | done | build / tests / providers / integration / coverage / docfx evidence below |

## Goal / PLAN (verbatim, r=1)

---BEGIN PLAN---
Goal: Add the SQLite FTS5 maintenance/control command surface from issue #195 (follow-up to #181):
`AutoMerge`, `CrisisMerge`, `Merge`, `Optimize`, `Rebuild`, `IntegrityCheck`, rendered through the FTS5
control interface (`INSERT INTO <table>(...) VALUES(...)`) and executed on a database-backed context.
Acceptance criteria A-01..A-10 (below). Completion requires CHECK evidence; unit/integration tests and
docs are sibling DO streams.

Frozen contract FC195-1 (public surface, namespace `NextORM.Core`):
- `public sealed class SqliteFts5CommandBuilder` (no public ctor, no mutable properties, no `Returning`/
  result materialization, no `UserMerge`).
- `public static SqliteFts5CommandBuilder CreateSqliteFts5CommandBuilder(this IDataContext dataContext, string tableName)`.
- `public SqliteFts5CommandBuilder AutoMerge(int value)`; `CrisisMerge(int value)`; `Merge(int pages)`;
  `Optimize()`; `Rebuild()`; `IntegrityCheck(bool? checkExternalContent = null)`.
- `public string ToSql()`; `public int Execute()`; `public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)`.

Validation contract:
- factory null `dataContext` → `ArgumentNullException(nameof(dataContext))`; null `tableName` →
  `ArgumentNullException(nameof(tableName))`; empty/whitespace/embedded-NUL `tableName` →
  `ArgumentException` with param `tableName`. Name preserved verbatim (no trim/split; `.` is a literal
  character).
- operation methods return a NEW immutable builder; the receiver is unchanged.
- terminal (`ToSql`/`Execute`/`ExecuteAsync`) on a builder with no operation selected →
  `InvalidOperationException("Select an FTS5 maintenance command before rendering or executing it.")`.
- `AutoMerge`: 0..16 inclusive, else `ArgumentOutOfRangeException(nameof(value))`. `CrisisMerge`:
  negative → `ArgumentOutOfRangeException(nameof(value))`; 0/1 pass through. `Merge`: any signed int passed
  through unchanged (never `abs`). `IntegrityCheck(null)` omits the rank argument; `false` → 0; `true` → 1.

Internal command model: `internal sealed class SqliteFts5Command : MutationCommand` in `Query/Mutations/`,
ctor `(string tableName, SqliteFts5Operation operation, int? value)` with base
`(SqlStatementType.Insert, typeof(object))`; exposes `TableName`/`Operation`/`Value`. Internal enum
`SqliteFts5Operation { AutoMerge, CrisisMerge, Merge, Optimize, Rebuild, IntegrityCheck }` with an explicit
numeric value per member (stable, do not reorder).

Rendering table (`SqlMutationBuilder.MakeSqliteFts5`, `internal static`, uppercase keyword example; `{T}` =
quoted table name, `{rank}` = quoted `rank` identifier, `{v}` = invariant integer):

| operation | SQL |
|---|---|
| AutoMerge | `INSERT INTO {T} ({T}, {rank}) VALUES ('automerge', {v})` |
| CrisisMerge | `INSERT INTO {T} ({T}, {rank}) VALUES ('crisismerge', {v})` |
| Merge | `INSERT INTO {T} ({T}, {rank}) VALUES ('merge', {v})` |
| Optimize | `INSERT INTO {T} ({T}) VALUES ('optimize')` |
| Rebuild | `INSERT INTO {T} ({T}) VALUES ('rebuild')` |
| IntegrityCheck (null) | `INSERT INTO {T} ({T}) VALUES ('integrity-check')` |
| IntegrityCheck (false/true) | `INSERT INTO {T} ({T}, {rank}) VALUES ('integrity-check', 0\|1)` |

Rendering rules: no trailing semicolon; keywords honor the context `KeywordCase`; identifiers are quoted
exactly like `MakeDropTableIfExists` (`quoteIdentifiers ? dialect.QuoteIdentifier(name) : name`) for both
the table name and `rank`; integers are invariant-formatted (no thousands separators, minus preserved);
the returned parameter list is empty (the operation and its argument are inline literals).

Unsupported-provider contract: capability guard is evaluated first —
`if (dialect.SqliteFunctions is null || !dialect.SupportsTableFunction("fts5")) throw new NotSupportedException($"{dialect.GetType().Name} does not support SQLite FTS5 maintenance commands.")`.
Never use `SupportsFullText` (that is the cross-provider `contains`/`freetext` surface). A non-mutating
context (`!is IMutationExecutor`) throws `NotSupportedException` from the terminal.

Dispatch/execution: `DataContext.BuildMutationSql` (case added near `TruncateCommand`) routes
`SqliteFts5Command` to a private `BuildSqliteFts5Sql` that calls `MakeSqliteFts5(Dialect, QuoteIdentifiers,
command, KeywordCase)`. No `BuildReturningSql` case (no returning path). Execution flows through the
existing `IMutationExecutor.Execute` → `_executor.ExecuteNonQuery`; async through
`ExecuteNonQueryAsync`; `ToSql` through `IMutationExecutor.Render`.

Variant matrix (owned by the sibling test streams): each of the six operations; `IntegrityCheck` null /
false / true; `AutoMerge` 0 and 16; `Merge` negative / 0 / positive (sign preserved); `CrisisMerge` 0 / 1;
quoted vs unquoted identifiers; upper vs lower keyword case; snake/camel verbatim table names (dot is
literal, no trimming, whitespace rejected, NUL rejected, empty rejected); unselected terminal (all three);
`AutoMerge(17)`/`AutoMerge(-1)`/`CrisisMerge(-1)` out-of-range; other providers (PostgreSQL/SQL Server/
MySQL/MariaDB/ClickHouse) rejected with `NotSupportedException`; in-memory context rejected; real SQLite
execution against an FTS5 table (optimize/rebuild/integrity-check/merge round-trips).

Test strategy: SQL-generation tests in `tests/nextorm.sqlite.tests` (no database) assert the exact SQL
shapes and the validation/quote/keyword-case matrix; provider-rejection tests use the placeholder contexts
of the other provider test projects; real execution lives in `tests/nextorm.integration.tests`
(`SqliteSpecificTests`) against the bundled SQLite FTS5 module; core unit tests assert factory validation
and terminal behavior. Tests and docs are separate DO streams and are not edited by the implementation
stream.

Docs plan (6 pages, EN/RU mirrors): `docs/providers/sqlite.md`, `docs/guide/provider-specific/sqlite.md`,
`docs/advanced/limitations.md` and `docs/ru/providers/sqlite.md`,
`docs/ru/guide/provider-specific/sqlite.md`, `docs/ru/advanced/limitations.md` — replace the
"FTS5 maintenance/control deferred to #195/#196" notes with the shipped builder surface; the
comparison/spec pages are tracked separately.

DO units:
- D195.0 contract/preflight — status file + frozen FC195-1 + evidence contract (this unit).
- D195.1 semantic command + builder + factory — internal command model (`SqliteFts5Command`,
  `SqliteFts5Operation`, stable numeric values), public `SqliteFts5CommandBuilder` (immutability,
  validation, terminals), factory `CreateSqliteFts5CommandBuilder`.
- D195.2 renderer + dispatch + capability guard — `SqlMutationBuilder.MakeSqliteFts5` + capability
  guard + `DataContext.BuildMutationSql`/`BuildSqliteFts5Sql`.
- D195.3 SQL-generation + core + provider-rejection tests.
- D195.4 real-SQLite execution tests.
- D195.5 docs EN+RU.
- D195.6 verification/handoff — CRLF normalization + `dotnet build nextorm.slnx -c Debug` and the
  frozen-contract handoff to D196 (D196 binds to FC195-1 unchanged; D195 already delivered the
  real-SQLite execution tests and the EN/RU docs).

D195 → D196 frozen-contract handoff: D195 owns the implementation, the unit/integration tests and the
EN/RU docs, all delivered in this cycle. D196 (follow-up, issue #196) is a LATER, SEPARATE task that binds
to the frozen FC195-1 without re-owning or re-delivering those artifacts; any change to the public
surface, validation or rendering requires a PLAN replan of D195 before D196 consumes it.

Perf decision: NONE. The builder is a one-time render/dispatch path (no per-row or per-query hot path);
the statement executes once. No benchmark or before/after measurement is required.

Recon decision: NO SPIKE. The factual preflight is already established: `SqliteDialect.SqliteFunctions`
is non-null and `SupportsTableFunction("fts5")` returns true (`SqliteDialect.cs:119,122`); bundled SQLite
3.53.4 has fts3/fts4/fts5 (D181 `fts-module-availability.log`); the FTS5 control interface is the standard
`INSERT INTO <table>(...) VALUES(...)` form. No unknown transport or server behavior to probe.

Unit mode: one current tree, sequential units (overlapping files). No worktree, no commits by this unit.

Evidence contract rv=1 (rows EN-01..EN-09):

| row | command / action | required result | actual result | owner |
|---|---|---|---|---|
| EN-01 | surface/route audit (Roslyn: extension resolution, `BuildMutationSql` switch, no name collision) | exact `file:line`; public surface matches FC195-1 | PASS — `SqliteFts5CommandBuilder.cs:14-134`: no public ctor, public `AutoMerge:36`/`CrisisMerge:48`/`Merge:59`/`Optimize:64`/`Rebuild:69`/`IntegrityCheck:75`/`ToSql:87`/`Execute:100`/`ExecuteAsync:114`; factory `DataContextExtensions.cs:1003` → internal ctor `:1011`; `SqliteFts5Command` refs: builder `SqliteFts5CommandBuilder.cs:123`, dispatch `DataContext.cs:1116`, `BuildSqliteFts5Sql` `DataContext.cs:1198`, renderer `SqlMutationBuilder.cs:486,552`; no name collision | coder (D195) |
| EN-02 | `dotnet test tests/nextorm.core.tests -c Debug` | exit 0 | PASS — exit 0; 1712 total / 0 failed / 0 skipped (r2, after the fix + new variant tests); `/tmp/nextorm-d195-r1/core-tests-r2.log` | tests (D195) |
| EN-03 | `dotnet test tests/nextorm.sqlite.tests -c Debug` | exit 0 | PASS — exit 0; 1135 total / 0 failed / 1 skipped (r2, after the fix + regression tests); `/tmp/nextorm-d195-r1/sqlite-tests-r2.log` | tests (D195) |
| EN-04 | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | executed not skipped; SQLite FTS5 tests pass | PASS (r2) — full integration `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` → exit 0 → Total 3311 / Passed 3114 / Failed 0 / Skipped 197, all 5 providers executed (PostgreSQL, SQL Server, MySQL, ClickHouse, SQLite); `/tmp/nextorm-d195-r1/integration-r2.log`. D195 confirmation filter `--filter FullyQualifiedName~Fts5Maintenance` → 11/11 passed, 0 failed, 0 skipped; `/tmp/nextorm-d195-r1/d195-fts5maintenance-r2.log` | tests (D195) |
| EN-05 | `dotnet build nextorm.slnx -c Debug` | `0 Warning(s) 0 Error(s)` | PASS — exit 0, `Build succeeded`, `0 Warning(s) 0 Error(s)`; `/tmp/nextorm-d195-r1/build-debug.log` | coder (D195) |
| EN-06 | coverage `.github/workflows/dotnet.yml:45` collect + `:55-59` reportgenerator | line ≥ 85 / branch ≥ 75 (hard only on `main`) | PASS (r2) — coverage collect+reportgenerator → exit 0 → Line 88.6% / Branch 80% (25164/31423), thresholds 85/75; report `/tmp/nextorm-d195-r1/coverage-report-r2/Summary.txt`; per-class `SqliteFts5Command` 100%/100%, `SqliteFts5CommandBuilder` 100%/100%, `SqlMutationBuilder` 96.0%/92.4%; logs `coverage-r2.log`, `coverage-report-r2.log` | coder (D195) |
| EN-07 | docs audit (6 pages EN/RU) | deferred notes replaced; EN/RU parity | PASS — 6 pages EN/RU updated; `sqlite.md`/`guide/provider-specific/sqlite.md`/`advanced/limitations.md` in both trees document the shipped builder surface with parity | docs (D195) |
| EN-08 | `dotnet docfx docs/docfx.json` | 0 errors | PASS — exit 0, 2 pre-existing warnings, 0 errors; `/tmp/nextorm-d195-r1/docfx.log` | docs (D195) |
| EN-09 | `git diff --check` + final reconciliation | no whitespace errors; planned files only | PASS — `git diff --check` exit 0; 5 source files (2 new) + this status file; CRLF verified (total lines == CRLF lines on all 6) | coder (D195) |

Evidence dir: `/tmp/nextorm-d195-r1/`. CHECK re-gather budget: max 2, owner `check`.
---END PLAN---

## Acceptance criteria (A-01..A-10)

- **A-01** Exactly the frozen public maintenance surface (`SqliteFts5CommandBuilder` +
  `CreateSqliteFts5CommandBuilder` + the six operations + three terminals) dispatched through the mutation
  pipeline. Negative: no maintenance member added to `SqlFunctions.Sqlite`; no `UserMerge`; no returning
  terminal; no public ctor.
- **A-02** Safe, deterministic rendering for all six operations, including unusual identifiers, with
  identifiers always safely double-quoted. Negative: quotes/`;` cannot escape into SQL;
  empty/whitespace/NUL names rejected; quoting is not conditional on the context setting.
- **A-03** `AutoMerge` 0..16, `CrisisMerge` nonnegative, `Merge` any signed int rendered unchanged (never
  abs). Negative: out-of-range throws the specified argument exceptions; no FTS3/4 `merge=X,Y` form.
- **A-04** Three `IntegrityCheck` forms: omitted → one-column; `false` → `0`; `true` → `1`, with the
  specified SQL and behavior. Negative: stale external content with `true` produces the native error, not
  silently ignored.
- **A-05** Every unsupported provider rejects before DB access with its dialect name. Negative: no fallback
  to ordinary INSERT / generic message; rejection also when either capability signal is absent.
- **A-06** All six commands execute on real SQLite FTS5 through sync and async terminals; the index stays
  queryable. Negative: native failures propagate; no fabricated affected-row value.
- **A-07** `Rebuild` repairs a stale external-content FTS5 index; contentless `Rebuild` is rejected
  natively. Negative: no claim that every FTS5 table supports Rebuild; native error not hidden.
- **A-08** Selection is immutable; unselected terminals throw `InvalidOperationException`; the cancellation
  token is forwarded. Negative: reusing a root cannot change a selected command; no queueing.
- **A-09** EN+RU docs explain the frozen API, limits, result semantics and provider restrictions; the
  deferred-maintenance statement is removed. Negative: no public link to `docs/specs/**`.
- **A-10** Clean Debug build (0/0), required tests, coverage ≥85/75, DocFX build. Negative: warnings,
  failed tests, or skipped providers are not reported as passing evidence.

### Criterion → test-symbol mapping (A-01..A-10)

| criterion | covering test symbols / evidence |
|---|---|
| A-01 | Roslyn surface (EN-01); `tests/nextorm.core.tests/CreateCommandBuilderFactoryTests.cs` name-set; `SqlFunctions.Sqlite` has none of the six names. |
| A-02 | `SqliteFts5MaintenanceSqlGenerationTests.TableName_ShouldBeQuotedAsOneIdentifier` (:85), `UnquotedContext_ShouldStillQuoteIdentifiers` (:109), `UnquotedContext_TableName_ShouldBeQuotedAsOneIdentifier` (:131), `LowercaseUnquotedDefaults_ShouldQuoteIdentifiersAndKeepLowercaseKeywords` (:118); `SqliteFts5CommandBuilderTests.TableNameWithSurroundingSpaces_ShouldBePreservedAndQuoted` (:405), `KeywordCaseAndQuoting_ShouldNotChangeTheQuotedIdentifierShape` (:419), factory guards (:81/:90). |
| A-03 | `AutoMerge_InRange`/`AutoMerge_OutOfRange`, `CrisisMerge_Negative/Zero/Positive` (:271/:283/:385), `Merge_Zero_ShouldRender` (:394), `Merge_SignedValue_ShouldRenderUnchangedWithoutAbs` (:292); sqlite `IntegerArgument_ShouldRenderInvariantUnchanged` (:93); integration `Fts5Maintenance_ExtremeMergeValue_ShouldMatchCanonicalDirectSql` (:1904), `Fts5Maintenance_ExtremeCrisisMergeValue_ShouldMatchCanonicalDirectSql` (:1938). |
| A-04 | `IntegrityCheck_OmittedFlag` (:211), `IntegrityCheck_ExplicitFlag` (:220), `ExplicitNullFlag_ShouldMatchTheOmittedForm` (:231), sqlite (:60-78); integration `Fts5Maintenance_IntegrityCheck_OmittedAndFalse_ShouldSucceed_TrueOnStale_ShouldThrowNative` (:1794). |
| A-05 | the five provider `SqliteFunctionsRejectionTests` (3 tests each) + core in-memory rejection (:307/:317/:327) + isolated signals `FunctionsWithoutTableFunction_AllTerminals_ShouldThrowNotSupported` (:430), `TableFunctionWithoutFunctions_AllTerminals_ShouldThrowNotSupported` (:438). |
| A-06 | integration `Fts5Maintenance_AllOperations_ShouldExecuteAndKeepIndexQueryable` (:1735), `...ExecuteAsyncAndKeepIndexQueryable` (:1755); filtered rerun 11/11 (`d195-fts5maintenance-r2.log`). |
| A-07 | integration `Fts5Maintenance_Rebuild_ShouldRepairStaleExternalContentIndex` (:1773), `Fts5Maintenance_RebuildOnContentlessTable_ShouldThrowNativeError` (:1817). |
| A-08 | `Unselected_ToSql/Execute/ExecuteAsync` (:107/:117/:127), `Reselection...` (:155, :164), integration `Fts5Maintenance_PreCancelledToken_ShouldNotExecute` (:1886). |
| A-09 | the six docs pages (anchors already in the file) + the no-`specs`-link check. |
| A-10 | build-r2 0/0; core-tests-r2 1712/0/0; sqlite-tests-r2 1135/0/1; provider tests-r2 all 0 failed; integration-r2 3311/3114/0/197 all 5 providers; coverage-r2 88.6%/80%; docfx 0 errors; `git diff --check` 0. |

## Durable state

- cycle `N=1` · plan revision **`r=1`** · attempt **`n=2/3`** · contract/evidence revision **`rv=1`** —
  carried in this file; a session/`task_id` reset never resets `r`, `n` or the defect history.
- DO ledger:

| unit | status | evidence |
|---|---|---|
| D195.0 contract/preflight | done | this status file + frozen FC195-1 + evidence contract rv=1 |
| D195.1 semantic command + builder + factory | done | `src/nextorm.core/Query/Mutations/SqliteFts5Command.cs` (command + enum, stable numeric values), `src/nextorm.core/Builders/SqliteFts5CommandBuilder.cs` (immutable builder, validation, terminals), `src/nextorm.core/DataContext/DataContextExtensions.cs` (factory) |
| D195.2 renderer + dispatch + capability guard | done | `src/nextorm.core/DataContext/SqlMutationBuilder.cs` (`MakeSqliteFts5` + capability guard + helper forms), `src/nextorm.core/DataContext/DataContext.cs` (`BuildMutationSql` case + `BuildSqliteFts5Sql`) |
| D195.3 SQL-generation + core + provider-rejection tests | done | `tests/nextorm.sqlite.tests` + core unit tests + five `SqliteFunctionsRejectionTests.cs`; `/tmp/nextorm-d195-r1/{core-tests-r2,sqlite-tests-r2,postgres-tests-r2,sqlserver-tests-r2,mysql-tests-r2,mariadb-tests-r2,clickhouse-tests-r2}.log` |
| D195.4 real-SQLite execution tests | done | `SqliteSpecificTests` + full integration; `/tmp/nextorm-d195-r1/{integration-sqlite,integration-r2,d195-fts5maintenance-r2}.log` |
| D195.5 docs EN+RU | done | 6 pages (EN/RU); `/tmp/nextorm-d195-r1/docfx.log` (0 errors) |
| D195.6 verification/handoff | done | build exit 0, `0 Warning(s) 0 Error(s)`, `/tmp/nextorm-d195-r1/build-r2.log`, all files CRLF-normalized; frozen-contract handoff to D196 (D196 binds to FC195-1 only, no re-delivery) |

### Defect history (stable keys)

| defect key | state | observed r/n | fixes applied | evidence / note |
|---|---|---|---|---|
| `D195-FTS5-IDENTIFIER-QUOTING` | fixed (CHECK FAIL → DO correction) | r=1 n=1/3 | 1 (always-quote) | P1: `MakeSqliteFts5` emitted `command.TableName` verbatim when `quoteIdentifiers == false` (the `DataContextBuilder` default) and only conditionally quoted `rank`; fixed by unconditionally quoting both via `ISqlDialect.QuoteIdentifier`, dropping the `quoteIdentifiers` parameter and updating `DataContext.BuildSqliteFts5Sql`. Evidence: new regression tests `SqliteFts5MaintenanceSqlGenerationTests.UnquotedContext_ShouldStillQuoteIdentifiers`, `LowercaseUnquotedDefaults_ShouldQuoteIdentifiersAndKeepLowercaseKeywords`, `UnquotedContext_TableName_ShouldBeQuotedAsOneIdentifier` (red `regression-red.log` exit 2, 6/6 failed → green `regression-green.log` exit 0, 6/6 passed); reruns `build-r2.log` (0/0), `core-tests-r2.log` (1712/0/0), `sqlite-tests-r2.log` (1135/0/1); textual `ToSql()` assertions strengthened in `SqliteSpecificTests`. Correction left `r=1`, incremented `n` to 2/3 |

## Decisions

- **Frozen surface.** FC195-1 is fixed: public surface, validation, rendering and dispatch exactly as in
  the PLAN. Unit/integration tests and docs consume it unchanged.
- **Capability gate.** Use `SqliteFunctions is null || !SupportsTableFunction("fts5")`; explicitly NOT
  `SupportsFullText`.
- **Quoting/keyword case (revised r=1, n=2/3).** FTS5 identifiers are ALWAYS quoted through
  `ISqlDialect.QuoteIdentifier`, independent of `quoteIdentifiers` (FC195-1); `keywordCase` is still
  honored. `MakeDropTableIfExists` remains the quoting-helper precedent.
- **No `UserMerge`.** The linq2db `UserMerge` op is out of scope; only the six requested operations.
- **No returning path.** The FTS5 control interface reports only an affected-row count; no
  `Returning`/result materialization and no `BuildReturningSql` case.
- **Perf**: not required (one-time render/dispatch).
- **Reconnaissance**: no spike (factual preflight established).
- **Unit mode**: one current tree, sequential units; no worktree/merge by this unit.

## Risks / known issues

- **Cross-stream contract drift.** The tests/docs streams must consume FC195-1 exactly; any surface change
  requires a D195 replan before D196 consumes it.
- **Keyword-case/quoting interpretation (resolved).** The initial stream honored `QuoteIdentifiers`
  (the codebase convention), which on the unquoted `DataContextBuilder` default broke FC195-1. CHECK FAIL
  `D195-FTS5-IDENTIFIER-QUOTING` fixed it by always quoting the FTS5 identifiers; keyword casing still
  honors `KeywordCase`.
- **`rank` reserved word.** `rank` is a hidden FTS5 column; quoting it as an identifier is required when
  `quoteIdentifiers` is set. The first column is the table name itself (the FTS5 control interface), not a
  user column.

## Perf measurement

**NONE.** The builder renders/dispatches a single maintenance statement; there is no per-row or per-query
hot path, so no benchmark or acceptance before/after is required.

## Reconnaissance

**NO SPIKE.** Factual preflight: `SqliteDialect.SqliteFunctions => SqliteFunctionRenderer.Instance`
(`src/nextorm.sqlite/SqliteDialect.cs:119`) and `SupportsTableFunction("fts5") => true` (`:122`); the bundled
SQLite 3.53.4 module availability was established by D181 (`artifacts/d181/fts-module-availability.log`);
the FTS5 control interface is the documented `INSERT INTO <table>(...) VALUES(...)` form. No unknown
transport/server behavior.

## Unit mode

One current tree, sequential units (overlapping files). No worktree, no commits by this unit; the later
collection unit owns auto-commit. Tests and docs are separate DO streams.

## Evidence (raw)

All raw logs under `/tmp/nextorm-d195-r1/`.

| command | exit | numbers | log |
|---|---|---|---|
| `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning(s) 0 Error(s) | `/tmp/nextorm-d195-r1/build-debug.log` |
| `dotnet test tests/nextorm.core.tests -c Debug` | 0 | 1702 total / 0 failed / 0 skipped | `/tmp/nextorm-d195-r1/core-tests.log` |
| `dotnet test tests/nextorm.sqlite.tests -c Debug` | 0 | 1130 / 0 / 1 skipped | `/tmp/nextorm-d195-r1/sqlite-tests.log` |
| `dotnet test tests/nextorm.postgres.tests -c Debug` | 0 | 786 / 0 / 0 | `/tmp/nextorm-d195-r1/postgres-tests.log` |
| `dotnet test tests/nextorm.sqlserver.tests -c Debug` | 0 | 710 / 0 / 0 | `/tmp/nextorm-d195-r1/sqlserver-tests.log` |
| `dotnet test tests/nextorm.mysql.tests -c Debug` | 0 | 292 / 0 / 0 | `/tmp/nextorm-d195-r1/mysql-tests.log` |
| `dotnet test tests/nextorm.mariadb.tests -c Debug` | 0 | 199 / 0 / 0 | `/tmp/nextorm-d195-r1/mariadb-tests.log` |
| `dotnet test tests/nextorm.clickhouse.tests -c Debug` | 0 | 579 / 0 / 0 | `/tmp/nextorm-d195-r1/clickhouse-tests.log` |
| SQLite integration `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class NextORM.Integration.Tests.SqliteSpecificTests` | 0 | 87 / 0 / 0 | `/tmp/nextorm-d195-r1/integration-sqlite.log` |
| `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor` | 0 | Total 3311 / Passed 3114 / Failed 0 / Skipped 197; all 5 providers executed (PG/SQLServer/MySQL/SQLite/ClickHouse) | `/tmp/nextorm-d195-r1/integration.log` |
| coverage collect + reportgenerator | 0 / 0 | Line 88.6% (47547/53639), Branch 80% (25168/31427), thresholds 85/75 | report `/tmp/nextorm-d195-r1/coverage-report/Summary.txt`; logs `coverage.log` / `coverage-report.log` |
| `dotnet docfx docs/docfx.json` | 0 | 2 pre-existing warnings, 0 errors | `/tmp/nextorm-d195-r1/docfx.log` |
| `git diff --check` | 0 | no whitespace errors | — |

Correction reruns (r=1, n=2/3; full container integration + coverage re-run in this correction):

| command | exit | numbers | log |
|---|---|---|---|
| `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning(s) 0 Error(s) | `/tmp/nextorm-d195-r1/build-r2.log` |
| `dotnet test tests/nextorm.core.tests -c Debug` | 0 | 1712 / 0 failed / 0 skipped | `/tmp/nextorm-d195-r1/core-tests-r2.log` |
| `dotnet test tests/nextorm.sqlite.tests -c Debug` | 0 | 1135 / 0 failed / 1 skipped | `/tmp/nextorm-d195-r1/sqlite-tests-r2.log` |
| `dotnet test tests/nextorm.postgres.tests -c Debug` | 0 | 786 / 0 / 0 | `/tmp/nextorm-d195-r1/postgres-tests-r2.log` |
| `dotnet test tests/nextorm.sqlserver.tests -c Debug` | 0 | 710 / 0 / 0 | `/tmp/nextorm-d195-r1/sqlserver-tests-r2.log` |
| `dotnet test tests/nextorm.mysql.tests -c Debug` | 0 | 292 / 0 / 0 | `/tmp/nextorm-d195-r1/mysql-tests-r2.log` |
| `dotnet test tests/nextorm.mariadb.tests -c Debug` | 0 | 199 / 0 / 0 | `/tmp/nextorm-d195-r1/mariadb-tests-r2.log` |
| `dotnet test tests/nextorm.clickhouse.tests -c Debug` | 0 | 579 / 0 / 0 | `/tmp/nextorm-d195-r1/clickhouse-tests-r2.log` |
| regression red `--filter` (sqlite, pre-fix) | 2 | 6 / 6 failed / 0 | `/tmp/nextorm-d195-r1/regression-red.log` |
| regression green `--filter` (sqlite, post-fix) | 0 | 6 / 0 failed / 0 | `/tmp/nextorm-d195-r1/regression-green.log` |
| `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | 0 | Total 3311 / Passed 3114 / Failed 0 / Skipped 197; all 5 providers executed (PG/SQLServer/MySQL/ClickHouse/SQLite) | `/tmp/nextorm-d195-r1/integration-r2.log` |
| SQLite FTS5 confirmation `--filter FullyQualifiedName~Fts5Maintenance` | 0 | 11 / 0 failed / 0 skipped | `/tmp/nextorm-d195-r1/d195-fts5maintenance-r2.log` |
| coverage collect + reportgenerator | 0 / 0 | Line 88.6% / Branch 80% (25164/31423), thresholds 85/75 | report `/tmp/nextorm-d195-r1/coverage-report-r2/Summary.txt`; logs `coverage-r2.log` / `coverage-report-r2.log` |

## Done / Verified

- **Done (DO).** D195.0–D195.6: contract/preflight; internal command + enum, renderer + capability guard,
  immutable public builder, factory + dispatch; SQL-gen + core + provider-rejection tests; real-SQLite
  execution tests; docs EN+RU; verification. Build/exit 0 with `0 Warning(s) 0 Error(s)`.
- **Correction (r=1, n=2/3).** CHECK FAIL on P1 `D195-FTS5-IDENTIFIER-QUOTING` → always-quote the FTS5
  table name + `rank`, drop the `quoteIdentifiers` parameter and update the caller; regression/variant
  tests added and the integration assertions strengthened. Red→green and all reruns recorded green above.
- **CHECK PASS (r=1, n=2/3).** CHECK re-adjudicated EN-01..EN-09 on the corrected tree: all A-01..A-10
  met; the P1 defect `D195-FTS5-IDENTIFIER-QUOTING` is fixed with no residual path (identifiers always
  quoted through `ISqlDialect.QuoteIdentifier`, `quoteIdentifiers` parameter dropped, caller updated).
  Evidence re-used: build-r2 0/0; core-tests-r2 1712/0/0; sqlite-tests-r2 1135/0/1; providers
  786/710/292/199/579 all 0 failed; integration-r2 3311/3114/0/197 with all 5 providers executed
  (Fts5Maintenance 11/11); coverage-r2 88.6%/80%; docfx 0 errors; `git diff --check` 0. Verdict summary:
  **PASS — all A-01..A-10 met; D195-FTS5-IDENTIFIER-QUOTING fixed, no residual path.**
- **ACT (done).** Cycle finalized: status `done`, counters `N=1 · r=1 · n=2/3 · rv=1`; task files
  committed; collection row updated; issue #195 closed.
- **Frozen-contract handoff for D196.** D196 (issue #196, a later, separate task) binds to the frozen
  **FC195-1 (rv=1)** without re-owning or re-delivering artifacts — D195 already delivered the
  implementation, the unit/integration tests (incl. real SQLite FTS5) and the EN+RU docs. Any change to
  the public surface, validation or rendering requires a D195 replan before D196 consumes it.

## Next plan

Proceed to CHECK (r=1, n=2/3, rv=1) on the corrected tree. CHECK re-adjudicates EN-01..EN-09 (re-gather
budget max 2); the EN-04 container integration and EN-06 coverage reruns already completed green
(integration-r2 3311/3114/0/197, all 5 providers; coverage-r2 88.6/80). On accepted CHECK, D195.6 hands
the frozen FC195-1 to D196 (a later, separate task).

## Changed files

Source (this DO stream):

- `src/nextorm.core/Query/Mutations/SqliteFts5Command.cs` (new)
- `src/nextorm.core/DataContext/SqlMutationBuilder.cs` (`MakeSqliteFts5` + helpers)
- `src/nextorm.core/Builders/SqliteFts5CommandBuilder.cs` (new)
- `src/nextorm.core/DataContext/DataContextExtensions.cs` (factory)
- `src/nextorm.core/DataContext/DataContext.cs` (`BuildMutationSql` case + `BuildSqliteFts5Sql`)

Tests:

- `tests/nextorm.core.tests/SqliteFts5CommandBuilderTests.cs` (new)
- `tests/nextorm.core.tests/CreateCommandBuilderFactoryTests.cs`
- `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs` (new)
- `tests/nextorm.postgres.tests/SqliteFunctionsRejectionTests.cs`
- `tests/nextorm.sqlserver.tests/SqliteFunctionsRejectionTests.cs`
- `tests/nextorm.mysql.tests/SqliteFunctionsRejectionTests.cs`
- `tests/nextorm.mariadb.tests/SqliteFunctionsRejectionTests.cs`
- `tests/nextorm.clickhouse.tests/SqliteFunctionsRejectionTests.cs`
- `tests/nextorm.integration.tests/SqliteSpecificTests.cs`

Docs pages (6, EN/RU):

- `docs/providers/sqlite.md`
- `docs/guide/provider-specific/sqlite.md`
- `docs/advanced/limitations.md`
- `docs/ru/providers/sqlite.md`
- `docs/ru/guide/provider-specific/sqlite.md`
- `docs/ru/advanced/limitations.md`

Task/status:

- `docs/specs/status/rc1-tail-195-fts5-maintenance-1.md` (this file)

Correction (r=1, n=2/3) touched in addition:

- `src/nextorm.core/DataContext/SqlMutationBuilder.cs` (always-quote + parameter drop)
- `src/nextorm.core/DataContext/DataContext.cs` (caller update)
- `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs` (unquoted-context regression)
- `tests/nextorm.core.tests/SqliteFts5CommandBuilderTests.cs` (capability/quoting/variant rows)
- `tests/nextorm.integration.tests/SqliteSpecificTests.cs` (textual `ToSql()` + error-code assertions)
- `docs/specs/status/rc1-tail-195-fts5-maintenance-1.md` (this file)

## Pointers

- Issue #195 (follow-up to #181); follow-up #196 is a later, separate task that binds to the frozen
  FC195-1 (D195 owns and has delivered the real-SQLite tests + docs).
- Predecessor: `docs/specs/status/rc1-181-sqlite-fts-1.md` (FTS query + FROM surface; maintenance deferred
  to #195/#196).
- Skeleton: `docs/specs/status/rc1-tail-198-ch-native-json-1.md`.
- Source: `src/nextorm.sqlite/SqliteDialect.cs:119,122` (`SqliteFunctions`, `SupportsTableFunction("fts5")`).
- Source: `src/nextorm.core/DataContext/SqlMutationBuilder.cs` (`MakeDropTableIfExists` quoting precedent;
  `MakeSqliteFts5`).
- Source: `src/nextorm.core/DataContext/DataContext.cs` (`BuildMutationSql`, `BuildTruncateSql`,
  `BuildSqliteFts5Sql`).
- Source: `src/nextorm.core/Query/Mutations/TruncateCommand.cs` (`MutationCommand` precedent).
- Source: `src/nextorm.core/Builders/TruncateBuilder.cs` (builder/`IMutationExecutor` precedent).
- Source: `src/nextorm.core/DataContext/Roles/IMutationExecutor.cs` (Render/Execute/ExecuteAsync).
- Evidence: `/tmp/nextorm-d195-r1/build-debug.log`.

## Evidence contract (rv=1; EN-01..EN-09)

See the table in the verbatim PLAN above. Summary ownership: EN-01/05/06/09 — coder (D195); EN-02/03/04 —
tests (D195); EN-07/08 — docs (D195). CHECK re-gather budget: max 2, owner `check`.

**Priority floor:** all `A-*` acceptance criteria and every evidence row are P1 by construction (nextorm
overlay) — none may be downgraded, deferred or dropped.

## Progress log

- 2026-10-07T00:33Z | PLAN | r=1 | iteration n=1/3 | PLAN ready (autonomous mode — no confirmation) | this file
- Notice: host has no `pdca-orchestrator` agent and no `todowrite` for subagents; flat primary drives this cycle and this status file carries the log.
- 2026-10-07T00:36Z | DO | r=1 | n=1/3 | D195.1–D195.4 implementation written (command+enum, renderer, builder, factory+dispatch) | `src/nextorm.core/{Query/Mutations/SqliteFts5Command.cs,Builders/SqliteFts5CommandBuilder.cs,DataContext/SqlMutationBuilder.cs,DataContext/DataContextExtensions.cs,DataContext/DataContext.cs}`
- 2026-10-07T00:36Z | DO | r=1 | n=1/3 | CRLF normalized; `dotnet build nextorm.slnx -c Debug` exit 0, Build succeeded, 0 Warning(s) 0 Error(s) | `/tmp/nextorm-d195-r1/build-debug.log`
- 2026-10-07T00:50Z | PLAN | r=1 | n=1/3 | plan recorded | FC195-1
- 2026-10-07T00:50Z | DO | r=1 | n=1/3 | implementation complete (command+enum, renderer+guard, immutable builder, factory+dispatch) | `src/nextorm.core/{Query/Mutations/SqliteFts5Command.cs,Builders/SqliteFts5CommandBuilder.cs,DataContext/SqlMutationBuilder.cs,DataContext/DataContextExtensions.cs,DataContext/DataContext.cs}`
- 2026-10-07T00:50Z | DO | r=1 | n=1/3 | docs EN+RU complete (6 pages) | `docs/{providers/sqlite.md,guide/provider-specific/sqlite.md,advanced/limitations.md}` + `docs/ru/{providers/sqlite.md,guide/provider-specific/sqlite.md,advanced/limitations.md}`
- 2026-10-07T00:50Z | DO | r=1 | n=1/3 | tests complete: core 1702/0/0, sqlite 1130/0/1, providers 786/710/292/199/579 all 0 failed, SQLite integration 87/0/0, full integration 3311 total / 3114 passed / 0 failed / 197 skipped (all 5 providers executed) | `/tmp/nextorm-d195-r1/{core-tests,sqlite-tests,postgres-tests,sqlserver-tests,mysql-tests,mariadb-tests,clickhouse-tests,integration-sqlite,integration}.log`
- 2026-10-07T00:50Z | DO | r=1 | n=1/3 | evidence green: coverage line 88.6% (47547/53639) / branch 80% (25168/31427), docfx 0 errors (2 pre-existing warnings), `git diff --check` exit 0 | `/tmp/nextorm-d195-r1/{coverage-report/Summary.txt,docfx.log}`
- 2026-10-07T00:58Z | CHECK | r=1 | n=1/3 | FAIL: P1 `D195-FTS5-IDENTIFIER-QUOTING` — `MakeSqliteFts5` emitted `command.TableName` verbatim when `quoteIdentifiers == false` (the `DataContextBuilder` default) and only conditionally quoted `rank`; FC195-1 requires always-quoted identifiers (breaks unusual names, permits identifier/SQL injection) | defect key `D195-FTS5-IDENTIFIER-QUOTING`
- 2026-10-07T01:04Z | DO | r=1 | n=2/3 | correction: always-quote the FTS5 table name and `rank` via `ISqlDialect.QuoteIdentifier`, drop the now-unused `quoteIdentifiers` parameter and update `DataContext.BuildSqliteFts5Sql`; add regression + closed variant tests; strengthen integration `ToSql()`/error-code assertions | `src/nextorm.core/DataContext/{SqlMutationBuilder.cs,DataContext.cs}`, `tests/nextorm.core.tests/SqliteFts5CommandBuilderTests.cs`, `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs`, `tests/nextorm.integration.tests/SqliteSpecificTests.cs`
- 2026-10-07T01:04Z | DO | r=1 | n=2/3 | red→green: `regression-red.log` exit 2 (6/6 failed, pre-fix) → `regression-green.log` exit 0 (6/6 passed); reruns build 0/0, core 1712/0/0, sqlite 1135/0/1, providers 786/710/292/199/579 all 0 failed | `/tmp/nextorm-d195-r1/{build-r2,core-tests-r2,sqlite-tests-r2,postgres-tests-r2,sqlserver-tests-r2,mysql-tests-r2,mariadb-tests-r2,clickhouse-tests-r2,regression-red,regression-green}.log`
- 2026-10-07T01:07Z | DO | r=1 n=2/3 | boundary integration rerun PASS (3311/3114/0/197, all 5 providers) | integration-r2.log
- 2026-10-07T01:09Z | DO | r=1 n=2/3 | coverage rerun PASS (line 88.6% / branch 80%) | coverage-report-r2/Summary.txt
- 2026-10-07T01:14Z | CHECK | r=1 n=2/3 | PASS — all A-01..A-10 met; D195-FTS5-IDENTIFIER-QUOTING fixed, no residual path | check verdict
