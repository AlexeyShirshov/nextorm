using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;
using NextORM.Sqlite;

namespace NextORM.AliasTests;

/// <summary>
/// Regression coverage for a <c>Where</c> placed on the base source, between two alias joins, or after
/// them. A predicate written before/around a second alias join must be rewritten onto the retained
/// projection slot it refers to (Item1 for the base, Item2/Buyer for the first joined table), not
/// silently re-rooted onto the first item.
/// </summary>
public class AliasJoinWhereTests
{
    [Fact]
    public void Where_on_base_before_single_alias_join_keeps_the_base_slot()
    {
        var (path, ctx, sql) = CreateContext();
        try
        {
            var people = ctx.From<Person>(b => b.Table("person"));

            var rows = ctx.From<Order>(b => b.Table("orders"))
                .Where(o => o.Id == 1)
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Select(p => p.Item1.Id)
                .ToList();

            rows.Should().Equal(1);
            sql.Statements[^1].Should().Contain("t1.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Where_on_base_before_a_second_alias_join_keeps_the_base_slot()
    {
        var (path, ctx, sql) = CreateContext();
        try
        {
            var people = ctx.From<Person>(b => b.Table("person"));

            var rows = ctx.From<Order>(b => b.Table("orders"))
                .Where(o => o.Id == 1)
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver)
                .Select(p => p.Buyer.Id)
                .ToList();

            rows.Should().Equal(10);

            // The base filter stays with the base relation (the retained source subquery), while both
            // joined tables are still present and reachable.
            var last = sql.Statements[^1];
            last.Should().Contain("where Id = 1");
            last.Should().Contain("join person as 't2'");
            last.Should().Contain("join person as 't3'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Where_between_first_and_second_alias_join_targets_the_first_joined_slot()
    {
        var (path, ctx, sql) = CreateContext();
        try
        {
            var people = ctx.From<Person>(b => b.Table("person"));

            // The generated surface erases the alias builder type at .Where, so the second join is
            // declared through the runtime JoinAlias seam (the path ApplyWhereToAliasJoined serves).
            var firstJoin = ctx.From<Order>(b => b.Table("orders"))
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer);
            var filtered = firstJoin.Where(p => p.Buyer.Id == 20);

            var chained = filtered.JoinAlias<AliasJoin_Buyer_Approver<Order, Person, Person>, AliasProjection_Buyer_Approver<Order, Person, Person>, Person>(
                static dc => new AliasJoin_Buyer_Approver<Order, Person, Person>(dc),
                people,
                (a, b) => a.Item1.ApproverId == b.Id);

            var rows = chained.Select(p => p.Item1.Id).ToList();

            rows.Should().Equal(2);
            sql.Statements[^1].Should().Contain("t2.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Where_after_second_alias_join_targets_the_second_joined_slot()
    {
        var (path, ctx, sql) = CreateContext();
        try
        {
            var people = ctx.From<Person>(b => b.Table("person"));

            var rows = ctx.From<Order>(b => b.Table("orders"))
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver)
                .Where(p => p.Approver.Id == 20)
                .Select(p => p.Item1.Id)
                .ToList();

            rows.Should().Equal(1);
            sql.Statements[^1].Should().Contain("t3.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static (string Path, SqliteDataContext Context, SqlRecordingInterceptor Sql) CreateContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-alias-where-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "create table orders (Id integer primary key, BuyerId integer, ApproverId integer);" +
                "create table person (Id integer primary key, Name text);" +
                "insert into orders (Id, BuyerId, ApproverId) values (1, 10, 20), (2, 20, 10);" +
                "insert into person (Id, Name) values (10, 'Buyer'), (20, 'Approver');";
            cmd.ExecuteNonQuery();
        }

        var sql = new SqlRecordingInterceptor();
        var context = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().AddInterceptor(sql));
        return (path, context, sql);
    }
}
