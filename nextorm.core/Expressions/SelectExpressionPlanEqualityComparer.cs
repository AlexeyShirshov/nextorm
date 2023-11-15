using System.Diagnostics.CodeAnalysis;
using nextorm.core;

public sealed class SelectExpressionPlanEqualityComparer : IEqualityComparer<SelectExpression>
{
    private SelectExpressionPlanEqualityComparer() { }
    public static SelectExpressionPlanEqualityComparer Instance => new();
    public bool Equals(SelectExpression? x, SelectExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.Index != y.Index) return false;

        if (x.PropertyType != y.PropertyType) return false;

        if (x.PropertyName != y.PropertyName) return false;

        return x.Expression.IsT0 == y.Expression.IsT0
            && x.Expression.Match(cmd => QueryPlanEqualityComparer.Instance.Equals(cmd, y.Expression.AsT0), e => ExpressionPlanEqualityComparer.Instance.Equals(e, y.Expression.AsT1));
    }

    public int GetHashCode([DisallowNull] SelectExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.Index);

            hash.Add(obj.PropertyType);

            hash.Add(obj.PropertyName);

            obj.Expression.Switch(cmd => hash.Add(cmd, QueryPlanEqualityComparer.Instance), exp => hash.Add(exp, ExpressionPlanEqualityComparer.Instance));

            return hash.ToHashCode();
        }

    }
}