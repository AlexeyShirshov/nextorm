using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Resolves the participants of a declared relationship from their entity metadata and validates the
/// structural shape of the key components. Resolution is deferred until the keys are read, so both
/// sides can be declared in either order; a configured mapping always wins over the auto-built
/// fallback.
/// </summary>
internal static class RelationshipResolver
{
    /// <summary>Finds the mapped property of <paramref name="entityType"/> by its CLR member.</summary>
    /// <param name="entityType">The entity type that owns the mapping.</param>
    /// <param name="property">The selected CLR member.</param>
    /// <param name="declaringType">The relationship's declaring entity, used in the rejection.</param>
    /// <param name="navigation">The relationship's navigation, used in the rejection.</param>
    /// <param name="role">The component role (for example <c>foreign key</c>), used in the rejection.</param>
    /// <exception cref="NotSupportedException">The property is not mapped on the entity.</exception>
    internal static IPropertyMetadata ResolveProperty(Type entityType, PropertyInfo property, Type declaringType, PropertyInfo? navigation, string role = "foreign key")
    {
        var metadata = ResolveEntityMetadata(entityType);
        var properties = metadata.Properties;
        for (var i = 0; i < properties.Count; i++)
        {
            if (SameMember(properties[i].PropertyInfo, property))
                return properties[i];
        }

        throw new NotSupportedException(
            $"The relationship '{Describe(declaringType, navigation)}' refers to the {role} '{property.Name}' which is not mapped on {entityType.Name}.");
    }

    /// <summary>Resolves every property of an ordered component list to its mapped metadata.</summary>
    /// <exception cref="NotSupportedException">A component is not mapped on the entity.</exception>
    internal static IReadOnlyList<IPropertyMetadata> ResolveProperties(
        Type entityType,
        IReadOnlyList<PropertyInfo> properties,
        Type declaringType,
        PropertyInfo? navigation,
        string role)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var resolved = new IPropertyMetadata[properties.Count];
        for (var i = 0; i < properties.Count; i++)
            resolved[i] = ResolveProperty(entityType, properties[i], declaringType, navigation, role);

        return resolved;
    }

    /// <summary>
    /// Finds the full key of <paramref name="entityType"/> in entity-metadata property order. A composite
    /// principal key is returned in full; the first key is never selected implicitly.
    /// </summary>
    /// <exception cref="NotSupportedException">The entity declares no key.</exception>
    internal static IReadOnlyList<IPropertyMetadata> ResolveKeys(Type entityType, Type declaringType, PropertyInfo? navigation)
    {
        var metadata = ResolveEntityMetadata(entityType);
        var properties = metadata.Properties;
        var keys = new List<IPropertyMetadata>();
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].IsKey)
                keys.Add(properties[i]);
        }

        if (keys.Count == 0)
            throw new NotSupportedException(
                $"The relationship '{Describe(declaringType, navigation)}' has no principal key: {entityType.Name} declares no property flagged with [Key], .Key(), or the Id/<TypeName>Id convention.");

        return keys;
    }

    /// <summary>
    /// Validates a resolved single-leg key pair: both lists are non-empty, carry no duplicate components,
    /// have the same arity and pair components whose nullable-unwrapped mapped types match positionally.
    /// </summary>
    /// <exception cref="NotSupportedException">A structural rule is violated.</exception>
    internal static void ValidateResolvedKeys(
        string relationshipName,
        Type dependentType,
        IReadOnlyList<IPropertyMetadata> foreignKey,
        Type principalType,
        IReadOnlyList<IPropertyMetadata> principalKey)
    {
        ValidateComponentList(relationshipName, "foreign key", dependentType, foreignKey);
        ValidateComponentList(relationshipName, "principal key", principalType, principalKey);

        if (foreignKey.Count != principalKey.Count)
            throw new NotSupportedException(
                $"The relationship '{relationshipName}' declares {foreignKey.Count} foreign-key component(s) but {principalKey.Count} principal-key component(s); the arities must match.");

        for (var i = 0; i < foreignKey.Count; i++)
        {
            var foreignKeyType = Unwrap(foreignKey[i].PropertyInfo.PropertyType);
            var principalKeyType = Unwrap(principalKey[i].PropertyInfo.PropertyType);
            if (foreignKeyType != principalKeyType)
                throw new NotSupportedException(
                    $"The relationship '{relationshipName}' pairs foreign-key component {i + 1} of {foreignKey.Count} ('{foreignKey[i].PropertyInfo.Name}' of type {foreignKeyType}) with principal-key component {i + 1} of {principalKey.Count} ('{principalKey[i].PropertyInfo.Name}' of type {principalKeyType}); the foreign-key component does not match the principal key at the same position: the nullable-unwrapped types must match.");
        }
    }

    /// <summary>
    /// Validates the four resolved many-to-many lists: each list is non-empty and duplicate-free, each
    /// key leg has the same arity as its junction foreign key, and the components pair positionally by
    /// nullable-unwrapped mapped type.
    /// </summary>
    /// <exception cref="NotSupportedException">A structural rule is violated.</exception>
    internal static void ValidateResolvedJunction(
        string relationshipName,
        Type principalType,
        IReadOnlyList<IPropertyMetadata> parentKey,
        Type dependentType,
        IReadOnlyList<IPropertyMetadata> childKey,
        Type junctionType,
        IReadOnlyList<IPropertyMetadata> junctionParentForeignKey,
        IReadOnlyList<IPropertyMetadata> junctionChildForeignKey)
    {
        ValidateComponentList(relationshipName, "parent key", principalType, parentKey);
        ValidateComponentList(relationshipName, "child key", dependentType, childKey);
        ValidateComponentList(relationshipName, "junction parent foreign key", junctionType, junctionParentForeignKey);
        ValidateComponentList(relationshipName, "junction child foreign key", junctionType, junctionChildForeignKey);

        ValidateJunctionLeg(relationshipName, "parent", parentKey, junctionParentForeignKey);
        ValidateJunctionLeg(relationshipName, "child", childKey, junctionChildForeignKey);
    }

    /// <summary>
    /// Validates a manual <see cref="IRelationshipMetadata"/> instance with the same structural rules as
    /// a built-in declaration. Components are matched semantically to the configured mapping of the
    /// entity (mapped CLR member), never by object reference, so a custom <see cref="IPropertyMetadata"/>
    /// instance equivalent to a configured one is accepted while an unmapped member is not.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="relationship"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A structural rule is violated.</exception>
    internal static void ValidateRelationship(IRelationshipMetadata relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var relationshipName = Describe(relationship.DeclaringType, relationship.Navigation);

        if (relationship.Kind == RelationshipKind.ManyToMany)
        {
            if (relationship.Junction is not { } junction)
                throw new NotSupportedException(
                    $"The many-to-many relationship '{relationshipName}' declares no junction descriptor; junction metadata is required.");

            var parentKey = ResolveComponents(relationship.DeclaringType, junction.ParentKey, relationshipName, "parent key", relationship.DeclaringType, relationship.Navigation);
            var childKey = ResolveComponents(relationship.RelatedType, junction.ChildKey, relationshipName, "child key", relationship.DeclaringType, relationship.Navigation);
            var junctionParentForeignKey = ResolveComponents(junction.JunctionType, junction.JunctionParentForeignKey, relationshipName, "junction parent foreign key", relationship.DeclaringType, relationship.Navigation);
            var junctionChildForeignKey = ResolveComponents(junction.JunctionType, junction.JunctionChildForeignKey, relationshipName, "junction child foreign key", relationship.DeclaringType, relationship.Navigation);

            ValidateResolvedJunction(
                relationshipName,
                relationship.DeclaringType,
                parentKey,
                relationship.RelatedType,
                childKey,
                junction.JunctionType,
                junctionParentForeignKey,
                junctionChildForeignKey);
            return;
        }

        var (dependentType, principalType) = ResolveRoles(relationship, relationshipName);
        var foreignKey = ResolveComponents(dependentType, relationship.ForeignKey, relationshipName, "foreign key", relationship.DeclaringType, relationship.Navigation);
        var principalKey = ResolveComponents(principalType, relationship.PrincipalKey, relationshipName, "principal key", relationship.DeclaringType, relationship.Navigation);

        ValidateResolvedKeys(relationshipName, dependentType, foreignKey, principalType, principalKey);
    }

    /// <summary>
    /// Finds the symmetric relationship declared on the related type, or <see langword="null"/> when the
    /// other side is not registered or does not converge on the same canonical mapping. The match uses
    /// the full ordered semantic mapping (entity roles plus mapped members), so a shared first component
    /// or an unrelated mapping of the same shape does not create an inverse. Only configured metadata
    /// participates: a type registered through <c>From&lt;T&gt;</c> has relationships, an auto-built type
    /// does not.
    /// </summary>
    /// <exception cref="NotSupportedException">More than one distinct navigation declares the same canonical mapping.</exception>
    internal static IRelationshipMetadata? FindInverse(RelationshipMetadata relationship)
    {
        if (!DataContextCache.Metadata.TryGetValue(relationship.RelatedType, out var related) || string.IsNullOrEmpty(related.TableName))
            return null;

        var relationships = related.Relationships;
        var matches = new List<RelationshipMetadata>(2);
        for (var i = 0; i < relationships.Count; i++)
        {
            if (relationships[i] is not RelationshipMetadata candidate
                || ReferenceEquals(candidate, relationship)
                || candidate.DeclaringType != relationship.RelatedType
                || candidate.RelatedType != relationship.DeclaringType)
                continue;

            if (HasSameCanonicalMapping(relationship, candidate))
                matches.Add(candidate);
        }

        if (matches.Count == 0)
            return null;

        RelationshipMetadata? navigated = null;
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Navigation is null)
                continue;
            if (navigated is null)
            {
                navigated = matches[i];
                continue;
            }

            throw new NotSupportedException(
                $"The inverse of the relationship '{Describe(relationship.DeclaringType, relationship.Navigation)}' is ambiguous: the related type '{relationship.RelatedType.Name}' declares more than one navigation with the same canonical mapping ('{Describe(navigated.DeclaringType, navigated.Navigation)}' and '{Describe(matches[i].DeclaringType, matches[i].Navigation)}').");
        }

        return navigated ?? matches[0];
    }

    /// <summary>
    /// Validates that a single many-to-many key member and the junction foreign key that references it
    /// have the same (nullable-unwrapped) type. Kept for the single-column explicit-key lower.
    /// </summary>
    /// <exception cref="NotSupportedException">The two member types differ.</exception>
    internal static void ValidateJunctionKeyType(string entityName, string keyName, PropertyInfo key, string foreignKeyName, PropertyInfo foreignKey)
        => ValidateJunctionKeyType(entityName, keyName, key, foreignKeyName, foreignKey, 0, 1);

    /// <summary>
    /// Validates that a many-to-many key member and the junction foreign key that references it have the
    /// same (nullable-unwrapped) type, so the junction can be joined without a runtime cast.
    /// </summary>
    /// <param name="entityName">The declaring entity name, used in the rejection.</param>
    /// <param name="keyName">The descriptive name of the key member.</param>
    /// <param name="key">The selected key member.</param>
    /// <param name="foreignKeyName">The descriptive name of the junction foreign key.</param>
    /// <param name="foreignKey">The selected junction foreign-key member.</param>
    /// <param name="index">The zero-based component position, used in the rejection.</param>
    /// <param name="count">The component count of the leg, used in the rejection.</param>
    /// <exception cref="NotSupportedException">The two member types differ.</exception>
    internal static void ValidateJunctionKeyType(
        string entityName,
        string keyName,
        PropertyInfo key,
        string foreignKeyName,
        PropertyInfo foreignKey,
        int index,
        int count)
    {
        var keyType = Unwrap(key.PropertyType);
        var foreignKeyType = Unwrap(foreignKey.PropertyType);
        if (keyType != foreignKeyType)
            throw new NotSupportedException(
                $"The many-to-many relationship declared on '{entityName}' has the {keyName} '{key.Name}' (component {index + 1} of {count}) of type {keyType} that does not match the junction {foreignKeyName} '{foreignKey.Name}' (component {index + 1} of {count}) of type {foreignKeyType}.");
    }

    private static void ValidateComponentList(
        string relationshipName,
        string role,
        Type entityType,
        IReadOnlyList<IPropertyMetadata> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        if (components.Count == 0)
            throw new NotSupportedException(
                $"The relationship '{relationshipName}' declares no {role} components on {entityType.Name}; at least one component is required.");

        var seen = new HashSet<PropertyInfo>();
        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i]
                ?? throw new NotSupportedException(
                    $"The relationship '{relationshipName}' has a null {role} component at position {i + 1} of {components.Count} on {entityType.Name}.");
            var property = component.PropertyInfo
                ?? throw new NotSupportedException(
                    $"The relationship '{relationshipName}' has a {role} component at position {i + 1} of {components.Count} on {entityType.Name} without a CLR member.");

            if (!seen.Add(property))
                throw new NotSupportedException(
                    $"The relationship '{relationshipName}' repeats the mapped {role} property '{property.Name}' at position {i + 1} of {components.Count} on {entityType.Name}.");
        }
    }

    private static void ValidateJunctionLeg(
        string relationshipName,
        string legName,
        IReadOnlyList<IPropertyMetadata> key,
        IReadOnlyList<IPropertyMetadata> junctionForeignKey)
    {
        if (key.Count != junctionForeignKey.Count)
            throw new NotSupportedException(
                $"The relationship '{relationshipName}' has {key.Count} {legName}-key component(s) but {junctionForeignKey.Count} {legName} junction foreign-key component(s); the arities must match.");

        for (var i = 0; i < key.Count; i++)
        {
            var keyType = Unwrap(key[i].PropertyInfo.PropertyType);
            var foreignKeyType = Unwrap(junctionForeignKey[i].PropertyInfo.PropertyType);
            if (keyType != foreignKeyType)
                throw new NotSupportedException(
                    $"The relationship '{relationshipName}' pairs {legName}-key component {i + 1} of {key.Count} ('{key[i].PropertyInfo.Name}' of type {keyType}) with junction {legName} foreign-key component {i + 1} of {junctionForeignKey.Count} ('{junctionForeignKey[i].PropertyInfo.Name}' of type {foreignKeyType}); the nullable-unwrapped types must match.");
        }
    }

    private static IReadOnlyList<IPropertyMetadata> ResolveComponents(
        Type entityType,
        IReadOnlyList<IPropertyMetadata>? components,
        string relationshipName,
        string role,
        Type declaringType,
        PropertyInfo? navigation)
    {
        if (components is null)
            throw new NotSupportedException(
                $"The relationship '{relationshipName}' exposes no {role} component list on {entityType.Name}; at least one component is required.");

        var resolved = new IPropertyMetadata[components.Count];
        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i]
                ?? throw new NotSupportedException(
                    $"The relationship '{relationshipName}' has a null {role} component at position {i + 1} of {components.Count} on {entityType.Name}.");
            var property = component.PropertyInfo
                ?? throw new NotSupportedException(
                    $"The relationship '{relationshipName}' has a {role} component at position {i + 1} of {components.Count} on {entityType.Name} without a CLR member.");

            resolved[i] = ResolveProperty(entityType, property, declaringType, navigation, role);
        }

        return resolved;
    }

    private static (Type Dependent, Type Principal) ResolveRoles(IRelationshipMetadata relationship, string relationshipName)
        => relationship.Kind switch
        {
            RelationshipKind.OneToMany or RelationshipKind.OneToOne => (relationship.RelatedType, relationship.DeclaringType),
            RelationshipKind.ManyToOne => (relationship.DeclaringType, relationship.RelatedType),
            _ => throw new NotSupportedException(
                $"The relationship '{relationshipName}' has the unsupported kind {relationship.Kind} for a direct key mapping."),
        };

    private static bool HasSameCanonicalMapping(RelationshipMetadata left, RelationshipMetadata right)
    {
        if (left.Kind == RelationshipKind.ManyToMany || right.Kind == RelationshipKind.ManyToMany)
            return left.Kind == RelationshipKind.ManyToMany
                   && right.Kind == RelationshipKind.ManyToMany
                   && HasSameJunctionMapping(left, right);

        if (left.DependentType != right.DependentType || left.PrincipalType != right.PrincipalType)
            return false;

        if (!IsCompatibleCardinality(left.Kind, right.Kind))
            return false;

        var leftForeignKey = left.ForeignKeyProperties;
        var rightForeignKey = right.ForeignKeyProperties;
        if (leftForeignKey is null || rightForeignKey is null || !SameOrderedMembers(leftForeignKey, rightForeignKey))
            return false;

        var leftPrincipalKey = left.PrincipalKeyProperties ?? ResolveDefaultPrincipalKey(left);
        var rightPrincipalKey = right.PrincipalKeyProperties ?? ResolveDefaultPrincipalKey(right);
        return SameOrderedMembers(leftPrincipalKey, rightPrincipalKey);
    }

    private static bool HasSameJunctionMapping(RelationshipMetadata left, RelationshipMetadata right)
    {
        var leftJunction = left.Junction;
        var rightJunction = right.Junction;
        if (leftJunction is null || rightJunction is null || leftJunction.JunctionType != rightJunction.JunctionType)
            return false;

        return SameOrderedMetadata(leftJunction.ParentKey, rightJunction.ChildKey)
               && SameOrderedMetadata(leftJunction.JunctionParentForeignKey, rightJunction.JunctionChildForeignKey)
               && SameOrderedMetadata(leftJunction.ChildKey, rightJunction.ParentKey)
               && SameOrderedMetadata(leftJunction.JunctionChildForeignKey, rightJunction.JunctionParentForeignKey);
    }

    private static IReadOnlyList<PropertyInfo> ResolveDefaultPrincipalKey(RelationshipMetadata relationship)
    {
        var keys = ResolveKeys(relationship.PrincipalType, relationship.DeclaringType, relationship.Navigation);
        var properties = new PropertyInfo[keys.Count];
        for (var i = 0; i < keys.Count; i++)
            properties[i] = keys[i].PropertyInfo;

        return properties;
    }

    private static bool IsCompatibleCardinality(RelationshipKind left, RelationshipKind right)
        => (left == RelationshipKind.OneToMany && right == RelationshipKind.ManyToOne)
           || (left == RelationshipKind.ManyToOne && right == RelationshipKind.OneToMany)
           || (left == RelationshipKind.OneToOne && right == RelationshipKind.ManyToOne)
           || (left == RelationshipKind.ManyToOne && right == RelationshipKind.OneToOne);

    private static bool SameOrderedMembers(IReadOnlyList<PropertyInfo> left, IReadOnlyList<PropertyInfo> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!SameMember(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static bool SameOrderedMetadata(IReadOnlyList<IPropertyMetadata> left, IReadOnlyList<IPropertyMetadata> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!SameMember(left[i].PropertyInfo, right[i].PropertyInfo))
                return false;
        }

        return true;
    }

    private static bool SameMember(PropertyInfo left, PropertyInfo right)
        => left.Equals(right) || (left.Name == right.Name && left.DeclaringType == right.DeclaringType);

    private static Type Unwrap(Type type)
        => Nullable.GetUnderlyingType(type) ?? type;

    private static IEntityMetadata ResolveEntityMetadata(Type entityType)
        => DataContextExtensions.ResolveMetadata(null, entityType);

    /// <summary>Describes a relationship as <c>DeclaringType.Navigation</c> or <c>DeclaringType</c>.</summary>
    internal static string Describe(Type declaringType, PropertyInfo? navigation)
        => navigation is null ? declaringType.Name : $"{declaringType.Name}.{navigation.Name}";
}
