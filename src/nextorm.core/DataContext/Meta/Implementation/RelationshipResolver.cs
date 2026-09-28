using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Resolves the participants of a declared relationship from their entity metadata. Resolution is
/// deferred until the keys are read, so both sides can be declared in either order; a configured
/// mapping always wins over the auto-built fallback.
/// </summary>
internal static class RelationshipResolver
{
    /// <summary>Finds the mapped property of <paramref name="entityType"/> by its CLR member.</summary>
    /// <exception cref="NotSupportedException">The property is not mapped on the entity.</exception>
    internal static IPropertyMetadata ResolveProperty(Type entityType, PropertyInfo property, Type declaringType, PropertyInfo? navigation)
    {
        var metadata = ResolveEntityMetadata(entityType);
        var properties = metadata.Properties;
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].PropertyInfo == property)
                return properties[i];
        }

        throw new NotSupportedException(
            $"The relationship '{Describe(declaringType, navigation)}' refers to the foreign key '{property.Name}' which is not mapped on {entityType.Name}.");
    }

    /// <summary>Finds the mapped property of <paramref name="entityType"/> selected as an explicit principal key.</summary>
    /// <exception cref="NotSupportedException">The property is not mapped on the entity.</exception>
    internal static IPropertyMetadata ResolvePrincipalProperty(Type entityType, PropertyInfo property, Type declaringType, PropertyInfo? navigation)
    {
        var metadata = ResolveEntityMetadata(entityType);
        var properties = metadata.Properties;
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].PropertyInfo == property)
                return properties[i];
        }

        throw new NotSupportedException(
            $"The relationship '{Describe(declaringType, navigation)}' refers to the principal key '{property.Name}' which is not mapped on {entityType.Name}.");
    }

    /// <summary>Finds the key property (<see cref="IPropertyMetadata.IsKey"/>) of <paramref name="entityType"/>.</summary>
    /// <exception cref="NotSupportedException">The entity declares no key.</exception>
    internal static IPropertyMetadata ResolveKey(Type entityType, Type declaringType, PropertyInfo? navigation)
    {
        var metadata = ResolveEntityMetadata(entityType);
        var properties = metadata.Properties;
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].IsKey)
                return properties[i];
        }

        throw new NotSupportedException(
            $"The relationship '{Describe(declaringType, navigation)}' has no principal key: {entityType.Name} declares no property flagged with [Key], .Key(), or the Id/<TypeName>Id convention.");
    }

    /// <summary>
    /// Finds the symmetric relationship declared on the related type, or <see langword="null"/> when the
    /// other side is not registered or does not converge on the same foreign key. Only configured
    /// metadata participates: a type registered through <c>From&lt;T&gt;</c> has relationships, an
    /// auto-built type does not.
    /// </summary>
    internal static IRelationshipMetadata? FindInverse(RelationshipMetadata relationship)
    {
        if (!DataContextCache.Metadata.TryGetValue(relationship.RelatedType, out var related) || string.IsNullOrEmpty(related.TableName))
            return null;

        var relationships = related.Relationships;
        for (var i = 0; i < relationships.Count; i++)
        {
            if (relationships[i] is RelationshipMetadata candidate
                && !ReferenceEquals(candidate, relationship)
                && candidate.DeclaringType == relationship.RelatedType
                && candidate.RelatedType == relationship.DeclaringType
                && candidate.ForeignKeyProperty == relationship.ForeignKeyProperty)
                return candidate;
        }

        return null;
    }

    private static IEntityMetadata ResolveEntityMetadata(Type entityType)
        => DataContextExtensions.ResolveMetadata(entityType);

    private static string Describe(Type declaringType, PropertyInfo? navigation)
        => navigation is null ? declaringType.Name : $"{declaringType.Name}.{navigation.Name}";
}
