# Строковые и массивные агрегаты

`SqlFunctions.Sql.string_agg` доступен в PostgreSQL, SQL Server 2017+, ClickHouse, MySQL/MariaDB и SQLite
([`SupportsStringAgg`](xref:NextORM.Core.ISqlDialect.SupportsStringAgg), по умолчанию берёт значение зонтичного
[`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates)); в ClickHouse он рендерится как
`arrayStringConcat(groupArray(x), delimiter)`, в MySQL/MariaDB — как
`group_concat(x separator delimiter)`, в SQLite — как `group_concat(x, delimiter)`.
`SqlFunctions.Postgres.array_agg` ([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg)) требует типа-массива, поэтому доступен только в
PostgreSQL. Результат `array_agg` — колонка-массив:

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

| C# | SQL | Провайдеры |
|---|---|---|
| `SqlFunctions.Sql.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` / `group_concat(x separator delimiter)` / `group_concat(x, delimiter)` | PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB, SQLite |
| `SqlFunctions.Postgres.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## Фильтр агрегатов (FILTER)

`count`/`count_big`/`min`/`max`/`avg`/`sum` и строковые/массивные агрегаты принимают дополнительный
аргумент `Expression<Func<bool>>`, который фильтрует строки, видимые агрегату. Предикат фильтра —
обычный предикат запроса и может ссылаться на колонки и параметры. Способ записи выбирает
[`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle): PostgreSQL и SQLite
генерируют ANSI-предложение `filter (where ...)`, ClickHouse — комбинатор `-If` (`countIf`/`sumIf`/...),
а MySQL/MariaDB и SQL Server отклоняют вызов:

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

В ClickHouse тот же запрос генерирует `toInt32(countIf((id > 10)))` и `sumIf(id, (b = true))`.

## Функции, возвращающие наборы (PostgreSQL)

`SqlFunctions.Postgres.generate_series` и `SqlFunctions.Postgres.unnest` — это предобъявленные источники
[`[SqlTableFunction]`](../guide/13-table-valued-functions.md), поэтому отдельная обёртка не нужна:

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

`generate_series` проецируется на [`Value`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow.Value), а `unnest` — на
[`Value`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1.Value); обе соответствуют единственной колонке, которую возвращает функция.
