using System.Diagnostics.CodeAnalysis;
namespace NextORM.Core;

/// <summary>
/// Compares and hashes query sources for plan-cache purposes: table names by value, and subqueries,
/// table functions, pivots and xml row-sets structurally.
/// </summary>
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
    /// <summary>Initializes a comparer.</summary>
    /// <param name="queryProvider">The registry that supplies the nested plan comparers.</param>
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
    /// <summary>Determines whether two query sources are equal for plan-cache purposes.</summary>
    /// <param name="x">The first source.</param>
    /// <param name="y">The second source.</param>
    /// <returns><c>true</c> when both sources resolve to the same underlying source.</returns>
    public bool Equals(FromExpression? x, FromExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        // if (x.TableAlias != y.TableAlias) return false;
        if (!string.Equals(x.SubQueryHint, y.SubQueryHint, StringComparison.Ordinal)) return false;

        if (!string.IsNullOrEmpty(x.Table) && x.Table == y.Table) return true;

        // A SelectMany/GroupJoin source is a computed node whose selectors are delegates over the
        // outer row; compare by identity so an unrelated node never shares a cached plan. (The
        // compiled selectors have their own closure-aware cache in the in-memory provider.)
        if (x.LinqSource is not null || y.LinqSource is not null)
            return ReferenceEquals(x.LinqSource, y.LinqSource);

        if (x.TableFunction is not null || y.TableFunction is not null)
        {
            if (x.TableFunction is null || y.TableFunction is null) return false;

            // The declared result schema is rendered into the SQL, so its row type and placement must
            // be part of the key: two calls that differ only in that schema must not share a plan.
            if (x.TableFunction.ResultType != y.TableFunction.ResultType) return false;
            if (x.TableFunction.ResultSchema != y.TableFunction.ResultSchema) return false;

            return _expComparer.Equals(x.TableFunction.Call, y.TableFunction.Call);
        }

        if (x.Pivot is not null || y.Pivot is not null)
            return PivotEquals(x.Pivot, y.Pivot);

        if (x.XmlNodes is not null || y.XmlNodes is not null)
        {
            if (x.XmlNodes is null || y.XmlNodes is null) return false;
            return x.XmlNodes.XPath == y.XmlNodes.XPath
                && _expComparer.Equals(x.XmlNodes.Operand, y.XmlNodes.Operand);
        }

        return _equalityComparer.Value.Equals(x.SubQuery, y.SubQuery);
    }

    private bool PivotEquals(PivotExpression? x, PivotExpression? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.IsUnpivot != y.IsUnpivot) return false;
        if (!Equals(x.Inner, y.Inner)) return false;

        if (x.IsUnpivot)
            return x.UnpivotValueColumn == y.UnpivotValueColumn
                && x.UnpivotNameColumn == y.UnpivotNameColumn
                && ColumnsEqual(x.Columns, y.Columns);

        return x.Aggregate == y.Aggregate
            && _expComparer.Equals(x.AggregateColumn, y.AggregateColumn)
            && _expComparer.Equals(x.ForColumn, y.ForColumn)
            && ValuesEqual(x.Values, y.Values);
    }

    private static bool ColumnsEqual(IReadOnlyList<UnpivotColumn> x, IReadOnlyList<UnpivotColumn> y)
    {
        if (x.Count != y.Count) return false;
        for (var i = 0; i < x.Count; i++)
            if (x[i].Column != y[i].Column) return false;
        return true;
    }

    private static bool ValuesEqual(IReadOnlyList<PivotValue> x, IReadOnlyList<PivotValue> y)
    {
        if (x.Count != y.Count) return false;
        for (var i = 0; i < x.Count; i++)
            if (x[i].Value != y[i].Value) return false;
        return true;
    }

    private int PivotHash(PivotExpression pivot)
    {
        var hash = new System.HashCode();
        hash.Add(pivot.IsUnpivot);
        hash.Add(GetHashCode(pivot.Inner));

        if (pivot.IsUnpivot)
        {
            hash.Add(pivot.UnpivotValueColumn);
            hash.Add(pivot.UnpivotNameColumn);
            foreach (var column in pivot.Columns)
                hash.Add(column.Column);
        }
        else
        {
            hash.Add(pivot.Aggregate);
            hash.Add(pivot.AggregateColumn is null ? 0 : _expComparer.GetHashCode(pivot.AggregateColumn));
            hash.Add(pivot.ForColumn is null ? 0 : _expComparer.GetHashCode(pivot.ForColumn));
            foreach (var value in pivot.Values)
                hash.Add(value.Value);
        }

        return hash.ToHashCode();
    }

    /// <summary>Returns a hash code for a query source consistent with the equality comparison.</summary>
    /// <param name="obj">The source to hash.</param>
    /// <returns>The hash code, or zero when <paramref name="obj"/> is <c>null</c>.</returns>
    public int GetHashCode(FromExpression? obj)
    {
        if (obj is null) return 0;

        if (obj.LinqSource is not null)
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.LinqSource);

        if (obj.TableFunction is not null)
        {
            var tableFunctionHash = new System.HashCode();
            tableFunctionHash.Add(obj.TableFunction.ResultType is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.TableFunction.ResultType));
            tableFunctionHash.Add((int)obj.TableFunction.ResultSchema);
            tableFunctionHash.Add(_expComparer.GetHashCode(obj.TableFunction.Call));
            return tableFunctionHash.ToHashCode();
        }

        if (obj.Pivot is not null)
            return PivotHash(obj.Pivot);

        if (obj.XmlNodes is not null)
        {
            var nodesHash = new System.HashCode();
            nodesHash.Add(obj.XmlNodes.XPath);
            nodesHash.Add(_expComparer.GetHashCode(obj.XmlNodes.Operand));
            return nodesHash.ToHashCode();
        }

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
        {
            var subQueryHash = _equalityComparer.Value.GetHashCode(obj.SubQuery!);

            if (obj.SubQueryHint is null)
                return subQueryHash;

            var hintHash = new System.HashCode();
            hintHash.Add(subQueryHash);
            hintHash.Add(obj.SubQueryHint);
            return hintHash.ToHashCode();
        }
    }
}