using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// The in-memory provider cannot project named aliases (there is no table alias to resolve against),
/// so every one of the seven alias operators must fail closed at construction with
/// <see cref="NotSupportedException"/> instead of silently producing wrong rows.
/// </summary>
public class AliasInMemoryRefusalTests
{
    [Fact]
    public void All_seven_alias_operators_are_refused_by_the_in_memory_provider()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();
        var people = ctx.From<Person>();

        AssertRefused(() => orders.Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer));
        AssertRefused(() => orders.LeftJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer));
        AssertRefused(() => orders.RightJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer));
        AssertRefused(() => orders.FullJoin<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer));
        AssertRefused(() => orders.CrossJoin(people, Alias.Buyer));
        AssertRefused(() => orders.CrossApply(people, Alias.Buyer));
        AssertRefused(() => orders.OuterApply(people, Alias.Buyer));
    }

    /// <summary>
    /// A root alias (<c>.WithAlias(Alias.Root)</c>) names slot 1 through the same alias-projection
    /// machinery, so it must fail closed on the in-memory provider even without any join.
    /// </summary>
    [Fact]
    public void A_root_WithAlias_is_refused_by_the_in_memory_provider()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();

        Action act = () => orders.WithAlias(Alias.Root);

        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// The alias refusal is not bypassed by a preceding positional join: the alias step itself is
    /// refused at construction, so a positional-prefix -&gt; alias chain never yields a partial result.
    /// </summary>
    [Fact]
    public void An_alias_join_after_a_positional_prefix_is_refused_by_the_in_memory_provider()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();
        var people = ctx.From<Person>();

        // A positional join is perfectly fine in memory; the alias step that follows must fail closed.
        var positionalPrefix = orders.Join(people, (o, p) => o.BuyerId == p.Id);

        Action act = () => positionalPrefix.Join<Person>(people, (p, a) => p.Item2.Id == a.Id, Alias.Approver);

        act.Should().Throw<NotSupportedException>();
    }

    private static void AssertRefused(Action action)
        => action.Should().Throw<NotSupportedException>();

    // ---------------------------------------------------------------------------------------------
    // #322: the source-less alias form (no `EntityBuilder<Person>` argument) is a named-alias
    // projection too, so the in-memory provider must refuse it at construction — it must never
    // silently degrade to a positional join because the caller did not hand over a source.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Source_less_alias_joins_are_refused_by_the_in_memory_provider_without_a_positional_fallback()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();
        var callbacks = 0;
        Action<JoinOptions> options = _ => callbacks++;

        Action first = () => orders.Join<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer, options);
        first.Should().Throw<NotSupportedException>().WithMessage("*in-memory provider*");
        AssertRefused(() => orders.LeftJoin<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer, options));
        AssertRefused(() => orders.RightJoin<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer, options));
        AssertRefused(() => orders.FullJoin<Person>((a, b) => a.BuyerId == b.Id, Alias.Buyer, options));

        callbacks.Should().Be(0, "the refusal is raised before the join options are applied");
        orders.Joins.Should().BeNull("the in-memory refusal must not build a positional join as a fallback");
    }

    [Fact]
    public void A_null_source_less_condition_is_reported_before_the_in_memory_refusal()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();
        var callbacks = 0;
        Action<JoinOptions> options = _ => callbacks++;
        Expression<Func<Order, Person, bool>>? condition = null;

        Action inner = () => orders.Join<Person>(condition!, Alias.Buyer, options);
        Action left = () => orders.LeftJoin<Person>(condition!, Alias.Buyer, options);
        Action right = () => orders.RightJoin<Person>(condition!, Alias.Buyer, options);
        Action full = () => orders.FullJoin<Person>(condition!, Alias.Buyer, options);

        // A06 precedence: the argument guard wins over the provider capability check, for every operator.
        foreach (var act in new[] { inner, left, right, full })
        {
            act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("joinCondition");
        }

        callbacks.Should().Be(0, "the guard runs before the options callback and before any provider check");
        orders.Joins.Should().BeNull("the rejected call must not mutate the receiver");
    }
}
