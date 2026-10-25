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
                ColName = SqlFunctions.SqlServer.col_name(SqlFunctions.SqlServer.object_id("simple_entity"), 1),
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

    [Fact]
    public void ConditionalFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Chosen = SqlFunctions.SqlServer.choose(2, "a", "b", "c"),
                OutOfRange = SqlFunctions.SqlServer.choose(9, "a", "b"),
                Iif = SqlFunctions.Sql.iif(x.Id > 1, "big", "small"),
                Greatest = SqlFunctions.Sql.greatest(1L, 5L, 3L),
                Least = SqlFunctions.Sql.least(1L, 5L, 3L)
            })
            .First();

        r.Chosen.Should().Be("b");
        r.OutOfRange.Should().BeNull();
        r.Iif.Should().Be("small");
        r.Greatest.Should().Be(5L);
        r.Least.Should().Be(1L);
    }

    [Fact]
    public void AggregateFamilies_ShouldCompute()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                Count = SqlFunctions.Sql.count(),
                CountArgument = SqlFunctions.Sql.count(x.Id),
                CountDistinct = SqlFunctions.Sql.count_distinct(x.Id),
                CountBig = SqlFunctions.Sql.count_big(),
                CountBigDistinct = SqlFunctions.Sql.count_big_distinct(x.Id),
                Min = SqlFunctions.Sql.min(x.Id),
                Max = SqlFunctions.Sql.max(x.Id),
                Avg = SqlFunctions.Sql.avg((double)x.Id),
                Sum = SqlFunctions.Sql.sum((long)x.Id),
                Stdev = SqlFunctions.Sql.stdev((double)x.Id),
                Stdevp = SqlFunctions.Sql.stdevp((double)x.Id),
                Var = SqlFunctions.Sql.var((double)x.Id),
                Varp = SqlFunctions.Sql.varp((double)x.Id),
                AvgDistinct = SqlFunctions.Sql.avg_distinct((double)x.Id),
                SumDistinct = SqlFunctions.Sql.sum_distinct((long)x.Id),
                StdevDistinct = SqlFunctions.Sql.stdev_distinct((double)x.Id),
                StdevpDistinct = SqlFunctions.Sql.stdevp_distinct((double)x.Id),
                VarDistinct = SqlFunctions.Sql.var_distinct((double)x.Id),
                VarpDistinct = SqlFunctions.Sql.varp_distinct((double)x.Id)
            })
            .First();

        r.Count.Should().Be(10);
        r.CountArgument.Should().Be(10);
        r.CountDistinct.Should().Be(10);
        r.CountBig.Should().Be(10L);
        r.CountBigDistinct.Should().Be(10L);
        r.Min.Should().Be(1);
        r.Max.Should().Be(10);
        r.Avg.Should().BeApproximately(5.5, 1e-12);
        r.Sum.Should().Be(55L);
        r.Stdev.Should().BeApproximately(3.0276503540974917, 1e-12);
        r.Stdevp.Should().BeApproximately(2.8722813232690143, 1e-12);
        r.Var.Should().BeApproximately(9.166666666666666, 1e-12);
        r.Varp.Should().BeApproximately(8.25, 1e-12);
        r.AvgDistinct.Should().BeApproximately(5.5, 1e-12);
        r.SumDistinct.Should().Be(55L);
        r.StdevDistinct.Should().BeApproximately(3.0276503540974917, 1e-12);
        r.StdevpDistinct.Should().BeApproximately(2.8722813232690143, 1e-12);
        r.VarDistinct.Should().BeApproximately(9.166666666666666, 1e-12);
        r.VarpDistinct.Should().BeApproximately(8.25, 1e-12);
    }

    [Fact]
    public void FilteredAggregates_ShouldBeRejected()
    {
        FluentActions.Invoking(() => _sut.SimpleEntity
                .Select(x => SqlFunctions.Sql.count(() => x.Id <= 2))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The FILTER clause is not supported by this provider.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Select(x => SqlFunctions.Sql.min(x.Id, () => x.Id <= 2))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The FILTER clause is not supported by this provider.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Select(x => SqlFunctions.Sql.avg((double)x.Id, () => x.Id <= 2))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The FILTER clause is not supported by this provider.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Select(x => SqlFunctions.Sql.string_agg(x.Id.ToString(), ",", () => x.Id <= 2))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The FILTER clause is not supported by this provider.");
    }

    [Fact]
    public void AdvancedAggregateCapabilityGuards_ShouldBeRejected()
    {
        void Reject(Action act, string message)
            => FluentActions.Invoking(act).Should().Throw<NotSupportedException>().WithMessage(message);

        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.Postgres.bool_and(x.Id > 0)).First(),
            "The bool_and/bool_or/every aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.Postgres.bit_and((int)x.Id)).First(),
            "The bit_and/bit_or/bit_xor aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.Sql.corr((double)x.Id, (double)x.Id)).First(),
            "The statistical (corr/covar) aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.Postgres.regr_slope((double)x.Id, (double)x.Id)).First(),
            "The regr_* aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.ClickHouse.arg_min(x.Id, x.Id)).First(),
            "The argMin/argMax aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.ClickHouse.uniq(x.Id)).First(),
            "The uniq/uniqExact/uniqCombined/uniqHLL12 aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.ClickHouse.quantile(0.5, x.Id)).First(),
            "The quantile/median aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.ClickHouse.median(x.Id)).First(),
            "The quantile/median aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.ClickHouse.top_k(2, x.Id)).First(),
            "The topK/topKWeighted aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.ClickHouse.group_array(x.Id)).First(),
            "The groupArray/groupUniqArray aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.ClickHouse.any_last(x.Id)).First(),
            "The anyLast aggregates are not supported by this provider.");
        Reject(() => _sut.ComplexEntity.Select(x => SqlFunctions.ClickHouse.window_funnel(3600, x.Id, x.Id > 0)).First(),
            "The windowFunnel/retention/sequenceMatch aggregates are not supported by this provider.");
        Reject(() => _sut.SimpleEntity.Select(x => SqlFunctions.Postgres.percentile_cont(0.5, () => (double)x.Id)).First(),
            "The ordered-set aggregates (percentile_cont/percentile_disc/mode) are not supported by this provider.");
    }

    /// <summary>
    /// #258: QUOTENAME(value, quote) over the real server. The expected matrix is written out
    /// independently of the production escaping helper: every delimiter is a one-character string whose
    /// apostrophe (when it is the delimiter) is doubled, and the server decides the native result —
    /// including the null results for a null argument and for a value longer than sysname.
    /// </summary>
    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_LiteralParameterAndNullVariants_ShouldExecute()
    {
        var value128 = new string('a', 128);
        var value129 = new string('a', 129);
        var paramValue = "O'Brien";
        var paramQuote = "'";
        string? nullParam = null;

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Apostrophe = SqlFunctions.SqlServer.quotename("abc", "'"),
                ApostropheInValue = SqlFunctions.SqlServer.quotename("O'Brien", "'"),
                DoubleQuote = SqlFunctions.SqlServer.quotename("abc", "\""),
                Bracket = SqlFunctions.SqlServer.quotename("abc", "["),
                RightBracket = SqlFunctions.SqlServer.quotename("abc", "]"),
                Parenthesis = SqlFunctions.SqlServer.quotename("abc", "("),
                RightParenthesis = SqlFunctions.SqlServer.quotename("abc", ")"),
                Angle = SqlFunctions.SqlServer.quotename("abc", "<"),
                RightAngle = SqlFunctions.SqlServer.quotename("abc", ">"),
                Brace = SqlFunctions.SqlServer.quotename("abc", "{"),
                RightBrace = SqlFunctions.SqlServer.quotename("abc", "}"),
                Backtick = SqlFunctions.SqlServer.quotename("abc", "`"),
                EscapedBracketValue = SqlFunctions.SqlServer.quotename("abc[]def"),
                OneArgument = SqlFunctions.SqlServer.quotename("abc"),
                EmptyValue = SqlFunctions.SqlServer.quotename("", "["),
                NullValue = SqlFunctions.SqlServer.quotename(null, "'"),
                NullQuote = SqlFunctions.SqlServer.quotename("abc", null),
                At128 = SqlFunctions.SqlServer.quotename(value128, "["),
                At129 = SqlFunctions.SqlServer.quotename(value129, "["),
                Param = SqlFunctions.SqlServer.quotename(paramValue, paramQuote),
                NullParam = SqlFunctions.SqlServer.quotename(nullParam, "'")
            })
            .First();

        r.Apostrophe.Should().Be("'abc'");
        r.ApostropheInValue.Should().Be("'O''Brien'");
        r.DoubleQuote.Should().Be("\"abc\"");
        r.Bracket.Should().Be("[abc]");
        r.RightBracket.Should().Be("[abc]");
        r.Parenthesis.Should().Be("(abc)");
        r.RightParenthesis.Should().Be("(abc)");
        r.Angle.Should().Be("<abc>");
        r.RightAngle.Should().Be("<abc>");
        r.Brace.Should().Be("{abc}");
        r.RightBrace.Should().Be("{abc}");
        r.Backtick.Should().Be("`abc`");
        r.EscapedBracketValue.Should().Be("[abc[]]def]");
        r.OneArgument.Should().Be("[abc]");
        r.EmptyValue.Should().Be("[]");
        r.NullValue.Should().BeNull();
        r.NullQuote.Should().BeNull();
        r.At128.Should().Be("[" + value128 + "]");
        r.At129.Should().BeNull("an input longer than sysname returns NULL natively, with no client truncation");
        r.Param.Should().Be("'O''Brien'");
        r.NullParam.Should().BeNull();
    }

    // ------------------------------------------------------------------ #259: SQL Server time materialisation
    //
    // SqlClient returns a native TimeSpan for a `time`/`timefromparts` field while a mapped duration
    // column stays bigint and reaches the same column mapper, so the facts below cover both sides of
    // that split against a live server: the native precision matrix, the NULL propagation, the
    // physical `time` columns, sync/async parity, the raw ADO.NET scalar, and the unchanged signed
    // bigint duration path.

    /// <summary>01:02:03.004 — the issue's <c>timefromparts(1, 2, 3, 4, 3)</c> value, in ticks.</summary>
    private const long FractionalTicks = 37_230_040_000L;

    /// <summary>01:02:03.0000004 — the <c>timefromparts(…, 4, 7)</c> value, in 100 ns ticks.</summary>
    private const long TimefrompartsTickTicks = 37_230_000_004L;

    /// <summary>01:02:03.0040004 — the <c>time(7)</c> column value, which keeps the 100 ns digit.</summary>
    private const long SeededTime7Ticks = 37_230_040_004L;

    [Fact]
    [Trait("Issue", "259")]
    public void Timefromparts_materialises_TimeSpan_precision_matrix()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Milli = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 3),
                Tick = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 7),
                Seconds = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 0, 0)
            })
            .First();

        r.Milli.Should().Be(TimeSpan.FromTicks(FractionalTicks));
        r.Milli!.Value.Ticks.Should().Be(FractionalTicks);
        r.Tick.Should().Be(TimeSpan.FromTicks(TimefrompartsTickTicks));
        r.Tick!.Value.Ticks.Should().Be(TimefrompartsTickTicks);
        r.Seconds.Should().Be(new TimeSpan(1, 2, 3));
    }

    [Fact]
    [Trait("Issue", "259")]
    public void Timefromparts_null_arguments_materialise_null()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                NullHour = SqlFunctions.SqlServer.timefromparts(null, 2, 3, 4, 3),
                NullMinute = SqlFunctions.SqlServer.timefromparts(1, null, 3, 4, 3),
                NullSeconds = SqlFunctions.SqlServer.timefromparts(1, 2, null, 4, 3),
                NullFractions = SqlFunctions.SqlServer.timefromparts(1, 2, 3, null, 3),
                Control = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 3)
            })
            .First();

        r.NullHour.Should().BeNull();
        r.NullMinute.Should().BeNull();
        r.NullSeconds.Should().BeNull();
        r.NullFractions.Should().BeNull();
        r.Control.Should().Be(TimeSpan.FromTicks(FractionalTicks));
    }

    [Fact]
    [Trait("Issue", "259")]
    public void Time_columns_materialise_nullable_and_required_TimeSpan()
    {
        var ctx = _sut.DataProvider;
        var table = CreateTimeTable(ctx);

        try
        {
            var rows = ctx.From(table)
                .Select(e => new
                {
                    Id = SqlFunctions.Column<int>(e, "id"),
                    T = SqlFunctions.Column<TimeSpan>(e, "t"),
                    T3 = SqlFunctions.Column<TimeSpan>(e, "t3"),
                    T7 = SqlFunctions.Column<TimeSpan>(e, "t7"),
                    Tn = SqlFunctions.Column<TimeSpan?>(e, "tn"),
                    T3n = SqlFunctions.Column<TimeSpan?>(e, "t3n"),
                    T7n = SqlFunctions.Column<TimeSpan?>(e, "t7n")
                })
                .ToList()
                .OrderBy(x => x.Id)
                .ToList();

            rows.Should().HaveCount(2);

            var seeded = rows[0];
            seeded.T.Ticks.Should().Be(FractionalTicks);
            seeded.T3.Ticks.Should().Be(FractionalTicks, "time(3) truncates .0040004 to .004");
            seeded.T7.Ticks.Should().Be(SeededTime7Ticks, "time(7) keeps the 100 ns tick");
            seeded.Tn.Should().BeNull();
            seeded.T3n.Should().BeNull();
            seeded.T7n.Should().BeNull();

            var midnight = rows[1];
            midnight.T.Should().Be(TimeSpan.Zero);
            midnight.T3.Should().Be(TimeSpan.Zero);
            midnight.T7.Should().Be(TimeSpan.Zero);
            midnight.Tn.Should().BeNull();
            midnight.T3n.Should().BeNull();
            midnight.T7n.Should().BeNull();
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    [Fact]
    [Trait("Issue", "259")]
    public async Task Timefromparts_sync_and_async_materialisation_agree()
    {
        var sync = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Milli = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 3),
                Tick = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 7)
            })
            .ToList();

        var async = await _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Milli = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 3),
                Tick = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 7)
            })
            .ToListAsync();

        sync.Should().ContainSingle();
        async.Should().ContainSingle();
        sync[0].Milli.Should().Be(TimeSpan.FromTicks(FractionalTicks));
        async[0].Milli.Should().Be(sync[0].Milli);
        sync[0].Tick.Should().Be(TimeSpan.FromTicks(TimefrompartsTickTicks));
        async[0].Tick.Should().Be(sync[0].Tick);
    }

    /// <summary>
    /// Labelled raw: the ADO.NET scalar path reads the driver's native <see cref="TimeSpan"/> directly.
    /// This does not exercise the row-mapper branch the other cases cover.
    /// </summary>
    [Fact]
    [Trait("Issue", "259")]
    public void Raw_time_scalar_remains_supported()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw("select cast('01:02:03.004' as time(3)) as t");

        result.Read<TimeSpan>().Should().Equal(TimeSpan.FromTicks(FractionalTicks));
    }

    /// <summary>
    /// The integer-backed duration path is unchanged: a <c>bigint</c> read as a <see cref="TimeSpan"/>
    /// keeps its sign and its beyond-24 h range because it is never reinterpreted as a time-of-day.
    /// </summary>
    [Fact]
    [Trait("Issue", "259")]
    public void Bigint_duration_projection_retains_signed_unbounded_values()
    {
        var ctx = _sut.DataProvider;
        var table = CreateTimeTable(ctx);

        try
        {
            var rows = ctx.From(table)
                .Select(e => new
                {
                    Id = SqlFunctions.Column<int>(e, "id"),
                    Dur = SqlFunctions.Column<TimeSpan>(e, "dur")
                })
                .ToList()
                .OrderBy(x => x.Id)
                .ToList();

            rows.Should().HaveCount(2);
            rows[0].Dur.Ticks.Should().Be(-37_230_040_000L, "a negative duration keeps its sign");
            rows[0].Dur.Should().Be(TimeSpan.FromTicks(-37_230_040_000L));
            rows[1].Dur.Ticks.Should().Be(900_610_000_000L, "25:01:01 is a valid TimeSpan beyond 24 h");
            rows[1].Dur.Should().Be(TimeSpan.FromTicks(900_610_000_000L));
            rows[1].Dur.Should().BeGreaterThan(TimeSpan.FromDays(1));
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    /// <summary>
    /// Creates the T259 test-owned table and seeds the two rows the time and duration cases read: the
    /// fractional/precision matrix with NULL nullable columns, and a 25:01:01 bigint duration.
    /// </summary>
    private static string CreateTimeTable(IDataContext ctx)
    {
        var table = "t259_time_" + Guid.NewGuid().ToString("N")[..8];

        Execute(ctx, $"create table {table} (" +
            "id int not null, " +
            "t time not null, " +
            "t3 time(3) not null, " +
            "t7 time(7) not null, " +
            "tn time null, " +
            "t3n time(3) null, " +
            "t7n time(7) null, " +
            "dur bigint not null)");

        Execute(ctx, $"insert into {table} (id, t, t3, t7, tn, t3n, t7n, dur) values " +
            "(1, '01:02:03.004', '01:02:03.004', '01:02:03.0040004', null, null, null, -37230040000), " +
            "(2, '00:00:00', '00:00:00', '00:00:00.0000000', null, null, null, 900610000000)");

        return table;
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
