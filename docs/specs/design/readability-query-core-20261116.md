# Query-guide readability review (2026-11-16)

## Scope and approach

This review covers the English query guide pages `01` and `05`–`23`, plus their Russian counterparts
(42 pages total: 21 English and 21 Russian; both `13` pages are included). Pages `02`–`04` are outside
the review scope. The review focused on the reading path within each guide: state the task, show the
shortest useful workflow, explain what the example returns, then place alternatives and
provider-specific constraints where readers need them.

All comparisons and API wording in this report refer to the current working tree and the `HEAD`
baseline for these docs. No released NuGet package, package installation/restore, or published version
was used as a validation target. The review retained every original heading and fenced example, while
adding 22 headings and two mirrored filtered-projection examples.

## Page-by-page rationale

| English page | Russian counterpart | Body-specific obstacle and rationale |
|---|---|---|
| `docs/guide/01-query-sources.md` | `docs/ru/guide/01-query-sources.md` | The source guide introduced query sources before explaining the basic chain. A short `From`/`Where`/`Select` example now shows a filtered projection and explains what each step returns; source-specific choices and per-query overrides remain separate. Each page adds one equivalent filtered-projection example. |
| `docs/guide/05-grouping-and-aggregates.md` | `docs/ru/guide/05-grouping-and-aggregates.md` | The overview now interprets the grouped result and routes readers among grouped summaries, `Having`, whole-source aggregates, extreme-row selection, and provider-specific functions. AVG’s nullable result and argument policy follow the basic grouping examples rather than interrupting the first workflow. The Russian page distinguishes direct aggregate methods from SQL aggregate expressions and retains the provider caveats. |
| `docs/guide/06-sorting-and-paging.md` | `docs/ru/guide/06-sorting-and-paging.md` | The opening now routes readers to property ordering, projected-column ordering, pages, single-row terminals, or `Any`. Example output explains descending order and tie-break keys. `Limit`, `Offset`, and `Page` were moved earlier, before NULL-ordering details, and show provider SQL, including SQL Server’s injected order and SQLite’s offset-only sentinel. HEAD and the current tree each contain one `Limit/Offset` and one `Page` section per page; no duplicate removal relative to HEAD is evidenced. |
| `docs/guide/07-subqueries.md` | `docs/ru/guide/07-subqueries.md` | The initial API inventory is now organized by the outer query’s need: rows, one scalar, or a predicate. The long derived-source explanation distinguishes composing from materializing, explains supported projection shapes and the single-column cache case, and separates correlated aggregate projection from filtering. The Russian page keeps the same distinctions while splitting dense paragraphs. |
| `docs/guide/08-set-operations.md` | `docs/ru/guide/08-set-operations.md` | The operation list now leads with result semantics: combine, intersect, exclude, or preserve duplicate counts. The right-side projection and selector overload are explained where readers choose a form, and the `ALL` provider limitation stays explicit. The Russian page follows the same result-first path. |
| `docs/guide/09-distinct.md` | `docs/ru/guide/09-distinct.md` | The guide now establishes that `Distinct` compares the complete projected tuple, then explains its use with a single projection, joins, paging, set-operation branches, and PostgreSQL `DISTINCT ON`. The SQL Server keyword-order explanation is tied to its example. The Russian page retains the same distinctions and provider behavior. |
| `docs/guide/10-cte.md` | `docs/ru/guide/10-cte.md` | The opening now explains why a CTE is useful before describing the string and typed APIs. The examples lead into typed projection behavior, dependency ordering, recursive anchor/step rules, and depth limits. Join-overload choices and write-CTE behavior are separated from read workflows. The Russian page preserves the same constraints and provider-specific syntax. |
| `docs/guide/11-window-functions.md` | `docs/ru/guide/11-window-functions.md` | The guide now explains that a window function keeps each row, introduces `.Over(...)`, and shows a partition example before the full function matrix. The expression-tree restriction and missing-`Over` failure remain stated next to window setup. The Russian page uses the same learning order. |
| `docs/guide/12-user-defined-functions.md` | `docs/ru/guide/12-user-defined-functions.md` | The attribute is introduced as a way to call an existing database function from a query. Placeholder execution, name/schema selection, captured parameters, instance receivers, and precedence of built-in translations are explained around the mapping example. The Russian page follows the same setup and boundaries. |
| `docs/guide/13-table-valued-functions.md` | `docs/ru/guide/13-table-valued-functions.md` | The main path now starts with a factory, its descriptor, and passing the factory call as an expression to `From`; this distinguishes SQL metadata from runtime arguments. Constructor choices and correlated `CrossApply`/`OuterApply` are explained at their use sites. The Russian page makes the same distinction and keeps provider behavior intact. |
| `docs/guide/13-tvf-result-schemas.md` | `docs/ru/guide/13-tvf-result-schemas.md` | The page now starts from the task of declaring output columns, explains where `TableFunctionSchema` places them, and then presents the supported SQL forms. DTO materialization, plan-key behavior, verbatim ClickHouse SQL safety, and provider rejection of unsupported forms remain explicit. The Russian page follows the same selection path. |
| `docs/guide/14-raw-sql.md` | `docs/ru/guide/14-raw-sql.md` | The introduction now distinguishes replacing a typed statement (`WithSql`/`PrepareFromSql`), using raw SQL as a source (`FromSql`), and executing standalone commands or procedures. Projection matching, parameter-object binding, and the async `params`/cancellation-token constraint are explained where each API appears. The Russian page mirrors those choices with clearer separation of planner and raw-command parameters. |
| `docs/guide/15-query-hints.md` | `docs/ru/guide/15-query-hints.md` | The hint guide is organized by use: statement hints, table/join hints, tags, and ClickHouse query modifiers. Provider limits and the warning about hinted set-operation branches remain near the relevant choices. The Russian page separates the same concepts and retains the fail-fast/provider qualifications. |
| `docs/guide/16-json.md` | `docs/ru/guide/16-json.md` | A task map now distinguishes streaming query output, SQL Server’s single JSON document, text-column functions, and native JSON-column mapping before the provider matrix. The `WriteJson` and `ForJson` paths, SQL Server version declaration, PostgreSQL contextual typing, and provider-specific behavior remain distinct. The Russian page adds the same routes and uses task-oriented wording instead of treating provider APIs as one portable feature. |
| `docs/guide/17-insert-statement.md` | `docs/ru/guide/17-insert-statement.md` | The opening now routes readers among a single explicit value, entity or collection values, server-side `INSERT ... SELECT`, returned keys/rows, and bulk insert. Generated-column mapping, CTE-source behavior, unsupported placeholders/defaults, and provider qualifications remain attached to those workflows. The Russian page gives the same choices before the detailed forms. |
| `docs/guide/18-delete-statement.md` | `docs/ru/guide/18-delete-statement.md` | The guide now routes among predicate deletion, explicit all-row deletion, key deletion, returning rows, truncate, and joined deletion. `All()` is described as deliberate; global-filter behavior and PostgreSQL-only joined returning are separated from the basic predicate path. The Russian page mirrors those safety and provider constraints. |
| `docs/guide/19-update-statement.md` | `docs/ru/guide/19-update-statement.md` | The introduction distinguishes setting selected columns and filtering rows from updating one entity by key. A new warning makes the broad effect of omitting `Where` explicit, while retaining the interaction with global query filters. The Russian page gives the same warning in direct language. |
| `docs/guide/20-create-table-as.md` | `docs/ru/guide/20-create-table-as.md` | The introduction now distinguishes immediate temporary-table creation, lazy creation on a later read, and persistent-table creation, with the CTE contrast kept short. Routing details were moved after the common lifecycle/session discussion so the initial choice is not buried in topology constraints. The Russian page follows the same order and preserves routing semantics. |
| `docs/guide/21-merge-statement.md` | `docs/ru/guide/21-merge-statement.md` | The opening now distinguishes key upsert from full multi-branch `MERGE` by the operation required. Runtime mapping-variant details were moved after the basic forms so tenant/shard handling is available without dominating the introduction. Provider differences remain explicit. The Russian page makes the same choice clear. |
| `docs/guide/22-bulk-insert.md` | `docs/ru/guide/22-bulk-insert.md` | The first paragraph now gives the ordinary `Values`/`BulkInsert` path and points to execution modes for choosing native versus parameterized insertion. The mapped-entity example and its generated identity explanation now appear together. The Russian page mirrors the same sequence. |
| `docs/guide/23-transactions.md` | `docs/ru/guide/23-transactions.md` | The introduction now begins with the use case—several commands sharing commit or rollback—then explains owned versus external transactions and provider limits. The `ITransactionManager` member listing is a dedicated section after the workflow rather than interrupting the opening. The Russian page uses the same reader-first order. |

## Before/after examples

| Before | After | Why |
|---|---|---|
| `From`/`Where`/`Select`/`ToList` appeared as a chain without explaining its result. | The source guide states that `Where` filters by `Id`, `Select` keeps that value, and `ToList` executes and returns the list. | Readers can interpret the minimal example before choosing another source form. |
| The set-operation guide began with six method names. | It first asks whether the reader needs duplicate removal, common rows, left-only rows, or preserved duplicate counts. | The operation can be selected from the desired result rather than memorized by name. |
| Raw SQL APIs were introduced together as a dense signature/parameter inventory. | The opening separates typed-statement replacement, raw `FROM` sources, and standalone commands/procedures. | Each entry point is tied to a distinct task before its overload details. |
| The update introduction stated that an update builder has an optional `WHERE`. | The guide now says explicitly that omitting `Where` updates every row allowed by global filters. | The broad effect is visible at the decision point, not inferred from SQL syntax. |
| Transaction-role signatures appeared in the opening explanation. | The opening first explains when to use a transaction; the interface is listed in its own later section. | The use case and execution model precede the complete API reference. |

## Verification and limits

The counts below compare the 42 current-tree guide pages with `HEAD`. Independent evidence is
`/tmp/opencode/readability-query-core-independent-check.json`.

- Headings: `HEAD` has 580 and the current pages have 602. The original-heading multiset has 0
  missing entries; 22 headings were added.
- Fenced code: `HEAD` has 982 blocks and the current pages have 984. The original-block multiset has
  0 missing entries; the two added blocks are the mirrored filtered-projection examples in the `01`
  pages.
- Paging sections: in both English and Russian `06` pages, `Limit/Offset` and `Page` each occur once
  in `HEAD` and once in the current tree. They moved earlier: English headings moved from lines 256
  and 284 to 137 and 165; Russian headings moved from 258 and 286 to 139 and 167.
- Links: the scan parsed 1,491 inline Markdown destinations (0 reference definitions), skipped 824
  `xref` and 6 HTTPS destinations, checked 518 local relative file targets (all exist), and counted
  143 same-page or fragment destinations without validating their anchors. No full anchor or DocFX
  validation was performed.
- All 42 guide pages and this report use CRLF. `git diff --check HEAD -- <42 scoped guide pages>`
  exited 0; the report has no trailing-whitespace lines.
- No build, tests, package installation/restore, released-package validation, or DocFX command was
  run. The earlier `/tmp/opencode/readability-query-core-20261116-verification.log` remains a
  historical artifact and is not the source for these corrected counts.
