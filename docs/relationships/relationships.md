# Relationships and single-query loading (`JoinInto`)

> A relationship is declared explicitly on the mapping metadata ([`HasMany`](xref:NextORM.Core.EntityMetadataBuilder`1)/[`HasOne`](xref:NextORM.Core.EntityMetadataBuilder`1) or [`[Relationship]`](xref:NextORM.Core.RelationshipAttribute)); [`JoinInto`](xref:NextORM.Core.EntityBuilder`1) then loads a declared navigation in a single round trip and stitches the denormalized rows back onto deduplicated parents: a one-to-many collection through one `LEFT JOIN` (or `INNER JOIN`), a one-to-one nullable reference through one join, and a many-to-many collection through a junction with two flat joins (a derived link join plus the child join).

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Joins](../guide/04-joins.md) · [Eager loading child collections](eager-loading.md)

## Overview

nextorm resolves a graph from **declared** metadata, not from a mapper convention: an entity without declared relationships is mapped exactly as before, and a navigation property is excluded from the column mapping only when it participates in a declared relationship. This page covers the metadata model and [`JoinInto`](xref:NextORM.Core.EntityBuilder`1), the explicit single-query relationship loader. Implicit navigation queries are supported for **declared** relationships — a reference navigation as a scalar chain or a presence check, and a collection navigation through exactly four direct terminals (see [Implicit navigation queries](implicit-navigation.md)) — not inferred by convention. Navigation **after** a temp-table/TVP boundary is **not available (fail-closed)**, and a table-valued parameter is parameter-only (no query root).

`JoinInto` and [`LoadWith`](eager-loading.md) are two ways to fill a parent collection and share one assignment contract: `JoinInto` is one denormalized query over all parents, and `LoadWith` defaults to a split query with one extra child statement per key chunk (or one denormalized query when the builder passes `EagerLoadMode.SingleQuery` to `LoadWith`). `JoinInto` fills **one** declared navigation; a multi-level (level 2+) graph is expressed by nesting `LoadWith` child queries, where each level runs in one graph-wide mode (see [Nested graphs](eager-loading.md#nested-graphs-level-2-and-deeper)).

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

The principal key is taken from the existing key metadata of the principal type (`[Key]`, [`EntityPropertyBuilder<T>.Key()`](xref:NextORM.Core.EntityPropertyBuilder`1), or the `Id`/`<TypeName>Id` convention), so the two sides converge as a one-to-many/many-to-one pair. The default principal key is the **whole** key in entity-metadata order — every key component, never only the first one.

`HasMany`, `HasOne`, `HasOneToOne` and `HasManyThrough` expose ten declaration overloads (A–J). All ten share one selector parser and build the same internal relationship descriptor; the six existing signatures are unchanged and keep working. The ten signatures are:

```csharp
// A — 1:N, navigation + explicit foreign key + explicit principal key
public EntityMetadataBuilder<T> HasMany<TChild>(
    Expression<Func<T, ICollection<TChild>>> navigation,
    Expression<Func<TChild, object?>> foreignKey,
    Expression<Func<T, object?>> principalKey) where TChild : class;

// B — 1:N, no navigation, explicit foreign key + explicit principal key
public EntityMetadataBuilder<T> HasMany<TChild>(
    Expression<Func<TChild, object?>> foreignKey,
    Expression<Func<T, object?>> principalKey) where TChild : class;

// C — N:1, navigation + explicit foreign key + explicit principal key
public EntityMetadataBuilder<T> HasOne<TChild>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, object?>> foreignKey,
    Expression<Func<TChild, object?>> principalKey) where TChild : class;

// D — N:1, no navigation, explicit foreign key + explicit principal key
public EntityMetadataBuilder<T> HasOne<TChild>(
    Expression<Func<T, object?>> foreignKey,
    Expression<Func<TChild, object?>> principalKey) where TChild : class;

// E — 1:1, navigation + explicit parent key + explicit child foreign key
public EntityMetadataBuilder<T> HasOneToOne<TChild>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, object?>> parentKey,
    Expression<Func<TChild, object?>> childForeignKey) where TChild : class;

// F — M:N, navigation + all four explicit key lists
public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction>(
    Expression<Func<T, IEnumerable<TChild>>> navigation,
    Expression<Func<T, object?>> parentKey,
    Expression<Func<TJunction, object?>> junctionParentForeignKey,
    Expression<Func<TChild, object?>> childKey,
    Expression<Func<TJunction, object?>> junctionChildForeignKey)
    where TChild : class where TJunction : class;

// G — 1:N, no navigation; principal = the full key of T
public EntityMetadataBuilder<T> HasMany<TChild>(
    Expression<Func<TChild, object?>> foreignKey) where TChild : class;

// H — N:1, no navigation; principal = the full key of TChild
public EntityMetadataBuilder<T> HasOne<TChild>(
    Expression<Func<T, object?>> foreignKey) where TChild : class;

// I — 1:1 with the child foreign key only; parent key = the full key of T
public EntityMetadataBuilder<T> HasOneToOne<TChild, TKey>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<TChild, TKey>> childForeignKey) where TChild : class;

// J — M:N with the two junction foreign keys only; both entity keys = full keys
public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction>(
    Expression<Func<T, IEnumerable<TChild>>> navigation,
    Expression<Func<TJunction, object?>> junctionParentForeignKey,
    Expression<Func<TJunction, object?>> junctionChildForeignKey)
    where TChild : class where TJunction : class;
```

When each form is required: **A, C, E and I** infer `TChild` from the navigation; **I** additionally infers `TKey` from the child foreign key, so a composite anonymous key needs no explicit type argument. The key-only forms **B, D, G and H** cannot infer `TChild` from an untyped lambda — call them with an explicit `<TChild>` (a typed selector variable can still infer it, but the public form is explicit). **F and J** require explicit `<TChild, TJunction>`: `TJunction` appears only inside untyped lambdas, and C# cannot supply a partial generic-argument list. Use **A–F** when the key must be explicit — a different component order, a non-key principal property, or a reversed role; use **G–J** when the full primary key is the principal key. To customise only one side of a many-to-many relationship use **F** with all four lists; there is no partially ordered overload.

#### Ordered key selectors

Every key selector — fluent or attribute — is resolved by one shared parser, so all declarations accept exactly the same forms:

- a direct scalar mapped property — `c => c.ParentId`;
- a flat anonymous object of direct scalar mapped properties — `c => new { c.TenantId, c.ParentId }`.

The **order is the contract**. Components are paired **positionally** between the two sides: the first foreign-key component pairs with the first principal-key component, the second with the second, and so on. They are never matched by name, sorted by key metadata, or matched by type. Anonymous-member aliases are ignored: `new { Parent = c.ParentId }` selects `ParentId`. Boxing and nullable conversions around a single component are unwrapped before the underlying mapped CLR types are compared.

A `null` selector (or a selector without a body) throws `ArgumentNullException` naming the parameter. Structural rejections throw `NotSupportedException` naming the relationship, the component position and the component count: field access, a nested member path, a computation, a method call, an index access, an array or collection initializer, a tuple literal, DTO or object construction (`new Dto { ... }`), a nested anonymous object, an empty anonymous object, and the same property selected twice.

#### Default and explicit principal keys

An overload without a `principalKey` parameter uses the **whole** primary key of the principal entity in entity-metadata order — the members flagged `IsKey` in the order `IEntityMetadata.Properties` lists them. There is **no first-key fallback** and **no name matching**: a foreign key that selects fewer components than the full principal key is an arity error when the metadata is resolved, not a silent use of the first key. An explicit principal key may select mapped **non-key** properties (an alternate logical key) and any component order; the two lists must still have the same length and a matching nullable-unwrapped type at every position. `HasManyThrough` applies the same rule to both legs: each entity key and the junction foreign key that references it must have the same component count and the same positional types.

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

`ForeignKey` names the single foreign-key property on the dependent side; the kind is inferred from the navigation shape, exactly as for the fluent API. A composite relationship uses the two arrays instead:

```csharp
public sealed class Order
{
    public int TenantId { get; set; }
    public int Id { get; set; }

    [Relationship(
        ForeignKeys = new[] { nameof(OrderItem.TenantId), nameof(OrderItem.OrderId) },
        PrincipalKeys = new[] { nameof(Order.TenantId), nameof(Order.Id) })]
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
```

Exactly one of `ForeignKey` and `ForeignKeys` must be set. `ForeignKeys` is an **ordered** list; the component pairing is positional, so the names on the two sides may differ. `PrincipalKeys` is optional: when omitted, the principal key is the whole key of the principal entity in entity-metadata order, and an explicit list may select mapped non-key properties. Every name must name a mapped property; an empty or missing foreign-key form, an empty array, a duplicate name, an unmapped or missing member, an arity mismatch, or a positional type mismatch is rejected with `NotSupportedException` when the metadata is resolved. The attribute declares one-to-many and many-to-one relationships only — one-to-one and many-to-many are fluent-only.

The attribute and a fluent declaration on the **same** navigation produce the same model, and the fluent declaration wins when both are present; a shadowed attribute is not validated. `[Relationship]` and the fluent overloads share one selector language and one set of validation rules.

### Validation timing

Validation is staged:

- **At declaration** — a `null` selector throws `ArgumentNullException`, and the selector syntax (scalar property or flat anonymous object only) is checked by the shared parser.
- **When the relationship metadata is resolved** — everything that needs the other type: mapped-property resolution, the default full principal key, arity, duplicate components and positional nullable-unwrapped types. Registration order does not matter: `From<T>()` does not eagerly validate a cross-reference that is not registered yet, and the related type may be configured later.
- **Before SQL and before a cached plan** — a relationship is validated when it is first consumed, and validation completes **before any SQL is generated** and **before a plan is inserted into the plan cache**. An invalid or partially resolved declaration never produces SQL and never caches a plan; correcting the declaration afterwards is not defeated by a cached failure.

The same structural validation applies to manually supplied [`IRelationshipMetadata`](xref:NextORM.Core.IRelationshipMetadata) and junction lists: at least one component per list, equal arity on every leg, no duplicate components, every component resolving by semantic mapped-member identity to a configured mapped property, and pairwise nullable-unwrapped CLR types. A custom property-metadata instance does not have to be the same object as the configured one; a valid escape-hatch mapping — including an explicit non-key principal key — keeps working, while a malformed list throws `NotSupportedException` before SQL and before a cache insert.

`Inverse` participates in demand-time validation only: a direct navigation needs no inverse, and an unrelated declaration does not fail. When the requested operation needs the inverse, an absent inverse resolves to `null`, while an **ambiguous** one — two different navigation properties exposing the same canonical mapping — throws `NotSupportedException` naming both conflicting candidates.

## Loading with `JoinInto`

[`JoinInto`](xref:NextORM.Core.EntityBuilder`1) declares a join that fills a declared navigation — a child collection (one-to-many), a nullable reference (one-to-one) or a junction-backed many-to-many collection — when the query is enumerated by a list terminal. It returns a **copy** of the builder, so the source builder is unchanged and declarations chain:

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

### One-to-one reference

A one-to-one relationship is declared with the principal key on the parent and the unique foreign key on the child; the foreign-key uniqueness is a **declaration-time trust** — the core neither inspects the schema or the data nor imposes a uniqueness constraint, so it is not validated at build time:

```csharp
ctx.From<Order>(b => b.HasOneToOne(o => o.Invoice, o => o.Id, i => i.OrderId));
```

Loading uses the reference-navigation `JoinInto` overloads, which assign the single joined child to the parent's reference member:

```csharp
var orders = ctx.From<Order>()
    .JoinInto(ctx.From<Invoice>(), (o, i) => o.Id == i.OrderId, o => o.Invoice)
    .ToList();
```

The reference renders the same flat join as the collection form; the joined child is materialized from the paired row:

```sql
select t1.id, t1.name, t2.id, t2.order_id, t2.total
 from order as t1
 left join invoice as t2 on t1.id = t2.order_id
```

`JoinOptions.OneToOne<TParent, TChild, TKey>(parentKey, childForeignKey)` configures the relationship locally through a `JoinInto` options lambda (the same two selectors) when it is not declared, fully replacing the declared metadata for that call:

```csharp
var orders = ctx.From<Order>()
    .JoinInto(
        ctx.From<Invoice>(),
        (o, i) => o.Id == i.OrderId,
        o => o.Invoice,
        j => j.OneToOne<Order, Invoice, int>(o => o.Id, i => i.OrderId))
    .ToList();
```

The LEFT/INNER semantics mirror the collection loaders: with `Left` (the default) a parent without a matching child keeps the reference `null`, and with `Inner` a parent without a matching child is excluded. The build-time uniqueness **trust** above is backed only per parent at load time: a parent that matches **more than one distinct non-null child identity** throws [`InvalidOperationException`](xref:System.InvalidOperationException) at materialization, while repeated rows of the **same** child identity (including cartesian repeats caused by a neighbouring join) and keyless/null-identity repeats are tolerated and collapse to the first child. The foreign-key uniqueness itself is **never** validated against the schema or the data, so a non-unique foreign key is accepted when the relationship is declared. The navigation member must be settable — a read-only reference throws [`NotSupportedException`](xref:System.NotSupportedException).

### Many-to-many through a junction

A many-to-many relationship is declared with the explicit junction (link) entity — the core never infers it. `HasManyThrough` takes the collection navigation, the keys on both principal sides and the two junction foreign keys that reference them:

```csharp
ctx.From<Post>(b => b.HasManyThrough<Tag, TagLink, int, int>(
    p => p.Tags,      // collection navigation on the principal
    p => p.Id,        // parent key
    l => l.PostId,    // junction FK referencing the parent key
    t => t.Id,        // child key
    l => l.TagId));   // junction FK referencing the child key
```

Each junction foreign key must match the type of the key it references. Loading uses the collection `JoinInto` overload with a local `JoinOptions.ManyToMany` configuration (the same four selectors), which fully replaces the declared metadata for that call:

```csharp
var posts = ctx.From<Post>()
    .JoinInto(
        ctx.From<Tag>(),
        (p, t) => t.Active,
        p => p.Tags,
        j => j.ManyToMany<Post, Tag, TagLink, int, int>(
            p => p.Id, l => l.PostId, t => t.Id, l => l.TagId))
    .ToList();
```

The junction is projected as a derived `row_number()` link subquery, joined flat to the parent and then to the child; the user predicate is appended to the child `ON`:

```sql
select t1.id, t1.title, t2.parent_key, t2.child_key, t2.occurrence, t3.id, t3.name
 from post as t1
 left join (
    select j.post_id as parent_key, j.tag_id as child_key,
           row_number() over (partition by j.post_id, j.tag_id order by j.tag_id) as occurrence
 from tag_link as j
) as t2 on t1.id = t2.parent_key
 left join tag as t3 on t3.id = t2.child_key and t3.active
```

The `(parent, child) => ...` predicate is an extra filter over the joined rows; the join keys come from the junction configuration, not from the predicate. Use `(p, t) => true` when no filter is needed.

The LEFT/INNER semantics are the same as for a one-to-many collection: with `Left` a parent without a junction row is kept with an empty collection, and a junction row that references a missing child contributes no element; with `Inner` a parent without a matching child is excluded. Multiplicity differs in one place: **duplicate `(parent, child)` junction rows are preserved as distinct elements** — the same child appears once per junction row — whereas a one-to-many join collapses duplicate child rows by child identity.

When a query carries two or more collection navigations (one-to-one is excluded) the preparation emits the `JoinInto.MultipleCollections` warning once per plan, because the intermediate row count multiplies into a cartesian product. Pass `JoinOptions.SuppressCartesianWarning()` through a `JoinInto` options lambda (`j => j.SuppressCartesianWarning()`) to silence it; the warning is informational and does not change the result.

A junction whose parent and/or child foreign key spans several **components** is fully supported: the join and the occurrence partition use every declared component, so changing any component (not only the first) changes the linked child, and repeated junction occurrences stay distinct. The single-query dispatcher ([`EagerLoadMode.SingleQuery`](eager-loading.md)) handles such a junction too. Declare the components with the composite fluent overloads (A–J) or with `ForeignKeys`/`PrincipalKeys` on the attribute; the legacy `ForeignKey` form remains for a single component. A many-to-many `JoinInto` takes **two** projection slots, so it counts double against the arity cap.

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

Parent deduplication and child identity both need a **mapped key** on the respective side. When the parent (or the child) declares no key, a plain `JoinInto` query falls back to **reference identity**: on a SQL provider every denormalized row materializes a fresh instance, so repeated rows are not collapsed and a cartesian product (two child collections) stays duplicated. Single-query eager loading ([`LoadWith`](eager-loading.md) with `EagerLoadMode.SingleQuery`) has no such fallback — it requires a mapped parent key and rejects a keyless parent — so give the parent and the child a mapped key (`[Key]`, `Key()`, or the `Id`/`<TypeName>Id` convention), or use the explicit-key overload with a keyed child, for reliable deduplication. See [Limitations](../advanced/limitations.md).

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

```sql
select t1.id, t1.name, t2.id, t2.order_id, t2.name
 from (select id, name
 from order limit 10 offset 20) as t1
 left join order_item as t2 on t1.id = t2.order_id
```

## Multiple collections

Each `JoinInto` adds its own join. Two child collections on the same parent produce a **cartesian product** of rows in the denormalized stream; parent deduplication and independent per-collection grouping keep the result correct, but the intermediate row count multiplies. When two or more collection navigations are present, preparation emits the `JoinInto.MultipleCollections` warning once per plan; pass `JoinOptions.SuppressCartesianWarning()` through a `JoinInto` options lambda to silence it. Declare several collections only when the parent sets are small, or load the second collection with a separate query (the [`LoadWith`](eager-loading.md) loader is the alternative — split-query by default, single-query with `EagerLoadMode.SingleQuery`).

`JoinInto` **cannot be combined with any other join on the same builder**: once the builder carries a `JoinInto`, adding `Join`, `LeftJoin`, `CrossJoin`, `SemiJoin`, `AntiJoin` (or any other explicit join) throws `NotSupportedException`, and vice versa. Declare the `JoinInto` collections on a plain entity source, so the non-list terminals know exactly which joins to drop. Mixing the two would leave the parent-only command unable to tell an explicit join from a `JoinInto`.

## Join modifiers

The join modifiers of the underlying join are available. On ClickHouse, [`JoinOptions.WithStrictness`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.WithStrictness(NextORM.Core.JoinOptions,NextORM.Core.JoinStrictness)) may be passed to `JoinInto(...)` through its trailing lambda: `j => j.WithStrictness(Any)` keeps only the **first** matching child per parent, so the loaded collection is truncated to at most one element; the other strictness modifiers (`All`, `Asof`) and [`JoinOptions.Global`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.Global(NextORM.Core.JoinOptions)) pass through unchanged. An option passed to a `JoinInto` preserves the parent-only terminal behaviour — the non-list terminals still exclude the join.

## Terminal boundary

The join is part of the command and is stitched **only** by the stitching terminals:

- [`ToList()`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) and [`ToListAsync()`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])).
- `ToArray()` and `ToArrayAsync()`.

[`ToCommand()`](xref:NextORM.Core.EntityBuilder`1.ToCommand) keeps the join but returns only the **denormalized** `(parent, child)` rows, so a bare enumeration may repeat a parent once per matching child. Every other terminal built on the parent command — `First`, `Count`, `Any`, `ToHashSet`, `ToEnumerable`, `ToAsyncEnumerable`, [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) — excludes the join and returns the **parent only**, with the collections not filled. A `Select` projection or an `As` derived source is rejected with [`NotSupportedException`](xref:System.NotSupportedException): `Select` would keep the join without stitching (repeating parents, never grouping children) and `As` would drop the stitching metadata. Materialize with `ToList`/`ToListAsync` when the collections must be filled.

## In-memory parity

`JoinInto` works on the in-memory provider: the join and the grouping run as delegates, and the LEFT/INNER semantics, parent deduplication, ordering and empty collections match the SQL providers. The in-memory provider pages the deduplicated parents instead of wrapping a parent subquery.

## `NotSupportedException` boundaries

The following are rejected with [`NotSupportedException`](xref:System.NotSupportedException):

- an **undeclared** relationship, when the collection overload is used without relationship metadata — declare it with `HasMany`/`HasOne`/`HasManyThrough`/`[Relationship]` or use the explicit-key overload; the reference overload likewise requires a declared one-to-one (`HasOneToOne`) or a local `JoinOptions.OneToOne` configuration;
- `JoinInto` applied to a derived (`As`) or joined projection source;
- a `Select` projection or an `As` derived source applied to a builder carrying a `JoinInto` — both drop the stitching contract (repeated parents or lost metadata), so materialize with `ToList`/`ToListAsync` instead;
- `JoinInto` combined with another join on the same builder (adding any explicit join once a `JoinInto` is declared throws, and adding a `JoinInto` once an explicit join exists throws) — declare `JoinInto` on a plain entity source only;
- a join kind other than `Inner`/`Left`;
- explicit-key selectors that select **different property types**, or that are not a simple property/field access (a computed key has no stable plan identity);
- a read-only collection member that is `null` at runtime;
- more than seven child collections on one query (the projection arity cap).

Key **arity** is not one of these boundaries: a many-to-many relationship under [`EagerLoadMode.SingleQuery`](eager-loading.md) and a composite principal, foreign or junction key on the relationship are supported on the explicit `JoinInto` path, which joins and groups on every declared component (see [Many-to-many through a junction](#many-to-many-through-a-junction)). The component lists come from the relationship metadata: declare them with the composite fluent overloads (A–J) or with `ForeignKeys`/`PrincipalKeys`, and `JoinInto` consumes every component in order.

The relationship metadata itself fails closed too: an FK property that is not mapped on the dependent type, a principal type with no key, an empty or duplicated component list, a mismatched arity between the two sides (including a single-column foreign key against a composite default principal key), a null component, a component without a CLR member, or an FK/principal-key type mismatch (compared after unwrapping nullable) throws `NotSupportedException` when the keys are resolved — before any SQL is generated and before a plan is inserted into the plan cache. The same structural validation applies to manually supplied [`IRelationshipMetadata`](xref:NextORM.Core.IRelationshipMetadata) (see [Validation timing](#validation-timing)).

## Relationship metadata

The metadata model is exposed through [`IEntityMetadata.Relationships`](xref:NextORM.Core.IEntityMetadata); each entry is an [`IRelationshipMetadata`](xref:NextORM.Core.IRelationshipMetadata) with [`Kind`](xref:NextORM.Core.IRelationshipMetadata) ([`RelationshipKind`](xref:NextORM.Core.RelationshipKind)), `DeclaringType`, `RelatedType`, `Navigation`, `IsCollection`, `ForeignKey`, `PrincipalKey` and `Inverse`. `ForeignKey` and `PrincipalKey` are ordered `IReadOnlyList<IPropertyMetadata>` component lists — the order is the declaration order (or, for a default principal key, entity-metadata order), and the component at position `i` on one side pairs with the component at position `i` on the other. `Inverse` points at the other side's metadata when both sides are declared and agree on the **full ordered** foreign key and types, and is `null` otherwise — it is never inferred. `Inverse` is resolved **lazily on its first read and cached**: register the related type (and the declaring type) before reading it, because a read taken before the other side is registered caches `null` and will not be refreshed by a later registration. Two different navigations that expose the same canonical mapping make the inverse ambiguous, and reading it throws `NotSupportedException` naming both candidates.

## See also

- [Eager loading child collections (`LoadWith`)](eager-loading.md) — the alternative loader (split-query by default, single-query with `EagerLoadMode.SingleQuery`).
- [Joins](../guide/04-joins.md) — the explicit join surface.
- [Entities and metadata](../getting-started/03-entities-and-metadata.md) — key and column mapping.

---

Source: `tests/nextorm.sqlite.tests/Joins/JoinIntoSqlGenerationTests.cs:23`,
`tests/nextorm.sqlite.tests/Joins/JoinIntoExecutionTests.cs:43`,
`tests/nextorm.core.tests/JoinInto/JoinIntoInMemoryTests.cs:31`,
`tests/nextorm.core.tests/Metadata/RelationshipMetadataTests.cs:173`.
