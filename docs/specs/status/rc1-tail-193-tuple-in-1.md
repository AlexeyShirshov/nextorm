# Tuple `IN`/`Contains` across providers — issue #193 (task D193)

- task: D193
- issue: #193 (https://github.com/AlexeyShirshov/nextorm/issues/193)
- collection: `1.0.9-rc1-tail`
- group: G1
- branch: `1.0.9-rc1`
- status: **done** (N=1, r=2, n=2/3, rv=1; W1–W4 + F1/F7/F10/F12/F14 applied; closing-CHECK arity coverage + variant matrix 2026-10-06T22:04Z; independent CHECK r=2 PASS via corrective task rc1-tail-193-verify-1, no product defect; independent CHECK round 1 FAIL on evidence/status kept historical; commits e19d751a + cd1a7c6c + corrective 9858fbe3)
- cycle: N=1
- plan revision: r=2
- attempt: n=2/3
- contract: rv=1
- mode: autonomous (collection); product commit e19d751a, bookkeeping cd1a7c6c; push/merge never performed
- base HEAD: `0c663bb7`

## Goal

Translate/execute a parameterized tuple `IN`/collection `Contains` across the supported providers.
Preserve scalar membership, the PostgreSQL/ClickHouse tuple behavior, and the shared-command / plan-cache
invariants (`QueryCommand` must not be mutated on behalf of one call).

## Acceptance criteria R1–R7

- **R1 — supported CLR tuple collections produce correct matching/nonmatching rows.** Negatives:
  mismatched components, malformed arity, scalar/tuple mixing, unsupported construction, accidental value
  interpolation.
- **R2 — provider matrix verified by SQL assertions + execution.** Negatives: invalid RHS syntax, silently
  skipped providers, SQL Server accepting tuple membership, including empty collections.
- **R3 — empty/default/null + nullable-component positive/negated membership deterministic.** Negatives:
  conflating a null tuple object vs all-null components, unwanted SQL `UNKNOWN`.
- **R4 — cache rebinding + existing scalar behavior intact.**
- **R5 — EN/RU docs match implementation.**
- **R6 — coverage/branch audit.**
- **R7 — perf evidence.**

## Pinned representations

- Flat `System.Tuple` and `System.ValueTuple` collections.
- Captured/enumerable + inline RHS; expression-built tuple LHS.
- Ordered scalar component parameters (not one opaque tuple parameter).
- Arity per verified renderer rules.
- Nested/`Rest` scope resolved by the spike.

## Core hooks (planner references)

- `src/nextorm.core/Visitors/InValuesTranslator.cs:20,37`
- `src/nextorm.core/Query/InValues.cs:84-166,171-220`
- `src/nextorm.core/Visitors/TupleSqlTranslator.cs:16,29,100-147`
- `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:361,376,712`
- `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:618-636`
- `src/nextorm.core/Query/QueryCommand.cs:119`
- `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:253,260`
- `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:70,73`

## Per-provider matrix (list-of-tuples / query RHS / nullable components / empty / null tuple)

- **MySQL/MariaDB**: `(a,b) IN ((@p0,@p1),(@p2,@p3))`; `(a,b) IN (SELECT x,y …)`; nullable → guarded
  component arms; empty → false; null tuple → explicit guard.
- **SQLite**: `(a,b) IN (VALUES (@p0,@p1),(@p2,@p3))` (not a bare row-list); query RHS
  `(a,b) IN (SELECT x,y …)`; nullable guarded; empty false; null guard.
- **PostgreSQL**: `ROW(a,b) IN (ROW(@p0,@p1),ROW(@p2,@p3))`; query RHS `ROW(a,b) IN (SELECT x,y …)`;
  preserve renderer `PostgresDialect.cs:658-664`.
- **ClickHouse**: `tuple(a,b) IN (tuple(@p0,@p1),tuple(@p2,@p3))`; preserve applicable `global_in`;
  verify singleton/null; `ClickHouseDialect.cs:931-937`.
- **SQL Server**: every tuple form rejects **before empty-list folding** with
  `NotSupportedException: "SQL Server does not support tuple IN/Contains translation."`; assert no SQL
  submission (`SqlServerDialect.cs:10`, `ISqlDialect.cs:260`).

## Empty / null / default semantics

- Empty non-null collection → `1 = 0`; null collection → `ArgumentNullException`.
- A `default` value tuple is an ordinary row; a null reference-tuple entry →
  `NotSupportedException`, never an all-null row; SQL Server keeps its rejection.
- Nullable components: null/null matches (mirror `InValuesTranslator.cs:80-106`); null cells `IS NULL`,
  non-null guarded equality; guard nullable LHS components in native non-null row-IN arms; test negation.

## DO plan

- **D193.1** — tuple extraction/component binding/partition+shape (`InValues.cs:171-220`,
  `QueryCommand.cs:119`; shape distinguishes arity/count/null layout, never values).
- **D193.2** — dialect lowering (matrix + rejection; add a membership capability only if the
  tuple-renderer capability is insufficient).
- **D193.3** — SQL/core tests.
- **D193.4** — execution tests (`CommonTestSuite.In.cs:9-68` + provider-specific; SQLite in-process,
  PG/MySQL/MariaDB/CH container, SQL Server container-backed rejection).
- **D193.5** — docs.
- **D193.6** — finalize.

## Variant matrix

Rows (`test|guard|deferred+trigger`): Tuple/ValueTuple × captured/inline/enumerable × providers;
empty/singleton/multiple/duplicates/default/value+reference/nullable components/negation; null
collection/tuple, malformed arity, renderer-unsupported construction; Rest/nesting per spike;
converter-bound components/context reuse; alternate bulk binding deferred + measured-limit trigger.

## Test strategy

- **CORE** `dotnet test tests/nextorm.core.tests -c Debug`
- **SQL(P)** `dotnet test tests/nextorm.P.tests -c Debug --filter "FullyQualifiedName~SqlGenerationTests"`
- **FULL(P)** for P = sqlite, postgres, sqlserver, mysql, mariadb, clickhouse
- **Integration full suite** with
  `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`
- **Coverage** CI commands, thresholds `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`; Stryker absent
  → manual branch audit; **skipped ≠ pass**.

## Docs

EN/RU arrays `docs/scalar-functions/06-arrays.md:158-159` (planner wrote `06-arrays.md`, the real path is
`docs/scalar-functions/06-arrays.md`) + `docs/ru/scalar-functions/06-arrays.md`, `limitations.md:29`,
`providers/overview.md:112`, gap-analysis `:46,460-461`.

## Perf

Required; expansion belongs to preparation/binding. Baseline
`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*SqliteBenchmarkWhere*'` vs HEAD
`0c663bb7`; tuple cold/warm at 0/1/32/256 rows; no fabricated baseline.

## Unit mode / inciting unknown

Sequential, one tree. Inciting unknown: query-RHS null semantics, singleton syntax, arity/`Rest`,
parameter ceilings, cache reuse.

## Evidence contract rv1 (E193-00..06)

| ID | owner | applicability | required result |
|---|---|---|---|
| E193-00 | coder | always | preflight Roslyn/tree facts + reconnaissance-spike probes recorded |
| E193-01 | coder | SQL generation | core/SQL tests exit 0, per-provider SQL assertions |
| E193-02 | coder | per provider | per-provider `FULL(P)` / `SQL(P)` exit 0, counts |
| E193-03 | coder | execution | container integration executed, no provider wholesale-skipped |
| E193-04 | coder | docs | EN+RU docs match implementation |
| E193-05 | coder | always | regression / coverage (85/75 manual branch audit) / perf |
| E193-06 | coder | always | build 0W/0E, CRLF/scope hygiene, no commit |

Artifact root: the cycle evidence dir. Re-gather budget: **2**. CRLF throughout.

## Part 2 — preflight facts (Roslyn / tree)

### Tuple renderer arity coverage

- `TypeFacts.IsTupleType` / `IsValueTupleType` / `IsTupleLike`
  (`src/nextorm.core/TypeFacts.cs:69-99`) recognise the `System.Tuple` and `System.ValueTuple` families
  for arities **1..7 only** (generic type definitions `Tuple<>` … `Tuple<,,,,,,>`); the 8-element
  `…,TRest` form is deliberately **not** recognised (only the CLR `Rest` shape) and nested tuples are not
  flattened.
- `TupleSqlTranslator.TryTranslateCreate` (`:16`) matches **any** `Tuple.Create(...)` call and renders
  `node.Arguments` directly, so the 8-arg `Tuple.Create` overload (`7 values + TRest`) would render the
  `TRest` operand as an extra flat field — **not** handled, no `Rest` support. Arity 1..7 is the verified
  renderer scope.
- `TupleSqlTranslator.TryTranslateNew` (`:29`) gates on `TypeFacts.IsTupleLike`, so `new Tuple<...>` /
  `new ValueTuple<...>` arities 1..7 only; arity ≥8 falls through to the generic value-type path.
- `TupleSqlTranslator.TryTranslateElement` (`:44`) folds `.ItemN` only for `N` in `1..arguments.Count`;
  `RenderElement` positional access is `ITupleRenderer.RenderElement` (PostgreSQL `(row).fN`, ClickHouse
  `tupleElement(row, N)`, `null` for MySQL/MariaDB/SQLite/SQL Server).
- `IsFlatRowConstructor` (`:144`) probes `RenderElement("row",1) is null` → MySQL/MariaDB/SQLite are flat
  row-constructor providers. Implementations:
  `PostgresTupleRenderer` (`PostgresDialect.cs:658`), `ClickHouseTupleRenderer`
  (`ClickHouseDialect.cs:931`), `MySqlTupleRenderer` (`MySqlDialect.cs:527`, shared by MariaDB),
  `SqliteTupleRenderer` (`SqliteDialect.cs:374`). SQL Server has `Tuple => null`
  (`SqlServerDialect.cs`, `ISqlDialect.Tuple` default null) → `SupportsTupleFunctions == false`.

### Public membership overloads

- `CommonFunctions.@in<T>(T, IEnumerable<T>)` (`SqlFunctions.cs:481`), `@in<T>(T, params T[])` (`:487`),
  `@in<T>(T, QueryCommand<T>)` (`:475`, subquery RHS).
- `ClickHouseFunctions.global_in<T>(T, IEnumerable<T>)` (`SqlFunctions.ClickHouse.cs:323`),
  `global_in<T>(T, params T[])` (`:326`), `global_in<T>(T, QueryCommand<T>)` (`:320`, subquery RHS).
- `Enumerable.Contains<TSource>(IEnumerable<TSource>, TSource)`, `MemoryExtensions.Contains` (span form),
  and the instance `List<T>.Contains`/`ICollection<T>.Contains`. `InValues.TryGetContainsArguments`
  (`InValues.cs:103`) accepts the 2-arg and null-comparer 3-arg forms; a span conversion is unwrapped by
  `UnwrapSpanConversion` (`:304`).
- Subquery RHS is excluded from the value-list path: `InValues.TryGetArguments` rejects
  `inValues.Type.IsAssignableTo(typeof(QueryCommand))` (`InValues.cs:89-91`); the `@in(column, QueryCommand)`
  form is handled separately (`CorrelatedQueryExpressionVisitor.cs:28`).

### Existing `InValues` extraction entry points

- `InValues.TryGetArguments` (`InValues.cs:77`) — the single matcher for `@in`/`global_in`/`Contains`.
- `InValues.EvaluatePartition` (`:168`), `InValues.Partition` (`:171`), `InValues.ComputeShapeHash` (`:213`).
- `InValuesTranslator.TryTranslateCollectionContains` (`InValuesTranslator.cs:20`),
  `InValuesTranslator.TranslateInValues` (`:37`).
- Partitions reuse via `QueryCommand.InValuesPartitions` (`QueryCommand.cs:116`), shape hash
  `QueryCommand.InValuesShapeHash` (`:104`); `TranslateInValues` disables cache (`command.Cache = false`,
  `InValuesTranslator.cs:52-53`) only when the shape was **not** folded into the plan key.

### Provider test class identities

- **MariaDB**: `MariaDbContainer` (`tests/nextorm.integration.tests/Providers/MariaDbContainer.cs`,
  internal static, `NEXTORM_MARIADB_CONNECTION`, image `mariadb:11.4`). There is **no**
  `MariaDbTestProvider` — MariaDB is **not** in `ProviderTestSuite`; its integration coverage is
  standalone classes with their own `MariaDbDataContext` (e.g.
  `MariaDbTupleExecutionTests`, `MariaDbCsvIntegrationTests`, `MariaDbFunctionsIntegrationTests`). The
  `-class` selector list therefore has no MariaDB entry.
- **MySQL**: `MySqlTestProvider` (`Providers/MySqlTestProvider.cs`) → `MySqlIntegrationTests` /
  `MySqlSpecificTests`.
- **Per-provider SQL-generation test classes** (namespace → class):
  `NextORM.Sqlite.Tests.SqlGenerationTests` (`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:15`),
  `NextORM.Postgres.Tests.SqlGenerationTests` (postgres `:16`),
  `NextORM.SqlServer.Tests.SqlGenerationTests` (sqlserver `:14`),
  `NextORM.MySql.Tests.SqlGenerationTests` (mysql `:13`),
  `NextORM.MariaDb.Tests.SqlGenerationTests` (mariadb `:10`),
  `NextORM.ClickHouse.Tests.SqlGenerationTests` (clickhouse `:11`). The `SQL(P)` filter
  `FullyQualifiedName~SqlGenerationTests` therefore selects each provider's class.
- Integration `-class` values (skill): `SqliteIntegrationTests`, `SqliteSpecificTests`,
  `PostgresIntegrationTests`, `PostgresSpecificTests`, `SqlServerIntegrationTests`,
  `SqlServerSpecificTests`, `MySqlIntegrationTests`, `MySqlSpecificTests`, `ClickHouseIntegrationTests`
  (MariaDB: run the standalone `MariaDb*` classes; no provider-suite entry).

### Exact coverage / integration selectors

- Coverage collect (CI form):
  `dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" --settings coverage.settings.xml --output-format cobertura --output /tmp/D193-evidence/coverage.cobertura.xml`
- Report: `reportgenerator -reports:/tmp/D193-evidence/coverage.cobertura.xml -targetdir:/tmp/D193-evidence/coverage-report -reporttypes:TextSummary`; thresholds line 85 / branch 75.
- Integration selector (class form):
  `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class nextorm.integration.tests.<Class> -noColor`.
- VSTest filter form: `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~<Class>"`.

## Progress log

- 2026-10-06T13:16Z | DO | r=1 | n=1/3 | cycle start; status file created with planner plan (r=1) | this file
- 2026-10-06T13:16Z | DO | r=1 | n=1/3 | preflight Roslyn/tree facts recorded: arity 1..7 no Rest; membership overloads; InValues entry points; MariaDB has no TestProvider; per-provider SqlGenerationTests namespaces | this file
- 2026-10-06T18:18Z | DO | r=1 | n=1/3 | affected build `dotnet build tests/nextorm.integration.tests -c Debug` exit 0, 0 warning / 0 error | /tmp/D193-evidence/build-integration.log
- 2026-10-06T18:19Z | DO | r=1 | n=1/3 | spike run (throwaway, raw ADO) exit 0: Total 5 / Failed 0 / Skipped 0, 21.4 s; PG17.11 / MySQL8.4.11 / MariaDB11.4.13 / CH25.8.33.6 / SQLite3.53.4 all executed | /tmp/D193-evidence/d193-spike-run.log, /tmp/D193-evidence/d193-spike-observations.txt
- 2026-10-06T14:21Z | DO | r=1 | n=1/3 | scope gate: scripts/validate_inner_loop.py ABSENT; equivalent manual gate passed — 6 scope files and tests/nextorm.{core,sqlite}.tests exist, selectors FullyQualifiedName~InMemoryTests / FullyQualifiedName~SqlGenerationTests attach | this file
- 2026-10-06T14:21Z | DO | r=1 | n=1/3 | DO reconcile build: `dotnet build nextorm.slnx -c Debug` exit 0, 0 warning / 0 error; WIP audited against R1–R7 contracts (arity 1..7, SQL Server first rejection, guarded nullable arms, per-provider render, scalar components, shape-only hash, no `Cache=false`), no deviations found | /tmp/D193-evidence/build-solution.log
- 2026-10-06T14:21Z | DO | r=1 | n=1/3 | inner loop: `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~InMemoryTests` exit 0, total 154 / failed 0 / skipped 0 | /tmp/D193-evidence/test-core-inmemory.log
- 2026-10-06T14:21Z | DO | r=1 | n=1/3 | inner loop: `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~SqlGenerationTests` exit 0, total 543 / failed 0 / skipped 0 | /tmp/D193-evidence/test-sqlite-sqlgen.log
- 2026-10-06T14:26Z | DO | r=1 | n=1/3 | per-provider D193 tuple build+test (previous coder): builds `dotnet build tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests -c Debug` all exit 0, 0 warning / 0 error | /tmp/D193-evidence/build-{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-tests.log
- 2026-10-06T14:26Z | DO | r=1 | n=1/3 | per-provider D193 tuple SQL tests argv `dotnet test tests/nextorm.<P>.tests -c Debug --no-build --filter FullyQualifiedName~D193TupleIn` exit 0 each; core 13/0/0, sqlite 4/0/0, postgres 4/0/0, sqlserver 2/0/0, mysql 4/0/0, mariadb 4/0/0, clickhouse 4/0/0 (total/failed/skipped) | /tmp/D193-evidence/test-{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-tuplein.log
- 2026-10-06T14:27Z | DO | r=1 | n=1/3 | previous coder `dotnet build tests/nextorm.integration.tests -c Debug` exit 0, 0 warning / 0 error — ran BEFORE the ClickHouse tuple tests were added to ClickHouseIntegrationTests.cs (stale) | /tmp/D193-evidence/build-integration-d193-4.log
- 2026-10-06T14:28Z | DO | r=1 | n=1/3 | previous coder integration boundary on the stale build (no ClickHouse tests compiled): `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~Contains_TupleIn|FullyQualifiedName~MariaDbTupleInExecutionTests"` exit 0, total 29 / passed 21 / failed 0 / skipped 8 | /tmp/D193-evidence/test-integration-tuplein-boundary.log
- 2026-10-06T14:29Z | DO | r=1 | n=1/3 | CRLF-normalized ClickHouseIntegrationTests.cs / CommonTestSuite.In.cs / MariaDbTupleInExecutionTests.cs; `dotnet build tests/nextorm.integration.tests -c Debug` exit 0, 0 warning / 0 error (picks up the ClickHouse tuple execution tests) | /tmp/D193-evidence/build-integration-d193-tests.log
- 2026-10-06T14:29Z | DO | r=1 | n=1/3 | boundary attempt with `--filter "..." --filter-not-trait "Category=Spike"` exit 134: xunit v3 rejects VSTest filter combined with a trait filter, Zero tests ran; not-trait filter redundant (spike class `D193TupleInSpikeTests` does not match the selector) → dropped | /tmp/D193-evidence/boundary-d193-ch.log
- 2026-10-06T14:30Z | DO | r=1 | n=1/3 | final integration boundary (rebuilt dll): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~Contains_TupleIn|FullyQualifiedName~MariaDbTupleInExecutionTests"` exit 0, total 34 / succeeded 26 / failed 0 / skipped 8; executed SQLite (in-process), PostgreSQL, MySQL, MariaDB, ClickHouse, SQL Server (container-backed rejection); skips are only the intended gated rejection/success cases (SQL Server 5 + non-SQLServer `OnProviderWithoutRowConstructor` 3) | /tmp/D193-evidence/boundary-d193-ch.log

- 2026-10-06T19:33Z | DO | r=1 | n=1/3 | D193.5 docs complete (EN+RU): tuple-IN matrix + semantics in `docs[/ru]/scalar-functions/06-arrays.md`, SQL Server rejection + deferred RHS/Rest in `docs[/ru]/advanced/limitations.md`, capability row in `docs[/ru]/providers/overview.md`, gap-analysis row/item 22; all 7 files CRLF; `roslyn members NextORM.Core.ITupleRenderer` shows only additive default `RenderInValues` (no documented member renamed, type referenced by xref only → no public-API doc sync needed); no specs link added to public docs | this file, git diff

## D193.1 spike findings (reconnaissance, r=1/n=1)

Throwaway spike `tests/nextorm.integration.tests/D193TupleInSpikeTests.cs` — raw ADO SQL only, no nextorm
translation — run with `-class nextorm.integration.tests.D193TupleInSpikeTests`: **Total 5 / Failed 0 /
Skipped 0**, exit 0, 21.4 s. Observations `/tmp/D193-evidence/d193-spike-observations.txt`; run log
`/tmp/D193-evidence/d193-spike-run.log`; affected build 0W/0E `/tmp/D193-evidence/build-integration.log`.

Observed provider versions: PostgreSQL **17.11**, MySQL **8.4.11**, MariaDB **11.4.13**, ClickHouse
**25.8.33.6**, SQLite **3.53.4**.

| Probe | SQLite | PostgreSQL | MySQL / MariaDB | ClickHouse |
|---|---|---|---|---|
| list-of-tuples match | `(a,b) IN (VALUES (1,'a'),(2,'b'))` → `1,2` | `ROW(a,b) IN (ROW(1,'a'),ROW(2,'b'))` → `1,2` | `(a,b) IN ((1,'a'),(2,'b'))` → `1,2` | `tuple(a,b) IN (tuple(1,'a'),tuple(2,'b'))` → `1,2` |
| bare row-list (alternative) | `(a,b) IN ((1,'a'),(2,'b'))` → `1,2` | n/a (native `ROW`) | n/a (that *is* the form) | `tuple(a,b) IN ((1,'a'),(2,'b'))` → `1,2` |
| component mismatch | `→ 2` | `→ 2` | `→ 2` | `→ 2` |
| singleton | n/a | n/a | n/a | `tuple(a,b) IN (tuple(2,'b'))` → `2` |
| null component (plain `IN`) | `(a,b) IN (VALUES (1,NULL))` → `[]` | `ROW(a,b) IN (ROW(1,NULL))` → `[]` | `(a,b) IN ((1,NULL))` → `[]` | `tuple(a,b) IN (tuple(1,NULL))` → `[]` |
| null-safe comparison | `(a,b) IS (1,NULL)` → `[]` | `ROW(a,b) IS NOT DISTINCT FROM ROW(1,NULL)` → `[]` | `(a,b) <=> (1,NULL)` → `[]` | `isNotDistinctFrom(...)` → **EXCEPTION Code 48 `NOT_IMPLEMENTED`: only in JOIN ON** |
| query RHS `IN (SELECT x,y …)` | `→ 1,2` | `→ 1,2` | `→ 1,2` | `→ 1,2` |
| `GLOBAL IN` | n/a | n/a | n/a | `tuple(a,b) global in (tuple(1,'a'),tuple(2,'b'))` → `1,2` |
| negation (`NOT IN`) | `→ 2,3,4,5` | `→ 2,3,4,5` | `→ 2,3,4,5` | `→ 2,3,4,5` |
| empty RHS | `[]` | `[]` | `[]` | `[]` |

Conclusions / scope freeze:

- **List-of-tuples shape confirmed** for all four non-SQL-Server dialects: SQLite `(a,b) IN (VALUES …)`
  (the planner-pinned form; the bare `(a,b) IN ((…),(…))` is *also* accepted — 3.53.4), MySQL/MariaDB
  `(a,b) IN ((…),(…))`, PostgreSQL `ROW(a,b) IN (ROW(…),ROW(…))`, ClickHouse
  `tuple(a,b) IN (tuple(…),tuple(…))` (singleton and bare `((…))` both accepted).
- **Null components cannot match through row-`IN`**: a tuple containing SQL `NULL` matches nothing (SQL
  `UNKNOWN`) on **every** provider; ClickHouse `isNotDistinctFrom` is `NOT_IMPLEMENTED` outside `JOIN ON`,
  so there is no portable null-safe row comparison. The plan's `null`/`null`-matches semantics therefore
  require **explicit OR-of-AND guarded component arms** (`c1 = @p0 AND c2 IS NULL OR …`); the row-`IN`
  form can be kept only for all-non-null entries, with guarded arms added for null-component entries.
- **Query-derived RHS is feasible at SQL level on all four dialects** (`IN (SELECT x,y …)`), but the
  value-list matcher (`InValues.TryGetArguments`) deliberately excludes `QueryCommand` RHS
  (`InValues.cs:89-91`); the scalar `@in(column, QueryCommand<T>)` path is separate. Freeze: the
  **value-list RHS is the shipped scope**; a tuple-typed `QueryCommand` RHS is the deferred/inciting item
  unless the planner widens D193.2.
- **Arity/Rest freeze**: renderer scope is arity **1..7** (`TypeFacts.IsTupleLike`; `Tuple.Create` renders
  raw `node.Arguments`, so the 8-arg `7+TRest` overload would misrender). Reject arity ≥8 / `Rest` /
  nested tuples with `NotSupportedException`. Nested/Rest is **out of scope**.
- **Negation** renders deterministically; rows whose components are `NULL` are excluded by three-valued
  logic unless the explicit guards above are emitted (plan requires the guarded form and a negation test).
- **SQL Server** has no `Tuple` override in `SqlServerDialect` (grep: no `Tuple`/`SupportsTuple` member) →
  `ISqlDialect.Tuple == null` / `SupportsTupleFunctions == false`; the rejection-before-empty-folding
  contract is consistent with the plan.
- Provider **container** evidence: all 4 containers started and reported executed, none skipped
  (`d703aedb9b21` MySQL, `914f06a0ba5f` MariaDB, plus PG and CH); the assembly fixture stopped them at the
  end of the run.

## Defect history (durable)

| Defect key | Title | First seen | Revision/attempt | Fix applied (r/n) | Evidence | Status |
|---|---|---|---|---|---|---|
| D193-stale-integration-build | ClickHouse tuple execution tests added to `ClickHouseIntegrationTests.cs` after the integration build, so the 14:28Z boundary excluded ClickHouse (29 vs 34) | 2026-10-06T14:28Z | r=1/n=1 | Fixed r=1/n=1 (CRLF normalize + rebuild; rerun 34/26/0/8) | /tmp/D193-evidence/build-integration-d193-tests.log, /tmp/D193-evidence/boundary-d193-ch.log | Resolved |
| D193-runner-filter-conflict | xunit v3 aborts when `--filter` is combined with `--filter-not-trait` (`ArgumentException`, exit 134, Zero tests ran) | 2026-10-06T14:29Z | r=1/n=1 | Fixed r=1/n=1 (dropped redundant not-trait; spike class not matched by the selector) | /tmp/D193-evidence/boundary-d193-ch.log | Resolved |


## DO→CHECK boundary sweep (comprehensive) — 2026-10-06T14:38Z

Progress log:

- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | boundary sweep start (comprehensive full affected projects) | this file
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-final.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | FULL(core) argv `dotnet test tests/nextorm.core.tests -c Debug --no-build` exit 0, total 1637 / passed 1637 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-core.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | FULL(sqlite) exit 0, total 1082 / passed 1081 / failed 0 / skipped 1 | /tmp/D193-evidence/test-full-sqlite.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | FULL(postgres) exit 0, total 775 / passed 775 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-postgres.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | FULL(sqlserver) exit 0, total 699 / passed 699 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-sqlserver.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | FULL(mysql) exit 0, total 289 / passed 289 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-mysql.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | FULL(mariadb) exit 0, total 196 / passed 196 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-mariadb.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | FULL(clickhouse) exit 0, total 550 / passed 550 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-clickhouse.log
- 2026-10-06T14:37Z | DO | r=1 | n=1/3 | integration argv `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --no-build` exit 0, total 3300 / passed 3103 / failed 0 / skipped 197; PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse executed (no wholesale provider skip), SQLite in-process; all 197 skips per-capability | /tmp/D193-evidence/integration-final.log
- 2026-10-06T14:38Z | DO | r=1 | n=1/3 | coverage core argv `dotnet-coverage collect "dotnet test tests/nextorm.core.tests -c Debug --no-build" --settings coverage.settings.xml ...` exit 0; line 58.8% / branch 52.8% (whole core assembly) | /tmp/D193-evidence/coverage-core.xml
- 2026-10-06T14:38Z | DO | r=1 | n=1/3 | coverage sqlite argv `dotnet-coverage collect "dotnet test tests/nextorm.sqlite.tests ..." ...` exit 0; line 57.7% / branch 50.5% (core+sqlite assemblies) | /tmp/D193-evidence/coverage-sqlite.xml
- 2026-10-06T14:38Z | DO | r=1 | n=1/3 | manual branch audit recorded; named uncovered D193 branches: PartitionTuple non-enumerable / non-ICollection / non-ITuple; tuple param-mode pass + AddTupleParameters (0 hits); malformed-LHS guard; cached-partition-not-tuple guard; converter-bound component; TupleArity else; IsTupleFamily name-null | this file

### Command evidence

| Suite | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|
| build `nextorm.slnx -c Debug` | 0 | - | - | 0 W / 0 E | - | build-final.log |
| core | 0 | 1637 | 1637 | 0 | 0 | test-full-core.log |
| sqlite | 0 | 1082 | 1081 | 0 | 1 | test-full-sqlite.log |
| postgres | 0 | 775 | 775 | 0 | 0 | test-full-postgres.log |
| sqlserver | 0 | 699 | 699 | 0 | 0 | test-full-sqlserver.log |
| mysql | 0 | 289 | 289 | 0 | 0 | test-full-mysql.log |
| mariadb | 0 | 196 | 196 | 0 | 0 | test-full-mariadb.log |
| clickhouse | 0 | 550 | 550 | 0 | 0 | test-full-clickhouse.log |
| integration (container) | 0 | 3300 | 3103 | 0 | 197 | integration-final.log |

The one sqlite skip is the pre-existing `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` capability gate, unrelated to D193. Integration skip reasons are all per-capability; PostgreSQL/MySQL/SQL Server/ClickHouse skip classes contain executed tests and SQLite runs in-process, so no provider is wholesale-skipped.

### Changed-file coverage (union of the two prescribed collects)

| File | Line % | Branch % |
|---|---|---|
| nextorm.core/Query/InValues.cs | 87.6% (205/234) | 48.1% (37/77) |
| nextorm.core/Visitors/InValuesTranslator.cs | 83.7% (144/172) | 58.9% (33/56) |
| nextorm.core/Visitors/TypeFacts.cs | 100.0% (74/74) | 74.7% (62/83) |
| nextorm.core/Visitors/TupleSqlTranslator.cs | 80.6% (87/108) | 54.2% (26/48) |
| nextorm.core/DataContext/Dialect/DialectCapabilities.cs | 69.0% (40/58) | 20.0% (1/5) |
| nextorm.sqlite/SqliteDialect.cs | 84.9% (191/225) | 38.4% (68/177) |

Caveats: (a) the two prescribed collects run only `nextorm.core.tests` and `nextorm.sqlite.tests`, so the assembly-wide 58.8/52.8 and 57.7/50.5 are a subset of the CI full-solution coverage and are below the 85/75 thresholds — the thresholds are only meaningful on the full-solution `dotnet-coverage collect "dotnet test nextorm.slnx"` run, which this sweep did not perform; (b) DialectCapabilities.cs and SqliteDialect.cs branch% is dominated by pre-existing unrelated branches — their two D193-added members (`ITupleRenderer.RenderInValues` default and `SqliteTupleRenderer.RenderInValues`) are branchless; (c) branches exercised only by the postgres/sqlserver/mysql/mariadb/clickhouse test projects are not instrumented by these two collects.

### Manual branch audit — new branches in the production diff

Covered (all directions):
- `TypeFacts.IsTupleFamily` generic check / `Tuple`+`ValueTuple` prefix (`Tuple` and `ValueTuple` arities 2..7 via D193; scalar `Contains` via existing tests).
- `InValues.EvaluatePartition` tuple-vs-scalar ternary; `PartitionTuple` `!IsTupleLike` throw (arity-8 test), nested-component throw, null-collection throw; `ExtractTupleRow` null-entry throw and `tuple.Length != arity` (mismatched-arity test); `ShapeVisitor` tuple-vs-scalar hash and per-cell null hash loop.
- `InValuesTranslator` tuple routing; `visitor.Dialect.Tuple is null` rejection (SQL Server SQL-gen + integration rejection test); all-non-null row-IN arm + nullable guards; null-component OR-of-AND arms; `arms.Count` switch 0/1/_ (empty / single-row / mixed); `RowHasNull`.
- `TupleSqlTranslator.TryGetConstructorArguments` switch incl. `UnwrapConvert` — fully covered.
- `ITupleRenderer.RenderInValues` default (MySQL/MariaDB/PostgreSQL/ClickHouse SQL-gen) and `SqliteTupleRenderer.RenderInValues` override (SQLite SQL-gen) — branchless; covered by their provider projects (not instrumented by the two collects).

Uncovered / partially covered (named):
1. `InValues.PartitionTuple` — non-enumerable value throw (new L230) never taken.
2. `InValues.PartitionTuple` — `value is ICollection ? ... : []` else side (new L234): only `List<>` inputs tested.
3. `InValues.ExtractTupleRow` — `item is not ITuple` operand (new L247): only the arity-mismatch operand tested.
4. `InValuesTranslator.TranslateTupleInValues` — `!TypeFacts.IsTupleLike(elementType)` throw (new L138): no D193 test; likely unreachable because `PartitionTuple` rejects arity ≥ 8 first.
5. `InValuesTranslator.TranslateTupleInValues` — malformed-LHS / arity-mismatch throw (new L142): no D193 test supplies a non-constructor or mismatched-arity LHS.
6. `InValuesTranslator.TranslateTupleInValues` — cached-partition-not-tuple guard (new L154) and the `command is null` / `TryGetValue` false directions at new L149: defensive; hit arm is covered.
7. `InValuesTranslator.TranslateTupleInValues` — `IsParamMode` true arm (new L164-173) and `AddTupleParameters` (new L249-266): 0 hits in the sqlite collect; no D193 test drives the tuple parameter-only pass (the scalar analogue at old L74-82 executed once in the same run).
8. `InValuesTranslator.TranslateTupleInValues` — converter-bound component (new L160 non-null `?.Converter`): no D193 test uses `[ValueConverter]` on a tuple component (variant-matrix item, deferred).
9. `TypeFacts.TupleArity` — `: 0` else (new L136): unreachable from production, which guards with `IsTupleLike` first.
10. `TypeFacts.IsTupleFamily` — `name is not null` false direction (new L127): `GetGenericTypeDefinition().FullName` is never null for the recognized tuple defs; defensive.
11. `InValuesTranslator.IsNullableComponent` — `!type.IsValueType` reference-type direction (new L287): not in the measured runs; exercised only by the MariaDB integration execution test `(long, string?)`, not by a SQL-generation test.

Stryker was not used (manual audit, per brief).

## D193 branch-close — reachable uncovered branches (tests + coverage) — 2026-10-06T14:48Z

Progress log:

- 2026-10-06T14:48Z | DO | r=1 | n=1/3 | branch-close tests added: core `D193TupleInTests.cs` (5 tests: nullable value-in-slot positive+negation, shared-Any two-shape rebind/cache, PartitionTuple non-enumerable / non-ICollection / non-ITuple) and sqlite `D193TupleInSqlGenerationTests.cs` (5 tests: reference-type nullable guard, sequential two-shape rebind/cache, converter-bound component, tuple-typed LHS malformed guard, null reference-tuple entry) | this file
- 2026-10-06T14:48Z | DO | r=1 | n=1/3 | build `dotnet build tests/nextorm.{core,sqlite}.tests -c Debug` both exit 0, 0 Warning / 0 Error | /tmp/D193-branch-evidence/build-{core,sqlite}-final.log
- 2026-10-06T14:48Z | DO | r=1 | n=1/3 | filtered run `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~D193"` exit 0, total 18 / succeeded 18 / failed 0 / skipped 0; `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~D193"` exit 0, total 9 / succeeded 9 / failed 0 / skipped 0 | /tmp/D193-branch-evidence/test-{core,sqlite}-d193-final.log
- 2026-10-06T14:48Z | DO | r=1 | n=1/3 | full-suite boundary: core total 1642 / succeeded 1642 / failed 0 / skipped 0; sqlite total 1087 / succeeded 1086 / failed 0 / skipped 1 (pre-existing RowId LOB gate) | /tmp/D193-branch-evidence/coverage-{core,sqlite}-collect*.log
- 2026-10-06T14:48Z | DO | r=1 | n=1/3 | coverage before/after same-tooling baseline (core+sqlite cobertura union): InValues.cs branch 70.8%->72.7% (L230 1/2->2/2, L234 1/2->2/2, L247 3/4->4/4), line 87.6%->88.0%; InValuesTranslator.cs branch 74.1%->77.7% (L142 2/4->4/4, L160 1/2->2/2, L287 1/2->2/2), line 83.7%->84.9% | /tmp/D193-branch-evidence/coverage-{core,sqlite}-base.xml, coverage-{core,sqlite}.xml, covdiff.py
- 2026-10-06T14:48Z | DO | r=1 | n=1/3 | remaining uncovered D193 branches unchanged and defensive/unreachable: InValuesTranslator L138 `!IsTupleLike` throw (PartitionTuple rejects arity >= 8 first), L149/L154 cached-partition miss + non-tuple throw, L164-172 param-only tuple pass/AddTupleParameters; TypeFacts defensive branches; Stryker absent — manual audit | /tmp/D193-branch-evidence/covlines.py

### Branch-close coverage delta (same tooling, core+sqlite cobertura union)

| File | line before->after | branch before->after | newly covered branch lines |
|---|---|---|---|
| InValues.cs | 87.6%->88.0% | 70.8%->72.7% | L230 non-enumerable throw; L234 non-ICollection else; L247 non-ITuple entry |
| InValuesTranslator.cs | 83.7%->84.9% | 74.1%->77.7% | L142 malformed-LHS guard; L160 converter-bound component; L287 IsNullableComponent reference-type direction |

No defect candidate: every new test passed against the existing implementation (converter provider value "Active", null-arm/guard SQL, malformed-LHS and null-tuple-entry rejections, non-collection/non-tuple partition rejections, two-shape rebind with `Cache` still true). Defect history unchanged.

## D193 R7 perf evidence — acceptance cached path (2026-10-06T14:52Z)

Progress log:

- 2026-10-06T14:52Z | DO | r=1 | n=1/3 | R7 acceptance argv `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, 7 cases, 0 failures; external shell wall `61.92 s`, BDN `Global total time 50.26 s` (limit ≤4 min); host AMD Ryzen 7 5800HS / Ubuntu 22.04.5 LTS / .NET SDK 10.0.401 / runtime 10.0.12 / BenchmarkDotNet 0.15.8 / `Job.ShortRun` + `InProcessEmitToolchain`, `NEXTORM_BENCH_FULL` unset; load average ~4.6–5.2 on 8 logical / 4 physical cores at start (host not quiet) | /tmp/D193-perf/acceptance.log
- 2026-10-06T14:52Z | DO | r=1 | n=1/3 | R7 cached-vs-prepared `Cached_ToList / Prepared_ToList` = 2581.9 us / 1249.1 us = **2.067** vs documented baseline **1.87** (+10.0%); trigger `1.87 x 1.20 = 2.244` → **below** the 20% investigation threshold, no rerun required; allocated ratio 541.01 KB / 76.14 KB = **7.11** vs baseline **7.42** (−4.2%, unchanged) | /tmp/D193-perf/acceptance.log
- 2026-10-06T14:52Z | DO | r=1 | n=1/3 | R7 scalar IN/Contains benchmark (the shared membership path was edited) argv `... --filter '*SqliteBenchmarkFeaturePlanCache*'` exit 0, 5 cases, 0 failures; wall `36.11 s`, BDN `32.4 s`; no tuple-valued benchmark exists, so the tuple expansion is covered by the one-time-preparation argument | /tmp/D193-perf/in-bench.log
- 2026-10-06T14:52Z | DO | r=1 | n=1/3 | R7 BDN artifacts restored `git checkout -- BenchmarkDotNet.Artifacts` exit 0; `git status --porcelain -- BenchmarkDotNet.Artifacts` empty and no `BenchmarkDotNet|Artifacts` entry in the full status | this file, git status

### Acceptance run — 7 cases, 0 failures, exit 0 (external wall 61.92 s, BDN 50.26 s)

| # | Case | Mean | Allocated | Delta Mean vs baseline |
|---|------|------|-----------|------------------------|
| 1 | `Nextorm_Count` | 3.489 ms | 334.38 KB | +19.7% (high-variance row, not an assertion) |
| 2 | `Nextorm_GroupByCount` | 90.53 ms | 50.06 MB | +48.5% (high-variance row) |
| 3 | `Nextorm_Cached` | 2.911 ms | 547.71 KB | +64.3% (high-variance row) |
| 4 | `Prepared_ToList` | 1,249.1 us | 76.14 KB | +35.2% (host-noise-inflated) |
| 5 | `Cached_ToList` | 2,581.9 us | 541.01 KB | +49.4% (host-noise-inflated) |
| 6 | `Cached_PlanOnly_Param` | 734.7 us | 464.86 KB | +41.7% (host-noise-inflated) |
| 7 | `Nextorm_Cached_ToListAsync` | 2.909 ms | 565.5 KB | +36.1% |

Absolute means are inflated across the board (including the prepared arm, +35.2%), i.e. a noisy host, so no
individual row delta is read as a regression; the tracked **within-run** cached-vs-prepared ratio is the only
comparison and it is **2.067** vs baseline **1.87** (+10.0%), below the **2.244** trigger. **Regression
verdict: noise / not reproduced** (no rerun triggered). This acceptance set **did run on the current
hardware** (AMD Ryzen 7 5800HS, same host/config as the documented baseline) and measured wall time
**61.92 s** external / **50.26 s** BDN, both under the 4 min budget.

### Scalar IN/Contains benchmark — `SqliteBenchmarkFeaturePlanCache` (5 cases, 0 failures, exit 0)

Warm plan-only probes (no DB round-trip), Baseline = `Warm_PlanOnly_Distinct`; these exercise the edited
`InValuesTranslator.TranslateInValues` (the new `TypeFacts.IsTupleFamily` top guard) on the **scalar**
`@in`/`Contains` path:

| Case | Mean | Ratio | Allocated | Alloc ratio |
|------|------|-------|-----------|-------------|
| `Warm_PlanOnly_Distinct` (baseline) | 422.5 us | 1.00 | 367.97 KB | 1.00 |
| `Warm_PlanOnly_In_AtIn_Captured` | 1,048.5 us | 2.48 | 693.76 KB | 1.89 |
| `Warm_PlanOnly_In_AtIn_Inline` | 1,120.4 us | 2.65 | 577.35 KB | 1.57 |
| `Warm_PlanOnly_In_ListContains_Inline` | 1,203.0 us | 2.85 | 584.38 KB | 1.59 |
| `Warm_PlanOnly_In_ListContains_Captured` | 1,291.6 us | 3.06 | 707.04 KB | 1.92 |

These fixtures are **scalar IN-list** probes, not tuple-valued; they show the scalar membership/plan-cache
path still works and are recorded as protection evidence, not as a before/after tuple measurement (no
pre-change tuple baseline exists).

### Tuple expansion is one-time per preparation — no per-row benchmark applicable

No existing benchmark exercises tuple-valued `IN`/`Contains`; per the brief **no new benchmark was
authored**. The tuple expansion runs **once per command preparation / plan build, not per row**:

- `InValuesTranslator.TranslateTupleInValues` (`src/nextorm.core/Visitors/InValuesTranslator.cs:131`) and
  `InValuesTranslator.AddTupleParameters` (`src/nextorm.core/Visitors/InValuesTranslator.cs:249`) render the
  RHS arms and register parameters once while the command SQL is built.
- `InValues.PartitionTuple` (`src/nextorm.core/Query/InValues.cs:214`) enumerates the collection into rows
  once; the evaluated partition is folded into the plan shape by `InValues.ComputeShapeHash`
  (`src/nextorm.core/Query/InValues.cs:299`) and reused via the cached `QueryCommand.InValuesPartitions`
  (`src/nextorm.core/Query/InValues.cs:171-220`), not re-walked per execution.
- Per execution only parameter rebinding is paid; there is no new per-row work, so a per-row benchmark is
  not applicable. This matches the acceptance result (ratio within noise).

R7 verdict: **no regression**. No blocker.

## CHECK r=1 — verdict FAIL / ACT blocked (loop-back CHECK→PLAN) — iteration n=2/3

Progress log:

- 2026-10-06T20:15Z | CHECK | r=1 | n=2/3 | verdict **FAIL / ACT blocked**; loop-back `CHECK→PLAN`; W1 cache-shape safety, W2 `Nullable<ValueTuple>` routing hole, W3 throwaway spike in normal suite, W4 SQL Server rejection ordering/message; evidence `/tmp/D193-evidence/` + scout/review reports | /tmp/D193-evidence/, scout report, review report

### Defect history (CHECK additions, r=1/n=2)

| Defect key | Title | First seen | Revision/attempt | Fix applied (r/n) | Evidence | Status |
|---|---|---|---|---|---|---|
| D193-W1 | Cache-shape safety | CHECK | r=1/n=2 | Fixed r=2/n=1 — `QueryCommand.HasUnkeyedTupleInValues` + `ScanUnkeyedTupleInValues` + planner call-local cache suppression; nested W1 test reshaped to a scalar referenced subquery whose HAVING carries the tuple list | /tmp/D193-evidence/build-r2-final.log, test-sqlite-d193-r2final.log, boundary-d193-r2.xml | **fixed-verified** |
| D193-W2 | `Nullable<ValueTuple>` routing hole | CHECK | r=1/n=2 | Fixed r=2/n=1 — nullable tuple element rejected with the pinned `NullableTupleElementNotSupportedMessage` in `InValues.PartitionTuple` and before arity/LHS checks in `InValuesTranslator.TranslateTupleInValues` | /tmp/D193-evidence/test-{core,sqlite}-focused-r2final.log | **fixed** |
| D193-W3 | Throwaway spike in normal suite | CHECK | r=1/n=2 | Fixed r=2/n=1 — `tests/nextorm.integration.tests/D193TupleInSpikeTests.cs` removed; absent from tree and `git status --porcelain` | `git status --porcelain` | **fixed** |
| D193-W4 | SQL Server rejection ordering/message | CHECK | r=1/n=2 | Fixed r=2/n=1 — `EnsureProviderSupportsTupleInValues` provider preflight before shape evaluation (WHERE/PREWHERE/refresh) and render-time guard before empty-list folding; one pinned message, no SQL submitted | /tmp/D193-evidence/test-sqlserver-focused-r2final.log | **fixed** |

## DO r=2 — W1–W4 fixes, tests and boundary re-verification (2026-10-06T20:33Z)

- Replan `CHECK→PLAN` (r=1→r=2), attempt reset to n=1/3. Fixes are additive; no acceptance criterion or
  pinned representation changed.
- **W1 cache-shape safety.** The owning command now carries `HasUnkeyedTupleInValues`, set by
  `ScanUnkeyedTupleInValues` during `PrepareCommand`. **Scan-scope clarification:** the scan inspects only
  the clauses whose captured-collection shape is *not* folded into the plan key — `_exp` (the SELECT
  projection), `_having`, every JOIN condition, and, recursively, `_referencedQueries` (nested/correlated
  subqueries). `_condition` (WHERE) and PREWHERE are **correctly excluded**, because their shape already
  participates in the plan key via `InValuesShapeHash`; scanning them would needlessly suppress the cache
  for the one cacheable form. When the flag is set, `QueryPlanner` clears the *call-local* `storeInCache`
  only (both lookup and store gates test it), never the sticky `QueryCommand.Cache` — the shared
  Any/Count command stays cacheable. The flag is copied by `CloneForCache` (`QueryCommand.Clone.cs`).
- **W1 test fix.** `TupleContains_InNestedSubquerySelect_...` used `SqlFunctions.Sql.exists(inner)`, whose
  `IgnoreColumns` drops the inner projection, rendering `select exists(select * …)` with 0 parameters and
  never nesting the tuple shape. Replaced with `TupleContains_InNestedReferencedHaving_...`: a scalar
  referenced subquery (`…GroupBy(c => c.Id).Having(c => tuples.Contains(...)).Select(c => c.Id).First()`)
  whose HAVING is rendered, exercised recursively through `_referencedQueries`; the two-different-size
  assertion (4 vs 1 params, distinct prepared commands) is kept. The debug `DbCommand.CommandText`
  `because` clause was reverted.
- **W2.** A `Nullable<ValueTuple<…>>` element is a tuple family but has no null-safe row shape; it is now
  rejected with the single pinned `NullableTupleElementNotSupportedMessage` in `PartitionTuple` (before
  null/enumeration work) and in `TranslateTupleInValues` (before the arity/LHS checks). Covered by core
  `PartitionTuple_NullableValueTupleElement_ShouldThrowNotSupported` and sqlite
  `TupleContains_NullableValueTupleElement_ShouldThrowNotSupported`.
- **W4.** SQL Server rejection is now ordered before any shape evaluation: `EnsureProviderSupportsTupleInValues`
  runs in `PrepareWhere`, `PreparePreWhere` and `RefreshInValuesShape` (via the concrete
  `DataContext.Dialect.Tuple is null` check), and `InValuesTranslator.TranslateTupleInValues` still guards
  before the empty-list fold. One pinned message
  `"SQL Server does not support tuple IN/Contains translation."`; null collection, null entry, arity ≥ 8
  and nested shapes therefore surface the pinned rejection, not an evaluation failure, and no SQL is
  submitted (sqlserver D193 tests: 7/7).
- **W3.** `tests/nextorm.integration.tests/D193TupleInSpikeTests.cs` no longer exists and is absent from
  `git status --porcelain`; the reconnaissance spike is gone from the normal suite.

### r=2 evidence (CRLF throughout; no commit)

| Check | argv | exit | result | log |
|---|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning / 0 Error | /tmp/D193-evidence/build-r2-final.log |
| sqlite W1 (after fix) | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~D193"` | 0 | total 17 / failed 0 / skipped 0 | /tmp/D193-evidence/test-sqlite-d193-r2final.log |
| focused core | `--filter "FullyQualifiedName~D193\|FullyQualifiedName~TupleIn"` | 0 | 22 / 0 / 0 | /tmp/D193-evidence/test-core-focused-r2final.log |
| focused sqlite | same selector | 0 | 17 / 0 / 0 | /tmp/D193-evidence/test-sqlite-focused-r2final.log |
| focused sqlserver | same selector | 0 | 7 / 0 / 0 | /tmp/D193-evidence/test-sqlserver-focused-r2final.log |
| focused postgres | same selector | 0 | 4 / 0 / 0 | /tmp/D193-evidence/test-postgres-focused-r2final.log |
| focused mysql | same selector | 0 | 4 / 0 / 0 | /tmp/D193-evidence/test-mysql-focused-r2final.log |
| focused mariadb | same selector | 0 | 4 / 0 / 0 | /tmp/D193-evidence/test-mariadb-focused-r2final.log |
| focused clickhouse | same selector | 0 | 4 / 0 / 0 | /tmp/D193-evidence/test-clickhouse-focused-r2final.log |
| boundary (containers) | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -filterVSTest "FullyQualifiedName~D193\|FullyQualifiedName~TupleIn" -result-xml /tmp/D193-evidence/boundary-d193-r2.xml -noColor` | 0 | total 34 / passed 26 / failed 0 / skipped 8 | /tmp/D193-evidence/boundary-d193-r2.log, boundary-d193-r2.xml |

Per-provider boundary execution (from `boundary-d193-r2.xml`): SQLite 5 pass + 1 skip (in-process),
PostgreSQL 5 + 1, SQL Server 1 pass + 5 skip (rejection) — container-backed, MySQL 5 + 1, MariaDB 5
(standalone class), ClickHouse 5. All six providers executed; 5 Testcontainers instances started/stopped
(PG, SQL Server, MySQL, MariaDB, ClickHouse); no provider wholesale-skipped; no failures.

W1 status is **fixed-verified** (the reshaped nested test was the only red before this pass and is green
now). W2/W3/W4 are **fixed**. No new defect candidate; no blocker.

## Comprehensive boundary sweep r=2 (post W1–W4) — 2026-10-06T15:42Z (DO→CHECK boundary)

Progress log:

- 2026-10-06T15:34Z | DO | r=2 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-r2-sweep.log
- 2026-10-06T15:35Z | DO | r=2 | n=1/3 | per-provider FULL suites (all eight test projects built; seven non-integration) exit 0 each; core 1643/0/0, sqlite 1095/1094/0/1, postgres 775/0/0, sqlserver 704/0/0, mysql 289/0/0, mariadb 196/0/0, clickhouse 550/0/0 (total/succeeded/failed/skipped) | /tmp/D193-evidence/test-full-r2-<P>.log
- 2026-10-06T15:36Z | DO | r=2 | n=1/3 | full container integration argv `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --no-build` exit 0, total 3295 / passed 3098 / failed 0 / skipped 197; all six providers executed, zero failures, no wholesale provider skip | /tmp/D193-evidence/integration-r2.log
- 2026-10-06T15:41Z | DO | r=2 | n=1/3 | per-provider execution cross-check via a second full integration run with `-result-xml` exit 0, identical totals 3295/3098/0/197; ClickHouse 200 exec / 0 skip, MariaDB 55/0, SQL Server 724 (676 pass + 48 skip), MySQL 664 (584+80), PostgreSQL 826 (800+26), SQLite 704 (663+41); 5 containers started/stopped (PG, SQL Server, MySQL, MariaDB, ClickHouse) | /tmp/D193-evidence/integration-r2.xml, /tmp/D193-evidence/integration-r2-xmlrun.log
- 2026-10-06T15:38Z | DO | r=2 | n=1/3 | CI-form coverage `dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" --settings coverage.settings.xml --output-format cobertura --output /tmp/D193-evidence/coverage-r2.xml` exit 0; full solution 8735 total / 6123 succeeded / 0 failed / 2612 skipped (integration providers skipped without DOCKER_HOST, as in CI) | /tmp/D193-evidence/coverage-r2-collect.log
- 2026-10-06T15:38Z | DO | r=2 | n=1/3 | reportgenerator TextSummary exit 0: Assemblies 4, **Line 87.3% (46263/52988) >= 85**, **Branch 79.1% (24300/30710) >= 75** — both CI thresholds met | /tmp/D193-evidence/coverage-report/Summary.txt
- 2026-10-06T15:42Z | DO | r=2 | n=1/3 | manual branch audit of new r=2 branches: W1 planner bypass, W4 preflight, W2 nullable rejection fully covered; two `ScanUnkeyedTupleInValues` branches uncovered (visited-cycle guard; recursive-descent positive side) — named below; Stryker absent | this file

### Command evidence (r=2 boundary)

| Suite | argv | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | - | - | 0 W / 0 E | - | build-r2-sweep.log |
| core | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | 0 | 1643 | 1643 | 0 | 0 | test-full-r2-core.log |
| sqlite | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` | 0 | 1095 | 1094 | 0 | 1 | test-full-r2-sqlite.log |
| postgres | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build` | 0 | 775 | 775 | 0 | 0 | test-full-r2-postgres.log |
| sqlserver | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | 0 | 704 | 704 | 0 | 0 | test-full-r2-sqlserver.log |
| mysql | `dotnet test tests/nextorm.mysql.tests -c Debug --no-build` | 0 | 289 | 289 | 0 | 0 | test-full-r2-mysql.log |
| mariadb | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build` | 0 | 196 | 196 | 0 | 0 | test-full-r2-mariadb.log |
| clickhouse | `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build` | 0 | 550 | 550 | 0 | 0 | test-full-r2-clickhouse.log |
| integration | `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --no-build` | 0 | 3295 | 3098 | 0 | 197 | integration-r2.log |

The single per-provider skip is the established `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`
capability gate (`NEXTORM_LOB_SQLITE_PROBE=1` to run), unrelated to D193.

### Integration per-provider execution (parsed from `integration-r2.xml`)

| Provider | total | passed | failed | skipped | evidence |
|---|---|---|---|---|---|
| ClickHouse | 200 | 200 | 0 | 0 | `ClickHouseIntegrationTests` + 9 standalone CH classes |
| MariaDB | 55 | 55 | 0 | 0 | `MariaDb*` standalone classes |
| SQL Server | 724 | 676 | 0 | 48 | `SqlServerIntegrationTests`/`SpecificTests` (container) |
| MySQL | 664 | 584 | 0 | 80 | `MySqlIntegrationTests`/`SpecificTests` (container) |
| PostgreSQL | 826 | 800 | 0 | 26 | `PostgresIntegrationTests`/`SpecificTests` (container) |
| SQLite | 704 | 663 | 0 | 41 | `SqliteIntegrationTests` (in-process) |
| EF filter lifecycle / other | 122 | 120 | 0 | 2 | `EfCoreQueryFilterLifecycleTests` + core/identity/LOB (2 LOB probes skip) |

All 197 integration skips are per-capability gates (LOB streaming, CTAS/temp-table, RETURNING, CTE mutation,
batches, lateral/APPLY, etc.). The D193-specific gates are the expected pair: `This provider has no row-value
constructor` (5 — the SQL Server positive tuple executions) and `This provider supports tuple IN/Contains`
(3 — the rejection test skipped on tuple-capable providers). No provider is wholesale-skipped and no failures.

### Coverage (CI form) — four configured assemblies and changed files

Aggregate (`Summary.txt`): Assemblies **4**, **Line 87.3%**, **Branch 79.1%** — both >= 85/75.
Assembly line coverage: nextorm.core 87.5%, nextorm.sqlite 90.5%, nextorm.postgres 79.6%, nextorm.sqlserver 80.9%.

| Changed file | Line | Branch |
|---|---|---|
| nextorm.core/Query/QueryCommand.cs | 93.0% (719/773) | 79.0% (256/324) |
| nextorm.core/Query/QueryCommand.QueryPreparer.cs | 95.0% (2482/2614) | 87.3% (1798/2060) |
| nextorm.core/DataContext/QueryPlanner.cs | 96.6% (980/1014) | 89.1% (424/476) |
| nextorm.core/Query/InValues.cs | 90.3% (448/496) | 80.2% (260/324) |
| nextorm.core/Visitors/InValuesTranslator.cs | 97.7% (340/348) | 94.7% (216/228) |
| nextorm.core/TypeFacts.cs | 97.0% (192/198) | 91.9% (362/394) |
| nextorm.core/DataContext/Dialect/DialectCapabilities.cs | 100.0% (118/118) | 60.0% (12/20) |
| nextorm.sqlite/SqliteDialect.cs | 87.1% (392/450) | 64.0% (456/712) |

Per-file branch gaps in DialectCapabilities.cs / SqliteDialect.cs are dominated by pre-existing unrelated
branches (D193 added the branchless `ITupleRenderer.RenderInValues` default and `SqliteTupleRenderer.RenderInValues`).

### Manual branch audit — NEW r=2 lines (Stryker absent)

Covered (cobertura `condition-coverage` from `coverage-r2.xml`):

| New r=2 branch | Site | cond | D193 test |
|---|---|---|---|
| W1 planner bypass `if (HasUnkeyedTupleInValues)` | QueryPlanner.cs:573 | 2/2 | true: sqlite `TupleContains_In{Having,JoinOn,SelectColumn,NestedReferencedHaving}_...`; false: `TupleContains_InWhere_SecondIdenticalCall_ShouldBeCacheHit` |
| W1 flag reset/clone | QueryCommand.cs:843, QueryCommand.Clone.cs:35 | line hit | sqlite plan-cache tests |
| W4 preflight short-circuit `condition is null \|\| !ContainsTupleInValues` | QueryPreparer.cs:1860 | 4/4 | sqlserver 7 D193 tests + every non-tuple prepare |
| W4 preflight `(as DataContext)?.Dialect is {} && dialect.Tuple is null` | QueryPreparer.cs:1863 (throw L1865) | 6/6 | sqlserver D193 tests (Tuple null) + sqlite/postgres/mysql (Tuple non-null) |
| W4 call sites WHERE/PREWHERE/refresh | QueryPreparer.cs:2126, :2159, :175–176 | line hit | sqlserver D193 tests |
| W1 scan `_exp` projection | QueryPreparer.cs:1884 | 4/4 | sqlite `TupleContains_InSelectColumn_...` |
| W1 scan `_having` | QueryPreparer.cs:1887 | 4/4 | sqlite `..._InHaving_...`, `..._InNestedReferencedHaving_...` |
| W1 scan `_joins` guard / loop / JoinCondition test | QueryPreparer.cs:1890/1892/1894 | 2/2, 2/2, 4/4 | sqlite `..._InJoinOn_...` |
| W1 scan `_referencedQueries` guard / loop | QueryPreparer.cs:1901/1903 | 4/4, 2/2 | nested/subquery prepares |
| W2 nullable rejection (PartitionTuple) | InValues.cs:260 | 2/2 | core `PartitionTuple_NullableValueTupleElement_ShouldThrowNotSupported` |
| W2 nullable rejection (TranslateTupleInValues) | InValuesTranslator.cs:140 | 2/2 | sqlite `TupleContains_NullableValueTupleElement_ShouldThrowNotSupported` |

Uncovered / partially covered — named:

1. `ScanUnkeyedTupleInValues` visited-cycle guard `if (!visited.Add(cmd)) return false;`
   (QueryPreparer.cs:1881; return L1882 hits=0) — condition 1/2. No D193 test builds a command graph that
   revisits a command, so the cycle short-circuit is never taken. Defensive; no functional gap.
2. `ScanUnkeyedTupleInValues` recursive-descent positive side
   `if (ScanUnkeyedTupleInValues(referenced[i], visited)) return true;` (QueryPreparer.cs:1905; return L1906
   hits=0) — condition 1/2. No test places an unkeyed tuple **only** inside a nested referenced query's
   unkeyed clause; the W1 `..._InNestedReferencedHaving_...` test is satisfied by the owning command's SELECT
   projection scan (L1884), so recursion's true path is unexercised. The branch remains a correct defensive
   safety net (a false negative there would be a wrong-result risk, but the projection scan currently catches
   the tested shapes). No D193 regression; recommend a dedicated test if the branch is to be closed.
3. Consequence of W4 (r=1 line, now unreachable): `InValuesTranslator` render-time SQL Server guard
   `visitor.Dialect.Tuple is not { }` (L135; throw L136 hits=0, 1/2) — the preflight now fires first.
   Pre-existing defensive throws L143 `!IsTupleLike` (L144–145 hits=0) and L159 `partition.Tuple is not {}`
   (L160 hits=0) also stay uncovered.

Stryker is absent; the audit is manual against the cobertura line/branch data.

## D193 r=2 branch-close — W1 scan recursion + visited guard (2026-10-06T15:49Z)

Progress log:

- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | W1 scan-recursion test added: sqlite `TupleContains_InReferencedQueryHavingOnly_ShouldFlagOuterAndRebuild` — the tuple list sits only in a captured inner query's HAVING referenced by the outer projection, so the outer's own `_exp`/`_having`/`_joins` probes miss it and only the recursive `_referencedQueries` descent can flag it; `outer.HasUnkeyedTupleInValues` asserted true (IVT), two different inner shapes 4 vs 1 params, `ReferenceEquals(first,second)` false | tests/nextorm.sqlite.tests/D193TupleInPlanCacheTests.cs
- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | visited-cycle guard exercised: sqlite `TupleContains_RepeatedReferencedQuery_ShouldNotRevisit` — the same tuple-free referenced command is used twice in the outer projection, so the second descent takes `!visited.Add(cmd)` (L1881 true / L1882); outer stays `HasUnkeyedTupleInValues == false` and executes 3 rows | tests/nextorm.sqlite.tests/D193TupleInPlanCacheTests.cs
- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | build argv `dotnet build tests/nextorm.sqlite.tests -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-r2b/build-sqlite-final.log
- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | focused argv `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~D193"` exit 0, total 19 / succeeded 19 / failed 0 / skipped 0 (was 17 before the two tests) | /tmp/D193-r2b/test-sqlite-d193-final.log
- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | coverage argv `dotnet tool run dotnet-coverage collect "dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~D193" --settings coverage.settings.xml --output-format cobertura --output /tmp/D193-r2b/coverage-d193-final.xml` exit 0; target branches now 100%: QueryPreparer.cs L1881 2/2 (L1882 hits=1, was 1/2 hits=0), L1905 2/2 (L1906 hits=1, was 1/2 hits=0) | /tmp/D193-r2b/coverage-d193-final.xml
- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | R5 docs sync (EN+RU, CRLF): SQL Server rejection stated for **every** tuple form (null collection / null reference-tuple entry / arity≥8 / nested) with the pinned message, non-SQL-Server null collection `ArgumentNullException` preserved, nullable tuple element (`List<(int,int)?>`) `NotSupportedException` documented; TypeFacts.cs `TupleArity` XML doc corrected 1..8 -> supported 1..7; no public doc links specs | docs[/ru]/scalar-functions/06-arrays.md, docs[/ru]/advanced/limitations.md, docs[/ru]/providers/overview.md, src/nextorm.core/Visitors/TypeFacts.cs
- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | roslyn check: `members NextORM.Core.TypeFacts` shows no rename (all internal; only additive internal members in the diff; `ITupleRenderer.RenderInValues` additive default) -> no public API/doc rename needed | roslyn tool
- 2026-10-06T15:49Z | DO | r=2 | n=1/3 | no new defect candidate; both tests pass against the existing implementation; defect history unchanged | this file

### r=2 W1 scan branch coverage delta

| Branch site | r=2 full-sweep (coverage-r2.xml) | focused D193 (coverage-d193-final.xml) |
|---|---|---|
| `ScanUnkeyedTupleInValues` visited guard L1881 / return L1882 | 50% (1/2), L1882 hits=0 | 100% (2/2), L1882 hits=1 |
| `ScanUnkeyedTupleInValues` recursive-descent L1905 / return L1906 | 50% (1/2), L1906 hits=0 | 100% (2/2), L1906 hits=1 |

Both branches were previously named uncovered in the r=2 manual audit (items 1 and 2); both are now
covered by a dedicated positive test, so the W1 scan result is no longer defensively unexercised.

## D193 R7 perf re-acceptance after W1 scan/planner changes — r=2 (2026-10-06T15:52Z)

Progress log:

- 2026-10-06T15:52Z | DO | r=2 | n=1/3 | R7 acceptance argv `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, 7 cases, 0 failures; external shell wall `54.48 s`, BDN `Global total time 41.55 s` (limit <=4 min) | /tmp/D193-perf-r2/acceptance.log
- 2026-10-06T15:52Z | DO | r=2 | n=1/3 | R7 cached-vs-prepared `Cached_ToList / Prepared_ToList` = 1895.8 us / 949.7 us = **2.00** vs documented baseline **1.87** (+6.7%); trigger `1.87 x 1.20 = 2.244` -> **below** the 20% investigation threshold, no rerun required; allocated ratio 562.88 KB / 76.14 KB = **7.39** vs baseline **7.42** (-0.4%, unchanged) | /tmp/D193-perf-r2/acceptance.log
- 2026-10-06T15:52Z | DO | r=2 | n=1/3 | R7 scalar IN/Contains benchmark (shared membership path) argv `... --filter '*SqliteBenchmarkFeaturePlanCache*'` exit 0, 5 cases, 0 failures; wall `42.07 s`, BDN `39.56 s`; no tuple-valued benchmark exists -> tuple expansion recorded as one-time-per-preparation, not per row | /tmp/D193-perf-r2/in-bench.log
- 2026-10-06T15:52Z | DO | r=2 | n=1/3 | R7 BDN artifacts restored `git checkout -- BenchmarkDotNet.Artifacts` exit 0; `git status --porcelain -- BenchmarkDotNet.Artifacts` empty and no `BenchmarkDotNet|Artifacts` entry in the full status | this file, git status

### Acceptance run (7 cases, 0 failures, exit 0; external wall 54.48 s, BDN 41.55 s)

| # | Case | Mean | Allocated | Delta Mean vs baseline |
|---|------|------|-----------|------------------------|
| 1 | `Nextorm_Count` | 2.437 ms | 353.91 KB | -16.4% (high-variance row, not an assertion) |
| 2 | `Nextorm_GroupByCount` | 66.43 ms | 50.07 MB | +9.0% (high-variance row) |
| 3 | `Nextorm_Cached` | 1.924 ms | 567.26 KB | +8.6% (high-variance row) |
| 4 | `Prepared_ToList` | 949.7 us | 76.14 KB | +2.8% |
| 5 | `Cached_ToList` | 1,895.8 us | 562.88 KB | +9.7% |
| 6 | `Cached_PlanOnly_Param` | 578.3 us | 486.73 KB | +11.6% (no-DB probe, not comparable) |
| 7 | `Nextorm_Cached_ToListAsync` | 2.039 ms | 587.38 KB | -4.6% |

Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **2.00** vs baseline **1.87**
(+6.7%) — below the **2.244** (+20%) trigger, so no rerun was required. The allocated ratio is **7.39**
(baseline **7.42**), unchanged. Absolute means are near baseline (the prepared arm +2.8%), so this run is
quiet. **Regression verdict: none.**

### Tuple-IN expansion is one-time per preparation

No existing benchmark exercises tuple-valued `IN`/`Contains` (`rg -n --glob '*.cs' 'InValues|Contains|SqliteBenchmarkWhere' benchmarks` finds only scalar `@in`/`Contains` arms and `SqliteBenchmarkWhere`), and per the brief no new benchmark was authored. The tuple expansion runs once per command preparation, not per row:

- `ScanUnkeyedTupleInValues` is called once per `PrepareCommand` at
  `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:99`
  (`cmd.HasUnkeyedTupleInValues = ScanUnkeyedTupleInValues(cmd);`); the recursive overload is at
  `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:1873`/`:1879`. Its cost is a single walk of the
  command's projection, HAVING, JOIN conditions and referenced queries (guarded by a `HashSet` visited
  set) — constant per prepare, no per-row or per-execution work.
- The tuple RHS expansion itself (`InValuesTranslator.TranslateTupleInValues`, `InValues.PartitionTuple`)
  also runs once while the SQL is built; per execution only parameter rebinding is paid, and the W1 fix
  only suppresses the call-local plan caching for unkeyed shapes (planner gate
  `src/nextorm.core/DataContext/QueryPlanner.cs:573`), never adds per-row work.

Scalar IN/Contains benchmark — `SqliteBenchmarkFeaturePlanCache` (5 cases, 0 failures, exit 0), warm
plan-only probes, baseline `Warm_PlanOnly_Distinct`:

| Case | Mean | Ratio | Allocated | Alloc ratio |
|------|------|-------|-----------|-------------|
| `Warm_PlanOnly_Distinct` (baseline) | 306.5 us | 1.00 | 387.5 KB | 1.00 |
| `Warm_PlanOnly_In_AtIn_Inline` | 756.5 us | 2.47 | 602.35 KB | 1.55 |
| `Warm_PlanOnly_In_AtIn_Captured` | 798.3 us | 2.61 | 718.75 KB | 1.85 |
| `Warm_PlanOnly_In_ListContains_Inline` | 859.5 us | 2.81 | 615.63 KB | 1.59 |
| `Warm_PlanOnly_In_ListContains_Captured` | 932.0 us | 3.04 | 738.29 KB | 1.91 |

These are scalar IN-list probes, not tuple-valued, so they are protection evidence for the shared
membership/plan-cache path, not a before/after tuple measurement.

R7 verdict (r=2): **no regression**. No blocker.


## DO r=2 (F1/F7 continuation) — widened unkeyed scan + SQL Server clause rejection coverage — 2026-10-06T16:12Z

Progress log:

- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | F1 fix: `ScanUnkeyedTupleInValues` widened to every unkeyed tuple-bearing clause — SELECT projection, HAVING, GROUP BY (`_groupExp`), JOIN conditions plus derived join sources (`joins[i].From`), ORDER BY (`_sorting[].SortExpression`/`PreparedExpression`), ClickHouse ARRAY JOIN (`_preparedArrayJoin`/`_arrayJoins`), LIMIT BY / DISTINCT ON / extreme-row selectors, named windows (PARTITION BY / ORDER BY), correlated outer references, FROM derived table / PIVOT inner / TVF call, UNION operand (`_union`), CTE bodies (`_ctes[].Query`) and recursively referenced subqueries. WHERE (`_condition`) / PREWHERE (`_preWhere`) stay excluded (shape-hashed) so tuple-IN in WHERE remains cacheable. Recursion keeps the existing `HashSet<QueryCommand>(ReferenceEqualityComparer.Instance)` visited set | src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:1873-2053
- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | F1 red->green regression: with the widened scan temporarily reverted (`git diff` saved, `git checkout --`, re-applied), sqlite `TupleContains_InUnionBranchHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult` and `TupleContains_InDerivedTableHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult` fail with exit 2, total 2 / failed 2, `Expected firstCmd.HasUnkeyedTupleInValues to be True ... but found False`; with the fix both pass | /tmp/D193-evidence/test-f1-red.log, /tmp/D193-evidence/f1-scan.patch
- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | F1 tests (runtime stale-plan reproduction): a captured tuple list whose shape changes between two calls returns the current result for both a UNION branch HAVING ({1,3} then {2,3}) and a derived-table HAVING ({1} then {2}), with `HasUnkeyedTupleInValues == true` and no cached-plan reuse (`ReferenceEquals(first, second) == false`) | tests/nextorm.sqlite.tests/D193TupleInPlanCacheTests.cs
- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | F7 tests: tuple `Contains` in HAVING / SELECT / JOIN-ON on SQL Server, each with an EMPTY collection, throws exclusively `SQL Server does not support tuple IN/Contains translation.` The render-time guard (`InValuesTranslator.cs:135`) is the first statement of `TranslateTupleInValues`, so it fires before collection evaluation and before the empty-list fold; no `EnsureProviderSupportsTupleInValues` extension was required. `Prepare` only (never execute), so no SQL is submitted | tests/nextorm.sqlserver.tests/D193TupleInSqlGenerationTests.cs
- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-f1.log
- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | focused argv `dotnet test tests/nextorm.{core,sqlite,sqlserver}.tests -c Debug --no-build --filter "FullyQualifiedName~D193|FullyQualifiedName~TupleIn"` exit 0 each; core 22 / failed 0 / skipped 0, sqlite 21 / failed 0 / skipped 0, sqlserver 10 / failed 0 / skipped 0 (sqlserver +3 F7, sqlite +2 F1 over the prior 19) | /tmp/D193-evidence/test-{core,sqlite,sqlserver}-focused-f1.log
- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | full-suite regression sweep (affected projects) exit 0 each: core 1643/0/0, sqlite 1099 total / 1098 succeeded / 0 failed / 1 skipped (pre-existing RowId LOB gate), sqlserver 707/0/0 (total/succeeded/failed/skipped); the widened scan only suppresses call-local caching (false positives), so no SQL-shape regression | /tmp/D193-evidence/test-full-{core,sqlite,sqlserver}-f1.log
- 2026-10-06T16:12Z | DO | r=2 | n=1/3 | CRLF verified on all three touched files (every line CRLF: QueryCommand.QueryPreparer.cs 2330/2330, D193TupleInSqlGenerationTests.cs 184/184, D193TupleInPlanCacheTests.cs 439/439) | this file

### Defect history additions (F1/F7)

| Defect key | Title | First seen | Revision/attempt | Fix applied (r/n) | Evidence | Status |
|---|---|---|---|---|---|---|
| D193-F1 | Cache scan coverage: `ScanUnkeyedTupleInValues` scanned only SELECT projection, HAVING, JOIN conditions and referenced subqueries, so a tuple `Contains` in a UNION branch, CTE body, derived table (`_from.SubQuery`), ORDER BY, GROUP BY or named window was a false negative and could reuse a stale plan | CHECK r=2 | r=2/n=1 | Fixed r=2/n=1 — scan widened to every unkeyed tuple-bearing clause; WHERE/PREWHERE remain shape-hashed | /tmp/D193-evidence/test-f1-red.log (red, exit 2), /tmp/D193-evidence/test-sqlite-focused-f1.log (green, exit 0) | **fixed-verified** |
| D193-F7 | SQL Server rejection coverage: a tuple `Contains` in HAVING/SELECT/JOIN-ON reached only the render-time guard and had no test proving it fires before collection evaluation/empty folding | CHECK r=2 | r=2/n=1 | Fixed r=2/n=1 — three empty-collection SQL Server tests prove the pinned message is thrown before evaluation/folding; no production change required | /tmp/D193-evidence/test-sqlserver-focused-f1.log | **verified** |

### F1/F7 evidence summary

| Check | argv | exit | result | log |
|---|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning / 0 Error | build-f1.log |
| focused core | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~D193|FullyQualifiedName~TupleIn"` | 0 | 22 / 0 / 0 | test-core-focused-f1.log |
| focused sqlite | same selector | 0 | 21 / 0 / 0 | test-sqlite-focused-f1.log |
| focused sqlserver | same selector | 0 | 10 / 0 / 0 | test-sqlserver-focused-f1.log |
| full core | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | 0 | 1643 / 0 / 0 | test-full-core-f1.log |
| full sqlite | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` | 0 | 1099 / 1098 / 0 / 1 | test-full-sqlite-f1.log |
| full sqlserver | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | 0 | 707 / 0 / 0 | test-full-sqlserver-f1.log |
| F1 red (fix reverted) | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~InUnionBranchHaving|FullyQualifiedName~InDerivedTableHaving"` | 2 | 2 / 2 failed / 0 passed | test-f1-red.log |

### Clauses now scanned by `ScanUnkeyedTupleInValues` (unkeyed) and their F1/F7 evidence

| Clause | Field(s) scanned | Evidence |
|---|---|---|
| SELECT projection | `_exp` | sqlite `TupleContains_InSelectColumn_...`; sqlserver F7 SELECT test |
| HAVING | `_having` | sqlite `TupleContains_InHaving_...`; sqlserver F7 HAVING test |
| JOIN ON / derived join source | `joins[i].JoinCondition`, `joins[i].From` | sqlite `TupleContains_InJoinOn_...`; sqlserver F7 JOIN test |
| GROUP BY | `_groupExp` | (covered by the scan; no dedicated test) |
| ORDER BY | `_sorting[].SortExpression` / `PreparedExpression` | widened scan |
| ARRAY JOIN / LIMIT BY / DISTINCT ON / extreme row | `_preparedArrayJoin`/`_arrayJoins`, `LimitBy`, `DistinctOn`, `ExtremeRow` | widened scan |
| Named windows | `_windows[].PartitionBy` / `OrderBy` | widened scan |
| Outer references | `_outerRefs` | widened scan |
| Derived table / PIVOT / TVF | `_from.SubQuery` / `Pivot.Inner` / `TableFunction.Call` | sqlite `TupleContains_InDerivedTableHaving_...` |
| UNION / set operation | `_union` | sqlite `TupleContains_InUnionBranchHaving_...` |
| CTE bodies | `_ctes[].Query` | widened scan |
| Nested referenced subqueries | `_referencedQueries` | sqlite `TupleContains_InReferencedQueryHavingOnly_...` |
| WHERE / PREWHERE (shape-keyed, excluded) | `_condition` / `_preWhere` | sqlite `TupleContains_InWhere_SecondIdenticalCall_ShouldBeCacheHit` (cache hit preserved) |

F1 verdict: **fixed-verified** (false-negative scan closed, regression test red->green). F7 verdict: **verified** (render guard
proven early for HAVING/SELECT/JOIN-ON; no production change). No blocker.

## DO r=2 (F10/F12/F14 fixes) — 2026-10-06T16:19Z

Progress log:

- 2026-10-06T16:19Z | DO | r=2 | n=1/3 | F10: HAVING/JOIN/SELECT/nested rebuild tests rewritten to capture the SAME field (`this._tuples` / `this._joinTuples`) instead of per-test locals (`twoRows`/`oneNullRow`); the old distinct closure-member names already changed the plan key, so the assertions could pass without `HasUnkeyedTupleInValues`. Flag-disabled red run (QueryPlanner gate temporarily `false &&`, restored from backup): sqlite D193 exit 2, 22 total / 6 failed / 16 passed — the four rewritten tests fail (HAVING, JOIN, SELECT, nested) plus the already-isolated UNION/derived tests | /tmp/D193-evidence/test-f10-red.log
- 2026-10-06T16:19Z | DO | r=2 | n=1/3 | F12: added `WhereTuple_ShapeChangeThroughCache_ShouldRebindAndKeepScalarCacheable` to `D193TupleInPlanCacheTests.cs` — real cache path (`Prepare` = `storeInCache:true`), same `_tuples` field, WHERE shape {1,3}→{1}: param count 4→2, SQL `in (VALUES ($p0, $p1))`, `ReferenceEquals(first,second) false`, results [1,3]→[1]; unchanged shape hits (`ReferenceEquals(second,third)` true); scalar path stays a hit. Replaces the `D193TupleInSqlGenerationTests.cs:91-109` blind spot (that test builds with `storeInCache:false`). Isolation is by construction (shape fold `InValuesShapeHash` is part of the plan key), not by the unkeyed flag, so the F12 red is orthogonal to the flag-disabled red above | tests/nextorm.sqlite.tests/D193TupleInPlanCacheTests.cs
- 2026-10-06T16:19Z | DO | r=2 | n=1/3 | F14: EN/RU `docs/providers/overview.md:113` in-memory cell corrected from `throws NotSupportedException` to "evaluated in process … no rejection"; EN/RU `docs/scalar-functions/06-arrays.md:158` row-constructor rejection sentence scoped to the **row-constructor/row-comparison** surface and noted in-memory evaluates tuple `IN`/`Contains` in process, plus an explicit `In-memory` row added to the tuple-IN provider table; no public doc links `docs/specs/**` | docs diff
- 2026-10-06T16:19Z | DO | r=2 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-f10.log
- 2026-10-06T16:19Z | DO | r=2 | n=1/3 | focused argv `dotnet test tests/nextorm.{core,sqlite,sqlserver}.tests -c Debug --no-build --filter "FullyQualifiedName~D193\|FullyQualifiedName~TupleIn"` exit 0 each; core 22 / failed 0 / skipped 0, sqlite 22 / failed 0 / skipped 0, sqlserver 10 / failed 0 / skipped 0 | /tmp/D193-evidence/test-{core,sqlite,sqlserver}-focused-f10.log
- 2026-10-06T16:19Z | DO | r=2 | n=1/3 | CRLF verified on every touched file (D193TupleInPlanCacheTests.cs 513/513; overview EN/RU 228/228; 06-arrays EN 270/270, RU 272/272; status file); no commit/push | this file

### Defect history additions (F10/F12/F14, r=2/n=1)

| Defect key | Title | First seen | Revision/attempt | Fix applied (r/n) | Evidence | Status |
|---|---|---|---|---|---|---|
| D193-F10 | Test validity: the HAVING/JOIN/SELECT/nested rebuild tests captured **different** closure member names for the two shapes, so `ExpressionPlanEqualityComparer` already produced different plan keys and the tests passed even without `HasUnkeyedTupleInValues` | CHECK r=2 | r=2/n=1 | Fixed r=2/n=1 — all four tests now capture a single test-class field (`_tuples`/`_joinTuples`) reassigned between calls; flag-disabled run fails all four | /tmp/D193-evidence/test-f10-red.log (exit 2, 6 failed) | **fixed-verified** |
| D193-F12 | WHERE shape-change never exercised the real plan cache: the sequential-shape SQL test builds with `storeInCache:false` | CHECK r=2 | r=2/n=1 | Fixed r=2/n=1 — added `WhereTuple_ShapeChangeThroughCache_ShouldRebindAndKeepScalarCacheable` (same captured field, `storeInCache:true`, shape change rebinds SQL/params/results, unchanged shape + scalar path hit) | /tmp/D193-evidence/test-sqlite-focused-f10.log | **verified** |
| D193-F14 | Docs: EN/RU `providers/overview.md:113` claimed the in-memory provider throws for tuple `IN`, but in-memory evaluates/matches (core `D193TupleInTests`); the arrays row-constructor sentence could be read as covering tuple `IN` | CHECK r=2 | r=2/n=1 | Fixed r=2/n=1 — EN/RU in-memory cell states in-process evaluation with no rejection; EN/RU 06-arrays scope the rejection to the row-constructor/row-comparison surface and add an `In-memory` tuple-IN row | docs diff | **fixed-verified** |

### F10/F12/F14 evidence summary

| Check | argv | exit | result | log |
|---|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | 0 W / 0 E | build-f10.log |
| focused core | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~D193\|FullyQualifiedName~TupleIn"` | 0 | 22 / 0 / 0 | test-core-focused-f10.log |
| focused sqlite | same selector | 0 | 22 / 0 / 0 | test-sqlite-focused-f10.log |
| focused sqlserver | same selector | 0 | 10 / 0 / 0 | test-sqlserver-focused-f10.log |
| F10 red (flag ignored) | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~D193"` | 2 | 22 total / 6 failed / 16 passed (HAVING, JOIN, SELECT, nested, UNION, derived) | test-f10-red.log |

F10/F12/F14 verdicts: **fixed-verified** (F10 red→green on the flag; F12 covers the real cache path and is isolated by the shape fold; F14 EN+RU corrected, no specs links). No blocker.

## Final comprehensive validation sweep after F1/F7/F10/F12/F14 — 2026-10-06T16:26Z (DO)

Progress log:

- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | temp-patch audit: `git diff src/nextorm.core/DataContext/QueryPlanner.cs` shows the intended `if (queryCommand.HasUnkeyedTupleInValues) storeInCache = false;` (L573), no `false &&`; `rg 'false\s*&&' src/nextorm.core/DataContext/QueryPlanner.cs` exit 1 (no match); `rg 'false\s*&&' --glob '*.cs' src` also no match; no `.bak/.orig/.patch` in `git status` | this file, /tmp/D193-evidence/build-final-sweep.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-final-sweep.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | FULL(core) `dotnet test tests/nextorm.core.tests -c Debug --no-build` exit 0, total 1643 / passed 1643 / failed 0 / skipped 0 | /tmp/D193-evidence/test-core.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | FULL(sqlite) exit 0, total 1100 / passed 1099 / failed 0 / skipped 1 (pre-existing `NEXTORM_LOB_SQLITE_PROBE` gate) | /tmp/D193-evidence/test-sqlite.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | FULL(postgres) exit 0, total 775 / passed 775 / failed 0 / skipped 0 | /tmp/D193-evidence/test-postgres.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | FULL(sqlserver) exit 0, total 707 / passed 707 / failed 0 / skipped 0 | /tmp/D193-evidence/test-sqlserver.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | FULL(mysql) exit 0, total 289 / passed 289 / failed 0 / skipped 0 | /tmp/D193-evidence/test-mysql.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | FULL(mariadb) exit 0, total 196 / passed 196 / failed 0 / skipped 0 | /tmp/D193-evidence/test-mariadb.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | FULL(clickhouse) exit 0, total 550 / passed 550 / failed 0 / skipped 0 | /tmp/D193-evidence/test-clickhouse.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | full container integration argv `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --no-build` exit 0, total 3295 / passed 3098 / failed 0 / skipped 197; no provider-unavailable skip | /tmp/D193-evidence/integration-final-sweep.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | focused D193 boundary `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~TupleIn"` exit 0, total 34 / passed 26 / failed 0 / skipped 8 (all six providers executed) | /tmp/D193-evidence/integration-focused-tuplein.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | CI-form coverage collect `dotnet-coverage collect -s coverage.settings.xml -f cobertura -o /tmp/D193-evidence/coverage-final.xml "dotnet test nextorm.slnx -c Debug --no-build --verbosity normal"` exit 0, solution 8743 total / 6131 succeeded / 0 failed / 2612 skipped (container providers skipped, no DOCKER_HOST, as in CI) | /tmp/D193-evidence/coverage-collect-final.log
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | reportgenerator TextSummary exit 0: Assemblies 4, **Line 87.2% >= 85**, **Branch 79.1% (24665/31158) >= 75**; both CI thresholds met | /tmp/D193-evidence/coverage-report-final/Summary.txt
- 2026-10-06T16:26Z | DO | r=2 | n=1/3 | manual branch audit of the NEW F1 scan (`ScanUnkeyedTupleInValues`/`ScanUnkeyedTupleInFrom`, QueryPreparer.cs:1877-2051); fully covered W1 branches and named uncovered widened-clause directions recorded below; Stryker absent | /tmp/D193-evidence/coverage-final.xml, covparse.py

### Command evidence (final sweep)

| Suite | argv | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | - | - | 0 W / 0 E | - | build-final-sweep.log |
| core | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | 0 | 1643 | 1643 | 0 | 0 | test-core.log |
| sqlite | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` | 0 | 1100 | 1099 | 0 | 1 | test-sqlite.log |
| postgres | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build` | 0 | 775 | 775 | 0 | 0 | test-postgres.log |
| sqlserver | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | 0 | 707 | 707 | 0 | 0 | test-sqlserver.log |
| mysql | `dotnet test tests/nextorm.mysql.tests -c Debug --no-build` | 0 | 289 | 289 | 0 | 0 | test-mysql.log |
| mariadb | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build` | 0 | 196 | 196 | 0 | 0 | test-mariadb.log |
| clickhouse | `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build` | 0 | 550 | 550 | 0 | 0 | test-clickhouse.log |
| integration | `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --no-build` | 0 | 3295 | 3098 | 0 | 197 | integration-final-sweep.log |
| focused D193 | `DOCKER_HOST=... dotnet test ... --filter "FullyQualifiedName~TupleIn"` | 0 | 34 | 26 | 0 | 8 | integration-focused-tuplein.log |

The single per-provider skip is the established `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`
capability gate (`NEXTORM_LOB_SQLITE_PROBE=1` to run), unrelated to D193.

### Integration per-provider execution (final sweep)

All six providers executed; `rg -i "not available"` over the run log is empty (a missing Docker host would
report every container test as skipped with an availability reason). Skip distribution by class:

| Provider | executed | capability skips this run | evidence |
|---|---|---|---|
| SQLite | yes (in-process) | 40 | integration-final-sweep.log |
| PostgreSQL | yes (container) | 26 | integration-final-sweep.log |
| SQL Server | yes (container) | 48 | integration-final-sweep.log |
| MySQL | yes (container) | 80 | integration-final-sweep.log |
| MariaDB | yes (container, `MariaDbTupleInExecutionTests`) | 0 | integration-final-sweep.log |
| ClickHouse | yes (container) | 0 | integration-final-sweep.log |
| LOB probe/perf (standalone) | yes | 3 | integration-final-sweep.log |

Totals: **3295 total / 3098 passed / 0 failed / 197 skipped** — identical to the r=2 sweep whose
`-result-xml` breakdown was ClickHouse 200/0 skip, MariaDB 55/0, SQL Server 724 (48 skip), MySQL 664 (80),
PostgreSQL 826 (26), SQLite 704 (41); all 197 skips are per-capability gates.

### Focused D193 boundary — per provider (`FullyQualifiedName~TupleIn`, total 34 / 26 / 0 / 8)

| Provider | passed | skipped | skip reason (established) |
|---|---|---|---|
| SQLite | 5 | 1 | `OnProviderWithoutRowConstructor` (provider supports tuple IN) |
| PostgreSQL | 5 | 1 | idem |
| SQL Server | 1 | 5 | no row-value constructor (positive executions gated) |
| MySQL | 5 | 1 | `OnProviderWithoutRowConstructor` |
| MariaDB | 5 | 0 | — |
| ClickHouse | 5 | 0 | — |

### Coverage (CI form) — four configured assemblies and changed files

Aggregate (`Summary.txt`): Assemblies **4**, **Line 87.2%**, **Branch 79.1%** — both >= 85/75.
Assembly line coverage: nextorm.core 87.4%, nextorm.sqlite 90.5%, nextorm.postgres 79.6%, nextorm.sqlserver 80.9%.

| Changed file | Line | Branch |
|---|---|---|
| nextorm.core/Query/QueryCommand.cs | 98.1% (503/513) | 75.4% (178/236) |
| nextorm.core/Query/QueryCommand.Clone.cs | 96.3% (312/324) | 100.0% (48/48) |
| nextorm.core/Query/QueryCommand.QueryPreparer.cs | 93.7% (2632/2810) | 86.7% (1980/2284) |
| nextorm.core/DataContext/QueryPlanner.cs | 96.6% (980/1014) | 89.1% (424/476) |
| nextorm.core/Query/InValues.cs | 90.3% (448/496) | 80.2% (260/324) |
| nextorm.core/Visitors/InValuesTranslator.cs | 98.3% (342/348) | 95.6% (218/228) |
| nextorm.core/Visitors/TupleSqlTranslator.cs | 91.7% (198/216) | 88.5% (170/192) |
| nextorm.core/Visitors/TypeFacts.cs | 100.0% (156/156) | 91.8% (336/366) |
| nextorm.core/DataContext/Dialect/DialectCapabilities.cs | 100.0% (118/118) | 60.0% (12/20) |
| nextorm.sqlite/SqliteDialect.cs | 87.1% (392/450) | 64.0% (456/712) |

`DialectCapabilities.cs` / `SqliteDialect.cs` branch gaps are dominated by pre-existing unrelated branches
(D193 added the branchless `ITupleRenderer.RenderInValues` default and `SqliteTupleRenderer.RenderInValues`).

### Manual branch audit — NEW F1 scan lines (`QueryPreparer.cs:1877-2051`, Stryker absent)

Covered (cobertura `condition-coverage`):

| New branch | Site | cond | D193 test |
|---|---|---|---|
| visited-cycle guard `if (!visited.Add(cmd)) return false;` | L1885 / L1886 | 2/2 | sqlite `TupleContains_RepeatedReferencedQuery_ShouldNotRevisit` |
| SELECT projection `_exp` | L1888 | 4/4 | `TupleContains_InSelectColumn_...`; sqlserver F7 SELECT |
| HAVING `_having` | L1891 | 4/4 | `TupleContains_InHaving_...`, `..._InNestedReferencedHaving_...` |
| JOIN guard / loop | L1897 / L1899 | 2/2, 2/2 | `TupleContains_InJoinOn_...` |
| JOIN condition test | L1901 | 4/4 | `TupleContains_InJoinOn_...` |
| FROM derived table `from.SubQuery` | L2030 / L2033 | 2/2, 4/4 | `TupleContains_InDerivedTableHaving_...` |
| UNION operand `_union` | L1995 | 4/4 | `TupleContains_InUnionBranchHaving_...` |
| referenced subqueries guard/loop/descent | L2010 / L2012 / L2014 | 4/4, 2/2, 2/2 | `TupleContains_InReferencedQueryHavingOnly_...`, `..._InNestedReferencedHaving_...` |
| planner bypass `if (HasUnkeyedTupleInValues)` | QueryPlanner.cs:573 | 2/2 | sqlite plan-cache tests (true) + `..._InWhere_SecondIdenticalCall_ShouldBeCacheHit` (false) |

Named uncovered / one-sided widened-clause branches (positive "tuple present in clause" direction never
exercised; only the false side is taken by the non-D193 suite). No D193 test covers them:

| New branch | Site | cond | missing direction |
|---|---|---|---|
| GROUP BY `_groupExp` tuple | L1894 | 3/4 | `Contains(grouping)` true |
| ORDER BY `SortExpression` tuple | L1914 | 3/4 | `Contains(sortExpression)` true |
| ORDER BY `PreparedExpression` tuple | L1917 | 3/4 | `Contains(preparedExpression)` true |
| clickhouse prepared ARRAY JOIN tuple | L1927 | 1/2 | `Contains(preparedArrayJoin[i])` true |
| clickhouse `_arrayJoins` fallback `else if` | L1931 | 1/2 | `_arrayJoins is {}` true |
| clickhouse `_arrayJoins` loop | L1933 | 0/2 | never entered |
| clickhouse `_arrayJoins[i]` tuple | L1935 | 0/2 | `Contains(arrayJoins[i])` true |
| LIMIT BY tuple | L1940 | 5/6 | `Contains(limitBy)` true |
| DISTINCT ON tuple | L1943 | 5/6 | `Contains(distinctOn)` true |
| extreme-row `ValueSelector` tuple | L1948 | 1/2 | `Contains(ValueSelector)` true |
| extreme-row `GroupBy` tuple | L1951 | 3/4 | `Contains(GroupBy)` true |
| extreme-row `Projection` tuple | L1954 | 3/4 | `Contains(Projection)` true |
| named-window PARTITION BY tuple | L1967 | 1/2 | `Contains(PartitionBy[p])` true |
| named-window ORDER BY tuple | L1973 | 1/2 | `Contains(OrderBy[o].Expression)` true |
| outer reference tuple | L1984 | 1/2 | `Contains(outerRefs[i])` true |
| CTE body tuple | L2003 | 1/2 | `Scan(ctes[i].Query)` true |
| join derived source tuple | L1904 | 1/2 | `ScanUnkeyedTupleInFrom(joins[i].From)` true |
| PIVOT `AggregateColumn` tuple | L2038 | 3/4 | `Contains(aggregateColumn)` true |
| PIVOT `ForColumn` tuple | L2041 | 3/4 | `Contains(forColumn)` true |
| PIVOT inner source tuple | L2044 | 1/2 | `ScanUnkeyedTupleInFrom(pivot.Inner)` true |
| TVF call tuple | L2048 | 3/4 | `Contains(tableFunction.Call)` true |

Audit note: these are F1 **defensive wide-scan** clauses; the untested direction is the one that *sets*
`HasUnkeyedTupleInValues` for a tuple list in that clause. A false negative there would reuse a stale plan
(wrong result), so the missing positive tests are an evidence gap, not a correctness regression in the
tested shapes. The fully uncovered pair L1933/L1935 (`_arrayJoins` fallback) is likely unreachable when
`_preparedArrayJoin` is populated; recommend either a dedicated test per clause or a note that the wide
scan is belt-and-braces. Recorded for the orchestrator; no blocker for the validated D193 scope.

### CRLF / hygiene

- Status file: 790 -> 948 lines; `awk '!/\r$/' docs/specs/status/rc1-tail-193-tuple-in-1.md` -> **0 lines** (100% CRLF); no prior section content rewritten (append-only; final sweep section starts at the old EOF).
- All touched/relevant files CRLF; no commit/push/merge performed.

### Defect history additions (final sweep)

| Defect key | Title | First seen | Revision/attempt | Fix applied (r/n) | Evidence | Status |
|---|---|---|---|---|---|---|
| D193-F1 | widened unkeyed scan | CHECK r=2 | r=2/n=1 | fixed-verified (red->green) | test-f1-red.log, test-sqlite-focused-f1.log | **fixed-verified** |
| D193-F7 | SQL Server early rejection | CHECK r=2 | r=2/n=1 | verified | test-sqlserver-focused-f1.log | **verified** |
| D193-F10 | single captured field in rebuild tests | CHECK r=2 | r=2/n=1 | fixed-verified (red->green) | test-f10-red.log | **fixed-verified** |
| D193-F12 | real WHERE cache-path test | CHECK r=2 | r=2/n=1 | verified | test-sqlite-focused-f10.log | **verified** |
| D193-F14 | EN/RU in-memory docs | CHECK r=2 | r=2/n=1 | fixed-verified | docs diff | **fixed-verified** |

Final sweep verdict: build 0W/0E, all 7 per-provider suites exit 0, container integration exit 0 with all
six providers executed and zero failures, focused D193 boundary exit 0, coverage 87.2/79.1 >= 85/75.
No blocker. The only open item is the named uncovered F1 wide-scan positive branches (evidence gap, not a
regression).

## DO r=2 — R4 residual scan gap + R1/R3 breadth + R5 stale pointer — CHECK→DO evidence closure (2026-10-06T16:45Z)

Progress log:

- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | R4 scan-gap closure: `ScanUnkeyedTupleInValues` now also descends into `CteDefinition.Mutation.Source` (`QueryPreparer.cs:2010`) and `ScanUnkeyedTupleInFrom` into `FromExpression.ColumnShape` (`:2047`), `TempTable.Source` (`:2052`), `LinqSource.OuterCommand`/`InnerCommand` + the four selector lambdas (`:2058`), and `XmlNodes.Operand` (`:2080`); `ScanUnkeyedTupleInFrom` recursion reuses the `HashSet<QueryCommand>` visited guard | src/nextorm.core/Query/QueryCommand.QueryPreparer.cs
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | R1 arity breadth: core `Contains_ValueTupleArityOne_ShouldMatchBothRows` (:75), `Contains_ValueTupleAritySeven_ShouldMatchBothRows` (:86), `Contains_ReferenceTupleArityThree_ShouldMatchBothRows` (:104); sqlite `TupleContains_ValueTupleArityOne_ShouldRenderValuesInList` (:170), `TupleContains_ValueTupleAritySeven_ShouldRenderValuesInList` (:185), `TupleContains_ReferenceTupleArityThree_ShouldRenderValuesInList` (:202) | tests/nextorm.{core,sqlite}.tests/D193TupleIn*Tests.cs
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | R3 default/null: core `Contains_DefaultValueTupleEntry_ShouldMatchDefaultsRow` (:119) — `default((int,int?))` is an ordinary `(0,null)` row; sqlite `TupleContains_DefaultValueTupleEntry_ShouldRenderOrdinaryRow` (:217) renders two params; sqlite `TupleContains_NullCollection_ShouldThrowArgumentNullException` (:233) adds the tuple-path null-collection `ArgumentNullException` (was only the direct `PartitionTuple` core test); null-entry `NotSupportedException` already covered by sqlite `TupleContains_NullReferenceTupleEntry_ShouldThrowNotSupported` (:158) and SQL Server pinned-message tests | tests/nextorm.{core,sqlite}.tests/D193TupleIn*Tests.cs
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | R5 stale pointer corrected: W4 call-site row `QueryPreparer.cs:1924, :1957, :175–176` → `:2126, :2159, :175–176` (lines shifted by the scan additions). Plan/criteria untouched | this file
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-gaps.log
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | focused argv `dotnet test tests/nextorm.<P>.tests -c Debug --no-build --filter "FullyQualifiedName~D193\|FullyQualifiedName~TupleIn"` exit 0 each: core 26/0/0, sqlite 27/0/0, sqlserver 10/0/0, postgres 4/0/0, mysql 4/0/0, mariadb 4/0/0, clickhouse 4/0/0 (total/failed/skipped) | /tmp/D193-evidence/test-{core,sqlite,sqlserver,postgres,mysql,mariadb,clickhouse}-focused-gaps.log
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | full affected suites exit 0: core 1647/1647/0/0 (+4), sqlite 1105/1104/0/1 (+5; the one skip is the pre-existing `NEXTORM_LOB_SQLITE_PROBE` gate), sqlserver 707/707/0/0 (unchanged) — no cache-hit regression from the widened scan | /tmp/D193-evidence/test-full-{core,sqlite,sqlserver}-gaps.log
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | CRLF verified on all touched files (QueryCommand.QueryPreparer.cs 2377/2377, D193TupleInTests.cs 309/309, D193TupleInSqlGenerationTests.cs 282/282, status file); no commit/push | this file
- 2026-10-06T16:45Z | DO | r=2 | n=1/3 | no new defect candidate; all new tests pass; defect history unchanged | this file

### R4 — residual FROM/CTE container disposition (scanned vs non-reachable)

| Field | Site | Holds | Disposition |
|---|---|---|---|
| `FromExpression.ColumnShape` | `FromExpression.cs:124` | `QueryCommand` (data-modifying CTE read: the mutation's RETURNING shape command) | **scanned** (`QueryPreparer.cs:2047`) |
| `FromExpression.TempTable.Source` | `TempTableSource.cs:33` | `QueryCommand` (the CTAS source rendered in the same batch) | **scanned** (`:2052`) |
| `LinqSource.OuterCommand` / `.InnerCommand` | `LinqSourceExpression.cs:14-16` | `QueryCommand` | **scanned** (recursion, `:2058`) |
| `LinqSource.{Collection,Result,OuterKey,InnerKey}Selector` | `LinqSourceExpression.cs:28-34` | `LambdaExpression` | **scanned** (`InValues.ContainsTupleInValues`, `:2058`) |
| `XmlNodes.Operand` | `XmlNodesExpression.cs:20` | `Expression` (rendered as the correlated XML column in `MakeXmlNodes`) | **scanned** (`:2080`) |
| `CteDefinition.Mutation.Source` | `CteMutation.cs:31` | `QueryCommand` (INSERT … SELECT source / UPDATE,DELETE predicate / joined source) | **scanned** (`:2010`) |
| `CteDefinition.Mutation.Command` | `CteMutation.cs:22` | `MutationCommand` (not a `QueryCommand`/`Expression`/lambda) | **not scanned — non-reachable**: its only nested `QueryCommand` is exactly `Mutation.Source` (already scanned); its assignment expressions render only inside a data-modifying CTE body, and a command carrying one is never plan-cached (`QueryCommand.HasDataModifyingCte` → `QueryPlanner` clears `storeInCache`), so it cannot reuse a stale plan |

All added probes are conservative: a false positive only forgoes caching, a false negative would be a wrong
result. The recursion keeps the reference-identity `visited` guard, so the new edges cannot loop.

### Evidence ledger — acceptance criteria R1..R7 → artifact

| Criterion | Evidence (test name / `file:line` / log / coverage) |
|---|---|
| **R1** supported CLR tuple collections, arities 1..7, reference `Tuple` >2 | core `Contains_ValueTupleArityOne_ShouldMatchBothRows` (`D193TupleInTests.cs:75`), `Contains_ValueTupleAritySeven_ShouldMatchBothRows` (:86), `Contains_ReferenceTupleArityThree_ShouldMatchBothRows` (:104), `Contains_ValueTupleList_ShouldMatchBothRows` (:42), `Contains_ReferenceTupleList_ShouldMatchRow` (:64), `Contains_ValueTupleList_MismatchedComponent_ShouldReturnNoRows` (:53); sqlite `TupleContains_ValueTupleArityOne_ShouldRenderValuesInList` (`D193TupleInSqlGenerationTests.cs:170`), `...AritySeven...` (:185), `TupleContains_ReferenceTupleArityThree_ShouldRenderValuesInList` (:202). Logs `/tmp/D193-evidence/test-core-focused-gaps.log` (26/0/0, exit 0), `/tmp/D193-evidence/test-sqlite-focused-gaps.log` (27/0/0, exit 0) |
| **R2** provider matrix by SQL assertions + execution; SQL Server rejects (incl. empty) | per-provider focused logs exit 0 — postgres/mysql/mariadb/clickhouse 4/0/0 each (`test-{postgres,mysql,mariadb,clickhouse}-focused-gaps.log`); SQL Server rejection `tests/nextorm.sqlserver.tests/D193TupleInSqlGenerationTests.cs` 10/0/0 (`test-sqlserver-focused-gaps.log`); execution boundary `DOCKER_HOST=… dotnet … integration … --filter "FullyQualifiedName~TupleIn"` 34/26/0/8 all six providers executed (`/tmp/D193-evidence/boundary-d193-r2.log`, `boundary-d193-r2.xml`, `integration-focused-tuplein.log`) |
| **R3** empty/default/null + nullable-component membership deterministic | core `Contains_DefaultValueTupleEntry_ShouldMatchDefaultsRow` (`D193TupleInTests.cs:119`), `Contains_EmptyList_ShouldReturnNoRows` (:131), `Contains_NullableComponent_ShouldMatchNullTupleRow` (:142), `...Negation...` (:153); `PartitionTuple_NullCollection_ShouldThrowArgumentNullException` (:234), `PartitionTuple_NullTupleEntry_ShouldThrowNotSupported` (:272); sqlite `TupleContains_Empty_ShouldRenderAlwaysFalse` (`D193TupleInSqlGenerationTests.cs:52`), `TupleContains_NullableComponent_ShouldGuardAndUseNullArm` (:65), `TupleContains_DefaultValueTupleEntry_ShouldRenderOrdinaryRow` (:217), `TupleContains_NullCollection_ShouldThrowArgumentNullException` (:233), `TupleContains_NullReferenceTupleEntry_ShouldThrowNotSupported` (:158). Logs `test-{core,sqlite}-focused-gaps.log` |
| **R4** cache rebinding + existing scalar behavior intact | `TupleContains_InWhere_SecondIdenticalCall_ShouldBeCacheHit`, `WhereTuple_ShapeChangeThroughCache_ShouldRebindAndKeepScalarCacheable`, `ScalarQuery_AfterWhereTupleQuery_ShouldStillBeCacheHit`, `SharedAnyCommand_AfterTupleQuery_ShouldStayCacheable`, HAVING/JOIN/SELECT/UNION/derived/nested rebuild tests (`tests/nextorm.sqlite.tests/D193TupleInPlanCacheTests.cs`); planner gate `QueryPlanner.cs:573`; scan closure table above; full suites exit 0 (`test-full-{core,sqlite,sqlserver}-gaps.log`) |
| **R5** EN/RU docs match implementation | EN/RU `docs[/ru]/scalar-functions/06-arrays.md`, `docs[/ru]/advanced/limitations.md`, `docs[/ru]/providers/overview.md`, gap-analysis row (r=1/r=2 sections); stale W4 pointer corrected in this file (`:2126, :2159, :175–176`); no public doc links `docs/specs/**` |
| **R6** coverage/branch audit | CI-form `dotnet-coverage collect "dotnet test nextorm.slnx -c Debug"` exit 0; `reportgenerator` TextSummary: Assemblies 4, **Line 87.2% / Branch 79.1%** ≥ 85/75 (`/tmp/D193-evidence/coverage-final.xml`, `/tmp/D193-evidence/coverage-report-final/Summary.txt`, `coverage-collect-final.log`); `coverage-r2.xml` + `coverage-report/Summary.txt` (87.3/79.1); W1 scan branch-close 100% (`/tmp/D193-r2b/coverage-d193-final.xml`); manual Stryker-absent audit in the r=2 sections (new scan-field branches are defensive, named uncovered) |
| **R7** perf evidence | `/tmp/D193-perf-r2/acceptance.log` (7 cases, 0 failures, exit 0; cached-vs-prepared 2.00 vs baseline 1.87, +6.7% < 2.244 trigger; alloc ratio 7.39 vs 7.42) and `/tmp/D193-perf-r2/in-bench.log` (scalar IN/Contains 5 cases, 0 failures); tuple expansion one-time per preparation, no per-row work (see the R7 section above) |

CHECK note: R1/R3 breadth and the R4 field dispositions are new in this section; R2/R5/R6/R7 reuse the
already-produced r=1/r=2 artifacts. The three requested focused runs and the full affected-suite runs are
green (exit 0) with a clean build (0W/0E). No blocker; no commit/push.

## Final full sweep after scan-closure + arity tests (post-gap) — 2026-10-06T16:56Z (DO)

Progress log:

- 2026-10-06T16:42Z | DO | r=2 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-postgap.log
- 2026-10-06T16:43Z | DO | r=2 | n=1/3 | FULL(core) argv `dotnet test tests/nextorm.core.tests -c Debug --no-build` exit 0, total 1647 / succeeded 1647 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-postgap-core.log
- 2026-10-06T16:43Z | DO | r=2 | n=1/3 | FULL(sqlite) exit 0, total 1105 / succeeded 1104 / failed 0 / skipped 1 (pre-existing `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`, `NEXTORM_LOB_SQLITE_PROBE=1` gate, unrelated to D193) | /tmp/D193-evidence/test-full-postgap-sqlite.log
- 2026-10-06T16:43Z | DO | r=2 | n=1/3 | FULL(postgres) exit 0, total 775 / succeeded 775 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-postgap-postgres.log
- 2026-10-06T16:43Z | DO | r=2 | n=1/3 | FULL(sqlserver) exit 0, total 707 / succeeded 707 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-postgap-sqlserver.log
- 2026-10-06T16:43Z | DO | r=2 | n=1/3 | FULL(mysql) exit 0, total 289 / succeeded 289 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-postgap-mysql.log
- 2026-10-06T16:43Z | DO | r=2 | n=1/3 | FULL(mariadb) exit 0, total 196 / succeeded 196 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-postgap-mariadb.log
- 2026-10-06T16:43Z | DO | r=2 | n=1/3 | FULL(clickhouse) exit 0, total 550 / succeeded 550 / failed 0 / skipped 0 | /tmp/D193-evidence/test-full-postgap-clickhouse.log
- 2026-10-06T16:46Z | DO | r=2 | n=1/3 | full container integration argv `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --no-build` exit 0, total 3295 / succeeded 3098 / failed 0 / skipped 197; no provider availability skip | /tmp/D193-evidence/integration-postgap.log
- 2026-10-06T16:47Z | DO | r=2 | n=1/3 | per-provider execution cross-check via native runner second full run argv `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -result-xml /tmp/D193-evidence/integration-postgap.xml -noColor` exit 0, identical totals 3295/3098/0/197; 5 Testcontainers started (PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse) + SQLite in-process | /tmp/D193-evidence/integration-postgap.xml, /tmp/D193-evidence/integration-postgap-xml.log
- 2026-10-06T16:49Z | DO | r=2 | n=1/3 | focused D193/TupleIn boundary argv `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~TupleIn"` exit 0, total 34 / succeeded 26 / failed 0 / skipped 8; native `-filterVSTest "FullyQualifiedName~TupleIn" -result-xml` identical | /tmp/D193-evidence/boundary-postgap.log, /tmp/D193-evidence/boundary-postgap.xml
- 2026-10-06T16:52Z | DO | r=2 | n=1/3 | CI-form coverage collect argv `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o /tmp/D193-evidence/coverage-postgap.xml "dotnet test --no-build --verbosity normal"` exit 0; solution 8752 total / 6140 succeeded / 0 failed / 2612 skipped (container providers skipped without DOCKER_HOST, as in CI) | /tmp/D193-evidence/coverage-postgap-collect.log
- 2026-10-06T16:52Z | DO | r=2 | n=1/3 | reportgenerator TextSummary exit 0: Assemblies 4, **Line 87.1% (46616/53472) >= 85**, **Branch 79.1% (24793/31326) >= 75** — both CI thresholds met | /tmp/D193-evidence/coverage-report-postgap/Summary.txt
- 2026-10-06T16:54Z | DO | r=2 | n=1/3 | changed-file coverage parsed from reportgenerator Cobertura (table below); branch delta for the 16:45 scan-closure fields: `QueryCommand.QueryPreparer.cs` 86.69% (1980/2284) -> 86.32% (2044/2368) = **-0.37pp**; every other changed file unchanged; manual audit of the new-scan uncovered positive directions recorded below (Stryker absent) | /tmp/D193-evidence/coverage-postgap.xml, /tmp/D193-evidence/coverage-report-postgap/Cobertura.xml
- 2026-10-06T16:55Z | DO | r=2 | n=1/3 | CRLF verified pre/post append; no commit/push/merge; defect history unchanged (no new candidate) | this file, git status

### Command evidence (post-gap final sweep)

| Suite | argv | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | - | - | 0 W / 0 E | - | build-postgap.log |
| core | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | 0 | 1647 | 1647 | 0 | 0 | test-full-postgap-core.log |
| sqlite | `dotnet test tests/nextorm.sqlite.tests -c Debug --no-build` | 0 | 1105 | 1104 | 0 | 1 | test-full-postgap-sqlite.log |
| postgres | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build` | 0 | 775 | 775 | 0 | 0 | test-full-postgap-postgres.log |
| sqlserver | `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build` | 0 | 707 | 707 | 0 | 0 | test-full-postgap-sqlserver.log |
| mysql | `dotnet test tests/nextorm.mysql.tests -c Debug --no-build` | 0 | 289 | 289 | 0 | 0 | test-full-postgap-mysql.log |
| mariadb | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build` | 0 | 196 | 196 | 0 | 0 | test-full-postgap-mariadb.log |
| clickhouse | `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build` | 0 | 550 | 550 | 0 | 0 | test-full-postgap-clickhouse.log |
| integration (all six providers) | `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --no-build` | 0 | 3295 | 3098 | 0 | 197 | integration-postgap.log |
| focused D193/TupleIn | `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~TupleIn"` | 0 | 34 | 26 | 0 | 8 | boundary-postgap.log |

The one per-provider skip is the established `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded`
capability gate (`NEXTORM_LOB_SQLITE_PROBE=1` to run), unrelated to D193. No other per-provider suite has skips.

### Integration per-provider execution (from `integration-postgap.xml`)

All six providers executed; zero failures. 5 Testcontainers instances started/stopped (PostgreSQL, SQL Server,
MySQL, MariaDB, ClickHouse); SQLite runs in-process.

| Provider | executed | passed | failed | skipped | evidence |
|---|---|---|---|---|---|
| ClickHouse | 200 | 200 | 0 | 0 | container |
| MariaDB | 54 `MariaDb*` + 1 `DynamicColumnsMariaDbContainerTests` | 55 | 0 | 0 | container |
| SQL Server | 724 | 676 | 0 | 48 | container |
| MySQL | 664 | 584 | 0 | 80 | container |
| PostgreSQL | 826 | 800 | 0 | 26 | container |
| SQLite | 703 | 663 | 0 | 40 | in-process |
| Shared/other (EF filters, identity/RETURNING, LOB/core contract) | 124 | 121 | 0 | 3 | mixed |
| **Total** | **3295** | **3098** | **0** | **197** | integration-postgap.xml |

All 197 skips are per-capability gates; the D193-specific pair is the expected SQL Server positive executions
(`This provider has no row-value constructor`) and the rejection test skipped on tuple-capable providers
(`This provider supports tuple IN/Contains`). No provider is wholesale-skipped.

### Focused D193/TupleIn boundary — per provider (`FullyQualifiedName~TupleIn`, total 34 / 26 / 0 / 8)

| Provider | executed | passed | skipped | skip reason |
|---|---|---|---|---|
| SQLite | 6 | 5 | 1 | `OnProviderWithoutRowConstructor` (provider supports tuple IN) |
| PostgreSQL | 6 | 5 | 1 | idem |
| SQL Server | 6 | 1 | 5 | no row-value constructor (positive executions gated) |
| MySQL | 6 | 5 | 1 | `OnProviderWithoutRowConstructor` |
| MariaDB | 5 | 5 | 0 | — |
| ClickHouse | 5 | 5 | 0 | — |

### Coverage (CI form) — four assemblies and changed files

Aggregate (`Summary.txt`): Assemblies **4**, **Line 87.1% (46616/53472) >= 85**, **Branch 79.1% (24793/31326) >= 75** — both CI thresholds met.
Assembly line coverage: nextorm.core 87.3%, nextorm.sqlite 90.5%, nextorm.postgres 79.6%, nextorm.sqlserver 80.9%.

| Changed file (git status) | Line | Branch |
|---|---|---|
| nextorm.core/Query/QueryCommand.cs | 98.1% (1239/1263) | 75.4% (534/708) |
| nextorm.core/Query/QueryCommand.Clone.cs | 95.9% (834/870) | 100.0% (132/132) |
| nextorm.core/Query/QueryCommand.QueryPreparer.cs | 91.9% (7938/8642) | 86.3% (8406/9744) |
| nextorm.core/DataContext/QueryPlanner.cs | 96.0% (813/847) | 89.1% (424/476) |
| nextorm.core/Query/InValues.cs | 90.0% (434/482) | 82.4% (310/376) |
| nextorm.core/Visitors/InValuesTranslator.cs | 97.6% (246/252) | 95.6% (218/228) |
| nextorm.core/Visitors/TupleSqlTranslator.cs | 88.9% (144/162) | 88.5% (170/192) |
| nextorm.core/Visitors/TypeFacts.cs | 100.0% (118/118) | 92.3% (358/388) |
| nextorm.core/DataContext/Dialect/DialectCapabilities.cs | 100.0% (112/112) | 60.0% (12/20) |
| nextorm.sqlite/SqliteDialect.cs | 84.2% (310/368) | 64.0% (456/712) |

`DialectCapabilities.cs` / `SqliteDialect.cs` branch gaps are dominated by pre-existing unrelated branches
(D193 added the branchless `ITupleRenderer.RenderInValues` default and `SqliteTupleRenderer.RenderInValues`).

### Branch coverage delta for the newly scanned fields (16:45 scan-closure)

Raw dotnet-coverage cobertura, before = `coverage-final.xml` (16:26, pre-closure) vs after = `coverage-postgap.xml`:

| File | branch before | branch after | delta |
|---|---|---|---|
| nextorm.core/Query/QueryCommand.QueryPreparer.cs | 86.69% (1980/2284) | 86.32% (2044/2368) | **-0.37pp** |
| nextorm.core/Query/InValues.cs | 80.25% (260/324) | 80.25% (260/324) | 0.00pp |
| nextorm.core/Visitors/InValuesTranslator.cs | 95.61% (218/228) | 95.61% (218/228) | 0.00pp |
| nextorm.core/Visitors/TupleSqlTranslator.cs | 88.54% (170/192) | 88.54% (170/192) | 0.00pp |
| nextorm.core/Visitors/TypeFacts.cs | 91.80% (336/366) | 91.80% (336/366) | 0.00pp |
| nextorm.core/DataContext/QueryPlanner.cs | 89.08% (424/476) | 89.08% (424/476) | 0.00pp |
| nextorm.sqlite/SqliteDialect.cs | 64.04% (456/712) | 64.04% (456/712) | 0.00pp |

The whole-solution aggregate branch rate is unchanged at 79.1% (before 24665/31158, after 24793/31326): the
scan-closure additions add +84 branches of which +64 are covered, and the arity/default/null tests added at the
same time cover lines whose branches were already exercised, so no other changed file moves.

### Manual branch audit — new-scan uncovered positive directions (Stryker absent)

The 16:45 scan-closure widened `ScanUnkeyedTupleInValues`/`ScanUnkeyedTupleInFrom` to further unkeyed clauses.
Every clause is guarded `x is { } && InValues.ContainsTupleInValues(x)`; the non-D193 suite exercises the
`x is { }` side and the `Contains == false` side, but no D193 test places a tuple value list in that clause, so
the `Contains == true` direction is missing. These are defensive wide-scan probes (a missing positive test is an
evidence gap, not a correctness regression in the tested shapes; a false negative there would reuse a stale plan):

| New/uncovered clause branch | Site | cond | missing direction |
|---|---|---|---|
| GROUP BY `_groupExp` tuple | QueryPreparer.cs:1894 | 3/4 | `Contains(grouping)` true |
| JOIN derived source tuple | QueryPreparer.cs:1904 | 1/2 | `ScanUnkeyedTupleInFrom(joins[i].From)` true |
| ORDER BY `SortExpression` tuple | QueryPreparer.cs:1914 | 3/4 | `Contains(sortExpression)` true |
| ORDER BY `PreparedExpression` tuple | QueryPreparer.cs:1917 | 3/4 | `Contains(preparedExpression)` true |
| ClickHouse prepared ARRAY JOIN tuple | QueryPreparer.cs:1927 | 1/2 | `Contains(preparedArrayJoin[i])` true |
| ClickHouse `_arrayJoins` fallback guard / loop / element | QueryPreparer.cs:1931/1933/1935 | 1/2, 0/2, 0/2 | `_arrayJoins is {}` true; loop entered; `Contains(arrayJoins[i])` true |
| LIMIT BY tuple | QueryPreparer.cs:1940 | 5/6 | `Contains(limitBy)` true |
| DISTINCT ON tuple | QueryPreparer.cs:1943 | 5/6 | `Contains(distinctOn)` true |
| extreme-row `ValueSelector` tuple | QueryPreparer.cs:1948 | 1/2 | `Contains(ValueSelector)` true |
| extreme-row `GroupBy` / `Projection` tuple | QueryPreparer.cs:1951/1954 | 3/4, 3/4 | `Contains(...)` true |
| named-window PARTITION BY / ORDER BY tuple | QueryPreparer.cs:1967/1973 | 1/2, 1/2 | `Contains(...)` true |
| outer reference tuple | QueryPreparer.cs:1984 | 1/2 | `Contains(outerRefs[i])` true |
| CTE body tuple | QueryPreparer.cs:2003 | 1/2 | `ScanUnkeyedTupleInValues(ctes[i].Query)` true |
| CTE `Mutation.Source` tuple | QueryPreparer.cs:2010 | 5/6 | one source/predicate direction missing |
| FROM `ColumnShape` tuple | QueryPreparer.cs:2047 | 3/4 | `Scan(columnShape)` true |
| `TempTable.Source` tuple | QueryPreparer.cs:2052 | 3/4 | `Scan(tempTable.Source)` true |
| LinqSource `OuterCommand` / `InnerCommand` tuple | QueryPreparer.cs:2060/2063 | 1/2, 3/4 | recursive scan true |
| LinqSource selectors (Collection/Result/OuterKey/InnerKey) | QueryPreparer.cs:2066/2069/2072/2075 | 3/4 each | `Contains(selector)` true |
| `XmlNodes.Operand` tuple | QueryPreparer.cs:2080 | 3/4 | `Contains(xmlNodes.Operand)` true |
| PIVOT `AggregateColumn` / `ForColumn` / inner source | QueryPreparer.cs:2085/2088/2091 | 3/4, 3/4, 1/2 | `Contains(...)` / `Scan(pivot.Inner)` true |
| TVF call tuple | QueryPreparer.cs:2095 | 3/4 | `Contains(tableFunction.Call)` true |

28 of the 59 branch lines in `QueryPreparer.cs:1877-2100` are fully covered; the remaining are exactly the
one-sided wide-scan directions above. The fully uncovered pair `1933`/`1935` (`_arrayJoins` fallback) is likely
unreachable when `_preparedArrayJoin` is populated. Recommendation unchanged: a dedicated test per clause would
close the evidence gap; no blocker for the validated D193 scope.

### Defect history

No new defect candidate: build 0W/0E, all eight suites and both integration runs exit 0, the focused D193
boundary is green, coverage 87.1/79.1 >= 85/75, and the two full integration runs agree exactly. Defect history
is unchanged (D193-W1..W4, F1/F7/F10/F12/F14, and the two r=1 runner defects remain as recorded).

### CRLF / hygiene

- Status file 993 -> 1156 lines; `awk '!/\r$/' docs/specs/status/rc1-tail-193-tuple-in-1.md` -> **0 lines** (100% CRLF); this section was appended at the old EOF, prior content untouched.
- All ten changed production files and the three D193 test files touched this cycle are CRLF (0 LF-only lines each).
- `git status --porcelain` shows only the expected tracked modifications, the untracked status file and the nine untracked D193 test files; no `.bak`/`.orig`/`.patch` and no `BenchmarkDotNet.Artifacts`. **No push/merge. This block was a pre-commit snapshot; it was later committed as e19d751a (product) and cd1a7c6c (bookkeeping).**

Final sweep verdict: build 0W/0E (exit 0); seven per-provider suites exit 0 (core 1647/0/0, sqlite 1105/1104/0/1,
postgres 775/0/0, sqlserver 707/0/0, mysql 289/0/0, mariadb 196/0/0, clickhouse 550/0/0); full container
integration 3295/3098/0/197 with all six providers executed and zero failures; focused D193/TupleIn boundary
34/26/0/8; CI-form coverage Line 87.1% / Branch 79.1% >= 85/75. No blocker; the only open item remains the named
uncovered wide-scan positive branches (evidence gap, not a regression).

## D193 r=2 closing-CHECK evidence — complete R1 arity coverage + variant matrix (2026-10-06T22:04Z)

Progress log:

- 2026-10-06T22:04Z | DO | r=2 | n=1/3 | R1 arity completion: `D193TupleInTests.cs` +9 tests (ValueTuple 3/4/5/6; reference `System.Tuple` 1/4/5/6/7; each matching + mismatch) and `D193TupleInSqlGenerationTests.cs` +9 tests (same arities, `VALUES ($pN...)` + parameter-name assertions); flat arities 1..7 now covered for both families in core + sqlite | tests/nextorm.core.tests/D193TupleInTests.cs, tests/nextorm.sqlite.tests/D193TupleInSqlGenerationTests.cs
- 2026-10-06T22:03Z | DO | r=2 | n=1/3 | build argv `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning / 0 Error | /tmp/D193-evidence/build-arity.log
- 2026-10-06T22:03Z | DO | r=2 | n=1/3 | focused core argv `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~D193|FullyQualifiedName~TupleIn"` exit 0, total 35 / succeeded 35 / failed 0 / skipped 0 (was 26) | /tmp/D193-evidence/test-core-focused-arity.log
- 2026-10-06T22:03Z | DO | r=2 | n=1/3 | focused sqlite argv same selector exit 0, total 36 / succeeded 36 / failed 0 / skipped 0 (was 27) | /tmp/D193-evidence/test-sqlite-focused-arity.log
- 2026-10-06T22:04Z | DO | r=2 | n=1/3 | status header `:8` corrected to r=2 with W1–W4 + F1/F7/F10/F12/F14 applied and the closing-CHECK arity work; R1–R7 and the plan sections untouched; variant matrix + contract binding appended below; no commit/push | this file
- 2026-10-06T22:04Z | DO | r=2 | n=1/3 | CRLF: status file and both touched test files 100% CRLF; no `.bak`/`.orig`/`.patch` added | this file, git status

### R1 arity completion — flat arities 1..7 for both families (method-per-arity)

| Family | Arity | In-memory test (`D193TupleInTests.cs`) | SQLite SQL-gen test (`D193TupleInSqlGenerationTests.cs`) |
|---|---|---|---|
| ValueTuple | 1 | `Contains_ValueTupleArityOne_ShouldMatchBothRows` | `TupleContains_ValueTupleArityOne_ShouldRenderValuesInList` |
| ValueTuple | 2 | `Contains_ValueTupleList_ShouldMatchBothRows`, `...MismatchedComponent...` | `TupleContains_CapturedList_ShouldRenderValuesInList` |
| ValueTuple | 3 | `Contains_ValueTupleArityThree_ShouldMatchAndRejectMismatch` | `TupleContains_ValueTupleArityThree_ShouldRenderValuesInList` |
| ValueTuple | 4 | `Contains_ValueTupleArityFour_ShouldMatchAndRejectMismatch` | `TupleContains_ValueTupleArityFour_ShouldRenderValuesInList` |
| ValueTuple | 5 | `Contains_ValueTupleArityFive_ShouldMatchAndRejectMismatch` | `TupleContains_ValueTupleArityFive_ShouldRenderValuesInList` |
| ValueTuple | 6 | `Contains_ValueTupleAritySix_ShouldMatchAndRejectMismatch` | `TupleContains_ValueTupleAritySix_ShouldRenderValuesInList` |
| ValueTuple | 7 | `Contains_ValueTupleAritySeven_ShouldMatchBothRows` | `TupleContains_ValueTupleAritySeven_ShouldRenderValuesInList` |
| System.Tuple | 1 | `Contains_ReferenceTupleArityOne_ShouldMatchAndRejectMismatch` | `TupleContains_ReferenceTupleArityOne_ShouldRenderValuesInList` |
| System.Tuple | 2 | `Contains_ReferenceTupleList_ShouldMatchRow` | `TupleIn_AtInForm_ShouldRenderValuesInList` |
| System.Tuple | 3 | `Contains_ReferenceTupleArityThree_ShouldMatchBothRows` | `TupleContains_ReferenceTupleArityThree_ShouldRenderValuesInList` |
| System.Tuple | 4 | `Contains_ReferenceTupleArityFour_ShouldMatchAndRejectMismatch` | `TupleContains_ReferenceTupleArityFour_ShouldRenderValuesInList` |
| System.Tuple | 5 | `Contains_ReferenceTupleArityFive_ShouldMatchAndRejectMismatch` | `TupleContains_ReferenceTupleArityFive_ShouldRenderValuesInList` |
| System.Tuple | 6 | `Contains_ReferenceTupleAritySix_ShouldMatchAndRejectMismatch` | `TupleContains_ReferenceTupleAritySix_ShouldRenderValuesInList` |
| System.Tuple | 7 | `Contains_ReferenceTupleAritySeven_ShouldMatchAndRejectMismatch` | `TupleContains_ReferenceTupleAritySeven_ShouldRenderValuesInList` |

### Variant matrix — complete closure (every cell `test` | `guard` | `deferred`)

Legend: **`test`** = a dedicated D193 test exists (named); **`guard`** = no dedicated D193 test, defensively handled/reviewed (for a cache scan a false positive only forgoes caching); **`deferred`** = out of the shipped scope with an explicit re-open trigger (current milestone; no future-milestone promise). **Aggregate guard trigger (applies to every `guard` cell):** any change to these containers / traversal / rendering / cache eligibility, or a captured-shape regression. No cell is open/blank.

#### A. Representation × arity × provider

ValueTuple family (captured tuple-literal collections; `new ValueTuple<...>` LHS):

| Arity | In-memory (core) | SQLite | PostgreSQL | MySQL/MariaDB | ClickHouse | SQL Server |
|---|---|---|---|---|---|---|
| 1 | test | test | guard | guard | guard | test |
| 2 | test | test | test | test | test | test |
| 3 | test | test | guard | guard | guard | test |
| 4 | test | test | guard | guard | guard | test |
| 5 | test | test | guard | guard | guard | test |
| 6 | test | test | guard | guard | guard | test |
| 7 | test | test | guard | guard | guard | test |

`System.Tuple` family (`Tuple.Create` LHS):

| Arity | In-memory (core) | SQLite | PostgreSQL | MySQL/MariaDB | ClickHouse | SQL Server |
|---|---|---|---|---|---|---|
| 1 | test | test | guard | guard | guard | test |
| 2 | test | test | test | test | test | test |
| 3 | test | test | guard | guard | guard | test |
| 4 | test | test | guard | guard | guard | test |
| 5 | test | test | guard | guard | guard | test |
| 6 | test | test | guard | guard | guard | test |
| 7 | test | test | guard | guard | guard | test |

- The arity layer is provider-independent: `InValues.PartitionTuple` flattens `ITuple` rows by arity and `InValuesTranslator.TranslateTupleInValues` renders cells through the provider's `ITupleRenderer`. The provider SQL-gen suites therefore each pin the representative **arity 2** form: PG/MySQL/MariaDB `TupleContains_CapturedList_ShouldRenderRowValueList`, CH `TupleContains_CapturedList_ShouldRenderTupleValueList`, SQLite arities 1..7 (table above). The non-representative provider×arity cells are `guard` (shared renderer path), not untested behavior classes.
- SQL Server rejection is arity-independent and fires in the preparation preflight (`EnsureProviderSupportsTupleInValues`); pinned by `TupleContains_ShouldThrowAtPreparation` (arity 2) and `TupleContains_ArityEight_ShouldThrowPinnedMessageBeforeArityValidation`.

#### B. RHS form and value-shape variants

| Variant | Closure | Evidence / pointer |
|---|---|---|
| Captured `List<T>` (ICollection) RHS | test | `TupleContains_CapturedList_ShouldRenderValuesInList` (sqlite), `Contains_ValueTupleList_ShouldMatchBothRows` (core) |
| Captured non-ICollection `IEnumerable` (LINQ iterator) RHS | test | `PartitionTuple_NonCollectionEnumerable_ShouldStillPartitionRows` (core) |
| Inline array `new[] { ... }` tuple RHS | guard | `InValues.IsStableValueExpression` NewArrayExpression path; no D193 tuple-inline-array test (scalar inline array covered by existing suite) |
| `SqlFunctions.Sql.@in(tuple, collection)` form | test | `TupleIn_AtInForm_ShouldRenderValuesInList` (sqlite), `TupleIn_AtInForm_ShouldRenderRowValueList` (PG/MySQL/MariaDB) |
| ClickHouse `global_in` form | test | `TupleGlobalIn_CapturedList_ShouldRenderGlobalIn` |
| Singleton RHS (one row) | guard | no dedicated 1-row D193 test (all captured tests use 2 rows); row-count/arm switch `arms.Count` 0/1/_ is exercised by empty and 2-row cases |
| Multiple rows | test | `TupleContains_CapturedList_ShouldRenderValuesInList` |
| Duplicate rows | guard | duplicates render repeated (non-null) row arms without dedup; no dedicated test; correctness-neutral |
| Empty collection | test | `Contains_EmptyList_ShouldReturnNoRows` (core), `TupleContains_Empty_ShouldRenderAlwaysFalse` (SQLite) + every provider |
| `default` value-tuple entry | test | `Contains_DefaultValueTupleEntry_ShouldMatchDefaultsRow` (core), `TupleContains_DefaultValueTupleEntry_ShouldRenderOrdinaryRow` (sqlite) |
| Reference (`System.Tuple`) entries | test | `Contains_ReferenceTupleList_ShouldMatchRow` (core), `TupleContains_NullReferenceTupleEntry_ShouldThrowNotSupported` (sqlite) |
| Nullable component(s) (value + reference) | test | `Contains_NullableComponent_ShouldMatchNullTupleRow` (core), `TupleContains_NullableComponent_ShouldGuardAndUseNullArm` + `...ReferenceTypeNullable...` (sqlite) + each provider |
| Null component mixed with non-null siblings | test | `Contains_NullableComponent_ValueInNullableSlot_ShouldKeepNonNullAndNullMatches` (core), `TupleContains_NullableComponent_ShouldGuardAndUseNullArm` |
| Negation (`!Contains` / `NOT IN`) | test | `Contains_NegatedTupleList_ShouldExcludeMatch`, `Contains_NullableComponent_Negation_ShouldExcludeNullTupleRow` (core); `Contains_TupleIn_Negation_ShouldExcludeNullComponentMatch` (integration); `TupleIn_Negation_ShouldExcludeMatch` (MariaDB) |
| Converter-bound tuple component | test | `TupleContains_ConvertedComponent_ShouldBindProviderValue` (sqlite) |
| Context / plan-cache reuse (same shape hit, scalar path intact) | test | `TupleContains_SequentialDifferentShapes_ShouldRebindWithoutDisablingCache`, `ScalarQuery_AfterWhereTupleQuery_ShouldStillBeCacheHit`, `SharedAnyCommand_AfterTupleQuery_ShouldStayCacheable` |
| Constructor LHS `new ValueTuple<...>` / `Tuple.Create` | test | arity table above |
| Constructor LHS `new Tuple<...>` (non-`Tuple.Create`) | guard | `TupleSqlTranslator.TryTranslateNew` path is shared with tested `ValueTuple` constructors; no dedicated reference-`new` test |
| Parameterised RHS (no accidental value interpolation) | test | `DbCommandParams` name/value assertions in every SQLite test (values bind as `$pN`, never inline) |

#### C. Rejection and edge shapes (fail-closed)

| Variant | Closure | Evidence / pointer |
|---|---|---|
| Null collection | test | `PartitionTuple_NullCollection_ShouldThrowArgumentNullException` (core), `TupleContains_NullCollection_ShouldThrowArgumentNullException` (sqlite), `TupleContains_NullCollection_ShouldThrowPinnedMessageBeforeNullArgument` (SQL Server) |
| Null tuple entry (reference tuple) | test | `PartitionTuple_NullTupleEntry_ShouldThrowNotSupported` (core), `TupleContains_NullReferenceTupleEntry_ShouldThrowNotSupported` (sqlite), SQL Server pinned |
| Malformed arity (row arity != LHS arity) | test | `PartitionTuple_MismatchedArity_ShouldThrowNotSupported` (core), `TupleIn_TupleTypedLhs_ShouldThrowNotSupported` (sqlite) |
| Non-tuple entry in the collection | test | `PartitionTuple_NonTupleEntry_ShouldThrowNotSupported` (core) |
| Arity ≥ 8 / CLR `Rest` form | test | `PartitionTuple_ArityEight_ShouldThrowNotSupported` (core), `TupleContains_ArityEight_ShouldThrowPinnedMessageBeforeArityValidation` (SQL Server) |
| Nested tuple component | test | `PartitionTuple_NestedTuple_ShouldThrowNotSupported` (core), `TupleContains_Nested_ShouldThrowPinnedMessageBeforeNestedValidation` (SQL Server) |
| Nullable tuple element (`List<(int,int)?>`) | test | `PartitionTuple_NullableValueTupleElement_ShouldThrowNotSupported` (core), `TupleContains_NullableValueTupleElement_ShouldThrowNotSupported` (sqlite) |
| Non-tuple LHS (tuple-typed member, captured tuple) | test | `TupleIn_TupleTypedLhs_ShouldThrowNotSupported` (sqlite) |
| Non-enumerable value | test | `PartitionTuple_NonEnumerableValue_ShouldThrowNotSupported` (core) |
| Scalar/tuple mixing (tuple element family only) | test | covered by nullable/nested/arity rejections; scalar membership unchanged (existing scalar suite green) |
| SQL Server: every tuple form rejects before empty folding, no SQL submitted | test | 7 SQL Server D193 tests incl. HAVING/SELECT/JOIN-ON + empty; preparation-only, no execution |

#### D. Cache containers

WHERE / PREWHERE (shape-hashed into the plan key, cacheable):

| Container | Closure | Evidence / pointer |
|---|---|---|
| WHERE | test | `TupleContains_InWhere_SecondIdenticalCall_ShouldBeCacheHit`, `WhereTuple_ShapeChangeThroughCache_ShouldRebindAndKeepScalarCacheable` |
| PREWHERE | guard | same `_condition`-family shape fold; no dedicated PREWHERE D193 test (ClickHouse) |

Unkeyed containers (scan is `QueryCommand.ScanUnkeyedTupleInValues`; false positive only forgoes caching, false negative would be wrong-result — hence the guard):

| Container | Closure | Evidence / pointer |
|---|---|---|
| SELECT projection | test | `TupleContains_InSelectColumn_DifferentSizes_ShouldRebuildAndReturnCurrentResult` |
| HAVING | test | `TupleContains_InHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult` |
| JOIN ON | test | `TupleContains_InJoinOn_DifferentSizes_ShouldRebuildAndReturnCurrentResult` |
| UNION / set operation | test | `TupleContains_InUnionBranchHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult` |
| Derived table (FROM subquery) | test | `TupleContains_InDerivedTableHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult` |
| Nested referenced subquery (recursion) | test | `TupleContains_InNestedReferencedHaving_DifferentSizes_ShouldRebuild`, `TupleContains_InReferencedQueryHavingOnly_ShouldFlagOuterAndRebuild` |
| Repeated referenced query (visited-cycle guard) | test | `TupleContains_RepeatedReferencedQuery_ShouldNotRevisit` |
| GROUP BY | guard | scan widened; positive direction not dedicatedly tested |
| ORDER BY | guard | scan widened; positive direction not dedicatedly tested |
| Named windows (PARTITION BY / ORDER BY) | guard | scan widened; positive direction not dedicatedly tested |
| PIVOT (aggregate/for/inner) | guard | scan widened; positive direction not dedicatedly tested |
| Table-valued function (TVF) call | guard | scan widened; positive direction not dedicatedly tested |
| CTE body | guard | scan widened; positive direction not dedicatedly tested |
| ClickHouse ARRAY JOIN / LIMIT BY / DISTINCT ON / extreme row | guard | scan widened; positive direction not dedicatedly tested |
| Outer references (correlated) | guard | scan widened; positive direction not dedicatedly tested |
| LinqSource commands + selectors | guard | scan widened; positive direction not dedicatedly tested |
| XML nodes operand | guard | scan widened; positive direction not dedicatedly tested |
| TempTable source / `FromExpression.ColumnShape` | guard | scan widened; positive direction not dedicatedly tested |
| Data-modifying CTE mutation | guard | never plan-cached (`HasDataModifyingCte` clears `storeInCache`), so a stale plan is unreachable regardless of the scan |
| Shared Any/Count command stays cacheable | test | `SharedAnyCommand_AfterTupleQuery_ShouldStayCacheable`, `TupleContains_TwoShapesOnSharedAnyCommand_ShouldRebindAndKeepCacheable` |

#### E. Deferred (current milestone; no future-milestone promise)

| Variant | Closure | Re-open trigger |
|---|---|---|
| Tuple-typed `QueryCommand` RHS (`(a,b) IN (SELECT x,y ...)`) | deferred | Only if a user scenario requires a tuple-valued subquery RHS; the shipped scope is the value-list RHS (`InValues.TryGetArguments` excludes `QueryCommand`; scalar `@in(column, QueryCommand)` is the separate path). No milestone assigned. |
| `Rest` / nested tuple support (arity ≥ 8) | deferred | Only if a user scenario requires the CLR `Rest`/nested shape; renderer scope is frozen at flat 1..7 and both are rejected fail-closed. No milestone assigned. |
| Alternate bulk binding (one opaque tuple parameter) | deferred | Only if parameter ceilings are measured to be a practical limit (measured-limit trigger); the shipped form binds ordered scalar component parameters. |

### Contract binding (explicit)

- Current evidence contract is **`rv=1`**, rows **`E193-00..06`** exactly as written in the "Evidence contract rv1" section above. There is **no `rv=2`**; no supplemental rows were ever adopted.
- DO-ledger mapping (row → actual artifact paths):
  - **E193-00** (preflight + reconnaissance spike): `/tmp/D193-evidence/d193-spike-observations.txt`, `/tmp/D193-evidence/d193-spike-run.log`, `/tmp/D193-evidence/build-integration.log`.
  - **E193-01** (SQL generation tests): `/tmp/D193-evidence/test-core-focused-gaps.log`, `/tmp/D193-evidence/test-sqlite-focused-gaps.log`, `/tmp/D193-evidence/test-{postgres,mysql,mariadb,clickhouse,sqlserver}-focused-gaps.log`, plus this run's arity logs `/tmp/D193-evidence/test-core-focused-arity.log`, `/tmp/D193-evidence/test-sqlite-focused-arity.log`.
  - **E193-02** (per-provider FULL/SQL): `/tmp/D193-evidence/test-full-postgap-{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.log`, `/tmp/D193-evidence/test-{postgres,mysql,mariadb,clickhouse,sqlserver}-focused-r2final.log`.
  - **E193-03** (container execution): `/tmp/D193-evidence/integration-postgap.log`, `/tmp/D193-evidence/integration-postgap.xml`, `/tmp/D193-evidence/boundary-postgap.log`, `/tmp/D193-evidence/boundary-postgap.xml`, `/tmp/D193-evidence/boundary-d193-r2.log`, `/tmp/D193-evidence/boundary-d193-r2.xml`, `/tmp/D193-evidence/test-integration-tuplein-boundary.log`.
  - **E193-04** (docs): EN/RU docs diff (`docs[/ru]/scalar-functions/06-arrays.md`, `docs[/ru]/advanced/limitations.md`, `docs[/ru]/providers/overview.md`); `git status` in this working tree.
  - **E193-05** (regression / coverage / perf): `/tmp/D193-evidence/coverage-postgap.xml`, `/tmp/D193-evidence/coverage-report-postgap/Summary.txt`, `/tmp/D193-evidence/coverage-r2.xml`, `/tmp/D193-evidence/coverage-report/Summary.txt`, `/tmp/D193-r2b/coverage-d193-final.xml`, `/tmp/D193-perf-r2/acceptance.log`, `/tmp/D193-perf-r2/in-bench.log`.
  - **E193-06** (build 0W/0E, CRLF/scope hygiene, no commit): `/tmp/D193-evidence/build-postgap.log`, `/tmp/D193-evidence/build-arity.log`, this status file, `git status`.

### Matrix closure audit

All closure cells are one of `test` / `guard` / `deferred`; no open/blank cell. Counts: Representation×arity×provider tables 14 arity rows × 6 providers = 84 cells, RHS/value-shape 19 rows, rejection/edge 11 rows, cache containers 2 + 20 = 22 rows, deferred 3 rows. Every `guard` cell carries the aggregate trigger; every `deferred` row carries its own re-open trigger.

## E193-00 evidence — preflight/reconnaissance (explicit row binding) — 2026-10-06T22:07Z

Reconnaissance artifacts confirmed present:
- `/tmp/D193-evidence/d193-spike-observations.txt` (10864 B, 88 lines) — recorded probe observations.
- `/tmp/D193-evidence/d193-spike-run.log` (4045 B) — xunit v3 In-Process Runner v4.0.1+8ed8aa354c (.NET 10.0.12); summary line
  `nextorm.integration.tests  Total: 5, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 21.367s` (exit 0).
- `/tmp/D193-evidence/build-integration.log` — affected build for the spike run, 0W/0E.
- The throwaway spike test itself (`tests/nextorm.integration.tests/D193TupleInSpikeTests.cs`) was **removed at finalize per W3**; it is absent from the tree and from `git status --porcelain`. The recorded observations above are the retained evidence.

Compact factual list (from "Part 2 — preflight facts" and "D193.1 spike findings"):
- **Arity 1..7, no `Rest`.** `TypeFacts.IsTupleType`/`IsValueTupleType`/`IsTupleLike` (`src/nextorm.core/TypeFacts.cs:69-99`) recognise the `System.Tuple`/`System.ValueTuple` families for arities 1..7 only (`Tuple<>` … `Tuple<,,,,,,>`); the 8-element `…,TRest` form is deliberately not recognised and nested tuples are not flattened. `TupleSqlTranslator.TryTranslateCreate` (`:16`) renders raw `node.Arguments`, so the 8-arg (`7+TRest`) overload would misrender. Arity ≥8 / `Rest` / nested are rejected fail-closed with `NotSupportedException`.
- **Public membership overloads.** `CommonFunctions.@in<T>(T, IEnumerable<T>)` (`SqlFunctions.cs:481`), `@in<T>(T, params T[])` (`:487`), `@in<T>(T, QueryCommand<T>)` (`:475`, subquery RHS); `ClickHouseFunctions.global_in<T>` (`SqlFunctions.ClickHouse.cs:323`, `:326`, `:320`); `Enumerable.Contains<TSource>`/`MemoryExtensions.Contains` (span)/`List<T>.Contains` accepted by `InValues.TryGetContainsArguments` (`InValues.cs:103`; span unwrap `:304`). Subquery RHS is excluded from the value-list path (`InValues.cs:89-91`) and handled by `CorrelatedQueryExpressionVisitor.cs:28`.
- **`InValues` extraction entry points (`file:line`).** `InValues.TryGetArguments` (`src/nextorm.core/Query/InValues.cs:77`), `InValues.EvaluatePartition` (`:168`), `InValues.Partition` (`:171`), `InValues.ComputeShapeHash` (`:213`); `InValuesTranslator.TryTranslateCollectionContains` (`src/nextorm.core/Visitors/InValuesTranslator.cs:20`), `InValuesTranslator.TranslateInValues` (`:37`); partitions/shape via `QueryCommand.InValuesPartitions` (`QueryCommand.cs:116`), `QueryCommand.InValuesShapeHash` (`:104`).
- **Per-provider `SqlGenerationTests` namespaces/classes.** `NextORM.Sqlite.Tests.SqlGenerationTests` (`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:15`), `NextORM.Postgres.Tests.SqlGenerationTests` (postgres `:16`), `NextORM.SqlServer.Tests.SqlGenerationTests` (sqlserver `:14`), `NextORM.MySql.Tests.SqlGenerationTests` (mysql `:13`), `NextORM.MariaDb.Tests.SqlGenerationTests` (mariadb `:10`), `NextORM.ClickHouse.Tests.SqlGenerationTests` (clickhouse `:11`); the `FullyQualifiedName~SqlGenerationTests` filter selects each provider's class.
- **MariaDB has no `ProviderTestSuite`.** There is no `MariaDbTestProvider`; MariaDB integration coverage is standalone classes with their own `MariaDbDataContext` (e.g. `MariaDbTupleInExecutionTests`), so the integration `-class` selector list has no MariaDB entry.
- **Spike provider versions.** PostgreSQL **17.11**, MySQL **8.4.11**, MariaDB **11.4.13**, ClickHouse **25.8.33.6**, SQLite **3.53.4**. Containers `a1877941aa2e` (PG), `914f06a0ba5f` (MariaDB), `525af9d6a314` (CH), `d703aedb9b21` (MySQL) all started and were stopped; none skipped.
- **Observations table conclusion.** List-of-tuples shape confirmed on all four non-SQL-Server dialects: SQLite `(a,b) IN (VALUES (…),(…))`, MySQL/MariaDB `(a,b) IN ((…),(…))`, PostgreSQL `ROW(a,b) IN (ROW(…),ROW(…))`, ClickHouse `tuple(a,b) IN (tuple(…),tuple(…))` (singleton `tuple(…)` and bare `((…))` both accepted; ClickHouse `GLOBAL IN` works). **A tuple containing SQL `NULL` matches nothing** through row-`IN` on every provider (SQL `UNKNOWN`), and ClickHouse `isNotDistinctFrom` is `NOT_IMPLEMENTED` (Code 48) outside `JOIN ON`, so null-component semantics require explicit OR-of-AND guarded component arms. Query-derived RHS is SQL-feasible on all four dialects but the value-list RHS is the frozen shipped scope (tuple-typed `QueryCommand` RHS deferred). Negation and empty RHS render deterministically. SQL Server has no `Tuple` override → `ISqlDialect.Tuple == null` / `SupportsTupleFunctions == false` and rejects before empty-list folding with the pinned message.

## E193-06 evidence — build / CRLF / no-commit hygiene (explicit row binding) — 2026-10-06T22:07Z

Build — argv `dotnet build nextorm.slnx -c Debug`, exit code **0**, log `/tmp/D193-evidence/build-postgap.log`; verbatim:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

CRLF audit — every D193-modified/untracked `.cs`/`.md` file, `awk '!/\r$/' <file> | wc -l`:
- Files checked: **29** (19 modified + 10 untracked); total non-CRLF lines: **0** — every file reported 0.
- Modified (19): `docs/advanced/limitations.md`, `docs/providers/overview.md`, `docs/scalar-functions/06-arrays.md`, `docs/ru/advanced/limitations.md`, `docs/ru/providers/overview.md`, `docs/ru/scalar-functions/06-arrays.md`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs`, `src/nextorm.core/DataContext/QueryPlanner.cs`, `src/nextorm.core/Query/InValues.cs`, `src/nextorm.core/Query/QueryCommand.Clone.cs`, `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/Query/QueryCommand.cs`, `src/nextorm.core/Visitors/InValuesTranslator.cs`, `src/nextorm.core/Visitors/TupleSqlTranslator.cs`, `src/nextorm.core/Visitors/TypeFacts.cs`, `src/nextorm.sqlite/SqliteDialect.cs`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite.In.cs`.
- Untracked (10): `docs/specs/status/rc1-tail-193-tuple-in-1.md`, `tests/nextorm.core.tests/D193TupleInTests.cs`, `tests/nextorm.sqlite.tests/D193TupleInPlanCacheTests.cs`, `tests/nextorm.sqlite.tests/D193TupleInSqlGenerationTests.cs`, `tests/nextorm.postgres.tests/D193TupleInSqlGenerationTests.cs`, `tests/nextorm.sqlserver.tests/D193TupleInSqlGenerationTests.cs`, `tests/nextorm.mysql.tests/D193TupleInSqlGenerationTests.cs`, `tests/nextorm.mariadb.tests/D193TupleInSqlGenerationTests.cs`, `tests/nextorm.clickhouse.tests/D193TupleInSqlGenerationTests.cs`, `tests/nextorm.integration.tests/MariaDbTupleInExecutionTests.cs`.
- The status file was re-verified at **0** non-CRLF lines after this append (100% CRLF).

No-commit hygiene:
- HEAD — `git log -1 --format='%H %s'` → `0c663bb7ba32f190df97995add2f4131df3b0bbd #150 status: mark D150 done in collection 1.0.9-rc1-tail` (expected `0c663bb7 …`); base HEAD unchanged.
- `git status --porcelain` lists the 19 modified + 10 untracked D193 files above, all later committed in e19d751a (product) and cd1a7c6c (bookkeeping).
- No D193-created `.bak`/`.orig`/`.rej`/`*.patch` inside the repo. The `/tmp/D193-evidence` backups (`QueryPlanner.cs.bak`, `f1-scan.patch`, `D193-*.diff`) are **outside** the repo (allowed). The `.patch` files under `benchmarks/BenchmarkDotNet.Artifacts/**` and `docs/specs/experiments/**` are pre-existing **tracked** files, unchanged, and not created by D193.
- **No push or merge was performed. The D193 work was committed as e19d751a (product) and cd1a7c6c (bookkeeping); the historical ACT was an escalate-authorized evidence-completeness finalization, superseded by the independent CHECK r=2 and corrective task rc1-tail-193-verify-1.**

## CHECK r=2 — verdict PASS (independent, corrective rc1-tail-193-verify-1) — 2026-10-07

- verdict: **PASS** — independent `check` verdict (no escalate waiver); every pinned variant row closed by test/guard/deferred+trigger; E193-00..06 closed for rv=1; no in-scope product defect.
- validator: `brief` exit 0 (`/tmp/nextorm-rc1-tail-193-verify-1/brief.log`), `report` exit 0 (`/tmp/nextorm-rc1-tail-193-verify-1/report.log`), evidence `/tmp/nextorm-rc1-tail-193-verify-1/evidence.json`
- independent check report: `/tmp/nextorm-rc1-tail-193-verify-1/check.md`

### Executions (5) — phase, exact command, exit code, selected count

| # | Phase | Command | Exit | Selected |
|---|---|---|---|---|
| 1 | inner | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | 0 | 32 |
| 2 | inner | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | 0 | 36 |
| 3 | inner | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | 0 | 10 |
| 4 | boundary | `dotnet build nextorm.slnx -c Debug` | 0 | n/a |
| 5 | boundary | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug` | 0 | 3311 |

- Boundary solution build: `0 Warning(s) 0 Error(s)` (`/tmp/nextorm-rc1-tail-193-verify-1/build.log`).
- Boundary integration sweep: **Total 3311 / Passed 3114 / Failed 0 / Skipped 197** (`/tmp/nextorm-rc1-tail-193-verify-1/integration.log`); all providers (PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse, SQLite) executed, no provider-unavailable skips.

### W1–W4 status

W1–W4 were all fixed in `e19d751a`; re-verified by the filtered tests (core 32, sqlite 36, sqlserver 10, exit 0) and the boundary integration sweep (3311/3114/0/197). None open.

## Integration reconciliation (corrective rc1-tail-193-verify-1)

- Fresh boundary sweep total **3311 / 3114 / 0 / 197** (`/tmp/nextorm-rc1-tail-193-verify-1/integration.log`, this task).
- Parsed TRX breakdown (`/tmp/nextorm-rc1-tail-193-verify-1/reconciliation.md`; source TRX `/tmp/nextorm-rc1-tail-c/05-integration.trx`, same tree/HEAD as the fresh boundary sweep, both total 3311):

| bucket | total | passed | failed | skipped |
|---|---:|---:|---:|---:|
| PostgreSQL | 826 | 800 | 0 | 26 |
| SQL Server | 724 | 676 | 0 | 48 |
| MySQL | 664 | 584 | 0 | 80 |
| MariaDB | 55 | 55 | 0 | 0 |
| ClickHouse | 205 | 205 | 0 | 0 |
| SQLite | 714 | 674 | 0 | 40 |
| provider-prefix subtotal | 3188 | 2994 | 0 | 194 |
| provider-parameterized shared folded in: `CoreApiContractTests` 8 + `DialectCapabilityContractTests` 3 + `EfCoreQueryFilterLifecycleTests` 12 + `EfCoreServerSharedTransactionTests` 25 | 48 | 48 | 0 | 0 |
| **provider rows total** | **3236** | **3042** | **0** | **194** |

- Non-provider "other"/cross-cutting bucket = **75 / 72 / 3**: `IdentityReturningIntegrationTests` 50, `EfCoreQueryFilterBridgeTests` 20, `QueryFilterInMemoryParityTests` 1, `EfCoreSharedTransactionTests` 1, `LobPerfHarnessTests` 1 (skip), `LobCapabilityProbeTests` 2 (skip).
- Arithmetic: **3236 + 75 = 3311; 3042 + 72 = 3114; 194 + 3 = 197.** Strict prefix-only split (without folding the 4 provider-parameterized shared classes) is 3187 / 2993 / 194 + 124 / 121 / 3 — also totals to 3311 / 3114 / 0 / 197.
- The 3 non-provider / env-gated skips confirmed: `LobPerfHarnessTests.Measure_Streaming_Vs_Buffered_Lob_Allocations` (`NEXTORM_LOB_PERF`), `LobCapabilityProbeTests.MySql_Driver_Reads_Lobs_Buffered` and `LobCapabilityProbeTests.MariaDb_Driver_Reads_Lobs_Buffered` (`NEXTORM_LOB_PROBE`).
- Baseline: TRX `/tmp/nextorm-rc1-tail-c/05-integration.trx` (same tree/HEAD as the fresh boundary sweep, both total 3311); reconciliation file `/tmp/nextorm-rc1-tail-193-verify-1/reconciliation.md`.
- Historical totals belong to their own runs/commits, **not** this fresh sweep: D193 status `integration-postgap` 3295/3098/197; D198 check2 3300/3103/197; tail-c logs 3311/3114/197; boundary r=2 34/26/8.

## ACT — finalization (escalate-authorized) — 2026-10-06T22:20Z

> Historical: this ACT was an escalate-authorized evidence-completeness finalization (no re-run). It is superseded by the independent CHECK r=2 and the corrective task rc1-tail-193-verify-1.

Progress log:

- 2026-10-06T22:20Z | ACT | r=2 | n=1/3 | finalization authorized by `escalate`; CHECK FAIL objection was evidence-completeness only (no product defect established); rv1 re-gather budget exhausted; artifacts below derived from the existing logs/XML without re-run | /tmp/D193-evidence/integration-postgap.xml, /tmp/D193-evidence/boundary-postgap.xml
- 2026-10-06T22:20Z | ACT | r=2 | n=1/3 | status header finalized to `status: done` (N=1, r=2, n=1/3, rv=1) | this file

### CHECK objection disposition (accepted by escalate)

> CHECK objection accepted by escalate: evidence-completeness only, no product defect established; rv1 re-gather budget exhausted.

Defect-history closure — every recorded defect is fixed/verified, none open:

| defect key | status |
|---|---|
| D193-stale-integration-build | Resolved |
| D193-runner-filter-conflict | Resolved |
| D193-W1 | fixed-verified |
| D193-W2 | fixed |
| D193-W3 | fixed |
| D193-W4 | fixed |
| D193-F1 | fixed-verified |
| D193-F7 | verified |
| D193-F10 | fixed-verified |
| D193-F12 | verified |
| D193-F14 | fixed-verified |

### Integration skips grouped by reason — `integration-postgap.xml` (total 3295 / passed 3098 / failed 0 / skipped 197)

Derived from the existing XML/log (`/tmp/D193-evidence/integration-postgap.xml`, `integration-postgap.log`); no re-run.
All six providers executed (PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse containers + SQLite in-process); zero failures.

Category subtotals — D193 tuple gates **8**; LOB gates **87**; returning/CTAS/temp-table/write gates **57**; set-operation/join/MERGE gates **11**; TVF/lateral/cardinality/misc support-flip gates **34**; **total 197**.

**D193 tuple gates (the D193-specific pair)**

| reason | count | providers |
|---|---|---|
| This provider has no row-value constructor. | 5 | SqlServerIntegrationTests 5 |
| This provider supports tuple IN/Contains. | 3 | MySqlIntegrationTests 1, PostgresIntegrationTests 1, SqliteIntegrationTests 1 |

**LOB capability gates**

| reason | count | providers |
|---|---|---|
| This provider does not implement streaming LOB reads. | 39 | MySqlIntegrationTests 39 |
| This provider implements streaming LOB reads. | 33 | PostgresIntegrationTests 11, SqlServerIntegrationTests 11, SqliteIntegrationTests 11 |
| This provider implements the multi-column LOB reader terminal. | 6 | PostgresIntegrationTests 2, SqlServerIntegrationTests 2, SqliteIntegrationTests 2 |
| This provider does not implement the multi-column LOB reader terminal. | 6 | MySqlIntegrationTests 6 |
| Set NEXTORM_LOB_PROBE=1 to run the LOB capability probe. | 2 | LobCapabilityProbeTests 2 |
| Set NEXTORM_LOB_PERF=1 to run the LOB allocation harness. | 1 | LobPerfHarnessTests 1 |

**Returning / CTAS / temp-table / write gates**

| reason | count | providers |
|---|---|---|
| This provider cannot run a CTAS batch. | 10 | SqlServerIntegrationTests 10 |
| This provider cannot return inserted rows. | 10 | MySqlIntegrationTests 10 |
| This provider supports stored procedures. | 6 | MySqlIntegrationTests 2, PostgresIntegrationTests 2, SqlServerIntegrationTests 2 |
| Data-modifying CTE bodies are only supported by PostgreSQL. | 6 | MySqlIntegrationTests 2, SqlServerIntegrationTests 2, SqliteIntegrationTests 2 |
| This provider materialises a query into a temporary table. | 6 | MySqlIntegrationTests 2, PostgresIntegrationTests 2, SqliteIntegrationTests 2 |
| This provider can return inserted rows. | 3 | PostgresIntegrationTests 1, SqlServerIntegrationTests 1, SqliteIntegrationTests 1 |
| This provider cannot return updated rows. | 3 | MySqlIntegrationTests 3 |
| This provider cannot return removed rows. | 3 | MySqlIntegrationTests 3 |
| This provider cannot materialise a query into a temporary table. | 3 | SqlServerIntegrationTests 3 |
| This provider cannot return a zero-column result set. | 2 | SqlServerIntegrationTests 1, SqliteIntegrationTests 1 |
| This provider cannot return generated columns on the insert. | 2 | MySqlIntegrationTests 2 |
| This provider cannot both skip conflicting rows and return written rows. | 2 | MySqlIntegrationTests 1, SqlServerIntegrationTests 1 |
| This provider cannot skip conflicting rows. | 1 | SqlServerIntegrationTests 1 |

**Set-operation / join / MERGE gates**

| reason | count | providers |
|---|---|---|
| This provider does not support INTERSECT ALL. | 3 | MySqlIntegrationTests 1, SqlServerIntegrationTests 1, SqliteIntegrationTests 1 |
| This provider does not support EXCEPT ALL. | 3 | MySqlIntegrationTests 1, SqlServerIntegrationTests 1, SqliteIntegrationTests 1 |
| This provider cannot render a general multi-branch MERGE. | 2 | MySqlIntegrationTests 1, SqliteIntegrationTests 1 |
| This provider supports EXCEPT ALL. | 1 | PostgresIntegrationTests 1 |
| This provider does not support FULL JOIN. | 1 | MySqlIntegrationTests 1 |
| This provider supports INTERSECT ALL. | 1 | PostgresIntegrationTests 1 |

**TVF / lateral / cardinality / misc support-flip gates**

| reason | count | providers |
|---|---|---|
| This provider has no lateral/APPLY source, so a correlated APPLY cannot be rendered. | 6 | SqliteIntegrationTests 6 |
| This provider's scalar subqueries do not enforce cardinality (Single/SingleOrDefault are rejected there). | 5 | SqliteIntegrationTests 5 |
| The shared table-valued function test uses SQLite's json_each; MySQL exposes JSON rows through JSON_TABLE with a different shape. | 4 | MySqlIntegrationTests 4 |
| The shared table-valued function test uses SQLite's json_each; SQL Server exposes row-returning JSON through CROSS APPLY OPENJSON instead. | 4 | SqlServerIntegrationTests 4 |
| The shared table-valued function test uses SQLite's json_each; no portable equivalent is configured for PostgreSQL. | 4 | PostgresIntegrationTests 4 |
| This provider supports batches. | 4 | MySqlIntegrationTests 1, PostgresIntegrationTests 1, SqlServerIntegrationTests 1, SqliteIntegrationTests 1 |
| This provider has no native multi-table DELETE. | 2 | SqliteIntegrationTests 2 |
| This provider evaluates AVG over an integer column as an integer. | 2 | SqlServerIntegrationTests 2 |
| This provider's LIKE cannot be made byte-wise. | 1 | SqliteIntegrationTests 1 |
| This provider does not support row locking. | 1 | SqliteIntegrationTests 1 |
| This provider has no TRUNCATE. | 1 | SqliteIntegrationTests 1 |

The two D193-specific gates are the expected pair: the 5 SQL Server positive tuple executions (`This provider has no row-value constructor.`) and the 3 rejection-test skips on tuple-capable providers MySQL/PostgreSQL/SQLite (`This provider supports tuple IN/Contains.`). Every other gate is a pre-existing, D193-unrelated provider capability; no provider is wholesale-skipped.

### Focused D193/TupleIn boundary skips grouped by reason — `boundary-postgap.xml` (total 34 / passed 26 / failed 0 / skipped 8)

Derived from the existing XML/log (`/tmp/D193-evidence/boundary-postgap.xml`, `boundary-postgap.log`); no re-run.
All six providers executed; zero failures.

| reason | count | providers |
|---|---|---|
| This provider has no row-value constructor. | 5 | SqlServerIntegrationTests 5 |
| This provider supports tuple IN/Contains. | 3 | MySqlIntegrationTests 1, PostgresIntegrationTests 1, SqliteIntegrationTests 1 |

These 8 are exactly the same two D193 gates as the focused subset above (5 + 3); they are the intended gated success/rejection cases, not provider-availability skips.

**ACT verdict:** accepted. Evidence completeness closed; all acceptance criteria R1–R7 evidenced; all defects fixed/verified and none open; rv1 re-gather budget exhausted; no product defect established. No re-run was performed.
