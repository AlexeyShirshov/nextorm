# D224 E06 — public docs re-layout (R5, CHECK-gather round r=1)

Scope: this CHECK-gather round re-normalised every public page that still carried an inline
generated `from`/`join`, so that each generated-SELECT ```sql block puts `from`/`join` on its
own line with exactly one leading space. **34 EN pages + their 34 RU twins** (`.md` only; the
`docs/assets/diagrams/nextorm-command-execution.svg` + RU twin the DO commit touched are
images, not pages). Transformer: `artifacts/pdca/D224/rv1/rewrite_docs_sql.py`; docs stay CRLF.

A second pass in the same round closed the residuals the ```sql-only pass could not reach: it
taught the transformer (a) to de-indent a line-start `from`/`join` inside a ```sql fence whose
indent is *wider* than the layout's single space (the earlier `needs_change` treated any
line-start clause as hand-drawn layout and skipped it), and (b) to re-render generated-SQL `//`
comment depictions inside ```csharp fences (see “Pass 2”).

## Residual scan (idempotence)

```
$ python3 artifacts/pdca/D224/rv1/rewrite_docs_sql.py --dry-run
--- dry-run: 0 insertion(s) in 0 file(s)
```

exit 0. → no generated-SELECT block under `docs/**` + `docs/ru/**` is left with an inline
`from`/`join` that the transformer would move; the only remaining occurrences are the
classified, intentionally preserved exceptions in the next section. (The script's default mode
is already a dry run; `--dry-run` is accepted as an explicit alias and the command above is the
exact one recorded here.) Re-run after the pass-2 `--apply` (`38 insertion(s) in 16 file(s)`):
same `0 insertion(s) in 0 file(s)`, exit 0 — the transformer is idempotent across both passes.

## Pass 2 — residuals closed in this round (16 files: 8 EN + 8 RU)

Two shapes were still off-layout; both are generated-SQL depictions and both are fixed EN+RU in
the same change.

**(a) Over-indented `from` inside a ```sql fence** (the flat layout uses exactly one leading
space; the enclosing derived table's `select`/`where` lines keep their author indentation and
are out of scope — `docs/advanced/eager-loading-per-parent-paging.md:87` already used the
one-space form in the same fence, which is the in-page precedent):

```
- select t1.id, t1.order_id, t1.created_at    docs/advanced/eager-loading-per-parent-paging.md:69
-    from order_item
+ from order_item
```

| EN page | RU twin | line |
|---|---|---|
| `docs/advanced/eager-loading-per-parent-paging.md` | `docs/ru/advanced/eager-loading-per-parent-paging.md` | `:69` |
| `docs/guide/07-distinct.md` | `docs/ru/guide/07-distinct.md` | EN `:270` (`from <source>`), RU `:273` (`from <источник>`) |

**(b) Generated-SQL `//` comment depictions inside ```csharp fences.** The comment marker's own
single space is the layout's one leading space, so each `from`/`join` clause is written as
`// from ...` / `// join ...` on its own comment line; only `from`/`join` placement changes (a
later `where`/`order by`/provider note stays where it was, exactly as in the ```sql pass).

```diff
-// select c.Id, p.Name from child as c left join parent as p on c.ParentId = p.Id
+// select c.Id, p.Name
+// from child as c
+// left join parent as p on c.ParentId = p.Id
```
(that is `docs/guide/29-implicit-navigation.md:48`, and identically `:91` with
`select c.Id, p.Id, p.Age, p.Name`; RU twin `:48`, `:92`)

```diff
-// select id, name from product where (id > 1)           docs/getting-started/03-entities-and-metadata.md:185, :324-326
+// select id, name
+// from product where (id > 1)

-// select id from simple_entity order by (select null as anyorder)   docs/connecting-providers/sqlserver.md:67, :70-72
+// select id
+// from simple_entity order by (select null as anyorder)

-// PostgreSQL:  select /*+ SeqScan(simple_entity) */ id from simple_entity  docs/guide/13-query-hints.md:199, :204
+// PostgreSQL:  select /*+ SeqScan(simple_entity) */ id
+// from simple_entity

-// with recent as (select id from simple_entity where (id > 1))   docs/guide/15-insert-statement.md:194-195, :211
+// with recent as (select id
+// from simple_entity where (id > 1))

-//  select id from __nextorm_temp_xxxxxxxx                         docs/guide/18-create-table-as.md:112-114
+// select id
+// from __nextorm_temp_xxxxxxxx
```

| EN page | RU twin | comment lines |
|---|---|---|
| `docs/getting-started/03-entities-and-metadata.md` | `docs/ru/getting-started/03-entities-and-metadata.md` | EN `:185`, `:324-326` / RU `:173`, `:306-308` |
| `docs/guide/29-implicit-navigation.md` | `docs/ru/guide/29-implicit-navigation.md` | EN `:48`, `:91` / RU `:48`, `:92` |
| `docs/guide/15-insert-statement.md` | `docs/ru/guide/15-insert-statement.md` | EN `:194-195`, `:211` / RU `:195-196`, `:213` |
| `docs/guide/13-query-hints.md` | `docs/ru/guide/13-query-hints.md` | EN `:199`, `:204` / RU `:204`, `:209` |
| `docs/guide/18-create-table-as.md` | `docs/ru/guide/18-create-table-as.md` | EN `:112-114` / RU `:124-126` |
| `docs/connecting-providers/sqlserver.md` | `docs/ru/connecting-providers/sqlserver.md` | EN `:67`, `:70` / RU `:67`, `:70` |

Three deliberate details of the pass-2 rendering:

1. the clause keeps its phrase, so the emitted `LEFT JOIN` reads `// left join parent as p on …`
   (the brief's illustration elided the `left` modifier); the ```sql pass does the same
   (` left join (` at `docs/advanced/eager-loading-per-parent-paging.md:85`);
2. a trailing provider note stays at the end of the clause it followed, e.g.
   `// from "simple_entity" where ("id" > 1)     (PostgreSQL, SQLite)`;
3. re-rendering a run normalises its statement-continuation prefixes to `// ` — so the
   `ToBatchSql` batch at `docs/guide/18-create-table-as.md:112-114` loses the two extra spaces
   it used for its 2nd/3rd statement (`//  create …`, `//  select …` → `// create …`,
   `// select …`) while gaining the split `// from __nextorm_temp_xxxxxxxx`.

Raw-mirror comments (`… -> select … from …`) are skipped by an explicit `->` guard in
`rewrite_comment_line`, so the opaque `ExecuteRaw` arguments on the clickhouse / `12-raw-sql`
pages are never reflowed (they are the pass-5 scan hits still listed in the residual section).

## Pass 1 — known miss fixed

`docs/guide/04-sorting-and-paging.md:106-112` (the `Page(10, 0)` / `complex_entity` sample) was
still inline in the previous round:

```
-select nullableint as 'Int', count(*) as 'Count' from complex_entity
+select nullableint as 'Int', count(*) as 'Count'
+ from complex_entity
  group by nullableint
  order by count(*) desc
```

It now reads (line 112 = ` from complex_entity`, one leading space; CRLF preserved), and the RU
twin `docs/ru/guide/04-sorting-and-paging.md:109-112` was fixed identically.

## Pass 1 pages changed (34 EN + 34 RU twins)

| # | EN page | RU twin |
|---|---|---|
| 1 | `docs/advanced/eager-loading-per-parent-paging.md` | `docs/ru/advanced/eager-loading-per-parent-paging.md` |
| 2 | `docs/advanced/eager-loading.md` | `docs/ru/advanced/eager-loading.md` |
| 3 | `docs/advanced/relationships.md` | `docs/ru/advanced/relationships.md` |
| 4 | `docs/advanced/select-where-extrema-native.md` | `docs/ru/advanced/select-where-extrema-native.md` |
| 5 | `docs/connecting-providers/postgres.md` | `docs/ru/connecting-providers/postgres.md` |
| 6 | `docs/connecting-providers/sqlite.md` | `docs/ru/connecting-providers/sqlite.md` |
| 7 | `docs/connecting-providers/sqlserver.md` | `docs/ru/connecting-providers/sqlserver.md` |
| 8 | `docs/guide/01-filtering-where.md` | `docs/ru/guide/01-filtering-where.md` |
| 9 | `docs/guide/02-joins.md` | `docs/ru/guide/02-joins.md` |
| 10 | `docs/guide/03-grouping-and-aggregates.md` | `docs/ru/guide/03-grouping-and-aggregates.md` |
| 11 | `docs/guide/04-sorting-and-paging.md` | `docs/ru/guide/04-sorting-and-paging.md` |
| 12 | `docs/guide/05-subqueries.md` | `docs/ru/guide/05-subqueries.md` |
| 13 | `docs/guide/07-distinct.md` | `docs/ru/guide/07-distinct.md` |
| 14 | `docs/guide/08-cte.md` | `docs/ru/guide/08-cte.md` |
| 15 | `docs/guide/09-window-functions.md` | `docs/ru/guide/09-window-functions.md` |
| 16 | `docs/guide/11-table-valued-functions.md` | `docs/ru/guide/11-table-valued-functions.md` |
| 17 | `docs/guide/13-query-hints.md` | `docs/ru/guide/13-query-hints.md` |
| 18 | `docs/guide/14-json.md` | `docs/ru/guide/14-json.md` |
| 19 | `docs/guide/16-delete-statement.md` | `docs/ru/guide/16-delete-statement.md` |
| 20 | `docs/guide/17-update-statement.md` | `docs/ru/guide/17-update-statement.md` |
| 21 | `docs/guide/25-range-columns.md` | `docs/ru/guide/25-range-columns.md` |
| 22 | `docs/guide/30-projections.md` | `docs/ru/guide/30-projections.md` |
| 23 | `docs/guide/31-query-sources.md` | `docs/ru/guide/31-query-sources.md` |
| 24 | `docs/guide/32-provider-specific-features.md` | `docs/ru/guide/32-provider-specific-features.md` |
| 25 | `docs/guide/provider-specific-sql/clickhouse.md` | `docs/ru/guide/provider-specific-sql/clickhouse.md` |
| 26 | `docs/guide/provider-specific-sql/mysql.md` | `docs/ru/guide/provider-specific-sql/mysql.md` |
| 27 | `docs/guide/provider-specific-sql/postgresql.md` | `docs/ru/guide/provider-specific-sql/postgresql.md` |
| 28 | `docs/guide/provider-specific-sql/sqlite.md` | `docs/ru/guide/provider-specific-sql/sqlite.md` |
| 29 | `docs/guide/provider-specific-sql/sqlserver.md` | `docs/ru/guide/provider-specific-sql/sqlserver.md` |
| 30 | `docs/scalar-functions/01-string-functions.md` | `docs/ru/scalar-functions/01-string-functions.md` |
| 31 | `docs/scalar-functions/03-date-and-time.md` | `docs/ru/scalar-functions/03-date-and-time.md` |
| 32 | `docs/scalar-functions/05-aggregates.md` | `docs/ru/scalar-functions/05-aggregates.md` |
| 33 | `docs/scalar-functions/06-arrays.md` | `docs/ru/scalar-functions/06-arrays.md` |
| 34 | `docs/scalar-functions/07-json-and-xml.md` | `docs/ru/scalar-functions/07-json-and-xml.md` |

## Residual preserved occurrences (classified, intentionally unchanged)

Every remaining hit of the two residual scans is one of the classes below; there is no
generated-SELECT depiction left off-layout.

1. **18 raw-mirror clause positions in ```sql blocks** — SQL literals shown verbatim inside a
   ```sql block that appears as a C# string literal on the same page (`WithSql` / `FromSql` /
   `WithTableExpression` / `ExecuteRaw`). The transformer locates them via `csharp_raw_sql()`
   and excludes them; opaque raw input is never reflowed (contract). Re-derived by instrumenting
   the transformer's own exclusion set (18 positions, EN+RU):
   - `docs/advanced/query-filters.md:502`, `:507` + RU `docs/ru/advanced/query-filters.md:502`, `:507`;
   - `docs/guide/12-raw-sql.md:62`, `:92`, `:210`, `:226`, `:262` + RU
     `docs/ru/guide/12-raw-sql.md:64`, `:94`, `:213`, `:229`, `:266`;
   - `docs/guide/31-query-sources.md:172` (2 nested `from`s) + RU
     `docs/ru/guide/31-query-sources.md:172` (2) — **total = 18** (the dry run above moves none).
2. **4 raw-mirror `//` comments** (scan-5 hits kept by the `->` guard, opaque `ExecuteRaw`
   arguments — never reflowed):
   - `docs/guide/12-raw-sql.md:753` and `docs/ru/guide/12-raw-sql.md:761`
     (`// entity set -> select t.1, t.2 from (select arrayJoin(@rows) as t)`);
   - `docs/connecting-providers/clickhouse.md:181` and `docs/ru/connecting-providers/clickhouse.md:183`
     (same fragment).
3. **`extract(... from ...)` function syntax** — `from` here is a function argument, not a
   clause: e.g. `extract(year from dt)` at `docs/scalar-functions/03-date-and-time.md:25` (+ RU),
   table cells `extract(quarter from dt)` etc. at `:47` (+ RU), `extract(epoch from (b - a))` at
   `:278` (+ RU), and `select ..., extract(year from dt) as "Year"` at
   `docs/connecting-providers/postgres.md:153` (+ RU `:154`). Not a generated clause boundary.
4. **2 prose lines matching the scan-4 pattern** (a plain sentence, not a SQL clause; neither
   sits in a fence, and neither has a twin in the other language — the EN `13-query-hints` and
   the RU `27-dynamic-columns` pages differ in layout):
   - `docs/guide/27-dynamic-columns.md:99` — `  from the dictionary is omitted — on `INSERT` …`;
   - `docs/ru/guide/13-query-hints.md:239` (was `:237` before the two-line split above) —
     `  join физической таблицы: APPLY-join, производная таблица, …`.
5. **2 malformed 08-cte prose lines** — `docs/guide/08-cte.md:136` and
   `docs/ru/guide/08-cte.md:137` are wrapped English/Russian prose inside the ```sql fence
   (`join of one descriptor -> one declaration, two aliases` / `join одного дескриптора -> ...`),
   listed in the transformer's `NON_CLAUSE_LINES` and preserved verbatim.
6. **Out of scope (internal, not published):** the scan-4 hits inside `docs/specs/**`
   (`docs/specs/roadmap/sql-capabilities-gap-analysis.md:385`,
   `docs/specs/roadmap/evidence-02-resultset-unification.md:324`,
   `docs/specs/experiments/94-skill-comparison-2/02-methodology.md:48`,
   `docs/specs/status/rc1-tail-134-sqlite-datareader-1.md:276`,
   `docs/specs/status/rc1-tail-194-pg-row-composite-1.md:453`) are prose in internal specs, not
   public pages — `docs/specs/**` is excluded from the transformer and from the DocFX build.

## Verdict

The transformer is idempotent across both passes (`0 insertion(s) in 0 file(s)`, exit 0); the
pass-1 known miss at `docs/guide/04-sorting-and-paging.md:106-112` and the pass-2 residuals
(over-indented `from` at `docs/advanced/eager-loading-per-parent-paging.md:69` /
`docs/guide/07-distinct.md:270`, and the generated-SQL `//` comments on the 6 EN + 6 RU pages)
are fixed, RU twins included. No generated-SELECT example remains inline or over-indented;
every remaining hit is one of the six residual classes above (raw/opaque mirror, function
syntax, non-SQL prose, internal specs). All 16 pass-2 files stay CRLF (LF count == CRLF count
per file). No public doc links to `docs/specs/**`; no page deleted or restored; `examples/**`,
diagrams and `.opencode/**` untouched.
