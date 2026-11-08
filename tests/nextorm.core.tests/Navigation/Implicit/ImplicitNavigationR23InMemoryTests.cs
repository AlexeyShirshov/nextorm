using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B R2.3 (in-memory): whole-reference projection returns the matched principal entity or
/// <c>null</c> for a dangling/null foreign key, resolved through the authoritative metadata binding —
/// never by reading the receiver's populated navigation graph. Also pins the absent-collection
/// projection (empty) and the multi-hop reference chain absence propagation.
/// </summary>
[Collection("Query cache controls")]
public class ImplicitNavigationR23InMemoryTests
{
    public ImplicitNavigationR23InMemoryTests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext Create()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R23Node>(b => b.HasOne(n => n.Parent, n => n.ParentId));
        ctx.From<R23Node>().WithData([
            new R23Node { Id = 1, ParentId = 0, Name = "root" },
            new R23Node { Id = 2, ParentId = 1, Name = "mid" },
            new R23Node { Id = 3, ParentId = 2, Name = "leaf" },
            new R23Node { Id = 4, ParentId = 999, Name = "dangling" },
            new R23Node { Id = 5, ParentId = 4, Name = "orphan-chain" },
        ]);
        return ctx;
    }

    // Seed an inconsistent CLR graph: the navigation property is populated even where the foreign key
    // is dangling (row 7) and points at the wrong entity where the foreign key is valid (row 6). The
    // FK/metadata binding must win; the CLR graph must never be read.
    private static InMemoryDataContext CreateInconsistentGraph()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R23Node>(b => b.HasOne(n => n.Parent, n => n.ParentId));
        ctx.From<R23Node>().WithData([
            new R23Node { Id = 1, ParentId = 0, Name = "root" },
            new R23Node { Id = 6, ParentId = 1, Name = "valid-fk", Parent = new R23Node { Id = 777, Name = "fabricated" } },
            new R23Node { Id = 7, ParentId = 999, Name = "dangling-fk", Parent = new R23Node { Id = 1, Name = "populated-but-wrong" } },
        ]);
        return ctx;
    }

    [Fact]
    public void Whole_reference_projection_should_return_the_matched_entity_or_null()
    {
        using var ctx = Create();

        var rows = ctx.From<R23Node>()
            .OrderBy(n => n.Id)
            .Select(n => new { n.Id, Parent = n.Parent })
            .ToList();

        var present = rows.Single(r => r.Id == 2);
        present.Parent.Should().NotBeNull("the foreign key matches a principal row");
        present.Parent!.Id.Should().Be(1);
        present.Parent.Name.Should().Be("root");

        rows.Single(r => r.Id == 4).Parent.Should().BeNull("a dangling foreign key materializes the absent reference as null");
    }

    [Fact]
    public void Whole_reference_projection_should_use_the_foreign_key_and_never_the_clr_graph()
    {
        using var ctx = CreateInconsistentGraph();

        var rows = ctx.From<R23Node>()
            .OrderBy(n => n.Id)
            .Select(n => new { n.Id, Parent = n.Parent })
            .ToList();

        // FK = 1 wins over the fabricated Parent (Id 777) seeded on the receiver.
        rows.Single(r => r.Id == 6).Parent!.Id.Should().Be(1, "the foreign-key/metadata binding is authoritative");
        // Dangling FK 999 yields null even though the receiver carries a populated Parent.
        rows.Single(r => r.Id == 7).Parent.Should().BeNull("a dangling foreign key is absent regardless of the CLR graph");
    }

    [Fact]
    public void Absent_collection_projection_should_be_empty()
    {
        using var ctx = CreateCollection();

        var rows = ctx.From<R23Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
            .ToList();

        var empty = rows.Single(r => r.Id == 2);
        empty.Has.Should().BeFalse();
        empty.C.Should().Be(0);
        empty.L.Should().Be(0L);
        empty.P.Should().Be(0);
    }

    [Fact]
    public void Two_hop_reference_chain_should_propagate_absence_at_every_hop()
    {
        using var ctx = Create();

        var rows = ctx.From<R23Node>()
            .OrderBy(n => n.Id)
            .Select(n => new { n.Id, GrandParent = n.Parent!.Parent!.Name, GrandParentId = (int?)n.Parent!.Parent!.Id })
            .ToList();

        rows.Single(r => r.Id == 3).GrandParent.Should().Be("root");
        rows.Single(r => r.Id == 3).GrandParentId.Should().Be(1);

        // Id 2: Parent -> 1 (root); root has no principal (ParentId 0) -> the final hop is absent.
        rows.Single(r => r.Id == 2).GrandParent.Should().BeNull();
        rows.Single(r => r.Id == 2).GrandParentId.Should().BeNull();

        // Id 4: intermediate (Parent) is dangling -> absent at the first hop.
        rows.Single(r => r.Id == 4).GrandParent.Should().BeNull();

        // Id 5: intermediate exists (4) but its next foreign key (999) dangles -> absent at the second hop.
        rows.Single(r => r.Id == 5).GrandParent.Should().BeNull();
    }

    [Fact]
    public void Whole_reference_and_chain_should_be_stable_across_cold_and_warm_runs()
    {
        using var ctx = Create();

        for (var run = 0; run < 2; run++)
        {
            var rows = ctx.From<R23Node>()
                .OrderBy(n => n.Id)
                .Select(n => new { n.Id, Parent = n.Parent, GrandParent = n.Parent!.Parent!.Name })
                .ToList();

            rows.Single(r => r.Id == 3).Parent!.Id.Should().Be(2);
            rows.Single(r => r.Id == 3).GrandParent.Should().Be("root");
            rows.Single(r => r.Id == 4).Parent.Should().BeNull();
            rows.Single(r => r.Id == 4).GrandParent.Should().BeNull();
        }
    }

    private static InMemoryDataContext CreateCollection()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R23Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<R23Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<R23Parent>().WithData([new R23Parent { Id = 1 }, new R23Parent { Id = 2 }]);
        ctx.From<R23Child>().WithData([new R23Child { Id = 10, ParentId = 1 }]);
        return ctx;
    }
}

[SqlTable("r23_node")]
public sealed class R23Node
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    public R23Node? Parent { get; set; }
}

[SqlTable("r23_parent")]
public sealed class R23Parent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    public ICollection<R23Child> Children { get; set; } = new List<R23Child>();
}

[SqlTable("r23_child")]
public sealed class R23Child
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    public R23Parent? Parent { get; set; }
}
