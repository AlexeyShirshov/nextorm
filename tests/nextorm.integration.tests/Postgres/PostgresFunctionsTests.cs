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

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
