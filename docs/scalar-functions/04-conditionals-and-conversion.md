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
