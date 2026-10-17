using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #211 per-parent top-N eager loading against a real server: a child query's own
/// <c>Limit</c>/<c>Offset</c>/<c>Page</c> is applied independently to each parent on both eager modes
/// (split and single-query). Root paging, empty-parent preservation add batching stay intact.
/// </summary>
public abstract partial class CommonTestSuite
{
    /// <summary>
    /// Seeds three fresh parents: <c>rich</c> with four children, <c>few</c> with two and <c>empty</c>
    /// with none. Child ids are strictly increasing so ordering by id is a total order.
    /// </summary>
    private (int Rich, int Few, int Empty) SeedPerParentPaging()
    {
        var ctx = _sut.DataProvider;
        var rich = NextEagerId();
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

    private EntityBuilder<EagerParent> PerParentPagingParents(int rich, int few, int empty)
        => _sut.DataProvider.From<EagerParent>().Where(p => p.Id == rich || p.Id == few || p.Id == empty);

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_Split_Take_AppliesPerParentIndependently()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(rich, few, empty);
        // A globally limited child query would give parent `rich` the first two rows and starve `few`.
        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 100, rich + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 110, rich + 111);
        parents[2].Children.Should().BeEmpty("a parent with no qualifying children is preserved");
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_SingleQuery_Take_AppliesPerParentIndependently()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(rich, few, empty);
        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 100, rich + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 110, rich + 111);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_Split_SkipAndTake_AppliesPerParent()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Offset(1).Limit(1), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 111);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_Split_FilterPrecedesRank()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        // The lowest child (r1) is filtered out before ranking, so the first page starts at r2.
        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().Where(x => x.Name != "r1").OrderBy(x => x.Id).Limit(2), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 101, rich + 102);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 110, rich + 111);
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_Split_DescendingOrder_AppliesPerParent()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderByDescending(x => x.Id).Limit(2), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 103, rich + 102);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 111, rich + 110);
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_RootPaging_IsIndependentOfChildPaging()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        // Root Skip(1)/Take(2) selects `few` and `empty`; the child page still applies per parent.
        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .Offset(1)
            .Limit(2)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(few, empty);
        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 110);
        parents[1].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_UnpagedChild_LoadsEveryChild()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Should().HaveCount(4);
        parents[1].Children.Should().HaveCount(2);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_Split_IsBatchedNotNPlusOne()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var interceptor = new CountingQueryInterceptor();
        var ctx = (DataContext)_sut.DataProvider;
        ctx.AddInterceptor(interceptor);

        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        // One parent statement plus one batched ranked child statement: two round trips, never N+1.
        interceptor.Executing.Should().Be(2);
        interceptor.Executed.Should().Be(2);
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_SingleQuery_IsOneCommand()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var interceptor = new CountingQueryInterceptor();
        var ctx = (DataContext)_sut.DataProvider;
        ctx.AddInterceptor(interceptor);

        var parents = PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        interceptor.Executing.Should().Be(1, "the ranked per-parent paging stays one denormalized command");
        interceptor.Executed.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_SingleQuery_TwoPagedNavigations_AreIndependent()
    {
        var (many, one, none) = SeedJoinInto();

        var parents = JoinIntoParents()
            .Where(p => p.Id == many || p.Id == one || p.Id == none)
            .LoadWith(p => p.Children, c => c.From<JoinIntoChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .LoadWith(p => p.Notes, n => n.From<JoinIntoNote>().OrderBy(x => x.Id).Limit(1), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(many, one, none);
        // Each navigation ranks independently within its own partition: one row per parent per collection.
        parents[0].Children.Select(c => c.Id).Should().Equal(many + 100);
        parents[0].Notes.Select(n => n.Id).Should().Equal(many + 200);
        parents[1].Children.Select(c => c.Id).Should().Equal(many + 102);
        parents[1].Notes.Should().BeEmpty();
        parents[2].Children.Should().BeEmpty();
        parents[2].Notes.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public async Task EagerPerParentPaging_Split_Async_AppliesPerParent()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var parents = await PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Limit(1), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 100);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 110);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "D211")]
    public async Task EagerPerParentPaging_SingleQuery_Async_AppliesPerParent()
    {
        var (rich, few, empty) = SeedPerParentPaging();

        var parents = await PerParentPagingParents(rich, few, empty)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id).Offset(1).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents[0].Children.Select(c => c.Id).Should().Equal(rich + 101);
        parents[1].Children.Select(c => c.Id).Should().Equal(rich + 111);
        parents[2].Children.Should().BeEmpty();
    }

    /// <summary>
    /// Seeds three parents whose composite link key <c>(tenant, link_id)</c> overlaps on one component
    /// each (<c>(t,l1)</c>, <c>(t,l2)</c>, <c>(t2,l1)</c>), with interleaved child sort values. The
    /// unique <c>row_id</c> table key keeps rows from different runs apart while the link key stays
    /// adversarial: a partition on either component alone, or a global page, changes every id set.
    /// </summary>
    private (int Base, int LinkShared, int LinkOther, int TenantOther) SeedPerParentComposite()
    {
        var ctx = _sut.DataProvider;
        var b = NextEagerId();
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
            new EagerCompositeChild { Id = b + 106, Tenant = tenantA, ParentId = linkShared, SortKey = 7 },
            new EagerCompositeChild { Id = b + 109, Tenant = tenantA, ParentId = linkShared, SortKey = 10 },
            new EagerCompositeChild { Id = b + 201, Tenant = tenantA, ParentId = linkOther, SortKey = 2 },
            new EagerCompositeChild { Id = b + 204, Tenant = tenantA, ParentId = linkOther, SortKey = 5 },
            new EagerCompositeChild { Id = b + 207, Tenant = tenantA, ParentId = linkOther, SortKey = 8 },
            new EagerCompositeChild { Id = b + 302, Tenant = tenantC, ParentId = linkShared, SortKey = 3 },
            new EagerCompositeChild { Id = b + 305, Tenant = tenantC, ParentId = linkShared, SortKey = 6 },
            new EagerCompositeChild { Id = b + 308, Tenant = tenantC, ParentId = linkShared, SortKey = 9 },
        ]).Insert();

        return (b, linkShared, linkOther, tenantC);
    }

    private EntityBuilder<EagerCompositeParent> PerParentCompositeParents(int b)
        => _sut.DataProvider.From<EagerCompositeParent>().Where(p => p.RowId == b || p.RowId == b + 1 || p.RowId == b + 2);

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_CompositeKey_Split_Take_PartitionsByBothComponents()
    {
        var (b, _, _, _) = SeedPerParentComposite();

        if (!Provider.SupportsTupleIn)
        {
            // The composite link key is filtered with a two-column row-value IN list; this provider has no
            // row-value constructor, so the existing tuple-IN preflight rejects the load before SQL. Split
            // is the supported composite mode on the other providers; this branch is the :236 capability
            // gate, not a new guard.
            var reject = () => PerParentCompositeParents(b)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerCompositeChild>().OrderBy(x => x.SortKey).Limit(2),
                    p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                    c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
                .OrderBy(p => p.RowId)
                .ToList();
            reject.Should().Throw<NotSupportedException>().WithMessage("*tuple IN/Contains*");
            return;
        }

        var parents = PerParentCompositeParents(b)
            .LoadWith(
                p => p.Children,
                c => c.From<EagerCompositeChild>().OrderBy(x => x.SortKey).Limit(2),
                p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(b, b + 1, b + 2);
        // Interleaved sorts across parents: only the complete two-column partition yields these.
        parents[0].Children.Select(c => c.Id).Should().Equal(b + 100, b + 103);
        parents[1].Children.Select(c => c.Id).Should().Equal(b + 201, b + 204);
        parents[2].Children.Select(c => c.Id).Should().Equal(b + 302, b + 305);
    }

    [Fact]
    [Trait("Feature", "D211")]
    public void EagerPerParentPaging_CompositeKey_Split_SkipAndTake_PartitionsByBothComponents()
    {
        var (b, _, _, _) = SeedPerParentComposite();

        if (!Provider.SupportsTupleIn)
        {
            var reject = () => PerParentCompositeParents(b)
                .LoadWith(
                    p => p.Children,
                    c => c.From<EagerCompositeChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2),
                    p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                    c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
                .OrderBy(p => p.RowId)
                .ToList();
            reject.Should().Throw<NotSupportedException>().WithMessage("*tuple IN/Contains*");
            return;
        }

        var parents = PerParentCompositeParents(b)
            .LoadWith(
                p => p.Children,
                c => c.From<EagerCompositeChild>().OrderBy(x => x.SortKey).Offset(1).Limit(2),
                p => new ValueTuple<int, int>(p.Tenant, p.LinkId),
                c => new ValueTuple<int, int>(c.Tenant, c.ParentId))
            .OrderBy(p => p.RowId)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(b + 103, b + 106);
        parents[1].Children.Select(c => c.Id).Should().Equal(b + 204, b + 207);
        parents[2].Children.Select(c => c.Id).Should().Equal(b + 305, b + 308);
    }
}
