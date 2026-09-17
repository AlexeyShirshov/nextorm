# Операции над множествами

> Объединяйте два набора результатов с помощью `Union`, `UnionAll`, `Intersect`, `IntersectAll`, `Except` и `ExceptAll` и выстраивайте их в цепочку слева направо.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Подзапросы](06-subqueries.md) · [SELECT DISTINCT](08-distinct.md)

## Обзор

Каждый `QueryCommand<TResult>` предоставляет шесть методов операций над множествами, которые
принимают другой запрос и возвращают новый запрос:

| Метод | Ключевое слово SQL | Сохраняет дубликаты |
|---|---|---|
| `Union(query)` | `union` | нет |
| `UnionAll(query)` | `union all` | да |
| `Intersect(query)` | `intersect` | нет |
| `IntersectAll(query)` | `intersect all` | да |
| `Except(query)` | `except` | нет |
| `ExceptAll(query)` | `except all` | да |

Правый запрос может проецировать другой тип элемента: метод является обобщённым по другой стороне
(`Union<T>(QueryCommand<T>)`), поэтому
`SimpleEntity.Select(it => it.Id).Union(ComplexEntity.Select(it => (int)it.Id))`
допустим, пока формы совпадают.

Результат сам является `QueryCommand`, поэтому его можно выполнить через `From(result)` или
применить к нему ещё одну операцию над множествами. **Цепочка применяется слева направо**: каждая
новая операция объединяет накопленную левую сторону со следующим запросом, и внутри цепочки
действует стандартный приоритет операторов (`(A except B) intersect C`, а не
`A except (B intersect C)`).

Самое серьёзное ограничение переносимости — `*ALL`. PostgreSQL реализует `intersect all` и
`except all`; SQLite и SQL Server — нет, и nextorm выбрасывает `NotSupportedException`, когда такой
запрос подготавливается. `union`, `union all`, `intersect` и `except` поддерживаются везде.

## Union и UnionAll

```csharp
var distinct = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Union(dataContext.Create<ISimpleEntity>().Select(it => it.Id));

var all = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .UnionAll(dataContext.Create<ISimpleEntity>().Select(it => it.Id));
```

```sql
select id from simple_entity
 union 
select id from simple_entity
```

```sql
select id from simple_entity
 union all 
select id from simple_entity
```

Сторонами также могут быть разные сущности, если типы элементов совпадают. В наборе интеграционных
тестов `SimpleEntity` (ids `1..10`) объединяется через union с `ComplexEntity` (ids `1..3`),
приведённой к `int`, поэтому объединение с устранением дубликатов даёт 10 строк, а `UnionAll` той же
пары — 13:

```csharp
var distinctCount = dataContext.From(
    dataContext.Create<ISimpleEntity>().Select(it => it.Id)
        .Union(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id))).Count(); // 10

var allCount = dataContext.From(
    dataContext.Create<ISimpleEntity>().Select(it => it.Id)
        .UnionAll(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id))).Count(); // 13
```

## Intersect и Except

```csharp
var common = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Intersect(dataContext.Create<ISimpleEntity>().Select(it => it.Id));

var onlyLeft = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Except(dataContext.Create<ISimpleEntity>().Select(it => it.Id));
```

```sql
select id from simple_entity
 intersect 
select id from simple_entity
```

```sql
select id from simple_entity
 except 
select id from simple_entity
```

Между разными сущностями: поскольку `simple_entity` содержит ids `1..10`, а `complex_entity` —
`1..3`, `INTERSECT` оставляет `1, 2, 3`, а `EXCEPT` — `4..10`.

## Цепочка выполняется слева направо

```csharp
// (simple EXCEPT complex) INTERSECT simple = {4..10} INTERSECT {1..10} = {4..10}.
var cmd = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .Except(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id))
    .Intersect(dataContext.Create<ISimpleEntity>().Select(it => it.Id));

var count = dataContext.From(cmd).Count(); // 7
```

## IntersectAll и ExceptAll

```csharp
var cmd = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .IntersectAll(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id));

dataContext.From(cmd).Count();
```

```sql
-- PostgreSQL only
select id from simple_entity
 intersect all 
select id from simple_entity
```

```csharp
var cmd = dataContext.Create<ISimpleEntity>().Select(it => it.Id)
    .ExceptAll(dataContext.Create<IComplexEntity>().Select(it => (int)it.Id));

dataContext.From(cmd).Count();
```

Когда диалект не поддерживает операцию, подготовка запроса выбрасывает `NotSupportedException` с
именем операции в сообщении (`"The IntersectAll set operation is not supported by this SQL
dialect"`).

## Запрос результата операции над множествами

Операция над множествами возвращает запрос, поэтому вы делаете запрос к нему через `From`:

```csharp
var cmd = dataContext.Create<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.Create<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

Результат, типизированный как сущность, можно проецировать таким же образом:

```csharp
var count = dataContext.From(
    dataContext.Create<ISimpleEntity>().Select(it => new { it.Id })
        .Union(dataContext.Create<IComplexEntity>().Select(it => new { it.Id })))
    .Count();
```

## Различия между провайдерами

| Провайдер | `union` / `union all` | `intersect` / `except` | `intersect all` / `except all` |
|---|---|---|---|
| SQLite | поддерживается | поддерживается | **`NotSupportedException`** при подготовке |
| SQL Server | поддерживается | поддерживается | **`NotSupportedException`** при подготовке |
| PostgreSQL | поддерживается | поддерживается | поддерживается |
| In-memory | не покрыто набором тестов in-memory | не покрыто | не покрыто |

Это соответствует возможности `SupportsIntersectExceptAll`: PostgreSQL — единственный поставляемый
провайдер, возвращающий `true`; SQLite и SQL Server возвращают `false`, и построитель SQL
отказывается генерировать операцию над множествами `*ALL` для них.

## См. также

- [SELECT DISTINCT](08-distinct.md) - `Distinct()` и операции над множествами взаимодействуют.
- [Подзапросы](06-subqueries.md) - запрос операции над множествами можно использовать как источник `FROM`.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.SetOperations.cs:8`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:743,761`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:49,69,89`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:62,102`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:48,88`.
