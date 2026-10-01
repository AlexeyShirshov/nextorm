# EF Core query-filter bridge

> When the EF Core integration registers a `DbContext` model, it imports the global query filters declared on that model — named/keyed and anonymous — into nextorm's query-filter pipeline, so EF's soft-delete and multi-tenancy predicates also apply to the nextorm reads and writes run over the same connection.

**Prerequisites:** [EF Core integration](integration-efcore.md) · [Global query filters](query-filters.md) · [API reference](api-reference.md)

## Overview

EF Core 10 stores its global query filters on the model (`IQueryFilter`), separate from the nextorm filter metadata. The `nextorm.entityframeworkcore` bridge closes that gap: [`NextOrmModelMapper.Register(db.Model)`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) — called automatically by `db.CreateNextOrmContext()` — enumerates the **root declared** query filters of every mapped entity and translates each one into a nextorm filter.

The two filter surfaces then behave the same way: an imported EF filter is `and`-ed into every query the entity participates in, takes part in the nextorm plan key by identity (not by value), and can be disabled per query with the same `IgnoreFilters`/`IgnoreQueryFilters` scope. This page describes what the bridge imports, how the owner context is recovered at execution time, which declarations are refused, and where the bridge stops.

## Importing EF filters

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

A mapped derived entity type inherits the filters declared on its root type, with the predicate's entity parameter adapted to the root CLR type. The model mapper's existing stance is unchanged: mapping an inheritance hierarchy itself (TPH/TPT/TPC) is still rejected (see [EF Core integration](integration-efcore.md#limitations)).

## Live owner semantics

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

## Capture guards

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

## Collisions and idempotency

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

## Fail-fast, atomic registration

Registration is all-or-none. The bridge first stages the entire model, the normalised filters and the registration fingerprints, rechecks conflicts across the staged batch, and only then publishes new metadata. The owner binding is attached **after** a successful publication.

Import is failure-atomic: failed publication preserves prior metadata. Concurrent-reader snapshot atomicity is not guaranteed.

If any entity or filter in the batch is invalid, the whole registration is refused and the previously published metadata is preserved — there is **no partial import**: you never end up with some entities mapped and some filters dropped. As usual, metadata is process-wide and first-registration-wins (see [Global query filters](query-filters.md#limitations)), so a refused registration leaves the earlier mapping and filters in force.

## Lifecycle, recovery, and bounded concurrency

An imported filter lives in the process-wide metadata cache, so the metadata can be dropped behind a context's back. A bridge-bound context then **fails closed**: on the next query or DML build it throws the documented `InvalidOperationException` (the message says the imported filter is no longer present and the bridge refuses to run unfiltered) instead of returning unfiltered rows. This also covers a command that was built before the metadata was dropped and is only now rendered or executed.

The metadata is dropped by:

- an explicit `DataContextCache.Clear()`, or
- a sliding-expiration eviction (`UseCacheSlidingExpiration`), which removes an entry not read within its TTL on the next access.

**Recovery** is to create or rebind a context through the bridge — `CreateNextOrmContext`, `GetNextOrmContext`, `ToNextOrm` or `AddNextOrmFromDbContext`. EF filter registration happens at context creation, so a fresh/rebound context re-publishes the imported filters and re-records the expectation; an already-created context cannot be repaired in place.

Concurrency is bounded, not free-threaded: metadata writers are serialised, but concurrent-reader snapshot atomicity is **not** promised (see [Fail-fast, atomic registration](#fail-fast-atomic-registration)). Do not run `DataContextCache.Clear()` or a bridge import concurrently with queries on a bridge context.

Two guards protect the imported-filter identity:

- **Parameter identity.** Each `DbContext` read (`this.TenantId`, `this.CurrentTenant.Threshold`, a nested chain) is named from its full host-free member chain, so two distinct chains that end in the same member (`…TenantId` versus `…Settings.TenantId`) bind distinct parameters. A collapse that would reuse one parameter name for two different owner chains is rejected before any SQL is produced, rather than silently binding one value in place of the other.
- **Accessor cache.** The compiled owner accessors take the executing context as an argument and are keyed host-free, so the cache does not retain a live `IDataContext`/`DbContext` (or its connection) after the caller's scope ends.

## IgnoreQueryFilters forwarding

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

## Supported adapters and limitations

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

## See also

- [EF Core integration](integration-efcore.md)
- [Global query filters](query-filters.md)
- [Limitations and out-of-scope features](limitations.md)
- [API reference](api-reference.md)

---

Source: `src/nextorm.entityframeworkcore/**` (XML doc comments are the authoritative API documentation).
