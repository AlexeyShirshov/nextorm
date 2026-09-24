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
            .UpdateJoin()
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
            .UpdateJoin()
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
            .UpdateJoin()
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
            .UpdateJoin()
            .Set(p => p.Item1.Id, p => p.Item2.Id)
            .Where(p => p.Item1.Id == 1)
            .ToSql()
            .Should().Be("update [t1] set t1.id = t2.id from simple_entity as [t1] join simple_entity as [t2] on t1.id = t2.id where t1.id = 1");
    }
}
