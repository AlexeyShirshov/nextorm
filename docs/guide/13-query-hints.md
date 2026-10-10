# Query hints

> Attach statement-level hints to a query with `Hint(...)`, for example SQL Server `OPTION (RECOMPILE)`.

**Prerequisites:** [Querying and projections](../querying/index.md) · [CTE](08-cte.md) · [Provider overview](../providers/overview.md)

## Overview

[`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) returns a new command carrying one or more
statement-level hints. Hints are provider specific: the command stores plain strings and the active
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect) decides where and how they are rendered. The method takes
`params string[]`, so one or more hints can be passed in a single call or accumulated across repeated
calls; the two forms are equivalent:

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
select id, somestring from complex_entity where (id > 1) option (recompile, fast 10)
```

Blank hints are ignored. The hint list is part of the query plan key, so a hinted command never reuses
the cached plan of an otherwise identical unhinted command (and vice versa), and two commands with
different hints do not share a plan.

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
select /* reports.orders */ id from complex_entity where (id > 1)
```

Use it to identify a statement in a profiler, the server log or a server-side query store
(`pg_stat_activity`, SQL Server Query Store, ClickHouse `system.query_log`). Line breaks and the
comment delimiters `*/` and `/*` are neutralised — SQL Server nests block comments, so `/*` is escaped
too — and the comment always opens with a space, so a tag starting with `!` or `+` cannot become a
MySQL/MariaDB executable comment or optimizer hint. The tag is part of the plan key, so two commands
that differ only in their tag never share a cached plan; passing `null` or an empty string clears it.
The in-memory provider accepts the call and ignores it (no SQL is generated). When a query also carries
`Hint(...)`, the optimizer-hint comment is placed first (`select /*+ hint */ /* tag */ ...`) so
`pg_hint_plan` and the MySQL/MariaDB optimizer still recognise it.

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

`FromOptions.WithTableHint(params string[] hints)` attaches SQL Server locking hints (`nolock`/`updlock`/`holdlock`) to the primary
physical table. SQL Server renders them as a `WITH (...)` clause between the table name and its alias:

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.WithTableHint("nolock"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id from complex_entity with (nolock)
```

[`JoinOptions.WithJoinTableHint(params string[] hints)`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])) attaches the same kind of table hint to a
join when it is declared, passed through that join's trailing lambda, like `WithJoinHint`. It is
not the same as [`JoinOptions.WithJoinHint`](xref:NextORM.Core.JoinOptions.WithJoinHint(System.String)) (an
optimizer *join* hint such as `loop`/`hash`/`merge`, rendered inside the join clause) and not the same as
`WithTablesInScopeHint` (which covers every physical table). SQL Server renders it as a `WITH (...)`
clause on that joined table only:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Join(dataContext.From<ISimpleEntity>(), (a, b) => a.Id == b.Id, j => j.WithJoinTableHint("nolock"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id from complex_entity as [t1] inner join simple_entity with (nolock) as [t2] on t1.id = t2.id
```

Combining it with `WithTablesInScopeHint` still yields a single `WITH (...)` clause on that table: the
join-local hint is listed first, then the scope hints (which still cover every other physical table):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTablesInScopeHint("holdlock")
    .Join(dataContext.From<ISimpleEntity>(), (a, b) => a.Id == b.Id, j => j.WithJoinTableHint("nolock"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

```sql
select t1.id from complex_entity with (holdlock) as [t1] inner join simple_entity with (nolock, holdlock) as [t2] on t1.id = t2.id
```

The hints are rendered verbatim, so only pass trusted values. A provider opts in through
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) and [`MakeTableHints`](xref:NextORM.Core.ISqlDialect.MakeTableHints(System.Collections.Generic.IReadOnlyList{System.String},NextORM.Core.KeywordCase)) (SQL Server); other dialects reject a
command that carries table hints with `NotSupportedException`. Pass `j => j.WithJoinTableHint(...)` to target a chosen
join; `WithTableHint` still covers only the primary table. A per-join table hint can only be rendered on a
**physical-table** join: an APPLY join (or a derived-table, table-valued-function, XML/pivot or raw-SQL join
source) has no table name to attach a `WITH (...)` clause to and is rejected with
`InvalidOperationException`. Passing only blank/null hints leaves any previously attached hints unchanged.
`WithTableHint` and `WithJoinTableHint` both require a dialect with table hints, so SQLite, ClickHouse and
the in-memory provider reject them with `NotSupportedException`.

## Index hints

`FromOptions.WithIndex(params string[] indexes)` asks the planner to consider a named index on the
primary physical table. The overload `WithIndex(IndexHintKind kind, params string[] indexes)` selects the
intent ([`IndexHintKind`](xref:NextORM.Core.IndexHintKind).`Use`/`Force`/`Ignore`), and `WithoutIndex()`
suppresses index use. Each dialect renders its native form after the table name and before its alias:

```csharp
var rows = dataContext.From<IComplexEntity>(o => o.WithIndex("ix_complex_id"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
-- MySQL/MariaDB  (also `force index (...)` / `ignore index (...)`):
select id from complex_entity use index (ix_complex_id)
-- SQLite  (exactly one index; `WithoutIndex()` renders `not indexed`):
select id from complex_entity indexed by ix_complex_id
-- SQL Server  (merged into the single table-hint clause):
select id from complex_entity with (index(ix_complex_id))
```

A provider opts in through [`ISqlDialect.IndexHints`](xref:NextORM.Core.ISqlDialect.IndexHints) and
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
// PostgreSQL:  select /*+ SeqScan(simple_entity) */ id from simple_entity
var pg = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("SeqScan(simple_entity)");

// MySQL / MariaDB:  select /*+ MAX_EXECUTION_TIME(1000) */ id from simple_entity
var my = dataContext.From<ISimpleEntity>()
    .Select(x => new { x.Id })
    .Hint("MAX_EXECUTION_TIME(1000)");
```

A provider opts in through [`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) and [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase)); the
builder rejects a command that carries hints on a dialect that reports `false`.

## Join, subquery and tables-in-scope hints

`Hint(...)` is statement-level. Four builder methods attach a hint to a narrower part of the query;
each dialect renders the form it has, or rejects the command:

```csharp
var rows = dataContext.From<ISimpleEntity>(o => o.WithTableHint("nolock"))
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id, j => j.WithJoinHint("loop"))
    .Select(p => new { p.Item1.Id })
    .ToList();
```

* [`JoinOptions.WithJoinHint(string hint)`](xref:NextORM.Core.JoinOptions.WithJoinHint(System.String)) attaches a hint to a join (`j => j.WithJoinHint("loop")`) when the join is declared. SQL Server inserts it inside the join clause
  (`inner loop join`, `left hash join`); a hint on a `CROSS`/`APPLY` join is rejected. PostgreSQL, MySQL
  and MariaDB fold it into the statement-level `/*+ ... */` comment.
* [`JoinOptions.WithJoinTableHint(params string[] hints)`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])) attaches a locking table hint to the join it is declared on
  — the per-join counterpart of `WithTableHint`, and distinct from the optimizer `WithJoinHint` above.
  SQL Server renders a `WITH (hint, ...)` clause on that joined table; other dialects reject it with
  `NotSupportedException`. Only a physical-table join can carry it: an APPLY, derived-table,
  table-valued-function or XML/pivot join source is rejected with `InvalidOperationException`. See
  [Locking table hints](#locking-table-hints).
* [`FromOptions.WithSubQueryHint(string hint)`](xref:NextORM.Core.FromOptions.WithSubQueryHint(System.String)) attaches a hint to the derived-table source of a
  `From(subQuery, o => o.WithSubQueryHint(...))` builder. PostgreSQL/MySQL/MariaDB fold it into `/*+ ... */`; SQL Server rejects it, because T-SQL
  cannot append a query hint to a subselect.
* `WithTablesInScopeHint(params string[] hints)` applies the hints to every physical table in the query's
  scope. SQL Server adds a `WITH (hint, ...)` clause to the primary and every joined table (the
  multi-table counterpart of `WithTableHint`); PostgreSQL/MySQL/MariaDB fold it into the statement
  comment.

```sql
-- SQL Server:
select t1.id from simple_entity as [t1] inner loop join complex_entity as [t2] on t1.id = t2.id
-- PostgreSQL (pg_hint_plan):
select /*+ HashJoin(t1 t2) */ id from simple_entity as "t1" join complex_entity as "t2" ...
-- MySQL 8:
select /*+ JOIN_ORDER(t1, t2) */ id from simple_entity as `t1` join complex_entity as `t2` ...
```

A blank hint is rejected with `ArgumentException` when the lambda runs. For the inline-comment dialects the hint text is rendered verbatim, so write
the source aliases yourself (`HashJoin(t1 t2)`): nextorm's aliases are assigned at render time and are not
exposed. All four hints are part of the plan key. On SQLite, ClickHouse and the in-memory provider they
are rejected with `NotSupportedException`.

### Tables-in-scope hints on joined DELETE/UPDATE

`WithTablesInScopeHint` is honoured by the multi-table `DELETE` and `UPDATE` builders, not only by
`SELECT`: the hints reach the target and every joined physical source.

```csharp
dataContext.From<IComplexEntity>()
    .WithTablesInScopeHint("SeqScan(t1)")        // SQL Server: .WithTablesInScopeHint("nolock")
    .Join(dataContext.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
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

ClickHouse exposes four query-level modifiers that are not hints: `Final()`, `PreWhere(predicate)` and
`Settings(("key", "value"), ...)` are dedicated builder methods, while the `Sample(ratio[, offset])`
modifier is a per-query source option set in `From`. They are ClickHouse-only and ship as extension
methods in the `nextorm.clickhouse` package, so a file that uses them needs
`using NextORM.ClickHouse;`. Every other provider and the in-memory context throw `NotSupportedException`.

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
select id, nullableint from complex_entity final sample 0.1 offset 0.5
prewhere (nullableint > 0)
where (b = true)
settings max_threads = 2
```

`Final()` forces the merge of a ReplacingMergeTree/CollapsingMergeTree before the read; `Sample` reads a
fraction `[0, 1]` of the rows; `PreWhere` filters before the regular `WHERE` so the read can skip other
columns; `Settings` appends a trailing clause whose values are rendered verbatim (only pass trusted
literals). `FINAL` and `PREWHERE` require a table engine that supports them — the `Memory` engine rejects
both.

## Limitations

* Locking table hints on the query's `FROM` table are rendered by [`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])); use [`JoinOptions.WithJoinTableHint`](xref:NextORM.Core.JoinOptions.WithJoinTableHint(System.String[])) for a chosen join, or `WithTablesInScopeHint` to cover every physical table.
* Concatenating a hinted command with a set operation ([`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})), [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})), ...) is not guarded against;
  the hint travels to the branch it was attached to and should be avoided there.

## See also

- [Joins](02-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})).
- [CTE](08-cte.md) - `maxRecursion` and the SQL Server `option (maxrecursion n)` clause.
- [Queries and projections](../querying/index.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[]))),
`src/nextorm.core/DataContext/FromOptions.cs` (`WithTableHint`/`WithIndex`/`WithoutIndex`),
`src/nextorm.core/Builders/JoinOptions.cs` (`WithJoinTableHint`/`WithJoinHint`), `src/nextorm.core/DataContext/FromOptions.cs` (`WithSubQueryHint`), `src/nextorm.core/Builders/EntityBuilder.cs` (`WithTablesInScopeHint`),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase))),
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`,
`src/nextorm.mysql/MySqlDialect.cs` (MariaDB inherits).
