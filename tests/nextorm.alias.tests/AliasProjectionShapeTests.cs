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
        var projection = typeof(AliasProjection_P1_A2_Buyer_A3_Approver<Order, Person, Person>);
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
    public void Positional_join_after_alias_binds_the_generated_receiver()
    {
        // #160 receiver-binding negative: the positional step after an alias must select the generated
        // `new` instance overload, whose static return type is the slot-encoded builder. If the inherited
        // EntityBuilder.Join were selected instead, this assignment would not compile (the static type
        // would be JoinedEntityBuilder<...>) — exactly the failure the mechanism prevents. The r=1
        // "positional cannot follow an alias" fail-closed boundary is superseded by free mixing.
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            AliasJoin_P1_A2_Buyer_P3<Order, Person, Person> chained = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join(people, (a, b) => a.Item2.Id == b.Id);

            chained.Select(p => p.Item3.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
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
        var projection = typeof(AliasProjection_P1_A2_Buyer_A3_Approver<Order, Person, Person>);

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
