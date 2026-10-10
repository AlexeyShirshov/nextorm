using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL generation of the multi-table <c>UPDATE ... FROM ... JOIN</c> builder on SQL Server: the target is
/// named again in the <c>FROM</c> clause and the <c>SET</c> list is qualified by its alias.
/// </summary>
public class UpdateJoinSqlGenerationTests
{
    [Fact]
    public void UpdateJoin_SetConstant_ShouldRenderTargetAliasFromJoin()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .ToSql()
            .Should().Be("update [t1] set t1.name = @p0 from merge_entity as [t1] join merge_entity as [t2] on t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_FromCte_ShouldHoistWith()
    {
        using var ctx = SqlServerTestContext.Create();

        var e = ctx.From<ISimpleEntity>();
        var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));

        var sql = e
            .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().StartWith("with c as (select id from simple_entity");
        sql.Should().Contain("update [t1] set t1.id = @p0 from simple_entity as [t1] join c as [t2] on t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_RecursiveCte_ShouldAppendMaxRecursion()
    {
        using var ctx = SqlServerTestContext.Create();

        var e = ctx.From<ISimpleEntity>();
        var anchor = e.Where(x => x.Id == 1).Select(x => new SqlGenerationTests.CteNumberRow { n = x.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new SqlGenerationTests.CteNumberRow { n = t["n"].AsInt + 1 });
        var scope = ctx.WithRecursive("nums", anchor.UnionAll(step), 50);

        var sql = e
            .Join(scope.From("nums"), (t, c) => t.Id == c.GetInt64("n"))
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().StartWith("with nums as (");
        sql.Should().EndWith("option (maxrecursion 50)");
    }

    [Fact]
    public void UpdateJoin_SetJoinedColumn_ShouldQualifyBothSides()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, p => p.Item2.Id)
            .Where(p => p.Item1.Id == 1)
            .ToSql()
            .Should().Be("update [t1] set t1.id = t2.id from simple_entity as [t1] join simple_entity as [t2] on t1.id = t2.id where t1.id = 1");
    }

    [Fact]
    public void UpdateJoin_DmlScopeHint_ShouldApplyToTargetAndJoinedTables()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.From<IMergeEntity>()
            .WithTablesInScopeHint("nolock")
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .ToSql();

        sql.Should().Be("update [t1] set t1.name = @p0 from merge_entity with (nolock) as [t1] join merge_entity with (nolock) as [t2] on t1.id = t2.id");
        (sql.Split("with (").Length - 1).Should().Be(2);
    }

    [Fact]
    public void UpdateJoin_DmlScopeHint_ShouldMergeWithJoinTableHint()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.From<IMergeEntity>()
            .WithTablesInScopeHint("updlock")
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id, j => j.WithJoinTableHint("rowlock"))
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .ToSql();

        sql.Should().Contain("from merge_entity with (updlock) as [t1]");
        sql.Should().Contain("join merge_entity with (rowlock, updlock) as [t2]");
        (sql.Split("with (").Length - 1).Should().Be(2);
    }

    [Fact]
    public void UpdateJoin_DmlScopeHint_SameContextRepeatedCalls_ShouldNotLeakCachePolicy()
    {
        using var ctx = SqlServerTestContext.Create();

        // V10: on one DataContext a hinted render, an unhinted render and a hinted render must be
        // deterministic and independent (no leak between calls), and a hinted build must not flip the
        // shared command policy (repo AGENTS: never set the sticky QueryCommand.Cache = false).
        string Render(bool hinted)
        {
            var query = ctx.From<IMergeEntity>();
            if (hinted)
                query = query.WithTablesInScopeHint("nolock");

            return query
                .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, "x")
                .ToSql();
        }

        var hintedFirst = Render(true);
        var unhinted = Render(false);
        var hintedAgain = Render(true);

        hintedFirst.Should().Be("update [t1] set t1.name = @p0 from merge_entity with (nolock) as [t1] join merge_entity with (nolock) as [t2] on t1.id = t2.id");
        hintedAgain.Should().Be(hintedFirst, "repeated hinted calls on the same context must produce identical SQL");
        unhinted.Should().Be("update [t1] set t1.name = @p0 from merge_entity as [t1] join merge_entity as [t2] on t1.id = t2.id");
        unhinted.Should().NotContain("with (");

        // The hinted command's own source command must keep its cache policy enabled (Cache == !_dontCache).
        var command = ctx.From<IMergeEntity>()
            .WithTablesInScopeHint("nolock")
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .BuildCommand();
        command.Source.Cache.Should().BeTrue("a hinted DML render must not set the sticky _dontCache flag");
    }
}
