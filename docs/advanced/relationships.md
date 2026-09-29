# Relationships and single-query loading (`JoinInto`)

> A relationship is declared explicitly on the mapping metadata ([`HasMany`](xref:NextORM.Core.EntityMetadataBuilder`1)/[`HasOne`](xref:NextORM.Core.EntityMetadataBuilder`1) or [`[Relationship]`](xref:NextORM.Core.RelationshipAttribute)); [`JoinInto`](xref:NextORM.Core.EntityBuilder`1) then loads one child collection in a single round trip — one `LEFT JOIN` (or `INNER JOIN`) — and stitches the denormalized rows back onto deduplicated parents.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Joins](../guide/02-joins.md) · [Eager loading child collections](eager-loading.md)

## Overview

nextorm resolves a graph from **declared** metadata, not from a mapper convention: an entity without declared relationships is mapped exactly as before, and a navigation property is excluded from the column mapping only when it participates in a declared relationship. This page covers the metadata model and [`JoinInto`](xref:NextORM.Core.EntityBuilder`1), the explicit single-query child-collection loader. Implicit joins inferred from a navigation (`e.Parent.Name`) are **not** implemented.

`JoinInto` and [`LoadWith`](eager-loading.md) are two ways to fill a parent collection and share one assignment contract: `JoinInto` is one denormalized query over all parents, `LoadWith` is a split query with one extra child statement per key chunk.

## Declaring a relationship

A relationship is declared on **each side independently and symmetrically**, at mapping time with `From<T>`. The kind is derived from the navigation shape — a collection is a one-to-many, a reference is a many-to-one — and the inverse is **not** inferred.

### Fluent (`HasMany`/`HasOne`)

The principal side declares the collection navigation and the foreign key on the dependent type:

```csharp
ctx.From<Order>(b => b.HasMany(o => o.Items, i => i.OrderId));
```

The dependent side declares the reference navigation and the foreign key on **its own** type:

```csharp
ctx.From<OrderItem>(b => b.HasOne(i => i.Order, i => i.OrderId));
```

The principal key is taken from the existing key metadata of the principal type (`[Key]`, [`EntityPropertyBuilder<T>.Key()`](xref:NextORM.Core.EntityPropertyBuilder`1), or the `Id`/`<TypeName>Id` convention), so the two sides converge as a one-to-many/many-to-one pair. The four declaration overloads are:

```csharp
public EntityMetadataBuilder<T> HasMany<TChild, TKey>(
    Expression<Func<T, ICollection<TChild>>> navigation,
    Expression<Func<TChild, TKey>> foreignKey);

public EntityMetadataBuilder<T> HasOne<TChild, TKey>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, TKey>> foreignKey);
```

Each also has a **key-only fallback** overload (`HasMany<TChild, TKey>(foreignKey, principalKey)` / `HasOne<TChild, TKey>(foreignKey, principalKey)`) for an entity whose relationship is not modelled as a navigation property; it declares the metadata without attaching a navigation.

The first registration for a type wins, like a query filter: a later `From<T>(...)` configuration for the same type is ignored. A property that participates in a declared relationship is excluded from [`IEntityMetadata.Properties`](xref:NextORM.Core.IEntityMetadata) and is reachable only through [`IEntityMetadata.Relationships`](xref:NextORM.Core.IEntityMetadata); mapping it as a column throws `InvalidOperationException`. Members that are not part of a declared relationship (including a reference-typed member with no declaration) are still mapped as columns as before.

### Attribute ([`[Relationship]`](xref:NextORM.Core.RelationshipAttribute))

The same metadata can be declared declaratively on the navigation property:

```csharp
public sealed class Order
{
    public int Id { get; set; }

    [Relationship(ForeignKey = nameof(OrderItem.OrderId))]
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
```

`ForeignKey` names the foreign-key property on the dependent side; the kind is inferred from the navigation shape, exactly as for the fluent API. The attribute and a fluent declaration on the **same** navigation produce the same model, and the fluent declaration wins when both are present.

## Loading with `JoinInto`

[`JoinInto`](xref:NextORM.Core.EntityBuilder`1) declares a join that fills a parent collection when the query is enumerated by a list terminal. It returns a **copy** of the builder, so the source builder is unchanged and declarations chain:

> **Note.** The former child-collection scenario (issue #40) — loading children from a parent+child join — is expressed today by `JoinInto` into a **declared collection property**, as shown below. Projecting children into an arbitrary (anonymous) shape, the historical `NORM.ChildCollection(...)` form, is **not provided**: declare the collection and load it with `JoinInto`, or select the child rows with an explicit join and materialize them yourself.

```csharp
var orders = ctx.From<Order>()
    .JoinInto(ctx.From<OrderItem>(), (o, i) => o.Id == i.OrderId, o => o.Items)
    .ToList();
```

The three overloads are:

```csharp
// LEFT (the default)
public EntityBuilder<TEntity> JoinInto<TChild>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection);

// explicit join kind: Inner or Left only
public EntityBuilder<TEntity> JoinInto<TChild>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    JoinType joinType);

// fallback without declared metadata: explicit parent/child keys (always LEFT)
public EntityBuilder<TEntity> JoinInto<TChild, TKey>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    Expression<Func<TEntity, TKey>> parentKey,
    Expression<Func<TChild, TKey>> childKey)
    where TKey : notnull;
```

The first two resolve the parent/child keys from the declared relationship; the third is symmetrical to the [`LoadWith`](eager-loading.md) fallback for an entity pair without relationship metadata:

```csharp
var orders = ctx.From<Order>()
    .JoinInto(ctx.From<OrderItem>(), (o, i) => o.Id == i.OrderId, o => o.Items, o => o.Id, i => i.OrderId)
    .ToList();
```

`JoinInto` is **`LEFT` by default**: a parent with no children is kept with an empty collection. The explicit overload accepts only [`JoinType.Inner`](xref:NextORM.Core.JoinType) and [`JoinType.Left`](xref:NextORM.Core.JoinType); `Inner` drops childless parents.

## One round trip

For a list terminal the join is **part of the query** and the whole graph is read with one statement:

```sql
select t1.id, t1.name, t2.id, t2.order_id, t2.name
from order as t1
left join order_item as t2 on t1.id = t2.order_id
```

The rows are a denormalized `(parent, child)` stream; after the query runs, the list terminal:

1. **deduplicates parents** by their principal key (the first row's parent instance wins),
2. **groups children** by the foreign-key value,
3. assigns each child collection through the shared eager-load assignment rules (below).

Parent order is the first-occurrence order of the rows; child order follows the statement, so add an `OrderBy` when the order matters. Duplicate child rows are dropped by the child identity.

Parent deduplication and child identity both need a **mapped key** on the respective side. When the parent (or the child) declares no key, a plain `JoinInto` query falls back to **reference identity**: on a SQL provider every denormalized row materializes a fresh instance, so repeated rows are not collapsed and a cartesian product (two child collections) stays duplicated. Single-query eager loading ([`LoadWith`](eager-loading.md) with `AsSingleQuery`) has no such fallback — it requires a mapped parent key and rejects a keyless parent — so give the parent and the child a mapped key (`[Key]`, `Key()`, or the `Id`/`<TypeName>Id` convention), or use the explicit-key overload with a keyed child, for reliable deduplication. See [Limitations](limitations.md).

## Assignment rule

For every parent the loader decides how to fill the collection, identical to [`LoadWith`](eager-loading.md#assignment-rule):

| Current value of `collection` | Member settable | Behaviour |
|---|---|---|
| non-null | either | cleared and refilled |
| `null` | yes | a fresh list is assigned |
| `null` | no (read-only) | [`NotSupportedException`](xref:System.NotSupportedException) |

A read-only collection is supported as long as it is initialized with an empty collection and never reassigned:

```csharp
public sealed class Order
{
    public int Id { get; set; }
    public ICollection<OrderItem> Items { get; } = new List<OrderItem>();
}
```

## `Where` semantics

A `Where` after `JoinInto` is applied to the **joined query** — it filters the denormalized rows, like a SQL `WHERE` over the two tables:

- with `Left`, a predicate that is not null-safe (`p => p.Items.First()...` or a raw condition on the child) can drop the parent entirely when no child matches, because the SQL `LEFT JOIN ... WHERE` semantics apply;
- with `Inner` it is an ordinary filter.

A `Where` written **before** `JoinInto` filters the parent source. The predicate passed to `JoinInto` is the `ON` condition and only pairs rows; it never removes a `LEFT` parent.

## Parent paging

`Limit`/`Offset` (`Take`/`Page`) constrain **parents**, not denormalized rows. On a SQL provider the parent source is selected in a subquery with the limit and the join wraps around it, so paging a parent does not truncate its children. On the in-memory provider the limit is applied to the deduplicated parent list.

## Multiple collections

Each `JoinInto` adds its own join. Two child collections on the same parent produce a **cartesian product** of rows in the denormalized stream; parent deduplication and independent per-collection grouping keep the result correct, but the intermediate row count multiplies. Declare several collections only when the parent sets are small, or load the second collection with a separate query ([`LoadWith`](eager-loading.md) is the split-query alternative).

`JoinInto` **cannot be combined with any other join on the same builder**: once the builder carries a `JoinInto`, adding `Join`, `LeftJoin`, `CrossJoin`, `SemiJoin`, `AntiJoin` (or any other explicit join) throws `NotSupportedException`, and vice versa. Declare the `JoinInto` collections on a plain entity source, so the non-list terminals know exactly which joins to drop. Mixing the two would leave the parent-only command unable to tell an explicit join from a `JoinInto`.

## Join modifiers

The join modifiers of the underlying join are available. On ClickHouse, [`WithStrictness`](xref:NextORM.Core.EntityBuilder`1.WithStrictness(NextORM.Core.JoinStrictness)) may be chained after `JoinInto(...)`: `WithStrictness(Any)` keeps only the **first** matching child per parent, so the loaded collection is truncated to at most one element; the other strictness modifiers (`All`, `Asof`) and [`Global`](xref:NextORM.Core.EntityBuilder`1.Global) pass through unchanged. A modifier applied to a `JoinInto` preserves the parent-only terminal behaviour — the non-list terminals still exclude the join.

## Terminal boundary

The join is part of the command and is stitched **only** by the stitching terminals:

- [`ToList()`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) and [`ToListAsync()`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])).
- `ToArray()` and `ToArrayAsync()`.

[`ToCommand()`](xref:NextORM.Core.EntityBuilder`1.ToCommand) keeps the join but returns only the **denormalized** `(parent, child)` rows, so a bare enumeration may repeat a parent once per matching child. Every other terminal built on the parent command — `First`, `Count`, `Any`, `ToHashSet`, `ToEnumerable`, `ToAsyncEnumerable`, [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) — excludes the join and returns the **parent only**, with the collections not filled. A `Select` projection or an `As` derived source is rejected with [`NotSupportedException`](xref:System.NotSupportedException): `Select` would keep the join without stitching (repeating parents, never grouping children) and `As` would drop the stitching metadata. Materialize with `ToList`/`ToListAsync` when the collections must be filled.

## In-memory parity

`JoinInto` works on the in-memory provider: the join and the grouping run as delegates, and the LEFT/INNER semantics, parent deduplication, ordering and empty collections match the SQL providers. The in-memory provider pages the deduplicated parents instead of wrapping a parent subquery.

## `NotSupportedException` boundaries

The following are rejected with [`NotSupportedException`](xref:System.NotSupportedException):

- `JoinInto` over a relationship declared as **many-to-many** or **one-to-one** (the metadata model can represent the kinds; loading them is not implemented);
- a **composite** principal or foreign key on the relationship (single-column keys only);
- an **undeclared** relationship, when the collection overload is used without relationship metadata — declare it with `HasMany`/`HasOne`/`[Relationship]` or use the explicit-key overload;
- `JoinInto` applied to a derived (`As`) or joined projection source;
- a `Select` projection or an `As` derived source applied to a builder carrying a `JoinInto` — both drop the stitching contract (repeated parents or lost metadata), so materialize with `ToList`/`ToListAsync` instead;
- `JoinInto` combined with another join on the same builder (adding any explicit join once a `JoinInto` is declared throws, and adding a `JoinInto` once an explicit join exists throws) — declare `JoinInto` on a plain entity source only;
- a join kind other than `Inner`/`Left`;
- explicit-key selectors that select **different property types**, or that are not a simple property/field access (a computed key has no stable plan identity);
- a read-only collection member that is `null` at runtime;
- more than seven child collections on one query (the projection arity cap).

The relationship metadata itself fails closed too: an FK property that is not mapped on the dependent type, a principal type with no key, or an FK/principal-key type mismatch throws `NotSupportedException` when the keys are resolved.

## Relationship metadata

The metadata model is exposed through [`IEntityMetadata.Relationships`](xref:NextORM.Core.IEntityMetadata); each entry is an [`IRelationshipMetadata`](xref:NextORM.Core.IRelationshipMetadata) with [`Kind`](xref:NextORM.Core.IRelationshipMetadata) ([`RelationshipKind`](xref:NextORM.Core.RelationshipKind)), `DeclaringType`, `RelatedType`, `Navigation`, `IsCollection`, `ForeignKey`, `PrincipalKey` and `Inverse`. `Inverse` points at the other side's metadata when both sides are declared and agree on the foreign key and types, and is `null` otherwise — it is never inferred. `Inverse` is resolved **lazily on its first read and cached**: register the related type (and the declaring type) before reading it, because a read taken before the other side is registered caches `null` and will not be refreshed by a later registration.

## See also

- [Eager loading child collections (`LoadWith`)](eager-loading.md) — the split-query alternative.
- [Joins](../guide/02-joins.md) — the explicit join surface.
- [Entities and metadata](../getting-started/03-entities-and-metadata.md) — key and column mapping.

---

Source: `tests/nextorm.sqlite.tests/JoinIntoSqlGenerationTests.cs:23`,
`tests/nextorm.sqlite.tests/JoinIntoExecutionTests.cs:43`,
`tests/nextorm.core.tests/JoinIntoInMemoryTests.cs:31`,
`tests/nextorm.core.tests/RelationshipMetadataTests.cs:173`.
