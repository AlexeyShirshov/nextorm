# Entity Framework Core integration

> Run nextorm reads and writes over the connection and transaction of an existing EF Core `DbContext`, reusing the EF model mapping instead of declaring it again.

**Prerequisites:** [Provider overview](../connecting-providers/overview.md) · [Transactions](../guide/23-transactions.md) · [API reference](api-reference.md)

## Overview

The `nextorm.entityframeworkcore` package bridges an EF Core `DbContext` to a nextorm context. `db.CreateNextOrmContext()` returns an [`IDataContext`](xref:NextORM.Core.IDataContext) that:

- runs every query on the very [`DbConnection`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection) the `DbContext` already owns;
- enlists in the transaction EF opened (`db.Database.CurrentTransaction`), if any;
- reads its table/column mapping from the EF `IModel`, so entity classes need no nextorm `[SqlTable]`/`[Column]` (or fluent) configuration.

This lets you keep writes, change tracking and navigation fixup in EF Core while running nextorm's analytical reads (windows, CTEs, aggregates) on the same connection and transaction. The bridge shares that connection and transaction and never commits, rolls back or closes them; EF Core keeps ownership of both.

## Requirements

- EF Core **Relational** (`Microsoft.EntityFrameworkCore.Relational`) with one of the supported providers: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.SqlServer`, `Pomelo.EntityFrameworkCore.MySql` (MySQL and MariaDB), Oracle's `MySql.EntityFrameworkCore` (MySQL) or `Microsoft.EntityFrameworkCore.Sqlite`. Both MySQL providers are recognised; because Pomelo has no EF Core 10 release yet, the MySQL shared-transaction path is verified through Oracle's `MySql.EntityFrameworkCore` 10.x (ADO.NET driver `MySql.Data`) — see [Shared connection and transaction](#shared-connection-and-transaction).
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

The shared connection/transaction path is verified end-to-end by container-backed integration tests on **PostgreSQL**, **SQL Server** and **MySQL**, and by local tests on **SQLite**. On PostgreSQL, SQL Server and MySQL the public `CreateNextOrmContext()` bridge is exercised against a live server: nextorm borrows the exact `DbConnection` EF owns, sees EF's uncommitted rows inside an open transaction, an EF rollback discards them, a nextorm `CreateInsertBuilder` run inside EF's transaction is visible to EF and then commits or rolls back with it, and disposing the nextorm context leaves EF's connection open and EF's transaction active. On SQLite the tests cover seeing EF's uncommitted rows and an EF rollback discarding them. Verifying PostgreSQL, SQL Server and MySQL requires running the integration suite against provisioned containers (Testcontainers); the SQLite cases run without one.

> **MySQL is verified through Oracle's provider.** Because `Pomelo.EntityFrameworkCore.MySql` has no EF Core 10 release (its latest 9.0.0 targets EF Core 9), the container-backed MySQL tests run on EF Core 10 with Oracle's `MySql.EntityFrameworkCore` 10.0.9 and its `MySql.Data` ADO.NET driver. The bridge recognises both provider names — `Pomelo.EntityFrameworkCore.MySql` and `MySql.EntityFrameworkCore`; to serve the second driver, `nextorm.mysql` creates its parameters through the executing command (`command.CreateParameter()`), leaving the normal `MySqlConnector` path unchanged. Sharing EF's transaction with MySQL is therefore verified, not pending. MariaDB is served by Pomelo's provider only.

See [Transactions](../guide/23-transactions.md) for the enlistment contract shared with Dapper and raw ADO.NET.

## Model mapping

[`NextOrmModelMapper.Register(db.Model)`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) projects the EF model into nextorm metadata and is called automatically by `CreateNextOrmContext`:

- **Table / view** — the table name (`entityType.GetTableName()`), or the view name (`GetViewName()`) for a view-mapped type.
- **Columns** — the column name from `property.GetColumnName()`, so renamed columns (`HasColumnName("selected_id")`) are honoured without a nextorm attribute.
- **Key / identity / computed** — the primary key (`FindPrimaryKey()`), `ValueGenerated.OnAdd` (identity) and `ValueGenerated.OnAddOrUpdate` (computed) are carried over.
- **Skipped** — shadow properties (no CLR member), owned types, and entity types with no table/view name or no mapped CLR properties.
- **Imported** — the model's global query filters (named/keyed and anonymous) are translated into nextorm filters, so soft-delete and multi-tenancy scoping is preserved (see [Global query filters from the EF model](#global-query-filters-from-the-ef-model)).
- **Rejected** — model shapes the integration cannot represent and would otherwise map wrongly: a schema-qualified table (nextorm metadata has no schema member) and any inheritance hierarchy — TPH/TPT/TPC (nextorm carries no discriminator). Each throws `NotSupportedException`.

Because nextorm's metadata cache is process-wide and keyed by `Type` (`DataContextCache.Metadata`), **at most one mapping per CLR type exists per process**. Re-registering the identical mapping is a no-op; registering a *different* table/column layout for an already-mapped type throws `InvalidOperationException`. Clear the cache (`DataContextCache.Clear()`) to map that type afresh. Clearing the cache also drops the global query filters the bridge imported; a bridge-bound context then **fails closed** with the documented `InvalidOperationException` on its next query or DML build — never unfiltered rows — and recovery is a fresh or rebound bridge context. See [Lifecycle, recovery, and bounded concurrency](#lifecycle-recovery-and-bounded-concurrency).

## Global query filters from the EF model

When the EF Core integration registers a `DbContext` model, it imports the global query filters declared on that model — named/keyed and anonymous — into nextorm's query-filter pipeline, so EF's soft-delete and multi-tenancy predicates also apply to the nextorm reads and writes run over the same connection.

EF Core 10 stores its global query filters on the model (`IQueryFilter`), separate from the nextorm filter metadata. The `nextorm.entityframeworkcore` bridge closes that gap: [`NextOrmModelMapper.Register(db.Model)`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) — called automatically by `db.CreateNextOrmContext()` — enumerates the **root declared** query filters of every mapped entity and translates each one into a nextorm filter.

The two filter surfaces then behave the same way: an imported EF filter is `and`-ed into every query the entity participates in, takes part in the nextorm plan key by identity (not by value), and can be disabled per query with the same `IgnoreFilters`/`IgnoreQueryFilters` scope. This section describes what the bridge imports, how the owner context is recovered at execution time, which declarations are refused, and where the bridge stops.

### Importing EF filters

The bridge reads each root filter once, when the EF model is registered:

- an **anonymous** EF filter (`Key == null`) maps to nextorm's [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey) (`""`);
- a **named/keyed** EF filter keeps its key and becomes a nextorm named filter with the same key.

```csharp
public sealed class AppDbContext : DbContext
{
    public DbSet<Document> Documents => Set<Document>();

    public int TenantId { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Anonymous: additive with every other anonymous filter.
        modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);

        // Named/keyed: targetable by IgnoreQueryFilters("soft-delete").
        modelBuilder.Entity<Document>().HasQueryFilter("soft-delete", d => !d.Archived);
    }
}
```

Every nextorm context built from that `DbContext` then applies both filters to `From<Document>()` and to the other operators that inject filters (joins, subqueries, DML):

```csharp
using var next = db.CreateNextOrmContext();

var visible = next.From<Document>().ToList(); // soft-delete + archived filters applied
```

A mapped derived entity type inherits the filters declared on its root type, with the predicate's entity parameter adapted to the root CLR type. The model mapper's existing stance is unchanged: mapping an inheritance hierarchy itself (TPH/TPT/TPC) is still rejected (see [EF Core integration](#limitations)).

### Live owner semantics

An EF query filter is a closure over the owning `DbContext` instance, and EF re-evaluates it against the **current** context on every query. The bridge preserves that:

- every `DbContext` read inside the filter (`this.TenantId`, `this.CurrentTenant.Threshold`, a nested member chain) is rewritten to a lookup of the **owner** of the executing nextorm context;
- the owner is recovered from the live nextorm context (bound to the exact `DbContext` that created it), not from the instance that happened to build the model;
- the owner identity and the context-derived values are **not** part of nextorm's process-wide metadata or of the plan key — only the filter's identity is.

Two contexts whose filters read different values therefore share one cached plan, and each execution re-reads its own value, including on a **warmed/cached** execution:

```csharp
using var east = new AppDbContext { TenantId = 1 };
using var west = new AppDbContext { TenantId = 2 };

using (var next = east.CreateNextOrmContext())
    Console.WriteLine(next.From<Document>().Count()); // east.TenantId

using (var next = west.CreateNextOrmContext())
    Console.WriteLine(next.From<Document>().Count()); // west.TenantId, same plan shape
```

Because the value is read from the live owner, a **null nested owner value** (for example a null `CurrentTenant`) fails closed at execution: the bridge surfaces the documented owner failure instead of a bare null-reference error, and a null **leaf** value (owner present) still binds null and keeps the predicate. The filter is never silently removed.

An imported filter lives in process-wide metadata, so it can also run on a plain nextorm context that was not created through the bridge. The owner lookup then **fails closed** with an `InvalidOperationException` naming the missing EF owner and the bridge requirement, rather than leaving a null owner. Run bridge-imported filters on a context created via `CreateNextOrmContext`, `GetNextOrmContext`, `ToNextOrm` or `AddNextOrmFromDbContext`.

### Capture guards

The owner is resolved at execution time, so a value captured when the model is built cannot be honoured without freezing the wrong value. The bridge therefore refuses such declarations **at bridge creation**, before any metadata is published:

- a **closure-local capture** (a member read over a compiler-generated closure, e.g. a local variable read by the filter) is rejected;
- a **static-member capture** (a member access with no instance, e.g. `Seed.TenantId`) is rejected;
- `EF.Property(...)` / `EF.Functions` calls and member accesses are rejected;
- **navigation / subquery filters** (`Queryable`/`Enumerable` calls, a nested query root) are rejected;
- a lambda with a signature other than exactly one entity parameter and a `bool` body is rejected;
- an incompatible context type (a captured `DbContext` value not assignable to the declared context type of its member access) is rejected;
- an invalid named key (a null/empty/whitespace key declares an *anonymous* filter, not a named one) and a duplicate named key on one entity are rejected;
- an unsupported provider/mapping or conflicting metadata is rejected.

Three of these checks are **defensive translator guards**, not promised public-EF reproduction cases: an invalid lambda signature and an incompatible context type arise only from hand-built or otherwise unsupported translator input, and EF may overwrite repeated named-key registrations before the bridge imports the model. The duplicate-key rejection therefore applies to duplicates observable in the imported/normalized input, not to registration history EF has already overwritten.

```csharp
// Rejected: closure-local capture settled at model build.
var tenant = 1;
modelBuilder.Entity<Document>().HasQueryFilter(d => d.TenantId == tenant);

// Rejected: static-member capture.
modelBuilder.Entity<Document>().HasQueryFilter(d => d.TenantId == Seeds.TenantId);

// Supported: a literal scalar constant folds to a bound parameter.
modelBuilder.Entity<Document>().HasQueryFilter(d => d.Value >= 100);
```

Read the value through a `DbContext` member instead of capturing it; the bridge rewrites that read to the live owner (see [Live owner semantics](#live-owner-semantics)).

### Collisions and idempotency

Importing a filter goes through the same collision rules as the nextorm surface, with the stricter rule that two independent **named** registrations never silently replace each other:

| Declaration | Outcome |
|---|---|
| Named EF filter vs a nextorm named filter with the same key on the same entity | **error** — the two registrations collide |
| Two EF named filters with the same key on the same entity | **error**, even when the predicates are identical |
| Anonymous EF filter vs an anonymous nextorm filter | **additive** — both predicates are `and`-ed (`""` key) |
| The same named key on **different** entity types | allowed |
| Repeating an identical complete bridge registration | **idempotent** — fingerprinted by mapping + provenance + normalised filter bodies |
| Repeating a registration with a changed mapping or changed filter body | **error** |

The fingerprint that drives idempotency excludes the owner identity and the context-derived values, so re-registering the same model is a no-op rather than a collision. The named-key collision rule applies to distinct registrations visible in the imported model: a repeated named-key registration may already have been overwritten by EF before import, in which case the bridge observes only the surviving registration.

### Fail-fast, atomic registration

Registration is all-or-none. The bridge first stages the entire model, the normalised filters and the registration fingerprints, rechecks conflicts across the staged batch, and only then publishes new metadata. The owner binding is attached **after** a successful publication.

Import is failure-atomic: failed publication preserves prior metadata. Concurrent-reader snapshot atomicity is not guaranteed.

If any entity or filter in the batch is invalid, the whole registration is refused and the previously published metadata is preserved — there is **no partial import**: you never end up with some entities mapped and some filters dropped. As usual, metadata is process-wide and first-registration-wins (see [Global query filters](query-filters.md#limitations)), so a refused registration leaves the earlier mapping and filters in force.

### Lifecycle, recovery, and bounded concurrency

An imported filter lives in the process-wide metadata cache, so the metadata can be dropped behind a context's back. A bridge-bound context then **fails closed**: on the next query or DML build it throws the documented `InvalidOperationException` (the message says the imported filter is no longer present and the bridge refuses to run unfiltered) instead of returning unfiltered rows. This also covers a command that was built before the metadata was dropped and is only now rendered or executed.

The metadata is dropped by:

- an explicit `DataContextCache.Clear()`, or
- a sliding-expiration eviction (`UseCacheSlidingExpiration`), which removes an entry not read within its TTL on the next access.

**Recovery** is to create or rebind a context through the bridge — `CreateNextOrmContext`, `GetNextOrmContext`, `ToNextOrm` or `AddNextOrmFromDbContext`. EF filter registration happens at context creation, so a fresh/rebound context re-publishes the imported filters and re-records the expectation; an already-created context cannot be repaired in place.

Concurrency is bounded, not free-threaded: metadata writers are serialised, but concurrent-reader snapshot atomicity is **not** promised (see [Fail-fast, atomic registration](#fail-fast-atomic-registration)). Do not run `DataContextCache.Clear()` or a bridge import concurrently with queries on a bridge context.

Two guards protect the imported-filter identity:

- **Parameter identity.** Each `DbContext` read (`this.TenantId`, `this.CurrentTenant.Threshold`, a nested chain) is named from its full host-free member chain, so two distinct chains that end in the same member (`…TenantId` versus `…Settings.TenantId`) bind distinct parameters. A collapse that would reuse one parameter name for two different owner chains is rejected before any SQL is produced, rather than silently binding one value in place of the other.
- **Accessor cache.** The compiled owner accessors take the executing context as an argument and are keyed host-free, so the cache does not retain a live `IDataContext`/`DbContext` (or its connection) after the caller's scope ends.

### IgnoreQueryFilters forwarding

A converted EF query can disable the imported filters with EF's own `IgnoreQueryFilters` operators; `ToNextOrm` translates them into the nextorm query-local filter scope:

```csharp
// Disables every filter of this query, including anonymous ones.
var all = db.Documents
    .IgnoreQueryFilters()
    .ToNextOrm(db)
    .Select(d => d.Id)
    .ToList();

// Disables only the named filter, keeping anonymous filters active.
var active = db.Documents
    .IgnoreQueryFilters("soft-delete")
    .ToNextOrm(db)
    .Select(d => d.Id)
    .ToList();
```

Forwarding rules:

- the **parameterless** form disables **every** filter, named and anonymous;
- the **named** form disables only the listed keys and **preserves** anonymous filters;
- an **unknown** key matches nothing, an **empty/whitespace** key never selects the anonymous filter, and an **empty** collection is a no-op;
- duplicate keys collapse, **chained** named calls union their keys, and a parameterless call dominates regardless of order;
- the scope is **query-local**: it applies only to the converted query. It does not affect a direct `From<T>()` on the same context and is not written to the shared `Any`/`Count`/`All` command state or to global metadata.

`IgnoreQueryFilters` is accepted only as a `ToNextOrm`-translated EF operator. A direct `From<T>()` uses nextorm's own `IgnoreFilters` overloads (see [Disabling filters for one query](query-filters.md#disabling-filters-for-one-query)).

### Supported adapters and limitations

The filter import runs wherever the bridge maps an EF model, so it is available for the EF adapters the integration recognises:

| EF Core adapter | `nextorm` provider |
|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | `nextorm.sqlite` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `nextorm.postgres` |
| `Microsoft.EntityFrameworkCore.SqlServer` | `nextorm.sqlserver` |
| `Pomelo.EntityFrameworkCore.MySql` / `MySql.EntityFrameworkCore` (MySQL) | `nextorm.mysql` |

The non-relational `Microsoft.EntityFrameworkCore.InMemory` provider is **not** supported (it has no `DbConnection`), so no filter can be imported through it. ClickHouse has no EF Core provider the bridge recognises today, and MariaDB's shared-connection path is served only through Pomelo's MySQL provider. The ClickHouse no-EF-adapter slice and the MariaDB-specific shared-connection certification are tracked open units in milestone `1.0.9-b`; until they land, use the supported adapters above.

Other limitations are inherited from the bridge and the filter subsystem:

- one process-wide mapping per CLR type, first-registration-wins;
- schema-qualified tables and inheritance hierarchies are still rejected by the model mapper;
- a filter that reads a raw `FromSql`/`From(string)` source needs `BindEntity<TEntity>` (see [Filters on bound raw sources](query-filters.md#filters-on-bound-raw-sources));
- there is no `SaveChanges`/change-tracking bridge; a filter applies to nextorm reads and writes, not to EF's own change tracker.

## Provider selection

`CreateNextOrmContext` inspects `dbContext.Database.ProviderName` and wires the matching nextorm context over the EF connection:

| `Database.ProviderName` (exact) | nextorm provider |
|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `nextorm.postgres` ([`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext)) |
| `Microsoft.EntityFrameworkCore.SqlServer` | `nextorm.sqlserver` ([`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext)) |
| `Pomelo.EntityFrameworkCore.MySql` | `nextorm.mysql` ([`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext)) |
| `MySql.EntityFrameworkCore` | `nextorm.mysql` ([`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext)) |
| `Microsoft.EntityFrameworkCore.Sqlite` | `nextorm.sqlite` ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext)) |

Provider names are matched exactly (ordinal), so a look-alike name is rejected. Anything else — including `Microsoft.EntityFrameworkCore.InMemory`, a missing provider name, or an EF provider for a database nextorm does not bridge (for example ClickHouse or Oracle) — throws `InvalidOperationException` before a context is created. Both MySQL provider names route to the same `nextorm.mysql` context; MariaDB is served by Pomelo's MySQL provider only. The `configure` callback cannot add a provider; build the nextorm context yourself over the EF connection if you need one of the other nextorm providers.

## Limitations

- **No change-tracking or `SaveChanges` bridge.** The bridge runs on EF Core's connection and, when EF has one open, inside EF's transaction: nextorm reads and writes executed there participate in that transaction, so a nextorm `CreateInsertBuilder` run inside EF's open transaction is visible to EF and commits or rolls back with it (verified on PostgreSQL, SQL Server and MySQL). It is still not a `SaveChanges` bridge — nextorm DML bypasses EF's change tracker, so rows it wrote are unknown to the tracker. Write through EF Core when you need tracking; use the returned [`IDataContext`](xref:NextORM.Core.IDataContext) for queries and for DML that should enlist in the same transaction.
- **`ToNextOrm` translates a bounded operator subset.** Every operator outside the [supported set](#supported-operators) — `Select`/`SelectMany`, `Include`/navigations, `Join`/`GroupJoin`, `GroupBy`, `EF.Property`/`EF.Functions`, `AsSplitQuery`, raw-SQL roots, subqueries, a second primary `OrderBy` after sorting has started, `ThenBy` without a primary, and an invalid sort/`Distinct`/paging order — throws `NotSupportedException`. Nothing is silently evaluated in memory; unlike `Select`, which is terminal in nextorm and must be applied after `ToNextOrm(...)`. `IgnoreQueryFilters`/`IgnoreQueryFilters(keys)` **are** accepted and forwarded to the nextorm query-local filter scope (see [Global query filters from the EF model](#global-query-filters-from-the-ef-model)).
- **One CLR mapping per process.** The mapping lives in nextorm's process-wide metadata cache keyed by `Type`; re-registering the identical mapping is fine, but a second `DbContext` that maps an already-mapped CLR type differently throws `InvalidOperationException` (clear `DataContextCache.Metadata` to reset).
- **EF InMemory is unsupported.** `Microsoft.EntityFrameworkCore.InMemory` has no `DbConnection` and its provider name is rejected as unsupported before the connection is touched. Only relational EF providers are bridged.
- **Unsupported model shapes are rejected, not silently mapped.** Schema-qualified tables and inheritance (TPH/TPT/TPC) throw `NotSupportedException`, because nextorm has no schema or discriminator support and would otherwise emit wrong SQL. Global query filters are not rejected: they are imported and applied (see [Global query filters from the EF model](#global-query-filters-from-the-ef-model)). Owned types, table splitting, shadow properties, value converters and temporal tables are not projected either.
- **Imported filters and the bridge lifecycle.** Import is failure-atomic: a refused registration preserves the previously published metadata (concurrent-reader snapshot atomicity is not guaranteed). A bridge-bound context whose imported filter metadata is later dropped — by `DataContextCache.Clear()` or a sliding-expiration eviction — fails closed with the documented `InvalidOperationException` on the next query or DML build instead of running unfiltered; recovery is a fresh or rebound bridge context. An imported filter is process-wide metadata, so on a context not created through the bridge the owner lookup fails closed with `InvalidOperationException` instead of dropping the filter. Invalid lambda signatures, incompatible context types and a duplicate named key are defensive translator guards, not promised public-EF reproduction cases: EF may overwrite a repeated named-key registration before import, so the duplicate check applies only to duplicates observable in the imported/normalized input.

## See also

- [Transactions](../guide/23-transactions.md)
- [Connections](../infrastructure/01-connections.md)
- [Provider overview](../connecting-providers/overview.md)
- [Global query filters](query-filters.md)
- [API reference](api-reference.md)

---

Source: `src/nextorm.entityframeworkcore/**` (XML doc comments are the authoritative API documentation).
