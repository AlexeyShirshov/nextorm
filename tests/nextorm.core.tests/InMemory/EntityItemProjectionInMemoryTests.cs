using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory parity for whole-entity projection items: the same joined shape the SQL provider
/// materializes from flattened columns is materialized from the in-memory projection objects, and the
/// outer-join null child matches.
/// </summary>
public class EntityItemProjectionInMemoryTests
{
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<MemoryItemParent>().WithData([new MemoryItemParent { Id = 1, Name = "p1" }, new MemoryItemParent { Id = 2, Name = "p2" }]);
        ctx.From<MemoryItemChild>().WithData([new MemoryItemChild { Id = 10, ParentId = 1, Name = "c1" }, new MemoryItemChild { Id = 11, ParentId = 1, Name = "c2" }]);
        return ctx;
    }

    [Fact]
    public void OuterJoin_EntityItems_ShouldMatchSqlSemantics()
    {
        using var ctx = CreateContext();

        var rows = ctx.From<MemoryItemParent>()
            .LeftJoin(ctx.From<MemoryItemChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => new { Left = p.Item1, Right = p.Item2 })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Count(r => r.Right is null).Should().Be(1);
        var childless = rows.Single(r => r.Right is null);
        childless.Left.Id.Should().Be(2);
        rows.Where(r => r.Right is not null).Select(r => r.Right!.Id).OrderBy(x => x).Should().Equal(10, 11);
    }

    [Fact]
    public void BareProjection_ShouldMatchSqlSemantics()
    {
        using var ctx = CreateContext();

        var rows = ctx.From<MemoryItemParent>()
            .LeftJoin(ctx.From<MemoryItemChild>(), (p, c) => p.Id == c.ParentId)
            .ToList();

        rows.Should().HaveCount(3);
        var childless = rows.Single(r => r.Item1.Id == 2);
        childless.Item2.Should().BeNull();
        rows.Single(r => r.Item2 is { Id: 10 }).Item1.Id.Should().Be(1);
    }

    [Fact]
    public void DirectEntityItem_InnerJoin_ShouldMaterializeSelectedEntity()
    {
        using var ctx = CreateContext();

        var children = ctx.From<MemoryItemParent>()
            .Join(ctx.From<MemoryItemChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2)
            .ToList();

        children.Should().HaveCount(2);
        children.Should().OnlyContain(c => c.ParentId == 1);
        children.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
    }

    [Fact]
    public void DirectEntityItem_OuterJoin_ShouldReturnNullForMissingSide()
    {
        using var ctx = CreateContext();

        var children = ctx.From<MemoryItemParent>()
            .LeftJoin(ctx.From<MemoryItemChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2)
            .ToList();

        children.Should().HaveCount(3);
        children.Count(c => c is null).Should().Be(1, "the missing outer-join side must materialize as null");
        children.Where(c => c is not null).Select(c => c!.Id).OrderBy(x => x).Should().Equal(10, 11);
    }

    [Fact]
    public void DirectEntityItem_FirstSlot_ShouldMaterializeParent()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<MemoryItemParent>()
            .LeftJoin(ctx.From<MemoryItemChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item1)
            .ToList();

        parents.Should().HaveCount(3);
        parents.Select(p => p.Id).OrderBy(x => x).Should().Equal(1, 1, 2);
        parents.Select(p => p.Name).OrderBy(x => x).Should().Equal("p1", "p1", "p2");
    }

    [Fact]
    public void DirectEntityItem_PresentAllDefaultEntity_ShouldNotBeNull()
    {
        // The in-memory source carries the entity object itself, so a matched entity whose scalar
        // values are all default (0/0/null) must materialize as that object, never as null.
        using var ctx = new InMemoryDataContext();
        ctx.From<MemoryItemParent>().WithData([new MemoryItemParent { Id = 0, Name = null }]);
        ctx.From<MemoryItemChild>().WithData([new MemoryItemChild { Id = 0, ParentId = 0, Name = null }]);

        var children = ctx.From<MemoryItemParent>()
            .Join(ctx.From<MemoryItemChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2)
            .ToList();

        children.Should().HaveCount(1);
        children[0].Should().NotBeNull("a present entity with all-default scalars must not be nulled");
        children[0].Id.Should().Be(0);
    }

    [Fact]
    public void DirectEntityItem_SelfJoin_ShouldMaterializeBothSlots()
    {
        using var ctx = CreateContext();

        var first = ctx.From<MemoryItemChild>()
            .Join(ctx.From<MemoryItemChild>(), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1)
            .ToList();
        var second = ctx.From<MemoryItemChild>()
            .Join(ctx.From<MemoryItemChild>(), (a, b) => a.Id == b.Id)
            .Select(p => p.Item2)
            .ToList();

        first.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
        second.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
        first.Select(c => c.Name).OrderBy(x => x).Should().Equal("c1", "c2");
        second.Select(c => c.Name).OrderBy(x => x).Should().Equal("c1", "c2");
    }
}

public sealed class MemoryItemParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ICollection<MemoryItemChild> Children { get; } = new List<MemoryItemChild>();
}

public sealed class MemoryItemChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}
