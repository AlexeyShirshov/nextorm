namespace NextORM.Core;

/// <summary>
/// Builds and resets the execution plan behind a query command.
/// </summary>
public interface IQueryPlanner
{
    /// <summary>
    /// Compiles a query command into an executable prepared command, optionally caching the plan for reuse.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="queryCommand">The query command to compile.</param>
    /// <param name="createEnumerator">Whether an enumerator over the rows should be created as part of preparation.</param>
    /// <param name="storeInCache">Whether the resulting plan should be stored in the plan cache.</param>
    /// <param name="cancellationToken">Cancels the compilation of the query.</param>
    /// <returns>The compiled, executable prepared command.</returns>
    IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken);
    /// <summary>
    /// Clears the execution plan built for <paramref name="queryCommand"/>, so it is compiled again on next use.
    /// </summary>
    /// <param name="queryCommand">The query command whose preparation should be reset.</param>
    void ResetPreparation(QueryCommand queryCommand);
    /// <summary>
    /// Resolves the <c>From</c> source expression for a CLR type, reusing the plan of
    /// <paramref name="queryCommand"/> when one is supplied.
    /// </summary>
    /// <param name="srcType">The CLR type to resolve the source for.</param>
    /// <param name="queryCommand">An existing command whose plan should supply the source, or <see langword="null"/>.</param>
    /// <returns>The resolved source expression, or <see langword="null"/> when the type has no mapping.</returns>
    FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand);
}
