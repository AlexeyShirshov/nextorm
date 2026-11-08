using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B R2.2 (in-memory): single-hop self-reference and dual same-typed reference paths lower through
/// the metadata-based correlated path and stay distinct. As of R2.3 a multi-hop reference chain lowers
/// through the in-memory navigation-chain walk and propagates absence at every hop.
/// </summary>
[Collection("Query cache controls")]
public class ImplicitNavigationR22InMemoryTests
{
    public ImplicitNavigationR22InMemoryTests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext CreateSelf()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R22Node>(b => b.HasOne(n => n.Parent, n => n.ParentId));
        ctx.From<R22Node>().WithData([
            new R22Node { Id = 1, ParentId = 0, Name = "root" },
            new R22Node { Id = 2, ParentId = 1, Name = "mid" },
            new R22Node { Id = 3, ParentId = 2, Name = "leaf" },
            new R22Node { Id = 4, ParentId = 999, Name = "dangling" },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateDual()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R22Node>();
        ctx.From<R22Pair>(b =>
        {
            b.HasOne(p => p.Left, p => p.LeftId);
            b.HasOne(p => p.Right, p => p.RightId);
        });
        ctx.From<R22Node>().WithData([
            new R22Node { Id = 1, Name = "left-node" },
            new R22Node { Id = 2, Name = "right-node" },
        ]);
        ctx.From<R22Pair>().WithData([new R22Pair { Id = 1, LeftId = 1, RightId = 2 }]);
        return ctx;
    }

    [Fact]
    public void Self_reference_single_hop_should_read_the_parent_and_dangling_as_null()
    {
        using var ctx = CreateSelf();

        var rows = ctx.From<R22Node>().OrderBy(n => n.Id)
            .Select(n => new { n.Id, Parent = n.Parent!.Name })
            .ToList();

        rows.Single(r => r.Id == 1).Parent.Should().BeNull();
        rows.Single(r => r.Id == 2).Parent.Should().Be("root");
        rows.Single(r => r.Id == 3).Parent.Should().Be("mid");
        rows.Single(r => r.Id == 4).Parent.Should().BeNull();
    }

    [Fact]
    public void Two_same_typed_reference_paths_should_read_distinct_values()
    {
        using var ctx = CreateDual();

        var row = ctx.From<R22Pair>()
            .Select(p => new { p.Id, Left = p.Left!.Name, Right = p.Right!.Name })
            .ToList()
            .Single();

        row.Left.Should().Be("left-node");
        row.Right.Should().Be("right-node");
    }

    // #148-B R2.3: the two-hop chain now walks the metadata binding hop by hop and propagates absence
    // at every hop; the previous fail-closed shape is superseded.
    [Fact]
    public void Multi_hop_reference_chain_should_chain_and_propagate_absence()
    {
        using var ctx = CreateSelf();

        var rows = ctx.From<R22Node>().OrderBy(n => n.Id)
            .Select(n => new { n.Id, GrandParent = n.Parent!.Parent!.Name })
            .ToList();

        rows.Single(r => r.Id == 1).GrandParent.Should().BeNull("the first hop is absent");
        rows.Single(r => r.Id == 2).GrandParent.Should().BeNull("the second hop is absent");
        rows.Single(r => r.Id == 3).GrandParent.Should().Be("root");
        rows.Single(r => r.Id == 4).GrandParent.Should().BeNull("the first hop dangles");
    }

    // #148-B R2.2: the occurrence-parameter binding must respect the source scope. A source registered
    // by exact parameter reference below the current SourceScopeStart (a popped/nested scope) must not
    // satisfy a non-outer lookup, otherwise a stale same-typed navigation join would silently resolve
    // to a sibling alias. Direct guard pin (mutation M04).
    [Fact]
    public void Out_of_scope_occurrence_parameter_should_not_resolve_to_an_alias()
    {
        var provider = new DefaultColumnsProvider();
        var occurrence = System.Linq.Expressions.Expression.Parameter(typeof(R22Node), "nav");

        provider.PushSourceScope();
        provider.Add(typeof(R22Node), fromProjection: false, sourceParameter: occurrence);

        // Open a nested source scope: `occurrence` now sits below SourceScopeStart.
        provider.PushSourceScope();

        provider.FindAlias(occurrence, fromProjection: false).Should().BeNull();
        provider.FindAlias(occurrence, fromProjection: false, includeOuterScopes: true).Should().Be(0);
    }
}

[SqlTable("r22_node")]
public sealed class R22Node
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    public R22Node? Parent { get; set; }
}

[SqlTable("r22_pair")]
public sealed class R22Pair
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("left_id")]
    public int LeftId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("right_id")]
    public int RightId { get; set; }

    public R22Node? Left { get; set; }

    public R22Node? Right { get; set; }
}
