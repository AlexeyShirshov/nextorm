# Провайдер SQLite

> Используйте `nextorm.sqlite` для файловых или in-memory баз данных SQLite; он отрисовывает параметры `$name`, разбиение на страницы `limit`/`offset`, coalesce `ifnull` и части даты `strftime`, а также регистрирует пользовательские агрегаты `stdev`/`var`.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

`SqliteDbContext` (`src/nextorm.sqlite/SqliteDbContext.cs`) оборачивает `Microsoft.Data.Sqlite`. Он создаёт
`SqliteConnection` из строки подключения, регистрирует пользовательские агрегатные функции на каждом
соединении, которое создаёт, и возвращает `SqliteDialect.Instance` из своего свойства `Dialect`.

`SqliteDialect` (`src/nextorm.sqlite/SqliteDialect.cs`) — это диалект:

- плейсхолдер параметра `$name`;
- конкатенация строк с помощью `||`;
- `MakeCoalesce` отрисовывает `ifnull(a, b)`;
- `MakeNow` отрисовывает `datetime('now')` и для локального, и для UTC (`SQLite has no now()`);
- части даты используют `cast(strftime(...) as integer)` для `year`/`month`/`day`/`hour`/`minute`/`second`,
  с откатом к ANSI `extract(part from value)` для всего остального;
- `Math.Log` отображается на `ln(...)` (в SQLite `log()` — это логарифм по основанию 10);
- разбиение на страницы — `limit n` / `limit n offset m`; offset без limit становится `limit -1 offset m`.

## Регистрация провайдера

На `DbContextBuilder` доступны две перегрузки
(`src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.sqlite;

// From a file path (the debug build checks that the file exists).
var byPath = new DbContextBuilder().UseSqlite("app.db");

// From an existing, caller-owned connection.
using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
var byConnection = new DbContextBuilder().UseSqlite(connection);

using var ctx = byPath.CreateDbContext();   // IDataContext
```

Вы также можете создать контекст напрямую (это делают тесты провайдера):

```csharp
using nextorm.core;
using nextorm.sqlite;

using IDataContext ctx = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());
```

## Пользовательские агрегатные функции

В Microsoft.Data.Sqlite нет авторегистрации на основе атрибутов, поэтому основная библиотека регистрирует свои пользовательские
агрегаты явно и один раз на соединение
(`src/nextorm.sqlite/SQLiteFunctions.cs`, вызывается из `SqliteDbContext.OnConnectionCreated`):

| Функция | Значение |
|---|---|
| `stdev` | выборочное стандартное отклонение (n−1) |
| `stdevp` | генеральное стандартное отклонение (n) |
| `var` | выборочная дисперсия (n−1) |
| `varp` | генеральная дисперсия (n) |

Все четыре используют один `VarianceAccumulator`; вариант возвращает `null`, когда непустых строк слишком мало
(меньше 2 для выборочного варианта, меньше 1 для генерального). Поскольку функции
уже названы `stdev`/`var`, SQLite не выполняет переименование агрегатов.

```csharp
var stddev = ctx.Create<IComplexEntity>()
    .Select(x => NORM.SQL.stdev((double)x.Id))
    .First();
```

```sql
select stdev(cast(id as double precision)) from complex_entity
```

## Части даты, coalesce и `LIKE`

```csharp
var query = ctx.Create<IComplexEntity>()
    .Select(x => new
    {
        Year = x.Datetime!.Value.Year,
        Fallback = x.String ?? "",
    });
```

```sql
select cast(strftime('%Y', dt) as integer) as 'Year', ifnull(somestring, '') as 'Fallback'
from complex_entity
```

`NORM.SQL.like(column, pattern)` и `NORM.SQL.like(column, pattern, escapeChar)` отрисовывают предикат
`like`; строковые методы (`Contains`, `StartsWith`, `EndsWith`) транслируются в `like` с
соответствующими подстановочными знаками.

## Разбиение на страницы

```csharp
ctx.Create<IComplexEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.Create<IComplexEntity>().Offset(10).Select(x => x.Id);    // limit -1 offset 10
```

```sql
select id from complex_entity limit 5 offset 10
select id from complex_entity limit -1 offset 10
```

В SQLite нет `OFFSET` без `LIMIT`, поэтому запрос только с offset выдаёт сигнальное значение `limit -1`.

## Ограничения

В SQLite нет поддержки подзапросов `ANY`/`ALL`. `NORM.SQL.any(...)` / `NORM.SQL.all(...)` транслируются в
SQL, и база данных отклоняет их во время выполнения с `SqliteException` — сбой не возникает
во время трансляции.

```csharp
// Throws Microsoft.Data.Sqlite.SqliteException when executed.
await ctx.Create<ISimpleEntity>()
    .Where(it => it.Id == NORM.SQL.any(ctx.Create<IComplexEntity>().Select(c => c.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

`IntersectAll` и `ExceptAll` отклоняются диалектом с `NotSupportedException`, потому что в SQLite
нет `intersect all` / `except all`.

## Различия провайдеров

| Аспект | SQLite |
|---|---|
| Плейсхолдер параметра | `$name` |
| Concat | `||` |
| Coalesce | `ifnull` |
| Логический литерал | `1` / `0` |
| Квотирование идентификаторов | одинарные кавычки (`as 't1'`) |
| Псевдоним производной таблицы | не требуется |
| Псевдоним TVF | не требуется |
| `*ALL` | не поддерживается |
| Подзапросы `ANY`/`ALL` | отклоняются базой данных при выполнении |

## См. также

- [Provider overview](overview.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `test/nextorm.sqlite.tests/SqliteDialectTests.cs:23,29,42,50`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:120,133,142,152,161,173,204,217,892`,
`test/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`src/nextorm.sqlite/SqliteDialect.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs`,
`src/nextorm.sqlite/SqliteDbContext.cs`, `src/nextorm.sqlite/DI/DataContextOptionsBuilderExtensions.cs`.
