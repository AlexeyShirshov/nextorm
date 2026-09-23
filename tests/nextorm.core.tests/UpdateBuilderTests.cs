using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation of the update terminal. The in-memory context is query-only, so executing an
/// update on it throws; no database is involved.
/// </summary>
public class UpdateBuilderTests
{
    [Fact]
    public void InMemory_ToSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var toSql = () => ctx.Update<ConventionalEntity>().Set(x => x.Name, "a").Where(x => x.Id == 1).ToSql();

        toSql.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_Update_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Update<ConventionalEntity>().Set(x => x.Name, "a").Where(x => x.Id == 1).Update();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task InMemory_UpdateAsync_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Update<ConventionalEntity>().Set(x => x.Name, "a").Where(x => x.Id == 1).UpdateAsync();

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void Update_EntityWithoutKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Update(new NoKeyEntity { Name = "a" });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InMemory_Returning_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Update<ConventionalEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToList();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_UpdateJoin_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "a")
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
