using Microsoft.Extensions.Logging;
namespace NextORM.Core;

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
    public SelectExpressionPlanEqualityComparer(IQueryRegistry queryProvider)
        : this(queryProvider, null)
    {
    }
    public SelectExpressionPlanEqualityComparer(IQueryRegistry queryProvider, ILogger? logger)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
        //_logger = logger;
    }
    // private SelectExpressionPlanEqualityComparer() { }
    // public static SelectExpressionPlanEqualityComparer Instance => new();
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