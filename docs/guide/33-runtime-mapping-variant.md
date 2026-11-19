# Runtime mapping variant

Use a mapping variant when one entity class can address multiple physical locations and the application
chooses which one: a database per tenant, an `orders` table per tenant in one database
(`orders_acme`, `orders_beta`), or a schema per tenant. The table, schema, and even column names can
depend on who is asking.

The usual alternatives are worse. A generated class per tenant multiplies the model and the migrations;
setting the name on every call scatters one decision across the code and breaks as soon as a join mixes two
sources; a global rename cannot serve two tenants from one process. NextORM keeps one mapped class and
makes the naming layer a **runtime lookup**, so the same `Order` renders `orders_acme` for one caller and
`orders_beta` for another on the same connection.

That lookup is the *mapping variant*: a key that the mapping layer turns into a table name, a schema,
column names and a parameter prefix.

## Terms

| Term | Meaning |
| --- | --- |
| [`MappingVariant`](xref:NextORM.Core.MappingVariant) | A `readonly record struct` around a nullable `string` key. It is the value passed around, stored and compared. |
| Key | Whatever the application puts inside a variant. Here a tenant slug (`acme`), in [sharding](35-sharding-and-horizontal-partitioning.md) a bucket number. The library never interprets it. |
| Default variant | `MappingVariant.Default` — the empty key, i.e. the ordinary mapping: SQL text and parameter names stay exactly what they were before variants existed. |
| [`IEntityVariantResolver`](xref:NextORM.Core.IEntityVariantResolver) | Answers *which* variant applies to an entity type, to an instance, or to a set of key values. |
| [`IEntityMetadataProvider`](xref:NextORM.Core.IEntityMetadataProvider) | Answers *what* a variant maps to: entity metadata (table, schema, properties) and column names. |

## How it works

The engine asks two questions, one per seam:

1. **Which variant?** The builders consult `IEntityVariantResolver` — `Resolve(Type)` for a read, an
   insert, an update/delete by predicate or a truncate, `Resolve(Type, object)` for a command whose target
   is an entity instance, `Resolve(Type, IReadOnlyList<object?>)` for explicit key values. Returning
   `MappingVariant.Default` keeps the ordinary mapping, which is what
   [`DefaultEntityVariantResolver`](xref:NextORM.Core.DefaultEntityVariantResolver) does for everything.
2. **What does the variant map to?** `IEntityMetadataProvider.GetMetadata(entityType, variant)` returns the
   table and schema for that variant, and `GetColumnName(property, variant, convention)` the column name.
   [`DefaultEntityMetadataProvider`](xref:NextORM.Core.DefaultEntityMetadataProvider) is the reflection
   mapping and ignores the variant, so names only change once a provider derives them from the key.

Both defaults are installed unless replaced on the builder:

```csharp
var builder = new DataContextBuilder()
    .UseVariantResolver(resolver)               // IEntityVariantResolver
    .UseEntityMetadataProvider(provider);       // IEntityMetadataProvider
```

A command can also take the variant directly with `WithVariant(...)` on the query and merge builders; that
wins over the resolver for that command only and never changes the context's default mapping. `WithVariant`
is not available on insert, update, delete or truncate — those take the resolver's answer.

Everything derived from a variant is keyed by it, which is what makes two variants coexist in one context:
metadata is cached per `(entity type, variant)`, plans are cached per variant, generated parameter names get
the variant prefix (`MappingVariant.ParameterPrefix`), and DML renders the variant's target and columns.

## What a variant isolates

- **Metadata** is cached per `(entity type, variant)`, so two variants never share a table, schema or column
  list. The default provider owns that cache; a custom provider keeps its own state and must isolate it the
  same way.
- **Plans** are keyed by the variant as well: the same query text is reused for the same variant and never
  reused across variants, including through the value-free plan-reuse shortcuts.
- **Parameter names** get the variant prefix (`MappingVariant.ParameterPrefix`, for example `acme_`), so two
  variants in one batch cannot collide: `... where total > $acme_minimum`.
- **DML** renders the variant's target and columns: insert, update, delete and truncate all resolve their
  variant before the statement is built.
- **The connection is not part of a mapping variant.** A variant maps names on the context's own connection;
  reaching another server is a separate layer.

## A tenant variant

The rest of the page is one example: the entity `Order` is mapped to the table `orders` without a schema,
and the key is a tenant slug, so `acme` renders `orders_acme` / `total_acme`, `beta` renders `orders_beta` /
`total_beta`, and the default variant renders `orders` / `total`.

**1. Resolve the key.**

```csharp
public sealed class TenantVariantResolver : IEntityVariantResolver
{
    // The key is the tenant slug, "acme" here; the ambient tenant decides it for every call.
    public MappingVariant Resolve(Type entityType)
        => new(AmbientTenant.Current
            ?? throw new InvalidOperationException("A tenant must be set before querying tenant-mapped entities."));

    public MappingVariant Resolve(Type entityType, object entity) => Resolve(entityType);

    public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Resolve(entityType);
}
```

**2. Map the key onto physical names.**

```csharp
public sealed class TenantMetadataProvider : IEntityMetadataProvider
{
    private readonly DefaultEntityMetadataProvider _inner = new();

    public IEntityMetadata GetMetadata(Type entityType, in MappingVariant variant)
    {
        var metadata = _inner.GetMetadata(entityType, MappingVariant.Default);
        if (variant == MappingVariant.Default)
            return metadata;

        var tenant = variant.Key ?? throw new InvalidOperationException("A tenant variant needs a key.");
        return new TenantMetadata(metadata, tenant);
    }

    public string GetColumnName(PropertyInfo property, in MappingVariant variant, INamingConvention? convention)
        => variant == MappingVariant.Default
            ? _inner.GetColumnName(property, variant, convention)
            : $"{variant.Key}_{_inner.GetColumnName(property, MappingVariant.Default, convention)}";
}

// IEntityMetadata with the tenant suffix: table "orders" -> "orders_acme"; properties are reused as is.
internal sealed class TenantMetadata(IEntityMetadata inner, string tenant) : IEntityMetadata
{
    public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
    public string? TableName => $"{inner.TableName}_{tenant}";
    public string? Schema => inner.Schema;
    public bool IsTableNameAuto => inner.IsTableNameAuto;
}
```

**3. Wire both into the context.**

```csharp
var builder = new DataContextBuilder()
    .UseVariantResolver(new TenantVariantResolver())
    .UseEntityMetadataProvider(new TenantMetadataProvider());

using var context = new PostgresDataContext(connectionString, builder);   // the provider's connection string
```

From here `context.From<Order>()` reads `orders_acme` while `AmbientTenant.Current` is `"acme"`, and
`orders_beta` while it is `"beta"`.

Points worth keeping:

- **Always delegate the default variant.** A provider that rewrites `MappingVariant.Default` changes the
  behaviour of every existing query.
- **Reuse the inner `Properties`** when the variant only changes the table or schema. A fresh
  `IPropertyMetadata` that exposes just `PropertyInfo` and `ColumnName` loses `IsKey`, `IsIdentity`,
  the value converter, range columns, duration and decimal precision — DML and materialization then break.
- **`GetColumnName` is the column seam.** For a non-default variant the renderer asks the provider for every
  column name (query projection, `WHERE`, `INSERT` column list, `UPDATE`/`DELETE`, `RETURNING`), and the
  result is materialized positionally, so a renamed column list stays consistent between SQL and rows.
- **The provider is trusted code.** The key is not escaped; quoting does not make an arbitrary key safe.
  Validate the key (for example `^[a-z0-9_]{1,32}$`) or map it to an allow-listed physical name.

## An explicit variant on one command

The resolver is not the only source of the key: `WithVariant` takes the same key by hand, for one command:

```csharp
var variant = new MappingVariant("acme");      // the tenant slug — the same key as above

var orders = context.From<Order>()
    .WithVariant(variant)                      // reads orders_acme
    .Where(order => order.Total > 100m)
    .ToList();

var incoming = new Order { Id = 1, Tenant = "acme", Total = 250m };

context.CreateMergeBuilder<Order>()
    .Using(incoming)                           // writes orders_acme
    .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
    // Optional when the ambient tenant already selects "acme": the resolver returns the same variant.
    // `Tenant` is only written into the row; it does not select the table (see the note below).
    .WithVariant(variant)
    .Merge();
```

### Choose how to supply the variant

Use the resolver for the context's usual mapping. Use `WithVariant` when one query or merge must address a
known variant explicitly:

| Operation | Variant source |
|---|---|
| Query or merge with `WithVariant` | The explicit variant for that command takes precedence. |
| Insert, update, delete or truncate | The resolver's answer; these builders do not expose `WithVariant`. |
| Merge without `WithVariant` | The resolver resolves the entity type, not the source object's property values. |

The variant applies to that command only; it never changes the context's default mapping. A variant on a
join, a subquery or a CTE belongs to that source, so one statement may render `orders_acme` for the outer
query and `orders_beta` for a `beta`-scoped subquery.

The explicit call repeats the key the resolver would answer at builder creation, which is why it can be
dropped when the ambient tenant already selects the right table — and why it has to be passed when it does
not. Note that `incoming.Tenant` plays no part in that decision: the builder resolves the variant from the
entity **type** (`CreateMergeBuilder` calls `Resolve(typeof(TEntity))` once), never from the source values.
Only sharded entities route by instance, and a sharded merge still demands an explicit variant.

## Limitations

- The default provider builds the same mapping for every variant: names only change when a provider derives
  them from the key.
- `WithVariant` exists on the query and merge builders. Insert, update, delete and truncate take the
  resolver's answer.
- One variant means one cached plan per query shape: many tenants times many shapes grows the plan cache,
  and each entry holds its own prepared statement.
- A variant renames objects; it does not add predicates. A table shared by several tenants still needs the
  tenant column in the query or in a [global query filter](../advanced/query-filters.md).
