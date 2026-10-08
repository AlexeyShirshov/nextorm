using System.Data.Common;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #148-B D8: ClickHouse reference-navigation conformance. ClickHouse does not derive
/// <see cref="CommonTestSuite"/>, so the reference cases are re-pinned here: a reference query gets the
/// query-local <c>join_use_nulls=1</c> (never session/global), an explicit conflict is rejected before
/// execution, and a dangling foreign key materializes SQL <c>NULL</c> instead of the column default.
/// </summary>
public sealed class ClickHouseImplicitNavigationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, TestContext.Current.CancellationToken))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Reference_query_should_inject_join_use_nulls_query_locally()
    {
        NavConScenarios.Seed(_sut.DataProvider);

        var sql = SqlOf(_sut.DataProvider, _sut.DataProvider.From<JoinIntoChild>()
            .Select(c => new { c.Id, Name = c.Parent!.Name }));

        sql.Should().Contain("settings join_use_nulls = 1");
        sql.ToLowerInvariant().Should().Contain("left join").And.Contain("eager_parent");
    }

    [Fact]
    public void Plain_query_should_not_inject_join_use_nulls()
    {
        NavConScenarios.Seed(_sut.DataProvider);

        var sql = SqlOf(_sut.DataProvider, _sut.DataProvider.From<JoinIntoChild>()
            .Select(c => new { c.Id, c.Name }));

        sql.Should().NotContain("join_use_nulls");
    }

    [Fact]
    public void Conflicting_join_use_nulls_zero_should_be_rejected_before_execution()
    {
        NavConScenarios.Seed(_sut.DataProvider);

        var cmd = _sut.DataProvider.From<JoinIntoChild>()
            .Settings(("join_use_nulls", "0"))
            .Select(c => new { c.Id, Parent = c.Parent });

        Action act = () => SqlOf(_sut.DataProvider, cmd);

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*join_use_nulls = 0*join_use_nulls = 1*");
    }

    [Fact]
    public void Dangling_fk_should_yield_null_reference_and_scalar()
    {
        var f = NavConScenarios.Seed(_sut.DataProvider);

        var rows = _sut.DataProvider.From<JoinIntoChild>()
            .Where(c => c.Id == f + 100 || c.Id == f + 103)
            .Select(c => new { c.Id, Parent = c.Parent, Name = c.Parent!.Name })
            .ToList();

        var present = rows.Single(r => r.Id == f + 100);
        present.Parent.Should().NotBeNull();
        present.Parent!.Name.Should().Be("nav-many");
        present.Name.Should().Be("nav-many");

        var dangling = rows.Single(r => r.Id == f + 103);
        dangling.Parent.Should().BeNull("join_use_nulls makes the unmatched LEFT JOIN side SQL NULL, not the default");
        dangling.Name.Should().BeNull();
    }

    [Fact]
    public void ReferenceScalarChain_ShouldReturnThePrincipalValueForPresentAndDanglingFk()
        => NavConScenarios.ReferenceScalarChainAndPresence(_sut.DataProvider);

    [Fact]
    public void ReferenceWholeProjection_ShouldBeNullForADanglingFk()
        => NavConScenarios.ReferenceWholeProjectionAndDangling(_sut.DataProvider);

    [Fact]
    public void ReferenceLiftedCoalescedAndUnliftedGuard_ShouldFollowTheNullContract()
        => NavConScenarios.ReferenceLiftedCoalescedAndUnliftedGuard(_sut.DataProvider);

    [Fact]
    public void RejectList_ShouldFailClosed()
        => NavConScenarios.RejectList(_sut.DataProvider);

    [Fact]
    public void MultiHop_reference_presence_should_propagate_absence()
        => NavConScenarios.MultiHopReferencePresence(_sut.DataProvider);

    [Fact]
    public void Reference_to_collection_is_rejected_with_a_precise_diagnostic()
    {
        NavConScenarios.Seed(_sut.DataProvider);

        // ClickHouse cannot correlate a subquery on a joined source alias (the engine reports
        // NOT_IMPLEMENTED: can't find correlated column), so the shape is gated explicitly by the
        // dialect capability rather than surfaced as an engine error.
        Action act = () => SqlOf(_sut.DataProvider, _sut.DataProvider.From<JoinIntoChild>()
            .Select(c => new { c.Id, C = c.Parent!.Children.Count() }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot correlate a subquery on a joined source*",
                "ClickHouse cannot correlate a collection reached through a reference");
    }

    [Fact]
    public void Reference_adapter_should_match_explicit_reference_semantics()
        => NavConScenarios.ReferenceAdapterEquivalentToExplicit(_sut.DataProvider);

    [Fact]
    public void Wide_count_boundary_should_stay_in_range_and_wide()
        => NavConScenarios.WideCountInRangeBoundary(_sut.DataProvider);
}
