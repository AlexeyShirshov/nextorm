using System.Linq.Expressions;

namespace nextorm.core;
public class ExpressionCache<T> : Dictionary<ExpressionKey, T>
{

}

public class ExpressionKey
{
    private int? _hash;
    private readonly Expression _exp;
    public ExpressionKey(Expression exp)
    {
        _exp = exp;
    }
    public override int GetHashCode() => _hash ??= ExpressionPlanEqualityComparer.Instance.GetHashCode(_exp);
    public override bool Equals(object? obj)
    {
        return Equals(obj as ExpressionKey);
    }
    public bool Equals(ExpressionKey? obj)
    {
        if (obj is null) return false;
        return ExpressionPlanEqualityComparer.Instance.Equals(_exp, obj._exp);
    }
    //public static implicit operator ExpressionKey(Expression exp) => new ExpressionKey(exp);
}