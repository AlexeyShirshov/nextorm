using System.Data.Common;
using FluentAssertions;
using nextorm.core;

namespace nextorm.mysql.tests;

/// <summary>
/// Verifies the MySQL specific SQL dialect. These tests never open a database connection, so they
/// run on every build/CI.
/// </summary>
public class SqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void StringConcat_ShouldUseConcatFunction()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("concat(");
        sql.Should().NotContain("||");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.Param<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseLimitOffset()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 10).Select(x => x.Id)).Should().EndWith("limit 5 offset 10");
    }

    [Fact]
    public void CrossApply_ShouldEmitCrossJoinLateral()
    {
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.CrossApply(complex).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" cross join lateral complex_entity as `t2`");
        sql.Should().NotContain(" on true");
    }

    [Fact]
    public void OuterApply_ShouldEmitLeftJoinLateralOnTrue()
    {
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.OuterApply(complex).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain(" left join lateral complex_entity as `t2` on true");
    }

    [Fact]
    public void QueryHint_ShouldThrowBecauseMySqlHasNoQueryHints()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { x.Id }).Hint("recompile"));

        act.Should().Throw<NotSupportedException>().WithMessage("*Query hints*");
    }

    [Fact]
    public void GroupByRollup_ShouldUseWithRollup()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByRollup(x => new { x.Int, x.Boolean })
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("group by nullableint, b with rollup");
    }

    [Fact]
    public void GroupByCube_ShouldThrowBecauseMySqlHasNoCube()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .GroupByCube(x => new { x.Int })
            .Select(x => new { x.Int }));

        act.Should().Throw<NotSupportedException>().WithMessage("*CUBE*");
    }

    [Fact]
    public void GroupByGroupingSets_ShouldThrowBecauseMySqlHasNoGroupingSets()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .GroupByGroupingSets(x => new { x.Int }, new[] { 0 })
            .Select(x => new { x.Int }));

        act.Should().Throw<NotSupportedException>().WithMessage("*GROUPING SETS*");
    }
}
