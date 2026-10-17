using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Regression tests for the <c>JoinInto</c> stitching path (#105): per-parent child deduplication,
/// single-application parent paging, unambiguous explicit-key plan identity and the null-row rule.
/// </summary>
public class JoinIntoStitchingRegressionTests
{
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<StitchParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([
                new StitchParent { Id = 1, Name = "p1" },
                new StitchParent { Id = 2, Name = "p2" },
                new StitchParent { Id = 3, Name = "p3" },
            ]);
        ctx.From<StitchChild>()
            .WithData([
                new StitchChild { Id = 10, ParentId = 1, Name = "c1" },
                new StitchChild { Id = 11, ParentId = 1, Name = "c2" },
            ]);
        ctx.From<StitchNote>().WithData([]);
        return ctx;
    }

    [Fact]
    public void SharedChildIdentityAcrossParents_ShouldAssignToBothParents()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<StitchParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([new StitchParent { Id = 1 }, new StitchParent { Id = 2 }]);
        ctx.From<StitchChild>().WithData([
            new StitchChild { Id = 5, ParentId = 1, Name = "a" },
            new StitchChild { Id = 5, ParentId = 2, Name = "b" },
        ]);

        var parents = ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Single(p => p.Id == 1).Children.Select(c => c.Name).Should().Equal("a");
        parents.Single(p => p.Id == 2).Children.Select(c => c.Name).Should().Equal("b");
    }

    [Fact]
    public void SharedParentKey_ShouldNotDuplicateTheSharedChild()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<StitchParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([new StitchParent { Id = 1, Name = "shared" }, new StitchParent { Id = 2, Name = "shared" }]);
        ctx.From<StitchChild>().WithData([new StitchChild { Id = 5, ParentId = 9, Name = "shared" }]);

        var parents = ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Name == c.Name, p => p.Children, p => p.Name, c => c.Name)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Should().OnlyContain(p => p.Children.Count == 1, "a child shared by two parents must be kept once for each");
    }

    [Fact]
    public void CartesianDuplicates_ShouldCollapseWithinEachParent()
    {
        using var ctx = CreateContext();
        ctx.From<StitchNote>().WithData([
            new StitchNote { Id = 100, ParentId = 1, Text = "n1" },
            new StitchNote { Id = 101, ParentId = 1, Text = "n2" },
        ]);

        var parents = ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(ctx.From<StitchNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        parents.Single(p => p.Id == 1).Notes.Select(n => n.Id).Should().Equal(100, 101);
    }

    [Fact]
    public void SkipTakeNotFromZero_ShouldPageParentsExactlyOnce()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Offset(1)
            .Limit(1)
            .ToList();

        parents.Should().HaveCount(1);
        parents[0].Id.Should().Be(2);
        parents[0].Children.Should().BeEmpty();
    }

    [Fact]
    public void NonMemberExplicitKey_ShouldThrowNotSupported()
    {
        using var ctx = CreateContext();

        Action parentKey = () => ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id + 1, c => c.ParentId)
            .ToList();

        Action childKey = () => ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId + 1)
            .ToList();

        parentKey.Should().Throw<NotSupportedException>()
            .WithMessage("*simple property or field accesses*");
        childKey.Should().Throw<NotSupportedException>()
            .WithMessage("*simple property or field accesses*");
    }

    [Fact]
    public void SortingWithJoinInto_ShouldOrderParents()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderByDescending(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(3, 2, 1);
    }

    [Fact]
    public void GlobalModifierOnJoinInto_ShouldThrowInMemory()
    {
        using var ctx = CreateContext();

        Action act = () => ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(ctx.From<StitchNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes, j => j.Global())
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*GLOBAL*");
    }

    [Fact]
    public void FirstRowWithNullParent_ShouldBeSkipped()
    {
        using var ctx = CreateContext();
        var builder = ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        var rows = new List<object?>
        {
            new Projection<StitchParent, StitchChild> { Item1 = null!, Item2 = new StitchChild { Id = 99, ParentId = 9 } },
            new Projection<StitchParent, StitchChild> { Item1 = new StitchParent { Id = 1 }, Item2 = new StitchChild { Id = 10, ParentId = 1 } },
        };

        var parents = JoinIntoStitcher.Stitch(builder, builder.JoinIntos!, rows);

        parents.Should().ContainSingle();
        parents[0].Id.Should().Be(1);
        parents[0].Children.Select(c => c.Id).Should().Equal(10);
    }
}

public sealed class StitchParent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<StitchChild> Children { get; set; } = new List<StitchChild>();
    public ICollection<StitchNote> Notes { get; set; } = new List<StitchNote>();
}

public sealed class StitchChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class StitchNote
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Text { get; set; }
}
