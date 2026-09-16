using System.Data.Common;
using FluentAssertions;
using nextorm.core;

namespace nextorm.sqlite.tests;

/// <summary>
/// Verifies the SQLite specific SQL dialect. These tests never open a database connection
/// (the in-memory connection string is never used), so they run on every build/CI.
/// Behavioural tests against a real database live in nextorm.integration.tests.
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
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Parameter_ShouldUseDollarPrefix()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.Param<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = $norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseLimitOffset()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 10).Select(x => x.Id)).Should().EndWith("limit 5 offset 10");
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldEmitLimitMinusOne()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        // SQLite has no OFFSET without LIMIT, so the provider emits "limit -1".
        SqlOf(ctx, e.Offset(10).Select(x => x.Id)).Should().EndWith("limit -1 offset 10");
    }

    [Fact]
    public void BooleanLiteral_ShouldUseOne()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean == true).Select(x => x.Boolean)).Should().Contain("b = 1");
    }

    [Fact]
    public void StringConcat_ShouldUseDoublePipe()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("||");
        sql.Should().Contain("as 'V'");
    }

    [Fact]
    public void Coalesce_ShouldUseIfNullFunction()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String ?? "" }));

        sql.Should().Contain("ifnull(");
        sql.Should().NotContain("coalesce(");
    }

    [Fact]
    public void Count_ShouldUseCountStar()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.count())).Should().Contain("count(*)");
    }

    [Fact]
    public void CountBig_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => NORM.SQL.count_big()));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Aggregate_ShouldKeepProviderSpecificName()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => NORM.SQL.stdev((double)x.Id)));

        // SQLite relies on its own registered functions, so no name remapping happens.
        sql.Should().Contain("stdev(");
        sql.Should().NotContain("stddev(");
    }

    [Fact]
    public void ComputedColumn_ShouldBeAliasedWithSingleQuotes()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Calc = x.Id + 1 }));

        sql.Should().Contain("as 'Calc'");
        sql.Should().NotContain("as \"");
    }

    [Fact]
    public void RenamedColumn_ShouldBeAliased()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // The underlying column is "somestring"; the projection calls it "String", so the SQL must
        // expose it under the projected name for outer queries to find it.
        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Renamed = x.String }));

        sql.Should().Contain("as 'Renamed'");
    }

    [Fact]
    public void NestedCalculatedColumn_ShouldReferenceInnerAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var nested = e.Select(x => new { x.Id, Calc = x.String + x.String });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id, t.Calc }));

        sql.Should().Contain("as 'Calc'");
        sql.Should().Contain("Calc");
    }

    [Fact]
    public void Subquery_ShouldNotRequireAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var nested = e.Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id }));

        // Unlike PostgreSQL, SQLite does not require a derived table to be aliased.
        sql.Should().NotContain(") as '");
    }

    [Fact]
    public void Join_ShouldQuoteTableAliases()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain("as 't1'");
        sql.Should().Contain("as 't2'");
    }

    [Fact]
    public void SubqueryAliasIsNotRequired()
    {
        using var ctx = SqliteTestContext.CreateSqlite();

        ctx.RequireSubqueryAlias.Should().BeFalse();
    }

    [Fact]
    public void Escape_ShouldUseSingleQuotes()
    {
        using var ctx = SqliteTestContext.CreateSqlite();

        ctx.Escape("Some Alias").Should().Be("'Some Alias'");
    }
}
