# Provider overview

> A provider supplies a dialect (SQL text rules) and a `DbContext` subclass (connection + parameter creation); pick the one that matches the database you already run.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Overview

nextorm is split into a provider-neutral core (`nextorm`, namespace `nextorm.core`) and one package per
database: `nextorm.sqlite`, `nextorm.sqlserver` and `nextorm.postgres`. The in-memory provider is built
into the core package. A provider contributes two things:

1. a **dialect** — a stateless object that renders everything that differs between databases (parameter
   placeholders, paging, quoting, function names, capability flags); and
2. a **context** — a `DbContext` subclass that knows how to create a connection and parameters, and
   exposes the dialect through its `Dialect` property.

SQL generation is driven entirely by `ISqlDialect` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`),
so the SQL builder and expression visitors never contain provider names. Query semantics (projection,
filtering, joins, grouping, set operations, CTE, window functions, scalar/UDF/TVF support) are shared;
only the rendering differs.

## Choosing a provider

| Situation | Provider |
|---|---|
| Local development, tests, embedded database, small apps | `nextorm.sqlite` |
| You already run Microsoft SQL Server / Azure SQL | `nextorm.sqlserver` |
| You already run PostgreSQL | `nextorm.postgres` |
| Unit tests that must not touch a database, plan-cache tests, query-shape tests | `InMemoryContext` (core) |

Use the same query code against every provider; the provider differences below are the only things that
change.

## Support matrix

| Capability | SQLite | SQL Server | PostgreSQL | In-memory |
|---|---|---|---|---|
| Package | `nextorm.sqlite` | `nextorm.sqlserver` | `nextorm.postgres` | built into `nextorm` |
| Parameter placeholder | `$name` | `@name` | `@name` | not applicable |
| Limit only | `limit n` | `top(n)` | `limit n` | in-process take |
| Limit + offset | `limit n offset m` | `offset m rows fetch next n rows only` | `limit n offset m` | in-process skip/take |
| Offset only | `limit -1 offset m` | `offset m rows` | `offset m` | in-process skip |
| Paging without `ORDER BY` | accepted | injects `order by (select null as anyorder)` | accepted | accepted |
| `INTERSECT ALL` / `EXCEPT ALL` (`*ALL`) | throws `NotSupportedException` | throws `NotSupportedException` | supported | not applicable |
| Recursive CTE keyword | `with recursive` | `with` (plus `option (maxrecursion n)`) | `with recursive` | not applicable |
| String concatenation | `||` | `+` | `||` | not applicable |
| Boolean literal | `1` / `0` | `1` / `0` (via `bit`) | `true` / `false` | not applicable |
| `??` (coalesce) | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` | not applicable |
| `stdev` / `stdevp` | `stdev` / `stdevp` (custom) | `stdev` / `stdevp` (native) | `stddev` / `stddev_pop` | not applicable |
| `var` / `varp` | `var` / `varp` (custom) | `var` / `varp` (native) | `variance` / `var_pop` | not applicable |
| Identifier / alias quoting | single quotes: `as 't1'` | brackets: `as [t1]` | double quotes: `as "t1"` | not applicable |
| Derived table (subquery in `FROM`) alias | not required | required | required | not applicable |
| Table-valued function alias | not required | required | required | TVF source not supported |
| `LEFT` / `RIGHT` / `FULL` / `CROSS` join | yes | yes | yes | yes |
| `RIGHT` / `FULL` join capability | supported | supported | supported | supported |

For the feature-by-feature comparison against EF Core and linq2db, see
[SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md).

## How a dialect plugs in

A dialect implements `ISqlDialect` or derives from `SqlDialectBase`. In `SqlDialectBase` only
`MakeParam` and `MakePage` are abstract; every other member has a working ANSI default, so a dialect
overrides just what is different. Capability differences (paging requiring an `ORDER BY`, required
subquery aliases, `INTERSECT ALL`/`EXCEPT ALL`) are expressed as properties rather than special cases in
the SQL builder.

```csharp
// The built-in dialects are singletons exposed as a static Instance.
ISqlDialect sqlite = SqliteDialect.Instance;
ISqlDialect sqlServer = SqlServerDialect.Instance;
ISqlDialect postgres = PostgresDialect.Instance;
```

A provider context returns its dialect from an overridden property:

```csharp
public class SqliteDbContext : DbContext
{
    public override ISqlDialect Dialect => SqliteDialect.Instance;
    // CreateDbConnection / CreateParam are provider specific.
}
```

The dialect contract deliberately excludes connection/parameter creation (`CreateConnection`/
`CreateParam`) and column mapping (`MapColumnExpression`): those live on the context, because they are a
separate axis from SQL text.

## Registering a provider

Every provider package adds `UseXxx` extension methods on `DbContextBuilder`, and every context also has a
public constructor that takes a connection string or an existing `DbConnection`:

```csharp
using nextorm.core;
using nextorm.sqlite;      // or nextorm.sqlserver / nextorm.postgres

var builder = new DbContextBuilder().UseSqlite("app.db");   // provider-specific overload
using var ctx = builder.CreateDbContext();                   // returns IDataContext
```

With dependency injection:

```csharp
services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

See [Dependency injection](../getting-started/04-dependency-injection.md) for keyed and generic
registrations.

## See also

- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [In-memory](in-memory.md)
- [SQL capabilities gap analysis](../sql-capabilities-gap-analysis.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`test/nextorm.sqlite.tests/SqliteDialectTests.cs`, `test/nextorm.sqlserver.tests/SqlServerDialectTests.cs`,
`test/nextorm.postgres.tests/PostgresDialectTests.cs`.
