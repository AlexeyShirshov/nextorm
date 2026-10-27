# MariaDB provider

> Use `nextorm.mariadb` for MariaDB 10.4+; it reuses the MySQL rendering and adds the `INTERSECT ALL`/`EXCEPT ALL` set-operation variants.

**Prerequisites:** [Provider overview](overview.md) · [MySQL](mysql.md)

## Overview

[`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext) (`src/nextorm.mariadb/MariaDbDataContext.cs`) derives from [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), so it uses
the same `MySqlConnector` driver and the same connection and parameter handling. It returns
[`Instance`](xref:NextORM.MariaDb.MariaDbDialect.Instance) from its `Dialect` property.

[`MariaDbDialect`](xref:NextORM.MariaDb.MariaDbDialect) (`src/nextorm.mariadb/MariaDbDialect.cs`) derives from [`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) and changes the
capability set: `INTERSECT ALL` and `EXCEPT ALL` are supported by MariaDB 10.4 and later, so
[`SupportsIntersectExceptAll`](xref:NextORM.Core.ISqlDialect.SupportsIntersectExceptAll) is `true`; MariaDB 10.3+ renders the window percentiles
`percentile_cont`/`percentile_disc` (`... within group (order by ...) over (...)`), so
[`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow) is `true`; and `UPDATE ... RETURNING` is enabled only when an explicit
server version 13.0+ is configured ([`SupportsUpdateReturning`](xref:NextORM.Core.ISqlDialect.SupportsUpdateReturning)). It also keeps the arbitrary-value
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

## Server version

The dialect can be pinned to the server version, which gates `UPDATE ... RETURNING` and the bulk
`Returning*` terminals. Configuration is **explicit** — nextorm never probes the live server. Pass the
version with the context constructor's `Version` parameter. Unlike PostgreSQL, an **unset** version does
**not** enable the feature: without an explicit 13.0+ the `Returning()` terminal of an `UPDATE` and a bulk
`Returning*` call reject with `NotSupportedException`. Single-row insert and delete `RETURNING` are
unchanged (MariaDB rejects them as before), and the `ANY_VALUE` gate (`any_agg`, targeted for 13.2) is also
unchanged. Bulk `Returning*` is supported on 13.0+ and refused below; the live certification runs on
`mariadb:11.4`, so it covers the refusal arm.

```csharp
// Explicit 13.0+: `UPDATE ... RETURNING` is enabled.
using var ctx = new MariaDbDataContext(
    "Server=localhost;Database=app;...", new DataContextBuilder(), new Version(13, 0));
```

The same explicit version also gates the native functions MariaDB added after 11.4. Each is emitted only at or
above its own minimum; an **unversioned** context (the two-argument constructor) refuses it with
`NotSupportedException` at translation/preparation rather than emitting SQL the server rejects:

| Member | Native form | Minimum |
|---|---|---|
| `SqlFunctions.MySql.months_between(a, b)` | `months_between(...)` | 12.2.1 |
| `SqlFunctions.MySql.to_date(value, format)` | `to_date(...)` | 12.3 |
| `SqlFunctions.MySql.to_number(value, format)` | `to_number(...)` | 12.2 family, enforced 12.2.1 |
| `SqlFunctions.Sql.gen_random_uuid()` | `uuid_v4()` | 11.7 |
| `SqlFunctions.Sql.uuidv7()` | `uuid_v7()` | 11.7 |

These are **function** minima, not a raised provider floor: the package still supports MariaDB 10.4+ and
`add_months`/`to_char`/the rest of the catalogue keep their own availability. The MariaDB-11.4 baseline names
(`add_months`, `to_char`, `nvl`, the JSON family, sequences) are not version-gated.

Unlike PostgreSQL, MariaDB imposes **no** one-version-per-concrete-context-type guard: each context carries
its own dialect instance and capability predicate, so the same concrete context type may be built with
different versions. A strongly typed subclass is still a convenient way to pin one version:

```csharp
public sealed class MariaDb130DataContext : MariaDbDataContext
{
    public MariaDb130DataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder, new Version(13, 0)) { }
}
```

## Set operations

```csharp
ctx.From<ISimpleEntity>().Select(x => x.Id).IntersectAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
ctx.From<ISimpleEntity>().Select(x => x.Id).ExceptAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
```

```sql
select id
 from simple_entity
 intersect all
select id
 from simple_entity
```

## Bulk insert

MariaDB shares the MySQL implementation, so the native path is `MySqlBulkCopy` (a `LOAD DATA LOCAL INFILE`
load) and it needs `AllowLoadLocalInfile=true` on the connection string plus the server variable
`@@GLOBAL.local_infile=1`, read with a read-only preflight. nextorm never enables either one; without them
`Auto` falls back only when the portable path honours the whole request and `Native` refuses by name.

- Identity and duplicates follow MySQL: `KeepIdentity` writes the explicit identity column under a scoped
  `NO_AUTO_VALUE_ON_ZERO` guard, and `IgnoreDuplicates` is `ConflictOption.Ignore` on the native load or
  `INSERT IGNORE` on the portable statement.
- `CheckConstraints=false` is honoured through scoped session controls, snapshotted and restored on the same
  session even when the load fails; `TableLock` and `FireTriggers` stay refused.
- `KeepNulls=false` needs a default-equivalent shape.
- **`Returning*` on the bulk path is version-gated.** MariaDB honours it at an explicitly configured 13.0+
  and refuses it below (unset or older) with a named `NotSupportedException`; single-row insert and delete
  `RETURNING` stay refused at every version, so read keys with `CreateInsertBuilder` there. The live
  certification runs on `mariadb:11.4`, so it exercises the refusal arm; the 13.0+ arm is certified offline
  for the rendered `INSERT ... RETURNING` SQL only.
- `DatabaseName` renders `db.table`; `ServerName` is refused (no rerouting). `Timeout` is one
  whole-invocation budget; an expired budget is a `TimeoutException` and cancellation wins.

See [Bulk insert](../guide/22-bulk-insert.md).

## Provider differences

MariaDB differs from [MySQL](mysql.md) in the set-operation capability, the window percentiles, the version-gated `UPDATE ... RETURNING` and the arbitrary-value aggregate:

| Aspect | MariaDB |
|---|---|
| Parameter placeholder | `@name` |
| Concat | `concat(a, b)` |
| Coalesce | `coalesce` |
| Identifier quoting | backticks (`` as `t1` ``) |
| Derived table alias | required |
| `*ALL` | supported (MariaDB 10.4+) |
| `UPDATE ... RETURNING` | enabled only with an explicit server version 13.0+ (`SupportsUpdateReturning`); otherwise `NotSupportedException` |
| Server version | configured explicitly via the context `Version` |
| Text JSON | inherited from MySQL (`JSON_EXTRACT`/`JSON_SET`) |
| Session/info functions | inherited from MySQL (`current_user()`, `session_user()`, `schema()`, `database()`, `version()`) |
| Arbitrary-value aggregate | not supported (no `ANY_VALUE` in 10.4–12.x; pending MDEV-10426, targeted for 13.2) |
| Conditional function | inherited from MySQL (`iif(cond, a, b)` → `if(cond, a, b)`) |
| Window percentiles | `percentile_cont`/`percentile_disc` as `... within group (order by x) over (...)` (MariaDB 10.3+) |
| Native functions | the `SqlFunctions.MySql` surface inherited from MySQL **minus** `uuid_to_bin`/`bin_to_uuid`, **plus** the MariaDB-only names (extended regexp, `nvl`/`nvl2`, `add_months`/`months_between`, `to_char`/`to_date`/`to_number`, `kdf`, `xxh3`/`xxh32`, `json_detailed`/`json_compact`, sequence access) |
| LOB streaming (`ToStream`/`ToTextReader`) | `NotSupportedException` |

## See also

- [Provider overview](overview.md)
- [MySQL and MariaDB-specific SQL](../guide/provider-specific-sql/mysql.md)
- [MySQL](mysql.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.mariadb/MariaDbDialect.cs`, `src/nextorm.mariadb/MariaDbDataContext.cs`,
`src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.mariadb.tests/Dialect/MariaDbDialectTests.cs`, `tests/nextorm.mariadb.tests/SqlGeneration/SqlGenerationTests.cs`.
