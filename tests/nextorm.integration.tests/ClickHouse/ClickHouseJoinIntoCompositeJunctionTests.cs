using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D03 (#223): ClickHouse-owned runtime cases for a <b>composite junction</b> many-to-many
/// <c>JoinInto</c>. ClickHouse does not derive <see cref="CommonTestSuite"/>, so the shared-provider
/// graph/occurrence/null/query-count matrix is re-pinned here against a real server for both the
/// standalone and the SingleQuery dispatcher. A skip is never a pass.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseJoinIntoCompositeJunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void JoinInto_CompositeJunction_Standalone_ShouldStitchGraphPreservingOccurrences()
    {
        var (first, second) = JoinIntoCompositeJunctionFixtures.Seed(_sut.DataProvider);

        var interceptor = new JoinIntoQueryCountingInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = _sut.DataProvider.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .JoinInto(_sut.DataProvider.From<JiCompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        interceptor.Executing.Should().Be(1, "the composite junction stitches the graph from one denormalized round trip");

        parents.Select(p => p.RowId).Should().Equal([first, second]);
        parents[0].Children.Should().HaveCount(2, "two junction occurrences of the same pair remain distinct elements");
        parents[0].Children.Select(c => c.Id).Should().OnlyContain(id => id == first + 100,
            "the junction's last parent component routes child c1 to the first parent only");
        parents[0].Children.Should().NotContainNulls("the dangling child foreign key contributes no element");
        parents[1].Children.Select(c => c.Id).Should().Equal([first + 101],
            "the junction's last parent component routes child c2 to the second parent only");
    }

    [Fact]
    public void JoinInto_CompositeJunction_SingleQuery_ShouldStitchGraphAndRunOneQuery()
    {
        var (first, _) = JoinIntoCompositeJunctionFixtures.Seed(_sut.DataProvider);

        var interceptor = new JoinIntoQueryCountingInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var parents = _sut.DataProvider.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first)
            .JoinInto(_sut.DataProvider.From<JiCompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JoinIntoNote>(), p => p.RowId, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        interceptor.Executing.Should().Be(1, "SingleQuery stitches the composite graph in one materialization query");

        parents.Should().ContainSingle("the denormalized rows collapse to one parent");
        parents[0].Children.Should().HaveCount(2, "the repeated junction occurrence is preserved in the single query");
        parents[0].Children.Select(c => c.Id).Should().OnlyContain(id => id == first + 100);
        parents[0].Notes.Select(n => n.Id).Should().Equal([first + 200],
            "the SingleQuery LoadWith note is stitched beside the composite junction");
    }
}
