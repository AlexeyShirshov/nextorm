# Readability inventory: assigned documentation

**Date:** 2026-11-16

**Scope:** 34 assigned English and Russian pages: getting-started 03/04; all
provider and infrastructure pages and their Russian counterparts; `docs/ru/overview.md`,
`docs/ru/motivation.md`; and the invariant string-format pages.

**Result:** 23 pages received prose edits; 11 were retained. The edits improve
task framing, split dense explanations, and clarify conditions and consequences.
They preserve the existing facts, examples, and headings. “Retained” means no
editorial text change; four retained provider pages also had line endings
normalized to CRLF.

## Per-page inventory

| Page | Status | Reason and concrete example |
|---|---|---|
| `docs/getting-started/03-entities-and-metadata.md` | Changed | Reframed the mapping introduction from “Before it can build SQL” to “Before NextORM can build SQL”; clarified when metadata is resolved, cached, and why an unregistered `FROM` type fails. |
| `docs/getting-started/04-dependency-injection.md` | Changed | Separated the options and type registration paths into shorter descriptions; states that the options factory calls `CreateDataContext` and that DI must resolve the context constructor. |
| `docs/ru/getting-started/03-entities-and-metadata.md` | Changed | Clarified that the first `From` call resolves metadata and that `Metadata` caches it by type for the process; made the unregistered `FROM` failure direct. |
| `docs/ru/getting-started/04-dependency-injection.md` | Changed | Names registration through a configuration delegate and registration by context type separately; explains the factory and the shared `IDataContext` instance. |
| `docs/connecting-providers/overview.md` | Changed | Replaced the vague “choice table helps you pick a package” lead-in with guidance to use the matrix to check provider-specific support; clarifies that a shared API does not imply universal support. |
| `docs/connecting-providers/sqlite.md` | Changed | Connects the `DEBUG` file-existence check to the file-path example instead of leaving it as a standalone note under registration. |
| `docs/connecting-providers/mysql.md` | Changed | Split the long string-literal explanation into inline literal behavior and cached parameter-slot behavior, including execution-time lowering and restoration. |
| `docs/connecting-providers/clickhouse.md` | Retained | The feature catalog is grouped by SQL syntax, date functions, aggregates, query shape, and modifiers; the version section separately explains the 25.8 baseline and 25.9 `arrayExcept` gate. |
| `docs/connecting-providers/mariadb.md` | Retained | The server-version section distinguishes the explicit 13.0 `UPDATE ... RETURNING` gate from individual function minimums; the existing table makes those thresholds scannable. |
| `docs/connecting-providers/postgres.md` | Retained | The version section clearly contrasts the unset-version behavior with explicit 18+ support for `array_sort` and `uuidv7`; detailed behavior remains in provider-specific lists and tables. |
| `docs/connecting-providers/sqlserver.md` | Changed | Reordered the `WITH TIES` explanation as condition, exception timing, SQL Server restriction, and supported alternative. |
| `docs/connecting-providers/in-memory.md` | Retained | The supported-feature table and the separate unsupported section distinguish what in-memory query evaluation can and cannot validate; the example shows data attachment and query use. |
| `docs/ru/connecting-providers/overview.md` | Changed | Reworked the table introduction to tell readers when to use the package-choice table and when to consult the provider-support matrix. |
| `docs/ru/connecting-providers/sqlite.md` | Changed | Moved the `DEBUG` file-existence behavior into the lead-in for the file-path example and clarified connection ownership. |
| `docs/ru/connecting-providers/mysql.md` | Changed | Split literal rendering from parameter-slot lowering; explains that `@name` is restored before the next execution. |
| `docs/ru/connecting-providers/clickhouse.md` | Retained | The existing sections group date conversion, aggregates, query shape, modifiers, and limitations; version-specific behavior is separated under its own heading. |
| `docs/ru/connecting-providers/mariadb.md` | Retained | The explicit server-version section and function-minimum table distinguish provider support from individual server gates. |
| `docs/ru/connecting-providers/postgres.md` | Changed | Split the `TimeSpan` interval rule: non-zero calendar months are rejected; zero-month days and microseconds use checked conversion and report overflow. |
| `docs/ru/connecting-providers/sqlserver.md` | Changed | Made the `WITH TIES` condition and failure timing easier to scan; removed a duplicated phrase in the connection-state function paragraph. |
| `docs/ru/connecting-providers/in-memory.md` | Changed | Replaced the mixed-language “контекст in-memory” with “контекст” and the colloquial “громко падает” with the direct “выбрасывает исключения”. |
| `docs/infrastructure/01-connections.md` | Changed | Split type inheritance from connection ownership; the prose now directly contrasts a connection string with a caller-supplied `DbConnection`. |
| `docs/infrastructure/02-interceptors.md` | Retained | The callback table, lifecycle diagram, and callback bullets already make event timing and behavior findable. |
| `docs/infrastructure/03-value-converters.md` | Changed | Broke the JSON provider comparison into MySQL/MariaDB and SQL Server cases; preserves the `Auto`, `Text`, and `Native` behavior and the server error 13609 constraint. |
| `docs/infrastructure/04-logging.md` | Changed | Joined the logger-count statement to the table introduction, removing a redundant standalone lead-in. |
| `docs/infrastructure/05-nativeaot.md` | Retained | The page separates prerequisites, enabling steps, support, limitations, and performance; the XML and publish examples remain directly under their instructions. |
| `docs/ru/infrastructure/01-connections.md` | Changed | Split context inheritance from ownership guidance; directly explains who creates and owns a connection for each constructor path. |
| `docs/ru/infrastructure/02-interceptors.md` | Changed | Rewrote the DI registration lead-in and clarified why type registration fails in the EF Core bridge without a container-backed builder. |
| `docs/ru/infrastructure/03-value-converters.md` | Changed | Split the dense JSON provider comparison into MySQL/MariaDB and SQL Server paragraphs; keeps each storage mode and limitation explicit. |
| `docs/ru/infrastructure/04-logging.md` | Changed | Combined the logger count and table introduction into one sentence, matching the English page. |
| `docs/ru/infrastructure/05-nativeaot.md` | Retained | The procedure keeps prerequisites, project settings, and publish command in order; support limits and performance notes have separate sections. |
| `docs/ru/overview.md` | Retained | The opening paragraph already states what NextORM does and its goal of separating the data-access layer from a specific relational database. |
| `docs/ru/motivation.md` | Retained | The three approaches are compared consistently through advantages, limitations, and use cases, followed by the NextORM position. |
| `docs/string-format-invariant-provider.md` | Changed | Split the translator/culture explanation and the structural-recognition rule; explicitly states that the getter is not invoked or evaluated. |
| `docs/ru/string-format-invariant-provider.md` | Changed | Split the culture constraint and structural-recognition explanation; states that the expression is not compiled or evaluated. |

## Validation

- Compared all 34 pages with the takeover snapshot at
  `/tmp/opencode/editorial-takeover-before`, created 2026-11-16 22:03:54.602314221
  +0500 (17:03:54.602314221 UTC): headings and fenced-block contents match the
  snapshot; this comparison does not assert fenced-block ordering.
- This snapshot comparison does not mean the pages match repository HEAD. At
  HEAD `7612b8f580296a56992a481921a0ce8c8747791b`, the 34-page set had 332
  headings and 330 fenced blocks, compared with 363 headings and 336 fenced
  blocks now. HEAD/current heading inventories differ on 20 pages; fenced-block
  content differs on 14 pages, with two additional pages differing only in
  fenced-block order. The matching snapshot/current headings and fenced-block
  contents show these differences were already present at takeover, rather than
  introduced by this prose-edit pass; this inventory does not claim the pages
  are otherwise unchanged from HEAD.
- All 34 pages use CRLF line endings.
- All checked local Markdown file targets exist. Anchors were not validated.
- No build, test, restore, package verification, or DocFX run was performed, as
  required by the task scope.
