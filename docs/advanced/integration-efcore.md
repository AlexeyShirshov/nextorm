# Entity Framework Core integration

> Run nextorm reads over the connection and transaction of an existing EF Core `DbContext`, reusing the EF model mapping instead of declaring it again.

**Prerequisites:** [Provider overview](../providers/overview.md) · [Transactions](../guide/23-transactions.md) · [API reference](api-reference.md)

## Overview

The `nextorm.entityframeworkcore` package bridges an EF Core `DbContext` to a nextorm context. `db.CreateNextOrmContext()` returns an [`IDataContext`](xref:NextORM.Core.IDataContext) that:

- runs every query on the very [`DbConnection`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection) the `DbContext` already owns;
- enlists in the transaction EF opened (`db.Database.CurrentTransaction`), if any;
- reads its table/column mapping from the EF `IModel`, so entity classes need no nextorm `[SqlTable]`/`[Column]` (or fluent) configuration.

This lets you keep writes, change tracking and navigation fixup in EF Core while running nextorm's analytical reads (windows, CTEs, aggregates) on the same connection and transaction. The bridge is one-way: nextorm observes, EF Core owns.

## Requirements

- EF Core **Relational** (`Microsoft.EntityFrameworkCore.Relational`) with one of the supported providers: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.SqlServer`, `Pomelo.EntityFrameworkCore.MySql` (MySQL and MariaDB) or `Microsoft.EntityFrameworkCore.Sqlite`.
- The `nextorm.entityframeworkcore` package; it references `nextorm.core` and the four provider packages, so no separate nextorm provider reference is needed for the mapping.
- EF's non-relational `InMemory` provider is not supported (see [Limitations](#limitations)).

## Minimal example

```csharp
using Microsoft.EntityFrameworkCore;
using NextORM.Core;                 // From<T>() and the terminal extension methods
using NextORM.EntityFrameworkCore; // CreateNextOrmContext()

using var db = new AppDbContext(options); // your EF Core context

// nextorm runs on db's connection and, when EF has one open, EF's current transaction.
using var next = db.CreateNextOrmContext();

var top = next.From<Order>()
    .Where(o => o.Total > 100)
    .OrderBy(o => o.Total)
    .Select(o => new { o.Id, o.Total })
    .ToList();
```

`CreateNextOrmContext` also takes an optional `configure` callback that runs after the provider is selected from `Database.ProviderName`; use it to override nextorm defaults (logger, naming convention, command timeout, …):

```csharp
using var next = db.CreateNextOrmContext(builder => builder
    .UseLoggerFactory(loggerFactory)
    .UseCommandTimeout(30));
```

To register the mapping without creating a context, call [`NextOrmModelMapper.Register`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) with `db.Model` directly — see [Model mapping](#model-mapping).

## Options builder and dependency injection

`UseNextOrm` stores the optional nextorm configuration on the EF Core options, so every later bridge call picks it up without taking the delegate again:

```csharp
DbContextOptionsBuilder UseNextOrm(this DbContextOptionsBuilder optionsBuilder, Action<DataContextBuilder>? configure = null);

DbContextOptionsBuilder<TContext> UseNextOrm<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, Action<DataContextBuilder>? configure = null)
    where TContext : DbContext;
```

```csharp
public sealed class AppDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .UseNpgsql(connectionString)
            .UseNextOrm(builder => builder.UseCommandTimeout(30));
}

using var db = new AppDbContext();

using var next = db.GetNextOrmContext();

var top = next.From<Order>()
    .Where(o => o.Total > 100)
    .Select(o => new { o.Id, o.Total })
    .ToList();
```

`GetNextOrmContext` takes no arguments — it reads the connection, current transaction and model from `db`, plus the configuration stored by `UseNextOrm`:

```csharp
IDataContext GetNextOrmContext(this DbContext dbContext);
```

`AddNextOrmFromDbContext<TDbContext>` registers `IDataContext` as a **scoped** service built from the scope's `TDbContext`, so its lifetime matches the EF context:

```csharp
IServiceCollection AddNextOrmFromDbContext<TDbContext>(this IServiceCollection services, Action<DataContextBuilder>? configure = null)
    where TDbContext : DbContext;
```

```csharp
services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseNextOrm(builder => builder.UseCommandTimeout(30)));

services.AddNextOrmFromDbContext<AppDbContext>();
```

A class can then take `IDataContext` through its constructor. The `configure` callback passed to `AddNextOrmFromDbContext` runs after the delegate stored by `UseNextOrm`, so it can override it per registration.

## Translating an EF query with `ToNextOrm`

`ToNextOrm` converts an EF Core `IQueryable<T>` rooted at a `DbSet<T>` into an equivalent nextorm [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1), so a LINQ-to-Entities query executes on the same connection and transaction. Both overloads require a class entity:

```csharp
EntityBuilder<T> ToNextOrm<T>(this DbSet<T> source) where T : class;

EntityBuilder<T> ToNextOrm<T>(this IQueryable<T> source, DbContext dbContext) where T : class;
```

The `DbSet<T>` overload resolves the owning `DbContext` from the set itself; pass the context explicitly through the `IQueryable<T>, DbContext` overload for a query already composed with operators:

```csharp
var all = db.Orders
    .ToNextOrm()
    .Select(o => new { o.Id, o.Total })
    .ToList();

var top = db.Orders
    .Where(o => o.Total > 100)
    .OrderBy(o => o.Total)
    .ThenBy(o => o.Id)
    .Skip(20)
    .Take(10)
    .ToNextOrm(db)
    .Select(o => new { o.Id, o.Total })
    .ToList();
```

### Supported operators

Only operators nextorm can render without a client-side fallback are translated:

| EF Core operator | nextorm |
|---|---|
| `Where` | `Where(predicate)` |
| `OrderBy` / `OrderByDescending` | the single primary `OrderBy` / `OrderByDescending` |
| `ThenBy` / `ThenByDescending` | ordering-key continuations |
| `Skip(n)` | `Offset(n)` |
| `Take(n)` | `Limit(n)` |
| `Distinct()` | `Distinct()` |
| `AsNoTracking()` / `AsTracking()` / `TagWith(...)` | ignored |

**`Select` is terminal in nextorm.** `ToNextOrm` returns an `EntityBuilder<T>`, so project **after** it with nextorm's own `Select`/projection; a `Select` placed before `ToNextOrm(...)` is captured as an unsupported operator.

## Shared connection and transaction

`CreateNextOrmContext` reads `dbContext.Database.GetDbConnection()` and builds the nextorm context over that exact `DbConnection` instance. If the connection is closed when a nextorm query runs, nextorm opens it and leaves it open; it never closes, commits or rolls back the borrowed connection or transaction. EF Core keeps ownership for their whole lifetime, and disposing the nextorm context disposes neither.

When `dbContext.Database.CurrentTransaction` is active, the bridge enlists it with `ITransactionManager.UseTransaction(...)`. As long as the EF transaction is open, nextorm sees EF's uncommitted rows, and an EF rollback removes them:

```csharp
await using var tx = await db.Database.BeginTransactionAsync();

db.Rows.Add(new Row { Name = "pending" });
await db.SaveChangesAsync(ct);          // uncommitted, still inside tx

using var next = db.CreateNextOrmContext();
next.From<Row>().Where(r => r.Name == "pending").ToList(); // sees the row

await tx.RollbackAsync(ct);
next.From<Row>().Where(r => r.Name == "pending").ToList(); // empty
```

See [Transactions](../guide/23-transactions.md) for the enlistment contract shared with Dapper and raw ADO.NET.

## Model mapping

[`NextOrmModelMapper.Register(db.Model)`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) projects the EF model into nextorm metadata and is called automatically by `CreateNextOrmContext`:

- **Table / view** — the table name (`entityType.GetTableName()`), or the view name (`GetViewName()`) for a view-mapped type.
- **Columns** — the column name from `property.GetColumnName()`, so renamed columns (`HasColumnName("selected_id")`) are honoured without a nextorm attribute.
- **Key / identity / computed** — the primary key (`FindPrimaryKey()`), `ValueGenerated.OnAdd` (identity) and `ValueGenerated.OnAddOrUpdate` (computed) are carried over.
- **Skipped** — shadow properties (no CLR member), owned types, and entity types with no table/view name or no mapped CLR properties.
- **Rejected** — model shapes the integration cannot represent and would otherwise map wrongly: a schema-qualified table (nextorm metadata has no schema member), a global query filter (soft-delete / multi-tenant filtering would be dropped) and any inheritance hierarchy — TPH/TPT/TPC (nextorm carries no discriminator). Each throws `NotSupportedException`.

Because nextorm's metadata cache is process-wide and keyed by `Type` (`DataContextCache.Metadata`), **at most one mapping per CLR type exists per process**. Re-registering the identical mapping is a no-op; registering a *different* table/column layout for an already-mapped type throws `InvalidOperationException`. Clear the cache (`DataContextCache.Clear()`) to map that type afresh.

## Provider selection

`CreateNextOrmContext` inspects `dbContext.Database.ProviderName` and wires the matching nextorm context over the EF connection:

| `Database.ProviderName` (exact) | nextorm provider |
|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `nextorm.postgres` ([`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext)) |
| `Microsoft.EntityFrameworkCore.SqlServer` | `nextorm.sqlserver` ([`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext)) |
| `Pomelo.EntityFrameworkCore.MySql` | `nextorm.mysql` ([`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext)) |
| `Microsoft.EntityFrameworkCore.Sqlite` | `nextorm.sqlite` ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext)) |

Provider names are matched exactly (ordinal), so a look-alike name is rejected. Anything else — including `Microsoft.EntityFrameworkCore.InMemory`, a missing provider name, or an EF provider for a database nextorm does not bridge (for example ClickHouse or Oracle) — throws `InvalidOperationException` before a context is created. MariaDB is served by Pomelo's MySQL provider, which routes to the MariaDB-compatible `nextorm.mysql` context. The `configure` callback cannot add a provider; build the nextorm context yourself over the EF connection if you need one of the other nextorm providers.

## Limitations

- **Read-only bridge.** The integration is designed for reads: there is no change-tracking sync and no `SaveChanges` bridge, and nextorm mutations would run outside EF's change tracker. Write through EF Core; use the returned [`IDataContext`](xref:NextORM.Core.IDataContext) for queries. A DML/`SaveChanges` bridge is explicitly out of scope.
- **`ToNextOrm` translates a bounded operator subset.** Every operator outside the [supported set](#supported-operators) — `Select`/`SelectMany`, `Include`/navigations, `Join`/`GroupJoin`, `GroupBy`, `EF.Property`/`EF.Functions`, `IgnoreQueryFilters`, `AsSplitQuery`, raw-SQL roots, subqueries, a second primary `OrderBy` after sorting has started, `ThenBy` without a primary, and an invalid sort/`Distinct`/paging order — throws `NotSupportedException`. Nothing is silently evaluated in memory; unlike `Select`, which is terminal in nextorm and must be applied after `ToNextOrm(...)`.
- **One CLR mapping per process.** The mapping lives in nextorm's process-wide metadata cache keyed by `Type`; re-registering the identical mapping is fine, but a second `DbContext` that maps an already-mapped CLR type differently throws `InvalidOperationException` (clear `DataContextCache.Metadata` to reset).
- **EF InMemory is unsupported.** `Microsoft.EntityFrameworkCore.InMemory` has no `DbConnection` and its provider name is rejected as unsupported before the connection is touched. Only relational EF providers are bridged.
- **Unsupported model shapes are rejected, not silently mapped.** Query filters, schema-qualified tables and inheritance (TPH/TPT/TPC) throw `NotSupportedException`, because nextorm has no query-filter, schema or discriminator support and would otherwise emit wrong SQL. Owned types, table splitting, shadow properties, value converters and temporal tables are not projected either.

## See also

- [Transactions](../guide/23-transactions.md)
- [Connections and logging](../guide/14-connections-and-logging.md)
- [Provider overview](../providers/overview.md)
- [API reference](api-reference.md)

---

Source: `src/nextorm.entityframeworkcore/**` (XML doc comments are the authoritative API documentation).
