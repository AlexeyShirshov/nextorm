using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// End-to-end coverage for the generated alias projections on SQLite: the same CLR type joined twice
/// must resolve to two distinct slots/table aliases, and every supported operator must forward the
/// right <c>JoinType</c>.
/// </summary>
public class AliasSqliteEndToEndTests
{
    [Fact]
    public void Repeated_joined_type_resolves_to_distinct_slots_and_distinct_sql_aliases()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var chained = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver);

            var buyerIds = chained.Select(p => p.Buyer.Id).ToList();
            var approverIds = chained.Select(p => p.Approver.Id).ToList();

            buyerIds.Should().Equal(AliasSqliteDatabase.BuyerId);
            approverIds.Should().Equal(AliasSqliteDatabase.ApproverId);
            buyerIds[0].Should().NotBe(approverIds[0]);

            sql.Statements.Should().HaveCount(2);
            var buyerSql = sql.Statements[0];
            var approverSql = sql.Statements[1];

            // Both joins are present and carry distinct aliases t2 (buyer) and t3 (approver).
            buyerSql.Should().Contain("person as 't2'").And.Contain("person as 't3'");
            approverSql.Should().Contain("person as 't2'").And.Contain("person as 't3'");
            buyerSql.Should().Contain("select t2.Id");
            approverSql.Should().Contain("select t3.Id");
            buyerSql.Should().NotBe(approverSql);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Inner_alias_join_matches_positional_join()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var positional = orders.Join(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
            var alias = orders.Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Id).ToList();

            AssertParity(" join ", positional, alias, sql.Statements[^1]);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Left_alias_join_matches_positional_join()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var positional = orders.LeftJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
            var alias = orders.LeftJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Id).ToList();

            AssertParity(" left join ", positional, alias, sql.Statements[^1]);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Right_alias_join_matches_positional_join()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var positional = orders.RightJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
            var alias = orders.RightJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Id).ToList();

            AssertParity(" right join ", positional, alias, sql.Statements[^1]);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Full_alias_join_matches_positional_join()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var positional = orders.FullJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
            var alias = orders.FullJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Id).ToList();

            AssertParity(" full join ", positional, alias, sql.Statements[^1]);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Cross_alias_join_matches_positional_join()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var positional = orders.CrossJoin(people).Select(p => p.Item2.Id).ToList();
            var alias = orders.CrossJoin(people, Alias.Buyer).Select(p => p.Buyer.Id).ToList();

            AssertParity(" cross join ", positional, alias, sql.Statements[^1]);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static void AssertParity(string keyword, IEnumerable<int> positional, IEnumerable<int> alias, string sql)
    {
        alias.OrderBy(x => x).Should().Equal(positional.OrderBy(x => x));
        sql.Should().Contain(keyword);
    }
}
