using System.Diagnostics.CodeAnalysis;
namespace nextorm.core;

public sealed class FromExpressionPlanEqualityComparer : IEqualityComparer<FromExpression?>
{
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    // private readonly IQueryProvider _queryProvider;
    private readonly Lazy<QueryPlanEqualityComparer> _equalityComparer;

    //private readonly ILogger? _logger;
    //private readonly ExpressionPlanEqualityComparer _expComparer;
    //private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public FromExpressionPlanEqualityComparer(IQueryProvider queryProvider)
    //        : this(cache, queryProvider, null)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _equalityComparer = new Lazy<QueryPlanEqualityComparer>(queryProvider.GetQueryPlanEqualityComparer);
    }
    // public FromExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider, ILogger? logger)
    // {
    //     _cache = cache ?? new ExpressionCache<Delegate>();
    //     _queryProvider = queryProvider;
    //     _logger = logger;
    //     //_expComparer = new ExpressionPlanEqualityComparer(cache);        
    // }
    // private FromExpressionPlanEqualityComparer() { }
    // public static FromExpressionPlanEqualityComparer Instance => new();
    public bool Equals(FromExpression? x, FromExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        // if (x.TableAlias != y.TableAlias) return false;
        if (!string.IsNullOrEmpty(x.Table) && x.Table == y.Table) return true;

        return _equalityComparer.Value.Equals(x.SubQuery, y.SubQuery);
    }

    public int GetHashCode(FromExpression? obj)
    {
        if (obj is null) return 0;

        /*if (obj.TableAlias is not null)
        {
            unchecked
            {
                var hash = new HashCode();
                hash.Add(obj.TableAlias);

                if (!string.IsNullOrEmpty(obj.Table))
                    hash.Add(obj.Table);
                else
                    hash.Add(obj.SubQuery, _equalityComparer.Value);

                return hash.ToHashCode();
            }
        }
        else */
        if (!string.IsNullOrEmpty(obj.Table))
            return obj.Table.GetHashCode();
        else
            return _equalityComparer.Value.GetHashCode(obj.SubQuery!);
    }
}