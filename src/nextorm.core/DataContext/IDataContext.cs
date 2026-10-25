namespace NextORM.Core;

/// <summary>
/// Composite facade over the individual context roles, kept as the convenient entry point for
/// consumers. The roles are separate interfaces so that providers and collaborators can depend
/// on the smallest surface they actually need:
/// <list type="bullet">
/// <item><see cref="IQueryExecutor"/> — terminal operators;</item>
/// <item><see cref="IQueryMaterializer"/> — planning + row reading;</item>
/// <item><see cref="IQueryCache"/> — plan cache;</item>
/// <item><see cref="IContextEnvironment"/> — loggers, mapping mode, property bag;</item>
/// <item><see cref="IRawCommandExecutor"/> — raw parameterised commands.</item>
/// </list>
/// Connection management is deliberately <b>not</b> part of this composite
/// (see <see cref="IConnectionManager"/>): the in-memory provider must not be forced to
/// implement a no-op connection lifecycle.
/// </summary>
public interface IDataContext :
    IQueryExecutor,
    IQueryMaterializer,
    IQueryCache,
    IContextEnvironment,
    IRawCommandExecutor,
    IAsyncDisposable,
    IDisposable
{
    /// <summary>
    /// Prepares <paramref name="queryCommand"/> with an explicit command-reuse policy, optionally creating
    /// the row enumerator and storing the plan in the cache.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to prepare.</param>
    /// <param name="createEnumerator">When <see langword="true"/>, creates the row enumerator as part of preparation.</param>
    /// <param name="storeInCache">When <see langword="true"/>, the resulting plan may be stored in the plan cache.</param>
    /// <param name="cache">The policy that selects connection-scoped command reuse or a command per execution.</param>
    /// <param name="cancellationToken">Token used to cancel preparation.</param>
    /// <returns>The prepared command, ready to execute.</returns>
    /// <remarks>
    /// The default implementation supports only <see cref="PreparedCommandCache.Connection"/> and forwards to the
    /// policy-less overload. A context that does not implement the policy fails closed for any other value instead
    /// of silently substituting the connection policy.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The context implementation does not implement the requested <paramref name="cache"/> policy.
    /// </exception>
    IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(
        QueryCommand<TResult> queryCommand,
        bool createEnumerator,
        bool storeInCache,
        PreparedCommandCache cache,
        CancellationToken cancellationToken)
        => cache == PreparedCommandCache.Connection
            ? GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cancellationToken)
            : throw new NotSupportedException(
                "This context implementation does not support per-connection prepared-command caching, so it cannot honour PrepareOptions.Cache = PreparedCommandCache.None; prepare with PreparedCommandCache.Connection or use a context that implements the requested policy.");
}
