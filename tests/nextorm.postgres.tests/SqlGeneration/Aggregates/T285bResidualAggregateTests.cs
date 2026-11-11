using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T285b residual aggregate cells for PostgreSQL: the native median composition
/// (<c>percentile_cont(0.5) within group (order by key)</c>), the generic <c>percentile</c> refusal, the
/// floating-point restriction of the continuous form, and the fraction guard shared by the ordered-set
/// family. These tests never open a database connection; the live counterpart lives in
/// NextORM.Integration.Tests.
/// </summary>
public class T285bResidualAggregateTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    [Fact]
    public void Median_ShouldEmitTheOrderedSetComposition()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { M = SqlFunctions.Postgres.median(() => (double)x.Id) }));

        sql.Should().Contain("percentile_cont(0.5) within group (order by cast(id as double precision))");
        sql.Should().NotContain("median(");
    }

    [Fact]
    public void PercentileDisc_ShouldKeepTheSortExpressionType()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Postgres.percentile_disc(0.5, () => x.Int) }));

        sql.Should().Contain("percentile_disc(0.5) within group (order by nullableint)");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefused()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.Postgres.percentile(0.5, () => (double)x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by PostgreSQL.");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefusedBeforeTheFractionIsInspectated()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.Postgres.percentile(-1, () => (double)x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile function is not supported by PostgreSQL.");
    }

    [Fact]
    public void PercentileCont_ShouldRefuseAnIntegerSortExpression()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Postgres.percentile_cont(0.5, () => x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile_cont function on PostgreSQL requires a floating-point sort expression.");
    }

    [Fact]
    public void Median_ShouldRefuseAnIntegerSortExpression()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { M = SqlFunctions.Postgres.median(() => x.Id) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile_cont function on PostgreSQL requires a floating-point sort expression.");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void FractionBoundaries_ShouldBeAccepted(double fraction)
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Postgres.percentile_cont(fraction, () => (double)x.Id) }));

        sql.Should().Contain("within group (order by");
    }

    [Fact]
    public void CapturedFraction_ShouldBeAccepted()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var fraction = 0.25;

        var sql = SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Postgres.percentile_cont(fraction, () => (double)x.Id) }));

        sql.Should().Contain("within group (order by");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void OutOfRangeFraction_ShouldBeRejected(double fraction)
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Postgres.percentile_cont(fraction, () => (double)x.Id) }));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_cont on PostgreSQL must be finite and between 0 and 1 inclusive.*");
    }

    [Fact]
    public void NonFiniteFraction_ShouldBeRejected()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Postgres.percentile_cont(double.NaN, () => (double)x.Id) }));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_cont on PostgreSQL must be finite and between 0 and 1 inclusive.*");
    }

    [Fact]
    public void PositiveInfinityFraction_ShouldBeRejected()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Postgres.percentile_cont(double.PositiveInfinity, () => (double)x.Id) }));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_cont on PostgreSQL must be finite and between 0 and 1 inclusive.*");
    }

    [Fact]
    public void NegativeInfinityFraction_ShouldBeRejected()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Postgres.percentile_cont(double.NegativeInfinity, () => (double)x.Id) }));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_cont on PostgreSQL must be finite and between 0 and 1 inclusive.*");
    }

    [Fact]
    public void RowDependentFraction_ShouldBeRefused()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // The fraction is a direct aggregate argument, not a per-row input: a row-derived value would
        // change the statistic per row while the server expects one fraction for the whole group.
        var act = () => SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.Postgres.percentile_cont(x.Int!.Value / 10.0, () => (double)x.Id)
        }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The fraction for percentile_cont on PostgreSQL must be a constant or a captured scalar.");
    }
}
