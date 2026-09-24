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
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool KeepIdentity { get; init; }

    /// <summary>
    /// Whether rows that violate a unique constraint are skipped instead of failing the whole write.
    /// Forces the portable path and uses the provider's ignore form (<c>ON CONFLICT DO NOTHING</c>,
    /// <c>INSERT OR IGNORE</c>, <c>INSERT IGNORE</c>). Defaults to <see langword="false"/>.
    /// </summary>
    public bool IgnoreDuplicates { get; init; }

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

    /// <summary>Validates the option values, throwing for a non-positive bound or interval.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size, timeout or interval is not positive.</exception>
    internal void Validate()
    {
        ThrowIfNonPositive(MaxBatchSize, nameof(MaxBatchSize));
        ThrowIfNonPositive(MaxParameters, nameof(MaxParameters));
        ThrowIfNonPositive(MaxSqlLength, nameof(MaxSqlLength));
        ThrowIfNonPositive(TimeoutSeconds, nameof(TimeoutSeconds));

        if (NotifyEvery <= 0)
            throw new ArgumentOutOfRangeException(nameof(NotifyEvery), NotifyEvery, "NotifyEvery must be positive.");
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
