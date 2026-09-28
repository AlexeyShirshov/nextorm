# Eager loading child collections (`LoadWith`)

> `LoadWith` fills a parent-side collection with one extra child query per key chunk — two round trips for at most 1000 parents, never N+1 — and stitches the children onto the already materialized parents in memory.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Joins](../guide/02-joins.md) · [Correlated queries](../guide/05-subqueries.md)

## Overview

nextorm does not infer relationships, so a graph is not loaded from a mapper convention. `LoadWith` declares one level of eager loading explicitly: a parent collection member, a factory that builds the child query, and the two key selectors that pair children with parents — it does not consume relationship metadata. The association lives on the query, not in the entity metadata; for the declared-metadata, single-round-trip alternative see [`JoinInto`](relationships.md).

This is the nextorm equivalent of linq2db `LoadWith` and EF Core `Include`, restricted to the level-one split-query shape.

## Signature

```csharp
public EntityBuilder<TEntity> LoadWith<TChild, TKey>(
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    Func<IDataContext, EntityBuilder<TChild>> childQuery,
    Expression<Func<TEntity, TKey>> parentKey,
    Expression<Func<TChild, TKey>> childKey)
    where TKey : notnull;
```

- `collection` — the parent-side [`ICollection<TChild>`](xref:System.Collections.Generic.ICollection`1) member to fill.
- `childQuery` — builds the child query from the data context that owns the parent query.
- `parentKey` / `childKey` — the selectors paired to decide which children belong to which parent.
- `TKey` must be a non-nullable type (`where TKey : notnull`), for example `int` or `string`. A null key value observed at runtime is skipped and never matches.

`LoadWith` returns a **copy** of the builder carrying the declaration, so the original builder is unchanged and several loads can be chained:

```csharp
var orders = ctx.From<Order>()
    .Where(o => o.CustomerId == customerId)
    .LoadWith(o => o.Items, c => c.From<OrderItem>(), o => o.Id, i => i.OrderId)
    .OrderBy(o => o.Id)
    .ToList();
```

Like every builder modifier, `LoadWith` needs the generic key type to be known at the call site; when a selector is implicitly convertible, spell the key type out or make both selector bodies produce the same type.

## Split-query semantics

The declaration is **not** part of the parent SQL. After the parent query is materialized:

1. The distinct, non-null parent keys are collected **in parent order** and split into chunks of at most **1000** keys.
2. One child query per chunk runs `WHERE childKey IN (<chunk keys>)` — a separate statement, no `JOIN` and no per-parent query.
3. The children are grouped by `childKey` and assigned to the parents whose `parentKey` matches.

A query with at most 1000 distinct parent keys therefore issues exactly **two round trips**: one parent statement and one child statement. Larger key sets add one child statement per additional chunk; the parent statement still runs once. There is no N+1: the number of child statements depends on the size of the key set, not on the number of parents.

The chunk predicate is an ordinary `IN` list, so it participates in the plan cache and provider parameter binding exactly like a captured-collection `Contains`.

## Level one only

Only the declared children are loaded. The child query is a plain `EntityBuilder<TChild>`; it carries no loader of its own and nested `LoadWith` calls are not honoured, so grandchild collections are not populated. Load a second level with a separate query if needed.

## Assignment rule

For every parent the loader decides the target collection:

| Current value of `collection` | Member settable | Behaviour |
|---|---|---|
| non-null | either | cleared and refilled |
| `null` | yes | a fresh list is assigned |
| `null` | no (read-only) | [`NotSupportedException`](xref:System.NotSupportedException) |

A read-only collection is therefore supported as long as it is initialized with an empty collection and never reassigned:

```csharp
public sealed class Order
{
    public int Id { get; set; }
    public ICollection<OrderItem> Items { get; } = new List<OrderItem>();
}
```

A read-only member is not mapped as a database column, so initializing it does not affect the parent `SELECT`.

## Ordering and deduplication

- **Parent order is preserved.** Children are assigned to the parents in the order the parent query returned them; the loader never reorders the parents.
- **Child order follows the child query.** Children keep the order the child statement returned them in, so add an `OrderBy` to `childQuery` when the order matters.
- **Duplicate parent keys share the data.** Two parents with the same key each receive a collection with the same children (each parent has its own collection instance).
- **Unmatched children are ignored.** A child whose key matches no parent is never assigned, and a parent whose key has no children gets an empty collection.
- **Null keys never match.** A parent or child key that is `null` at runtime is skipped; it is never looked up and never crashes the grouping.

## Duplicate declarations

A collection member can be declared **only once** per query. Calling `LoadWith` twice for the same member throws [`InvalidOperationException`](xref:System.InvalidOperationException), because the two declarations would execute two child queries for the same collection and the later assignment would discard the earlier load. Declare each member at most once and use distinct members to load several collections:

```csharp
ctx.From<Order>()
    .LoadWith(o => o.Items, c => c.From<OrderItem>(), o => o.Id, i => i.OrderId)
    .LoadWith(o => o.Payments, c => c.From<Payment>(), o => o.Id, p => p.OrderId); // a different member
```

## Terminal boundary

The load specification is honoured **only** by the list terminals:

- [`ToList()`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) and [`ToListAsync()`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])).

`ToCommand()` ignores it deliberately, which is what lets the child query run without recursing into the parent's loader. Every other terminal built on `ToCommand()` — `First`, `Count`, `Any`, a `Select` projection, `ToAsyncEnumerable`, and a `QueryCommand<T>` obtained directly — therefore returns the parent rows **without** touching the child collections. Materialize the parents with `ToList`/`ToListAsync` when the collections must be filled.

## Relationship metadata and `JoinInto`

`LoadWith` does not create or consume navigation metadata; the association is expressed by the two key selectors at each call site. nextorm also has a metadata model for **declared** relationships (`HasMany`/`HasOne` or [`[Relationship]`](xref:NextORM.Core.RelationshipAttribute)) and a single-query loader, [`JoinInto`](relationships.md), which fills one child collection from one `LEFT JOIN` (or `INNER JOIN`) and stitches the denormalized rows back onto the deduplicated parents. Use `JoinInto` when one denormalized round trip is preferable, and `LoadWith` when the split query is; both share the same assignment contract. Relationships are still never inferred from conventions or foreign keys. A graph is loaded explicitly, one declared level per call.
