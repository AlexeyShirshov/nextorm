namespace NextORM.Core;

/// <summary>
/// A read-only <see cref="Stream"/> over a single LOB column. It owns the provider stream and the
/// <see cref="CommandReaderOwner"/> (the <c>DbDataReader</c> and the per-call <c>DbCommand</c>);
/// disposing the stream releases both. The context and its connection are never closed here.
/// </summary>
internal sealed class LobStream : Stream
{
    private readonly Stream _inner;
    private CommandReaderOwner? _owner;
    private bool _disposed;

    internal LobStream(Stream inner, CommandReaderOwner owner)
    {
        _inner = inner;
        _owner = owner;
    }

    /// <inheritdoc/>
    public override bool CanRead => !_disposed && _inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => !_disposed && _inner.CanSeek;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return _inner.Length;
        }
    }

    /// <inheritdoc/>
    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return _inner.Position;
        }
        set
        {
            ThrowIfDisposed();
            _inner.Position = value;
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        ThrowIfDisposed();
        _inner.Flush();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        return _inner.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        return _inner.Read(buffer);
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        ThrowIfDisposed();
        return _inner.ReadByte();
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _inner.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        return _inner.Seek(offset, origin);
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private void ReleaseOwner()
    {
        var owner = _owner;
        _owner = null;
        owner?.Dispose();
    }
}
