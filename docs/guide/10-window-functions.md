# Window functions

> Compute ranking, neighbour values and running/windowed aggregates over a partition with the SQL
> `OVER` clause.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Grouping and aggregates](04-grouping-and-aggregates.md) · [Sorting and paging](05-sorting-and-paging.md)

## Overview

Window functions live on `NORM.SQL`. Each call returns a `NORM.WindowFunction<T>` marker that must be
completed with `.Over(...)`; inside the query expression it reads as the value type of the function
(`int` for the ranking functions, `T?` for the value/aggregate functions). The marker types and their
methods are only ever interpreted by the expression visitor - they are never executed.

```csharp
public static class NORM
{
    public static NORM_SQL SQL { get; }
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

`Over()` with no arguments renders an empty specification (`over ()`). Because C# expression trees
reject named arguments that skip a preceding defaulted parameter, an **order-only** specification must
use the `WindowOrder` overload - `Over(NORM.SQL.asc(() => e.Id))` - rather than `Over(orderBy: ...)`.
`NORM.SQL.asc(expression)` and `NORM.SQL.desc(expression)` return a `WindowOrder` (an order key plus an
`OrderDirection`).

Calling a window function **without** `Over` is an error: the visitor throws `NotSupportedException`
whose message mentions `Over`.

## Functions

| Function | `NORM.SQL` call | Emitted SQL |
|---|---|---|
| Row number | `row_number()` | `row_number()` |
| Rank (with gaps) | `rank()` | `rank()` |
| Dense rank | `dense_rank()` | `dense_rank()` |
| Buckets | `ntile(buckets)` | `ntile(n)` |
| Previous value | `lag(property[, offset[, defaultValue]])` | `lag(expr, offset[, default])` |
| Next value | `lead(property[, offset[, defaultValue]])` | `lead(expr, offset[, default])` |
| First in frame | `first_value(property)` | `first_value(expr)` |
| Last in frame | `last_value(property)` | `last_value(expr)` |
| Windowed sum | `sum_over(property)` | `sum(expr)` |
| Windowed average | `avg_over(property)` | `avg(expr)` |
| Windowed minimum | `min_over(property)` | `min(expr)` |
| Windowed maximum | `max_over(property)` | `max(expr)` |
| Windowed count | `count_over()` / `count_over(property)` | `count(*)` / `count(expr)` |

The aggregate variants carry an `_over` suffix so they do not clash with the scalar aggregates `sum`,
`avg`, `min`, `max` and `count` that are used with `GroupBy`.

## Row number over a partition

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        rn = NORM.SQL.row_number().Over(partitionBy: () => e.Int, orderBy: () => e.Id)
    })
    .OrderBy(1, OrderDirection.Asc)
    .ToList();
```

```sql
select id, row_number() over (partition by nullableint order by id) as 'rn' from complex_entity
```

The seeded `complex_entity` has a singleton `nullableint` partition (`id` 1) and a two-row partition
(`id` 2, 3), so `rn` is 1, 1, 2.

## Rank, dense rank, and `asc`/`desc`

`rank()` leaves a gap after a tie, `dense_rank()` does not. Both use the `WindowOrder` overload here:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        r = NORM.SQL.rank().Over(NORM.SQL.asc(() => e.Id)),
        dr = NORM.SQL.dense_rank().Over(NORM.SQL.asc(() => e.Id))
    })
    .ToList();
```

```sql
select id, rank() over (order by id) as 'r', dense_rank() over (order by id) as 'dr' from complex_entity
```

A descending key is written with `NORM.SQL.desc`. When several keys (or a mix of directions) are needed,
use the `WindowOrder[]` overload; partitions are supplied through the array overload:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        r = NORM.SQL.row_number().Over(
            partitionBy: new Expression<Func<object?>>[] { () => e.Int },
            orderBy: new[] { NORM.SQL.desc(() => e.Id) })
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
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        prev = NORM.SQL.lag(e.Id, 1, 0L).Over(NORM.SQL.asc(() => e.Id)),
        next = NORM.SQL.lead(e.Int, 2, 0).Over(NORM.SQL.asc(() => e.Id))
    })
    .ToList();
```

```sql
select id, lag(id, 1, 0) over (order by id) as 'prev', lead(nullableint, 2, 0) over (order by id) as 'next' from complex_entity
```

## Windowed aggregates

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        total = NORM.SQL.sum_over(e.Id).Over(partitionBy: () => e.Int),
        n = NORM.SQL.count_over().Over(partitionBy: () => e.Int)
    })
    .ToList();
```

```sql
select id, sum(id) over (partition by nullableint) as 'total', count(*) over (partition by nullableint) as 'n' from complex_entity
```

## Frames

A frame restricts the rows an aggregate sees. `NORM.WindowFrame` has factory methods for both SQL frame
units and `NORM.WindowFrameBound` has the boundaries:

| Factory | Renders |
|---|---|
| `WindowFrame.Rows(start, end)` | `rows between <start> and <end>` |
| `WindowFrame.Range(start, end)` | `range between <start> and <end>` |
| `WindowFrame.Rows(preceding, following)` | `rows between <preceding> preceding and <following> following` |
| `WindowFrame.RowsUnboundedPrecedingToCurrentRow` | `rows between unbounded preceding and current row` |
| `WindowFrame.RangeUnboundedPrecedingToCurrentRow` | `range between unbounded preceding and current row` |

| Boundary | Renders |
|---|---|
| `WindowFrameBound.UnboundedPreceding` | `unbounded preceding` |
| `WindowFrameBound.Preceding(n)` | `<n> preceding` |
| `WindowFrameBound.CurrentRow` | `current row` |
| `WindowFrameBound.Following(n)` | `<n> following` |
| `WindowFrameBound.UnboundedFollowing` | `unbounded following` |

A framed running aggregate and a sliding window:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        running = NORM.SQL.sum_over(e.Id).Over(
            NORM.SQL.asc(() => e.Id),
            NORM.WindowFrame.RowsUnboundedPrecedingToCurrentRow),
        sliding = NORM.SQL.sum_over(e.Id).Over(
            NORM.SQL.asc(() => e.Id),
            NORM.WindowFrame.Rows(1, 1))
    })
    .ToList();
```

```sql
select id, sum(id) over (order by id rows between unbounded preceding and current row) as 'running', sum(id) over (order by id rows between 1 preceding and 1 following) as 'sliding' from complex_entity
```

## Provider differences

Window functions are ANSI and every SQL provider nextorm targets supports all functions and both frame
units, so the rendered SQL is the same apart from identifier quoting (single quotes, brackets or double
quotes for the alias; see [Provider overview](../providers/overview.md)).

| Provider | Behaviour |
|---|---|
| SQLite | Full `OVER` support; column aliases single-quoted (`as 'rn'`). |
| SQL Server | Full `OVER` support; aliases bracket-quoted (`as [rn]`). |
| PostgreSQL | Full `OVER` support; aliases double-quoted (`as "rn"`). |
| In-memory | Not applicable: window functions are rendered by the SQL dialects and are not part of the in-memory provider. |

## See also

* [Grouping and aggregates](04-grouping-and-aggregates.md) - the scalar aggregates that `_over` variants shadow.
* [Sorting and paging](05-sorting-and-paging.md) - ordering the outer query that selects window columns.
* [Provider overview](../providers/overview.md) - alias quoting and provider capability flags.

---

Source: `src/nextorm.core/Query/NORM.cs:23` (`WindowFunction<T>`), `:64` (`WindowOrder`), `:80`/`:87` (frame enums), `:97` (`WindowFrameBound`), `:126` (`WindowFrame`), `:215` (`asc`/`desc`), `:221` (functions);
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:621`;
`test/nextorm.integration.tests/CommonTestSuite.Window.cs:14`, `:33`, `:54`, `:72`, `:99`;
`test/nextorm.core.tests/WindowFunctionMarkerTests.cs:28`, `:64`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:1266`, `:1281`, `:1297`, `:1314`, `:1330`, `:1346`, `:1366`, `:1383`, `:1401`, `:1422`.
