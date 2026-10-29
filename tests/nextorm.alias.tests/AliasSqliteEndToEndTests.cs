using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;
using NextORM.Sqlite;

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

    // ---------------------------------------------------------------------------------------------
    // #322: the source-less alias form (no `EntityBuilder<Person>` argument, e.g.
    // `orders.Join<Person>((a, b) => ..., Alias.Buyer)`) must return the same rows as the
    // source-explicit alias form and as the positional join, including the unmatched rows of an outer
    // join. The fixture below has two orders (order 2 has no matching buyer) and three persons (persons
    // 20 and 30 are referenced by no order), so Left/Right/Full each expose a different missing side and
    // a slot or JoinType mix-up cannot pass by returning the same multiset.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(JoinType.Inner)]
    [InlineData(JoinType.Left)]
    [InlineData(JoinType.Right)]
    [InlineData(JoinType.Full)]
    public void Source_less_alias_join_returns_the_same_rows_as_the_source_explicit_and_positional_joins(JoinType joinType)
    {
        var (path, ctx, sql) = CreateUnmatchedContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var sourceLess = JoinedNames(joinType, orders, people, sourceLess: true);
            var sourceExplicit = JoinedNames(joinType, orders, people, sourceLess: false);
            var positional = PositionalNames(joinType, orders, people);

            // Non-vacuous expectation per operator: every outer operator drops a different side.
            sourceLess.Should().Equal(ExpectedNames(joinType));
            sourceLess.Should().Equal(sourceExplicit);
            sourceLess.Should().Equal(positional);

            // The source-less chain renders the same join clause and the same slot-2 alias.
            sql.Statements[0].Should().Contain(Keyword(joinType), "the statement was rendered for {0}", joinType);
            sql.Statements[0].Should().Contain("person as 't2'");
            sql.Statements[0].Should().Be(sql.Statements[1], "source-less and source-explicit must render the same SQL");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void A_two_step_source_less_alias_chain_matches_the_source_explicit_and_positional_chain()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var sourceLess = orders
                .Join<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>((a, b) => a.Item1.ApproverId == b.Id, Alias.Approver);
            var sourceExplicit = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver);
            var positional = orders
                .Join(people, (a, b) => a.BuyerId == b.Id)
                .Join(people, (a, b) => a.Item1.ApproverId == b.Id);

            // The anonymous projection path (a control) must yield the same triples in every form.
            var sourceLessRows = sourceLess
                .Select(p => new { Order = p.Item1.Id, Buyer = p.Buyer.Id, Approver = p.Approver.Id })
                .ToList();
            var explicitRows = sourceExplicit
                .Select(p => new { Order = p.Item1.Id, Buyer = p.Buyer.Id, Approver = p.Approver.Id })
                .ToList();
            var positionalRows = positional
                .Select(p => new { Order = p.Item1.Id, Buyer = p.Item2.Id, Approver = p.Item3.Id })
                .ToList();

            sourceLessRows.Should().HaveCount(1);
            sourceLessRows[0].Buyer.Should().Be(AliasSqliteDatabase.BuyerId);
            sourceLessRows[0].Approver.Should().Be(AliasSqliteDatabase.ApproverId);
            sourceLessRows.Should().Equal(explicitRows);
            sourceLessRows.Should().Equal(positionalRows);

            var last = sql.Statements[^1];
            last.Should().Contain("person as 't2'").And.Contain("person as 't3'");
            sql.Statements[^3].Should().Contain("person as 't2'").And.Contain("person as 't3'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static string[] JoinedNames(
        JoinType joinType,
        EntityBuilder<Order> orders,
        EntityBuilder<Person> people,
        bool sourceLess)
    {
        var names = joinType switch
        {
            JoinType.Inner => sourceLess
                ? orders.Join<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList()
                : orders.Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList(),
            JoinType.Left => sourceLess
                ? orders.LeftJoin<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList()
                : orders.LeftJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList(),
            JoinType.Right => sourceLess
                ? orders.RightJoin<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList()
                : orders.RightJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList(),
            _ => sourceLess
                ? orders.FullJoin<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList()
                : orders.FullJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer).Select(p => p.Buyer.Name).ToList()
        };

        return Sorted(names);
    }

    private static string[] PositionalNames(JoinType joinType, EntityBuilder<Order> orders, EntityBuilder<Person> people)
    {
        var names = joinType switch
        {
            JoinType.Inner => orders.Join(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Name).ToList(),
            JoinType.Left => orders.LeftJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Name).ToList(),
            JoinType.Right => orders.RightJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Name).ToList(),
            _ => orders.FullJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Name).ToList()
        };

        return Sorted(names);
    }

    private static string[] ExpectedNames(JoinType joinType) => joinType switch
    {
        JoinType.Inner => ["Buyer"],
        JoinType.Left => ["<null>", "Buyer"],
        JoinType.Right => ["Approver", "Buyer", "Orphan"],
        _ => ["<null>", "Approver", "Buyer", "Orphan"]
    };

    private static string[] Sorted(IEnumerable<string> names)
        => names.Select(name => name ?? "<null>").OrderBy(name => name, StringComparer.Ordinal).ToArray();

    private static string Keyword(JoinType joinType) => joinType switch
    {
        JoinType.Inner => " join ",
        JoinType.Left => " left join ",
        JoinType.Right => " right join ",
        _ => " full join "
    };

    private static (string Path, SqliteDataContext Context, SqlRecordingInterceptor Sql) CreateUnmatchedContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-alias-unmatched-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "create table orders (Id integer primary key, BuyerId integer, ApproverId integer);" +
                "create table person (Id integer primary key, Name text);" +
                "insert into orders (Id, BuyerId, ApproverId) values (1, 10, 20), (2, 99, 20);" +
                "insert into person (Id, Name) values (10, 'Buyer'), (20, 'Approver'), (30, 'Orphan');";
            cmd.ExecuteNonQuery();
        }

        var sql = new SqlRecordingInterceptor();
        var context = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().AddInterceptor(sql));
        return (path, context, sql);
    }

    private static void AssertParity(string keyword, IEnumerable<int> positional, IEnumerable<int> alias, string sql)
    {
        alias.OrderBy(x => x).Should().Equal(positional.OrderBy(x => x));
        sql.Should().Contain(keyword);
    }
}
