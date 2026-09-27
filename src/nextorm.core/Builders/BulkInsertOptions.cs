namespace NextORM.Core;

/// <summary>
/// Options for a bulk insert started with <c>BulkInsertInto</c>.
/// Immutable and safe to reuse; build one with <see cref="BulkInsertOptionsBuilder"/> or set the properties
/// directly. Providers that cannot express a requested option reject it with
/// <see cref="NotSupportedException"/> when the write runs.
/// </summary>
public sealed record BulkInsertOptions
{
    /// <summary>
    /// The maximum rows per statement on the portable path; the native path treats it as a batch hint
    /// (SQL Server <c>SqlBulkCopy.BatchSize</c>) or ignores it (PostgreSQL <c>COPY</c> streams). Defaults
    /// to <see langword="null"/> (one statement, or the provider default).
    /// </summary>
    public int? MaxBatchSize { get; init; }

    /// <summary>The maximum bound parameters per statement on the portable path. Defaults to <see langword="null"/>.</summary>
    public int? MaxParameters { get; init; }

    /// <summary>The approximate maximum SQL text length per statement on the portable path. Defaults to <see langword="null"/>.</summary>
    public int? MaxSqlLength { get; init; }

    /// <summary>
    /// Whether explicit values are written to identity columns. Forces the portable path: SQL Server wraps
    /// the insert with <c>SET IDENTITY_INSERT</c> and PostgreSQL adds <c>OVERRIDING SYSTEM VALUE</c>.
    /// Silently ignored when the entity's mapping declares no identity column (matching linq2db), so it
    /// never surfaces SQL Server error 8106 on a non-identity table and does not force the portable path
    /// there. Defaults to <see langword="false"/>.
    /// </summary>
    public bool KeepIdentity { get; init; }

    /// <summary>
    /// Whether rows that violate a unique constraint are skipped instead of failing the whole write.
    /// Forces the portable path and uses the provider's ignore form (<c>ON CONFLICT DO NOTHING</c>,
    /// <c>INSERT OR IGNORE</c>, <c>INSERT IGNORE</c>). Defaults to <see langword="false"/>.
    /// </summary>
    public bool IgnoreDuplicates { get; init; }

    /// <summary>
    /// Whether CHECK and FOREIGN KEY constraints are checked by the destination while the rows are
    /// written. Only the SQL Server native <c>SqlBulkCopy</c> path can express it (it maps to
    /// <c>SqlBulkCopyOptions.CheckConstraints</c>, which SQL Server leaves off by default); every other
    /// bulk path always enforces or always ignores constraints and rejects a request with a clear
    /// <see cref="NotSupportedException"/>. Defaults to <see langword="null"/> (the provider default).
    /// </summary>
    public bool? CheckConstraints { get; init; }

    /// <summary>
    /// Whether a table-level bulk-update lock is taken for the duration of the write. Only the SQL
    /// Server native <c>SqlBulkCopy</c> path can express it (<c>SqlBulkCopyOptions.TableLock</c>);
    /// other bulk paths reject a request with a clear <see cref="NotSupportedException"/>. Defaults to
    /// <see langword="null"/> (row locks).
    /// </summary>
    public bool? TableLock { get; init; }

    /// <summary>
    /// Whether explicit null values are written instead of the destination column's DEFAULT. Only the
    /// SQL Server native <c>SqlBulkCopy</c> path can express it (<c>SqlBulkCopyOptions.KeepNulls</c>);
    /// other bulk paths reject a request with a clear <see cref="NotSupportedException"/>. Defaults to
    /// <see langword="null"/> (the provider default).
    /// </summary>
    public bool? KeepNulls { get; init; }

    /// <summary>
    /// Whether INSERT triggers fire for the written rows. Only the SQL Server native
    /// <c>SqlBulkCopy</c> path can express it (<c>SqlBulkCopyOptions.FireTriggers</c>); other bulk paths
    /// always fire (or never have) triggers and reject a request with a clear
    /// <see cref="NotSupportedException"/>. Defaults to <see langword="null"/> (triggers do not fire on
    /// the native path).
    /// </summary>
    public bool? FireTriggers { get; init; }

    /// <summary>
    /// Overrides the entity's mapped table for this write, without requiring a second <c>[SqlTable]</c>
    /// mapping. When set, the naming convention is not applied to the target; <see cref="TableSchema"/>
    /// optionally qualifies it. Defaults to <see langword="null"/> (the mapped table name is used).
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// The schema (or database) that qualifies <see cref="TableName"/>, or <see langword="null"/> for an
    /// unqualified target. Only meaningful together with <see cref="TableName"/>.
    /// </summary>
    public string? TableSchema { get; init; }

    /// <summary>
    /// The command timeout in seconds. Only SQL Server's native <c>SqlBulkCopy</c> path honours it; the
    /// PostgreSQL <c>COPY</c> path and the portable path reject it with a clear
    /// <see cref="NotSupportedException"/>. Defaults to <see langword="null"/> (the provider default).
    /// </summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Called with the cumulative written-row count every <see cref="NotifyEvery"/> rows, or
    /// <see langword="null"/>. The callback runs inline after a committed batch (portable) or on the
    /// provider's native progress tick; nextorm opens no implicit transaction, so a throwing callback
    /// propagates and leaves the already-written rows committed. The second argument is the token from
    /// <see cref="ProgressCancellationTokenSource"/> (or <see cref="CancellationToken.None"/>); the write
    /// aborts with <see cref="OperationCanceledException"/> if the token is cancelled when the callback
    /// returns — arm it with <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/> to time-limit the
    /// callback cooperatively.
    /// </summary>
    public Action<int, CancellationToken>? Progress { get; init; }

    /// <summary>
    /// The source whose <see cref="CancellationTokenSource.Token"/> is passed to <see cref="Progress"/>, or
    /// <see langword="null"/> for <see cref="CancellationToken.None"/>. Owned by the caller: nextorm never
    /// cancels or disposes it. Cancelling it (for example through
    /// <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>) makes the writer throw
    /// <see cref="OperationCanceledException"/> once the in-flight callback returns.
    /// </summary>
    public CancellationTokenSource? ProgressCancellationTokenSource { get; init; }

    /// <summary>The progress reporting interval in rows. Defaults to <c>1</c>.</summary>
    public int NotifyEvery { get; init; } = 1;

    /// <summary>Validates the option values, throwing for a non-positive bound, an empty table target or an invalid interval.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size, timeout or interval is not positive.</exception>
    /// <exception cref="ArgumentException">A table target is empty, or a schema was given without a table.</exception>
    internal void Validate()
    {
        ThrowIfNonPositive(MaxBatchSize, nameof(MaxBatchSize));
        ThrowIfNonPositive(MaxParameters, nameof(MaxParameters));
        ThrowIfNonPositive(MaxSqlLength, nameof(MaxSqlLength));
        ThrowIfNonPositive(TimeoutSeconds, nameof(TimeoutSeconds));

        if (NotifyEvery <= 0)
            throw new ArgumentOutOfRangeException(nameof(NotifyEvery), NotifyEvery, "NotifyEvery must be positive.");

        if (TableName is not null && string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("TableName must not be empty.", nameof(TableName));

        if (TableSchema is not null && string.IsNullOrWhiteSpace(TableSchema))
            throw new ArgumentException("TableSchema must not be empty.", nameof(TableSchema));

        if (TableSchema is not null && TableName is null)
            throw new ArgumentException("TableSchema requires TableName to be set.", nameof(TableSchema));
    }

    private static void ThrowIfNonPositive(int? value, string name)
    {
        if (value is <= 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
    }
}

/// <summary>
/// Fluent builder for <see cref="BulkInsertOptions"/>. Every setter validates immediately and returns the
/// builder, so an invalid option fails at the call site rather than at execution.
/// </summary>
public sealed class BulkInsertOptionsBuilder
{
    private int? _maxBatchSize;
    private int? _maxParameters;
    private int? _maxSqlLength;
    private bool _keepIdentity;
    private bool _ignoreDuplicates;
    private bool? _checkConstraints;
    private bool? _tableLock;
    private bool? _keepNulls;
    private bool? _fireTriggers;
    private string? _tableName;
    private string? _tableSchema;
    private int? _timeoutSeconds;
    private Action<int, CancellationToken>? _progress;
    private int _notifyEvery = 1;
    private CancellationTokenSource? _progressCancellationTokenSource;

    /// <summary>Limits the rows written per statement (the portable path chunks; the native path treats it as a batch hint or ignores it).</summary>
    /// <param name="rows">The maximum rows per statement; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rows"/> is not positive.</exception>
    public BulkInsertOptionsBuilder MaxBatchSize(int rows)
    {
        ThrowIfNonPositive(rows, nameof(rows));
        _maxBatchSize = rows;
        return this;
    }

    /// <summary>Limits the bound parameters per statement on the portable path.</summary>
    /// <param name="count">The maximum parameters per statement; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public BulkInsertOptionsBuilder MaxParameters(int count)
    {
        ThrowIfNonPositive(count, nameof(count));
        _maxParameters = count;
        return this;
    }

    /// <summary>Limits the approximate SQL text length per statement on the portable path.</summary>
    /// <param name="characters">The approximate maximum SQL length; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="characters"/> is not positive.</exception>
    public BulkInsertOptionsBuilder MaxSqlLength(int characters)
    {
        ThrowIfNonPositive(characters, nameof(characters));
        _maxSqlLength = characters;
        return this;
    }

    /// <summary>Writes explicit values to identity columns. Forces the portable path.</summary>
    /// <param name="value">Whether to write identity values; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder KeepIdentity(bool value = true)
    {
        _keepIdentity = value;
        return this;
    }

    /// <summary>Skips rows that violate a unique constraint. Forces the portable path.</summary>
    /// <param name="value">Whether to skip conflicting rows; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder IgnoreDuplicates(bool value = true)
    {
        _ignoreDuplicates = value;
        return this;
    }

    /// <summary>Checks CHECK and FOREIGN KEY constraints during the write (SQL Server native path only).</summary>
    /// <param name="value">Whether constraints are checked; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder CheckConstraints(bool value = true)
    {
        _checkConstraints = value;
        return this;
    }

    /// <summary>Takes a table-level bulk-update lock for the duration of the write (SQL Server native path only).</summary>
    /// <param name="value">Whether the table is locked; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder TableLock(bool value = true)
    {
        _tableLock = value;
        return this;
    }

    /// <summary>Writes explicit null values instead of the destination DEFAULT (SQL Server native path only).</summary>
    /// <param name="value">Whether nulls are kept; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder KeepNulls(bool value = true)
    {
        _keepNulls = value;
        return this;
    }

    /// <summary>Fires INSERT triggers for the written rows (SQL Server native path only).</summary>
    /// <param name="value">Whether triggers fire; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder FireTriggers(bool value = true)
    {
        _fireTriggers = value;
        return this;
    }

    /// <summary>
    /// Overrides the target table for this write. The naming convention is not applied to an explicit
    /// target; use the two-argument <see cref="Table(string, string)"/> overload to qualify it with a schema.
    /// </summary>
    /// <param name="table">The destination table name; must not be empty.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="table"/> is <see langword="null"/> or empty.</exception>
    public BulkInsertOptionsBuilder Table(string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        _tableName = table;
        _tableSchema = null;
        return this;
    }

    /// <summary>Overrides the target table for this write and qualifies it with a schema (or database).</summary>
    /// <param name="schema">The schema (or database) name; must not be empty.</param>
    /// <param name="table">The destination table name; must not be empty.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="schema"/> or <paramref name="table"/> is <see langword="null"/> or empty.</exception>
    public BulkInsertOptionsBuilder Table(string schema, string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        ArgumentException.ThrowIfNullOrEmpty(table);
        _tableSchema = schema;
        _tableName = table;
        return this;
    }

    /// <summary>Sets the command timeout in seconds (only SQL Server's native path honours it).</summary>
    /// <param name="seconds">The timeout in seconds; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is not positive.</exception>
    public BulkInsertOptionsBuilder Timeout(int seconds)
    {
        ThrowIfNonPositive(seconds, nameof(seconds));
        _timeoutSeconds = seconds;
        return this;
    }

    /// <summary>Reports progress with the cumulative written-row count every <paramref name="rows"/> rows.</summary>
    /// <param name="rows">The reporting interval in rows; must be positive.</param>
    /// <param name="onRows">Called with the cumulative written-row count and the progress cancellation token.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onRows"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rows"/> is not positive.</exception>
    public BulkInsertOptionsBuilder NotifyAfter(int rows, Action<int, CancellationToken> onRows)
    {
        ArgumentNullException.ThrowIfNull(onRows);
        ThrowIfNonPositive(rows, nameof(rows));

        _notifyEvery = rows;
        _progress = onRows;
        return this;
    }

    /// <summary>
    /// Supplies the token passed to the <see cref="NotifyAfter"/> callback, enabling cooperative
    /// time-limiting (<c>source.CancelAfter(timeout)</c>). The source is owned by the caller.
    /// </summary>
    /// <param name="source">The source whose token the callback observes.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public BulkInsertOptionsBuilder ProgressCancellationTokenSource(CancellationTokenSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _progressCancellationTokenSource = source;
        return this;
    }

    /// <summary>Builds the immutable options.</summary>
    /// <returns>The configured options.</returns>
    public BulkInsertOptions Build() => new()
    {
        MaxBatchSize = _maxBatchSize,
        MaxParameters = _maxParameters,
        MaxSqlLength = _maxSqlLength,
        KeepIdentity = _keepIdentity,
        IgnoreDuplicates = _ignoreDuplicates,
        CheckConstraints = _checkConstraints,
        TableLock = _tableLock,
        KeepNulls = _keepNulls,
        FireTriggers = _fireTriggers,
        TableName = _tableName,
        TableSchema = _tableSchema,
        TimeoutSeconds = _timeoutSeconds,
        Progress = _progress,
        NotifyEvery = _notifyEvery,
        ProgressCancellationTokenSource = _progressCancellationTokenSource,
    };

    private static void ThrowIfNonPositive(int value, string name)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
    }
}
