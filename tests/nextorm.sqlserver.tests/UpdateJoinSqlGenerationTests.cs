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
