# Math functions

| C# | SQL | Notes |
|---|---|---|
| `Math.Abs(x)` | `abs(x)` | |
| `Math.Round(x)` | `round(x)` / `round(x, 0)` | SQL Server supplies the required length argument. |
| `Math.Round(x, digits)` | `round(x, digits)` | PostgreSQL casts a `double`/`float` first argument to `numeric` (`round((x)::numeric, digits)`), because it has no `round(double precision, integer)`. |
| `Math.Truncate(x)` | `trunc(x)` / `round(x, 0, 1)` | SQL Server has no `trunc`. |
| `Math.Log(x)` | natural logarithm: `ln(x)` (SQLite, PostgreSQL) / `log(x)` (SQL Server) | Single-argument form only. |
| `Math.Log10(x)` | `log10(x)` | |
| `Math.Acos(x)` / `Math.Asin(x)` / `Math.Atan(x)` | `acos(x)` / `asin(x)` / `atan(x)` | |
| `Math.Atan2(y, x)` | `atan2(y, x)` (SQL Server: `atn2(y, x)`) | |

```csharp
var values = dataContext.From<IComplexEntity>()
    .Select(e => Math.Abs(e.Id - 5))
    .ToList();
// ids 1, 2, 3 -> 4, 3, 2
```

```sql
select abs((id - 5))
 from complex_entity
```

The `Output:` tables below show the rows returned by each example against the integration-test seed data (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Output:

| Abs |
|-----|
| 4   |
| 3   |
| 2   |

## Portable trigonometric and angle functions

Besides the `Math.*` translation above, [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) exposes
the angle functions that have no BCL member. They render natively where the provider has them (gated
per name by [`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String))):

| C# | SQL | Notes |
|---|---|---|
| `SqlFunctions.Sql.cot(x)` | `cot(x)` | PostgreSQL, SQL Server and MySQL/MariaDB; ClickHouse and SQLite have no `cot` and reject it. |
| `SqlFunctions.Sql.degrees(x)` | `degrees(x)` | All providers (SQL Server casts the argument to `float`). |
| `SqlFunctions.Sql.radians(x)` | `radians(x)` | All providers (SQL Server casts the argument to `float`). |
| `SqlFunctions.Sql.pi()` | `pi()` | All providers. |

`Math.Acos`/`Asin`/`Atan`/`Atan2` need no wrapper — they translate directly, with SQL Server emitting
its `atn2` spelling for the two-argument form.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Angle = SqlFunctions.Sql.degrees(SqlFunctions.Sql.pi()),
        Cot = SqlFunctions.Sql.cot(1.0)
    })
    .ToList();
```

## SQL Server bit functions (2022+)

`SqlFunctions.SqlServer` exposes the five T-SQL bit functions, each as one overload set per operand type
(`byte?`/`short?`/`int?`/`long?`), and every call renders the native function behind a typed-NULL guard:

| C# | SQL Server | Result |
|---|---|---|
| `left_shift(value, shift)` / `right_shift(value, shift)` | `LEFT_SHIFT` / `RIGHT_SHIFT` (logical, zero-fill) | the operand's own type: `byte?`/`short?`/`int?`/`long?` |
| `bit_count(value)` | `BIT_COUNT` | `long?` (`bigint`) |
| `get_bit(value, offset)` | `GET_BIT` | `bool?` (`bit`) |
| `set_bit(value, offset)` | `SET_BIT`, native two-argument form (bit set to 1) | the operand's own type |
| `set_bit(value, offset, bitValue)` | `SET_BIT`, three-argument form | the operand's own type |

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Shifted = SqlFunctions.SqlServer.left_shift((int?)1, 2),        // 4
        Negative = SqlFunctions.SqlServer.left_shift((int?)-8, 1),      // -16
        RightShifted = SqlFunctions.SqlServer.right_shift((int?)-8, 1), // 2147483644 (logical shift)
        Bits = SqlFunctions.SqlServer.bit_count((byte?)5),              // 2 (long)
        Bit = SqlFunctions.SqlServer.get_bit((byte?)5, 1),              // true (bool)
        Set = SqlFunctions.SqlServer.set_bit((short?)5, 1),             // 7
        Cleared = SqlFunctions.SqlServer.set_bit((short?)5, 1, false),  // 5
        NullOperand = SqlFunctions.SqlServer.left_shift((byte?)null, 1) // null
    })
    .ToList();
```

```sql
CASE WHEN 1 IS NULL OR 2 IS NULL THEN CAST(NULL AS int)
     ELSE left_shift(cast(1 as int), cast(2 as bigint)) END
```

The operand decides the SQL type — `byte`→`tinyint`, `short`→`smallint`, `int`→`int`, `long`→`bigint` —
the shift amount is cast to `bigint`, the bit offset to `int`, and the three-argument `SET_BIT` value to
`bit`. `bit_count` is `bigint` on the engine, so its CLR shape is `long?`; `get_bit` is `bit`, so it is
`bool?`. `right_shift` is the engine's logical (zero-fill) shift, not an arithmetic one:
`right_shift(-8, 1)` is `2147483644`, not `-4`.

A bare `left_shift(NULL, …)` is rejected by the server with **Msg 8116** ("Argument data type NULL is
invalid"), so the renderer types every argument and guards a NULL operand, shift, offset or bit value with a
typed NULL of the result type — a NULL never reaches the native call.

The whole family is **SQL Server 2022 (major version 16) or later**, gated by the version-only
[`SupportsSqlServer2022Functions`](xref:NextORM.Core.ISqlDialect.SupportsSqlServer2022Functions) flag. A
context declared for 15 or an unversioned context refuses every name before SQL, for example `The SET_BIT
function requires SQL Server major version 16 or later; declared major version: 15.` (or `… unspecified.`).

> **Overload resolution.** Because the family is one overload set per operand type, an untyped constant
> binds to the *smallest* overload that can hold it: `left_shift(1, 2)` selects `byte?` and renders
> `tinyint`, and `left_shift(-8, 1)` selects `short?`. The rendered SQL type therefore follows the
> compiler's pick, not the literal's intent — cast the operand explicitly (`left_shift((int?)1, 2)`)
> whenever a specific SQL Server type is required.

For MariaDB numeric, bit and vector functions, including the MariaDB 11.7 vector gates, see the
[MariaDB catalogue](08-reference.md#mariadb-catalogue).


## ClickHouse-only math

Beyond the portable [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) surface,
[`SqlFunctions.ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) exposes four ClickHouse-only math
functions. They are **not** part of the cross-provider `Math.*`/BCL translation and were not added to
[`CommonFunctions`](xref:NextORM.Core.CommonFunctions); every other provider (PostgreSQL, SQL Server,
MySQL, MariaDB, SQLite) and the in-memory provider rejects a call with `NotSupportedException` instead of
emulating it.

| C# | SQL | Result |
|---|---|---|
| `SqlFunctions.ClickHouse.log2(x)` / `log2(x)` | `log2(x)` | Base-2 logarithm. |
| `SqlFunctions.ClickHouse.cbrt(x)` / `cbrt(x)` | `cbrt(x)` | Cube root. |
| `SqlFunctions.ClickHouse.exp2(x)` / `exp2(x)` | `exp2(x)` | Two raised to the power `x`. |
| `SqlFunctions.ClickHouse.exp10(x)` / `exp10(x)` | `exp10(x)` | Ten raised to the power `x`. |

Each function has two overloads, `double f(double x)` and `double? f(double? x)`. ClickHouse returns
`Float64` for the non-nullable form and `Nullable(Float64)` for the nullable one; a `NULL` argument
yields `NULL`, never zero. On the pinned 25.8 baseline `log2(8) = 3`, `cbrt(8) = 2`, `exp2(3) = 8` and
`exp10(2) = 100`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        L2 = SqlFunctions.ClickHouse.log2((double)e.Id),
        Cbrt = SqlFunctions.ClickHouse.cbrt((double)e.Id),
        Exp2 = SqlFunctions.ClickHouse.exp2((double)e.Id),
        E10 = SqlFunctions.ClickHouse.exp10((double)e.Id),
        NullableL2 = SqlFunctions.ClickHouse.log2(e.Double)
    })
    .ToList();
```

### Delivered numeric, rounding and bit functions (#286, T286b)

The remaining ClickHouse-only numeric families are typed members too. `is_finite`/`is_infinite`/`is_nan`
return `bool`, the integer forms keep the vendor width (`int_div`/`int_exp2`/`int_exp10` are `UInt64` →
`ulong`), the rounding family returns the argument's integer width, and the bit family declares
`UInt64`-widened arithmetic (`bitAnd`/`bitOr`/`bitXor`/`bit_count`/`bit_hamming_distance` → `ulong`) with
the width-preserving shifts/rotations and the `bitTest*` booleans. Each has a `NULL`-propagating
overload where the vendor accepts `Nullable`.

| Function | SQL | Result |
|---|---|---|
| `SqlFunctions.ClickHouse.erf(x)` / `erfc(x)` | `erf(x)` / `erfc(x)` | `double` (`Nullable` for `double?`) |
| `SqlFunctions.ClickHouse.lgamma(x)` / `tgamma(x)` | `lgamma(x)` / `tgamma(x)` | `double` |
| `SqlFunctions.ClickHouse.gcd(a, b)` / `lcm(a, b)` | `gcd(a, b)` / `lcm(a, b)` | `ulong` |
| `SqlFunctions.ClickHouse.is_finite(x)` / `is_infinite(x)` / `is_nan(x)` | `isFinite(x)` / `isInfinite(x)` / `isNaN(x)` | `bool` |
| `SqlFunctions.ClickHouse.hypot(a, b)` | `hypot(a, b)` | `double` |
| `SqlFunctions.ClickHouse.int_div(a, b)` / `int_exp2(x)` / `int_exp10(x)` | `intDiv(a, b)` / `intExp2(x)` / `intExp10(x)` | `ulong` |
| `SqlFunctions.ClickHouse.sigmoid(x)` / `sqr(x)` | `sigmoid(x)` / `sqr(x)` | `double` |
| `SqlFunctions.ClickHouse.round_age(x)` / `round_bankers(x)` / `round_down(x)` / `round_duration(x)` / `round_to_exp2(x)` | `roundAge` / `roundBankers` / `roundDown` / `roundDuration` / `roundToExp2` | the argument's integer width |
| `SqlFunctions.ClickHouse.bitAnd(a, b)` / `bitOr` / `bitXor` | `bitAnd(a, b)` / `bitOr` / `bitXor` | `ulong` |
| `SqlFunctions.ClickHouse.bitNot(a)` / `bitShiftLeft(a, n)` / `bitShiftRight(a, n)` | `bitNot(a)` / `bitShiftLeft(a, n)` / `bitShiftRight(a, n)` | the operand width (`byte`/`int`/`uint`/`ulong`) |
| `SqlFunctions.ClickHouse.bitRotateLeft(a, n)` / `bitRotateRight(a, n)` | `bitRotateLeft(a, n)` / `bitRotateRight(a, n)` | the operand width |
| `SqlFunctions.ClickHouse.bitCount(a)` | `bitCount(a)` | `byte` |
| `SqlFunctions.ClickHouse.bitTest(a, i)` / `bitTestAll` / `bitTestAny` | `bitTest(a, i)` / `bitTestAll` / `bitTestAny` | `bool` |
| `SqlFunctions.ClickHouse.bitHammingDistance(a, b)` | `bitHammingDistance(a, b)` | `ulong` (string overload included) |

The portable equivalents of `e`, `plus`, `minus`, `multiply`, `divide` and `negate` are the `Math.E`
constant and the CLR arithmetic operators, not ClickHouse-only members.

## PostgreSQL extended math

The remaining math functions are part of the extended scalar library
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), PostgreSQL only).
`degrees`/`radians`/`pi` are no longer provider-specific — use the portable
[`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) spellings above:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.asin(x)` / `acos(x)` / `atan(x)` | `asin(x)` / `acos(x)` / `atan(x)` |
| `SqlFunctions.Postgres.atan2(y, x)` | `atan2(y, x)` |
| `SqlFunctions.Postgres.cbrt(x)` | `cbrt(x)` |
| `SqlFunctions.Postgres.sinh(x)` / `cosh(x)` / `tanh(x)` | `sinh(x)` / `cosh(x)` / `tanh(x)` |
| `SqlFunctions.Postgres.asinh(x)` / `acosh(x)` / `atanh(x)` | `asinh(x)` / `acosh(x)` / `atanh(x)` |
| `SqlFunctions.Postgres.random()` | `random()` |
| `SqlFunctions.Postgres.log(base, x)` | `log((base)::numeric, (x)::numeric)` — both operands are cast to `numeric`, because PostgreSQL's only two-argument form is `log(numeric, numeric)`; one-argument `log` stays `ln` |
| `SqlFunctions.Postgres.gcd(a, b)` / `lcm(a, b)` | `gcd(a, b)` / `lcm(a, b)` |
| `SqlFunctions.Postgres.factorial(n)` | `factorial(n)` |
| `SqlFunctions.Postgres.width_bucket(x, low, high, count)` | `width_bucket(x, low, high, count)` |

`SqlFunctions.Postgres.setseed(seed)` renders `setseed(seed)` and is gated separately by
[`SupportsRandomSeed`](xref:NextORM.Core.ISqlDialect.SupportsRandomSeed) (PostgreSQL only). The
PostgreSQL function returns `void`, so a projected value is always `null` and the call is made for its
side effect (subsequent `random()` calls in the session become reproducible).

### Delivered math and degree-trigonometric helpers (#281)

The remainder of the PostgreSQL math catalogue is exposed on the same surface, one member per server
overload. Every name is gated **individually** by
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)), so a
provider that does not implement the interface rejects the call with `NotSupportedException`:

| C# | SQL | Result |
|---|---|---|
| `SqlFunctions.Postgres.div(y, x)` | `div(y, x)` | `decimal?` — `numeric` division truncated toward zero |
| `SqlFunctions.Postgres.erf(x)` | `erf(x)` | `double?` — the error function, in `-1..1` |
| `SqlFunctions.Postgres.erfc(x)` | `erfc(x)` | `double?` — `1 - erf(x)`, in `0..2` |
| `SqlFunctions.Postgres.random_normal(mean, stddev)` | `random_normal(mean, stddev)` | `double?` — volatile, a new value per execution |
| `SqlFunctions.Postgres.acosd(x)` | `acosd(x)` | `double?` — arccosine in degrees |
| `SqlFunctions.Postgres.asind(x)` | `asind(x)` | `double?` — arcsine in degrees |
| `SqlFunctions.Postgres.atand(x)` | `atand(x)` | `double?` — arctangent in degrees |
| `SqlFunctions.Postgres.atan2d(y, x)` | `atan2d(y, x)` | `double?` — arctangent of `y`/`x` in degrees, using the signs of both |
| `SqlFunctions.Postgres.cosd(x)` | `cosd(x)` | `double?` — cosine of an angle in degrees |
| `SqlFunctions.Postgres.cotd(x)` | `cotd(x)` | `double?` — cotangent of an angle in degrees |
| `SqlFunctions.Postgres.sind(x)` | `sind(x)` | `double?` — sine of an angle in degrees |
| `SqlFunctions.Postgres.tand(x)` | `tand(x)` | `double?` — tangent of an angle in degrees |

`div` is truncating `numeric` division, not a rounding one: `div(7, 2)` is `3` and `div(-7, 2)` is
`-3`; division by zero raises on the server. The `*d` family takes its argument **in degrees** and
returns degrees, so `cosd(60)` is `0.5`; its radian counterparts are the
`asin`/`acos`/`atan`/`atan2` rows above. Only the two-argument `random_normal(mean, stddev)` is
exposed; the server's zero- and one-argument forms are not. A NULL argument yields NULL.

A name the pinned server cannot run is refused while the query is prepared:

| Family | Refused member | Tested reason |
|---|---|---|
| numeric | `gamma(x)` | absent on the pinned PostgreSQL 17.11 — missing from `pg_proc` and from the PG17 mathematical-functions table, and a live call fails with `42883`; the name does not exist in any release, so there is no rename story. |
| numeric | `tgamma(x)` | the same absence on 17.11 (`42883`). |
| numeric | `lgamma(x)` | the same absence on 17.11 (`42883`). |

## PostgreSQL numeric scale

Three PostgreSQL-only functions describe the scale of a `numeric` value. They are gated **per name**
by [`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)), so every
other provider rejects them with `NotSupportedException` instead of rendering PostgreSQL SQL:

| C# | SQL | Result |
|---|---|---|
| `SqlFunctions.Postgres.scale(decimal? value)` | `scale(value)` | `int?`: digits in the fractional part, trailing zeroes included |
| `SqlFunctions.Postgres.min_scale(decimal? value)` | `min_scale(value)` | `int?`: digits that remain after the trailing zeroes are removed |
| `SqlFunctions.Postgres.trim_scale(decimal? value)` | `trim_scale(value)` | `decimal?`: the value with its trailing fractional zeroes removed |

`scale` is available on every supported PostgreSQL version; `min_scale` and `trim_scale` were added in
PostgreSQL 13. All three return NULL for a NULL argument. `trim_scale` only removes trailing zeroes —
it is not `round`/`trunc` and never changes a significant digit:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Scale = SqlFunctions.Postgres.scale(12.3400m),
        MinScale = SqlFunctions.Postgres.min_scale(12.3400m),
        Trimmed = SqlFunctions.Postgres.trim_scale(12.3400m)
    })
    .ToList();
// Scale = 4, MinScale = 2, Trimmed = 12.34 (decimal scale 2)
```

`12.3400` reports scale 4 because the fractional part holds four digits, while `min_scale` ignores the
trailing zeroes and reports 2. `trim_scale(12.3456m)` returns `12.3456` unchanged, keeping scale 4, and
`trim_scale(0.0000m)` returns `0` with scale 0.

## SQL Server random

`SqlFunctions.SqlServer.rand()` / `rand(seed)` render `rand()` / `rand(seed)` and are gated per name
by [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions) (SQL Server only; every other
provider throws `NotSupportedException`):

| C# | SQL |
|---|---|
| `rand()` / `rand(seed)` | `rand()` / `rand(seed)` |

`rand` without a seed is **non-deterministic** and evaluated by the server; a fixed integer seed makes
the sequence reproducible. The PostgreSQL `random()`/`setseed()` equivalent is in the
[extended math](#postgresql-extended-math) section above.
