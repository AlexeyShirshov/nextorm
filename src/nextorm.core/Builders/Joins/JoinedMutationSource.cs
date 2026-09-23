namespace NextORM.Core;

/// <summary>
/// Shared preparation for a mutation driven by a joined query (the multi-table <c>DELETE</c> and
/// <c>UPDATE</c> forms). Both restrict the join to INNER joins — folding an outer join into the filter (or
/// its <c>ON</c>) would silently change which rows are affected — and both read only the source, joins and
/// condition of the prepared command, so the projection is a throwaway placeholder.
/// </summary>
internal static class JoinedMutationSource
{
    /// <summary>
    /// Builds the prepared joined command that carries the source, joins and condition of a multi-table
    /// mutation. The projection is an unused placeholder and column preparation is skipped.
    /// </summary>
    /// <typeparam name="TProjection">The joined projection type.</typeparam>
    /// <param name="query">The joined query whose source, joins and condition drive the mutation.</param>
    /// <param name="operation">The statement name (<c>DELETE</c> or <c>UPDATE</c>) used in error messages.</param>
    /// <returns>The prepared placeholder command.</returns>
    /// <exception cref="NotSupportedException">A join is not an INNER join.</exception>
    internal static QueryCommand Prepare<TProjection>(EntityBuilder<TProjection> query, string operation)
    {
        RequireInnerJoins(query.Joins, operation);

        var command = query.Select(p => new { Unit = 1 });
        command.IgnoreColumns = true;
        return command;
    }

    /// <summary>
    /// Rejects any join that is not an INNER join. A multi-table mutation folds the join conditions into
    /// the filter, so an outer join would silently become an inner join.
    /// </summary>
    /// <param name="joins">The joins of the mutation source, or <see langword="null"/> when there are none.</param>
    /// <param name="operation">The statement name (<c>DELETE</c> or <c>UPDATE</c>) used in the message.</param>
    /// <exception cref="NotSupportedException">A join is not an INNER join.</exception>
    internal static void RequireInnerJoins(IReadOnlyList<JoinExpression>? joins, string operation)
    {
        if (joins is null)
            return;

        for (var i = 0; i < joins.Count; i++)
        {
            if (joins[i].JoinType is not JoinType.Inner)
                throw new NotSupportedException(
                    $"A multi-table {operation} only supports INNER joins, not {joins[i].JoinType}. Use a correlated subquery (Where with EXISTS/IN) instead.");
        }
    }
}
