using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D3 (F03 Rounding): live execution of the ClickHouse-only rounding members <c>roundAge</c>,
/// <c>roundBankers</c>, <c>roundDown</c>, <c>roundDuration</c> and <c>roundToExp2</c> against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the vendor bucket tables, the half-to-even tie
/// rule against half-away-from-zero, negative values and negative scales, the precision/scale behaviour
/// (<c>roundBankers(2.675, 2) = 2.68</c> where the CLR <see cref="Math.Round(double, int)"/> returns
/// <c>2.67</c>), the <c>roundToExp2</c> integral and fractional domains, null propagation and the
/// documented server-side refusals (an empty or non-constant boundary array).
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF03RoundingFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f03_entity")]
    internal interface IT286bRoundingEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("age")]
        long Age { get; set; }

        [Column("na")]
        long? NullableAge { get; set; }

        [Column("d")]
        double Value { get; set; }

        [Column("nd")]
        double? NullableValue { get; set; }
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
                     "drop table if exists t286b_f03_entity",
                     "create table t286b_f03_entity (id Int32, age Int64, na Nullable(Int64), d Float64, nd Nullable(Float64), bufs Array(Float64)) engine = Memory",
                     "insert into t286b_f03_entity (id, age, na, d, nd, bufs) values "
                     + "(1, 20, 20, 2.5, 2.5, [1, 5, 10]), (2, 0, null, 2.675, null, [2]), "
                     + "(3, 55, 55, 7.5, 7.5, [10]), (4, -3, null, -3.7, null, [1, 5])"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void RoundAge_ShouldUseTheVendorAgeBuckets()
    {
        using (new AssertionScope())
        {
            // The server's own table: 0 for age < 1, 17 for 1..17, 18 for 18..24, 25 for 25..34,
            // 35 for 35..44, 45 for 45..54 and 55 for 55+.
            Scalar<long>("toInt64(roundAge(toInt64(0)))").Should().Be(0L);
            Scalar<long>("toInt64(roundAge(toInt64(-1)))").Should().Be(0L, "a negative age is 0");
            Scalar<long>("toInt64(roundAge(toInt64(1)))").Should().Be(17L, "the first bucket is 17, not 18");
            Scalar<long>("toInt64(roundAge(toInt64(17)))").Should().Be(17L);
            Scalar<long>("toInt64(roundAge(toInt64(18)))").Should().Be(18L);
            Scalar<long>("toInt64(roundAge(toInt64(24)))").Should().Be(18L);
            Scalar<long>("toInt64(roundAge(toInt64(25)))").Should().Be(25L);
            Scalar<long>("toInt64(roundAge(toInt64(34)))").Should().Be(25L);
            Scalar<long>("toInt64(roundAge(toInt64(35)))").Should().Be(35L);
            Scalar<long>("toInt64(roundAge(toInt64(44)))").Should().Be(35L);
            Scalar<long>("toInt64(roundAge(toInt64(45)))").Should().Be(45L);
            Scalar<long>("toInt64(roundAge(toInt64(54)))").Should().Be(45L);
            Scalar<long>("toInt64(roundAge(toInt64(55)))").Should().Be(55L);
            Scalar<long>("toInt64(roundAge(toInt64(100)))").Should().Be(55L, "the last bucket saturates");

            Scalar<string>("toTypeName(roundAge(toUInt8(20)))").Should().Be("UInt8", "the native result is UInt8");
            Scalar<long>("toInt64(roundAge(toFloat64(17.9)))").Should().Be(17L, "a floating-point age truncates into its bucket");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var ages = ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 1 || x.Id == 2)
            .OrderBy(x => x.Id)
            .Select(x => new { Id = x.Id, Bucket = SqlFunctions.ClickHouse.round_age(x.Age) })
            .ToList();

        ages.Should().HaveCount(2);
        ages[0].Bucket.Should().Be(18L, "roundAge(20) is 18");
        ages[1].Bucket.Should().Be(0L, "roundAge(0) is 0");
    }

    [Fact]
    public void RoundBankers_ShouldRoundHalfToEvenAndNotAwayFromZero()
    {
        using (new AssertionScope())
        {
            // Half-to-even at every tie, including the negative side (which keeps its sign).
            Scalar<double>("roundBankers(0.5)").Should().Be(0d, "0.5 is a tie and goes to the even 0");
            Scalar<double>("roundBankers(1.5)").Should().Be(2d, "1.5 is a tie and goes to the even 2");
            Scalar<double>("roundBankers(2.5)").Should().Be(2d, "2.5 is a tie and goes to the even 2");
            Scalar<double>("roundBankers(3.5)").Should().Be(4d);
            Scalar<double>("roundBankers(4.5)").Should().Be(4d);
            Scalar<double>("roundBankers(-1.5)").Should().Be(-2d);
            Scalar<double>("roundBankers(-2.5)").Should().Be(-2d);
            Scalar<double>("1 / roundBankers(-0.5)").Should().Be(
                double.NegativeInfinity, "roundBankers(-0.5) is negative zero, not positive zero");

            // Half-away-from-zero would give 1, 2, 3, 4, 5 and -2, -3: the tie rule is the whole point.
            // The server's own round() is banker's rounding for Float64 as well, so the two agree there.
            Scalar<double>("round(2.5)").Should().Be(2d);

            // Scale semantics: N > 0 to the right of the point, N < 0 to the left, and the tie rule
            // still decides (2.65 -> 2.6, 2.55 -> 2.6).
            Scalar<double>("roundBankers(2.65, 1)").Should().Be(2.6);
            Scalar<double>("roundBankers(2.55, 1)").Should().Be(2.6);
            Scalar<double>("roundBankers(1234.5, -2)").Should().Be(1200d);
            Scalar<double>("roundBankers(toFloat64(1250), -2)").Should().Be(1200d, "a tie on the hundreds goes to the even 1200");
            Scalar<double>("roundBankers(toFloat64(1350), -2)").Should().Be(1400d);

            // The decimal representation, not the binary one, is rounded: 2.675 is stored slightly
            // below 2.675 but the server still rounds the tie away to 2.68.
            Scalar<double>("roundBankers(2.675, 2)").Should().Be(2.68);
            Scalar<double>("roundBankers(-2.675, 2)").Should().Be(-2.68);

            // Special values propagate; the type follows the argument type.
            double.IsNaN(Scalar<double>("roundBankers(nan)")).Should().BeTrue();
            Scalar<double>("roundBankers(inf)").Should().Be(double.PositiveInfinity);
            Scalar<double>("roundBankers(-inf)").Should().Be(double.NegativeInfinity);
            Scalar<string>("toTypeName(roundBankers(toFloat32(2.5)))").Should().Be("Float32", "the result follows the width");
            Scalar<string>("toTypeName(roundBankers(toInt64(5), -1))").Should().Be("Int64", "an integer argument keeps its type");
            Scalar<long>("roundBankers(toInt64(45), -1)").Should().Be(40L, "the integral half-to-even example");
            Scalar<long>("roundBankers(toInt64(1350), -2)").Should().Be(1400L);
        }

        // This is why the member is not a portable Math.Round representation:
        // - the portable API cannot express the documented integral/negative-scale domain at all
        //   (Math.Round accepts digits 0..15 only);
        // - for Decimal inputs the server's round() — the portable rendering — rounds away from zero
        //   while roundBankers() is half-even.
        FluentActions.Invoking(() => Math.Round(45d, -1)).Should().Throw<ArgumentOutOfRangeException>(
            "the CLR rejects the negative scale that roundBankers(45, -1) = 40 documents");
        Scalar<string>("toString(round(toDecimal64('2.5', 1)))").Should().Be("3", "round(Decimal) rounds away from zero");
        Scalar<string>("toString(roundBankers(toDecimal64('2.5', 1)))").Should().Be("2", "roundBankers(Decimal) is half-even");
        Math.Round(2.5).Should().Be(2d, "the one-argument CLR rule agrees with the server for Float64");
        Math.Round(2.675, 2).Should().Be(2.68, "the CLR also rounds the decimal representation here");
        Scalar<double>("roundBankers(2.675, 2)").Should().Be(2.68);

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Tied = SqlFunctions.ClickHouse.round_bankers(x.Value),
                Scaled = SqlFunctions.ClickHouse.round_bankers(x.Value, 2),
                Integer = SqlFunctions.ClickHouse.round_bankers(x.Age, -1)
            })
            .First();

        row.Tied.Should().Be(2d, "roundBankers(2.5) is 2");
        row.Scaled.Should().Be(2.5d);
        row.Integer.Should().Be(20L, "roundBankers(20, -1) is 20");
    }

    [Fact]
    public void RoundDown_ShouldPickTheGreatestBoundaryNotAboveTheValue()
    {
        using (new AssertionScope())
        {
            Scalar<double>("roundDown(7.5, [1, 2, 5, 10])").Should().Be(5d);
            Scalar<double>("roundDown(10.0, [1, 2, 5, 10])").Should().Be(10d, "an exact boundary is returned unchanged");
            Scalar<double>("roundDown(11.0, [1, 2, 5, 10])").Should().Be(10d, "above the largest boundary it clamps down");
            Scalar<double>("roundDown(0.5, [1, 2, 5, 10])").Should().Be(1d, "below the smallest boundary it clamps up");
            Scalar<double>("roundDown(-3.5, [1, 2, 5, 10])").Should().Be(1d);
            Scalar<double>("roundDown(7.5, [10, 5, 2, 1])").Should().Be(5d, "the boundaries need not be ordered");
            Scalar<double>("roundDown(7.5, [1.5, 2, 5, 10])").Should().Be(5d);
            Scalar<double>("roundDown(-1, [0.5, 1])").Should().Be(0.5);

            // The result type follows the value argument.
            Scalar<string>("toTypeName(roundDown(toInt32(7), [1, 2, 5, 10]))").Should().Be("Int32");
            Scalar<long>("toInt64(roundDown(toInt32(7), [1, 2, 5, 10]))").Should().Be(5L);
            Scalar<string>("toTypeName(roundDown(toFloat32(7.5), [1, 2, 5, 10]))").Should().Be("Float32");
        }

        // The boundary array must be constant and non-empty; both refusals are surfaced, never
        // substituted with a default.
        FluentActions.Invoking(() => Scalar<double>("roundDown(7.5, [])"))
            .Should().Throw<Exception>()
            .WithMessage("*must be numeric type*", "an empty boundary array is refused by the server");
        FluentActions.Invoking(() => Scalar<double>("roundDown(7.5, materialize([1, 2, 5, 10]))"))
            .Should().Throw<Exception>()
            .WithMessage("*must be constant*", "a non-constant boundary array is refused by the server");

        // The CLR path binds the boundaries as one Array(Float64) parameter, which the driver submits
        // as a server-side constant: the "must be constant" requirement is satisfied by the parameter,
        // and the array column (an SQL expression) still reaches the server as a column and is refused.
        Seed();
        using var ctx = Provider.CreateContext();
        var bounds = new[] { 1.0, 5.0, 10.0 };

        var sql = SqlOf(ctx, ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new { V = SqlFunctions.ClickHouse.round_down(x.Value, bounds) }));
        sql.Should().Contain("roundDown(d, @p0)", "the boundaries are one array parameter");

        var rows = ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 1 || x.Id == 3 || x.Id == 4)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Id = x.Id,
                Down = SqlFunctions.ClickHouse.round_down(x.Value, bounds),
                Nullable = SqlFunctions.ClickHouse.round_down(x.NullableValue, 1.0, 5.0)
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Down.Should().Be(1d, "roundDown(2.5, [1, 5, 10]) is 1");
        rows[0].Nullable.Should().Be(1d);
        rows[1].Down.Should().Be(5d, "roundDown(7.5, [1, 5, 10]) is 5");
        rows[1].Nullable.Should().Be(5d);
        rows[2].Down.Should().Be(1d, "roundDown(-3.7, [1, 5, 10]) clamps up to 1");

        FluentActions.Invoking(() => ctx.ExecuteRaw(
                "select roundDown(d, bufs) as v from t286b_f03_entity where id = 1").Read<double>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*must be constant*", "an array column cannot be a boundary set");
    }

    [Fact]
    public void RoundDuration_ShouldUseTheVendorDurationBuckets()
    {
        using (new AssertionScope())
        {
            // The server's own table, one row per boundary and one row just below each of them.
            (long Input, long Expected)[] cases =
            [
                (0, 0), (1, 1), (2, 1), (9, 1), (10, 10), (29, 10), (30, 30), (59, 30), (60, 60),
                (119, 60), (120, 120), (179, 120), (180, 180), (239, 180), (240, 240), (299, 240),
                (300, 300), (599, 300), (600, 600), (1199, 600), (1200, 1200), (1799, 1200),
                (1800, 1800), (3599, 1800), (3600, 3600), (7199, 3600), (7200, 7200), (17999, 7200),
                (18000, 18000), (35999, 18000), (36000, 36000), (36001, 36000)
            ];

            foreach (var (input, expected) in cases)
                Scalar<long>($"toInt64(roundDuration(toInt64({input})))").Should().Be(
                    expected, "roundDuration({0}) is {1}", input, expected);

            Scalar<long>("toInt64(roundDuration(toInt64(-5)))").Should().Be(0L, "a negative duration is 0");
            Scalar<long>("toInt64(roundDuration(toInt64(9223372036854775807)))").Should().Be(36000L);
            Scalar<long>("toInt64(roundDuration(inf))").Should().Be(36000L);
            Scalar<long>("toInt64(roundDuration(nan))").Should().Be(
                36000L, "the pinned server's comparisons send nan to the top bucket");
            Scalar<string>("toTypeName(roundDuration(toUInt16(10)))").Should().Be("UInt16", "the native result is UInt16");
            Scalar<long>("toInt64(roundDuration(toFloat64(1.5)))").Should().Be(1L, "a fractional duration truncates");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var durations = ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 1 || x.Id == 2)
            .OrderBy(x => x.Id)
            .Select(x => new { Id = x.Id, D = SqlFunctions.ClickHouse.round_duration(x.Age) })
            .ToList();

        durations.Should().HaveCount(2);
        durations[0].D.Should().Be(10L, "roundDuration(20) is 10");
        durations[1].D.Should().Be(0L, "roundDuration(0) is 0");
    }

    [Fact]
    public void RoundToExp2_ShouldFloorToTheNearestPowerOfTwoInBothDomains()
    {
        using (new AssertionScope())
        {
            // Integral domain: floor to the largest power of two not above the value.
            (long Input, long Expected)[] integral =
                [(0, 0), (1, 1), (2, 2), (3, 2), (4, 4), (7, 4), (8, 8), (15, 8), (16, 16), (17, 16), (100, 64)];

            foreach (var (input, expected) in integral)
                Scalar<long>($"roundToExp2(toInt64({input}))").Should().Be(expected, "roundToExp2({0})", input);

            Scalar<long>("roundToExp2(toInt64(-1))").Should().Be(0L, "a negative integer is 0, not the negative power");
            Scalar<long>("roundToExp2(toInt64(-5))").Should().Be(0L);
            Scalar<long>("roundToExp2(toInt64(-9223372036854775808))").Should().Be(0L);
            Scalar<long>("roundToExp2(toInt64(9223372036854775807))").Should().Be(
                4611686018427387904L, "an Int64 argument saturates at 2^62");
            Scalar<string>("toTypeName(roundToExp2(toInt64(0)))").Should().Be("Int64", "the result follows the argument type");

            // Fractional domain: the mantissa is truncated, so the result is a possibly fractional power
            // of two and the sign is preserved. Values between zero and one are not collapsed to zero.
            Scalar<double>("roundToExp2(0.7)").Should().Be(0.5);
            Scalar<double>("roundToExp2(0.3)").Should().Be(0.25);
            Scalar<double>("roundToExp2(0.999)").Should().Be(0.5);
            Scalar<double>("roundToExp2(1.0)").Should().Be(1d);
            Scalar<double>("roundToExp2(1.5)").Should().Be(1d);
            Scalar<double>("roundToExp2(2.9)").Should().Be(2d);
            Scalar<double>("roundToExp2(3.7)").Should().Be(2d);
            Scalar<double>("roundToExp2(100.0)").Should().Be(64d);
            Scalar<double>("roundToExp2(-0.7)").Should().Be(-0.5, "the sign is preserved");
            Scalar<double>("roundToExp2(-2.5)").Should().Be(-2d);
            Scalar<double>("roundToExp2(-3.7)").Should().Be(-2d);
            Scalar<double>("roundToExp2(inf)").Should().Be(double.PositiveInfinity);
            Scalar<double>("roundToExp2(-inf)").Should().Be(double.NegativeInfinity);
            Scalar<double>("roundToExp2(nan)").Should().Be(
                double.PositiveInfinity, "the pinned server maps nan onto +inf; the arm does not correct it client-side");
            Scalar<string>("toTypeName(roundToExp2(toFloat32(2.9)))").Should().Be("Float32");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                FromInteger = SqlFunctions.ClickHouse.round_to_exp2(x.Age),
                FromDouble = SqlFunctions.ClickHouse.round_to_exp2(x.Value)
            })
            .First();

        row.FromInteger.Should().Be(32L, "roundToExp2(55) is 32");
        row.FromDouble.Should().Be(4d, "roundToExp2(7.5) is 4");
        row.FromInteger.GetType().Should().Be(typeof(long));
        row.FromDouble.GetType().Should().Be(typeof(double));
    }

    [Fact]
    public void NullOperands_ShouldProduceNullNeverADefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var bounds = new[] { 1.0, 5.0, 10.0 };

        var row = ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Age = SqlFunctions.ClickHouse.round_age(x.NullableAge),
                Duration = SqlFunctions.ClickHouse.round_duration(x.NullableAge),
                Bankers = SqlFunctions.ClickHouse.round_bankers(x.NullableValue),
                BankersScaled = SqlFunctions.ClickHouse.round_bankers(x.NullableValue, 2),
                BankersInteger = SqlFunctions.ClickHouse.round_bankers(x.NullableAge, -1),
                Down = SqlFunctions.ClickHouse.round_down(x.NullableValue, bounds),
                Exp2Long = SqlFunctions.ClickHouse.round_to_exp2(x.NullableAge),
                Exp2Double = SqlFunctions.ClickHouse.round_to_exp2(x.NullableValue)
            })
            .First();

        using (new AssertionScope())
        {
            row.Age.Should().BeNull();
            row.Duration.Should().BeNull();
            row.Bankers.Should().BeNull();
            row.BankersScaled.Should().BeNull();
            row.BankersInteger.Should().BeNull();
            row.Down.Should().BeNull();
            row.Exp2Long.Should().BeNull();
            row.Exp2Double.Should().BeNull();
        }

        // The server reports Nullable result types for a nullable argument; nothing collapses to 0.
        using (new AssertionScope())
        {
            Scalar<string>("toTypeName(roundAge(CAST(NULL AS Nullable(Int64))))").Should().Be("Nullable(UInt8)");
            Scalar<string>("toTypeName(roundDuration(CAST(NULL AS Nullable(Int64))))").Should().Be("Nullable(UInt16)");
            Scalar<string>("toTypeName(roundBankers(CAST(NULL AS Nullable(Float64))))").Should().Be("Nullable(Float64)");
            Scalar<string>("toTypeName(roundDown(CAST(NULL AS Nullable(Float64)), [1, 5]))").Should().Be("Nullable(Float64)");
            Scalar<string>("toTypeName(roundToExp2(CAST(NULL AS Nullable(Int64))))").Should().Be("Nullable(Int64)");
        }
    }

    [Fact]
    public void Projection_ShouldMaterializeTheDeclaredClrShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var bounds = new[] { 1.0, 5.0, 10.0 };

        var row = ctx.From<IT286bRoundingEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Age = SqlFunctions.ClickHouse.round_age(x.Age),
                Bankers = SqlFunctions.ClickHouse.round_bankers(x.Value),
                Down = SqlFunctions.ClickHouse.round_down(x.Value, bounds),
                Duration = SqlFunctions.ClickHouse.round_duration(x.Age),
                Exp2 = SqlFunctions.ClickHouse.round_to_exp2(x.Age)
            })
            .First();

        using (new AssertionScope())
        {
            row.Age.GetType().Should().Be(typeof(long), "roundAge is lowered to Int64");
            row.Bankers.GetType().Should().Be(typeof(double));
            row.Down.GetType().Should().Be(typeof(double));
            row.Duration.GetType().Should().Be(typeof(long), "roundDuration is lowered to Int64");
            row.Exp2.GetType().Should().Be(typeof(long));

            row.Age.Should().Be(18L, "roundAge(20) is 18");
            row.Bankers.Should().Be(2d, "roundBankers(2.5) is 2");
            row.Down.Should().Be(1d, "roundDown(2.5, [1, 5, 10]) is 1");
            row.Duration.Should().Be(10L, "roundDuration(20) is 10");
            row.Exp2.Should().Be(16L, "roundToExp2(20) is 16");
        }
    }
}
