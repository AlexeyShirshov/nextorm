# Window functions

> Compute ranking, neighbour values and running/windowed aggregates over a partition with the SQL
> `OVER` clause.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Grouping and aggregates](04-grouping-and-aggregates.md) · [Sorting and paging](05-sorting-and-paging.md)

## Overview

Window functions live on [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql). Each call returns a
[`WindowFunction<T>`](xref:NextORM.Core.WindowFunction`1) marker that must be completed with `.Over(...)`;
inside the query expression it reads as the value type of the function (`int` for
`row_number`/`rank`/`dense_rank`/`ntile`, `double` for `percent_rank`/`cume_dist`, `T?` for the
value/aggregate functions). The marker types and their methods are only ever interpreted by the
expression visitor - they are never executed.

```csharp
public static class SqlFunctions
{
    public static CommonFunctions SQL { get; }
}

public sealed class WindowFunction<T>
{
    // partition-only / partition+order+frame; every argument is optional
    public T Over(Expression<Func<object?>>? partitionBy = null,
                  Expression<Func<object?>>? orderBy = null,
                  WindowFrame? frame = null);

    // order-only or a single ordered key
    public T Over(WindowOrder orderBy, WindowFrame? frame = null);

    // several ordered keys
    public T Over(WindowOrder[] orderBy, WindowFrame? frame = null);

    // several partitions plus ordered keys (mixed asc/desc)
    public T Over(Expression<Func<object?>>[]? partitionBy,
                  WindowOrder[]? orderBy = null,
                  WindowFrame? frame = null);
}
```

[`Over`](xref:NextORM.Core.WindowFunction`1) with no arguments renders an empty specification (`over ()`). Because C# expression trees
reject named arguments that skip a preceding defaulted parameter, an **order-only** specification must
use the [`WindowOrder`](xref:NextORM.Core.WindowOrder) overload - `Over(SqlFunctions.Sql.asc(() => e.Id))` - rather than `Over(orderBy: ...)`.
`SqlFunctions.Sql.asc(expression)` and `SqlFunctions.Sql.desc(expression)` return a [`WindowOrder`](xref:NextORM.Core.WindowOrder) (an order key plus an
[`OrderDirection`](xref:NextORM.Core.OrderDirection)).

Calling a window function **without** [`Over`](xref:NextORM.Core.WindowFunction`1) is an error: the visitor throws `NotSupportedException`
whose message mentions [`Over`](xref:NextORM.Core.WindowFunction`1).

## Functions

| Function | [`Sql`](xref:NextORM.Core.SqlFunctions.Sql) call | Emitted SQL |
|---|---|---|
| Row number | `row_number()` | `row_number()` |
| Rank (with gaps) | `rank()` | `rank()` |
| Dense rank | `dense_rank()` | `dense_rank()` |
| Relative rank | `percent_rank()` | `percent_rank()` |
| Cumulative distribution | `cume_dist()` | `cume_dist()` |
| Interpolated percentile | `percentile_cont(fraction, property)` | `percentile_cont(f) within group (order by expr) over (...)` |
| Discrete percentile | `percentile_disc(fraction, property)` | `percentile_disc(f) within group (order by expr) over (...)` |
| Buckets | `ntile(buckets)` | `ntile(n)` |
| Previous value | `lag(property[, offset[, defaultValue]])` | `lag(expr, offset[, default])` |
| Next value | `lead(property[, offset[, defaultValue]])` | `lead(expr, offset[, default])` |
| First in frame | `first_value(property)` | `first_value(expr)` |
| Last in frame | `last_value(property)` | `last_value(expr)` |
| N-th in frame | `nth_value(property, n)` | `nth_value(expr, n)` |
| Windowed sum | `sum_over(property)` | `sum(expr)` |
| Windowed average | `avg_over(property)` | `avg(expr)` |
| Windowed minimum | `min_over(property)` | `min(expr)` |
| Windowed maximum | `max_over(property)` | `max(expr)` |
| Windowed count | `count_over()` / `count_over(property)` | `count(*)` / `count(expr)` |

The aggregate variants carry an `_over` suffix so they do not clash with the scalar aggregates `sum`,
`avg`, `min`, `max` and `count` that are used with [`GroupBy`](xref:NextORM.Core.EntityBuilder`1).

## Row number over a partition

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => e.Int, orderBy: () => e.Id)
    })
    .OrderBy(1, OrderDirection.Asc)
    .ToList();
```

```sql
select id, row_number() over (partition by nullableint order by id) as 'rn' from complex_entity
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Id | rn |
|----|----|
| 1 | 1 |
| 2 | 1 |
| 3 | 2 |

The seeded `complex_entity` has a singleton `nullableint` partition (`id` 1) and a two-row partition
(`id` 2, 3), so `rn` is 1, 1, 2.

## Rank, dense rank, and `asc`/`desc`

`rank()` leaves a gap after a tie, `dense_rank()` does not. Both use the [`WindowOrder`](xref:NextORM.Core.WindowOrder) overload here:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        r = SqlFunctions.Sql.rank().Over(SqlFunctions.Sql.asc(() => e.Id)),
        dr = SqlFunctions.Sql.dense_rank().Over(SqlFunctions.Sql.asc(() => e.Id))
    })
    .ToList();
```

```sql
select id, rank() over (order by id) as 'r', dense_rank() over (order by id) as 'dr' from complex_entity
```

A descending key is written with `SqlFunctions.Sql.desc`. When several keys (or a mix of directions) are needed,
use the `WindowOrder[]` overload; partitions are supplied through the array overload:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        r = SqlFunctions.Sql.row_number().Over(
            partitionBy: new Expression<Func<object?>>[] { () => e.Int },
            orderBy: new[] { SqlFunctions.Sql.desc(() => e.Id) })
    })
    .ToList();
```

```sql
select id, row_number() over (partition by nullableint order by id desc) as 'r' from complex_entity
```

## `lag` and `lead` with offset and default

`lag`/`lead` accept an optional offset and default. The default only fills a **missing row** at the
boundary; a `null` value in an existing row is returned unchanged:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        prev = SqlFunctions.Sql.lag(e.Id, 1, 0L).Over(SqlFunctions.Sql.asc(() => e.Id)),
        next = SqlFunctions.Sql.lead(e.Int, 2, 0).Over(SqlFunctions.Sql.asc(() => e.Id))
    })
    .ToList();
```

```sql
select id, lag(id, 1, 0) over (order by id) as 'prev', lead(nullableint, 2, 0) over (order by id) as 'next' from complex_entity
```

## Windowed aggregates

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        total = SqlFunctions.Sql.sum_over(e.Id).Over(partitionBy: () => e.Int),
        n = SqlFunctions.Sql.count_over().Over(partitionBy: () => e.Int)
    })
    .ToList();
```

```sql
select id, sum(id) over (partition by nullableint) as 'total', count(*) over (partition by nullableint) as 'n' from complex_entity
```

## Frames

A frame restricts the rows an aggregate sees. [`WindowFrame`](xref:NextORM.Core.WindowFrame) has factory methods for both SQL frame
units and [`WindowFrameBound`](xref:NextORM.Core.WindowFrameBound) has the boundaries:

| Factory | Renders |
|---|---|
| [`Rows`](xref:NextORM.Core.WindowFrame) | `rows between <start> and <end>` |
| [`Range`](xref:NextORM.Core.WindowFrame) | `range between <start> and <end>` |
| [`Rows`](xref:NextORM.Core.WindowFrame) | `rows between <preceding> preceding and <following> following` |
| [`RowsUnboundedPrecedingToCurrentRow`](xref:NextORM.Core.WindowFrame.RowsUnboundedPrecedingToCurrentRow) | `rows between unbounded preceding and current row` |
| [`RangeUnboundedPrecedingToCurrentRow`](xref:NextORM.Core.WindowFrame.RangeUnboundedPrecedingToCurrentRow) | `range between unbounded preceding and current row` |

| Boundary | Renders |
|---|---|
| [`UnboundedPreceding`](xref:NextORM.Core.WindowFrameBound.UnboundedPreceding) | `unbounded preceding` |
| [`Preceding`](xref:NextORM.Core.WindowFrameBound) | `<n> preceding` |
| [`CurrentRow`](xref:NextORM.Core.WindowFrameBound.CurrentRow) | `current row` |
| [`Following`](xref:NextORM.Core.WindowFrameBound) | `<n> following` |
| [`UnboundedFollowing`](xref:NextORM.Core.WindowFrameBound.UnboundedFollowing) | `unbounded following` |

A framed running aggregate and a sliding window:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        running = SqlFunctions.Sql.sum_over(e.Id).Over(
            SqlFunctions.Sql.asc(() => e.Id),
            WindowFrame.RowsUnboundedPrecedingToCurrentRow),
        sliding = SqlFunctions.Sql.sum_over(e.Id).Over(
            SqlFunctions.Sql.asc(() => e.Id),
            WindowFrame.Rows(1, 1))
    })
    .ToList();
```

```sql
select id, sum(id) over (order by id rows between unbounded preceding and current row) as 'running', sum(id) over (order by id rows between 1 preceding and 1 following) as 'sliding' from complex_entity
```

## Provider differences

Window functions are ANSI and every SQL provider nextorm targets supports both frame units, so the
rendered SQL is the same apart from identifier quoting (single quotes, brackets or double quotes for the
alias; see [Provider overview](../providers/overview.md)). The `percent_rank()`/`cume_dist()` pair is
supported by every provider ([`SupportsPercentRankCumeDist`](xref:NextORM.Core.ISqlDialect.SupportsPercentRankCumeDist)).
The only non-portable value function is `nth_value`: SQL Server has no `NTH_VALUE`, so
[`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue) makes it reject the call with
`NotSupportedException`, while PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse render `nth_value(expr, n)`.

The window percentiles are the other exception: `percentile_cont`/`percentile_disc` are not portable as
window functions. SQL Server and MariaDB render them as
`percentile_cont(f) within group (order by x) over (...)` under
[`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow); PostgreSQL expresses
percentiles as an ordered-set **aggregate** instead
([`SqlFunctions.Postgres.percentile_cont`](xref:NextORM.Core.PostgresFunctions.percentile_cont)), and
MySQL, SQLite and ClickHouse reject the window form with `NotSupportedException`.

| Provider | Behaviour |
|---|---|
| SQLite | Full `OVER` support; column aliases single-quoted (`as 'rn'`). |
| SQL Server | Full `OVER` support; no `nth_value`; aliases bracket-quoted (`as [rn]`). |
| PostgreSQL | Full `OVER` support; aliases double-quoted (`as "rn"`). |
| MySQL | Full `OVER` support; column aliases backtick-quoted (`` as `rn` ``). |
| MariaDB | Full `OVER` support; aliases backtick-quoted. |
| ClickHouse | `OVER` support; aliases backtick-quoted. |
| In-memory | Not applicable: window functions are rendered by the SQL dialects and are not part of the in-memory provider. |

## See also

* [Grouping and aggregates](04-grouping-and-aggregates.md) - the scalar aggregates that `_over` variants shadow.
* [Sorting and paging](05-sorting-and-paging.md) - ordering the outer query that selects window columns.
* [Provider overview](../providers/overview.md) - alias quoting and provider capability flags.

---

Source: `src/nextorm.core/Query/WindowFunctions.cs:16` ([`WindowFunction<T>`](xref:NextORM.Core.WindowFunction`1)), `:56` ([`WindowOrder`](xref:NextORM.Core.WindowOrder)), `:72`/`:79` (frame enums), `:89` ([`WindowFrameBound`](xref:NextORM.Core.WindowFrameBound)), `:118` ([`WindowFrame`](xref:NextORM.Core.WindowFrame)); `src/nextorm.core/Query/SqlFunctions.cs:308` (`asc`/`desc`), `:314` (functions);
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:621`;
`tests/nextorm.integration.tests/CommonTestSuite.Window.cs:14`, `:33`, `:54`, `:72`, `:99`;
`tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:28`, `:64`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1266`, `:1281`, `:1297`, `:1314`, `:1330`, `:1346`, `:1366`, `:1383`, `:1401`, `:1422`.
