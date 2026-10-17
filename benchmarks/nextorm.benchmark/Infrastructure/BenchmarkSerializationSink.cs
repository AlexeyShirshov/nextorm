namespace NextORM.Benchmark;

/// <summary>
/// A write-only <see cref="Stream"/> that counts the bytes a materialize→serialize arm produces but
/// discards them, so the measured competitor pipeline does not retain the serialized output in a
/// growing buffer. Reset outside the measured body; the byte count is the consumed result.
/// </summary>
internal sealed class BenchmarkSerializationSink : Stream
{
    /// <summary>Bytes written since the last <see cref="Reset"/>.</summary>
    public long Bytes { get; private set; }

    /// <summary>Clears the byte count without touching any backing storage (there is none).</summary>
    public void Reset() => Bytes = 0;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => Bytes;

    public override long Position
    {
        get => Bytes;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override void Write(byte[] buffer, int offset, int count) => Bytes += count;

    public override void Write(ReadOnlySpan<byte> buffer) => Bytes += buffer.Length;

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Bytes += buffer.Length;
        return default;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
