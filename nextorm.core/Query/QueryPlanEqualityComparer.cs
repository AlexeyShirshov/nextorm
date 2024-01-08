using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace nextorm.core;

public sealed class QueryPlanEqualityComparer : IEqualityComparer<QueryCommand>
{
    // private readonly IDictionary<ExpressionKey, Delegate> _cache;
    // private readonly ILogger? _logger;
    //private readonly ExpressionPlanEqualityComparer _expComparer;
    //private readonly SelectExpressionPlanEqualityComparer _selectComparer;
    //private readonly FromExpressionPlanEqualityComparer _fromComparer;
    //private readonly JoinExpressionPlanEqualityComparer _joinComparer;
    private readonly IQueryProvider _queryProvider;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public QueryPlanEqualityComparer(IQueryProvider queryProvider)
        : this(queryProvider, null)
    {
    }
    public QueryPlanEqualityComparer(IQueryProvider queryProvider, ILogger? logger)
    {
        // _cache = cache ?? new ExpressionCache<Delegate>();
        // _logger = logger;
        //_expComparer = new ExpressionPlanEqualityComparer(cache, queryProvider);
        //_selectComparer = new SelectExpressionPlanEqualityComparer(cache, queryProvider);
        //_fromComparer = new FromExpressionPlanEqualityComparer(cache, queryProvider);
        //_joinComparer = new JoinExpressionPlanEqualityComparer(cache, queryProvider);
        _queryProvider = queryProvider;
    }
    // private QueryPlanEqualityComparer() { }
    // public static QueryPlanEqualityComparer Instance => new();
    public bool Equals(QueryCommand? x, QueryCommand? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (!_queryProvider.GetFromExpressionPlanEqualityComparer().Equals(x.From, y.From)) return false;

        if (x.EntityType != y.EntityType) return false;

        if (x.ResultType != y.ResultType) return false;

        if (x.Paging.Limit != y.Paging.Limit || x.Paging.Offset != y.Paging.Offset) return false;

        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.PreparedCondition, y.PreparedCondition)) return false;

        if (x.SelectList is null && y.SelectList is not null) return false;
        if (x.SelectList is not null && y.SelectList is null) return false;

        if (x.SelectList is not null && y.SelectList is not null)
        {
            if (x.SelectList.Count != y.SelectList.Count) return false;

            for (int i = 0; i < x.SelectList.Count; i++)
            {
                if (!_queryProvider.GetSelectExpressionPlanEqualityComparer().Equals(x.SelectList[i], y.SelectList[i])) return false;
            }
        }

        if (x.Joins is null && y.Joins is not null) return false;
        if (x.Joins is not null && y.Joins is null) return false;

        if (x.Joins is not null && y.Joins is not null)
        {
            if (x.Joins.Count != y.Joins.Count) return false;

            for (int i = 0; i < x.Joins.Count; i++)
            {
                if (!_queryProvider.GetJoinExpressionPlanEqualityComparer().Equals(x.Joins[i], y.Joins[i])) return false;
            }
        }

        if (x.Sorting is null && y.Sorting is not null) return false;
        if (x.Sorting is not null && y.Sorting is null) return false;

        if (x.Sorting is not null && y.Sorting is not null)
        {
            if (x.Sorting.Count != y.Sorting.Count) return false;

            for (int i = 0; i < x.Sorting.Count; i++)
            {
                if (!_queryProvider.GetSortingExpressionPlanEqualityComparer().Equals(x.Sorting[i], y.Sorting[i])) return false;
            }
        }

        return true;
    }

    public int GetHashCode([DisallowNull] QueryCommand obj)
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
            if (obj.From is not null)
                hash.Add(_queryProvider.GetFromExpressionPlanEqualityComparer().GetHashCode(obj.From));

            if (obj.EntityType is not null)
                hash.Add(obj.EntityType);

            hash.Add(obj.ResultPlanHash);

            hash.Add(obj.WherePlanHash);

            hash.Add(obj.ColumnsPlanHash);

            hash.Add(obj.JoinPlanHash);

            hash.Add(obj.SortingPlanHash);

            hash.Add(obj.Paging.Limit);
            hash.Add(obj.Paging.Offset);

            return hash.ToHashCode();

        }
    }
}