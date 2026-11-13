using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <content>Provider-independent nearest-neighbor query entry point.</content>
public partial class EntityBuilder<TEntity>
{
    private static readonly MethodInfo VectorDistanceMethod =
        typeof(SqlFunctions).GetMethod(nameof(SqlFunctions.VectorDistance))!;

    /// <summary>
    /// Searches this entity query for its <paramref name="topK"/> nearest rows, ordered by the
    /// selected vector-distance metric.
    /// </summary>
    /// <typeparam name="TVector">The vector property type.</typeparam>
    /// <param name="vectorSelector">Selects the vector property on each entity.</param>
    /// <param name="queryVector">The vector to search near.</param>
    /// <param name="metric">The distance metric. Lower distances are nearer.</param>
    /// <param name="topK">The positive maximum number of neighbors to return.</param>
    /// <param name="mode">The requested exact or approximate search mode.</param>
    /// <returns>A query of entities paired with their computed distance.</returns>
    /// <remarks>
    /// The source query's filtering and source modifiers are preserved, while its prior ordering and
    /// paging are replaced by distance ascending and <paramref name="topK"/>. PostgreSQL emits the same
    /// distance-operator query for <see cref="VectorSearchMode.Exact"/> and
    /// <see cref="VectorSearchMode.Approximate"/>; its planner may use HNSW or IVFFlat transparently.
    /// The mode does not rewrite the query into a different metric or result semantics. Other providers
    /// reject vector search during preparation unless their dialect advertises support.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="vectorSelector"/> or <paramref name="queryVector"/> is null.</exception>
    /// <exception cref="ArgumentException">The query vector has no dimensions, or an enum value is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is not positive.</exception>
    public QueryCommand<VectorSearchResult<TEntity>> VectorSearch<TVector>(
        Expression<Func<TEntity, TVector>> vectorSelector,
        TVector queryVector,
        VectorDistanceMetric metric,
        int topK,
        VectorSearchMode mode = VectorSearchMode.Exact)
        where TVector : IVectorValue
    {
        if (DataProvider is InMemoryDataContext)
            throw new NotSupportedException(
                "Vector distance and nearest-neighbor search are not supported by the InMemoryDialect provider.");

        ArgumentNullException.ThrowIfNull(vectorSelector);
        ArgumentNullException.ThrowIfNull(queryVector);
        if (queryVector.Dimensions <= 0)
            throw new ArgumentException("The query vector must have at least one dimension.", nameof(queryVector));
        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "The nearest-neighbor top-k value must be positive.");
        if (metric is not (VectorDistanceMetric.Cosine or VectorDistanceMetric.Euclidean or VectorDistanceMetric.DotProduct))
            throw new ArgumentException("The vector distance metric is not supported by the unified vector-search API.", nameof(metric));
        if (mode is not (VectorSearchMode.Exact or VectorSearchMode.Approximate))
            throw new ArgumentException("The vector search mode must be Exact or Approximate.", nameof(mode));

        var distance = Expression.Call(
            VectorDistanceMethod.MakeGenericMethod(typeof(TVector)),
            vectorSelector.Body,
            Expression.Constant(queryVector, typeof(TVector)),
            Expression.Constant(metric));
        var parameter = vectorSelector.Parameters[0];
        var orderSelector = Expression.Lambda<Func<TEntity, object?>>(
            Expression.Convert(distance, typeof(object)), parameter);

        var resultType = typeof(VectorSearchResult<TEntity>);
        var resultExpression = Expression.MemberInit(
            Expression.New(resultType),
            Expression.Bind(resultType.GetProperty(nameof(VectorSearchResult<TEntity>.Entity))!, parameter),
            Expression.Bind(resultType.GetProperty(nameof(VectorSearchResult<TEntity>.Distance))!, distance));
        var resultSelector = Expression.Lambda<Func<TEntity, VectorSearchResult<TEntity>>>(resultExpression, parameter);

        // k-NN has its own primary order and page; an earlier user sort/offset must not change which
        // rows are the nearest top-k. Filters and other source shape remain intact.
        var search = Clone();
        search._sorting = null;
        search.Paging.Limit = topK;
        search.Paging.Offset = 0;
        _ = mode; // A future dialect can lower Approximate through this same public entry point.
        return search.OrderBy(orderSelector).Select(resultSelector);
    }
}
