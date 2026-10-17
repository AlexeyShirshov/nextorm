using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B D-R3-4/5/6 (in-memory): the three previously fail-closed shapes —
/// (a) multi-hop reference presence (<c>a.Parent.Parent == null</c> / <c>!= null</c>) with absence at
/// the first, middle or final hop;
/// (b) a collection reached through a reference prefix (<c>a.Parent.Children.Count()</c>): an absent
/// reference yields an empty correlated collection and must never match a default-valued foreign key;
/// (c) the reference-navigation <c>AsEntityBuilder</c> adapter (the <c>object?</c> overload), lowered
/// with reference semantics (presence plus chain/null), never reinterpreted as a collection.
/// </summary>
[Collection("Query cache controls")]
public class ImplicitNavigationR3ResidualTests
{
    public ImplicitNavigationR3ResidualTests()
    {
        DataContextCache.Clear();
    }

    // ---- D-R3-4: multi-hop reference presence ----------------------------------------------------

    private static InMemoryDataContext CreateSelfReference()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R3Node>(b => b.HasOne(n => n.Parent, n => n.ParentId));
        ctx.From<R3Node>().WithData([
            new R3Node { Id = 1, ParentId = 0, Name = "root" },       // first hop absent (no id 0)
            new R3Node { Id = 2, ParentId = 1, Name = "mid" },        // first present, final hop absent
            new R3Node { Id = 3, ParentId = 2, Name = "leaf" },       // both hops present
            new R3Node { Id = 4, ParentId = 999, Name = "dangling" }, // first hop dangles
            new R3Node { Id = 5, ParentId = 4, Name = "orphan" },     // middle present, final dangles
        ]);
        return ctx;
    }

    [Fact]
    public void Multi_hop_reference_presence_should_be_consistent_and_cover_first_middle_final_absence()
    {
        using var ctx = CreateSelfReference();

        var rows = ctx.From<R3Node>().OrderBy(n => n.Id)
            .Select(n => new { n.Id, Absent = n.Parent!.Parent == null, Present = n.Parent!.Parent != null })
            .ToList();

        rows.Should().OnlyContain(r => r.Absent != r.Present, "== null and != null must be mutually consistent");

        rows.Single(r => r.Id == 1).Absent.Should().BeTrue("the first hop is absent");
        rows.Single(r => r.Id == 2).Absent.Should().BeTrue("the final (second) hop is absent");
        rows.Single(r => r.Id == 4).Absent.Should().BeTrue("the first hop dangles");
        rows.Single(r => r.Id == 5).Absent.Should().BeTrue("the middle hop exists but the final hop dangles");

        var present = rows.Single(r => r.Id == 3);
        present.Absent.Should().BeFalse("both hops resolve to a principal row");
        present.Present.Should().BeTrue();
    }

    [Fact]
    public void Multi_hop_reference_presence_predicates_should_filter_both_directions()
    {
        using var ctx = CreateSelfReference();

        ctx.From<R3Node>().Where(n => n.Parent!.Parent == null).Select(n => n.Id).ToList()
            .Should().BeEquivalentTo(new[] { 1, 2, 4, 5 });

        ctx.From<R3Node>().Where(n => n.Parent!.Parent != null).Select(n => n.Id).ToList()
            .Should().BeEquivalentTo(new[] { 3 });
    }

    // ---- D-R3-5: collection reached through a reference prefix -----------------------------------

    private static InMemoryDataContext CreateReferenceToCollection()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R3Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<R3Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<R3Parent>().WithData([
            new R3Parent { Id = 1, Name = "p1" },
            new R3Parent { Id = 2, Name = "p2" },
        ]);
        ctx.From<R3Child>().WithData([
            new R3Child { Id = 10, ParentId = 1 },
            new R3Child { Id = 11, ParentId = 1 },
            new R3Child { Id = 12, ParentId = 2 },
            new R3Child { Id = 13, ParentId = 0 },   // default-valued foreign key: no principal 0 exists
            new R3Child { Id = 14, ParentId = 999 }, // dangling foreign key
        ]);
        return ctx;
    }

    [Fact]
    public void Collection_through_a_reference_prefix_should_report_the_principals_children()
    {
        using var ctx = CreateReferenceToCollection();

        var rows = ctx.From<R3Child>().OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                Has = c.Parent!.Children.Any(),
                C = c.Parent!.Children.Count(),
                L = c.Parent!.Children.LongCount(),
                P = c.Parent!.Children.Count,
            })
            .ToList();

        var two = rows.Single(r => r.Id == 10);
        two.Has.Should().BeTrue();
        two.C.Should().Be(2);
        two.L.Should().Be(2L);
        two.P.Should().Be(2);

        var one = rows.Single(r => r.Id == 12);
        one.C.Should().Be(1);
        one.L.Should().Be(1L);

        // An absent reference yields an empty correlated collection: no throw, and never a phantom
        // match against the default-valued foreign key (id 13 itself carries ParentId 0).
        foreach (var id in new[] { 13, 14 })
        {
            var absent = rows.Single(r => r.Id == id);
            absent.Has.Should().BeFalse();
            absent.C.Should().Be(0);
            absent.L.Should().Be(0L);
            absent.P.Should().Be(0);
        }
    }

    [Fact]
    public void Collection_through_a_reference_prefix_predicate_should_not_match_a_default_key()
    {
        using var ctx = CreateReferenceToCollection();

        var ids = ctx.From<R3Child>()
            .Where(c => c.Parent!.Children.Any())
            .Select(c => c.Id)
            .ToList();

        ids.Should().BeEquivalentTo(new[] { 10, 11, 12 }, "an absent reference must not match a default-valued foreign key");
    }

    // ---- D-R3-6: reference adapter ---------------------------------------------------------------

    [Fact]
    public void Reference_adapter_terminal_should_use_reference_semantics_and_chain_null_behaviour()
    {
        using var ctx = CreateReferenceToCollection();

        var rows = ctx.From<R3Child>().OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                Has = c.Parent!.AsEntityBuilder<R3Parent>().Any(),
                C = c.Parent!.AsEntityBuilder<R3Parent>().Count(),
                L = c.Parent!.AsEntityBuilder<R3Parent>().LongCount(),
            })
            .ToList();

        // Parent 1 has two children, so a collection reinterpretation would report 2; the reference
        // adapter must report the single related principal (1), proving it is not a collection.
        var present = rows.Single(r => r.Id == 10);
        present.Has.Should().BeTrue();
        present.C.Should().Be(1);
        present.L.Should().Be(1L);

        foreach (var id in new[] { 13, 14 })
        {
            var absent = rows.Single(r => r.Id == id);
            absent.Has.Should().BeFalse();
            absent.C.Should().Be(0);
            absent.L.Should().Be(0L);
        }
    }

    [Fact]
    public void Reference_adapter_on_a_multi_hop_reference_should_propagate_absence()
    {
        using var ctx = CreateSelfReference();

        var rows = ctx.From<R3Node>().OrderBy(n => n.Id)
            .Select(n => new { n.Id, Has = n.Parent!.Parent!.AsEntityBuilder<R3Node>().Any() })
            .ToList();

        rows.Single(r => r.Id == 3).Has.Should().BeTrue("both hops resolve");
        foreach (var id in new[] { 1, 2, 4, 5 })
            rows.Single(r => r.Id == id).Has.Should().BeFalse("any absent hop makes the reference adapter absent");
    }
}

[SqlTable("r3_node")]
public sealed class R3Node
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    public R3Node? Parent { get; set; }
}

[SqlTable("r3_parent")]
public sealed class R3Parent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    public ICollection<R3Child> Children { get; set; } = new List<R3Child>();
}

[SqlTable("r3_child")]
public sealed class R3Child
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    public R3Parent? Parent { get; set; }
}
