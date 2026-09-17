# Оконные функции

> Вычисляйте ранжирование, значения соседних строк и накопительные/оконные агрегаты по секции с помощью
> SQL-предложения `OVER`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Группировка и агрегаты](04-grouping-and-aggregates.md) · [Сортировка и постраничная навигация](05-sorting-and-paging.md)

## Обзор

Оконные функции находятся в `NORM.SQL`. Каждый вызов возвращает маркер `NORM.WindowFunction<T>`, который
должен быть дополнен `.Over(...)`; внутри выражения запроса он читается как тип значения функции
(`int` для функций ранжирования, `T?` для функций значения/агрегатов). Типы маркеров и их методы
интерпретируются исключительно посетителем выражений — они никогда не выполняются.

```csharp
public static class NORM
{
    public static NORM_SQL SQL { get; }
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

`Over()` без аргументов рендерит пустую спецификацию (`over ()`). Поскольку деревья выражений C# отклоняют
именованные аргументы, пропускающие предшествующий параметр со значением по умолчанию, спецификация
**только с сортировкой** должна использовать перегрузку `WindowOrder` — `Over(NORM.SQL.asc(() => e.Id))` —
а не `Over(orderBy: ...)`. `NORM.SQL.asc(expression)` и `NORM.SQL.desc(expression)` возвращают `WindowOrder`
(ключ сортировки плюс `OrderDirection`).

Вызов оконной функции **без** `Over` — ошибка: посетитель бросает `NotSupportedException`, в сообщении
которого упоминается `Over`.

## Функции

| Функция | Вызов `NORM.SQL` | Генерируемый SQL |
|---|---|---|
| Номер строки | `row_number()` | `row_number()` |
| Ранг (с пропусками) | `rank()` | `rank()` |
| Плотный ранг | `dense_rank()` | `dense_rank()` |
| Сегменты (бакеты) | `ntile(buckets)` | `ntile(n)` |
| Предыдущее значение | `lag(property[, offset[, defaultValue]])` | `lag(expr, offset[, default])` |
| Следующее значение | `lead(property[, offset[, defaultValue]])` | `lead(expr, offset[, default])` |
| Первое в рамке | `first_value(property)` | `first_value(expr)` |
| Последнее в рамке | `last_value(property)` | `last_value(expr)` |
| Оконная сумма | `sum_over(property)` | `sum(expr)` |
| Оконное среднее | `avg_over(property)` | `avg(expr)` |
| Оконный минимум | `min_over(property)` | `min(expr)` |
| Оконный максимум | `max_over(property)` | `max(expr)` |
| Оконное количество | `count_over()` / `count_over(property)` | `count(*)` / `count(expr)` |

Варианты агрегатов имеют суффикс `_over`, чтобы не конфликтовать со скалярными агрегатами `sum`, `avg`,
`min`, `max` и `count`, используемыми с `GroupBy`.

## Номер строки по секции

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        rn = NORM.SQL.row_number().Over(partitionBy: () => e.Int, orderBy: () => e.Id)
    })
    .OrderBy(1, OrderDirection.Asc)
    .ToList();
```

```sql
select id, row_number() over (partition by nullableint order by id) as 'rn' from complex_entity
```

В заполненной `complex_entity` есть секция `nullableint` из одной строки (`id` 1) и секция из двух строк
(`id` 2, 3), поэтому `rn` равно 1, 1, 2.

## Rank, dense rank и `asc`/`desc`

`rank()` оставляет пропуск после совпадения, `dense_rank()` — нет. Здесь обе используют перегрузку
`WindowOrder`:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        r = NORM.SQL.rank().Over(NORM.SQL.asc(() => e.Id)),
        dr = NORM.SQL.dense_rank().Over(NORM.SQL.asc(() => e.Id))
    })
    .ToList();
```

```sql
select id, rank() over (order by id) as 'r', dense_rank() over (order by id) as 'dr' from complex_entity
```

Убывающий ключ записывается через `NORM.SQL.desc`. Когда нужно несколько ключей (или смесь направлений),
используйте перегрузку `WindowOrder[]`; секции передаются через перегрузку с массивом:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        r = NORM.SQL.row_number().Over(
            partitionBy: new Expression<Func<object?>>[] { () => e.Int },
            orderBy: new[] { NORM.SQL.desc(() => e.Id) })
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
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        prev = NORM.SQL.lag(e.Id, 1, 0L).Over(NORM.SQL.asc(() => e.Id)),
        next = NORM.SQL.lead(e.Int, 2, 0).Over(NORM.SQL.asc(() => e.Id))
    })
    .ToList();
```

```sql
select id, lag(id, 1, 0) over (order by id) as 'prev', lead(nullableint, 2, 0) over (order by id) as 'next' from complex_entity
```

## Оконные агрегаты

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        total = NORM.SQL.sum_over(e.Id).Over(partitionBy: () => e.Int),
        n = NORM.SQL.count_over().Over(partitionBy: () => e.Int)
    })
    .ToList();
```

```sql
select id, sum(id) over (partition by nullableint) as 'total', count(*) over (partition by nullableint) as 'n' from complex_entity
```

## Рамки (frames)

Рамка ограничивает строки, которые видит агрегат. `NORM.WindowFrame` имеет фабричные методы для обеих
единиц рамки SQL, а `NORM.WindowFrameBound` — границы:

| Фабрика | Генерирует |
|---|---|
| `WindowFrame.Rows(start, end)` | `rows between <start> and <end>` |
| `WindowFrame.Range(start, end)` | `range between <start> and <end>` |
| `WindowFrame.Rows(preceding, following)` | `rows between <preceding> preceding and <following> following` |
| `WindowFrame.RowsUnboundedPrecedingToCurrentRow` | `rows between unbounded preceding and current row` |
| `WindowFrame.RangeUnboundedPrecedingToCurrentRow` | `range between unbounded preceding and current row` |

| Граница | Генерирует |
|---|---|
| `WindowFrameBound.UnboundedPreceding` | `unbounded preceding` |
| `WindowFrameBound.Preceding(n)` | `<n> preceding` |
| `WindowFrameBound.CurrentRow` | `current row` |
| `WindowFrameBound.Following(n)` | `<n> following` |
| `WindowFrameBound.UnboundedFollowing` | `unbounded following` |

Агрегат с накопительной рамкой и скользящее окно:

```csharp
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        running = NORM.SQL.sum_over(e.Id).Over(
            NORM.SQL.asc(() => e.Id),
            NORM.WindowFrame.RowsUnboundedPrecedingToCurrentRow),
        sliding = NORM.SQL.sum_over(e.Id).Over(
            NORM.SQL.asc(() => e.Id),
            NORM.WindowFrame.Rows(1, 1))
    })
    .ToList();
```

```sql
select id, sum(id) over (order by id rows between unbounded preceding and current row) as 'running', sum(id) over (order by id rows between 1 preceding and 1 following) as 'sliding' from complex_entity
```

## Различия между провайдерами

Оконные функции соответствуют ANSI, и каждый SQL-провайдер, поддерживаемый nextorm, поддерживает все
функции и обе единицы рамки, поэтому генерируемый SQL одинаков, за исключением заключения в кавычки
идентификаторов (одинарные кавычки, квадратные скобки или двойные кавычки для псевдонима; см.
[Обзор провайдеров](../providers/overview.md)).

| Провайдер | Поведение |
|---|---|
| SQLite | Полная поддержка `OVER`; псевдонимы столбцов в одинарных кавычках (`as 'rn'`). |
| SQL Server | Полная поддержка `OVER`; псевдонимы в квадратных скобках (`as [rn]`). |
| PostgreSQL | Полная поддержка `OVER`; псевдонимы в двойных кавычках (`as "rn"`). |
| In-memory | Не применимо: оконные функции рендерятся SQL-диалектами и не являются частью провайдера in-memory. |

## См. также

* [Группировка и агрегаты](04-grouping-and-aggregates.md) — скалярные агрегаты, которые затеняют варианты `_over`.
* [Сортировка и постраничная навигация](05-sorting-and-paging.md) — сортировка внешнего запроса, выбирающего столбцы окна.
* [Обзор провайдеров](../providers/overview.md) — кавычки для псевдонимов и флаги возможностей провайдеров.

---

Source: `src/nextorm.core/Query/NORM.cs:23` (`WindowFunction<T>`), `:64` (`WindowOrder`), `:80`/`:87` (frame enums), `:97` (`WindowFrameBound`), `:126` (`WindowFrame`), `:215` (`asc`/`desc`), `:221` (functions);
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:621`;
`test/nextorm.integration.tests/CommonTestSuite.Window.cs:14`, `:33`, `:54`, `:72`, `:99`;
`test/nextorm.core.tests/WindowFunctionMarkerTests.cs:28`, `:64`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:1266`, `:1281`, `:1297`, `:1314`, `:1330`, `:1346`, `:1366`, `:1383`, `:1401`, `:1422`.
