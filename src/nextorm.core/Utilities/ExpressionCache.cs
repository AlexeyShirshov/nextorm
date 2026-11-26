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
/// Structural key for an <see cref="Expression"/>. The query registry is used to compute the hash
/// once at construction, but it isn't retained for equality comparisons.
/// </summary>
public sealed class ExpressionKey : IEquatable<ExpressionKey>
{
    private readonly Expression _exp;
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
        _hash = queryProvider.GetExpressionPlanEqualityComparer().GetHashCode(exp);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => _hash;
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as ExpressionKey);
    }
    /// <summary>
    /// Determines whether <paramref name="obj"/> describes a plan-equivalent expression.
    /// </summary>
    /// <param name="obj">The other key to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both keys hold plan-equivalent expressions.</returns>
    public bool Equals(ExpressionKey? obj)
    {
        if (obj is null) return false;
        return ExpressionPlanEqualityComparer.AreEqual(_exp, obj._exp);
    }
    //public static implicit operator ExpressionKey(Expression exp) => new ExpressionKey(exp);
}
