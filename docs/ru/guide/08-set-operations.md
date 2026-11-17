# Операции над множествами

> Объединяйте результаты двух запросов. Выбирайте операцию по тому, нужно ли удалить дубликаты, оставить общие строки или исключить строки правой стороны.

**Предварительные требования:** [Запросы и проекции](../engine/index.md) · [Подзапросы](07-subqueries.md) · [SELECT DISTINCT](09-distinct.md)

## Обзор

У каждого [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) есть шесть методов, которые
сочетают его результат с другим запросом и возвращают новый запрос. Выберите операцию по нужному
результату:

* `Union` объединяет результаты и удаляет дубликаты, а `UnionAll` сохраняет их.
* `Intersect` оставляет строки, присутствующие с обеих сторон, а `Except` — только строки левой стороны.
* Варианты `IntersectAll` и `ExceptAll` сохраняют количество повторений.

| Метод | Ключевое слово SQL | Сохраняет дубликаты | Правая сторона по селектору |
|---|---|---|---|
| `Union(query)` / `Union<TEntity>(selector)` | `union` | нет | `Union<TEntity>(selector)` |
| `UnionAll(query)` / `UnionAll<TEntity>(selector)` | `union all` | да | `UnionAll<TEntity>(selector)` |
| `Intersect(query)` / `Intersect<TEntity>(selector)` | `intersect` | нет | `Intersect<TEntity>(selector)` |
| `IntersectAll(query)` / `IntersectAll<TEntity>(selector)` | `intersect all` | да | `IntersectAll<TEntity>(selector)` |
| `Except(query)` / `Except<TEntity>(selector)` | `except` | нет | `Except<TEntity>(selector)` |
| `ExceptAll(query)` / `ExceptAll<TEntity>(selector)` | `except all` | да | `ExceptAll<TEntity>(selector)` |

Правый запрос может проецировать другой тип элемента, если формы результатов совпадают. Например,
`SimpleEntity.Select(it => it.Id).Union(ComplexEntity.Select(it => (int)it.Id))` объединяет две проекции
идентификаторов.

Перегрузка с селектором (`Union<TEntity>(selector)` и аналоги) преобразует `TEntity` правой стороны в
`TResult` левой. Если справа нужен другой тип CLR, используйте явную перегрузку
`Union<T>(QueryCommand<T>)`.

Результат сам является [`QueryCommand`](xref:NextORM.Core.QueryCommand), поэтому его можно выполнить через `From(result)` или
применить к нему ещё одну операцию над множествами. **Цепочка применяется слева направо**: каждая
новая операция объединяет накопленную левую сторону со следующим запросом, и внутри цепочки
действует стандартный приоритет операторов (`(A except B) intersect C`, а не
`A except (B intersect C)`).

Самое серьёзное ограничение переносимости — `*ALL`. PostgreSQL реализует `intersect all` и
`except all`; SQLite и SQL Server — нет, и nextorm выбрасывает `NotSupportedException`, когда такой
запрос подготавливается. `union`, `union all`, `intersect` и `except` поддерживаются везде.

## Union и UnionAll

```csharp
var distinct = dataContext.From<ISimpleEntity>().Select(it => it.Id)
    .Union(dataContext.From<ISimpleEntity>().Select(it => it.Id));

var all = dataContext.From<ISimpleEntity>().Select(it => it.Id)
    .UnionAll(dataContext.From<ISimpleEntity>().Select(it => it.Id));
```

```sql
select id
 from simple_entity
 union 
select id
 from simple_entity
```

```sql
select id
 from simple_entity
 union all 
select id
 from simple_entity
```

Сторонами также могут быть разные сущности, если типы элементов совпадают. В наборе интеграционных
тестов `SimpleEntity` (ids `1..10`) объединяется через union с `ComplexEntity` (ids `1..3`),
приведённой к `int`, поэтому объединение с устранением дубликатов даёт 10 строк, а [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) той же
пары — 13:

```csharp
var distinctCount = dataContext.From(
    dataContext.From<ISimpleEntity>().Select(it => it.Id)
        .Union<IComplexEntity>(it => (int)it.Id)).Count(); // 10

var allCount = dataContext.From(
    dataContext.From<ISimpleEntity>().Select(it => it.Id)
        .UnionAll<IComplexEntity>(it => (int)it.Id)).Count(); // 13
```

## Intersect и Except

`Intersect` возвращает значения, присутствующие с обеих сторон. `Except` оставляет значения, которые
есть только слева.

```csharp
var common = dataContext.From<ISimpleEntity>().Select(it => it.Id)
    .Intersect(dataContext.From<ISimpleEntity>().Select(it => it.Id));

var onlyLeft = dataContext.From<ISimpleEntity>().Select(it => it.Id)
    .Except(dataContext.From<ISimpleEntity>().Select(it => it.Id));
```

```sql
select id
 from simple_entity
 intersect 
select id
 from simple_entity
```

```sql
select id
 from simple_entity
 except 
select id
 from simple_entity
```

Между разными сущностями: поскольку `simple_entity` содержит ids `1..10`, а `complex_entity` —
`1..3`, `INTERSECT` оставляет `1, 2, 3`, а `EXCEPT` — `4..10`.

## Цепочка выполняется слева направо

```csharp
// (simple EXCEPT complex) INTERSECT simple = {4..10} INTERSECT {1..10} = {4..10}.
var cmd = dataContext.From<ISimpleEntity>().Select(it => it.Id)
    .Except(dataContext.From<IComplexEntity>().Select(it => (int)it.Id))
    .Intersect(dataContext.From<ISimpleEntity>().Select(it => it.Id));

var count = dataContext.From(cmd).Count(); // 7
```

## IntersectAll и ExceptAll

Варианты `All` сохраняют количество дубликатов. Они доступны только у провайдеров, чьи диалекты
поддерживают соответствующие операции `INTERSECT ALL` и `EXCEPT ALL`.

```csharp
var cmd = dataContext.From<ISimpleEntity>().Select(it => it.Id)
    .IntersectAll(dataContext.From<IComplexEntity>().Select(it => (int)it.Id));

dataContext.From(cmd).Count();
```

```sql
-- PostgreSQL only
select id
 from simple_entity
 intersect all 
select id
 from simple_entity
```

```csharp
var cmd = dataContext.From<ISimpleEntity>().Select(it => it.Id)
    .ExceptAll(dataContext.From<IComplexEntity>().Select(it => (int)it.Id));

dataContext.From(cmd).Count();
```

Если диалект не поддерживает операцию, подготовка запроса выбрасывает `NotSupportedException`. В
сообщении указано её имя (`"The IntersectAll set operation is not supported by this SQL dialect"`).

## Запрос результата операции над множествами

Операция над множествами возвращает запрос. Чтобы продолжить работу с результатом, передайте его в
[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)):

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

Результат, типизированный как сущность, можно проецировать таким же образом:

```csharp
var count = dataContext.From(
    dataContext.From<ISimpleEntity>().Select(it => new { it.Id })
        .Union(dataContext.From<IComplexEntity>().Select(it => new { it.Id })))
    .Count();
```

### Прямое чтение одного скалярного столбца

Если обе стороны проецируют один скалярный столбец, `From(cmd)` сразу материализует строки, а не
объекты. `UnionAll` при этом сохраняет дубликаты:

```csharp
var left = dataContext.From<ISimpleEntity>().Where(it => it.Id <= 2).Select(it => it.Id);
var right = dataContext.From<ISimpleEntity>().Where(it => it.Id >= 2 && it.Id <= 3).Select(it => it.Id);

var rows = dataContext.From(left.UnionAll(right)).ToList(); // 1, 2, 2, 3
```

Результат не зависит от состояния общего кэша списка выборки для этого типа: пуст ли он или заполнен
предыдущим чтением скаляра. Производное чтение строит собственную форму из одного столбца и не
использует и не перезаписывает запись кэша. Для производного источника с несколькими столбцами
по-прежнему нужен внешний `Select` (см.
[Подзапросы](07-subqueries.md#подзапрос-как-источник-from)).

## Различия между провайдерами

| Провайдер | `union` / `union all` | `intersect` / `except` | `intersect all` / `except all` |
|---|---|---|---|
| SQLite | поддерживается | поддерживается | **`NotSupportedException`** при подготовке |
| SQL Server | поддерживается | поддерживается | **`NotSupportedException`** при подготовке |
| PostgreSQL | поддерживается | поддерживается | поддерживается |
| MySQL | поддерживается | поддерживается (MySQL 8.0.31+) | **`NotSupportedException`** при подготовке (варианты `ALL` отсутствуют) |
| MariaDB | поддерживается | поддерживается (MariaDB 10.4+) | поддерживается |
| ClickHouse | поддерживается | поддерживается | поддерживается |
| In-memory | не покрыто набором тестов in-memory | не покрыто | не покрыто |

Это соответствует возможности [`SupportsIntersectExceptAll`](xref:NextORM.Core.ISqlDialect.SupportsIntersectExceptAll): PostgreSQL, MariaDB и ClickHouse —
поставляемые провайдеры, возвращающие `true`; SQLite и SQL Server возвращают `false`, и построитель SQL
отказывается генерировать операцию над множествами `*ALL` для них.

## См. также

- [SELECT DISTINCT](09-distinct.md) - [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) и операции над множествами взаимодействуют.
- [Подзапросы](07-subqueries.md) - запрос операции над множествами можно использовать как источник `FROM`.
- [Запросы и проекции](../engine/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.SetOperations.cs:8`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:743,761`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:49,69,89`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:62,102`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:48,88`.
