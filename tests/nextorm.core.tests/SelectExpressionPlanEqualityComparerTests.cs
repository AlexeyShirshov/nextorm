using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Plan-key coverage for <see cref="SelectExpressionPlanEqualityComparer"/>: two projections identical
/// except for their physical column name must compare unequal and hash apart, so the dynamic-columns
/// store can skip a mapped column by its physical name without sharing a cached plan.
/// </summary>
public class SelectExpressionPlanEqualityComparerTests
{
    [Fact]
    public void Comparer_ShouldDistinguishPhysicalColumnNames()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = new SelectExpression(typeof(string))
        {
            Index = 0,
            PropertyName = "Name",
            PhysicalColumnName = "name",
            ProviderType = typeof(string),
        };
        var second = new SelectExpression(typeof(string))
        {
            Index = 0,
            PropertyName = "Name",
            PhysicalColumnName = "other_name",
            ProviderType = typeof(string),
        };

        comparer.Equals(first, first).Should().BeTrue();
        comparer.Equals(first, second).Should().BeFalse();
        comparer.GetHashCode(first).Should().NotBe(comparer.GetHashCode(second));
    }
}
