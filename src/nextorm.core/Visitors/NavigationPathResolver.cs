using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Resolves an exact navigation-member chain rooted in a visible query source into an immutable
/// <see cref="ResolvedNavigationPath"/>, using the process-wide configured relationship metadata.
/// </summary>
/// <remarks>
/// <para>
/// Metadata authority: relationship metadata is read through the same configured, CLR-type-keyed path
/// <see cref="RelationshipResolver"/> uses (<see cref="DataContextExtensions.ResolveMetadata(IDataContext?, Type)"/>
/// with a <see langword="null"/> context). A mapping registered through <c>From&lt;T&gt;(cfg)</c> therefore
/// wins over any auto-built mapping; the resolver itself never writes, seeds, overwrites or clears a
/// metadata cache.
/// </para>
/// <para>
/// Source identity: the path root must be a <see cref="ParameterExpression"/> that matches a
/// <see cref="NavigationResolutionScope"/> root binding by reference identity, and each member must be the
/// exact declared navigation member of the current entity. Parameter identity and the exact member
/// sequence decide, never expression text or the entity type alone, so two same-typed sources with
/// different aliases/parameters stay distinct.
/// </para>
/// <para>
/// The resolver never compiles or evaluates an expression, reads a navigation getter, or enumerates entity
/// objects. Every unsupported shape fails closed with <see cref="NotSupportedException"/>.
/// </para>
/// </remarks>
internal static class NavigationPathResolver
{
    /// <summary>
    /// Resolves <paramref name="navigationPath"/> — a navigation-member chain rooted at a visible source
    /// parameter — against <paramref name="scope"/>.
    /// </summary>
    /// <param name="navigationPath">
    /// The navigation-member chain. Every member must be a declared navigation relationship of the current
    /// entity; a scalar member is not part of a resolvable navigation path and is rejected.
    /// </param>
    /// <param name="scope">The scope supplying the current scope identity and visible root bindings.</param>
    /// <returns>The immutable resolved path.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// The path is not rooted at a bound visible parameter, an intermediate hop is a collection, a member is
    /// not a declared navigation, a key is missing/composite, or the path contains a captured/static root,
    /// an arbitrary method call, or a downcast/user-defined conversion. The resolver never returns a null or
    /// default marker instead of throwing.
    /// </exception>
    internal static ResolvedNavigationPath Resolve(Expression navigationPath, NavigationResolutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(navigationPath);
        ArgumentNullException.ThrowIfNull(scope);

        var (root, members) = Decompose(navigationPath);
        var binding = MatchRoot(root, scope);

        var hops = new List<ResolvedNavigationHop>(members.Count);
        var currentType = binding.EntityType;

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var relationship = FindRelationship(currentType, member);
            var isFinal = i == members.Count - 1;

            if (!isFinal && relationship.IsCollection)
                throw new NotSupportedException(
                    $"The navigation member '{Describe(member)}' is a collection and cannot be an intermediate hop of '{navigationPath}'.");

            hops.Add(ResolveHop(relationship));
            currentType = relationship.RelatedType;
        }

        return new ResolvedNavigationPath(scope.ScopeIdentity, binding, hops);
    }

    /// <summary>
    /// Splits the path into its root parameter and the ordered navigation members, rejecting any root that
    /// is not a lambda parameter and any conversion that is not an identity or a safe reference upcast.
    /// </summary>
    private static (ParameterExpression Root, List<PropertyInfo> Members) Decompose(Expression navigationPath)
    {
        var members = new List<PropertyInfo>();
        var node = Unwrap(navigationPath);

        while (true)
        {
            node = Unwrap(node);

            switch (node)
            {
                case ParameterExpression parameter:
                    members.Reverse();
                    return (parameter, members);

                case MemberExpression { Member: PropertyInfo property } member:
                    if (member.Expression is null)
                        throw new NotSupportedException(
                            $"The static property '{Describe(property)}' is not a valid navigation root.");
                    members.Add(property);
                    node = member.Expression;
                    continue;

                case MemberExpression member:
                    throw new NotSupportedException(
                        $"The member '{member.Member.Name}' is not a property and cannot form a navigation path.");

                case ConstantExpression:
                    throw new NotSupportedException(
                        "A captured or constant navigation root is not supported; the path must be rooted at a query source parameter.");

                case MethodCallExpression call:
                    throw new NotSupportedException(
                        $"The method call '{call.Method.Name}' inside the navigation operand is not supported.");

                default:
                    throw new NotSupportedException(
                        $"The navigation root expression '{node.NodeType}' is not supported; the path must be rooted at a query source parameter.");
            }
        }
    }

    /// <summary>
    /// Strips identity conversions and safe reference upcasts. A user-defined conversion, a downcast or any
    /// other numeric/boxing conversion is rejected.
    /// </summary>
    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            if (unary.Method is not null)
                throw new NotSupportedException(
                    "A user-defined conversion inside a navigation path is not supported.");

            var operand = unary.Operand.Type;
            var target = unary.Type;

            if (operand == target)
            {
                expression = unary.Operand;
                continue;
            }

            // A safe reference upcast (including to object) preserves the declared member binding; a
            // downcast or a value-type conversion does not.
            if (!operand.IsValueType && !target.IsValueType && target.IsAssignableFrom(operand))
            {
                expression = unary.Operand;
                continue;
            }

            throw new NotSupportedException(
                $"The conversion from '{operand.Name}' to '{target.Name}' inside a navigation path is not an identity or reference upcast and is not supported.");
        }

        return expression;
    }

    /// <summary>
    /// Matches the root parameter to a visible root binding by reference identity, rejecting an unbound
    /// parameter and a binding owned by a sibling or unregistered scope.
    /// </summary>
    private static NavigationSourceBinding MatchRoot(ParameterExpression root, NavigationResolutionScope scope)
    {
        NavigationSourceBinding? candidate = null;
        var roots = scope.VisibleRoots;

        for (var i = 0; i < roots.Count; i++)
        {
            if (ReferenceEquals(roots[i].Anchor, root))
            {
                candidate = roots[i];
                break;
            }
        }

        if (candidate is null)
            throw new NotSupportedException(
                $"The navigation parameter '{root.Name}' is not bound to a visible source of the current scope.");

        if (!ReferenceEquals(candidate.OwningScope, scope.ScopeIdentity))
            throw new NotSupportedException(
                $"The navigation parameter '{root.Name}' is bound to a source of a sibling or unregistered scope.");

        return candidate;
    }

    /// <summary>
    /// Finds the declared navigation relationship whose member is <paramref name="member"/> on
    /// <paramref name="entityType"/>, resolved through the configured metadata path.
    /// </summary>
    private static IRelationshipMetadata FindRelationship(Type entityType, PropertyInfo member)
    {
        // Exactly RelationshipResolver.ResolveEntityMetadata: the configured, CLR-type-keyed path with a
        // null context. An auto-built fallback is produced by that shared path; this resolver adds no
        // registration of its own.
        var metadata = DataContextExtensions.ResolveMetadata(null, entityType);
        var relationships = metadata.Relationships;

        for (var i = 0; i < relationships.Count; i++)
        {
            if (relationships[i].Navigation is { } navigation && navigation.Equals(member))
                return relationships[i];
        }

        throw new NotSupportedException(
            $"The member '{Describe(member)}' is not a declared navigation relationship on '{entityType.Name}'.");
    }

    /// <summary>Snapshots one relationship into a resolved hop, refusing composite keys.</summary>
    private static ResolvedNavigationHop ResolveHop(IRelationshipMetadata relationship)
    {
        // The navigation is non-null for every relationship returned by FindRelationship (it matched by
        // navigation member).
        var navigation = relationship.Navigation!;
        var declaringType = relationship.DeclaringType;
        var relatedType = relationship.RelatedType;

        switch (relationship.Kind)
        {
            case RelationshipKind.OneToMany:
                RequireSingleKey(relationship);
                return new ResolvedNavigationHop(
                    navigation,
                    declaringType,
                    relatedType,
                    relationship.Kind,
                    isCollection: true,
                    NavigationDirection.PrincipalToDependent,
                    new ResolvedNavigationLeg(relationship.ForeignKey[0], relationship.PrincipalKey[0]));

            case RelationshipKind.ManyToOne:
                RequireSingleKey(relationship);
                return new ResolvedNavigationHop(
                    navigation,
                    declaringType,
                    relatedType,
                    relationship.Kind,
                    isCollection: relationship.IsCollection,
                    NavigationDirection.DependentToPrincipal,
                    new ResolvedNavigationLeg(relationship.ForeignKey[0], relationship.PrincipalKey[0]));

            // HasOneToOne declares the principal side: the declaring type holds the principal key and the
            // related (dependent) type holds the foreign key, so the hop traverses principal to dependent.
            case RelationshipKind.OneToOne:
                RequireSingleKey(relationship);
                return new ResolvedNavigationHop(
                    navigation,
                    declaringType,
                    relatedType,
                    relationship.Kind,
                    isCollection: relationship.IsCollection,
                    NavigationDirection.PrincipalToDependent,
                    new ResolvedNavigationLeg(relationship.ForeignKey[0], relationship.PrincipalKey[0]));

            case RelationshipKind.ManyToMany:
                return ResolveJunctionHop(relationship, navigation);

            default:
                throw new NotSupportedException(
                    $"The relationship kind '{relationship.Kind}' on '{Describe(navigation)}' is not supported.");
        }
    }

    /// <summary>Snapshots a many-to-many relationship, requiring a single-column key on each junction leg.</summary>
    private static ResolvedNavigationHop ResolveJunctionHop(IRelationshipMetadata relationship, PropertyInfo navigation)
    {
        var junction = relationship.Junction
            ?? throw new NotSupportedException(
                $"The many-to-many relationship '{Describe(navigation)}' has no junction metadata.");

        if (junction.ParentKey.Count != 1
            || junction.ChildKey.Count != 1
            || junction.JunctionParentForeignKey.Count != 1
            || junction.JunctionChildForeignKey.Count != 1)
        {
            throw new NotSupportedException(
                $"The many-to-many relationship '{Describe(navigation)}' has a composite junction key; only single-column junction keys are supported.");
        }

        var childLeg = new ResolvedNavigationLeg(junction.JunctionChildForeignKey[0], junction.ChildKey[0]);
        var parentLeg = new ResolvedNavigationLeg(junction.JunctionParentForeignKey[0], junction.ParentKey[0]);

        // The junction mapping identity is resolved through the same configured path; it is captured for
        // plan identity only and is never registered or mutated here.
        var junctionMetadata = DataContextExtensions.ResolveMetadata(null, junction.JunctionType);

        return new ResolvedNavigationHop(
            navigation,
            relationship.DeclaringType,
            relationship.RelatedType,
            RelationshipKind.ManyToMany,
            isCollection: true,
            NavigationDirection.ThroughJunction,
            childLeg,
            parentLeg,
            junction.JunctionType,
            junctionMetadata);
    }

    /// <summary>Fails closed when a relationship leg is not exactly one key member.</summary>
    private static void RequireSingleKey(IRelationshipMetadata relationship)
    {
        if (relationship.ForeignKey.Count != 1 || relationship.PrincipalKey.Count != 1)
            throw new NotSupportedException(
                $"The relationship '{Describe(relationship)}' has a composite key; only single-column relationship keys are supported.");
    }

    private static string Describe(IRelationshipMetadata relationship)
        => relationship.Navigation is { } navigation ? Describe(navigation) : relationship.DeclaringType.Name;

    private static string Describe(PropertyInfo navigation)
        => $"{navigation.DeclaringType?.Name}.{navigation.Name}";
}
