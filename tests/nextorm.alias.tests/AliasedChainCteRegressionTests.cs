using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

public class AliasedChainCteRegressionTests
{
    [Fact]
    public void Joined_cte_from_aliased_chain_preserves_typed_join_alias()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var people = ctx.From<Person>(b => b.Table("person"))
                .WithAlias(Alias.Root)
                .Join<Person>(ctx.From<Person>(b => b.Table("person")), (a, b) => a.Root.Id == b.Id, Alias.Buyer)
                .Select(p => new { Id = p.Buyer.Id, Name = p.Buyer.Name })
                .AsCte("aliased_people");

            var rows = ctx.From<Order>(b => b.Table("orders"))
                .WithAlias(Alias.Root)
                .Join(people, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer)
                .Select(p => p.Buyer.Name)
                .ToList();

            rows.Should().ContainSingle().Which.Should().Be("Buyer");
            sql.Statements[^1].Should().Be(
                "with aliased_people as (" +
                "select t2.Id, t2.Name\n" +
                " from person as 't1'\n" +
                " join person as 't2' on t1.Id = t2.Id) " +
                "select t2.Name\n" +
                " from orders as 't1'\n" +
                " join aliased_people as 't2' on t1.BuyerId = t2.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Select_group_producer_is_consumed_by_aliased_chain()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var grouped = ctx.From<Person>(b => b.Table("person"))
                .SelectGroup(p => new { p.Id, p.Name })
                .AsCte("grouped_people");

            var rows = ctx.From<Order>(b => b.Table("orders"))
                .WithAlias(Alias.Root)
                .Join(grouped, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer)
                .Select(p => p.Buyer.Name)
                .ToList();

            rows.Should().ContainSingle().Which.Should().Be("Buyer");
            sql.Statements[^1].Should().Be(
                "with grouped_people as (select Id, Name\n" +
                " from person\n" +
                " group by Id, Name) " +
                "select t2.Name\n" +
                " from orders as 't1'\n" +
                " join grouped_people as 't2' on t1.BuyerId = t2.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
