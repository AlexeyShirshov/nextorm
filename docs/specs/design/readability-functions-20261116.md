# Readability review: function and provider-specific SQL references

> **Status:** Complete body-level editorial pass. The initial intro-heavy pass was
> rejected as incomplete (62 insertions, 6 deletions across 22 pages); this follow-up
> re-read the bodies of all 34 assigned English and Russian pages.

**Date:** 2026-11-16

**Scope:** 17 English/Russian page pairs under `docs/scalar-functions/` and
`docs/guide/provider-specific-sql/` (34 pages total).

## Review approach

These pages are reference catalogues, not tutorials. The review focused on the choices
readers need to make and on dense explanations that obscured behavior. It retained the
reference structure and preserved APIs, SQL mappings, semantics, versions, constraints,
headings, anchors, links, and code examples. Prose and explanatory table text were
clarified in place; a pair was left unchanged only when its body already explained the
relevant limits and choices.

## Coverage and disposition

| English page | Russian counterpart | Body-level disposition |
|---|---|---|
| `docs/scalar-functions/index.md` | `docs/ru/scalar-functions/index.md` | Clarified when to use translated CLR expressions, shared `SqlFunctions.Sql`, or provider-specific APIs; distinguished database evaluation from local CLR execution. |
| `docs/scalar-functions/01-string-functions.md` | `docs/ru/scalar-functions/01-string-functions.md` | Reframed collation guidance as a choice: ordinary operators follow database collation; supported explicit `StringComparison` forms use database-side ordinal comparison and provider-specific limits. |
| `docs/scalar-functions/02-math-functions.md` | `docs/ru/scalar-functions/02-math-functions.md` | Clarified API selection and database-vs-CLR evaluation; identified the SQLite integration seed data behind the example result. |
| `docs/scalar-functions/03-date-and-time.md` | `docs/ru/scalar-functions/03-date-and-time.md` | Made `DateTimeOffset` storage/offset behavior and database-clock evaluation explicit; grouped unsupported members and `TimeOnly` provider/converter boundaries by decision. |
| `docs/scalar-functions/04-conditionals-and-conversion.md` | `docs/ru/scalar-functions/04-conditionals-and-conversion.md` | Clarified helper selection, provider-specific NULL behavior for extrema, and SQL three-valued conditional behavior. |
| `docs/scalar-functions/05-aggregates.md` | `docs/ru/scalar-functions/05-aggregates.md` | Distinguished empty ungrouped input, empty grouped input, empty grouping sets, and existing all-NULL groups; clarified `grouping_id` and the ClickHouse version caveat. |
| `docs/scalar-functions/06-arrays.md` | `docs/ru/scalar-functions/06-arrays.md` | Contrasted PostgreSQL's single array parameter with ClickHouse `Array(T)` binding and element-type rules; removed a redundant table lead-in. |
| `docs/scalar-functions/07-json-and-xml.md` | `docs/ru/scalar-functions/07-json-and-xml.md` | Clarified JSON operand types, key/index/path parameter forms, JSONPath-vs-key lookup, PostgreSQL 17 forms, `_tz` selection, and row-producing readers. |
| `docs/scalar-functions/08-reference.md` | `docs/ru/scalar-functions/08-reference.md` | Marked the matrix as a selected SQLite/SQL Server/PostgreSQL comparison, not a complete support inventory; separated in-memory evaluation from SQL mappings. |
| `docs/scalar-functions/09-system-functions.md` | `docs/ru/scalar-functions/09-system-functions.md` | Separated client-reported values, server facts, and authenticated identities; organized per-name gates, runtime conditions, and connection/session scope. |
| `docs/scalar-functions/10-sequences.md` | `docs/ru/scalar-functions/10-sequences.md` | Distinguished sequence advancement from `currval`/`lastval` reads and explained PostgreSQL session and MariaDB connection scope. |
| `docs/guide/provider-specific-sql/overview.md` | `docs/ru/guide/provider-specific-sql/overview.md` | Clarified that the exclusive-constructs list is not a full portability matrix and directed readers to concept pages for shared behavior. |
| `docs/guide/provider-specific-sql/sqlite.md` | `docs/ru/guide/provider-specific-sql/sqlite.md` | **Unchanged.** The body already identifies the `soundex` refusal, FTS boundaries, and SQLite version requirements at the relevant sections. |
| `docs/guide/provider-specific-sql/postgresql.md` | `docs/ru/guide/provider-specific-sql/postgresql.md` | Added choices for array values vs. `unnest`, native ranges vs. `RangeColumns`, JSON mapping, and row-producing functions; clarified Russian terminology. |
| `docs/guide/provider-specific-sql/mysql.md` | `docs/ru/guide/provider-specific-sql/mysql.md` | Clarified that a shared `SqlFunctions.MySql` entry point does not imply shared MySQL/MariaDB support; directed readers to member-level provider/version limits. |
| `docs/guide/provider-specific-sql/sqlserver.md` | `docs/ru/guide/provider-specific-sql/sqlserver.md` | Distinguished declared-version checks from runtime database prerequisites; identified scalar, aggregate, ordered-set, and rowset forms; reorganized dense T-SQL catalogue details. |
| `docs/guide/provider-specific-sql/clickhouse.md` | `docs/ru/guide/provider-specific-sql/clickhouse.md` | Clarified array/map result types, SQL-vs-CLR mapping, version and NULL behavior, unsupported-function reasons, and provider-specific choices; replaced Russian jargon with plain explanations. |

**Totals:** 34 pages reviewed; 32 edited; the SQLite pair was reviewed and left
unchanged for the body-specific reason above.

## Substantive before/after examples

- **String comparison:** the revised text tells readers that `==` and basic `LIKE`
  forms use database collation, while supported explicit ordinal overloads still execute
  in SQL using the provider's binary collation. It also calls out unsupported culture-aware
  comparisons and SQLite's ASCII `LIKE` limitation.
- **Date/time:** the `DateTimeOffset` section now leads with the consequence of each
  provider's SQL storage type: SQL Server retains the original offset, while other
  providers materialize offset zero; MySQL/MariaDB store wall time without that offset.
  Unsupported operations and `TimeOnly` conversion boundaries are grouped separately.
- **JSON:** the PostgreSQL pages distinguish a JSON key lookup from a JSONPath query,
  show the SQL types used for keys, indexes, and paths, and explain when a path array is
  one PostgreSQL array parameter. The `_tz` functions and table-function placement now
  have explicit selection guidance.
- **SQL Server:** the versioned catalogue now labels query form and separates the
  provider's declared-major-version check from database compatibility, preview-feature,
  and collation prerequisites that require a suitably configured server.
- **ClickHouse:** dense mapping notes now distinguish server result types from declared
  CLR result types and explain why map helpers can return arrays or tuples instead of a
  server `Map`.

## Checks and limits

- Reviewed the body-level diffs for all 34 pages and recorded a disposition for each
  English/Russian pair.
- `git diff --check` on the assigned page set: exit 0, no output.
- CRLF check: all 32 edited pages and this review note use CRLF; no mixed or LF-only
  line endings found in those 33 files.
- No DocFX, build, test, or product-code changes were made, as requested.
- This is an editorial readability review, not an independent semantic audit against
  product code. Existing technical claims were preserved rather than revalidated, and
  generated/rendered documentation was not checked.
