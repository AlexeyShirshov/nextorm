# Соединения

> Объединяйте строки из двух или более сущностей, производных запросов или необработанных таблиц с помощью [`Join`](xref:NextORM.Core.EntityBuilder`1), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1).

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md)

## Обзор

Каждый `EntityBuilder<T>` предоставляет семь методов соединения: [`Join`](xref:NextORM.Core.EntityBuilder`1) (inner), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1),
[`FullJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1). Условие соединения — это выражение над двумя
сторонами, которое генерируется как предложение `ON` соединения, именно там, где построитель может его
транслировать. [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1) не принимают условие; первый генерирует
`cross join`, а последние два — форму lateral/apply конкретного провайдера (см.
[APPLY и LATERAL](#apply-и-lateral)).

Правая сторона может быть:

* другой типизированной сущностью, `EntityBuilder<TJoinEntity>`;
* `QueryCommand<TJoinEntity>` — подзапрос, который отображается как производная таблица;
* необработанной таблицей, [`EntityBuilder`](xref:NextORM.Core.EntityBuilder), созданной через [`From`](xref:NextORM.Core.DataContext), столбцы которой
  читаются через индексатор [`TableAlias`](xref:NextORM.Core.TableAlias) (`t["id"]`).

Прежде чем перейти к примерам, важно понять две вещи:

1. **Арность соединений ограничена восемью таблицами на этапе компиляции.** Первое соединение
   возвращает [`JoinedEntityBuilder<T1, T2>`](xref:NextORM.Core.JoinedEntityBuilder`2), следующее — [`JoinedEntityBuilder<T1, T2, T3>`](xref:NextORM.Core.JoinedEntityBuilder`3) и так далее вплоть до
   `JoinedEntityBuilder<T1..T8>`. `JoinedEntityBuilder` намеренно не предоставляет дальнейших методов
   [`Join`](xref:NextORM.Core.EntityBuilder`1)/[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1)/[`RightJoin`](xref:NextORM.Core.EntityBuilder`1)/[`FullJoin`](xref:NextORM.Core.EntityBuilder`1)/[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1), а `Projection<T1..T8>` не реализует
   [`IExtendableProjection`](xref:NextORM.Core.IExtendableProjection), поэтому девятое соединение не компилируется.
2. **Накопленная проекция адресуется как `p.Item1`, `p.Item2`, … `p.Item8`.** После первого соединения
   условие получает эту проекцию вместо обычной сущности, поэтому цепочка соединений ссылается на
   уже соединённые таблицы через `p.tN`.

Сгенерированный SQL ссылается на каждую таблицу по позиционному псевдониму: `t1` для базовой
таблицы и `t2`, `t3`, … для присоединённых таблиц, в порядке их добавления. Псевдонимы экранируются
в зависимости от провайдера (`'t1'` в SQLite, `[t1]` в SQL Server, `"t1"` в PostgreSQL).

## Внутреннее соединение

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id | RequiredString |
|----|----------------|
| 1 | sdf |
| 2 | asdfgoi |
| 3 | 34mfs |

`SimpleEntity.Id` имеет тип `int`, тогда как `ComplexEntity.Id` — `long`, поэтому более узкая
сторона расширяется с помощью `cast(t1.id as bigint)`.

`WHERE` после соединения применяется к накопленной проекции и может ссылаться на любую сторону:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.Boolean ?? false)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

[`Where`](xref:NextORM.Core.EntityBuilder`1), размещённый до соединения, сначала фильтрует левую сторону; для внутренних соединений эти
два варианта эквивалентны, но для внешних соединений они различаются:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id > 2)
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.RequiredString == "34mfs")
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

## Внешние соединения

[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1) сохраняет каждую строку левой стороны и заполняет правую сторону значением `NULL`, когда
совпадения нет; [`RightJoin`](xref:NextORM.Core.EntityBuilder`1) и [`FullJoin`](xref:NextORM.Core.EntityBuilder`1) ведут себя симметрично.

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

```sql
select t1.id as 'LeftId', t2.requiredstring as 'RightString' from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1) и [`FullJoin`](xref:NextORM.Core.EntityBuilder`1) генерируются в той же форме:

```csharp
var right = dataContext.From<IComplexEntity>()
    .RightJoin(dataContext.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
    .Select(p => new { LeftString = p.Item1.RequiredString, RightId = p.Item2.Id })
    .ToList();

var full = dataContext.From<ISimpleEntity>()
    .FullJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1) и [`FullJoin`](xref:NextORM.Core.EntityBuilder`1) отклоняются с `NotSupportedException` только тогда, когда диалект сообщает
`SupportsRightFullJoin == false`; все поставляемые с nextorm провайдеры заявляют о поддержке.

## Перекрёстное соединение

[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1) не принимает условие и порождает декартово произведение:

```csharp
var count = dataContext.From<ISimpleEntity>().CrossJoin(dataContext.From<IComplexEntity>()).Count();
```

```sql
select count(*) from simple_entity as 't1' cross join complex_entity as 't2'
```

Вывод:

| Count |
|-------|
| 30 |

## APPLY и LATERAL

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1) генерируют форму lateral-источника конкретного провайдера. Правая сторона —
это тот же набор источников, что принимает обычное соединение: типизированная сущность, производная
таблица `QueryCommand<T>`, необработанная таблица или табличная функция, — но без условия `ON`:

* [`CrossApply`](xref:NextORM.Core.EntityBuilder`1) оставляет только те строки левой стороны, для которых применяемый источник возвращает
  хотя бы одну строку (SQL Server `CROSS APPLY`, PostgreSQL/MySQL/MariaDB `CROSS JOIN LATERAL`);
* [`OuterApply`](xref:NextORM.Core.EntityBuilder`1) дополнительно сохраняет строки левой стороны с пустым применяемым источником, заполняя
  правую сторону значением `NULL` (SQL Server `OUTER APPLY`, PostgreSQL/MySQL/MariaDB
  `LEFT JOIN LATERAL ... ON true`).

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .CrossApply(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
-- SQL Server
select t1.id, t2.somestring from simple_entity as [t1] cross apply complex_entity as [t2]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t2.somestring from simple_entity as t1 cross join lateral complex_entity as t2
```

`OUTER APPLY` по производной таблице:

```csharp
var subQuery = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString });

var rows = dataContext.From<ISimpleEntity>()
    .OuterApply(subQuery)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToList();
```

```sql
-- SQL Server
... from simple_entity as [t1] outer apply (select id, somestring from complex_entity where ...) as [t2]
-- PostgreSQL / MySQL / MariaDB
... from simple_entity as t1 left join lateral (select ...) as t2 on true
```

> **Корреляция пока не выражается.** Применяемый источник не может ссылаться на столбцы строки левой
> стороны, потому что нет публичного API, позволяющего описать ссылку на внешнюю строку внутри
> подзапроса в `FROM`. До его появления [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1) эквивалентны `CROSS JOIN`/`LEFT JOIN`
> по некоррелированному источнику и отклоняются диалектами без lateral-источника
> (`SupportsApply == false`).

## Соединение с подзапросом

`QueryCommand<T>` можно присоединить напрямую. Он заключается в скобки и получает псевдоним как
производная таблица (псевдоним необязателен в SQLite и обязателен в SQL Server и PostgreSQL):

```csharp
var subQuery = dataContext.From<IComplexEntity>()
    .Where(it => it.Id == 3)
    .Select(it => new { it.Id, it.RequiredString, it.Boolean });

var rows = await dataContext.From<ISimpleEntity>()
    .Join(subQuery, (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

## Соединение с необработанной таблицей

`From("table")` создаёт неуниверсальную [`EntityBuilder`](xref:NextORM.Core.EntityBuilder), столбцы которой доступны через [`TableAlias`](xref:NextORM.Core.TableAlias). Левую
и правую стороны можно свободно смешивать с типизированными сущностями:

```csharp
var rows = dataContext
    .From("simple_entity")
    .Join(dataContext.From("complex_entity"), (s, c) => s["id"] == c["id"])
    .Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2["someString"].AsString })
    .ToList();

var mixed = dataContext
    .From("simple_entity")
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s["id"].AsInt == c.Id)
    .Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2.String })
    .ToList();
```

## Цепочки соединений и арность 2..8

Каждый вызов в цепочке добавляет одну таблицу. Условие получает накопленную на данный момент
проекцию и новую сущность:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)          // JoinedEntityBuilder<SimpleEntity, ComplexEntity>
    .Join(dataContext.From<ISimpleEntity>(), (p, s) => p.Item2.Id == s.Id)        // JoinedEntityBuilder<...>
    .Join(dataContext.From<IComplexEntity>(), (p, c) => p.Item3.Id == c.Id)       // JoinedEntityBuilder<...>
    .Select(p => new { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString })
    .ToList();
```

```sql
select t1.id as 'A', t2.requiredstring as 'B', t3.id as 'C', t4.requiredstring as 'D' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id join simple_entity as 't3' on t2.id = cast(t3.id as bigint) join complex_entity as 't4' on cast(t3.id as bigint) = t4.id
```

Тот же приём наращивает арность вплоть до `JoinedEntityBuilder<T1..T8>` (восемь таблиц). При восьми
таблицах проекция предоставляет `Item1`..[`Item8`](xref:NextORM.Core.Projection`8.Item8):

```csharp
var e = new[]
{
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
};

var sql = e[0]
    .Join(e[1], (a, b) => a.Id == b.Id)
    .Join(e[2], (p, c) => p.Item2.Id == c.Id)
    .Join(e[3], (p, c) => p.Item3.Id == c.Id)
    .Join(e[4], (p, c) => p.Item4.Id == c.Id)
    .Join(e[5], (p, c) => p.Item5.Id == c.Id)
    .Join(e[6], (p, c) => p.Item6.Id == c.Id)
    .Join(e[7], (p, c) => p.Item7.Id == c.Id)
    .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id,
                       E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id });
```

## Захваченные параметры в соединении

Захваченная локальная переменная в условии соединения становится параметром и извлекается заново
при каждом выполнении, в том числе при неявном попадании в кэш планов:

```csharp
for (var i = 1; i <= 3; i++)
{
    var id = i;
    var rows = dataContext.From<ISimpleEntity>()
        .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
        .Where(p => p.Item2.Id == id)
        .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
        .ToList();
}
```

## Специфичные для провайдера модификаторы соединения (ClickHouse)

ClickHouse добавляет два модификатора соединения, которых нет у остальных диалектов: модификатор
**строгости** (`ANY`/`ALL`/`ASOF`) и распределённый префикс `GLOBAL`. Оба применяются к только что
добавленному соединению методами [`WithStrictness`](xref:NextORM.Core.EntityBuilder`1) и
[`Global`](xref:NextORM.Core.EntityBuilder`1):

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .WithStrictness(JoinStrictness.Any)
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring from simple_entity as `t1` left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

[`JoinStrictness.Any`](xref:NextORM.Core.JoinStrictness) рендерит `<type> any join` и оставляет одну
правую строку на каждую левую; [`JoinStrictness.All`](xref:NextORM.Core.JoinStrictness) оставляет все
совпадения; [`JoinStrictness.Asof`](xref:NextORM.Core.JoinStrictness) рендерит `asof join`, для
которого нужна одна колонка равенства и завершающее неравенство.
[`Global`](xref:NextORM.Core.EntityBuilder`1) рендерит префикс `GLOBAL`, используемый в
распределённых запросах, и сочетается с модификатором строгости в любом порядке
(`global left any join`):

```csharp
var global = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Global()
    .WithStrictness(JoinStrictness.Any);
```

```sql
... from simple_entity as `t1` global left any join complex_entity as `t2` on ...
```

Оба метода копируют билдер и заменяют только его последнее соединение, поэтому исходный билдер и
предыдущие цепочки не мутируются; модификатор, заданный до следующего соединения, остаётся на том
соединении, к которому был применён. Они выбрасывают `InvalidOperationException`, если соединения
перед ними нет, а модификатор принимается только на соединениях `INNER`/`LEFT`/`RIGHT`/`FULL`
(для `CROSS`/`APPLY` — `NotSupportedException`). Модификаторы доступны только в ClickHouse
([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness),
[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)); остальные провайдеры и
контекст in-memory отклоняют их через `NotSupportedException`. Соединения `SEMI`/`ANTI`/`PASTE` не
поддерживаются. Полный каталог — в разделе [Специфичный для провайдеров SQL](provider-specific/overview.md).

## Различия между провайдерами

| Провайдер | Псевдонимы соединений | Псевдоним производной таблицы | Внешние соединения | APPLY / LATERAL |
|---|---|---|---|---|
| SQLite | `as 't1'` | необязателен | поддержаны left/right/full | не поддерживается (`NotSupportedException`) |
| SQL Server | `as [t1]` | обязателен | поддержаны left/right/full | `CROSS APPLY` / `OUTER APPLY` |
| PostgreSQL | `as "t1"` | обязателен | поддержаны left/right/full | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` |
| MySQL / MariaDB | `as \`t1\`` | обязателен | поддержаны left/right (без full) | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` |
| ClickHouse | `as \`t1\`` | обязателен | поддержаны left/right/full | не поддерживается (`NotSupportedException`) |
| In-memory | неприменимо (выполнение через делегаты) | неприменимо | поддержаны inner/left/right/full/cross; APPLY и источники в виде табличных функций — нет | не поддерживается |

Провайдер in-memory компилирует условие соединения в делегат и выполняет цикл, поэтому он не
генерирует SQL; он поддерживает соединения [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full) и [`Cross`](xref:NextORM.Core.JoinType.Cross) (см.
`tests/nextorm.core.tests/InMemoryJoinTests.cs`). [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1) существуют только для SQL и
выбрасывают `NotSupportedException` в провайдере in-memory, как и остальные неподдерживаемые типы
соединений. Соединения через `JoinedEntityBuilder<T1..T8>` разрешаются на этапе построения запроса у каждого
провайдера.

## См. также

- [Подзапросы](06-subqueries.md) - присоединённый `QueryCommand<T>` — это производная таблица.
- [Группировка и агрегаты](04-grouping-and-aggregates.md) - агрегат по соединению.
- [Хинты запросов](17-query-hints.md) - хинты уровня инструкции, например SQL Server `OPTION (RECOMPILE)`.
- [Специфичный для провайдеров SQL](provider-specific/overview.md) - полный каталог конструкций, доступных только у отдельных провайдеров.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.Join.cs:9`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:14`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:320`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:379`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:312`
(and the other provider `SqlGenerationTests.cs`).
