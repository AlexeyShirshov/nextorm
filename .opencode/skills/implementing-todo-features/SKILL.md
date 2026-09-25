---
name: implementing-todo-features
description: Close an unchecked backlog item (a per-feature docs/specs/roadmap/todo_*.md, indexed by docs/specs/roadmap/sql-capabilities-gap-analysis.md) end-to-end — save a work plan to docs/specs/roadmap/todo_<feature>.md first, map the function across every provider (mandatory provider x form matrix), pick the closest C# analog, add the SqlFunctions.Sql / provider *Functions surface, run nextorm-code-auditor and nextorm-design-engineer, add tests, keep line coverage >= MIN_LINE_COVERAGE, and update docs EN+RU plus specs. Use when adding a missing SQL function/aggregate/operator or LINQ operator, when the user says "нереализованный функционал", "закрыть TODO", "добавить функцию", "SqlFunctions.Sql", "провайдерный пробел", "план фичи", or picks a blocked/`[ ]` item from todo_*.
---

# Implementing a TODO feature across providers

Turn one unchecked/blocked backlog item into a shipped, audited, tested, documented feature.
Work the eight steps in order. Do not skip step 3 (work plan), step 6 (audit) or step 8 (docs/specs).

## 0. Backlog map (pick exactly one item)

The open backlog lives in the per-feature `docs/specs/roadmap/todo_*.md`, one file per item; the ordered index
(gaps, blockers, owners) is `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4:

- gap-analysis §4 — prioritized remaining gaps, each `Todo:` line pointing at its `todo_*.md`;
- `todo_*.md` — the actual per-feature plan/backlog for one item;
- deliberately out-of-scope work is documented in `docs/advanced/limitations.md` (+RU).

Cross-provider context: `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
`docs/specs/comparison/linq2db-comparison.md`. Primary code by area:
`src/nextorm.core/Query/SqlFunctions.{Postgres,SqlServer,ClickHouse}.cs` and the matching
`src/nextorm.<provider>/*Dialect.cs`. One item per change.

## 1. Map the function across every provider (mandatory matrix)

This step answers **which providers can express the construct, and under what native name/form** —
a coverage question, not a "who already implemented it" question. A function that exists on only one
provider today can still have an obvious analog on the others (`current_user` / `CURRENT_USER` /
`current_user()` / `currentUser()`). Search the engine before designing anything:

```bash
rg -n "<function-or-operator>" src/nextorm.core src/nextorm.sqlite src/nextorm.postgres src/nextorm.sqlserver src/nextorm.mysql src/nextorm.mariadb src/nextorm.clickhouse
```

Build a **provider × form matrix** with a row for **every** provider (`PostgreSQL`, `SQL Server`,
`MySQL`, `MariaDB`, `ClickHouse`, `SQLite`, and `InMemory` where relevant); an unsupported cell is
written as `—` plus the reason (e.g. "no session-user concept"). The matrix is the deliverable of this
step and goes verbatim into the work plan (step 3). A "sibling findings" section that mentions only one
provider — or that lists only surfaces *within* one provider (e.g. `PostgresFunctions` vs
`ExtendedScalarFunctionTranslator`) — has **not** done this step.

The `rg` scan above only finds what nextorm has **already** wired; it cannot tell you that SQL Server
spells the same thing `DB_NAME()` or that ClickHouse has `currentDatabase()`. So the matrix has to be
filled from the **providers' own documentation**, not from the nextorm codebase:

- PostgreSQL — the official function reference (e.g. session/system-information functions: `current_user`,
  `current_schema`, `current_database()`, `version()`);
- SQL Server — Microsoft Learn / T-SQL reference (`CURRENT_USER`, `SESSION_USER`, `SYSTEM_USER`,
  `SCHEMA_NAME()`, `DB_NAME()`, `@@VERSION`); use the `mslearn` MCP to check, do not guess;
- MySQL and MariaDB — the respective SQL function references (`CURRENT_USER()`, `SESSION_USER()`,
  `DATABASE()`/`SCHEMA()`, `VERSION()`);
- ClickHouse — the ClickHouse function reference (`currentUser()`, `currentDatabase()`, `version()`,
  `hostName()`, …);
- SQLite — the core function list (`sqlite_version()`, `sqlite_source_id()`, …).

For each provider record **whether the function exists there and its exact native spelling/signature**.
A cell may only say `—` (unsupported) after the provider's own docs were checked; absence from the
nextorm codebase or from a translator is **not** evidence that the provider cannot express it. State in
the work plan which documentation was consulted per provider.

Check, in this order:
1. Public surfaces: `CommonFunctions` (`src/nextorm.core/Query/SqlFunctions.cs`), `PostgresFunctions`,
   `SqlServerFunctions`, `ClickHouseFunctions` (`SqlFunctions.*.cs`), SQLite's runtime registration
   (`src/nextorm.sqlite/SQLiteFunctions.cs`), MySQL/MariaDB.
2. Capability flags and hooks: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` and
   `SqlDialectBase.cs` (`Supports*` default `false`, `Make*`), then each provider dialect.
3. Translators: `src/nextorm.core/Visitors/` — `BuiltinFunctionTranslator`, `AdvancedAggregateTranslator`,
   `ExtendedScalarFunctionTranslator`, `ArraySqlTranslator`, `JsonSqlTranslator`,
   `StringFunctionTranslator` / `MathFunctionTranslator` / `DateTimeFunctionTranslator`, `WindowFunctionTranslator`.
4. Tests and docs: grep `tests/**` and `docs/**` for the SQL name.

Decision (apply to **every** row of the matrix, not just the origin provider):
- Same semantics on ≥2 providers → the function belongs on the **cross-provider `CommonFunctions`**
  surface with a `Supports*`/`Make*` pair. A provider-specific surface (`PostgresFunctions`,
  `SqlServerFunctions`, `ClickHouseFunctions`) is reserved for constructs with no analog elsewhere.
- A different native spelling on another provider (`current_user` vs `current_database()` vs `DB_NAME()`)
  is still the same feature: expose one cross-provider method and let `Make*` render the native form.
- Only when a provider genuinely cannot express it: leave its cell `—`, add a `Supports*` flag (default
  `false` in `SqlDialectBase`) so it rejects with a clear `NotSupportedException`. **Justify this per
  function** ("ClickHouse has no session-user concept"), never by a family umbrella such as
  `SupportsExtendedScalarFunctions` — an umbrella flag is not evidence that a specific function is
  inexpressible.
- **Red flag / post-check:** `rg "<function-name>" src/nextorm.*/*Dialect.cs`. If a function that ≥2
  providers can express matches only one dialect, this step was done wrong — redo the matrix before
  writing code.

## 2. Find the closest C# analog and choose the implementation tier

Establish what the SQL construct means and whether an existing CLR member already expresses it.
`ScalarFunctionTranslator` routes `string`, `Math` and `DateTime` methods to
`StringFunctionTranslator` / `MathFunctionTranslator` / `DateTimeFunctionTranslator`.

Examples already in the repo:
- `left` / `right` / `replicate` -> `string.Substring` / `new string(c, n)` (tier a, no new surface).
- `date_add` -> `DateTime.Add*` (tier a); `date_trunc` has no BCL equivalent, so it is tier b.

Rules:
- BCL method wins when the semantics match exactly.
- Do **not** force a BCL mapping when the semantics differ (e.g. `DateTime.DayOfWeek` is 0-based while
  `datepart(weekday, x)` depends on `DATEFIRST`) — move down a tier.
- If no CLR member matches, add a new method; names mirror SQL tokens (`count_big`, `array_agg`); see
  `docs/specs/design/API-NAMING-REVIEW.md` finding P1-10.

## 3. Save the work plan (before writing any code)

Persist the analysis from steps 0–2 to `docs/specs/roadmap/todo_<feature>.md` (specs/roadmap) — lowercase, SQL/CLR
token as the name (e.g. `todo_date_trunc.md`); if the item already has a root todo, update it. This is
the working plan the rest of the steps execute against; do not start step 4 without it. Use the register
style (Russian H2/H3 is fine, match the neighbouring `todo_*` files). It must contain:

- Item + source backlog, target provider(s), and acceptance criterion.
- The step-1 **provider × form matrix — one row for every provider, `—` + reason where unsupported,
  filled from the providers' own documentation (which source was consulted per provider)** — and the
  "Единообразие провайдеров" decision (which providers get it, which are gated off and why). A
  prose-only "sibling findings" section, or a matrix built only from grepping nextorm, is not accepted.
- Closest C# analog and the chosen implementation tier (a/b/c) with a one-line justification.
- Dialect plan: `Supports*`/`Make*` hooks, per provider, and the base defaults.
- Public API additions (exact signatures) and any XML-doc/register impact.
- Test plan: SQL-generation, integration `CommonTestSuite.*`, in-memory, and the coverage baseline
  (line/branch before the change).
- Docs/specs files to touch.

Keep the file updated as the work proceeds. When the item ships, fold its conclusions into the docs and
delete the work-plan file (step 8) — do not leave a stale plan behind; the item's `Todo:` entry in
`docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 is then repointed/removed.

## 4. Add the surface — priority order

Work down this ladder and use the **first** tier that fits. `[SqlFunction]` is the edge case, not the
default. In every tier the dialect still has to be implemented: a `Supports*` gate and/or a `Make*`
hook in `ISqlDialect` + `SqlDialectBase` and the capable provider dialects. No tier skips the dialect.

**a) Native C# construct + translator (preferred).** Map an existing CLR member to SQL in the matching
translator — `left`/`right` -> `string.Substring` in `StringFunctionTranslator`, `date_add` ->
`DateTime.Add*` in `DateTimeFunctionTranslator`, etc. No new public API. Add/route the `Make*`/`Supports*`
dialect hook the translator calls and override it per provider.

**b) New `CommonFunctions` method + translator.** When no CLR member matches: add the public method
(`src/nextorm.core/Query/SqlFunctions.cs`), a translator branch in the matching
`Visitors/*Translator.cs`, and the dialect `Supports*`/`Make*` hook. Provider-only functions go on the
provider surface (`SqlFunctions.Postgres.cs` / `.SqlServer.cs` / `.ClickHouse.cs`) but follow the same
pattern.

**c) `[SqlFunction]` attribute (last resort).** Only when the construct cannot be expressed through a
translator + dialect hook — typically a plain name -> SQL swap with no operator/precedence handling:

```csharp
[SqlFunction("patindex")]
public int patindex(string pattern, string expression) => default!;
```

`SqlFunctionAttribute` lives in `src/nextorm.core/SqlFunctionAttribute.cs`. Even here the dialect must
be wired: add the `Supports*` gate and, when the SQL name/signature differs per provider, override it
or declare the method on the provider-specific surface. Never add an ungated cross-provider attribute.

Details:
- Table functions use `[SqlTableFunction]` + `FromTableFunction`; add the row-shape interface next to
  `IGenerateSeriesRow` / `IStringSplitRow` / `IOpenJsonRow` in `SqlFunctions.cs`.
- In-memory items have no SQL: implement in `InMemoryDataContext` plus `InMemoryAggregates` /
  `InMemoryGroupBy` / etc., and `throw` explicitly for unsupported source kinds (async, filtered aggregates).

Hard constraints:
- Every public member needs a `<summary>` XML doc (CS1591 + `TreatWarningsAsErrors=true`).
- No code comments (AGENTS.md).
- CRLF everywhere: `perl -pi -e 's/\r?\n/\r\n/g' <file>`.
- Public additions/renames also require docs EN+RU (step 8) and a register update in
  `docs/specs/design/API-NAMING-REVIEW.md`.

## 5. Build

```bash
dotnet build nextorm.slnx -c Release
```

Must be 0 warnings / 0 errors before the audit.

## 6. Code audit (mandatory)

Run **both** subagents over the feature diff, in this order:

1. `nextorm-code-auditor` (via `task`). Read-only for code; owns only
   `docs/specs/design/code-smells-review.md` and `docs/specs/design/API-NAMING-REVIEW.md`; it reports
   findings and updates those registers.
2. `nextorm-design-engineer` (via `task`). Reviews the feature against SOLID/DRY, type design
   (sealed / readonly struct / Span vs Memory / ValueTask / collection return types) and the
   `analyzing-dotnet-performance` anti-patterns; it applies the agreed fixes, builds and tests them,
   and updates `docs/specs/design/solid-review.md` / `code-smells-review.md`.

Then apply the combined findings:
- Code / analyzer-policy fixes -> applied by `nextorm-design-engineer` (or directly).
- Hot-path measurement (if a reviewer flags allocations) -> `nextorm-inmemory-perf-analyst` for
  in-memory, `nextorm-db-perf-analyst` for SQL providers, `nextorm-performance-analyst` to triage.
- Persist the register entries so the next audit does not re-report them.

Re-run `dotnet build nextorm.slnx -c Release`; 0 warnings is the acceptance gate.

## 7. Tests + coverage

Placement:
- SQL generation (no database): `tests/nextorm.<provider>.tests/SqlGenerationTests.cs`; direct dialect
  hooks in `<Provider>DialectTests.cs`.
- Core / in-memory: `tests/nextorm.core.tests/InMemoryTests.cs` (or the matching `InMemory*Tests.cs`).
- Cross-provider behavior: `tests/nextorm.integration.tests/CommonTestSuite.<Area>.cs` (runs for every
  provider). Provider-only behavior belongs in `*SpecificTests.cs`.

Run (fast, no container):

```bash
dotnet test tests/nextorm.postgres.tests -c Debug
dotnet run --project tests/nextorm.integration.tests -c Debug -- -class nextorm.integration.tests.PostgresIntegrationTests
```

Container-backed integration tests need the `running-integration-tests` skill (set `DOCKER_HOST`).

Coverage must not drop:

```bash
dotnet tool restore
dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"
dotnet tool run reportgenerator \
  -reports:tests/coverage/coverage.cobertura.xml \
  -targetdir:tests/coverage/report \
  -reporttypes:"Html;TextSummary;Cobertura" \
  -riskhotspotassemblyfilters:"+nextorm.*"
```

Read `tests/coverage/report/Summary.txt` and report the before/after line and branch numbers.
Threshold is `MIN_LINE_COVERAGE=75` (hard-fails only on `main`; warns elsewhere).
`coverage.settings.xml` includes only `nextorm.{core,sqlite,postgres,sqlserver}` — a
ClickHouse/MySQL-only feature will not move the number, so state that explicitly and still add the
SQL-generation tests.

## 8. Docs and specs

- Update the item's per-feature `docs/specs/roadmap/todo_*.md` (mark shipped / delete) and repoint its `Todo:`
  line in `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 to the shipped docs.
- **Move the new functionality into the documentation** (EN + RU). First try to cluster it into the
  existing guide section that matches its area — `docs/guide/11-scalar-functions.md`,
  `04-grouping-and-aggregates.md`, `10-window-functions.md`, `13-table-valued-functions.md` (plus the
  `docs/ru/guide/` mirror). If the feature does not belong to any existing section, **create a new
  guide page** (and its RU mirror, and a DocFX `toc.yml` entry on both sides) rather than leaving it
  undocumented. Reference the new function/operator with its exact C# signature and a short sample.
- After the documentation is in place, **delete** the provisional work-plan file — its conclusions must
  live in the docs/backlog, not in the plan file.
- Provider docs EN + RU: `docs/providers/<provider>.md` and `docs/ru/providers/<provider>.md`; guide
  pages `docs/guide/11-scalar-functions.md`, `04-grouping-and-aggregates.md`,
  `10-window-functions.md`, `13-table-valued-functions.md` plus the `docs/ru/guide/` mirror, as applicable.
- Matrix/limitations: `docs/providers/overview.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/advanced/api-reference.md` (+RU).
- Specs: `docs/specs/roadmap/sql-capabilities-gap-analysis.md`; on a public-API change
  `docs/specs/design/API-NAMING-REVIEW.md`; on auditor findings `docs/specs/design/code-smells-review.md`.
- On any public rename, grep both trees first: `rg -n "<old-name>" docs` (AGENTS.md).
- Optional validation: `dotnet docfx docs/docfx.json`.

## Definition of done

- [ ] Work plan `docs/specs/roadmap/todo_<feature>.md` written (or the existing todo updated) before
      coding and kept current, including the step-1 provider × form matrix with a row for every provider,
      filled from the providers' own documentation (state the source consulted per provider), not only
      from grepping nextorm.
- [ ] All capable providers updated with their native spelling via `Make*`; the rest gated with a
      `Supports*` flag justified per function. Post-check: `rg "<name>" src/nextorm.*/*Dialect.cs`
      matches more than the origin dialect, or the single match is explained by the matrix.
- [ ] Implementation tier chosen by the ladder (native CLR -> `CommonFunctions` -> `[SqlFunction]`),
      with the dialect hook wired in every case.
- [ ] Public surface has XML docs; `dotnet build nextorm.slnx -c Release` is 0/0.
- [ ] `nextorm-code-auditor` and `nextorm-design-engineer` run; findings applied and persisted in the registers.
- [ ] SQL-generation tests + integration/core tests added and green.
- [ ] Coverage before/after reported; line coverage did not drop below the previous value / 75.
- [ ] Item status in its `docs/specs/roadmap/todo_*.md` / gap-analysis §4, docs EN+RU and specs updated: the new
      functionality is documented in an existing guide section, or in a **new** guide page (+ RU mirror +
      `toc.yml`) when it cannot be clustered; provisional work-plan file deleted.

## Boundaries

- Never `git push`; do not create commits unless explicitly asked (AGENTS.md).
- Do not reword or uncheck unrelated backlog items; one item per change.
- Never duplicate a surface that already exists in `CommonFunctions`; extend the existing hook.
- Do not reach for `[SqlFunction]` before tiers (a)/(b); it is the edge case.
- Do not make a provider-only function cross-provider unless every dialect can express it.
- Do not leave a function that ≥2 providers can express on a provider-specific surface. "It was first
  needed on PostgreSQL, so it went on `PostgresFunctions`" is exactly the mistake the step-1 matrix
  exists to prevent.
- `Supports* => false` and a `...ShouldThrow` test must be justified per function, never by a family
  umbrella (`SupportsExtendedScalarFunctions` is PostgreSQL-only and says nothing about a specific
  function's expressibility).
- Do not claim a provider cannot express a construct from the nextorm source alone. Check that
  provider's own documentation first; a missing nextorm hook only means the feature is not wired yet.
- Keep CRLF; run the normalize command after writing or editing files.
