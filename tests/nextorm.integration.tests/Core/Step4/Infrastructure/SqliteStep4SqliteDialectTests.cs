using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 1 — the SQLite provider rendering surface on a live database:
/// <c>src/nextorm.sqlite/SqliteDialect.cs</c> (the date add/diff/part switch arms, the greatest/least
/// emulation and the CLR date-method translation) and <c>SqliteScalarFunctions</c> (the enrolled
/// cross-provider scalars plus the names the provider deliberately does not have).
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4SqliteDialectTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_dial_row (id integer primary key, dt datetime null);
        delete from s4_dial_row;
        insert into s4_dial_row (id, dt) values
            (1, '2023-01-01 00:00:00'),
            (2, '2023-01-02 01:02:03'),
            (3, '2024-04-01 00:00:00'),
            (4, null),
            (5, '2023-01-01 00:00:01');
        """;

    public SqliteStep4SqliteDialectTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>The sub-day and calendar date_add fields shift the anchor deterministically.</summary>
    [Fact]
    public void DateAdd_SubDayAndCalendarFields_ShouldShiftTheAnchor()
    {
        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Second = SqlFunctions.Sql.date_add("second", 1, x.Dt),
                Minute = SqlFunctions.Sql.date_add("minute", 1, x.Dt),
                Hour = SqlFunctions.Sql.date_add("hour", 1, x.Dt),
                Day = SqlFunctions.Sql.date_add("day", 1, x.Dt),
                Week = SqlFunctions.Sql.date_add("week", 1, x.Dt),
                Month = SqlFunctions.Sql.date_add("month", 1, x.Dt),
                Quarter = SqlFunctions.Sql.date_add("quarter", 1, x.Dt),
                Year = SqlFunctions.Sql.date_add("year", 1, x.Dt)
            })
            .Single();

        r.Second.Should().Be(At("2023-01-01 00:00:01"));
        r.Minute.Should().Be(At("2023-01-01 00:01:00"));
        r.Hour.Should().Be(At("2023-01-01 01:00:00"));
        r.Day.Should().Be(At("2023-01-02 00:00:00"));
        r.Week.Should().Be(At("2023-01-08 00:00:00"));
        r.Month.Should().Be(At("2023-02-01 00:00:00"));
        r.Quarter.Should().Be(At("2023-04-01 00:00:00"));
        r.Year.Should().Be(At("2024-01-01 00:00:00"));
    }

    /// <summary>The three multi-year units multiply the amount before the shift.</summary>
    [Fact]
    public void DateAdd_MultiYearUnits_ShouldScaleTheAmount()
    {
        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Decade = SqlFunctions.Sql.date_add("decade", 1, x.Dt),
                Century = SqlFunctions.Sql.date_add("century", 1, x.Dt),
                Millennium = SqlFunctions.Sql.date_add("millennium", 1, x.Dt)
            })
            .Single();

        r.Decade.Should().Be(At("2033-01-01 00:00:00"));
        r.Century.Should().Be(At("2123-01-01 00:00:00"));
        r.Millennium.Should().Be(At("3023-01-01 00:00:00"));
    }

    /// <summary>The two sub-second fields keep a fractional result inside the first second.</summary>
    [Fact]
    public void DateAdd_SubSecondFields_ShouldKeepFractionalSeconds()
    {
        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Milliseconds = SqlFunctions.Sql.date_add("milliseconds", 1, x.Dt),
                Microseconds = SqlFunctions.Sql.date_add("microseconds", 1, x.Dt)
            })
            .Single();

        r.Milliseconds.Should().NotBeNull();
        r.Milliseconds!.Value.Should().BeOnOrAfter(At("2023-01-01 00:00:00"))
            .And.BeBefore(At("2023-01-01 00:00:02"));
        r.Microseconds.Should().NotBeNull();
        r.Microseconds!.Value.Should().BeOnOrAfter(At("2023-01-01 00:00:00"))
            .And.BeBefore(At("2023-01-01 00:00:02"));
    }

    /// <summary>The two sub-second date_diff fields scale the one-second delta.</summary>
    [Fact]
    public void DateDiff_SubSecondFields_ShouldScaleTheDelta()
    {
        var end = At("2023-01-01 00:00:01");

        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Microseconds = SqlFunctions.Sql.date_diff("microseconds", x.Dt, end),
                Milliseconds = SqlFunctions.Sql.date_diff("milliseconds", x.Dt, end)
            })
            .Single();

        r.Microseconds.Should().Be(1_000_000);
        r.Milliseconds.Should().Be(1_000);
    }

    /// <summary>Every remaining date_diff field returns the exact integer delta of its unit.</summary>
    [Fact]
    public void DateDiff_CalendarAndTimeFields_ShouldComputeTheDelta()
    {
        var end = At("2024-04-01 00:00:00");

        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Second = SqlFunctions.Sql.date_diff("second", x.Dt, end),
                Minute = SqlFunctions.Sql.date_diff("minute", x.Dt, end),
                Hour = SqlFunctions.Sql.date_diff("hour", x.Dt, end),
                Day = SqlFunctions.Sql.date_diff("day", x.Dt, end),
                Week = SqlFunctions.Sql.date_diff("week", x.Dt, end),
                Month = SqlFunctions.Sql.date_diff("month", x.Dt, end),
                Quarter = SqlFunctions.Sql.date_diff("quarter", x.Dt, end),
                Year = SqlFunctions.Sql.date_diff("year", x.Dt, end)
            })
            .Single();

        r.Second.Should().Be(39_398_400);
        r.Minute.Should().Be(656_640);
        r.Hour.Should().Be(10_944);
        r.Day.Should().Be(456);
        r.Week.Should().Be(65);
        r.Month.Should().Be(15);
        r.Quarter.Should().Be(5);
        r.Year.Should().Be(1);
    }

    /// <summary>Every accepted integer date part returns the exact component of the anchor.</summary>
    [Fact]
    public void ExtractDatePart_ShouldComputeTheComponents()
    {
        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.extract("year", x.Dt),
                Quarter = SqlFunctions.Sql.extract("quarter", x.Dt),
                Month = SqlFunctions.Sql.extract("month", x.Dt),
                Week = SqlFunctions.Sql.extract("week", x.Dt),
                Day = SqlFunctions.Sql.extract("day", x.Dt),
                DayOfYear = SqlFunctions.Sql.extract("doy", x.Dt),
                Hour = SqlFunctions.Sql.extract("hour", x.Dt),
                Minute = SqlFunctions.Sql.extract("minute", x.Dt),
                Second = SqlFunctions.Sql.extract("second", x.Dt),
                DayOfWeek = SqlFunctions.Sql.extract("dow", x.Dt),
                IsoDayOfWeek = SqlFunctions.Sql.extract("isodow", x.Dt)
            })
            .Single();

        r.Year.Should().Be(2024);
        r.Quarter.Should().Be(2);
        r.Month.Should().Be(4);
        r.Week.Should().Be(14);
        r.Day.Should().Be(1);
        r.DayOfYear.Should().Be(92);
        r.Hour.Should().Be(0);
        r.Minute.Should().Be(0);
        r.Second.Should().Be(0);
        r.DayOfWeek.Should().Be(1);
        r.IsoDayOfWeek.Should().Be(1);
    }

    /// <summary>The numeric surface provides only the epoch part and computes it.</summary>
    [Fact]
    public void DatePartEpoch_ShouldComputeTheUnixTime()
    {
        var value = Context.From<S4DialRow>()
            .Where(x => x.Id == 3)
            .Select(x => SqlFunctions.Sql.date_part("epoch", x.Dt))
            .Single();

        value.Should().BeApproximately(1_711_929_600d, 1d);
    }

    /// <summary>The CLR date methods route through the same date-add surface.</summary>
    [Fact]
    public void DateTimeMethods_ShouldCompute()
    {
        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Days = x.Dt!.Value.AddDays(2),
                Months = x.Dt!.Value.AddMonths(2),
                Years = x.Dt!.Value.AddYears(1),
                Hours = x.Dt!.Value.AddHours(2),
                Minutes = x.Dt!.Value.AddMinutes(2),
                Seconds = x.Dt!.Value.AddSeconds(2),
                Milliseconds = x.Dt!.Value.AddMilliseconds(500)
            })
            .Single();

        r.Days.Should().Be(At("2023-01-03 00:00:00"));
        r.Months.Should().Be(At("2023-03-01 00:00:00"));
        r.Years.Should().Be(At("2024-01-01 00:00:00"));
        r.Hours.Should().Be(At("2023-01-01 02:00:00"));
        r.Minutes.Should().Be(At("2023-01-01 00:02:00"));
        r.Seconds.Should().Be(At("2023-01-01 00:00:02"));
        r.Milliseconds.Should().BeOnOrAfter(At("2023-01-01 00:00:00"))
            .And.BeBefore(At("2023-01-01 00:00:01"));
    }

    /// <summary>The enrolled cross-provider scalars compute on SQLite; the two <c>right</c> arms differ.</summary>
    [Fact]
    public void CrossProviderScalars_ShouldCompute()
    {
        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Left2 = SqlFunctions.Sql.left("abcde", 2),
                Right2 = SqlFunctions.Sql.right("abcde", 2),
                RightLong = SqlFunctions.Sql.right("abc", 10),
                ConcatWs = SqlFunctions.Sql.concat_ws("-", "a", "b", "c"),
                Ascii = SqlFunctions.Sql.ascii("A"),
                Char = SqlFunctions.Sql.@char(65),
                BitLength = SqlFunctions.Sql.bit_length("abc"),
                OctetLength = SqlFunctions.Sql.octet_length("abc"),
                Degrees = SqlFunctions.Sql.degrees(1.0),
                Radians = SqlFunctions.Sql.radians(180.0),
                Pi = SqlFunctions.Sql.pi()
            })
            .Single();

        r.Left2.Should().Be("ab");
        r.Right2.Should().Be("de");
        r.RightLong.Should().Be("abc");
        r.ConcatWs.Should().Be("a-b-c");
        r.Ascii.Should().Be(65);
        r.Char.Should().Be("A");
        r.BitLength.Should().Be(24);
        r.OctetLength.Should().Be(3);
        r.Degrees.Should().BeApproximately(57.29577951308232, 1e-12);
        r.Radians.Should().BeApproximately(Math.PI, 1e-12);
        r.Pi.Should().BeApproximately(Math.PI, 1e-12);
    }

    /// <summary>A name the provider does not enrol is refused before SQL by its own name.</summary>
    [Fact]
    public void MissingScalars_ShouldBeRefusedByName()
    {
        FluentActions.Invoking(() => Context.From<S4DialRow>()
                .Select(x => SqlFunctions.Sql.cot(1.0)).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The cot function is not supported by this provider*");

        FluentActions.Invoking(() => Context.From<S4DialRow>()
                .Select(x => SqlFunctions.Sql.lpad("a", 3)).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The lpad function is not supported by this provider*");

        FluentActions.Invoking(() => Context.From<S4DialRow>()
                .Select(x => SqlFunctions.Sql.repeat("a", 3)).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The repeat function is not supported by this provider*");

        FluentActions.Invoking(() => Context.From<S4DialRow>()
                .Select(x => SqlFunctions.Sql.translate("a", "a", "b")).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The translate function is not supported by this provider*");
    }

    /// <summary>Greatest/least take the multi-argument and the single-argument arm.</summary>
    [Fact]
    public void GreatestLeast_ShouldComputeBothArities()
    {
        var r = Context.From<S4DialRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Max = SqlFunctions.Sql.greatest(3, 5, 9),
                Min = SqlFunctions.Sql.least(3, 5, 9),
                Single = SqlFunctions.Sql.greatest(7),
                SingleLeast = SqlFunctions.Sql.least(7)
            })
            .Single();

        r.Max.Should().Be(9);
        r.Min.Should().Be(3);
        r.Single.Should().Be(7);
        r.SingleLeast.Should().Be(7);
    }

    private static DateTime At(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture);

    /// <summary>Marker source of the SQLite dialect facts.</summary>
    [SqlTable("s4_dial_row")]
    public sealed class S4DialRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("dt")]
        public DateTime? Dt { get; set; }
    }
}
