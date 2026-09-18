# Подзапросы

> Используйте `QueryCommand<T>` как источник `FROM`, как скалярное значение в проекции, `WHERE` или `ORDER BY`, либо как коррелированный предикат `EXISTS` / `IN` / `ANY` / `ALL`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md) · [Соединения](03-joins.md)

## Обзор

Любой `QueryCommand<T>` — объект, который возвращает `EntityBuilder<T>.Select(...)`, — можно встроить в
другой запрос четырьмя способами:

* как **производную таблицу** в `FROM`, через `DataContext.From(query)`;
* как **скалярный подзапрос** в проекции, `WHERE` или `ORDER BY`, вызывая терминал для одной строки,
  такой как `First()` или `Single()`, внутри внешнего выражения;
* как **коррелированный предикат** с `NORM.SQL.exists(...)`, `NORM.SQL.@in(column, query)`,
  `NORM.SQL.any(query)` или `NORM.SQL.all(query)`.

Вложенный запрос подготавливается независимо и отображается в круглых скобках. Коррелированный
подзапрос может ссылаться на параметр внешнего запроса; nextorm отслеживает эти внешние ссылки и
квалифицирует их псевдонимом внешней таблицы.

Об одном ограничении провайдера стоит знать заранее: `NORM.SQL.any` и `NORM.SQL.all` являются
допустимым SQL только в SQL Server и PostgreSQL. В SQLite нет ни одного из этих операторов, поэтому
запрос доходит до базы данных и завершается с `SqliteException` во время выполнения.

## Подзапрос как источник FROM

`From` принимает подготовленный `QueryCommand<T>` (или `EntityBuilder<T>`) и создаёт построитель по его
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

Вызов терминала для одной строки (`First`, `FirstOrDefault`, `Single`, `SingleOrDefault`) внутри
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

Внутренний запрос также можно отсортировать:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().OrderByDescending(it => it.Id).Select(it => it.Id).First() })
    .ToListAsync();
```

## Скалярный подзапрос в WHERE

Тот же терминал для одной строки, использованный в предикате, становится скалярным подзапросом в
правой части:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => it.Id == dataContext.From<ISimpleEntity>().OrderBy(it => it.Id).Select(it => it.Id).First())
    .Select(it => new { it.Id })
    .FirstOrDefaultAsync();
```

## Скалярный подзапрос в ORDER BY

Ключ `ORDER BY` может быть выражением, содержащим скалярный подзапрос. Первый `OrderBy` использует
подзапрос, второй разрешает равенство:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(_ => dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First())
    .OrderBy(it => it.Id)
    .Select(it => new { it.Id })
    .ToList();
```

## Коррелированный EXISTS

`NORM.SQL.exists(query)` возвращает логическое значение и транслируется в предикат `exists(...)`.
Подзапрос, ссылающийся на внешний параметр, является коррелированным; nextorm генерирует внешний
псевдоним внутри внутреннего предиката:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Where(s => NORM.SQL.exists(dataContext.From<IComplexEntity>().Where(c => c.Id == s.Id)))
    .Select(it => it.Id)
    .ToList();
```

```sql
select t1.id from simple_entity as 't1' where exists(select * from complex_entity where (id = cast(t1.id as bigint)))
```

`EntityBuilder<T>` также можно передать напрямую в `exists`, когда важно лишь её существование:

```csharp
var all = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = NORM.SQL.exists(dataContext.From<ISimpleEntity>()) }).ToListAsync();
var none = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = NORM.SQL.exists(dataContext.From<ISimpleEntity>().Where(it => it.Id == 100)) }).ToListAsync();
```

## IN с подзапросом

`NORM.SQL.@in(column, query)` генерирует `IN (SELECT ...)`:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => NORM.SQL.@in((int)it.Id, dataContext.From<ISimpleEntity>().Where(it => it.Id == 2).Select(it => it.Id)))
    .Select(it => it.Id)
    .FirstOrDefaultAsync();
```

```sql
select id from complex_entity where (cast(id as integer) in (select id from simple_entity where (id = 2)))
```

Тот же метод также принимает `IEnumerable<T>` или `params T[]` литеральных значений (см.
[Фильтрация (WHERE)](02-filtering-where.md)); перегрузка с `QueryCommand<T>` — это форма подзапроса.

## ANY и ALL

`NORM.SQL.any(query)` и `NORM.SQL.all(query)` создают скаляр, который сравнивается с внешним
столбцом:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id == NORM.SQL.any(dataContext.From<IComplexEntity>().Select(it => it.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
-- SQL Server / PostgreSQL
select id from simple_entity where (cast(id as bigint) = any(select id from complex_entity))
```

SQLite не реализует `ANY` или `ALL`; тот же запрос принимается транслятором, но при выполнении
выбрасывает `Microsoft.Data.Sqlite.SqliteException` (покрыто в
`test/nextorm.integration.tests/SqliteSpecificTests.cs:16` и `:30`).

## Различия между провайдерами

| Провайдер | Псевдоним производной таблицы | Скалярный подзапрос | `any` / `all` |
|---|---|---|---|
| SQLite | необязателен | поддерживается | **не поддерживается** - `SqliteException` при выполнении |
| SQL Server | обязателен (`as [t1]`) | поддерживается | поддерживается |
| PostgreSQL | обязателен (`as "t1"`) | поддерживается | поддерживается |
| In-memory | неприменимо | не покрыто набором тестов in-memory | не покрыто |

Подзапрос, ссылающийся на внешний запрос, вынуждает присваивать псевдоним (`t1`) внешнему `FROM` у
каждого SQL-провайдера. В SQL Server булев предикат подзапроса, проецируемый как скаляр,
оборачивается в `cast(case when ... then 1 else 0 end as bit)`, потому что в T-SQL нет логического
скалярного типа.

## См. также

- [Соединения](03-joins.md) - соединение `QueryCommand<T>` как производной таблицы.
- [Операции над множествами](07-set-operations.md)
- [Фильтрация (WHERE)](02-filtering-where.md) - `IN` по списку литералов.

---

Source: `test/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:612,623,633,643,653,663`,
`test/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:248`.
