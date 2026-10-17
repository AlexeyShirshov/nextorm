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

        ctx.CreateDeleteBuilder<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("alter table merge_entity delete where id = 1 settings mutations_sync = 1");
    }

    [Fact]
    public void Delete_All_ShouldRenderMutation()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.CreateDeleteBuilder<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("alter table merge_entity delete where 1 settings mutations_sync = 1");
    }

    [Fact]
    public void Truncate_ShouldRenderTruncateTable()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.CreateTruncateBuilder<IMergeEntity>().ToSql().Should().Be("truncate table merge_entity");
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

    [Fact]
    public void DeleteJoin_DmlScopeHint_ShouldThrowBecauseMutationCannotJoin()
    {
        using var ctx = ClickHouseTestContext.Create();

        // ClickHouse has no joined DML at all, so the capability rejection dominates any hint handling;
        // the scope hint must not turn the failure into a silent render or a different diagnostic.
        var act = () => ctx.From<IComplexEntity>()
            .WithTablesInScopeHint("x")
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }
}
