namespace nextorm.core;

/// <summary>
/// Builds and resets the execution plan behind a query command.
/// </summary>
public interface IQueryPlanner
{
    IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken);
    void ResetPreparation(QueryCommand queryCommand);
    FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand);
}
