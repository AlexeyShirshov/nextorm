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

    private static readonly string[] JoinOperators =
        ["Join", "LeftJoin", "RightJoin", "FullJoin", "CrossJoin", "CrossApply", "OuterApply"];

    [Fact]
    public void Generated_new_instance_transitions_hide_the_inherited_positional_overloads()
    {
        // E160-21 shape: every generated positional transition is a `new` instance method declared on
        // the chain builder, so it hides the inherited EntityBuilder<TEntity>.Join/Apply overloads.
        // Without that hiding a positional step after an alias would bind the inherited member (and
        // return JoinedEntityBuilder) instead of routing the step through the JoinAlias seam.
        var receivers = new[]
        {
            typeof(AliasJoin_P1_A2_Buyer<Order, Person>), // declares the positional transition after the alias join
            typeof(AliasJoin_A1_Root<Order>),             // declares every positional transition after the root alias
        };

        foreach (var receiver in receivers)
        {
            receiver.BaseType!.GetGenericTypeDefinition().Should().Be(typeof(EntityBuilder<>));
            var baseType = typeof(EntityBuilder<>).MakeGenericType(receiver.BaseType.GetGenericArguments().Single());

            var declared = receiver
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => JoinOperators.Contains(method.Name))
                .ToArray();

            declared.Should().NotBeEmpty($"{receiver.Name} must declare generated `new` transitions");
            declared.Should().OnlyContain(method => !method.IsStatic && method.IsHideBySig);
            declared.Should().OnlyContain(method =>
                method.ReturnType.Name.StartsWith("AliasJoin_", StringComparison.Ordinal));

            // The inherited positional overload exists on the base and is exactly the hidden member.
            foreach (var method in declared)
            {
                baseType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Should().Contain(baseMethod => baseMethod.Name == method.Name,
                        $"{receiver.Name}.{method.Name} must hide an inherited {baseType.Name}.{method.Name}");
            }
        }

        // The root alias receiver emits the full operator set (the call site is not observable when the
        // root is stored in a variable), so no positional-after-root operator is left unhidden.
        typeof(AliasJoin_A1_Root<Order>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => JoinOperators.Contains(method.Name))
            .Select(method => method.Name)
            .Distinct()
            .Should().BeEquivalentTo(JoinOperators);
    }

    [Fact]
    public void Generated_new_instance_transitions_win_over_inherited_and_keep_alias_extensions_applicable()
    {
        // E160-21 binding: the concrete static return type proves overload resolution selects the
        // generated `new` instance transition; the inherited EntityBuilder<TEntity>.Join would yield
        // JoinedEntityBuilder<...> and these assignments would not compile. The marker-taking alias
        // extension must stay applicable on the same receivers (including the pure-positional
        // JoinedEntityBuilder prefix), so alias steps still bind the generated extension surface.
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));

            AliasJoin_P1_A2_Buyer_P3<Order, Person, Person> positionalAfterAlias = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join(people, (a, b) => a.Item2.Id == b.Id);

            AliasJoin_A1_Root_P2<Order, Person> positionalAfterRoot = ctx.From<Order>(b => b.Table("orders"))
                .WithAlias(Alias.Root)
                .Join(people, (o, p) => o.Root.BuyerId == p.Id);

            AliasJoin_P1_A2_Buyer_A3_Approver<Order, Person, Person> aliasAfterAlias = orders
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver);

            AliasJoin_A1_Root_A2_Buyer<Order, Person> aliasAfterRoot = ctx.From<Order>(b => b.Table("orders"))
                .WithAlias(Alias.Root)
                .Join<Person>(people, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer);

            // Alias extension stays applicable on the pure-positional JoinedEntityBuilder prefix.
            AliasJoin_P1_P2_A3_Buyer2<Order, Person, Person> aliasAfterJoined = orders
                .Join(people, (o, p) => o.BuyerId == p.Id)
                .Join<Person>(people, (p, a) => p.Item1.ApproverId == a.Id, Alias.Buyer2);

            positionalAfterAlias.Select(p => p.Item3.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            positionalAfterRoot.Select(p => p.Item2.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            aliasAfterAlias.Select(p => p.Approver.Id).ToList().Should().Equal(AliasSqliteDatabase.ApproverId);
            aliasAfterRoot.Select(p => p.Buyer.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            aliasAfterJoined.Select(p => p.Buyer2.Id).ToList().Should().Equal(AliasSqliteDatabase.ApproverId);
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
