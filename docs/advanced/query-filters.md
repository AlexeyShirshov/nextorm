# Global query filters

> A query filter is a predicate bound to an entity type in the mapping metadata; nextorm `and`s it into every query in which the entity participates — the primary `FROM`, join sources and subqueries — unless the query calls `IgnoreFilters()`.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Provider overview](../providers/overview.md) · [Filtering (`Where`)](../guide/01-filtering-where.md)

## Overview

Global query filters attach a recurring predicate — soft-delete and multi-tenancy are the canonical cases — to the entity type instead of repeating it in every `Where`. A filter can read the executing [`IDataContext`](xref:NextORM.Core.IDataContext), so a tenant identifier or a soft-delete flag is resolved per query while the generated plan stays cached and shared across contexts.

This is the nextorm equivalent of EF Core global query filters and linq2db query filters.

## Declaring a filter

### Fluent

Declare the filter where the entity's mapping is configured with `From<T>`. The declaration runs once, when the type's metadata is first built, so the condition is part of the mapping rather than of one query:

```csharp
ctx.From<Document>(m => m.HasQueryFilter(d => !d.IsDeleted));
```

The context-aware overload receives the executing [`IDataContext`](xref:NextORM.Core.IDataContext) as a second parameter, so the filter can read per-context state:

```csharp
ctx.From<Document>(m => m.HasQueryFilter((d, c) => d.TenantId == (int)c.Properties["tenant"]));
```

[`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) exposes both overloads:

```csharp
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter);
```

A repeated call adds another predicate; all filters declared for a type are combined with `and`.

### Attribute

The same filters can be declared declaratively with [`[QueryFilter]`](xref:NextORM.Core.QueryFilterAttribute) on a class or an interface. `FilterLambda` names a **static member** of the attributed type — a field, a property or a parameterless method — that returns the filter lambda:

```csharp
[QueryFilter(FilterLambda = nameof(Active))]
public sealed class Document
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }

    public static Expression<Func<Document, bool>> Active() => d => !d.IsDeleted;
}
```

The named member may also return a context-aware predicate:

```csharp
[QueryFilter(FilterLambda = nameof(ForTenant))]
public sealed class Document
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public static Expression<Func<Document, IDataContext, bool>> ForTenant()
        => (d, c) => d.TenantId == (int)c.Properties["tenant"];
}
```

`AllowMultiple = true` means several `[QueryFilter]` attributes can be placed on one type; they are combined with `and` just like repeated fluent calls.

## Reading the executing context

A filter whose lambda takes `IDataContext` reads the value from the context at execution time:

```csharp
public static Expression<Func<Document, IDataContext, bool>> ForTenant()
    => (d, c) => d.TenantId == (int)c.Properties["tenant"];
```

The tenant value is not inlined into the SQL. It becomes a **runtime parameter**, and the filter's identity (not its current values) is part of the plan key, so:

- two contexts with different tenant values can share the same cached plan;
- each execution reads its **own** context value, even on a plan-cache hit.

The same predicate compiles and re-reads the context on every invocation in the in-memory provider.

## Disabling filters for one query

[`IgnoreFilters()`](xref:NextORM.Core.EntityBuilder`1.IgnoreFilters) returns a copy of the builder with the filters disabled for that query — for the query's entity type and for every entity joined by it:

```csharp
var deleted = ctx.From<Document>()
    .IgnoreFilters()
    .Where(d => d.IsDeleted)
    .Select(d => d.Id)
    .ToList();
```

```csharp
public EntityBuilder<TEntity> IgnoreFilters();
```

The original builder is unchanged; only the returned copy ignores the filters.

## Scope: primary source, joins and subqueries

A filter is attached to the entity type and is injected wherever that type appears in the query shape:

| Position | Injected |
|---|---|
| Primary `FROM` | into the statement's `WHERE`, combined with an explicit `Where` |
| Join source | into the join's `ON` condition for the joined entity |
| Subquery | into the subquery when it is prepared |

`IgnoreFilters()` disables all of them for the query.

## In-memory provider

The in-memory provider applies the same predicate to the registered sequence before projection and joins, so soft-delete and multi-tenancy queries behave identically to SQL providers. `IgnoreFilters()` is honoured there too.

## Metadata

A declared filter is exposed through [`IQueryFilterMetadata`](xref:NextORM.Core.IQueryFilterMetadata):

```csharp
public interface IQueryFilterMetadata
{
    string? Key { get; }          // null for an anonymous filter
    LambdaExpression Lambda { get; }
}
```

`Key` is reserved for named filters; a Phase-1 filter always reports `null`.

## Limitations

- **Process-global, first-registration-wins.** Filters are registered **process-globally** and the first registration for an entity type wins (consistent with nextorm metadata): a later `From<T>(cfg)` / `HasQueryFilter` for the same type is ignored — filters are not scoped to a `DataContext`.
- **`IgnoreFilters()` scope.** `IgnoreFilters()` suppresses filters for the query's **main source**; suppressing a filter for an individual joined builder is not supported yet (Phase 2).
- **Plan lifetime.** A context value read by a filter is captured as a runtime parameter (plan-cache safe). The prepared plan retains the first `IDataContext` instance for its lifetime (bounded, one per plan shape).

## Not yet (Phase 2)

The following remain deferred and are **not** available in this release:

- keyed / named filters — `HasQueryFilter(string filterKey, ...)` and a non-`null` `IQueryFilterMetadata.Key`;
- the `FilterFunc` form — `Func<IQueryable<T>, IDataContext, IQueryable<T>>`;
- selective disabling — `IgnoreFilters(params Type[])` and key-based overloads;
- filters on DML (`INSERT` / `UPDATE` / `DELETE`) — query filters apply to reads only;
- filters on `FromSql` / raw sources.
