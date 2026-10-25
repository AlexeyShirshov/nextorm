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
| `SqlFunctions.Postgres.log(base, x)` | `log(base, x)` |
| `SqlFunctions.Postgres.gcd(a, b)` / `lcm(a, b)` | `gcd(a, b)` / `lcm(a, b)` |
| `SqlFunctions.Postgres.factorial(n)` | `factorial(n)` |
| `SqlFunctions.Postgres.width_bucket(x, low, high, count)` | `width_bucket(x, low, high, count)` |

`SqlFunctions.Postgres.setseed(seed)` renders `setseed(seed)` and is gated separately by
[`SupportsRandomSeed`](xref:NextORM.Core.ISqlDialect.SupportsRandomSeed) (PostgreSQL only). The
PostgreSQL function returns `void`, so a projected value is always `null` and the call is made for its
side effect (subsequent `random()` calls in the session become reproducible).

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
