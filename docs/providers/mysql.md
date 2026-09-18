# MySQL provider

> Use `nextorm.mysql` for MySQL 8.x; it renders `@name` parameters, backtick-quoted identifiers, `concat(...)` concatenation, `limit`/`offset` paging and the standard-deviation/variance aggregate names.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

`MySqlDbContext` (`src/nextorm.mysql/MySqlDbContext.cs`) wraps `MySqlConnector`. It creates a
`MySqlConnection` from the connection string and returns `MySqlDialect.Instance` from its `Dialect`
property. `MySqlDialect` (`src/nextorm.mysql/MySqlDialect.cs`) is the dialect:

- parameter placeholder `@name`;
- identifiers and aliases are quoted with backticks;
- string concatenation uses the `concat(a, b, ...)` function — MySQL's infix `||` is a logical OR
  unless the `PIPES_AS_CONCAT` SQL mode is set, so the dialect never emits it;
- `MakeCoalesce` renders `coalesce(a, b)`;
- `MakeStringLength` renders `char_length(x)` (MySQL's `length()` counts bytes);
- `MakeNow` renders `now()` for local time and `utc_timestamp()` for UTC;
- `stdev`/`stdevp`/`var`/`varp` map to `stddev_samp`/`stddev_pop`/`var_samp`/`var_pop` (`stddev` and
  `variance` are the *population* synonyms in MySQL);
- a CLR conversion is rendered with a MySQL `CAST` target (`signed`/`unsigned` for the integer types,
  `double`, `decimal`, `char`, `datetime`) rather than the ANSI `bigint`/`integer`;
- the integer ones-complement renders as `(-(x) - 1)`, because MySQL's `~` yields an unsigned 64-bit
  value that overflows the signed CLR integer;
- the `ESCAPE` character of a `LIKE` predicate is written as `'\\'`, since MySQL also treats the
  backslash as a string-literal escape;
- paging is `limit n` / `limit n offset m`; an offset without a limit becomes
  `limit 18446744073709551615 offset m`, because MySQL only accepts `offset` together with `limit`.

## Registering the provider

Two overloads are available on `DbContextBuilder`
(`src/nextorm.mysql/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.mysql;

// From a connection string.
var builder = new DbContextBuilder()
    .UseMySql("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

// From an existing, caller-owned connection.
using var connection = new MySqlConnector.MySqlConnection("Server=localhost;Database=app");
var byConnection = new DbContextBuilder().UseMySql(connection);

using var ctx = builder.CreateDbContext();   // IDataContext
```

You can also construct the context directly (this is what the provider tests do):

```csharp
using nextorm.core;
using nextorm.mysql;

using IDataContext ctx = new MySqlDbContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DbContextBuilder());
```

## String concatenation

```csharp
var query = ctx.From<ISimpleEntity>().Select(x => new { Label = "id:" + x.Id });
```

```sql
select concat('id:', id) as `Label` from simple_entity
```

## Paging

```csharp
ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<ISimpleEntity>().Offset(10).Select(x => x.Id);    // limit 18446744073709551615 offset 10
```

## Provider differences

| Aspect | MySQL |
|---|---|
| Parameter placeholder | `@name` |
| Concat | `concat(a, b)` |
| Coalesce | `coalesce` |
| Boolean literal | `1` / `0` (aliases `true` / `false`) |
| Identifier quoting | backticks (`` as `t1` ``) |
| Derived table alias | required |
| TVF alias | required |
| `FULL JOIN` | not supported (right join is) |
| `*ALL` | not supported (MySQL 8.0.31 has `INTERSECT`/`EXCEPT`, but not the `ALL` variants) |

MySQL 8.0.31 and later support `INTERSECT`/`EXCEPT`; the dialect rejects the `*ALL` variants with a
`NotSupportedException`, matching the engine.

## See also

- [Provider overview](overview.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.mysql/MySqlDialect.cs`, `src/nextorm.mysql/MySqlDbContext.cs`,
`src/nextorm.mysql/DI/DataContextOptionsBuilderExtensions.cs`,
`test/nextorm.mysql.tests/MySqlDialectTests.cs`, `test/nextorm.mysql.tests/SqlGenerationTests.cs`.
