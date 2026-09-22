namespace NextORM.Core;

/// <summary>
/// Holds cached query plans, including the shared <c>Any</c> plan owned by the context.
/// </summary>
public interface IQueryCache
{
    /// <summary>
    /// The lazily built plan for <c>Any</c>, shared across calls on the context so a bare existence
    /// check does not rebuild its plan; <see langword="null"/> until first requested.
    /// </summary>
    Lazy<QueryCommand<bool>>? AnyCommand { get; set; }
    /// <summary>
    /// Removes all cached plans and the shared <c>Any</c> plan, forcing them to be rebuilt on next use.
    /// </summary>
    void PurgeQueryCache();
}
