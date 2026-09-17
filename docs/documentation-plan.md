# NextORM documentation plan

Status: **implemented**. See [Execution log](#execution-log).

## Execution log

- **Phase 0** - folders created, `docs/index.md` rebuilt as the TOC hub, page template at
  `docs/_template.md`, Russian intro moved from `src/nextorm.core/docs/ru` to `docs/ru`.
  `readme.md` (lowercase) is the packaged `PackageReadmeFile` (`src/nextorm.core/nextorm.core.csproj`);
  on the case-insensitive shared working copy `README.md` resolves to the same file, so neither is
  renamed.
- **Phases 1-3 (English)** - 27 pages written under `docs/getting-started`, `docs/guide`,
  `docs/providers`, `docs/advanced`. Every page carries a `Source: test/...:line` footnote and, where
  available, the SQL asserted by the provider `SqlGenerationTests`.
- **Phase 4 (Russian)** - full mirror of the 27 pages under `docs/ru/`; code fences are byte-identical
  to the English source, prose is translated, relative links and source footnotes are preserved.
- **Phase 5 (quality)** - link check across all published pages: 0 broken links; all files CRLF;
  EN/RU fenced code blocks identical. The optional `docs/samples` compile harness was **not** built;
  verification relies on the footnote links to the existing test suite.
- **Phase 6 (site + API reference)** - the Jekyll/`dinky` site was replaced by **DocFX**.
  `docs/docfx.json` builds the articles plus a generated **API reference** from the XML doc comments
  (XML doc generation enabled in the four library projects). DocFX also provides site navigation
  (`toc.yml` files) and client-side **search**. `.github/workflows/docs.yml` builds and deploys the site
  to GitHub Pages.
- Open questions resolved: page granularity kept as planned; no `docs/samples` harness;
  Russian tree is a full mirror.

This plan describes how to grow nextorm's documentation from "examples only in the README" into a
complete, bilingual (English + Russian) feature guide with a runnable example for every feature.
Examples are sourced from the existing tests, so documented behaviour is behaviour that is already
verified by the test suite.

---

## 1. Goals

1. One discoverable page per feature, each with at least one compilable C# example and the generated
   SQL where it aids understanding.
2. Every example traceable to a passing test (`test/.../File.cs:MethodName`), so docs cannot drift into
   describing behaviour that does not exist.
3. A single entry point (`docs/index.md`) with a table of contents, provider matrix and links.
4. English and Russian in parallel, Russian under `docs/ru/`.
5. No new runtime dependencies; the only new tooling is DocFX as a pinned local tool
   (`.config/dotnet-tools.json`), used to build the docs site and the API reference.

Non-goals (documented as limitations, not documented as features):

* DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) - out of scope by design.
* Navigation properties / relationship metadata - out of scope.
* `APPLY` / `LATERAL` - not implemented.

## 2. Audience

* A developer evaluating nextorm who wants a 5-minute feel (`getting-started`).
* A developer using nextorm who needs the exact SQL semantics of one feature (`guide`).
* A maintainer who needs the provider-specific caveats (`providers`, `advanced/limitations`).

## 3. Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Language | Bilingual: English primary, Russian mirror in `docs/ru/` | README/`docs/index.md`/project artifacts are English; `src/nextorm.core/docs/ru` already exists. |
| Tooling | Plain Markdown rendered by **DocFX** (with a generated API reference and site search) | Keeps authoring in Markdown while adding the navigation, search and API pages that Jekyll/`dinky` lacked. |
| Examples | Extracted from integration/provider tests | Guarantees the example is real and compiles; tests are the de-facto spec. |
| Line endings | CRLF, normalized with `perl -pi -e 's/\r?\n/\r\n/g' <file>` | `AGENTS.md`. |
| Code fences | ` ```csharp ` / ` ```sql ` | Consistent syntax highlighting. |

## 4. Current state

Existing documentation:

| File | Content | Verdict |
|---|---|---|
| `README.md` / `readme.md` (both present, identical) | Features + 3 examples + benchmarks | Keep as the short landing page; duplicate files should be resolved (keep `README.md`). |
| `docs/index.md` | Overview, status, roadmap, installation, release notes | Keep; becomes the TOC hub. |
| `docs/prepared-vs-cached.md` | Excellent deep-dive on plan cache vs `Prepare()` | Keep; link from `guide/query-reuse.md`. |
| `docs/sql-capabilities-gap-analysis.md` | Capability matrix vs EF Core / linq2db, implementation status | Keep; source for `advanced/limitations.md` and the provider matrix. |
| `src/nextorm.core/docs/ru/overview.md`, `motivation.md` | Russian intro | Move under `docs/ru/`. |
| `benchmark-report.md`, `performance-findings.md`, `solid-review.md`, `code-smells-review.md` | Internal engineering reports | Link from the landing page only; not part of the feature docs. |

What is missing: getting started, metadata, filtering, joins, grouping, paging, subqueries, set
operations, distinct, CTE, window functions, scalar/UDF/TVF functions, raw SQL, provider differences,
DI/connections/logging, query reuse (except the deep-dive), limitations.

## 5. Target information architecture

```text
docs/
  index.md                                  # landing + TOC + provider matrix (en)
  documentation-plan.md                     # this file
  _config.yml
  prepared-vs-cached.md                     # existing deep-dive (keep)
  sql-capabilities-gap-analysis.md          # existing analysis (keep)
  getting-started/
    01-installation.md
    02-quickstart.md
    03-entities-and-metadata.md
    04-dependency-injection.md
  guide/
    01-querying-and-projections.md
    02-filtering-where.md
    03-joins.md
    04-grouping-and-aggregates.md
    05-sorting-and-paging.md
    06-subqueries.md
    07-set-operations.md
    08-distinct.md
    09-cte.md
    10-window-functions.md
    11-scalar-functions.md
    12-user-defined-functions.md
    13-table-valued-functions.md
    14-raw-sql.md
    15-query-reuse.md
    16-connections-and-logging.md
  providers/
    overview.md
    sqlite.md
    sqlserver.md
    postgres.md
    in-memory.md
  advanced/
    limitations.md
    api-reference.md                        # curated; XML docs + future DocFX
  ru/
    index.md
    getting-started/…                       # mirror of the English tree
    guide/…
    providers/…
    advanced/…
```

Navigation: DocFX builds the sidebar from the `toc.yml` files (`docs/toc.yml` plus one per section) and
the generated `api/toc.yml`; the homepage `docs/index.md` still holds a full linked TOC for readers who
land there directly. Client-side search is enabled (`_enableSearch`).

## 6. Feature → page → example source

`Status`: **new** = page to write, **exists** = page already exists, **extend** = page exists but needs work.

### Getting started

| Page | Status | Content | Example source (tests) | SQL-generation evidence |
|---|---|---|---|---|
| `getting-started/01-installation.md` | new | NuGet packages (`nextorm`, `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`), target framework, in-memory built-in | `docs/index.md` | - |
| `getting-started/02-quickstart.md` | new | Define an entity, create a context, run a query, get a list | `CommonTestSuite.SqlCommand.cs:9`, `:20`; `Providers/SqliteTestProvider.cs` | - |
| `getting-started/03-entities-and-metadata.md` | new | `[SqlTable]`/`[Column]`/`[Key]`, interface + class mapping, no-entity `TableAlias` mode, `From("table")`, fluent `EntityBuilder` (`Table`, `Property().HasColumnType`/`HasColumnName`) | `Entities.cs`; `MetadataRegistrationTests.cs:18`; `DataContext/Meta/EntityBuilder.cs` | `CommonTestSuite.SqlCommand.cs:37` |
| `getting-started/04-dependency-injection.md` | new | `AddNextOrmContext` (generic / options / keyed), scoped context, `DbContextBuilder.UseSqlite/UseSqlServer/UsePostgres`, `UseLoggerFactory`, `LogSensitiveData` | `DependencyInjectionTests.cs:13-71` | - |

### Guide

| Page | Status | Content | Example source (tests) | SQL-generation evidence |
|---|---|---|---|---|
| `guide/01-querying-and-projections.md` | new | `Entity.Select`: anonymous type, DTO, record, tuple, member-init, primitive/scalar, nested and calculated columns | `CommonTestSuite.SqlCommand.cs:9,20,26,75-186,353,368,728` | `SqlGenerationTests` ComputedColumn/NestedCalculatedColumn |
| `guide/02-filtering-where.md` | new | `Where`, `==`/`!=`/`>`/`>=`/`<`/`<=`, null (`IS NULL`), `and`/`or`, `!`, arithmetic, bitwise, shifts, `??` (COALESCE), `?:`/`switch` (CASE WHEN), captured parameters, `NORM.Param`, `in`/`Contains` (incl. nulls, captured list/array mutation) | `CommonTestSuite.SqlCommand.cs:187-306`; `CommonTestSuite.Conditional.cs:9-42`; `CommonTestSuite.Unary.cs:8-44`; `CommonTestSuite.In.cs:9-122` | `SqlGenerationTests` Conditional/Switch/LogicalNot/InValues/Contains |
| `guide/03-joins.md` | new | Inner / left / right / full / cross joins, join to entity / table / subquery, arity 2..8 via `EntityP2..P8` + `Projection<T1..T8>`, projected values | `CommonTestSuite.Join.cs:9-190`; `InMemoryJoinTests.cs:14-162` | `SqlGenerationTests` Join/LeftJoin/RightJoin/FullJoin/CrossJoin/Join4..8 |
| `guide/04-grouping-and-aggregates.md` | new | `GroupBy`, `Having`, `count`/`count_big`/`count_distinct`, `min`/`max`/`avg`/`sum`, `stdev`/`stdevp`/`var`/`varp` + `_distinct`, grouping with where/having/limit/sort | `CommonTestSuite.GroupBy.cs:9-70`; `CommonTestSuite.Aggregates.cs:9-372` | `SqlGenerationTests` Count/CountBig/Aggregate |
| `guide/05-sorting-and-paging.md` | new | `OrderBy` by expression and by ordinal, `OrderByDescending`, nulls first/last, `Limit`/`Offset`/`Page`, `TOP` vs `LIMIT/OFFSET` vs `OFFSET…FETCH`, `First`/`FirstOrDefault`/`Single`/`SingleOrDefault` (+ async), `Any` | `CommonTestSuite.SqlCommand.cs:479-621,397-477`; `CommonTestSuite.LinqExtensions.cs:8-49`; `PostgresSpecificTests.cs:24`; `SqlServerSpecificTests.cs:24,43` | `SqlGenerationTests` Limit/Paging/OrderBy |
| `guide/06-subqueries.md` | new | Subquery as `FROM` source, scalar subquery in `SELECT`/`WHERE`/`ORDER BY`, correlated `EXISTS`/`IN`/`ANY`/`ALL` via `NORM.SQL`, provider limits (SQLite `any`/`all`) | `CommonTestSuite.SqlCommand.cs:461,623-669`; `CommonTestSuite.CorrelatedQuery.cs:9`; `SqliteSpecificTests.cs:16,30` | `SqlGenerationTests` Subquery |
| `guide/07-set-operations.md` | new | `Union`, `UnionAll`, `Intersect`, `IntersectAll`, `Except`, `ExceptAll`; chaining order; provider support (SQLite/SQL Server lack `*ALL`) | `CommonTestSuite.SetOperations.cs:8-77` | `SqlGenerationTests` Union/UnionAll/Intersect/Except(+All) |
| `guide/08-distinct.md` | new | `Select(...).Distinct()`, distinct over join/cross join, distinct + paging, distinct + union | `CommonTestSuite.Distinct.cs:9-84` | `SqlGenerationTests` SelectDistinct / WithLimit (SQL Server) |
| `guide/09-cte.md` | new | `With`/`WithRecursive`, chained CTEs, `From(cte)`, recursive series, `maxRecursion` (SQL Server), CTE + parameters + plan cache | `CommonTestSuite.Cte.cs:14-33`; `CteQueryTests.cs:8`; `PlanCacheTests.cs:190,343` | `SqlGenerationTests` Cte_* |
| `guide/10-window-functions.md` | new | `row_number`, `rank`, `dense_rank`, `ntile`, `lag`/`lead` (offset, default), `first_value`/`last_value`, `sum_over`/`avg_over`/`min_over`/`max_over`/`count_over`, `Over(partition, order, frame)`, `asc`/`desc`, frames | `CommonTestSuite.Window.cs:14-99`; `WindowFunctionMarkerTests.cs:28-64` | `SqlGenerationTests` RowNumber/Rank/Window*/LagAndLead/Ntile |
| `guide/11-scalar-functions.md` | new | String: `Contains`/`StartsWith`/`EndsWith`/`ToUpper`/`ToLower`/`Substring`/`Length`/`Trim`/`Replace`/`string.IsNullOrEmpty`/`like`; math: `Abs`/`Round`/`Truncate`/`Log`; date/time: `Now`, year/month/day/hour; `COALESCE`, numeric `CAST`; per-provider mapping table | `CommonTestSuite.Functions.cs:8-117` | `SqlGenerationTests` String*/Math*/DateTime*/Coalesce (all 3 providers) |
| `guide/12-user-defined-functions.md` | new | `[SqlFunction]` on method or declaring type, name/schema, captured argument as parameter | `CommonTestSuite.Udf.cs:14-36` | `SqlGenerationTests` SqlFunction_* |
| `guide/13-table-valued-functions.md` | new | `[SqlTableFunction]`, `FromTableFunction(() => Db.Tvf(...))`, join/where/group by over a TVF, alias requirements per provider | `CommonTestSuite.Tvf.cs:34-76`; `SqlTableFunctionAttributeTests.cs:8-25` | `SqlGenerationTests` TableFunction_* |
| `guide/14-raw-sql.md` | new | `WithSql`, `PrepareFromSql` (with/without params, `nonStreamUsing`, `storeInCache`), mapping raw SQL into entities/DTOs | `CommonTestSuite.SqlCommand.cs:671-727` | - |
| `guide/15-query-reuse.md` | new | Implicit plan cache vs `Prepare()`; links to `prepared-vs-cached.md`; prepared-command terminals; streaming (`ToAsyncEnumerable`, `Pipeline`, `CreateEnumeratorAsync`); cache scope/purging; in-list cache refresh | `PlanCacheTests.cs:49-343`; `InListCacheTests.cs:41-117`; `CommonTestSuite.Cache.cs:6`; `DataContextCacheScopeTests.cs:20-44` | - |
| `guide/16-connections-and-logging.md` | new | Connection string vs supplied `DbConnection`, connection lifetime/disposal, `IConnectionManager`, `UseLoggerFactory`, `LogSensitiveData`, `CommandLogger` | `ConnectionManagementTests.cs:24-116` | - |

### Providers

| Page | Status | Content | Example source |
|---|---|---|---|
| `providers/overview.md` | new | Support matrix (parameter prefix, paging, `*ALL`, recursive CTE, TVF alias, stdev naming), how dialects plug in (`ISqlDialect`/`SqlDialectBase`) | `docs/sql-capabilities-gap-analysis.md`; dialect tests |
| `providers/sqlite.md` | new | `UseSqlite`, `SQLiteFunctions` custom aggregates (`stdev/stdevp/var/varp`), `strftime` date parts, `ifnull`, `any`/`all` unsupported | `SqliteDialectTests.cs`; `SqliteSpecificTests.cs`; `SQLiteFunctions.cs` |
| `providers/sqlserver.md` | new | `UseSqlServer`, `TOP`/`OFFSET…FETCH`, `+` concat, `isnull`, `datepart`, injected `ORDER BY`, `*ALL` unsupported | `SqlServerDialectTests.cs`; `SqlServerSpecificTests.cs` |
| `providers/postgres.md` | new | `UsePostgres`, `LIMIT/OFFSET`, `coalesce`, `extract`, `stddev`, `*ALL` supported, nulls ordering | `PostgresDialectTests.cs`; `PostgresSpecificTests.cs` |
| `providers/in-memory.md` | new | `InMemoryDataContext`, when to use in tests, supported feature subset, clear `NotSupportedException`s | `InMemoryTests.cs`; `InMemoryJoinTests.cs` |

### Advanced / reference

| Page | Status | Content | Example source |
|---|---|---|---|
| `advanced/limitations.md` | new | Out-of-scope: DML, navigation properties, `APPLY`/`LATERAL`, provider-specific set ops, general correlated scalar projection; based on the gap analysis | `docs/sql-capabilities-gap-analysis.md` |
| `advanced/api-reference.md` | new | Curated index of public types (`IDataContext`, `Entity<T>`, `Entity`, `QueryCommand<T>`, `NORM`, attributes) with links to source; XML docs already rich | `src/nextorm.core/**` |

## 7. Bilingual strategy

* English is authored first; Russian pages mirror the same slugs under `docs/ru/`.
* A Russian page may lag, but it must exist with either the translation or a "перевод в процессе" note
  linking to the English page - never a dead link.
* Keep code identical in both languages; translate only prose and comments.
* `docs/index.md` and `docs/ru/index.md` are language switchers for each other.
* Move `src/nextorm.core/docs/ru/{overview,motivation}.md` into `docs/ru/` and update links.

## 8. Sourcing examples from tests

For each page:

1. Identify the test(s) in section 6 and read the test body.
2. Reduce it to the smallest self-contained snippet: entity declaration (or a link to
   `getting-started/03-entities-and-metadata.md`) + the query + the terminal.
3. Add the SQL the query produces (from the provider `SqlGenerationTests` where available).
4. Record the source as a footnote: `` Source: `test/nextorm.integration.tests/CommonTestSuite.Join.cs:9` ``.
5. Keep snippets async-first (`await ... ToListAsync()`), since that is the common case.

Optional verification harness (recommended, phase 4): a single `docs/samples` xUnit project that contains
the example snippets as tests. It compiles the documentation and runs against the in-memory or SQLite
provider, so a broken example fails CI. This is a new project and is **optional**; the minimum bar is the
footnote link back to the existing test.

## 9. Phased roadmap

| Phase | Deliverable | Pages | Estimate |
|---|---|---|---|
| 0 | Foundations: folders, TOC skeleton in `docs/index.md`, page template, resolve `README.md`/`readme.md`, move RU intro, CRLF check | - | 0.5 day |
| 1 | Getting started + core querying | 8 pages (getting-started x4, querying, filtering, sorting/paging + landing) | 2-3 days |
| 2 | Advanced query features | joins, grouping/aggregates, subqueries, set ops, distinct, CTE, window, scalar/UDF/TVF, raw SQL (9 pages) | 3-4 days |
| 3 | Operational + reference | query reuse, connections/logging, providers x5, limitations, api-reference (8 pages) | 1-2 days |
| 4 | Russian mirror | all pages under `docs/ru/` | 2-3 days (parallelizable) |
| 5 | Quality pass | example/source audit, link check, provider test run, CI hook | 1 day |

Effort assumes page-per-feature with 2-4 examples each and reuse of the existing deep-dives.

## 10. Generated API reference (implemented)

The four library projects now set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (missing
`CS1591` comments are suppressed) so their XML doc comments are emitted next to the assemblies. DocFX
metadata reads the four `.csproj` files and generates the **API reference** into `docs/api/`
(`memberLayout: samePage`, `namespaceLayout: flattened`), which is rendered by the default DocFX template
and is searchable. `advanced/api-reference.md` remains a curated entry point that links to the generated
pages.

Build locally with `dotnet tool restore && dotnet docfx docs/docfx.json`; output lands in `docs/_site/`
(both `docs/api/` and `docs/_site/` are git-ignored).

## 11. Definition of done (per page)

* Front matter: title, one-sentence purpose, prerequisites.
* At least one `csharp` example and, where relevant, the generated `sql`.
* Provider differences called out in a small table or note when behaviour differs.
* Footnote linking to the source test(s).
* Added to `docs/index.md` TOC (and `docs/ru/index.md` once translated).
* Links resolve; file saved as CRLF.
* New/edited feature docs re-verified with the relevant tests:
  `dotnet test test/nextorm.sqlite.tests`, `.../nextorm.sqlserver.tests`, `.../nextorm.postgres.tests`,
  `.../nextorm.integration.tests`.

## 12. Risks and open questions

| Risk | Mitigation |
|---|---|
| Docs drift from tests as features change | Footnote links + optional `docs/samples` compile harness; include docs checklist in PR template. |
| Bilingual maintenance cost | Russian mirror allowed to lag with an explicit in-progress note. |
| DocFX site needs a Pages source change | Set repository **Settings → Pages → Source** to *GitHub Actions*; `.github/workflows/docs.yml` builds and deploys. No `_config.yml`/Jekyll build is used any more. |
| `README.md` and `readme.md` both exist | Resolve in phase 0 (keep one, case-correct for GitHub). |
| Provider behaviour differences are subtle | Derive provider pages from the existing dialect/SQL-generation tests, not from memory. |

Open questions for the maintainer:

1. Is the proposed page granularity right, or should some pages be merged (e.g. set operations +
   distinct, scalar + UDF + TVF as "functions")?
2. Should the optional `docs/samples` compile harness be in scope, or are footnote links enough?
3. Should the Russian tree be a full mirror or a selected subset (getting-started + guide only)?
