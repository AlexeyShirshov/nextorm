using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of the update builder on PostgreSQL (no database connection): the <c>SET</c> list
/// (parameterised constants, column references and expressions) and the <c>WHERE</c> filter.
/// </summary>
public class UpdateSqlGenerationTests
{
    [Fact]
    public void Update_SetConstants_ShouldRenderSetAndWhere()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Set(x => x.Age, 5)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set name = @p0, age = @p1 where id = 1");
    }

    [Fact]
    public void Update_SetCapturedValue_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var name = "a";

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, name)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 where id = 1");
    }

    [Fact]
    public void Update_SetCapturedProperty_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var holder = new MergeEntity { Name = "h" };

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, x => holder.Name)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 where id = 1");
    }

    [Fact]
    public void Update_SetColumn_ShouldRenderColumnReference()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Age, x => x.Total)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set age = total where id = 1");
    }

    [Fact]
    public void Update_SetExpression_ShouldRenderArithmetic()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Age, x => x.Age + 1)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set age = (age + 1) where id = 1");
    }

    [Fact]
    public void Update_SetExpressionCapturedValue_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var increment = 2;

        ctx.Update<IMergeEntity>()
            .Set(x => x.Age, x => x.Age + increment)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update merge_entity set age = (age + @increment) where id = 1");
    }

    [Fact]
    public void Update_WithoutWhere_ShouldUpdateAllRows()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .ToSql()
            .Should().Be("update merge_entity set name = @p0");
    }

    [Fact]
    public void Update_ByEntitySql_ShouldRenderKeyPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var entity = new MergeEntity { Id = 3, Name = "b", Age = 4 };

        ctx.Update<IMergeEntity>()
            .Set(entity)
            .Where(x => x.Id == entity.Id)
            .ToSql()
            .Should().Be("update merge_entity set name = @p0, age = @p1 where id = @Id");
    }

    [Fact]
    public void Update_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("update \"merge_entity\" set \"name\" = @p0 where \"id\" = 1");
    }

    [Fact]
    public void Update_ComputedColumn_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Set(x => x.Total, 1).Where(x => x.Id == 1).ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Update_ReturningEntity_ShouldRenderReturning()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 where id = 1 returning id, name, age, total");
    }

    [Fact]
    public void Update_ReturningProjection_ShouldRenderSelectedColumns()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Update<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 where id = 1 returning id, name");
    }

    [Fact]
    public void Update_WithoutAssignment_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.Update<IMergeEntity>().Where(x => x.Id == 1).ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Update_EntityWithoutKey_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.Update(new UnkeyedEntity { Name = "x" });

        act.Should().Throw<InvalidOperationException>();
    }
}
