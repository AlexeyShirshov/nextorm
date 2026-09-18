# Query hints

> Attach statement-level hints to a query with `Hint(...)`, for example SQL Server `OPTION (RECOMPILE)`.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [CTE](09-cte.md) · [Provider overview](../providers/overview.md)

## Overview

`QueryCommand<TResult>.Hint(params string[] hints)` returns a new command carrying one or more
statement-level hints. Hints are provider specific: the command stores plain strings and the active
`ISqlDialect` decides where and how they are rendered. A repeated call accumulates:

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
`ISqlDialect.SupportsTableHints` and `ISqlDialect.MakeTableHints` (SQL Server); other dialects reject a
command that carries table hints with `NotSupportedException`. Only the primary table is covered; hints
on joined tables are not part of the API yet.

## Providers

| Provider | Query hints |
|---|---|
| SQL Server | Supported: rendered as a trailing `OPTION (hint, ...)` clause. |
| SQLite | Not supported: building the SQL throws `NotSupportedException`. |
| PostgreSQL | Not supported: building the SQL throws `NotSupportedException`. |
| MySQL / MariaDB | Not supported: building the SQL throws `NotSupportedException`. |
| ClickHouse | Not supported: building the SQL throws `NotSupportedException`. |

A provider opts in through `ISqlDialect.SupportsQueryHints` and `ISqlDialect.RenderQueryHints`; the
builder rejects a command that carries hints on a dialect that reports `false`.

## Limitations

* Table hints are only rendered for the primary table; hints on a joined table are not part of the API
  yet (`WithTableHint` applies to the query's `FROM` table).
* Concatenating a hinted command with a set operation (`Union`, `Intersect`, ...) is not guarded against;
  the hint travels to the branch it was attached to and should be avoided there.

## See also

- [Joins](03-joins.md) - `CrossApply`/`OuterApply`.
- [CTE](09-cte.md) - `maxRecursion` and the SQL Server `option (maxrecursion n)` clause.
- [Queries and projections](01-querying-and-projections.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs` (`Hint`),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` (`SupportsQueryHints` / `RenderQueryHints`),
`src/nextorm.sqlserver/SqlServerDialect.cs`.
