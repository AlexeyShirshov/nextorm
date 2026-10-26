namespace NextORM.Core;

/// <summary>
/// The portable bulk-insert path: slices the row stream into batches and executes each as an ordinary
/// parameterised <c>INSERT ... VALUES</c> through <see cref="IMutationExecutor"/>. Used when the provider
/// has no native bulk API, or when the request needs <c>RETURNING</c>/<c>OUTPUT</c>, conflict handling or
/// identity-insert forms that the native API cannot express.
/// </summary>
/// <remarks>
/// One <see cref="BulkInsertDeadline"/> is started at execution entry and shared by every batch: the
/// budget covers the row source and the writes together and is never reset. Batches are limited by the
/// real parameter count (each row binds one parameter per column) and by the exact rendered SQL length,
/// measured by rendering the candidate batch; a single row that cannot be expressed within
/// <see cref="BulkInsertOptions.MaxSqlLength"/> is refused before it is sent.
/// </remarks>
internal sealed class PortableBulkInsertExecutor(IMutationExecutor mutation) : IBulkInsertExecutor
{
    /// <inheritdoc/>
    public int BulkInsert(BulkInsertCommand command)
    {
        EnsurePortableOptionsSupported(command);

        var rows = command.SyncRows
            ?? throw new InvalidOperationException("BulkInsert is synchronous but the source is async; use BulkInsertAsync instead.");

        var dialect = ResolveDialect(command);
        using var deadline = BulkInsertDeadline.Start(command.Timeout, CancellationToken.None);
        // The provider-scoped session controls and shape guards run on this path too: Mode.Portable must
        // not bypass the contract the native path would apply.
        using var scope = BeginProviderScope(command);

        var rowLimit = RowLimit(command);
        var total = 0;
        var batch = new List<object?[]>();

        foreach (var row in rows)
        {
            deadline.ThrowIfCancelled();

            batch.Add(row);
            if (batch.Count >= rowLimit)
                total = Drain(command, batch, total, deadline, dialect);
        }

        if (batch.Count > 0)
            total = Drain(command, batch, total, deadline, dialect);

        return total;
    }

    /// <inheritdoc/>
    public async Task<int> BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        EnsurePortableOptionsSupported(command);

        var rows = command.AsyncRows ?? new SyncToAsyncEnumerable<object?[]>(command.SyncRows!);
        var dialect = ResolveDialect(command);
        using var deadline = BulkInsertDeadline.Start(command.Timeout, cancellationToken);
        // Mirror of the synchronous entry: the provider-scoped contract is entered before the first row
        // is pulled and restored on disposal, including the error/cancellation paths.
        using var scope = BeginProviderScope(command);

        var rowLimit = RowLimit(command);
        var total = 0;
        var batch = new List<object?[]>();

        try
        {
            await foreach (var row in rows.WithCancellation(deadline.Token).ConfigureAwait(false))
            {
                deadline.ThrowIfCancelled();

                batch.Add(row);
                if (batch.Count >= rowLimit)
                    total = await DrainAsync(command, batch, total, deadline, dialect).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException cancellation)
        {
            throw deadline.Translate(cancellation);
        }

        if (batch.Count > 0)
            total = await DrainAsync(command, batch, total, deadline, dialect).ConfigureAwait(false);

        return total;
    }

    internal static void EnsurePortableOptionsSupported(BulkInsertCommand command)
    {
        // An explicit Native request that reaches the portable executor cannot be honoured: either the
        // provider has no native bulk path (SQLite) or the request needs a portable-only form. Refuse it
        // instead of silently falling back. Auto keeps its documented fallback and is unaffected.
        if (command.Mode == BulkInsertMode.Native)
            throw new NotSupportedException(
                "Bulk insert Mode.Native was requested, but this provider or request has no compatible native bulk path. "
                + "Use Mode.Auto to let nextorm pick a compatible path, or Mode.Portable to request the parameterised INSERT path.");

        // Resolve the destination before any row is pulled: a server qualification and disagreeing schema
        // aliases are refused up front.
        var destination = BulkInsertDestination.From(command.Options);

        // A local temporary target is the caller's table on the context's own connection/session. The
        // portable INSERT addresses it exactly like a regular table on that connection, with no CREATE/DROP
        // and no implicit transaction, so the only request it cannot honour is a qualification that points
        // at another database. That is refused by name here, before the source is read.
        if (command.TargetKind == BulkInsertTargetKind.LocalTemporary && destination.Database is not null)
            throw new NotSupportedException(
                $"BulkInsertOptions.TargetKind.LocalTemporary cannot be combined with BulkInsertOptions.DatabaseName ('{destination.Database}'): a local temporary table lives in the current session's temporary store, never in another database.");

        command.BulkCopy.ThrowIfRequested("the portable INSERT ... VALUES path");
    }

    /// <summary>
    /// Resolves the active dialect for destination validation and reporting, and refuses a qualification or
    /// a conflict form the dialect cannot express before any row is read.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The active dialect, or <see langword="null"/> when the executor is not attached to a context.</returns>
    private ISqlDialect? ResolveDialect(BulkInsertCommand command)
    {
        var dialect = (mutation as DataContext)?.Dialect;
        if (dialect is not null)
        {
            _ = BulkInsertDestination.From(command.Options).ResolveQualification(dialect);

            // MySQL/MariaDB and SQLite/PostgreSQL/ClickHouse express the skip through their own ignore or
            // conflict form; a dialect with neither (SQL Server) cannot skip a conflicting row on this path,
            // so the request is refused by name before the source is pulled.
            if (command.IgnoreConflicts && !dialect.SupportsInsertIgnore && !dialect.SupportsOnConflictDoNothing)
                throw new NotSupportedException(
                    "BulkInsertOptions.IgnoreDuplicates=true cannot be honoured by the portable INSERT ... VALUES path on "
                    + dialect.GetType().Name
                    + ": it has neither an INSERT IGNORE/INSERT OR IGNORE head nor an ON CONFLICT DO NOTHING clause, so a conflicting row would fail the whole write.");
        }

        return dialect;
    }

    /// <summary>
    /// Enters the provider-scoped session controls and shape guards that <see cref="BulkInsertMode.Portable"/>
    /// must run exactly like the native path. A provider without such state contributes nothing.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The scope to dispose once the write is finished, or <see langword="null"/> when the provider has none.</returns>
    private IDisposable? BeginProviderScope(BulkInsertCommand command)
        => (mutation as IBulkInsertPortableScope)?.BeginPortableBulkInsertScope(command);

    // Writes every batch in the buffer, splitting it further when a batch exceeds the rendered-SQL limit.
    // Progress is reported only after a batch has been written; nothing is rolled back on failure.
    private int Drain(BulkInsertCommand command, List<object?[]> batch, int total, BulkInsertDeadline deadline, ISqlDialect? dialect)
    {
        while (batch.Count > 0)
        {
            var take = FitBatch(command, batch, dialect);
            var slice = take == batch.Count ? batch : batch.GetRange(0, take);

            deadline.ThrowIfCancelled();
            total += mutation.Execute(BuildBatch(command, slice, dialect: dialect));
            deadline.ThrowIfCancelled();

            command.Progress?.Invoke(total);
            batch.RemoveRange(0, take);
        }

        return total;
    }

    // Asynchronous mirror of Drain: every write is awaited before the next batch is pulled.
    private async Task<int> DrainAsync(BulkInsertCommand command, List<object?[]> batch, int total, BulkInsertDeadline deadline, ISqlDialect? dialect)
    {
        while (batch.Count > 0)
        {
            var take = FitBatch(command, batch, dialect);
            var slice = take == batch.Count ? batch : batch.GetRange(0, take);

            deadline.ThrowIfCancelled();

            int written;
            try
            {
                written = await mutation.Execute(BuildBatch(command, slice, dialect: dialect), deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException cancellation)
            {
                throw deadline.Translate(cancellation);
            }

            deadline.ThrowIfCancelled();

            total += written;
            command.Progress?.Invoke(total);
            batch.RemoveRange(0, take);
        }

        return total;
    }

    /// <summary>
    /// Returns how many leading rows of <paramref name="batch"/> fit in one statement: the whole batch when
    /// no rendered-length limit applies or it fits, otherwise the largest prefix whose rendered SQL fits.
    /// </summary>
    /// <param name="command">The bulk-insert command the batch belongs to.</param>
    /// <param name="batch">The buffered rows.</param>
    /// <param name="dialect">The active dialect, or <see langword="null"/>.</param>
    /// <returns>The number of leading rows to write as one statement.</returns>
    /// <exception cref="NotSupportedException">A single row already exceeds the rendered-length limit.</exception>
    private int FitBatch(BulkInsertCommand command, List<object?[]> batch, ISqlDialect? dialect)
    {
        if (command.Batch?.MaxSqlLength is not int maxLength)
            return batch.Count;

        var renderedLength = RenderBatch(command, batch, batch.Count, dialect).Length;
        if (renderedLength <= maxLength)
            return batch.Count;

        // Binary search the largest prefix that fits; a single oversized row leaves low at 0.
        var low = 0;
        var high = batch.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (RenderBatch(command, batch, middle, dialect).Length <= maxLength)
                low = middle;
            else
                high = middle - 1;
        }

        if (low == 0)
            throw new NotSupportedException(
                $"A single bulk-insert row renders {renderedLength} characters of SQL, which exceeds BulkInsertOptions.MaxSqlLength={maxLength}: "
                + "the row cannot be expressed as one portable INSERT ... VALUES statement. Raise MaxSqlLength or split the row.");

        return low;
    }

    private string RenderBatch(BulkInsertCommand command, List<object?[]> batch, int count, ISqlDialect? dialect)
    {
        var slice = count == batch.Count ? batch : batch.GetRange(0, count);
        return mutation.Render(BuildBatch(command, slice, dialect: dialect));
    }

    /// <summary>
    /// The maximum rows one statement binds: the row limit and the exact parameter count (one parameter per
    /// column per row). The rendered-length limit is applied by <see cref="FitBatch"/>.
    /// </summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The maximum rows to write per statement.</returns>
    private static int RowLimit(BulkInsertCommand command)
    {
        if (command.Batch is null || command.Batch.IsUnbounded)
            return int.MaxValue;

        var columnCount = Math.Max(1, command.Columns.Count);
        var limit = int.MaxValue;

        if (command.Batch.MaxBatchSize is int rows && rows > 0)
            limit = Math.Min(limit, rows);

        if (command.Batch.MaxParameters is int parameters && parameters > 0)
            limit = Math.Min(limit, Math.Max(1, parameters / columnCount));

        return limit;
    }

    /// <summary>Computes the rows per batch from the command's limits (<see cref="int.MaxValue"/> for unbounded).</summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The maximum rows to write per statement.</returns>
    internal static int RowsPerBatch(BulkInsertCommand command)
    {
        if (command.Batch is null || command.Batch.IsUnbounded)
            return int.MaxValue;

        var columnCount = Math.Max(1, command.Columns.Count);
        var limit = RowLimit(command);

        if (command.Batch.MaxSqlLength is int maxLength and > 0)
        {
            const int EstimatedCharactersPerParameter = 12;
            const int EstimatedCharactersPerRowOverhead = 8;
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
    /// <param name="dialect">The active dialect used to resolve the destination qualification, or <see langword="null"/> when it is not known.</param>
    /// <returns>The insert command for the batch.</returns>
    internal static InsertCommand BuildBatch(BulkInsertCommand command, List<object?[]> batch, IReadOnlyList<IPropertyMetadata>? returningColumns = null, ISqlDialect? dialect = null)
    {
        var columns = new InsertColumn[command.Columns.Count];

        for (var c = 0; c < command.Columns.Count; c++)
        {
            var values = new InsertValue[batch.Count];
            for (var r = 0; r < batch.Count; r++)
                values[r] = InsertValue.FromConstant(batch[r][c]);

            columns[c] = new InsertColumn(command.Columns[c], values);
        }

        // The destination is resolved once per batch: a schema-only override keeps the mapped table, and the
        // qualification is rendered per part by the insert renderer. Without a dialect the raw parts are used,
        // which is what the SQL-only diagnostics path renders.
        var destination = BulkInsertDestination.From(command.Options);
        var qualification = dialect is null
            ? destination.Qualification
            : destination.ResolveQualification(dialect);

        return new InsertCommand(
            command.EntityType,
            destination.Table ?? command.TableName,
            destination.Table is null && command.IsTableNameAuto,
            columns,
            batch.Count,
            identityColumn: null,
            returningColumns: returningColumns,
            source: null,
            sourceColumns: null,
            ignoreConflicts: command.IgnoreConflicts,
            keepIdentity: command.KeepIdentity,
            tableSchema: qualification);
    }
}
