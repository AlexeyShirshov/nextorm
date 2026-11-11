using System.Diagnostics;
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

    // -----------------------------------------------------------------------------------------
    // #322 D7: live row parity for the source-less alias join over the frozen O x R x V matrix
    // (M04), plus the MySQL/MariaDB FULL refusal (M03).
    //
    // O = the conditional operator applied to the join under test (Inner/Left/Right/Full).
    // R = the chain the join completes: its last join lands on slot 2 (root receiver) .. slot 8
    //     (seven slots already joined), so the matrix covers the seven-to-eight slot boundary.
    // V = the options call shape of the source-less surface: omitted, explicit null, callback.
    //
    // Fixture (identical in every provider seed): orders (1,10,20) and (2,20,10); person 10
    // 'Buyer', 20 'Approver', 30 'Unlinked'. The join under test matches person 30 for order 1
    // (10 + 20) and nothing for order 2 (20 + 20 = 40), so INNER keeps one row, LEFT adds the
    // unmatched left row as null, RIGHT adds the two unmatched persons and FULL keeps both sides.
    // The same arithmetic is reused at every arity, so the expected multiset does not depend on R.
    //
    // The source-explicit and positional oracles keep their frozen option-less signatures, so V is
    // carried by the source-less call only; their rows are the oracle for each V cell.
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> TypeOnlyAliasMatrix()
    {
        foreach (var op in Enum.GetValues<AliasProbeOperator>())
        {
            for (var arity = 1; arity <= 7; arity++)
            {
                foreach (var opt in Enum.GetValues<AliasProbeOptions>())
                {
                    yield return new object[] { op, arity, opt };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TypeOnlyAliasMatrix))]
    public void TypeOnlyAlias_rows_match_the_source_explicit_and_positional_chains(
        AliasProbeOperator op, int arity, AliasProbeOptions opt)
        => TypeOnlyAliasRowsMatch(op, arity, opt, Provider.SupportsFullJoin, _sut);

    /// <summary>
    /// One matrix cell: the source-less chain, the source-explicit chain and the positional chain
    /// must return the same rows, and those rows must be the exact expected multiset (a vacuous or
    /// collapsed chain cannot pass). When the provider has no FULL join the cell is replaced by
    /// refusal parity: all three forms must throw the same <c>*Full join*</c> message, so the
    /// unsupported operator is never rendered, executed or skipped.
    /// </summary>
    internal static void TypeOnlyAliasRowsMatch(
        AliasProbeOperator op, int arity, AliasProbeOptions opt, bool supportsFullJoin, TestDataRepository sut)
    {
        if (op == AliasProbeOperator.Full && !supportsFullJoin)
        {
            string? expectedMessage = null;
            foreach (var form in Enum.GetValues<AliasProbeForm>())
            {
                var current = form;
                Action act = () => ProbeRows(sut, arity, op, opt, current);
                var message = act.Should().Throw<NotSupportedException>().Which.Message;
                message.Should().Contain("Full join", "the {0} form must refuse FULL instead of emitting it", current);
                if (expectedMessage is null) expectedMessage = message;
                else message.Should().Be(expectedMessage, "every form must refuse FULL with the same message");
            }

            return;
        }

        var expected = ExpectedNames(op);

        var sourceLess = ProbeRows(sut, arity, op, opt, AliasProbeForm.SourceLess);
        var sourceExplicit = ProbeRows(sut, arity, op, opt, AliasProbeForm.SourceExplicit);
        var positional = ProbeRows(sut, arity, op, opt, AliasProbeForm.Positional);

        sourceLess.Should().Equal(expected, "source-less {0} at arity {1} with {2}", op, arity, opt);
        sourceExplicit.Should().Equal(expected, "source-explicit {0} at arity {1}", op, arity);
        positional.Should().Equal(expected, "positional {0} at arity {1}", op, arity);
        sourceLess.Should().Equal(sourceExplicit, "the source argument must not change the rows");
        sourceLess.Should().Equal(positional, "the alias surface must not change the rows");

        if (op is AliasProbeOperator.Left or AliasProbeOperator.Full)
        {
            sourceLess.Should().Contain(NullName, "an outer join must keep the unmatched left row of order 2");
        }

        if (op is AliasProbeOperator.Right or AliasProbeOperator.Full)
        {
            sourceLess.Should().Contain("Buyer").And.Contain("Approver",
                "a right/full join must keep the persons no order references");
        }
    }

    private const string NullName = "<null>";

    private static readonly Action<JoinOptions> NeutralOptions = static options => options.SuppressCartesianWarning();

    private static List<string> ExpectedNames(AliasProbeOperator op)
    {
        var names = op switch
        {
            AliasProbeOperator.Inner => new List<string?> { "Unlinked" },
            AliasProbeOperator.Left => new List<string?> { null, "Unlinked" },
            AliasProbeOperator.Right => new List<string?> { "Unlinked", "Buyer", "Approver" },
            _ => new List<string?> { null, "Unlinked", "Buyer", "Approver" }
        };

        return Names(names);
    }

    private static List<string> Names(List<string?> names)
        => names.Select(name => name ?? NullName).OrderBy(name => name, StringComparer.Ordinal).ToList();

    private static List<string> ProbeRows(
        TestDataRepository sut, int arity, AliasProbeOperator op, AliasProbeOptions opt, AliasProbeForm form)
        => form switch
        {
            AliasProbeForm.SourceLess => SourceLess(sut, arity, op, opt),
            AliasProbeForm.SourceExplicit => SourceExplicit(sut, arity, op, opt),
            _ => Positional(sut, arity, op, opt)
        };

    private static List<string> SourceLess1(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var tested = (op, opt) switch
        {
            (AliasProbeOperator.Inner, AliasProbeOptions.Omitted)
                => sut.AliasOrder.Join<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            (AliasProbeOperator.Inner, AliasProbeOptions.ExplicitNull)
                => sut.AliasOrder.Join<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, null),
            (AliasProbeOperator.Inner, AliasProbeOptions.Callback)
                => sut.AliasOrder.Join<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Left, AliasProbeOptions.Omitted)
                => sut.AliasOrder.LeftJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            (AliasProbeOperator.Left, AliasProbeOptions.ExplicitNull)
                => sut.AliasOrder.LeftJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, null),
            (AliasProbeOperator.Left, AliasProbeOptions.Callback)
                => sut.AliasOrder.LeftJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Right, AliasProbeOptions.Omitted)
                => sut.AliasOrder.RightJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            (AliasProbeOperator.Right, AliasProbeOptions.ExplicitNull)
                => sut.AliasOrder.RightJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, null),
            (AliasProbeOperator.Right, AliasProbeOptions.Callback)
                => sut.AliasOrder.RightJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Full, AliasProbeOptions.Omitted)
                => sut.AliasOrder.FullJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            (AliasProbeOperator.Full, AliasProbeOptions.ExplicitNull)
                => sut.AliasOrder.FullJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, null),
            (AliasProbeOperator.Full, AliasProbeOptions.Callback)
                => sut.AliasOrder.FullJoin<AliasPerson>((a, p) => p.Id == a.BuyerId + 20, Alias.Tested, NeutralOptions),
            _ => throw new UnreachableException($"unhandled cell {op}/{opt}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceLess2(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var receiver = sut.AliasOrder
            .Join<AliasPerson>((a, p) => p.Id == a.BuyerId, Alias.S1);
        var tested = (op, opt) switch
        {
            (AliasProbeOperator.Inner, AliasProbeOptions.Omitted)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            (AliasProbeOperator.Inner, AliasProbeOptions.ExplicitNull)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Inner, AliasProbeOptions.Callback)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Left, AliasProbeOptions.Omitted)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            (AliasProbeOperator.Left, AliasProbeOptions.ExplicitNull)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Left, AliasProbeOptions.Callback)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Right, AliasProbeOptions.Omitted)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            (AliasProbeOperator.Right, AliasProbeOptions.ExplicitNull)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Right, AliasProbeOptions.Callback)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Full, AliasProbeOptions.Omitted)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            (AliasProbeOperator.Full, AliasProbeOptions.ExplicitNull)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Full, AliasProbeOptions.Callback)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item2.Id + 20, Alias.Tested, NeutralOptions),
            _ => throw new UnreachableException($"unhandled cell {op}/{opt}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceLess3(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var receiver = sut.AliasOrder
            .Join<AliasPerson>((a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>((a, p) => p.Id == a.Item2.Id, Alias.S2);
        var tested = (op, opt) switch
        {
            (AliasProbeOperator.Inner, AliasProbeOptions.Omitted)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            (AliasProbeOperator.Inner, AliasProbeOptions.ExplicitNull)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Inner, AliasProbeOptions.Callback)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Left, AliasProbeOptions.Omitted)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            (AliasProbeOperator.Left, AliasProbeOptions.ExplicitNull)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Left, AliasProbeOptions.Callback)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Right, AliasProbeOptions.Omitted)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            (AliasProbeOperator.Right, AliasProbeOptions.ExplicitNull)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Right, AliasProbeOptions.Callback)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Full, AliasProbeOptions.Omitted)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            (AliasProbeOperator.Full, AliasProbeOptions.ExplicitNull)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Full, AliasProbeOptions.Callback)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item3.Id + 20, Alias.Tested, NeutralOptions),
            _ => throw new UnreachableException($"unhandled cell {op}/{opt}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceLess4(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var receiver = sut.AliasOrder
            .Join<AliasPerson>((a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>((a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>((a, p) => p.Id == a.Item3.Id, Alias.S3);
        var tested = (op, opt) switch
        {
            (AliasProbeOperator.Inner, AliasProbeOptions.Omitted)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            (AliasProbeOperator.Inner, AliasProbeOptions.ExplicitNull)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Inner, AliasProbeOptions.Callback)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Left, AliasProbeOptions.Omitted)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            (AliasProbeOperator.Left, AliasProbeOptions.ExplicitNull)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Left, AliasProbeOptions.Callback)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Right, AliasProbeOptions.Omitted)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            (AliasProbeOperator.Right, AliasProbeOptions.ExplicitNull)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Right, AliasProbeOptions.Callback)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Full, AliasProbeOptions.Omitted)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            (AliasProbeOperator.Full, AliasProbeOptions.ExplicitNull)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Full, AliasProbeOptions.Callback)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item4.Id + 20, Alias.Tested, NeutralOptions),
            _ => throw new UnreachableException($"unhandled cell {op}/{opt}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceLess5(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var receiver = sut.AliasOrder
            .Join<AliasPerson>((a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>((a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>((a, p) => p.Id == a.Item3.Id, Alias.S3)
            .Join<AliasPerson>((a, p) => p.Id == a.Item4.Id, Alias.S4);
        var tested = (op, opt) switch
        {
            (AliasProbeOperator.Inner, AliasProbeOptions.Omitted)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            (AliasProbeOperator.Inner, AliasProbeOptions.ExplicitNull)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Inner, AliasProbeOptions.Callback)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Left, AliasProbeOptions.Omitted)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            (AliasProbeOperator.Left, AliasProbeOptions.ExplicitNull)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Left, AliasProbeOptions.Callback)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Right, AliasProbeOptions.Omitted)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            (AliasProbeOperator.Right, AliasProbeOptions.ExplicitNull)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Right, AliasProbeOptions.Callback)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Full, AliasProbeOptions.Omitted)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            (AliasProbeOperator.Full, AliasProbeOptions.ExplicitNull)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Full, AliasProbeOptions.Callback)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item5.Id + 20, Alias.Tested, NeutralOptions),
            _ => throw new UnreachableException($"unhandled cell {op}/{opt}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceLess6(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var receiver = sut.AliasOrder
            .Join<AliasPerson>((a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>((a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>((a, p) => p.Id == a.Item3.Id, Alias.S3)
            .Join<AliasPerson>((a, p) => p.Id == a.Item4.Id, Alias.S4)
            .Join<AliasPerson>((a, p) => p.Id == a.Item5.Id, Alias.S5);
        var tested = (op, opt) switch
        {
            (AliasProbeOperator.Inner, AliasProbeOptions.Omitted)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            (AliasProbeOperator.Inner, AliasProbeOptions.ExplicitNull)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Inner, AliasProbeOptions.Callback)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Left, AliasProbeOptions.Omitted)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            (AliasProbeOperator.Left, AliasProbeOptions.ExplicitNull)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Left, AliasProbeOptions.Callback)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Right, AliasProbeOptions.Omitted)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            (AliasProbeOperator.Right, AliasProbeOptions.ExplicitNull)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Right, AliasProbeOptions.Callback)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Full, AliasProbeOptions.Omitted)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            (AliasProbeOperator.Full, AliasProbeOptions.ExplicitNull)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Full, AliasProbeOptions.Callback)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item6.Id + 20, Alias.Tested, NeutralOptions),
            _ => throw new UnreachableException($"unhandled cell {op}/{opt}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceLess7(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var receiver = sut.AliasOrder
            .Join<AliasPerson>((a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>((a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>((a, p) => p.Id == a.Item3.Id, Alias.S3)
            .Join<AliasPerson>((a, p) => p.Id == a.Item4.Id, Alias.S4)
            .Join<AliasPerson>((a, p) => p.Id == a.Item5.Id, Alias.S5)
            .Join<AliasPerson>((a, p) => p.Id == a.Item6.Id, Alias.S6);
        var tested = (op, opt) switch
        {
            (AliasProbeOperator.Inner, AliasProbeOptions.Omitted)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            (AliasProbeOperator.Inner, AliasProbeOptions.ExplicitNull)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Inner, AliasProbeOptions.Callback)
                => receiver.Join<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Left, AliasProbeOptions.Omitted)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            (AliasProbeOperator.Left, AliasProbeOptions.ExplicitNull)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Left, AliasProbeOptions.Callback)
                => receiver.LeftJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Right, AliasProbeOptions.Omitted)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            (AliasProbeOperator.Right, AliasProbeOptions.ExplicitNull)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Right, AliasProbeOptions.Callback)
                => receiver.RightJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, NeutralOptions),
            (AliasProbeOperator.Full, AliasProbeOptions.Omitted)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            (AliasProbeOperator.Full, AliasProbeOptions.ExplicitNull)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, null),
            (AliasProbeOperator.Full, AliasProbeOptions.Callback)
                => receiver.FullJoin<AliasPerson>((a, p) => p.Id == a.Item7.Id + 20, Alias.Tested, NeutralOptions),
            _ => throw new UnreachableException($"unhandled cell {op}/{opt}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceExplicit1(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var tested = op switch
        {
            AliasProbeOperator.Inner => sut.AliasOrder.Join(person, (a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            AliasProbeOperator.Left => sut.AliasOrder.LeftJoin(person, (a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            AliasProbeOperator.Right => sut.AliasOrder.RightJoin(person, (a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            AliasProbeOperator.Full => sut.AliasOrder.FullJoin(person, (a, p) => p.Id == a.BuyerId + 20, Alias.Tested),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceExplicit2(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join<AliasPerson>(person, (a, p) => p.Id == a.BuyerId, Alias.S1);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item2.Id + 20, Alias.Tested),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceExplicit3(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join<AliasPerson>(person, (a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item2.Id, Alias.S2);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item3.Id + 20, Alias.Tested),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceExplicit4(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join<AliasPerson>(person, (a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item3.Id, Alias.S3);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item4.Id + 20, Alias.Tested),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceExplicit5(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join<AliasPerson>(person, (a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item3.Id, Alias.S3)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item4.Id, Alias.S4);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item5.Id + 20, Alias.Tested),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceExplicit6(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join<AliasPerson>(person, (a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item3.Id, Alias.S3)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item4.Id, Alias.S4)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item5.Id, Alias.S5);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item6.Id + 20, Alias.Tested),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> SourceExplicit7(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join<AliasPerson>(person, (a, p) => p.Id == a.BuyerId, Alias.S1)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item2.Id, Alias.S2)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item3.Id, Alias.S3)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item4.Id, Alias.S4)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item5.Id, Alias.S5)
            .Join<AliasPerson>(person, (a, p) => p.Id == a.Item6.Id, Alias.S6);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item7.Id + 20, Alias.Tested),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Tested.Name).ToList());
    }

    private static List<string> Positional1(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var tested = op switch
        {
            AliasProbeOperator.Inner => sut.AliasOrder.Join(person, (a, p) => p.Id == a.BuyerId + 20),
            AliasProbeOperator.Left => sut.AliasOrder.LeftJoin(person, (a, p) => p.Id == a.BuyerId + 20),
            AliasProbeOperator.Right => sut.AliasOrder.RightJoin(person, (a, p) => p.Id == a.BuyerId + 20),
            AliasProbeOperator.Full => sut.AliasOrder.FullJoin(person, (a, p) => p.Id == a.BuyerId + 20),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Item2.Name).ToList());
    }

    private static List<string> Positional2(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join(person, (a, p) => p.Id == a.BuyerId);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item2.Id + 20),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item2.Id + 20),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item2.Id + 20),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item2.Id + 20),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Item3.Name).ToList());
    }

    private static List<string> Positional3(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join(person, (a, p) => p.Id == a.BuyerId)
            .Join(person, (a, p) => p.Id == a.Item2.Id);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item3.Id + 20),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item3.Id + 20),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item3.Id + 20),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item3.Id + 20),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Item4.Name).ToList());
    }

    private static List<string> Positional4(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join(person, (a, p) => p.Id == a.BuyerId)
            .Join(person, (a, p) => p.Id == a.Item2.Id)
            .Join(person, (a, p) => p.Id == a.Item3.Id);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item4.Id + 20),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item4.Id + 20),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item4.Id + 20),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item4.Id + 20),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Item5.Name).ToList());
    }

    private static List<string> Positional5(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join(person, (a, p) => p.Id == a.BuyerId)
            .Join(person, (a, p) => p.Id == a.Item2.Id)
            .Join(person, (a, p) => p.Id == a.Item3.Id)
            .Join(person, (a, p) => p.Id == a.Item4.Id);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item5.Id + 20),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item5.Id + 20),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item5.Id + 20),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item5.Id + 20),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Item6.Name).ToList());
    }

    private static List<string> Positional6(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join(person, (a, p) => p.Id == a.BuyerId)
            .Join(person, (a, p) => p.Id == a.Item2.Id)
            .Join(person, (a, p) => p.Id == a.Item3.Id)
            .Join(person, (a, p) => p.Id == a.Item4.Id)
            .Join(person, (a, p) => p.Id == a.Item5.Id);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item6.Id + 20),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item6.Id + 20),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item6.Id + 20),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item6.Id + 20),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Item7.Name).ToList());
    }

    private static List<string> Positional7(TestDataRepository sut, AliasProbeOperator op, AliasProbeOptions opt)
    {
        var person = sut.AliasPerson;
        var receiver = sut.AliasOrder
            .Join(person, (a, p) => p.Id == a.BuyerId)
            .Join(person, (a, p) => p.Id == a.Item2.Id)
            .Join(person, (a, p) => p.Id == a.Item3.Id)
            .Join(person, (a, p) => p.Id == a.Item4.Id)
            .Join(person, (a, p) => p.Id == a.Item5.Id)
            .Join(person, (a, p) => p.Id == a.Item6.Id);
        var tested = op switch
        {
            AliasProbeOperator.Inner => receiver.Join(person, (a, p) => p.Id == a.Item7.Id + 20),
            AliasProbeOperator.Left => receiver.LeftJoin(person, (a, p) => p.Id == a.Item7.Id + 20),
            AliasProbeOperator.Right => receiver.RightJoin(person, (a, p) => p.Id == a.Item7.Id + 20),
            AliasProbeOperator.Full => receiver.FullJoin(person, (a, p) => p.Id == a.Item7.Id + 20),
            _ => throw new UnreachableException($"unhandled operator {op}")
        };
        return Names(tested.Select(p => p.Item8.Name).ToList());
    }

    private static List<string> SourceLess(TestDataRepository sut, int arity, AliasProbeOperator op, AliasProbeOptions opt) => arity switch
    {
        1 => SourceLess1(sut, op, opt),
        2 => SourceLess2(sut, op, opt),
        3 => SourceLess3(sut, op, opt),
        4 => SourceLess4(sut, op, opt),
        5 => SourceLess5(sut, op, opt),
        6 => SourceLess6(sut, op, opt),
        _ => SourceLess7(sut, op, opt)
    };

    private static List<string> SourceExplicit(TestDataRepository sut, int arity, AliasProbeOperator op, AliasProbeOptions opt) => arity switch
    {
        1 => SourceExplicit1(sut, op, opt),
        2 => SourceExplicit2(sut, op, opt),
        3 => SourceExplicit3(sut, op, opt),
        4 => SourceExplicit4(sut, op, opt),
        5 => SourceExplicit5(sut, op, opt),
        6 => SourceExplicit6(sut, op, opt),
        _ => SourceExplicit7(sut, op, opt)
    };

    private static List<string> Positional(TestDataRepository sut, int arity, AliasProbeOperator op, AliasProbeOptions opt) => arity switch
    {
        1 => Positional1(sut, op, opt),
        2 => Positional2(sut, op, opt),
        3 => Positional3(sut, op, opt),
        4 => Positional4(sut, op, opt),
        5 => Positional5(sut, op, opt),
        6 => Positional6(sut, op, opt),
        _ => Positional7(sut, op, opt)
    };
}

    /// <summary>The conditional operator under test for one matrix cell.</summary>
    public enum AliasProbeOperator
    {
        Inner,
        Left,
        Right,
        Full
    }

    /// <summary>The options call shape of the source-less surface for one matrix cell.</summary>
    public enum AliasProbeOptions
    {
        Omitted,
        ExplicitNull,
        Callback
    }

    /// <summary>The chain forms compared by one matrix cell.</summary>
    public enum AliasProbeForm
    {
        SourceLess,
        SourceExplicit,
        Positional
    }

