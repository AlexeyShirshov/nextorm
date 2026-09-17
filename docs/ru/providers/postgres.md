# Провайдер PostgreSQL

> Используйте `nextorm.postgres` для PostgreSQL; он отрисовывает параметры `@name`, разбиение на страницы `limit`/`offset`, `coalesce`, части даты `extract` и агрегаты `stddev`/`variance`, поддерживает `INTERSECT ALL`/`EXCEPT ALL` и требует псевдонимов в двойных кавычках.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

`PostgresDbContext` (`src/nextorm.postgres/PostgresDbContext.cs`) оборачивает `Npgsql`. Он создаёт
`NpgsqlConnection` и передаёт значения параметров null как `DBNull` (Npgsql отклоняет значение параметра null).

`PostgresDialect` (`src/nextorm.postgres/PostgresDialect.cs`) — это диалект:

- плейсхолдер параметра `@name`;
- конкатенация строк с помощью `||`;
- `MakeCoalesce` отрисовывает `coalesce(a, b)`;
- логические литералы — `true`/`false`;
- идентификаторы используют двойные кавычки (`Escape` возвращает `"name"`), и `MakeColumnReference` тоже квотирует имя,
  чтобы квотированный псевдоним сохранялся, когда на него ссылаются из внешнего запроса;
- производные таблицы и табличные функции должны иметь псевдонимы (`RequireSubqueryAlias` равно `true`, и его
  следствием является то, что псевдоним выдаётся всегда);
- `INTERSECT ALL` / `EXCEPT ALL` поддерживаются (`SupportsIntersectExceptAll` равно `true`);
- имена агрегатов переотображаются: `stdev`→`stddev`, `stdevp`→`stddev_pop`, `var`→`variance`,
  `varp`→`var_pop`;
- `Math.Log` отображается на `ln(...)` (в PostgreSQL `log()` — это логарифм по основанию 10);
- `DateTime.Now` отрисовывает `now()`, `DateTime.UtcNow` отрисовывает `now() at time zone 'utc'`;
- части даты отрисовываются как `extract(part from value)`;
- разбиение на страницы — `limit n` / `limit n offset m`; запрос только с offset выдаёт только `offset m`.

## Регистрация провайдера

На `DbContextBuilder` доступны две перегрузки
(`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.postgres;

var byString = new DbContextBuilder().UsePostgres("Host=localhost;Database=app;Username=app;Password=secret");

using var connection = new Npgsql.NpgsqlConnection("Host=localhost;Database=app;...");
var byConnection = new DbContextBuilder().UsePostgres(connection);

using var ctx = byString.CreateDbContext();   // IDataContext
```

Напрямую:

```csharp
using nextorm.core;
using nextorm.postgres;

using IDataContext ctx = new PostgresDbContext("Host=localhost;Database=app;...", new DbContextBuilder());
```

## Разбиение на страницы

```csharp
ctx.Create<ISimpleEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.Create<ISimpleEntity>().Offset(10).Select(x => x.Id);    // offset 10
```

```sql
select id from simple_entity limit 5 offset 10
select id from simple_entity offset 10
```

`OFFSET` может появляться сам по себе, но `LIMIT` должен идти первым, когда присутствуют оба. Поскольку PostgreSQL
не требует внедрённой сортировки, `ORDER BY` не добавляется.

## Coalesce, части даты и агрегаты

```csharp
var query = ctx.Create<IComplexEntity>()
    .Select(x => new
    {
        Fallback = x.String ?? "",
        Year = x.Datetime!.Value.Year,
    });
```

```sql
select coalesce(somestring, '') as "Fallback", extract(year from dt) as "Year"
from complex_entity
```

```csharp
var stdev = ctx.Create<IComplexEntity>().Select(x => NORM.SQL.stdev((double)x.Id));  // stddev(...)
var varp  = ctx.Create<IComplexEntity>().Select(x => NORM.SQL.varp((double)x.Id));   // var_pop(...)
```

`count` и `count_big` оба отрисовывают `count(*)`, потому что `count` в PostgreSQL уже возвращает 64-битное
целое.

## Операции над множествами `*ALL` и порядок null

PostgreSQL — единственный поддерживаемый реляционный провайдер, реализующий `INTERSECT ALL` и `EXCEPT ALL`,
поэтому `IntersectAll`/`ExceptAll` отрисовывают свой SQL напрямую.

```csharp
var q = a.Select(x => x.Id).IntersectAll(b.Select(x => x.Id));   // ... intersect all ...
```

PostgreSQL рассматривает `NULL` как наибольшее значение, поэтому `ORDER BY … DESC` ставит группу `NULL` первой. Общий
набор тестов избегает зависимости от этого; тест провайдера фиксирует это явно.

```csharp
var r = ctx.Create<IComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToList();
// r[0].Id == 1 (the row whose Int is NULL)
```

## Псевдонимы

Производные таблицы и табличные функции должны иметь псевдонимы в двойных кавычках:

```sql
select t1.value, t2.somestring as "String"
from all_rows() as "t1"
join complex_entity as "t2" on t1.id = t2.id
```

## Различия провайдеров

| Аспект | PostgreSQL |
|---|---|
| Плейсхолдер параметра | `@name` |
| Разбиение на страницы | `limit n` / `limit n offset m` / `offset m` |
| Внедряемая сортировка при разбиении на страницы | нет |
| Concat | `||` |
| Coalesce | `coalesce` |
| Логический литерал | `true` / `false` |
| Квотирование идентификаторов | двойные кавычки (`as "t1"`) |
| Псевдоним производной таблицы / TVF | требуется |
| `*ALL` | поддерживается |
| Рекурсивный CTE | `with recursive` (без опции max-recursion) |
| `stdev` / `stdevp` | `stddev` / `stddev_pop` |
| `var` / `varp` | `variance` / `var_pop` |
| `DateTime.Now` / `UtcNow` | `now()` / `now() at time zone 'utc'` |
| Размещение null при `ORDER BY … DESC` | null сортируются первыми |

## См. также

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `test/nextorm.postgres.tests/PostgresDialectTests.cs:21,27,41,49`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:117,130,139,151,160,172,199,211,223,248,788,976`,
`test/nextorm.integration.tests/PostgresSpecificTests.cs:15,24`,
`src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.postgres/PostgresDbContext.cs`,
`src/nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs`.
