using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation of the delete and truncate terminals. The in-memory context is query-only,
/// so executing a delete or truncate on it throws; no database is involved.
/// </summary>
public class DeleteBuilderTests
{
    [Fact]
    public void InMemory_ToSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var toSql = () => ctx.DeleteFrom<ConventionalEntity>().Where(x => x.Id == 1).ToSql();

        toSql.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_Delete_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.DeleteFrom<ConventionalEntity>().Where(x => x.Id == 1).Delete();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task InMemory_DeleteAsync_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.DeleteFrom<ConventionalEntity>().Where(x => x.Id == 1).DeleteAsync();

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void InMemory_Truncate_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Truncate<ConventionalEntity>().Execute();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Delete_WithoutFilter_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.DeleteFrom<ConventionalEntity>().Delete();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_EntityWithoutKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Delete(new NoKeyEntity { Name = "a" });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void All_AfterWhere_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.DeleteFrom<ConventionalEntity>().Where(x => x.Id == 1).All();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Where_AfterAll_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.DeleteFrom<ConventionalEntity>().All().Where(x => x.Id == 1);

        act.Should().Throw<InvalidOperationException>();
    }
}
