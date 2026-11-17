# Группировка и агрегаты

> Группируйте строки с помощью [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), фильтруйте группы с помощью [`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) и вычисляйте `count`, `min`, `max`, `avg`, `sum`, `stdev`, `var` и их `_distinct`-варианты через [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql).

**Предварительные требования:** [Запросы и проекции](../engine/index.md) · [Фильтрация (WHERE)](03-filtering-where.md) · [Сортировка и постраничный вывод](06-sorting-and-paging.md)

## Обзор

Используйте `EntityBuilder<T>.GroupBy(...)`, чтобы сгруппировать строки по ключу. Добавьте
`EntityBuilder<T>.Having(...)`, чтобы оставить только группы, удовлетворяющие условию на агрегат.
Оба метода добавляют SQL-предложения к одному построителю, поэтому их можно сочетать с
[`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})),
[`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)), [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)) и проекцией:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

Запрос возвращает по одной строке на каждое различное значение `Int`. `count()` показывает, сколько
исходных строк попало в каждую группу.

Выберите форму запроса по нужному результату:

- Для сводной строки на каждый ключ используйте [`GroupBy`](#groupby) и проекцию с агрегатом.
- Чтобы отсеять группы после агрегирования, добавьте [`Having`](#having).
- Для одного значения по всему источнику опустите `GroupBy`; см. раздел
  [«Агрегаты без группировки»](#агрегаты-без-группировки).
- Чтобы вернуть строку с максимальным или минимальным значением, а не сводку, см.
  [«Выбор экстремальной строки»](#выбор-экстремальной-строки-selectwheremax--selectwheremin).
- Агрегаты и синтаксис, доступные только у отдельных провайдеров, описаны в разделах
  [«Логические, битовые, статистические и упорядоченные агрегаты»](#логические-битовые-статистические-и-упорядоченные-агрегаты)
  и [«Различия между провайдерами»](#различия-между-провайдерами).

Агрегатные функции доступны через [`Sql`](xref:NextORM.Core.SqlFunctions.Sql). Используйте их в проекции
или предикате [`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})):

| Функция | Результат | Функция | Результат |
|---|---|---|---|
| `count()` | `int` | `count_distinct(...)` | `int` |
| `count_big()` | `long` | `count_big_distinct(...)` | `long` |
| `min(x)` | совпадает с `x` | `avg_distinct(x)` | как `avg(x)` |
| `max(x)` | совпадает с `x` | `sum_distinct(x)` | совпадает с `x` |
| `avg(x)` | `double?` / `float?` / `decimal?` (по типу входа) | `stdev_distinct(x)` | совпадает с `x` |
| `sum(x)` | совпадает с `x` | `stdevp_distinct(x)` | совпадает с `x` |
| `stdev(x)` | совпадает с `x` | `var_distinct(x)` | совпадает с `x` |
| `stdevp(x)` | совпадает с `x` | `varp_distinct(x)` | совпадает с `x` |
| `var(x)` | совпадает с `x` | `varp(x)` | совпадает с `x` |

При вызове без аргументов `count()` / `count_big()` считают строки (`count(*)`). Если передать одно
или несколько выражений, они считают только значения, отличные от `NULL`. Остальные функции принимают
выражение как аргумент. Суффикс `_distinct` добавляет `distinct` внутрь круглых скобок.

Распространённые агрегаты можно вызвать напрямую методами `EntityBuilder<T>`:
[`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})), `Min(x)`, `Max(x)`,
`Avg(x)`, `Sum(x)`, `Stdev(x)`, `Stdevp(x)`, `Var(x)` и `Varp(x)`. Эти методы выполняют запрос и
возвращают значение агрегата. Для каждого есть синхронная и `...Async`-перегрузки, а также перегрузка
с позиционными параметрами. Используйте [`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})) для подсчёта строк. Если агрегат нужно включить в проекцию или предикат `Having`, используйте [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql). Прямой метод есть не для каждого SQL-агрегата.

Если данные распределены по бакетам, `AcrossBuckets()` выполняет запрос по одному разу на каждый бакет и
возвращает отдельный вектор результатов для каждого из них. `GroupBy` и агрегаты считаются **внутри**
каждого бакета; между бакетами результаты не объединяются и не агрегируются повторно. См.
[«Шардирование и горизонтальное партиционирование»](35-sharding-and-horizontal-partitioning.md).

## GroupBy

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

```sql
select nullableint as 'Int', count(*)
 from complex_entity group by nullableint
```

Для одной группирующей колонки анонимная обёртка необязательна: можно передать скалярное выражение
напрямую. `GroupBy(e => e.Int)` и `GroupBy(e => new { e.Int })` дают один и тот же SQL и делят один
план запроса, поэтому формы взаимозаменяемы:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => e.Int)
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`).

Вывод:

| Int | count |
|-----|-------|
| null | 1 |
| 1 | 2 |

## Having

[`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) фильтрует группы после агрегирования, тогда как [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) фильтрует строки до него:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Having(e => SqlFunctions.Sql.count() > 1)
    .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
    .ToList();
```

```sql
select nullableint as 'Int', count(*)
 from complex_entity group by nullableint having (count(*) > 1)
```

Вывод:

| Int | count |
|-----|-------|
| 1 | 2 |

[`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) и [`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) можно комбинировать в одном запросе; [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) применяется до группировки:

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
select nullableint as 'Int', count(*)
 from complex_entity group by nullableint order by 2 limit 1
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

## Типы результата и аргументы AVG

`avg` не возвращает тип своего аргумента: для целочисленного столбца результат дробный, поэтому
`avg` по `[1, 2]` даёт `1.5`. Раньше результат был целым числом (`2`, а в SQL Server — `1`, поскольку
его целочисленный `AVG` отбрасывает дробную часть). На всех провайдерах результат допускает `null`,
поэтому пустой вход или вход, где все значения равны `NULL`, возвращает `null`.

| Вход | Результат `avg(x)` |
|---|---|
| `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` и их nullable-формы | `double?` |
| `float`, `float?` | `float?` |
| `double`, `double?` | `double?` |
| `decimal`, `decimal?` | `decimal?` |

Способ обработки аргумента зависит от провайдера. Для SQL Server nextorm преобразует аргумент без
плавающей точки в `float` внутри `AVG`, чтобы результат сохранял дробную часть. Остальные диалекты
передают исходный аргумент. Это правило действует для `avg`, `avg_distinct` и оконного `avg_over`:

| Провайдер | Аргумент в SQL |
|---|---|
| SQLite, PostgreSQL, MySQL, MariaDB, ClickHouse | `avg(id)` |
| SQL Server | `avg(cast(id as float))` |

`Avg` возвращает nullable-результат `double?` (или `float?`/`decimal?` для соответствующих входов).
Сохраняйте этот тип или явно преобразуйте значение после проверки на `null`; присваивание результата
переменной типа `int` не компилируется:

```csharp
int average = dataContext.From<ISimpleEntity>().Avg(e => e.Id);        // не компилируется
var average = dataContext.From<ISimpleEntity>().Avg(e => e.Id);        // double?
var direct  = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.avg(e.Id)).First(); // double?
```

Позвольте перегрузке выбрать объявление по типу селектора, а не указывайте generic-аргументы явно.

## Группировка, выведенная из проекции: SelectGroup

`SelectGroup` поставляется в пакете `NextORM.Extensions` (`using NextORM.Extensions;`). Он принимает ту же лямбду проекции, что и `Select`, но вместо явного `GroupBy` выводит
ключи группировки из самой проекции. Каждое зависящее от строки выражение без агрегата становится
ключом, а агрегатные вызовы сворачивают каждую группу. Проекция сохраняет ровно свою форму — нет ни
клиентской группировки, ни динамического типа, ни reflection-emit слияния плоского типа результата.
Проекция типизирована по исходной сущности: указывайте оба generic-аргумента
(`.SelectGroup<TEntity, TResult>(...)`) или опускайте их. Квартальный пример:

```csharp
ctx.From<Product>()
   .SelectGroup(p => new
   {
       ProductID        = p.Item1.ProductId,
       ProductName      = p.Item3.Name,
       SalesQuarter     = SqlFunctions.Sql.date_trunc("quarter", p.Item2.OrderDate),
       QuarterlyRevenue = SqlFunctions.Sql.sum(p.Item1.LineTotal),
       QuarterlyQty     = SqlFunctions.Sql.sum(p.Item1.OrderQty),
   });
```

`ProductID`, `ProductName` и весь вызов `date_trunc('quarter', OrderDate)` становятся ключами
`GROUP BY`, а два вызова `sum` остаются агрегатами. Результаты и SQL эквивалентны явной форме
`GroupBy(...).Select(...)` с теми же ключами. Компактный пример и его SQL:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .SelectGroup(e => new { e.Int, Total = SqlFunctions.Sql.sum(e.Id) })
    .ToList();
```

```sql
select nullableint as 'Int', sum(id) as 'Total'
 from complex_entity
 group by nullableint
```

Единственным ключом стал `nullableint`, а `sum(id)` остался агрегатом — явный `GroupBy` не нужен.

Правила вывода:

* **Максимальные зависящие от строки подвыражения.** Анализатор собирает наибольшее подвыражение
  каждого выходного значения, не содержащее агрегата и зависящее от текущей строки, а не спускается
  до отдельных столбцов.
* **Порядок первого появления.** Ключи выводятся в порядке первого появления их выражений в
  проекции, поэтому сгенерированный `GROUP BY` детерминирован.
* **Константы и захваченные значения не являются ключами.** Литерал или захваченная локальная
  переменная не группирует и не сдвигает порядок ключей.
* **Одинаковые выражения группировки не дублируются.** Два структурно равных выражения дают один ключ.
* **Целая скалярная функция остаётся одним ключом.** Ключом является
  `date_trunc('quarter', OrderDate)`, «сырой» `OrderDate` рядом не добавляется.
* **Гранулярность подвыражений сохраняется.** `sum(Amount) + Adjustment` выводит `Adjustment`;
  `sum(Amount) + Price * Qty` выводит `Price * Qty`, а не `Price` и `Qty` по отдельности.
* **Агрегатные вызовы не влияют на ключи.** Аргументы агрегата, его `DISTINCT`-вход и его
  `FILTER`-предикат не добавляются к ключам группировки.
* **Проекция сохраняется.** SQL для исходной проекции генерируется без изменений: ни один ключ не
  переписывается, ни одно значение не сворачивается на клиенте, форма результата не меняется.

Вырожденные проекции разрешаются без изобретения группировки:

* **Ключи без агрегатов** возвращают уникальные комбинации ключей через `GROUP BY`; это не
  построчный select.
* **Агрегаты без ключей** — глобальная агрегация: без `GROUP BY`, ровно одна строка, а существующее
  поведение провайдера на пустом входе для `count`, `sum` и `NULL` сохраняется.
* **Не зависящая от строки проекция без агрегатов** ведёт себя как DISTINCT: каждое различное
  значение возвращается один раз, а пустой источник даёт ноль строк вместо невалидного пустого
  `GROUP BY`.

`Having` и expression-`OrderBy` валидируются против выведенных ключей: каждое упомянутое в них
выражение строки должно быть покрыто выведенным ключом, быть агрегатом либо захваченным/константным
значением. Непокрытый член строки бросает `QueryPreparationException` до того, как команда дойдёт до
базы данных, — группировка никогда не расширяется молча, а функциональные зависимости не выводятся.
Поэтому захваченный порог допустим, а «голый» столбец внешней строки — нет. `OrderBy` по порядковому
номеру сохраняет существующую проверку границ и не добавляет ключей. `Where` остаётся
pre-group-фильтром и не участвует в выводе.

Следующие комбинации бросают `QueryPreparationException`:

* уже существующий явный `GroupBy`, `GroupByRollup`, `GroupByCube`, `GroupByGroupingSets` или
  `WithTotals` — два механизма группировки никогда не сливаются и не переопределяют друг друга;
* window-функции в проекции, `Having` или expression-`OrderBy`;
* скалярные или коррелированные подзапросы в этих выражениях, в том числе внутри аргументов
  агрегатов и внутри `FILTER`-предиката. Это отклонение безусловно и не зависит от вывода ключей;
  подзапрос в `Where` по-прежнему поддерживается.

Провайдер in-memory не поддерживается: оба оператора бросают `NotSupportedException` ещё до построения
плана, поэтому выведенная группировка всегда выполняется на реальной базе данных.

### SelectGroupHaving

`SelectGroupHaving(projection, condition)` использует тот же вывод ключей, что и `SelectGroup`, и
добавляет `condition` в эффективный `HAVING` через `AND`. `condition` типизирован по результату
проекции (`TResult`), а не по исходной сущности: каждый член результата перед валидацией заменяется
выражением проекции, которое его производит, поэтому `r => r.Rows > 1` — это тот же `HAVING`, что и
нижележащее `count(...) > 1`, а при скалярной проекции параметр условия — само спроецированное значение
(`f => f > 5`). Ключи выводятся **только** из `projection`; `condition` никогда не добавляет ключей.
Существующий `Having` источника объединяется с `condition` через `AND` до валидации. Член условия,
который проекция не связывает (или результат целиком), а также непокрытое ссылкой на строку выражение в
`projection` или в эффективном `HAVING` завершаются `QueryPreparationException` до обращения к БД. Три
исхода (ключевая группировка, глобальная агрегация, проекция-константа) те же, что и у `SelectGroup`.

Пример: `r => r.Count > 1` становится `HAVING` по нижележащему `count(*)`, а ключ по-прежнему
выводится только из проекции:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .SelectGroupHaving(
        e => new { e.Int, Count = SqlFunctions.Sql.count() },
        r => r.Count > 1)
    .ToList();
```

```sql
select nullableint as 'Int', count(*) as 'Count'
 from complex_entity
 group by nullableint
 having (count(*) > 1)
```

Оба оператора бросают `NotSupportedException` (сообщение содержит `in-memory`) при выполнении на
`InMemoryDataContext`.

Упакованные операторы сами являются обычными компонентами запроса на публичном контракте
[`IQueryComponent`](xref:NextORM.Core.IQueryComponent): сторонний пакет может добавить свой структурный
оператор тем же способом — без friend-доступа, реестра и сырого SQL. См.
[Расширение построения запросов компонентами запроса](../advanced/extensibility.md#расширение-построения-запросов-компонентами-запроса).

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
select nullableint as 'Int', b as 'Boolean', count(*)
 from complex_entity group by rollup (nullableint, b)
-- MySQL / MariaDB / ClickHouse
select nullableint as 'Int', b as 'Boolean', count(*)
 from complex_entity group by nullableint, b with rollup
```

`ROLLUP` доступен на всех SQL-провайдерах; `CUBE` недоступен в MySQL/MariaDB. Провайдер in-memory не
поддерживает ни один из модификаторов и выбрасывает `NotSupportedException`.

## WITH TOTALS

В ClickHouse можно добавить `WITH TOTALS` ([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals)) вызовом
`.WithTotals()` после группировки. Запрос возвращает строки групп и одну строку с общими итогами.
Модификатор сочетается с `ROLLUP`/`CUBE`, но не с `GROUPING SETS`. Официальный `ClickHouse.Driver` не
передаёт строку итогов через свой API, поэтому nextorm добавляет `WITH TOTALS` в SQL, но читает только
строки групп.

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
select nullableint as 'Int', b as 'Boolean', count(*)
 from complex_entity group by grouping sets ((nullableint, b), (nullableint), ())
```

Grouping sets доступны в SQL Server, PostgreSQL, SQLite и ClickHouse
([`SupportsGroupingSets`](xref:NextORM.Core.ISqlDialect.SupportsGroupingSets)); MySQL/MariaDB и провайдер in-memory выбрасывают
`NotSupportedException`. Индекс вне диапазона выбрасывает [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException).

## `DISTINCT ON` — одна строка на ключ

PostgreSQL-конструкция `DISTINCT ON` — «выбирающая строку» пара к группировке: вместо свёртки каждого
ключа в агрегаты она ключует строки так же, как `GROUP BY`, но оставляет по одной представительной
строке на ключ, выбранной через `ORDER BY`. Используйте [`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})) вместо [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), когда нужна сама строка, а не агрегат по ней:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring
 from complex_entity order by somestring
```

Ключом может быть анонимный тип — тогда ключ составляется из нескольких колонок; ведущие выражения
`ORDER BY` должны ему соответствовать. [`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})) нельзя комбинировать с [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct), и реализует его
только PostgreSQL — остальные провайдеры отклоняют его на этапе построения SQL. Подробнее см.
раздел [SELECT DISTINCT](09-distinct.md#distinct-on-postgresql).

## Выбор экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)

> **Имена операторов.** Встроенные операторы — `SelectWhereMinOld` / `SelectWhereMaxOld`; они покрывают
> путь совместимости, включая провайдер in-memory. `SelectWhereMin` / `SelectWhereMax` из
> [пакета расширений](../advanced/extensions-package.md) построены на публичном слое компонентов запроса.

[`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMaxOld``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) и
[`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMinOld``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) оставляют строку (или строки),
значение которой по селектору максимально или минимально в источнике. Это фильтр строк, а не
агрегат: выжившая строка сохраняет все колонки.

**Глобально и по группам.** Без ключа группировки экстремум берётся по всему источнику; с ключом
группировки экстремальные строки выбираются внутри каждой отдельной группы. Ключ `null` — это
отдельная группа.

```csharp
// Глобально: единственная строка с максимальным score.
var top = dataContext.From<ExtremaEntity>()
    .SelectWhereMax(e => e.Score)
    .ToList();

// По группам: одна экстремальная строка на категорию.
var perGroup = dataContext.From<ExtremaEntity>()
    .SelectWhereMax(e => e.Score, ExtremeRowTies.One, e => e.Category)
    .ToList();
```

```sql
-- PostgreSQL
-- глобально: целочисленный ключ проходит нативно — ORDER BY ... LIMIT 1
select t1.id, t1.score, t1.category, t1.label
 from (select *
 from extrema_entity
 where score is not null order by "score" desc limit 1) as "t1"

-- по группам: строковый ключ группировки не проходит нативно, остаётся переносимое оконное понижение
select t1.id, t1.score, t1.category, t1.label
 from (select *, row_number() over (partition by category order by score desc) as "__nextorm_rn"
 from extrema_entity
 where score is not null) as "t1"
 where t1."__nextorm_rn" = 1
```

[`ExtremeRowTies`](xref:NextORM.Core.ExtremeRowTies) управляет обработкой совпадающих значений. [`One`](xref:NextORM.Core.ExtremeRowTies.One) (по умолчанию)
оставляет одну строку — при равенстве значений провайдер выбирает одну из них, — а [`All`](xref:NextORM.Core.ExtremeRowTies.All)
оставляет все строки, совпадающие по экстремальному значению. Совпадение определяется только
селектором, поэтому результат `All` содержит все такие строки, а `One` — ровно одну произвольную.

**Форма с проекцией.** Перегрузки
[`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMaxOld``2(System.Linq.Expressions.Expression{System.Func{`0,``0}},System.Linq.Expressions.Expression{System.Func{`0,``1}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) /
[`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMinOld``2(System.Linq.Expressions.Expression{System.Func{`0,``0}},System.Linq.Expressions.Expression{System.Func{`0,``1}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) принимают проекцию и применяют
её к каждой выжившей строке, возвращая типизированную команду запроса:

```csharp
var projected = dataContext.From<ExtremaEntity>()
    .SelectWhereMax(e => e.Score, e => new { e.Id, e.Label })
    .ToList();
```

```sql
-- PostgreSQL
select t1.id, t1.label
 from (select *
 from extrema_entity
 where score is not null order by "score" desc limit 1) as "t1"
```

**Источник с маппингом.** Перегрузки без проекции возвращают строки сущности целиком, поэтому источник
должен быть типом с маппингом (`From<TEntity>()` или другой источник, несущий метаданные сущности).
У именованного «сырого» источника вроде `From("orders")` нет маппированных колонок для материализации,
поэтому подготовка отклоняет его через `BuildSqlCommandException`
("SelectWhereMax/SelectWhereMin requires a mapped entity source.") вместо вводящей в заблуждение ошибки
проекции. Для «сырого» именованного источника используйте перегрузки с проекцией выше — форму
результата задаёт проекция, — если диалект поддерживает операцию.

**Семантика NULL.** Сравниваемые значения `null` игнорируются, поэтому они никогда не выигрывают
экстремум и не попадают в результат. Группа, у которой сравниваемое значение всегда `null`, не даёт
ни одной строки, а глобальная форма возвращает пустой результат, когда все значения `null`.

**Как понижается запрос.** `SelectWhereMax`/`SelectWhereMin` — это синтаксический сахар на стороне
SQL: логический запрос понижается внутри общего конвейера, и форма зависит только от политики
совпадений.

[`One`](xref:NextORM.Core.ExtremeRowTies.One) строит производную таблицу с оконной функцией —
`row_number()` над `(partition by <группа> order by <значение> [desc])` — с фильтром по `1`, а условие
источника объединяется через `AND` с фильтром `is not null` по значению. Для поддерживаемой формы
запроса PostgreSQL и ClickHouse автоматически используют собственную стратегию `One` — см.
[Нативные стратегии выбора экстремальной строки](../advanced/select-where-extrema-native.md). Для
других провайдеров и форм остаётся переносимая версия с оконной функцией.

[`All`](xref:NextORM.Core.ExtremeRowTies.All) понижается до **агрегатной** формы. Ранжирующее окно не
используется никогда: в запросе с `All` нет ни `rank()`, ни `row_number()`, ни оконного
`over (...)`, — за одним исключением: ключ экстремума с плавающей точкой на ClickHouse сохраняет
ранжирующее окно, потому что `max()`/`min()` этого сервера считают `NaN` экстремальным значением,
а `ORDER BY` ставит `NaN` последним, и эталоном контракта служит именно окно. По группам это
`GROUP BY <группа>` с выбором экстремального значения (и null-safe ключа
группы) и соединением обратно с источником; глобально — одна скалярная агрегатная подзапрос-выборка,
соединённая обратно; составной ключ разрешается покомпонентно, лексикографически — экстремум каждого
следующего компонента берётся по строкам, уже совпавшим с выбранным префиксом, — поэтому `(9, 2)`
побеждает `(8, 100)`.

```sql
-- All, глобально: один скалярный агрегат, соединённый обратно с источником
select t1.id, t1.score, t1.category, t1.label
 from extrema_entity as "t1"
 join (select max(score) as "__nextorm_ext_1_0"
 from extrema_entity) as "t2" on t1.score = t2."__nextorm_ext_1_0"

-- All, по группам: null-safe ключ группы и экстремальное значение, сгруппированные и соединённые обратно
select t1.id, t1.score, t1.category, t1.label
 from extrema_entity as "t1"
 join (select category as "__nextorm_ext_1_1", max(score) as "__nextorm_ext_1_0"
 from extrema_entity
 group by category) as "t2" on ((t1.category = t2."__nextorm_ext_1_1" or (t1.category is null and t2."__nextorm_ext_1_1" is null)) and t1.score = t2."__nextorm_ext_1_0")

-- One, по группам (переносимая форма): ранжируем строки источника, затем оставляем ранг 1
select t1.id, t1.score, t1.category, t1.label
 from (select *, row_number() over (partition by category order by score desc) as "__nextorm_rn"
 from extrema_entity
 where score is not null) as "t1"
 where t1."__nextorm_rn" = 1
```

Обе формы сохраняют условие источника от вызывающего кода и семантику `null`: сравнимое значение
`null` никогда не выигрывает экстремум. Оконная форма фильтрует `is not null` перед ранжированием;
агрегатная форма полагается на то, что агрегат и сравнение ключа отбрасывают `null` сами, а при
nullable-ключе рендерит тот же фильтр `is not null` внутри агрегата:

```sql
-- All, глобально, nullable-ключ: агрегат сам отфильтровывает null
select t1.id, t1.score, t1.category
 from extrema_nullable as "t1"
 join (select max(score) as "__nextorm_ext_1_0"
 from extrema_nullable
 where score is not null) as "t2" on t1.score = t2."__nextorm_ext_1_0"
```

Обе формы порождаются одним и тем же шагом конвейера, поэтому контракт результата выше — целые
строки сущности, перегрузки с проекцией, требование отображённого источника и семантика `null` —
одинаков для любой политики совпадений и любого SQL-провайдера (SQLite, SQL Server, PostgreSQL, MySQL,
MariaDB и ClickHouse). Провайдер in-memory вычисляет тот же контракт напрямую своим перечислителем и
SQL не рендерит вовсе.

Запрос отклоняется на этапе построения SQL при сочетании с другим модификатором формы строк, который
не выражается через производную таблицу: [`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})), [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), соединение, `Having`,
именованные окна, `LimitBy`, `ArrayJoin`, `PreWhere`, постраничный вывод, операция над множествами
или нефизический источник.

## Агрегаты без группировки

Агрегат по всей таблице — это проекция без [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

```csharp
var count = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.count()).First();
var sum   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.sum(e.Id)).First();
var avg   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.avg(e.Id)).First();
```

```sql
select count(*)
 from simple_entity
```

Вывод:

| Count |
|-------|
| 10 |

```sql
select sum(id)
 from simple_entity
```

Вывод:

| Sum |
|-----|
| 55 |

`avg` возвращает дробное nullable-значение для любого числового входа (см.
[типы результата и аргументы AVG](#типы-результата-и-аргументы-avg)), поэтому `[1, 2]` даёт `1.5`
на любом провайдере, включая SQL Server. Для распространённых агрегатов есть методы, которые сразу
выполняют запрос и возвращают результат: `Sum`, `Avg`, `Min`, `Max` и `Count`. Вызовы `Sum`, `Avg`,
`Min` и `Max` эквивалентны `Select(...)` с последующим `First()`, а `Count()` — вызову
`Select(e => SqlFunctions.Sql.count()).First()`:

```csharp
var sum   = dataContext.From<ISimpleEntity>().Sum(e => e.Id);            // SqlFunctions.Sql.sum
var avg   = dataContext.From<ISimpleEntity>().Avg(e => e.Id);            // SqlFunctions.Sql.avg
var min   = dataContext.From<ISimpleEntity>().Min(e => e.Id);            // SqlFunctions.Sql.min
var max   = await dataContext.From<ISimpleEntity>().MaxAsync(e => e.Id); // SqlFunctions.Sql.max
var count = dataContext.From<ISimpleEntity>().Count();                   // SqlFunctions.Sql.count
```

У каждого из этих методов есть синхронная, `...Async` и параметризованная перегрузки. Подробнее о
`Count()` см. в разделе [Подсчёт](#подсчёт).

## Подсчёт

```csharp
var all       = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.count()).First();             // count(*)
var nonNull   = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.count(e.Int)).First();       // count(nullableint)
var distinct  = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.count_distinct(e.Int)).First();
var big       = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.count_big()).First();         // long
var bigDist   = dataContext.From<IComplexEntity>().Select(e => SqlFunctions.Sql.count_big_distinct(e.Int)).First();
```

```sql
select count(*)
 from simple_entity
select count(nullableint)
 from complex_entity
select count(distinct nullableint)
 from complex_entity
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
`count`/`count_distinct` (и фильтрованный `countIf`) в `toInt32(...)`, а `count_big`/`count_big_distinct` — в
`toInt64(...)`.

Сокращение уровня построителя `EntityBuilder<T>.Count()` эквивалентно
`Select(e => SqlFunctions.Sql.count())` с последующим [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})):

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
select min(id)
 from simple_entity
select max(id)
 from simple_entity
select avg(id)
 from simple_entity
select sum(id)
 from simple_entity
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
| 5.5 |

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
select avg(distinct nullableint)
 from complex_entity
select sum(distinct nullableint)
 from complex_entity
```

## stdev / stdevp / var / varp

```csharp
var stdev  = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.stdev(e.Id)).First();
var stdevp = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.stdevp(e.Id)).First();
var var    = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.var(e.Id)).First();
var varp   = dataContext.From<ISimpleEntity>().Select(e => SqlFunctions.Sql.varp(e.Id)).First();
```

```sql
select stdev(id)
 from simple_entity
select stdevp(id)
 from simple_entity
select var(id)
 from simple_entity
select varp(id)
 from simple_entity
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

`stdev` — выборочное стандартное отклонение, `stdevp` — для всей совокупности; `var`/`varp` вычисляют
соответствующую дисперсию. У каждой функции есть `_distinct`-форма (`stdev_distinct`,
`stdevp_distinct`, `var_distinct`, `varp_distinct`) и прямой метод на `EntityBuilder`:
[`Stdev`](xref:NextORM.Core.EntityBuilderExtensions.Stdev``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})), [`Stdevp`](xref:NextORM.Core.EntityBuilderExtensions.Stdevp``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})), [`Var`](xref:NextORM.Core.EntityBuilderExtensions.Var``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})) и [`Varp`](xref:NextORM.Core.EntityBuilderExtensions.Varp``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})). У каждого метода есть синхронная, `...Async` и параметризованная перегрузки:

```csharp
var sample = dataContext.From<ISimpleEntity>().Stdev(e => e.Id);
var asyncSample = await dataContext.From<ISimpleEntity>().VarAsync(e => e.Id);
var sumAfter5 = dataContext.From<ISimpleEntity>()
    .Where(e => e.Id > SqlFunctions.Parameter<int>(0))
    .Sum(e => e.Id, 5);
```

Преобразование значения в `double` перед агрегированием предотвращает потерю точности при работе с
целыми числами:

```csharp
var variance = dataContext.From<ISimpleEntity>().Select(x => SqlFunctions.Sql.varp((double)x.Id)).First();
```

### Nullable-результаты и кардинальность

Методы `Stdev`, `Stdevp`, `Var`, `Varp` и восемь форм фасада `Sql.stdev*`/`Sql.var*` возвращают `T?`,
поскольку для некоторых входов результат не определён. Число значений, участвующих в вычислении, —
это количество непустых значений после `DISTINCT`:

| Вносящих вклад значений | `Stdev` / `Var` (выборочные) | `Stdevp` / `Varp` (генеральные) |
|---|---|---|
| 0 | `null` | `null` |
| 1 | `null` | `0.0` |
| 2 и более | установленная формула | установленная формула |

Определённый ноль остаётся нулём: два и более одинаковых значения дают `0.0`, а генеральный
результат по одной строке — `0.0`. `null` возвращается только для неопределённого результата и
никогда не подменяется придуманным нулём.

Результат имеет тип `Nullable<T>`: сохраняйте его, например как `double?`, или явно преобразуйте в
обычный тип после проверки на `null`. Присваивание результата в non-nullable `int`/`double` не
компилируется. Используйте `?? 0`, только если ноль действительно означает «нет разброса»: иначе
оператор заменит нулём неопределённый результат.

## Логические, битовые, статистические и упорядоченные агрегаты

Помимо `count`/`min`/`max`/`sum`/`avg`/`stdev`/`var`, [`Sql`](xref:NextORM.Core.SqlFunctions.Sql) предоставляет несколько семейств
агрегатов, а провайдерно-специфичные находятся в [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (логические, битовые, регрессионные и
упорядоченные) и [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (`arg_min`/`arg_max`, агрегаты числа уникальных
`uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12`, параметрическое семейство квантилей
`quantile`/`quantile_exact`/`quantile_timing`/`quantiles`/`median`, агрегаты наиболее частых значений
`top_k`/`top_k_weighted` и агрегаты
последовательностей/воронки `window_funnel`/`sequence_match`/`retention`). Каждое семейство включается
своим флагом диалекта; PostgreSQL и ClickHouse включают разные подмножества.

| Семейство | C# | SQL | Флаг | Провайдеры |
|---|---|---|---|---|
| Логические | `SqlFunctions.Postgres.bool_and(x)`, `bool_or(x)`, `every(x)` | `bool_and(x)`, ... | [`SupportsBooleanAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBooleanAggregates) | PostgreSQL |
| Битовые | `SqlFunctions.Postgres.bit_and(x)`, `bit_or(x)`, `bit_xor(x)` | `bit_and(x)` … / `groupBitAnd(x)` … | [`SupportsBitAggregates`](xref:NextORM.Core.ISqlDialect.SupportsBitAggregates) | PostgreSQL, ClickHouse |
| Статистические | `SqlFunctions.Sql.corr(y, x)`, `covar_pop(y, x)`, `covar_samp(y, x)` | `corr(y, x)`, ... / `covarPop(y, x)`, ... | [`SupportsStatisticalAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStatisticalAggregates) | PostgreSQL, ClickHouse |
| Регрессия | `SqlFunctions.Postgres.regr_slope(y, x)`, `regr_intercept(y, x)`, `regr_r2(y, x)`, `regr_count(y, x)`, `regr_avgx(y, x)`, `regr_avgy(y, x)` | `regr_slope(y, x)`, ... | [`SupportsRegressionAggregates`](xref:NextORM.Core.ISqlDialect.SupportsRegressionAggregates) | PostgreSQL |
| ArgMin/ArgMax | `SqlFunctions.ClickHouse.arg_min(value, by)`, `arg_max(value, by)` | `argMin(value, by)`, `argMax(value, by)` | [`SupportsArgMinMax`](xref:NextORM.Core.ISqlDialect.SupportsArgMinMax) | ClickHouse |
| Число уникальных | `SqlFunctions.ClickHouse.uniq(x)`, `uniq_exact(x)`, `uniq_combined(x)`, `uniq_hll12(x)` | `toInt64(uniq(x))`, `toInt64(uniqExact(x))`, ... | [`UniqAggregates`](xref:NextORM.Core.ISqlDialect.UniqAggregates) | ClickHouse |
| Квантиль / медиана | `SqlFunctions.ClickHouse.quantile(0.5, x)`, `quantile_exact(0.9, x)`, `quantile_timing(0.5, x)`, `quantiles(new[] { 0.25, 0.5, 0.75 }, x)`, `median(x)` | `toFloat64(quantile(0.5)(x))`, `quantiles(0.25, 0.5, 0.75)(x)`, `toFloat64(median(x))`, ... | [`QuantileAggregates`](xref:NextORM.Core.ISqlDialect.QuantileAggregates) | ClickHouse |
| Наиболее частые (top-K) | `SqlFunctions.ClickHouse.top_k(3, x)`, `top_k_weighted(2, x, w)` | `topK(3)(x)`, `topKWeighted(2)(x, w)` | [`TopKAggregates`](xref:NextORM.Core.ISqlDialect.TopKAggregates) | ClickHouse |
| Произвольное значение | `SqlFunctions.Sql.any_agg(x)` | `ANY_VALUE(x)` / `any(x)` | [`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate) | MySQL, ClickHouse |
| Последняя строка | `SqlFunctions.ClickHouse.any_last(x)` | `anyLast(x)` | [`SupportsAnyLastAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyLastAggregate) | ClickHouse |
| С фильтром | `SqlFunctions.Sql.count(() => p)`, `sum(x, () => p)`, `avg(x, () => p)`, `min(x, () => p)`, `max(x, () => p)` | `count(*) filter (where p)`, ... / `countIf(p)`, `sumIf(x, p)`, ... | [`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle) | PostgreSQL (9.4+), SQLite (ANSI `FILTER`), ClickHouse (`-If`) |
| Последовательности / воронка | `SqlFunctions.ClickHouse.window_funnel(window, ts, c1, c2)`, `sequence_match(pattern, ts, c1, c2)`, `retention(c1, c2)` | `toInt32(windowFunnel(window)(ts, c1, c2))`, `toInt32(sequenceMatch(pattern)(ts, c1, c2))`, `retention(c1, c2)` | [`SequenceAggregates`](xref:NextORM.Core.ISqlDialect.SequenceAggregates) | ClickHouse |
| Упорядоченные | `SqlFunctions.Postgres.percentile_cont(fraction, () => x)`, `percentile_disc(fraction, () => x)`, `median(() => x)`, `mode(() => x)` | `percentile_cont(f) within group (order by x)`, `percentile_cont(0.5) within group (order by x)`, ... | [`SupportsOrderedAggregates`](xref:NextORM.Core.ISqlDialect.SupportsOrderedAggregates) (`mode`), [`SupportsOrderedPercentiles`](xref:NextORM.Core.ISqlDialect.SupportsOrderedPercentiles) (квантили, `median`) | PostgreSQL |

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
select bool_and(b), bit_xor(id), corr(id, nullableint), percentile_cont(0.5) within group (order by id)
 from complex_entity
```

В ClickHouse те же семейства рендерятся по-своему — `groupBitAnd`, `covarPop`, `argMin`/`argMax` — а
логические агрегаты и семейство `regr_*` недоступны (в ClickHouse нет ни
`bool_and`, ни `regr_*`, и диалект отклоняет их через `NotSupportedException`):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Bits = SqlFunctions.Postgres.bit_and(e.Id),
        Cov = SqlFunctions.Sql.covar_pop(e.Id, e.Int),
        FirstByMax = SqlFunctions.ClickHouse.arg_max(e.String, e.Id)
    })
    .First();
```

```sql
-- ClickHouse
select groupBitAnd(id), covarPop(id, nullableint), argMax(somestring, id)
 from complex_entity
```

Упорядоченные агрегаты принимают ключ сортировки как цитируемую лямбду, которая замыкается на параметр
запроса; ключ становится `order by <key>`. Вызов такого агрегата у провайдера без соответствующей
возможности бросает `NotSupportedException`.

В PostgreSQL медиана не является серверной функцией: `SqlFunctions.Postgres.median(() => x)` рендерит
`percentile_cont(0.5) within group (order by x)` и является **агрегатом** (одна строка на группу) под
[`SupportsOrderedPercentiles`](xref:NextORM.Core.ISqlDialect.SupportsOrderedPercentiles). Непрерывные формы интерполируют
и возвращают `double precision`, поэтому `percentile_cont` и `median` требуют выражения сортировки с плавающей
точкой — целочисленный/`numeric` ключ отклоняется с
`The percentile_cont function on PostgreSQL requires a floating-point sort expression.`,
а `percentile_disc` сохраняет тип выражения сортировки. Дробь — это `double` в `[0, 1]`, а не проценты; она
должна быть конечной и константной (литерал или захваченная переменная), а зависящая от строки дробь
отклоняется при генерации SQL. Общий `SqlFunctions.Postgres.percentile(fraction, () => x)` отклоняется:
он никогда не становится псевдонимом непрерывной формы.

Агрегаты ClickHouse для последовательностей/воронки принимают условия как встроенные булевы выражения
(столбец `timestamp` — первым). `window_funnel` возвращает длину наибольшей последовательной цепочки в
скользящем окне, `sequence_match` — `1`/`0` для паттерна; оба материализуются как CLR `int` (диалект
приводит нативный беззнаковый результат через `toInt32(...)`). `retention` возвращает маску `UInt8`,
которую диалект нормализует к объявленному `int[]` (проецируется напрямую или внутри другой
array-функции):

```csharp
var funnel = dataContext.From<IEventEntity>()
    .Select(e => new
    {
        Level = SqlFunctions.ClickHouse.window_funnel(600, e.Timestamp, e.Event == 1, e.Event == 2),
        Matched = SqlFunctions.ClickHouse.sequence_match("(?1).*(?2)", e.Timestamp, e.Event == 1, e.Event == 2),
        Mask = SqlFunctions.ClickHouse.array_string_concat(
            SqlFunctions.ClickHouse.retention(e.Event == 1, e.Event == 2), ",")
    })
    .First();
```

```sql
-- ClickHouse
select toInt32(windowFunnel(600)(ts, (event = 1), (event = 2))) as `Level`,
       toInt32(sequenceMatch('(?1).*(?2)')(ts, (event = 1), (event = 2))) as `Matched`,
       arrayStringConcat(cast(retention((event = 1), (event = 2)) as Array(Int32)), ',') as `Mask`
 from event_entity
```

Возвращающие массив агрегаты следуют **объявленному CLR-типу элемента**, а не выведенному сервером:
`top_k`/`top_k_weighted`/`group_array`/`group_uniq_array` объявляют nullable-тип элемента, если вход
nullable (`int?[]` → `Array(Nullable(Int32))`), а `retention` остаётся `int[]`, `array_map`/`range`
следуют своим объявленным типам (`int[]`, `long[]`). Nullable описывает только **тип результата**:
ClickHouse по-прежнему отбрасывает `NULL`-**входы** агрегата, поэтому `group_array`/`group_uniq_array`
по nullable-колонке не сохраняют null-элементы — объявленный тип элемента nullable, но сам агрегат
nulls не хранит.

## Агрегаты с FILTER

Агрегат может нести фильтр строк, если передать предикат дополнительным аргументом. Один и тот же API
работает у всех провайдеров; способ записи выбирает
[`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle). PostgreSQL и SQLite
генерируют ANSI-предложение `filter (where ...)`, ClickHouse — комбинатор `-If`; MySQL/MariaDB и
SQL Server отклоняют вызов через `NotSupportedException`, потому что у них нет ни того, ни другого. В
PostgreSQL ANSI-форма требует версии сервера 9.4 или новее: диалект, настроенный на более старую
`Version`, отклоняет вызов через `NotSupportedException`, а незаданная версия предполагает 9.4+ (см.
[провайдер PostgreSQL](../connecting-providers/postgres.md#версия-сервера)).

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

Тот же запрос в ClickHouse генерирует комбинатор `-If`:

```sql
-- ClickHouse
select nullableint, toInt32(countIf((id > 10))) as `Big`, sumIf(id, (b = true)) as `Total`
 from complex_entity group by nullableint
```

Фильтр принимают `count`/`count_big`/`min`/`max`/`avg`/`sum` и `string_agg`. В ClickHouse `string_agg`
становится `arrayStringConcat(groupArrayIf(value, predicate), delimiter)`. У MySQL/MariaDB, SQL Server
и in-memory провайдера фильтрованной формы нет.

`string_agg`/`array_agg` тоже принимают фильтр; `string_agg` доступен в PostgreSQL, SQL Server
2017+ и ClickHouse (как `arrayStringConcat(groupArray(x), delimiter)`), а `array_agg` — только в
PostgreSQL (в SQL Server нет типа-массива). Оба результата — колонки-массивы, которые материализуются
как CLR-массивы `T[]`. В ClickHouse дополнительно доступны нативные возвращающие массивы агрегаты
`group_array`/`group_uniq_array` (`groupArray`/`groupUniqArray`). Подробности — в разделе
[Скалярные функции](../scalar-functions/05-aggregates.md#фильтр-агрегатов-filter).

## Различия между провайдерами

| Провайдер | `count_big` | Имена агрегатов | Целочисленный `AVG` | `var` / `varp` |
|---|---|---|---|---|
| SQLite | генерирует `count(*)` (уже 64-битный) | сохраняются как есть; `stdev`/`var` берутся из пользовательских агрегатов, регистрируемых провайдером | дробный (5.5 остаётся 5.5) | поддерживаются через эти пользовательские агрегаты |
| SQL Server | генерирует `count_big(...)` | сохраняются как есть; `stdev`/`var` встроенные | дробный; аргумент расширяется (`avg(cast(id as float))`) | встроенные |
| PostgreSQL | генерирует `count(*)` (уже 64-битный) | `stdev` → `stddev`, `stdevp` → `stddev_pop`, `var` → `variance`, `varp` → `var_pop` | дробный | встроенные |
| MySQL | генерирует `count(*)` (уже 64-битный) | `stdev` → `stddev_samp`, `stdevp` → `stddev_pop`, `var` → `var_samp`, `varp` → `var_pop` | дробный | встроенные (`var_samp` / `var_pop`) |
| MariaDB | генерирует `count(*)` (уже 64-битный) | то же отображение, что в MySQL (`stddev_samp` / `stddev_pop` / `var_samp` / `var_pop`) | дробный | встроенные (`var_samp` / `var_pop`) |
| ClickHouse | оборачивает `count`/`count_distinct` (и фильтрованный `countIf`) в `toInt32(...)`, а `count_big`/`count_big_distinct` — в `toInt64(...)` (нативный `UInt64` не материализуется) | `stdev` → `stddevSamp`, `stdevp` → `stddevPop`, `var` → `varSamp`, `varp` → `varPop`, `covar_*` → `covarPop`/`covarSamp`, `bit_*` → `groupBit*`, фильтр агрегата → комбинатор `<fn>If` (`sumIf`, `countIf`) | дробный | `varSamp` / `varPop` |
| In-memory | не покрыто набором тестов in-memory | не покрыто | дробный (`double?` для целочисленных входов) | не покрыто |

Порядок групп не определён стандартом SQL; проверяйте по ключу группы, а не по позиции. PostgreSQL и
SQLite различаются в порядке `NULL` (PostgreSQL — первыми, SQLite — последними).

`GROUP BY ROLLUP (...)`/`CUBE (...)` генерируется в ANSI-форме в SQL Server, PostgreSQL и SQLite, а в
виде хвостового `... WITH ROLLUP`/`WITH CUBE` — в MySQL/MariaDB и ClickHouse. В MySQL/MariaDB нет
`CUBE`, поэтому [`GroupByCube`](xref:NextORM.Core.EntityBuilder`1.GroupByCube``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) там бросает исключение; провайдер in-memory отклоняет оба модификатора.
`GROUP BY GROUPING SETS (...)` доступен в SQL Server, PostgreSQL, SQLite и ClickHouse, но не в
MySQL/MariaDB и не в провайдере in-memory. `WITH TOTALS` — только ClickHouse; провайдер in-memory
отклоняет его так же, как `ROLLUP`/`CUBE`.

## См. также

- [Сортировка и постраничный вывод](06-sorting-and-paging.md) - [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) по выражению или порядковому номеру.
- [Соединения](04-joins.md) - агрегат по соединённой проекции.
- [SELECT DISTINCT](09-distinct.md) - `DISTINCT` и PostgreSQL `DISTINCT ON`.
- [Запросы и проекции](../engine/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.GroupBy.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite/Functions/CommonTestSuite.Aggregates.cs:9`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:185`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:240`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:181`,
`tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SelectWhereExtrema.cs:41`,
`tests/nextorm.sqlite.tests/SqlGeneration/Sources/T302ExtremeRowAllSqlGenerationTests.cs:27`,
`tests/nextorm.integration.tests/ClickHouse/Integration/ClickHouseIntegrationTests.cs:1353`.
