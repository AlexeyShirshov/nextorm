# Query hints

> Attach statement-level hints to a query with `Hint(...)`, for example SQL Server `OPTION (RECOMPILE)`.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [CTE](09-cte.md) · [Provider overview](../providers/overview.md)

## Overview

[`Hint`](xref:NextORM.Core.QueryCommand`1) returns a new command carrying one or more
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

## Table hints

`EntityBuilder<T>.WithTableHint(params string[] hints)` attaches table-level hints to the primary
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
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) and [`MakeTableHints`](xref:NextORM.Core.ISqlDialect) (SQL Server); other dialects reject a
command that carries table hints with `NotSupportedException`. Only the primary table is covered; hints
on joined tables are not part of the API yet.

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

A provider opts in through [`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) and [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect); the
builder rejects a command that carries hints on a dialect that reports `false`.

## ClickHouse query modifiers

ClickHouse exposes four query-level modifiers that are not hints but dedicated builder methods:
`Final()`, `Sample(ratio[, offset])`, `PreWhere(predicate)` and `Settings(("key", "value"), ...)`. They
are only valid on ClickHouse; every other provider and the in-memory context throw
`NotSupportedException`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Final()
    .Sample(0.1, 0.5)
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

* Table hints are only rendered for the primary table; hints on a joined table are not part of the API
  yet ([`WithTableHint`](xref:NextORM.Core.EntityBuilder`1) applies to the query's `FROM` table).
* Concatenating a hinted command with a set operation ([`Union`](xref:NextORM.Core.QueryCommand`1), [`Intersect`](xref:NextORM.Core.QueryCommand`1), ...) is not guarded against;
  the hint travels to the branch it was attached to and should be avoided there.

## See also

- [Joins](03-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1).
- [CTE](09-cte.md) - `maxRecursion` and the SQL Server `option (maxrecursion n)` clause.
- [Queries and projections](01-querying-and-projections.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1)),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect)),
`src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`,
`src/nextorm.mysql/MySqlDialect.cs` (MariaDB inherits).
