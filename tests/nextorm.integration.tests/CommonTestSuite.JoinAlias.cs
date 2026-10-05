using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_integration_tests;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    // One order per relationship, plus an unlinked person for RIGHT/FULL. Buyer and Approver are the
    // same AliasPerson CLR type, so a slot mix-up cannot pass by returning the same multiset.
    [Fact]
    public void Alias_inner_join_returns_matched_rows()
    {
        var ids = _sut.AliasOrder
            .Join<AliasPerson>(_sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20);
    }

    [Fact]
    public void Alias_left_join_returns_all_orders()
    {
        var ids = _sut.AliasOrder
            .LeftJoin<AliasPerson>(_sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20);
    }

    [Fact]
    public void Alias_right_join_keeps_unmatched_right_rows()
    {
        var ids = _sut.AliasOrder
            .RightJoin<AliasPerson>(_sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20, 30);
    }

    [Fact]
    public void Alias_full_join_returns_both_sides_or_fails_closed_without_full_join()
    {
        if (!Provider.SupportsFullJoin)
        {
            // MySQL/MariaDB have no FULL JOIN: the alias operator must fail closed, not be skipped.
            var act = () => _sut.AliasOrder
                .FullJoin<AliasPerson>(_sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Select(p => p.Buyer.Id)
                .ToList();

            act.Should().Throw<NotSupportedException>();
            return;
        }

        var ids = _sut.AliasOrder
            .FullJoin<AliasPerson>(_sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList()
            .OrderBy(id => id)
            .ToList();

        ids.Should().Equal(10, 20, 30);
    }

    [Fact]
    public void Alias_cross_join_returns_cartesian_product()
    {
        var ids = _sut.AliasOrder
            .CrossJoin(_sut.AliasPerson, Alias.Buyer)
            .Select(p => p.Buyer.Id)
            .ToList();

        ids.Should().HaveCount(6);
    }

    [Fact]
    public void Alias_buyer_and_approver_resolve_to_distinct_ids()
    {
        var chained = _sut.AliasOrder
            .Join<AliasPerson>(_sut.AliasPerson, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
            .Join<AliasPerson>(_sut.AliasPerson, (p, a) => p.Item1.ApproverId == a.Id, Alias.Approver);

        var rows = chained
            .Select(p => new { Buyer = p.Buyer.Id, Approver = p.Approver.Id })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Buyer != r.Approver);
        rows.Select(r => r.Buyer).OrderBy(id => id).Should().Equal(10, 20);
        rows.Select(r => r.Approver).OrderBy(id => id).Should().Equal(10, 20);
    }

    [Fact]
    public void Alias_join_across_a_method_boundary_resolves_both_slots()
    {
        var person = _sut.AliasPerson;
        var intermediate = _sut.AliasOrder.Join<AliasPerson>(person, (o, p) => o.BuyerId == p.Id, Alias.Buyer);
        var chained = AddApprover(intermediate, person);

        var rows = chained
            .Select(p => new { Buyer = p.Buyer.Id, Approver = p.Approver.Id })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Buyer != r.Approver);
    }

    // A method that names the generated builder types in its signature and returns the second alias join.
    private static AliasJoin_Buyer_Approver<AliasOrder, AliasPerson, AliasPerson> AddApprover(
        AliasJoin_Buyer<AliasOrder, AliasPerson> builder,
        EntityBuilder<AliasPerson> person)
        => builder.Join<AliasPerson>(person, (p, a) => p.Item1.ApproverId == a.Id, Alias.Approver);
}
