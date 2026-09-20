# Группировка и агрегаты

> Группируйте строки с помощью [`GroupBy`](xref:NextORM.Core.EntityBuilder`1), фильтруйте группы с помощью [`Having`](xref:NextORM.Core.EntityBuilder`1) и вычисляйте `count`, `min`, `max`, `avg`, `sum`, `stdev`, `var` и их `_distinct`-варианты через ``

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md) · [Сортировка и постраничный вывод](05-sorting-and-paging.md)

## Обзор

`EntityBuilder<T>.GroupBy(...)` добавляет предложение `GROUP BY`, а `EntityBuilder<T>.Having(...)` добавляет
предложение `HAVING`, которое фильтрует группы. Оба являются предложениями построителя, поэтому они
комбинируются с [`Where`](xref:NextORM.Core.EntityBuilder`1), [`OrderBy`](xref:NextORM.Core.EntityBuilder`1), [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Page`](xref:NextORM.Core.EntityBuilder`1) и проекцией точно так же, как в любом другом
запросе:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

Агрегатные функции находятся в [`Sql`](xref:NextORM.Core.SqlFunctions.Sql) и имеют смысл только внутри проекции или предиката
[`Having`](xref:NextORM.Core.EntityBuilder`1). Они называются ровно так:

| Функция | Результат | Функция | Результат |
|---|---|---|---|
| `count()` | `int` | `count_distinct(...)` | `int` |
| `count_big()` | `long` | `count_big_distinct(...)` | `long` |
| `min(x)` | совпадает с `x` | `avg_distinct(x)` | совпадает с `x` |
| `max(x)` | совпадает с `x` | `sum_distinct(x)` | совпадает с `x` |
| `avg(x)` | совпадает с `x` | `stdev_distinct(x)` | совпадает с `x` |
| `sum(x)` | совпадает с `x` | `stdevp_distinct(x)` | совпадает с `x` |
| `stdev(x)` | совпадает с `x` | `var_distinct(x)` | совпадает с `x` |
| `stdevp(x)` | совпадает с `x` | `varp_distinct(x)` | совпадает с `x` |
| `var(x)` | совпадает с `x` | `varp(x)` | совпадает с `x` |

При вызове без аргументов `count()` / `count_big()` считают строки (`count(*)`); при вызове с одним
или несколькими выражениями они считают непустые (non-null) значения этих выражений. Остальные
функции принимают аргумент-выражение. Суффикс `_distinct` добавляет `distinct` внутри круглых
скобок.

У распространённых агрегатов также есть удобные терминалы на `EntityBuilder<T>`: [`Count`](xref:NextORM.Core.EntityBuilder`1), `Min(x)`,
`Max(x)`, `Avg(x)`, `Sum(x)`, `Stdev(x)`, `Stdevp(x)`, `Var(x)` и `Varp(x)`, каждый с `...Async`-двойником
и перегрузкой, принимающей позиционные параметры. У `count`, `count_big`, `count_distinct` и
`count_big_distinct` нет терминала на сущности, и они используются через ``

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

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Int | count |
|-----|-------|
| null | 1 |
| 1 | 2 |

## Having

[`Having`](xref:NextORM.Core.EntityBuilder`1) фильтрует группы после агрегирования, тогда как [`Where`](xref:NextORM.Core.EntityBuilder`1) фильтрует строки до него:

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

Вывод:

| Int | count |
|-----|-------|
| 1 | 2 |

[`Where`](xref:NextORM.Core.EntityBuilder`1) и [`Having`](xref:NextORM.Core.EntityBuilder`1) можно комбинировать в одном запросе; [`Where`](xref:NextORM.Core.EntityBuilder`1) применяется до группировки:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Having(e => SqlFunctions.Sql.count() > 1)
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

Вывод:

| Int | count |
|-----|-------|
| 1 | 2 |

## Группировка с сортировкой и ограничением

Сгруппированная проекция сортируется и разбивается на страницы, как и любой другой запрос.
Сортировка по порядковому номеру выбранного столбца сохраняет запрос переносимым между
провайдерами:

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

Вывод:

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

Вывод:

| Int | count |
|-----|-------|
| 1 | 2 |

Обратите внимание, что порядок `NULL` различается между провайдерами (PostgreSQL при сортировке по
убыванию ставит `NULL` первыми, SQLite — последними), поэтому в примере сортировки группа `NULL`
исключена.

## ROLLUP и CUBE

`GroupByRollup(...)` и `GroupByCube(...)` добавляют ANSI-модификаторы супер-агрегации. `ROLLUP`
добавляет каждую префиксную подытоговую строку и общий итог; `CUBE` — каждую комбинацию:

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

`ROLLUP` доступен на всех SQL-провайдерах; `CUBE` недоступен в MySQL/MariaDB. Провайдер in-memory не
поддерживает ни один из модификаторов и выбрасывает `NotSupportedException`.

## WITH TOTALS

ClickHouse добавляет `WITH TOTALS` ([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals)) через
`.WithTotals()` после любой группировки: запрос возвращает строки групп плюс одну строку с итогами по
всем группам. Сочетается с `ROLLUP`/`CUBE`, но не с `GROUPING SETS`. Официальный `ClickHouse.Driver` не
отдаёт строку итогов, поэтому nextorm эмитит модификатор, но читаются только строки групп.

## GROUPING SETS

`GroupByGroupingSets(...)` группирует по явному списку подмножеств. Первый аргумент объявляет полный
список колонок, а каждый следующий — набор 0-based индексов колонок; пустой набор — общий итог:

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

Grouping sets доступны в SQL Server, PostgreSQL, SQLite и ClickHouse
([`SupportsGroupingSets`](xref:NextORM.Core.ISqlDialect.SupportsGroupingSets)); MySQL/MariaDB и провайдер in-memory выбрасывают
`NotSupportedException`. Индекс вне диапазона выбрасывает [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException).

## Агрегаты без группировки

Агрегат по всей таблице — это проекция без [`GroupBy`](xref:NextORM.Core.EntityBuilder`1):

```csharp
var count = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.count()).First();
var sum   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.sum(e.Id)).First();
var avg   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.avg(e.Id)).First();
```

```sql
select count(*) from simple_entity
```

Вывод:

| Count |
|-------|
| 10 |

```sql
select sum(id) from simple_entity
```

Вывод:

| Sum |
|-----|
| 55 |

`avg` сохраняет дробную часть в SQLite и PostgreSQL; SQL Server вычисляет `AVG` по целочисленному
столбцу как целое. Терминалы на сущности перенаправляют к тем же функциям:

```csharp
var min = dataContext.From<ISimpleEntity>().Min(e => e.Id);            // SqlFunctions.Sql.min
var max = await dataContext.From<ISimpleEntity>().MaxAsync(e => e.Id); // SqlFunctions.Sql.max
```

## Подсчёт

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

Вывод:

| Count |
|-------|
| 10 |

Вывод:

| Count |
|-------|
| 2 |

Вывод:

| Count |
|-------|
| 1 |

`count_big()` возвращает 64-битное количество. В SQL Server есть отдельная функция `count_big`, и он
генерирует `count_big(...)`; SQLite и PostgreSQL и так возвращают 64-битное целое из `count(...)`,
поэтому диалект генерирует `count(...)` для обоих. Агрегаты ClickHouse возвращают беззнаковый
`UInt64`, который построитель строк не может материализовать, поэтому диалект оборачивает
`count`/`count_distinct`/`count_if` в `toInt32(...)`, а `count_big`/`count_big_distinct` — в
`toInt64(...)`.

Сокращение уровня построителя `EntityBuilder<T>.Count()` эквивалентно
`Select(e => SqlFunctions.Sql.count())` с последующим [`First`](xref:NextORM.Core.EntityBuilder`1):

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

Вывод:

| Min |
|-----|
| 1 |

Вывод:

| Max |
|-----|
| 10 |

Вывод:

| Avg |
|-----|
| 6 |

Вывод:

| Sum |
|-----|
| 55 |

Формы `_distinct` добавляют `distinct`:

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

Вывод:

| Stdev |
|-------|
| 3 |

Вывод:

| Stdevp |
|--------|
| 3 |

Вывод:

| Var |
|-----|
| 9 |

Вывод:

| Varp |
|------|
| 8 |

`stdev` — выборочное стандартное отклонение, `stdevp` — генеральное; `var`/`varp` — соответствующие
дисперсии. У каждой есть `_distinct`-форма (`stdev_distinct`, `stdevp_distinct`, `var_distinct`,
`varp_distinct`) и терминал на сущности ([`Stdev`](xref:NextORM.Core.EntityBuilder`1), [`Stdevp`](xref:NextORM.Core.EntityBuilder`1), [`Var`](xref:NextORM.Core.EntityBuilder`1), [`Varp`](xref:NextORM.Core.EntityBuilder`1)) с синхронной,
`...Async` и параметризованной перегрузкой:

```csharp
var sample = dataContext.From<ISimpleEntity>().Stdev(e => e.Id);
var asyncSample = await dataContext.From<ISimpleEntity>().VarAsync(e => e.Id);
var sumAfter5 = dataContext.From<ISimpleEntity>()
    .Where(e => e.Id > SqlFunctions.Parameter<int>(0))
    .Sum(e => e.Id, 5);
```

Проецирование через `double` позволяет избежать потери точности при преобразовании целых чисел:

```csharp
var variance = dataContext.From<ISimpleEntity>().Select(x => SqlFunctions.Sql.varp((double)x.Id)).First();
```

## Логические, битовые, статистические и упорядоченные агрегаты

Помимо `count`/`min`/`max`/`sum`/`avg`/`stdev`/`var`, [`Sql`](xref:NextORM.Core.SqlFunctions.Sql) предоставляет несколько семейств
агрегатов, а провайдерно-специфичные находятся в [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (логические, битовые, регрессионные и
упорядоченные) и [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (`arg_min`/`arg_max`, агрегаты числа уникальных
`uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12`, параметрическое семейство квантилей
`quantile`/`quantile_exact`/`quantile_timing`/`median` и комбинатор `-If`). Каждое семейство включается
своим флагом диалекта; PostgreSQL и ClickHouse включают разные подмножества.

| Семейство | C# | SQL | Флаг | Провайдеры |
|---|---|---|---|---|
| Логические | `SqlFunctions.Postgres.bool_and(x)`, `bool_or(x)`, `every(x)` | `bool_and(x)`, ... | [`SupportsBooleanAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBooleanAggregates) | PostgreSQL |
| Битовые | `SqlFunctions.Postgres.bit_and(x)`, `bit_or(x)`, `bit_xor(x)` | `bit_and(x)` … / `groupBitAnd(x)` … | [`SupportsBitAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBitAggregates) | PostgreSQL, ClickHouse |
| Статистические | `SqlFunctions.Sql.corr(y, x)`, `covar_pop(y, x)`, `covar_samp(y, x)` | `corr(y, x)`, ... / `covarPop(y, x)`, ... | [`SupportsStatisticalAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStatisticalAggregates) | PostgreSQL, ClickHouse |
| Регрессия | `SqlFunctions.Postgres.regr_slope(y, x)`, `regr_intercept(y, x)`, `regr_r2(y, x)`, `regr_count(y, x)`, `regr_avgx(y, x)`, `regr_avgy(y, x)` | `regr_slope(y, x)`, ... | [`SupportsRegressionAggregates`](xref:NextORM.Core.ISqlDialect.SupportsRegressionAggregates) | PostgreSQL |
| ArgMin/ArgMax | `SqlFunctions.ClickHouse.arg_min(value, by)`, `arg_max(value, by)` | `argMin(value, by)`, `argMax(value, by)` | [`SupportsArgMinMax`](xref:NextORM.Core.ISqlDialect.SupportsArgMinMax) | ClickHouse |
| Число уникальных | `SqlFunctions.ClickHouse.uniq(x)`, `uniq_exact(x)`, `uniq_combined(x)`, `uniq_hll12(x)` | `toInt64(uniq(x))`, `toInt64(uniqExact(x))`, ... | [`SupportsUniqAggregates`](xref:NextORM.Core.ISqlDialect.SupportsUniqAggregates) | ClickHouse |
| Квантиль / медиана | `SqlFunctions.ClickHouse.quantile(0.5, x)`, `quantile_exact(0.9, x)`, `quantile_timing(0.5, x)`, `median(x)` | `toFloat64(quantile(0.5)(x))`, `toFloat64(median(x))`, ... | [`SupportsQuantileAggregates`](xref:NextORM.Core.ISqlDialect.SupportsQuantileAggregates) | ClickHouse |
| Произвольное значение | `SqlFunctions.Sql.any_agg(x)` | `ANY_VALUE(x)` / `any(x)` | [`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate) | MySQL, ClickHouse |
| Последняя строка | `SqlFunctions.ClickHouse.any_last(x)` | `anyLast(x)` | [`SupportsAnyAggregates`](xref:NextORM.Core.ISqlDialect.SupportsAnyAggregates) | ClickHouse |
| С фильтром (`-If`) | `SqlFunctions.ClickHouse.count_if(() => p)`, `sum_if(x, () => p)`, `avg_if(x, () => p)`, `min_if(x, () => p)`, `max_if(x, () => p)` | `countIf(p)`, `sumIf(x, p)`, ... | [`SupportsIfAggregates`](xref:NextORM.Core.ISqlDialect.SupportsIfAggregates) | ClickHouse |
| Упорядоченные | `SqlFunctions.Postgres.percentile_cont(fraction, () => x)`, `percentile_disc(fraction, () => x)`, `mode(() => x)` | `percentile_cont(f) within group (order by x)`, ... | [`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates) | PostgreSQL |

MariaDB **не** поддерживает `any_agg`: в 10.4–12.x нет `ANY_VALUE` (возможность SQL-2023 `T626` всё ещё
ожидается, ориентир — 13.2), поэтому [`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate) равно `false`, а вызов отклоняется через `NotSupportedException`.

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

В ClickHouse те же семейства рендерятся по-своему — `groupBitAnd`, `covarPop`, `argMin`/`argMax` и
комбинаторы `-If` — а логические агрегаты и семейство `regr_*` недоступны (в ClickHouse нет ни
`bool_and`, ни `regr_*`, и диалект отклоняет их через `NotSupportedException`):

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

Упорядоченные агрегаты принимают ключ сортировки как цитируемую лямбду, которая замыкается на параметр
запроса; ключ становится `order by <key>`. Вызов такого агрегата у провайдера без соответствующей
возможности бросает `NotSupportedException`.

## Агрегаты с FILTER

Агрегат может нести предложение `filter (where ...)`, если передать предикат дополнительным аргументом.
Предложение включается флагом [`SupportsFilter`](xref:NextORM.Core.ISqlDialect.SupportsFilter) (его включают PostgreSQL и SQLite);
MySQL/MariaDB и SQL Server отклоняют его через `NotSupportedException`.

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

В ClickHouse аналог фильтрованного агрегата — комбинатор `-If` (`countIf`, `sumIf`, `avgIf`, `minIf`,
`maxIf`), доступный как `SqlFunctions.ClickHouse.count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` ([`SupportsIfAggregates`](xref:NextORM.Core.ISqlDialect.SupportsIfAggregates)).
ClickHouse не принимает ANSI-предложение `filter (where ...)`, поэтому обобщённый API фильтрованных
агрегатов там отклоняется.

`string_agg`/`array_agg` тоже принимают фильтр; `string_agg` доступен в PostgreSQL, SQL Server
2017+ и ClickHouse (как `arrayStringConcat(groupArray(x), delimiter)`), а `array_agg` — только в
PostgreSQL (в SQL Server нет типа-массива, а массивы ClickHouse пока не материализуются). Полная
поверхность — в разделе [Скалярные функции](11-scalar-functions.md#фильтр-агрегатов-filter).

## Различия между провайдерами

| Провайдер | `count_big` | Имена агрегатов | Целочисленный `AVG` | `var` / `varp` |
|---|---|---|---|---|
| SQLite | генерирует `count(*)` (уже 64-битный) | сохраняются как есть; `stdev`/`var` берутся из пользовательских агрегатов, регистрируемых провайдером | дробный (5.5 остаётся 5.5) | поддерживаются через эти пользовательские агрегаты |
| SQL Server | генерирует `count_big(...)` | сохраняются как есть; `stdev`/`var` встроенные | целочисленный (5.5 становится 5) | встроенные |
| PostgreSQL | генерирует `count(*)` (уже 64-битный) | `stdev` → `stddev`, `stdevp` → `stddev_pop`, `var` → `variance`, `varp` → `var_pop` | дробный | встроенные |
| MySQL | генерирует `count(*)` (уже 64-битный) | `stdev` → `stddev_samp`, `stdevp` → `stddev_pop`, `var` → `var_samp`, `varp` → `var_pop` | дробный | встроенные (`var_samp` / `var_pop`) |
| MariaDB | генерирует `count(*)` (уже 64-битный) | то же отображение, что в MySQL (`stddev_samp` / `stddev_pop` / `var_samp` / `var_pop`) | дробный | встроенные (`var_samp` / `var_pop`) |
| ClickHouse | оборачивает `count`/`count_distinct`/`count_if` в `toInt32(...)`, а `count_big`/`count_big_distinct` — в `toInt64(...)` (нативный `UInt64` не материализуется) | `stdev` → `stddevSamp`, `stdevp` → `stddevPop`, `var` → `varSamp`, `varp` → `varPop`, `covar_*` → `covarPop`/`covarSamp`, `bit_*` → `groupBit*`, `*_if` → `*If` | дробный | `varSamp` / `varPop` |
| In-memory | не покрыто набором тестов in-memory | не покрыто | не покрыто | не покрыто |

Порядок групп не определён стандартом SQL; проверяйте по ключу группы, а не по позиции. PostgreSQL и
SQLite различаются в порядке `NULL` (PostgreSQL — первыми, SQLite — последними).

`GROUP BY ROLLUP (...)`/`CUBE (...)` генерируется в ANSI-форме в SQL Server, PostgreSQL и SQLite, а в
виде хвостового `... WITH ROLLUP`/`WITH CUBE` — в MySQL/MariaDB и ClickHouse. В MySQL/MariaDB нет
`CUBE`, поэтому [`GroupByCube`](xref:NextORM.Core.EntityBuilder`1) там бросает исключение; провайдер in-memory отклоняет оба модификатора.
`GROUP BY GROUPING SETS (...)` доступен в SQL Server, PostgreSQL, SQLite и ClickHouse, но не в
MySQL/MariaDB и не в провайдере in-memory. `WITH TOTALS` — только ClickHouse; провайдер in-memory
отклоняет его так же, как `ROLLUP`/`CUBE`.

## См. также

- [Сортировка и постраничный вывод](05-sorting-and-paging.md) - [`OrderBy`](xref:NextORM.Core.EntityBuilder`1) по выражению или порядковому номеру.
- [Соединения](03-joins.md) - агрегат по соединённой проекции.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.GroupBy.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.Aggregates.cs:9`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:185`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:240`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:181`.
