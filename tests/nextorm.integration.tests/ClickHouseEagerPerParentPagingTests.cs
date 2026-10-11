using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #211 per-parent top-N eager loading against a real ClickHouse server. ClickHouse does not derive
/// <see cref="CommonTestSuite"/>, so the per-parent paging execution is pinned here against the shared
/// <c>eager_parent</c>/<c>eager_child</c> Memory tables. A skip is never a pass: the base suite reports
/// the unavailable-provider reason.
/// </summary>
public sealed class ClickHouseEagerPerParentPagingTests : ProviderTestSuite
{
    private static int _seed = -660_000_000;

    private static int NextBase() => Interlocked.Add(ref _seed, -10);

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private (int Rich, int Few, int Empty) Seed()
    {
        var ctx = _sut.DataProvider;
        var rich = NextBase();
        var few = rich + 1;
        var empty = rich + 2;

        ctx.CreateInsertBuilder<EagerParent>().Values([
            new EagerParent { Id = rich, Name = "rich" },
            new EagerParent { Id = few, Name = "few" },
            new EagerParent { Id = empty, Name = "empty" },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerChild>().Values([
            new EagerChild { Id = rich + 100, ParentId = rich, Name = "r1" },
            new EagerChild { Id = rich + 101, ParentId = rich, Name = "r2" },
            new EagerChild { Id = rich + 102, ParentId = rich, Name = "r3" },
            new EagerChild { Id = rich + 103, ParentId = rich, Name = "r4" },
            new EagerChild { Id = rich + 110, ParentId = few, Name = "f1" },
            new EagerChild { Id = rich + 111, ParentId = few, Name = "f2" },
        ]).Insert();

        return (rich, few, empty);
    }

    private EntityBuilder<EagerParent> Parents(int rich, int few, int empty)
        => _sut.DataProvider.From<EagerParent>().Where(p => p.Id == rich || p.Id == few || p.Id == empty);

    [Fact]
    [Trait("Feature", "D211")]
    public void ClickHouse_Split_Take_AppliesPerParent()
    {
        var (rich, few, empty) = Seed();

        var parents = Parents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(rich, few, empty);
        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 100, rich + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 110, rich + 111);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void ClickHouse_SingleQuery_SkipAndTake_AppliesPerParent()
    {
        var (rich, few, empty) = Seed();

        var parents = Parents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Offset(1).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 111);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void ClickHouse_Split_FilterPrecedesRankAndRootPagingStaysIndependent()
    {
        var (rich, few, empty) = Seed();

        var parents = Parents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().Where(x => x.Name != "r1").OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .Offset(1)
            .Limit(2)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(few, empty);
        // r1 was filtered before ranking, so `rich` would page to r2 -- but root paging dropped `rich`.
        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 110);
        parents[1].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void ClickHouse_CompositeKey_Split_Take_PartitionsByBothComponents()
    {
        var ctx = _sut.DataProvider;
        var b = NextBase();
        var tenantA = b;
        var tenantC = b + 1;
        var linkShared = b + 10;
        var linkOther = b + 20;

        ctx.CreateInsertBuilder<EagerCompositeParent>().Values([
            new EagerCompositeParent { RowId = b, Tenant = tenantA, LinkId = linkShared },
            new EagerCompositeParent { RowId = b + 1, Tenant = tenantA, LinkId = linkOther },
            new EagerCompositeParent { RowId = b + 2, Tenant = tenantC, LinkId = linkShared },
        ]).Insert();

        ctx.CreateInsertBuilder<EagerCompositeChild>().Values([
            new EagerCompositeChild { Id = b + 100, Tenant = tenantA, ParentId = linkShared, SortKey = 1 },
            new EagerCompositeChild { Id = b + 103, Tenant = tenantA, ParentId = linkShared, SortKey = 4 },
            new EagerCompositeChild { Id = b + 201, Tenant = tenantA, ParentId = linkOther, SortKey = 2 },
            new EagerCompositeChild { Id = b + 204, Tenant = tenantA, ParentId = linkOther, SortKey = 5 },
            new EagerCompositeChild { Id = b + 302, Tenant = tenantC, ParentId = linkShared, SortKey = 3 },
            new EagerCompositeChild { Id = b + 305, Tenant = tenantC, ParentId = linkShared, SortKey = 6 },
        ]).Insert();

        var parents = _sut.DataProvider.From<EagerCompositeParent>()
            .Where(p => p.RowId == b || p.RowId == b + 1 || p.RowId == b + 2)
            .LoadWith(
                p => p.Children,
                c => c.From<EagerCompositeChild>().OrderBy(x => x.SortKey).Limit(2),
                p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(b, b + 1, b + 2);
        parents[0].Children.Select(c => c.Id).Should().Equal(b + 100, b + 103);
        parents[1].Children.Select(c => c.Id).Should().Equal(b + 201, b + 204);
        parents[2].Children.Select(c => c.Id).Should().Equal(b + 302, b + 305);
    }
}
