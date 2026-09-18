# Группировка и агрегаты

> Группируйте строки с помощью `GroupBy`, фильтруйте группы с помощью `Having` и вычисляйте `count`, `min`, `max`, `avg`, `sum`, `stdev`, `var` и их `_distinct`-варианты через `NORM.SQL`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md) · [Сортировка и постраничный вывод](05-sorting-and-paging.md)

## Обзор

`EntityBuilder<T>.GroupBy(...)` добавляет предложение `GROUP BY`, а `EntityBuilder<T>.Having(...)` добавляет
предложение `HAVING`, которое фильтрует группы. Оба являются предложениями построителя, поэтому они
комбинируются с `Where`, `OrderBy`, `Limit`/`Page` и проекцией точно так же, как в любом другом
запросе:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .ToList();
```

Агрегатные функции находятся в `NORM.SQL` и имеют смысл только внутри проекции или предиката
`Having`. Они называются ровно так:

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

У распространённых агрегатов также есть удобные терминалы на `EntityBuilder<T>`: `Count()`, `Min(x)`,
`Max(x)`, `Avg(x)`, `Sum(x)`, `Stdev(x)`, `Stdevp(x)`, `Var(x)` и `Varp(x)`, каждый с `...Async`-двойником
и перегрузкой, принимающей позиционные параметры. У `count`, `count_big`, `count_distinct` и
`count_big_distinct` нет терминала на сущности, и они используются через `NORM.SQL`.

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

`Having` фильтрует группы после агрегирования, тогда как `Where` фильтрует строки до него:

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

`Where` и `Having` можно комбинировать в одном запросе; `Where` применяется до группировки:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Having(e => NORM.SQL.count() > 1)
    .Select(e => new { e.Int, count = NORM.SQL.count() })
    .ToList();
```

## Группировка с сортировкой и ограничением

Сгруппированная проекция сортируется и разбивается на страницы, как и любой другой запрос.
Сортировка по порядковому номеру выбранного столбца сохраняет запрос переносимым между
провайдерами:

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

Обратите внимание, что порядок `NULL` различается между провайдерами (PostgreSQL при сортировке по
убыванию ставит `NULL` первыми, SQLite — последними), поэтому в примере сортировки группа `NULL`
исключена.

## ROLLUP и CUBE

`GroupByRollup(...)` и `GroupByCube(...)` добавляют ANSI-модификаторы супер-агрегации. `ROLLUP`
добавляет каждую префиксную подытоговую строку и общий итог; `CUBE` — каждую комбинацию:

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

`ROLLUP` доступен на всех SQL-провайдерах; `CUBE` недоступен в MySQL/MariaDB. Провайдер in-memory не
поддерживает ни один из модификаторов и выбрасывает `NotSupportedException`.

## Агрегаты без группировки

Агрегат по всей таблице — это проекция без `GroupBy`:

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

`avg` сохраняет дробную часть в SQLite и PostgreSQL; SQL Server вычисляет `AVG` по целочисленному
столбцу как целое. Терминалы на сущности перенаправляют к тем же функциям:

```csharp
var min = dataContext.From<ISimpleEntity>().Min(e => e.Id);            // NORM.SQL.min
var max = await dataContext.From<ISimpleEntity>().MaxAsync(e => e.Id); // NORM.SQL.max
```

## Подсчёт

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

`count_big()` возвращает 64-битное количество. В SQL Server есть отдельная функция `count_big`, и он
генерирует `count_big(...)`; SQLite и PostgreSQL и так возвращают 64-битное целое из `count(...)`,
поэтому диалект генерирует `count(...)` для обоих.

Сокращение уровня построителя `EntityBuilder<T>.Count()` эквивалентно
`Select(e => NORM.SQL.count())` с последующим `First()`:

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

Формы `_distinct` добавляют `distinct`:

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

`stdev` — выборочное стандартное отклонение, `stdevp` — генеральное; `var`/`varp` — соответствующие
дисперсии. У каждой есть `_distinct`-форма (`stdev_distinct`, `stdevp_distinct`, `var_distinct`,
`varp_distinct`) и терминал на сущности (`Stdev`, `Stdevp`, `Var`, `Varp`) с синхронной,
`...Async` и параметризованной перегрузкой:

```csharp
var sample = dataContext.From<ISimpleEntity>().Stdev(e => e.Id);
var asyncSample = await dataContext.From<ISimpleEntity>().VarAsync(e => e.Id);
var sumAfter5 = dataContext.From<ISimpleEntity>()
    .Where(e => e.Id > NORM.Param<int>(0))
    .Sum(e => e.Id, 5);
```

Проецирование через `double` позволяет избежать потери точности при преобразовании целых чисел:

```csharp
var variance = dataContext.From<ISimpleEntity>().Select(x => NORM.SQL.varp((double)x.Id)).First();
```

## Логические, битовые, статистические и упорядоченные агрегаты

Помимо `count`/`min`/`max`/`sum`/`avg`/`stdev`/`var`, `NORM.SQL` предоставляет несколько семейств
агрегатов. Каждое семейство включается своим флагом диалекта; PostgreSQL и ClickHouse включают разные
подмножества.

| Семейство | C# | SQL | Флаг | Провайдеры |
|---|---|---|---|---|
| Логические | `NORM.SQL.bool_and(x)`, `bool_or(x)`, `every(x)` | `bool_and(x)`, ... | `SupportsBooleanAggregates` | PostgreSQL |
| Битовые | `NORM.SQL.bit_and(x)`, `bit_or(x)`, `bit_xor(x)` | `bit_and(x)` … / `groupBitAnd(x)` … | `SupportsBitAggregates` | PostgreSQL, ClickHouse |
| Статистические | `NORM.SQL.corr(y, x)`, `covar_pop(y, x)`, `covar_samp(y, x)` | `corr(y, x)`, ... / `covarPop(y, x)`, ... | `SupportsStatisticalAggregates` | PostgreSQL, ClickHouse |
| Регрессия | `NORM.SQL.regr_slope(y, x)`, `regr_intercept(y, x)`, `regr_r2(y, x)`, `regr_count(y, x)`, `regr_avgx(y, x)`, `regr_avgy(y, x)` | `regr_slope(y, x)`, ... | `SupportsRegressionAggregates` | PostgreSQL |
| ArgMin/ArgMax | `NORM.SQL.arg_min(value, by)`, `arg_max(value, by)` | `argMin(value, by)`, `argMax(value, by)` | `SupportsArgMinMax` | ClickHouse |
| С фильтром (`-If`) | `NORM.SQL.count_if(() => p)`, `sum_if(x, () => p)`, `avg_if(x, () => p)`, `min_if(x, () => p)`, `max_if(x, () => p)` | `countIf(p)`, `sumIf(x, p)`, ... | `SupportsIfAggregates` | ClickHouse |
| Упорядоченные | `NORM.SQL.percentile_cont(fraction, () => x)`, `percentile_disc(fraction, () => x)`, `mode(() => x)` | `percentile_cont(f) within group (order by x)`, ... | `SupportsOrderedAggregates` | PostgreSQL |

```csharp
var stats = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        AllTrue = NORM.SQL.bool_and(e.Boolean),
        Xor = NORM.SQL.bit_xor(e.Id),
        Correlation = NORM.SQL.corr(e.Id, e.Int),
        Median = NORM.SQL.percentile_cont(0.5, () => e.Id)
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
        Bits = NORM.SQL.bit_and(e.Id),
        Cov = NORM.SQL.covar_pop(e.Id, e.Int),
        FirstByMax = NORM.SQL.arg_max(e.String, e.Id),
        Positives = NORM.SQL.count_if(() => e.Id > 0L),
        PositiveSum = NORM.SQL.sum_if(e.Id, () => e.Id > 0L)
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
Предложение включается флагом `ISqlDialect.SupportsFilter` (его включают PostgreSQL и SQLite);
MySQL/MariaDB и SQL Server отклоняют его через `NotSupportedException`.

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

В ClickHouse аналог фильтрованного агрегата — комбинатор `-If` (`countIf`, `sumIf`, `avgIf`, `minIf`,
`maxIf`), доступный как `NORM.SQL.count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` (`SupportsIfAggregates`).
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
| ClickHouse | генерирует `count(*)` (уже 64-битный) | `stdev` → `stddevSamp`, `stdevp` → `stddevPop`, `var` → `varSamp`, `varp` → `varPop`, `covar_*` → `covarPop`/`covarSamp`, `bit_*` → `groupBit*`, `*_if` → `*If` | дробный | `varSamp` / `varPop` |
| In-memory | не покрыто набором тестов in-memory | не покрыто | не покрыто | не покрыто |

Порядок групп не определён стандартом SQL; проверяйте по ключу группы, а не по позиции. PostgreSQL и
SQLite различаются в порядке `NULL` (PostgreSQL — первыми, SQLite — последними).

`GROUP BY ROLLUP (...)`/`CUBE (...)` генерируется в ANSI-форме в SQL Server, PostgreSQL и SQLite, а в
виде хвостового `... WITH ROLLUP`/`WITH CUBE` — в MySQL/MariaDB и ClickHouse. В MySQL/MariaDB нет
`CUBE`, поэтому `GroupByCube` там бросает исключение; провайдер in-memory отклоняет оба модификатора.

## См. также

- [Сортировка и постраничный вывод](05-sorting-and-paging.md) - `OrderBy` по выражению или порядковому номеру.
- [Соединения](03-joins.md) - агрегат по соединённой проекции.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.GroupBy.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.Aggregates.cs:9`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:185`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:240`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:181`.
