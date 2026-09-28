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
