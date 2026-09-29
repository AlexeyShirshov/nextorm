using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Slice-B in-memory tests for functional <c>JoinInto</c> (#105): the denormalized rows are stitched
/// back into child collections with LEFT/INNER semantics, parent deduplication and per-collection
/// grouping; the non-list terminals see only the parent.
/// </summary>
public class JoinIntoInMemoryTests
{
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<MemoryJoinParent>(b => b.HasMany(p => p.Children, c => c.ParentId).HasMany(p => p.Notes, n => n.ParentId))
            .WithData([
                new MemoryJoinParent { Id = 1, Name = "p1" },
                new MemoryJoinParent { Id = 2, Name = "p2" },
                new MemoryJoinParent { Id = 3, Name = "p3" },
            ]);
        ctx.From<MemoryJoinChild>(b => b.HasOne(c => c.Parent, c => c.ParentId))
            .WithData([
                new MemoryJoinChild { Id = 10, ParentId = 1, Name = "c1" },
                new MemoryJoinChild { Id = 11, ParentId = 1, Name = "c2" },
                new MemoryJoinChild { Id = 12, ParentId = 2, Name = "c3" },
            ]);

        return ctx;
    }

    [Fact]
    public void LeftJoinInto_ShouldKeepChildlessParentsAndGroupChildren()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Should().HaveCount(3);
        parents.Select(p => p.Id).Should().Equal(1, 2, 3);

        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
        parents.Single(p => p.Id == 3).Children.Should().BeEmpty();
    }

    [Fact]
    public void InnerJoinInto_ShouldExcludeChildlessParents()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void CountAndAnyFirst_ShouldBeParentOnly()
    {
        using var ctx = CreateContext();
        var builder = ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        builder.Count().Should().Be(3);
        builder.Any().Should().BeTrue();
        builder.Where(p => p.Id == 1).Count().Should().Be(1);
        builder.Where(p => p.Id == 1).First().Children.Should().BeEmpty("non-list terminals do not stitch");
    }

    [Fact]
    public void MultipleCollections_ShouldGroupEachIndependently()
    {
        using var ctx = CreateContext();
        ctx.From<MemoryJoinNote>()
            .WithData([
                new MemoryJoinNote { Id = 100, ParentId = 1, Text = "n1" },
                new MemoryJoinNote { Id = 101, ParentId = 1, Text = "n2" },
            ]);

        var parents = ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(ctx.From<MemoryJoinNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        parents.Should().HaveCount(3);
        parents.Single(p => p.Id == 1).Children.Should().HaveCount(2);
        parents.Single(p => p.Id == 1).Notes.Should().HaveCount(2);
        parents.Single(p => p.Id == 2).Children.Should().HaveCount(1);
        parents.Single(p => p.Id == 2).Notes.Should().BeEmpty();
    }

    [Fact]
    public void Where_ShouldFilterParentsBeforeStitching()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Where(p => p.Id == 1)
            .ToList();

        parents.Should().HaveCount(1);
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public void ExplicitKeys_ShouldStitchWithoutRelationshipMetadata()
    {
        using var ctx = CreateContext();
        ctx.From<MemoryJoinNote>()
            .WithData([new MemoryJoinNote { Id = 100, ParentId = 1, Text = "n1" }]);

        var parents = ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes, p => p.Id, n => n.ParentId)
            .ToList();

        parents.Single(p => p.Id == 1).Notes.Select(n => n.Id).Should().Equal(100);
        parents.Single(p => p.Id == 2).Notes.Should().BeEmpty();
    }

    [Fact]
    public async Task LeftJoinInto_ToListAsync_ShouldMatchSyncStitching()
    {
        using var ctx = CreateContext();

        var parents = await ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToListAsync();

        parents.Should().HaveCount(3);
        parents.Select(p => p.Id).Should().Equal(1, 2, 3);

        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
        parents.Single(p => p.Id == 3).Children.Should().BeEmpty();
    }

    [Fact]
    public async Task InnerJoinInto_ToListAsync_ShouldExcludeChildlessParents()
    {
        using var ctx = CreateContext();

        var parents = await ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToListAsync();

        parents.Select(p => p.Id).Should().Equal(1, 2);
    }

    [Fact]
    public async Task MultipleCollections_ToListAsync_ShouldGroupEachIndependently()
    {
        using var ctx = CreateContext();
        ctx.From<MemoryJoinNote>()
            .WithData([
                new MemoryJoinNote { Id = 100, ParentId = 1, Text = "n1" },
                new MemoryJoinNote { Id = 101, ParentId = 1, Text = "n2" },
            ]);

        var parents = await ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(ctx.From<MemoryJoinNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToListAsync();

        parents.Should().HaveCount(3);
        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        parents.Single(p => p.Id == 1).Notes.Select(n => n.Id).Should().Equal(100, 101);
        parents.Single(p => p.Id == 2).Notes.Should().BeEmpty();
    }
}

public sealed class MemoryJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ICollection<MemoryJoinChild> Children { get; set; } = new List<MemoryJoinChild>();
    public ICollection<MemoryJoinNote> Notes { get; set; } = new List<MemoryJoinNote>();
}

public sealed class MemoryJoinChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
    public MemoryJoinParent? Parent { get; set; }
}

public sealed class MemoryJoinNote
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Text { get; set; }
}
