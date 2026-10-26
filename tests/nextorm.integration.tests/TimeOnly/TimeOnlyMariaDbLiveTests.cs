using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T340 (#340) MariaDB reality, run on its own container through
/// <see cref="MariaDbContainer.ConnectionString"/> — never through the MySQL container. MariaDB
/// inherits MySQL's <c>TIME</c> duration semantics, so the mapped <see cref="TimeOnly"/> projection
/// must be refused with the MariaDB-specific policy (asserted at unit level in
/// <c>TimeOnlyMariaDbPolicyTests</c>); this class records the live raw driver representation only.
/// </summary>
public sealed class TimeOnlyMariaDbLiveTests : IDisposable
{
    private const string Table = "t340_time_probe";

    private readonly MariaDbDataContext _ctx;
    private readonly ITestOutputHelper _output;

    public TimeOnlyMariaDbLiveTests(ITestOutputHelper output)
    {
        _output = output;
        MariaDbContainer.IsAvailable.Should().BeTrue(
            "the MariaDB container must be reachable for the T340 reality probe: " +
            $"{MariaDbContainer.Failure ?? "unavailable without a reported reason"}");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());

        using var version = _ctx.ExecuteRaw("select version() as version");
        var banner = version.Read<string>().Single();
        banner.Should().Contain("MariaDB", "the probe must run against the MariaDB container");
        _output.WriteLine($"T340 MariaDB server version: {banner}");
    }

    public void Dispose() => _ctx.Dispose();

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

    /// <summary>AC3: MariaDB has no time of day, so the mapped projection is refused.</summary>
    [Fact]
    public void MappedTimeOnly_ShouldBeRefused()
    {
        CreateFixture();
        try
        {
            var act = FluentActions.Invoking(() => _ctx.From<ITimeProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.T })
                .ToList());

            var failure = act.Should().Throw<NotSupportedException>().Which;
            _output.WriteLine($"T340 MariaDB mapped TimeOnly refusal: {failure.Message}");
            failure.Message.Should().Contain("TimeOnly");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>CC4: the <see cref="TimeSpan"/> projection keeps MariaDB's duration semantics.</summary>
    [Fact]
    public void TimeSpanControl_ShouldKeepDurationSemantics()
    {
        CreateFixture();
        try
        {
            var rows = _ctx.From<ISpanProbe>().OrderBy(x => x.Id).Select(x => x.T).ToList();

            rows[0].Should().Be(TimeSpan.FromTicks(495_071_234_560));
            rows[1].Should().Be(-TimeSpan.FromSeconds(3723));
            rows[2].Should().Be(TimeSpan.FromSeconds(90_061));
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC3: the native driver representation of a MariaDB <c>TIME(6)</c> value.</summary>
    [Fact]
    public void NativeTimeRepresentation_ShouldBeObserved()
    {
        CreateFixture();
        try
        {
            _ctx.EnsureConnectionOpen();
            using var command = _ctx.CreateCommand($"select id, t from {Table} order by id");
            using var reader = command.ExecuteReader();

            _output.WriteLine($"T340 MariaDB ADO connection: {command.Connection!.GetType().FullName}");

            var fieldTypes = new List<string>();
            var values = new List<object?>();
            while (reader.Read())
            {
                fieldTypes.Add(reader.GetFieldType(1).FullName!);
                values.Add(reader.GetValue(1));
            }

            fieldTypes.Should().OnlyContain(x => x == "System.TimeSpan");
            values[0].Should().Be(TimeSpan.FromTicks(495_071_234_560));
            values[1].Should().Be(-TimeSpan.FromSeconds(3723));
            values[2].Should().Be(TimeSpan.FromSeconds(90_061));

            _output.WriteLine($"T340 MariaDB TIME(6) raw: GetFieldType={fieldTypes[0]}, " +
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
        Execute($"create table {Table} (id bigint not null primary key, t time(6) null) engine=InnoDB");
        Execute($"insert into {Table} (id, t) values (1, '13:45:07.123456'), (2, '-01:02:03'), (3, '25:01:01')");
    }

    private void DropFixture() => Execute($"drop table if exists {Table}");

    private void Execute(string sql)
    {
        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
