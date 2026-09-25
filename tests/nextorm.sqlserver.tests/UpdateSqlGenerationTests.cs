using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL generation of the update builder on SQL Server (no database connection): the <c>SET</c> list and
/// the <c>WHERE</c> filter.
/// </summary>
public class UpdateSqlGenerationTests
{
    [Fact]
    public void Update_SetConstants_ShouldRenderSetAndWhere()
    {
        using var ctx = SqlServerTestContext.Create();

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
        using var ctx = SqlServerTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Age, x => x.Age + 1)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set age = (age + 1) where id = 1");
    }

    [Fact]
    public void Update_WithoutWhere_ShouldUpdateAllRows()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .ToSql()
            .Should().Be("update merge_entity set name = @p0");
    }

    [Fact]
    public void Update_UppercaseKeywords_ShouldCaseUpdate()
    {
        using var ctx = SqlServerTestContext.CreateUppercase();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("UPDATE merge_entity SET name = @p0 WHERE id = 1");
    }

    [Fact]
    public void Update_ComputedColumn_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Set(x => x.Total, 1).Where(x => x.Id == 1).ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Update_WithoutAssignment_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Where(x => x.Id == 1).ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Update_ReturningEntity_ShouldRenderOutput()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 output inserted.id, inserted.name, inserted.age, inserted.total where id = 1");
    }

    [Fact]
    public void Update_ReturningProjection_ShouldRenderOutput()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 output inserted.id, inserted.name where id = 1");
    }

    [Fact]
    public void Update_OutputInto_ShouldRenderInto()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .OutputInto("audit_log")
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 output inserted.id, inserted.name into audit_log (id, name) where id = 1");
    }

    [Fact]
    public void Update_OutputIntoThenOutput_ShouldRenderBoth()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .OutputIntoThenOutput("audit_log")
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 output inserted.id, inserted.name into audit_log (id, name) output inserted.id, inserted.name where id = 1");
    }
}
