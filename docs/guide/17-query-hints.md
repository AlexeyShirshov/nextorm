# Query hints

> Attach statement-level hints to a query with `Hint(...)`, for example SQL Server `OPTION (RECOMPILE)`.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [CTE](09-cte.md) · [Provider overview](../providers/overview.md)

## Overview

[`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) returns a new command carrying one or more
statement-level hints. Hints are provider specific: the command stores plain strings and the active
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect) decides where and how they are rendered. A repeated call accumulates:

```csharp
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

`EntityBuilder<T>.WithTableHint(params string[] hints)` attaches SQL Server locking hints (`nolock`/`updlock`/`holdlock`) to the primary
physical table. SQL Server renders them as a `WITH (...)` clause between the table name and its alias:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTableHint("nolock")
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id from complex_entity with (nolock)
```

The hints are rendered verbatim, so only pass trusted values. A provider opts in through
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) and [`MakeTableHints`](xref:NextORM.Core.ISqlDialect.MakeTableHints(System.Collections.Generic.IReadOnlyList{System.String},NextORM.Core.KeywordCase)) (SQL Server); other dialects reject a
command that carries table hints with `NotSupportedException`. Only the primary table is covered; hints
on joined tables are not part of the API yet.

## Index hints

`EntityBuilder<T>.WithIndex(params string[] indexes)` asks the planner to consider a named index on the
primary physical table. The overload `WithIndex(IndexHintKind kind, params string[] indexes)` selects the
intent ([`IndexHintKind`](xref:NextORM.Core.IndexHintKind).`Use`/`Force`/`Ignore`), and `WithoutIndex()`
suppresses index use. Each dialect renders its native form after the table name and before its alias:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithIndex("ix_complex_id")
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

## ClickHouse query modifiers

ClickHouse exposes four query-level modifiers that are not hints: `Final()`, `PreWhere(predicate)` and
`Settings(("key", "value"), ...)` are dedicated builder methods, while the `Sample(ratio[, offset])`
modifier is a per-query source option set in `From`. They are only valid on ClickHouse; every other
provider and the in-memory context throw `NotSupportedException`.

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

* Locking table hints are only rendered for the primary table; hints on a joined table are not part of the API
  yet ([`WithTableHint`](xref:NextORM.Core.EntityBuilder`1.WithTableHint(System.String[])) applies to the query's `FROM` table).
* Concatenating a hinted command with a set operation ([`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})), [`Intersect`](xref:NextORM.Core.QueryCommand`1.Intersect``1(NextORM.Core.QueryCommand{``0})), ...) is not guarded against;
  the hint travels to the branch it was attached to and should be avoided there.

## See also

- [Joins](03-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0})).
- [CTE](09-cte.md) - `maxRecursion` and the SQL Server `option (maxrecursion n)` clause.
- [Queries and projections](01-querying-and-projections.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[]))),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect.RenderQueryHints(System.String,System.Collections.Generic.IReadOnlyList{System.String},System.String,NextORM.Core.KeywordCase))),
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`,
`src/nextorm.mysql/MySqlDialect.cs` (MariaDB inherits).
