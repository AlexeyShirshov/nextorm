# Дата и время

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

## Извлечение произвольных частей даты

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

## Расширенные дата и время PostgreSQL

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

## Runtime-настройки и последовательности (PostgreSQL)

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

## Форматирование дат и чисел в строки

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

## Информация о сессии и сервере

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

## Генераторы UUID

`SqlFunctions.Sql.gen_random_uuid()` (случайный v4) и `uuidv7()` — кросс-провайдерные
([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators)):

| C# | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `gen_random_uuid()` | `gen_random_uuid()` (13+) | `newid()` | — | `UUID_v4()` | `generateUUIDv4()` | — |
| `uuidv7()` | `uuidv7()` (18+) | — | — | `UUID_v7()` (11.7+) | `generateUUIDv7()` | — |

У MySQL есть только `UUID()` (v1), у SQLite генератора UUID нет, поэтому оба отклоняют вызов. Это
серверные генераторы, вычисляемые на каждую строку базой; `Guid.NewGuid()` — клиентское значение и
заменой не является.

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
