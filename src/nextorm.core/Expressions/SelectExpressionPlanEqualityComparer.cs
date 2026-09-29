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

        if (x.PhysicalColumnName != y.PhysicalColumnName) return false;

        if (x.IsDynamicColumnsStore != y.IsDynamicColumnsStore) return false;

        if (x.DefaultOnNull != y.DefaultOnNull) return false;

        if (x.DurationUnit != y.DurationUnit) return false;

        if (x.ProviderType != y.ProviderType) return false;

        // The projection-item grouping decides how the flattened columns are rebuilt into an entity
        // (and into null on the missing side of an outer join), so it must be part of the plan identity
        // exactly as it is part of the row-mapper signature. The group is compared by shape (entity type
        // and slot), not by reference: two separately built but identical projections have distinct
        // group instances and must still share a cached plan.
        if (x.ProjectionItem is null != (y.ProjectionItem is null)) return false;
        if (x.ProjectionItem is { } xItem && y.ProjectionItem is { } yItem)
        {
            if (xItem.EntityType != yItem.EntityType) return false;
            if (xItem.Slot != yItem.Slot) return false;
        }

        if (!ReferenceEquals(x.Converter, y.Converter)) return false;

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

            hash.Add(obj.PhysicalColumnName);

            hash.Add(obj.IsDynamicColumnsStore);

            hash.Add(obj.DefaultOnNull);

            hash.Add(obj.DurationUnit);

            hash.Add(obj.ProviderType);

            hash.Add(obj.ProjectionItem is { } item ? item.EntityType.GetHashCode() * 31 + item.Slot : 0);

            if (obj.Converter is not null)
                hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Converter));

            //_expComparer ??= new ExpressionPlanEqualityComparer(_cache, _queryProvider);
            hash.Add(obj.Expression, _queryProvider.GetExpressionPlanEqualityComparer());

            return hash.ToHashCode();
        }
    }
}