# Scalar functions

> Translate `string`, `Math` and `DateTime` members, `??` coalescing, boolean predicates and numeric
> conversions into provider-specific SQL.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](01-querying-and-projections.md) · [Filtering (WHERE)](02-filtering-where.md)

## Overview

nextorm recognises a fixed set of CLR members and rewrites them to SQL inside any query expression.
The dispatch lives in [`BaseExpressionVisitor`](xref:NextORM.Core.BaseExpressionVisitor): `string` methods, `Math` methods, `DateTime` members,
`SqlFunctions.Sql.like`, the `??` operator and numeric conversions. Everything provider-specific is delegated to
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect), so the same C# code renders the correct function on every provider.

Two rules apply throughout:

* a **captured** value (a local or parameter) becomes a query **parameter**, not a literal;
* a **constant** is inlined. For `Contains`/`StartsWith`/`EndsWith` that also means `%`, `_` and `\` in a
  constant are escaped and an `escape '\'` clause is emitted.

The built-in translations are attempted **before** any [`[SqlFunction]`](12-user-defined-functions.md)
mapping, so a user-defined attribute cannot change the behaviour of `string`/`Math`/`DateTime` members.

Cross-provider helpers live on `` Functions that only one provider supports are grouped
under a provider-specific surface: [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (PostgreSQL: native arrays, native JSON, the extended
scalar library, the PostgreSQL-only aggregates and the `generate_series`/`unnest` table functions),
[`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) (SQL Server: the JSON-as-text functions and `string_split`/`openjson`) and
[`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (ClickHouse: `arg_min`/`arg_max`, the `-If` combinator, the string-JSON
`JSONExtract*` family, the flat-JSON `visitParamExtract*` fast path, the JSONPath scalars
`json_value`/`json_query`/`json_exists` and the dictionary functions
`dict_get`/`dict_get_or_default`/`dict_has`/`dict_get_hierarchy`/`dict_get_children`/`dict_is_in`). Calling one of them on a
provider that does not opt in throws `NotSupportedException`.

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
| `s.Remove(start, count)` | splice removing `count` characters | `stuff` on SQL Server, `insert` on MySQL/MariaDB, `overlay` on PostgreSQL, `substring` splicing elsewhere. |
| `s.Remove(start)` | splice removing everything from `start` | |
| `s.Insert(start, text)` | splice inserting `text` at `start` | |
| `s.IndexOf(x)` | zero-based position, or `-1` | `charindex`/`instr`/`strpos`/`position`. SQL is one-based and returns `0` when absent; both are adjusted. |
| `s.IndexOf(x, start)` | zero-based position at or after `start` | |
| `s.LastIndexOf(x)` | zero-based last position, or `-1` | Needs a character-wise reversal; not supported on SQLite. |
| `s.PadLeft(width[, c])` | left pad to `width`, never truncating | `replicate`/`repeat`; the SQL `lpad` family truncates, so a length guard is emitted. |
| `s.PadRight(width[, c])` | right pad to `width`, never truncating | |
| `new string(c, n)` | `replicate(c, n)` / `repeat(c, n)` | `c` must be a constant. |
| `s.Split(x)` | `string_to_array(s, x)` | PostgreSQL only; used as an array operand. |
| `string.Join(sep, s.Split(x))` | `array_to_string(string_to_array(s, x), sep)` | PostgreSQL only; requires native arrays. |
| `s.Contains(x)` | `s like '%x%'` | Constant `x` is escaped. |
| `s.StartsWith(x)` | `s like 'x%'` | |
| `s.EndsWith(x)` | `s like '%x'` | |
| `string.IsNullOrEmpty(s)` | `(s is null or s = '')` | |
| `s.ToUpperInvariant()` / `s.ToLowerInvariant()` | `upper(s)` / `lower(s)` | Same as `ToUpper`/`ToLower`; see the invariant note below. |
| `s.ToUpper(CultureInfo.InvariantCulture)` | `upper(s)` | Any other `CultureInfo` is rejected. |
| `s.Equals(t)` / `string.Equals(s, t)` | `s collate <binary> = t collate <binary>` | `string.Equals` is ordinal in C#. |
| `s.Equals(t, StringComparison.OrdinalIgnoreCase)` | case-folded ordinal equality | See [Ordinal comparison and collation](#ordinal-comparison-and-collation). |
| `string.CompareOrdinal(s, t)` / `string.Compare(s, t, StringComparison.Ordinal)` | a signed `CASE` returning `-1`/`0`/`1` | Keeps the enclosing `< 0` / `> 0` comparison. |
| `s.Contains(x, comparison)` / `StartsWith` / `EndsWith` | collation/case-folded `LIKE` | |
| `s.IndexOf(x, comparison)` / `LastIndexOf(x, comparison)` | collation/case-folded position | |
| `SqlFunctions.Sql.like(s, pattern)` | `s like pattern` | Explicit `LIKE`. |
| `SqlFunctions.Sql.like(s, pattern, escape)` | `s like pattern escape escape` | |
| `SqlFunctions.Sql.collate(s, name)` | `s collate name` | Per-expression collation. |
| `[Collation("name")]` / `.Collation("name")` | `s collate name` | Column-level collation; see [Column collation](#column-collation). |

`SqlFunctions.Sql.like` is the escape hatch when the pattern is not a simple `Contains`/`StartsWith`/`EndsWith`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Sql.like(e.String, "%a%"))
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

### Ordinal comparison and collation

C# `string` comparisons are **ordinal** (byte order), while a SQL `=`/`LIKE` uses the column's database
collation — on SQL Server the default is case-insensitive. nextorm makes the ordinal overloads explicit
and honest: `string.Equals`, `string.Compare`/`CompareOrdinal`, `Contains`/`StartsWith`/`EndsWith` and
`IndexOf`/`LastIndexOf` accept a constant `StringComparison`, and only `Ordinal` and `OrdinalIgnoreCase`
have a portable SQL form. `Ordinal` uses the provider's binary collation, `OrdinalIgnoreCase` additionally
case-folds both operands; `InvariantCulture`/`CurrentCulture` (and the culture-sensitive
`string.Compare(s, t)` overload without a comparison) throw `NotSupportedException`
([`SupportsOrdinalComparison`](xref:NextORM.Core.ISqlDialect.SupportsOrdinalComparison)).

```csharp
var strict = dataContext.From<IComplexEntity>()
    .Where(e => e.String!.Contains("X", StringComparison.Ordinal))       // case-sensitive
    .Select(e => new { e.Id })
    .ToList();

var loose = dataContext.From<IComplexEntity>()
    .Where(e => e.String!.StartsWith("x", StringComparison.OrdinalIgnoreCase))
    .Select(e => new { e.Id })
    .ToList();
```

| Provider | `Ordinal` | `OrdinalIgnoreCase` |
|---|---|---|
| PostgreSQL | `s collate "C"` | `lower(s) collate "C"` |
| SQL Server | `s collate Latin1_General_100_BIN2` | `lower(s) collate Latin1_General_100_BIN2` |
| MySQL/MariaDB | `s collate utf8mb4_bin` | `lower(s) collate utf8mb4_bin` |
| SQLite | `s collate binary` | `lower(s) collate binary` |
| ClickHouse | `s` (native byte order) | `lower(s)` |

The plain `==` operator is intentionally left as the provider's `=`; it follows the database collation,
not C#. Use `string.Equals`/`string.CompareOrdinal` when byte order is required.

SQLite's `LIKE` is always case-insensitive for ASCII regardless of the operand's collation, so the
**case-sensitive** ordinal `Contains`/`StartsWith`/`EndsWith` are rejected there
([`SupportsOrdinalLike`](xref:NextORM.Core.ISqlDialect.SupportsOrdinalLike)); `CompareOrdinal`,
`Equals` and `OrdinalIgnoreCase` work.

Apply any provider collation explicitly with `SqlFunctions.Sql.collate` (requires
[`SupportsCollation`](xref:NextORM.Core.ISqlDialect.SupportsCollation), so every SQL provider except
ClickHouse):

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

`collate` takes the provider-native collation name; it must be a constant. The in-memory provider has no
collations and treats the call as the ordinal identity.

#### Column collation

Instead of wrapping every expression, a collation can be declared on a mapped property with
[`CollationAttribute`](xref:NextORM.Core.CollationAttribute) or the fluent
[`EntityPropertyBuilder<T>.Collation`](xref:NextORM.Core.EntityPropertyBuilder`1.Collation(System.String)). It is then applied to the column in
every collation-sensitive operation (comparison, `LIKE`, `ORDER BY`, `GROUP BY`), so the column follows
the declared collation instead of the database default:

```csharp
[SqlTable("complex_entity")]
public interface IComplexEntity
{
    [Column("somestring")]
    [Collation("C")]
    string? String { get; set; }
}

// equivalent fluent mapping
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

The declared collation is the provider-native name (quoted where the provider requires it, see
[`MakeCollate`](xref:NextORM.Core.ISqlDialect.MakeCollate(System.String,System.String,NextORM.Core.KeywordCase))) and the provider must
support per-expression collation ([`SupportsCollation`](xref:NextORM.Core.ISqlDialect.SupportsCollation)):
PostgreSQL, SQL Server, MySQL/MariaDB and SQLite do, ClickHouse has no `COLLATE` and rejects a collated
column with `NotSupportedException`. The explicit ordinal overloads (`string.Equals`,
`string.CompareOrdinal`, a `StringComparison`) still override the column's collation with the binary
one. nextorm does not generate DDL, so the declaration describes an **existing** column's collation
rather than creating it; the in-memory provider ignores it (its comparisons are already ordinal).

### Regular expressions

`Regex.IsMatch(value, pattern[, options])` and `Regex.Replace(value, pattern, replacement[, options])`
translate into the provider's native regular expression. The instance forms `regex.IsMatch(value)` and
`regex.Replace(value, replacement)` read the pattern and options from a constant `Regex` (a literal, a
`new Regex(...)`, or a captured local). The provider gate is
[`SupportsRegex`](xref:NextORM.Core.ISqlDialect.SupportsRegex).

```csharp
using System.Text.RegularExpressions;

var rows = dataContext.From<IComplexEntity>()
    .Where(e => Regex.IsMatch(e.String!, "^d"))
    .Select(e => new { e.Id, Cleaned = Regex.Replace(e.String!, "[0-9]+", "#") })
    .ToList();
```

The pattern, the replacement and the options must be **compile-time constants**; a runtime pattern cannot
be compiled by the CLR into the provider's own regex dialect and throws `NotSupportedException`. Only
`RegexOptions.IgnoreCase` changes the SQL; `RegexOptions.Compiled` and `RegexOptions.CultureInvariant` are
accepted as no-ops, and any other option (for example `Multiline`) is rejected. `Regex.Replace` replaces
every match, like the CLR method.

| Provider | `Regex.IsMatch` | `Regex.Replace` | `RegexOptions.IgnoreCase` |
|---|---|---|---|
| PostgreSQL | `s ~ 'p'` | `regexp_replace(s, 'p', 'r', 'g')` | `s ~* 'p'` / `'gi'` |
| MySQL | `regexp_like(s, 'p', 'c')` | `regexp_replace(s, 'p', 'r', 1, 0, 'c')` | the `'i'` match type |
| MariaDB | `s regexp '(?-i)p'` | `regexp_replace(s, '(?-i)p', 'r')` | the `(?i)` flag |
| ClickHouse | `match(s, 'p')` | `replaceRegexpAll(s, 'p', 'r')` | the `(?i)` flag |
| SQLite | `s regexp 'p'` | `regexp_replace(s, 'p', 'r')` | the `(?i)` flag |
| SQL Server | `NotSupportedException` | `NotSupportedException` | — |

Like the CLR method, a match is a search anywhere in the value, so `^`/`$` anchor the whole value and the
pattern is not implicitly anchored. The pattern follows the provider's **own** engine (POSIX/ARE on
PostgreSQL, ICU on MySQL, PCRE on MariaDB, RE2 on ClickHouse, .NET on SQLite), so a complex pattern —
lookaround, backreferences, named groups — is not portable in general; the C# and SQL escaping rules and
the replacement group syntax (C# `$1` versus SQL `\1`) also differ. SQLite matches through a CLR-backed
`regexp`/`regexp_replace` function registered on every connection, so it keeps the .NET syntax. On SQL
Server there is no regular-expression engine, so the call is rejected: use
[`SqlFunctions.Sql.like`](xref:NextORM.Core.CommonFunctions.like(System.String,System.String)) for simple
patterns. The in-memory provider runs `System.Text.RegularExpressions` natively.

## Cross-provider scalar functions

[`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) exposes a small library of string and number
functions that render natively on every provider that can express them. Each call is gated **per
function** through [`ISqlDialect.ScalarFunctions`](xref:NextORM.Core.ISqlDialect.ScalarFunctions) and
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)): a provider without a native
form rejects the call with a clear `NotSupportedException` instead of emitting SQL it cannot run.

| C# | PostgreSQL | SQL Server | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|
| `SqlFunctions.Sql.left(s, n)` | `left` | `LEFT` | `LEFT` | `leftUTF8` | `substr(s, 1, n)` |
| `SqlFunctions.Sql.right(s, n)` | `right` | `RIGHT` | `RIGHT` | `rightUTF8` | `substr` (guarded) |
| `SqlFunctions.Sql.lpad(s, n, pad)` | `lpad` | `RIGHT(REPLICATE(...))` | `LPAD` | `leftPadUTF8` | — |
| `SqlFunctions.Sql.rpad(s, n, pad)` | `rpad` | `LEFT(s + REPLICATE(...))` | `RPAD` | `rightPadUTF8` | — |
| `SqlFunctions.Sql.repeat(s, n)` | `repeat` | `REPLICATE` | `REPEAT` | `repeat` | — |
| `SqlFunctions.Sql.reverse(s)` | `reverse` | `REVERSE` | `REVERSE` | `reverseUTF8` | — |
| `SqlFunctions.Sql.space(n)` | `repeat(' ', n)` | `SPACE` | `SPACE` | `space` | — |
| `SqlFunctions.Sql.concat_ws(sep, ...)` | `concat_ws` | `CONCAT_WS` | `CONCAT_WS` | `concatWithSeparator` | `concat_ws` |
| `SqlFunctions.Sql.translate(s, from, to)` | `translate` | `TRANSLATE` | — | `translate` | — |
| `SqlFunctions.Sql.ascii(s)` | `ascii` | `ASCII` | `ASCII` | `ascii` | `unicode` |
| `SqlFunctions.Sql.@char(n)` | `chr` | `CHAR` | `CAST(CHAR(..) AS CHAR)` | `char` | `char` |

- `left`/`right`/`lpad`/`rpad` count **characters**, not bytes (ClickHouse uses its `*UTF8` variants).
  A negative `n` is provider-specific: PostgreSQL reads it as "all but the last |n|", the others do not.
- `concat_ws` skips NULL arguments on every provider except ClickHouse, whose `concatWithSeparator`
  returns NULL when any argument is NULL.
- `lpad`/`rpad` truncate a value that is already longer than the target length, matching the SQL
  `lpad`/`rpad` family (this is why they are not the same as `string.PadLeft`/`PadRight`).
- MySQL and MariaDB have no `translate`; SQLite has no `lpad`, `rpad`, `repeat`, `reverse`, `space` or
  `translate`. Those calls are rejected on the respective provider.
- Remainder, base-10 logarithm and power stay on the portable CLR methods (`%`, `Math.Log10`,
  `Math.Pow`), which already translate per provider — there is no `SqlFunctions.Sql` spelling for them.

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

The in-memory provider evaluates the same calls through their CLR equivalents, so the same query runs
without a database.

## String and regular-expression extensions (PostgreSQL)

Besides the portable `string` and [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) methods
above, [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) exposes the PostgreSQL-only remainder of
the string library and the POSIX regular-expression functions. They are part of the extended scalar
library ([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), PostgreSQL only):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.split_part(s, delim, n)` | `split_part(s, delim, n)` |
| `SqlFunctions.Postgres.strpos(s, sub)` | `strpos(s, sub)` |
| `SqlFunctions.Postgres.initcap(s)` | `initcap(s)` |
| `SqlFunctions.Postgres.overlay(s, placing, from, count)` | `overlay(s, placing, from, count)` |
| `SqlFunctions.Postgres.format(fmt, ...)` | `format(fmt, ...)` |
| `SqlFunctions.Postgres.md5(s)` | `md5(s)` |
| `SqlFunctions.Postgres.digest(s\|bytes, type)` | `digest(data, type)` (requires the `pgcrypto` extension) |
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

## Math functions

| C# | SQL | Notes |
|---|---|---|
| `Math.Abs(x)` | `abs(x)` | |
| `Math.Round(x)` | `round(x)` / `round(x, 0)` | SQL Server supplies the required length argument. |
| `Math.Round(x, digits)` | `round(x, digits)` | PostgreSQL casts a `double`/`float` first argument to `numeric` (`round((x)::numeric, digits)`), because it has no `round(double precision, integer)`. |
| `Math.Truncate(x)` | `trunc(x)` / `round(x, 0, 1)` | SQL Server has no `trunc`. |
| `Math.Log(x)` | natural logarithm: `ln(x)` (SQLite, PostgreSQL) / `log(x)` (SQL Server) | Single-argument form only. |
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

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Abs |
|-----|
| 4   |
| 3   |
| 2   |

### PostgreSQL extended math

The remaining math functions are part of the extended scalar library
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), PostgreSQL only):

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

`SqlFunctions.Postgres.setseed(seed)` renders `setseed(seed)` and is gated separately by
[`SupportsRandomSeed`](xref:NextORM.Core.ISqlDialect.SupportsRandomSeed) (PostgreSQL only). The
PostgreSQL function returns `void`, so a projected value is always `null` and the call is made for its
side effect (subsequent `random()` calls in the session become reproducible).

## Date and time

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

### Extracting arbitrary date parts

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

### PostgreSQL extended date and time

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

### Runtime settings and sequences (PostgreSQL)

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

### Formatting dates and numbers to strings

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

### Session and server information

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

### UUID generators

`SqlFunctions.Sql.gen_random_uuid()` (random v4) and `uuidv7()` are cross-provider
([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators)):

| C# | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `gen_random_uuid()` | `gen_random_uuid()` (13+) | `newid()` | — | `UUID_v4()` | `generateUUIDv4()` | — |
| `uuidv7()` | `uuidv7()` (18+) | — | — | `UUID_v7()` (11.7+) | `generateUUIDv7()` | — |

MySQL has only `UUID()` (v1) and SQLite has no UUID generator, so both reject the calls. These are
server-side generators, evaluated per row by the database; `Guid.NewGuid()` is a client-side value and
is not a substitute.

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

Numeric cast targets come from [`MakeTypeName`](xref:NextORM.Core.ISqlDialect.MakeTypeName(System.Type)):

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
elements and the plan stays cacheable. An array can be a runtime parameter ([`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32))), a
captured local/field or an inline `new[]`. Only a dialect that opts in with [`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays)
(PostgreSQL) can render the array surface; every other provider throws `NotSupportedException`.

`SqlFunctions.Postgres.any` / `SqlFunctions.Postgres.all` accept an array, either as a complete predicate (`column = any(@array)`)
or as the right-hand side of a comparison:

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

A runtime array parameter uses the same [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) mechanism, so the array never has to be known when
the query is prepared:

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

The array functions and operators map to their PostgreSQL names:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.cardinality(a)` | `cardinality(a)` |
| `SqlFunctions.Postgres.array_length(a, dim)` | `array_length(a, dim)` |
| `SqlFunctions.Postgres.array_ndims(a)` | `array_ndims(a)` |
| `SqlFunctions.Postgres.array_lower(a, dim)` | `array_lower(a, dim)` |
| `SqlFunctions.Postgres.array_upper(a, dim)` | `array_upper(a, dim)` |
| `SqlFunctions.Postgres.array_position(a, element)` | `array_position(a, element)` |
| `SqlFunctions.Postgres.array_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.array_contained_by(a, b)` | `a <@ b` |
| `SqlFunctions.Postgres.array_overlaps(a, b)` | `a && b` |
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

> The functions that return an array (`array_append`, `array_cat`, `array_reverse`, `string_to_array`,
> ...) can be used inside a query (a predicate, `having` or a nested expression) or projected directly:
> the row reader materialises an `Array(T)` result as a CLR `T[]`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.array_length(SqlFunctions.Parameter<long[]>(0), 1) == 3)
    .Select(e => new { N = SqlFunctions.Postgres.cardinality(SqlFunctions.Parameter<long[]>(1)) })
    .ToList();
```

```sql
select cardinality(@norm_p1) as "N" from complex_entity where array_length(@norm_p0, 1) = 3
```

## Arrays (ClickHouse)

ClickHouse has a native `Array(T)` type. The array functions operate on array **columns** (or on nested
array expressions) and are gated by `ISqlDialect.SupportsArrayFunctions`; `arrayJoin` additionally
requires `SupportsArrayJoin`. `arrayJoin(array)` expands the array into one row per element, so its
result can be projected like a scalar column.

| Function | SQL |
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

> `length`/`indexOf` return `UInt64` natively, so the dialect casts them with `toInt64(...)`. Functions
> that return an array (`split_by_char`, `array_sort`, `array_reverse`, `array_distinct`, `range`,
> `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`, `group_array`,
> `group_uniq_array`) can be projected directly — the row reader materialises an `Array(T)` result as a
> CLR `T[]` — or used as the operand of another array function (for example `length(...)` or
> `array_string_concat(...)`). The same reader materialises a native `Tuple(...)` column (or a
> `Tuple(...)`-returning expression) as a `System.Tuple<...>` of arity 1–7.

> The array relation predicates return `bool`: `starts_with(array, prefix)`/`ends_with(array, suffix)`
> test a prefix/suffix and `has_substr(array, other)` tests that `other` occurs in `array` contiguously
> and in order (an empty `other` is always contained). They require a provider that supports the array
> functions.

> The row-value (tuple) surface is cross-provider and built from `System.Tuple.Create` /
> `new Tuple<...>` / `System.Tuple<...>.ItemN`: a constructor renders through the dialect's row
> constructor — `tuple(a, b)` on ClickHouse, `ROW(a, b)` on PostgreSQL — and access to an element of a
> *server-side* row renders through the dialect's positional access (`tupleElement(pair, 1)` on
> ClickHouse, `(pair).f1` on PostgreSQL). Access to an element of an *inline* constructor
> (`Tuple.Create(a, b).Item1`, `new ValueTuple<...>(a, b).Item2`) folds to the argument, so it works on
> every dialect that can express the constructor. `System.Tuple<,> ==` (reference equality in C#) is
> reinterpreted as a SQL row-value comparison (`Tuple.Create(x.A, x.B) == Tuple.Create(1, 'a')` →
> `ROW(a, b) = ROW(1, 'a')`); `ValueTuple` `==` cannot appear in an expression tree. Requires a provider
> with a native row type (see [`ITupleRenderer`](xref:NextORM.Core.ITupleRenderer) /
> [`ISqlDialect.Tuple`](xref:NextORM.Core.ISqlDialect.Tuple); PostgreSQL and ClickHouse); SQL Server,
> MySQL, MariaDB, SQLite and the in-memory provider reject the surface, and tuple `IN`/`Contains` over a
> value list is not translated yet. `untuple` is not supported because it changes the result column set
> rather than producing a scalar.

> The higher-order (lambda) functions take an inline C# lambda whose parameter is the array element,
> for example `array_map(v => -v, e.Nums)` renders `arrayMap(v -> -(v), nums)`. `array_exists`/`array_all`
> return `bool`; `array_count`/`array_first_index`/`array_last_index` return `long` (the dialect casts
> the native `UInt32` with `toInt64(...)`); `array_first`/`array_last` return the element or its default
> value when nothing matches. They require a provider that supports the higher-order array functions
> (see [`SupportsHigherOrderArrayFunctions`](xref:NextORM.Core.ISqlDialect.SupportsHigherOrderArrayFunctions);
> ClickHouse). ClickHouse promotes the arithmetic result type independently of C# (an `Int32` element
> multiplied by an integer literal becomes `Array(Int64)`), so cast inside the lambda
> (`v => (long)v * 2`) when the element type must match the projected `T[]`.

The CLR `string.Split` is rendered as `splitByChar(separator, value)` (gated by
[`StringSplit`](xref:NextORM.Core.ISqlDialect.StringSplit)); only a single-character
separator is supported (the multi-character `splitByString` is not exposed), the result is a `string[]`
that can be projected directly or used inside another array function, and the `count` overload, multiple separators and
`StringSplitOptions` other than `None` throw `NotSupportedException`:

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

`EntityBuilder.ArrayJoin`/`LeftArrayJoin` render the `[LEFT] ARRAY JOIN` clause, which expands the rows
before `WHERE`/`GROUP BY`; `LEFT ARRAY JOIN` keeps a row whose array is empty. The expanded element is
not bound to a CLR member, so use the scalar `array_join` above when the value must be projected or
filtered.

```csharp
var ids = dataContext.From<IArrayEntity>()
    .LeftArrayJoin(e => e.Tags)
    .Select(e => e.Id)
    .ToList();
```

```sql
select id from array_entity left array join tags
```

`EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement` add the same clause but return
`EntityBuilder<ArrayJoinProjection<TEntity, TElement>>`, so both the original entity (`p.Item1`) and the
expanded element (`p.Element`) can be referenced. The clause expression is aliased and `p.Element`
translates to that alias:

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

A bound array join is only supported on a single, un-joined source; `Where`/`Having` must be applied
after it (their parameter type is the array-join projection). Use `ArrayJoin`/`LeftArrayJoin` with the
scalar `array_join` when you need multiple arrays or a joined query.

## JSON and JSONB (PostgreSQL)

PostgreSQL is the only supported provider with `json`/`jsonb` types. A JSON operand is expected to be a
`json`/`jsonb` expression: a mapped column, another JSON function, or a parameter whose runtime value is
a `JsonDocument`, `JsonElement` or `JsonNode` (Npgsql binds those as `jsonb`). A plain JSON string is
bound as `text` and can be parsed explicitly with `SqlFunctions.Postgres.json_cast(value)` (`cast(value as jsonb)`).

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

The aggregates collapse a result set into a single JSON document:

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

A `path`/`keys` operand is a `string[]` and is bound as a **single array parameter** (see
[Arrays](#arrays-postgresql)), so `SqlFunctions.Postgres.json_get_path(json, new[] { "a", "b" })` renders
`json #> @p0`. The JSONPath functions take the path as a plain string and render it as
`cast(<path> as jsonpath)`.

## JSON as text (SQL Server, MySQL/MariaDB)

SQL Server stores JSON in an ordinary `nvarchar` column and offers a text-oriented function subset
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)); MySQL/MariaDB expose the same
surface over the `JSON_EXTRACT`/`JSON_SET` family. The path is a JSONPath string (`'$.name'`), and `json_value` returns
a scalar while `json_query` returns an object/array fragment:

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

On MySQL/MariaDB the same calls render as `json_unquote(json_extract(...))`, `json_extract(...)`,
`json_set(...)` and `json_valid(...)`.

`SqlFunctions.SqlServer.isjson` returns a boolean: in a predicate it renders `(isjson(x)) = 1` (T-SQL `ISJSON`
returns an `int`) and as a projected value it is cast to `bit`. `isjson` is also used directly in a
`WHERE` (`Where(e => SqlFunctions.SqlServer.isjson(e.String))`).

## XML data-type methods (SQL Server)

SQL Server's `xml` type exposes postfix methods
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)). They are
called through `SqlFunctions.SqlServer` and render `xmlcol.method(...)`; the XQuery and the SQL type
must be string literals (both are emitted verbatim, with embedded single quotes escaped):

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
| `SqlFunctions.SqlServer.xml_nodes(xml, xpath)` | `xml.nodes('xpath') as [alias]([value])` (APPLY source) |

`xml_exist` returns `bit`: in a predicate it renders `(xml.exist('xpath')) = 1`, as a projected value it
stays a bit.

The rowset method `.nodes` is a correlated source, not a scalar: use it as the source of
`CrossApply`/`OuterApply` and project the unfolded `IXmlNodesRow.Value` with the scalar methods above.
It unfolds the XML value into one row per node selected by the XQuery and renders
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

The operand must be a column of the outer row and the XQuery a string literal; every other provider
rejects `xml_nodes` with a `NotSupportedException`, as does the in-memory provider.

## Conditional helpers

`SqlFunctions.Sql.nullif` is ANSI and works on every SQL provider; `greatest`/`least` are gated by
[`SupportsGreatestLeast`](xref:NextORM.Core.ISqlDialect.SupportsGreatestLeast) (PostgreSQL, MySQL/MariaDB, ClickHouse, SQL Server 2022+ and SQLite opt in; SQLite renders `max`/`min`). NULL handling is provider-specific: PostgreSQL, SQL Server 2022+ and ClickHouse 24.12+ ignore NULL arguments and return NULL only when every argument is NULL, while MySQL/MariaDB and SQLite return NULL when any argument is NULL:

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
| `SqlFunctions.Postgres.num_nonnulls(a, b, ...)` | `num_nonnulls(a, b, ...)` |
| `SqlFunctions.Sql.iif(condition, a, b)` | `iif(...)` (SQL Server, SQLite 3.32+), `if(...)` (MySQL/MariaDB, ClickHouse), `case when ... then ... else ... end` (PostgreSQL) |
| `SqlFunctions.SqlServer.choose(index, a, b, ...)` | `choose(index, a, b, ...)` (SQL Server) |
| `SqlFunctions.ClickHouse.multi_if(when(c1, v1), ..., otherwise(v))` | `multiIf(c1, v1, ..., v)` (ClickHouse) |

`num_nulls`/`num_nonnulls` are part of the extended scalar library
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions)).
`iif` is portable ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif)) and each dialect supplies its native
spelling through [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String)); `choose` remains SQL Server-only
([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)). Calling `iif` through the specialized
`SqlFunctions.SqlServer` surface still works by inheritance. The C# ternary `condition ? a : b` is separate
and always renders the portable `case when ... end`.

ClickHouse additionally has the multi-branch `multiIf` surface
([`MultiIf`](xref:NextORM.Core.ISqlDialect.MultiIf),
[`IMultiIfRenderer.Render`](xref:NextORM.Core.IMultiIfRenderer.Render(System.Collections.Generic.IReadOnlyList{System.String},System.Type))): build each branch with `when(condition, value)`
and close it with `otherwise(value)`, which must be last. Other providers just use `case when`, which is
already the portable form behind `iif`/the C# conditional, so they reject the ClickHouse-native spelling.

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

## String and array aggregates

`SqlFunctions.Sql.string_agg` is available on PostgreSQL, SQL Server 2017+, ClickHouse, MySQL/MariaDB and SQLite
([`SupportsStringAgg`](xref:NextORM.Core.ISqlDialect.SupportsStringAgg), which defaults to the umbrella [`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates));
ClickHouse renders it as `arrayStringConcat(groupArray(x), delimiter)`, MySQL/MariaDB as
`group_concat(x separator delimiter)` and SQLite as `group_concat(x, delimiter)`. `SqlFunctions.Postgres.array_agg`
([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg)) requires an array type and is therefore PostgreSQL-only. An `array_agg`
result is an array column:

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

| C# | SQL | Providers |
|---|---|---|
| `SqlFunctions.Sql.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` / `group_concat(x separator delimiter)` / `group_concat(x, delimiter)` | PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB, SQLite |
| `SqlFunctions.Postgres.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## Aggregate FILTER

`count`/`count_big`/`min`/`max`/`avg`/`sum` and the string/array aggregates accept an extra
`Expression<Func<bool>>` argument that filters the rows the aggregate sees. The filter predicate is a
full query predicate and may reference columns and parameters. The spelling is selected by
[`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle) — PostgreSQL and SQLite
render the ANSI `filter (where ...)` clause, ClickHouse renders its `-If` combinator
(`countIf`/`sumIf`/...) and MySQL/MariaDB and SQL Server reject the call:

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

On ClickHouse the same query renders `toInt32(countIf((id > 10)))` and `sumIf(id, (b = true))`.

## Set-returning helpers (PostgreSQL)

`SqlFunctions.Postgres.generate_series` and `SqlFunctions.Postgres.unnest` are pre-declared
[`[SqlTableFunction]`](13-table-valued-functions.md) sources, so no user-defined wrapper is needed:

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

`generate_series` selects [`Value`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow.Value) and `unnest` selects
[`Value`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1.Value); both map to the single column the function returns.

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
| `string.Format` / `ToString(format)` | `printf` / `strftime` | `format` | `to_char` |
| Ordinal `Equals` / `CompareOrdinal` | `collate binary` | `collate Latin1_General_100_BIN2` | `collate "C"` |
| `collate(s, name)` | `s collate name` | `s collate name` | `s collate "name"` |
| `[Collation]` column | `s collate name` | `s collate name` | `s collate "name"` |
| Regex (`IsMatch` / `Replace`) | `s regexp ...` / `regexp_replace(...)` | `NotSupportedException` | `s ~ ...` / `regexp_replace(...)` |
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
| Text JSON (`json_value`, `json_query`, `json_modify`, `isjson`) | `NotSupportedException` | `json_value(...)`, ..., `isjson(...)` | `NotSupportedException` |
| `nullif` | supported | supported | supported |
| `greatest` / `least` | `max(...)` / `min(...)` (single argument -> `(...)`) | supported (2022+) | supported |
| `iif` | `iif(cond, a, b)` (3.32+) | `iif(cond, a, b)` | `case when cond then a else b end` |
| `multi_if` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (ClickHouse-only; `multiIf`) |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | supported |
| `date_add` / `end_of_month` / `date_diff` / `date_from_parts` | `datetime(x, n \|\| ' days')` / `date(x, 'start of month', ...)` / `strftime` difference / `date(printf(...))` | `dateadd(...)` / `eomonth(...)` / `datediff(...)` / `datefromparts(...)` | interval arithmetic / `date_trunc` / date-part difference / `make_date` |
| `string_agg` / `array_agg` | `group_concat(x, delimiter)` (no `array_agg`) | `string_agg` (2017+); `array_agg` throws | supported |
| Aggregate `filter (where ...)` | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Extended scalar library (`asin`, `split_part`, `regexp_*`, `to_char`, ...) | `NotSupportedException` | `NotSupportedException` | supported |
| Boolean/bitwise/statistical aggregates | `NotSupportedException` | `NotSupportedException` | supported |
| Ordered-set aggregates (`percentile_cont`, ...) | `NotSupportedException` | window `percentile_cont(f) within group (order by x) over (...)` | `within group (order by ...)` |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Built-in table functions | `NotSupportedException` | `string_split(...)`, `openjson(...)` | `generate_series(...)`, `unnest(...)` |

The in-memory provider does not render SQL: it compiles and evaluates the expression against in-memory
rows, so the .NET method itself runs. The SQL matrix above applies to the SQLite, SQL Server and
PostgreSQL providers.

ClickHouse renders `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (and a scaled
`addYears` for `decade`/`century`/`millennium`), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax`, the `-If` combinators and `multiIf`. It rejects the ANSI
`filter (where ...)` clause and the `regr_*`/boolean aggregates with `NotSupportedException`; see the
[ClickHouse provider](../providers/clickhouse.md).

## Explicitly unsupported

These throw `NotSupportedException` rather than emitting SQL with different semantics:

* `string.IsNullOrWhiteSpace(x)` - throws with a message mentioning `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` - the two-argument form has a provider-specific argument order, so it is left
  unsupported (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:985`). The PostgreSQL two-argument form is available
  as `SqlFunctions.Postgres.log(base, x)` in the extended scalar library instead.
* `Math.Round` overloads that take a `MidpointRounding` (more than two arguments) - not portable.
* `string.Substring(Range)` - no SQL equivalent.
* `string.Trim(c)`/`TrimStart(c)`/`TrimEnd(c)` - SQL trims whitespace only, not an arbitrary character
  set.
* `string.ToUpper(CultureInfo)`/`ToLower(CultureInfo)` with a culture other than
  `CultureInfo.InvariantCulture` - only the invariant upper/lower has a portable form.
* `string.Compare(a, b)` and `string.Compare(a, b, bool)` without a `StringComparison` - culture-sensitive;
  use `string.Compare(a, b, StringComparison.Ordinal)` or `CompareOrdinal`.
* `StringComparison.InvariantCulture`/`CurrentCulture` (with or without `IgnoreCase`) - no portable form.
* `string.Format`/`ToString(format)` specifiers outside the documented subset in
  [Formatting dates and numbers to strings](#formatting-dates-and-numbers-to-strings) - never dropped.
* `Regex` on SQL Server - there is no regular-expression engine; use `SqlFunctions.Sql.like` for simple
  patterns (see [Regular expressions](#regular-expressions)).
* A `Regex` pattern, replacement or `RegexOptions` that is not a compile-time constant, a `RegexOptions`
  other than `IgnoreCase` (with `Compiled`/`CultureInvariant` as no-ops), and the `Regex` members other
  than `IsMatch`/`Replace` (for example `Match`, `Split`, capture groups).

## See also

* [Filtering (WHERE)](02-filtering-where.md) - `Contains`/`in`, `??` and conditional expressions in predicates.
* [Grouping and aggregates](04-grouping-and-aggregates.md) - aggregate functions (`count`, `sum`, ...).
* [User-defined functions](12-user-defined-functions.md) - when a scalar function is not built in.
* [Provider overview](../providers/overview.md) - capability flags and quoting.

---

Source: `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:541`, `:1157`, `:1204`, `:1752`;
`src/nextorm.core/Visitors/BuiltinFunctionTranslator.cs`, `src/nextorm.core/Visitors/AggregateFilter.cs`;
`src/nextorm.core/Query/SqlFunctions.cs`;
`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:57`;
`tests/nextorm.integration.tests/CommonTestSuite.Functions.cs:8`, `:19`, `:43`, `:54`, `:65`, `:76`, `:87`, `:98`, `:117`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:695`, `:775`, `:801`, `:811`, `:832`, `:852`, `:861`, `:872`, `:882`, `:892`, `:912`, `:921`, `:931`, `:940`, `:950`, `:960`, `:974`, `:985`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:457`, `:512`, `:522`, `:533`, `:543`, `:552`, `:563`, `:573`, `:583`, `:593`, `:602`, `:613`, `:624`, `:633`, `:643`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:390`, `:445`, `:465`, `:475`, `:484`, `:495`, `:505`, `:515`, `:525`, `:534`, `:544`, `:554`, `:563`, `:573`.
