using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Process-wide registry of entity mappings described at compile time by the source generator.
/// </summary>
/// <remarks>
/// <para>
/// The registry holds a <em>factory</em>, not a built mapping: <see cref="DataContextCache.Clear()"/>
/// drops the resolved metadata and the factory simply runs again, so invalidating the caches never
/// removes generated coverage. The registry is populated by module initializers in the assembly that
/// declares the entity types, which is why it survives a cache clear while the metadata itself does not.
/// </para>
/// <para>
/// A registered type is resolved without reflecting over its members. A type the generator did not
/// cover is absent from the registry and keeps the reflection path, so the two mechanisms coexist and
/// the registry is never a precondition for a query to work.
/// </para>
/// </remarks>
public static class GeneratedEntityMetadata
{
    private static readonly ConcurrentDictionary<Type, Func<IEntityMetadata>> Factories = new();

    /// <summary>
    /// Registers the compile-time description of <paramref name="entityType"/>. Called from generated
    /// module initializers; the first registration for a type wins, so a second generated provider for
    /// the same type cannot silently replace the first.
    /// </summary>
    /// <param name="entityType">The entity type the description maps.</param>
    /// <param name="factory">Builds the mapping; run once per cache generation, not once per query.</param>
    /// <returns><see langword="true"/> when this call registered the type; <see langword="false"/> when a description was already present.</returns>
    public static bool Register(Type entityType, Func<IEntityMetadata> factory)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(factory);

        return Factories.TryAdd(entityType, factory);
    }

    internal static bool TryGet(Type entityType, out Func<IEntityMetadata> factory)
        => Factories.TryGetValue(entityType, out factory!);
}

/// <summary>
/// A compile-time description of an entity mapping: the data the reflective auto-build would derive from
/// the entity's attributes, emitted as ordinary code instead.
/// </summary>
/// <remarks>
/// Only what the auto-build derives from attributes belongs here. Anything the application declares at
/// run time (a fluent <c>From&lt;T&gt;(cfg =&gt; ...)</c> mapping, an EF Core model, a per-scope variant)
/// still wins: it is published into <see cref="DataContextCache"/> first and this description is only
/// consulted when no such entry exists.
/// </remarks>
public sealed class EntityMetadataDescriptor
{
    /// <summary>The mapped table name, or <see langword="null"/> for an unmapped type.</summary>
    public string? TableName { get; init; }

    /// <summary>The mapped schema/owner, or <see langword="null"/> for an unqualified table.</summary>
    public string? Schema { get; init; }

    /// <summary>Whether <see cref="TableName"/> is derived from the CLR type name (naming conventions apply only to auto names).</summary>
    public bool IsTableNameAuto { get; init; }

    /// <summary>The CLR type name, used by the <c>Id</c>/<c>&lt;TypeName&gt;Id</c> key convention when no property declares a key.</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>The mapped properties, in declaration order.</summary>
    public IReadOnlyList<EntityPropertyDescriptor> Properties { get; init; } = Array.Empty<EntityPropertyDescriptor>();

    /// <summary>
    /// The global query-filter declarations of the entity, in declaration order. Keys are resolved when
    /// the mapping is built (a repeated key replaces the earlier filter, an anonymous declaration is
    /// additive), so the declarations stay exactly as written.
    /// </summary>
    public IReadOnlyList<EntityFilterDescriptor> Filters { get; init; } = Array.Empty<EntityFilterDescriptor>();

    /// <summary>
    /// Builds the mapping. The declaration order is kept as-is: the generator emits the properties in the
    /// order the compiler emits them, which is the order a reflective enumeration returns, so a generated
    /// mapping and the reflection fallback produce the same column order (and therefore the same SQL).
    /// </summary>
    /// <returns>The entity mapping.</returns>
    public IEntityMetadata Build()
    {
        var properties = new List<IPropertyMetadata>(Properties.Count);
        for (var i = 0; i < Properties.Count; i++)
        {
            var property = Properties[i];

            // Same fail-fast rule the reflective auto-build applies: a declared precision/scale pair is
            // valid only on a decimal column, and only as a complete pair.
            DecimalPrecisionRules.Validate(property.Property.PropertyType, property.Property.Name, property.DecimalPrecision, property.DecimalScale);

            properties.Add(new PropertyMetadata
            {
                PropertyInfo = property.Property,
                ColumnName = property.ColumnName,
                IsColumnNameAuto = property.IsColumnNameAuto,
                IsKey = property.IsKey,
                IsIdentity = property.IsIdentity,
                IsComputed = property.IsComputed,
                DurationUnit = property.DurationUnit,
                DurationPrecision = property.DurationPrecision,
                DecimalPrecision = property.DecimalPrecision,
                DecimalScale = property.DecimalScale,
                Collation = property.Collation,
                RangeColumns = property.RangeColumns,
            });
        }

        InferKeyIfMissing(properties, TypeName);

        return new EntityMetadata(TableName, properties, IsTableNameAuto, dynamicColumnsStore: null, filters: BuildFilters(), relationships: null, schema: Schema);
    }

    // The key rules of the reflective build: an anonymous declaration is additive, a named one replaces
    // an earlier declaration with the same key, and a named declaration without a lambda or function
    // removes the slot.
    private IReadOnlyList<IQueryFilterMetadata> BuildFilters()
    {
        var filters = new List<IQueryFilterMetadata>(Filters.Count);
        for (var i = 0; i < Filters.Count; i++)
        {
            var declared = Filters[i];
            var key = declared.Key;

            if (string.IsNullOrWhiteSpace(key))
            {
                if (declared.Lambda is not null || declared.Func is not null)
                    filters.Add(new QueryFilterMetadata(QueryFilters.AnonymousKey, declared.Lambda, declared.Func));
                continue;
            }

            var index = filters.FindIndex(filter => string.Equals(filter.Key, key, StringComparison.Ordinal));
            if (declared.Lambda is null && declared.Func is null)
            {
                if (index >= 0)
                    filters.RemoveAt(index);
                continue;
            }

            var metadata = new QueryFilterMetadata(key, declared.Lambda, declared.Func);
            if (index >= 0)
                filters[index] = metadata;
            else
                filters.Add(metadata);
        }

        return filters;
    }

    // The convention the reflective auto-build applies: with no declared key, a property named "Id"
    // wins, then "<TypeName>Id"; both comparisons are case-insensitive.
    private static void InferKeyIfMissing(List<IPropertyMetadata> properties, string typeName)
    {
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].IsKey)
                return;
        }

        var conventionalName = typeName + "Id";
        foreach (var candidate in new[] { "Id", conventionalName })
        {
            for (var i = 0; i < properties.Count; i++)
            {
                if (string.Equals(properties[i].PropertyInfo.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    ((PropertyMetadata)properties[i]).IsKey = true;
                    return;
                }
            }
        }
    }
}

/// <summary>
/// One global query-filter declaration in a compile-time <see cref="EntityMetadataDescriptor"/>: the same
/// three declarations the <c>[QueryFilter]</c> attribute carries, with the static member already
/// resolved to its value by generated code instead of a reflective member lookup.
/// </summary>
public sealed class EntityFilterDescriptor
{
    /// <summary>The filter key, or <see langword="null"/>/empty for an anonymous filter.</summary>
    public string? Key { get; init; }

    /// <summary>The filter predicate, or <see langword="null"/> when the declaration carries a function instead.</summary>
    public LambdaExpression? Lambda { get; init; }

    /// <summary>The builder-function form of the filter, or <see langword="null"/> when the declaration carries a predicate instead.</summary>
    public Delegate? Func { get; init; }
}

/// <summary>
/// One mapped property in a compile-time <see cref="EntityMetadataDescriptor"/>.
/// </summary>
public sealed class EntityPropertyDescriptor
{
    /// <summary>The CLR property this mapping describes.</summary>
    public required PropertyInfo Property { get; init; }

    /// <summary>The column the property maps to.</summary>
    public required string ColumnName { get; init; }

    /// <summary>Whether <see cref="ColumnName"/> is derived from the property name (naming conventions apply only to auto names).</summary>
    public bool IsColumnNameAuto { get; init; }

    /// <summary>Whether the property is (part of) the entity key.</summary>
    public bool IsKey { get; init; }

    /// <summary>Whether the database generates the value (an identity column excluded from inserts).</summary>
    public bool IsIdentity { get; init; }

    /// <summary>Whether the database generates the value and it can never be written (a computed column).</summary>
    public bool IsComputed { get; init; }

    /// <summary>The declared decimal precision, or <see langword="null"/> for the provider default.</summary>
    public int? DecimalPrecision { get; init; }

    /// <summary>The declared decimal scale, or <see langword="null"/> for the provider default.</summary>
    public int? DecimalScale { get; init; }

    /// <summary>The declared provider-native collation, or <see langword="null"/> for the database default.</summary>
    public string? Collation { get; init; }

    /// <summary>The storage unit of a <see cref="System.TimeSpan"/> property, or <see langword="null"/> for the provider's native duration type.</summary>
    public DurationUnit? DurationUnit { get; init; }

    /// <summary>The fractional-second precision of a native duration type; zero for the provider default.</summary>
    public int DurationPrecision { get; init; }

    /// <summary>The column pair of a <see cref="Range{T}"/> property stored as two scalar columns, or <see langword="null"/> for a single column.</summary>
    public RangeColumnsMetadata? RangeColumns { get; init; }
}
