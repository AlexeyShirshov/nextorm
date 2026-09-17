# Соединения

> Объединяйте строки из двух или более сущностей, производных запросов или необработанных таблиц с помощью `Join`, `LeftJoin`, `RightJoin`, `FullJoin` и `CrossJoin`.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md)

## Обзор

Каждый `Entity<T>` предоставляет пять методов соединения: `Join` (inner), `LeftJoin`, `RightJoin`,
`FullJoin` и `CrossJoin`. Условие соединения — это выражение над двумя сторонами, которое
генерируется как предложение `ON` соединения, именно там, где построитель может его
транслировать. `CrossJoin` не принимает условие и генерирует `cross join`.

Правая сторона может быть:

* другой типизированной сущностью, `Entity<TJoinEntity>`;
* `QueryCommand<TJoinEntity>` — подзапрос, который отображается как производная таблица;
* необработанной таблицей, `Entity`, созданной через `DataContext.From("table")`, столбцы которой
  читаются через индексатор `TableAlias` (`t["id"]`).

Прежде чем перейти к примерам, важно понять две вещи:

1. **Арность соединений ограничена восемью таблицами на этапе компиляции.** Первое соединение
   возвращает `EntityP2<T1, T2>`, следующее — `EntityP3<T1, T2, T3>` и так далее вплоть до
   `EntityP8<T1..T8>`. `EntityP8` намеренно не предоставляет дальнейших методов
   `Join`/`LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`, а `Projection<T1..T8>` не реализует
   `IExtendableProjection`, поэтому девятое соединение не компилируется.
2. **Накопленная проекция адресуется как `p.t1`, `p.t2`, … `p.t8`.** После первого соединения
   условие получает эту проекцию вместо обычной сущности, поэтому цепочка соединений ссылается на
   уже соединённые таблицы через `p.tN`.

Сгенерированный SQL ссылается на каждую таблицу по позиционному псевдониму: `t1` для базовой
таблицы и `t2`, `t3`, … для присоединённых таблиц, в порядке их добавления. Псевдонимы экранируются
в зависимости от провайдера (`'t1'` в SQLite, `[t1]` в SQL Server, `"t1"` в PostgreSQL).

## Внутреннее соединение

```csharp
var rows = await dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

`SimpleEntity.Id` имеет тип `int`, тогда как `ComplexEntity.Id` — `long`, поэтому более узкая
сторона расширяется с помощью `cast(t1.id as bigint)`.

`WHERE` после соединения применяется к накопленной проекции и может ссылаться на любую сторону:

```csharp
var rows = await dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.t2.Boolean ?? false)
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

`Where`, размещённый до соединения, сначала фильтрует левую сторону; для внутренних соединений эти
два варианта эквивалентны, но для внешних соединений они различаются:

```csharp
var rows = await dataContext.Create<ISimpleEntity>()
    .Where(it => it.Id > 2)
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.t2.RequiredString == "34mfs")
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

## Внешние соединения

`LeftJoin` сохраняет каждую строку левой стороны и заполняет правую сторону значением `NULL`, когда
совпадения нет; `RightJoin` и `FullJoin` ведут себя симметрично.

```csharp
var rows = dataContext.Create<ISimpleEntity>()
    .LeftJoin(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.t1.Id, RightString = p.t2.RequiredString })
    .ToList();
```

```sql
select t1.id as 'LeftId', t2.requiredstring as 'RightString' from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

`RightJoin` и `FullJoin` генерируются в той же форме:

```csharp
var right = dataContext.Create<IComplexEntity>()
    .RightJoin(dataContext.Create<ISimpleEntity>(), (c, s) => c.Id == s.Id)
    .Select(p => new { LeftString = p.t1.RequiredString, RightId = p.t2.Id })
    .ToList();

var full = dataContext.Create<ISimpleEntity>()
    .FullJoin(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.t1.Id, RightString = p.t2.RequiredString })
    .ToList();
```

`RightJoin` и `FullJoin` отклоняются с `NotSupportedException` только тогда, когда диалект сообщает
`SupportsRightFullJoin == false`; все поставляемые с nextorm провайдеры заявляют о поддержке.

## Перекрёстное соединение

`CrossJoin` не принимает условие и порождает декартово произведение:

```csharp
var count = dataContext.Create<ISimpleEntity>().CrossJoin(dataContext.Create<IComplexEntity>()).Count();
```

```sql
select count(*) from simple_entity as 't1' cross join complex_entity as 't2'
```

## Соединение с подзапросом

`QueryCommand<T>` можно присоединить напрямую. Он заключается в скобки и получает псевдоним как
производная таблица (псевдоним необязателен в SQLite и обязателен в SQL Server и PostgreSQL):

```csharp
var subQuery = dataContext.Create<IComplexEntity>()
    .Where(it => it.Id == 3)
    .Select(it => new { it.Id, it.RequiredString, it.Boolean });

var rows = await dataContext.Create<ISimpleEntity>()
    .Join(subQuery, (s, c) => s.Id == c.Id)
    .Select(p => new { p.t1.Id, p.t2.RequiredString })
    .ToListAsync();
```

## Соединение с необработанной таблицей

`From("table")` создаёт неуниверсальную `Entity`, столбцы которой доступны через `TableAlias`. Левую
и правую стороны можно свободно смешивать с типизированными сущностями:

```csharp
var rows = dataContext
    .From("simple_entity")
    .Join(dataContext.From("complex_entity"), (s, c) => s["id"] == c["id"])
    .Select(p => new { Id = p.t1["id"].AsInt, Str = p.t2["someString"].AsString })
    .ToList();

var mixed = dataContext
    .From("simple_entity")
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s["id"].AsInt == c.Id)
    .Select(p => new { Id = p.t1["id"].AsInt, Str = p.t2.String })
    .ToList();
```

## Цепочки соединений и арность 2..8

Каждый вызов в цепочке добавляет одну таблицу. Условие получает накопленную на данный момент
проекцию и новую сущность:

```csharp
var rows = dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)          // EntityP2<SimpleEntity, ComplexEntity>
    .Join(dataContext.Create<ISimpleEntity>(), (p, s) => p.t2.Id == s.Id)        // EntityP3<...>
    .Join(dataContext.Create<IComplexEntity>(), (p, c) => p.t3.Id == c.Id)       // EntityP4<...>
    .Select(p => new { A = p.t1.Id, B = p.t2.RequiredString, C = p.t3.Id, D = p.t4.RequiredString })
    .ToList();
```

```sql
select t1.id as 'A', t2.requiredstring as 'B', t3.id as 'C', t4.requiredstring as 'D' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id join simple_entity as 't3' on t2.id = cast(t3.id as bigint) join complex_entity as 't4' on cast(t3.id as bigint) = t4.id
```

Тот же приём позволяет дойти от `EntityP5` до `EntityP8`. При восьми таблицах проекция предоставляет
`t1`..`t8`:

```csharp
var e = new[]
{
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
    dataContext.Create<ISimpleEntity>(), dataContext.Create<ISimpleEntity>(),
};

var sql = e[0]
    .Join(e[1], (a, b) => a.Id == b.Id)
    .Join(e[2], (p, c) => p.t2.Id == c.Id)
    .Join(e[3], (p, c) => p.t3.Id == c.Id)
    .Join(e[4], (p, c) => p.t4.Id == c.Id)
    .Join(e[5], (p, c) => p.t5.Id == c.Id)
    .Join(e[6], (p, c) => p.t6.Id == c.Id)
    .Join(e[7], (p, c) => p.t7.Id == c.Id)
    .Select(p => new { A = p.t1.Id, B = p.t2.Id, C = p.t3.Id, D = p.t4.Id,
                       E = p.t5.Id, F = p.t6.Id, G = p.t7.Id, H = p.t8.Id });
```

## Захваченные параметры в соединении

Захваченная локальная переменная в условии соединения становится параметром и извлекается заново
при каждом выполнении, в том числе при неявном попадании в кэш планов:

```csharp
for (var i = 1; i <= 3; i++)
{
    var id = i;
    var rows = dataContext.Create<ISimpleEntity>()
        .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
        .Where(p => p.t2.Id == id)
        .Select(p => new { p.t1.Id, p.t2.RequiredString })
        .ToList();
}
```

## Различия между провайдерами

| Провайдер | Псевдонимы соединений | Псевдоним производной таблицы | Внешние соединения |
|---|---|---|---|
| SQLite | `as 't1'` | необязателен | поддержаны left/right/full |
| SQL Server | `as [t1]` | обязателен | поддержаны left/right/full |
| PostgreSQL | `as "t1"` | обязателен | поддержаны left/right/full |
| In-memory | неприменимо (выполнение через делегаты) | неприменимо | поддержаны inner/left/right/full/cross; источники в виде табличных функций — нет |

Провайдер in-memory компилирует условие соединения в делегат и выполняет цикл, поэтому он не
генерирует SQL; он поддерживает соединения `Inner`, `Left`, `Right`, `Full` и `Cross` (см.
`test/nextorm.core.tests/InMemoryJoinTests.cs`). Соединения через `EntityP2..P8` разрешаются на этапе
построения запроса у каждого провайдера.

## См. также

- [Подзапросы](06-subqueries.md) - присоединённый `QueryCommand<T>` — это производная таблица.
- [Группировка и агрегаты](04-grouping-and-aggregates.md) - агрегат по соединению.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.Join.cs:9`,
`test/nextorm.core.tests/InMemoryJoinTests.cs:14`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:268` (and the other provider `SqlGenerationTests.cs`).
