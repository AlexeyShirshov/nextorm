using System.Diagnostics.CodeAnalysis;
using nextorm.core;

public sealed class JoinExpressionPlanEqualityComparer : IEqualityComparer<JoinExpression>
{
    private JoinExpressionPlanEqualityComparer() { }
    public static JoinExpressionPlanEqualityComparer Instance => new();
    public bool Equals(JoinExpression? x, JoinExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.JoinType != y.JoinType) return false;

        if (!ExpressionPlanEqualityComparer.Instance.Equals(x.JoinCondition, y.JoinCondition)) return false;

        if (!QueryPlanEqualityComparer.Instance.Equals(x.Query, y.Query)) return false;

        return true;
    }
    public int GetHashCode([DisallowNull] JoinExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.JoinType);

            hash.Add(obj.JoinCondition, ExpressionPlanEqualityComparer.Instance);

            if (obj.Query is not null)
                hash.Add(obj.Query, QueryPlanEqualityComparer.Instance);

            return hash.ToHashCode();
        }
    }
}