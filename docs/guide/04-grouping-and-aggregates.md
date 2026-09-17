# Grouping and aggregates

> Group rows with `GroupBy`, filter groups with `Having`, and compute `count`, `min`, `max`, `avg`, `sum`, `stdev`, `var` and their `_distinct` variants through `NORM.SQL`.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md) · [Sorting and paging](05-sorting-and-paging.md)

## Overview

`Entity<T>.GroupBy(...)` attaches a `GROUP BY` clause and `Entity<T>.Having(...)` attaches a `HAVING`
clause that filters the groups. Both are clauses on the builder, so they are combined with `Where`,
`OrderBy`, `Limit`/`Page` and the projection exactly like any other query:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .ToList();
```

The aggregate functions live on `NORM.SQL` and are only meaningful inside a projection or a `Having`
predicate. They are named exactly as follows:

| Function | Result | Function | Result |
|---|---|---|---|
| `count()` | `int` | `count_distinct(...)` | `int` |
| `count_big()` | `long` | `count_big_distinct(...)` | `long` |
| `min(x)` | same as `x` | `avg_distinct(x)` | same as `x` |
| `max(x)` | same as `x` | `sum_distinct(x)` | same as `x` |
| `avg(x)` | same as `x` | `stdev_distinct(x)` | same as `x` |
| `sum(x)` | same as `x` | `stdevp_distinct(x)` | same as `x` |
| `stdev(x)` | same as `x` | `var_distinct(x)` | same as `x` |
| `stdevp(x)` | same as `x` | `varp_distinct(x)` | same as `x` |
| `var(x)` | same as `x` | `varp(x)` | same as `x` |

Called with no arguments, `count()` / `count_big()` count rows (`count(*)`); called with one or more
expressions they count non-null values of those expressions. The other functions take an expression
argument. The `_distinct` suffix adds `distinct` inside the parentheses.

The common aggregates also have convenience terminals on `Entity<T>`: `Count()`, `Min(x)`, `Max(x)`,
`Avg(x)`, `Sum(x)`, `Stdev(x)`, `Stdevp(x)`, `Var(x)` and `Varp(x)`, each with an `...Async` twin and
an overload that accepts positional parameters. `count`, `count_big`, `count_distinct` and
`count_big_distinct` have no entity terminal and are used through `NORM.SQL`.

## GroupBy

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .ToList();
```

```sql
select nullableint as 'Int', count(*) from complex_entity group by nullableint
```

## Having

`Having` filters groups after aggregation, where a `Where` filters rows before it:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Having(e => NORM.SQL.count() > 1)
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .ToList();
```

```sql
select nullableint as 'Int', count(*) from complex_entity group by nullableint having (count(*) > 1)
```

`Where` and `Having` can be combined on the same query; the `Where` is applied before grouping:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Having(e => NORM.SQL.count() > 1)
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .ToList();
```

## Grouping with sort and limit

A grouped projection is sorted and paged like any other query. Ordering by the ordinal of a selected
column keeps the query provider-portable:

```csharp
// Order by the aggregate (never null), then take the first group.
var first = dataContext.Create<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .OrderBy(2, OrderDirection.Asc)
    .First();
```

```sql
select nullableint as 'Int', count(*) from complex_entity group by nullableint order by 2 limit 1
```

```csharp
var top = dataContext.Create<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .OrderBy(1, OrderDirection.Desc)
    .First();
```

Note that `NULL` ordering differs between providers (PostgreSQL sorts `NULL`s first on a descending
sort, SQLite last), which is why the sort example excludes the `NULL` group.

## Aggregates without grouping

An aggregate over the whole table is a projection without `GroupBy`:

```csharp
var count = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.count()).First();
var sum   = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.sum(e.Id)).First();
var avg   = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.avg(e.Id)).First();
```

```sql
select count(*) from simple_entity
```

```sql
select sum(id) from simple_entity
```

`avg` keeps the fractional part on SQLite and PostgreSQL; SQL Server evaluates `AVG` over an integer
column as an integer. The entity terminals forward to the same functions:

```csharp
var min = dataContext.Create<ISimpleEntity>().Min(e => e.Id);            // NORM.SQL.min
var max = await dataContext.Create<ISimpleEntity>().MaxAsync(e => e.Id); // NORM.SQL.max
```

## Counting

```csharp
var all       = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.count()).First();             // count(*)
var nonNull   = dataContext.Create<IComplexEntity>().Select(e => NORM.SQL.count(e.Int)).First();       // count(nullableint)
var distinct  = dataContext.Create<IComplexEntity>().Select(e => NORM.SQL.count_distinct(e.Int)).First();
var big       = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.count_big()).First();         // long
var bigDist   = dataContext.Create<IComplexEntity>().Select(e => NORM.SQL.count_big_distinct(e.Int)).First();
```

```sql
select count(*) from simple_entity
select count(nullableint) from complex_entity
select count(distinct nullableint) from complex_entity
```

`count_big()` returns a 64-bit count. SQL Server has a distinct `count_big` function and emits
`count_big(...)`; SQLite and PostgreSQL already return a 64-bit integer from `count(...)`, so the
dialect emits `count(...)` for both.

The builder-level shortcut `Entity<T>.Count()` is equivalent to
`Select(e => NORM.SQL.count())` followed by `First()`:

```csharp
var count = dataContext.Create<ISimpleEntity>().Count();
```

## min / max / avg / sum

```csharp
var min = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.min(e.Id)).First();
var max = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.max(e.Id)).First();
var avg = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.avg(e.Id)).First();
var sum = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.sum(e.Id)).First();
```

```sql
select min(id) from simple_entity
select max(id) from simple_entity
select avg(id) from simple_entity
select sum(id) from simple_entity
```

The `_distinct` forms add `distinct`:

```csharp
var avgDistinct = dataContext.Create<IComplexEntity>().Select(e => NORM.SQL.avg_distinct(e.Int)).First();
var sumDistinct = dataContext.Create<IComplexEntity>().Select(e => NORM.SQL.sum_distinct(e.Int)).First();
```

```sql
select avg(distinct nullableint) from complex_entity
select sum(distinct nullableint) from complex_entity
```

## stdev / stdevp / var / varp

```csharp
var stdev  = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.stdev(e.Id)).First();
var stdevp = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.stdevp(e.Id)).First();
var var    = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.var(e.Id)).First();
var varp   = dataContext.Create<ISimpleEntity>().Select(e => NORM.SQL.varp(e.Id)).First();
```

```sql
select stdev(id) from simple_entity
select stdevp(id) from simple_entity
select var(id) from simple_entity
select varp(id) from simple_entity
```

`stdev` is the sample standard deviation, `stdevp` the population one; `var`/`varp` are the matching
variances. Each has a `_distinct` form (`stdev_distinct`, `stdevp_distinct`, `var_distinct`,
`varp_distinct`) and an entity terminal (`Stdev`, `Stdevp`, `Var`, `Varp`) with a synchronous,
`...Async` and parameterised overload:

```csharp
var sample = dataContext.Create<ISimpleEntity>().Stdev(e => e.Id);
var asyncSample = await dataContext.Create<ISimpleEntity>().VarAsync(e => e.Id);
var sumAfter5 = dataContext.Create<ISimpleEntity>()
    .Where(e => e.Id > NORM.Param<int>(0))
    .Sum(e => e.Id, 5);
```

Projecting through `double` avoids the lossy integer conversion:

```csharp
var variance = dataContext.Create<ISimpleEntity>().Select(x => NORM.SQL.varp((double)x.Id)).First();
```

## Provider differences

| Provider | `count_big` | Aggregate names | Integer `AVG` | `var` / `varp` |
|---|---|---|---|---|
| SQLite | emits `count(*)` (already 64-bit) | kept as written; `stdev`/`var` come from the custom aggregates registered by the provider | fractional (5.5 stays 5.5) | supported through those custom aggregates |
| SQL Server | emits `count_big(...)` | kept as written; `stdev`/`var` are native | integer (5.5 becomes 5) | native |
| PostgreSQL | emits `count(*)` (already 64-bit) | `stdev` → `stddev`, `stdevp` → `stddev_pop`, `var` → `variance`, `varp` → `var_pop` | fractional | native |
| In-memory | not covered by the in-memory test suite | not covered | not covered | not covered |

Group ordering is not defined by SQL; assert by group key, not by position. PostgreSQL and SQLite
differ on `NULL` ordering (PostgreSQL first, SQLite last).

## See also

- [Sorting and paging](05-sorting-and-paging.md) - `OrderBy` by expression or ordinal.
- [Joins](03-joins.md) - aggregate over a joined projection.
- [Querying and projections](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.GroupBy.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.Aggregates.cs:9`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:185`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:240`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:181`.
