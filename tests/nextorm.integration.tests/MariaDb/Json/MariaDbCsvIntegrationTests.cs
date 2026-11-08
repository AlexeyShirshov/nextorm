using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// MariaDB-only coverage for the <c>WriteCsv</c>/<c>WriteCsvAsync</c> terminals. MariaDB has no
/// <see cref="CommonTestSuite"/>-based integration class (it has its own <see cref="MariaDbContainer"/>
/// harness), so the shared CSV facts are re-pinned here against a real MariaDB server. As in the
/// shared suite, the typed values travel through <see cref="SqlFunctions.Parameter{T}"/> so the exact
/// bytes are controlled by the CLR value rather than by a provider-specific column type.
/// </summary>
public sealed class MariaDbCsvIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public MariaDbCsvIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    private EntityBuilder<ICsvProbe> Probe => _ctx.From<ICsvProbe>();

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

        Probe
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                NullText = x.S,
                When = SqlFunctions.Parameter<DateTime>(0),
                Key = SqlFunctions.Parameter<Guid>(1),
                Amount = SqlFunctions.Parameter<decimal>(2),
                Flag = SqlFunctions.Parameter<bool>(3),
                Text = SqlFunctions.Parameter<string>(4),
            })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken, CsvWhen, CsvGuid, CsvAmount, true, CsvTricky);

        Utf8(destination).Should().Be(
            "Id,NullText,When,Key,Amount,Flag,Text\r\n" +
            "1,dadfasd,2023-01-02T03:04:05.0000000,d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e,1234.5678,true,\"a,b\"\"c\rd\ne\"\r\n" +
            "2,\\N,2023-01-02T03:04:05.0000000,d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e,1234.5678,true,\"a,b\"\"c\rd\ne\"\r\n");
    }

    [Fact]
    public async Task CsvAsync_ShouldMatchSyncBytesAndLeaveDestinationOpen()
    {
        using var sync = new MemoryStream();
        Probe
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.S })
            .WriteCsv(sync, null, TestContext.Current.CancellationToken);

        var async = new MemoryStream();
        await Probe
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.S })
            .WriteCsvAsync(async, null, TestContext.Current.CancellationToken);

        Utf8(async).Should().Be(Utf8(sync));
        Utf8(sync).Should().Be("Id,S\r\n1,dadfasd\r\n2,\\N\r\n");

        // The terminal must not dispose a destination it was handed.
        async.CanRead.Should().BeTrue();
        async.ToArray().Should().NotBeEmpty();
        async.Dispose();
    }

    private void Seed()
    {
        Execute("drop table if exists csv_probe");
        Execute("create table csv_probe (id int not null primary key, s varchar(100) null)");
        Execute("insert into csv_probe (id, s) values (1, 'dadfasd'), (2, null)");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("csv_probe")]
internal interface ICsvProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("s")]
    string? S { get; set; }
}
