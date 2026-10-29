using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T340 (#340) streaming/terminal control: after the reader arm landed, a <see cref="TimeOnly"/> projection
/// must still be rejected by the JSON streaming shape planner and by the CSV terminal — their own type
/// switches are what the docs claim, so the claim is observed here rather than read out of the source.
/// <para>
/// SQL Server is used deliberately: it is a <b>supported</b> provider, so the row mapper composes without the
/// provider refusal and the rejection must come from the writer itself. A SQLite run would only re-prove the
/// provider refusal and would tell us nothing about the streaming switches.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class TimeOnlyStreamingControlTests : ProviderTestSuite
{
    private const string Table = "t340_stream_probe";

    private readonly ITestOutputHelper _output;

    public TimeOnlyStreamingControlTests(ITestOutputHelper output) => _output = output;

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [SqlTable(Table)]
    public interface IProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("t")]
        TimeOnly? T { get; set; }
    }

    /// <summary>The JSON streaming planner rejects a <see cref="TimeOnly"/> leaf while planning the shape.</summary>
    [Fact]
    public void JsonStreaming_ShouldRejectTimeOnly()
    {
        CreateFixture();
        try
        {
            using var stream = new MemoryStream();

            var failure = FluentActions.Invoking(() => Context.From<IProbe>()
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.T })
                    .WriteJson(stream))
                .Should().Throw<NotSupportedException>().Which;

            _output.WriteLine($"T340 JSON streaming TimeOnly rejection: {failure.Message}");
            failure.Message.Should().Contain("TimeOnly");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>The CSV terminal rejects the same projection before writing any output.</summary>
    [Fact]
    public void CsvTerminal_ShouldRejectTimeOnly()
    {
        CreateFixture();
        try
        {
            using var stream = new MemoryStream();

            var failure = FluentActions.Invoking(() => Context.From<IProbe>()
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.T })
                    .WriteCsv(stream))
                .Should().Throw<NotSupportedException>().Which;

            _output.WriteLine($"T340 CSV terminal TimeOnly rejection: {failure.Message}");
            failure.Message.Should().Contain("TimeOnly");
        }
        finally
        {
            DropFixture();
        }
    }

    private void CreateFixture()
    {
        Execute($"drop table if exists {Table}");
        Execute($"create table {Table} (id bigint not null primary key, t time(7) null)");
        Execute($"insert into {Table} (id, t) values (1, cast('13:45:07.1234567' as time(7))), (2, null)");
    }

    private void DropFixture() => Execute($"drop table if exists {Table}");

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
