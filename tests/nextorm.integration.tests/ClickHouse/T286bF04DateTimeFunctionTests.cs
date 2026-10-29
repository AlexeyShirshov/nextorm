using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D4 (F04 Date/time): live execution of the ClickHouse-only date/time members against the
/// pinned <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the server's range substitutions
/// (<c>Date</c>/<c>Date32</c>/<c>DateTime</c> out of range), the sub-hour and interval truncation
/// grids, the ISO/week/year-week mode table, the <c>toModifiedJulianDay</c> text domain (widened to
/// year 0..9999, which is why the member takes a string), the fixed-date legacy <c>toTime</c>, the
/// epoch variants with negative and overflow boundaries, DST-aware zone offsets, NULL propagation
/// and the three deterministic CLR-shape refusals.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF04DateTimeFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f04_entity")]
    internal interface IT286bDateTimeEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("dt")]
        DateTime Value { get; set; }

        [Column("ndt")]
        DateTime? NullableValue { get; set; }

        [Column("dur")]
        uint Duration { get; set; }

        [Column("mode")]
        int Mode { get; set; }

        [Column("s")]
        string Text { get; set; }

        [Column("unit")]
        string Unit { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private T Scalar<T>(string expression)
    {
        using var context = Provider.CreateContext();
        using var result = context.ExecuteRaw($"select {expression} as v");
        return result.Read<T>().Single();
    }

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f04_entity",
                     "create table t286b_f04_entity (id Int32, dt DateTime64(6), ndt Nullable(DateTime64(6)), "
                     + "dur UInt32, mode Int32, s String, unit String) engine = Memory",
                     "insert into t286b_f04_entity (id, dt, ndt, dur, mode, s, unit) values "
                     + "(1, '2024-01-02 03:44:59.123456', '2024-01-02 03:44:59.123456', 1800, 3, '2024-01-02', 'year'), "
                     + "(2, '2024-12-31 23:30:00.000000', null, 0, 0, '2023-12-31', 'day'), "
                     + "(3, '2023-12-31 23:30:00.000000', '2023-12-31 23:30:00.000000', 900, 9, '0000-01-01', 'month')"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    private static DateTime Dt(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int micro = 0)
        => new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc).AddTicks(micro * 10L);

    private static void ShouldBeInstant(DateTime? actual, DateTime expected, string? because = null)
    {
        actual.Should().NotBeNull(because);
        actual!.Value.Ticks.Should().Be(expected.Ticks, because);
    }

    [Fact]
    public void MakeDate_ShouldBuildDatesAndPinTheServerRangeSubstitutions()
    {
        using (new AssertionScope())
        {
            ShouldBeInstant(Scalar<DateTime>("makeDate(2024, 2, 29)"), Dt(2024, 2, 29));
            ShouldBeInstant(Scalar<DateTime>("makeDate(2024, 60)"), Dt(2024, 2, 29));

            Scalar<string>("toTypeName(makeDate(2024, 2, 29))").Should().Be("Date");
            Scalar<string>("toTypeName(makeDate32(2024, 2, 29))").Should().Be("Date32");

            // The server normalizes a day past the month and substitutes the epoch outside the type
            // range; the client neither clamps nor rejects these (they are the vendor's contract).
            ShouldBeInstant(Scalar<DateTime>("makeDate(2024, 2, 31)"), Dt(2024, 3, 2));
            ShouldBeInstant(Scalar<DateTime>("makeDate(1969, 12, 31)"), Dt(1970, 1, 1), "below the Date range -> epoch");
            ShouldBeInstant(Scalar<DateTime>("makeDate(2149, 6, 7)"), Dt(1970, 1, 1), "above the Date range -> epoch");
            ShouldBeInstant(Scalar<DateTime>("makeDate(2024, 366)"), Dt(1970, 1, 1), "a day past the year -> epoch");

            ShouldBeInstant(Scalar<DateTime>("makeDate32(1900, 1, 1)"), Dt(1900, 1, 1));
            ShouldBeInstant(Scalar<DateTime>("makeDate32(2299, 12, 31)"), Dt(2299, 12, 31));
            ShouldBeInstant(Scalar<DateTime>("makeDate32(1899, 12, 31)"), Dt(1970, 1, 1), "below Date32 -> epoch");
            ShouldBeInstant(Scalar<DateTime>("makeDate32(2300, 1, 1)"), Dt(1970, 1, 1), "above Date32 -> epoch");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Date = SqlFunctions.ClickHouse.make_date(2024, 2, 29),
                DayOfYear = SqlFunctions.ClickHouse.make_date(2024, 60),
                Date32 = SqlFunctions.ClickHouse.make_date32(2299, 12, 31)
            })
            .First();

        ShouldBeInstant(row.Date, Dt(2024, 2, 29));
        ShouldBeInstant(row.DayOfYear, Dt(2024, 2, 29));
        ShouldBeInstant(row.Date32, Dt(2299, 12, 31));
        row.Date.GetType().Should().Be(typeof(DateTime));
    }

    [Fact]
    public void MakeDateTime_ShouldBuildDateTimeAndNormalizeOutOfRangeParts()
    {
        using (new AssertionScope())
        {
            ShouldBeInstant(Scalar<DateTime>("makeDateTime(2024, 1, 2, 3, 4, 5)"), Dt(2024, 1, 2, 3, 4, 5));
            ShouldBeInstant(Scalar<DateTime>("makeDateTime(2024, 1, 1, 0, 0, 60)"), Dt(2024, 1, 1, 0, 1, 0),
                "a second past 59 rolls into the next minute");
            ShouldBeInstant(Scalar<DateTime>("makeDateTime(1969, 12, 31, 23, 59, 59)"), Dt(1970, 1, 1),
                "below the DateTime range -> epoch");
            Scalar<string>("toTypeName(makeDateTime(2024, 1, 2, 3, 4, 5))").Should().Be("DateTime");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var value = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.make_date_time(2024, 1, 2, 3, 4, 5))
            .First();

        ShouldBeInstant(value, Dt(2024, 1, 2, 3, 4, 5));
    }

    [Fact]
    public void Now64AndNowInBlock_ShouldReportTickPrecisionAndBlockConstancy()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toTypeName(now64())").Should().Be("DateTime64(3)", "the vendor default scale is 3");
            Scalar<string>("toTypeName(now64(0))").Should().Be("DateTime64(0)");
            Scalar<string>("toTypeName(now64(6))").Should().Be("DateTime64(6)");
            Scalar<string>("toTypeName(now64(7))").Should().Be("DateTime64(7)");

            // nowInBlock is fixed within one block: one distinct value over 1000 rows.
            Scalar<long>("countDistinct(x) from (select nowInBlock() as x from numbers(1000))")
                .Should().Be(1L, "nowInBlock is constant per block");
            Scalar<string>("toTypeName(nowInBlock())").Should().Be("DateTime");
        }

        var before = DateTime.UtcNow;
        Scalar<DateTime>("now64(6)").Should().BeOnOrAfter(before.AddSeconds(-5), "the value is fresh, not constant-folded");
        Scalar<DateTime>("now64(6)").Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Precise = SqlFunctions.ClickHouse.now64(6),
                InBlock = SqlFunctions.ClickHouse.now_in_block()
            })
            .First();

        row.Precise.Should().BeOnOrAfter(before.AddSeconds(-5));
        row.InBlock.Should().BeOnOrAfter(before.AddSeconds(-5));
        row.Precise.GetType().Should().Be(typeof(DateTime));
    }

    [Fact]
    public void TimeSlotAndTimeSlots_ShouldRoundToHalfHourAndListBoundaries()
    {
        using (new AssertionScope())
        {
            ShouldBeInstant(Scalar<DateTime>("timeSlot(toDateTime('2024-01-02 03:44:59'))"), Dt(2024, 1, 2, 3, 30));
            ShouldBeInstant(Scalar<DateTime>("timeSlot(toDateTime('2024-01-02 03:45:00'))"), Dt(2024, 1, 2, 3, 30),
                "03:45:00 is still in the slot that started at 03:30");
            ShouldBeInstant(Scalar<DateTime>("timeSlot(toDateTime('2024-01-02 03:00:00'))"), Dt(2024, 1, 2, 3, 0));

            Scalar<string>("toString(timeSlots(toDateTime('2024-01-02 03:17:45'), toUInt32(1800)))")
                .Should().Be("['2024-01-02 03:00:00','2024-01-02 03:30:00']");
            Scalar<string>("toTypeName(timeSlots(toDateTime('2024-01-02 03:17:45'), toUInt32(1800)))")
                .Should().Be("Array(DateTime)");

            Scalar<long>("length(timeSlots(toDateTime('2024-01-02 03:17:45'), toUInt32(0)))")
                .Should().Be(1L, "a zero duration still yields the containing slot");
            Scalar<long>("length(timeSlots(toDateTime('2024-01-02 03:17:45'), toUInt32(900)))")
                .Should().Be(2L, "slots are 30-minute buckets, so a 900-second window still yields two");
            Scalar<string>("toTypeName(timeSlots(toDateTime64('2024-01-02 03:17:45.123456', 6), toDecimal64(1800, 0)))")
                .Should().Be("Array(DateTime64(6))", "a DateTime64 start needs a Decimal64 duration; the member casts the start to DateTime");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Slot = SqlFunctions.ClickHouse.time_slot(x.Value),
                Slots = SqlFunctions.ClickHouse.time_slots(x.Value, 1800)
            })
            .First();

        ShouldBeInstant(row.Slot, Dt(2024, 1, 2, 3, 30));
        row.Slots.Should().HaveCount(2);
        ShouldBeInstant(row.Slots[0], Dt(2024, 1, 2, 3, 30), "03:44:59 is in the slot that started at 03:30");
        ShouldBeInstant(row.Slots[1], Dt(2024, 1, 2, 4, 0), "the 1800-second window reaches 04:00");
        row.Slots.GetType().Should().Be(typeof(DateTime[]));
    }

    [Fact]
    public void ToStartOfSubHourAndInterval_ShouldTruncateOnTheVendorGrid()
    {
        using (new AssertionScope())
        {
            ShouldBeInstant(Scalar<DateTime>("toStartOfFifteenMinutes(toDateTime('2024-01-02 03:44:59'))"), Dt(2024, 1, 2, 3, 30));
            ShouldBeInstant(Scalar<DateTime>("toStartOfFiveMinutes(toDateTime('2024-01-02 03:44:59'))"), Dt(2024, 1, 2, 3, 40));
            ShouldBeInstant(Scalar<DateTime>("toStartOfTenMinutes(toDateTime('2024-01-02 03:44:59'))"), Dt(2024, 1, 2, 3, 40));

            Scalar<long>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 1 DAY) = toStartOfDay(toDateTime('2024-01-02 03:44:59'))")
                .Should().Be(1L, "the day form equals toStartOfDay");
            ShouldBeInstant(Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 1 WEEK)"),
                Dt(2024, 1, 1), "weeks are anchored on Monday (01-05-1970 for this grid)");
            ShouldBeInstant(Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 7 MINUTE)"),
                Dt(2024, 1, 2, 3, 40), "sub-hour grids are anchored at the epoch, not at the hour");
            ShouldBeInstant(Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 1 WEEK, toDateTime('2024-01-02 00:00:00'))"),
                Dt(2024, 1, 2), "the origin anchors the grid");
            ShouldBeInstant(Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 1 QUARTER)"),
                Dt(2024, 1, 1));
            ShouldBeInstant(Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 2 YEAR)"),
                Dt(2024, 1, 1));
        }

        FluentActions.Invoking(() => Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 0 DAY)"))
            .Should().Throw<Exception>().WithMessage("*must be positive*", "the server refuses a zero interval");
        FluentActions.Invoking(() => Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL -1 DAY)"))
            .Should().Throw<Exception>().WithMessage("*must be positive*");
        FluentActions.Invoking(() => Scalar<DateTime>("toStartOfInterval(toDateTime('2024-01-02 03:44:59'), INTERVAL 1 MILLISECOND)"))
            .Should().Throw<Exception>().WithMessage("*Illegal interval kind*",
                "a DateTime argument has no sub-second grid on the server");

        Seed();
        using var ctx = Provider.CreateContext();
        var origin = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fifteen = SqlFunctions.ClickHouse.to_start_of_fifteen_minutes(x.Value),
                Interval = SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "day"),
                Anchored = SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "week", origin)
            })
            .First();

        ShouldBeInstant(row.Fifteen, Dt(2024, 1, 2, 3, 30));
        ShouldBeInstant(row.Interval, Dt(2024, 1, 2));
        ShouldBeInstant(row.Anchored, Dt(2024, 1, 2));
    }

    [Fact]
    public void IsoYearAndWeekAndYearWeek_ShouldPinTheModeTable()
    {
        using (new AssertionScope())
        {
            Scalar<int>("toISOYear(toDate('2024-01-01'))").Should().Be(2024);
            Scalar<int>("toISOYear(toDate('2021-01-01'))").Should().Be(2020, "the ISO year precedes the calendar year");
            Scalar<int>("toISOYear(toDate('2020-12-31'))").Should().Be(2020);
            Scalar<string>("toTypeName(toISOYear(toDate('2024-01-01')))").Should().Be("UInt16");

            // The vendor's ten modes for 2024-01-01 (a Monday): 0 -> 0, 1 -> 1, 2 -> 53, 3..9 -> 1.
            int[] expected = [0, 1, 53, 1, 1, 1, 1, 1, 1, 1];
            for (var mode = 0; mode < expected.Length; mode++)
                Scalar<int>($"toWeek(toDate('2024-01-01'), {mode})").Should().Be(
                    expected[mode], "toWeek(2024-01-01, {0}) is the vendor table value", mode);

            Scalar<int>("toWeek(toDate('2024-12-31'), 0)").Should().Be(52);
            Scalar<int>("toWeek(toDate('2024-12-31'), 3)").Should().Be(1, "mode 3 is the ISO week");
            Scalar<int>("toWeek(toDate('2024-01-07'), 0)").Should().Be(1);

            // toYearWeek follows the week into the neighbouring year; toWeek reports it in the
            // context of the date's own year.
            Scalar<int>("toYearWeek(toDate('2024-01-01'))").Should().Be(202353);
            Scalar<int>("toYearWeek(toDate('2024-01-01'), 3)").Should().Be(202401);
            Scalar<int>("toYearWeek(toDate('2020-12-31'), 0)").Should().Be(202052);
            Scalar<int>("toWeek(toDate('2020-12-31'), 0)").Should().Be(52);
            Scalar<string>("toTypeName(toWeek(toDate('2024-01-01')))").Should().Be("UInt8");
            Scalar<string>("toTypeName(toYearWeek(toDate('2024-01-01')))").Should().Be("UInt32");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                IsoYear = SqlFunctions.ClickHouse.to_iso_year(x.Value),
                Week = SqlFunctions.ClickHouse.to_week(x.Value, 3),
                YearWeek = SqlFunctions.ClickHouse.to_year_week(x.Value, 3)
            })
            .First();

        row.IsoYear.Should().Be(2023, "2023-12-31 is in ISO year 2023");
        row.Week.Should().Be(52);
        row.YearWeek.Should().Be(202352);
        row.IsoYear.GetType().Should().Be(typeof(int));
    }

    [Fact]
    public void ToLastDayOfWeekAndDaysInMonth_ShouldPinModesAndTheVendorDefinition()
    {
        using (new AssertionScope())
        {
            for (var mode = 0; mode <= 9; mode++)
            {
                var expected = mode % 2 == 0 ? Dt(2024, 1, 6) : Dt(2024, 1, 7);
                ShouldBeInstant(Scalar<DateTime>($"toLastDayOfWeek(toDate('2024-01-03'), {mode})"), expected);
            }

            ShouldBeInstant(Scalar<DateTime>("toLastDayOfWeek(toDate('2024-01-03'))"), Dt(2024, 1, 6),
                "the default mode 0 ends the week on Saturday");
            Scalar<string>("toTypeName(toLastDayOfWeek(toDate('2024-01-03')))").Should().Be("Date");

            Scalar<int>("toInt32(toDayOfMonth(toLastDayOfMonth(toDate('2024-02-01'))))").Should().Be(29);
            Scalar<int>("toInt32(toDayOfMonth(toLastDayOfMonth(toDate('2023-02-01'))))").Should().Be(28);
            Scalar<int>("toInt32(toDayOfMonth(toLastDayOfMonth(toDate('2024-04-01'))))").Should().Be(30);
            Scalar<string>("toTypeName(toDayOfMonth(toLastDayOfMonth(toDate('2024-02-01'))))").Should().Be("UInt8",
                "the vendor definition keeps the declared UInt8 type");
        }

        // The catalog name itself arrived in 26.3; the arm renders the vendor definition instead.
        FluentActions.Invoking(() => Scalar<int>("toDaysInMonth(toDate('2024-02-01'))"))
            .Should().Throw<Exception>().WithMessage("*UNKNOWN_FUNCTION*",
                "the pinned 25.8 server has no toDaysInMonth, which is why the arm is the definition");

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                LastDay = SqlFunctions.ClickHouse.to_last_day_of_week(x.Value, 1),
                Days = SqlFunctions.ClickHouse.to_days_in_month(x.Value)
            })
            .First();

        ShouldBeInstant(row.LastDay, Dt(2024, 1, 7));
        row.Days.Should().Be(31);
        row.Days.GetType().Should().Be(typeof(int));
    }

    [Fact]
    public void ModifiedJulianDayAndTimeAndTimestamp_ShouldConvertTextAndTimeOfDay()
    {
        using (new AssertionScope())
        {
            Scalar<int>("toModifiedJulianDay('2020-01-01')").Should().Be(58849);
            Scalar<int>("toModifiedJulianDay('2024-01-02')").Should().Be(60311);
            Scalar<int>("toModifiedJulianDay('0000-01-01')").Should().Be(-678941,
                "year 0 is inside the vendor text domain although System.DateTime cannot represent it");
            Scalar<int>("toModifiedJulianDay('9999-12-31')").Should().Be(2973483);
            Scalar<string>("toTypeName(toModifiedJulianDay('2024-01-02'))").Should().Be("Int32");
        }

        FluentActions.Invoking(() => Scalar<int>("toModifiedJulianDay('10000-01-01')"))
            .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE*", "a five-digit year is outside the domain");
        FluentActions.Invoking(() => Scalar<int>("toModifiedJulianDay('2024-02-30')"))
            .Should().Throw<Exception>().WithMessage("*out of range*", "an invalid calendar date is never defaulted");

        using (new AssertionScope())
        {
            // toTime is the legacy fixed-date value; the arm renders its stable vendor name and the
            // two agree on the pinned server (use_legacy_to_time defaults to 1 here).
            ShouldBeInstant(Scalar<DateTime>("toTimeWithFixedDate(toDateTime('2024-05-17 03:04:05'))"),
                Dt(1970, 1, 2, 3, 4, 5));
            Scalar<long>("toTimeWithFixedDate(toDateTime('2024-05-17 03:04:05')) = toTime(toDateTime('2024-05-17 03:04:05'))")
                .Should().Be(1L, "toTimeWithFixedDate is the legacy toTime under the pinned setting");
            Scalar<long>("getSetting('use_legacy_to_time')").Should().Be(1L,
                "the pinned server defaults the legacy setting on; 26.7 reassigns the name");
            ShouldBeInstant(Scalar<DateTime>("toTimeWithFixedDate(toDateTime64('2024-05-17 03:04:05.123456', 6))"),
                Dt(1970, 1, 2, 3, 4, 5), "sub-second digits are dropped");

            Scalar<string>("toString(timestamp('2024-01-02 03:04:05.123456789'))")
                .Should().Be("2024-01-02 03:04:05.123456", "timestamp produces DateTime64(6)");
            Scalar<string>("toTypeName(timestamp('2024-01-02'))").Should().Be("DateTime64(6)");
            Scalar<string>("toTypeName(toTime64(toDateTime('2024-05-17 03:04:05'), 6))")
                .Should().Be("Time64(6)", "toTime64 returns the Time64 type, which the typed surface refuses");
            ShouldBeInstant(Scalar<DateTime>("timestamp('2023-12-31', '12:00:00.11')"),
                Dt(2023, 12, 31, 12, 0, 0, 110000));
            ShouldBeInstant(Scalar<DateTime>("timestamp('2023-12-31', '25:00:00')"),
                Dt(2024, 1, 1, 1, 0, 0), "an hour past 23 rolls into the next day");
        }

        FluentActions.Invoking(() => Scalar<DateTime>("timestamp('bad')"))
            .Should().Throw<Exception>().WithMessage("*CANNOT_PARSE_DATETIME*");

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Julian = SqlFunctions.ClickHouse.to_modified_julian_day(x.Text),
                TimeOfDay = SqlFunctions.ClickHouse.to_time(x.Value),
                Parsed = SqlFunctions.ClickHouse.timestamp(x.Text)
            })
            .First();

        row.Julian.Should().Be(60311);
        ShouldBeInstant(row.TimeOfDay, Dt(1970, 1, 2, 3, 44, 59));
        ShouldBeInstant(row.Parsed, Dt(2024, 1, 2));

        // The two-argument form is evaluated for constant arguments on 25.8; with a table column the
        // server raises ILLEGAL_COLUMN. The typed surface surfaces that error instead of simulating
        // the addition, and the one-argument form above is the column-safe path.
        FluentActions.Invoking(() => ctx.From<IT286bDateTimeEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { V = SqlFunctions.ClickHouse.timestamp(x.Text, "12:00:00.11") })
                .First())
            .Should().Throw<Exception>().WithMessage("*ILLEGAL_COLUMN*",
                "the vendor's two-argument form is constant-only on the pinned server");
    }

    [Fact]
    public void AgeAndDateName_ShouldCountWholeUnitsAndNameComponents()
    {
        using (new AssertionScope())
        {
            Scalar<long>("age('year', toDate('2000-01-01'), toDate('2024-01-02'))").Should().Be(24);
            Scalar<long>("age('month', toDate('2000-01-01'), toDate('2024-01-02'))").Should().Be(288);
            Scalar<long>("age('day', toDate('2024-01-01'), toDate('2024-01-02'))").Should().Be(1);
            Scalar<long>("age('hour', toDateTime('2024-01-01 00:00:00'), toDateTime('2024-01-02 03:00:00'))").Should().Be(27);
            Scalar<long>("age('week', toDate('2024-01-01'), toDate('2024-01-15'))").Should().Be(2);
            Scalar<long>("age('quarter', toDate('2024-01-01'), toDate('2024-12-31'))").Should().Be(3);
            Scalar<long>("age('day', toDate('2024-01-02'), toDate('2024-01-01'))").Should().Be(-1,
                "the result is signed; a reversed range is never clamped");
            Scalar<string>("toTypeName(age('year', toDate('2000-01-01'), toDate('2024-01-02')))").Should().Be("Int64");

            Scalar<string>("dateName('year', toDate('2024-01-02'))").Should().Be("2024");
            Scalar<string>("dateName('month', toDate('2024-01-02'))").Should().Be("January");
            Scalar<string>("dateName('month', toDate('2024-12-02'))").Should().Be("December");
            Scalar<string>("dateName('day', toDate('2024-01-02'))").Should().Be("2");
            Scalar<string>("dateName('quarter', toDate('2024-05-02'))").Should().Be("2");
            Scalar<string>("dateName('week', toDate('2024-01-08'))").Should().Be("2");
            Scalar<string>("dateName('weekday', toDate('2024-01-02'))").Should().Be("Tuesday");
            Scalar<string>("dateName('dayofyear', toDate('2024-02-01'))").Should().Be("32");
            Scalar<string>("dateName('hour', toDateTime('2024-01-02 03:04:05'))").Should().Be("3");
            Scalar<string>("dateName('minute', toDateTime('2024-01-02 03:04:05'))").Should().Be("4");
            Scalar<string>("dateName('second', toDateTime('2024-01-02 03:04:05'))").Should().Be("5");
            Scalar<string>("toTypeName(dateName('year', toDate('2024-01-02')))").Should().Be("String");
        }

        FluentActions.Invoking(() => Scalar<long>("age('fortnight', toDate('2024-01-01'), toDate('2024-01-02'))"))
            .Should().Throw<Exception>().WithMessage("*does not support 'fortnight'*");
        FluentActions.Invoking(() => Scalar<string>("dateName('fortnight', toDate('2024-01-02'))"))
            .Should().Throw<Exception>().WithMessage("*Invalid date part*");

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Years = SqlFunctions.ClickHouse.age("year", x.Value, x.Value),
                Month = SqlFunctions.ClickHouse.date_name("month", x.Value)
            })
            .First();

        row.Years.Should().Be(0L);
        row.Month.Should().Be("January");

        // The unit must be a constant expression; a column is rejected by the server, and that
        // error is surfaced (the member does not try to freeze a per-row unit into SQL).
        FluentActions.Invoking(() => ctx.From<IT286bDateTimeEntity>()
                .Select(x => new { V = SqlFunctions.ClickHouse.date_name(x.Unit, x.Value) })
                .First())
            .Should().Throw<Exception>().WithMessage("*must be constant*");
    }

    [Fact]
    public void Timezones_ShouldReportTheServerZoneOffsetsAndDst()
    {
        using (new AssertionScope())
        {
            Scalar<string>("serverTimezone()").Should().Be("UTC");
            Scalar<string>("timezoneOf(now())").Should().Be("UTC");
            Scalar<string>("timezoneOf(toDateTime('2024-01-02 03:04:05', 'Europe/Moscow'))").Should().Be("Europe/Moscow");

            // DST is resolved at the instant, not by a fixed offset.
            Scalar<int>("timezoneOffset(toDateTime('2024-01-02 03:04:05', 'Europe/Berlin'))").Should().Be(3600);
            Scalar<int>("timezoneOffset(toDateTime('2024-07-02 03:04:05', 'Europe/Berlin'))").Should().Be(7200);
            Scalar<int>("timezoneOffset(toDateTime('2024-01-02 03:04:05', 'America/New_York'))").Should().Be(-18000);
            Scalar<int>("timezoneOffset(toDateTime('2024-07-02 03:04:05', 'America/New_York'))").Should().Be(-14400);
            Scalar<int>("timezoneOffset(toDateTime('2024-01-02 03:04:05', 'Asia/Kolkata'))").Should().Be(19800,
                "a non-hour offset is reported exactly");
            Scalar<int>("timezoneOffset(toDateTime('2024-01-02 03:04:05'))").Should().Be(0);
            Scalar<string>("toTypeName(timezoneOffset(now()))").Should().Be("Int32");
        }

        // fromUTCTimestamp returns a plain DateTime: the zone is not part of the type, while the
        // underlying instant is shifted by the zone offset. That (wall clock, zone) pair is what
        // System.DateTime cannot carry, and the typed surface refuses it.
        using (new AssertionScope())
        {
            Scalar<string>("toTypeName(fromUTCTimestamp(toDateTime('2024-01-02 03:04:05'), 'Europe/Moscow'))")
                .Should().Be("DateTime", "the result type carries no zone on the pinned server");
            Scalar<string>("timezoneOf(fromUTCTimestamp(toDateTime('2024-01-02 03:04:05'), 'Europe/Moscow'))")
                .Should().Be("UTC", "the zone label is not attached to the result");
            Scalar<string>("toString(fromUTCTimestamp(toDateTime('2024-01-02 03:04:05'), 'Europe/Moscow'))")
                .Should().Be("2024-01-02 06:04:05", "the wall clock is the target-zone time");
            Scalar<long>("toUnixTimestamp(fromUTCTimestamp(toDateTime('2024-01-02 03:04:05'), 'Europe/Moscow')) "
                         + "- toUnixTimestamp(toDateTime('2024-01-02 03:04:05'))")
                .Should().Be(10800L, "the underlying instant is shifted by the zone offset");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT286bDateTimeEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.from_utc_timestamp(x.Value, "Europe/Moscow"))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*plain DateTime without a zone*shifted by the zone offset*cannot carry the target zone*",
                "the refusal is precise and raised before execution");

        var row = e.Where(x => x.Id == 1)
            .Select(x => new
            {
                Zone = SqlFunctions.ClickHouse.server_timezone(),
                Offset = SqlFunctions.ClickHouse.timezone_offset(x.Value)
            })
            .First();

        row.Zone.Should().Be("UTC");
        row.Offset.Should().Be(0);
    }

    [Fact]
    public void FromUnixTimestampVariants_ShouldConvertEpochsAndRefuseNanoseconds()
    {
        using (new AssertionScope())
        {
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp(toUInt32(0))"), Dt(1970, 1, 1));
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp(toUInt32(1704164645))"), Dt(2024, 1, 2, 3, 4, 5));
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp(toUInt32(4294967295))"), Dt(2106, 2, 7, 6, 28, 15),
                "the UInt32 domain ends at 2106-02-07");
            Scalar<string>("toTypeName(fromUnixTimestamp(toUInt32(0)))").Should().Be("DateTime");

            // The formatting form is a distinct, representable signature: MySQL specifiers, String
            // result, and the zone is applied to the instant.
            Scalar<string>("fromUnixTimestamp(toUInt32(1704164645), '%Y-%m-%d %H:%i:%s')")
                .Should().Be("2024-01-02 03:04:05");
            Scalar<string>("fromUnixTimestamp(toUInt32(1704164645), '%Y-%m-%d %H:%i:%s', 'Europe/Moscow')")
                .Should().Be("2024-01-02 06:04:05");
            Scalar<string>("toTypeName(fromUnixTimestamp(toUInt32(1), '%Y'))").Should().Be("String");

            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp64Milli(toInt64(1704164645123))"),
                Dt(2024, 1, 2, 3, 4, 5, 123000));
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp64Milli(toInt64(-1))"),
                Dt(1969, 12, 31, 23, 59, 59, 999000), "a negative epoch is a pre-epoch instant, not clamped");
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp64Milli(toInt64(-2208988800000))"),
                Dt(1900, 1, 1), "the DateTime64(3) minimum is 1900-01-01");
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp64Milli(toInt64(7258118399999))"),
                Dt(2199, 12, 31, 23, 59, 59, 999000), "a far in-range boundary keeps its millisecond");

            // Outside the DateTime64 range the server clamps the toString form (2299-12-31 23:00);
            // that substitution is the server's and is pinned as such, not as a CLR value.
            Scalar<string>("toString(fromUnixTimestamp64Milli(toInt64(253402300800000)))")
                .Should().Be("2299-12-31 23:00:00.000", "the server clamps past the DateTime64(3) maximum");

            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp64Micro(toInt64(1704164645123456))"),
                Dt(2024, 1, 2, 3, 4, 5, 123456));
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp64Micro(toInt64(-1500000))"),
                Dt(1969, 12, 31, 23, 59, 58, 500000));
            ShouldBeInstant(Scalar<DateTime>("fromUnixTimestamp64Micro(toInt64(-2208988800000000))"),
                Dt(1900, 1, 1), "the DateTime64(6) minimum is 1900-01-01");
            Scalar<string>("toTypeName(fromUnixTimestamp64Milli(toInt64(0)))").Should().Be("DateTime64(3)");
            Scalar<string>("toTypeName(fromUnixTimestamp64Micro(toInt64(0)))").Should().Be("DateTime64(6)");
        }

        // The nanosecond variant is refused for its precision, and the server proves why: the value
        // exists as DateTime64(9), whose digits below 100 ns have no System.DateTime representation.
        using (new AssertionScope())
        {
            Scalar<string>("toTypeName(fromUnixTimestamp64Nano(toInt64(1704164645123456789)))")
                .Should().Be("DateTime64(9)");
            Scalar<string>("toString(fromUnixTimestamp64Nano(toInt64(1704164645123456789)))")
                .Should().Be("2024-01-02 03:04:05.123456789", "the digits beyond 100 ns are real and would be lost");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT286bDateTimeEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.from_unix_timestamp64_nano(1L))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DateTime64(9)*nanosecond digits*100 ns tick resolution*");

        var row = e.Where(x => x.Id == 1)
            .Select(x => new
            {
                Epoch = SqlFunctions.ClickHouse.from_unix_timestamp(1704164645u),
                Milli = SqlFunctions.ClickHouse.from_unix_timestamp64_milli(1704164645123L),
                Micro = SqlFunctions.ClickHouse.from_unix_timestamp64_micro(1704164645123456L),
                Formatted = SqlFunctions.ClickHouse.from_unix_timestamp(1704164645u, "%Y-%m-%d")
            })
            .First();

        ShouldBeInstant(row.Epoch, Dt(2024, 1, 2, 3, 4, 5));
        ShouldBeInstant(row.Milli, Dt(2024, 1, 2, 3, 4, 5, 123000));
        ShouldBeInstant(row.Micro, Dt(2024, 1, 2, 3, 4, 5, 123456));
        row.Formatted.Should().Be("2024-01-02");
    }

    [Fact]
    public void NullOperands_ShouldProduceNullNeverADefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Slot = SqlFunctions.ClickHouse.time_slot(x.NullableValue),
                Five = SqlFunctions.ClickHouse.to_start_of_five_minutes(x.NullableValue),
                Interval = SqlFunctions.ClickHouse.to_start_of_interval(x.NullableValue, 1, "day"),
                IsoYear = SqlFunctions.ClickHouse.to_iso_year(x.NullableValue),
                Week = SqlFunctions.ClickHouse.to_week(x.NullableValue, 3),
                YearWeek = SqlFunctions.ClickHouse.to_year_week(x.NullableValue),
                LastDay = SqlFunctions.ClickHouse.to_last_day_of_week(x.NullableValue),
                Days = SqlFunctions.ClickHouse.to_days_in_month(x.NullableValue),
                Age = SqlFunctions.ClickHouse.age("year", x.NullableValue, x.Value),
                Name = SqlFunctions.ClickHouse.date_name("month", x.NullableValue),
                ZoneOffset = SqlFunctions.ClickHouse.timezone_offset(x.NullableValue),
                TimeOfDay = SqlFunctions.ClickHouse.to_time(x.NullableValue),
                Julian = SqlFunctions.ClickHouse.to_modified_julian_day((string?)null),
                Parsed = SqlFunctions.ClickHouse.timestamp((string?)null),
                Epoch = SqlFunctions.ClickHouse.from_unix_timestamp((uint?)null),
                Milli = SqlFunctions.ClickHouse.from_unix_timestamp64_milli((long?)null)
            })
            .First();

        using (new AssertionScope())
        {
            row.Slot.Should().BeNull();
            row.Five.Should().BeNull();
            row.Interval.Should().BeNull();
            row.IsoYear.Should().BeNull();
            row.Week.Should().BeNull();
            row.YearWeek.Should().BeNull();
            row.LastDay.Should().BeNull();
            row.Days.Should().BeNull();
            row.Age.Should().BeNull();
            row.Name.Should().BeNull();
            row.ZoneOffset.Should().BeNull();
            row.TimeOfDay.Should().BeNull();
            row.Julian.Should().BeNull();
            row.Parsed.Should().BeNull();
            row.Epoch.Should().BeNull();
            row.Milli.Should().BeNull();
        }

        // The server reports Nullable results for a nullable argument; nothing collapses to a default.
        using (new AssertionScope())
        {
            Scalar<string>("toTypeName(toStartOfFiveMinutes(CAST(NULL AS Nullable(DateTime))))").Should().Be("Nullable(DateTime)");
            Scalar<string>("toTypeName(toISOYear(CAST(NULL AS Nullable(DateTime))))").Should().Be("Nullable(UInt16)");
            Scalar<string>("toTypeName(age('year', CAST(NULL AS Nullable(DateTime)), toDate('2024-01-01')))").Should().Be("Nullable(Int64)");
            Scalar<string>("toTypeName(makeDate(CAST(NULL AS Nullable(Int32)), 1, 1))").Should().Be("Nullable(Date)");
        }

        // A nullable start for timeSlots is not representable as an array: the server refuses the
        // shape instead of yielding an empty array, and the typed surface offers no nullable overload.
        FluentActions.Invoking(() => Scalar<DateTime[]>("timeSlots(CAST(NULL AS Nullable(DateTime)), toUInt32(1800))"))
            .Should().Throw<Exception>().WithMessage("*Nested type Array(DateTime) cannot be inside Nullable type*");
    }

    [Fact]
    public void Projection_ShouldMaterializeTheDeclaredClrShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bDateTimeEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Date = SqlFunctions.ClickHouse.make_date(2024, 2, 29),
                DateTimeValue = SqlFunctions.ClickHouse.make_date_time(2024, 1, 2, 3, 4, 5),
                Slot = SqlFunctions.ClickHouse.time_slot(x.Value),
                Slots = SqlFunctions.ClickHouse.time_slots(x.Value, 1800),
                Five = SqlFunctions.ClickHouse.to_start_of_five_minutes(x.Value),
                Interval = SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 15, "minute"),
                IsoYear = SqlFunctions.ClickHouse.to_iso_year(x.Value),
                Week = SqlFunctions.ClickHouse.to_week(x.Value, 3),
                YearWeek = SqlFunctions.ClickHouse.to_year_week(x.Value),
                LastDay = SqlFunctions.ClickHouse.to_last_day_of_week(x.Value),
                Days = SqlFunctions.ClickHouse.to_days_in_month(x.Value),
                Julian = SqlFunctions.ClickHouse.to_modified_julian_day(x.Text),
                Age = SqlFunctions.ClickHouse.age("day", x.Value, x.Value),
                Name = SqlFunctions.ClickHouse.date_name("weekday", x.Value),
                Zone = SqlFunctions.ClickHouse.server_timezone(),
                ZoneOf = SqlFunctions.ClickHouse.timezone_of(x.Value),
                ZoneOffset = SqlFunctions.ClickHouse.timezone_offset(x.Value),
                TimeOfDay = SqlFunctions.ClickHouse.to_time(x.Value),
                Parsed = SqlFunctions.ClickHouse.timestamp(x.Text),
                Epoch = SqlFunctions.ClickHouse.from_unix_timestamp(1704164645u)
            })
            .First();

        using (new AssertionScope())
        {
            ShouldBeInstant(row.Date, Dt(2024, 2, 29));
            ShouldBeInstant(row.DateTimeValue, Dt(2024, 1, 2, 3, 4, 5));
            ShouldBeInstant(row.Slot, Dt(2024, 1, 2, 3, 30));
            row.Slots.Should().HaveCount(2);
            ShouldBeInstant(row.Five, Dt(2024, 1, 2, 3, 40));
            ShouldBeInstant(row.Interval, Dt(2024, 1, 2, 3, 30));
            row.IsoYear.Should().Be(2024);
            row.Week.Should().Be(1);
            row.YearWeek.Should().Be(202353, "2024-01-02 is in the last week of 2023 under mode 0");
            ShouldBeInstant(row.LastDay, Dt(2024, 1, 6));
            row.Days.Should().Be(31);
            row.Julian.Should().Be(60311);
            row.Age.Should().Be(0L);
            row.Name.Should().Be("Tuesday");
            row.Zone.Should().Be("UTC");
            row.ZoneOf.Should().Be("UTC");
            row.ZoneOffset.Should().Be(0);
            ShouldBeInstant(row.TimeOfDay, Dt(1970, 1, 2, 3, 44, 59));
            ShouldBeInstant(row.Parsed, Dt(2024, 1, 2));
            ShouldBeInstant(row.Epoch, Dt(2024, 1, 2, 3, 4, 5));

            row.Date.GetType().Should().Be(typeof(DateTime));
            row.Slots.GetType().Should().Be(typeof(DateTime[]));
            row.IsoYear.GetType().Should().Be(typeof(int));
            row.Age.GetType().Should().Be(typeof(long));
            row.Zone.GetType().Should().Be(typeof(string));
        }
    }
}
