using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T212-D7 corrective: the mandatory nested eager-load variant-matrix rows the r1 evidence missed.
/// Row A (declaration frontends), row C (identity/state: nullable/composite nested keys, repeated CLR
/// type), the uncovered nested single-query rejection branch, and the deepest-node per-parent paging
/// probe. In-memory rows assert in-memory behavior; the SQL rows live in the dialect projects.
/// </summary>
[Trait("Feature", "T212-D7")]
public sealed class NestedEagerLoadingMatrixTests
{
    // ============================================================================================
    // Row C — same-CLR-type binding (warning: EntityBuilder.ResolveJoinLeftItemNumber latest-match).
    // An acyclic graph repeats a CLR type: a deeper node of the same type precedes a later sibling
    // whose direct parent is the earlier same-typed node. The join must attach to the direct parent.
    // ============================================================================================

    private sealed class RepRoot
    {
        public int Id { get; set; }
        public ICollection<RepT> Ts { get; set; } = new List<RepT>();
    }

    private sealed class RepT
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int RefId { get; set; }
        public ICollection<RepU> Us { get; set; } = new List<RepU>();
        public ICollection<RepW> Ws { get; set; } = new List<RepW>();
    }

    private sealed class RepU
    {
        public int Id { get; set; }
        public int TId { get; set; }
        public ICollection<RepT> Ts2 { get; set; } = new List<RepT>();
    }

    private sealed class RepW
    {
        public int Id { get; set; }
        public int TId { get; set; }
    }

    private static EntityBuilder<RepRoot> RepeatedTypeBuilder(InMemoryDataContext context)
        => context.From<RepRoot>()
            .LoadWith(
                r => r.Ts,
                c => c.From<RepT>()
                    .LoadWith(
                        t => t.Us,
                        u => u.From<RepU>().LoadWith(x => x.Ts2, y => y.From<RepT>(), x => x.Id, y => y.RefId),
                        t => t.Id,
                        u => u.TId)
                    .LoadWith(t => t.Ws, w => w.From<RepW>(), t => t.Id, w => w.TId),
                r => r.Id,
                t => t.RootId,
                EagerLoadMode.SingleQuery);

    [Fact]
    public void SingleQuery_RepeatedClrType_SiblingAfterDeeperSameType_AttachesToDirectParent()
    {
        using var context = new InMemoryDataContext();
        context.From<RepRoot>().WithData(new[] { new RepRoot { Id = 1 } });
        context.From<RepT>().WithData(new[]
        {
            new RepT { Id = 10, RootId = 1 },  // RepT attached to RepRoot
            new RepT { Id = 20, RefId = 100 }, // deeper repeated RepT under RepU
        });
        context.From<RepU>().WithData(new[] { new RepU { Id = 100, TId = 10 } });
        context.From<RepW>().WithData(new[] { new RepW { Id = 200, TId = 10 } });

        var roots = RepeatedTypeBuilder(context).ToList();

        var t1 = roots.Single().Ts.Single(t => t.Id == 10);
        var t2 = t1.Us.Single().Ts2.Single(t => t.Id == 20);
        t1.Ws.Select(w => w.Id).Should().Equal(new[] { 200 }, "the sibling attaches to its direct parent, not the deeper same-typed node");
        t2.Ws.Should().BeEmpty("a deeper repeated-type node must not adopt a sibling collection");
    }

    [Fact]
    public void SingleQuery_RepeatedClrType_WithDeeperSameTypeAbsent_StillAttachesSibling()
    {
        using var context = new InMemoryDataContext();
        context.From<RepRoot>().WithData(new[] { new RepRoot { Id = 1 } });
        context.From<RepT>().WithData(new[] { new RepT { Id = 10, RootId = 1 } });
        context.From<RepU>().WithData(new[] { new RepU { Id = 100, TId = 10 } });
        context.From<RepW>().WithData(new[] { new RepW { Id = 200, TId = 10 } });

        var roots = RepeatedTypeBuilder(context).ToList();

        roots.Single().Ts.Single().Ws.Select(w => w.Id)
            .Should().Equal(new[] { 200 }, "a nullable deeper same-typed node must not null-propagate away the sibling join");
    }

    // ============================================================================================
    // The uncovered nested single-query rejection branch (EntityBuilderEagerLoading.cs:360-363):
    // the child builder itself carries an unsupported shape AND declares a nested graph.
    // ============================================================================================

    private sealed class ShapeRoot
    {
        public int Id { get; set; }
        public ICollection<ShapeMid> Mids { get; set; } = new List<ShapeMid>();
    }

    private sealed class ShapeMid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int SortKey { get; set; }
        public ICollection<ShapeLeaf> Leaves { get; set; } = new List<ShapeLeaf>();
    }

    private sealed class ShapeLeaf
    {
        public int Id { get; set; }
        public int MidId { get; set; }
    }

    [Fact]
    public void SingleQuery_NestedChildWithOwnUnsupportedShape_IsRejectedFailClosed()
    {
        using var context = new InMemoryDataContext();
        context.From<ShapeRoot>().WithData(new[] { new ShapeRoot { Id = 1 } });

        // The Mid child builder declares a nested Leaves graph AND carries its own OrderBy, so the
        // nested branch (not the flat level-one fold) is the one that must reject the shape.
        var builder = context.From<ShapeRoot>()
            .LoadWith(
                r => r.Mids,
                c => c.From<ShapeMid>().OrderBy(m => m.SortKey)
                    .LoadWith(m => m.Leaves, l => l.From<ShapeLeaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery);

        var act = () => builder.ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*OrderBy*");
    }

    // ============================================================================================
    // Row C — nullable value-type nested key (no crash, no match) and composite nested key.
    // ============================================================================================

    private sealed class NullValKeyRoot
    {
        public int Id { get; set; }
        public ICollection<NullValKeyMid> Mids { get; set; } = new List<NullValKeyMid>();
    }

    private sealed class NullValKeyMid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int? Key { get; set; }
    }

    [Fact]
    public void Split_NullableValueNestedKey_IsIgnoredAndDoesNotCrash()
    {
        using var context = new InMemoryDataContext();
        context.From<NullValKeyRoot>().WithData(new[] { new NullValKeyRoot { Id = 1 } });
        context.From<NullValKeyMid>().WithData(new[]
        {
            new NullValKeyMid { Id = 10, RootId = 1, Key = 5 },
            new NullValKeyMid { Id = 11, RootId = 1, Key = null },
        });

        var roots = context.From<NullValKeyRoot>()
            .LoadWith(
                r => r.Mids,
                c => c.From<NullValKeyMid>().Where(m => m.Key != null || m.Key == null),
                r => r.Id,
                m => m.RootId)
            .ToList();

        roots.Single().Mids.Select(m => m.Id).Should().Equal(new[] { 10, 11 });
    }

    private sealed class CompositeKeyRoot
    {
        public int Id { get; set; }
        public int Region { get; set; }
        public ICollection<CompositeKeyChild> Children { get; set; } = new List<CompositeKeyChild>();
    }

    private sealed class CompositeKeyChild
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int Region { get; set; }
    }

    [Fact]
    public void Split_CompositeNestedKey_PartitionsOnEveryKeyComponent()
    {
        using var context = new InMemoryDataContext();
        context.From<CompositeKeyRoot>().WithData(new[]
        {
            new CompositeKeyRoot { Id = 1, Region = 1 },
            new CompositeKeyRoot { Id = 1, Region = 2 },
        });
        context.From<CompositeKeyChild>().WithData(new[]
        {
            new CompositeKeyChild { Id = 10, RootId = 1, Region = 1 },
            new CompositeKeyChild { Id = 20, RootId = 1, Region = 2 },
        });

        // The key is the whole (RootId, Region) tuple, so the two parents with the same id but a
        // different region must not blend their children.
        var roots = context.From<CompositeKeyRoot>()
            .LoadWith(
                r => r.Children,
                c => c.From<CompositeKeyChild>(),
                r => ValueTuple.Create(r.Id, r.Region),
                c => ValueTuple.Create(c.RootId, c.Region))
            .OrderBy(r => r.Region)
            .ToList();

        roots.Single(r => r.Region == 1).Children.Select(c => c.Id).Should().Equal(10);
        roots.Single(r => r.Region == 2).Children.Select(c => c.Id).Should().Equal(20);
    }

    // ============================================================================================
    // Warning: the deepest node (no descendants) with per-parent paging falls to the flat #211
    // lowering. On the in-memory provider it must still fail closed before any execution.
    // ============================================================================================

    private sealed class LeafPageRoot
    {
        public int Id { get; set; }
        public ICollection<LeafPageMid> Mids { get; set; } = new List<LeafPageMid>();
    }

    private sealed class LeafPageMid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public ICollection<LeafPageLeaf> Leaves { get; set; } = new List<LeafPageLeaf>();
    }

    private sealed class LeafPageLeaf
    {
        public int Id { get; set; }
        public int MidId { get; set; }
        public int SortKey { get; set; }
    }

    [Fact]
    public void SingleQuery_DeepestLeafPaged_InMemory_IsRejectedFailClosed()
    {
        using var context = new InMemoryDataContext();
        context.From<LeafPageRoot>().WithData(new[] { new LeafPageRoot { Id = 1 } });
        context.From<LeafPageMid>().WithData(new[] { new LeafPageMid { Id = 10, RootId = 1 } });

        var builder = context.From<LeafPageRoot>()
            .LoadWith(
                r => r.Mids,
                c => c.From<LeafPageMid>().LoadWith(
                    m => m.Leaves,
                    l => l.From<LeafPageLeaf>().OrderBy(x => x.SortKey).Limit(1),
                    m => m.Id,
                    l => l.MidId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery);

        var act = () => builder.ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    // ============================================================================================
    // Row A — JoinInto frontend: a JoinInto level plus a nested LoadWith graph must stitch both.
    // ============================================================================================

    private sealed class JiRoot
    {
        public int Id { get; set; }
        public ICollection<JiRootChild> Children { get; set; } = new List<JiRootChild>();
        public ICollection<JiRootNote> Notes { get; set; } = new List<JiRootNote>();
    }

    private sealed class JiRootChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    private sealed class JiRootNote
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public ICollection<JiRootReply> Replies { get; set; } = new List<JiRootReply>();
    }

    private sealed class JiRootReply
    {
        public int Id { get; set; }
        public int NoteId { get; set; }
    }

    private static InMemoryDataContext CreateJoinIntoContext()
    {
        var context = new InMemoryDataContext();
        context.From<JiRoot>(b => b.HasMany(p => p.Children, c => c.ParentId).HasMany(p => p.Notes, n => n.ParentId));
        context.From<JiRootChild>().WithData(new[]
        {
            new JiRootChild { Id = 10, ParentId = 1 },
            new JiRootChild { Id = 11, ParentId = 1 },
        });
        context.From<JiRootNote>().WithData(new[]
        {
            new JiRootNote { Id = 20, ParentId = 1 },
            new JiRootNote { Id = 21, ParentId = 1 },
        });
        context.From<JiRootReply>().WithData(new[]
        {
            new JiRootReply { Id = 200, NoteId = 20 },
            new JiRootReply { Id = 210, NoteId = 21 },
        });
        context.From<JiRoot>().WithData(new[] { new JiRoot { Id = 1 } });
        return context;
    }

    [Fact]
    public void JoinInto_WithNestedLoadWith_Split_StitchesJoinedAndNestedFrontends()
    {
        using var context = CreateJoinIntoContext();

        var parents = context.From<JiRoot>()
            .JoinInto(context.From<JiRootChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .LoadWith(
                p => p.Notes,
                c => c.From<JiRootNote>().LoadWith(n => n.Replies, r => r.From<JiRootReply>(), n => n.Id, r => r.NoteId),
                p => p.Id,
                n => n.ParentId)
            .ToList();

        var root = parents.Single();
        root.Children.Select(c => c.Id).Should().BeEquivalentTo([10, 11], "the JoinInto level still stitches");
        root.Notes.Select(n => n.Id).Should().BeEquivalentTo([20, 21]);
        root.Notes.Single(n => n.Id == 20).Replies.Select(r => r.Id).Should().Equal(new[] { 200 }, "the nested LoadWith level stitches too");
        root.Notes.Single(n => n.Id == 21).Replies.Select(r => r.Id).Should().Equal(210);
    }

    // ============================================================================================
    // Row A — entity-materializing implicit-navigation frontend: a nested LoadWith graph cannot be
    // composed with an implicit-navigation entity projection; it must fail closed, never silently drop.
    // ============================================================================================

    private sealed class NavRefNode
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string? Name { get; set; }
        public NavRefNode? Parent { get; set; }
        public ICollection<NavRefChild> Children { get; set; } = new List<NavRefChild>();
    }

    private sealed class NavRefChild
    {
        public int Id { get; set; }
        public int NodeId { get; set; }
        public ICollection<NavRefTip> Tips { get; set; } = new List<NavRefTip>();
    }

    private sealed class NavRefTip
    {
        public int Id { get; set; }
        public int ChildId { get; set; }
    }

    [Fact]
    public void ImplicitNavigation_EntityMaterializingProjection_WithNestedLoadWith_IsRejectedFailClosed()
    {
        using var context = new InMemoryDataContext();
        context.From<NavRefNode>(b => b.HasOne(n => n.Parent, n => n.ParentId));
        context.From<NavRefNode>().WithData(new[]
        {
            new NavRefNode { Id = 1, Name = "root" },
            new NavRefNode { Id = 2, ParentId = 1, Name = "child" },
        });

        // The implicit-navigation projection materializes whole entities (<c>n.Parent</c>), and the
        // query also declares a nested (level-2) LoadWith graph. The projection terminal has no loader,
        // so the composition must be rejected rather than silently dropping the nested graph.
        var builder = context.From<NavRefNode>()
            .LoadWith(
                n => n.Children,
                c => c.From<NavRefChild>().LoadWith(x => x.Tips, t => t.From<NavRefTip>(), x => x.Id, t => t.ChildId),
                n => n.Id,
                c => c.NodeId);

        var act = () => builder.Select(n => new { n.Id, Parent = n.Parent });

        act.Should().Throw<NotSupportedException>().WithMessage("*Select*");
    }
}
