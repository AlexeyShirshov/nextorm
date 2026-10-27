using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// T340 (#340) SQLite reality on a task-private file database (<c>Data Source=…</c>), no container.
/// SQLite has no time-of-day type: a <see cref="TimeSpan"/> is stored as an integer tick count and a
/// text column would need parsing, which the projection must never do silently. The mapped
/// <see cref="TimeOnly"/> projection is therefore refused, and the control shows the unchanged
/// integer-backed <see cref="TimeSpan"/> route.
/// </summary>
public sealed class TimeOnlySqliteLiveTests : IDisposable
{
    private const string Table = "t340_time_probe";

    private const string Refusal =
        "TimeOnly projection refused for SQLite: there is no native time-of-day type; use an explicit value converter.";

    private readonly string _path;
    private readonly SqliteDataContext _ctx;
    private readonly ITestOutputHelper _output;

    public TimeOnlySqliteLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _path = Path.Combine(Path.GetTempPath(), $"nextorm.t340.{Environment.ProcessId}.{Guid.NewGuid():N}.db");
        _ctx = new SqliteDataContext($"Data Source='{_path}'", new DataContextBuilder());
    }

    public void Dispose()
    {
        _ctx.Dispose();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [SqlTable(Table)]
    public interface ITimeProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("ticks")]
        TimeOnly? Ticks { get; set; }
    }

    [SqlTable(Table)]
    public interface ISpanProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("ticks")]
        TimeSpan? Ticks { get; set; }
    }

    /// <summary>AC3: no native time of day, so the mapped projection is refused.</summary>
    [Fact]
    public void MappedTimeOnly_ShouldBeRefused()
    {
        CreateFixture();
        try
        {
            var act = FluentActions.Invoking(() => _ctx.From<ITimeProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Ticks })
                .ToList());

            var failure = act.Should().Throw<NotSupportedException>().Which;
            _output.WriteLine($"T340 SQLite mapped TimeOnly refusal: {failure.Message}");
            failure.Message.Should().Be(Refusal);
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>The refusal is a policy, not a value check: a <c>NULL</c> row is refused too.</summary>
    [Fact]
    public void MappedTimeOnly_ShouldRefuseEvenForNullRows()
    {
        CreateFixture();
        try
        {
            var act = FluentActions.Invoking(() => _ctx.From<ITimeProbe>()
                .Where(x => x.Id == 2)
                .Select(x => x.Ticks)
                .First());

            act.Should().Throw<NotSupportedException>().WithMessage(Refusal);
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>CC4: the integer-backed <see cref="TimeSpan"/> route keeps exact tick storage.</summary>
    [Fact]
    public void TimeSpanControl_ShouldReadIntegerTicks()
    {
        CreateFixture();
        try
        {
            var rows = _ctx.From<ISpanProbe>().OrderBy(x => x.Id).Select(x => x.Ticks).ToList();

            rows[0].Should().Be(TimeSpan.FromTicks(495_071_234_560));
            rows[1].Should().BeNull();
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC3: the physical storage of both shapes, observed through the driver's own reader.</summary>
    [Fact]
    public void NoNativeTimeOfDay_ShouldBeRecorded()
    {
        CreateFixture();
        try
        {
            _ctx.EnsureConnectionOpen();
            using var command = _ctx.CreateCommand($"select ticks, txt from {Table} order by id");
            using var reader = command.ExecuteReader();

            _output.WriteLine($"T340 SQLite ADO connection: {command.Connection!.GetType().FullName}");
            reader.Read().Should().BeTrue();

            var integerType = reader.GetFieldType(0).FullName;
            var textType = reader.GetFieldType(1).FullName;
            var integerValue = reader.GetValue(0);
            var textValue = reader.GetValue(1);

            integerType.Should().Be("System.Int64");
            textType.Should().Be("System.String");
            integerValue.Should().Be(495_071_234_560L);
            textValue.Should().Be("13:45:07.123456");

            _output.WriteLine($"T340 SQLite storage: ticks -> {integerType} ({integerValue}); " +
                              $"txt -> {textType} (\u0027{textValue}\u0027) — no time-of-day type exists");
        }
        finally
        {
            DropFixture();
        }
    }

    private void CreateFixture()
    {
        Execute($"drop table if exists {Table}");
        Execute($"create table {Table} (id integer primary key, ticks integer null, txt text null)");
        Execute($"insert into {Table} (id, ticks, txt) values (1, 495071234560, '13:45:07.123456'), (2, null, null)");
    }

    private void DropFixture() => Execute($"drop table if exists {Table}");

    private void Execute(string sql)
    {
        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
