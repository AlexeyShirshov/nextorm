using System.Collections;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T212-D3 focal coverage for nested (level 2+) eager loading on the <b>split</b> path over the in-memory
/// provider: iterative descendant orchestration, per-level ordering, section-7 assembly/identity,
/// filter-scope propagation, null/read-only handling and sync/async parity. Per-parent top-N remains
/// fail-closed on the in-memory provider (#211) at every level.
/// </summary>
[Trait("Feature", "T212-D3")]
public sealed class NestedEagerLoadingSplitTests
{
    private sealed class Root
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public ICollection<Mid> Mids { get; set; } = new List<Mid>();
        public ICollection<Mid> AltMids { get; set; } = new List<Mid>();
    }

    private sealed class Mid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int SortKey { get; set; }
        public bool IsDeleted { get; set; }
        public ICollection<Leaf> Leaves { get; set; } = new List<Leaf>();
    }

    private sealed class Leaf
    {
        public int Id { get; set; }
        public int MidId { get; set; }
        public int SortKey { get; set; }
        public bool IsDeleted { get; set; }
        public ICollection<Tip> Tips { get; set; } = new List<Tip>();
    }

    private sealed class Tip
    {
        public int Id { get; set; }
        public int LeafId { get; set; }
        public ICollection<End> Ends { get; set; } = new List<End>();
    }

    private sealed class End
    {
        public int Id { get; set; }
        public int TipId { get; set; }
    }

    private sealed class ReadOnlyMid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public ICollection<Leaf> Leaves { get; } = null!;
    }

    private sealed class ReadOnlyRoot
    {
        public int Id { get; set; }
        public ICollection<ReadOnlyMid> Mids { get; set; } = new List<ReadOnlyMid>();
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

    private sealed class FilteredRoot
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public ICollection<FilteredMid> Mids { get; set; } = new List<FilteredMid>();
    }

    private sealed class FilteredMid
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public bool IsDeleted { get; set; }
        public ICollection<FilteredLeaf> Leaves { get; set; } = new List<FilteredLeaf>();
    }

    private sealed class FilteredLeaf
    {
        public int Id { get; set; }
        public int MidId { get; set; }
        public bool IsDeleted { get; set; }
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[]
        {
            new Root { Id = 1 },
            new Root { Id = 2 },
        });
        context.From<Mid>().WithData(new[]
        {
            new Mid { Id = 10, RootId = 1, SortKey = 2 },
            new Mid { Id = 11, RootId = 1, SortKey = 1 },
            new Mid { Id = 12, RootId = 2, SortKey = 3 },
        });
        context.From<Leaf>().WithData(new[]
        {
            new Leaf { Id = 100, MidId = 10, SortKey = 2 },
            new Leaf { Id = 101, MidId = 10, SortKey = 1 },
            new Leaf { Id = 110, MidId = 11, SortKey = 1 },
            new Leaf { Id = 120, MidId = 12, SortKey = 1 },
        });
        context.From<Tip>().WithData(new[]
        {
            new Tip { Id = 1000, LeafId = 100 },
            new Tip { Id = 1001, LeafId = 100 },
            new Tip { Id = 1010, LeafId = 101 },
            new Tip { Id = 1100, LeafId = 110 },
            new Tip { Id = 1200, LeafId = 120 },
        });
        context.From<End>().WithData(new[]
        {
            new End { Id = 10000, TipId = 1000 },
            new End { Id = 10001, TipId = 1000 },
            new End { Id = 10010, TipId = 1010 },
            new End { Id = 11000, TipId = 1100 },
        });
        return context;
    }

    private static EntityBuilder<Root> Depth2Builder(InMemoryDataContext context)
        => context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(m => m.Leaves, l => l.From<Leaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId);

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
                m => m.RootId);

    [Fact]
    public void Split_Depth2_LoadsEveryLevel()
    {
        using var context = CreateContext();

        var roots = Depth2Builder(context).OrderBy(r => r.Id).ToList();

        roots.Select(r => r.Id).Should().Equal(1, 2);
        roots.SelectMany(r => r.Mids).Select(m => m.Id).Should().Equal(10, 11, 12);
        roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).Select(l => l.Id)
            .Should().BeEquivalentTo(new[] { 100, 101, 110, 120 });
    }

    [Fact]
    public void Split_Depth3_LoadsEveryLevel()
    {
        using var context = CreateContext();

        var roots = Depth3Builder(context).OrderBy(r => r.Id).ToList();

        roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).SelectMany(l => l.Tips).Select(t => t.Id)
            .Should().BeEquivalentTo(new[] { 1000, 1001, 1010, 1100, 1200 });
        roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 10).Leaves.Single(l => l.Id == 100)
            .Tips.Select(t => t.Id).Should().Equal(1000, 1001);
    }

    [Fact]
    public void Split_DeepChainStress_Depth4_LoadsAllLevels()
    {
        using var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[] { new Root { Id = 1 } });
        context.From<Mid>().WithData(new[] { new Mid { Id = 10, RootId = 1 } });
        context.From<Leaf>().WithData(new[] { new Leaf { Id = 100, MidId = 10 } });
        context.From<Tip>().WithData(new[] { new Tip { Id = 1000, LeafId = 100 } });
        context.From<End>().WithData(new[]
        {
            new End { Id = 10000, TipId = 1000 },
            new End { Id = 10001, TipId = 1000 },
        });

        var roots = context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(
                    m => m.Leaves,
                    l => l.From<Leaf>().LoadWith(
                        t => t.Tips,
                        p => p.From<Tip>().LoadWith(e => e.Ends, q => q.From<End>(), e => e.Id, q => q.TipId),
                        t => t.Id,
                        p => p.LeafId),
                    m => m.Id,
                    l => l.MidId),
                r => r.Id,
                m => m.RootId)
            .ToList();

        var ends = roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).SelectMany(l => l.Tips)
            .SelectMany(t => t.Ends).Select(e => e.Id);
        ends.Should().BeEquivalentTo(new[] { 10000, 10001 });
    }

    [Fact]
    public void Split_PerLevelOrder_IsIndependentAtEachLevel()
    {
        using var context = CreateContext();

        var roots = context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>()
                    .OrderBy(m => m.SortKey)
                    .LoadWith(m => m.Leaves, l => l.From<Leaf>().OrderByDescending(x => x.SortKey), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId)
            .OrderBy(r => r.Id)
            .ToList();

        roots.SelectMany(r => r.Mids).Select(m => m.Id).Should().Equal(new[] { 11, 10, 12 }, "level-1 order by Mid.SortKey");
        roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 10).Leaves.Select(l => l.Id)
            .Should().Equal(new[] { 100, 101 }, "level-2 order by Leaf.SortKey descending");
        roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 11).Leaves.Select(l => l.Id)
            .Should().Equal(110);
    }

    [Fact]
    public void Split_NestedPagedChild_InMemory_IsRejectedFailClosed()
    {
        using var context = CreateContext();

        var act = () => context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(
                    m => m.Leaves,
                    l => l.From<Leaf>().OrderBy(x => x.SortKey).Limit(1),
                    m => m.Id,
                    l => l.MidId),
                r => r.Id,
                m => m.RootId)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void Split_SiblingBranches_LoadIndependently()
    {
        using var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[] { new Root { Id = 1 } });
        context.From<Mid>().WithData(new[]
        {
            new Mid { Id = 10, RootId = 1 },
            new Mid { Id = 11, RootId = 1 },
        });
        context.From<Leaf>().WithData(new[]
        {
            new Leaf { Id = 100, MidId = 10 },
            new Leaf { Id = 110, MidId = 11 },
        });

        var roots = context.From<Root>()
            .LoadWith(
                r => r.Mids,
                c => c.From<Mid>().LoadWith(m => m.Leaves, l => l.From<Leaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId)
            .LoadWith(
                r => r.AltMids,
                c => c.From<Mid>().LoadWith(m => m.Leaves, l => l.From<Leaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId)
            .ToList();

        var root = roots.Single();
        // Both branches target the same child type and key, so both query the same middle/leaf sets.
        root.Mids.Select(m => m.Id).Should().BeEquivalentTo(new[] { 10, 11 });
        root.AltMids.Select(m => m.Id).Should().BeEquivalentTo(new[] { 10, 11 });
        root.Mids.Single(m => m.Id == 10).Leaves.Select(l => l.Id).Should().Equal(100);
        root.AltMids.Single(m => m.Id == 11).Leaves.Select(l => l.Id).Should().Equal(110);
    }

    [Fact]
    public void Split_RepeatedChildKeyUnderDifferentAncestors_UsesCompositeIdentity()
    {
        using var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[] { new Root { Id = 1 }, new Root { Id = 2 } });
        // The same middle id (10) lives under two different ancestors (roots 1 and 2).
        context.From<Mid>().WithData(new[]
        {
            new Mid { Id = 10, RootId = 1 },
            new Mid { Id = 10, RootId = 2 },
        });
        context.From<Leaf>().WithData(new[] { new Leaf { Id = 100, MidId = 10 } });
        context.From<Tip>().WithData(new[] { new Tip { Id = 1000, LeafId = 100 } });

        var roots = Depth3Builder(context).OrderBy(r => r.Id).ToList();

        var leafUnderRoot1 = roots.Single(r => r.Id == 1).Mids.Single().Leaves.Single();
        var leafUnderRoot2 = roots.Single(r => r.Id == 2).Mids.Single().Leaves.Single();
        leafUnderRoot1.Id.Should().Be(100);
        leafUnderRoot2.Id.Should().Be(100);
        leafUnderRoot1.Tips.Select(t => t.Id).Should().Equal(1000);
        leafUnderRoot2.Tips.Select(t => t.Id).Should().Equal(1000);
        leafUnderRoot1.Should().BeSameAs(leafUnderRoot2, "children are linked by key, so a repeated child key resolves to one instance under every ancestor");
    }

    [Fact]
    public void Split_EmptyIntermediateResult_LoadsNoGrandchildrenAndDoesNotCrash()
    {
        using var context = new InMemoryDataContext();
        context.From<Root>().WithData(new[] { new Root { Id = 1 }, new Root { Id = 2 } });
        // Root 1 has a middle with no leaves; root 2 has no middle at all.
        context.From<Mid>().WithData(new[] { new Mid { Id = 10, RootId = 1 } });
        context.From<Leaf>().WithData(new[] { new Leaf { Id = 100, MidId = 99 } });

        var roots = Depth2Builder(context).OrderBy(r => r.Id).ToList();

        roots.Single(r => r.Id == 1).Mids.Single().Leaves.Should().BeEmpty();
        roots.Single(r => r.Id == 2).Mids.Should().BeEmpty();
    }

    [Fact]
    public void Split_NullKeyChild_AtNestedLevel_IsIgnoredAndDoesNotCrash()
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
                m => m.RootId)
            .ToList();

        var leaves = roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).Select(l => l.Id);
        leaves.Should().Equal(new[] { 100 }, "a null nested key never matches and never crashes");
    }

    [Fact]
    public void Split_ReadOnlyNullCollectionAtNestedLevel_ThrowsFailClosed()
    {
        using var context = new InMemoryDataContext();
        context.From<ReadOnlyRoot>().WithData(new[] { new ReadOnlyRoot { Id = 1 } });
        context.From<ReadOnlyMid>().WithData(new[] { new ReadOnlyMid { Id = 10, RootId = 1 } });

        var act = () => context.From<ReadOnlyRoot>()
            .LoadWith(
                r => r.Mids,
                c => c.From<ReadOnlyMid>().LoadWith(m => m.Leaves, l => l.From<Leaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*not settable*");
    }

    private static InMemoryDataContext CreateFilteredContext()
    {
        var context = new InMemoryDataContext();
        context.From<FilteredRoot>(b => b.HasQueryFilter("soft", (e, _) => !e.IsDeleted)).WithData(new[]
        {
            new FilteredRoot { Id = 1 },
            new FilteredRoot { Id = 2, IsDeleted = true },
        });
        context.From<FilteredMid>(b => b.HasQueryFilter("soft", (e, _) => !e.IsDeleted)).WithData(new[]
        {
            new FilteredMid { Id = 10, RootId = 1 },
            new FilteredMid { Id = 11, RootId = 1, IsDeleted = true },
            new FilteredMid { Id = 12, RootId = 2 },
        });
        context.From<FilteredLeaf>(b => b.HasQueryFilter("soft", (e, _) => !e.IsDeleted)).WithData(new[]
        {
            new FilteredLeaf { Id = 100, MidId = 10 },
            new FilteredLeaf { Id = 101, MidId = 10, IsDeleted = true },
            new FilteredLeaf { Id = 110, MidId = 11 },
            new FilteredLeaf { Id = 120, MidId = 12 },
        });
        return context;
    }

    private static EntityBuilder<FilteredRoot> FilteredDepth2Builder(InMemoryDataContext context)
        => context.From<FilteredRoot>()
            .LoadWith(
                r => r.Mids,
                c => c.From<FilteredMid>().LoadWith(m => m.Leaves, l => l.From<FilteredLeaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId);

    [Fact]
    public void Split_GlobalFilters_ApplyAtEveryNestedLevel()
    {
        using var context = CreateFilteredContext();

        var roots = FilteredDepth2Builder(context).OrderBy(r => r.Id).ToList();

        roots.Select(r => r.Id).Should().Equal(new[] { 1 });
        roots.Single().Mids.Select(m => m.Id).Should().Equal(new[] { 10 });
        roots.Single().Mids.Single().Leaves.Select(l => l.Id).Should().Equal(new[] { 100 });
    }

    [Fact]
    public void Split_RootIgnoreFilters_PropagatesToEveryNestedLevel()
    {
        using var context = CreateFilteredContext();

        var roots = FilteredDepth2Builder(context).IgnoreFilters().OrderBy(r => r.Id).ToList();

        roots.Select(r => r.Id).Should().Equal(1, 2);
        roots.SelectMany(r => r.Mids).Select(m => m.Id).Should().BeEquivalentTo(new[] { 10, 11, 12 });
        roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).Select(l => l.Id)
            .Should().BeEquivalentTo(new[] { 100, 101, 110, 120 });
    }

    [Fact]
    public void Split_RootSelectiveIgnore_PropagatesTheScopeToEveryNestedLevel()
    {
        using var context = CreateFilteredContext();

        var roots = FilteredDepth2Builder(context).IgnoreFilters(["soft"]).OrderBy(r => r.Id).ToList();

        roots.Select(r => r.Id).Should().Equal(1, 2);
        roots.SelectMany(r => r.Mids).Select(m => m.Id).Should().BeEquivalentTo(new[] { 10, 11, 12 });
        roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).Select(l => l.Id)
            .Should().BeEquivalentTo(new[] { 100, 101, 110, 120 });
    }

    [Fact]
    public void Split_ChildIgnoreFilters_AtLevel2_PropagatesToLevel3()
    {
        using var context = CreateFilteredContext();

        // The level-2 child disables its own filters (all). Root filters still apply, but level-2 and the
        // level-3 grandchildren inherit the child's all-or-nothing scope.
        var roots = context.From<FilteredRoot>()
            .LoadWith(
                r => r.Mids,
                c => c.From<FilteredMid>().IgnoreFilters().LoadWith(m => m.Leaves, l => l.From<FilteredLeaf>(), m => m.Id, l => l.MidId),
                r => r.Id,
                m => m.RootId)
            .OrderBy(r => r.Id)
            .ToList();

        roots.Select(r => r.Id).Should().Equal(new[] { 1 }, "the parent filter still applies");
        roots.Single().Mids.Select(m => m.Id).Should().Equal(new[] { 10, 11 }, "the level-2 child disables its own filter");
        roots.Single().Mids.SelectMany(m => m.Leaves).Select(l => l.Id)
            .Should().BeEquivalentTo(new[] { 100, 101, 110 }, "the child scope propagates to level 3");
    }

    [Fact]
    public async Task Split_Nested_AsyncParity_MatchesSync()
    {
        using var syncContext = CreateContext();
        using var asyncContext = CreateContext();

        var sync = Depth3Builder(syncContext).OrderBy(r => r.Id).ToList();
        var async = await Depth3Builder(asyncContext).OrderBy(r => r.Id).ToListAsync();

        static string Flatten(IReadOnlyList<Root> roots) => string.Join("|",
            roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).SelectMany(l => l.Tips).Select(t => t.Id));

        Flatten(async).Should().Be(Flatten(sync));
    }

    [Fact]
    public void Split_NoPerParentNPlusOne_ExecutionCountsStayStableAsFanOutGrows()
    {
        // The child-query source enumeration count is the execution-count proxy: if the split loader issued
        // one query per parent, the count would scale with the fan-out. It must stay flat.
        static (int MidEnumerations, int LeafEnumerations, int LoadedLeaves) Run(int parentCount)
        {
            var mids = new CountingEnumerable<Mid>(Enumerable.Range(1, parentCount).SelectMany(
                rootId => Enumerable.Range(0, 3).Select(i => new Mid { Id = rootId * 10 + i, RootId = rootId })));
            var leaves = new CountingEnumerable<Leaf>(mids.SelectMany(mid => Enumerable.Range(0, 2).Select(i => new Leaf { Id = mid.Id * 10 + i, MidId = mid.Id })));

            using var context = new InMemoryDataContext();
            context.From<Root>().WithData(Enumerable.Range(1, parentCount).Select(id => new Root { Id = id }));
            context.From<Mid>().WithData(mids);
            context.From<Leaf>().WithData(leaves);

            var roots = context.From<Root>()
                .LoadWith(
                    r => r.Mids,
                    c => c.From<Mid>().LoadWith(m => m.Leaves, l => l.From<Leaf>(), m => m.Id, l => l.MidId),
                    r => r.Id,
                    m => m.RootId)
                .OrderBy(r => r.Id)
                .ToList();

            var loadedLeaves = roots.SelectMany(r => r.Mids).SelectMany(m => m.Leaves).Count();
            return (mids.Enumerations, leaves.Enumerations, loadedLeaves);
        }

        var small = Run(4);
        var large = Run(16);

        small.LoadedLeaves.Should().Be(4 * 3 * 2);
        large.LoadedLeaves.Should().Be(16 * 3 * 2);
        large.MidEnumerations.Should().Be(small.MidEnumerations, "a larger fan-out must not add child queries");
        large.LeafEnumerations.Should().Be(small.LeafEnumerations, "a larger fan-out must not add grandchild queries");
    }

    private sealed class CountingEnumerable<T>(IEnumerable<T> items) : IEnumerable<T>
    {
        private readonly List<T> _items = items.ToList();

        public int Enumerations { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            Enumerations++;
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
