using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQLite specific surface, second sweep: the date-unit switches (add/diff parts), the extract
/// parts, the number/date format renderers, the JSON1 library and the SQLite-only scalar helpers.
/// Every call runs against a real file-backed database.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteExtendedFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public void DateAddUnits_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.date_add("year", 1, Value),
                Quarter = SqlFunctions.Sql.date_add("quarter", 1, Value),
                Month = SqlFunctions.Sql.date_add("month", 1, Value),
                Week = SqlFunctions.Sql.date_add("week", 1, Value),
                Day = SqlFunctions.Sql.date_add("day", 1, Value),
                Hour = SqlFunctions.Sql.date_add("hour", 1, Value),
                Minute = SqlFunctions.Sql.date_add("minute", 1, Value),
                Second = SqlFunctions.Sql.date_add("second", 30, Value),
                Milli = SqlFunctions.Sql.date_add("milliseconds", 500, Value)
            })
            .First();

        r.Year.Should().Be(Value.AddYears(1));
        r.Quarter.Should().Be(Value.AddMonths(3));
        r.Month.Should().Be(Value.AddMonths(1));
        r.Week.Should().Be(Value.AddDays(7));
        r.Day.Should().Be(Value.AddDays(1));
        r.Hour.Should().Be(Value.AddHours(1));
        r.Minute.Should().Be(Value.AddMinutes(1));
        r.Second.Should().Be(Value.AddSeconds(30));
        r.Milli.Should().Be(Value.AddMilliseconds(500));
    }

    [Fact]
    public void DateDiffUnits_ShouldExecute()
    {
        var later = Value.AddDays(40);

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.date_diff("year", Value, later),
                Quarter = SqlFunctions.Sql.date_diff("quarter", Value, later),
                Month = SqlFunctions.Sql.date_diff("month", Value, later),
                Week = SqlFunctions.Sql.date_diff("week", Value, later),
                Day = SqlFunctions.Sql.date_diff("day", Value, later),
                Hour = SqlFunctions.Sql.date_diff("hour", Value, later),
                Minute = SqlFunctions.Sql.date_diff("minute", Value, later),
                Second = SqlFunctions.Sql.date_diff("second", Value, later),
                Big = SqlFunctions.Sql.date_diff_big("second", Value, later)
            })
            .First();

        r.Year.Should().Be(0);
        r.Quarter.Should().Be(0);
        r.Month.Should().Be(1);
        r.Week.Should().Be(5);
        r.Day.Should().Be(40);
        r.Hour.Should().Be(960);
        r.Minute.Should().Be(57_600);
        r.Second.Should().Be(3_456_000);
        r.Big.Should().Be(3_456_000);
    }

    [Fact]
    public void DateParts_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Quarter = SqlFunctions.Sql.extract("quarter", Value),
                Dow = SqlFunctions.Sql.extract("dow", Value),
                IsoDow = SqlFunctions.Sql.extract("isodow", Value),
                Doy = SqlFunctions.Sql.extract("doy", Value),
                Week = SqlFunctions.Sql.extract("week", Value),
                Epoch = SqlFunctions.Sql.date_part("epoch", Value),
                Epoch2 = SqlFunctions.Sql.date_part("epoch", Value)
            })
            .First();

        r.Quarter.Should().Be(2);
        r.Dow.Should().Be(3);
        r.IsoDow.Should().Be(3);
        r.Doy.Should().Be(137);
        r.Week.Should().Be(20);
        r.Epoch.Should().BeApproximately(1684331130, 0.001);
        r.Epoch2.Should().BeApproximately(1684331130, 0.001);
    }

    [Fact]
    public void NumberAndDateFormats_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fixed = 1234.5.ToString("F2"),
                Padded = 12.ToString("D4"),
                Hex = 255.ToString("X"),
                Date = Value.ToString("yyyy-MM-dd"),
                Clock = Value.ToString("HH:mm:ss"),
                Year = Value.ToString("yyyy")
            })
            .First();

        r.Fixed.Should().Be("1234.50");
        r.Padded.Should().Be("0012");
        r.Hex.Should().Be("ff");
        r.Date.Should().Be("2023-05-17");
        r.Year.Should().Be("2023");
        r.Clock.Should().Be("13:45:30");
    }

    [Fact]
    public void JsonFunctions_ShouldExecute()
    {
        const string Doc = "{\"a\":1,\"b\":[1,2]}";

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Extract = SqlFunctions.Sqlite.json_extract<int>(Doc, "$.a"),
                Get = SqlFunctions.Sqlite.json_get(Doc, "$.a"),
                GetText = SqlFunctions.Sqlite.json_get_text(Doc, "$.a"),
                Array = SqlFunctions.Sqlite.json_array(1, 2, 3),
                Insert = SqlFunctions.Sqlite.json_insert(Doc, "$.c", 3),
                Replace = SqlFunctions.Sqlite.json_replace(Doc, "$.a", 9),
                Set = SqlFunctions.Sqlite.json_set(Doc, "$.a", 2),
                Object = SqlFunctions.Sqlite.json_object("a", 1),
                Patch = SqlFunctions.Sqlite.json_patch(Doc, "{\"c\":3}"),
                Pretty = SqlFunctions.Sqlite.json_pretty(Doc),
                Quote = SqlFunctions.Sqlite.json_quote("abc"),
                Remove = SqlFunctions.Sqlite.json_remove(Doc, "$.a"),
                Type = SqlFunctions.Sqlite.json_type(Doc),
                TypePath = SqlFunctions.Sqlite.json_type(Doc, "$.b"),
                Valid = SqlFunctions.Sqlite.json_valid(Doc),
                ValidFlags = SqlFunctions.Sqlite.json_valid(Doc, 1),
                ArrayInsert = SqlFunctions.Sqlite.json_array_insert("[1,2]", "$[0]", 0)
            })
            .First();

        r.Extract.Should().Be(1);
        r.Get.Should().NotBeNull();
        r.GetText.Should().Be("1");
        r.Array.Should().Be("[1,2,3]");
        r.Insert.Should().Contain("\"c\":3");
        r.Replace.Should().Contain("\"a\":9");
        r.Set.Should().Contain("\"a\":2");
        r.Object.Should().Contain("\"a\":1");
        r.Patch.Should().Contain("\"c\":3");
        r.Pretty.Should().Contain("\n");
        r.Quote.Should().Be("\"abc\"");
        r.Remove.Should().NotContain("\"a\"");
        r.Type.Should().Be("object");
        r.TypePath.Should().Be("array");
        r.Valid.Should().BeTrue();
        r.ValidFlags.Should().BeTrue();
        r.ArrayInsert.Should().Contain("0");
    }

    [Fact]
    public void SqliteScalarHelpers_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Printf = SqlFunctions.Sqlite.printf("%s-%d", "a", 1),
                Format = SqlFunctions.Sqlite.format("%s-%d", "a", 1),
                Hex = SqlFunctions.Sqlite.hex("abc"),
                Unhex = SqlFunctions.Sqlite.unhex("616263"),
                UnhexIgnored = SqlFunctions.Sqlite.unhex("616263", "x"),
                Random = SqlFunctions.Sqlite.random(),
                Quote = SqlFunctions.Sqlite.quote("abc"),
                TypeOf = SqlFunctions.Sqlite.@typeof(1),
                Glob = SqlFunctions.Sqlite.glob("a*", "abc"),
                Unicode = SqlFunctions.Sqlite.unicode("A"),
                Char = SqlFunctions.Sqlite.@char(65, 66),
                Octets = SqlFunctions.Sqlite.octet_length("abc"),
                IfNull = SqlFunctions.Sqlite.ifnull<string>(null, "fallback"),
                If = SqlFunctions.Sqlite.@if(true, "yes", "no"),
                Diff = SqlFunctions.Sqlite.timediff(Value.AddHours(1), Value),
                Unix = SqlFunctions.Sqlite.unixepoch(Value),
                Julian = SqlFunctions.Sqlite.julianday(Value),
                Mod = SqlFunctions.Sqlite.mod(7, 4),
                Log2 = SqlFunctions.Sqlite.log2(8),
                Log10 = SqlFunctions.Sqlite.log10(1000),
                Acosh = SqlFunctions.Sqlite.acosh(1),
                Asinh = SqlFunctions.Sqlite.asinh(0),
                Atanh = SqlFunctions.Sqlite.atanh(0),
                Cosh = SqlFunctions.Sqlite.cosh(0),
                Sinh = SqlFunctions.Sqlite.sinh(0),
                Tanh = SqlFunctions.Sqlite.tanh(0),
                Atan2 = SqlFunctions.Sqlite.atan2(1, 1)
            })
            .First();

        r.Printf.Should().Be("a-1");
        r.Format.Should().Be("a-1");
        r.Hex.Should().Be("616263");
        r.Unhex.Should().Equal(97, 98, 99);
        r.UnhexIgnored.Should().Equal(97, 98, 99);
        r.Random.Should().NotBe(0);
        r.Quote.Should().Be("'abc'");
        r.TypeOf.Should().Be("integer");
        r.Glob.Should().BeTrue();
        r.Unicode.Should().Be(65);
        r.Char.Should().Be("AB");
        r.Octets.Should().Be(3);
        r.IfNull.Should().Be("fallback");
        r.If.Should().Be("yes");
        r.Diff.Should().StartWith("+0000-00-00 01:00:00");
        r.Unix.Should().Be(1684331130);
        r.Julian.Should().BeApproximately(2460082.0733, 0.001);
        r.Mod.Should().Be(3);
        r.Log2.Should().Be(3);
        r.Log10.Should().Be(3);
        r.Acosh.Should().Be(0);
        r.Asinh.Should().Be(0);
        r.Atanh.Should().Be(0);
        r.Cosh.Should().Be(1);
        r.Sinh.Should().Be(0);
        r.Tanh.Should().Be(0);
        r.Atan2.Should().BeApproximately(Math.Atan2(1, 1), 1e-9);
    }
}
