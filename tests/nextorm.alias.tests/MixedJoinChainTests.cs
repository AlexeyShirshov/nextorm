using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Mixed join chains that interleave positional and alias joins. The generator serves a positional join
/// followed by an alias join (the alias belongs to the newly added, later table). The opposite order
/// (alias join, then positional join) would nest the alias projection as <c>Item1</c> of the result,
/// which the planner cannot map to a physical table; it fails closed at construction with an explicit
/// <see cref="NotSupportedException"/>. Deferred until an owner-approved design for nested-projection
/// alias inheritance exists (milestone 1.0.9-b, see docs/specs/status/join-alias-113-1.md).
/// </summary>
public class MixedJoinChainTests
{
    [Fact]
    public void Positional_then_alias_join_resolves_the_alias_to_the_second_join()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            // First join positional (buyer, slot 2), second join aliased (approver, slot 3). The alias
            // must resolve to the newly joined table, not to the earlier positional Person of the same
            // CLR type.
            var chained = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Buyer);

            var aliasIds = chained.Select(p => p.Buyer.Id).ToList();
            var item2Ids = chained.Select(p => p.Item2.Id).ToList();
            var item3Ids = chained.Select(p => p.Item3.Id).ToList();

            item2Ids.Should().Equal(AliasSqliteDatabase.BuyerId);
            aliasIds.Should().Equal(AliasSqliteDatabase.ApproverId);
            aliasIds.Should().Equal(item3Ids);
            aliasIds.Should().NotEqual(item2Ids);

            var lastSql = sql.Statements[^1];
            lastSql.Should().Contain("person as 't2'").And.Contain("person as 't3'");
            lastSql.Should().Contain("select t3.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Alias_then_positional_join_fails_closed_at_construction()
    {
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            // Alias join first (buyer, slot 2), then a positional join (approver, slot 3). The alias
            // projection would be nested as Item1 of the result, which the planner cannot map to a
            // table; the chain must be refused at construction, not with a BuildSqlCommandException
            // during planning. Deferred pending an owner-approved nested-projection alias design.
            var act = () => orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id);

            act.Should().Throw<NotSupportedException>()
                .WithMessage("*positional Join/Apply cannot follow an alias Join*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
