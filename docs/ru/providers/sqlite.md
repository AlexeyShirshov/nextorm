# Провайдер SQLite

> Используйте `nextorm.sqlite` для файловых или in-memory баз данных SQLite; он отрисовывает параметры `$name`, разбиение на страницы `limit`/`offset`, coalesce `ifnull` и части даты `strftime`, а также регистрирует пользовательские агрегаты `stdev`/`var`.

**Предварительные требования:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

[`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext) (`src/nextorm.sqlite/SqliteDataContext.cs`) оборачивает `Microsoft.Data.Sqlite`. Он создаёт
`SqliteConnection` из строки подключения, регистрирует пользовательские агрегатные функции на каждом
соединении, которое создаёт, и возвращает [`Instance`](xref:NextORM.Sqlite.SqliteDialect.Instance) из своего свойства `Dialect`.

[`SqliteDialect`](xref:NextORM.Sqlite.SqliteDialect) (`src/nextorm.sqlite/SqliteDialect.cs`) — это диалект:

- плейсхолдер параметра `$name`;
- конкатенация строк с помощью `||`;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect.MakeCoalesce(System.String,System.String)) отрисовывает `ifnull(a, b)`;
- [`MakeNow`](xref:NextORM.Core.ISqlDialect.MakeNow(System.Boolean)) отрисовывает `datetime('now')` и для локального, и для UTC (`SQLite has no now()`);
- части даты используют `cast(strftime(...) as integer)` для `year`/`month`/`day`/`hour`/`minute`/`second`
  и `dayofyear` (`%j`), с откатом к ANSI `extract(part from value)` для всего остального;
- `Math.Log` отображается на `ln(...)` (в SQLite `log()` — это логарифм по основанию 10);
- разбиение на страницы — `limit n` / `limit n offset m`; offset без limit становится `limit -1 offset m`.

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.Sqlite;

// From a file path (the debug build checks that the file exists).
var byPath = new DataContextBuilder().UseSqlite("app.db");

// From an existing, caller-owned connection.
using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
var byConnection = new DataContextBuilder().UseSqlite(connection);

using var ctx = byPath.CreateDataContext();   // IDataContext
```

Вы также можете создать контекст напрямую (это делают тесты провайдера):

```csharp
using NextORM.Core;
using NextORM.Sqlite;

using IDataContext ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());
```

## Пользовательские агрегатные функции

В Microsoft.Data.Sqlite нет авторегистрации на основе атрибутов, поэтому основная библиотека регистрирует свои пользовательские
агрегаты явно и один раз на соединение
(`src/nextorm.sqlite/SQLiteFunctions.cs`, вызывается из [`OnConnectionCreated`](xref:NextORM.Sqlite.SqliteDataContext.OnConnectionCreated(System.Data.Common.DbConnection))):

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
var stddev = ctx.From<IComplexEntity>()
    .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
    .First();
```

```sql
select stdev(cast(id as double precision)) from complex_entity
```

## Части даты, coalesce и `LIKE`

```csharp
var query = ctx.From<IComplexEntity>()
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

`SqlFunctions.Sql.like(column, pattern)` и `SqlFunctions.Sql.like(column, pattern, escapeChar)` отрисовывают предикат
`like`; строковые методы (`Contains`, `StartsWith`, `EndsWith`) транслируются в `like` с
соответствующими подстановочными знаками.

## Разбиение на страницы

```csharp
ctx.From<IComplexEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<IComplexEntity>().Offset(10).Select(x => x.Id);    // limit -1 offset 10
```

```sql
select id from complex_entity limit 5 offset 10
select id from complex_entity limit -1 offset 10
```

В SQLite нет `OFFSET` без `LIMIT`, поэтому запрос только с offset выдаёт сигнальное значение `limit -1`.

## Функции, специфичные для SQLite

`SqlFunctions.Sqlite` даёт поверхность, специфичную для SQLite: функции ядра, JSON1, функции дат и
функции математического расширения. `json_each`/`json_tree` доступны как табличные функции. Прочие
провайдеры отклоняют любой член поверхности с `NotSupportedException`.

```csharp
ctx.From<IComplexEntity>()
    .Select(x => new
    {
        Json = SqlFunctions.Sqlite.json_extract<string>(x.String, "$.name"),
        Kind = SqlFunctions.Sqlite.@typeof(x.String),
        Pi = SqlFunctions.Sql.pi()
    });
```

Полный список, нативное написание в SQLite и требования к версии/опциям сборки — в разделе
[Специфичный для SQLite SQL](../guide/provider-specific/sqlite.md).

## Ограничения

В SQLite нет поддержки подзапросов `ANY`/`ALL`. `SqlFunctions.Sql.any(...)` / `SqlFunctions.Sql.all(...)` транслируются в
SQL, и база данных отклоняет их во время выполнения с `SqliteException` — сбой не возникает
во время трансляции.

```csharp
// Throws Microsoft.Data.Sqlite.SqliteException when executed.
await ctx.From<ISimpleEntity>()
    .Where(it => it.Id == SqlFunctions.Sql.any(ctx.From<IComplexEntity>().Select(c => c.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

[`IntersectAll`](xref:NextORM.Core.QueryCommand`1.IntersectAll``1(NextORM.Core.QueryCommand{``0})) и [`ExceptAll`](xref:NextORM.Core.QueryCommand`1.ExceptAll``1(NextORM.Core.QueryCommand{``0})) отклоняются диалектом с `NotSupportedException`, потому что в SQLite
нет `intersect all` / `except all`.

## Различия провайдеров

| Аспект | SQLite |
|---|---|
| Плейсхолдер параметра | `$name` |
| Concat | `||` |
| Coalesce | `ifnull` |
| `greatest` / `least` | `max` / `min` (скалярные, 2+ аргумента; возвращают NULL, если хотя бы один аргумент NULL) |
| Условная функция | `iif(cond, a, b)` (SQLite 3.32+) |
| Оконные функции | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` поддерживаются |
| Логический литерал | `1` / `0` |
| Квотирование идентификаторов | одинарные кавычки (`as 't1'`) |
| Псевдоним производной таблицы | не требуется |
| Псевдоним TVF | не требуется |
| `*ALL` | не поддерживается |
| Подзапросы `ANY`/`ALL` | отклоняются базой данных при выполнении |
| Session/info-функции | `version()` → `sqlite_version()` (нет информации о пользователе/схеме/БД) |

## См. также

- [Provider overview](overview.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.sqlite.tests/SqliteDialectTests.cs:23,29,42,50`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:120,133,142,152,161,173,204,217,892`,
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:16,30`,
`src/nextorm.sqlite/SqliteDialect.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs`,
`src/nextorm.sqlite/SqliteDataContext.cs`, `src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs`.
