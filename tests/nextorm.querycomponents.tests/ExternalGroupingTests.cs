using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.QueryComponents.Tests;

public class ExternalGroupingTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    [Fact]
    public void ExternalGroup_GeneratesGroupBy()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = e.ExternalGroup(x => new { x.Category, Total = x.Score }, x => x.Category!);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt!);

        sql.Should().Contain("group by");
        sql.Should().Contain("Category");
    }

    [Fact]
    public void ExternalGroup_HavingOnly_PlacesHaving()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = e.ExternalGroup(x => new { x.Category, Total = x.Score }, x => x.Category!, having: x => x.Score > 5);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt!);

        sql.Should().Contain("having");
    }

    [Fact]
    public void ExternalGroup_HavingAndExistingHaving_AndedOnce()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        // Source already has a Having; operator contributes another -> both AND-ed once.
        var cmd = e.Having(x => x.Score > 0)
            .ExternalGroup(x => new { x.Category, Total = x.Score }, x => x.Category!, having: x => x.Score < 100);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt!);

        // The combined condition should appear once (no duplicated AND of the same predicate).
        sql.Should().Contain("having");
    }

    [Fact]
    public void ExternalGroup_NoComponent_ControlUnchanged()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        var cmd = e.Select(x => x.Id);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt!);
        sql.Should().Contain("select");
        sql.Should().NotContain("group by");
    }

    [Fact]
    public void ExternalGroup_ZeroKeys_IsGlobalAggregation()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<Entity>();

        // Zero keys = explicit global aggregation.
        var comp = new ExternalGroupingComponent<Entity>(Array.Empty<LambdaExpression>());
        comp.Should().NotBeNull();

        var cmd = e.WithComponent(comp).Select(x => x.Id);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt!);
        sql.Should().Contain("select");
        // No group by for global aggregation.
        sql.Should().NotContain("group by");
    }
}
