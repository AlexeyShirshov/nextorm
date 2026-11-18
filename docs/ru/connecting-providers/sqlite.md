# Провайдер SQLite

> Используйте `nextorm.sqlite` для файловой базы данных или базы в памяти. Провайдер формирует SQL с параметрами `$name`, постраничной выборкой `limit`/`offset`, `ifnull` и частями даты `strftime`, а также регистрирует пользовательские агрегаты `stdev`/`var`.

**Перед началом:** [Обзор провайдеров](overview.md) · [Быстрый старт](../getting-started/02-quickstart.md)

## Регистрация провайдера

Выберите перегрузку со строкой подключения, если контекст должен сам создать соединение и владеть
им. Передайте существующий `DbConnection`, если им владеет приложение. В первом примере используется
путь к файлу; в сборках `DEBUG` `UseSqlite(string)` также проверяет, что файл существует:

```csharp
using NextORM.Core;
using NextORM.Sqlite;

var builder = new DataContextBuilder().UseSqlite("app.db");
using var ctx = builder.CreateDataContext();
```

Чтобы повторно использовать соединение, которым владеет вызывающий код, передайте его явно:

```csharp
using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
var builder = new DataContextBuilder().UseSqlite(connection);
using var ctx = builder.CreateDataContext();
```

Можно также создать контекст напрямую:

```csharp
using NextORM.Core;
using NextORM.Sqlite;

using IDataContext ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());
```

## Обзор

[`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext) (`src/nextorm.sqlite/SqliteDataContext.cs`) оборачивает `Microsoft.Data.Sqlite`. Он создаёт
`SqliteConnection` из строки подключения, регистрирует пользовательские агрегатные функции на каждом
соединении, которое создаёт, и возвращает [`Instance`](xref:NextORM.Sqlite.SqliteDialect.Instance) из своего свойства `Dialect`.

[`SqliteDialect`](xref:NextORM.Sqlite.SqliteDialect) (`src/nextorm.sqlite/SqliteDialect.cs`) — это диалект:

- маркер параметра `$name`;
- конкатенация строк с помощью `||`;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect.MakeCoalesce(System.String,System.String)) формирует `ifnull(a, b)`;
- [`MakeNow`](xref:NextORM.Core.ISqlDialect.MakeNow(System.Boolean)) формирует `datetime('now')` и для локального, и для UTC (`SQLite has no now()`);
- части даты используют `cast(strftime(...) as integer)` для `year`/`month`/`day`/`hour`/`minute`/`second`
  и `dayofyear` (`%j`), с откатом к ANSI `extract(part from value)` для всего остального;
- `Math.Log` отображается на `ln(...)` (в SQLite `log()` — это логарифм по основанию 10);
- разбиение на страницы — `limit n` / `limit n offset m`; offset без limit становится `limit -1 offset m`.

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

Это обещание null сохраняется и на границе CLR: четыре терминала на сущности и формы фасада
`Sql.stdev*`/`Sql.var*` возвращают `T?`, поэтому `null` из аккумулятора доходит до вызывающего как
`null`, а не бросает исключение на non-nullable `double`. Генеральный результат по одной строке
по-прежнему `0.0`, а дубликаты, свёрнутые `DISTINCT`, подчиняются тому же правилу кардинальности,
что и обычные формы.

```csharp
var stddev = ctx.From<IComplexEntity>()
    .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
    .First();
```

```sql
select stdev(cast(id as double precision))
 from complex_entity
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

`SqlFunctions.Sql.like(column, pattern)` и `SqlFunctions.Sql.like(column, pattern, escapeChar)` формируют предикат
`like`; строковые методы (`Contains`, `StartsWith`, `EndsWith`) преобразуются в `like` с
соответствующими подстановочными знаками.

## Разбиение на страницы

```csharp
ctx.From<IComplexEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<IComplexEntity>().Offset(10).Select(x => x.Id);    // limit -1 offset 10
```

```sql
select id
 from complex_entity limit 5 offset 10
select id
 from complex_entity limit -1 offset 10
```

В SQLite нет `OFFSET` без `LIMIT`, поэтому запрос только с offset выдаёт сигнальное значение `limit -1`.

## Функции, специфичные для SQLite

`SqlFunctions.Sqlite` содержит специфичные для SQLite функции: функции ядра, JSON1, агрегаты,
функции дат, математическое расширение и полнотекстовый поиск FTS3/FTS4/FTS5. `json_each`/`json_tree`
и поиск FTS5 доступны как табличные функции. Другие провайдеры отклоняют вызов этих функций
с `NotSupportedException`.

```csharp
ctx.From<IComplexEntity>()
    .Select(x => new
    {
        Json = SqlFunctions.Sqlite.json_extract<string>(x.String, "$.name"),
        Kind = SqlFunctions.Sqlite.@typeof(x.String),
        Total = SqlFunctions.Sqlite.total(x.NullableInt),
        Pi = SqlFunctions.Sql.pi()
    });
```

Поставляемый агрегат — `total(value)`: он возвращает `double` и никогда не возвращает null, поэтому
пустая группа или группа из одних null даёт `0.0`.

### Полнотекстовый поиск (FTS3/FTS4/FTS5)

Функции FTS выполняют поиск по виртуальной таблице. Отдельный SQLite-построитель
`IDataContext.CreateSqliteFts5CommandBuilder(tableName)` возвращает `SqliteFts5CommandBuilder` для её
обслуживания; nextorm не создаёт саму таблицу. Запросы доступны, только если сборка SQLite содержит
соответствующий модуль; поставляемая сборка включает `fts3`, `fts4` и `fts5`. Функции запросов FTS
находятся в `SqlFunctions.Sqlite`, а команды обслуживания — в отдельном построителе.
Эти функции не связаны с общими для провайдеров предикатами `SqlFunctions.Sql.contains`/`freetext`: в SQLite
свойство [`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText) равно `false`, поэтому
эти предикаты вызывают `NotSupportedException`. Функции FTS доступны только для SQL; провайдер в памяти
также вызывает `NotSupportedException`.

| C# | SQLite | Модуль |
|---|---|---|
| `Match(tableOrColumn, query)` | `tableOrColumn MATCH query` | FTS3/4/5 |
| `MatchTable<TEntity>(table, query)` | `table(query)` как источник `FROM` | FTS5 |
| `FTS5bm25(table)` / `FTS5bm25(table, weights…)` | `bm25(table[, weights…])` | FTS5 |
| `Highlight(table, columnIndex, startMatch, endMatch)` | `highlight(...)` | FTS5 |
| `Snippet(table, columnIndex, startMatch, endMatch, ellipses, tokens)` | `snippet(...)` | FTS5 |
| `Rank(table)` | `table.rank` (скрытая колонка) | FTS5 |
| `Rank(matchInfo)` | `rank(matchInfo)` (нужна UDF `rank`) | FTS3/4 |
| `RowId(table)` | `table.rowid` (скрытая колонка) | FTS3/4 |
| `FTS3Offsets(table)` | `offsets(table)` | FTS3/4 |
| `FTS3MatchInfo(table[, format])` | `matchinfo(table[, format])` | FTS3/4 |
| `FTS3Snippet(table, …)` | `snippet(table, …)` | FTS3/4 |

Первый аргумент вспомогательных функций (и имя таблицы в `Match`) — **доверенная константа**
имя таблицы; оно подставляется как quoted-идентификатор, поэтому никогда не строите его из
пользовательского ввода. `query` FTS привязывается как параметр, когда это значение времени выполнения, и подставляется
литералом, когда оно константно.

```csharp
var query = "hello";

var rows = ctx.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", query))
    .OrderBy(x => x.RowId)
    .Select(x => new
    {
        x.RowId,
        x.Title,
        Score = SqlFunctions.Sqlite.FTS5bm25("article_fts"),
        Preview = SqlFunctions.Sqlite.Snippet("article_fts", 1, "[", "]", "...", 8)
    })
    .ToList();
```

```sql
select rowid, title, bm25("article_fts") as 'Score',
       snippet("article_fts", 1, '[', ']', '...', 8) as 'Preview'
 from article_fts
where "article_fts" match $query
order by rowid
```

У FTS5 есть также табличная форма `FROM` — `MatchTable`, которая композируется как любой другой
источник (`From(() => ...)`):

```csharp
var rows = ctx
    .From(() => SqlFunctions.Sqlite.MatchTable<Article>("article_fts", query))
    .OrderBy(x => x.RowId)
    .Select(x => new { x.RowId, x.Title })
    .ToList();
```

```sql
select rowid, title
 from "article_fts"($query) order by rowid
```

У FTS3/4 нет встроенного `rank`: SQLite разрешает `rank(matchinfo(...))` через **зарегистрированную
на соединении SQL UDF** с именем `rank`, которую провайдер не регистрирует. Зарегистрируйте её на
каждом соединении, иначе запрос упадёт при выполнении с `SQLite Error 1: no such function: rank`:

```csharp
using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=app.db");
connection.CreateFunction<byte[]?, long>("rank", static matchInfo =>
{
    // Оцените blob matchinfo() (см. документацию SQLite по matchinfo); здесь просто сумма байт.
    long score = 0;
    if (matchInfo is not null)
        foreach (var b in matchInfo)
            score += b;
    return score;
});
```

```csharp
var scores = ctx.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", "hello"))
    .Select(x => new
    {
        x.RowId,
        Score = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("article_fts"))
    })
    .ToList();
```

Создайте и заполните виртуальную таблицу FTS сырым SQL: nextorm не выполняет эти шаги. Для обслуживания
FTS5 используйте SQLite-построитель команд `IDataContext.CreateSqliteFts5CommandBuilder(string tableName)`:

| Операция | SQL-форма | Примечание |
|---|---|---|
| `AutoMerge(value)` | `('automerge', value)` | `value` — 0..16; другие значения бросают |
| `CrisisMerge(value)` | `('crisismerge', value)` | `value` должен быть неотрицательным; `0`/`1` проходят без изменений |
| `Merge(pages)` | `('merge', pages)` | любое знаковое `int`, передаётся без изменений |
| `Optimize()` | `('optimize')` | одноколоночная форма |
| `Rebuild()` | `('rebuild')` | одноколоночная форма; недоступна для contentless-таблиц FTS5 |
| `IntegrityCheck(checkExternalContent = null)` | `('integrity-check')` / `('integrity-check', 0\|1)` | одноколоночная, когда флаг опущен; при `true` проверяется и внешнее содержимое |

Каждая операция возвращает новый неизменяемый builder; терминалы — `ToSql()` (генерирует SQL без
обращения к базе), `Execute()` (`int` затронутых строк) и `ExecuteAsync(CancellationToken = default)`
(`Task<int>`). Вызов терминала до выбора операции бросает `InvalidOperationException`.
Двухколоночная форма `"rank"` используется для `automerge`/`crisismerge`/`merge`, одноколоночная —
для `optimize`/`rebuild` (и для `integrity-check` с опущенным флагом). Это команды обслуживания, а не
скалярные или табличные функции и не часть `SqlFunctions.Sqlite`. Имя таблицы
отклоняется, если оно null, пустое, состоит из пробелов или содержит символ NUL.

```csharp
var builder = ctx.CreateSqliteFts5CommandBuilder("article_fts");
var sql = builder.AutoMerge(4).ToSql();
// INSERT INTO "article_fts" ("article_fts", "rank") VALUES ('automerge', 4)
var affected = builder.Optimize().Execute();
```

Этот построитель команд работает только с SQLite: любой другой провайдер вызывает
`NotSupportedException($"{dialect.GetType().Name} does not support SQLite FTS5 maintenance commands.")`
до любого доступа к базе. При успехе `Execute`/`ExecuteAsync` возвращают `int` затронутых строк
драйвера — неотрицательный для успешной команды обслуживания; nextorm не обещает фиксированного
числа или значения (это не число страниц и не счётчики восстановления). Нативные ошибки SQLite
пробрасываются. `Rebuild` недоступна для contentless-таблиц FTS5, а `IntegrityCheck(true)` проверяет
и внешнее содержимое.

Полный список, нативное написание в SQLite и требования к версии/опциям сборки — в разделе
[Специфичный для SQLite SQL](../guide/provider-specific-sql/sqlite.md).

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

Терминалы `ToStream`/`ToTextReader` позволяют читать одну колонку `byte[]`/`string` без загрузки всего
значения в память. В SQLite нет встроенного локатора строк, поэтому `Microsoft.Data.Sqlite` возвращает
потоковый `SqliteBlob`, только если запрос также выбирает `rowid`; диалект добавляет его в конец запроса
через [`LobLocatorColumn`](xref:NextORM.Core.ISqlDialect.LobLocatorColumn), оставляя значение LOB на позиции `0`.
Поэтому источник должен быть обычной rowid-таблицей. Для `view` или таблицы `WITHOUT ROWID`
**одноколоночное потоковое** чтение завершается исходным исключением
`Microsoft.Data.Sqlite.SqliteException: no such column: rowid`: для этого скалярного пути с локатором нет
буферизованного обходного варианта. Многоколоночный `ToDataReader` ниже использует отдельный
буферизованный путь без локатора. См. [Потоковое чтение больших объектов](../guide/28-large-objects.md).

Многоколоночный терминал `ToDataReader`/`ToDataReaderAsync` в SQLite **поддерживается**. Он использует
отдельную буферизованную подготовку для каждого вызова (`storeInCache: false`), а не LOB-путь с
`SequentialAccess`: `rowid` не добавляется, поэтому `FieldCount` равен числу колонок проекции, а порядковые
номера соответствуют `Select`. Такой `DbDataReader` буферизует результат целиком, а не читает его частями:
колонка `byte[]`/`string` внутри многоколоночной проекции полностью загружается в память. Поэтому для
одной большой LOB-колонки используйте `ToStream`/`ToTextReader`. Возвращённый объект чтения принадлежит
вызывающему коду: освободите его, чтобы освободить объект чтения провайдера и созданную для вызова команду.
Контекст при этом остаётся открытым и пригодным для работы.

Собственный полнотекстовый поиск SQLite (`FTS3/FTS4/FTS5`) доступен (см.
[Полнотекстовый поиск](#полнотекстовый-поиск-fts3fts4fts5)), как и отдельный SQLite-построитель команд
обслуживания FTS5 (`CreateSqliteFts5CommandBuilder`). Кросс-провайдерные предикаты
`SqlFunctions.Sql.contains`/`freetext` на SQLite недоступны: свойство
[`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText) равно `false`. Другие провайдеры
отклоняют команды обслуживания с `NotSupportedException`; `Rebuild` недоступна для contentless-таблиц
FTS5.

## Массовая вставка

В SQLite нет нативного bulk API, поэтому каждая массовая вставка идёт портируемым путём
`INSERT ... VALUES`: `Mode.Native` отклоняется по имени до перечисления источника, `Auto` всегда
разрешается в портируемый путь, а `Portable` — его явная форма. Сертифицированная конфигурация — **файловая
база** (межпроцессного сервера нет).

- Батч делится по `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`; каждый чанк — одно утверждение.
- `KeepIdentity` пишет явную identity-колонку; `IgnoreDuplicates` использует `INSERT OR IGNORE`, а
  `Returning*` — `RETURNING` (SQLite 3.35+).
- `CheckConstraints=false` выполняется временной прагмой `PRAGMA foreign_keys` вокруг записи и отклоняется внутри
  активной транзакции (там прагма не действует); `KeepNulls=false` допускается только для формы, эквивалентной
  значениям по умолчанию, а
  `TableLock`/`FireTriggers` отклоняются.
- `DatabaseName` добавляет квалификатор attached/main/temp базы там, где диалект это поддерживает; `ServerName`
  отклоняется (перенаправления нет).
- `Timeout` — один бюджет на весь вызов вокруг записей; истёкший бюджет даёт `TimeoutException`, а отмена
  вызывающего всегда побеждает.

См. [Массовая вставка](../guide/22-bulk-insert.md).

## Различия провайдеров

| Аспект | SQLite |
|---|---|
| Маркер параметра | `$name` |
| Конкатенация | `||` |
| Подстановка при `NULL` | `ifnull` |
| `greatest` / `least` | `max` / `min` (скалярные, 2+ аргумента; возвращают NULL, если хотя бы один аргумент NULL) |
| Условная функция | `iif(cond, a, b)` (SQLite 3.32+) |
| Оконные функции | `percent_rank()`, `cume_dist()`, `nth_value(expr, n)` поддерживаются |
| Логический литерал | `1` / `0` |
| Квотирование идентификаторов | одинарные кавычки (`as 't1'`) |
| Псевдоним производной таблицы | не требуется |
| Псевдоним TVF | не требуется |
| `*ALL` | не поддерживается |
| Подзапросы `ANY`/`ALL` | отклоняются базой данных при выполнении |
| Полнотекстовый поиск | Запросы FTS3/FTS4/FTS5 через `SqlFunctions.Sqlite` (`Match`, FTS5 `FTS5bm25`/`Highlight`/`Snippet`/`Rank`, помощники FTS3/4, табличный `MatchTable` FTS5; FTS3/4 `Rank` требует зарегистрированной на соединении UDF `rank`); отдельный SQLite-построитель команд обслуживания FTS5 (`CreateSqliteFts5CommandBuilder`: `AutoMerge`/`CrisisMerge`/`Merge`/`Optimize`/`Rebuild`/`IntegrityCheck`, синхронные и асинхронные терминалы возвращают `int` затронутых строк); кросс-провайдерные `contains`/`freetext` бросают |
| Потоковое чтение LOB (`ToStream`/`ToTextReader`) | поддерживается (`blob` / `text`; источник должен раскрывать `rowid` — на `view`/`WITHOUT ROWID` падает с `SqliteException: no such column: rowid`) |
| Многоколоночный reader (`ToDataReader`/`ToDataReaderAsync`) | поддерживается (буферизованный, без локатора; LOB не чанками; не-LOB проекции и LOB-колонка внутри многоколоночного select) |
| Функции сведений о сеансе | `version()` → `sqlite_version()` (нет информации о пользователе/схеме/БД) |

## См. также

- [Обзор провайдеров](overview.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [Ограничения и неподдерживаемые возможности](../advanced/limitations.md)

---

Source: `tests/nextorm.sqlite.tests/Dialect/SqliteDialectTests.cs:23,29,42,50`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:120,133,142,152,161,173,204,217,892`,
`tests/nextorm.integration.tests/Sqlite/SqliteSpecificTests.cs:16,30`,
`src/nextorm.sqlite/SqliteDialect.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs`,
`src/nextorm.sqlite/SqliteDataContext.cs`, `src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs`.
