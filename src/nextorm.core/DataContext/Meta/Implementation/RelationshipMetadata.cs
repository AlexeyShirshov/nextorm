using System.Reflection;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Immutable metadata for one declared navigation relationship. The foreign and principal keys are
/// resolved lazily from the participating types' entity metadata, so a relationship can be declared
/// before either type has been registered. Slice A supports single-column keys only, exposed as
/// one-element lists.
/// </summary>
/// <remarks>
/// Instances are published in the process-wide <see cref="DataContextCache.Metadata"/> and read
/// concurrently, so the lazy resolution of <see cref="ForeignKey"/>/<see cref="PrincipalKey"/> and
/// <see cref="Inverse"/> is guarded by <see cref="Lazy{T}"/> (<see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>):
/// exactly one thread runs the factory and every reader observes the fully published value, never a
/// half-written field.
/// </remarks>
internal sealed class RelationshipMetadata : IRelationshipMetadata
{
    private readonly Type _dependentType;
    private readonly Type _principalType;
    private readonly PropertyInfo? _principalKeyProperty;
    private readonly RelationshipJunctionDeclaration? _junctionDeclaration;
    private readonly Lazy<ResolvedKeys> _keys;
    private readonly Lazy<IRelationshipMetadata?> _inverse;
    private readonly Lazy<RelationshipJunctionMetadata?> _junction;

    internal RelationshipMetadata(
        RelationshipKind kind,
        Type declaringType,
        Type relatedType,
        PropertyInfo? navigation,
        bool isCollection,
        PropertyInfo foreignKey,
        PropertyInfo? principalKey,
        Type dependentType,
        Type principalType,
        RelationshipJunctionDeclaration? junction = null)
    {
        Kind = kind;
        DeclaringType = declaringType;
        RelatedType = relatedType;
        Navigation = navigation;
        IsCollection = navigation is not null && isCollection;
        ForeignKeyProperty = foreignKey;
        _principalKeyProperty = principalKey;
        _dependentType = dependentType;
        _principalType = principalType;
        _junctionDeclaration = junction;
        _keys = new Lazy<ResolvedKeys>(ResolveKeys, LazyThreadSafetyMode.ExecutionAndPublication);
        _inverse = new Lazy<IRelationshipMetadata?>(() => RelationshipResolver.FindInverse(this), LazyThreadSafetyMode.ExecutionAndPublication);
        _junction = new Lazy<RelationshipJunctionMetadata?>(ResolveJunction, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public RelationshipKind Kind { get; }

    public Type DeclaringType { get; }

    public Type RelatedType { get; }

    public PropertyInfo? Navigation { get; }

    public bool IsCollection { get; }

    /// <summary>The CLR foreign-key property on the dependent side, kept for inverse matching.</summary>
    internal PropertyInfo ForeignKeyProperty { get; }

    public IReadOnlyList<IPropertyMetadata> ForeignKey => _keys.Value.ForeignKey;

    public IReadOnlyList<IPropertyMetadata> PrincipalKey => _keys.Value.PrincipalKey;

    public IRelationshipMetadata? Inverse => _inverse.Value;

    public RelationshipJunctionMetadata? Junction => _junction.Value;

    private ResolvedKeys ResolveKeys()
    {
        // A many-to-many relationship has no dependent foreign key of its own: the rows on either side are
        // tied through the junction, so the exposed foreign key is the child-side key and the principal key
        // the parent-side key. The junction descriptor validates the parent/child-to-junction type pairs.
        if (_junctionDeclaration is not null)
        {
            var junction = _junction.Value!;
            return new ResolvedKeys(junction.ChildKey, junction.ParentKey);
        }

        var foreignKey = RelationshipResolver.ResolveProperty(_dependentType, ForeignKeyProperty, DeclaringType, Navigation);
        var principalKey = _principalKeyProperty is not null
            ? RelationshipResolver.ResolvePrincipalProperty(_principalType, _principalKeyProperty, DeclaringType, Navigation)
            : RelationshipResolver.ResolveKey(_principalType, DeclaringType, Navigation);

        var foreignKeyType = Nullable.GetUnderlyingType(foreignKey.PropertyInfo.PropertyType) ?? foreignKey.PropertyInfo.PropertyType;
        var principalKeyType = Nullable.GetUnderlyingType(principalKey.PropertyInfo.PropertyType) ?? principalKey.PropertyInfo.PropertyType;
        if (foreignKeyType != principalKeyType)
        {
            var relationshipName = Navigation is null ? DeclaringType.Name : $"{DeclaringType.Name}.{Navigation.Name}";
            throw new NotSupportedException(
                $"The relationship '{relationshipName}' has foreign key '{foreignKey.PropertyInfo.Name}' of type {foreignKeyType} that does not match the principal key '{principalKey.PropertyInfo.Name}' of type {principalKeyType}.");
        }

        return new ResolvedKeys(new[] { foreignKey }, new[] { principalKey });
    }

    private RelationshipJunctionMetadata? ResolveJunction()
    {
        if (_junctionDeclaration is not { } declaration)
            return null;

        return new RelationshipJunctionMetadata(
            declaration.JunctionType,
            ResolveJunctionProperties(_principalType, declaration.ParentKey),
            ResolveJunctionProperties(_dependentType, declaration.ChildKey),
            ResolveJunctionProperties(declaration.JunctionType, declaration.JunctionParentForeignKey),
            ResolveJunctionProperties(declaration.JunctionType, declaration.JunctionChildForeignKey));
    }

    private IReadOnlyList<IPropertyMetadata> ResolveJunctionProperties(Type entityType, IReadOnlyList<PropertyInfo> properties)
    {
        var resolved = new IPropertyMetadata[properties.Count];
        for (var i = 0; i < properties.Count; i++)
            resolved[i] = RelationshipResolver.ResolveProperty(entityType, properties[i], DeclaringType, Navigation);

        return resolved;
    }

    private sealed class ResolvedKeys
    {
        internal ResolvedKeys(IReadOnlyList<IPropertyMetadata> foreignKey, IReadOnlyList<IPropertyMetadata> principalKey)
        {
            ForeignKey = foreignKey;
            PrincipalKey = principalKey;
        }

        internal IReadOnlyList<IPropertyMetadata> ForeignKey { get; }

        internal IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }
    }
}

/// <summary>
/// The CLR-member declaration of a many-to-many junction carried by a <see cref="RelationshipMetadata"/>
/// until <see cref="RelationshipMetadata.Junction"/> is read: it keeps the junction type and the four
/// selected members unresolved, so a relationship can be declared before the junction type is registered.
/// </summary>
internal sealed class RelationshipJunctionDeclaration
{
    /// <summary>The CLR type of the junction entity.</summary>
    public required Type JunctionType { get; init; }

    /// <summary>The parent-side key members.</summary>
    public required IReadOnlyList<PropertyInfo> ParentKey { get; init; }

    /// <summary>The child-side key members.</summary>
    public required IReadOnlyList<PropertyInfo> ChildKey { get; init; }

    /// <summary>The junction members that reference the parent key.</summary>
    public required IReadOnlyList<PropertyInfo> JunctionParentForeignKey { get; init; }

    /// <summary>The junction members that reference the child key.</summary>
    public required IReadOnlyList<PropertyInfo> JunctionChildForeignKey { get; init; }
}
