using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace nextorm.core;
public class ExpressionCache<T> : ConcurrentDictionary<ExpressionKey, T>
{

}

public class ExpressionKey
{
    private int? _hash;
    private readonly Expression _exp;
    private readonly IQueryProvider _queryProvider;

    //private readonly ExpressionPlanEqualityComparer _comparer;

    public ExpressionKey(Expression exp, IDictionary<ExpressionKey, Delegate> cache, IQueryProvider queryProvider)
    {
        _exp = exp;
        _queryProvider = queryProvider;
        //_comparer = new ExpressionPlanEqualityComparer(cache, queryProvider);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "<Pending>")]
    public override int GetHashCode() => _hash ??= _queryProvider.GetExpressionPlanEqualityComparer().GetHashCode(_exp);
    public override bool Equals(object? obj)
    {
        return Equals(obj as ExpressionKey);
    }
    public bool Equals(ExpressionKey? obj)
    {
        if (obj is null) return false;
        return _queryProvider.GetExpressionPlanEqualityComparer().Equals(_exp, obj._exp);
    }
    //public static implicit operator ExpressionKey(Expression exp) => new ExpressionKey(exp);
}