using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Shared result-set navigation for commands that return more than one set. Moved verbatim out of
/// <see cref="BatchRunner"/> so the batch reader and the raw-command reader advance identically.
/// </summary>
internal static class ResultSetNavigator
{
    // The result-bearing statement may be preceded by side-effecting statements (DDL/DML) that surface
    // as result sets without columns. Stopping on the first set that has columns deliberately avoids
    // calling NextResult on an already-last set: Microsoft.Data.Sqlite empties the current result set
    // when NextResult returns false.
    internal static void AdvanceToResultSet(DbDataReader reader)
    {
        while (reader.FieldCount == 0)
        {
            if (!reader.NextResult())
                break;
        }
    }

    /// <summary>Asynchronously advances to the first result set that has columns.</summary>
    /// <param name="reader">The reader to advance.</param>
    /// <param name="cancellationToken">Cancels a slow provider call.</param>
    /// <returns>A task that completes once the reader is positioned on a result set (or has run out of them).</returns>
    internal static async Task AdvanceToResultSetAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        while (reader.FieldCount == 0)
        {
            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                break;
        }
    }

    /// <summary>
    /// Moves to the next result set that has columns, skipping any zero-column set a side-effecting
    /// statement produced. The single place <c>NextResult</c> is driven for multi-set batches.
    /// </summary>
    /// <param name="reader">The reader to advance.</param>
    /// <returns><see langword="true"/> when the reader sits on a set with columns; <see langword="false"/> when no further set exists.</returns>
    internal static bool MoveToNextResultSet(DbDataReader reader)
    {
        if (!reader.NextResult())
            return false;

        AdvanceToResultSet(reader);
        return reader.FieldCount > 0;
    }

    /// <summary>
    /// Asynchronously moves to the next result set that has columns, skipping any zero-column set a
    /// side-effecting statement produced.
    /// </summary>
    /// <param name="reader">The reader to advance.</param>
    /// <param name="cancellationToken">Cancels a slow provider call.</param>
    /// <returns>A task producing <see langword="true"/> when the reader sits on a set with columns; <see langword="false"/> when no further set exists.</returns>
    internal static async Task<bool> MoveToNextResultSetAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            return false;

        await AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);
        return reader.FieldCount > 0;
    }
}
