using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Alias-slot coverage for an ordinary typed CTE (#146 slice A). A <c>Cte&lt;T&gt;</c> read through
/// <c>From(cte)</c> and joined through a generated alias slot must be referenced by the CTE name under a
/// distinct table alias, with its columns binding to that alias. The suite also covers a self-join of
/// the same typed CTE and a join of two distinct same-type typed CTEs.
/// </summary>
public class TypedCteAliasTests
{
    [Fact]
    public void Typed_cte_joined_as_alias_slot_gets_its_own_alias_and_binds_columns()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var peopleCte = ctx.From<Person>(b => b.Table("person")).ToCommand().AsCte("people_cte");

            var rows = orders
                .Join<Person>(ctx.From(peopleCte), (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, BuyerName = p.Buyer.Name })
                .ToList();

            // The single order's buyer is person 10 ('Buyer').
            rows.Should().ContainSingle();
            rows[0].OrderId.Should().Be(1);
            rows[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            rows[0].BuyerName.Should().Be("Buyer");

            // The CTE is declared once, referenced by name in the join and given its own alias; the
            // projected columns bind to that alias, not to a derived-table wrapper.
            var last = sql.Statements[^1];
            last.Should().Contain("with people_cte as (");
            last.Should().Contain("join people_cte as 't2'");
            last.Should().Contain("t2.Id");
            last.Should().Contain("t2.Name");
            last.Should().NotContain("join (select");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Self_join_of_same_typed_cte_uses_two_aliases_and_one_declaration()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var peopleCte = ctx.From<Person>(b => b.Table("person")).ToCommand().AsCte("people_cte");

            // The typed CTE is the base source and is also joined as an alias slot: a real self-join of
            // the same descriptor, whose columns must bind to two distinct aliases.
            var rows = ctx.From(peopleCte)
                .Join<Person>(ctx.From(peopleCte), (a, b) => a.Id == b.Id, Alias.Buyer)
                .Select(p => new { Id = p.Item1.Id, BuyerName = p.Buyer.Name })
                .ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(10, 20);
            rows.Single(r => r.Id == 10).BuyerName.Should().Be("Buyer");
            rows.Single(r => r.Id == 20).BuyerName.Should().Be("Approver");

            var last = sql.Statements[^1];
            last.Should().Contain("from people_cte as 't1'");
            last.Should().Contain("join people_cte as 't2'");
            last.Should().Contain("t2.Id");

            // The same descriptor is reused on both sides, so its declaration must not be duplicated.
            CountOccurrences(last, "people_cte as (").Should().Be(1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Join_of_two_distinct_same_type_typed_ctes_binds_each_to_its_alias()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var buyersCte = ctx.From<Person>(b => b.Table("person")).Where(p => p.Id < 20).ToCommand().AsCte("buyers_cte");
            var approversCte = ctx.From<Person>(b => b.Table("person")).Where(p => p.Id > 10).ToCommand().AsCte("approvers_cte");

            var rows = orders
                .Join<Person>(ctx.From(buyersCte), (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Join<Person>(ctx.From(approversCte), (a, p) => a.Item1.ApproverId == p.Id, Alias.Approver)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, ApproverId = p.Approver.Id })
                .ToList();

            // Only order 1 has buyer 10 (< 20) and approver 20 (> 10); each side must keep its own body.
            rows.Should().ContainSingle();
            rows[0].OrderId.Should().Be(1);
            rows[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            rows[0].ApproverId.Should().Be(AliasSqliteDatabase.ApproverId);

            var last = sql.Statements[^1];
            last.Should().Contain("with buyers_cte as (");
            last.Should().Contain(", approvers_cte as (");
            last.Should().Contain("join buyers_cte as 't2'");
            last.Should().Contain("join approvers_cte as 't3'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
