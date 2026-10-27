using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// T285b residual aggregate cells for MariaDB: the native analytic median
/// (<c>percentile_cont(0.5) within group (order by value) over (...)</c>, which keeps one row per input
/// row), the native continuous/discrete window percentiles, the MariaDB-named generic <c>percentile</c>
/// refusal, and the fraction guard of the continuous family. These tests never open a database
/// connection; the live counterpart lives in NextORM.Integration.Tests.
/// </summary>
public class T285bResidualAggregateTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    [Fact]
    public void Median_ShouldEmitTheAnalyticCompositionAndKeepOneRowPerInputRow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { M = SqlFunctions.MySql.median(x.Id).Over() }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by id) over ()");
        sql.Should().NotContain("median(");
    }

    [Fact]
    public void Median_ShouldEmitThePartitionedAnalyticCompositionAndKeepOneRowPerInputRow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.MySql.median(x.Id).Over(partitionBy: () => x.String)
        }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by id) over (partition by somestring)");
    }

    [Fact]
    public void ContinuousAndDiscreteCells_ShouldStayNativeWindowPercentiles()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over(),
            D = SqlFunctions.Sql.percentile_disc(0.5, x.Id).Over()
        }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by id) over ()");
        sql.Should().Contain("percentile_disc(0.5) within group (order by id) over ()");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefusedWithTheMariaDbName()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.MySql.percentile(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by MariaDB.");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefusedBeforeTheFractionIsInspected()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.MySql.percentile(-1, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by MariaDB.");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void FractionBoundaries_ShouldBeAccepted(double fraction)
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Sql.percentile_cont(fraction, x.Id).Over() }));

        sql.Should().Contain("percentile_cont(").And.Contain(") within group (order by id) over ()");
    }

    [Fact]
    public void CapturedFraction_ShouldBeAccepted()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var fraction = 0.25;

        var sql = SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Sql.percentile_cont(fraction, x.Id).Over() }));

        sql.Should().Contain(") within group (order by id) over ()");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void OutOfRangeFraction_ShouldBeRejected(double fraction)
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Sql.percentile_cont(fraction, x.Id).Over() }));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_cont on MariaDB must be finite and between 0 and 1 inclusive.*");
    }

    [Fact]
    public void NonFiniteFraction_ShouldBeRejected()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.percentile_disc(double.NaN, x.Id).Over() }));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_disc on MariaDB must be finite and between 0 and 1 inclusive.*");
    }
}
