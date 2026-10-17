using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse-only coverage for the <c>WriteCsv</c>/<c>WriteCsvAsync</c> terminals. ClickHouse does not
/// derive <see cref="CommonTestSuite"/>, so the shared CSV facts are re-pinned here against a real
/// server. Like the shared suite, the typed values travel through <see cref="SqlFunctions.Parameter{T}"/>
/// so the exact bytes are controlled by the CLR value rather than by a provider-specific column type.
/// </summary>
public sealed class ClickHouseCsvIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    // A whole-second, Unspecified value keeps the "O" rendering free of a kind suffix.
    private static readonly DateTime CsvWhen = new(2023, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
    private static readonly Guid CsvGuid = Guid.Parse("d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e");
    private const decimal CsvAmount = 1234.5678m;

    // Comma, double quote, CR and LF: every RFC 4180 escaping branch in a single field.
    private const string CsvTricky = "a,b\"c\rd\ne";

    private static string Utf8(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    [Fact]
    public void Csv_ShouldWriteExactHeaderAndInvariantTypedRows()
    {
        using var destination = new MemoryStream();

        _sut.ComplexEntity
            .Where(x => x.Id == 1L)
            .Select(x => new
            {
                x.Id,
                NullInt = x.Int,
                NullText = x.String,
                When = SqlFunctions.Parameter<DateTime>(0),
                Key = SqlFunctions.Parameter<Guid>(1),
                Amount = SqlFunctions.Parameter<decimal>(2),
                Flag = SqlFunctions.Parameter<bool>(3),
                Text = SqlFunctions.Parameter<string>(4),
            })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken, CsvWhen, CsvGuid, CsvAmount, true, CsvTricky);

        Utf8(destination).Should().Be(
            "Id,NullInt,NullText,When,Key,Amount,Flag,Text\r\n" +
            "1,\\N,dadfasd,2023-01-02T03:04:05.0000000,d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e,1234.5678,true,\"a,b\"\"c\rd\ne\"\r\n");
    }

    [Fact]
    public async Task CsvAsync_ShouldMatchSyncBytesAndLeaveDestinationOpen()
    {
        using var sync = new MemoryStream();
        _sut.ComplexEntity
            .Where(x => x.Id == 1L)
            .Select(x => new { x.Id, Text = SqlFunctions.Parameter<string>(0) })
            .WriteCsv(sync, null, TestContext.Current.CancellationToken, CsvTricky);

        var async = new MemoryStream();
        await _sut.ComplexEntity
            .Where(x => x.Id == 1L)
            .Select(x => new { x.Id, Text = SqlFunctions.Parameter<string>(0) })
            .WriteCsvAsync(async, null, TestContext.Current.CancellationToken, CsvTricky);

        Utf8(async).Should().Be(Utf8(sync));
        Utf8(sync).Should().Be("Id,Text\r\n1,\"a,b\"\"c\rd\ne\"\r\n");

        // The terminal must not dispose a destination it was handed.
        async.CanRead.Should().BeTrue();
        async.ToArray().Should().NotBeEmpty();
        async.Dispose();
    }
}
