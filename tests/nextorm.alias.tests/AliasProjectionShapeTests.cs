using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Shape-level coverage for a generated alias projection: retained <c>ItemN</c> members keep working
/// alongside the lexical alias members, and the alias members resolve to the right projection slot.
/// </summary>
public class AliasProjectionShapeTests
{
    [Fact]
    public void Alias_projection_retains_item_members_and_exposes_alias_members()
    {
        var projection = typeof(AliasProjection_Buyer_Approver<Order, Person, Person>);
        projection.GetProperty("Item1").Should().NotBeNull();
        projection.GetProperty("Item2").Should().NotBeNull();
        projection.GetProperty("Item3").Should().NotBeNull();
        projection.GetProperty("Buyer").Should().NotBeNull();
        projection.GetProperty("Approver").Should().NotBeNull();

        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));
            var chained = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver);

            // Existing positional usage (ItemN) yields exactly the alias members' values.
            chained.Select(p => p.Item2.Id).ToList().Should().Equal(chained.Select(p => p.Buyer.Id).ToList());
            chained.Select(p => p.Item3.Id).ToList().Should().Equal(chained.Select(p => p.Approver.Id).ToList());
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Positional_join_cannot_follow_an_alias_join()
    {
        // R159-12 boundary (CHECK round 2): a positional Join/Apply applied to the generated alias
        // projection builder must fail closed at construction (CreateJoined) instead of nesting the
        // projection as Item1 and failing later in the planner. Legacy positional joins without aliases
        // are unaffected (covered by the parity tests).
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            var chained = orders.Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer);

            var act = () => chained.Join(people, (a, b) => a.Item1.BuyerId == b.Id);

            act.Should().Throw<NotSupportedException>()
                .WithMessage("*cannot follow an alias Join*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Alias_members_carry_join_slot_attribute_and_resolve_to_the_right_slot()
    {
        var projection = typeof(AliasProjection_Buyer_Approver<Order, Person, Person>);

        projection.GetProperty("Buyer")!.GetCustomAttribute<JoinSlotAttribute>()!.Position.Should().Be(2);
        projection.GetProperty("Approver")!.GetCustomAttribute<JoinSlotAttribute>()!.Position.Should().Be(3);

        // ProjectionAliasCache is internal, so the production resolver is exercised through reflection:
        // the attribute is proven to map to the right zero-based slot, not merely to carry a number.
        GetMemberPosition(projection.GetProperty("Buyer")!).Should().Be(1);
        GetMemberPosition(projection.GetProperty("Approver")!).Should().Be(2);
        GetMemberPosition(projection.GetProperty("Item2")!).Should().Be(1);
        GetMemberPosition(projection.GetProperty("Item3")!).Should().Be(2);
    }

    private static int GetMemberPosition(MemberInfo member)
    {
        var cache = typeof(JoinSlotAttribute).Assembly.GetType("NextORM.Core.ProjectionAliasCache")
            ?? throw new InvalidOperationException("ProjectionAliasCache type was not found.");
        var method = cache.GetMethod("GetMemberPosition", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("ProjectionAliasCache.GetMemberPosition was not found.");
        return (int)method.Invoke(null, [member])!;
    }
}
