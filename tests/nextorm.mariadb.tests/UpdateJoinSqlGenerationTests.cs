using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// SQL generation of the multi-table <c>UPDATE ... JOIN ... SET</c> builder on MariaDB (inherited from the
/// MySQL dialect): the join specification sits between <c>UPDATE</c> and <c>SET</c>.
/// </summary>
public class UpdateJoinSqlGenerationTests
{
    [Fact]
    public void UpdateJoin_SetConstant_ShouldRenderJoinBeforeSet()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .ToSql()
            .Should().Be("update merge_entity as `t1` join merge_entity as `t2` on t1.id = t2.id set t1.name = @p0");
    }

    [Fact]
    public void UpdateJoin_SetJoinedColumn_ShouldQualifyBothSides()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Id, p => p.Item2.Id)
            .Where(p => p.Item1.Id == 1)
            .ToSql()
            .Should().Be("update simple_entity as `t1` join simple_entity as `t2` on t1.id = t2.id set t1.id = t2.id where t1.id = 1");
    }
}
