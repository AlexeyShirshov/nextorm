using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the MySQL specific scalar/date/format surface against a live server: the native
/// function library, the portable functions rendered through the MySQL forms, the date-unit
/// switches and the session/format renderers. The shared suite covers only the portable core.
/// </summary>
[Collection("MySql")]
public sealed class MySqlFunctionExecutionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public void PortableScalarFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lp = SqlFunctions.Sql.lpad("7", 3, "0"),
                Rp = SqlFunctions.Sql.rpad("7", 3, "0"),
                Rep = SqlFunctions.Sql.repeat("ab", 2),
                Rev = SqlFunctions.Sql.reverse("abc"),
                Sp = SqlFunctions.Sql.space(2),
                Cw = SqlFunctions.Sql.concat_ws("-", "a", "b", "c"),
                Asc = SqlFunctions.Sql.ascii("A"),
                Ch = SqlFunctions.Sql.@char(65),
                Bits = SqlFunctions.Sql.bit_length("abc"),
                Octets = SqlFunctions.Sql.octet_length("abc"),
                Deg = SqlFunctions.Sql.degrees(Math.PI),
                Rad = SqlFunctions.Sql.radians(180),
                Pi = SqlFunctions.Sql.pi(),
                Cot = SqlFunctions.Sql.cot(1)
            })
            .First();

        r.L.Should().Be("abc");
        r.R.Should().Be("def");
        r.Lp.Should().Be("007");
        r.Rp.Should().Be("700");
        r.Rep.Should().Be("abab");
        r.Rev.Should().Be("cba");
        r.Sp.Should().Be("  ");
        r.Cw.Should().Be("a-b-c");
        r.Asc.Should().Be(65);
        r.Ch.Should().Be("A");
        r.Bits.Should().Be(24);
        r.Octets.Should().Be(3);
        r.Deg.Should().BeApproximately(180, 1e-9);
        r.Rad.Should().BeApproximately(Math.PI, 1e-9);
        r.Pi.Should().BeApproximately(Math.PI, 1e-5);
        r.Cot.Should().BeApproximately(1 / Math.Tan(1), 1e-9);
    }

    [Fact]
    public void NativeFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                FindInSet = SqlFunctions.MySql.find_in_set("b", "a,b,c"),
                Field = SqlFunctions.MySql.field("b", "a", "b", "c"),
                Elt = SqlFunctions.MySql.elt(2, "a", "b", "c"),
                SubstringIndex = SqlFunctions.MySql.substring_index("a,b,c", ",", 2),
                Format = SqlFunctions.MySql.format(1234.567m, 2),
                StrToDate = SqlFunctions.MySql.str_to_date("2023-05-17", "%Y-%m-%d"),
                DateFormat = SqlFunctions.MySql.date_format(Value, "%Y/%m/%d"),
                FromUnix = SqlFunctions.MySql.from_unixtime(0),
                Unix = SqlFunctions.MySql.unix_timestamp(Value),
                Md5 = SqlFunctions.MySql.md5("abc"),
                Sha1 = SqlFunctions.MySql.sha1("abc"),
                Sha2 = SqlFunctions.MySql.sha2("abc", 256),
                InetAton = SqlFunctions.MySql.inet_aton("127.0.0.1"),
                InetNtoa = SqlFunctions.MySql.inet_ntoa(2130706433),
            })
            .First();

        r.FindInSet.Should().Be(2);
        r.Field.Should().Be(2);
        r.Elt.Should().Be("b");
        r.SubstringIndex.Should().Be("a,b");
        r.Format.Should().Be("1,234.57");
        r.StrToDate.Should().Be(new DateTime(2023, 5, 17));
        r.DateFormat.Should().Be("2023/05/17");
        r.FromUnix.Should().Be(new DateTime(1970, 1, 1));
        r.Unix.Should().Be(new DateTimeOffset(Value, TimeSpan.Zero).ToUnixTimeSeconds());
        r.Md5.Should().Be("900150983cd24fb0d6963f7d28e17f72");
        r.Sha1.Should().Be("a9993e364706816aba3e25717850c26c9cd0d89d");
        r.Sha2.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        r.InetAton.Should().Be(2130706433);
        r.InetNtoa.Should().Be("127.0.0.1");
    }

    [Fact]
    public void JsonFunctions_ShouldExecute()
    {
        const string Doc = "{\"a\":1,\"b\":[1,2]}";

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Set = SqlFunctions.MySql.json_set(Doc, "$.a", "2"),
                Insert = SqlFunctions.MySql.json_insert(Doc, "$.c", "3"),
                Replace = SqlFunctions.MySql.json_replace(Doc, "$.a", "9"),
                Remove = SqlFunctions.MySql.json_remove(Doc, "$.a"),
                Append = SqlFunctions.MySql.json_array_append(Doc, "$.b", "3"),
                Depth = SqlFunctions.MySql.json_depth(Doc),
                Keys = SqlFunctions.MySql.json_keys(Doc),
                Length = SqlFunctions.MySql.json_length(Doc),
                Type = SqlFunctions.MySql.json_type(Doc)
            })
            .First();

        r.Set.Should().Contain("\"a\": \"2\"");
        r.Insert.Should().Contain("\"c\": \"3\"");
        r.Replace.Should().Contain("\"a\": \"9\"");
        r.Remove.Should().NotContain("\"a\"");
        r.Append.Should().Contain("3");
        r.Depth.Should().Be(3);
        r.Keys.Should().Contain("a");
        r.Length.Should().Be(2);
        r.Type.Should().Be("OBJECT");
    }

    [Fact]
    public void DateFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.extract("year", Value),
                Quarter = SqlFunctions.Sql.extract("quarter", Value),
                Month = SqlFunctions.Sql.extract("month", Value),
                Day = SqlFunctions.Sql.extract("day", Value),
                Dow = SqlFunctions.Sql.extract("dow", Value),
                IsoDow = SqlFunctions.Sql.extract("isodow", Value),
                Doy = SqlFunctions.Sql.extract("doy", Value),
                Week = SqlFunctions.Sql.extract("week", Value),
                Hour = SqlFunctions.Sql.extract("hour", Value),
                Minute = SqlFunctions.Sql.extract("minute", Value),
                Second = SqlFunctions.Sql.extract("second", Value),
                AddYear = SqlFunctions.Sql.date_add("year", 1, Value),
                AddWeek = SqlFunctions.Sql.date_add("week", 1, Value),
                AddMilli = SqlFunctions.Sql.date_add("milliseconds", 500, Value),
                AddMicro = SqlFunctions.Sql.date_add("microseconds", 500, Value),
                DiffDays = SqlFunctions.Sql.date_diff("day", Value, Value.AddDays(10)),
                DiffBig = SqlFunctions.Sql.date_diff_big("second", Value, Value.AddSeconds(5))
            })
            .First();

        r.Year.Should().Be(2023);
        r.Quarter.Should().Be(2);
        r.Month.Should().Be(5);
        r.Day.Should().Be(17);
        r.Dow.Should().Be(3);
        r.IsoDow.Should().Be(3);
        r.Doy.Should().Be(137);
        r.Week.Should().Be(20);
        r.Hour.Should().Be(13);
        r.Minute.Should().Be(45);
        r.Second.Should().Be(30);
        r.AddYear.Should().Be(Value.AddYears(1));
        r.AddWeek.Should().Be(Value.AddDays(7));
        r.AddMilli.Should().Be(Value.AddMilliseconds(500));
        r.AddMicro.Should().Be(Value.AddTicks(5_000));
        r.DiffDays.Should().Be(10);
        r.DiffBig.Should().Be(5);
    }

    [Fact]
    public void SessionInfoFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                User = SqlFunctions.Sql.current_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                Database = SqlFunctions.Sql.current_database(),
                Version = SqlFunctions.Sql.version()
            })
            .First();

        r.User.Should().NotBeNullOrEmpty();
        r.Schema.Should().NotBeNullOrEmpty();
        r.Database.Should().NotBeNullOrEmpty();
        r.Version.Should().StartWith("8");
    }

    [Fact]
    public void FormatStrings_ShouldTranslate()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Numeric = 1234.5.ToString("N2"),
                Date = Value.ToString("yyyy-MM-dd"),
                Clock = Value.ToString("HH:mm:ss"),
                ShortYear = Value.ToString("yy"),
                Separators = Value.ToString("yyyy/MM/dd HH:mm")
            })
            .First();

        r.Numeric.Should().Be("1,234.50");
        r.Date.Should().Be("2023-05-17");
        r.Clock.Should().Be("13:45:30");
        r.ShortYear.Should().Be("23");
        r.Separators.Should().Be("2023/05/17 13:45");
    }

    [Fact]
    public void DateUnitMatrix_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                AddMicro = SqlFunctions.Sql.date_add("microseconds", 500, Value),
                AddSecond = SqlFunctions.Sql.date_add("second", 30, Value),
                AddMinute = SqlFunctions.Sql.date_add("minute", 1, Value),
                AddHour = SqlFunctions.Sql.date_add("hour", 1, Value),
                AddDay = SqlFunctions.Sql.date_add("day", 1, Value),
                AddWeek = SqlFunctions.Sql.date_add("week", 1, Value),
                AddMonth = SqlFunctions.Sql.date_add("month", 1, Value),
                AddQuarter = SqlFunctions.Sql.date_add("quarter", 1, Value),
                AddYear = SqlFunctions.Sql.date_add("year", 1, Value),
                DiffSecond = SqlFunctions.Sql.date_diff("second", Value, Value.AddSeconds(30)),
                DiffMinute = SqlFunctions.Sql.date_diff("minute", Value, Value.AddMinutes(5)),
                DiffHour = SqlFunctions.Sql.date_diff("hour", Value, Value.AddHours(2)),
                DiffDay = SqlFunctions.Sql.date_diff("day", Value, Value.AddDays(3)),
                DiffWeek = SqlFunctions.Sql.date_diff("week", Value, Value.AddDays(14)),
                DiffMonth = SqlFunctions.Sql.date_diff("month", Value, Value.AddMonths(2)),
                DiffQuarter = SqlFunctions.Sql.date_diff("quarter", Value, Value.AddMonths(6)),
                DiffYear = SqlFunctions.Sql.date_diff("year", Value, Value.AddYears(1))
            })
            .First();

        r.AddMicro.Should().Be(Value.AddTicks(5_000));
        r.AddSecond.Should().Be(Value.AddSeconds(30));
        r.AddMinute.Should().Be(Value.AddMinutes(1));
        r.AddHour.Should().Be(Value.AddHours(1));
        r.AddDay.Should().Be(Value.AddDays(1));
        r.AddWeek.Should().Be(Value.AddDays(7));
        r.AddMonth.Should().Be(Value.AddMonths(1));
        r.AddQuarter.Should().Be(Value.AddMonths(3));
        r.AddYear.Should().Be(Value.AddYears(1));
        r.DiffSecond.Should().Be(30);
        r.DiffMinute.Should().Be(5);
        r.DiffHour.Should().Be(2);
        r.DiffDay.Should().Be(3);
        r.DiffWeek.Should().Be(2);
        r.DiffMonth.Should().Be(2);
        r.DiffQuarter.Should().Be(2);
        r.DiffYear.Should().Be(1);
    }

    [Fact]
    public void NumberFormatsAndSessionFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Padded = 12.ToString("D4"),
                Hex = 255.ToString("X"),
                SessionUser = SqlFunctions.Sql.session_user(),
                CurrentUser = SqlFunctions.Sql.current_user(),
                Uuid = SqlFunctions.MySql.uuid_to_bin("550e8400-e29b-41d4-a716-446655440000"),
                UuidBack = SqlFunctions.MySql.bin_to_uuid(
                    SqlFunctions.MySql.uuid_to_bin("550e8400-e29b-41d4-a716-446655440000")),
                MergePatch = SqlFunctions.MySql.json_merge_patch("{\"a\":1}", "{\"b\":2}"),
                MergePreserve = SqlFunctions.MySql.json_merge_preserve("{\"a\":1}", "{\"b\":2}")
            })
            .First();

        r.Padded.Should().Be("0012");
        r.Hex.Should().Be("FF");
        r.SessionUser.Should().NotBeNullOrEmpty();
        r.CurrentUser.Should().NotBeNullOrEmpty();
        r.Uuid.Should().HaveCount(16);
        r.UuidBack.Should().Be("550e8400-e29b-41d4-a716-446655440000");
        r.MergePatch.Should().Contain("\"b\"");
        r.MergePreserve.Should().Contain("\"a\"");
    }
}
