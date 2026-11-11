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
select id
 from complex_entity where somestring like '%a%'
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
select id
 from complex_entity where somestring like '%'||$needle||'%'
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
select id
 from complex_entity where somestring collate "C" = 'x' collate "C"
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
select somestring collate "C" as "String"
 from complex_entity
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

### SQL Server 2025: `regexp_count`

`SqlFunctions.SqlServer.regexp_count(input, pattern)` возвращает число неперекрывающихся совпадений
`pattern` в `input` как `int?` — `0`, если совпадений нет, и SQL `NULL`, если вход `NULL`:

```csharp
var counts = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Matches = SqlFunctions.SqlServer.regexp_count(e.String, "[0-9]"),
        NoMatch = SqlFunctions.SqlServer.regexp_count("abc", "[0-9]"),
        NullInput = SqlFunctions.SqlServer.regexp_count(null, "[0-9]")
    })
    .ToList();
// Matches = 3 для "a1b2c3", NoMatch = 0, NullInput = null
```

```sql
select regexp_count(somestring, N'[0-9]'), regexp_count(N'abc', N'[0-9]'),
       regexp_count(CAST(NULL AS nvarchar(4000)), N'[0-9]')
 from complex_entity
```

Функция доступна **только в SQL Server 2025 (мажорная версия 17)** и закрыта версионным флагом
[`SupportsSqlServer2025Functions`](xref:NextORM.Core.ISqlDialect.SupportsSqlServer2025Functions). В
отличие от JSON-гейта, объявление edition никогда не подменяет объявленную мажорную версию: контекст,
объявленный для 16, неверсионированный контекст или объявление только edition отклоняет вызов **до SQL**
с `The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: 16.`
(или `… unspecified.` для неустановленной версии). Уровень совместимости БД не требуется — функция
работает на compat 160 на движке мажорной версии 17.

### SQL Server 2025: regex-скаляры и `regexp_matches`

`regexp_substr` возвращает первую совпавшую подстроку, а `regexp_instr` — one-based позицию
совпадения (`0`, если совпадений нет); обе принимают необязательные нативные аргументы `start`,
`occurrence`, `return_option` (только `regexp_instr`) и `match_type` (`c`/`i`/`s`/`m`). Опущенный
необязательный аргумент и переданный SQL `NULL` — разные формы вызова: переданный `NULL` для start,
occurrence или match type даёт SQL `NULL`, а переданный `NULL` для return option ведёт себя как `0`.
Некорректные шаблоны, флаги и позиции — нативные ошибки движка (Msg 19308/19303/19301); nextorm
никогда не проверяет их на клиенте. Нативный match type — аргумент `varchar`: литерал или параметр
`nvarchar` даёт Msg 8116, поэтому рендерер выдаёт его как `cast(… as varchar(4000))`.

```csharp
var matches = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Sub = SqlFunctions.SqlServer.regexp_substr(e.String, "[0-9]+", 1, 1, "i"),
        Pos = SqlFunctions.SqlServer.regexp_instr(e.String, "[0-9]+", 1, 1, 1)
    })
    .ToList();
```

`regexp_matches(input, pattern[, match_type])` — источник `FROM`: одна строка на неперекрывающееся
совпадение с `match_id bigint`, `start_position int`, `end_position int`, `match_value` (`varchar(n)`)
и `substring_matches` (`varchar(max)`, JSON-текст захватов движка, материализуется полностью и никогда
не обрезается). Строка без совпадений или `NULL`-вход дают ноль строк.

```csharp
var rows = dataContext
    .From(() => SqlFunctions.SqlServer.regexp_matches("a1b2", "[0-9]"))
    .Select(r => new { r.MatchId, r.MatchValue, r.SubstringMatches })
    .ToList();
```

Все три доступны **только в SQL Server 2025 (мажорная версия 17)** и закрыты так же, как
`regexp_count`; скаляры не зависят от уровня совместимости и работают на 160 и 170. `regexp_matches`
отличается: ему нужен уровень совместимости **170**, а на 160 движок сообщает, что функции нет
(Msg 208). Это рантайм-свойство, которое диалект не может прочитать; оно документируется и
проверяется вживую, а не предполагается. SQL Server 2022 и раньше отклоняют каждое имя до SQL.

### SQL Server 2025: нечёткое сопоставление

`edit_distance(input, pattern)` возвращает расстояние Левенштейна как `int?`,
`edit_distance_similarity` — сходство как `int?` в `0..100`, `jaro_winkler_distance` — как `double?`
(`float(53)`), `jaro_winkler_similarity` — как `int?` в `0..100`; `NULL`-операнд даёт SQL `NULL`.
Ограниченная форма `edit_distance(input, pattern, maximum_distance)` возвращает `maximum_distance + 1`,
когда истинное расстояние превышает предел, а отрицательный предел движок трактует как отсутствие
ограничения; опущенный предел и переданный `NULL` различаются (`NULL` даёт SQL `NULL`). CLR-операнд
шире 32 бит для предела отклоняется до SQL, а не обрезается.

```csharp
var scores = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Distance = SqlFunctions.SqlServer.edit_distance(e.String, "kitten"),
        Bounded = SqlFunctions.SqlServer.edit_distance(e.String, "kitten", 2),
        Similar = SqlFunctions.SqlServer.edit_distance_similarity(e.String, "kitten"),
        Jaro = SqlFunctions.SqlServer.jaro_winkler_similarity(e.String, "martha")
    })
    .ToList();
```

Четыре имени доступны **только в SQL Server 2025 (мажорная версия 17)**, и на протестированной
сборке (`17.0.5005.3`) дополнительно требуют уровень совместимости БД 170, `PREVIEW_FEATURES = ON`
(по умолчанию выключен) и Windows-коллацию — коллация `SQL_*` даёт Msg 9847, а без preview-флага
функция даёт Msg 195. Версионный флаг выражает только гейт мажорной версии; рантайм-предусловия
документируются и проверяются вживую, и обещаний для будущих сборок не даётся.

Доступность строковых функций в MariaDB, ограничения версий и отказы перечислены в
[каталоге MariaDB](08-reference.md#mariadb-catalogue).


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
| `SqlFunctions.Sql.octet_length(s)` | `octet_length` | `DATALENGTH` | `OCTET_LENGTH` | `toInt32(length(s))` | `octet_length` |
| `SqlFunctions.Sql.bit_length(s)` | `bit_length` | `DATALENGTH(s) * 8` | `BIT_LENGTH` | `toInt32(length(s) * 8)` | `octet_length(s) * 8` |

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
- ClickHouse-ветки приводят серверный `UInt64` к `Int32` через `toInt32` (для `bit_length` обёрнуто
  всё произведение `length(s) * 8`). Это преобразование **не проверяется**: результаты вне диапазона
  `Int32` переполняются по модулю 2^32, а не бросают исключение, и не являются точными, поэтому
  объявленный `int?` для очень больших строк следует семантике преобразования ClickHouse.

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

## Строковые функции ClickHouse (#286, T286b)

Строковая поверхность, существующая только в ClickHouse, живёт на `SqlFunctions.ClickHouse`; каждый
член рендерит точное имя вендора и отклоняется всеми остальными диалектами. Формы результатов
объявлены явно: `overlay`/`initcap`/`soundex`/`substring_index`/`normalize_utf8_*` возвращают `string`
(или `string?` для nullable-аргумента), `count_substrings`/`edit_distance`/`byte_hamming_distance` —
`ulong`/`int`, а `extract_groups`/`split_by_*` — `string[]`.

| Функция | SQL |
|---|---|
| `SqlFunctions.ClickHouse.overlay(s, replacement, offset[, length])` | `overlay(s, replacement, offset[, length])` |
| `SqlFunctions.ClickHouse.initcap(s)` | `initcap(s)` |
| `SqlFunctions.ClickHouse.soundex(s)` | `soundex(s)` |
| `SqlFunctions.ClickHouse.base64_encode(s)` / `base64_decode(s)` | `base64Encode` / `base64Decode` |
| `SqlFunctions.ClickHouse.hex(s)` / `unhex(s)` | `hex` / `unhex` |
| `SqlFunctions.ClickHouse.edit_distance(a, b)` | `editDistance(a, b)` |
| `SqlFunctions.ClickHouse.jaro_similarity(a, b)` | `jaroSimilarity(a, b)` |
| `SqlFunctions.ClickHouse.byte_hamming_distance(a, b)` | `byteHammingDistance(a, b)` |
| `SqlFunctions.ClickHouse.substring_index(s, delim, count)` | `substringIndex(s, delim, count)` |
| `SqlFunctions.ClickHouse.count_substrings(s, sub[, start])` | `countSubstrings(s, sub[, start])` |
| `SqlFunctions.ClickHouse.extract_groups(pattern, s)` | `extractGroups(pattern, s)` → `string[]` |
| `SqlFunctions.ClickHouse.normalize_utf8_nfc(s)` / `_nfd` / `_nfkc` / `_nfkd` | `normalizeUTF8NFC` / `NFD` / `NFKC` / `NFKD` |

Краевые семантики закреплены живыми тестами на 25.8.33.6: `overlay` считает с 1, длина по умолчанию
равна длине замены (нулевая длина вставляет, смещение за концом дописывает); `initcap` работает только
с ASCII; `soundex` возвращает `0000`, если во входе нет ASCII-буквы; `editDistance` считает **байты**
UTF-8, а `byteHammingDistance` дополняет более короткий вход нулями. `base64Decode('!!!')`,
многосимвольный разделитель `substringIndex` и отрицательный старт `countSubstrings` отклоняются
сервером, а не подменяются дефолтом; `extractGroups` по nullable-аргументу отклоняется, потому что
ClickHouse не может вложить `Array(String)` в `Nullable`.

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

### Остаточные строковые, кодировочные и regexp-помощники (#281)

Оставшаяся часть строкового каталога PostgreSQL доступна на той же поверхности, один член на
серверную перегрузку. Каждое имя гейтится **отдельно** через
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)), а
провайдер, не реализующий интерфейс, отклоняет вызов `NotSupportedException`:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.btrim(value)` | `btrim(value)` |
| `SqlFunctions.Postgres.btrim(value, characters)` | `btrim(value, characters)` |
| `SqlFunctions.Postgres.btrim(bytes, bytesToTrim)` | `btrim(bytes, bytesToTrim)` → `bytea` |
| `SqlFunctions.Postgres.ltrim(value, characters)` | `ltrim(value, characters)` |
| `SqlFunctions.Postgres.ltrim(bytes, bytesToTrim)` | `ltrim(bytes, bytesToTrim)` → `bytea` |
| `SqlFunctions.Postgres.rtrim(value, characters)` | `rtrim(value, characters)` |
| `SqlFunctions.Postgres.rtrim(bytes, bytesToTrim)` | `rtrim(bytes, bytesToTrim)` → `bytea` |
| `SqlFunctions.Postgres.starts_with(value, prefix)` | `starts_with(value, prefix)` |
| `SqlFunctions.Postgres.parse_ident(value, strict)` | `parse_ident(value, strict)` → `text[]` |
| `SqlFunctions.Postgres.quote_ident(value)` | `quote_ident(value)` |
| `SqlFunctions.Postgres.quote_literal(value)` | `quote_literal(value)` |
| `SqlFunctions.Postgres.quote_nullable(value)` | `quote_nullable(value)` |
| `SqlFunctions.Postgres.to_ascii(value, encoding)` | `to_ascii(value, encoding)` (`text`, `name`) |
| `SqlFunctions.Postgres.to_ascii(value, encodingCode)` (`int`) | `to_ascii(value, encodingCode)` (`text`, `integer`) — **код** кодировки PostgreSQL: `8` = `LATIN1`, `9` = `LATIN2`, `16` = `LATIN9`, `29` = `WIN1250` |
| `SqlFunctions.Postgres.to_hex(value)` (`int?`) | `to_hex(value)` |
| `SqlFunctions.Postgres.to_hex(value)` (`long?`) | `to_hex(value)` (`bigint`) |
| `SqlFunctions.Postgres.to_oct(value)` (`int?`) | `to_oct(value)` |
| `SqlFunctions.Postgres.to_oct(value)` (`long?`) | `to_oct(value)` (`bigint`) |
| `SqlFunctions.Postgres.to_bin(value)` (`int?`) | `to_bin(value)` |
| `SqlFunctions.Postgres.to_bin(value)` (`long?`) | `to_bin(value)` (`bigint`) |
| `SqlFunctions.Postgres.convert_from(bytes, encoding)` | `convert_from(bytes, encoding)` |
| `SqlFunctions.Postgres.convert_to(value, encoding)` | `convert_to(value, encoding)` → `bytea` |
| `SqlFunctions.Postgres.encode(bytes, format)` | `encode(bytes, format)` |
| `SqlFunctions.Postgres.decode(value, format)` | `decode(value, format)` → `bytea` |
| `SqlFunctions.Postgres.get_bit(bytes, index)` | `get_bit(bytes, index)` → `int` (`0` или `1`) |
| `SqlFunctions.Postgres.set_bit(bytes, index, value)` | `set_bit(bytes, index, value)` → `bytea` |
| `SqlFunctions.Postgres.get_byte(bytes, index)` | `get_byte(bytes, index)` → `int` |
| `SqlFunctions.Postgres.set_byte(bytes, index, value)` | `set_byte(bytes, index, value)` → `bytea` |
| `SqlFunctions.Postgres.bit_count(bytes)` | `bit_count(bytes)` → `bigint` |
| `SqlFunctions.Postgres.normalize(value, form)` | `normalize(value, form)` |
| `SqlFunctions.Postgres.unistr(value)` | `unistr(value)` |
| `SqlFunctions.Postgres.string_to_table(value, delimiter)` | `string_to_table(value, delimiter)` → одна строка на фрагмент |
| `SqlFunctions.Postgres.string_to_table(value, delimiter, nullString)` | `string_to_table(value, delimiter, nullString)` |
| `SqlFunctions.Postgres.regexp_match(value, pattern)` | `regexp_match(value, pattern)` → `text[]` |
| `SqlFunctions.Postgres.regexp_match(value, pattern, flags)` | `regexp_match(value, pattern, flags)` → `text[]` |

NULL-аргумент даёт NULL для каждого из них. Неочевидная семантика:

* `regexp_match` возвращает NULL, когда шаблон не совпал, — а не пустой массив, — и массив из одного
  элемента с полным совпадением, когда в шаблоне нет захватывающих групп.
* `normalize` принимает голое ключевое слово грамматики (`NFC`, `NFD`, `NFKC` или `NFKD`), а не
  строковый литерал: форма обязана быть константой и рендерится без кавычек (`normalize(value, NFD)`).
* `to_hex` для отрицательного `int` даёт запись в дополнительном коде (`-1` → `ffffffff`);
  перегрузки с `long` — это формы `to_hex`/`to_oct`/`to_bin` для `bigint`.
* `encode`/`decode` принимают `(data, format)` с `base64`, `hex` или `escape`; некорректный вход
  `decode` вызывает ошибку на сервере, а не «ремонтируется».
* `get_bit`/`set_bit` нумеруют биты от младшего бита первого байта, `get_byte`/`set_byte` — байты
  с нуля, а `bit_count` возвращает число установленных бит как `bigint` (`0xA1`: бит 0 установлен,
  байт 0 = `161`, установлено три бита).
* Двухаргументные `btrim`/`ltrim`/`rtrim` принимают **набор символов** (срезается каждый
  перечисленный символ), а не подстроку; перегрузки с `byte[]` — это формы для `bytea`.
* `parse_ident` возвращает части через точку как `text[]`, а `strict` решает, бросить ли ошибку на
  невалидном идентификаторе или обрезать его после первой ошибки.
* `string_to_table` — единственный член семейства, возвращающий набор строк: одна строка на фрагмент,
  а третий аргумент превращает совпавший с ним фрагмент в NULL-строку.
* `to_ascii` доступна с кодировкой, заданной текстом (`to_ascii(value, 'LATIN1')`), и с кодировкой,
  заданной её **кодом** PostgreSQL (`to_ascii(value, 8)`); одноаргументная форма отклонена (см. ниже).

### Проверенные отказы

Закреплённый сервер — PostgreSQL 17.11. Член, который на нём не исполняется, отклоняется во время
подготовки запроса — `NotSupportedException`, до того как на сервер уйдёт команда:

| Семейство | Отклонённый член | Проверенная причина |
|---|---|---|
| string | `casefold(text)` | Функция PostgreSQL 18, отсутствует в `pg_proc` версии 17.11; имя падает с `42883`. |
| string | `to_ascii(text)` (один аргумент) | PostgreSQL 17 поддерживает конверсию только из LATIN1, LATIN2, LATIN9 и WIN1250, а закреплённая база-фикстура — UTF8, поэтому форма выдаёт `0A000`; используйте `to_ascii(value, encoding)` — поставляемое сообщение: `The one-argument to_ascii function is refused: PostgreSQL 17 supports the conversion only from LATIN1, LATIN2, LATIN9 and WIN1250, and the pinned fixture database is UTF8, so the form raises 0A000; use to_ascii(value, encoding).` |
| string | `crc32(bytea)` / `crc32c(bytea)` | Функции PostgreSQL 18, отсутствуют в `pg_proc` версии 17.11; имя падает с `42883`. |
| string | `quote_literal(anyelement)` | доступна только перегрузка `text`; типизированная перегрузка над каждым CLR-типом вынудила бы преобразования, которых пути выражений не поддерживают. |
| string | `quote_nullable(anyelement)` | то же ограничение `anyelement`; перегрузка `text` доступна. |

`unistr(text)` поставлена — в 17.11 она есть. Выставлены обе перегрузки `to_ascii`: форма
`(text, integer)` принимает код кодировки PostgreSQL, поэтому `to_ascii(value, 8)` — это конверсия
`LATIN1` (`SELECT pg_encoding_to_char(8)` → `LATIN1`), а номер Windows-кодовой страницы вроде `1252`
кодом кодировки не является, и отправленная команда падает на сервере с `42704`. У `bit`-форм
бинарных помощников (`get_bit`/`set_bit`/`bit_count` над `bit`) нет CLR-отображения, поэтому для них
не выставлено ни одного члена, и вызов отклоняется, а не транслируется.

## Полнотекстовый поиск (PostgreSQL, #281)

Кросс-провайдерные предикаты и более ранняя native-поверхность — `contains`/`freetext`, `to_tsvector`,
`to_tsquery`, `plainto_tsquery`/`phraseto_tsquery`/`websearch_to_tsquery`, `ts_match`, `ts_rank`,
`ts_rank_cd`, `ts_headline` и `@@` — описаны в разделе
[SQL для конкретных провайдеров › Полнотекстовый поиск](../guide/provider-specific-sql/postgresql.md#полнотекстовый-поиск).
17 поставленных имён ниже живут на той же поверхности, гейтятся
[`SupportsTextSearchFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTextSearchFunctions), и все
остальные провайдеры отклоняют их на этапе построения SQL.

**Мост через `text`.** Npgsql отображает `tsvector`/`tsquery` в собственные типы
`NpgsqlTsVector`/`NpgsqlTsQuery`, а не в `string`, поэтому каждый `tsvector`-результат этих членов
отрисовывается как `cast(... as text)`. По той же причине каждый аргумент типов
`tsvector`/`tsquery`/`regconfig`/`regdictionary`/`"char"` приводится к своему серверному типу:
`text`-параметр не разрешается ни в одну из таких перегрузок. `to_tsvector` (и остальные более ранние
члены) сохраняют голый native-вызов, потому что используются в позиции аргумента (`@@`, `ts_rank`,
`ts_headline`), а не проецируются как значение.

14 скалярных имён делятся на три группы в таком порядке — векторы и запросы, проверка и перезапись
запроса, преобразование и лексирование:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.to_tsvector(document)` | `to_tsvector(document)` |
| `SqlFunctions.Postgres.array_to_tsvector(words)` | `cast(array_to_tsvector(@words) as text)` |
| `SqlFunctions.Postgres.json_to_tsvector(json, filter)` | `cast(json_to_tsvector(cast(@json as json), cast(@filter as jsonb)) as text)` |
| `SqlFunctions.Postgres.json_to_tsvector(config, json, filter)` | `cast(json_to_tsvector(cast(@config as regconfig), cast(@json as json), cast(@filter as jsonb)) as text)` |
| `SqlFunctions.Postgres.jsonb_to_tsvector(json, filter)` | `cast(jsonb_to_tsvector(cast(@json as jsonb), cast(@filter as jsonb)) as text)` |
| `SqlFunctions.Postgres.jsonb_to_tsvector(config, json, filter)` | `cast(jsonb_to_tsvector(cast(@config as regconfig), cast(@json as jsonb), cast(@filter as jsonb)) as text)` |
| `SqlFunctions.Postgres.numnode(tsquery)` | `numnode(cast(@tsquery as tsquery))` |
| `SqlFunctions.Postgres.querytree(tsquery)` | `querytree(cast(@tsquery as tsquery))` |
| `SqlFunctions.Postgres.setweight(tsvector, weight)` | `cast(setweight(cast(@tsvector as tsvector), cast(@weight as "char")) as text)` |
| `SqlFunctions.Postgres.setweight(tsvector, weight, lexemes)` | `cast(setweight(cast(@tsvector as tsvector), cast(@weight as "char"), cast(@lexemes as text[])) as text)` |
| `SqlFunctions.Postgres.strip(tsvector)` | `cast(strip(cast(@tsvector as tsvector)) as text)` |
| `SqlFunctions.Postgres.ts_delete(tsvector, lexeme)` | `cast(ts_delete(cast(@tsvector as tsvector), @lexeme) as text)` |
| `SqlFunctions.Postgres.ts_delete(tsvector, lexemes)` | `cast(ts_delete(cast(@tsvector as tsvector), @lexemes) as text)` |
| `SqlFunctions.Postgres.ts_filter(tsvector, weights)` | `cast(ts_filter(cast(@tsvector as tsvector), cast(@weights as "char"[])) as text)` |
| `SqlFunctions.Postgres.ts_rewrite(query, select)` | `cast(ts_rewrite(cast(@query as tsquery), @select) as text)` |
| `SqlFunctions.Postgres.ts_rewrite(query, target, substitute)` | `cast(ts_rewrite(cast(@query as tsquery), cast(@target as tsquery), cast(@substitute as tsquery)) as text)` |
| `SqlFunctions.Postgres.tsquery_phrase(query1, query2)` | `cast(tsquery_phrase(cast(@query1 as tsquery), cast(@query2 as tsquery)) as text)` |
| `SqlFunctions.Postgres.tsquery_phrase(query1, query2, distance)` | `cast(tsquery_phrase(cast(@query1 as tsquery), cast(@query2 as tsquery), @distance) as text)` |
| `SqlFunctions.Postgres.tsvector_to_array(tsvector)` | `tsvector_to_array(cast(@tsvector as tsvector))` |
| `SqlFunctions.Postgres.ts_lexize(dictionary, token)` | `ts_lexize(cast(@dictionary as regdictionary), @token)` |

* `array_to_tsvector` не назначает позиции и вызывает ошибку, если массив содержит `NULL`; пустой
  массив даёт пустой `tsvector`.
* `json_to_tsvector`/`jsonb_to_tsvector` принимают фильтр как текст `jsonb`-массива
  (`["string","numeric","boolean","key","all"]`); форма с тремя аргументами задаёт конфигурацию
  (`regconfig`).
* Второй аргумент `ts_delete` намеренно оставлен без приведения: объявленный тип параметра выбирает
  перегрузку `ts_delete(tsvector, text)` или `ts_delete(tsvector, text[])`.
* `strip` отбрасывает позиции и веса, `ts_filter` оставляет только лексемы с указанным весом, а
  `setweight` переназначает вес всем лексемам или только перечисленным — `NULL`-элемент, пустой массив
  и лексема, отсутствующая в векторе, оставляют его без изменений.
* `ts_rewrite` принимает либо SQL-запрос таблицы перезаписи (текст), либо пару
  `target`/`substitute`; `tsquery_phrase` соединяет два запроса через `<->`, а форма с тремя
  аргументами задаёт расстояние.
* `tsvector_to_array` возвращает лексемы как `text[]` в порядке хранения; `ts_lexize` возвращает
  лексемы одного токена как `text[]`, а стоп-слово даёт пустой массив.
* **Аргументы-массивы должны приходить из параметра.** `words`, `lexemes` и `weights` — это CLR
  `string?[]`: передавайте их как `SqlFunctions.Parameter<string?[]>(idx)` (или захваченный массив) и
  никогда как встроенный литерал массива — отправленный вызов всегда читает значения из связанного
  параметра, который диалект приводит к `text[]` или `"char"[]`.

Три читателя токенизатора — это `FROM`-источники (`ctx.From(() => ...)`); они токенизируют текст
конфигурацией текстового поиска текущей сессии:

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.ts_debug(document)` | `ts_debug(@document)` | `ITsDebugRow` | `Alias string? ← alias`; `Description string? ← description`; `Token string? ← token`; `Dictionaries string[]? ← dictionaries`; `Dictionary string? ← dictionary`; `Lexemes string[]? ← lexemes` |
| `SqlFunctions.Postgres.ts_parse(parser, document)` | `ts_parse(@parser, @document)` | `ITsParseRow` | `Tokid int? ← tokid`; `Token string? ← token` |
| `SqlFunctions.Postgres.ts_token_type(parser)` | `ts_token_type(@parser)` | `ITsTokenTypeRow` | `Tokid int? ← tokid`; `Alias string? ← alias`; `Description string? ← description` |

Перегрузки с `oid` у `ts_parse`/`ts_token_type` и `ts_debug(regconfig, text)` **не выставлены**:
оба члена принимают парсер по **имени** (или работают через конфигурацию сессии), неявного
приведения `text` → `oid` в PostgreSQL нет, поэтому `text`-аргумент никогда не может попасть в
`oid`-формы. У колонок `regdictionary`/`regdictionary[]` в `ts_debug` тоже нет CLR-отображения,
поэтому проекция приводит их к `text[]`/`text`. `ts_debug('')` и `ts_debug(NULL)` не дают строк,
`ts_token_type('default')` даёт 23 строки на PostgreSQL 17, а неизвестное имя парсера вызывает
`42704`.

## Строковые функции, хэширование и сжатие SQL Server

Остальные строковые функции и функции хэширования/сжатия, специфичные для T-SQL, живут на
`SqlFunctions.SqlServer` и гейтятся по имени через
[`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions); SQL Server — единственный провайдер,
реализующий флаг, поэтому все остальные бросают `NotSupportedException`:

| C# | SQL |
|---|---|
| `stuff(value, start, length, newValue)` | `stuff(...)` |
| `str(value)` / `str(value, length)` / `str(value, length, decimalPlaces)` | `str(...)` |
| `formatmessage(message, args...)` / `formatmessage(messageId, args...)` | `formatmessage(...)` |
| `parsename(objectName, piece)` | `parsename(...)` |
| `hashbytes(algorithm, data)` | `hashbytes(...)` |
| `checksum(values...)` / `binary_checksum(values...)` | `checksum(...)` / `binary_checksum(...)` |
| `compress(value)` (строка и `byte[]`) / `decompress(value)` | `compress(...)` / `decompress(...)` |

* `str` фиксирует общую длину (по умолчанию 10) и число знаков после запятой (по умолчанию 0);
  `formatmessage` принимает строку формата или id из `sys.messages` и не более 20 аргументов
  форматирования (больше — `NotSupportedException`).
* `parsename` разбирает четырёхчастное имя объекта и принимает константную часть в `1..4`.
* `hashbytes` принимает константное имя алгоритма (например, `"SHA2_256"`) и значение `byte[]`;
  `checksum`/`binary_checksum` — некриптографические контрольные суммы для обнаружения изменений, не
  замена `hashbytes`, и требуют хотя бы одного аргумента (`checksum()`/`binary_checksum()` бросают;
  форма с подстановочным знаком `checksum(*)` не открыта).
* `compress`/`decompress` используют GZIP и работают с `varbinary(max)`; `decompress` возвращает
  `null` для некорректного или обрезанного значения.
