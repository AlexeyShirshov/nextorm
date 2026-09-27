namespace NextORM.Core;

/// <summary>
/// A read-only <see cref="TextReader"/> over a single LOB column. It owns the provider reader and the
/// <see cref="CommandReaderOwner"/> (the <c>DbDataReader</c> and the per-call <c>DbCommand</c>);
/// disposing the reader releases both. The context and its connection are never closed here.
/// </summary>
internal sealed class LobTextReader : TextReader, IAsyncDisposable
{
    private readonly TextReader _inner;
    private CommandReaderOwner? _owner;
    private bool _disposed;

    internal LobTextReader(TextReader inner, CommandReaderOwner owner)
    {
        _inner = inner;
        _owner = owner;
    }

    /// <inheritdoc/>
    public override int Peek()
    {
        ThrowIfDisposed();
        return _inner.Peek();
    }

    /// <inheritdoc/>
    public override int Read()
    {
        ThrowIfDisposed();
        return _inner.Read();
    }

    /// <inheritdoc/>
    public override int Read(char[] buffer, int index, int count)
    {
        ThrowIfDisposed();
        return _inner.Read(buffer, index, count);
    }

    /// <inheritdoc/>
    public override int Read(Span<char> buffer)
    {
        ThrowIfDisposed();
        return _inner.Read(buffer);
    }

    /// <inheritdoc/>
    public override int ReadBlock(char[] buffer, int index, int count)
    {
        ThrowIfDisposed();
        return _inner.ReadBlock(buffer, index, count);
    }

    /// <inheritdoc/>
    public override int ReadBlock(Span<char> buffer)
    {
        ThrowIfDisposed();
        return _inner.ReadBlock(buffer);
    }

    /// <inheritdoc/>
    public override string? ReadLine()
    {
        ThrowIfDisposed();
        return _inner.ReadLine();
    }

    /// <inheritdoc/>
    public override string ReadToEnd()
    {
        ThrowIfDisposed();
        return _inner.ReadToEnd();
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(char[] buffer, int index, int count)
    {
        ThrowIfDisposed();
        return _inner.ReadAsync(buffer, index, count);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _inner.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
    {
        ThrowIfDisposed();
        return _inner.ReadBlockAsync(buffer, index, count);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _inner.ReadBlockAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<string?> ReadLineAsync()
    {
        ThrowIfDisposed();
        return _inner.ReadLineAsync();
    }

    /// <inheritdoc/>
    public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _inner.ReadLineAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<string> ReadToEndAsync()
    {
        ThrowIfDisposed();
        return _inner.ReadToEndAsync();
    }

    /// <inheritdoc/>
    public override Task<string> ReadToEndAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _inner.ReadToEndAsync(cancellationToken);
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

    /// <summary>Asynchronously disposes the provider reader and the per-call command.</summary>
    /// <returns>A value task that completes once both have been released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _inner.Dispose();
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
