using System.Reflection;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Immutable metadata for one declared navigation relationship. The foreign and principal keys are
/// resolved lazily from the participating types' entity metadata, so a relationship can be declared
/// before either type has been registered. Each key is an ordered list of components: the foreign-key
/// order is the declaration/selector order and the principal-key order is the selector order or, for a
/// default principal key, the full key in entity-metadata order. Pairing is positional.
/// </summary>
/// <remarks>
/// Instances are published in the process-wide <see cref="DataContextCache.Metadata"/> and read
/// concurrently, so the lazy resolution of <see cref="ForeignKey"/>/<see cref="PrincipalKey"/> and
/// <see cref="Inverse"/> is guarded by <see cref="Lazy{T}"/> (<see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>):
/// exactly one thread runs the factory and every reader observes the fully published value, never a
/// half-written field. Resolution validates the declared components (non-empty, no duplicates, mapped,
/// equal arity, matching nullable-unwrapped positional types) before any SQL is built.
/// </remarks>
internal sealed class RelationshipMetadata : IRelationshipMetadata
{
    private readonly Type _dependentType;
    private readonly Type _principalType;
    private readonly IReadOnlyList<PropertyInfo>? _foreignKeyProperties;
    private readonly IReadOnlyList<PropertyInfo>? _principalKeyProperties;
    private readonly RelationshipJunctionDeclaration? _junctionDeclaration;
    private readonly Lazy<ResolvedKeys> _keys;
    private readonly Lazy<IRelationshipMetadata?> _inverse;
    private readonly Lazy<RelationshipJunctionMetadata?> _junction;

    /// <summary>
    /// Initializes single-column relationship metadata. Kept so the legacy declarations and the
    /// explicit-key lowers keep constructing one-component relationships; the components are forwarded
    /// to the ordered-list constructor.
    /// </summary>
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
        : this(
            kind,
            declaringType,
            relatedType,
            navigation,
            isCollection,
            new[] { foreignKey },
            principalKey is null ? null : new[] { principalKey },
            dependentType,
            principalType,
            junction)
    {
    }

    /// <summary>Initializes relationship metadata from ordered component lists.</summary>
    internal RelationshipMetadata(
        RelationshipKind kind,
        Type declaringType,
        Type relatedType,
        PropertyInfo? navigation,
        bool isCollection,
        IReadOnlyList<PropertyInfo>? foreignKeys,
        IReadOnlyList<PropertyInfo>? principalKeys,
        Type dependentType,
        Type principalType,
        RelationshipJunctionDeclaration? junction = null)
    {
        Kind = kind;
        DeclaringType = declaringType;
        RelatedType = relatedType;
        Navigation = navigation;
        IsCollection = navigation is not null && isCollection;
        _foreignKeyProperties = foreignKeys;
        _principalKeyProperties = principalKeys;
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

    /// <summary>The ordered CLR foreign-key members on the dependent side, kept for inverse matching.</summary>
    internal IReadOnlyList<PropertyInfo>? ForeignKeyProperties => _foreignKeyProperties;

    /// <summary>
    /// The ordered CLR principal-key members, or <see langword="null"/> when the declaration uses the
    /// default full principal key.
    /// </summary>
    internal IReadOnlyList<PropertyInfo>? PrincipalKeyProperties => _principalKeyProperties;

    internal RelationshipJunctionDeclaration? JunctionDeclaration => _junctionDeclaration;

    /// <summary>The dependent entity type: the type that carries the foreign key.</summary>
    internal Type DependentType => _dependentType;

    /// <summary>The principal entity type: the type that carries the principal key.</summary>
    internal Type PrincipalType => _principalType;

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

        var foreignKey = RelationshipResolver.ResolveProperties(_dependentType, _foreignKeyProperties!, DeclaringType, Navigation, "foreign key");
        var principalKey = _principalKeyProperties is not null
            ? RelationshipResolver.ResolveProperties(_principalType, _principalKeyProperties, DeclaringType, Navigation, "principal key")
            : RelationshipResolver.ResolveKeys(_principalType, DeclaringType, Navigation);

        RelationshipResolver.ValidateResolvedKeys(
            RelationshipResolver.Describe(DeclaringType, Navigation),
            _dependentType,
            foreignKey,
            _principalType,
            principalKey);

        return new ResolvedKeys(foreignKey, principalKey);
    }

    private RelationshipJunctionMetadata? ResolveJunction()
    {
        if (_junctionDeclaration is not { } declaration)
            return null;

        var parentKey = declaration.ParentKey is { } parentKeyProperties
            ? RelationshipResolver.ResolveProperties(_principalType, parentKeyProperties, DeclaringType, Navigation, "parent key")
            : RelationshipResolver.ResolveKeys(_principalType, DeclaringType, Navigation);
        var childKey = declaration.ChildKey is { } childKeyProperties
            ? RelationshipResolver.ResolveProperties(_dependentType, childKeyProperties, DeclaringType, Navigation, "child key")
            : RelationshipResolver.ResolveKeys(_dependentType, DeclaringType, Navigation);
        var junctionParentForeignKey = RelationshipResolver.ResolveProperties(
            declaration.JunctionType,
            declaration.JunctionParentForeignKey,
            DeclaringType,
            Navigation,
            "junction parent foreign key");
        var junctionChildForeignKey = RelationshipResolver.ResolveProperties(
            declaration.JunctionType,
            declaration.JunctionChildForeignKey,
            DeclaringType,
            Navigation,
            "junction child foreign key");

        RelationshipResolver.ValidateResolvedJunction(
            RelationshipResolver.Describe(DeclaringType, Navigation),
            _principalType,
            parentKey,
            _dependentType,
            childKey,
            declaration.JunctionType,
            junctionParentForeignKey,
            junctionChildForeignKey);

        return new RelationshipJunctionMetadata(
            declaration.JunctionType,
            parentKey,
            childKey,
            junctionParentForeignKey,
            junctionChildForeignKey);
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
/// ordered member lists unresolved, so a relationship can be declared before the junction type is
/// registered. The two key lists are <see langword="null"/> when the declaration uses the default full
/// principal keys.
/// </summary>
internal sealed class RelationshipJunctionDeclaration
{
    /// <summary>The CLR type of the junction entity.</summary>
    public required Type JunctionType { get; init; }

    /// <summary>The parent-side key members, or <see langword="null"/> for the full default key.</summary>
    public required IReadOnlyList<PropertyInfo>? ParentKey { get; init; }

    /// <summary>The child-side key members, or <see langword="null"/> for the full default key.</summary>
    public required IReadOnlyList<PropertyInfo>? ChildKey { get; init; }

    /// <summary>The junction members that reference the parent key.</summary>
    public required IReadOnlyList<PropertyInfo> JunctionParentForeignKey { get; init; }

    /// <summary>The junction members that reference the child key.</summary>
    public required IReadOnlyList<PropertyInfo> JunctionChildForeignKey { get; init; }
}
