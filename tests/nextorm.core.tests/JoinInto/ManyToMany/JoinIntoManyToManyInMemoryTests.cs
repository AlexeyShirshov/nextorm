using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory stitching tests for the many-to-many <c>JoinInto</c> (#135): the junction rows drive the
/// child collection, LEFT keeps a parent without a junction row (empty collection) while INNER drops it,
/// a dangling child foreign key contributes no element, duplicate junction rows for one
/// <c>(parent, child)</c> pair are preserved as distinct elements, a user predicate filters the children
/// and a default-valued child key is a legitimate child rather than a missing one.
/// </summary>
public class JoinIntoManyToManyInMemoryTests
{
    public JoinIntoManyToManyInMemoryTests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
                .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([
                new ManyToManyJoinParent { Id = 1, Name = "p1" },
                new ManyToManyJoinParent { Id = 2, Name = "p2" },
                new ManyToManyJoinParent { Id = 3, Name = "p3" },
            ]);
        ctx.From<ManyToManyJoinChild>().WithData([
            new ManyToManyJoinChild { Id = 10, Name = "keep" },
            new ManyToManyJoinChild { Id = 11, Name = "drop" },
            new ManyToManyJoinChild { Id = 0, Name = "zero" },
        ]);
        ctx.From<ManyToManyJoinLink>().WithData([
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 11 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 0 },
            new ManyToManyJoinLink { ParentId = 3, ChildId = 99 },
        ]);

        return ctx;
    }

    [Fact]
    public void DuplicateJunctionRowsPreserved_ShouldKeepBothElements()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Where(c => c.Id == 10).Should().HaveCount(2,
            "two distinct junction rows for the same (parent, child) pair are two elements");
        parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(0, 10, 10, 11);
    }

    [Fact]
    public void MissingJunctionLeftKeepsParent()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 2)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Id.Should().Be(2);
        parents[0].Children.Should().BeEmpty("a LEFT join keeps the parent when it has no junction row");
    }

    [Fact]
    public void MissingJunctionInnerExcludesParent()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 2)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children, JoinType.Inner)
            .ToList();

        parents.Should().BeEmpty("an INNER join excludes a parent without a junction row");
    }

    [Fact]
    public void DanglingChildYieldsNoElement()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 3)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Id.Should().Be(3);
        parents[0].Children.Should().BeEmpty("a junction row whose child does not exist contributes no element");
    }

    [Fact]
    public void PredicateInChildOn_ShouldFilterChildren()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => c.Name == "keep", p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().OnlyContain(c => c.Id == 10, "the user predicate filters the joined children");
        parents[0].Children.Should().HaveCount(2, "both junction rows for the retained child survive");
    }

    [Fact]
    public void NullChild_DefaultKeyIsARealChild()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => c.Id == 0, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().ContainSingle(
            "a child whose key is the type default is a legitimate child, not a missing one");
        var child = parents[0].Children.Single();
        child.Id.Should().Be(0);
        child.Name.Should().Be("zero");
    }

    [Fact]
    public void SameParentChildDistinctOccurrences_ShouldKeepBothElements()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
                .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([new ManyToManyJoinParent { Id = 1, Name = "p1" }]);
        ctx.From<ManyToManyJoinChild>().WithData([new ManyToManyJoinChild { Id = 10, Name = "child" }]);
        // Two distinct junction instances for the same (parent, child): the occurrence token differs, so
        // the dedup key treats them as two elements (the ReferenceEquals branch of OccurrenceEquals).
        ctx.From<ManyToManyJoinLink>().WithData([
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
        ]);

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().HaveCount(2, "distinct junction instances are distinct occurrences");
    }

    [Fact]
    public void SameParentChildAndOccurrence_ShouldCollapseToOneElement()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
                .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([new ManyToManyJoinParent { Id = 1, Name = "p1" }]);
        // Two child rows share the key 10, so the single junction row matches both. Every key component
        // is identical (same parent, same junction child key, same junction instance), so the second
        // element collapses into the first (the equal-occurrence branch of OccurrenceEquals).
        ctx.From<ManyToManyJoinChild>().WithData([
            new ManyToManyJoinChild { Id = 10, Name = "first" },
            new ManyToManyJoinChild { Id = 10, Name = "second" },
        ]);
        ctx.From<ManyToManyJoinLink>().WithData([new ManyToManyJoinLink { ParentId = 1, ChildId = 10 }]);

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().ContainSingle("an identical (parent, child, occurrence) key is one element");
    }

    [Fact]
    public void DistinctChildren_ShouldYieldDistinctElements()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
                .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([new ManyToManyJoinParent { Id = 1, Name = "p1" }]);
        ctx.From<ManyToManyJoinChild>().WithData([
            new ManyToManyJoinChild { Id = 10, Name = "ten" },
            new ManyToManyJoinChild { Id = 11, Name = "eleven" },
        ]);
        ctx.From<ManyToManyJoinLink>().WithData([
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 11 },
        ]);

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
    }

    [Fact]
    public void DanglingChildInnerExcludesParent()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 3)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children, JoinType.Inner)
            .ToList();

        parents.Should().BeEmpty(
            "an INNER join drops a parent whose only junction row references a child that does not exist");
    }

    [Fact]
    public void DanglingJunctionRowDoesNotSuppressMatchingChild()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
                .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([new ManyToManyJoinParent { Id = 1, Name = "p1" }]);
        ctx.From<ManyToManyJoinChild>().WithData([new ManyToManyJoinChild { Id = 10, Name = "exists" }]);
        // One junction row matches the child, the other references a missing child 99: the dangling row
        // contributes no element but must not drop the matching one.
        ctx.From<ManyToManyJoinLink>().WithData([
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 99 },
        ]);

        var parents = ctx.From<ManyToManyJoinParent>()
            .Where(p => p.Id == 1)
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10);
    }

    [Fact]
    public void NullOccurrenceComparison_IsDefensiveOnly()
    {
        // No observable many-to-many path can produce a null occurrence: the stitcher drops a null link
        // before it builds the dedup key, and both providers tokenize the occurrence with a non-null value
        // (the junction instance in memory, the boxed row_number() on SQL). The null guard is therefore
        // reachable only by the private dedup key itself, so it is exercised directly.
        var keyType = typeof(JoinIntoManyToManySpec<,>)
            .GetNestedType("ManyToManyLinkKey", BindingFlags.NonPublic)!
            .MakeGenericType(typeof(ManyToManyJoinParent), typeof(ManyToManyJoinChild));
        var occurrenceEquals = keyType
            .GetMethod("OccurrenceEquals", BindingFlags.NonPublic | BindingFlags.Static)!;

        occurrenceEquals.Invoke(null, [null, null, true]).Should().Be(true);
        occurrenceEquals.Invoke(null, [null, new object(), true]).Should().Be(false);
        occurrenceEquals.Invoke(null, [new object(), null, false]).Should().Be(false);

        // The object-typed Equals overload is likewise only reachable when a caller compares the key as
        // object; an observable run always compares through IEquatable, so it is exercised here too.
        var key = Activator.CreateInstance(keyType, [null, null, null, true])!;
        var equalsObject = keyType.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance, [typeof(object)])!;
        equalsObject.Invoke(key, [key]).Should().Be(true);
        equalsObject.Invoke(key, [null]).Should().Be(false);
    }

    [Fact]
    public void MixedCardinalities_ShouldPopulateEveryNavigation()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<MixedCardinalityParent>(b => b
                .HasOneToOne(p => p.Profile, p => p.Id, c => c.ParentId)
                .HasMany(p => p.Children, c => c.ParentId)
                .HasManyThrough<MixedCardinalityTag, MixedCardinalityTagLink, int, int>(
                    p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.TagId))
            .WithData([new MixedCardinalityParent { Id = 1, Name = "p1" }]);
        ctx.From<MixedCardinalityProfile>().WithData([
            new MixedCardinalityProfile { Id = 50, ParentId = 1, Name = "profile" },
        ]);
        ctx.From<MixedCardinalityChild>().WithData([
            new MixedCardinalityChild { Id = 60, ParentId = 1, Name = "child" },
        ]);
        ctx.From<MixedCardinalityTag>().WithData([
            new MixedCardinalityTag { Id = 70, Name = "tag" },
        ]);
        ctx.From<MixedCardinalityTagLink>().WithData([
            new MixedCardinalityTagLink { ParentId = 1, TagId = 70 },
        ]);

        var parents = ctx.From<MixedCardinalityParent>()
            .JoinInto(ctx.From<MixedCardinalityProfile>(), (p, c) => p.Id == c.ParentId, p => p.Profile)
            .JoinInto(ctx.From<MixedCardinalityChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(ctx.From<MixedCardinalityTag>(), (p, c) => true, p => p.Tags)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Profile!.Id.Should().Be(50);
        parents[0].Children.Select(c => c.Id).Should().Equal(60);
        parents[0].Tags.Select(t => t.Id).Should().Equal(70);
    }
}

public sealed class ManyToManyJoinParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ICollection<ManyToManyJoinChild> Children { get; set; } = new List<ManyToManyJoinChild>();
}

public sealed class ManyToManyJoinChild
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class ManyToManyJoinLink
{
    public int ParentId { get; set; }
    public int ChildId { get; set; }
}

public sealed class MixedCardinalityParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public MixedCardinalityProfile? Profile { get; set; }
    public ICollection<MixedCardinalityChild> Children { get; set; } = new List<MixedCardinalityChild>();
    public ICollection<MixedCardinalityTag> Tags { get; set; } = new List<MixedCardinalityTag>();
}

public sealed class MixedCardinalityProfile
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class MixedCardinalityChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class MixedCardinalityTag
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class MixedCardinalityTagLink
{
    public int ParentId { get; set; }
    public int TagId { get; set; }
}
