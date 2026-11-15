# Runtime mapping scope

One entity class can address more than one physical place: a database per tenant, a `{tenant}_orders`
table next to `orders` on one server, a tenant-specific schema. A *mapping scope* is the runtime decision
behind that: a key that the mapping layer turns into a table name, a schema, column names and a
parameter prefix.

## Two contracts

| Contract | Answers |
| --- | --- |
| [`IEntityScopeResolver`](xref:NextORM.Core.IEntityScopeResolver) | Which scope applies to this entity, instance or set of key values |
| [`IEntityMetadataProvider`](xref:NextORM.Core.IEntityMetadataProvider) | What a scope maps to: entity metadata (table, schema, properties) and column names |

A scope is [`MappingScope`](xref:NextORM.Core.MappingScope) — a `readonly record struct` around a nullable
`string` key. `MappingScope.Default` (an empty key) is the ordinary, unscoped mapping: SQL text and
parameter names stay exactly what they were before scopes existed.

The default implementations are [`DefaultEntityScopeResolver`](xref:NextORM.Core.DefaultEntityScopeResolver)
(always returns `Default`) and [`DefaultEntityMetadataProvider`](xref:NextORM.Core.DefaultEntityMetadataProvider)
(the reflection mapping). Register replacements on the builder:

```csharp
var builder = new DataContextBuilder()
    .UseScopeResolver(new TenantScopeResolver())
    .UseEntityMetadataProvider(new TenantMetadataProvider());

using var context = new PostgresDataContext(connectionString, builder);
```

## Explicit scope on one command

`WithScope` is available on the query builders (including a joined builder) and on `MergeBuilder<T>`:

```csharp
var scope = new MappingScope("acme");

var orders = context.From<Order>()
    .WithScope(scope)
    .Where(order => order.Total > 100m)
    .ToList();

context.CreateMergeBuilder<Order>()
    .Using(order)
    .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
    .WithScope(scope)
    .Merge();
```

The scope applies to that command only; it never changes the context's default mapping. A scope on a
join, a subquery or a CTE belongs to that source, so one statement may render `acme_orders` for the outer
query and `beta_orders` for a `beta`-scoped subquery.

## Resolving the scope automatically

Most applications do not want to pass a scope to every call. `IEntityScopeResolver` is consulted by the
builders:

- `Resolve(Type)` — a read (`From<T>()`), an insert, a delete/update by predicate, a truncate;
- `Resolve(Type, object)` — an update, delete or merge whose target is an entity instance;
- `Resolve(Type, IReadOnlyList<object?>)` — explicit key values.

```csharp
public sealed class TenantScopeResolver : IEntityScopeResolver
{
    public MappingScope Resolve(Type entityType)
        => new(AmbientTenant.Current
            ?? throw new InvalidOperationException("A tenant must be set before querying tenant-mapped entities."));

    public MappingScope Resolve(Type entityType, object entity) => Resolve(entityType);

    public MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Resolve(entityType);
}
```

An explicit `WithScope(...)` on a query or merge builder wins over the resolver. Returning
`MappingScope.Default` keeps the ordinary mapping, which is what the default resolver does.

## Mapping a scope onto physical names

The provider reads the base mapping and derives the scoped one. Deriving from the default metadata keeps
every property mapping intact:

```csharp
public sealed class TenantMetadataProvider : IEntityMetadataProvider
{
    private readonly DefaultEntityMetadataProvider _inner = new();

    public IEntityMetadata GetMetadata(Type entityType, in MappingScope scope)
    {
        var metadata = _inner.GetMetadata(entityType, MappingScope.Default);
        if (scope == MappingScope.Default)
            return metadata;

        var tenant = scope.Key ?? throw new InvalidOperationException("A tenant scope needs a key.");
        return new TenantMetadata(metadata, tenant);
    }

    public string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention)
        => scope == MappingScope.Default
            ? _inner.GetColumnName(property, scope, convention)
            : $"{scope.Key}_{_inner.GetColumnName(property, MappingScope.Default, convention)}";
}

internal sealed class TenantMetadata(IEntityMetadata inner, string tenant) : IEntityMetadata
{
    public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
    public string? TableName => $"{inner.TableName}_{tenant}";
    public string? Schema => inner.Schema;
    public bool IsTableNameAuto => inner.IsTableNameAuto;
}
```

This mapping produces `orders_acme` and `total_acme` for the `acme` scope, and the untouched `orders` /
`total` for the default one.

Points worth keeping:

- **Always delegate the default scope.** A provider that rewrites `MappingScope.Default` changes the
  behaviour of every existing query.
- **Reuse the inner `Properties`** when the scope only changes the table or schema. A fresh
  `IPropertyMetadata` that exposes just `PropertyInfo` and `ColumnName` loses `IsKey`, `IsIdentity`,
  the value converter, range columns, duration and decimal precision — DML and materialization then break.
- **`GetColumnName` is the column seam.** For a non-default scope the renderer asks the provider for every
  column name (query projection, `WHERE`, `INSERT` column list, `UPDATE`/`DELETE`, `RETURNING`), and the
  result is materialized positionally, so a renamed column list stays consistent between SQL and rows.
- **The provider is trusted code.** The key is not escaped; quoting does not make an arbitrary key safe.
  Validate the key (for example `^[a-z0-9_]{1,32}$`) or map it to an allow-listed physical name.

## What a scope isolates

- **Metadata** is cached per `(entity type, scope)`, so two scopes never share a table, schema or column
  list. The default provider owns that cache; a custom provider keeps its own state and must isolate it the
  same way.
- **Plans** are keyed by the scope as well: the same query text is reused for the same scope and never
  reused across scopes, including through the value-free plan-reuse shortcuts.
- **Parameter names** get the scope prefix (`MappingScope.ParameterPrefix`, for example `acme_`), so two
  scopes in one batch cannot collide: `... where total > $acme_minimum`.
- **DML** renders the scoped target and columns: insert, update, delete and truncate all resolve their
  scope before the statement is built.
- **The connection is not part of a mapping scope.** A scope maps names on the context's own connection;
  reaching another server is a separate layer.

## Limitations

- The default provider builds the same mapping for every scope: names only change when a provider derives
  them from the key.
- `WithScope` exists on the query and merge builders. Insert, update, delete and truncate take the
  resolver's answer.
- One scope means one cached plan per query shape: many tenants times many shapes grows the plan cache,
  and each entry holds its own prepared statement.
- A scope renames objects; it does not add predicates. A table shared by several tenants still needs the
  tenant column in the query or in a [global query filter](../advanced/query-filters.md).
