# Специфичный для SQLite SQL

> SQLite даёт функции ядра (`printf`/`format`, `hex`/`unhex`, `random`/`randomblob`, `quote`,
> `typeof`, `glob`, `unicode`/`char`, `soundex`, `octet_length`, `if`/`ifnull`), функции/операторы/
> агрегаты JSON1 и табличные функции `json_each`/`json_tree`, функции дат (`timediff`, `unixepoch`,
> `julianday`), функции математического расширения (`acos`/`asin`/`atan`/`atan2`, гиперболические,
> `log2`/`log10`, `mod`) и поверхность полнотекстового поиска FTS3/FTS4/FTS5.

**Что нужно знать:** [Запросы и проекции](../../querying/index.md) · [Провайдер SQLite](../../providers/sqlite.md)

Все функции этой страницы доступны через
[`SqlFunctions.Sqlite`](xref:NextORM.Core.SqliteFunctions) и гейтятся
[`ISqlDialect.SqliteFunctions`](xref:NextORM.Core.ISqlDialect.SqliteFunctions). Провайдер, который не
включает возможность, бросает `NotSupportedException`, а не генерирует неисполнимый SQL.

## Функции ядра

| C# | SQLite | Примечание |
|---|---|---|
| `printf(format, ...)` / `format(format, ...)` | `printf(...)` / `format(...)` | форматирование в стиле C; `format` — 3.38+ |
| `hex(value)` | `hex(...)` | hex в верхнем регистре значения как BLOB |
| `unhex(value)` / `unhex(value, ignored)` | `unhex(...)` | декодирование hex в BLOB; 3.41+ |
| `random()` | `random()` | псевдослучайное 64-битное целое |
| `randomblob(count)` | `randomblob(...)` | случайный BLOB длиной `count` байт |
| `quote(value)` | `quote(...)` | SQL-литерал значения |
| `@typeof(value)` | `typeof(...)` | `null`/`integer`/`real`/`text`/`blob` |
| `glob(pattern, value)` | `glob(...)` | совпадение GLOB (шаблон идёт первым аргументом) |
| `unicode(value)` | `unicode(...)` | код первой кодовой точки |
| `@char(c1, ...)` | `char(...)` | строка из кодовых точек Unicode |
| `octet_length(value)` | `octet_length(...)` | длина значения в байтах |
| `soundex(value)` | `soundex(...)` | требует опции сборки `SQLITE_SOUNDEX` |
| `ifnull(value, other)` | `ifnull(...)` | двухаргументный `coalesce` |
| `@if(condition, whenTrue, whenFalse)` | `if(...)` | псевдоним `if()` — 3.48+ |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Label = SqlFunctions.Sqlite.printf("%d-%s", c.Id, c.String),
        Kind = SqlFunctions.Sqlite.@typeof(c.String),
        Ascii = SqlFunctions.Sqlite.unicode(c.String)
    })
    .ToList();
```

```sql
select printf('%d-%s', id, somestring) as 'Label',
       typeof(somestring) as 'Kind',
       unicode(somestring) as 'Ascii'
from complex_entity
```

## JSON1

Доступ к скалярам, построение и изменение:

| C# | SQLite |
|---|---|
| `json_extract<T>(json, path)` | `json_extract(json, path)` |
| `json_get(json, path)` | `json -> path` |
| `json_get_text(json, path)` | `json ->> path` |
| `json(value)` / `jsonb(value)` | `json(...)` / `jsonb(...)` (`jsonb` — 3.45+) |
| `json_array(v1, ...)` | `json_array(...)` |
| `json_array_insert(json, path, value, ...)` | `json_array_insert(...)` |
| `json_insert` / `json_replace` / `json_set` | те же имена |
| `json_object(label, value, ...)` | `json_object(...)` |
| `json_patch(target, patch)` | `json_patch(...)` |
| `json_pretty(json)` | `json_pretty(...)` (3.46+) |
| `json_quote(value)` | `json_quote(...)` |
| `json_remove(json, path, ...)` | `json_remove(...)` |
| `json_type(json)` / `json_type(json, path)` | `json_type(...)` |
| `json_valid(json)` / `json_valid(json, flags)` | `json_valid(...)` |

Функции JSON1 встроены в SQLite с 3.38 (до этого — опция `SQLITE_ENABLE_JSON1`).

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Name = SqlFunctions.Sqlite.json_extract<string>(c.String, "$.name"),
        Raw = SqlFunctions.Sqlite.json_get(c.String, "$.name"),
        Updated = SqlFunctions.Sqlite.json_set(c.String, "$.name", "nextorm")
    })
    .ToList();
```

```sql
select json_extract(somestring, '$.name') as 'Name',
       (somestring -> '$.name') as 'Raw',
       json_set(somestring, '$.name', 'nextorm') as 'Updated'
from complex_entity
```

Агрегаты собирают группу в JSON-массив/объект:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Array = SqlFunctions.Sqlite.json_group_array(c.String),
        Object = SqlFunctions.Sqlite.json_group_object(c.Int, c.String)
    })
    .ToList();
```

## Табличные функции `json_each` / `json_tree`

`json_each` обходит непосредственных детей JSON-значения, `json_tree` — рекурсивно. Используйте их
через [`FromTableFunction`](../11-table-valued-functions.md) и проецируйте форму строки вызова;
колонки — `key`, `value`, `type`, `fullkey` и `path` (плюс `id`/`parent` у `json_tree`).

```csharp
var values = dataContext
    .FromTableFunction(() => SqlFunctions.Sqlite.json_each("[\"a\",\"b\",\"c\"]"))
    .Select(r => r.Value)
    .ToList();
```

```sql
select value from json_each('["a","b","c"]')
```

## Функции дат

`timediff(a, b)` возвращает знаковую строку интервала SQLite (3.43+), `unixepoch(value)` — секунды с
1970-01-01 (3.38+), `julianday(value)` — номер юлианского дня.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Seconds = SqlFunctions.Sqlite.unixepoch(c.Datetime),
        Day = SqlFunctions.Sqlite.julianday(c.Datetime)
    })
    .ToList();
```

## Математические функции

Математические функции соответствуют расширению SQLite (`SQLITE_ENABLE_MATH_FUNCTIONS`), доступному
в сборке SQLite, поставляемой с `nextorm.sqlite`. Поверхность даёт `acos`, `acosh`, `asin`, `asinh`,
`atan`, `atan2`, `atanh`, `cosh`, `log10`, `log2`, `mod`, `sinh` и
`tanh`; переносимые отображения [`Math.*`](../../scalar-functions/index.md) (`Math.Sqrt`,
`Math.Log10`, `Math.Sign`, …) также рендерятся на SQLite. `degrees`/`radians`/`pi` переносимы
([`SqlFunctions.Sql`](../../scalar-functions/02-math-functions.md)), а не специфичны для SQLite.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Pi = SqlFunctions.Sql.pi(),
        Degrees = SqlFunctions.Sql.degrees(SqlFunctions.Sql.pi()),
        Log2 = SqlFunctions.Sqlite.log2(8.0)
    })
    .ToList();
```

```sql
select pi() as 'Pi', degrees(pi()) as 'Degrees', log2(8) as 'Log2' from complex_entity
```

## Полнотекстовый поиск (FTS3/FTS4/FTS5)

Полнотекстовый поиск SQLite живёт в модулях виртуальных таблиц `fts3`, `fts4` и `fts5`. nextorm
предоставляет только сторону **запросов** через [`SqlFunctions.Sqlite`](xref:NextORM.Core.SqliteFunctions);
он не создаёт и не обслуживает виртуальную таблицу, поэтому создайте её сырым SQL (или собственной
миграцией):

```sql
create virtual table article_fts using fts5(title, body);
```

Поддержка существует только когда сборка SQLite включает модуль — поставляемая сборка содержит все
три. Эти члены независимы от кросс-провайдерных предикатов `SqlFunctions.Sql.contains`/`freetext`,
которые по-прежнему гейтятся [`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText)
(на SQLite `false`) и потому бросают `NotSupportedException`. Каждый член FTS — SQL-only: in-memory
провайдер бросает `NotSupportedException`.

### FTS5

| C# | SQLite |
|---|---|
| `Match(tableOrColumn, query)` | `tableOrColumn MATCH query` |
| `MatchTable<TEntity>(table, query)` | `table(query)` как источник `FROM` |
| `FTS5bm25(table)` / `FTS5bm25(table, weights…)` | `bm25(table[, weights…])` |
| `Highlight(table, columnIndex, startMatch, endMatch)` | `highlight(...)` |
| `Snippet(table, columnIndex, startMatch, endMatch, ellipses, tokens)` | `snippet(...)` |
| `Rank(table)` | `table.rank` (скрытая колонка) |

`Match` принимает первым аргументом либо доверенный константный токен таблицы/алиаса, либо
выражение mapped-колонки; `query` — это match-выражение FTS5 (`hel*`, `"hello world"`,
`hello OR goodbye`, …).

```csharp
var query = "hello";

var rows = dataContext.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", query))
    .OrderBy(x => x.RowId)
    .Select(x => new
    {
        x.RowId,
        x.Title,
        Score = SqlFunctions.Sqlite.FTS5bm25("article_fts"),
        Preview = SqlFunctions.Sqlite.Snippet("article_fts", 1, "[", "]", "...", 8),
        Marked = SqlFunctions.Sqlite.Highlight("article_fts", 0, "<b>", "</b>")
    })
    .ToList();
```

```sql
select rowid, title, bm25("article_fts") as 'Score',
       snippet("article_fts", 1, '[', ']', '...', 8) as 'Preview',
       highlight("article_fts", 0, '<b>', '</b>') as 'Marked'
from article_fts
where "article_fts" match $query
order by rowid
```

`Rank(table)` читает скрытую колонку `rank` FTS5 (равна стандартному score `bm25`; меньше — лучшее
совпадение). Передайте веса в `FTS5bm25`, чтобы оценить конкретную колонку:

```csharp
var weighted = dataContext.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", query))
    .Select(x => new { x.RowId, Score = SqlFunctions.Sqlite.FTS5bm25("article_fts", 1.0, 2.0) })
    .ToList();
```

Табличная форма фильтрует в `FROM`, поэтому композируется с остальным построителем:

```csharp
var rows = dataContext
    .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<Article>("article_fts", query))
    .Where(x => x.RowId > 0)
    .OrderBy(x => x.RowId)
    .Select(x => new { x.RowId, x.Title })
    .ToList();
```

```sql
select rowid, title from "article_fts"($query) where rowid > 0 order by rowid
```

### FTS3 / FTS4

| C# | SQLite |
|---|---|
| `Match(tableOrColumn, query)` | `tableOrColumn MATCH query` |
| `RowId(table)` | `table.rowid` (скрытая колонка) |
| `FTS3Offsets(table)` | `offsets(table)` |
| `FTS3MatchInfo(table)` / `FTS3MatchInfo(table, format)` | `matchinfo(table[, format])` |
| `FTS3Snippet(table)` / `FTS3Snippet(table, …)` | `snippet(table[, …])` |
| `Rank(matchInfo)` | `rank(matchInfo)` |

У FTS3/4 **нет встроенного `rank`**. `Rank` рендерит `rank(matchinfo(...))` и ожидает SQL UDF с
именем `rank`, зарегистрированную на соединении (провайдер её не регистрирует); без неё запрос
падает с `SQLite Error 1: no such function: rank`. У `FTS3Snippet` есть перегрузки для маркеров
начала/конца, многоточия, индекса колонки (`-1` — все колонки) и числа токенов.

```csharp
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

// Зарегистрируйте ранжирующую UDF один раз на соединении, которое будет использовать контекст.
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

using var dataContext = new DataContextBuilder().UseSqlite(connection).CreateDataContext();

var rows = dataContext.From<Article>()
    .Where(x => SqlFunctions.Sqlite.Match("article_fts", "hello"))
    .Select(x => new
    {
        x.RowId,
        HiddenRowId = SqlFunctions.Sqlite.RowId("article_fts"),
        Offsets = SqlFunctions.Sqlite.FTS3Offsets("article_fts"),
        MatchInfo = SqlFunctions.Sqlite.FTS3MatchInfo("article_fts", "pcx"),
        Snippet = SqlFunctions.Sqlite.FTS3Snippet("article_fts", "[", "]", "...", 0, 8),
        Score = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("article_fts"))
    })
    .ToList();
```

Первый аргумент вспомогательных функций (и токен таблицы у `Match`) — доверенная константа,
подставляемая как quoted-идентификатор; никогда не строите её из пользовательского ввода.

Создание, наполнение и настройка индекса остаются вне поверхности запросов: функции
обслуживания/управления FTS5 (`AutoMerge`, `CrisisMerge`, `Merge`, `Optimize`, `Rebuild`,
`IntegrityCheck`) отложены в follow-up
[#195](https://github.com/AlexeyShirshov/nextorm/issues/195) и
[#196](https://github.com/AlexeyShirshov/nextorm/issues/196). Пока выполняйте эти операторы сырым
SQL.

Сводка на уровне провайдера — в разделе [Провайдер SQLite](../../providers/sqlite.md).
