using System.Text;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Cross-provider coverage for the <c>WriteCsv</c>/<c>WriteCsvAsync</c> terminals. The facts run once
/// per concrete provider subclass (PostgreSQL, SQL Server, MySQL and SQLite) and assert the exact
/// UTF-8/RFC 4180 bytes: header, escaping, invariant typed formatting, projection shapes, empty
/// results, custom delimiters, destination ownership, parameters, cancellation and the box-free
/// streaming guarantee. Provider-specific parameter/type quirks are avoided by projecting the typed
/// values through <see cref="SqlFunctions.Parameter{T}"/> rather than dedicated columns.
/// </summary>
public abstract partial class CommonTestSuite
{
    // A whole-second, Unspecified value keeps the "O" rendering identical on every provider.
    private static readonly DateTime CsvWhen = new(2023, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
    private static readonly Guid CsvGuid = Guid.Parse("d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e");
    private const decimal CsvAmount = 1234.5678m;

    // Comma, double quote, CR and LF: every RFC 4180 escaping branch in a single field.
    private const string CsvTricky = "a,b\"c\rd\ne";

    private static string Utf8(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    [Fact]
    public void Csv_Projection_ShouldWriteExactHeaderAndEscapedRows()
    {
        using var destination = new MemoryStream();

        _sut.ComplexEntity
            .Where(x => x.Id >= 1)
            .Select(x => new { x.Id, x.TinyInt, x.Boolean, x.String, x.RequiredString })
            .OrderBy(x => x.Id)
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        Utf8(destination).Should().Be(
            "Id,TinyInt,Boolean,String,RequiredString\r\n" +
            "1,2,true,dadfasd,sdf\r\n" +
            "2,2,false,xxx,asdfgoi\r\n" +
            "3,2,false,\\N,34mfs\r\n");
    }

    [Fact]
    public void Csv_InvariantTypedColumns_ShouldWriteExactBytes()
    {
        var blob = new byte[] { 1, 2, 3, 4 };
        using var destination = new MemoryStream();

        _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                x.Id,
                Amount = SqlFunctions.Parameter<decimal>(0),
                When = SqlFunctions.Parameter<DateTime>(1),
                Flag = SqlFunctions.Parameter<bool>(2),
                Key = SqlFunctions.Parameter<Guid>(3),
                Blob = SqlFunctions.Parameter<byte[]>(4),
                Text = SqlFunctions.Parameter<string>(5),
            })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken, CsvAmount, CsvWhen, true, CsvGuid, blob, CsvTricky);

        Utf8(destination).Should().Be(
            "Id,Amount,When,Flag,Key,Blob,Text\r\n" +
            "1,1234.5678,2023-01-02T03:04:05.0000000,true,d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e,AQIDBA==,\"a,b\"\"c\rd\ne\"\r\n");
    }

    [Fact]
    public async Task CsvAsync_ShouldMatchSyncBytes()
    {
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        _sut.ComplexEntity
            .Select(x => new { x.Id, x.TinyInt, x.Boolean, x.String, x.RequiredString })
            .OrderBy(x => x.Id)
            .WriteCsv(sync, null, TestContext.Current.CancellationToken);

        await _sut.ComplexEntity
            .Select(x => new { x.Id, x.TinyInt, x.Boolean, x.String, x.RequiredString })
            .OrderBy(x => x.Id)
            .WriteCsvAsync(async, null, TestContext.Current.CancellationToken);

        Utf8(async).Should().Be(Utf8(sync));
        Utf8(sync).Should().Contain("3,2,false,\\N,34mfs\r\n");
    }

    [Fact]
    public void Csv_EntityBuilderWithoutSelect_ShouldWriteAllEntityColumns()
    {
        using var destination = new MemoryStream();

        _sut.BinaryEntity.OrderBy(x => x.Id).WriteCsv(destination, null, TestContext.Current.CancellationToken);

        Utf8(destination).Should().Be(
            "Id,Data\r\n" +
            "1,AQIDBA==\r\n" +
            "2,\\N\r\n");
    }

    [Fact]
    public void Csv_DtoProjection_ShouldWriteProjectedColumn()
    {
        using var destination = new MemoryStream();

        _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new CsvDto(x.Id))
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        Utf8(destination).Should().Be("Id\r\n1\r\n");
    }

    [Fact]
    public void Csv_EmptyResult_ShouldWriteHeaderOnly()
    {
        using var destination = new MemoryStream();

        _sut.ComplexEntity
            .Where(x => x.Id > 100)
            .Select(x => new { x.Id, x.RequiredString })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        Utf8(destination).Should().Be("Id,RequiredString\r\n");
    }

    [Fact]
    public void Csv_IncludeHeaderFalse_ShouldWriteRowsOnly()
    {
        using var destination = new MemoryStream();

        _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new { x.Id, x.RequiredString })
            .WriteCsv(destination, new CsvStreamOptions { IncludeHeader = false }, TestContext.Current.CancellationToken);

        Utf8(destination).Should().Be("1,sdf\r\n");
    }

    [Fact]
    public void Csv_CustomDelimiter_ShouldQuoteOnlyTheConfiguredDelimiter()
    {
        using var quoted = new MemoryStream();
        _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new { x.Id, Text = SqlFunctions.Parameter<string>(0) })
            .WriteCsv(quoted, new CsvStreamOptions { Delimiter = ';' }, TestContext.Current.CancellationToken, "a;b");

        Utf8(quoted).Should().Be("Id;Text\r\n1;\"a;b\"\r\n");

        using var unquoted = new MemoryStream();
        _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new { x.Id, Text = SqlFunctions.Parameter<string>(0) })
            .WriteCsv(unquoted, new CsvStreamOptions { Delimiter = ';' }, TestContext.Current.CancellationToken, "a,b");

        Utf8(unquoted).Should().Be("Id;Text\r\n1;a,b\r\n");
    }

    [Fact]
    public async Task Csv_ShouldLeaveDestinationOpen()
    {
        using var sync = new TrackingStream();
        _sut.ComplexEntity.Where(x => x.Id == 1).Select(x => new { x.Id })
            .WriteCsv(sync, null, TestContext.Current.CancellationToken);

        sync.WasDisposed.Should().BeFalse();
        sync.CanRead.Should().BeTrue();
        Utf8(sync).Should().Be("Id\r\n1\r\n");

        var async = new TrackingStream();
        await _sut.ComplexEntity.Where(x => x.Id == 1).Select(x => new { x.Id })
            .WriteCsvAsync(async, null, TestContext.Current.CancellationToken);

        async.WasDisposed.Should().BeFalse();
        async.CanRead.Should().BeTrue();
        Utf8(async).Should().Be("Id\r\n1\r\n");
        async.Dispose();
    }

    [Fact]
    public async Task Csv_WithWhereParameter_ShouldFilterRows()
    {
        using var sync = new MemoryStream();
        _sut.ComplexEntity
            .Where(x => x.Id > SqlFunctions.Parameter<long>(0))
            .Select(x => new { x.Id })
            .OrderBy(x => x.Id)
            .WriteCsv(sync, null, TestContext.Current.CancellationToken, 1L);

        Utf8(sync).Should().Be("Id\r\n2\r\n3\r\n");

        using var asyncStream = new MemoryStream();
        await _sut.ComplexEntity
            .Where(x => x.Id > SqlFunctions.Parameter<long>(0))
            .Select(x => new { x.Id })
            .OrderBy(x => x.Id)
            .WriteCsvAsync(asyncStream, null, TestContext.Current.CancellationToken, 1L);

        Utf8(asyncStream).Should().Be("Id\r\n2\r\n3\r\n");
    }

    [Fact]
    public void Csv_CancelledMidStream_ShouldThrowAndLeaveDestinationOpen()
    {
        using var cts = new CancellationTokenSource();
        var destination = new CancellingStream(cts, cancelAfterWrites: 2);

        var act = () => _sut.ComplexEntity
            .Select(x => new { x.Id })
            .OrderBy(x => x.Id)
            .WriteCsv(destination, null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        destination.WasDisposed.Should().BeFalse();
        destination.CanRead.Should().BeTrue();
        Utf8(destination).Should().Be("Id\r\n1\r\n");
        destination.Dispose();
    }

    [Fact]
    public async Task CsvAsync_CancelledMidStream_ShouldThrowAndLeaveDestinationOpen()
    {
        using var cts = new CancellationTokenSource();
        var destination = new CancellingStream(cts, cancelAfterWrites: 2);

        var act = async () => await _sut.ComplexEntity
            .Select(x => new { x.Id })
            .OrderBy(x => x.Id)
            .WriteCsvAsync(destination, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        destination.WasDisposed.Should().BeFalse();
        destination.CanRead.Should().BeTrue();
        destination.Dispose();
    }

    [Fact]
    public void Csv_ShouldNotConstructProjectionType()
    {
        // The counter is process-wide (one static per closed nested type), but the four provider
        // suites run in parallel; the lock keeps reset/count/assert atomic across them so a
        // concurrent provider can never corrupt this suite's observation.
        lock (CsvCountingRow.Sync)
        {
            CsvCountingRow.Constructions = 0;
            using var destination = new MemoryStream();

            _sut.ComplexEntity
                .OrderBy(x => x.Id)
                .Select(x => new CsvCountingRow(x.Id, x.RequiredString))
                .WriteCsv(destination, null, TestContext.Current.CancellationToken);

            CsvCountingRow.Constructions.Should().Be(0, "the CSV terminal must stream rows without materializing TResult");
            Utf8(destination).Should().Contain("1,sdf");
        }
    }

    [Fact]
    public void Csv_InMemory_ShouldThrowNotSupported()
    {
        using var sync = new MemoryStream();
        using var memory = new InMemoryDataContext();

        var act = () => memory.From<CsvMemoryRow>().WriteCsv(sync);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task CsvAsync_InMemory_ShouldThrowNotSupported()
    {
        using var destination = new MemoryStream();
        using var memory = new InMemoryDataContext();

        var act = async () => await memory.From<CsvMemoryRow>().WriteCsvAsync(destination);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void Csv_UnsupportedProjectionColumn_ShouldThrowNotSupported()
    {
        using var destination = new MemoryStream();

        var act = () => _sut.DataProvider.From<IValueConverterProbe>()
            .Select(x => new { x.State })
            .WriteCsv(destination);

        act.Should().Throw<NotSupportedException>().WithMessage("*State*");
    }

    /// <summary>A DTO projection; the header takes the constructor parameter name.</summary>
    private sealed record CsvDto(long Id);

    /// <summary>A projection whose constructor counts materializations; the terminal must never call it.</summary>
    private sealed class CsvCountingRow
    {
        /// <summary>Serializes the reset/count/assert window of every parallel provider suite.</summary>
        public static readonly object Sync = new();

        public static int Constructions;

        public CsvCountingRow(long id, string? text)
        {
            Constructions++;
            Id = id;
            Text = text;
        }

        public long Id { get; }

        public string? Text { get; }
    }

    /// <summary>An in-memory-only row type used to prove the terminal rejects the in-memory provider.</summary>
    private sealed class CsvMemoryRow
    {
        public int Id { get; set; }
    }

    /// <summary>Reveals whether the terminal disposed a destination it was handed.</summary>
    private sealed class TrackingStream : MemoryStream
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>Cancels its token source once the configured number of buffer writes has completed, so the next loop iteration observes cancellation.</summary>
    private sealed class CancellingStream(CancellationTokenSource source, int cancelAfterWrites) : MemoryStream
    {
        private int _writes;

        public bool WasDisposed { get; private set; }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            CountWrite();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var result = base.WriteAsync(buffer, cancellationToken);
            CountWrite();
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        private void CountWrite()
        {
            if (Interlocked.Increment(ref _writes) == cancelAfterWrites)
                source.Cancel();
        }
    }
}
