using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
namespace NextORM.Core;

public sealed class JoinExpressionPlanEqualityComparer : IEqualityComparer<JoinExpression>
{
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    private readonly IQueryRegistry _queryProvider;
    //private readonly ILogger? _logger;
    //private ExpressionPlanEqualityComparer? _expComparer;
    //private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public JoinExpressionPlanEqualityComparer(IQueryRegistry queryProvider)
        : this(queryProvider, null)
    {
    }
    public JoinExpressionPlanEqualityComparer(IQueryRegistry queryProvider, ILogger? logger)
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
        if (x.Strictness != y.Strictness) return false;
        if (x.IsGlobal != y.IsGlobal) return false;

        //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.JoinCondition, y.JoinCondition)) return false;

        //_cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetFromExpressionPlanEqualityComparer().Equals(x.From, y.From)) return false;

        return true;
    }
    public int GetHashCode([DisallowNull] JoinExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new XxHash32();

            hash.Add(obj.JoinType);

            hash.Add(obj.Strictness);

            hash.Add(obj.IsGlobal);

            hash.Add(obj.JoinCondition, _queryProvider.GetExpressionPlanEqualityComparer());

            hash.Add(obj.From, _queryProvider.GetFromExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
}