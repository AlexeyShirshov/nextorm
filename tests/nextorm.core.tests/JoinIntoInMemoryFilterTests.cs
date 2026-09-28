using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Global query filters (#67) on the in-memory <c>JoinInto</c> path (#105 slice B): the parent and the
/// joined child both honor their declared filters, and <c>IgnoreFilters</c> disables both.
/// </summary>
public class JoinIntoInMemoryFilterTests
{
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<FilteredJoinParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasQueryFilter(p => !p.IsDeleted))
            .WithData(
            [
                new FilteredJoinParent { Id = 1, Name = "keep" },
                new FilteredJoinParent { Id = 2, Name = "gone", IsDeleted = true },
                new FilteredJoinParent { Id = 3, Name = "empty" },
            ]);
        ctx.From<FilteredJoinChild>(b => b
                .HasOne(c => c.Parent, c => c.ParentId)
                .HasQueryFilter(c => !c.IsDeleted))
            .WithData(
            [
                new FilteredJoinChild { Id = 10, ParentId = 1, Name = "keep" },
                new FilteredJoinChild { Id = 11, ParentId = 1, Name = "gone", IsDeleted = true },
                new FilteredJoinChild { Id = 12, ParentId = 2, Name = "child-of-gone" },
            ]);

        return ctx;
    }

    [Fact]
    public void ParentFilter_ShouldExcludeFilteredParents()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<FilteredJoinParent>()
            .JoinInto(ctx.From<FilteredJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 3);
    }

    [Fact]
    public void ChildFilter_ShouldExcludeFilteredChildren()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<FilteredJoinParent>()
            .JoinInto(ctx.From<FilteredJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);
        parents.Single(p => p.Id == 3).Children.Should().BeEmpty();
    }

    [Fact]
    public void IgnoreFilters_ShouldDisableParentAndChildFilters()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<FilteredJoinParent>()
            .JoinInto(ctx.From<FilteredJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .IgnoreFilters()
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2, 3);
        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
    }

    private static InMemoryDataContext CreateTwoJoinContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<TwoJoinParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([new TwoJoinParent { Id = 1, Name = "p1" }]);
        ctx.From<TwoJoinChild>().WithData([new TwoJoinChild { Id = 10, ParentId = 1 }]);
        ctx.From<TwoJoinNote>(b => b
                .HasOne(n => n.Parent, n => n.ParentId)
                .HasQueryFilter(n => !n.IsDeleted))
            .WithData([
                new TwoJoinNote { Id = 100, ParentId = 1, Text = "keep" },
                new TwoJoinNote { Id = 101, ParentId = 1, Text = "gone", IsDeleted = true },
            ]);

        return ctx;
    }

    [Fact]
    public void SecondJoinInto_WithChildFilter_ShouldStillFilterTheSecondCollection()
    {
        using var ctx = CreateTwoJoinContext();

        var parents = ctx.From<TwoJoinParent>()
            .JoinInto(ctx.From<TwoJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(ctx.From<TwoJoinNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10);
        parents[0].Notes.Select(n => n.Id).Should().Equal(new[] { 100 }, "the second JoinInto's child global filter must still apply");
    }
}

public sealed class FilteredJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<FilteredJoinChild> Children { get; set; } = new List<FilteredJoinChild>();
}

public sealed class FilteredJoinChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
    public bool IsDeleted { get; set; }
    public FilteredJoinParent? Parent { get; set; }
}

public sealed class TwoJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ICollection<TwoJoinChild> Children { get; set; } = new List<TwoJoinChild>();
    public ICollection<TwoJoinNote> Notes { get; set; } = new List<TwoJoinNote>();
}

public sealed class TwoJoinChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
}

public sealed class TwoJoinNote
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Text { get; set; }
    public bool IsDeleted { get; set; }
    public TwoJoinParent? Parent { get; set; }
}
