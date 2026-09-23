using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// SQL generation of the update builder on MariaDB (no database connection): the <c>SET</c> list and the
/// <c>WHERE</c> filter.
/// </summary>
public class UpdateSqlGenerationTests
{
    [Fact]
    public void Update_SetConstants_ShouldRenderSetAndWhere()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Set(x => x.Age, 5)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set name = @p0, age = @p1 where id = 1");
    }

    [Fact]
    public void Update_SetExpression_ShouldRenderArithmetic()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Age, x => x.Age + 1)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set age = (age + 1) where id = 1");
    }

    [Fact]
    public void Update_WithoutWhere_ShouldUpdateAllRows()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .ToSql()
            .Should().Be("update merge_entity set name = @p0");
    }

    [Fact]
    public void Update_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = MariaDbTestContext.CreateQuoted();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update `merge_entity` set `name` = @p0 where `id` = 1");
    }

    [Fact]
    public void Update_ComputedColumn_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Set(x => x.Total, 1).Where(x => x.Id == 1).ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Update_WithoutAssignment_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Where(x => x.Id == 1).ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Update_Returning_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Set(x => x.Name, "a").Where(x => x.Id == 1).Returning().ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
