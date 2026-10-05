using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// How a navigation hop traverses its declared relationship: which side carries the foreign key.
/// </summary>
internal enum NavigationDirection
{
    /// <summary>A navigation whose foreign key lives on the declaring (dependent) side.</summary>
    DependentToPrincipal,

    /// <summary>A navigation whose foreign key lives on the related (dependent) side: a collection for a
    /// one-to-many relationship, a reference for a one-to-one relationship.</summary>
    PrincipalToDependent,

    /// <summary>A collection navigation realized through a junction entity (many-to-many).</summary>
    ThroughJunction,
}

/// <summary>
/// One resolved foreign-key/principal-key leg of a navigation hop: for a single-column relationship the
/// only pair, and for a many-to-many relationship one of the two junction legs.
/// </summary>
internal sealed class ResolvedNavigationLeg
{
    /// <summary>Creates a leg from its resolved mapped members.</summary>
    /// <param name="foreignKey">The dependent-side foreign-key member.</param>
    /// <param name="principalKey">The principal-side key member.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    internal ResolvedNavigationLeg(IPropertyMetadata foreignKey, IPropertyMetadata principalKey)
    {
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(principalKey);

        ForeignKey = foreignKey;
        PrincipalKey = principalKey;
    }

    /// <summary>The dependent-side foreign-key member.</summary>
    internal IPropertyMetadata ForeignKey { get; }

    /// <summary>The principal-side key member.</summary>
    internal IPropertyMetadata PrincipalKey { get; }

    /// <summary>Compares the leg's mapped-member identity without hashing.</summary>
    /// <param name="other">The leg to compare with.</param>
    /// <returns><see langword="true"/> when both members are the same mapped properties.</returns>
    internal bool SameIdentity(ResolvedNavigationLeg other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return SameProperty(ForeignKey, other.ForeignKey) && SameProperty(PrincipalKey, other.PrincipalKey);
    }

    /// <summary>Hashes the leg's mapped-member identity consistently with <see cref="SameIdentity"/>.</summary>
    /// <returns>A hash code over the mapped-member identity.</returns>
    internal int IdentityHashCode()
        => HashCode.Combine(PropertyHash(ForeignKey), PropertyHash(PrincipalKey));

    private static bool SameProperty(IPropertyMetadata left, IPropertyMetadata right)
        => left.PropertyInfo.Equals(right.PropertyInfo)
           && string.Equals(left.ColumnName, right.ColumnName, StringComparison.Ordinal)
           && left.IsKey == right.IsKey;

    private static int PropertyHash(IPropertyMetadata property)
        => HashCode.Combine(property.PropertyInfo, property.ColumnName, property.IsKey);
}

/// <summary>
/// One resolved navigation member of a path: the exact declared member, the participating types, the
/// relationship kind/cardinality and direction, and the resolved key leg(s) — one for a single-column
/// relationship and both junction legs for a many-to-many relationship.
/// </summary>
/// <remarks>
/// The hop is an immutable snapshot: it retains only the required <see cref="IPropertyMetadata"/> /
/// <see cref="PropertyInfo"/> members and, for a junction, the junction type and its mapping identity. It
/// never retains the mutable <see cref="IRelationshipMetadata"/> object itself.
/// </remarks>
internal sealed class ResolvedNavigationHop
{
    /// <summary>Creates a resolved hop.</summary>
    /// <param name="navigation">The exact declared navigation member on the declaring type.</param>
    /// <param name="declaringType">The entity type that declares the navigation.</param>
    /// <param name="relatedType">The entity type the navigation points to.</param>
    /// <param name="kind">The declared relationship cardinality.</param>
    /// <param name="isCollection">Whether the navigation is a collection.</param>
    /// <param name="direction">How the hop traverses the relationship.</param>
    /// <param name="primaryLeg">The first leg: the only leg, or the child leg of a junction.</param>
    /// <param name="secondaryLeg">The parent leg of a many-to-many junction, else <see langword="null"/>.</param>
    /// <param name="junctionType">The junction entity type of a many-to-many relationship, else <see langword="null"/>.</param>
    /// <param name="junctionMetadata">The junction mapping identity of a many-to-many relationship, else <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    internal ResolvedNavigationHop(
        PropertyInfo navigation,
        Type declaringType,
        Type relatedType,
        RelationshipKind kind,
        bool isCollection,
        NavigationDirection direction,
        ResolvedNavigationLeg primaryLeg,
        ResolvedNavigationLeg? secondaryLeg = null,
        Type? junctionType = null,
        IEntityMetadata? junctionMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(relatedType);
        ArgumentNullException.ThrowIfNull(primaryLeg);

        Navigation = navigation;
        DeclaringType = declaringType;
        RelatedType = relatedType;
        Kind = kind;
        IsCollection = isCollection;
        Direction = direction;
        PrimaryLeg = primaryLeg;
        SecondaryLeg = secondaryLeg;
        JunctionType = junctionType;
        JunctionMetadata = junctionMetadata;
    }

    /// <summary>The exact declared navigation member on <see cref="DeclaringType"/>.</summary>
    internal PropertyInfo Navigation { get; }

    /// <summary>The entity type that declares the navigation.</summary>
    internal Type DeclaringType { get; }

    /// <summary>The entity type the navigation points to.</summary>
    internal Type RelatedType { get; }

    /// <summary>The declared relationship cardinality.</summary>
    internal RelationshipKind Kind { get; }

    /// <summary>Whether the navigation is a collection.</summary>
    internal bool IsCollection { get; }

    /// <summary>How the hop traverses the relationship.</summary>
    internal NavigationDirection Direction { get; }

    /// <summary>The first key leg: the only leg, or the child leg of a junction.</summary>
    internal ResolvedNavigationLeg PrimaryLeg { get; }

    /// <summary>The parent key leg of a many-to-many junction, else <see langword="null"/>.</summary>
    internal ResolvedNavigationLeg? SecondaryLeg { get; }

    /// <summary>The junction entity type of a many-to-many relationship, else <see langword="null"/>.</summary>
    internal Type? JunctionType { get; }

    /// <summary>The junction mapping identity of a many-to-many relationship, else <see langword="null"/>.</summary>
    internal IEntityMetadata? JunctionMetadata { get; }

    /// <summary>Whether the hop is realized through a many-to-many junction.</summary>
    internal bool IsManyToMany => Direction == NavigationDirection.ThroughJunction;

    /// <summary>Compares the hop's component identity without hashing.</summary>
    /// <param name="other">The hop to compare with.</param>
    /// <returns><see langword="true"/> when every component matches.</returns>
    internal bool SameIdentity(ResolvedNavigationHop other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Navigation.Equals(other.Navigation)
               && DeclaringType == other.DeclaringType
               && RelatedType == other.RelatedType
               && Kind == other.Kind
               && IsCollection == other.IsCollection
               && Direction == other.Direction
               && PrimaryLeg.SameIdentity(other.PrimaryLeg)
               && SameOptionalLeg(SecondaryLeg, other.SecondaryLeg)
               && JunctionType == other.JunctionType
               && ReferenceEquals(JunctionMetadata, other.JunctionMetadata);
    }

    /// <summary>Hashes the hop's component identity consistently with <see cref="SameIdentity"/>.</summary>
    /// <returns>A hash code over the components.</returns>
    internal int IdentityHashCode()
    {
        var hash = new HashCode();
        hash.Add(Navigation);
        hash.Add(DeclaringType);
        hash.Add(RelatedType);
        hash.Add(Kind);
        hash.Add(IsCollection);
        hash.Add(Direction);
        hash.Add(PrimaryLeg.IdentityHashCode());
        hash.Add(SecondaryLeg is null ? 0 : SecondaryLeg.IdentityHashCode());
        hash.Add(JunctionType);
        hash.Add(JunctionMetadata is null ? 0 : RuntimeHelpers.GetHashCode(JunctionMetadata));
        return hash.ToHashCode();
    }

    private static bool SameOptionalLeg(ResolvedNavigationLeg? left, ResolvedNavigationLeg? right)
        => left is null ? right is null : right is not null && left.SameIdentity(right);
}

/// <summary>
/// The immutable result of resolving a navigation-member chain: the scope it was resolved in, the root
/// source binding it is anchored to, and the ordered resolved hops.
/// </summary>
/// <remarks>
/// The path is a snapshot with no writable arrays and no retained <see cref="IRelationshipMetadata"/>.
/// Equality compares the scope identity, the root binding identity and every hop's components, including
/// exact member identity; hash codes are derived from the same components but are never used as a
/// substitute for the comparison.
/// </remarks>
internal sealed class ResolvedNavigationPath : IEquatable<ResolvedNavigationPath>
{
    /// <summary>Creates a resolved navigation path.</summary>
    /// <param name="scopeIdentity">The identity of the scope the path was resolved in.</param>
    /// <param name="rootBinding">The root source binding the path is anchored to.</param>
    /// <param name="hops">The ordered resolved navigation hops (empty for a root-only path).</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    internal ResolvedNavigationPath(
        object scopeIdentity,
        NavigationSourceBinding rootBinding,
        IReadOnlyList<ResolvedNavigationHop> hops)
    {
        ArgumentNullException.ThrowIfNull(scopeIdentity);
        ArgumentNullException.ThrowIfNull(rootBinding);
        ArgumentNullException.ThrowIfNull(hops);

        ScopeIdentity = scopeIdentity;
        RootBinding = rootBinding;
        // Back the exposed list with a read-only wrapper over a copied array: a collection expression
        // targeted at IReadOnlyList<T> would materialize a mutable List<T> that a caller could downcast
        // and mutate, breaking the snapshot.
        Hops = Array.AsReadOnly<ResolvedNavigationHop>([.. hops]);
    }

    /// <summary>The identity of the scope the path was resolved in.</summary>
    internal object ScopeIdentity { get; }

    /// <summary>The root source binding the path is anchored to, carrying the owning-source identity.</summary>
    internal NavigationSourceBinding RootBinding { get; }

    /// <summary>The ordered resolved navigation hops; empty for a root-only path.</summary>
    internal IReadOnlyList<ResolvedNavigationHop> Hops { get; }

    /// <summary>The entity type the path starts from.</summary>
    internal Type RootEntityType => RootBinding.EntityType;

    /// <summary>The final hop, or <see langword="null"/> for a root-only path.</summary>
    internal ResolvedNavigationHop? FinalHop => Hops.Count > 0 ? Hops[^1] : null;

    /// <inheritdoc/>
    public bool Equals(ResolvedNavigationPath? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        if (!ReferenceEquals(ScopeIdentity, other.ScopeIdentity))
            return false;

        if (!RootBinding.SameIdentity(other.RootBinding))
            return false;

        if (Hops.Count != other.Hops.Count)
            return false;

        for (var i = 0; i < Hops.Count; i++)
        {
            if (!Hops[i].SameIdentity(other.Hops[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ResolvedNavigationPath);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(ScopeIdentity));
        hash.Add(RootBinding.IdentityHashCode());

        for (var i = 0; i < Hops.Count; i++)
            hash.Add(Hops[i].IdentityHashCode());

        return hash.ToHashCode();
    }
}
