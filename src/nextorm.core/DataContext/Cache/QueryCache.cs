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

    internal QueryCache(Action purge)
    {
        ArgumentNullException.ThrowIfNull(purge);
        _purge = purge;
    }

    public Lazy<QueryCommand<bool>>? AnyCommand { get; set; }

    public void PurgeQueryCache() => _purge();
}
