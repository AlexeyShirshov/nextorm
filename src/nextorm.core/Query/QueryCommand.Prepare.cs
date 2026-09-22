namespace NextORM.Core;

public partial class QueryCommand
{
    /// <summary>
    /// Builds the prepared pieces (from/joins/columns/where/grouping/sorting, CTEs, set operations) and
    /// the plan-cache hashes for this command. The pipeline itself lives in
    /// <see cref="QueryPreparer"/>; <see cref="QueryCommand{TResult}"/> extends it with the result hash.
    /// </summary>
    public void PrepareCommand(CancellationToken cancellationToken) => PrepareCommand(false, cancellationToken);
    /// <summary>
    /// Builds the prepared pieces and plan-cache hashes for this command, optionally skipping the
    /// hash computation when the hash is already known.
    /// </summary>
    /// <param name="dontCalculateHash">Whether to skip recomputing the plan hashes.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    public virtual void PrepareCommand(bool dontCalculateHash, CancellationToken cancellationToken)
        => QueryPreparer.Prepare(this, dontCalculateHash, cancellationToken);
}
