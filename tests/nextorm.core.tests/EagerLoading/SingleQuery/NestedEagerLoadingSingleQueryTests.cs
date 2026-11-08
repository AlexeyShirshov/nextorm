using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T212-D4 focal coverage for nested (level 2+) eager loading on the <b>single-query</b> path over the
/// in-memory provider: the expressible case joins every level into one denormalized command and assembles
/// descendants by ancestor identity (section 7), while every unexpressible form fails closed with
/// <see cref="NotSupportedException"/> before execution instead of silently dropping nested metadata or
/// falling back to split-query loading.
/// </summary>
[Trait("Feature", "T212-D4")]
public sealed class NestedEagerLoadingSingleQueryTests
{
    private sealed class Root
    {
        public int Id { get; set; }
        public ICollection<Mid> Mids { get; set; } = new List<Mid>();
    }

    private sealed class Mid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int SortKey { get; set; }
        public bool Active { get; set; }
        public ICollection<Leaf> Leaves { get; set; } = new List<Leaf>();
    }

    private sealed class Leaf
    {
        public int Id { get; set; }
        public int MidId { get; set; }
        public ICollection<Tip> Tips { get; set; } = new List<Tip>();
    }

    private sealed class Tip
    {
        public int Id { get; set; }
        public int LeafId { get; set; }
    }

    private sealed class NullKeyRoot
    {
        public int Id { get; set; }
        public ICollection<NullKeyMid> Mids { get; set; } = new List<NullKeyMid>();
    }

    private sealed class NullKeyMid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public string Key { get; set; } = "";
        public ICollection<NullKeyLeaf> Leaves { get; set; } = new List<NullKeyLeaf>();
    }

    private sealed class NullKeyLeaf
    {
        public int Id { get; set; }
        public string Key { get; set; } = null!;
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[] { new Root { Id = 1 }, new Root { Id = 2 } });
        context.From<Mid>().WithData(new[]
        {
            new Mid { Id = 10, RootId = 1, SortKey = 2, Active = true },
            new Mid { Id = 11, RootId = 1, SortKey = 1, Active = true },
            new Mid { Id = 12, RootId = 2, SortKey = 3, Active = true },
        });
        context.From<Leaf>().WithData(new[]
        {
            new Leaf { Id = 100, MidId = 10 },
            new Leaf { Id = 101, MidId = 10 },
            new Leaf { Id = 110, MidId = 11 },
            new Leaf { Id = 120, MidId = 12 },
        });
        context.From<Tip>().WithData(new[]
        {
            new Tip { Id = 1000, LeafId = 100 },
            new Tip { Id = 1001, LeafId = 100 },
            new Tip { Id = 1010, LeafId = 101 },
            new Tip { Id = 1100, LeafId = 110 },
            new Tip { Id = 1200, LeafId = 120 },
        });
        return context;
    }

    private static EntityBuilder<Root> Depth2Builder(InMemoryDataContext context)
        => context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(m => m.Leaves, l => l.From<Leaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery);

    private static EntityBuilder<Root> Depth3Builder(InMemoryDataContext context)
        => context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(
                    m => m.Leaves,
                    l => l.From<Leaf>().LoadWith(t => t.Tips, p => p.From<Tip>(), t => t.Id, p => p.LeafId),
                    m => m.Id,
                    l => l.MidId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery);

    [Fact]
    public void SingleQuery_Depth2_LoadsEveryLevelFromOneResultSet()
    {
        using var context = CreateContext();

        var roots = Depth2Builder(context).OrderBy(r => r.Id).ToList();

        roots.Select(r => r.Id).Should().Equal(1, 2);
        roots.SelectMany(r => r.Mids).Select(m => m.Id).Should().BeEquivalentTo(new[] { 10, 11, 12 });
        roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).Select(l => l.Id)
            .Should().BeEquivalentTo(new[] { 100, 101, 110, 120 }, "the nested level-2 declaration must not be dropped");
        roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 10).Leaves.Select(l => l.Id).Should().BeEquivalentTo(new[] { 100, 101 });
    }

    [Fact]
    public void SingleQuery_Depth3_LoadsEveryLevel()
    {
        using var context = CreateContext();

        var roots = Depth3Builder(context).OrderBy(r => r.Id).ToList();

        roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).SelectMany(l => l.Tips).Select(t => t.Id)
            .Should().BeEquivalentTo(new[] { 1000, 1001, 1010, 1100, 1200 });
        roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 10).Leaves.Single(l => l.Id == 100)
            .Tips.Select(t => t.Id).Should().BeEquivalentTo(new[] { 1000, 1001 });
    }

    [Fact]
    public void SingleQuery_RepeatedChildKeyUnderDifferentAncestors_AssemblesPerAncestor()
    {
        using var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[] { new Root { Id = 1 }, new Root { Id = 2 } });
        // The same middle id (10) lives under two different ancestors (roots 1 and 2), each with its own leaf.
        context.From<Mid>().WithData(new[]
        {
            new Mid { Id = 10, RootId = 1 },
            new Mid { Id = 10, RootId = 2 },
        });
        context.From<Leaf>().WithData(new[]
        {
            new Leaf { Id = 100, MidId = 10 },
            new Leaf { Id = 200, MidId = 10 },
        });

        var roots = Depth2Builder(context).OrderBy(r => r.Id).ToList();

        roots.Single(r => r.Id == 1).Mids.Single().Leaves.Should().NotBeEmpty("the leaf under the first ancestor must attach");
        roots.Single(r => r.Id == 2).Mids.Single().Leaves.Should().NotBeEmpty("the leaf under the second ancestor must attach");
        roots.Single(r => r.Id == 1).Mids.Single().Leaves.Select(l => l.Id).Should().BeEquivalentTo(new[] { 100, 200 });
        roots.Single(r => r.Id == 2).Mids.Single().Leaves.Select(l => l.Id).Should().BeEquivalentTo(new[] { 100, 200 });
    }

    [Fact]
    public void SingleQuery_EmptyIntermediateResult_LoadsNoGrandchildrenAndDoesNotCrash()
    {
        using var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[] { new Root { Id = 1 }, new Root { Id = 2 } });
        context.From<Mid>().WithData(new[] { new Mid { Id = 10, RootId = 1 } });
        context.From<Leaf>().WithData(new[] { new Leaf { Id = 100, MidId = 99 } });

        var roots = Depth2Builder(context).OrderBy(r => r.Id).ToList();

        roots.Single(r => r.Id == 1).Mids.Single().Leaves.Should().BeEmpty();
        roots.Single(r => r.Id == 2).Mids.Should().BeEmpty();
    }

    [Fact]
    public void SingleQuery_NullKeyChild_AtNestedLevel_IsIgnoredAndDoesNotCrash()
    {
        using var context = new InMemoryDataContext();
        context.From<NullKeyRoot>().WithData(new[] { new NullKeyRoot { Id = 1 } });
        context.From<NullKeyMid>().WithData(new[] { new NullKeyMid { Id = 10, RootId = 1, Key = "a" } });
        context.From<NullKeyLeaf>().WithData(new[]
        {
            new NullKeyLeaf { Id = 100, Key = "a" },
            new NullKeyLeaf { Id = 101, Key = null! },
        });

        var roots = context.From<NullKeyRoot>()
            .LoadWith(
                r => r.Mids,
                c => c.From<NullKeyMid>().LoadWith(m => m.Leaves, l => l.From<NullKeyLeaf>(), m => m.Key, l => l.Key),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery)
            .ToList();

        var leaves = roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).Select(l => l.Id);
        leaves.Should().Equal(new[] { 100 }, "a null nested key never matches and never crashes");
    }

    [Fact]
    public void SingleQuery_NestedChildOrderBy_IsRejectedFailClosed()
    {
        using var context = CreateContext();

        var act = () => context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(m => m.Leaves, l => l.From<Leaf>().OrderBy(x => x.Id), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*OrderBy*");
    }

    [Fact]
    public void SingleQuery_NestedChildDistinct_IsRejectedFailClosed()
    {
        using var context = CreateContext();

        var act = () => context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(m => m.Leaves, l => l.From<Leaf>().Distinct(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Distinct*");
    }

    [Fact]
    public void SingleQuery_NestedPerParentPaging_IsRejectedFailClosed()
    {
        using var context = CreateContext();

        var act = () => context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().OrderBy(m => m.Id).Limit(1)
                    .LoadWith(m => m.Leaves, l => l.From<Leaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void SingleQuery_ConflictingNestedMode_IsRejectedNotSilentlyReinterpreted()
    {
        using var context = CreateContext();

        var builder = context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(
                    m => m.Leaves,
                    l => l.From<Leaf>(),
                    m => m.Id,
                    l => l.MidId,
                    EagerLoadMode.SplitQuery),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SingleQuery);

        var act = () => builder.ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*mixes modes*");
    }

    [Fact]
    public async Task SingleQuery_Nested_AsyncParity_MatchesSync()
    {
        using var syncContext = CreateContext();
        using var asyncContext = CreateContext();

        var sync = Depth3Builder(syncContext).OrderBy(r => r.Id).ToList();
        var async = await Depth3Builder(asyncContext).OrderBy(r => r.Id).ToListAsync();

        static string Flatten(IReadOnlyList<Root> roots) => string.Join("|",
            roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).SelectMany(l => l.Tips).Select(t => t.Id));

        Flatten(async).Should().Be(Flatten(sync));
    }

    // ---- Depth-8 chain: the projection arity cap (parent + 7 children max) fails closed. ----

    private sealed class C1 { public int Id { get; set; } public int P { get; set; } public ICollection<C2> Next { get; set; } = new List<C2>(); }
    private sealed class C2 { public int Id { get; set; } public int P { get; set; } public ICollection<C3> Next { get; set; } = new List<C3>(); }
    private sealed class C3 { public int Id { get; set; } public int P { get; set; } public ICollection<C4> Next { get; set; } = new List<C4>(); }
    private sealed class C4 { public int Id { get; set; } public int P { get; set; } public ICollection<C5> Next { get; set; } = new List<C5>(); }
    private sealed class C5 { public int Id { get; set; } public int P { get; set; } public ICollection<C6> Next { get; set; } = new List<C6>(); }
    private sealed class C6 { public int Id { get; set; } public int P { get; set; } public ICollection<C7> Next { get; set; } = new List<C7>(); }
    private sealed class C7 { public int Id { get; set; } public int P { get; set; } public ICollection<C8> Next { get; set; } = new List<C8>(); }
    private sealed class C8 { public int Id { get; set; } public int P { get; set; } }

    private sealed class Root8
    {
        public int Id { get; set; }
        public ICollection<C1> Next { get; set; } = new List<C1>();
    }

    [Fact]
    public void SingleQuery_GraphDeeperThanProjectionArity_IsRejectedFailClosed()
    {
        using var context = new InMemoryDataContext();

        // Root8 -> C1 -> ... -> C8 is eight child collections, one more than the Projection<..8> arity
        // supports (parent + seven). It must fail closed before any SQL instead of overflowing the join.
        var builder = context.From<Root8>()
            .LoadWith(r => r.Next, c => c.From<C1>()
                .LoadWith(a => a.Next, d => d.From<C2>()
                    .LoadWith(b => b.Next, e => e.From<C3>()
                        .LoadWith(f => f.Next, g => g.From<C4>()
                            .LoadWith(h => h.Next, i => i.From<C5>()
                                .LoadWith(j => j.Next, k => k.From<C6>()
                                    .LoadWith(l => l.Next, m => m.From<C7>()
                                        .LoadWith(n => n.Next, o => o.From<C8>(),
                                            n => n.Id, o => o.P, EagerLoadMode.SingleQuery),
                                        l => l.Id, m => m.P, EagerLoadMode.SingleQuery),
                                    j => j.Id, k => k.P, EagerLoadMode.SingleQuery),
                                h => h.Id, i => i.P, EagerLoadMode.SingleQuery),
                            f => f.Id, g => g.P, EagerLoadMode.SingleQuery),
                        b => b.Id, e => e.P, EagerLoadMode.SingleQuery),
                    a => a.Id, d => d.P, EagerLoadMode.SingleQuery),
                r => r.Id, c => c.P, EagerLoadMode.SingleQuery);

        var act = () => builder.ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*at most 7*");
    }
}
