# Eager loading child collections (`LoadWith`)

> `LoadWith` fills a parent-side collection with one extra child query per key chunk — two round trips for at most 1000 parents, never N+1 — and stitches the children onto the already materialized parents in memory. Split is the default; call `AsSingleQuery()` to fetch the parents and every declared collection in one denormalized round trip instead.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Joins](../guide/02-joins.md) · [Correlated queries](../guide/05-subqueries.md)

## Overview

nextorm does not infer relationships, so a graph is not loaded from a mapper convention. `LoadWith` declares one level of eager loading explicitly: a parent collection member, a factory that builds the child query, and the two key selectors that pair children with parents — it does not consume relationship metadata. The association lives on the query, not in the entity metadata; for the declared-metadata, single-round-trip alternative see [`JoinInto`](relationships.md).

This is the nextorm equivalent of linq2db `LoadWith` and EF Core `Include`, restricted to level one (no nested loads). Split-query is the default; an opt-in single-query mode (`AsSingleQuery`) issues one denormalized round trip instead.

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

When `ToListAsync`/`ToArrayAsync` is cancelled, the token is checked between key chunks and between multiple `LoadWith` declarations, so a cancelled call stops issuing further child statements.

## Single-query mode (`AsSingleQuery`)

By default `LoadWith` uses the split shape above — two or more round trips, one per key chunk. Call `AsSingleQuery()` on the builder to fetch the parents and every declared collection with **one** denormalized command instead:

```csharp
var orders = ctx.From<Order>()
    .Where(o => o.CustomerId == customerId)
    .LoadWith(o => o.Items, c => c.From<OrderItem>(), o => o.Id, i => i.OrderId)
    .AsSingleQuery()
    .ToList();
```

- **One round trip, no key chunking.** The parent source and each declared child are joined in a single `LEFT JOIN` command. There is no `IN` list and no chunk size, so any number of parents — including more than 1000 distinct keys — is fetched by the same command, and there is no silent fallback to split.
- **Only the child query's own `Where` is folded in.** A child condition, for example `c => c.From<OrderItem>().Where(i => i.Active)`, is merged into the join `ON` predicate, so it filters the children without dropping childless parents.
- **Other child-query shapes are rejected.** A single denormalized join cannot apply per-parent modifiers, joins or table-level clauses, so a child query carrying any of `OrderBy`, `Limit`/`Offset`/`Page`, `Distinct`, `GroupBy`, `Having`, `DistinctOn`, `LimitBy`, a join (`Join`/`LeftJoin`/`SemiJoin`/`AntiJoin`/…), `Final`, `PreWhere`, `ArrayJoin`, `TableSample`, `Sample`, `ForSystemTime`, `ForUpdate`/`ForShare`, `Window`, `Settings`, a CTE, a table/index hint or a derived (`As`) source throws [`NotSupportedException`](xref:System.NotSupportedException) as soon as `AsSingleQuery()` is materialized. Split mode honors every one of them; use split (omit `AsSingleQuery`) when the child query needs a shape beyond `Where`.
- **Parent and child filter scopes combine by union, identically in split and single-query mode.** The child's effective scope is always the union of its own `IgnoreFilters` scope and the parent's. A parent `IgnoreFilters()` disables the declared children's filters too (the all-or-nothing scope absorbs the child's own scope), and a selective parent `IgnoreFilters(...)` reaches the children as well. Conversely, an `IgnoreFilters(...)` applied inside the child query disables only that child's filters while the parent's still apply. The same rule holds whether you load with the default split shape or with `AsSingleQuery()`.
- **The parent type must declare a mapped key.** Denormalized rows are deduplicated by that key, so a keyless parent type throws [`NotSupportedException`](xref:System.NotSupportedException) with guidance to configure a key or use split mode, which does not require one. Rows that share the same mapped key collapse into one parent; split mode keeps them separate.
- **The key type must support value equality.** The join predicate is built with the equality operator, so a key type without one — for example a struct with no `==` overload, or a reference type whose `==` would degrade to reference equality — throws [`NotSupportedException`](xref:System.NotSupportedException). Split mode compares keys with the default equality comparer and works for such types.
- **Assignment and ordering are unchanged.** The same stitcher deduplicates the parents and fills each collection, so the [assignment rule](#assignment-rule) and [ordering guarantees](#ordering-and-deduplication) below hold in both modes.
- **One plain entity source only.** `AsSingleQuery` cannot be combined with other joins on the same builder; declaring it on a query that already carries a regular (non-`JoinInto`) join throws [`NotSupportedException`](xref:System.NotSupportedException).
- **Cannot be composed further.** `LoadWith`/`AsSingleQuery` cannot be followed by `Select` or `As` (a projection command has no loader) or by `Join`/`Apply` (a joined builder carries neither the load specifications nor the mode). Each of those compositions throws [`NotSupportedException`](xref:System.NotSupportedException) instead of silently leaving the collections empty; materialize with a stitching terminal first.

Split stays the default: prefer it when the chunked child result is smaller than the denormalized join product, and opt into `AsSingleQuery` when a single round trip matters more.

## Level one only

Only the declared children are loaded. The child query is a plain `EntityBuilder<TChild>`; it carries no loader of its own and nested `LoadWith` calls are not honoured, so grandchild collections are not populated. N-level eager loading (level two and deeper) is **not supported** in either split or single-query mode; load a second level with a separate query if needed.

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
- **Duplicate parent keys share the data (split).** Two parents with the same key each receive a collection with the same children (each parent has its own collection instance). In single-query mode the mapped parent key is the deduplication identity, so duplicate-key rows collapse into one parent — use split mode when the duplicate rows must stay distinct.
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

The load specification is honoured **only** by the four stitching terminals:

- [`ToList()`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) and [`ToListAsync()`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])).
- `ToArray()` and `ToArrayAsync()`.

These run the parent query — plus the split child queries, or the single denormalized command with `AsSingleQuery()` — and stitch the materialized rows into the collections.

Every other terminal evaluates the parent query **only** and does not run eager loading; it issues no child or load query, so the collections keep whatever value the entity was materialized with (usually empty):

- `ToHashSet` / `ToHashSetAsync` and `ToDictionary` / `ToDictionaryAsync`.
- `First` / `FirstAsync` and `FirstOrDefault` / `FirstOrDefaultAsync`.
- `Single` / `SingleAsync` and `SingleOrDefault` / `SingleOrDefaultAsync`.
- `ToEnumerable` and `ToAsyncEnumerable`.
- `ToCommand` and any `QueryCommand<T>` obtained from it.

`Any` / `AnyAsync` and `Count` / `CountAsync` are **scalar** terminals: they return a single value and never run eager loading either. `ToCommand()` ignoring the specification is deliberate — it is what lets the child query run without recursing into the parent's loader. Materialize the parents with one of the four stitching terminals when the collections must be filled.

## Relationship metadata and `JoinInto`

`LoadWith` does not create or consume navigation metadata; the association is expressed by the two key selectors at each call site. nextorm also has a metadata model for **declared** relationships (`HasMany`/`HasOne` or [`[Relationship]`](xref:NextORM.Core.RelationshipAttribute)) and a single-query loader, [`JoinInto`](relationships.md), which fills one child collection from one `LEFT JOIN` (or `INNER JOIN`) and stitches the denormalized rows back onto the deduplicated parents. Use `JoinInto` when the association is declared as relationship metadata, and `LoadWith` when it is expressed ad hoc by key selectors at the call site; `LoadWith` with `AsSingleQuery()` also reduces to one denormalized query, so the two share the same single-query materialization, assignment contract and ordering guarantees. Relationships are still never inferred from conventions or foreign keys. A graph is loaded explicitly, one declared level per call.
