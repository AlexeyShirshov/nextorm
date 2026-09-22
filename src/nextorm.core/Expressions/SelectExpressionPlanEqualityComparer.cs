using Microsoft.Extensions.Logging;
namespace NextORM.Core;

/// <summary>
/// Compares and hashes projected columns for query-plan caching, taking the column ordinal, target
/// property, null-defaulting flag and source expression into account.
/// </summary>
public sealed class SelectExpressionPlanEqualityComparer : IEqualityComparer<SelectExpression>
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
    /// <param name="queryProvider">The registry that supplies the expression plan comparer.</param>
    public SelectExpressionPlanEqualityComparer(IQueryRegistry queryProvider)
        : this(queryProvider, null)
    {
    }
    /// <summary>Initializes a comparer.</summary>
    /// <param name="queryProvider">The registry that supplies the expression plan comparer.</param>
    /// <param name="logger">An optional logger; currently unused.</param>
    public SelectExpressionPlanEqualityComparer(IQueryRegistry queryProvider, ILogger? logger)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
        //_logger = logger;
    }
    // private SelectExpressionPlanEqualityComparer() { }
    // public static SelectExpressionPlanEqualityComparer Instance => new();
    /// <summary>Determines whether two projected columns are equal for plan-cache purposes.</summary>
    /// <param name="x">The first column.</param>
    /// <param name="y">The second column.</param>
    /// <returns><c>true</c> when both columns describe the same projection.</returns>
    public bool Equals(SelectExpression? x, SelectExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        if (x.Index != y.Index) return false;

        if (x.PropertyType != y.PropertyType) return false;

        if (x.PropertyName != y.PropertyName) return false;

        if (x.DefaultOnNull != y.DefaultOnNull) return false;

        //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
        if (!_queryProvider.GetExpressionPlanEqualityComparer().Equals(x.Expression, y.Expression))
            return false;

        return true;
    }

    /// <summary>Returns a hash code for a projected column consistent with the equality comparison.</summary>
    /// <param name="obj">The column to hash.</param>
    /// <returns>The hash code, or zero when <paramref name="obj"/> is <c>null</c>.</returns>
    public int GetHashCode(SelectExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new XxHash32();

            hash.Add(obj.Index);

            hash.Add(obj.PropertyType);

            hash.Add(obj.PropertyName);

            hash.Add(obj.DefaultOnNull);

            //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
            hash.Add(obj.Expression, _queryProvider.GetExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
}