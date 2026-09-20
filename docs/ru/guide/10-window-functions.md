# Оконные функции

> Вычисляйте ранжирование, значения соседних строк и накопительные/оконные агрегаты по секции с помощью
> SQL-предложения `OVER`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Группировка и агрегаты](04-grouping-and-aggregates.md) · [Сортировка и постраничная навигация](05-sorting-and-paging.md)

## Обзор

Оконные функции находятся в [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql). Каждый вызов возвращает
маркер [`WindowFunction<T>`](xref:NextORM.Core.WindowFunction`1), который должен быть дополнен `.Over(...)`;
внутри выражения запроса он читается как тип значения функции (`int` для
`row_number`/`rank`/`dense_rank`/`ntile`, `double` для `percent_rank`/`cume_dist`, `T?` для функций
значения/агрегатов). Типы маркеров и их методы интерпретируются исключительно посетителем выражений —
они никогда не выполняются.

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
}
```

[`Over`](xref:NextORM.Core.WindowFunction`1) без аргументов рендерит пустую спецификацию (`over ()`). Поскольку деревья выражений C# отклоняют
именованные аргументы, пропускающие предшествующий параметр со значением по умолчанию, спецификация
**только с сортировкой** должна использовать перегрузку [`WindowOrder`](xref:NextORM.Core.WindowOrder) — `Over(SqlFunctions.Sql.asc(() => e.Id))` —
а не `Over(orderBy: ...)`. `SqlFunctions.Sql.asc(expression)` и `SqlFunctions.Sql.desc(expression)` возвращают [`WindowOrder`](xref:NextORM.Core.WindowOrder)
(ключ сортировки плюс [`OrderDirection`](xref:NextORM.Core.OrderDirection)).

Вызов оконной функции **без** [`Over`](xref:NextORM.Core.WindowFunction`1) — ошибка: посетитель бросает `NotSupportedException`, в сообщении
которого упоминается [`Over`](xref:NextORM.Core.WindowFunction`1).

## Функции

| Функция | Вызов [`Sql`](xref:NextORM.Core.SqlFunctions.Sql) | Генерируемый SQL |
|---|---|---|
| Номер строки | `row_number()` | `row_number()` |
| Ранг (с пропусками) | `rank()` | `rank()` |
| Плотный ранг | `dense_rank()` | `dense_rank()` |
| Относительный ранг | `percent_rank()` | `percent_rank()` |
| Накопленное распределение | `cume_dist()` | `cume_dist()` |
| Интерполированный квантиль | `percentile_cont(fraction, property)` | `percentile_cont(f) within group (order by expr) over (...)` |
| Дискретный квантиль | `percentile_disc(fraction, property)` | `percentile_disc(f) within group (order by expr) over (...)` |
| Сегменты (бакеты) | `ntile(buckets)` | `ntile(n)` |
| Предыдущее значение | `lag(property[, offset[, defaultValue]])` | `lag(expr, offset[, default])` |
| Следующее значение | `lead(property[, offset[, defaultValue]])` | `lead(expr, offset[, default])` |
| Первое в рамке | `first_value(property)` | `first_value(expr)` |
| Последнее в рамке | `last_value(property)` | `last_value(expr)` |
| N-е в рамке | `nth_value(property, n)` | `nth_value(expr, n)` |
| Оконная сумма | `sum_over(property)` | `sum(expr)` |
| Оконное среднее | `avg_over(property)` | `avg(expr)` |
| Оконный минимум | `min_over(property)` | `min(expr)` |
| Оконный максимум | `max_over(property)` | `max(expr)` |
| Оконное количество | `count_over()` / `count_over(property)` | `count(*)` / `count(expr)` |

Варианты агрегатов имеют суффикс `_over`, чтобы не конфликтовать со скалярными агрегатами `sum`, `avg`,
`min`, `max` и `count`, используемыми с [`GroupBy`](xref:NextORM.Core.EntityBuilder`1).

## Номер строки по секции

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

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id | rn |
|----|----|
| 1 | 1 |
| 2 | 1 |
| 3 | 2 |

В заполненной `complex_entity` есть секция `nullableint` из одной строки (`id` 1) и секция из двух строк
(`id` 2, 3), поэтому `rn` равно 1, 1, 2.

## Rank, dense rank и `asc`/`desc`

`rank()` оставляет пропуск после совпадения, `dense_rank()` — нет. Здесь обе используют перегрузку
[`WindowOrder`](xref:NextORM.Core.WindowOrder):

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

Убывающий ключ записывается через `SqlFunctions.Sql.desc`. Когда нужно несколько ключей (или смесь направлений),
используйте перегрузку `WindowOrder[]`; секции передаются через перегрузку с массивом:

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

## `lag` и `lead` со смещением и значением по умолчанию

`lag`/`lead` принимают необязательное смещение и значение по умолчанию. Значение по умолчанию заполняет
только **отсутствующую строку** на границе; значение `null` в существующей строке возвращается без
изменений:

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

## Оконные агрегаты

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

## Рамки (frames)

Рамка ограничивает строки, которые видит агрегат. [`WindowFrame`](xref:NextORM.Core.WindowFrame) имеет фабричные методы для обеих
единиц рамки SQL, а [`WindowFrameBound`](xref:NextORM.Core.WindowFrameBound) — границы:

| Фабрика | Генерирует |
|---|---|
| [`Rows`](xref:NextORM.Core.WindowFrame) | `rows between <start> and <end>` |
| [`Range`](xref:NextORM.Core.WindowFrame) | `range between <start> and <end>` |
| [`Rows`](xref:NextORM.Core.WindowFrame) | `rows between <preceding> preceding and <following> following` |
| [`RowsUnboundedPrecedingToCurrentRow`](xref:NextORM.Core.WindowFrame.RowsUnboundedPrecedingToCurrentRow) | `rows between unbounded preceding and current row` |
| [`RangeUnboundedPrecedingToCurrentRow`](xref:NextORM.Core.WindowFrame.RangeUnboundedPrecedingToCurrentRow) | `range between unbounded preceding and current row` |

| Граница | Генерирует |
|---|---|
| [`UnboundedPreceding`](xref:NextORM.Core.WindowFrameBound.UnboundedPreceding) | `unbounded preceding` |
| [`Preceding`](xref:NextORM.Core.WindowFrameBound) | `<n> preceding` |
| [`CurrentRow`](xref:NextORM.Core.WindowFrameBound.CurrentRow) | `current row` |
| [`Following`](xref:NextORM.Core.WindowFrameBound) | `<n> following` |
| [`UnboundedFollowing`](xref:NextORM.Core.WindowFrameBound.UnboundedFollowing) | `unbounded following` |

Агрегат с накопительной рамкой и скользящее окно:

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

## Различия между провайдерами

Оконные функции соответствуют ANSI, и каждый SQL-провайдер, поддерживаемый nextorm, поддерживает обе
единицы рамки, поэтому генерируемый SQL одинаков, за исключением заключения в кавычки идентификаторов
(одинарные кавычки, квадратные скобки или двойные кавычки для псевдонима; см.
[Обзор провайдеров](../providers/overview.md)). Пару `percent_rank()`/`cume_dist()` поддерживают все
провайдеры ([`SupportsPercentRankCumeDist`](xref:NextORM.Core.ISqlDialect.SupportsPercentRankCumeDist)).
Единственная непереносимая value-функция — `nth_value`: в SQL Server нет `NTH_VALUE`, поэтому
[`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue) заставляет его отклонять вызов
через `NotSupportedException`, тогда как PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse рендерят
`nth_value(expr, n)`.

Второе исключение — оконные квантили: `percentile_cont`/`percentile_disc` непереносимы как оконные
функции. SQL Server и MariaDB рендерят их как
`percentile_cont(f) within group (order by x) over (...)` под флагом
[`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow); PostgreSQL выражает
квантили упорядоченным **агрегатом**
([`SqlFunctions.Postgres.percentile_cont`](xref:NextORM.Core.PostgresFunctions.percentile_cont)), а
MySQL, SQLite и ClickHouse отклоняют оконную форму через `NotSupportedException`.

| Провайдер | Поведение |
|---|---|
| SQLite | Полная поддержка `OVER`; псевдонимы столбцов в одинарных кавычках (`as 'rn'`). |
| SQL Server | Полная поддержка `OVER`; нет `nth_value`; псевдонимы в квадратных скобках (`as [rn]`). |
| PostgreSQL | Полная поддержка `OVER`; псевдонимы в двойных кавычках (`as "rn"`). |
| MySQL | Полная поддержка `OVER`; псевдонимы столбцов в обратных кавычках (`` as `rn` ``). |
| MariaDB | Полная поддержка `OVER`; псевдонимы в обратных кавычках. |
| ClickHouse | Поддержка `OVER`; псевдонимы в обратных кавычках. |
| In-memory | Не применимо: оконные функции рендерятся SQL-диалектами и не являются частью провайдера in-memory. |

## См. также

* [Группировка и агрегаты](04-grouping-and-aggregates.md) — скалярные агрегаты, которые затеняют варианты `_over`.
* [Сортировка и постраничная навигация](05-sorting-and-paging.md) — сортировка внешнего запроса, выбирающего столбцы окна.
* [Обзор провайдеров](../providers/overview.md) — кавычки для псевдонимов и флаги возможностей провайдеров.

---

Source: `src/nextorm.core/Query/WindowFunctions.cs:16` ([`WindowFunction<T>`](xref:NextORM.Core.WindowFunction`1)), `:56` ([`WindowOrder`](xref:NextORM.Core.WindowOrder)), `:72`/`:79` (frame enums), `:89` ([`WindowFrameBound`](xref:NextORM.Core.WindowFrameBound)), `:118` ([`WindowFrame`](xref:NextORM.Core.WindowFrame)); `src/nextorm.core/Query/SqlFunctions.cs:308` (`asc`/`desc`), `:314` (functions);
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:621`;
`tests/nextorm.integration.tests/CommonTestSuite.Window.cs:14`, `:33`, `:54`, `:72`, `:99`;
`tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:28`, `:64`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1266`, `:1281`, `:1297`, `:1314`, `:1330`, `:1346`, `:1366`, `:1383`, `:1401`, `:1422`.
