namespace NextORM.Core;

/// <summary>
/// The backend that owns a result-set cursor: a live command reader (<see cref="ProcedureResult"/>) or
/// an eager buffer (<see cref="BatchResult"/>). Implemented explicitly so the read methods do not leak
/// onto the public surface of either result type.
/// </summary>
internal interface IResultSetCursorSource
{
    /// <summary>Reads the current set of <paramref name="cursor"/> eagerly into <typeparamref name="T"/>.</summary>
    IReadOnlyList<T> ReadCurrentSet<T>(ResultSet cursor);

    /// <summary>Reads the current set of <paramref name="cursor"/> lazily into <typeparamref name="T"/>.</summary>
    IAsyncEnumerable<T> ReadCurrentSetAsync<T>(ResultSet cursor, CancellationToken cancellationToken);
}
