# Grouping and aggregates

> Group rows with [`GroupBy`](xref:NextORM.Core.EntityBuilder`1), filter groups with [`Having`](xref:NextORM.Core.EntityBuilder`1), and compute `count`, `min`, `max`, `avg`, `sum`, `stdev`, `var` and their `_distinct` variants through ``

**Prerequisites:** [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md) · [Sorting and paging](05-sorting-and-paging.md)

## Overview

`EntityBuilder<T>.GroupBy(...)` attaches a `GROUP BY` clause and `EntityBuilder<T>.Having(...)` attaches a `HAVING`
clause that filters the groups. Both are clauses on the builder, so they are combined with [`Where`](xref:NextORM.Core.EntityBuilder`1),
[`OrderBy`](xref:NextORM.Core.EntityBuilder`1), [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Page`](xref:NextORM.Core.EntityBuilder`1) and the projection exactly like any other query:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

The aggregate functions live on [`Sql`](xref:NextORM.Core.SqlFunctions.Sql) and are only meaningful inside a projection or a [`Having`](xref:NextORM.Core.EntityBuilder`1)
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

The common aggregates also have convenience terminals on `EntityBuilder<T>`: [`Count`](xref:NextORM.Core.EntityBuilder`1), `Min(x)`, `Max(x)`,
`Avg(x)`, `Sum(x)`, `Stdev(x)`, `Stdevp(x)`, `Var(x)` and `Varp(x)`, each with an `...Async` twin and
an overload that accepts positional parameters. `count`, `count_big`, `count_distinct` and
`count_big_distinct` have no entity terminal and are used through ``

## GroupBy

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

```sql
select nullableint as 'Int', count(*) from complex_entity group by nullableint
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Int | count |
|-----|-------|
| null | 1 |
| 1 | 2 |

## Having

[`Having`](xref:NextORM.Core.EntityBuilder`1) filters groups after aggregation, where a [`Where`](xref:NextORM.Core.EntityBuilder`1) filters rows before it:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Having(e => SqlFunctions.Sql.count() > 1)
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

```sql
select nullableint as 'Int', count(*) from complex_entity group by nullableint having (count(*) > 1)
```

Output:

| Int | count |
|-----|-------|
| 1 | 2 |

[`Where`](xref:NextORM.Core.EntityBuilder`1) and [`Having`](xref:NextORM.Core.EntityBuilder`1) can be combined on the same query; the [`Where`](xref:NextORM.Core.EntityBuilder`1) is applied before grouping:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Having(e => SqlFunctions.Sql.count() > 1)
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

Output:

| Int | count |
|-----|-------|
| 1 | 2 |

## Grouping with sort and limit

A grouped projection is sorted and paged like any other query. Ordering by the ordinal of a selected
column keeps the query provider-portable:

```csharp
// Order by the aggregate (never null), then take the first group.
var first = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .OrderBy(2, OrderDirection.Asc)
    .First();
```

```sql
select nullableint as 'Int', count(*) from complex_entity group by nullableint order by 2 limit 1
```

Output:

| Int | count |
|-----|-------|
| null | 1 |

```csharp
var top = dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .OrderBy(1, OrderDirection.Desc)
    .First();
```

Output:

| Int | count |
|-----|-------|
| 1 | 2 |

Note that `NULL` ordering differs between providers (PostgreSQL sorts `NULL`s first on a descending
sort, SQLite last), which is why the sort example excludes the `NULL` group.

## ROLLUP and CUBE

`GroupByRollup(...)` and `GroupByCube(...)` attach the ANSI super-aggregate modifiers. `ROLLUP` adds
every prefix subtotal plus the grand total; `CUBE` adds every combination:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupByRollup(e => new { e.Int, e.Boolean })
    .Select(e => new { e.Int, e.Boolean, count = SqlFunctions.Sql.count() })
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

## WITH TOTALS

ClickHouse adds `WITH TOTALS` ([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals)) through
`.WithTotals()` after any grouping: the query returns the group rows plus one row with the totals over
all groups. It can be combined with `ROLLUP`/`CUBE` but not with `GROUPING SETS`. The official
`ClickHouse.Driver` does not surface the totals row, so nextorm emits the modifier but only the group
rows are read back.

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
    .Select(e => new { e.Int, e.Boolean, count = SqlFunctions.Sql.count() })
    .ToList();
```

```sql
select nullableint as 'Int', b as 'Boolean', count(*) from complex_entity group by grouping sets ((nullableint, b), (nullableint), ())
```

Grouping sets are available on SQL Server, PostgreSQL, SQLite and ClickHouse
([`SupportsGroupingSets`](xref:NextORM.Core.ISqlDialect.SupportsGroupingSets)); MySQL/MariaDB and the in-memory provider throw
`NotSupportedException`. An out-of-range index throws [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException).

## Aggregates without grouping

An aggregate over the whole table is a projection without [`GroupBy`](xref:NextORM.Core.EntityBuilder`1):

```csharp
var count = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.count()).First();
var sum   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.sum(e.Id)).First();
var avg   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.avg(e.Id)).First();
```

```sql
select count(*) from simple_entity
```

Output:

| Count |
|-------|
| 10 |

```sql
select sum(id) from simple_entity
```

Output:

| Sum |
|-----|
| 55 |

`avg` keeps the fractional part on SQLite and PostgreSQL; SQL Server evaluates `AVG` over an integer
column as an integer. The entity terminals forward to the same functions:

```csharp
var min = dataContext.From<ISimpleEntity>().Min(e => e.Id);            // SqlFunctions.Sql.min
var max = await dataContext.From<ISimpleEntity>().MaxAsync(e => e.Id); // SqlFunctions.Sql.max
```

## Counting

```csharp
var all       = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.count()).First();             // count(*)
var nonNull   = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.count(e.Int)).First();       // count(nullableint)
var distinct  = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.count_distinct(e.Int)).First();
var big       = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.count_big()).First();         // long
var bigDist   = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.count_big_distinct(e.Int)).First();
```

```sql
select count(*) from simple_entity
select count(nullableint) from complex_entity
select count(distinct nullableint) from complex_entity
```

Output:

| Count |
|-------|
| 10 |

Output:

| Count |
|-------|
| 2 |

Output:

| Count |
|-------|
| 1 |

`count_big()` returns a 64-bit count. SQL Server has a distinct `count_big` function and emits
`count_big(...)`; SQLite and PostgreSQL already return a 64-bit integer from `count(...)`, so the
dialect emits `count(...)` for both. ClickHouse count aggregates return an unsigned `UInt64`, which
the row reader cannot materialise, so the dialect casts `count`/`count_distinct`/`count_if` to
`toInt32(...)` and `count_big`/`count_big_distinct` to `toInt64(...)`.

The builder-level shortcut `EntityBuilder<T>.Count()` is equivalent to
`Select(e => SqlFunctions.Sql.count())` followed by [`First`](xref:NextORM.Core.EntityBuilder`1):

```csharp
var count = dataContext.From<ISimpleEntity>().Count();
```

## min / max / avg / sum

```csharp
var min = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.min(e.Id)).First();
var max = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.max(e.Id)).First();
var avg = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.avg(e.Id)).First();
var sum = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.sum(e.Id)).First();
```

```sql
select min(id) from simple_entity
select max(id) from simple_entity
select avg(id) from simple_entity
select sum(id) from simple_entity
```

Output:

| Min |
|-----|
| 1 |

Output:

| Max |
|-----|
| 10 |

Output:

| Avg |
|-----|
| 6 |

Output:

| Sum |
|-----|
| 55 |

The `_distinct` forms add `distinct`:

```csharp
var avgDistinct = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.avg_distinct(e.Int)).First();
var sumDistinct = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.sum_distinct(e.Int)).First();
```

```sql
select avg(distinct nullableint) from complex_entity
select sum(distinct nullableint) from complex_entity
```

## stdev / stdevp / var / varp

```csharp
var stdev  = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.stdev(e.Id)).First();
var stdevp = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.stdevp(e.Id)).First();
var var    = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.var(e.Id)).First();
var varp   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.varp(e.Id)).First();
```

```sql
select stdev(id) from simple_entity
select stdevp(id) from simple_entity
select var(id) from simple_entity
select varp(id) from simple_entity
```

Output:

| Stdev |
|-------|
| 3 |

Output:

| Stdevp |
|--------|
| 3 |

Output:

| Var |
|-----|
| 9 |

Output:

| Varp |
|------|
| 8 |

`stdev` is the sample standard deviation, `stdevp` the population one; `var`/`varp` are the matching
variances. Each has a `_distinct` form (`stdev_distinct`, `stdevp_distinct`, `var_distinct`,
`varp_distinct`) and an entity terminal ([`Stdev`](xref:NextORM.Core.EntityBuilder`1), [`Stdevp`](xref:NextORM.Core.EntityBuilder`1), [`Var`](xref:NextORM.Core.EntityBuilder`1), [`Varp`](xref:NextORM.Core.EntityBuilder`1)) with a synchronous,
`...Async` and parameterised overload:

```csharp
var sample = dataContext.From<ISimpleEntity>().Stdev(e => e.Id);
var asyncSample = await dataContext.From<ISimpleEntity>().VarAsync(e => e.Id);
var sumAfter5 = dataContext.From<ISimpleEntity>()
    .Where(e => e.Id > SqlFunctions.Parameter<int>(0))
    .Sum(e => e.Id, 5);
```

Projecting through `double` avoids the lossy integer conversion:

```csharp
var variance = dataContext.From<ISimpleEntity>().Select(x => SqlFunctions.Sql.varp((double)x.Id)).First();
```

## Boolean, bitwise, statistical and ordered-set aggregates

Beyond `count`/`min`/`max`/`sum`/`avg`/`stdev`/`var`, [`Sql`](xref:NextORM.Core.SqlFunctions.Sql) exposes several aggregate families,
with the provider-only ones on [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (boolean, bitwise, regression and ordered-set) and
[`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (`arg_min`/`arg_max`, the distinct-count
`uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12`, the parameterised quantile family
`quantile`/`quantile_exact`/`quantile_timing`/`median` and the `-If` combinator). Each family is gated by
its own dialect capability; PostgreSQL and ClickHouse opt into different subsets.

| Family | C# | SQL | Capability | Providers |
|---|---|---|---|---|
| Boolean | `SqlFunctions.Postgres.bool_and(x)`, `bool_or(x)`, `every(x)` | `bool_and(x)`, ... | [`SupportsBooleanAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBooleanAggregates) | PostgreSQL |
| Bitwise | `SqlFunctions.Postgres.bit_and(x)`, `bit_or(x)`, `bit_xor(x)` | `bit_and(x)` … / `groupBitAnd(x)` … | [`SupportsBitAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBitAggregates) | PostgreSQL, ClickHouse |
| Statistical | `SqlFunctions.Sql.corr(y, x)`, `covar_pop(y, x)`, `covar_samp(y, x)` | `corr(y, x)`, ... / `covarPop(y, x)`, ... | [`SupportsStatisticalAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStatisticalAggregates) | PostgreSQL, ClickHouse |
| Regression | `SqlFunctions.Postgres.regr_slope(y, x)`, `regr_intercept(y, x)`, `regr_r2(y, x)`, `regr_count(y, x)`, `regr_avgx(y, x)`, `regr_avgy(y, x)` | `regr_slope(y, x)`, ... | [`SupportsRegressionAggregates`](xref:NextORM.Core.ISqlDialect.SupportsRegressionAggregates) | PostgreSQL |
| ArgMin/ArgMax | `SqlFunctions.ClickHouse.arg_min(value, by)`, `arg_max(value, by)` | `argMin(value, by)`, `argMax(value, by)` | [`SupportsArgMinMax`](xref:NextORM.Core.ISqlDialect.SupportsArgMinMax) | ClickHouse |
| Distinct count | `SqlFunctions.ClickHouse.uniq(x)`, `uniq_exact(x)`, `uniq_combined(x)`, `uniq_hll12(x)` | `toInt64(uniq(x))`, `toInt64(uniqExact(x))`, ... | [`SupportsUniqAggregates`](xref:NextORM.Core.ISqlDialect.SupportsUniqAggregates) | ClickHouse |
| Quantile / median | `SqlFunctions.ClickHouse.quantile(0.5, x)`, `quantile_exact(0.9, x)`, `quantile_timing(0.5, x)`, `median(x)` | `toFloat64(quantile(0.5)(x))`, `toFloat64(median(x))`, ... | [`SupportsQuantileAggregates`](xref:NextORM.Core.ISqlDialect.SupportsQuantileAggregates) | ClickHouse |
| Arbitrary value | `SqlFunctions.Sql.any_agg(x)` | `ANY_VALUE(x)` / `any(x)` | [`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate) | MySQL, ClickHouse |
| Last row | `SqlFunctions.ClickHouse.any_last(x)` | `anyLast(x)` | [`SupportsAnyAggregates`](xref:NextORM.Core.ISqlDialect.SupportsAnyAggregates) | ClickHouse |
| Filtered (`-If`) | `SqlFunctions.ClickHouse.count_if(() => p)`, `sum_if(x, () => p)`, `avg_if(x, () => p)`, `min_if(x, () => p)`, `max_if(x, () => p)` | `countIf(p)`, `sumIf(x, p)`, ... | [`SupportsIfAggregates`](xref:NextORM.Core.ISqlDialect.SupportsIfAggregates) | ClickHouse |
| Ordered-set | `SqlFunctions.Postgres.percentile_cont(fraction, () => x)`, `percentile_disc(fraction, () => x)`, `mode(() => x)` | `percentile_cont(f) within group (order by x)`, ... | [`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates) | PostgreSQL |

MariaDB does **not** support `any_agg`: it has no `ANY_VALUE` in 10.4–12.x (the SQL-2023 `T626` feature
is still pending, targeted for 13.2), so [`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate) is `false` and the call is rejected with `NotSupportedException`.

```csharp
var stats = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        AllTrue = SqlFunctions.Postgres.bool_and(e.Boolean),
        Xor = SqlFunctions.Postgres.bit_xor(e.Id),
        Correlation = SqlFunctions.Sql.corr(e.Id, e.Int),
        Median = SqlFunctions.Postgres.percentile_cont(0.5, () => e.Id)
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
        Bits = SqlFunctions.Postgres.bit_and(e.Id),
        Cov = SqlFunctions.Sql.covar_pop(e.Id, e.Int),
        FirstByMax = SqlFunctions.ClickHouse.arg_max(e.String, e.Id),
        Positives = SqlFunctions.ClickHouse.count_if(() => e.Id > 0L),
        PositiveSum = SqlFunctions.ClickHouse.sum_if(e.Id, () => e.Id > 0L)
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
clause is gated by [`SupportsFilter`](xref:NextORM.Core.ISqlDialect.SupportsFilter) (PostgreSQL and SQLite opt in); MySQL/MariaDB and
SQL Server reject it with `NotSupportedException`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Big = SqlFunctions.Sql.count(() => e.Id > 10L),
        Total = SqlFunctions.Sql.sum(e.Id, () => e.Boolean == true)
    })
    .ToList();
```

```sql
select nullableint, count(*) filter (where (id > 10)) as "Big", sum(id) filter (where (b = true)) as "Total"
from complex_entity group by nullableint
```

On ClickHouse the equivalent of a filtered aggregate is the `-If` combinator — `countIf`, `sumIf`,
`avgIf`, `minIf`, `maxIf` — exposed as `SqlFunctions.ClickHouse.count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`
([`SupportsIfAggregates`](xref:NextORM.Core.ISqlDialect.SupportsIfAggregates)). ClickHouse does not accept the ANSI `filter (where ...)` clause, so the
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
| MySQL | emits `count(*)` (already 64-bit) | `stdev` → `stddev_samp`, `stdevp` → `stddev_pop`, `var` → `var_samp`, `varp` → `var_pop` | fractional | native (`var_samp` / `var_pop`) |
| MariaDB | emits `count(*)` (already 64-bit) | same mapping as MySQL (`stddev_samp` / `stddev_pop` / `var_samp` / `var_pop`) | fractional | native (`var_samp` / `var_pop`) |
| ClickHouse | casts `count`/`count_distinct`/`count_if` to `toInt32(...)` and `count_big`/`count_big_distinct` to `toInt64(...)` (native `UInt64` is not materialisable) | `stdev` → `stddevSamp`, `stdevp` → `stddevPop`, `var` → `varSamp`, `varp` → `varPop`, `covar_*` → `covarPop`/`covarSamp`, `bit_*` → `groupBit*`, `*_if` → `*If` | fractional | `varSamp` / `varPop` |
| In-memory | not covered by the in-memory test suite | not covered | not covered | not covered |

Group ordering is not defined by SQL; assert by group key, not by position. PostgreSQL and SQLite
differ on `NULL` ordering (PostgreSQL first, SQLite last).

`GROUP BY ROLLUP (...)`/`CUBE (...)` is emitted in the ANSI form by SQL Server, PostgreSQL and SQLite,
and as the trailing `... WITH ROLLUP`/`WITH CUBE` by MySQL/MariaDB and ClickHouse. MySQL/MariaDB have
no `CUBE`, so [`GroupByCube`](xref:NextORM.Core.EntityBuilder`1) throws there; the in-memory provider rejects both modifiers.
`GROUP BY GROUPING SETS (...)` is available on SQL Server, PostgreSQL, SQLite and ClickHouse, but not on
MySQL/MariaDB or the in-memory provider. `WITH TOTALS` is ClickHouse-only; the in-memory provider rejects
it as well as `ROLLUP`/`CUBE`.

## See also

- [Sorting and paging](05-sorting-and-paging.md) - [`OrderBy`](xref:NextORM.Core.EntityBuilder`1) by expression or ordinal.
- [Joins](03-joins.md) - aggregate over a joined projection.
- [Querying and projections](01-querying-and-projections.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.GroupBy.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.Aggregates.cs:9`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:185`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:240`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:181`.
