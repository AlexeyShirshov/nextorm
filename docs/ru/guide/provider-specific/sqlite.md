# Специфичный для SQLite SQL

> SQLite даёт функции ядра (`printf`/`format`, `hex`/`unhex`, `random`/`randomblob`, `quote`,
> `typeof`, `glob`, `unicode`/`char`, `soundex`, `octet_length`, `if`/`ifnull`), функции/операторы/
> агрегаты JSON1 и табличные функции `json_each`/`json_tree`, функции дат (`timediff`, `unixepoch`,
> `julianday`) и функции математического расширения (`acos` … `tanh`, `degrees`, `log2`/`log10`,
> `mod`, `pi`, `radians`).

**Что нужно знать:** [Запросы и проекции](../01-querying-and-projections.md) · [Провайдер SQLite](../../providers/sqlite.md)

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
через [`FromTableFunction`](../13-table-valued-functions.md) и проецируйте форму строки вызова;
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
`atan`, `atan2`, `atanh`, `cosh`, `degrees`, `log10`, `log2`, `mod`, `pi`, `radians`, `sinh` и
`tanh`; переносимые отображения [`Math.*`](../../scalar-functions/index.md) (`Math.Sqrt`,
`Math.Log10`, `Math.Sign`, …) также рендерятся на SQLite.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(c => new
    {
        Pi = SqlFunctions.Sqlite.pi(),
        Degrees = SqlFunctions.Sqlite.degrees(SqlFunctions.Sqlite.pi()),
        Log2 = SqlFunctions.Sqlite.log2(8.0)
    })
    .ToList();
```

```sql
select pi() as 'Pi', degrees(pi()) as 'Degrees', log2(8) as 'Log2' from complex_entity
```
