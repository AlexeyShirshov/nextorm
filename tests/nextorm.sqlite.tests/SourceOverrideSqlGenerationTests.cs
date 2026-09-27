using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Verifies the per-query source overrides on SQLite: a schema/database qualifier names an attached
/// database and a raw table expression renders as a derived source. No connection is opened.
/// </summary>
public class SourceOverrideSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void WithSchema_ShouldRenderAttachedDatabaseQualifier()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithSchema("main").Select(x => new { x.Id }))
            .Should().Be("select id from main.simple_entity");
    }

    [Fact]
    public void WithDatabase_ShouldRenderAttachedDatabaseQualifier()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithDatabase("main").Select(x => new { x.Id }))
            .Should().Be("select id from main.simple_entity");
    }

    [Fact]
    public void WithServer_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithServer("srv").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithTableExpression_ShouldRenderDerivedSource()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.WithTableExpression("select id from other").Select(x => new { x.Id }))
            .Should().Be("select id from (select id from other) as 't1'");
    }

    [Fact]
    public void WithoutOverride_ShouldRenderMappedTable()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void NamedTableBuilder_WithSchema_ShouldQualify()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From("simple_entity").WithSchema("main").Select(t => new { id = t["id"].AsInt }))
            .Should().Be("select id from main.simple_entity");
    }

    [Fact]
    public void NamedTableBuilder_WithTableExpression_ShouldRenderDerivedSource()
    {
        using var ctx = SqliteTestContext.Create();

        SqlOf(ctx, ctx.From("simple_entity").WithTableExpression("select id from other").Select(t => new { id = t["id"].AsInt }))
            .Should().Be("select t1.id from (select id from other) as 't1'");
    }

    [Fact]
    public void SameOverride_ShouldReuseCachedPlan()
    {
        using var ctx = SqliteTestContext.Create();

        var first = ctx.GetPreparedQueryCommand(
            ctx.From<ISimpleEntity>().WithSchema("main").Select(x => new { x.Id }), false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(
            ctx.From<ISimpleEntity>().WithSchema("main").Select(x => new { x.Id }), false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeTrue("an equal source override must reuse the cached plan");
    }

    [Fact]
    public void DifferentOverride_ShouldNotReuseCachedPlan()
    {
        using var ctx = SqliteTestContext.Create();

        var first = ctx.GetPreparedQueryCommand(
            ctx.From<ISimpleEntity>().WithSchema("main").Select(x => new { x.Id }), false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(
            ctx.From<ISimpleEntity>().WithSchema("other").Select(x => new { x.Id }), false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeFalse("a different source override must not share a cached plan");
    }
}
