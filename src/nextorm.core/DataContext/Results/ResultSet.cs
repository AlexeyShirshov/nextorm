namespace NextORM.Core;

/// <summary>
/// A single column-bearing result set of a multi-result command, produced by enumerating
/// <see cref="ProcedureResult"/> or <see cref="BatchResult"/>. The cursor is valid only while its set is
/// the current one: reading after the outer traversal has advanced or ended, after a previous read of
/// the same set, or through an uninitialized default value throws <see cref="InvalidOperationException"/>;
/// reading after the owning result was disposed throws <see cref="ObjectDisposedException"/>.
/// </summary>
/// <remarks>
/// <see cref="ColumnNames"/> is snapshotted when the cursor is produced and stays readable after the
/// outer cursor advances. Each set can be consumed once, either with <see cref="Read{T}"/> (eager) or
/// with <see cref="ReadAsync{T}"/> (lazy); mixing the two on one set is rejected.
/// </remarks>
public readonly struct ResultSet
{
    private readonly IResultSetCursorSource? _source;
    private readonly int _index;
    private readonly string[]? _columnNames;

    internal ResultSet(IResultSetCursorSource source, int index, string[] columnNames)
    {
        _source = source;
        _index = index;
        _columnNames = columnNames;
    }

    /// <summary>The 0-based position of this set among the column-bearing result sets of the command.</summary>
    public int Index => _index;

    /// <summary>The number of columns in this result set.</summary>
    public int FieldCount => _columnNames?.Length ?? 0;

    /// <summary>The column names of this result set, snapshotted when the cursor was produced.</summary>
    public IReadOnlyList<string> ColumnNames => _columnNames ?? Array.Empty<string>();

    /// <summary>Materializes all rows of this result set into <typeparamref name="T"/> (eager).</summary>
    /// <typeparam name="T">A mapped entity type or a scalar type (see <see cref="IRawCommandExecutor"/>).</typeparam>
    /// <returns>The rows of this set; empty when the set has no rows.</returns>
    /// <exception cref="InvalidOperationException">The cursor is uninitialized, stale, already read, or the traversal has ended.</exception>
    /// <exception cref="ObjectDisposedException">The owning result has been disposed.</exception>
    public IReadOnlyList<T> Read<T>()
        => _source is null
            ? throw new InvalidOperationException("The result-set cursor is not initialized.")
            : _source.ReadCurrentSet<T>(this);

    /// <summary>Lazily reads the rows of this result set into <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">A mapped entity type or a scalar type (see <see cref="IRawCommandExecutor"/>).</typeparam>
    /// <param name="ct">Cancels reading of this set.</param>
    /// <returns>An async sequence over the rows of this set.</returns>
    /// <exception cref="InvalidOperationException">The cursor is uninitialized, stale, already read, or the traversal has ended.</exception>
    /// <exception cref="ObjectDisposedException">The owning result has been disposed.</exception>
    public IAsyncEnumerable<T> ReadAsync<T>(CancellationToken ct = default)
        => _source is null
            ? throw new InvalidOperationException("The result-set cursor is not initialized.")
            : _source.ReadCurrentSetAsync<T>(this, ct);

    internal IResultSetCursorSource? Source => _source;

    internal bool IsInitialized => _source is not null;
}
