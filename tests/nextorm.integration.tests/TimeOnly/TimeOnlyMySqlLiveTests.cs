using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T340 (#340) MySQL reality: <c>TIME</c> is a duration, not a time of day. The mapped
/// <see cref="TimeOnly"/> projection must be refused, the unchanged <see cref="TimeSpan"/> projection
/// keeps working, and the native driver value is observed raw (<c>GetFieldType</c>/<c>GetValue</c>) —
/// including the negative and over-24 h durations MySQL accepts.
/// </summary>
[Collection("MySql")]
public sealed class TimeOnlyMySqlLiveTests : ProviderTestSuite
{
    private const string Table = "t340_time_probe";

    private readonly ITestOutputHelper _output;

    public TimeOnlyMySqlLiveTests(ITestOutputHelper output) => _output = output;

    protected override ITestProvider Provider => MySqlTestProvider.Instance;

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
    public interface ISpanProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("t")]
        TimeSpan? T { get; set; }
    }

    /// <summary>AC3: MySQL has no time of day, so the mapped projection is refused (named refusal at green).</summary>
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
            _output.WriteLine($"T340 MySQL mapped TimeOnly refusal: {failure.Message}");
            failure.Message.Should().Contain("TimeOnly");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>CC4: the <see cref="TimeSpan"/> projection of the same column keeps MySQL's duration semantics.</summary>
    [Fact]
    public void TimeSpanControl_ShouldKeepDurationSemantics()
    {
        CreateFixture();
        try
        {
            var rows = Context.From<ISpanProbe>().OrderBy(x => x.Id).Select(x => x.T).ToList();

            rows[0].Should().Be(TimeSpan.FromTicks(495_071_234_560));
            rows[1].Should().Be(-TimeSpan.FromSeconds(3723));
            rows[2].Should().Be(TimeSpan.FromSeconds(90_061));
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC3: the native driver representation of a MySQL <c>TIME(6)</c> value.</summary>
    [Fact]
    public void NativeTimeRepresentation_ShouldBeObserved()
    {
        CreateFixture();
        try
        {
            var context = (DataContext)Context;
            context.EnsureConnectionOpen();
            using var command = context.CreateCommand($"select id, t from {Table} order by id");
            using var reader = command.ExecuteReader();

            _output.WriteLine($"T340 MySQL ADO connection: {command.Connection!.GetType().FullName}");

            var fieldTypes = new List<string>();
            var values = new List<object?>();
            while (reader.Read())
            {
                fieldTypes.Add(reader.GetFieldType(1).FullName!);
                values.Add(reader.GetValue(1));
            }

            fieldTypes.Should().OnlyContain(x => x == "System.TimeSpan");
            values[0].Should().BeOfType<TimeSpan>();
            values[0].Should().Be(TimeSpan.FromTicks(495_071_234_560));
            values[1].Should().Be(-TimeSpan.FromSeconds(3723));
            values[2].Should().Be(TimeSpan.FromSeconds(90_061));

            _output.WriteLine($"T340 MySQL TIME(6) raw: GetFieldType={fieldTypes[0]}, " +
                              $"GetValue types={string.Join(",", values.Select(v => v!.GetType().FullName))}, " +
                              $"values={string.Join(",", values.Select(v => ((TimeSpan)v!).ToString()))}");
        }
        finally
        {
            DropFixture();
        }
    }

    private void CreateFixture()
    {
        Execute($"drop table if exists {Table}");
        Execute($"create table {Table} (id bigint not null primary key, t time(6) null)");
        Execute($"insert into {Table} (id, t) values (1, '13:45:07.123456'), (2, '-01:02:03'), (3, '25:01:01')");
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
