using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>SQL generation of the delete builder on SQLite (no database connection).</summary>
public class DeleteSqlGenerationTests
{
    [Fact]
    public void Delete_Where_ShouldRenderPredicate()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1");
    }

    [Fact]
    public void Delete_All_ShouldRenderNoWhere()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("delete from merge_entity");
    }

    [Fact]
    public void Delete_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = SqliteTestContext.CreateQuoted();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from \"merge_entity\" where \"id\" = 1");
    }

    [Fact]
    public void Delete_ReturningEntity_ShouldRenderReturning()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1 returning id, name, age, total");
    }

    [Fact]
    public void Delete_ReturningProjection_ShouldRenderSelectedColumns()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1 returning id, name");
    }

    [Fact]
    public void Truncate_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.Truncate<IMergeEntity>().ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DeleteJoin_ShouldThrowBecauseNoMultiTableDelete()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }
}
