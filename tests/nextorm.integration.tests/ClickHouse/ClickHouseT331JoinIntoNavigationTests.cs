using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T331 (#331) — ClickHouse-owned live cells. ClickHouse does not derive <see cref="CommonTestSuite"/>, so
/// the navigation-first matrix is re-pinned here against a real server over the shared eager fixtures:
/// the metadata-derived <c>ON</c>, the LEFT preservation of unmatched parents, the filter as an additional
/// conjunct and the inherited junction synthesis.
/// <para>
/// The junction cell compares the navigation form against the T330 source-less form on the same rows
/// instead of pinning tag ids, so it proves inheritance of the existing synthesis (including the
/// documented duplicate-junction rule) without restating the seed.
/// </para>
/// </summary>
[Trait("Category", "T331")]
public sealed class ClickHouseT331JoinIntoNavigationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void T331_Collection_NavigationOnly_ShouldGroupDeclaredChildren()
    {
        var many = NavConScenarios.Seed(_sut.DataProvider);
        var one = many + 1;
        var none = many + 2;

        var parents = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([many, one, none], "a LEFT join keeps every parent");
        parents.Single(p => p.Id == many).Children.Select(c => c.Id).OrderBy(id => id).Should()
            .Equal([many + 100, many + 101]);
        parents.Single(p => p.Id == one).Children.Select(c => c.Id).Should().Equal([many + 102]);
        parents.Single(p => p.Id == none).Children.Should().BeEmpty();
        parents.SelectMany(p => p.Children.Select(c => c.Id)).Should().NotContain([many + 103, many + 104],
            "a dangling foreign key and a default foreign key match no parent");
    }

    [Fact]
    public void T331_Collection_ChildFilter_ShouldKeepTheFilteredOutParentWithAnEmptyCollection()
    {
        var many = NavConScenarios.Seed(_sut.DataProvider);
        var one = many + 1;
        var none = many + 2;

        // ON-vs-WHERE: the filter removes both of `many`'s children; if it were pushed into a WHERE the
        // parent row would disappear, so asserting the row is present is the oracle.
        var parents = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .JoinInto(p => p.Children, (p, c) => c.Id == many + 102)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([many, one, none]);
        parents.Single(p => p.Id == many).Children.Should().BeEmpty();
        parents.Single(p => p.Id == one).Children.Select(c => c.Id).Should().Equal([many + 102]);
        parents.Single(p => p.Id == none).Children.Should().BeEmpty();
    }

    [Fact]
    public void T331_Reference_NavigationOnly_ShouldAssignTheSingleChildAndKeepNullForTheChildlessParent()
    {
        var many = NavConScenarios.Seed(_sut.DataProvider);
        var one = many + 1;
        var none = many + 2;

        var parents = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == one || p.Id == none)
            .JoinInto(p => p.PrimaryChild)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal([one, none]);
        parents.Single(p => p.Id == one).PrimaryChild!.Id.Should().Be(many + 102);
        parents.Single(p => p.Id == none).PrimaryChild.Should().BeNull("no phantom default child");
    }

    [Fact]
    public void T331_Junction_ShouldInheritTheExistingSynthesisExactly()
    {
        var many = NavConScenarios.Seed(_sut.DataProvider);

        static string[] Shape(IEnumerable<JoinIntoParent> parents)
            => parents.OrderBy(p => p.Id)
                .Select(p => $"{p.Id}:{string.Join(",", p.Tags.Select(t => t.Id).OrderBy(id => id))}")
                .ToArray();

        var navigationFirst = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == many || p.Id == many + 1 || p.Id == many + 2)
            .JoinInto(p => p.Tags)
            .ToList();

        var sourceLess = _sut.DataProvider.From<JoinIntoParent>()
            .Where(p => p.Id == many || p.Id == many + 1 || p.Id == many + 2)
            .JoinInto<JoinIntoTag>((p, t) => t.Id >= many + 300, p => p.Tags)
            .ToList();

        Shape(navigationFirst).Should().Equal(Shape(sourceLess),
            "a junction navigation reuses the existing source-less synthesis");
    }
}
