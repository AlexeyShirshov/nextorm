#define PLAN_CACHE

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public sealed class QueryPlanEqualityComparer : IEqualityComparer<QueryCommand>
{
    private readonly IDictionary<ExpressionKey, Delegate> _cache;
    private readonly ILogger? _logger;
    private readonly ExpressionPlanEqualityComparer _expComparer;
    private readonly SelectExpressionPlanEqualityComparer _selectComparer;
    private readonly FromExpressionPlanEqualityComparer _fromComparer;
    private readonly JoinExpressionPlanEqualityComparer _joinComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public QueryPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider)
        : this(cache, queryProvider, null)
    {
    }
    public QueryPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider, ILogger? logger)
    {
        _cache = cache ?? new ExpressionCache<Delegate>();
        _logger = logger;
        _expComparer = new ExpressionPlanEqualityComparer(cache, queryProvider);
        _selectComparer = new SelectExpressionPlanEqualityComparer(cache, queryProvider);
        _fromComparer = new FromExpressionPlanEqualityComparer(cache, queryProvider);
        _joinComparer = new JoinExpressionPlanEqualityComparer(cache, queryProvider);
    }
    // private QueryPlanEqualityComparer() { }
    // public static QueryPlanEqualityComparer Instance => new();
    public bool Equals(QueryCommand? x, QueryCommand? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (!_fromComparer.Equals(x.From, y.From)) return false;

        if (x.EntityType != y.EntityType) return false;

        if (x.Paging.Limit != y.Paging.Limit || x.Paging.Offset != y.Paging.Offset) return false;

        if (!_expComparer.Equals(x.Condition, y.Condition)) return false;

        if (x.SelectList is null && y.SelectList is not null) return false;
        if (x.SelectList is not null && y.SelectList is null) return false;

        if (x.SelectList is not null && y.SelectList is not null)
        {
            if (x.SelectList.Count != y.SelectList.Count) return false;

            for (int i = 0; i < x.SelectList.Count; i++)
            {
                if (!_selectComparer.Equals(x.SelectList[i], y.SelectList[i])) return false;
            }
        }

        if (x.Joins is null && y.Joins is not null) return false;
        if (x.Joins is not null && y.Joins is null) return false;

        if (x.Joins is not null && y.Joins is not null)
        {
            if (x.Joins.Count != y.Joins.Count) return false;

            for (int i = 0; i < x.Joins.Count; i++)
            {
                if (!_joinComparer.Equals(x.Joins[i], y.Joins[i])) return false;
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
                hash.Add(_fromComparer.GetHashCode(obj.From));

            if (obj.EntityType is not null)
                hash.Add(obj.EntityType);

            if (obj.Condition is not null)
                hash.Add(obj.Condition, _expComparer);

#if PLAN_CACHE
            hash.Add(obj.ColumnsPlanHash);

            hash.Add(obj.JoinPlanHash);

            // obj.PlanHash = hash.ToHashCode();

            // return obj.PlanHash.Value;
#else
#endif

            hash.Add(obj.Paging.Limit);
            hash.Add(obj.Paging.Offset);

            return hash.ToHashCode();

        }
    }
}