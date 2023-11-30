using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
namespace nextorm.core;

public sealed class SelectExpressionPlanEqualityComparer : IEqualityComparer<SelectExpression>
{
    private readonly IDictionary<ExpressionKey, Delegate> _cache;
    private readonly IQueryProvider _queryProvider;
    private readonly ILogger? _logger;
    private ExpressionPlanEqualityComparer? _expComparer;
    private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public SelectExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider)
        : this(cache, queryProvider, null)
    {
    }
    public SelectExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider, ILogger? logger)
    {
        _cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
        _logger = logger;
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

        return x.Expression.IsT0 == y.Expression.IsT0
            && x.Expression.Match(cmd =>
            {
                _cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
                return _cmdComparer.Equals(cmd, y.Expression.AsT0);
            }, e =>
            {
                _expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
                return _expComparer.Equals(e, y.Expression.AsT1);
            });
    }

    public int GetHashCode([DisallowNull] SelectExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.Index);

            hash.Add(obj.PropertyType);

            hash.Add(obj.PropertyName);

            obj.Expression.Switch(cmd =>
            {
                _cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
                hash.Add(cmd, _cmdComparer);
            }, exp =>
            {
                _expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
                hash.Add(exp, _expComparer);
            });

            return hash.ToHashCode();
        }

    }
}