# PostgreSQL: raw `ROW(...)` / composite materialization — issue #194 (task D194)

- task: D194
- issue: #194 (https://github.com/AlexeyShirshov/nextorm/issues/194) — OPEN, milestone `1.0.9-rc1`; scope: raw `ROW(...)`/composite materialization through Npgsql (follow-up to #126); title *"Raw-row materialization: PostgreSQL ROW/composite support"*
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: done
- cycle: N=1
- plan revision: r=2
- attempt: n=3/3
- contract: rv=2
- previous contract: rv=1 (superseded, preserved)
- mode: autonomous (collection G1 lane)
- git: no commit/push/merge in this unit; auto-commit of task files only after CHECK/ACT (collection exception)
- milestone: 1.0.9-rc1

## CHECK r2/n=1 — **FAIL** (loop-back to DO n=2)

`pdca-check` rejected r2/n=1. Verdict pointer: `/tmp/D194-evidence/SUMMARY.txt` (CHECK-gather
evidence summary). Build was 0W/0E and the focused D194 suites were green, but the CHECK found the
converter-bypass critical and three secondary defects; the loop-back re-opens D194.2/D194.3:

- **C1 🔴 Critical — converter bypass.** `PostgresDataContext.MapColumnExpression`'s named-composite
  branch (`src/nextorm.postgres/PostgresDataContext.cs:249-264`) intercepts *any* class type before
  `base.MapColumnExpression` resolves `column.Converter` (`RowMapperFactory.cs:60-62`), so
  `[JsonColumn]`/`HasConversion` POCO columns bypass their converter.
- **C3 🟡 — arity validation.** `RawMapperFactory.MapAnonymousTupleColumn` (`RawMapperFactory.cs:302-332`)
  indexes `values[0..args.Length-1]` without checking `values.Length == args.Length`.
- **C6 🟡 — provider leakage.** The `"must be registered with MapComposite<T>"` guard
  (`RawMapperFactory.cs:243-250`) fires for non-PostgreSQL providers on a single non-scalar column.
- **C9 ℹ️ — docs.** `docs/guide/12-raw-sql.md:421-422` + RU mirror state the driver exception is always
  preserved as `InnerException`; a plain SQL-NULL non-nullable item has none.
- **C2/C4/C5** — evidence-or-fix items (metadata-probe catch; excluded conversion exceptions;
  non-`object[]` record value).

## CHECK r2/n=2 — **FAIL** (loop-back to DO n=3/3, final attempt; no 4th)

`pdca-check` rejected r2/n=2. Verdict pointer: `/tmp/D194-evidence/check2/SUMMARY.txt` (build 0W/0E;
core 1614/0/0, postgres 771/0/0, sqlite 1 skipped, sqlserver 697/0/0; coverage line 88.6 % / branch
79.8 %; PG RawRow 30/0/0, PG JsonColumn 7/0/0, CH 107/0/0). The implementation is functionally green,
but the CHECK confirmed correctness/safety defects in the raw-row classification and error handling:

- **#1 (P1) `NamedComposite` eligibility not constrained.** `RawMapperFactory.TryGetRawRowShape`
  (`:230-236`) classified ANY single non-scalar column as `NamedComposite` when
  `fieldType == resultType`, so `Read<int[]>`/`Read<Dictionary<,>>`/provider structs previously
  rejected now route to `GetFieldValue<T>` and can silently succeed; a same-type entity column can be
  hijacked. Fix: require a genuine composite (schema-qualified `GetDataTypeName` + result type not an
  array/collection), else fall through.
- **#2 (P1) named-composite driver failures escape bare.** `MapNamedCompositeColumn` (`:343-359`) had
  no try/catch: a `GetFieldValue<T>` failure or NULL into a non-nullable struct composite escaped as a
  driver exception. Fix: wrap into R194-ERROR `InvalidOperationException` with the driver exception as
  `InnerException`; whole-record `DBNull` still → CLR null via `IsDBNull` first.
- **#4 (P1) guard ordering.** `EnsureNameMappable` (`:449`) ran before the S12
  `TryGetUnresolvableCompositeColumn` guard (`:523-530`), so a mixed unregistered-composite + scalar
  whose composite is a positional record yielded the generic `"... requires a public parameterless
  constructor"` message instead of R194-ERROR. Fix: decide the raw-row shape/error first.
- **#5 (P1) unsupported-metadata misclassification.** `TryGetUnresolvableCompositeColumn` (`:525-530`)
  fired for any column whose `GetFieldType` throws with a non-empty/non-`record` `GetDataTypeName`, so
  an ordinary PG entity no-match read on `hstore`/`ltree`/unknown got a misleading
  `"named composite column (hstore)"` `NotSupportedException`. Fix: the same genuine-composite
  predicate; ordinary no-match behaviour preserved.
- **#6 (P1) covariant array.** `CurrentRecordField` nested-ROW check `value is object[]` matched
  `string[]`/`text[]` via array covariance. Fix: exact `IsSZArray && GetElementType()==object`.
- **#3 (narrow catch, re-gather)** — replace the broad metadata-probe catch with the known
  metadata-rejection set (`InvalidCastException`/`NotSupportedException`/`InvalidOperationException`,
  excluding `ObjectDisposedException`) so provider bugs propagate.
- **#7** — exclude `OperationCanceledException`/`OutOfMemoryException` from the `ConvertField` wrap.
- **#8** — `DataContext.SupportsRawRowColumns` vs R194-COMPAT "no new public API": **kept**, an
  established precedent exists (`SupportsTypedColumnMapping`, see §Durable state).
- **#9/#10 (docs)** — frozen-seam text corrected to `RawMapperFactory.MapNamedCompositeColumn`; EN+RU
  `providers/overview` + `advanced/limitations` no longer imply an anonymous `ROW(...)` needs a
  caller-owned `NpgsqlDataSource` (only named composites do).

## Goal

Make a **PostgreSQL raw `ROW(...)` / composite result materialize into a declared CLR shape** through
Npgsql, and pin the exact materialization, NULL and error behaviour. The raw result path today
(`DataContext.ExecuteRaw(...)` → `ProcedureResult.Read<T>()` → `RawMapperFactory.GetOrBuild<T>`)
supports a scalar column (`RawMapperFactory.IsScalarType`, `RawMapperFactory.cs:300`) or a mapped
entity by column name (`RawMapperFactory.cs:82-96`); a **single record column** is neither, so it
falls into the entity branch and fails with
`NotSupportedException: ExecuteRaw supports mapped entity types and scalar types; …` (`:99-102`) or is
rejected deeper by `SelectExpression.GetDataRecordMethod(readType)` (`SelectExpression.cs:177-281`).
ClickHouse already materializes its native `Tuple(...)` column as `System.Tuple<…>` (`SelectExpression.cs:268-273`
→ `GetValueMI`, consumed by `RawMapperFactory`); PostgreSQL has no equivalent. This task adds the
PostgreSQL raw-row materialization path with a **caller-owned configuration** (approach A below) and
records the outcome for the other providers.

## Acceptance criteria (R194-SHAPE / NULL / ERROR / COMPAT / PROVIDER / DOC / PERF)

Observable and negative cases for each; the shape matrix is normative in §Shape contract.

- **R194-SHAPE** — a raw PostgreSQL statement projecting an anonymous
  `ROW(a, b, …)` (arity 1..7) of scalar-allow-listed fields materializes into the matching
  `System.Tuple<…>` through `ExecuteRaw(...).Read<System.Tuple<…>>()`. The projection must carry a
  **single record column**; multi-scalar result sets keep the existing scalar/entity behaviour
  (`R194-COMPAT`). *Negative case:* `ValueTuple<…>`, arity ≥8 / `Tuple<…,TRest>`, nested `ROW(ROW(...))`,
  empty `ROW()`, named composites without registration, a named composite declared as `System.Tuple`,
  an anonymous `ROW` declared as a DTO/class, multiple record columns in one row, and mixed
  record + scalar columns are **not** materialized in this unit; each yields the `R194-ERROR` guard.
- **R194-NULL** — SQL `NULL` for the whole record column yields CLR `null` (for a nullable declared
  type); a non-null record whose fields are **all** `NULL` yields a **non-null** object with `null`
  fields (never `null`); no `DBNull` ever leaks as a field value; a `NULL` field is **not** substituted
  with `default(T)`. *Negative case:* all-null-fields must not collapse the record to `null`, and a
  `NULL` field must not be replaced by `0`/`""`/`false`/`default`.
- **R194-ERROR** — configuration/unsupported shape surfaces
  `NotSupportedException` with message `"PostgreSQL raw-row materialization is not supported: <reason>."`;
  a data mismatch (record open, field not convertible to the declared field CLR type) surfaces
  `InvalidOperationException` with message `"PostgreSQL raw-row materialization failed: <reason>."` and
  **preserves the inner exception** (`InnerException` set). *Negative case:* neither exception is
  swallowed, wrapped in a bare `Exception`, nor replaced by the generic
  `Cannot get ctor from …` / `… is not supported` message.
- **R194-COMPAT** — multi-scalar raw result sets, scalar raw reads, mapped-entity raw reads, LINQ
  projections, and the ClickHouse `System.Tuple` route are unchanged (byte-for-byte SQL and behaviour);
  `PostgresDataContext.CreateDbConnection` (`:92`) is unchanged; no new public NextORM API; no global
  driver mapper. *Negative case:* no regression in the core/postgres unit suites and the
  PG/CH integration suites (same skip counts).
- **R194-PROVIDER** — PostgreSQL is **implemented**; ClickHouse is **unchanged** (already
  `System.Tuple`); SQL Server is **N/A** (no server-side row/composite type); MySQL/MariaDB/SQLite are
  **not applicable** (no server-side row type); the in-memory context is **N/A**. Each non-PostgreSQL
  disposition is recorded with the reason and (where reachable) an explicit rejection/absence — see
  §Provider applicability matrix.
- **R194-DOC** — EN + RU docs and the specs record the shipped behaviour, the allowed shapes and the
  guards; public docs do **not** link to `docs/specs/**`; the D126 follow-up mapping is corrected (see
  §Docs plan).
- **R194-PERF** — the mandatory acceptance benchmark (7 cases) is run green and within the 4-minute
  budget, and a targeted 10 000-row raw-row probe is compared against a direct Npgsql control; the
  tracked BenchmarkDotNet artifacts are restored afterwards (no artifact churn). See §Perf decision.
- **R194-EVIDENCE** — every DO unit records command + exit code + key numbers + log path; the
  E194-01..E194-12 evidence contract is complete at CHECK; coverage uses the exact CI commands
  (E194-09).

## Plan revision r2 (frozen 2026-10-06; supersedes rv1 shape/config/seam — rv1 preserved below)

`planner` issued a genuine revision after the D194.1 spike surfaced two plan-assumption defects
(`D194-CONFIG-API`, `D194-NULL-DEFAULT`). r2 keeps **every** original acceptance criterion
(R194-SHAPE / NULL / ERROR / COMPAT / PROVIDER / DOC / PERF / EVIDENCE) and the D194.2..D194.6 scope,
and freezes the following decisions. The rv1 contract (`E194-01..E194-12`) is preserved as history and
carried forward at rv2.

- **Goal (unchanged).** A raw PostgreSQL statement projecting an anonymous `ROW(a,b,…)` (arity 1..7,
  scalar item types) or a caller-registered named composite materializes into the matching
  `System.Tuple<…>` / named `T` through `ExecuteRaw(sql).Read<T>()`.
- **Config (approach A).** The caller supplies the configured `NpgsqlDataSource`
  (or `dataSource.CreateConnection()`), through the existing ctors
  `PostgresDataContext.cs:51` / `:77` / `DataContextBuilder.UsePostgres(DbConnection)`.
  `CreateDbConnection` (`PostgresDataContext.cs:92`) unchanged. No new public NextORM API.
  `EnableRecordsAsTuples()` is **optional for S1** (our seam reads `object[]`); it is required only
  for direct typed-driver tuple reads, which is NOT our seam. Named composite S5 requires caller
  `MapComposite<T>`.
- **Seam (frozen).** A single-record-column branch in `RawMapperFactory.GetOrBuild<T>`
  (`src/nextorm.core/DataContext/RawMapperFactory.cs:65`) **before** entity-metadata resolution
  (`:82-96`) / `RowMaterializerBuilder`. Anonymous tuples: typed `GetValue()` of the record column
  returns a null-preserving `System.Object[]` (verified `/tmp/D194-evidence/spike-results.txt:102-108`);
  NextORM validates and constructs `System.Tuple<…>` from it. Registered named `T`: driver typed
  composite accessor built in the raw path by `RawMapperFactory.MapNamedCompositeColumn`
  (not `PostgresDataContext.MapColumnExpression`). Unsupported shapes → guard.
- **NULL/error (frozen).** `IsDBNull` first → whole-record NULL = CLR `null` for
  reference/nullable targets. A SQL NULL in a position whose declared tuple item type is a
  **non-nullable value type** → R194-ERROR `InvalidOperationException`
  (`"PostgreSQL raw-row materialization failed: <reason>."`, preserve `InnerException` when present);
  null accepted only for reference/nullable item types; **never** substitute `default(T)`.
  Unsupported config/shape → `NotSupportedException`
  (`"PostgreSQL raw-row materialization is not supported: <reason>."`). Nested/empty/multi-record/
  mixed/ValueTuple/arity≥8/`Tuple<…,TRest>`/unregistered-named/`Tuple`-declared-named → guards per
  shape contract S3-S13.
- **Cache (frozen).** Extend `RawMapperCacheKey` (`src/nextorm.core/DataContext/MapperCache.cs:25`)
  to capture record-kind + structural tuple arity/item types (record shape is currently NOT
  represented — only the column-name string); ordinary mappings keep identity. Never set
  `Cache=false` / `_dontCache`.
- **Scope (frozen).** S1 (anonymous `System.Tuple` arity 1..7, scalar allow-list fields) + S5
  (caller-registered named composite) implement; S2 unchanged; S3/S4/S6-S13 guard/deferred as
  recorded.

## DO plan (ordered)

- **D194.1** (STEP 0, blocking) — **time-boxed reconnaissance spike** (mandatory; see §Reconnaissance
  decision): probe Npgsql's record/composite read shapes and decide the exact implementation seam and
  configuration. **No production code.** Output: a findings note appended to this file (probe → observed
  type/behaviour) and a frozen shape/config decision.
- **D194.2** (STEP 1, blocked on D194.1) — **implementation** of the PostgreSQL raw-row materialization
  at the seam chosen by D194.1, behind approach A (caller-owned data source + external-connection ctor;
  no new public NextORM API). Guard every unsupported shape with the R194-ERROR contract.
- **D194.3** (STEP 2, blocked on D194.2) — **tests**: unit tests (core/postgres) + PostgreSQL-only
  integration execution tests (DOCKER_HOST), including the shape matrix and the negative/error cases;
  capture the RED→GREEN where a fix is involved.
- **D194.4** (STEP 3, independent after the D194.1 contract freeze) — **docs**: EN + RU limitations,
  `12-raw-sql`, PG guide, specs gap-analysis, and the D126 status mapping correction.
- **D194.5** (STEP 4, blocked on D194.2) — **perf**: acceptance benchmark (7 cases, ≤4 min) + the
  targeted 10 000-row raw-row probe vs the direct Npgsql control; restore BDN artifacts.
- **D194.6** (STEP 5) — **evidence/status**: build 0W/0E, CRLF audit, scope audit, E194 rows, ledger
  finalization. No commit (orchestrator auto-commits after CHECK/ACT).

r2 re-bases D194.2..D194.6 on the frozen §Plan revision r2 decisions; their original scope and
acceptance criteria are unchanged. D194.1 (r1 spike) is complete; D194.2 starts at r2/n=1.

## Shape contract (normative)

| # | raw PG input | declared result type | disposition | closure |
|---|---|---|---|---|
| S1 | anonymous `ROW(a)`..`ROW(a..g)`, arity 1..7, all fields scalar-allow-listed | matching `System.Tuple<…>` | **implement** | test |
| S2 | multi-scalar row (N independent columns), no record column | scalar / mapped entity | **unchanged** (R194-COMPAT) | regression |
| S3 | `ROW(...)` arity 1..7 | `System.ValueTuple<…>` | guard/deferred | error test |
| S4 | `ROW(...)` arity ≥8, or `Tuple<…,TRest>` | `System.Tuple<…,TRest>` | guard/deferred | error test |
| S5 | named composite type, registered by the caller via explicit `MapComposite<T>` | the named `T` | **implement** | test |
| S6 | named composite type, **not** registered | the named `T` | guard | error test |
| S7 | named composite type | `System.Tuple` | guard | error test |
| S8 | anonymous `ROW(...)` | DTO/class | guard | error test |
| S9 | nested `ROW(ROW(...), …)` | any | guard/deferred | error test |
| S10 | empty `ROW()` | any | guard/deferred | error test |
| S11 | multiple record columns in one row | any | guard/deferred | error test |
| S12 | mixed record + scalar columns | any | guard/deferred | error test |
| S13 | any S5..S12 field outside the scalar allow-list | — | guard | error test |

- **Scalar allow-list (record fields).** Same families the raw path already materializes as scalars:
  `int`/`long`/`short`/`byte`/`bool`/`float`/`double`/`decimal`, `string`, `DateTime`,
  `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`, `Guid`, `byte[]`, enum, and a nullable of these
  (`RawMapperFactory.IsScalarType`, `RawMapperFactory.cs:300-314`; `SelectExpression.GetDataRecordMethod`,
  `SelectExpression.cs:177-281`). Anything outside the allow-list inside a record → `R194-ERROR` guard.
- **ValueTuple / arity ≥8.** `TypeFacts.IsTupleType` recognizes only `System.Tuple<>`..`Tuple<,,,,,,>`
  (arity 1..7, `Visitors/TypeFacts.cs:88-96`); `IsValueTupleType` mirrors it for `ValueTuple` (`:102-110`).
  The non-generic `Tuple` and the arity-8 `Tuple<…,TRest>` driver shape are intentionally not
  recognized. Deferred, not silently accepted.
- **Named composite.** Only an explicit caller registration (`Npgsql` `MapComposite<T>`, approach A)
  makes a named composite materializable; an unregistered named composite is a guard, not a silent
  fallback.

## NULL semantics (normative)

- Whole record column `NULL` (e.g. `NULL::record`, or `SELECT (NULL::mytype)`) → CLR `null` for a
  nullable/reference declared type; the record is **not** opened.
- Non-null record with **all** fields `NULL` → a **non-null** object whose fields are `null`
  (`Tuple` items `null`). Distinguishing "record is null" from "record exists, fields are null" is part
  of the contract.
- `DBNull` is never exposed as a field value; fields surface as CLR `null` / the typed value.
- No default substitution: a `NULL` field stays `null`, never `default(T)` (`0`, `""`, `false`).

## Error contract (normative)

- **Config / unsupported shape** (S3, S4, S6, S7, S8, S9, S10, S11, S12, S13 and any non-PostgreSQL
  reachability guard):
  `NotSupportedException` with message
  `"PostgreSQL raw-row materialization is not supported: <reason>."`.
  `<reason>` names the concrete cause (e.g. `ValueTuple result types are not supported`,
  `a composite type mapped to System.Tuple is not supported`, `nested ROW values are not supported`).
- **Data mismatch** (record opened but a field cannot be converted to the declared CLR field type):
  `InvalidOperationException` with message
  `"PostgreSQL raw-row materialization failed: <reason>."`, **`InnerException` preserved** (the driver
  exception is not discarded).
- No generic `NotSupportedException("Property '…' … type … which is not supported")` and no
  `QueryPreparationException("Cannot get ctor from …")` may surface for a shape this contract covers.

## Configuration approach A (normative)

- **Caller-owned `NpgsqlDataSource`.** The caller builds the `NpgsqlDataSource`
  (enabling records / registering `MapComposite<T>` as required); the data source is **not** created or
  cached by nextorm and there is **no global mapper**.
- **External-connection ctor.** The caller passes `dataSource.CreateConnection()` to the existing
  context ctor `NextORM.Postgres.PostgresDataContext(DbConnection, DataContextBuilder)`
  (`src/nextorm.postgres/PostgresDataContext.cs:51`) — or the version-aware twin `:77` — or through
  `DataContextBuilder.UsePostgres(DbConnection)`
  (`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs:33-38`).
- **`CreateDbConnection` unchanged** (`PostgresDataContext.cs:92`) — nextorm still builds a plain
  `NpgsqlConnection` for the connection-string path.
- **No new public NextORM API**; no change to the connection-string path; no ambient/static driver
  configuration.

## Reconnaissance decision

**Spike mandatory before implementation (D194.1, time-boxed).** The exact Npgsql read shapes and the
seam cannot be decided from the current code alone: `RawMapperFactory`/`SelectExpression` show where a
single record column is *rejected*, but not which Npgsql entry point surfaces an anonymous `ROW` or a
registered composite (and whether a `NpgsqlDataSource` with records enabled is required at all). The
spike runs exactly these probes and records the observed type/behaviour:

1. **Connection mode** — bare `NpgsqlConnection` vs `NpgsqlDataSource` **without** `EnableRecords()` vs
   `NpgsqlDataSource` **with** `EnableRecords()` vs `NpgsqlDataSource` with `MapComposite<T>`.
2. **SQL probes** — `SELECT ROW(1::integer,'a'::text)`, `SELECT ROW(NULL::integer,NULL::text)`,
   `SELECT NULL::record`, `SELECT ROW(ROW(1::integer,'a'::text),2)`.
3. **Reader probes** on the record column — `IsDBNull`, `GetDataTypeName`, `GetFieldType`, `GetValue`
   runtime type, `GetFieldValue<object[]>`, `GetFieldValue<Tuple<…>>`, `GetFieldValue<ValueTuple<…>>`,
   and a named composite registered vs unregistered.
4. **Existing NextORM raw entry point** — `ExecuteRaw(sql).Read<T>()` on the same shapes, to pin the
   current failure mode and the seam (`RawMapperFactory.GetOrBuild<T>` /
   `RowMapperFactory.GetReaderAccessor` / `PostgresDataContext.MapColumnExpression`).

The spike produces a findings note (appended to this file) and freezes: the supported shapes (S1/S5),
the configuration requirement, and the exact implementation seam. **No production code in the spike.**

## Test strategy + commands

- **Unit (core / postgres), focused only (inner loop).** Build once when compilable inputs change, then
  filter:
  - `dotnet build nextorm.slnx -c Debug` (or `dotnet build tests/nextorm.core.tests -c Debug` when only
    that project changed).
  - `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~<RawRow/RowMaterializer filter>"`
  - `dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~<RawRow filter>"`
  - The raw-path seam `RawMapperFactory.GetOrBuild<T>` is `internal` (assembly has
    `InternalsVisibleTo("nextorm.core.tests")`), and `PostgresDataContext.MapColumnExpression` is public;
    both are unit-testable with a fake `DbDataReader` exposing one record column.
- **Boundary run (DO→CHECK), exactly once.** One full `dotnet test tests/nextorm.core.tests -c Debug`
  (and, when the postgres unit surface changed, one `dotnet test tests/nextorm.postgres.tests -c Debug`).
- **PostgreSQL execution tests (container-backed; not optional).** Load
  `.opencode/skills/running-integration-tests/SKILL.md` first, then:
  ```bash
  DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
    dotnet run --project tests/nextorm.integration.tests -c Debug -- -class nextorm.integration.tests.<PostgresRawRowTests> -noColor
  ```
  A run where PostgreSQL reports `skipped` (no `DOCKER_HOST`) is **not** passing evidence; if the Podman
  socket is missing, start the machine and rerun (skill §Troubleshooting).
- **Cross-provider guards.** ClickHouse `ClickHouseIntegrationTests` (the `System.Tuple` route must be
  unchanged) and, where reachable, the SQL Server/MySQL/MariaDB/SQLite unit suites for the "not
  applicable" evidence.
- **Build gate.** `dotnet build nextorm.slnx -c Debug` = 0 Warning / 0 Error (warnings-as-errors).
- **Coverage** uses the exact CI commands in E194-09.

## Provider applicability matrix

| provider | disposition | reason / evidence |
|---|---|---|
| PostgreSQL | **implement** | server-side `ROW(...)`/composite types exist; Npgsql surfaces them; this task adds the raw materialization. |
| ClickHouse | **unchanged** (already shipped) | `SelectExpression.GetDataRecordMethod` routes `IsTupleType` to `GetValueMI` (`SelectExpression.cs:268-273`); the driver returns `System.Tuple<…>`; integration `TupleColumn_ShouldProjectAsSystemTuple` (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:1293`). |
| SQL Server | **N/A** | no server-side row/composite type; nothing to materialize. |
| MySQL / MariaDB | **not applicable** | no server-side row type; `#126` only renders a flat `(a, b)` comparison operand, never a result-set column. |
| SQLite | **not applicable** | no server-side row type. |
| in-memory | **N/A** | no server/driver row types. |

## Docs plan (D194.4; EN + RU parity, no `docs/specs/**` links from public docs)

- `docs/advanced/limitations.md` + `docs/ru/advanced/limitations.md` — replace/qualify the raw-`ROW`
  limitation with the shipped PostgreSQL behaviour and the guards.
- `docs/guide/12-raw-sql.md` + `docs/ru/guide/12-raw-sql.md` — document materializing a raw
  `ROW(...)`/composite into `System.Tuple<…>` / a caller-registered composite, and the configuration
  (caller-owned `NpgsqlDataSource` + external-connection ctor).
- `docs/guide/provider-specific/postgresql.md` + RU mirror — PostgreSQL-specific section for raw
  rows/composites.
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` — item 22 (and the summary/ledger row) update:
  raw-row materialization moves from "Open" to shipped on PostgreSQL; remove the `#194` open reference.
- **D126 status mapping correction** — `docs/specs/status/rc1-126-tuple-ctor-1.md:361` currently swaps
  the follow-up issues (`#193` raw-row materialization / `#194` tuple `IN`/`Contains`); the verified
  GitHub mapping is `#193` = *Tuple IN/Contains translation and execution across providers* and
  `#194` = *Raw-row materialization: PostgreSQL ROW/composite support*. Correct that line (status file
  only; no production code).
- No public page links to `docs/specs/**`; renumber no guide series unless a page is added/removed.

## Perf decision

- **Mandatory acceptance run** (task touches the raw materialization path):
  `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
  — must report **7** selected cases, **0** failures, and complete within **4 minutes** (external shell
  and BDN `Global total time` both recorded). Baseline/reference:
  `docs/specs/performance/acceptance-benchmarks.md`.
- **Targeted probe**: a focused raw-row benchmark over **10 000 rows** compared against a **direct
  Npgsql control** (same SQL read without nextorm) to confirm the added materialization does not
  introduce per-row reflection/unbounded allocation. A new `[BenchmarkCategory]` (e.g. `D194RawRow`) is
  added for it and must not carry the `acceptance` category (so it cannot perturb the 7-case gate).
- **Artifact hygiene**: BenchmarkDotNet writes tracked reports under
  `BenchmarkDotNet.Artifacts/results/*`; after the runs restore them with
  `git checkout -- BenchmarkDotNet.Artifacts/results` and remove any new report files, leaving
  `git status` clean of artifact churn.

## Evidence contract (rv=2; E194-01..E194-12 carried from rv1 + E194-R2-SEAM/NULL/CACHE/PROBE)

Unit command **U** (filtered inner loop) and PostgreSQL integration command **I** (container-backed
boundary) referenced by the r2 rows:

- **U**: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~RawRowMaterializer"`
- **I**: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -class nextorm.integration.tests.PostgresRawRowTests -noColor`

| row | applicability | command / action | required result | actual result | owner | rv |
|---|---|---|---|---|---|---|
| E194-01 | always (preflight) | read the anchors (`TypeFacts.cs:71-113`, `PostgresDataContext.cs:40-93,235-246`, `RawMapperFactory.cs:65-102`, `RowMapperFactory.cs:52-143,263-279`, `SelectExpression.cs:177-281`, `ProcedureResult.cs:125-174`, `DataContext.cs:635,832`); create this status file; set the D194 row `in-progress` in `collection-1.0.9-rc1-tail.md` | call sites resolved; status file present, CRLF | PASS — anchors resolved (see §Roslyn recon); `rc1-tail-194-pg-row-composite-1.md` created; D194 row `in-progress` | coder | rv2 |
| E194-02 | PostgreSQL (spike) | D194.1 spike probes exactly as in §Reconnaissance decision (connection mode × SQL × reader accessor × existing raw entry point) | every probe has an observed type/behaviour; a frozen shape/config/seam decision | **PASS** — `dotnet build tests/nextorm.integration.tests -c Debug` 0W/0E; `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class nextorm.integration.tests.PostgresRowSpikeTests -noColor` exit 0 (Total 1, Failed 0, Skipped 0). Every probe observed; table + implications in §D194.1 spike findings; `/tmp/D194-evidence/spike-results.txt` (186 lines), run log `/tmp/D194-evidence/spike-run.log`. Config requirement corrected (`EnableRecordsAsTuples`, not `EnableRecords`) → **proposed r2** | coder | rv2 |
| E194-03 | PostgreSQL | implement the PostgreSQL raw-row path at the D194.1 seam under configuration approach A | S1/S5 materialize; S3/S4/S6..S12/S13 guarded; no new public NextORM API; `CreateDbConnection` unchanged | **PASS** (D194.2, 2026-10-06) — record branch in `RawMapperFactory.GetOrBuild<T>` + named-composite branch in `RawMapperFactory.MapNamedCompositeColumn`; `dotnet build nextorm.slnx -c Debug` exit 0 / 0W / 0E (`/tmp/D194-evidence/r2-build.log`); S1/S5 exercised green by D194.3 (E194-04/06/08); unsupported shapes guarded per §Error contract; no new public API; `CreateDbConnection` unchanged | coder | rv2 |
| E194-04 | core + postgres | focused unit tests (core/postgres) with the allowed filters; capture RED before a fix if applicable | exit 0 after implementation; RED captured where a bug fix is involved | **PASS** (D194.3; refreshed D194.6 r=2/n=3 final post-amendment) — `[dotnet, test, tests/nextorm.core.tests, -c, Debug, --no-build, --filter, "FullyQualifiedName~RawRowMaterializer"]` exit 0, Total **45** / Failed 0 / Skipped 0 (`/tmp/D194-evidence/final/core-rawrow.log`; every durable pin from D194-GUARD-S12/ARITY/hijack/narrow-catch/amendment #1-#5 present in the 45; full core rose 1619→1624, so the earlier 54 was a broader historical filter, not a test loss); scope filter 41/0/0 (`d194-3-core-scope.log`); RED→GREEN D194-GUARD-S12: RED 1 failed (`d194-3-core-red.log`) → GREEN 27/27 | coder | rv2 |
| E194-05 | core (postgres if changed) | one boundary full `dotnet test tests/nextorm.core.tests -c Debug` (+ postgres if changed) | exit 0, 0 failed; key totals recorded | **PASS** (D194.6, r=2/n=3 final post-amendment boundary) — `[dotnet, test, tests/nextorm.core.tests, -c, Debug, --no-build]` exit 0: Total **1624** / Failed 0 / Skipped 0 (`/tmp/D194-evidence/final/test-core.log`); `[dotnet, test, tests/nextorm.postgres.tests, -c, Debug, --no-build]` exit 0: Total **771** / Failed 0 / Skipped 0 (`final/test-postgres.log`); `tests/nextorm.sqlite.tests` exit 0: Total **1078** / Succeeded 1077 / Failed 0 / Skipped 1 (`final/test-sqlite.log`); `tests/nextorm.sqlserver.tests` exit 0: Total **697** / Failed 0 / Skipped 0 (`final/test-sqlserver.log`) | coder | rv2 |
| E194-06 | PostgreSQL (container-backed) | PostgreSQL-only container-backed execution tests (`DOCKER_HOST=…`, `-class …PostgresRawRowTests`) | exit 0, 0 failed, **0 skipped**; shape matrix + NULL + error cases PASS | **PASS** (D194.6, r=2/n=3 final post-amendment boundary) — `DOCKER_HOST=… [dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.PostgresRawRowTests, -noColor]` exit 0: Total **36** / Failed 0 / **Skipped 0** / Errors 0 (`/tmp/D194-evidence/final/pg-rawrow.log`); converter-regression `PostgresJsonColumnTests` exit 0: Total **7** / Failed 0 / Skipped 0 (`final/pg-json.log`); `PostgresIntegrationTests` exit 0: Total **617** / Failed 0 / Skipped **25** / Errors 0 (`final/pg-integration.log`) — 25 skips are all functional (21 other-provider-rejection skips (13 LOB + Batch + CreateTableAs + ExceptAll + 2×ExecuteProcedure + Insert_Returning + IntersectAll + TempTableSource) + 4 CH-only `TableFunction_*`), no environment/container skip | coder | rv2 |
| E194-07 | cross-provider (CH + non-PG N/A) | cross-provider guards: ClickHouse `ClickHouseIntegrationTests` (System.Tuple route unchanged); SQL Server/MySQL/MariaDB/SQLite "not applicable" evidence; in-memory N/A | CH 0 failed/0 skipped with the tuple test still PASS; non-PG dispositions recorded | **PASS** (D194.6, r=2/n=3 final post-amendment boundary) — `DOCKER_HOST=… [dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.ClickHouseIntegrationTests, -noColor]` exit 0: Total **107** / Failed 0 / **Skipped 0** / Errors 0 (`/tmp/D194-evidence/final/ch-integration.log`; tuple regression `TupleColumn_ShouldProjectAsSystemTuple` included and PASS); SQL Server/MySQL/MariaDB/SQLite/in-memory N/A recorded in §Provider applicability matrix | coder | rv2 |
| E194-08 | PostgreSQL | error-contract tests: every guard shape yields the exact `NotSupportedException` message; a data-mismatch case yields `InvalidOperationException` with `InnerException` preserved | exact message prefixes; inner preserved; no swallowed/generic exception | **PASS** (D194.3/D194.6 r=2/n=3 final post-amendment) — guard shapes + exact `NotSupportedException`/`InvalidOperationException` message prefixes + `InnerException` preservation covered by `RawRowMaterializerTests` **45/0/0** (`/tmp/D194-evidence/final/core-rawrow.log`) and PG `PostgresRawRowTests` **36/0/0** (`final/pg-rawrow.log`); no swallowed/generic exception | coder | rv2 |
| E194-09 | always (CI coverage) | **coverage** — exact CI commands, verbatim from `.github/workflows/dotnet.yml` (Test step `:42-45`, Report step `:53-59`): `mkdir -p tests/coverage`; `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`; `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`. Thresholds `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75` (`:26-28`; enforce `:60-77`, hard-fail on `main` only). Scope `coverage.settings.xml`: `.*nextorm\.(core\|sqlite\|postgres\|sqlserver)\.dll$`; exclude `ExcludeFromCodeCoverage`/`GeneratedCode`/`Obsolete`. See §Coverage command E194-09. | line ≥ 85 %, branch ≥ 75 % (hard-fail on `main` only); report produced at `tests/coverage/report/Summary.txt` | **PASS** (D194.6, r=2/n=3 final boundary, `DOCKER_HOST` set) — collect exit 0: Total **8606** / Failed 0 / Succeeded 8416 / Skipped 190; report exit 0 → `Line coverage: **88.6%**` (46644/52590) · `Branch coverage: **79.8%**` (24299/30416), both above thresholds. Artifact bound to the final tree: `tests/coverage/coverage.cobertura.xml` sha256 `2be0ba4cb2d3c23eaa731317686de324b6a5d2f942ce6a243eac80097edf9d32` (2026-10-06 16:34:11) → `tests/coverage/report/Summary.txt` sha256 `21fa919073507c261accb7b5a5a4201746b51711dfe62370a0b5f77d2c06b7cb` (generated 16:34:29; report `Cobertura.xml` sha256 `40f81e092d35a350f01e3d2d66de9c19822d2ff7685c50361baddeffb5cec447`). Logs `/tmp/D194-evidence/final/coverage-collect.log`, `final/coverage-report.log` | coder | rv2 |
| E194-10 | EN+RU docs + specs | EN+RU docs + specs gap-analysis + D126 status mapping correction; `dotnet docfx docs/docfx.json` | EN/RU parity; no `docs/specs/**` link from public docs; docfx exit 0 (or only pre-existing warnings) | **PASS** (D194.4, 2026-10-06) — EN+RU pages updated (`docs/advanced/limitations.md`, `docs/guide/12-raw-sql.md`, `docs/guide/provider-specific/postgresql.md` + RU mirrors, `providers/overview`, `scalar-functions/06-arrays`); gap-analysis item 22 + `rc1-126-tuple-ctor-1.md:361` mapping corrected; `dotnet docfx docs/docfx.json` exit 0 — 2 warnings (pre-existing `sourcegenerator` duplicate-source) / 0 errors (`/tmp/D194-evidence/d194-6-docfx.log`); no public-doc `docs/specs/**` link (`rg` clean) | coder | rv2 |
| E194-11 | PostgreSQL + benchmark host | perf: acceptance 7 cases ≤4 min; targeted 10 000-row raw-row probe vs direct Npgsql control; BDN artifacts restored | 7 selected / 0 failures / ≤4 min; probe recorded; `git status` free of BDN artifact churn | **PASS** (D194.5, 2026-10-06) — `[dotnet, run, --project, benchmarks/nextorm.benchmark, -c, Release, --, --anyCategories=acceptance]` exit 0; external wall **52.16 s**, BDN `Global total time` **45.26 s**; **7** cases / **0** failures: `Nextorm_Count` 2.540 ms / 334.38 KB; `Nextorm_GroupByCount` 71.08 ms / 50.05 MB; `Nextorm_Cached` 1.907 ms / 547.71 KB; `Prepared_ToList` 1,105.1 μs / 76.14 KB; `Cached_ToList` 2,108.5 μs / 541.01 KB; `Cached_PlanOnly_Param` 632.3 μs / 464.86 KB; `Nextorm_Cached_ToListAsync` 2.086 ms / 565.5 KB; cached/prepared ratio **1.91** (baseline 1.87, +2 %; alloc ratio 7.11 vs baseline 7.42); artifacts restored (`git status` free of `BenchmarkDotNet.Artifacts`; log `/tmp/D194-evidence/r2-acceptance.log`). **Re-run after the n=3/d pre-verdict amendments (D194.6 final post-amendment boundary):** `[dotnet, run, --project, benchmarks/nextorm.benchmark, -c, Release, --, --anyCategories=acceptance]` exit 0; external wall **66 s**, BDN `Global total time` **47.71 s / 7 executed**; **7** cases / **0** failures: `Nextorm_Count` 3.018 ms / 334.38 KB; `Nextorm_GroupByCount` 79.47 ms / 50.07 MB; `Nextorm_Cached` 2.468 ms / 547.71 KB; `Prepared_ToList` 1,205.3 μs / 76.14 KB; `Cached_ToList` 2,603.0 μs / 541.01 KB; `Cached_PlanOnly_Param` 726.0 μs / 464.86 KB; `Nextorm_Cached_ToListAsync` 2.523 ms / 565.5 KB; cached/prepared ratio **2.16** (baseline 1.87; +15.5 %, below the 20 % investigation threshold 2.24), alloc ratio **7.11** (baseline 7.42); BDN artifacts restored and no new reports (`git status` clean of `BenchmarkDotNet.Artifacts`; log `/tmp/D194-evidence/final/acceptance.log`) | coder | rv2 |
| E194-12 | always (finalize) | finalize: `dotnet build nextorm.slnx -c Debug` 0W/0E; `roslyn`/`git diff --stat` scope audit; CRLF audit; ledger + status | build 0W/0E; only planned files changed; every edited file CRLF; no commit | **PASS** (D194.6, r=2/n=3 final post-amendment boundary) — `[dotnet, build, nextorm.slnx, -c, Debug]` exit 0 / **0 Warning(s) / 0 Error(s)** (`/tmp/D194-evidence/final/build.log`); scope audit **18 modified + 3 new** planned files only (`git status --short`); all pre-amendment-edited files CRLF-OK (no lone LF in `RawRowMaterializerTests.cs`, `RawMapperFactory.cs`, `PostgresRawRowTests.cs`, status file); throwaway probes absent (`D194TypeProbeTests.cs`/`PostgresRowSpikeTests.cs`/`PostgresRawRowProbeTests.cs` gone); cache-mutation check `git diff -G"Cache = false"` and `git diff -G_dontCache` both **empty**; BDN artifact churn none; no commit | coder | rv2 |
| E194-R2-SEAM | PostgreSQL record column; R194-R2-SEAM (S1 + S5 implemented) | **U** + **I**: unit mapper test with a fake one-record-column reader (`GetDataTypeName == "record"`, `GetValue` = `object[]`) and the PG `PostgresRawRowTests` S1/S5 matrix | single-record-column branch in `RawMapperFactory.GetOrBuild<T>` before entity metadata; S1 `System.Tuple<…>` constructed from `object[]`; S5 via `RawMapperFactory.MapNamedCompositeColumn`; no new public API; `CreateDbConnection` unchanged | **PASS** (D194.2/D194.3; re-verified D194.6 r=2/n=3) — record branch before entity metadata in `RawMapperFactory.GetOrBuild<T>`; S1 `System.Tuple<…>` from `object[]`, S5 via `RawMapperFactory.MapNamedCompositeColumn`; build 0W/0E (`do-n3c/build.log`); exercised by core 45/0/0 (`/tmp/D194-evidence/final/core-rawrow.log`) + PG 36/0/0 (`final/pg-rawrow.log`) | coder | rv2 |
| E194-R2-NULL | PostgreSQL record column; R194-NULL / R194-ERROR | **U** + **I**: fake reader with `IsDBNull=true` (whole-record null ⇒ CLR `null`), `object[]{null,…}` with a non-nullable value item ⇒ `InvalidOperationException`, nullable/ref item ⇒ `null`; PG matrix `NULL::record`, `ROW(NULL,NULL)` | whole-record NULL ⇒ CLR `null`; SQL NULL in a non-nullable value-type item ⇒ `InvalidOperationException` `"PostgreSQL raw-row materialization failed: …"` with no `default(T)` substitution; nullable/ref item ⇒ `null` | **PASS** (D194.2/D194.3; re-verified D194.6 r=2/n=3) — `IsDBNull`-first whole-record NULL ⇒ CLR `null`; non-nullable value item + SQL NULL ⇒ `InvalidOperationException` (`"PostgreSQL raw-row materialization failed: …"`) with no `default(T)` substitution; nullable/ref item ⇒ `null`; covered by `RawRowMaterializerTests` 45/0/0 (`/tmp/D194-evidence/final/core-rawrow.log`) + PG 36/0/0 (`final/pg-rawrow.log`) | coder | rv2 |
| E194-R2-CACHE | core; R194-R2-CACHE | **U**: build the raw mapper twice for the same record shape and once per distinct tuple signature; assert one cache entry per shape and no `_dontCache` mutation | `RawMapperCacheKey` captures record kind + structural tuple arity/item types; ordinary (non-record) mappings keep their existing key identity; no `Cache=false`/`_dontCache` | **PASS** (D194.2/D194.3; re-verified D194.6 r=2/n=3) — `RawMapperCacheKey` captures record kind + structural tuple arity/item types; ordinary mappings keep key identity; asserted in `RawRowMaterializerTests` (45/0/0, `/tmp/D194-evidence/final/core-rawrow.log`); no `Cache=false`/`_dontCache` | coder | rv2 |
| E194-R2-PROBE | PostgreSQL + benchmark host; R194-R2-PERF | **I** + benchmark: 10 000-row raw-row probe compared against a direct Npgsql control (same SQL, no nextorm); no per-row reflection/unbounded allocation | probe recorded; added materialization comparable to the direct control; BDN artifacts restored (`git status` free of artifact churn) | **PASS** (D194.5, 2026-10-06) — throwaway `PostgresRawRowProbeTests` (deleted before ACT; `[dotnet, build, tests/nextorm.integration.tests, -c, Debug]` 0W/0E); `DOCKER_HOST=… [dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.PostgresRawRowProbeTests, -noColor]` exit 0 (Total 1, Failed 0, **Skipped 0**); 10 000-row `select row(i::integer,'a'::text) … generate_series(1,10000)`, warmup 3 / 5 measured runs: NextORM `ExecuteRaw(sql).Read<Tuple<int,string>>()` mean **64.392 ms** / **2 826 432 B** (282.64 B/row), direct Npgsql control (`GetValue`→`object[]`→`Tuple.Create` into `List`) mean **35.587 ms** / **1 762 712 B** (176.27 B/row); allocated bytes identical across repetitions for both paths (bounded, no per-row reflection); logs `/tmp/D194-evidence/r2-probe.log`, `/tmp/D194-evidence/r2-probe-run.log`; re-verified D194.6 r=2/n=3 final post-amendment — `PostgresRawRowProbeTests.cs` absent from the final tree (no throwaway probe in `git status`), acceptance re-run 7/0 wall 66 s ratio 2.16 (`final/acceptance.log`), artifacts restored | coder | rv2 |

### Coverage command E194-09 (extracted verbatim from `.github/workflows/dotnet.yml`)

The CI `Test with coverage` step (`dotnet.yml:42-45`), verbatim:

```bash
mkdir -p tests/coverage
dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"
```

The CI `Coverage report` step (`dotnet.yml:53-59`), verbatim:

```bash
dotnet tool run reportgenerator \
  -reports:tests/coverage/coverage.cobertura.xml \
  -targetdir:tests/coverage/report \
  -reporttypes:"Html;TextSummary;Cobertura" \
  -riskhotspotassemblyfilters:"+nextorm.*"
```

**Thresholds** (`dotnet.yml:26-28`): `MIN_LINE_COVERAGE: 85`, `MIN_BRANCH_COVERAGE: 75`. The CI
`Enforce coverage thresholds` step (`dotnet.yml:60-77`) reads `Line coverage` / `Branch coverage` from
`tests/coverage/report/Summary.txt` with
`grep -oP 'Line coverage: \K[0-9.]+'` / `grep -oP 'Branch coverage: \K[0-9.]+'` and hard-fails (exit 1)
only when `GITHUB_REF = refs/heads/main`; on other branches it emits `::warning::`.

**Scope** (`coverage.settings.xml`): product assemblies only —
`ModulePath: .*nextorm\.(core|sqlite|postgres|sqlserver)\.dll$` (`:12`); test assemblies excluded.
Attributes excluded from coverage: `System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute`,
`System.CodeDom.Compiler.GeneratedCodeAttribute`, `System.ObsoleteAttribute` (`:20-22`). Note: MySQL,
MariaDB and ClickHouse assemblies are **not** in the coverage scope.

## Risks / assumptions

- **Assumption (spike-resolved):** an anonymous PG `ROW` can be materialized through the documented
  Npgsql surface without a named composite registration; if the spike shows it cannot, S1 is re-scoped
  to "register a composite type first" and the docs/config contract changes (replan, not a silent
  widening).
- **Assumption:** `MapComposite<T>` requires a caller-owned `NpgsqlDataSource`; the connection-string
  path (`CreateDbConnection`) cannot register composites — hence approach A and the external-connection
  ctor.
- **Risk:** the raw mapper cache key (`RawMapperCacheKey`, `MapperCache.cs`) is keyed by provider type,
  result type, `oneColumn`, column names and naming-convention type; a record column's shape must be
  fully captured by that key or two distinct record shapes could alias. The implementation must extend
  the key/signature if the record shape is not already represented; **no** `Cache = false` mutation
  (sticky `_dontCache` leak) is permitted.
- **Risk:** `RowMaterializerBuilder` positionally binds the longest ctor when its parameter count
  matches the projection (`RowMaterializerBuilder.cs:303-310`) and rejects a non-constructible type with
  `QueryPreparationException: Cannot get ctor from …` (`:264-267`); the record path must be branched
  **before** that, otherwise the error contract regresses to a misleading message.
- **Risk:** `System.Tuple` is a reference type; `SelectExpression` sets `Nullable = true` for reference
  types (`:143`), which is what makes the whole-`NULL` → `null` behaviour reachable, but the
  all-fields-null case must still produce a non-null tuple (R194-NULL).
- **Assumption:** MySQL/MariaDB/SQLite/SQL Server have no server-side row type, so their disposition is
  a recorded "not applicable" with no code path; only the PG provider is touched.
- **Out of scope / deferred:** `ValueTuple`, arity ≥8 / `Rest`, nested/empty/multi-record/mixed shapes,
  and named composites mapped to `System.Tuple` (all guarded, not implemented) — see §Shape contract.
- **Perf risk:** opening a record per row may allocate; the 10 000-row probe must show it is bounded and
  comparable to the direct Npgsql control (no per-row reflection).

## Roslyn recon (D194, recorded at r=1/n=1 — no production code)

- **`NextORM.Core.TypeFacts`** (`src/nextorm.core/Visitors/TypeFacts.cs`):
  - `IsSingleColumnProjection` — `:71`; the doc comment (`:62-70`) deliberately **excludes** a tuple
    from the single-column families.
  - `IsTupleType(Type)` — `:88`; recognizes `System.Tuple<>` .. `System.Tuple<,,,,,,>` (arity 1..7).
    It does **not** recognize the non-generic `Tuple` or `Tuple<…,TRest>` (arity 8). `refs`:
    `SelectExpression.cs:268`, `QueryCommand.QueryPreparer.cs:417`, `:537`, `TypeFacts.cs:69` (doc),
    `:113` (via `IsTupleLike`).
  - `IsValueTupleType(Type)` — `:102` (`ValueTuple<>`..`ValueTuple<,,,,,,>`, arity 1..7).
  - `IsTupleLike(Type)` — `:113` (either family).
- **`NextORM.Postgres.PostgresDataContext` external-connection ctor** —
  `NextORM.Postgres.PostgresDataContext.PostgresDataContext(System.Data.Common.DbConnection, NextORM.Core.DataContextBuilder)`
  at `src/nextorm.postgres/PostgresDataContext.cs:51` (version-aware twin at `:77`; private base at `:82`).
  Callers: `PostgresDataContextOptionsBuilderExtensions.UsePostgres(this DataContextBuilder, DbConnection)`
  (`src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs:35`),
  `tests/nextorm.integration.tests/CoreApiContractTests.cs:89`,
  `tests/nextorm.integration.tests/PostgresSpecificTests.cs:135` (manual `NpgsqlConnection`). The context
  does not dispose a caller-supplied connection (doc `:45-50`).
- **Raw result path (where a single record column would be materialized):**
  - Public entry: `NextORM.Core.DataContext.ExecuteRaw(string, params IReadOnlyList<ProcedureParameter>)`
    at `src/nextorm.core/DataContext/DataContext.cs:832` → `ProcedureResult`
    (`ProcedureResult.cs:23`).
  - `NextORM.Core.ProcedureResult.Read<T>()` at `ProcedureResult.cs:125` (async `ReadAsync<T>` at `:160`;
    enumerator/cursor path at `:360,:375`) → `NextORM.Core.RawMapperFactory.GetOrBuild<T>(DataContext, DbDataReader)`
    at `RawMapperFactory.cs:65`.
  - `RawMapperFactory.GetOrBuild<T>` (`:65`) branches on `IsScalarType` (`:70,:300`); a non-scalar
    single-column record falls into the entity branch (`:82-96`) → `entityMeta.Properties.Count == 0`
    → `Unsupported(resultType)` `NotSupportedException` (`:86-87,:99-102`); the entity path calls
    `context.MapColumnExpression` (`:96`).
  - `NextORM.Core.DataContext.MapColumnExpression` virtual at `DataContext.cs:635` →
    `RowMapperFactory.MapColumn`; PostgreSQL override
    `NextORM.Postgres.PostgresDataContext.MapColumnExpression` at `PostgresDataContext.cs:235`
    (handles `Range<>`/multirange, else `base`).
  - `NextORM.Core.RowMapperFactory.GetOrBuildRaw<TResult>` at `RowMapperFactory.cs:263` →
    `Build`/`Build<TResult>` (`:281,:294`) → `NextORM.Core.RowMaterializerBuilder.Build` at
    `RowMaterializerBuilder.cs:41` → `BuildCore` at `:256`, `BuildProjectionItems` at `:72`,
    `BuildNonItemValue` at `:194`, `BuildMemberInit` at `:362`.
  - `NextORM.Core.RowMapperFactory.GetReaderAccessor(SelectExpression, Expression, Type)` at
    `RowMapperFactory.cs:118`; `NextORM.Core.SelectExpression.GetDataRecordMethod(Type)` at
    `SelectExpression.cs:177`; a `System.Tuple` type hits the `IsTupleType` branch (`:268-273`) →
    `GetValueMI` (untyped `GetValue`, caller-casts). That is the ClickHouse route; PostgreSQL's record
    value is not a `System.Tuple`, so casting fails.
- **Unit-testable seam:** **yes.** `RawMapperFactory.GetOrBuild<T>` (internal) and
  `RowMapperFactory.GetReaderAccessor` are reachable from `nextorm.core.tests`
  (`InternalsVisibleTo`), and `PostgresDataContext.MapColumnExpression` is public; the raw mapper takes
  an `IDataRecord`/`DbDataReader`, so a fake reader with one record column exercises the path without a
  database. The PostgreSQL integration execution tests remain the boundary proof.

## D194.1 spike findings (r=1/n=1 — reconnaissance, no production code)

Throwaway probe `tests/nextorm.integration.tests/PostgresRowSpikeTests.cs` (delete before ACT) ran against
the real PG container. Command (exact argv):
`[dotnet, build, tests/nextorm.integration.tests, -c, Debug]` → exit 0, 0W/0E; then
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`
`[dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.PostgresRowSpikeTests, -noColor]`
→ exit 0, Total 1 / Failed 0 / Skipped 0. Results table: `/tmp/D194-evidence/spike-results.txt` (186 lines);
full run log: `/tmp/D194-evidence/spike-run.log`.

### Observed per-configuration behaviour (SQL → accessor)

| configuration | `GetFieldType`/`GetValue` of anonymous `ROW(1,'a')` | `GetFieldValue<Tuple<int,string>>` | `GetFieldValue<Tuple<int?,string>>` on `ROW(NULL,NULL)` | named composite `GetFieldValue<PtComposite>` (registered) |
|---|---|---|---|---|
| bare `NpgsqlConnection` | `System.Object[]`, `object[]{1,"a"}` | `InvalidCastException` | `InvalidCastException` | `InvalidCastException` (`DataTypeName=-.-`, no resolver) |
| `NpgsqlSlimDataSourceBuilder` plain | throws `InvalidCastException` ("not supported for DataTypeName 'record'") | `InvalidCastException` | `InvalidCastException` | `InvalidCastException` (unmapped) |
| `NpgsqlSlimDataSourceBuilder.EnableRecords()` | `System.Object[]`, `object[]{1,"a"}` | `InvalidCastException` | `InvalidCastException` | `InvalidCastException` (unmapped) |
| `NpgsqlDataSourceBuilder` plain (full) | `System.Object[]`, `object[]{1,"a"}` (**record resolver on by default**) | `InvalidCastException` | `InvalidCastException` | `InvalidCastException` (unmapped) |
| `NpgsqlDataSourceBuilder.EnableRecordsAsTuples()` | `GetFieldType=object[]`, `GetValue=object[]{…}` but typed read returns the tuple | **`(1, a)`** | `Tuple<int?,string>` → `(, )` = `(null, null)`; `Tuple<int,string>` → `(0, )` | `InvalidCastException` (unmapped) |
| `NpgsqlDataSourceBuilder.MapComposite<PtComposite>(schema.pt_composite)` only | as plain full | `InvalidCastException` | `InvalidCastException` | **`PtComposite{1,"a"}`** |
| `EnableRecordsAsTuples()` + `MapComposite<T>` | typed read returns the tuple | **`(1, a)`** | `(, )` (`Tuple<int?,string>`) / `(0, )` (`Tuple<int,string>`) | **`PtComposite{1,"a"}`** |

Additional observed shapes:
- **`NULL::record`** — `IsDBNull=true`, `GetFieldType=object[]`, `GetValue=DBNull`; every
  `GetFieldValue<…>` throws `InvalidCastException: Column 'v' is null.` (whole-record null must be
  taken via `IsDBNull` before any typed read).
- **`ROW(NULL,NULL)`** (non-null record, all fields null) — `IsDBNull=false`; untyped `GetValue` is
  `object[]{null,null}`; under `EnableRecordsAsTuples` `GetFieldValue<Tuple<int?,string>>` = `(null,null)`
  while `GetFieldValue<Tuple<int,string>>` substitutes `0` for the SQL `NULL`. `EnableRecords` alone
  (object[]) preserves `null` elements.
- **nested `ROW(ROW(1,'a'),2)`** — untyped `GetValue` = `object[]{object[]{1,"a"},2}`; under
  `EnableRecordsAsTuples` `GetFieldValue<Tuple<int,string>>` throws
  `ArgumentException: Object of type 'System.Object[]' cannot be converted to type 'System.Int32'`.
- **Named composite** — requires `MapComposite<T>`; no `EnableRecords*` needed; unregistered read throws
  `InvalidCastException: Reading as '…PtComposite' is not supported for fields having DataTypeName
  'schema.pt_composite'`. A registered composite read as `System.Tuple` throws `InvalidCastException`
  (S7 guard, driver-side).

### Existing NextORM raw entry point (`ExecuteRaw(...).Read<T>()`)

| probe | observed |
|---|---|
| `Read<Tuple<int,string>>` (all four SQL shapes, provider ctx) | `NotSupportedException: ExecuteRaw supports mapped entity types and scalar types; Tuple`2 is neither. Register a mapping with From<Tuple`2>() first, or read the result as a scalar.` |
| `Read<object[]>` | `ArgumentException: Interface maps for generic interfaces on arrays cannot be retrieved.` |
| `Read<PtComposite>` / `Read<SpikeDto>` | `InvalidOperationException: None of the result-set columns (v) matches a mapped property of …` |
| same on a `EnableRecordsAsTuples` data-source-backed `PostgresDataContext` | identical failures — NextORM's mapper build rejects **before** the driver read, so the caller data source alone changes nothing. |

### Frozen design implications (input to PLAN)

1. **Configuration correction (forces replan).** `NpgsqlDataSourceBuilder` (full) has **no**
   `EnableRecords()`; that API exists only on `NpgsqlSlimDataSourceBuilder` and maps a record to
   `object[]`. The tuple route the S1 shape needs is `NpgsqlDataSourceBuilder.EnableRecordsAsTuples()`
   (maps a record to `ValueTuple`/`Tuple`), which the plan currently does not name. The full builder
   already maps records to `object[]` by default (`RecordTypeInfoResolverFactory`), so `GetValue`/
   `GetFieldType` on the full-builder path are `object[]` regardless of `EnableRecordsAsTuples`; only the
   typed `GetFieldValue<Tuple<…>>` reaches the tuple resolver. Approach A must therefore state
   `EnableRecordsAsTuples()` for S1.
2. **`EnableRecords` alone does NOT suffice for anonymous ROW → Tuple** — it yields `object[]`. The
   `Tuple` materialization is driver-side through the tupled resolver, or NextORM-built from `object[]`.
3. **Named composites require `MapComposite<T>` and nothing else** — S5 is reachable with
   `MapComposite<T>(schema-qualified name)` alone; unregistered is the S6 driver `InvalidCastException`,
   which NextORM must translate to the R194-ERROR guard.
4. **NULL / default-substitution is a real contract tension.** `Tuple<int,string>` +
   `ROW(NULL,NULL)` yields `(0, null)` — a `NULL` value-type field is substituted with `default`
   (driver behavior), violating R194-NULL ("no default substitution"). Only a declared field type with
   `Nullable<T>` (or an object[]-based mapper that assigns CLR `null`) preserves it. PLAN must pin
   whether nullable item types are required / how non-nullable value-type fields handle SQL `NULL`
   (throw R194-ERROR vs default) — this cannot be decided from the current plan.
5. **Seam.** The ClickHouse `SelectExpression.GetDataRecordMethod` → `GetValueMI` route is **not**
   reusable as-is: PostgreSQL `GetValue` on the full builder returns `object[]`, not `System.Tuple`.
   The viable seams are (a) a new `GetFieldValue<System.Tuple<…>>` accessor for the record column (driver
   tupled resolver; requires `EnableRecordsAsTuples`) or (b) a NextORM tuple builder fed from the
   `object[]` returned by `GetValue` (controls NULL semantics, needs only the default full-builder record
   mapping). Branch the record case in `RawMapperFactory.GetOrBuild<T>` (or
   `RowMapperFactory.GetReaderAccessor`) **before** entity metadata resolution / `RowMaterializerBuilder`.
6. Nested/whole-null/unsupported shapes surface driver exceptions (`ArgumentException`,
   `InvalidCastException`), not the R194-ERROR `NotSupportedException`, so guards must be added
   NextORM-side.

**Proposed plan revision r2 (STOP for PLAN).** Amend §Shape contract S1/§Configuration approach A to
`EnableRecordsAsTuples`, refine §NULL semantics for non-nullable value-type tuple items, and freeze the
seam (5). No production code was written; r=1/n=1 is retained because no replan was executed here.

## Durable state

- cycle `N=1` · plan revision **`r=2`** · attempt **`n=3/3`** · contract/evidence revision **`rv=2`**
  (previous contract `rv=1` superseded, preserved) — carried in this file; a session/`task_id` reset
  never resets `r`, `n` or the defect history.
- DO ledger:

| unit | STEP | status | evidence |
|---|---|---|---|
| D194.1 | 0 spike | **done** (2026-10-06T09:47Z) | E194-02 — spike `PostgresRowSpikeTests` exit 0 (1 passed/0 failed/0 skipped); results `/tmp/D194-evidence/spike-results.txt`; run log `/tmp/D194-evidence/spike-run.log`; **proposed r2 (STOP for PLAN)** |
| D194.2 | 1 implementation | **done** (2026-10-06) | frozen seam landed (record branch in `RawMapperFactory.GetOrBuild<T>` + `PostgresDataContext.MapColumnExpression` named-composite branch); exercised green by D194.3. |
| D194.3 | 2 tests | **done** (2026-10-06T10:19Z) | E194-04/E194-06/E194-08 — core `RawRowMaterializerTests` 27/27 with RED→GREEN for S12; full core 1606 passed / 0 failed / 0 skipped; PG `PostgresRawRowTests` 18 passed / 0 failed / **0 skipped**; logs `/tmp/D194-evidence/d194-3-*.log` |
| D194.4 | 3 docs | **done** (2026-10-06) | E194-10 — EN+RU `limitations`/`12-raw-sql`/PG guide + `providers/overview` + `scalar-functions/06-arrays`; gap-analysis item 22; D126 mapping corrected; docfx exit 0 (2 pre-existing warnings / 0 errors; `/tmp/D194-evidence/d194-6-docfx.log`) |
| D194.5 | 4 perf | **done** (2026-10-06T10:23Z) | E194-11/E194-R2-PROBE — acceptance exit 0, 7 cases / 0 failures, wall 52.16 s / BDN 45.26 s, cached/prepared 1.91; throwaway PG probe exit 0 (1/0/0), NextORM 64.392 ms / 2 826 432 B vs direct Npgsql 35.587 ms / 1 762 712 B over 10 000 rows; BDN artifacts restored (`git status` clean); logs `/tmp/D194-evidence/r2-acceptance.log`, `r2-probe.log`, `r2-probe-run.log` |
| D194.6 | 5 evidence/status | **done** (2026-10-06T16:37Z, r=2/n=3 final post-amendment boundary) | E194-04/05/06/07/08/09/11/12 — build exit 0/0W/0E; core 1624/0/0, postgres 771/0/0, sqlite 1077/0/1 (Total 1078), sqlserver 697/0/0; PG RawRow 36/0/0, PG Json 7/0/0, PG common 617/0/25 (21 other-provider-rejection + 4 CH-only TableFunction, all functional); CH full 107/0/0; coverage collect 8606/0/8416/190, line 88.6 % / branch 79.8 %, cobertura sha256 2be0ba4c… → Summary.txt sha256 21fa9190…; acceptance 7/0, wall 66 s, BDN 47.71 s, ratio 2.16; focused core 45/0/0; scope 18 modified + 3 new planned only, CRLF-OK; cache-mutation diffs empty; throwaway probes absent; BDN artifacts restored; logs `/tmp/D194-evidence/final/*` |

> **D194.6 review-gate addendum (2026-10-06T11:44Z).** CHECK r2/n=3 opened an evidence-completeness gate
> (not a product defect): +1 PostgreSQL execution test for the S9 nested-ROW applicability
> (`TupleEnabledDataSource_NestedRow_ShouldGuardNotDataMismatch`, `PostgresRawRowTests` 36 → **37/0/0**,
> 0 skipped) and the §Evidence manifest (final tree). Production is unchanged (test-only delta); `r=2`,
> `n=3/3`, `rv=2` retained.

> **DO ledger final (2026-10-06).** All units **D194.1–D194.6** are `done`; no unit is `blocked` or
> `pending`. This ledger is frozen for the cycle; `r=2`, `n=3/3`, `rv=2` are final and ACT re-opens no
> unit.

### Defect history (stable keys)

| defect key | state | observed r/n | fixes applied | evidence / note |
|---|---|---|---|---|
| D194-CONFIG-API | **resolved by r2** | r=1 / n=1 → resolved r=2 / n=1 | 1 | Plan/approach A named `NpgsqlDataSourceBuilder.EnableRecords()`; Npgsql 10.0.3 has no such member on the full builder (compile error CS1061). **r2 decision:** the seam reads the driver's `object[]` for anonymous `ROW` (full `NpgsqlDataSourceBuilder` maps records to `object[]` by default), so `EnableRecordsAsTuples()` is **optional for S1**; named composite S5 requires caller `MapComposite<T>`. Approach A is unchanged (caller-owned `NpgsqlDataSource` + existing external-connection ctor). |
| D194-NULL-DEFAULT | **resolved by r2** | r=1 / n=1 → resolved r=2 / n=1 | 1 | `GetFieldValue<Tuple<int,string>>` on `ROW(NULL,NULL)` returns `(0, null)` — SQL `NULL` substituted with `default(int)`. **r2 decision:** `IsDBNull` first → whole-record `NULL` = CLR `null`; a SQL `NULL` in a non-nullable value-type tuple item → `InvalidOperationException` (R194-ERROR), never `default(T)`; null only for reference/nullable item types. Read from the null-preserving `object[]`, not the driver tuple resolver. |
| — | none | r=1 / n=1 → r=2 / n=1 | 0 | No code defect; the two entries above were plan-assumption defects surfaced by the D194.1 spike and are closed by r2. |
| D194-GUARD-S12 | **resolved** (D194.3) | observed r=2 / n=1 → resolved r=2 / n=1 | 1 | Mixed named-composite + scalar columns fell through to the entity path (`InvalidOperationException: None of the result-set columns …`) instead of the R194-ERROR guard. Fix: `RawMapperFactory.TryGetRawRowShape` matches the driver field type against the declared non-scalar result type in the multi-column branch. RED `/tmp/D194-evidence/d194-3-core-red.log` (1 failed); GREEN `/tmp/D194-evidence/d194-3-core-green.log` (27/27). |
| D194-CONVERTER-BYPASS | **resolved** (r=2/n=2) | observed r=2/n=1 → resolved r=2/n=2 | 1 | 🔴 Critical. `PostgresDataContext.MapColumnExpression` named-composite branch hijacked *any* class column before `column.Converter` was resolved, so `[JsonColumn]`/`HasConversion` POCO columns bypassed their converter. **Fix:** the named-composite accessor moved out of the provider into `RawMapperFactory.MapNamedCompositeColumn` (`src/nextorm.core/DataContext/RawMapperFactory.cs:343-359`), so ordinary LINQ class columns keep `RowMapperFactory` converter resolution. PRE-fix RED: `PostgresJsonColumnTests` 2 failed (`/tmp/D194-evidence/json-pre-fix.log`); POST-fix GREEN 7/7 (`json-post-fix.log`) + `PostgresIntegrationTests.JsonColumn_*` 3/3 (`json-post-fix-common.log`); new in-scope pin `ConverterBackedJsonColumn_ShouldStillMaterializeViaConverter` PASS (`/tmp/D194-evidence/do-n2b-pg-rawrow.log`). |
| D194-ARITY | **resolved** (r=2/n=2) | observed r=2/n=1 → resolved r=2/n=2 | 1 | 🟡. The tuple accessor indexed `values[0..args.Length-1]` without verifying arity: a wider ROW silently dropped fields, a narrower one threw a bare `IndexOutOfRangeException`. **Fix:** `RawRowValue.ValidateRecord` (`src/nextorm.core/DataContext/RawMapperFactory.cs:709-725`) validates `System.Object[]` + `values.Length == expectedArity` before indexing, called from `MapAnonymousTupleColumn` (`:378-384`). Tests: core `RecordWiderThanDeclaredTuple_ShouldThrowContractedInvalidOperation` / `RecordNarrowerThanDeclaredTuple_ShouldThrowContractedInvalidOperation` PASS (`/tmp/D194-evidence/do-n2b-core-rawrow.log`); PG `RecordWiderThanDeclaredTuple_ShouldThrowContractedError` / `RecordNarrowerThanDeclaredTuple_ShouldThrowContractedError` PASS (`do-n2b-pg-rawrow.log`). |
| D194-PROV-NONPG-DIAG | **resolved** (r=2/n=2) | observed r=2/n=1 → resolved r=2/n=2 | 1 | 🟡. The `"must be registered with MapComposite<T>"` diagnostic fired for non-PostgreSQL providers on a single non-scalar column, replacing the previous `Unsupported`/entity path. **Fix:** `DataContext.SupportsRawRowColumns` capability gate (default false; PostgreSQL overrides true at `src/nextorm.postgres/PostgresDataContext.cs:227-231`), read through `DataContext.RawRowColumnsSupported` and guarding the whole raw-row detection/diagnostic in `RawMapperFactory.GetOrBuild` (`:89`). Test: core `NonPostgresProvider_SingleNonScalarColumn_ShouldKeepTheOrdinaryPath` PASS (`/tmp/D194-evidence/do-n2b-core-rawrow.log`). |
| D194-MIXED-UNREG-COMPOSITE | **resolved** (r=2/n=2) | observed r=2/n=2 → resolved r=2/n=2 | 1 | 🟡. A named composite column cast to an **unregistered** type mixed with a scalar column fell through to the entity path (`InvalidOperationException: None of the result-set columns (v, n) matches a mapped property of PtComposite.`) instead of the R194-ERROR guard. **Fix:** in `RawMapperFactory.BuildEntitySelectList` (`:527-533`), when no column matched and `context.RawRowColumnsSupported`, `TryGetUnresolvableCompositeColumn` (`:296-329`) detects an unresolvable non-`record` column and throws the contracted `NotSupportedException`. Only the already-failing read is re-typed, so successful entity reads are unaffected. RED `/tmp/D194-evidence/probe-mixed-unregistered.log` (Total 30 / Failed 1, generic entity error); GREEN `/tmp/D194-evidence/do-n2b-pg-rawrow.log` (Total 30 / Failed 0 / Skipped 0). |
| D194-DOC-INNER | **resolved** (r=2/n=2) | observed r=2/n=1 → resolved r=2/n=2 | 1 | ℹ️. EN+RU docs stated the driver exception is always preserved as `InnerException`; a plain SQL-NULL non-nullable item has none. **Fix:** docs now say "when one exists" (`docs/guide/12-raw-sql.md`, `docs/ru/guide/12-raw-sql.md`); pinned by core `NonNullableValueTypeItemWithSqlNull_ShouldThrowNeverDefault` and PG `NonNullableValueTypeItemWithSqlNull_ShouldThrowNeverDefault` asserting `InnerException` is null (`/tmp/D194-evidence/do-n2b-core-rawrow.log`, `do-n2b-pg-rawrow.log`). |
| D194-NAMED-COMPOSITE-HIJACK | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #1 (P1). `RawMapperFactory.TryGetRawRowShape` classified **any** single non-scalar column as `NamedComposite` when `fieldType == resultType`, so `Read<int[]>`/`Read<Dictionary<,>>` (and a same-type entity column) routed to `GetFieldValue<T>` and silently succeeded. **Fix:** require `IsGenuineNamedComposite` (schema-qualified data type name + declared type not an array/collection). Durable pins: core `SingleNonScalarColumn_IntArray_ShouldNotHijackAsNamedComposite`, `SingleNonScalarColumn_Dictionary_ShouldNotHijackAsNamedComposite` (`/tmp/D194-evidence/do-n3b/core-rawrow.log`). |
| D194-NAMED-COMPOSITE-WRAP | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #2 (P1). The named-composite accessor read `GetFieldValue<T>` bare, so a driver failure / SQL NULL into a non-nullable struct composite escaped as a bare driver exception. **Fix:** `RawRowValue.ReadNamedComposite<T>` wraps into R194-ERROR `InvalidOperationException` with the driver exception as `InnerException` (whole-record `DBNull` still → CLR null first). Durable pin: PG `NonNullableStructCompositeSqlNull_ShouldWrapWithInnerPreserved` (`/tmp/D194-evidence/do-n3b/pg-rawrow.log`). |
| D194-GUARD-ORDER | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #4 (P1). `EnsureNameMappable` (generic "requires a public parameterless constructor") ran before the S12 guard, so a mixed unregistered-composite + scalar whose composite is a positional record surfaced the generic ctor message. **Fix:** the raw-row shape/error is decided in `RawMapperFactory.BuildEntitySelectList` before `EnsureNameMappable`. Durable pin: PG `MixedUnregisteredNamedComposite_PositionalRecord_ShouldGuardNotCtorMessage`. |
| D194-UNSUPPORTED-METADATA | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #5 (P1). `TryGetUnresolvableCompositeColumn` fired for any unresolvable column with a dotted data type name, so an ordinary PG entity no-match read over an unmapped type got a misleading `"named composite column (...)"` `NotSupportedException`. **Fix:** the shared genuine-composite predicate (schema-qualified name), which keeps the ordinary no-match behaviour. Durable pin: PG `UnmappedColumnType_OrdinaryNoMatchRead_ShouldKeepTheEntityError` — `hstore` resolves through the driver (`public.hstore` → `Dictionary<string,string?>`), so the entity `"None of the result-set columns …"` error is preserved (`/tmp/D194-evidence/do-n3b/pg-rawrow.log`). |
| D194-COVARIANT-ARRAY | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #6 (P1). `RawRowValue.ConvertField`'s nested-ROW check `value is object[]` matched `string[]`/`text[]` via array covariance. **Fix:** exact `value.GetType().IsSZArray && GetElementType() == typeof(object)`. Durable pin: core `CovariantArrayRecordField_WithMismatchedItemType_ShouldBeDataMismatchNotNestedRow` (data-mismatch `InvalidOperationException`, not nested-ROW `NotSupportedException`). |
| D194-METADATA-CATCH | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #3 (narrow catch). The metadata-probe catch was too broad. **Fix:** `IsMetadataRejection` accepts only `InvalidCastException`/`NotSupportedException`/`InvalidOperationException`, excluding `ObjectDisposedException`; driver/connection errors propagate. Durable pins: core `DisposedReaderMetadataProbe_ShouldPropagate` + `MetadataProbeFailure_ShouldSurfaceTheContractAndNotSwallowDriverErrors` (`/tmp/D194-evidence/do-n3b/core-rawrow.log`). |
| D194-CANCELLATION-WRAP | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #7. `RawRowValue.ConvertField` wrapped every conversion failure. **Fix:** the catch filter excludes `OperationCanceledException`/`OutOfMemoryException` (and the named-composite read filter likewise). Durable pin: core `CancelledConversion_ShouldPropagateUnwrapped`. |
| D194-DOC-DATASOURCE | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #10 (docs). EN+RU `providers/overview` + `advanced/limitations` implied an anonymous `ROW(...)` needs a caller-owned `NpgsqlDataSource`. **Fix:** docs now state only named composites require it (`docs/providers/overview.md`, `docs/advanced/limitations.md` + RU mirrors). |
| D194-SEAM-DOC | **fixed** (r=2/n=3) | observed r=2/n=2 → fixed r=2/n=3 | 1 | #9 (docs). Frozen-seam documentation named `PostgresDataContext.MapColumnExpression`. **Fix:** corrected to `RawMapperFactory.MapNamedCompositeColumn` (§Seam text + E194-03/E194-R2-SEAM rows). |
| D194-RAWROW-API | **kept** (decision, #13) | r=2 / n=3 | 0 | `DataContext.SupportsRawRowColumns` vs R194-COMPAT "no new public API": **kept** — it follows the established protected-extensibility precedent `DataContext.SupportsTypedColumnMapping` (`DataContext.cs:664`, internal seam `:668`, overridden `SqlServerDataContext.cs:558`). `SupportsRawRowColumns` is `protected virtual` (`DataContext.cs:677`, internal seam `:681`, overridden `PostgresDataContext.cs:231`); documented as consistent with the existing API, not a new public surface. |
| D194-UNRESOLVABLE-EXC-DISCRIMINATOR | **fixed** (r=2/n=3, pre-verdict amendment #1, same class as #5) | observed r=2/n=3 → fixed r=2/n=3 | 1 | The unresolvable-column probe used "DataTypeName contains a dot" as the composite signal, but Npgsql emits dotted names for every non-`pg_catalog` schema-qualified type (`public.hstore`, `public.ltree`, domains/enums), so an unresolvable non-composite was misclassified. **Fix:** the driver exception shape is the discriminator — `TryGetFieldType` now surfaces the probe exception and `TryGetUnresolvableCompositeColumn` classifies a named composite only on `InvalidCastException`; an unresolvable non-composite (`NotSupportedException`, e.g. `ltree`) keeps the established `InvalidOperationException("None of the result-set columns …")`. `IsGenuineNamedComposite` keeps the dotted-name test because the exception discriminator is only available when `GetFieldType` fails (the registered-composite positive path succeeds and has no exception); noted, not changed. Durable pins: core `UnresolvableNonCompositeColumn_OrdinaryNoMatchRead_ShouldKeepTheEntityError`; PG `UnmappedLtreeColumn_OrdinaryNoMatchRead_ShouldKeepTheEntityError`. |
| D194-NAMED-COMPOSITE-DISPOSED | **fixed** (r=2/n=3, pre-verdict amendment #2) | observed r=2/n=3 → fixed r=2/n=3 | 1 | `RawRowValue.ReadNamedComposite<T>` wrapped every read failure, so an `ObjectDisposedException` (an `InvalidOperationException`) surfaced as a misleading `InvalidOperationException("PostgreSQL raw-row materialization failed: …")` instead of the real fault. **Fix:** the catch filter also excludes `ObjectDisposedException` (alongside `OperationCanceledException`/`OutOfMemoryException`), consistent with the metadata probes; only genuine materialization mismatches are wrapped. Durable pin: core `DisposedNamedCompositeRead_ShouldPropagateUnwrapped`. |
| D194-METADATA-NARROW | **fixed** (r=2/n=3, pre-verdict amendment #3) | observed r=2/n=3 → fixed r=2/n=3 | 1 | `IsMetadataRejection` treated any `InvalidOperationException` (not `ObjectDisposedException`) as a metadata verdict, swallowing real faults. **Fix:** narrowed to the genuine metadata-not-available signals `InvalidCastException`/`NotSupportedException`; a plain `InvalidOperationException` now propagates. `RecordReader.ThrowOnDataTypeName` now throws `NotSupportedException` (a genuine rejection). Durable pin: core `MetadataProbeInvalidOperation_ShouldPropagateNotBeSwallowed`. |
| D194-COLLECTION-EXCLUSION | **fixed** (r=2/n=3, pre-verdict amendment #4) | observed r=2/n=3 → fixed r=2/n=3 | 1 | `IsGenuineNamedComposite`'s blanket `typeof(IEnumerable).IsAssignableFrom` excluded a caller-registered composite that merely implements `IEnumerable`. **Fix:** narrowed to `IsArray` + `System.Collections.IDictionary` (the shapes array covariance / provider dictionaries would misroute). Durable pin: core `SingleNonScalarColumn_CustomEnumerableComposite_ShouldNotBeBlanketRejected`. |
| D194-NULLABLE-STRUCT-COMPOSITE | **fixed** (r=2/n=3, pre-verdict amendment #5) | observed r=2/n=3 → fixed r=2/n=3 | 1 | `fieldType == resultType` could never match `Read<PtStruct?>` (the driver reports `PtStruct`), leaving `MapNamedCompositeColumn`'s `Nullable.GetUnderlyingType` branch dead and routing the read to the generic entity error. **Fix:** the eligibility predicate (single- and multi-column) now unwraps `Nullable.GetUnderlyingType(resultType)`, and `MapNamedCompositeColumn` lifts the getter to the declared nullable type so the whole-record-NULL condition branches share a type. **#2 decision: APPLIED** (small and safe), not deferred. Durable pins: core `NullableStructComposite_Registered_ShouldRouteToNamedComposite`; PG `NullableStructComposite_Registered_ShouldMaterialize` (incl. whole-record NULL → CLR null). |

## Evidence manifest (final tree)

**Gate disposition.** The third `pdca-check` raised no product defect; it required (1) an execution test for
the S9 nested-ROW applicability under a caller-owned `NpgsqlDataSource` with `EnableRecordsAsTuples()`, and
(2) an explicit final-tree manifest binding every acceptance criterion and every E194 row to a test anchor /
command / log. Both are complete below. **No production code changed and no defect was exposed**, so no fix
was applied; `r=2`, `n=3/3`, `rv=2` are unchanged (no replan, no new attempt).

**Final-tree delta vs the tree that produced the earlier `final/*` logs.** The only source delta is one new
test method in `tests/nextorm.integration.tests/PostgresRawRowTests.cs` (test-only; **no production assembly
and no core-unit test changed**). The build and the PostgreSQL `PostgresRawRowTests` logs below were re-run on
the final tree; the core/postgres/sqlite/sqlserver unit logs and the coverage/acceptance artifacts are bound to
production assemblies identical in the final tree (coverage scope excludes test assemblies, so the line/branch
percentages are unchanged by the added test).

### S9 nested-ROW disposition (step 1)

- **Test:** `tests/nextorm.integration.tests/PostgresRawRowTests.cs:358` —
  `TupleEnabledDataSource_NestedRow_ShouldGuardNotDataMismatch`.
- **Shape:** caller-owned `NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString).EnableRecordsAsTuples().Build()`
  → external-connection `PostgresDataContext` (existing ctor), SQL
  `select row(row(1::integer, 'a'::text), 2::integer) as v`, declared `Tuple<int, int>`.
- **Observed (final tree):** the contracted nested-ROW guard is **reachable and fires** — `NotSupportedException`
  with prefix `"PostgreSQL raw-row materialization is not supported:"` and reason `"nested ROW values are not
  supported"`; it is **not** wrapped as a data-mismatch `InvalidOperationException`.
- **Why:** the seam reads the untyped `GetValue` of the record column, which (Npgsql 10.0.3, D194.1 spike
  `/tmp/D194-evidence/spike-results.txt:117,142,167`) stays a null-preserving `object[]` even when the typed
  tuple resolver is enabled, and the nested ROW element stays a nested `object[]`; the exact `SZArray`/`object`
  check in `RawRowValue.ConvertField` (`src/nextorm.core/DataContext/RawMapperFactory.cs:872`) therefore matches.
  **Disposition: applicable; no product change.**
- **Evidence:** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`
  `[dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.PostgresRawRowTests, -noColor]`
  exit **0**, Total **37** / Failed 0 / Skipped 0 (`/tmp/D194-evidence/final/pg-rawrow.log`).

### Acceptance criteria → test anchors (final tree)

Every anchor below was verified against the current tree (Roslyn member locations in
`RawRowMaterializerTests.cs` / `PostgresRawRowTests.cs`).

| criterion | requirement | anchor(s) |
|---|---|---|
| R194-SHAPE | S1 anonymous `Tuple` arity 1..7 | core `tests/nextorm.core.tests/RawRowMaterializerTests.cs:30,:34,:38,:42,:47,:53,:59`; PG `tests/nextorm.integration.tests/PostgresRawRowTests.cs:29,:33,:38,:43,:49,:55,:61` |
| R194-SHAPE | scalar allow-list widest (enum/byte[]/DateOnly/TimeOnly/TimeSpan/DateTimeOffset/float; short/nullable-decimal; widening) | core `RawRowMaterializerTests.cs:71,:87,:65` |
| R194-SHAPE | S1 through the tupled resolver (external data source) | PG `PostgresRawRowTests.cs:343` |
| R194-SHAPE | S5 caller-registered named composite materializes | PG `PostgresRawRowTests.cs:163`, `:305`; core `RawRowMaterializerTests.cs:452` |
| R194-NULL | whole-record `NULL` → CLR `null` | core `RawRowMaterializerTests.cs:96`; PG `PostgresRawRowTests.cs:74` |
| R194-NULL | all-fields-null → non-null object with `null` items (no `DBNull` leak, no collapse to null) | core `RawRowMaterializerTests.cs:106,:118`; PG `PostgresRawRowTests.cs:78,:88` |
| R194-NULL | `NULL` value-type item → `InvalidOperationException`, never `default(T)` (InnerException when present / null for plain SQL NULL) | core `RawRowMaterializerTests.cs:129`; PG `PostgresRawRowTests.cs:97` (asserts `InnerException` null at `:108`) |
| R194-NULL | whole/partial named-composite `NULL` | PG `PostgresRawRowTests.cs:250,:305`; core `RawRowMaterializerTests.cs:452` |
| R194-ERROR | unsupported shapes → exact `NotSupportedException` prefix | core `RawRowMaterializerTests.cs:244,:248,:262,:274,:285,:296,:308,:319,:333,:347,:364`; PG `PostgresRawRowTests.cs:114,:118,:122,:135,:139,:144,:191,:550,:579,:583` |
| R194-ERROR | data mismatch → exact `InvalidOperationException` prefix, `InnerException` preserved | core `RawRowMaterializerTests.cs:140,:153,:172,:184,:196`; PG `PostgresRawRowTests.cs:384,:388,:274` |
| R194-ERROR | n=3d exception-discrimination amendments (#1 exception-shape discriminator, #2 disposed, #3 narrow `IsMetadataRejection`, #4 collection exclusion, #5 nullable struct) | core `RawRowMaterializerTests.cs:435,:495,:227,:419,:452`; PG `PostgresRawRowTests.cs:527,:305` |
| R194-COMPAT | S2 multi-scalar unchanged (mapped entity) | PG `PostgresRawRowTests.cs:465`; non-PostgreSQL ordinary path core `RawRowMaterializerTests.cs:375` |
| R194-COMPAT | converter/LINQ/JSON unchanged (converter not hijacked) | PG `PostgresRawRowTests.cs:220`; `PostgresJsonColumnTests` 7/0/0 (`final/pg-json.log`); `PostgresIntegrationTests` 617/0/25 (`final/pg-integration.log`) |
| R194-COMPAT | `CreateDbConnection` unchanged; no global driver mapper; no new public API | `git diff -- src/nextorm.postgres/PostgresDataContext.cs` adds only `SupportsRawRowColumns` (`:231`), `CreateDbConnection` (`:92`) untouched; `rg 'MapComposite|NpgsqlDataSourceBuilder' src` → only the guard string `RawMapperFactory.cs:261` |
| R194-PROVIDER | PostgreSQL implemented; ClickHouse unchanged; SQL Server/MySQL/MariaDB/SQLite/in-memory N/A | CH `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:1293` (`TupleColumn_ShouldProjectAsSystemTuple`), 107/0/0 (`final/ch-integration.log`); dispositions §Provider applicability matrix; non-PG path core `RawRowMaterializerTests.cs:375` |
| R194-DOC | EN+RU parity; D126 correction; no public `docs/specs/**` link | `docs/guide/provider-specific/postgresql.md:324` ∥ `docs/ru/guide/provider-specific/postgresql.md:327`; `docs/guide/12-raw-sql.md` ∥ RU; `docs/advanced/limitations.md` ∥ RU; `docs/providers/overview.md` ∥ RU; D126 mapping `docs/specs/status/rc1-126-tuple-ctor-1.md:361`; audit `rg 'docs/specs|\.\./specs|specs/' docs/{advanced,guide,providers,scalar-functions} docs/ru/{advanced,guide,providers,scalar-functions} readme.md` → **no matches**; docfx exit 0, 2 pre-existing warnings (`/tmp/D194-evidence/d194-6-docfx.log`) |
| R194-PERF | acceptance 7 cases ≤4 min; 10 000-row probe vs direct Npgsql control | `final/acceptance.log` (7 executed / 0 failures, wall 66 s, BDN 47.71 s, ratio 2.16); `final/` coverage rows below; probe row E194-R2-PROBE |
| R194-R2-CACHE | `RawMapperCacheKey` captures record kind + tuple arity/item types; ordinary keys keep identity | impl `src/nextorm.core/DataContext/MapperCache.cs:24` (`RawRowKind`), `:45` (`RawMapperCacheKey` + `RecordKind`/`RecordSignature`); tests core `RawRowMaterializerTests.cs:523,:553,:565`; no `Cache=false`/`_dontCache` (`git diff -G"Cache = false"` / `-G_dontCache` empty) |

### E194 rows → final-tree command / result / log

`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` for all `I` rows.

| row | command (argv) | exit | key numbers | log (final tree) |
|---|---|---|---|---|
| E194-01 | preflight: anchors resolved (Roslyn), status file present, D194 row `in-progress` | — | call sites resolved; CRLF | this file; `collection-1.0.9-rc1-tail.md` |
| E194-02 | `[dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.PostgresRowSpikeTests, -noColor]` (throwaway spike, deleted) | 0 | Total 1 / Failed 0 / Skipped 0 | `/tmp/D194-evidence/spike-run.log`; probes `spike-results.txt` |
| E194-03 | implementation seam (record branch + named-composite accessor); verified by `[dotnet, build, nextorm.slnx, -c, Debug]` | 0 | 0 Warning(s) / 0 Error(s) | `/tmp/D194-evidence/r2-build.log` |
| E194-04 | `[dotnet, test, tests/nextorm.core.tests, -c, Debug, --no-build, --filter, "FullyQualifiedName~RawRowMaterializer"]` | 0 | Total **45** / Failed 0 / Skipped 0 | `/tmp/D194-evidence/final/core-rawrow.log` |
| E194-05 | `[dotnet, test, tests/nextorm.core.tests, -c, Debug, --no-build]`; `[dotnet, test, tests/nextorm.postgres.tests, -c, Debug, --no-build]`; sqlite; sqlserver | 0 | core **1624**/0/0; postgres **771**/0/0; sqlite **1078** (1077+1 skipped); sqlserver **697**/0/0 | `final/test-core.log`, `final/test-postgres.log`, `final/test-sqlite.log`, `final/test-sqlserver.log` |
| E194-06 | `[dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.PostgresRawRowTests, -noColor]` | 0 | Total **37** / Failed 0 / **Skipped 0** (final tree); JSON 7/0/0; common PG 617/0/25 (25 functional) | `final/pg-rawrow.log`, `final/pg-json.log`, `final/pg-integration.log` |
| E194-07 | `[dotnet, run, --project, tests/nextorm.integration.tests, -c, Debug, --no-build, --, -class, nextorm.integration.tests.ClickHouseIntegrationTests, -noColor]` | 0 | Total **107** / Failed 0 / Skipped 0; tuple regression PASS | `final/ch-integration.log` |
| E194-08 | error contract: exact message + `InnerException` (covered by E194-04/E194-06) | 0 | core **45**/0/0; PG **37**/0/0 | `final/core-rawrow.log`, `final/pg-rawrow.log` |
| E194-09 | coverage: `mkdir -p tests/coverage`; `[dotnet, tool, run, dotnet-coverage, collect, -s, coverage.settings.xml, -f, cobertura, -o, tests/coverage/coverage.cobertura.xml, "dotnet test --no-build --verbosity normal"]`; `[dotnet, tool, run, reportgenerator, -reports:tests/coverage/coverage.cobertura.xml, -targetdir:tests/coverage/report, -reporttypes:"Html;TextSummary;Cobertura", -riskhotspotassemblyfilters:"+nextorm.*"]` | 0 / 0 | collect total **8606** / Failed 0 / Succeeded 8416 / Skipped 190; **line 88.6 %** (46644/52590), **branch 79.8 %** (24299/30416) | `final/coverage-collect.log`, `final/coverage-report.log` |
| E194-10 | `[dotnet, docfx, docs/docfx.json]`; EN+RU docs + D126 mapping | 0 | 2 pre-existing warnings / 0 errors | `/tmp/D194-evidence/d194-6-docfx.log` |
| E194-11 | `[dotnet, run, --project, benchmarks/nextorm.benchmark, -c, Release, --, --anyCategories=acceptance]` | 0 | **7** cases / **0** failures; wall 66 s; BDN 47.71 s; ratio 2.16 | `final/acceptance.log` |
| E194-12 | finalize: `[dotnet, build, nextorm.slnx, -c, Debug]`; scope/CRLF/cache audits | 0 | **0 W / 0 E**; 18 modified + 3 new planned; CRLF-OK; cache-mutation diffs empty | `final/build.log` |
| E194-R2-SEAM | **U** + **I** (S1 `System.Tuple` from `object[]`; S5 named composite) | 0 | core 45/0/0; PG 37/0/0 | `final/core-rawrow.log`, `final/pg-rawrow.log` |
| E194-R2-NULL | **U** + **I** whole-record null / non-nullable item / nullable ref | 0 | core 45/0/0; PG 37/0/0 | `final/core-rawrow.log`, `final/pg-rawrow.log` |
| E194-R2-CACHE | `RawRowKind`/`RecordSignature` + core cache tests | 0 | core 45/0/0 (`:523,:553,:565`) | `final/core-rawrow.log` |
| E194-R2-PROBE | 10 000-row probe vs direct Npgsql control (throwaway, deleted) | 0 | NextORM 64.392 ms / 2 826 432 B vs control 35.587 ms / 1 762 712 B over 10 000 rows; bounded allocation | `/tmp/D194-evidence/r2-probe-run.log` |

**Coverage binding (final tree).** `tests/coverage/coverage.cobertura.xml` sha256
`2be0ba4cb2d3c23eaa731317686de324b6a5d2f942ce6a243eac80097edf9d32` (mtime 2026-10-06 16:34:11) →
`tests/coverage/report/Summary.txt` sha256
`21fa919073507c261accb7b5a5a4201746b51711dfe62370a0b5f77d2c06b7cb` (16:34:29); generated report
`Cobertura.xml` sha256 `40f81e092d35a350f01e3d2d66de9c19822d2ff7685c50361baddeffb5cec447`. Thresholds
`MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75` both satisfied.

## Progress log

- 2026-10-06T11:44Z | DO | r=2 | n=3/3 | **CHECK r2/n=3 evidence-completeness gate closed** (no product defect, no replan; `r`,`n`,`rv` unchanged). Step 1: added final-tree S9 execution test `TupleEnabledDataSource_NestedRow_ShouldGuardNotDataMismatch` (`PostgresRawRowTests.cs:358`) — nested `ROW(ROW(1,'a'),2)` into `Tuple<int,int>` over a caller-owned `EnableRecordsAsTuples()` data source surfaces the contracted nested-ROW `NotSupportedException` (not a data-mismatch wrap); PG `PostgresRawRowTests` **37/0/0** (was 36), 0 skipped. Step 2: wrote §Evidence manifest (final tree) binding R194-SHAPE/NULL/ERROR/COMPAT/PROVIDER/DOC/PERF/R2-CACHE + E194-01..12 + E194-R2-SEAM/NULL/CACHE/PROBE to verified anchors/commands/logs and the S9 disposition. Runs: `[dotnet, build, nextorm.slnx, -c, Debug]` exit 0 / **0W / 0E**; focused core `RawRowMaterializer` **45/0/0**. No production change. | `/tmp/D194-evidence/final/{build,core-rawrow,pg-rawrow}.log`
- 2026-10-06T09:41Z | DO | r=1 | n=1/3 | STEP 0: status file created; D194 row `in-progress` in `collection-1.0.9-rc1-tail.md`; plan/contract rv=1 recorded; recon (TypeFacts, external ctor, raw path) recorded | `docs/specs/status/{rc1-tail-194-pg-row-composite-1,collection-1.0.9-rc1-tail}.md`
- 2026-10-06T09:47Z | DO | r=1 | n=1/3 | STEP 0 D194.1 spike **done**: Npgsql 10.0.3 read shapes probed (7 configs × 4 SQL shapes × accessors); full `NpgsqlDataSourceBuilder` has no `EnableRecords` (tuple route is `EnableRecordsAsTuples`, object[] is slim-only/default-full); named composite needs only `MapComposite<T>`; `Tuple<int,string>` + all-null row → `(0,null)` vs `Tuple<int?,string>` → `(null,null)`; NextORM `ExecuteRaw` rejects `Tuple` before any driver read. Build 0W/0E; spike test exit 0 (Total 1, Failed 0, Skipped 0). **PROPOSED r2, STOP for PLAN.** | `/tmp/D194-evidence/spike-results.txt`; `/tmp/D194-evidence/spike-run.log`
- 2026-10-06 | PLAN | Replanned: r2 — spike resolved config API (full NpgsqlDataSourceBuilder: EnableRecordsAsTuples optional for S1; object[] route used) and NULL default-substitution (non-nullable item + SQL NULL = R194-ERROR).
- 2026-10-06T10:19Z | DO | r=2 | n=1/3 | STEP 2 D194.3 tests: `tests/nextorm.core.tests/RawRowMaterializerTests.cs` (S1 arity 1..7, R194-NULL, S3/S4/S6..S13 guards, R194-R2-CACHE) + `tests/nextorm.integration.tests/PostgresRawRowTests.cs` (PG container-backed, S1 arity 1..7, NULL, ValueTuple/nested/arity≥8/mixed/multiple guards, S5 `MapComposite<T>` + unregistered-composite guard). RED→GREEN for D194-GUARD-S12 (mixed named-composite+scalar). Solution build 0W/0E; full core 1606/0/0; PG 18/0/0; scope filter 41/0/0. | `/tmp/D194-evidence/d194-3-*.log`
- 2026-10-06T10:23Z | DO | r=2 | n=1/3 | STEP 4 D194.5 perf **done**: acceptance `--anyCategories=acceptance` exit 0, **7** cases / **0** failures, external wall 52.16 s, BDN `Global total time` 45.26 s; cached/prepared ratio 1.91 (baseline 1.87), alloc ratio 7.11 (baseline 7.42); targeted throwaway PG probe (10 000 rows, warmup 3 / measured 5) exit 0 (Total 1, Failed 0, Skipped 0) — NextORM mean 64.392 ms / 2 826 432 B vs direct Npgsql control mean 35.587 ms / 1 762 712 B, allocation constant across runs (bounded/no per-row reflection); probe test deleted; BDN artifacts restored (`git status` clean); no commit. | `/tmp/D194-evidence/r2-acceptance.log`; `/tmp/D194-evidence/r2-probe.log`; `/tmp/D194-evidence/r2-probe-run.log`
- 2026-10-06T10:26Z | DO | r=2 | n=1/3 | STEP 5 D194.6 evidence/status **done**: removed throwaway `tests/nextorm.integration.tests/PostgresRowSpikeTests.cs` (no source/test refs remain; historical status notes preserved); `dotnet build nextorm.slnx -c Debug` exit 0 / 0W / 0E; boundary full core 1606 passed / 0 failed / 0 skipped, postgres 771/0/0, PG `PostgresRawRowTests` 18/0/0 (0 skipped); CH `ClickHouseIntegrationTests.Tuple` 3/0/0; `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing warnings / 0 errors); scope audit 17 modified + 3 new planned files only; CRLF audit 20/20 CRLF-OK; BDN artifact churn none; rv=2; no commit. | `/tmp/D194-evidence/d194-6-{build,core,postgres,pg-integration,ch-tuple,docfx}.log`; throwaway removal in `git status --short` |
- 2026-10-06T15:39Z | DO | r=2 | n=2/3 | **CHECK FAIL r2/n=1 → DO n=2**. Applied C1/C3/C5/C6/C9 fixes + C2/C4 hardening: named-composite accessor moved out of `PostgresDataContext.MapColumnExpression` into the raw path (`RawMapperFactory.MapNamedCompositeColumn`) so converter-backed/ordinary LINQ class columns are never hijacked (C1); runtime `RawRowValue.ValidateRecord` validates the driver returned `System.Object[]` and `values.Length == args.Length` before indexing (C3/C5); new `DataContext.SupportsRawRowColumns` capability provider-gates the whole raw-row detection/diagnostic (default false; PG true) (C6); catch narrowing excludes `DbException`/`OperationCanceledException` from the metadata probes and the converter-exception filter is removed so every conversion failure is wrapped (C2/C4); EN+RU docs now say `InnerException` "when one exists" (C9). Build exit 0/0W/0E (`/tmp/D194-evidence/do-n2-build.log`); core `RawRowMaterializer` **32/32**, scope 46/46 (`do-n2-core-rawrow.log`, `do-n2-core-scope.log`); PG `PostgresRawRowTests` **18/18** (`do-n2-pg-rawrow.log`); **JSON attribution**: pre-fix `PostgresJsonColumnTests` 2 failed reading via `GetFieldValue<Shape>` (`json-pre-fix.log`) → post-fix **7/7** (`json-post-fix.log`) and `PostgresIntegrationTests.JsonColumn_*` **3/3** (`json-post-fix-common.log`) ⇒ the 5 failures were the **D194 converter bypass**, not `EnableDynamicJson`. | `/tmp/D194-evidence/do-n2-*.log`, `json-pre-fix.log`, `json-post-fix.log`, `json-post-fix-common.log`
- 2026-10-06T15:47Z | DO | r=2 | n=2/3 | **Test-matrix gaps closed** (re-CHECK prep): core `RawRowMaterializerTests` +3 (allow-list enum/byte[]/DateOnly/TimeOnly/TimeSpan/DateTimeOffset/float/short/nullable-decimal; C4 non-Format Guid→int wrapped with `InnerException` preserved; tautological key test replaced by real `GetOrBuild` shape-separation + `RawRowKind.None` vs `AnonymousTuple` key guard) → **35/35**; integration `PostgresRawRowTests` +12 (C1 `[JsonColumn]` converter pin; whole-composite SQL NULL → CLR null; `EnableRecordsAsTuples` + external-ctor S1; C3 arity both directions; mixed registered composite + scalar guard; S2 multi-scalar DTO; S7 named-composite-declared-as-Tuple; S8 ROW-as-DTO; S13 non-allow-listed field) → **30/30, 0 skipped**. **New defect D194-MIXED-UNREG-COMPOSITE** exposed and fixed: unregistered composite cast + scalar fell to the generic entity error; `RawMapperFactory.BuildEntitySelectList` + `TryGetUnresolvableCompositeColumn` now emit the contracted guard (RED `probe-mixed-unregistered.log` 1 failed → GREEN 30/30). Defect history marks D194-CONVERTER-BYPASS/ARITY/PROV-NONPG-DIAG/DOC-INNER **resolved r=2/n=2**. Build 0W/0E; boundary core 1614/0/0, postgres unit 771/0/0; no commit. | `/tmp/D194-evidence/do-n2b-{solution-build,core-build,core-rawrow,core-scope,pg-rawrow,core-full,postgres-full}.log`; `probe-mixed-unregistered.log` |
- 2026-10-06T11:11Z | DO | r=2 | n=3/3 | **n=3 P1 fixes + durable tests**. Production fixes already applied at n=3: `IsGenuineNamedComposite` constrains the named-composite hijack (#1); `RawRowValue.ReadNamedComposite<T>` wraps driver failures into R194-ERROR with `InnerException` preserved (#2); guard ordering decided before `EnsureNameMappable` (#4); shared genuine-composite predicate for unresolvable metadata (#5); exact `SZArray`/`object` element check for covariant arrays (#6); narrowed metadata catch excluding `ObjectDisposedException` (#3); `OperationCanceledException`/`OutOfMemoryException` excluded from conversion wrapping (#7); EN+RU datasource docs (#10); seam docs corrected (#9). Durable tests added: core `RawRowMaterializerTests` +5 (#1 `int[]`/`Dictionary`, #6 covariant array, #3 disposed-reader propagation, #7 cancelled conversion, #11 key separation via `GetOrBuild`) → filtered **54/0/0**, full core **1619/0/0**; integration `PostgresRawRowTests` +4 (#4 positional-record mixed-composite guard, #5 hstore ordinary no-match, #2 non-nullable struct composite NULL wrap, S10 empty `ROW()`) → **34/0/0, 0 skipped**. `dotnet build nextorm.slnx -c Debug` exit 0 / 0W / 0E; no commit. | `/tmp/D194-evidence/do-n3b/{solution-build,core-rawrow,core-full,pg-rawrow}.log`; `/tmp/D194-evidence/do-n3/*`
- 2026-10-06T16:17Z | DO | r=2 | n=3/3 | **DO n=3/3 boundary complete** (DO→CHECK, no replan; r stays 2, n stays 3/3). CRLF-normalized `RawRowMaterializerTests.cs`/`PostgresRawRowTests.cs`/status file (no lone LF); throwaway probes absent, `git status --short` = 17 modified + 3 new planned files only. Build `nextorm.slnx -c Debug` exit 0 / 0W / 0E. Unit: core **1619/0/0**, postgres **771/0/0**, sqlite **1078/0/1**, sqlserver **697/0/0**. PG container: `PostgresRawRowTests` **34/0/0**, `PostgresJsonColumnTests` **7/0/0**, `PostgresIntegrationTests` **617/0/25** (all 25 functional: 20 unsupported-provider + 5 CH-only TableFunction). CH `ClickHouseIntegrationTests` **107/0/0**. Fresh CI coverage (exact E194-09 commands, `DOCKER_HOST` set): collect exit 0 Total **8599/0/8409/190**; report exit 0 line **88.6 %** / branch **79.8 %**; cobertura sha256 `7d595139…` (16:15:31) → Summary.txt sha256 `c3cfa89e…` (16:15:37). Acceptance re-run 7/0, wall **55 s**, BDN 48.99 s, cached/prepared ratio **1.96** (baseline 1.91); BDN artifacts restored, no new reports. Logs `/tmp/D194-evidence/do-n3c/*`. | `/tmp/D194-evidence/do-n3c/{build,test-core,test-postgres,test-sqlite,test-sqlserver,pg-rawrow,pg-json,pg-integration,ch-integration,coverage-collect,coverage-report,acceptance}.log`; `do-n3b/{core-rawrow,core-full}` |
- 2026-10-06T16:30Z | DO | r=2 | n=3/3 | **Pre-verdict amendments n=3/d applied** (no replan; r stays 2, n stays 3/3). #1 exception-shape discriminator: `TryGetFieldType` surfaces the probe exception and `TryGetUnresolvableCompositeColumn` classifies a named composite only on `InvalidCastException` (dotted name no longer a composite signal; `ltree`/non-composite keep `None of the result-set columns …`); #2 `RawRowValue.ReadNamedComposite<T>` also excludes `ObjectDisposedException` from the wrap; #3 `IsMetadataRejection` narrowed to `InvalidCastException`/`NotSupportedException`; #4 `IsGenuineNamedComposite` collection exclusion narrowed to arrays + `System.Collections.IDictionary`; #5 nullable struct composite eligibility unwraps `Nullable.GetUnderlyingType` in both predicates and `MapNamedCompositeColumn` lifts the getter to the declared nullable type — **#2 decision: APPLIED** (small and safe), not deferred. New durable pins (core +5, PG +2): core `UnresolvableNonCompositeColumn_OrdinaryNoMatchRead_ShouldKeepTheEntityError`, `MetadataProbeInvalidOperation_ShouldPropagateNotBeSwallowed`, `DisposedNamedCompositeRead_ShouldPropagateUnwrapped`, `SingleNonScalarColumn_CustomEnumerableComposite_ShouldNotBeBlanketRejected`, `NullableStructComposite_Registered_ShouldRouteToNamedComposite`; PG `UnmappedLtreeColumn_OrdinaryNoMatchRead_ShouldKeepTheEntityError`, `NullableStructComposite_Registered_ShouldMaterialize`. **RED** (5 new core pins, fixes temporarily reverted): `[dotnet, test, tests/nextorm.core.tests, -c, Debug, --no-build, --filter, <5 new>]` exit **2**, 5 failed / 0 succeeded (`core-red.log`); observed — #1 NotSupported named-composite instead of `None of the result-set columns`; #2 wrapped `InvalidOperationException` instead of `ObjectDisposedException`; #3/#1 NotSupported instead of InvalidOperationException; #4/#5 entity error instead of the composite route. **GREEN** (fixes restored): `[dotnet, build, nextorm.slnx, -c, Debug]` exit 0 / 0W / 0E (`build.log`); focused core **59/0/0** (`core-focused.log`); full core **1624/0/0** (`core-full.log`); PG `PostgresRawRowTests` **36/0/0, 0 skipped** (`pg-rawrow.log`), `PostgresJsonColumnTests` **7/0/0** (`pg-json.log`), `PostgresIntegrationTests` **617/0/25** all functional (`pg-integration.log`). CRLF-normalized; no commit. | `/tmp/D194-evidence/do-n3d/*`; defect rows D194-UNRESOLVABLE-EXC-DISCRIMINATOR / D194-NAMED-COMPOSITE-DISPOSED / D194-METADATA-NARROW / D194-COLLECTION-EXCLUSION / D194-NULLABLE-STRUCT-COMPOSITE |
- 2026-10-06T16:37Z | DO | r=2 | n=3/3 | **DO n=3/3 final boundary complete** (post-amendment re-run; no replan, r stays 2, n stays 3/3, rv=2). CRLF-verified on all pre-amendment-edited files (`RawRowMaterializerTests.cs`, `RawMapperFactory.cs`, `PostgresRawRowTests.cs`, status file: 0 lone LF); throwaway probes absent (`PostgresRowSpikeTests`/`PostgresRawRowProbeTests`/`D194TypeProbeTests`); `git status --short` = 18 modified + 3 new planned files only. Build `nextorm.slnx -c Debug` exit 0 / 0W / 0E. Unit: core **1624/0/0**, postgres **771/0/0**, sqlite **1078 (1077+1 skipped)**, sqlserver **697/0/0**. PG container: `PostgresRawRowTests` **36/0/0**, `PostgresJsonColumnTests` **7/0/0**, `PostgresIntegrationTests` **617/0/25** (all 25 functional: 21 other-provider-rejection + 4 CH-only `TableFunction_*`). CH `ClickHouseIntegrationTests` **107/0/0**. Coverage (E194-09 exact CI commands, `DOCKER_HOST` set): collect exit 0 total **8606/0/8416/190**; report exit 0 line **88.6 %** (46644/52590) / branch **79.8 %** (24299/30416); `coverage.cobertura.xml` sha256 `2be0ba4c…` (16:34:11) → `Summary.txt` sha256 `21fa9190…` (16:34:29). Acceptance: exit 0, **7** cases / **0** failures, wall **66 s**, BDN **47.71 s**, cached/prepared ratio **2.16** (baseline 1.87, +15.5 % < 20 % threshold), alloc ratio **7.11**; BDN artifacts restored (`git status` clean). Cache-mutation check `git diff -G"Cache = false"` and `-G_dontCache` both **empty**. Logs `/tmp/D194-evidence/final/*`. | `/tmp/D194-evidence/final/{build,test-core,test-postgres,test-sqlite,test-sqlserver,core-rawrow,pg-rawrow,pg-json,pg-integration,ch-integration,coverage-collect,coverage-report,acceptance}.log` |

- 2026-10-06T11:48Z | ACT | r=2 | n=3/3 | **ACT executed (authorized auto-commit mode, collection exception).** CHECK r2/rv2 n=3/3 = PASS (no product defect, no replan). Finalized this file `status: done` + `## ACT`; DO ledger frozen. Accepted the `DataContext.SupportsRawRowColumns` protected-virtual seam (precedent `SupportsTypedColumnMapping`) and the informational perf note (cached/prepared +15.5 % < 20 % threshold); file kept (not deleted). Committing the D194 task files, then marking D194 `done` in `collection-1.0.9-rc1-tail.md`. | `docs/specs/status/{rc1-tail-194-pg-row-composite-1,collection-1.0.9-rc1-tail}.md`

## ACT

**Verdict: PASS** — CHECK r2/rv2 at n=3/3 (final). The third `pdca-check` accepted the implementation
with no product defect; ACT records the accepted seam, the accepted informational perf note and the
status-file disposition. No replan was executed at ACT; `r=2`, `n=3/3`, `rv=2` are retained.

- **Accepted seam — `DataContext.SupportsRawRowColumns` (`protected virtual`).** Kept under R194-COMPAT
  "no new public API": it is the same protected-virtual extensibility pattern as the established precedent
  `DataContext.SupportsTypedColumnMapping` (internal seam `DataContext.cs:668`, overridden
  `SqlServerDataContext.cs:558`). `SupportsRawRowColumns` is `protected virtual` (`DataContext.cs:677`,
  internal seam `:681`) and PostgreSQL overrides it (`PostgresDataContext.cs:231`). No public member and no
  `CreateDbConnection` change; the public API surface is unchanged.
- **Accepted informational perf note.** The post-amendment acceptance re-run measured cached/prepared
  ratio **2.16** vs baseline **1.87** — **+15.5 %**, below the 20 % investigation threshold (2.24).
  Recorded as informational; no corrective action. Acceptance: **7 cases / 0 failures**, wall **66 s**,
  BDN `Global total time` **47.71 s**; allocation ratio **7.11** (baseline 7.42); BDN artifacts restored
  (no artifact churn).
- **CHECK PASS evidence (r2/rv2, n=3/3).** `dotnet build nextorm.slnx -c Debug` **0 W / 0 E**; unit core
  **1624/0/0**, postgres **771/0/0**, sqlite **1078 (1077+1 skipped)**, sqlserver **697/0/0**; PG
  `PostgresRawRowTests` **37/0/0** (0 skipped), `PostgresJsonColumnTests` **7/0/0**,
  `PostgresIntegrationTests` **617/0/25** (all 25 functional); CH `ClickHouseIntegrationTests` **107/0/0**;
  coverage line **88.6 %** / branch **79.8 %** (above thresholds). Logs `/tmp/D194-evidence/final/*`.
- **Status-file disposition.** This file is **kept** (not deleted) as the durable D194 record;
  `status: done`, `r=2`, `n=3/3`, `rv=2`.
