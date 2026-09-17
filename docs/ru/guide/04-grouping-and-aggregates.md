# Группировка и агрегаты

> Группируйте строки с помощью `GroupBy`, фильтруйте группы с помощью `Having` и вычисляйте `count`, `min`, `max`, `avg`, `sum`, `stdev`, `var` и их `_distinct`-варианты через `NORM.SQL`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md) · [Сортировка и постраничный вывод](05-sorting-and-paging.md)

## Обзор

`Entity<T>.GroupBy(...)` добавляет предложение `GROUP BY`, а `Entity<T>.Having(...)` добавляет
предложение `HAVING`, которое фильтрует группы. Оба являются предложениями построителя, поэтому они
комбинируются с `Where`, `OrderBy`, `Limit`/`Page` и проекцией точно так же, как в любом другом
запросе:

```csharp
var rows = dataContext.Create<IComplexEntity>()
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

У распространённых агрегатов также есть удобные терминалы на `Entity<T>`: `Count()`, `Min(x)`,
`Max(x)`, `Avg(x)`, `Sum(x)`, `Stdev(x)`, `Stdevp(x)`, `Var(x)` и `Varp(x)`, каждый с `...Async`-двойником
и перегрузкой, принимающей позиционные параметры. У `count`, `count_big`, `count_distinct` и
`count_big_distinct` нет терминала на сущности, и они используются через `NORM.SQL`.

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

`Having` фильтрует группы после агрегирования, тогда как `Where` фильтрует строки до него:

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

`Where` и `Having` можно комбинировать в одном запросе; `Where` применяется до группировки:

```csharp
var rows = dataContext.Create<IComplexEntity>()
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

Обратите внимание, что порядок `NULL` различается между провайдерами (PostgreSQL при сортировке по
убыванию ставит `NULL` первыми, SQLite — последними), поэтому в примере сортировки группа `NULL`
исключена.

## Агрегаты без группировки

Агрегат по всей таблице — это проекция без `GroupBy`:

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

`avg` сохраняет дробную часть в SQLite и PostgreSQL; SQL Server вычисляет `AVG` по целочисленному
столбцу как целое. Терминалы на сущности перенаправляют к тем же функциям:

```csharp
var min = dataContext.Create<ISimpleEntity>().Min(e => e.Id);            // NORM.SQL.min
var max = await dataContext.Create<ISimpleEntity>().MaxAsync(e => e.Id); // NORM.SQL.max
```

## Подсчёт

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

`count_big()` возвращает 64-битное количество. В SQL Server есть отдельная функция `count_big`, и он
генерирует `count_big(...)`; SQLite и PostgreSQL и так возвращают 64-битное целое из `count(...)`,
поэтому диалект генерирует `count(...)` для обоих.

Сокращение уровня построителя `Entity<T>.Count()` эквивалентно
`Select(e => NORM.SQL.count())` с последующим `First()`:

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

Формы `_distinct` добавляют `distinct`:

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

`stdev` — выборочное стандартное отклонение, `stdevp` — генеральное; `var`/`varp` — соответствующие
дисперсии. У каждой есть `_distinct`-форма (`stdev_distinct`, `stdevp_distinct`, `var_distinct`,
`varp_distinct`) и терминал на сущности (`Stdev`, `Stdevp`, `Var`, `Varp`) с синхронной,
`...Async` и параметризованной перегрузкой:

```csharp
var sample = dataContext.Create<ISimpleEntity>().Stdev(e => e.Id);
var asyncSample = await dataContext.Create<ISimpleEntity>().VarAsync(e => e.Id);
var sumAfter5 = dataContext.Create<ISimpleEntity>()
    .Where(e => e.Id > NORM.Param<int>(0))
    .Sum(e => e.Id, 5);
```

Проецирование через `double` позволяет избежать потери точности при преобразовании целых чисел:

```csharp
var variance = dataContext.Create<ISimpleEntity>().Select(x => NORM.SQL.varp((double)x.Id)).First();
```

## Различия между провайдерами

| Провайдер | `count_big` | Имена агрегатов | Целочисленный `AVG` | `var` / `varp` |
|---|---|---|---|---|
| SQLite | генерирует `count(*)` (уже 64-битный) | сохраняются как есть; `stdev`/`var` берутся из пользовательских агрегатов, регистрируемых провайдером | дробный (5.5 остаётся 5.5) | поддерживаются через эти пользовательские агрегаты |
| SQL Server | генерирует `count_big(...)` | сохраняются как есть; `stdev`/`var` встроенные | целочисленный (5.5 становится 5) | встроенные |
| PostgreSQL | генерирует `count(*)` (уже 64-битный) | `stdev` → `stddev`, `stdevp` → `stddev_pop`, `var` → `variance`, `varp` → `var_pop` | дробный | встроенные |
| In-memory | не покрыто набором тестов in-memory | не покрыто | не покрыто | не покрыто |

Порядок групп не определён стандартом SQL; проверяйте по ключу группы, а не по позиции. PostgreSQL и
SQLite различаются в порядке `NULL` (PostgreSQL — первыми, SQLite — последними).

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
