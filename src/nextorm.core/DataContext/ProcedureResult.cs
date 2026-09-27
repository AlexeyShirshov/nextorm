using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// The result of a raw command (<see cref="IRawCommandExecutor.ExecuteRaw(string, IReadOnlyList{ProcedureParameter})"/>)
/// or a stored procedure (<see cref="IRawCommandExecutor.ExecuteProcedure(string, IReadOnlyList{ProcedureParameter})"/>).
/// Owns the command and its data reader until disposed; result sets are read sequentially with
/// <see cref="Read{T}"/> / <see cref="ReadAsync{T}"/>, each call advancing to the next set.
/// </summary>
/// <remarks>
/// The first <c>Read</c> skips leading result sets without columns (DDL/DML statements executed before
/// the result-bearing statement) and then reads the current set. Accessing
/// <see cref="OutputParameters"/> or <see cref="ReturnValue"/> closes the reader (discarding any
/// remaining result sets) so providers can populate ADO.NET output parameters. The connection stays
/// owned by the context and is never closed by this object.
/// </remarks>
public sealed class ProcedureResult : IAsyncDisposable, IDisposable
{
    private readonly DataContext _context;
    private readonly CommandReaderOwner _owner;
    private int _resultSetIndex = -1;
    private bool _outputsRead;
    private bool _disposed;
    private IReadOnlyList<ProcedureOutputParameter>? _outputs;
    private object? _returnValue;

    internal ProcedureResult(DataContext context, CommandReaderOwner owner)
    {
        _context = context;
        _owner = owner;
    }

    /// <summary>
    /// The output and input/output parameters of the command, snapshotted when first accessed
    /// (<c>DBNull</c> normalized to <see langword="null"/>). Accessing this property closes the reader
    /// and discards any result sets not yet read.
    /// </summary>
    public IReadOnlyList<ProcedureOutputParameter> OutputParameters
    {
        get
        {
            ThrowIfDisposed();
            EnsureOutputsRead();
            return _outputs!;
        }
    }

    /// <summary>
    /// The return value of the command (a parameter with <see cref="ParameterDirection.ReturnValue"/>),
    /// or <see langword="null"/> when the command declares none. Accessing this property closes the
    /// reader and discards any result sets not yet read.
    /// </summary>
    public object? ReturnValue
    {
        get
        {
            ThrowIfDisposed();
            EnsureOutputsRead();
            return _returnValue;
        }
    }

    /// <summary>
    /// Advances to the next result set (the first call reads the current one) and materializes all of
    /// its rows into <typeparamref name="T"/>. Each call returns one set; when there are no more sets,
    /// an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    /// <typeparam name="T">A mapped entity type or a scalar type (see <see cref="IRawCommandExecutor"/>).</typeparam>
    /// <returns>The materialized rows of the current result set; empty when the set has no rows.</returns>
    /// <exception cref="InvalidOperationException">There are no further result sets.</exception>
    /// <exception cref="ObjectDisposedException">The result has been disposed.</exception>
    public IReadOnlyList<T> Read<T>()
    {
        ThrowIfDisposed();
        ThrowIfReaderClosed();

        var reader = MoveToNextResultSet();
        // Advance the cursor before mapping/building the mapper: a mapping failure must not make the
        // next Read re-read this set (the reader is already positioned past it).
        _resultSetIndex++;
        var mapper = RawMapperFactory.GetOrBuild<T>(_context, reader);

        var list = new List<T>();
        while (reader.Read())
            list.Add(mapper(reader));

        return list;
    }

    /// <summary>
    /// Asynchronously advances to the next result set (the first call reads the current one) and
    /// materializes all of its rows into <typeparamref name="T"/>. Each call returns one set; when
    /// there are no more sets, an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    /// <typeparam name="T">A mapped entity type or a scalar type (see <see cref="IRawCommandExecutor"/>).</typeparam>
    /// <param name="cancellationToken">Cancels reading of the result set.</param>
    /// <returns>An async sequence over the rows of the current result set.</returns>
    /// <remarks>
    /// This is a lazy async iterator: it advances to the next result set when enumeration starts, not
    /// when the method is called. Enumerate the returned sequence once; a second enumeration would try
    /// to read another result set.
    /// </remarks>
    /// <exception cref="InvalidOperationException">There are no further result sets.</exception>
    /// <exception cref="ObjectDisposedException">The result has been disposed.</exception>
    public async IAsyncEnumerable<T> ReadAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfReaderClosed();

        var reader = await MoveToNextResultSetAsync(cancellationToken).ConfigureAwait(false);
        // See Read<T>: advance the cursor before mapping so a mapping failure cannot re-read this set.
        _resultSetIndex++;
        var mapper = RawMapperFactory.GetOrBuild<T>(_context, reader);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return mapper(reader);
    }

    /// <summary>Releases the reader and the command. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _owner.Dispose();
    }

    /// <summary>Asynchronously releases the reader and the command. Safe to call more than once.</summary>
    /// <returns>A value task that completes once the reader and command are disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _owner.DisposeAsync().ConfigureAwait(false);
    }

    private DbDataReader MoveToNextResultSet()
    {
        var reader = _owner.Reader;

        if (_resultSetIndex < 0)
        {
            ResultSetNavigator.AdvanceToResultSet(reader);
        }
        else
        {
            if (!reader.NextResult())
                throw NoMoreResultSets();

            ResultSetNavigator.AdvanceToResultSet(reader);
        }

        if (reader.FieldCount == 0)
            throw NoMoreResultSets();

        return reader;
    }

    private async Task<DbDataReader> MoveToNextResultSetAsync(CancellationToken cancellationToken)
    {
        var reader = _owner.Reader;

        if (_resultSetIndex < 0)
        {
            await ResultSetNavigator.AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                throw NoMoreResultSets();

            await ResultSetNavigator.AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        if (reader.FieldCount == 0)
            throw NoMoreResultSets();

        return reader;
    }

    private void EnsureOutputsRead()
    {
        if (_outputsRead)
            return;

        // Closing the reader is what makes ADO.NET output/return-value parameters available; it also
        // means any result set that was not read is discarded.
        _owner.CloseReader();

        var outputs = new List<ProcedureOutputParameter>();
        object? returnValue = null;

        foreach (DbParameter parameter in _owner.Command.Parameters)
        {
            switch (parameter.Direction)
            {
                case ParameterDirection.ReturnValue:
                    returnValue = Normalize(parameter.Value);
                    break;
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                    outputs.Add(new ProcedureOutputParameter(parameter.ParameterName, Normalize(parameter.Value), parameter.Direction));
                    break;
            }
        }

        _outputs = outputs;
        _returnValue = returnValue;
        _outputsRead = true;
    }

    private static object? Normalize(object? value) => value is DBNull ? null : value;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void ThrowIfReaderClosed()
    {
        if (_outputsRead)
            throw new InvalidOperationException("The reader was closed to read output parameters; result sets can no longer be read.");
    }

    private static InvalidOperationException NoMoreResultSets()
        => new("The command has no further result sets to read. Each Read call advances to the next result set; check the number of sets the command returns before reading again.");
}
