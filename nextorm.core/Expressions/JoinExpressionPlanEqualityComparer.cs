using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
namespace nextorm.core;

public sealed class JoinExpressionPlanEqualityComparer : IEqualityComparer<JoinExpression>
{
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    private readonly IQueryProvider _queryProvider;
    //private readonly ILogger? _logger;
    //private ExpressionPlanEqualityComparer? _expComparer;
    //private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public JoinExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider)
        : this(cache, queryProvider, null)
    {
    }
    public JoinExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider, ILogger? logger)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
        //_logger = logger;
    }
    //     private JoinExpressionPlanEqualityComparer() { }
    //     public static JoinExpressionPlanEqualityComparer Instance => new();
    public bool Equals(JoinExpression? x, JoinExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.JoinType != y.JoinType) return false;

        //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.JoinCondition, y.JoinCondition)) return false;

        //_cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetQueryPlanEqualityComparer().Equals(x.Query, y.Query)) return false;

        return true;
    }
    public int GetHashCode([DisallowNull] JoinExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.JoinType);

            //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
            hash.Add(obj.JoinCondition, _queryProvider.GetExpressionPlanEqualityComparer());

            if (obj.Query is not null)
            {
                //_cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
                hash.Add(obj.Query, _queryProvider.GetQueryPlanEqualityComparer());
            }

            return hash.ToHashCode();
        }
    }
}