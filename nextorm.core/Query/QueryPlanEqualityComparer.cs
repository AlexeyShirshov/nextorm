using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace nextorm.core;

public sealed class QueryPlanEqualityComparer : IEqualityComparer<QueryCommand?>
{
    // private readonly IDictionary<ExpressionKey, Delegate> _cache;
    // private readonly ILogger? _logger;
    private readonly ExpressionPlanEqualityComparer _expComparer;
    private readonly SelectExpressionPlanEqualityComparer _selectComparer;
    private readonly FromExpressionPlanEqualityComparer _fromComparer;
    private readonly JoinExpressionPlanEqualityComparer _joinComparer;
    private readonly SortingExpressionPlanEqualityComparer _sortComparer;
    //private readonly IQueryProvider _queryProvider;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public QueryPlanEqualityComparer(IQueryProvider queryProvider)
    {
        //_queryProvider = queryProvider;
        _expComparer = queryProvider.GetExpressionPlanEqualityComparer();
        _selectComparer = queryProvider.GetSelectExpressionPlanEqualityComparer();
        _fromComparer = queryProvider.GetFromExpressionPlanEqualityComparer();
        _joinComparer = queryProvider.GetJoinExpressionPlanEqualityComparer();
        _sortComparer = queryProvider.GetSortingExpressionPlanEqualityComparer();
    }
    // private QueryPlanEqualityComparer() { }
    // public static QueryPlanEqualityComparer Instance => new();
    public bool Equals(QueryCommand? x, QueryCommand? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.EntityType != y.EntityType) return false;

        if (x.ResultType != y.ResultType) return false;

        if (x.Paging.Limit != y.Paging.Limit || x.Paging.Offset != y.Paging.Offset) return false;

        if (x.UnionType != y.UnionType) return false;

        if (!_fromComparer.Equals(x.From, y.From)) return false;

        if (!_selectComparer.Equals(x.SelectList, y.SelectList)) return false;

        if (!_expComparer.Equals(x.PreparedCondition, y.PreparedCondition)) return false;

        if (!_joinComparer.Equals(x.Joins, y.Joins)) return false;

        if (!_sortComparer.ValueEquals(x.Sorting, y.Sorting)) return false;

        if (!_selectComparer.Equals(x.GroupingList, y.GroupingList)) return false;

        if (!_expComparer.Equals(x.Having, y.Having)) return false;

        if (!Equals(x.UnionQuery, y.UnionQuery)) return false;

        if (!IEqualityComparerExtensions.Equals(this, x.ReferencedQueries, y.ReferencedQueries)) return false;

        return true;
    }

    public int GetHashCode(QueryCommand? obj)
    {
        if (obj is null)
            return 0;

        // #if PLAN_CACHE
        //         if (obj.PlanHash.HasValue)
        //             return obj.PlanHash.Value;
        // #endif
        unchecked
        {
            HashCode hash = new();

            if (obj.FromPlanHash != 0)
                hash.Add(obj.FromPlanHash);

            if (obj.EntityType is not null)
                hash.Add(obj.EntityType);

            if (obj.ResultPlanHash != 0)
                hash.Add(obj.ResultPlanHash);

            if (obj.WherePlanHash != 0)
                hash.Add(obj.WherePlanHash);

            if (obj.ColumnsPlanHash != 0)
                hash.Add(obj.ColumnsPlanHash);

            if (obj.JoinPlanHash != 0)
                hash.Add(obj.JoinPlanHash);

            if (obj.SortingPlanHash != 0)
                hash.Add(obj.SortingPlanHash);

            hash.Add(obj.Paging.Limit);
            hash.Add(obj.Paging.Offset);

            if (obj.GroupingPlanHash != 0)
                hash.Add(obj.GroupingPlanHash);

            if (obj.UnionPlanHash != 0)
                hash.Add(obj.UnionPlanHash);

            if (obj.ReferencedQueriesPlanHash != 0)
                hash.Add(obj.ReferencedQueriesPlanHash);

            return hash.ToHashCode();
        }
    }
}