using System.Data;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D179 managed-seam fault coverage for the internal JSON stream writer: the writer owns the container
/// framing and flushes each row into a caller-owned destination. These tests drive it with a controlled
/// <see cref="IDataRecord"/> and a recording/throwing destination to pin the route-independent, fail-stop
/// policy — the original error propagates, no recovery/rollback tail is written, the destination stays
/// open and the library never adds a <c>Stream.Flush</c>. The public SQLite-terminal cells live in
/// <c>JsonStreamingTests</c> and the SQL Server native cells in <c>JsonNativeStreamTests</c>.
/// </summary>
public class JsonStreamWriterTests
{
    private sealed class FakeRecord : IDataRecord
    {
        private readonly object?[] _values;
        private readonly Type[] _types;
        private readonly int _throwOnOrdinal;

        public FakeRecord(object?[] values, Type[] types, int throwOnOrdinal = -1)
        {
            _values = values;
            _types = types;
            _throwOnOrdinal = throwOnOrdinal;
        }

        public int FieldCount => _values.Length;
        public object this[int i] => GetValue(i);
        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => (bool)_values[i]!;
        public byte GetByte(int i) => (byte)_values[i]!;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => _types[i].Name;
        public DateTime GetDateTime(int i) => (DateTime)_values[i]!;
        public decimal GetDecimal(int i) => (decimal)_values[i]!;
        public double GetDouble(int i) => (double)_values[i]!;
        public Type GetFieldType(int i) => _types[i];
        public float GetFloat(int i) => (float)_values[i]!;
        public Guid GetGuid(int i) => (Guid)_values[i]!;
        public short GetInt16(int i) => (short)_values[i]!;

        public int GetInt32(int i)
        {
            if (i == _throwOnOrdinal)
                throw new InvalidOperationException("reader failed mid-read");
            return (int)_values[i]!;
        }

        public long GetInt64(int i) => (long)_values[i]!;
        public string GetName(int i) => $"c{i}";
        public int GetOrdinal(string name) => throw new NotSupportedException();
        public string GetString(int i) => (string)_values[i]!;
        public object GetValue(int i) => _values[i] ?? DBNull.Value;

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _values.Length);
            for (var i = 0; i < count; i++)
                values[i] = GetValue(i);
            return count;
        }

        public bool IsDBNull(int i) => _values[i] is null;
    }

    // A destination that records bytes, counts Write/Flush calls and can fail on a chosen write call.
    private sealed class DestinationSpy : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly int _failAtWriteCall;

        public DestinationSpy(int failAtWriteCall = -1) => _failAtWriteCall = failAtWriteCall;

        public int WriteCalls { get; private set; }
        public int FlushCalls { get; private set; }
        public bool IsDisposed { get; private set; }
        public string Text => Encoding.UTF8.GetString(_inner.ToArray());

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !IsDisposed;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => FlushCalls++;
        public override Task FlushAsync(CancellationToken cancellationToken) { FlushCalls++; return Task.CompletedTask; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteCalls++;
            if (WriteCalls == _failAtWriteCall)
                throw new InvalidOperationException("destination failed");
            _inner.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCalls++;
            if (WriteCalls == _failAtWriteCall)
                throw new InvalidOperationException("destination failed");
            _inner.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    // A destination that cancels a token after its first (async) write, then throws from every later
    // write because a well-behaved async Stream observes the cancellation token it is handed.
    private sealed class CancellingDestination : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly CancellationTokenSource _cts;

        public CancellingDestination(CancellationTokenSource cts) => _cts = cts;

        public bool IsDisposed { get; private set; }
        public string Text => Encoding.UTF8.GetString(_inner.ToArray());

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !IsDisposed;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            _cts.Cancel();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _inner.Write(buffer.Span);
            _cts.Cancel();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private static JsonRowWriter BuildRowWriter(JsonStreamOptions options)
    {
        var plan = JsonShapePlan.Build(
            [new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" }],
            oneColumn: false,
            options);
        return JsonRowWriterFactory.Build(plan);
    }

    [Fact]
    public void WriteRow_SyncMidRowReadFailure_ShouldPropagateWithoutRecoveryTail()
    {
        var options = new JsonStreamOptions();
        using var destination = new DestinationSpy();
        using var writer = new JsonStreamWriter(destination, BuildRowWriter(options), options);

        writer.WriteRow(new FakeRecord([1], [typeof(int)]));

        var act = () => writer.WriteRow(new FakeRecord([2], [typeof(int)], throwOnOrdinal: 0));

        act.Should().Throw<InvalidOperationException>().WithMessage("reader failed mid-read");
        // The prefix flushed by the first row stays in place: fail-stop, no rollback and no closing bracket.
        destination.Text.Should().Be("[{\"Id\":1}");
        destination.Text.Should().NotContain("]");
        destination.FlushCalls.Should().Be(0);
        destination.CanWrite.Should().BeTrue();
        destination.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void WriteRow_NdJsonMidRowReadFailure_ShouldPropagate()
    {
        var options = new JsonStreamOptions { Mode = JsonStreamMode.NdJson };
        using var destination = new DestinationSpy();
        using var writer = new JsonStreamWriter(destination, BuildRowWriter(options), options);

        writer.WriteRow(new FakeRecord([1], [typeof(int)]));

        var act = () => writer.WriteRow(new FakeRecord([2], [typeof(int)], throwOnOrdinal: 0));

        act.Should().Throw<InvalidOperationException>().WithMessage("reader failed mid-read");
        destination.Text.Should().Be("{\"Id\":1}\n");
        destination.FlushCalls.Should().Be(0);
        destination.CanWrite.Should().BeTrue();
    }

    [Fact]
    public void WriteRow_SyncSinkFailure_ShouldKeepDestinationOpenAndPrefix()
    {
        var options = new JsonStreamOptions();
        using var destination = new DestinationSpy(failAtWriteCall: 2);
        using var writer = new JsonStreamWriter(destination, BuildRowWriter(options), options);

        writer.WriteRow(new FakeRecord([1], [typeof(int)]));

        var act = () => writer.WriteRow(new FakeRecord([2], [typeof(int)]));

        act.Should().Throw<InvalidOperationException>().WithMessage("destination failed");
        destination.Text.Should().Be("[{\"Id\":1}");
        destination.FlushCalls.Should().Be(0);
        destination.CanWrite.Should().BeTrue();
        destination.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task WriteRowAsync_MidRowReadFailure_ShouldPropagate()
    {
        var options = new JsonStreamOptions();
        using var destination = new DestinationSpy();
        using var writer = new JsonStreamWriter(destination, BuildRowWriter(options), options);

        await writer.WriteRowAsync(new FakeRecord([1], [typeof(int)]), TestContext.Current.CancellationToken);

        var act = async () => await writer.WriteRowAsync(new FakeRecord([2], [typeof(int)], throwOnOrdinal: 0), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("reader failed mid-read");
        destination.Text.Should().Be("[{\"Id\":1}");
        destination.FlushCalls.Should().Be(0);
        destination.CanWrite.Should().BeTrue();
    }

    [Fact]
    public async Task WriteRowAsync_SinkFailure_ShouldKeepDestinationOpen()
    {
        var options = new JsonStreamOptions();
        using var destination = new DestinationSpy(failAtWriteCall: 2);
        using var writer = new JsonStreamWriter(destination, BuildRowWriter(options), options);

        await writer.WriteRowAsync(new FakeRecord([1], [typeof(int)]), TestContext.Current.CancellationToken);

        var act = async () => await writer.WriteRowAsync(new FakeRecord([2], [typeof(int)]), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("destination failed");
        destination.Text.Should().Be("[{\"Id\":1}");
        destination.FlushCalls.Should().Be(0);
        destination.CanWrite.Should().BeTrue();
        destination.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task WriteRowAsync_CancellationAfterPrefix_ShouldAbortAndKeepPrefix()
    {
        var options = new JsonStreamOptions();
        using var cts = new CancellationTokenSource();
        using var destination = new CancellingDestination(cts);
        using var writer = new JsonStreamWriter(destination, BuildRowWriter(options), options);

        await writer.WriteRowAsync(new FakeRecord([1], [typeof(int)]), cts.Token);

        var act = async () => await writer.WriteRowAsync(new FakeRecord([2], [typeof(int)]), cts.Token);

        var exception = await act.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.CancellationToken.Should().Be(cts.Token);
        destination.Text.Should().Be("[{\"Id\":1}");
        destination.CanWrite.Should().BeTrue();
        destination.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task WriteRowAsync_NdJsonCancellationAfterPrefix_ShouldAbort()
    {
        var options = new JsonStreamOptions { Mode = JsonStreamMode.NdJson };
        using var cts = new CancellationTokenSource();
        using var destination = new CancellingDestination(cts);
        using var writer = new JsonStreamWriter(destination, BuildRowWriter(options), options);

        await writer.WriteRowAsync(new FakeRecord([1], [typeof(int)]), cts.Token);

        var act = async () => await writer.WriteRowAsync(new FakeRecord([2], [typeof(int)]), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        destination.Text.Should().Be("{\"Id\":1}\n");
        destination.CanWrite.Should().BeTrue();
    }
}
