# Inheritance and derived entities

> nextorm does not own the inheritance strategy. The engine maps one CLR type to one table and carries no
> discriminator, so the relational shape of a hierarchy is a modelling decision on your side — exactly as
> it would be in hand-written SQL. The engine supplies the pieces: inherited writable properties, joins,
> projections, `UNION ALL`, JSON and dynamic columns. This page shows the four mapping shapes, how to
> read across them, and what remains your responsibility.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Joins](04-joins.md) · [Set operations](08-set-operations.md) · [JSON support](16-json.md) · [Dynamic columns](29-dynamic-columns.md)

## Where the boundary is

The mapping unit is a **CLR type**. When metadata is built for `T`, its public writable properties are
mapped, including the ones inherited from a base class (`GetProperties(FlattenHierarchy | Public | Instance)`,
see `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs`). That gives you ordinary C# inheritance
for free: a base class can hold shared columns, and each concrete type maps to its own table.

What the engine does **not** do is interpret the hierarchy:

* it has no discriminator column and no base/derived registration;
* it never switches the materialised CLR type at runtime;
* it never unions the tables of concrete types for a base-typed query;
* it does not create tables, views, foreign keys or check constraints.

Those are the parts you choose and own. The rest of this page is the catalogue of shapes that work with
that boundary.

## Choosing a shape

| If you need | Shape | What nextorm provides |
|---|---|---|
| One table, a few optional subtype columns, filtering by kind | Discriminator column + a flat entity or one entity per subtype (manual TPH) | Ordinary columns and `Where` |
| Each subtype has its own table, and queries always target a known type | Base class + one table per concrete type (manual TPC) | Inherited writable properties map into each concrete type |
| Shared columns plus a subtype-only table, with per-subtype referential integrity | Base table + subtype table (manual TPT) | `Join`/`LeftJoin`, `HasOneToOne`/`JoinInto`, transactions |
| Open-ended or rarely used subtype fields | JSON column or `[DynamicColumns]` store | `[JsonColumn]`, `[DynamicColumns]` |
| One mixed list across several tables | DTO projection over `UNION ALL`, a database view, or per-type queries | `Select`, `UnionAll`, `[SqlTable]` on a view |

## One table with a discriminator (TPH by hand)

This is the physical shape TPH uses in an ORM, without the ORM's runtime type switch: one table, a
discriminator column, and nullable columns for the fields that only some subtypes use.

### One flat entity

```csharp
[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Kind { get; set; } = "";       // "standard" / "express"
    public string Number { get; set; } = "";
    public decimal? Total { get; set; }          // standard orders
    public DateTime? PromisedAt { get; set; }    // express orders
}
```

```csharp
var express = ctx.From<Order>()
    .Where(o => o.Kind == "express" && o.PromisedAt < cutoff)
    .ToList();
```

* `Kind` is an ordinary mapped column. Write it yourself on insert/update; a `CHECK` constraint or a
  trigger is your schema choice, not something nextorm emits.
* Subtype-only columns must be nullable (or carry a sentinel), because every row shares the same table.
* In this variant the entity is always `Order`; there is no `ExpressOrder` instance to materialise.
* This is the cheapest shape when there are few subtype fields and most queries are
  discriminator-filtered.

### One entity per subtype, same table

A derived entity can map the same table and expose only its subtype's columns; the engine does not
forbid two entity types sharing a table. It is still not ORM TPH: there is no discriminator metadata, so
the filter is yours at every query.

```csharp
[SqlTable("orders")]
public abstract class OrderBase
{
    [Key] public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Number { get; set; } = "";
}

// Both inherit [SqlTable("orders")].
public sealed class StandardOrder : OrderBase
{
    public decimal Total { get; set; }
}

public sealed class ExpressOrder : OrderBase
{
    public DateTime PromisedAt { get; set; }
}
```

```csharp
var express = ctx.From<ExpressOrder>()
    .Where(o => o.Kind == "express" && o.PromisedAt < cutoff)
    .ToList();
```

* `From<ExpressOrder>()` reads **all** rows of `orders`; nextorm does not add `Kind = "express"`. The
  `Where` is mandatory. If a standard row reaches the materializer, its `PromisedAt` is `NULL` and a
  non-nullable `DateTime` throws (`InvalidOperationException: The data is NULL ...`).
* The shared table must keep `PromisedAt` and `Total` nullable (or defaulted), because each row uses
  only one of them. A `CHECK` constraint can enforce
  `(Kind = 'express' AND PromisedAt IS NOT NULL) OR (Kind = 'standard' AND Total IS NOT NULL)`.
* `ExpressOrder` maps common columns plus `PromisedAt`; it does not map `Total`, and `StandardOrder`
  does not map `PromisedAt`. That is the point of the split. Writes are per subtype:
  `CreateInsertBuilder<ExpressOrder>()` writes the common columns and `PromisedAt`, and you set `Kind`
  yourself; the other subtype's column must be nullable/defaulted.
* For safety, put a global query filter on `ExpressOrder` so the discriminator predicate is appended
  automatically ([Global query filters](../advanced/query-filters.md)); a DB `CHECK` still owns the
  integrity guarantee, and raw SQL bypasses the filter.
* `[SqlTable]` on the base is inherited, which is what makes all subtypes map to `orders`; in the TPC
  shape below the same inheritance is why each concrete type must name its own table.
* A discriminator as a CLR enum works only where the provider maps enums on the read path; otherwise
  store `int`/`string`, or map the enum to text with `EnumToStringConverter<TEnum>`
  ([Value converters](../infrastructure/03-value-converters.md)).

## One table per concrete type (TPC by hand)

Put shared columns on a base class, then map each concrete type to its own table. This is the closest
shape to TPC: every concrete table contains the full set of columns, including the inherited ones.

```csharp
public abstract class OrderBase
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

[SqlTable("order_standard")]
public sealed class StandardOrder : OrderBase
{
    public decimal Total { get; set; }
}

[SqlTable("order_express")]
public sealed class ExpressOrder : OrderBase
{
    public decimal Total { get; set; }
    public DateTime PromisedAt { get; set; }
}
```

```csharp
var express = ctx.From<ExpressOrder>()
    .Where(o => o.PromisedAt < cutoff)
    .ToList();

ctx.CreateInsertBuilder<ExpressOrder>()
    .Values(new ExpressOrder
    {
        Id = 10,
        Number = "E-10",
        CreatedAt = DateTime.UtcNow,
        Total = 42m,
        PromisedAt = promised,
    })
    .Insert();
```

Rules and caveats:

* `OrderBase` is a C# convenience, **not an entity**. It is not queried and has no table of its own.
* Name every concrete table explicitly. `[SqlTable]`/`[Table]` is inherited from the base, so a base
  attribute would otherwise send every derived type to the same table.
* `[Key]`, `[Column]`, `[DatabaseGenerated]` and the other mapping attributes declared on base
  properties apply to the concrete type. Do not hide a mapped property with `new`: the derived member
  wins and the attributes on the hidden base member are not applied. Prefer leaving the property alone,
  or use `virtual`/`override` if you really need to change its behavior.
* Only public **writable** properties are columns. A computed getter on the base is ordinary C# and is
  not mapped; declare columns as properties with a setter.
* Reading is per type. There is no `OfType<T>()` and no base-typed query that returns a mix of
  `StandardOrder` and `ExpressOrder` — see [Reading a mixed list](#reading-a-mixed-list).
* Schema evolution is manual: adding a base column means altering every concrete table.

## A shared base table (TPT by hand)

If the shared columns are large, or per-subtype foreign keys and constraints matter, keep them in a
base table and put only the subtype-specific columns in a side table.

```csharp
[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

[SqlTable("order_express")]
public sealed class OrderExpress
{
    [Key] public long OrderId { get; set; }  // same value as orders.Id
    public DateTime PromisedAt { get; set; }
}
```

Read with a join:

```csharp
var rows = ctx.From<Order>()
    .Join<OrderExpress>((o, e) => o.Id == e.OrderId)
    .Select(p => new
    {
        p.Item1.Id,
        p.Item1.Number,
        p.Item2.PromisedAt,
    })
    .ToList();
```

Or declare a one-to-one relationship with `HasOneToOne` on an entity that exposes the detail as a
navigation property, and let `JoinInto` fill it in one round trip
([Relationships](../relationships/relationships.md#one-to-one-reference)).

Write both rows in one transaction — nextorm has no unit of work, so the two commands are explicit:

```csharp
var transactions = (ITransactionManager)ctx;
await using var tx = await transactions.BeginTransactionAsync();

ctx.CreateInsertBuilder<Order>().Values(order).Insert();
ctx.CreateInsertBuilder<OrderExpress>().Values(details).Insert();

await tx.CommitAsync();
```

The primary key/foreign key relationship and its uniqueness are part of your schema. nextorm does not
create or validate constraints; the one-to-one declaration is a load-time contract, not a database
constraint (see [One-to-one reference](../relationships/relationships.md#one-to-one-reference)).

## A side payload: JSON or dynamic columns

When subtype fields are open-ended, sparse, or owned by an extension, keeping them out of the relational
shape is often better than adding nullable columns.

**JSON column.** Store a payload object in one column and treat it as opaque in portable code:

```csharp
public sealed record ExpressPayload(DateTime PromisedAt, string? Courier);

[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public string Kind { get; set; } = "";

    [JsonColumn]
    public ExpressPayload? Express { get; set; }
}
```

A `[JsonColumn]` property holds any serializable CLR object; the provider type and the storage mode are
described in [JSON support across providers](16-json.md#map-a-clr-object-to-a-native-json-column).
Filtering inside the payload is provider-specific (native JSON functions or JSON-as-text functions), so
it is not a portable query surface.

**Dynamic columns.** Keep a dictionary of unmapped physical columns on the entity:

```csharp
[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public string Kind { get; set; } = "";

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
```

The read side receives every unmapped column when the entity is queried as a whole, and the write side
persists the dictionary keys as physical columns on `INSERT`, `UPDATE` and `MERGE`
([Dynamic columns](29-dynamic-columns.md)). The store is untyped and there is no query surface over its
keys: use it for storage and projection, not for SQL predicates.

## Reading a mixed list

There is no automatic polymorphic read: a base-typed query does not return runtime subtypes. Choose one
of the following.

**Per-type queries, merged in memory.** The simplest option when the list is small or the types are
usually queried separately:

```csharp
var standard = ctx.From<StandardOrder>().ToList();
var express = ctx.From<ExpressOrder>().ToList();

var all = standard.Cast<OrderBase>().Concat(express).ToList();
```

**One `UNION ALL` over a common DTO.** This is one round trip and keeps a discriminator in the result:

```csharp
public sealed class OrderRow
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Number { get; set; } = "";
    public DateTime? PromisedAt { get; set; }
}

var rows = ctx.From<StandardOrder>()
    .Select(o => new OrderRow
    {
        Id = o.Id,
        Kind = "standard",
        Number = o.Number,
    })
    .UnionAll(ctx.From<ExpressOrder>()
        .Select(o => new OrderRow
        {
            Id = o.Id,
            Kind = "express",
            Number = o.Number,
            PromisedAt = o.PromisedAt,
        }))
    .ToList();
```

Both projections must have the same shape; `UnionAll` renders `UNION ALL` on every provider
([Set operations](08-set-operations.md)). The result is the DTO, not the concrete entity, so map it to
your domain model in memory if the distinction matters.

**A database view.** If the mixed list is used in many places, create a view that unions the concrete
tables and map the view as a flat entity:

```sql
create view v_orders as
select Id, 'standard' as Kind, Number, null as PromisedAt from order_standard
union all
select Id, 'express'  as Kind, Number, PromisedAt      from order_express;
```

```csharp
[SqlTable("v_orders")]
public sealed class OrderView
{
    [Key] public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Number { get; set; } = "";
    public DateTime? PromisedAt { get; set; }
}

var all = ctx.From<OrderView>().ToList();
```

The view, its `UNION ALL` and its discriminator are schema you own. Writes still go to the concrete
tables, not to the view.

**TPT.** For the shared-base-table shape, a join plus a DTO projection (shown above) is the mixed read;
add a constant discriminator column to the projection when the consumer needs to know which subtype the
row came from.

## Shared code and query filters

A base class is the natural place for shared C# behavior (methods, calculated properties, constants).
Remember that only writable public properties become columns, so a calculated property is not part of
the mapping.

Global query filters declared on a base class are inherited by the mapped derived types. The resolver
applies attributes from the base type down to the most derived one, so a same-key filter on the derived
type deterministically overrides the base one ([Global query filters](../advanced/query-filters.md)). A filter is
still metadata of each concrete entity type; it is not a hierarchy-wide switch.

## EF Core integration

The EF Core bridge has a stricter rule than the core: importing an EF model rejects any entity type with
a base type or with derived types, because the nextorm metadata it produces has no discriminator
([EF Core integration](../advanced/integration-efcore.md#limitations)). If the EF model contains a hierarchy, either
flatten it for the import or declare the nextorm mapping by hand using the shapes above. Manual mapping
is unaffected: a concrete derived class is an ordinary entity.

## What you own and what the engine gives you

| You own | The engine gives you |
|---|---|
| The shape: discriminator, table per concrete type, base table, JSON/dynamic payload | Per-type mapping that includes inherited writable properties |
| Schema: tables, views, keys, foreign keys, `CHECK` constraints, migrations | `[Key]`, `[Column]`, `[DatabaseGenerated]`, fluent mapping |
| The discriminator value on writes and the dispatch on reads | `Join`/`LeftJoin`, `HasOneToOne`/`JoinInto`, transactions |
| Mixed-list composition (DTO, view or per-type queries) | `Select`, `UnionAll`, `[SqlTable]` on a view |
| Sparse/open-ended fields | `[JsonColumn]`, `[DynamicColumns]` |
| Query-filter policy per concrete type | Query-filter inheritance from the base type |

The result is not a hidden ORM hierarchy: it is a flat mapping plus the relational shape you chose. That
keeps the generated SQL predictable and the schema explicit, at the cost of writing the dispatch and the
schema yourself.
