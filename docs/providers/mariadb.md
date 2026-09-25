# MariaDB provider

> Use `nextorm.mariadb` for MariaDB 10.4+; it reuses the MySQL rendering and adds the `INTERSECT ALL`/`EXCEPT ALL` set-operation variants.

**Prerequisites:** [Provider overview](overview.md) · [MySQL](mysql.md)

## Overview

[`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext) (`src/nextorm.mariadb/MariaDbDataContext.cs`) derives from [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), so it uses
the same `MySqlConnector` driver and the same connection and parameter handling. It returns
[`Instance`](xref:NextORM.MariaDb.MariaDbDialect.Instance) from its `Dialect` property.

[`MariaDbDialect`](xref:NextORM.MariaDb.MariaDbDialect) (`src/nextorm.mariadb/MariaDbDialect.cs`) derives from [`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) and changes two
capabilities: `INTERSECT ALL` and `EXCEPT ALL` are supported by MariaDB 10.4 and later, so
[`SupportsIntersectExceptAll`](xref:NextORM.Core.ISqlDialect.SupportsIntersectExceptAll) is `true`; and MariaDB 10.3+ renders the window percentiles
`percentile_cont`/`percentile_disc` (`... within group (order by ...) over (...)`), so
[`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow) is `true`. It also keeps the arbitrary-value
aggregate `any_agg` gated **off**, because MariaDB has no `ANY_VALUE` in 10.4–12.x (the SQL-2023 `T626`
feature is still pending, targeted for 13.2). Everything else (parameters, quoting, `concat`,
coalesce, paging, aggregate names, the `if(...)` spelling of the portable `iif`) is inherited unchanged.

## Registering the provider

Two overloads are available on [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder)
(`src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.MariaDb;

var builder = new DataContextBuilder()
    .UseMariaDb("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

using var ctx = builder.CreateDataContext();   // IDataContext
```

You can also construct the context directly:

```csharp
using NextORM.Core;
using NextORM.MariaDb;

using IDataContext ctx = new MariaDbDataContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DataContextBuilder());
```

## Set operations

```csharp
ctx.From<ISimpleEntity>().Select(x => x.Id).IntersectAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
ctx.From<ISimpleEntity>().Select(x => x.Id).ExceptAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
```

```sql
select id from simple_entity
 intersect all
select id from simple_entity
```

## Provider differences

MariaDB differs from [MySQL](mysql.md) in the set-operation capability, the window percentiles and the arbitrary-value aggregate:

| Aspect | MariaDB |
|---|---|
| Parameter placeholder | `@name` |
| Concat | `concat(a, b)` |
| Coalesce | `coalesce` |
| Identifier quoting | backticks (`` as `t1` ``) |
| Derived table alias | required |
| `*ALL` | supported (MariaDB 10.4+) |
| Text JSON | inherited from MySQL (`JSON_EXTRACT`/`JSON_SET`) |
| Session/info functions | inherited from MySQL (`current_user()`, `session_user()`, `schema()`, `database()`, `version()`) |
| Arbitrary-value aggregate | not supported (no `ANY_VALUE` in 10.4–12.x; pending MDEV-10426, targeted for 13.2) |
| Conditional function | inherited from MySQL (`iif(cond, a, b)` → `if(cond, a, b)`) |
| Window percentiles | `percentile_cont`/`percentile_disc` as `... within group (order by x) over (...)` (MariaDB 10.3+) |
| Native functions | the `SqlFunctions.MySql` surface inherited from MySQL **minus** `uuid_to_bin`/`bin_to_uuid`, **plus** the MariaDB-only names (extended regexp, `nvl`/`nvl2`, `add_months`/`months_between`, `to_char`/`to_date`/`to_number`, `kdf`, `xxh3`/`xxh32`, `json_detailed`/`json_compact`, sequence access) |

## See also

- [Provider overview](overview.md)
- [MySQL and MariaDB-specific SQL](../guide/provider-specific/mysql.md)
- [MySQL](mysql.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.mariadb/MariaDbDialect.cs`, `src/nextorm.mariadb/MariaDbDataContext.cs`,
`src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`.
