# Строковые функции

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

## Ordinal-сравнение и коллация

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

### Коллация на уровне столбца

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

## Регулярные выражения

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
| SQL Server | `regexp_like(s, 'p', 'c')` | `regexp_replace(s, 'p', 'r', 1, 0, 'c')` | флаг `'i'` |

Как и метод CLR, совпадение ищется в любом месте значения, поэтому `^`/`$` привязывают всё значение, а
шаблон не анкорится неявно. Шаблон исполняет **собственный** движок провайдера (POSIX/ARE в PostgreSQL,
ICU в MySQL, PCRE в MariaDB, RE2 в ClickHouse и SQL Server 2025, .NET в SQLite), поэтому сложный шаблон —
lookaround, обратные ссылки, именованные группы — в общем случае непереносим; также различаются правила
экранирования C# и SQL и синтаксис ссылок на группы в замене (C# `$1` против SQL `\1`). SQLite
сопоставляет через CLR-функции `regexp`/`regexp_replace`, регистрируемые на каждом соединении, поэтому
сохраняет синтаксис .NET. В SQL Server функции `REGEXP_*` есть **только с SQL Server 2025**: совпадение
рендерится как `REGEXP_LIKE` (ему дополнительно нужен уровень совместимости БД 170), а замена — как
`REGEXP_REPLACE` (доступна на любом уровне совместимости); в SQL Server 2022 и раньше нет ни одной из
них, поэтому там для простых шаблонов остаётся
[`SqlFunctions.Sql.like`](xref:NextORM.Core.CommonFunctions.like(System.String,System.String)). In-memory
провайдер выполняет `System.Text.RegularExpressions` нативно.

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
| `SqlFunctions.Sql.octet_length(s)` | `octet_length` | `DATALENGTH` | `OCTET_LENGTH` | `length` | `octet_length` |
| `SqlFunctions.Sql.bit_length(s)` | `bit_length` | `DATALENGTH(s) * 8` | `BIT_LENGTH` | `length(s) * 8` | `octet_length(s) * 8` |

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
- `octet_length`/`bit_length` считают октеты значения, поэтому многобайтовый `nvarchar` в SQL Server
  даёт два байта на символ (через `DATALENGTH`); SQLite требует 3.43+ для нативного `octet_length()`.

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
