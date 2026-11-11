using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;
using NextORM.Sqlite;

namespace NextORM.AliasTests;

/// <summary>
/// T324 D6b1 — E01/E06 (AC01, AC06) on the <em>generated</em> alias surface: the correlated
/// <c>CROSS APPLY</c>/<c>OUTER APPLY</c> transitions of an after-join alias receiver.
/// <para>
/// The alias suite runs on SQLite, which has no lateral/APPLY support, so nothing here renders SQL —
/// the SQL form and the correlation qualification are asserted by the SQL Server matrix
/// (<c>CorrelatedApplyAfterJoinMatrixTests</c>). What only this project can assert is the generated
/// surface itself: both lambda forms (<c>EntityBuilder&lt;T&gt;</c> and <c>QueryCommand&lt;T&gt;</c>) and
/// both APPLY kinds exist and add exactly one slot; the marker extension carries the marker last and no
/// <c>Action&lt;JoinOptions&gt;</c>; the instance <c>new</c> transition carries the options parameter and
/// no marker; and the named slots keep their <see cref="JoinSlotAttribute.Position"/> across the APPLY,
/// including the repeated-CLR-type shape (the applied body is <see cref="Person"/>, the same type as the
/// <c>Buyer</c> slot).
/// </para>
/// <para>
/// Every chain is written inline at its call site: the generator discovers generated builder types from
/// the chain syntax, so routing a receiver through a helper method would hide the chain that names it.
/// The instance transition is exercised on its own receiver (<c>Root3</c>/<c>Buyer3</c>): once a positional
/// continuation exists for a receiver schema the emitted instance APPLY members hide the marker
/// extensions of that same schema (an applicable instance member always wins), and the two forms must not
/// be asserted through one receiver.
/// </para>
/// </summary>
public class AliasCorrelatedApplyTests
{
    private static void WithContext(Action<SqliteDataContext, EntityBuilder<Person>> body)
    {
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            body(ctx, ctx.From<Person>(b => b.Table("person")));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static int? SlotOf(Type projection, string member)
        => projection.GetProperty(member)!.GetCustomAttribute<JoinSlotAttribute>()?.Position;

    // ---------------------------------------------------------------------------------------------
    // Marker extension: marker last, no options. Both kinds x both lambda return forms.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Marker_extension_adds_one_named_slot_for_both_kinds_and_both_lambda_forms()
    {
        WithContext((ctx, people) =>
        {
            var receiver = ctx.From<Order>(b => b.Table("orders"))
                .WithAlias(Alias.Root)
                .Join<Person>(people, (a, b) => a.Root.BuyerId == b.Id, Alias.Buyer);

            // One marker for all four transitions: the added slot's schema depends on the receiver and the
            // marker only, so the four (kind x lambda form) call sites collapse into one generated schema
            // instead of growing the frozen public type set by four pairs.
            var crossEntity = receiver.CrossApply(
                p => people.Where(x => x.Id == p.Buyer.Id), Alias.Approver);
            var outerEntity = receiver.OuterApply(
                p => people.Where(x => x.Id == p.Root.ApproverId), Alias.Approver);
            var crossQuery = receiver.CrossApply(
                p => people.Where(x => x.Id == p.Buyer.Id).ToCommand(), Alias.Approver);
            var outerQuery = receiver.OuterApply(
                p => people.Where(x => x.Id == p.Root.ApproverId).ToCommand(), Alias.Approver);

            AssertMarkerApply(crossEntity, JoinType.CrossApply);
            AssertMarkerApply(outerEntity, JoinType.OuterApply);
            AssertMarkerApply(crossQuery, JoinType.CrossApply);
            AssertMarkerApply(outerQuery, JoinType.OuterApply);

            // The marker form is an extension method: marker last, and never an options parameter.
            AssertMarkerExtensionSignature(receiver.GetType());
        });
    }

    private static void AssertMarkerApply<TProjection>(EntityBuilder<TProjection> applied, JoinType expectedJoinType)
    {
        applied.GetType().GetGenericTypeDefinition().Name.Should().Be("AliasJoin_A1_Root_A2_Buyer_A3_Approver`3");
        applied.GetType().GetGenericArguments().Should().HaveCount(
            3, "the alias APPLY adds one flat slot and never nests the alias projection");
        applied.GetType().GetGenericArguments()[0].Should().Be(typeof(Order));
        applied.GetType().GetGenericArguments()[2].Should().Be(typeof(Person));

        applied.Joins.Should().NotBeNull().And.HaveCount(2, "the preceding alias join survives and the APPLY is appended once");
        applied.Joins![0].JoinType.Should().Be(JoinType.Inner);
        applied.Joins[^1].JoinType.Should().Be(expectedJoinType);

        var projection = applied.GetType().BaseType!.GetGenericArguments()[0];
        SlotOf(projection, "Root").Should().Be(1);
        SlotOf(projection, "Buyer").Should().Be(2);
        SlotOf(projection, "Approver").Should().Be(3);
        projection.GetProperty("Item3").Should().NotBeNull("the added named slot keeps its ItemN identity");
    }

    private static void AssertMarkerExtensionSignature(Type receiverType)
    {
        var receiverDefinition = receiverType.GetGenericTypeDefinition();
        var applies = typeof(JoinAliasExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "CrossApply" or "OuterApply")
            .Where(method => method.IsDefined(typeof(ExtensionAttribute), false))
            .Where(method =>
            {
                var first = method.GetParameters()[0].ParameterType;
                return first.IsGenericType && first.GetGenericTypeDefinition() == receiverDefinition;
            })
            .ToArray();

        applies.Should().NotBeEmpty("the alias receiver must expose the generated marker APPLY extensions");
        foreach (var method in applies)
        {
            var parameters = method.GetParameters();
            parameters[^1].ParameterType.Name.Should().EndWith(
                "Marker", "the generated alias APPLY extension takes the alias marker as its last parameter");
            parameters.Should().NotContain(
                parameter => parameter.ParameterType == typeof(Action<JoinOptions>),
                "the marker form deliberately carries no options parameter");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Instance `new` transition: options allowed, no marker. Both kinds x both lambda return forms.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Instance_new_transition_adds_one_positional_slot_with_options_and_no_marker()
    {
        WithContext((ctx, people) =>
        {
            var receiver = ctx.From<Order>(b => b.Table("orders"))
                .WithAlias(Alias.Root3)
                .Join<Person>(people, (a, b) => a.Root3.BuyerId == b.Id, Alias.Buyer3);

            // Forces the positional continuation schema the generated instance APPLY transition extends.
            var positionalContinuation = receiver.Join(people, (l, r) => l.Buyer3.Id == r.Id);
            positionalContinuation.GetType().GetGenericTypeDefinition().Name.Should().Be(
                "AliasJoin_A1_Root3_A2_Buyer3_P3`3",
                "the generated positional continuation is what the instance APPLY transition extends");

            var optionsSeen = 0;
            var crossEntity = receiver.CrossApply(
                p => people.Where(x => x.Id == p.Buyer3.Id),
                _ => optionsSeen++);
            var outerQuery = receiver.OuterApply(
                p => people.Where(x => x.Id == p.Root3.ApproverId).ToCommand(),
                _ => optionsSeen++);

            optionsSeen.Should().Be(2, "the instance transition accepts and invokes the positional options callback");

            AssertInstanceApply(crossEntity, JoinType.CrossApply);
            AssertInstanceApply(outerQuery, JoinType.OuterApply);

            // The generated instance transitions are declared on the receiver, take options and no marker.
            var instanceApplies = receiver.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name is "CrossApply" or "OuterApply")
                .ToArray();

            instanceApplies.Should().NotBeEmpty();
            foreach (var method in instanceApplies)
            {
                var parameters = method.GetParameters();
                parameters.Should().HaveCount(2);
                parameters[^1].ParameterType.Should().Be(
                    typeof(Action<JoinOptions>), "the instance transition carries the options parameter");
                parameters.Should().NotContain(
                    parameter => parameter.ParameterType.Name.Contains("Marker", StringComparison.Ordinal),
                    "the instance transition must not take an alias marker");
            }
        });
    }

    private static void AssertInstanceApply<TProjection>(EntityBuilder<TProjection> applied, JoinType expectedJoinType)
    {
        applied.GetType().GetGenericTypeDefinition().Name.Should().Be("AliasJoin_A1_Root3_A2_Buyer3_P3`3");
        applied.GetType().GetGenericArguments().Should().HaveCount(3);
        applied.Joins.Should().NotBeNull().And.HaveCount(2);
        applied.Joins![^1].JoinType.Should().Be(expectedJoinType);

        var projection = applied.GetType().BaseType!.GetGenericArguments()[0];
        SlotOf(projection, "Root3").Should().Be(1);
        SlotOf(projection, "Buyer3").Should().Be(2);
        projection.GetProperty("Item3").Should().NotBeNull("the positional slot keeps its ItemN identity");
    }

    // ---------------------------------------------------------------------------------------------
    // Named-slot identity survives the APPLY, including a repeated CLR type in the applied body.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Named_slot_identity_survives_the_correlated_apply()
    {
        WithContext((ctx, people) =>
        {
            var receiver = ctx.From<Order>(b => b.Table("orders"))
                .WithAlias(Alias.Root)
                .Join<Person>(people, (a, b) => a.Root.BuyerId == b.Id, Alias.Buyer);

            var receiverProjection = receiver.GetType().BaseType!.GetGenericArguments()[0];
            SlotOf(receiverProjection, "Root").Should().Be(1);
            SlotOf(receiverProjection, "Buyer").Should().Be(2);

            // The applied body is Person, the same CLR type as the Buyer slot: only the slot identity can
            // keep the two occurrences apart (the SQL Server matrix asserts the rendered qualification).
            var applied = receiver.CrossApply(
                p => people.Where(x => x.Id == p.Buyer.Id), Alias.Approver);

            var appliedProjection = applied.GetType().BaseType!.GetGenericArguments()[0];
            SlotOf(appliedProjection, "Root").Should().Be(1);
            SlotOf(appliedProjection, "Buyer").Should().Be(2);
            SlotOf(appliedProjection, "Approver").Should().Be(3);

            // Every named member carries its explicit slot; the inherited ItemN members still address all
            // three slots, so the named and positional views of the same row agree.
            appliedProjection.GetProperties()
                .Select(property => (Property: property, Slot: property.GetCustomAttribute<JoinSlotAttribute>()))
                .Where(entry => entry.Slot is not null)
                .Select(entry => (entry.Property.Name, entry.Slot!.Position))
                .Should().BeEquivalentTo([("Root", 1), ("Buyer", 2), ("Approver", 3)]);

            appliedProjection.GetProperty("Item1").Should().NotBeNull();
            appliedProjection.GetProperty("Item2").Should().NotBeNull();
            appliedProjection.GetProperty("Item3").Should().NotBeNull();
        });
    }
}
