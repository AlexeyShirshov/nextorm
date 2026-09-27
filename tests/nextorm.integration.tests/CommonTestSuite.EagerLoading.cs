using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static int _eagerIdSeed = -1_000_000;

    private static int NextEagerId() => Interlocked.Add(ref _eagerIdSeed, -1_000);

    [Fact]
    public void EagerLoading_LoadWith_FillsCollectionsForZeroOneAndManyChildren()
    {
        var ctx = _sut.DataProvider;
        var zero = NextEagerId();
        var one = zero + 1;
        var many = zero + 2;

        ctx.InsertInto<EagerParent>().Values([
            new EagerParent { Id = zero, Name = "zero" },
            new EagerParent { Id = one, Name = "one" },
            new EagerParent { Id = many, Name = "many" },
        ]).Insert();

        ctx.InsertInto<EagerChild>().Values([
            new EagerChild { Id = zero + 100, ParentId = one, Name = "one-a" },
            new EagerChild { Id = zero + 103, ParentId = many, Name = "many-c" },
            new EagerChild { Id = zero + 101, ParentId = many, Name = "many-a" },
            new EagerChild { Id = zero + 104, ParentId = many, Name = "many-d" },
            new EagerChild { Id = zero + 102, ParentId = many, Name = "many-b" },
        ]).Insert();

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == zero || p.Id == one || p.Id == many)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(zero, one, many);
        parents[0].Children.Should().BeEmpty();
        parents[1].Children.Select(c => c.Name).Should().Equal("one-a");
        parents[2].Children.Select(c => c.Name).Should().Equal("many-a", "many-b", "many-c", "many-d");
    }

    [Fact]
    public void EagerLoading_LoadWith_GroupsChildrenPerParentWithoutCrossAssignment()
    {
        var ctx = _sut.DataProvider;
        var first = NextEagerId();
        var second = first + 1;
        var orphan = first + 2;

        ctx.InsertInto<EagerParent>().Values([
            new EagerParent { Id = first, Name = "first" },
            new EagerParent { Id = second, Name = "second" },
        ]).Insert();

        var firstA = first + 100;
        var firstB = first + 101;
        var secondA = first + 102;
        ctx.InsertInto<EagerChild>().Values([
            new EagerChild { Id = firstA, ParentId = first, Name = "first-a" },
            new EagerChild { Id = secondA, ParentId = second, Name = "second-a" },
            new EagerChild { Id = firstB, ParentId = first, Name = "first-b" },
            new EagerChild { Id = first + 103, ParentId = orphan, Name = "orphan" },
        ]).Insert();

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == first || p.Id == second)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(first, second);
        parents[0].Children.Select(c => c.Id).Should().Equal(firstA, firstB);
        parents[1].Children.Select(c => c.Id).Should().Equal(secondA);
        parents.SelectMany(p => p.Children).Should().HaveCount(3);
        parents.SelectMany(p => p.Children).Select(c => c.ParentId).Distinct().Should().Equal(first, second);
    }

    [Fact]
    public void EagerLoading_LoadWith_NoMatchingParents_ReturnsEmptyList()
    {
        var ctx = _sut.DataProvider;
        var missing = NextEagerId();

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == missing)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .ToList();

        parents.Should().BeEmpty();
    }

    [Fact]
    public async Task EagerLoading_LoadWithAsync_FillsCollections()
    {
        var ctx = _sut.DataProvider;
        var parent = NextEagerId();
        var childA = parent + 100;
        var childB = parent + 101;

        ctx.InsertInto<EagerParent>().Values([
            new EagerParent { Id = parent, Name = "async" },
        ]).Insert();

        ctx.InsertInto<EagerChild>().Values([
            new EagerChild { Id = childA, ParentId = parent, Name = "async-a" },
            new EagerChild { Id = childB, ParentId = parent, Name = "async-b" },
        ]).Insert();

        var parents = await ctx.From<EagerParent>()
            .Where(p => p.Id == parent)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(childA, childB);
    }
}
