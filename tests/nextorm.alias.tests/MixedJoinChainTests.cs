using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Free mixing of positional and alias joins (issue #160, Phase 1). An alias slot and the positional
/// <c>ItemK</c> of the same slot are the same table; chains may go alias-to-positional,
/// positional-to-alias and alternate. Every mixed step routes through the generated surface and the
/// <c>JoinAlias</c> seam, so the SQL aliases keep matching the chain order.
/// </summary>
public class MixedJoinChainTests
{
    [Fact]
    public void Alias_then_positional_join_keeps_slot_identity_and_distinct_sql_aliases()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var chained = orders
                .Join<Person>(people, (o, p) => o.BuyerId == p.Id, Alias.Buyer) // slot 2, alias
                .Join(people, (p, x) => p.Item2.Id == x.Id);                     // slot 3, positional

            var rows = chained
                .Select(p => new { Alias = p.Buyer.Name, Positional = p.Item3.Name })
                .ToList();

            rows.Should().HaveCount(1);
            rows[0].Alias.Should().Be("Buyer");
            rows[0].Positional.Should().Be("Buyer");

            // The alias slot and ItemK are the same table, not two different ones.
            chained.Select(p => p.Item2.Id).ToList().Should().Equal(chained.Select(p => p.Buyer.Id).ToList());

            var last = sql.Statements[^1];
            last.Should().Contain("person as 't2'").And.Contain("person as 't3'");
            last.Should().Contain("t2.Id").And.Contain("t3.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Positional_then_alias_join_binds_the_alias_to_the_next_slot()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var chained = orders
                .Join(people, (o, p) => o.BuyerId == p.Id)                          // slot 2, positional
                .Join<Person>(people, (p, a) => p.Item2.Id == a.Id, Alias.Approver); // slot 3, alias

            var rows = chained
                .Select(p => new { Positional = p.Item2.Name, Alias = p.Approver.Name })
                .ToList();

            rows.Should().HaveCount(1);
            rows[0].Positional.Should().Be("Buyer");
            rows[0].Alias.Should().Be("Buyer");

            var last = sql.Statements[^1];
            last.Should().Contain("person as 't2'").And.Contain("person as 't3'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Alternating_alias_positional_alias_assigns_slots_in_chain_order()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var chained = orders
                .Join<Person>(people, (o, p) => o.BuyerId == p.Id, Alias.Buyer)       // slot 2, alias
                .Join(people, (p, x) => p.Buyer.Id == x.Id)                           // slot 3, positional
                .Join<Person>(people, (p, a) => p.Item3.Id == a.Id, Alias.Approver);   // slot 4, alias

            var rows = chained
                .Select(p => new { Buyer = p.Buyer.Name, Third = p.Item3.Name, Approver = p.Approver.Name })
                .ToList();

            rows.Should().HaveCount(1);
            rows[0].Buyer.Should().Be("Buyer");
            rows[0].Third.Should().Be("Buyer");
            rows[0].Approver.Should().Be("Buyer");

            var last = sql.Statements[^1];
            last.Should().Contain("person as 't2'")
                .And.Contain("person as 't3'")
                .And.Contain("person as 't4'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Mixed_chain_matches_the_equivalent_positional_chain()
    {
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var mixed = orders
                .Join<Person>(people, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Join(people, (p, x) => p.Item2.Id == x.Id)
                .Select(p => p.Item3.Id)
                .ToList();

            var positional = orders
                .Join(people, (o, p) => o.BuyerId == p.Id)
                .Join(people, (p, x) => p.Item2.Id == x.Id)
                .Select(p => p.Item3.Id)
                .ToList();

            mixed.Should().Equal(positional);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void A_mixed_alias_chain_fails_closed_on_the_in_memory_provider()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();
        var people = ctx.From<Person>();

        // The alias step is refused at construction, so a positional step after it can never run in memory.
        Action act = () => orders.Join<Person>(people, (o, p) => o.BuyerId == p.Id, Alias.Buyer);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Pure_positional_in_memory_chain_is_not_refused()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();
        var people = ctx.From<Person>();

        // Negative of the alias refusal: a positional-only chain must never enter the alias seam.
        Action act = () => orders.Join(people, (o, p) => o.BuyerId == p.Id).Select(p => p.Item2.Id).ToList();

        act.Should().NotThrow();
    }

    [Fact]
    public void Digit_ending_alias_is_a_name_and_does_not_shift_the_slot()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var chained = orders.Join<Person>(people, (o, p) => o.BuyerId == p.Id, Alias.Buyer2);

            // R160-01 negative: 'Buyer2' is an alias name in slot 2, not a slot number. Item2 and Buyer2
            // must be the same table and no later slot may shift.
            chained.Select(p => p.Buyer2.Id).ToList()
                .Should().Equal(chained.Select(p => p.Item2.Id).ToList());
            sql.Statements[^1].Should().Contain("person as 't2'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
