using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T285b residual aggregate cells for SQL Server: the native analytic median
/// (<c>percentile_cont(0.5) within group (order by value) over (...)</c>), the analytic
/// continuous/discrete cells that must keep one row per input row, the generic <c>percentile</c>
/// refusal, and the window-shape restrictions of the median composition. These tests never open a
/// database connection; the live counterpart lives in NextORM.Integration.Tests.
/// </summary>
public class T285bResidualAggregateTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    [Fact]
    public void Median_ShouldEmitTheAnalyticCompositionWithPartition()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.SqlServer.median((double?)x.Int).Over(partitionBy: () => x.String)
        }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by cast(nullableint as float)) over (partition by somestring)");
        sql.Should().NotContain("median(");
    }

    [Fact]
    public void Median_ShouldAllowAnEmptyOverClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { M = SqlFunctions.SqlServer.median((double?)x.Int).Over() }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by cast(nullableint as float)) over ()");
    }

    [Fact]
    public void ContinuousAndDiscreteCells_ShouldKeepTheAnalyticShape()
    {
        using var ctx = SqlServerTestContext.Create();
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
    public void GenericPercentile_ShouldBeRefused()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.SqlServer.percentile(0.5, x.Id).Over() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by SQL Server.");
    }

    [Fact]
    public void Median_ShouldRefuseAWindowOrderBy()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.SqlServer.median((double?)x.Int).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The median window function supports a partition-only OVER clause; a window ORDER BY or a frame is not supported.");
    }

    [Fact]
    public void Median_ShouldRefuseAFrame()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.SqlServer.median((double?)x.Int)
                .Over(() => x.String, null, WindowFrame.RowsUnboundedPrecedingToCurrentRow)
        }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The median window function supports a partition-only OVER clause; a window ORDER BY or a frame is not supported.");
    }

    [Fact]
    public void Median_ShouldRequireTheOverClause()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { M = SqlFunctions.SqlServer.median((double?)x.Int) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The window function percentile_cont must be completed with Over(...).");
    }

    [Fact]
    public void WindowPercentileFraction_ShouldBeGuardedWithTheProviderName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Sql.percentile_cont(1.5, x.Id).Over() }));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_cont on SQL Server must be finite and between 0 and 1 inclusive.*");
    }
}
