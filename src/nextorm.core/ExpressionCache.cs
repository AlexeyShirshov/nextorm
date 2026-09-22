using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace NextORM.Core;
/// <summary>
/// A concurrent cache keyed by <see cref="ExpressionKey"/>, used to reuse a compiled artifact per
/// structurally equivalent expression.
/// </summary>
/// <typeparam name="T">The cached artifact type.</typeparam>
public class ExpressionCache<T> : ConcurrentDictionary<ExpressionKey, T>
{

}

/// <summary>
/// Equality wrapper around an <see cref="Expression"/> that uses the query registry's
/// <c>ExpressionPlanEqualityComparer</c>, so two expressions that describe the same query plan hash and
/// compare equal even when they are different object instances. The hash is computed once at
/// construction because both inputs are immutable.
/// </summary>
public sealed class ExpressionKey : IEquatable<ExpressionKey>
{
    private readonly Expression _exp;
    private readonly ExpressionPlanEqualityComparer _equalityComparer;
    // Both inputs are readonly, so the hash is stable for the lifetime of the key.
    // Computing it once up front avoids caching it in a mutable field (S2328 false
    // positive) and any lazy-initialization race when the key is shared.
    private readonly int _hash;

    /// <summary>
    /// Initializes a new key for <paramref name="exp"/> using the plan equality comparer obtained from
    /// <paramref name="queryProvider"/>, and precomputes its hash.
    /// </summary>
    /// <param name="exp">The expression to use as the cache key.</param>
    /// <param name="queryProvider">Registry that supplies the plan equality comparer for the expression.</param>
    public ExpressionKey(Expression exp, IQueryRegistry queryProvider)
    {
        _exp = exp;
        _equalityComparer = queryProvider.GetExpressionPlanEqualityComparer();
        _hash = _equalityComparer.GetHashCode(_exp);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => _hash;
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as ExpressionKey);
    }
    /// <summary>
    /// Determines whether <paramref name="obj"/> describes the same query plan as this key, using the
    /// registry's plan equality comparer.
    /// </summary>
    /// <param name="obj">The other key to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both keys hold plan-equivalent expressions.</returns>
    public bool Equals(ExpressionKey? obj)
    {
        if (obj is null) return false;
        return _equalityComparer.Equals(_exp, obj._exp);
    }
    //public static implicit operator ExpressionKey(Expression exp) => new ExpressionKey(exp);
}
