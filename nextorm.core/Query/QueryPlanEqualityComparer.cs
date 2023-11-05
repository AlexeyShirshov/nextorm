using System.Diagnostics.CodeAnalysis;
using nextorm.core;

public sealed class QueryPlanEqualityComparer : IEqualityComparer<QueryCommand>
{
    private QueryPlanEqualityComparer() { }
    public static QueryPlanEqualityComparer Instance => new();
    public bool Equals(QueryCommand? x, QueryCommand? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (!FromExpressionPlanEqualityComparer.Instance.Equals(x.From, y.From)) return false;

        if (x.EntityType != y.EntityType) return false;

        if (!ExpressionPlanEqualityComparer.Instance.Equals(x.Condition, y.Condition)) return false;

        if (x.SelectList is null && y.SelectList is not null) return false;
        if (x.SelectList is not null && y.SelectList is null) return false;

        if (x.SelectList is not null && y.SelectList is not null)
        {
            if (x.SelectList.Count != y.SelectList.Count) return false;

            for (int i = 0; i < x.SelectList.Count; i++)
            {
                if (!SelectExpressionPlanEqualityComparer.Instance.Equals(x.SelectList[i], y.SelectList[i])) return false;
            }
        }

        if (x.Joins is null && y.Joins is not null) return false;
        if (x.Joins is not null && y.Joins is null) return false;

        if (x.Joins is not null && y.Joins is not null)
        {
            if (x.Joins.Count != y.Joins.Count) return false;

            for (int i = 0; i < x.Joins.Count; i++)
            {
                if (!JoinExpressionPlanEqualityComparer.Instance.Equals(x.Joins[i], y.Joins[i])) return false;
            }
        }

        return true;
    }

    public int GetHashCode([DisallowNull] QueryCommand obj)
    {
        if (obj is null)
            return 0;

        if (obj.PlanHash.HasValue)
            return obj.PlanHash.Value;

        unchecked
        {
            HashCode hash = new();
            if (obj.From is not null)
                hash.Add(FromExpressionPlanEqualityComparer.Instance.GetHashCode(obj.From));

            if (obj.EntityType is not null)
                hash.Add(obj.EntityType);

            if (obj.Condition is not null)
                hash.Add(obj.Condition, ExpressionPlanEqualityComparer.Instance);

            hash.Add(obj.ColumnsPlanHash);

            hash.Add(obj.JoinPlanHash);

            obj.PlanHash = hash.ToHashCode();

            return obj.PlanHash.Value;
        }
    }
}