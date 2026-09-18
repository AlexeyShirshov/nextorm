# Grouping and aggregates

> Group rows with `GroupBy`, filter groups with `Having`, and compute `count`, `min`, `max`, `avg`, `sum`, `stdev`, `var` and their `_distinct` variants through `NORM.SQL`.

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md) · [Sorting and paging](05-sorting-and-paging.md)

## Overview

`EntityBuilder<T>.GroupBy(...)` attaches a `GROUP BY` clause and `EntityBuilder<T>.Having(...)` attaches a `HAVING`
clause that filters the groups. Both are clauses on the builder, so they are combined with `Where`,
`OrderBy`, `Limit`/`Page` and the projection exactly like any other query:

```csharp
var rows = dataContext.From<IComplexEntity>()
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

The common aggregates also have convenience terminals on `EntityBuilder<T>`: `Count()`, `Min(x)`, `Max(x)`,
`Avg(x)`, `Sum(x)`, `Stdev(x)`, `Stdevp(x)`, `Var(x)` and `Varp(x)`, each with an `...Async` twin and
an overload that accepts positional parameters. `count`, `count_big`, `count_distinct` and
`count_big_distinct` have no entity terminal and are used through `NORM.SQL`.

## GroupBy

```csharp
var rows = dataContext.From<IComplexEntity>()
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
var rows = dataContext.From<IComplexEntity>()
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
var rows = dataContext.From<IComplexEntity>()
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
var first = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .OrderBy(2, OrderDirection.Asc)
    .First();
```

```sql
select nullableint as 'Int', count(*) from complex_entity group by nullableint order by 2 limit 1
```

```csharp
var top = dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .OrderBy(1, OrderDirection.Desc)
    .First();
```

Note that `NULL` ordering differs between providers (PostgreSQL sorts `NULL`s first on a descending
sort, SQLite last), which is why the sort example excludes the `NULL` group.

## ROLLUP and CUBE

`GroupByRollup(...)` and `GroupByCube(...)` attach the ANSI super-aggregate modifiers. `ROLLUP` adds
every prefix subtotal plus the grand total; `CUBE` adds every combination:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupByRollup(e => new { e.Int, e.Boolean })
    .Select(e => new { e.Int, e.Boolean, count = NORM.SQL.count() })
    .ToList();
```

```sql
-- SQL Server / PostgreSQL / SQLite
select nullableint as 'Int', b as 'Boolean', count(*) from complex_entity group by rollup (nullableint, b)
-- MySQL / MariaDB / ClickHouse
select nullableint as 'Int', b as 'Boolean', count(*) from complex_entity group by nullableint, b with rollup
```

`ROLLUP` is available on every SQL provider; `CUBE` is not available on MySQL/MariaDB. The in-memory
provider supports neither modifier and throws `NotSupportedException`.

## GROUPING SETS

`GroupByGroupingSets(...)` groups by an explicit list of subsets. The first argument declares the full
column list and each additional argument is a set of 0-based column indices; an empty set is the grand
total:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupByGroupingSets(e => new { e.Int, e.Boolean },
        new[] { 0, 1 },
        new[] { 0 },
        Array.Empty<int>())
    .Select(e => new { e.Int, e.Boolean, count = NORM.SQL.count() })
    .ToList();
```

```sql
select nullableint as 'Int', b as 'Boolean', count(*) from complex_entity group by grouping sets ((nullableint, b), (nullableint), ())
```

Grouping sets are available on SQL Server, PostgreSQL, SQLite and ClickHouse
(`ISqlDialect.SupportsGroupingSets`); MySQL/MariaDB and the in-memory provider throw
`NotSupportedException`. An out-of-range index throws `BuildSqlCommandException`.

## Aggregates without grouping

An aggregate over the whole table is a projection without `GroupBy`:

```csharp
var count = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.count()).First();
var sum   = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.sum(e.Id)).First();
var avg   = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.avg(e.Id)).First();
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
var min = dataContext.From<ISimpleEntity>().Min(e => e.Id);            // NORM.SQL.min
var max = await dataContext.From<ISimpleEntity>().MaxAsync(e => e.Id); // NORM.SQL.max
```

## Counting

```csharp
var all       = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.count()).First();             // count(*)
var nonNull   = dataContext.From<IComplexEntity>().Select(e => NORM.SQL.count(e.Int)).First();       // count(nullableint)
var distinct  = dataContext.From<IComplexEntity>().Select(e => NORM.SQL.count_distinct(e.Int)).First();
var big       = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.count_big()).First();         // long
var bigDist   = dataContext.From<IComplexEntity>().Select(e => NORM.SQL.count_big_distinct(e.Int)).First();
```

```sql
select count(*) from simple_entity
select count(nullableint) from complex_entity
select count(distinct nullableint) from complex_entity
```

`count_big()` returns a 64-bit count. SQL Server has a distinct `count_big` function and emits
`count_big(...)`; SQLite and PostgreSQL already return a 64-bit integer from `count(...)`, so the
dialect emits `count(...)` for both.

The builder-level shortcut `EntityBuilder<T>.Count()` is equivalent to
`Select(e => NORM.SQL.count())` followed by `First()`:

```csharp
var count = dataContext.From<ISimpleEntity>().Count();
```

## min / max / avg / sum

```csharp
var min = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.min(e.Id)).First();
var max = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.max(e.Id)).First();
var avg = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.avg(e.Id)).First();
var sum = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.sum(e.Id)).First();
```

```sql
select min(id) from simple_entity
select max(id) from simple_entity
select avg(id) from simple_entity
select sum(id) from simple_entity
```

The `_distinct` forms add `distinct`:

```csharp
var avgDistinct = dataContext.From<IComplexEntity>().Select(e => NORM.SQL.avg_distinct(e.Int)).First();
var sumDistinct = dataContext.From<IComplexEntity>().Select(e => NORM.SQL.sum_distinct(e.Int)).First();
```

```sql
select avg(distinct nullableint) from complex_entity
select sum(distinct nullableint) from complex_entity
```

## stdev / stdevp / var / varp

```csharp
var stdev  = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.stdev(e.Id)).First();
var stdevp = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.stdevp(e.Id)).First();
var var    = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.var(e.Id)).First();
var varp   = dataContext.From<ISimpleEntity>().Select(e => NORM.SQL.varp(e.Id)).First();
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
var sample = dataContext.From<ISimpleEntity>().Stdev(e => e.Id);
var asyncSample = await dataContext.From<ISimpleEntity>().VarAsync(e => e.Id);
var sumAfter5 = dataContext.From<ISimpleEntity>()
    .Where(e => e.Id > NORM.Param<int>(0))
    .Sum(e => e.Id, 5);
```

Projecting through `double` avoids the lossy integer conversion:

```csharp
var variance = dataContext.From<ISimpleEntity>().Select(x => NORM.SQL.varp((double)x.Id)).First();
```

## Boolean, bitwise, statistical and ordered-set aggregates

Beyond `count`/`min`/`max`/`sum`/`avg`/`stdev`/`var`, `NORM.SQL` exposes several aggregate families,
with the provider-only ones on `NORM.PG_SQL` (boolean, bitwise, regression and ordered-set) and
`NORM.CLK_SQL` (`arg_min`/`arg_max` and the `-If` combinator). Each family is gated by its own dialect
capability; PostgreSQL and ClickHouse opt into different subsets.

| Family | C# | SQL | Capability | Providers |
|---|---|---|---|---|
| Boolean | `NORM.PG_SQL.bool_and(x)`, `bool_or(x)`, `every(x)` | `bool_and(x)`, ... | `SupportsBooleanAggregates` | PostgreSQL |
| Bitwise | `NORM.PG_SQL.bit_and(x)`, `bit_or(x)`, `bit_xor(x)` | `bit_and(x)` … / `groupBitAnd(x)` … | `SupportsBitAggregates` | PostgreSQL, ClickHouse |
| Statistical | `NORM.SQL.corr(y, x)`, `covar_pop(y, x)`, `covar_samp(y, x)` | `corr(y, x)`, ... / `covarPop(y, x)`, ... | `SupportsStatisticalAggregates` | PostgreSQL, ClickHouse |
| Regression | `NORM.PG_SQL.regr_slope(y, x)`, `regr_intercept(y, x)`, `regr_r2(y, x)`, `regr_count(y, x)`, `regr_avgx(y, x)`, `regr_avgy(y, x)` | `regr_slope(y, x)`, ... | `SupportsRegressionAggregates` | PostgreSQL |
| ArgMin/ArgMax | `NORM.CLK_SQL.arg_min(value, by)`, `arg_max(value, by)` | `argMin(value, by)`, `argMax(value, by)` | `SupportsArgMinMax` | ClickHouse |
| Filtered (`-If`) | `NORM.CLK_SQL.count_if(() => p)`, `sum_if(x, () => p)`, `avg_if(x, () => p)`, `min_if(x, () => p)`, `max_if(x, () => p)` | `countIf(p)`, `sumIf(x, p)`, ... | `SupportsIfAggregates` | ClickHouse |
| Ordered-set | `NORM.PG_SQL.percentile_cont(fraction, () => x)`, `percentile_disc(fraction, () => x)`, `mode(() => x)` | `percentile_cont(f) within group (order by x)`, ... | `SupportsOrderedAggregates` | PostgreSQL |

```csharp
var stats = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        AllTrue = NORM.PG_SQL.bool_and(e.Boolean),
        Xor = NORM.PG_SQL.bit_xor(e.Id),
        Correlation = NORM.SQL.corr(e.Id, e.Int),
        Median = NORM.PG_SQL.percentile_cont(0.5, () => e.Id)
    })
    .First();
```

```sql
select bool_and(b), bit_xor(id), corr(id, nullableint), percentile_cont(0.5) within group (order by id) from complex_entity
```

On ClickHouse the same families render with their own spellings — `groupBitAnd`, `covarPop`,
`argMin`/`argMax` and the `-If` combinators — while the boolean aggregates and the `regr_*` family
are not available (ClickHouse has neither `bool_and` nor `regr_*`, and the dialect rejects them with
`NotSupportedException`):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Bits = NORM.PG_SQL.bit_and(e.Id),
        Cov = NORM.SQL.covar_pop(e.Id, e.Int),
        FirstByMax = NORM.CLK_SQL.arg_max(e.String, e.Id),
        Positives = NORM.CLK_SQL.count_if(() => e.Id > 0L),
        PositiveSum = NORM.CLK_SQL.sum_if(e.Id, () => e.Id > 0L)
    })
    .First();
```

```sql
-- ClickHouse
select groupBitAnd(id), covarPop(id, nullableint), argMax(somestring, id), countIf((id > 0)), sumIf(id, (id > 0)) from complex_entity
```

The ordered-set aggregates take the ordering key as a quoted lambda that closes over the query
parameter; the key becomes `order by <key>`. Calling one on a provider without the capability throws
`NotSupportedException`.

## Filtered aggregates (FILTER)

An aggregate can carry a `filter (where ...)` clause by passing a predicate as an extra argument. The
clause is gated by `ISqlDialect.SupportsFilter` (PostgreSQL and SQLite opt in); MySQL/MariaDB and
SQL Server reject it with `NotSupportedException`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Big = NORM.SQL.count(() => e.Id > 10L),
        Total = NORM.SQL.sum(e.Id, () => e.Boolean == true)
    })
    .ToList();
```

```sql
select nullableint, count(*) filter (where (id > 10)) as "Big", sum(id) filter (where (b = true)) as "Total"
from complex_entity group by nullableint
```

On ClickHouse the equivalent of a filtered aggregate is the `-If` combinator — `countIf`, `sumIf`,
`avgIf`, `minIf`, `maxIf` — exposed as `NORM.CLK_SQL.count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`
(`SupportsIfAggregates`). ClickHouse does not accept the ANSI `filter (where ...)` clause, so the
generic filtered-aggregate API rejects it there.

`string_agg`/`array_agg` take a filter as well; `string_agg` is available on PostgreSQL, SQL Server
2017+ and ClickHouse (as `arrayStringConcat(groupArray(x), delimiter)`), while `array_agg` is
PostgreSQL-only (SQL Server has no array type and ClickHouse's array columns cannot be materialised).
See [Scalar functions](11-scalar-functions.md#aggregate-filter) for the full surface.

## Provider differences

| Provider | `count_big` | Aggregate names | Integer `AVG` | `var` / `varp` |
|---|---|---|---|---|
| SQLite | emits `count(*)` (already 64-bit) | kept as written; `stdev`/`var` come from the custom aggregates registered by the provider | fractional (5.5 stays 5.5) | supported through those custom aggregates |
| SQL Server | emits `count_big(...)` | kept as written; `stdev`/`var` are native | integer (5.5 becomes 5) | native |
| PostgreSQL | emits `count(*)` (already 64-bit) | `stdev` → `stddev`, `stdevp` → `stddev_pop`, `var` → `variance`, `varp` → `var_pop` | fractional | native |
| ClickHouse | emits `count(*)` (already 64-bit) | `stdev` → `stddevSamp`, `stdevp` → `stddevPop`, `var` → `varSamp`, `varp` → `varPop`, `covar_*` → `covarPop`/`covarSamp`, `bit_*` → `groupBit*`, `*_if` → `*If` | fractional | `varSamp` / `varPop` |
| In-memory | not covered by the in-memory test suite | not covered | not covered | not covered |

Group ordering is not defined by SQL; assert by group key, not by position. PostgreSQL and SQLite
differ on `NULL` ordering (PostgreSQL first, SQLite last).

`GROUP BY ROLLUP (...)`/`CUBE (...)` is emitted in the ANSI form by SQL Server, PostgreSQL and SQLite,
and as the trailing `... WITH ROLLUP`/`WITH CUBE` by MySQL/MariaDB and ClickHouse. MySQL/MariaDB have
no `CUBE`, so `GroupByCube` throws there; the in-memory provider rejects both modifiers.
`GROUP BY GROUPING SETS (...)` is available on SQL Server, PostgreSQL, SQLite and ClickHouse, but not on
MySQL/MariaDB or the in-memory provider.

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
