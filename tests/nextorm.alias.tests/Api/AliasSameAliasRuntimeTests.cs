using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;
using NextORM.Sqlite;

namespace NextORM.AliasTests;

/// <summary>
/// End-to-end coverage for issue #206: two INDEPENDENT queries reuse the SAME alias letter
/// (<c>Alias.Target</c>) but join DIFFERENT entity types. The generator collapses the two chains to
/// one generic <c>Join&lt;TJoin&gt;</c> extension (the last-step joined type is the method type
/// parameter), so both must compile without <c>CS0111</c> and each must resolve to the correct
/// slot/table at runtime.
/// </summary>
public class AliasSameAliasRuntimeTests
{
    [Fact]
    public void Same_alias_different_joined_types_across_two_queries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-alias-samealias-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "create table orders (Id integer primary key, BuyerId integer, ApproverId integer);" +
                "create table person (Id integer primary key, Name text);" +
                "create table product (Id integer primary key, OrderId integer, Name text);" +
                "insert into orders (Id, BuyerId, ApproverId) values (1, 10, 20);" +
                "insert into person (Id, Name) values (10, 'Buyer'), (20, 'Approver');" +
                "insert into product (Id, OrderId, Name) values (100, 1, 'Widget');";
            cmd.ExecuteNonQuery();
        }

        var sql = new SqlRecordingInterceptor();
        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().AddInterceptor(sql));
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));
            var products = ctx.From<Product>(b => b.Table("product"));

            // Query 1: Alias.Target binds the Person join at slot 2.
            var buyerIds = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Target)
                .Select(p => p.Target.Id)
                .ToList();

            // Query 2: the SAME Alias.Target, but this join binds Product at slot 2.
            var productIds = orders
                .Join<Product>(products, (a, b) => a.Id == b.OrderId, Alias.Target)
                .Select(p => p.Target.Id)
                .ToList();

            buyerIds.Should().Equal(10);
            productIds.Should().Equal(100);

            sql.Statements.Should().HaveCount(2);
            sql.Statements[0].Should().Contain("person as 't2'").And.Contain("select t2.Id");
            sql.Statements[1].Should().Contain("product as 't2'").And.Contain("select t2.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
