# MySQL provider

> Use `nextorm.mysql` for MySQL 8.x; it renders `@name` parameters, backtick-quoted identifiers, `concat(...)` concatenation, `limit`/`offset` paging and the standard-deviation/variance aggregate names.

**Prerequisites:** [Provider overview](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Overview

[`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext) (`src/nextorm.mysql/MySqlDataContext.cs`) wraps `MySqlConnector`. It creates a
`MySqlConnection` from the connection string and returns [`Instance`](xref:NextORM.MySql.MySqlDialect.Instance) from its `Dialect`
property. [`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) (`src/nextorm.mysql/MySqlDialect.cs`) is the dialect:

- parameter placeholder `@name`;
- identifiers and aliases are quoted with backticks;
- string concatenation uses the `concat(a, b, ...)` function — MySQL's infix `||` is a logical OR
  unless the `PIPES_AS_CONCAT` SQL mode is set, so the dialect never emits it;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect) renders `coalesce(a, b)`;
- [`MakeStringLength`](xref:NextORM.Core.ISqlDialect) renders `char_length(x)` (MySQL's `length()` counts bytes);
- [`MakeNow`](xref:NextORM.Core.ISqlDialect) renders `now()` for local time and `utc_timestamp()` for UTC;
- `stdev`/`stdevp`/`var`/`varp` map to `stddev_samp`/`stddev_pop`/`var_samp`/`var_pop` (`stddev` and
  `variance` are the *population* synonyms in MySQL);
- a CLR conversion is rendered with a MySQL `CAST` target (`signed`/`unsigned` for the integer types,
  `double`, `decimal`, `char`, `datetime`) rather than the ANSI `bigint`/`integer`;
- the integer ones-complement renders as `(-(x) - 1)`, because MySQL's `~` yields an unsigned 64-bit
  value that overflows the signed CLR integer;
- the `ESCAPE` character of a `LIKE` predicate is written as `'\\'`, since MySQL also treats the
  backslash as a string-literal escape;
- paging is `limit n` / `limit n offset m`; an offset without a limit becomes
  `limit 18446744073709551615 offset m`, because MySQL only accepts `offset` together with `limit`;
- the session/information family ([`SupportsSessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SupportsSessionInfoFunctions)) renders
  `SqlFunctions.Sql.current_user`/`session_user`/`current_database`/`version` as `current_user()`/`session_user()`/
  `database()`/`version()` and `current_schema` as `schema()`;
- the arbitrary-value aggregate [`SqlFunctions.Sql.any_agg`](xref:NextORM.Core.CommonFunctions.any_agg``1) renders as
  `ANY_VALUE(x)` ([`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate), MySQL 5.7+);
- the portable conditional [`SqlFunctions.Sql.iif`](xref:NextORM.Core.CommonFunctions.iif``1) renders as
  `if(condition, a, b)` ([`SupportsIif`](xref:NextORM.Core.ISqlDialect.SupportsIif), [`MakeIif`](xref:NextORM.Core.ISqlDialect.MakeIif)).

## Registering the provider

Two overloads are available on [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)
(`src/nextorm.mysql/DI/MySqlDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.MySql;

// From a connection string.
var builder = new DataContextBuilder()
    .UseMySql("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

// From an existing, caller-owned connection.
using var connection = new MySqlConnector.MySqlConnection("Server=localhost;Database=app");
var byConnection = new DataContextBuilder().UseMySql(connection);

using var ctx = builder.CreateDataContext();   // IDataContext
```

You can also construct the context directly (this is what the provider tests do):

```csharp
using NextORM.Core;
using NextORM.MySql;

using IDataContext ctx = new MySqlDataContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DataContextBuilder());
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
| Text JSON | `json_value`/`json_query`/`json_modify`/`isjson` (over `JSON_EXTRACT`/`JSON_UNQUOTE`/`JSON_SET`/`JSON_VALID`) |
| Session/info functions | `current_user()`, `session_user()`, `schema()`, `database()`, `version()` |
| Arbitrary-value aggregate | `ANY_VALUE(x)` |
| Conditional function | `iif(cond, a, b)` → `if(cond, a, b)` |
| Window percentiles | not supported (`PERCENTILE_CONT` is MariaDB-only) |

MySQL 8.0.31 and later support `INTERSECT`/`EXCEPT`; the dialect rejects the `*ALL` variants with a
`NotSupportedException`, matching the engine.

## See also

- [Provider overview](overview.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.mysql/MySqlDialect.cs`, `src/nextorm.mysql/MySqlDataContext.cs`,
`src/nextorm.mysql/DI/MySqlDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.mysql.tests/MySqlDialectTests.cs`, `tests/nextorm.mysql.tests/SqlGenerationTests.cs`.
