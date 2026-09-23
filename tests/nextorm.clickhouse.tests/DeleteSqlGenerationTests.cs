using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse deletes through its <c>ALTER TABLE ... DELETE</c> mutation (with
/// <c>SETTINGS mutations_sync = 1</c>), so the delete builder renders that form.
/// </summary>
public class DeleteSqlGenerationTests
{
    [Fact]
    public void Delete_Where_ShouldRenderMutation()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("alter table merge_entity delete where id = 1 settings mutations_sync = 1");
    }

    [Fact]
    public void Delete_All_ShouldRenderMutation()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("alter table merge_entity delete where 1 settings mutations_sync = 1");
    }

    [Fact]
    public void Truncate_ShouldRenderTruncateTable()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.Truncate<IMergeEntity>().ToSql().Should().Be("truncate table merge_entity");
    }

    [Fact]
    public void DeleteJoin_ShouldThrowBecauseMutationCannotJoin()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }
}
