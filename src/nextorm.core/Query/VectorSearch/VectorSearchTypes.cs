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

/// <summary>Whether a nearest-neighbor search requests an exact or approximate search plan.</summary>
public enum VectorSearchMode
{
    /// <summary>Request exact nearest-neighbor semantics.</summary>
    Exact = 0,
    /// <summary>Request an approximate nearest-neighbor search where the provider supports it.</summary>
    /// <remarks>
    /// PostgreSQL renders the same pgvector distance-operator query as <see cref="Exact"/>. PostgreSQL
    /// chooses an HNSW or IVFFlat index transparently when the query and index permit it; the mode does
    /// not silently change the metric, ordering or result semantics.
    /// </remarks>
    Approximate = 1,
}

/// <summary>A vector-search row containing the entity and its distance from the query vector.</summary>
/// <typeparam name="TEntity">The entity returned for the neighbor.</typeparam>
public sealed class VectorSearchResult<TEntity>
{
    /// <summary>Creates an empty result instance.</summary>
    public VectorSearchResult()
    {
    }

    /// <summary>Creates a result from the matching entity and its distance.</summary>
    /// <param name="entity">The matching entity.</param>
    /// <param name="distance">The metric distance; lower values are nearer.</param>
    public VectorSearchResult(TEntity entity, double? distance)
    {
        Entity = entity;
        Distance = distance;
    }

    /// <summary>The matching entity.</summary>
    public TEntity Entity { get; init; } = default!;

    /// <summary>The metric distance; lower values are nearer.</summary>
    public double? Distance { get; init; }
}
