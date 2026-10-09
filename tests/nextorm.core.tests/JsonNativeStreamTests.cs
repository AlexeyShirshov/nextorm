using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D177 native JSON streaming: the eligibility predicate must admit only the narrow flat SQL Server
/// shape whose null, type, alias and provider semantics match the managed writer, and the bounded pump
/// must transcode a document across row / character / byte-buffer boundaries without managed row
/// serialization. Live SQL Server behaviour is covered by the integration suite.
/// </summary>
public class JsonNativeStreamTests
{
    private enum SampleEnum
    {
        Zero = 0,
        One = 1,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum StringSampleEnum
    {
        Zero = 0,
        One = 1,
    }

    private sealed class DirectProbe
    {
        public long LongProp { get; set; }
        public int IntProp { get; set; }
        public short ShortProp { get; set; }
        public bool BoolProp { get; set; }
        public string? StringProp { get; set; }
    }

    private static JsonShapePlan Plan(bool scalar, JsonStreamOptions options, params SelectExpression[] columns)
    {
        for (var i = 0; i < columns.Length; i++)
            columns[i].Index = i;

        return JsonShapePlan.Build(columns, oneColumn: scalar, options);
    }

    // A direct pass-through column: a mapped member read (the predicate admits only these).
    private static SelectExpression Column(Type type, string name, bool defaultOnNull = false, Type? providerType = null)
        => new(type)
        {
            PropertyName = name,
            DefaultOnNull = defaultOnNull,
            ProviderType = providerType,
            Expression = Expression.Property(Expression.Parameter(typeof(DirectProbe), "p"), nameof(DirectProbe.LongProp)),
        };

    // A raw named-table accessor (TableColumn.AsInt): the storage type is not part of the CLR projection.
    private static SelectExpression RawColumn(Type type, string name)
        => new(type)
        {
            PropertyName = name,
            Expression = Expression.Property(Expression.Parameter(typeof(TableColumn), "c"), nameof(TableColumn.AsInt)),
        };

    [Fact]
    public void FlatAdmittedTypes_ShouldBeEligible()
    {
        var plan = Plan(false, new JsonStreamOptions(),
            Column(typeof(long), "Id"),
            Column(typeof(int?), "Int"),
            Column(typeof(short), "Small"),
            Column(typeof(bool?), "Flag"),
            Column(typeof(string), "Name"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeTrue();
    }

    [Fact]
    public void IgnoreNull_ShouldNotChangeEligibility()
    {
        var plan = Plan(false, new JsonStreamOptions { IgnoreNull = true },
            Column(typeof(int), "Id"),
            Column(typeof(string), "Name"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(byte[]))]
    public void NonAdmittedScalarTypes_ShouldStayManaged(Type type)
    {
        var plan = Plan(false, new JsonStreamOptions(), Column(type, "Value"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void NumericEnum_ShouldStayManaged()
    {
        // A plain enum is classified as a number over its underlying integral type, so DeclaredType is
        // the only signal that keeps it on the managed path.
        var plan = Plan(false, new JsonStreamOptions(),
            new SelectExpression(typeof(SampleEnum)) { PropertyName = "State", Expression = Expression.Property(Expression.Parameter(typeof(DirectProbe), "p"), nameof(DirectProbe.LongProp)) });

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void StringEnum_ShouldStayManaged()
    {
        // A [JsonStringEnumConverter] enum is classified as JsonWriteKind.EnumString (not Number), so it
        // must be excluded by the write-kind switch, independently of the numeric-enum DeclaredType guard.
        var plan = Plan(false, new JsonStreamOptions(),
            new SelectExpression(typeof(StringSampleEnum)) { PropertyName = "State", Expression = Expression.Property(Expression.Parameter(typeof(DirectProbe), "p"), nameof(DirectProbe.LongProp)) });

        plan.Columns[0].Kind.Should().Be(JsonWriteKind.EnumString);
        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void DefaultOnNull_ShouldStayManaged()
    {
        var plan = Plan(false, new JsonStreamOptions(), Column(typeof(int), "Total", defaultOnNull: true));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void Scalar_ShouldStayManaged()
    {
        var plan = Plan(true, new JsonStreamOptions(), Column(typeof(int), "Value"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void NdJson_ShouldStayManaged()
    {
        var plan = Plan(false, new JsonStreamOptions { Mode = JsonStreamMode.NdJson }, Column(typeof(int), "Id"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void Root_ShouldStayManaged()
    {
        var plan = Plan(false, new JsonStreamOptions { Root = "items" }, Column(typeof(int), "Id"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void WriteIndented_ShouldStayManaged()
    {
        var plan = Plan(false, new JsonStreamOptions { WriteIndented = true }, Column(typeof(int), "Id"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void PropertyNamingPolicy_ShouldStayManaged()
    {
        var plan = Plan(false, new JsonStreamOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }, Column(typeof(int), "Id"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void DottedAlias_ShouldStayManaged()
    {
        // FOR JSON PATH treats a dot as a nested path, so it cannot be trusted as a property name.
        var plan = Plan(false, new JsonStreamOptions(), Column(typeof(int), "a.b"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void SpecialCharacterAlias_ShouldStayManaged()
    {
        var plan = Plan(false, new JsonStreamOptions(), Column(typeof(int), "a-b"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    // P1 eligibility defect: keying on the projected CLR type alone admits a wider/different provider
    // binding, whose raw FOR JSON value diverges from the managed narrowing/conversion.
    [Fact]
    public void WiderProviderType_ShouldStayManaged()
    {
        var plan = Plan(false, new JsonStreamOptions(), Column(typeof(int), "Narrow", providerType: typeof(long)));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void SameTypeAndNullProviderType_ShouldStayNative()
    {
        var same = Plan(false, new JsonStreamOptions(), Column(typeof(int), "Narrow", providerType: typeof(int)));
        JsonNativeStream.IsEligible(same, same.Options).Should().BeTrue();

        var none = Plan(false, new JsonStreamOptions(), Column(typeof(long), "Wide"));
        JsonNativeStream.IsEligible(none, none.Options).Should().BeTrue();
    }

    [Fact]
    public void RawTableAccessor_ShouldStayManaged()
    {
        // TableColumn.AsInt over an unknown storage column: the managed reader inspects the runtime field
        // type; native FOR JSON would emit the raw (possibly wider) value.
        var plan = Plan(false, new JsonStreamOptions(), RawColumn(typeof(int), "Narrow"));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Fact]
    public void NestedShape_ShouldStayManaged()
    {
        // A captured recursive shape is reconstructed by managed presence/alias logic, so even a flat
        // FOR JSON document could not reproduce the managed output.
        var columns = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" } };
        var shape = JsonShapeNode.Object(null, typeof(object),
            [JsonShapeNode.Scalar("Id", typeof(int), new JsonShapeBinding(0, nullable: false, defaultOnNull: false))],
            JsonShapePresence.Always, slot: null, member: null);
        var plan = JsonShapePlan.Build(columns, oneColumn: false, new JsonStreamOptions(), shape);

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    [Theory]
    [InlineData("_")]
    [InlineData("a")]
    [InlineData("Z")]
    [InlineData("a0")]
    [InlineData("_9")]
    [InlineData("A_1")]
    public void SimpleAliasBoundary_ShouldStayNative(string name)
    {
        var plan = Plan(false, new JsonStreamOptions(), Column(typeof(int), name));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeTrue();
    }

    [Theory]
    [InlineData("0a")]
    [InlineData("@a")]
    [InlineData("[a")]
    [InlineData("`a")]
    [InlineData("{a")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("a c")]
    [InlineData("a\u00e9")]
    public void NonSimpleAliasBoundary_ShouldStayManaged(string name)
    {
        var plan = Plan(false, new JsonStreamOptions(), Column(typeof(int), name));

        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeFalse();
    }

    // --- Bounded pump: a fake multi-row document reader exercises row / char / byte boundaries. ---

    [Fact]
    public void Pump_SurrogateSplitAcrossRows_ShouldBeReassembled()
    {
        // The high surrogate ends row 0, the low surrogate starts row 1; the encoder must not be flushed
        // between rows.
        var reader = new FakeDocumentReader(["[", "\uD83D", "\uDE00", "]"]);

        Pump(reader, out var text);

        text.Should().Be("[😀]");
    }

    [Fact]
    public void Pump_SurrogateSplitAcrossCharBuffer_ShouldBeReassembled()
    {
        var highPrefix = new string('x', 4095) + "\uD83D";
        var reader = new FakeDocumentReader([highPrefix, "\uDE00end"]);

        Pump(reader, out var text);

        text.Should().Be(new string('x', 4095) + "😀end");
    }

    [Fact]
    public void Pump_DocumentLargerThanByteBuffer_ShouldBeChunked()
    {
        var value = new string('é', 9000);
        var reader = new FakeDocumentReader(["\"", value, "\""]);

        Pump(reader, out var text);

        text.Should().Be("\"" + value + "\"");
    }

    [Fact]
    public void Pump_NullRowAndEmpty_ShouldWriteEmptyArray()
    {
        var nullReader = new FakeDocumentReader([null]);
        Pump(nullReader, out var nullText);
        nullText.Should().Be("[]");

        var emptyReader = new FakeDocumentReader([]);
        Pump(emptyReader, out var emptyText);
        emptyText.Should().Be("[]");
    }

    [Fact]
    public void Pump_UnpairedSurrogate_ShouldWriteReplacement()
    {
        var reader = new FakeDocumentReader(["a\uD83Db"]);

        Pump(reader, out var text);

        text.Should().Be("a\uFFFDb");
    }

    [Fact]
    public async Task Pump_Async_ShouldMatchSync()
    {
        var reader = new FakeDocumentReader(["\"", new string('x', 8192), "\""]);
        using var sync = new MemoryStream();
        JsonNativeStream.WriteDocument(reader, sync);

        var asyncReader = new FakeDocumentReader(["\"", new string('x', 8192), "\""]);
        using var async = new MemoryStream();
        await JsonNativeStream.WriteDocumentAsync(asyncReader, async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
        reader.ManagedReadCount.Should().Be(0, "the native pump must not call any managed row accessor");
    }

    // --- D179 native-route error policy: fail-stop, no recovery tail, destination stays open. ---

    [Fact]
    public void WriteDocument_ReaderFailureMidRead_ShouldPropagateAndKeepPrefix()
    {
        // The first chunk is already copied to the sink when the second GetChars throws: the prefix stays
        // in place, no flush is added and the destination is not disposed.
        var reader = new FakeDocumentReader(["\"", "rest", "\""], throwOnCharsCall: 2);
        using var sink = new RecordingSink();

        var act = () => JsonNativeStream.WriteDocument(reader, sink);

        act.Should().Throw<InvalidOperationException>().WithMessage("native reader failed mid-read");
        sink.Text.Should().Be("\"");
        sink.FlushCalls.Should().Be(0);
        sink.CanWrite.Should().BeTrue();
        sink.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task WriteDocumentAsync_ReaderFailureMidRead_ShouldPropagateAndKeepPrefix()
    {
        var reader = new FakeDocumentReader(["\"", "rest", "\""], throwOnCharsCall: 2);
        using var sink = new RecordingSink();

        var act = async () => await JsonNativeStream.WriteDocumentAsync(reader, sink, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("native reader failed mid-read");
        sink.Text.Should().Be("\"");
        sink.FlushCalls.Should().Be(0);
        sink.CanWrite.Should().BeTrue();
        sink.IsDisposed.Should().BeFalse();
    }

    // D179 native Read() fault: the failure is raised by the next row's Read, so the first row's prefix is
    // already in the destination. This exercises the FakeDocumentReader.throwOnReadCall hook: the original
    // error propagates, no flush is added, no recovery tail is written and the destination stays open.
    [Fact]
    public void WriteDocument_ReaderReadFailureMidPump_ShouldPropagateAndKeepPrefix()
    {
        var reader = new FakeDocumentReader(["\"", "rest", "\""], throwOnReadCall: 2);
        using var sink = new RecordingSink();

        var act = () => JsonNativeStream.WriteDocument(reader, sink);

        act.Should().Throw<InvalidOperationException>().WithMessage("native reader failed mid-read");
        sink.Text.Should().Be("\"");
        sink.FlushCalls.Should().Be(0);
        sink.CanWrite.Should().BeTrue();
        sink.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task WriteDocumentAsync_ReaderReadFailureMidPump_ShouldPropagateAndKeepPrefix()
    {
        var reader = new FakeDocumentReader(["\"", "rest", "\""], throwOnReadCall: 2);
        using var sink = new RecordingSink();

        var act = async () => await JsonNativeStream.WriteDocumentAsync(reader, sink, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("native reader failed mid-read");
        sink.Text.Should().Be("\"");
        sink.FlushCalls.Should().Be(0);
        sink.CanWrite.Should().BeTrue();
        sink.IsDisposed.Should().BeFalse();
    }

    // D179 V08/V11 native cancellation witness: the sink cancels a controllable token after its first
    // successful write, so the pump observes it on the next asynchronous boundary. The cancellation
    // propagates with its token, the already-written prefix stays, no flush is added and the destination
    // remains caller-owned/open.
    [Fact]
    public async Task WriteDocumentAsync_CancellationMidPump_ShouldAbortKeepingPrefixAndDestinationOpen()
    {
        using var cts = new CancellationTokenSource();
        var reader = new FakeDocumentReader(["\"", "rest", "\""]);
        using var sink = new RecordingSink(cancelOnFirstWrite: cts);

        var act = async () => await JsonNativeStream.WriteDocumentAsync(reader, sink, cts.Token);

        var exception = await act.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.CancellationToken.Should().Be(cts.Token);
        sink.Text.Should().Be("\"");
        sink.FlushCalls.Should().Be(0);
        sink.CanWrite.Should().BeTrue();
        sink.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void WriteDocument_SinkFailure_ShouldPropagateWithoutFlush()
    {
        // The first document chunk reaches the destination before the second write fails; the sink is not
        // flushed or disposed, and no recovery tail is attempted.
        var reader = new FakeDocumentReader(["first-chunk", "second-chunk"]);
        using var sink = new RecordingSink(failAtWriteCall: 2);

        var act = () => JsonNativeStream.WriteDocument(reader, sink);

        act.Should().Throw<InvalidOperationException>().WithMessage("native sink failed");
        sink.Text.Should().Be("first-chunk");
        sink.FlushCalls.Should().Be(0);
        sink.CanWrite.Should().BeTrue();
        sink.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task WriteDocumentAsync_SinkFailure_ShouldPropagateWithoutFlush()
    {
        var reader = new FakeDocumentReader(["first-chunk", "second-chunk"]);
        using var sink = new RecordingSink(failAtWriteCall: 2);

        var act = async () => await JsonNativeStream.WriteDocumentAsync(reader, sink, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("native sink failed");
        sink.Text.Should().Be("first-chunk");
        sink.FlushCalls.Should().Be(0);
        sink.CanWrite.Should().BeTrue();
        sink.IsDisposed.Should().BeFalse();
    }

    // A destination that records bytes/flushes and can fail on a chosen write call; never disposed by the
    // native pump. It can also cancel a controllable token after its first successful write, so a test can
    // cancel mid-pump on the next asynchronous boundary.
    private sealed class RecordingSink : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly int _failAtWriteCall;
        private readonly CancellationTokenSource? _cancelOnFirstWrite;
        private bool _cancelled;

        internal RecordingSink(int failAtWriteCall = -1, CancellationTokenSource? cancelOnFirstWrite = null)
        {
            _failAtWriteCall = failAtWriteCall;
            _cancelOnFirstWrite = cancelOnFirstWrite;
        }

        internal int WriteCalls { get; private set; }
        internal int FlushCalls { get; private set; }
        internal bool IsDisposed { get; private set; }
        internal string Text => Encoding.UTF8.GetString(_inner.ToArray());

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
                throw new InvalidOperationException("native sink failed");
            _inner.Write(buffer, offset, count);
            CancelAfterFirstWrite();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCalls++;
            if (WriteCalls == _failAtWriteCall)
                throw new InvalidOperationException("native sink failed");
            _inner.Write(buffer.Span);
            CancelAfterFirstWrite();
            return ValueTask.CompletedTask;
        }

        private void CancelAfterFirstWrite()
        {
            if (_cancelOnFirstWrite is { } cts && !_cancelled)
            {
                _cancelled = true;
                cts.Cancel();
            }
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private static void Pump(FakeDocumentReader reader, out string text)
    {
        using var stream = new MemoryStream();
        JsonNativeStream.WriteDocument(reader, stream);
        text = Encoding.UTF8.GetString(stream.ToArray());
        // A native document copy uses only Read/IsDBNull/GetChars, never a managed typed accessor.
        reader.ManagedReadCount.Should().Be(0);
    }

    /// <summary>
    /// Minimal forward-only <see cref="DbDataReader"/> over a sequence of document chunks. Each chunk is
    /// one reader row; a chunk may hold NULL (a NULL document row). Only the members the native pump
    /// uses are implemented; every managed row accessor counts as a managed read and throws.
    /// </summary>
    private sealed class FakeDocumentReader : DbDataReader
    {
        private readonly IReadOnlyList<string?> _rows;
        private readonly int _throwOnReadCall;
        private readonly int _throwOnCharsCall;
        private int _index = -1;
        private int _readCalls;
        private int _charsCalls;

        internal FakeDocumentReader(IReadOnlyList<string?> rows, int throwOnReadCall = -1, int throwOnCharsCall = -1)
        {
            _rows = rows;
            _throwOnReadCall = throwOnReadCall;
            _throwOnCharsCall = throwOnCharsCall;
        }

        internal int ManagedReadCount { get; private set; }

        public override int FieldCount => 1;
        public override bool HasRows => _rows.Count > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;

        public override bool Read()
        {
            _readCalls++;
            if (_readCalls == _throwOnReadCall)
                throw new InvalidOperationException("native reader failed mid-read");

            _index++;
            return _index < _rows.Count;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read());
        }

        public override bool IsDBNull(int ordinal)
        {
            EnsureOrdinal(ordinal);
            return _rows[_index] is null;
        }

        public override Type GetFieldType(int ordinal)
        {
            EnsureOrdinal(ordinal);
            return typeof(string);
        }

        public override string GetName(int ordinal) => "document";
        public override int GetOrdinal(string name) => 0;
        public override string GetDataTypeName(int ordinal) => "nvarchar";

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        {
            EnsureOrdinal(ordinal);
            _charsCalls++;
            if (_charsCalls == _throwOnCharsCall)
                throw new InvalidOperationException("native reader failed mid-read");

            var row = _rows[_index];
            if (row is null || dataOffset >= row.Length || length <= 0)
                return 0;

            var offset = (int)dataOffset;
            var count = Math.Min(length, row.Length - offset);
            row.CopyTo(offset, buffer!, bufferOffset, count);
            return count;
        }

        public override IEnumerator GetEnumerator() => _rows.GetEnumerator();
        public override bool NextResult() => false;
        public override int GetValues(object[] values) => 0;

        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));

        private object ManagedRead() => throw new NotSupportedException("A managed row accessor was invoked on the native document reader.");

        public override object GetValue(int ordinal) { ManagedReadCount++; return ManagedRead(); }
        public override bool GetBoolean(int ordinal) { ManagedReadCount++; return (bool)ManagedRead(); }
        public override byte GetByte(int ordinal) { ManagedReadCount++; return (byte)ManagedRead(); }
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) { ManagedReadCount++; return (long)ManagedRead(); }
        public override char GetChar(int ordinal) { ManagedReadCount++; return (char)ManagedRead(); }
        public override DateTime GetDateTime(int ordinal) { ManagedReadCount++; return (DateTime)ManagedRead(); }
        public override decimal GetDecimal(int ordinal) { ManagedReadCount++; return (decimal)ManagedRead(); }
        public override double GetDouble(int ordinal) { ManagedReadCount++; return (double)ManagedRead(); }
        public override float GetFloat(int ordinal) { ManagedReadCount++; return (float)ManagedRead(); }
        public override Guid GetGuid(int ordinal) { ManagedReadCount++; return (Guid)ManagedRead(); }
        public override short GetInt16(int ordinal) { ManagedReadCount++; return (short)ManagedRead(); }
        public override int GetInt32(int ordinal) { ManagedReadCount++; return (int)ManagedRead(); }
        public override long GetInt64(int ordinal) { ManagedReadCount++; return (long)ManagedRead(); }
        public override string GetString(int ordinal) { ManagedReadCount++; return (string)ManagedRead(); }

        private void EnsureOrdinal(int ordinal)
        {
            if (ordinal != 0)
                throw new IndexOutOfRangeException();
        }
    }
}
