using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #249 / T353 D4b live rows on ClickHouse. ClickHouse has no <see cref="CommonTestSuite"/> subclass
/// (its integration class derives from <see cref="ProviderTestSuite"/> directly), so the shared live
/// bodies are re-pinned here against a real server and the seeded <c>eager_*</c> Memory tables. The
/// bodies assert values, not "did not throw", and the statement counts are the ones the split/single
/// eager-loading paths produce on this dialect. Depth three runs the same shared body PostgreSQL and
/// SQLite run and is covered in both modes below (split 4 statements, single 1).
/// </summary>
public sealed class ClickHouseCollectionProjectionIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void CollectionProjectionLive_Depth1_Direct_Split()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.Depth1DirectSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Direct_Single()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(ctx);
        CommonTestSuite.Depth1DirectSingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Adapter_Split()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.Depth1AdapterSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Adapter_Shapes()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.Depth1AdapterShapes(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth2_Direct_Split()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.Depth2DirectSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth2_Adapter_Single()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(ctx);
        CommonTestSuite.Depth2AdapterSingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_Depth3_Split()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.Depth3Split(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth3_Single()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(ctx);
        CommonTestSuite.Depth3Single(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_FlatManyToMany_Direct_Split()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.FlatManyToManyDirectSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_FlatManyToMany_Adapter_Single()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(ctx);
        CommonTestSuite.FlatManyToManyAdapterSingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_NestedManyToMany_Split()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.NestedManyToManySplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_NestedManyToMany_Single()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(ctx);
        CommonTestSuite.NestedManyToManySingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_ManyToManyWithLimit_Split()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.ManyToManyWithLimit_Split(_sut.DataProvider);
        CommonTestSuite.NestedManyToManyWithLimit_Split(_sut.DataProvider);
        CommonTestSuite.LoadWithManyToManyPage_Split(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_ManyToManyWithLimit_Single()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(ctx);
        CommonTestSuite.ManyToManyWithLimit_Single(ctx);
        CommonTestSuite.LoadWithManyToManyPage_Single(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_AdapterRefusals()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.AdapterRefusals(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_CompositionAndShapeRefusals()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.CompositionAndShapeRefusals(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_BrokenReferenceCardinality()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var single = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(single);
        CommonTestSuite.BrokenReferenceCardinalityThrows(_sut.DataProvider, single);
    }

    [Fact]
    public void CollectionProjectionLive_RepeatedIdentityAndMissingReference()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        using var single = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        CommonTestSuite.RegisterCollectionProjectionLive(single);
        CommonTestSuite.RepeatedIdentityAndMissingReference(_sut.DataProvider, single);
    }

    [Fact]
    public void CollectionProjectionLive_EmptyAtEveryLevel()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.EmptyAtEveryLevel(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_CapturedFilterRebinds()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.CapturedFilterRebinds(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_CompositeJunctionSlotAndPage()
    {
        CommonTestSuite.EnsureCollectionProjectionLiveSeeded(_sut);
        CommonTestSuite.CompositeJunctionSlotAndPage(_sut.DataProvider);
    }
}
