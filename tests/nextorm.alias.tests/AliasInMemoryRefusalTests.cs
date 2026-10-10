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
}
