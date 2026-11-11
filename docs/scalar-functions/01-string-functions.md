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
select id
 from complex_entity where somestring like '%a%'
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
select id
 from complex_entity where somestring like '%'||$needle||'%'
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
select id
 from complex_entity where somestring collate "C" = 'x' collate "C"
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
select somestring collate "C" as "String"
 from complex_entity
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

### SQL Server 2025: `regexp_count`

`SqlFunctions.SqlServer.regexp_count(input, pattern)` returns the number of non-overlapping matches of
`pattern` in `input` as `int?` — `0` when there is no match, SQL `NULL` when the input is `NULL`:

```csharp
var counts = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Matches = SqlFunctions.SqlServer.regexp_count(e.String, "[0-9]"),
        NoMatch = SqlFunctions.SqlServer.regexp_count("abc", "[0-9]"),
        NullInput = SqlFunctions.SqlServer.regexp_count(null, "[0-9]")
    })
    .ToList();
// Matches = 3 for "a1b2c3", NoMatch = 0, NullInput = null
```

```sql
select regexp_count(somestring, N'[0-9]'), regexp_count(N'abc', N'[0-9]'),
       regexp_count(CAST(NULL AS nvarchar(4000)), N'[0-9]')
 from complex_entity
```

The function is **SQL Server 2025 (major version 17) only** and is gated by the version-only
[`SupportsSqlServer2025Functions`](xref:NextORM.Core.ISqlDialect.SupportsSqlServer2025Functions) flag.
Unlike the JSON storage gate, an engine-edition declaration never substitutes for a declared major
version: a context declared for 16, an unversioned context or an edition-only declaration refuses the
call **before SQL** with `The REGEXP_COUNT function requires SQL Server major version 17 or later;
declared major version: 16.` (or `… unspecified.` for an unset version). No database compatibility level
is required — the function works at compatibility 160 on a major-17 engine.

### SQL Server 2025: the regex scalars and `regexp_matches`

`regexp_substr` returns the first matching substring and `regexp_instr` the one-based match position
(`0` when there is no match); both take the optional native `start`, `occurrence`, `return_option`
(`regexp_instr` only) and `match_type` (`c`/`i`/`s`/`m`) arguments. An omitted optional argument and a
supplied SQL `NULL` are different call shapes — a supplied `NULL` start, occurrence or match type yields
SQL `NULL`, and a supplied `NULL` return option behaves like `0`. Invalid patterns, flags and positions
are the engine's native errors (Msg 19308/19303/19301); nextorm never validates them client-side. The
native match type is a `varchar` argument — an `nvarchar` literal or parameter is Msg 8116 — so the
renderer emits it as `cast(… as varchar(4000))`.

```csharp
var matches = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Sub = SqlFunctions.SqlServer.regexp_substr(e.String, "[0-9]+", 1, 1, "i"),
        Pos = SqlFunctions.SqlServer.regexp_instr(e.String, "[0-9]+", 1, 1, 1)
    })
    .ToList();
```

`regexp_matches(input, pattern[, match_type])` is a `FROM` source: one row per non-overlapping match with
`match_id bigint`, `start_position int`, `end_position int`, `match_value` (`varchar(n)`) and
`substring_matches` (`varchar(max)`, the engine's JSON capture text, materialized in full and never
truncated). A string without a match, or a `NULL` input, produces zero rows.

```csharp
var rows = dataContext
    .From(() => SqlFunctions.SqlServer.regexp_matches("a1b2", "[0-9]"))
    .Select(r => new { r.MatchId, r.MatchValue, r.SubstringMatches })
    .ToList();
```

All three are **SQL Server 2025 (major version 17) only** and gated like `regexp_count`; the scalars are
not compatibility-level gated and execute at 160 and 170. `regexp_matches` is different: it needs
compatibility level **170**, and at 160 the engine reports it missing (Msg 208). That prerequisite is a
runtime property the dialect cannot inspect, documented and asserted live rather than assumed. SQL
Server 2022 and earlier refuse every name before SQL.

### SQL Server 2025: fuzzy matching

`edit_distance(input, pattern)` returns the Levenshtein distance as `int?`, `edit_distance_similarity`
the similarity as `int?` in `0..100`, `jaro_winkler_distance` as `double?` (`float(53)`) and
`jaro_winkler_similarity` as `int?` in `0..100`; a `NULL` operand yields SQL `NULL`. The bounded
`edit_distance(input, pattern, maximum_distance)` returns `maximum_distance + 1` when the true distance
exceeds the cap, and a negative cap is uncapped by the engine; an omitted cap and a supplied `NULL` cap
differ (the `NULL` yields SQL `NULL`). A CLR operand wider than 32 bits for the cap is refused before
SQL rather than truncated.

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

The four names are **SQL Server 2025 (major version 17) only**, and on the tested build
(`17.0.5005.3`) they additionally require database compatibility level 170, `PREVIEW_FEATURES = ON`
(off by default) and a Windows collation — a `SQL_*` collation raises Msg 9847, and without the preview
flag the function is Msg 195. The version flag expresses only the major-version gate; the runtime
prerequisites are documented and asserted live, and no promise is made for future builds.

MariaDB string-function availability, version gates and refusals are listed in the
[MariaDB catalogue](08-reference.md#mariadb-catalogue).


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
| `SqlFunctions.Sql.octet_length(s)` | `octet_length` | `DATALENGTH` | `OCTET_LENGTH` | `toInt32(length(s))` | `octet_length` |
| `SqlFunctions.Sql.bit_length(s)` | `bit_length` | `DATALENGTH(s) * 8` | `BIT_LENGTH` | `toInt32(length(s) * 8)` | `octet_length(s) * 8` |

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
- The ClickHouse arms convert the server's `UInt64` byte count to `Int32` with `toInt32` (wrapping the
  complete `length(s) * 8` product for `bit_length`). That conversion is **not checked**: results beyond
  the `Int32` range wrap instead of throwing and are not lossless, so the declared `int?` follows
  ClickHouse conversion semantics for very large strings.

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

## ClickHouse string functions (#286, T286b)

The ClickHouse-only string surface lives on `SqlFunctions.ClickHouse`; every member renders the exact
vendor name and is rejected by every other dialect. Result shapes are explicit: `overlay`/`initcap`/
`soundex`/`substring_index`/`normalize_utf8_*` return `string` (or `string?` for a nullable argument),
`count_substrings`/`edit_distance`/`byte_hamming_distance` return `ulong`/`int`, and
`extract_groups`/`split_by_*` return `string[]`.

| Function | SQL |
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

Edge semantics are pinned live on 25.8.33.6: `overlay` is 1-based with the default length equal to the
replacement length (a zero length inserts, an offset past the end appends); `initcap` is ASCII-only;
`soundex` returns `0000` when the input has no ASCII letter; `editDistance` counts UTF-8 **bytes** and
`byteHammingDistance` zero-pads the shorter input. `base64Decode('!!!')`, a multi-character
`substringIndex` delimiter and a negative `countSubstrings` start are rejected by the server rather than
defaulted, and `extractGroups` over a nullable argument is refused because ClickHouse cannot nest
`Array(String)` inside `Nullable`.

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

### Residual string, encoding and regexp helpers (#281)

The rest of the PostgreSQL string catalogue is exposed on the same surface, one member per server
overload. Every name is gated **individually** by
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)), and a
provider that does not implement the interface rejects the call with `NotSupportedException`:

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
| `SqlFunctions.Postgres.to_ascii(value, encodingCode)` (`int`) | `to_ascii(value, encodingCode)` (`text`, `integer`) — a PostgreSQL encoding **code**: `8` = `LATIN1`, `9` = `LATIN2`, `16` = `LATIN9`, `29` = `WIN1250` |
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
| `SqlFunctions.Postgres.get_bit(bytes, index)` | `get_bit(bytes, index)` → `int` (`0` or `1`) |
| `SqlFunctions.Postgres.set_bit(bytes, index, value)` | `set_bit(bytes, index, value)` → `bytea` |
| `SqlFunctions.Postgres.get_byte(bytes, index)` | `get_byte(bytes, index)` → `int` |
| `SqlFunctions.Postgres.set_byte(bytes, index, value)` | `set_byte(bytes, index, value)` → `bytea` |
| `SqlFunctions.Postgres.bit_count(bytes)` | `bit_count(bytes)` → `bigint` |
| `SqlFunctions.Postgres.normalize(value, form)` | `normalize(value, form)` |
| `SqlFunctions.Postgres.unistr(value)` | `unistr(value)` |
| `SqlFunctions.Postgres.string_to_table(value, delimiter)` | `string_to_table(value, delimiter)` → one row per fragment |
| `SqlFunctions.Postgres.string_to_table(value, delimiter, nullString)` | `string_to_table(value, delimiter, nullString)` |
| `SqlFunctions.Postgres.regexp_match(value, pattern)` | `regexp_match(value, pattern)` → `text[]` |
| `SqlFunctions.Postgres.regexp_match(value, pattern, flags)` | `regexp_match(value, pattern, flags)` → `text[]` |

A NULL argument yields NULL for every one of them. The non-obvious semantics are:

* `regexp_match` returns NULL when the pattern does not match — not an empty array — and a
  one-element array holding the whole match when the pattern has no capturing group.
* `normalize` takes a bare grammar keyword (`NFC`, `NFD`, `NFKC` or `NFKD`), not a string literal:
  the form must be a constant and is rendered unquoted (`normalize(value, NFD)`).
* `to_hex` on a negative `int` yields the two's-complement spelling (`-1` → `ffffffff`); the `long`
  overloads are the `bigint` forms for `to_hex`/`to_oct`/`to_bin`.
* `encode`/`decode` take `(data, format)` with `base64`, `hex` or `escape`; a malformed `decode`
  input raises on the server instead of being repaired.
* `get_bit`/`set_bit` index bits from the least significant bit of the first byte, `get_byte`/
  `set_byte` index bytes from zero, and `bit_count` returns the number of set bits as `bigint`
  (`0xA1` has bit 0 set, byte 0 = `161` and three set bits).
* The two-argument `btrim`/`ltrim`/`rtrim` take a **character set** (every listed character is
  trimmed), not a substring; the `byte[]` overloads are the `bytea` forms.
* `parse_ident` returns the dot-separated parts as `text[]`, and `strict` decides whether an invalid
  identifier raises or is truncated after the first error.
* `string_to_table` is the one set-returning member of the family: one row per fragment, and the
  third argument turns a fragment equal to it into a NULL row.
* `to_ascii` is exposed with the encoding named as text (`to_ascii(value, 'LATIN1')`) and with the
  encoding as its PostgreSQL **code** (`to_ascii(value, 8)`); the one-argument form is refused (see
  below).

### Tested refusals

The pinned server is PostgreSQL 17.11. A member that cannot run on it is refused while the query is
prepared — `NotSupportedException`, before any command reaches the server:

| Family | Refused member | Tested reason |
|---|---|---|
| string | `casefold(text)` | PostgreSQL 18 function, absent from 17.11 `pg_proc`; the name fails with `42883`. |
| string | `to_ascii(text)` (one argument) | PostgreSQL 17 supports the conversion only from LATIN1, LATIN2, LATIN9 and WIN1250, and the pinned fixture database is UTF8, so the form raises `0A000`; use `to_ascii(value, encoding)` — the shipped message is `The one-argument to_ascii function is refused: PostgreSQL 17 supports the conversion only from LATIN1, LATIN2, LATIN9 and WIN1250, and the pinned fixture database is UTF8, so the form raises 0A000; use to_ascii(value, encoding).` |
| string | `crc32(bytea)` / `crc32c(bytea)` | PostgreSQL 18 functions, absent from the 17.11 `pg_proc`; the name fails with `42883`. |
| string | `quote_literal(anyelement)` | only the `text` overload is exposed; a typed overload over every CLR type would force conversions the expression paths do not support. |
| string | `quote_nullable(anyelement)` | the same `anyelement` limitation; the `text` overload is exposed. |

`unistr(text)` is delivered — 17.11 has it. Both `to_ascii` overloads are exposed: the `(text,
integer)` form takes a PostgreSQL encoding code, so `to_ascii(value, 8)` is the `LATIN1` conversion
(`SELECT pg_encoding_to_char(8)` → `LATIN1`), while a Windows codepage number such as `1252` is not an
encoding code and the emitted statement fails on the server with `42704`. The `bit`-typed spellings of
the binary helpers (`get_bit`/`set_bit`/`bit_count` over `bit`) have no CLR mapping, so no member is
offered for them and the call is refused instead of being translated.

## Full-text search (PostgreSQL, #281)

The cross-provider predicates and the earlier native surface — `contains`/`freetext`, `to_tsvector`,
`to_tsquery`, `plainto_tsquery`/`phraseto_tsquery`/`websearch_to_tsquery`, `ts_match`, `ts_rank`,
`ts_rank_cd`, `ts_headline` and `@@` — are described in
[PostgreSQL-specific SQL › Full-text search](../guide/provider-specific-sql/postgresql.md#full-text-search).
The 17 delivered names below sit on the same surface, gated by
[`SupportsTextSearchFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTextSearchFunctions), and every
other provider refuses them while the SQL is built.

**The `text` bridge.** Npgsql maps `tsvector`/`tsquery` to its own `NpgsqlTsVector`/`NpgsqlTsQuery`
types rather than to `string`, so every `tsvector` result of these members is emitted as
`cast(... as text)`. Every `tsvector`/`tsquery`/`regconfig`/`regdictionary`/`"char"` argument is cast to
its server type for the same reason: a `text` parameter matches no such overload. `to_tsvector` (and the
other earlier members) keep their bare native call, because they are consumed in argument positions
(`@@`, `ts_rank`, `ts_headline`) rather than projected as a value.

The 14 scalar names fall into three groups, in this order — vectors and queries, query inspection and
rewriting, conversion and lexing:

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

* `array_to_tsvector` assigns no positions and raises when the array holds a `NULL`; an empty array
  yields an empty `tsvector`.
* `json_to_tsvector`/`jsonb_to_tsvector` take the filter as the `jsonb` array text
  (`["string","numeric","boolean","key","all"]`); the three-argument form names the configuration
  (`regconfig`).
* `ts_delete`'s second argument is deliberately left uncast: the parameter's declared type picks the
  `ts_delete(tsvector, text)` or the `ts_delete(tsvector, text[])` overload.
* `strip` drops positions and weights, `ts_filter` keeps only the lexemes whose weight is listed, and
  `setweight` relabels every lexeme or only the listed ones — a `NULL` element, an empty array and a
  lexeme absent from the vector all leave it unchanged.
* `ts_rewrite` takes either a rewrite-table SQL query (text) or a `target`/`substitute` pair;
  `tsquery_phrase` joins two queries with `<->`, and the three-argument form sets the distance.
* `tsvector_to_array` returns the lexemes as `text[]` in stored order; `ts_lexize` returns the lexemes of
  one token as `text[]`, and a stop word yields an empty array.
* **Array arguments must come from a parameter.** `words`, `lexemes` and `weights` are CLR `string?[]`:
  pass them as `SqlFunctions.Parameter<string?[]>(idx)` (or a captured array) and never as an inline
  array literal — the emitted call always reads the values from the bound parameter, which the dialect
  casts to `text[]` or `"char"[]`.

The three tokenizer readers are `FROM` sources (`ctx.From(() => ...)`) and tokenize with the session's
default text-search configuration:

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.ts_debug(document)` | `ts_debug(@document)` | `ITsDebugRow` | `Alias string? ← alias`; `Description string? ← description`; `Token string? ← token`; `Dictionaries string[]? ← dictionaries`; `Dictionary string? ← dictionary`; `Lexemes string[]? ← lexemes` |
| `SqlFunctions.Postgres.ts_parse(parser, document)` | `ts_parse(@parser, @document)` | `ITsParseRow` | `Tokid int? ← tokid`; `Token string? ← token` |
| `SqlFunctions.Postgres.ts_token_type(parser)` | `ts_token_type(@parser)` | `ITsTokenTypeRow` | `Tokid int? ← tokid`; `Alias string? ← alias`; `Description string? ← description` |

The `oid` overloads of `ts_parse`/`ts_token_type` and `ts_debug(regconfig, text)` are **not exposed**:
both members take the parser by **name** (or run through the session configuration), PostgreSQL has no
implicit `text` → `oid` cast, and a `text` argument can therefore never reach the `oid` forms. The
`regdictionary`/`regdictionary[]` columns of `ts_debug` have no CLR mapping either, so the projection
casts them to `text[]`/`text`. `ts_debug('')` and `ts_debug(NULL)` yield no rows, `ts_token_type('default')`
yields 23 rows on PostgreSQL 17, and an unknown parser name raises `42704`.

## SQL Server string, hashing and compression functions

The remaining T-SQL-only string, hashing and compression scalars live on `SqlFunctions.SqlServer` and
are gated per name by [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions); SQL Server is the
only provider that implements the flag, so every other provider throws `NotSupportedException`:

| C# | SQL |
|---|---|
| `stuff(value, start, length, newValue)` | `stuff(...)` |
| `str(value)` / `str(value, length)` / `str(value, length, decimalPlaces)` | `str(...)` |
| `formatmessage(message, args...)` / `formatmessage(messageId, args...)` | `formatmessage(...)` |
| `parsename(objectName, piece)` | `parsename(...)` |
| `hashbytes(algorithm, data)` | `hashbytes(...)` |
| `checksum(values...)` / `binary_checksum(values...)` | `checksum(...)` / `binary_checksum(...)` |
| `compress(value)` (string and `byte[]`) / `decompress(value)` | `compress(...)` / `decompress(...)` |

* `str` fixes the total length (default 10) and decimal places (default 0); `formatmessage` accepts a
  format string or a `sys.messages` id and at most 20 formatting arguments (more throws
  `NotSupportedException`).
* `parsename` splits a four-part object name and takes a constant piece in `1..4`.
* `hashbytes` takes a constant algorithm name (such as `"SHA2_256"`) and a `byte[]` value;
  `checksum`/`binary_checksum` are non-cryptographic change-detection checksums, not a substitute for
  `hashbytes`, and require at least one argument (`checksum()`/`binary_checksum()` throw; the
  `checksum(*)` wildcard form is not exposed).
* `compress`/`decompress` use GZIP and produce/consume `varbinary(max)`; `decompress` returns `null`
  for an invalid or truncated value.
