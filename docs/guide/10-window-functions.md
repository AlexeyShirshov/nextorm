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

    // reference a named window declared on the query (see "Named windows")
    public T Over(string windowName);
}
```

[`Over`](xref:NextORM.Core.WindowFunction`1.Over(NextORM.Core.WindowOrder,NextORM.Core.WindowFrame)) with no arguments renders an empty specification (`over ()`). Because C# expression trees
reject named arguments that skip a preceding defaulted parameter, an **order-only** specification must
use the [`WindowOrder`](xref:NextORM.Core.WindowOrder) overload - `Over(SqlFunctions.Sql.asc(() => e.Id))` - rather than `Over(orderBy: ...)`.
`SqlFunctions.Sql.asc(expression)` and `SqlFunctions.Sql.desc(expression)` return a [`WindowOrder`](xref:NextORM.Core.WindowOrder) (an order key plus an
[`OrderDirection`](xref:NextORM.Core.OrderDirection)).

Calling a window function **without** [`Over`](xref:NextORM.Core.WindowFunction`1.Over(NextORM.Core.WindowOrder,NextORM.Core.WindowFrame)) is an error: the visitor throws `NotSupportedException`
whose message mentions [`Over`](xref:NextORM.Core.WindowFunction`1.Over(NextORM.Core.WindowOrder,NextORM.Core.WindowFrame)).

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
| Previous value in frame (ClickHouse) | `SqlFunctions.ClickHouse.lag_in_frame(property[, offset[, defaultValue]])` | `lagInFrame(expr, offset[, default])` |
| Next value in frame (ClickHouse) | `SqlFunctions.ClickHouse.lead_in_frame(property[, offset[, defaultValue]])` | `leadInFrame(expr, offset[, default])` |
| First in frame | `first_value(property)` | `first_value(expr)` |
| Last in frame | `last_value(property)` | `last_value(expr)` |
| N-th in frame | `nth_value(property, n)` | `nth_value(expr, n)` |
| Windowed sum | `sum_over(property)` | `sum(expr)` |
| Windowed average | `avg_over(property)` | `avg(expr)` |
| Windowed minimum | `min_over(property)` | `min(expr)` |
| Windowed maximum | `max_over(property)` | `max(expr)` |
| Windowed count | `count_over()` / `count_over(property)` | `count(*)` / `count(expr)` |

The aggregate variants carry an `_over` suffix so they do not clash with the scalar aggregates `sum`,
`avg`, `min`, `max` and `count` that are used with [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})).

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

### `lagInFrame` / `leadInFrame` (ClickHouse)

ClickHouse's [`lagInFrame`](xref:NextORM.Core.ClickHouseFunctions.lag_in_frame``1(``0))/[`leadInFrame`](xref:NextORM.Core.ClickHouseFunctions.lead_in_frame``1(``0)) are the frame-respecting
counterparts of `lag`/`lead`, exposed as `SqlFunctions.ClickHouse.lag_in_frame`/`lead_in_frame`
([`SupportsInFrameWindowFunctions`](xref:NextORM.Core.ISqlDialect.SupportsInFrameWindowFunctions)). The
standard `lag`/`lead` look at the whole partition and, on ClickHouse, reject an explicit frame with
`BAD_ARGUMENTS`; the in-frame variants are evaluated within the ordered frame, so a partial frame can
yield the default where the standard function would return a partition row:

```csharp
var startingFrame = WindowFrame.Rows(WindowFrameBound.CurrentRow, WindowFrameBound.Following(1));

var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        prev = SqlFunctions.Sql.lag(e.Id, 1, 0L).Over(SqlFunctions.Sql.asc(() => e.Id)),
        prevInFrame = SqlFunctions.ClickHouse.lag_in_frame(e.Id, 1, 0L)
            .Over(SqlFunctions.Sql.asc(() => e.Id), startingFrame)
    })
    .ToList();
```

```sql
-- ClickHouse: the frame [current row, 1 following] has no preceding row, so prevInFrame is always 0
select id,
       lag(id, 1, 0) over (order by id) as `prev`,
       lagInFrame(id, 1, 0) over (order by id rows between current row and 1 following) as `prevInFrame`
from complex_entity
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
| [`Rows`](xref:NextORM.Core.WindowFrame.Rows(NextORM.Core.WindowFrameBound,NextORM.Core.WindowFrameBound)) | `rows between <start> and <end>` |
| [`Range`](xref:NextORM.Core.WindowFrame.Range(NextORM.Core.WindowFrameBound,NextORM.Core.WindowFrameBound)) | `range between <start> and <end>` |
| [`Groups`](xref:NextORM.Core.WindowFrame.Groups(NextORM.Core.WindowFrameBound,NextORM.Core.WindowFrameBound)) | `groups between <start> and <end>` (peer groups) |
| [`Rows`](xref:NextORM.Core.WindowFrame.Rows(NextORM.Core.WindowFrameBound,NextORM.Core.WindowFrameBound)) | `rows between <preceding> preceding and <following> following` |
| [`RowsUnboundedPrecedingToCurrentRow`](xref:NextORM.Core.WindowFrame.RowsUnboundedPrecedingToCurrentRow) | `rows between unbounded preceding and current row` |
| [`RangeUnboundedPrecedingToCurrentRow`](xref:NextORM.Core.WindowFrame.RangeUnboundedPrecedingToCurrentRow) | `range between unbounded preceding and current row` |

| Boundary | Renders |
|---|---|
| [`UnboundedPreceding`](xref:NextORM.Core.WindowFrameBound.UnboundedPreceding) | `unbounded preceding` |
| [`Preceding`](xref:NextORM.Core.WindowFrameBound.Preceding(System.Int32)) | `<n> preceding` |
| [`CurrentRow`](xref:NextORM.Core.WindowFrameBound.CurrentRow) | `current row` |
| [`Following`](xref:NextORM.Core.WindowFrameBound.Following(System.Int32)) | `<n> following` |
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

A frame may also exclude rows around the current one with
[`WindowFrame.WithExclusion`](xref:NextORM.Core.WindowFrame.WithExclusion(NextORM.Core.WindowFrameExclusion)) and a
[`WindowFrameExclusion`](xref:NextORM.Core.WindowFrameExclusion):

| Exclusion | Renders |
|---|---|
| [`NoOthers`](xref:NextORM.Core.WindowFrameExclusion.NoOthers) | `exclude no others` (the default) |
| [`CurrentRow`](xref:NextORM.Core.WindowFrameExclusion.CurrentRow) | `exclude current row` |
| [`Group`](xref:NextORM.Core.WindowFrameExclusion.Group) | `exclude group` (current row and its peers) |
| [`Ties`](xref:NextORM.Core.WindowFrameExclusion.Ties) | `exclude ties` (peers only) |

## Named windows

A window specification can be declared once on the query and reused by several window functions,
rendering a single SQL `WINDOW` clause. Declare it with
`EntityBuilder<TEntity>.Window(name, partitionBy, orderBy, frame)` and reference it with
`Over("name")`; build the `ORDER BY` keys with the builder's `Asc`/`Desc` helpers.

```csharp
var e = dataContext.From<IComplexEntity>();

var rows = e
    .Window("w", partitionBy: [x => x.Int], orderBy: [e.Asc(x => x.Id)])
    .Select(x => new
    {
        x.Id,
        rn = SqlFunctions.Sql.row_number().Over("w"),
        total = SqlFunctions.Sql.sum_over(x.Id).Over("w")
    })
    .ToList();
```

```sql
select id, row_number() over w as 'rn', sum(id) over w as 'total' from complex_entity window w as (partition by nullableint order by id)
```

The name must be a plain SQL identifier. A repeated name on the same query, an invalid name, or
`Window` before a later `Join` is rejected. Named windows are supported by PostgreSQL, MySQL, MariaDB,
ClickHouse and SQLite ([`SupportsNamedWindows`](xref:NextORM.Core.ISqlDialect.SupportsNamedWindows));
SQL Server has no `WINDOW` clause and rejects the query with `NotSupportedException`.

## Provider differences

Window functions are ANSI and every SQL provider nextorm targets supports the `ROWS` and `RANGE` frame
units, so most SQL is the same apart from identifier quoting (single quotes, brackets or double quotes
for the alias; see [Provider overview](../providers/overview.md)). The `percent_rank()`/`cume_dist()`
pair is supported by every provider
([`SupportsPercentRankCumeDist`](xref:NextORM.Core.ISqlDialect.SupportsPercentRankCumeDist)).
The only non-portable value function is `nth_value`: SQL Server has no `NTH_VALUE`, so
[`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue) makes it reject the call with
`NotSupportedException`, while PostgreSQL, MySQL/MariaDB, SQLite and ClickHouse render `nth_value(expr, n)`.

The newer pieces are gated individually:

- **Named windows** ([`SupportsNamedWindows`](xref:NextORM.Core.ISqlDialect.SupportsNamedWindows)) -
  PostgreSQL, MySQL, MariaDB, ClickHouse and SQLite; SQL Server has no `WINDOW` clause.
- **`GROUPS` frame unit** ([`SupportsWindowFrameGroups`](xref:NextORM.Core.ISqlDialect.SupportsWindowFrameGroups)) -
  PostgreSQL (11+), ClickHouse and SQLite (3.28+); SQL Server, MySQL and MariaDB reject it.
- **Frame `EXCLUDE`** ([`SupportsWindowFrameExclusion`](xref:NextORM.Core.ISqlDialect.SupportsWindowFrameExclusion)) -
  PostgreSQL and SQLite; SQL Server, MySQL, MariaDB and ClickHouse reject it.

The window percentiles are another exception: `percentile_cont`/`percentile_disc` are not portable as
window functions. SQL Server and MariaDB render them as
`percentile_cont(f) within group (order by x) over (...)` under
[`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow); PostgreSQL expresses
percentiles as an ordered-set **aggregate** instead
([`SqlFunctions.Postgres.percentile_cont`](xref:NextORM.Core.PostgresFunctions.percentile_cont``1(System.Double,System.Linq.Expressions.Expression{System.Func{``0}}))), and
MySQL, SQLite and ClickHouse reject the window form with `NotSupportedException`.

| Provider | Behaviour |
|---|---|
| SQLite | Full `OVER` support, named windows, `GROUPS` and `EXCLUDE`; column aliases single-quoted (`as 'rn'`). |
| SQL Server | Full `OVER` support; no named windows, no `GROUPS`, no `EXCLUDE`, no `nth_value`; aliases bracket-quoted (`as [rn]`). |
| PostgreSQL | Full `OVER` support, named windows, `GROUPS` (11+) and `EXCLUDE`; aliases double-quoted (`as "rn"`). |
| MySQL | `OVER` support and named windows; no `GROUPS`, no `EXCLUDE`; column aliases backtick-quoted (`` as `rn` ``). |
| MariaDB | `OVER` support and named windows; no `GROUPS`, no `EXCLUDE`; aliases backtick-quoted. |
| ClickHouse | `OVER` support, named windows and `GROUPS`; no `EXCLUDE`; aliases backtick-quoted. |
| In-memory | Not applicable: window functions are rendered by the SQL dialects and are not part of the in-memory provider. |

## See also

* [Grouping and aggregates](04-grouping-and-aggregates.md) - the scalar aggregates that `_over` variants shadow.
* [Sorting and paging](05-sorting-and-paging.md) - ordering the outer query that selects window columns.
* [Provider overview](../providers/overview.md) - alias quoting and provider capability flags.

---

Source: `src/nextorm.core/Query/WindowFunctions.cs` ([`WindowFunction<T>`](xref:NextORM.Core.WindowFunction`1), [`WindowOrder`](xref:NextORM.Core.WindowOrder), frame enums, [`WindowFrameBound`](xref:NextORM.Core.WindowFrameBound), [`WindowFrame`](xref:NextORM.Core.WindowFrame)); `src/nextorm.core/Query/WindowDefinition.cs` ([`WindowDefinition`](xref:NextORM.Core.WindowDefinition), [`NamedWindowOrderKey`](xref:NextORM.Core.NamedWindowOrderKey)); `src/nextorm.core/Query/SqlFunctions.cs` (`asc`/`desc`, functions);
`src/nextorm.core/Visitors/WindowFunctionTranslator.cs`, `src/nextorm.core/Visitors/WindowSql.cs`;
`tests/nextorm.integration.tests/CommonTestSuite.Window.cs`, `tests/nextorm.integration.tests/PostgresSpecificTests.cs` (named windows/`GROUPS`/`EXCLUDE`);
`tests/nextorm.core.tests/WindowFunctionMarkerTests.cs`;
generated SQL: `tests/nextorm.postgres.tests/SqlGenerationTests.cs`, `tests/nextorm.sqlite.tests/SqlGenerationTests.cs`.
