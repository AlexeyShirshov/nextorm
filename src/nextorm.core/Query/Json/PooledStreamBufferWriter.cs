using System.Buffers;

namespace NextORM.Core;

/// <summary>
/// A bounded <see cref="IBufferWriter{T}"/> over a caller-owned destination <see cref="Stream"/>,
/// backed by an <see cref="ArrayPool{T}"/> buffer. Data is flushed to the stream when the buffer
/// fills (or explicitly), so live memory stays O(buffer) regardless of the result-set size. The
/// destination stream is never closed; <see cref="Dispose"/> only returns the rented buffer.
/// </summary>
internal sealed class PooledStreamBufferWriter : IBufferWriter<byte>, IDisposable
{
    /// <summary>The default buffer size (64 KiB).</summary>
    internal const int DefaultBufferSize = 64 * 1024;

    private readonly Stream _destination;
    private byte[]? _buffer;
    private int _written;
    private bool _disposed;

    /// <summary>Initializes the sink over the destination stream.</summary>
    /// <param name="destination">The stream to flush into; ownership stays with the caller.</param>
    /// <param name="bufferSize">The requested buffer size in bytes (defaults to 64 KiB).</param>
    public PooledStreamBufferWriter(Stream destination, int bufferSize = DefaultBufferSize)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        _destination = destination;
        _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    }

    /// <summary>The number of bytes currently buffered and not yet written to the destination.</summary>
    public int WrittenCount => _written;

    /// <summary>
    /// When <see langword="true"/>, a full buffer is grown instead of flushed synchronously, so the
    /// async caller can flush it with <see cref="FlushAsync"/> and observe its cancellation token. The
    /// synchronous path leaves this <see langword="false"/>.
    /// </summary>
    public bool DeferFlush { get; set; }

    /// <inheritdoc/>
    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var buffer = _buffer!;
        if (_written + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        _written += count;
        if (_written == buffer.Length)
        {
            if (DeferFlush)
                Grow(buffer.Length + 1);
            else
                WritePending();
        }
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer!.AsMemory(_written);
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer.AsSpan(_written);
    }

    /// <summary>Appends raw bytes (for example an NDJSON newline) to the buffered output.</summary>
    /// <param name="source">The bytes to append.</param>
    public void Write(ReadOnlySpan<byte> source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (!source.IsEmpty)
        {
            Ensure(source.Length);
            var span = _buffer!.AsSpan(_written);
            var take = Math.Min(span.Length, source.Length);
            source[..take].CopyTo(span);
            _written += take;
            source = source[take..];

            if (_written == _buffer!.Length)
            {
                if (DeferFlush)
                    Grow(_buffer.Length + 1);
                else
                    WritePending();
            }
        }
    }

    /// <summary>Flushes the buffered bytes to the destination synchronously.</summary>
    public void Flush() => WritePending();

    /// <summary>Flushes the buffered bytes to the destination asynchronously.</summary>
    /// <param name="cancellationToken">A token observed while writing.</param>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_written > 0)
        {
            await _destination.WriteAsync(_buffer!.AsMemory(0, _written), cancellationToken).ConfigureAwait(false);
            _written = 0;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        var buffer = _buffer;
        _buffer = null;
        if (buffer is not null)
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }

    private void Ensure(int sizeHint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        if (sizeHint <= 0)
            sizeHint = 1;

        if (_buffer!.Length - _written < sizeHint)
        {
            // The async path defers the destination write to FlushAsync so the cancellation token is
            // honoured; it grows the buffer instead of blocking here. A single request larger than the
            // rented buffer (unlikely: Utf8JsonWriter asks for small chunks) is satisfied by growing.
            if (DeferFlush || sizeHint > _buffer.Length)
                Grow(_written + Math.Max(sizeHint, _buffer.Length + 1));
            else
                WritePending();
        }
    }

    // Rents a larger buffer and copies the pending bytes into it; the old block is returned cleared.
    private void Grow(int minCapacity)
    {
        var current = _buffer!;
        var grown = ArrayPool<byte>.Shared.Rent(minCapacity);
        current.AsSpan(0, _written).CopyTo(grown);
        ArrayPool<byte>.Shared.Return(current, clearArray: true);
        _buffer = grown;
    }

    private void WritePending()
    {
        if (_written <= 0)
            return;

        _destination.Write(_buffer!, 0, _written);
        _written = 0;
    }
}
