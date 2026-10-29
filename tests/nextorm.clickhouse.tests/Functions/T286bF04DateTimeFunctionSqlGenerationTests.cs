using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D4 (F04 Date/time): the ClickHouse-only date/time members. The tests pin the exact
/// <see cref="IScalarFunctions"/> arms (including the <c>toInt32</c> lowering of the
/// <c>UInt8</c>/<c>UInt16</c>/<c>UInt32</c> results, the <c>toUInt8</c> week modes and the
/// <c>toUInt32</c> duration), the declared CLR shapes, the inlined interval unit, the three tested
/// CLR-shape refusals and the fail-closed refusal of every other provider. They never open a
/// connection.
/// </summary>
public class T286bF04DateTimeFunctionSqlGenerationTests
{
    private static readonly IScalarFunctions ClickHouseScalars = ClickHouseDialect.Instance.ScalarFunctions!;

    /// <summary>One row per render arm of the delivered members.</summary>
    private static readonly (string Name, string[] Args, string Expected)[] Arms =
    [
        ("make_date", ["y", "m", "d"], "makeDate(y, m, d)"),
        ("make_date", ["y", "doy"], "makeDate(y, doy)"),
        ("make_date32", ["y", "m", "d"], "makeDate32(y, m, d)"),
        ("make_date32", ["y", "doy"], "makeDate32(y, doy)"),
        ("make_date_time", ["y", "m", "d", "h", "mi", "s"], "makeDateTime(y, m, d, h, mi, s)"),
        ("now64", [], "now64()"),
        ("now64", ["6"], "now64(6)"),
        ("now_in_block", [], "nowInBlock()"),
        ("time_slot", ["dt"], "timeSlot(dt)"),
        ("time_slots", ["dt", "dur"], "timeSlots(toDateTime(dt), toUInt32(dur))"),
        ("to_start_of_fifteen_minutes", ["dt"], "toStartOfFifteenMinutes(dt)"),
        ("to_start_of_five_minutes", ["dt"], "toStartOfFiveMinutes(dt)"),
        ("to_start_of_ten_minutes", ["dt"], "toStartOfTenMinutes(dt)"),
        ("to_start_of_interval", ["dt", "15", "day"], "toStartOfInterval(dt, INTERVAL 15 DAY)"),
        ("to_start_of_interval", ["dt", "1", "week", "origin"], "toStartOfInterval(toDateTime(dt), INTERVAL 1 WEEK, toDateTime(origin))"),
        ("to_iso_year", ["dt"], "toInt32(toISOYear(dt))"),
        ("to_week", ["dt"], "toInt32(toWeek(dt))"),
        ("to_week", ["dt", "3"], "toInt32(toWeek(dt, toUInt8(3)))"),
        ("to_year_week", ["dt"], "toInt32(toYearWeek(dt))"),
        ("to_year_week", ["dt", "3"], "toInt32(toYearWeek(dt, toUInt8(3)))"),
        ("to_last_day_of_week", ["dt"], "toLastDayOfWeek(dt)"),
        ("to_last_day_of_week", ["dt", "1"], "toLastDayOfWeek(dt, toUInt8(1))"),
        ("to_days_in_month", ["dt"], "toInt32(toDayOfMonth(toLastDayOfMonth(dt)))"),
        ("to_modified_julian_day", ["s"], "toModifiedJulianDay(s)"),
        ("age", ["unit", "start", "end"], "age(unit, start, end)"),
        ("date_name", ["unit", "dt"], "dateName(unit, dt)"),
        ("server_timezone", [], "serverTimezone()"),
        ("timezone_of", ["dt"], "timezoneOf(dt)"),
        ("timezone_offset", ["dt"], "timezoneOffset(dt)"),
        ("to_time", ["dt"], "toTimeWithFixedDate(dt)"),
        ("timestamp", ["s"], "timestamp(s)"),
        ("timestamp", ["s", "t"], "timestamp(s, t)"),
        ("from_unix_timestamp", ["x"], "fromUnixTimestamp(x)"),
        ("from_unix_timestamp", ["x", "fmt"], "fromUnixTimestamp(x, fmt)"),
        ("from_unix_timestamp", ["x", "fmt", "tz"], "fromUnixTimestamp(x, fmt, tz)"),
        ("from_unix_timestamp64_milli", ["x"], "fromUnixTimestamp64Milli(x)"),
        ("from_unix_timestamp64_micro", ["x"], "fromUnixTimestamp64Micro(x)")
    ];

    /// <summary>The delivered catalog names, one per implemented member.</summary>
    private static readonly string[] MemberNames =
    [
        "make_date", "make_date32", "make_date_time", "now64", "now_in_block", "time_slot",
        "time_slots", "to_start_of_fifteen_minutes", "to_start_of_five_minutes",
        "to_start_of_ten_minutes", "to_start_of_interval", "to_iso_year", "to_week", "to_year_week",
        "to_last_day_of_week", "to_days_in_month", "to_modified_julian_day", "age", "date_name",
        "server_timezone", "timezone_of", "timezone_offset", "to_time", "timestamp",
        "from_unix_timestamp", "from_unix_timestamp64_milli", "from_unix_timestamp64_micro"
    ];

    /// <summary>The three members refused for a demonstrated CLR-shape limitation.</summary>
    private static readonly string[] RefusedNames =
    [
        "from_utc_timestamp", "to_time64", "from_unix_timestamp64_nano"
    ];

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
        string DateText { get; set; }

        [Column("unit")]
        string Unit { get; set; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryMember()
    {
        using (new AssertionScope())
        {
            foreach (var (name, args, expected) in Arms)
                ClickHouseScalars.Render(name, args).Should().Be(
                    expected, "'{0}({1})' is the ClickHouse arm", name, string.Join(", ", args));
        }
    }

    [Fact]
    public void Supports_ShouldAcceptTheCompleteDateTimeSliceAndRejectNativeSpellings()
    {
        MemberNames.Should().HaveCount(27, "the F04 slice delivers 27 ClickHouse-only date/time names");

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
                ClickHouseScalars.Supports(name).Should().BeTrue("'{0}' is part of the ClickHouse inventory", name);

            foreach (var name in RefusedNames)
                ClickHouseScalars.Supports(name).Should().BeFalse("'{0}' is a tested refusal, not an arm", name);

            ClickHouseScalars.Supports("MAKE_DATE").Should().BeFalse("names are case-sensitive CLR member names");
            ClickHouseScalars.Supports("makeDate").Should().BeFalse("the table is keyed by the CLR member name");
            ClickHouseScalars.Supports("toWeek").Should().BeFalse("the native spelling is not the table key");
            ClickHouseScalars.Supports("fromUnixTimestamp").Should().BeFalse("the native spelling is not the table key");
            ClickHouseScalars.Supports("toTime64").Should().BeFalse("the refused member has no arm");
        }
    }

    [Fact]
    public void Render_ShouldThrowForTheNativeSpellingInsteadOfTheMemberName()
    {
        FluentActions.Invoking(() => ClickHouseScalars.Render("makeDate", ["y", "m", "d"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The makeDate function is not supported by ClickHouse.");

        FluentActions.Invoking(() => ClickHouseScalars.Render("fromUnixTimestamp", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The fromUnixTimestamp function is not supported by ClickHouse.");
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            Shape("make_date", typeof(int), typeof(int), typeof(int)).Should().Be(typeof(DateTime), "Date materializes as DateTime");
            Shape("make_date", typeof(int), typeof(int)).Should().Be(typeof(DateTime), "the day-of-year form is a Date");
            Shape("make_date", typeof(int?), typeof(int?), typeof(int?)).Should().Be(typeof(DateTime?), "NULL stays Nullable(Date)");
            Shape("make_date32", typeof(int), typeof(int), typeof(int)).Should().Be(typeof(DateTime), "Date32 materializes as DateTime");
            Shape("make_date_time", typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)).Should().Be(typeof(DateTime));
            Shape("now64").Should().Be(typeof(DateTime), "DateTime64(3) materializes as DateTime");
            Shape("now64", typeof(int)).Should().Be(typeof(DateTime));
            Shape("now_in_block").Should().Be(typeof(DateTime));
            Shape("time_slot", typeof(DateTime)).Should().Be(typeof(DateTime));
            Shape("time_slots", typeof(DateTime), typeof(uint)).Should().Be(typeof(DateTime[]), "Array(DateTime) materializes as DateTime[]");
            Shape("to_iso_year", typeof(DateTime)).Should().Be(typeof(int), "UInt16 is lowered to Int32");
            Shape("to_iso_year", typeof(DateTime?)).Should().Be(typeof(int?));
            Shape("to_week", typeof(DateTime)).Should().Be(typeof(int), "UInt8 is lowered to Int32");
            Shape("to_week", typeof(DateTime), typeof(int)).Should().Be(typeof(int));
            Shape("to_week", typeof(DateTime?), typeof(int?)).Should().Be(typeof(int?));
            Shape("to_year_week", typeof(DateTime)).Should().Be(typeof(int), "UInt32 is lowered to Int32");
            Shape("to_year_week", typeof(DateTime), typeof(int)).Should().Be(typeof(int));
            Shape("to_days_in_month", typeof(DateTime)).Should().Be(typeof(int));
            Shape("to_modified_julian_day", typeof(string)).Should().Be(typeof(int?), "the single text argument keeps Nullable(Int32)");
            Shape("age", typeof(string), typeof(DateTime), typeof(DateTime)).Should().Be(typeof(long), "age returns Int64 on the pinned server");
            Shape("timezone_of", typeof(DateTime)).Should().Be(typeof(string));
            Shape("timezone_offset", typeof(DateTime)).Should().Be(typeof(int));
            Shape("to_time", typeof(DateTime)).Should().Be(typeof(DateTime), "toTimeWithFixedDate returns DateTime");
            Shape("timestamp", typeof(string)).Should().Be(typeof(DateTime?), "DateTime64(6) materializes as DateTime");
            Shape("timestamp", typeof(string), typeof(string)).Should().Be(typeof(DateTime?));
            Shape("from_unix_timestamp", typeof(uint)).Should().Be(typeof(DateTime));
            Shape("from_unix_timestamp", typeof(uint), typeof(string)).Should().Be(typeof(string), "the formatting form returns String");
            Shape("from_unix_timestamp64_milli", typeof(long)).Should().Be(typeof(DateTime));
            Shape("from_unix_timestamp64_micro", typeof(long)).Should().Be(typeof(DateTime));
            Shape("from_utc_timestamp", typeof(DateTime), typeof(string)).Should().Be(typeof(DateTime), "the refused member keeps its documented shape");
            Shape("to_time64", typeof(DateTime), typeof(int)).Should().Be(typeof(TimeSpan), "the refused member names the closest CLR shape");
            Shape("from_unix_timestamp64_nano", typeof(long)).Should().Be(typeof(DateTime), "the refused member keeps its documented shape");
        }

        static Type Shape(string member, params Type[] parameters) => typeof(ClickHouseFunctions)
            .GetMethods()
            .Single(method => method.Name == member
                && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters))
            .ReturnType;
    }

    [Fact]
    public void Refusals_ShouldThrowThePreciseClrShapeReason()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.from_utc_timestamp(x.Value, "Europe/Moscow"))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*plain DateTime without a zone*shifted by the zone offset*cannot carry the target zone*");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.to_time64(x.Value, 6))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Time64*SupportsNativeDuration is false*");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.from_unix_timestamp64_nano(1L))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DateTime64(9)*nanosecond digits*100 ns tick resolution*");
    }

    [Fact]
    public void Now64Scale_ShouldRejectSubTickAndNonConstantScales()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.now64(9))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*refused for scale 9*8/9 carries sub-100 ns digits*");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.now64(8))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*refused for scale 8*");

        var scale = 6;
        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.now64(scale))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must be a compile-time constant*");

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.now64(7)))
            .Should().Contain("now64(7)", "scale 7 is the last representable precision");
    }

    [Fact]
    public void StartOfInterval_ShouldInlineTheUnitAndRejectANonLiteralOrSubSecondUnit()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();
        var unit = "day";
        var origin = new DateTime(2024, 1, 1);

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Local = SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 15, "minute"),
                Anchored = SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "WEEK", origin)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toStartOfInterval(dt, INTERVAL 15 MINUTE) as `Local`", "the unit is a validated SQL keyword");
            sql.Should().Contain("toStartOfInterval(toDateTime(dt), INTERVAL 1 WEEK, toDateTime(@origin)) as `Anchored`", "value and origin share the DateTime type");
        }

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "millisecond"))))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*compile-time string literal*ClickHouse rejects sub-second units*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "fortnight"))))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*second, minute, hour, day, week, month, quarter or year*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, unit))))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*must be a compile-time string literal*");
        }

        FluentActions.Invoking(() => ClickHouseScalars.Render("to_start_of_interval", ["dt", "1", "microsecond"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*ClickHouse rejects sub-second units*");
    }

    [Fact]
    public void WeekModes_ShouldRenderToUInt8AcrossEveryMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Week = SqlFunctions.ClickHouse.to_week(x.Value, 3),
                WeekFromColumn = SqlFunctions.ClickHouse.to_week(x.Value, x.Mode),
                YearWeek = SqlFunctions.ClickHouse.to_year_week(x.Value, 3),
                LastDay = SqlFunctions.ClickHouse.to_last_day_of_week(x.Value, 1)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toInt32(toWeek(dt, toUInt8(3))) as `Week`");
            sql.Should().Contain("toInt32(toWeek(dt, toUInt8(mode))) as `WeekFromColumn`");
            sql.Should().Contain("toInt32(toYearWeek(dt, toUInt8(3))) as `YearWeek`");
            sql.Should().Contain("toLastDayOfWeek(dt, toUInt8(1)) as `LastDay`");
        }
    }

    [Fact]
    public void TimeSlots_ShouldSendUInt32DurationAndKeepTheArrayShape()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Literal = SqlFunctions.ClickHouse.time_slots(x.Value, 1800),
                Column = SqlFunctions.ClickHouse.time_slots(x.Value, x.Duration)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("timeSlots(toDateTime(dt), toUInt32(1800)) as `Literal`", "the server requires UInt32 for the duration and a DateTime (not DateTime64) start");
            sql.Should().Contain("timeSlots(toDateTime(dt), toUInt32(dur)) as `Column`");
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryMemberOverDateTimeColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();
        var origin = new DateTime(2024, 1, 1);

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Date = SqlFunctions.ClickHouse.make_date(2024, 2, 29),
                DateDoy = SqlFunctions.ClickHouse.make_date(2024, 60),
                Date32 = SqlFunctions.ClickHouse.make_date32(2024, 2, 29),
                DateTime = SqlFunctions.ClickHouse.make_date_time(2024, 1, 2, 3, 4, 5),
                Now = SqlFunctions.ClickHouse.now64(6),
                InBlock = SqlFunctions.ClickHouse.now_in_block(),
                Slot = SqlFunctions.ClickHouse.time_slot(x.Value),
                Slots = SqlFunctions.ClickHouse.time_slots(x.Value, 1800),
                Fifteen = SqlFunctions.ClickHouse.to_start_of_fifteen_minutes(x.Value),
                Five = SqlFunctions.ClickHouse.to_start_of_five_minutes(x.Value),
                Ten = SqlFunctions.ClickHouse.to_start_of_ten_minutes(x.Value),
                Interval = SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "day"),
                Anchored = SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "week", origin),
                IsoYear = SqlFunctions.ClickHouse.to_iso_year(x.Value),
                Week = SqlFunctions.ClickHouse.to_week(x.Value),
                YearWeek = SqlFunctions.ClickHouse.to_year_week(x.Value, 3),
                LastDay = SqlFunctions.ClickHouse.to_last_day_of_week(x.Value),
                DaysInMonth = SqlFunctions.ClickHouse.to_days_in_month(x.Value),
                Julian = SqlFunctions.ClickHouse.to_modified_julian_day(x.DateText),
                Age = SqlFunctions.ClickHouse.age("year", x.Value, x.Value),
                Name = SqlFunctions.ClickHouse.date_name("month", x.Value),
                Zone = SqlFunctions.ClickHouse.server_timezone(),
                ZoneOf = SqlFunctions.ClickHouse.timezone_of(x.Value),
                ZoneOffset = SqlFunctions.ClickHouse.timezone_offset(x.Value),
                TimeOfDay = SqlFunctions.ClickHouse.to_time(x.Value),
                Parsed = SqlFunctions.ClickHouse.timestamp(x.DateText),
                ParsedWithTime = SqlFunctions.ClickHouse.timestamp(x.DateText, "12:00:00.11"),
                Epoch = SqlFunctions.ClickHouse.from_unix_timestamp(1704164645),
                EpochFormatted = SqlFunctions.ClickHouse.from_unix_timestamp(1704164645, "%Y-%m-%d"),
                EpochMilli = SqlFunctions.ClickHouse.from_unix_timestamp64_milli(1704164645123),
                EpochMicro = SqlFunctions.ClickHouse.from_unix_timestamp64_micro(1704164645123456)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("makeDate(2024, 2, 29) as `Date`");
            sql.Should().Contain("makeDate(2024, 60) as `DateDoy`");
            sql.Should().Contain("makeDate32(2024, 2, 29) as `Date32`");
            sql.Should().Contain("makeDateTime(2024, 1, 2, 3, 4, 5) as `DateTime`");
            sql.Should().Contain("now64(6) as `Now`");
            sql.Should().Contain("nowInBlock() as `InBlock`");
            sql.Should().Contain("timeSlot(dt) as `Slot`");
            sql.Should().Contain("timeSlots(toDateTime(dt), toUInt32(1800)) as `Slots`");
            sql.Should().Contain("toStartOfFifteenMinutes(dt) as `Fifteen`");
            sql.Should().Contain("toStartOfFiveMinutes(dt) as `Five`");
            sql.Should().Contain("toStartOfTenMinutes(dt) as `Ten`");
            sql.Should().Contain("toStartOfInterval(dt, INTERVAL 1 DAY) as `Interval`");
            sql.Should().Contain("toStartOfInterval(toDateTime(dt), INTERVAL 1 WEEK, toDateTime(@origin)) as `Anchored`");
            sql.Should().Contain("toInt32(toISOYear(dt)) as `IsoYear`");
            sql.Should().Contain("toInt32(toWeek(dt)) as `Week`");
            sql.Should().Contain("toInt32(toYearWeek(dt, toUInt8(3))) as `YearWeek`");
            sql.Should().Contain("toLastDayOfWeek(dt) as `LastDay`");
            sql.Should().Contain("toInt32(toDayOfMonth(toLastDayOfMonth(dt))) as `DaysInMonth`");
            sql.Should().Contain("toModifiedJulianDay(s) as `Julian`");
            sql.Should().Contain("age('year', dt, dt) as `Age`");
            sql.Should().Contain("dateName('month', dt) as `Name`");
            sql.Should().Contain("serverTimezone() as `Zone`");
            sql.Should().Contain("timezoneOf(dt) as `ZoneOf`");
            sql.Should().Contain("timezoneOffset(dt) as `ZoneOffset`");
            sql.Should().Contain("toTimeWithFixedDate(dt) as `TimeOfDay`");
            sql.Should().Contain("timestamp(s) as `Parsed`");
            sql.Should().Contain("timestamp(s, '12:00:00.11') as `ParsedWithTime`");
            sql.Should().Contain("fromUnixTimestamp(1704164645) as `Epoch`");
            sql.Should().Contain("fromUnixTimestamp(1704164645, '%Y-%m-%d') as `EpochFormatted`");
            sql.Should().Contain("fromUnixTimestamp64Milli(1704164645123) as `EpochMilli`");
            sql.Should().Contain("fromUnixTimestamp64Micro(1704164645123456) as `EpochMicro`");
        }
    }

    [Fact]
    public void NullValues_ShouldStayRealNullOperands()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Slot = SqlFunctions.ClickHouse.time_slot(x.NullableValue),
                Five = SqlFunctions.ClickHouse.to_start_of_five_minutes(x.NullableValue),
                Week = SqlFunctions.ClickHouse.to_week(x.NullableValue),
                Days = SqlFunctions.ClickHouse.to_days_in_month(x.NullableValue),
                Age = SqlFunctions.ClickHouse.age(null, x.NullableValue, x.Value),
                Name = SqlFunctions.ClickHouse.date_name("day", x.NullableValue),
                ZoneOffset = SqlFunctions.ClickHouse.timezone_offset(x.NullableValue),
                TimeOfDay = SqlFunctions.ClickHouse.to_time(x.NullableValue),
                Parsed = SqlFunctions.ClickHouse.timestamp((string?)null),
                Julian = SqlFunctions.ClickHouse.to_modified_julian_day((string?)null)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("timeSlot(ndt) as `Slot`");
            sql.Should().Contain("toStartOfFiveMinutes(ndt) as `Five`");
            sql.Should().Contain("toInt32(toWeek(ndt)) as `Week`");
            sql.Should().Contain("toInt32(toDayOfMonth(toLastDayOfMonth(ndt))) as `Days`");
            sql.Should().Contain("age(null, ndt, dt) as `Age`", "a required null unit is a real NULL operand");
            sql.Should().Contain("dateName('day', ndt) as `Name`");
            sql.Should().Contain("timezoneOffset(ndt) as `ZoneOffset`");
            sql.Should().Contain("toTimeWithFixedDate(ndt) as `TimeOfDay`");
            sql.Should().Contain("timestamp(null) as `Parsed`");
            sql.Should().Contain("toModifiedJulianDay(null) as `Julian`");
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDateTimeEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.to_week(x.Value, 3) == 1
                && SqlFunctions.ClickHouse.to_iso_year(x.Value) == 2024
                && SqlFunctions.ClickHouse.age("day", x.Value, x.Value) == 0
                && SqlFunctions.ClickHouse.timezone_offset(x.Value) == 0)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where");
            sql.Should().Contain("toInt32(toWeek(dt, toUInt8(3))) = 1");
            sql.Should().Contain("toInt32(toISOYear(dt)) = 2024");
            sql.Should().Contain("age('day', dt, dt) = 0");
            sql.Should().Contain("timezoneOffset(dt) = 0");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IT286bDateTimeEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new { W = SqlFunctions.ClickHouse.to_week(x.NullableValue, 3) }));

        sql.Should().Contain("toInt32(toWeek(`ndt`, toUInt8(3))) as `W`", "the argument stays a quoted column and the alias is preserved");
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldReportEveryNameUnsupportedAndRejectTheCalls()
    {
        IScalarFunctions[] others =
        [
            PostgresDialect.Instance.ScalarFunctions!,
            SqlServerDialect.Instance.ScalarFunctions!
        ];

        using (new AssertionScope())
        {
            foreach (var functions in others)
            {
                foreach (var name in MemberNames)
                    functions.Supports(name).Should().BeFalse("'{0}' is ClickHouse-only", name);
            }
        }

        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            var origin = new DateTime(2024, 1, 1);

            Func<string> query = name switch
            {
                "make_date" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.make_date(2024, 1, 2))),
                "make_date32" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.make_date32(2024, 1, 2))),
                "make_date_time" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.make_date_time(2024, 1, 2, 3, 4, 5))),
                "now64" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.now64(6))),
                "now_in_block" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.now_in_block())),
                "time_slot" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.time_slot(x.Value))),
                "time_slots" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.time_slots(x.Value, 1800))),
                "to_start_of_fifteen_minutes" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_start_of_fifteen_minutes(x.Value))),
                "to_start_of_five_minutes" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_start_of_five_minutes(x.Value))),
                "to_start_of_ten_minutes" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_start_of_ten_minutes(x.Value))),
                "to_start_of_interval" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_start_of_interval(x.Value, 1, "day", origin))),
                "to_iso_year" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_iso_year(x.Value))),
                "to_week" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_week(x.Value, 3))),
                "to_year_week" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_year_week(x.Value, 3))),
                "to_last_day_of_week" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_last_day_of_week(x.Value, 1))),
                "to_days_in_month" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_days_in_month(x.Value))),
                "to_modified_julian_day" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_modified_julian_day(x.DateText))),
                "age" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.age("day", x.Value, x.Value))),
                "date_name" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.date_name("day", x.Value))),
                "server_timezone" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.server_timezone())),
                "timezone_of" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.timezone_of(x.Value))),
                "timezone_offset" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.timezone_offset(x.Value))),
                "to_time" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.to_time(x.Value))),
                "timestamp" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.timestamp(x.DateText))),
                "from_unix_timestamp" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.from_unix_timestamp(1))),
                "from_unix_timestamp64_milli" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.from_unix_timestamp64_milli(1L))),
                "from_unix_timestamp64_micro" => () => SqlOf(ctx, ctx.From<IT286bDateTimeEntity>().Select(x => SqlFunctions.ClickHouse.from_unix_timestamp64_micro(1L))),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the F04 slice")
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage($"The {name} function is not supported by this provider.");
        }
    }
}
