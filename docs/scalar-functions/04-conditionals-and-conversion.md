# COALESCE (`??`) and CAST

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
select ifnull(somestring, '')
 from complex_entity
select (cast(id as double precision) / 2)
 from complex_entity

-- SQL Server
select isnull(somestring, '')
 from complex_entity
select (cast(id as float) / 2)
 from complex_entity

-- PostgreSQL
select coalesce(somestring, '')
 from complex_entity
select (cast(id as double precision) / 2)
 from complex_entity
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
select nullif(nullableint, 0) as "NoZero", greatest(id, 10) as "Hi", least(id, 10) as "Lo"
 from complex_entity
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
select id, multiIf((id = 1), 'one', (id = 2), 'two', 'many') as `Bucket`
 from complex_entity
```

## Null-safe comparison (`IS [NOT] DISTINCT FROM`)

`SqlFunctions.Sql.is_not_distinct_from` and `SqlFunctions.Sql.is_distinct_from` are the explicit,
opt-in null-safe comparison. Both take one common operand type `T` and return a non-nullable `bool`:

```csharp
bool is_not_distinct_from<T>(T left, T right);
bool is_distinct_from<T>(T left, T right);
```

The result is always two-valued — it is never SQL `NULL`/UNKNOWN, so it cannot drop a row the way an
ordinary comparison does:

| `left` | `right` | `is_not_distinct_from` | `is_distinct_from` |
|---|---|---|---|
| `1` | `1` | `true` | `false` |
| `1` | `2` | `false` | `true` |
| `NULL` | `NULL` | `true` | `false` |
| `NULL` | `1` | `false` | `true` |
| `1` | `NULL` | `false` | `true` |

`is_distinct_from` is the complement of `is_not_distinct_from`. Ordinary `==` / `!=` are **unchanged**:
they keep SQL three-valued logic and are never rewritten to this helper (a `NULL` comparison yields no
row in `WHERE`). Use the helpers when "both are `NULL`" must count as equal.

```csharp
int? previous = 42;

var rows = await dataContext.From<ComplexEntity>()
    .Where(e => SqlFunctions.Sql.is_not_distinct_from(e.Int, previous))
    .Select(e => new { e.Id })
    .ToListAsync();
```

| Provider | `is_not_distinct_from(a, b)` | `is_distinct_from(a, b)` | Disposition |
|---|---|---|---|
| PostgreSQL | `(a is not distinct from b)` | `(a is distinct from b)` | native |
| SQLite | `(a is b)` | `(a is not b)` | native |
| MySQL, MariaDB | `(a <=> b)` | `(not (a <=> b))` | native |
| SQL Server | `((a is not null and b is not null and a = b) or (a is null and b is null))` | `(not ((a is not null and b is not null and a = b) or (a is null and b is null)))` | emulated |
| ClickHouse | `((a is null and b is null) or (a is not null and b is not null and a = b))` | `(not ((a is null and b is null) or (a is not null and b is not null and a = b)))` | emulated |

A boolean projection is shaped by the dialect: SQL Server legalises the predicate as
`cast(case when ... then 1 else 0 end as bit)`, while the other providers project it directly (on
ClickHouse the guarded predicate is already a `UInt8` value).

Folding, typing and parameters behave deterministically:

* two literal `NULL` operands fold to `1 = 1` (`is_not_distinct_from`) or `1 = 0` (`is_distinct_from`);
* exactly one literal `NULL` lowers to `is null` / `is not null` on the other operand;
* a **captured** operand stays one command parameter, and its SQL type comes from the compared column's
  mapping (or its converter), never from the runtime value — a captured `NULL` works and never
  specializes the cached SQL;
* two statically non-nullable CLR **value** operands that read no query source (two captured `int`
  locals, for example) may lower to `=` / `<>`; a nullable value, a column, a navigation or an
  outer-join null-extended operand always keeps the null-safe rendering. A nullable reference
  annotation is **not** runtime proof: a reference declared non-nullable that actually holds `NULL`
  still takes the null-safe path.

The single operand type `T` is inferred when both arguments already share it. A bare `null` literal has
no type of its own, so name it explicitly — `is_not_distinct_from<int?>(null, null)` — or cast it
(`(int?)null`). Operands whose effective CLR types differ (after nullable unwrapping) are rejected with
`NotSupportedException` and `Null-safe comparison requires compatible operand types.` instead of being
silently converted. On SQL Server the SQL type of a null operand must be resolvable from metadata (the
bound column/converter mapping, then the operand's CLR type); an unmapped operand fails before execution
with `Null-safe comparison requires a SQL Server type mapping for null operands.`

The in-memory provider applies the same truth table — both `NULL` equal, exactly one `NULL` unequal,
otherwise `EqualityComparer<T>.Default` — so a `NULL` reference is never dereferenced. CLR string
equality is **not** database collation: the in-memory provider compares CLR strings, while a SQL
provider compares with the column's collation, so no cross-collation equality is promised.

## BASE64 and decimal parsing

`SqlFunctions.Sql.base64_encode(byte[]?)` and `base64_decode(string?)` render standard RFC 4648 BASE64
(`+`, `/`, `=` padding, no line wrapping):

| C# | SQL Server 2025 | PostgreSQL | MySQL / MariaDB |
|---|---|---|---|---|
| `base64_encode(value)` | `BASE64_ENCODE(value)` | `replace(encode(value, 'base64'), E'\n', '')` | `CAST(REPLACE(REPLACE(TO_BASE64(value), CHAR(13), ''), CHAR(10), '') AS CHAR)` |
| `base64_decode(value)` | `BASE64_DECODE(value)` | `decode(value, 'base64')` | `FROM_BASE64(value)` |

A SQL NULL (literal, parameter or nullable column) yields SQL NULL; an empty input yields an empty
result. Invalid or URL-safe BASE64 is outside the portable input domain and the provider error is not
masked. ClickHouse, SQLite and the in-memory provider reject both members with
`The {name} function is not supported by this provider.` ClickHouse has native `base64Encode`/
`base64Decode`, but nextorm does not expose them in this slice: the adapter binds a `byte[]` argument
as `Array(UInt8)` and reads a `String` result as `string`, so the `byte[]` signature cannot be honoured.
On SQL Server `base64_decode` takes `varchar` input, so an `nvarchar` column needs an explicit cast.

`SqlFunctions.SqlServer.parse(string?)` and `try_parse(string?)` parse a `decimal(38,18)` with the
fixed `en-US` culture — never the session language and never a culture argument. `parse` raises the
server error for invalid text (`Microsoft.Data.SqlClient.SqlException`); `try_parse` returns NULL.
Both return `decimal?`, and invalid text never falls back to client-side parsing.

## SQL Server TRY_CAST and TRY_CONVERT

`SqlFunctions.SqlServer.try_cast<T>(value)` and `try_convert<T>(value[, style])` render the T-SQL
`TRY_CAST`/`TRY_CONVERT`: a malformed, overflowing or out-of-range value yields SQL NULL instead of an
error. The overloads are separate per target shape:

| C# | SQL |
|---|---|
| `try_cast<T>(value)` | `try_cast(value as <type>)` |
| `try_cast(value)` / `try_convert(value)` | `try_cast(value as nvarchar(4000))` / `try_convert(nvarchar(4000), value)` |
| `try_convert<T>(value)` | `try_convert(<type>, value)` |
| `try_convert<T>(value, style)` / `try_convert(value, style)` | `try_convert(<type>, value, style)` |

The destination is a **closed map from the CLR target `T`**, never a CLR type name:

| CLR target | SQL Server type |
|---|---|
| `byte` / `short` / `int` / `long` | `tinyint` / `smallint` / `int` / `bigint` |
| `float` / `double` | `real` / `float(53)` |
| `decimal` | `decimal(38,10)` |
| `bool` | `bit` |
| `Guid` | `uniqueidentifier` |
| `DateTime` / `DateTimeOffset` | `datetime2(7)` / `datetimeoffset(7)` |
| `TimeSpan` | `time(7)` |
| `string` (the non-generic overloads) | `nvarchar(4000)` |

A target outside the map is refused before the command is prepared: `The TRY_CAST function cannot map CLR
target 'MyType' to a supported SQL Server conversion type.` Lengths and precisions are fixed — there is no
length or precision argument yet — and the server applies its native truncation: a value longer than 4000
characters converts to exactly 4000 characters (`try_cast(<5000 chars> as nvarchar(4000))` is 4000
characters / 8000 bytes), not an error.

The style argument is the native `datetime`/`float`/money style, not a target selector. Omitting it and
passing a NULL style are **different overloads**: the NULL style is emitted as `CAST(NULL AS int)` and SQL
Server then returns NULL **regardless of the value** — `try_convert(int, '42', CAST(NULL AS int))` is NULL,
not `42`.

Invalid *values* become NULL; an invalid *conversion* stays the engine's error, because the renderer never
rewrites the destination. The closed map exposes no such destination today and `xml` is deliberately not a
public target — the raw statement `TRY_CAST(1 AS xml)` raises `Msg 529` ("Explicit conversion from data
type int to xml is not allowed."), which is the behaviour a future target of that kind would inherit. Both
functions are available on every SQL Server declaration (15, 16, 17 and unversioned), since
`TRY_CAST`/`TRY_CONVERT` predate 2022; every other provider rejects the member with `The TRY_CAST function
is not supported by this provider.`

## SQL Server type predicates

`SqlFunctions.SqlServer.isdate(value)` and `isnumeric(value)` render `isdate(...)` / `isnumeric(...)`
and are gated per name by [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions) (SQL Server
only):

| C# | SQL |
|---|---|
| `isdate(value)` / `isnumeric(value)` | `isdate(...)` / `isnumeric(...)` |

They deliberately return the native T-SQL `int` (1/0), **not** a `bit`, so they are projected as
ordinary integer values and are not materialised into a
`cast(case when ... then 1 else 0 end as bit)` predicate.

## Type conversion (ClickHouse)

The ClickHouse `to*`/`accurateCast`/`reinterpretAs` surface is typed on `SqlFunctions.ClickHouse` and
gated by the shared scalar-function capability, so every other dialect rejects it. Targets are exact:
`Int8` is widened to `short`/`Int16` and `UInt16` to `int` because the shared row reader has no
`sbyte`/`ushort` accessor, `to_decimal32`/`to_decimal64` take a validated compile-time scale, and
`accurate_cast_*` is delivered as twelve typed checked targets (base, `_or_null`, `_or_default`)
instead of collapsing into the `to*` arm.

| Function | SQL |
|---|---|
| `SqlFunctions.ClickHouse.to_string(v)` / `to_bool(v)` / `to_fixed_string(v, n)` / `to_low_cardinality(v)` | `toString` / `toBool` / `toFixedString` / `toLowCardinality` |
| `SqlFunctions.ClickHouse.to_int8(v)` … `to_int64(v)` | `toInt8` … `toInt64` |
| `SqlFunctions.ClickHouse.to_uint8(v)` … `to_uint64(v)` | `toUInt8` … `toUInt64` |
| `SqlFunctions.ClickHouse.to_float32(v)` / `to_float64(v)` | `toFloat32` / `toFloat64` |
| `SqlFunctions.ClickHouse.to_decimal32(v, scale)` / `to_decimal64(v, scale)` | `toDecimal32` / `toDecimal64` |
| `SqlFunctions.ClickHouse.accurate_cast_int8(v)` … plus `_or_null` / `_or_default` for every target | `accurateCast` / `accurateCastOrNull` / `accurateCastOrDefault` |
| `SqlFunctions.ClickHouse.reinterpret_as_uint8` … `reinterpret_as_uint64`, `_int8` … `_int64`, `_float32`/`_float64`, `_date`/`_date_time`/`_string`/`_fixed_string`/`_uuid` | `reinterpretAs*` |

`reinterpretAs*` preserves the little-endian bit pattern. The 128/256-bit integers, `Decimal128`/
`Decimal256` and the eleven `to_interval_*` units remain refused (no exact CLR carrier — `BigInteger`,
`System.Decimal`'s 96-bit mantissa, and the ClickHouse-only `Interval*` types respectively). See
[ClickHouse-specific SQL › Not yet supported](../guide/provider-specific-sql/clickhouse.md#not-yet-supported).
