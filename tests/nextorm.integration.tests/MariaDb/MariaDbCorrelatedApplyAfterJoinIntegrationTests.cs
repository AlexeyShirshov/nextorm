using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T324 D7 (E07/AC07) — the MariaDB column of the live correlated APPLY-after-join ledger. MariaDB has no
/// <see cref="CommonTestSuite"/> provider registration (it is not in the <c>ITestProvider</c> set), so the
/// shared bodies are re-run here against <see cref="MariaDbContainer.ConnectionString"/> directly. Every fact
/// creates its own <c>t324l_*</c> fixture; a MySQL run is never counted as MariaDB evidence, and an
/// unavailable container is a reported skip reason, never a pass.
/// </summary>
public sealed class MariaDbCorrelatedApplyAfterJoinIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public MariaDbCorrelatedApplyAfterJoinIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void T324_Live_CrossApply_AfterJoin_DropsUnmatchedParent_OuterApply_PreservesIt()
        => CommonTestSuite.T324LiveOrphanParent(_ctx, supportsApply: true);

    [Fact]
    public void T324_Live_AfterJoin_Correlates_Both_Left_Sides()
        => CommonTestSuite.T324LiveBothLeftSides(_ctx, supportsApply: true);

    [Fact]
    public void T324_Live_AfterJoin_OrderedTop1_PerParentRow()
        => CommonTestSuite.T324LiveOrderedTop1(_ctx, supportsApply: true);

    [Fact]
    public void T324_Live_AfterJoin_RepeatedClrTypes_ResolveTheCorrectOccurrence()
        => CommonTestSuite.T324LiveRepeatedClrTypes(_ctx, supportsApply: true);

    [Fact]
    public void T324_Live_ApplyAfterApply_SecondSeesTheFirstSlot()
        => CommonTestSuite.T324LiveApplyAfterApply(_ctx, supportsApply: true);

    [Fact]
    public void T324_Live_OuterApply_PreservedParent_YieldsNulls_NotDefaults()
        => CommonTestSuite.T324LiveOuterNulls(_ctx, supportsApply: true);
}
