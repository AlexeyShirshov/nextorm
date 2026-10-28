using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D5 (#330) ClickHouse adapter: ClickHouse does not derive <see cref="CommonTestSuite"/>, so the live
/// equivalence of the source-less <c>JoinInto</c> forms is re-pinned here against a real server. It reuses
/// <see cref="NavConScenarios.Seed"/>, which already carries the unmatched-parent and multi-child shapes the
/// comparison depends on, and the class is fail-closed through <see cref="ProviderTestSuite"/> — no skip and
/// no <c>Supports*</c> guard is introduced for <c>JoinInto</c>, mirroring the existing ClickHouse
/// <c>JoinInto</c> remainder coverage.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseJoinIntoSourceLessTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static object[] Shape(IEnumerable<JoinIntoParent> parents)
        => parents
            .OrderBy(p => p.Id)
            .Select(p => (object)new
            {
                p.Id,
                p.Name,
                Children = p.Children.Select(c => c.Id).OrderBy(id => id).ToArray(),
                PrimaryChild = p.PrimaryChild?.Id,
            })
            .ToArray();

    [Fact]
    public void JoinIntoSourceLess_CollectionLeft_ShouldMatchTheExplicitBareSource()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        var sourceLess = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 1 || p.Id == f + 2)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 1 || p.Id == f + 2)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal([f, f + 1, f + 2], "a LEFT join keeps every parent");
        sourceLess.Single(p => p.Id == f).Children.Should().HaveCount(2);
        sourceLess.Single(p => p.Id == f + 2).Children.Should().BeEmpty("the childless parent survives empty");
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_CollectionInner_ShouldMatchTheExplicitBareSource()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        var sourceLess = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 1 || p.Id == f + 2)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 1 || p.Id == f + 2)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .OrderBy(p => p.Id)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal([f, f + 1], "an INNER join drops the childless parent");
        sourceLess.Sum(p => p.Children.Count).Should().Be(explicitBare.Sum(p => p.Children.Count));
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_ReferenceLeft_ShouldMatchTheExplicitBareSource()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        // "f" has two children and would trip the one-to-one backstop in both calls.
        var sourceLess = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1 || p.Id == f + 2)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1 || p.Id == f + 2)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal([f + 1, f + 2]);
        sourceLess.Single(p => p.Id == f + 2).PrimaryChild.Should().BeNull();
        sourceLess.Single(p => p.Id == f + 1).PrimaryChild!.Id.Should()
            .Be(explicitBare.Single(p => p.Id == f + 1).PrimaryChild!.Id);
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_ReferenceInner_ShouldMatchTheExplicitBareSource()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        // "f" has two children and would trip the one-to-one backstop in both calls.
        var sourceLess = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1 || p.Id == f + 2)
            .JoinInto<JoinIntoChild>((p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Inner)
            .ToList();

        var explicitBare = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f + 1 || p.Id == f + 2)
            .JoinInto(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.PrimaryChild, JoinType.Inner)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal([f + 1], "an INNER one-to-one join excludes a parent with no matching child");
        sourceLess[0].PrimaryChild.Should().NotBeNull();
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void JoinIntoSourceLess_KeyForm_ShouldMatchTheExplicitBareSource()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        var sourceLess = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 1 || p.Id == f + 2)
            .JoinInto<JoinIntoChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        var explicitBare = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == f || p.Id == f + 1 || p.Id == f + 2)
            .JoinInto<JoinIntoChild, int>(_sut.DataProvider.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        sourceLess.Select(p => p.Id).Should().Equal([f, f + 1, f + 2], "the key form exposes only the LEFT mode");
        sourceLess.Single(p => p.Id == f + 2).Children.Should().BeEmpty();
        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }
}
