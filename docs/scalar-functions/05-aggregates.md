# String and array aggregates

`SqlFunctions.Sql.string_agg` is available on PostgreSQL, SQL Server 2017+, ClickHouse, MySQL/MariaDB and SQLite
([`SupportsStringAgg`](xref:NextORM.Core.ISqlDialect.SupportsStringAgg), which defaults to the umbrella [`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates));
ClickHouse renders it as `arrayStringConcat(groupArray(x), delimiter)`, MySQL/MariaDB as
`group_concat(x separator delimiter)` and SQLite as `group_concat(x, delimiter)`. `SqlFunctions.Postgres.array_agg`
([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg)) requires an array type and is therefore PostgreSQL-only. An `array_agg`
result is an array column:

```csharp
var names = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Sql.string_agg(e.String, ","))
    .First();
```

```sql
-- PostgreSQL / SQL Server
select string_agg(somestring, ',') from complex_entity
-- ClickHouse
select arrayStringConcat(groupArray(somestring), ',') from complex_entity
-- MySQL/MariaDB
select group_concat(somestring separator ',') from complex_entity
-- SQLite
select group_concat(somestring, ',') from complex_entity
```

| C# | SQL | Providers |
|---|---|---|
| `SqlFunctions.Sql.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` / `group_concat(x separator delimiter)` / `group_concat(x, delimiter)` | PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB, SQLite |
| `SqlFunctions.Postgres.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## Aggregate FILTER

`count`/`count_big`/`min`/`max`/`avg`/`sum` and the string/array aggregates accept an extra
`Expression<Func<bool>>` argument that filters the rows the aggregate sees. The filter predicate is a
full query predicate and may reference columns and parameters. The spelling is selected by
[`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle) — PostgreSQL and SQLite
render the ANSI `filter (where ...)` clause, ClickHouse renders its `-If` combinator
(`countIf`/`sumIf`/...) and MySQL/MariaDB and SQL Server reject the call:

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

On ClickHouse the same query renders `toInt32(countIf((id > 10)))` and `sumIf(id, (b = true))`.

## Set-returning helpers (PostgreSQL)

`SqlFunctions.Postgres.generate_series` and `SqlFunctions.Postgres.unnest` are pre-declared
[`[SqlTableFunction]`](../guide/13-table-valued-functions.md) sources, so no user-defined wrapper is needed:

```csharp
var numbers = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`generate_series` selects [`Value`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow.Value) and `unnest` selects
[`Value`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1.Value); both map to the single column the function returns.
