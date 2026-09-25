# String functions

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

## Ordinal comparison and collation

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

### Column collation

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

## Regular expressions

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
| SQL Server | `regexp_like(s, 'p', 'c')` | `regexp_replace(s, 'p', 'r', 1, 0, 'c')` | the `'i'` flag |

Like the CLR method, a match is a search anywhere in the value, so `^`/`$` anchor the whole value and the
pattern is not implicitly anchored. The pattern follows the provider's **own** engine (POSIX/ARE on
PostgreSQL, ICU on MySQL, PCRE on MariaDB, RE2 on ClickHouse and SQL Server 2025, .NET on SQLite), so a
complex pattern — lookaround, backreferences, named groups — is not portable in general; the C# and SQL
escaping rules and the replacement group syntax (C# `$1` versus SQL `\1`) also differ. SQLite matches
through a CLR-backed `regexp`/`regexp_replace` function registered on every connection, so it keeps the
.NET syntax. On SQL Server the `REGEXP_*` functions are **SQL Server 2025+ only**: the match renders as
`REGEXP_LIKE`, which additionally requires database compatibility level 170, and the replace as
`REGEXP_REPLACE` (available at every compatibility level); SQL Server 2022 and earlier have neither, so
there [`SqlFunctions.Sql.like`](xref:NextORM.Core.CommonFunctions.like(System.String,System.String)) is
the fallback for simple patterns. The in-memory provider runs `System.Text.RegularExpressions` natively.

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
| `SqlFunctions.Sql.octet_length(s)` | `octet_length` | `DATALENGTH` | `OCTET_LENGTH` | `length` | `octet_length` |
| `SqlFunctions.Sql.bit_length(s)` | `bit_length` | `DATALENGTH(s) * 8` | `BIT_LENGTH` | `length(s) * 8` | `octet_length(s) * 8` |

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
- `octet_length`/`bit_length` count the value's octets, so a multi-byte SQL Server `nvarchar` value
  reports two bytes per character (via `DATALENGTH`); SQLite needs 3.43+ for its native `octet_length()`.

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
