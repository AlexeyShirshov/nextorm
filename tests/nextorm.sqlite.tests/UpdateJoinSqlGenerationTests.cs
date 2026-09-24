using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation of the multi-table <c>UPDATE ... FROM</c> builder on SQLite (3.33+), which follows the
/// PostgreSQL spelling: the target stays out of the <c>FROM</c> list and the join conditions are folded
/// into the <c>WHERE</c>.
/// </summary>
public class UpdateJoinSqlGenerationTests
{
    [Fact]
    public void UpdateJoin_SetConstant_ShouldRenderFromAndFoldJoin()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .ToSql()
            .Should().Be("update merge_entity as 't1' set name = $p0 from merge_entity as 't2' where t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_FromCte_ShouldHoistWith()
    {
        using var ctx = SqliteTestContext.Create();

        var e = ctx.From<ISimpleEntity>();
        var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));

        var sql = e
            .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
            .UpdateJoin()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().StartWith("with c as (select id from simple_entity");
        sql.Should().Contain("update simple_entity as 't1' set id = $p0 from c as 't2' where t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_CteOnSecondJoin_ShouldHoistWith()
    {
        using var ctx = SqliteTestContext.Create();

        var e = ctx.From<ISimpleEntity>();
        var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));

        var sql = e
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .Join(scope.From("c"), (p, c) => p.Item2.Id == c["id"].AsInt)
            .UpdateJoin()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().StartWith("with c as (select id from simple_entity");
        sql.Should().Contain("from simple_entity as 't2', c as 't3'");
    }

    [Fact]
    public void UpdateJoin_SetJoinedColumn_ShouldQualifyBothSides()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Id, p => p.Item2.Id)
            .Where(p => p.Item1.Id == 1)
            .ToSql()
            .Should().Be("update simple_entity as 't1' set id = t2.id from simple_entity as 't2' where t1.id = t2.id and t1.id = 1");
    }
}
