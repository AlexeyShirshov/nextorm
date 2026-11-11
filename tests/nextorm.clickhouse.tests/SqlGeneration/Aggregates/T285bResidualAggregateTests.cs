using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T285b residual aggregate cells for ClickHouse: the generic <c>percentile</c> and the inherited window
/// percentile members are deterministic refusals naming ClickHouse, while <c>median</c> stays exactly the
/// T286b native form (<c>toFloat64(median(x))</c>) and must never be re-rendered as a
/// <c>percentile</c>/<c>percentile_cont</c> alias. These tests never open a database connection.
/// </summary>
public class T285bResidualAggregateTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    [Fact]
    public void Median_ShouldKeepTheConsumedT286bNativeForm()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { M = SqlFunctions.ClickHouse.median(x.Id) }));

        sql.Should().Contain("toFloat64(median(id))");
        sql.Should().NotContain("percentile");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefusedWithTheClickHouseName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.ClickHouse.percentile(0.5, x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by ClickHouse.");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefusedBeforeTheFractionIsInspected()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.ClickHouse.percentile(-1, x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by ClickHouse.");
    }

    [Fact]
    public void ContinuousCell_ShouldBeRefusedWithTheClickHouseName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile_cont function is not supported by ClickHouse.");
    }

    [Fact]
    public void DiscreteCell_ShouldBeRefusedWithTheClickHouseName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.percentile_disc(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile_disc function is not supported by ClickHouse.");
    }
}
