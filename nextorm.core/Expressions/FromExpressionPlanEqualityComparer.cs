using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
namespace nextorm.core;

public sealed class FromExpressionPlanEqualityComparer : IEqualityComparer<FromExpression>
{
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    private readonly IQueryProvider _queryProvider;
    //private readonly ILogger? _logger;
    //private readonly ExpressionPlanEqualityComparer _expComparer;
    //private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public FromExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryProvider queryProvider)
    //        : this(cache, queryProvider, null)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _queryProvider = queryProvider;
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

        return x.TableAlias == y.TableAlias && x.Table.IsT0 == y.Table.IsT0 && x.Table.Match(
                tbl => tbl == y.Table.AsT0,
                cmd =>
                {
                    //_cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
                    return _queryProvider.GetQueryPlanEqualityComparer().Equals(cmd, y.Table.AsT1);
                });
    }

    public int GetHashCode([DisallowNull] FromExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.TableAlias);

            obj.Table.Switch(
                 tbl => hash.Add(tbl),
                 cmd =>
                 {
                     //_cmdComparer ??= new QueryPlanEqualityComparer(_cache, _queryProvider);
                     hash.Add(cmd, _queryProvider.GetQueryPlanEqualityComparer());
                 }
            );

            return hash.ToHashCode();
        }
    }
}