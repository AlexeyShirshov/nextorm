using FluentAssertions;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Locks the intentional fail-closed contract of the generated alias members: they are compile-time
/// slot names that only make sense inside a <c>Select</c>/<c>Where</c> expression tree, where the
/// translator rewrites them onto the right joined table. Reading one as an ordinary CLR property
/// (outside an expression tree) gets no table to resolve against, so the generated getter throws
/// <see cref="NotSupportedException"/> rather than returning a defaulted value. The retained
/// positional <c>ItemN</c> members stay real properties and do not throw.
/// </summary>
public class AliasExpressionOnlyContractTests
{
    [Fact]
    public void Reading_an_alias_member_outside_an_expression_tree_throws()
    {
        var projection = new AliasProjection_Buyer_Approver<Order, Person, Person>();

        // ItemN still behaves like a normal projection member.
        projection.Item1.Should().BeNull();
        projection.Item2.Should().BeNull();
        projection.Item3.Should().BeNull();

        Action readBuyer = () => _ = projection.Buyer;
        Action readApprover = () => _ = projection.Approver;

        readBuyer.Should().Throw<NotSupportedException>(
            "alias members are expression-only slot names, not materializable values");
        readApprover.Should().Throw<NotSupportedException>(
            "alias members are expression-only slot names, not materializable values");
    }

    [Fact]
    public void Reading_a_single_alias_member_outside_an_expression_tree_throws()
    {
        var projection = new AliasProjection_Buyer<Order, Person>();

        Action readBuyer = () => _ = projection.Buyer;

        readBuyer.Should().Throw<NotSupportedException>();
    }
}
