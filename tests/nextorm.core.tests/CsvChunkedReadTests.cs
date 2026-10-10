using System.Buffers;
using System.Buffers.Text;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Contract tests for the bounded CSV binary path (<see cref="CsvBinaryFieldWriter"/> and the chunked
/// column descriptor on <see cref="CsvStreamWriter"/>). The probe reader serves <c>GetBytes</c> from a
/// deterministic generator, throws on any whole-field getter (<c>GetFieldValue&lt;byte[]&gt;</c>/
/// <c>GetValue</c>), counts offset/request/returned sizes and ordinal access, and can return short reads.
/// The destination is instrumented to register the largest single write, so an implementation that
/// accumulates the whole Base64 in the row buffer cannot pass the bounded-memory assertions.
/// </summary>
public class CsvChunkedReadTests
{
    private static CsvStreamOptions RowOnly() => new() { IncludeHeader = false };

    private static SelectExpression BinaryColumn(int index) => new(typeof(byte[])) { Index = index, PropertyName = $"B{index}", PhysicalColumnName = $"b{index}" };

    private static SelectExpression IntColumn(int index) => new(typeof(int)) { Index = index, PropertyName = $"S{index}" };

    /// <summary>
    /// A raw-SQL/computed-like <c>byte[]</c> projection: no producing expression and no mapping-derived
    /// physical column name (raw scalar/expression ordinals have neither). It must never be admitted to
    /// the bounded <c>GetBytes</c> path.
    /// </summary>
    private static SelectExpression RawLikeBinaryColumn(int index)
        => new(typeof(byte[])) { Index = index, PropertyName = "RawBlob" };

    /// <summary>
    /// Excel-mode cases for the first-byte guard: a leading base64 <c>+</c> must be apostrophe-guarded
    /// no matter where a trailing 1-2 byte remainder is shifted, and a non-<c>+</c> first byte must not be.
    /// </summary>
    public static TheoryData<byte[], int[]?> ExcelGuardCases()
    {
        return new TheoryData<byte[], int[]?>
        {
            { [0xFB, 0x00, 0x00, 0x00], null },              // remainder 1, first base64 char '+'
            { [0xFB, 0x00, 0x00, 0x00, 0x00], null },        // remainder 2, first base64 char '+'
            { [0x00, 0x00, 0x00, 0xFB], null },              // remainder 1, first char not '+' (trailing byte would fake it)
            { [0xFB, 0x00, 0x00], null },                    // remainder 0, first base64 char '+'
            { [0xFB, 0x00, 0x00, 0x00], [2, 2] },            // short first read leaves remainder 1
            { [0xFB, 0x00, 0x00, 0x00, 0x00], [2, 3] },      // short first read leaves remainder 2
        };
    }

    /// <summary>
    /// A projected bound parameter (<c>SqlFunctions.Parameter&lt;byte[]&gt;</c>), the shape the query
    /// preparer records for a parameter projection: a non-member method call, not a direct member read.
    /// </summary>
    private static SelectExpression ParameterBinaryColumn(int index)
    {
        var call = Expression.Call(
            typeof(SqlFunctions).GetMethod(nameof(SqlFunctions.Parameter))!.MakeGenericMethod(typeof(byte[])),
            Expression.Constant(0));
        return new SelectExpression(typeof(byte[])) { Index = index, PropertyName = "Blob", Expression = call };
    }

    private static byte GeneratorByte(long index) => (byte)((index * 31 + 7) & 0xFF);

    private static byte[] Generate(long length)
    {
        var bytes = new byte[length];
        for (long i = 0; i < length; i++)
            bytes[i] = GeneratorByte(i);

        return bytes;
    }

    private static (CsvPlan Plan, CsvStreamOptions Options) BuildPlan(
        ChunkProbeReader reader,
        SelectExpression[] columns,
        CsvStreamOptions? options = null,
        bool sequentialAccess = true)
    {
        var opts = options ?? RowOnly();
        var plan = CsvStreamWriter.Build(
            columns,
            reader,
            (column, record, _) => RowMapperFactory.MapColumn(column, record),
            sequentialAccess,
            opts);
        return (plan, opts);
    }

    private static async Task RunWriter(bool async, ChunkProbeReader reader, CountingStream destination, CsvPlan plan, CsvStreamOptions options, CancellationToken cancellationToken)
    {
        if (async)
            await CsvStreamWriter.WriteAsync(reader, destination, plan, options, cancellationToken).ConfigureAwait(false);
        else
            CsvStreamWriter.Write(reader, destination, plan, options, cancellationToken);
    }

    public static TheoryData<int, bool> SizeCases()
    {
        var b = CsvBinaryFieldWriter.BinaryCapacity;
        return new TheoryData<int, bool>
        {
            { 1, false }, { 1, true },
            { 2, false }, { 3, false }, { 4, false },
            { b - 2, false }, { b - 1, false }, { b, false }, { b + 1, false }, { b + 2, false },
            { 2 * b + 1, false },
            { 1024 * 1024, false }, { 1024 * 1024, true },
        };
    }

    [Theory]
    [MemberData(nameof(SizeCases))]
    public async Task Chunked_ExactBytes_MatchBase64Oracle(int length, bool async)
    {
        var reader = new ChunkProbeReader(ProbeColumn.Generated(length));
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);

        plan.Columns[0].BinaryOrdinal.Should().Be(0, "a direct byte[] column on a confirmed sequential reader is chunked");
        plan.Columns[0].Write.Should().BeNull("the chunked path must not compile a whole-field getter");

        await RunWriter(async, reader, destination, plan, options, TestContext.Current.CancellationToken);

        var expected = Convert.ToBase64String(Generate(length)) + "\r\n";
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(expected);

        reader.WholeFieldGetterCalls.Should().BeEmpty("the bounded path never reads the whole field");
        reader.GetBytesCalls.Should().NotBeEmpty("the bounded path must use IDataRecord.GetBytes");
        reader.MaxRequestLength.Should().BeLessThanOrEqualTo(CsvBinaryFieldWriter.BinaryCapacity, "the logical request size is bounded");
        destination.MaxWriteLength.Should().BeLessThanOrEqualTo(Base64.GetMaxEncodedToUtf8Length(CsvBinaryFieldWriter.BinaryCapacity));
        destination.Disposed.Should().BeFalse("the caller owns the destination");
    }

    [Theory]
    [MemberData(nameof(ExcelGuardCases))]
    public async Task ExcelMode_Guard_LatchesTheFirstFieldByte_AcrossRemaindersAndShortReads(byte[] bytes, int[]? shortReadPattern)
    {
        // D3c FIX 1 regression: the '+' guard must be evaluated against the first field byte, not the byte
        // left at _input[0] after the carry BlockCopy moved a trailing 1-2 byte remainder over it.
        var reader = new ChunkProbeReader(ProbeColumn.Blob(bytes)) { ShortReadPattern = shortReadPattern };
        var destination = new CountingStream();
        var options = new CsvStreamOptions { IncludeHeader = false, ExcelMode = true };
        var (plan, opts) = BuildPlan(reader, [BinaryColumn(0)], options);

        plan.Columns[0].BinaryOrdinal.Should().Be(0, "a direct stored byte[] column is chunked");

        await RunWriter(async: false, reader, destination, plan, opts, TestContext.Current.CancellationToken);

        var b64 = Convert.ToBase64String(bytes);
        var expected = (bytes[0] >> 2 == 62 ? "'" : "") + b64 + "\r\n";
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(expected);
        reader.WholeFieldGetterCalls.Should().BeEmpty("the bounded path never reads the whole field");
    }

    [Fact]
    public async Task BoundedHighWater_ForOneMiB_StaysFarBelowTheField()
    {
        const int length = 1024 * 1024;
        var reader = new ChunkProbeReader(ProbeColumn.Generated(length));
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);

        await RunWriter(async: true, reader, destination, plan, options, TestContext.Current.CancellationToken);

        var expectedLength = Convert.ToBase64String(Generate(length)).Length + 2;
        destination.TotalWritten.Should().Be(expectedLength);
        destination.MaxWriteLength.Should().BeLessThanOrEqualTo(Base64.GetMaxEncodedToUtf8Length(CsvBinaryFieldWriter.BinaryCapacity));
        (destination.MaxWriteLength * 10).Should().BeLessThan(expectedLength, "no single write may approach the whole Base64 field");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullAndEmpty_Chunked_Semantics(bool async)
    {
        var nullReader = new ChunkProbeReader(ProbeColumn.NullBlob());
        var nullDestination = new CountingStream();
        var (nullPlan, nullOptions) = BuildPlan(nullReader, [BinaryColumn(0)]);
        nullPlan.Columns[0].BinaryOrdinal.Should().Be(0);

        await RunWriter(async, nullReader, nullDestination, nullPlan, nullOptions, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(nullDestination.ToArray()).Should().Be("\\N\r\n");
        nullReader.GetBytesCalls.Should().BeEmpty("NULL is written as the marker without touching GetBytes");

        var emptyReader = new ChunkProbeReader(ProbeColumn.EmptyBlob());
        var emptyDestination = new CountingStream();
        var (emptyPlan, emptyOptions) = BuildPlan(emptyReader, [BinaryColumn(0)]);
        emptyPlan.Columns[0].BinaryOrdinal.Should().Be(0);

        await RunWriter(async, emptyReader, emptyDestination, emptyPlan, emptyOptions, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(emptyDestination.ToArray()).Should().Be("\r\n", "a non-null empty field stays empty");
    }

    [Fact]
    public async Task ShortReads_CarryGroups_AndPaddingOnlyAtTheEnd()
    {
        const int length = 1000;
        var reader = new ChunkProbeReader(ProbeColumn.Generated(length))
        {
            ShortReadPattern = [1, 2, 1, 2, 1, 1, 2, 1, 2, 1, 3],
        };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);

        await RunWriter(async: false, reader, destination, plan, options, TestContext.Current.CancellationToken);

        var result = Encoding.UTF8.GetString(destination.ToArray());
        result.Should().Be(Convert.ToBase64String(Generate(length)) + "\r\n");
        reader.ReturnedSizes.Should().Contain(1);
        reader.ReturnedSizes.Should().Contain(2);

        var unpadded = result.TrimEnd('=', '\r', '\n');
        unpadded.Should().NotContain("=", "interior 3-byte groups are encoded whole, so '=' can only trail");
    }

    [Fact]
    public async Task OneAndTwoByteShortReads_ReconstructAThreeByteGroup()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Generated(3)) { ShortReadPattern = [1, 2] };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);

        await RunWriter(async: false, reader, destination, plan, options, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(Convert.ToBase64String(Generate(3)) + "\r\n");
    }

    [Fact]
    public async Task TwoBlobsWithScalars_AreReadInOrdinalOrder()
    {
        var reader = new ChunkProbeReader(
            ProbeColumn.Int(11),
            ProbeColumn.Blob([1, 2, 3]),
            ProbeColumn.Int(22),
            ProbeColumn.Blob([4, 5, 6, 7]),
            ProbeColumn.Int(33));
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [IntColumn(0), BinaryColumn(1), IntColumn(2), BinaryColumn(3), IntColumn(4)]);

        await RunWriter(async: false, reader, destination, plan, options, TestContext.Current.CancellationToken);

        var expected = "11," + Convert.ToBase64String([1, 2, 3]) + ",22," + Convert.ToBase64String([4, 5, 6, 7]) + ",33\r\n";
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(expected);
        reader.RowOrdinalAccesses.Should().BeInAscendingOrder("the reader is consumed in result-set ordinal order");
        reader.RowOrdinalAccesses.Should().ContainInOrder(0, 1, 2, 3, 4);
        reader.WholeFieldGetterCalls.Should().BeEmpty();
    }

    [Fact]
    public void Chunked_AdmittedOnlyWithConfirmedSequentialProvenance()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Generated(16));
        var options = RowOnly();

        var sequential = CsvStreamWriter.Build([BinaryColumn(0)], reader, (c, r, _) => RowMapperFactory.MapColumn(c, r), sequentialAccess: true, options);
        sequential.Columns[0].BinaryOrdinal.Should().Be(0);
        sequential.Columns[0].Write.Should().BeNull();

        var nonSequential = CsvStreamWriter.Build([BinaryColumn(0)], reader, (c, r, _) => RowMapperFactory.MapColumn(c, r), sequentialAccess: false, options);
        nonSequential.Columns[0].BinaryOrdinal.Should().BeNull("the mode is never inferred from the reader/GetBytes");
        nonSequential.Columns[0].Write.Should().NotBeNull();

        var noOptions = CsvStreamWriter.Build([BinaryColumn(0)], reader, (c, r, _) => RowMapperFactory.MapColumn(c, r), sequentialAccess: true, options: null);
        noOptions.Columns[0].BinaryOrdinal.Should().BeNull("the framing policy must be known before the field is read");

        var untyped = CsvStreamWriter.Build([BinaryColumn(0)], (c, r) => RowMapperFactory.MapColumn(c, r));
        untyped.Columns[0].BinaryOrdinal.Should().BeNull("a schema-less build has no storage type to confirm");
    }

    [Fact]
    public async Task ExplicitBufferedPath_StillWorks_WhenNotEligible()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([1, 2, 3])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)], RowOnly(), sequentialAccess: false);

        plan.Columns[0].BinaryOrdinal.Should().BeNull();
        await RunWriter(async: false, reader, destination, plan, options, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("AQID\r\n");
        reader.WholeFieldGetterCalls.Should().NotBeEmpty("the explicit buffered path reads the whole array");
    }

    /// <summary>
    /// Regression for the D3b P1 defect: a bound parameter <c>byte[]</c> projection is not a direct
    /// stored column, so it must never be admitted to the <c>GetBytes</c> path (calling <c>GetBytes</c>
    /// on that ordinal crashes the provider natively). It stays on the buffered path and produces the
    /// same Base64 bytes.
    /// </summary>
    [Fact]
    public async Task BoundParameterBinary_IsNotChunked_AndUsesTheBufferedPath()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([1, 2, 3, 4])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [ParameterBinaryColumn(0)]);

        plan.Columns[0].BinaryOrdinal.Should().BeNull("a bound SqlFunctions.Parameter<byte[]> projection is not a direct stored column");
        plan.Columns[0].Write.Should().NotBeNull("the parameter takes the buffered path");

        await RunWriter(async: false, reader, destination, plan, options, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("AQIDBA==\r\n");
        reader.GetBytesCalls.Should().BeEmpty("GetBytes must never be called for a projected parameter");
        reader.WholeFieldGetterCalls.Should().NotBeEmpty("the buffered path reads the whole parameter array");
    }

    /// <summary>
    /// D3c FIX 2 regression: a raw-SQL/computed-like <c>byte[]</c> column that carries no producing
    /// expression and no mapping-derived physical column name is not positive evidence of a stored entity
    /// column, so it must not be admitted to <c>GetBytes</c>; it stays on the buffered path.
    /// </summary>
    [Fact]
    public async Task RawOrComputedNullExpressionBinary_IsNotChunked_AndUsesTheBufferedPath()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([1, 2, 3, 4])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [RawLikeBinaryColumn(0)]);

        plan.Columns[0].BinaryOrdinal.Should().BeNull("a null-Expression, unmapped byte[] ordinal is not a proven stored column");
        plan.Columns[0].Write.Should().NotBeNull("the raw/computed-like column takes the buffered path");

        await RunWriter(async: false, reader, destination, plan, options, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("AQIDBA==\r\n");
        reader.GetBytesCalls.Should().BeEmpty("GetBytes must never be called for an unknown-provenance byte[] ordinal");
        reader.WholeFieldGetterCalls.Should().NotBeEmpty("the buffered path reads the whole array");
    }

    [Theory]
    [InlineData('/', false)]
    [InlineData('+', false)]
    [InlineData('=', false)]
    [InlineData(',', true)]
    [InlineData(';', true)]
    public void Eligibility_DelimiterIntersectingBase64Alphabet_IsRejected(char delimiter, bool eligible)
    {
        var dialect = new CsvDialect(delimiter);
        CsvBinaryFieldWriter.IsEligible(dialect, new CsvFieldPolicy("\\N", excelMode: false, valueTransform: null))
            .Should().Be(eligible, $"delimiter '{delimiter}' vs the Base64 alphabet");
    }

    [Theory]
    [InlineData("\\N", false, true)]
    [InlineData("AQID", false, false)]
    [InlineData("+/8=", false, false)]
    [InlineData("\\N", true, true)]
    [InlineData("'\\N", true, false)]
    [InlineData("'-A", true, false)]
    public void Eligibility_NullMarkerAndExcelMode_Guards(string marker, bool excelMode, bool eligible)
    {
        CsvBinaryFieldWriter.IsEligible(new CsvDialect(','), new CsvFieldPolicy(marker, excelMode, valueTransform: null))
            .Should().Be(eligible);
    }

    [Fact]
    public void Eligibility_Transform_ForcesTheBufferedPath()
    {
        CsvBinaryFieldWriter.IsEligible(new CsvDialect(','), new CsvFieldPolicy("\\N", excelMode: false, valueTransform: _ => "x"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task DelimiterInBase64Alphabet_UsesBufferedPathAndQuotes()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([0xFB, 0xFF])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var options = new CsvStreamOptions { IncludeHeader = false, Delimiter = '/' };
        var (plan, opts) = BuildPlan(reader, [BinaryColumn(0)], options);

        plan.Columns[0].BinaryOrdinal.Should().BeNull("'/' occurs in the Base64 alphabet, so framing cannot be known up front");
        await RunWriter(async: false, reader, destination, plan, opts, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("\"+/8=\"\r\n");
        reader.WholeFieldGetterCalls.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NullMarkerCollidingWithBase64_UsesBufferedPathAndQuotes()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([1, 2, 3])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var options = new CsvStreamOptions { IncludeHeader = false, NullMarker = "AQID" };
        var (plan, opts) = BuildPlan(reader, [BinaryColumn(0)], options);

        plan.Columns[0].BinaryOrdinal.Should().BeNull();
        await RunWriter(async: false, reader, destination, plan, opts, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("\"AQID\"\r\n");
    }

    [Fact]
    public async Task Transform_OtherResult_UsesBufferedPath()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([1, 2, 3])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var options = new CsvStreamOptions { IncludeHeader = false, ValueTransform = _ => "X" };
        var (plan, opts) = BuildPlan(reader, [BinaryColumn(0)], options);

        plan.Columns[0].BinaryOrdinal.Should().BeNull();
        await RunWriter(async: false, reader, destination, plan, opts, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("X\r\n");
    }

    [Fact]
    public async Task Transform_IdentityResult_MatchesBase64()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([1, 2, 3])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var options = new CsvStreamOptions { IncludeHeader = false, ValueTransform = value => Convert.ToBase64String((byte[])value!) };
        var (plan, opts) = BuildPlan(reader, [BinaryColumn(0)], options);

        await RunWriter(async: false, reader, destination, plan, opts, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("AQID\r\n");
    }

    [Fact]
    public async Task Transform_NullResult_WritesNullMarker()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Blob([1, 2, 3])) { AllowWholeFieldGetter = true };
        var destination = new CountingStream();
        var options = new CsvStreamOptions { IncludeHeader = false, ValueTransform = _ => null };
        var (plan, opts) = BuildPlan(reader, [BinaryColumn(0)], options);

        await RunWriter(async: false, reader, destination, plan, opts, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be("\\N\r\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transform_IsNeverInvokedForNullOrEmpty(bool async)
    {
        var calls = 0;
        var options = new CsvStreamOptions
        {
            IncludeHeader = false,
            ValueTransform = _ =>
            {
                calls++;
                return "X";
            },
        };

        var nullReader = new ChunkProbeReader(ProbeColumn.NullBlob()) { AllowWholeFieldGetter = true };
        var nullDestination = new CountingStream();
        var (nullPlan, nullOptions) = BuildPlan(nullReader, [BinaryColumn(0)], options);
        await RunWriter(async, nullReader, nullDestination, nullPlan, nullOptions, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(nullDestination.ToArray()).Should().Be("\\N\r\n");

        var emptyReader = new ChunkProbeReader(ProbeColumn.EmptyBlob()) { AllowWholeFieldGetter = true };
        var emptyDestination = new CountingStream();
        var (emptyPlan, emptyOptions) = BuildPlan(emptyReader, [BinaryColumn(0)], options);
        await RunWriter(async, emptyReader, emptyDestination, emptyPlan, emptyOptions, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(emptyDestination.ToArray()).Should().Be("\r\n");

        calls.Should().Be(0, "NULL and the empty field bypass the transform on both paths");
    }

    [Fact]
    public void PreCancelled_Sync_ThrowsBeforeAnyOutput()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Generated(64));
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CsvStreamWriter.Write(reader, destination, plan, options, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        destination.TotalWritten.Should().Be(0);
        destination.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task PreCancelled_Async_ThrowsBeforeAnyOutput()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Generated(64));
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await CsvStreamWriter.WriteAsync(reader, destination, plan, options, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        destination.TotalWritten.Should().Be(0);
        destination.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBetweenChunks_Async_ThrowsAndKeepsDestinationOpen()
    {
        using var cts = new CancellationTokenSource();
        var reader = new ChunkProbeReader(ProbeColumn.Generated(CsvBinaryFieldWriter.BinaryCapacity * 4))
        {
            OnGetBytes = probe =>
            {
                if (probe.GetBytesCalls.Count >= 1)
                    cts.Cancel();
            },
        };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);

        var act = async () => await CsvStreamWriter.WriteAsync(reader, destination, plan, options, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        destination.Disposed.Should().BeFalse("cancellation must not dispose the caller's destination");
    }

    [Fact]
    public void CancelBetweenChunks_Sync_ThrowsAndKeepsDestinationOpen()
    {
        // D3c FIX 3: the sync path must observe the token between GetBytes reads, not silently ignore it.
        using var cts = new CancellationTokenSource();
        var reader = new ChunkProbeReader(ProbeColumn.Generated(CsvBinaryFieldWriter.BinaryCapacity * 4))
        {
            OnGetBytes = probe =>
            {
                if (probe.GetBytesCalls.Count >= 1)
                    cts.Cancel();
            },
        };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);

        var act = () => CsvStreamWriter.Write(reader, destination, plan, options, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        destination.Disposed.Should().BeFalse("cancellation must not dispose the caller's destination");
    }

    [Fact]
    public void GetBytesError_DoesNotFallBackToAWholeFieldGetter()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Generated(200_000)) { GetBytesThrowAfter = 1 };
        var destination = new CountingStream();
        var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);

        var act = () => CsvStreamWriter.Write(reader, destination, plan, options, TestContext.Current.CancellationToken);

        act.Should().Throw<InvalidOperationException>();
        reader.GetBytesCalls.Should().NotBeEmpty("the error happens after at least one successful bounded read");
        reader.WholeFieldGetterCalls.Should().BeEmpty("the chunked path fails closed instead of falling back");
        destination.Disposed.Should().BeFalse();
    }

    [Fact]
    public void BufferOnlyWriteRow_RejectsABoundedBinaryColumn()
    {
        var reader = new ChunkProbeReader(ProbeColumn.Generated(16));
        var (plan, _) = BuildPlan(reader, [BinaryColumn(0)]);
        using var buffer = new CsvRowBuffer(new CsvDialect(','));

        var act = () => CsvStreamWriter.WriteRow(plan, reader, buffer);

        act.Should().Throw<InvalidOperationException>().WithMessage("*bounded binary*");
    }

    [Fact]
    public void PooledBuffers_AreReturned_OnSuccess()
        => OnFreshThread(() =>
        {
            var seedA = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            var seedB = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            ArrayPool<byte>.Shared.Return(seedB);
            ArrayPool<byte>.Shared.Return(seedA);

            var reader = new ChunkProbeReader(ProbeColumn.Generated(100));
            var destination = new CountingStream();
            var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);
            CsvStreamWriter.Write(reader, destination, plan, options, TestContext.Current.CancellationToken);

            var first = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            var second = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            try
            {
                ((object)first).Should().BeSameAs(seedB, "the writer's output buffer must be returned to the pool");
                ((object)second).Should().BeSameAs(seedA, "the writer's input buffer must be returned to the pool");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(second);
                ArrayPool<byte>.Shared.Return(first);
            }
        });

    [Fact]
    public void PooledBuffers_AreReturned_OnError()
        => OnFreshThread(() =>
        {
            var seedA = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            var seedB = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            ArrayPool<byte>.Shared.Return(seedB);
            ArrayPool<byte>.Shared.Return(seedA);

            var reader = new ChunkProbeReader(ProbeColumn.Generated(200_000)) { GetBytesThrowAfter = 1 };
            var destination = new CountingStream();
            var (plan, options) = BuildPlan(reader, [BinaryColumn(0)]);
            try
            {
                CsvStreamWriter.Write(reader, destination, plan, options, TestContext.Current.CancellationToken);
            }
            catch (InvalidOperationException)
            {
                // expected provider failure
            }

            var first = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            var second = ArrayPool<byte>.Shared.Rent(CsvBinaryFieldWriter.BinaryCapacity);
            try
            {
                ((object)first).Should().BeSameAs(seedB, "buffers are returned in a finally, even on error");
                ((object)second).Should().BeSameAs(seedA);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(second);
                ArrayPool<byte>.Shared.Return(first);
            }
        });

    private static void OnFreshThread(Action action)
    {
        ExceptionDispatchInfo? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                captured = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.Start();
        thread.Join();
        captured?.Throw();
    }

    /// <summary>A single probe column: either a scalar value or a generated binary field.</summary>
    private sealed class ProbeColumn
    {
        public bool IsBinary { get; private init; }

        public bool IsNull { get; private init; }

        public object? Scalar { get; private init; }

        public long Length { get; private init; }

        public Func<long, byte> Generator { get; private init; } = _ => 0;

        public Type ClrType => IsBinary ? typeof(byte[]) : Scalar?.GetType() ?? typeof(object);

        public static ProbeColumn Blob(byte[] bytes) => new()
        {
            IsBinary = true,
            Length = bytes.Length,
            Generator = index => bytes[index],
        };

        public static ProbeColumn Generated(long length) => new()
        {
            IsBinary = true,
            Length = length,
            Generator = GeneratorByte,
        };

        public static ProbeColumn NullBlob() => new() { IsBinary = true, IsNull = true };

        public static ProbeColumn EmptyBlob() => new() { IsBinary = true };

        public static ProbeColumn Int(int value) => new() { Scalar = value };
    }

    /// <summary>
    /// A deterministic <see cref="DbDataReader"/> that serves binary columns from a generator (no whole
    /// BLOB is held), throws on whole-field getters, and records every bounded read so the writer's
    /// memory/ordinal behaviour is observable.
    /// </summary>
    private sealed class ChunkProbeReader : DbDataReader
    {
        private readonly ProbeColumn[] _columns;
        private int _row;

        public ChunkProbeReader(params ProbeColumn[] columns) => _columns = columns;

        public bool AllowWholeFieldGetter { get; init; }

        public bool ThrowOnGetBytes { get; init; }

        /// <summary>Throws from <c>GetBytes</c> once this many successful calls have happened; -1 disables.</summary>
        public int GetBytesThrowAfter { get; init; } = -1;

        public Action<ChunkProbeReader>? OnGetBytes { get; init; }

        public IReadOnlyList<int>? ShortReadPattern { get; init; }

        public int Rows { get; init; } = 1;

        public List<(int Ordinal, long Offset, int Requested)> GetBytesCalls { get; } = [];

        public List<int> ReturnedSizes { get; } = [];

        public int MaxRequestLength { get; private set; }

        public List<string> WholeFieldGetterCalls { get; } = [];

        public List<int> RowOrdinalAccesses { get; } = [];

        public List<int> SchemaCalls { get; } = [];

        private int _shortIndex;

        public override int FieldCount => _columns.Length;

        public override int Depth => 0;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool IsDBNull(int ordinal)
        {
            RowOrdinalAccesses.Add(ordinal);
            return _columns[ordinal].IsNull;
        }

        public override object GetValue(int ordinal)
            => throw new InvalidOperationException($"GetValue({ordinal}) must not be called on the CSV write path.");

        public override T GetFieldValue<T>(int ordinal)
        {
            if (typeof(T) == typeof(byte[]))
            {
                WholeFieldGetterCalls.Add($"GetFieldValue<Byte[]>({ordinal})");
                if (!AllowWholeFieldGetter)
                    throw new InvalidOperationException(
                        $"whole-field GetFieldValue<Byte[]>({ordinal}) must not be called on the bounded CSV path.");

                return (T)(object)Materialize(ordinal);
            }

            var scalar = _columns[ordinal].Scalar;
            return (T)scalar!;
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        {
            if (ThrowOnGetBytes || (GetBytesThrowAfter >= 0 && GetBytesCalls.Count >= GetBytesThrowAfter))
                throw new InvalidOperationException($"The probe provider failed GetBytes(ordinal: {ordinal}, offset: {dataOffset}).");

            RowOrdinalAccesses.Add(ordinal);
            var column = _columns[ordinal];
            var remaining = column.Length - dataOffset;
            if (remaining <= 0)
            {
                GetBytesCalls.Add((ordinal, dataOffset, length));
                ReturnedSizes.Add(0);
                return 0;
            }

            var requested = (int)Math.Min(length, remaining);
            if (ShortReadPattern is { Count: > 0 })
            {
                var want = ShortReadPattern[_shortIndex++ % ShortReadPattern.Count];
                requested = Math.Min(requested, want);
            }

            GetBytesCalls.Add((ordinal, dataOffset, length));
            MaxRequestLength = Math.Max(MaxRequestLength, length);

            if (buffer is null)
            {
                ReturnedSizes.Add((int)Math.Min(remaining, int.MaxValue));
                return remaining;
            }

            for (var i = 0; i < requested; i++)
                buffer[bufferOffset + i] = column.Generator(dataOffset + i);

            ReturnedSizes.Add(requested);
            OnGetBytes?.Invoke(this);
            return requested;
        }

        public override bool Read() => _row++ < Rows;

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

        public override string GetName(int ordinal) => $"c{ordinal}";

        public override int GetOrdinal(string name) => 0;

        public override string GetDataTypeName(int ordinal) => "probe";

        public override Type GetFieldType(int ordinal)
        {
            SchemaCalls.Add(ordinal);
            return _columns[ordinal].ClrType;
        }

        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _columns.Length);
            for (var i = 0; i < count; i++)
                values[i] = _columns[i].Scalar ?? DBNull.Value;

            return count;
        }

        public override System.Collections.IEnumerator GetEnumerator() => _columns.GetEnumerator();

        public override bool NextResult() => false;

        public override bool GetBoolean(int ordinal) => (bool)_columns[ordinal].Scalar!;

        public override byte GetByte(int ordinal) => (byte)_columns[ordinal].Scalar!;

        public override char GetChar(int ordinal) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override DateTime GetDateTime(int ordinal) => (DateTime)_columns[ordinal].Scalar!;

        public override decimal GetDecimal(int ordinal) => (decimal)_columns[ordinal].Scalar!;

        public override double GetDouble(int ordinal) => (double)_columns[ordinal].Scalar!;

        public override float GetFloat(int ordinal) => (float)_columns[ordinal].Scalar!;

        public override Guid GetGuid(int ordinal) => (Guid)_columns[ordinal].Scalar!;

        public override short GetInt16(int ordinal)
        {
            RowOrdinalAccesses.Add(ordinal);
            return (short)_columns[ordinal].Scalar!;
        }

        public override int GetInt32(int ordinal)
        {
            RowOrdinalAccesses.Add(ordinal);
            return (int)_columns[ordinal].Scalar!;
        }

        public override long GetInt64(int ordinal) => (long)_columns[ordinal].Scalar!;

        public override string GetString(int ordinal)
        {
            RowOrdinalAccesses.Add(ordinal);
            return (string)_columns[ordinal].Scalar!;
        }

        private byte[] Materialize(int ordinal)
        {
            var column = _columns[ordinal];
            var bytes = new byte[column.Length];
            for (long i = 0; i < column.Length; i++)
                bytes[i] = column.Generator(i);

            return bytes;
        }
    }

    /// <summary>
    /// An instrumented destination that records the largest single write (the writer's output high-water)
    /// and whether it was ever disposed. It keeps the received bytes for exact-byte comparison.
    /// </summary>
    private sealed class CountingStream : Stream
    {
        private readonly MemoryStream _sink = new();

        public int MaxWriteLength { get; private set; }

        public int WriteCount { get; private set; }

        public long TotalWritten => _sink.Length;

        public bool Disposed { get; private set; }

        public byte[] ToArray() => _sink.ToArray();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _sink.Length;

        public override long Position
        {
            get => _sink.Position;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Record(count);
            _sink.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Record(buffer.Length);
            _sink.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Record(count);
            return _sink.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Record(buffer.Length);
            return _sink.WriteAsync(buffer, cancellationToken);
        }

        public override void Flush() => _sink.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _sink.FlushAsync(cancellationToken);

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        private void Record(int count)
        {
            WriteCount++;
            if (count > MaxWriteLength)
                MaxWriteLength = count;
        }
    }
}
