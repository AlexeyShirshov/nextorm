namespace NextORM.Core;

/// <summary>
/// Holds cached query plans, including the shared <c>Any</c> plan owned by the context.
/// </summary>
public interface IQueryCache
{
    Lazy<QueryCommand<bool>>? AnyCommand { get; set; }
    void PurgeQueryCache();
}
