using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D2 (F02 Numeric/math): live execution of the ClickHouse-only numeric members
/// <c>erf</c>, <c>erfc</c>, <c>lgamma</c>, <c>tgamma</c>, <c>gcd</c>, <c>lcm</c>,
/// <c>isFinite</c>/<c>isInfinite</c>/<c>isNaN</c>, <c>hypot</c>, <c>intDiv</c>,
/// <c>intExp2</c>/<c>intExp10</c>, <c>sigmoid</c> and <c>sqr</c> against the pinned <c>25.8-alpine</c>
/// server, plus the executable evidence for the two non-member catalog entries: the portable operator
/// forms (<c>plus</c>/<c>minus</c>/<c>multiply</c>/<c>divide</c>/<c>negate</c>) and the constant
/// <c>e</c>. Special values (<c>nan</c>, <c>±inf</c>, zero, negative domains, 64-bit overflow), the
/// documented server-side refusals (zero operands of <c>gcd</c>/<c>lcm</c>, zero divisors of
/// <c>intDiv</c>) and NULL propagation are asserted rather than substituted.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF02NumericFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f02_entity")]
    internal interface IT286bNumericEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("i")]
        long Integer { get; set; }

        [Column("d")]
        double Double { get; set; }

        [Column("ni")]
        long? NullableInteger { get; set; }

        [Column("nd")]
        double? NullableDouble { get; set; }
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

    /// <summary>Evaluates an expression over the seeded table (needed for column-scoped type probes).</summary>
    private T ScalarFrom<T>(string expression)
    {
        using var context = Provider.CreateContext();
        using var result = context.ExecuteRaw(
            $"select {expression} as v from t286b_f02_entity where id = 1");
        return result.Read<T>().Single();
    }

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f02_entity",
                     "create table t286b_f02_entity (id Int32, i Int64, d Float64, ni Nullable(Int64), nd Nullable(Float64)) engine = Memory",
                     "insert into t286b_f02_entity (id, i, d, ni, nd) values "
                     + "(1, 12, 3, 12, 3), (2, -7, 2.5, null, null), "
                     + "(3, 9223372036854775807, 0, 0, 0), (4, -9223372036854775808, nan, null, null)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void SpecialFunctions_ShouldExecuteWithTheirDocumentedSpecialValues()
    {
        using (new AssertionScope())
        {
            // erf is odd, tends to ±1 and stays nan for nan.
            Scalar<double>("erf(0)").Should().Be(0d);
            Scalar<double>("erf(1)").Should().BeApproximately(0.8427007929497149, 1e-12);
            Scalar<double>("erf(-1)").Should().BeApproximately(-0.8427007929497149, 1e-12);
            Scalar<double>("erf(inf)").Should().Be(1d, "erf(+inf) is 1");
            Scalar<double>("erf(-inf)").Should().Be(-1d, "erf(-inf) is -1");
            double.IsNaN(Scalar<double>("erf(nan)")).Should().BeTrue("erf(nan) is nan");

            // erfc keeps the tail: erfc(inf) is 0 and erfc(-inf) is 2.
            Scalar<double>("erfc(0)").Should().Be(1d);
            Scalar<double>("erfc(1)").Should().BeApproximately(0.15729920705028513, 1e-12);
            Scalar<double>("erfc(inf)").Should().Be(0d);
            Scalar<double>("erfc(-inf)").Should().Be(2d);
            double.IsNaN(Scalar<double>("erfc(nan)")).Should().BeTrue();

            // lgamma: log|Γ|, inf at the pole 0 and nan for the other non-positive integers.
            Scalar<double>("lgamma(5)").Should().BeApproximately(3.1780538303479458, 1e-12);
            Scalar<double>("lgamma(1)").Should().Be(0d);
            Scalar<double>("lgamma(0.5)").Should().BeApproximately(0.5723649429247001, 1e-12);
            double.IsPositiveInfinity(Scalar<double>("lgamma(0)")).Should().BeTrue("lgamma(0) is the pole inf");
            double.IsNaN(Scalar<double>("lgamma(-1)")).Should().BeTrue("lgamma(-1) is nan, not an exception");

            // tgamma: Γ, inf at 0 and nan for negative integers.
            Scalar<double>("tgamma(5)").Should().Be(24d);
            Scalar<double>("tgamma(0.5)").Should().BeApproximately(1.772453850905516, 1e-12);
            Scalar<double>("tgamma(-0.5)").Should().BeApproximately(-3.5449077018110318, 1e-12);
            double.IsPositiveInfinity(Scalar<double>("tgamma(0)")).Should().BeTrue("tgamma(0) is the pole inf");
            double.IsNaN(Scalar<double>("tgamma(-1)")).Should().BeTrue("tgamma(-1) is nan");

            // hypot: overflow-free sqrt(a²+b²); an infinite operand wins over a nan operand.
            Scalar<double>("hypot(3, 4)").Should().Be(5d);
            Scalar<double>("hypot(-3, 4)").Should().Be(5d);
            Scalar<double>("hypot(5, 12)").Should().Be(13d);
            Scalar<double>("hypot(inf, 5)").Should().Be(double.PositiveInfinity);
            double.IsNaN(Scalar<double>("hypot(nan, 5)")).Should().BeTrue("hypot(nan, 5) is nan");
            Scalar<double>("hypot(inf, nan)").Should().Be(double.PositiveInfinity, "an infinite operand wins");

            // sigmoid: the server implementation is single-precision, so it tracks 1/(1+exp(-x)) to
            // about 1e-6 rather than to the double-precision formula; sigmoid(nan) is pinned to 0.
            Scalar<double>("sigmoid(0)").Should().Be(0.5d);
            Scalar<double>("sigmoid(1)").Should().BeApproximately(1d / (1d + Math.Exp(-1d)), 1e-6,
                "the server evaluates sigmoid in single precision");
            Scalar<double>("sigmoid(-1)").Should().BeApproximately(1d / (1d + Math.Exp(1d)), 1e-6);
            Scalar<double>("sigmoid(3)").Should().BeApproximately(1d / (1d + Math.Exp(-3d)), 1e-6);
            Scalar<double>("sigmoid(inf)").Should().Be(1d);
            Scalar<double>("sigmoid(-inf)").Should().Be(0d);
            Scalar<double>("sigmoid(nan)").Should().Be(0d, "the server defines sigmoid(nan) as 0");
        }
    }

    [Fact]
    public void IntegerFunctions_ShouldExecuteWithTheDocumentedZeroAndOverflowRules()
    {
        using (new AssertionScope())
        {
            // gcd/lcm are sign-independent.
            Scalar<long>("gcd(12, 18)").Should().Be(6L);
            Scalar<long>("gcd(-12, 18)").Should().Be(6L);
            Scalar<long>("gcd(-12, -18)").Should().Be(6L);
            Scalar<long>("lcm(4, 6)").Should().Be(12L);
            Scalar<long>("lcm(-4, 6)").Should().Be(12L);
            Scalar<long>("lcm(4, -6)").Should().Be(12L);

            // intDiv truncates toward zero; the result is Int64 for Int64 operands.
            Scalar<long>("intDiv(7, 2)").Should().Be(3L);
            Scalar<long>("intDiv(-7, 2)").Should().Be(-3L, "intDiv truncates toward zero");
            Scalar<long>("intDiv(7, -2)").Should().Be(-3L);
            Scalar<long>("intDiv(6, 3)").Should().Be(2L);
            Scalar<string>("toTypeName(intDiv(toInt64(7), toInt64(2)))").Should().Be("Int64");

            // intExp2/intExp10 are UInt64; a negative exponent is 0 and the range saturates.
            Scalar<string>("toString(intExp2(10))").Should().Be("1024");
            Scalar<string>("toString(intExp2(0))").Should().Be("1");
            Scalar<string>("toString(intExp2(62))").Should().Be("4611686018427387904");
            Scalar<string>("toString(intExp2(63))").Should().Be("9223372036854775808");
            Scalar<string>("toString(intExp2(64))").Should().Be("18446744073709551615", "the range saturates at UInt64");
            Scalar<string>("toString(intExp2(-1))").Should().Be("0");
            Scalar<string>("toString(intExp10(3))").Should().Be("1000");
            Scalar<string>("toString(intExp10(19))").Should().Be("10000000000000000000");
            Scalar<string>("toString(intExp10(20))").Should().Be("18446744073709551615");
            Scalar<string>("toString(intExp10(-1))").Should().Be("0");
            Scalar<string>("toTypeName(intExp2(toInt64(10)))").Should().Be("UInt64");
        }

        // A zero operand is not an identity for gcd/lcm: the server raises a division-by-zero error and
        // that error is surfaced instead of a substituted default.
        foreach (var expression in new[] { "gcd(0, 5)", "gcd(5, 0)", "lcm(0, 5)", "lcm(5, 0)" })
        {
            var sql = expression;
            FluentActions.Invoking(() => Scalar<long>(sql))
                .Should().Throw<Exception>()
                .WithMessage("*Division by zero*", "'{0}' is a server-side error, never 0", expression);
        }

        // intDiv by zero is the integer error (ILLEGAL_DIVISION), unlike floating-point divide.
        FluentActions.Invoking(() => Scalar<long>("intDiv(1, 0)"))
            .Should().Throw<Exception>()
            .WithMessage("*Division by zero*");

        // gcd of Int64.MinValue overflows the intermediate signed result.
        FluentActions.Invoking(() => Scalar<long>("gcd(toInt64(-9223372036854775808), toInt64(1))"))
            .Should().Throw<Exception>()
            .WithMessage("*overflow*", "the Int64 minimum has no representable absolute value");
    }

    [Fact]
    public void Predicates_ShouldDistinguishFiniteInfiniteAndNaN()
    {
        using (new AssertionScope())
        {
            Scalar<bool>("isFinite(1.0)").Should().BeTrue();
            Scalar<bool>("isFinite(inf)").Should().BeFalse();
            Scalar<bool>("isFinite(-inf)").Should().BeFalse();
            Scalar<bool>("isFinite(nan)").Should().BeFalse();

            Scalar<bool>("isInfinite(inf)").Should().BeTrue();
            Scalar<bool>("isInfinite(-inf)").Should().BeTrue();
            Scalar<bool>("isInfinite(1.0)").Should().BeFalse();
            Scalar<bool>("isInfinite(nan)").Should().BeFalse("nan is not infinite");

            Scalar<bool>("isNaN(nan)").Should().BeTrue();
            Scalar<bool>("isNaN(inf)").Should().BeFalse("inf is not nan");
            Scalar<bool>("isNaN(1.0)").Should().BeFalse();

            // The result type is UInt8 (surfaced as bool) and a NULL argument yields NULL.
            Scalar<string>("toTypeName(isNaN(1.0))").Should().Be("UInt8");
            Scalar<string>("toTypeName(isFinite(CAST(NULL AS Nullable(Float64))))").Should().Be("Nullable(UInt8)");
            Scalar<bool?>("isFinite(CAST(NULL AS Nullable(Float64)))").Should().BeNull();
            Scalar<bool?>("isInfinite(CAST(NULL AS Nullable(Float64)))").Should().BeNull();
            Scalar<bool?>("isNaN(CAST(NULL AS Nullable(Float64)))").Should().BeNull();
        }

        // The mapped nullable column keeps the NULL instead of collapsing to false.
        Seed();
        using var ctx = Provider.CreateContext();
        var rows = ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 1 || x.Id == 2)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Id = x.Id,
                Fin = SqlFunctions.ClickHouse.is_finite(x.NullableDouble),
                Inf = SqlFunctions.ClickHouse.is_infinite(x.NullableDouble),
                Nan = SqlFunctions.ClickHouse.is_nan(x.NullableDouble)
            })
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Fin.Should().BeTrue();
        rows[0].Inf.Should().BeFalse();
        rows[0].Nan.Should().BeFalse();
        rows[1].Fin.Should().BeNull("isFinite(NULL) is NULL, never false");
        rows[1].Inf.Should().BeNull();
        rows[1].Nan.Should().BeNull();
    }

    [Fact]
    public void NullOperands_ShouldProduceNullNeverADefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Erf = SqlFunctions.ClickHouse.erf(x.NullableDouble),
                Erfc = SqlFunctions.ClickHouse.erfc(x.NullableDouble),
                Lg = SqlFunctions.ClickHouse.lgamma(x.NullableDouble),
                Tg = SqlFunctions.ClickHouse.tgamma(x.NullableDouble),
                Hyp = SqlFunctions.ClickHouse.hypot(x.NullableDouble, 4.0),
                Idv = SqlFunctions.ClickHouse.int_div(x.NullableInteger, 2L),
                E2 = SqlFunctions.ClickHouse.int_exp2(x.NullableInteger),
                E10 = SqlFunctions.ClickHouse.int_exp10(x.NullableInteger),
                Sig = SqlFunctions.ClickHouse.sigmoid(x.NullableDouble),
                Sqr = SqlFunctions.ClickHouse.sqr(x.NullableDouble),
                // A null in a *required* second position is a real NULL operand, so the call keeps both
                // arguments and the result is NULL rather than an arity error.
                HypNullSecond = SqlFunctions.ClickHouse.hypot(x.Double, null)
            })
            .First();

        using (new AssertionScope())
        {
            row.Erf.Should().BeNull();
            row.Erfc.Should().BeNull();
            row.Lg.Should().BeNull();
            row.Tg.Should().BeNull();
            row.Hyp.Should().BeNull();
            row.Idv.Should().BeNull();
            row.E2.Should().BeNull();
            row.E10.Should().BeNull();
            row.Sig.Should().BeNull();
            row.Sqr.Should().BeNull();
            row.HypNullSecond.Should().BeNull();
        }
    }

    [Fact]
    public void NullableGcdAndLcm_ShouldBeRefusedByTheServerInsteadOfProducingNull()
    {
        // gcd/lcm have no nullable shape: the server adapts them to Nullable(Int64) by evaluating the
        // function over the underlying column, where a NULL is its zero default, and a zero operand is a
        // division by zero. The refusal is asserted here instead of being papered over with a coalesce.
        Seed();
        using var ctx = Provider.CreateContext();

        foreach (var name in new[] { "gcd", "lcm" })
        {
            var expression = $"{name}(ni, 2)";
            FluentActions.Invoking(() => ctx.ExecuteRaw(
                    $"select {expression} as v from t286b_f02_entity where id = 2").Read<long>().ToList())
                .Should().Throw<Exception>()
                .WithMessage("*Division by zero*",
                    "'{0}' over a NULL element reaches the zero operand and must not be substituted", name);
        }

        // The same call over a NULL-free selection still evaluates, so the refusal is about the NULL
        // element, not about the member.
        ScalarFrom<long>("gcd(i, 2)").Should().Be(2L);
    }

    [Fact]
    public void Operators_ShouldExecuteAsTheIdenticalClickHouseFunctions()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Add = x.Integer + 2L,
                Sub = x.Integer - 2L,
                Mul = x.Integer * 3L,
                Div = x.Double / 2.5,
                Neg = -x.Integer,
                Overflows = x.Integer + 2L > 0
            })
            .First();

        using (new AssertionScope())
        {
            row.Add.Should().Be(14L);
            row.Sub.Should().Be(10L);
            row.Mul.Should().Be(36L);
            row.Div.Should().BeApproximately(1.2, 1e-12);
            row.Neg.Should().Be(-12L);
            row.Overflows.Should().BeTrue("the predicate stays server-side");
        }

        // The CLR operator and the native function are the same ClickHouse function: same value and
        // same result type on the mapped column.
        using (new AssertionScope())
        {
            Scalar<long>("plus(12, 2)").Should().Be(14L);
            Scalar<long>("minus(12, 2)").Should().Be(10L);
            Scalar<long>("multiply(12, 3)").Should().Be(36L);
            Scalar<double>("divide(toFloat64(3), toFloat64(2.5))").Should().BeApproximately(1.2, 1e-12);
            Scalar<long>("negate(12)").Should().Be(-12L);

            ScalarFrom<string>("toTypeName(i + 2)").Should().Be(
                ScalarFrom<string>("toTypeName(plus(i, 2))"), "'+' and plus() have the same result type");
            ScalarFrom<string>("toTypeName(i - 2)").Should().Be(
                ScalarFrom<string>("toTypeName(minus(i, 2))"), "'-' and minus() have the same result type");
            ScalarFrom<string>("toTypeName(i * 3)").Should().Be(
                ScalarFrom<string>("toTypeName(multiply(i, 3))"), "'*' and multiply() have the same result type");
            ScalarFrom<string>("toTypeName(d / 2.5)").Should().Be(
                ScalarFrom<string>("toTypeName(divide(d, 2.5))"), "'/' and divide() have the same result type");
            ScalarFrom<string>("toTypeName(-i)").Should().Be(
                ScalarFrom<string>("toTypeName(negate(i))"), "unary '-' and negate() have the same result type");
        }

        // 64-bit wrap, floating-point division by zero and the Int64 minimum are identical in both forms.
        using (new AssertionScope())
        {
            Scalar<long>("(toInt64(9223372036854775807) + 1)").Should().Be(long.MinValue);
            Scalar<long>("plus(toInt64(9223372036854775807), toInt64(1))").Should().Be(long.MinValue);
            Scalar<long>("-(toInt64(-9223372036854775808))").Should().Be(long.MinValue);
            Scalar<long>("negate(toInt64(-9223372036854775808))").Should().Be(long.MinValue);
            Scalar<double>("(toFloat64(1) / 0)").Should().Be(double.PositiveInfinity);
            Scalar<double>("divide(toFloat64(1), toFloat64(0))").Should().Be(double.PositiveInfinity);
        }

        // The mapped column wraps exactly like the native function, so the operator form is not a
        // different CLR arithmetic path.
        var maxRow = ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new { Wrapped = x.Integer + 1L })
            .First();
        maxRow.Wrapped.Should().Be(long.MinValue, "Int64.MaxValue + 1 wraps in ClickHouse arithmetic");

        // `divide` is not `intDiv`: the operator is floating-point division, the integer quotient is the
        // separate intDiv member.
        Scalar<double>("(toInt64(7) / toInt64(2))").Should().Be(3.5);
        Scalar<double>("divide(toInt64(7), toInt64(2))").Should().Be(3.5);
        Scalar<long>("intDiv(7, 2)").Should().Be(3L);
    }

    [Fact]
    public void ConstantE_ShouldEqualTheServerConstantExactly()
    {
        var serverE = Scalar<double>("e()");

        serverE.Should().Be(Math.E, "e() returns the same double as the portable Math.E constant");
        BitConverter.DoubleToInt64Bits(serverE).Should().Be(
            BitConverter.DoubleToInt64Bits(Math.E), "the two constants are bit-identical");

        Seed();
        using var ctx = Provider.CreateContext();
        var projected = ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { E = Math.E })
            .First();

        projected.E.Should().Be(Math.E, "the portable constant survives the projection unchanged");
        projected.E.Should().Be(serverE, "the literal equals the server function value");
    }

    [Fact]
    public void Sqr_ShouldExecuteTheVendorProductAndKeepTheVersionGapExplicit()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var sql = SqlOf(ctx, ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { S = SqlFunctions.ClickHouse.sqr(x.Double) }));
        sql.Should().Contain("(d * d)", "sqr is rendered as the vendor-defined product on server versions without the name");

        var row = ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { S = SqlFunctions.ClickHouse.sqr(x.Double), Sn = SqlFunctions.ClickHouse.sqr(x.NullableDouble) })
            .First();
        row.S.Should().Be(9d, "sqr(3) is 9");
        row.Sn.Should().Be(9d);

        Scalar<double>("(toFloat64(-3) * toFloat64(-3))").Should().Be(9d);
        double.IsNaN(Scalar<double>("(nan * nan)")).Should().BeTrue("the product of nan is nan");
        Scalar<double>("(inf * inf)").Should().Be(double.PositiveInfinity);

        // The pinned server predates the sqr spelling (introduced in ClickHouse 26.7), which is exactly
        // why the arm renders the product; the gap is asserted instead of being hidden.
        using var context = Provider.CreateContext();
        FluentActions.Invoking(() => context.ExecuteRaw("select sqr(3) as v").Read<double>().ToList())
            .Should().Throw<Exception>()
            .WithMessage("*UNKNOWN_FUNCTION*", "the 25.8 server has no sqr function");
    }

    [Fact]
    public void Projection_ShouldMaterializeTheDeclaredClrShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bNumericEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Erf = SqlFunctions.ClickHouse.erf(x.Double),
                Gcd = SqlFunctions.ClickHouse.gcd(x.Integer, 18L),
                Fin = SqlFunctions.ClickHouse.is_finite(x.Double),
                Nan = SqlFunctions.ClickHouse.is_nan(x.Double),
                E2 = SqlFunctions.ClickHouse.int_exp2(x.Integer),
                E10 = SqlFunctions.ClickHouse.int_exp10(x.Integer),
                Sig = SqlFunctions.ClickHouse.sigmoid(x.Double),
                Sqr = SqlFunctions.ClickHouse.sqr(x.Double)
            })
            .First();

        using (new AssertionScope())
        {
            row.Erf.GetType().Should().Be(typeof(double));
            row.Gcd.GetType().Should().Be(typeof(long));
            row.Fin.GetType().Should().Be(typeof(bool));
            row.Nan.GetType().Should().Be(typeof(bool));
            row.E2.GetType().Should().Be(typeof(ulong), "intExp2 returns UInt64");
            row.E10.GetType().Should().Be(typeof(ulong), "intExp10 returns UInt64");
            row.Sig.GetType().Should().Be(typeof(double));

            row.Erf.Should().BeApproximately(0.9999779095030014, 1e-12, "erf(3) is evaluated server-side");
            row.Gcd.Should().Be(6L, "gcd(12, 18) is 6");
            row.Fin.Should().BeTrue();
            row.Nan.Should().BeFalse();
            row.E2.Should().Be(4096UL, "intExp2(12) is 4096");
            row.E10.Should().Be(1_000_000_000_000UL, "intExp10(12) is 1e12");
            row.Sig.Should().BeApproximately(0.9525742685384149, 1e-6, "sigmoid(3) is single-precision on the server");
            row.Sqr.Should().Be(9d);
        }
    }
}
