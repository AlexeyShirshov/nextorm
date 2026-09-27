using System.Collections;
using System.Data;
using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// A forward-only <see cref="DbDataReader"/> over a multi-column LOB projection. It owns the inner
/// provider reader and the <see cref="CommandReaderOwner"/> (the per-call <c>DbCommand</c>);
/// disposing or closing the reader releases both. The context and its connection are never closed
/// here, so the caller must keep the context alive until the reader is disposed.
/// </summary>
/// <remarks>
/// The reader exposes the provider's sequential-access ordering: columns must be read in ascending
/// ordinal order and a LOB column must not be read twice or after a later column. Access is
/// forward-only; <see cref="Read"/> and <see cref="NextResult"/> advance the reader irreversibly.
/// </remarks>
internal sealed class LobDataReader : DbDataReader
{
    private readonly DbDataReader _inner;
    private readonly CancellationToken _cancellationToken;
    private CommandReaderOwner? _owner;
    private bool _disposed;

    internal LobDataReader(CommandReaderOwner owner, CancellationToken cancellationToken)
    {
        _inner = owner.Reader;
        _cancellationToken = cancellationToken;
        _owner = owner;
    }

    /// <inheritdoc/>
    public override int FieldCount
    {
        get
        {
            ThrowIfDisposed();
            return _inner.FieldCount;
        }
    }

    /// <inheritdoc/>
    public override int Depth
    {
        get
        {
            ThrowIfDisposed();
            return _inner.Depth;
        }
    }

    /// <inheritdoc/>
    public override bool HasRows
    {
        get
        {
            ThrowIfDisposed();
            return _inner.HasRows;
        }
    }

    /// <inheritdoc/>
    public override bool IsClosed => _inner.IsClosed;

    /// <inheritdoc/>
    public override int RecordsAffected
    {
        get
        {
            ThrowIfDisposed();
            return _inner.RecordsAffected;
        }
    }

    /// <inheritdoc/>
    public override object this[int ordinal] => GetValue(ordinal);

    /// <inheritdoc/>
    public override object this[string name] => GetValue(GetOrdinal(name));

    /// <inheritdoc/>
    public override bool Read()
    {
        ThrowIfDisposed();
        _cancellationToken.ThrowIfCancellationRequested();
        return _inner.Read();
    }

    /// <inheritdoc/>
    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!_cancellationToken.CanBeCanceled)
            return await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);
        return await _inner.ReadAsync(linked.Token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override bool NextResult()
    {
        ThrowIfDisposed();
        _cancellationToken.ThrowIfCancellationRequested();
        return _inner.NextResult();
    }

    /// <inheritdoc/>
    public override async Task<bool> NextResultAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!_cancellationToken.CanBeCanceled)
            return await _inner.NextResultAsync(cancellationToken).ConfigureAwait(false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);
        return await _inner.NextResultAsync(linked.Token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Close()
    {
        if (_disposed)
            return;

        Dispose(true);
    }

    /// <inheritdoc/>
    public override string GetName(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetName(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override int GetOrdinal(string name)
    {
        ThrowIfDisposed();
        return _inner.GetOrdinal(name);
    }

    /// <inheritdoc/>
    public override Type GetFieldType(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetFieldType(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override string GetDataTypeName(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetDataTypeName(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override object GetValue(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetValue(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override int GetValues(object[] values)
    {
        ThrowIfDisposed();
        return _inner.GetValues(values);
    }

    /// <inheritdoc/>
    public override bool IsDBNull(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.IsDBNull(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _inner.IsDBNullAsync(CheckOrdinal(ordinal), cancellationToken);
    }

    /// <inheritdoc/>
    public override bool GetBoolean(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetBoolean(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override byte GetByte(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetByte(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        ThrowIfDisposed();
        return _inner.GetBytes(CheckOrdinal(ordinal), dataOffset, buffer, bufferOffset, length);
    }

    /// <inheritdoc/>
    public override char GetChar(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetChar(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        ThrowIfDisposed();
        return _inner.GetChars(CheckOrdinal(ordinal), dataOffset, buffer, bufferOffset, length);
    }

    /// <inheritdoc/>
    public override DateTime GetDateTime(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetDateTime(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override decimal GetDecimal(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetDecimal(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override double GetDouble(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetDouble(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override float GetFloat(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetFloat(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override Guid GetGuid(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetGuid(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override short GetInt16(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetInt16(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override int GetInt32(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetInt32(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override long GetInt64(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetInt64(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override string GetString(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetString(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override Stream GetStream(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetStream(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override TextReader GetTextReader(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetTextReader(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override T GetFieldValue<T>(int ordinal)
    {
        ThrowIfDisposed();
        return _inner.GetFieldValue<T>(CheckOrdinal(ordinal));
    }

    /// <inheritdoc/>
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _inner.GetFieldValueAsync<T>(CheckOrdinal(ordinal), cancellationToken);
    }

    /// <inheritdoc/>
    public override IEnumerator GetEnumerator()
    {
        ThrowIfDisposed();
        return _inner.GetEnumerator();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                _inner.Dispose();
            }
            finally
            {
                ReleaseOwner();
            }

            GC.SuppressFinalize(this);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            var owner = _owner;
            _owner = null;
            if (owner is not null)
                await owner.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    private int CheckOrdinal(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
            throw new IndexOutOfRangeException();
        return ordinal;
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private void ReleaseOwner()
    {
        var owner = _owner;
        _owner = null;
        owner?.Dispose();
    }
}
