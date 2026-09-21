using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace NextORM.Core;
public class ExpressionCache<T> : ConcurrentDictionary<ExpressionKey, T>
{

}

public sealed class ExpressionKey : IEquatable<ExpressionKey>
{
    private readonly Expression _exp;
    private readonly ExpressionPlanEqualityComparer _equalityComparer;
    // Both inputs are readonly, so the hash is stable for the lifetime of the key.
    // Computing it once up front avoids caching it in a mutable field (S2328 false
    // positive) and any lazy-initialization race when the key is shared.
    private readonly int _hash;

    public ExpressionKey(Expression exp, IQueryRegistry queryProvider)
    {
        _exp = exp;
        _equalityComparer = queryProvider.GetExpressionPlanEqualityComparer();
        _hash = _equalityComparer.GetHashCode(_exp);
    }

    public override int GetHashCode() => _hash;
    public override bool Equals(object? obj)
    {
        return Equals(obj as ExpressionKey);
    }
    public bool Equals(ExpressionKey? obj)
    {
        if (obj is null) return false;
        return _equalityComparer.Equals(_exp, obj._exp);
    }
    //public static implicit operator ExpressionKey(Expression exp) => new ExpressionKey(exp);
}
