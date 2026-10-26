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
select cast(strftime('%Y', dt) as integer) as 'Year', cast(strftime('%m', dt) as integer) as 'Month', cast(strftime('%d', dt) as integer) as 'Day'
 from complex_entity where (id = 1)
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

## DateTimeOffset

`DateTimeOffset` — поддерживаемый тип колонки и скалярного значения на всех провайдерах: драйвер
читает его обратно без конвертера значений. Объявленный/хранимый SQL-тип зависит от провайдера:

| Провайдер | Тип |
|---|---|
| PostgreSQL | `timestamp with time zone` |
| SQL Server | `datetimeoffset` |
| MySQL / MariaDB | `datetime` |
| ClickHouse | `DateTime64(6)` |
| SQLite | динамическая типизация (выделенного типа нет) |

### Серверная трансляция

Поддерживаемые операции `DateTimeOffset` рендерятся как SQL и вычисляются **на сервере** — клиентские
часы и локальная зона клиента никогда не захватываются, и никакая операция не вычисляется молча как
клиентский параметр:

| C# | SQL |
|---|---|
| `DateTimeOffset.Now` | SQL Server `sysdatetimeoffset()`; PostgreSQL `now()`; MySQL/MariaDB `utc_timestamp(6)`; ClickHouse `now64(6, 'UTC')`; SQLite `strftime('%Y-%m-%d %H:%M:%f','now') \|\| '+00:00'` |
| `DateTimeOffset.UtcNow` | SQL Server `todatetimeoffset(sysutcdatetime(), '+00:00')`; у остальных провайдеров то же, что `Now` |
| `.Year` `.Month` `.Day` `.Hour` `.Minute` `.Second` `.DayOfYear` | извлечение части даты, специфичное для провайдера, с явной нормализацией в UTC в PostgreSQL (`... at time zone 'UTC'`) и ClickHouse (`toTimeZone(value, 'UTC')`) |
| `.AddYears` `.AddMonths` `.AddDays` `.AddHours` `.AddMinutes` `.AddSeconds` `.AddMilliseconds` | прибавление даты средствами провайдера (см. [Арифметику дат](#арифметика-дат)); значимая дробная часть интервала сохраняется, а не усекается до целых единиц |

Nullable-значения и `DateTimeOffset?` в `Where`/`Select`/`OrderBy` подчиняются обычным правилам: SQL
`NULL` остаётся `null`, а `HasValue`/`Value` прозрачны.

Члены, у которых **нет** SQL-трансляции, **падают fail-closed** с `NotSupportedException` во время
построения запроса — до любого клиентского вычисления или постоянного свёртывания:

* `.Offset`, `.UtcDateTime`, `.LocalDateTime`, `.Date`, `.Ticks` и `.DayOfWeek` — в том числе как
  получатель дальнейшего доступа (`e.Value.Offset.TotalMinutes`, `e.Value.UtcDateTime.Year`,
  `e.Value.Date.AddDays(1)`);
* `.ToOffset(...)` — независимо от того, константный аргумент или динамический;
* `.AddTicks(...)` и `.Add(TimeSpan)` — сейчас под guard'ом; их серверная трансляция отложена до
  следующего релиза;
* дробные `.AddYears`/`.AddMonths` — параметр CLR имеет тип `int`.

Отказ не зависит от формы проекции: анонимная проекция, DTO, предикат и простой скаляр
(`Select(e => e.Value.DayOfWeek)`, включая `.First()`/`.Single()` поверх него) сообщают одно и то же
сообщение при подготовке, до отправки любого SQL. Он также ограничен провайдерами с маппингом строк
(`IContextEnvironment.NeedMapping`); in-memory-провайдер продолжает вычислять такие члены на клиенте.
Члены, у которых трансляция есть (`.Year` … `.DayOfYear`, семейство `Add*`, `Now`/`UtcNow`), работают
в любой форме проекции.

`value.ToString(format)` и `string.Format` по-прежнему транслируются (см.
[Форматирование дат и чисел в строки](#форматирование-дат-и-чисел-в-строки)). Колонка табличного
параметра и элемент PostgreSQL-массива типа `DateTimeOffset` отображаются на форму timestamp with
time zone.

### Ограничения зоны и точности по провайдерам

Только SQL Server хранит offset как часть значения; остальные провайдеры хранят instant (или wall
time) и материализуют `DateTimeOffset` с нулевым offset'ом (канонический `+00:00`):

| Провайдер | Часы и offset | Части и арифметика |
|---|---|---|
| SQL Server | `datetimeoffset` сохраняет offset значения; `Now` — `sysdatetimeoffset()`, `UtcNow` — `todatetimeoffset(sysutcdatetime(), '+00:00')`. | Нативный `datepart` (по offset значения) и нативный `dateadd`, сохраняющий offset; дробное значение в day/hour/minute/second/millisecond масштабируется в более мелкую единицу `dateadd`, чтобы не усечься. |
| PostgreSQL | `now()` возвращает instant, не зависящий от сессионной зоны; `timestamp with time zone` хранит instant, поэтому материализованное значение имеет нулевой offset. | Части и `Add*` работают по UTC wall time (`at time zone 'UTC'`), а `Add*` перетипизируется в `timestamptz`, поэтому результат не зависит от сессионного `TimeZone` и DST. |
| MySQL / MariaDB | `utc_timestamp(6)`; колонка `datetime` хранит wall time и **не** сохраняет offset, поэтому исходный offset не восстанавливается. | Нативные части даты сохранённого wall time и нативный `date_add`; дробное значение в day/hour/minute масштабируется в более мелкую единицу. |
| ClickHouse | `now64(6, 'UTC')`; `DateTime64(6)` **не** хранит offset. Микросекундная точность сохраняется при материализации. | Части и `Add*` явно нормализуются в UTC (`toTimeZone(value, 'UTC')`); дробное значение в крупной единице масштабируется в `addMicroseconds`, чтобы дробь не потерялась. |
| SQLite | `strftime('%Y-%m-%d %H:%M:%f','now') \|\| '+00:00'`; динамическая типизация хранит UTC instant как текст, с миллисекундной точностью. | Части и арифметика UTC-текста через `strftime`/модификаторы даты; текст с offset'ом трактуется как instant. |

В ClickHouse значение материализуется через адаптацию читателя: драйвер отдаёт `DateTime64` как
`System.DateTime`, которое переинтерпретируется как `DateTimeOffset` с нулевым offset'ом, с
сохранением микросекунд.

## Расширенные дата и время PostgreSQL

Эти функции входят в расширенную библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), только PostgreSQL):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.make_interval(y, mo, d, h, mi, s)` | `make_interval(y, mo, 0, d, h, mi, s)` (параметр `weeks` у PostgreSQL зафиксирован в `0`) |
| `SqlFunctions.Postgres.make_time(h, mi, sec)` / `make_timestamp(y, mo, d, h, mi, sec)` | `make_time(...)` / `make_timestamp(...)` |
| `SqlFunctions.Postgres.age(a, b)` | `age(a, b)` (результат — `interval`; при материализации в колонку `TimeSpan` бросается `InvalidCastException`, если возвращённое значение несёт ненулевую месячную составляющую — любого знака, — с указанием календарных месяцев, поэтому месяц никогда не читается как 30 дней; значение с нулевыми месяцами конвертируется точно) |
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

Oracle-совместимые конверсии MariaDB **зависят от версии** и не входят в переносимую поверхность:
`months_between` (12.2.1), `to_date` (12.3) и `to_number` (семейство 12.2, принудительный минимум 12.2.1)
отклоняются с `NotSupportedException` контекстом `MariaDbDataContext` без версии; `add_months`/`to_char`
доступны с MariaDB 10.6. См.
[MySQL и MariaDB-специфичный SQL](../guide/provider-specific-sql/mysql.md#mariadb).

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
select date_trunc('month', dt) as "Month"
 from complex_entity
-- ClickHouse
select dateTrunc('month', dt) as `Month`
 from complex_entity
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
select dateadd(day, 1, dt) as [NextDay], eomonth(dt) as [MonthEnd]
 from complex_entity
-- PostgreSQL
select dt + (1 * interval '1 day') as "NextDay", (date_trunc('month', dt) + interval '1 month - 1 day') as "MonthEnd"
 from complex_entity
-- ClickHouse
select addDays(dt, 1) as `NextDay`, toLastDayOfMonth(dt) as `MonthEnd`
 from complex_entity
-- MySQL/MariaDB
select date_add(dt, interval 1 day) as `NextDay`, last_day(dt) as `MonthEnd`
 from complex_entity
-- SQLite
select datetime(dt, (1) || ' days') as 'NextDay', date(dt, 'start of month', '+1 month', '-1 day') as 'MonthEnd'
 from complex_entity
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

## Часы, смещение и `*FROMPARTS` SQL Server

Функции часов, смещения и `*FROMPARTS` живут на `SqlFunctions.SqlServer` и гейтятся по имени
[`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions); SQL Server — единственный провайдер,
реализующий флаг, поэтому все остальные (включая in-memory) бросают `NotSupportedException`.
`sysdatetime`/`sysutcdatetime` — недетерминированные серверные часы (локальные и UTC), поэтому их
нельзя считать фиксированным значением; `switchoffset`/`todatetimeoffset` принимают смещение либо
как константную строку (`"-08:00"`), либо как число минут со знаком.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Now = SqlFunctions.SqlServer.sysdatetime(),
        NowOffset = SqlFunctions.SqlServer.sysdatetimeoffset(),
        UtcNow = SqlFunctions.SqlServer.sysutcdatetime(),
        Switched = SqlFunctions.SqlServer.switchoffset(SqlFunctions.SqlServer.sysdatetimeoffset(), "-08:00"),
        Offset = SqlFunctions.SqlServer.todatetimeoffset(e.Datetime, 120),
        Time = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 7),
        Stamp = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 2, 3, 4, 5, 6, 3)
    })
    .ToList();
```

```sql
select sysdatetime() as [Now], sysdatetimeoffset() as [NowOffset], sysutcdatetime() as [UtcNow],
       switchoffset(sysdatetimeoffset(), '-08:00') as [Switched], todatetimeoffset(dt, 120) as [Offset],
       timefromparts(1, 2, 3, 4, 7) as [Time], datetime2fromparts(2020, 1, 2, 3, 4, 5, 6, 3) as [Stamp]
 from complex_entity
```

| C# | SQL |
|---|---|
| `sysdatetime()` | `sysdatetime()` |
| `sysdatetimeoffset()` | `sysdatetimeoffset()` |
| `sysutcdatetime()` | `sysutcdatetime()` |
| `switchoffset(value, timeZone)` / `switchoffset(value, minutes)` | `switchoffset(value, <offset>)` |
| `todatetimeoffset(value, timeZone)` / `todatetimeoffset(value, minutes)` | `todatetimeoffset(value, <offset>)` |
| `timefromparts(h, mi, s, fractions, precision)` | `timefromparts(...)` |
| `smalldatetimefromparts(y, mo, d, h, mi)` | `smalldatetimefromparts(...)` |
| `datetimefromparts(y, mo, d, h, mi, s, ms)` | `datetimefromparts(...)` |
| `datetime2fromparts(y, mo, d, h, mi, s, fractions, precision)` | `datetime2fromparts(...)` |
| `datetimeoffsetfromparts(y, mo, d, h, mi, s, fractions, hourOffset, minuteOffset, precision)` | `datetimeoffsetfromparts(...)` |

`timefromparts`, `datetime2fromparts` и `datetimeoffsetfromparts` требуют, чтобы завершающая точность
была **константным целым в `0..7`**: неконстантное, `null` или выходящее за диапазон значение бросает
`NotSupportedException` до генерации SQL. `null` в любой другой части даёт `null`-результат, как у
нативной функции. `timefromparts` материализуется как `TimeSpan`, конструкторы
`smalldatetime`/`datetime`/`datetime2` — как `DateTime`, а функции часов и смещения — как
`DateTime` / `DateTimeOffset`, соответствующие типу результата T-SQL. Это датовая половина
скалярной поверхности, специфичной для SQL Server; остальные T-SQL-скаляры описаны на
соответствующих категорийных страницах.

Чтение нативного `time` не меняет тип хранения. Вычисленный результат `timefromparts`/`cast` и
физическая колонка `time`, `time(3)` или `time(7)` читаются как `TimeSpan` построчно, с сохранением
точности значения. Свойство-**длительность** (свойство `TimeSpan` с `[Duration(...)]` или fluent
`Duration(...)`) по-прежнему хранится как `bigint` в SQL Server и по-прежнему масштабируется через
свой `DurationUnit`; оно может быть отрицательным или не меньше 24 ч, что нативный `time` представить
не может. Проекция `TimeOnly` не поддерживается.

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
select toInt32(toYear(dt)) as `Year`, toStartOfMonth(dt) as `MonthStart`
 from complex_entity
```
