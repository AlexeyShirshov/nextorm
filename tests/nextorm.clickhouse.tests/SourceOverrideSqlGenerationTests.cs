using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Verifies the per-query source overrides on ClickHouse: a database-qualified name and a raw table
/// expression are expressible, while a linked-server qualifier is rejected. No connection is opened.
/// </summary>
public class SourceOverrideSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void WithDatabase_ShouldRenderDatabaseQualifiedName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithDatabase("shop").Select(x => new { x.Id }))
            .Should().Be("select id from shop.simple_entity");
    }

    [Fact]
    public void WithServer_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithServer("srv").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithTableExpression_ShouldRenderDerivedSource()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTableExpression("select id from other").Select(x => new { x.Id }))
            .Should().Be("select id from (select id from other) as `t1`");
    }
}
