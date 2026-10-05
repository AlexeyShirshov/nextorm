using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// The scope a <see cref="NavigationPathResolver"/> resolves a navigation path against: the identity of
/// the command scope being resolved plus the explicit root source bindings visible in it.
/// </summary>
/// <remarks>
/// The scope carries the source identity rules the query pipeline already uses: a root parameter is
/// matched to a binding by <see cref="ParameterExpression"/> reference identity, so two same-typed
/// sources with different parameters are distinct, and a binding owned by a different scope is not
/// visible. The scope is a plain read-only carrier; the resolver never mutates it.
/// </remarks>
internal sealed class NavigationResolutionScope
{
    /// <summary>Creates a scope over the given visible root bindings.</summary>
    /// <param name="scopeIdentity">The identity of the command scope being resolved.</param>
    /// <param name="visibleRoots">The root source bindings exposed to the resolver.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    internal NavigationResolutionScope(object scopeIdentity, IReadOnlyList<NavigationSourceBinding> visibleRoots)
    {
        ArgumentNullException.ThrowIfNull(scopeIdentity);
        ArgumentNullException.ThrowIfNull(visibleRoots);

        ScopeIdentity = scopeIdentity;
        // Back the exposed list with a read-only wrapper over a copied array: a collection expression
        // targeted at IReadOnlyList<T> would materialize a mutable List<T> that a caller could downcast
        // and mutate, breaking the scope's read-only contract.
        VisibleRoots = Array.AsReadOnly<NavigationSourceBinding>([.. visibleRoots]);
    }

    /// <summary>The identity of the command scope being resolved.</summary>
    internal object ScopeIdentity { get; }

    /// <summary>The root source bindings exposed to the resolver, in the caller's order.</summary>
    internal IReadOnlyList<NavigationSourceBinding> VisibleRoots { get; }
}

/// <summary>
/// Binds one query source to the expression anchor that denotes it inside a resolvable navigation path:
/// the owning scope, the source identity, the root expression anchor and the entity type it exposes.
/// </summary>
/// <remarks>
/// <see cref="SourceIdentity"/> is an opaque token compared by reference; it is the
/// <see cref="ParameterExpression"/> of a lambda source or the <see cref="FromExpression"/> of a
/// registered source. This is what makes two same-typed sources with different aliases/parameters
/// distinct. <see cref="Anchor"/> is the root expression a navigation path must start with (the lambda
/// parameter), also compared by reference.
/// </remarks>
internal sealed class NavigationSourceBinding
{
    /// <summary>Creates a root source binding.</summary>
    /// <param name="owningScope">The identity of the scope that owns the source.</param>
    /// <param name="sourceIdentity">The opaque source identity token (parameter or <see cref="FromExpression"/>).</param>
    /// <param name="anchor">The root expression that denotes the source in a navigation path.</param>
    /// <param name="entityType">The entity type exposed by the source.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    internal NavigationSourceBinding(object owningScope, object sourceIdentity, Expression anchor, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(owningScope);
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(entityType);

        OwningScope = owningScope;
        SourceIdentity = sourceIdentity;
        Anchor = anchor;
        EntityType = entityType;
    }

    /// <summary>The identity of the scope that owns the source.</summary>
    internal object OwningScope { get; }

    /// <summary>The opaque source identity token, compared by reference.</summary>
    internal object SourceIdentity { get; }

    /// <summary>The root expression that denotes the source in a navigation path, compared by reference.</summary>
    internal Expression Anchor { get; }

    /// <summary>The entity type exposed by the source.</summary>
    internal Type EntityType { get; }

    /// <summary>Compares the binding's reference-identity components without hashing.</summary>
    /// <param name="other">The binding to compare with.</param>
    /// <returns><see langword="true"/> when every identity component matches.</returns>
    internal bool SameIdentity(NavigationSourceBinding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ReferenceEquals(OwningScope, other.OwningScope)
               && ReferenceEquals(SourceIdentity, other.SourceIdentity)
               && ReferenceEquals(Anchor, other.Anchor)
               && EntityType == other.EntityType;
    }

    /// <summary>Hashes the binding's reference-identity components consistently with <see cref="SameIdentity"/>.</summary>
    /// <returns>A hash code over the identity components.</returns>
    internal int IdentityHashCode()
        => HashCode.Combine(
            RuntimeHelpers.GetHashCode(OwningScope),
            RuntimeHelpers.GetHashCode(SourceIdentity),
            RuntimeHelpers.GetHashCode(Anchor),
            EntityType);
}
