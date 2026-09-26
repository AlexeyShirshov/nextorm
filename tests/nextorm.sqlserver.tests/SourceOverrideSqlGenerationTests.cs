using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Verifies the per-query source overrides (<c>WithTableName</c>/<c>WithSchema</c>/<c>WithDatabase</c>/
/// <c>WithServer</c>/<c>WithTableExpression</c>) render the SQL Server four-part name. No database
/// connection is opened.
/// </summary>
public class SourceOverrideSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void WithTableName_ShouldReplaceMappedTable()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTableName("orders_archive").Select(x => new { x.Id }))
            .Should().Be("select id from orders_archive");
    }

    [Fact]
    public void WithSchema_ShouldRenderSchemaQualifiedName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithSchema("sales").Select(x => new { x.Id }))
            .Should().Be("select id from sales.simple_entity");
    }

    [Fact]
    public void WithDatabase_ShouldRenderDatabaseQualifiedName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithDatabase("shop").Select(x => new { x.Id }))
            .Should().Be("select id from shop.simple_entity");
    }

    [Fact]
    public void WithServer_ShouldRenderServerQualifiedName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithServer("linked").Select(x => new { x.Id }))
            .Should().Be("select id from linked.simple_entity");
    }

    [Fact]
    public void AllParts_ShouldRenderFourPartName()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithServer("srv").WithDatabase("db").WithSchema("dbo").WithTableName("t").Select(x => new { x.Id }))
            .Should().Be("select id from srv.db.dbo.t");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldQuoteEveryPart()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithServer("srv").WithDatabase("db").WithSchema("sales").Select(x => new { x.Id }))
            .Should().Be("select [id] from [srv].[db].[sales].[simple_entity]");
    }

    [Fact]
    public void WithTableExpression_ShouldRenderDerivedSource()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTableExpression("select id from other").Select(x => new { x.Id }))
            .Should().Be("select id from (select id from other) as [t1]");
    }

    [Fact]
    public void WithTableExpressionAndQualifier_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithTableExpression("select 1 as id").WithSchema("sales").Select(x => new { x.Id }));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Override_ShouldNotLeakToAnotherQuery()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithSchema("sales").Select(x => new { x.Id })).Should().Be("select id from sales.simple_entity");
        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }
}
