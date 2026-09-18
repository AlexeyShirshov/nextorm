# Scalar functions

> Translate `string`, `Math` and `DateTime` members, `??` coalescing, boolean predicates and numeric
> conversions into provider-specific SQL.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md)

## Overview

nextorm recognises a fixed set of CLR members and rewrites them to SQL inside any query expression.
The dispatch lives in `BaseExpressionVisitor`: `string` methods, `Math` methods, `DateTime` members,
`NORM.SQL.like`, the `??` operator and numeric conversions. Everything provider-specific is delegated to
`ISqlDialect`, so the same C# code renders the correct function on every provider.

Two rules apply throughout:

* a **captured** value (a local or parameter) becomes a query **parameter**, not a literal;
* a **constant** is inlined. For `Contains`/`StartsWith`/`EndsWith` that also means `%`, `_` and `\` in a
  constant are escaped and an `escape '\'` clause is emitted.

The built-in translations are attempted **before** any [`[SqlFunction]`](12-user-defined-functions.md)
mapping, so a user-defined attribute cannot change the behaviour of `string`/`Math`/`DateTime` members.

## String functions

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

| C# | SQL | Notes |
|---|---|---|
| `s.ToUpper()` | `upper(s)` | |
| `s.ToLower()` | `lower(s)` | |
| `s.Trim()` | `trim(s)` | |
| `s.TrimStart()` | `ltrim(s)` | |
| `s.TrimEnd()` | `rtrim(s)` | |
| `s.Substring(start, length)` | `substring(s, start + 1, length)` | C# index is 0-based; SQL is 1-based. |
| `s.Substring(start)` | `substring(s, start + 1, length(s) - (start))` | The remaining length is derived. `Substring(Range)` is not supported. |
| `s.Length` | `length(s)` / `len(s)` | `len` on SQL Server. |
| `s.Replace(a, b)` | `replace(s, a, b)` | |
| `s.Contains(x)` | `s like '%x%'` | Constant `x` is escaped. |
| `s.StartsWith(x)` | `s like 'x%'` | |
| `s.EndsWith(x)` | `s like '%x'` | |
| `string.IsNullOrEmpty(s)` | `(s is null or s = '')` | |
| `NORM.SQL.like(s, pattern)` | `s like pattern` | Explicit `LIKE`. |
| `NORM.SQL.like(s, pattern, escape)` | `s like pattern escape escape` | |

`NORM.SQL.like` is the escape hatch when the pattern is not a simple `Contains`/`StartsWith`/`EndsWith`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.SQL.like(e.String, "%a%"))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where somestring like '%a%'
```

A runtime value in `Contains` cannot be escaped at translation time, so the wildcards are concatenated
around the parameter and the parameter-extraction pass still collects it:

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

> On SQL Server there is no boolean scalar type, so a **projected** predicate (for example
> `Select(e => e.String!.Contains("df"))`) is materialised with a `CASE`:
> `cast(case when somestring like '%df%' then 1 else 0 end as bit)`.

## String and regular-expression extensions (PostgreSQL)

Besides the portable `string` methods above, `NORM.SQL` exposes the common PostgreSQL string functions
and the POSIX regular-expression functions. They are part of the extended scalar library
(`ISqlDialect.SupportsExtendedScalarFunctions`, PostgreSQL only):

| C# | SQL |
|---|---|
| `NORM.SQL.split_part(s, delim, n)` | `split_part(s, delim, n)` |
| `NORM.SQL.strpos(s, sub)` | `strpos(s, sub)` |
| `NORM.SQL.left(s, n)` / `NORM.SQL.right(s, n)` | `left(s, n)` / `right(s, n)` |
| `NORM.SQL.lpad(s, n, fill)` / `NORM.SQL.rpad(s, n, fill)` | `lpad(s, n, fill)` / `rpad(s, n, fill)` |
| `NORM.SQL.repeat(s, n)` | `repeat(s, n)` |
| `NORM.SQL.reverse(s)` | `reverse(s)` |
| `NORM.SQL.initcap(s)` | `initcap(s)` |
| `NORM.SQL.translate(s, from, to)` | `translate(s, from, to)` |
| `NORM.SQL.overlay(s, placing, from, count)` | `overlay(s, placing, from, count)` |
| `NORM.SQL.concat_ws(sep, ...)` | `concat_ws(sep, ...)` |
| `NORM.SQL.format(fmt, ...)` | `format(fmt, ...)` |
| `NORM.SQL.md5(s)` | `md5(s)` |
| `NORM.SQL.regexp_replace(s, pattern, replacement[, flags])` | `regexp_replace(...)` |
| `NORM.SQL.regexp_like(s, pattern[, flags])` | `regexp_like(...)` |
| `NORM.SQL.regexp_split_to_array(s, pattern)` | `regexp_split_to_array(s, pattern)` |
| `NORM.SQL.regexp_count(s, pattern)` | `regexp_count(s, pattern)` |
| `NORM.SQL.regexp_instr(s, pattern)` | `regexp_instr(s, pattern)` |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Part = NORM.SQL.split_part(e.String, ",", 1),
        LooksLikeA = NORM.SQL.regexp_like(e.String, "^a")
    })
    .ToList();
```

## Math functions

| C# | SQL | Notes |
|---|---|---|
| `Math.Abs(x)` | `abs(x)` | |
| `Math.Round(x)` | `round(x)` / `round(x, 0)` | SQL Server supplies the required length argument. |
| `Math.Round(x, digits)` | `round(x, digits)` | |
| `Math.Truncate(x)` | `trunc(x)` / `round(x, 0, 1)` | SQL Server has no `trunc`. |
| `Math.Log(x)` | natural logarithm: `ln(x)` (SQLite, PostgreSQL) / `log(x)` (SQL Server) | Single-argument form only. |

```csharp
var values = dataContext.From<IComplexEntity>()
    .Select(e => Math.Abs(e.Id - 5))
    .ToList();
// ids 1, 2, 3 -> 4, 3, 2
```

```sql
select abs((id - 5)) from complex_entity
```

### PostgreSQL extended math

The remaining math functions are part of the extended scalar library
(`ISqlDialect.SupportsExtendedScalarFunctions`, PostgreSQL only):

| C# | SQL |
|---|---|
| `NORM.SQL.asin(x)` / `acos(x)` / `atan(x)` | `asin(x)` / `acos(x)` / `atan(x)` |
| `NORM.SQL.atan2(y, x)` | `atan2(y, x)` |
| `NORM.SQL.cbrt(x)` | `cbrt(x)` |
| `NORM.SQL.sinh(x)` / `cosh(x)` / `tanh(x)` | `sinh(x)` / `cosh(x)` / `tanh(x)` |
| `NORM.SQL.asinh(x)` / `acosh(x)` / `atanh(x)` | `asinh(x)` / `acosh(x)` / `atanh(x)` |
| `NORM.SQL.degrees(x)` / `NORM.SQL.radians(x)` | `degrees(x)` / `radians(x)` |
| `NORM.SQL.pi()` / `NORM.SQL.random()` | `pi()` / `random()` |
| `NORM.SQL.log(base, x)` | `log(base, x)` |
| `NORM.SQL.mod(a, b)` / `gcd(a, b)` / `lcm(a, b)` | `mod(a, b)` / `gcd(a, b)` / `lcm(a, b)` |
| `NORM.SQL.factorial(n)` | `factorial(n)` |
| `NORM.SQL.width_bucket(x, low, high, count)` | `width_bucket(x, low, high, count)` |

## Date and time

`DateTime.Now` and `DateTime.UtcNow` are rendered as SQL expressions instead of being evaluated as a
parameter. `.Year`, `.Month`, `.Day` and `.Hour` (as well as `.Minute` and `.Second`) become the
provider's date-part extraction:

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

An important detail for SQLite: `strftime` returns text, so the result is wrapped in
`cast(... as integer)` to materialise like the `int` CLR property.

### PostgreSQL extended date and time

These are part of the extended scalar library (`ISqlDialect.SupportsExtendedScalarFunctions`,
PostgreSQL only):

| C# | SQL |
|---|---|
| `NORM.SQL.age(a, b)` | `age(a, b)` |
| `NORM.SQL.date_bin(stride, source, origin)` | `date_bin(stride, source, origin)` |
| `NORM.SQL.make_date(year, month, day)` | `make_date(year, month, day)` |
| `NORM.SQL.make_interval(y, mo, d, h, mi, s)` | `make_interval(y, mo, d, h, mi, s)` |
| `NORM.SQL.justify_days(interval)` / `justify_hours(interval)` | `justify_days(interval)` / `justify_hours(interval)` |
| `NORM.SQL.to_char(value, format)` | `to_char(value, format)` |
| `NORM.SQL.to_date(text, format)` | `to_date(text, format)` |
| `NORM.SQL.to_number(text, format)` | `to_number(text, format)` |
| `NORM.SQL.to_timestamp(epoch)` / `to_timestamp(text, format)` | `to_timestamp(...)` |
| `NORM.SQL.timezone(zone, value)` | `timezone(zone, value)` |
| `NORM.SQL.extract(field, value)` | `extract(field from value)` |
| `NORM.SQL.current_date()` / `current_time()` / `localtime()` / `localtimestamp()` | the same key words |

`NORM.SQL.extract` validates the field name and accepts the PostgreSQL-specific parts in addition to
the standard ones: `quarter`, `week`, `epoch`, `dow`, `isodow`, `doy`, `isoyear`, `timezone`,
`timezone_hour`, `timezone_minute`.

## COALESCE (`??`) and CAST

`a ?? b` maps to the provider's two-argument null replacement. A numeric conversion of a numeric operand
- a C# cast such as `(double)e.Id`, or a `Convert.ToXxx(value)` call - maps to `cast(x as <type>)`:

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

Numeric cast targets come from `ISqlDialect.MakeTypeName`:

| CLR type | SQLite / PostgreSQL | SQL Server |
|---|---|---|
| `byte` | `smallint` | `tinyint` |
| `short` | `smallint` | `smallint` |
| `int` | `integer` | `int` |
| `long` | `bigint` | `bigint` |
| `float` | `real` | `real` |
| `double` | `double precision` | `float` |
| `decimal` | `numeric` | `decimal(38, 10)` |

## Arrays (PostgreSQL)

PostgreSQL has native array types. An array operand is always passed as a **single parameter** (the
whole array), never expanded into a value list, so the SQL text does not depend on the number of
elements and the plan stays cacheable. An array can be a runtime parameter (`NORM.Param<T[]>(idx)`), a
captured local/field or an inline `new[]`. Only a dialect that opts in with `ISqlDialect.SupportsArrays`
(PostgreSQL) can render the array surface; every other provider throws `NotSupportedException`.

`NORM.SQL.any` / `NORM.SQL.all` accept an array, either as a complete predicate (`column = any(@array)`)
or as the right-hand side of a comparison:

```csharp
var ids = new long[] { 1, 2, 3 };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.SQL.any(e.Id, ids))     // (id = any(@p0))
    .Select(e => new { e.Id })
    .ToList();

var same = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == NORM.SQL.any(ids))   // id = any(@p0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id from complex_entity where (id = any(@p0))
```

A runtime array parameter uses the same `NORM.Param` mechanism, so the array never has to be known when
the query is prepared:

```csharp
var prepared = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == NORM.SQL.any(NORM.Param<long[]>(0)))
    .Select(e => new { e.Id })
    .Prepare();

var rows = prepared.ToList(new long[] { 1, 2, 3 });
```

```sql
select id from complex_entity where id = any(@norm_p0)
```

The array functions and operators map to their PostgreSQL names:

| C# | SQL |
|---|---|
| `NORM.SQL.cardinality(a)` | `cardinality(a)` |
| `NORM.SQL.array_length(a, dim)` | `array_length(a, dim)` |
| `NORM.SQL.array_ndims(a)` | `array_ndims(a)` |
| `NORM.SQL.array_lower(a, dim)` | `array_lower(a, dim)` |
| `NORM.SQL.array_upper(a, dim)` | `array_upper(a, dim)` |
| `NORM.SQL.array_position(a, element)` | `array_position(a, element)` |
| `NORM.SQL.array_contains(a, b)` | `a @> b` |
| `NORM.SQL.array_contained_by(a, b)` | `a <@ b` |
| `NORM.SQL.array_overlaps(a, b)` | `a && b` |
| `NORM.SQL.array_concat(a, b)` | `a \|\| b` |
| `NORM.SQL.array_cat(a, b)` | `array_cat(a, b)` |
| `NORM.SQL.array_append(a, element)` | `array_append(a, element)` |
| `NORM.SQL.array_prepend(element, a)` | `array_prepend(element, a)` |
| `NORM.SQL.array_remove(a, element)` | `array_remove(a, element)` |
| `NORM.SQL.array_replace(a, from, to)` | `array_replace(a, from, to)` |
| `NORM.SQL.array_fill(value, dims)` | `array_fill(value, dims)` |
| `NORM.SQL.array_dims(a)` | `array_dims(a)` |
| `NORM.SQL.array_positions(a, element)` | `array_positions(a, element)` |
| `NORM.SQL.array_reverse(a)` | `array_reverse(a)` |
| `NORM.SQL.array_sort(a)` | `array_sort(a)` |
| `NORM.SQL.array_to_string(a, delimiter)` | `array_to_string(a, delimiter)` |
| `NORM.SQL.string_to_array(s, delimiter)` | `string_to_array(s, delimiter)` |

> The functions that return an array (`array_append`, `array_cat`, `array_reverse`, `string_to_array`,
> ...) are meant to be used inside a query (a predicate, `having` or a nested expression); the row reader
> cannot materialise an array column yet, so projecting one directly throws at preparation time.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.SQL.array_length(NORM.Param<long[]>(0), 1) == 3)
    .Select(e => new { N = NORM.SQL.cardinality(NORM.Param<long[]>(1)) })
    .ToList();
```

```sql
select cardinality(@norm_p1) as "N" from complex_entity where array_length(@norm_p0, 1) = 3
```

## JSON and JSONB (PostgreSQL)

PostgreSQL is the only supported provider with `json`/`jsonb` types. A JSON operand is expected to be a
`json`/`jsonb` expression: a mapped column, another JSON function, or a parameter whose runtime value is
a `JsonDocument`, `JsonElement` or `JsonNode` (Npgsql binds those as `jsonb`). A plain JSON string is
bound as `text` and can be parsed explicitly with `NORM.SQL.json_cast(value)` (`cast(value as jsonb)`).

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

var rows = dataContext.From<IComplexEntity>()
    .Where(e => NORM.SQL.json_get_text(NORM.Param<JsonDocument>(0), "name") == "Alice")
    .Select(e => new { e.Id })
    .ToList(document);
```

```sql
select id from complex_entity where ((@norm_p0 ->> 'name') = 'Alice')
```

The aggregates collapse a result set into a single JSON document:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => NORM.SQL.jsonb_agg(e.String))
    .First();

var person = dataContext.From<IComplexEntity>()
    .Select(e => NORM.SQL.jsonb_build_object("id", e.Id, "name", e.String))
    .First();
```

```sql
select jsonb_agg(somestring) from complex_entity
select jsonb_build_object('id', id, 'name', somestring) from complex_entity
```

| C# | SQL |
|---|---|
| `NORM.SQL.json_agg(x)` / `jsonb_agg(x)` | `json_agg(x)` / `jsonb_agg(x)` |
| `NORM.SQL.json_object_agg(k, v)` / `jsonb_object_agg(k, v)` | `json_object_agg(k, v)` / `jsonb_object_agg(k, v)` |
| `NORM.SQL.json_build_object("a", x, ...)` | `json_build_object('a', x, ...)` |
| `NORM.SQL.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `NORM.SQL.json_build_array(x, y)` / `jsonb_build_array(x, y)` | `json_build_array(x, y)` / `jsonb_build_array(x, y)` |
| `NORM.SQL.to_json(x)` / `to_jsonb(x)` | `to_json(x)` / `to_jsonb(x)` |
| `NORM.SQL.json_cast(x)` | `cast(x as jsonb)` |
| `NORM.SQL.json_get(json, "key")` / `json_get(json, 0)` | `json -> key` / `json -> 0` |
| `NORM.SQL.json_get_text(json, "key")` / `json_get_text(json, 0)` | `json ->> key` / `json ->> 0` |
| `NORM.SQL.json_get_path(json, path)` / `json_get_path_text(json, path)` | `json #> path` / `json #>> path` |
| `NORM.SQL.json_contains(a, b)` | `a @> b` |
| `NORM.SQL.json_exists(json, "key")` | `json ? 'key'` |
| `NORM.SQL.json_exists_any(json, keys)` / `json_exists_all(json, keys)` | `json ?\| keys` / `json ?& keys` |
| `NORM.SQL.json_array_length(json)` / `jsonb_array_length(json)` | `json_array_length(json)` / `jsonb_array_length(json)` |
| `NORM.SQL.json_typeof(json)` / `jsonb_typeof(json)` | `json_typeof(json)` / `jsonb_typeof(json)` |
| `NORM.SQL.jsonb_set(json, path, value[, create])` | `jsonb_set(...)` |
| `NORM.SQL.jsonb_insert(json, path, value[, after])` | `jsonb_insert(...)` |
| `NORM.SQL.jsonb_strip_nulls(json)` | `jsonb_strip_nulls(json)` |
| `NORM.SQL.jsonb_pretty(json)` | `jsonb_pretty(json)` |
| `NORM.SQL.jsonb_delete(json, "key")` / `jsonb_delete(json, 0)` | `json - 'key'` / `json - 0` |
| `NORM.SQL.json_concat(a, b)` | `a \|\| b` |
| `NORM.SQL.row_to_json(row)` | `row_to_json(row)` |
| `NORM.SQL.array_to_json(array)` | `array_to_json(array)` |
| `NORM.SQL.jsonb_path_exists(json, path)` | `jsonb_path_exists(json, cast(path as jsonpath))` |
| `NORM.SQL.jsonb_path_match(json, path)` | `jsonb_path_match(json, cast(path as jsonpath))` |
| `NORM.SQL.jsonb_path_query_first(json, path)` | `jsonb_path_query_first(json, cast(path as jsonpath))` |
| `NORM.SQL.jsonb_path_query_array(json, path)` | `jsonb_path_query_array(json, cast(path as jsonpath))` |

A `path`/`keys` operand is a `string[]` and is bound as a **single array parameter** (see
[Arrays](#arrays-postgresql)), so `NORM.SQL.json_get_path(json, new[] { "a", "b" })` renders
`json #> @p0`. The JSONPath functions take the path as a plain string and render it as
`cast(<path> as jsonpath)`.

## JSON as text (SQL Server)

SQL Server stores JSON in an ordinary `nvarchar` column and offers a text-oriented function subset
(`ISqlDialect.SupportsTextJson`). The path is a JSONPath string (`'$.name'`), and `json_value` returns
a scalar while `json_query` returns an object/array fragment:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Id = NORM.SQL.json_value(e.String, "$.id"),
        Name = NORM.SQL.json_query(e.String, "$.name"),
        Updated = NORM.SQL.json_modify(e.String, "$.id", "1")
    })
    .ToList();
```

```sql
select json_value(somestring, '$.id') as [Id], json_query(somestring, '$.name') as [Name], json_modify(somestring, '$.id', '1') as [Updated] from complex_entity
```

| C# | SQL |
|---|---|
| `NORM.SQL.json_value(json, path)` | `json_value(json, path)` |
| `NORM.SQL.json_query(json, path)` | `json_query(json, path)` |
| `NORM.SQL.json_modify(json, path, value)` | `json_modify(json, path, value)` |

## Conditional helpers

`NORM.SQL.nullif` is ANSI and works on every SQL provider; `greatest`/`least` are gated by
`ISqlDialect.SupportsGreatestLeast` (PostgreSQL, MySQL/MariaDB, ClickHouse and SQL Server 2022+ opt in):

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
| `NORM.SQL.num_nulls(a, b, ...)` | `num_nulls(a, b, ...)` |
| `NORM.SQL.num_nonnulls(a, b, ...)` | `num_nonnulls(a, b, ...)` |

`num_nulls`/`num_nonnulls` are part of the extended scalar library
(`ISqlDialect.SupportsExtendedScalarFunctions`).

## Date truncation (PostgreSQL, SQL Server, ClickHouse)

`NORM.SQL.date_trunc(field, value)` truncates a timestamp to a date part
(`ISqlDialect.SupportsDateTrunc`; PostgreSQL, SQL Server 2022+ and ClickHouse opt in). The field must
be a constant string from the supported set. SQL Server renders `datetrunc(part, value)`, folding the
plural ANSI parts to the singular T-SQL spellings (`milliseconds` → `millisecond`) and rejecting
`decade`/`century`/`millennium`; ClickHouse renders `dateTrunc('part', value)` with the same
singular mapping and the same rejection of the three large parts:

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

## Date arithmetic (PostgreSQL, SQL Server, ClickHouse)

`NORM.SQL.date_add(field, amount, value)` adds a number of units to a date/time and
`NORM.SQL.end_of_month(value)` returns the last day of its month (`ISqlDialect.SupportsDateArithmetic`;
PostgreSQL, SQL Server and ClickHouse opt in). The field must be a constant string from the same set as
`date_trunc`. SQL Server renders `dateadd(field, amount, value)` and `eomonth(value)`, folding
`decade`/`century`/`millennium` onto a scaled `year` add; PostgreSQL renders interval arithmetic;
ClickHouse renders the dedicated `addDays`/`addMonths`/…/`addSeconds` functions (folding the three
large parts onto a scaled `addYears`) and `toLastDayOfMonth(value)`. `DateTime.AddDays`/`AddMonths`/…
inside a projection or predicate go through the same hook:

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
```

| C# | SQL Server | PostgreSQL | ClickHouse |
|---|---|---|---|
| `NORM.SQL.date_add("day", n, x)` | `dateadd(day, n, x)` | `x + (n * interval '1 day')` | `addDays(x, n)` |
| `NORM.SQL.date_add("decade", n, x)` | `dateadd(year, (n) * 10, x)` | `x + (n * interval '10 years')` | `addYears(x, (n) * 10)` |
| `NORM.SQL.end_of_month(x)` | `eomonth(x)` | `date_trunc('month', x) + interval '1 month - 1 day'` | `toLastDayOfMonth(x)` |
| `x.AddDays(7)` | `dateadd(day, 7, x)` | `x + (7 * interval '1 day')` | `addDays(x, 7)` |
| `x.AddMonths(2)` | `dateadd(month, 2, x)` | `x + (2 * interval '1 month')` | `addMonths(x, 2)` |

## String and array aggregates (PostgreSQL, SQL Server, ClickHouse)

`NORM.SQL.string_agg` is available on PostgreSQL, SQL Server 2017+ and ClickHouse
(`ISqlDialect.SupportsStringAgg`, which defaults to the umbrella `SupportsStringArrayAggregates`);
ClickHouse renders it as `arrayStringConcat(groupArray(x), delimiter)`. `NORM.SQL.array_agg`
(`ISqlDialect.SupportsArrayAgg`) requires an array type and is therefore PostgreSQL-only. An `array_agg`
result is an array column:

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
```

| C# | SQL | Providers |
|---|---|---|
| `NORM.SQL.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` | PostgreSQL, SQL Server, ClickHouse |
| `NORM.SQL.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## Aggregate FILTER

`count`/`count_big`/`min`/`max`/`avg`/`sum` and the string/array aggregates accept an extra
`Expression<Func<bool>>` argument that renders a `filter (where ...)` clause. The filter predicate is a
full query predicate and may reference columns and parameters. The clause is gated by
`ISqlDialect.SupportsFilter` (PostgreSQL and SQLite opt in):

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

## Set-returning helpers (PostgreSQL)

`NORM.SQL.generate_series` and `NORM.SQL.unnest` are pre-declared
[`[SqlTableFunction]`](13-table-valued-functions.md) sources, so no user-defined wrapper is needed:

```csharp
var numbers = dataContext
    .FromTableFunction(() => NORM.SQL.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => NORM.SQL.unnest(NORM.Param<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`generate_series` selects `NORM.IGenerateSeriesRow.Value` and `unnest` selects
`NORM.IUnnestRow<T>.Value`; both map to the single column the function returns.

## Provider mapping table

| Feature | SQLite | SQL Server | PostgreSQL |
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
| `Log` (natural) | `ln` | `log` | `ln` |
| `Now` / `UtcNow` | `datetime('now')` / `datetime('now')` | `getdate()` / `getutcdate()` | `now()` / `now() at time zone 'utc'` |
| `Year` / `Month` / `Day` / `Hour` | `cast(strftime('%Y'...`/`'%m'`/`'%d'`/`'%H'` `as integer)` | `datepart(year, ...)` etc. | `extract(year from ...)` etc. |
| `??` coalesce | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` |
| String concatenation (`+`) | `\|\|` | `+` | `\|\|` |
| Boolean predicate as a value | unchanged | `cast(case when ... then 1 else 0 end as bit)` | unchanged |
| Arrays (`any`/`all`, array functions) | `NotSupportedException` | `NotSupportedException` | `any(@array)`, `cardinality(...)`, ... |
| JSON/JSONB (`json_agg`, `->`, ...) | `NotSupportedException` | `NotSupportedException` | supported |
| Text JSON (`json_value`, `json_query`, `json_modify`) | `NotSupportedException` | `json_value(...)`, ... | `NotSupportedException` |
| `nullif` | supported | supported | supported |
| `greatest` / `least` | `NotSupportedException` | supported (2022+) | supported |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | supported |
| `date_add` / `end_of_month` | `NotSupportedException` | `dateadd(...)` / `eomonth(...)` | interval arithmetic / `date_trunc` |
| `string_agg` / `array_agg` | `NotSupportedException` | `string_agg` (2017+); `array_agg` throws | supported |
| Aggregate `filter (where ...)` | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Extended scalar library (`asin`, `split_part`, `regexp_*`, `to_char`, `extract`, ...) | `NotSupportedException` | `NotSupportedException` | supported |
| Boolean/bitwise/statistical aggregates | `NotSupportedException` | `NotSupportedException` | supported |
| Ordered-set aggregates (`percentile_cont`, ...) | `NotSupportedException` | `NotSupportedException` | `within group (order by ...)` |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Table functions (`generate_series`, `unnest`) | — | — | `generate_series(...)`, `unnest(...)` |

The in-memory provider does not render SQL: it compiles and evaluates the expression against in-memory
rows, so the .NET method itself runs. The SQL matrix above applies to the SQLite, SQL Server and
PostgreSQL providers.

ClickHouse renders `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (and a scaled
`addYears` for `decade`/`century`/`millennium`), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax` and the `-If` combinators. It rejects the ANSI
`filter (where ...)` clause and the `regr_*`/boolean aggregates with `NotSupportedException`; see the
[ClickHouse provider](../providers/clickhouse.md).

## Explicitly unsupported

These throw `NotSupportedException` rather than emitting SQL with different semantics:

* `string.IsNullOrWhiteSpace(x)` - throws with a message mentioning `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `test/nextorm.sqlite.tests/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` - the two-argument form has a provider-specific argument order, so it is left
  unsupported (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `test/nextorm.sqlite.tests/SqlGenerationTests.cs:985`). The PostgreSQL two-argument form is available
  as `NORM.SQL.log(base, x)` in the extended scalar library instead.
* `Math.Round` overloads that take a `MidpointRounding` (more than two arguments) - not portable.
* `string.Substring(Range)` - no SQL equivalent.

## See also

* [Filtering (WHERE)](02-filtering-where.md) - `Contains`/`in`, `??` and conditional expressions in predicates.
* [Grouping and aggregates](04-grouping-and-aggregates.md) - aggregate functions (`count`, `sum`, ...).
* [User-defined functions](12-user-defined-functions.md) - when a scalar function is not built in.
* [Provider overview](../providers/overview.md) - capability flags and quoting.

---

Source: `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:541`, `:1157`, `:1204`, `:1752`;
`src/nextorm.core/Visitors/BuiltinFunctionTranslator.cs`, `src/nextorm.core/Visitors/AggregateFilter.cs`;
`src/nextorm.core/Query/NORM.cs`;
`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:57`;
`test/nextorm.integration.tests/CommonTestSuite.Functions.cs:8`, `:19`, `:43`, `:54`, `:65`, `:76`, `:87`, `:98`, `:117`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:695`, `:775`, `:801`, `:811`, `:832`, `:852`, `:861`, `:872`, `:882`, `:892`, `:912`, `:921`, `:931`, `:940`, `:950`, `:960`, `:974`, `:985`;
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:457`, `:512`, `:522`, `:533`, `:543`, `:552`, `:563`, `:573`, `:583`, `:593`, `:602`, `:613`, `:624`, `:633`, `:643`;
`test/nextorm.postgres.tests/SqlGenerationTests.cs:390`, `:445`, `:465`, `:475`, `:484`, `:495`, `:505`, `:515`, `:525`, `:534`, `:544`, `:554`, `:563`, `:573`.
