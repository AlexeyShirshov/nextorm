using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using Npgsql;

namespace NextORM.Integration.Tests;

/// <summary>
/// Exercises the built-in scalar translation surface (string, Math, DateTime) against PostgreSQL,
/// complementing the JSON/array coverage in <see cref="PostgresSpecificTests"/>.
/// </summary>
public sealed class PostgresFunctionsTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void StringCaseAndTrim_ShouldTransform()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Lower = x.String!.ToUpper().ToLower(),
                Trim = x.String!.Trim(),
                TrimStart = x.String!.TrimStart(),
                TrimEnd = x.String!.TrimEnd()
            })
            .First();

        r.Lower.Should().Be("dadfasd");
        r.Trim.Should().Be("dadfasd");
        r.TrimStart.Should().Be("dadfasd");
        r.TrimEnd.Should().Be("dadfasd");
    }

    [Fact]
    public void StringReplaceAndIndexOf_ShouldTransform()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Replaced = x.String!.Replace("a", "o"),
                Last = x.String!.LastIndexOf("a"),
                Ends = x.String!.EndsWith("sd"),
                Starts = x.String!.StartsWith("dad"),
                Sub = x.String!.Substring(1),
                Empty = string.IsNullOrEmpty(x.String)
            })
            .First();

        r.Replaced.Should().Be("dodfosd");
        r.Last.Should().Be(4);
        r.Ends.Should().BeTrue();
        r.Starts.Should().BeTrue();
        r.Sub.Should().Be("adfasd");
        r.Empty.Should().BeFalse();
    }

    [Fact]
    public void IsNullOrEmpty_ShouldDetectNull()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 3)
            .Select(x => string.IsNullOrEmpty(x.String))
            .First();

        r.Should().BeTrue();
    }

    [Fact]
    public void MathRounding_ShouldRound()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Ceil = Math.Ceiling((double)x.Id + 0.2),
                Floor = Math.Floor((double)x.Id + 0.8),
                Trunc = Math.Truncate((double)x.Id + 0.9),
                Sign = (double)Math.Sign(x.Id - 2)
            })
            .First();

        r.Ceil.Should().Be(2.0);
        r.Floor.Should().Be(1.0);
        r.Trunc.Should().Be(1.0);
        r.Sign.Should().Be(-1.0);
    }

    [Fact]
    public void MathNumeric_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 3)
            .Select(x => new
            {
                Sqrt = Math.Sqrt(x.Id),
                Pow = Math.Pow(x.Id, 2),
                Log = Math.Log(x.Id),
                Exp = Math.Exp(x.Id),
                Abs = Math.Abs(x.Id - 5)
            })
            .First();

        r.Sqrt.Should().BeApproximately(1.7320508075688772, 1e-12);
        r.Pow.Should().Be(9.0);
        r.Log.Should().BeApproximately(Math.Log(3), 1e-12);
        r.Exp.Should().BeApproximately(Math.Exp(3), 1e-6);
        r.Abs.Should().Be(2L);
    }

    [Fact]
    public void MathTrigonometry_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Sin = Math.Sin(x.Id),
                Cos = Math.Cos(x.Id),
                Tan = Math.Tan(x.Id)
            })
            .First();

        r.Sin.Should().BeApproximately(Math.Sin(1), 1e-12);
        r.Cos.Should().BeApproximately(Math.Cos(1), 1e-12);
        r.Tan.Should().BeApproximately(Math.Tan(1), 1e-12);
    }

    [Fact]
    public void DateTimeAdd_ShouldShift()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Day = x.Datetime!.Value.AddDays(1),
                Month = x.Datetime!.Value.AddMonths(1),
                Year = x.Datetime!.Value.AddYears(1),
                Hour = x.Datetime!.Value.AddHours(2),
                Minute = x.Datetime!.Value.AddMinutes(30),
                Second = x.Datetime!.Value.AddSeconds(15),
                Milli = x.Datetime!.Value.AddMilliseconds(500)
            })
            .First();

        r.Day.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
        r.Month.Should().Be(new DateTime(2023, 2, 1, 10, 0, 0));
        r.Year.Should().Be(new DateTime(2024, 1, 1, 10, 0, 0));
        r.Hour.Should().Be(new DateTime(2023, 1, 1, 12, 0, 0));
        r.Minute.Should().Be(new DateTime(2023, 1, 1, 10, 30, 0));
        r.Second.Should().Be(new DateTime(2023, 1, 1, 10, 0, 15));
        r.Milli.Should().Be(new DateTime(2023, 1, 1, 10, 0, 0, 500));
    }

    [Fact]
    public void DateTimeParts_ShouldExtract()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Min = x.Datetime!.Value.Minute,
                Sec = x.Datetime!.Value.Second,
                Month = x.Datetime!.Value.Month
            })
            .First();

        r.Min.Should().Be(0);
        r.Sec.Should().Be(0);
        r.Month.Should().Be(1);
    }

    [Fact]
    public void DateTimeNow_ShouldReturnCurrentValue()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { Now = DateTime.Now, Utc = DateTime.UtcNow })
            .First();

        r.Now.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        r.Utc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ExtendedTrigonometry_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Asin = SqlFunctions.Postgres.asin(0.5),
                Acos = SqlFunctions.Postgres.acos(0.5),
                Atan = SqlFunctions.Postgres.atan(1.0),
                Atan2 = SqlFunctions.Postgres.atan2(1.0, 1.0),
                Cbrt = SqlFunctions.Postgres.cbrt(27.0),
                Sinh = SqlFunctions.Postgres.sinh(1.0),
                Cosh = SqlFunctions.Postgres.cosh(1.0),
                Tanh = SqlFunctions.Postgres.tanh(1.0),
                Asinh = SqlFunctions.Postgres.asinh(1.0),
                Acosh = SqlFunctions.Postgres.acosh(2.0),
                Atanh = SqlFunctions.Postgres.atanh(0.5),
                Degrees = SqlFunctions.Postgres.degrees(SqlFunctions.Postgres.pi()),
                Radians = SqlFunctions.Postgres.radians(180.0)
            })
            .First();

        r.Asin.Should().BeApproximately(Math.Asin(0.5), 1e-12);
        r.Acos.Should().BeApproximately(Math.Acos(0.5), 1e-12);
        r.Atan.Should().BeApproximately(Math.PI / 4, 1e-12);
        r.Atan2.Should().BeApproximately(Math.PI / 4, 1e-12);
        r.Cbrt.Should().BeApproximately(3.0, 1e-12);
        r.Sinh.Should().BeApproximately(Math.Sinh(1), 1e-12);
        r.Cosh.Should().BeApproximately(Math.Cosh(1), 1e-12);
        r.Tanh.Should().BeApproximately(Math.Tanh(1), 1e-12);
        r.Asinh.Should().BeApproximately(Math.Asinh(1), 1e-12);
        r.Acosh.Should().BeApproximately(Math.Acosh(2), 1e-12);
        r.Atanh.Should().BeApproximately(Math.Atanh(0.5), 1e-12);
        r.Degrees.Should().BeApproximately(180.0, 1e-9);
        r.Radians.Should().BeApproximately(Math.PI, 1e-12);
    }

    [Fact]
    public void ExtendedMath_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Log = SqlFunctions.Postgres.log(2.0, 8.0),
                Bucket = SqlFunctions.Postgres.width_bucket(5.0, 0.0, 10.0, 2),
                Pi = SqlFunctions.Postgres.pi(),
                Random = SqlFunctions.Postgres.random()
            })
            .First();

        r.Log.Should().BeApproximately(3.0, 1e-12);
        r.Bucket.Should().Be(2);
        r.Pi.Should().BeApproximately(Math.PI, 1e-12);
        r.Random.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void ExtendedLogFunction_TwoArgumentParameters_ShouldCompute()
    {
        LogViaParameters(2.0, 8.0).Should().BeApproximately(3.0, 1e-12);
        LogViaParameters(10.0, 100.0).Should().BeApproximately(2.0, 1e-12);
        LogViaParameters(4.0, 8.0).Should().BeApproximately(1.5, 1e-12);
    }

    [Fact]
    public void ExtendedLogFunction_NullOperands_ShouldReturnNull()
    {
        LogViaParameters(null, 8.0).Should().BeNull();
        LogViaParameters(2.0, null).Should().BeNull();
        LogViaParameters(null, null).Should().BeNull();
    }

    [Fact]
    public void ExtendedLogFunction_LiteralAndConvertedOperands_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Ints = SqlFunctions.Postgres.log(2, 8),
                Longs = SqlFunctions.Postgres.log(2L, 8L),
                Converted = SqlFunctions.Postgres.log((double?)2m, (double?)8m),
                ColumnAndLiteral = SqlFunctions.Postgres.log(2.0, x.Double)
            })
            .First();

        r.Ints.Should().BeApproximately(3.0, 1e-12);
        r.Longs.Should().BeApproximately(3.0, 1e-12);
        r.Converted.Should().BeApproximately(3.0, 1e-12);
        // complex_entity row 1 stores 5 in the double precision column: log base 2 of 5.
        r.ColumnAndLiteral.Should().BeApproximately(Math.Log(5.0, 2.0), 1e-12);
    }

    [Fact]
    public void ExtendedLogFunction_IntegralParameters_ShouldCompute()
    {
        LogViaIntParameters(2, 8).Should().BeApproximately(3.0, 1e-12);
        LogViaLongParameters(10L, 100L).Should().BeApproximately(2.0, 1e-12);
    }

    private double? LogViaIntParameters(int baseValue, int x) =>
        _sut.ComplexEntity.Where(e => e.Id == 1)
            .Select(e => new
            {
                Log = SqlFunctions.Postgres.log(
                    SqlFunctions.Parameter<int>(0),
                    SqlFunctions.Parameter<int>(1))
            })
            .First(baseValue, x)
            .Log;

    private double? LogViaLongParameters(long baseValue, long x) =>
        _sut.ComplexEntity.Where(e => e.Id == 1)
            .Select(e => new
            {
                Log = SqlFunctions.Postgres.log(
                    SqlFunctions.Parameter<long>(0),
                    SqlFunctions.Parameter<long>(1))
            })
            .First(baseValue, x)
            .Log;

    private double? LogViaParameters(double? baseValue, double? x) =>
        _sut.ComplexEntity.Where(e => e.Id == 1)
            .Select(e => new
            {
                Log = SqlFunctions.Postgres.log(
                    SqlFunctions.Parameter<double?>(0),
                    SqlFunctions.Parameter<double?>(1))
            })
            .First(baseValue, x)
            .Log;

    [Fact]
    public void ExtendedStringFunctions_ShouldTransform()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Split = SqlFunctions.Postgres.split_part("a,b,c", ",", 2),
                StrPos = SqlFunctions.Postgres.strpos(x.String, "f"),
                Left = SqlFunctions.Postgres.left(x.String, 3),
                Right = SqlFunctions.Postgres.right(x.String, 3),
                LPad = SqlFunctions.Postgres.lpad("5", 3, "0"),
                RPad = SqlFunctions.Postgres.rpad("5", 3, "0"),
                Repeat = SqlFunctions.Postgres.repeat("ab", 2),
                Reverse = SqlFunctions.Postgres.reverse("abc"),
                InitCap = SqlFunctions.Postgres.initcap("hello world"),
                Translate = SqlFunctions.Postgres.translate("dadfasd", "a", "o"),
                Overlay = SqlFunctions.Postgres.overlay("dadfasd", "XX", 2, 3),
                ConcatWs = SqlFunctions.Postgres.concat_ws("-", "a", "b"),
                Format = SqlFunctions.Postgres.format("%s-%s", "a", "b"),
                Md5 = SqlFunctions.Postgres.md5("abc")
            })
            .First();

        r.Split.Should().Be("b");
        r.StrPos.Should().Be(4);
        r.Left.Should().Be("dad");
        r.Right.Should().Be("asd");
        r.LPad.Should().Be("005");
        r.RPad.Should().Be("500");
        r.Repeat.Should().Be("abab");
        r.Reverse.Should().Be("cba");
        r.InitCap.Should().Be("Hello World");
        r.Translate.Should().Be("dodfosd");
        r.Overlay.Should().Be("dXXasd");
        r.ConcatWs.Should().Be("a-b");
        r.Format.Should().Be("a-b");
        r.Md5.Should().Be("900150983cd24fb0d6963f7d28e17f72");
    }

    [Fact]
    public void ExtendedRegexpFunctions_ShouldMatch()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Replace = SqlFunctions.Postgres.regexp_replace(x.String, "a", "o"),
                Like = SqlFunctions.Postgres.regexp_like(x.String, "^d"),
                Split = SqlFunctions.Postgres.regexp_split_to_array("a,b,c", ","),
                Count = SqlFunctions.Postgres.regexp_count(x.String, "a"),
                Instr = SqlFunctions.Postgres.regexp_instr(x.String, "f")
            })
            .First();

        r.Replace.Should().Be("dodfasd");
        r.Like.Should().BeTrue();
        r.Split.Should().Equal("a", "b", "c");
        r.Count.Should().Be(2);
        r.Instr.Should().Be(4);
    }

    [Fact]
    public void FilteredAggregates_ShouldAggregate()
    {
        var r = _sut.ComplexEntity
            .Select(x => new
            {
                Filtered = SqlFunctions.Sql.string_agg(x.String, ",", () => x.Id > 0L),
                ArrayFiltered = SqlFunctions.Postgres.array_agg(x.Id, () => x.Id > 0L)
            })
            .First();

        r.Filtered.Should().NotBeNullOrEmpty();
        r.ArrayFiltered.Should().NotBeNull();
    }

    [Fact]
    public void DateFunctions_ShouldShift()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                AddMonth = SqlFunctions.Sql.date_add("month", 1, x.Datetime),
                AddYear = SqlFunctions.Sql.date_add("year", 1, x.Datetime),
                DiffMonth = SqlFunctions.Sql.date_diff("month", x.Datetime, SqlFunctions.Sql.date_add("month", 2, x.Datetime)),
                Eom = SqlFunctions.Sql.end_of_month(x.Datetime),
                Greatest3 = SqlFunctions.Sql.greatest(x.Id, 2L, 3L),
                Least3 = SqlFunctions.Sql.least(x.Id, 2L, 3L)
            })
            .First();

        r.AddMonth.Should().Be(new DateTime(2023, 2, 1, 10, 0, 0));
        r.AddYear.Should().Be(new DateTime(2024, 1, 1, 10, 0, 0));
        r.DiffMonth.Should().Be(2);
        r.Eom.Should().Be(new DateTime(2023, 1, 31));
        r.Greatest3.Should().Be(3L);
        r.Least3.Should().Be(1L);
    }

    [Fact]
    public void ConditionalValueFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Iif = SqlFunctions.Sql.iif(x.Id > 0, "yes", "no"),
                Greatest = SqlFunctions.Sql.greatest(x.Id, 5L),
                Least = SqlFunctions.Sql.least(x.Id, 2L),
                Trunc = SqlFunctions.Sql.date_trunc("month", x.Datetime),
                FromParts = SqlFunctions.Sql.date_from_parts(2023, 1, 1)
            })
            .First();

        r.Iif.Should().Be("yes");
        r.Greatest.Should().Be(5L);
        r.Least.Should().Be(1L);
        r.Trunc.Should().Be(new DateTime(2023, 1, 1, 0, 0, 0));
        r.FromParts.Should().Be(new DateTime(2023, 1, 1));
    }

    [Fact]
    public void ArrayAgg_ShouldAggregate()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.Postgres.array_agg(x.Id))
            .First();

        r.Should().BeEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Fact]
    public void PostgresAggregates_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Select(x => new
            {
                BoolAnd = SqlFunctions.Postgres.bool_and(x.Id > 0),
                BoolOr = SqlFunctions.Postgres.bool_or(x.Id > 2),
                Every = SqlFunctions.Postgres.every(x.Id > 0),
                BitAnd = SqlFunctions.Postgres.bit_and((int)x.Id),
                BitOr = SqlFunctions.Postgres.bit_or((int)x.Id),
                BitXor = SqlFunctions.Postgres.bit_xor((int)x.Id),
                Corr = SqlFunctions.Sql.corr((double)x.Id, (double)x.Id),
                CovarPop = SqlFunctions.Sql.covar_pop((double)x.Id, (double)x.Id),
                CovarSamp = SqlFunctions.Sql.covar_samp((double)x.Id, (double)x.Id),
                Slope = SqlFunctions.Postgres.regr_slope((double)x.Id, (double)x.Id),
                Intercept = SqlFunctions.Postgres.regr_intercept((double)x.Id, (double)x.Id),
                R2 = SqlFunctions.Postgres.regr_r2((double)x.Id, (double)x.Id),
                RegrCount = SqlFunctions.Postgres.regr_count((double)x.Id, (double)x.Id),
                AvgX = SqlFunctions.Postgres.regr_avgx((double)x.Id, (double)x.Id),
                AvgY = SqlFunctions.Postgres.regr_avgy((double)x.Id, (double)x.Id)
            })
            .First();

        r.BoolAnd.Should().BeTrue();
        r.BoolOr.Should().BeTrue();
        r.Every.Should().BeTrue();
        r.BitAnd.Should().Be(0);
        r.BitOr.Should().Be(3);
        r.BitXor.Should().Be(0);
        r.Corr.Should().BeApproximately(1.0, 1e-12);
        r.CovarPop.Should().BeApproximately(2.0 / 3.0, 1e-12);
        r.CovarSamp.Should().BeApproximately(1.0, 1e-12);
        r.Slope.Should().BeApproximately(1.0, 1e-12);
        r.Intercept.Should().BeApproximately(0.0, 1e-12);
        r.R2.Should().BeApproximately(1.0, 1e-12);
        r.RegrCount.Should().Be(3.0);
        r.AvgX.Should().BeApproximately(2.0, 1e-12);
        r.AvgY.Should().BeApproximately(2.0, 1e-12);
    }

    [Fact]
    public void ExtendedDateFunctions_ShouldConvert()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Char = SqlFunctions.Postgres.to_char(x.Datetime, "YYYY-MM"),
                Date = SqlFunctions.Postgres.to_date("2023-01-01", "YYYY-MM-DD"),
                Number = SqlFunctions.Postgres.to_number("42", "999"),
                Timestamp = SqlFunctions.Postgres.to_timestamp(0.0),
                Nulls = SqlFunctions.Postgres.num_nulls(x.String, x.Int),
                NonNulls = SqlFunctions.Postgres.num_nonnulls(x.String, x.Int),
                Type = SqlFunctions.Postgres.pg_typeof(x.Id)
            })
            .First();

        r.Char.Should().Be("2023-01");
        r.Date.Should().Be(new DateTime(2023, 1, 1));
        r.Number.Should().Be(42m);
        r.Timestamp!.Value.Year.Should().Be(1970);
        r.Nulls.Should().Be(1);
        r.NonNulls.Should().Be(1);
        r.Type.Should().Be("bigint");
    }

    [Fact]
    public void ExtendedMathMisc_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Gcd = SqlFunctions.Postgres.gcd(12L, 18L),
                Lcm = SqlFunctions.Postgres.lcm(4L, 6L),
                Factorial = SqlFunctions.Postgres.factorial(5)
            })
            .First();

        r.Gcd.Should().Be(6L);
        r.Lcm.Should().Be(12L);
        r.Factorial.Should().Be(120);
    }

    [Fact]
    public void KeywordDateFunctions_ShouldReturnCurrent()
    {
        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Today = SqlFunctions.Postgres.current_date(),
                LocalTimestamp = SqlFunctions.Postgres.localtimestamp()
            })
            .First();

        r.Today.Should().NotBeNull();
        r.LocalTimestamp.Should().NotBeNull();
    }

    [Fact]
    public void IntervalFunctions_ShouldMaterialiseTimeSpan()
    {
        // `current_time()` is deliberately absent: PostgreSQL returns `time with time zone`, which the
        // Npgsql driver cannot read as a TimeSpan (its declared return type). See the work plan §10.
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Built = SqlFunctions.Postgres.make_interval(0, 0, 1, 2, 3, 4.0),
                JustifiedHours = SqlFunctions.Postgres.justify_hours(
                    SqlFunctions.Postgres.make_interval(0, 0, 0, 30, 0, 0.0)),
                JustifiedDays = SqlFunctions.Postgres.justify_days(
                    SqlFunctions.Postgres.make_interval(0, 0, 20, 0, 0, 0.0)),
                Time = SqlFunctions.Postgres.localtime()
            })
            .First();

        // A PostgreSQL interval materialises through the TimeSpan read path.
        r.Built.Should().Be(new TimeSpan(1, 2, 3, 4));
        // justify_hours/justify_days only re-format the interval, so the duration is unchanged.
        r.JustifiedHours.Should().Be(TimeSpan.FromHours(30));
        r.JustifiedDays.Should().Be(TimeSpan.FromDays(20));
        r.Time.Should().NotBeNull();
    }

    [Fact]
    public void TimezoneAndLikeEscape_ShouldWork()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Tz = SqlFunctions.Postgres.timezone("UTC", x.Datetime),
                Matches = SqlFunctions.Sql.like(x.String, "dad%", "!")
            })
            .First();

        r.Tz!.Value.Hour.Should().Be(10);
        r.Matches.Should().BeTrue();
    }

    [Fact]
    public void RepeatedQuery_WithScalarFunctions_ShouldCoverParamRefresh()
    {
        var threshold = 0L;

        for (var i = 0; i < 2; i++)
        {
            var r = _sut.ComplexEntity
                .Where(x => x.Id > threshold)
                .Select(x => new
                {
                    Lower = x.String!.ToLower(),
                    Upper = x.String!.ToUpper(),
                    Trim = x.String!.Trim(),
                    TrimStart = x.String!.TrimStart(),
                    TrimEnd = x.String!.TrimEnd(),
                    Replaced = x.String!.Replace("a", "o"),
                    Ends = x.String!.EndsWith("sd"),
                    Last = x.String!.LastIndexOf("a"),
                    Empty = string.IsNullOrEmpty(x.String),
                    Sub = x.String!.Substring(1),
                    Pad = x.String!.PadLeft(9, '0'),
                    IndexOf = x.String!.IndexOf("a"),
                    Nulled = SqlFunctions.Sql.nullif(x.String, "zzz"),
                    Like = SqlFunctions.Sql.like(x.String, "dad%"),
                    Iif = SqlFunctions.Sql.iif(x.Id > 1, "y", "n"),
                    Greatest = SqlFunctions.Sql.greatest(x.Id, 2L),
                    Least = SqlFunctions.Sql.least(x.Id, 2L),
                    Trunc = SqlFunctions.Sql.date_trunc("month", x.Datetime),
                    Add = SqlFunctions.Sql.date_add("day", 1, x.Datetime),
                    Diff = SqlFunctions.Sql.date_diff("day", x.Datetime, x.Datetime),
                    Eom = SqlFunctions.Sql.end_of_month(x.Datetime),
                    FromParts = SqlFunctions.Sql.date_from_parts(2023, 1, 1),
                    Extract = SqlFunctions.Sql.extract("year", x.Datetime),
                    Epoch = SqlFunctions.Sql.date_part("epoch", x.Datetime)
                })
                .First();

            r.Lower.Should().NotBeNull();
            r.Extract.Should().Be(2023);
        }
    }

    [Fact]
    public void RepeatedQuery_WithAggregates_ShouldCoverParamRefresh()
    {
        var threshold = 0L;

        for (var i = 0; i < 2; i++)
        {
            var r = _sut.ComplexEntity
                .Where(x => x.Id > threshold)
                .Select(x => new
                {
                    BoolAnd = SqlFunctions.Postgres.bool_and(x.Id > 0),
                    BoolOr = SqlFunctions.Postgres.bool_or(x.Id > 0),
                    BitAnd = SqlFunctions.Postgres.bit_and((int)x.Id),
                    BitOr = SqlFunctions.Postgres.bit_or((int)x.Id),
                    Corr = SqlFunctions.Sql.corr((double)x.Id, (double)x.Id),
                    CovarPop = SqlFunctions.Sql.covar_pop((double)x.Id, (double)x.Id),
                    Slope = SqlFunctions.Postgres.regr_slope((double)x.Id, (double)x.Id),
                    Intercept = SqlFunctions.Postgres.regr_intercept((double)x.Id, (double)x.Id),
                    StringAgg = SqlFunctions.Sql.string_agg(x.String, ","),
                    ArrayAgg = SqlFunctions.Postgres.array_agg(x.Id),
                    Pct = SqlFunctions.Postgres.percentile_cont(0.5, () => (double)x.Id),
                    PctDisc = SqlFunctions.Postgres.percentile_disc(0.5, () => x.Id),
                    Mode = SqlFunctions.Postgres.mode(() => x.Id)
                })
                .First();

            r.BoolAnd.Should().BeTrue();
            r.Pct.Should().BeGreaterThan(0);
            r.Mode.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void Sha224_384_512_ShouldReturnKnownDigests()
    {
        var data = new byte[] { 0x61, 0x62, 0x63 };

        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.Postgres.sha224(SqlFunctions.Parameter<byte[]>(0)),
                B = SqlFunctions.Postgres.sha384(SqlFunctions.Parameter<byte[]>(0)),
                C = SqlFunctions.Postgres.sha512(SqlFunctions.Parameter<byte[]>(0))
            })
            .First(data);

        r.A.Should().Equal(Convert.FromHexString("23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7"));
        r.B.Should().Equal(Convert.FromHexString("cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7"));
        r.C.Should().Equal(Convert.FromHexString("ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f"));
    }

    [Fact]
    public void RegexpSubstrShouldReturnMatch()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Missing = SqlFunctions.Postgres.regexp_substr(x.String, "[0-9]"),
                Digits = SqlFunctions.Postgres.regexp_substr("a1b2", "[0-9]+")
            })
            .First();

        r.Missing.Should().BeNull();
        r.Digits.Should().Be("1");
    }

    [Fact]
    public void MakeTimeAndTimestamp_ShouldConstruct()
    {
        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                T = SqlFunctions.Postgres.make_time(12, 30, 15.0),
                S = SqlFunctions.Postgres.make_timestamp(2020, 1, 2, 3, 4, 5.0)
            })
            .First();

        r.T.Should().Be(new TimeSpan(12, 30, 15));
        r.S.Should().Be(new DateTime(2020, 1, 2, 3, 4, 5));
    }

    [Fact]
    public void AgeAndDateBin_ShouldCompute()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Age = SqlFunctions.Postgres.age(x.Datetime, SqlFunctions.Postgres.make_timestamp(2023, 1, 1, 0, 0, 0.0)),
                Binned = SqlFunctions.Postgres.date_bin("1 hour", x.Datetime, x.Datetime)
            })
            .First();

        r.Age.Should().Be(TimeSpan.FromHours(10));
        r.Binned.Should().Be(new DateTime(2023, 1, 1, 10, 0, 0));
    }

    [Fact]
    [Trait("Issue", "281")]
    public void NumericScaleFunctions_ShouldReportAndTrimScale()
    {
        decimal? typedNull = null;

        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Scale = SqlFunctions.Postgres.scale(12.3400m),
                MinScale = SqlFunctions.Postgres.min_scale(12.3400m),
                Trimmed = SqlFunctions.Postgres.trim_scale(12.3400m),
                ScaleZero = SqlFunctions.Postgres.scale(0.0000m),
                MinScaleZero = SqlFunctions.Postgres.min_scale(0.0000m),
                TrimmedZero = SqlFunctions.Postgres.trim_scale(0.0000m),
                ScaleNegative = SqlFunctions.Postgres.scale(-12.3400m),
                MinScaleNegative = SqlFunctions.Postgres.min_scale(-12.3400m),
                TrimmedNegative = SqlFunctions.Postgres.trim_scale(-12.3400m),
                ScaleUnchanged = SqlFunctions.Postgres.scale(12.3456m),
                MinScaleUnchanged = SqlFunctions.Postgres.min_scale(12.3456m),
                TrimmedUnchanged = SqlFunctions.Postgres.trim_scale(12.3456m),
                ScaleNull = SqlFunctions.Postgres.scale(typedNull),
                MinScaleNull = SqlFunctions.Postgres.min_scale(typedNull),
                TrimmedNull = SqlFunctions.Postgres.trim_scale(typedNull)
            })
            .First();

        r.Scale.Should().Be(4);
        ((object)r.Scale!).Should().BeOfType<int>();
        r.MinScale.Should().Be(2);
        ((object)r.MinScale!).Should().BeOfType<int>();
        r.Trimmed.Should().Be(12.34m);
        ((object)r.Trimmed!).Should().BeOfType<decimal>();
        DecimalScale(r.Trimmed!.Value).Should().Be(2);

        r.ScaleZero.Should().Be(4);
        r.MinScaleZero.Should().Be(0);
        r.TrimmedZero.Should().Be(0m);
        DecimalScale(r.TrimmedZero!.Value).Should().Be(0);

        r.ScaleNegative.Should().Be(4);
        r.MinScaleNegative.Should().Be(2);
        r.TrimmedNegative.Should().Be(-12.34m);
        DecimalScale(r.TrimmedNegative!.Value).Should().Be(2);

        r.ScaleUnchanged.Should().Be(4);
        r.MinScaleUnchanged.Should().Be(4);
        r.TrimmedUnchanged.Should().Be(12.3456m);
        DecimalScale(r.TrimmedUnchanged!.Value).Should().Be(4);

        r.ScaleNull.Should().BeNull();
        r.MinScaleNull.Should().BeNull();
        r.TrimmedNull.Should().BeNull();
    }

    [Fact]
    [Trait("Issue", "281")]
    public void NumericScaleFunctions_ShouldWorkOnNullableColumnProjections()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 2)
            .Select(x => new
            {
                Scale = SqlFunctions.Postgres.scale(x.Numeric),
                MinScale = SqlFunctions.Postgres.min_scale(x.Numeric),
                Trimmed = SqlFunctions.Postgres.trim_scale(x.Numeric)
            })
            .First();

        r.Scale.Should().Be(0);
        r.MinScale.Should().Be(0);
        r.Trimmed.Should().Be(6m);
        DecimalScale(r.Trimmed!.Value).Should().Be(0);
    }

    [Fact]
    [Trait("Issue", "281")]
    public void NumericScaleFunctions_ShouldPreserveParameterScale()
    {
        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Scale = SqlFunctions.Postgres.scale(SqlFunctions.Parameter<decimal>(0)),
                MinScale = SqlFunctions.Postgres.min_scale(SqlFunctions.Parameter<decimal>(0)),
                Trimmed = SqlFunctions.Postgres.trim_scale(SqlFunctions.Parameter<decimal>(0))
            })
            .First(12.3400m);

        r.Scale.Should().Be(4);
        r.MinScale.Should().Be(2);
        r.Trimmed.Should().Be(12.34m);
        DecimalScale(r.Trimmed!.Value).Should().Be(2);
    }

    [Fact]
    public void CurrentSettingAndSetConfig_ShouldReadAndWrite()
    {
        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Server = SqlFunctions.Postgres.current_setting("server_version"),
                Missing = SqlFunctions.Postgres.current_setting("nextorm.missing", true),
                Set = SqlFunctions.Postgres.set_config("nextorm.test", "42", false)
            })
            .First();

        r.Server.Should().NotBeNullOrEmpty();
        r.Missing.Should().BeNull();
        r.Set.Should().Be("42");
    }

    [Fact]
    public async Task SequenceFunctions_ShouldAdvanceAndSet()
    {
        await using (var connection = new NpgsqlConnection(PostgresContainer.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("create sequence if not exists nextorm_test_seq start 1", connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Next = SqlFunctions.Postgres.nextval("nextorm_test_seq"),
                Set = SqlFunctions.Postgres.setval("nextorm_test_seq", 100L)
            })
            .First();

        r.Next.Should().BeGreaterThanOrEqualTo(1);
        r.Set.Should().Be(100);
    }

    [Fact]
    public void SqlJsonConstructorsAndQueryFunctions_ShouldWork()
    {
        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Arr = SqlFunctions.Postgres.json_array(1, 2, 3),
                BArr = SqlFunctions.Postgres.jsonb_array(1, 2, 3),
                Val = SqlFunctions.Postgres.json_value(SqlFunctions.Postgres.jsonb_build_object("a", 1), "$.a"),
                Query = SqlFunctions.Postgres.json_query(SqlFunctions.Postgres.jsonb_build_object("a", 1), "$.a"),
                Exists = SqlFunctions.Postgres.json_exists(SqlFunctions.Postgres.jsonb_build_object("a", 1), "$.a", true)
            })
            .First();

        using (var arr = JsonDocument.Parse(r.Arr))
            arr.RootElement.GetArrayLength().Should().Be(3);

        using (var arr = JsonDocument.Parse(r.BArr))
            arr.RootElement.GetArrayLength().Should().Be(3);

        r.Val.Should().Be("1");
        r.Query.Should().Be("1");
        r.Exists.Should().BeTrue();
    }

    [Fact]
    public void PortableCotAndLength_ShouldCompute()
    {
        var r = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Cot = SqlFunctions.Sql.cot(1.0),
                Bl = SqlFunctions.Sql.bit_length("abc"),
                Ol = SqlFunctions.Sql.octet_length("abc")
            })
            .First();

        ((double?)r.Cot).GetValueOrDefault().Should().BeApproximately(1.0 / Math.Tan(1.0), 1e-9);
        r.Bl.Should().Be(24);
        r.Ol.Should().Be(3);
    }

    [Fact]
    public void LastIndexOfOrdinalOverloads_ShouldReturnTheLastPosition()
    {
        // PostgreSQL has the reverse primitive the shared renderer composes. "dadfasd" (row 1) holds
        // its last 'a' at zero-based index 4, and the ignore-case arm searches for the upper-case
        // spelling, which cannot match unless both sides are case-folded.
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Plain = x.String!.LastIndexOf("a"),
                Ordinal = x.String!.LastIndexOf("a", StringComparison.Ordinal),
                OrdinalIgnoreCase = x.String!.LastIndexOf("A", StringComparison.OrdinalIgnoreCase),
                Missing = x.String!.LastIndexOf("zzz", StringComparison.Ordinal)
            })
            .First();

        r.Plain.Should().Be(4);
        r.Ordinal.Should().Be(4);
        r.OrdinalIgnoreCase.Should().Be(4);
        r.Missing.Should().Be(-1);
    }

    [Fact]
    public void SearchOrdinalOverloads_ShouldMatchByCodePoint()
    {
        // PostgreSQL opts into ordinal LIKE with the "C" collation; row 2 is "xxx", so the
        // upper-case ordinal spellings must not match while the ignore-case forms do.
        var r = _sut.ComplexEntity.Where(x => x.Id == 2)
            .Select(x => new
            {
                ContainsOrdinal = x.String!.Contains("xx", StringComparison.Ordinal),
                ContainsOrdinalUpper = x.String!.Contains("XX", StringComparison.Ordinal),
                StartsWithOrdinal = x.String!.StartsWith("xx", StringComparison.Ordinal),
                StartsWithOrdinalUpper = x.String!.StartsWith("XX", StringComparison.Ordinal),
                EndsWithOrdinal = x.String!.EndsWith("xx", StringComparison.Ordinal),
                EndsWithOrdinalUpper = x.String!.EndsWith("XX", StringComparison.Ordinal),
                ContainsIgnoreCase = x.String!.Contains("XX", StringComparison.OrdinalIgnoreCase),
                StartsWithIgnoreCase = x.String!.StartsWith("XX", StringComparison.OrdinalIgnoreCase),
                EndsWithIgnoreCase = x.String!.EndsWith("XX", StringComparison.OrdinalIgnoreCase)
            })
            .First();

        r.ContainsOrdinal.Should().BeTrue();
        r.ContainsOrdinalUpper.Should().BeFalse();
        r.StartsWithOrdinal.Should().BeTrue();
        r.StartsWithOrdinalUpper.Should().BeFalse();
        r.EndsWithOrdinal.Should().BeTrue();
        r.EndsWithOrdinalUpper.Should().BeFalse();
        r.ContainsIgnoreCase.Should().BeTrue();
        r.StartsWithIgnoreCase.Should().BeTrue();
        r.EndsWithIgnoreCase.Should().BeTrue();
    }

    /// <summary>
    /// The pair-of-columns range translator accepts only predicates, inspection functions and
    /// captured range values. A range-returning function (projected or used as an operand), the empty
    /// range and a multirange operand are rejected at translation with the documented messages.
    /// </summary>
    [Fact]
    public void RangeColumnPairOperandGuards_ShouldBeRejected()
    {
        var ctx = (DataContext)_sut.DataProvider;

        FluentActions.Invoking(() => ctx.From<CommonTestSuite.IRangeProbe>()
                .Where(x => x.Id == 1
                    && SqlFunctions.Postgres.range_union(x.During, new Range<int>(1, 10)) == new Range<int>(2, 9))
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*returns a range and cannot be expressed over a pair of columns*");

        FluentActions.Invoking(() => ctx.From<CommonTestSuite.IRangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, SqlFunctions.Postgres.int4range(1, 10)))
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The PostgreSQL range function 'int4range' returns a range*");

        FluentActions.Invoking(() => ctx.From<CommonTestSuite.IRangeProbe>()
                .Where(x => SqlFunctions.Postgres.range_contains(x.During, Range<int>.Empty))
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*The empty range cannot be expressed over a pair of columns*");

        FluentActions.Invoking(() => ctx.From<CommonTestSuite.IRangeProbe>()
                .Select(x => SqlFunctions.Postgres.range_union(x.During, new Range<int>(1, 10)))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The PostgreSQL range function 'range_union' returns a range and cannot be expressed over a pair of columns. Use a provider with a native range type (PostgreSQL) or a range-returning projection.");

        FluentActions.Invoking(() => ctx.From<CommonTestSuite.IRangeProbe>()
                .Where(x => SqlFunctions.Postgres.overlaps(x.During, Array.Empty<Range<int>>()))
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*is neither a range column pair nor a captured range value*");
    }

    [Fact]
    public void ConditionalAndNullIfFunctions_ShouldReturnValues()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Nulled = SqlFunctions.Sql.nullif(x.String, "dadfasd"),
                Kept = SqlFunctions.Sql.nullif(x.String, "other"),
                ChosenFalse = SqlFunctions.Sql.iif(x.Id > 1, "big", "small"),
                ChosenTrue = SqlFunctions.Sql.iif(x.Id == 1, "one", "other"),
                Greatest = SqlFunctions.Sql.greatest(x.Id, 2L, 3L),
                Least = SqlFunctions.Sql.least(x.Id, 2L, 3L)
            })
            .First();

        r.Nulled.Should().BeNull();
        r.Kept.Should().Be("dadfasd");
        r.ChosenFalse.Should().Be("small");
        r.ChosenTrue.Should().Be("one");
        r.Greatest.Should().Be(3L);
        r.Least.Should().Be(1L);
    }

    [Fact]
    public void GreatestAndLeast_WithoutArguments_ShouldBeRejected()
    {
        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.greatest<long>())
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("greatest/least requires at least one argument.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.least<long>())
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("greatest/least requires at least one argument.");
    }

    [Fact]
    public void DateFunctionGuards_ShouldBeRejected()
    {
        var field = "month";
        var part = "year";

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.date_trunc(field, x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The date_trunc field must be a constant string.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.date_trunc("fortnight", x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("'fortnight' is not a supported date_trunc field for this provider.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.date_add(field, 1, x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The date_add field must be a constant string.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.date_add("fortnight", 1, x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("'fortnight' is not a supported date_add field for this provider.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.date_diff(field, x.Datetime, x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The date_diff field must be a constant string.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.date_diff("decade", x.Datetime, x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("'decade' is not a supported date_diff field for this provider.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.extract(part, x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The date part must be a constant string.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.extract("epoch", x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("'epoch' is a numeric date part; use date_part.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.date_part("year", x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("date_part only provides the numeric 'epoch' part; use extract for the 'year' part.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.Sql.extract("fortnight", x.Datetime))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("'fortnight' is not a supported date part for this provider.");
    }

    [Fact]
    public void FullTextPredicates_ShouldMatchTheDocument()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Contains = SqlFunctions.Sql.contains(x.String, "dadfasd"),
                FreeText = SqlFunctions.Sql.freetext(x.String, "dadfasd"),
                Missing = SqlFunctions.Sql.freetext(x.String, "zzz")
            })
            .First();

        r.Contains.Should().BeTrue();
        r.FreeText.Should().BeTrue();
        r.Missing.Should().BeFalse();
    }

    [Fact]
    public void DistinctAndFilteredAggregates_ShouldCompute()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                Count = SqlFunctions.Sql.count(),
                CountArgument = SqlFunctions.Sql.count(x.Id),
                CountFiltered = SqlFunctions.Sql.count(() => x.Id <= 4),
                CountBig = SqlFunctions.Sql.count_big(),
                CountBigFiltered = SqlFunctions.Sql.count_big(() => x.Id > 5),
                CountDistinct = SqlFunctions.Sql.count_distinct(x.Id),
                CountBigDistinct = SqlFunctions.Sql.count_big_distinct(x.Id),
                Min = SqlFunctions.Sql.min(x.Id),
                Max = SqlFunctions.Sql.max(x.Id),
                MinFiltered = SqlFunctions.Sql.min(x.Id, () => x.Id > 3),
                MaxFiltered = SqlFunctions.Sql.max(x.Id, () => x.Id < 4),
                Avg = SqlFunctions.Sql.avg((double)x.Id),
                Sum = SqlFunctions.Sql.sum((long)x.Id),
                AvgFiltered = SqlFunctions.Sql.avg((double)x.Id, () => x.Id <= 4),
                SumFiltered = SqlFunctions.Sql.sum((long)x.Id, () => x.Id <= 4),
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
        r.CountFiltered.Should().Be(4);
        r.CountBig.Should().Be(10L);
        r.CountBigFiltered.Should().Be(5L);
        r.CountDistinct.Should().Be(10);
        r.CountBigDistinct.Should().Be(10L);
        r.Min.Should().Be(1);
        r.Max.Should().Be(10);
        r.MinFiltered.Should().Be(4);
        r.MaxFiltered.Should().Be(3);
        r.Avg.Should().BeApproximately(5.5, 1e-12);
        r.Sum.Should().Be(55L);
        r.AvgFiltered.Should().BeApproximately(2.5, 1e-12);
        r.SumFiltered.Should().Be(10L);
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
    public void OrderedSetAggregates_ShouldReturnTheOrderStatistics()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Int != null)
            .Select(x => new
            {
                PctCont = SqlFunctions.Postgres.percentile_cont(0.5, () => (double)x.Int!),
                PctDisc = SqlFunctions.Postgres.percentile_disc(0.5, () => x.Int),
                Mode = SqlFunctions.Postgres.mode(() => x.Int)
            })
            .First();

        r.PctCont.Should().BeApproximately(1.0, 1e-12);
        r.PctDisc.Should().Be(1);
        r.Mode.Should().Be(1);
    }

    [Fact]
    public void ClickHouseGlobalIn_OnPostgres_ShouldBeRejected()
    {
        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => SqlFunctions.ClickHouse.global_in(x.Id, 1, 2, 3))
                .Select(x => x.Id)
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The GLOBAL IN predicate is not supported by this provider.");
    }

    [SqlTable("inline_literal_probe")]
    internal interface IInlineLiteralProbe
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("value")]
        string? Value { get; set; }
    }

    /// <summary>
    /// #299: an inline string literal containing an apostrophe must reach PostgreSQL as one valid
    /// string literal. The probe table is owned by this test (created and dropped here); rows are
    /// inserted through bound parameters, while the WHERE values are genuine inline constants, so the
    /// literal renderer is what the server sees. The decoy proves the match is exact and the hostile
    /// value proves the corrected literal cannot terminate the statement or broaden the result.
    /// </summary>
    [Fact]
    [Trait("Issue", "299")]
    public void InlineStringLiteralRoundTrip()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists inline_literal_probe");
        Execute(ctx, "create table inline_literal_probe (id integer primary key, value varchar(100))");

        try
        {
            ctx.CreateInsertBuilder<IInlineLiteralProbe>()
                .Value(x => x.Id, 1)
                .Value(x => x.Value, "it's (a) test")
                .Insert();
            ctx.CreateInsertBuilder<IInlineLiteralProbe>()
                .Value(x => x.Id, 2)
                .Value(x => x.Value, "it's (a) test!")
                .Insert();
            ctx.CreateInsertBuilder<IInlineLiteralProbe>()
                .Value(x => x.Id, 3)
                .Value(x => x.Value, "it's; --")
                .Insert();
            ctx.CreateInsertBuilder<IInlineLiteralProbe>()
                .Value(x => x.Id, 4)
                .Value(x => x.Value, "plain")
                .Insert();

            ctx.From<IInlineLiteralProbe>()
                .Where(x => x.Value == "it's (a) test")
                .Select(x => x.Id)
                .ToList()
                .Should().Equal(new[] { 1 }, "the apostrophe literal must match its row exactly, not the longer decoy");

            ctx.From<IInlineLiteralProbe>()
                .Where(x => x.Value == "it's; --")
                .Select(x => x.Id)
                .ToList()
                .Should().Equal(new[] { 3 }, "the semicolon and comment marker stay inside the literal and never broaden the result");

            // The hostile value left the table usable: rows are still there and a bound parameter still selects.
            ctx.From<IInlineLiteralProbe>()
                .Select(x => x.Id)
                .ToList()
                .Should().HaveCount(4);

            var decoy = "it's (a) test!";
            ctx.From<IInlineLiteralProbe>()
                .Where(x => x.Value == decoy)
                .Select(x => x.Id)
                .ToList()
                .Should().Equal(2);
        }
        finally
        {
            Execute(ctx, "drop table if exists inline_literal_probe");
        }
    }

    [Fact]
    [Trait("Issue", "281")]
    public void StringFamily_ShouldComputeValuesNullsAndClrTypes()
    {
        string? nullText = null;
        byte[]? nullBytes = null;

        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Btrim1 = SqlFunctions.Postgres.btrim("  ab  "),
                Btrim2 = SqlFunctions.Postgres.btrim("xxabyy", "xy"),
                BtrimBytes = SqlFunctions.Postgres.btrim(
                    SqlFunctions.Postgres.convert_to("xxabxx", "UTF8"),
                    SqlFunctions.Postgres.convert_to("x", "UTF8")),
                Ltrim2 = SqlFunctions.Postgres.ltrim("xxab", "x"),
                LtrimBytes = SqlFunctions.Postgres.ltrim(
                    SqlFunctions.Postgres.convert_to("xxab", "UTF8"),
                    SqlFunctions.Postgres.convert_to("x", "UTF8")),
                Rtrim2 = SqlFunctions.Postgres.rtrim("abxx", "x"),
                RtrimBytes = SqlFunctions.Postgres.rtrim(
                    SqlFunctions.Postgres.convert_to("abxx", "UTF8"),
                    SqlFunctions.Postgres.convert_to("x", "UTF8")),
                StartsWith = SqlFunctions.Postgres.starts_with("dadfasd", "dad"),
                ParseIdent = SqlFunctions.Postgres.parse_ident("a.b", false),
                QuoteIdent = SqlFunctions.Postgres.quote_ident("a b"),
                QuoteIdentPlain = SqlFunctions.Postgres.quote_ident("test"),
                QuoteLiteral = SqlFunctions.Postgres.quote_literal(x.String),
                QuoteNullable = SqlFunctions.Postgres.quote_nullable(x.String),
                ToAscii = SqlFunctions.Postgres.to_ascii("abc", "LATIN1"),
                ToAscii2 = SqlFunctions.Postgres.to_ascii("abc", "LATIN2"),
                ToAsciiInt = SqlFunctions.Postgres.to_ascii("abc", 8),
                HexNeg = SqlFunctions.Postgres.to_hex(-1),
                HexLong = SqlFunctions.Postgres.to_hex(255L),
                Oct = SqlFunctions.Postgres.to_oct(8),
                OctLong = SqlFunctions.Postgres.to_oct(8L),
                Bin = SqlFunctions.Postgres.to_bin(5),
                BinLong = SqlFunctions.Postgres.to_bin(5L),
                ConvertFrom = SqlFunctions.Postgres.convert_from(
                    SqlFunctions.Postgres.convert_to("abc", "UTF8"), "UTF8"),
                ConvertTo = SqlFunctions.Postgres.convert_to("abc", "UTF8"),
                Encode = SqlFunctions.Postgres.encode(
                    SqlFunctions.Postgres.convert_to("abc", "UTF8"), "hex"),
                Decode = SqlFunctions.Postgres.decode("616263", "hex"),
                NormalizeNfc = SqlFunctions.Postgres.normalize("abc", "NFC"),
                NormalizeNfd = SqlFunctions.Postgres.normalize("\u00E9", "NFD"),
                Unistr = SqlFunctions.Postgres.unistr("a\\0441"),
                Match = SqlFunctions.Postgres.regexp_match("dadfasd", "(d)(a)"),
                MatchFlags = SqlFunctions.Postgres.regexp_match("DADFASD", "(d)(a)", "i"),
                NoMatch = SqlFunctions.Postgres.regexp_match("dadfasd", "zzz"),
                NoGroup = SqlFunctions.Postgres.regexp_match("dadfasd", "da")
            })
            .First();

        r.Btrim1.Should().Be("ab");
        r.Btrim2.Should().Be("ab");
        r.BtrimBytes.Should().Equal("ab"u8.ToArray());
        r.Ltrim2.Should().Be("ab");
        r.LtrimBytes.Should().Equal("ab"u8.ToArray());
        r.Rtrim2.Should().Be("ab");
        r.RtrimBytes.Should().Equal("ab"u8.ToArray());
        r.StartsWith.Should().BeTrue();
        r.ParseIdent.Should().Equal("a", "b");
        r.QuoteIdent.Should().Be("\"a b\"");
        r.QuoteIdentPlain.Should().Be("test");
        r.QuoteLiteral.Should().Be("'dadfasd'");
        r.QuoteNullable.Should().Be("'dadfasd'");
        r.ToAscii.Should().Be("abc");
        r.ToAscii2.Should().Be("abc");
        r.ToAsciiInt.Should().Be("abc");
        r.HexNeg.Should().Be("ffffffff");
        r.HexLong.Should().Be("ff");
        r.Oct.Should().Be("10");
        r.OctLong.Should().Be("10");
        r.Bin.Should().Be("101");
        r.BinLong.Should().Be("101");
        r.ConvertFrom.Should().Be("abc");
        r.ConvertTo.Should().Equal("abc"u8.ToArray());
        r.Encode.Should().Be("616263");
        r.Decode.Should().Equal("abc"u8.ToArray());
        r.NormalizeNfc.Should().Be("abc");
        r.NormalizeNfd.Should().Be("e\u0301");
        r.Unistr.Should().Be("a\u0441");
        r.Match.Should().Equal("d", "a");
        r.MatchFlags.Should().Equal("D", "A");
        r.NoMatch.Should().BeNull();
        r.NoGroup.Should().Equal("da");

        // Exact CLR types: the mapped result type is part of the contract, not just the value.
        ((object)r.Btrim1!).Should().BeOfType<string>();
        ((object)r.BtrimBytes!).Should().BeOfType<byte[]>();
        ((object)r.StartsWith!).Should().BeOfType<bool>();
        ((object)r.ParseIdent!).Should().BeOfType<string[]>();
        ((object)r.QuoteIdent!).Should().BeOfType<string>();
        ((object)r.ToAscii!).Should().BeOfType<string>();
        ((object)r.ToAsciiInt!).Should().BeOfType<string>();
        ((object)r.HexNeg!).Should().BeOfType<string>();
        ((object)r.ConvertTo!).Should().BeOfType<byte[]>();
        ((object)r.Decode!).Should().BeOfType<byte[]>();
        ((object)r.NormalizeNfd!).Should().BeOfType<string>();
        ((object)r.Unistr!).Should().BeOfType<string>();
        ((object)r.Match!).Should().BeOfType<string[]>();

        // NULL cases: the string column of row 3 and the bytea column of row 2 are NULL.
        var strings = _sut.ComplexEntity.Where(x => x.Id == 3)
            .Select(x => new
            {
                Btrim1 = SqlFunctions.Postgres.btrim(x.String),
                Btrim2 = SqlFunctions.Postgres.btrim(x.String, "xy"),
                Ltrim2 = SqlFunctions.Postgres.ltrim(x.String, "xy"),
                Rtrim2 = SqlFunctions.Postgres.rtrim(x.String, "xy"),
                StartsWith = SqlFunctions.Postgres.starts_with(x.String, "dad"),
                ParseIdent = SqlFunctions.Postgres.parse_ident(x.String, false),
                QuoteIdent = SqlFunctions.Postgres.quote_ident(x.String),
                QuoteLiteral = SqlFunctions.Postgres.quote_literal(x.String),
                ToAscii = SqlFunctions.Postgres.to_ascii(x.String, "LATIN1"),
                Encode = SqlFunctions.Postgres.encode(nullBytes, "hex"),
                ConvertFromNull = SqlFunctions.Postgres.convert_from(nullBytes, "UTF8"),
                ConvertToNull = SqlFunctions.Postgres.convert_to(nullText, "UTF8"),
                DecodeNull = SqlFunctions.Postgres.decode(nullText, "hex"),
                Normalize = SqlFunctions.Postgres.normalize(x.String, "NFD"),
                Unistr = SqlFunctions.Postgres.unistr(x.String),
                Match = SqlFunctions.Postgres.regexp_match(x.String, "(d)(a)"),
                MatchFlags = SqlFunctions.Postgres.regexp_match(x.String, "(d)(a)", "i"),
                QuoteNullable = SqlFunctions.Postgres.quote_nullable(x.String)
            })
            .First();

        strings.Btrim1.Should().BeNull();
        strings.Btrim2.Should().BeNull();
        strings.Ltrim2.Should().BeNull();
        strings.Rtrim2.Should().BeNull();
        strings.StartsWith.Should().BeNull();
        strings.ParseIdent.Should().BeNull();
        strings.QuoteIdent.Should().BeNull();
        strings.QuoteLiteral.Should().BeNull();
        strings.ToAscii.Should().BeNull();
        strings.Encode.Should().BeNull();
        strings.ConvertFromNull.Should().BeNull();
        strings.ConvertToNull.Should().BeNull();
        strings.DecodeNull.Should().BeNull();
        strings.Normalize.Should().BeNull();
        strings.Unistr.Should().BeNull();
        strings.Match.Should().BeNull();
        strings.MatchFlags.Should().BeNull();
        // Non-strict by definition: quote_nullable(NULL) is the literal text NULL, not a NULL value.
        strings.QuoteNullable.Should().Be("NULL");

        // The to_hex/to_oct/to_bin NULL argument must be typed: a bare NULL literal is ambiguous
        // between the integer and bigint overloads, so the column of row 1 (NULL) carries the type.
        var intNulls = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                ToHex = SqlFunctions.Postgres.to_hex(x.Int),
                ToOct = SqlFunctions.Postgres.to_oct(x.Int),
                ToBin = SqlFunctions.Postgres.to_bin(x.Int)
            })
            .First();
        intNulls.ToHex.Should().BeNull();
        intNulls.ToOct.Should().BeNull();
        intNulls.ToBin.Should().BeNull();

        var bytes = _sut.BinaryEntity.Where(x => x.Id == 2)
            .Select(x => new
            {
                Btrim = SqlFunctions.Postgres.btrim(x.Data, x.Data),
                Ltrim = SqlFunctions.Postgres.ltrim(x.Data, x.Data),
                Rtrim = SqlFunctions.Postgres.rtrim(x.Data, x.Data),
                ConvertFrom = SqlFunctions.Postgres.convert_from(x.Data, "UTF8")
            })
            .First();

        bytes.Btrim.Should().BeNull();
        bytes.Ltrim.Should().BeNull();
        bytes.Rtrim.Should().BeNull();
        bytes.ConvertFrom.Should().BeNull();

        // string_to_table is a set-returning function: one row per fragment.
        var fragments = _sut.DataProvider
            .From(() => SqlFunctions.Postgres.string_to_table("a,b,c", ","))
            .Select(row => row.Value)
            .ToList();
        fragments.Should().Equal("a", "b", "c");

        var withNullString = _sut.DataProvider
            .From(() => SqlFunctions.Postgres.string_to_table("a,,c", ",", ""))
            .Select(row => row.Value)
            .ToList();
        withNullString.Should().HaveCount(3);
        withNullString[0].Should().Be("a");
        withNullString[1].Should().BeNull();
        withNullString[2].Should().Be("c");
    }

    /// <summary>
    /// The documented <c>to_ascii(text, integer)</c> form takes a PostgreSQL encoding <em>code</em>
    /// (<c>8</c> = <c>LATIN1</c>, verified live with <c>pg_encoding_to_char</c>). The code is validated by
    /// the server, not by nextorm: a Windows codepage number such as <c>1252</c> is not an encoding code,
    /// so the emitted statement reaches PostgreSQL and fails there with <c>42704</c>.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void ToAsciiIntegerCode_ShouldReachTheServerAndValidateTheCode()
    {
        var valid = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.to_ascii("abc", 8) })
            .First();
        valid.V.Should().Be("abc");

        var invalid = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.to_ascii("abc", 1252) })
            .First();

        invalid.Should().Throw<PostgresException>()
            .Which.SqlState.Should().Be("42704", "1252 is a Windows codepage, not a PostgreSQL encoding code");
    }

    /// <summary>
    /// The session/system-information members of #281 against the live server: live values, the NULL
    /// cases (no log collector; strict functions on a NULL argument), the server-side validation of the
    /// log format and the exact CLR types.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void SystemInfoSessionFamily_ShouldComputeValuesNullsAndClrTypes()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 2)
            .Select(x => new
            {
                CsFalse = SqlFunctions.Postgres.current_schemas(false),
                CsTrue = SqlFunctions.Postgres.current_schemas(true),
                Pst = SqlFunctions.Postgres.pg_postmaster_start_time(),
                Cl = SqlFunctions.Postgres.pg_current_logfile(),
                ClFmt = SqlFunctions.Postgres.pg_current_logfile("stderr"),
                Cfg = SqlFunctions.Postgres.pg_conf_load_time(),
                Jit = SqlFunctions.Postgres.pg_jit_available(),
                Mts = SqlFunctions.Postgres.pg_my_temp_schema(),
                Iots = SqlFunctions.Postgres.pg_is_other_temp_schema("0"),
                Nqu = SqlFunctions.Postgres.pg_notification_queue_usage(),
                Bp = SqlFunctions.Postgres.pg_blocking_pids(x.Int),
                Sbp = SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(x.Int),
                Td = SqlFunctions.Postgres.pg_trigger_depth()
            })
            .First();

        var now = DateTime.UtcNow;
        r.CsFalse.Should().Contain("public").And.NotContain("pg_catalog");
        r.CsTrue.Should().Contain("pg_catalog");
        r.Pst.Should().NotBeNull().And.BeBefore(now);
        // No log collector is configured for the fixture server, so both log-file forms yield NULL.
        r.Cl.Should().BeNull();
        r.ClFmt.Should().BeNull();
        r.Cfg.Should().NotBeNull().And.BeBefore(now);
        r.Jit.Should().NotBeNull();
        r.Mts.Should().Be("0", "no temporary schema has been created by this session");
        r.Iots.Should().BeFalse();
        r.Nqu.Should().Be(0.0);
        r.Nqu!.Value.Should().BeInRange(0.0, 1.0);
        // pid 1 is not a waiting session, so neither blocking list has an entry.
        r.Bp.Should().BeEmpty();
        r.Sbp.Should().BeEmpty();
        r.Td.Should().Be(0, "the projection runs outside any trigger");

        // Exact CLR types.
        ((object)r.CsFalse!).Should().BeOfType<string[]>();
        ((object)r.CsTrue!).Should().BeOfType<string[]>();
        ((object)r.Pst!).Should().BeOfType<DateTime>();
        ((object)r.Cfg!).Should().BeOfType<DateTime>();
        ((object)r.Jit!).Should().BeOfType<bool>();
        ((object)r.Mts!).Should().BeOfType<string>();
        ((object)r.Iots!).Should().BeOfType<bool>();
        ((object)r.Nqu!).Should().BeOfType<double>();
        ((object)r.Bp!).Should().BeOfType<int[]>();
        ((object)r.Sbp!).Should().BeOfType<int[]>();
        ((object)r.Td!).Should().BeOfType<int>();

        // NULL argument: row 1 has a NULL nullableint, and the blocking-pid functions are strict.
        var nulls = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Bp = SqlFunctions.Postgres.pg_blocking_pids(x.Int),
                Sbp = SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(x.Int)
            })
            .First();
        nulls.Bp.Should().BeNull();
        nulls.Sbp.Should().BeNull();

        // The log-format argument is validated by the server, not by nextorm.
        var badFormat = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.pg_current_logfile("text") })
            .First();
        badFormat.Should().Throw<PostgresException>()
            .Which.Message.Should().Contain("log format");
    }

    /// <summary>
    /// The catalog-visibility helpers of #281 against the live server. The OIDs are resolved with raw
    /// SQL first and bound as parameters, which also proves that the renderer's <c>cast(.. as oid)</c>
    /// is load-bearing: PostgreSQL has no <c>pg_*_is_visible(text)</c> overload. An OID that names no
    /// object yields NULL, not false, and a NULL argument is NULL (the functions are strict).
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void CatalogVisibilityFamily_ShouldReportVisibilityNullsAndClrTypes()
    {
        var ctx = (DataContext)_sut.DataProvider;
        var oid0 = Scalar(ctx, "select oid from pg_collation where collname = 'default'");
        var oid1 = "0";
        var oid2 = Scalar(ctx, "select oid from pg_proc where proname = 'pg_sleep' and pronargs = 1");
        var oid3 = "0";
        var oid4 = Scalar(ctx, "select oid from pg_operator where oprname = '+' and oprleft = 'int4'::regtype and oprright = 'int4'::regtype and oprnamespace = 'pg_catalog'::regnamespace");
        var oid5 = "0";
        var oid6 = "0";
        var oid7 = "1259";
        var oid8 = Scalar(ctx, "select oid from pg_ts_config where cfgname = 'english'");
        var oid9 = Scalar(ctx, "select oid from pg_ts_dict where dictname = 'simple' and dictnamespace = 'pg_catalog'::regnamespace");
        var oid10 = Scalar(ctx, "select oid from pg_ts_parser where prsname = 'default'");
        var oid11 = Scalar(ctx, "select oid from pg_ts_template where tmplname = 'simple'");

        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                C0 = SqlFunctions.Postgres.pg_collation_is_visible(oid0),
                C1 = SqlFunctions.Postgres.pg_conversion_is_visible(oid1),
                C2 = SqlFunctions.Postgres.pg_function_is_visible(oid2),
                C3 = SqlFunctions.Postgres.pg_opclass_is_visible(oid3),
                C4 = SqlFunctions.Postgres.pg_operator_is_visible(oid4),
                C5 = SqlFunctions.Postgres.pg_opfamily_is_visible(oid5),
                C6 = SqlFunctions.Postgres.pg_statistics_obj_is_visible(oid6),
                C7 = SqlFunctions.Postgres.pg_table_is_visible(oid7),
                C8 = SqlFunctions.Postgres.pg_ts_config_is_visible(oid8),
                C9 = SqlFunctions.Postgres.pg_ts_dict_is_visible(oid9),
                C10 = SqlFunctions.Postgres.pg_ts_parser_is_visible(oid10),
                C11 = SqlFunctions.Postgres.pg_ts_template_is_visible(oid11),
                Missing = SqlFunctions.Postgres.pg_collation_is_visible("0"),
                NullArg = SqlFunctions.Postgres.pg_collation_is_visible(null)
            })
            .First();

        // A real, visible catalog object of each kind: collation, function, operator, table, text search.
        r.C0.Should().BeTrue("the default collation is visible");
        r.C2.Should().BeTrue("a pg_catalog function is visible");
        r.C4.Should().BeTrue("the int4 + operator is visible");
        r.C7.Should().BeTrue("pg_class is visible");
        r.C8.Should().BeTrue("the english text-search configuration is visible");
        r.C9.Should().BeTrue("the simple text-search dictionary is visible");
        r.C10.Should().BeTrue("the default text-search parser is visible");
        r.C11.Should().BeTrue("the simple text-search template is visible");

        // OID 0 names no object: the helpers return NULL (not false) for it.
        r.C1.Should().BeNull();
        r.C3.Should().BeNull();
        r.C5.Should().BeNull();
        r.C6.Should().BeNull();
        r.Missing.Should().BeNull();
        r.NullArg.Should().BeNull();

        // Exact CLR type: nullable boolean, the same for every name.
        ((object)r.C0!).Should().BeOfType<bool>();
        ((object)r.C7!).Should().BeOfType<bool>();

    }

    /// <summary>
    /// The encoding, catalog-format and index-property helpers of #281 against the live server: values,
    /// the NULL and error cases measured on 17.11 (<c>pg_char_to_encoding</c> returns -1 for an unknown
    /// name, the empty string for the default tablespace, NULL for an unrecognised property) and the
    /// exact CLR types.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void EncodingAndCatalogFormatFamily_ShouldComputeValuesNullsAndClrTypes()
    {
        var ctx = (DataContext)_sut.DataProvider;
        var typeOid = Scalar(ctx, "select oid from pg_type where typname = 'int4' and typnamespace = 'pg_catalog'::regnamespace");
        var amOid = Scalar(ctx, "select oid from pg_am where amname = 'btree'");
        var classOid = "1259";

        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Tiv = SqlFunctions.Postgres.pg_type_is_visible(typeOid),
                Cte = SqlFunctions.Postgres.pg_char_to_encoding("UTF8"),
                CteUnknown = SqlFunctions.Postgres.pg_char_to_encoding("nonesuch"),
                Etc = SqlFunctions.Postgres.pg_encoding_to_char(6),
                Bt = SqlFunctions.Postgres.pg_basetype("integer"),
                Bta = SqlFunctions.Postgres.pg_basetype("integer[]"),
                Do = SqlFunctions.Postgres.pg_describe_object(classOid, classOid, 0),
                Tl = SqlFunctions.Postgres.pg_tablespace_location("0"),
                Sgf = SqlFunctions.Postgres.pg_settings_get_flags("work_mem"),
                SgfEmpty = SqlFunctions.Postgres.pg_settings_get_flags("shared_buffers"),
                SgfUnknown = SqlFunctions.Postgres.pg_settings_get_flags("nonesuch"),
                Ihp = SqlFunctions.Postgres.pg_index_has_property("pg_class_oid_index", "clusterable"),
                IhpUnknown = SqlFunctions.Postgres.pg_index_has_property("pg_class_oid_index", "nonesuch"),
                Iahp = SqlFunctions.Postgres.pg_indexam_has_property(amOid, "can_order"),
                Ichp = SqlFunctions.Postgres.pg_index_column_has_property("pg_class_oid_index", 1, "orderable"),
                Iiv = SqlFunctions.Postgres.pg_input_is_valid("123", "integer"),
                IivBad = SqlFunctions.Postgres.pg_input_is_valid("abc", "integer"),
                Gss = SqlFunctions.Postgres.pg_get_serial_sequence("pg_class", "oid"),
                MissingType = SqlFunctions.Postgres.pg_type_is_visible("0"),
                NullInput = SqlFunctions.Postgres.pg_input_is_valid(null, "integer"),
                NullAm = SqlFunctions.Postgres.pg_indexam_has_property(null, "can_order")
            })
            .First();

        r.Tiv.Should().BeTrue("int4 is a visible catalog type");
        r.Cte.Should().Be(6, "UTF8 is encoding code 6");
        r.CteUnknown.Should().Be(-1, "an unknown encoding name yields -1, not an error");
        r.Etc.Should().Be("UTF8");
        r.Bt.Should().Be("integer");
        r.Bta.Should().Be("integer[]", "the base type of an array is its element type");
        r.Do.Should().Be("table pg_class");
        r.Tl.Should().BeEmpty("the default tablespace has the empty location");
        r.Sgf.Should().Equal("EXPLAIN");
        r.SgfEmpty.Should().BeEmpty();
        r.SgfUnknown.Should().BeNull();
        r.Ihp.Should().BeTrue();
        r.IhpUnknown.Should().BeNull("an unrecognised property yields NULL, not false");
        r.Iahp.Should().BeTrue();
        r.Ichp.Should().BeTrue();
        r.Iiv.Should().BeTrue();
        r.IivBad.Should().BeFalse();
        r.Gss.Should().BeNull("pg_class.oid is not a serial column");
        r.MissingType.Should().BeNull();
        r.NullInput.Should().BeNull();
        r.NullAm.Should().BeNull();

        // Exact CLR types.
        ((object)r.Tiv!).Should().BeOfType<bool>();
        ((object)r.Cte!).Should().BeOfType<int>();
        ((object)r.Etc!).Should().BeOfType<string>();
        ((object)r.Bt!).Should().BeOfType<string>();
        ((object)r.Do!).Should().BeOfType<string>();
        ((object)r.Tl!).Should().BeOfType<string>();
        ((object)r.Sgf!).Should().BeOfType<string[]>();
        ((object)r.Ihp!).Should().BeOfType<bool>();
        ((object)r.Iiv!).Should().BeOfType<bool>();

        // Two error cases: an unknown type name is a server error in both helpers.
        var badBaseType = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.pg_basetype("nonesuch") })
            .First();
        badBaseType.Should().Throw<PostgresException>().Which.SqlState.Should().Be("42704");

        var badInputType = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.pg_input_is_valid("1", "nosuchtype") })
            .First();
        badInputType.Should().Throw<PostgresException>().Which.SqlState.Should().Be("42704");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void NumericFamily_ShouldComputeValuesNullsAndClrTypes()
    {
        double? nullDouble = null;
        decimal? nullDecimal = null;

        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Div = SqlFunctions.Postgres.div(7m, 2m),
                DivNegative = SqlFunctions.Postgres.div(-7m, 2m),
                Erf = SqlFunctions.Postgres.erf(0.0),
                Erfc = SqlFunctions.Postgres.erfc(0.0),
                RandomNormal = SqlFunctions.Postgres.random_normal(0.0, 0.0),
                Acosd = SqlFunctions.Postgres.acosd(1.0),
                Asind = SqlFunctions.Postgres.asind(1.0),
                Atand = SqlFunctions.Postgres.atand(1.0),
                Atan2d = SqlFunctions.Postgres.atan2d(1.0, 1.0),
                Cosd = SqlFunctions.Postgres.cosd(60.0),
                Cotd = SqlFunctions.Postgres.cotd(45.0),
                Sind = SqlFunctions.Postgres.sind(30.0),
                Tand = SqlFunctions.Postgres.tand(45.0),
                DivNull = SqlFunctions.Postgres.div(nullDecimal, 2m),
                ErfNull = SqlFunctions.Postgres.erf(nullDouble),
                ErfcNull = SqlFunctions.Postgres.erfc(nullDouble),
                RandomNormalNull = SqlFunctions.Postgres.random_normal(nullDouble, 1.0),
                AcosdNull = SqlFunctions.Postgres.acosd(nullDouble),
                AsindNull = SqlFunctions.Postgres.asind(nullDouble),
                AtandNull = SqlFunctions.Postgres.atand(nullDouble),
                Atan2dNull = SqlFunctions.Postgres.atan2d(nullDouble, 1.0),
                CosdNull = SqlFunctions.Postgres.cosd(nullDouble),
                CotdNull = SqlFunctions.Postgres.cotd(nullDouble),
                SindNull = SqlFunctions.Postgres.sind(nullDouble),
                TandNull = SqlFunctions.Postgres.tand(nullDouble)
            })
            .First();

        r.Div.Should().Be(3m);
        ((object)r.Div!).Should().BeOfType<decimal>();
        r.DivNegative.Should().Be(-3m);
        r.Erf.Should().Be(0.0);
        r.Erfc.Should().Be(1.0);
        // A zero standard deviation makes the volatile function deterministic: it returns the mean.
        r.RandomNormal.Should().Be(0.0);
        r.Acosd.Should().Be(0.0);
        r.Asind.Should().Be(90.0);
        r.Atand.Should().Be(45.0);
        r.Atan2d.Should().Be(45.0);
        r.Cosd.Should().Be(0.5);
        r.Cotd.Should().Be(1.0);
        r.Sind.Should().Be(0.5);
        r.Tand.Should().Be(1.0);

        ((object)r.Erf!).Should().BeOfType<double>();
        ((object)r.Erfc!).Should().BeOfType<double>();
        ((object)r.RandomNormal!).Should().BeOfType<double>();
        ((object)r.Acosd!).Should().BeOfType<double>();
        ((object)r.Asind!).Should().BeOfType<double>();
        ((object)r.Atand!).Should().BeOfType<double>();
        ((object)r.Atan2d!).Should().BeOfType<double>();
        ((object)r.Cosd!).Should().BeOfType<double>();
        ((object)r.Cotd!).Should().BeOfType<double>();
        ((object)r.Sind!).Should().BeOfType<double>();
        ((object)r.Tand!).Should().BeOfType<double>();

        r.DivNull.Should().BeNull();
        r.ErfNull.Should().BeNull();
        r.ErfcNull.Should().BeNull();
        r.RandomNormalNull.Should().BeNull();
        r.AcosdNull.Should().BeNull();
        r.AsindNull.Should().BeNull();
        r.AtandNull.Should().BeNull();
        r.Atan2dNull.Should().BeNull();
        r.CosdNull.Should().BeNull();
        r.CotdNull.Should().BeNull();
        r.SindNull.Should().BeNull();
        r.TandNull.Should().BeNull();
    }

    [Fact]
    [Trait("Issue", "281")]
    public void DateTimeFamily_ShouldComputeValuesNullsAndClrTypes()
    {
        var date = new DateOnly(2024, 1, 1);
        var day = TimeSpan.FromDays(1);

        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Ds = SqlFunctions.Postgres.date_subtract(x.Datetime, day),
                Ds3 = SqlFunctions.Postgres.date_subtract(x.Datetime, day, "UTC"),
                Ji = SqlFunctions.Postgres.justify_interval(TimeSpan.FromHours(25)),
                Mt = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5),
                Mt3 = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5, "UTC"),
                Ct = SqlFunctions.Postgres.clock_timestamp(),
                St = SqlFunctions.Postgres.statement_timestamp(),
                Tt = SqlFunctions.Postgres.transaction_timestamp(),
                Tod = SqlFunctions.Postgres.timeofday(),
                IsfDate = SqlFunctions.Postgres.isfinite(date),
                IsfTs = SqlFunctions.Postgres.isfinite(x.Datetime),
                IsfInterval = SqlFunctions.Postgres.isfinite(day)
            })
            .First();

        // dt of row 1 is 2023-01-01 10:00:00; the interval form subtracts one day.
        r.Ds.Should().Be(new DateTime(2022, 12, 31, 10, 0, 0));
        r.Ds3.Should().Be(new DateTime(2022, 12, 31, 10, 0, 0));
        r.Ji.Should().Be(TimeSpan.FromDays(1) + TimeSpan.FromHours(1));
        r.Mt.Should().Be(new DateTime(2024, 1, 2, 3, 4, 5).AddMilliseconds(500));
        r.Mt3.Should().Be(new DateTime(2024, 1, 2, 3, 4, 5).AddMilliseconds(500));
        r.Ct.Should().NotBeNull();
        r.St.Should().NotBeNull();
        r.Tt.Should().NotBeNull();
        // transaction_timestamp() is the transaction-start clock and statement_timestamp() the
        // statement-start clock; in an implicit (autocommit) transaction the server sets them at
        // slightly different instants, so only their proximity is guaranteed — an exact equality
        // holds inside a single explicit transaction block, not here.
        r.St.Should().BeCloseTo(r.Tt!.Value, TimeSpan.FromSeconds(5),
            "the statement and transaction clocks are both taken at the start of this statement");
        r.Ct.Should().BeCloseTo(r.St!.Value, TimeSpan.FromMinutes(5));
        r.Tod.Should().Contain("UTC");
        r.IsfDate.Should().BeTrue();
        r.IsfTs.Should().BeTrue();
        r.IsfInterval.Should().BeTrue();

        // Exact CLR types.
        ((object)r.Ds!).Should().BeOfType<DateTime>();
        ((object)r.Ds3!).Should().BeOfType<DateTime>();
        ((object)r.Ji!).Should().BeOfType<TimeSpan>();
        ((object)r.Mt!).Should().BeOfType<DateTime>();
        ((object)r.Ct!).Should().BeOfType<DateTime>();
        ((object)r.St!).Should().BeOfType<DateTime>();
        ((object)r.Tt!).Should().BeOfType<DateTime>();
        ((object)r.Tod!).Should().BeOfType<string>();
        ((object)r.IsfDate!).Should().BeOfType<bool>();
        ((object)r.IsfTs!).Should().BeOfType<bool>();
        ((object)r.IsfInterval!).Should().BeOfType<bool>();

        // NULL row: row 3 has a NULL string, and the CASE guard turns every projection into NULL. This
        // is the only way to obtain a NULL from the no-argument, non-strict-by-nature timestamp
        // functions; the strict members are checked through the same row.
        var nulls = _sut.ComplexEntity.Where(x => x.Id == 3)
            .Select(x => new
            {
                Ds = x.String == null ? null : SqlFunctions.Postgres.date_subtract(x.Datetime, day),
                Ds3 = x.String == null ? null : SqlFunctions.Postgres.date_subtract(x.Datetime, day, "UTC"),
                Ji = x.String == null ? null : SqlFunctions.Postgres.justify_interval(TimeSpan.FromHours(25)),
                Mt = x.String == null ? null : SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5),
                Mt3 = x.String == null ? null : SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5, "UTC"),
                Ct = x.String == null ? null : SqlFunctions.Postgres.clock_timestamp(),
                St = x.String == null ? null : SqlFunctions.Postgres.statement_timestamp(),
                Tt = x.String == null ? null : SqlFunctions.Postgres.transaction_timestamp(),
                Tod = x.String == null ? null : SqlFunctions.Postgres.timeofday(),
                IsfDate = x.String == null ? null : SqlFunctions.Postgres.isfinite(date),
                IsfTs = x.String == null ? null : SqlFunctions.Postgres.isfinite(x.Datetime),
                IsfInterval = x.String == null ? null : SqlFunctions.Postgres.isfinite(day)
            })
            .First();

        nulls.Ds.Should().BeNull();
        nulls.Ds3.Should().BeNull();
        nulls.Ji.Should().BeNull();
        nulls.Mt.Should().BeNull();
        nulls.Mt3.Should().BeNull();
        nulls.Ct.Should().BeNull();
        nulls.St.Should().BeNull();
        nulls.Tt.Should().BeNull();
        nulls.Tod.Should().BeNull();
        nulls.IsfDate.Should().BeNull();
        nulls.IsfTs.Should().BeNull();
        nulls.IsfInterval.Should().BeNull();
    }

    [Fact]
    [Trait("Issue", "281")]
    public void UuidFamily_ShouldComputeValuesNullsAndClrTypes()
    {
        var v1 = Guid.Parse("a0eebc99-9c0b-11d1-9c9b-00c04fd430c8");
        var v4 = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
        var nil = Guid.Empty;

        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                V1Version = SqlFunctions.Postgres.uuid_extract_version(v1),
                V1Timestamp = SqlFunctions.Postgres.uuid_extract_timestamp(v1),
                V4Version = SqlFunctions.Postgres.uuid_extract_version(v4),
                V4Timestamp = SqlFunctions.Postgres.uuid_extract_timestamp(v4),
                NilVersion = SqlFunctions.Postgres.uuid_extract_version(nil),
                NilTimestamp = SqlFunctions.Postgres.uuid_extract_timestamp(nil)
            })
            .First();

        r.V1Version.Should().Be((short)1);
        r.V1Timestamp.Should().Be(new DateTime(1998, 2, 2, 20, 23, 12, 902).AddTicks(8_760));
        r.V4Version.Should().Be((short)4);
        r.V4Timestamp.Should().BeNull("a version 4 UUID carries no timestamp");
        r.NilVersion.Should().BeNull("the nil UUID has no version");
        r.NilTimestamp.Should().BeNull("the nil UUID has no timestamp");

        // Exact CLR types.
        ((object)r.V1Version!).Should().BeOfType<short>();
        ((object)r.V4Version!).Should().BeOfType<short>();
        ((object)r.V1Timestamp!).Should().BeOfType<DateTime>();
    }

    [Fact]
    [Trait("Issue", "281")]
    public void BinaryStringFamily_ShouldComputeValuesNullsAndClrTypes()
    {
        var r = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new
            {
                Bit = SqlFunctions.Postgres.get_bit(SqlFunctions.Postgres.decode("a1", "hex"), 0L),
                BitSet = SqlFunctions.Postgres.set_bit(SqlFunctions.Postgres.decode("a1", "hex"), 0L, 0),
                Byte = SqlFunctions.Postgres.get_byte(SqlFunctions.Postgres.decode("a1", "hex"), 0),
                ByteSet = SqlFunctions.Postgres.set_byte(SqlFunctions.Postgres.decode("a1", "hex"), 0, 255),
                Count = SqlFunctions.Postgres.bit_count(SqlFunctions.Postgres.decode("a1", "hex"))
            })
            .First();

        // 0xA1 = 1010 0001: bit 0 is 1, the byte is 161, three bits are set.
        r.Bit.Should().Be(1);
        r.BitSet.Should().Equal(new byte[] { 0xA0 });
        r.Byte.Should().Be(161);
        r.ByteSet.Should().Equal(new byte[] { 0xFF });
        r.Count.Should().Be(3L);

        // Exact CLR types.
        ((object)r.Bit!).Should().BeOfType<int>();
        ((object)r.BitSet!).Should().BeOfType<byte[]>();
        ((object)r.Byte!).Should().BeOfType<int>();
        ((object)r.ByteSet!).Should().BeOfType<byte[]>();
        ((object)r.Count!).Should().BeOfType<long>();

        // NULL row: the binary entity's row 2 carries a NULL bytea column.
        var nulls = _sut.BinaryEntity.Where(x => x.Id == 2)
            .Select(x => new
            {
                Bit = SqlFunctions.Postgres.get_bit(x.Data, 0L),
                BitSet = SqlFunctions.Postgres.set_bit(x.Data, 0L, 0),
                Byte = SqlFunctions.Postgres.get_byte(x.Data, 0),
                ByteSet = SqlFunctions.Postgres.set_byte(x.Data, 0, 255),
                Count = SqlFunctions.Postgres.bit_count(x.Data)
            })
            .First();

        nulls.Bit.Should().BeNull();
        nulls.BitSet.Should().BeNull();
        nulls.Byte.Should().BeNull();
        nulls.ByteSet.Should().BeNull();
        nulls.Count.Should().BeNull();
    }

    [Fact]
    [Trait("Issue", "281")]
    public void RefusedMembers_ShouldFailDuringPreparation()
    {
        var casefold = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.casefold(x.String) }).First();
        var gamma = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.gamma(1.5) }).First();
        var tgamma = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.tgamma(1.5) }).First();
        var lgamma = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.lgamma(1.5) }).First();
        var toAsciiOneArg = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String) }).First();
        var crc32 = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.crc32(SqlFunctions.Postgres.decode(x.String, "hex")) }).First();
        var crc32c = () => _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.crc32c(SqlFunctions.Postgres.decode(x.String, "hex")) }).First();

        casefold.Should().Throw<NotSupportedException>().WithMessage("The casefold function is refused:*PostgreSQL 18*");
        gamma.Should().Throw<NotSupportedException>().WithMessage("The gamma function is refused:*17.11*");
        tgamma.Should().Throw<NotSupportedException>().WithMessage("The tgamma function is refused:*17.11*");
        lgamma.Should().Throw<NotSupportedException>().WithMessage("The lgamma function is refused:*17.11*");
        toAsciiOneArg.Should().Throw<NotSupportedException>()
            .WithMessage("The one-argument to_ascii function is refused:*LATIN1, LATIN2, LATIN9 and WIN1250*0A000*to_ascii(value, encoding)*");
        crc32.Should().Throw<NotSupportedException>().WithMessage("The crc32 function is refused:*PostgreSQL 18*");
        crc32c.Should().Throw<NotSupportedException>().WithMessage("The crc32c function is refused:*PostgreSQL 18*");

        // pg_sleep* returns void, so it cannot appear in a projection; the refusal is a member-level
        // throw (no SQL, no wait). pg_sleep_until gets a past target so nothing would ever sleep.
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep(0.0))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_for(TimeSpan.Zero))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_for function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_until(DateTime.UnixEpoch))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_until function is refused:*void*");

        // Positive control: the delivered sibling shapes render and execute.
        _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.erfc(0.0) })
            .First().V.Should().Be(1.0);
        _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String, "LATIN1") })
            .First().V.Should().Be("dadfasd");
    }

    private static string Scalar(DataContext ctx, string sql)
    {
        ctx.EnsureConnectionOpen();
        using var cmd = ctx.CreateCommand(sql);
        return Convert.ToString(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}
