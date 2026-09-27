using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Verifies the per-query source overrides on PostgreSQL: a schema-qualified name and a raw table
/// expression are expressible, while a database/linked-server qualifier is rejected. No connection is
/// opened.
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
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTableName("orders_archive").Select(x => new { x.Id }))
            .Should().Be("select id from orders_archive");
    }

    [Fact]
    public void WithSchema_ShouldRenderSchemaQualifiedName()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithSchema("sales").Select(x => new { x.Id }))
            .Should().Be("select id from sales.simple_entity");
    }

    [Fact]
    public void WithDatabase_ShouldThrowBecauseNotSupported()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithDatabase("shop").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithServer_ShouldThrowBecauseNotSupported()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithServer("srv").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithTableExpression_ShouldRenderDerivedSource()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTableExpression("select id from other").Select(x => new { x.Id }))
            .Should().Be("select id from (select id from other) as \"t1\"");
    }
}
