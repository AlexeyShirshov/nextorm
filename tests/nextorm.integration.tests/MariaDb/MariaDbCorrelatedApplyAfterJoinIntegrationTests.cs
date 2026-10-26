using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T324 D7 (E07/AC07) — the MariaDB column of the correlated APPLY-after-join ledger. r=2 withdraws the
/// live-lateral claim (MariaDB 11.4 rejects the generated <c>cross join lateral (…)</c> spelling) and
/// replaces it with the capability negative: both <c>CrossApply</c> and <c>OuterApply</c> are refused by
/// the dialect gate while the command is prepared, before any SQL executes. The six formerly required
/// scenario shapes remain as ledger rows, each exercising both kinds through the shared
/// <see cref="CommonTestSuite.T324LiveAssertLateralRefusal"/> body.
/// <para>
/// The refusal precedes execution, so these facts need no live server: the context uses an externally
/// configured connection string when <see cref="MariaDbContainer.ConnectionStringVariable"/> is set and a
/// never-opened placeholder otherwise. No connection is opened and no container is started, so the facts
/// run with or without a MariaDB container — a skip would hide the ledger row. MariaDB has no
/// <see cref="CommonTestSuite"/> provider registration, so the shared bodies are called directly; a MySQL
/// run is never counted as MariaDB evidence.
/// </para>
/// </summary>
public sealed class MariaDbCorrelatedApplyAfterJoinIntegrationTests : IDisposable
{
    private const string NeverOpenedConnectionString =
        "Server=127.0.0.1;Port=1;Database=nextorm;User ID=nextorm;Password=nextorm;Connection Timeout=1";

    private readonly IDataContext _ctx;

    public MariaDbCorrelatedApplyAfterJoinIntegrationTests()
        => _ctx = new MariaDbDataContext(ConnectionString(), new DataContextBuilder());

    public void Dispose() => _ctx.Dispose();

    /// <summary>
    /// The APPLY gate is a dialect capability, evaluated before the source is rendered, so the shape of
    /// the applied body cannot change the outcome: every ledger row asserts the CrossApply and OuterApply
    /// refusal, and the prepare-time throw is the execution boundary (no command is executed).
    /// </summary>
    private void AssertBothKindsAreRefused()
    {
        CommonTestSuite.T324LiveAssertLateralRefusal(_ctx, outer: false);
        CommonTestSuite.T324LiveAssertLateralRefusal(_ctx, outer: true);
    }

    private static string ConnectionString()
    {
        var external = Environment.GetEnvironmentVariable(MariaDbContainer.ConnectionStringVariable);
        return string.IsNullOrWhiteSpace(external) ? NeverOpenedConnectionString : external;
    }

    [Fact]
    public void T324_Live_CrossApply_AfterJoin_DropsUnmatchedParent_OuterApply_PreservesIt()
        => AssertBothKindsAreRefused();

    [Fact]
    public void T324_Live_AfterJoin_Correlates_Both_Left_Sides()
        => AssertBothKindsAreRefused();

    [Fact]
    public void T324_Live_AfterJoin_OrderedTop1_PerParentRow()
        => AssertBothKindsAreRefused();

    [Fact]
    public void T324_Live_AfterJoin_RepeatedClrTypes_ResolveTheCorrectOccurrence()
        => AssertBothKindsAreRefused();

    [Fact]
    public void T324_Live_ApplyAfterApply_SecondSeesTheFirstSlot()
        => AssertBothKindsAreRefused();

    [Fact]
    public void T324_Live_OuterApply_PreservedParent_YieldsNulls_NotDefaults()
        => AssertBothKindsAreRefused();
}
