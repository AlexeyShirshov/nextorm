# SQLite `ToDataReader`/`ToDataReaderAsync` for non-LOB projections — issue #134 (task D134)

- task: D134
- issue: #134 (https://github.com/AlexeyShirshov/nextorm/issues/134)
- collection: `1.0.9-rc1-tail`
- group: G1
- branch: `1.0.9-rc1`
- status: **done** (CHECK r=1/n=3 PASS; ACT done)
- cycle: N=1
- plan revision: r=1
- attempt: n=3/3
- contract: rv=1
- mode: autonomous (collection); auto-commit of task files only at ACT; no push/merge
- milestone: design `issue-189-todatareader-sqlite.md` says `1.0.9-rc2`; implemented in **`1.0.9-rc1`** per the
  `nextorm-pdca` overlay invariant 8 (milestone stays `1.0.9-rc1`). Recorded as notice, the milestone is not moved.

## Goal

Adopt approved design **variant A**: make `ToDataReader`/`ToDataReaderAsync` work on SQLite for **non-LOB
projections** by routing the locator dialect through the existing locator-free `OpenResultReader`/
`OpenResultReaderAsync` seam (default `sequentialAccess: false`), while PostgreSQL and SQL Server keep the
existing `OpenLobReader` LOB path unchanged. Remain fail-closed on dialects without sequential access
(MySQL/MariaDB, ClickHouse) and on the in-memory provider. Preserve ownership/cache invariants: the caller
owns the returned reader; the per-call command is released on dispose; the connection stays open; the plan is
prepared per call with `storeInCache: false` and no `QueryCommand.Cache` mutation.

Design source: `docs/specs/design/issue-189-todatareader-sqlite.md` (variant A).

## Acceptance criteria

- **R01** (SQLite non-LOB, sync + async): `ToDataReader`/`ToDataReaderAsync` return a reader on SQLite whose
  `FieldCount` equals the projection column count (no trailing `rowid` locator); ordinals map to `Select`
  order; every row is readable forward-only; no `NotSupportedException`.
- **R02** (buffered LOB in a multi-column SQLite projection): a `byte[]`/`string` column inside a
  multi-column select is read **buffered** (not chunked `SqliteBlob`); values round-trip via `GetValue`/
  typed getters; no locator is exposed.
- **R03** (PG/SQL Server unchanged): the `OpenLobReader` path, sequential-access semantics, and existing
  `ToDataReader` tests stay green with no behavior change.
- **R04** (fail-closed): non-sequential dialects and the in-memory provider still throw `NotSupportedException`
  with a message naming the `ToDataReader` terminal; a lazy temp-table source (`AsTempTable`) fails closed in
  `PrepareResultCommand` with a terminal-appropriate message while `WriteCsv`/`WriteCsvAsync` keep the existing
  CSV wording (CSV label preserved).
- **R05** (surface parity): all four public overloads (sync/async × `CancellationToken`/no-token ×
  `ReadOnlySpan`/`object[]` params) route identically; signatures unchanged.
- **R06** (ownership): the returned reader is caller-owned; disposing it disposes the provider reader **and**
  the per-call command; the connection/context stay reusable afterwards.
- **R07** (cache invariants): the per-call plan uses `storeInCache: false`; no shared-command mutation
  (`QueryCommand.Cache` untouched) — shared `AnyCommand`/context cache unaffected.
- **R08** (streaming class rows): document/measure the streaming class-row case; `EntityBuilder<T>` is **N/A**
  (it exposes `Select<TResult>` → `QueryCommand<TResult>`, not `ToDataReader`); target/flush semantics N/A.
- **R09** (docs EN+RU): the EN and RU docs listed in the docs plan are consistent and remove the stale
  "not supported on SQLite" / `rowid` claims.
- **R10** (gates): Debug **and** Release build 0 warning / 0 error; real provider execution (SQLite unit +
  container integration) performed; perf measured against the cached-path acceptance baseline.

## DO tasks

- **D:01** — recon + baseline: Roslyn recon (EntityBuilder members, terminal signatures, guard/CSV callers);
  baseline acceptance benchmark **before** any source edit; restore `BenchmarkDotNet.Artifacts`.
- **D:02** — routing + messages: dialect routing in `ToDataReader`/`ToDataReaderAsync`; narrow
  `EnsureDataReaderSupported`; remove `LobDataReaderLocatorMessage`; generalize the `PrepareResultCommand`
  lazy temp-table message; XML-doc updates.
- **D:03** — unit/SQLite tests: core `LobDataReader`/routing tests and SQLite `ResultReaderTests` /
  `LobSqlGenerationTests` sync+async coverage.
- **D:04** — real SQLite + provider coverage: SQLite integration class, full container integration
  (PG/SQL Server/MySQL/ClickHouse), guard negatives.
- **D:05** — docs/registers: EN+RU docs, internal specs/registers (CSV label, design notice).
- **D:06** — gates/evidence: Debug+Release builds, coverage, docfx, CRLF/scope checks, E01–E13.

## Variant matrix

| # | Variant | Decision |
|---|---|---|
| V1 | Dialect routing: locator dialect (SQLite) → locator-free `OpenResultReader` (buffered, non-sequential); non-locator sequential (PG/SQL Server) → `OpenLobReader`; others fail closed | **Chosen (design A)** — required seam already exists (`WriteCsv`) |
| V2 | Keep the SQLite streaming path and hide the trailing `rowid` in `LobDataReader` | Rejected (design B) — needs read-order proof; risks `GetSchemaTable`/`GetValues`/by-name access |
| V3 | Keep the blanket SQLite rejection | Rejected — the no-locator path exists; blocks the #188 zero-materialization benchmark |
| V4 | Reject the whole terminal because one column is a LOB | Rejected — a shared terminal must not fail on one column; buffered read is documented instead |

## Test strategy (commands C01–C11)

- **C01** inner/rebuild: `dotnet build nextorm.slnx -c Debug` (affected rebuild; expect 0/0).
- **C02** boundary: `dotnet build nextorm.slnx -c Release` (0/0).
- **C03** inner (core): `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~LobDataReader"`.
- **C04** inner (core): `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~LobStreaming"`.
- **C05** inner (SQLite): `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~ResultReader"`.
- **C06** boundary (SQLite integration class, filtered).
- **C07** boundary (full container integration): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug`.
- **C08** coverage: `dotnet-coverage collect` with `coverage.settings.xml`.
- **C09** `reportgenerator` (line/branch vs `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`).
- **C10** boundary docs: `dotnet docfx docs/docfx.json`.
- **C11** hygiene: `git diff --check` (whitespace/CRLF) + scope `git status --porcelain`.

Selectors: `FullyQualifiedName~LobDataReader`, `FullyQualifiedName~LobStreaming`, `FullyQualifiedName~ResultReader`.
Inner loop is the affected filtered subset only; one full build + affected tests at the DO→CHECK boundary.

## Docs plan (EN + RU)

- `docs/guide/26-large-objects.md` + RU; `docs/guide/28-streaming-data.md` + RU.
- `docs/providers/overview.md` + RU; `docs/providers/sqlite.md` + RU; `docs/providers/postgres.md` + RU;
  `docs/providers/sqlserver.md` + RU; `docs/providers/in-memory.md` + RU.
- `docs/comparisons/capabilities.md` + RU.
- `docs/advanced/api-reference.md` + RU; `docs/advanced/limitations.md` + RU.
- Internal specs: `docs/specs/design/issue-189-todatareader-sqlite.md` (implementation notice),
  `docs/specs/comparison/linq2db-comparison.md` + RU, `docs/specs/comparison/capability-matrix.md`,
  `docs/specs/design/API-NAMING-REVIEW.md` (LOB1 register), `docs/specs/design/code-smells-review.md`
  (close the SQLite locator-backed `ToDataReader` item).
- Plus the mandatory cycle status file (bookkeeping, excluded from DocFX).
- `docs/comparison-benchmark-scenarios` (comparison benchmark scenario text) kept consistent.

## Perf-measurement decision

Runtime code path changes (routing + guard), so the cached-path acceptance benchmark is required:
baseline **before** code and candidate **after** code,
`[dotnet,run,--project,benchmarks/nextorm.benchmark,-c,Release,--,--anyCategories=acceptance]`,
7 cases, ≤120 s each, then `git checkout -- BenchmarkDotNet.Artifacts`.

## Evidence contract E01–E13

| ID | owner | applicability | required result |
|---|---|---|---|
| E01 | coder | always (before dispatch) | inner-loop validator `brief` exit 0 |
| E02 | coder | always | Roslyn recon: EntityBuilder N/A proof + CSV caller `file:line` |
| E03 | coder | perf path affected | baseline acceptance benchmark, 7 cases, exit 0, log path |
| E04 | coder | perf path affected | candidate acceptance benchmark, 7 cases, exit 0, log path |
| E05 | coder | always | C01 Debug build exit 0, 0 warning / 0 error |
| E06 | coder | always | C02 Release build exit 0, 0 warning / 0 error |
| E07 | coder | always | C03/C04 selected counts + exit 0 |
| E08 | coder | always | C05 SQLite unit tests + SQLite integration class exit 0 |
| E09 | coder | provider coverage | C07 container integration PG/SQLServer/MySQL/ClickHouse executed (not skipped) |
| E10 | coder | SQLite behavioral | real SQLite sync+async reader: FieldCount/ordinals/buffered LOB |
| E11 | coder | guards | non-sequential/in-memory/lazy-temp-table fail-closed; CSV wording preserved |
| E12 | coder | coverage | C08+C09 line/branch ≥ thresholds (or documented deviation) |
| E13 | coder | docs | EN+RU consistency + C10 docfx + C11 CRLF/scope |

CHECK re-gather budget: at most **2** targeted evidence requests per CHECK invocation.

## Reconnaissance (bounded Roslyn)

- `members NextORM.Core.EntityBuilder<TEntity>` — **no** `ToDataReader`, `ToStream`, `ToTextReader`. It exposes
  `Select<TResult>(Expression<Func<TEntity,TResult>>)` → `QueryCommand<TResult>` at
  `src/nextorm.core/Builders/EntityBuilder.cs:249`. → **R08 EntityBuilder N/A** confirmed.
- Four `ToDataReader` overloads: `QueryCommandExtensions.cs:306` (span/no-token), `:322` (span+token),
  `:354` (object[]/no-token), `:370` (object[]+token). Sibling streaming terminals: `ToStream` (`:96/:105`,
  `:212/:220`), `ToTextReader` (`:154/:163`, `:255/:263`).
- `callers EnsureDataReaderSupported` → `ToDataReader` `QueryCommandExtensions.cs:328` and
  `ToDataReaderAsync` `:376` (only these two).
- `callers OpenResultReader` → `WriteCsv` `src/nextorm.core/Query/QueryCommandExtensions.cs:412` (CSV caller)
  and `NextORM.Sqlite.Tests.ResultReaderTests.OpenResultReader_Sqlite_MultiColumn_ReadsEveryRowWithoutLocator`
  `tests/nextorm.sqlite.tests/ResultReaderTests.cs:54`.
- `callers OpenResultReaderAsync` → `WriteCsvAsyncCore` `QueryCommandExtensions.cs:454`.
- `callers PrepareResultCommand` → `OpenResultReader` `DataContext.cs:448`, `OpenResultReaderAsync` `:455`.

## Progress log

- 2026-10-06T08:07Z | DO | r=1 | n=1/3 | DO started | this file
- 2026-10-06T08:09Z | DO | r=1 | n=1/3 | baseline acceptance benchmark: exit 0, 7/7 cases, 48.08 s; `BenchmarkDotNet.Artifacts` restored clean | /tmp/D134-evidence/baseline-acceptance.log
- 2026-10-06T08:12Z | DO | r=1 | n=1/3 | C01 Debug build exit 0 (0 warning/0 error); C02 Release build exit 0 (0/0) | /tmp/D134-evidence/build-debug.log, /tmp/D134-evidence/build-release.log
- 2026-10-06T08:12Z | DO | r=1 | n=1/3 | C03 RED (21 selected, 2 failed: obsolete locator-dialect tests, now re-routed -> D:03 test stream); C04 GREEN (29/0); C05 GREEN (2/0) | /tmp/D134-evidence/c03-core-lobdatareader.log, c04-core-lobstreaming.log, c05-sqlite-resultreader.log
- 2026-10-06T08:12Z | DO | r=1 | n=1/3 | implementation complete (variant A): dialect routing, narrowed guard, terminal-labelled lazy-temp message, XML docs; C11 `git diff --check` exit 0; scope = 3 source files + this status file | this file

## DO r=1 record (variant A)

- **Recon (Roslyn)** — recorded in the Reconnaissance section above. EntityBuilder<TEntity> has no
  `ToDataReader`/`ToStream`/`ToTextReader` (N/A for R08); the CSV caller is
  `WriteCsv` → `OpenResultReader` at `src/nextorm.core/Query/QueryCommandExtensions.cs:412`; the only
  `EnsureDataReaderSupported` callers are the two `ToDataReader` terminals.
- **Baseline acceptance benchmark (before any source edit)** —
  `[dotnet,run,--project,benchmarks/nextorm.benchmark,-c,Release,--,--anyCategories=acceptance]` →
  exit **0**, **7/7** cases, global **48.08 s**; log `/tmp/D134-evidence/baseline-acceptance.log`;
  artifacts restored with `git checkout -- BenchmarkDotNet.Artifacts` (git status clean).
- **Implementation** —
  - `QueryCommandExtensions.cs`: `ToDataReader`/`ToDataReaderAsync` route by `Dialect.LobLocatorColumn`
    (`not null` → `OpenResultReader`/`Async`, else `OpenLobReader`/`Async`); `EnsureDataReaderSupported`
    locator branch removed (only non-sequential dialects fail closed, message now lists SQLite);
    `LobDataReaderLocatorMessage` removed; `<remarks>` of all four members rewritten.
  - `DataContext.cs`: `OpenResultReader`/`OpenResultReaderAsync` gained an optional `terminalName`
    (default `WriteCsv/WriteCsvAsync`); `PrepareResultCommand` takes it and `LazyTemporaryTableMessage`
    keeps the exact CSV text for CSV callers and names the calling terminal otherwise.
  - `LobDataReader.cs`: XML-doc generalized to owner-owning forward-only reader; behavior unchanged.
- **Gates** —
  - C01 `dotnet build nextorm.slnx -c Debug` → exit **0**, **0 warning / 0 error** (`build-debug.log`).
  - C02 `dotnet build nextorm.slnx -c Release` → exit **0**, **0/0** (`build-release.log`).
  - C03 `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~LobDataReader"`
    → exit **2**, **21 selected, 2 failed** — only the two obsolete locator-dialect tests
    (`ToDataReader_WithLocatorDialect_ShouldThrowNotSupportedBeforeExecution`, `...Async`): they asserted
    the pre-change SQLite rejection and now reach execution (fake provider seam → `InvalidOperationException`).
    These belong to the **D:03 test stream**; not edited in this core-only stream.
  - C04 `... --filter "FullyQualifiedName~LobStreaming"` → exit **0**, **29 selected, 0 failed**.
  - C05 `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~ResultReader"`
    → exit **0**, **2 selected, 0 failed** (locator-free seam unchanged and green).
  - C11 `git diff --check` → exit **0**; scope = the 3 allowed source files + this status file.
- **Stale test map handed to D:03/D:04 (not touched here)** —
  `tests/nextorm.core.tests/LobDataReaderTests.cs:90,97`,
  `tests/nextorm.integration.tests/SqliteSpecificTests.cs:519-544` (asserts the removed message),
  `tests/nextorm.integration.tests/CommonTestSuite.Lob.cs:783+` (SQLite branch — now a green path incl.
  non-LOB multi-column projection; LOB columns buffered), and the comment in
  `tests/nextorm.core.tests/LobStreamingTests.cs:11,302`.

- 2026-10-06T13:14Z | DO | r=1 | n=1/3 | D:03 inner-loop validator `brief` `{unit,scope}` exit 0 | /tmp/D134-evidence/brief.json
- 2026-10-06T13:20Z | DO | r=1 | n=1/3 | D:03/D:04 test stream complete: C01 Debug 0/0; C03 23/0; C04 29/0; C05 1065 total / 0 failed / 1 skipped; C06 SQLite shared 617 / 0 failed / 39 skipped; C06b SqliteSpecific 76 / 0 failed | /tmp/D134-evidence/build-debug.log, c03-core-lobdatareader.log, c04-core-lobstreaming.log, c05-sqlite-all.log, c06-sqlite-integration-tail.log, c06b-sqlite-specific.log
- 2026-10-06T13:20Z | DO | r=1 | n=1/3 | capability flag: `SqliteTestProvider.SupportsLobDataReader = true` (buffered locator-free reader) | tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs
- 2026-10-06T13:20Z | DO | r=1 | n=1/3 | gating decision: none needed in CommonTestSuite.Lob.cs (non-LOB projections only; buffered and sequential both satisfy them); PG/SQL Server contract untouched | tests/nextorm.integration.tests/CommonTestSuite.Lob.cs:782
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | D:05 docs/registers complete: 23 docs files (EN+RU guide 26, providers overview/sqlite/postgres/sqlserver, comparisons/capabilities, advanced api-reference/limitations; internal specs design/comparison/API-naming/code-smells + design notice) | `git status` M docs/** (23), D:05 section below
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep C01/C02 builds: Debug exit 0 (0 warning/0 error), Release exit 0 (0/0) | /tmp/D134-evidence/build-debug.log, /tmp/D134-evidence/build-release.log
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep unit suites: core exit 0 (total 1577/passed 1577/failed 0/skipped 0), sqlite exit 0 (total 1065/passed 1064/failed 0/skipped 1) | /tmp/D134-evidence/unit-core.log, /tmp/D134-evidence/unit-sqlite.log
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep SQLite integration (no container) exit 0: total 702/passed 663/failed 0/skipped 39 | /tmp/D134-evidence/integration-sqlite.log
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep C07 container integration exit 0: Total 3206/Errors 0/Failed 0/Skipped 189/Not Run 0 (54.09 s); per-provider executed/skipped PG 757/25, SQL Server 674/43, MySQL 578/79, MariaDB 49/0, ClickHouse 177/0, SQLite 657/39; all 5 DB containers ran, none skipped wholesale | /tmp/D134-evidence/integration-container.log, /tmp/D134-evidence/integration-list-methods.log
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep C08/C09 coverage exit 0: line 87.2% (45443/52112), branch 78.8% (23803/30180) — above 85/75 | /tmp/D134-evidence/coverage.cobertura.xml, /tmp/D134-evidence/coverage-report/Summary.txt
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep E04 candidate acceptance benchmark exit 0: 7/7 cases, global 44.89 s vs baseline 48.08 s (no regression); BenchmarkDotNet.Artifacts restored clean | /tmp/D134-evidence/candidate-acceptance.log
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep C10 docfx exit 0: 2 warnings/0 errors (pre-existing sourcegenerator duplicate source-file warnings) | /tmp/D134-evidence/docfx.log
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | sweep C11 hygiene: `git diff --check` exit 0; all 33 changed/new text files CRLF; no Directory.Packages.props churn, no artifacts/TestResults, no unrelated files | this file
- 2026-10-06T13:34Z | DO | r=1 | n=1/3 | check-ready: D:01–D:06 done, E01–E13 satisfied; no blockers | this file

## DO D:03/D:04 test stream record (r=1, n=1)

- **Changed files** —
  - `tests/nextorm.core.tests/LobDataReaderTests.cs`: the obsolete locator-dialect rejection tests are
    replaced by routing expectations (a locator dialect that supports sequential access passes the gate
    and reaches the fake provider seam); added a `SupportsSequentialAccess=false` **and**
    `LobLocatorColumn!=null` rejection (non-sequential wins); both gate helpers now require the message
    to name `ToDataReader` and not contain `WriteCsv`/`CSV`; the class summary is updated.
  - `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs` (new): the buffered preparation emits the
    projection unchanged (`select id, name from reader_probe`, no `rowid`); a sync/async positive
    multi-column reader asserts `FieldCount`, names, ordinals and values; a `ToDataReader` call does not
    mutate the shared `QueryCommand.Cache` (still `true`, command reusable, later cached query works);
    the lazy temp-table guard names `ToDataReader` without CSV/`WriteCsv` text.
  - `tests/nextorm.sqlite.tests/CsvStreamTests.cs`: the two lazy temp-table CSV tests now also assert the
    preserved CSV label (`WriteCsv/WriteCsvAsync`, `CSV`).
  - `tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`: `SupportsLobDataReader => true`.
  - `tests/nextorm.integration.tests/CommonTestSuite.Lob.cs`: stale "SQLite fails closed" comment fixed.
  - `tests/nextorm.integration.tests/SqliteSpecificTests.cs`: the two former rejection tests are replaced
    by 8 positive SQLite tests — single- and multi-column shapes; buffered `byte[]`/`string`
    null/empty/non-empty (sync + async); cancellation (sync + async); dispose/ownership — plus
    `SetUpLobReaderProbeTable` and `ILobReaderProbeEntity`.
- **Capability flag** — SQLite now advertises the multi-column reader (`SupportsLobDataReader => true`),
  matching PostgreSQL/SQL Server, because the locator-free seam returns a buffered reader without a
  `rowid` column.
- **Gating decision** — none required in the shared suite. Its reader assertions
  (`CommonTestSuite.Lob.cs:796-828` positive, `:830-868` disposal, `:870-902` cancellation,
  `:904-924` unsupported) use non-LOB projections (`Id`, `String`) only, so they hold identically for
  the buffered SQLite reader and the sequential PostgreSQL/SQL Server reader; no
  `SequentialAccess`/chunked-LOB assumption is present. The unsupported tests now skip on SQLite via
  the flag. The PostgreSQL/SQL Server sequential contract was not weakened.
- **Commands / evidence** —
  - C01 `[dotnet,build,nextorm.slnx,-c,Debug]` → exit **0**, 0 warning / 0 error
    (`/tmp/D134-evidence/build-debug.log`).
  - C03 `[dotnet,test,tests/nextorm.core.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~LobDataReader]`
    → exit **0**, 23 selected / 0 failed (`c03-core-lobdatareader.log`).
  - C04 `... --filter FullyQualifiedName~LobStreaming` → exit **0**, 29 selected / 0 failed
    (`c04-core-lobstreaming.log`).
  - C05 `[dotnet,test,tests/nextorm.sqlite.tests,-c,Debug]` → exit **0**, 1065 total / 1064 passed /
    0 failed / 1 skipped (`c05-sqlite-all.log`; the skip is the pre-existing opt-in
    `Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` probe gated by `NEXTORM_LOB_SQLITE_PROBE`).
  - C06 `[dotnet,run,--project,tests/nextorm.integration.tests,-c,Debug,--,-class,nextorm.integration.tests.SqliteIntegrationTests,-noColor]`
    → exit **0**, 617 total / 0 failed / 39 skipped (`c06-sqlite-integration-tail.log`).
  - C06b `... -class nextorm.integration.tests.SqliteSpecificTests -noColor` → exit **0**, 76 total /
    0 failed / 0 skipped (`c06b-sqlite-specific.log`).
- **Inner-loop validator** — `validate_inner_loop.py brief /tmp/D134-evidence/brief.json` → exit **0**.
- **Hygiene** — all touched files CRLF; `git diff --check` exit **0**; scope = 3 core source files
  (previous stream) + 6 test files (5 modified + 1 new) + this status file.

## DO D:05/D:06 record (r=1, n=1) — gate sweep

### D:05 docs/registers

- **23 modified docs files** (EN + RU): `docs/advanced/api-reference.md`, `docs/advanced/limitations.md`,
  `docs/comparisons/capabilities.md`, `docs/guide/26-large-objects.md`, `docs/providers/overview.md`,
  `docs/providers/postgres.md`, `docs/providers/sqlite.md`, `docs/providers/sqlserver.md`, their `docs/ru/**`
  counterparts (8), and `docs/specs/comparison/capability-matrix.md`,
  `docs/specs/comparison/linq2db-comparison.md`, `docs/specs/ru/comparison/linq2db-comparison.md`,
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/design/code-smells-review.md`,
  `docs/specs/design/issue-189-todatareader-sqlite.md` (implementation notice),
  `docs/specs/performance/comparison-benchmark-scenarios.md`. Milestone notice recorded at the top of this
  file (implemented in `1.0.9-rc1` per overlay invariant 8).
- Docs consistency is exercised by C10 docfx (exit 0, 2 pre-existing warnings); no docs link to `docs/specs/**`
  from public pages.

### D:06 gate sweep — commands / exit / counts / logs

| # | Gate | Command (array) | Exit | Result | Log |
|---|---|---|---|---|---|
| 1 | C01 Debug build | `[dotnet,build,nextorm.slnx,-c,Debug]` | 0 | 0 warning / 0 error | `/tmp/D134-evidence/build-debug.log` |
| 2 | C02 Release build | `[dotnet,build,nextorm.slnx,-c,Release]` | 0 | 0 warning / 0 error | `/tmp/D134-evidence/build-release.log` |
| 3 | Core unit suite | `[dotnet,test,tests/nextorm.core.tests,-c,Debug]` | 0 | total 1577 / passed 1577 / failed 0 / skipped 0 | `/tmp/D134-evidence/unit-core.log` |
| 4 | SQLite unit suite | `[dotnet,test,tests/nextorm.sqlite.tests,-c,Debug]` | 0 | total 1065 / passed 1064 / failed 0 / skipped 1 | `/tmp/D134-evidence/unit-sqlite.log` |
| 5 | SQLite integration (no container) | `[dotnet,test,tests/nextorm.integration.tests,-c,Debug,--filter,FullyQualifiedName~Sqlite]` | 0 | total 702 / passed 663 / failed 0 / skipped 39 | `/tmp/D134-evidence/integration-sqlite.log` |
| 6 | C07 container integration | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | 0 | Total 3206 / Errors 0 / Failed 0 / Skipped 189 / Not Run 0, 54.09 s | `/tmp/D134-evidence/integration-container.log` |
| 7 | C08 coverage collect | `[dotnet-coverage,collect,dotnet test nextorm.slnx -c Debug,--settings,coverage.settings.xml,--output-format,cobertura,--output,/tmp/D134-evidence/coverage.cobertura.xml]` | 0 | 8510 total / 5982 passed / 0 failed / 2528 skipped (integration providers skip without `DOCKER_HOST`) | `/tmp/D134-evidence/coverage-collect.log` |
| 8 | C09 report | `[reportgenerator,-reports:…,-targetdir:…,-reporttypes:TextSummary]` | 0 | line 87.2%, branch 78.8%, method 77.7% | `/tmp/D134-evidence/coverage-report/Summary.txt` |
| 9 | E04 candidate benchmark | `[timeout,240,dotnet,run,--project,benchmarks/nextorm.benchmark,-c Release,--,--anyCategories=acceptance]` | 0 | 7/7 cases, global 44.89 s (wall 50 s), baseline 48.08 s | `/tmp/D134-evidence/candidate-acceptance.log` |
| 10 | C10 docfx | `[dotnet,docfx,docs/docfx.json]` | 0 | 2 warnings / 0 errors (pre-existing sourcegenerator duplicates) | `/tmp/D134-evidence/docfx.log` |
| 11 | C11 hygiene | `[git,diff,--check]` + `[git,status,--porcelain]` | 0 | no whitespace errors; scope below | this file |

### Container integration per-provider (C07)

All five DB containers started and exited cleanly (verified with
`podman.exe --connection podman-machine-default ps -a`): `postgres:17-alpine`, `mssql/server:2025-latest`,
`mysql:8.4`, `mariadb:11.4`, `clickhouse/clickhouse-server:25.8-alpine`; SQLite needs none.
`Skipped 189` are capability skips, **not** provider skips:

| Provider | Executed | Skipped | Note |
|---|---|---|---|
| PostgreSQL | 757 | 25 | sequential LOB + capability skips |
| SQL Server | 674 | 43 | capability skips |
| MySQL | 578 | 79 | non-sequential dialect skips |
| MariaDB | 49 | 0 | all executed |
| ClickHouse | 177 | 0 | all executed |
| SQLite | 657 | 39 | capability skips |
| cross-provider / harness | 69 | 3 | `LobPerfHarnessTests` (opt-in), 2 `LobCapabilityProbeTests` (opt-in) |

Method counts are from `-list methods` (`/tmp/D134-evidence/integration-list-methods.log`, 3147 methods);
the run total 3206 additionally counts 59 theory data rows. No provider was skipped wholesale — each of
PostgreSQL, SQL Server, MySQL, MariaDB and ClickHouse reported executed (non-zero) tests and 0 failures.

### Coverage (C08/C09)

- **Line 87.2%** (45443 / 52112) — ≥ `MIN_LINE_COVERAGE=85`.
- **Branch 78.8%** (23803 / 30180) — ≥ `MIN_BRANCH_COVERAGE=75`.
- Thresholds are hard only on `main`; on `1.0.9-rc1` this is a pass anyway. The instrumented `dotnet test
  nextorm.slnx` ran without `DOCKER_HOST`, so the PG/SQL Server/MySQL/ClickHouse integration tests are the
  bulk of the 2528 skips; core/SQLite (the changed area) are fully exercised.

### Candidate benchmark vs baseline (E03/E04)

- Baseline (before code): exit 0, 7/7, **48.08 s** (`/tmp/D134-evidence/baseline-acceptance.log`).
- Candidate (after code): exit 0, 7/7, **44.89 s** global, wall 50 s
  (`/tmp/D134-evidence/candidate-acceptance.log`).
- No regression (−3.19 s, within run-to-run noise); cached-path acceptance baseline not worsened.
- `BenchmarkDotNet.Artifacts` had only the 15 expected benchmark-output modifications, restored with
  `git checkout -- BenchmarkDotNet.Artifacts` (exit 0); path clean afterwards — no unrelated changes lost.

### Hygiene / scope (C11)

- `git diff --check` → exit **0**.
- Changed/new scope: 3 core source files (`DataContext.cs`, `LobDataReader.cs`, `QueryCommandExtensions.cs`),
  6 test files (`LobDataReaderTests.cs`, `ToDataReaderSqliteTests.cs` new, `CsvStreamTests.cs`,
  `SqliteTestProvider.cs`, `CommonTestSuite.Lob.cs`, `SqliteSpecificTests.cs`), 23 docs files, and this status
  file. No `Directory.Packages.props` version churn, no `BenchmarkDotNet.Artifacts`, no `TestResults`, no
  unrelated files.
- All 33 changed/new text files report CRLF line terminators (`file -b`).

### Evidence index E01–E13

| ID | Artifact |
|---|---|
| E01 | `validate_inner_loop.py brief /tmp/D134-evidence/brief.json` → exit 0 (D:03 stream) |
| E02 | Roslyn recon in `## Reconnaissance` above: EntityBuilder N/A + CSV caller `QueryCommandExtensions.cs:412` |
| E03 | `/tmp/D134-evidence/baseline-acceptance.log` (48.08 s, 7/7) |
| E04 | `/tmp/D134-evidence/candidate-acceptance.log` (44.89 s, 7/7) |
| E05 | `/tmp/D134-evidence/build-debug.log` (exit 0, 0/0) |
| E06 | `/tmp/D134-evidence/build-release.log` (exit 0, 0/0) |
| E07 | `/tmp/D134-evidence/c03-core-lobdatareader.log`, `/tmp/D134-evidence/c04-core-lobstreaming.log`, `/tmp/D134-evidence/unit-core.log` |
| E08 | `/tmp/D134-evidence/unit-sqlite.log`, `/tmp/D134-evidence/integration-sqlite.log` |
| E09 | `/tmp/D134-evidence/integration-container.log`, `/tmp/D134-evidence/integration-list-methods.log` |
| E10 | `tests/nextorm.integration.tests/SqliteSpecificTests.cs` + `CommonTestSuite.Lob.cs`; logs `integration-sqlite.log`, `integration-container.log` |
| E11 | `tests/nextorm.core.tests/LobDataReaderTests.cs`, `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`; logs `c03-core-lobdatareader.log`, `unit-sqlite.log` |
| E12 | `/tmp/D134-evidence/coverage.cobertura.xml`, `/tmp/D134-evidence/coverage-report/Summary.txt` (87.2% / 78.8%) |
| E13 | `/tmp/D134-evidence/docfx.log`; CRLF/scope evidence in this section |

- **Status** — D:01–D:06 done, E01–E13 satisfied, no blockers, **check-ready**. No commit (ACT later), no
  push/merge; `r=1`, `n=2/3` (DO n=2 after the CHECK fail at r=1/n=1, no replan).

## DO n=2 record (r=1, no replan) — CHECK P2 fixes

CHECK at r=1/n=1 failed with five P2 defects; DO n=2 (same plan revision r=1) applies the five fixes.

- **D134-1 — provider-docs scoping** — `docs/providers/sqlite.md:253` (EN) and
  `docs/ru/providers/sqlite.md:255` (RU): the unqualified "no buffered fallback" is now scoped to the
  **single-column streaming** (`ToStream`/`ToTextReader`) scalar/locator path, with an explicit
  cross-reference that the multi-column `ToDataReader` uses a separate buffered, locator-free seam
  (mirrors `docs/guide/26-large-objects.md:147`). A page-wide sweep found no other unqualified
  "not supported" claim that the implementation invalidates: the provider-difference table rows
  (`sqlite.md:276-277` / RU `:278-279`) already name `ToStream`/`ToTextReader` explicitly.
- **D134-2 — guard message wording** — `src/nextorm.core/Query/QueryCommandExtensions.cs:495`: the
  `NotSupportedException` no longer says SQLite "requires sequential-access support"; it now reads
  `ToDataReader is not supported by the {X} provider; it requires sequential-access support
  (PostgreSQL or SQL Server) or SQLite.` The fail-closed predicate stays `!SupportsSequentialAccess`
  only and the terminal name stays `ToDataReader`. The affected core assertions
  (`tests/nextorm.core.tests/LobDataReaderTests.cs:134-137,150-153`) pin only that the message names
  `ToDataReader` and omits `WriteCsv`/`CSV`, so they remain valid unchanged.
- **D134-3 — status test-count** — this file: `SqliteSpecificTests.cs` now correctly claims **8**
  positive SQLite `ToDataReader` tests (verified by enumeration at
  `tests/nextorm.integration.tests/SqliteSpecificTests.cs:524-667`: single-column, multi-column
  sync/async, buffered blob+text sync/async, cancellation sync/async, dispose). The former "9" was a
  miscount.
- **D134-4 — design-doc present tense** —
  `docs/specs/design/issue-189-todatareader-sqlite.md:16-24`: §1 now opens with a
  **Pre-implementation description (historical) — superseded by the implementation notice above**
  blockquote; the body is reframed to the pre-implementation state ("бросали"/"держал") and §2 is
  retitled "Контракт до реализации и причина отказа". The approval history is not rewritten.
- **D134-5 — PG/SQL Server lazy-temp diagnostic (fixed on the LOB path, scoped)** — the candidate was
  real: on a non-locator dialect `ToDataReader` routes to `DataContext.OpenLobReader`
  (`QueryCommandExtensions.cs:336,390`) → `PrepareLobCommand` (`DataContext.cs:429-447`), which called
  `_planner.GetPreparedQueryCommand` directly and thus bypassed `HasTemporaryTableSource()`; an
  `AsTempTable().ToDataReader()` on PostgreSQL/SQL Server would have surfaced a raw provider
  "relation does not exist" error. Fix: `OpenLobReader`/`OpenLobReaderAsync`/`PrepareLobCommand` now
  take an optional `string? terminalName = null`; `ToDataReader`/`ToDataReaderAsync` pass
  `DataReaderTerminalName`, and `PrepareLobCommand` fails closed with the terminal-correct
  `LazyTemporaryTableMessage` before preparing. `ToStream`/`ToTextReader` callers keep the default
  (`null`) and are unchanged — extending the guard to them would change their current behavior
  (raw provider error → `NotSupportedException`) beyond the R04 scope, so it was deliberately **not**
  forced; their wording/behavior is preserved. This is an **R04 scope note**: R04's lazy-temp clause is
  satisfied on both reader seams (`PrepareResultCommand` for the locator dialect and now
  `PrepareLobCommand` for the non-locator `ToDataReader` caller); the scalar-streaming terminals are
  explicitly out of this scope. No public signature changed (the new parameter is `internal`), variant A
  and `SqlBuilder.cs:527` are untouched. Regression tests added at
  `tests/nextorm.core.tests/LobDataReaderTests.cs`
  (`ToDataReader[Async]_SequentialDialectWithTempTableSource_ShouldThrowNotSupportedBeforeExecution`):
  red without the guard (exit 2, 2/2 failed — the `InvalidOperationException` from the provider seam,
  `regress-red.log`), green with it (exit 0, 2/2 passed, `regress-green.log`).

### Progress log (DO n=2)

- 2026-10-06T13:52Z | DO | r=1 | n=2/3 | DO n=2 fixes applied: D134-1 provider-docs scoping, D134-2 guard message wording, D134-3 status count 9→8, D134-4 design-doc pre-implementation marker, D134-5 lazy-temp guard on the LOB path for `ToDataReader` (ToStream/ToTextReader preserved) | this file; `src/nextorm.core/Query/QueryCommandExtensions.cs:495`; `src/nextorm.core/DataContext/DataContext.cs:429-447`
- 2026-10-06T13:52Z | DO | r=1 | n=2/3 | D134-5 investigation: confirmed via code reading (`ToDataReader` non-locator → `OpenLobReader` → `PrepareLobCommand` → `_planner.GetPreparedQueryCommand` without `HasTemporaryTableSource()`) → raw provider error candidate real; fixed as scoped above | `QueryCommandExtensions.cs:336,390`, `DataContext.cs:429-447`
- 2026-10-06T13:45Z | DO | r=1 | n=2/3 | D134-5 regression test: red without guard (exit 2, 2/2 failed, provider-seam `InvalidOperationException`), green with guard (exit 0, 2/2 passed); C01 Debug exit 0 (0 warning/0 error), C02 Release exit 0 (0/0); C03 `FullyQualifiedName~LobDataReader` exit 0 (25 selected / 0 failed); C10 docfx exit 0 (2 warnings / 0 errors, pre-existing sourcegenerator duplicates); C11 `git diff --check` exit 0 | /tmp/D134-evidence/regress-red.log, regress-green.log, build-debug-n2.log, build-release-n2.log, c03-core-lobdatareader-n2.log, docfx-n2.log
- 2026-10-06T13:53Z | DO | r=1 | n=2/3 | DO n=2 CHECK test/evidence gaps (6) closed in `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`: single-column async + default-token async, span/array bound params incl. null, empty result sync+async, observable per-call command+reader disposal, async cache counterpart, cancellation-after-open sync+async; 9 new facts (class now 15); no production code touched | /tmp/D134-evidence/c07-todatareadersqlite-n2.log
- 2026-10-06T13:53Z | DO | r=1 | n=2/3 | inner-loop validator brief `{unit,scope}` (new mandatory scope) exit 0 | /tmp/D134-evidence/brief-n2.json
- 2026-10-06T13:53Z | DO | r=1 | n=2/3 | C01 Debug build exit 0 (0 warning / 0 error) | /tmp/D134-evidence/build-debug-n2b.log
- 2026-10-06T13:53Z | DO | r=1 | n=2/3 | sqlite unit suite exit 0: total 1074 / passed 1073 / failed 0 / skipped 1 (+9 new); core `FullyQualifiedName~LobDataReader` exit 0 (25 selected / 0 failed) | /tmp/D134-evidence/unit-sqlite-n2b.log, c03-core-lobdatareader-n2b.log
- 2026-10-06T13:53Z | DO | r=1 | n=2/3 | SQLite integration (no container) exit 0: SqliteIntegrationTests 617 total / 0 failed / 39 skipped; SqliteSpecificTests 76 total / 0 failed / 0 skipped | /tmp/D134-evidence/c06-sqlite-integration-n2b.log, c06b-sqlite-specific-n2b.log
- 2026-10-06T13:53Z | DO | r=1 | n=2/3 | no deferred items: all six gaps deterministic (incl. disposal and cancellation-after-open); test-only `TrackingSqliteConnection`/`TrackingSqliteCommand` hook added because `IQueryInterceptor` exposes no disposal event | this file
- 2026-10-06T08:55Z | DO | r=1 | n=2/3 | DO→CHECK boundary sweep C01 Debug build exit 0 (0 warning / 0 error); C02 Release build exit 0 (0 warning / 0 error) | /tmp/D134-evidence/build-debug-neg2.log, /tmp/D134-evidence/build-release-neg2.log
- 2026-10-06T08:55Z | DO | r=1 | n=2/3 | boundary unit suites exit 0: core total 1579 / passed 1579 / failed 0 / skipped 0; sqlite total 1074 / passed 1073 / failed 0 / skipped 1 | /tmp/D134-evidence/unit-core-neg2.log, /tmp/D134-evidence/unit-sqlite-neg2.log
- 2026-10-06T08:56Z | DO | r=1 | n=2/3 | boundary SQLite integration (`--filter FullyQualifiedName~Sqlite`, no container) exit 0: total 702 / passed 663 / failed 0 / skipped 39 | /tmp/D134-evidence/integration-sqlite-neg2.log
- 2026-10-06T08:57Z | DO | r=1 | n=2/3 | boundary C07 container integration exit 0: Total 3206 / Errors 0 / Failed 0 / Skipped 189 / Not Run 0 (58.58 s); 5 DB containers started+stopped; per-provider executed/skipped PG 757/25, SQL Server 674/43, MySQL 578/79, MariaDB 50/0, ClickHouse 177/0, SQLite 657/39; no provider wholesale-skipped | /tmp/D134-evidence/integration-container-neg2.log, /tmp/D134-evidence/integration-list-methods-neg2.log
- 2026-10-06T08:57Z | DO | r=1 | n=2/3 | boundary sweep complete: all 5 commands exit 0; n=2 re-check-ready, no blockers; keep r=1/n=2 | this file

## DO n=2 record (r=1, continued) — CHECK test/evidence gaps

CHECK's test lens flagged six test/evidence gaps around the already-correct SQLite locator-free
`ToDataReader` implementation. All six are closed deterministically in
`tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs` (9 new facts; the reader class now has 15). No
production code was touched and no existing test was rewritten.

| Gap | Requirement | Test added | Evidence |
|---|---|---|---|
| 1 | SQLite single-column async positive + default-token async on the locator-free path | `ToDataReaderAsync_Sqlite_SingleColumn_ShouldReadEveryRow` (explicit token, `FieldCount==1`), `ToDataReaderAsync_Sqlite_DefaultToken_WithParameters_ShouldBindArrayValues` (no explicit token) | `c07-todatareadersqlite-n2.log` |
| 2 | Non-empty bound params, sync span + async array, several values incl. a null | `ToDataReader_Sqlite_WithParameters_ShouldBindSpanValues`, `ToDataReaderAsync_Sqlite_DefaultToken_WithParameters_ShouldBindArrayValues` (`@p0..@p3`, `IsDBNull` for the nulls) | same |
| 3 | Empty result set: `Read()` false / `HasRows` false, no crash, reader disposed | `ToDataReader_Sqlite_EmptyResult_ShouldReturnNoRows`, `ToDataReaderAsync_Sqlite_EmptyResult_ShouldReturnNoRows` | same |
| 4 | Observable per-call `DbCommand` (+ reader) disposal, context reusable afterwards | `ToDataReader_Sqlite_DisposesPerCallCommandAndReader` | same |
| 5 | Async cache counterpart at terminal level (`storeInCache:false`, no shared-command mutation) | `ToDataReaderAsync_Sqlite_ShouldNotMutateSharedCommandCache` | same |
| 6 | Cancellation-after-open, deterministic sync + async | `ToDataReader_Sqlite_CancelAfterOpen_ShouldThrowOnNextRead`, `ToDataReaderAsync_Sqlite_CancelAfterOpen_ShouldThrowOnNextRead` | same |

- **Gap 4 — no existing disposal hook.** `IQueryInterceptor` exposes `CommandInitialized`/`CommandExecuting`/
  `CommandExecuted`/`CommandFailed` only; `SqliteCommand.Dispose` keeps `Connection` set, so an interceptor
  alone cannot observe disposal. A minimal **test-only** hook was added: a
  `TrackingSqliteConnection : SqliteConnection` whose `CreateCommand()` returns a
  `TrackingSqliteCommand : SqliteCommand` recording `Dispose(bool)` and exposing the still-attached provider
  reader. The test captures the per-call command through the existing `SqlRecordingInterceptor`
  (`LastCommand`) and asserts `HasOpenReader` true→false and `WasDisposed` false→true across the reader
  `using`, then reads the context again. No production or shared test infra was modified.
- **Gap 6 — deterministic, no sleeps.** `LobDataReader.Read`/`NextResult` call
  `_cancellationToken.ThrowIfCancellationRequested()`, and `ReadAsync` links the open-time token into the
  inner read, so cancelling after the first row throws `OperationCanceledException` on the next read with
  no timers. No `deferred + trigger` items remain for this task.
- **"Also" item (`FieldCount` vs the shared suite).** The shared `CommonTestSuite.Lob.cs` positive tests were
  **not** rewritten. The terminal-level `FieldCount`/no-`rowid` assertion already exists on the SQLite side:
  `ToDataReader_Sqlite_ReadsMultiColumnProjectionWithoutLocator` captures the executed SQL through the
  interceptor (asserts `select id, name from reader_probe`, no `rowid`) and asserts `FieldCount == 2`; the
  integration `LobDataReader_ToDataReader_*` facts assert `FieldCount` 1/2/3. Confirmed; no extra assertion
  required.

### Commands / evidence (DO n=2 test gaps)

| # | Command (array) | Exit | Counts | Log |
|---|---|---|---|---|
| 1 | `[python3, validate_inner_loop.py, brief, /tmp/D134-evidence/brief-n2.json]` | 0 | — | `/tmp/D134-evidence/brief-n2.json` |
| 2 | `[dotnet,build,nextorm.slnx,-c,Debug]` | 0 | 0 warning / 0 error | `/tmp/D134-evidence/build-debug-n2b.log` |
| 3 | `[dotnet,test,tests/nextorm.sqlite.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~ToDataReaderSqlite]` | 0 | 15 selected / 0 failed | `/tmp/D134-evidence/c07-todatareadersqlite-n2.log` |
| 4 | `[dotnet,test,tests/nextorm.sqlite.tests,-c,Debug,--no-build]` | 0 | total 1074 / passed 1073 / failed 0 / skipped 1 | `/tmp/D134-evidence/unit-sqlite-n2b.log` |
| 5 | `[dotnet,test,tests/nextorm.core.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~LobDataReader]` | 0 | 25 selected / 0 failed | `/tmp/D134-evidence/c03-core-lobdatareader-n2b.log` |
| 6 | `[dotnet,run,--project,tests/nextorm.integration.tests,-c,Debug,--,-class,nextorm.integration.tests.SqliteIntegrationTests,-noColor]` | 0 | 617 total / 0 failed / 39 skipped | `/tmp/D134-evidence/c06-sqlite-integration-n2b.log` |
| 7 | `[dotnet,run,--project,tests/nextorm.integration.tests,-c,Debug,--,-class,nextorm.integration.tests.SqliteSpecificTests,-noColor]` | 0 | 76 total / 0 failed / 0 skipped | `/tmp/D134-evidence/c06b-sqlite-specific-n2b.log` |
| 8 | `[git,diff,--check]` | 0 | no whitespace errors; the new test file is CRLF | this file |

## Defect history (durable)

| Defect key | Title | First seen | Revision/attempt | Fix applied (r/n) | Evidence | Status |
|---|---|---|---|---|---|---|
| D134-1 | Provider-docs scoping (unqualified "no buffered fallback") | CHECK r=1/n=1 | r=1/n=1 | r=1/n=2 | `docs/providers/sqlite.md:253`, `docs/ru/providers/sqlite.md:255`, `docs/guide/26-large-objects.md:147` | fixed |
| D134-2 | Guard message names SQLite as requiring sequential access | CHECK r=1/n=1 | r=1/n=1 | r=1/n=2 | `src/nextorm.core/Query/QueryCommandExtensions.cs:495` | fixed |
| D134-3 | Status file claims 9 positive SQLite tests (actual 8) | CHECK r=1/n=1 | r=1/n=1 | r=1/n=2 | this file; `tests/nextorm.integration.tests/SqliteSpecificTests.cs:524-667` | fixed |
| D134-4 | Design doc §1/§2 present tense contradicts implementation notice | CHECK r=1/n=1 | r=1/n=1 | r=1/n=2 | `docs/specs/design/issue-189-todatareader-sqlite.md:16-24` | fixed |
| D134-5 | PG/SQL Server `AsTempTable().ToDataReader()` bypasses lazy-temp guard | CHECK r=1/n=1 | r=1/n=1 | r=1/n=2 | `src/nextorm.core/DataContext/DataContext.cs:429-447`; `QueryCommandExtensions.cs:336,390` | fixed (scoped) |
| D134-6 | CHECK test/evidence gaps: single-column/default-token async, bound params, empty result, observable command+reader disposal, async cache, cancellation-after-open | CHECK r=1/n=2 | r=1/n=2 | r=1/n=2 | `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`; `/tmp/D134-evidence/c07-todatareadersqlite-n2.log`, `unit-sqlite-n2b.log`, `c06b-sqlite-specific-n2b.log` | fixed |
| D134-7 | R06: a connection open failure leaks the planner-created command — `OpenResultReader(Async)`/`OpenLobReader(Async)` called `GetDbCommand` outside their `try/finally` | Escalate after CHECK r=1/n=2 | r=1/n=2 | r=1/n=3 (1 fix) | `src/nextorm.core/DataContext/QueryExecutor.cs`; `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`; `/tmp/D134-evidence/c08-todatareadersqlite-n3-red.log`, `c08-todatareadersqlite-n3-green.log` | fixed |

## CHECK evidence re-gather (r=1, n=2) — no DO iteration consumed

CHECK at r=1/n=2 requested attributable artifact evidence because code changed since n=1 (guard + guard
message + LOB temp-table guard; test-only additions). This is an evidence re-gather: **no production/test
code was edited and no DO attempt/iteration was consumed** (`n` stays `2/3`, `r` stays `1`). All commands
re-run on the frozen n=2 tree.

### C08/C09 coverage at n=2

- **C08** — `[dotnet dotnet-coverage,collect,"dotnet test nextorm.slnx -c Debug",--settings,coverage.settings.xml,--output-format,cobertura,--output,/tmp/D134-evidence/coverage-n2.cobertura.xml]`
  → exit **0**; run: total **8521**, failed **0**, succeeded **5993**, skipped **2528**
  (n=1: 8510/5982/0/2528; +11 = 9 new SQLite + 2 core regression facts). Logs
  `/tmp/D134-evidence/coverage-collect-n2.log`, artifact `/tmp/D134-evidence/coverage-n2.cobertura.xml`.
- **C09** — `[dotnet reportgenerator,-reports:/tmp/D134-evidence/coverage-n2.cobertura.xml,-targetdir:/tmp/D134-evidence/coverage-report-n2,-reporttypes:TextSummary]`
  → exit **0**; **line 87.2%** (45447/52116), **branch 78.8%** (23816/30188), method 77.7%
  (5692/7324) — both ≥ `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75` (n=1: 45443/52112,
  23803/30180). Log `/tmp/D134-evidence/coverage-reportgen-n2.log`, summary
  `/tmp/D134-evidence/coverage-report-n2/Summary.txt`.

### E04 acceptance benchmark at n=2

- `[timeout,240,dotnet,run,--project,benchmarks/nextorm.benchmark,-c Release,--,--anyCategories=acceptance]`
  → exit **0**, **7/7** cases, **global 47.9 s**; baseline **48.08 s**
  (`baseline-acceptance.log`), n=1 candidate **44.89 s** (`candidate-acceptance.log`). No regression
  (+3.01 s vs n=1, −0.18 s vs baseline; within run-to-run noise). Log
  `/tmp/D134-evidence/candidate-acceptance-n2.log`. `BenchmarkDotNet.Artifacts` had exactly the 15
  expected benchmark-output modifications, restored with `[git,checkout,--,BenchmarkDotNet.Artifacts]`
  (exit 0) → clean, no unrelated/untracked changes there.

### Container count reconciliation (C07, `/tmp/D134-evidence/integration-list-methods-neg2.log`)

Run summary (`integration-container-neg2.log:435`): **Total 3206, Errors 0, Failed 0, Skipped 189,
Not Run 0**. `-list methods` (neg2) lists **3147** methods; the run total adds **59 theory-data rows**.
Provider-attributed **method** executions = **2893**, each bucket = class methods − skips:

| Provider | Class methods | Skipped | Executed (=2893) |
|---|---|---|---|
| PostgreSQL | 782 | 25 | 757 |
| SQL Server | 717 | 43 | 674 |
| MySQL | 657 | 79 | 578 |
| MariaDB | 50 | 0 | 50 |
| ClickHouse | 177 | 0 | 177 |
| SQLite | 696 | 39 | 657 |

The remaining **124** = **65 provider-agnostic method executions + 59 theory-data rows**:

- **68 methods** across 10 classes are not attributed to a provider bucket:
  `IdentityReturningIntegrationTests` 28, `EfCoreServerSharedTransactionTests` 11,
  `EfCoreQueryFilterBridgeTests` 10, `CoreApiContractTests` 8, `EfCoreQueryFilterLifecycleTests` 3,
  `DialectCapabilityContractTests` 3, `LobCapabilityProbeTests` 2, `EfCoreSharedTransactionTests` 1,
  `QueryFilterInMemoryParityTests` 1, `LobPerfHarnessTests` 1. Three are the opt-in skips
  (`LobPerfHarnessTests` 1 + `LobCapabilityProbeTests` 2), so **65 executed**.
- **59 theory-data rows** (3206 − 3147): `IdentityReturningIntegrationTests` 8 methods × 30 `InlineData`
  → +22; `EfCoreServerSharedTransactionTests` 7×3 `ServerProviders` → +14;
  `EfCoreQueryFilterBridgeTests` 5×3 → +10; `EfCoreQueryFilterLifecycleTests` 3×4 `LiveProviders` → +9;
  `CommonTestSuite.SqlCommand` 3 methods → 4 `InlineData` rows × 4 `CommonTestSuite` providers (PG,
  SQL Server, MySQL, SQLite) → +4. Total 22+14+10+9+4 = 59.

Arithmetic: 2893 (provider methods) + 65 (provider-agnostic methods) + 59 (theory rows) = **3017
executed** = 3206 total − 189 skipped. No provider was skipped wholesale; all five DB containers started
and all reported non-zero executed with 0 failures.

### C01–C11 command / exit / counts / log ledger (n=2)

| # | Command (array) | Exit | Key counts | Log |
|---|---|---|---|---|
| C01 | `[dotnet,build,nextorm.slnx,-c,Debug]` | 0 | 0 warning / 0 error | `/tmp/D134-evidence/build-debug-neg2.log` |
| C02 | `[dotnet,build,nextorm.slnx,-c,Release]` | 0 | 0 warning / 0 error | `/tmp/D134-evidence/build-release-neg2.log` |
| C03 | `[dotnet,test,tests/nextorm.core.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~LobDataReader]` | 0 | 25 succeeded / 0 failed / 0 skipped | `/tmp/D134-evidence/c03-core-lobdatareader-n2b.log` |
| C04 | `[dotnet,test,tests/nextorm.core.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~LobStreaming]` | 0 | 29 succeeded / 0 failed (not re-run at n=2; unchanged area) | `/tmp/D134-evidence/c04-core-lobstreaming.log` |
| C05 | `[dotnet,test,tests/nextorm.sqlite.tests,-c,Debug,--no-build]` | 0 | total 1074 / succeeded 1073 / failed 0 / skipped 1 | `/tmp/D134-evidence/unit-sqlite-neg2.log` |
| C06 | `[dotnet,run,--project,tests/nextorm.integration.tests,-c,Debug,--,-class,nextorm.integration.tests.SqliteIntegrationTests,-noColor]` | 0 | total 617 / 0 failed / 39 skipped | `/tmp/D134-evidence/c06-sqlite-integration-n2b.log` |
| C06b | `[dotnet,run,--project,tests/nextorm.integration.tests,-c,Debug,--,-class,nextorm.integration.tests.SqliteSpecificTests,-noColor]` | 0 | total 76 / 0 failed / 0 skipped | `/tmp/D134-evidence/c06b-sqlite-specific-n2b.log` |
| C07 | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock [dotnet,run,--project,tests/nextorm.integration.tests,-c,Debug,--,-noColor]` | 0 | total 3206 / errors 0 / failed 0 / skipped 189 / not run 0 (58.58 s) | `/tmp/D134-evidence/integration-container-neg2.log`, `/tmp/D134-evidence/integration-list-methods-neg2.log` |
| C08 | `[dotnet dotnet-coverage,collect,"dotnet test nextorm.slnx -c Debug",--settings,coverage.settings.xml,--output-format,cobertura,--output,/tmp/D134-evidence/coverage-n2.cobertura.xml]` | 0 | total 8521 / failed 0 / succeeded 5993 / skipped 2528 | `/tmp/D134-evidence/coverage-collect-n2.log` |
| C09 | `[dotnet reportgenerator,-reports:/tmp/D134-evidence/coverage-n2.cobertura.xml,-targetdir:/tmp/D134-evidence/coverage-report-n2,-reporttypes:TextSummary]` | 0 | line 87.2% (45447/52116) / branch 78.8% (23816/30188) | `/tmp/D134-evidence/coverage-report-n2/Summary.txt` |
| C10 | `[dotnet,docfx,docs/docfx.json]` | 0 | 2 warnings / 0 errors (pre-existing sourcegenerator duplicates) | `/tmp/D134-evidence/docfx-n2.log` |
| C11 | `[git,diff,--check]` + `[git,status,--porcelain]` | 0 | no whitespace errors; 33 task files (below) | this file |

### Task-file allowlist (C11, `git status --porcelain`)

33 entries, all D134: **3 core** (`DataContext.cs`, `LobDataReader.cs`, `QueryCommandExtensions.cs`),
**6 tests** (`LobDataReaderTests.cs`, `CsvStreamTests.cs`, `SqliteTestProvider.cs`,
`CommonTestSuite.Lob.cs`, `SqliteSpecificTests.cs` modified + `ToDataReaderSqliteTests.cs` new),
**23 docs** (EN/RU guide/providers/advanced/comparisons + internal specs), **1 status file**. No
forbidden path: no `Directory.Packages.props`, no `BenchmarkDotNet.Artifacts`, no `TestResults`, no
unrelated files. `[git,diff,--check]` exit **0**; all text files CRLF.

### Evidence index E01–E13 refreshed to n=2

| ID | Artifact (n=2) |
|---|---|
| E01 | `/tmp/D134-evidence/brief-n2.json` (inner-loop validator exit 0) |
| E02 | Roslyn recon in `## Reconnaissance`: EntityBuilder N/A + CSV caller `QueryCommandExtensions.cs:412` |
| E03 | `/tmp/D134-evidence/baseline-acceptance.log` (48.08 s, 7/7) |
| E04 | `/tmp/D134-evidence/candidate-acceptance-n2.log` (47.9 s, 7/7) |
| E05 | `/tmp/D134-evidence/build-debug-neg2.log` (exit 0, 0/0) |
| E06 | `/tmp/D134-evidence/build-release-neg2.log` (exit 0, 0/0) |
| E07 | `/tmp/D134-evidence/c03-core-lobdatareader-n2b.log` (25/0), `unit-core-neg2.log` (1579/0), `c04-core-lobstreaming.log` (29/0) |
| E08 | `/tmp/D134-evidence/unit-sqlite-neg2.log` (1074/1073/0/1), `integration-sqlite-neg2.log` (702/663/0/39) |
| E09 | `/tmp/D134-evidence/integration-container-neg2.log` (3206/0/0/189), `integration-list-methods-neg2.log` (3147) |
| E10 | `tests/nextorm.integration.tests/SqliteSpecificTests.cs` + `CommonTestSuite.Lob.cs`; `integration-sqlite-neg2.log`, `integration-container-neg2.log` |
| E11 | `tests/nextorm.core.tests/LobDataReaderTests.cs`, `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`; `c03-core-lobdatareader-n2b.log`, `unit-sqlite-neg2.log` |
| E12 | `/tmp/D134-evidence/coverage-n2.cobertura.xml`, `/tmp/D134-evidence/coverage-report-n2/Summary.txt` (87.2% / 78.8%) — **superseded** by the n=3 coverage artifacts (see "CHECK coverage re-gather (n=3)" below) |
| E13 | `/tmp/D134-evidence/docfx-n2.log` (2/0); CRLF/scope evidence in this section |

### Progress log (CHECK evidence re-gather, append-only)

- 2026-10-06T09:01Z | CHECK | r=1 | n=2/3 | evidence re-gather (no DO iteration consumed): C08 n=2 coverage exit 0, total 8521 / succeeded 5993 / failed 0 / skipped 2528; C09 n=2 reportgen exit 0, line 87.2% (45447/52116), branch 78.8% (23816/30188) | /tmp/D134-evidence/coverage-collect-n2.log, coverage-report-n2/Summary.txt, coverage-reportgen-n2.log
- 2026-10-06T09:01Z | CHECK | r=1 | n=2/3 | E04 n=2 acceptance benchmark exit 0: 7/7 cases, global 47.9 s vs baseline 48.08 s and n=1 candidate 44.89 s; 15 artifacts restored clean | /tmp/D134-evidence/candidate-acceptance-n2.log
- 2026-10-06T09:02Z | CHECK | r=1 | n=2/3 | C07 container reconciliation: total 3206 / skipped 189 / executed 3017 = 2893 provider-attributed methods + 65 provider-agnostic methods + 59 theory-data rows; no wholesale skip | /tmp/D134-evidence/integration-container-neg2.log, integration-list-methods-neg2.log
- 2026-10-06T09:02Z | CHECK | r=1 | n=2/3 | C01/C02/C03/C05/C06/C06b/C10/C11 exit 0; allowlist 33 task files (3 core + 6 tests + 23 docs + 1 status), no forbidden path; all CRLF | this file; /tmp/D134-evidence/build-debug-neg2.log … docfx-n2.log
- 2026-10-06T09:22Z | DO | r=1 | n=3/3 | escalate-routed R06 open-failure coverage: 4 deterministic tests added (execute-fail + open-fail, sync+async) to `tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`; open-failure pair red→green (exit 2 → exit 0); minimal `QueryExecutor.cs` fix disposes the planner-created command when `GetDbCommand(Async)` throws in all four reader-open helpers | /tmp/D134-evidence/c08-todatareadersqlite-n3-red.log, c08-todatareadersqlite-n3-green.log
- 2026-10-06T09:22Z | DO | r=1 | n=3/3 | gates: Debug build exit 0 (0/0); Release build exit 0 (0/0) [E06 refreshed]; core 1579/0/0; sqlite 1078/1077/0/1 (+4); SQLite integration 702/663/0/39; SqliteSpecific 76/0/0; container 3206/0/0/189 (PG 757/25, SQL Server 674/43, MySQL 578/79, MariaDB 49/0, ClickHouse 177/0, SQLite 657/39); acceptance 7/7, 46.43 s; `git diff --check` exit 0 | /tmp/D134-evidence/build-debug-n3.log, build-release-n3.log, unit-core-n3.log, unit-sqlite-n3.log, integration-sqlite-n3.log, integration-sqlitespecific-n3.log, integration-container-n3.log, acceptance-n3.log

## DO n=3 record (r=1, no replan) — R06 open-failure coverage (escalate-routed)

Escalate decision after CHECK r=1/n=2: R06 ("an open failure or cancellation does not leak") was uncovered —
the existing cancelled-before-open tests throw before the planner creates a command, and the disposal test
covers only the success path. Deterministic failure-path tests were added; the open-failure pair exposed a
real leak and a minimal production fix closes it.

- **Test-only hooks** (`tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs`) — `TrackingSqliteConnection`
  gained `FailOpen` (overrides `Open`/`OpenAsync`) and `FailExecute` (propagated to every command it mints);
  `TrackingSqliteCommand` gained `FailExecute` (overrides `ExecuteDbDataReader` **and**
  `ExecuteDbDataReaderAsync`) and keeps the existing `WasDisposed`/`HasOpenReader` observers. Defaults off;
  no other test is affected. `CreateTrackingFileDb` seeds a file-backed probe table and leaves the connection
  **closed**, so the next terminal prepares its command on the closed connection before the open attempt.
- **New tests (4)** —
  - `ToDataReader_Sqlite_ExecuteReaderFails_DisposesCommandAndContextRemainsUsable`
  - `ToDataReaderAsync_Sqlite_ExecuteReaderFails_DisposesCommandAndContextRemainsUsable`
  - `ToDataReader_Sqlite_ConnectionOpenFails_DisposesCommandAndContextRemainsUsable`
  - `ToDataReaderAsync_Sqlite_ConnectionOpenFails_DisposesCommandAndContextRemainsUsable`
  Each asserts: the expected `InvalidOperationException` surfaces; the per-call/planner-created command
  `WasDisposed == true` and `HasOpenReader == false`; the failure path did not mutate the shared
  `command.Cache` (still `true`, E07); after the toggle is cleared the **same context** reads a row and a
  normal cached query still returns data.
- **Red→green (open-failure, the real R06 defect, defect key D134-7)** —
  - RED (before fix): `[dotnet,test,tests/nextorm.sqlite.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~ToDataReaderSqlite]`
    → exit **2**, 19 selected, 2 failed — both `ConnectionOpenFails` tests, `failed.WasDisposed` was
    `False`. The execute-failure pair already passed on the old code (`try/finally` in `QueryExecutor.cs`).
    Log `/tmp/D134-evidence/c08-todatareadersqlite-n3-red.log`.
  - FIX (1 applied): `src/nextorm.core/DataContext/QueryExecutor.cs` — in `OpenLobReader(Async)` and
    `OpenResultReader(Async)` seed the local `DbCommand? command = compiledQuery.DbCommand;` before the
    `try` and assign the `GetDbCommand(...)` result inside it, so the existing `finally` disposes the
    planner-created command when `EnsureConnectionOpen(Async)` throws. `GetDbCommandCore` always returns
    `DbCommand`, and these plans are per-call (`storeInCache: false`), so releasing it on failure is safe;
    the raw `OpenReader` and other terminals are untouched.
  - GREEN (after fix): same command → exit **0**, 19 selected, 0 failed.
    Log `/tmp/D134-evidence/c08-todatareadersqlite-n3-green.log`.
- **Affected checks** —
  - `[dotnet,build,nextorm.slnx,-c,Debug]` → exit **0**, 0 warning / 0 error (`build-debug-n3.log`).
  - `[dotnet,build,nextorm.slnx,-c,Release]` → exit **0**, 0 warning / 0 error (`build-release-n3.log`) — **E06 refreshed**.
  - `[dotnet,test,tests/nextorm.core.tests,-c,Debug,--no-build]` → exit **0**, total 1579 / 0 failed / 0 skipped (`unit-core-n3.log`).
  - `[dotnet,test,tests/nextorm.sqlite.tests,-c,Debug,--no-build]` → exit **0**, total 1078 / 1077 passed / 0 failed / 1 skipped (+4 new) (`unit-sqlite-n3.log`).
  - `[dotnet,test,tests/nextorm.integration.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~Sqlite]` → exit **0**, total 702 / 663 passed / 0 failed / 39 skipped (`integration-sqlite-n3.log`).
  - `[dotnet,test,tests/nextorm.integration.tests,-c,Debug,--no-build,--filter,FullyQualifiedName~SqliteSpecificTests]` → exit **0**, total 76 / 0 failed / 0 skipped (`integration-sqlitespecific-n3.log`).
  - Container integration (`QueryExecutor.cs` changed): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock [dotnet,run,--project,tests/nextorm.integration.tests,-c,Debug,--no-build,--,-noColor]`
    → exit **0**, Total 3206 / Errors 0 / Failed 0 / Skipped 189 / Not Run 0 (80.0 s); all five DB containers
    started and stopped. Per-provider executed/skipped: PostgreSQL 757/25, SQL Server 674/43, MySQL 578/79,
    MariaDB 49/0, ClickHouse 177/0, SQLite 657/39 — no provider wholesale-skipped.
    Logs `/tmp/D134-evidence/integration-container-n3.log`, `/tmp/D134-evidence/integration-list-methods-n3.log`.
  - Acceptance benchmark (reader-open path is not the cached path): `[timeout,240,dotnet,run,--project,benchmarks/nextorm.benchmark,-c Release,--,--anyCategories=acceptance]`
    → exit **0**, 7/7 cases, global **46.43 s** vs baseline 48.08 s / n=2 47.9 s — no regression. The cached
    acceptance cases do not call `OpenResultReader`/`OpenLobReader`, so the acceptance path is unaffected
    (`/tmp/D134-evidence/acceptance-n3.log`); the 15 `BenchmarkDotNet.Artifacts` files were restored clean.
- **Hygiene** — `[git,diff,--check]` exit **0**; `QueryExecutor.cs` and the test file are CRLF; scope is the
  prior 33 task files plus `QueryExecutor.cs` (now 4 core files) = 34 entries, no forbidden path.
- **Evidence index (n=3, refreshed)** — E01 `brief-n3.json` (validator exit 0); E05 `build-debug-n3.log`;
  **E06 `build-release-n3.log` (exit 0, 0/0)**; E07 `unit-core-n3.log`, `c08-todatareadersqlite-n3-green.log`;
  E08 `unit-sqlite-n3.log`, `integration-sqlite-n3.log`, `integration-sqlitespecific-n3.log`;
  E09 `integration-container-n3.log` (3206/0/0/189), `integration-list-methods-n3.log` (3149 lines / 3147
  methods); E04 `acceptance-n3.log` (46.43 s, 7/7); **E12 (coverage; the re-gather brief labels it E11)**
  `/tmp/D134-evidence/coverage-n3.cobertura.xml` (collect exit 0; total 8525 / succeeded 5997 / failed 0 /
  skipped 2528), `/tmp/D134-evidence/coverage-report-n3/Summary.txt` (line 87.2% 45455/52124, branch 78.9%
  23820/30188); **E13 CRLF** `/tmp/D134-evidence/E13-crlf-python-n3.txt` (34 files, 0 LF-only/mixed;
  `E13-crlf-python.txt` stale/superseded).
- **Status** — R06 open-failure now covered (D134-7 fixed, red→green); D:01–D:06 remain done, no blockers,
  **check-ready**. No commit (ACT later), no push/merge; `r=1`, `n=3/3`.
- 2026-10-06T09:23Z | DO | r=1 | n=3/3 | hygiene: CRLF normalized on all touched files, `[git,diff,--check]` exit 0, byte-level CRLF check (34 files, 0 LF-only/mixed); allowlist 34 entries (4 core / 6 tests / 23 docs / 1 status), no forbidden path | this file

## CHECK coverage re-gather (r=1, n=3) — no DO iteration consumed

CHECK at r=1/n=3 requested attributable coverage for the **final n=3 snapshot** because
`src/nextorm.core/DataContext/QueryExecutor.cs` changed after the n=2 coverage run. This is an evidence
re-gather: **no production/test code was edited and no DO attempt/iteration was consumed** (`n` stays
`3/3`, `r` stays `1`). All commands were re-run on the frozen n=3 tree.

### C08/C09 coverage at n=3

- **C08 collect** — `[dotnet,dotnet-coverage,collect,"dotnet test nextorm.slnx -c Debug",--settings,coverage.settings.xml,--output-format,cobertura,--output,/tmp/D134-evidence/coverage-n3.cobertura.xml]`
  → exit **0**; run: total **8525**, succeeded **5997**, failed **0**, skipped **2528**
  (n=2: total 8521 / succeeded 5993 / failed 0 / skipped 2528; +4 = the n=3 open-failure facts). Log
  `/tmp/D134-evidence/coverage-collect-n3.log`, artifact `/tmp/D134-evidence/coverage-n3.cobertura.xml`.
- **C09 report** — `[dotnet,reportgenerator,-reports:/tmp/D134-evidence/coverage-n3.cobertura.xml,-targetdir:/tmp/D134-evidence/coverage-report-n3,-reporttypes:TextSummary;Html]`
  → exit **0**; **line 87.2%** (45455/52124), **branch 78.9%** (23820/30188), method 77.7% (5692/7324).
  n=2 was line 87.2% (45447/52116), branch 78.8% (23816/30188) — the n=3 delta is +8 covered lines /
  +4 covered branches (new failure-path facts), totals +8 coverable lines / +0 branches. Both remain ≥
  `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`. **Thresholds are hard only on `main`;** on
  `1.0.9-rc1` this is a pass with **no shortfall** (warning: none). Log
  `/tmp/D134-evidence/coverage-reportgen-n3.log`, summary
  `/tmp/D134-evidence/coverage-report-n3/Summary.txt` (Html in `coverage-report-n3/index.html`).
- **Scope (`coverage.settings.xml`)** — only `.*nextorm\.(core|sqlite|postgres|sqlserver)\.dll$` are
  instrumented (**4 assemblies**); test assemblies are skipped, and `nextorm.mysql`, `nextorm.mariadb`,
  `nextorm.clickhouse`, `nextorm.entityframeworkcore` are **not** measured. Attribute exclusions:
  `ExcludeFromCodeCoverage`, `GeneratedCode`, `Obsolete`.
- **Binding** — the coverage evidence index row is **E12** (the re-gather brief labels it E11; E11 in the
  `## Evidence contract E01–E13` table is the guards row and is unchanged). The **n=2 coverage artifact
  (`coverage-n2.cobertura.xml`, `coverage-report-n2/Summary.txt`) and the n=1 `coverage.cobertura.xml`
  are superseded** as attributable coverage for this task by the n=3 artifacts above.

### E13 CRLF at n=3

- The n=3 byte-level CRLF check over all **34** changed/new task files (`git status --porcelain`, incl.
  `QueryExecutor.cs`): pure CRLF **34**, LF-only **0**, mixed **0**. Artifact
  `/tmp/D134-evidence/E13-crlf-python-n3.txt`.
- `E13-crlf-python.txt` (33 files, pre-`QueryExecutor.cs`) is **stale/superseded** by the n=3 34-file
  verification above; it now carries an appended `[SUPERSEDED …]` banner and must not be cited as the
  n=3 CRLF evidence.

### Progress log (coverage re-gather, append-only)

- 2026-10-06T09:29Z | CHECK | r=1 | n=3/3 | coverage re-gathered at n=3 (no DO iteration consumed): C08 collect exit 0, total 8525 / succeeded 5997 / failed 0 / skipped 2528; C09 reportgen exit 0, line 87.2% (45455/52124), branch 78.9% (23820/30188), method 77.7% (5692/7324) — ≥ 85/75, no shortfall; n=2 coverage artifacts superseded; E13-crlf-python-n3.txt = 34 files pure CRLF, E13-crlf-python.txt stale/superseded | /tmp/D134-evidence/coverage-collect-n3.log, coverage-n3.cobertura.xml, coverage-report-n3/Summary.txt, coverage-reportgen-n3.log, E13-crlf-python-n3.txt

## CHECK r=1/n=3 (PASS) and ACT

CHECK at r=1/n=3 passed on the frozen n=3 tree: **R01–R10 all checked clean**. The final n=3
coverage re-gather stands (line **87.2%** 45455/52124, branch **78.9%** 23820/30188 — above
`MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`); the n=3 acceptance benchmark is **7/7** cases,
global **46.43 s** (baseline 48.08 s, no regression); Debug and Release builds are **0 warning /
0 error**. Defect keys D134-1..D134-7 are all `fixed` (D134-7 fixed at r=1/n=3 with a red→green
regression). Verdict: **PASS**. ACT: task D134 finalized as **done**; the 34 task files are
committed by the collection lane (no push/merge). Cycle `N=1`, `plan revision: r=1`, `attempt:
n=3/3`, `contract: rv=1` held unchanged.

- 2026-10-06T09:30Z | CHECK | r=1 | n=3/3 | CHECK PASS: R01–R10 all checked clean; coverage n=3 line 87.2% / branch 78.9%; acceptance 7/7, 46.43 s; Debug/Release builds 0 warning / 0 error | this file; /tmp/D134-evidence/coverage-report-n3/Summary.txt, acceptance-n3.log, build-debug-n3.log, build-release-n3.log
- 2026-10-06T09:30Z | ACT | r=1 | n=3/3 | ACT done: status set `done`; contract rv=1, r=1, n=3/3 held; 34 task files committed (no push/merge) | this file
