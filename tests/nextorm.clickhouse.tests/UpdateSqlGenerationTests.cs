using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse updates through its synchronous <c>ALTER TABLE ... UPDATE</c> mutation (with
/// <c>SETTINGS mutations_sync = 1</c>), so the update builder renders that form. The mutation reports
/// no affected-row count, and it has no <c>RETURNING</c> form.
/// </summary>
public class UpdateSqlGenerationTests
{
    [Fact]
    public void Update_Where_ShouldRenderMutation()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("alter table merge_entity update name = @p0 where id = 1 settings mutations_sync = 1");
    }

    [Fact]
    public void Update_WithoutWhere_ShouldRenderTriviallyTrueFilter()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .ToSql()
            .Should().Be("alter table merge_entity update name = @p0 where 1 settings mutations_sync = 1");
    }

    [Fact]
    public void Update_SetColumnAndExpression_ShouldRenderAssignments()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Age, x => x.Age + 1)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("alter table merge_entity update age = (age + 1) where id = 1 settings mutations_sync = 1");
    }

    [Fact]
    public void Update_Returning_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Set(x => x.Name, "a").Where(x => x.Id == 1).Returning().ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UpdateJoin_ShouldThrowBecauseMutationCannotJoin()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "x")
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support updating from a joined table*");
    }
}
