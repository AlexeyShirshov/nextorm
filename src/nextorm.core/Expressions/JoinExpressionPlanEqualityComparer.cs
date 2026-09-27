using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
namespace NextORM.Core;

/// <summary>
/// Compares and hashes joins for query-plan caching, taking the join type, strictness, global flag,
/// condition and joined source into account.
/// </summary>
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
    /// <summary>Initializes a comparer without a logger.</summary>
    /// <param name="queryProvider">The registry that supplies the nested plan comparers.</param>
    public JoinExpressionPlanEqualityComparer(IQueryRegistry queryProvider)
        : this(queryProvider, null)
    {
    }
    /// <summary>Initializes a comparer.</summary>
    /// <param name="queryProvider">The registry that supplies the nested plan comparers.</param>
    /// <param name="logger">An optional logger; currently unused.</param>
    public JoinExpressionPlanEqualityComparer(IQueryRegistry queryProvider, ILogger? logger)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
        //_logger = logger;
    }
    //     private JoinExpressionPlanEqualityComparer() { }
    //     public static JoinExpressionPlanEqualityComparer Instance => new();
    /// <summary>Determines whether two joins are equal for plan-cache purposes.</summary>
    /// <param name="x">The first join.</param>
    /// <param name="y">The second join.</param>
    /// <returns><c>true</c> when both joins describe the same operation with equivalent expressions.</returns>
    public bool Equals(JoinExpression? x, JoinExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.JoinType != y.JoinType) return false;
        if (x.Strictness != y.Strictness) return false;
        if (x.IsGlobal != y.IsGlobal) return false;
        if (!string.Equals(x.JoinHint, y.JoinHint, StringComparison.Ordinal)) return false;

        //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.JoinCondition, y.JoinCondition)) return false;

        //_cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetFromExpressionPlanEqualityComparer().Equals(x.From, y.From)) return false;

        return true;
    }
    /// <summary>Returns a hash code for a join consistent with the equality comparison.</summary>
    /// <param name="obj">The join to hash.</param>
    /// <returns>The hash code, or zero when <paramref name="obj"/> is <c>null</c>.</returns>
    public int GetHashCode([DisallowNull] JoinExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new XxHash32();

            hash.Add(obj.JoinType);

            hash.Add(obj.Strictness);

            hash.Add(obj.IsGlobal);

            hash.Add(obj.JoinHint);

            hash.Add(obj.JoinCondition, _queryProvider.GetExpressionPlanEqualityComparer());

            hash.Add(obj.From, _queryProvider.GetFromExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
}