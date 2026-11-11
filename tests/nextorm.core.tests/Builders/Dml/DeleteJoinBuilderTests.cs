using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation of the multi-table delete terminals. Native SQL generation is covered by the
/// provider test projects; here we check the provider-independent gates and that every join arity is wired.
/// </summary>
public class DeleteJoinBuilderTests
{
    [Fact]
    public void InMemory_DeleteJoin_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Delete();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }

    [Fact]
    public async Task InMemory_DeleteJoinAsync_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .DeleteAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }

    [Fact]
    public void OuterJoin_Delete_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .LeftJoin(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Delete();

        act.Should().Throw<NotSupportedException>().WithMessage("*only supports INNER joins*");
    }

    [Fact]
    public void FourTableJoin_Delete_ShouldReachTheGate()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, c) => p.Item2.Id == c.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, d) => p.Item3.Id == d.Id)
            .Delete();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }

    [Fact]
    public void FiveTableJoin_Delete_ShouldReachTheGate()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, c) => p.Item2.Id == c.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, d) => p.Item3.Id == d.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, e) => p.Item4.Id == e.Id)
            .Delete();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }

    [Fact]
    public void SixTableJoin_Delete_ShouldReachTheGate()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, c) => p.Item2.Id == c.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, d) => p.Item3.Id == d.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, e) => p.Item4.Id == e.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, f) => p.Item5.Id == f.Id)
            .Delete();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }

    [Fact]
    public void SevenTableJoin_ToSql_ShouldReachTheGate()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, c) => p.Item2.Id == c.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, d) => p.Item3.Id == d.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, e) => p.Item4.Id == e.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, f) => p.Item5.Id == f.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, g) => p.Item6.Id == g.Id)
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }

    [Fact]
    public void EightTableJoin_ToSql_ShouldReachTheGate()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, c) => p.Item2.Id == c.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, d) => p.Item3.Id == d.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, e) => p.Item4.Id == e.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, f) => p.Item5.Id == f.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, g) => p.Item6.Id == g.Id)
            .Join(ctx.From<ConventionalEntity>(), (p, h) => p.Item7.Id == h.Id)
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support deleting from a joined table*");
    }
}
