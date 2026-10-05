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

    private static void AssertRefused(Action action)
        => action.Should().Throw<NotSupportedException>();
}
