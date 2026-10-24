using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D2 (F02 Numeric/math): the ClickHouse-only numeric members <c>erf</c>, <c>erfc</c>,
/// <c>lgamma</c>, <c>tgamma</c>, <c>gcd</c>, <c>lcm</c>, <c>isFinite</c>/<c>isInfinite</c>/<c>isNaN</c>,
/// <c>hypot</c>, <c>intDiv</c>, <c>intExp2</c>/<c>intExp10</c>, <c>sigmoid</c> and <c>sqr</c>, plus the
/// two non-member catalog entries: the numeric operator forms (<c>plus</c>/<c>minus</c>/<c>multiply</c>/
/// <c>divide</c>/<c>negate</c>), which the portable CLR operators represent, and the constant <c>e</c>,
/// which the portable <see cref="Math.E"/> literal represents. The tests pin the exact
/// <see cref="IScalarFunctions"/> arms, the declared CLR shapes, the real NULL operands of required
/// parameters, the operator rendering (the chosen representation of the catalog's "operator forms"),
/// the constant literal, and the real refusal of the other dialects. They never open a connection.
/// </summary>
public class T286bF02NumericFunctionSqlGenerationTests
{
    private static readonly IScalarFunctions ClickHouseScalars = ClickHouseDialect.Instance.ScalarFunctions!;

    /// <summary>One row per render arm of the delivered members.</summary>
    private static readonly (string Name, string[] Args, string Expected)[] Arms =
    [
        ("erf", ["x"], "erf(x)"),
        ("erfc", ["x"], "erfc(x)"),
        ("lgamma", ["x"], "lgamma(x)"),
        ("tgamma", ["x"], "tgamma(x)"),
        ("gcd", ["a", "b"], "gcd(a, b)"),
        ("lcm", ["a", "b"], "lcm(a, b)"),
        ("is_finite", ["x"], "isFinite(x)"),
        ("is_infinite", ["x"], "isInfinite(x)"),
        ("is_nan", ["x"], "isNaN(x)"),
        ("hypot", ["a", "b"], "hypot(a, b)"),
        ("int_div", ["a", "b"], "intDiv(a, b)"),
        ("int_exp2", ["x"], "intExp2(x)"),
        ("int_exp10", ["x"], "intExp10(x)"),
        ("sigmoid", ["x"], "sigmoid(x)"),
        ("sqr", ["x"], "(x * x)")
    ];

    private static readonly string[] MemberNames = Arms.Select(arm => arm.Name).ToArray();

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

        [Column("s")]
        string? String { get; set; }
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
    public void Supports_ShouldAcceptTheCompleteNumericSliceAndRejectCaseAndNativeSpellings()
    {
        MemberNames.Should().HaveCount(15, "the F02 slice delivers 15 ClickHouse-only numeric names");

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
                ClickHouseScalars.Supports(name).Should().BeTrue("'{0}' is part of the ClickHouse inventory", name);

            ClickHouseScalars.Supports("ERF").Should().BeFalse("names are case-sensitive CLR member names");
            ClickHouseScalars.Supports("isFinite").Should().BeFalse("the table is keyed by the CLR member name");
            ClickHouseScalars.Supports("isInfinite").Should().BeFalse("the table is keyed by the CLR member name");
            ClickHouseScalars.Supports("isNaN").Should().BeFalse("the table is keyed by the CLR member name");
            ClickHouseScalars.Supports("intDiv").Should().BeFalse("the native spelling is not the table key");
            ClickHouseScalars.Supports("intExp2").Should().BeFalse("the native spelling is not the table key");
        }
    }

    [Fact]
    public void Render_ShouldThrowForTheNativeSpellingInsteadOfTheMemberName()
    {
        FluentActions.Invoking(() => ClickHouseScalars.Render("isFinite", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The isFinite function is not supported by ClickHouse.");

        FluentActions.Invoking(() => ClickHouseScalars.Render("intDiv", ["a", "b"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The intDiv function is not supported by ClickHouse.");
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            Shape("erf", 1).Should().Be(typeof(double), "erf returns Float64");
            Shape("erf", 1, nullable: true).Should().Be(typeof(double?), "a nullable argument stays Nullable(Float64)");
            Shape("erfc", 1).Should().Be(typeof(double), "erfc returns Float64");
            Shape("lgamma", 1).Should().Be(typeof(double), "lgamma returns Float64");
            Shape("tgamma", 1).Should().Be(typeof(double), "tgamma returns Float64");
            Shape("gcd", 2).Should().Be(typeof(long), "gcd(Int64, Int64) returns Int64");
            Shape("lcm", 2).Should().Be(typeof(long), "lcm(Int64, Int64) returns Int64");
            Shape("is_finite", 1).Should().Be(typeof(bool), "isFinite returns UInt8 surfaced as bool");
            Shape("is_finite", 1, nullable: true).Should().Be(typeof(bool?), "isFinite(NULL) is Nullable(UInt8)");
            Shape("is_infinite", 1).Should().Be(typeof(bool), "isInfinite returns UInt8 surfaced as bool");
            Shape("is_infinite", 1, nullable: true).Should().Be(typeof(bool?), "isInfinite(NULL) is Nullable(UInt8)");
            Shape("is_nan", 1).Should().Be(typeof(bool), "isNaN returns UInt8 surfaced as bool");
            Shape("is_nan", 1, nullable: true).Should().Be(typeof(bool?), "isNaN(NULL) is Nullable(UInt8)");
            Shape("hypot", 2).Should().Be(typeof(double), "hypot returns Float64");
            Shape("hypot", 2, nullable: true).Should().Be(typeof(double?), "nullable operands stay Nullable(Float64)");
            Shape("int_div", 2).Should().Be(typeof(long), "intDiv over Int64 operands returns Int64");
            Shape("int_div", 2, nullable: true).Should().Be(typeof(long?), "nullable operands stay Nullable(Int64)");
            Shape("int_exp2", 1).Should().Be(typeof(ulong), "intExp2 returns UInt64");
            Shape("int_exp2", 1, nullable: true).Should().Be(typeof(ulong?), "a nullable argument is Nullable(UInt64)");
            Shape("int_exp10", 1).Should().Be(typeof(ulong), "intExp10 returns UInt64");
            Shape("int_exp10", 1, nullable: true).Should().Be(typeof(ulong?), "a nullable argument is Nullable(UInt64)");
            Shape("sigmoid", 1).Should().Be(typeof(double), "sigmoid returns Float64");
            Shape("sigmoid", 1, nullable: true).Should().Be(typeof(double?), "a nullable argument stays Nullable(Float64)");
            Shape("sqr", 1).Should().Be(typeof(double), "the product of two Float64 operands is Float64");
            Shape("sqr", 1, nullable: true).Should().Be(typeof(double?), "nullable operands stay Nullable(Float64)");
        }

        static Type Shape(string member, int arity, bool nullable = false) => typeof(ClickHouseFunctions)
            .GetMethods()
            .Single(method => method.Name == member
                && method.GetParameters().Length == arity
                && (method.GetParameters()[0].ParameterType == typeof(double?)
                    || method.GetParameters()[0].ParameterType == typeof(long?)) == nullable)
            .ReturnType;
    }

    [Fact]
    public void GcdAndLcm_ShouldOfferNoNullableOverload()
    {
        // The server adapts gcd/lcm to Nullable(Int64) by evaluating the function over the underlying
        // column, where a NULL is its zero default; a zero operand is an ILLEGAL_DIVISION error, so a
        // nullable overload could not honour a NULL-for-NULL contract. The refusal is a shape contract:
        // exactly one (long, long) signature per name, and no nullable shape to call.
        using (new AssertionScope())
        {
            foreach (var name in new[] { "gcd", "lcm" })
            {
                var parameters = typeof(ClickHouseFunctions)
                    .GetMethods()
                    .Where(method => method.Name == name)
                    .Select(method => method.GetParameters().Select(parameter => parameter.ParameterType).ToArray())
                    .ToArray();

                parameters.Should().ContainSingle("'{0}' has exactly one non-nullable shape", name);
                parameters[0].Should().Equal(new[] { typeof(long), typeof(long) },
                    "the only accepted shape is (long, long)");
            }
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryMemberOverNumericColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bNumericEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Erf = SqlFunctions.ClickHouse.erf(x.Double),
                Erfc = SqlFunctions.ClickHouse.erfc(x.Double),
                Lg = SqlFunctions.ClickHouse.lgamma(x.Double),
                Tg = SqlFunctions.ClickHouse.tgamma(x.Double),
                Gcd = SqlFunctions.ClickHouse.gcd(x.Integer, 12L),
                Lcm = SqlFunctions.ClickHouse.lcm(x.Integer, 6L),
                Fin = SqlFunctions.ClickHouse.is_finite(x.Double),
                Inf = SqlFunctions.ClickHouse.is_infinite(x.Double),
                Nan = SqlFunctions.ClickHouse.is_nan(x.Double),
                Hyp = SqlFunctions.ClickHouse.hypot(x.Double, 4.0),
                Idv = SqlFunctions.ClickHouse.int_div(x.Integer, 2L),
                E2 = SqlFunctions.ClickHouse.int_exp2(x.Integer),
                E10 = SqlFunctions.ClickHouse.int_exp10(x.Integer),
                Sig = SqlFunctions.ClickHouse.sigmoid(x.Double),
                Sqr = SqlFunctions.ClickHouse.sqr(x.Double)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("erf(d) as `Erf`");
            sql.Should().Contain("erfc(d) as `Erfc`");
            sql.Should().Contain("lgamma(d) as `Lg`");
            sql.Should().Contain("tgamma(d) as `Tg`");
            sql.Should().Contain("gcd(i, 12) as `Gcd`");
            sql.Should().Contain("lcm(i, 6) as `Lcm`");
            sql.Should().Contain("isFinite(d) as `Fin`");
            sql.Should().Contain("isInfinite(d) as `Inf`");
            sql.Should().Contain("isNaN(d) as `Nan`");
            sql.Should().Contain("hypot(d, 4) as `Hyp`");
            sql.Should().Contain("intDiv(i, 2) as `Idv`");
            sql.Should().Contain("intExp2(i) as `E2`");
            sql.Should().Contain("intExp10(i) as `E10`");
            sql.Should().Contain("sigmoid(d) as `Sig`");
            sql.Should().Contain("(d * d) as `Sqr`", "sqr is rendered as the vendor-defined product");
        }
    }

    [Fact]
    public void NullableOperands_ShouldKeepTheRealNullArgumentInsteadOfDroppingIt()
    {
        // A null passed to a required parameter is a real NULL operand: only an optional parameter
        // (one that declares a default) may be omitted. Dropping a required null would change the arity
        // (hypot(a, NULL) -> hypot(a)) and the call would fail on the server instead of returning NULL.
        // gcd/lcm have no nullable shape at all (see GcdAndLcm_ShouldOfferNoNullableOverload).
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bNumericEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                HypNull = SqlFunctions.ClickHouse.hypot(x.NullableDouble, null),
                DivNull = SqlFunctions.ClickHouse.int_div(x.NullableInteger, null),
                FinNull = SqlFunctions.ClickHouse.is_finite((double?)null),
                SqrNull = SqlFunctions.ClickHouse.sqr((double?)null),
                // An optional parameter is still dropped when its default is used (the F01 contract).
                OverlayDefault = SqlFunctions.ClickHouse.overlay(x.String, "XY", 2, null)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("hypot(nd, null) as `HypNull`");
            sql.Should().NotContain("hypot(nd) as");
            sql.Should().Contain("intDiv(ni, null) as `DivNull`");
            sql.Should().Contain("isFinite(null) as `FinNull`");
            sql.Should().Contain("(null * null) as `SqrNull`");
            sql.Should().Contain("overlay(s, 'XY', 2) as `OverlayDefault`", "an optional null length is dropped");
        }
    }

    [Fact]
    public void OperatorsAndConstantE_ShouldEmitTheIdenticalClickHouseForms()
    {
        // The catalog's operator forms are the portable CLR operators: they render the infix operator
        // that ClickHouse defines as the very same function (plus/minus/multiply/divide/negate), and the
        // constant `e` is the portable Math.E literal. The representation is part of the delivered
        // contract, so it is pinned here rather than silently left to the shared translator.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bNumericEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Add = x.Integer + 2L,
                Sub = x.Integer - 2L,
                Mul = x.Integer * 3L,
                Div = x.Double / 2.5,
                Neg = -x.Integer,
                Euler = Math.E
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("(i + 2) as `Add`", "plus(a, b) is the function form of a + b");
            sql.Should().Contain("(i - 2) as `Sub`", "minus(a, b) is the function form of a - b");
            sql.Should().Contain("(i * 3) as `Mul`", "multiply(a, b) is the function form of a * b");
            sql.Should().Contain("(d / 2.5) as `Div`", "divide(a, b) is the function form of a / b");
            sql.Should().Contain("-(i) as `Neg`", "negate(a) is the function form of -a");
            sql.Should().Contain("2.718281828459045", "e is the portable Math.E constant rendered as a literal");
            sql.Should().NotContain("e()", "the constant is not re-routed through the server function");
            double.Parse("2.718281828459045", System.Globalization.CultureInfo.InvariantCulture)
                .Should().Be(Math.E, "the rendered literal round-trips to Math.E exactly");
            sql.Should().NotContain("intDiv", "divide is not silently mapped onto the integer division");
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bNumericEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.erf(x.Double) > 0.5
                && SqlFunctions.ClickHouse.is_nan(x.Double)
                && SqlFunctions.ClickHouse.gcd(x.Integer, 6L) == 6L
                && SqlFunctions.ClickHouse.int_div(x.Integer, 2L) > 0L)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where");
            sql.Should().Contain("erf(d) > 0.5");
            sql.Should().Contain("isNaN(d)");
            sql.Should().Contain("gcd(i, 6) = 6");
            sql.Should().Contain("intDiv(i, 2) > 0");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IT286bNumericEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new { S = SqlFunctions.ClickHouse.sigmoid(x.NullableDouble) }));

        sql.Should().Contain("sigmoid(`nd`) as `S`", "the argument stays a quoted column and the alias is preserved");
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
            Func<string> query = name switch
            {
                "erf" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.erf(x.NullableDouble.GetValueOrDefault()))),
                "erfc" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.erfc(x.Double))),
                "lgamma" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.lgamma(x.Double))),
                "tgamma" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.tgamma(x.Double))),
                "gcd" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.gcd(x.Integer, 2L))),
                "lcm" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.lcm(x.Integer, 2L))),
                "is_finite" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.is_finite(x.Double))),
                "is_infinite" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.is_infinite(x.Double))),
                "is_nan" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.is_nan(x.Double))),
                "hypot" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.hypot(x.Double, 1.0))),
                "int_div" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.int_div(x.Integer, 2L))),
                "int_exp2" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.int_exp2(x.Integer))),
                "int_exp10" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.int_exp10(x.Integer))),
                "sigmoid" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.sigmoid(x.Double))),
                "sqr" => () => SqlOf(ctx, ctx.From<IT286bNumericEntity>().Select(x => SqlFunctions.ClickHouse.sqr(x.Double))),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the F02 slice")
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage($"The {name} function is not supported by this provider.");
        }
    }
}
