using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_integration_tests;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    // One order per relationship, plus an unlinked person for RIGHT/FULL. Buyer and Approver are the
    // same AliasPerson CLR type, so a slot mix-up cannot pass by returning the same multiset.

    // MariaDB and ClickHouse do not derive CommonTestSuite (they run against their own container
    // harnesses), so every body is exposed as an internal static helper and re-pinned by
    // MariaDbJoinAliasIntegrationTests / ClickHouseJoinAliasIntegrationTests. A skip is never a pass.
    [Fact]
    public void Alias_inner_join_returns_matched_rows() => AliasInnerJoinReturnsMatchedRows(_sut);

    internal static void AliasInnerJoinReturnsMatchedRows(TestDataRepository sut)
    {
        var ids = sut.AliasOrder
            .Join<AliasPerson>(sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20);
    }

    [Fact]
    public void Alias_left_join_returns_all_orders() => AliasLeftJoinReturnsAllOrders(_sut);

    internal static void AliasLeftJoinReturnsAllOrders(TestDataRepository sut)
    {
        var ids = sut.AliasOrder
            .LeftJoin<AliasPerson>(sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20);
    }

    [Fact]
    public void Alias_right_join_keeps_unmatched_right_rows() => AliasRightJoinKeepsUnmatchedRightRows(_sut);

    internal static void AliasRightJoinKeepsUnmatchedRightRows(TestDataRepository sut)
    {
        var ids = sut.AliasOrder
            .RightJoin<AliasPerson>(sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20, 30);
    }

    [Fact]
    public void Alias_full_join_returns_both_sides_or_fails_closed_without_full_join()
        => AliasFullJoinReturnsBothSidesOrFailsClosed(_sut, Provider.SupportsFullJoin);

    internal static void AliasFullJoinReturnsBothSidesOrFailsClosed(TestDataRepository sut, bool supportsFullJoin)
    {
        if (!supportsFullJoin)
        {
            // MySQL/MariaDB have no FULL JOIN: the alias operator must fail closed, not be skipped.
            var act = () => sut.AliasOrder
                .FullJoin<AliasPerson>(sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Select(p => p.Buyer.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>();
            return;
        }

        var ids = sut.AliasOrder
            .FullJoin<AliasPerson>(sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20, 30);
    }

    [Fact]
    public void Alias_cross_join_returns_cartesian_product() => AliasCrossJoinReturnsCartesianProduct(_sut);

    internal static void AliasCrossJoinReturnsCartesianProduct(TestDataRepository sut)
    {
        var ids = sut.AliasOrder
            .CrossJoin(sut.AliasPerson, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList();

        ids.Should().HaveCount(6);
    }

    [Fact]
    public void Alias_buyer_and_approver_resolve_to_distinct_ids() => AliasBuyerAndApproverResolveToDistinctIds(_sut);

    internal static void AliasBuyerAndApproverResolveToDistinctIds(TestDataRepository sut)
    {
        var chained = sut.AliasOrder
            .Join<AliasPerson>(sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Join<AliasPerson>(sut.AliasPerson, (p, a) => p.Item1.ApproverId == a.Id, Alias.Approver);

        var rows = chained
            .Select(p => new { Buyer = p.Buyer.Id, Approver = p.Approver.Id })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Buyer != r.Approver);
        rows.Select(r => r.Buyer).OrderBy(id => id).Should().Equal(10, 20);
        rows.Select(r => r.Approver).OrderBy(id => id).Should().Equal(10, 20);
    }

    [Fact]
    public void Alias_join_across_a_method_boundary_resolves_both_slots() => AliasJoinAcrossMethodBoundaryResolvesBothSlots(_sut);

    internal static void AliasJoinAcrossMethodBoundaryResolvesBothSlots(TestDataRepository sut)
    {
        var person = sut.AliasPerson;
        var intermediate = sut.AliasOrder.Join<AliasPerson>(person, (o, p) => o.BuyerId == p.Id, Alias.Buyer);
        var chained = AddApprover(intermediate, person);

        var rows = chained
            .Select(p => new { Buyer = p.Buyer.Id, Approver = p.Approver.Id })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Buyer != r.Approver);
    }

    // A method that names the generated builder types in its signature and returns the second alias join.
    private static AliasJoin_P1_A2_Buyer_A3_Approver<AliasOrder, AliasPerson, AliasPerson> AddApprover(
        AliasJoin_P1_A2_Buyer<AliasOrder, AliasPerson> builder,
        EntityBuilder<AliasPerson> person)
        => builder.Join<AliasPerson>(person, (p, a) => p.Item1.ApproverId == a.Id, Alias.Approver);

    // ---------------------------------------------------------------------------------------------
    // Root alias (.WithAlias(Alias.Root)) and free positional/alias mixing (issue #160, Phase 2).
    // These were exercised only against SQLite in the alias suite; they must also run for real on
    // every container provider (R160-09).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Root_alias_inner_join_returns_matched_rows() => RootAliasInnerJoinReturnsMatchedRows(_sut);

    internal static void RootAliasInnerJoinReturnsMatchedRows(TestDataRepository sut)
    {
        var ids = sut.AliasOrder
            .WithAlias(Alias.Root)
            .Join<AliasPerson>(sut.AliasPerson, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20);
    }

    [Fact]
    public void Root_alias_item1_and_root_name_resolve_to_the_same_slot() => RootAliasItem1AndRootNameResolveToSameSlot(_sut);

    internal static void RootAliasItem1AndRootNameResolveToSameSlot(TestDataRepository sut)
    {
        var rooted = sut.AliasOrder
            .WithAlias(Alias.Root)
            .Join<AliasPerson>(sut.AliasPerson, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer);

        var rootIds = rooted.Select(p => p.Root.Id).ToList().OrderBy(id => id).ToList();
        var item1Ids = rooted.Select(p => p.Item1.Id).ToList().OrderBy(id => id).ToList();

        rootIds.Should().Equal(1, 2);
        item1Ids.Should().Equal(rootIds);
    }

    [Fact]
    public void Root_alias_supports_a_positional_join_after_it() => RootAliasSupportsPositionalJoinAfterIt(_sut);

    internal static void RootAliasSupportsPositionalJoinAfterIt(TestDataRepository sut)
    {
        var ids = sut.AliasOrder
            .WithAlias(Alias.Root)
            .Join(sut.AliasPerson, (o, p) => o.Root.BuyerId == p.Id)
            .Select(p => p.Item2.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20);
    }

    [Fact]
    public void Mixed_alias_then_positional_join_executes_and_keeps_slot_identity() => MixedAliasThenPositionalJoinExecutesAndKeepsSlotIdentity(_sut);

    internal static void MixedAliasThenPositionalJoinExecutesAndKeepsSlotIdentity(TestDataRepository sut)
    {
        var chained = sut.AliasOrder
            .Join<AliasPerson>(sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Join(sut.AliasPerson, (p, x) => p.Item2.Id == x.Id);

        var rows = chained
            .Select(p => new { Alias = p.Buyer.Id, Positional = p.Item3.Id })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Alias == r.Positional);
    }

    [Fact]
    public void Mixed_positional_then_alias_join_executes_and_keeps_slot_identity() => MixedPositionalThenAliasJoinExecutesAndKeepsSlotIdentity(_sut);

    internal static void MixedPositionalThenAliasJoinExecutesAndKeepsSlotIdentity(TestDataRepository sut)
    {
        var chained = sut.AliasOrder
            .Join(sut.AliasPerson, (o, p) => o.BuyerId == p.Id)
            .Join<AliasPerson>(sut.AliasPerson, (p, a) => p.Item2.Id == a.Id, Alias.Approver);

        var rows = chained
            .Select(p => new { Positional = p.Item2.Id, Alias = p.Approver.Id })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Positional == r.Alias);
    }
}
