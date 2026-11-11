using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// A mapped raw-source row whose only projected column is a PostgreSQL interval (T264): the raw-SQL
/// and CSV surfaces read through the same provider column mapper as an ordinary LINQ projection.
/// </summary>
[SqlTable("simple_entity")]
public sealed class T264RawIntervalRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("v")]
    public TimeSpan? V { get; set; }

    /// <summary>A second column so the projection is materialised by the buffered row mapper.</summary>
    [Column("k")]
    public int K { get; set; }
}

/// <summary>
/// Container-backed execution tests for PostgreSQL <c>interval</c> → <see cref="TimeSpan"/>
/// materialisation (T264, issue #264). A calendar month has no lossless <see cref="TimeSpan"/>
/// representation, so a mapped interval carrying one — either sign — must throw
/// <see cref="InvalidCastException"/>; zero-month days/microseconds convert exactly, and
/// <c>time</c>-of-day mappings are unchanged. A run where PostgreSQL is skipped is not evidence.
/// </summary>
[Collection("Postgres")]
public sealed class T264IntervalMaterializationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    /// <summary>1 day 02:03:04 as PostgreSQL reports it (days = 1, time = 7_384_000_000 µs).</summary>
    private const long OneDayTwoHoursTicks = 937_840_000_000L;

    private static readonly DateTime AgeFrom = new(2023, 5, 17, 13, 45, 30);

    /// <summary>Both signs of a constant calendar month are rejected, never approximated or dropped.</summary>
    [Fact]
    public void MakeIntervalRejectsSignedConstantMonths()
    {
        // The projection carries a second column so the buffered row mapper (not the scalar terminal)
        // materialises the interval; the scalar path is out of scope by plan.
        AssertMonthRejected(
            () => _sut.ComplexEntity.Where(x => x.Id == 1)
                .Select(x => new { x.Id, V = SqlFunctions.Postgres.make_interval(0, 1, 0, 0, 0, 0.0) }).First(),
            "+1 month constant");

        AssertMonthRejected(
            () => _sut.ComplexEntity.Where(x => x.Id == 1)
                .Select(x => new { x.Id, V = SqlFunctions.Postgres.make_interval(0, -1, 0, 0, 0, 0.0) }).First(),
            "-1 month constant");

        AssertMonthRejected(
            () => _sut.ComplexEntity.Where(x => x.Id == 1)
                .Select(x => new { x.Id, V = SqlFunctions.Postgres.make_interval(0, -1, 2, 0, 0, 0.0) }).First(),
            "-1 month + 2 days constant");
    }

    /// <summary>
    /// A runtime month parameter is evaluated from the value of each returned interval: the cached
    /// mapper reads zero-month rows exactly and rejects a month-bearing row, either sign.
    /// </summary>
    [Fact]
    public void MakeIntervalChecksRuntimeMonthsAcrossCachedExecutions()
    {
        var command = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                x.Id,
                V = SqlFunctions.Postgres.make_interval(0, SqlFunctions.Parameter<int>(0), 0, 0, 0, 0.0),
            });

        command.First(0).V.Should().Be(TimeSpan.Zero);
        AssertMonthRejected(() => command.First(1), "runtime +1 month");
        command.First(0).V.Should().Be(TimeSpan.Zero, "the decision is per value and must not stick");
        AssertMonthRejected(() => command.First(-1), "runtime -1 month");
    }

    /// <summary>Zero-month values convert exactly and a SQL NULL interval stays null.</summary>
    [Fact]
    public void MakeIntervalPreservesZeroMonthAndNullableResults()
    {
        var built = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { x.Id, V = SqlFunctions.Postgres.make_interval(0, 0, 1, 2, 3, 4.0) }).First();
        built.V.Should().Be(TimeSpan.FromTicks(OneDayTwoHoursTicks));

        var zero = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { x.Id, V = SqlFunctions.Postgres.make_interval(0, 0, 0, 0, 0, 0.0) }).First();
        zero.V.Should().Be(TimeSpan.Zero);

        var nullInterval = _sut.DataProvider.FromSql("select null::interval as v, 1 as k")
            .BindEntity<T264RawIntervalRow>(["v", "k"])
            .Select(x => new { x.V, x.K })
            .First();
        nullInterval.V.Should().BeNull();
        nullInterval.K.Should().Be(1);
    }

    /// <summary>
    /// <c>justify_days</c>/<c>justify_hours</c>/<c>age</c> follow the components of their result:
    /// a normalized day/time result is exact, a result carrying whole months is rejected.
    /// </summary>
    [Fact]
    public void NormalizedAndAgeIntervalsFollowResultComponents()
    {
        var justifiedHours = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                x.Id,
                V = SqlFunctions.Postgres.justify_hours(
                    SqlFunctions.Postgres.make_interval(0, 0, 0, 30, 0, 0.0)),
            }).First();
        justifiedHours.V.Should().Be(TimeSpan.FromHours(30));

        var justifiedDays = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                x.Id,
                V = SqlFunctions.Postgres.justify_days(
                    SqlFunctions.Postgres.make_interval(0, 0, 20, 0, 0, 0.0)),
            }).First();
        justifiedDays.V.Should().Be(TimeSpan.FromDays(20));

        AssertMonthRejected(
            () => _sut.ComplexEntity.Where(x => x.Id == 1)
                .Select(x => new
                {
                    x.Id,
                    V = SqlFunctions.Postgres.justify_days(
                        SqlFunctions.Postgres.make_interval(0, 1, 0, 0, 0, 0.0)),
                }).First(),
            "justify_days of a month-bearing interval");

        var tenDays = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { x.Id, V = SqlFunctions.Postgres.age(AgeFrom, AgeFrom.AddDays(-10)) }).First();
        tenDays.V.Should().Be(TimeSpan.FromDays(10));

        AssertMonthRejected(
            () => _sut.ComplexEntity.Where(x => x.Id == 1)
                .Select(x => new { x.Id, V = SqlFunctions.Postgres.age(AgeFrom, AgeFrom.AddMonths(-1)) }).First(),
            "age whose result carries a whole month");
    }

    /// <summary>A <c>time</c>-of-day projection still materialises as <see cref="TimeSpan"/>.</summary>
    [Fact]
    public void TimeOfDayMappingIsUnchanged()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                LocalTime = SqlFunctions.Postgres.localtime(),
                Built = SqlFunctions.Postgres.make_time(10, 11, 12.5),
            })
            .First();

        r.LocalTime.Should().NotBeNull();
        r.Built.Should().Be(new TimeSpan(10, 11, 12) + TimeSpan.FromMilliseconds(500));
    }

    /// <summary>
    /// The same rule reaches a raw-SQL entity column and the typed CSV terminal: an exact zero-month
    /// interval is written, a month-bearing one throws instead of producing a truncated value.
    /// </summary>
    [Fact]
    public void RawEntityAndTypedCsvUseIntervalValidation()
    {
        var rawExact = _sut.DataProvider.FromSql("select make_interval(0, 0, 0, 1, 2, 3, 4) as v, 1 as k")
            .BindEntity<T264RawIntervalRow>(["v", "k"])
            .Select(x => new { x.V, x.K })
            .First();
        rawExact.V.Should().Be(TimeSpan.FromTicks(OneDayTwoHoursTicks));

        AssertMonthRejected(
            () => _sut.DataProvider.FromSql("select make_interval(0, -1, 0, 0, 0, 0, 0) as v, 1 as k")
                .BindEntity<T264RawIntervalRow>(["v", "k"])
                .Select(x => new { x.V, x.K })
                .First(),
            "raw entity interval with -1 month");

        using var csv = new MemoryStream();
        _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.make_interval(0, 0, 1, 2, 3, 4.0) })
            .WriteCsv(csv, new CsvStreamOptions { IncludeHeader = false }, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(csv.ToArray()).Should().Be("1.02:03:04\r\n");

        using var rejected = new MemoryStream();
        AssertMonthRejected(
            () => _sut.ComplexEntity.Where(x => x.Id == 1)
                .Select(x => new { V = SqlFunctions.Postgres.make_interval(0, -1, 0, 0, 0, 0.0) })
                .WriteCsv(rejected, new CsvStreamOptions { IncludeHeader = false }, TestContext.Current.CancellationToken),
            "typed CSV of a month-bearing interval");
    }

    private static void AssertMonthRejected(Action act, string description)
    {
        var exception = act.Should().Throw<InvalidCastException>(description).Which;

        exception.Message.Should().Contain("calendar month");
        exception.Message.Should().Contain("no lossless TimeSpan representation");
    }
}
