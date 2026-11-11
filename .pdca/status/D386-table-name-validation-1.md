# Cycle status: D386-table-name-validation / cycle 1

## Current state

- task: `D386-table-name-validation` · form: `cycle` · specialized skill: `pdca-dotnet` (overlay `pdca-coder` + `pdca-dotnet`) + project overlay `nextorm-pdca`
- mode: `autonomous`
- cycle: `N=1` · revision: `r=1` · attempt: `n=2/3`
- scope: issue #386 (milestone 21, 1.0.9) — table-name validation on `ctx.From(string)` and all 56 string-table join overloads; one cycle
- goal: call-time argument validation of `table` (`null` → `ArgumentNullException`, `""` / whitespace → `ArgumentException`, `ParamName == "table"`) across 3 `From(string, ...)` forms + 56 string-table join overloads, updated `#384` negative assertions, full-sweep tests, synchronized XML + EN/RU docs — the §PLAN record and §Original task are authoritative
- phase: ACT · record: completed · outcome: accepted (CHECK → ACT) · counters: N=1, r=1, n=2/3
- source: user dispatch (issue #386) + pdca-planner plan r=1; evidence root `.pdca/D386/rv1/`
- criteria: `AC-1`..`AC-7` — exact text in §PLAN record `r1` (source: user dispatch + issue #386), not retyped here

## Original task

TITLE: Core: table-name validation gap — ctx.From(string) and the string-table join overloads reject nothing (null/"" crash later with NullReferenceException, "   " renders invalid SQL)

## Symptom

`nextorm` performs no validation of the table name handed to `ctx.From(string)` or to the string-table join overloads. Any value is accepted at the call site, and the failure (or the invalid SQL) surfaces much later:

| `tableName` | Observed outcome |
|---|---|
| `null` | builder/query construction succeeds; SQL compilation/preparation throws `NullReferenceException` ("Object reference not set to an instance of an object.") because the name resolves to no `From` source (`QueryCommand.FromHasTemporaryTableSource` reaches a null `From`) |
| `""` (empty) | identical to `null`: construction succeeds, `NullReferenceException` at SQL compilation |
| `"   "` (whitespace-only) | accepted by both the explicit `ctx.From("   ")` form and every string-table join overload; renders `join     as 't2'` — syntactically invalid SQL, no exception |

## Repro

Measured on SQLite through the source-less join overloads of `#384` (branch `d384-source-less-joins`, baseline `cb5f316d`), over all 25 table-name cases (`Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`/`CrossApply`/`OuterApply`):

- **Class A — `null` + `""` (18 cases**, including 4 cast/typed-`null` cases**)**: `SqlCompilation` / `System.NullReferenceException`, identical on the string-table form and on the explicit `ctx.From(tableName)` form; the builder call itself does not throw.
- **Class B — `"   "` (5 cases)**: no exception on either form, SQL equality between the two forms, SQL carries `join     as 't2'` (raw whitespace identifier token).
- **Class C — `CrossApply`/`OuterApply` on SQLite (2 cases)**: `NotSupportedException` — a dialect capability refusal, expected and unrelated to this gap.

Evidence: `.pdca/D384/rv2/probe-results.txt` (cycle-local probe artifact of the PDCA cycle that produced `#384`; not a committed file); retained assertions in `tests/nextorm.sqlite.tests/Joins/SourceLessJoinNegativePathsTests.cs`.

## Affected API

- `IDataContext.From(string table)` / `DataContext.From(string)` (`src/nextorm.core/DataContext/DataContext.cs`) and the string overload on `DataContextExtensions`.
- All string-table join overloads on `EntityBuilder`, `EntityBuilder<TEntity>` and `JoinedEntityBuilder<T1..Tn>`:
  `Join`/`LeftJoin`/`RightJoin`/`FullJoin(string table, Expression<...> joinCondition, Action<JoinOptions>? options = null)` and `CrossJoin`/`CrossApply`/`OuterApply(string table, Action<JoinOptions>? options = null)`.
- Every such overload delegates to `ctx.From(table)`, so the gap is engine-wide and not specific to `#384`.

## Expected behaviour

Validate the table name at call time (argument guard), before any builder state is created:

- `null` → `ArgumentNullException` (parameter `table`);
- `""` → `ArgumentException` (parameter `table`);
- whitespace-only `"   "` → `ArgumentException` (parameter `table`) — it is not a table identifier and currently produces invalid SQL.

Both the explicit `ctx.From(...)` form and the string-table join overloads must fail at the same stage with the same exception type and the same parameter name.

## Relation to `#384`

`#384` delivers the convenience overloads with **error parity** to the explicit `ctx.From(...)` form; it deliberately does not change `ctx.From(string)` validation. The `#384` negative suite currently pins the measured outcomes above (Class A: non-throwing builder call, then `NullReferenceException` at SQL compilation; Class B: whitespace accepted with equal SQL). When this guard lands, those parity assertions are updated to the call-time `ArgumentNullException`/`ArgumentException` outcomes — tracked here, same milestone.

Источник: GitHub issue AlexeyShirshov/nextorm#386, milestone 21 (1.0.9), state OPEN

## PLAN record

### r1

# PLAN — D386: Table-name validation on `ctx.From(string)` and string-table join overloads (`#386`)

- **Re-anchor snapshot:** repo `/home/alex/sources/nextorm`, branch `1.0.9`, HEAD `c835ff85` (untracked `timefinal.txt` out of scope and never staged).
- **Status / evidence bindings:** `.pdca/status/D386-table-name-validation-1.md` (`<N> = 1`, `r = 1`), evidence root `.pdca/D386/rv1/**` in `/home/alex/sources/nextorm`.
- **Priority / class:** **Class 2 — Bug fix / argument-validation gap** (exhaustive sweep across all occurrences of the pattern in `src/nextorm.core`, synced tests and EN+RU docs in the same milestone `1.0.9` / milestone 21).

---

## 1. Form, specialized skill, and entry scope

- **Form:** `cycle` (ordinary single cycle).
- **Specialized skill (overlay):** `coder+.NET` (`pdca-coder` + `pdca-dotnet`, with project overlay `nextorm-pdca`).
- **Form rationale:** The validation mechanism (`ArgumentException.ThrowIfNullOrWhiteSpace(table)` BCL guard convention already used across `src/nextorm.core`) is proven and deterministic, and the blast radius is strictly bounded to the 3 `From(string, ...)` entry points, the 56 string-table join overloads across `EntityBuilder`, `EntityBuilder<TEntity>`, and `JoinedEntityBuilder<T1..T7>`, their XML docs, the `#384` negative-path test suite plus exhaustive surface coverage, and the EN/RU `04-joins.md` guide passages.
- **Entry scope:** Whole goal of `#386` in one cycle — call-time `table` argument validation across `ctx.From(string)` (`DataContext.From` and `DataContextExtensions.From` string overloads) and all 56 string-table join overloads, with updated negative-path assertions, exhaustive arity/operator guard coverage, and synchronized XML + EN/RU user documentation.

---

## 2. Original task (verbatim) and result identity

### Original task (verbatim)

TITLE: Core: table-name validation gap — ctx.From(string) and the string-table join overloads reject nothing (null/"" crash later with NullReferenceException, "   " renders invalid SQL)

BODY:
#### Symptom

`nextorm` performs no validation of the table name handed to `ctx.From(string)` or to the string-table join overloads. Any value is accepted at the call site, and the failure (or the invalid SQL) surfaces much later:

| `tableName` | Observed outcome |
|---|---|
| `null` | builder/query construction succeeds; SQL compilation/preparation throws `NullReferenceException` ("Object reference not set to an instance of an object.") because the name resolves to no `From` source (`QueryCommand.FromHasTemporaryTableSource` reaches a null `From`) |
| `""` (empty) | identical to `null`: construction succeeds, `NullReferenceException` at SQL compilation |
| `"   "` (whitespace-only) | accepted by both the explicit `ctx.From("   ")` form and every string-table join overload; renders `join     as 't2'` — syntactically invalid SQL, no exception |

#### Repro

Measured on SQLite through the source-less join overloads of `#384` (branch `d384-source-less-joins`, baseline `cb5f316d`), over all 25 table-name cases (`Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`/`CrossApply`/`OuterApply`):

- **Class A — `null` + `""` (18 cases**, including 4 cast/typed-`null` cases**)**: `SqlCompilation` / `System.NullReferenceException`, identical on the string-table form and on the explicit `ctx.From(tableName)` form; the builder call itself does not throw.
- **Class B — `"   "` (5 cases)**: no exception on either form, SQL equality between the two forms, SQL carries `join     as 't2'` (raw whitespace identifier token).
- **Class C — `CrossApply`/`OuterApply` on SQLite (2 cases)**: `NotSupportedException` — a dialect capability refusal, expected and unrelated to this gap.

Evidence: `.pdca/D384/rv2/probe-results.txt` (cycle-local probe artifact of the PDCA cycle that produced `#384`; not a committed file); retained assertions in `tests/nextorm.sqlite.tests/Joins/SourceLessJoinNegativePathsTests.cs`.

#### Affected API

- `IDataContext.From(string table)` / `DataContext.From(string)` (`src/nextorm.core/DataContext/DataContext.cs`) and the string overload on `DataContextExtensions`.
- All string-table join overloads on `EntityBuilder`, `EntityBuilder<TEntity>` and `JoinedEntityBuilder<T1..Tn>`:
  `Join`/`LeftJoin`/`RightJoin`/`FullJoin(string table, Expression<...> joinCondition, Action<JoinOptions>? options = null)` and `CrossJoin`/`CrossApply`/`OuterApply(string table, Action<JoinOptions>? options = null)`.
- Every such overload delegates to `ctx.From(table)`, so the gap is engine-wide and not specific to `#384`.

#### Expected behaviour

Validate the table name at call time (argument guard), before any builder state is created:

- `null` → `ArgumentNullException` (parameter `table`);
- `""` → `ArgumentException` (parameter `table`);
- whitespace-only `"   "` → `ArgumentException` (parameter `table`) — it is not a table identifier and currently produces invalid SQL.

Both the explicit `ctx.From(...)` form and the string-table join overloads must fail at the same stage with the same exception type and the same parameter name.

#### Relation to `#384`

`#384` delivers the convenience overloads with **error parity** to the explicit `ctx.From(...)` form; it deliberately does not change `ctx.From(string)` validation. The `#384` negative suite currently pins the measured outcomes above (Class A: non-throwing builder call, then `NullReferenceException` at SQL compilation; Class B: whitespace accepted with equal SQL). When this guard lands, those parity assertions are updated to the call-time `ArgumentNullException`/`ArgumentException` outcomes — tracked here, same milestone.

---

### Requested result and delivered result

- **Requested result:** Call-time argument validation of the `table` parameter on `DataContext.From(string)`, `DataContextExtensions.From(IDataContext, string, ...)`, and all string-table join overloads on `EntityBuilder`, `EntityBuilder<TEntity>`, and `JoinedEntityBuilder<T1..T7>` so that `null` throws `ArgumentNullException` (`ParamName == "table"`), `""` throws `ArgumentException` (`ParamName == "table"`), and whitespace-only strings throw `ArgumentException` (`ParamName == "table"`) before any builder state is created, with full call-time error parity between explicit `ctx.From(table)` and string-table join overloads and updated `#384` negative assertions and documentation.
- **Delivered result:** The exact same call-time argument validation behaviour across `DataContext.From(string)`, `DataContextExtensions.From(IDataContext, string, ...)`, and all 56 string-table join overloads on `EntityBuilder`, `EntityBuilder<TEntity>`, and `JoinedEntityBuilder<T1..T7>`, together with updated `#384` parity tests, full-sweep regression tests across every affected surface, and synchronized XML and EN/RU documentation.
### Measurement method

Not applicable — `#386` is a discrete behavioral/contract bug fix judged by deterministic binary acceptance criteria, not a continuous metric campaign.

---

## 3. Acceptance criteria

### Verbatim Expected-behaviour criteria from the issue

- **AC-1 (`null` table guard):** Validate the table name at call time (argument guard), before any builder state is created:
  - `null` → `ArgumentNullException` (parameter `table`);
  - **Negative condition:** Passing `null` (including a typed `(string?)null` cast) to `ctx.From(table)` or any string-table join overload must not return a builder instance, must not defer failure to SQL compilation/preparation (`NullReferenceException`), and must not throw an exception with a `ParamName` other than `"table"`.

- **AC-2 (`""` empty table guard):** Validate the table name at call time (argument guard), before any builder state is created:
  - `""` → `ArgumentException` (parameter `table`);
  - **Negative condition:** Passing `""` (`string.Empty`) to `ctx.From(table)` or any string-table join overload must not return a builder instance, must not throw `ArgumentNullException` (the runtime type must be `ArgumentException`, not its `ArgumentNullException` subclass), must not defer failure to SQL compilation (`NullReferenceException`), and must not report a `ParamName` other than `"table"`.

- **AC-3 (Whitespace-only table guard):** Validate the table name at call time (argument guard), before any builder state is created:
  - whitespace-only `"   "` → `ArgumentException` (parameter `table`) — it is not a table identifier and currently produces invalid SQL.
  - **Negative condition:** Passing any whitespace-only string (e.g., `" "`, `"   "`, `"\t\r\n"`) to `ctx.From(table)` or any string-table join overload must not return a builder instance, must not render SQL containing whitespace table tokens (`join     as 't2'`), must not reach dialect capability checks at SQL compilation (such as SQLite `NotSupportedException` on `CrossApply("   ")` / `OuterApply("   ")`), and must not throw `ArgumentNullException` or report a `ParamName` other than `"table"`.

- **AC-4 (Call-time stage, exception type, and parameter name parity across all surfaces):**
  - Both the explicit `ctx.From(...)` form and the string-table join overloads must fail at the same stage with the same exception type and the same parameter name.
  - This covers every affected entry point without exception:
    1. `DataContext.From(string table)` (`src/nextorm.core/DataContext/DataContext.cs:234`);
    2. `DataContextExtensions.From(this IDataContext dataContext, string table)` (`src/nextorm.core/DataContext/DataContextExtensions.cs:1213`);
    3. `DataContextExtensions.From(this IDataContext dataContext, string table, Action<FromOptions> options)` (`src/nextorm.core/DataContext/DataContextExtensions.cs:1225`);
    4. All 7 string-table join overloads on `EntityBuilder<TEntity>` (`Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply`, `OuterApply` at `EntityBuilder.cs:3884-3942`);
    5. All 7 string-table join overloads on non-generic `EntityBuilder` (`Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply`, `OuterApply` at `EntityBuilder.cs:6350-6387`);
    6. All 42 string-table join overloads on `JoinedEntityBuilder<T1..T7>` (6 arities `T1..T2` through `T1..T7` × 7 operators `Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply`, `OuterApply` at `JoinedEntityBuilder.cs:135-1985`).
  - **Negative condition:** No overload in the 3 `From(string, ...)` forms or the 56 string-table join overloads may remain unguarded or fail at a later stage (e.g., inside `options` invocation, expression visitation, or SQL compilation) when `table` is `null`, `""`, or whitespace-only.

- **AC-5 (Preservation of valid table names and non-table builder paths):**
  - Valid non-whitespace table names (including schema-qualified names or names with surrounding non-empty identifier characters) continue to construct builders and compile SQL unchanged; internal/type-only/deferred-table paths (`new EntityBuilder(IDataContext)`, `EntityBuilder.CloneImp`, `From<TEntity>()`, and CTE query builders) continue to function without regression.
  - **Negative condition:** Guarding `From(string)` and string-table join overloads must not break `EntityBuilder(IDataContext)` / `CloneImp` when `_table` is `null`, must not alter `CteQuery.From(string cteName)` / `MutationCteQuery.FromTable` parameter-name contracts, and must not break any existing unit or dialect test.

- **AC-6 (`#384` negative suite update and red↔green regression coverage):**
  - The retained `#384` negative parity assertions in `tests/nextorm.sqlite.tests/Joins/SourceLessJoinNegativePathsTests.cs` (all 25 table-name cases across `Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply`, `OuterApply`, including typed-`null` casts and `CrossApply`/`OuterApply` whitespace cases) are updated to assert call-time `ArgumentNullException` (for `null`) and `ArgumentException` (for `""` and `"   "`) with `ParamName == "table"` on both the string-table overload and the explicit `ctx.From(table)` form; and automated tests cover all affected surface groups (`DataContext.From`, `DataContextExtensions.From` both overloads, `EntityBuilder<TEntity>`, non-generic `EntityBuilder`, and all 6 arities `JoinedEntityBuilder<T1..T2>` through `JoinedEntityBuilder<T1..T7>` × 7 operators).
  - **Negative condition:** No test may retain expectations of `NullReferenceException` at SQL compilation or accepted whitespace SQL for `From(string)` / string-table joins, and no assertion in `tests/**` may violate `AssertionStyleTests` (FluentAssertions only) or `IntegrationSuiteScopingTests`.

- **AC-7 (Synchronized XML docs and EN/RU guide documentation):**
  - Public guide pages `docs/guide/04-joins.md` (`:484-488`, `:976-977`) and `docs/ru/guide/04-joins.md` (`:497-502`, `:994-996`), as well as the XML documentation comments on the affected `From(string)` and string-table join members in `DataContext.cs`, `DataContextExtensions.cs`, `EntityBuilder.cs`, and `JoinedEntityBuilder.cs`, are updated to state the call-time `ArgumentNullException` / `ArgumentException` (`table`) behaviour and build cleanly without DocFX or compiler warnings.
  - **Negative condition:** Neither `docs/**`, `docs/ru/**`, nor XML doc comments may state that `null`, `""`, or whitespace-only `table` arguments succeed at builder construction, throw `NullReferenceException` at SQL compilation, or render whitespace SQL; public docs must not link to `docs/specs/**` or `.pdca/**`.

---

## 4. Resolution of decision-relevant unknowns (Gaps → Evidence / Decision)

1. **Guard placement consequence vs. CTE surfaces and non-generic `EntityBuilder` ctor:**
   - *Facts:* `EntityBuilder(IDataContext, string? table)` (`EntityBuilder.cs:6103`) explicitly accepts `null` for `EntityBuilder(IDataContext)` (`:6099`) and `CloneImp` (`:6178`). Generic `EntityBuilder<TEntity>(IDataContext, string table)` (`:363`) is called by `DataContextExtensions.From`, `EntityBuilder<TEntity>` joins, `JoinedEntityBuilder<T1..T7>` joins, and also by `CteQuery.From(string cteName)` (`:155`) and `MutationCteQuery.FromTable(string tableName)` (`:70`).
   - *Decision:* The contract of `#386` covers `DataContext.From(string)`, `DataContextExtensions.From(IDataContext, string, ...)`, and all 56 string-table join overloads on `EntityBuilder`, `EntityBuilder<TEntity>`, and `JoinedEntityBuilder<T1..T7>`, where the parameter name is `table`. Guarding at the entry surfaces (or guarding `DataContext.From(string)`, `DataContextExtensions.From` overloads, and the string-table join entry points / `EntityBuilder<TEntity>(IDataContext, string table)` while ensuring `CteQuery.From(string cteName)` and `MutationCteQuery.FromTable(string tableName)` validate their own parameter names first if the generic ctor is also guarded) ensures `ParamName == "table"` on every `#386` surface without breaking non-generic `EntityBuilder(IDataContext, string? table)` or CTE parameter names. CTE-name APIs are distinct surfaces not in `#386`, so nothing from `#386` is deferred out of milestone 21.
2. **Documentation obligation:**
   - *Facts:* `docs/guide/04-joins.md:484-488, 976-977`, `docs/ru/guide/04-joins.md:497-502, 994-996`, and XML doc comments in `EntityBuilder.cs` / `JoinedEntityBuilder.cs` explicitly describe the pre-`#386` `NullReferenceException` / whitespace behaviour.
   - *Decision:* Updating EN + RU guide pages and XML doc comments on the affected overloads is **mandatory in-scope work** (AC-7) so that code, XML docs, and EN/RU user docs remain truthful and synchronized in the same change.
3. **Regression-test shape (red↔green):**
   - *Decision:* Every affected surface must fail before the guard and pass after the guard when asserted for call-time `ArgumentNullException` (`null`, `ParamName == "table"`) and exact `ArgumentException` (`""` and `"   "`, `ParamName == "table"`). Updating `SourceLessJoinNegativePathsTests.cs` from old outcomes to call-time guard assertions plus adding full-sweep coverage over `DataContext.From`, `DataContextExtensions.From`, non-generic `EntityBuilder` joins, and all 6 arities of `JoinedEntityBuilder<T1..T7>` × 7 operators provides direct red↔green proof.
4. **Class C (`CrossApply`/`OuterApply` with `"   "` on SQLite):**
   - *Facts:* Previously, `CrossApply("   ")` and `OuterApply("   ")` did not throw at call time and therefore reached SQL compilation, where SQLite threw `NotSupportedException` (Class C in `#384`). Meanwhile, `CrossApply(null)` / `CrossApply("")` threw `NullReferenceException` before SQLite's `APPLY` check.
   - *Decision:* Because `#386` validates `table` at call time before any builder state is created, `CrossApply("   ")` and `OuterApply("   ")` now throw `ArgumentException` (`ParamName == "table"`) at the builder call site, before SQL compilation or dialect capability checks are ever reached. All 25 invalid-table-name cases in `SourceLessJoinNegativePathsTests.cs` therefore fail at the builder call site (`BuilderCall` stage).

---

## 5. Variant matrix

Axes: **Surface group** × **Input kind** (`null` literal, typed `(string?)null` cast, `""` empty, `"   "` whitespace-only, valid non-empty table name `"users"`).

| # | Surface group | Declarations | `null` / typed `(string?)null` | `""` (`string.Empty`) | `"   "` (whitespace-only) | Valid `"users"` | Status |
|---|---|---|---|---|---|---|---|
| 1 | `DataContext.From(string table)` (`DataContext.cs:234`) | 1 | Guard + test: call-time `ArgumentNullException` (`ParamName == "table"`) | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) | Test: returns non-null `EntityBuilder` | In scope (guard + test) |
| 2 | `DataContextExtensions.From(this IDataContext, string table)` (`DataContextExtensions.cs:1213`) | 1 | Guard + test: call-time `ArgumentNullException` (`ParamName == "table"`) | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) | Test: returns `EntityBuilder<TableAlias>` | In scope (guard + test) |
| 3 | `DataContextExtensions.From(this IDataContext, string table, Action<FromOptions> options)` (`DataContextExtensions.cs:1225`) | 1 | Guard + test: call-time `ArgumentNullException` (`ParamName == "table"`), before `options` runs | Guard + test: call-time `ArgumentException` (`ParamName == "table"`), before `options` runs | Guard + test: call-time `ArgumentException` (`ParamName == "table"`), before `options` runs | Test: invokes `options` and returns `EntityBuilder<TableAlias>` | In scope (guard + test) |
| 4 | `EntityBuilder<TEntity>` string-table joins (`Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply`, `OuterApply` at `EntityBuilder.cs:3884-3942`) | 7 | Guard + test: call-time `ArgumentNullException` (`ParamName == "table"`), parity with `ctx.From(table)` | Guard + test: call-time `ArgumentException` (`ParamName == "table"`), parity with `ctx.From(table)` | Guard + test: call-time `ArgumentException` (`ParamName == "table"`), including `CrossApply`/`OuterApply` before SQL compilation | Covered by existing positive suite | In scope (guard + test) |
| 5 | Non-generic `EntityBuilder` string-table joins (`Join`, `LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply`, `OuterApply` at `EntityBuilder.cs:6350-6387`) | 7 | Guard + test: call-time `ArgumentNullException` (`ParamName == "table"`) | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) | Covered by existing positive suite | In scope (guard + test) |
| 6 | `JoinedEntityBuilder<T1..T7>` string-table joins (arities 2..7 × 7 ops at `JoinedEntityBuilder.cs:135-1985`) | 42 | Guard + test: call-time `ArgumentNullException` (`ParamName == "table"`) across all 6 arities × 7 ops | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) across all 6 arities × 7 ops | Guard + test: call-time `ArgumentException` (`ParamName == "table"`) across all 6 arities × 7 ops | Covered by existing positive suite | In scope (guard + test) |
| 7 | Non-generic `EntityBuilder(IDataContext)` / `CloneImp` deferred-null table path (`EntityBuilder.cs:6099, 6103, 6178`) | internal | N/A (not a `From(string)` or string-table join call) | N/A | N/A | Preserved unchanged | In scope (regression guard: must not break) |

*(Zero rows deferred; every surface in `#386` is guarded and tested in this cycle.)*

---

## 6. Minimal solution and alternatives

- **Chosen minimal solution:**
  1. Use `ArgumentException.ThrowIfNullOrWhiteSpace(table)` (the established BCL helper in `src/nextorm.core`, which throws `ArgumentNullException` when `table is null` and `ArgumentException` when `table` is `""` or whitespace-only, capturing `nameof(table)` = `"table"` via `[CallerArgumentExpression]`) at the call-time entry points:
     - `DataContext.From(string table)` (`DataContext.cs:234`);
     - `DataContextExtensions.From(this IDataContext dataContext, string table)` (`DataContextExtensions.cs:1213`) and `DataContextExtensions.From(this IDataContext dataContext, string table, Action<FromOptions> options)` (`DataContextExtensions.cs:1228`, replacing `ArgumentNullException.ThrowIfNull(table)` with `ArgumentException.ThrowIfNullOrWhiteSpace(table)`);
     - All string-table join overloads on `EntityBuilder<TEntity>` (7), `EntityBuilder` (7), and `JoinedEntityBuilder<T1..T7>` (42) — either directly at each overload entry and/or in `EntityBuilder<TEntity>(IDataContext dataProvider, string table)` (while ensuring `CteQuery.From(string cteName)` and `MutationCteQuery.FromTable(string tableName)` validate their own parameter names first so their `ParamName` is unaffected). Placing `ArgumentException.ThrowIfNullOrWhiteSpace(table)` at the public entry points and `EntityBuilder<TEntity>(IDataContext, string table)` guarantees that validation runs before any builder or `From` state is allocated and before any `dataProvider` dereference in `DataContextExtensions.From`.
  2. Update `tests/nextorm.sqlite.tests/Joins/SourceLessJoinNegativePathsTests.cs` to expect call-time `ArgumentNullException` / `ArgumentException` with `ParamName == "table"` across all 25 table-name cases, and add unit coverage across `DataContext.From`, `DataContextExtensions.From`, non-generic `EntityBuilder` joins, and all 6 arities of `JoinedEntityBuilder<T1..T7>` × 7 operators.
  3. Update XML docs on `DataContext.From`, `DataContextExtensions.From`, `EntityBuilder`, and `JoinedEntityBuilder`, and update `docs/guide/04-joins.md` and `docs/ru/guide/04-joins.md`.
- **Rejected alternative:** Guarding inside `EntityBuilder(IDataContext, string? table)` (non-generic constructor) or `EntityBuilder.ResolveSource()`.
  - *Why rejected:* Guarding the non-generic `(IDataContext, string? table)` constructor breaks `new EntityBuilder(IDataContext)` and `CloneImp()` when `_table` is legitimately `null`; guarding in `ResolveSource()` defers validation past builder creation on `ctx.From(table)` and fails the requirement to validate at call time before any builder state is created.

---

## 7. Constraints, assumptions, and resource bounds

- **Constraints:**
  - Single milestone `1.0.9` (milestone 21): no scope deferral allowed.
  - `TreatWarningsAsErrors=true` across all projects and configurations; nullable reference types enabled.
  - FluentAssertions-only in `tests/**` (`AssertionStyleTests`); no non-DB tests in `tests/nextorm.*integration.tests` (`IntegrationSuiteScopingTests`).
  - Source folder cap: max 10 `.cs` files per folder in `src/**` and `tests/**`.
  - Line endings: preserve CRLF across all edited files.
  - Public docs (`docs/**`, `docs/ru/**`, `readme.md`) must never link to `docs/specs/**` or `.pdca/**`.
  - Git policy: autonomous cycle commits only the cycle's own modified paths after an accepted CHECK; never stage untracked `timefinal.txt`; `git push` is forbidden; issue `#386` is closed via `gh issue close` in ACT upon whole-goal acceptance.
- **Assumptions:** None that affect the accepted outcome. All target files and call sites are verified in the supplied facts.
- **Resource & access constraints:** Local .NET 11 SDK build and unit/dialect test projects (`nextorm.core.tests`, `nextorm.sqlite.tests`) require no external containers; no resource blocker exists.

---

## 8. Risks and stop conditions

- **Risk 1 — Parameter name mismatch (`ParamName` != `"table"`):** If a guard is reached via a helper or constructor whose parameter is not named `table` without passing `"table"` explicitly, `ParamName` could differ from `"table"`. Mitigation: AC-1..AC-4 require asserting `.ParamName.Should().Be("table")` on every surface group.
- **Risk 2 — `ArgumentNullException` vs `ArgumentException` hierarchy:** In .NET, `ArgumentNullException` inherits from `ArgumentException`, so a loose `.Throw<ArgumentException>()` would pass even if `ArgumentNullException` were thrown for `""` or `"   "`. Mitigation: AC-2 and AC-3 require exact `ArgumentException` (not `ArgumentNullException`) for empty and whitespace inputs (`ThrowExactly<ArgumentException>()`).
- **Risk 3 — `CteQuery.From(string cteName)` / `MutationCteQuery.FromTable(string tableName)` side effect:** If `EntityBuilder<TEntity>(IDataContext, string table)` is guarded, calls from `CteQuery.From(null/""/"   ")` would hit `table` unless `CteQuery.From` validates `cteName` first. Mitigation: AC-5 requires preserving existing CTE tests and parameter contracts.
- **Stop conditions:**
  - Standard inner-loop cap: at most 3 DO→CHECK attempts (`n <= 3`) per plan revision before escalation to `pdca-escalate`.
  - Any convention failure (`AssertionStyleTests`, `IntegrationSuiteScopingTests`, folder file-count cap, CRLF check, or compiler/DocFX warning under `TreatWarningsAsErrors=true`) blocks acceptance until resolved.

---

## 9. Plan-to-DO handoff

- **Carry by reference into DO and CHECK:**
  - Original task (Section 2 verbatim);
  - Acceptance criteria `AC-1` through `AC-7` (Section 3);
  - Resolution of unknowns (Section 4) and Variant Matrix rows 1–7 (Section 5);
  - Constraints, prohibitions, and stop conditions (Sections 7–8).
- **Prohibitions for DO:**
  - Do not stage or touch `timefinal.txt`.
  - Do not run `git push`.
  - Do not add links to `docs/specs/**` or `.pdca/**` in `docs/**`, `docs/ru/**`, or XML docs.
  - Do not use `Xunit.Assert` (except `Assert.Skip*`) in any test.

---

## 10. Confidence

- **Verified from supplied facts:** All 56 string-table join overload locations (`EntityBuilder.cs`, `JoinedEntityBuilder.cs`), all 3 `From(string, ...)` locations (`DataContext.cs`, `DataContextExtensions.cs`), constructor call graph (`EntityBuilder.cs:363`, `:6103`), `#384` negative test locations (`SourceLessJoinNegativePathsTests.cs`), and EN/RU documentation locations (`docs/guide/04-joins.md`, `docs/ru/guide/04-joins.md`).
- **Remaining assumptions:** None.

## Approval record

autonomous — approval gate not applicable (dispatch: mode autonomous, commit_policy auto)

Commit policy: after an accepted CHECK the executor commits ONLY the cycle's own paths (`git add <paths>`, never `-A`), commit message names the task and cycle, tip recorded in evidence; `git push` is FORBIDDEN; unrelated changes (untracked `timefinal.txt`, `.pdca/scripts/` copies) are never staged.

On whole-goal acceptance ACT closes `#386` via `gh issue close 386 --repo AlexeyShirshov/nextorm --comment "<итог + ссылка на проверку>"`; a failed close is a `Notice:` and never a false "closed".

## DO result

Completed candidate (no verdict): call-time `table` argument validation across `DataContext.From(string)`, `DataContextExtensions.From` (both overloads) and all 56 string-table join overloads, updated `#384` negative parity assertions, full-sweep tests, synchronized XML + EN/RU docs.

Execution units:
| ID | state | criteria covered | dependency/pointer |
|---|---|---|---|
| U1 | completed | AC-1..AC-7, VM-1..VM-7 | guard in `src/**` + tests/docs; evidence `.pdca/D386/rv1/` |

Criteria coverage: AC-1..AC-7 met (candidate); VM-1..VM-7 met (candidate). Negative conditions recorded in tests; red↔green evidence persisted.

Changed artefacts (Task-1 ledger, 10 paths):
- docs/guide/04-joins.md
- docs/ru/guide/04-joins.md
- src/nextorm.core/Builders/Cte/CteQuery.cs
- src/nextorm.core/Builders/Cte/MutationCteQuery.cs
- src/nextorm.core/Builders/Entity/EntityBuilder.cs
- src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs
- src/nextorm.core/DataContext/DataContext.cs
- src/nextorm.core/DataContext/DataContextExtensions.cs
- tests/nextorm.sqlite.tests/Joins/SourceLessJoinNegativePathsTests.cs
- tests/nextorm.core.tests/TableGuardSweepTests.cs

Evidence (exit status + key numbers + log path):
- guard patch: `.pdca/D386/rv1/guard-src.patch` sha256 `afc25b9f73311488a9ab759f7c8661960f9fe3ed81f4421f494667eb1abf3857`, 61451 bytes
- RED (guard reverted): core selector rc 2, 19/19 failed (no exception thrown); sqlite selector rc 2, 25/32 failed (no exception thrown) + 7 passed; logs `.pdca/D386/rv1/red-core.log`, `.pdca/D386/rv1/red-sqlite.log`
- patch restored: sha256 equal `afc25b9f...`, rc 0
- GREEN: core selector rc 0, 19/19 passed; sqlite selector rc 0, 32/32 passed; logs `.pdca/D386/rv1/green-core.log`, `.pdca/D386/rv1/green-sqlite.log`
- full core project: rc 0, 4024 passed / 0 failed / 0 skipped; log `.pdca/D386/rv1/full-core.log`
- full sqlite project: rc 0, 2385 passed / 0 failed / 1 skipped; log `.pdca/D386/rv1/full-sqlite.log`
- solution build: rc 0, 0 warnings / 0 errors; log `.pdca/D386/rv1/build-solution.log`
- manifest: `.pdca/D386/rv1/manifest.json`; `validate_inner_loop.py manifest` rc 0; `pdca_evidence.py lint` rc 0

Blockers: none.
Unfinished: CHECK not run; issue #386 close deferred to ACT.
F-03 correction (append-only; earlier RED line unchanged): the 19 core RED failures were 10 ArgumentNullException-expected-with-no-exception, 8 ArgumentException-expected-with-no-exception, 1 arity-8 NotSupportedException.

## CHECK record

CHECK (1) verdict: not accepted → DO evidence round (not a counted attempt; n=1/3). Findings: F-01, F-02, F-03, F-04; VM-8, VM-9, VM-10 (CHECK-added rows). Route CHECK → DO (evidence round). Full CHECK report not persisted as a separate artefact; verdict/findings recorded from CHECK return.

CHECK (2) verdict: not accepted → DO. Findings: F-02 (manifest VM-6 description), F-05 (three mislabeled test surfaces), F-06 (evidence identity drift). Route CHECK → DO. Attempt incremented to n=2/3. Process defect D386-PROC-1 recorded (docs/specs/design/D386-pdca-process-defects.md). Validators rc 0.

CHECK (3) verdict: not accepted → DO (evidence packaging only; not a counted attempt). Matrix outcome: AC-1…AC-7 and VM-1…VM-10 all met on the staged sources and attempts/n2/ logs; the single open row is P-1 (mandatory process record) unverified because the spec was outside CHECK's readable package.

Packaging done this round (evidence packaging only; no product/test correction, no test rerun):
- spec staged byte-identically at `.pdca/D386/rv1/staged/sources/docs/specs/design/D386-pdca-process-defects.md` and `.pdca/D386/rv1/staged/new-files/D386-pdca-process-defects.md`; staged spec sha256 `e82d65a3e60ff184057a0294062dd1cbbb3b2bdae0205cfbdc5c3daa55ffc00c` (29 lines, 3348 bytes); recorded in `staged-manifest.json` with `untracked_new_file: true`.
- manifest `VM-1` and `VM-5` evidence paths and reason text repointed to `attempts/n2/sweep-sqlite.log` and `tests/nextorm.sqlite.tests/TableGuardSweepTests.cs`; all 17 manifest `rows[].evidence` paths verified present on disk (none missing).
- validators re-run: `validate_inner_loop.py manifest` rc 0, `pdca_evidence.py lint` rc 0; logged in `attempts/n2/validators.log` (UTC 2026-11-11T11:34:01Z).

CHECK finding SLOP-1 (recorded, not fixed): low severity / confirmed / not a blocking criterion — unused lookup in `tests/nextorm.core.tests/TableGuardSweepTests.cs` around lines 116–117 and a type-presence-only test around lines 242–247; counted at least 8/499 added lines (1.60 %). Left unchanged because it is not a mandatory requirement of the original task (#386 prescribes no test-quality threshold), CHECK supplied no acceptance threshold, and the transition it authorized was packaging only; the finding stays visible for the retrospective. Not claimed fixed.

Note: CHECK (1) report was recorded as a summary (no separate artefact) — not reconstructed here.

Источник: pdca-check, re-CHECK (4), принят; полный отчёт приведён ниже дословно.

### CHECK 4 — accepted (verbatim)

## Verdict

**accepted** — the staged process record closes P-1, the manifest corrections close F-02, and the evidence covers the whole-goal scope of **#386 in this single cycle**, without deferral.

Counters read from status: **N=1, r=1, n=2/3**; this packaging round does not increment the attempt.

## Criteria matrix

Paths below are relative to `.pdca/D386/rv1/`.

- **C** = `staged/sources/tests/nextorm.core.tests/TableGuardSweepTests.cs`
- **S** = `staged/sources/tests/nextorm.sqlite.tests/TableGuardSweepTests.cs`
- **J** = `staged/sources/tests/nextorm.sqlite.tests/Joins/SourceLessJoinNegativePathsTests.cs`
- **D** = `staged/working-tree.diff`
- **P** = `staged/sources/docs/specs/design/D386-pdca-process-defects.md`
- **n2/** = `attempts/n2/`

| ID | Status | Basis and concrete evidence |
|---|---|---|
| AC-1 | met | Exact `ArgumentNullException`, `"table"`; C:15–60, S:15–57; n2 sweep logs. |
| AC-2 | met | Exact `ArgumentException`, excluding its null subclass; C:15–26, S:15–26; n2 sweep logs. |
| AC-3 | met | Whitespace rejected during construction; same assertions and sweeps; guard implementation D:100–111, 513–559. |
| AC-4 | met | Three From forms and all 56 declared joins covered; C:27–135, S:27–57, J:89–174. Inherited arity-8 coverage also present. |
| AC-5 | met | Valid-name/deferred-null/CTE preservation: C:179–239, S:34–73; n2/full-core.log:27–33 and full-sqlite.log:6–12. Valid forwarding paths remain unchanged in D. |
| AC-6 | met | All 25 table-name cases updated: J:89–174; historical red/green logs establish regression sensitivity; current suites pass. Conventions: conventions.log:19–33. |
| AC-7 | met | Synchronized EN/RU and XML changes in D; n2/build-solution.log:46–48; docfx.log:46–51 records zero warnings/errors, rc 0. |
| VM-1 | met | Concrete DataContext invalid/valid cases: S:23–40; n2/sweep-sqlite.log:3–9; corrected manifest.json:185–195. |
| VM-2 | met | Extension From guard/valid cases: C:23–43, 190–205; n2/sweep-core.log:19–25. |
| VM-3 | met | Guard before callback; valid callback exactly once: C:27–43, 179–187; same core sweep. |
| VM-4 | met | Seven generic join operators × three invalid inputs: C:46–60; same core sweep. |
| VM-5 | met | Seven concrete non-generic joins × three invalid inputs: S:42–57; n2/sweep-sqlite.log:3–9; corrected manifest.json:237–247. |
| VM-6 | met | Six arities 2–7 × seven operators × three inputs: C:63–135; core sweep. |
| VM-7 | met | Deferred-null construction/cloning and valid CTE paths: C:208–232; full core suite. |
| VM-8 | met | Arity 8 included: C:81–135; core sweep; historical red-core.log:83–96 shows the unguarded capability refusal instead. |
| VM-9 | met | Own `"cteName"` exception contracts and valid paths: C:157–177, 208–217; core sweep. |
| VM-10 | met | Direct generic constructor invalid/valid cases: C:147–155, 235–239; core sweep. |
| P-1 | met | P:10–29 records key, symptom, cause, attempt impact, evidence and prevention; status §Defect history links it. Both staged copies contain the same text; staged-manifest.json:98–114 registers them as new files. |

**Observed run results:** core sweep **28 passed**; SQLite sweep **8 passed**; full core **4033 passed, zero skipped**; full SQLite **2393 passed, one skipped**, zero failures. The manifest records rc 0 for these runs and the solution build. Both validator commands record rc 0 in `n2/validators.log:1–3`.

## Findings

| Finding | Status and disposition |
|---|---|
| **F-02 residual** | **Confirmed historically; resolved, closed.** VM-1/VM-5 now point to the concrete SQLite sweep, matching S and its eight passing cases. |
| **P-1** | **Resolved, closed.** The previously inaccessible mandatory process record is now readable and contains the required fields. This closes the evidence gap, not the historical process defect. |
| **SLOP-1** | **Confirmed, low severity, non-blocking; not fixed.** C:116–117 contains an unused lookup; C:242–247 checks only type presence. The previously reported ≥8/499 lines, 1.60%, remains recorded, not independently recalculated here. No acceptance threshold prohibits it, and actual convention execution exists separately. |

## Criteria assessment and verification limits

The acceptance set covers the original behavior, complete affected surface, preservation, regression tests and synchronized documentation; no acceptance-blocking contradiction or ambiguity was found. VM-8–10 are verification rows supporting that scope, not replacement criteria.

No audit skills were loaded this round; direct read-only inspection of staged artifacts and underlying logs substituted. No commands, proving runs, product research or symbol rediscovery were performed.

Limits:

- Hash equality is **recorded**, not independently recomputed; matching identities alone were not used as correctness proof.
- Exact n2 command invocations are absent from the raw logs; their results and manifest exit statuses are visible.
- Historical red/green and DocFX evidence is retained, not represented as a rerun of the current test inventory.
- The single SQLite skip is the explicitly optional LOB probe (`full-sqlite.log:3–4`), unrelated to table guards; no skipped behavior is claimed verified.
- Container/provider-wide runtime behavior was not newly tested.

**Confidence:** high for closure of the packaging findings and the scoped #386 result, subject to the recorded run-to-candidate provenance.

## Next authorized transition

**→ ACT**, because every mandatory row is met. Persist this complete CHECK report and finalize the cycle; no further correction or test rerun is requested. This verdict does not itself perform or establish an issue closure, commit or push.

## ACT record

Outcome: `accepted` (CHECK → ACT, whole-goal scope of #386 covered in this single cycle, nothing deferred out of milestone 21). Completed work: call-time `table` guard on the 3 `From(string…)` forms, all 56 string-table join overloads (3 builder families, arities 2–8 inherited), CTE entry points given their own parameter names, `#384` parity suite updated to call-time outcomes, sweep tests over every surface (core 28 + sqlite 8 cases), EN/RU guide + XML docs synchronised, process-defect spec `D386-PROC-1`. Residual/known issues: `SLOP-1` open, low severity, non-blocking (unused lookup + type-presence-only test), not fixed — recorded as accepted-with-reason (not a task requirement, no threshold) so it is not silently dropped. Limitations: single SQLite opt-in LOB skip (unrelated); no provider-wide runtime run; guard RED/GREEN was executed on the product source before the test-only corrections (source bytes unchanged since). Handoff/next authorized action: none required for #386; a future cycle may address `SLOP-1` only if a maintainer wants it. No `PASS` wording beyond the accepted CHECK; no new verdict.

## Lessons learned

### Ретроспектива D386

**Что сработало:** по записям DO, сочетание RED/GREEN, полного свипа, проверок соглашений и синхронизации EN/RU/XML обеспечило разносторонние свидетельства. Разделение исторических логов и `attempts/n2/` сделало исправленный пакет понятнее.

### Уроки

1. **Категория: идентичность свидетельств.**  
   **Наблюдение:** статус заявлял 4038 успешных core-тестов, тогда как связанный лог содержал 4024; дефект `D386-PROC-1` сопровождался повторным прогоном на `n=2/3`.  
   **На будущее:** сохранять отдельный неизменяемый лог каждого прогона, связывая его с кандидатом и конкретной записью манифеста.  
   **Основание:** `.pdca/D386/rv1/staged/sources/docs/specs/design/D386-pdca-process-defects.md:13–29`.

2. **Категория: семантическое покрытие.**  
   **Наблюдение:** CHECK выявил три неверно обозначенные тестовые поверхности; следующая попытка добавила исправленные проверки, включая concrete `DataContext.From` и non-generic builder.  
   **Вывод:** название теста и общий зелёный результат не доказывают проверку нужного API. При подготовке свипа следует явно сопоставлять поверхность, фактический вызов и проверяемое поведение.  
   **Основание:** `.pdca/status/D386-table-name-validation-1.md:326,366,400–406`.

3. **Категория: комплектность пакета и сохранность отчётов.**  
   **Наблюдение:** потребовался отдельный упаковочный раунд: обязательная спецификация была недоступна CHECK, ссылки манифеста нуждались в исправлении. Полный первый CHECK-отчёт отдельно не сохранён.  
   **На будущее:** включать все обязательные материалы в читаемый пакет до передачи и сохранять полные исходные фазовые отчёты, а не только пересказ. Это уменьшит неопределённость при последующем анализе.  
   **Основание:** тот же статус, строки `324,328–333`.

### Ограничения и неопределённость

Новых проверок не выполнялось. Анализ основан на сохранённых фазовых записях и процессной спецификации. Полные CHECK-отчёты в прочитанном статусе отсутствуют; четвёртый CHECK указан только в брифе, а статус заканчивается упаковочным раундом. Поэтому полнота истории ограничена.

`SLOP-1` остаётся явно незакрытым замечанием низкой тяжести; исправление ему не приписывается (`статус:335`).

**Сигнал завершения: `retrospective_complete`.**

## Revision history

none

## Defect history

- `D386-PROC-1` (alias `EVIDENCE-LOG-IDENTITY-DRIFT`, linked to CHECK finding `F-06`) — evidence-log identity drift across attempts; spec `docs/specs/design/D386-pdca-process-defects.md`.

## Changed files

Task-owned artefacts (Task-1 ledger; `timefinal.txt` and `.pdca/**` evidence excluded):
- docs/guide/04-joins.md — guide update (AC-7)
- docs/ru/guide/04-joins.md — guide update (AC-7)
- src/nextorm.core/Builders/Cte/CteQuery.cs — own-parameter guard
- src/nextorm.core/Builders/Cte/MutationCteQuery.cs — own-parameter guard
- src/nextorm.core/Builders/Entity/EntityBuilder.cs — guards + XML docs
- src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs — guards + XML docs
- src/nextorm.core/DataContext/DataContext.cs — From guard (line 234)
- src/nextorm.core/DataContext/DataContextExtensions.cs — From guards (lines 1213, 1228)
- tests/nextorm.sqlite.tests/Joins/SourceLessJoinNegativePathsTests.cs — updated parity assertions
- tests/nextorm.core.tests/TableGuardSweepTests.cs — new full-sweep tests
- tests/nextorm.sqlite.tests/TableGuardSweepTests.cs — concrete DataContext.From + non-generic builder guards (n=2/3)

This round (evidence round) test additions/updates:
- tests/nextorm.core.tests/TableGuardSweepTests.cs — re-staged (changed this round); VM-8/9/10 closure tests (JoinedEntityBuilder_AllArityOps_Guard arity 8, CteQuery_From_Guards, MutationCteQuery_FromTable_Guards, EntityBuilder_Generic_Ctor_Guards) and AC-5 preservation tests (Ac5_ValidTableNames_Construct, Ac5_Cte_From_ValidNames, Ac5_DeferredNull_Preserved, Ac5_DirectCtor_ValidName, Ac5_AssertionStyle_And_Scoping_Present).

## Git state

status path: `.pdca/status/D386-table-name-validation-1.md` — modified this cycle · untracked (durable on disk, not committed); no commit made in this cycle. Baseline HEAD `c835ff85`.
Touched-file ledger (Task-1): 10 modified/added paths (docs 2, src 6, tests 2; see Changed files); `timefinal.txt` remains untracked and unstaged; no other `.pdca/**` path touched by product work (evidence under `.pdca/D386/rv1/` is local/ignored). Evidence root `.pdca/D386/rv1/`.
Git state (this round): status file untracked, evidence root ignored, no commit.
Post-commit: commit sha 196f4b56 (full 196f4b56825bc0f1164bfc8a9847bdddc60acdb6), branch 1.0.9, 13 files committed; this single progress-log append is the only post-commit delta (uncommitted).
## Progress log

append-only, one row per event, never edited retroactively:

2026-11-11T10:21:38Z | PLAN | revision r1 | attempt n1 | plan persisted | .pdca/status/D386-table-name-validation-1.md
2026-11-11T10:48:42Z | DO | revision r1 | attempt n1 | DO started | .pdca/D386/rv1/
2026-11-11T10:50:00Z | DO | revision r1 | attempt n1 | DO result recorded (manifest validated; validators rc 0) | .pdca/D386/rv1/manifest.json

2026-11-11T11:07:21Z | CHECK | revision r1 | attempt n1/3 | CHECK (1) verdict: not accepted → DO evidence round (not a counted attempt; n=1/3); findings F-01, F-02, F-03, F-04; VM-8, VM-9, VM-10 | .pdca/D386/rv1/
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | staged evidence refreshed: .pdca/D386/rv1/staged/ (12 files: 11 sources + 1 new-test); staged-manifest .pdca/D386/rv1/staged/staged-manifest.json; working_tree_diff_sha256 6663379808bb1c365afdfc365654ec1d0d9891026e006bbcb69e96fd1111fe91 | .pdca/D386/rv1/staged/
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | F-03 correction (append-only): the 19 core RED failures were 10 ArgumentNullException-expected-with-no-exception, 8 ArgumentException-expected-with-no-exception, 1 arity-8 NotSupportedException | .pdca/D386/rv1/red-core.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | VM-8 closure: arity 8 exists, guarded, covered by TableGuardSweepTests.JoinedEntityBuilder_AllArityOps_Guard (arity 8) — green | .pdca/D386/rv1/green-core.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | VM-9: CteQuery_From_Guards, MutationCteQuery_FromTable_Guards, own ParamName (cteName) | .pdca/D386/rv1/green-core.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | VM-10: EntityBuilder_Generic_Ctor_Guards | .pdca/D386/rv1/green-core.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | AC-5 preservation tests: Ac5_ValidTableNames_Construct, Ac5_Cte_From_ValidNames, Ac5_DeferredNull_Preserved, Ac5_DirectCtor_ValidName, Ac5_AssertionStyle_And_Scoping_Present | .pdca/D386/rv1/green-core.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | convention tests filtered: 4/4 passed rc=0 — NextORM.Core.Tests.AssertionStyleTests.TestSources_ShouldNotUseXunitAssert_BeyondTheSkipPrimitives; NextORM.Core.Tests.AssertionStyleTests.TestProjects_ShouldReferenceFluentAssertions; NextORM.Core.Tests.IntegrationSuiteScopingTests.IntegrationTests_ShouldNotUseTheInMemoryProvider; NextORM.Core.Tests.IntegrationSuiteScopingTests.IntegrationTestClasses_ShouldBeBackedByARealDatabase | .pdca/D386/rv1/conventions.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | docfx rc=0, 0 warnings/0 errors | .pdca/D386/rv1/docfx.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | suppression/slop counts 0 each (ratio 0/442) | .pdca/D386/rv1/
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | build 0/0 (warnings/errors) | .pdca/D386/rv1/build-solution.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | core tests 4038 passed / 0 failed | .pdca/D386/rv1/full-core.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | sqlite tests 2385 passed / 1 skipped (unchanged this round — not re-run: no sqlite-project change this round) | .pdca/D386/rv1/full-sqlite.log
2026-11-11T11:07:21Z | DO | revision r1 | attempt n1/3 | validators rc 0 (validate_inner_loop + pdca_evidence lint) | .pdca/D386/rv1/validators.log
2026-11-11T11:07:45Z | DO | revision r1 | attempt n1/3 | staged evidence file count correction: 11 staged source/test files (10 sources + 1 new-test), not 12 | .pdca/D386/rv1/staged/

2026-11-11T12:00:00Z | CHECK | revision r1 | attempt n1/3 | CHECK (2) verdict: not accepted → DO; findings F-02, F-05, F-06; process defect D386-PROC-1 | .pdca/D386/rv1/
2026-11-11T12:05:00Z | DO | revision r1 | attempt n2/3 | DO started (n=2/3): corrected sweep surfaces, process-defect spec, manifest F-02 fix, staged refresh | .pdca/D386/rv1/attempts/n2/
2026-11-11T12:10:00Z | DO | revision r1 | attempt n2/3 | build 0/0 (warnings/errors) | .pdca/D386/rv1/attempts/n2/build-solution.log
2026-11-11T12:12:00Z | DO | revision r1 | attempt n2/3 | sweep core: 28 passed, 0 failed, 0 skipped | .pdca/D386/rv1/attempts/n2/sweep-core.log
2026-11-11T12:13:00Z | DO | revision r1 | attempt n2/3 | sweep sqlite: 8 passed, 0 failed, 0 skipped | .pdca/D386/rv1/attempts/n2/sweep-sqlite.log
2026-11-11T12:15:00Z | DO | revision r1 | attempt n2/3 | full core: 4033 passed, 0 failed, 0 skipped | .pdca/D386/rv1/attempts/n2/full-core.log
2026-11-11T12:17:00Z | DO | revision r1 | attempt n2/3 | full sqlite: 2393 passed, 0 failed, 1 skipped | .pdca/D386/rv1/attempts/n2/full-sqlite.log
2026-11-11T12:18:00Z | DO | revision r1 | attempt n2/3 | guard-removal RED/GREEN not redone: no product source changed this attempt; earlier pair retained as history (guard-src.patch sha256 afc25b9f...) | .pdca/D386/rv1/
2026-11-11T12:20:00Z | DO | revision r1 | attempt n2/3 | staged refreshed; manifest rows → attempts/n2 logs; F-02 VM-6 description corrected; validators rc 0 | .pdca/D386/rv1/attempts/n2/validators.log
2026-11-11T11:34:20Z | DO | revision r1 | attempt n2/3 | evidence packaging: spec staged (sha256 e82d65a3...), manifest VM-1/VM-5 repointed, validators rc 0; CHECK (3) not accepted (packaging only) | .pdca/D386/rv1/staged/
2026-11-11T11:40:21Z | ACT | revision r1 | attempt n2/3 | commit 196f4b56 (full 196f4b56825bc0f1164bfc8a9847bdddc60acdb6) — 13 files, branch 1.0.9; issue #386 close attempted | .pdca/D386/rv1/commit.txt
2026-11-11T11:42:00Z | ACT | revision r1 | attempt n2/3 | ACT closed: outcome accepted (CHECK → ACT); issue #386 closed via gh; commit 196f4b56 | .pdca/D386/rv1/commit.txt
