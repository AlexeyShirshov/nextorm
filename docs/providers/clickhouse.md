# ClickHouse provider

> Use `nextorm.clickhouse` for ClickHouse; it renders `@name` parameters (rewritten to `{name:Type}` by the driver), backtick-quoted identifiers, `concat(...)` concatenation, `limit`/`offset` paging and ClickHouse type names.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

`ClickHouseDbContext` (`src/nextorm.clickhouse/ClickHouseDbContext.cs`) wraps the official
`ClickHouse.Driver` ADO.NET provider. It creates a `ClickHouseConnection` from the connection string
and returns `ClickHouseDialect.Instance` from its `Dialect` property.

`ClickHouseDialect` (`src/nextorm.clickhouse/ClickHouseDialect.cs`) renders:

- parameter placeholder `@name`; the driver rewrites these to ClickHouse's native
  `{name:Type}` form and infers the type from the .NET value;
- backtick-quoted identifiers and aliases;
- string concatenation with the `concat(a, b, ...)` function;
- `coalesce(a, b)`, `true`/`false` boolean literals and `lengthUTF8(x)` for string length;
- `trimBoth`/`trimLeft`/`trimRight` for the three trim kinds;
- `now()` for local time and `now('UTC')` for UTC;
- `stdev`/`stdevp`/`var`/`varp` as `stddevSamp`/`stddevPop`/`varSamp`/`varPop`;
- `date_trunc(field, x)` as `dateTrunc('field', x)` (the plural ANSI sub-second parts are singularised;
  `decade`/`century`/`millennium` throw), and `date_add`/`DateTime.Add*` as the dedicated
  `addYears`/`addQuarters`/…/`addSeconds` functions (`decade`/`century`/`millennium` fold onto a scaled
  `addYears`), `end_of_month` as `toLastDayOfMonth(x)`;
- `string_agg(x, delimiter)` as `arrayStringConcat(groupArray(x), delimiter)`;
- `bit_and`/`bit_or`/`bit_xor` as `groupBitAnd`/`groupBitOr`/`groupBitXor`, `covar_pop`/`covar_samp` as
  `covarPop`/`covarSamp`, `corr` as `corr`, `arg_min`/`arg_max` as `argMin`/`argMax`, and the filtered
  aggregates `count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` as the `-If` combinators
  `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`;
- ClickHouse type names in casts (`Int32`, `Int64`, `Float64`, `Decimal(38, 10)`, …);
- `limit n` / `limit n offset m` paging; an offset without a limit becomes
  `limit 18446744073709551615 offset m`, because ClickHouse only accepts `offset` together with `limit`.

ClickHouse has no recursive CTE support, so the dialect declares every CTE with plain `with`.

## Registering the provider

Two overloads are available on `DbContextBuilder`
(`src/nextorm.clickhouse/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.clickhouse;

var builder = new DbContextBuilder()
    .UseClickHouse("Host=localhost;Port=8123;Username=default;Password=secret;Database=app");

using var ctx = builder.CreateDbContext();   // IDataContext
```

You can also construct the context directly:

```csharp
using nextorm.core;
using nextorm.clickhouse;

using IDataContext ctx = new ClickHouseDbContext(
    "Host=localhost;Username=default;Database=app", new DbContextBuilder());
```

## String concatenation

```csharp
var query = ctx.From<ISimpleEntity>().Select(x => new { Label = "id:" + x.Id });
```

```sql
select concat('id:', id) as `Label` from simple_entity
```

## Provider differences

| Aspect | ClickHouse |
|---|---|
| Parameter placeholder | `@name` (driver rewrites to `{name:Type}`) |
| Concat | `concat(a, b)` |
| Coalesce | `coalesce` |
| Boolean literal | `true` / `false` |
| Identifier quoting | backticks (`` as `t1` ``) |
| Derived table alias | required |
| TVF alias | required |
| `*ALL` | supported |
| Recursive CTE | not supported (the `recursive` modifier is omitted) |
| `date_trunc` | `dateTrunc('field', x)` |
| Date arithmetic | `addDays(x, n)` … `addYears(x, (n) * 10)`; `toLastDayOfMonth(x)` |
| `string_agg` | `arrayStringConcat(groupArray(x), delimiter)` (no `array_agg`) |
| Bit / statistical aggregates | `groupBitAnd`/`groupBitOr`/`groupBitXor`; `corr`/`covarPop`/`covarSamp` |
| Regression aggregates | not supported (`regr_*` is PostgreSQL-only) |
| Boolean aggregates | not supported (`bool_and`/`bool_or`/`every` are PostgreSQL-only) |
| Filtered aggregate | `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf` (no ANSI `filter (where ...)`) |
| ArgMin / ArgMax | `argMin`/`argMax` |
| Arrays / JSON / extended scalars | not supported (PostgreSQL-only) |

## Notes and limitations

- ClickHouse parameters are sent as HTTP query parameters. Bulk inserts should use the driver's binary
  insert API rather than parameterized `INSERT` statements, which is outside the query builder.
- Null parameter values cannot have their ClickHouse type inferred from the CLR value alone. When a
  query binds a `null` parameter, set an explicit parameter type at the driver level (for example with
  a custom resolver) or cast the placeholder in SQL.

## See also

- [Provider overview](overview.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.clickhouse/ClickHouseDbContext.cs`,
`src/nextorm.clickhouse/DI/DataContextOptionsBuilderExtensions.cs`,
`test/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`, `test/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
