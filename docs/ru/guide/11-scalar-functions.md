# Скалярные функции

> Преобразуйте члены `string`, `Math` и `DateTime`, объединение `??`, логические предикаты и числовые
> преобразования в SQL, специфичный для провайдера.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md)

## Обзор

nextorm распознаёт фиксированный набор членов CLR и переписывает их в SQL внутри любого выражения запроса.
Диспетчеризация находится в [`BaseExpressionVisitor`](xref:NextORM.Core.BaseExpressionVisitor): методы `string`, методы `Math`, члены `DateTime`,
`SqlFunctions.Sql.like`, оператор `??` и числовые преобразования. Всё, что зависит от провайдера, делегируется
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect), поэтому один и тот же код C# генерирует правильную функцию на каждом провайдере.

Повсюду действуют два правила:

* **захваченное** значение (локальная переменная или параметр) становится **параметром** запроса, а не
  литералом;
* **константа** встраивается. Для `Contains`/`StartsWith`/`EndsWith` это также означает, что `%`, `_` и `\`
  в константе экранируются и генерируется предложение `escape '\'`.

Встроенные преобразования применяются **до** любого сопоставления
[`[SqlFunction]`](12-user-defined-functions.md), поэтому пользовательский атрибут не может изменить
поведение членов `string`/`Math`/`DateTime`.

Кросс-провайдерные помощники находятся в `` Функции, которые поддерживает только один
провайдер, сгруппированы в отдельную провайдерную поверхность: [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (PostgreSQL: нативные
массивы, нативный JSON, расширенная библиотека скалярных функций, PG-only агрегаты и табличные
функции `generate_series`/`unnest`), [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) (SQL Server: JSON-как-текст и
`string_split`/`openjson`) и [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (ClickHouse: `arg_min`/`arg_max`, комбинатор `-If`,
семейство строкового JSON `JSONExtract*`, быстрый разбор плоского JSON `visitParamExtract*`,
JSONPath-скаляры `json_value`/`json_query`/`json_exists` и функции
словарей `dict_get`/`dict_get_or_default`/`dict_has`/`dict_get_hierarchy`/`dict_get_children`/`dict_is_in`).
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
| `s.ToUpperInvariant()` / `s.ToLowerInvariant()` | `upper(s)` / `lower(s)` | То же, что `ToUpper`/`ToLower`; см. примечание об инварианте ниже. |
| `s.ToUpper(CultureInfo.InvariantCulture)` | `upper(s)` | Любая другая `CultureInfo` отклоняется. |
| `s.Equals(t)` / `string.Equals(s, t)` | `s collate <binary> = t collate <binary>` | `string.Equals` в C# — ordinal. |
| `s.Equals(t, StringComparison.OrdinalIgnoreCase)` | ordinal-равенство со свёрткой регистра | См. [Ordinal-сравнение и коллация](#ordinal-сравнение-и-коллация). |
| `string.CompareOrdinal(s, t)` / `string.Compare(s, t, StringComparison.Ordinal)` | знаковый `CASE`, возвращающий `-1`/`0`/`1` | Сохраняется внешнее сравнение `< 0` / `> 0`. |
| `s.Contains(x, comparison)` / `StartsWith` / `EndsWith` | `LIKE` с коллацией/свёрткой регистра | |
| `s.IndexOf(x, comparison)` / `LastIndexOf(x, comparison)` | позиция с коллацией/свёрткой регистра | |
| `SqlFunctions.Sql.like(s, pattern)` | `s like pattern` | Явный `LIKE`. |
| `SqlFunctions.Sql.like(s, pattern, escape)` | `s like pattern escape escape` | |
| `SqlFunctions.Sql.collate(s, name)` | `s collate name` | Коллация на уровне выражения. |
| `[Collation("name")]` / `.Collation("name")` | `s collate name` | Коллация на уровне столбца; см. [Коллация на уровне столбца](#коллация-на-уровне-столбца). |

`SqlFunctions.Sql.like` — это запасной вариант, когда шаблон не является простым
`Contains`/`StartsWith`/`EndsWith`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Sql.like(e.String, "%a%"))
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

### Ordinal-сравнение и коллация

Сравнения `string` в C# — **ordinal** (побайтовые), тогда как SQL `=`/`LIKE` использует коллацию
столбца — в SQL Server по умолчанию регистронезависимую. nextorm делает ordinal-перегрузки явными и
честными: `string.Equals`, `string.Compare`/`CompareOrdinal`, `Contains`/`StartsWith`/`EndsWith` и
`IndexOf`/`LastIndexOf` принимают константный `StringComparison`, и только `Ordinal` и
`OrdinalIgnoreCase` имеют переносимую SQL-форму. `Ordinal` использует бинарную коллацию провайдера,
`OrdinalIgnoreCase` дополнительно сворачивает регистр обоих операндов;
`InvariantCulture`/`CurrentCulture` (и culture-sensitive `string.Compare(s, t)` без сравнения)
бросают `NotSupportedException`
([`SupportsOrdinalComparison`](xref:NextORM.Core.ISqlDialect.SupportsOrdinalComparison)).

```csharp
var strict = dataContext.From<IComplexEntity>()
    .Where(e => e.String!.Contains("X", StringComparison.Ordinal))       // регистрозависимо
    .Select(e => new { e.Id })
    .ToList();

var loose = dataContext.From<IComplexEntity>()
    .Where(e => e.String!.StartsWith("x", StringComparison.OrdinalIgnoreCase))
    .Select(e => new { e.Id })
    .ToList();
```

| Провайдер | `Ordinal` | `OrdinalIgnoreCase` |
|---|---|---|
| PostgreSQL | `s collate "C"` | `lower(s) collate "C"` |
| SQL Server | `s collate Latin1_General_100_BIN2` | `lower(s) collate Latin1_General_100_BIN2` |
| MySQL/MariaDB | `s collate utf8mb4_bin` | `lower(s) collate utf8mb4_bin` |
| SQLite | `s collate binary` | `lower(s) collate binary` |
| ClickHouse | `s` (нативный побайтовый порядок) | `lower(s)` |

Оператор `==` намеренно оставлен как SQL `=` провайдера: он следует коллации БД, а не C#. Для
побайтового порядка используйте `string.Equals`/`string.CompareOrdinal`.

`LIKE` в SQLite всегда регистронезависим для ASCII независимо от коллации операнда, поэтому
**регистрозависимые** ordinal `Contains`/`StartsWith`/`EndsWith` там отклоняются
([`SupportsOrdinalLike`](xref:NextORM.Core.ISqlDialect.SupportsOrdinalLike)); `CompareOrdinal`,
`Equals` и `OrdinalIgnoreCase` работают.

Применить произвольную коллацию провайдера можно через `SqlFunctions.Sql.collate` (требует
[`SupportsCollation`](xref:NextORM.Core.ISqlDialect.SupportsCollation), то есть все SQL-провайдеры
кроме ClickHouse):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Sql.collate(e.String, "C") == SqlFunctions.Sql.collate("x", "C"))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
-- PostgreSQL
select id from complex_entity where somestring collate "C" = 'x' collate "C"
```

`collate` принимает родное для провайдера имя коллации; оно должно быть константой. У in-memory
провайдера коллаций нет, и вызов трактуется как ordinal-тождество.

#### Коллация на уровне столбца

Вместо обёртки каждого выражения коллацию можно объявить на свойстве сущности через
[`CollationAttribute`](xref:NextORM.Core.CollationAttribute) или fluent-метод
[`EntityPropertyBuilder<T>.Collation`](xref:NextORM.Core.EntityPropertyBuilder`1.Collation(System.String)). Тогда она применяется к
столбцу во всех collation-чувствительных операциях (сравнение, `LIKE`, `ORDER BY`, `GROUP BY`), и
столбец следует объявленной коллации, а не дефолту БД:

```csharp
[SqlTable("complex_entity")]
public interface IComplexEntity
{
    [Column("somestring")]
    [Collation("C")]
    string? String { get; set; }
}

// эквивалентный fluent-маппинг
var e = dataContext.From<IComplexEntity>(b => b.Property(x => x.String!).Collation("C"));

var rows = e
    .Where(x => x.String == "x")   // somestring collate "C" = 'x'
    .OrderBy(x => x.String)        // order by somestring collate "C"
    .Select(x => new { x.String })
    .ToList();
```

```sql
-- PostgreSQL
select somestring collate "C" as "String" from complex_entity
 where somestring collate "C" = 'x' order by somestring collate "C"
```

Имя коллации — родное для провайдера (квотируется там, где требует провайдер, см.
[`MakeCollate`](xref:NextORM.Core.ISqlDialect.MakeCollate(System.String,System.String,NextORM.Core.KeywordCase))), и провайдер должен
поддерживать коллацию на уровне выражения ([`SupportsCollation`](xref:NextORM.Core.ISqlDialect.SupportsCollation)):
PostgreSQL, SQL Server, MySQL/MariaDB и SQLite поддерживают, у ClickHouse `COLLATE` нет — колонка с
объявленной коллацией бросает `NotSupportedException`. Явные ordinal-перегрузки (`string.Equals`,
`string.CompareOrdinal`, `StringComparison`) по-прежнему переопределяют коллацию столбца бинарной.
nextorm не генерирует DDL, поэтому объявление описывает коллацию **существующего** столбца, а не
создаёт её; in-memory провайдер его игнорирует (его сравнения и так ordinal).

### Регулярные выражения

`Regex.IsMatch(value, pattern[, options])` и `Regex.Replace(value, pattern, replacement[, options])`
транслируются в родные регулярные выражения провайдера. Экземплярные формы `regex.IsMatch(value)` и
`regex.Replace(value, replacement)` читают шаблон и опции из константного `Regex` (литерал,
`new Regex(...)` или захваченная локальная переменная). Гейт провайдера —
[`SupportsRegex`](xref:NextORM.Core.ISqlDialect.SupportsRegex).

```csharp
using System.Text.RegularExpressions;

var rows = dataContext.From<IComplexEntity>()
    .Where(e => Regex.IsMatch(e.String!, "^d"))
    .Select(e => new { e.Id, Cleaned = Regex.Replace(e.String!, "[0-9]+", "#") })
    .ToList();
```

Шаблон, замена и опции должны быть **константами времени компиляции**: шаблон-значение невозможно
скомпилировать средствами CLR в собственный диалект регулярных выражений провайдера, поэтому такой
вызов бросает `NotSupportedException`. SQL меняет только `RegexOptions.IgnoreCase`;
`RegexOptions.Compiled` и `RegexOptions.CultureInvariant` принимаются как no-op, а любая другая опция
(например, `Multiline`) отклоняется. `Regex.Replace`, как и метод CLR, заменяет все совпадения.

| Провайдер | `Regex.IsMatch` | `Regex.Replace` | `RegexOptions.IgnoreCase` |
|---|---|---|---|
| PostgreSQL | `s ~ 'p'` | `regexp_replace(s, 'p', 'r', 'g')` | `s ~* 'p'` / `'gi'` |
| MySQL | `regexp_like(s, 'p', 'c')` | `regexp_replace(s, 'p', 'r', 1, 0, 'c')` | match type `'i'` |
| MariaDB | `s regexp '(?-i)p'` | `regexp_replace(s, '(?-i)p', 'r')` | флаг `(?i)` |
| ClickHouse | `match(s, 'p')` | `replaceRegexpAll(s, 'p', 'r')` | флаг `(?i)` |
| SQLite | `s regexp 'p'` | `regexp_replace(s, 'p', 'r')` | флаг `(?i)` |
| SQL Server | `NotSupportedException` | `NotSupportedException` | — |

Как и метод CLR, совпадение ищется в любом месте значения, поэтому `^`/`$` привязывают всё значение, а
шаблон не анкорится неявно. Шаблон исполняет **собственный** движок провайдера (POSIX/ARE в PostgreSQL,
ICU в MySQL, PCRE в MariaDB, RE2 в ClickHouse, .NET в SQLite), поэтому сложный шаблон — lookaround,
обратные ссылки, именованные группы — в общем случае непереносим; также различаются правила
экранирования C# и SQL и синтаксис ссылок на группы в замене (C# `$1` против SQL `\1`). SQLite
сопоставляет через CLR-функции `regexp`/`regexp_replace`, регистрируемые на каждом соединении, поэтому
сохраняет синтаксис .NET. В SQL Server движка регулярных выражений нет, поэтому вызов отклоняется:
используйте [`SqlFunctions.Sql.like`](xref:NextORM.Core.CommonFunctions.like(System.String,System.String))
для простых шаблонов. In-memory провайдер выполняет `System.Text.RegularExpressions` нативно.

## Кросс-провайдерные скалярные функции

[`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) предоставляет небольшой набор строковых и
числовых функций, которые рендерятся нативно у каждого провайдера, умеющего их выразить. Каждый вызов
гейтится **по функции** через [`ISqlDialect.ScalarFunctions`](xref:NextORM.Core.ISqlDialect.ScalarFunctions)
и [`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)): провайдер без нативной формы
отклоняет вызов понятным `NotSupportedException`, а не рендерит SQL, который не сможет выполнить.

| C# | PostgreSQL | SQL Server | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|
| `SqlFunctions.Sql.left(s, n)` | `left` | `LEFT` | `LEFT` | `leftUTF8` | `substr(s, 1, n)` |
| `SqlFunctions.Sql.right(s, n)` | `right` | `RIGHT` | `RIGHT` | `rightUTF8` | `substr` (с защитой) |
| `SqlFunctions.Sql.lpad(s, n, pad)` | `lpad` | `RIGHT(REPLICATE(...))` | `LPAD` | `leftPadUTF8` | — |
| `SqlFunctions.Sql.rpad(s, n, pad)` | `rpad` | `LEFT(s + REPLICATE(...))` | `RPAD` | `rightPadUTF8` | — |
| `SqlFunctions.Sql.repeat(s, n)` | `repeat` | `REPLICATE` | `REPEAT` | `repeat` | — |
| `SqlFunctions.Sql.reverse(s)` | `reverse` | `REVERSE` | `REVERSE` | `reverseUTF8` | — |
| `SqlFunctions.Sql.space(n)` | `repeat(' ', n)` | `SPACE` | `SPACE` | `space` | — |
| `SqlFunctions.Sql.concat_ws(sep, ...)` | `concat_ws` | `CONCAT_WS` | `CONCAT_WS` | `concatWithSeparator` | `concat_ws` |
| `SqlFunctions.Sql.translate(s, from, to)` | `translate` | `TRANSLATE` | — | `translate` | — |
| `SqlFunctions.Sql.ascii(s)` | `ascii` | `ASCII` | `ASCII` | `ascii` | `unicode` |
| `SqlFunctions.Sql.@char(n)` | `chr` | `CHAR` | `CAST(CHAR(..) AS CHAR)` | `char` | `char` |

- `left`/`right`/`lpad`/`rpad` считают **символы**, а не байты (ClickHouse использует варианты `*UTF8`).
  Отрицательный `n` зависит от провайдера: PostgreSQL читает его как «всё, кроме последних |n|», остальные — нет.
- `concat_ws` пропускает NULL-аргументы у всех провайдеров, кроме ClickHouse: его
  `concatWithSeparator` возвращает NULL, если любой аргумент NULL.
- `lpad`/`rpad` усекают значение, которое уже длиннее целевой длины, как и семейство SQL
  `lpad`/`rpad` (поэтому они не совпадают с `string.PadLeft`/`PadRight`).
- В MySQL и MariaDB нет `translate`; в SQLite нет `lpad`, `rpad`, `repeat`, `reverse`, `space` и
  `translate`. Такие вызовы отклоняются у соответствующего провайдера.
- Остаток, десятичный логарифм и возведение в степень остаются на переносимых CLR-методах (`%`,
  `Math.Log10`, `Math.Pow`), которые уже транслируются у каждого провайдера — отдельной формы
  `SqlFunctions.Sql` для них нет.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Prefix = SqlFunctions.Sql.left(e.String, 3),
        Padded = SqlFunctions.Sql.lpad(e.String, 8, "0"),
        Joined = SqlFunctions.Sql.concat_ws("-", e.String, "x")
    })
    .ToList();
```

In-memory провайдер вычисляет те же вызовы через их CLR-эквиваленты, поэтому тот же запрос выполняется
без базы данных.

## Расширения строк и регулярных выражений (PostgreSQL)

Помимо переносимых методов `string` и [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) выше,
[`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) предоставляет оставшуюся часть строковой
библиотеки (только PostgreSQL) и функции POSIX-регулярных выражений. Они входят в расширенную
библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), только PostgreSQL):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.split_part(s, delim, n)` | `split_part(s, delim, n)` |
| `SqlFunctions.Postgres.strpos(s, sub)` | `strpos(s, sub)` |
| `SqlFunctions.Postgres.initcap(s)` | `initcap(s)` |
| `SqlFunctions.Postgres.overlay(s, placing, from, count)` | `overlay(s, placing, from, count)` |
| `SqlFunctions.Postgres.format(fmt, ...)` | `format(fmt, ...)` |
| `SqlFunctions.Postgres.md5(s)` | `md5(s)` |
| `SqlFunctions.Postgres.digest(s\|bytes, type)` | `digest(data, type)` (требует расширения `pgcrypto`) |
| `SqlFunctions.Postgres.sha224(bytes)` / `sha384(bytes)` / `sha512(bytes)` | `sha224(bytes)` / `sha384(bytes)` / `sha512(bytes)` |
| `SqlFunctions.Postgres.sha256(bytes)` | `sha256(bytes)` |
| `SqlFunctions.Postgres.regexp_replace(s, pattern, replacement[, flags])` | `regexp_replace(...)` |
| `SqlFunctions.Postgres.regexp_like(s, pattern[, flags])` | `regexp_like(...)` |
| `SqlFunctions.Postgres.regexp_split_to_array(s, pattern)` | `regexp_split_to_array(s, pattern)` |
| `SqlFunctions.Postgres.regexp_count(s, pattern)` | `regexp_count(s, pattern)` |
| `SqlFunctions.Postgres.regexp_instr(s, pattern)` | `regexp_instr(s, pattern)` |
| `SqlFunctions.Postgres.regexp_substr(s, pattern[, flags])` | `regexp_substr(...)` |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Part = SqlFunctions.Postgres.split_part(e.String, ",", 1),
        LooksLikeA = SqlFunctions.Postgres.regexp_like(e.String, "^a")
    })
    .ToList();
```

## Математические функции

| C# | SQL | Примечания |
|---|---|---|
| `Math.Abs(x)` | `abs(x)` | |
| `Math.Round(x)` | `round(x)` / `round(x, 0)` | SQL Server требует аргумент длины. |
| `Math.Round(x, digits)` | `round(x, digits)` | PostgreSQL приводит первый аргумент `double`/`float` к `numeric` (`round((x)::numeric, digits)`), так как в нём нет `round(double precision, integer)`. |
| `Math.Truncate(x)` | `trunc(x)` / `round(x, 0, 1)` | В SQL Server нет `trunc`. |
| `Math.Log(x)` | натуральный логарифм: `ln(x)` (SQLite, PostgreSQL) / `log(x)` (SQL Server) | Только одноаргументная форма. |
| `Math.Log10(x)` | `log10(x)` | |

```csharp
var values = dataContext.From<IComplexEntity>()
    .Select(e => Math.Abs(e.Id - 5))
    .ToList();
// ids 1, 2, 3 -> 4, 3, 2
```

```sql
select abs((id - 5)) from complex_entity
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Abs |
|-----|
| 4   |
| 3   |
| 2   |

### Расширенная математика PostgreSQL

Остальные математические функции входят в расширенную библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), только PostgreSQL):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.asin(x)` / `acos(x)` / `atan(x)` | `asin(x)` / `acos(x)` / `atan(x)` |
| `SqlFunctions.Postgres.atan2(y, x)` | `atan2(y, x)` |
| `SqlFunctions.Postgres.cbrt(x)` | `cbrt(x)` |
| `SqlFunctions.Postgres.sinh(x)` / `cosh(x)` / `tanh(x)` | `sinh(x)` / `cosh(x)` / `tanh(x)` |
| `SqlFunctions.Postgres.asinh(x)` / `acosh(x)` / `atanh(x)` | `asinh(x)` / `acosh(x)` / `atanh(x)` |
| `SqlFunctions.Postgres.degrees(x)` / `SqlFunctions.Postgres.radians(x)` | `degrees(x)` / `radians(x)` |
| `SqlFunctions.Postgres.pi()` / `SqlFunctions.Postgres.random()` | `pi()` / `random()` |
| `SqlFunctions.Postgres.log(base, x)` | `log(base, x)` |
| `SqlFunctions.Postgres.gcd(a, b)` / `lcm(a, b)` | `gcd(a, b)` / `lcm(a, b)` |
| `SqlFunctions.Postgres.factorial(n)` | `factorial(n)` |
| `SqlFunctions.Postgres.width_bucket(x, low, high, count)` | `width_bucket(x, low, high, count)` |

`SqlFunctions.Postgres.setseed(seed)` рендерит `setseed(seed)` и гейтится отдельно
[`SupportsRandomSeed`](xref:NextORM.Core.ISqlDialect.SupportsRandomSeed) (только PostgreSQL). Функция
PostgreSQL возвращает `void`, поэтому проецируемое значение всегда `null`, а вызов делается ради
побочного эффекта (последующие `random()` в сессии становятся воспроизводимыми).

## Дата и время

`DateTime.Now` и `DateTime.UtcNow` рендерятся как SQL-выражения, а не вычисляются как параметр. `.Year`,
`.Month`, `.Day`, `.DayOfYear` и `.Hour` (а также `.Minute` и `.Second`) становятся извлечением части
даты, специфичным для провайдера:

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

Вывод:

| Year | Month | Day |
|------|-------|-----|
| 2023 | 1     | 1   |

Важная деталь для SQLite: `strftime` возвращает текст, поэтому результат оборачивается в
`cast(... as integer)`, чтобы материализоваться как свойство CLR `int`.

### Извлечение произвольных частей даты

`SqlFunctions.Sql.extract(part, value)` возвращает целочисленную часть даты для `year`, `quarter`,
`month`, `week` (ISO 8601), `day`, `doy`, `dow` (0=воскресенье..6=суббота), `isodow`
(1=понедельник..7=воскресенье), `hour`, `minute` и `second`. `SqlFunctions.Sql.date_part(part, value)`
возвращает числовую часть `epoch` (секунды с 1970-01-01, включая дробную часть). Обе принимают
константное имя части и рендерят нативную форму каждого провайдера, поэтому результат одинаков везде:

| Провайдер | `extract("quarter", dt)` | `extract("week", dt)` | `extract("dow", dt)` | `date_part("epoch", dt)` |
|---|---|---|---|---|
| PostgreSQL | `extract(quarter from dt)` | `extract(week from dt)` | `extract(dow from dt)` | `cast(extract(epoch from dt) as double precision)` |
| SQL Server | `datepart(quarter, dt)` | `datepart(isowk, dt)` | `(datepart(weekday, dt) + @@datefirst - 1) % 7` | `cast(datediff_big(millisecond, '19700101', dt) as float) / 1000.0` |
| MySQL/MariaDB | `quarter(dt)` | `weekofyear(dt)` | `(dayofweek(dt) - 1)` | `cast(unix_timestamp(dt) as double)` |
| SQLite | `cast((cast(strftime('%m', dt) as integer) + 2) / 3 as integer)` | ISO-неделя через `strftime('%j', date(dt, '-3 days', 'weekday 4'))` | `cast(strftime('%w', dt) as integer)` | `((julianday(dt) - 2440587.5) * 86400.0)` |
| ClickHouse | `toQuarter(dt)` | `toISOWeek(dt)` | `(toDayOfWeek(dt) % 7)` | `toFloat64(toUnixTimestamp(dt))` |

`DateTime.DayOfWeek` не транслируется как свойство (его аналог `datepart(weekday)` зависит от
сессионного `DATEFIRST`); для нормализованного значения используйте `extract("dow", value)` или
`extract("isodow", value)`.

### Расширенные дата и время PostgreSQL

Эти функции входят в расширенную библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), только PostgreSQL):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.make_interval(y, mo, d, h, mi, s)` | `make_interval(y, mo, 0, d, h, mi, s)` (параметр `weeks` у PostgreSQL зафиксирован в `0`) |
| `SqlFunctions.Postgres.make_time(h, mi, sec)` / `make_timestamp(y, mo, d, h, mi, sec)` | `make_time(...)` / `make_timestamp(...)` |
| `SqlFunctions.Postgres.age(a, b)` | `age(a, b)` (результат — `interval`; целая часть в месяцах читается как 30 дней) |
| `SqlFunctions.Postgres.date_bin(stride, source, origin)` | `date_bin(cast(stride as interval), source, origin)` |
| `SqlFunctions.Postgres.justify_days(interval)` / `justify_hours(interval)` | `justify_days(interval)` / `justify_hours(interval)` |
| `SqlFunctions.Postgres.to_char(value, format)` | `to_char(value, format)` |
| `SqlFunctions.Postgres.to_date(text, format)` | `to_date(text, format)` |
| `SqlFunctions.Postgres.to_number(text, format)` | `to_number(text, format)` |
| `SqlFunctions.Postgres.to_timestamp(epoch)` / `to_timestamp(text, format)` | `to_timestamp(...)` |
| `SqlFunctions.Postgres.timezone(zone, value)` | `timezone(zone, value)` |
| `SqlFunctions.Postgres.current_date()` / `current_time()` / `localtime()` / `localtimestamp()` | те же ключевые слова |
| `SqlFunctions.Postgres.pg_typeof(x)` | `cast(pg_typeof(x) as text)` |

Для построения дат и арифметики в остальном используется переносимый набор: `date_from_parts`
(рендерит PostgreSQL `make_date`), `date_add`, `date_diff`, `date_trunc` и члены `DateTime` (см.
[Арифметику дат](#арифметика-дат) ниже).

### Runtime-настройки и последовательности (PostgreSQL)

Также входят в расширенную скалярную библиотеку
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions),
только PostgreSQL). Имя последовательности приводится к `regclass`:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.current_setting(name)` / `current_setting(name, missingOk)` | `current_setting(name[, missing_ok])` |
| `SqlFunctions.Postgres.set_config(name, value, isLocal)` | `set_config(name, value, is_local)` |
| `SqlFunctions.Postgres.nextval(sequence)` | `nextval(cast(sequence as regclass))` |
| `SqlFunctions.Postgres.setval(sequence, value)` | `setval(cast(sequence as regclass), value)` |
| `SqlFunctions.Postgres.currval(sequence)` | `currval(cast(sequence as regclass))` |
| `SqlFunctions.Postgres.lastval()` | `lastval()` |

```csharp
var next = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.nextval("order_id_seq"))
    .First();
```

`currval`/`lastval` в PostgreSQL привязаны к сессии и потому требуют того же соединения, которое
последним продвинуло последовательность.

### Форматирование дат и чисел в строки

nextorm транслирует culture-invariant подмножество CLR-форматирования — `string.Format`,
интерполяцию со спецификатором (`$"{e.Created:yyyy-MM-dd}"`) и `value.ToString(format)` — в родную
функцию форматирования провайдера. Языки шаблонов несовместимы (SQL Server `FORMAT` использует
.NET-шаблоны, PostgreSQL `to_char`, а `DATE_FORMAT`/`strftime`/`formatDateTime` — свои `%`-шаблоны),
поэтому nextorm принимает только документированное переносимое подмножество и **отклоняет** всё
остальное, вместо того чтобы молча сгенерировать SQL с другим форматированием:

* **Числа** — стандартные спецификаторы `N`, `F`, `D`, `X` с необязательной точностью
  (`$"{amount:N2}"`, `value.ToString("D8")`). Провайдер, который не может выразить спецификатор
  точно (в MySQL/MariaDB нет `F` без группировки; в SQLite и ClickHouse нет инвариантного `N`),
  бросает `NotSupportedException`.
* **Даты** — токены `yyyy`, `yy`, `MM`, `dd`, `HH`, `mm`, `ss` с разделителями `-`, `/`, `.`, `:`,
  `T` и пробелом (`$"{e.Created:yyyy-MM-dd}"`). Всё остальное отклоняется.
* **Культура** — только инвариантная: `string.Format` без провайдера или с
  `CultureInfo.InvariantCulture`. Любой другой `IFormatProvider` бросает исключение.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        Total = string.Format("Total: {0:N2}", e.Numeric),
        Month = e.Datetime!.Value.ToString("yyyy-MM")
    })
    .ToList();
```

```sql
-- PostgreSQL
select id, ('Total: '||to_char(m, 'FM9999999999999990.' || repeat('0', 2))) as total, to_char(dt, 'YYYY-MM') as month
from complex_entity
```

| C# | PostgreSQL | SQL Server | MySQL/MariaDB | SQLite | ClickHouse |
|---|---|---|---|---|---|
| `N{p}` | — | `format(v, 'N{p}')` | `format(v, p)` | — | — |
| `F{p}` | `to_char(v, 'FM…0.{p}')` | `format(v, 'F{p}')` | — | `printf('%.{p}f', v)` | `format('{:.{p}f}', v)` |
| `D{p}` | `to_char(v, 'FM' \|\| repeat('0', {p}))` | `format(v, 'D{p}')` | `lpad(v, {p}, '0')` | `printf('%0{p}d', v)` | `leftPad(toString(v), {p}, '0')` |
| `X{p}` | `to_hex(v)` | `format(v, 'X{p}')` | `hex(v)` | `printf('%0{p}x', v)` | `hex(v)` |
| `yyyy-MM-dd` | `to_char(v, 'YYYY-MM-DD')` | `format(v, 'yyyy-MM-dd')` | `date_format(v, '%Y-%m-%d')` | `strftime('%Y-%m-%d', v)` | `formatDateTime(v, '%Y-%m-%d')` |

Для формата вне этого подмножества объявите пользовательскую функцию нужного провайдера. На SQL Server,
например, UDF `format`:

```csharp
public static class DemoUdf
{
    [SqlFunction("format")]
    public static string Format(DateTime? value, string format) => throw new NotSupportedException();
}
```

На PostgreSQL ту же задачу решает `SqlFunctions.Postgres.to_char(value, 'YYYY-MM')`
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions)).

### Информация о сессии и сервере

`SqlFunctions.Sql.current_user()`, `session_user()`, `current_schema()`, `current_database()` и
`version()` — кросс-провайдерные
([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)):

| C# | PostgreSQL | SQL Server | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|
| `current_user()` | `current_user` | `current_user` | `current_user()` | `currentUser()` | — |
| `session_user()` | `session_user` | `session_user` | `session_user()` | — | — |
| `current_schema()` | `current_schema` | `schema_name()` | `schema()` | — | — |
| `current_database()` | `current_database()` | `db_name()` | `database()` | `currentDatabase()` | — |
| `version()` | `version()` | `@@version` | `version()` | `version()` | `sqlite_version()` |

Провайдер, который не умеет функцию, выбрасывает `NotSupportedException`.

### Генераторы UUID

`SqlFunctions.Sql.gen_random_uuid()` (случайный v4) и `uuidv7()` — кросс-провайдерные
([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators)):

| C# | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `gen_random_uuid()` | `gen_random_uuid()` (13+) | `newid()` | — | `UUID_v4()` | `generateUUIDv4()` | — |
| `uuidv7()` | `uuidv7()` (18+) | — | — | `UUID_v7()` (11.7+) | `generateUUIDv7()` | — |

У MySQL есть только `UUID()` (v1), у SQLite генератора UUID нет, поэтому оба отклоняют вызов. Это
серверные генераторы, вычисляемые на каждую строку базой; `Guid.NewGuid()` — клиентское значение и
заменой не является.

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

Целевые типы числового приведения берутся из [`MakeTypeName`](xref:NextORM.Core.ISqlDialect.MakeTypeName(System.Type)):

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
запроса остаётся кэшируемым. Массивом может быть runtime-параметр ([`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32))),
захваченная локальная переменная/поле или встроенный `new[]`. Поверхность массивов умеет рендерить
только диалект, включивший [`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays) (PostgreSQL); все остальные провайдеры бросают
`NotSupportedException`.

`SqlFunctions.Postgres.any` / `SqlFunctions.Postgres.all` принимают массив — либо как готовый предикат (`column = any(@array)`),
либо как правую часть сравнения:

```csharp
var ids = new long[] { 1, 2, 3 };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.any(e.Id, ids))     // (id = any(@p0))
    .Select(e => new { e.Id })
    .ToList();

var same = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == SqlFunctions.Postgres.any(ids))   // id = any(@p0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (id = any(@p0))
```

Runtime-параметр-массив использует тот же механизм [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)), поэтому массив не нужно знать в момент
подготовки запроса:

```csharp
var prepared = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == SqlFunctions.Postgres.any(SqlFunctions.Parameter<long[]>(0)))
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
| `SqlFunctions.Postgres.cardinality(a)` | `cardinality(a)` |
| `SqlFunctions.Postgres.array_length(a, dim)` | `array_length(a, dim)` |
| `SqlFunctions.Postgres.array_ndims(a)` | `array_ndims(a)` |
| `SqlFunctions.Postgres.array_lower(a, dim)` | `array_lower(a, dim)` |
| `SqlFunctions.Postgres.array_upper(a, dim)` | `array_upper(a, dim)` |
| `SqlFunctions.Postgres.array_position(a, element)` | `array_position(a, element)` |
| `SqlFunctions.Postgres.array_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.array_overlaps(a, b)` | `a && b` |
| `SqlFunctions.Postgres.array_contained_by(a, b)` | `a <@ b` |
| `SqlFunctions.Postgres.array_concat(a, b)` | `a \|\| b` |
| `SqlFunctions.Postgres.array_cat(a, b)` | `array_cat(a, b)` |
| `SqlFunctions.Postgres.array_append(a, element)` | `array_append(a, element)` |
| `SqlFunctions.Postgres.array_prepend(element, a)` | `array_prepend(element, a)` |
| `SqlFunctions.Postgres.array_remove(a, element)` | `array_remove(a, element)` |
| `SqlFunctions.Postgres.array_replace(a, from, to)` | `array_replace(a, from, to)` |
| `SqlFunctions.Postgres.array_fill(value, dims)` | `array_fill(value, dims)` |
| `SqlFunctions.Postgres.array_dims(a)` | `array_dims(a)` |
| `SqlFunctions.Postgres.array_positions(a, element)` | `array_positions(a, element)` |
| `SqlFunctions.Postgres.array_reverse(a)` | `array_reverse(a)` (PostgreSQL 18+) |
| `SqlFunctions.Postgres.array_sort(a)` | `array_sort(a)` (PostgreSQL 18+) |
| `SqlFunctions.Postgres.array_shuffle(a)` | `array_shuffle(a)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_sample(a, n)` | `array_sample(a, n)` (PostgreSQL 16+) |
| `SqlFunctions.Postgres.array_to_string(a, delimiter)` | `array_to_string(a, delimiter)` |
| `SqlFunctions.Postgres.string_to_array(s, delimiter)` | `string_to_array(s, delimiter)` |

> Функции, возвращающие массив (`array_append`, `array_cat`, `array_reverse`, `string_to_array`, ...),
> можно использовать внутри запроса (предикат, `having` или вложенное выражение) или проецировать
> напрямую: row reader материализует результат `Array(T)` как CLR `T[]`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.array_length(SqlFunctions.Parameter<long[]>(0), 1) == 3)
    .Select(e => new { N = SqlFunctions.Postgres.cardinality(SqlFunctions.Parameter<long[]>(1)) })
    .ToList();
```

```sql
select cardinality(@norm_p1) as "N" from complex_entity where array_length(@norm_p0, 1) = 3
```

## Массивы (ClickHouse)

В ClickHouse есть нативный тип `Array(T)`. Функции массивов работают с array-**колонками** (или
вложенными array-выражениями) и включаются флагом `ISqlDialect.SupportsArrayFunctions`; для
`arrayJoin` дополнительно нужен `SupportsArrayJoin`. `arrayJoin(array)` разворачивает массив в одну
строку на элемент, поэтому его результат можно проецировать как скалярную колонку.

| Функция | SQL |
|---|---|
| `SqlFunctions.ClickHouse.length(a)` | `length(a)` |
| `SqlFunctions.ClickHouse.has(a, element)` | `has(a, element)` |
| `SqlFunctions.ClickHouse.index_of(a, element)` | `indexOf(a, element)` |
| `SqlFunctions.ClickHouse.has_any(a, b)` | `hasAny(a, b)` |
| `SqlFunctions.ClickHouse.has_all(a, b)` | `hasAll(a, b)` |
| `SqlFunctions.ClickHouse.starts_with(a, prefix)` | `startsWith(a, prefix)` |
| `SqlFunctions.ClickHouse.ends_with(a, suffix)` | `endsWith(a, suffix)` |
| `SqlFunctions.ClickHouse.has_substr(a, other)` | `hasSubstr(a, other)` |
| `SqlFunctions.ClickHouse.array_string_concat(a, delimiter)` | `arrayStringConcat(a, delimiter)` |
| `SqlFunctions.ClickHouse.split_by_char(separator, s)` | `splitByChar(separator, s)` |
| `SqlFunctions.ClickHouse.array_sort(a)` | `arraySort(a)` |
| `SqlFunctions.ClickHouse.array_reverse(a)` | `arrayReverse(a)` |
| `SqlFunctions.ClickHouse.array_distinct(a)` | `arrayDistinct(a)` |
| `SqlFunctions.ClickHouse.range(start, end)` | `range(start, end)` |
| `SqlFunctions.ClickHouse.array_enumerate(a)` | `arrayEnumerate(a)` |
| `SqlFunctions.ClickHouse.array_cum_sum(a)` | `arrayCumSum(a)` |
| `SqlFunctions.ClickHouse.array_slice(a, offset, length)` | `arraySlice(a, offset, length)` |
| `SqlFunctions.ClickHouse.array_push_back(a, element)` | `arrayPushBack(a, element)` |
| `SqlFunctions.ClickHouse.array_join(a)` | `arrayJoin(a)` |
| `SqlFunctions.ClickHouse.group_array(a)` | `groupArray(a)` |
| `SqlFunctions.ClickHouse.group_uniq_array(a)` | `groupUniqArray(a)` |
| `SqlFunctions.ClickHouse.array_map(f, a)` | `arrayMap(f, a)` |
| `SqlFunctions.ClickHouse.array_filter(f, a)` | `arrayFilter(f, a)` |
| `SqlFunctions.ClickHouse.array_exists(f, a)` | `arrayExists(f, a)` |
| `SqlFunctions.ClickHouse.array_all(f, a)` | `arrayAll(f, a)` |
| `SqlFunctions.ClickHouse.array_count(f, a)` | `arrayCount(f, a)` |
| `SqlFunctions.ClickHouse.array_first(f, a)` | `arrayFirst(f, a)` |
| `SqlFunctions.ClickHouse.array_first_index(f, a)` | `arrayFirstIndex(f, a)` |
| `SqlFunctions.ClickHouse.array_last(f, a)` | `arrayLast(f, a)` |
| `SqlFunctions.ClickHouse.array_last_index(f, a)` | `arrayLastIndex(f, a)` |

> `length`/`indexOf` нативно возвращают `UInt64`, поэтому диалект оборачивает их в `toInt64(...)`.
> Функции, возвращающие массив (`split_by_char`, `array_sort`, `array_reverse`, `array_distinct`,
> `range`, `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`, `group_array`,
> `group_uniq_array`), можно проецировать напрямую — row reader материализует результат `Array(T)`
> как CLR `T[]` — либо использовать как операнд другой array-функции (например, `length(...)` или
> `array_string_concat(...)`). Тот же reader материализует нативную колонку `Tuple(...)` (или
> выражение типа `Tuple(...)`) как `System.Tuple<...>` арности 1–7.

> Предикаты отношения массивов возвращают `bool`: `starts_with(array, prefix)`/`ends_with(array, suffix)`
> проверяют префикс/суффикс, а `has_substr(array, other)` — что `other` входит в `array` непрерывно и
> по порядку (пустой `other` содержится всегда). Требуется провайдер, поддерживающий array-функции.

> Поверхность row-значений (кортежей) кросс-провайдерная и строится на `System.Tuple.Create` /
> `new Tuple<...>` / `System.Tuple<...>.ItemN`: конструктор рендерится через row-конструктор диалекта —
> `tuple(a, b)` в ClickHouse, `ROW(a, b)` в PostgreSQL — а доступ к элементу *серверного* row рендерится
> через позиционный доступ диалекта (`tupleElement(pair, 1)` в ClickHouse, `(pair).f1` в PostgreSQL).
> Доступ к элементу *inline*-конструктора (`Tuple.Create(a, b).Item1`,
> `new ValueTuple<...>(a, b).Item2`) сворачивается в сам аргумент, поэтому работает на любом диалекте,
> умеющем выразить конструктор. `System.Tuple<,> ==` (в C# — ссылочное равенство) переинтерпретируется
> как SQL-сравнение row-значений (`Tuple.Create(x.A, x.B) == Tuple.Create(1, 'a')` →
> `ROW(a, b) = ROW(1, 'a')`); `ValueTuple` `==` в дереве выражений недостижим. Требуется провайдер с
> нативным row-типом (см. [`ITupleRenderer`](xref:NextORM.Core.ITupleRenderer) /
> [`ISqlDialect.Tuple`](xref:NextORM.Core.ISqlDialect.Tuple); PostgreSQL и ClickHouse); SQL Server,
> MySQL, MariaDB, SQLite и провайдер in-memory отклоняют эту поверхность, а tuple `IN`/`Contains` по
> списку значений пока не транслируется. `untuple` не поддерживается, так как меняет набор колонок
> результата, а не даёт скаляр.

> Функции высшего порядка (lambda) принимают inline-лямбду C#, параметр которой — элемент массива;
> например `array_map(v => -v, e.Nums)` рендерится как `arrayMap(v -> -(v), nums)`.
> `array_exists`/`array_all` возвращают `bool`; `array_count`/`array_first_index`/`array_last_index` —
> `long` (диалект оборачивает нативный `UInt32` в `toInt64(...)`); `array_first`/`array_last`
> возвращают элемент или его значение по умолчанию при отсутствии совпадения. Требуется провайдер,
> поддерживающий higher-order array-функции (см.
> [`SupportsHigherOrderArrayFunctions`](xref:NextORM.Core.ISqlDialect.SupportsHigherOrderArrayFunctions);
> ClickHouse). ClickHouse повышает тип арифметического результата независимо от C# (элемент `Int32`,
> умноженный на целочисленный литерал, даёт `Array(Int64)`), поэтому приводите тип внутри лямбды
> (`v => (long)v * 2`), если тип элемента должен совпадать с проецируемым `T[]`.

CLR-метод `string.Split` рендерится как `splitByChar(separator, value)` (гейт
[`StringSplit`](xref:NextORM.Core.ISqlDialect.StringSplit)); поддерживается только
одноразрядный разделитель (многосимвольный `splitByString` не выставлен), результат — `string[]`,
который можно проецировать напрямую или использовать внутри другой array-функции; overload с `count`, несколько разделителей и
`StringSplitOptions`, отличный от `None`, бросают `NotSupportedException`:

```csharp
var parts = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.ClickHouse.length(e.String!.Split(',')))
    .First();
```

```csharp
var tags = dataContext.From<IArrayEntity>()
    .Where(e => e.Id == 1)
    .Select(e => new { e.Id, Tag = SqlFunctions.ClickHouse.array_join(e.Tags) })
    .ToList();
```

```sql
select id, arrayJoin(tags) as `Tag` from array_entity where id = 1
```

`EntityBuilder.ArrayJoin`/`LeftArrayJoin` рендерят клаузу `[LEFT] ARRAY JOIN`, которая разворачивает
строки до `WHERE`/`GROUP BY`; `LEFT ARRAY JOIN` сохраняет строку с пустым массивом. Вырожденный элемент
не привязан к CLR-члену, поэтому для проецирования/фильтрации используйте скалярный `array_join` выше.

```csharp
var ids = dataContext.From<IArrayEntity>()
    .LeftArrayJoin(e => e.Tags)
    .Select(e => e.Id)
    .ToList();
```

```sql
select id from array_entity left array join tags
```

`EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement` добавляют ту же клаузу, но возвращают
`EntityBuilder<ArrayJoinProjection<TEntity, TElement>>`, поэтому доступны и исходная сущность
(`p.Item1`), и вырожденный элемент (`p.Element`). Выражение клаузы получает алиас, и `p.Element`
транслируется в этот алиас:

```csharp
var rows = dataContext.From<IArrayEntity>()
    .ArrayJoinElement(e => e.Tags)
    .Where(p => p.Element == "b")
    .Select(p => new { p.Item1.Id, Tag = p.Element })
    .ToList();
```

```sql
select id, __nextorm_aj_element as `Tag` from array_entity
array join tags as __nextorm_aj_element
where __nextorm_aj_element = 'b'
```

Привязка элемента поддерживается только для одного источника без join'ов; `Where`/`Having` нужно
применять после неё (их параметр — проекция array join). Если нужны несколько массивов или join —
используйте `ArrayJoin`/`LeftArrayJoin` со скалярным `array_join`.

## JSON и JSONB (PostgreSQL)

PostgreSQL — единственный поддерживаемый провайдер с типами `json`/`jsonb`. Операнд JSON должен быть
выражением `json`/`jsonb`: колонка, другая JSON-функция или параметр, runtime-значение которого —
`JsonDocument`, `JsonElement` или `JsonNode` (Npgsql привязывает их как `jsonb`). Обычная строка с JSON
привязывается как `text`; её можно разобрать явно через `SqlFunctions.Postgres.json_cast(value)`
(`cast(value as jsonb)`).

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), "name") == "Alice")
    .Select(e => new { e.Id })
    .ToList(document);
```

```sql
select id from complex_entity where ((@norm_p0 ->> 'name') = 'Alice')
```

Агрегаты сворачивают набор строк в один JSON-документ:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(e.String))
    .First();

var person = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String))
    .First();
```

```sql
select jsonb_agg(somestring) from complex_entity
select jsonb_build_object('id', id, 'name', somestring) from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_agg(x)` / `jsonb_agg(x)` | `json_agg(x)` / `jsonb_agg(x)` |
| `SqlFunctions.Postgres.json_object_agg(k, v)` / `jsonb_object_agg(k, v)` | `json_object_agg(k, v)` / `jsonb_object_agg(k, v)` |
| `SqlFunctions.Postgres.json_build_object("a", x, ...)` | `json_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.json_build_array(x, y)` / `jsonb_build_array(x, y)` | `json_build_array(x, y)` / `jsonb_build_array(x, y)` |
| `SqlFunctions.Postgres.json_array(x, y)` / `jsonb_array(x, y)` | `json_array(x, y)` / `json_array(x, y returning jsonb)` |
| `SqlFunctions.Postgres.to_json(x)` / `to_jsonb(x)` | `to_json(x)` / `to_jsonb(x)` |
| `SqlFunctions.Postgres.json_cast(x)` | `cast(x as jsonb)` |
| `SqlFunctions.Postgres.json_get(json, "key")` / `json_get(json, 0)` | `json -> key` / `json -> 0` |
| `SqlFunctions.Postgres.json_get_text(json, "key")` / `json_get_text(json, 0)` | `json ->> key` / `json ->> 0` |
| `SqlFunctions.Postgres.json_get_path(json, path)` / `json_get_path_text(json, path)` | `json #> path` / `json #>> path` |
| `SqlFunctions.Postgres.json_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.json_exists(json, "key")` | `json ? 'key'` |
| `SqlFunctions.Postgres.json_exists_any(json, keys)` / `json_exists_all(json, keys)` | `json ?\| keys` / `json ?& keys` |
| `SqlFunctions.Postgres.json_array_length(json)` / `jsonb_array_length(json)` | `json_array_length(json)` / `jsonb_array_length(json)` |
| `SqlFunctions.Postgres.json_typeof(json)` / `jsonb_typeof(json)` | `json_typeof(json)` / `jsonb_typeof(json)` |
| `SqlFunctions.Postgres.jsonb_set(json, path, value[, create])` | `jsonb_set(...)` |
| `SqlFunctions.Postgres.jsonb_insert(json, path, value[, after])` | `jsonb_insert(...)` |
| `SqlFunctions.Postgres.jsonb_strip_nulls(json)` | `jsonb_strip_nulls(json)` |
| `SqlFunctions.Postgres.jsonb_pretty(json)` | `jsonb_pretty(json)` |
| `SqlFunctions.Postgres.jsonb_delete(json, "key")` / `jsonb_delete(json, 0)` | `json - 'key'` / `json - 0` |
| `SqlFunctions.Postgres.json_concat(a, b)` | `a \|\| b` |
| `SqlFunctions.Postgres.row_to_json(row)` | `row_to_json(row)` |
| `SqlFunctions.Postgres.array_to_json(array)` | `array_to_json(array)` |
| `SqlFunctions.Postgres.jsonb_path_exists(json, path)` | `jsonb_path_exists(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_match(json, path)` | `jsonb_path_match(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_query_first(json, path)` | `jsonb_path_query_first(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_query_array(json, path)` | `jsonb_path_query_array(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.json_value(json, path)` / `json_query(json, path)` | `json_value(json, cast(path as jsonpath))` / `json_query(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.json_exists(json, path, fromJsonPath)` | `json_exists(json, cast(path as jsonpath))` |

Операнд `path`/`keys` — это `string[]`, привязываемый как **один параметр-массив** (см.
[Массивы](#массивы-postgresql)), поэтому `SqlFunctions.Postgres.json_get_path(json, new[] { "a", "b" })` отрисует
`json #> @p0`. Функции JSONPath принимают путь как обычную строку и отрисовывают его как
`cast(<path> as jsonpath)`.

## JSON как текст (SQL Server, MySQL/MariaDB)

SQL Server хранит JSON в обычной колонке `nvarchar` и предоставляет текстовое подмножество функций
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)); MySQL/MariaDB предоставляют ту же
поверхность через семейство `JSON_EXTRACT`/`JSON_SET`. Путь — это строка JSONPath (`'$.name'`); `json_value` возвращает
скаляр, а `json_query` — фрагмент-объект/массив:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Id = SqlFunctions.SqlServer.json_value(e.String, "$.id"),
        Name = SqlFunctions.SqlServer.json_query(e.String, "$.name"),
        Updated = SqlFunctions.SqlServer.json_modify(e.String, "$.id", "1")
    })
    .ToList();
```

```sql
select json_value(somestring, '$.id') as [Id], json_query(somestring, '$.name') as [Name], json_modify(somestring, '$.id', '1') as [Updated] from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.json_value(json, path)` | `json_value(json, path)` |
| `SqlFunctions.SqlServer.json_query(json, path)` | `json_query(json, path)` |
| `SqlFunctions.SqlServer.json_modify(json, path, value)` | `json_modify(json, path, value)` |
| `SqlFunctions.SqlServer.isjson(value)` | `isjson(value)` |

В MySQL/MariaDB те же вызовы рендерятся как `json_unquote(json_extract(...))`, `json_extract(...)`,
`json_set(...)` и `json_valid(...)`.

`SqlFunctions.SqlServer.isjson` возвращает логическое значение: в предикате он отрисовывается как `(isjson(x)) = 1`
(T-SQL `ISJSON` возвращает `int`), а при проецировании как значение приводится к `bit`. `isjson`
можно также использовать прямо в `WHERE` (`Where(e => SqlFunctions.SqlServer.isjson(e.String))`).

## Методы типа XML (SQL Server)

Тип `xml` SQL Server предоставляет постфиксные методы
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)). Они
вызываются через `SqlFunctions.SqlServer` и рендерятся как `xmlcol.method(...)`; XQuery и SQL-тип
обязаны быть строковыми литералами (оба эмитятся дословно, одиночные кавычки экранируются):

```csharp
var rows = dataContext.From<IXmlEntity>()
    .Select(x => new
    {
        Value = SqlFunctions.SqlServer.xml_value<string>(x.Payload, "(/root/item)[1]", "nvarchar(100)"),
        Fragment = SqlFunctions.SqlServer.xml_query(x.Payload, "/root/item[1]"),
        Exists = SqlFunctions.SqlServer.xml_exist(x.Payload, "/root/item[2]")
    })
    .ToList();
```

```sql
select payload.value('(/root/item)[1]', 'nvarchar(100)') as [Value], payload.query('/root/item[1]') as [Fragment], payload.exist('/root/item[2]') as [Exists] from xml_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.xml_value<T>(xml, xpath, sqlType)` | `xml.value('xpath', 'sqlType')` |
| `SqlFunctions.SqlServer.xml_query(xml, xpath)` | `xml.query('xpath')` |
| `SqlFunctions.SqlServer.xml_exist(xml, xpath)` | `xml.exist('xpath')` |
| `SqlFunctions.SqlServer.xml_nodes(xml, xpath)` | `xml.nodes('xpath') as [alias]([value])` (источник APPLY) |

`xml_exist` возвращает `bit`: в предикате рендерится как `(xml.exist('xpath')) = 1`, при проецировании
остаётся bit.

Строковый метод `.nodes` — это коррелированный источник, а не скаляр: используйте его как источник
`CrossApply`/`OuterApply` и проецируйте развёрнутый `IXmlNodesRow.Value` скалярными методами выше. Он
разворачивает XML-значение в строки — по одной на узел, выбранный XQuery, — и рендерит
`<xml>.nodes('xpath') as [alias]([value])`:

```csharp
var rows = dataContext.From<IXmlEntity>()
    .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.Payload, "/root/item"))
    .Select(p => new
    {
        Id = SqlFunctions.SqlServer.xml_value<int>(p.Item2.Value, "(.)[1]/@id", "int"),
        Text = SqlFunctions.SqlServer.xml_value<string>(p.Item2.Value, "(.)[1]", "nvarchar(100)")
    })
    .ToList();
```

```sql
select t2.value.value('(.)[1]/@id', 'int') as [Id], t2.value.value('(.)[1]', 'nvarchar(100)') as [Text]
from xml_entity as [t1] cross apply t1.payload.nodes('/root/item') as [t2](value)
```

Операнд обязан быть колонкой внешней строки, а XQuery — строковым литералом; все прочие провайдеры
отвергают `xml_nodes` с `NotSupportedException`, как и in-memory-провайдер.

## Условные функции

`SqlFunctions.Sql.nullif` — ANSI и работает на всех SQL-провайдерах; `greatest`/`least` включаются флагом
[`SupportsGreatestLeast`](xref:NextORM.Core.ISqlDialect.SupportsGreatestLeast) (его включают PostgreSQL, MySQL/MariaDB, ClickHouse, SQL Server 2022+ и SQLite; SQLite рендерит `max`/`min`). Обработка NULL зависит от провайдера: PostgreSQL, SQL Server 2022+ и ClickHouse 24.12+ игнорируют NULL-аргументы и возвращают NULL, только если все аргументы NULL, тогда как MySQL/MariaDB и SQLite возвращают NULL, если хотя бы один аргумент NULL:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        NoZero = SqlFunctions.Sql.nullif(e.Int, 0),
        Hi = SqlFunctions.Sql.greatest(e.Id, 10L),
        Lo = SqlFunctions.Sql.least(e.Id, 10L)
    })
    .ToList();
```

```sql
select nullif(nullableint, 0) as "NoZero", greatest(id, 10) as "Hi", least(id, 10) as "Lo" from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Sql.nullif(a, b)` | `nullif(a, b)` |
| `SqlFunctions.Sql.greatest(a, b, ...)` | `greatest(a, b, ...)` |
| `SqlFunctions.Sql.least(a, b, ...)` | `least(a, b, ...)` |
| `SqlFunctions.Postgres.num_nulls(a, b, ...)` | `num_nulls(a, b, ...)` |
| `SqlFunctions.Sql.iif(condition, a, b)` | `iif(...)` (SQL Server, SQLite 3.32+), `if(...)` (MySQL/MariaDB, ClickHouse), `case when ... then ... else ... end` (PostgreSQL) |
| `SqlFunctions.SqlServer.choose(index, a, b, ...)` | `choose(index, a, b, ...)` (SQL Server) |
| `SqlFunctions.Postgres.num_nonnulls(a, b, ...)` | `num_nonnulls(a, b, ...)` |
| `SqlFunctions.ClickHouse.multi_if(when(c1, v1), ..., otherwise(v))` | `multiIf(c1, v1, ..., v)` (ClickHouse) |

`num_nulls`/`num_nonnulls` входят в расширенную библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions)).
`iif` переносим ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif)), и каждый диалект задаёт своё
нативное написание через [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String)); `choose` остаётся только для
SQL Server ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)). Вызов `iif` через
специализированную поверхность `SqlFunctions.SqlServer` по-прежнему работает по наследованию.
C#-тернарник `condition ? a : b` отдельный и всегда рендерит переносимый `case when ... end`.

Помимо этого ClickHouse предоставляет многоветвевную поверхность `multiIf`
([`MultiIf`](xref:NextORM.Core.ISqlDialect.MultiIf),
[`IMultiIfRenderer.Render`](xref:NextORM.Core.IMultiIfRenderer.Render(System.Collections.Generic.IReadOnlyList{System.String},System.Type))): каждая ветвь собирается через
`when(condition, value)`, а завершает вызов `otherwise(value)` (обязательно последним). Остальные
провайдеры используют `case when` — это уже переносимая форма за `iif`/C#-тернарником, поэтому
нативное написание ClickHouse они отвергают.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        Bucket = SqlFunctions.ClickHouse.multi_if(
            SqlFunctions.ClickHouse.when(e.Id == 1L, "one"),
            SqlFunctions.ClickHouse.when(e.Id == 2L, "two"),
            SqlFunctions.ClickHouse.otherwise("many"))
    })
    .ToList();
```

```sql
-- ClickHouse
select id, multiIf((id = 1), 'one', (id = 2), 'two', 'many') as `Bucket` from complex_entity
```

## Усечение даты (PostgreSQL, SQL Server, ClickHouse)

`SqlFunctions.Sql.date_trunc(field, value)` усекает отметку времени до части даты
([`SupportsDateTrunc`](xref:NextORM.Core.ISqlDialect.SupportsDateTrunc); его включают PostgreSQL, SQL Server 2022+ и ClickHouse). Поле должно
быть константной строкой из поддерживаемого набора. SQL Server отрисовывает `datetrunc(part, value)`,
сворачивая множественные ANSI-части в единственные T-SQL-написания (`milliseconds` → `millisecond`)
и отклоняя `decade`/`century`/`millennium`; ClickHouse отрисовывает `dateTrunc('part', value)` с тем
же сворачиванием и тем же отклонением трёх крупных частей:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new { Month = SqlFunctions.Sql.date_trunc("month", e.Datetime) })
    .ToList();
```

```sql
-- PostgreSQL / SQL Server 2022+
select date_trunc('month', dt) as "Month" from complex_entity
-- ClickHouse
select dateTrunc('month', dt) as `Month` from complex_entity
```

## Арифметика дат

`SqlFunctions.Sql.date_add(field, amount, value)` прибавляет к дате/времени заданное число единиц, а
`SqlFunctions.Sql.end_of_month(value)` возвращает последний день месяца ([`SupportsDateArithmetic`](xref:NextORM.Core.ISqlDialect.SupportsDateArithmetic);
его включают PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB и SQLite). Поле должно быть константной
строкой; диалект проверяет, какие части он принимает ([`SupportsDateAddField`](xref:NextORM.Core.ISqlDialect.SupportsDateAddField(System.String)) и др.).
SQL Server отрисовывает `dateadd(field, amount, value)` и `eomonth(value)`, сворачивая
`decade`/`century`/`millennium` в масштабированное прибавление `year`; PostgreSQL отрисовывает
интервальную арифметику; ClickHouse отрисовывает выделенные функции
`addDays`/`addMonths`/…/`addSeconds` (сворачивая три крупные части в масштабированный `addYears`) и
`toLastDayOfMonth(value)`; MySQL/MariaDB отрисовывают `date_add(value, interval n unit)` и
`last_day(value)`; SQLite настраивает дату через строку-модификатор `datetime`/`strftime`.
`SqlFunctions.Sql.date_diff(field, start, end)` возвращает число границ `<field>` между двумя отметками
времени (SQL Server `datediff`, ClickHouse `dateDiff`, MySQL/MariaDB `timestampdiff`); резервные
реализации PostgreSQL и SQLite считают части даты границами, а части времени — целыми единицами.
`SqlFunctions.Sql.date_diff_big(field, start, end)` — 64-битный вариант (SQL Server `datediff_big`;
остальные расширяют результат) для промежутка в `millisecond`/`microsecond`, который переполнил бы
32-битный `date_diff`. Sub-day `date_add`/`DateTime.Add*` продвигает `date`-операнд к дробному
timestamp (SQL Server `datetime2`, ClickHouse `DateTime`/`DateTime64`), чтобы время суток не терялось.
`SqlFunctions.Sql.date_from_parts(year, month, day)` строит дату. `DateTime.AddDays`/`AddMonths`/… внутри
проекции или предиката идут через тот же хук:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        NextDay = SqlFunctions.Sql.date_add("day", 1, e.Datetime),
        MonthEnd = SqlFunctions.Sql.end_of_month(e.Datetime)
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
| `SqlFunctions.Sql.date_add("day", n, x)` | `dateadd(day, n, x)` | `x + (n * interval '1 day')` | `addDays(x, n)` | `date_add(x, interval n day)` | `datetime(x, (n) \|\| ' days')` |
| `SqlFunctions.Sql.date_add("decade", n, x)` | `dateadd(year, (n) * 10, x)` | `x + (n * interval '10 years')` | `addYears(x, (n) * 10)` | `date_add(x, interval (n) * 10 year)` | `datetime(x, ((n) * 10) \|\| ' years')` |
| `SqlFunctions.Sql.end_of_month(x)` | `eomonth(x)` | `date_trunc('month', x) + interval '1 month - 1 day'` | `toLastDayOfMonth(x)` | `last_day(x)` | `date(x, 'start of month', '+1 month', '-1 day')` |
| `SqlFunctions.Sql.date_diff("day", a, b)` | `datediff(day, a, b)` | `cast(b as date) - cast(a as date)` | `dateDiff('day', a, b)` | `timestampdiff(day, a, b)` | `(strftime('%s', b) - strftime('%s', a)) / 86400` |
| `SqlFunctions.Sql.date_diff_big("milliseconds", a, b)` | `datediff_big(millisecond, a, b)` | `cast(trunc(extract(epoch from (b - a)) * 1000) as bigint)` | `dateDiff('millisecond', a, b)` | `cast((timestampdiff(microsecond, a, b) / 1000) as signed)` | `((strftime('%s', b) - strftime('%s', a)) * 1000)` |
| `SqlFunctions.Sql.date_from_parts(y, m, d)` | `datefromparts(y, m, d)` | `make_date(y, m, d)` | `makeDate(y, m, d)` | `str_to_date(concat_ws('-', y, m, d), '%Y-%m-%d')` | `date(printf('%04d-%02d-%02d', y, m, d))` |
| `x.AddDays(7)` | `dateadd(day, 7, x)` | `x + (7 * interval '1 day')` | `addDays(x, 7)` | `date_add(x, interval 7 day)` | `datetime(x, (7) \|\| ' days')` |
| `x.AddMonths(2)` | `dateadd(month, 2, x)` | `x + (2 * interval '1 month')` | `addMonths(x, 2)` | `date_add(x, interval 2 month)` | `datetime(x, (2) \|\| ' months')` |

## Приведение и части даты (ClickHouse)

ClickHouse предоставляет свои `to*`-функции даты/времени через `SqlFunctions.ClickHouse`
([`DateConversion`](xref:NextORM.Core.ISqlDialect.DateConversion); только ClickHouse).
`to_date`/`to_date_time`/`to_date32` приводят к `Date`/`DateTime`/`Date32`;
`to_year`/`to_quarter`/`to_month`/`to_day_of_month`/`to_day_of_week`/`to_day_of_year`/`to_hour`/
`to_minute`/`to_second` возвращают части даты (`toDayOfWeek` — понедельник 1 … воскресенье 7);
`to_start_of_year`/`_quarter`/`_month`/`_week`/`_day`/`_hour`/`_minute`/`_second` усекают до начала
периода, а `to_monday` возвращает ISO-понедельник недели (`toStartOfWeek` начинает неделю с
воскресенья); `to_yyyymm`/`to_yyyymmdd` упаковывают дату в целое, `to_unix_timestamp` возвращает
секунды Unix. Проекции `DateTime.Year`/`Month`/`Day`/`Hour`/… в ClickHouse используют те же
`to`-аксессоры. Аксессоры, возвращающие целое, а также `toYYYYMM`/`toYYYYMMDD`/`toUnixTimestamp`
оборачиваются в `toInt32`/`toInt64`, чтобы построитель строк мог их прочитать. На любом другом
провайдере вся поверхность бросает `NotSupportedException`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Year = SqlFunctions.ClickHouse.to_year(e.Datetime),
        MonthStart = SqlFunctions.ClickHouse.to_start_of_month(e.Datetime)
    })
    .ToList();
```

```sql
select toInt32(toYear(dt)) as `Year`, toStartOfMonth(dt) as `MonthStart` from complex_entity
```

## Строковые и массивные агрегаты

`SqlFunctions.Sql.string_agg` доступен в PostgreSQL, SQL Server 2017+, ClickHouse, MySQL/MariaDB и SQLite
([`SupportsStringAgg`](xref:NextORM.Core.ISqlDialect.SupportsStringAgg), по умолчанию берёт значение зонтичного
[`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates)); в ClickHouse он рендерится как
`arrayStringConcat(groupArray(x), delimiter)`, в MySQL/MariaDB — как
`group_concat(x separator delimiter)`, в SQLite — как `group_concat(x, delimiter)`.
`SqlFunctions.Postgres.array_agg` ([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg)) требует типа-массива, поэтому доступен только в
PostgreSQL. Результат `array_agg` — колонка-массив:

```csharp
var names = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Sql.string_agg(e.String, ","))
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
| `SqlFunctions.Sql.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` / `group_concat(x separator delimiter)` / `group_concat(x, delimiter)` | PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB, SQLite |
| `SqlFunctions.Postgres.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## Фильтр агрегатов (FILTER)

`count`/`count_big`/`min`/`max`/`avg`/`sum` и строковые/массивные агрегаты принимают дополнительный
аргумент `Expression<Func<bool>>`, который фильтрует строки, видимые агрегату. Предикат фильтра —
обычный предикат запроса и может ссылаться на колонки и параметры. Способ записи выбирает
[`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle): PostgreSQL и SQLite
генерируют ANSI-предложение `filter (where ...)`, ClickHouse — комбинатор `-If` (`countIf`/`sumIf`/...),
а MySQL/MariaDB и SQL Server отклоняют вызов:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Big = SqlFunctions.Sql.count(() => e.Id > 10L),
        Total = SqlFunctions.Sql.sum(e.Id, () => e.Boolean == true)
    })
    .ToList();
```

```sql
select nullableint, count(*) filter (where (id > 10)) as "Big", sum(id) filter (where (b = true)) as "Total"
from complex_entity group by nullableint
```

В ClickHouse тот же запрос генерирует `toInt32(countIf((id > 10)))` и `sumIf(id, (b = true))`.

## Функции, возвращающие наборы (PostgreSQL)

`SqlFunctions.Postgres.generate_series` и `SqlFunctions.Postgres.unnest` — это предобъявленные источники
[`[SqlTableFunction]`](13-table-valued-functions.md), поэтому отдельная обёртка не нужна:

```csharp
var numbers = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`generate_series` проецируется на [`Value`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow.Value), а `unnest` — на
[`Value`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1.Value); обе соответствуют единственной колонке, которую возвращает функция.

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
| `string.Format` / `ToString(format)` | `printf` / `strftime` | `format` | `to_char` |
| Ordinal `Equals` / `CompareOrdinal` | `collate binary` | `collate Latin1_General_100_BIN2` | `collate "C"` |
| `collate(s, name)` | `s collate name` | `s collate name` | `s collate "name"` |
| Столбец с `[Collation]` | `s collate name` | `s collate name` | `s collate "name"` |
| Regex (`IsMatch` / `Replace`) | `s regexp ...` / `regexp_replace(...)` | `NotSupportedException` | `s ~ ...` / `regexp_replace(...)` |
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
| `greatest` / `least` | `max(...)` / `min(...)` (один аргумент -> `(...)`) | поддерживается (2022+) | поддерживается |
| `iif` | `iif(cond, a, b)` (3.32+) | `iif(cond, a, b)` | `case when cond then a else b end` |
| `multi_if` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (только ClickHouse; `multiIf`) |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | поддерживается |
| `date_add` / `end_of_month` / `date_diff` / `date_from_parts` | `datetime(x, n \|\| ' days')` / `date(x, 'start of month', ...)` / разность `strftime` / `date(printf(...))` | `dateadd(...)` / `eomonth(...)` / `datediff(...)` / `datefromparts(...)` | интервальная арифметика / `date_trunc` / разность частей даты / `make_date` |
| `string_agg` / `array_agg` | `group_concat(x, delimiter)` (нет `array_agg`) | `string_agg` (2017+); `array_agg` бросает исключение | поддерживается |
| `filter (where ...)` у агрегатов | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Расширенная библиотека скалярных функций (`asin`, `split_part`, `regexp_*`, `to_char`, ...) | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Логические/битовые/статистические агрегаты | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Упорядоченные агрегаты (`percentile_cont`, ...) | `NotSupportedException` | оконный `percentile_cont(f) within group (order by x) over (...)` | `within group (order by ...)` |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Встроенные табличные функции | `NotSupportedException` | `string_split(...)`, `openjson(...)` | `generate_series(...)`, `unnest(...)` |

Провайдер in-memory не рендерит SQL: он компилирует и вычисляет выражение для строк в памяти, поэтому
выполняется сам метод .NET. Приведённая выше матрица SQL относится к провайдерам SQLite, SQL Server и
PostgreSQL.

ClickHouse рендерит `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (и масштабированный
`addYears` для `decade`/`century`/`millennium`), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax`, комбинаторы `-If` и `multiIf`. Он отклоняет ANSI-предложение
`filter (where ...)`, агрегаты `regr_*` и логические агрегаты через `NotSupportedException`; см.
[Провайдер ClickHouse](../providers/clickhouse.md).

## Явно не поддерживается

Эти случаи бросают `NotSupportedException`, а не генерируют SQL с другой семантикой:

* `string.IsNullOrWhiteSpace(x)` — бросает исключение с сообщением, упоминающим `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` — у двухаргументной формы порядок аргументов зависит от провайдера, поэтому она
  оставлена неподдерживаемой (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:985`). Двухаргументная форма PostgreSQL доступна как
  `SqlFunctions.Postgres.log(base, x)` в расширенной библиотеке скалярных функций.
* Перегрузки `Math.Round`, принимающие `MidpointRounding` (больше двух аргументов), — не переносимы.
* `string.Substring(Range)` — нет эквивалента в SQL.
* `string.Trim(c)`/`TrimStart(c)`/`TrimEnd(c)` — SQL обрезает только пробельные символы, не
  произвольный набор символов.
* `string.ToUpper(CultureInfo)`/`ToLower(CultureInfo)` с культурой, отличной от
  `CultureInfo.InvariantCulture` — переносимую форму имеет только инвариантный upper/lower.
* `string.Compare(a, b)` и `string.Compare(a, b, bool)` без `StringComparison` — culture-sensitive;
  используйте `string.Compare(a, b, StringComparison.Ordinal)` или `CompareOrdinal`.
* `StringComparison.InvariantCulture`/`CurrentCulture` (с `IgnoreCase` или без) — нет переносимой формы.
* Спецификаторы `string.Format`/`ToString(format)` вне документированного подмножества в
  [Форматировании дат и чисел в строки](#форматирование-дат-и-чисел-в-строки) — никогда не
  отбрасываются.
* `Regex` в SQL Server — движка регулярных выражений нет; используйте `SqlFunctions.Sql.like` для
  простых шаблонов (см. [Регулярные выражения](#регулярные-выражения)).
* `Regex`-шаблон, замена или `RegexOptions`, не являющиеся константой времени компиляции; опция
  `RegexOptions`, отличная от `IgnoreCase` (при этом `Compiled`/`CultureInvariant` — no-op); прочие
  члены `Regex`, кроме `IsMatch`/`Replace` (например, `Match`, `Split`, группы захвата).

## См. также

* [Фильтрация (WHERE)](02-filtering-where.md) — `Contains`/`in`, `??` и условные выражения в предикатах.
* [Группировка и агрегаты](04-grouping-and-aggregates.md) — агрегатные функции (`count`, `sum`, ...).
* [Пользовательские функции](12-user-defined-functions.md) — когда скалярная функция не встроена.
* [Обзор провайдеров](../providers/overview.md) — флаги возможностей и кавычки.

---

Source: `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:541`, `:1157`, `:1204`, `:1752`;
`src/nextorm.core/Visitors/BuiltinFunctionTranslator.cs`, `src/nextorm.core/Visitors/AggregateFilter.cs`;
`src/nextorm.core/Query/SqlFunctions.cs`;
`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:57`;
`tests/nextorm.integration.tests/CommonTestSuite.Functions.cs:8`, `:19`, `:43`, `:54`, `:65`, `:76`, `:87`, `:98`, `:117`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:695`, `:775`, `:801`, `:811`, `:832`, `:852`, `:861`, `:872`, `:882`, `:892`, `:912`, `:921`, `:931`, `:940`, `:950`, `:960`, `:974`, `:985`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:457`, `:512`, `:522`, `:533`, `:543`, `:552`, `:563`, `:573`, `:583`, `:593`, `:602`, `:613`, `:624`, `:633`, `:643`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:390`, `:445`, `:465`, `:475`, `:484`, `:495`, `:505`, `:515`, `:525`, `:534`, `:544`, `:554`, `:563`, `:573`.
