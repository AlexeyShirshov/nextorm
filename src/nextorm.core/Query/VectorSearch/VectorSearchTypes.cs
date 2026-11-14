namespace NextORM.Core;

/// <summary>The distance metric used by a nearest-neighbor search.</summary>
public enum VectorDistanceMetric
{
    /// <summary>Cosine distance; lower values represent closer vectors.</summary>
    Cosine = 0,
    /// <summary>Euclidean (L2) distance; lower values represent closer vectors.</summary>
    Euclidean = 1,
    /// <summary>Negative inner product; lower values represent closer vectors.</summary>
    DotProduct = 2,
}

/// <summary>Whether a nearest-neighbor search requests exact or approximate results.</summary>
public enum VectorSearchMode
{
    /// <summary>Request exact nearest-neighbor semantics.</summary>
    /// <remarks>
    /// PostgreSQL uses the pgvector distance-function form, which is not eligible for HNSW or IVFFlat
    /// distance-operator indexes and may be slower. SQL Server uses <c>VECTOR_DISTANCE</c>.
    /// </remarks>
    Exact = 0,
    /// <summary>Request approximate nearest-neighbor search where the provider supports it.</summary>
    /// <remarks>
    /// PostgreSQL uses pgvector distance operators, making the query eligible for a matching HNSW or
    /// IVFFlat index; the planner still decides whether to use one. SQL Server uses the explicitly
    /// selected <c>VECTOR_SEARCH</c> grammar. The mode does not silently change the metric, ordering or
    /// result shape.
    /// </remarks>
    Approximate = 1,
}

/// <summary>A vector-search row containing the entity and its distance from the query vector.</summary>
/// <typeparam name="TEntity">The entity returned for the neighbor.</typeparam>
public sealed class VectorSearchResult<TEntity>
{
    /// <summary>Creates a result from the matching entity and its distance.</summary>
    /// <param name="entity">The matching entity.</param>
    /// <param name="distance">The metric distance; lower values are nearer.</param>
    public VectorSearchResult(TEntity entity, double? distance)
    {
        Entity = entity;
        Distance = distance;
    }

    /// <summary>The matching entity.</summary>
    public TEntity Entity { get; }

    /// <summary>The metric distance; lower values are nearer.</summary>
    public double? Distance { get; }
}
