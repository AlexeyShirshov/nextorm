using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory tests for the one-to-one <c>JoinInto</c> overload (#135 slice A): a single child reference
/// is assigned per parent, LEFT leaves the reference null, INNER excludes the childless parent, a parent
/// matching more than one distinct child throws at materialization, and cartesian repeats of one child
/// caused by a neighbouring join are tolerated.
/// </summary>
public class JoinIntoOneToOneTests
{
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<OneToOneJoinParent>(b => b
                .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([
                new OneToOneJoinParent { Id = 1, Name = "p1" },
                new OneToOneJoinParent { Id = 2, Name = "p2" },
                new OneToOneJoinParent { Id = 3, Name = "p3" },
            ]);
        ctx.From<OneToOneJoinChild>().WithData([
            new OneToOneJoinChild { Id = 10, ParentId = 1, Name = "c1" },
            new OneToOneJoinChild { Id = 11, ParentId = 3, Name = "c2" },
            new OneToOneJoinChild { Id = 12, ParentId = 3, Name = "c3" },
        ]);
        ctx.From<OneToOneJoinNote>().WithData([
            new OneToOneJoinNote { Id = 100, ParentId = 1, Text = "n1" },
            new OneToOneJoinNote { Id = 101, ParentId = 1, Text = "n2" },
        ]);

        return ctx;
    }

    [Fact]
    public void LeftJoinInto_ShouldAssignTheChildOrLeaveTheReferenceNull()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1 || p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Primary!.Id.Should().Be(10, "the matching child is assigned to the single reference");
        parents[1].Primary.Should().BeNull("a LEFT join without a matching child leaves the reference null");
    }

    [Fact]
    public void InnerJoinInto_ShouldExcludeTheChildlessParent()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1 || p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary, JoinType.Inner)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(1);
        parents[0].Primary!.Id.Should().Be(10);
    }

    [Fact]
    public void MoreThanOneDistinctChild_ShouldThrowAtMaterialization()
    {
        using var ctx = CreateContext();

        Action act = () => ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 3)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToList();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    [Fact]
    public void CartesianRepeatOfTheSameChild_ShouldNotThrowAndShouldAssignOnce()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .JoinInto(ctx.From<OneToOneJoinNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Primary!.Id.Should().Be(10, "the repeated child rows collapse to the one source occurrence");
        parents[0].Notes.Select(n => n.Id).Should().Equal(100, 101);
    }

    [Fact]
    public async Task OneToOneJoinInto_ToListAsync_ShouldMatchSyncStitching()
    {
        using var ctx = CreateContext();

        var parents = await ctx.From<OneToOneJoinParent>()
            .Where(p => p.Id == 1 || p.Id == 2)
            .JoinInto(ctx.From<OneToOneJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .OrderBy(p => p.Id)
            .ToListAsync();

        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Primary!.Id.Should().Be(10);
        parents[1].Primary.Should().BeNull();
    }
}

public sealed class OneToOneJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public OneToOneJoinChild? Primary { get; set; }
    public ICollection<OneToOneJoinNote> Notes { get; set; } = new List<OneToOneJoinNote>();
}

public sealed class OneToOneJoinChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class OneToOneJoinNote
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Text { get; set; }
}
