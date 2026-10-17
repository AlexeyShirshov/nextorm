namespace NextORM.Core;

/// <summary>
/// Implementation of <see cref="IQueryCache"/> shared by the SQL contexts and
/// <see cref="InMemoryDataContext"/>. Holds the per-context <c>Any</c> plan slot and routes purging to the
/// provider-specific store: the SQL contexts clear the thread-local <see cref="QueryPlanStore"/>, the
/// in-memory context its per-instance plan dictionary.
/// </summary>
internal sealed class QueryCache : IQueryCache
{
    private readonly Action _purge;
    // Context-owned immutable scalar artifacts (T243/D243 fast path). Bounded and FIFO; it holds only
    // normalized terminal keys, never a builder, an executable command, a closure, an argument or a result.
    private readonly ScalarArtifactCache _scalarArtifacts = new();

    internal QueryCache(Action purge)
    {
        ArgumentNullException.ThrowIfNull(purge);
        _purge = purge;
    }

    public Lazy<QueryCommand<bool>>? AnyCommand { get; set; }

    /// <summary>Returns the retained scalar artifact for <paramref name="key"/>, or <see langword="null"/>.</summary>
    /// <param name="key">The normalized terminal key.</param>
    /// <returns>The artifact, or <see langword="null"/> on a miss.</returns>
    internal ScalarArtifact? FindScalarArtifact(in ScalarArtifactKey key) => _scalarArtifacts.Find(key);

    /// <summary>Returns the retained scalar artifact for <paramref name="key"/>, creating it on a miss.</summary>
    /// <param name="key">The normalized terminal key.</param>
    /// <returns>The existing or newly created artifact.</returns>
    internal ScalarArtifact GetOrAddScalarArtifact(in ScalarArtifactKey key) => _scalarArtifacts.GetOrAdd(key);

    /// <summary>Drops every retained scalar artifact, releasing the plan keys it referenced.</summary>
    internal void ClearScalarArtifacts() => _scalarArtifacts.Clear();

    public void PurgeQueryCache()
    {
        // The artifacts reference plan keys of the store this purge clears, so both must go together:
        // a retained key would otherwise be looked up against an empty store (a miss, never a stale hit).
        _scalarArtifacts.Clear();
        _purge();
    }
}
