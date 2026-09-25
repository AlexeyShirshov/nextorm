namespace NextORM.Core;

/// <summary>
/// The portable bulk-insert path: slices the row stream into batches (a single batch by default) and
/// executes each as an ordinary parameterised <c>INSERT ... VALUES</c> through
/// <see cref="IMutationExecutor"/>. Used when the provider has no native bulk API, or when the request
/// needs <c>RETURNING</c>/<c>OUTPUT</c>, conflict handling or identity-insert forms that the native API
/// cannot express.
/// </summary>
internal sealed class PortableBulkInsertExecutor(IMutationExecutor mutation) : IBulkInsertExecutor
{
    private const int EstimatedCharactersPerParameter = 12;
    private const int EstimatedCharactersPerRowOverhead = 8;

    /// <inheritdoc/>
    public int BulkInsert(BulkInsertCommand command)
    {
        EnsureTimeoutSupported(command);

        var rows = command.SyncRows
            ?? throw new InvalidOperationException("BulkInsert is synchronous but the source is async; use BulkInsertAsync instead.");

        var rowsPerBatch = RowsPerBatch(command);
        var total = 0;
        var batch = NewBatch(rowsPerBatch);

        foreach (var row in rows)
        {
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
            {
                total += mutation.Execute(BuildBatch(command, batch));
                command.Progress?.Invoke(total);
                batch = NewBatch(rowsPerBatch);
            }
        }

        if (batch.Count > 0)
        {
            total += mutation.Execute(BuildBatch(command, batch));
            command.Progress?.Invoke(total);
        }

        return total;
    }

    /// <inheritdoc/>
    public Task<int> BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        EnsureTimeoutSupported(command);

        return command.AsyncRows is not null
            ? BulkInsertAsyncRowsAsync(command, command.AsyncRows, cancellationToken)
            : BulkInsertSyncRowsAsync(command, command.SyncRows!, cancellationToken);
    }

    private async Task<int> BulkInsertAsyncRowsAsync(BulkInsertCommand command, IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken)
    {
        var rowsPerBatch = RowsPerBatch(command);
        var total = 0;
        var batch = NewBatch(rowsPerBatch);

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
            {
                total += await mutation.Execute(BuildBatch(command, batch), cancellationToken).ConfigureAwait(false);
                command.Progress?.Invoke(total);
                batch = NewBatch(rowsPerBatch);
            }
        }

        if (batch.Count > 0)
        {
            total += await mutation.Execute(BuildBatch(command, batch), cancellationToken).ConfigureAwait(false);
            command.Progress?.Invoke(total);
        }

        return total;
    }

    private async Task<int> BulkInsertSyncRowsAsync(BulkInsertCommand command, IEnumerable<object?[]> rows, CancellationToken cancellationToken)
    {
        var rowsPerBatch = RowsPerBatch(command);
        var total = 0;
        var batch = NewBatch(rowsPerBatch);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
            {
                total += await mutation.Execute(BuildBatch(command, batch), cancellationToken).ConfigureAwait(false);
                command.Progress?.Invoke(total);
                batch = NewBatch(rowsPerBatch);
            }
        }

        if (batch.Count > 0)
        {
            total += await mutation.Execute(BuildBatch(command, batch), cancellationToken).ConfigureAwait(false);
            command.Progress?.Invoke(total);
        }

        return total;
    }

    internal static void EnsureTimeoutSupported(BulkInsertCommand command)
    {
        if (command.TimeoutSeconds is not null)
            throw new NotSupportedException(
                "A bulk-insert Timeout is only supported on the native bulk path; the portable INSERT ... VALUES path uses the provider's default command timeout.");
    }

    /// <summary>Computes the rows per batch from the command's limits (<see cref="int.MaxValue"/> for unbounded).</summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The maximum rows to write per statement.</returns>
    internal static int RowsPerBatch(BulkInsertCommand command)
    {
        if (command.Batch is null || command.Batch.IsUnbounded)
            return int.MaxValue;

        var columnCount = Math.Max(1, command.Columns.Count);
        var limit = int.MaxValue;

        if (command.Batch.MaxBatchSize is int rows && rows > 0)
            limit = Math.Min(limit, rows);

        if (command.Batch.MaxParameters is int parameters && parameters > 0)
            limit = Math.Min(limit, Math.Max(1, parameters / columnCount));

        if (command.Batch.MaxSqlLength is int maxLength and > 0)
        {
            var perRow = (columnCount * EstimatedCharactersPerParameter) + EstimatedCharactersPerRowOverhead;
            limit = Math.Min(limit, Math.Max(1, maxLength / perRow));
        }

        return limit;
    }

    /// <summary>Creates a batch buffer sized for <paramref name="rowsPerBatch"/>.</summary>
    /// <param name="rowsPerBatch">The rows per batch, or <see cref="int.MaxValue"/> for unbounded.</param>
    /// <returns>A new batch buffer.</returns>
    internal static List<object?[]> NewBatch(int rowsPerBatch)
        => new(rowsPerBatch == int.MaxValue ? 4 : rowsPerBatch);

    /// <summary>Builds the portable <c>INSERT ... VALUES</c> command for one batch.</summary>
    /// <param name="command">The bulk-insert command the batch belongs to.</param>
    /// <param name="batch">The rows of this batch.</param>
    /// <param name="returningColumns">The columns to return through <c>RETURNING</c>/<c>OUTPUT</c>, or <see langword="null"/>.</param>
    /// <returns>The insert command for the batch.</returns>
    internal static InsertCommand BuildBatch(BulkInsertCommand command, List<object?[]> batch, IReadOnlyList<IPropertyMetadata>? returningColumns = null)
    {
        var columns = new InsertColumn[command.Columns.Count];

        for (var c = 0; c < command.Columns.Count; c++)
        {
            var values = new InsertValue[batch.Count];
            for (var r = 0; r < batch.Count; r++)
                values[r] = InsertValue.FromConstant(batch[r][c]);

            columns[c] = new InsertColumn(command.Columns[c], values);
        }

        return new InsertCommand(
            command.EntityType,
            command.TableName,
            command.IsTableNameAuto,
            columns,
            batch.Count,
            identityColumn: null,
            returningColumns: returningColumns,
            source: null,
            sourceColumns: null,
            ignoreConflicts: command.IgnoreConflicts,
            keepIdentity: command.KeepIdentity,
            tableSchema: command.TableSchema);
    }
}
