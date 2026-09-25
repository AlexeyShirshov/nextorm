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

## Runtime settings and sequences (PostgreSQL)

Also part of the extended scalar library
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions),
PostgreSQL only). The sequence name is cast to `regclass`:

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

`currval`/`lastval` are session-scoped in PostgreSQL and therefore require the same connection that
last advanced the sequence.

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

## Session and server information

`SqlFunctions.Sql.current_user()`, `session_user()`, `current_schema()`, `current_database()` and
`version()` are cross-provider
([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)):

| C# | PostgreSQL | SQL Server | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|
| `current_user()` | `current_user` | `current_user` | `current_user()` | `currentUser()` | — |
| `session_user()` | `session_user` | `session_user` | `session_user()` | — | — |
| `current_schema()` | `current_schema` | `schema_name()` | `schema()` | — | — |
| `current_database()` | `current_database()` | `db_name()` | `database()` | `currentDatabase()` | — |
| `version()` | `version()` | `@@version` | `version()` | `version()` | `sqlite_version()` |

A provider that cannot express a function throws `NotSupportedException`.

## UUID generators

`SqlFunctions.Sql.gen_random_uuid()` (random v4) and `uuidv7()` are cross-provider
([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators)):

| C# | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `gen_random_uuid()` | `gen_random_uuid()` (13+) | `newid()` | — | `UUID_v4()` | `generateUUIDv4()` | — |
| `uuidv7()` | `uuidv7()` (18+) | — | — | `UUID_v7()` (11.7+) | `generateUUIDv7()` | — |

MySQL has only `UUID()` (v1) and SQLite has no UUID generator, so both reject the calls. These are
server-side generators, evaluated per row by the database; `Guid.NewGuid()` is a client-side value and
is not a substitute.

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
select date_trunc('month', dt) as "Month" from complex_entity
-- ClickHouse
select dateTrunc('month', dt) as `Month` from complex_entity
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
select toInt32(toYear(dt)) as `Year`, toStartOfMonth(dt) as `MonthStart` from complex_entity
```
