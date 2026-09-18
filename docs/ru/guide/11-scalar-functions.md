# Скалярные функции

> Преобразуйте члены `string`, `Math` и `DateTime`, объединение `??`, логические предикаты и числовые
> преобразования в SQL, специфичный для провайдера.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md)

## Обзор

nextorm распознаёт фиксированный набор членов CLR и переписывает их в SQL внутри любого выражения запроса.
Диспетчеризация находится в `BaseExpressionVisitor`: методы `string`, методы `Math`, члены `DateTime`,
`NORM.SQL.like`, оператор `??` и числовые преобразования. Всё, что зависит от провайдера, делегируется
`ISqlDialect`, поэтому один и тот же код C# генерирует правильную функцию на каждом провайдере.

Повсюду действуют два правила:

* **захваченное** значение (локальная переменная или параметр) становится **параметром** запроса, а не
  литералом;
* **константа** встраивается. Для `Contains`/`StartsWith`/`EndsWith` это также означает, что `%`, `_` и `\`
  в константе экранируются и генерируется предложение `escape '\'`.

Встроенные преобразования применяются **до** любого сопоставления
[`[SqlFunction]`](12-user-defined-functions.md), поэтому пользовательский атрибут не может изменить
поведение членов `string`/`Math`/`DateTime`.

Кросс-провайдерные помощники находятся в `NORM.SQL`. Функции, которые поддерживает только один
провайдер, сгруппированы в отдельную провайдерную поверхность: `NORM.PG_SQL` (PostgreSQL: нативные
массивы, нативный JSON, расширенная библиотека скалярных функций, PG-only агрегаты и табличные
функции `generate_series`/`unnest`), `NORM.MS_SQL` (SQL Server: JSON-как-текст и
`string_split`/`openjson`) и `NORM.CLK_SQL` (ClickHouse: `arg_min`/`arg_max` и комбинатор `-If`).
Вызов любой из них на провайдере, который не opt-in, бросает `NotSupportedException`.

## Строковые функции

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => e.String!.Contains("df"))
    .Select(e => new
    {
        Upper = e.String!.ToUpper(),
        Lower = e.String!.ToLower(),
        Part = e.String!.Substring(1, 2),
        Length = e.String!.Length,
        Trimmed = e.String!.Trim(),
        Replaced = e.String!.Replace("a", "b")
    })
    .ToList();
```

| C# | SQL | Примечания |
|---|---|---|
| `s.ToUpper()` | `upper(s)` | |
| `s.ToLower()` | `lower(s)` | |
| `s.Trim()` | `trim(s)` | |
| `s.TrimStart()` | `ltrim(s)` | |
| `s.TrimEnd()` | `rtrim(s)` | |
| `s.Substring(start, length)` | `substring(s, start + 1, length)` | Индекс в C# начинается с 0; в SQL — с 1. |
| `s.Substring(start)` | `substring(s, start + 1, length(s) - (start))` | Оставшаяся длина вычисляется. `Substring(Range)` не поддерживается. |
| `s.Length` | `length(s)` / `len(s)` | `len` в SQL Server. |
| `s.Replace(a, b)` | `replace(s, a, b)` | |
| `s.Remove(start, count)` | вставка, удаляющая `count` символов | `stuff` в SQL Server, `insert` в MySQL/MariaDB, `overlay` в PostgreSQL, склейка `substring` в остальных. |
| `s.Remove(start)` | вставка, удаляющая всё начиная с `start` | |
| `s.Insert(start, text)` | вставка `text` в позицию `start` | |
| `s.IndexOf(x)` | позиция с нуля, или `-1` | `charindex`/`instr`/`strpos`/`position`. В SQL нумерация с 1 и `0` при отсутствии; обе корректируются. |
| `s.IndexOf(x, start)` | позиция с нуля, начиная с `start` | |
| `s.LastIndexOf(x)` | последняя позиция с нуля, или `-1` | Требует посимвольного разворота; в SQLite не поддерживается. |
| `s.PadLeft(width[, c])` | дополнение слева до `width`, без обрезки | `replicate`/`repeat`; семейство SQL `lpad` обрезает, поэтому добавляется проверка длины. |
| `s.PadRight(width[, c])` | дополнение справа до `width`, без обрезки | |
| `new string(c, n)` | `replicate(c, n)` / `repeat(c, n)` | `c` должна быть константой. |
| `s.Split(x)` | `string_to_array(s, x)` | Только PostgreSQL; используется как array-операнд. |
| `string.Join(sep, s.Split(x))` | `array_to_string(string_to_array(s, x), sep)` | Только PostgreSQL; требует нативных массивов. |
| `s.Contains(x)` | `s like '%x%'` | Константа `x` экранируется. |
| `s.StartsWith(x)` | `s like 'x%'` | |
| `s.EndsWith(x)` | `s like '%x'` | |
| `string.IsNullOrEmpty(s)` | `(s is null or s = '')` | |
| `NORM.SQL.like(s, pattern)` | `s like pattern` | Явный `LIKE`. |
| `NORM.SQL.like(s, pattern, escape)` | `s like pattern escape escape` | |

`NORM.SQL.like` — это запасной вариант, когда шаблон не является простым
`Contains`/`StartsWith`/`EndsWith`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.SQL.like(e.String, "%a%"))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where somestring like '%a%'
```

Значение времени выполнения в `Contains` не может быть экранировано во время преобразования, поэтому
подстановочные знаки конкатенируются вокруг параметра, и проход извлечения параметров всё равно его
собирает:

```csharp
var needle = "df";
var prepared = dataContext.From<IComplexEntity>()
    .Where(e => e.String!.Contains(needle))
    .Select(e => new { e.Id })
    .Prepare();
```

```sql
-- SQLite: % and the concatenation operator; SQL Server uses '+' and @needle
select id from complex_entity where somestring like '%'||$needle||'%'
```

> В SQL Server нет логического скалярного типа, поэтому **проецируемый** предикат (например,
> `Select(e => e.String!.Contains("df"))`) материализуется с помощью `CASE`:
> `cast(case when somestring like '%df%' then 1 else 0 end as bit)`.

## Расширения строк и регулярных выражений (PostgreSQL)

Помимо переносимых методов `string` выше, `NORM.PG_SQL` предоставляет распространённые строковые функции
PostgreSQL и функции POSIX-регулярных выражений. Они входят в расширенную библиотеку скалярных функций
(`ISqlDialect.SupportsExtendedScalarFunctions`, только PostgreSQL):

> Предпочитайте переносимые встроенные формы там, где они есть: они рендерятся всеми провайдерами, тогда
> как формы `NORM.PG_SQL` ниже доступны только в PostgreSQL. Используйте `s.Substring(0, n)` /
> `s.Substring(s.Length - n)` вместо `left`/`right` и `s.PadLeft(n, c)` / `s.PadRight(n, c)` вместо
> `lpad`/`rpad`.

| C# | SQL |
|---|---|
| `NORM.PG_SQL.split_part(s, delim, n)` | `split_part(s, delim, n)` |
| `NORM.PG_SQL.strpos(s, sub)` | `strpos(s, sub)` |
| `NORM.PG_SQL.left(s, n)` / `NORM.PG_SQL.right(s, n)` | `left(s, n)` / `right(s, n)` |
| `NORM.PG_SQL.lpad(s, n, fill)` / `NORM.PG_SQL.rpad(s, n, fill)` | `lpad(s, n, fill)` / `rpad(s, n, fill)` |
| `NORM.PG_SQL.repeat(s, n)` | `repeat(s, n)` |
| `NORM.PG_SQL.reverse(s)` | `reverse(s)` |
| `NORM.PG_SQL.initcap(s)` | `initcap(s)` |
| `NORM.PG_SQL.translate(s, from, to)` | `translate(s, from, to)` |
| `NORM.PG_SQL.overlay(s, placing, from, count)` | `overlay(s, placing, from, count)` |
| `NORM.PG_SQL.concat_ws(sep, ...)` | `concat_ws(sep, ...)` |
| `NORM.PG_SQL.format(fmt, ...)` | `format(fmt, ...)` |
| `NORM.PG_SQL.md5(s)` | `md5(s)` |
| `NORM.PG_SQL.regexp_replace(s, pattern, replacement[, flags])` | `regexp_replace(...)` |
| `NORM.PG_SQL.regexp_like(s, pattern[, flags])` | `regexp_like(...)` |
| `NORM.PG_SQL.regexp_split_to_array(s, pattern)` | `regexp_split_to_array(s, pattern)` |
| `NORM.PG_SQL.regexp_count(s, pattern)` | `regexp_count(s, pattern)` |
| `NORM.PG_SQL.regexp_instr(s, pattern)` | `regexp_instr(s, pattern)` |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Part = NORM.PG_SQL.split_part(e.String, ",", 1),
        LooksLikeA = NORM.PG_SQL.regexp_like(e.String, "^a")
    })
    .ToList();
```

## Математические функции

| C# | SQL | Примечания |
|---|---|---|
| `Math.Abs(x)` | `abs(x)` | |
| `Math.Round(x)` | `round(x)` / `round(x, 0)` | SQL Server требует аргумент длины. |
| `Math.Round(x, digits)` | `round(x, digits)` | |
| `Math.Truncate(x)` | `trunc(x)` / `round(x, 0, 1)` | В SQL Server нет `trunc`. |
| `Math.Log(x)` | натуральный логарифм: `ln(x)` (SQLite, PostgreSQL) / `log(x)` (SQL Server) | Только одноаргументная форма. |

```csharp
var values = dataContext.From<IComplexEntity>()
    .Select(e => Math.Abs(e.Id - 5))
    .ToList();
// ids 1, 2, 3 -> 4, 3, 2
```

```sql
select abs((id - 5)) from complex_entity
```

### Расширенная математика PostgreSQL

Остальные математические функции входят в расширенную библиотеку скалярных функций
(`ISqlDialect.SupportsExtendedScalarFunctions`, только PostgreSQL):

| C# | SQL |
|---|---|
| `NORM.PG_SQL.asin(x)` / `acos(x)` / `atan(x)` | `asin(x)` / `acos(x)` / `atan(x)` |
| `NORM.PG_SQL.atan2(y, x)` | `atan2(y, x)` |
| `NORM.PG_SQL.cbrt(x)` | `cbrt(x)` |
| `NORM.PG_SQL.sinh(x)` / `cosh(x)` / `tanh(x)` | `sinh(x)` / `cosh(x)` / `tanh(x)` |
| `NORM.PG_SQL.asinh(x)` / `acosh(x)` / `atanh(x)` | `asinh(x)` / `acosh(x)` / `atanh(x)` |
| `NORM.PG_SQL.degrees(x)` / `NORM.PG_SQL.radians(x)` | `degrees(x)` / `radians(x)` |
| `NORM.PG_SQL.pi()` / `NORM.PG_SQL.random()` | `pi()` / `random()` |
| `NORM.PG_SQL.log(base, x)` | `log(base, x)` |
| `NORM.PG_SQL.mod(a, b)` / `gcd(a, b)` / `lcm(a, b)` | `mod(a, b)` / `gcd(a, b)` / `lcm(a, b)` |
| `NORM.PG_SQL.factorial(n)` | `factorial(n)` |
| `NORM.PG_SQL.width_bucket(x, low, high, count)` | `width_bucket(x, low, high, count)` |

## Дата и время

`DateTime.Now` и `DateTime.UtcNow` рендерятся как SQL-выражения, а не вычисляются как параметр. `.Year`,
`.Month`, `.Day` и `.Hour` (а также `.Minute` и `.Second`) становятся извлечением части даты, специфичным
для провайдера:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == 1)
    .Select(e => new { e.Datetime!.Value.Year, e.Datetime!.Value.Month, e.Datetime!.Value.Day })
    .First();
```

```sql
-- SQLite
select cast(strftime('%Y', dt) as integer) as 'Year', cast(strftime('%m', dt) as integer) as 'Month', cast(strftime('%d', dt) as integer) as 'Day' from complex_entity where (id = 1)
```

```sql
-- SQL Server
... datepart(year, dt) ... datepart(month, dt) ... datepart(day, dt) ...

-- PostgreSQL
... extract(year from dt) ... extract(month from dt) ... extract(day from dt) ...
```

Важная деталь для SQLite: `strftime` возвращает текст, поэтому результат оборачивается в
`cast(... as integer)`, чтобы материализоваться как свойство CLR `int`.

### Расширенные дата и время PostgreSQL

Эти функции входят в расширенную библиотеку скалярных функций
(`ISqlDialect.SupportsExtendedScalarFunctions`, только PostgreSQL):

| C# | SQL |
|---|---|
| `NORM.PG_SQL.make_interval(y, mo, d, h, mi, s)` | `make_interval(y, mo, d, h, mi, s)` |
| `NORM.PG_SQL.justify_days(interval)` / `justify_hours(interval)` | `justify_days(interval)` / `justify_hours(interval)` |
| `NORM.PG_SQL.to_char(value, format)` | `to_char(value, format)` |
| `NORM.PG_SQL.to_date(text, format)` | `to_date(text, format)` |
| `NORM.PG_SQL.to_number(text, format)` | `to_number(text, format)` |
| `NORM.PG_SQL.to_timestamp(epoch)` / `to_timestamp(text, format)` | `to_timestamp(...)` |
| `NORM.PG_SQL.timezone(zone, value)` | `timezone(zone, value)` |
| `NORM.PG_SQL.current_date()` / `current_time()` / `localtime()` / `localtimestamp()` | те же ключевые слова |

Для построения дат и арифметики используется переносимый набор: `date_from_parts`, `date_add`,
`date_diff`, `date_trunc` и члены `DateTime` (см. [Арифметику дат](#арифметика-дат) ниже).
`make_date`, `age`, `date_bin` и `extract` больше не предоставляются отдельно.

## COALESCE (`??`) и CAST

`a ?? b` отображается на двухаргументную замену null у провайдера. Числовое преобразование числового
операнда — приведение C#, такое как `(double)e.Id`, или вызов `Convert.ToXxx(value)` — отображается на
`cast(x as <type>)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new { V = e.String ?? "" })
    .ToList();

var halves = dataContext.From<IComplexEntity>()
    .Select(e => (double)e.Id / 2.0)
    .ToList();
```

```sql
-- SQLite
select ifnull(somestring, '') from complex_entity
select (cast(id as double precision) / 2) from complex_entity

-- SQL Server
select isnull(somestring, '') from complex_entity
select (cast(id as float) / 2) from complex_entity

-- PostgreSQL
select coalesce(somestring, '') from complex_entity
select (cast(id as double precision) / 2) from complex_entity
```

Целевые типы числового приведения берутся из `ISqlDialect.MakeTypeName`:

| Тип CLR | SQLite / PostgreSQL | SQL Server |
|---|---|---|
| `byte` | `smallint` | `tinyint` |
| `short` | `smallint` | `smallint` |
| `int` | `integer` | `int` |
| `long` | `bigint` | `bigint` |
| `float` | `real` | `real` |
| `double` | `double precision` | `float` |
| `decimal` | `numeric` | `decimal(38, 10)` |

## Массивы (PostgreSQL)

В PostgreSQL есть встроенные типы-массивы. Массив всегда передаётся **одним параметром** (целиком), а
не разворачивается в список значений, поэтому текст SQL не зависит от количества элементов, и план
запроса остаётся кэшируемым. Массивом может быть runtime-параметр (`NORM.Param<T[]>(idx)`),
захваченная локальная переменная/поле или встроенный `new[]`. Поверхность массивов умеет рендерить
только диалект, включивший `ISqlDialect.SupportsArrays` (PostgreSQL); все остальные провайдеры бросают
`NotSupportedException`.

`NORM.PG_SQL.any` / `NORM.PG_SQL.all` принимают массив — либо как готовый предикат (`column = any(@array)`),
либо как правую часть сравнения:

```csharp
var ids = new long[] { 1, 2, 3 };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.PG_SQL.any(e.Id, ids))     // (id = any(@p0))
    .Select(e => new { e.Id })
    .ToList();

var same = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == NORM.PG_SQL.any(ids))   // id = any(@p0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (id = any(@p0))
```

Runtime-параметр-массив использует тот же механизм `NORM.Param`, поэтому массив не нужно знать в момент
подготовки запроса:

```csharp
var prepared = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == NORM.PG_SQL.any(NORM.Param<long[]>(0)))
    .Select(e => new { e.Id })
    .Prepare();

var rows = prepared.ToList(new long[] { 1, 2, 3 });
```

```sql
select id from complex_entity where id = any(@norm_p0)
```

Функции и операторы для массивов отображаются в свои имена PostgreSQL:

| C# | SQL |
|---|---|
| `NORM.PG_SQL.cardinality(a)` | `cardinality(a)` |
| `NORM.PG_SQL.array_length(a, dim)` | `array_length(a, dim)` |
| `NORM.PG_SQL.array_ndims(a)` | `array_ndims(a)` |
| `NORM.PG_SQL.array_lower(a, dim)` | `array_lower(a, dim)` |
| `NORM.PG_SQL.array_upper(a, dim)` | `array_upper(a, dim)` |
| `NORM.PG_SQL.array_position(a, element)` | `array_position(a, element)` |
| `NORM.PG_SQL.array_contains(a, b)` | `a @> b` |
| `NORM.PG_SQL.array_overlaps(a, b)` | `a && b` |
| `NORM.PG_SQL.array_contained_by(a, b)` | `a <@ b` |
| `NORM.PG_SQL.array_concat(a, b)` | `a \|\| b` |
| `NORM.PG_SQL.array_cat(a, b)` | `array_cat(a, b)` |
| `NORM.PG_SQL.array_append(a, element)` | `array_append(a, element)` |
| `NORM.PG_SQL.array_prepend(element, a)` | `array_prepend(element, a)` |
| `NORM.PG_SQL.array_remove(a, element)` | `array_remove(a, element)` |
| `NORM.PG_SQL.array_replace(a, from, to)` | `array_replace(a, from, to)` |
| `NORM.PG_SQL.array_fill(value, dims)` | `array_fill(value, dims)` |
| `NORM.PG_SQL.array_dims(a)` | `array_dims(a)` |
| `NORM.PG_SQL.array_positions(a, element)` | `array_positions(a, element)` |
| `NORM.PG_SQL.array_reverse(a)` | `array_reverse(a)` |
| `NORM.PG_SQL.array_sort(a)` | `array_sort(a)` |
| `NORM.PG_SQL.array_to_string(a, delimiter)` | `array_to_string(a, delimiter)` |
| `NORM.PG_SQL.string_to_array(s, delimiter)` | `string_to_array(s, delimiter)` |

> Функции, возвращающие массив (`array_append`, `array_cat`, `array_reverse`, `string_to_array`, ...),
> предназначены для использования внутри запроса (предикат, `having` или вложенное выражение);
> построитель строк пока не умеет материализовать колонку-массив, поэтому прямое проецирование такой
> функции падает на этапе подготовки.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.PG_SQL.array_length(NORM.Param<long[]>(0), 1) == 3)
    .Select(e => new { N = NORM.PG_SQL.cardinality(NORM.Param<long[]>(1)) })
    .ToList();
```

```sql
select cardinality(@norm_p1) as "N" from complex_entity where array_length(@norm_p0, 1) = 3
```

## JSON и JSONB (PostgreSQL)

PostgreSQL — единственный поддерживаемый провайдер с типами `json`/`jsonb`. Операнд JSON должен быть
выражением `json`/`jsonb`: колонка, другая JSON-функция или параметр, runtime-значение которого —
`JsonDocument`, `JsonElement` или `JsonNode` (Npgsql привязывает их как `jsonb`). Обычная строка с JSON
привязывается как `text`; её можно разобрать явно через `NORM.PG_SQL.json_cast(value)`
(`cast(value as jsonb)`).

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.PG_SQL.json_get_text(NORM.Param<JsonDocument>(0), "name") == "Alice")
    .Select(e => new { e.Id })
    .ToList(document);
```

```sql
select id from complex_entity where ((@norm_p0 ->> 'name') = 'Alice')
```

Агрегаты сворачивают набор строк в один JSON-документ:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => NORM.PG_SQL.jsonb_agg(e.String))
    .First();

var person = dataContext.From<IComplexEntity>()
    .Select(e => NORM.PG_SQL.jsonb_build_object("id", e.Id, "name", e.String))
    .First();
```

```sql
select jsonb_agg(somestring) from complex_entity
select jsonb_build_object('id', id, 'name', somestring) from complex_entity
```

| C# | SQL |
|---|---|
| `NORM.PG_SQL.json_agg(x)` / `jsonb_agg(x)` | `json_agg(x)` / `jsonb_agg(x)` |
| `NORM.PG_SQL.json_object_agg(k, v)` / `jsonb_object_agg(k, v)` | `json_object_agg(k, v)` / `jsonb_object_agg(k, v)` |
| `NORM.PG_SQL.json_build_object("a", x, ...)` | `json_build_object('a', x, ...)` |
| `NORM.PG_SQL.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `NORM.PG_SQL.json_build_array(x, y)` / `jsonb_build_array(x, y)` | `json_build_array(x, y)` / `jsonb_build_array(x, y)` |
| `NORM.PG_SQL.to_json(x)` / `to_jsonb(x)` | `to_json(x)` / `to_jsonb(x)` |
| `NORM.PG_SQL.json_cast(x)` | `cast(x as jsonb)` |
| `NORM.PG_SQL.json_get(json, "key")` / `json_get(json, 0)` | `json -> key` / `json -> 0` |
| `NORM.PG_SQL.json_get_text(json, "key")` / `json_get_text(json, 0)` | `json ->> key` / `json ->> 0` |
| `NORM.PG_SQL.json_get_path(json, path)` / `json_get_path_text(json, path)` | `json #> path` / `json #>> path` |
| `NORM.PG_SQL.json_contains(a, b)` | `a @> b` |
| `NORM.PG_SQL.json_exists(json, "key")` | `json ? 'key'` |
| `NORM.PG_SQL.json_exists_any(json, keys)` / `json_exists_all(json, keys)` | `json ?\| keys` / `json ?& keys` |
| `NORM.PG_SQL.json_array_length(json)` / `jsonb_array_length(json)` | `json_array_length(json)` / `jsonb_array_length(json)` |
| `NORM.PG_SQL.json_typeof(json)` / `jsonb_typeof(json)` | `json_typeof(json)` / `jsonb_typeof(json)` |
| `NORM.PG_SQL.jsonb_set(json, path, value[, create])` | `jsonb_set(...)` |
| `NORM.PG_SQL.jsonb_insert(json, path, value[, after])` | `jsonb_insert(...)` |
| `NORM.PG_SQL.jsonb_strip_nulls(json)` | `jsonb_strip_nulls(json)` |
| `NORM.PG_SQL.jsonb_pretty(json)` | `jsonb_pretty(json)` |
| `NORM.PG_SQL.jsonb_delete(json, "key")` / `jsonb_delete(json, 0)` | `json - 'key'` / `json - 0` |
| `NORM.PG_SQL.json_concat(a, b)` | `a \|\| b` |
| `NORM.PG_SQL.row_to_json(row)` | `row_to_json(row)` |
| `NORM.PG_SQL.array_to_json(array)` | `array_to_json(array)` |
| `NORM.PG_SQL.jsonb_path_exists(json, path)` | `jsonb_path_exists(json, cast(path as jsonpath))` |
| `NORM.PG_SQL.jsonb_path_match(json, path)` | `jsonb_path_match(json, cast(path as jsonpath))` |
| `NORM.PG_SQL.jsonb_path_query_first(json, path)` | `jsonb_path_query_first(json, cast(path as jsonpath))` |
| `NORM.PG_SQL.jsonb_path_query_array(json, path)` | `jsonb_path_query_array(json, cast(path as jsonpath))` |

Операнд `path`/`keys` — это `string[]`, привязываемый как **один параметр-массив** (см.
[Массивы](#массивы-postgresql)), поэтому `NORM.PG_SQL.json_get_path(json, new[] { "a", "b" })` отрисует
`json #> @p0`. Функции JSONPath принимают путь как обычную строку и отрисовывают его как
`cast(<path> as jsonpath)`.

## JSON как текст (SQL Server)

SQL Server хранит JSON в обычной колонке `nvarchar` и предоставляет текстовое подмножество функций
(`ISqlDialect.SupportsTextJson`). Путь — это строка JSONPath (`'$.name'`); `json_value` возвращает
скаляр, а `json_query` — фрагмент-объект/массив:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Id = NORM.MS_SQL.json_value(e.String, "$.id"),
        Name = NORM.MS_SQL.json_query(e.String, "$.name"),
        Updated = NORM.MS_SQL.json_modify(e.String, "$.id", "1")
    })
    .ToList();
```

```sql
select json_value(somestring, '$.id') as [Id], json_query(somestring, '$.name') as [Name], json_modify(somestring, '$.id', '1') as [Updated] from complex_entity
```

| C# | SQL |
|---|---|
| `NORM.MS_SQL.json_value(json, path)` | `json_value(json, path)` |
| `NORM.MS_SQL.json_query(json, path)` | `json_query(json, path)` |
| `NORM.MS_SQL.json_modify(json, path, value)` | `json_modify(json, path, value)` |
| `NORM.MS_SQL.isjson(value)` | `isjson(value)` |

`NORM.MS_SQL.isjson` возвращает логическое значение: в предикате он отрисовывается как `(isjson(x)) = 1`
(T-SQL `ISJSON` возвращает `int`), а при проецировании как значение приводится к `bit`. `isjson`
можно также использовать прямо в `WHERE` (`Where(e => NORM.MS_SQL.isjson(e.String))`).

## Условные функции

`NORM.SQL.nullif` — ANSI и работает на всех SQL-провайдерах; `greatest`/`least` включаются флагом
`ISqlDialect.SupportsGreatestLeast` (его включают PostgreSQL, MySQL/MariaDB, ClickHouse и SQL Server 2022+):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        NoZero = NORM.SQL.nullif(e.Int, 0),
        Hi = NORM.SQL.greatest(e.Id, 10L),
        Lo = NORM.SQL.least(e.Id, 10L)
    })
    .ToList();
```

```sql
select nullif(nullableint, 0) as "NoZero", greatest(id, 10) as "Hi", least(id, 10) as "Lo" from complex_entity
```

| C# | SQL |
|---|---|
| `NORM.SQL.nullif(a, b)` | `nullif(a, b)` |
| `NORM.SQL.greatest(a, b, ...)` | `greatest(a, b, ...)` |
| `NORM.SQL.least(a, b, ...)` | `least(a, b, ...)` |
| `NORM.PG_SQL.num_nulls(a, b, ...)` | `num_nulls(a, b, ...)` |
| `NORM.PG_SQL.num_nonnulls(a, b, ...)` | `num_nonnulls(a, b, ...)` |

`num_nulls`/`num_nonnulls` входят в расширенную библиотеку скалярных функций
(`ISqlDialect.SupportsExtendedScalarFunctions`).

## Усечение даты (PostgreSQL, SQL Server, ClickHouse)

`NORM.SQL.date_trunc(field, value)` усекает отметку времени до части даты
(`ISqlDialect.SupportsDateTrunc`; его включают PostgreSQL, SQL Server 2022+ и ClickHouse). Поле должно
быть константной строкой из поддерживаемого набора. SQL Server отрисовывает `datetrunc(part, value)`,
сворачивая множественные ANSI-части в единственные T-SQL-написания (`milliseconds` → `millisecond`)
и отклоняя `decade`/`century`/`millennium`; ClickHouse отрисовывает `dateTrunc('part', value)` с тем
же сворачиванием и тем же отклонением трёх крупных частей:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new { Month = NORM.SQL.date_trunc("month", e.Datetime) })
    .ToList();
```

```sql
-- PostgreSQL / SQL Server 2022+
select date_trunc('month', dt) as "Month" from complex_entity
-- ClickHouse
select dateTrunc('month', dt) as `Month` from complex_entity
```

## Арифметика дат

`NORM.SQL.date_add(field, amount, value)` прибавляет к дате/времени заданное число единиц, а
`NORM.SQL.end_of_month(value)` возвращает последний день месяца (`ISqlDialect.SupportsDateArithmetic`;
его включают PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB и SQLite). Поле должно быть константной
строкой; диалект проверяет, какие части он принимает (`ISqlDialect.SupportsDateAddField` и др.).
SQL Server отрисовывает `dateadd(field, amount, value)` и `eomonth(value)`, сворачивая
`decade`/`century`/`millennium` в масштабированное прибавление `year`; PostgreSQL отрисовывает
интервальную арифметику; ClickHouse отрисовывает выделенные функции
`addDays`/`addMonths`/…/`addSeconds` (сворачивая три крупные части в масштабированный `addYears`) и
`toLastDayOfMonth(value)`; MySQL/MariaDB отрисовывают `date_add(value, interval n unit)` и
`last_day(value)`; SQLite настраивает дату через строку-модификатор `datetime`/`strftime`.
`NORM.SQL.date_diff(field, start, end)` возвращает число границ `<field>` между двумя отметками
времени (SQL Server `datediff`, ClickHouse `dateDiff`, MySQL/MariaDB `timestampdiff`); резервные
реализации PostgreSQL и SQLite считают части даты границами, а части времени — целыми единицами.
`NORM.SQL.date_from_parts(year, month, day)` строит дату. `DateTime.AddDays`/`AddMonths`/… внутри
проекции или предиката идут через тот же хук:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        NextDay = NORM.SQL.date_add("day", 1, e.Datetime),
        MonthEnd = NORM.SQL.end_of_month(e.Datetime)
    })
    .ToList();
```

```sql
-- SQL Server
select dateadd(day, 1, dt) as [NextDay], eomonth(dt) as [MonthEnd] from complex_entity
-- PostgreSQL
select dt + (1 * interval '1 day') as "NextDay", (date_trunc('month', dt) + interval '1 month - 1 day') as "MonthEnd" from complex_entity
-- ClickHouse
select addDays(dt, 1) as `NextDay`, toLastDayOfMonth(dt) as `MonthEnd` from complex_entity
-- MySQL/MariaDB
select date_add(dt, interval 1 day) as `NextDay`, last_day(dt) as `MonthEnd` from complex_entity
-- SQLite
select datetime(dt, (1) || ' days') as 'NextDay', date(dt, 'start of month', '+1 month', '-1 day') as 'MonthEnd' from complex_entity
```

| C# | SQL Server | PostgreSQL | ClickHouse | MySQL/MariaDB | SQLite |
|---|---|---|---|---|---|
| `NORM.SQL.date_add("day", n, x)` | `dateadd(day, n, x)` | `x + (n * interval '1 day')` | `addDays(x, n)` | `date_add(x, interval n day)` | `datetime(x, (n) \|\| ' days')` |
| `NORM.SQL.date_add("decade", n, x)` | `dateadd(year, (n) * 10, x)` | `x + (n * interval '10 years')` | `addYears(x, (n) * 10)` | `date_add(x, interval (n) * 10 year)` | `datetime(x, ((n) * 10) \|\| ' years')` |
| `NORM.SQL.end_of_month(x)` | `eomonth(x)` | `date_trunc('month', x) + interval '1 month - 1 day'` | `toLastDayOfMonth(x)` | `last_day(x)` | `date(x, 'start of month', '+1 month', '-1 day')` |
| `NORM.SQL.date_diff("day", a, b)` | `datediff(day, a, b)` | `cast(b as date) - cast(a as date)` | `dateDiff('day', a, b)` | `timestampdiff(day, a, b)` | `(strftime('%s', b) - strftime('%s', a)) / 86400` |
| `NORM.SQL.date_from_parts(y, m, d)` | `datefromparts(y, m, d)` | `make_date(y, m, d)` | `makeDate(y, m, d)` | `str_to_date(concat_ws('-', y, m, d), '%Y-%m-%d')` | `date(printf('%04d-%02d-%02d', y, m, d))` |
| `x.AddDays(7)` | `dateadd(day, 7, x)` | `x + (7 * interval '1 day')` | `addDays(x, 7)` | `date_add(x, interval 7 day)` | `datetime(x, (7) \|\| ' days')` |
| `x.AddMonths(2)` | `dateadd(month, 2, x)` | `x + (2 * interval '1 month')` | `addMonths(x, 2)` | `date_add(x, interval 2 month)` | `datetime(x, (2) \|\| ' months')` |

## Строковые и массивные агрегаты

`NORM.SQL.string_agg` доступен в PostgreSQL, SQL Server 2017+, ClickHouse, MySQL/MariaDB и SQLite
(`ISqlDialect.SupportsStringAgg`, по умолчанию берёт значение зонтичного
`SupportsStringArrayAggregates`); в ClickHouse он рендерится как
`arrayStringConcat(groupArray(x), delimiter)`, в MySQL/MariaDB — как
`group_concat(x separator delimiter)`, в SQLite — как `group_concat(x, delimiter)`.
`NORM.PG_SQL.array_agg` (`ISqlDialect.SupportsArrayAgg`) требует типа-массива, поэтому доступен только в
PostgreSQL. Результат `array_agg` — колонка-массив:

```csharp
var names = dataContext.From<IComplexEntity>()
    .Select(e => NORM.SQL.string_agg(e.String, ","))
    .First();
```

```sql
-- PostgreSQL / SQL Server
select string_agg(somestring, ',') from complex_entity
-- ClickHouse
select arrayStringConcat(groupArray(somestring), ',') from complex_entity
-- MySQL/MariaDB
select group_concat(somestring separator ',') from complex_entity
-- SQLite
select group_concat(somestring, ',') from complex_entity
```

| C# | SQL | Провайдеры |
|---|---|---|
| `NORM.SQL.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` / `group_concat(x separator delimiter)` / `group_concat(x, delimiter)` | PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB, SQLite |
| `NORM.PG_SQL.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## Фильтр агрегатов (FILTER)

`count`/`count_big`/`min`/`max`/`avg`/`sum` и строковые/массивные агрегаты принимают дополнительный
аргумент `Expression<Func<bool>>`, который рендерит предложение `filter (where ...)`. Предикат фильтра —
обычный предикат запроса и может ссылаться на колонки и параметры. Предложение включается флагом
`ISqlDialect.SupportsFilter` (его включают PostgreSQL и SQLite):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Big = NORM.SQL.count(() => e.Id > 10L),
        Total = NORM.SQL.sum(e.Id, () => e.Boolean == true)
    })
    .ToList();
```

```sql
select nullableint, count(*) filter (where (id > 10)) as "Big", sum(id) filter (where (b = true)) as "Total"
from complex_entity group by nullableint
```

В ClickHouse аналог фильтрованного агрегата — комбинатор `-If` (`countIf`, `sumIf`, `avgIf`, `minIf`,
`maxIf`), доступный как `NORM.CLK_SQL.count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` (`SupportsIfAggregates`).
ClickHouse не принимает ANSI-предложение `filter (where ...)`, поэтому обобщённый API фильтрованных
агрегатов там отклоняется.

## Функции, возвращающие наборы (PostgreSQL)

`NORM.PG_SQL.generate_series` и `NORM.PG_SQL.unnest` — это предобъявленные источники
[`[SqlTableFunction]`](13-table-valued-functions.md), поэтому отдельная обёртка не нужна:

```csharp
var numbers = dataContext
    .FromTableFunction(() => NORM.PG_SQL.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => NORM.PG_SQL.unnest(NORM.Param<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`generate_series` проецируется на `NORM.IGenerateSeriesRow.Value`, а `unnest` — на
`NORM.IUnnestRow<T>.Value`; обе соответствуют единственной колонке, которую возвращает функция.

## Таблица сопоставления провайдеров

| Возможность | SQLite | SQL Server | PostgreSQL |
|---|---|---|---|
| `ToUpper` / `ToLower` | `upper` / `lower` | `upper` / `lower` | `upper` / `lower` |
| `Length` | `length` | `len` | `length` |
| `Substring` | `substring`, 1-based | `substring`, 1-based | `substring`, 1-based |
| `Trim` / `TrimStart` / `TrimEnd` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` |
| `Replace` | `replace` | `replace` | `replace` |
| `Contains` / `StartsWith` / `EndsWith` / `like` | `like` (escape `\`) | `like` (escape `\`) | `like` (escape `\`) |
| `string.IsNullOrEmpty` | `(x is null or x = '')` | `(x is null or x = '')` | `(x is null or x = '')` |
| `Abs` | `abs` | `abs` | `abs` |
| `Round` | `round(x)` | `round(x, 0)` | `round(x)` |
| `Truncate` | `trunc` | `round(x, 0, 1)` | `trunc` |
| `Log` (натуральный) | `ln` | `log` | `ln` |
| `Now` / `UtcNow` | `datetime('now')` / `datetime('now')` | `getdate()` / `getutcdate()` | `now()` / `now() at time zone 'utc'` |
| `Year` / `Month` / `Day` / `Hour` | `cast(strftime('%Y'...`/`'%m'`/`'%d'`/`'%H'` `as integer)` | `datepart(year, ...)` и т. д. | `extract(year from ...)` и т. д. |
| `??` объединение | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` |
| Конкатенация строк (`+`) | `\|\|` | `+` | `\|\|` |
| Логический предикат как значение | без изменений | `cast(case when ... then 1 else 0 end as bit)` | без изменений |
| Массивы (`any`/`all`, функции массивов) | `NotSupportedException` | `NotSupportedException` | `any(@array)`, `cardinality(...)`, ... |
| JSON/JSONB (`json_agg`, `->`, ...) | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Текстовый JSON (`json_value`, `json_query`, `json_modify`, `isjson`) | `NotSupportedException` | `json_value(...)`, ..., `isjson(...)` | `NotSupportedException` |
| `nullif` | поддерживается | поддерживается | поддерживается |
| `greatest` / `least` | `NotSupportedException` | поддерживается (2022+) | поддерживается |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | поддерживается |
| `date_add` / `end_of_month` / `date_diff` / `date_from_parts` | `datetime(x, n \|\| ' days')` / `date(x, 'start of month', ...)` / разность `strftime` / `date(printf(...))` | `dateadd(...)` / `eomonth(...)` / `datediff(...)` / `datefromparts(...)` | интервальная арифметика / `date_trunc` / разность частей даты / `make_date` |
| `string_agg` / `array_agg` | `group_concat(x, delimiter)` (нет `array_agg`) | `string_agg` (2017+); `array_agg` бросает исключение | поддерживается |
| `filter (where ...)` у агрегатов | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Расширенная библиотека скалярных функций (`asin`, `split_part`, `regexp_*`, `to_char`, ...) | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Логические/битовые/статистические агрегаты | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Упорядоченные агрегаты (`percentile_cont`, ...) | `NotSupportedException` | `NotSupportedException` | `within group (order by ...)` |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Встроенные табличные функции | `NotSupportedException` | `string_split(...)`, `openjson(...)` | `generate_series(...)`, `unnest(...)` |

Провайдер in-memory не рендерит SQL: он компилирует и вычисляет выражение для строк в памяти, поэтому
выполняется сам метод .NET. Приведённая выше матрица SQL относится к провайдерам SQLite, SQL Server и
PostgreSQL.

ClickHouse рендерит `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (и масштабированный
`addYears` для `decade`/`century`/`millennium`), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax` и комбинаторы `-If`. Он отклоняет ANSI-предложение
`filter (where ...)`, агрегаты `regr_*` и логические агрегаты через `NotSupportedException`; см.
[Провайдер ClickHouse](../providers/clickhouse.md).

## Явно не поддерживается

Эти случаи бросают `NotSupportedException`, а не генерируют SQL с другой семантикой:

* `string.IsNullOrWhiteSpace(x)` — бросает исключение с сообщением, упоминающим `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `test/nextorm.sqlite.tests/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` — у двухаргументной формы порядок аргументов зависит от провайдера, поэтому она
  оставлена неподдерживаемой (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `test/nextorm.sqlite.tests/SqlGenerationTests.cs:985`). Двухаргументная форма PostgreSQL доступна как
  `NORM.PG_SQL.log(base, x)` в расширенной библиотеке скалярных функций.
* Перегрузки `Math.Round`, принимающие `MidpointRounding` (больше двух аргументов), — не переносимы.
* `string.Substring(Range)` — нет эквивалента в SQL.

## См. также

* [Фильтрация (WHERE)](02-filtering-where.md) — `Contains`/`in`, `??` и условные выражения в предикатах.
* [Группировка и агрегаты](04-grouping-and-aggregates.md) — агрегатные функции (`count`, `sum`, ...).
* [Пользовательские функции](12-user-defined-functions.md) — когда скалярная функция не встроена.
* [Обзор провайдеров](../providers/overview.md) — флаги возможностей и кавычки.

---

Source: `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:541`, `:1157`, `:1204`, `:1752`;
`src/nextorm.core/Visitors/BuiltinFunctionTranslator.cs`, `src/nextorm.core/Visitors/AggregateFilter.cs`;
`src/nextorm.core/Query/NORM.cs`;
`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:57`;
`test/nextorm.integration.tests/CommonTestSuite.Functions.cs:8`, `:19`, `:43`, `:54`, `:65`, `:76`, `:87`, `:98`, `:117`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:695`, `:775`, `:801`, `:811`, `:832`, `:852`, `:861`, `:872`, `:882`, `:892`, `:912`, `:921`, `:931`, `:940`, `:950`, `:960`, `:974`, `:985`;
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:457`, `:512`, `:522`, `:533`, `:543`, `:552`, `:563`, `:573`, `:583`, `:593`, `:602`, `:613`, `:624`, `:633`, `:643`;
`test/nextorm.postgres.tests/SqlGenerationTests.cs:390`, `:445`, `:465`, `:475`, `:484`, `:495`, `:505`, `:515`, `:525`, `:534`, `:544`, `:554`, `:563`, `:573`.
