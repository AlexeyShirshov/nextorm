using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace nextorm.core;
public class ExpressionCache<T> : ConcurrentDictionary<ExpressionKey, T>
{

}

public sealed class ExpressionKey(Expression exp, IQueryProvider queryProvider) : IEquatable<ExpressionKey>
{
    private readonly Expression _exp = exp;
    private readonly ExpressionPlanEqualityComparer _equalityComparer = queryProvider.GetExpressionPlanEqualityComparer();
    private int? _hash;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
    public override int GetHashCode() => _hash ??= _equalityComparer.GetHashCode(_exp);
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