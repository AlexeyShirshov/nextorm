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
