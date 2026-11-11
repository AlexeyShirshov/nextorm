using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Converts a <see cref="TimeOnly"/> model value through the driver's native <see cref="TimeSpan"/>
/// (MySQL/MariaDB <c>TIME</c>). Used to prove that a converter-owned projection keeps its existing route
/// and never enters the unconverted refusal.
/// </summary>
public sealed class TimeOnlyToTimeSpanConverter : ValueConverter<TimeOnly, TimeSpan>
{
    public override TimeSpan ConvertToProvider(TimeOnly model) => model.ToTimeSpan();

    public override TimeOnly ConvertFromProvider(TimeSpan provider) => TimeOnly.FromTimeSpan(provider);
}

/// <summary>
/// T340 (#340) MySQL reality: <c>TIME</c> is a duration, not a time of day. The mapped
/// <see cref="TimeOnly"/> projection must be refused with the frozen MySQL text, the unchanged
/// <see cref="TimeSpan"/> projection keeps working, and the native driver value is observed raw
/// (<c>GetFieldType</c>/<c>GetValue</c>) — including the negative and over-24 h durations MySQL accepts.
/// <para>
/// The refusal is a mapping policy, so it fires for a row whose value is <c>NULL</c> as well; a projection
/// that opts in through <see cref="TimeOnlyToTimeSpanConverter"/> keeps its converter-owned route and is
/// <b>not</b> refused. ClickHouse/SQLite cannot express that converter (their storage types are not
/// <see cref="TimeSpan"/>), so the converter cell there is <c>na</c> — see <c>E/provider-reality.md</c>.
/// </para>
/// </summary>
[Collection("MySql")]
public sealed class TimeOnlyMySqlLiveTests : ProviderTestSuite
{
    private const string Table = "t340_time_probe";

    private const string Refusal =
        "TimeOnly projection refused for MySQL: TIME is a duration; use TimeSpan or an explicit value converter.";

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
    public interface IConvertedProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("t")]
        [ValueConverter(typeof(TimeOnlyToTimeSpanConverter))]
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

    /// <summary>AC3: MySQL has no time of day, so the mapped projection is refused with the frozen text.</summary>
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
            var act = FluentActions.Invoking(() => Context.From<ITimeProbe>()
                .Where(x => x.Id == 4)
                .Select(x => x.T)
                .First());

            act.Should().Throw<NotSupportedException>().WithMessage(Refusal);
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>CC4/converter: an explicit converter keeps its own route instead of the refusal.</summary>
    [Fact]
    public void ConverterOwnedProjection_ShouldStillWork()
    {
        CreateFixture();
        try
        {
            // Rows 2 and 3 are out-of-domain durations (`-01:02:03`, `25:01:01`): a TimeOnly model cannot
            // represent them, so the converter is expected to fail there — that is the converter owner's
            // contract, not this task's. The claim here is only that the in-domain and NULL rows keep their
            // converter-owned route instead of the refusal.
            var inDomain = Context.From<IConvertedProbe>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Id, x.T })
                .ToList();
            inDomain.Single().T!.Value.Ticks.Should().Be(495_071_234_560);

            var nullRow = Context.From<IConvertedProbe>()
                .Where(x => x.Id == 4)
                .Select(x => new { x.Id, x.T })
                .ToList();
            nullRow.Single().T.Should().BeNull();
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
            rows[3].Should().BeNull();
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
                              $"GetValue types={string.Join(",", values.Select(Describe))}, " +
                              $"values={string.Join(",", values.Select(Format))}");
        }
        finally
        {
            DropFixture();
        }
    }

    private static string Describe(object? value) => value is null or DBNull ? "NULL" : value.GetType().FullName!;

    private static string Format(object? value) => value switch
    {
        TimeSpan span => span.ToString(),
        null or DBNull => "NULL",
        _ => value.ToString() ?? "NULL",
    };

    private void CreateFixture()
    {
        Execute($"drop table if exists {Table}");
        Execute($"create table {Table} (id bigint not null primary key, t time(6) null)");
        Execute(
            $"insert into {Table} (id, t) values (1, '13:45:07.123456'), (2, '-01:02:03'), (3, '25:01:01'), (4, null)");
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
