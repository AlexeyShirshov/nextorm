using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>Standalone SQL Server MERGE capture terminals.</summary>
public static class SqlServerMergeOutputExtensions
{
    /// <summary>
    /// Captures <c>$action</c> in the target's <c>action</c> column, followed by the projected
    /// <c>inserted</c> columns under their mapped names, without returning rows to the client.
    /// </summary>
    /// <typeparam name="TEntity">The merged entity type.</typeparam>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="merge">The MERGE returning builder.</param>
    /// <param name="targetTable">The existing capture table's bare name.</param>
    /// <returns>A terminal for executing the capture.</returns>
    public static OutputIntoBuilder OutputInto<TEntity, TResult>(
        this MergeReturningBuilder<TEntity, TResult> merge, string targetTable)
    {
        ArgumentNullException.ThrowIfNull(merge);
        return merge.CreateExistingTableOutput(targetTable);
    }

    /// <summary>
    /// Captures the same rows as <see cref="OutputInto{TEntity,TResult}"/> and also returns their
    /// projected <c>inserted</c> values, without <c>$action</c> or pre-existing capture rows.
    /// </summary>
    /// <typeparam name="TEntity">The merged entity type.</typeparam>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="merge">The MERGE returning builder.</param>
    /// <param name="targetTable">The existing capture table's bare name.</param>
    /// <returns>A terminal for executing the capture and reading the projected rows.</returns>
    public static MergeReturningBuilder<TEntity, TResult> OutputIntoThenOutput<TEntity, TResult>(
        this MergeReturningBuilder<TEntity, TResult> merge, string targetTable)
    {
        ArgumentNullException.ThrowIfNull(merge);
        return merge.WithExistingTableOutput(targetTable);
    }
}
