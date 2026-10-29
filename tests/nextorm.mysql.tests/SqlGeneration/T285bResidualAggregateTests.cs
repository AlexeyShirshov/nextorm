using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// T285b residual aggregate cells for MySQL: every percentile-family cell is a deterministic refusal
/// naming MySQL (MySQL 8 has neither a <c>median</c> function nor the <c>WITHIN GROUP</c> grammar), and
/// no speculative SQL is emitted. The inherited window members
/// <c>CommonFunctions.percentile_cont</c>/<c>percentile_disc</c> are the callable continuous/discrete
/// surface, so their refusal is pinned here too. These tests never open a database connection.
/// </summary>
public class T285bResidualAggregateTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    [Fact]
    public void Median_ShouldBeRefusedWithTheMySqlName()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { M = SqlFunctions.MySql.median(x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The median function is not supported by MySQL.");
    }

    [Fact]
    public void MedianWithoutOver_ShouldBeRefusedBeforeTheWindowShapeCheck()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { M = SqlFunctions.MySql.median(x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The median function is not supported by MySQL.");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefusedWithTheMySqlName()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.MySql.percentile(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by MySQL.");
    }

    [Fact]
    public void GenericPercentileWithoutOver_ShouldBeRefusedWithTheMySqlName()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.MySql.percentile(0.5, x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by MySQL.");
    }

    [Fact]
    public void ContinuousCell_ShouldBeRefusedWithTheMySqlName()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile_cont function is not supported by MySQL.");
    }

    [Fact]
    public void DiscreteCell_ShouldBeRefusedWithTheMySqlName()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.percentile_disc(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile_disc function is not supported by MySQL.");
    }
}
