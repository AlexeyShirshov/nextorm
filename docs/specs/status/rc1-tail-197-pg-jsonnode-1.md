# PostgreSQL: read `JsonNode` from native `json`/`jsonb` — issue #197 (task D197)

- task: D197
- issue: #197 (https://github.com/AlexeyShirshov/nextorm/issues/197)
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: done
- cycle: N=1
- plan revision: r=2
- attempt: n=1/3
- contract: rv=2
- prerequisite: D197-P01 (done — additive r=2 unit)
- mode: autonomous + auto-commit (collection exception)
- git: no push/merge; task files only at collection auto-commit after CHECK/ACT

## Goal

A native PostgreSQL `json`/`jsonb` column mapped to a property of the bare
`System.Text.Json.Nodes.JsonNode` type currently cannot be materialized: `RowMapperFactory` picks the
reader accessor from the CLR property type and `SelectExpression.GetDataRecordMethod` has no
`GetFieldValue<JsonNode>` accessor, so mapper construction throws `NotSupportedException`. Npgsql does
not expose `jsonb` as `JsonNode` directly, while the write side already binds `JsonNode` as `jsonb`
(`PostgresDataContext.CreateParam`). The fix composes an equivalent read path in
`RowMapperFactory.GetReaderAccessor`: read the JSON text with `DbDataReader.GetFieldValue<string>` and
parse it with `JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)` (default options). The
change is core-only and exact-type-scoped: only `readType == typeof(JsonNode)` is special-cased;
declared `JsonObject`/`JsonArray` properties remain unsupported (deferred trigger below).

## Acceptance criteria (R197-01..08)

- **R197-01** `RowMapperFactory.GetReaderAccessor` special-cases **exact** `readType == typeof(JsonNode)`
  **before** the generic `column.GetDataRecordMethod(readType)` path; the emitted accessor calls
  `DbDataReader.GetFieldValue<string>(column.Index)` and then the static
  `JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)` with default options. No
  `GetFieldValue<JsonNode>` (or `GetFieldValue<JsonObject>`/`GetFieldValue<JsonArray>`) is emitted.
  `IsAssignableFrom` is not used; `JsonObject`/`JsonArray` are not special-cased.
- **R197-02** both reflection lookups are resolved once into `private static readonly MethodInfo`
  fields (closed `GetFieldValue<string>` and `JsonNode.Parse`); no reflection happens per row.
- **R197-03** SQL NULL yields `null` **without invoking the string getter** for both the `JsonNode`
  and `JsonNode?` annotations; the `MapColumn` null guards (`:84-106`) and the materializer
  outer-join guard (`:317`) are untouched.
- **R197-04** object, array, number, string, boolean and JSON-null roots parse to nodes semantically
  equal to `JsonNode.Parse(text)` (`JsonNode.DeepEquals`).
- **R197-05** an empty / malformed JSON string surfaces `System.Text.Json.JsonException` from the
  parse call — not wrapped and not suppressed.
- **R197-06** the existing `JsonDocument`/`JsonElement` accessor route is unchanged;
  `SelectExpression.GetDataRecordMethod` (`:177-282`) is **not** modified; `PostgresDataContext`
  parameter binding, converters, cache keys are untouched.
- **R197-07** unit tests U197-01..06 are green after a captured red; `dotnet build nextorm.slnx -c
  Debug` is 0 Warning / 0 Error; every edited file is CRLF; no commit/push.
- **R197-08** the predecessor R131 invariants are preserved (see §Predecessor R131 invariants), and the
  `code-smells-review.md:7105` interaction («Находка 197» / observation B) is **reported only** — that
  file is not edited.

## DO plan (ordered)

- **D197-01** (STEP 0, blocking) — read the anchors; create this status file; set the D197 row to
  `in-progress` in `collection-1.0.9-rc1-tail.md` (G1 stays `in-progress`). No production edit.
- **D197-02** (STEP 1) — implement the exact-`JsonNode` branch in `GetReaderAccessor` plus the two
  `private static readonly` MethodInfo fields and the `System.Text.Json.Nodes` using.
- **D197-03** (STEP 2) — add U197-01..06 to `tests/nextorm.core.tests/SelectExpressionTests.cs`,
  capture RED (focused run before the implementation is present), then GREEN.
- **D197-04** (STEP 3) — `dotnet build nextorm.slnx -c Debug` 0 Warning / 0 Error.
- **D197-05** (STEP 4) — CRLF-normalize every edited file; `roslyn`/`git diff --stat` scope audit;
  append evidence rows and `DO complete` to this file. No commit (orchestrator auto-commits after
  CHECK/ACT).
- **D197-06** (separate DO unit) — PostgreSQL integration round-trip for a bare `JsonNode`
  property over `json` and `jsonb` (issue acceptance: "Тесты (интеграционный PG)"). Pending; not
  performed by this unit.
- **D197-07** (separate DO unit) — EN + RU docs note for the `JsonNode` native read path. Pending;
  not performed by this unit.
- **deferred** — declared `JsonObject`/`JsonArray` support and the targeted BDN run (see triggers).

## Revised plan r=2 — additive prerequisite D197-P01 (does not supersede D197-06)

The r=1 `D197-06` scalar criterion exposed an out-of-scope core gap (`F197-JSONNODE-SCALAR-PROJECTION`).
Rather than widening the test unit, r=2 adds a **new active prerequisite unit `D197-P01`**; the original
`D197-06` stays active with its criteria and residual unchanged (it was blocked on this dependency, not
superseded). `D197-P01` closes only the core classification gap: a bare `JsonNode` must classify as a
single-column projection so the scalar `Select(x => x.Data)` path reaches `RowMapperFactory`.

### Consumer audit (authoritative, r=2)

`TypeFacts.IsSingleColumnProjection` consumers: `QueryCommand.QueryPreparer.cs:458,467,535,634,2007`
and `InMemoryExtremeRow.cs:112`.
- Adding the exact `JsonNode` check newly fires only the scalar-identity routes `:458`, `:535` and
  `:634`: `:535` sets `OneColumn=true`, builds a single `selExp` and `selectList=[selExp]`; `:458`
  (`ProjectionType ?? srcType == srcType` with a one-column `scalarIdentityShape`) and `:634` (same
  guard for the else-branch source) reuse the one readable column. All are the intended
  scalar-projection route.
- `:467`/`:2007` are `NewExpression`/`MemberInit` guards — `JsonNode` is abstract and cannot be
  constructed, so they are unreachable for `JsonNode` and unchanged. (`:583` is **not** an
  `IsSingleColumnProjection` call site — it is the plan-hash block after `:535`; the earlier r=2 list
  wrongly included it and this CHECK re-gather removes it.)
- `InMemoryExtremeRow.cs:112` is in `CompileComponents`, invoked once per enumeration
  (`CreateEnumerator:72` → `InMemoryQueryBuilder.cs:336,338` → `InMemoryDataContext.cs:193`), not per
  row; the classification change adds no per-row cost.
- `InternalsVisibleTo("nextorm.core.tests")` exists (`nextorm.core.csproj:54`), so the direct internal
  `TypeFacts` tests are possible.

### D197-P01 criteria

- **P01-01** `IsSingleColumnProjection` accepts the **exact** `System.Text.Json.Nodes.JsonNode` type and
  does **not** accept `JsonObject`/`JsonArray`/`JsonValue`/`JsonDocument`/`JsonElement` (no
  `IsAssignableFrom`).
- **P01-02** direct internal tests for the exact-positive, the non-exact negatives and the existing
  categories; `dotnet test tests/nextorm.core.tests -c Debug` exit 0 / 0 failed.
- **P01-03** the previously blocked `BareJsonNode_ScalarProjection_ShouldMaterialize` passes; PG
  `PostgresJsonColumnTests` exit 0 / 0 failed / 0 skipped.
- **P01-04** no regression: PG `PostgresIntegrationTests` skips unchanged (25 by-design) and ClickHouse
  `ClickHouseIntegrationTests` 0 failed / 0 skipped; solution build 0W/0E.

## Design — read-path composition

`MapColumn` (unchanged) resolves `realType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType`
and, with no converter, calls `GetReaderAccessor(column, param, realType)`. The new composition:

```
condition(IsDBNull(column.Index), null,                      // MapColumn guard, unchanged
          JsonNode.Parse(
            ((DbDataReader)param).GetFieldValue<string>(column.Index),
            null,                                            // JsonNodeOptions?  -> default
            default(JsonDocumentOptions)))                   // document options -> default
```

- Receiver: `Expression.Convert(param, typeof(DbDataReader))` (same shape as the generic path for a
  `DbDataReader`-declared method; `param` is `IDataRecord` in `Build` and `DbDataReader` in tests).
- Both MethodInfos are resolved from `private static readonly` fields
  (`GetFieldValueStringMI`, `JsonNodeParseMI`), so a cached mapper performs no reflection per row.
- Null handling stays in `MapColumn`: because `SelectExpression(Type)` sets `Nullable = true` for a
  reference type, both `JsonNode` and `JsonNode?` go through the `IsDBNull` short-circuit and the
  string getter is not invoked on SQL NULL.
- The branch is checked by exact type equality and only in `GetReaderAccessor`; the generic
  `GetDataRecordMethod` oracle and all other providers are untouched.

## Variant matrix

| axis | variant | closure | test |
|---|---|---|---|
| accessor | exact `JsonNode`, non-null JSON | test | U197-01 (`GetFieldValue<string>` + parse) |
| accessor | exact `JsonNode`, emitted accessor shape | test | U197-01b (no `GetFieldValue<JsonNode>`) |
| null | SQL NULL, `JsonNode` annotation | test | U197-02 |
| null | SQL NULL, `JsonNode?` annotation | test | U197-02 |
| root kind | object / array / number / string / boolean / JSON-null | test | U197-03 |
| malformed | empty / truncated / syntax error → `JsonException` | test | U197-04 |
| regression | `JsonDocument` / `JsonElement` route unchanged | test | U197-05 |
| caching | repeated materialization, parse MethodInfo reused, no per-row reflection | test | U197-06 |
| provider | PostgreSQL `json` / `jsonb` round-trip | **separate DO unit** | D197-06 |
| declared types | `JsonObject` / `JsonArray` property | **deferred** | trigger below |

## Test strategy

- Unit (U197-01..06) in `tests/nextorm.core.tests/SelectExpressionTests.cs` using `RowMapperFactory`
  (internal, `InternalsVisibleTo`), the existing `SingleColumnReader` plus a deriving
  `RecordingJsonReader` that records requested `GetFieldValue<T>` type arguments and refuses any type
  other than `string`. An `ExpressionVisitor` collects emitted `MethodInfo`s for the shape assertions.
- RED first: the focused test run is captured **before** the implementation (mapper construction throws
  `NotSupportedException` for `JsonNode`), then GREEN after.
- Focused command (internal loop):
  `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~SelectExpressionTests"`.
- Boundary run: one full `dotnet test tests/nextorm.core.tests -c Debug` at DO→CHECK.
- The PostgreSQL integration round-trip is owned by the separate DO unit D197-06.

## Docs plan

EN + RU: document that a bare `System.Text.Json.Nodes.JsonNode` property maps to a native
`json`/`jsonb` column (read as text + parsed; write already binds `jsonb`) alongside
`JsonDocument`/`JsonElement`. Owned by the separate DO unit D197-07; no public-doc edit in this unit.
No links to `docs/specs/**`.

## Perf decision

**Targeted BDN category `D197JsonRead` is required before ACT** because the new branch adds a
per-row `JsonNode.Parse` (unlike `GetFieldValue<JsonDocument>`/`<JsonElement>`, which materialize
driver-side). The category must compare the three read paths for a `JsonNode`-typed projection over
three payload sizes — **128 B**, **4 KiB**, **64 KiB** — on PostgreSQL (`jsonb`), confirming the
documented cost of parse-on-read and that no per-row reflection is introduced. This is a **planned
follow-up** (not run by this unit; the unit tests cannot measure throughput). Justification is recorded
now so the CHECK/ACT gate has an explicit decision and the deferred trigger is unambiguous.

### Outcome (DO part 3, 2026-10-06) — executed

- **Acceptance run** `--anyCategories=acceptance`: **7** cases selected, **0** failures, exit **0**;
  external shell wall clock **53.87 s**, BDN `Global total time` **44.69 s** — both under the 4 min
  budget. Comparable cached-vs-prepared ratio (`Cached_ToList / Prepared_ToList`) = **1.97** vs documented
  baseline **1.87** (**+5.3 %**, below the **20 %** investigation threshold); allocated ratio **7.25** vs
  **7.42** (unchanged). Absolute means were host-noise-inflated across all rows, so no individual delta is
  read as a regression. **Verdict: no regression.** Evidence: `artifacts/pdca/D197/perf-acceptance.log`;
  recorded in `docs/specs/performance/acceptance-benchmarks.md`.
- **Targeted `D197JsonRead`** micro-benchmark (`JsonNodeReadBenchmark`, 3 methods × 3 payloads = **9**
  cases, exit 0, BDN total 63.02 s): `JsonNode.Parse` allocates the full DOM (~848 B / 16.8 KB / 219 KB
  for 128 B / 4 KiB / 64 KiB) versus the pooled ~72 B of `JsonDocument.Parse`, and is ~1.0–1.5× its Mean;
  the compiled accessor (`GetFieldValue<string>` + `JsonNode.Parse`, no per-row reflection) adds **no
  measurable overhead** over the bare parse (sub-noise: 1.285 vs 1.298 µs; 29.7 vs 27.0 µs; 468 vs 485 µs,
  with `Error` 53 µs / 291 µs at the larger sizes). No SLA asserted. Evidence:
  `artifacts/pdca/D197/perf-jsonnode.log`.
- The deferred trigger below is therefore **executed and closed**; `RowMapperFactory` performs no per-row
  reflection (both `MethodInfo`s are `private static readonly`).
- BDN artifact churn (`BenchmarkDotNet.Artifacts/results/*`, tracked at the repo root; the
  `BenchmarkArtifacts` resolver's `nextorm.sln` fallback misses `nextorm.slnx`) was restored with
  `git checkout`, and the new `JsonNodeReadBenchmark` reports were removed — no artifact churn remains.

## Reconnaissance decision

**None** — the change is a single composition point whose call sites were read directly while writing
this file: `RowMapperFactory.cs:48-121` (`MapColumn`, `GetReaderAccessor`),
`SelectExpression.cs:175-282` (`GetDataRecordMethod`), `PostgresDataContext.cs:145-152`
(`JsonNode` bound as `jsonb`), `SelectExpressionTests.cs:11-188` (existing test pattern). No prototype
and no fresh scout pass.

## Unit mode

Single sequential `coder` unit per STEP (one axis per step, verified after each). Single collection
group ⇒ no branch/worktree/merge; no commit inside the unit (orchestrator auto-commits task files
after CHECK/ACT). Integration tests and docs are separate DO units.

## Design checklist

- Exact-type branch only (`==`), placed before the generic accessor; no `IsAssignableFrom`.
- Both MethodInfos in `private static readonly` fields; no per-row reflection.
- `MapColumn` null guards and the `:317` outer-join guard unchanged.
- `SelectExpression.GetDataRecordMethod`, `PostgresDataContext`, converters, cache keys unchanged.
- `TreatWarningsAsErrors=true`; Debug build 0/0; CRLF preserved on every edited file.
- No new public API; no dependency change; no commit/push.

## Deferred triggers

- **«Находка 197» — report only, do not fix.** `docs/specs/design/code-smells-review.md:7105`
  (observation B: `SelectExpression.GetDataRecordMethod()` parameterless uses the unresolved
  `Converter?.ProviderType`; later renumbered/closed as observation B — `:7118`). Not touched; this
  unit only reports the interaction. The issue's own numbering ("Находка 197" at `:7094`, fluent
  `HasConversion` model validation) is also untouched.
- **Declared `JsonObject`/`JsonArray`.** A property declared as `JsonObject`/`JsonArray` is **not**
  special-cased (the branch is exact `typeof(JsonNode)`), so it keeps the current
  `NotSupportedException`. Widening to `IsAssignableFrom(typeof(JsonNode))` is deferred until required
  by an issue. The same applies to `JsonValue`.
- **Bare `JsonDocument`/`JsonElement` scalar projection.** A scalar `Select(x => x.Doc)` for a bare
  `JsonDocument`/`JsonElement` property is **not** classified as single-column by `D197-P01` (only the
  exact `JsonNode` was added); if a scalar projection over those types is required it needs a separate
  exact-type addition. Not regressed: their composite accessor route (`GetFieldValue<JsonDocument>` /
  `<JsonElement>`) is unchanged.
- **ClickHouse `JsonObject` scalar.** ClickHouse returns `JsonObject` for native JSON columns; that
  path remains governed by the existing ClickHouse read route and is **not** touched by the
  PostgreSQL-scoped `D197-P01` addition (the guard run is `ch-regression-r2.log`, 107/0/0).
- **Perf BDN `D197JsonRead`.** **Executed** by DO part 3 (see §Perf decision §Outcome): acceptance
  7/7 exit 0, cached/prepared ratio 1.97 vs 1.87; targeted category 9/9 exit 0, accessor within noise of
  the bare parse. The `D197-P01` classification change adds no per-row cost (consumer audit E197-P01).

## Predecessor R131 invariants (must hold)

- A native `json`/`jsonb` column is read through the provider-typed accessor:
  `GetFieldValue<JsonDocument>` / `GetFieldValue<JsonElement>` remain available and unchanged
  (`SelectExpression.cs:246-254`).
- `[JsonColumn]` model mapping and `IJsonColumnConverter.Resolve(dialect)` (Auto → native on a
  JSON-capable dialect) keep routing to `MapConvertedColumn` with the resolved `providerType`; the new
  branch does not intercept a non-`JsonNode` provider type.
- `PostgresDataContext.CreateParam` continues to bind `JsonDocument`, `JsonElement` and `JsonNode` as
  `NpgsqlDbType.Jsonb` (`:151-152`); the read fix does not change the write binding.
- The public parameterless `GetDataRecordMethod()` keeps using the resolved `ProviderType ??
  _realType` (`:175`) — observation B closure, no regression.

## Durable state

- cycle `N=1` · plan revision **`r=2`** · attempt **`n=1/3`** · contract/evidence revision **`rv=2`** —
  carried in this file; a session/`task_id` reset never resets `r`, `n` or the defect history.
- DO ledger:

| unit | STEP | status | evidence |
|---|---|---|---|
| D197-P01 | r=2 prerequisite | done | exact `JsonNode` branch in `Visitors/TypeFacts.cs:78`; direct `TypeFactsTests` (13 cases); focused 36/0/0, full core 1575/0/0; PG scalar `PostgresJsonColumnTests` 7/0/0; PG suite 617/0/25, CH 107/0/0; build 0W/0E |
| D197-01 | 0 preflight | done | status file created; D197 row `in-progress` in `collection-1.0.9-rc1-tail.md` |
| D197-02 | 1 implementation | done | `RowMapperFactory.cs:32-33` static fields; `:118-138` exact-`JsonNode` branch |
| D197-03 | 2 unit tests | done | `SelectExpressionTests.cs:115-262` U197-01..06; red `red-focused.log` exit 2 (23/13/10) → green `green-focused.log` exit 0 (23/23) |
| D197-04 | 3 build | done | `build-debug.log` exit 0, 0 Warning / 0 Error |
| D197-05 | 4 scope/CRLF | done | `git diff --stat` = planned files only; 4 files 100% CRLF |
| D197-06 | separate unit | done | PG integration `json`/`jsonb` bare-`JsonNode` round-trip: composite projection + all roots + JSON/SQL null + `JsonNode`-param (`pg_typeof` = jsonb) + `JsonDocument`/`JsonElement` controls PASS (6/7, `pg-json.log`); scalar `Select(x => x.Data)` was BLOCKED (`F197-JSONNODE-SCALAR-PROJECTION`), now **PASS**: `PostgresJsonColumnTests` 7/0/0 (`pg-json-r2.log`). Scalar matrix closed: `BareJsonNode_ScalarProjection_ShouldMaterialize` (`:317`) + `AssertScalarJsonNodeRoots` (`:475`) cover `json`/`jsonb` × `JsonNode`/`JsonNode?` × object/array/number/string/boolean + JSON literal null + SQL NULL via `JsonNode.DeepEquals`; `pg-json-scalar-matrix.log` exit 0 (7/0/0). Evidence complete; re-verification at CHECK. |
| D197-07 | separate unit | done | EN+RU docs note: `docs/{,ru/}providers/postgres.md`, `docs/{,ru/}guide/14-json.md`, `docs/{,ru/}guide/provider-specific/postgresql.md`; bare `JsonNode`/`JsonNode?` native read documented, stale "not read" statements corrected; no `docs/specs/**` links; no public-API/DDL claim |
| D197-08 | separate unit | done | perf: acceptance 7/7 exit 0 (ratio 1.97 vs 1.87); targeted `D197JsonRead` 9/9 exit 0; accessor no measurable overhead, no per-row reflection; BDN artifacts restored |

### Priority matrix (P1 rows — acceptance invariants, CHECK must not downgrade)

| id | invariant | criterion | closure |
|---|---|---|---|
| P1-1 | exact `JsonNode` accessor emits `GetFieldValue<string>` + `JsonNode.Parse` (default options); no `GetFieldValue<JsonNode>`/`<JsonObject>`/`<JsonArray>`; branch before the generic path | R197-01 | `RowMapperFactory.cs:120-138`; U197-01/01b `SelectExpressionTests.cs` |
| P1-2 | both reflection lookups in `private static readonly` `MethodInfo` fields; no per-row reflection | R197-02 | `RowMapperFactory.cs:32-33`; U197-06 |
| P1-3 | SQL NULL → `null` without invoking the string getter for `JsonNode` and `JsonNode?`; `MapColumn` guards untouched | R197-03 | U197-02; `MapColumn` `:84-106` + outer-join guard unchanged |
| P1-4 | object/array/number/string/boolean/JSON-null roots `JsonNode.DeepEquals(JsonNode.Parse(text))` | R197-04 | U197-03 |
| P1-5 | empty/truncated/syntax-error JSON surfaces `System.Text.Json.JsonException`, unwrapped | R197-05 | U197-04 |
| P1-6 | `JsonDocument`/`JsonElement` route unchanged; `GetDataRecordMethod` (`:177-282`) not modified; PG binding/converters/cache keys untouched | R197-06 | U197-05; `SelectExpression.cs`/`PostgresDataContext.cs` `git diff` empty |
| P1-7 | red→green captured; `dotnet build nextorm.slnx -c Debug` 0 Warning/0 Error; every edited file CRLF; no commit | R197-07 | red `red-focused.log` exit 2 (23/13/10) → green `green-focused.log` exit 0 (23/23); `build-debug.log` 0W/0E |
| P1-8 | R131 invariants preserved; «Находка 197» reported only | R197-08 | `code-smells-review.md` diff empty; E197-09 |
| P1-9 | `IsSingleColumnProjection` accepts the **exact** `JsonNode` and no `JsonObject`/`JsonArray`/`JsonValue`/`JsonDocument`/`JsonElement` (no `IsAssignableFrom`) | P01-01 | `Visitors/TypeFacts.cs:78`; `TypeFactsTests` 13 cases (focused 36/0/0) |
| P1-10 | bare `JsonNode` scalar projection reaches the mapper: `BareJsonNode_ScalarProjection_ShouldMaterialize` PASS, 0 skipped | P01-03 | `pg-json-r2.log` exit 0 (7/0/0) |
| P1-11 | no regression: PG 617/0/25 (= baseline), CH 107/0/0, solution build 0/0 | P01-04 | `pg-regression-r2.log`, `ch-regression-r2.log`, `build-debug-r2.log` |

### Defect history (stable keys)

| defect key | state | observed r/n | fixes applied | evidence / note |
|---|---|---|---|---|
| F197-JSONNODE-MISSING | **resolved — this unit** | r1 / n1 | 1 | `GetDataRecordMethod(JsonNode)` threw `NotSupportedException` (`GetReaderAccessor`); red `red-focused.log` exit 2 (23/13/10) → green `green-focused.log` exit 0 (23/23). |
| F197-DECLARED-JSONOBJECT | deferred | r1 / n1 | 0 | `JsonObject`/`JsonArray` declarations remain unsupported by design; exact-type branch. |
| F197-JSONNODE-SCALAR-PROJECTION | **resolved — D197-P01 (r=2)** | r1 / n1 → r2 / n1 | 1 | A bare `JsonNode` scalar projection (`ctx.From<T>().Select(x => x.Data)`) never reached `GetReaderAccessor`: `TypeFacts.IsSingleColumnProjection(JsonNode)` was false (`Visitors/TypeFacts.cs`), so `PrepareColumns` (`QueryCommand.QueryPreparer.cs:535`) fell into entity mapping and `RowMaterializerBuilder.BuildCore` threw `QueryPreparationException: Cannot get ctor from System.Text.Json.Nodes.JsonNode`. Fixed by the exact-type branch (`TypeFacts.cs:78`). Evidence: r1 `artifacts/pdca/D197/pg-json.log` exit 1 (1 failed / 7) → r2 `artifacts/pdca/D197/pg-json-r2.log` exit 0 (7/0/0, `BareJsonNode_ScalarProjection_ShouldMaterialize` PASS). |

## Evidence contract (rv=2; E197-01..14 retained from rv=1)

| row | command / action | required result | actual result | owner |
|---|---|---|---|---|
| E197-01 | read the anchors (`RowMapperFactory.cs:48-121`, `SelectExpression.cs:175-282`, `PostgresDataContext.cs:145-152`, `SelectExpressionTests.cs:11-188`) | call sites understood; no conflict | PASS — anchors read while writing the plan; no conflict with R131 invariants | coder |
| E197-02 | create this status file; set D197 row `in-progress` in `collection-1.0.9-rc1-tail.md` | present, CRLF | PASS — `rc1-tail-197-pg-jsonnode-1.md` created; D197 row `in-progress`; both CRLF | coder |
| E197-03 | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~SelectExpressionTests"` **before** the implementation | RED: nonzero exit, assertion/`NotSupportedException` failures, not a compile error | PASS — `artifacts/pdca/D197/red-focused.log` exit 2; total 23 / failed 13 / succeeded 10; failures are `NotSupportedException: Property 'Data' ... type System.Text.Json.Nodes.JsonNode ...` (not compile) | coder |
| E197-04 | implement `RowMapperFactory.GetReaderAccessor` + the two static MethodInfo fields + `using` | exact-type branch; no scope creep | PASS — `RowMapperFactory.cs:32-33` (`GetFieldValueStringMI`, `JsonNodeParseMI`), `:120` `readType == typeof(JsonNode)`, `:127-134` `GetFieldValue<string>` + `JsonNode.Parse`; `System.Text.Json.Nodes` + `System.Text.Json` usings | coder |
| E197-05 | same focused command **after** the implementation | GREEN: exit 0, all selected pass | PASS — `artifacts/pdca/D197/green-focused.log` exit 0; total 23 / failed 0 / succeeded 23 | coder |
| E197-06 | `dotnet build nextorm.slnx -c Debug` | `0 Warning(s) 0 Error(s)` | PASS — `artifacts/pdca/D197/build-debug.log` exit 0; Build succeeded, 0 Warning(s) 0 Error(s) | coder |
| E197-07 | `roslyn` + `git diff --stat` scope audit | only planned files changed; `GetReaderAccessor` sole new branch | PASS — `roslyn members NextORM.Core.RowMapperFactory` lists `GetFieldValueStringMI`/`JsonNodeParseMI`; `git diff --stat` = `RowMapperFactory.cs`, `SelectExpressionTests.cs`, `collection-1.0.9-rc1-tail.md` only (+ untracked status file); `SelectExpression.cs`/`PostgresDataContext.cs`/`code-smells-review.md` diffs empty | coder |
| E197-08 | CRLF check on every edited file | 100% CRLF | PASS — `file` reports CRLF for all 4 edited/created files | coder |
| E197-09 | report «Находка 197» / `code-smells-review.md:7105` interaction | reported only; file unchanged | PASS — `git diff -- docs/specs/design/code-smells-review.md` empty; interaction only recorded in §Deferred triggers | coder |
| E197-10 | `dotnet test tests/nextorm.core.tests -c Debug` (boundary) | exit 0, 0 failed | PASS — `artifacts/pdca/D197/green-core-full.log` exit 0; total 1562 / failed 0 / succeeded 1562 / skipped 0 | coder |
| E197-11 | PG integration `json`/`jsonb` bare-`JsonNode` round-trip (`dotnet run … -class …PostgresJsonColumnTests`) | exit 0, 0 failed, 0 skipped | **BLOCKED — partial**: `artifacts/pdca/D197/pg-json.log` exit 1; total 7 / failed 1 / skipped 0. PASS (6): `BareJsonNode_PhysicalJsonb/Json_CompositeProjection_ShouldReadEveryRoot` (composite projection; object/array/number/string/boolean roots; JSON literal `null` → CLR null; SQL NULL → CLR null; `JsonNode?` null), `BareJsonNode_Parameter_ShouldBindAsJsonbAndRoundTrip` (write + `pg_typeof`=jsonb + read-back via anonymous projection), `JsonDocument_And_JsonElement_ShouldStillReadNative`, both `[JsonColumn]` round-trips. FAIL (scalar `Select(x => x.Data)`): `BareJsonNode_ScalarProjection_ShouldMaterialize` — `Cannot get ctor from System.Text.Json.Nodes.JsonNode` (defect F197-JSONNODE-SCALAR-PROJECTION) | D197-06 unit |
| E197-12 | EN+RU docs note | EN/RU parity; no `docs/specs/**` links | PASS — D197-07 done: six files (`docs/{,ru/}providers/postgres.md`, `docs/{,ru/}guide/14-json.md`, `docs/{,ru/}guide/provider-specific/postgresql.md`) updated with matching scope; stale "bare `JsonNode` column not read" removed; no `docs/specs/**` link; see E197-18 | D197-07 unit |
| E197-13 | ClickHouse guard `-class …ClickHouseIntegrationTests` (bare-`JsonObject` core path not regressed) | 0 failed / 0 skipped | PASS — `artifacts/pdca/D197/ch-integration.log` exit 0; total 107 / failed 0 / skipped 0 | D197-06 unit |
| E197-14 | Postgres guard `-class …PostgresIntegrationTests` (no regression) | 0 failed | PASS — `artifacts/pdca/D197/pg-integration.log` exit 0; total 617 / failed 0 / skipped 25 (all pre-existing by-design `*UnsupportedProvider*`/`TableFunction_*` skips, none from this change) | D197-06 unit |
| E197-15 | perf acceptance `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` | 7 cases, 0 failures, exit 0; ≤4 min; ratio recorded | PASS — `artifacts/pdca/D197/perf-acceptance.log` exit 0, 7 executed, shell wall **53.87 s**, BDN total **44.69 s**; cached/prepared **1.97** vs baseline **1.87** (+5.3 %, <20 %), alloc ratio **7.25** vs **7.42**; no regression | DO part 3 |
| E197-16 | targeted BDN category `D197JsonRead` (new `JsonNodeReadBenchmark`) | 9 cases, 0 failures; Mean/Allocated per size; accessor vs direct parse | PASS — `artifacts/pdca/D197/perf-jsonnode.log` exit 0, 9 executed, BDN total 63.02 s; `JsonNode.Parse` 848 B/16.8 KB/219 KB (128 B/4 KiB/64 KiB) vs `JsonDocument` ~72 B; compiled accessor sub-noise vs bare parse; no per-row reflection | DO part 3 |
| E197-17 | restore BDN artifacts (tracked `BenchmarkDotNet.Artifacts/results/*` + new reports) | no artifact churn in `git status` | PASS — `git checkout -- BenchmarkDotNet.Artifacts/results` (15 modified files) + removed 3 new `JsonNodeReadBenchmark` reports; artifacts status empty | DO part 3 |
| E197-18 | EN+RU docs (`docs/{,ru/}providers/postgres.md`, `docs/{,ru/}guide/14-json.md`, `docs/{,ru/}guide/provider-specific/postgresql.md`) | EN/RU parity; bare `JsonNode` read documented; no `docs/specs/**` links; scalar-functions doc untouched (parameter-only) | PASS — six files updated; stale "bare JsonNode column not read" statements corrected; scope (exact `JsonNode`/`JsonNode?`, object/array/scalar roots, SQL/JSON null → CLR null, scalar+composite projection; `JsonObject`/`JsonArray` and bare `JsonDocument`/`JsonElement` scalar not included) documented on both mirrors; no `docs/specs/**` link introduced | DO part 3 |
| E197-19 | finalize builds `dotnet build nextorm.slnx -c Debug` / `-c Release` | both 0 Warning / 0 Error | PASS — `build-debug-part3.log` exit 0, 0W/0E; `build-release-part3.log` exit 0, 0W/0E (benchmark file compiles in both) | DO part 3 |
| E197-P01 | consumer audit of `TypeFacts.IsSingleColumnProjection` (`QueryPreparer.cs:458,467,535,583,634,2007`; `InMemoryExtremeRow.cs:112`) | additive scope confined to the scalar route; no per-row cost | PASS — only `:458`/`:535` newly fire (intended scalar route); `:467`/`:583`/`:2007` are `NewExpression`/`MemberInit` guards unreachable for abstract `JsonNode`; `InMemoryExtremeRow.cs:112` is once-per-enumeration `CompileComponents`, not per row | coder |
| E197-P02 | extend `Visitors/TypeFacts.cs` with exact `t == typeof(JsonNode)` (no `IsAssignableFrom`; no `JsonObject`/`JsonArray`/`JsonValue`/`JsonDocument`/`JsonElement`) | exact-type branch only | PASS — `Visitors/TypeFacts.cs:78` (`using System.Text.Json.Nodes;` at `:2`); no other production file touched | coder |
| E197-P03 | direct internal `TypeFactsTests` + `dotnet test tests/nextorm.core.tests -c Debug` | exit 0, 0 failed | PASS — focused `core-r2-focused.log` exit 0 (36/0/0); full `artifacts/pdca/D197/core-r2.log` exit 0; total 1575 / failed 0 / skipped 0 (+13 = new `TypeFactsTests` cases on the 1562 baseline) | coder |
| E197-P04 | PG `-class …PostgresJsonColumnTests` (scalar criterion) | exit 0, 0 failed, 0 skipped; `BareJsonNode_ScalarProjection_ShouldMaterialize` PASS | PASS — `artifacts/pdca/D197/pg-json-r2.log` exit 0; total 7 / failed 0 / skipped 0; previously failing scalar test now PASS | coder |
| E197-P05 | regressions: PG `…PostgresIntegrationTests`, CH `…ClickHouseIntegrationTests`, `dotnet build nextorm.slnx -c Debug` | PG 0 failed / skips = 25 baseline; CH 0 failed / 0 skipped; build 0W/0E | PASS — `pg-regression-r2.log` exit 0 (617/0/25, same 25 by-design skips as r1); `ch-regression-r2.log` exit 0 (107/0/0); `build-debug-r2.log` exit 0 (Build succeeded, 0 Warning(s) 0 Error(s)) | coder |
| E197-P06 | close the scalar matrix in `BareJsonNode_ScalarProjection_ShouldMaterialize` (physical `json`/`jsonb` × declared `JsonNode`/`JsonNode?`; object/array/number/string/boolean + JSON literal null + SQL NULL; `JsonNode.DeepEquals`), new helper `AssertScalarJsonNodeRoots` | exit 0, 0 failed, 0 skipped | PASS — `artifacts/pdca/D197/pg-json-scalar-matrix.log` exit 0; total 7 / failed 0 / skipped 0; anchors `PostgresJsonColumnTests.cs:317-353` + helper `:475-494`; build 0W/0E | coder |

## Progress log

- pending — DO not started.
- 2026-10-06T05:44Z | DO | r=1 | n=1/3 | STEP 0: status file created, D197 row `in-progress` | `docs/specs/status/{rc1-tail-197-pg-jsonnode-1,collection-1.0.9-rc1-tail}.md`
- 2026-10-06T05:45Z | DO | r=1 | n=1/3 | STEP 2 RED: focused `FullyQualifiedName~SelectExpressionTests` exit 2, total 23 / failed 13 / succeeded 10 (`NotSupportedException` for `JsonNode`) | `artifacts/pdca/D197/red-focused.log`
- 2026-10-06T05:46Z | DO | r=1 | n=1/3 | STEP 1 impl + STEP 2 GREEN: exact-`JsonNode` branch; focused run exit 0, 23/0/23; solution build exit 0, 0W/0E; boundary core run exit 0, 1562/0/0 | `artifacts/pdca/D197/{green-focused,build-debug,green-core-full}.log`
- 2026-10-06T05:46Z | DO | r=1 | n=1/3 | STEP 4 scope/CRLF: `git diff --stat` = planned files only; 4 files CRLF; `code-smells-review.md` unchanged | this file, E197-01..E197-10
- DO complete | r=1 | n=1/3 | this unit's steps done; D197-06 (PG integration) and D197-07 (EN+RU docs) remain separate pending DO units | `artifacts/pdca/D197/*.log`
- 2026-10-06T10:53Z | DO | r=1 | n=1/3 | D197-06 part 2: added bare-`JsonNode` PG integration tests (`PostgresJsonColumnTests.cs:269-471`); build exit 0, 0W/0E | `artifacts/pdca/D197/build-integration.log`
- 2026-10-06T10:53Z | DO | r=1 | n=1/3 | D197-06 RED→partial: `PostgresJsonColumnTests` exit 1, total 7 / failed 2 / skipped 0; scalar `Select(x => x.Data)` fails `Cannot get ctor from JsonNode` (core: `TypeFacts.IsSingleColumnProjection` lacks `JsonNode`); composite/roots/nulls/param/control pass | `artifacts/pdca/D197/pg-json.log`
- 2026-10-06T10:53Z | DO | r=1 | n=1/3 | D197-06 cross-provider guard: ClickHouse exit 0 (107/0/0), PostgresIntegrationTests exit 0 (617/0/25 by-design skips) | `artifacts/pdca/D197/{ch-integration,pg-integration}.log`
- **D197-06 BLOCKED** | r=1 | n=1/3 | PG integration unit not `done`: scalar `JsonNode` projection criterion needs a core `TypeFacts.IsSingleColumnProjection` addition (out of `tests`+`status` scope) — replan/`escalate` required before CHECK | `pg-json.log`, defect F197-JSONNODE-SCALAR-PROJECTION
- 2026-10-06T06:02Z | P (replan) | r=2 | n=1/3 | additive prerequisite **D197-P01** opened; D197-06 stays active with criteria/residual unchanged (blocked on the new dependency, not superseded); consumer audit recorded | this file, defect F197-JSONNODE-SCALAR-PROJECTION
- 2026-10-06T06:02Z | DO | r=2 | n=1/3 | D197-P01 STEP 1+2: exact `JsonNode` branch in `Visitors/TypeFacts.cs:78`; direct `TypeFactsTests`; focused 36/0/0, full core 1575/0/0 | `artifacts/pdca/D197/{core-r2-focused,core-r2}.log`
- 2026-10-06T06:02Z | DO | r=2 | n=1/3 | D197-P01 STEP 3: PG `PostgresJsonColumnTests` exit 0 (7/0/0); previously failing `BareJsonNode_ScalarProjection_ShouldMaterialize` now PASS | `artifacts/pdca/D197/pg-json-r2.log`
- 2026-10-06T06:02Z | DO | r=2 | n=1/3 | D197-P01 STEP 4: PG regression 617/0/25 (= r1 baseline), CH 107/0/0, solution build 0W/0E | `artifacts/pdca/D197/{pg-regression-r2,ch-regression-r2,build-debug-r2}.log`
- **D197-P01 done** | r=2 | n=1/3 | prerequisite complete; `F197-JSONNODE-SCALAR-PROJECTION` resolved; D197-06 now unblocked (evidence complete), re-verification at CHECK; D197-07 docs still pending | this file, E197-P01..P05
- 2026-10-06T11:05Z | DO (part 3) | r=2 | n=1/3 | perf acceptance: `--anyCategories=acceptance` exit 0, 7 executed / 0 failures, shell 53.87 s, BDN 44.69 s; cached/prepared 1.97 vs 1.87, alloc 7.25 vs 7.42 — no regression | `artifacts/pdca/D197/perf-acceptance.log`, `docs/specs/performance/acceptance-benchmarks.md`
- 2026-10-06T11:06Z | DO (part 3) | r=2 | n=1/3 | targeted `D197JsonRead` (`JsonNodeReadBenchmark`, 3 methods × 3 payloads): exit 0, 9/9, BDN 63.02 s; `JsonNode.Parse` 848 B/16.8 KB/219 KB vs `JsonDocument` ~72 B; compiled accessor sub-noise vs bare parse, no per-row reflection | `artifacts/pdca/D197/perf-jsonnode.log`
- 2026-10-06T11:06Z | DO (part 3) | r=2 | n=1/3 | BDN artifacts restored: `git checkout -- BenchmarkDotNet.Artifacts/results` (15 tracked) + removed 3 new `JsonNodeReadBenchmark` reports; artifacts status empty | `git status`, `BenchmarkDotNet.Artifacts/results`
- 2026-10-06T11:07Z | DO (part 3) | r=2 | n=1/3 | docs EN+RU: stale "bare `JsonNode` column not read" corrected in `providers/postgres.md`, `guide/14-json.md`, `guide/provider-specific/postgresql.md` (+ RU mirrors); scalar-functions doc untouched (parameter-only mention) | six docs files
- 2026-10-06T11:08Z | DO (part 3) | r=2 | n=1/3 | finalize: `dotnet build nextorm.slnx -c Debug` 0W/0E and `-c Release` 0W/0E (new benchmark compiles in both); D197-07 → done, D197-08 perf unit → done | `artifacts/pdca/D197/{build-debug-part3,build-release-part3}.log`
- **DO part 3 complete** | r=2 | n=1/3 | E197-15..E197-19 recorded; D197-06 remains active pending CHECK re-verification; no commit (collection auto-commit after CHECK/ACT) | this file
- 2026-10-06T11:16Z | CHECK (re-gather) | r=2 | n=1/3 | consumer-audit list corrected to `QueryPreparer.cs:458,467,535,634,2007` + `InMemoryExtremeRow.cs:112` (removed erroneous `:583`); P1 priority matrix (P1-1..P1-11) added; all 7 provider unit suites exit 0 (core 1575/0/1575/0, sqlite 1059/0/1058/1 env-gated LOB probe, postgres 771/0/771/0, sqlserver 697/0/697/0, mysql 285/0/285/0, mariadb 192/0/192/0, clickhouse 529/0/529/0); CI coverage full-solution collect exit 0 (total 8496 / failed 0 / succeeded 5964 / skipped 2532 no-DOCKER integration), reportgenerator line **87.1 %** / branch **78.8 %** (both > 85/75; branch `1.0.9-rc1` ≠ `main` ⇒ warn-only); no plan/`r`/`n` change | `artifacts/pdca/D197/check-unit-*.log`, `coverage-collect.log`, `coverage/coverage.cobertura.xml`, `coverage/report/Summary.txt`
- 2026-10-06T06:18Z | DO | r=2 | n=1/3 | D197-06 scalar matrix closed: `BareJsonNode_ScalarProjection_ShouldMaterialize` extended to `json`/`jsonb` × `JsonNode`/`JsonNode?` covering object/array/number/string/boolean + JSON literal null + SQL NULL, compared with `JsonNode.DeepEquals`; new helper `AssertScalarJsonNodeRoots`; build 0W/0E; `PostgresJsonColumnTests` exit 0, total 7 / failed 0 / skipped 0 | `artifacts/pdca/D197/pg-json-scalar-matrix.log`, `PostgresJsonColumnTests.cs:317` + `:475`
- 2026-10-06T11:25Z | CHECK | r=2 | n=1/3 | **PASS** (r=2, n=1, rv=2) — E197-01..E197-19 + E197-P01..E197-P06 closed; priority invariants P1-1..P1-11 checked clean; open mandatory evidence = **none**; no plan/`r`/`n` change | this file, E197-01..E197-19, E197-P01..E197-P06, P1-1..P1-11
- 2026-10-06T11:25Z | ACT | r=2 | n=1/3 | finalized: D197-06 ledger → `done`, task `status` → `done`; collection D197 row → `done`; task files staged for auto-commit (no merge, no push); task commit `95849d38` | `docs/specs/status/collection-1.0.9-rc1-tail.md`
