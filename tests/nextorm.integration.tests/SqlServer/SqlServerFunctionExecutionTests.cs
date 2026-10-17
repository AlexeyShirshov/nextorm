using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the SQL Server specific T-SQL surface against a live server: the scalar library that
/// has no portable equivalent, the metadata/session functions, the date and math renderers, the
/// .NET format-string translation and FOR JSON. The shared suite only touches this surface at its
/// portable edges; the SQL-shape unit tests never open a connection.
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerFunctionExecutionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public void ScalarFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                PatIndex = SqlFunctions.SqlServer.patindex("%b%", "abc"),
                Quoted = SqlFunctions.SqlServer.quotename("abc"),
                Soundex = SqlFunctions.SqlServer.soundex("Robert"),
                Difference = SqlFunctions.SqlServer.difference("Robert", "Rupert"),
                Escaped = SqlFunctions.SqlServer.string_escape("a\nb", "json"),
                Unicode = SqlFunctions.SqlServer.unicode("A"),
                NChar = SqlFunctions.SqlServer.nchar(65),
                Acos = SqlFunctions.SqlServer.acos(0.5),
                Asin = SqlFunctions.SqlServer.asin(0.5),
                Atan = SqlFunctions.SqlServer.atan(1),
                Atn2 = SqlFunctions.SqlServer.atn2(1, 1),
                Square = SqlFunctions.SqlServer.square(3),
                CheckSum = SqlFunctions.SqlServer.checksum(1, "a"),
                BinaryCheckSum = SqlFunctions.SqlServer.binary_checksum(1, "a"),
                Rand = SqlFunctions.SqlServer.rand(7),
                Stuff = SqlFunctions.SqlServer.stuff("abcdef", 2, 3, "XY"),
                Str = SqlFunctions.SqlServer.str(123.456, 8, 2),
                IsDate = SqlFunctions.SqlServer.isdate("2023-01-01"),
                NotDate = SqlFunctions.SqlServer.isdate("not a date"),
                IsNumeric = SqlFunctions.SqlServer.isnumeric("12.5"),
                NotNumeric = SqlFunctions.SqlServer.isnumeric("abc"),
                ParseName = SqlFunctions.SqlServer.parsename("a.b.c", 1),
                Date = SqlFunctions.SqlServer.datetimefromparts(2020, 2, 29, 10, 11, 12, 500),
                SmallDate = SqlFunctions.SqlServer.smalldatetimefromparts(2020, 2, 29, 10, 11),
                Date2 = SqlFunctions.SqlServer.datetime2fromparts(2020, 2, 29, 10, 11, 12, 5, 2),
                Json = SqlFunctions.SqlServer.json_array(1, "a"),
                JsonObject = SqlFunctions.SqlServer.json_object("k", "v"),
                HasJsonPath = SqlFunctions.SqlServer.json_path_exists("{\"name\":\"alice\"}", "$.name"),
            })
            .First();

        r.PatIndex.Should().Be(2);
        r.Quoted.Should().Be("[abc]");
        r.Soundex.Should().Be("R163");
        r.Difference.Should().Be(4);
        r.Escaped.Should().Be("a\\nb");
        r.Unicode.Should().Be(65);
        r.NChar.Should().Be("A");
        r.Acos.Should().BeApproximately(Math.Acos(0.5), 1e-9);
        r.Asin.Should().BeApproximately(Math.Asin(0.5), 1e-9);
        r.Atan.Should().BeApproximately(Math.Atan(1), 1e-9);
        r.Atn2.Should().BeApproximately(Math.Atan2(1, 1), 1e-9);
        r.Square.Should().Be(9);
        r.CheckSum.Should().NotBe(0);
        r.BinaryCheckSum.Should().NotBe(0);
        r.Rand.Should().BeApproximately(0.7136, 1e-3);
        r.Stuff.Should().Be("aXYef");
        r.Str.Should().Be("  123.46");
        r.IsDate.Should().Be(1);
        r.NotDate.Should().Be(0);
        r.IsNumeric.Should().Be(1);
        r.NotNumeric.Should().Be(0);
        r.ParseName.Should().Be("c");
        r.Date.Should().Be(new DateTime(2020, 2, 29, 10, 11, 12, 500));
        r.SmallDate.Should().Be(new DateTime(2020, 2, 29, 10, 11, 0));
        r.Date2.Should().Be(new DateTime(2020, 2, 29, 10, 11, 12, 50));
        r.Json.Should().Be("[1,\"a\"]");
        r.JsonObject.Should().Be("{\"k\":\"v\"}");
        r.HasJsonPath.Should().BeTrue();
    }

    [Fact]
    public void MetadataFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                DbId = SqlFunctions.SqlServer.db_id(),
                DbIdMaster = SqlFunctions.SqlServer.db_id("master"),
                DbName = SqlFunctions.SqlServer.db_name(),
                SchemaId = SqlFunctions.SqlServer.schema_id(),
                SchemaIdDbo = SqlFunctions.SqlServer.schema_id("dbo"),
                SchemaName = SqlFunctions.SqlServer.schema_name(),
                TypeId = SqlFunctions.SqlServer.type_id("int"),
                TypeName = SqlFunctions.SqlServer.type_name(56),
                FileGroupId = SqlFunctions.SqlServer.filegroup_id("PRIMARY"),
                FileGroupName = SqlFunctions.SqlServer.filegroup_name(1),
                FileId = SqlFunctions.SqlServer.file_id("tempdev"),
                FileName = SqlFunctions.SqlServer.file_name(1),
                ObjectId = SqlFunctions.SqlServer.object_id("simple_entity"),
                ObjectIdType = SqlFunctions.SqlServer.object_id("simple_entity", "U"),
                ObjectName = SqlFunctions.SqlServer.object_name(SqlFunctions.SqlServer.object_id("simple_entity")),
                ColLength = SqlFunctions.SqlServer.col_length("simple_entity", "id"),
                ColName = SqlFunctions.SqlServer.col_name(1, 1),
                AnsiNull = SqlFunctions.SqlServer.getansinull(),
            })
            .First();

        r.DbId.Should().BeGreaterThan(0);
        r.DbIdMaster.Should().Be(1);
        r.DbName.Should().NotBeNullOrEmpty();
        r.SchemaId.Should().Be(1);
        r.SchemaIdDbo.Should().Be(1);
        r.SchemaName.Should().Be("dbo");
        r.TypeId.Should().Be(56);
        r.TypeName.Should().Be("int");
        r.FileGroupId.Should().Be(1);
        r.FileGroupName.Should().NotBeNullOrEmpty();
        r.ObjectId.Should().BeGreaterThan(0);
        r.ObjectIdType.Should().Be(r.ObjectId);
        r.ObjectName.Should().Be("simple_entity");
        r.ColLength.Should().Be(4);
        r.ColName.Should().Be("id");
        r.AnsiNull.Should().Be(1);
    }

    [Fact]
    public void SessionInfoFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                CurrentUser = SqlFunctions.Sql.current_user(),
                SessionUser = SqlFunctions.Sql.session_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                Database = SqlFunctions.Sql.current_database(),
                Version = SqlFunctions.Sql.version(),
                ServerName = SqlFunctions.SqlServer.server_name(),
                HostName = SqlFunctions.SqlServer.host_name(),
                AppName = SqlFunctions.SqlServer.app_name(),
                SuserName = SqlFunctions.SqlServer.suser_name(),
                OriginalLogin = SqlFunctions.SqlServer.original_login(),
                SysDateTime = SqlFunctions.SqlServer.sysdatetime(),
                SysUtc = SqlFunctions.SqlServer.sysutcdatetime(),
                SysDto = SqlFunctions.SqlServer.sysdatetimeoffset()
            })
            .First();

        r.CurrentUser.Should().NotBeNullOrEmpty();
        r.SessionUser.Should().NotBeNullOrEmpty();
        r.Schema.Should().Be("dbo");
        r.Database.Should().NotBeNullOrEmpty();
        r.Version.Should().NotBeNullOrEmpty();
        r.ServerName.Should().NotBeNullOrEmpty();
        r.HostName.Should().NotBeNullOrEmpty();
        r.AppName.Should().NotBeNullOrEmpty();
        r.SuserName.Should().NotBeNullOrEmpty();
        r.OriginalLogin.Should().NotBeNullOrEmpty();
        r.SysDateTime.Should().BeAfter(new DateTime(2020, 1, 1));
        r.SysUtc.Should().BeAfter(new DateTime(2020, 1, 1));
        r.SysDto.Should().BeAfter(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
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
                TruncMonth = SqlFunctions.Sql.date_trunc("month", Value),
                TruncDay = SqlFunctions.Sql.date_trunc("day", Value),
                AddYear = SqlFunctions.Sql.date_add("year", 1, Value),
                AddQuarter = SqlFunctions.Sql.date_add("quarter", 1, Value),
                AddMilli = SqlFunctions.Sql.date_add("milliseconds", 500, Value),
                AddMicro = SqlFunctions.Sql.date_add("microseconds", 500, Value),
                DiffDays = SqlFunctions.Sql.date_diff("day", Value, Value.AddDays(10)),
                DiffBig = SqlFunctions.Sql.date_diff_big("second", Value, Value.AddSeconds(5)),
                Name = SqlFunctions.SqlServer.datename("year", Value),
                Bucket = SqlFunctions.SqlServer.date_bucket("day", 1, Value)
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
        r.TruncMonth.Should().Be(new DateTime(2023, 5, 1));
        r.TruncDay.Should().Be(new DateTime(2023, 5, 17));
        r.AddYear.Should().Be(Value.AddYears(1));
        r.AddQuarter.Should().Be(Value.AddMonths(3));
        r.AddMilli.Should().Be(Value.AddMilliseconds(500));
        r.AddMicro.Should().Be(Value.AddTicks(5_000));
        r.DiffDays.Should().Be(10);
        r.DiffBig.Should().Be(5);
        r.Name.Should().Be("2023");
        r.Bucket.Should().Be(new DateTime(2023, 5, 17));
    }

    [Fact]
    public void MathFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Abs = Math.Abs(-3.5),
                Ceil = Math.Ceiling(2.2),
                Floor = Math.Floor(2.8),
                Round = Math.Round(2.345, 2),
                Pow = Math.Pow(2, 10),
                Sqrt = Math.Sqrt(16),
                Exp = Math.Exp(0),
                Ln = Math.Log(Math.E),
                Log10 = Math.Log10(1000),
                Sign = Math.Sign(-42),
                Sin = Math.Sin(0),
                Cos = Math.Cos(0),
                Tan = Math.Tan(0),
                Cot = SqlFunctions.Sql.cot(1),
                Degrees = SqlFunctions.Sql.degrees(Math.PI),
                Radians = SqlFunctions.Sql.radians(180),
                Pi = SqlFunctions.Sql.pi()
            })
            .First();

        r.Abs.Should().Be(3.5);
        r.Ceil.Should().Be(3);
        r.Floor.Should().Be(2);
        r.Round.Should().Be(2.35);
        r.Pow.Should().Be(1024);
        r.Sqrt.Should().Be(4);
        r.Exp.Should().Be(1);
        r.Ln.Should().Be(1);
        r.Log10.Should().Be(3);
        r.Sign.Should().Be(-1);
        r.Sin.Should().Be(0);
        r.Cos.Should().Be(1);
        r.Tan.Should().Be(0);
        r.Cot.Should().BeApproximately(1 / Math.Tan(1), 1e-9);
        r.Degrees.Should().BeApproximately(180, 1e-9);
        r.Radians.Should().BeApproximately(Math.PI, 1e-9);
        r.Pi.Should().BeApproximately(Math.PI, 1e-9);
    }

    [Fact]
    public void FormatStrings_ShouldTranslate()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Numeric = 1234.5.ToString("N2"),
                Fixed = 1234.5.ToString("F1"),
                Date = Value.ToString("yyyy-MM-dd"),
                Clock = Value.ToString("HH:mm:ss"),
                ShortYear = Value.ToString("yy"),
                Separators = Value.ToString("yyyy/MM/dd HH:mm")
            })
            .First();

        r.Numeric.Should().Be("1,234.50");
        r.Fixed.Should().Be("1234.5");
        r.Date.Should().Be("2023-05-17");
        r.Clock.Should().Be("13:45:30");
        r.ShortYear.Should().Be("23");
        r.Separators.Should().Be("2023/05/17 13:45");
    }

    [Fact]
    public void ForJson_ShouldExecute()
    {
        var path = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new { x.Id, x.RequiredString })
            .ForJson(ForJsonMode.Path);

        path.Should().NotBeNullOrEmpty();
        path.Should().Contain("\"id\":2");

        var auto = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new { x.Id, x.RequiredString })
            .ForJson(ForJsonMode.Auto);

        auto.Should().NotBeNullOrEmpty();
        auto.Should().Contain("\"id\":2");

        var withRoot = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new { x.Id, x.RequiredString })
            .WithForJson(ForJsonMode.Path, root: "row")
            .ForJson(ForJsonMode.Path, root: "row");

        withRoot.Should().Contain("\"row\"");
    }

    [Fact]
    public void IsJsonPredicate_ShouldExecute()
    {
        var predicate = _sut.ComplexEntity
            .Where(x => SqlFunctions.SqlServer.isjson("{\"a\":1}"))
            .Count();

        predicate.Should().Be(3);

        var asValue = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.SqlServer.isjson("{\"a\":1}"))
            .First();

        asValue.Should().BeTrue();
    }

    [Fact]
    public void PagingWithTies_ShouldExecute()
    {
        var rows = _sut.ComplexEntity
            .Where(x => x.Int != null)
            .OrderBy(x => x.Int)
            .Page(1, 0)
            .WithTies()
            .Select(x => x.Id)
            .ToList();

        // Both rows share Int == 1, so WITH TIES returns the whole tied group.
        rows.Should().HaveCount(2);
    }

    [Fact]
    public void OffsetOnlyPaging_ShouldExecute()
    {
        var all = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id).ToList();
        var shifted = _sut.ComplexEntity.OrderBy(x => x.Id).Offset(1).Select(x => x.Id).ToList();

        shifted.Should().Equal(all.Skip(1));
    }
}
