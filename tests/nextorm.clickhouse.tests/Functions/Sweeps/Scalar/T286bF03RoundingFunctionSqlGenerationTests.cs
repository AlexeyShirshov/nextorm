using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D3 (F03 Rounding): the ClickHouse-only rounding members <c>roundAge</c>,
/// <c>roundBankers</c>, <c>roundDown</c>, <c>roundDuration</c> and <c>roundToExp2</c>. The tests pin the
/// exact <see cref="IScalarFunctions"/> arms (including the <c>toInt64</c> lowering of the
/// <c>UInt8</c>/<c>UInt16</c> results), the declared CLR shapes, the optional scale of
/// <c>roundBankers</c>, the constant-array argument of <c>roundDown</c> (bound as one array parameter,
/// rendered as SQL for an array column), the real NULL operands of required parameters, and the
/// fail-closed refusal of every other provider. They never open a connection.
/// </summary>
public class T286bF03RoundingFunctionSqlGenerationTests
{
    private static readonly IScalarFunctions ClickHouseScalars = ClickHouseDialect.Instance.ScalarFunctions!;

    /// <summary>One row per render arm of the delivered members.</summary>
    private static readonly (string Name, string[] Args, string Expected)[] Arms =
    [
        ("round_age", ["age"], "toInt64(roundAge(age))"),
        ("round_bankers", ["x"], "roundBankers(x)"),
        ("round_bankers", ["x", "2"], "roundBankers(x, 2)"),
        ("round_down", ["x", "bounds"], "roundDown(x, bounds)"),
        ("round_duration", ["duration"], "toInt64(roundDuration(duration))"),
        ("round_to_exp2", ["x"], "roundToExp2(x)")
    ];

    /// <summary>The catalog names, one per member (the scale variant shares its member's name).</summary>
    private static readonly string[] MemberNames =
    [
        "round_age", "round_bankers", "round_down", "round_duration", "round_to_exp2"
    ];

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

        [Column("bufs")]
        double[] Buffers { get; set; }
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
    public void Supports_ShouldAcceptTheCompleteRoundingSliceAndRejectCaseAndNativeSpellings()
    {
        MemberNames.Should().HaveCount(5, "the F03 slice delivers 5 ClickHouse-only rounding names");

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
                ClickHouseScalars.Supports(name).Should().BeTrue("'{0}' is part of the ClickHouse inventory", name);

            ClickHouseScalars.Supports("ROUND_AGE").Should().BeFalse("names are case-sensitive CLR member names");
            ClickHouseScalars.Supports("roundAge").Should().BeFalse("the table is keyed by the CLR member name");
            ClickHouseScalars.Supports("roundBankers").Should().BeFalse("the native spelling is not the table key");
            ClickHouseScalars.Supports("roundDown").Should().BeFalse("the native spelling is not the table key");
            ClickHouseScalars.Supports("roundDuration").Should().BeFalse("the native spelling is not the table key");
            ClickHouseScalars.Supports("roundToExp2").Should().BeFalse("the native spelling is not the table key");
        }
    }

    [Fact]
    public void Render_ShouldThrowForTheNativeSpellingInsteadOfTheMemberName()
    {
        FluentActions.Invoking(() => ClickHouseScalars.Render("roundAge", ["age"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The roundAge function is not supported by ClickHouse.");

        FluentActions.Invoking(() => ClickHouseScalars.Render("roundBankers", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The roundBankers function is not supported by ClickHouse.");
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        using (new AssertionScope())
        {
            // roundAge/roundDuration return UInt8/UInt16, lowered with toInt64 -> long.
            Shape("round_age", typeof(long)).Should().Be(typeof(long), "roundAge returns UInt8 lowered to Int64");
            Shape("round_age", typeof(long?)).Should().Be(typeof(long?), "a NULL argument stays Nullable(Int64)");
            Shape("round_duration", typeof(long)).Should().Be(typeof(long), "roundDuration returns UInt16 lowered to Int64");
            Shape("round_duration", typeof(long?)).Should().Be(typeof(long?), "a NULL argument stays Nullable(Int64)");
            Shape("round_bankers", typeof(double)).Should().Be(typeof(double), "a Float64 argument returns Float64");
            Shape("round_bankers", typeof(double), typeof(int)).Should().Be(typeof(double), "the scale keeps the Float64 result");
            Shape("round_bankers", typeof(long), typeof(int)).Should().Be(typeof(long), "an Int64 argument returns Int64");
            Shape("round_bankers", typeof(double?)).Should().Be(typeof(double?), "a NULL argument stays Nullable(Float64)");
            Shape("round_bankers", typeof(double?), typeof(int)).Should().Be(typeof(double?));
            Shape("round_bankers", typeof(long?), typeof(int)).Should().Be(typeof(long?), "a NULL integer stays Nullable(Int64)");
            Shape("round_down", typeof(double), typeof(double[])).Should().Be(typeof(double), "roundDown over Float64 returns Float64");
            Shape("round_down", typeof(double?), typeof(double[])).Should().Be(typeof(double?), "a NULL value stays Nullable(Float64)");
            Shape("round_to_exp2", typeof(long)).Should().Be(typeof(long), "roundToExp2 returns the argument type (Int64)");
            Shape("round_to_exp2", typeof(long?)).Should().Be(typeof(long?));
            Shape("round_to_exp2", typeof(double)).Should().Be(typeof(double), "roundToExp2 over Float64 returns Float64");
            Shape("round_to_exp2", typeof(double?)).Should().Be(typeof(double?));
        }

        static Type Shape(string member, params Type[] parameters) => typeof(ClickHouseFunctions)
            .GetMethods()
            .Single(method => method.Name == member
                && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters))
            .ReturnType;
    }

    [Fact]
    public void RoundDown_ShouldKeepTheBoundariesConstantAndRenderAnArrayColumnAsSql()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bRoundingEntity>();
        var bounds = new[] { 1.0, 5.0, 10.0 };

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                // A captured array is a constant for the server: bound as one Array(Float64) parameter.
                Captured = SqlFunctions.ClickHouse.round_down(x.Value, bounds),
                Inline = SqlFunctions.ClickHouse.round_down(x.Value, new[] { 1.0, 5.0 }),
                // An array column is rendered as SQL (the server rejects it: the argument must be
                // constant); it must never be silently turned into a parameter.
                Column = SqlFunctions.ClickHouse.round_down(x.Value, x.Buffers)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("roundDown(d, @p0) as `Captured`", "a captured array is one constant parameter");
            sql.Should().Contain("roundDown(d, @p1) as `Inline`", "an inline array is one constant parameter");
            sql.Should().Contain("roundDown(d, bufs) as `Column`", "an array column stays a column");
            sql.Should().NotContain("[1, 5, 10]", "the boundaries are never inlined element by element");
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryMemberOverNumericColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bRoundingEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Age = SqlFunctions.ClickHouse.round_age(x.Age),
                Bankers = SqlFunctions.ClickHouse.round_bankers(x.Value),
                BankersScaled = SqlFunctions.ClickHouse.round_bankers(x.Value, 2),
                BankersInteger = SqlFunctions.ClickHouse.round_bankers(x.Age, -1),
                Down = SqlFunctions.ClickHouse.round_down(x.Value, 1.0, 5.0, 10.0),
                Duration = SqlFunctions.ClickHouse.round_duration(x.Age),
                Exp2Long = SqlFunctions.ClickHouse.round_to_exp2(x.Age),
                Exp2Double = SqlFunctions.ClickHouse.round_to_exp2(x.Value)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toInt64(roundAge(age)) as `Age`");
            sql.Should().Contain("roundBankers(d) as `Bankers`");
            sql.Should().Contain("roundBankers(d, 2) as `BankersScaled`", "N > 0 rounds right of the decimal point");
            sql.Should().Contain("roundBankers(age, -1) as `BankersInteger`", "a negative scale rounds to the left");
            sql.Should().Contain("roundDown(d, @p0) as `Down`", "the params boundaries become one array parameter");
            sql.Should().Contain("toInt64(roundDuration(age)) as `Duration`");
            sql.Should().Contain("roundToExp2(age) as `Exp2Long`");
            sql.Should().Contain("roundToExp2(d) as `Exp2Double`");
        }
    }

    [Fact]
    public void NullValues_ShouldStayRealNullOperands()
    {
        // Every parameter of these members is required, so a null is a real NULL operand: dropping it
        // would change the arity (roundBankers(x, NULL) -> roundBankers(x)) or render an empty call.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bRoundingEntity>();
        var bounds = new[] { 1.0, 5.0 };

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Age = SqlFunctions.ClickHouse.round_age(null),
                Duration = SqlFunctions.ClickHouse.round_duration(null),
                Bankers = SqlFunctions.ClickHouse.round_bankers((double?)null),
                Exp2 = SqlFunctions.ClickHouse.round_to_exp2((double?)null),
                Down = SqlFunctions.ClickHouse.round_down(null, bounds)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toInt64(roundAge(null)) as `Age`");
            sql.Should().Contain("toInt64(roundDuration(null)) as `Duration`");
            sql.Should().Contain("roundBankers(null) as `Bankers`");
            sql.Should().Contain("roundToExp2(null) as `Exp2`");
            sql.Should().Contain("roundDown(null, @p0) as `Down`");
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bRoundingEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.round_age(x.Age) == 25
                && SqlFunctions.ClickHouse.round_bankers(x.Value, 2) == 2.68
                && SqlFunctions.ClickHouse.round_duration(x.Age) == 3600
                && SqlFunctions.ClickHouse.round_to_exp2(x.Age) == 64)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where");
            sql.Should().Contain("toInt64(roundAge(age)) = 25");
            sql.Should().Contain("roundBankers(d, 2) = 2.68");
            sql.Should().Contain("toInt64(roundDuration(age)) = 3600");
            sql.Should().Contain("roundToExp2(age) = 64");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IT286bRoundingEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new { R = SqlFunctions.ClickHouse.round_age(x.NullableAge) }));

        sql.Should().Contain("toInt64(roundAge(`na`)) as `R`", "the argument stays a quoted column and the alias is preserved");
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
            var bounds = new[] { 1.0, 5.0 };

            Func<string> query = name switch
            {
                "round_age" => () => SqlOf(ctx, ctx.From<IT286bRoundingEntity>().Select(x => SqlFunctions.ClickHouse.round_age(x.Age))),
                "round_bankers" => () => SqlOf(ctx, ctx.From<IT286bRoundingEntity>().Select(x => SqlFunctions.ClickHouse.round_bankers(x.Value, 2))),
                "round_down" => () => SqlOf(ctx, ctx.From<IT286bRoundingEntity>().Select(x => SqlFunctions.ClickHouse.round_down(x.Value, bounds))),
                "round_duration" => () => SqlOf(ctx, ctx.From<IT286bRoundingEntity>().Select(x => SqlFunctions.ClickHouse.round_duration(x.Age))),
                "round_to_exp2" => () => SqlOf(ctx, ctx.From<IT286bRoundingEntity>().Select(x => SqlFunctions.ClickHouse.round_to_exp2(x.Age))),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the F03 slice")
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage($"The {name} function is not supported by this provider.");
        }
    }
}
