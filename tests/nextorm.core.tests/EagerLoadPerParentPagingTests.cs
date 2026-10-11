using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Feature D211 — per-parent top-N eager loading, core-level coverage. The in-memory provider has no SQL
/// window, so a paged <c>LoadWith</c> child is retained as fail-closed in both eager modes (split list
/// loader and single-query join), instead of silently applying a global child limit or a client-side
/// per-parent page. Unpaged eager loading is unchanged. Rejection is raised before any command runs.
/// </summary>
[Trait("Feature", "D211")]
public sealed class EagerLoadPerParentPagingTests
{
    public sealed class LpParent
    {
        public int Id { get; set; }
        public ICollection<LpChild> Children { get; set; } = new List<LpChild>();
    }

    public sealed class LpChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public int SortKey { get; set; }
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<LpParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        context.From<LpChild>().WithData(new[]
        {
            new LpChild { Id = 10, ParentId = 1, SortKey = 2 },
            new LpChild { Id = 11, ParentId = 1, SortKey = 1 },
            new LpChild { Id = 12, ParentId = 2, SortKey = 3 },
        });
        context.From<LpParent>().WithData(new[]
        {
            new LpParent { Id = 1 },
            new LpParent { Id = 2 },
            new LpParent { Id = 3 },
        });
        return context;
    }

    [Fact]
    public void SplitPagedChild_InMemory_IsRejectedFailClosed()
    {
        using var context = CreateContext();

        var act = () => context.From<LpParent>()
            .LoadWith(p => p.Children, c => c.From<LpChild>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void SingleQueryPagedChild_InMemory_IsRejectedFailClosed()
    {
        using var context = CreateContext();

        var act = () => context.From<LpParent>()
            .LoadWith(p => p.Children, c => c.From<LpChild>().OrderBy(x => x.SortKey).Offset(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void UnpagedSplitChild_InMemory_StillLoadsEveryChild()
    {
        using var context = CreateContext();

        var parents = context.From<LpParent>()
            .LoadWith(p => p.Children, c => c.From<LpChild>().OrderBy(x => x.SortKey), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2, 3);
        parents[0].Children.Select(c => c.Id).Should().Equal(11, 10);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void UnpagedSingleQueryChild_InMemory_StillLoadsEveryChild()
    {
        using var context = CreateContext();

        var parents = context.From<LpParent>()
            .LoadWith(p => p.Children, c => c.From<LpChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2, 3);
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([10, 11]);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[2].Children.Should().BeEmpty();
    }
}

/// <summary>
/// #211 variant row :244 / W-4: the in-memory <c>JoinInto</c> entry points must stay fail-closed for a
/// child <c>Limit</c>/<c>Offset</c>/<c>Page</c>, so a plain <c>JoinInto</c> never silently reinterprets a
/// paged <c>LoadWith</c> as a global limit. The guard runs before any command is built or executed.
/// </summary>
[Trait("Feature", "D211")]
public sealed class EagerLoadPerParentPagingJoinIntoTests
{
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<JiParent>(b => b.HasMany(p => p.Children, c => c.ParentId).HasMany(p => p.Notes, n => n.ParentId));
        ctx.From<JiChild>().WithData([
            new JiChild { Id = 10, ParentId = 1, SortKey = 2 },
            new JiChild { Id = 11, ParentId = 1, SortKey = 1 },
            new JiChild { Id = 12, ParentId = 2, SortKey = 3 },
        ]);
        ctx.From<JiNote>().WithData([
            new JiNote { Id = 20, ParentId = 1, Rank = 1 },
            new JiNote { Id = 21, ParentId = 1, Rank = 2 },
        ]);
        ctx.From<JiParent>().WithData([
            new JiParent { Id = 1 },
            new JiParent { Id = 2 },
        ]);
        return ctx;
    }

    [Fact]
    public void JoinInto_WithPagedLoadWith_InMemory_IsRejectedFailClosedBeforeSql()
    {
        using var ctx = CreateContext();

        var act = () => ctx.From<JiParent>()
            .JoinInto(ctx.From<JiChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JiNote>().OrderBy(x => x.Rank).Limit(1), p => p.Id, n => n.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void JoinIntoExplicitKey_WithPagedLoadWith_InMemory_IsRejectedFailClosedBeforeSql()
    {
        using var ctx = CreateContext();

        // The TKey selector overload carries no child-query factory, so it must not invent a paging API:
        // a paged LoadWith is still rejected through the shared JoinInto entry guard.
        var act = () => ctx.From<JiParent>()
            .JoinInto<JiChild, int>(ctx.From<JiChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .LoadWith(p => p.Notes, n => n.From<JiNote>().OrderBy(x => x.Rank).Offset(1), p => p.Id, n => n.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void JoinInto_WithUnpagedLoadWith_KeepsWorking()
    {
        using var ctx = CreateContext();

        // Unchanged behavior: a JoinInto + unpaged LoadWith still stitches both collections.
        var parents = ctx.From<JiParent>()
            .JoinInto(ctx.From<JiChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JiNote>().OrderBy(x => x.Rank), p => p.Id, n => n.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([10, 11]);
        parents[0].Notes.Select(n => n.Id).Should().Equal(20, 21);
    }
}

/// <summary>
/// #211 variant row :245: the implicit-navigation expansion (<c>NavigationExpansion.cs</c>) carries no
/// paging metadata, so a reference-navigation query remains unchanged by the per-parent top-N feature.
/// </summary>
[Trait("Feature", "D211")]
public sealed class EagerLoadPerParentPagingImplicitNavigationTests
{
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<NavParent>();
        ctx.From<NavChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<NavParent>().WithData([
            new NavParent { Id = 1, Name = "root" },
            new NavParent { Id = 2, Name = "other" },
        ]);
        ctx.From<NavChild>().WithData([
            new NavChild { Id = 10, ParentId = 1, Name = "child" },
            new NavChild { Id = 11, ParentId = 999, Name = "dangling" },
        ]);
        return ctx;
    }

    [Fact]
    public void ImplicitReferenceNavigation_WithoutPagingMetadata_IsUnchanged()
    {
        using var ctx = CreateContext();

        // The reference member is expanded to a join by the navigation pass, not by any paging metadata.
        var rows = ctx.From<NavChild>()
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, Parent = c.Parent!.Name })
            .ToList();

        rows.Single(r => r.Id == 10).Parent.Should().Be("root");
        rows.Single(r => r.Id == 11).Parent.Should().BeNull("a dangling reference resolves to null");
    }

    [Fact]
    public void ImplicitReferenceNavigation_SurvivesAnUnpagedEagerLoad()
    {
        using var ctx = CreateContext();

        // The same implicit-navigation query alongside an eager (unpaged) reference load is byte-for-byte
        // the pre-existing behavior: no paging path is entered.
        var child = ctx.From<NavChild>().Where(c => c.Id == 10).Single();
        child.Parent.Should().BeNull("the in-memory context does not attach the reference eagerly");

        var parents = ctx.From<NavParent>()
            .LoadWith(p => p.Children, c => c.From<NavChild>(), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();
        parents[0].Children.Select(c => c.Id).Should().Equal([10]);
        parents[1].Children.Should().BeEmpty("child 11 references a non-existent parent id");
    }
}

public sealed class JiParent
{
    public int Id { get; set; }
    public ICollection<JiChild> Children { get; set; } = new List<JiChild>();
    public ICollection<JiNote> Notes { get; set; } = new List<JiNote>();
}

public sealed class JiChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int SortKey { get; set; }
}

public sealed class JiNote
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int Rank { get; set; }
}

public sealed class NavParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ICollection<NavChild> Children { get; set; } = new List<NavChild>();
}

public sealed class NavChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
    public NavParent? Parent { get; set; }
}
