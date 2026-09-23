# Подзапросы

> Используйте `QueryCommand<T>` как источник `FROM`, как скалярное значение в проекции, `WHERE`, `ORDER BY` или `HAVING`, либо как коррелированный предикат `EXISTS` / `IN` / `ANY` / `ALL`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md) · [Соединения](03-joins.md)

## Обзор

Любой `QueryCommand<T>` — объект, который возвращает `EntityBuilder<T>.Select(...)`, — можно встроить в
другой запрос четырьмя способами:

* как **производную таблицу** в `FROM`, через [`From`](xref:NextORM.Core.DataContext.From(System.String));
* как **скалярный подзапрос** в проекции, `WHERE`, `ORDER BY` или `HAVING`, вызывая терминал для одной строки,
  такой как [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) или [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})), внутри внешнего выражения;
* как **коррелированный предикат** с `SqlFunctions.Sql.exists(...)`, `SqlFunctions.Sql.@in(column, query)`,
  `SqlFunctions.Sql.any(query)` или `SqlFunctions.Sql.all(query)`.

Вложенный запрос подготавливается независимо и отображается в круглых скобках. Коррелированный
подзапрос может ссылаться на параметр внешнего запроса; nextorm отслеживает эти внешние ссылки и
квалифицирует их псевдонимом внешней таблицы.

Об одном ограничении провайдера стоит знать заранее: `SqlFunctions.Sql.any` и `SqlFunctions.Sql.all` являются
допустимым SQL только в SQL Server и PostgreSQL. В SQLite нет ни одного из этих операторов, поэтому
запрос доходит до базы данных и завершается с `SqliteException` во время выполнения.

## Подзапрос как источник FROM

[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) принимает подготовленный `QueryCommand<T>` (или `EntityBuilder<T>`) и создаёт построитель по его
столбцам:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id });
var rows = dataContext.From(nested).Select(t => new { t.Id }).ToList();
```

```sql
-- SQLite (derived tables do not require an alias)
select id from (select id from complex_entity)
```

```sql
-- SQL Server / PostgreSQL (the derived table is aliased)
select id from (select id from complex_entity) as [t1]
```

Производную таблицу можно так же проецировать, фильтровать и соединять, как любой источник.
Столбец, переименованный во внутренней проекции, снаружи упоминается по своему спроецированному
имени:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id, Calc = x.String + x.String });
var rows = dataContext.From(nested).Select(t => new { t.Id, t.Calc }).ToList();
```

## Скалярный подзапрос в SELECT

Вызов терминала для одной строки ([`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})), [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})), [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0}))) внутри
проекции встраивает внутренний запрос как скалярный столбец. Скалярной проекции присваивается
псевдоним по имени внешнего свойства:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First() })
    .ToListAsync();
```

```sql
select id, (select id from simple_entity where (id = 1) limit 1) as 'sid' from complex_entity
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id | sid |
|----|-----|
| 1 | 1 |
| 2 | 1 |
| 3 | 1 |

Внутренний запрос также можно отсортировать:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().OrderByDescending(it => it.Id).Select(it => it.Id).First() })
    .ToListAsync();
```

Вывод:

| Id | sid |
|----|-----|
| 1 | 10 |
| 2 | 10 |
| 3 | 10 |

## Скалярный подзапрос в WHERE

Тот же терминал для одной строки, использованный в предикате, становится скалярным подзапросом в
правой части:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => it.Id == dataContext.From<ISimpleEntity>().OrderBy(it => it.Id).Select(it => it.Id).First())
    .Select(it => new { it.Id })
    .FirstOrDefaultAsync();
```

Вывод:

| Id |
|----|
| 1 |

## Скалярный подзапрос в ORDER BY

Ключ `ORDER BY` может быть выражением, содержащим скалярный подзапрос. Первый [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) использует
подзапрос, второй разрешает равенство:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(_ => dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First())
    .OrderBy(it => it.Id)
    .Select(it => new { it.Id })
    .ToList();
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |

## Коррелированный скалярный подзапрос

Скалярный подзапрос может ссылаться на член внешнего запроса; nextorm квалифицирует его псевдонимом
внешней таблицы. Это работает у каждого SQL-провайдера и в любой позиции значения (проекция,
`WHERE`, `ORDER BY`, `HAVING`):

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().Where(s => s.Id == it.Id).Select(s => s.Id).First() })
    .ToListAsync();
```

```sql
select t1.id, (select t2.id from simple_entity as 't2' where t2.id = t1.id limit 1) as 'sid'
from complex_entity as 't1'
```

Внешний запрос квалифицирует только упомянутый член; внутренний запрос сохраняет свои столбцы и
псевдонимы. [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0}))/[`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) дают `NULL`, когда во внутреннем запросе нет строк.

Упомянутый член может также приходить из join-проекции: `p.Item1.Id` определяет псевдоним таблицы
по позиции элемента проекции, а столбец — по его разметке, поэтому подзапрос может коррелировать с
любым из соединённых источников:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, cid = dataContext.From<IComplexEntity>().Where(c => c.Id == p.Item1.Id).Select(c => c.Id).First() })
    .ToListAsync();
```

```sql
select t1.id, (select top(1) t3.id from complex_entity as [t3]
 where t3.id = cast(t1.id as bigint)) as [cid] from simple_entity as [t1] join complex_entity as [t2] on cast(t1.id as bigint) = t2.id
```

Подзапрос может также стоять в `HAVING`, где он может ссылаться на ключ группировки. Он
подготавливается тем же коррелированным visitor'ом, что и `WHERE`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Having(g => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Int == g.Int)))
    .Select(g => new { g.Int, count = SqlFunctions.Sql.count() })
    .ToListAsync();
```

Внешняя ссылка может быть обёрнута в скалярную функцию над внешним столбцом (например
`e.String.ToUpper()`); функция отображается вокруг квалифицированного внешнего псевдонима так же,
как и любое другое выражение.

Агрегатный терминал ([`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})), `Sum(...)`, `Min`/`Max`/`Avg`, ...)
транслируется в соответствующий SQL-агрегат над подзапросом, поэтому его можно использовать прямо с
внешней ссылкой:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, cnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Count() })
    .ToListAsync();
```

```sql
select t1.id, (select count(*) from complex_entity as 't2'
 where cast(t2.id as bigint) = t1.id
limit 1) as 'cnt' from complex_entity as 't1'
```

Корреляция вкладывается произвольно: коррелированный подзапрос может сам содержать коррелированный
подзапрос, и каждая внешняя ссылка разрешается в псевдоним того scope, который её объявил.

На in-memory провайдере коррелированный подзапрос тоже вычисляется один раз на внешнюю строку, поэтому
скалярные подзапросы, агрегатные терминалы, `EXISTS` и `IN` работают там и для корреляции глубины
один. По-прежнему отклоняются с `NotSupportedException`: глубина корреляции больше одной, ссылка на
внешнюю строку во внутренней проекции или `ORDER BY`, коррелированный `GROUP BY`/`HAVING` и
асинхронный источник. Для этих форм используйте SQL-провайдер.

На SQLite числовой `Single`/`SingleOrDefault` при более чем одной строке поднимает ошибку БД через
рендер-гард по количеству строк (провайдер не проверяет кардинальность скалярного подзапроса);
нечисловая проекция отклоняется с `NotSupportedException`.

## Коррелированный EXISTS

`SqlFunctions.Sql.exists(query)` возвращает логическое значение и транслируется в предикат `exists(...)`.
Подзапрос, ссылающийся на внешний параметр, является коррелированным; nextorm генерирует внешний
псевдоним внутри внутреннего предиката:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Where(s => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Id == s.Id)))
    .Select(it => it.Id)
    .ToList();
```

```sql
select t1.id from simple_entity as 't1' where exists(select * from complex_entity where (id = cast(t1.id as bigint)))
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |

`EntityBuilder<T>` также можно передать напрямую в `exists`, когда важно лишь её существование:

```csharp
var all = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(dataContext.From<ISimpleEntity>()) }).ToListAsync();
var none = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(dataContext.From<ISimpleEntity>().Where(it => it.Id == 100)) }).ToListAsync();
```

## IN с подзапросом

`SqlFunctions.Sql.@in(column, query)` генерирует `IN (SELECT ...)`:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => SqlFunctions.Sql.@in((int)it.Id, dataContext.From<ISimpleEntity>().Where(it => it.Id == 2).Select(it => it.Id)))
    .Select(it => it.Id)
    .FirstOrDefaultAsync();
```

```sql
select id from complex_entity where (cast(id as integer) in (select id from simple_entity where (id = 2)))
```

Вывод:

| Id |
|----|
| 2 |

Тот же метод также принимает `IEnumerable<T>` или `params T[]` литеральных значений (см.
[Фильтрация (WHERE)](02-filtering-where.md)); перегрузка с `QueryCommand<T>` — это форма подзапроса.

## ANY и ALL

`SqlFunctions.Sql.any(query)` и `SqlFunctions.Sql.all(query)` создают скаляр, который сравнивается с внешним
столбцом:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id == SqlFunctions.Sql.any(dataContext.From<IComplexEntity>().Select(it => it.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
-- SQL Server / PostgreSQL
select id from simple_entity where (cast(id as bigint) = any(select id from complex_entity))
```

SQLite не реализует `ANY` или `ALL`; тот же запрос принимается транслятором, но при выполнении
выбрасывает `Microsoft.Data.Sqlite.SqliteException` (покрыто в
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:16` и `:30`).

## Различия между провайдерами

| Провайдер | Псевдоним производной таблицы | Скалярный подзапрос | `any` / `all` |
|---|---|---|---|
| SQLite | необязателен | поддерживается (в т. ч. коррелированный) | **не поддерживается** - `SqliteException` при выполнении |
| SQL Server | обязателен (`as [t1]`) | поддерживается (в т. ч. коррелированный) | поддерживается |
| PostgreSQL | обязателен (`as "t1"`) | поддерживается (в т. ч. коррелированный) | поддерживается |
| MySQL | обязателен (`` as `t1` ``) | поддерживается (в т. ч. коррелированный) | поддерживается |
| MariaDB | обязателен (`` as `t1` ``) | поддерживается (в т. ч. коррелированный) | поддерживается |
| ClickHouse | обязателен (`` as `t1` ``) | поддерживается (в т. ч. коррелированный) | поддерживается |
| In-memory | неприменимо | поддерживается (коррелированный скаляр/`EXISTS`/`IN` глубины один); более глубокие формы выбрасывают `NotSupportedException` | не покрыто |

Подзапрос, ссылающийся на внешний запрос, вынуждает присваивать псевдоним (`t1`) внешнему `FROM` у
каждого SQL-провайдера. В SQL Server булев предикат подзапроса, проецируемый как скаляр,
оборачивается в `cast(case when ... then 1 else 0 end as bit)`, потому что в T-SQL нет логического
скалярного типа.

## Ограничения

* Вложенная корреляция (подзапрос, ссылающийся на внешнюю ссылку другого подзапроса) отклоняется с
  `NotSupportedException`; явная ошибка предотвращает привязку внешнего маркера к чужому запросу.
* In-memory провайдер поддерживает коррелированный скаляр/`EXISTS`/`IN` глубины один и отклоняет более глубокие формы, перечисленные выше.

## См. также

- [Соединения](03-joins.md) - соединение `QueryCommand<T>` как производной таблицы.
- [Операции над множествами](07-set-operations.md)
- [Фильтрация (WHERE)](02-filtering-where.md) - `IN` по списку литералов.

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs`,
`tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:612,623,633,643,653,663`,
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:248`.
