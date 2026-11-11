using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T212-D2 focal coverage for the shared nested eager-load graph contract: node/path metadata,
/// ancestor-composite identity, graph-wide mode/filter context, declaration validation (the deterministic
/// cycle guard and nested-mode conflicts) and frontend normalization. These tests exercise the internal
/// contract directly; they build no command and execute no query.
/// </summary>
[Trait("Feature", "T212-D2")]
public sealed class NestedEagerLoadingGraphContractTests
{
    public sealed class RootEntity
    {
        public int Id { get; set; }
        public ICollection<MiddleEntity> Middles { get; set; } = new List<MiddleEntity>();
        public ICollection<MiddleEntity> AltMiddles { get; set; } = new List<MiddleEntity>();
    }

    public sealed class MiddleEntity
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public ICollection<LeafEntity> Leaves { get; set; } = new List<LeafEntity>();
    }

    public sealed class LeafEntity
    {
        public int Id { get; set; }
        public int MiddleId { get; set; }
        public ICollection<TipEntity> Tips { get; set; } = new List<TipEntity>();
    }

    public sealed class TipEntity
    {
        public int Id { get; set; }
        public int LeafId { get; set; }
    }

    public sealed class SelfEntity
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public ICollection<SelfEntity> Children { get; set; } = new List<SelfEntity>();
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<RootEntity>().WithData(new[] { new RootEntity { Id = 1 }, new RootEntity { Id = 2 } });
        context.From<MiddleEntity>().WithData(new[] { new MiddleEntity { Id = 10, RootId = 1 }, new MiddleEntity { Id = 11, RootId = 2 } });
        context.From<LeafEntity>().WithData(new[] { new LeafEntity { Id = 100, MiddleId = 10 } });
        context.From<TipEntity>().WithData(new[] { new TipEntity { Id = 1000, LeafId = 100 } });
        context.From<SelfEntity>().WithData(new[] { new SelfEntity { Id = 1 }, new SelfEntity { Id = 2, ParentId = 1 } });
        return context;
    }

    private static EntityBuilder<RootEntity> NestedBuilder(InMemoryDataContext context, EagerLoadMode mode = EagerLoadMode.Default)
        => context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(
                    m => m.Leaves,
                    l => l.From<LeafEntity>(),
                    m => m.Id,
                    l => l.MiddleId),
                r => r.Id,
                m => m.RootId,
                mode);

    [Fact]
    public void Normalize_PlainBuilder_ProducesEmptyGraph()
    {
        using var context = CreateContext();

        var graph = context.From<RootEntity>().NormalizeEagerLoadGraph(context);

        graph.IsEmpty.Should().BeTrue();
        graph.Nodes.Should().BeEmpty();
        graph.MaxDepth.Should().Be(0);
        graph.ToShape().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Normalize_NestedGraph_ExposesRootAndChildNodesWithAncestorCompositeIdentity()
    {
        using var context = CreateContext();

        var graph = NestedBuilder(context).NormalizeEagerLoadGraph(context);

        graph.Nodes.Should().HaveCount(2);
        graph.Roots.Should().HaveCount(1);
        graph.MaxDepth.Should().Be(2);

        var root = graph.Roots[0];
        root.IsRoot.Should().BeTrue();
        root.Parent.Should().BeNull();
        root.Depth.Should().Be(1);
        root.Path.Should().Equal(new[] { root.Segment });
        root.CompositeIdentity.Should().Be(root.Segment, "a top-level node's composite identity is its own key");

        var child = graph.Nodes[1];
        child.IsRoot.Should().BeFalse();
        child.Parent.Should().BeSameAs(root);
        child.Depth.Should().Be(2);
        child.Path.Should().Equal(new[] { root.Segment, child.Segment });
        child.CompositeIdentity.Should().Be(root.Segment + "/" + child.Segment, "the identity is L1 key + L2 key");

        graph.Find(root.CompositeIdentity).Should().BeSameAs(root);
        graph.Find(child.CompositeIdentity).Should().BeSameAs(child);
    }

    [Fact]
    public void Normalize_Depth3Graph_ExtendsAncestorCompositeIdentityPerLevel()
    {
        using var context = CreateContext();

        var graph = context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(
                    m => m.Leaves,
                    l => l.From<LeafEntity>().LoadWith(
                        t => t.Tips,
                        p => p.From<TipEntity>(),
                        t => t.Id,
                        p => p.LeafId),
                    m => m.Id,
                    l => l.MiddleId),
                r => r.Id,
                m => m.RootId)
            .NormalizeEagerLoadGraph(context);

        graph.Nodes.Should().HaveCount(3);
        graph.MaxDepth.Should().Be(3);
        graph.Roots[0].Depth.Should().Be(1);
        graph.Nodes[1].Depth.Should().Be(2);

        var deepest = graph.Nodes[2];
        deepest.Depth.Should().Be(3);
        deepest.Path.Should().Equal(new[] { graph.Roots[0].Segment, graph.Nodes[1].Segment, deepest.Segment });
        deepest.CompositeIdentity.Should().Be(string.Join("/", deepest.Path));
    }

    [Fact]
    public void Normalize_SingleQueryMode_IsTheGraphWideMode()
    {
        using var context = CreateContext();

        var split = NestedBuilder(context).NormalizeEagerLoadGraph(context);
        var single = NestedBuilder(context, EagerLoadMode.SingleQuery).NormalizeEagerLoadGraph(context);

        split.Context.Mode.Should().Be(EagerLoadMode.SplitQuery);
        single.Context.Mode.Should().Be(EagerLoadMode.SingleQuery);
    }

    [Fact]
    public void Normalize_InheritsTheRootFilterScopeForTheWholeGraph()
    {
        using var context = CreateContext();

        var builder = NestedBuilder(context).IgnoreFilters();
        var graph = builder.NormalizeEagerLoadGraph(context);

        graph.Context.FilterScope.Should().BeSameAs(builder.FilterScope);
        graph.Context.FilterScope.All.Should().BeTrue();
    }

    [Fact]
    public void Normalize_ConflictingNestedMode_IsRejected()
    {
        using var context = CreateContext();

        var builder = context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(
                    m => m.Leaves,
                    l => l.From<LeafEntity>(),
                    m => m.Id,
                    l => l.MiddleId,
                    EagerLoadMode.SingleQuery),
                r => r.Id,
                m => m.RootId,
                EagerLoadMode.SplitQuery);

        var act = () => builder.NormalizeEagerLoadGraph(context);

        act.Should().Throw<NotSupportedException>().WithMessage("*mixes modes*");
    }

    [Fact]
    public void Normalize_SelfReferentialCycle_IsRejectedDeterministically()
    {
        using var context = CreateContext();

        Func<IDataContext, EntityBuilder<SelfEntity>> factory = null!;
        factory = ctx => ctx.From<SelfEntity>().LoadWith(n => n.Children, factory, n => n.Id, n => n.ParentId);

        var builder = context.From<SelfEntity>().LoadWith(n => n.Children, factory, n => n.Id, n => n.ParentId);

        var act = () => builder.NormalizeEagerLoadGraph(context);

        act.Should().Throw<NotSupportedException>().WithMessage("*cyclic*");
    }

    [Fact]
    public void Normalize_BuilderReferenceCycle_IsRejected()
    {
        using var context = CreateContext();

        EntityBuilder<SelfEntity> builder = null!;
        builder = context.From<SelfEntity>().LoadWith(n => n.Children, _ => builder, n => n.Id, n => n.ParentId);

        var act = () => builder.NormalizeEagerLoadGraph(context);

        act.Should().Throw<NotSupportedException>().WithMessage("*cyclic*");
    }

    [Fact]
    public void Normalize_SiblingBranchesWithTheSameTypes_AreNotTreatedAsCycles()
    {
        using var context = CreateContext();

        var builder = context.From<RootEntity>()
            .LoadWith(
                r => r.Middles,
                c => c.From<MiddleEntity>().LoadWith(m => m.Leaves, l => l.From<LeafEntity>(), m => m.Id, l => l.MiddleId),
                r => r.Id,
                m => m.RootId)
            .LoadWith(r => r.AltMiddles, c => c.From<MiddleEntity>(), r => r.Id, m => m.RootId);

        var graph = builder.NormalizeEagerLoadGraph(context);

        graph.Roots.Should().HaveCount(2);
        graph.Nodes.Should().HaveCount(3);
        graph.MaxDepth.Should().Be(2);
    }

    [Fact]
    public void ToShape_IncludesNestedIdentity_SoANestedGraphDiffersFromALevelOneGraph()
    {
        using var context = CreateContext();

        var nested = NestedBuilder(context).NormalizeEagerLoadGraph(context).ToShape();
        var flat = context.From<RootEntity>()
            .LoadWith(r => r.Middles, c => c.From<MiddleEntity>(), r => r.Id, m => m.RootId)
            .NormalizeEagerLoadGraph(context)
            .ToShape();

        nested.IsEmpty.Should().BeFalse();
        flat.IsEmpty.Should().BeFalse();
        nested.Equals(flat).Should().BeFalse("the nested graph carries the level-2 identity the flat graph does not");
    }
}
