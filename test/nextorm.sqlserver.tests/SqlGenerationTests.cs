using System.Data.Common;
using FluentAssertions;
using nextorm.core;

namespace nextorm.sqlserver.tests;

/// <summary>
/// Verifies the Microsoft SQL Server specific SQL dialect. These tests never open a database
/// connection, so they run on every build/CI. Behavioural tests against a real server live in
/// nextorm.integration.tests.
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
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.Param<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Limit_ShouldUseTop()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Limit(5).Select(x => x.Id));

        // SQL Server can only apply TOP when there is no offset.
        sql.Should().StartWith("select top(5) ");
        sql.Should().NotContain("offset");
        sql.Should().NotContain("fetch");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseOffsetFetch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Page(5, 10).Select(x => x.Id));

        // OFFSET/FETCH requires an ORDER BY, so the provider injects an empty sort first.
        sql.Should().Contain("order by (select null as anyorder)");
        sql.Should().EndWith("offset 10 rows\nfetch next 5 rows only");
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldNotEmitFetch()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Offset(10).Select(x => x.Id));

        sql.Should().EndWith("offset 10 rows");
        sql.Should().NotContain("fetch next");
    }

    [Fact]
    public void Paging_WithOrderBy_ShouldNotInjectEmptySorting()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Offset(10).OrderBy(x => x.Id).Select(x => x.Id));

        sql.Should().Contain("order by id");
        sql.Should().NotContain("anyorder");
    }

    [Fact]
    public void OrderByWithoutPaging_ShouldNotInjectEmptySorting()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Select(x => x.Id));

        // The empty sort exists only to make OFFSET/FETCH legal; a plain select must stay clean.
        sql.Should().NotContain("order by");
    }

    [Fact]
    public void BooleanLiteral_ShouldUseOne()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Boolean == true).Select(x => x.Boolean)).Should().Contain("= 1");
    }

    [Fact]
    public void StringConcat_ShouldUsePlusOperator()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("+");
        sql.Should().NotContain("||");
        sql.Should().Contain("as [V]");
    }

    [Fact]
    public void Coalesce_ShouldUseIsNullFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String ?? "" })).Should().Contain("isnull(");
    }

    [Fact]
    public void Count_ShouldUseCountStar()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.count())).Should().Contain("count(*)");
    }

    [Fact]
    public void CountBig_ShouldUseCountBigFunction()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.count_big())).Should().Contain("count_big(*)");
    }

    [Fact]
    public void Aggregate_ShouldKeepProviderSpecificName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        // SQL Server natively has stdev/var; no remapping is needed.
        SqlOf(ctx, e.Select(x => NORM.SQL.stdev((double)x.Id))).Should().Contain("stdev(");
    }

    [Fact]
    public void ComputedColumn_ShouldBeAliasedWithBrackets()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id, Calc = x.Id + 1 }));

        sql.Should().Contain("as [Calc]");
        sql.Should().NotContain("as '");
    }

    [Fact]
    public void NestedCalculatedColumn_ShouldReferenceInnerAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var nested = e.Select(x => new { x.Id, Calc = x.String + x.String });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id, t.Calc }));

        sql.Should().Contain("as [Calc]");
        sql.Should().Contain(") as [t1]");
    }

    [Fact]
    public void Subquery_ShouldAlwaysHaveAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.Create<IComplexEntity>();

        var nested = e.Select(x => new { x.Id });
        var sql = SqlOf(ctx, ctx.From(nested).Select(t => new { t.Id }));

        // A SQL Server derived table must be aliased; single quoted aliases are not valid here.
        sql.Should().Contain(") as [t1]");
        sql.Should().NotContain("as '");
    }

    [Fact]
    public void Join_ShouldQuoteTableAliases()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.Create<ISimpleEntity>();
        var complex = ctx.Create<IComplexEntity>();

        var sql = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.String }));

        sql.Should().Contain("as [t1]");
        sql.Should().Contain("as [t2]");
    }

    [Fact]
    public void AliasEscape_ShouldUseBrackets()
    {
        using var ctx = SqlServerTestContext.CreateSqlServer();

        ctx.Escape("Some Alias").Should().Be("[Some Alias]");
    }

    [Fact]
    public void SubqueryAlias_ShouldBeRequired()
    {
        using var ctx = SqlServerTestContext.CreateSqlServer();

        ctx.RequireSubqueryAlias.Should().BeTrue();
    }
}
