namespace NextORM.Core;

/// <summary>
/// Runs queries and exposes the context services used to prepare, execute, and materialize them.
/// The interface combines smaller roles so consumers can use one entry point:
/// <list type="bullet">
/// <item><see cref="IQueryExecutor"/> — query execution;</item>
/// <item><see cref="IQueryMaterializer"/> — query preparation and row materialization;</item>
/// <item><see cref="IQueryCache"/> — prepared-query caching;</item>
/// <item><see cref="IContextEnvironment"/> — logging and mapping settings;</item>
/// <item><see cref="IRawCommandExecutor"/> — parameterized raw commands.</item>
/// </list>
/// Connection management is separate (see <see cref="IConnectionManager"/>), because an in-memory
/// context has no connection to manage.
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
    /// Prepares <paramref name="queryCommand"/> for execution using the selected command-reuse policy.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to prepare.</param>
    /// <param name="createEnumerator"><see langword="true"/> to create the row enumerator during preparation; otherwise, defer it until execution.</param>
    /// <param name="storeInCache"><see langword="true"/> to allow the prepared plan to be stored in the plan cache; otherwise, do not cache it.</param>
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
