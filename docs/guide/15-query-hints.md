# Query hints

> Use `Hint(...)` to pass an optimizer hint with a query. Use `WithTag(...)` instead when you need to identify a statement in database logs or a profiler.

**Prerequisites:** [Querying and projections](../engine/index.md) · [CTE](10-cte.md) · [Provider overview](../connecting-providers/overview.md)

## Choose a hint type

- To pass an optimizer hint for the whole statement, use [`Hint(...)`](#overview).
- To label a statement for logs or a profiler, use [`WithTag(...)`](#query-tags).
- To control locking or index use for a table, see [Locking table hints](#locking-table-hints) and [Index hints](#index-hints).
- For hints on a join, subquery, or every table in scope, see [Join, subquery and tables-in-scope hints](#join-subquery-and-tables-in-scope-hints).
- For ClickHouse query modifiers, see [ClickHouse query modifiers](#clickhouse-query-modifiers). These modifiers are not hints.

## Overview

Use a statement-level optimizer hint when you need to influence how the database plans a query.
[`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) returns a new command carrying one or more
hints. The active [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) decides where and how to render them, so
the hint syntax depends on the provider.

Because `Hint` accepts `params string[]`, pass several hints in one call or add them in repeated calls.
Both forms are equivalent:

```csharp
// one call with several arguments:
var rows = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString })
    .Hint("recompile", "fast 10")
    .ToList();
```

```csharp
// equivalent two-call form:
var rows = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString })
    .Hint("recompile")
    .Hint("fast 10")
    .ToList();
```

```sql
select id, somestring
 from complex_entity where (id > 1) option (recompile, fast 10)
```

Blank hints are ignored. The hint list is part of the query plan key. A hinted command does not reuse the
cached plan of an otherwise identical unhinted command, and commands with different hints do not share a
plan.

## Query tags

`WithTag(string? tag)` attaches a free-form tag to a query. Unlike `Hint`, the tag is a plain SQL
comment (`/* tag */`), not an optimizer hint, so it renders on **every** SQL provider — it is not
gated. It is emitted immediately after the `SELECT` keyword:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTag("reports.orders")
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select /* reports.orders */ id
 from complex_entity where (id > 1)
```

Use a tag to find a statement in a profiler, server log, or server-side query store such as
`pg_stat_activity`, SQL Server Query Store, or ClickHouse `system.query_log`.

The tag is part of the plan key, so commands with different tags do not share a cached plan. Passing
`null` or an empty string clears the tag. The in-memory provider accepts the call and ignores it because
it generates no SQL.

nextorm neutralizes line breaks and the comment delimiters `*/` and `/*`. SQL Server nests block
comments, so nextorm escapes `/*` too. The comment always opens with a space, which prevents a tag that
starts with `!` or `+` from becoming a MySQL/MariaDB executable comment or optimizer hint. When a query
also carries `Hint(...)`, nextorm places the optimizer-hint comment first
(`select /*+ hint */ /* tag */ ...`) so `pg_hint_plan` and the MySQL/MariaDB optimizer still recognize it.

## Combining with a recursive CTE

SQL Server allows only one `OPTION` clause per statement. When a query also declared a CTE recursion
limit, the hints are merged into that same clause:

```csharp
var sql = dataContext.WithRecursive("nums", body, 100)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .Hint("recompile");
```

```sql
-- ends with:
... option (maxrecursion 100, recompile)
```

## Locking table hints

To attach SQL Server locking hints (`nolock`/`updlock`/`holdlock`) to the primary physical table, use
`FromOptions.WithTableHint(params string[] hints)`. SQL Server renders them in a `WITH (...)` clause
between the table name and its alias:

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.WithTableHint("nolock"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id
 from complex_entity with (nolock)
```

To attach a locking hint to one joined table, pass it through that join's trailing lambda with
[`JoinOptions.WithJoinTableHint(params string[] hints)`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])).
This is different from [`JoinOptions.WithJoinHint`](xref:NextORM.Core.JoinOptions.WithJoinHint(System.String)),
which adds an optimizer *join* hint such as `loop`, `hash`, or `merge` inside the join clause. It is also
different from `WithTablesInScopeHint`, which covers every physical table. SQL Server renders the table
hint as a `WITH (...)` clause on that joined table only:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Join<ISimpleEntity>((a, b) => a.Id == b.Id, j => j.WithJoinTableHint("nolock"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id
 from complex_entity as [t1]
 inner join simple_entity with (nolock) as [t2] on t1.id = t2.id
```

When you combine it with `WithTablesInScopeHint`, SQL Server still emits one `WITH (...)` clause on the
joined table. The join-local hint comes first, followed by the scope hints. Scope hints also cover every
other physical table:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTablesInScopeHint("holdlock")
    .Join<ISimpleEntity>((a, b) => a.Id == b.Id, j => j.WithJoinTableHint("nolock"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id
 from complex_entity with (holdlock) as [t1]
 inner join simple_entity with (nolock, holdlock) as [t2] on t1.id = t2.id
```

The hints are rendered verbatim, so pass only trusted values. A provider opts in through
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) and [`MakeTableHints`](xref:NextORM.Core.ISqlDialect.MakeTableHints(System.Collections.Generic.IReadOnlyList{System.String},NextORM.Core.KeywordCase)) (SQL Server); other dialects reject a
command that carries table hints with `NotSupportedException`. Pass `j => j.WithJoinTableHint(...)` to target a chosen
join; `WithTableHint` still covers only the primary table. A per-join table hint can only be rendered on a
**physical-table** join: an APPLY join (or a derived-table, table-valued-function, XML/pivot or raw-SQL join
source) has no table name to attach a `WITH (...)` clause to and is rejected with
`InvalidOperationException`. Passing only blank/null hints leaves any previously attached hints unchanged.
`WithTableHint` and `WithJoinTableHint` both require a dialect with table hints, so SQLite, ClickHouse and
the in-memory provider reject them with `NotSupportedException`.

PostgreSQL has no `WITH (...)` table-hint syntax — that clause is T-SQL-only. To lock a single table on
PostgreSQL or MySQL, use the source-level row lock ([`FromOptions.ForUpdate`](xref:NextORM.Core.FromOptions.ForUpdate)/
[`FromOptions.ForShare`](xref:NextORM.Core.FromOptions.ForShare), or [`JoinOptions.ForUpdate`](xref:NextORM.Core.JoinOptions.ForUpdate)/
[`JoinOptions.ForShare`](xref:NextORM.Core.JoinOptions.ForShare) on a join): it renders
`FOR UPDATE OF <alias>`/`FOR SHARE OF <alias>` — see
[Row locking](31-provider-specific-features.md#row-locking-for-update--for-share). Optimizer hints via
`pg_hint_plan` are the separate `Hint(...)` surface described above.

## Index hints

To guide index selection for the primary physical table, use
`FromOptions.WithIndex(params string[] indexes)`. The overload
`WithIndex(IndexHintKind kind, params string[] indexes)` selects the intent
([`IndexHintKind`](xref:NextORM.Core.IndexHintKind).`Use`/`Force`/`Ignore`), and `WithoutIndex()` suppresses
index use. Each dialect renders its native form after the table name and before its alias:

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.WithIndex("ix_complex_id"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
-- MySQL/MariaDB  (also `force index (...)` / `ignore index (...)`):
select id
 from complex_entity use index (ix_complex_id)
-- SQLite  (exactly one index; `WithoutIndex()` renders `not indexed`):
select id
 from complex_entity indexed by ix_complex_id
-- SQL Server  (merged into the single table-hint clause):
select id
 from complex_entity with (index(ix_complex_id))
```

Support depends on the dialect. A provider opts in through [`ISqlDialect.IndexHints`](xref:NextORM.Core.ISqlDialect.IndexHints) and
[`IIndexHintRenderer`](xref:NextORM.Core.IIndexHintRenderer) (MySQL/MariaDB, SQLite, SQL Server).
PostgreSQL (without `pg_hint_plan`), ClickHouse and the in-memory provider have no native index hint and
reject a command that carries one with `NotSupportedException`. On SQL Server an index hint is merged with
a locking `WithTableHint` into a single `with (nolock, index(...))` clause, and an `Ignore` hint is rejected
(there is no index-ignore hint); SQLite accepts exactly one index name (its `INDEXED BY` takes one). The
names are rendered verbatim, so only pass trusted values, and the hint list is part of the plan key.

## Providers

| Provider | Query hints |
|---|---|
| SQL Server | Supported: rendered as a trailing `OPTION (hint, ...)` clause. |
| PostgreSQL | Supported: rendered as an inline `/*+ hint ... */` comment immediately after `SELECT`, the position the optional `pg_hint_plan` extension reads; on a server without the extension it is an ordinary comment. |
| MySQL / MariaDB | Supported: rendered as an inline `/*+ hint ... */` optimizer-hint comment immediately after `SELECT`. |
| SQLite | Not supported: building the SQL throws `NotSupportedException`. |
| ClickHouse | Not supported: use `Settings(...)` instead; `Hint(...)` throws `NotSupportedException`. |

Multiple hints are joined inside one comment (space-separated), the form both `pg_hint_plan` and the
MySQL/MariaDB optimizer expect:

```csharp
// PostgreSQL:  select /*+ SeqScan(simple_entity) */ id
// from simple_entity
var pg = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("SeqScan(simple_entity)");

// MySQL / MariaDB:  select /*+ MAX_EXECUTION_TIME(1000) */ id
// from simple_entity
var my = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("MAX_EXECUTION_TIME(1000)");
```

A provider opts in through [`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) and [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase)); the
builder rejects a command that carries hints on a dialect that reports `false`.

## Join, subquery and tables-in-scope hints

`Hint(...)` applies to the whole statement. Use the following builder methods when a hint must target a
specific join, subquery, or table scope. Each dialect renders only the forms it supports and rejects the
others:

```csharp
var rows = dataContext.From<ISimpleEntity>(o => o.WithTableHint("nolock"))
    .Join<IComplexEntity>((s, c) => s.Id == c.Id, j => j.WithJoinHint("loop"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

* [`JoinOptions.WithJoinHint(string hint)`](xref:NextORM.Core.JoinOptions.WithJoinHint(System.String)) attaches a hint to a join (`j => j.WithJoinHint("loop")`) when the join is declared. SQL Server inserts it inside the join clause (`inner loop join`, `left hash join`). A hint on a `CROSS`/`APPLY` join is rejected. PostgreSQL, MySQL and MariaDB fold it into the statement-level `/*+ ... */` comment.
* [`JoinOptions.WithJoinTableHint(params string[] hints)`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])) attaches a locking table hint to the join it is declared on
  — the per-join counterpart of `WithTableHint`, and distinct from the optimizer `WithJoinHint` above.
  SQL Server renders a `WITH (hint, ...)` clause on that joined table; other dialects reject it with
  `NotSupportedException`. Only a physical-table join can carry it: an APPLY, derived-table,
  table-valued-function or XML/pivot join source is rejected with `InvalidOperationException`. See
  [Locking table hints](#locking-table-hints).
* [`FromOptions.WithSubQueryHint(string hint)`](xref:NextORM.Core.FromOptions.WithSubQueryHint(System.String)) attaches a hint to the derived-table source of a `From(subQuery, o => o.WithSubQueryHint(...))` builder. PostgreSQL/MySQL/MariaDB fold it into `/*+ ... */`. SQL Server rejects it because T-SQL cannot append a query hint to a subselect.
* `WithTablesInScopeHint(params string[] hints)` applies the hints to every physical table in the query's
  scope. SQL Server adds a `WITH (hint, ...)` clause to the primary and every joined table (the
  multi-table counterpart of `WithTableHint`); PostgreSQL/MySQL/MariaDB fold it into the statement
  comment.

```sql
-- SQL Server:
select t1.id
 from simple_entity as [t1] inner loop
 join complex_entity as [t2] on t1.id = t2.id
-- PostgreSQL (pg_hint_plan):
select /*+ HashJoin(t1 t2) */ id
 from simple_entity as "t1"
 join complex_entity as "t2" ...
-- MySQL 8:
select /*+ JOIN_ORDER(t1, t2) */ id
 from simple_entity as `t1`
 join complex_entity as `t2` ...
```

A blank hint is rejected with `ArgumentException` when the lambda runs. For dialects that use inline
comments, the hint text is rendered verbatim. Write the source aliases yourself (for example,
`HashJoin(t1 t2)`): nextorm assigns aliases while rendering and does not expose them. All four hints are
part of the plan key. SQLite, ClickHouse, and the in-memory provider reject them with
`NotSupportedException`.

### Tables-in-scope hints on joined DELETE/UPDATE

`WithTablesInScopeHint` is honoured by the multi-table `DELETE` and `UPDATE` builders, not only by
`SELECT`: the hints reach the target and every joined physical source.

```csharp
dataContext.From<IComplexEntity>()
    .WithTablesInScopeHint("SeqScan(t1)")        // SQL Server: .WithTablesInScopeHint("nolock")
    .Join<ISimpleEntity>((c, s) => c.Id == s.Id)
    .CreateUpdateJoinBuilder()
    .Set(p => p.Item1.String, "x")
    .ToSql();
```

```sql
-- SQL Server: a WITH (...) clause on each physical table
update [t1] set t1.somestring = @p0 from complex_entity with (nolock) as [t1] join simple_entity with (nolock) as [t2] on t1.id = t2.id
-- PostgreSQL: one statement-level comment immediately after UPDATE (the same for DELETE)
update /*+ SeqScan(t1) */ complex_entity as "t1" set somestring = @p0 from simple_entity as "t2" where t1.id = t2.id
```

On PostgreSQL/MySQL/MariaDB the scope hint — together with any join or subquery hints collected for the
statement — is emitted once as a `/*+ ... */` comment immediately after the `DELETE`/`UPDATE` keyword.
For a CTE-backed DML statement the hoisted `WITH` prefix is placed before the statement while the
comment stays after the DML verb, matching how the `SELECT` path places it after `SELECT` in a
`WITH … SELECT`. SQLite and ClickHouse reject a nonempty scope hint with `NotSupportedException`,
consistently with `SELECT`; a joined `DELETE`/`UPDATE` is itself unsupported on those providers, so their
existing capability error is unchanged. Hint-free DML stays byte-identical.

## ClickHouse query modifiers

ClickHouse has four query-level modifiers that are not hints. Use `Final()`, `PreWhere(predicate)`, or
`Settings(("key", "value"), ...)` as builder methods. Set `Sample(ratio[, offset])` as a per-query
source option in `From`.

These modifiers are ClickHouse-only extension methods from the `nextorm.clickhouse` package. Add
`using NextORM.ClickHouse;` to a file that uses them. Other providers and the in-memory context throw
`NotSupportedException`.

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.Sample(0.1, 0.5))
    .Final()
    .PreWhere(c => c.Int > 0)
    .Where(c => c.Boolean == true)
    .Select(c => new { c.Id, c.Int })
    .Settings(("max_threads", "2"))
    .ToList();
```

```sql
select id, nullableint
 from complex_entity final sample 0.1 offset 0.5
prewhere (nullableint > 0)
where (b = true)
settings max_threads = 2
```

`Settings` is the trailer of the clause order: it is emitted after `LIMIT ... BY` and after
`LIMIT`/`OFFSET`, and ClickHouse rejects a `LIMIT` placed after `SETTINGS` (server Code 62). A scalar
terminal adds paging — `First`/`FirstOrDefault` render `limit 1`, `Single`/`SingleOrDefault` render
`limit 2` — so the settings clause follows it:

```csharp
var count = dataContext.From<IComplexEntity>()
    .Settings(("max_threads", "2"))
    .Select(c => SqlFunctions.Sql.count())
    .First();
```

```sql
select toInt32(count(*))
 from complex_entity
limit 1
 settings max_threads = 2
```

`Final()` forces the merge of a ReplacingMergeTree/CollapsingMergeTree before the read; `Sample` reads a
fraction `[0, 1]` of the rows; `PreWhere` filters before the regular `WHERE` so the read can skip other
columns; `Settings` appends a trailing clause whose values are rendered verbatim (only pass trusted
literals). `FINAL` and `PREWHERE` require a table engine that supports them — the `Memory` engine rejects
both.

## Limitations

* Locking table hints on the query's `FROM` table are rendered by [`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])); use [`JoinOptions.WithJoinTableHint`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])) for a chosen join, or `WithTablesInScopeHint` to cover every physical table.
* A set operation does not guard against concatenating a hinted command. The hint stays on the branch
  where you attached it, so avoid combining hinted commands with [`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})), [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})), or similar operations.

## See also

- [Joins](04-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})).
- [CTE](10-cte.md) - `maxRecursion` and the SQL Server `option (maxrecursion n)` clause.
- [Queries and projections](../engine/index.md)

---

Source: `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[]))),
`src/nextorm.core/DataContext/Sql/FromOptions.cs` (`WithTableHint`/`WithIndex`/`WithoutIndex`),
`src/nextorm.core/Builders/JoinOptions.cs` (`WithJoinTableHint`/`WithJoinHint`), `src/nextorm.core/DataContext/Sql/FromOptions.cs` (`WithSubQueryHint`), `src/nextorm.core/Builders/Entity/EntityBuilder.cs` (`WithTablesInScopeHint`),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase))),
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`,
`src/nextorm.mysql/MySqlDialect.cs` (MariaDB inherits).
