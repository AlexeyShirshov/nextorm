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
