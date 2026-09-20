using System.Diagnostics.CodeAnalysis;
namespace NextORM.Core;

public sealed class FromExpressionPlanEqualityComparer : IEqualityComparer<FromExpression?>
{
    //private readonly IDictionary<ExpressionKey, Delegate> _cache;
    // private readonly IQueryRegistry _queryProvider;
    private readonly Lazy<QueryPlanEqualityComparer> _equalityComparer;
    // Table-function calls are compared/hashed structurally, with the same closure-aware comparer the
    // WHERE/SELECT expressions use, so two equivalent calls built from different closure instances
    // still share a cached plan while different arguments produce different plans.
    private readonly ExpressionPlanEqualityComparer _expComparer;

    //private readonly ILogger? _logger;
    //private readonly ExpressionPlanEqualityComparer _expComparer;
    //private QueryPlanEqualityComparer? _cmdComparer;

    // public PreciseExpressionEqualityComparer()
    //     : this(new ExpressionCache<Delegate>())
    // {
    // }
    public FromExpressionPlanEqualityComparer(IQueryRegistry queryProvider)
    //        : this(cache, queryProvider, null)
    {
        //_cache = cache ?? new ExpressionCache<Delegate>();
        _equalityComparer = new Lazy<QueryPlanEqualityComparer>(queryProvider.GetQueryPlanEqualityComparer);
        _expComparer = queryProvider.GetExpressionPlanEqualityComparer();
    }
    // public FromExpressionPlanEqualityComparer(IDictionary<ExpressionKey, Delegate>? cache, IQueryRegistry queryProvider, ILogger? logger)
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

        // A SelectMany/GroupJoin source is a computed node whose selectors are delegates over the
        // outer row; compare by identity so an unrelated node never shares a cached plan. (The
        // compiled selectors have their own closure-aware cache in the in-memory provider.)
        if (x.LinqSource is not null || y.LinqSource is not null)
            return ReferenceEquals(x.LinqSource, y.LinqSource);

        if (x.TableFunction is not null || y.TableFunction is not null)
            return _expComparer.Equals(x.TableFunction?.Call, y.TableFunction?.Call);

        return _equalityComparer.Value.Equals(x.SubQuery, y.SubQuery);
    }

    public int GetHashCode(FromExpression? obj)
    {
        if (obj is null) return 0;

        if (obj.LinqSource is not null)
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.LinqSource);

        if (obj.TableFunction is not null)
            return _expComparer.GetHashCode(obj.TableFunction.Call);

        /*if (obj.TableAlias is not null)
        {
            unchecked
            {
                var hash = new XxHash32();
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