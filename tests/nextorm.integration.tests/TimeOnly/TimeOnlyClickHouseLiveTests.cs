using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T340 (#340) ClickHouse reality: there is no time-of-day type. The binder keeps
/// <see cref="TimeOnly"/> as a <c>String</c> column type
/// (<c>ClickHouseDataContext.ColumnTypeName</c>, the <c>typeof(TimeOnly) =&gt; "String"</c> arm), so a
/// mapped <see cref="TimeOnly"/> reader has no native value to accept and must be refused. The raw
/// probe reads the stored value back as the driver's <see cref="string"/>.
/// </summary>
[Collection("ClickHouse")]
public sealed class TimeOnlyClickHouseLiveTests : ProviderTestSuite
{
    private const string Table = "t340_time_probe";

    private readonly ITestOutputHelper _output;

    public TimeOnlyClickHouseLiveTests(ITestOutputHelper output) => _output = output;

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [SqlTable(Table)]
    public interface ITimeProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("t")]
        TimeOnly? T { get; set; }
    }

    [SqlTable(Table)]
    public interface IStringProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("t")]
        string? T { get; set; }
    }

    /// <summary>AC3: no native time of day, so the mapped projection is refused.</summary>
    [Fact]
    public void MappedTimeOnly_ShouldBeRefused()
    {
        CreateFixture();
        try
        {
            var act = FluentActions.Invoking(() => Context.From<ITimeProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.T })
                .ToList());

            var failure = act.Should().Throw<NotSupportedException>().Which;
            _output.WriteLine($"T340 ClickHouse mapped TimeOnly refusal: {failure.Message}");
            failure.Message.Should().Contain("TimeOnly");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC3: the stored ClickHouse value is a string, read back unchanged.</summary>
    [Fact]
    public void StoredValue_ShouldBeReadAsString()
    {
        CreateFixture();
        try
        {
            var rows = Context.From<IStringProbe>().OrderBy(x => x.Id).Select(x => x.T).ToList();

            rows[0].Should().Be("13:45:07.123456");
            rows[1].Should().Be("00:00:00.0000000");
            rows[2].Should().BeNull();

            var context = (DataContext)Context;
            context.EnsureConnectionOpen();
            using var command = context.CreateCommand($"select id, t from {Table} order by id");
            using var reader = command.ExecuteReader();

            _output.WriteLine($"T340 ClickHouse ADO connection: {command.Connection!.GetType().FullName}");
            var fieldTypes = new List<string>();
            while (reader.Read())
                fieldTypes.Add(reader.GetFieldType(1).FullName!);

            fieldTypes.Should().OnlyContain(x => x == "System.String");
            _output.WriteLine($"T340 ClickHouse stored time: GetFieldType={fieldTypes[0]}");
        }
        finally
        {
            DropFixture();
        }
    }

    private void CreateFixture()
    {
        Execute($"drop table if exists {Table}");
        Execute($"create table {Table} (id Int64, t Nullable(String)) engine = Memory");
        Execute($"insert into {Table} (id, t) values (1, '13:45:07.123456'), (2, '00:00:00.0000000'), (3, null)");
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
