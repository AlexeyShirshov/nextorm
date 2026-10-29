using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286 (#286): the ClickHouse-only math functions <c>log2</c>, <c>cbrt</c>, <c>exp2</c> and
/// <c>exp10</c>. This slice exposes them through <see cref="ClickHouseFunctions"/> only, so the
/// tests pin the exact <see cref="IScalarFunctions"/> arm, both overloads, the generated projection
/// and predicate placement, quoted identifiers/aliases, and the real refusal of the other dialects.
/// These tests never open a connection.
/// </summary>
public class T286ClickHouseMathSqlGenerationTests
{
    private static readonly IScalarFunctions ClickHouseScalars = ClickHouseDialect.Instance.ScalarFunctions!;

    private static readonly (string Name, string Expected)[] Arms =
    [
        ("log2", "log2(x)"),
        ("cbrt", "cbrt(x)"),
        ("exp2", "exp2(x)"),
        ("exp10", "exp10(x)")
    ];

    [SqlTable("t286_math_entity")]
    internal interface IT286MathEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("v")]
        double Value { get; set; }

        [Column("nv")]
        double? NullableValue { get; set; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEachFunction()
    {
        using (new AssertionScope())
        {
            foreach (var (name, expected) in Arms)
                ClickHouseScalars.Render(name, ["x"]).Should().Be(expected, "'{0}' is the ClickHouse arm", name);
        }
    }

    [Fact]
    public void Supports_ShouldAcceptTheFourNativeNamesAndNothingElseFromThisSlice()
    {
        using (new AssertionScope())
        {
            foreach (var (name, _) in Arms)
                ClickHouseScalars.Supports(name).Should().BeTrue("'{0}' is part of the ClickHouse inventory", name);

            ClickHouseScalars.Supports("LOG2").Should().BeFalse("names are case-sensitive CLR member names");
            ClickHouseScalars.Supports("log2x").Should().BeFalse("the name must match exactly");
        }
    }

    [Fact]
    public void Projection_ShouldTranslateBothOverloadsOfEveryFunction()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286MathEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L2 = SqlFunctions.ClickHouse.log2(x.Value),
            L2n = SqlFunctions.ClickHouse.log2(x.NullableValue),
            Cb = SqlFunctions.ClickHouse.cbrt(x.Value),
            Cbn = SqlFunctions.ClickHouse.cbrt(x.NullableValue),
            E2 = SqlFunctions.ClickHouse.exp2(x.Value),
            E2n = SqlFunctions.ClickHouse.exp2(x.NullableValue),
            E10 = SqlFunctions.ClickHouse.exp10(x.Value),
            E10n = SqlFunctions.ClickHouse.exp10(x.NullableValue)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("log2(v) as `L2`");
            sql.Should().Contain("log2(nv) as `L2n`");
            sql.Should().Contain("cbrt(v) as `Cb`");
            sql.Should().Contain("cbrt(nv) as `Cbn`");
            sql.Should().Contain("exp2(v) as `E2`");
            sql.Should().Contain("exp2(nv) as `E2n`");
            sql.Should().Contain("exp10(v) as `E10`");
            sql.Should().Contain("exp10(nv) as `E10n`");
        }
    }

    [Fact]
    public void Predicate_ShouldPlaceTheFourFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286MathEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.log2(x.Value) > 3
                && SqlFunctions.ClickHouse.cbrt(x.Value) > 2
                && SqlFunctions.ClickHouse.exp2(x.Value) > 8
                && SqlFunctions.ClickHouse.exp10(x.Value) > 100)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where", "a filter over the four functions stays a predicate");
            sql.Should().Contain("log2(v) > 3");
            sql.Should().Contain("cbrt(v) > 2");
            sql.Should().Contain("exp2(v) > 8");
            sql.Should().Contain("exp10(v) > 100");
            sql.Should().NotContain("Float64 NaN", "an unsupported form must not be emulated");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IT286MathEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Q = SqlFunctions.ClickHouse.cbrt(x.NullableValue)
        }));

        sql.Should().Contain("cbrt(`nv`) as `Q`", "the argument stays a quoted column and the alias is preserved");
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
                foreach (var (name, _) in Arms)
                    functions.Supports(name).Should().BeFalse("'{0}' is ClickHouse-only", name);
            }
        }

        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var (name, _) in Arms)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            Func<string> query = name switch
            {
                "log2" => () => SqlOf(ctx, ctx.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.log2(x.NullableValue))),
                "cbrt" => () => SqlOf(ctx, ctx.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.cbrt(x.NullableValue))),
                "exp2" => () => SqlOf(ctx, ctx.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.exp2(x.NullableValue))),
                "exp10" => () => SqlOf(ctx, ctx.From<IT286MathEntity>().Select(x => SqlFunctions.ClickHouse.exp10(x.NullableValue))),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the T286 slice")
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage($"The {name} function is not supported by this provider.");
        }
    }
}
