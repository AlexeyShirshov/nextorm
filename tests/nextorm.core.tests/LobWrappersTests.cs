using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Exercises the internal <see cref="LobStream"/> and <see cref="LobTextReader"/> wrappers directly.
/// The integration suite proves they own a real provider reader; here a tracking inner stream/reader
/// pins that every operation delegates, that disposal is idempotent and releases the inner resource,
/// and that every member rejects a disposed instance with <see cref="ObjectDisposedException"/>.
/// </summary>
public class LobWrappersTests
{
    // A null owner is enough to pin the wrapper surface: reading/writing never touches the owner and
    // CommandReaderOwner.Dispose already tolerates a null reader/command. Owner release is covered by
    // the integration LOB tests against a real provider.
    private static CommandReaderOwner NoOwner() => new(null!, null!);

    private sealed class TrackingStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public int DisposeCount { get; private set; }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override int ReadByte() => _inner.ReadByte();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;

            _inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingTextReader(string text) : TextReader
    {
        private readonly StringReader _inner = new(text);

        public int DisposeCount { get; private set; }

        public override int Peek() => _inner.Peek();
        public override int Read() => _inner.Read();
        public override int Read(char[] buffer, int index, int count) => _inner.Read(buffer, index, count);
        public override int Read(Span<char> buffer) => _inner.Read(buffer);
        public override int ReadBlock(char[] buffer, int index, int count) => _inner.ReadBlock(buffer, index, count);
        public override int ReadBlock(Span<char> buffer) => _inner.ReadBlock(buffer);
        public override string? ReadLine() => _inner.ReadLine();
        public override string ReadToEnd() => _inner.ReadToEnd();
        public override Task<int> ReadAsync(char[] buffer, int index, int count) => _inner.ReadAsync(buffer, index, count);
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count) => _inner.ReadBlockAsync(buffer, index, count);
        public override ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadBlockAsync(buffer, cancellationToken);
        public override Task<string?> ReadLineAsync() => _inner.ReadLineAsync();
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => _inner.ReadLineAsync(cancellationToken);
        public override Task<string> ReadToEndAsync() => _inner.ReadToEndAsync();
        public override Task<string> ReadToEndAsync(CancellationToken cancellationToken) => _inner.ReadToEndAsync(cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;

            _inner.Dispose();
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void LobStream_Read_ShouldDelegateToInner()
    {
        using var stream = new LobStream(new TrackingStream([1, 2, 3, 4]), NoOwner());

        var buffer = new byte[4];
        stream.Read(buffer, 0, buffer.Length).Should().Be(4);
        buffer.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void LobStream_ReadSpanAndByte_ShouldDelegateToInner()
    {
        using var stream = new LobStream(new TrackingStream([1, 2, 3]), NoOwner());

        var span = new byte[2];
        stream.Read(span).Should().Be(2);
        span.Should().Equal(1, 2);
        stream.ReadByte().Should().Be(3);
        stream.ReadByte().Should().Be(-1);
    }

    [Fact]
    public void LobStream_PropertiesAndSeek_ShouldDelegateToInner()
    {
        using var stream = new LobStream(new TrackingStream([1, 2, 3, 4]), NoOwner());

        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
        stream.Length.Should().Be(4);
        stream.Position.Should().Be(0);

        stream.Position = 2;
        stream.Position.Should().Be(2);
        stream.Seek(1, SeekOrigin.Begin).Should().Be(1);
        stream.Seek(-1, SeekOrigin.End).Should().Be(3);

        stream.Flush();
    }

    [Fact]
    public async Task LobStream_ReadAsync_ShouldDelegateToInner()
    {
        await using var stream = new LobStream(new TrackingStream([1, 2, 3, 4]), NoOwner());

        var buffer = new byte[2];
        (await stream.ReadAsync(buffer, 0, buffer.Length, TestContext.Current.CancellationToken)).Should().Be(2);
        buffer.Should().Equal(1, 2);

        var memory = new byte[2];
        (await stream.ReadAsync(memory.AsMemory(), TestContext.Current.CancellationToken)).Should().Be(2);
        memory.Should().Equal(3, 4);
    }

    [Fact]
    public void LobStream_WriteAndSetLength_ShouldThrowNotSupported()
    {
        using var stream = new LobStream(new TrackingStream([1]), NoOwner());

        var write = () => stream.Write(new byte[1], 0, 1);
        write.Should().Throw<NotSupportedException>();

        var setLength = () => stream.SetLength(1);
        setLength.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void LobStream_Dispose_ShouldReleaseInnerOnceAndRejectReads()
    {
        var inner = new TrackingStream([1, 2, 3]);
        var stream = new LobStream(inner, NoOwner());

        stream.Dispose();
        stream.Dispose();
        inner.DisposeCount.Should().Be(1);

        stream.CanRead.Should().BeFalse();
        stream.CanSeek.Should().BeFalse();

        FluentActions.Invoking(() => stream.Read(new byte[1], 0, 1)).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => stream.Read(new byte[1].AsSpan())).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => stream.ReadByte()).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => _ = stream.Length).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => _ = stream.Position).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => stream.Position = 0).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => stream.Seek(0, SeekOrigin.Begin)).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => stream.Flush()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task LobStream_DisposeAsync_ShouldReleaseInnerOnceAndRejectAsyncReads()
    {
        var inner = new TrackingStream([1, 2, 3]);
        var stream = new LobStream(inner, NoOwner());

        await stream.DisposeAsync();
        await stream.DisposeAsync();
        inner.DisposeCount.Should().Be(1);

        var read = async () => await stream.ReadAsync(new byte[1], 0, 1);
        await read.Should().ThrowAsync<ObjectDisposedException>();

        var readMemory = async () => await stream.ReadAsync(new byte[1].AsMemory());
        await readMemory.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void LobTextReader_PeekAndRead_ShouldDelegateToInner()
    {
        using var reader = new LobTextReader(new TrackingTextReader("abc"), NoOwner());

        reader.Peek().Should().Be('a');
        reader.Read().Should().Be('a');

        var buffer = new char[2];
        reader.Read(buffer, 0, buffer.Length).Should().Be(2);
        buffer.Should().Equal('b', 'c');
    }

    [Fact]
    public void LobTextReader_ReadSpanAndBlock_ShouldDelegateToInner()
    {
        using var reader = new LobTextReader(new TrackingTextReader("abcd"), NoOwner());

        var span = new char[2];
        reader.Read(span).Should().Be(2);
        span.Should().Equal('a', 'b');

        var block = new char[2];
        reader.ReadBlock(block, 0, block.Length).Should().Be(2);
        block.Should().Equal('c', 'd');

        var blockSpan = new char[0];
        reader.ReadBlock(blockSpan).Should().Be(0);
    }

    [Fact]
    public void LobTextReader_ReadLineAndToEnd_ShouldDelegateToInner()
    {
        using var reader = new LobTextReader(new TrackingTextReader("first\nsecond"), NoOwner());

        reader.ReadLine().Should().Be("first");
        reader.ReadToEnd().Should().Be("second");
    }

    [Fact]
    public async Task LobTextReader_ReadAsync_ShouldDelegateToInner()
    {
        await using var reader = new LobTextReader(new TrackingTextReader("abcd"), NoOwner());

        var buffer = new char[2];
        (await reader.ReadAsync(buffer, 0, buffer.Length)).Should().Be(2);
        buffer.Should().Equal('a', 'b');

        var memory = new char[2];
        (await reader.ReadAsync(memory.AsMemory(), TestContext.Current.CancellationToken)).Should().Be(2);
        memory.Should().Equal('c', 'd');
    }

    [Fact]
    public async Task LobTextReader_AsyncBlocksLinesAndEnd_ShouldDelegateToInner()
    {
        await using var reader = new LobTextReader(new TrackingTextReader("a\nb"), NoOwner());

        (await reader.ReadLineAsync(TestContext.Current.CancellationToken)).Should().Be("a");
        (await reader.ReadLineAsync(CancellationToken.None)).Should().Be("b");
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await reader.ReadToEndAsync(CancellationToken.None)).Should().BeEmpty();

        await using var blockReader = new LobTextReader(new TrackingTextReader("xyz"), NoOwner());
        var buffer = new char[3];
        (await blockReader.ReadBlockAsync(buffer, 0, buffer.Length)).Should().Be(3);
        buffer.Should().Equal('x', 'y', 'z');

        var empty = new char[0];
        (await blockReader.ReadBlockAsync(empty.AsMemory(), TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public void LobTextReader_Dispose_ShouldReleaseInnerOnceAndRejectReads()
    {
        var inner = new TrackingTextReader("abc");
        var reader = new LobTextReader(inner, NoOwner());

        reader.Dispose();
        reader.Dispose();
        inner.DisposeCount.Should().Be(1);

        FluentActions.Invoking(() => reader.Peek()).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => reader.Read()).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => reader.Read(new char[1], 0, 1)).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => reader.Read(new char[1].AsSpan())).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => reader.ReadBlock(new char[1], 0, 1)).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => reader.ReadBlock(new char[1].AsSpan())).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => reader.ReadLine()).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => reader.ReadToEnd()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task LobTextReader_DisposeAsync_ShouldReleaseInnerOnceAndRejectAsyncReads()
    {
        var inner = new TrackingTextReader("abc");
        var reader = new LobTextReader(inner, NoOwner());

        await reader.DisposeAsync();
        await reader.DisposeAsync();
        inner.DisposeCount.Should().Be(1);

        var read = async () => await reader.ReadAsync(new char[1], 0, 1);
        await read.Should().ThrowAsync<ObjectDisposedException>();

        var readMemory = async () => await reader.ReadAsync(new char[1].AsMemory());
        await readMemory.Should().ThrowAsync<ObjectDisposedException>();

        var readBlock = async () => await reader.ReadBlockAsync(new char[1], 0, 1);
        await readBlock.Should().ThrowAsync<ObjectDisposedException>();

        var readBlockMemory = async () => await reader.ReadBlockAsync(new char[1].AsMemory());
        await readBlockMemory.Should().ThrowAsync<ObjectDisposedException>();

        var readLine = async () => await reader.ReadLineAsync();
        await readLine.Should().ThrowAsync<ObjectDisposedException>();

        var readLineToken = async () => await reader.ReadLineAsync(CancellationToken.None);
        await readLineToken.Should().ThrowAsync<ObjectDisposedException>();

        var readToEnd = async () => await reader.ReadToEndAsync();
        await readToEnd.Should().ThrowAsync<ObjectDisposedException>();

        var readToEndToken = async () => await reader.ReadToEndAsync(CancellationToken.None);
        await readToEndToken.Should().ThrowAsync<ObjectDisposedException>();
    }
}
