# MariaDB provider

> Use `nextorm.mariadb` for MariaDB 10.4+; it reuses the MySQL rendering and adds the `INTERSECT ALL`/`EXCEPT ALL` set-operation variants.

**Prerequisites:** [Provider overview](overview.md) · [MySQL](mysql.md)

## Overview

`MariaDbContext` (`src/nextorm.mariadb/MariaDbContext.cs`) derives from `MySqlDbContext`, so it uses
the same `MySqlConnector` driver and the same connection and parameter handling. It returns
`MariaDbDialect.Instance` from its `Dialect` property.

`MariaDbDialect` (`src/nextorm.mariadb/MariaDbDialect.cs`) derives from `MySqlDialect` and changes one
capability: `INTERSECT ALL` and `EXCEPT ALL` are supported by MariaDB 10.4 and later, so
`SupportsIntersectExceptAll` is `true`. Everything else (parameters, quoting, `concat`, coalesce,
paging, aggregate names) is inherited unchanged.

## Registering the provider

Two overloads are available on `DbContextBuilder`
(`src/nextorm.mariadb/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.mariadb;

var builder = new DbContextBuilder()
    .UseMariaDb("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

using var ctx = builder.CreateDbContext();   // IDataContext
```

You can also construct the context directly:

```csharp
using nextorm.core;
using nextorm.mariadb;

using IDataContext ctx = new MariaDbContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DbContextBuilder());
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

MariaDB differs from [MySQL](mysql.md) only in the set-operation capability:

| Aspect | MariaDB |
|---|---|
| Parameter placeholder | `@name` |
| Concat | `concat(a, b)` |
| Coalesce | `coalesce` |
| Identifier quoting | backticks (`` as `t1` ``) |
| Derived table alias | required |
| `*ALL` | supported (MariaDB 10.4+) |

## See also

- [Provider overview](overview.md)
- [MySQL](mysql.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.mariadb/MariaDbDialect.cs`, `src/nextorm.mariadb/MariaDbContext.cs`,
`src/nextorm.mariadb/DI/DataContextOptionsBuilderExtensions.cs`,
`test/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `test/nextorm.mariadb.tests/SqlGenerationTests.cs`.
