namespace NextORM.Core;

/// <summary>
/// Batch-size limits for the bulk-insert paths. A <see langword="null"/> limit is unbounded, so with no
/// options the whole set is written as one <c>INSERT ... VALUES</c> on the portable path.
/// </summary>
internal sealed class BulkBatchOptions
{
    /// <summary>Creates a batch-size limit set.</summary>
    /// <param name="maxBatchSize">Maximum rows per statement, or <see langword="null"/> for unbounded.</param>
    /// <param name="maxParameters">Maximum bound parameters per statement, or <see langword="null"/> for unbounded.</param>
    /// <param name="maxSqlLength">Approximate maximum SQL text length per statement, or <see langword="null"/> for unbounded.</param>
    public BulkBatchOptions(int? maxBatchSize, int? maxParameters, int? maxSqlLength)
    {
        MaxBatchSize = maxBatchSize;
        MaxParameters = maxParameters;
        MaxSqlLength = maxSqlLength;
    }

    /// <summary>Maximum rows per statement, or <see langword="null"/> for unbounded.</summary>
    public int? MaxBatchSize { get; }

    /// <summary>Maximum bound parameters per statement, or <see langword="null"/> for unbounded.</summary>
    public int? MaxParameters { get; }

    /// <summary>Approximate maximum SQL text length per statement, or <see langword="null"/> for unbounded.</summary>
    public int? MaxSqlLength { get; }

    /// <summary>Whether no limit was set, so the portable path writes the whole set as one statement.</summary>
    public bool IsUnbounded => MaxBatchSize is null && MaxParameters is null && MaxSqlLength is null;
}

/// <summary>
/// A bulk-insert request: the target table, the written columns and a streaming sequence of rows, plus
/// the options that only apply to the portable path (<c>RETURNING</c>/<c>OUTPUT</c> and conflict handling
/// force the portable path, so the native executor never sees them set).
/// </summary>
internal sealed class BulkInsertCommand
{
    /// <summary>Creates a bulk-insert command.</summary>
    /// <param name="entityType">The CLR entity type written.</param>
    /// <param name="tableName">The mapped table name, before the naming convention and identifier quoting are applied.</param>
    /// <param name="isTableNameAuto">Whether <paramref name="tableName"/> was derived from the CLR type name.</param>
    /// <param name="columns">The written columns, in parameter order.</param>
    /// <param name="syncRows">The rows to write, or <see langword="null"/> when only an async source was supplied.</param>
    /// <param name="asyncRows">The rows to write, or <see langword="null"/> when only a sync source was supplied.</param>
    /// <param name="keepIdentity">Whether explicit values are written to identity columns.</param>
    /// <param name="ignoreConflicts">Whether rows that violate a unique constraint should be skipped.</param>
    /// <param name="batch">The batch-size limits, or <see langword="null"/> for one statement.</param>
    /// <param name="progress">Called with the cumulative written-row count while the write proceeds, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The reporting interval in rows; the native path uses it to set its own progress cadence.</param>
    /// <param name="timeoutSeconds">The command timeout in seconds, or <see langword="null"/> for the provider default.</param>
    /// <param name="tableSchema">The schema (or database) that qualifies <paramref name="tableName"/>, or <see langword="null"/>.</param>
    /// <param name="bulkCopy">The SQL Server-only bulk-copy flags, or <see cref="BulkCopyFlags.None"/>.</param>
    public BulkInsertCommand(
        Type entityType,
        string tableName,
        bool isTableNameAuto,
        IReadOnlyList<IPropertyMetadata> columns,
        IEnumerable<object?[]>? syncRows,
        IAsyncEnumerable<object?[]>? asyncRows,
        bool keepIdentity,
        bool ignoreConflicts,
        BulkBatchOptions? batch,
        Action<int>? progress,
        int notifyEvery,
        int? timeoutSeconds,
        string? tableSchema = null,
        BulkCopyFlags bulkCopy = default)
    {
        EntityType = entityType;
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        Columns = columns;
        SyncRows = syncRows;
        AsyncRows = asyncRows;
        KeepIdentity = keepIdentity;
        IgnoreConflicts = ignoreConflicts;
        Batch = batch;
        Progress = progress;
        NotifyEvery = notifyEvery;
        TimeoutSeconds = timeoutSeconds;
        TableSchema = tableSchema;
        BulkCopy = bulkCopy;
    }

    /// <summary>The CLR entity type written.</summary>
    public Type EntityType { get; }

    /// <summary>The mapped table name, before the naming convention and identifier quoting are applied.</summary>
    public string TableName { get; }

    /// <summary>Whether <see cref="TableName"/> was auto-derived and the naming convention applies to it.</summary>
    public bool IsTableNameAuto { get; }

    /// <summary>The schema (or database) that qualifies <see cref="TableName"/>, or <see langword="null"/>.</summary>
    public string? TableSchema { get; }

    /// <summary>The written columns, in parameter order.</summary>
    public IReadOnlyList<IPropertyMetadata> Columns { get; }

    /// <summary>The rows to write, or <see langword="null"/> when only an async source was supplied.</summary>
    public IEnumerable<object?[]>? SyncRows { get; }

    /// <summary>The rows to write, or <see langword="null"/> when only a sync source was supplied.</summary>
    public IAsyncEnumerable<object?[]>? AsyncRows { get; }

    /// <summary>Whether explicit values are written to identity columns.</summary>
    public bool KeepIdentity { get; }

    /// <summary>Whether rows that violate a unique constraint should be skipped.</summary>
    public bool IgnoreConflicts { get; }

    /// <summary>The batch-size limits, or <see langword="null"/> for one statement.</summary>
    public BulkBatchOptions? Batch { get; }

    /// <summary>
    /// Called with the cumulative written-row count while the write proceeds, or <see langword="null"/>.
    /// It is invoked inline, after a committed batch (portable path) or on the native progress tick; a
    /// callback that throws or blocks therefore propagates/blocks the call, and already-written rows are
    /// not rolled back (nextorm opens no implicit transaction).
    /// </summary>
    public Action<int>? Progress { get; }

    /// <summary>The reporting interval in rows; the native path uses it to drive its progress cadence.</summary>
    public int NotifyEvery { get; }

    /// <summary>The command timeout in seconds, or <see langword="null"/> for the provider default.</summary>
    public int? TimeoutSeconds { get; }

    /// <summary>The SQL Server-only bulk-copy flags, or <see cref="BulkCopyFlags.None"/>.</summary>
    public BulkCopyFlags BulkCopy { get; }
}

/// <summary>
/// Execution role of a database-backed context for bulk inserts. Implemented by
/// <see cref="DataContext"/>: it runs the provider's native bulk path when one is available and the
/// portable <c>INSERT ... VALUES</c> path otherwise.
/// </summary>
internal interface IBulkInsertExecutor
{
    /// <summary>Writes every row of <paramref name="command"/> and returns the number written.</summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <returns>The number of rows written.</returns>
    int BulkInsert(BulkInsertCommand command);

    /// <summary>Asynchronously writes every row of <paramref name="command"/> and returns the number written.</summary>
    /// <param name="command">The bulk-insert command.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    Task<int> BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken);
}
