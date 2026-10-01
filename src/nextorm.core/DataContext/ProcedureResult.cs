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
    private bool _legacyReadUsed;
    private bool _setsStarted;
    private bool _setsEnumerated;
    private bool _traversalActive;
    private bool _currentCursorConsumed;
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
    /// Enumerates the column-bearing result sets of the command in order, yielding a <see cref="ResultSet"/>
    /// cursor for each. Leading, intermediate and trailing result sets without columns (DDL/DML) are
    /// skipped and do not count toward <see cref="ResultSet.Index"/>; a set with columns but no rows is
    /// still yielded. Each set can be consumed once, eagerly with <see cref="ResultSet.Read{T}"/> or lazily
    /// with <see cref="ResultSet.ReadAsync{T}"/>.
    /// </summary>
    /// <returns>A one-shot, forward-only sequence of result-set cursors; empty when there is none.</returns>
    /// <remarks>
    /// The outer iteration is one-shot and must not be mixed with the legacy <see cref="Read{T}"/> /
    /// <see cref="ReadAsync{T}"/> sequence or with <see cref="OutputParameters"/>/<see cref="ReturnValue"/>
    /// while it is active. Disposing the outer enumerator invalidates its cursors but does not dispose this
    /// result or its reader.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Result sets were already traversed or read another way.</exception>
    /// <exception cref="ObjectDisposedException">The result has been disposed.</exception>
    public IEnumerable<ResultSet> ReadSets()
    {
        ThrowIfDisposed();
        ThrowIfReaderClosed();
        BeginSetTraversal();
        return ReadSetsCore();
    }

    /// <summary>
    /// Asynchronously enumerates the column-bearing result sets of the command in order, yielding a
    /// <see cref="ResultSet"/> cursor for each. See <see cref="ReadSets"/> for the traversal semantics.
    /// </summary>
    /// <param name="cancellationToken">Cancels the traversal (including a slow provider move).</param>
    /// <returns>A one-shot, forward-only async sequence of result-set cursors; empty when there is none.</returns>
    /// <exception cref="InvalidOperationException">Result sets were already traversed or read another way.</exception>
    /// <exception cref="ObjectDisposedException">The result has been disposed.</exception>
    public IAsyncEnumerable<ResultSet> ReadSetsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfReaderClosed();
        BeginSetTraversal();
        return ReadSetsAsyncCore(cancellationToken);
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
        ThrowIfSetTraversalStarted();
        _legacyReadUsed = true;

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
        ThrowIfSetTraversalStarted();
        _legacyReadUsed = true;

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
        _traversalActive = false;
        _owner.Dispose();
    }

    /// <summary>Asynchronously releases the reader and the command. Safe to call more than once.</summary>
    /// <returns>A value task that completes once the reader and command are disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _traversalActive = false;
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

        if (_traversalActive)
            throw new InvalidOperationException("Output parameters cannot be read while result sets are being enumerated; finish or dispose the traversal first.");

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

    private IEnumerable<ResultSet> ReadSetsCore()
    {
        if (_setsEnumerated)
            throw AlreadyEnumerated();

        _setsEnumerated = true;
        // Outputs may have drained the reader between ReadSets() and the first MoveNext; the already
        // returned sequence must not resurrect it.
        ThrowIfReaderClosed();
        // Activation happens on the first MoveNext, not when ReadSets() was called, so an
        // un-enumerated sequence does not block OutputParameters/ReturnValue.
        _traversalActive = true;

        var reader = _owner.Reader;
        try
        {
            ResultSetNavigator.AdvanceToResultSet(reader);
            if (reader.FieldCount == 0)
                yield break;

            var index = 0;
            while (true)
            {
                _resultSetIndex = index;
                _currentCursorConsumed = false;
                yield return new ResultSet(this, index, SnapshotColumns(reader));

                InvalidateCurrentCursor();
                if (!ResultSetNavigator.MoveToNextResultSet(reader))
                    yield break;

                index++;
            }
        }
        finally
        {
            InvalidateCurrentCursor();
            _traversalActive = false;
        }
    }

    private async IAsyncEnumerable<ResultSet> ReadSetsAsyncCore([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_setsEnumerated)
            throw AlreadyEnumerated();

        _setsEnumerated = true;
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfReaderClosed();
        // See ReadSetsCore: activation is deferred to the first MoveNextAsync.
        _traversalActive = true;

        var reader = _owner.Reader;
        try
        {
            await ResultSetNavigator.AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);
            if (reader.FieldCount == 0)
                yield break;

            var index = 0;
            while (true)
            {
                _resultSetIndex = index;
                _currentCursorConsumed = false;
                yield return new ResultSet(this, index, SnapshotColumns(reader));

                InvalidateCurrentCursor();
                if (!await ResultSetNavigator.MoveToNextResultSetAsync(reader, cancellationToken).ConfigureAwait(false))
                    yield break;

                index++;
            }
        }
        finally
        {
            InvalidateCurrentCursor();
            _traversalActive = false;
        }
    }

    internal IReadOnlyList<T> ReadCurrentSet<T>(ResultSet cursor)
    {
        var reader = BeginCursorRead(cursor);
        // Advance the cursor before mapping/building the mapper, mirroring Read<T>: a mapping failure
        // must not make the set re-readable.
        var mapper = RawMapperFactory.GetOrBuild<T>(_context, reader);

        var list = new List<T>();
        while (reader.Read())
            list.Add(mapper(reader));

        return list;
    }

    internal async IAsyncEnumerable<T> ReadCurrentSetAsync<T>(ResultSet cursor, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reader = BeginCursorRead(cursor);
        // See ReadCurrentSet<T>: consume the set before building the mapper.
        var mapper = RawMapperFactory.GetOrBuild<T>(_context, reader);

        // The cursor is spent from BeginCursorRead on (the consumed flag stays set until the outer
        // traversal advances), so a stale copy cannot re-read the set. Deliberately do NOT invalidate
        // here: an inner read that finishes after the outer advanced would otherwise zero the owner's
        // current-set index and impersonate that advance.
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return mapper(reader);
    }

    private DbDataReader BeginCursorRead(ResultSet cursor)
    {
        ThrowIfDisposed();
        ThrowIfReaderClosed();

        if (!cursor.IsInitialized
            || !ReferenceEquals(cursor.Owner, this)
            || !_traversalActive
            || cursor.Index != _resultSetIndex)
        {
            throw new InvalidOperationException("The result-set cursor is stale: the outer traversal has advanced or ended, and a cursor is valid only while its set is current.");
        }

        if (_currentCursorConsumed)
            throw new InvalidOperationException("This result set has already been read; each set can be read once, either eagerly or lazily.");

        _currentCursorConsumed = true;
        return _owner.Reader;
    }

    private static string[] SnapshotColumns(DbDataReader reader)
    {
        var names = new string[reader.FieldCount];
        for (var i = 0; i < names.Length; i++)
            names[i] = reader.GetName(i);

        return names;
    }

    private void InvalidateCurrentCursor()
    {
        _resultSetIndex = -1;
        _currentCursorConsumed = false;
    }

    private void BeginSetTraversal()
    {
        if (_legacyReadUsed)
            throw new InvalidOperationException("Result sets were already read with Read<T>/ReadAsync<T>; a command supports a single traversal style.");

        if (_setsStarted)
            throw new InvalidOperationException("Result sets were already enumerated; each command supports a single traversal.");

        // The one-shot/mode guard is claimed eagerly so mixing legacy Read<T> is rejected even before
        // the sequence is enumerated; activity itself starts on the first MoveNext (see the cores).
        _setsStarted = true;
    }

    private void ThrowIfSetTraversalStarted()
    {
        if (_setsStarted)
            throw new InvalidOperationException("Result sets are being read with ReadSets/ReadSetsAsync; Read<T>/ReadAsync<T> cannot be mixed with that traversal.");
    }

    private static InvalidOperationException AlreadyEnumerated()
        => new("This result-set enumeration has already been consumed; each command supports a single traversal.");

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
