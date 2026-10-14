# Date and time

`DateTime.Now` and `DateTime.UtcNow` are rendered as SQL expressions instead of being evaluated as a
parameter. `.Year`, `.Month`, `.Day`, `.DayOfYear` and `.Hour` (as well as `.Minute` and `.Second`)
become the provider's date-part extraction:

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

Output:

| Year | Month | Day |
|------|-------|-----|
| 2023 | 1     | 1   |

An important detail for SQLite: `strftime` returns text, so the result is wrapped in
`cast(... as integer)` to materialise like the `int` CLR property.

## Extracting arbitrary date parts

`SqlFunctions.Sql.extract(part, value)` returns the integer date part for `year`, `quarter`, `month`,
`week` (ISO 8601), `day`, `doy`, `dow` (0=Sunday..6=Saturday), `isodow` (1=Monday..7=Sunday), `hour`,
`minute` and `second`. `SqlFunctions.Sql.date_part(part, value)` returns the numeric `epoch` (seconds
since 1970-01-01, including any fraction). Both take a constant part name and render each provider's
native form, so the result is the same on every provider:

| Provider | `extract("quarter", dt)` | `extract("week", dt)` | `extract("dow", dt)` | `date_part("epoch", dt)` |
|---|---|---|---|---|
| PostgreSQL | `extract(quarter from dt)` | `extract(week from dt)` | `extract(dow from dt)` | `cast(extract(epoch from dt) as double precision)` |
| SQL Server | `datepart(quarter, dt)` | `datepart(isowk, dt)` | `(datepart(weekday, dt) + @@datefirst - 1) % 7` | `cast(datediff_big(millisecond, '19700101', dt) as float) / 1000.0` |
| MySQL/MariaDB | `quarter(dt)` | `weekofyear(dt)` | `(dayofweek(dt) - 1)` | `cast(unix_timestamp(dt) as double)` |
| SQLite | `cast((cast(strftime('%m', dt) as integer) + 2) / 3 as integer)` | ISO week via `strftime('%j', date(dt, '-3 days', 'weekday 4'))` | `cast(strftime('%w', dt) as integer)` | `((julianday(dt) - 2440587.5) * 86400.0)` |
| ClickHouse | `toQuarter(dt)` | `toISOWeek(dt)` | `(toDayOfWeek(dt) % 7)` | `toFloat64(toUnixTimestamp(dt))` |

`DateTime.DayOfWeek` is not translated as a property (its `datepart(weekday)` equivalent depends on the
session `DATEFIRST`); use `extract("dow", value)` or `extract("isodow", value)` for a normalised value.

## DateTimeOffset

`DateTimeOffset` is a supported column and scalar type on every provider: the driver reads it back
without a value converter. The declared/stored SQL type is provider-specific:

| Provider | Type |
|---|---|
| PostgreSQL | `timestamp with time zone` |
| SQL Server | `datetimeoffset` |
| MySQL / MariaDB | `datetime` |
| ClickHouse | `DateTime64(6)` |
| SQLite | dynamic typing (no dedicated type) |

### Server-side translation

The supported `DateTimeOffset` operations are rendered as SQL and evaluated **on the server** — the
client clock and the client's local time zone are never captured, and no operation is silently computed
as a client parameter:

| C# | SQL |
|---|---|
| `DateTimeOffset.Now` | SQL Server `sysdatetimeoffset()`; PostgreSQL `now()`; MySQL/MariaDB `utc_timestamp(6)`; ClickHouse `now64(6, 'UTC')`; SQLite `strftime('%Y-%m-%d %H:%M:%f','now') \|\| '+00:00'` |
| `DateTimeOffset.UtcNow` | SQL Server `todatetimeoffset(sysutcdatetime(), '+00:00')`; on the other providers the same as `Now` |
| `.Year` `.Month` `.Day` `.Hour` `.Minute` `.Second` `.DayOfYear` | the provider's date-part extraction, with an explicit UTC normalization on PostgreSQL (`... at time zone 'UTC'`) and ClickHouse (`toTimeZone(value, 'UTC')`) |
| `.AddYears` `.AddMonths` `.AddDays` `.AddHours` `.AddMinutes` `.AddSeconds` `.AddMilliseconds` | the provider's date-add (see [Date arithmetic](#date-arithmetic)); a significant fractional interval is kept instead of being truncated to whole units |

Nullable values and a `DateTimeOffset?` in `Where`/`Select`/`OrderBy` follow the ordinary rules: SQL
`NULL` stays `null`, and `HasValue`/`Value` are transparent.

Members with **no** SQL translation **fail closed** with `NotSupportedException` while the query is
built, before any client evaluation or constant folding:

* `.Offset`, `.UtcDateTime`, `.LocalDateTime`, `.Date`, `.Ticks` and `.DayOfWeek` — including as the
  receiver of a further access (`e.Value.Offset.TotalMinutes`, `e.Value.UtcDateTime.Year`,
  `e.Value.Date.AddDays(1)`);
* `.ToOffset(...)`, whether the argument is a constant or dynamic;
* `.AddTicks(...)` and `.Add(TimeSpan)` — guarded now; their server translation is deferred to a later
  release;
* a fractional `.AddYears`/`.AddMonths` — the CLR amount is an `int`.

`value.ToString(format)` and `string.Format` are still translated (see
[Formatting dates and numbers to strings](#formatting-dates-and-numbers-to-strings)). A
`DateTimeOffset` table-valued-parameter column and a PostgreSQL array element map to the
timestamp-with-time-zone form.

### Per-provider zone and precision limits

Only SQL Server keeps the offset as part of the value; every other provider stores an instant (or a
wall time) and materialises an offset-zero `DateTimeOffset` (a canonical `+00:00`):

| Provider | Clock and offset | Parts and arithmetic |
|---|---|---|
| SQL Server | `datetimeoffset` preserves the value's offset; `Now` is `sysdatetimeoffset()`, `UtcNow` is `todatetimeoffset(sysutcdatetime(), '+00:00')`. | Native `datepart` (per the value's offset) and native `dateadd`, which preserves the offset; a fractional day/hour/minute/second/millisecond amount is scaled to a finer `dateadd` unit so it is not truncated. |
| PostgreSQL | `now()` returns an instant independent of the session time zone; `timestamp with time zone` stores an instant, so the materialised value is offset-zero. | Parts and `Add*` run on the UTC wall time (`at time zone 'UTC'`) and `Add*` is re-typed to `timestamptz`, so the result does not depend on the session `TimeZone` or DST. |
| MySQL / MariaDB | `utc_timestamp(6)`; a `datetime` column stores a wall time and **does not** keep the offset, so the original offset is not restored. | Native date parts of the stored wall time and native `date_add`; a fractional day/hour/minute amount is scaled to a finer unit. |
| ClickHouse | `now64(6, 'UTC')`; `DateTime64(6)` does **not** store the offset. Microsecond precision is preserved on materialisation. | Parts and `Add*` normalise to UTC (`toTimeZone(value, 'UTC')`); a coarse-unit fractional amount is scaled to `addMicroseconds` so the fraction survives. |
| SQLite | `strftime('%Y-%m-%d %H:%M:%f','now') \|\| '+00:00'`; dynamic typing stores a UTC instant as text, at millisecond precision. | The `strftime`/date-modifier parts and arithmetic of the UTC text; an offset-bearing text is treated as an instant. |

On ClickHouse the value is materialised through a reader adaptation that reads the driver's `DateTime`
(the driver surfaces `DateTime64` as `System.DateTime`) and reinterprets it as an offset-zero
`DateTimeOffset`, retaining microseconds.

## PostgreSQL extended date and time

These are part of the extended scalar library ([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions),
PostgreSQL only):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.make_interval(y, mo, d, h, mi, s)` | `make_interval(y, mo, 0, d, h, mi, s)` (PostgreSQL's `weeks` is pinned to `0`) |
| `SqlFunctions.Postgres.make_time(h, mi, sec)` / `make_timestamp(y, mo, d, h, mi, sec)` | `make_time(...)` / `make_timestamp(...)` |
| `SqlFunctions.Postgres.age(a, b)` | `age(a, b)` (the result is an `interval`; a whole-month part is read back as 30 days) |
| `SqlFunctions.Postgres.date_bin(stride, source, origin)` | `date_bin(cast(stride as interval), source, origin)` |
| `SqlFunctions.Postgres.justify_days(interval)` / `justify_hours(interval)` | `justify_days(interval)` / `justify_hours(interval)` |
| `SqlFunctions.Postgres.to_char(value, format)` | `to_char(value, format)` |
| `SqlFunctions.Postgres.to_date(text, format)` | `to_date(text, format)` |
| `SqlFunctions.Postgres.to_number(text, format)` | `to_number(text, format)` |
| `SqlFunctions.Postgres.to_timestamp(epoch)` / `to_timestamp(text, format)` | `to_timestamp(...)` |
| `SqlFunctions.Postgres.timezone(zone, value)` | `timezone(zone, value)` |
| `SqlFunctions.Postgres.current_date()` / `current_time()` / `localtime()` / `localtimestamp()` | the same key words |
| `SqlFunctions.Postgres.pg_typeof(x)` | `cast(pg_typeof(x) as text)` |

Date construction and arithmetic otherwise use the portable surface: `date_from_parts` (which renders
PostgreSQL `make_date`), `date_add`, `date_diff`, `date_trunc` and the `DateTime` members (see
[Date arithmetic](#date-arithmetic) below).

## Formatting dates and numbers to strings

nextorm translates the culture-invariant subset of the CLR formatting API — `string.Format`,
interpolated strings with a format specifier (`$"{e.Created:yyyy-MM-dd}"`) and
`value.ToString(format)` — into the provider's native formatting function. Because the template
languages are mutually incompatible (SQL Server `FORMAT` follows .NET custom format strings,
PostgreSQL `to_char`, and the `%`-style `DATE_FORMAT`/`strftime`/`formatDateTime`), nextorm accepts only
a documented portable subset and rejects anything else instead of emitting SQL that formats differently:

* **Numbers** — the standard specifiers `N`, `F`, `D` and `X` with an optional precision
  (`$"{amount:N2}"`, `value.ToString("D8")`). A provider that cannot render a specifier exactly
  (MySQL/MariaDB have no grouping-free `F`; SQLite and ClickHouse have no invariant `N`) throws
  `NotSupportedException`.
* **Dates** — the custom tokens `yyyy`, `yy`, `MM`, `dd`, `HH`, `mm`, `ss` with the separators `-`, `/`,
  `.`, `:`, `T` and space (`$"{e.Created:yyyy-MM-dd}"`). Everything else is rejected.
* **Culture** — invariant only: `string.Format` with no provider or with
  `CultureInfo.InvariantCulture`. Any other `IFormatProvider` throws.

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

For a format outside this subset, declare a provider-specific user-defined function instead. On SQL
Server, a `format` UDF, for example:

```csharp
public static class DemoUdf
{
    [SqlFunction("format")]
    public static string Format(DateTime? value, string format) => throw new NotSupportedException();
}
```

On PostgreSQL the same job is done by `SqlFunctions.Postgres.to_char(value, 'YYYY-MM')`
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions)).

## Date truncation (PostgreSQL, SQL Server, ClickHouse)

`SqlFunctions.Sql.date_trunc(field, value)` truncates a timestamp to a date part
([`SupportsDateTrunc`](xref:NextORM.Core.ISqlDialect.SupportsDateTrunc); PostgreSQL, SQL Server 2022+ and ClickHouse opt in). The field must
be a constant string from the supported set. SQL Server renders `datetrunc(part, value)`, folding the
plural ANSI parts to the singular T-SQL spellings (`milliseconds` → `millisecond`) and rejecting
`decade`/`century`/`millennium`; ClickHouse renders `dateTrunc('part', value)` with the same
singular mapping and the same rejection of the three large parts:

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

## Date arithmetic

`SqlFunctions.Sql.date_add(field, amount, value)` adds a number of units to a date/time and
`SqlFunctions.Sql.end_of_month(value)` returns the last day of its month ([`SupportsDateArithmetic`](xref:NextORM.Core.ISqlDialect.SupportsDateArithmetic);
PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB and SQLite opt in). The field must be a constant
string; the provider validates which parts it accepts ([`SupportsDateAddField`](xref:NextORM.Core.ISqlDialect.SupportsDateAddField(System.String)) and friends).
SQL Server renders `dateadd(field, amount, value)` and `eomonth(value)`, folding
`decade`/`century`/`millennium` onto a scaled `year` add; PostgreSQL renders interval arithmetic;
ClickHouse renders the dedicated `addDays`/`addMonths`/…/`addSeconds` functions (folding the three
large parts onto a scaled `addYears`) and `toLastDayOfMonth(value)`; MySQL/MariaDB render
`date_add(value, interval n unit)` and `last_day(value)`; SQLite adjusts through a `datetime`/`strftime`
modifier string. `SqlFunctions.Sql.date_diff(field, start, end)` returns the number of `<field>` boundaries
between two timestamps (SQL Server `datediff`, ClickHouse `dateDiff`, MySQL/MariaDB `timestampdiff`);
the PostgreSQL and SQLite fallbacks count date parts as boundaries and time parts as whole units.
`SqlFunctions.Sql.date_diff_big(field, start, end)` is the 64-bit variant (SQL Server `datediff_big`; the
others widen the result) for a `millisecond`/`microsecond` span that would overflow the 32-bit `date_diff`.
A sub-day `date_add`/`DateTime.Add*` promotes a `date`-only operand to a fractional timestamp (SQL Server
`datetime2`, ClickHouse `DateTime`/`DateTime64`) so the time of day is not lost.
`SqlFunctions.Sql.date_from_parts(year, month, day)` builds a date. `DateTime.AddDays`/`AddMonths`/… inside a
projection or predicate go through the same hook:

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

## SQL Server clock, offset and `*FROMPARTS`

The clock, offset and `*FROMPARTS` functions live on `SqlFunctions.SqlServer` and are gated per name by
[`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions); SQL Server is the only provider that
implements the flag, so every other provider (including the in-memory one) throws
`NotSupportedException`. `sysdatetime`/`sysutcdatetime` are non-deterministic server-local and UTC
clocks, so they must not be treated as a fixed value; `switchoffset`/`todatetimeoffset` accept the
offset either as a constant string (`"-08:00"`) or as signed minutes.

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

`timefromparts`, `datetime2fromparts` and `datetimeoffsetfromparts` require the trailing precision to
be a **constant integer in `0..7`**: a non-constant, `null` or out-of-range value throws
`NotSupportedException` before any SQL is emitted. A `null` in any other part yields a `null` result,
like the native function. `timefromparts` materialises as a `TimeSpan`, the `smalldatetime`/`datetime`/
`datetime2` builders as a `DateTime`, and the clock- and offset-returning functions as a `DateTime` /
`DateTimeOffset` matching the T-SQL result type. This is the date half of the SQL Server-only scalar
surface; the other T-SQL scalars are documented on the matching category pages.

## Date conversion and parts (ClickHouse)

ClickHouse exposes its `to*` date/time functions through `SqlFunctions.ClickHouse`
([`DateConversion`](xref:NextORM.Core.ISqlDialect.DateConversion); ClickHouse only).
`to_date`/`to_date_time`/`to_date32` convert to `Date`/`DateTime`/`Date32`;
`to_year`/`to_quarter`/`to_month`/`to_day_of_month`/`to_day_of_week`/`to_day_of_year`/`to_hour`/
`to_minute`/`to_second` return the date parts (`toDayOfWeek` is Monday 1 … Sunday 7);
`to_start_of_year`/`_quarter`/`_month`/`_week`/`_day`/`_hour`/`_minute`/`_second` truncate to the
start of the period, and `to_monday` returns the ISO Monday of the week (`toStartOfWeek` starts on
Sunday instead); `to_yyyymm`/`to_yyyymmdd` pack the date as an integer and `to_unix_timestamp` returns
Unix seconds. The `DateTime.Year`/`Month`/`Day`/`Hour`/... projections on ClickHouse use the same
`to`-accessors. The integer-returning accessors and `toYYYYMM`/`toYYYYMMDD`/`toUnixTimestamp` are
wrapped in `toInt32`/`toInt64` so the row reader can materialise them. On any other provider the whole
surface throws `NotSupportedException`.

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
